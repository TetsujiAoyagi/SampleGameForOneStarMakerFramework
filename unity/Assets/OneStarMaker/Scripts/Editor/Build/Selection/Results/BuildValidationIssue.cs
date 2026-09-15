#nullable enable

namespace OneStarMaker.Build.Selection
{
    /// <summary>issueがbuild planの公開を妨げるかを定義します。</summary>
    public enum BuildValidationSeverity
    {
        Error,
        Warning
    }

    /// <summary>issueに関連する入力entityの種類を識別します。</summary>
    public enum BuildValidationSubject
    {
        Request,
        Candidate,
        Requirement
    }

    /// <summary>
    /// canonical orderingに使えるfieldで、構造化されたvalidation結果1件を表します。
    /// display messageは説明専用であり、snapshot identityには含めません。
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
