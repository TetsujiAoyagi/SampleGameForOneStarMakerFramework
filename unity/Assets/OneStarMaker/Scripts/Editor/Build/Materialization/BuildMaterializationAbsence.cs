#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace OneStarMaker.Editor.Build.Materialization
{
    /// <summary>手元で source が欠けて閉包を作れなかった候補の理由。</summary>
    public enum BuildMaterializationAbsenceReason
    {
        /// <summary>root の GUID が手元で path に解決しない（その asset を一度も import していない checkout）。足りない path は分からない。</summary>
        RootUnresolved,
        /// <summary>root の GUID は path に解決するが、その file が無い。</summary>
        RootFileMissing,
        /// <summary>root の file はあるが、Unity が返した依存の path の file が無い。</summary>
        DependencyMissing,
    }

    /// <summary>
    /// 欠けを許す materialize（<see cref="SceneResourceContentMaterializer.MaterializeAllowingAbsentSources"/>）が残した、閉包を持たない候補 1 件の記録。
    /// 候補自体は snapshot に残る（stable key は欠けの無い時と同じ）ので、選択は今の規則で走る。閉包が無いので、selected に残すと projection が止める。
    /// </summary>
    /// <remarks>
    /// 依存の file が欠けても、Unity の <c>AssetDatabase.GetDependencies</c> がその path を返さない時は、閉包が小さく見えるだけで欠けの記録は
    /// 出ない（6000.6.9f1 で、material を退かした Scene の依存に material もその texture も出なかった）。その欠けは base の閉包の記録で見つける。
    /// </remarks>
    public sealed class BuildMaterializationAbsence
    {
        private readonly ReadOnlyCollection<string> _missing;

        internal BuildMaterializationAbsence(string stableKey, string logicalKey, string rootGuid, string? rootPath,
            BuildMaterializationAbsenceReason reason, IEnumerable<string> missingPaths)
        {
            StableKey = stableKey;
            LogicalKey = logicalKey;
            RootGuid = rootGuid;
            RootPath = rootPath;
            Reason = reason;
            _missing = Array.AsReadOnly(missingPaths.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray());
        }

        public string StableKey { get; }
        public string LogicalKey { get; }
        public string RootGuid { get; }
        /// <summary>root の path。<see cref="BuildMaterializationAbsenceReason.RootUnresolved"/> では null。</summary>
        public string? RootPath { get; }
        public BuildMaterializationAbsenceReason Reason { get; }
        /// <summary>手元に無い file の path（root の file か依存の file）。root の GUID が解決しない時は空。</summary>
        public IReadOnlyList<string> MissingPaths => _missing;
    }
}
