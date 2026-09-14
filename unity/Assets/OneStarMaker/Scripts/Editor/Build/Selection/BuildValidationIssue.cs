#nullable enable

namespace OneStarMaker.Build.Selection
{
    public enum BuildValidationSeverity
    {
        Error,
        Warning
    }

    public enum BuildValidationSubject
    {
        Request,
        Candidate,
        Requirement
    }

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
