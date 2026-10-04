#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using OneStarMaker.Foundation.UpdateSystem;
using OneStarMaker.Foundation.UpdateSystem.World;
using OneStarMaker.Runtime.InputSystem;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.UpdateSystem.Api;
using R3;

namespace OneStarMaker.Tests.InputSystem
{
    /// <summary>
    /// マネージャの公開、マップ通知、Update 順を、デバイス無しのリーダーで検証する。
    /// アセットの ReadValue 経路は Unity がこのアセンブリをコンパイルできる環境の判定で確認する。
    /// </summary>
    [TestFixture]
    public sealed class InputManagerTests
    {
        [Test]
        public void Stable_PublishesActiveMap_AndDropsInactiveValues()
        {
            var reader = new FillReader();
            using var manager = new InputManager(reader, SampleSlots());
            manager.SetInteractionState(SceneState.Stable);

            manager.Sample();

            Assert.That(reader.Calls, Is.EqualTo(1));
            Assert.That(manager.TryRead(0, out var move), Is.True);
            Assert.That(move.X, Is.EqualTo(4f));
            Assert.That(move.Map, Is.EqualTo(InputControlMap.Player));
            Assert.That(move.Kind, Is.EqualTo(InputActionValueKind.Axis2));
            Assert.That(move.Index, Is.EqualTo(0));
            Assert.That(manager.TryRead(2, out var navigate), Is.True);
            Assert.That(navigate.X, Is.EqualTo(0f));
            Assert.That(navigate.Y, Is.EqualTo(0f));
            Assert.That(navigate.Map, Is.EqualTo(InputControlMap.UI));
        }

        [Test]
        public void NotStable_RewritesNeutralWithoutReadingTheDevice()
        {
            var reader = new FillReader();
            using var manager = new InputManager(reader, SampleSlots());
            manager.SetInteractionState(SceneState.Stable);
            manager.Sample();
            manager.SetInteractionState(SceneState.Loading);

            manager.Sample();

            Assert.That(reader.Calls, Is.EqualTo(1));
            Assert.That(manager.TryRead(0, out var move), Is.True);
            Assert.That(move.X, Is.EqualTo(0f));
            Assert.That(move.Y, Is.EqualTo(0f));
            Assert.That(move.Index, Is.EqualTo(0));
            Assert.That(move.Kind, Is.EqualTo(InputActionValueKind.Axis2));
        }

        [Test]
        public void ReturnToStable_PublishesTheNewLevel_NotThePreviousOne()
        {
            var reader = new FillReader();
            using var manager = new InputManager(reader, SampleSlots());
            manager.SetInteractionState(SceneState.Stable);
            manager.Sample();
            manager.SetInteractionState(SceneState.PreUnloading);
            manager.Sample();
            reader.X = 8f;
            manager.SetInteractionState(SceneState.Stable);

            manager.Sample();

            Assert.That(manager.TryRead(0, out var move), Is.True);
            Assert.That(move.X, Is.EqualTo(8f));
        }

        [Test]
        public void UndefinedSceneState_IsNotAccepting()
        {
            var reader = new FillReader();
            using var manager = new InputManager(reader, SampleSlots());
            manager.SetInteractionState((SceneState)99);

            manager.Sample();

            Assert.That(reader.Calls, Is.EqualTo(0));
            Assert.That(manager.TryRead(0, out var move), Is.True);
            Assert.That(move.X, Is.EqualTo(0f));
        }

        [Test]
        public void MapChange_NotifiesOnce_AndPublishesTheNewlyActiveMap()
        {
            var reader = new FillReader();
            using var manager = new InputManager(reader, SampleSlots());
            var seen = new List<InputControlMap>();
            using var subscription = manager.MapChanged.Subscribe(seen.Add);
            manager.SetInteractionState(SceneState.Stable);

            Assert.That(manager.TrySetMap(InputControlMap.UI), Is.True);
            Assert.That(manager.TrySetMap(InputControlMap.UI), Is.True);
            manager.Sample();

            Assert.That(seen, Is.EqualTo(new[] { InputControlMap.UI }));
            Assert.That(manager.ActiveMap, Is.EqualTo(InputControlMap.UI));
            Assert.That(manager.TryRead(2, out var navigate), Is.True);
            Assert.That(navigate.X, Is.EqualTo(4f));
            Assert.That(manager.TryRead(0, out var move), Is.True);
            Assert.That(move.X, Is.EqualTo(0f));
        }

        [Test]
        public void UnknownMap_AndUnknownProfile_LeaveTheCurrentSelection()
        {
            using var manager = new InputManager(new FillReader(), SampleSlots());

            Assert.That(manager.TrySetMap((InputControlMap)9), Is.False);
            Assert.That(manager.ActiveMap, Is.EqualTo(InputControlMap.Player));
            Assert.That(manager.TrySelectProfile(new InputProfileId("one-handed")), Is.False);
            Assert.That(manager.TrySelectProfile(InputProfileId.Default), Is.True);
            Assert.That(manager.ActiveProfile.Value, Is.EqualTo(InputProfileId.DefaultValue));
        }

        [Test]
        public void TryRegister_SamplesBeforeTheDefaultGameplayOrder()
        {
            var reader = new FillReader();
            using var manager = new InputManager(reader, SampleSlots());
            manager.SetInteractionState(SceneState.Stable);
            var coordinator = new UpdateCoordinator();
            var log = new List<string>();
            var probe = new ProbeElement(log, () => reader.Calls > 0 ? "after-input" : "before-input");

            Assert.That(manager.TryRegister(coordinator), Is.True);
            Assert.That(manager.TryRegister(coordinator), Is.False);
            Assert.That(
                coordinator.RegisterElement("Gameplay", probe, layerOrder: 0, executionOrder: 0),
                Is.True);
            coordinator.ActivatePendingRegistrations();
            coordinator.RunUpdate(0.016f, 0.016f);

            Assert.That(log, Is.EqualTo(new[] { "after-input" }));
            Assert.That(UpdateLayerIds.InputLayerOrder, Is.LessThan(0));
            Assert.That(UpdateLayerIds.InputLayerOrder, Is.LessThan(UpdateLayerIds.CameraLayerOrder));
        }

        [Test]
        public void Sample_DoesNotAllocate_OnTheStableOrNeutralPath()
        {
            var reader = new FillReader();
            using var manager = new InputManager(reader, SampleSlots());
            manager.SetInteractionState(SceneState.Stable);
            manager.Sample();
            manager.SetInteractionState(SceneState.Loading);
            manager.Sample();
            manager.SetInteractionState(SceneState.Stable);

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++)
            {
                manager.Sample();
                manager.SetInteractionState(SceneState.Loading);
                manager.Sample();
                manager.SetInteractionState(SceneState.Stable);
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.EqualTo(0));
        }

        [Test]
        public void Dispose_IsIdempotent_AndRejectsFurtherSamples()
        {
            var manager = new InputManager(new FillReader(), SampleSlots());
            manager.Dispose();
            manager.Dispose();

            Assert.Throws<ObjectDisposedException>(() => manager.Sample());
        }

        private static InputActionSlot[] SampleSlots()
        {
            return new[]
            {
                new InputActionSlot(0, InputControlMap.Player, InputActionValueKind.Axis2, "Move"),
                new InputActionSlot(1, InputControlMap.Player, InputActionValueKind.Button, "Attack"),
                new InputActionSlot(2, InputControlMap.UI, InputActionValueKind.Axis2, "Navigate"),
                new InputActionSlot(3, InputControlMap.UI, InputActionValueKind.Button, "Submit"),
            };
        }

        private sealed class FillReader : IInputDeviceReader
        {
            public int Calls;
            public float X = 4f;

            public void Read(Span<InputActionValue> destination)
            {
                Calls++;
                for (var i = 0; i < destination.Length; i++)
                {
                    destination[i] = new InputActionValue(
                        99,
                        InputControlMap.UI,
                        InputActionValueKind.Button,
                        X,
                        7f);
                }
            }
        }

        private sealed class ProbeElement : IUpdateElement
        {
            private readonly List<string> _log;
            private readonly Func<string> _read;

            public ProbeElement(List<string> log, Func<string> read)
            {
                _log = log;
                _read = read;
            }

            public void OnElementStart()
            {
            }

            public void OnElementUpdate(in UpdateFrameContext context)
            {
                _log.Add(_read());
            }

            public void OnElementLateUpdate(in UpdateFrameContext context)
            {
            }
        }
    }
}
