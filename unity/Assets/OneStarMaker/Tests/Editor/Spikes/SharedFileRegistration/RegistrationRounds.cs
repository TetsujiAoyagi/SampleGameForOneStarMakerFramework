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
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OneStarMaker.Tests.Editor.Spikes.SharedFileRegistration
{
    /// <summary>
    /// 第 2 process（HANDOFF §2.2 AC1〜AC4）。第 1 process の出力を照合し、同時登録の確認・基準・正の対照・AC2〜AC4 の round を
    /// round ごとに新しい Play で行う。登録は製品の UnityContentDirectoryBackend を無改変で 2 つ持って行う。
    /// test が assert するのは harness の成立だけ（全 round の開始行と終了行、後始末、log の購読と LogAssert の復元、record）で、
    /// 答えの向きも未判定かどうかも assert しない（HANDOFF §2 末尾）。Unity の log は test の失敗にせず、全件を round と
    /// 呼び出しの phase に結びつけて record に書く。前提: Enter Play Mode で Domain Reload が無効（本 project の既定）。
    /// 有効だと test の enumerator の位置と局所変数が Play の出入りで失われるので、開始時に確かめて止める。
    /// </summary>
    public sealed class RegistrationRounds
    {
        [UnityTest, Explicit, Timeout(7200000)]
        public IEnumerator SecondProcess()
        {
            var s = Session.Start();
            try
            {
                foreach (var spec in s.Plan())
                {
                    s.BeforePlay(spec);
                    yield return new EnterPlayMode(false);
                    yield return UniTask.ToCoroutine(() => s.PlayAsync(spec));
                    yield return new ExitPlayMode();
                    s.AfterPlay(spec);
                }
            }
            finally { s.Finish(); }
            Assert.That(s.HarnessFailures, Is.Empty, string.Join("\n", s.HarnessFailures));
        }

        private sealed class Spec
        {
            public string Ac = "", Key = "", Order = "";
            public int Rep;
            public string First => Order.Split('>')[0];
            public string Second => Order.Split('>')[1];
        }

        private sealed class Dir
        {
            public Dir(string name) => Name = name;
            public string Name { get; }
            public UnityContentDirectoryBackend Backend { get; } = new UnityContentDirectoryBackend();
        }

        private sealed class Session
        {
            // shader の Object.name は shader の source の名前（asset の file 名ではない）。
            private const string ShaderObjectName = "OsmSpikeSfr/SharedShader";
            private static readonly string[] FixtureNames = { RegistrationFixture.SharedTexName, RegistrationFixture.SharedMatName,
                RegistrationFixture.SharedPrefabName, ShaderObjectName, RegistrationFixture.TwinName };
            private SpikeRun _run = null!;
            private Aftercare _care = null!;
            private SceneSetup[] _setup = Array.Empty<SceneSetup>();
            private SceneAsset? _startScene;
            private string _envKey = "";
            private string? _envPrevious;
            private bool _ignoreBefore, _capturing;
            private readonly object _lock = new object();
            private string _phase = "";
            private readonly List<(string phase, string type, string message, bool main)> _pending = new List<(string, string, string, bool)>();
            private readonly List<(string phase, string type)> _roundLogs = new List<(string, string)>();
            private int _mainThread;
            // round ごとの状態。Play の中でだけ使い、cleanup で空にする。
            private readonly List<Dir> _dirs = new List<Dir>();
            private readonly Dictionary<string, IBackendAsset> _assets = new Dictionary<string, IBackendAsset>();
            private readonly Dictionary<string, IBackendScene> _scenes = new Dictionary<string, IBackendScene>();
            private RoundObservation _obs = new RoundObservation();

            public VerdictInput Input { get; } = new VerdictInput();
            public List<string> HarnessFailures { get; } = new List<string>();
            private readonly List<string> _problems = new List<string>();

            public static Session Start()
            {
                if (!File.Exists(SpikeRun.FirstPointer)) throw new InvalidOperationException("FirstProcess has not written its record.");
                var s = new Session { _mainThread = Thread.CurrentThread.ManagedThreadId };
                s._run = SpikeRun.Open(File.ReadAllText(SpikeRun.FirstPointer).Trim(), "second");
                var first = JsonUtility.FromJson<RegistrationBuilds.FirstState>(File.ReadAllText(Path.Combine(s._run.Directory, "first-state.json")));
                var p = s.Input.Preconditions;
                p.FixtureAbsentAtSecondStart = !RegistrationFixture.Exists;
                s._care = Aftercare.Begin(s._run);
                // 第 1 process の同じ実行の出力か（head と、棚卸しを取り直した digest。AC1 の G-09）。
                var digests = RegistrationBuilds.Directories.Select(d => d + "=" + RegistrationBuilds.Digest(RegistrationBuilds.Inventory(Output(s._run, d)))).ToArray();
                p.DigestMatched = first.RunId == s._run.RunId && first.Head == s._run.Head && digests.SequenceEqual(first.Digests);
                p.BuildsSucceeded = first.BuildsSucceeded;
                p.NotSameFile.AddRange(first.NotSameFile);
                p.TwinFilesDiffer = first.TwinFilesDiffer;
                p.HarnessProblems.AddRange(first.Problems.Select(x => "first process: " + x));
                s._run.Write("second-start", ("firstHead", first.Head), ("digests", digests), ("firstDigests", first.Digests),
                    ("digestMatched", p.DigestMatched), ("fixtureAbsent", p.FixtureAbsentAtSecondStart));
                if (!EditorSettings.enterPlayModeOptionsEnabled || (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) == 0)
                    throw new InvalidOperationException("The second process requires Enter Play Mode with Domain Reload disabled.");
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

            private static string Output(SpikeRun run, string d) => RegistrationBuilds.Output(run, d);

            /// <summary>Play の round の列。同時登録の確認の結果で、拒否された順の round を適用除外にする（HANDOFF §2 の (r1)・手順 1）。</summary>
            public IEnumerable<Spec> Plan()
            {
                yield return new Spec { Ac = "check", Key = "check" };
                foreach (var d in RegistrationBuilds.Directories) yield return new Spec { Ac = "base", Key = "base " + d, Order = d + ">" + d };
                var refused = new HashSet<string>(Input.Orders.Where(x => x.Refused).Select(x => x.Order));
                if (refused.Count == RegistrationVerdict.OrderNames.Length)
                {
                    _run.Write("excluded", ("what", "positive control and AC2-AC4"), ("reason", "simultaneous registration refused in both orders"));
                    yield break;
                }
                yield return new Spec { Ac = "control", Key = "control", Order = RegistrationVerdict.OrderNames.First(x => !refused.Contains(x)) };
                foreach (var rep in new[] { 1, 2 })
                foreach (var (ac, key, order) in RegistrationVerdict.ExpectedRounds())
                {
                    if (!refused.Contains(order)) { yield return new Spec { Ac = ac, Key = key, Order = order, Rep = rep }; continue; }
                    _run.Write("round-start", ("round", key), ("rep", rep), ("exclusion", "r1"));
                    _run.Write("round-end", ("round", key), ("rep", rep), ("answer", RegistrationVerdict.ExcludedAnswer));
                    Input.Rounds.Add(new RoundObservation { Ac = ac, Key = key, Order = order, Repetition = rep, Exclusion = "r1", Ended = true });
                }
            }

            public void BeforePlay(Spec spec)
            {
                _ignoreBefore = LogAssert.ignoreFailingMessages;
                LogAssert.ignoreFailingMessages = true;
                _capturing = true;
                lock (_lock) _phase = spec.Key + "/enter-play";
                Application.logMessageReceivedThreaded += OnLog;
                _run.Write("log-capture", ("round", spec.Key), ("rep", spec.Rep), ("subscribed", true), ("ignoreFailingMessagesBefore", _ignoreBefore));
            }

            public void AfterPlay(Spec spec)
            {
                Phase(spec, "exit-play");
                Flush();
                Application.logMessageReceivedThreaded -= OnLog;
                LogAssert.ignoreFailingMessages = _ignoreBefore;
                _capturing = false;
                _run.Write("log-capture", ("round", spec.Key), ("rep", spec.Rep), ("subscribed", false), ("ignoreFailingMessagesRestored", LogAssert.ignoreFailingMessages));
            }

            public async UniTask PlayAsync(Spec spec)
            {
                _roundLogs.Clear();
                _obs = new RoundObservation { Ac = spec.Ac, Key = spec.Key, Order = spec.Order, Repetition = spec.Rep };
                var completed = false;
                try
                {
                    if (!Application.isPlaying) throw new InvalidOperationException("EnterPlayMode returned without entering Play.");
                    _obs.DirectoriesAtStart = ContentLoadManager.GetContentDirectories().Length;
                    var leftovers = FixtureNames.Select(n => n + "=" + Enumerate<Object>(n).Count).ToArray();
                    _obs.LeftoverAtStart = FixtureNames.Sum(n => Enumerate<Object>(n).Count);
                    _obs.SourceInAssetDatabase = AssetDatabase.IsValidFolder(RegistrationFixture.Folder)
                        || AssetDatabase.AssetPathToGUID(RegistrationFixture.SharedTex, AssetPathToGUIDOptions.OnlyExistingAssets).Length != 0;
                    _run.Write("round-start", ("round", spec.Key), ("rep", spec.Rep), ("order", spec.Order), ("directories", _obs.DirectoriesAtStart),
                        ("leftovers", leftovers), ("sourceInAssetDatabase", _obs.SourceInAssetDatabase));
                    switch (spec.Ac)
                    {
                        case "check": await CheckAsync(spec); break;
                        case "base": await BaseAsync(spec); break;
                        case "control": await ControlAsync(spec); break;
                        case "AC2": await LoadableAsync(spec); break;
                        case "AC3": await DirectAsync(spec); break;
                        default: await UnregisterAsync(spec); break;
                    }
                    completed = true;
                }
                catch (Exception ex)
                {
                    HarnessFailures.Add(spec.Key + " #" + spec.Rep + ": " + ex.Message);
                    _run.Write("round-abort", ("round", spec.Key), ("rep", spec.Rep), ("error", ex.ToString()));
                }
                finally
                {
                    await CleanupAsync(spec);
                }
                if (completed)
                {
                    _obs.Ended = true;
                    _run.Write("round-end", ("round", spec.Key), ("rep", spec.Rep), ("cleanedUp", _obs.CleanedUp));
                }
                if (!_obs.CleanedUp) HarnessFailures.Add(spec.Key + " #" + spec.Rep + ": cleanup did not finish");
                if (spec.Ac.StartsWith("AC", StringComparison.Ordinal)) Input.Rounds.Add(_obs);
            }

            // ───── AC1: 同時登録の確認 ─────
            private async UniTask CheckAsync(Spec spec)
            {
                foreach (var order in RegistrationVerdict.OrderNames)
                {
                    var names = order.Split('>');
                    var check = new OrderCheck { Order = order };
                    var path = Output(_run, names[1]);
                    var pathOk = File.Exists(Path.Combine(path, "BuildManifestHash.txt"));
                    var first = new Dir(names[0]);
                    var second = new Dir(names[1]);
                    _dirs.Add(first); _dirs.Add(second);
                    Phase(spec, order + "/register-first");
                    check.FirstRegistered = first.Backend.Register(Output(_run, names[0]));
                    Phase(spec, order + "/register-second");
                    string error = "";
                    try { check.SecondValid = second.Backend.Register(path); }
                    catch (Exception ex) { check.SecondThrew = true; error = ex.GetType().FullName + ": " + ex.Message; }
                    Phase(spec, order + "/after-register");
                    var listed = Listed();
                    check.SecondListed = listed.Contains(RegistrationBuilds.BuildName(names[1]));
                    check.FailureRecorded = check.SecondThrew || ErrorIn(spec, order + "/register-second");
                    check.ExcludedCause = pathOk ? "" : "path";
                    _run.Write("order-check", ("order", order), ("firstRegistered", check.FirstRegistered), ("secondValid", check.SecondValid),
                        ("secondThrew", check.SecondThrew), ("error", error), ("listed", listed), ("failureRecorded", check.FailureRecorded),
                        ("excludedCause", check.ExcludedCause));
                    await CleanupAsync(spec);
                    check.CleanedUp = _obs.CleanedUp;
                    Input.Orders.Add(check);
                }
            }

            // ───── AC1: 単独登録の基準 ─────
            private async UniTask BaseAsync(Spec spec)
            {
                var d = Register(spec, spec.First);
                var b = new Baseline { Directory = d.Name };
                var prefab = await LoadAsync<GameObject>(spec, d, RegistrationFixture.SharedPrefabName);
                b.PrefabLoaded = prefab != null;
                if (prefab != null)
                {
                    var (mat, tex) = Chain(prefab.GetComponent<Renderer>());
                    var counts = new[] { (Enumerate<GameObject>(RegistrationFixture.SharedPrefabName), Id(prefab)),
                        (Enumerate<Material>(RegistrationFixture.SharedMatName), Id(mat)), (Enumerate<Texture2D>(RegistrationFixture.SharedTexName), Id(tex)) };
                    b.CoreCountsOne = counts.All(x => x.Item1.Count == 1 && x.Item1[0] == x.Item2);
                    _run.Write("base-counts", ("directory", d.Name), ("counts", counts.Select(x => string.Join("|", x.Item1) + " load=" + x.Item2)));
                }
                b.SceneLoaded = await LoadSceneAsync(spec, d) != null;
                _run.Write("base", ("directory", d.Name), ("prefabLoaded", b.PrefabLoaded), ("coreCountsOne", b.CoreCountsOne), ("sceneLoaded", b.SceneLoaded));
                Input.Baselines.Add(b);
            }

            // ───── AC1: 正の対照（Object.name だけが同じ別の texture） ─────
            private async UniTask ControlAsync(Spec spec)
            {
                var (a, b) = RegisterBoth(spec);
                var ta = await LoadAsync<Texture2D>(spec, a, RegistrationFixture.TwinName);
                var tb = await LoadAsync<Texture2D>(spec, b, RegistrationFixture.TwinName);
                Input.Control = Count(Kind.Texture, new[] { ta, tb }, Enumerate<Texture2D>(RegistrationFixture.TwinName));
                _run.Write("control", ("order", spec.Order), ("loadIds", Input.Control.LoadIds), ("enumerated", Input.Control.EnumeratedIds));
            }

            // ───── AC2: Loadable からの load ─────
            private async UniTask LoadableAsync(Spec spec)
            {
                var (a, b) = RegisterBoth(spec);
                var before = Profiler.GetTotalAllocatedMemoryLong();
                var pa = await LoadAsync<GameObject>(spec, a, RegistrationFixture.SharedPrefabName);
                var pb = await LoadAsync<GameObject>(spec, b, RegistrationFixture.SharedPrefabName);
                if (pa == null) _obs.LoadFailures.Add("A:prefab");
                if (pb == null) _obs.LoadFailures.Add("B:prefab");
                if (pa == null || pb == null) { _run.Write("load-failures", ("round", spec.Key), ("failures", _obs.LoadFailures)); return; }
                var after = Profiler.GetTotalAllocatedMemoryLong();
                var (ma, ta) = Chain(pa.GetComponent<Renderer>());
                var (mb, tb) = Chain(pb.GetComponent<Renderer>());
                _obs.Counts.Add(Count(Kind.Prefab, new Object?[] { pa, pb }, Enumerate<GameObject>(RegistrationFixture.SharedPrefabName)));
                _obs.Counts.Add(Count(Kind.Material, new Object?[] { ma, mb }, Enumerate<Material>(RegistrationFixture.SharedMatName)));
                _obs.Counts.Add(Count(Kind.Texture, new Object?[] { ta, tb }, Enumerate<Texture2D>(RegistrationFixture.SharedTexName)));
                var ida = Entry(a, RegistrationFixture.SharedPrefabName).Object!.LoadableObjectId;
                var idb = Entry(b, RegistrationFixture.SharedPrefabName).Object!.LoadableObjectId;
                WriteCounts(spec, ("loadableObjectIdEqual", ida.Equals(idb)), ("allocatedBefore", before), ("allocatedAfter", after),
                    ("sharedTexRuntimeSize", ta != null ? Profiler.GetRuntimeMemorySizeLong(ta) : -1));
            }

            // ───── AC3: 直接参照からの load（Scene の Renderer） ─────
            private async UniTask DirectAsync(Spec spec)
            {
                var (a, b) = RegisterBoth(spec);
                var ra = await LoadSceneAsync(spec, a);
                var rb = await LoadSceneAsync(spec, b);
                if (ra == null) _obs.LoadFailures.Add("A:scene");
                if (rb == null) _obs.LoadFailures.Add("B:scene");
                if (ra == null || rb == null) { _run.Write("load-failures", ("round", spec.Key), ("failures", _obs.LoadFailures)); return; }
                var (ma, ta) = Chain(ra);
                var (mb, tb) = Chain(rb);
                _obs.Counts.Add(Count(Kind.Material, new Object?[] { ma, mb }, Enumerate<Material>(RegistrationFixture.SharedMatName)));
                _obs.Counts.Add(Count(Kind.Texture, new Object?[] { ta, tb }, Enumerate<Texture2D>(RegistrationFixture.SharedTexName)));
                // 観測: Loadable と直接参照の混在（A の root の prefab の material が Scene の material と同じ実体か）。
                var pa = await LoadAsync<GameObject>(spec, a, RegistrationFixture.SharedPrefabName);
                var (mp, _) = pa != null ? Chain(pa.GetComponent<Renderer>()) : (null, null);
                WriteCounts(spec, ("prefabMaterialEqualsSceneA", pa != null && Id(mp) == Id(ma)), ("prefabLoaded", pa != null));
            }

            // ───── AC4: 登録解除（case の形は HANDOFF §2.2 の表） ─────
            private static readonly Dictionary<string, (string[] loads, string? pre, string target, string survivor)> CaseTable =
                new Dictionary<string, (string[], string?, string, string)>
                {
                    ["U1"] = (new[] { "A:prefab", "B:prefab" }, "A:prefab", "A", "B:prefab"),
                    ["U2"] = (new[] { "B:prefab" }, null, "A", "B:prefab"),
                    ["U3"] = (new[] { "A:prefab" }, null, "B", "A:prefab"),
                    ["U4"] = (new[] { "B:prefab" }, null, "A", "B:prefab"),
                    ["U5"] = (new[] { "A:scene" }, null, "B", "A:scene"),
                    ["U6"] = (new[] { "A:prefab", "B:prefab" }, "B:prefab", "B", "A:prefab"),
                    ["U7"] = (new[] { "A:scene", "B:scene" }, "B:scene", "B", "A:scene"),
                };

            private async UniTask UnregisterAsync(Spec spec)
            {
                var c = CaseTable[spec.Key.Substring(4)];
                var (a, b) = RegisterBoth(spec);
                Dir D(string name) => name == "A" ? a : b;
                var loaded = new Dictionary<string, Object?>();
                foreach (var load in c.loads)
                {
                    var parts = load.Split(':');
                    Object? o = parts[1] == "scene" ? (Object?)await LoadSceneAsync(spec, D(parts[0])) : await LoadAsync<GameObject>(spec, D(parts[0]), RegistrationFixture.SharedPrefabName);
                    if (o == null) _obs.LoadFailures.Add(load);
                    loaded[load] = o;
                }
                if (_obs.LoadFailures.Count != 0) { _run.Write("load-failures", ("round", spec.Key), ("failures", _obs.LoadFailures)); return; }
                // 生き残る側の参照は T3 の UnloadUnusedAssets の完了まで probe が保持する。
                var probe = new Probe(loaded[c.survivor]!);
                var u = new UnregisterObservation { HasPreRelease = c.pre != null };
                if (c.pre != null)
                {
                    u.R0Broken = probe.Broken();
                    Phase(spec, "pre-release");
                    await ReleaseAsync(c.pre);
                    Phase(spec, "after-pre-release");
                    u.R1Broken = probe.Broken();
                    _run.Write("pre-release", ("round", spec.Key), ("rep", spec.Rep), ("released", c.pre), ("r0", u.R0Broken), ("r1", u.R1Broken), ("survivor", probe.Describe()));
                }
                var target = D(c.target);
                var targetName = RegistrationBuilds.BuildName(c.target);
                _run.Write("t0", ("round", spec.Key), ("rep", spec.Rep), ("broken", probe.Broken()), ("listed", Listed()), ("survivor", probe.Describe()));
                Phase(spec, "unregister-call");
                var threw = "";
                try { target.Backend.Unregister(); }
                catch (Exception ex) { threw = ex.GetType().FullName + ": " + ex.Message; }
                Phase(spec, "t1-t3");
                u.CallLoggedError = threw.Length != 0 || ErrorIn(spec, "unregister-call");
                u.T1Broken = probe.Broken();
                _run.Write("t1", ("round", spec.Key), ("rep", spec.Rep), ("broken", u.T1Broken), ("listed", Listed()), ("threw", threw));
                await UniTask.NextFrame();
                u.T2Broken = probe.Broken();
                _run.Write("t2", ("round", spec.Key), ("rep", spec.Rep), ("broken", u.T2Broken), ("listed", Listed()));
                await Resources.UnloadUnusedAssets();
                u.T3Broken = probe.Broken();
                var listed = Listed();
                u.ListedAtT3 = listed.Contains(targetName);
                _run.Write("t3", ("round", spec.Key), ("rep", spec.Rep), ("broken", u.T3Broken), ("listed", listed), ("survivor", probe.Describe()));
                Phase(spec, "reload-after-t3");
                // T3 の後に 1 回だけ、生き残る側の物を Release（Scene は unload）して同じ handle から再 load する（外部レビュー E-11）。
                var survivor = c.survivor.Split(':');
                await ReleaseAsync(c.survivor);
                Object? again = survivor[1] == "scene" ? (Object?)await LoadSceneAsync(spec, D(survivor[0])) : await LoadAsync<GameObject>(spec, D(survivor[0]), RegistrationFixture.SharedPrefabName);
                var reloadBroken = again == null ? new List<string> { "reload.null" } : new Probe(again).Broken();
                u.ReloadAfterT3Failed = reloadBroken.Count != 0;
                _run.Write("reload-after-t3", ("round", spec.Key), ("rep", spec.Rep), ("broken", reloadBroken));
                _obs.Unregister = u;
                _run.Write("case", ("round", spec.Key), ("rep", spec.Rep), ("callLoggedError", u.CallLoggedError), ("listedAtT3", u.ListedAtT3),
                    ("class", RegistrationVerdict.Classify(u, out var why)), ("unclassified", why));
            }

            // ───── 登録・load・解放の部品 ─────
            private Dir Register(Spec spec, string name)
            {
                var d = new Dir(name);
                _dirs.Add(d);
                Phase(spec, "register-" + name);
                if (!d.Backend.Register(Output(_run, name))) throw new InvalidOperationException("Registration of " + name + " returned an invalid handle.");
                return d;
            }

            private (Dir a, Dir b) RegisterBoth(Spec spec)
            {
                var first = Register(spec, spec.First);
                var second = Register(spec, spec.Second);
                _run.Write("registered", ("round", spec.Key), ("rep", spec.Rep), ("order", spec.Order), ("listed", Listed()));
                return first.Name == "A" ? (first, second) : (second, first);
            }

            // root の entry の引き方は harness の前提なので、失敗は load の失敗ではなく round の中止にする。
            private static BuildContentEntry Entry(Dir d, string key)
            {
                var roots = d.Backend.GetRoots();
                if (roots.Length != 1) throw new InvalidOperationException(d.Name + " has " + roots.Length + " roots.");
                return roots[0].Entries.FirstOrDefault(x => x.LogicalKey == key) ?? throw new InvalidOperationException(d.Name + " has no entry " + key);
            }

            // 製品の wrapper が返す load の失敗（null・型の不一致・例外）は観測として null を返す。
            private async UniTask<T?> LoadAsync<T>(Spec spec, Dir d, string key) where T : Object
            {
                var entry = Entry(d, key);
                Phase(spec, "load-" + d.Name + "-" + key);
                try
                {
                    var asset = await d.Backend.LoadObjectAsync<T>(entry);
                    _assets[d.Name + ":" + (key == RegistrationFixture.SharedPrefabName ? "prefab" : key)] = asset;
                    return asset.Asset as T;
                }
                catch (Exception ex) { _run.Write("load-failed", ("round", spec.Key), ("directory", d.Name), ("key", key), ("error", ex.ToString())); return null; }
            }

            private async UniTask<Renderer?> LoadSceneAsync(Spec spec, Dir d)
            {
                var entry = Entry(d, "Scene");
                Phase(spec, "load-" + d.Name + "-scene");
                try
                {
                    var scene = await d.Backend.LoadSceneAsync(entry, new SceneLoadOptions(LoadSceneMode.Additive, true));
                    _scenes[d.Name + ":scene"] = scene;
                    return scene.GetRootGameObjects().Select(x => x.GetComponent<Renderer>()).FirstOrDefault(x => x != null);
                }
                catch (Exception ex) { _run.Write("load-failed", ("round", spec.Key), ("directory", d.Name), ("key", "scene"), ("error", ex.ToString())); return null; }
            }

            private async UniTask ReleaseAsync(string key)
            {
                var dir = _dirs.Last(x => x.Name == key.Split(':')[0]);
                if (_assets.TryGetValue(key, out var asset)) { dir.Backend.Release(asset); _assets.Remove(key); }
                if (_scenes.TryGetValue(key, out var scene)) { await dir.Backend.UnloadSceneAsync(scene); _scenes.Remove(key); }
            }

            // round の後始末（例外の時も）。wrapper の解除の後に登録が残れば一覧の handle から直接回収する（A2 G-08、I-3 (c)）。
            private async UniTask CleanupAsync(Spec spec)
            {
                Phase(spec, "cleanup");
                var errors = new List<string>();
                foreach (var key in _assets.Keys.Concat(_scenes.Keys).ToArray())
                {
                    try { await ReleaseAsync(key); }
                    catch (Exception ex) { errors.Add(key + ": " + ex.Message); }
                }
                foreach (var d in _dirs)
                {
                    try { d.Backend.Unregister(); }
                    catch (Exception ex) { errors.Add("unregister " + d.Name + ": " + ex.Message); }
                }
                _dirs.Clear();
                var reclaimed = new List<string>();
                foreach (var h in ContentLoadManager.GetContentDirectories().Where(x => x.IsValid && x.BuildName.StartsWith("sfr-", StringComparison.Ordinal)))
                {
                    try { ContentLoadManager.UnregisterContentDirectory(h); reclaimed.Add(h.BuildName); }
                    catch (Exception ex) { errors.Add("reclaim " + h.BuildName + ": " + ex.Message); }
                }
                await Resources.UnloadUnusedAssets();
                var remaining = Listed();
                var scenesLoaded = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt)
                    .Any(x => x.isLoaded && (x.name == RegistrationFixture.SceneAName || x.name == RegistrationFixture.SceneBName));
                _obs.CleanedUp = remaining.Count == 0 && !scenesLoaded;
                _run.Write("round-cleanup", ("round", spec.Key), ("rep", spec.Rep), ("reclaimed", reclaimed), ("remaining", remaining),
                    ("fixtureScenesLoaded", scenesLoaded), ("errors", errors), ("countsAfter", FixtureNames.Select(n => n + "=" + Enumerate<Object>(n).Count)));
                Flush();
            }

            // ───── 観測の部品 ─────
            private static List<string> Listed() => ContentLoadManager.GetContentDirectories().Select(x => x.IsValid ? x.BuildName : "invalid").ToList();

            // Unity の偽 null を見落とさないよう、== null で判定する（AGENTS.md）。
            internal static string Id(Object? o) => o == null ? Kind.NullId : EntityId.ToULong(o.GetEntityId()).ToString();

            private static List<string> Enumerate<T>(string name) where T : Object =>
                Resources.FindObjectsOfTypeAll<T>().Where(x => x != null && x.name == name).Select(x => Id(x)).ToList();

            private static CountObservation Count(string kind, Object?[] loaded, List<string> enumerated) =>
                new CountObservation { Kind = kind, LoadIds = loaded.Select(Id).ToList(), EnumeratedIds = enumerated };

            internal static (Material? mat, Texture2D? tex) Chain(Renderer? r)
            {
                if (r == null) return (null, null);
                var mat = r.sharedMaterial;
                return mat == null ? (null, null) : (mat, mat.mainTexture as Texture2D);
            }

            private void WriteCounts(Spec spec, params (string key, object? value)[] extra) =>
                _run.Write("counts", new (string, object?)[] { ("round", spec.Key), ("rep", spec.Rep), ("order", spec.Order),
                    ("counts", _obs.Counts.Select(x => x.Kind + " load=" + string.Join("|", x.LoadIds) + " enumerated=" + string.Join("|", x.EnumeratedIds))) }
                    .Concat(extra).ToArray());

            // ───── log の捕捉。Unity の Error・Exception・Assert・Warning を、その時点の phase と一緒に record へ移す ─────
            private void OnLog(string message, string stack, LogType type)
            {
                if (type == LogType.Log) return;
                lock (_lock) _pending.Add((_phase, type.ToString(), message, Thread.CurrentThread.ManagedThreadId == _mainThread));
            }

            private void Phase(Spec spec, string phase)
            {
                Flush();
                lock (_lock) _phase = spec.Key + "/" + phase;
            }

            private void Flush()
            {
                (string phase, string type, string message, bool main)[] logs;
                lock (_lock) { logs = _pending.ToArray(); _pending.Clear(); }
                foreach (var l in logs)
                {
                    _roundLogs.Add((l.phase, l.type));
                    _run.Write("unity-log", ("phase", l.phase), ("logType", l.type), ("message", l.message), ("mainThread", l.main));
                }
            }

            private bool ErrorIn(Spec spec, string phase)
            {
                Flush();
                return _roundLogs.Any(x => x.phase == spec.Key + "/" + phase && (x.type == "Error" || x.type == "Exception" || x.type == "Assert"));
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
                Aftercare.End(_run, _care, _problems, false);
                Input.Preconditions.HarnessProblems.AddRange(_problems);
                Input.Preconditions.HarnessProblems.AddRange(HarnessFailures);
                var verdict = RegistrationVerdict.Judge(Input);
                // 補助の判定の行。判定 C は観測の行から独立に確かめる（HANDOFF §2 末尾）。
                _run.Write("verdict", ("outcome", verdict.Outcome), ("reasons", verdict.Reasons), ("answers", verdict.Answers),
                    ("implications", verdict.Implications), ("scope", "this PC, Unity " + Application.unityVersion + ", StandaloneWindows64, Editor Play, loose files, fixture only"));
                _run.Write("process-end", ("harnessFailures", HarnessFailures));
            }

            // project の Game の bootstrap は Play に入ると必ず起動する。S0-b（commit 94b0c4a6 の PlayEnvironment）と同じく、process の
            // 環境変数で addressables mode を与え、Content Directory を登録させない。Game の名前は書かず Framework の型から発見する。
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
        /// 生き残る側の物の状態（HANDOFF §2「壊れた」の項目）。prefab か Scene の Renderer から material・texture・shader の参照を取り、
        /// T3 まで保持する。pixel の sha256 は load 直後の値と比べる（texture は読める設定で build してある）。
        /// </summary>
        private sealed class Probe
        {
            // prefab から作った probe か。破棄済みの判定（== null）と区別するため、型の別は bool で持つ。
            private readonly bool _fromPrefab;
            private readonly GameObject? _prefab;
            private readonly Renderer? _renderer;
            private readonly Material? _mat;
            private readonly Texture2D? _tex;
            private readonly Shader? _shader;
            private readonly string _texId, _shaderId, _pixels;

            public Probe(Object loaded)
            {
                _prefab = loaded as GameObject;
                _fromPrefab = _prefab != null;
                _renderer = _prefab != null ? _prefab.GetComponent<Renderer>() : loaded as Renderer;
                (_mat, _tex) = Session.Chain(_renderer);
                _shader = _mat != null ? _mat.shader : null;
                _texId = Session.Id(_tex);
                _shaderId = Session.Id(_shader);
                _pixels = Pixels(_tex);
            }

            public List<string> Broken()
            {
                var b = new List<string>();
                if (_fromPrefab && _prefab == null) b.Add("prefab.null");
                var renderer = _fromPrefab ? (_prefab == null ? null : _prefab.GetComponent<Renderer>()) : _renderer;
                if (renderer == null) b.Add("renderer.null");
                else if (renderer.sharedMaterial == null) b.Add("renderer.sharedMaterial.null");
                if (_mat == null) b.Add("material.null");
                else
                {
                    if (_mat.mainTexture == null) b.Add("material.mainTexture.null");
                    else if (Session.Id(_mat.mainTexture) != _texId) b.Add("material.mainTexture.changed");
                    if (_mat.shader == null) b.Add("material.shader.null");
                    else if (Session.Id(_mat.shader) != _shaderId) b.Add("material.shader.changed");
                }
                if (_tex == null) b.Add("texture.null");
                else
                {
                    if (_tex.width == 0) b.Add("texture.width");
                    if (Pixels(_tex) != _pixels) b.Add("texture.pixels");
                }
                return b;
            }

            public string Describe() => "renderer=" + Session.Id(_renderer) + " material=" + Session.Id(_mat) + " texture=" + _texId
                                        + " shader=" + _shaderId + " width=" + (_tex == null ? -1 : _tex.width) + " pixels=" + _pixels;

            private static string Pixels(Texture2D? tex)
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
                    return RegistrationFixture.Sha256(bytes);
                }
                catch (Exception ex) { return "unreadable: " + ex.GetType().Name; }
            }
        }
    }
}
