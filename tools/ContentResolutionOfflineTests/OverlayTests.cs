#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Runtime.ContentResolution;

namespace OneStarMaker.ContentResolutionOfflineTests
{
    // Tests for the overlay source (ContentOverlay + RecordedBaseMatch): the resolver's fixed basis table and the
    // overlay branch of the product compatibility rule. The overlay candidate carries its own identity with an empty digest
    // and the recorded base as facts; the rule compares the recorded base with the envelope, row by row in a fixed order.
    internal static partial class Program
    {
        private const string BaseDigest = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        private static readonly ContentIdentity OverlayIdentity = new ContentIdentity("ovset", "ovrev", "target", "");

        private static void RunOverlayTests()
        {
            Run(nameof(Overlay_CompatibleCandidate_IsAdopted_WithOverlayReason), Overlay_CompatibleCandidate_IsAdopted_WithOverlayReason);
            Run(nameof(Overlay_SourceAndBasisTable_MatchesResolverTable), Overlay_SourceAndBasisTable_MatchesResolverTable);
            Run(nameof(Overlay_MissingBaseFact_IsOverlayBaseFactMissing), Overlay_MissingBaseFact_IsOverlayBaseFactMissing);
            Run(nameof(Overlay_EachBranchRow_RejectsWithItsValue), Overlay_EachBranchRow_RejectsWithItsValue);
            Run(nameof(Overlay_RowsAreEvaluatedInOrder), Overlay_RowsAreEvaluatedInOrder);
            Run(nameof(Overlay_PinnedRequest_IsNotAdopted), Overlay_PinnedRequest_IsNotAdopted);
            Run(nameof(Overlay_HostPredicate_IsApplied), Overlay_HostPredicate_IsApplied);
            Run(nameof(Overlay_CannotRepairARejectedIdentity), Overlay_CannotRepairARejectedIdentity);
        }

        // O1
        private static void Overlay_CompatibleCandidate_IsAdopted_WithOverlayReason()
        {
            var rule = new ContentCompatibilityRule(Env);
            var result = Resolve(Req(), Policy(rule, OverlayProvider("O", OverlayCand("O"))));
            Equal("O", result.Candidate!.ProviderId, "compatible overlay candidate is adopted");
            Outcomes(result, ProbeOutcome.Adopted);
            Equal("compat:Compatible:ContentOverlay", result.Probes[0].Reason, "adoption reason names the overlay source");
            var reason = Verdict(rule, Req(), OverlayCand("O"), ContentCompatibilityMismatch.Compatible);
            NoDigestClaim(reason);
        }

        // O2: the resolver table and the rule agree on every source / basis pair, including the overlay pair.
        private static void Overlay_SourceAndBasisTable_MatchesResolverTable()
        {
            var rule = new ContentCompatibilityRule(Env);
            var sources = new[] { ContentSourceKind.EditorSnapshot, ContentSourceKind.ContentDirectory, ContentSourceKind.Addressables, ContentSourceKind.ContentOverlay };
            var bases = new[] { ContentAcceptanceBasis.None, ContentAcceptanceBasis.VerifiedDigest, ContentAcceptanceBasis.OpenSceneSnapshot, ContentAcceptanceBasis.RecordedBaseMatch, (ContentAcceptanceBasis)99 };
            var accepted = new List<string>();
            foreach (var source in sources)
            {
                foreach (var basis in bases)
                {
                    var candidate = Cand("P", source: source, basis: basis, facts: OverlayFacts());
                    var verdict = rule.Evaluate(Req(), candidate, out _);
                    var unsupported = verdict == ContentCompatibilityMismatch.UnsupportedSource;
                    var result = Resolve(Req(), Policy(Rule.All, new Provider("P", source, _ => Answer(candidate))));
                    var noBasis = result.Probes[0].Outcome == ProbeOutcome.NoAcceptanceBasis;
                    Equal(noBasis, unsupported, $"rule and resolver agree for {source}/{basis}");
                    if (!unsupported) accepted.Add(source + "/" + basis);
                }
            }

            Equal("EditorSnapshot/OpenSceneSnapshot|ContentDirectory/VerifiedDigest|ContentOverlay/RecordedBaseMatch", string.Join("|", accepted), "the supported pairs");
            Equal(4, (int)ContentSourceKind.ContentOverlay, "ContentOverlay value");
            Equal(3, (int)ContentAcceptanceBasis.RecordedBaseMatch, "RecordedBaseMatch value");
            Equal(14, (int)ContentCompatibilityMismatch.OverlayBaseFactMissing, "OverlayBaseFactMissing value");
            Equal("osm.compat.overlayBaseContentSet", ContentCompatibilityFactKeys.OverlayBaseContentSet, "fact key");
            Equal("osm.compat.overlayBaseRevision", ContentCompatibilityFactKeys.OverlayBaseRevision, "fact key");
            Equal("osm.compat.overlayBaseTarget", ContentCompatibilityFactKeys.OverlayBaseTarget, "fact key");
            Equal("osm.compat.overlayBaseManifestSha256", ContentCompatibilityFactKeys.OverlayBaseManifestSha256, "fact key");
        }

        // O3: row 3. A missing or blank record fact is rejected and the reason names the key.
        private static void Overlay_MissingBaseFact_IsOverlayBaseFactMissing()
        {
            var rule = new ContentCompatibilityRule(Env);
            var keys = new[]
            {
                ContentCompatibilityFactKeys.OverlayBaseContentSet,
                ContentCompatibilityFactKeys.OverlayBaseRevision,
                ContentCompatibilityFactKeys.OverlayBaseTarget,
                ContentCompatibilityFactKeys.OverlayBaseManifestSha256,
            };
            foreach (var key in keys)
            {
                foreach (var blank in new string?[] { null, "", "  " })
                {
                    var facts = OverlayFacts();
                    if (blank == null) facts.Remove(key);
                    else facts[key] = blank;
                    var reason = Verdict(rule, Req(), OverlayCand("O", facts: facts), ContentCompatibilityMismatch.OverlayBaseFactMissing);
                    True(reason.Contains(key), "reason names the missing key: " + reason);
                }
            }
        }

        // O4: rows 2 and 4 to 8, each broken alone.
        private static void Overlay_EachBranchRow_RejectsWithItsValue()
        {
            var rule = new ContentCompatibilityRule(Env);
            var fixedRevision = new ContentCompatibilityRule(new ContentCompatibilityEnvelope("set", "target", Engine, 2, revision: "r1"));

            // Row 2: the rule called alone compares the expected identity (the resolver drops the pin before the rule).
            Verdict(rule, Req(expected: X), OverlayCand("O"), ContentCompatibilityMismatch.ExpectedIdentityMismatch);
            // Row 4: the recorded base content set, not the overlay's own content set.
            Verdict(rule, Req(), OverlayCand("O", facts: OverlayFacts(baseSet: "other")), ContentCompatibilityMismatch.ContentSetMismatch);
            Verdict(rule, Req(), OverlayCand("O", facts: OverlayFacts(baseSet: "SET")), ContentCompatibilityMismatch.ContentSetMismatch);
            Verdict(rule, Req(), OverlayCand("O", identity: new ContentIdentity("set", "ovrev", "target", "")), ContentCompatibilityMismatch.Compatible);
            // Row 5: only when the envelope fixes the revision.
            Verdict(fixedRevision, Req(), OverlayCand("O"), ContentCompatibilityMismatch.Compatible);
            Verdict(fixedRevision, Req(), OverlayCand("O", facts: OverlayFacts(baseRevision: "r2")), ContentCompatibilityMismatch.RevisionMismatch);
            Verdict(rule, Req(), OverlayCand("O", facts: OverlayFacts(baseRevision: "r2")), ContentCompatibilityMismatch.Compatible);
            // Row 6: the recorded base target, or the candidate's own target.
            var baseTarget = Verdict(rule, Req(), OverlayCand("O", facts: OverlayFacts(baseTarget: "other-target")), ContentCompatibilityMismatch.TargetMismatch);
            True(baseTarget.Contains("overlay base target"), "reason names the base target: " + baseTarget);
            var ownTarget = Verdict(rule, Req(), OverlayCand("O", identity: new ContentIdentity("ovset", "ovrev", "other-target", "")), ContentCompatibilityMismatch.TargetMismatch);
            True(ownTarget.Contains("overlay target"), "reason names the overlay target: " + ownTarget);
            // Row 7.
            Verdict(rule, Req(), OverlayCand("O", facts: OverlayFacts(engine: null)), ContentCompatibilityMismatch.EngineVersionMissing);
            Verdict(rule, Req(), OverlayCand("O", facts: OverlayFacts(engine: "engine-2")), ContentCompatibilityMismatch.EngineVersionMismatch);
            // Row 8.
            Verdict(rule, Req(), OverlayCand("O", facts: OverlayFacts(schema: null)), ContentCompatibilityMismatch.SchemaVersionMissing);
            Verdict(rule, Req(), OverlayCand("O", facts: OverlayFacts(schema: "02")), ContentCompatibilityMismatch.SchemaVersionMalformed);
            Verdict(rule, Req(), OverlayCand("O", facts: OverlayFacts(schema: "1")), ContentCompatibilityMismatch.SchemaVersionMismatch);
            // The empty digest of the overlay identity is normal and is not IncompleteIdentity.
            Verdict(rule, Req(), OverlayCand("O", identity: new ContentIdentity("ovset", "", "target", "")), ContentCompatibilityMismatch.Compatible);
        }

        // O5: the first row that applies wins.
        private static void Overlay_RowsAreEvaluatedInOrder()
        {
            var rule = new ContentCompatibilityRule(Env);
            var missingAndWrongSet = OverlayFacts(baseSet: "other");
            missingAndWrongSet.Remove(ContentCompatibilityFactKeys.OverlayBaseManifestSha256);
            Verdict(rule, Req(), OverlayCand("O", facts: missingAndWrongSet), ContentCompatibilityMismatch.OverlayBaseFactMissing);
            Verdict(rule, Req(), OverlayCand("O", facts: OverlayFacts(baseSet: "other", baseTarget: "other-target")), ContentCompatibilityMismatch.ContentSetMismatch);
            Verdict(rule, Req(), OverlayCand("O", facts: OverlayFacts(baseTarget: "other-target", engine: "engine-2")), ContentCompatibilityMismatch.TargetMismatch);
            Verdict(rule, Req(), OverlayCand("O", facts: OverlayFacts(engine: "engine-2", schema: "1")), ContentCompatibilityMismatch.EngineVersionMismatch);
        }

        // O6: a pinned request never adopts the overlay (its digest is empty); the resolver drops it before the rule.
        private static void Overlay_PinnedRequest_IsNotAdopted()
        {
            var rule = new ContentCompatibilityRule(Env);
            var result = Resolve(Req(expected: X), Policy(rule, OverlayProvider("O", OverlayCand("O")), Provider.NotFound("B")));
            False(result.IsResolved, "a pinned request is not adopted from the overlay");
            Outcomes(result, ProbeOutcome.PinMismatch, ProbeOutcome.NotFound);
        }

        // O7: row 9. Host predicates apply to the overlay candidate as they do to a directory candidate.
        private static void Overlay_HostPredicate_IsApplied()
        {
            var accepting = new ContentCompatibilityRule(Env, new IContentCompatibilityRule[] { HostPredicate.FactEquals(HostNameKey, "Alpha") });
            var facts = OverlayFacts();
            facts[HostNameKey] = "Alpha";
            Verdict(accepting, Req(), OverlayCand("O", facts: facts), ContentCompatibilityMismatch.Compatible);
            facts[HostNameKey] = "Beta";
            Verdict(accepting, Req(), OverlayCand("O", facts: facts), ContentCompatibilityMismatch.HostPredicateRejected);
            // A generic rejection is not loosened by an accepting predicate.
            var always = new ContentCompatibilityRule(Env, new IContentCompatibilityRule[] { HostPredicate.Accepting("always") });
            Verdict(always, Req(), OverlayCand("O", facts: OverlayFacts(baseSet: "other")), ContentCompatibilityMismatch.ContentSetMismatch);
        }

        // O8: after an identity-carrying rejection only a verified-digest copy may repair; an overlay may not.
        private static void Overlay_CannotRepairARejectedIdentity()
        {
            var rule = new ContentCompatibilityRule(Env);
            var result = Resolve(Req(), Policy(rule, Provider.Rejected("A", X), OverlayProvider("O", OverlayCand("O"))));
            False(result.IsResolved, "an overlay does not repair a rejected identity");
            Outcomes(result, ProbeOutcome.Rejected, ProbeOutcome.PinMismatch);
        }

        // ---- overlay fixtures ----

        private static Dictionary<string, string> OverlayFacts(string baseSet = "set", string baseRevision = "r1", string baseTarget = "target",
            string? engine = Engine, string? schema = "2")
        {
            var facts = CompatFacts(engine, schema);
            facts[ContentCompatibilityFactKeys.OverlayBaseContentSet] = baseSet;
            facts[ContentCompatibilityFactKeys.OverlayBaseRevision] = baseRevision;
            facts[ContentCompatibilityFactKeys.OverlayBaseTarget] = baseTarget;
            facts[ContentCompatibilityFactKeys.OverlayBaseManifestSha256] = BaseDigest;
            return facts;
        }

        private static ContentCandidate OverlayCand(string providerId, ContentIdentity? identity = null, IReadOnlyDictionary<string, string>? facts = null)
            => Cand(providerId, source: ContentSourceKind.ContentOverlay, identity: identity ?? OverlayIdentity,
                basis: ContentAcceptanceBasis.RecordedBaseMatch, facts: facts ?? OverlayFacts());

        private static Provider OverlayProvider(string id, ContentCandidate candidate)
            => new Provider(id, ContentSourceKind.ContentOverlay, _ => Answer(candidate));
    }
}
