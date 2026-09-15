#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace OneStarMaker.Build.Selection
{
    /// <summary>
    /// Carries canonical validation issues and exposes a plan only when no error issue exists.
    /// </summary>
    public sealed class BuildPlanResult
    {
        private readonly ReadOnlyCollection<BuildValidationIssue> _issues;

        internal BuildPlanResult(BuildPlan? plan, IEnumerable<BuildValidationIssue> issues)
        {
            _issues = Array.AsReadOnly(issues.OrderBy(issue => issue.Severity)
                .ThenBy(issue => issue.Code)
                .ThenBy(issue => issue.Subject)
                .ThenBy(issue => issue.SubjectKey, StringComparer.Ordinal)
                .ThenBy(issue => issue.Dimension, StringComparer.Ordinal)
                .ThenBy(issue => issue.Value, StringComparer.Ordinal)
                .ThenBy(issue => issue.ProviderKey, StringComparer.Ordinal)
                .ToArray());
            Plan = _issues.Any(issue => issue.Severity == BuildValidationSeverity.Error) ? null : plan;
        }

        public BuildPlan? Plan { get; }
        public IReadOnlyList<BuildValidationIssue> Issues => _issues;
        public bool IsSuccess => Plan != null;
    }
}
