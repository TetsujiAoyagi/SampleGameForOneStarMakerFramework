#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;

namespace CD0Spike.Editor
{
    internal static class Cd0FixtureCleanup
    {
        internal static void DeleteVerifiedFixture(IReadOnlyDictionary<string, string> expectedGuidByPath)
        {
            if (expectedGuidByPath == null) throw new ArgumentNullException(nameof(expectedGuidByPath));
            foreach (var pair in expectedGuidByPath)
            {
                if (!pair.Key.StartsWith(Cd0FixturePaths.FixtureRoot + "/", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Cleanup path is outside the fixture root: {pair.Key}");
                }
                var actualGuid = AssetDatabase.AssetPathToGUID(pair.Key);
                if (!string.Equals(actualGuid, pair.Value, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"GUID changed; refusing to delete {pair.Key}.");
                }
            }

            foreach (var pair in expectedGuidByPath)
            {
                if (!AssetDatabase.DeleteAsset(pair.Key))
                {
                    throw new InvalidOperationException($"Failed to delete verified fixture asset: {pair.Key}");
                }
            }
            AssetDatabase.Refresh();
        }
    }
}
