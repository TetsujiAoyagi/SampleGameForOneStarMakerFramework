#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetManagement;
using SampleGame.DependOnAll.Editor.Build;
using UnityEditor;

namespace SampleGame.Tests.Editor.Build
{
    /// <summary>
    /// 共通 ledger の戻し方。Play には入らず、mode 変化と update の handler を直接呼び、isPlaying と
    /// isPlayingOrWillChangePlaymode の 2 値を注入する。開発者の Editor の ledger と環境は SetUp で退避し TearDown で戻す。
    /// </summary>
    public sealed class ContentDeliveryPlayBridgeTests
    {
        private const string LedgerKey = "OSM.ContentDelivery.PlayBridge.v1";
        private const string A = "SAMPLEGAME_BS5B2TEST__A";
        private const string B = "SAMPLEGAME_BS5B2TEST__B";
        private string _savedLedger = "";
        private string? _savedA;
        private string? _savedB;

        [SetUp]
        public void SetUp()
        {
            _savedLedger = SessionState.GetString(LedgerKey, string.Empty);
            _savedA = Environment.GetEnvironmentVariable(A);
            _savedB = Environment.GetEnvironmentVariable(B);
            SessionState.EraseString(LedgerKey);
            Environment.SetEnvironmentVariable(A, "previous");
            Environment.SetEnvironmentVariable(B, null);
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable(A, _savedA);
            Environment.SetEnvironmentVariable(B, _savedB);
            if (_savedLedger.Length == 0) SessionState.EraseString(LedgerKey);
            else SessionState.SetString(LedgerKey, _savedLedger);
        }

        private static void ApplyOnePlay(string a = "placed", string b = "placedB") =>
            ContentDeliveryPlayBridge.ApplyForOnePlay(new[]
            {
                new KeyValuePair<string, string>(A, a), new KeyValuePair<string, string>(B, b),
            });

        private static void AssertPlaced()
        {
            Assert.That(Environment.GetEnvironmentVariable(A), Is.EqualTo("placed"));
            Assert.That(Environment.GetEnvironmentVariable(B), Is.EqualTo("placedB"));
        }

        private static void AssertRestored()
        {
            Assert.That(Environment.GetEnvironmentVariable(A), Is.EqualTo("previous"));
            Assert.That(Environment.GetEnvironmentVariable(B), Is.Null);
            Assert.That(ContentDeliveryPlayBridge.HasOnePlayLedger, Is.False);
        }

        // ═══ AC3: 1 回の Play で戻す ═══

        [Test]
        public void OnePlay_PlayThenEditMode_RestoresOnlyOnEnteredEditMode()
        {
            ApplyOnePlay();
            ContentDeliveryPlayBridge.HandlePlayModeStateChanged(PlayModeStateChange.ExitingEditMode);
            ContentDeliveryPlayBridge.HandleIdle(isPlaying: false, isPlayingOrWillChangePlaymode: true);
            ContentDeliveryPlayBridge.HandlePlayModeStateChanged(PlayModeStateChange.EnteredPlayMode);
            // Play 中の domain reload の後の初期化も同じ判定。
            ContentDeliveryPlayBridge.HandleIdle(isPlaying: true, isPlayingOrWillChangePlaymode: true);
            ContentDeliveryPlayBridge.HandlePlayModeStateChanged(PlayModeStateChange.ExitingPlayMode);
            AssertPlaced();
            ContentDeliveryPlayBridge.HandlePlayModeStateChanged(PlayModeStateChange.EnteredEditMode);
            AssertRestored();
        }

        [Test]
        public void OnePlay_TransitionAbandonedBeforePlay_RestoresOnEnteredEditMode()
        {
            ApplyOnePlay();
            ContentDeliveryPlayBridge.HandlePlayModeStateChanged(PlayModeStateChange.ExitingEditMode);
            AssertPlaced();
            ContentDeliveryPlayBridge.HandlePlayModeStateChanged(PlayModeStateChange.EnteredEditMode);
            AssertRestored();
        }

        [Test]
        public void OnePlay_TransitionNeverStarts_RestoresOnNextIdle()
        {
            ApplyOnePlay();
            ContentDeliveryPlayBridge.HandleIdle(isPlaying: false, isPlayingOrWillChangePlaymode: false);
            AssertRestored();
        }

        [Test]
        public void OnePlay_RequestWithdrawnWithoutExitingEditMode_RestoresOnLaterIdle()
        {
            ApplyOnePlay();
            ContentDeliveryPlayBridge.HandleIdle(isPlaying: false, isPlayingOrWillChangePlaymode: true);
            ContentDeliveryPlayBridge.HandleIdle(isPlaying: false, isPlayingOrWillChangePlaymode: true);
            AssertPlaced();
            ContentDeliveryPlayBridge.HandleIdle(isPlaying: false, isPlayingOrWillChangePlaymode: false);
            AssertRestored();
        }

        [Test]
        public void OnePlay_ExitingPlayModeIdle_DoesNotRestoreBeforeEditMode()
        {
            ApplyOnePlay();
            // ExitingPlayMode の間は isPlaying が真のまま isPlayingOrWillChangePlaymode が偽になる。
            ContentDeliveryPlayBridge.HandleIdle(isPlaying: true, isPlayingOrWillChangePlaymode: false);
            AssertPlaced();
            ContentDeliveryPlayBridge.HandlePlayModeStateChanged(PlayModeStateChange.EnteredEditMode);
            AssertRestored();
        }

        [Test]
        public void OnePlay_ValueChangedByOthers_IsNotRolledBack()
        {
            ApplyOnePlay();
            Environment.SetEnvironmentVariable(A, "someone-else");
            ContentDeliveryPlayBridge.HandlePlayModeStateChanged(PlayModeStateChange.EnteredEditMode);
            Assert.That(Environment.GetEnvironmentVariable(A), Is.EqualTo("someone-else"));
            Assert.That(Environment.GetEnvironmentVariable(B), Is.Null);
        }

        [Test]
        public void OnePlay_NewApply_ResetsPreviousOnePlayFirst()
        {
            ContentDeliveryPlayBridge.ApplyForOnePlay(new[] { new KeyValuePair<string, string>(B, "first") });
            ContentDeliveryPlayBridge.ApplyForOnePlay(new[] { new KeyValuePair<string, string>(A, "second") });
            Assert.That(Environment.GetEnvironmentVariable(B), Is.Null, "前の「1 回の Play で戻す」値は戻ってから置く");
            Assert.That(Environment.GetEnvironmentVariable(A), Is.EqualTo("second"));
            ContentDeliveryPlayBridge.HandlePlayModeStateChanged(PlayModeStateChange.EnteredEditMode);
            Assert.That(Environment.GetEnvironmentVariable(A), Is.EqualTo("previous"));
        }

        // ═══ AC3・AC4: 手で戻す ledger ═══

        [Test]
        public void Manual_V1LedgerWithoutMark_SurvivesEditModeAndIdle_AndBlocksOnePlay()
        {
            // 戻し方の欄が無い v1 の ledger（Delivery 窓の Use For Next Play）。
            Environment.SetEnvironmentVariable(A, "window");
            SessionState.SetString(LedgerKey, "{\"keys\":[\"" + A + "\"],\"previous\":[\"previous\"],\"installed\":[\"window\"]}");
            ContentDeliveryPlayBridge.HandleIdle(isPlaying: false, isPlayingOrWillChangePlaymode: false);
            ContentDeliveryPlayBridge.HandlePlayModeStateChanged(PlayModeStateChange.EnteredEditMode);
            Assert.That(ContentDeliveryPlayBridge.HasManualLedger, Is.True);
            Assert.That(Environment.GetEnvironmentVariable(A), Is.EqualTo("window"));

            Assert.Throws<InvalidOperationException>(() => ApplyOnePlay());
            Assert.That(Environment.GetEnvironmentVariable(A), Is.EqualTo("window"));
            Assert.That(Environment.GetEnvironmentVariable(B), Is.Null);

            ContentDeliveryPlayBridge.Reset();
            Assert.That(Environment.GetEnvironmentVariable(A), Is.EqualTo("previous"));
            Assert.That(ContentDeliveryPlayBridge.HasManualLedger, Is.False);
        }

        // ═══ AC2: 外の値 ═══

        [Test]
        public void ForeignValues_ExternalValueIsReported_OwnOnePlayValueIsNot()
        {
            Assert.That(ContentDeliveryPlayBridge.FindForeignValues(new[] { A, B }), Is.EqualTo(new[] { A }));
            Environment.SetEnvironmentVariable(A, null);
            ContentDeliveryPlayBridge.ApplyForOnePlay(new[] { new KeyValuePair<string, string>(B, "mine") });
            Assert.That(ContentDeliveryPlayBridge.FindForeignValues(new[] { A, B }), Is.Empty);
        }

        [Test]
        public void ForeignValues_CompanionSetFromOutside_RejectsPlannerPlayAs_WithoutChangingEnvironment()
        {
            const string companion = "SAMPLEGAME_WORLD__CELLCOMPANIONSET";
            var saved = Environment.GetEnvironmentVariable(companion);
            var resolver = SceneVariantRuntimeBridge.EditorSceneVariantResolver;
            try
            {
                // 開発者の Variant profile の選択に依らず、外の値の検査だけを見る。
                SceneVariantRuntimeBridge.EditorSceneVariantResolver = null;
                Environment.SetEnvironmentVariable(companion, "Full");
                var settings = PurposeLaunchSettings.PlayAs(SampleGameBuildPurposes.SpringPlannerWhitebox);
                var reason = PlayAsLauncher.CheckLaunchConflicts(settings.Controlled, Array.Empty<string>());
                Assert.That(reason, Does.Contain(companion));
                Assert.That(Environment.GetEnvironmentVariable(companion), Is.EqualTo("Full"));
                Assert.That(ContentDeliveryPlayBridge.HasOnePlayLedger, Is.False);
            }
            finally
            {
                Environment.SetEnvironmentVariable(companion, saved);
                SceneVariantRuntimeBridge.EditorSceneVariantResolver = resolver;
            }
        }

        [Test]
        public void ContentValues_KeepTheFiveDeliveryKeys()
        {
            var values = ContentDeliveryPlayBridge.ContentValues("root", "digest", "identity", "Full");
            Assert.That(values, Is.EqualTo(new[]
            {
                new KeyValuePair<string, string>("SAMPLEGAME_CONTENT__RUNTIMEMODE", "directory"),
                new KeyValuePair<string, string>("SAMPLEGAME_CONTENT__INSTALLEDREVISIONPATH", "root"),
                new KeyValuePair<string, string>("SAMPLEGAME_CONTENT__MANIFESTSHA256", "digest"),
                new KeyValuePair<string, string>("SAMPLEGAME_CONTENT__BUILDIDENTITY", "identity"),
                new KeyValuePair<string, string>("SAMPLEGAME_CONTENT__REPRESENTATION", "Full"),
            }));
        }
    }
}
