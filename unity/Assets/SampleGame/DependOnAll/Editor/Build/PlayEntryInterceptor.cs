#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneStarMaker.Editor.PlaySnapshot;
using OneStarMaker.Foundation.Config;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.SceneSystem;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SampleGame.DependOnAll.Editor.Build
{
    /// <summary>
    /// Unity の Play ボタンの配線。Editor の事実の取得と副作用をここだけに置き、順序は <see cref="PlayEntryTransaction"/>、規則は
    /// <see cref="PlayEntryDecision"/> に任せる。
    /// <para>
    /// log の購読と mode 変化の購読は static constructor で毎回張る。Domain Reload が有効な Play では ExitingEditMode と BeforeSceneLoad の
    /// 間で static が消えるので、購読の開始を「計画を書いた時」にすると起動の失敗を取り逃がす。観測するかは SessionState の record が決める。
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    internal static class PlayEntryInterceptor
    {
        private static readonly PlayEntryTransaction Transaction = new(new PlayEntryPorts
        {
            CollectFacts = CollectFacts,
            CancelPlay = () => EditorApplication.isPlaying = false,
            IsIdle = () => !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode,
            ApplyForOnePlay = ContentDeliveryPlayBridge.ApplyForOnePlay,
            AlignFastMode = AlignFastMode,
            EnterPlaymode = EditorApplication.EnterPlaymode,
            PublishPlanned = PublishPlanned,
            PublishBlocked = PublishBlocked,
            ConfirmSaveScenes = EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo,
            ReplaceWithEmptyScene = () => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single),
            // 起動時に lifecycle へ要求する Scene の規則: 開いている Scene のうち SceneResourceMap にある物は全て要求する（今までの動きと同じで、
            // 利用者の新しい入力は無い）。port の Action の形は変えない。
            PrepareSnapshot = () => PlaySnapshotSession.Prepare(ReadSceneResourceMapNames().Contains),
            ReleaseSnapshot = PlaySnapshotSession.Release,
            CloseObservation = PlayEntryRecord.StopObserving,
            EnumerateUnsavedAssets = EnumerateUnsavedAssets,
            ConfirmSaveAssets = ConfirmSaveAssets,
            DiscardSaveAssetsResponse = () => SessionState.EraseString(SaveAssetsResponseKey),
            SaveAssets = AssetDatabase.SaveAssets,
            ApplyImports = AssetDatabase.Refresh,
            IsCompilingOrUpdating = () => EditorApplication.isCompiling || EditorApplication.isUpdating,
            RunOverlay = RunOverlay,
        });

        /// <summary>
        /// 未保存の asset の確認への、1 回だけの先置きの応答（<see cref="SaveAssetsResponseSave"/> か <see cref="SaveAssetsResponseCancel"/>）。
        /// 実 Editor の判定は eval で modal の dialog に答えられないので、この口だけを足す。確認の段に着いた時に 1 回読んで消し、
        /// 検出が 0 件で確認を出さない時も消す（残った応答が後の Play の確認に勝手に答えない）。無ければ dialog を出す。
        /// </summary>
        internal const string SaveAssetsResponseKey = "OSM.PlayEntry.SaveAssetsResponse.v1";
        internal const string SaveAssetsResponseSave = "save";
        internal const string SaveAssetsResponseCancel = "cancel";

        static PlayEntryInterceptor()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += Transaction.OnTick;
            Application.logMessageReceived += OnLogMessage;
            // snapshot の Scene が起動の後に別の供給元から読み直されたら record と Warning に出す（domain reload の後もここで張り直す）。
            PlaySnapshotSession.SourceChanged += PlayEntryRecord.MarkSourceChanged;
            // batch には Toolbar が無い。
            if (!Application.isBatchMode) PlayEntryRecord.Changed += PlayEntryWindow.RefreshToolbar;
        }

        /// <summary>SceneResourceMap の Scene 名（Prepare の時点の asset から読む。map が無ければ例外で、入口は準備の失敗として扱う）。</summary>
        private static HashSet<string> ReadSceneResourceMapNames()
        {
            var map = AssetDatabase.LoadAssetAtPath<SceneResourceMap>(SampleGameBootstrapContentComposer.SceneResourceMapPath);
            if (map == null) throw new InvalidOperationException("SceneResourceMap is missing: " + SampleGameBootstrapContentComposer.SceneResourceMapPath);
            return new HashSet<string>(map.SceneResources.Where(x => x != null).Select(x => x.Identity), StringComparer.Ordinal);
        }

        /// <summary>
        /// load 済みで Dirty の asset（<c>Assets/</c> の下、Scene 以外）の path。<c>Assets/</c> の外は built-in と package の object が
        /// 数百件 Dirty と出るので数えない。load されていない asset、Undo の履歴だけの変更、import の設定の未適用の変更は検出に掛からない
        /// （既知の限界。掛からない Dirty は確認なしで複製の保存に書かれうる）。
        /// </summary>
        internal static IReadOnlyList<string> EnumerateUnsavedAssets()
        {
            var paths = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var item in Resources.FindObjectsOfTypeAll<UnityEngine.Object>())
            {
                // 破棄されうる UnityEngine.Object なので == で判定する（偽 null）。
                if (item == null || !EditorUtility.IsPersistent(item) || !EditorUtility.IsDirty(item)) continue;
                var path = AssetDatabase.GetAssetPath(item);
                if (path.StartsWith("Assets/", StringComparison.Ordinal) && !path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    paths.Add(path);
            }
            return paths.ToArray();
        }

        private static bool ConfirmSaveAssets(IReadOnlyList<string> unsaved, string message)
        {
            var response = SessionState.GetString(SaveAssetsResponseKey, string.Empty);
            SessionState.EraseString(SaveAssetsResponseKey);
            if (response.Length != 0) return string.Equals(response, SaveAssetsResponseSave, StringComparison.Ordinal);
            return EditorUtility.DisplayDialog("Play with local edits", message, "Save and continue", "Cancel");
        }

        /// <summary>上書きの段の実物。base は候補の検証した install、他の供給元は今準備した複製のうち map にある Scene。</summary>
        private static PlayEntryOverlayResult RunOverlay(PlayEntryFacts facts)
        {
            var install = facts.Candidate?.Install
                          ?? throw new InvalidOperationException("The verified installed revision has no install result for the overlay plan.");
            var supplied = PlayEntryOverlay.SuppliedSceneKeys(PlaySnapshotSession.Declaration.Select(x => x.Identity), ReadSceneResourceMapNames());
            var ports = SampleGameContentBuild.OverlayPorts(SampleGameBuildPurposes.Find(facts.RunPurpose), install, supplied,
                SampleGameContentBuild.OverlayContentSetOf(SampleGameBuildPurposes.Find(facts.RunPurpose)), () => EditorApplication.timeSinceStartup);
            return PlayEntryOverlay.Run(ports);
        }

        /// <summary>回復操作「空 Scene で Play」。保存を拒まれたら偽。</summary>
        internal static bool RecoverInEmptyScene() => Transaction.RecoverInEmptyScene();

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.ExitingEditMode:
                    Transaction.OnExitingEditMode();
                    break;
                case PlayModeStateChange.EnteredPlayMode:
                    Transaction.OnEnteredPlayMode();
                    PlayEntryRecord.MarkEnteredPlay(EditorApplication.timeSinceStartup);
                    if (!Application.isBatchMode && PlayEntryRecord.Load()?.policy.rule != nameof(PlayEntryRule.TestRunnerScene))
                        PlayEntryWindow.OpenOncePerSession();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    // 複製の解除は PlaySnapshotSession が同じ通知で行う。購読の順序に依らないよう、ここでも呼ぶ（何度呼んでも同じ）。
                    PlaySnapshotSession.Release();
                    PlayEntryRecord.MarkCopiesRemoved(PlaySnapshotSession.HasNoCopiesOnDisk);
                    PlayEntryRecord.StopObserving();
                    // 取り込みと由来、複製の残りは Play の後でないと揃わないので、snapshot の Play は終わった時にもう 1 行出す。
                    if (PlayEntryRecord.Load() is { policy: { snapshot: { Length: > 0 } } } finished)
                        Debug.Log(PlayEntryRecord.FormatConsoleLine(finished));
                    break;
            }
        }

        private static void OnLogMessage(string condition, string stackTrace, LogType type) =>
            PlayEntryRecord.ObserveLog(condition, type, EditorApplication.timeSinceStartup);

        private static void PublishPlanned(PlayEntryFacts? facts, PlayEntryPlan plan)
        {
            var snapshot = PlayEntryDecision.IsSnapshotRule(plan.Rule)
                ? PlaySnapshotSession.Declaration.Select(x => new PlayEntryRecord.SnapshotScene
                    { identity = x.Identity, sourcePath = x.SourcePath, dirty = x.WasDirty, sha256 = x.Sha256, active = x.IsActive }).ToArray()
                : null;
            PlayEntryRecord.BeginPlanned(facts, plan, snapshot);
            foreach (var warning in plan.Warnings) Debug.LogWarning(PlayEntryRecord.LogPrefix + warning);
        }

        private static void PublishBlocked(PlayEntryFacts? facts, PlayEntryPlan plan)
        {
            PlayEntryRecord.MarkBlocked(facts, plan);
            if (!Application.isBatchMode) PlayEntryWindow.ShowBlocked();
        }

        private static void AlignFastMode()
        {
            // Settings は UnityEngine.Object なので、破棄されうる値を ?? で判定しない（偽 null）。
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) throw new InvalidOperationException("Addressables settings are missing.");
            var index = settings.DataBuilders.FindIndex(x => x is BuildScriptFastMode);
            if (index < 0) throw new InvalidOperationException("The Addressables 'Use Asset Database' builder is not registered.");
            // ActivePlayerDataBuilderIndex と違い、こちらの保存先は Library/ で追跡 file を dirty にしない。
            settings.ActivePlayModeDataBuilderIndex = index;
            Debug.Log(PlayEntryRecord.LogPrefix + "Addressables Play Mode Script set to 'Use Asset Database' for source Play.");
        }

        private static PlayEntryFacts CollectFacts(bool validateCandidate)
        {
            var facts = new PlayEntryFacts { IsBatchMode = Application.isBatchMode };
            if (facts.IsBatchMode) return facts;

            var openScenes = new List<string>();
            var loadedScenes = new List<(string Name, string Path)>();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                openScenes.Add(scene.name);
                if (scene.isLoaded) loadedScenes.Add((scene.name, scene.path));
            }
            facts.IsTestRunnerScene = PlayEntryDecision.IsTestRunnerScene(openScenes);
            if (facts.IsTestRunnerScene) return facts;

            var commandLine = Environment.GetCommandLineArgs();
            var appConfig = File.Exists(PurposeLaunchSettings.AppConfigPath) ? File.ReadAllText(PurposeLaunchSettings.AppConfigPath) : null;
            var declaration = SampleGameBuildPurposes.Find(PlayEntrySettings.RunPurpose);
            facts.RunPurpose = declaration.Name;
            facts.Route = PlayEntrySettings.Route;
            facts.JsonLayer = ReadJsonLayer(appConfig);
            facts.EnvironmentLayer = ReadEnvironmentLayer();
            facts.CommandLineLayer = ReadCommandLineLayer(commandLine);
            facts.HasOnePlayLedger = ContentDeliveryPlayBridge.HasOnePlayLedger;
            facts.HasManualLedger = ContentDeliveryPlayBridge.HasManualLedger;
            facts.ReusableScenes = VerifyPlayLauncher.FindReusableLoadedScenes();
            facts.HasVariantProfile = SceneVariantRuntimeBridge.EditorSceneVariantResolver?.Invoke() != null;
            facts.OpenScenes = loadedScenes;
            facts.IsSceneReloadDisabled = EditorSettings.enterPlayModeOptionsEnabled
                                          && (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableSceneReload) != 0;
            // SceneAsset は UnityEngine.Object なので、偽 null を == で判定する。
            facts.HasPlayModeStartScene = EditorSceneManager.playModeStartScene != null;
            facts.SnapshotMatchesOpenScenes = PlaySnapshotSession.Matches(loadedScenes)
                && string.Equals(Environment.GetEnvironmentVariable(PurposeLaunchSettings.EnvironmentName(PurposeLaunchSettings.SnapshotExpectedKey)),
                    "true", StringComparison.Ordinal);

            var addressables = AddressableAssetSettingsDefaultObject.Settings;
            facts.IsFastMode = addressables != null && addressables.ActivePlayModeDataBuilder is BuildScriptFastMode;

            if (facts.Route == PlayEntryRoute.Source)
            {
                var settings = PurposeLaunchSettings.PlayAs(declaration);
                facts.SourceEnvironment = settings.PlacedEnvironment;
                facts.ConflictReason = (addressables == null ? "Addressables settings are missing." : null)
                                       ?? PlayAsLauncher.CheckLaunchConflicts(settings.Controlled, commandLine)
                                       ?? PlayAsLauncher.CheckEmptyLowerLayer(settings.RequiresEmptyLowerLayer, appConfig);
                return facts;
            }

            facts.ConflictReason = facts.Route == PlayEntryRoute.BuiltWithOpenScenes
                ? PlayAsLauncher.CheckLaunchConflicts(PurposeLaunchSettings.SnapshotPlayConfigKeys(declaration.RuntimeDefaults), commandLine)
                  ?? SnapshotFirstSceneConflict(appConfig)
                : facts.Route == PlayEntryRoute.BuiltWithLocalEdits
                ? PlayAsLauncher.CheckLaunchConflicts(PurposeLaunchSettings.LocalEditsPlayConfigKeys(declaration.RuntimeDefaults), commandLine)
                  ?? SnapshotFirstSceneConflict(appConfig)
                : PlayAsLauncher.CheckLaunchConflicts(PurposeLaunchSettings.DirectoryPlayConfigKeys(declaration.RuntimeDefaults), commandLine);
            if (validateCandidate)
            {
                var cacheRoot = VerifyPlayLauncher.ResolveCacheRoot(ContentDeliveryWindow.LoadSettings().cacheRoot);
                // 取得で選んだ base の記録（無ければ今の並び。読めなくても例外を漏らさず Warning になる）。
                var selection = PlayEntryCandidates.Select(cacheRoot, declaration, VerifyPlayLauncher.Target, SampleGameBuildPurposes.FirstSceneOf,
                    ContentBaseSelection.Read(ContentBaseSelection.DefaultPath, cacheRoot, declaration.Name, VerifyPlayLauncher.Target));
                facts.Candidate = selection.Candidate;
                facts.CandidateFailure = selection.Failure;
                facts.CandidateWarnings = selection.Warnings;
            }
            return facts;
        }

        /// <summary>
        /// 混成の経路は初回 Scene を置かない。環境変数は空文字で上書きできないので、app-config（JSON の層）に空でない
        /// <c>content:firstScene</c> があれば、開いている Scene の複製と初回 Scene が重なりうるとして拒否の理由を返す。
        /// 明示設定と数える key（<see cref="PlayEntryDecision.ExplicitSettingKeys"/>）だけを残す <see cref="PlayEntryConfigLayer"/> には載らないので、本文から直接読む。環境変数とコマンドラインの層は
        /// <see cref="PlayAsLauncher.CheckLaunchConflicts"/> が拒否する。
        /// </summary>
        internal static string? SnapshotFirstSceneConflict(string? appConfigJson)
        {
            var reason = PlayAsLauncher.CheckEmptyLowerLayer(new[] { PurposeLaunchSettings.FirstSceneKey }, appConfigJson);
            return reason == null ? null : reason + " (the first scene could overlap the open scenes).";
        }

        // 3 層とも Runtime と同じ読み手で読む。JSON は disk の file で、起動が読む bytes との一致までは保証しない（明示 addressables の Warning の理由）。

        internal static PlayEntryConfigLayer ReadJsonLayer(string? appConfigJson)
        {
            var store = NewStore();
            if (appConfigJson != null) JsonConfigFlattener.Flatten(appConfigJson, store);
            return PlayEntryConfigLayer.From(store);
        }

        internal static PlayEntryConfigLayer ReadEnvironmentLayer()
        {
            var store = NewStore();
            new EnvironmentVariableConfigProvider(PurposeLaunchSettings.EnvironmentPrefix).Load(store);
            return PlayEntryConfigLayer.From(store);
        }

        internal static PlayEntryConfigLayer ReadCommandLineLayer(IReadOnlyList<string> commandLine)
        {
            var store = NewStore();
            new CommandLineConfigProvider(commandLine.ToArray()).Load(store);
            return PlayEntryConfigLayer.From(store);
        }

        private static Dictionary<string, string> NewStore() => new(StringComparer.OrdinalIgnoreCase);
    }
}
