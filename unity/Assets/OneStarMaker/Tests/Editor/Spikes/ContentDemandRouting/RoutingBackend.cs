#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.AssetManagement.Internal;
using OneStarMaker.Runtime.BuildContent;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.Spikes.ContentDemandRouting
{
    /// <summary>
    /// 実験専用の `IContentDirectoryBackend`（HANDOFF §3）。`AssetManagement.InstallContentDirectory` へ 1 回だけ install され、
    /// 内部に native directory（`IContentNativeDirectory`）を直接持つ。既存の `ContentDirectorySession` は使わない。
    /// `ContentRevisionGate` が process に 2 件目の登録を許さず、session を 2 つ持つ構成は作れないため（HANDOFF §2 判定定義の 2 段の証明）。
    ///
    /// 所有する責務: native 登録の順序と解除、論理 identity ごとの解決（同じ identity は先に足した directory、
    /// すなわち snapshot を優先し、Unity の後勝ちの横断探索に委ねない）、load の token の計数、取消の gate、
    /// 停止時の drain と unregister、一方の unregister が失敗しても他方を解除する回収。
    /// 証明しないもの: session の revision lease・rollback・token の安全性、製品が 2 件を install できること。
    /// この型は製品の resolver の候補ではない（層の先取りをしない）。
    ///
    /// gate は harness の偽の遅延で、gate で止めている間も native の load は進む。record には native の完了と
    /// gate の区間を別の行で残し、gate だけの観測から native の取消の安全を判定させない（AC6）。
    /// </summary>
    internal sealed class RoutingBackend : IContentDirectoryBackend
    {
        /// <summary>
        /// 閉じていない backend。Domain Reload 無効では Play を跨いで残るので、次の round の開始時に検出・回収する（N4、AC8）。
        /// Domain Reload 有効では managed の一覧が消えるため、登録の残りは round の状態 file の側で検出する。
        /// </summary>
        internal static readonly List<RoutingBackend> Unclosed = new();

        private readonly List<NativeDirectory> _directories = new();
        private readonly Dictionary<string, UniTaskCompletionSource> _gates = new(StringComparer.Ordinal);
        private readonly HashSet<string> _nativeCompleted = new(StringComparer.Ordinal);
        private readonly ExperimentRecord _record;
        private readonly Cell _cell;
        private readonly string _instanceId = Guid.NewGuid().ToString("N").Substring(0, 8);
        private int _liveTokens;
        private int _pendingOperations;
        private int _abandonedIssued;
        private int _abandonedRecovered;
        private UniTaskCompletionSource? _drained;
        private bool _accepting = true;
        private bool _deferredUnregister;
        private Action? _evictCache;

        internal RoutingBackend(ExperimentRecord record, Cell cell)
        {
            _record = record;
            _cell = cell;
            Unclosed.Add(this);
            Write("routing.created");
        }

        public string BuildIdentity => "cdr-routing";
        public string Target => FixtureFactory.TargetName;
        public string CachePrefix => "directory:cdr-routing:" + _instanceId + ":";

        internal int LiveTokens => Volatile.Read(ref _liveTokens);
        internal int PendingOperations => Volatile.Read(ref _pendingOperations);
        /// <summary>取消で caller が受け取らなかった native の Scene のうち、まだ unload で回収していない数（AC6 の未回収 token）。</summary>
        internal int UnrecoveredAbandoned => Volatile.Read(ref _abandonedIssued) - Volatile.Read(ref _abandonedRecovered);
        internal bool IsClosed { get; private set; }
        internal string InstanceId => _instanceId;

        internal IReadOnlyList<NativeDirectory> Directories => _directories;

        /// <summary>identity の native load が 1 回以上 Unity 側で完了したか（gate の区間と区別する観測）。</summary>
        internal bool HasNativeCompleted(string identity) => _nativeCompleted.Contains(identity);

        /// <summary>directory を 1 つ登録する。先に足した物ほど解決の優先度が高い。失敗した時は登録を残さない。</summary>
        internal NativeDirectory Add(string name, string origin, BuiltDirectory built, IContentNativeDirectory? native = null)
        {
            native ??= new UnityContentDirectoryBackend();
            var directory = new NativeDirectory(name, origin, built.path, built.identity, native, _directories.Count);
            if (!native.Register(built.path))
                throw new ContentDirectoryException(ContentDirectoryFailureCode.RegistrationFailed, "Native registration returned an invalid handle: " + name);
            directory.Registered = true;
            try
            {
                var roots = native.GetRoots();
                if (roots.Length != 1 || roots[0] == null)
                    throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidRoot, "Directory must contain exactly one BuildContentRoot: " + name);
                if (!ContentDirectoryIndex.TryCreate(roots[0], built.identity, Target, out var index, out var issue))
                    throw new ContentDirectoryException(issue.Code, issue.Message + " (" + name + ")");
                directory.Index = index!;
            }
            catch
            {
                native.Unregister();
                directory.Registered = false;
                throw;
            }
            _directories.Add(directory);
            Write("routing.registered", ("directory", name), ("origin", origin), ("path", built.path), ("order", directory.Order),
                ("registeredCount", RegisteredCount()));
            return directory;
        }

        /// <summary>identity の native load の完了を止める gate を閉じる。<see cref="OpenGate"/> まで caller への完了の通知を保留する。</summary>
        internal void CloseGate(string identity)
        {
            _gates[identity] = new UniTaskCompletionSource();
            Write("routing.gate.closed", ("identity", identity));
        }

        internal void OpenGate(string identity)
        {
            if (!_gates.TryGetValue(identity, out var gate)) return;
            _gates.Remove(identity);
            Write("routing.gate.opened", ("identity", identity));
            gate.TrySetResult();
        }

        internal void OpenAllGates()
        {
            foreach (var identity in new List<string>(_gates.Keys)) OpenGate(identity);
        }

        public AssetKey GetObjectKey(string logicalKey, string representation)
            => throw new ContentDirectoryException(ContentDirectoryFailureCode.EntryMissing, "The routing spike serves scenes only.", BuildIdentity, Target, logicalKey, representation);

        public UniTask<IBackendAsset> LoadAssetAsync<T>(string logicalKey, string representation, CancellationToken ct) where T : UnityEngine.Object
            => throw new ContentDirectoryException(ContentDirectoryFailureCode.EntryMissing, "The routing spike serves scenes only.", BuildIdentity, Target, logicalKey, representation);

        public UniTask<IBackendInstance> InstantiateAsync(string logicalKey, string representation, Transform? parent, bool worldSpace, CancellationToken ct)
            => throw new ContentDirectoryException(ContentDirectoryFailureCode.EntryMissing, "The routing spike serves scenes only.", BuildIdentity, Target, logicalKey, representation);

        public async UniTask<IBackendScene> LoadSceneAsync(string sceneIdentity, string representation, SceneLoadOptions options, CancellationToken ct)
        {
            EnsureAccepting();
            ct.ThrowIfCancellationRequested();
            var (directory, entry) = ResolveScene(sceneIdentity, representation);
            BeginOperation();
            var ownsCounter = true;
            try
            {
                // caller の取消は Unity の load 自体を止めない。遅れて完了した Scene を回収するまで unregister しない。
                var operation = LoadThroughGate(directory, entry, sceneIdentity, options).Preserve();
                IBackendScene scene;
                try { scene = await operation.AttachExternalCancellation(ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    ownsCounter = false;
                    Interlocked.Increment(ref _abandonedIssued);
                    Write("routing.load.canceled", ("identity", sceneIdentity), ("directory", directory.Name));
                    DrainAbandoned(directory, operation, sceneIdentity).Forget();
                    throw;
                }
                Interlocked.Increment(ref _liveTokens);
                Write("routing.token.issued", ("identity", sceneIdentity), ("directory", directory.Name), ("origin", directory.Origin),
                    ("liveTokens", LiveTokens));
                return new RoutedScene(this, directory, scene, sceneIdentity);
            }
            catch (ContentDirectoryException) { throw; }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                Write("routing.load.failed", ("identity", sceneIdentity), ("directory", directory.Name), ("error", ex));
                throw new ContentDirectoryException(ContentDirectoryFailureCode.OperationFailed, "Routed scene load failed.", BuildIdentity, Target, sceneIdentity, representation, ex);
            }
            finally { if (ownsCounter) CompleteOperation(); }
        }

        public UniTask UnloadSceneAsync(IBackendScene scene)
            => scene is RoutedScene routed ? routed.UnloadAsync() : UniTask.CompletedTask;

        public void ReleaseSceneAfterUnityShutdown(IBackendScene scene)
        {
            if (scene is RoutedScene routed) routed.ReleaseTokenOnly("after-unity-shutdown");
        }

        public void ReleaseSceneTokenAfterPlayStop(IBackendScene scene)
        {
            if (scene is RoutedScene routed) routed.ReleaseTokenOnly("after-play-stop");
        }

        public void Release(IBackendAsset asset) { }

        public void ConfigureCacheEviction(Action evictRevisionEntries) => _evictCache = evictRevisionEntries;

        public async UniTask StopAndDrainAsync()
        {
            _accepting = false;
            var drained = _drained;
            if (PendingOperations != 0 && drained != null) await drained.Task;
        }

        public void EnsureAccepting()
        {
            if (!_accepting || IsClosed)
                throw new ContentDirectoryException(ContentDirectoryFailureCode.DirectoryNotRegistered, "Routing backend is not accepting loads.", BuildIdentity, Target);
        }

        public async UniTask CloseAsync()
        {
            await StopAndDrainAsync();
            _evictCache?.Invoke();
            if (LiveTokens != 0)
                throw new ContentDirectoryException(ContentDirectoryFailureCode.ResourcesInUse, "Routing backend still has live scene tokens.", BuildIdentity, Target);
            UnregisterAll("close");
        }

        /// <summary>
        /// Play 停止。製品の session と同じく native の unload を待たない（quit 中の同期待ちは死鎖しうる）。
        /// 製品と違い、load の terminal を待っている operation があれば unregister をその drain の後へ遅らせ、
        /// token の回収より先に登録を解除しない（AC7 (b)）。遅らせた解除が起きなければ、次の round の開始時に検出される。
        /// </summary>
        public void CompletePlayStop()
        {
            _accepting = false;
            _evictCache?.Invoke();
            if (PendingOperations == 0) { UnregisterAll("play-stop"); return; }
            _deferredUnregister = true;
            Write("routing.playstop.deferred", ("pendingOperations", PendingOperations), ("liveTokens", LiveTokens),
                ("unrecoveredAbandoned", UnrecoveredAbandoned));
        }

        public void BeginSynchronousShutdown()
        {
            _accepting = false;
            if (PendingOperations == 0 && LiveTokens == 0) UnregisterAll("synchronous-shutdown");
        }

        /// <summary>次の round の開始時に見つかった、閉じていない backend の回収（N4）。gate を開け、全ての登録を外す。</summary>
        internal void ReclaimFromPreviousRound()
        {
            _accepting = false;
            OpenAllGates();
            UnregisterAll("reclaimed-by-next-round");
        }

        internal Dictionary<string, object?> Describe()
        {
            var directories = new List<object?>();
            foreach (var directory in _directories)
                directories.Add(new Dictionary<string, object?>
                {
                    ["name"] = directory.Name, ["origin"] = directory.Origin, ["registered"] = directory.Registered,
                    ["order"] = directory.Order, ["path"] = directory.Path,
                });
            return new Dictionary<string, object?>
            {
                ["instance"] = _instanceId, ["closed"] = IsClosed, ["accepting"] = _accepting, ["liveTokens"] = LiveTokens,
                ["pendingOperations"] = PendingOperations, ["unrecoveredAbandoned"] = UnrecoveredAbandoned,
                ["deferredUnregister"] = _deferredUnregister, ["directories"] = directories,
            };
        }

        private (NativeDirectory Directory, BuildContentEntry Entry) ResolveScene(string identity, string representation)
        {
            NativeDirectory? chosen = null;
            BuildContentEntry? chosenEntry = null;
            var shadowed = new List<string>();
            foreach (var directory in _directories)
            {
                if (!directory.Registered || !directory.Index!.TryResolveScene(identity, representation, out var entry, out _)) continue;
                if (chosen == null) { chosen = directory; chosenEntry = entry; }
                else shadowed.Add(directory.Name);
            }
            Write("routing.resolve", ("identity", identity), ("chosen", chosen?.Name), ("chosenOrigin", chosen?.Origin), ("shadowed", shadowed));
            if (chosen == null || chosenEntry == null)
                throw new ContentDirectoryException(ContentDirectoryFailureCode.EntryMissing, "No routed directory has the scene.", BuildIdentity, Target, identity, representation);
            return (chosen, chosenEntry);
        }

        private async UniTask<IBackendScene> LoadThroughGate(NativeDirectory directory, BuildContentEntry entry, string identity, SceneLoadOptions options)
        {
            Write("routing.native.started", ("identity", identity), ("directory", directory.Name));
            var scene = await directory.Native.LoadSceneAsync(entry, options);
            _nativeCompleted.Add(identity);
            Write("routing.native.completed", ("identity", identity), ("directory", directory.Name), ("sceneName", scene.Name));
            if (_gates.TryGetValue(identity, out var gate))
            {
                Write("routing.gate.holding", ("identity", identity));
                await gate.Task;
                Write("routing.gate.released", ("identity", identity));
            }
            return scene;
        }

        private async UniTaskVoid DrainAbandoned(NativeDirectory directory, UniTask<IBackendScene> operation, string identity)
        {
            try
            {
                var scene = await operation;
                await directory.Native.UnloadSceneAsync(scene);
                Write("routing.abandoned.recovered", ("identity", identity), ("directory", directory.Name));
            }
            catch (Exception ex)
            {
                Write("routing.abandoned.failed", ("identity", identity), ("directory", directory.Name), ("error", ex));
            }
            finally
            {
                Interlocked.Increment(ref _abandonedRecovered);
                CompleteOperation();
            }
        }

        private void BeginOperation()
        {
            if (Interlocked.Increment(ref _pendingOperations) == 1) _drained = new UniTaskCompletionSource();
        }

        private void CompleteOperation()
        {
            if (Interlocked.Decrement(ref _pendingOperations) != 0) return;
            _drained?.TrySetResult();
            if (_deferredUnregister) UnregisterAll("deferred-after-drain");
        }

        private void ReleaseToken(RoutedScene scene, string reason)
        {
            Interlocked.Decrement(ref _liveTokens);
            Write("routing.token.released", ("identity", scene.Identity), ("directory", scene.Directory.Name), ("reason", reason),
                ("liveTokens", LiveTokens));
        }

        // 全ての登録を後から足した順に外す。1 つの解除の失敗で他の解除を止めない。
        private void UnregisterAll(string reason)
        {
            _deferredUnregister = false;
            var allReleased = true;
            for (var i = _directories.Count - 1; i >= 0; i--)
            {
                var directory = _directories[i];
                if (!directory.Registered) continue;
                try
                {
                    directory.Native.Unregister();
                    directory.Registered = false;
                    Write("routing.unregistered", ("directory", directory.Name), ("reason", reason), ("liveTokens", LiveTokens),
                        ("pendingOperations", PendingOperations), ("unrecoveredAbandoned", UnrecoveredAbandoned));
                }
                catch (Exception ex)
                {
                    allReleased = false;
                    Write("routing.unregister.failed", ("directory", directory.Name), ("reason", reason), ("error", ex));
                }
            }
            if (!allReleased) return;
            IsClosed = true;
            Unclosed.Remove(this);
            Write("routing.closed", ("reason", reason));
        }

        private int RegisteredCount()
        {
            var count = 0;
            foreach (var directory in _directories) if (directory.Registered) count++;
            return count;
        }

        private void Write(string kind, params (string Key, object? Value)[] fields)
        {
            var all = new (string, object?)[fields.Length + 1];
            all[0] = ("routing", _instanceId);
            Array.Copy(fields, 0, all, 1, fields.Length);
            _record.Write(_cell, kind, all);
        }

        internal sealed class NativeDirectory
        {
            internal NativeDirectory(string name, string origin, string path, string identity, IContentNativeDirectory native, int order)
            { Name = name; Origin = origin; Path = path; Identity = identity; Native = native; Order = order; }

            internal string Name { get; }
            internal string Origin { get; }
            internal string Path { get; }
            internal string Identity { get; }
            internal IContentNativeDirectory Native { get; }
            internal int Order { get; }
            internal ContentDirectoryIndex? Index { get; set; }
            internal bool Registered { get; set; }
        }

        /// <summary>AssetManagement の scene 台帳に載る token。unload は 1 回だけ native に回し、成功した時に token を返す。</summary>
        private sealed class RoutedScene : IBackendScene, IContentDirectoryToken
        {
            private readonly RoutingBackend _owner;
            private IBackendScene? _inner;
            private UniTask? _unloading;

            internal RoutedScene(RoutingBackend owner, NativeDirectory directory, IBackendScene inner, string identity)
            { _owner = owner; Directory = directory; _inner = inner; Identity = identity; }

            internal NativeDirectory Directory { get; }
            internal string Identity { get; }
            public bool IsLoaded => _inner?.IsLoaded == true;
            public string Name => _inner?.Name ?? string.Empty;
            public GameObject[] GetRootGameObjects() => _inner?.GetRootGameObjects() ?? Array.Empty<GameObject>();

            internal UniTask UnloadAsync()
            {
                if (_inner == null) return UniTask.CompletedTask;
                _unloading ??= UnloadCore().Preserve();
                return _unloading.Value;
            }

            private async UniTask UnloadCore()
            {
                var inner = _inner!;
                _owner.Write("routing.unload.started", ("identity", Identity), ("directory", Directory.Name));
                try
                {
                    await Directory.Native.UnloadSceneAsync(inner);
                }
                catch (Exception ex)
                {
                    _unloading = null;
                    _owner.Write("routing.unload.failed", ("identity", Identity), ("directory", Directory.Name), ("error", ex));
                    throw;
                }
                _inner = null;
                _owner.ReleaseToken(this, "unloaded");
            }

            internal void ReleaseTokenOnly(string reason)
            {
                if (_inner == null) return;
                _inner = null;
                _owner.ReleaseToken(this, reason);
            }
        }
    }
}
