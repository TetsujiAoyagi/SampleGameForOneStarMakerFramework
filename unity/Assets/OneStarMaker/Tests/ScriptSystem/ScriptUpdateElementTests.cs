#nullable enable

using System;
using NUnit.Framework;
using Unity.Profiling;
using OneStarMaker.Foundation.UpdateSystem;
using OneStarMaker.Foundation.UpdateSystem.World;
using OneStarMaker.Runtime.ScriptSystem;

namespace OneStarMaker.Tests.ScriptSystem
{
    [TestFixture]
    public sealed class ScriptUpdateElementTests
    {
        [Test]
        public void RunUpdate_ExecutesTheBudget_AndLateUpdateDoesNotAdvance()
        {
            var registers = new ScriptRegisters(1);
            var machine = new ScriptMachine(
                ScriptProgram.Create(new[]
                {
                    ScriptInstruction.LoadImmediate(0, 1),
                    ScriptInstruction.LoadImmediate(0, 2),
                }),
                registers);
            var element = new ScriptUpdateElement(machine, 1);
            var coordinator = new UpdateCoordinator();
            coordinator.RegisterElement("Script", element);
            coordinator.ActivatePendingRegistrations();

            coordinator.RunUpdate(0.5f, 0.5f);

            Assert.That(registers[0], Is.EqualTo(1L));
            Assert.That(machine.ProgramCounter, Is.EqualTo(1));
            Assert.That(machine.Status, Is.EqualTo(ScriptMachineStatus.Yielded));

            coordinator.RunLateUpdate(0.5f, 0.5f);

            Assert.That(registers[0], Is.EqualTo(1L));
            Assert.That(machine.ProgramCounter, Is.EqualTo(1));
            Assert.That(machine.Status, Is.EqualTo(ScriptMachineStatus.Yielded));
        }

        [Test]
        public void LaterUpdate_ResumesYieldedProgram()
        {
            var registers = new ScriptRegisters(1);
            var machine = new ScriptMachine(
                ScriptProgram.Create(new[]
                {
                    ScriptInstruction.LoadImmediate(0, 1),
                    ScriptInstruction.LoadImmediate(0, 2),
                }),
                registers);
            var element = new ScriptUpdateElement(machine, 1);
            var coordinator = new UpdateCoordinator();
            coordinator.RegisterElement("Script", element);
            coordinator.ActivatePendingRegistrations();

            coordinator.RunUpdate(0.1f, 0.1f);
            coordinator.RunUpdate(0.1f, 0.1f);

            Assert.That(registers[0], Is.EqualTo(2L));
            Assert.That(machine.ProgramCounter, Is.EqualTo(2));
            Assert.That(machine.Status, Is.EqualTo(ScriptMachineStatus.Yielded));

            coordinator.RunUpdate(0.1f, 0.1f);

            Assert.That(machine.Status, Is.EqualTo(ScriptMachineStatus.Halted));
            Assert.That(machine.ProgramCounter, Is.EqualTo(2));
            Assert.That(registers[0], Is.EqualTo(2L));
        }

        [Test]
        public void Fault_DoesNotStopALaterElementInTheSameUpdate()
        {
            var registers = new ScriptRegisters(1);
            var machine = new ScriptMachine(
                ScriptProgram.Create(new[] { ScriptInstruction.LoadImmediate(5, 1) }),
                registers);
            var script = new ScriptUpdateElement(machine, 4);
            var recorder = new RecordingElement();
            var coordinator = new UpdateCoordinator();
            coordinator.RegisterElement("Script", script, executionOrder: 0);
            coordinator.RegisterElement("Script", recorder, executionOrder: 1);
            coordinator.ActivatePendingRegistrations();

            Assert.DoesNotThrow(() => coordinator.RunUpdate(0.1f, 0.1f));

            Assert.That(machine.Status, Is.EqualTo(ScriptMachineStatus.InvalidRegister));
            Assert.That(recorder.Updates, Is.EqualTo(1));
        }

        [Test]
        public void Constructor_RejectsNullMachineAndNonPositiveBudget()
        {
            var machine = new ScriptMachine(
                ScriptProgram.Create(new[] { ScriptInstruction.Halt() }),
                new ScriptRegisters(0));

            Assert.Throws<ArgumentNullException>(() => new ScriptUpdateElement(null!, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ScriptUpdateElement(machine, 0));
        }

        [Test]
        public void RegisteredUpdateAndLateUpdate_NonterminatingJumpLoop_AllocatesZeroManagedBytesAfterWarmup()
        {
            const int warmupIterations = 128;
            const int measuredIterations = 1000;
            const int budget = 16;
            var registers = new ScriptRegisters(2);
            registers[1] = 1;
            var machine = new ScriptMachine(ScriptProgram.Create(new[]
            {
                ScriptInstruction.Add(0, 0, 1),
                ScriptInstruction.Jump(0),
            }), registers);
            var element = new ScriptUpdateElement(machine, budget);
            // Coordinator is the isolated registered execution seam; production callers use UpdateSystemRuntime.
            var coordinator = new UpdateCoordinator();
            coordinator.RegisterElement("Script", element);
            coordinator.ActivatePendingRegistrations();
            for (var i = 0; i < warmupIterations; i++)
            {
                coordinator.ActivatePendingRegistrations();
                coordinator.RunUpdate(0.1f, 0.1f);
                coordinator.RunLateUpdate(0.1f, 0.1f);
            }

            var initialValue = registers[0];
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
            TestContext.Out.WriteLine($"GC.Alloc unit={recorder.UnitType} positiveEvents={positiveEvents}");
            Assert.That(positiveEvents, Is.GreaterThan(0L));
            recorder.Reset();
            Assert.That(recorder.Count, Is.Zero);

            recorder.Start();
            try
            {
                for (var i = 0; i < measuredIterations; i++)
                {
                    coordinator.ActivatePendingRegistrations();
                    coordinator.RunUpdate(0.1f, 0.1f);
                    coordinator.RunLateUpdate(0.1f, 0.1f);
                }
            }
            finally
            {
                recorder.Stop();
            }
            var targetEvents = recorder.Count == 0 ? 0L : recorder.GetSample(0).Count;

            TestContext.Out.WriteLine(
                $"allocation test={nameof(RegisteredUpdateAndLateUpdate_NonterminatingJumpLoop_AllocatesZeroManagedBytesAfterWarmup)} " +
                $"runtime={System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription} " +
                $"unity={UnityEngine.Application.unityVersion} warmup={warmupIterations} " +
                $"iterations={measuredIterations} budget={budget} unit={recorder.UnitType} positiveEvents={positiveEvents} targetEvents={targetEvents} " +
                $"increments={registers[0] - initialValue} status={machine.Status}");
            Assert.That(targetEvents, Is.Zero);
            Assert.That(registers[0] - initialValue, Is.EqualTo(measuredIterations * (budget / 2)));
            Assert.That(machine.Status, Is.EqualTo(ScriptMachineStatus.Yielded));
            Assert.That(machine.IsLatched, Is.False);
            Assert.That(machine.ProgramCounter, Is.EqualTo(0));
        }

        private sealed class RecordingElement : IUpdateElement
        {
            public int Updates { get; private set; }

            public void OnElementStart()
            {
            }

            public void OnElementUpdate(in UpdateFrameContext context)
            {
                Updates++;
            }

            public void OnElementLateUpdate(in UpdateFrameContext context)
            {
            }
        }
    }
}
