#nullable enable

using System;
using NUnit.Framework;
using OneStarMaker.Runtime.SceneSystem;

namespace OneStarMaker.Tests.SceneSystem
{
    [TestFixture]
    public class SceneLifecycleManagerTests
    {
        private SceneLifecycleManager _manager = null!;

        [SetUp]
        public void SetUp()
        {
            _manager = new SceneLifecycleManager();
        }

        // ─── Helper: 正規パスで指定状態まで遷移する ───

        private void AdvanceToPreLoaded()
        {
            _manager.TransitionTo(SceneState.PreLoading);
            _manager.TransitionTo(SceneState.PreLoaded);
        }

        private void AdvanceToWaitLoadChildScene()
        {
            AdvanceToPreLoaded();
            _manager.TransitionTo(SceneState.Loading);
            _manager.TransitionTo(SceneState.Loaded);
            _manager.TransitionTo(SceneState.WaitLoadChildScene);
        }

        private void AdvanceToStable()
        {
            AdvanceToWaitLoadChildScene();
            _manager.TransitionTo(SceneState.Initializing);
            _manager.TransitionTo(SceneState.Stable);
        }

        private void AdvanceToUnloaded()
        {
            AdvanceToStable();
            _manager.TransitionTo(SceneState.PreUnloading);
            _manager.TransitionTo(SceneState.PreUnloaded);
            _manager.TransitionTo(SceneState.Unloading);
            _manager.TransitionTo(SceneState.Unloaded);
        }

        // ─── 初期状態 ───

        [Test]
        public void InitialState_IsNone()
        {
            Assert.AreEqual(SceneState.None, _manager.State);
        }

        [Test]
        public void InitialState_IsNone_Property()
        {
            Assert.IsTrue(_manager.IsNone);
        }

        // ─── 正常遷移 ───

        [Test]
        public void TransitionTo_None_To_PreLoading_Succeeds()
        {
            _manager.TransitionTo(SceneState.PreLoading);
            Assert.AreEqual(SceneState.PreLoading, _manager.State);
        }

        [Test]
        public void TransitionTo_FullHappyPath_Succeeds()
        {
            var states = new[]
            {
                SceneState.PreLoading,
                SceneState.PreLoaded,
                SceneState.Loading,
                SceneState.Loaded,
                SceneState.WaitLoadChildScene,
                SceneState.Initializing,
                SceneState.Stable,
                SceneState.PreUnloading,
                SceneState.PreUnloaded,
                SceneState.Unloading,
                SceneState.Unloaded,
                SceneState.AfterUnloading,
            };

            foreach (var state in states)
            {
                _manager.TransitionTo(state);
                Assert.AreEqual(state, _manager.State, $"Failed to transition to {state}");
            }
        }

        // ─── ヘルパープロパティ ───

        [Test]
        public void IsInLoadingPhase_True_During_PreLoading_To_WaitLoadChildScene()
        {
            _manager.TransitionTo(SceneState.PreLoading);
            Assert.IsTrue(_manager.IsInLoadingPhase);

            _manager.TransitionTo(SceneState.PreLoaded);
            Assert.IsTrue(_manager.IsInLoadingPhase);

            _manager.TransitionTo(SceneState.Loading);
            Assert.IsTrue(_manager.IsInLoadingPhase);

            _manager.TransitionTo(SceneState.Loaded);
            Assert.IsTrue(_manager.IsInLoadingPhase);

            _manager.TransitionTo(SceneState.WaitLoadChildScene);
            Assert.IsTrue(_manager.IsInLoadingPhase);
        }

        [Test]
        public void IsInLoadingPhase_False_After_Initializing()
        {
            AdvanceToWaitLoadChildScene();
            _manager.TransitionTo(SceneState.Initializing);
            Assert.IsFalse(_manager.IsInLoadingPhase);
        }

        [Test]
        public void IsActive_True_For_Initializing_And_Stable()
        {
            AdvanceToWaitLoadChildScene();
            _manager.TransitionTo(SceneState.Initializing);
            Assert.IsTrue(_manager.IsActive);

            _manager.TransitionTo(SceneState.Stable);
            Assert.IsTrue(_manager.IsActive);
        }

        [Test]
        public void IsActive_False_For_PreLoading()
        {
            _manager.TransitionTo(SceneState.PreLoading);
            Assert.IsFalse(_manager.IsActive);
        }

        [Test]
        public void IsUnloadStarted_True_From_PreUnloading()
        {
            AdvanceToStable();
            _manager.TransitionTo(SceneState.PreUnloading);
            Assert.IsTrue(_manager.IsUnloadStarted);
        }

        [Test]
        public void IsLoadedOrActive_True_From_Loading_To_Stable()
        {
            AdvanceToPreLoaded();
            _manager.TransitionTo(SceneState.Loading);
            Assert.IsTrue(_manager.IsLoadedOrActive);

            _manager.TransitionTo(SceneState.Loaded);
            Assert.IsTrue(_manager.IsLoadedOrActive);

            _manager.TransitionTo(SceneState.WaitLoadChildScene);
            Assert.IsTrue(_manager.IsLoadedOrActive);

            _manager.TransitionTo(SceneState.Initializing);
            Assert.IsTrue(_manager.IsLoadedOrActive);

            _manager.TransitionTo(SceneState.Stable);
            Assert.IsTrue(_manager.IsLoadedOrActive);
        }

        [Test]
        public void IsLoadedOrActive_False_For_PreUnloading()
        {
            AdvanceToStable();
            _manager.TransitionTo(SceneState.PreUnloading);
            Assert.IsFalse(_manager.IsLoadedOrActive);
        }

        [Test]
        public void IsLoadCanceled_True_When_LoadCanceled()
        {
            AdvanceToPreLoaded();
            _manager.TransitionTo(SceneState.Loading);
            _manager.TransitionTo(SceneState.LoadCanceled);
            Assert.IsTrue(_manager.IsLoadCanceled);
        }

        [Test]
        public void IsLoadCanceled_Means_NotLoadedOrActive()
        {
            AdvanceToPreLoaded();
            _manager.TransitionTo(SceneState.Loading);
            _manager.TransitionTo(SceneState.LoadCanceled);
            Assert.IsFalse(_manager.IsLoadedOrActive);
        }

        [Test]
        public void IsInAfterUnloading_True_When_AfterUnloading()
        {
            AdvanceToUnloaded();
            _manager.TransitionTo(SceneState.AfterUnloading);
            Assert.IsTrue(_manager.IsInAfterUnloading);
        }

        // ─── 不正遷移 ───

        [Test]
        public void TransitionTo_SameState_Throws()
        {
            _manager.TransitionTo(SceneState.PreLoading);
            Assert.Throws<InvalidOperationException>(
                () => _manager.TransitionTo(SceneState.PreLoading));
        }

        [Test]
        public void TransitionTo_Backward_Throws()
        {
            _manager.TransitionTo(SceneState.PreLoading);
            _manager.TransitionTo(SceneState.PreLoaded);
            Assert.Throws<InvalidOperationException>(
                () => _manager.TransitionTo(SceneState.PreLoading));
        }

        [Test]
        public void TransitionTo_None_From_NonNone_Throws()
        {
            _manager.TransitionTo(SceneState.PreLoading);
            Assert.Throws<InvalidOperationException>(
                () => _manager.TransitionTo(SceneState.None));
        }

        [Test]
        public void TransitionTo_Skip_Intermediate_Throws()
        {
            // None directly to Loading (skipping PreLoading, PreLoaded) should throw
            Assert.Throws<InvalidOperationException>(
                () => _manager.TransitionTo(SceneState.Loading));
        }

        [Test]
        public void TransitionTo_Skip_PreLoaded_Throws()
        {
            // PreLoading → Loading (skipping PreLoaded) should throw
            _manager.TransitionTo(SceneState.PreLoading);
            Assert.Throws<InvalidOperationException>(
                () => _manager.TransitionTo(SceneState.Loading));
        }

        // ─── LoadCanceled 特殊遷移 ───

        [Test]
        public void TransitionTo_LoadCanceled_From_Loading_Succeeds()
        {
            AdvanceToPreLoaded();
            _manager.TransitionTo(SceneState.Loading);
            _manager.TransitionTo(SceneState.LoadCanceled);
            Assert.AreEqual(SceneState.LoadCanceled, _manager.State);
        }

        [Test]
        public void TransitionTo_LoadCanceled_From_PreLoading_Succeeds()
        {
            _manager.TransitionTo(SceneState.PreLoading);
            _manager.TransitionTo(SceneState.LoadCanceled);
            Assert.AreEqual(SceneState.LoadCanceled, _manager.State);
        }

        [Test]
        public void TransitionTo_LoadCanceled_From_WaitLoadChildScene_Succeeds()
        {
            AdvanceToWaitLoadChildScene();
            _manager.TransitionTo(SceneState.LoadCanceled);
            Assert.AreEqual(SceneState.LoadCanceled, _manager.State);
        }

        [Test]
        public void TransitionTo_LoadCanceled_From_Stable_Throws()
        {
            AdvanceToStable();
            Assert.Throws<InvalidOperationException>(
                () => _manager.TransitionTo(SceneState.LoadCanceled));
        }

        // ─── LoadCanceled → AfterUnloading 遷移（キャンセル後クリーンアップ） ───

        [Test]
        public void TransitionTo_AfterUnloading_From_LoadCanceled_Succeeds()
        {
            _manager.TransitionTo(SceneState.PreLoading);
            _manager.TransitionTo(SceneState.LoadCanceled);
            _manager.TransitionTo(SceneState.AfterUnloading);
            Assert.AreEqual(SceneState.AfterUnloading, _manager.State);
        }

        [Test]
        public void TransitionTo_AfterUnloading_From_LoadCanceled_Via_PreLoaded()
        {
            AdvanceToPreLoaded();
            _manager.TransitionTo(SceneState.LoadCanceled);
            _manager.TransitionTo(SceneState.AfterUnloading);
            Assert.AreEqual(SceneState.AfterUnloading, _manager.State);
        }

        [Test]
        public void TransitionTo_AfterUnloading_From_LoadCanceled_Via_WaitLoadChildScene()
        {
            AdvanceToWaitLoadChildScene();
            _manager.TransitionTo(SceneState.LoadCanceled);
            _manager.TransitionTo(SceneState.AfterUnloading);
            Assert.AreEqual(SceneState.AfterUnloading, _manager.State);
        }
    }

}
