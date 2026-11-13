#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.AssetManagement.Internal;
using OneStarMaker.Runtime.BuildContent;
using Unity.Loading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using E = OneStarMaker.Tests.Editor.Spikes.OverlayRegistration.OverlayExpected;
using V = OneStarMaker.Tests.Editor.Spikes.OverlayRegistration.OverlayVerdict;
using Object = UnityEngine.Object;

namespace OneStarMaker.Tests.Editor.Spikes.OverlayRegistration
{
    /// <summary>
    /// 第 2 process（HANDOFF §2.2 AC2〜AC7）。第 1 process の出力を照合し、round を 1 つずつ新しい Play で行う。test は最初の round だけが
    /// 違う 5 つと、単独の登録の round だけを行う条件付きの 2 つで、それぞれ別の Unity process で 1 回ずつ実行する（round の並びは
    /// <see cref="OverlayVerdict.Plan"/>）。登録は製品の UnityContentDirectoryBackend を無改変で 2 つ持って行い、両方を登録してから load する。
    /// test が assert するのは harness の成立だけ（全 round の開始行と終了行、後始末、log の購読と LogAssert の復元、record）で、
    /// 答えの向きは assert しない（HANDOFF §2）。Unity の log は test の失敗にせず、全件を round と呼び出しの phase に結びつけて record に書く。
    /// 前提: Enter Play Mode で Domain Reload が無効（本 project の既定）。有効だと test の enumerator の位置と局所変数が Play の出入りで
    /// 失われるので、開始時に確かめて止める。
    /// </summary>
    public sealed class OverlayRounds
    {
        [UnityTest, Explicit, Timeout(7200000)] public IEnumerator ClassFirst() => Run(V.ClassFirst);
        [UnityTest, Explicit, Timeout(7200000)] public IEnumerator LifetimeFirst() => Run(V.LifetimeFirst);
        [UnityTest, Explicit, Timeout(7200000)] public IEnumerator ReadFirst() => Run(V.ReadFirst);
        [UnityTest, Explicit, Timeout(7200000)] public IEnumerator ControlClassFirst() => Run(V.ControlClassFirst);
        [UnityTest, Explicit, Timeout(7200000)] public IEnumerator ControlReadFirst() => Run(V.ControlReadFirst);
        // 条件付き（HANDOFF §5）: 主の順の登録か load を Unity が拒否した時だけ実行し、同じ呼び出しが単独の登録の最初の Play で成功するかを見る。
        [UnityTest, Explicit, Timeout(7200000)] public IEnumerator BaseAloneFirst() => Run(V.BaseAloneFirst);
        [UnityTest, Explicit, Timeout(7200000)] public IEnumerator OverlayAloneFirst() => Run(V.OverlayAloneFirst);

        private static IEnumerator Run(string process)
        {
            var s = Session.Start(process);
            try
            {
                var plan = V.Plan(process);
                for (var i = 0; i < plan.Length; i++)
                {
                    var round = plan[i];
                    var index = i;
                    s.BeforePlay(round);
                    yield return new EnterPlayMode(false);
                    yield return UniTask.ToCoroutine(() => s.PlayAsync(round, index));
                    yield return new ExitPlayMode();
                    s.AfterPlay(round);
                }
            }
            finally { s.Finish(); }
            Assert.That(s.HarnessFailures, Is.Empty, string.Join("\n", s.HarnessFailures));
        }

        private sealed class Dir
        {
            public Dir(string name) => Name = name;
            public string Name { get; }
            public UnityContentDirectoryBackend Backend { get; } = new UnityContentDirectoryBackend();
        }

        private sealed class Session
        {
            private static readonly string[] FixtureNames = { E.SharedTex, E.SharedMat, E.ShaderObjectName, E.EditedTex, E.EditedMat, E.KeepTex, E.KeepMat,
                E.SharedPrefab, E.Keeper, E.MarkerV1, E.MarkerV2 };
            private SpikeRun _run = null!;
            private Aftercare _care = null!;
            private FirstFacts _first = new FirstFacts();
            private readonly ProcessObservation _process = new ProcessObservation();
            private SceneSetup[] _setup = Array.Empty<SceneSetup>();
            private SceneAsset? _startScene;
            private string _envKey = "";
            private string? _envPrevious;
            private bool _ignoreBefore, _capturing;
            private readonly object _lock = new object();
            private string _phase = "";
            private readonly List<(string phase, string type, string message, bool main)> _pending = new List<(string, string, string, bool)>();
            private int _mainThread;
            // round ごとの状態。Play の中でだけ使い、cleanup で空にする。
            private readonly List<Dir> _dirs = new List<Dir>();
            private readonly List<(Dir dir, IBackendAsset token)> _assets = new List<(Dir, IBackendAsset)>();
            private readonly List<(Dir dir, IBackendScene scene)> _scenes = new List<(Dir, IBackendScene)>();
            private RoundObservation _obs = new RoundObservation();

            public List<string> HarnessFailures { get; } = new List<string>();

            public static Session Start(string process)
            {
                if (!File.Exists(SpikeRun.FirstPointer)) throw new InvalidOperationException("FirstProcess has not written its record.");
                var s = new Session { _mainThread = Thread.CurrentThread.ManagedThreadId };
                s._run = SpikeRun.Open(File.ReadAllText(SpikeRun.FirstPointer).Trim(), process);
                s._run.WriteScope();
                s._first = JsonUtility.FromJson<FirstFacts>(File.ReadAllText(Path.Combine(s._run.Directory, "first-facts.json")));
                var p = s._process;
                p.Process = process; p.RunId = s._run.RunId; p.Head = s._run.Head;
                p.DomainReloadDisabled = EditorSettings.enterPlayModeOptionsEnabled && (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != 0;
                p.TargetOk = EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64 && EditorUserBuildSettings.standaloneBuildSubtarget == StandaloneBuildSubtarget.Player;
                // 第 1 process の同じ実行の出力か（実行 id、head、棚卸しを取り直した digest。HANDOFF AC2）。
                var digests = OverlayBuilds.Directories.Select(d => d + "=" + OverlayBuilds.Digest(OverlayBuilds.Inventory(OverlayBuilds.Output(s._run, d)))).ToArray();
                p.InputMatched = s._first.RunId == s._run.RunId && s._first.Head == s._run.Head && digests.SequenceEqual(s._first.Digests);
                s._run.Write("second-start", ("plan", V.Plan(process)), ("firstHead", s._first.Head), ("digests", digests), ("firstDigests", s._first.Digests),
                    ("inputMatched", p.InputMatched), ("domainReloadDisabled", p.DomainReloadDisabled), ("targetOk", p.TargetOk));
                if (!p.DomainReloadDisabled) throw new InvalidOperationException("The second process requires Enter Play Mode with Domain Reload disabled.");
                s._care = Aftercare.Begin(s._run);
                for (var i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("An open scene has unsaved changes.");
                // Play は空の untitled Scene から始める。Play の初期 Scene の設定を退避して外し、Finish で戻す。
                s._setup = EditorSceneManager.GetSceneManagerSetup();
                s._startScene = EditorSceneManager.playModeStartScene;
                EditorSceneManager.playModeStartScene = null;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                s.QuietGameBootstrap();
                return s;
            }

            public void BeforePlay(string round)
            {
                _ignoreBefore = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;
                _capturing = true;
                lock (_lock) _phase = round + "/enter-play";
                Application.logMessageReceivedThreaded += OnLog;
                _run.Write("log-capture", ("round", round), ("subscribed", true), ("ignoreFailingMessagesBefore", _ignoreBefore));
            }

            public void AfterPlay(string round)
            {
                Phase(round + "/exit-play");
                Flush();
                Application.logMessageReceivedThreaded -= OnLog;
                LogAssert.ignoreFailingMessages = _ignoreBefore;
                _capturing = false;
                _run.Write("log-capture", ("round", round), ("subscribed", false), ("ignoreFailingMessagesRestored", LogAssert.ignoreFailingMessages));
            }

            public async UniTask PlayAsync(string round, int index)
            {
                _obs = new RoundObservation { Round = round, PlayIndex = index };
                var completed = false;
                try
                {
                    if (!Application.isPlaying) throw new InvalidOperationException("EnterPlayMode returned without entering Play.");
                    _obs.DirectoriesAtStart = ContentLoadManager.GetContentDirectories().Length;
                    var leftovers = FixtureNames.Select(n => n + "=" + Enumerate<Object>(n).Count).ToArray();
                    _obs.LeftoverAtStart = FixtureNames.Sum(n => Enumerate<Object>(n).Count);
                    _obs.SourceInAssetDatabase = AssetDatabase.IsValidFolder(OverlayFixture.Folder)
                        || AssetDatabase.AssetPathToGUID(OverlayFixture.SceneEdit, AssetPathToGUIDOptions.OnlyExistingAssets).Length != 0;
                    _run.Write("round-start", ("round", round), ("playIndex", index), ("registerOrder", V.RegisterOrder(round)), ("directories", _obs.DirectoriesAtStart),
                        ("leftovers", leftovers), ("sourceInAssetDatabase", _obs.SourceInAssetDatabase));
                    switch (round)
                    {
                        case V.Ac3: case V.Ac6Read: await ReadAsync(); break;
                        case V.Ac4: case V.Ac6Class: await ClassAsync(); break;
                        case V.Ac5: await LifetimeAsync(); break;
                        default: await AloneAsync(); break;
                    }
                    completed = true;
                }
                catch (Exception ex)
                {
                    HarnessFailures.Add(round + ": " + ex.Message);
                    _run.Write("round-abort", ("round", round), ("error", ex.ToString()));
                }
                finally
                {
                    await CleanupAsync();
                }
                if (completed)
                {
                    _obs.Ended = true;
                    _run.Write("round-end", ("round", round), ("playIndex", index), ("cleanedUp", _obs.CleanedUp), ("errorLogs", _obs.ErrorLogs), ("errorLogsInRelease", _obs.ErrorLogsInRelease));
                }
                if (!_obs.CleanedUp) HarnessFailures.Add(round + ": cleanup did not finish");
                _process.Rounds.Add(_obs);
            }

            // ───── AC3 と AC6-read: 読まれる側（Q1） ─────
            private async UniTask ReadAsync()
            {
                var dirs = RegisterAll();
                if (dirs == null) return;
                var b = dirs[V.Base];
                var o = dirs[V.Overlay];
                _obs.SceneIdsEqual = Entry(b, E.SceneEdit).SceneId.Equals(Entry(o, E.SceneEdit).SceneId);
                _obs.LoadableIdsEqual = Entry(b, E.EditedTex).Object!.LoadableObjectId.Equals(Entry(o, E.EditedTex).Object!.LoadableObjectId);
                // SceneEdit を base の root の id、上書きの root の id の順に 1 回ずつ load して版を読み、unload する。
                foreach (var d in new[] { b, o })
                {
                    var (scene, call) = await LoadSceneAsync(d, E.SceneEdit);
                    if (scene == null) continue;
                    var marker = Marker(scene);
                    call.Version = E.VersionOfMarker(marker == null ? "" : marker.name);
                    Write("version", ("call", call.Call), ("objectName", marker == null ? null : marker.name), ("version", call.Version));
                    await UnloadSceneAsync(scene);
                }
                await Resources.UnloadUnusedAssets();
                // EditedTex を両方の root の Loadable から load して両方を保持する。
                var held = new List<Texture2D?>();
                foreach (var d in new[] { b, o })
                {
                    var (tex, call) = await LoadObjectAsync<Texture2D>(d, E.EditedTex);
                    held.Add(tex);
                    if (tex == null) continue;
                    call.Version = E.VersionOfPixels(Pixels(tex));
                    Write("version", ("call", call.Call), ("entityId", Id(tex)), ("pixels", Pixels(tex)), ("version", call.Version));
                }
                _obs.Counts.Add(new CountObservation { Item = E.EditedTex, LoadIds = held.Select(Id).ToList(), EnumeratedIds = Enumerate<Texture2D>(E.EditedTex) });
                Write("read", ("sceneIdsEqual", _obs.SceneIdsEqual), ("loadableIdsEqual", _obs.LoadableIdsEqual), ("counts", CountLines()),
                    ("sceneIds", new[] { Entry(b, E.SceneEdit).SceneId.ToString(), Entry(o, E.SceneEdit).SceneId.ToString() }),
                    ("loadableIds", new[] { Entry(b, E.EditedTex).Object!.LoadableObjectId.ToString(), Entry(o, E.EditedTex).Object!.LoadableObjectId.ToString() }));
            }

            // ───── AC4 と AC6-class: class と重複（Q2・Q3） ─────
            private async UniTask ClassAsync()
            {
                var dirs = RegisterAll();
                if (dirs == null) return;
                var b = dirs[V.Base];
                var o = dirs[V.Overlay];
                // SceneKeep（base の root の id）、SceneEdit（上書きの root の id）の順に additive に load して載せたままにする（HANDOFF §2 の (k)）。
                var (keep, _) = await LoadSceneAsync(b, E.SceneKeep);
                var (edit, _) = await LoadSceneAsync(o, E.SceneEdit);
                var (prefab, _) = await LoadObjectAsync<GameObject>(b, E.SharedPrefab);
                var (tex, _) = await LoadObjectAsync<Texture2D>(o, E.EditedTex);
                var keeper = keep == null ? null : Root(keep, E.Keeper);
                var marker = edit == null ? null : Marker(edit);
                _obs.Probes.Add(ObserveProbe(E.SceneKeep, keeper));
                _obs.Probes.Add(ObserveProbe(E.SceneEdit, marker));
                _obs.Probes.Add(ObserveProbe(E.SharedPrefab, prefab));
                CountAll(keeper, marker, prefab, tex);
                // 観測（答えには使わない）: SceneKeep の Renderer が直接参照する EditedMat の mainTexture の版。
                var keepMaterials = keeper == null ? null : keeper.GetComponent<Renderer>();
                var keepEdited = keepMaterials == null || keepMaterials.sharedMaterials.Length < 2 ? null : keepMaterials.sharedMaterials[1];
                var keepTex = keepEdited == null ? null : keepEdited.mainTexture as Texture2D;
                _obs.KeepEditedTexVersion = keepTex == null ? "null" : E.VersionOfPixels(Pixels(keepTex));
                Write("class", ("probes", _obs.Probes.Select(Describe)), ("counts", CountLines()), ("keepEditedTexVersion", _obs.KeepEditedTexVersion),
                    ("keepEditedTexId", Id(keepTex)), ("loadableEditedTexId", Id(tex)));
            }

            // ───── AC5: 寿命（Q4・Q3） ─────
            private async UniTask LifetimeAsync()
            {
                var dirs = RegisterAll();
                if (dirs == null) return;
                var b = dirs[V.Base];
                var o = dirs[V.Overlay];
                // (1) 上書きの SceneEdit を先に、SceneKeep を後に load する（AC4 と逆の順。HANDOFF §2 の (k)）。
                Phase(_obs.Round + "/(1)");
                var (edit, _) = await LoadSceneAsync(o, E.SceneEdit);
                var (keep, _) = await LoadSceneAsync(b, E.SceneKeep);
                var (prefab, prefabToken) = await LoadHeldAsync<GameObject>(b, E.SharedPrefab);
                var (tex, texToken) = await LoadHeldAsync<Texture2D>(o, E.EditedTex);
                if (edit == null || keep == null || prefab == null || tex == null || prefabToken == null || texToken == null) return;
                var keeper = Root(keep, E.Keeper);
                var marker = Marker(edit);
                CountAll(keeper, marker, prefab, tex);
                Write("counts", ("step", "(1)"), ("counts", CountLines()));
                var held = new Dictionary<string, Held>
                {
                    [E.SceneEdit] = Held.Of("SceneEdit", marker, E.SceneEdit), [E.SceneKeep] = Held.Of("SceneKeep", keeper, E.SceneKeep),
                    [E.SharedPrefab] = Held.Of("SharedPrefab", prefab, E.SharedPrefab), [E.EditedTex] = Held.Of("EditedTex", tex),
                };
                Check("(1)", held);
                // (2) SceneEdit を unload し UnloadUnusedAssets。
                Phase(_obs.Round + "/(2)");
                await UnloadSceneAsync(edit);
                held.Remove(E.SceneEdit);
                await Resources.UnloadUnusedAssets();
                Check("(2)", held);
                // (3) SceneEdit を load し直す。Scene の物は新しい load として扱い、前の load の EntityId と比べない（HANDOFF §2 の「壊れた」）。
                Phase(_obs.Round + "/(3)");
                (edit, _) = await LoadSceneAsync(o, E.SceneEdit);
                if (edit == null) return;
                held[E.SceneEdit] = Held.Of("SceneEdit", Marker(edit), E.SceneEdit);
                Check("(3)", held);
                // (4) SceneKeep を unload し UnloadUnusedAssets。
                Phase(_obs.Round + "/(4)");
                await UnloadSceneAsync(keep);
                held.Remove(E.SceneKeep);
                await Resources.UnloadUnusedAssets();
                Check("(4)", held);
                // (5) SceneKeep を load し直す。
                Phase(_obs.Round + "/(5)");
                (keep, _) = await LoadSceneAsync(b, E.SceneKeep);
                if (keep == null) return;
                held[E.SceneKeep] = Held.Of("SceneKeep", Root(keep, E.Keeper), E.SceneKeep);
                Check("(5)", held);
                // (6) Loadable を Release し、両 Scene を unload し、上書き → base の順に解除する。この間の log は (1)〜(5) と分けて数える。
                Phase(_obs.Round + "/" + V.StepRelease);
                Release(prefabToken!);
                Release(texToken!);
                await UnloadSceneAsync(edit);
                await UnloadSceneAsync(keep);
                o.Backend.Unregister();
                b.Backend.Unregister();
                _dirs.Clear();
                _obs.DirectoriesAfterUnregister = ContentLoadManager.GetContentDirectories().Length;
                Write("release", ("step", V.StepRelease), ("directoriesAfterUnregister", _obs.DirectoriesAfterUnregister), ("listed", Listed()));
            }

            // ───── AC2 と単独の登録の最初の Play（BaseAloneFirst・OverlayAloneFirst） ─────
            private async UniTask AloneAsync()
            {
                var dirs = RegisterAll();
                if (dirs == null) return;
                if (_obs.Round == V.Ac2Base)
                {
                    var d = dirs[V.Base];
                    var (keep, _) = await LoadSceneAsync(d, E.SceneKeep);
                    _obs.Probes.Add(ObserveProbe(E.SceneKeep, keep == null ? null : Root(keep, E.Keeper)));
                    await EditAsync(d);
                    var (prefab, _) = await LoadObjectAsync<GameObject>(d, E.SharedPrefab);
                    _obs.Probes.Add(ObserveProbe(E.SharedPrefab, prefab));
                }
                else await EditAsync(dirs[V.Overlay]);
                Write("alone", ("probes", _obs.Probes.Select(Describe)), ("calls", _obs.Calls.Select(x => x.Call + "=" + x.Succeeded + "/" + x.Version)));
            }

            private async UniTask EditAsync(Dir d)
            {
                var (edit, sceneCall) = await LoadSceneAsync(d, E.SceneEdit);
                var marker = edit == null ? null : Marker(edit);
                _obs.Probes.Add(ObserveProbe(E.SceneEdit, marker));
                if (edit != null) sceneCall.Version = E.VersionOfMarker(marker == null ? "" : marker.name);
                var (tex, texCall) = await LoadObjectAsync<Texture2D>(d, E.EditedTex);
                if (tex != null) texCall.Version = E.VersionOfPixels(Pixels(tex));
            }

            // ───── 登録・load・解放の部品 ─────

            // round の定義の順に登録し、両方の handle の root の BuildIdentity をその directory の build の物と照合する（HANDOFF §2 の (j)）。
            // 登録の失敗は観測（呼び出しの失敗）として null を返し、round の load を始めない。
            private Dictionary<string, Dir>? RegisterAll()
            {
                var dirs = new Dictionary<string, Dir>();
                foreach (var name in V.RegisterOrder(_obs.Round))
                {
                    var d = new Dir(name);
                    _dirs.Add(d);
                    _obs.RegisterOrder.Add(name);
                    Phase(_obs.Round + "/register-" + name);
                    var call = new CallObservation { Call = name + ":register" };
                    _obs.Calls.Add(call);
                    string error = "";
                    try { call.Succeeded = d.Backend.Register(OverlayBuilds.Output(_run, name)); }
                    catch (Exception ex) { error = ex.GetType().FullName + ": " + ex.Message; }
                    Write("call", ("call", call.Call), ("succeeded", call.Succeeded), ("error", error));
                    if (call.Succeeded) dirs[name] = d;
                }
                var identities = dirs.Values.Select(d => (d.Name, roots: d.Backend.GetRoots())).ToArray();
                _obs.RootsMatched = identities.All(x => x.roots.Length == 1 && x.roots[0].BuildIdentity == OverlayBuilds.BuildName(x.Name));
                Write("registered", ("registerOrder", _obs.RegisterOrder), ("listed", Listed()), ("rootsMatched", _obs.RootsMatched),
                    ("roots", identities.Select(x => x.Name + "=" + string.Join("|", x.roots.Select(r => r.BuildIdentity)))));
                return dirs.Count == V.RegisterOrder(_obs.Round).Length ? dirs : null;
            }

            // root の entry の引き方は harness の前提なので、見つからなければ round の中止にする。
            private static BuildContentEntry Entry(Dir d, string key)
            {
                var roots = d.Backend.GetRoots();
                if (roots.Length != 1) throw new InvalidOperationException(d.Name + " has " + roots.Length + " roots.");
                return roots[0].Entries.FirstOrDefault(x => x.LogicalKey == key) ?? throw new InvalidOperationException(d.Name + " has no entry " + key);
            }

            // 製品の wrapper が返す load の失敗（null・型の不一致・例外）は観測として null を返し、呼び出しの失敗として残す。
            private async UniTask<(T? asset, IBackendAsset? token, CallObservation call)> LoadObjectCoreAsync<T>(Dir d, string key) where T : Object
            {
                var entry = Entry(d, key);
                var call = new CallObservation { Call = d.Name + ":" + key };
                _obs.Calls.Add(call);
                Phase(_obs.Round + "/load-" + call.Call);
                try
                {
                    var token = await d.Backend.LoadObjectAsync<T>(entry);
                    _assets.Add((d, token));
                    var asset = token.Asset as T;
                    call.Succeeded = asset != null;
                    Write("call", ("call", call.Call), ("succeeded", call.Succeeded), ("entityId", Id(asset)));
                    return (asset, token, call);
                }
                catch (Exception ex) { Write("call", ("call", call.Call), ("succeeded", false), ("error", ex.ToString())); return (null, null, call); }
            }

            private async UniTask<(T? asset, CallObservation call)> LoadObjectAsync<T>(Dir d, string key) where T : Object
            {
                var (asset, _, call) = await LoadObjectCoreAsync<T>(d, key);
                return (asset, call);
            }

            private async UniTask<(T? asset, IBackendAsset? token)> LoadHeldAsync<T>(Dir d, string key) where T : Object
            {
                var (asset, token, _) = await LoadObjectCoreAsync<T>(d, key);
                return (asset, token);
            }

            private async UniTask<(IBackendScene? scene, CallObservation call)> LoadSceneAsync(Dir d, string key)
            {
                var entry = Entry(d, key);
                var call = new CallObservation { Call = d.Name + ":" + key };
                _obs.Calls.Add(call);
                Phase(_obs.Round + "/load-" + call.Call);
                try
                {
                    var scene = await d.Backend.LoadSceneAsync(entry, new SceneLoadOptions(LoadSceneMode.Additive, true));
                    _scenes.Add((d, scene));
                    call.Succeeded = scene.IsLoaded;
                    Write("call", ("call", call.Call), ("succeeded", call.Succeeded), ("roots", scene.GetRootGameObjects().Select(x => x.name)));
                    return (scene, call);
                }
                catch (Exception ex) { Write("call", ("call", call.Call), ("succeeded", false), ("error", ex.ToString())); return (null, call); }
            }

            private async UniTask UnloadSceneAsync(IBackendScene scene)
            {
                var entry = _scenes.FirstOrDefault(x => ReferenceEquals(x.scene, scene));
                if (entry.dir == null) return;
                await entry.dir.Backend.UnloadSceneAsync(scene);
                _scenes.Remove(entry);
            }

            private void Release(IBackendAsset token)
            {
                var entry = _assets.FirstOrDefault(x => ReferenceEquals(x.token, token));
                if (entry.dir == null) return;
                entry.dir.Backend.Release(token);
                _assets.Remove(entry);
            }

            // round の後始末（例外の時も）。wrapper の解除の後に登録が残れば一覧の handle から直接回収する（I-3 (c)）。
            private async UniTask CleanupAsync()
            {
                Phase(_obs.Round + "/cleanup");
                var errors = new List<string>();
                foreach (var (_, token) in _assets.ToArray())
                {
                    try { Release(token); }
                    catch (Exception ex) { errors.Add("release: " + ex.Message); }
                }
                _assets.Clear();
                foreach (var (_, scene) in _scenes.ToArray())
                {
                    try { await UnloadSceneAsync(scene); }
                    catch (Exception ex) { errors.Add("unload: " + ex.Message); }
                }
                _scenes.Clear();
                // 登録の逆順に解除する（後から登録した側を先に）。
                foreach (var d in Enumerable.Reverse(_dirs).ToArray())
                {
                    try { d.Backend.Unregister(); }
                    catch (Exception ex) { errors.Add("unregister " + d.Name + ": " + ex.Message); }
                }
                _dirs.Clear();
                var reclaimed = new List<string>();
                foreach (var h in ContentLoadManager.GetContentDirectories().Where(x => x.IsValid && x.BuildName.StartsWith("ovl-", StringComparison.Ordinal)))
                {
                    try { ContentLoadManager.UnregisterContentDirectory(h); reclaimed.Add(h.BuildName); }
                    catch (Exception ex) { errors.Add("reclaim " + h.BuildName + ": " + ex.Message); }
                }
                await Resources.UnloadUnusedAssets();
                var remaining = Listed();
                var scenesLoaded = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt)
                    .Any(x => x.isLoaded && (x.name == E.SceneKeep || x.name == E.SceneEdit));
                _obs.CleanedUp = remaining.Count == 0 && !scenesLoaded && errors.Count == 0;
                Write("round-cleanup", ("reclaimed", reclaimed), ("remaining", remaining), ("fixtureScenesLoaded", scenesLoaded), ("errors", errors),
                    ("countsAfter", FixtureNames.Select(n => n + "=" + Enumerate<Object>(n).Count)));
                Flush();
            }

            // ───── 観測の部品 ─────

            private static List<string> Listed() => ContentLoadManager.GetContentDirectories().Select(x => x.IsValid ? x.BuildName : "invalid").ToList();

            // Unity の偽 null を見落とさないよう、== null で判定する（AGENTS.md）。
            internal static string Id(Object? o) => o == null ? "null" : EntityId.ToULong(o.GetEntityId()).ToString();

            private static List<string> Enumerate<T>(string name) where T : Object =>
                Resources.FindObjectsOfTypeAll<T>().Where(x => x != null && x.name == name).Select(x => Id(x)).ToList();

            private static GameObject? Root(IBackendScene scene, string name) => scene.GetRootGameObjects().FirstOrDefault(x => x != null && x.name == name);

            // SceneEdit の root の GameObject（v1 は Marker_v1、v2 は Marker_v2）。
            private static GameObject? Marker(IBackendScene scene) =>
                scene.GetRootGameObjects().FirstOrDefault(x => x != null && x.name.StartsWith("Marker_", StringComparison.Ordinal));

            internal static ProbeObservation ObserveProbe(string item, GameObject? go)
            {
                var p = new ProbeObservation { Item = item, Found = go != null };
                if (go == null) return p;
                p.ObjectName = go.name;
                p.Components = Components(go);
                return p;
            }

            // GetComponents<Component>() の並びのまま。Missing（== null）も数える（HANDOFF §2 の (c)）。値は MonoBehaviour だけ JsonUtility で読む。
            internal static List<ComponentObservation> Components(GameObject go) => go.GetComponents<Component>().Select(c => c == null
                ? new ComponentObservation { Missing = true }
                : new ComponentObservation { FullName = c.GetType().FullName ?? "", Json = c is MonoBehaviour ? JsonUtility.ToJson(c) : "" }).ToList();

            private static string Describe(ProbeObservation p) =>
                p.Item + " found=" + p.Found + " name=" + p.ObjectName + " [" + string.Join(", ", p.Components.Select(c => c.Missing ? "MISSING" : c.FullName + (c.Json.Length != 0 ? " " + c.Json : ""))) + "]";

            // Renderer の slot の定義（fixture の順）。material が == null でも、その slot の物として "null" を数える（整合の規則で未判定になる）。
            private static readonly Dictionary<string, string[]> Slots = new Dictionary<string, string[]>
            {
                [E.Keeper] = new[] { E.SharedMat, E.EditedMat, E.KeepMat },
                [E.MarkerV1] = new[] { E.SharedMat, E.EditedMat },
                [E.MarkerV2] = new[] { E.SharedMat, E.EditedMat },
                [E.SharedPrefab] = new[] { E.SharedMat },
            };

            private static readonly Dictionary<string, string> TextureOf = new Dictionary<string, string>
                { [E.SharedMat] = E.SharedTex, [E.EditedMat] = E.EditedTex, [E.KeepMat] = E.KeepTex };

            // AC4 と AC5 の (1) の実体の数（HANDOFF §2 の「実体の数」、AC7）。load した物から辿った id と、列挙の id を物ごとに記録する。
            private void CountAll(GameObject? keeper, GameObject? marker, GameObject? prefab, Texture2D? tex)
            {
                var reached = E.Counted.ToDictionary(x => x, _ => new List<string>());
                void Add(string item, Object? o) { if (reached.TryGetValue(item, out var list)) list.Add(Id(o)); }
                foreach (var go in new[] { keeper, marker, prefab })
                {
                    if (go == null || !Slots.TryGetValue(go.name, out var slots)) continue;
                    var renderer = go.GetComponent<Renderer>();
                    var materials = renderer == null ? Array.Empty<Material>() : renderer.sharedMaterials;
                    for (var i = 0; i < slots.Length; i++)
                    {
                        var m = i < materials.Length ? materials[i] : null;
                        Add(slots[i], m);
                        Add(TextureOf[slots[i]], m == null ? null : m.mainTexture);
                        Add(E.SharedShader, m == null ? null : m.shader);
                    }
                }
                if (prefab != null) Add(E.SharedPrefab, prefab);
                if (tex != null) Add(E.EditedTex, tex);
                _obs.Counts = E.Counted.Select(item => new CountObservation
                {
                    Item = item, LoadIds = reached[item].Distinct().ToList(),
                    EnumeratedIds = item switch
                    {
                        E.SharedShader => Enumerate<Shader>(E.ShaderObjectName),
                        E.SharedPrefab => Enumerate<GameObject>(item),
                        E.SharedMat or E.EditedMat => Enumerate<Material>(item),
                        _ => Enumerate<Texture2D>(item),
                    },
                }).ToList();
            }

            private IEnumerable<string> CountLines() => _obs.Counts.Select(x => x.Item + " load=" + string.Join("|", x.LoadIds) + " enumerated=" + string.Join("|", x.EnumeratedIds));

            private void Check(string step, Dictionary<string, Held> held)
            {
                var broken = held.Values.SelectMany(x => x.Broken()).ToList();
                _obs.Checkpoints.Add(new CheckpointObservation { Step = step, Broken = broken });
                Write("checkpoint", ("step", step), ("broken", broken), ("held", held.Values.Select(x => x.Describe())));
            }

            internal static string Pixels(Texture2D? tex)
            {
                if (tex == null) return "null";
                try
                {
                    var pixels = tex.GetPixels32();
                    var bytes = new byte[pixels.Length * 4];
                    for (var i = 0; i < pixels.Length; i++)
                    {
                        bytes[i * 4] = pixels[i].r; bytes[i * 4 + 1] = pixels[i].g; bytes[i * 4 + 2] = pixels[i].b; bytes[i * 4 + 3] = pixels[i].a;
                    }
                    return E.Sha256(bytes);
                }
                catch (Exception ex) { return "unreadable: " + ex.GetType().Name; }
            }

            private void Write(string type, params (string key, object? value)[] fields) =>
                _run.Write(type, new (string, object?)[] { ("round", _obs.Round), ("playIndex", _obs.PlayIndex) }.Concat(fields).ToArray());

            // ───── log の捕捉。全ての種類を、その時点の phase と一緒に record へ移す（HANDOFF §2.2 の前文） ─────
            private void OnLog(string message, string stack, LogType type)
            {
                lock (_lock) _pending.Add((_phase, type.ToString(), message, Thread.CurrentThread.ManagedThreadId == _mainThread));
            }

            private void Phase(string phase)
            {
                Flush();
                lock (_lock) _phase = phase;
            }

            // AC5 の Q4 に添える log の数は Error と Exception（HANDOFF §2 の Q4）。(6) と後始末の間の物は分けて数える。
            private void Flush()
            {
                (string phase, string type, string message, bool main)[] logs;
                lock (_lock) { logs = _pending.ToArray(); _pending.Clear(); }
                foreach (var l in logs)
                {
                    if (l.type == nameof(LogType.Error) || l.type == nameof(LogType.Exception))
                    {
                        if (l.phase.EndsWith("/" + V.StepRelease, StringComparison.Ordinal) || l.phase.EndsWith("/cleanup", StringComparison.Ordinal)) _obs.ErrorLogsInRelease++;
                        else _obs.ErrorLogs++;
                    }
                    _run.Write("unity-log", ("phase", l.phase), ("logType", l.type), ("message", l.message), ("mainThread", l.main));
                }
            }

            // ───── 終了 ─────
            public void Finish()
            {
                // round の途中で止まった時も、log の購読と LogAssert を元へ戻す（I-3 (d)）。
                Application.logMessageReceivedThreaded -= OnLog;
                if (_capturing) { LogAssert.ignoreFailingMessages = _ignoreBefore; _capturing = false; }
                RestoreGameBootstrap();
                EditorSceneManager.playModeStartScene = _startScene;
                if (EditorApplication.isPlaying) HarnessFailures.Add("finished while still in Play; the scene setup was not restored");
                else if (_setup.All(x => File.Exists(Path.Combine(SpikeRun.RepoRoot, "unity", x.path))) && _setup.Any(x => x.isActive))
                    EditorSceneManager.RestoreSceneManagerSetup(_setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var problems = new List<string>();
                Aftercare.End(_run, _care, problems, false);
                _process.HarnessProblems.AddRange(problems);
                _process.HarnessProblems.AddRange(HarnessFailures);
                _process.Finished = _process.Rounds.Count == V.Plan(_process.Process).Length;
                File.WriteAllText(_run.ObservationPath, JsonUtility.ToJson(_process, true));
                // 補助の判定の行。この run の全ての process の観測を読んで判定する（最後の process で全てが揃う）。判定 C は観測の行から独立に確かめる。
                var all = Directory.GetFiles(_run.Directory, "observations-*.json").OrderBy(x => x, StringComparer.Ordinal)
                    .Select(x => JsonUtility.FromJson<ProcessObservation>(File.ReadAllText(x))).ToList();
                var verdict = V.Judge(_first, all);
                _run.Write("verdict", ("outcome", verdict.Outcome), ("spike", verdict.Spike), ("answers", verdict.Answers), ("reasons", verdict.Reasons),
                    ("processes", all.Select(x => x.Process)),
                    ("scope", SpikeRun.Scope));
                _run.Write("process-end", ("harnessFailures", HarnessFailures), ("finished", _process.Finished));
            }

            // project の Game の bootstrap は Play に入ると必ず起動する。S2-0b と同じく、process の環境変数で addressables mode を与え、
            // Content Directory を登録させない。Game の名前は書かず Framework の型から発見する。
            private void QuietGameBootstrap()
            {
                var initializers = TypeCache.GetTypesDerivedFrom<AbstractApplicationInitializer>()
                    .Where(t => !t.IsAbstract && t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                        .Any(m => m.IsDefined(typeof(RuntimeInitializeOnLoadMethodAttribute), false))).ToArray();
                var method = initializers.Length == 1 ? initializers[0].GetMethod("GetEnvironmentVariablePrefix",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, Type.EmptyTypes, null) : null;
                // constructor の副作用を避けるため、初期化しない instance で override を呼ぶ。
                var prefix = method?.Invoke(FormatterServices.GetUninitializedObject(initializers[0]), null) as string;
                if (prefix != null)
                {
                    _envKey = prefix + "CONTENT__RUNTIMEMODE";
                    _envPrevious = Environment.GetEnvironmentVariable(_envKey, EnvironmentVariableTarget.Process);
                    Environment.SetEnvironmentVariable(_envKey, "addressables", EnvironmentVariableTarget.Process);
                }
                _run.Write("environment", ("gameBootstrap", initializers.Length), ("envKey", _envKey), ("mode", "addressables"));
            }

            private void RestoreGameBootstrap()
            {
                if (_envKey.Length != 0) Environment.SetEnvironmentVariable(_envKey, _envPrevious, EnvironmentVariableTarget.Process);
            }
        }

        /// <summary>
        /// 保持している物の状態（HANDOFF §2 の「壊れた」）。load した時点の material・texture・shader の参照と EntityId、texture の pixel の
        /// sha256 を持ち、確認点ごとに比べる。比べる区間は「その物を最後に load した時点から今の確認点まで」で、load し直した Scene の物は
        /// 新しい Held にする。probe を持つ GameObject は、確認点ごとに位置ごとの class と値も照合する。
        /// </summary>
        private sealed class Held
        {
            private string _label = "";
            private bool _isObject;
            private GameObject? _go;
            private IReadOnlyList<ProbeExpectation>? _probes;
            private readonly List<(Material? mat, string texId, string shaderId)> _materials = new List<(Material?, string, string)>();
            private readonly List<(Texture2D? tex, string pixels)> _textures = new List<(Texture2D?, string)>();

            public static Held Of(string label, GameObject? go, string item)
            {
                var h = new Held { _label = label, _isObject = true, _go = go };
                if (go == null) return h;
                var version = item == E.SceneEdit ? E.VersionOfMarker(go.name) : "";
                h._probes = item == E.SceneEdit && version == E.UnknownVersion ? null : E.Probes(item, version);
                var renderer = go.GetComponent<Renderer>();
                foreach (var m in renderer == null ? Array.Empty<Material>() : renderer.sharedMaterials)
                {
                    h._materials.Add((m, m == null ? "null" : Session.Id(m.mainTexture), m == null ? "null" : Session.Id(m.shader)));
                    var t = m == null ? null : m.mainTexture as Texture2D;
                    h._textures.Add((t, Session.Pixels(t)));
                }
                return h;
            }

            public static Held Of(string label, Texture2D? tex)
            {
                var h = new Held { _label = label };
                h._textures.Add((tex, Session.Pixels(tex)));
                return h;
            }

            public List<string> Broken()
            {
                var b = new List<string>();
                void Add(string what) => b.Add(_label + "." + what);
                if (_isObject)
                {
                    if (_go == null) Add("object.null");
                    else
                    {
                        var renderer = _go.GetComponent<Renderer>();
                        if (renderer == null) Add("renderer.null");
                        else if (renderer.sharedMaterials.Any(x => x == null)) Add("renderer.sharedMaterial.null");
                        if (_probes == null) Add("class.unknownVersion");
                        else if (!OverlayVerdict.ClassesCorrect(Session.Components(_go), _probes, out var why)) Add("class.incorrect(" + why + ")");
                    }
                }
                foreach (var (mat, texId, shaderId) in _materials)
                {
                    if (mat == null) { Add("material.null"); continue; }
                    if (mat.mainTexture == null) Add(mat.name + ".mainTexture.null");
                    else if (Session.Id(mat.mainTexture) != texId) Add(mat.name + ".mainTexture.changed");
                    if (mat.shader == null) Add(mat.name + ".shader.null");
                    else if (Session.Id(mat.shader) != shaderId) Add(mat.name + ".shader.changed");
                }
                foreach (var (tex, pixels) in _textures)
                {
                    if (tex == null) { Add("texture.null"); continue; }
                    if (Session.Pixels(tex) != pixels) Add(tex.name + ".pixels");
                }
                return b;
            }

            public string Describe() => _label + " object=" + (_isObject ? Session.Id(_go) : "-")
                                        + " materials=" + string.Join("|", _materials.Select(x => Session.Id(x.mat) + ":" + x.texId + ":" + x.shaderId))
                                        + " textures=" + string.Join("|", _textures.Select(x => Session.Id(x.tex) + ":" + x.pixels));
        }
    }
}
