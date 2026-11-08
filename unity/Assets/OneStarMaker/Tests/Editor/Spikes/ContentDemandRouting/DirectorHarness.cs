#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.AssetManagement.Internal;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.UISystem;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using AssetManagementImpl = OneStarMaker.Runtime.AssetManagement.AssetManagement;

namespace OneStarMaker.Tests.Editor.Spikes.ContentDemandRouting
{
    /// <summary>
    /// Play 中の OSM 層の構成（U-b2）。製品の `SceneDirector` と `AssetManagement` を、fixture の `SceneResourceMap` と
    /// 実験の `RoutingBackend` で組む。`AbstractApplicationInitializer` は Game 側の派生を要するので使わず、
    /// その `RegisterAlreadyLoadedScenes`（ロード済み Scene のうち map に名前がある物を `AddScene`）を同じ順序で再現する。
    /// 実物との差（protected config、Bootstrap の段の順序、封じの時点）は HANDOFF §6 に書く。
    ///
    /// `SceneDirector` は reflection で組み・呼ぶ。この test asmdef は Foundation と R3 を参照せず
    /// （asmdef 参照を足さない制約、N6）、`AddScene` / `UnloadScene` の既定引数（Foundation の `TelemetryLevel`）や
    /// `Observable` の公開面へ直接束縛できないため。`TestableSceneDirector` は別の test asmdef にあり、ここから使えない。
    ///
    /// 所有の 3 層（HANDOFF §2 用語）の観測口: (a) SceneDirector の参加は `ISceneQuery.GetLoadedScene` と lifecycle の状態、
    /// (c) AssetManagement の scene 台帳は private な registry を reflection で読む。(b) unload の経路は
    /// `RoutingBackend` の unload の行と `SceneManager.sceneUnloaded` の行の組で判定担当が読む。
    /// </summary>
    internal sealed class DirectorHarness
    {
        private static readonly Type DirectorType = typeof(SceneResourceMap).Assembly.GetType("OneStarMaker.Runtime.SceneSystem.SceneDirector", true)!;
        private readonly object _director;
        private readonly ExperimentRecord _record;
        private readonly Cell _cell;
        private readonly GameObject _uiHost;
        private bool _stopped;

        private DirectorHarness(object director, AssetManagementImpl assets, CountingAddressablesBackend addressables,
            RoutingBackend? routing, SceneResourceMap map, GameObject uiHost, ExperimentRecord record, Cell cell)
        {
            _director = director; Assets = assets; Addressables = addressables; Routing = routing; Map = map; _uiHost = uiHost;
            _record = record; _cell = cell;
        }

        internal AssetManagementImpl Assets { get; }
        internal CountingAddressablesBackend Addressables { get; }
        internal RoutingBackend? Routing { get; }
        internal SceneResourceMap Map { get; }
        internal bool Stopped => _stopped;

        /// <summary>Play 中だけ呼ぶ。`identities` は fixture の map に載せる論理 Scene identity。</summary>
        internal static DirectorHarness Create(IReadOnlyCollection<string> identities, IReadOnlyDictionary<string, string> sceneGuids,
            RoutingBackend? routing, ExperimentRecord record, Cell cell)
        {
            var addressables = new CountingAddressablesBackend();
            var assets = new AssetManagementImpl(addressables);
            if (routing != null) assets.InstallContentDirectory(routing);
            var map = BuildMap(identities, sceneGuids);
            var uiHost = new GameObject("cdr-UICommon");
            var ui = uiHost.AddComponent<UICommon>();
            var ctor = DirectorType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null,
                new[] { typeof(ISceneFactory), typeof(UICommon), typeof(SceneResourceMap), typeof(ILoadingDisplay), typeof(IAssetManagement), typeof(string), typeof(bool) },
                null) ?? throw new MissingMethodException("SceneDirector internal constructor was not found.");
            // useContentDirectory は directory を持つ構成だけ true。機構 (i) でも Next は directory から読むので true になる。
            var director = ctor.Invoke(new object[] { new FixtureSceneFactory(), ui, map, new NullLoadingDisplay(), assets, FixtureFactory.Representation, routing != null });
            record.Write(cell, "director.created", ("identities", identities), ("useContentDirectory", routing != null));
            var harness = new DirectorHarness(director, assets, addressables, routing, map, uiHost, record, cell);
            // 製品の Play 停止は Application.quitting の同期 ReleaseAll から directory の停止へ進む。同じ経路を通す。
            Application.quitting += harness.OnQuitting;
            return harness;
        }

        /// <summary>
        /// 起動の config 確定に相当する時点で Addressables を封じる（U-b4）。最初の Scene の要求より前に呼ぶ。
        /// 呼んだ時刻（frame と seq）を record に残し、封じの前後の load の順序を判定担当が照合できるようにする（AC11）。
        /// </summary>
        internal void Seal()
        {
            Assets.SealAddressables();
            _record.Write(_cell, "assets.sealed", ("addressablesCallsBeforeSeal", Addressables.TotalCalls), ("sealed", Assets.IsAddressablesSealed));
        }

        /// <summary>`RegisterAlreadyLoadedScenes` の再現。ロード済み Scene の順に、map に名前がある物だけを `AddScene` する。</summary>
        internal async UniTask<List<string>> RegisterAlreadyLoadedScenesAsync()
        {
            var registered = new List<string>();
            var skipped = new List<string>();
            var names = new List<string>();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded) names.Add(scene.name);
            }
            foreach (var name in names)
            {
                if (Map.GetSceneResource(name) == null) { skipped.Add(name); continue; }
                await AddSceneAsync(name, "register-already-loaded");
                registered.Add(name);
            }
            _record.Write(_cell, "director.registerAlreadyLoaded", ("registered", registered), ("skipped", skipped));
            return registered;
        }

        internal async UniTask AddSceneAsync(string identity, string reason)
        {
            _record.Write(_cell, "director.addScene.started", ("identity", identity), ("reason", reason));
            try
            {
                await Invoke("AddScene", identity, CancellationToken.None);
                _record.Write(_cell, "director.addScene.completed", ("identity", identity), ("ownership", Ownership(identity)));
            }
            catch (Exception ex)
            {
                _record.Write(_cell, "director.addScene.failed", ("identity", identity), ("error", ex), ("ownership", Ownership(identity)));
                throw;
            }
        }

        internal async UniTask UnloadSceneAsync(string identity)
        {
            var before = Ownership(identity);
            _record.Write(_cell, "director.unloadScene.started", ("identity", identity), ("ownership", before));
            await Invoke("UnloadScene", identity, null);
            _record.Write(_cell, "director.unloadScene.completed", ("identity", identity), ("ownership", Ownership(identity)));
        }

        /// <summary>所有の (a) と (c) の観測。(b) は unload の行で別に残る。</summary>
        internal Dictionary<string, object?> Ownership(string identity)
        {
            var scene = ((ISceneQuery)_director).GetLoadedScene(identity);
            return new Dictionary<string, object?>
            {
                ["directorParticipates"] = scene != null,
                ["lifecycleState"] = scene?.Lifecycle.State.ToString(),
                ["directorScenes"] = DirectorSceneIds(),
                ["assetLedgerScenes"] = AssetLedgerScenes(),
                ["routing"] = Routing?.Describe(),
                ["addressablesCalls"] = Addressables.TotalCalls,
            };
        }

        internal List<string> DirectorSceneIds()
        {
            var ids = new List<string>();
            var field = DirectorType.GetField("_currentScenes", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field?.GetValue(_director) is IDictionary scenes)
                foreach (var key in scenes.Keys) ids.Add(key?.ToString() ?? string.Empty);
            return ids;
        }

        /// <summary>AssetManagement の scene 台帳（identity と、directory の token か否か、unload 済みか）。</summary>
        internal List<string> AssetLedgerScenes()
        {
            var entries = new List<string>();
            var registryField = typeof(AssetManagementImpl).GetField("_registry", BindingFlags.Instance | BindingFlags.NonPublic);
            if (registryField?.GetValue(Assets) is not AssetRegistry registry) { entries.Add("<registry-unavailable>"); return entries; }
            foreach (var scene in registry.GetScenes())
                entries.Add(scene.Identity + "|token=" + (scene.Backend is IContentDirectoryToken ? "directory" : "other") + "|unloaded=" + scene.IsUnloaded);
            return entries;
        }

        /// <summary>製品の Play 停止と同じ順序（ReleaseAll → directory の停止）。Application.quitting か harness が 1 回だけ呼ぶ。</summary>
        internal void StopLikeProduct(string reason)
        {
            if (_stopped) return;
            _stopped = true;
            Application.quitting -= OnQuitting;
            try
            {
                Assets.ReleaseAll();
                Assets.CompleteContentDirectoryPlayStop();
                _record.Write(_cell, "harness.stopped", ("reason", reason), ("routing", Routing?.Describe()));
            }
            catch (Exception ex)
            {
                _record.Write(_cell, "harness.stop.failed", ("reason", reason), ("error", ex), ("routing", Routing?.Describe()));
            }
            try { (_director as IDisposable)?.Dispose(); }
            catch (Exception ex) { _record.Write(_cell, "director.dispose.failed", ("error", ex)); }
            if (_uiHost != null) UnityEngine.Object.Destroy(_uiHost);
        }

        private void OnQuitting() => StopLikeProduct("application-quitting");

        private UniTask Invoke(string methodName, string identity, object? third)
        {
            MethodInfo? method = null;
            foreach (var candidate in DirectorType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (candidate.Name != methodName) continue;
                var parameters = candidate.GetParameters();
                if (parameters.Length > 0 && parameters[0].ParameterType == typeof(string)) { method = candidate; break; }
            }
            if (method == null) throw new MissingMethodException("SceneDirector." + methodName + " was not found.");
            var all = method.GetParameters();
            var args = new object?[all.Length];
            args[0] = identity;
            for (var i = 1; i < all.Length; i++)
            {
                if (all[i].ParameterType == typeof(CancellationToken)) args[i] = third is CancellationToken token ? token : CancellationToken.None;
                else if (all[i].HasDefaultValue) args[i] = all[i].DefaultValue;
                else args[i] = null;
            }
            try { return (UniTask)method.Invoke(_director, args)!; }
            catch (TargetInvocationException ex) when (ex.InnerException != null) { return UniTask.FromException(ex.InnerException); }
        }

        private static SceneResourceMap BuildMap(IReadOnlyCollection<string> identities, IReadOnlyDictionary<string, string> sceneGuids)
        {
            var resources = new List<SceneResource>();
            var descriptionField = typeof(SceneResource).GetField("_sceneAssetDescription", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException("SceneResource._sceneAssetDescription");
            foreach (var identity in identities)
            {
                var resource = ScriptableObject.CreateInstance<SceneResource>();
                resource.name = identity;
                resource.Identity = identity;
                // payload の無い SceneResource は「Unity Scene 実体を持たない論理ノード」として load されない。
                // directory mode では payload の参照は使われず、identity と表現だけで entry を引く。
                var guid = sceneGuids.TryGetValue(identity, out var value) ? value : new string('0', 32);
                descriptionField.SetValue(resource, new SceneAssetDescription(identity, LoadType.OnDemand,
                    new List<AssetPayload> { new(FixtureFactory.Representation, new AssetReference(guid)) }));
                resources.Add(resource);
            }
            var map = ScriptableObject.CreateInstance<SceneResourceMap>();
            var listField = typeof(SceneResourceMap).GetField("_sceneResources", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException("SceneResourceMap._sceneResources");
            listField.SetValue(map, resources);
            map.RebuildDictionary();
            return map;
        }

        private sealed class FixtureSceneFactory : ISceneFactory
        {
            public SceneBase? CreateSceneClass(SceneResource sceneResource, ISceneQuery sceneQuery, ISceneController sceneController)
                => new FixtureScene(sceneResource, sceneQuery, sceneController);
        }

        private sealed class FixtureScene : SceneBase
        {
            internal FixtureScene(SceneResource resource, ISceneQuery query, ISceneController controller) : base(resource, query, controller) { }
        }

        private sealed class NullLoadingDisplay : ILoadingDisplay
        {
            public UniTask Show(LoadingDisplayType displayType, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask Hide(CancellationToken ct) => UniTask.CompletedTask;
        }
    }

    /// <summary>Addressables の代わり。封じた後に呼ばれた回数を数え、封じの後の Addressables の load を検出する（AC11）。</summary>
    internal sealed class CountingAddressablesBackend : IAssetBackend
    {
        internal int TotalCalls;
        public UniTask<IBackendAsset> LoadAssetAsync<T>(string address, CancellationToken ct) where T : UnityEngine.Object
        { TotalCalls++; throw new NotSupportedException("Addressables is not used by the routing spike."); }
        public IBackendAsset LoadAssetSync<T>(string address) where T : UnityEngine.Object
        { TotalCalls++; throw new NotSupportedException("Addressables is not used by the routing spike."); }
        public UniTask<IBackendScene> LoadSceneAsync(string address, SceneLoadOptions options, CancellationToken ct)
        { TotalCalls++; throw new NotSupportedException("Addressables is not used by the routing spike."); }
        public UniTask UnloadSceneAsync(IBackendScene scene, CancellationToken ct) => UniTask.CompletedTask;
        public UniTask<IBackendInstance> InstantiateAsync(string address, Transform? parent, bool worldSpace, CancellationToken ct)
        { TotalCalls++; throw new NotSupportedException("Addressables is not used by the routing spike."); }
        public void Release(IBackendAsset asset) { }
    }
}
