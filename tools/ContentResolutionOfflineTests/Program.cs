#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OneStarMaker.Runtime.ContentResolution;

namespace OneStarMaker.ContentResolutionOfflineTests
{
    // Dependency-free harness for the content resolution core. All production types are linked sources
    // from Runtime/ContentResolution/Core. Each test prints "PASS <name>" so the handoff can match names.
    internal static class Program
    {
        private static int _passed;
        private static int _failed;

        private static readonly ContentIdentity X = new ContentIdentity("set", "r1", "target", "digest-x");
        private static readonly ContentIdentity XPrime = new ContentIdentity("set", "r2", "target", "digest-x2");
        private static readonly ContentIdentity Y = new ContentIdentity("set", "r3", "target", "digest-y");

        private static int Main()
        {
            Run(nameof(Priority_FirstAvailableWins), Priority_FirstAvailableWins);
            Run(nameof(Priority_IsNotCompatibility), Priority_IsNotCompatibility);
            Run(nameof(Rejected_ThenDifferentIdentity_FailsClosed), Rejected_ThenDifferentIdentity_FailsClosed);
            Run(nameof(Rejected_ThenSameVerifiedIdentity_Repairs), Rejected_ThenSameVerifiedIdentity_Repairs);
            Run(nameof(AllNotFound_NoCandidate), AllNotFound_NoCandidate);
            Run(nameof(AllUnavailable_NoCandidate_WithDistinctProbes), AllUnavailable_NoCandidate_WithDistinctProbes);
            Run(nameof(Record_WithoutProvenance_IsRejected), Record_WithoutProvenance_IsRejected);
            Run(nameof(Request_Validation), Request_Validation);
            Run(nameof(Policy_Validation_AndDefensiveCopies), Policy_Validation_AndDefensiveCopies);
            Run(nameof(Candidate_WithDifferentKey_IsProviderFault), Candidate_WithDifferentKey_IsProviderFault);
            Run(nameof(ObjectKind_ResolvesAndKindMismatchIsRejected), ObjectKind_ResolvesAndKindMismatchIsRejected);
            Run(nameof(Cancelled_BeforeFirstProbe), Cancelled_BeforeFirstProbe);
            Run(nameof(Cancelled_WhilePending_SkipsLaterProviders), Cancelled_WhilePending_SkipsLaterProviders);
            Run(nameof(Provider_IgnoringToken_IsNotAdopted), Provider_IgnoringToken_IsNotAdopted);
            Run(nameof(CoreSources_DoNotNameUnityOrHosts), CoreSources_DoNotNameUnityOrHosts);
            Run(nameof(Representation_MustMatchWhenRequested), Representation_MustMatchWhenRequested);
            Run(nameof(Candidate_EmptyRepresentation_IsNotAdopted), Candidate_EmptyRepresentation_IsNotAdopted);
            Run(nameof(ExpectedIdentity_IsInitialPin), ExpectedIdentity_IsInitialPin);
            Run(nameof(Rejected_WithoutIdentity_StopsImmediately_AndPartialIdentityIsRejected), Rejected_WithoutIdentity_StopsImmediately_AndPartialIdentityIsRejected);
            Run(nameof(Repair_RequiresVerifiedCandidate), Repair_RequiresVerifiedCandidate);
            Run(nameof(ConflictingPins_FailClosed), ConflictingPins_FailClosed);
            Run(nameof(RuleException_IsFaultAndStops), RuleException_IsFaultAndStops);
            Run(nameof(Candidate_FromDifferentProvider_IsProviderMismatch), Candidate_FromDifferentProvider_IsProviderMismatch);
            Run(nameof(Candidate_WithoutAcceptanceBasis_IsNeverAdopted), Candidate_WithoutAcceptanceBasis_IsNeverAdopted);
            Run(nameof(Adoption_StopsProbing), Adoption_StopsProbing);
            Run(nameof(Repair_DoesNotDependOnProviderOrder), Repair_DoesNotDependOnProviderOrder);
            Run(nameof(FailureCode_Precedence), FailureCode_Precedence);
            Run(nameof(RequestPin_ConflictingRejected_StopsImmediately), RequestPin_ConflictingRejected_StopsImmediately);
            Console.WriteLine($"ContentResolution offline: {_passed} passed, {_failed} failed, {_passed + _failed} executed");
            return _failed == 0 && _passed > 0 ? 0 : 1;
        }

        // T1
        private static void Priority_FirstAvailableWins()
        {
            var a = Provider.NotFound("A");
            var b = Provider.Available("B", Cand("B"));
            var result = Resolve(Req(), Policy(Rule.All, a, b));
            True(result.IsResolved, "B is adopted");
            Equal("B", result.Candidate!.ProviderId, "adopted provider");
            Equal(ContentResolutionFailureCode.None, result.FailureCode, "failure code");
            Outcomes(result, ProbeOutcome.NotFound, ProbeOutcome.Adopted);
            Equal("A", result.Probes[0].ProviderId, "first probe");
            Equal("B", result.Probes[1].ProviderId, "second probe");
        }

        // T2
        private static void Priority_IsNotCompatibility()
        {
            var a = Provider.Available("A", Cand("A"));
            var b = Provider.Available("B", Cand("B"));
            var rule = Rule.Only("B");
            var result = Resolve(Req(), Policy(rule, a, b));
            Equal("B", result.Candidate!.ProviderId, "higher priority does not bypass the rule");
            Outcomes(result, ProbeOutcome.RuleRejected, ProbeOutcome.Adopted);
            Equal("rule rejected A", result.Probes[0].Reason, "rule reason is recorded");
        }

        // T3
        private static void Rejected_ThenDifferentIdentity_FailsClosed()
        {
            var a = Provider.Rejected("A", X);
            var b = Provider.Available("B", Cand("B", identity: Y));
            var result = Resolve(Req(), Policy(Rule.All, a, b));
            False(result.IsResolved, "a different revision is not adopted after a rejection");
            Null(result.Candidate, "no candidate");
            Equal(ContentResolutionFailureCode.IntegrityRejected, result.FailureCode, "failure code");
            Outcomes(result, ProbeOutcome.Rejected, ProbeOutcome.PinMismatch);
            Equal(X, result.PinnedIdentity!.Value, "pin from the rejection");
        }

        // T4
        private static void Rejected_ThenSameVerifiedIdentity_Repairs()
        {
            var result = Resolve(Req(), Policy(Rule.All, Provider.Rejected("A", X), Provider.Available("B", Cand("B", identity: X))));
            Equal("B", result.Candidate!.ProviderId, "verified copy of the same identity repairs");
            Equal(X, result.PinnedIdentity!.Value, "pinned identity");
            Outcomes(result, ProbeOutcome.Rejected, ProbeOutcome.Adopted);

            var withRequestPin = Resolve(Req(expected: X), Policy(Rule.All, Provider.Rejected("A", X), Provider.Available("B", Cand("B", identity: X))));
            Equal("B", withRequestPin.Candidate!.ProviderId, "a rejection carrying the request pin is not a conflict");
            Equal(X, withRequestPin.PinnedIdentity!.Value, "pinned identity with request pin");
        }

        // T5
        private static void AllNotFound_NoCandidate()
        {
            var result = Resolve(Req(), Policy(Rule.All, Provider.NotFound("A"), Provider.NotFound("B")));
            Equal(ContentResolutionFailureCode.NoCandidate, result.FailureCode, "failure code");
            Outcomes(result, ProbeOutcome.NotFound, ProbeOutcome.NotFound);
            Null(result.PinnedIdentity, "no pin");
        }

        // T6
        private static void AllUnavailable_NoCandidate_WithDistinctProbes()
        {
            var result = Resolve(Req(), Policy(Rule.All, Provider.Unavailable("A"), Provider.Unavailable("B")));
            Equal(ContentResolutionFailureCode.NoCandidate, result.FailureCode, "failure code");
            Outcomes(result, ProbeOutcome.Unavailable, ProbeOutcome.Unavailable);

            var mixed = Resolve(Req(), Policy(Rule.All, Provider.NotFound("A"), Provider.Unavailable("B")));
            Outcomes(mixed, ProbeOutcome.NotFound, ProbeOutcome.Unavailable);
            Equal("timeout", mixed.Probes[1].Reason, "unavailable reason is recorded");
        }

        // T7
        private static void Record_WithoutProvenance_IsRejected()
        {
            var request = Req();
            var probes = new ProviderProbe[0];
            Throws<ArgumentException>(() => new ContentLoadRecord(request, new ContentCandidate("A", ContentSourceKind.None, "Entry", ContentKind.Scene, "Full", X, null, ContentAcceptanceBasis.VerifiedDigest, ContentLoadPath.ContentDirectory), probes, "Scene(Entry)", ContentTraceability.Traceable, false));
            Throws<ArgumentException>(() => new ContentLoadRecord(request, new ContentCandidate("", ContentSourceKind.ContentDirectory, "Entry", ContentKind.Scene, "Full", X, null, ContentAcceptanceBasis.VerifiedDigest, ContentLoadPath.ContentDirectory), probes, "Scene(Entry)", ContentTraceability.Traceable, false));
            Throws<ArgumentException>(() => new ContentLoadRecord(request, Cand("A", basis: ContentAcceptanceBasis.None), probes, "Scene(Entry)", ContentTraceability.Traceable, false));
            Throws<ArgumentException>(() => new ContentLoadRecord(request, null!, probes, "Scene(Entry)", ContentTraceability.Traceable, false));
            Throws<ArgumentException>(() => new ContentLoadRecord(request, Cand("A"), probes, " ", ContentTraceability.Traceable, false));

            var record = new ContentLoadRecord(request, Cand("A"), probes, "Scene(Entry)", ContentTraceability.Traceable, false);
            Same(request, record.Request, "request is kept");
            False(record.SourceBindingVerified, "binding is not verified");
        }

        // T8
        private static void Request_Validation()
        {
            Throws<ArgumentException>(() => new ContentRequest("", ContentKind.Scene, "Full"));
            Throws<ArgumentException>(() => new ContentRequest("  ", ContentKind.Scene, "Full"));
            Throws<ArgumentException>(() => new ContentRequest(null!, ContentKind.Scene, "Full"));
            Throws<ArgumentException>(() => new ContentRequest("Entry", ContentKind.None, "Full"));
            Throws<ArgumentException>(() => new ContentRequest("Entry", (ContentKind)99, "Full"));
            Throws<ArgumentNullException>(() => new ContentRequest("Entry", ContentKind.Scene, null!));
            Equal(string.Empty, new ContentRequest("Entry", ContentKind.Scene, "").Representation, "empty representation is allowed");
            Throws<ArgumentException>(() => new ContentRequest("Entry", ContentKind.Scene, "Full", new ContentIdentity(null, null, null, "digest")));
            Throws<ArgumentException>(() => new ContentRequest("Entry", ContentKind.Scene, "Full", new ContentIdentity("set", "r1", null, null)));
            Throws<ArgumentException>(() => new ContentRequest("Entry", ContentKind.Scene, "Full", default(ContentIdentity)));
            Equal(X, new ContentRequest("Entry", ContentKind.Scene, "Full", X).ExpectedIdentity!.Value, "complete identity is allowed");
            Null(new ContentRequest("Entry", ContentKind.Scene, "Full").ExpectedIdentity, "no pin is allowed");
        }

        // T9
        private static void Policy_Validation_AndDefensiveCopies()
        {
            Throws<ArgumentException>(() => new ResolutionPolicy(new IContentProvider[0], Rule.All));
            Throws<ArgumentException>(() => new ResolutionPolicy(new IContentProvider[] { Provider.NotFound("A"), Provider.NotFound("A") }, Rule.All));
            Throws<ArgumentException>(() => new ResolutionPolicy(new IContentProvider[] { Provider.NotFound("") }, Rule.All));
            Throws<ArgumentException>(() => new ResolutionPolicy(new IContentProvider[] { Provider.NotFound("A") }, null!));
            Throws<ArgumentException>(() => new ResolutionPolicy(new IContentProvider[] { null! }, Rule.All));
            Throws<ArgumentException>(() => new ResolutionPolicy(null!, Rule.All));
            Throws<ArgumentException>(() => Cand(""));
            Throws<ArgumentException>(() => Cand(" "));

            // Ordinal: ids that differ only by case are distinct providers.
            new ResolutionPolicy(new IContentProvider[] { Provider.NotFound("a"), Provider.NotFound("A") }, Rule.All);

            var list = new List<IContentProvider> { Provider.NotFound("A") };
            var policy = new ResolutionPolicy(list, Rule.All);
            list.Add(Provider.NotFound("B"));
            list.Clear();
            Equal(1, policy.Providers.Count, "policy keeps its own copy");
            Equal("A", policy.Providers[0].Id, "policy provider");

            var facts = new Dictionary<string, string> { ["dirty"] = "false" };
            var candidate = Cand("A", facts: facts);
            facts["dirty"] = "true";
            facts["added"] = "x";
            Equal(1, candidate.Facts.Count, "candidate keeps its own facts");
            Equal("false", candidate.Facts["dirty"], "fact value");
            True(!(candidate.Facts is IDictionary<string, string> writable) || writable.IsReadOnly, "facts are read-only");
        }

        // T10
        private static void Candidate_WithDifferentKey_IsProviderFault()
        {
            var result = Resolve(Req(), Policy(Rule.All, Provider.Available("A", Cand("A", key: "Other")), Provider.Available("B", Cand("B"))));
            Equal("B", result.Candidate!.ProviderId, "next provider is probed");
            Outcomes(result, ProbeOutcome.KeyMismatch, ProbeOutcome.Adopted);

            var only = Resolve(Req(), Policy(Rule.All, Provider.Available("A", Cand("A", key: "Other"))));
            Equal(ContentResolutionFailureCode.ProviderFault, only.FailureCode, "failure code");
            Outcomes(only, ProbeOutcome.KeyMismatch);
        }

        // T11
        private static void ObjectKind_ResolvesAndKindMismatchIsRejected()
        {
            var request = Req("Widget", ContentKind.Object);
            var a = Provider.Available("A", Cand("A", key: "Widget", kind: ContentKind.Scene));
            var b = Provider.Available("B", Cand("B", key: "Widget", kind: ContentKind.Object));
            var result = Resolve(request, Policy(Rule.All, a, b));
            Equal("B", result.Candidate!.ProviderId, "object candidate is adopted");
            Equal(ContentKind.Object, result.Candidate.Kind, "kind");
            Outcomes(result, ProbeOutcome.KindMismatch, ProbeOutcome.Adopted);

            var only = Resolve(request, Policy(Rule.All, Provider.Available("A", Cand("A", key: "Widget", kind: ContentKind.Scene))));
            Equal(ContentResolutionFailureCode.ProviderFault, only.FailureCode, "failure code");
        }

        // T12
        private static void Cancelled_BeforeFirstProbe()
        {
            var a = Provider.Available("A", Cand("A"));
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            ThrowsCancelled(() => new ContentResolver().ResolveAsync(Req(), Policy(Rule.All, a), cts.Token).AsTask().GetAwaiter().GetResult());
            Equal(0, a.Calls, "no probe after cancellation");
        }

        // T12b
        private static void Cancelled_WhilePending_SkipsLaterProviders()
        {
            var pending = new Provider("A", ContentSourceKind.ContentDirectory, observeToken: true);
            var b = Provider.Available("B", Cand("B"));
            using var cts = new CancellationTokenSource();
            var task = new ContentResolver().ResolveAsync(Req(), Policy(Rule.All, pending, b), cts.Token).AsTask();
            False(task.IsCompleted, "the first probe is pending");
            Equal(1, pending.Calls, "A was asked");
            cts.Cancel();
            ThrowsCancelled(() => task.GetAwaiter().GetResult());
            Equal(0, b.Calls, "B is not probed after cancellation");
        }

        // T12c
        private static void Provider_IgnoringToken_IsNotAdopted()
        {
            var ignoring = new Provider("A", ContentSourceKind.ContentDirectory, observeToken: false);
            var b = Provider.Available("B", Cand("B"));
            using var cts = new CancellationTokenSource();
            var task = new ContentResolver().ResolveAsync(Req(), Policy(Rule.All, ignoring, b), cts.Token).AsTask();
            cts.Cancel();
            ignoring.Complete(new ContentAvailability(ContentAvailabilityStatus.Available, Cand("A")));
            ThrowsCancelled(() => task.GetAwaiter().GetResult());
            Equal(0, b.Calls, "B is not probed");

            // Without a cancelled token, an OperationCanceledException from a provider is a fault.
            var throwing = Provider.Throwing("A", new OperationCanceledException("spurious"));
            var result = Resolve(Req(), Policy(Rule.All, throwing, Provider.Available("B", Cand("B"))));
            Equal(ContentResolutionFailureCode.ProviderFault, result.FailureCode, "failure code");
            Outcomes(result, ProbeOutcome.Fault);
        }

        // T13
        private static void CoreSources_DoNotNameUnityOrHosts()
        {
            var coreDirectory = CoreDirectory();
            var files = Directory.GetFiles(coreDirectory, "*.cs");
            True(files.Length >= 20, "core files are found: " + coreDirectory);

            // Host and engine words are assembled from pieces so that this test's own source does not match the
            // repository-wide text check that covers this project.
            var forbidden = new[]
            {
                "Unity" + "Engine", "Cy" + "sharp", "Scene" + "Director", "Sample" + "Game", "Spr" + "ing", "Sea" + "son",
                "Run" + "Purpose", "Scene" + "ResourceMap", "UI" + "Common", "S" + "vn", "Work" + "set",
            };
            var forbiddenUsings = new[]
            {
                "using OneStarMaker.Runtime.AssetManagement", "using OneStarMaker.Runtime.BuildContent", "using OneStarMaker.Runtime.SceneSystem",
            };
            var gridWord = "ce" + "ll";
            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                var lines = File.ReadAllLines(file);
                Equal("#nullable enable", lines.Length > 0 ? lines[0] : string.Empty, name + " starts with #nullable enable");
                foreach (var line in lines)
                {
                    foreach (var word in forbidden)
                        True(line.IndexOf(word, StringComparison.OrdinalIgnoreCase) < 0, $"{name} names '{word}': {line}");
                    foreach (var directive in forbiddenUsings)
                        True(!line.TrimStart().StartsWith(directive, StringComparison.Ordinal), $"{name} has '{directive}'");
                    var withoutCancel = line.ToLowerInvariant().Replace("cancel", string.Empty);
                    True(!withoutCancel.Contains(gridWord), $"{name} names the grid word: {line}");
                    True(!Regex.IsMatch(line, @"\brecord\s+(class\s+|struct\s+)?[A-Z]"), $"{name} declares a record: {line}");
                    True(!Regex.IsMatch(line, @"\binit\s*;"), $"{name} uses an init accessor: {line}");
                }
            }
        }

        // T14
        private static void Representation_MustMatchWhenRequested()
        {
            var request = Req(representation: "Low");
            var result = Resolve(request, Policy(Rule.All, Provider.Available("A", Cand("A", representation: "Full")), Provider.Available("B", Cand("B", representation: "Low"))));
            Equal("B", result.Candidate!.ProviderId, "matching representation is adopted");
            Outcomes(result, ProbeOutcome.RepresentationMismatch, ProbeOutcome.Adopted);

            var only = Resolve(request, Policy(Rule.All, Provider.Available("A", Cand("A", representation: "Full"))));
            Equal(ContentResolutionFailureCode.NoCompatibleCandidate, only.FailureCode, "failure code");

            var unspecified = Resolve(Req(representation: ""), Policy(Rule.All, Provider.Available("A", Cand("A", representation: "Full"))));
            Equal("Full", unspecified.Candidate!.Representation, "unspecified request takes the candidate representation");

            var request2 = Req(representation: "");
            var record = new ContentLoadRecord(request2, unspecified.Candidate, unspecified.Probes, "Scene(Entry)", ContentTraceability.Traceable, false);
            Equal("Full", record.Candidate.Representation, "record representation is the candidate's");
            Equal(string.Empty, request2.Representation, "request is not changed");
        }

        // T15
        private static void Candidate_EmptyRepresentation_IsNotAdopted()
        {
            Throws<ArgumentException>(() => Cand("A", representation: ""));
            Throws<ArgumentNullException>(() => Cand("A", representation: null!));
        }

        // T16
        private static void ExpectedIdentity_IsInitialPin()
        {
            var request = Req(expected: X);
            var result = Resolve(request, Policy(Rule.All, Provider.Available("A", Cand("A", identity: Y)), Provider.Available("B", Cand("B", identity: X))));
            Equal("B", result.Candidate!.ProviderId, "pinned identity is adopted");
            Equal(X, result.PinnedIdentity!.Value, "pinned identity");
            Outcomes(result, ProbeOutcome.PinMismatch, ProbeOutcome.Adopted);

            var only = Resolve(request, Policy(Rule.All, Provider.Available("A", Cand("A", identity: Y))));
            Equal(ContentResolutionFailureCode.IntegrityRejected, only.FailureCode, "failure code");
            Equal(X, only.PinnedIdentity!.Value, "pin is reported on failure");
        }

        // T17
        private static void Rejected_WithoutIdentity_StopsImmediately_AndPartialIdentityIsRejected()
        {
            var b = Provider.Available("B", Cand("B"));
            var result = Resolve(Req(), Policy(Rule.All, Provider.Rejected("A", null), b));
            Equal(ContentResolutionFailureCode.IntegrityRejected, result.FailureCode, "failure code");
            Outcomes(result, ProbeOutcome.Rejected);
            Equal(0, b.Calls, "B is not probed");
            Null(result.PinnedIdentity, "no pin");

            Throws<ArgumentException>(() => new ContentAvailability(ContentAvailabilityStatus.Rejected, null, new ContentIdentity("set", "r1", "target", null)));
            Throws<ArgumentException>(() => new ContentAvailability(ContentAvailabilityStatus.Rejected, null, default(ContentIdentity)));

            var sameDigestOtherTarget = new ContentIdentity("set", "r1", "other-target", "digest-x");
            False(sameDigestOtherTarget.MatchesPin(X), "same digest with a different target does not match");
            False(default(ContentIdentity).MatchesPin(X), "an empty identity does not match");
            False(new ContentIdentity(null, null, null, "digest-x").MatchesPin(X), "a partial identity does not match");
            True(new ContentIdentity("set", "r1", "target", "digest-x").MatchesPin(X), "full equality matches");
            Throws<ArgumentException>(() => X.MatchesPin(new ContentIdentity(null, null, null, "digest-x")));

            var emptyCandidate = Resolve(Req(), Policy(Rule.All, Provider.Rejected("A", X), Provider.Available("B", Cand("B", identity: default(ContentIdentity)))));
            Outcomes(emptyCandidate, ProbeOutcome.Rejected, ProbeOutcome.PinMismatch);
        }

        // T18
        private static void Repair_RequiresVerifiedCandidate()
        {
            foreach (var basis in new[] { ContentAcceptanceBasis.None, ContentAcceptanceBasis.OpenSceneSnapshot })
            {
                var result = Resolve(Req(), Policy(Rule.All, Provider.Rejected("A", X), Provider.Available("B", Cand("B", identity: X, basis: basis))));
                Outcomes(result, ProbeOutcome.Rejected, ProbeOutcome.NoAcceptanceBasis);
                Equal(ContentResolutionFailureCode.IntegrityRejected, result.FailureCode, "unmet pin after rejection: " + basis);
            }

            // A snapshot candidate is normally acceptable, but not as a repair of a rejected identity.
            var snapshot = new Provider("S", ContentSourceKind.EditorSnapshot, _ => Answer(Cand("S", source: ContentSourceKind.EditorSnapshot, identity: X, basis: ContentAcceptanceBasis.OpenSceneSnapshot)));
            var repaired = Resolve(Req(), Policy(Rule.All, Provider.Rejected("A", X), snapshot));
            Outcomes(repaired, ProbeOutcome.Rejected, ProbeOutcome.NoAcceptanceBasis);
            Equal(ContentResolutionFailureCode.IntegrityRejected, repaired.FailureCode, "snapshot does not repair");
        }

        // T19
        private static void ConflictingPins_FailClosed()
        {
            var c = Provider.Available("C", Cand("C", identity: XPrime));
            var result = Resolve(Req(), Policy(Rule.All, Provider.Rejected("A", X), Provider.Rejected("B", XPrime), c));
            Equal(ContentResolutionFailureCode.IntegrityRejected, result.FailureCode, "failure code");
            Outcomes(result, ProbeOutcome.Rejected, ProbeOutcome.Rejected);
            Equal(0, c.Calls, "C is not probed");
            Equal(X, result.PinnedIdentity!.Value, "the first pin is kept");
        }

        // T20
        private static void RuleException_IsFaultAndStops()
        {
            var b = Provider.Available("B", Cand("B"));
            var result = Resolve(Req(), Policy(Rule.Throwing(new InvalidOperationException("broken rule")), Provider.Available("A", Cand("A")), b));
            Equal(ContentResolutionFailureCode.ProviderFault, result.FailureCode, "failure code");
            Outcomes(result, ProbeOutcome.Fault);
            Equal(0, b.Calls, "B is not probed");

            var defaultAnswer = new Provider("A", ContentSourceKind.ContentDirectory, _ => new ValueTask<ContentAvailability>(default(ContentAvailability)));
            var b2 = Provider.Available("B", Cand("B"));
            var broken = Resolve(Req(), Policy(Rule.All, defaultAnswer, b2));
            Equal(ContentResolutionFailureCode.ProviderFault, broken.FailureCode, "default answer is a fault");
            Outcomes(broken, ProbeOutcome.Fault);
            Equal(0, b2.Calls, "B is not probed after a default answer");

            var throwingProvider = Resolve(Req(), Policy(Rule.All, Provider.Throwing("A", new IOException("disk")), Provider.Available("B", Cand("B"))));
            Equal(ContentResolutionFailureCode.ProviderFault, throwingProvider.FailureCode, "provider exception is a fault");

            Throws<ArgumentException>(() => new ContentAvailability(ContentAvailabilityStatus.Available));
            Throws<ArgumentException>(() => new ContentAvailability(ContentAvailabilityStatus.NotFound, Cand("A")));
            Throws<ArgumentException>(() => new ContentAvailability(ContentAvailabilityStatus.NotFound, null, X));
            Throws<ArgumentException>(() => new ContentAvailability(ContentAvailabilityStatus.Rejected, Cand("A"), X));
            Throws<ArgumentException>(() => new ContentAvailability(ContentAvailabilityStatus.None));
        }

        // T21
        private static void Candidate_FromDifferentProvider_IsProviderMismatch()
        {
            var claimsOther = Resolve(Req(), Policy(Rule.All, Provider.Available("A", Cand("B")), Provider.Available("C", Cand("C"))));
            Outcomes(claimsOther, ProbeOutcome.ProviderMismatch, ProbeOutcome.Adopted);
            Equal("A", claimsOther.Probes[0].ProviderId, "the probe records the probed provider");

            var otherSource = Resolve(Req(), Policy(Rule.All, Provider.Available("A", Cand("A", source: ContentSourceKind.EditorSnapshot, basis: ContentAcceptanceBasis.OpenSceneSnapshot))));
            Outcomes(otherSource, ProbeOutcome.ProviderMismatch);
            Equal(ContentResolutionFailureCode.ProviderFault, otherSource.FailureCode, "failure code");
        }

        // T22
        private static void Candidate_WithoutAcceptanceBasis_IsNeverAdopted()
        {
            foreach (var basis in new[] { ContentAcceptanceBasis.None, ContentAcceptanceBasis.OpenSceneSnapshot })
            {
                var result = Resolve(Req(), Policy(Rule.All, Provider.Available("A", Cand("A", basis: basis))));
                Outcomes(result, ProbeOutcome.NoAcceptanceBasis);
                Equal(ContentResolutionFailureCode.NoCompatibleCandidate, result.FailureCode, "directory candidate without verified digest: " + basis);
            }

            var snapshot = new Provider("S", ContentSourceKind.EditorSnapshot, _ => Answer(Cand("S", source: ContentSourceKind.EditorSnapshot, basis: ContentAcceptanceBasis.OpenSceneSnapshot)));
            var adopted = Resolve(Req(), Policy(Rule.All, snapshot));
            True(adopted.IsResolved, "snapshot candidate with an open-scene basis is adopted");

            foreach (var basis in new[] { ContentAcceptanceBasis.None, ContentAcceptanceBasis.VerifiedDigest, ContentAcceptanceBasis.OpenSceneSnapshot })
            {
                var addressables = new Provider("D", ContentSourceKind.Addressables, _ => Answer(Cand("D", source: ContentSourceKind.Addressables, basis: basis, loadPath: ContentLoadPath.Addressables)));
                var result = Resolve(Req(), Policy(Rule.All, addressables));
                Outcomes(result, ProbeOutcome.NoAcceptanceBasis);
            }
        }

        // T23
        private static void Adoption_StopsProbing()
        {
            var b = Provider.Available("B", Cand("B"));
            var result = Resolve(Req(), Policy(Rule.All, Provider.Available("A", Cand("A")), b));
            Equal("A", result.Candidate!.ProviderId, "A is adopted");
            Outcomes(result, ProbeOutcome.Adopted);
            Equal(0, b.Calls, "B is not probed");
        }

        // T24
        private static void Repair_DoesNotDependOnProviderOrder()
        {
            var result = Resolve(Req(), Policy(Rule.All, Provider.Rejected("A", X), Provider.Available("B", Cand("B", identity: Y)), Provider.Available("C", Cand("C", identity: X))));
            Equal("C", result.Candidate!.ProviderId, "C repairs");
            Outcomes(result, ProbeOutcome.Rejected, ProbeOutcome.PinMismatch, ProbeOutcome.Adopted);
            Equal(X, result.PinnedIdentity!.Value, "pinned identity");
        }

        // T25
        private static void FailureCode_Precedence()
        {
            var pinAndKey = Resolve(Req(expected: X), Policy(Rule.All, Provider.Available("A", Cand("A", identity: Y)), Provider.Available("B", Cand("B", key: "Other"))));
            Outcomes(pinAndKey, ProbeOutcome.PinMismatch, ProbeOutcome.KeyMismatch);
            Equal(ContentResolutionFailureCode.IntegrityRejected, pinAndKey.FailureCode, "pin mismatch wins");

            var keyAndRule = Resolve(Req(), Policy(Rule.Only("none"), Provider.Available("A", Cand("A", key: "Other")), Provider.Available("B", Cand("B"))));
            Outcomes(keyAndRule, ProbeOutcome.KeyMismatch, ProbeOutcome.RuleRejected);
            Equal(ContentResolutionFailureCode.ProviderFault, keyAndRule.FailureCode, "key mismatch wins over rule");

            var ruleAndNotFound = Resolve(Req(), Policy(Rule.Only("none"), Provider.Available("A", Cand("A")), Provider.NotFound("B")));
            Outcomes(ruleAndNotFound, ProbeOutcome.RuleRejected, ProbeOutcome.NotFound);
            Equal(ContentResolutionFailureCode.NoCompatibleCandidate, ruleAndNotFound.FailureCode, "rule wins over not found");

            var notFoundAndUnavailable = Resolve(Req(), Policy(Rule.All, Provider.NotFound("A"), Provider.Unavailable("B")));
            Equal(ContentResolutionFailureCode.NoCandidate, notFoundAndUnavailable.FailureCode, "absence only");
        }

        // T26
        private static void RequestPin_ConflictingRejected_StopsImmediately()
        {
            var b = Provider.Available("B", Cand("B", identity: XPrime));
            var result = Resolve(Req(expected: X), Policy(Rule.All, Provider.Rejected("A", XPrime), b));
            Equal(ContentResolutionFailureCode.IntegrityRejected, result.FailureCode, "failure code");
            Outcomes(result, ProbeOutcome.Rejected);
            Equal(0, b.Calls, "B is not probed");
            Equal(X, result.PinnedIdentity!.Value, "request pin is kept");
        }

        // ---- fixtures ----

        private static ContentRequest Req(string key = "Entry", ContentKind kind = ContentKind.Scene, string representation = "Full", ContentIdentity? expected = null)
            => new ContentRequest(key, kind, representation, expected);

        private static ContentCandidate Cand(
            string providerId,
            ContentSourceKind source = ContentSourceKind.ContentDirectory,
            string key = "Entry",
            ContentKind kind = ContentKind.Scene,
            string representation = "Full",
            ContentIdentity? identity = null,
            ContentAcceptanceBasis basis = ContentAcceptanceBasis.VerifiedDigest,
            ContentLoadPath loadPath = ContentLoadPath.ContentDirectory,
            IReadOnlyDictionary<string, string>? facts = null)
            => new ContentCandidate(providerId, source, key, kind, representation, identity ?? X, "manifest://opaque", basis, loadPath, facts);

        private static ValueTask<ContentAvailability> Answer(ContentCandidate candidate)
            => new ValueTask<ContentAvailability>(new ContentAvailability(ContentAvailabilityStatus.Available, candidate));

        private static ResolutionPolicy Policy(IContentCompatibilityRule rule, params IContentProvider[] providers)
            => new ResolutionPolicy(providers, rule);

        private static ContentResolutionResult Resolve(ContentRequest request, ResolutionPolicy policy)
        {
            var task = new ContentResolver().ResolveAsync(request, policy, CancellationToken.None);
            True(task.IsCompleted, "resolution with completed probes finishes synchronously");
            var result = task.Result;
            Equal(result.IsResolved, result.FailureCode == ContentResolutionFailureCode.None, "failure code consistency");
            return result;
        }

        private static string CoreDirectory([CallerFilePath] string sourcePath = "")
        {
            var projectDirectory = Path.GetDirectoryName(sourcePath) ?? ".";
            return Path.GetFullPath(Path.Combine(projectDirectory, "..", "..", "unity", "Assets", "OneStarMaker", "Scripts", "Runtime", "ContentResolution", "Core"));
        }

        private sealed class Provider : IContentProvider
        {
            private readonly Func<CancellationToken, ValueTask<ContentAvailability>>? _answer;
            private readonly bool _observeToken;
            private TaskCompletionSource<ContentAvailability>? _pending;

            internal Provider(string id, ContentSourceKind sourceKind, Func<CancellationToken, ValueTask<ContentAvailability>> answer)
            {
                Id = id;
                SourceKind = sourceKind;
                _answer = answer;
            }

            // Pending provider: the answer is completed by the test through a TaskCompletionSource signal.
            internal Provider(string id, ContentSourceKind sourceKind, bool observeToken)
            {
                Id = id;
                SourceKind = sourceKind;
                _observeToken = observeToken;
            }

            public string Id { get; }

            public ContentSourceKind SourceKind { get; }

            internal int Calls { get; private set; }

            public ValueTask<ContentAvailability> FindAsync(ContentRequest request, CancellationToken cancellationToken)
            {
                Calls++;
                if (_answer != null)
                    return _answer(cancellationToken);
                _pending = new TaskCompletionSource<ContentAvailability>(TaskCreationOptions.RunContinuationsAsynchronously);
                if (_observeToken)
                {
                    var pending = _pending;
                    cancellationToken.Register(() => pending.TrySetCanceled(cancellationToken));
                }

                return new ValueTask<ContentAvailability>(_pending.Task);
            }

            internal void Complete(ContentAvailability answer) => _pending!.TrySetResult(answer);

            internal static Provider Available(string id, ContentCandidate candidate)
                => new Provider(id, ContentSourceKind.ContentDirectory, _ => Answer(candidate));

            internal static Provider NotFound(string id)
                => new Provider(id, ContentSourceKind.ContentDirectory, _ => new ValueTask<ContentAvailability>(new ContentAvailability(ContentAvailabilityStatus.NotFound, reason: "absent")));

            internal static Provider Unavailable(string id)
                => new Provider(id, ContentSourceKind.ContentDirectory, _ => new ValueTask<ContentAvailability>(new ContentAvailability(ContentAvailabilityStatus.Unavailable, reason: "timeout")));

            internal static Provider Rejected(string id, ContentIdentity? expected)
                => new Provider(id, ContentSourceKind.ContentDirectory, _ => new ValueTask<ContentAvailability>(new ContentAvailability(ContentAvailabilityStatus.Rejected, null, expected, "hash mismatch")));

            internal static Provider Throwing(string id, Exception exception)
                => new Provider(id, ContentSourceKind.ContentDirectory, _ => throw exception);
        }

        private sealed class Rule : IContentCompatibilityRule
        {
            private readonly Func<ContentCandidate, bool> _accepts;
            private readonly Exception? _throw;

            private Rule(Func<ContentCandidate, bool> accepts, Exception? toThrow = null)
            {
                _accepts = accepts;
                _throw = toThrow;
            }

            internal static readonly Rule All = new Rule(_ => true);

            internal static Rule Only(string providerId) => new Rule(c => c.ProviderId == providerId);

            internal static Rule Throwing(Exception exception) => new Rule(_ => true, exception);

            public bool Accepts(ContentRequest request, ContentCandidate candidate, out string reason)
            {
                if (_throw != null)
                    throw _throw;
                var accepted = _accepts(candidate);
                reason = accepted ? "accepted" : "rule rejected " + candidate.ProviderId;
                return accepted;
            }
        }

        // ---- assertions ----

        private static void Run(string name, Action body)
        {
            try { body(); _passed++; Console.WriteLine("PASS " + name); }
            catch (Exception exception) { _failed++; Console.WriteLine("FAIL " + name + "\n" + exception); }
        }

        private static void Outcomes(ContentResolutionResult result, params ProbeOutcome[] expected)
        {
            var actual = new List<string>();
            foreach (var probe in result.Probes) actual.Add(probe.Outcome.ToString());
            Equal(string.Join(",", expected), string.Join(",", actual), "probe outcomes");
        }

        private static void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException($"{message}: expected {expected}, actual {actual}");
        }

        private static void Same(object? expected, object? actual, string message)
        {
            if (!ReferenceEquals(expected, actual))
                throw new InvalidOperationException(message + ": references differ");
        }

        private static void True(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        private static void False(bool value, string message) => True(!value, message);

        private static void Null<T>(T? value, string message) where T : struct
        {
            if (value.HasValue) throw new InvalidOperationException(message + ": expected null, actual " + value.Value);
        }

        private static void Null(object? value, string message)
        {
            if (value != null) throw new InvalidOperationException(message + ": expected null");
        }

        private static void Throws<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            catch (Exception other) { throw new InvalidOperationException($"Expected {typeof(T).Name}, got {other.GetType().Name}: {other.Message}"); }
            throw new InvalidOperationException($"Expected {typeof(T).Name}, but nothing was thrown");
        }

        private static void Throws<T>(Func<object> func) where T : Exception => Throws<T>(() => { func(); });

        private static void ThrowsCancelled(Action action)
        {
            try { action(); }
            catch (OperationCanceledException) { return; }
            catch (Exception other) { throw new InvalidOperationException($"Expected OperationCanceledException, got {other.GetType().Name}: {other.Message}"); }
            throw new InvalidOperationException("Expected OperationCanceledException, but nothing was thrown");
        }
    }
}
