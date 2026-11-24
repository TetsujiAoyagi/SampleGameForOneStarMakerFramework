#nullable enable

using System;
using OneStarMaker.Runtime.BuildContent;

namespace OneStarMaker.Runtime
{
    /// <summary>
    /// 起動の UICommon を取る規則。<see cref="BootstrapSourcePolicy.Plan"/> が起動の構成から 1 つ選び、
    /// <see cref="BootstrapSourcePolicy.DecideUiCommon"/> が active な Scene の状態と合わせて読む先を決める。
    /// </summary>
    internal enum BootstrapUiCommonRule
    {
        /// <summary>Editor（directory と Addressables 互換）。active な Scene の UICommon があれば借用し、無ければ directory か Addressables から読む。</summary>
        BorrowOrLoad,
        /// <summary>写しの順の Player（動作確認の Player）。active な Scene の UICommon を借用するだけで、無ければ起動の失敗。</summary>
        BorrowOnly,
        /// <summary>content が先の順の Player（遊べる Player）。登録した content からだけ読む。active な Scene に UICommon があれば起動の失敗。</summary>
        ContentOnly,
    }

    /// <summary>起動の SceneResourceMap を読む口。</summary>
    internal enum BootstrapMapSource
    {
        /// <summary>Player に写した graph metadata（派生の <c>LoadPlayerSceneResourceMap</c>）。写しの順の Player だけ。</summary>
        PlayerResources,
        /// <summary>登録した content の logical key。</summary>
        Content,
        /// <summary>Addressables の address。</summary>
        Addressables,
    }

    /// <summary>UICommon を実際に読んだ先。起動の log と loader の分岐に使う。</summary>
    internal enum BootstrapUiCommonOrigin
    {
        /// <summary>読まない（起動の失敗）。<see cref="BootstrapUiCommonDecision.Failure"/> に理由がある。</summary>
        None,
        ActiveScene,
        Content,
        Addressables,
    }

    /// <summary><see cref="BootstrapSourcePolicy.Plan"/> の結果。値だけを持つ。</summary>
    internal readonly struct BootstrapSourcePlan
    {
        internal BootstrapSourcePlan(bool registerContentFirst, BootstrapUiCommonRule uiCommonRule, BootstrapMapSource mapSource)
        {
            RegisterContentFirst = registerContentFirst;
            UiCommonRule = uiCommonRule;
            MapSource = mapSource;
        }

        /// <summary>真なら、UICommon より前（AfterSceneLoad の最初）に content を登録する。</summary>
        internal bool RegisterContentFirst { get; }
        internal BootstrapUiCommonRule UiCommonRule { get; }
        internal BootstrapMapSource MapSource { get; }
    }

    /// <summary><see cref="BootstrapSourcePolicy.DecideUiCommon"/> の結果。</summary>
    internal readonly struct BootstrapUiCommonDecision
    {
        private BootstrapUiCommonDecision(BootstrapUiCommonOrigin origin, string? failure)
        {
            Origin = origin;
            Failure = failure;
        }

        internal static BootstrapUiCommonDecision From(BootstrapUiCommonOrigin origin) => new(origin, null);
        internal static BootstrapUiCommonDecision Fail(string reason) => new(BootstrapUiCommonOrigin.None, reason);

        internal BootstrapUiCommonOrigin Origin { get; }
        /// <summary>起動を失敗にする理由。null なら <see cref="Origin"/> から読む。</summary>
        internal string? Failure { get; }
    }

    /// <summary>
    /// 起動の時に、content を登録する位置、UICommon の規則、SceneResourceMap の読み口を、起動の構成から 1 回で決める。状態を持たず、Unity を呼ばない。
    /// <para>
    /// 遊べる Player は Editor の directory Play と同じ「登録 → UICommon → map（どちらも content から）」の順で、
    /// 動作確認の Player は receipt の段の順の契約を保つために今の写しの順（UICommon を開始 Scene から借用、map を Player の写しから読み、
    /// サービスの開始の後に登録）のままにする。Player の構成でない起動（Editor）は種類を見ない。
    /// </para>
    /// <para>
    /// 種類は検証済みの baked の値を渡す（重ねた後の値ではない）。開始 Scene と baked の種類が食い違う exe は、
    /// <see cref="DecideUiCommon"/> がどちらの向きでも起動の失敗にする（開始 Scene の名前が Player の構成の marker で、
    /// 種類は baked にしか無いので、その組を照らすのはここだけである）。
    /// </para>
    /// </summary>
    internal static class BootstrapSourcePolicy
    {
        internal const string PlayableRequiresDirectoryMessage = "A playable Player requires a content directory.";
        internal const string PlayableMustNotBorrowMessage = "A playable Player must not start from a scene that contains UICommon.";
        internal const string BorrowOnlyMissingMessage = "The Player bootstrap scene must contain UICommon; Addressables fallback is forbidden.";

        /// <param name="isPlayerConfiguration">派生の <c>UseRequiredPlayerFileConfiguration</c>。</param>
        /// <param name="kind">検証済みの baked の種類。Player の構成でない時は見ない。</param>
        /// <param name="useContentDirectory">content directory の設定がある。</param>
        /// <exception cref="InvalidOperationException">directory の無い遊べる Player。</exception>
        internal static BootstrapSourcePlan Plan(bool isPlayerConfiguration, PlayerContentKind kind, bool useContentDirectory)
        {
            if (!isPlayerConfiguration)
            {
                return useContentDirectory
                    ? new BootstrapSourcePlan(true, BootstrapUiCommonRule.BorrowOrLoad, BootstrapMapSource.Content)
                    : new BootstrapSourcePlan(false, BootstrapUiCommonRule.BorrowOrLoad, BootstrapMapSource.Addressables);
            }

            if (kind == PlayerContentKind.Playable)
            {
                if (!useContentDirectory)
                    throw new InvalidOperationException(PlayableRequiresDirectoryMessage);
                return new BootstrapSourcePlan(true, BootstrapUiCommonRule.ContentOnly, BootstrapMapSource.Content);
            }

            // 動作確認の Player は directory の有無によらず写しの順（登録はサービスの開始の後の今の位置）。
            return new BootstrapSourcePlan(false, BootstrapUiCommonRule.BorrowOnly, BootstrapMapSource.PlayerResources);
        }

        /// <param name="rule"><see cref="Plan"/> が返した規則。</param>
        /// <param name="activeSceneHasUiCommon">active な Scene の名前が <c>UICommon</c> で、root <c>UICommon</c> に component がある。</param>
        /// <param name="useContentDirectory">content directory の設定がある。</param>
        internal static BootstrapUiCommonDecision DecideUiCommon(BootstrapUiCommonRule rule, bool activeSceneHasUiCommon, bool useContentDirectory)
        {
            switch (rule)
            {
                case BootstrapUiCommonRule.BorrowOrLoad:
                    if (activeSceneHasUiCommon) return BootstrapUiCommonDecision.From(BootstrapUiCommonOrigin.ActiveScene);
                    return BootstrapUiCommonDecision.From(useContentDirectory
                        ? BootstrapUiCommonOrigin.Content
                        : BootstrapUiCommonOrigin.Addressables);
                case BootstrapUiCommonRule.BorrowOnly:
                    return activeSceneHasUiCommon
                        ? BootstrapUiCommonDecision.From(BootstrapUiCommonOrigin.ActiveScene)
                        : BootstrapUiCommonDecision.Fail(BorrowOnlyMissingMessage);
                case BootstrapUiCommonRule.ContentOnly:
                    // 借用より先に拒む。UICommon を持つ Scene から始まった遊べる Player は、content 以外の UICommon で動きうるので起動させない。
                    if (activeSceneHasUiCommon) return BootstrapUiCommonDecision.Fail(PlayableMustNotBorrowMessage);
                    return useContentDirectory
                        ? BootstrapUiCommonDecision.From(BootstrapUiCommonOrigin.Content)
                        : BootstrapUiCommonDecision.Fail(PlayableRequiresDirectoryMessage);
                default:
                    throw new ArgumentOutOfRangeException(nameof(rule), rule, "Unknown UICommon rule.");
            }
        }

        /// <summary>起動の log の <c>source=</c> の値。</summary>
        internal static string FormatOrigin(BootstrapUiCommonOrigin origin) => origin switch
        {
            BootstrapUiCommonOrigin.ActiveScene => "active-scene",
            BootstrapUiCommonOrigin.Content => "content",
            BootstrapUiCommonOrigin.Addressables => "addressables",
            _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, "UICommon was not loaded."),
        };

        /// <summary>起動の log の <c>source=</c> の値。</summary>
        internal static string FormatMapSource(BootstrapMapSource source) => source switch
        {
            BootstrapMapSource.PlayerResources => "player-resources",
            BootstrapMapSource.Content => "content",
            BootstrapMapSource.Addressables => "addressables",
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown SceneResourceMap source."),
        };
    }
}
