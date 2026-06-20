#nullable enable

using System;
using System.Reflection;
using NUnit.Framework;
using OneStarMaker.Foundation.UpdateSystem;

namespace OneStarMaker.Tests.UpdateSystem
{
    [TestFixture]
    public class UpdateElementRegistryTests
    {
        [Test]
        public void Test1()
        {
            var registry = new UpdateElementRegistry();
            var element = new PlainElement();

            Assert.That(
                registry.Register(element, UpdateElementSyncPolicy.AllowMainThreadApply, out var originalHandle),
                Is.True);

            ForceElementEntryGeneration(
                registry,
                originalHandle.Slot,
                element,
                uint.MaxValue,
                UpdateElementSyncPolicy.AllowMainThreadApply,
                isAlive: true);

            Assert.That(registry.Remove(new UpdateHandle(originalHandle.Slot, uint.MaxValue)), Is.True);

            var replacement = new PlainElement();
            Assert.That(
                registry.Register(replacement, UpdateElementSyncPolicy.AllowMainThreadApply, out var reusedHandle),
                Is.True);

            Assert.That(reusedHandle.Slot, Is.EqualTo(originalHandle.Slot));
            Assert.That(reusedHandle.Generation, Is.EqualTo(1u));
        }

        [Test]
        public void Test2()
        {
            var registry = new UpdateElementRegistry();
            var element = new PlainElement();

            Assert.That(
                registry.Register(element, UpdateElementSyncPolicy.AllowMainThreadApply, out var handle),
                Is.True);

            Assert.That(registry.TryGetPolicy(handle, out var policy), Is.True);
            Assert.That(policy, Is.EqualTo(UpdateElementSyncPolicy.AllowMainThreadApply));
        }

        private static void ForceElementEntryGeneration(
            UpdateElementRegistry registry,
            int slot,
            IUpdateElement element,
            uint generation,
            UpdateElementSyncPolicy policy,
            bool isAlive)
        {
            var registryType = typeof(UpdateElementRegistry);
            var entriesField = registryType.GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic);
            var entries = entriesField!.GetValue(registry);
            var entryType = registryType.GetNestedType("ElementEntry", BindingFlags.NonPublic);
            var ctor = entryType!.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                binder: null,
                new[] { typeof(IUpdateElement), typeof(uint), typeof(UpdateElementSyncPolicy), typeof(bool) },
                modifiers: null);

            var entry = ctor!.Invoke(new object?[] { element, generation, policy, isAlive });
            var itemProperty = entries!.GetType().GetProperty("Item");
            itemProperty!.SetValue(entries, entry, new object[] { slot });
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
    }
}
