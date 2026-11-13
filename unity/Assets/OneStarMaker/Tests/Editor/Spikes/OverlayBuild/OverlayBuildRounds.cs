#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
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
using S = OneStarMaker.Tests.Editor.Spikes.OverlayBuild.OverlaySpec;
using Object = UnityEngine.Object;

namespace OneStarMaker.Tests.Editor.Spikes.OverlayBuild
{
    /// <summary>
    /// round の process（HANDOFF CONTENT_OVERLAY_BUILD §5 の判定必須 3、AC6〜AC8）。build の process の出力を読み、fixture の source が無い
    /// Editor の Play で、製品の UnityContentDirectoryBackend を 2 つ持って base → 上書きの順に登録し、6 root を load して観測し、
    /// 上書き → base の順に解除する。2 つの test は root を load する順だけが違い、それぞれ別の Unity process で 1 回ずつ実行する
    /// （Domain Reload 無効の Play で前の Play が読んだ物が残らないよう、観測はその process の最初の Play から採る）。
    /// logical key は登録契約 C5 の規則（上書きの root の entry を先、無ければ base の entry）で引く。test が assert するのは harness の成立だけで、
    /// 答えの向きは assert しない。Unity の log は test の失敗にせず全件を record に書く。
    /// </summary>
    public sealed class OverlayBuildRounds
    {
        [UnityTest, Explicit, Timeout(3600000)] public IEnumerator ScenesFirst() => Run(nameof(ScenesFirst), true);
        [UnityTest, Explicit, Timeout(3600000)] public IEnumerator ObjectFirst() => Run(nameof(ObjectFirst), false);

        private static IEnumerator Run(string process, bool scenesFirst)
        {
            var s = Session.Start(process);
            try
            {
                yield return new EnterPlayMode(false);
                yield return UniTask.ToCoroutine(() => s.PlayAsync(scenesFirst));
                yield return new ExitPlayMode();
            }
            finally { s.Finish(); }
            Assert.That(s.Facts.Problems, Is.Empty, string.Join("\n", s.Facts.Problems));
        }

        private sealed class Session
        {
            private OverlayRun _run = null!;
            private Aftercare _care = null!;
            private OverlayLog _log = null!;
            private BuildFacts _build = new BuildFacts();
            private SceneSetup[] _setup = Array.Empty<SceneSetup>();
            private SceneAsset? _startScene;
            private string _envKey = "";
            private string? _envPrevious;
            private readonly List<(string name, UnityContentDirectoryBackend backend)> _dirs = new List<(string, UnityContentDirectoryBackend)>();
            private readonly List<(UnityContentDirectoryBackend backend, IBackendAsset token)> _assets = new List<(UnityContentDirectoryBackend, IBackendAsset)>();
            private readonly List<(UnityContentDirectoryBackend backend, IBackendScene scene)> _scenes = new List<(UnityContentDirectoryBackend, IBackendScene)>();

            public RoundFacts Facts { get; } = new RoundFacts();

            public static Session Start(string process)
            {
                var s = new Session();
                s._run = OverlayRun.Open(process);
                s._build = JsonUtility.FromJson<BuildFacts>(File.ReadAllText(Path.Combine(s._run.Directory, "build-facts.json")));
                var f = s.Facts;
                f.Process = process; f.RunId = s._run.RunId; f.Head = s._run.Head;
                f.DomainReloadDisabled = EditorSettings.enterPlayModeOptionsEnabled && (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != 0;
                s._run.Write("round-start", ("buildHead", s._build.Head), ("buildFinished", s._build.Finished), ("baseDirectory", s._build.BaseDirectory),
                    ("overlayDirectory", s._build.OverlayDirectory), ("domainReloadDisabled", f.DomainReloadDisabled));
                // Domain Reload が有効だと test の enumerator の位置と局所変数が Play の出入りで失われる。
                if (!f.DomainReloadDisabled) throw new InvalidOperationException("The rounds require Enter Play Mode with Domain Reload disabled.");
                if (!s._build.Finished) throw new InvalidOperationException("The build process did not finish; see its record.");
                s._care = Aftercare.Begin(s._run);
                for (var i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("An open scene has unsaved changes.");
                // Play は空の untitled Scene から始める。Play の初期 Scene の設定を退避して外し、Finish で戻す。
                s._setup = EditorSceneManager.GetSceneManagerSetup();
                s._startScene = EditorSceneManager.playModeStartScene;
                EditorSceneManager.playModeStartScene = null;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                s.QuietGameBootstrap();
                s._log = OverlayLog.Start(s._run);
                s._log.Phase("enter-play", false);
                return s;
            }

            public async UniTask PlayAsync(bool scenesFirst)
            {
                var f = Facts;
                try
                {
                    if (!Application.isPlaying) throw new InvalidOperationException("EnterPlayMode returned without entering Play.");
                    // canary と合わせて、観測が source ではなく directory を読むことの前提（AC7 (2)）。
                    f.SourceInAssetDatabase = AssetDatabase.IsValidFolder(S.Folder)
                        || AssetDatabase.AssetPathToGUID(S.RootPath(S.SceneDirect), AssetPathToGUIDOptions.OnlyExistingAssets).Length != 0;
                    // AC6 の log の窓: 登録の開始から、上書き → base の解除の完了まで。
                    _log.Phase("register", true);
                    var b = Register("base", _build.BaseDirectory, out var baseOk);
                    var o = Register("overlay", _build.OverlayDirectory, out var overlayOk);
                    f.BaseRegistered = baseOk; f.OverlayRegistered = overlayOk;
                    if (!baseOk || !overlayOk) { f.Problems.Add("registration failed"); return; }
                    var baseRoot = SingleRoot(b);
                    var overlayRoot = SingleRoot(o);
                    f.BaseKeys = baseRoot.Entries.Select(x => x.LogicalKey).OrderBy(x => x, StringComparer.Ordinal).ToList();
                    f.OverlayKeys = overlayRoot.Entries.Select(x => x.LogicalKey).OrderBy(x => x, StringComparer.Ordinal).ToList();
                    f.BaseDeclaration = baseRoot.Declaration?.Name ?? "";
                    f.OverlayDeclaration = overlayRoot.Declaration?.Name ?? "";
                    _run.Write("registered", ("baseIdentity", baseRoot.BuildIdentity), ("overlayIdentity", overlayRoot.BuildIdentity), ("baseKeys", f.BaseKeys),
                        ("overlayKeys", f.OverlayKeys), ("baseDeclaration", f.BaseDeclaration), ("overlayDeclaration", f.OverlayDeclaration),
                        ("sourceInAssetDatabase", f.SourceInAssetDatabase));

                    var order = scenesFirst
                        ? S.Roots.Where(x => x != S.PrefabRoot).Concat(new[] { S.PrefabRoot })
                        : new[] { S.PrefabRoot }.Concat(S.Roots.Where(x => x != S.PrefabRoot));
                    var loadedScenes = new Dictionary<string, IBackendScene>();
                    GameObject? prefabRoot = null;
                    foreach (var key in order)
                    {
                        _log.Phase("load-" + key, true);
                        var (backend, entry, from) = Resolve(b, baseRoot, o, overlayRoot, key);
                        f.Resolved.Add(key + "=" + from);
                        if (key == S.PrefabRoot)
                        {
                            var token = await backend.LoadObjectAsync<GameObject>(entry);
                            _assets.Add((backend, token));
                            prefabRoot = token.Asset as GameObject;
                            _run.Write("load", ("key", key), ("from", from), ("loaded", prefabRoot != null));
                        }
                        else
                        {
                            var scene = await backend.LoadSceneAsync(entry, new SceneLoadOptions(LoadSceneMode.Additive, true));
                            _scenes.Add((backend, scene));
                            loadedScenes[key] = scene;
                            _run.Write("load", ("key", key), ("from", from), ("loaded", scene.IsLoaded), ("roots", scene.GetRootGameObjects().Select(x => x.name)));
                        }
                    }

                    _log.Phase("observe", true);
                    GameObject? FindObject(string root, string path) => root == S.PrefabRoot ? prefabRoot
                        : loadedScenes.TryGetValue(root, out var sc) ? OverlayBuildFixture.Find(sc.GetRootGameObjects(), path) : null;
                    foreach (var p in S.Points)
                    {
                        var go = FindObject(p.Root, p.ObjectPath);
                        f.Points.Add(new PointValue { Id = p.Id, Path = go == null ? "missing" : OverlayBuildFixture.HierarchyPath(go), Value = OverlayBuildFixture.Read(p, go) });
                    }
                    f.SceneCleanLoaded = loadedScenes.TryGetValue(S.SceneClean, out var clean) && clean.IsLoaded;
                    // AC8: 実体は、load した物から辿った相異なる EntityId で数える（S2-1 と同じ）。列挙の数は観測として残す。
                    var direct = FindObject(S.SceneDirect, S.DirectObject);
                    var cleanObject = FindObject(S.SceneClean, S.CleanObject);
                    var shared = Ids(OverlayBuildFixture.Texture(direct, Array.IndexOf(S.DirectSlots, S.SharedTex)),
                        OverlayBuildFixture.Texture(cleanObject, Array.IndexOf(S.CleanSlots, S.SharedTex)));
                    var cleanOnly = Ids(OverlayBuildFixture.Texture(cleanObject, Array.IndexOf(S.CleanSlots, S.CleanOnlyTex)));
                    f.SharedTexCount = shared.Count;
                    f.CleanOnlyTexCount = cleanOnly.Count;
                    _run.Write("observe", ("order", order), ("resolved", f.Resolved), ("points", f.Points.Select(x => x.Id + " " + x.Path + " " + x.Value)),
                        ("sceneCleanLoaded", f.SceneCleanLoaded), ("sharedTexIds", shared), ("cleanOnlyTexIds", cleanOnly),
                        ("enumeratedSharedTex", Enumerate(S.SharedTex)), ("enumeratedCleanOnlyTex", Enumerate(S.CleanOnlyTex)));

                    // 登録契約 C7: 載せた物を全て離し、上書き → base の順に解除する。
                    _log.Phase("release", true);
                    foreach (var (backend, token) in _assets.ToArray()) { backend.Release(token); _assets.RemoveAll(x => ReferenceEquals(x.token, token)); }
                    foreach (var (backend, scene) in _scenes.ToArray()) { await backend.UnloadSceneAsync(scene); _scenes.RemoveAll(x => ReferenceEquals(x.scene, scene)); }
                    o.Unregister();
                    _dirs.RemoveAll(x => ReferenceEquals(x.backend, o));
                    b.Unregister();
                    _dirs.RemoveAll(x => ReferenceEquals(x.backend, b));
                    _log.Phase("after-unregister", false);
                    f.ErrorLogs = _log.Errors;
                    f.Completed = true;
                    _run.Write("released", ("errorLogs", f.ErrorLogs), ("listed", Listed()));
                }
                catch (Exception ex)
                {
                    f.Problems.Add("round aborted: " + ex.Message);
                    _run.Write("round-abort", ("error", ex.ToString()));
                }
                finally
                {
                    await CleanupAsync();
                }
            }

            private UnityContentDirectoryBackend Register(string name, string path, out bool ok)
            {
                var backend = new UnityContentDirectoryBackend();
                _dirs.Add((name, backend));
                var error = "";
                ok = false;
                try { ok = backend.Register(path); }
                catch (Exception ex) { error = ex.GetType().FullName + ": " + ex.Message; }
                _run.Write("register", ("directory", name), ("path", path), ("succeeded", ok), ("error", error));
                return backend;
            }

            private static BuildContentRoot SingleRoot(UnityContentDirectoryBackend backend)
            {
                var roots = backend.GetRoots();
                if (roots.Length != 1) throw new InvalidOperationException("A directory has " + roots.Length + " roots.");
                return roots[0];
            }

            // 登録契約 C5: 同じ logical key が両方にあれば上書きの entry、上書きに無ければ base の entry。
            private static (UnityContentDirectoryBackend backend, BuildContentEntry entry, string from) Resolve(
                UnityContentDirectoryBackend b, BuildContentRoot baseRoot, UnityContentDirectoryBackend o, BuildContentRoot overlayRoot, string key)
            {
                var overlay = overlayRoot.Entries.FirstOrDefault(x => x.LogicalKey == key);
                if (overlay != null) return (o, overlay, "overlay");
                var entry = baseRoot.Entries.FirstOrDefault(x => x.LogicalKey == key) ?? throw new InvalidOperationException("No entry " + key);
                return (b, entry, "base");
            }

            // round の後始末（例外の時も）。製品の wrapper の解除の後に登録が残れば、一覧の handle から直接回収する。
            private async UniTask CleanupAsync()
            {
                _log.Phase("cleanup", false);
                var errors = new List<string>();
                foreach (var (backend, token) in _assets.ToArray())
                {
                    try { backend.Release(token); } catch (Exception ex) { errors.Add("release: " + ex.Message); }
                }
                _assets.Clear();
                foreach (var (backend, scene) in _scenes.ToArray())
                {
                    try { await backend.UnloadSceneAsync(scene); } catch (Exception ex) { errors.Add("unload: " + ex.Message); }
                }
                _scenes.Clear();
                foreach (var (name, backend) in Enumerable.Reverse(_dirs).ToArray())
                {
                    try { backend.Unregister(); } catch (Exception ex) { errors.Add("unregister " + name + ": " + ex.Message); }
                }
                _dirs.Clear();
                var reclaimed = new List<string>();
                // この run の 2 つの directory（build の名前は build identity）だけを回収し、他の登録には触れない。
                var owned = new HashSet<string>(new[] { _build.BaseRevision, _build.OverlayIdentity }.Where(x => x.Length != 0), StringComparer.Ordinal);
                foreach (var h in ContentLoadManager.GetContentDirectories().Where(x => x.IsValid && owned.Contains(x.BuildName)))
                {
                    try { ContentLoadManager.UnregisterContentDirectory(h); reclaimed.Add(h.BuildName); }
                    catch (Exception ex) { errors.Add("reclaim " + h.BuildName + ": " + ex.Message); }
                }
                await Resources.UnloadUnusedAssets();
                var remaining = Listed();
                var scenesLoaded = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt)
                    .Any(x => x.isLoaded && S.Roots.Contains(x.name));
                Facts.CleanedUp = remaining.Count == 0 && !scenesLoaded && errors.Count == 0;
                _run.Write("round-cleanup", ("reclaimed", reclaimed), ("remaining", remaining), ("fixtureScenesLoaded", scenesLoaded), ("errors", errors));
                if (!Facts.CleanedUp) Facts.Problems.Add("cleanup did not finish: " + string.Join("; ", errors));
                _log.Flush();
            }

            private static List<string> Listed() => ContentLoadManager.GetContentDirectories().Select(x => x.IsValid ? x.BuildName : "invalid").ToList();

            // Unity の偽 null を見落とさないよう、== null で判定する（AGENTS.md）。
            private static string Id(Object o) => EntityId.ToULong(o.GetEntityId()).ToString();

            private static List<string> Ids(params Texture2D?[] textures) =>
                textures.Where(x => x != null).Select(x => Id(x!)).Distinct().ToList();

            private static List<string> Enumerate(string name) =>
                Resources.FindObjectsOfTypeAll<Texture2D>().Where(x => x != null && x.name == name).Select(Id).ToList();

            public void Finish()
            {
                // 途中で止まった時も、log の購読と LogAssert を元へ戻す。
                _log?.Stop();
                RestoreGameBootstrap();
                EditorSceneManager.playModeStartScene = _startScene;
                if (EditorApplication.isPlaying) Facts.Problems.Add("finished while still in Play; the scene setup was not restored");
                else if (_setup.Length != 0 && _setup.All(x => File.Exists(Path.Combine(OverlayRun.RepoRoot, "unity", x.path))) && _setup.Any(x => x.isActive))
                    EditorSceneManager.RestoreSceneManagerSetup(_setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (_care != null) Aftercare.End(_run, _care, Facts.Problems);
                File.WriteAllText(Path.Combine(_run.Directory, "round-" + Facts.Process + ".json"), JsonUtility.ToJson(Facts, true));
                // 補助の判定の行。この run の round を両方読めた時だけ書く。判定 C は観測の行から独立に確かめる。
                var rounds = Directory.GetFiles(_run.Directory, "round-*.json").OrderBy(x => x, StringComparer.Ordinal)
                    .Select(x => JsonUtility.FromJson<RoundFacts>(File.ReadAllText(x))).ToList();
                if (rounds.Count == 2)
                {
                    var verdict = OverlayBuildVerdict.Judge(_build, rounds);
                    _run.Write("verdict", ("outcome", verdict.Outcome), ("acs", verdict.Acs), ("reasons", verdict.Reasons), ("processes", rounds.Select(x => x.Process)));
                }
                _run.Write("process-end", ("completed", Facts.Completed), ("problems", Facts.Problems), ("errorLogs", Facts.ErrorLogs));
            }

            // project の Game の bootstrap は Play に入ると必ず起動する。S2-1 と同じく、process の環境変数で addressables mode を与え、
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
    }
}
