#nullable enable

using System;
using System.Globalization;

namespace SampleGame.DependOnAll.Editor.WorldAuthoring
{
    /// <summary>S-4b まで旧 bulk generator が必要とする無修飾 Environment 名称。</summary>
    internal static class LegacyWorldAuthoringNames
    {
        internal const string EnvironmentPrefix = "Environment_";
        internal const string EnvironmentRootName = "EnvironmentRoot";

        internal static string FormatEnvironment(int x, int y)
        {
            if (x < 0) throw new ArgumentOutOfRangeException(nameof(x));
            if (y < 0) throw new ArgumentOutOfRangeException(nameof(y));
            return $"{EnvironmentPrefix}{x}_{y}";
        }

        internal static bool IsEnvironmentIdentity(string? identity)
        {
            if (identity == null || identity.Length == 0
                || !identity.StartsWith(EnvironmentPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            var tokens = identity.Substring(EnvironmentPrefix.Length).Split('_');
            return tokens.Length == 2
                && int.TryParse(tokens[0], NumberStyles.None, CultureInfo.InvariantCulture, out _)
                && int.TryParse(tokens[1], NumberStyles.None, CultureInfo.InvariantCulture, out _);
        }
    }
}
