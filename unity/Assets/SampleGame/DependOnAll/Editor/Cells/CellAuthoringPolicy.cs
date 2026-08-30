#nullable enable

using System;
using System.Collections.Generic;

namespace SampleGame.DependOnAll.Editor.Cells
{
    public enum CellAuthoringPolicyKind
    {
        Generated = 0,
        HandAuthored = 1,
    }

    /// <summary>SampleGame の Cell identity → 編集方針の解決。</summary>
    public static class CellAuthoringPolicy
    {
        private static readonly HashSet<string> HandAuthoredIdentities = new(StringComparer.Ordinal)
        {
            "Cell_0_0",
            "Cell_1_0",
            "Cell_2_0",
            "Cell_3_0",
        };

        public static CellAuthoringPolicyKind Resolve(string identity)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            return HandAuthoredIdentities.Contains(identity)
                ? CellAuthoringPolicyKind.HandAuthored
                : CellAuthoringPolicyKind.Generated;
        }
    }
}
