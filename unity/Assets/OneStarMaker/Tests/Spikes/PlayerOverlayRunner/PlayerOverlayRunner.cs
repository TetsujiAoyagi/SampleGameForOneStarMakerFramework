#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.BuildContent;
using Unity.Loading;
using UnityEngine;
using UnityEngine.SceneManagement;
using O = OneStarMaker.Tests.Spikes.PlayerOverlayRunner.PlayerOverlayObservations;
using Object = UnityEngine.Object;

namespace OneStarMaker.Tests.Spikes.PlayerOverlayRunner
{
    /// <summary>
    /// Windows の Player での上書きの登録のスパイク（docs/handoff/PLAYER_OVERLAY_REGISTRATION_SPIKE.md §3）の runner。spike の Scene
    /// （PovBoot）に置き、環境変数で渡された 1 つの round だけを行う。環境変数が無ければ何もしない（Phase D まで SampleGame の Player にも
    /// この型が入るが、どの Scene も使わない）。[RuntimeInitializeOnLoadMethod] を持たない。
    /// 登録と load は Unity の公開 API を直接呼ぶ（製品の wrapper は internal で Player の assembly から見えない。HANDOFF §2.5）。
    /// 生の値だけを record に書き、期待値を持たない（判定は Editor 側の写像）。操作ごとに開始と結果の行を書き、操作が失敗したら残りの観測を
    /// 行わずに後始末へ進み、打ち切った段を終了行に書く（HANDOFF §2 の「操作と、その失敗」）。
    /// round は Start 以降（製品の AfterSceneLoad の後）に始め、始める時に開始の印を Debug.Log で出す（P0、HANDOFF §2 の round）。
    /// </summary>
    public sealed class PlayerOverlayRunner : MonoBehaviour
    {
        // 設定の環境変数。製品の設定の読み口が読む `SAMPLEGAME_` で始めない（HANDOFF §2 の round）。
        public const string EnvRound = "OSM_POV_ROUND", EnvLabel = "OSM_POV_LABEL", EnvOrder = "OSM_POV_ORDER", EnvBase = "OSM_POV_DIR_BASE",
            EnvOverlay = "OSM_POV_DIR_OVERLAY", EnvFacts = "OSM_POV_FACTS", EnvRecord = "OSM_POV_RECORD", EnvRun = "OSM_POV_RUN", EnvHead = "OSM_POV_HEAD",
            EnvLimit = "OSM_POV_LIMIT", EnvDomainReload = "OSM_POV_DOMAIN_RELOAD_DISABLED";
        public const string StartMark = "[OSM-POV] round-start";
        private const string Base = "base", Overlay = "overlay";

        // Editor の対照（AC6）で「その Unity の process の最初の Play」かを記録する（Domain Reload が無効なら Play をまたいで残る）。
        private static bool s_played;

        private PlayerOverlayRecord _record = null!;
        private Facts _facts = null!;
        private Timer? _limitTimer;
        private string _round = "";
        private readonly Dictionary<string, string> _paths = new Dictionary<string, string>();
        private readonly List<(string dir, ContentDirectoryHandle handle)> _handles = new List<(string, ContentDirectoryHandle)>();
        private readonly List<(string dir, string key, Loadable<Object> loadable)> _loadables = new List<(string, string, Loadable<Object>)>();
        private readonly List<(string dir, string key, Scene scene)> _scenes = new List<(string, string, Scene)>();
        // 保持している物（寿命の round）。item は logical key、generation は load し直した回数。
        private readonly Dictionary<string, (Object? obj, int generation)> _held = new Dictionary<string, (Object?, int)>();

        private void Start()
        {
            var round = Environment.GetEnvironmentVariable(EnvRound);
            if (string.IsNullOrEmpty(round)) return;
            RunAsync(round!).Forget();
        }

        private async UniTaskVoid RunAsync(string round)
        {
            var firstPlay = !s_played;
            s_played = true;
            _round = round;
            _record = new PlayerOverlayRecord(Env(EnvRecord), round, Env(EnvLabel));
            var order = Env(EnvOrder).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            _paths[Base] = Env(EnvBase);
            _paths[Overlay] = Env(EnvOverlay);
            var limit = int.TryParse(Env(EnvLimit), out var seconds) ? seconds : 180;
            _record.Write("header", ("runId", Env(EnvRun)), ("head", Env(EnvHead)), ("isEditor", Application.isEditor), ("platform", Application.platform.ToString()),
                ("backend", Backend), ("unityVersion", Application.unityVersion), ("buildGuid", Application.buildGUID),
                ("graphics", SystemInfo.graphicsDeviceType.ToString()), ("pid", ProcessId()), ("order", order), ("limitSeconds", limit),
                ("firstPlay", firstPlay), ("domainReloadDisabled", Env(EnvDomainReload) == "true"), ("basePath", _paths[Base]), ("overlayPath", _paths[Overlay]));
            // 上限は main thread に依らない timer で見る（main thread が止まっても process を終える。Player だけ。HANDOFF I-3 (d)）。
            if (!Application.isEditor)
                _limitTimer = new Timer(_ => { _record.Write("limit-exceeded", ("limitSeconds", limit)); Environment.Exit(97); }, null, limit * 1000, Timeout.Infinite);
            Debug.Log(StartMark + " " + round);
            _record.Write("round-start");
            _record.StartLogCapture();
            var aborted = "";
            try
            {
                _facts = Facts.Read(Env(EnvFacts));
                _record.Stage = "p0";
                _record.Write("p0", ("directories", ContentLoadManager.GetContentDirectories().Length), ("scenes", O.LoadedScenes()),
                    ("fixtureObjects", O.CountByName(_facts.fixtureObjectNames).Sum(x => int.Parse(x.Substring(x.LastIndexOf('=') + 1)))),
                    ("fixtureCounts", O.CountByName(_facts.fixtureObjectNames)), ("roots", O.RootObjects()));
                _record.Write("p4", ("probeTypes", _facts.probeTypes.Select(ProbeType).ToArray()));
                _record.Stage = "register";
                foreach (var dir in order) Register(dir);
                switch (round)
                {
                    case "base-alone": await AloneAsync(Base, true); break;
                    case "overlay-alone": await AloneAsync(Overlay, false); break;
                    case "read": await ReadAsync(); break;
                    case "class": await ClassAsync(); break;
                    case "base-lifetime":
                    case "lifetime": await LifetimeAsync(order.Last(), order); break;
                    default: throw new InvalidOperationException("Unknown round " + round);
                }
            }
            catch (OperationFailed failed) { aborted = failed.Stage; }
            catch (Exception ex)
            {
                // 操作の失敗でない例外は harness の不備（判定の写像は未判定にする）。種類と文面を残す。
                aborted = "harness: " + ex.GetType().FullName + ": " + ex.Message;
                _record.Write("harness-exception", ("exceptionType", ex.GetType().FullName), ("message", ex.ToString()));
            }
            finally
            {
                await CleanupAsync();
            }
            _record.Write("end", ("completed", aborted.Length == 0), ("abortedAt", aborted));
            _record.StopLogCapture();
            _limitTimer?.Dispose();
            if (!Application.isEditor) Application.Quit();
        }

        private static string Backend =>
#if ENABLE_IL2CPP
            "IL2CPP";
#else
            "Mono";
#endif

        private static int ProcessId()
        {
            try { return System.Diagnostics.Process.GetCurrentProcess().Id; }
            catch (Exception) { return -1; }
        }

        private static string Env(string name) => Environment.GetEnvironmentVariable(name) ?? "";

        // P4: 型の完全名と assembly 名の文字列から引く（typeof・型引数を使わない。HANDOFF §4 の禁止）。
        private static string ProbeType(string assemblyQualified)
        {
            var fullName = assemblyQualified.Split(',')[0].Trim();
            var type = Type.GetType(assemblyQualified, false);
            return fullName + "|" + (type != null ? "true|" + type.Assembly.GetName().Name : "false|");
        }

        // ───── round ─────

        // AC2 の (1) と (3): base だけ（4 つを load して保持し、class・版・pixel・数）と、上書きだけ（SceneEdit と EditedTex）。
        private async UniTask AloneAsync(string dir, bool full)
        {
            var stage = full ? "base-alone" : "overlay-alone";
            _record.Stage = stage;
            var keep = full ? await LoadSceneAsync(dir, _facts.sceneKeep) : default;
            var edit = await LoadSceneAsync(dir, _facts.sceneEdit);
            var prefab = full ? await LoadObjectAsync<GameObject>(dir, _facts.sharedPrefab) : null;
            var tex = await LoadObjectAsync<Texture2D>(dir, _facts.editedTex);
            var keeper = full ? O.Root(keep, x => x == _facts.keeper) : null;
            var marker = Marker(edit);
            if (full) WriteComponents(_facts.sceneKeep, keeper);
            WriteComponents(_facts.sceneEdit, marker);
            if (full) WriteComponents(_facts.sharedPrefab, prefab);
            WriteScene(dir, edit);
            WriteTexture(dir, tex);
            if (full) WriteCounts(stage, new[] { keeper, marker, prefab }, prefab, new[] { tex });
        }

        // AC3: 読まれる側。両方の root の id の比較、SceneEdit を base・上書きの root の id の順に load・unload、EditedTex を両方から保持。
        private async UniTask ReadAsync()
        {
            _record.Stage = "read";
            var b = Entry(Base, _facts.sceneEdit);
            var o = Entry(Overlay, _facts.sceneEdit);
            var tb = Entry(Base, _facts.editedTex);
            var to = Entry(Overlay, _facts.editedTex);
            _record.Write("ids", ("sceneIdsEqual", b.SceneId.Equals(o.SceneId)), ("loadableIdsEqual", tb.Object != null && to.Object != null && tb.Object.LoadableObjectId.Equals(to.Object.LoadableObjectId)),
                ("sceneIds", new[] { b.SceneId.ToString(), o.SceneId.ToString() }), ("loadableIds", new[] { tb.Object?.LoadableObjectId.ToString() ?? "", to.Object?.LoadableObjectId.ToString() ?? "" }));
            foreach (var dir in new[] { Base, Overlay })
            {
                var scene = await LoadSceneAsync(dir, _facts.sceneEdit);
                WriteScene(dir, scene);
                await UnloadSceneAsync(dir, _facts.sceneEdit);
            }
            await UnloadUnusedAsync();
            var held = new List<Texture2D?>();
            foreach (var dir in new[] { Base, Overlay })
            {
                var tex = await LoadObjectAsync<Texture2D>(dir, _facts.editedTex);
                held.Add(tex);
                WriteTexture(dir, tex);
            }
            _record.Write("count", ("point", "read"), ("item", _facts.editedTex), ("reached", held.Select(O.Id).ToArray()),
                ("enumerated", O.Enumerate("Texture2D", _facts.editedTex)));
        }

        // AC4: class と実体の数。SceneKeep（base）、SceneEdit（上書き）の順に載せたまま、SharedPrefab（base）と EditedTex（上書き）を保持。
        private async UniTask ClassAsync()
        {
            _record.Stage = "class";
            var keep = await LoadSceneAsync(Base, _facts.sceneKeep);
            var edit = await LoadSceneAsync(Overlay, _facts.sceneEdit);
            var prefab = await LoadObjectAsync<GameObject>(Base, _facts.sharedPrefab);
            var tex = await LoadObjectAsync<Texture2D>(Overlay, _facts.editedTex);
            var keeper = O.Root(keep, x => x == _facts.keeper);
            var marker = Marker(edit);
            WriteComponents(_facts.sceneKeep, keeper);
            WriteComponents(_facts.sceneEdit, marker);
            WriteComponents(_facts.sharedPrefab, prefab);
            WriteCounts("class", new[] { keeper, marker, prefab }, prefab, new[] { tex });
            // X2 の観測点: SceneKeep の Renderer が直接参照する EditedMat の mainTexture。
            var renderer = keeper == null ? null : keeper.GetComponent<Renderer>();
            var materials = renderer == null ? Array.Empty<Material>() : renderer.sharedMaterials;
            var mat = _facts.keepEditedSlot < materials.Length ? materials[_facts.keepEditedSlot] : null;
            var keepTex = mat == null ? null : mat.mainTexture as Texture2D;
            _record.Write("keep-edited", ("entityId", O.Id(keepTex)), ("pixels", O.Pixels(keepTex)), ("loadedEditedTexId", O.Id(tex)));
        }

        // AC5（主の順）と AC2 の (2)（base だけの寿命）。editDir は SceneEdit と EditedTex を読む directory（主の順は上書き、base だけでは base）。
        private async UniTask LifetimeAsync(string editDir, string[] order)
        {
            _record.Stage = "(1)";
            var edit = await LoadSceneAsync(editDir, _facts.sceneEdit);
            WriteScene(editDir, edit);
            var keep = await LoadSceneAsync(Base, _facts.sceneKeep);
            var prefab = await LoadObjectAsync<GameObject>(Base, _facts.sharedPrefab);
            var tex = await LoadObjectAsync<Texture2D>(editDir, _facts.editedTex);
            WriteTexture(editDir, tex);
            var marker = Marker(edit);
            var keeper = O.Root(keep, x => x == _facts.keeper);
            Hold(_facts.sceneEdit, marker, 0);
            Hold(_facts.sceneKeep, keeper, 0);
            Hold(_facts.sharedPrefab, prefab, 0);
            Hold(_facts.editedTex, tex, 0);
            WriteCounts("(1)", new[] { keeper, marker, prefab }, prefab, new[] { tex });
            Checkpoint("(1)");
            _record.Stage = "(2)";
            await UnloadSceneAsync(editDir, _facts.sceneEdit);
            _held.Remove(_facts.sceneEdit);
            await UnloadUnusedAsync();
            Checkpoint("(2)");
            // (3) load し直した Scene の物は新しい世代として扱い、前の load と比べない（HANDOFF §2 の「壊れた」）。
            _record.Stage = "(3)";
            edit = await LoadSceneAsync(editDir, _facts.sceneEdit);
            WriteScene(editDir, edit);
            Hold(_facts.sceneEdit, Marker(edit), 1);
            Checkpoint("(3)");
            _record.Stage = "(4)";
            await UnloadSceneAsync(Base, _facts.sceneKeep);
            _held.Remove(_facts.sceneKeep);
            await UnloadUnusedAsync();
            Checkpoint("(4)");
            _record.Stage = "(5)";
            keep = await LoadSceneAsync(Base, _facts.sceneKeep);
            Hold(_facts.sceneKeep, O.Root(keep, x => x == _facts.keeper), 1);
            Checkpoint("(5)");
            // (6) Loadable を Release し、両 Scene を unload し、登録の逆順（上書き → base）に解除する。この段の log は (1)〜(5) と分かれる。
            _record.Stage = "(6)";
            _held.Clear();
            await ReleaseAsync(Base, _facts.sharedPrefab);
            await ReleaseAsync(editDir, _facts.editedTex);
            await UnloadSceneAsync(editDir, _facts.sceneEdit);
            await UnloadSceneAsync(Base, _facts.sceneKeep);
            foreach (var dir in order.Reverse()) Unregister(dir);
            _record.Write("directories", ("count", ContentLoadManager.GetContentDirectories().Length), ("listed", Listed()));
        }

        // ───── 操作（開始と結果の行。失敗は例外、無効な handle、null か型の違う結果、load 済みにならない Scene） ─────

        private void Register(string dir)
        {
            _record.Write("op-start", ("op", "register"), ("dir", dir), ("key", ""));
            var failure = "";
            var exceptionType = "";
            var message = "";
            var handle = default(ContentDirectoryHandle);
            var identity = "";
            try
            {
                handle = ContentLoadManager.RegisterContentDirectory(_paths[dir]);
                if (!handle.IsValid) failure = "invalid handle";
                else
                {
                    _handles.Add((dir, handle));
                    identity = string.Join("|", ContentLoadManager.GetRootAssets<BuildContentRoot>(handle).Select(x => x == null ? "null" : x.BuildIdentity));
                }
            }
            catch (Exception ex) { failure = "exception"; exceptionType = ex.GetType().FullName ?? ""; message = ex.Message; }
            _record.Write("op-result", ("op", "register"), ("dir", dir), ("key", ""), ("ok", failure.Length == 0), ("failure", failure), ("exceptionType", exceptionType),
                ("message", message), ("handleValid", handle.IsValid), ("listed", Listed()), ("rootIdentity", identity));
            if (failure.Length != 0) throw new OperationFailed(_record.Stage);
        }

        private void Unregister(string dir)
        {
            var index = _handles.FindIndex(x => x.dir == dir);
            if (index < 0) return;
            var handle = _handles[index].handle;
            _handles.RemoveAt(index);
            RunOp("unregister", dir, "", () => ContentLoadManager.UnregisterContentDirectory(handle));
        }

        private BuildContentEntry Entry(string dir, string key)
        {
            var handle = _handles.First(x => x.dir == dir).handle;
            var roots = ContentLoadManager.GetRootAssets<BuildContentRoot>(handle);
            if (roots.Length != 1 || roots[0] == null) throw new InvalidOperationException(dir + " has " + roots.Length + " roots");
            return roots[0].Entries.FirstOrDefault(x => x.LogicalKey == key) ?? throw new InvalidOperationException(dir + " has no entry " + key);
        }

        private async UniTask<Scene> LoadSceneAsync(string dir, string key)
        {
            var entry = Entry(dir, key);
            _record.Write("op-start", ("op", "load-scene"), ("dir", dir), ("key", key));
            var failure = "";
            var exceptionType = "";
            var message = "";
            var scene = default(Scene);
            try
            {
                var operation = SceneManager.LoadSceneAsync(entry.SceneId, new LoadSceneParameters(LoadSceneMode.Additive));
                if (operation == null) failure = "did not start";
                else
                {
                    await operation;
                    scene = SceneManager.GetSceneByLoadableSceneId(entry.SceneId);
                    if (!scene.IsValid() || !scene.isLoaded) failure = "not loaded";
                    else _scenes.Add((dir, key, scene));
                }
            }
            catch (Exception ex) { failure = "exception"; exceptionType = ex.GetType().FullName ?? ""; message = ex.Message; }
            _record.Write("op-result", ("op", "load-scene"), ("dir", dir), ("key", key), ("ok", failure.Length == 0), ("failure", failure), ("exceptionType", exceptionType),
                ("message", message), ("roots", O.RootNames(scene)));
            if (failure.Length != 0) throw new OperationFailed(_record.Stage);
            return scene;
        }

        private async UniTask<T?> LoadObjectAsync<T>(string dir, string key) where T : Object
        {
            var entry = Entry(dir, key);
            _record.Write("op-start", ("op", "load-object"), ("dir", dir), ("key", key));
            var failure = "";
            var exceptionType = "";
            var message = "";
            T? result = null;
            try
            {
                var loadable = entry.Object ?? throw new InvalidOperationException(key + " has no loadable");
                var loaded = await loadable.LoadAsync();
                _loadables.Add((dir, key, loadable));
                result = loaded as T;
                if (loaded == null) failure = "null";
                else if (result == null) failure = "type mismatch: " + loaded.GetType().FullName;
            }
            catch (Exception ex) { failure = "exception"; exceptionType = ex.GetType().FullName ?? ""; message = ex.Message; }
            _record.Write("op-result", ("op", "load-object"), ("dir", dir), ("key", key), ("ok", failure.Length == 0), ("failure", failure), ("exceptionType", exceptionType),
                ("message", message), ("entityId", O.Id(result)));
            if (failure.Length != 0) throw new OperationFailed(_record.Stage);
            return result;
        }

        private async UniTask UnloadSceneAsync(string dir, string key)
        {
            var index = _scenes.FindIndex(x => x.dir == dir && x.key == key);
            if (index < 0) return;
            var scene = _scenes[index].scene;
            _scenes.RemoveAt(index);
            await RunOpAsync("unload-scene", dir, key, async () =>
            {
                var operation = SceneManager.UnloadSceneAsync(scene);
                if (operation == null) return "did not start";
                await operation;
                return "";
            });
        }

        private UniTask UnloadUnusedAsync() => RunOpAsync("unload-unused", "", "", async () => { await Resources.UnloadUnusedAssets(); return ""; });

        private UniTask ReleaseAsync(string dir, string key)
        {
            var index = _loadables.FindIndex(x => x.dir == dir && x.key == key);
            if (index < 0) return UniTask.CompletedTask;
            var loadable = _loadables[index].loadable;
            _loadables.RemoveAt(index);
            RunOp("release", dir, key, () => loadable.Release());
            return UniTask.CompletedTask;
        }

        // 同期の操作。開始の行を先に書いてから呼ぶ（呼び出しの中で落ちた時、record の最後の行が開始の行になる）。
        private void RunOp(string op, string dir, string key, Action body)
        {
            _record.Write("op-start", ("op", op), ("dir", dir), ("key", key));
            (string failure, string type, string message) result;
            try { body(); result = ("", "", ""); }
            catch (Exception ex) { result = ("exception", ex.GetType().FullName ?? "", ex.Message); }
            Finish(op, dir, key, result);
        }

        private async UniTask RunOpAsync(string op, string dir, string key, Func<UniTask<string>> body)
        {
            _record.Write("op-start", ("op", op), ("dir", dir), ("key", key));
            (string failure, string type, string message) result;
            try { var failure = await body(); result = (failure, "", ""); }
            catch (Exception ex) { result = ("exception", ex.GetType().FullName ?? "", ex.Message); }
            Finish(op, dir, key, result);
        }

        private void Finish(string op, string dir, string key, (string failure, string type, string message) r)
        {
            _record.Write("op-result", ("op", op), ("dir", dir), ("key", key), ("ok", r.failure.Length == 0), ("failure", r.failure), ("exceptionType", r.type),
                ("message", r.message), ("listed", Listed()));
            if (r.failure.Length != 0 && _record.Stage != "cleanup") throw new OperationFailed(_record.Stage);
        }

        // 後始末（失敗した時も）。残った Loadable・Scene・登録を解放し、自分の build の名前の登録が一覧に残れば handle から解除する。
        // 後始末の操作の失敗も op-result に残すが、投げずに続ける（I-3）。
        private async UniTask CleanupAsync()
        {
            _record.Stage = "cleanup";
            _held.Clear();
            foreach (var (dir, key, _) in _loadables.ToArray()) await ReleaseAsync(dir, key);
            foreach (var (dir, key, _) in _scenes.ToArray()) await UnloadSceneAsync(dir, key);
            foreach (var (dir, _) in Enumerable.Reverse(_handles).ToArray()) Unregister(dir);
            foreach (var h in ContentLoadManager.GetContentDirectories().Where(x => x.IsValid && x.BuildName.StartsWith(_facts?.buildNamePrefix ?? "pov-", StringComparison.Ordinal)))
                RunOp("unregister", "reclaim", h.BuildName, () => ContentLoadManager.UnregisterContentDirectory(h));
        }

        private static string[] Listed() => ContentLoadManager.GetContentDirectories().Select(x => x.IsValid ? x.BuildName : "invalid").ToArray();

        // ───── 観測の行 ─────

        private GameObject? Marker(Scene scene) => O.Root(scene, x => x.StartsWith(_facts.markerPrefix, StringComparison.Ordinal));

        private void WriteComponents(string item, GameObject? go) =>
            _record.Write("components", ("item", item), ("objectName", go == null ? "" : go.name), ("components", O.Components(go)));

        private void WriteScene(string dir, Scene scene)
        {
            var marker = Marker(scene);
            _record.Write("scene", ("dir", dir), ("key", _facts.sceneEdit), ("marker", marker == null ? "" : marker.name), ("roots", O.RootNames(scene)));
        }

        private void WriteTexture(string dir, Texture2D? tex) =>
            _record.Write("texture", ("dir", dir), ("key", _facts.editedTex), ("entityId", O.Id(tex)), ("pixels", O.Pixels(tex)));

        // 物ごとに、参照をたどった EntityId と列挙の EntityId を書く（HANDOFF §2 の「実体の数」、AC7）。
        private void WriteCounts(string point, GameObject?[] objects, GameObject? prefab, Texture2D?[] textures)
        {
            var reached = _facts.counted.ToDictionary(x => x, _ => new List<string>());
            foreach (var go in objects) O.Reach(go, _facts.Slots, _facts.TextureOf, _facts.sharedShader, reached);
            if (prefab != null) reached[_facts.sharedPrefab].Add(O.Id(prefab));
            foreach (var tex in textures) if (tex != null) reached[_facts.editedTex].Add(O.Id(tex));
            for (var i = 0; i < _facts.counted.Length; i++)
                _record.Write("count", ("point", point), ("item", _facts.counted[i]), ("reached", reached[_facts.counted[i]].Distinct().ToArray()),
                    ("enumerated", O.Enumerate(_facts.countKinds[i], _facts.countObjectNames[i])));
        }

        // 保持を始めた直後の状態を stage "loaded" で書き、確認点ごとに同じ物の今の状態を書く（比べるのは判定の写像）。
        private void Hold(string item, Object? obj, int generation)
        {
            // load は成功したが物が == null（Scene に期待の root が無い等）でも保持の対象に入れ、確認点で object.null として書く。
            _held[item] = (obj, generation);
            WriteHeld("loaded", item, obj, generation);
        }

        private void Checkpoint(string step)
        {
            foreach (var kv in _held) WriteHeld(step, kv.Key, kv.Value.obj, kv.Value.generation);
        }

        private void WriteHeld(string stage, string item, Object? obj, int generation)
        {
            if (obj is Texture2D || (obj == null && item == _facts.editedTex))
            {
                var tex = obj as Texture2D;
                _record.Write("held", ("stage", stage), ("item", item), ("generation", generation), ("objectId", O.Id(tex)), ("textures", O.HeldTexture(tex)));
                return;
            }
            var h = O.Held(obj as GameObject);
            _record.Write("held", ("stage", stage), ("item", item), ("generation", generation), ("objectId", h.objectId), ("renderer", h.renderer),
                ("materials", h.materials), ("textures", h.textures), ("components", h.components), ("objectName", h.name));
        }

        /// <summary>操作の失敗。runner は残りの観測を行わずに後始末へ進む（Stage は打ち切った段）。</summary>
        private sealed class OperationFailed : Exception
        {
            public OperationFailed(string stage) : base("operation failed at " + stage) => Stage = stage;
            public string Stage { get; }
        }

        /// <summary>fixture の事実の file（Editor 側の PovFixtureFacts と同じ欄。key と名前だけで、期待値は入れない）。</summary>
        [Serializable]
        private sealed class Facts
        {
            public string sceneKeep = "", sceneEdit = "", sharedPrefab = "", editedTex = "", keeper = "", markerPrefix = "", bootScene = "", buildNamePrefix = "";
            public string[] fixtureObjectNames = Array.Empty<string>();
            public string[] counted = Array.Empty<string>(), countKinds = Array.Empty<string>(), countObjectNames = Array.Empty<string>();
            public string[] slots = Array.Empty<string>(), textureOf = Array.Empty<string>();
            public string sharedShader = "";
            public int keepEditedSlot;
            public string[] probeTypes = Array.Empty<string>();

            public Dictionary<string, string[]> Slots { get; private set; } = new Dictionary<string, string[]>();
            public Dictionary<string, string> TextureOf { get; private set; } = new Dictionary<string, string>();

            public static Facts Read(string path)
            {
                var facts = new Facts();
                JsonUtility.FromJsonOverwrite(File.ReadAllText(path), facts);
                facts.Slots = facts.slots.Select(x => x.Split('=')).ToDictionary(x => x[0], x => x[1].Split(','));
                facts.TextureOf = facts.textureOf.Select(x => x.Split('=')).ToDictionary(x => x[0], x => x[1]);
                return facts;
            }
        }
    }
}
