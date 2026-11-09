#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace SampleGame.DependOnAll.Editor.Build
{
    internal enum PlayEntryRoute { Source, Built }

    internal enum PlayEntryAction { PassThrough, Intervene, Reject }

    /// <summary>判定表のどの行で決まったか。record に出す名前で、並びは判定の順序と同じ。</summary>
    internal enum PlayEntryRule
    {
        BatchMode, TestRunnerScene, DirectoryWithReusableScenes, OnePlayLedger, ExplicitSetting, ConflictingState,
        SourceRoute, NoVerifiedCandidate, BuiltWithReusableScenes, BuiltRoute, EntryFailed,
    }

    [Flags]
    internal enum PlayEntryRecovery { None = 0, EmptyScene = 1, VerifyPlayBuild = 2, SwitchToSource = 4 }

    /// <summary>JSON・環境変数・コマンドラインの 1 層のうち、mode を決める 3 key だけの値（無い key は入らない）。</summary>
    internal sealed class PlayEntryConfigLayer
    {
        internal static readonly PlayEntryConfigLayer Empty = new(new Dictionary<string, string>());
        private readonly IReadOnlyDictionary<string, string> _values;

        internal PlayEntryConfigLayer(IReadOnlyDictionary<string, string> values) => _values = values;

        internal bool HasAny => _values.Count != 0;

        internal bool TryGet(string key, out string value)
        {
            if (_values.TryGetValue(key, out var found)) { value = found; return true; }
            value = string.Empty;
            return false;
        }

        /// <summary>
        /// Runtime の provider が作った 1 層の store から 3 key を抜き出す。環境変数の provider は名前の大文字のまま store へ入れるので、
        /// 大文字小文字を区別せずに探す（Runtime の AppConfig も区別しない）。空文字の値も「ある」として残す。
        /// </summary>
        internal static PlayEntryConfigLayer From(IEnumerable<KeyValuePair<string, string>> store)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in store)
                if (PlayEntryDecision.ModeKeys.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
                    values[pair.Key] = pair.Value;
            return new PlayEntryConfigLayer(values);
        }
    }

    /// <summary>検証を通った installed revision と、それで directory Play する時に置く環境変数。</summary>
    internal sealed class PlayEntryCandidate
    {
        internal PlayEntryCandidate(string revision, IReadOnlyList<KeyValuePair<string, string>> placedEnvironment, bool hasMultiple)
        { Revision = revision; PlacedEnvironment = placedEnvironment; HasMultiple = hasMultiple; }

        internal string Revision { get; }
        internal IReadOnlyList<KeyValuePair<string, string>> PlacedEnvironment { get; }
        /// <summary>宣言に一致する installed revision が他にもある（最新を選んだことを Warning で知らせる）。</summary>
        internal bool HasMultiple { get; }
    }

    /// <summary>判定の入力。値だけの snapshot で、Unity Editor の状態はここへ写してから渡す。</summary>
    internal sealed class PlayEntryFacts
    {
        internal bool IsBatchMode { get; set; }
        internal bool IsTestRunnerScene { get; set; }
        internal bool HasOnePlayLedger { get; set; }
        internal bool HasManualLedger { get; set; }
        internal PlayEntryConfigLayer JsonLayer { get; set; } = PlayEntryConfigLayer.Empty;
        internal PlayEntryConfigLayer EnvironmentLayer { get; set; } = PlayEntryConfigLayer.Empty;
        internal PlayEntryConfigLayer CommandLineLayer { get; set; } = PlayEntryConfigLayer.Empty;
        /// <summary>入口がこれから置く値と外の状態の衝突の理由。Variant profile もここへ入る。無ければ null。</summary>
        internal string? ConflictReason { get; set; }
        internal IReadOnlyList<string> ReusableScenes { get; set; } = Array.Empty<string>();
        /// <summary>結果に効く事実として record に出す。拒否の判定は <see cref="ConflictReason"/> が持つ。</summary>
        internal bool HasVariantProfile { get; set; }
        internal bool IsFastMode { get; set; }
        internal string RunPurpose { get; set; } = string.Empty;
        internal PlayEntryRoute Route { get; set; }
        internal IReadOnlyList<KeyValuePair<string, string>> SourceEnvironment { get; set; } = Array.Empty<KeyValuePair<string, string>>();
        internal PlayEntryCandidate? Candidate { get; set; }
        /// <summary>候補が無い理由（宣言違い・検証失敗など）。理由が無いだけの「0 件」は空。</summary>
        internal string CandidateFailure { get; set; } = string.Empty;
        /// <summary>選択に関わる注意（検証に失敗した新しい revision を飛ばした等）。Play は止めない。</summary>
        internal IReadOnlyList<string> CandidateWarnings { get; set; } = Array.Empty<string>();
    }

    /// <summary>判定の出力。<see cref="Environment"/> は介入の時だけ値を持つ。</summary>
    internal sealed class PlayEntryPlan
    {
        internal PlayEntryPlan(PlayEntryAction action, PlayEntryRule rule, string reason, string effectiveMode,
            IReadOnlyList<KeyValuePair<string, string>>? environment = null, bool alignFastMode = false,
            PlayEntryRecovery recovery = PlayEntryRecovery.None, IReadOnlyList<string>? warnings = null, string revision = "")
        {
            Action = action; Rule = rule; Reason = reason; EffectiveMode = effectiveMode;
            Environment = environment ?? Array.Empty<KeyValuePair<string, string>>();
            AlignFastMode = alignFastMode; Recovery = recovery; Warnings = warnings ?? Array.Empty<string>(); Revision = revision;
        }

        internal PlayEntryAction Action { get; }
        internal PlayEntryRule Rule { get; }
        internal string Reason { get; }
        /// <summary>この Play の実効 mode（addressables / directory / 値そのまま / 空）。</summary>
        internal string EffectiveMode { get; }
        internal IReadOnlyList<KeyValuePair<string, string>> Environment { get; }
        internal bool AlignFastMode { get; }
        internal PlayEntryRecovery Recovery { get; }
        internal IReadOnlyList<string> Warnings { get; }
        internal string Revision { get; }
    }

    /// <summary>
    /// Play ボタンの Play を素通りさせるか、介入するか、止めるかの判定表。上から最初に当たった行で決まる。
    /// <para>
    /// directory と再利用しうる Scene の検査（行 2）を一回の ledger の素通り（行 3）より先に見るのは、検査の保証を各経路の実装に頼らないため。
    /// 明示設定（行 4）に競合の検査（行 5）を掛けないのは、行 5 が「入口がこれから置く値との衝突」を見るもので、行 4 は何も置かないから。
    /// 拒否すると利用者の明示操作で Play できなくなる。
    /// </para>
    /// </summary>
    internal static class PlayEntryDecision
    {
        internal const string RuntimeModeKey = PurposeLaunchSettings.RuntimeModeKey;
        internal const string InstalledRevisionPathKey = "content:installedRevisionPath";
        internal const string ManifestSha256Key = "content:manifestSha256";
        internal const string Addressables = "addressables";
        internal const string Directory = "directory";
        internal const string TestRunnerScenePrefix = "InitTestScene";
        internal static readonly IReadOnlyList<string> ModeKeys = new[] { RuntimeModeKey, InstalledRevisionPathKey, ManifestSha256Key };

        internal const string ExistingBuildWarning =
            "The Addressables Play Mode Script is not 'Use Asset Database', so this Play may read an existing build.";

        /// <summary>PlayMode テストは必ず <c>Assets/InitTestScene&lt;guid&gt;.unity</c> で始まる。同じ接頭辞の利用者の Scene は素通りし、record に理由が出る。</summary>
        internal static bool IsTestRunnerScene(IEnumerable<string> openSceneNames) =>
            openSceneNames.Any(name => name.StartsWith(TestRunnerScenePrefix, StringComparison.Ordinal));

        internal static PlayEntryPlan Decide(PlayEntryFacts facts)
        {
            if (facts == null) throw new ArgumentNullException(nameof(facts));
            var mode = EffectiveMode(facts);

            if (facts.IsBatchMode)
                return new PlayEntryPlan(PlayEntryAction.PassThrough, PlayEntryRule.BatchMode, "Batch mode.", mode);
            if (facts.IsTestRunnerScene)
                return new PlayEntryPlan(PlayEntryAction.PassThrough, PlayEntryRule.TestRunnerScene,
                    "An open scene starts with '" + TestRunnerScenePrefix + "', so this Play is treated as a Test Runner Play.", mode);

            if (mode == Directory && facts.ReusableScenes.Count != 0)
                return new PlayEntryPlan(PlayEntryAction.Reject, PlayEntryRule.DirectoryWithReusableScenes,
                    OpenScenesMessage(facts), mode, recovery: PlayEntryRecovery.EmptyScene);

            if (facts.HasOnePlayLedger)
                return new PlayEntryPlan(PlayEntryAction.PassThrough, PlayEntryRule.OnePlayLedger,
                    "Settings for one Play are already in place.", mode);

            if (HasExplicitSetting(facts))
            {
                var warnings = mode == Addressables && !facts.IsFastMode ? new[] { ExistingBuildWarning } : null;
                return new PlayEntryPlan(PlayEntryAction.PassThrough, PlayEntryRule.ExplicitSetting,
                    "An explicit content setting is present, so nothing is written.", mode, warnings: warnings);
            }

            if (facts.ConflictReason != null)
                return new PlayEntryPlan(PlayEntryAction.Reject, PlayEntryRule.ConflictingState, facts.ConflictReason, mode);

            if (facts.Route == PlayEntryRoute.Source)
                return new PlayEntryPlan(PlayEntryAction.Intervene, PlayEntryRule.SourceRoute,
                    "No explicit setting; source Play with the selected purpose.", Addressables,
                    facts.SourceEnvironment, alignFastMode: !facts.IsFastMode);

            // 経路 Built。候補が無くても Source へ黙って切り替えない（利用者の選んだ経路を入口が変えない）。
            if (facts.Candidate == null)
                return new PlayEntryPlan(PlayEntryAction.Reject, PlayEntryRule.NoVerifiedCandidate,
                    "No verified installed content for '" + facts.RunPurpose + "'." +
                    (facts.CandidateFailure.Length == 0 ? "" : " " + facts.CandidateFailure),
                    Directory, recovery: PlayEntryRecovery.VerifyPlayBuild | PlayEntryRecovery.SwitchToSource);

            if (facts.ReusableScenes.Count != 0)
                return new PlayEntryPlan(PlayEntryAction.Reject, PlayEntryRule.BuiltWithReusableScenes,
                    OpenScenesMessage(facts), Directory, recovery: PlayEntryRecovery.EmptyScene, revision: facts.Candidate.Revision);

            var builtWarnings = new List<string>(facts.CandidateWarnings);
            if (facts.Candidate.HasMultiple)
                builtWarnings.Add("Several verified revisions exist for '" + facts.RunPurpose + "'; using the latest: " + facts.Candidate.Revision);
            return new PlayEntryPlan(PlayEntryAction.Intervene, PlayEntryRule.BuiltRoute,
                "Built route with a verified installed revision.", Directory,
                facts.Candidate.PlacedEnvironment, warnings: builtWarnings, revision: facts.Candidate.Revision);
        }

        /// <summary>facts の採取や適用に失敗した時の計画。何が起きるか分からない Play を始めない。</summary>
        internal static PlayEntryPlan EntryFailed(string message) =>
            new(PlayEntryAction.Reject, PlayEntryRule.EntryFailed, message, string.Empty);

        /// <summary>
        /// 3 層を Runtime の優先順位（コマンドライン → 環境変数 → JSON）で重ねた実効 mode。mode が無く pair が揃っていれば
        /// Runtime と同じく暗黙の directory。pair が片方だけなら Runtime が起動で拒否するので directory とは数えない。
        /// </summary>
        internal static string EffectiveMode(PlayEntryFacts facts)
        {
            if (TryEffective(facts, RuntimeModeKey, out var mode)) return mode;
            TryEffective(facts, InstalledRevisionPathKey, out var path);
            TryEffective(facts, ManifestSha256Key, out var digest);
            return path.Length != 0 && digest.Length != 0 ? Directory : string.Empty;
        }

        // 空文字の値も「ある」（Runtime の ContainsKey が真）。手動の ledger は値が環境変数の層に見えるが、値の有無に依らず利用者の明示操作として扱う。
        private static bool HasExplicitSetting(PlayEntryFacts facts) =>
            facts.HasManualLedger || facts.JsonLayer.HasAny || facts.EnvironmentLayer.HasAny || facts.CommandLineLayer.HasAny;

        private static bool TryEffective(PlayEntryFacts facts, string key, out string value) =>
            facts.CommandLineLayer.TryGet(key, out value)
            || facts.EnvironmentLayer.TryGet(key, out value)
            || facts.JsonLayer.TryGet(key, out value);

        private static string OpenScenesMessage(PlayEntryFacts facts) =>
            "Open scenes that startup would reuse instead of the content directory: " + string.Join(", ", facts.ReusableScenes);
    }
}
