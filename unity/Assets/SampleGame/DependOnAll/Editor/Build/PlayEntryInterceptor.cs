#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneStarMaker.Editor.PlaySnapshot;
using OneStarMaker.Foundation.Config;
using OneStarMaker.Runtime.AssetManagement;
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
            PrepareSnapshot = PlaySnapshotSession.Prepare,
            ReleaseSnapshot = PlaySnapshotSession.Release,
            CloseObservation = PlayEntryRecord.StopObserving,
        });

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
            var snapshot = plan.Rule == PlayEntryRule.SnapshotRoute
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
                : PlayAsLauncher.CheckLaunchConflicts(PurposeLaunchSettings.DirectoryPlayConfigKeys(declaration.RuntimeDefaults), commandLine);
            if (validateCandidate)
            {
                var selection = PlayEntryCandidates.Select(VerifyPlayLauncher.ResolveCacheRoot(ContentDeliveryWindow.LoadSettings().cacheRoot),
                    declaration, VerifyPlayLauncher.Target, SampleGameBuildPurposes.FirstSceneOf);
                facts.Candidate = selection.Candidate;
                facts.CandidateFailure = selection.Failure;
                facts.CandidateWarnings = selection.Warnings;
            }
            return facts;
        }

        /// <summary>
        /// 混成の経路は初回 Scene を置かない。環境変数は空文字で上書きできないので、app-config（JSON の層）に空でない
        /// <c>content:firstScene</c> があれば、開いている Scene の複製と初回 Scene が重なりうるとして拒否の理由を返す。
        /// mode の 3 key だけを残す <see cref="PlayEntryConfigLayer"/> には載らないので、本文から直接読む。環境変数とコマンドラインの層は
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
