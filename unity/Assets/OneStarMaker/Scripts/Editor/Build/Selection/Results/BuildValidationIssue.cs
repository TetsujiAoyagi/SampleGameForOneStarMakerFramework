#nullable enable

namespace OneStarMaker.Build.Selection
{
    /// <summary>Defines whether an issue prevents publication of a build plan.</summary>
    public enum BuildValidationSeverity
    {
        Error,
        Warning
    }

    /// <summary>Identifies the kind of input entity associated with an issue.</summary>
    public enum BuildValidationSubject
    {
        Request,
        Candidate,
        Requirement
    }

    /// <summary>
    /// Describes one structured validation outcome using fields suitable for canonical ordering.
    /// The display message is informational and is not part of snapshot identity.
    /// </summary>
    public sealed class BuildValidationIssue
    {
        public BuildValidationIssue(
            BuildValidationSeverity severity,
            BuildValidationCode code,
            BuildValidationSubject subject,
            string subjectKey,
            string? dimension,
            string? value,
            string? providerKey,
            string message)
        {
            Severity = severity;
            Code = code;
            Subject = subject;
            SubjectKey = subjectKey;
            Dimension = dimension;
            Value = value;
            ProviderKey = providerKey;
            Message = message;
        }

        public BuildValidationSeverity Severity { get; }
        public BuildValidationCode Code { get; }
        public BuildValidationSubject Subject { get; }
        public string SubjectKey { get; }
        public string? Dimension { get; }
        public string? Value { get; }
        public string? ProviderKey { get; }
        public string Message { get; }
    }
}
