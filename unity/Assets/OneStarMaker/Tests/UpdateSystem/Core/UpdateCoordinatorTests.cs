#nullable enable

using System;
using NUnit.Framework;
using OneStarMaker.Foundation.UpdateSystem;
using OneStarMaker.Foundation.UpdateSystem.Configuration;
using OneStarMaker.Foundation.UpdateSystem.World;

namespace OneStarMaker.Tests.UpdateSystem
{
    /// <summary>
    /// UpdateCoordinator の実行順序・構造変更・例外伝播の契約を検証する。
    ///
    /// <para>
    /// 主張している軸は 4 つ:
    /// </para>
    /// <list type="number">
    /// <item><description>
    /// 実行順序 — layerOrder → executionOrder → 登録順の三段で決まり、
    /// 同一 layer 内では native が managed より先に走る。
    /// </description></item>
    /// <item><description>
    /// 遅延反映 — Update / start 中の登録・解除はそのフレームには効かず、
    /// ActivatePendingRegistrations / ApplyStructuralChanges を跨いで初めて反映される。
    /// </description></item>
    /// <item><description>
    /// 例外伝播 — Element が投げた例外は握り潰さずフレームを中断して伝播し、
    /// それでも registry と native state を壊れた状態で残さない。
    /// </description></item>
    /// <item><description>
    /// mainThread apply — 要求は畳まれて 1 回だけ適用され、解除済み Element には届かない。
    /// </description></item>
    /// </list>
    ///
    /// <para>
    /// ただしここで検証しているのは Coordinator の個々の API であって、
    /// PlayerLoop 上での呼び順ではない。1 フレームの順序
    /// （ActivatePendingRegistrations → RunUpdate → RunLateUpdate →
    /// ApplyMainThreadChanges → ApplyStructuralChanges）を実装しているのは
    /// <c>UpdaterDriver</c> 側であり、そちらを直接守るテストは存在しない。
    /// </para>
    /// </summary>
    [TestFixture]
    public class UpdateCoordinatorTests
    {
        private UpdateCoordinator _coordinator = null!;

        [SetUp]
        public void SetUp()
        {
            _coordinator = new UpdateCoordinator();
        }

        [Test]
        public void GetOrCreateUpdateLayer_返したlayerのtimeScaleがdeltaTimeへ反映される()
        {
            var world = new UpdateCoordinator();
            var layer = world.GetOrCreateUpdateLayer("Gameplay");
            var element = new RecordingElement();

            layer.SetTimeScale(0.5f);
            world.RegisterElement("Gameplay", element);
            world.ActivatePendingRegistrations();
            world.RunUpdate(2f, 3f);

            Assert.That(layer, Is.TypeOf<OneStarMaker.Foundation.UpdateSystem.Layers.UpdateLayer>());
            Assert.AreEqual(1f, element.LastUpdateContext.DeltaTime);
            Assert.AreEqual(3f, element.LastUpdateContext.UnscaledDeltaTime);
        }

        [Test]
        public void ExecutionConfigurationCommand_Register時にlayerIdがnullなら例外()
        {
            Assert.Throws<ArgumentException>(
                () => new ExecutionConfigurationCommand(
                    ExecutionConfigurationCommandKind.Register,
                    new UpdateHandle(1, 2),
                    layerId: null));
        }

        [Test]
        public void ExecutionConfigurationQueue_未定義のcommand種別をEnqueueすると例外()
        {
            var queue = new ExecutionConfigurationQueue();
            var invalid = new ExecutionConfigurationCommand(
                (ExecutionConfigurationCommandKind)999,
                new UpdateHandle(1, 2));

            Assert.Throws<ArgumentOutOfRangeException>(() => queue.Enqueue(invalid));
        }

        [Test]
        public void ExecutionConfigurationCommand_Register時にhandleがInvalidなら例外()
        {
            Assert.Throws<ArgumentException>(
                () => new ExecutionConfigurationCommand(
                    ExecutionConfigurationCommandKind.Register,
                    UpdateHandle.Invalid,
                    layerId: "Gameplay"));
        }

        [Test]
        public void RegisterElement_Activate後の初回Updateでstartとupdateが順に走る()
        {
            var element = new RecordingElement();

            _coordinator.RegisterElement("Gameplay", element);
            _coordinator.ActivatePendingRegistrations();
            _coordinator.RunUpdate(0.5f, 1f);

            CollectionAssert.AreEqual(
                new[] { "start", "update" },
                element.Events);
            Assert.AreEqual(0.5f, element.LastUpdateContext.DeltaTime);
            Assert.AreEqual(1f, element.LastUpdateContext.UnscaledDeltaTime);
        }

        [Test]
        public void RunUpdate_LayerTimeScale適用時はdeltaTimeのみscaleされunscaledは素通しになる()
        {
            var element = new RecordingElement();
            var layer = _coordinator.GetOrCreateLayer("Gameplay");
            layer.SetTimeScale(0.25f);

            _coordinator.RegisterElement("Gameplay", element);
            _coordinator.ActivatePendingRegistrations();
            _coordinator.RunUpdate(4f, 10f);

            Assert.AreEqual(1f, element.LastUpdateContext.DeltaTime);
            Assert.AreEqual(10f, element.LastUpdateContext.UnscaledDeltaTime);
            Assert.AreEqual(0.25f, element.LastUpdateContext.TimeScale);
        }

        [Test]
        public void RunUpdate_LayerがpausedならstartのみでupdateもlateUpdateも走らない()
        {
            var element = new RecordingElement();
            var layer = _coordinator.GetOrCreateLayer("Gameplay");
            layer.IsPaused = true;

            _coordinator.RegisterElement("Gameplay", element);
            _coordinator.ActivatePendingRegistrations();
            _coordinator.RunUpdate(1f, 1f);
            _coordinator.RunLateUpdate(1f, 1f);

            CollectionAssert.AreEqual(new[] { "start" }, element.Events);
        }

        [Test]
        public void RegisterElement_Update中の登録は次フレームのActivate後に開始する()
        {
            var lateRegistrant = new RecordingElement();
            var first = new RecordingElement
            {
                OnUpdateAction = _ => _coordinator.RegisterElement("Gameplay", lateRegistrant)
            };

            _coordinator.RegisterElement("Gameplay", first);
            _coordinator.ActivatePendingRegistrations();
            _coordinator.RunUpdate(1f, 1f);
            _coordinator.RunLateUpdate(1f, 1f);
            _coordinator.ApplyStructuralChanges();

            CollectionAssert.AreEqual(
                new[] { "start", "update", "late-update" },
                first.Events);
            Assert.IsEmpty(lateRegistrant.Events);

            _coordinator.ActivatePendingRegistrations();
            _coordinator.RunUpdate(1f, 1f);

            CollectionAssert.AreEqual(
                new[] { "start", "update" },
                lateRegistrant.Events);
        }

        [Test]
        public void UnregisterElement_Update中の自己解除でもそのフレームのlateUpdateまでは走る()
        {
            RecordingElement? element = null;
            element = new RecordingElement
            {
                OnUpdateAction = _ => _coordinator.UnregisterElement(element!)
            };

            _coordinator.RegisterElement("Gameplay", element);
            _coordinator.ActivatePendingRegistrations();
            _coordinator.RunUpdate(1f, 1f);
            _coordinator.RunLateUpdate(1f, 1f);
            _coordinator.ApplyStructuralChanges();
            _coordinator.RunUpdate(1f, 1f);

            CollectionAssert.AreEqual(
                new[] { "start", "update", "late-update" },
                element.Events);
        }

        [Test]
        public void RegisterElement_既存layerへ異なるlayerOrderを指定すると例外()
        {
            var first = new RecordingElement();
            var second = new RecordingElement();

            _coordinator.RegisterElement("Gameplay", first, layerOrder: 10);
            Assert.Throws<InvalidOperationException>(
                () => _coordinator.RegisterElement("Gameplay", second, layerOrder: 20));
        }

        [Test]
        public void RunUpdate_executionOrderの昇順で実行される()
        {
            var executionLog = new System.Collections.Generic.List<string>();
            var second = new RecordingElement("second", executionLog);
            var first = new RecordingElement("first", executionLog);

            _coordinator.RegisterElement("Gameplay", second, executionOrder: 20);
            _coordinator.RegisterElement("Gameplay", first, executionOrder: 10);
            _coordinator.ActivatePendingRegistrations();
            _coordinator.RunUpdate(1f, 1f);

            CollectionAssert.AreEqual(
                new[] { "first:update", "second:update" },
                executionLog);
        }

        [Test]
        public void RunUpdate_executionOrderが同値なら登録順を保つ()
        {
            var executionLog = new System.Collections.Generic.List<string>();
            var first = new RecordingElement("first", executionLog);
            var second = new RecordingElement("second", executionLog);

            _coordinator.RegisterElement("Gameplay", first, executionOrder: 10);
            _coordinator.RegisterElement("Gameplay", second, executionOrder: 10);
            _coordinator.ActivatePendingRegistrations();
            _coordinator.RunUpdate(1f, 1f);

            CollectionAssert.AreEqual(
                new[] { "first:update", "second:update" },
                executionLog);
        }

        [Test]
        public void RunUpdate_layerOrderの昇順でlayerをまたいで実行される()
        {
            var executionLog = new System.Collections.Generic.List<string>();
            var lateLayer = new RecordingElement("late-layer", executionLog);
            var earlyLayer = new RecordingElement("early-layer", executionLog);

            _coordinator.RegisterElement("LateGameplay", lateLayer, layerOrder: 20);
            _coordinator.RegisterElement("EarlyGameplay", earlyLayer, layerOrder: 10);
            _coordinator.ActivatePendingRegistrations();
            _coordinator.RunUpdate(1f, 1f);

            CollectionAssert.AreEqual(
                new[] { "early-layer:update", "late-layer:update" },
                executionLog);
        }

        [Test]
        public void UnregisterElement_構造変更適用後の次フレームから実行対象外になる()
        {
            var executionLog = new System.Collections.Generic.List<string>();
            var first = new RecordingElement("first", executionLog);
            RecordingElement? second = null;
            second = new RecordingElement("second", executionLog)
            {
                OnUpdateAction = _ => _coordinator.UnregisterElement(second!)
            };
            var third = new RecordingElement("third", executionLog);

            _coordinator.RegisterElement("Gameplay", first, executionOrder: 10);
            _coordinator.RegisterElement("Gameplay", second, executionOrder: 20);
            _coordinator.RegisterElement("Gameplay", third, executionOrder: 30);
            _coordinator.ActivatePendingRegistrations();
            _coordinator.RunUpdate(1f, 1f);
            _coordinator.RunLateUpdate(1f, 1f);
            _coordinator.ApplyStructuralChanges();

            executionLog.Clear();
            _coordinator.RunUpdate(1f, 1f);

            CollectionAssert.AreEqual(
                new[] { "first:update", "third:update" },
                executionLog);
        }

        [Test]
        public void RegisterElement_解除して別executionOrderで再登録すると新しい順序が効く()
        {
            var executionLog = new System.Collections.Generic.List<string>();
            var first = new RecordingElement("first", executionLog);
            RecordingElement? target = null;
            target = new RecordingElement("target", executionLog)
            {
                OnUpdateAction = _ =>
                {
                    var currentTarget = target!;
                    _coordinator.UnregisterElement(currentTarget);
                    _coordinator.RegisterElement("Gameplay", currentTarget, executionOrder: 5);
                    currentTarget.OnUpdateAction = null;
                }
            };

            _coordinator.RegisterElement("Gameplay", first, executionOrder: 10);
            _coordinator.RegisterElement("Gameplay", target, executionOrder: 20);
            _coordinator.ActivatePendingRegistrations();
            _coordinator.RunUpdate(1f, 1f);
            _coordinator.RunLateUpdate(1f, 1f);
            _coordinator.ApplyStructuralChanges();

            executionLog.Clear();
            _coordinator.RunUpdate(1f, 1f);

            CollectionAssert.AreEqual(
                new[] { "target:update", "first:update" },
                executionLog);
        }

        [Test]
        public void ActivatePendingRegistrations_start例外は伝播し失敗Elementはupdate対象にならない()
        {
            var survivor = new RecordingElement();
            var failing = new RecordingElement
            {
                OnStartAction = () => throw new InvalidOperationException("start failed")
            };

            _coordinator.RegisterElement("Gameplay", failing, executionOrder: 10);
            _coordinator.RegisterElement("Gameplay", survivor, executionOrder: 20);

            Assert.Throws<InvalidOperationException>(() => _coordinator.ActivatePendingRegistrations());

            failing.OnStartAction = null;
            _coordinator.ActivatePendingRegistrations();
            _coordinator.RunUpdate(1f, 1f);

            CollectionAssert.AreEqual(new[] { "start", "update" }, survivor.Events);
            CollectionAssert.DoesNotContain(failing.Events, "update");
        }

        [Test]
        public void RunUpdate_update例外はフレームを中断して伝播し次フレームで再開する()
        {
            var survivor = new RecordingElement();
            var failing = new RecordingElement
            {
                OnUpdateAction = _ => throw new InvalidOperationException("update failed")
            };

            _coordinator.RegisterElement("Gameplay", failing, executionOrder: 10);
            _coordinator.RegisterElement("Gameplay", survivor, executionOrder: 20);
            _coordinator.ActivatePendingRegistrations();

            Assert.Throws<InvalidOperationException>(() => _coordinator.RunUpdate(1f, 1f));

            failing.OnUpdateAction = null;
            _coordinator.RunUpdate(1f, 1f);

            Assert.AreEqual(2, failing.Events.FindAll(e => e == "update").Count);
            Assert.AreEqual(1, survivor.Events.FindAll(e => e == "update").Count);
        }

        [Test]
        public void RunLateUpdate_lateUpdate例外はフレームを中断して伝播し次フレームで再開する()
        {
            var survivor = new RecordingElement();
            var failing = new RecordingElement
            {
                OnLateUpdateAction = _ => throw new InvalidOperationException("late update failed")
            };

            _coordinator.RegisterElement("Gameplay", failing, executionOrder: 10);
            _coordinator.RegisterElement("Gameplay", survivor, executionOrder: 20);
            _coordinator.ActivatePendingRegistrations();

            Assert.Throws<InvalidOperationException>(() => _coordinator.RunLateUpdate(1f, 1f));

            failing.OnLateUpdateAction = null;
            _coordinator.RunLateUpdate(1f, 1f);

            Assert.AreEqual(2, failing.Events.FindAll(e => e == "late-update").Count);
            Assert.AreEqual(1, survivor.Events.FindAll(e => e == "late-update").Count);
        }

        [Test]
        public void RunUpdate_backendへUpdateとLateUpdateのphaseが順に渡る()
        {
            var backend = new RecordingBackend();
            var world = new UpdateCoordinator(backend);
            var element = new RecordingElement();

            world.RegisterElement("Gameplay", element);
            world.ActivatePendingRegistrations();
            world.RunUpdate(2f, 3f);
            world.RunLateUpdate(2f, 3f);

            CollectionAssert.AreEqual(
                new[] { UpdateExecutionPhase.Update, UpdateExecutionPhase.LateUpdate },
                backend.Phases);
            CollectionAssert.AreEqual(
                new[] { "start", "update", "late-update" },
                element.Events);
            Assert.AreEqual(1, backend.ElementsPerDispatch[0]);
            Assert.AreEqual(1, backend.ElementsPerDispatch[1]);
        }

        [Test]
        public void RequestUnregister_構造変更を適用するまでは当該フレームの実行を継続する()
        {
            var element = new RecordingElement();

            _coordinator.RegisterElement("Gameplay", element);
            _coordinator.ActivatePendingRegistrations();
            Assert.That(_coordinator.TryGetHandle(element, out var handle), Is.True);

            _coordinator.RequestUnregister(handle);
            _coordinator.RunUpdate(1f, 1f);
            _coordinator.RunLateUpdate(1f, 1f);
            _coordinator.ApplyStructuralChanges();

            _coordinator.RunUpdate(1f, 1f);

            CollectionAssert.AreEqual(
                new[] { "start", "update", "late-update" },
                element.Events);
        }

        [Test]
        public void RequestReorder_mirrorの並べ替えがnativeRegistryのexecutionOrderへ伝わる()
        {
            using var registry = new NativeStateRegistry<NativeTestState>();
            var world = new UpdateCoordinator();

            var mirrorHandle = world.RegisterNative(
                registry,
                new LoggingNativeBackend("native", new System.Collections.Generic.List<string>()),
                new RecordingElement(),
                new NativeTestState { Value = 10 },
                out var nativeHandle,
                executionOrder: 20);

            world.RequestReorder(mirrorHandle, 5);
            world.ApplyStructuralChanges();

            Assert.That(registry.TryGetExecutionOrder(nativeHandle, out var executionOrder), Is.True);
            Assert.That(executionOrder, Is.EqualTo(5));
        }

        [Test]
        public void RequestReorder_未知のhandleは無視され構造変更は0件になる()
        {
            Assert.DoesNotThrow(() => _coordinator.RequestReorder(new UpdateHandle(999, 1), 5));
            Assert.That(_coordinator.ApplyStructuralChanges(), Is.EqualTo(0));
        }

        [Test]
        public void ApplyMainThreadChanges_enqueueしたcommandが1回適用される()
        {
            var command = new RecordingApplyCommand();

            _coordinator.EnqueueMainThreadApply(command);
            var appliedCount = _coordinator.ApplyMainThreadChanges();

            Assert.AreEqual(1, appliedCount);
            Assert.AreEqual(1, command.ApplyCount);
        }

        [Test]
        public void ApplyMainThreadChanges_command例外が伝播した後もqueueは再適用されない()
        {
            var first = new RecordingApplyCommand();
            var failing = new ThrowingApplyCommand();

            _coordinator.EnqueueMainThreadApply(first);
            _coordinator.EnqueueMainThreadApply(failing);

            Assert.Throws<InvalidOperationException>(() => _coordinator.ApplyMainThreadChanges());

            Assert.AreEqual(1, first.ApplyCount);
            Assert.AreEqual(0, _coordinator.ApplyMainThreadChanges());
            Assert.AreEqual(1, first.ApplyCount);
        }

        [Test]
        public void RequestElementApply_handle指定でElementのmainThreadApplyが呼ばれる()
        {
            var element = new RecordingElement();

            _coordinator.RegisterElement("Gameplay", element);
            _coordinator.ActivatePendingRegistrations();
            Assert.That(_coordinator.TryGetHandle(element, out var handle), Is.True);

            _coordinator.RequestElementApply(handle);
            var appliedCount = _coordinator.ApplyMainThreadChanges();

            Assert.AreEqual(1, appliedCount);
            Assert.AreEqual(1, element.MainThreadApplyCount);
            Assert.That(element.LastAppliedHandle, Is.EqualTo(handle));
        }

        [Test]
        public void RequestElementApply_未登録Elementはfalseを返す()
        {
            var element = new RecordingElement();

            Assert.That(_coordinator.RequestElementApply(element), Is.False);
        }

        [Test]
        public void RequestElementApply_同一Elementへの重複要求は1回に畳まれる()
        {
            var element = new RecordingElement();

            _coordinator.RegisterElement("Gameplay", element);
            _coordinator.ActivatePendingRegistrations();
            Assert.That(_coordinator.TryGetHandle(element, out var handle), Is.True);

            _coordinator.RequestElementApply(handle);
            _coordinator.RequestElementApply(handle);
            Assert.That(_coordinator.RequestElementApply(element), Is.True);

            var appliedCount = _coordinator.ApplyMainThreadChanges();

            Assert.AreEqual(1, appliedCount);
            Assert.AreEqual(1, element.MainThreadApplyCount);
        }

        [Test]
        public void RequestElementApply_解除済みElementへの要求は適用されない()
        {
            var element = new RecordingElement();

            _coordinator.RegisterElement("Gameplay", element);
            _coordinator.ActivatePendingRegistrations();
            Assert.That(_coordinator.TryGetHandle(element, out var handle), Is.True);

            Assert.That(_coordinator.UnregisterElement(element), Is.True);
            Assert.That(_coordinator.ApplyStructuralChanges(), Is.EqualTo(1));

            _coordinator.RequestElementApply(handle);

            Assert.That(_coordinator.ApplyMainThreadChanges(), Is.EqualTo(0));
            Assert.AreEqual(0, element.MainThreadApplyCount);
        }

        [Test]
        public void RegisterElement_layerOrder競合で例外になったElementはhandleを持たない()
        {
            var first = new RecordingElement();
            var second = new RecordingElement();

            _coordinator.RegisterElement("Gameplay", first, layerOrder: 10);

            Assert.Throws<InvalidOperationException>(
                () => _coordinator.RegisterElement("Gameplay", second, layerOrder: 20));
            Assert.That(_coordinator.TryGetHandle(second, out _), Is.False);
        }

        [Test]
        public void RegisterElement_同一Elementを別layerへ登録すると既存layer名付きで例外()
        {
            var element = new RecordingElement();

            _coordinator.RegisterElement("Gameplay", element, layerOrder: 10);

            var ex = Assert.Throws<InvalidOperationException>(
                () => _coordinator.RegisterElement("Ui", element, layerOrder: 20));
            Assert.That(ex!.Message, Does.Contain("Gameplay"));
        }

        [Test]
        public void RegisterElement_start中の登録は次フレームのActivate後に開始する()
        {
            var lateRegistrant = new RecordingElement();
            var first = new RecordingElement
            {
                OnStartAction = () => _coordinator.RegisterElement("Gameplay", lateRegistrant, executionOrder: -10)
            };

            _coordinator.RegisterElement("Gameplay", first, executionOrder: 10);

            _coordinator.ActivatePendingRegistrations();
            _coordinator.RunUpdate(1f, 1f);

            CollectionAssert.AreEqual(new[] { "start", "update" }, first.Events);
            Assert.IsEmpty(lateRegistrant.Events);

            _coordinator.ActivatePendingRegistrations();
            _coordinator.RunUpdate(1f, 1f);

            CollectionAssert.AreEqual(new[] { "start", "update" }, lateRegistrant.Events);
        }

        [Test]
        public void RegisterNative_RunUpdateでnativeStateが更新されmirrorへapplyされる()
        {
            using var registry = new NativeStateRegistry<NativeTestState>();
            var backend = new JobSystemUpdateProcessorBackend<NativeTestState, NativeIncrementProcessor>(new NativeIncrementProcessor());
            var world = new UpdateCoordinator();
            var mirror = new RecordingElement();

            var mirrorHandle = world.RegisterNative(
                registry,
                backend,
                mirror,
                new NativeTestState { Value = 10 },
                out var nativeHandle);

            world.RunUpdate(2f, 2f);
            var appliedCount = world.ApplyMainThreadChanges();

            Assert.That(mirrorHandle, Is.Not.EqualTo(UpdateHandle.Invalid));
            Assert.That(nativeHandle, Is.Not.EqualTo(UpdateHandle.Invalid));
            Assert.That(appliedCount, Is.EqualTo(1));
            Assert.That(mirror.MainThreadApplyCount, Is.EqualTo(1));
            Assert.That(mirror.LastAppliedHandle, Is.EqualTo(mirrorHandle));
            Assert.That(registry.TryGetState(nativeHandle, out var state), Is.True);
            Assert.That(state.Value, Is.EqualTo(12));
            Assert.That(registry.IsDirty(nativeHandle), Is.False);
        }

        [Test]
        public void UnregisterElement_mirror解除でnativeRegistryのentryも削除される()
        {
            using var registry = new NativeStateRegistry<NativeTestState>();
            var backend = new JobSystemUpdateProcessorBackend<NativeTestState, NativeIncrementProcessor>(new NativeIncrementProcessor());
            var world = new UpdateCoordinator();
            var mirror = new RecordingElement();

            var mirrorHandle = world.RegisterNative(
                registry,
                backend,
                mirror,
                new NativeTestState { Value = 10 },
                out var nativeHandle);

            Assert.That(world.UnregisterElement(mirror), Is.True);
            Assert.That(registry.Contains(nativeHandle), Is.False);

            world.RequestElementApply(mirrorHandle);
            Assert.That(world.ApplyMainThreadChanges(), Is.EqualTo(0));
        }

        [Test]
        public void RunUpdate_nativeBackendが例外を投げたときはdirtyが残る()
        {
            using var registry = new NativeStateRegistry<NativeTestState>();
            var backend = new ThrowingNativeBackend();
            var world = new UpdateCoordinator();
            var mirror = new RecordingElement();

            world.RegisterNative(
                registry,
                backend,
                mirror,
                new NativeTestState { Value = 10 },
                out var nativeHandle);

            Assert.Throws<InvalidOperationException>(() => world.RunUpdate(1f, 1f));
            Assert.That(registry.IsDirty(nativeHandle), Is.True);
            Assert.DoesNotThrow(() => registry.ClearDirty(nativeHandle));
        }

        [Test]
        public void RegisterNative_layerのtimeScaleがnative処理のdeltaTimeにも効く()
        {
            using var registry = new NativeStateRegistry<NativeTestState>();
            var backend = new JobSystemUpdateProcessorBackend<NativeTestState, NativeIncrementProcessor>(new NativeIncrementProcessor());
            var world = new UpdateCoordinator();
            var mirror = new RecordingElement();
            world.GetOrCreateLayer("Gameplay").SetTimeScale(0.5f);

            world.RegisterNative(
                registry,
                backend,
                mirror,
                new NativeTestState { Value = 10 },
                out var nativeHandle,
                layerId: "Gameplay");

            world.RunUpdate(4f, 4f);

            Assert.That(registry.TryGetState(nativeHandle, out var state), Is.True);
            Assert.That(state.Value, Is.EqualTo(12));
        }

        [Test]
        public void RegisterNative_layerがpausedならnative処理もapplyも走らない()
        {
            using var registry = new NativeStateRegistry<NativeTestState>();
            var backend = new JobSystemUpdateProcessorBackend<NativeTestState, NativeIncrementProcessor>(new NativeIncrementProcessor());
            var world = new UpdateCoordinator();
            var mirror = new RecordingElement();
            world.GetOrCreateLayer("Gameplay").IsPaused = true;

            world.RegisterNative(
                registry,
                backend,
                mirror,
                new NativeTestState { Value = 10 },
                out var nativeHandle,
                layerId: "Gameplay");

            world.RunUpdate(4f, 4f);

            Assert.That(registry.TryGetState(nativeHandle, out var state), Is.True);
            Assert.That(state.Value, Is.EqualTo(10));
            Assert.That(world.ApplyMainThreadChanges(), Is.EqualTo(0));
        }

        [Test]
        public void RegisterNative_layerOrderの昇順でnativePipelineが実行される()
        {
            using var earlyRegistry = new NativeStateRegistry<NativeTestState>();
            using var lateRegistry = new NativeStateRegistry<NativeTestState>();
            var executionLog = new System.Collections.Generic.List<string>();
            var world = new UpdateCoordinator();

            world.RegisterNative(
                earlyRegistry,
                new LoggingNativeBackend("early-native", executionLog),
                new RecordingElement(),
                new NativeTestState { Value = 1 },
                out _,
                layerId: "EarlyGameplay",
                layerOrder: 10);

            world.RegisterNative(
                lateRegistry,
                new LoggingNativeBackend("late-native", executionLog),
                new RecordingElement(),
                new NativeTestState { Value = 1 },
                out _,
                layerId: "LateGameplay",
                layerOrder: 20);

            world.RunUpdate(1f, 1f);

            CollectionAssert.AreEqual(
                new[] { "early-native:update", "late-native:update" },
                executionLog);
        }

        [Test]
        public void RunUpdate_同一layerではnativeがmanagedより先に実行される()
        {
            using var registry = new NativeStateRegistry<NativeTestState>();
            var executionLog = new System.Collections.Generic.List<string>();
            var world = new UpdateCoordinator();

            world.RegisterElement("Gameplay", new RecordingElement("managed", executionLog));
            world.RegisterNative(
                registry,
                new LoggingNativeBackend("native", executionLog),
                new RecordingElement(),
                new NativeTestState { Value = 1 },
                out _,
                layerId: "Gameplay");

            world.ActivatePendingRegistrations();
            world.RunUpdate(1f, 1f);

            CollectionAssert.AreEqual(
                new[] { "native:update", "managed:update" },
                executionLog);
        }

        [Test]
        public void RegisterNative_同一registryを別layerへ登録すると既存layer名付きで例外()
        {
            using var registry = new NativeStateRegistry<NativeTestState>();
            var world = new UpdateCoordinator();
            var backend = new LoggingNativeBackend("native", new System.Collections.Generic.List<string>());

            world.RegisterNative(
                registry,
                backend,
                new RecordingElement(),
                new NativeTestState { Value = 1 },
                out _,
                layerId: "Gameplay",
                layerOrder: 10);

            var ex = Assert.Throws<InvalidOperationException>(
                () => world.RegisterNative(
                    registry,
                    backend,
                    new RecordingElement(),
                    new NativeTestState { Value = 2 },
                    out _,
                    layerId: "Ui",
                    layerOrder: 20));

            Assert.That(ex!.Message, Does.Contain("Gameplay"));
        }

        [Test]
        public void RegisterNativePipeline_異なるstate型のpipelineが同一layerで併存する()
        {
            using var primaryRegistry = new NativeStateRegistry<NativeTestState>();
            using var secondaryRegistry = new NativeStateRegistry<SecondaryNativeTestState>();
            var world = new UpdateCoordinator();

            var primaryPipelineId = world.RegisterNativePipeline(
                "gameplay.primary",
                primaryRegistry,
                new JobSystemUpdateProcessorBackend<NativeTestState, NativeIncrementProcessor>(new NativeIncrementProcessor()),
                layerId: "Gameplay");

            var secondaryPipelineId = world.RegisterNativePipeline(
                "gameplay.secondary",
                secondaryRegistry,
                new JobSystemUpdateProcessorBackend<SecondaryNativeTestState, SecondaryNativeIncrementProcessor>(new SecondaryNativeIncrementProcessor()),
                layerId: "Gameplay");

            world.RegisterNative(
                primaryPipelineId,
                new RecordingElement(),
                new NativeTestState { Value = 10 },
                out var primaryHandle);

            world.RegisterNative(
                secondaryPipelineId,
                new RecordingElement(),
                new SecondaryNativeTestState { Value = 100 },
                out var secondaryHandle);

            world.RunUpdate(2f, 2f);

            Assert.That(primaryRegistry.TryGetState(primaryHandle, out var primaryState), Is.True);
            Assert.That(primaryState.Value, Is.EqualTo(12));
            Assert.That(secondaryRegistry.TryGetState(secondaryHandle, out var secondaryState), Is.True);
            Assert.That(secondaryState.Value, Is.EqualTo(104));
        }

        [Test]
        public void RegisterNative_pipelineのstate型と異なる型を登録すると型名付きで例外()
        {
            using var registry = new NativeStateRegistry<NativeTestState>();
            var world = new UpdateCoordinator();
            var pipelineId = world.RegisterNativePipeline(
                "gameplay.primary",
                registry,
                new JobSystemUpdateProcessorBackend<NativeTestState, NativeIncrementProcessor>(new NativeIncrementProcessor()),
                layerId: "Gameplay");

            var ex = Assert.Throws<InvalidOperationException>(
                () => world.RegisterNative(
                    pipelineId,
                    new RecordingElement(),
                    new SecondaryNativeTestState { Value = 1 },
                    out _));

            Assert.That(ex!.Message, Does.Contain(nameof(NativeTestState)));
        }

        [Test]
        public void RegisterNativePipeline_同名pipelineIdの二重登録はid付きで例外()
        {
            using var firstRegistry = new NativeStateRegistry<NativeTestState>();
            using var secondRegistry = new NativeStateRegistry<SecondaryNativeTestState>();
            var world = new UpdateCoordinator();

            world.RegisterNativePipeline(
                "gameplay.shared",
                firstRegistry,
                new JobSystemUpdateProcessorBackend<NativeTestState, NativeIncrementProcessor>(new NativeIncrementProcessor()),
                layerId: "Gameplay");

            var ex = Assert.Throws<InvalidOperationException>(
                () => world.RegisterNativePipeline(
                    "gameplay.shared",
                    secondRegistry,
                    new JobSystemUpdateProcessorBackend<SecondaryNativeTestState, SecondaryNativeIncrementProcessor>(new SecondaryNativeIncrementProcessor()),
                    layerId: "Gameplay"));

            Assert.That(ex!.Message, Does.Contain("gameplay.shared"));
        }

        [Test]
        public void NativePipelineId_既定値でもGetHashCodeが例外にならない()
        {
            var pipelineId = default(NativePipelineId);

            Assert.DoesNotThrow(() => _ = pipelineId.GetHashCode());
        }

        [Test]
        public void RegisterNativePipeline_pipelineOrderの昇順で実行される()
        {
            using var firstRegistry = new NativeStateRegistry<NativeTestState>();
            using var secondRegistry = new NativeStateRegistry<SecondaryNativeTestState>();
            var executionLog = new System.Collections.Generic.List<string>();
            var world = new UpdateCoordinator();

            var laterPipelineId = world.RegisterNativePipeline(
                "gameplay.later",
                secondRegistry,
                new LoggingNativeBackend("later", executionLog),
                pipelineOrder: 20,
                layerId: "Gameplay");

            var earlierPipelineId = world.RegisterNativePipeline(
                "gameplay.earlier",
                firstRegistry,
                new LoggingNativeBackend("earlier", executionLog),
                pipelineOrder: 10,
                layerId: "Gameplay");

            world.RegisterNative(
                laterPipelineId,
                new RecordingElement(),
                new SecondaryNativeTestState { Value = 10 },
                out _);

            world.RegisterNative(
                earlierPipelineId,
                new RecordingElement(),
                new NativeTestState { Value = 1 },
                out _);

            world.RunUpdate(1f, 1f);

            CollectionAssert.AreEqual(
                new[] { "earlier:update", "later:update" },
                executionLog);
        }

        [Test]
        public void RunUpdate_pipeline例外時も先行pipelineのstateはcommitされapplyされる()
        {
            using var successfulRegistry = new NativeStateRegistry<NativeTestState>();
            using var failingRegistry = new NativeStateRegistry<SecondaryNativeTestState>();
            var world = new UpdateCoordinator();
            var successfulMirror = new RecordingElement();
            var failingMirror = new RecordingElement();

            var successfulPipelineId = world.RegisterNativePipeline(
                "gameplay.success",
                successfulRegistry,
                new JobSystemUpdateProcessorBackend<NativeTestState, NativeIncrementProcessor>(new NativeIncrementProcessor()),
                pipelineOrder: 10,
                layerId: "Gameplay");

            var failingPipelineId = world.RegisterNativePipeline(
                "gameplay.fail",
                failingRegistry,
                new ThrowingNativeBackend(),
                pipelineOrder: 20,
                layerId: "Gameplay");

            world.RegisterNative(
                successfulPipelineId,
                successfulMirror,
                new NativeTestState { Value = 10 },
                out var successfulHandle);

            world.RegisterNative(
                failingPipelineId,
                failingMirror,
                new SecondaryNativeTestState { Value = 100 },
                out var failingHandle);

            Assert.Throws<InvalidOperationException>(() => world.RunUpdate(1f, 1f));

            Assert.That(successfulRegistry.TryGetState(successfulHandle, out var successfulState), Is.True);
            Assert.That(successfulState.Value, Is.EqualTo(11));
            Assert.That(successfulRegistry.IsDirty(successfulHandle), Is.False);
            Assert.That(failingRegistry.TryGetState(failingHandle, out var failingState), Is.True);
            Assert.That(failingState.Value, Is.EqualTo(100));
            Assert.That(failingRegistry.IsDirty(failingHandle), Is.True);

            Assert.That(world.ApplyMainThreadChanges(), Is.EqualTo(1));
            Assert.That(successfulMirror.MainThreadApplyCount, Is.EqualTo(1));
            Assert.That(failingMirror.MainThreadApplyCount, Is.EqualTo(0));
            Assert.DoesNotThrow(() => failingRegistry.ClearDirty(failingHandle));
        }

        private sealed class RecordingElement : IUpdateElement, IMainThreadApplyElement
        {
            public readonly System.Collections.Generic.List<string> Events = new();
            private readonly string? _name;
            private readonly System.Collections.Generic.List<string>? _executionLog;

            public RecordingElement()
            {
            }

            public RecordingElement(
                string name,
                System.Collections.Generic.List<string> executionLog)
            {
                _name = name;
                _executionLog = executionLog;
            }

            public Action<UpdateFrameContext>? OnUpdateAction { get; set; }
            public Action? OnStartAction { get; set; }
            public Action<UpdateFrameContext>? OnLateUpdateAction { get; set; }
            public Action<MainThreadApplyContext>? OnMainThreadApplyAction { get; set; }

            public UpdateFrameContext LastUpdateContext { get; private set; }
            public UpdateHandle LastAppliedHandle { get; private set; }
            public int MainThreadApplyCount { get; private set; }

            public void OnElementStart()
            {
                Events.Add("start");
                OnStartAction?.Invoke();
            }

            public void OnElementUpdate(in UpdateFrameContext context)
            {
                LastUpdateContext = context;
                Events.Add("update");
                if (_executionLog != null && _name != null)
                {
                    _executionLog.Add($"{_name}:update");
                }

                OnUpdateAction?.Invoke(context);
            }

            public void OnElementLateUpdate(in UpdateFrameContext context)
            {
                Events.Add("late-update");
                if (_executionLog != null && _name != null)
                {
                    _executionLog.Add($"{_name}:late-update");
                }

                OnLateUpdateAction?.Invoke(context);
            }

            public void ApplyMainThread(in MainThreadApplyContext context)
            {
                MainThreadApplyCount++;
                LastAppliedHandle = context.Handle;
                OnMainThreadApplyAction?.Invoke(context);
            }
        }

        private sealed class RecordingBackend : IUpdateExecutionBackend
        {
            public readonly System.Collections.Generic.List<UpdateExecutionPhase> Phases = new();
            public readonly System.Collections.Generic.List<int> ElementsPerDispatch = new();

            public void ExecuteManaged(in ManagedExecutionBatch batch)
            {
                var context = batch.Context;
                Phases.Add(batch.Phase);
                ElementsPerDispatch.Add(batch.Count);

                for (var i = 0; i < batch.Elements.Count; i++)
                {
                    switch (batch.Phase)
                    {
                        case UpdateExecutionPhase.Update:
                            batch.Elements[i].OnElementUpdate(in context);
                            break;

                        case UpdateExecutionPhase.LateUpdate:
                            batch.Elements[i].OnElementLateUpdate(in context);
                            break;

                        default:
                            throw new ArgumentOutOfRangeException(nameof(batch.Phase), batch.Phase, null);
                    }
                }
            }

            public void ExecuteNative<TState>(NativeExecutionBatch<TState> batch)
                where TState : unmanaged
            {
                throw new NotSupportedException();
            }
        }

        private sealed class ThrowingNativeBackend : IUpdateExecutionBackend
        {
            public void ExecuteManaged(in ManagedExecutionBatch batch)
            {
                throw new NotSupportedException();
            }

            public void ExecuteNative<TState>(NativeExecutionBatch<TState> batch)
                where TState : unmanaged
            {
                if (batch.ElementCount > 0)
                {
                    var dirtyFlags = batch.DirtyFlags;
                    dirtyFlags[0] = 1;
                }

                throw new InvalidOperationException("native failed");
            }
        }

        private sealed class LoggingNativeBackend : IUpdateExecutionBackend
        {
            private readonly string _name;
            private readonly System.Collections.Generic.List<string> _executionLog;

            public LoggingNativeBackend(string name, System.Collections.Generic.List<string> executionLog)
            {
                _name = name;
                _executionLog = executionLog;
            }

            public void ExecuteManaged(in ManagedExecutionBatch batch)
            {
                throw new NotSupportedException();
            }

            public void ExecuteNative<TState>(NativeExecutionBatch<TState> batch)
                where TState : unmanaged
            {
                _executionLog.Add($"{_name}:{batch.Phase.ToString().ToLowerInvariant()}");
            }
        }

        private sealed class RecordingApplyCommand : IMainThreadApplyCommand
        {
            public int ApplyCount { get; private set; }

            public void Apply()
            {
                ApplyCount++;
            }
        }

        private sealed class ThrowingApplyCommand : IMainThreadApplyCommand
        {
            public void Apply()
            {
                throw new InvalidOperationException("apply failed");
            }
        }

        private struct NativeTestState
        {
            public int Value;
        }

        private struct SecondaryNativeTestState
        {
            public int Value;
        }

        private struct NativeIncrementProcessor : INativeUpdateJobProcessor<NativeTestState>
        {
            public void Execute(
                int index,
                ref NativeTestState state,
                ref byte dirtyFlag,
                UpdateExecutionPhase phase,
                in UpdateFrameContext context)
            {
                if (phase != UpdateExecutionPhase.Update)
                {
                    return;
                }

                state.Value += (int)context.DeltaTime;
                dirtyFlag = 1;
            }
        }

        private struct SecondaryNativeIncrementProcessor : INativeUpdateJobProcessor<SecondaryNativeTestState>
        {
            public void Execute(
                int index,
                ref SecondaryNativeTestState state,
                ref byte dirtyFlag,
                UpdateExecutionPhase phase,
                in UpdateFrameContext context)
            {
                if (phase != UpdateExecutionPhase.Update)
                {
                    return;
                }

                state.Value += (int)context.DeltaTime * 2;
                dirtyFlag = 1;
            }
        }
    }
}
