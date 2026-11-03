#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneStarMaker.Build.Selection;
using OneStarMaker.Foundation.Config;
using OneStarMaker.Runtime.AssetManagement;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEngine;

namespace SampleGame.DependOnAll.Editor.Build
{
    /// <summary>
    /// 宣言を選んで source（Addressables の Use Asset Database）の Play に入る Play As。mode・表現・runtime 既定値を
    /// 「1 回の Play で戻す」ledger で置き、宣言と食い違う状態（Variant profile、外の値、コマンドライン、app-config）では
    /// 置く前に止まる。保証するのはこの経路が制御する key の実効値まで。
    /// <para>
    /// Unity の menu は static なので、宣言を <see cref="SampleGameBuildPurposes"/> に足す担当がここと
    /// <see cref="VerifyPlayLauncher"/> の menu も足す。menu の path と本体は同じ宣言名の定数を使う（足し忘れはテストが見つける）。
    /// </para>
    /// </summary>
    internal static class PlayAsLauncher
    {
        internal const string MenuRoot = "Tools/OSM/Play As/";
        private const string AllFull = "all-full";
        private const string Bs4SpringFull = "bs4-spring-full";
        private const string SpringFull = "spring-full";
        private const string SpringFullWhitebox = "spring-full-whitebox";
        private const string SpringPlannerWhitebox = "spring-planner-whitebox";
        private const string SpringWhitebox = "spring-whitebox";
        private const string LogPrefix = "[PlayAs] ";

        [MenuItem(MenuRoot + AllFull)] private static void MenuAllFull() => LaunchFromMenu(AllFull);
        [MenuItem(MenuRoot + Bs4SpringFull)] private static void MenuBs4SpringFull() => LaunchFromMenu(Bs4SpringFull);
        [MenuItem(MenuRoot + SpringFull)] private static void MenuSpringFull() => LaunchFromMenu(SpringFull);
        [MenuItem(MenuRoot + SpringFullWhitebox)] private static void MenuSpringFullWhitebox() => LaunchFromMenu(SpringFullWhitebox);
        [MenuItem(MenuRoot + SpringPlannerWhitebox)] private static void MenuSpringPlannerWhitebox() => LaunchFromMenu(SpringPlannerWhitebox);
        [MenuItem(MenuRoot + SpringWhitebox)] private static void MenuSpringWhitebox() => LaunchFromMenu(SpringWhitebox);

        private static void LaunchFromMenu(string name)
        {
            var reason = Launch(SampleGameBuildPurposes.Find(name), interactive: true);
            if (reason != null) EditorUtility.DisplayDialog("Play As " + name, reason, "OK");
        }

        /// <summary>
        /// Play As の入口。拒否なら理由を返し、process 環境を変えない。<paramref name="interactive"/> が偽（eval・テスト）の時は
        /// 確認の dialog を出さず、Delivery 窓の ledger も拒否で止まる。
        /// </summary>
        internal static string? Launch(BuildPurposeDeclaration declaration, bool interactive)
        {
            var reason = Check(declaration, interactive, out var settings);
            if (reason != null)
            {
                Debug.LogError(LogPrefix + declaration.Name + " rejected: " + reason);
                return reason;
            }
            ContentDeliveryPlayBridge.ApplyForOnePlay(settings!.PlacedEnvironment);
            try
            {
                Debug.Log(LogPrefix + declaration.Name + " " +
                          string.Join(" ", settings.Placed.Select(x => x.Key + "=" + x.Value)) +
                          (settings.RequiresEmptyLowerLayer.Count == 0 ? "" : " (empty from app-config: " + string.Join(",", settings.RequiresEmptyLowerLayer) + ")"));
                EditorApplication.EnterPlaymode();
            }
            catch
            {
                ContentDeliveryPlayBridge.Reset();
                throw;
            }
            return null;
        }

        private static string? Check(BuildPurposeDeclaration declaration, bool interactive, out PurposeLaunchSettings.LaunchSettings? settings)
        {
            settings = null;
            var reason = CheckEditorState();
            if (reason != null) return reason;
            try { settings = PurposeLaunchSettings.PlayAs(declaration); }
            catch (ArgumentException ex) { return ex.Message; }
            reason = OfferDeliveryReset(interactive) ?? CheckLaunchConflicts(settings.Controlled, Environment.GetCommandLineArgs());
            if (reason != null) return reason;
            reason = CheckEmptyLowerLayer(settings.RequiresEmptyLowerLayer,
                File.Exists(PurposeLaunchSettings.AppConfigPath) ? File.ReadAllText(PurposeLaunchSettings.AppConfigPath) : null);
            if (reason != null) return reason;
            return CheckAddressablesPlayMode();
        }

        /// <summary>
        /// Play mode 中・移行中、compile error 中（EnterPlaymode が Play に入らず ledger が残る）、compile 中・asset の更新中は拒否する。
        /// </summary>
        internal static string? CheckEditorState()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return "Stop Play first.";
            if (EditorUtility.scriptCompilationFailed) return "Fix the script compile errors first.";
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return "Wait until compiling and asset import finish.";
            return null;
        }

        /// <summary>
        /// Delivery 窓の「手で戻す」ledger がある時、menu からなら Reset して続けるかを聞く。dialog の無い呼び出しは拒否。
        /// 黙っては消さない（窓の手入力は窓の Reset まで残す契約）。
        /// </summary>
        internal static string? OfferDeliveryReset(bool interactive)
        {
            if (!ContentDeliveryPlayBridge.HasManualLedger) return null;
            if (interactive && EditorUtility.DisplayDialog("Content Delivery settings are active",
                    "'Use For Next Play' settings from Tools/OSM/Content/Delivery are still applied.", "Reset them and continue", "Cancel"))
            {
                ContentDeliveryPlayBridge.Reset();
                return null;
            }
            return "Content Delivery settings from 'Use For Next Play' are active. Reset them in Tools/OSM/Content/Delivery first.";
        }

        /// <summary>
        /// 宣言と食い違う Play になる外の状態の検査（Play As・検証 Play・From published directory で共通）。
        /// active Variant profile（config より優先して表現を変える。Production の空も選択に含める）、Delivery 窓の ledger、
        /// 制御する key の外の process 環境の値、Editor のコマンドライン（config の最上位の層）。
        /// </summary>
        internal static string? CheckLaunchConflicts(IReadOnlyList<string> controlledConfigKeys, IReadOnlyList<string> commandLine)
        {
            if (SceneVariantRuntimeBridge.EditorSceneVariantResolver?.Invoke() != null)
                return "A Variant profile is active and overrides the representation. Clear the profile selection in the Variant settings window.";
            if (ContentDeliveryPlayBridge.HasManualLedger)
                return "Content Delivery settings from 'Use For Next Play' are active. Reset them in Tools/OSM/Content/Delivery first.";
            var foreign = ContentDeliveryPlayBridge.FindForeignValues(controlledConfigKeys.Select(PurposeLaunchSettings.EnvironmentName));
            if (foreign.Count != 0)
                return "Environment variables already set outside this launcher: " + string.Join(", ", foreign);
            var arguments = FindCommandLineConflicts(commandLine, controlledConfigKeys);
            if (arguments.Count != 0)
                return "The Editor command line sets keys this launcher controls: " + string.Join(", ", arguments);
            return null;
        }

        /// <summary>Editor process の引数を Runtime と同じ <see cref="CommandLineConfigProvider"/> で読み、制御する key と重なるものを返す。</summary>
        internal static IReadOnlyList<string> FindCommandLineConflicts(IReadOnlyList<string> commandLine, IEnumerable<string> configKeys)
        {
            var store = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            new CommandLineConfigProvider(commandLine.ToArray()).Load(store);
            return configKeys.Where(store.ContainsKey).ToArray();
        }

        /// <summary>
        /// 置かない key（主値 Full の <c>assets:sceneVariant</c>）の実効値を空にするため、下位の層の app-config の同じ key が空でなければ拒否する。
        /// 上位の 2 層（環境・コマンドライン）は <see cref="CheckLaunchConflicts"/> が拒否で除く。
        /// </summary>
        internal static string? CheckEmptyLowerLayer(IReadOnlyList<string> keys, string? appConfigJson)
        {
            if (keys.Count == 0 || appConfigJson == null) return null;
            var store = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            JsonConfigFlattener.Flatten(appConfigJson, store);
            var set = keys.Where(key => store.TryGetValue(key, out var value) && value.Length != 0).ToArray();
            return set.Length == 0
                ? null
                : PurposeLaunchSettings.AppConfigPath + " sets keys that must be empty for this purpose: " + string.Join(", ", set);
        }

        /// <summary>
        /// source の Play の前提。既存 build を使う Play Mode では起動が古い bundle の app-config を読みうるので塞ぐ。
        /// disk の app-config と起動が読む bytes の一致までは保証しない（実効値は起動した AppConfig で観測する）。
        /// </summary>
        internal static string? CheckAddressablesPlayMode()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return "Addressables settings are missing.";
            return settings.ActivePlayModeDataBuilder is BuildScriptFastMode
                ? null
                : "Set the Addressables Play Mode Script to 'Use Asset Database' for Play As.";
        }
    }
}
