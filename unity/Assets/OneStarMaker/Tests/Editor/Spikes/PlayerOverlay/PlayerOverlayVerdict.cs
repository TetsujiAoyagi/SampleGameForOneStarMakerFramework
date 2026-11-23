#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using E = OneStarMaker.Tests.Editor.Spikes.PlayerOverlay.PlayerOverlayExpected;

// 判定の写像（HANDOFF §2 の語と判定定義を、record の行に機械的に当てる）。入力は文字列・数値・真偽だけで、Unity の型も製品の型も
// 受け取らない。語の意味はここで変えない。判定 C は record の観測の行から独立に確かめ、この出力は補助である（HANDOFF §2）。
// S2-1 の OverlayVerdict を写し、対照の順の語（上書きの版が出る前提で対照の class を読む、既知の誤りのある読み）を除き、
// Player の語（P0、P4、X1〜X3、答えにできる失敗、型の欠落、process の間の一致、Editor の対照）を足した。
namespace OneStarMaker.Tests.Editor.Spikes.PlayerOverlay
{
    // record の横に JSON で書く（JsonUtility）ので Serializable にする。
    [Serializable] internal sealed class VerdictResult
    {
        public const string Go = "GO", Undecided = "UNDECIDED";
        public const string Same = "same", DiffersButHolds = "differs but holds", DoesNotHold = "does not hold";
        public string Outcome = "";
        // スパイクの答え。未判定の時も、読めた所までの答えを入れる（AC6 の Editor の対照を待つ時など）。
        public string Spike = "";
        public List<string> Reasons = new List<string>();
        public List<string> Answers = new List<string>();
        public List<string> Differences = new List<string>();
        public bool ProcessVariance;
        public List<string> Variance = new List<string>();
        public bool BaselineHolds, TypeMissing, OnlyTypeMissingFailed, EditorControlRequired;
    }

    internal static class PlayerOverlayVerdict
    {
        public const string BaseAlone = "base-alone", BaseLifetime = "base-lifetime", OverlayAlone = "overlay-alone", Read = "read", Class = "class", Lifetime = "lifetime";
        public static readonly string[] BaselineRounds = { BaseAlone, BaseLifetime, OverlayAlone };
        public static readonly string[] MainRounds = { Read, Class, Lifetime };
        public static readonly string[] AllRounds = BaselineRounds.Concat(MainRounds).ToArray();
        public const int Replicates = 3;
        public static readonly string[] Checkpoints = { "(1)", "(2)", "(3)", "(4)", "(5)" };
        public const string StepRelease = "(6)", Loaded = "loaded";
        public const int SilenceSeconds = 60;
        public const string Strong = "strong", Weak = "weak", NotHeld = "not held", Correct = "correct", Wrong = "wrong",
            Match = "match", Fits = "fits but differs", Exceeds = "exceeds", Intact = "intact", Broken = "broken", SameAsEditor = "same", DiffersFromEditor = "differs";

        private static readonly string[] NonProbeTypes = { "UnityEngine.Transform", "UnityEngine.MeshFilter", "UnityEngine.MeshRenderer" };
        private static readonly string[] ErrorTypes = { "Error", "Exception", "Assert" };

        /// <summary>round の Register の順。主の順は base → 上書き（HANDOFF §2）。launcher はこの順を runner に渡す。</summary>
        public static string[] RegisterOrder(string round) => round switch
        {
            BaseAlone or BaseLifetime => new[] { E.BaseDirectory },
            OverlayAlone => new[] { E.OverlayDirectory },
            _ => new[] { E.BaseDirectory, E.OverlayDirectory },
        };

        /// <summary>P1〜P3 と準備の成否（HANDOFF §2 の危険な条件の前提、AC1）。成り立たない理由の列で、空なら成り立つ。</summary>
        public static List<string> Preconditions(FirstFacts f)
        {
            var reasons = new List<string>();
            if (!f.TargetOk) reasons.Add("AC1: the active build target is not StandaloneWindows64 Player");
            if (!f.BuildsSucceeded) reasons.Add("AC1: base, Player or overlay build did not succeed");
            reasons.AddRange(f.Problems.Select(x => "AC1: " + x));
            if (!f.PackedAssetsRead) reasons.Add("AC1: the Player build report packed assets were not read");
            if (f.PackedFixtureAssets.Count != 0) reasons.Add("AC1: fixture assets other than the runner scene are packed into the Player: " + string.Join(",", f.PackedFixtureAssets));
            if (f.PlayerBuildGuid.Length == 0) reasons.Add("AC1: the Player build report GUID is missing");
            // P1 の根拠は、MonoScript をまとめた file の layout の SourceAssets の一覧（sha256 の差だけでは満たさない）。
            var s = f.ScriptFile;
            if (!s.FoundBase || !s.FoundOverlay) reasons.Add("P1: script file is not in both directories");
            else
            {
                if (s.IdBase != s.IdOverlay) reasons.Add("P1: script file ids differ");
                if (s.ShaBase == s.ShaOverlay) reasons.Add("P1: script file sha256 is the same");
            }
            void Sources(string dir, List<string> list, string present, string absent)
            {
                foreach (var t in new[] { present, E.Both })
                    if (!list.Contains(t)) reasons.Add("P1: " + dir + " script list lacks " + t);
                if (list.Contains(absent)) reasons.Add("P1: " + dir + " script list has " + absent);
            }
            Sources(E.BaseDirectory, f.ScriptSourcesBase, E.BaseOnly, E.OverlayOnly);
            Sources(E.OverlayDirectory, f.ScriptSourcesOverlay, E.OverlayOnly, E.BaseOnly);
            foreach (var item in new[] { E.SceneEdit, E.EditedTex })
            {
                var x = f.Files.FirstOrDefault(y => y.Item == item);
                if (x == null || !x.FoundBase || !x.FoundOverlay) reasons.Add("P2: " + item + " is not in both directories");
                else if (x.IdBase != x.IdOverlay || x.ShaBase == x.ShaOverlay) reasons.Add("P2: " + item + " is not the same id with different sha256");
            }
            foreach (var item in new[] { E.SharedTex, E.SharedMat, E.SharedShader, E.EditedMat })
            {
                var x = f.Files.FirstOrDefault(y => y.Item == item);
                if (x == null || !x.FoundBase || !x.FoundOverlay) reasons.Add("P3: " + item + " is not in both directories");
                else if (x.IdBase != x.IdOverlay || x.NameBase != x.NameOverlay || x.ShaBase != x.ShaOverlay) reasons.Add("P3: " + item + " is not the same file");
            }
            return reasons;
        }

        /// <summary>
        /// HANDOFF §2 の「正しい class」。Missing が無く、probe でない物を除いた列が期待の列と位置ごとに一致する（完全名と `_tag`・`_value`）。
        /// exemptType を渡すと「型の欠落」を読む: その型の位置だけが Missing か列から欠け、他が全て正しい時に typeMissing を真にして偽を返す。
        /// </summary>
        public static bool ClassesCorrect(IReadOnlyList<string> observed, IReadOnlyList<ProbeExpectation> expected, string? exemptType, out bool typeMissing, out string why)
        {
            typeMissing = false;
            var list = observed.Where(x => !NonProbeTypes.Contains(TypeOf(x))).ToArray();
            if (Matches(list, expected, out why)) return true;
            if (exemptType == null || !expected.Any(x => x.FullName == exemptType)) return false;
            var rest = expected.Where(x => x.FullName != exemptType).ToArray();
            var withMissing = expected.Select(x => x.FullName == exemptType ? null : x).ToArray();
            // 欠けた型の位置が Missing で残る形と、列から落ちる形の両方を「型の欠落」と読む（HANDOFF §2 の (f)）。
            if (MatchesWithMissing(list, withMissing) || (!list.Contains(MissingComponent) && Matches(list, rest, out _)))
            {
                typeMissing = true;
                why = "only " + exemptType + " is missing";
            }
            return false;
        }

        public const string MissingComponent = "Missing";
        private static string TypeOf(string component) => component.Split('\t')[0];

        private static bool Matches(IReadOnlyList<string> list, IReadOnlyList<ProbeExpectation> expected, out string why)
        {
            var missing = list.Count(x => x == MissingComponent);
            if (missing != 0) { why = "missing " + missing; return false; }
            if (list.Count != expected.Count) { why = "probe count " + list.Count + " / " + expected.Count; return false; }
            for (var i = 0; i < list.Count; i++)
                if (!SameProbe(list[i], expected[i])) { why = "position " + i + " is " + list[i] + ", expected " + expected[i].FullName + " " + expected[i].Tag + "/" + expected[i].Value; return false; }
            why = "";
            return true;
        }

        private static bool MatchesWithMissing(IReadOnlyList<string> list, IReadOnlyList<ProbeExpectation?> expected) =>
            list.Count == expected.Count && list.Select((x, i) => expected[i] == null ? x == MissingComponent : SameProbe(x, expected[i]!)).All(b => b);

        private static bool SameProbe(string component, ProbeExpectation e)
        {
            var parts = component.Split('\t');
            if (parts[0] != e.FullName || parts.Length < 2) return false;
            var t = Regex.Match(parts[1], "\"_tag\":\"((?:[^\"\\\\]|\\\\.)*)\"");
            var v = Regex.Match(parts[1], "\"_value\":(-?\\d+)");
            return t.Success && v.Success && Regex.Unescape(t.Groups[1].Value) == e.Tag && int.Parse(v.Groups[1].Value, CultureInfo.InvariantCulture) == e.Value;
        }

        /// <summary>1 つの物の実体の数（HANDOFF §2 の「実体の数」）。参照をたどった集合と列挙の集合のうち多い方の数と、2 つが一致したか。</summary>
        public static (int count, bool setsEqual) Count(PovLine c)
        {
            var reached = new HashSet<string>(c.reached.Where(x => x != "null"), StringComparer.Ordinal);
            var enumerated = new HashSet<string>(c.enumerated, StringComparer.Ordinal);
            return (Math.Max(reached.Count, enumerated.Count), !c.reached.Contains("null") && reached.SetEquals(enumerated));
        }

        /// <summary>
        /// HANDOFF §2 の「壊れた」を、held の行から読む。比べる相手は、その物（item と load の世代）の load 直後の行（stage が loaded）。
        /// 戻り値は確認点ごとの壊れた項目。load 直後の行が無い物は harness の不備として "no loaded line" を返す。
        /// </summary>
        public static Dictionary<string, List<string>> BrokenByCheckpoint(IEnumerable<PovLine> heldLines)
        {
            var result = Checkpoints.ToDictionary(x => x, _ => new List<string>());
            var held = heldLines.ToArray();
            foreach (var line in held.Where(x => x.stage != Loaded))
            {
                if (!result.TryGetValue(line.stage, out var list)) continue;
                var loaded = held.FirstOrDefault(x => x.stage == Loaded && x.item == line.item && x.generation == line.generation);
                var label = line.item + "#" + line.generation;
                if (loaded == null) { list.Add(label + ".no loaded line"); continue; }
                if (line.objectId == "null") { list.Add(label + ".object.null"); continue; }
                if (loaded.renderer && !line.renderer) list.Add(label + ".renderer.null");
                for (var i = 0; i < loaded.materials.Length; i++)
                {
                    var b = loaded.materials[i].Split('|');
                    var n = i < line.materials.Length ? line.materials[i].Split('|') : new[] { "null", "null", "null" };
                    if (n[0] == "null") { list.Add(label + ".material[" + i + "].null"); continue; }
                    if (n[1] == "null") list.Add(label + ".material[" + i + "].mainTexture.null");
                    else if (n[1] != b[1]) list.Add(label + ".material[" + i + "].mainTexture.changed");
                    if (n[2] == "null") list.Add(label + ".material[" + i + "].shader.null");
                    else if (n[2] != b[2]) list.Add(label + ".material[" + i + "].shader.changed");
                }
                for (var i = 0; i < loaded.textures.Length; i++)
                {
                    var b = loaded.textures[i].Split('|');
                    var n = i < line.textures.Length ? line.textures[i].Split('|') : new[] { "null", "" };
                    if (n[0] == "null") list.Add(label + ".texture[" + i + "].null");
                    else if (n[1] != b[1]) list.Add(label + ".texture[" + i + "].pixels");
                }
                // 最初から Missing の位置は、Missing のままなら壊れたに数えない（列が load 直後と同じなら変化なし）。
                if (!line.components.SequenceEqual(loaded.components)) list.Add(label + ".components.changed");
            }
            return result;
        }

        /// <summary>HANDOFF §2 の判定定義。未判定の理由を集め、各問・スパイクの答え・Editor と違った点・process の間の違いを返す。</summary>
        public static VerdictResult Judge(FirstFacts first, bool inputMatched, bool leftoverProcesses, IReadOnlyList<ProcessInput> processes, IReadOnlyList<ProcessInput> editorControl)
        {
            var v = new VerdictResult();
            v.Reasons.AddRange(Preconditions(first));
            if (!inputMatched) v.Reasons.Add("launcher: the outputs do not match the first process digests");
            if (leftoverProcesses) v.Reasons.Add("launcher: a launched Player process remains after the run");
            var all = processes.Select(p => new Parsed(p)).ToList();
            foreach (var round in AllRounds)
                if (all.Count(x => x.Round == round) != Replicates) v.Reasons.Add(round + ": " + all.Count(x => x.Round == round) + " processes, expected " + Replicates);
            foreach (var p in all) ValidatePlayer(p, first, v.Reasons);

            // P4 と型の欠落（HANDOFF §2 の P4、「型の欠落」）。base の型が無いのは harness の問題（base の型の記録を渡せていない）。
            foreach (var p in all.Where(x => x.P4 != null))
                foreach (var t in new[] { E.BaseOnly, E.Both })
                    if (!p.TypePresent(t)) v.Reasons.Add(p.Label + ": P4 " + t + " is not in the Player");
            v.TypeMissing = all.Any(x => x.P4 != null && !x.TypePresent(E.OverlayOnly));
            var exempt = new HashSet<string>(StringComparer.Ordinal);
            if (v.TypeMissing)
            {
                var overlayAlone = all.Where(x => x.Round == OverlayAlone).Select(x => new HashSet<string>(x.ErrorLogs(exempt).Select(l => l.message))).ToList();
                if (overlayAlone.Count == Replicates) exempt.UnionWith(overlayAlone.Aggregate((a, b) => { a.IntersectWith(b); return a; }));
            }
            v.Answers.Add("P4: " + string.Join("; ", all.Where(x => x.P4 != null).Select(x => x.Label + " " + string.Join(",", x.P4!.probeTypes))));
            if (exempt.Count != 0) v.Answers.Add("errors that come with the missing type (not counted): " + string.Join(" | ", exempt));
            var scriptMissing = all.SelectMany(x => x.Logs().Where(l => l.message.IndexOf("script", StringComparison.OrdinalIgnoreCase) >= 0 && l.message.IndexOf("missing", StringComparison.OrdinalIgnoreCase) >= 0)).Count();
            v.Answers.Add("log lines saying a script is missing: " + scriptMissing);

            // 単独の基準（AC2・AC7）。
            v.BaselineHolds = true;
            foreach (var round in BaselineRounds)
            {
                var group = all.Where(x => x.Round == round).ToList();
                foreach (var p in group)
                {
                    var problems = BaselineProblems(p, exempt, v.TypeMissing);
                    if (problems.Count == 0) continue;
                    v.BaselineHolds = false;
                    v.Reasons.AddRange(problems.Select(x => p.Label + ": baseline: " + x));
                }
                var differing = Differing(group, exempt);
                if (differing.Count != 0) { v.BaselineHolds = false; v.Reasons.AddRange(differing.Select(x => round + ": baseline differs across processes: " + x)); }
            }

            // 主の順の round。
            var ranks = new List<(Parsed p, int rank, List<string> causes, bool typeOnly)>();
            var q = new Dictionary<string, List<string>> { ["Q1"] = new List<string>(), ["Q2"] = new List<string>(), ["Q3"] = new List<string>(), ["Q4"] = new List<string>(), ["X1"] = new List<string>(), ["X2"] = new List<string>() };
            var x3 = new List<string>();
            foreach (var p in all.Where(x => MainRounds.Contains(x.Round)))
            {
                var causes = new List<string>();
                var differences = new List<string>();
                var before = v.Reasons.Count;
                var errors = p.ErrorLogs(exempt).ToList();
                x3.AddRange(errors.Select(l => p.Label + " " + l.stage + " " + l.logType + ": " + l.message));
                if (errors.Count != 0) causes.Add("X3 " + errors.Count + " Error/Exception in the round stages");
                var failure = FailureAnswer(p, v.BaselineHolds, v.Reasons);
                if (failure != null) causes.Add(failure);
                else if (p.End != null && p.End.completed) ObserveMain(p, v.TypeMissing, q, causes, differences, v.Reasons);
                else if (p.End != null && v.Reasons.Count == before) v.Reasons.Add(p.Label + ": the round was aborted without an operation failure (" + p.End.abortedAt + ")");
                if (v.Reasons.Count != before) continue;
                var typeOnly = causes.Count != 0 && causes.All(c => c.StartsWith("Q2 type missing", StringComparison.Ordinal));
                ranks.Add((p, causes.Count != 0 ? 2 : differences.Count != 0 ? 1 : 0, causes, typeOnly));
                v.Differences.AddRange(differences.Select(x => p.Label + ": " + x));
            }
            foreach (var kv in q.Where(x => x.Value.Count != 0)) v.Answers.Add(kv.Key + ": " + string.Join("; ", kv.Value));
            v.Answers.Add("X3: " + x3.Count + (x3.Count != 0 ? " (" + string.Join(" | ", x3) + ")" : ""));
            foreach (var r in ranks.Where(x => x.causes.Count != 0)) v.Answers.Add(r.p.Label + " does not hold: " + string.Join("; ", r.causes));

            // process の間の違い（主の順では答えに入れ、最も悪い答えを採る。HANDOFF §2）。
            foreach (var round in MainRounds)
            {
                var group = all.Where(x => x.Round == round).ToList();
                var differing = Differing(group, exempt);
                if (differing.Count == 0) continue;
                v.ProcessVariance = true;
                v.Variance.AddRange(differing.Select(x => round + ": " + x));
                v.Variance.Add(round + ": the same in all processes: " + string.Join(", ", SameKeys(group, exempt)));
            }

            var worst = ranks.Count == 0 ? 2 : ranks.Max(x => x.rank);
            if (worst == 0 && v.ProcessVariance) worst = 1;
            v.Spike = ranks.Count == 0 ? "" : worst == 0 ? VerdictResult.Same : worst == 1 ? VerdictResult.DiffersButHolds : VerdictResult.DoesNotHold;
            v.OnlyTypeMissingFailed = worst == 2 && !v.ProcessVariance && ranks.Where(x => x.rank == 2).All(x => x.typeOnly);

            // AC6: 答えが「同じ」でない時だけ、同じ runner の Editor の対照が Editor の実測を再現することを求める。
            v.EditorControlRequired = v.Spike.Length != 0 && v.Spike != VerdictResult.Same;
            if (v.EditorControlRequired)
            {
                var needed = v.ProcessVariance ? Replicates : 1;
                var controls = editorControl.Select(x => new Parsed(x)).ToList();
                foreach (var round in MainRounds)
                {
                    var group = controls.Where(x => x.Round == round).ToList();
                    if (group.Count < needed) v.Reasons.Add("AC6: editor control of " + round + " has " + group.Count + " runs, needs " + needed);
                    foreach (var c in group)
                        foreach (var problem in ControlProblems(c))
                            v.Reasons.Add("AC6: " + c.Label + " does not reproduce the Editor measurement: " + problem);
                }
            }
            v.Outcome = v.Reasons.Count == 0 ? VerdictResult.Go : VerdictResult.Undecided;
            return v;
        }

        // ───── 1 つの process の読み取り ─────

        private sealed class Parsed
        {
            public Parsed(ProcessInput input)
            {
                Input = input;
                Lines = input.Record;
                Header = Lines.FirstOrDefault(x => x.type == "header");
                StartIndex = Lines.FindIndex(x => x.type == "round-start");
                End = Lines.LastOrDefault(x => x.type == "end");
                P4 = Lines.FirstOrDefault(x => x.type == "p4");
            }
            public ProcessInput Input { get; }
            public List<PovLine> Lines { get; }
            public PovLine? Header { get; }
            public int StartIndex { get; }
            public PovLine? End { get; }
            public PovLine? P4 { get; }
            public string Round => Input.Launch.Round;
            public string Label => Input.Launch.Label.Length != 0 ? Input.Launch.Label : Round;
            public IEnumerable<PovLine> Of(string type) => Lines.Where(x => x.type == type);
            // round の段（開始の印から終了行まで）の log だけ。終了行より後に届いた log は数えない（HANDOFF §2 の X3）。
            public IEnumerable<PovLine> Logs()
            {
                var end = End == null ? Lines.Count : Lines.LastIndexOf(End);
                return Lines.Where((x, i) => x.type == "log" && i > StartIndex && i < end);
            }
            public IEnumerable<PovLine> ErrorLogs(HashSet<string> exempt) => Logs().Where(x => ErrorTypes.Contains(x.logType) && !exempt.Contains(x.message));
            public PovLine? FirstFailure => Of("op-result").FirstOrDefault(x => !x.ok);
            public bool TypePresent(string fullName) => P4 != null && P4.probeTypes.Any(x => x.StartsWith(fullName + "|true", StringComparison.Ordinal));
            public PovLine? Scene(string stage, string dir) => Of("scene").FirstOrDefault(x => x.stage == stage && x.dir == dir);
            public PovLine? Texture(string stage, string dir) => Of("texture").FirstOrDefault(x => x.stage == stage && x.dir == dir);
            // load 直後の列は、寿命の round では held の行（最初の世代）が持つ。
            public PovLine? Components(string stage, string item) => stage == Loaded
                ? Of("held").FirstOrDefault(x => x.stage == Loaded && x.item == item && x.generation == 0)
                : Of("components").FirstOrDefault(x => x.stage == stage && x.item == item);
            public List<PovLine> Counts(string point) => Of("count").Where(x => x.point == point).ToList();
        }

        // AC8 と P0、登録の順、handle の root（HANDOFF §2 の (b)(c)(p)、AC8）。外れれば未判定。
        private static void ValidatePlayer(Parsed p, FirstFacts first, List<string> reasons)
        {
            var h = p.Header;
            if (h == null) { reasons.Add(p.Label + ": no header line"); return; }
            if (h.isEditor) reasons.Add(p.Label + ": the header is from the Editor");
            if (h.backend != "IL2CPP") reasons.Add(p.Label + ": backend " + h.backend + " is not IL2CPP");
            if (h.unityVersion != first.ProjectVersion) reasons.Add(p.Label + ": Unity " + h.unityVersion + " is not the project version " + first.ProjectVersion);
            if (h.runId != first.RunId || h.head != first.Head) reasons.Add(p.Label + ": run id or head differs from the first process");
            if (Guid(h.buildGuid) != Guid(first.PlayerBuildGuid)) reasons.Add(p.Label + ": buildGUID " + h.buildGuid + " is not the Player build " + first.PlayerBuildGuid);
            if (h.round != p.Round) reasons.Add(p.Label + ": the header round " + h.round + " is not the launched round");
            if (p.StartIndex < 0) { reasons.Add(p.Label + ": no round-start line"); return; }
            var log = p.Input.PlayerLog;
            var stopped = Array.FindIndex(log, x => x.Contains(E.ProductStoppedLine));
            var mark = Array.FindIndex(log, x => x.Contains(E.StartMark));
            if (stopped < 0 || mark < 0 || stopped > mark) reasons.Add(p.Label + ": P0 (1) the product AfterSceneLoad line is not in the start-up stage of the Player log");
            var p0 = p.Of("p0").FirstOrDefault();
            if (p0 == null || p0.directories != 0 || p0.fixtureObjects != 0 || !p0.scenes.SequenceEqual(new[] { E.BootScene }))
                reasons.Add(p.Label + ": P0 (2) does not hold (directories " + p0?.directories + ", scenes " + string.Join(",", p0?.scenes ?? Array.Empty<string>()) + ", fixture objects " + p0?.fixtureObjects + ")");
            if (p.P4 == null || E.ProbeTypes.Any(t => !p.P4.probeTypes.Any(x => x.StartsWith(t + "|", StringComparison.Ordinal)))) reasons.Add(p.Label + ": P4 values are missing");
            var order = p.Of("op-start").Where(x => x.op == "register").Select(x => x.dir).ToArray();
            if (!order.SequenceEqual(RegisterOrder(p.Round))) reasons.Add(p.Label + ": Register order " + string.Join(">", order) + " is not the round definition");
            foreach (var r in p.Of("op-result").Where(x => x.op == "register" && x.ok))
                if (r.rootIdentity != E.BuildName(r.dir)) reasons.Add(p.Label + ": the root of " + r.dir + " is " + r.rootIdentity);
        }

        private static string Guid(string s) => s.Replace("-", "").ToLowerInvariant();

        // 単独の基準が成り立つか（HANDOFF §2 の語）。型の欠落の位置と、型の欠落に伴う Error は除く。
        private static List<string> BaselineProblems(Parsed p, HashSet<string> exempt, bool typeMissing)
        {
            var problems = new List<string>();
            if (p.End == null || !p.End.completed) problems.Add("no completed end line");
            problems.AddRange(p.Of("op-result").Where(x => !x.ok).Select(x => x.op + " " + x.dir + " " + x.key + " failed: " + x.failure));
            var errors = p.ErrorLogs(exempt).Count();
            if (errors != 0) problems.Add(errors + " Error/Exception logs");
            string? exemptType = typeMissing ? E.OverlayOnly : null;
            void Classes(string stage, string item, string version)
            {
                var c = p.Components(stage, item);
                if (c == null) { problems.Add(item + " components not observed at " + stage); return; }
                if (c.objectName != E.ProbeObjectName(item, version)) problems.Add(item + " object is " + c.objectName);
                else if (!ClassesCorrect(c.components, E.Probes(item, version), exemptType, out var missing, out var why) && !missing) problems.Add(item + " " + why);
            }
            void Version(string stage, string dir, string version)
            {
                var s = p.Scene(stage, dir);
                var t = p.Texture(stage, dir);
                if (s == null || E.VersionOfMarker(s.marker) != version) problems.Add("SceneEdit at " + stage + " is " + (s?.marker ?? "not observed") + ", expected " + version);
                if (stage != "(3)" && (t == null || E.VersionOfPixels(t.pixels) != version)) problems.Add("EditedTex at " + stage + " is " + (t == null ? "not observed" : E.VersionOfPixels(t.pixels)) + ", expected " + version);
            }
            void Counts(string point)
            {
                foreach (var item in E.Counted)
                {
                    var c = p.Counts(point).FirstOrDefault(x => x.item == item);
                    if (c == null) { problems.Add(item + " not counted at " + point); continue; }
                    var (n, setsEqual) = Count(c);
                    if (!setsEqual) problems.Add(item + " at " + point + ": reached and enumerated differ (AC7)");
                    if (n != E.SingleDirectoryCount) problems.Add(item + " at " + point + " count " + n);
                }
            }
            switch (p.Round)
            {
                case BaseAlone:
                    Classes(BaseAlone, E.SceneKeep, ""); Classes(BaseAlone, E.SceneEdit, E.V1); Classes(BaseAlone, E.SharedPrefab, "");
                    Version(BaseAlone, E.BaseDirectory, E.V1); Counts(BaseAlone);
                    break;
                case OverlayAlone:
                    Classes(OverlayAlone, E.SceneEdit, E.V2); Version(OverlayAlone, E.OverlayDirectory, E.V2);
                    break;
                case BaseLifetime:
                    Classes(Loaded, E.SceneKeep, ""); Classes(Loaded, E.SceneEdit, E.V1); Classes(Loaded, E.SharedPrefab, "");
                    Version("(1)", E.BaseDirectory, E.V1); Version("(3)", E.BaseDirectory, E.V1); Counts("(1)");
                    LifetimeProblems(p, problems);
                    break;
            }
            return problems;
        }

        private static void LifetimeProblems(Parsed p, List<string> problems)
        {
            var steps = p.Of("held").Where(x => x.stage != Loaded).Select(x => x.stage).Distinct().ToArray();
            if (!steps.SequenceEqual(Checkpoints)) problems.Add("checkpoints " + string.Join(",", steps));
            foreach (var kv in BrokenByCheckpoint(p.Of("held")).Where(x => x.Value.Count != 0)) problems.Add("broken at " + kv.Key + ": " + string.Join(",", kv.Value));
            var directories = p.Of("directories").FirstOrDefault();
            if (directories == null || directories.count != 0) problems.Add("directories after unregister " + (directories?.count.ToString(CultureInfo.InvariantCulture) ?? "not observed"));
        }

        /// <summary>
        /// 主の順の round の「答えにできる失敗」（HANDOFF §2）。失敗も切れ目も無ければ null。答えにできない失敗は reasons に足して null。
        /// </summary>
        private static string? FailureAnswer(Parsed p, bool baselineHolds, List<string> reasons)
        {
            // harness の例外で打ち切った round は、その後の後始末の操作の失敗や record の切れ目を Unity の答えにしない
            // （HANDOFF §2 の (k)。round の定義の操作の列が harness の不備で途切れた後の物で、未判定として B へ戻す）。
            if (p.Of("harness-exception").Any() || p.End != null && p.End.abortedAt.StartsWith("harness:", StringComparison.Ordinal))
            { reasons.Add(p.Label + ": the round was aborted by a harness exception (" + (p.End?.abortedAt ?? "no end line") + ")"); return null; }
            var failure = p.FirstFailure;
            var registers = p.Of("op-start").Where(x => x.op == "register").ToList();
            var firstRegisterOk = p.Of("op-result").Any(x => x.op == "register" && x.ok && x.dir == E.BaseDirectory);
            if (failure != null && failure.op == "register" && failure.dir == E.BaseDirectory) { reasons.Add(p.Label + ": the first Register (base) failed in the main order"); return null; }
            string? answer = null;
            if (failure != null) answer = "does not hold (" + Kind(failure.op) + ": " + failure.op + " " + failure.dir + " " + failure.key + " " + failure.failure + " " + failure.exceptionType + " " + failure.message + ")";
            else if (p.End == null)
            {
                var second = registers.Count >= 2 ? p.Lines.IndexOf(registers[1]) : -1;
                if (second < 0 || p.Lines.Count - 1 < second) { reasons.Add(p.Label + ": the record ends before the second Register without an end line"); return null; }
                var launch = p.Input.Launch;
                var limit = p.Of("limit-exceeded").FirstOrDefault();
                if (limit == null && launch.StopReason != "launcher-limit") answer = "does not hold (crashed: last line " + p.Lines.Last().type + " " + p.Lines.Last().op + " " + p.Lines.Last().key + ", exit " + launch.ExitCode + ")";
                else
                {
                    // 「終わらない」は、止める直前の 60 秒に行が増えていない時だけ（runner の上限の超過の行は数えない）。
                    var last = p.Lines.Last(x => x.type != "limit-exceeded");
                    var stoppedAt = limit != null ? limit.at : launch.EndedAt;
                    if (!TryTime(last.at, out var a) || !TryTime(stoppedAt, out var b) || (b - a).TotalSeconds < SilenceSeconds)
                    { reasons.Add(p.Label + ": stopped at the limit while the record was still growing"); return null; }
                    answer = "does not hold (does not finish: last line " + last.type + " " + last.op + " " + last.key + ")";
                }
            }
            if (answer == null) return null;
            if (p.Header == null || p.P4 == null || !firstRegisterOk) { reasons.Add(p.Label + ": an answerable failure lacks the header, P4 or the first Register success"); return null; }
            if (!baselineHolds) { reasons.Add(p.Label + ": an answerable failure while the baseline does not hold"); return null; }
            return answer;
        }

        private static string Kind(string op) => op switch
        {
            "register" => "cannot register",
            "load-scene" or "load-object" => "cannot load",
            _ => "cannot unload or unregister",
        };

        private static bool TryTime(string s, out DateTime t) => DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out t);

        // 主の順の round の観測から、問ごとの答えと、Editor と違った点・成り立たない理由を決める（HANDOFF §2 の各問）。
        private static void ObserveMain(Parsed p, bool typeMissing, Dictionary<string, List<string>> q, List<string> causes, List<string> differences, List<string> reasons)
        {
            var label = p.Label;
            switch (p.Round)
            {
                case Read:
                    var items = new[] { (E.SceneEdit, (Func<string, string>)(d => p.Scene(Read, d) is { } s ? E.VersionOfMarker(s.marker) : "")), (E.EditedTex, (Func<string, string>)(d => p.Texture(Read, d) is { } t ? E.VersionOfPixels(t.pixels) : "")) };
                    var q1 = items.Select(x =>
                    {
                        var b = x.Item2(E.BaseDirectory);
                        var o = x.Item2(E.OverlayDirectory);
                        return (x.Item1, b, o, answer: o == E.V2 && b == E.V2 ? Strong : o == E.V2 && b == E.V1 ? Weak : NotHeld);
                    }).ToArray();
                    var worstQ1 = q1.Any(x => x.answer == NotHeld) ? NotHeld : q1.Any(x => x.answer == Weak) ? Weak : Strong;
                    q["Q1"].Add(label + " " + worstQ1 + " (" + string.Join(", ", q1.Select(x => x.Item1 + " base=" + x.b + " overlay=" + x.o)) + ")");
                    if (worstQ1 == NotHeld) causes.Add("Q1 not held");
                    else if (worstQ1 == Weak) differences.Add("Q1 weak");
                    var ids = p.Of("ids").FirstOrDefault();
                    var tb = p.Texture(Read, E.BaseDirectory);
                    var to = p.Texture(Read, E.OverlayDirectory);
                    var count = p.Counts(Read).FirstOrDefault(x => x.item == E.EditedTex);
                    if (ids == null || tb == null || to == null || count == null) { reasons.Add(label + ": read observations are missing"); return; }
                    // X1 の「実体 1」は、2 つの load の EntityId が等しいことに加えて、参照をたどった数と列挙の数の多い方が 1 であること（AC7。
                    // 主の順で 2 つが違う時は多い方の数で X1 を読む）。列挙だけが 2 つ目の実体を見た時に「同じ」と読まないため。
                    var (texObjects, texSetsEqual) = Count(count);
                    var x1 = ids.sceneIdsEqual && ids.loadableIdsEqual && tb.entityId == to.entityId && texObjects == 1 ? SameAsEditor : DiffersFromEditor;
                    q["X1"].Add(label + " " + x1 + " (scene ids equal " + ids.sceneIdsEqual + ", loadable ids equal " + ids.loadableIdsEqual + ", one EditedTex object " + (tb.entityId == to.entityId) + ", EditedTex objects " + texObjects + ")");
                    if (x1 == DiffersFromEditor) differences.Add("X1 differs");
                    if (!texSetsEqual) differences.Add("EditedTex reached and enumerated differ (" + string.Join("|", count.reached) + " / " + string.Join("|", count.enumerated) + ", using " + texObjects + ")");
                    break;
                case Class:
                    var wrong = new List<string>();
                    var missingOnly = new List<string>();
                    foreach (var (item, version) in new[] { (E.SceneKeep, ""), (E.SceneEdit, E.V2), (E.SharedPrefab, "") })
                    {
                        var c = p.Components(Class, item);
                        if (c == null) { reasons.Add(label + ": " + item + " components not observed"); return; }
                        // 上書きの root から load した SceneEdit が上書き版でなければ、v2 の期待の列と別の class として誤りになる（Q2 の語のまま）。
                        if (ClassesCorrect(c.components, E.Probes(item, version), typeMissing ? E.OverlayOnly : null, out var missing, out var why)) continue;
                        if (missing) missingOnly.Add(item + " " + why); else wrong.Add(item + " (" + c.objectName + ") " + why);
                    }
                    var q2 = wrong.Count == 0 && missingOnly.Count == 0 ? Correct : Wrong + " (" + string.Join("; ", wrong.Concat(missingOnly)) + ")";
                    q["Q2"].Add(label + " " + q2);
                    if (wrong.Count != 0) causes.Add("Q2 wrong");
                    else if (missingOnly.Count != 0) causes.Add("Q2 type missing (ProbeOverlayOnly is not in the Player)");
                    Q3(p, Class, q, causes, differences, reasons);
                    var keep = p.Of("keep-edited").FirstOrDefault();
                    if (keep == null) { reasons.Add(label + ": SceneKeep EditedMat texture not observed"); return; }
                    var x2 = E.VersionOfPixels(keep.pixels) == E.V1 ? SameAsEditor : DiffersFromEditor;
                    q["X2"].Add(label + " " + x2 + " (" + E.VersionOfPixels(keep.pixels) + ")");
                    if (x2 == DiffersFromEditor) differences.Add("X2 differs");
                    break;
                case Lifetime:
                    Q3(p, "(1)", q, causes, differences, reasons);
                    var problems = new List<string>();
                    LifetimeProblems(p, problems);
                    var v3 = p.Scene("(3)", E.OverlayDirectory);
                    q["Q4"].Add(label + " " + (problems.Count == 0 ? Intact : Broken + " (" + string.Join("; ", problems) + ")") + ", SceneEdit at (3) " + (v3 == null ? "not observed" : E.VersionOfMarker(v3.marker)));
                    if (problems.Any(x => x.StartsWith("checkpoints", StringComparison.Ordinal) || x.Contains("no loaded line"))) { reasons.Add(label + ": lifetime checkpoints are not recorded as defined"); return; }
                    if (problems.Count != 0) causes.Add("Q4 broken");
                    break;
            }
        }

        // Q3 の 1 つの計数の時点（HANDOFF §2 の Q3、AC7 の主の順）。
        private static void Q3(Parsed p, string point, Dictionary<string, List<string>> q, List<string> causes, List<string> differences, List<string> reasons)
        {
            var parts = new List<string>();
            var match = true;
            var exceeds = false;
            foreach (var item in E.Counted)
            {
                var c = p.Counts(point).FirstOrDefault(x => x.item == item);
                if (c == null) { reasons.Add(p.Label + ": " + item + " not counted at " + point); return; }
                var (n, setsEqual) = Count(c);
                // load の操作が全て成功しているのに 0 なら、数えられていない（未判定）。失敗があればここには来ない。
                if (n == 0) { reasons.Add(p.Label + ": " + item + " count is 0 at " + point + " although every load succeeded"); return; }
                if (!setsEqual) differences.Add(item + " at " + point + ": reached " + string.Join("|", c.reached) + " and enumerated " + string.Join("|", c.enumerated) + " differ (using " + n + ")");
                if (n != E.EditorMeasuredCount(item)) match = false;
                if (E.CountedAtMostTwo.Contains(item) ? n > 2 : n != 1) exceeds = true;
                parts.Add(item + "=" + n);
            }
            var answer = exceeds ? Exceeds : match ? Match : Fits;
            q["Q3"].Add(p.Label + " at " + point + " " + answer + " (" + string.Join(",", parts) + ")");
            if (exceeds) causes.Add("Q3 exceeds");
            else if (!match) differences.Add("Q3 counts differ from the Editor at " + point);
        }

        // AC6: Editor の対照が、Editor の実測（現況の (a)〜(e)）を再現するか。外れた点の列（空なら再現した）。
        private static List<string> ControlProblems(Parsed c)
        {
            var problems = new List<string>();
            var h = c.Header;
            if (h == null || !h.isEditor) problems.Add("the header is not from the Editor");
            else
            {
                if (!h.domainReloadDisabled) problems.Add("Domain Reload is not disabled");
                if (!h.firstPlay) problems.Add("not the first Play of the Unity process");
            }
            if (c.End == null || !c.End.completed) problems.Add("no completed end line");
            problems.AddRange(c.Of("op-result").Where(x => !x.ok).Select(x => x.op + " " + x.dir + " " + x.key + " failed"));
            var errors = c.ErrorLogs(new HashSet<string>()).Count();
            if (errors != 0) problems.Add(errors + " Error/Exception logs");
            var q = new Dictionary<string, List<string>> { ["Q1"] = new List<string>(), ["Q2"] = new List<string>(), ["Q3"] = new List<string>(), ["Q4"] = new List<string>(), ["X1"] = new List<string>(), ["X2"] = new List<string>() };
            var causes = new List<string>();
            var differences = new List<string>();
            ObserveMain(c, false, q, causes, differences, problems);
            problems.AddRange(causes);
            // 集合の不一致（AC7）は Editor の実測の数え方の違いで、それだけでは再現の外れにしない。数の違いと他の違いは外れ。
            problems.AddRange(differences.Where(x => !x.Contains("reached and enumerated")));
            return problems;
        }

        // ───── process の間の一致（HANDOFF §2 の判定定義。EntityId の値、時刻、pid は比べない） ─────

        private static Dictionary<string, string> ObservationKeys(Parsed p, HashSet<string> exempt)
        {
            var keys = new Dictionary<string, string>(StringComparer.Ordinal);
            var i = 0;
            foreach (var r in p.Of("op-result")) keys["op#" + i++ + " " + r.op + " " + r.dir + " " + r.key] = "ok=" + r.ok + " " + r.failure;
            foreach (var s in p.Of("scene")) keys["scene " + s.stage + " " + s.dir + " " + s.key] = s.marker;
            foreach (var t in p.Of("texture")) keys["texture " + t.stage + " " + t.dir + " " + t.key] = t.pixels;
            foreach (var x in p.Of("ids")) keys["ids"] = "scene=" + x.sceneIdsEqual + " loadable=" + x.loadableIdsEqual;
            var tb = p.Texture(Read, E.BaseDirectory);
            var to = p.Texture(Read, E.OverlayDirectory);
            if (tb != null && to != null) keys["EditedTex one object"] = (tb.entityId == to.entityId).ToString();
            foreach (var c in p.Of("components")) keys["components " + c.stage + " " + c.item] = c.objectName + " [" + string.Join(", ", c.components) + "]";
            foreach (var c in p.Of("count")) { var (n, eq) = Count(c); keys["count " + c.point + " " + c.item] = n + " setsEqual=" + eq; }
            foreach (var k in p.Of("keep-edited")) keys["keep-edited"] = k.pixels;
            foreach (var kv in BrokenByCheckpoint(p.Of("held"))) if (p.Of("held").Any()) keys["broken " + kv.Key] = string.Join(",", kv.Value);
            foreach (var d in p.Of("directories")) keys["directories"] = d.count.ToString(CultureInfo.InvariantCulture);
            foreach (var g in p.ErrorLogs(exempt).GroupBy(x => x.stage)) keys["errors " + g.Key] = g.Count().ToString(CultureInfo.InvariantCulture);
            keys["end"] = p.End == null ? "none" : "completed=" + p.End.completed + " " + p.End.abortedAt;
            return keys;
        }

        private static List<string> Differing(IReadOnlyList<Parsed> group, HashSet<string> exempt)
        {
            var maps = group.Select(x => (x.Label, keys: ObservationKeys(x, exempt))).ToList();
            return maps.SelectMany(x => x.keys.Keys).Distinct().OrderBy(x => x, StringComparer.Ordinal)
                .Where(k => maps.Select(m => m.keys.TryGetValue(k, out var value) ? value : "(none)").Distinct().Count() > 1)
                .Select(k => k + ": " + string.Join(" | ", maps.Select(m => m.Label + "=" + (m.keys.TryGetValue(k, out var value) ? value : "(none)"))))
                .ToList();
        }

        private static IEnumerable<string> SameKeys(IReadOnlyList<Parsed> group, HashSet<string> exempt)
        {
            var maps = group.Select(x => ObservationKeys(x, exempt)).ToList();
            return maps.SelectMany(x => x.Keys).Distinct().OrderBy(x => x, StringComparer.Ordinal)
                .Where(k => maps.All(m => m.ContainsKey(k)) && maps.Select(m => m[k]).Distinct().Count() == 1);
        }
    }
}
