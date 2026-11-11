#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using OneStarMaker.Runtime.ContentResolution;

namespace OneStarMaker.ContentResolutionOfflineTests
{
    // Tests for the product compatibility rule (ContentCompatibilityRule and its envelope / fact keys / verdict enum).
    // They share the fixtures and assertions of Program.cs. Host predicates here are test fixtures that compare
    // host-specific facts; the product types never read those keys.
    internal static partial class Program
    {
        private const string Engine = "engine-1";
        private const string HostNameKey = "host.declaration.name";
        private const string HostRepresentationKey = "host.declaration.representation";
        private const string HostGraphKey = "host.graph.version";

        private static readonly ContentCompatibilityEnvelope Env = new ContentCompatibilityEnvelope("set", "target", Engine, 2);

        private static void RunCompatibilityTests()
        {
            Run(nameof(Compat_CompatibleDirectoryCandidate_IsAdopted), Compat_CompatibleDirectoryCandidate_IsAdopted);
            Run(nameof(Compat_TargetMismatch_IsRejected_AndNextProviderIsTried), Compat_TargetMismatch_IsRejected_AndNextProviderIsTried);
            Run(nameof(Compat_EngineVersionMismatch_IsRejected), Compat_EngineVersionMismatch_IsRejected);
            Run(nameof(Compat_OlderRootSchema_IsRejected), Compat_OlderRootSchema_IsRejected);
            Run(nameof(Compat_MissingFacts_AreRejected), Compat_MissingFacts_AreRejected);
            Run(nameof(Compat_MalformedSchema_IsRejected), Compat_MalformedSchema_IsRejected);
            Run(nameof(Compat_IncompleteIdentity_IsRejected), Compat_IncompleteIdentity_IsRejected);
            Run(nameof(Compat_SameNameDifferentDigest_WithExpectedIdentity_IsNotAdopted), Compat_SameNameDifferentDigest_WithExpectedIdentity_IsNotAdopted);
            Run(nameof(Compat_ContentSetAndFixedRevision_MismatchIsRejected), Compat_ContentSetAndFixedRevision_MismatchIsRejected);
            Run(nameof(Compat_CaseDifference_IsMismatch), Compat_CaseDifference_IsMismatch);
            Run(nameof(Compat_WithoutHostPredicate_DeclarationFactsAreIgnored), Compat_WithoutHostPredicate_DeclarationFactsAreIgnored);
            Run(nameof(Compat_HostPredicates_DeclarationNameRepresentationAndGraph), Compat_HostPredicates_DeclarationNameRepresentationAndGraph);
            Run(nameof(Compat_HostPredicate_CannotLoosenGenericRejection), Compat_HostPredicate_CannotLoosenGenericRejection);
            Run(nameof(Compat_HostPredicateException_PropagatesUnchanged), Compat_HostPredicateException_PropagatesUnchanged);
            Run(nameof(Compat_HostPredicates_AllMustAccept_InOrder), Compat_HostPredicates_AllMustAccept_InOrder);
            Run(nameof(Compat_SnapshotWithEmptyIdentity_IsAdopted_WithoutDigestClaim), Compat_SnapshotWithEmptyIdentity_IsAdopted_WithoutDigestClaim);
            Run(nameof(Compat_UnsupportedSourceOrBasis_MatchesResolverTable), Compat_UnsupportedSourceOrBasis_MatchesResolverTable);
            Run(nameof(Compat_UnsetIsNotCompatible), Compat_UnsetIsNotCompatible);
            Run(nameof(Compat_Envelope_Validation), Compat_Envelope_Validation);
            Run(nameof(Compat_HostPredicateList_Validation_AndDefensiveCopy), Compat_HostPredicateList_Validation_AndDefensiveCopy);
            Run(nameof(Compat_RejectionReason_CarriesMismatchCode), Compat_RejectionReason_CarriesMismatchCode);
            Run(nameof(Compat_AllIncompatible_IsNoCompatibleCandidate), Compat_AllIncompatible_IsNoCompatibleCandidate);
            Run(nameof(Compat_PinMatchesButSchemaMismatch_IsNoCompatibleCandidate), Compat_PinMatchesButSchemaMismatch_IsNoCompatibleCandidate);
            Run(nameof(Compat_EvaluateIsDeterministic_AndDoesNotMutateInputs), Compat_EvaluateIsDeterministic_AndDoesNotMutateInputs);
            Run(nameof(Compat_NoHostPredicate_SameCandidateSameVerdict_AcrossSourceOrder), Compat_NoHostPredicate_SameCandidateSameVerdict_AcrossSourceOrder);
            Run(nameof(Compat_RepairAfterRejected_RequiresCompatibleCopy), Compat_RepairAfterRejected_RequiresCompatibleCopy);
            Run(nameof(Compat_HostPredicate_DoesNotDisplaceSnapshot), Compat_HostPredicate_DoesNotDisplaceSnapshot);
            Run(nameof(Compat_NewCoreFiles_AreDirectlyUnderCore_AndGeneric), Compat_NewCoreFiles_AreDirectlyUnderCore_AndGeneric);
            Run(nameof(Compat_UnspecifiedRepresentation_MayAdoptOtherRepresentation), Compat_UnspecifiedRepresentation_MayAdoptOtherRepresentation);
        }

        // C1
        private static void Compat_CompatibleDirectoryCandidate_IsAdopted()
        {
            var rule = new ContentCompatibilityRule(Env);
            var result = Resolve(Req(), Policy(rule, Provider.Available("A", DirCand("A"))));
            Equal("A", result.Candidate!.ProviderId, "compatible candidate is adopted");
            Outcomes(result, ProbeOutcome.Adopted);
            Equal("compat:Compatible:ContentDirectory", result.Probes[0].Reason, "adoption reason");
            Verdict(rule, Req(), DirCand("A"), ContentCompatibilityMismatch.Compatible);
        }

        // C2
        private static void Compat_TargetMismatch_IsRejected_AndNextProviderIsTried()
        {
            var rule = new ContentCompatibilityRule(Env);
            var a = Provider.Available("A", DirCand("A", identity: new ContentIdentity("set", "r1", "other-target", "digest-x")));
            var b = Provider.Available("B", DirCand("B"));
            var result = Resolve(Req(), Policy(rule, a, b));
            Equal("B", result.Candidate!.ProviderId, "next provider is adopted");
            Outcomes(result, ProbeOutcome.RuleRejected, ProbeOutcome.Adopted);
            StartsWith("compat:TargetMismatch:", result.Probes[0].Reason, "rejection reason");
        }

        // C3
        private static void Compat_EngineVersionMismatch_IsRejected()
        {
            var rule = new ContentCompatibilityRule(Env);
            var candidate = DirCand("A", facts: CompatFacts(engine: "engine-2"));
            Verdict(rule, Req(), candidate, ContentCompatibilityMismatch.EngineVersionMismatch);
            var result = Resolve(Req(), Policy(rule, Provider.Available("A", candidate)));
            Outcomes(result, ProbeOutcome.RuleRejected);
            Equal(ContentResolutionFailureCode.NoCompatibleCandidate, result.FailureCode, "failure code");
        }

        // C4
        private static void Compat_OlderRootSchema_IsRejected()
        {
            var rule = new ContentCompatibilityRule(Env);
            var candidate = DirCand("A", facts: CompatFacts(schema: "1"));
            Verdict(rule, Req(), candidate, ContentCompatibilityMismatch.SchemaVersionMismatch);
            var result = Resolve(Req(), Policy(rule, Provider.Available("A", candidate)));
            Outcomes(result, ProbeOutcome.RuleRejected);
            Equal(ContentResolutionFailureCode.NoCompatibleCandidate, result.FailureCode, "failure code");
        }

        // C5
        private static void Compat_MissingFacts_AreRejected()
        {
            var rule = new ContentCompatibilityRule(Env);
            foreach (var key in new[] { ContentCompatibilityFactKeys.EngineVersion, ContentCompatibilityFactKeys.RootSchemaVersion })
            {
                var expected = key == ContentCompatibilityFactKeys.EngineVersion
                    ? ContentCompatibilityMismatch.EngineVersionMissing
                    : ContentCompatibilityMismatch.SchemaVersionMissing;

                var withoutKey = CompatFacts();
                withoutKey.Remove(key);
                Verdict(rule, Req(), DirCand("A", facts: withoutKey), expected);

                foreach (var blank in new[] { null, "", " ", "\t" })
                {
                    var facts = CompatFacts();
                    facts[key] = blank!;
                    Verdict(rule, Req(), DirCand("A", facts: facts), expected);
                }

                var result = Resolve(Req(), Policy(rule, Provider.Available("A", DirCand("A", facts: withoutKey))));
                Outcomes(result, ProbeOutcome.RuleRejected);
                Equal(ContentResolutionFailureCode.NoCompatibleCandidate, result.FailureCode, "missing fact is not a match: " + key);
            }

            Verdict(rule, Req(), DirCand("A", facts: new Dictionary<string, string>()), ContentCompatibilityMismatch.EngineVersionMissing);
        }

        // C6
        private static void Compat_MalformedSchema_IsRejected()
        {
            var rule = new ContentCompatibilityRule(Env);
            foreach (var text in new[] { " 2", "2 ", "+2", "-1", "0", "02", "2.0", "２", "1234567890" })
                Verdict(rule, Req(), DirCand("A", facts: CompatFacts(schema: text)), ContentCompatibilityMismatch.SchemaVersionMalformed);

            var result = Resolve(Req(), Policy(rule, Provider.Available("A", DirCand("A", facts: CompatFacts(schema: "02")))));
            Outcomes(result, ProbeOutcome.RuleRejected);
            StartsWith("compat:SchemaVersionMalformed:", result.Probes[0].Reason, "reason");

            // The canonical upper bound is accepted by the parser and then compared with the envelope.
            Verdict(rule, Req(), DirCand("A", facts: CompatFacts(schema: "999999999")), ContentCompatibilityMismatch.SchemaVersionMismatch);
            var wide = new ContentCompatibilityRule(new ContentCompatibilityEnvelope("set", "target", Engine, 999999999));
            Verdict(wide, Req(), DirCand("A", facts: CompatFacts(schema: "999999999")), ContentCompatibilityMismatch.Compatible);
        }

        // C7
        private static void Compat_IncompleteIdentity_IsRejected()
        {
            var rule = new ContentCompatibilityRule(Env);
            Verdict(rule, Req(), DirCand("A", identity: new ContentIdentity("set", "r1", "target", "")), ContentCompatibilityMismatch.IncompleteIdentity);
            Verdict(rule, Req(), DirCand("A", identity: new ContentIdentity("set", " ", "target", "digest-x")), ContentCompatibilityMismatch.IncompleteIdentity);
            Verdict(rule, Req(), DirCand("A", identity: default(ContentIdentity)), ContentCompatibilityMismatch.IncompleteIdentity);

            var result = Resolve(Req(), Policy(rule, Provider.Available("A", DirCand("A", identity: new ContentIdentity("set", "r1", "target", "")))));
            Outcomes(result, ProbeOutcome.RuleRejected);
            Equal(ContentResolutionFailureCode.NoCompatibleCandidate, result.FailureCode, "failure code");
        }

        // C8
        private static void Compat_SameNameDifferentDigest_WithExpectedIdentity_IsNotAdopted()
        {
            var rule = new ContentCompatibilityRule(Env);
            var sameName = new ContentIdentity("set", "r1", "target", "digest-other");
            var result = Resolve(Req(expected: X), Policy(rule, Provider.Available("A", DirCand("A", identity: sameName))));
            False(result.IsResolved, "same name with another digest is not adopted");
            Outcomes(result, ProbeOutcome.PinMismatch);
            Equal(ContentResolutionFailureCode.IntegrityRejected, result.FailureCode, "failure code");

            Verdict(rule, Req(expected: X), DirCand("A", identity: sameName), ContentCompatibilityMismatch.ExpectedIdentityMismatch);
            Verdict(rule, Req(expected: X), SnapCand("S"), ContentCompatibilityMismatch.ExpectedIdentityMismatch);
            Verdict(rule, Req(expected: X), DirCand("A"), ContentCompatibilityMismatch.Compatible);
        }

        // C9
        private static void Compat_ContentSetAndFixedRevision_MismatchIsRejected()
        {
            var rule = new ContentCompatibilityRule(Env);
            var otherSet = new ContentIdentity("other-set", "r1", "target", "digest-x");
            Verdict(rule, Req(), DirCand("A", identity: otherSet), ContentCompatibilityMismatch.ContentSetMismatch);
            var result = Resolve(Req(), Policy(rule, Provider.Available("A", DirCand("A", identity: otherSet))));
            Outcomes(result, ProbeOutcome.RuleRejected);
            Equal(ContentResolutionFailureCode.NoCompatibleCandidate, result.FailureCode, "another content set is not adopted");

            var fixedRule = new ContentCompatibilityRule(new ContentCompatibilityEnvelope("set", "target", Engine, 2, "r1"));
            Verdict(fixedRule, Req(), DirCand("A", identity: XPrime), ContentCompatibilityMismatch.RevisionMismatch);
            Verdict(fixedRule, Req(), DirCand("A", identity: X), ContentCompatibilityMismatch.Compatible);

            // Without a fixed revision any compatible revision may be adopted.
            Verdict(rule, Req(), DirCand("A", identity: XPrime), ContentCompatibilityMismatch.Compatible);
            var unfixed = Resolve(Req(), Policy(rule, Provider.Available("A", DirCand("A", identity: XPrime))));
            Equal(XPrime, unfixed.Candidate!.Identity, "another revision is adopted when the envelope does not fix it");
        }

        // C10
        private static void Compat_CaseDifference_IsMismatch()
        {
            var rule = new ContentCompatibilityRule(Env);
            Verdict(rule, Req(), DirCand("A", identity: new ContentIdentity("set", "r1", "TARGET", "digest-x")), ContentCompatibilityMismatch.TargetMismatch);
            Verdict(rule, Req(), DirCand("A", identity: new ContentIdentity("SET", "r1", "target", "digest-x")), ContentCompatibilityMismatch.ContentSetMismatch);
            Verdict(rule, Req(), DirCand("A", facts: CompatFacts(engine: "ENGINE-1")), ContentCompatibilityMismatch.EngineVersionMismatch);
            Verdict(rule, Req(), DirCand("A", facts: CompatFacts(engine: Engine + " ")), ContentCompatibilityMismatch.EngineVersionMismatch);
        }

        // C11
        private static void Compat_WithoutHostPredicate_DeclarationFactsAreIgnored()
        {
            var rule = new ContentCompatibilityRule(Env);
            var plain = DirCand("A");
            var withHostFacts = DirCand("A", facts: CompatFacts(extra: new Dictionary<string, string>
            {
                [HostNameKey] = "Alpha",
                [HostRepresentationKey] = "Low",
                [HostGraphKey] = "7",
                [ContentCompatibilityFactKeys.ReservedPrefix + "unknownKey"] = "anything",
            }));
            var withEmptyHostFacts = DirCand("A", facts: CompatFacts(extra: new Dictionary<string, string> { [HostNameKey] = "" }));

            var plainReason = Verdict(rule, Req(), plain, ContentCompatibilityMismatch.Compatible);
            Equal(plainReason, Verdict(rule, Req(), withHostFacts, ContentCompatibilityMismatch.Compatible), "host facts do not change the reason");
            Equal(plainReason, Verdict(rule, Req(), withEmptyHostFacts, ContentCompatibilityMismatch.Compatible), "empty host facts do not change the reason");

            // An incompatible candidate stays incompatible with or without host facts.
            var schemaFacts = CompatFacts(schema: "1", extra: new Dictionary<string, string> { [HostNameKey] = "Alpha" });
            Verdict(rule, Req(), DirCand("A", facts: schemaFacts), ContentCompatibilityMismatch.SchemaVersionMismatch);
        }

        // C12
        private static void Compat_HostPredicates_DeclarationNameRepresentationAndGraph()
        {
            var name = HostPredicate.FactEquals(HostNameKey, "Alpha");
            var representation = HostPredicate.FactEqualsRequestRepresentation(HostRepresentationKey);
            var graph = HostPredicate.FactEquals(HostGraphKey, "7");
            var claiming = HostPredicate.Accepting("digest verified");
            var rule = new ContentCompatibilityRule(Env, new IContentCompatibilityRule[] { name, representation, graph, claiming });

            var matching = HostCand("A", "Alpha", "Full", "7");
            var reason = Verdict(rule, Req(), matching, ContentCompatibilityMismatch.Compatible);
            Equal("compat:Compatible:ContentDirectory", reason, "host predicate reasons are not appended");
            Equal(2, claiming.Calls, "all predicates are evaluated when every one accepts (once by Evaluate, once by Accepts)");
            var adopted = Resolve(Req(), Policy(rule, Provider.Available("A", matching)));
            Equal("compat:Compatible:ContentDirectory", adopted.Probes[0].Reason, "adoption reason through the resolver");

            Verdict(rule, Req(), HostCand("A", "Beta", "Full", "7"), ContentCompatibilityMismatch.HostPredicateRejected);
            Verdict(rule, Req(), HostCand("A", "Alpha", "Low", "7"), ContentCompatibilityMismatch.HostPredicateRejected);
            Verdict(rule, Req(), HostCand("A", "Alpha", "Full", "8"), ContentCompatibilityMismatch.HostPredicateRejected);
            Verdict(rule, Req(), DirCand("A"), ContentCompatibilityMismatch.HostPredicateRejected);

            // Empty expectations and empty facts never match.
            var emptyExpectation = new ContentCompatibilityRule(Env, new IContentCompatibilityRule[] { HostPredicate.FactEquals(HostNameKey, "") });
            Verdict(emptyExpectation, Req(), HostCand("A", "", "Full", "7"), ContentCompatibilityMismatch.HostPredicateRejected);
            Verdict(emptyExpectation, Req(), DirCand("A"), ContentCompatibilityMismatch.HostPredicateRejected);
            Verdict(rule, Req(), HostCand("A", "", "Full", "7"), ContentCompatibilityMismatch.HostPredicateRejected);
            Verdict(rule, Req(representation: ""), HostCand("A", "Alpha", "Full", "7"), ContentCompatibilityMismatch.HostPredicateRejected);

            // A declared representation that differs from the request is "another representation" and is not adopted.
            var other = Resolve(Req(), Policy(rule, Provider.Available("A", HostCand("A", "Alpha", "Low", "7"))));
            Outcomes(other, ProbeOutcome.RuleRejected);
            StartsWith("compat:HostPredicateRejected:", other.Probes[0].Reason, "reason");
            Equal(ContentResolutionFailureCode.NoCompatibleCandidate, other.FailureCode, "failure code");
        }

        // C13
        private static void Compat_HostPredicate_CannotLoosenGenericRejection()
        {
            var accepting = HostPredicate.Accepting("host says yes");
            var rule = new ContentCompatibilityRule(Env, new IContentCompatibilityRule[] { accepting });
            Verdict(rule, Req(), DirCand("A", identity: new ContentIdentity("set", "r1", "other-target", "digest-x")), ContentCompatibilityMismatch.TargetMismatch);
            Verdict(rule, Req(), DirCand("A", facts: CompatFacts(schema: "1")), ContentCompatibilityMismatch.SchemaVersionMismatch);
            Verdict(rule, Req(), DirCand("A", facts: new Dictionary<string, string>()), ContentCompatibilityMismatch.EngineVersionMissing);
            Verdict(rule, Req(expected: X), DirCand("A", identity: Y), ContentCompatibilityMismatch.ExpectedIdentityMismatch);
            Verdict(rule, Req(), Cand("A", basis: ContentAcceptanceBasis.None, facts: CompatFacts()), ContentCompatibilityMismatch.UnsupportedSource);
            Equal(0, accepting.Calls, "predicates are not consulted after a generic rejection");

            var result = Resolve(Req(), Policy(rule, Provider.Available("A", DirCand("A", facts: CompatFacts(schema: "1")))));
            Outcomes(result, ProbeOutcome.RuleRejected);
            Equal(0, accepting.Calls, "predicates are not consulted through the resolver either");
        }

        // C14
        private static void Compat_HostPredicateException_PropagatesUnchanged()
        {
            // (i) No pin: a predicate exception is a fault and stops the search.
            var throwing = new ContentCompatibilityRule(Env, new IContentCompatibilityRule[] { HostPredicate.Throwing(new InvalidOperationException("broken host")) });
            var b = Provider.Available("B", DirCand("B"));
            var fault = Resolve(Req(), Policy(throwing, Provider.Available("A", DirCand("A")), b));
            Outcomes(fault, ProbeOutcome.Fault);
            Equal(ContentResolutionFailureCode.ProviderFault, fault.FailureCode, "fault without a pin");
            Equal(0, b.Calls, "later providers are not probed");
            StartsWith("Rule threw: InvalidOperationException", fault.Probes[0].Reason, "the original exception type is reported");

            // (ii) An unmet request pin before the fault keeps the integrity outcome.
            var pinned = Resolve(Req(expected: X), Policy(throwing, Provider.Available("A", DirCand("A", identity: Y)), Provider.Available("B", DirCand("B"))));
            Outcomes(pinned, ProbeOutcome.PinMismatch, ProbeOutcome.Fault);
            Equal(ContentResolutionFailureCode.IntegrityRejected, pinned.FailureCode, "unmet pin wins over the fault");

            // (iii) The predicate cancels the very source passed to ResolveAsync, then throws its token's exception.
            using (var cts = new CancellationTokenSource())
            {
                var cancelling = new ContentCompatibilityRule(Env, new IContentCompatibilityRule[] { HostPredicate.CancellingThenThrowing(cts) });
                var later = Provider.Available("B", DirCand("B"));
                var policy = Policy(cancelling, Provider.Available("A", DirCand("A")), later);
                ThrowsCancelled(() => new ContentResolver().ResolveAsync(Req(), policy, cts.Token).AsTask().GetAwaiter().GetResult());
                Equal(0, later.Calls, "cancellation stops the search");
            }

            // (iv) A spurious OperationCanceledException with an uncancelled token is a fault.
            var spurious = new ContentCompatibilityRule(Env, new IContentCompatibilityRule[] { HostPredicate.Throwing(new OperationCanceledException("spurious")) });
            var spuriousResult = Resolve(Req(), Policy(spurious, Provider.Available("A", DirCand("A"))));
            Outcomes(spuriousResult, ProbeOutcome.Fault);
            Equal(ContentResolutionFailureCode.ProviderFault, spuriousResult.FailureCode, "uncancelled token");

            // (v) Direct calls see the very same exception instance.
            var instance = new FormatException("host format");
            var direct = new ContentCompatibilityRule(Env, new IContentCompatibilityRule[] { HostPredicate.Throwing(instance) });
            try
            {
                direct.Evaluate(Req(), DirCand("A"), out _);
                throw new InvalidOperationException("Expected the predicate exception");
            }
            catch (FormatException caught)
            {
                Same(instance, caught, "the exception is not wrapped");
            }

            try
            {
                direct.Accepts(Req(), DirCand("A"), out _);
                throw new InvalidOperationException("Expected the predicate exception from Accepts");
            }
            catch (FormatException caught)
            {
                Same(instance, caught, "Accepts does not wrap either");
            }
        }

        // C15
        private static void Compat_HostPredicates_AllMustAccept_InOrder()
        {
            var first = HostPredicate.Accepting("first");
            var second = HostPredicate.Rejecting("second says no");
            var third = HostPredicate.Accepting("third");
            var rule = new ContentCompatibilityRule(Env, new IContentCompatibilityRule[] { first, second, third });
            Equal(ContentCompatibilityMismatch.HostPredicateRejected, rule.Evaluate(Req(), DirCand("A"), out var reason), "verdict");
            Equal(1, first.Calls, "first is called");
            Equal(1, second.Calls, "second is called");
            Equal(0, third.Calls, "third is not called after a rejection");
            True(reason.Contains("host predicate 1"), "reason names the zero-based index: " + reason);
            True(reason.Contains("second says no"), "reason carries the predicate reason: " + reason);
        }

        // C16
        private static void Compat_SnapshotWithEmptyIdentity_IsAdopted_WithoutDigestClaim()
        {
            foreach (var predicates in new[] { new IContentCompatibilityRule[0], new IContentCompatibilityRule[] { HostPredicate.Accepting("digest verified") }, new IContentCompatibilityRule[] { HostPredicate.Rejecting("digest verified") } })
            {
                var rule = new ContentCompatibilityRule(Env, predicates);
                var snapshot = new Provider("S", ContentSourceKind.EditorSnapshot, _ => Answer(SnapCand("S")));
                var result = Resolve(Req(), Policy(rule, snapshot));
                Equal("S", result.Candidate!.ProviderId, "snapshot with an empty identity is adopted");
                var reason = result.Probes[0].Reason;
                Equal("compat:Compatible:EditorSnapshot", reason, "snapshot reason");
                NoDigestClaim(reason);
                NoDigestClaim(Verdict(rule, Req(), SnapCand("S"), ContentCompatibilityMismatch.Compatible));
                foreach (var predicate in predicates)
                    Equal(0, ((HostPredicate)predicate).Calls, "predicates are not applied to snapshots");
            }
        }

        // C17
        private static void Compat_UnsupportedSourceOrBasis_MatchesResolverTable()
        {
            var rule = new ContentCompatibilityRule(Env);
            var sources = new[] { ContentSourceKind.EditorSnapshot, ContentSourceKind.ContentDirectory, ContentSourceKind.Addressables };
            var bases = new[] { ContentAcceptanceBasis.None, ContentAcceptanceBasis.VerifiedDigest, ContentAcceptanceBasis.OpenSceneSnapshot, (ContentAcceptanceBasis)99 };
            foreach (var source in sources)
            {
                foreach (var basis in bases)
                {
                    var candidate = Cand("P", source: source, basis: basis, facts: CompatFacts());
                    var verdict = rule.Evaluate(Req(), candidate, out var reason);
                    var unsupported = verdict == ContentCompatibilityMismatch.UnsupportedSource;
                    var provider = new Provider("P", source, _ => Answer(candidate));
                    var result = Resolve(Req(), Policy(Rule.All, provider));
                    var noBasis = result.Probes[0].Outcome == ProbeOutcome.NoAcceptanceBasis;
                    Equal(noBasis, unsupported, $"rule and resolver agree for {source}/{basis}");
                    if (!unsupported)
                    {
                        Equal(ProbeOutcome.Adopted, result.Probes[0].Outcome, $"resolver adopts {source}/{basis}");
                        Equal(ContentCompatibilityMismatch.Compatible, verdict, $"rule accepts {source}/{basis}");
                    }
                    else
                    {
                        StartsWith("compat:UnsupportedSource:", reason, "reason");
                    }

                    if (basis == ContentAcceptanceBasis.None || basis == (ContentAcceptanceBasis)99)
                        True(unsupported, $"basis {basis} is never supported ({source})");
                }
            }
        }

        // C18
        private static void Compat_UnsetIsNotCompatible()
        {
            Equal(ContentCompatibilityMismatch.Unset, default(ContentCompatibilityMismatch), "default is Unset");
            True(default(ContentCompatibilityMismatch) != ContentCompatibilityMismatch.Compatible, "default is not Compatible");
            Equal(0, (int)ContentCompatibilityMismatch.Unset, "Unset is zero");

            var rule = new ContentCompatibilityRule(Env, new IContentCompatibilityRule[] { HostPredicate.FactEquals(HostNameKey, "Alpha") });
            var candidates = new[]
            {
                HostCand("A", "Alpha", "Full", "7"),
                HostCand("A", "Beta", "Full", "7"),
                DirCand("A", facts: CompatFacts(schema: "1")),
                SnapCand("S"),
                Cand("A", basis: ContentAcceptanceBasis.None, facts: CompatFacts()),
            };
            foreach (var candidate in candidates)
            {
                var verdict = rule.Evaluate(Req(), candidate, out var evaluateReason);
                var accepted = rule.Accepts(Req(), candidate, out var acceptsReason);
                Equal(verdict == ContentCompatibilityMismatch.Compatible, accepted, "Accepts is true only for Compatible: " + verdict);
                Equal(evaluateReason, acceptsReason, "Accepts returns the Evaluate reason");
                True(verdict != ContentCompatibilityMismatch.Unset, "the rule never returns Unset");
            }
        }

        // C19
        private static void Compat_Envelope_Validation()
        {
            foreach (var blank in new[] { null, "", " " })
            {
                Throws<ArgumentException>(() => new ContentCompatibilityEnvelope(blank!, "target", Engine, 2));
                Throws<ArgumentException>(() => new ContentCompatibilityEnvelope("set", blank!, Engine, 2));
                Throws<ArgumentException>(() => new ContentCompatibilityEnvelope("set", "target", blank!, 2));
            }

            foreach (var schema in new[] { 0, -1, 1000000000, int.MinValue, int.MaxValue })
                Throws<ArgumentOutOfRangeException>(() => new ContentCompatibilityEnvelope("set", "target", Engine, schema));

            Throws<ArgumentException>(() => new ContentCompatibilityEnvelope("set", "target", Engine, 2, " "));
            Throws<ArgumentException>(() => new ContentCompatibilityEnvelope("set", "target", Engine, 2, ""));

            var unfixed = new ContentCompatibilityEnvelope("set", "target", Engine, 1);
            Null(unfixed.Revision, "null revision means not fixed");
            Equal(1, unfixed.RootSchemaVersion, "lower bound");
            Equal(999999999, new ContentCompatibilityEnvelope("set", "target", Engine, 999999999).RootSchemaVersion, "upper bound");

            var full = new ContentCompatibilityEnvelope("set", "target", Engine, 2, "r1");
            Equal("set", full.ContentSet, "content set");
            Equal("target", full.Target, "target");
            Equal(Engine, full.EngineVersion, "engine");
            Equal(2, full.RootSchemaVersion, "schema");
            Equal("r1", full.Revision, "revision");
        }

        // C20
        private static void Compat_HostPredicateList_Validation_AndDefensiveCopy()
        {
            Throws<ArgumentNullException>(() => new ContentCompatibilityRule(null!));
            Throws<ArgumentException>(() => new ContentCompatibilityRule(Env, new IContentCompatibilityRule[] { HostPredicate.Accepting("a"), null! }));
            Verdict(new ContentCompatibilityRule(Env, null), Req(), DirCand("A"), ContentCompatibilityMismatch.Compatible);

            var list = new List<IContentCompatibilityRule> { HostPredicate.Accepting("a") };
            var rule = new ContentCompatibilityRule(Env, list);
            list.Add(HostPredicate.Rejecting("added later"));
            list[0] = HostPredicate.Rejecting("replaced later");
            Verdict(rule, Req(), DirCand("A"), ContentCompatibilityMismatch.Compatible);

            Throws<ArgumentNullException>(() => rule.Evaluate(null!, DirCand("A"), out _));
            Throws<ArgumentNullException>(() => rule.Evaluate(Req(), null!, out _));
        }

        // C21
        private static void Compat_RejectionReason_CarriesMismatchCode()
        {
            var name = new ContentCompatibilityRule(Env, new IContentCompatibilityRule[] { HostPredicate.FactEquals(HostNameKey, "Alpha") });
            var plain = new ContentCompatibilityRule(Env);
            var cases = new (ContentCompatibilityRule Rule, ContentCandidate Candidate, ContentCompatibilityMismatch Expected)[]
            {
                (plain, DirCand("A", identity: new ContentIdentity("set", "r1", "target", "")), ContentCompatibilityMismatch.IncompleteIdentity),
                (plain, DirCand("A", identity: new ContentIdentity("set", "r1", "other-target", "digest-x")), ContentCompatibilityMismatch.TargetMismatch),
                (plain, DirCand("A", facts: CompatFacts(engine: null)), ContentCompatibilityMismatch.EngineVersionMissing),
                (plain, DirCand("A", facts: CompatFacts(engine: "engine-2")), ContentCompatibilityMismatch.EngineVersionMismatch),
                (plain, DirCand("A", facts: CompatFacts(schema: null)), ContentCompatibilityMismatch.SchemaVersionMissing),
                (plain, DirCand("A", facts: CompatFacts(schema: "x")), ContentCompatibilityMismatch.SchemaVersionMalformed),
                (plain, DirCand("A", facts: CompatFacts(schema: "3")), ContentCompatibilityMismatch.SchemaVersionMismatch),
                (plain, DirCand("A", identity: new ContentIdentity("other-set", "r1", "target", "digest-x")), ContentCompatibilityMismatch.ContentSetMismatch),
                (new ContentCompatibilityRule(new ContentCompatibilityEnvelope("set", "target", Engine, 2, "r9")), DirCand("A"), ContentCompatibilityMismatch.RevisionMismatch),
                (name, DirCand("A"), ContentCompatibilityMismatch.HostPredicateRejected),
            };
            foreach (var c in cases)
            {
                var result = Resolve(Req(), Policy(c.Rule, Provider.Available("A", c.Candidate)));
                Outcomes(result, ProbeOutcome.RuleRejected);
                StartsWith("compat:" + c.Expected + ":", result.Probes[0].Reason, "probe reason for " + c.Expected);
            }

            // The expected identity mismatch is caught by the resolver pin before the rule, so it is checked directly.
            var direct = Verdict(plain, Req(expected: X), DirCand("A", identity: Y), ContentCompatibilityMismatch.ExpectedIdentityMismatch);
            StartsWith("compat:ExpectedIdentityMismatch:", direct, "direct reason");
        }

        // C22
        private static void Compat_AllIncompatible_IsNoCompatibleCandidate()
        {
            var rule = new ContentCompatibilityRule(Env);
            var result = Resolve(Req(), Policy(rule,
                Provider.Available("A", DirCand("A", facts: CompatFacts(schema: "1"))),
                Provider.NotFound("B"),
                Provider.Unavailable("C"),
                Provider.Available("D", DirCand("D", facts: CompatFacts(engine: "engine-2")))));
            Outcomes(result, ProbeOutcome.RuleRejected, ProbeOutcome.NotFound, ProbeOutcome.Unavailable, ProbeOutcome.RuleRejected);
            Equal(ContentResolutionFailureCode.NoCompatibleCandidate, result.FailureCode, "rule rejections without a pin");
            Null(result.PinnedIdentity, "no pin");
        }

        // C23
        private static void Compat_PinMatchesButSchemaMismatch_IsNoCompatibleCandidate()
        {
            var rule = new ContentCompatibilityRule(Env);
            var result = Resolve(Req(expected: X), Policy(rule, Provider.Available("A", DirCand("A", identity: X, facts: CompatFacts(schema: "1")))));
            Outcomes(result, ProbeOutcome.RuleRejected);
            Equal(ContentResolutionFailureCode.NoCompatibleCandidate, result.FailureCode, "pin matched, so no pin mismatch is recorded");
            Equal(X, result.PinnedIdentity!.Value, "pin");
        }

        // C24 is UnmetPin_ThenFault_IsIntegrityRejected in Program.cs.

        // C25
        private static void Compat_EvaluateIsDeterministic_AndDoesNotMutateInputs()
        {
            var rule = new ContentCompatibilityRule(Env, new IContentCompatibilityRule[] { HostPredicate.FactEquals(HostNameKey, "Alpha") });
            var request = Req(expected: X);
            foreach (var candidate in new[] { HostCand("A", "Alpha", "Full", "7"), HostCand("A", "Beta", "Full", "7"), DirCand("A", facts: CompatFacts(schema: "02")), SnapCand("S", identity: X) })
            {
                var factsBefore = new Dictionary<string, string>(candidate.Facts);
                var identityBefore = candidate.Identity;
                var first = rule.Evaluate(request, candidate, out var firstReason);
                var second = rule.Evaluate(request, candidate, out var secondReason);
                Equal(first, second, "same verdict");
                Equal(firstReason, secondReason, "same reason");
                Equal(identityBefore, candidate.Identity, "identity unchanged");
                Equal(factsBefore.Count, candidate.Facts.Count, "fact count unchanged");
                foreach (var pair in factsBefore)
                    Equal(pair.Value, candidate.Facts[pair.Key], "fact unchanged: " + pair.Key);
            }

            Equal(X, request.ExpectedIdentity!.Value, "request unchanged");
            Equal("Full", request.Representation, "request representation unchanged");
            Equal("set", Env.ContentSet, "envelope unchanged");
            Equal(2, Env.RootSchemaVersion, "envelope schema unchanged");
        }

        // C26
        private static void Compat_NoHostPredicate_SameCandidateSameVerdict_AcrossSourceOrder()
        {
            var rule = new ContentCompatibilityRule(Env);
            var a = DirCand("A", facts: CompatFacts(schema: "1"));
            var c = DirCand("C", identity: new ContentIdentity("set", "r1", "other-target", "digest-x"));
            var b = DirCand("B", identity: XPrime);

            var forward = Resolve(Req(), Policy(rule, Provider.Available("A", a), Provider.Available("C", c), Provider.Available("B", b)));
            var swapped = Resolve(Req(), Policy(rule, Provider.Available("C", c), Provider.Available("A", a), Provider.Available("B", b)));
            Equal("B", forward.Candidate!.ProviderId, "B is adopted (forward)");
            Equal("B", swapped.Candidate!.ProviderId, "B is adopted (swapped)");
            Equal(ProbeReason(forward, "A"), ProbeReason(swapped, "A"), "A has the same verdict in either order");
            Equal(ProbeReason(forward, "C"), ProbeReason(swapped, "C"), "C has the same verdict in either order");
            Equal(ProbeReason(forward, "B"), ProbeReason(swapped, "B"), "B has the same verdict in either order");
        }

        // C27
        private static void Compat_RepairAfterRejected_RequiresCompatibleCopy()
        {
            var rule = new ContentCompatibilityRule(Env);
            var incompatible = Resolve(Req(), Policy(rule, Provider.Rejected("A", X), Provider.Available("B", DirCand("B", identity: X, facts: CompatFacts(schema: "1")))));
            Outcomes(incompatible, ProbeOutcome.Rejected, ProbeOutcome.RuleRejected);
            Equal(ContentResolutionFailureCode.IntegrityRejected, incompatible.FailureCode, "repair-only state wins");

            var compatible = Resolve(Req(), Policy(rule, Provider.Rejected("A", X), Provider.Available("B", DirCand("B", identity: X))));
            Outcomes(compatible, ProbeOutcome.Rejected, ProbeOutcome.Adopted);
            Equal("B", compatible.Candidate!.ProviderId, "a compatible copy repairs");
        }

        // C28
        private static void Compat_HostPredicate_DoesNotDisplaceSnapshot()
        {
            var name = HostPredicate.FactEquals(HostNameKey, "Alpha");
            var rule = new ContentCompatibilityRule(Env, new IContentCompatibilityRule[] { name });
            var snapshot = new Provider("S", ContentSourceKind.EditorSnapshot, _ => Answer(SnapCand("S")));
            var local = Provider.Available("L", HostCand("L", "Alpha", "Full", "7"));
            var result = Resolve(Req(), Policy(rule, snapshot, local));
            Equal("S", result.Candidate!.ProviderId, "snapshot is adopted");
            Outcomes(result, ProbeOutcome.Adopted);
            Equal(0, local.Calls, "Local is not probed");
            Equal(0, name.Calls, "the predicate is not applied to the snapshot");
            Equal("compat:Compatible:EditorSnapshot", result.Probes[0].Reason, "snapshot reason");
        }

        // C29
        private static void Compat_NewCoreFiles_AreDirectlyUnderCore_AndGeneric()
        {
            var core = CoreDirectory();
            var newFiles = new[] { "ContentCompatibilityEnvelope.cs", "ContentCompatibilityFactKeys.cs", "ContentCompatibilityMismatch.cs", "ContentCompatibilityRule.cs" };
            foreach (var file in newFiles)
                True(File.Exists(Path.Combine(core, file)), "directly under Core: " + file);

            foreach (var file in Directory.GetFiles(core, "*.cs", SearchOption.AllDirectories))
                Equal(Path.GetFullPath(core), Path.GetFullPath(Path.GetDirectoryName(file)!), "no .cs file in a child folder of Core: " + file);

            // Words are assembled from pieces so that this source does not contain them.
            var words = new[]
            {
                "scene" + "director", "sample" + "game", "spr" + "ing", "sum" + "mer", "aut" + "umn", "win" + "ter", "sea" + "son",
                "ce" + "ll", "run" + "purpose", "scene" + "resourcemap", "ui" + "common", "s" + "vn", "work" + "set", "decla" + "ration",
            };
            foreach (var file in newFiles)
            {
                foreach (var line in File.ReadAllLines(Path.Combine(core, file)))
                {
                    var lower = line.ToLowerInvariant().Replace("cancel", string.Empty);
                    foreach (var word in words)
                        True(!lower.Contains(word), $"{file} contains a host word: {line}");
                }
            }
        }

        // C30
        private static void Compat_UnspecifiedRepresentation_MayAdoptOtherRepresentation()
        {
            var rule = new ContentCompatibilityRule(Env);
            var local = Provider.Available("Local", DirCand("Local", representation: "Full", facts: CompatFacts(schema: "1")));
            var lan = Provider.Available("Lan", DirCand("Lan", representation: "Low"));
            var unspecified = Resolve(Req(representation: ""), Policy(rule, local, lan));
            Equal("Lan", unspecified.Candidate!.ProviderId, "another representation may be adopted when the request does not specify one");
            Outcomes(unspecified, ProbeOutcome.RuleRejected, ProbeOutcome.Adopted);

            var local2 = Provider.Available("Local", DirCand("Local", representation: "Full", facts: CompatFacts(schema: "1")));
            var lan2 = Provider.Available("Lan", DirCand("Lan", representation: "Low"));
            var specified = Resolve(Req(representation: "Full"), Policy(rule, local2, lan2));
            Outcomes(specified, ProbeOutcome.RuleRejected, ProbeOutcome.RepresentationMismatch);
            Equal(ContentResolutionFailureCode.NoCompatibleCandidate, specified.FailureCode, "a specified representation is not switched");
        }

        // ---- compatibility fixtures ----

        private static Dictionary<string, string> CompatFacts(string? engine = Engine, string? schema = "2", IReadOnlyDictionary<string, string>? extra = null)
        {
            var facts = new Dictionary<string, string>(StringComparer.Ordinal);
            if (engine != null)
                facts[ContentCompatibilityFactKeys.EngineVersion] = engine;
            if (schema != null)
                facts[ContentCompatibilityFactKeys.RootSchemaVersion] = schema;
            if (extra != null)
            {
                foreach (var pair in extra)
                    facts[pair.Key] = pair.Value;
            }

            return facts;
        }

        private static ContentCandidate DirCand(string providerId, ContentIdentity? identity = null, IReadOnlyDictionary<string, string>? facts = null, string representation = "Full")
            => Cand(providerId, identity: identity, representation: representation, facts: facts ?? CompatFacts());

        private static ContentCandidate HostCand(string providerId, string name, string representation, string graph)
            => DirCand(providerId, facts: CompatFacts(extra: new Dictionary<string, string>
            {
                [HostNameKey] = name,
                [HostRepresentationKey] = representation,
                [HostGraphKey] = graph,
            }));

        private static ContentCandidate SnapCand(string providerId, ContentIdentity? identity = null)
            => Cand(providerId, source: ContentSourceKind.EditorSnapshot, identity: identity ?? default(ContentIdentity), basis: ContentAcceptanceBasis.OpenSceneSnapshot, loadPath: ContentLoadPath.AlreadyLoadedScene);

        private static string Verdict(ContentCompatibilityRule rule, ContentRequest request, ContentCandidate candidate, ContentCompatibilityMismatch expected)
        {
            var actual = rule.Evaluate(request, candidate, out var reason);
            Equal(expected, actual, "verdict (" + reason + ")");
            StartsWith("compat:" + expected + ":", reason, "reason prefix");
            var accepted = rule.Accepts(request, candidate, out var acceptsReason);
            Equal(expected == ContentCompatibilityMismatch.Compatible, accepted, "Accepts agrees with Evaluate");
            Equal(reason, acceptsReason, "Accepts reason");
            return reason;
        }

        private static string ProbeReason(ContentResolutionResult result, string providerId)
        {
            foreach (var probe in result.Probes)
            {
                if (probe.ProviderId == providerId)
                    return probe.Outcome + "|" + probe.Reason;
            }

            throw new InvalidOperationException("no probe for " + providerId);
        }

        private static void StartsWith(string prefix, string actual, string message)
        {
            if (!actual.StartsWith(prefix, StringComparison.Ordinal))
                throw new InvalidOperationException($"{message}: expected prefix '{prefix}', actual '{actual}'");
        }

        private static void NoDigestClaim(string reason)
        {
            True(reason.IndexOf("digest", StringComparison.OrdinalIgnoreCase) < 0, "reason claims a digest: " + reason);
            True(reason.IndexOf("verified", StringComparison.OrdinalIgnoreCase) < 0, "reason claims verification: " + reason);
        }

        // Host predicate fixtures. Empty expectations and empty / absent facts never match.
        private sealed class HostPredicate : IContentCompatibilityRule
        {
            private readonly Func<ContentRequest, ContentCandidate, bool> _accepts;
            private readonly string _reason;

            private HostPredicate(Func<ContentRequest, ContentCandidate, bool> accepts, string reason)
            {
                _accepts = accepts;
                _reason = reason;
            }

            internal int Calls { get; private set; }

            internal static HostPredicate FactEquals(string key, string expected)
                => new HostPredicate((_, c) => expected.Length != 0 && c.Facts.TryGetValue(key, out var value) && value.Length != 0 && string.Equals(value, expected, StringComparison.Ordinal), "fact " + key);

            internal static HostPredicate FactEqualsRequestRepresentation(string key)
                => new HostPredicate((r, c) => r.Representation.Length != 0 && c.Facts.TryGetValue(key, out var value) && value.Length != 0 && string.Equals(value, r.Representation, StringComparison.Ordinal), "fact " + key);

            internal static HostPredicate Accepting(string reason) => new HostPredicate((_, __) => true, reason);

            internal static HostPredicate Rejecting(string reason) => new HostPredicate((_, __) => false, reason);

            internal static HostPredicate Throwing(Exception exception) => new HostPredicate((_, __) => throw exception, "throws");

            // Accepts has no token, so the fixture captures the same source that was passed to ResolveAsync.
            internal static HostPredicate CancellingThenThrowing(CancellationTokenSource source)
                => new HostPredicate((_, __) =>
                {
                    source.Cancel();
                    throw new OperationCanceledException(source.Token);
                }, "cancels");

            public bool Accepts(ContentRequest request, ContentCandidate candidate, out string reason)
            {
                Calls++;
                var accepted = _accepts(request, candidate);
                reason = _reason;
                return accepted;
            }
        }
    }
}
