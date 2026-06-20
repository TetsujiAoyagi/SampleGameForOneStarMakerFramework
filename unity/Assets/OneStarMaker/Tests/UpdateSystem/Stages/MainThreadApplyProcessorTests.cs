#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using OneStarMaker.Foundation.UpdateSystem;
using OneStarMaker.Foundation.UpdateSystem.Apply;

namespace OneStarMaker.Tests.UpdateSystem
{
    [TestFixture]
    public class MainThreadApplyProcessorTests
    {
        [Test]
        public void Test1()
        {
            var stage = new MainThreadApplyProcessor();
            var elementRegistry = new UpdateElementRegistry();
            var handleBuffer = new MainThreadApplyHandleBuffer();
            var commandBuffer = new MainThreadApplyCommandBuffer();
            var log = new List<string>();
            var element = new RecordingApplyElement(log);

            Assert.That(
                elementRegistry.Register(element, UpdateElementSyncPolicy.AllowMainThreadApply, out var handle),
                Is.True);

            handleBuffer.Enqueue(handle);
            commandBuffer.Enqueue(new RecordingApplyCommand(log));

            var appliedCount = stage.Apply(elementRegistry, handleBuffer, commandBuffer);

            Assert.That(appliedCount, Is.EqualTo(2));
            CollectionAssert.AreEqual(new[] { "handle", "command" }, log);
        }

        [Test]
        public void Test2()
        {
            var stage = new MainThreadApplyProcessor();
            var elementRegistry = new UpdateElementRegistry();
            var handleBuffer = new MainThreadApplyHandleBuffer();
            var commandBuffer = new MainThreadApplyCommandBuffer();
            var element = new PlainElement();

            Assert.That(
                elementRegistry.Register(element, UpdateElementSyncPolicy.AllowMainThreadApply, out var handle),
                Is.True);

            handleBuffer.Enqueue(handle);

            var appliedCount = stage.Apply(elementRegistry, handleBuffer, commandBuffer);

            Assert.That(appliedCount, Is.EqualTo(0));
        }

        private sealed class RecordingApplyElement : IUpdateElement, IMainThreadApplyElement
        {
            private readonly List<string> _log;

            public RecordingApplyElement(List<string> log)
            {
                _log = log;
            }

            public void OnElementStart()
            {
            }

            public void OnElementUpdate(in UpdateFrameContext context)
            {
            }

            public void OnElementLateUpdate(in UpdateFrameContext context)
            {
            }

            public void ApplyMainThread(in MainThreadApplyContext context)
            {
                _log.Add("handle");
            }
        }

        private sealed class PlainElement : IUpdateElement
        {
            public void OnElementStart()
            {
            }

            public void OnElementUpdate(in UpdateFrameContext context)
            {
            }

            public void OnElementLateUpdate(in UpdateFrameContext context)
            {
            }
        }

        private sealed class RecordingApplyCommand : IMainThreadApplyCommand
        {
            private readonly List<string> _log;

            public RecordingApplyCommand(List<string> log)
            {
                _log = log;
            }

            public void Apply()
            {
                _log.Add("command");
            }
        }
    }
}
