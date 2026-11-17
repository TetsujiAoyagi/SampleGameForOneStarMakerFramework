#nullable enable

using System;
using OneStarMaker.Runtime.ContentResolution;

namespace OneStarMaker.ContentResolutionOfflineTests
{
    // Tests for the explicit fallback representation on ContentRequest and how the resolver adopts it.
    // A request may name one fallback representation; the resolver adopts a candidate whose representation is
    // either the requested one or that fallback, and nothing else. Without a fallback, behaviour is unchanged.
    internal static partial class Program
    {
        private static void RunFallbackTests()
        {
            Run(nameof(Fallback_Request_Validation), Fallback_Request_Validation);
            Run(nameof(Fallback_ExplicitFallback_IsAdopted), Fallback_ExplicitFallback_IsAdopted);
            Run(nameof(Fallback_WithoutFallback_FullCandidateIsStillRepresentationMismatch), Fallback_WithoutFallback_FullCandidateIsStillRepresentationMismatch);
            Run(nameof(Fallback_RequestedRepresentationStillWins_InProviderOrder), Fallback_RequestedRepresentationStillWins_InProviderOrder);
            Run(nameof(Fallback_OtherRepresentation_IsNotAdopted), Fallback_OtherRepresentation_IsNotAdopted);
            Run(nameof(Fallback_RecordKeepsTheAdoptedRepresentation), Fallback_RecordKeepsTheAdoptedRepresentation);
        }

        // F1
        private static void Fallback_Request_Validation()
        {
            Equal(string.Empty, new ContentRequest("Entry", ContentKind.Scene, "Whitebox").FallbackRepresentation, "no fallback by default");
            Equal("Full", new ContentRequest("Entry", ContentKind.Scene, "Whitebox", fallbackRepresentation: "Full").FallbackRepresentation, "explicit fallback");
            Throws<ArgumentException>(() => new ContentRequest("Entry", ContentKind.Scene, "", fallbackRepresentation: "Full"));
            Throws<ArgumentException>(() => new ContentRequest("Entry", ContentKind.Scene, "Full", fallbackRepresentation: "Full"));
            Throws<ArgumentException>(() => new ContentRequest("Entry", ContentKind.Scene, "Whitebox", fallbackRepresentation: "  "));
            Throws<ArgumentException>(() => new ContentRequest("Entry", ContentKind.Scene, "Whitebox", fallbackRepresentation: ""));
            // The positional arguments of the existing constructor do not move.
            var request = new ContentRequest("Entry", ContentKind.Scene, "Whitebox", X, "corr", "Full");
            Equal(X, request.ExpectedIdentity!.Value, "pin keeps its position");
            Equal("corr", request.CorrelationId, "correlation id keeps its position");
        }

        // F2
        private static void Fallback_ExplicitFallback_IsAdopted()
        {
            var request = new ContentRequest("Entry", ContentKind.Scene, "Whitebox", fallbackRepresentation: "Full");
            var result = Resolve(request, Policy(Rule.All, Provider.Available("A", Cand("A", representation: "Full"))));
            True(result.IsResolved, "fallback candidate is adopted");
            Equal("Full", result.Candidate!.Representation, "adopted representation");
            Outcomes(result, ProbeOutcome.Adopted);
        }

        // F3
        private static void Fallback_WithoutFallback_FullCandidateIsStillRepresentationMismatch()
        {
            var request = new ContentRequest("Entry", ContentKind.Scene, "Whitebox");
            var result = Resolve(request, Policy(Rule.All, Provider.Available("A", Cand("A", representation: "Full"))));
            False(result.IsResolved, "no implicit fallback");
            Equal(ContentResolutionFailureCode.NoCompatibleCandidate, result.FailureCode, "failure code");
            Outcomes(result, ProbeOutcome.RepresentationMismatch);
        }

        // F4
        private static void Fallback_RequestedRepresentationStillWins_InProviderOrder()
        {
            var request = new ContentRequest("Entry", ContentKind.Scene, "Whitebox", fallbackRepresentation: "Full");
            var result = Resolve(request, Policy(Rule.All,
                Provider.Available("A", Cand("A", representation: "Whitebox")),
                Provider.Available("B", Cand("B", representation: "Full"))));
            Equal("A", result.Candidate!.ProviderId, "first acceptable candidate is adopted");
            Equal("Whitebox", result.Candidate.Representation, "requested representation");
        }

        // F5
        private static void Fallback_OtherRepresentation_IsNotAdopted()
        {
            var request = new ContentRequest("Entry", ContentKind.Scene, "Whitebox", fallbackRepresentation: "Full");
            var result = Resolve(request, Policy(Rule.All, Provider.Available("A", Cand("A", representation: "Low"))));
            False(result.IsResolved, "a representation that is neither requested nor the fallback is rejected");
            Outcomes(result, ProbeOutcome.RepresentationMismatch);
        }

        // F6
        private static void Fallback_RecordKeepsTheAdoptedRepresentation()
        {
            var request = new ContentRequest("Entry", ContentKind.Scene, "Whitebox", fallbackRepresentation: "Full");
            var result = Resolve(request, Policy(Rule.All, Provider.Available("A", Cand("A", representation: "Full"))));
            var record = new ContentLoadRecord(request, result.Candidate!, result.Probes, "Scene(Entry)", ContentTraceability.Traceable, true);
            Equal("Full", record.Candidate.Representation, "record says what was adopted");
            Equal("Whitebox", record.Request.Representation, "request is not rewritten");
        }
    }
}
