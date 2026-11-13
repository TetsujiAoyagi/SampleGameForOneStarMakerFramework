#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;

namespace OneStarMaker.Tests.Editor.Spikes.OverlayBuild
{
    /// <summary>
    /// 実機の harness の fixture の仕様（HANDOFF CONTENT_OVERLAY_BUILD §2.2）。Unity に依存しない値だけを持つ。
    /// 6 root、v2 で変える 5 つの source file、E の 4 path、観測点と、その v1 / v2 の期待値。
    /// </summary>
    internal static class OverlaySpec
    {
        public const string Folder = "Assets/OsmOvb";
        public const string V1 = "v1";
        public const string V2 = "v2";
        public const int TextureSize = 8;
        public const string DeclarationName = "ovb_purpose";
        public const string BaseSet = "ovb_base";
        public const string OverlaySet = "ovb_ovl";

        public const string SceneDirect = "SceneDirect";
        public const string SceneInstance = "SceneInstance";
        public const string SceneNested = "SceneNested";
        public const string PrefabRoot = "PrefabRoot";
        public const string SceneClean = "SceneClean";
        public const string SceneUnlisted = "SceneUnlisted";
        public static readonly string[] Roots = { SceneDirect, SceneInstance, SceneNested, PrefabRoot, SceneClean, SceneUnlisted };
        // AC2: 上書きに入るべき root（ordinal 順）。
        public static readonly string[] OverlayRoots = { PrefabRoot, SceneDirect, SceneInstance, SceneNested };

        public static string RootPath(string root) => Folder + "/" + root + (root == PrefabRoot ? ".prefab" : ".unity");

        public static string Shader => Folder + "/Shader.shader";
        public static string Tex(string name) => Folder + "/" + name + ".png";
        public static string Mat(string name) => Folder + "/" + name + ".mat";
        public static string Prefab(string name) => Folder + "/" + name + ".prefab";

        public const string EditedTex = "EditedTex";
        public const string SharedTex = "SharedTex";
        public const string UnlistedTex = "UnlistedTex";
        public const string CanaryTex = "CanaryTex";
        public const string CleanOnlyTex = "CleanOnlyTex";
        public const string UnusedTex = "UnusedTex";
        public static readonly string[] Textures = { EditedTex, SharedTex, UnlistedTex, CanaryTex, CleanOnlyTex, UnusedTex };
        // texture ごとに別の material（SceneDirect は別々の material で 3 つを直接参照する）。
        public static readonly Dictionary<string, string> MaterialOf = new Dictionary<string, string>
        {
            [EditedTex] = "EditedMat", [SharedTex] = "SharedMat", [UnlistedTex] = "UnlistedMat", [CanaryTex] = "CanaryMat", [CleanOnlyTex] = "CleanMat",
        };

        public const string EditedPrefab = "EditedPrefab";
        public const string InnerPrefab = "InnerPrefab";
        public const string OuterPrefab = "OuterPrefab";
        public const string Probe = "Probe";
        public const string InnerProbe = "InnerProbe";

        // Scene の中の Renderer を持つ GameObject と、その material の並び（texture の名前）。
        public const string DirectObject = "Direct";
        public const string CleanObject = "Clean";
        public const string UnlistedObject = "Unlisted";
        public static readonly string[] DirectSlots = { EditedTex, SharedTex, UnlistedTex };
        public static readonly string[] CleanSlots = { SharedTex, CleanOnlyTex };
        public static readonly string[] UnlistedSlots = { UnlistedTex, CanaryTex };
        public static readonly string[] PrefabRootSlots = { EditedTex };

        // v2 で変える source file（前提 P2）。
        public static readonly string[] ChangedInV2 =
            new[] { Tex(EditedTex), Prefab(EditedPrefab), Prefab(InnerPrefab), Tex(UnlistedTex), Tex(CanaryTex) }
                .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        // E（渡す編集）。UnlistedTex と CanaryTex は入れない。
        public static readonly string[] Edits = { Tex(EditedTex), Prefab(EditedPrefab), Prefab(InnerPrefab), Tex(UnusedTex) };

        public static IEnumerable<string> SourceFiles() =>
            new[] { Shader }.Concat(Textures.Select(Tex)).Concat(MaterialOf.Values.Select(Mat))
                .Concat(new[] { EditedPrefab, InnerPrefab, OuterPrefab }.Select(Prefab)).Concat(Roots.Select(RootPath))
                .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal);

        // 単色の texture の色。v1 と v2 で変わるのは EditedTex・UnlistedTex・CanaryTex だけ。
        public static byte[] Color(string texture, string version)
        {
            var v2 = version == V2;
            return texture switch
            {
                EditedTex => v2 ? new byte[] { 30, 200, 60, 255 } : new byte[] { 200, 40, 40, 255 },
                UnlistedTex => v2 ? new byte[] { 20, 190, 210, 255 } : new byte[] { 20, 30, 220, 255 },
                CanaryTex => v2 ? new byte[] { 10, 10, 10, 255 } : new byte[] { 250, 240, 20, 255 },
                SharedTex => new byte[] { 120, 120, 120, 255 },
                CleanOnlyTex => new byte[] { 90, 160, 90, 255 },
                _ => new byte[] { 255, 255, 255, 255 },
            };
        }

        // prefab の root ではない子の位置。v1 と v2 で変える。
        public static float[] Position(string child, string version) => (child, version == V2) switch
        {
            (Probe, false) => new[] { 1f, 2f, 3f },
            (Probe, true) => new[] { 4f, 5f, 6f },
            (InnerProbe, false) => new[] { 7f, 8f, 9f },
            _ => new[] { 10f, 11f, 12f },
        };

        /// <summary>観測点の定義。Scene の観測点は、Scene の階層の path で引く GameObject。Object の root は asset そのもの。</summary>
        internal sealed class Point
        {
            public Point(string id, string root, string objectPath, string texture, int slot, string child)
            { Id = id; Root = root; ObjectPath = objectPath; Texture = texture; Slot = slot; Child = child; }
            public string Id { get; }
            public string Root { get; }
            /// <summary>Scene の root の GameObject からの階層 path。Object の root では空。</summary>
            public string ObjectPath { get; }
            /// <summary>Renderer の material の texture を読む点では texture の名前、位置を読む点では空。</summary>
            public string Texture { get; }
            public int Slot { get; }
            /// <summary>位置を読む点では子の名前。</summary>
            public string Child { get; }
            public bool IsTexture => Texture.Length != 0;
            public string Expected(string version) => IsTexture ? TextureValue(Color(Texture, version)) : PositionValue(Position(Child, version));
        }

        public static readonly Point[] Points =
        {
            // AC6
            new Point("SceneDirect.EditedTex", SceneDirect, DirectObject, EditedTex, 0, ""),
            new Point("PrefabRoot.EditedTex", PrefabRoot, "", EditedTex, 0, ""),
            new Point("SceneInstance.Probe", SceneInstance, EditedPrefab + "/" + Probe, "", -1, Probe),
            new Point("SceneNested.InnerProbe", SceneNested, OuterPrefab + "/" + InnerPrefab + "/" + InnerProbe, "", -1, InnerProbe),
            // AC7
            new Point("SceneDirect.UnlistedTex", SceneDirect, DirectObject, UnlistedTex, 2, ""),
            new Point("SceneUnlisted.UnlistedTex", SceneUnlisted, UnlistedObject, UnlistedTex, 0, ""),
            new Point("SceneUnlisted.CanaryTex", SceneUnlisted, UnlistedObject, CanaryTex, 1, ""),
        };
        public static readonly string[] Ac6Points = { "SceneDirect.EditedTex", "PrefabRoot.EditedTex", "SceneInstance.Probe", "SceneNested.InnerProbe" };
        public const string CanaryPoint = "SceneUnlisted.CanaryTex";
        public const string BaseSideUnlisted = "SceneUnlisted.UnlistedTex";
        public const string OverlaySideUnlisted = "SceneDirect.UnlistedTex";

        // 値の文字列。texture は読み出した RGBA の bytes の sha256、位置は R 形式の 3 値。
        public static string TextureValue(byte[] solidColor)
        {
            var bytes = new byte[TextureSize * TextureSize * 4];
            for (var i = 0; i < bytes.Length; i++) bytes[i] = solidColor[i % 4];
            return TextureValueOfBytes(bytes);
        }

        public static string TextureValueOfBytes(byte[] rgba) => "px:" + Sha256(rgba);

        public static string PositionValue(float[] xyz) =>
            "pos:" + string.Join(",", xyz.Select(v => v.ToString("R", CultureInfo.InvariantCulture)));

        public static string Sha256(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(bytes).Select(x => x.ToString("x2")));
        }
    }

    [Serializable] internal sealed class PointValue
    {
        public string Id = "";
        public string Path = "";
        public string Value = "";
    }

    /// <summary>build の process が record と一緒に残す事実（round と判定の入力）。</summary>
    [Serializable] internal sealed class BuildFacts
    {
        public string RunId = "";
        public string Head = "";
        public bool BaseBuilt;
        public bool BasePublished;
        public bool OverlayBuilt;
        public string BaseDirectory = "";
        public string OverlayDirectory = "";
        public string OverlayIdentity = "";
        public string BaseRevision = "";
        public string BaseManifestDigest = "";
        public List<PointValue> BeforeBase = new List<PointValue>();
        public List<PointValue> BeforeOverlay = new List<PointValue>();
        public List<string> ChangedSources = new List<string>();
        public List<string> SelectedRoots = new List<string>();
        public List<string> RootReasons = new List<string>();
        public List<string> Unmatched = new List<string>();
        public List<string> SharedAssets = new List<string>();
        public bool PreflightOverlay;
        public bool OutcomeOverlay;
        public List<string> PreflightBase = new List<string>();
        public List<string> PreflightEdits = new List<string>();
        public List<string> PreflightRoots = new List<string>();
        public List<string> PreflightUnmatched = new List<string>();
        public List<string> PreflightShared = new List<string>();
        public bool BaseUnchanged;
        public bool FixtureDeleted;
        public List<string> Problems = new List<string>();
        public bool Finished;
    }

    /// <summary>round の process 1 つの観測。</summary>
    [Serializable] internal sealed class RoundFacts
    {
        public string Process = "";
        public string RunId = "";
        public string Head = "";
        public bool DomainReloadDisabled;
        public bool SourceInAssetDatabase;
        public bool BaseRegistered;
        public bool OverlayRegistered;
        public List<string> BaseKeys = new List<string>();
        public List<string> OverlayKeys = new List<string>();
        public string BaseDeclaration = "";
        public string OverlayDeclaration = "";
        public List<string> Resolved = new List<string>();
        public List<PointValue> Points = new List<PointValue>();
        public bool SceneCleanLoaded;
        public int SharedTexCount = -1;
        public int CleanOnlyTexCount = -1;
        public int ErrorLogs;
        public bool Completed;
        public bool CleanedUp;
        public List<string> Problems = new List<string>();
    }

    /// <summary>
    /// 観測から AC ごとの成否と §2.6 の分類を返す pure な写像（補助）。判定 C は record の行から独立に読む。
    /// 分類: <see cref="Undetermined"/>（B へ: P1・P2 か canary の不成立、harness の不備）、<see cref="UndeterminedOwner"/>（2 つの process の値が
    /// 食い違う）、<see cref="NoGo"/>（集合・版・実体の数の不成立）、<see cref="FailB"/>（その他の不成立）、<see cref="AllHold"/>。
    /// </summary>
    internal static class OverlayBuildVerdict
    {
        public const string Undetermined = "undetermined-return-to-B";
        public const string UndeterminedOwner = "undetermined-return-to-owner";
        public const string NoGo = "no-go";
        public const string FailB = "fail-return-to-B";
        public const string AllHold = "all-hold";

        internal sealed class Result
        {
            public string Outcome = "";
            public List<string> Acs = new List<string>();
            public List<string> Reasons = new List<string>();
        }

        public static Result Judge(BuildFacts build, IReadOnlyList<RoundFacts> rounds)
        {
            var r = new Result();
            var pre = Preconditions(build);
            if (pre.Count != 0) { r.Outcome = Undetermined; r.Reasons.AddRange(pre); return r; }
            if (rounds.Count != 2 || rounds.Any(x => !x.Completed || !x.CleanedUp || x.Problems.Count != 0 || !x.BaseRegistered || !x.OverlayRegistered
                                                     || !x.DomainReloadDisabled || x.SourceInAssetDatabase || x.RunId != build.RunId))
            {
                r.Outcome = Undetermined;
                r.Reasons.Add("rounds incomplete or harness problems: " + string.Join("; ", rounds.Select(x => x.Process + " completed=" + x.Completed
                    + " cleanedUp=" + x.CleanedUp + " problems=" + string.Join("|", x.Problems))));
                return r;
            }
            var v1 = build.BeforeBase.ToDictionary(x => x.Id, x => x.Value);
            var v2 = build.BeforeOverlay.ToDictionary(x => x.Id, x => x.Value);
            foreach (var round in rounds)
                if (Value(round, OverlaySpec.CanaryPoint) != v1[OverlaySpec.CanaryPoint])
                {
                    r.Outcome = Undetermined;
                    r.Reasons.Add(round.Process + ": canary is not v1 (" + Value(round, OverlaySpec.CanaryPoint) + "); the observation does not read the directory");
                    return r;
                }
            if (!SameObservation(rounds[0], rounds[1], out var diff))
            {
                r.Outcome = UndeterminedOwner;
                r.Reasons.Add("the two processes disagree: " + diff);
                return r;
            }

            var round0 = rounds[0];
            var noGo = new List<string>();
            var failB = new List<string>();
            void Ac(string id, bool holds, string why, List<string> sink)
            {
                r.Acs.Add(id + "=" + (holds ? "holds" : "fails"));
                if (!holds) sink.Add(id + ": " + why);
            }

            Ac("AC2", build.SelectedRoots.SequenceEqual(OverlaySpec.OverlayRoots), "selected " + string.Join(",", build.SelectedRoots), noGo);
            Ac("AC5.entries", round0.OverlayKeys.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(OverlaySpec.OverlayRoots),
                "overlay keys " + string.Join(",", round0.OverlayKeys), noGo);
            Ac("AC5.declaration", round0.BaseDeclaration == OverlaySpec.DeclarationName && round0.OverlayDeclaration == OverlaySpec.DeclarationName,
                "declarations " + round0.BaseDeclaration + "/" + round0.OverlayDeclaration, failB);
            Ac("AC5.baseUnchanged", build.BaseUnchanged, "base content or workspace changed", failB);
            var stale = OverlaySpec.Ac6Points.Where(p => Value(round0, p) != v2[p]).ToArray();
            Ac("AC6.versions", stale.Length == 0, "not v2: " + string.Join(",", stale.Select(p => p + "=" + Value(round0, p))), noGo);
            Ac("AC6.errorLogs", rounds.All(x => x.ErrorLogs == 0), "error logs " + string.Join(",", rounds.Select(x => x.ErrorLogs)), failB);
            Ac("AC7.sceneClean", rounds.All(x => x.SceneCleanLoaded), "SceneClean did not load", failB);
            Ac("AC7.baseSide", Value(round0, OverlaySpec.BaseSideUnlisted) == v1[OverlaySpec.BaseSideUnlisted],
                "the base-only root resolved UnlistedTex to the overlay version", noGo);
            Ac("AC7.overlaySide", Value(round0, OverlaySpec.OverlaySideUnlisted) == v2[OverlaySpec.OverlaySideUnlisted],
                "the overlay root did not carry the local UnlistedTex (C10)", noGo);
            Ac("AC8.counts", round0.SharedTexCount == 2 && round0.CleanOnlyTexCount == 1,
                "SharedTex=" + round0.SharedTexCount + " CleanOnlyTex=" + round0.CleanOnlyTexCount, noGo);
            var shared = new HashSet<string>(build.SharedAssets, StringComparer.Ordinal);
            Ac("AC8.report", shared.Contains(OverlaySpec.Tex(OverlaySpec.SharedTex)) && shared.Contains(OverlaySpec.Tex(OverlaySpec.UnlistedTex))
                             && !shared.Contains(OverlaySpec.Tex(OverlaySpec.CleanOnlyTex)) && !shared.Contains(OverlaySpec.Tex(OverlaySpec.CanaryTex))
                             && !shared.Contains(OverlaySpec.Tex(OverlaySpec.EditedTex)), "shared " + string.Join(",", build.SharedAssets), noGo);
            Ac("AC9", Ac9(build, out var why9), why9, failB);

            r.Reasons.AddRange(noGo);
            r.Reasons.AddRange(failB);
            r.Outcome = noGo.Count != 0 ? NoGo : failB.Count != 0 ? FailB : AllHold;
            return r;
        }

        /// <summary>前提 P1・P2 と build の成立。成り立たない回は未判定（B へ戻す）。</summary>
        public static List<string> Preconditions(BuildFacts build)
        {
            var reasons = new List<string>();
            if (!build.Finished) reasons.Add("build process did not finish");
            if (build.Problems.Count != 0) reasons.Add("build problems: " + string.Join("; ", build.Problems));
            if (!build.BaseBuilt || !build.BasePublished || !build.OverlayBuilt) reasons.Add("builds incomplete");
            foreach (var p in OverlaySpec.Points)
            {
                var b = build.BeforeBase.FirstOrDefault(x => x.Id == p.Id)?.Value;
                var o = build.BeforeOverlay.FirstOrDefault(x => x.Id == p.Id)?.Value;
                if (b != p.Expected(OverlaySpec.V1) || o != p.Expected(OverlaySpec.V2))
                    reasons.Add("P1 " + p.Id + ": before base " + b + ", before overlay " + o);
            }
            if (!build.ChangedSources.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(OverlaySpec.ChangedInV2))
                reasons.Add("P2 changed sources: " + string.Join(",", build.ChangedSources));
            return reasons;
        }

        private static bool Ac9(BuildFacts build, out string why)
        {
            var problems = new List<string>();
            if (!build.PreflightOverlay || !build.OutcomeOverlay) problems.Add("overlay mark");
            if (build.PreflightBase.Count != 5 || build.PreflightBase.Take(4).Any(string.IsNullOrEmpty)
                || build.PreflightBase[4] != OverlaySpec.DeclarationName) problems.Add("base identity " + string.Join("|", build.PreflightBase));
            if (!build.PreflightEdits.SequenceEqual(OverlaySpec.Edits.OrderBy(x => x, StringComparer.Ordinal))) problems.Add("edits");
            if (!build.PreflightRoots.Contains(OverlaySpec.SceneNested + "=" + OverlaySpec.Prefab(OverlaySpec.InnerPrefab))) problems.Add("SceneNested reason");
            if (!build.PreflightUnmatched.SequenceEqual(new[] { OverlaySpec.Tex(OverlaySpec.UnusedTex) + "=NotInAnyClosure" })) problems.Add("unmatched");
            if (build.PreflightShared.Count == 0 || !build.PreflightShared.SequenceEqual(build.SharedAssets)) problems.Add("shared");
            why = string.Join(", ", problems);
            return problems.Count == 0;
        }

        private static string Value(RoundFacts round, string id) => round.Points.FirstOrDefault(x => x.Id == id)?.Value ?? "missing";

        private static bool SameObservation(RoundFacts a, RoundFacts b, out string diff)
        {
            var d = new List<string>();
            foreach (var p in OverlaySpec.Points)
                if (Value(a, p.Id) != Value(b, p.Id)) d.Add(p.Id);
            if (a.SharedTexCount != b.SharedTexCount || a.CleanOnlyTexCount != b.CleanOnlyTexCount) d.Add("counts");
            if (!a.BaseKeys.SequenceEqual(b.BaseKeys) || !a.OverlayKeys.SequenceEqual(b.OverlayKeys)) d.Add("entries");
            diff = string.Join(",", d);
            return d.Count == 0;
        }
    }
}
