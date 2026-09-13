#nullable enable

namespace CD0Spike
{
    internal interface ICd0ProbeEventSink
    {
        void Record(Cd0ProbeEvent probeEvent);
    }

    internal readonly struct Cd0ProbeEvent
    {
        internal Cd0ProbeEvent(string caseId, int generation, string operationKind, string stage, string detail)
        {
            CaseId = caseId;
            Generation = generation;
            OperationKind = operationKind;
            Stage = stage;
            Detail = detail;
        }

        internal string CaseId { get; }
        internal int Generation { get; }
        internal string OperationKind { get; }
        internal string Stage { get; }
        internal string Detail { get; }
    }
}
