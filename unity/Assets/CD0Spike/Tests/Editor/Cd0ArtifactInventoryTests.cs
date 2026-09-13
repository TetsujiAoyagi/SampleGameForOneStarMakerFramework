#nullable enable

using System.IO;
using NUnit.Framework;

namespace CD0Spike.Tests
{
    public sealed class Cd0ArtifactInventoryTests
    {
        [Test]
        public void IsContained_AcceptsRootAndDescendant()
        {
            var root = Path.GetFullPath(Path.Combine("tmp", "cd0"));

            Assert.That(Editor.Cd0ArtifactInventory.IsContained(root, root), Is.True);
            Assert.That(Editor.Cd0ArtifactInventory.IsContained(root, Path.Combine(root, "run-1")), Is.True);
        }

        [Test]
        public void IsContained_RejectsPrefixConfusionAndParentTraversal()
        {
            var root = Path.GetFullPath(Path.Combine("tmp", "cd0"));

            Assert.That(Editor.Cd0ArtifactInventory.IsContained(root, root + "-other"), Is.False);
            Assert.That(Editor.Cd0ArtifactInventory.IsContained(root, Path.GetFullPath(Path.Combine(root, "..", "outside"))), Is.False);
        }
    }
}
