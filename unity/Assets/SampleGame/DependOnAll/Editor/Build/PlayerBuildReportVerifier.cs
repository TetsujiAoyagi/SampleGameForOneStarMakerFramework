#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Build.Reporting;

namespace SampleGame.DependOnAll.Editor.Build
{
    internal sealed class PlayerBuildVerification
    {
        internal PlayerBuildVerification(string buildGuid, string[] checkedGuids, string[] files)
        { BuildGuid = buildGuid; CheckedGuids = checkedGuids; Files = files; }
        internal string BuildGuid { get; } internal string[] CheckedGuids { get; } internal string[] Files { get; }
    }
    internal static class PlayerBuildReportVerifier
    {
        internal static PlayerBuildVerification Verify(BuildReport report, IEnumerable<string> selectedRootGuids, string sourceBootstrapGuid, string generatedBootstrapGuid)
        {
            if (report == null || report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Player build did not succeed.");
            var selected = new HashSet<string>(selectedRootGuids.Where(x => !string.IsNullOrEmpty(x)), StringComparer.OrdinalIgnoreCase);
            if (selected.Count == 0 || report.packedAssets == null || report.packedAssets.Length == 0)
                throw new InvalidOperationException("Detailed packed asset data is required.");
            var packed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pack in report.packedAssets)
                foreach (var content in pack.contents)
                    if (!string.IsNullOrEmpty(content.sourceAssetGUID.ToString())) packed.Add(content.sourceAssetGUID.ToString());
            var duplicates = selected.Where(packed.Contains).ToArray();
            if (duplicates.Length != 0) throw new InvalidOperationException("Selected content roots were duplicated into the Player: " + string.Join(",", duplicates));
            if (packed.Contains(sourceBootstrapGuid)) throw new InvalidOperationException("Production bootstrap Scene was packed into the Player.");
            // Detailed report は Scene 内 object ごとに同じ source Scene GUID を複数行返す。
            // GUID 行数ではなく格納先 pack 数を数え、Scene 入力と Resources の二重格納を区別する。
            var generatedPacks = report.packedAssets
                .Where(pack => pack.contents.Any(x => string.Equals(x.sourceAssetGUID.ToString(), generatedBootstrapGuid, StringComparison.OrdinalIgnoreCase)))
                .Select(pack => pack.shortPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (generatedPacks.Length != 1)
                throw new InvalidOperationException("Generated bootstrap Scene must belong to exactly one Player pack; packs=" + string.Join(",", generatedPacks) + ".");
            var files = report.GetFiles().Select(x => x.path).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            if (files.Length == 0) throw new InvalidOperationException("Player report contains no files.");
            return new PlayerBuildVerification(report.summary.guid.ToString(), selected.OrderBy(x => x, StringComparer.Ordinal).ToArray(), files);
        }
    }
}
