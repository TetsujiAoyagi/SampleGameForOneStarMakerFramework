#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

// 同じ file を持つ 2 つの Content Directory の登録のスパイク（docs/handoff/CONTENT_SHARED_FILE_REGISTRATION_SPIKE.md）の
// 判定の写像。入力は文字列・数値・真偽だけで、Unity の型も製品の型も受け取らない。語と判定定義は HANDOFF §2 で実測の前に
// 固定したもので、ここで意味を変えない。判定 C は record の観測の行から独立に確かめ、この出力は補助である（HANDOFF §2 末尾）。
// 実験 code であり、Phase D で git 履歴へ退避して削除する。
namespace OneStarMaker.Tests.Editor.Spikes.SharedFileRegistration
{
    internal static class Kind
    {
        public const string Texture = "texture", Material = "material", Prefab = "prefab";
        // load の結果が == null だった時に LoadIds へ入れる値。整合の規則で未判定になる。
        public const string NullId = "null";
    }

    /// <summary>AC1 の前提（第 1 process の build と棚卸し、第 2 process の照合）と、process の後始末（AC5）。</summary>
    internal sealed class Preconditions
    {
        public bool BuildsSucceeded;
        public bool DigestMatched;
        public bool FixtureAbsentAtSecondStart;
        // 両 directory で同じ file でなかった帰属（"texture.cf"、"material"、"prefab"、"shader"、SharedTex の pixel を持つ file は
        // PixelLocation.NotSameFileItem）。空なら全て同じ file。
        public List<string> NotSameFile = new List<string>();
        // 正の対照の pixel の所在が両方で同じで、その所在の file（.cf、所在が .resS なら .resS も）が名前と sha256 で違う。
        public bool TwinFilesDiffer;
        public List<string> HarnessProblems = new List<string>();
    }

    /// <summary>
    /// 1 つの directory の layout から読んだ、ある texture の pixel の所在の材料（HANDOFF AC1 の (1)〜(4b)）。
    /// layout の参照の向きは、contentfile の項目の ArtifactReferences が texture の項目の Index を指す（S2-0 の FileInventory と同じ）。
    /// </summary>
    internal sealed class PixelEvidence
    {
        // (1) その texture の .cf（出力の file）。帰属できなかった時は空。
        public string CfName = "", CfSha256 = "";
        // (2) Category が contentfile で、ContentHash が .cf と一致する BinaryArtifacts の項目の数。
        public int ContentFileArtifacts;
        // (3) その項目の ArtifactReferences が指す、Category が texture の項目の数。
        public int TextureReferences;
        // (4a) (3) が 1 件の時の .resS（その ContentHash を名前に持つ出力の file）。無ければ空。
        public string ResSName = "", ResSSha256 = "";
        // (4b)(i) どの contentfile の項目からも指されない texture の項目の数。
        public int UnreferencedTextures;
        // (4b)(ii) .cf の size と、source の mip 0 の raw pixel の byte 数（record に format と一緒に書いた値。求められなければ 0）。
        public long CfSize, RawBytes;
    }

    /// <summary>SharedTex の pixel の所在（"resS" か "cf"。決められなければ空）と、両 directory で pixel を持つ file が同じ file か。</summary>
    internal sealed class PixelLocation
    {
        public const string ResS = "resS", Cf = "cf";
        public string Location = "";
        public List<string> Reasons = new List<string>();
        public bool SameFile => Reasons.Count == 0;
        // 同じ file でない時に Preconditions.NotSameFile へ入れる項目。同じ file なら null。
        public string? NotSameFileItem => SameFile ? null : "texture.pixels (" + string.Join("; ", Reasons) + ")";
    }

    /// <summary>1 つの登録の順の、同時登録の確認（AC1）。</summary>
    internal sealed class OrderCheck
    {
        public string Order = "";
        public bool FirstRegistered;
        public bool SecondValid;
        public bool SecondThrew;
        public bool SecondListed;
        // Unity の log か例外の型が record にある。
        public bool FailureRecorded;
        public bool CleanedUp;
        // 拒否の条件から除く原因（"path"、"manifest"、"type-tree"、"harness"）。空なら除かない。
        public string ExcludedCause = "";

        // HANDOFF §2「同時登録の拒否の条件」。
        public bool Refused => FirstRegistered && (!SecondValid || SecondThrew) && !SecondListed && FailureRecorded && CleanedUp && ExcludedCause.Length == 0;
        public bool Registered => FirstRegistered && SecondValid && !SecondThrew && SecondListed;
    }

    /// <summary>1 つの directory だけを登録した基準（AC1）。</summary>
    internal sealed class Baseline
    {
        public string Directory = "";
        public bool PrefabLoaded;
        public bool CoreCountsOne;
        public bool SceneLoaded;
    }

    /// <summary>ある共通物の、load の結果の EntityId と列挙（FindObjectsOfTypeAll）の EntityId。</summary>
    internal sealed class CountObservation
    {
        public string Kind = "";
        public List<string> LoadIds = new List<string>();
        public List<string> EnumeratedIds = new List<string>();
    }

    /// <summary>登録解除の case の観測（AC4）。各 list は、その時点で当たった「壊れた」の項目の名前。</summary>
    internal sealed class UnregisterObservation
    {
        public bool HasPreRelease;
        public List<string> R0Broken = new List<string>();
        public List<string> R1Broken = new List<string>();
        public List<string> T1Broken = new List<string>();
        public List<string> T2Broken = new List<string>();
        public List<string> T3Broken = new List<string>();
        public bool ReferencesHeldToT3 = true;
        public bool ReloadAfterT3Failed;
        public bool CallLoggedError;
        public bool ListedAtT3;
    }

    internal sealed class RoundObservation
    {
        public string Ac = "";
        public string Key = "";
        public string Order = "";
        public int Repetition;
        // (r1) で実行しなかった round は "r1" を書く。
        public string Exclusion = "";
        public bool Ended;
        public int LeftoverAtStart;
        public int DirectoriesAtStart;
        public bool SourceInAssetDatabase;
        public bool CleanedUp;
        // 両方の登録の下で失敗した load（"A:prefab"、"B:scene" 等）。(r2) の材料。
        public List<string> LoadFailures = new List<string>();
        public List<CountObservation> Counts = new List<CountObservation>();
        public UnregisterObservation? Unregister;
    }

    internal sealed class VerdictInput
    {
        public Preconditions Preconditions = new Preconditions();
        public List<OrderCheck> Orders = new List<OrderCheck>();
        public CountObservation? Control;
        public List<Baseline> Baselines = new List<Baseline>();
        public List<RoundObservation> Rounds = new List<RoundObservation>();
    }

    internal sealed class Verdict
    {
        public string Outcome = "";
        public List<string> Reasons = new List<string>();
        public List<string> Answers = new List<string>();
        public List<string> Implications = new List<string>();
    }

    internal static class RegistrationVerdict
    {
        public const string Go = "GO", Undecided = "UNDECIDED";
        public static readonly string[] OrderNames = { "A>B", "B>A" };
        public static readonly string[] Cases = { "U1", "U2", "U3", "U4", "U5", "U6", "U7" };
        // AC4 の case の登録の順（HANDOFF §2.2 の表）。U4 だけが B>A。
        public static string CaseOrder(string id) => id == "U4" ? "B>A" : "A>B";

        // HANDOFF §2「S2 への含意」の表の行。
        public const string RowOneIntact = "row1: one entity, all cases intact";
        public const string RowTwo = "row2: two entities";
        public const string RowRefused = "row3: a case was refused";
        public const string RowBroken = "row4: a case was broken";
        public const string RowNoSimultaneous = "row5: cannot register both";
        public const string RowCannotLoad = "row6: cannot load from one directory under both";

        // round の答え（反復の比較にも使う）。
        public const string ExcludedAnswer = "excluded: simultaneous registration refused in this order";
        public const string BrokenBefore = "broken before unregister";
        public const string Broken = "broken", Intact = "intact";

        /// <summary>HANDOFF §2 の判定の手順。上から順に当てはめる。</summary>
        public static Verdict Judge(VerdictInput input)
        {
            var orders = OrderNames.Select(o => input.Orders.FirstOrDefault(x => x.Order == o)).ToArray();
            // 手順 1: 両方の順で同時登録の拒否。AC2〜AC4 と正の対照の load は適用除外。
            if (orders.All(x => x != null && x.Refused))
                return new Verdict { Outcome = Go, Answers = { "Q1: cannot register both (both orders)", "Q3: cannot register both (both orders)" },
                    Implications = { RowNoSimultaneous } };

            var reasons = new List<string>();
            var p = input.Preconditions;
            if (!p.BuildsSucceeded) reasons.Add("AC1: builds did not succeed");
            if (!p.DigestMatched) reasons.Add("AC1: second process input does not match the first process output");
            if (!p.FixtureAbsentAtSecondStart) reasons.Add("AC1: fixture folder exists at the second process start");
            foreach (var x in p.NotSameFile) reasons.Add("AC1: not the same file in both directories: " + x);
            if (!p.TwinFilesDiffer) reasons.Add("AC1: positive control files are not different");
            reasons.AddRange(p.HarnessProblems.Select(x => "harness: " + x));

            var refused = new HashSet<string>();
            for (var i = 0; i < OrderNames.Length; i++)
            {
                var check = orders[i];
                if (check == null) reasons.Add("AC1: no simultaneous registration check for " + OrderNames[i]);
                else if (check.Refused) refused.Add(check.Order);
                else if (!check.Registered) reasons.Add("AC1: simultaneous registration check for " + check.Order + " is neither registered nor refused");
            }

            var control = Count(input.Control);
            if (control != 2) reasons.Add("AC1: positive control did not count two (" + (control?.ToString() ?? "inconsistent") + ")");
            foreach (var d in new[] { "A", "B" })
            {
                var b = input.Baselines.FirstOrDefault(x => x.Directory == d);
                if (b == null || !b.PrefabLoaded || !b.CoreCountsOne || !b.SceneLoaded) reasons.Add("AC1: baseline of " + d + " failed or is missing");
            }

            var answers = new Dictionary<(string key, int rep), string>();
            foreach (var rep in new[] { 1, 2 })
            foreach (var (ac, key, order) in ExpectedRounds())
            {
                var r = input.Rounds.FirstOrDefault(x => x.Key == key && x.Repetition == rep);
                var label = key + " #" + rep;
                if (r == null) { reasons.Add(label + ": round is missing"); continue; }
                if (refused.Contains(order))
                {
                    if (r.Exclusion != "r1" || !r.Ended) reasons.Add(label + ": order was refused but the round is not marked excluded");
                    else answers[(key, rep)] = ExcludedAnswer;
                    continue;
                }
                if (r.Exclusion.Length != 0) { reasons.Add(label + ": excluded although the order registered"); continue; }
                var answer = RoundAnswer(ac, r, input.Baselines, reasons, label);
                if (answer != null) answers[(key, rep)] = answer;
            }
            foreach (var group in answers.GroupBy(x => x.Key.key))
                if (group.Select(x => x.Value).Distinct().Count() > 1)
                    reasons.Add(group.Key + ": repetitions differ (" + string.Join(" / ", group.Select(x => x.Value)) + ")");

            if (reasons.Count != 0) return new Verdict { Outcome = Undecided, Reasons = reasons };

            var verdict = new Verdict { Outcome = Go };
            foreach (var key in answers.Where(x => x.Key.rep == 1).Select(x => x.Key.key))
                verdict.Answers.Add(key + ": " + answers[(key, 1)]);
            verdict.Implications.AddRange(Implications(answers.Where(x => x.Key.rep == 1).Select(x => (x.Key.key, x.Value)).ToList()));
            return verdict;
        }

        /// <summary>
        /// HANDOFF AC1 の SharedTex の pixel の所在と、pixel を持つ file が両 directory で同じ file か（revision 1）。
        /// .cf 自体の一致は "texture.cf" の帰属で別に見るので、ここでは見ない。所在が .cf だという読みは layout の参照と
        /// file の size からの推論で、.cf の bytes の中の pixel の位置は確かめていない（HANDOFF §2「同じ file」の限界）。
        /// </summary>
        public static PixelLocation LocatePixels(PixelEvidence a, PixelEvidence b)
        {
            var result = new PixelLocation();
            var both = new[] { ("A", a), ("B", b) };
            foreach (var (d, e) in both)
            {
                if (e.ContentFileArtifacts != 1) result.Reasons.Add(d + ": contentfile artifacts " + e.ContentFileArtifacts);
                if (e.TextureReferences >= 2) result.Reasons.Add(d + ": texture references " + e.TextureReferences);
            }
            if (a.TextureReferences != b.TextureReferences) result.Reasons.Add("texture references differ (" + a.TextureReferences + " / " + b.TextureReferences + ")");
            if (result.Reasons.Count != 0) return result;

            if (a.TextureReferences == 1)
            {
                // (4a) pixel は .resS にある。両 directory に実在し、名前と sha256 が同じ時だけ同じ file。
                result.Location = PixelLocation.ResS;
                foreach (var (d, e) in both)
                    if (e.ResSName.Length == 0) result.Reasons.Add(d + ": .resS file is not in the output");
                if (result.Reasons.Count == 0 && (a.ResSName != b.ResSName || a.ResSSha256 != b.ResSSha256)) result.Reasons.Add(".resS differs");
                return result;
            }

            // (4b) 指す texture の項目が 0 件なので pixel は .cf にあるとする。根拠 (i)・(ii) が両 directory で成り立つ時だけ。
            // (ii) は byte 数を求められなかった時（0）も成り立たないとする。小さな値で .cf の size の比較を素通りさせないため。
            result.Location = PixelLocation.Cf;
            foreach (var (d, e) in both)
            {
                if (e.UnreferencedTextures != 0) result.Reasons.Add(d + ": (i) unreferenced texture artifacts " + e.UnreferencedTextures);
                if (e.RawBytes <= 0 || e.CfSize < e.RawBytes) result.Reasons.Add(d + ": (ii) .cf size " + e.CfSize + " is not at least raw bytes " + e.RawBytes);
            }
            return result;
        }

        /// <summary>
        /// HANDOFF AC1 の正の対照の file。(1)〜(3) で所在を決め、所在が .resS なら .cf と .resS の両方、.cf なら .cf が、
        /// 互いに名前と sha256 で違う時だけ真。(4b) の根拠は求めない（対照が示すのは数え方で、file の同一性ではない）。
        /// (2) が 1 件でない、(3) が 2 件以上、TwinA と TwinB で所在が違う時は対照不成立で偽（A2 R-3）。
        /// </summary>
        public static bool ControlFilesDiffer(PixelEvidence a, PixelEvidence b, out string reason)
        {
            static bool Differ(string nameA, string shaA, string nameB, string shaB) =>
                nameA.Length != 0 && nameB.Length != 0 && nameA != nameB && shaA != shaB;
            reason = "";
            if (a.ContentFileArtifacts != 1 || b.ContentFileArtifacts != 1) reason = "contentfile artifacts " + a.ContentFileArtifacts + " / " + b.ContentFileArtifacts;
            else if (a.TextureReferences >= 2 || b.TextureReferences >= 2) reason = "texture references " + a.TextureReferences + " / " + b.TextureReferences;
            else if (a.TextureReferences != b.TextureReferences) reason = "pixel locations differ";
            else if (!Differ(a.CfName, a.CfSha256, b.CfName, b.CfSha256)) reason = ".cf is not different";
            else if (a.TextureReferences == 1 && !Differ(a.ResSName, a.ResSSha256, b.ResSName, b.ResSSha256)) reason = ".resS is not different";
            return reason.Length == 0;
        }

        public static IEnumerable<(string ac, string key, string order)> ExpectedRounds()
        {
            foreach (var order in OrderNames) yield return ("AC2", "AC2 " + order, order);
            foreach (var order in OrderNames) yield return ("AC3", "AC3 " + order, order);
            foreach (var id in Cases) yield return ("AC4", "AC4 " + id, CaseOrder(id));
        }

        /// <summary>HANDOFF §2「実体の数」の整合の規則。答えにできない時は null。</summary>
        public static int? Count(CountObservation? o)
        {
            if (o == null || o.LoadIds.Count == 0 || o.LoadIds.Contains(Kind.NullId)) return null;
            var loaded = new HashSet<string>(o.LoadIds, StringComparer.Ordinal);
            if (!loaded.SetEquals(o.EnumeratedIds)) return null;
            return loaded.Count is 1 or 2 ? loaded.Count : (int?)null;
        }

        /// <summary>AC4 の case の分類。R0 で既に壊れている等、分類できない時は null と理由。</summary>
        public static string? Classify(UnregisterObservation u, out string reason)
        {
            reason = "";
            if (u.HasPreRelease && u.R0Broken.Count != 0) { reason = "R0 already broken: " + string.Join(",", u.R0Broken); return null; }
            if (u.HasPreRelease && u.R1Broken.Count != 0) return BrokenBefore;
            if (!u.ReferencesHeldToT3) { reason = "references were released before T3"; return null; }
            if (u.T1Broken.Count + u.T2Broken.Count + u.T3Broken.Count != 0 || u.ReloadAfterT3Failed) return Broken;
            if (u.CallLoggedError || u.ListedAtT3)
                return "refused (" + string.Join("+", new[] { u.CallLoggedError ? "log" : "", u.ListedAtT3 ? "list" : "" }.Where(x => x.Length != 0)) + ")";
            return Intact;
        }

        private static string? RoundAnswer(string ac, RoundObservation r, List<Baseline> baselines, List<string> reasons, string label)
        {
            var before = reasons.Count;
            if (!r.Ended) reasons.Add(label + ": no end line");
            if (r.LeftoverAtStart != 0) reasons.Add(label + ": objects with fixture names at round start");
            if (r.DirectoriesAtStart != 0) reasons.Add(label + ": directories registered at round start");
            if (r.SourceInAssetDatabase) reasons.Add(label + ": fixture source in AssetDatabase");
            if (!r.CleanedUp) reasons.Add(label + ": round cleanup did not finish");
            if (reasons.Count != before) return null;

            // (r2): 両方の登録の下での load の失敗。基準で同じ load が成功している時だけ round の答えにする。
            if (r.LoadFailures.Count != 0)
            {
                foreach (var failure in r.LoadFailures)
                {
                    var parts = failure.Split(':');
                    var b = baselines.FirstOrDefault(x => x.Directory == parts[0]);
                    var ok = b != null && (parts.Length > 1 && parts[1] == "scene" ? b.SceneLoaded : b.PrefabLoaded);
                    if (!ok) reasons.Add(label + ": load " + failure + " also fails in the baseline");
                }
                return reasons.Count != before ? null : "cannot load from " + string.Join(",", r.LoadFailures.OrderBy(x => x, StringComparer.Ordinal)) + " under both";
            }

            if (ac == "AC4")
            {
                if (r.Unregister == null) { reasons.Add(label + ": no unregister observation"); return null; }
                var c = Classify(r.Unregister, out var why);
                if (c == null) reasons.Add(label + ": " + why);
                return c;
            }
            var kinds = ac == "AC2" ? new[] { Kind.Texture, Kind.Material, Kind.Prefab } : new[] { Kind.Texture, Kind.Material };
            var parts2 = new List<string>();
            foreach (var kind in kinds)
            {
                var n = Count(r.Counts.FirstOrDefault(x => x.Kind == kind));
                if (n == null) reasons.Add(label + ": " + kind + " count does not follow the consistency rule");
                else parts2.Add(kind + "=" + n);
            }
            return reasons.Count != before ? null : string.Join(";", parts2);
        }

        // 行は排他ではない。当たった行を全て返す（HANDOFF §2、外部レビュー E-5・E-9）。
        private static IEnumerable<string> Implications(List<(string key, string answer)> rounds)
        {
            var counts = rounds.Where(x => !x.key.StartsWith("AC4", StringComparison.Ordinal) && x.answer.Contains('=')).ToList();
            var cases = rounds.Where(x => x.key.StartsWith("AC4", StringComparison.Ordinal)).ToList();
            var r1 = rounds.Any(x => x.answer == ExcludedAnswer);
            var r2 = rounds.Any(x => x.answer.StartsWith("cannot load", StringComparison.Ordinal));
            var allOne = counts.Count != 0 && counts.All(x => x.answer.Split(';').All(y => y.EndsWith("=1", StringComparison.Ordinal)));
            if (allOne && !r1 && !r2 && cases.Count == Cases.Length && cases.All(x => x.answer == Intact)) yield return RowOneIntact;
            if (counts.Any(x => x.answer.Contains("=2"))) yield return RowTwo;
            if (cases.Any(x => x.answer.StartsWith("refused", StringComparison.Ordinal))) yield return RowRefused;
            if (cases.Any(x => x.answer == Broken || x.answer == BrokenBefore)) yield return RowBroken;
            if (r1) yield return RowNoSimultaneous;
            if (r2) yield return RowCannotLoad;
        }
    }
}
