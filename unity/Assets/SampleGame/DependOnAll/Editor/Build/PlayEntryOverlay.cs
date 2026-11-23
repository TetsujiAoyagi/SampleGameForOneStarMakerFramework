#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OneStarMaker.Editor.ContentOverlay;

namespace SampleGame.DependOnAll.Editor.Build
{
    /// <summary>上書きの段の結果の状態。record と Console に名前で出す。</summary>
    internal enum PlayEntryOverlayState
    {
        /// <summary>上書きに入れる root も作り直せない root も無い。build せず、上書きを登録しない。</summary>
        NotRequired,
        /// <summary>同じ鍵の前回の上書きを登録する（build 0 回）。</summary>
        Reused,
        /// <summary>上書きを build して登録する。</summary>
        Built,
        /// <summary>作り直せない root だけがある。build せず、上書きを登録しないが、作り直せない root を示す。</summary>
        UnbuildableOnly,
    }

    /// <summary>計画の状態の写し（Framework の計画の型を SampleGame の段に持ち込まないため）。</summary>
    internal enum PlayEntryOverlayPlanState { NotRequired, BuildRequired, UnbuildableOnly }

    /// <summary>作り直せない root の、Play で何が起きるかの違う 3 つの組。</summary>
    internal enum PlayEntryUnbuildableGroup
    {
        /// <summary>base の entry から base の版で載る（理由 SourceAbsent・NotInLocalSelection）。</summary>
        LoadsFromBase,
        /// <summary>base にも上書きにも無く、要求すると解決の失敗になる（理由 NotInBase）。</summary>
        FailsOnRequest,
        /// <summary>base に閉包の記録が無いので、上の 2 つのどちらか見分けられない（理由 RecordMissing・RecordUnsupported・RecordInvalid）。</summary>
        Undetermined,
    }

    /// <summary>作り直せない root 1 つの要約。</summary>
    internal sealed class PlayEntryUnbuildableRoot
    {
        internal PlayEntryUnbuildableRoot(string logicalKey, string kind, string representation, string reason)
        { LogicalKey = logicalKey; Kind = kind; Representation = representation; Reason = reason; Group = PlayEntryOverlay.GroupOf(reason); }

        internal string LogicalKey { get; }
        internal string Kind { get; }
        internal string Representation { get; }
        /// <summary>計画の理由の名前（<c>ContentOverlayUnbuildableReason</c> の値の名前）。</summary>
        internal string Reason { get; }
        internal PlayEntryUnbuildableGroup Group { get; }
        internal string Label => LogicalKey + " (" + Kind + ", " + Representation + ", " + Reason + ")";
    }

    /// <summary>計画の port が返す要約。分類と理由の正本は計画で、ここはその写しだけを持つ。</summary>
    internal sealed class PlayEntryOverlayPlanSummary
    {
        internal PlayEntryOverlayPlanSummary(PlayEntryOverlayPlanState state, int overlayRootCount, IReadOnlyList<PlayEntryUnbuildableRoot> unbuildable)
        { State = state; OverlayRootCount = overlayRootCount; Unbuildable = unbuildable; }

        internal PlayEntryOverlayPlanState State { get; }
        internal int OverlayRootCount { get; }
        /// <summary>今回の計画の作り直せない root（前回の build の preflight ではない）。</summary>
        internal IReadOnlyList<PlayEntryUnbuildableRoot> Unbuildable { get; }
    }

    /// <summary>再利用か build の port が返す成果物。失敗は <see cref="IsSuccess"/> が偽で、理由は issue の code と対象を含む。</summary>
    internal sealed class PlayEntryOverlayArtifact
    {
        internal PlayEntryOverlayArtifact(bool isSuccess, string identity, string contentPath, string failure, string? reuseRecordFailure)
        { IsSuccess = isSuccess; Identity = identity; ContentPath = contentPath; Failure = failure; ReuseRecordFailure = reuseRecordFailure; }

        internal bool IsSuccess { get; }
        internal string Identity { get; }
        internal string ContentPath { get; }
        internal string Failure { get; }
        /// <summary>build は成功したが再利用の記録を書けなかった理由。無ければ null。</summary>
        internal string? ReuseRecordFailure { get; }
    }

    /// <summary>上書きの段の port。実物は host（<see cref="SampleGameContentBuild"/>）が作り、test は fake にする。</summary>
    internal sealed class PlayEntryOverlayPorts
    {
        /// <summary>選択の準備（欠けを許す materialize・宣言の選択・合成）。失敗は例外。</summary>
        internal Action Prepare { get; set; } = () => throw new InvalidOperationException("Prepare is not set.");
        /// <summary>計画（編集の検出・分類・鍵）。準備の後に 1 回呼ぶ。失敗は例外。</summary>
        internal Func<PlayEntryOverlayPlanSummary> Plan { get; set; } = () => throw new InvalidOperationException("Plan is not set.");
        /// <summary>今回の作り直せない root の集合を認め、同じ鍵の前回の上書きを返す。無ければ null。</summary>
        internal Func<PlayEntryOverlayArtifact?> AcknowledgeAndReuse { get; set; } = () => throw new InvalidOperationException("AcknowledgeAndReuse is not set.");
        /// <summary>今回の作り直せない root の集合を認め、上書きを build する。</summary>
        internal Func<PlayEntryOverlayArtifact> AcknowledgeAndBuild { get; set; } = () => throw new InvalidOperationException("AcknowledgeAndBuild is not set.");
        /// <summary>秒の時計（所要を測るだけ）。</summary>
        internal Func<double> Now { get; set; } = () => 0;
    }

    /// <summary>上書きの段の結果。所要は秒で、行わなかった段は null（0 と区別する）。</summary>
    internal sealed class PlayEntryOverlayResult
    {
        internal PlayEntryOverlayResult(PlayEntryOverlayState state, int overlayRootCount, IReadOnlyList<PlayEntryUnbuildableRoot> unbuildable,
            double prepareSeconds, double planSeconds, double? reuseCheckSeconds, double? buildSeconds,
            PlayEntryOverlayArtifact? artifact)
        {
            State = state; OverlayRootCount = overlayRootCount; Unbuildable = unbuildable;
            PrepareSeconds = prepareSeconds; PlanSeconds = planSeconds; ReuseCheckSeconds = reuseCheckSeconds; BuildSeconds = buildSeconds;
            Artifact = artifact;
        }

        internal PlayEntryOverlayState State { get; }
        internal int OverlayRootCount { get; }
        internal IReadOnlyList<PlayEntryUnbuildableRoot> Unbuildable { get; }
        internal double PrepareSeconds { get; }
        internal double PlanSeconds { get; }
        internal double? ReuseCheckSeconds { get; }
        internal double? BuildSeconds { get; }
        /// <summary>登録する上書き（<see cref="PlayEntryOverlayState.Reused"/> と <see cref="PlayEntryOverlayState.Built"/> の時だけ）。</summary>
        internal PlayEntryOverlayArtifact? Artifact { get; }
        internal bool RegistersOverlay => Artifact != null;
        internal string? ReuseRecordFailure => Artifact?.ReuseRecordFailure;

        /// <summary>上書きを登録する Play が、Built + open scenes の値に足して置く環境変数。登録しない Play では空。</summary>
        internal IReadOnlyList<KeyValuePair<string, string>> Environment => Artifact == null
            ? Array.Empty<KeyValuePair<string, string>>()
            : new[]
            {
                new KeyValuePair<string, string>(PurposeLaunchSettings.EnvironmentName(ContentOverlayPlayConfig.OverlayPathKey), Artifact.ContentPath),
                new KeyValuePair<string, string>(PurposeLaunchSettings.EnvironmentName(ContentOverlayPlayConfig.OverlayIdentityKey), Artifact.Identity),
            };
    }

    /// <summary>上書きの段の失敗（build の失敗）。入口は Play に入らず Blocked にする。</summary>
    internal sealed class PlayEntryOverlayException : Exception
    {
        internal PlayEntryOverlayException(string message) : base(message) { }
    }

    /// <summary>
    /// 入口の上書きの段の規則。計画の状態から、build しない・前回を使う・build するを決めて port を呼び、結果の要約を返す。状態を持たない。
    /// <para>
    /// 検査（準備・計画・再利用の確かめ）は省かない。計画の例外、再利用の確かめの例外、build の失敗は、どれも確かめていない結果
    /// （NotRequired・Reused）にせず例外で返す。入口はそれを Blocked にし、他の経路の値で Play に入らない。
    /// 作り直せない root がある時は止めず、今回の計画の集合をそのまま認める（owner の裁定。示すのは呼び手）。閾値は持たない。
    /// </para>
    /// </summary>
    internal static class PlayEntryOverlay
    {
        /// <summary>Console の 1 行に並べる root の上限。超えた分は件数だけを出し、詳細窓は全件を出す。</summary>
        internal const int ConsoleRootLimit = 8;

        internal static PlayEntryOverlayResult Run(PlayEntryOverlayPorts ports)
        {
            if (ports == null) throw new ArgumentNullException(nameof(ports));
            var start = ports.Now();
            ports.Prepare();
            var prepared = ports.Now();
            var plan = ports.Plan();
            var planned = ports.Now();
            if (plan == null) throw new InvalidOperationException("The overlay plan port returned nothing.");

            switch (plan.State)
            {
                case PlayEntryOverlayPlanState.NotRequired:
                    return new PlayEntryOverlayResult(PlayEntryOverlayState.NotRequired, plan.OverlayRootCount, plan.Unbuildable,
                        prepared - start, planned - prepared, null, null, null);
                case PlayEntryOverlayPlanState.UnbuildableOnly:
                    return new PlayEntryOverlayResult(PlayEntryOverlayState.UnbuildableOnly, plan.OverlayRootCount, plan.Unbuildable,
                        prepared - start, planned - prepared, null, null, null);
            }

            var reused = ports.AcknowledgeAndReuse();
            var checkedAt = ports.Now();
            if (reused != null && reused.IsSuccess)
                return new PlayEntryOverlayResult(PlayEntryOverlayState.Reused, plan.OverlayRootCount, plan.Unbuildable,
                    prepared - start, planned - prepared, checkedAt - planned, null, reused);

            var built = ports.AcknowledgeAndBuild();
            var builtAt = ports.Now();
            if (built == null || !built.IsSuccess)
                throw new PlayEntryOverlayException("The overlay build failed: " + (built?.Failure ?? "no result"));
            return new PlayEntryOverlayResult(PlayEntryOverlayState.Built, plan.OverlayRootCount, plan.Unbuildable,
                prepared - start, planned - prepared, checkedAt - planned, builtAt - checkedAt, built);
        }

        internal static PlayEntryUnbuildableGroup GroupOf(string reason) => reason switch
        {
            "SourceAbsent" or "NotInLocalSelection" => PlayEntryUnbuildableGroup.LoadsFromBase,
            "NotInBase" => PlayEntryUnbuildableGroup.FailsOnRequest,
            _ => PlayEntryUnbuildableGroup.Undetermined,
        };

        /// <summary>
        /// 他の供給元が届ける Scene の logical key。複製にして載せる Scene（宣言の identity）のうち SceneResourceMap にあるものだけ。
        /// map に無い Scene は計画の root ではないので渡さない。
        /// </summary>
        internal static IReadOnlyList<string> SuppliedSceneKeys(IEnumerable<string> snapshotIdentities, ISet<string> sceneResourceMapNames) =>
            snapshotIdentities.Where(sceneResourceMapNames.Contains).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();

        /// <summary>
        /// Play のたびに Console に出す Warning。作り直せない root は組ごとに 1 行（黙らせる設定は無い）、記録の失敗は 1 行。
        /// root の一覧は <see cref="ConsoleRootLimit"/> で切り、残りの件数を出す。
        /// </summary>
        internal static IReadOnlyList<string> Warnings(PlayEntryOverlayResult result)
        {
            var lines = new List<string>();
            foreach (var group in new[] { PlayEntryUnbuildableGroup.LoadsFromBase, PlayEntryUnbuildableGroup.FailsOnRequest, PlayEntryUnbuildableGroup.Undetermined })
            {
                var roots = result.Unbuildable.Where(x => x.Group == group).ToArray();
                if (roots.Length == 0) continue;
                var shown = roots.Take(ConsoleRootLimit).Select(x => x.Label);
                var rest = roots.Length > ConsoleRootLimit ? " (+" + (roots.Length - ConsoleRootLimit) + " more; see the Play entry window)" : "";
                lines.Add(GroupMessage(group) + " (" + roots.Length + "): " + string.Join(", ", shown) + rest);
            }
            if (result.ReuseRecordFailure != null)
                lines.Add("The overlay was built, but it will be built again next time: " + result.ReuseRecordFailure);
            return lines;
        }

        internal static string GroupMessage(PlayEntryUnbuildableGroup group) => group switch
        {
            PlayEntryUnbuildableGroup.LoadsFromBase =>
                "Your edits affect these roots, but they cannot be rebuilt from your checkout; they load the base version",
            PlayEntryUnbuildableGroup.FailsOnRequest =>
                "These roots are in neither the base nor the overlay; requesting them fails",
            _ => "The base has no closure record, so these roots may load the base version or fail when requested",
        };

        /// <summary>record の所要の欄の値。行わなかった段は <c>not-run</c>（0 秒と区別する）。</summary>
        internal static string FormatSeconds(double? seconds) =>
            seconds == null ? NotRun : seconds.Value.ToString("0.000", CultureInfo.InvariantCulture);

        internal const string NotRun = "not-run";
    }
}
