#nullable enable

using System;
using NUnit.Framework;
using Unity.Profiling;
using OneStarMaker.Runtime.InputSystem;
using OneStarMaker.Runtime.SceneSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace OneStarMaker.Tests.InputSystem
{
    [TestFixture]
    // The standard Unity EditMode runner executes cases serially on the editor main thread.
    // SetUp/cleanup scopes the temporary global settings to each case.
    public sealed class InputActionAssetReaderTests
    {
        private InputActionAsset _asset = null!;
        private Gamepad _pad = null!;
        private InputManager? _manager;
        private InputSettings _originalSettings = null!;
        private InputSettings _fixtureSettings = null!;
        private HideFlags _originalSettingsHideFlags;
        private bool _originalSettingsCaptured;

        [SetUp]
        public void SetUp()
        {
            try
            {
                _originalSettings = UnityEngine.InputSystem.InputSystem.settings;
                if (_originalSettings == null)
                    throw new InvalidOperationException("The fixture requires live original input settings.");
                _originalSettingsHideFlags = _originalSettings.hideFlags;
                _originalSettingsCaptured = true;
                _fixtureSettings = UnityEngine.Object.Instantiate(_originalSettings);
                _fixtureSettings.hideFlags = HideFlags.HideAndDontSave;
                _fixtureSettings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
                // Package 1.20.0 ignores action state changes in Editor updates.
                // This public, non-persisted flag lets explicit Update drive real player action state in EditMode.
                _fixtureSettings.SetInternalFeatureFlag("RUN_PLAYER_UPDATES_IN_EDIT_MODE", true);
                // Package 1.20.0 destroys outgoing settings whose flags exactly equal HideAndDontSave.
                // Preserve the original object, retaining its no-save/no-unload flags until it is restored.
                if (_originalSettings.hideFlags == HideFlags.HideAndDontSave)
                    _originalSettings.hideFlags &= ~HideFlags.NotEditable;
                UnityEngine.InputSystem.InputSystem.settings = _fixtureSettings;

                _pad = UnityEngine.InputSystem.InputSystem.AddDevice<Gamepad>();
                _asset = ScriptableObject.CreateInstance<InputActionAsset>();
                // 専用 device に限定し、人間の接続済み device を読まない。
                _asset.devices = new InputDevice[] { _pad };
                var player = new InputActionMap("Player");
                player.AddAction("Move", InputActionType.Value, "<Gamepad>/leftStick", expectedControlLayout: "Vector2");
                player.AddAction("Attack", InputActionType.Button, "<Gamepad>/buttonSouth", expectedControlLayout: "Button");
                player.AddAction("Pose", InputActionType.Value, expectedControlLayout: "Vector3");
                var ui = new InputActionMap("UI");
                ui.AddAction("Navigate", InputActionType.Value, "<Gamepad>/leftStick", expectedControlLayout: "Vector2");
                ui.AddAction("Submit", InputActionType.Button, "<Gamepad>/buttonSouth", expectedControlLayout: "Button");
                _asset.AddActionMap(player);
                _asset.AddActionMap(ui);
            }
            catch (Exception setupException)
            {
                // NUnit does not guarantee TearDown when SetUp fails.
                try { CleanupFixture(); }
                catch (Exception cleanupException)
                {
                    throw new AggregateException("Fixture setup and cleanup failed.", setupException, cleanupException);
                }
                throw;
            }
        }

        [TearDown]
        public void TearDown() => CleanupFixture();

        private void CleanupFixture()
        {
            try { _manager?.Dispose(); }
            finally
            {
                _manager = null;
                try
                {
                    if (_asset != null) UnityEngine.Object.DestroyImmediate(_asset);
                }
                finally
                {
                    try
                    {
                        if (_pad != null && _pad.added) UnityEngine.InputSystem.InputSystem.RemoveDevice(_pad);
                    }
                    finally
                    {
                        try
                        {
                            if (_originalSettingsCaptured)
                            {
                                Assert.That(_originalSettings != null, Is.True, "Original settings must remain alive.");
                                try { UnityEngine.InputSystem.InputSystem.settings = _originalSettings; }
                                finally
                                {
                                    if (_originalSettings != null)
                                        _originalSettings.hideFlags = _originalSettingsHideFlags;
                                }

                                // Cleanup observations are outside the native allocation window.
                                Assert.That(_originalSettings != null, Is.True);
                                Assert.That(UnityEngine.InputSystem.InputSystem.settings == _originalSettings, Is.True);
                                Assert.That(_originalSettings.hideFlags, Is.EqualTo(_originalSettingsHideFlags));
                            }
                        }
                        finally
                        {
                            // Restoring settings may already destroy the temporary clone.
                            // If restoration throws, keep any still-active global settings alive.
                            if (_fixtureSettings != null &&
                                UnityEngine.InputSystem.InputSystem.settings != _fixtureSettings)
                                UnityEngine.Object.DestroyImmediate(_fixtureSettings);
                            _originalSettings = null!;
                            _fixtureSettings = null!;
                            _originalSettingsCaptured = false;
                        }
                    }
                }
            }
        }

        private void HoldControls()
        {
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(_pad, new GamepadState());
            UnityEngine.InputSystem.InputSystem.Update();
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(_pad,
                new GamepadState { leftStick = new Vector2(0.75f, 0.5f) }.WithButton(GamepadButton.South));
            UnityEngine.InputSystem.InputSystem.Update();
            // Native preconditions are outside the allocation measurement window.
            Assert.That(InputState.currentUpdateType, Is.EqualTo(InputUpdateType.Manual));
            Assert.That(_pad.buttonSouth.ReadValue(), Is.EqualTo(1f));
            var playerEnabled = _asset.FindActionMap("Player").enabled;
            var map = _asset.FindActionMap(playerEnabled ? "Player" : "UI");
            Assert.That(map.FindAction(playerEnabled ? "Attack" : "Submit").ReadValue<float>(), Is.EqualTo(1f));
            Assert.That(map.FindAction(playerEnabled ? "Move" : "Navigate").ReadValue<Vector2>(),
                Is.EqualTo(_pad.leftStick.ReadValue()));
        }

        [Test]
        public void RealAsset_ReadsLevels_SelectsMaps_AndNeutralizesWithoutAnotherTick()
        {
            _manager = new InputManager(_asset);
            var manager = _manager;
            Assert.That(manager.UnsupportedActionCount, Is.EqualTo(1));
            Assert.That(_asset.FindActionMap("Player").enabled, Is.True);
            Assert.That(_asset.FindActionMap("UI").enabled, Is.False);
            Assert.That(manager.TryGetActionIndex("Attack", InputControlMap.Player, out var attack), Is.True);
            Assert.That(manager.TryGetActionIndex("Move", InputControlMap.Player, out var move), Is.True);
            Assert.That(manager.TryGetActionIndex("Submit", InputControlMap.UI, out var submit), Is.True);
            Assert.That(manager.TryGetActionIndex("Pose", InputControlMap.Player, out _), Is.False);
            HoldControls();
            manager.Sample();
            Assert.That(manager.Published[attack].X, Is.Zero);
            manager.SetInteractionState(SceneState.Stable);
            manager.Sample();
            Assert.That(manager.Published[attack].X, Is.EqualTo(1f));
            var expectedMove = _pad.leftStick.ReadValue();
            Assert.That(expectedMove.x, Is.GreaterThan(0f));
            Assert.That(expectedMove.y, Is.GreaterThan(0f));
            Assert.That(manager.Published[move].X, Is.EqualTo(expectedMove.x));
            Assert.That(manager.Published[move].Y, Is.EqualTo(expectedMove.y));
            var span = manager.Published;
            manager.SetInteractionState(SceneState.PreUnloading);
            Assert.That(span[attack].X, Is.Zero);
            manager.SetInteractionState(SceneState.Stable);
            Assert.That(span[attack].X, Is.Zero);
            manager.Sample();
            Assert.That(manager.TrySetMap((InputControlMap)99), Is.False);
            Assert.That(span[attack].X, Is.EqualTo(1f));
            manager.TrySetMap(InputControlMap.UI);
            Assert.That(span[attack].X, Is.Zero);
            Assert.That(_asset.FindActionMap("Player").enabled, Is.False);
            Assert.That(_asset.FindActionMap("UI").enabled, Is.True);
            HoldControls();
            manager.Sample();
            Assert.That(span[submit].X, Is.EqualTo(1f));
            manager.Dispose();
            Assert.That(span[submit].X, Is.Zero);
            Assert.That(_asset != null, Is.True);
            Assert.That(_asset.FindActionMap("Player").enabled, Is.False);
            Assert.That(_asset.FindActionMap("UI").enabled, Is.False);
        }

        [Test]
        public void MissingMap_LeavesExistingNativeEnableStateUnchanged()
        {
            _asset.RemoveActionMap(_asset.FindActionMap("UI"));
            var player = _asset.FindActionMap("Player");
            player.Enable();
            Assert.Throws<InvalidOperationException>(() => new InputManager(_asset));
            Assert.That(player.enabled, Is.True);
        }

        [TestCase(SceneState.Stable)]
        [TestCase(SceneState.Loading)]
        public void RealReader_SteadySampleReadAndSpanAllocateZero(SceneState state)
        {
            _manager = new InputManager(_asset);
            var manager = _manager;
            HoldControls();
            manager.SetInteractionState(state);
            manager.TryGetActionIndex("Attack", InputControlMap.Player, out var attack);
            for (var i = 0; i < 256; i++) Exercise(manager, attack);
            var expected = state == SceneState.Stable ? 1f : 0f;
            Assert.That(Exercise(manager, attack), Is.EqualTo(expected * 2f));
            const ProfilerRecorderOptions options =
                ProfilerRecorderOptions.SumAllSamplesInFrame |
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread;
            using var recorder = new ProfilerRecorder(ProfilerCategory.Internal, "GC.Alloc", 1, options);
            Assert.That(recorder.Valid, Is.True);
            recorder.Start();
            try
            {
                var positiveControl = new byte[4096];
                GC.KeepAlive(positiveControl);
            }
            finally
            {
                recorder.Stop();
            }
            var positiveEvents = recorder.Count == 0 ? 0L : recorder.GetSample(0).Count;
            TestContext.WriteLine($"GC.Alloc unit={recorder.UnitType} positiveEvents={positiveEvents}");
            Assert.That(positiveEvents, Is.GreaterThan(0L));
            recorder.Reset();
            Assert.That(recorder.Count, Is.Zero);
            var sum = 0f;
            recorder.Start();
            try
            {
                for (var i = 0; i < 1000; i++) sum += Exercise(manager, attack);
            }
            finally
            {
                recorder.Stop();
            }
            var targetEvents = recorder.Count == 0 ? 0L : recorder.GetSample(0).Count;
            TestContext.WriteLine($"native {state}: unit={recorder.UnitType}, positiveEvents={positiveEvents}, targetEvents={targetEvents}, samples=1000, sum={sum}");
            Assert.That(targetEvents, Is.Zero);
            Assert.That(sum, Is.EqualTo(expected * 2000f));
            Assert.That(Exercise(manager, attack), Is.EqualTo(expected * 2f));
        }

        private static float Exercise(InputManager manager, int index)
        {
            manager.Sample();
            manager.TryRead(index, out var value);
            return value.X + manager.Published[index].X;
        }
    }
}
