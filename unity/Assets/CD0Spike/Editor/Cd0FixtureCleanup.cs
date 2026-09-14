#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace CD0Spike.Editor
{
    internal static class Cd0FixtureCleanup
    {
        internal static void DeleteVerifiedFixture(IReadOnlyDictionary<string, string> expectedGuidByPath)
        {
            if (expectedGuidByPath == null) throw new ArgumentNullException(nameof(expectedGuidByPath));
            var verifiedPaths = new List<string>(expectedGuidByPath.Count);
            foreach (var pair in expectedGuidByPath)
            {
                var verifiedPath = NormalizeFixtureAssetPath(pair.Key);
                var actualGuid = AssetDatabase.AssetPathToGUID(verifiedPath);
                if (!string.Equals(actualGuid, pair.Value, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"GUID changed; refusing to delete {verifiedPath}.");
                }
                verifiedPaths.Add(verifiedPath);
            }

            foreach (var verifiedPath in verifiedPaths)
            {
                if (!AssetDatabase.DeleteAsset(verifiedPath))
                {
                    throw new InvalidOperationException($"Failed to delete verified fixture asset: {verifiedPath}");
                }
            }
            AssetDatabase.Refresh();
        }

        internal static string NormalizeFixtureAssetPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || Path.IsPathRooted(assetPath))
            {
                throw new InvalidOperationException($"Cleanup path must be project-relative: {assetPath}");
            }
            var candidate = assetPath.Replace('\\', '/');
            var projectRoot = Cd0ArtifactInventory.CanonicalDirectory(Cd0FixturePaths.ProjectRoot);
            var fixtureRoot = Cd0ArtifactInventory.CanonicalDirectory(Path.Combine(projectRoot, Cd0FixturePaths.FixtureRoot));
            var canonical = Path.GetFullPath(Path.Combine(projectRoot, candidate));
            if (!Cd0ArtifactInventory.IsContained(fixtureRoot, canonical)
                || string.Equals(fixtureRoot, Cd0ArtifactInventory.CanonicalDirectory(canonical), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Cleanup path is outside the fixture contents: {assetPath}");
            }
            var normalized = Path.GetRelativePath(projectRoot, canonical).Replace('\\', '/');
            if (!string.Equals(candidate, normalized, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Cleanup path is not canonical: {assetPath}");
            }
            return normalized;
        }
    }
}
