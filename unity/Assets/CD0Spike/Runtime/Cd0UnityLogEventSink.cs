#nullable enable

using UnityEngine;

namespace CD0Spike
{
    internal sealed class Cd0UnityLogEventSink : ICd0ProbeEventSink
    {
        public void Record(Cd0ProbeEvent probeEvent)
        {
            Debug.Log($"[CD0] case={probeEvent.CaseId} generation={probeEvent.Generation} operation={probeEvent.OperationKind} stage={probeEvent.Stage} detail={probeEvent.Detail}");
        }
    }
}
