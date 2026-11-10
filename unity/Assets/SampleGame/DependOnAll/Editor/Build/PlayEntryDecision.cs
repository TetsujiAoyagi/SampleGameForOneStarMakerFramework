#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SampleGame.DependOnAll.Editor.Build
{
    /// <summary>
    /// 経路。<see cref="BuiltWithOpenScenes"/> は build 済み content の Play に、開いている Scene の Play 前の複製を載せる（混成）。
    /// <see cref="Built"/> は「directory だけ」のまま（Scene が開いていれば従来どおり拒否する）。保存値は名前なので末尾に足す。
    /// </summary>
    internal enum PlayEntryRoute { Source, Built, [InspectorName("Built + open scenes")] BuiltWithOpenScenes }

    internal enum PlayEntryAction { PassThrough, Intervene, Reject }

    /// <summary>判定表のどの行で決まったか。record に出す名前で、並びは判定の順序と同じ。</summary>
    internal enum PlayEntryRule
    {
        BatchMode, TestRunnerScene, SnapshotReentry, DirectoryWithReusableScenes, OnePlayLedger, ExplicitSetting, ConflictingState,
        SourceRoute, SnapshotUnsupportedSetting, NoVerifiedCandidate, BuiltWithReusableScenes, BuiltRoute,
        SnapshotNothingToCarry, SnapshotUnsupportedScenes, SnapshotRoute, EntryFailed,
    }

    [Flags]
    internal enum PlayEntryRecovery { None = 0, EmptyScene = 1, VerifyPlayBuild = 2, SwitchToSource = 4, SwitchToBuilt = 8 }

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
        /// <summary>loaded で開いている Scene の名前と path（開いている順）。新規未保存の Scene の path は空。</summary>
        internal IReadOnlyList<(string Name, string Path)> OpenScenes { get; set; } = Array.Empty<(string, string)>();
        /// <summary>Enter Play Mode の設定で Scene reload が無効（Unity が開いている Scene をそのまま Play に使う）。</summary>
        internal bool IsSceneReloadDisabled { get; set; }
        /// <summary><c>EditorSceneManager.playModeStartScene</c> が非 null。</summary>
        internal bool HasPlayModeStartScene { get; set; }
        /// <summary>
        /// 入口が準備した Play snapshot の session が今開いている Scene と一致し、入口の置く <c>editor:playSnapshotExpected</c> が環境にある。
        /// 再入の行（<see cref="PlayEntryRule.SnapshotReentry"/>）だけが見る。
        /// </summary>
        internal bool SnapshotMatchesOpenScenes { get; set; }
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

            // 混成の経路の再 Play は、入口が ledger を置いた後、元の Scene を開いたまま 2 回目の ExitingEditMode に入る。入口が準備した
            // session と 1 回の ledger が揃った再入だけを通し、明示の directory・手動の ledger・他の launcher の ledger は次の行で従来どおり拒否する。
            // 通すのは入口自身の EnterPlaymode の中だけで、それ以外で当たった時は PlayEntryTransaction が取り消す。
            if (mode == Directory && facts.ReusableScenes.Count != 0 && facts.HasOnePlayLedger && !facts.HasManualLedger
                && facts.SnapshotMatchesOpenScenes)
                return new PlayEntryPlan(PlayEntryAction.PassThrough, PlayEntryRule.SnapshotReentry,
                    "The play snapshot prepared by this entry matches the open scenes.", mode);

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

            if (facts.Route == PlayEntryRoute.BuiltWithOpenScenes)
            {
                var unsupported = SnapshotUnsupportedSetting(facts);
                if (unsupported != null)
                    return new PlayEntryPlan(PlayEntryAction.Reject, PlayEntryRule.SnapshotUnsupportedSetting, unsupported, Directory);
            }

            // 経路 Built と混成。候補が無くても Source へ黙って切り替えない（利用者の選んだ経路を入口が変えない）。
            if (facts.Candidate == null)
                return new PlayEntryPlan(PlayEntryAction.Reject, PlayEntryRule.NoVerifiedCandidate,
                    "No verified installed content for '" + facts.RunPurpose + "'." +
                    (facts.CandidateFailure.Length == 0 ? "" : " " + facts.CandidateFailure),
                    Directory, recovery: PlayEntryRecovery.VerifyPlayBuild | PlayEntryRecovery.SwitchToSource);

            if (facts.Route == PlayEntryRoute.BuiltWithOpenScenes)
                return DecideSnapshot(facts);

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

        /// <summary>
        /// 混成の経路で候補がある時の残りの行。開いている Scene を複製して載せ、初回 Scene を置かない directory の値で Play する
        /// （置くと開いている Scene と初回 Scene の両方が load される）。
        /// </summary>
        private static PlayEntryPlan DecideSnapshot(PlayEntryFacts facts)
        {
            var candidate = facts.Candidate!;
            var open = facts.OpenScenes;
            if (open.Count == 0 || open.Count == 1 && open[0].Path.Length == 0)
                return new PlayEntryPlan(PlayEntryAction.Reject, PlayEntryRule.SnapshotNothingToCarry,
                    "No saved scene is open, so there is nothing to carry into the built content.", Directory,
                    recovery: PlayEntryRecovery.SwitchToBuilt, revision: candidate.Revision);
            var problems = OpenSceneProblems(open);
            if (problems != null)
                return new PlayEntryPlan(PlayEntryAction.Reject, PlayEntryRule.SnapshotUnsupportedScenes, problems, Directory,
                    revision: candidate.Revision);

            var warnings = new List<string>(facts.CandidateWarnings);
            if (candidate.HasMultiple)
                warnings.Add("Several verified revisions exist for '" + facts.RunPurpose + "'; using the latest: " + candidate.Revision);
            return new PlayEntryPlan(PlayEntryAction.Intervene, PlayEntryRule.SnapshotRoute,
                "Built route with a verified installed revision and copies of the open scenes: " +
                string.Join(", ", open.Select(x => x.Name)) + ".", Directory,
                PurposeLaunchSettings.SnapshotPlay(candidate.PlacedEnvironment), warnings: warnings, revision: candidate.Revision);
        }

        /// <summary>混成の経路が扱えない Editor の設定。複製を作る前に分かるものだけ。</summary>
        private static string? SnapshotUnsupportedSetting(PlayEntryFacts facts)
        {
            if (facts.IsSceneReloadDisabled)
                return "Playing with open scenes needs Scene Reload. Enable it in Project Settings > Editor > Enter Play Mode Settings.";
            if (facts.HasPlayModeStartScene)
                return "A Play Mode start scene is already set, and this route does not overwrite it. Clear EditorSceneManager.playModeStartScene.";
            return null;
        }

        /// <summary>複製できない開いている Scene（新規未保存、UICommon、同名 2 つ）。無ければ null。</summary>
        internal static string? OpenSceneProblems(IReadOnlyList<(string Name, string Path)> openScenes)
        {
            var unsaved = openScenes.Where(x => x.Path.Length == 0).Select(x => x.Name.Length == 0 ? "(untitled)" : x.Name).ToArray();
            if (unsaved.Length != 0)
                return "Save these scenes before playing with open scenes: " + string.Join(", ", unsaved);
            var uiCommon = openScenes.Where(x => string.Equals(x.Name, "UICommon", StringComparison.Ordinal)
                                                 || string.Equals(x.Path, SampleGameBootstrapContentComposer.UiCommonPath, StringComparison.Ordinal)).ToArray();
            if (uiCommon.Length != 0)
                return "UICommon cannot be played from an open scene. Close it: " + string.Join(", ", uiCommon.Select(x => x.Path));
            var duplicate = openScenes.GroupBy(x => x.Name, StringComparer.Ordinal).FirstOrDefault(x => x.Count() > 1);
            if (duplicate != null)
                return "Two open scenes are named '" + duplicate.Key + "'. Close one of them: " + string.Join(", ", duplicate.Select(x => x.Path));
            return null;
        }

        /// <summary>
        /// 再入の行に、入口自身の EnterPlaymode の外で当たった（前の Play の session の残り）。別の Play の許可にしないため取り消す計画。
        /// </summary>
        internal static PlayEntryPlan StaleSnapshot(PlayEntryPlan reentry) =>
            new(PlayEntryAction.Reject, PlayEntryRule.SnapshotReentry,
                "A play snapshot from an earlier Play entry was still in place and has been released. Press Play again.", reentry.EffectiveMode);

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
