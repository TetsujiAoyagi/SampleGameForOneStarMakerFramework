#nullable enable

using System.Linq;
using System.Text;
using NUnit.Framework;
using OneStarMaker.Runtime.BuildContent.Distribution;

namespace OneStarMaker.Tests.BuildContent.Distribution
{
    /// <summary>
    /// 順のある候補からの選択。入力は transport.json の bytes と期待 digest だけで、content の file は存在しない。
    /// 目的 A（Entry・Shared・tag 無しの SharedPrefab）と B（Next・Shared の Full と Whitebox・WhiteboxOnly）。
    /// </summary>
    public sealed class ContentPurposeSelectionTests
    {
        private static ContentPurposeCandidate A => PurposeManifests.Candidate(PurposeManifests.Bytes(PurposeManifests.A()));
        private static ContentPurposeCandidate B => PurposeManifests.Candidate(PurposeManifests.Bytes(PurposeManifests.B()));

        [Test]
        public void Select_UsesTheCallersOrder()
        {
            var next = ContentPurposeIndex.Select(PurposeManifests.Scene("Next"), new[] { A, B });
            AssertSelected(next, 1, ContentPurposeVerdict.DoesNotContain, ContentPurposeVerdict.Contains);
            var entry = ContentPurposeIndex.Select(PurposeManifests.Scene("Entry"), new[] { A, B });
            AssertSelected(entry, 0, ContentPurposeVerdict.Contains, ContentPurposeVerdict.NotEvaluated);
            var sharedAB = ContentPurposeIndex.Select(PurposeManifests.Scene("Shared"), new[] { A, B });
            AssertSelected(sharedAB, 0, ContentPurposeVerdict.Contains, ContentPurposeVerdict.NotEvaluated);
            var sharedBA = ContentPurposeIndex.Select(PurposeManifests.Scene("Shared"), new[] { B, A });
            AssertSelected(sharedBA, 0, ContentPurposeVerdict.Contains, ContentPurposeVerdict.NotEvaluated);
        }

        [Test]
        public void Select_RepresentationRules()
        {
            var unspecified = ContentPurposeIndex.Select(PurposeManifests.Scene("WhiteboxOnly"), new[] { A, B });
            AssertNotFound(unspecified, ContentPurposeVerdict.DoesNotContain, ContentPurposeVerdict.DoesNotContain);
            var whitebox = ContentPurposeIndex.Select(PurposeManifests.Scene("WhiteboxOnly", "Whitebox"), new[] { A, B });
            AssertSelected(whitebox, 1, ContentPurposeVerdict.DoesNotContain, ContentPurposeVerdict.Contains);
            // A には Shared の Whitebox が無いが、Full へ fallback して含む。
            var sharedWhitebox = ContentPurposeIndex.Select(PurposeManifests.Scene("Shared", "Whitebox"), new[] { A, B });
            AssertSelected(sharedWhitebox, 0, ContentPurposeVerdict.Contains, ContentPurposeVerdict.NotEvaluated);
        }

        [Test]
        public void Select_Missing_IsNotFoundWithBothReasons()
        {
            var missing = ContentPurposeIndex.Select(PurposeManifests.Scene("Missing"), new[] { A, B });
            AssertNotFound(missing, ContentPurposeVerdict.DoesNotContain, ContentPurposeVerdict.DoesNotContain);
        }

        [Test]
        public void Select_SkipsUnsupportedAndUnindexedCandidates()
        {
            var unsupported = PurposeManifests.Candidate(PurposeManifests.WithRawIndex("{\"version\":2,\"entries\":[]}"));
            var skipUnsupported = ContentPurposeIndex.Select(PurposeManifests.Scene("Shared"), new[] { unsupported, B });
            AssertSelected(skipUnsupported, 1, ContentPurposeVerdict.UnsupportedIndex, ContentPurposeVerdict.Contains);

            var legacy = PurposeManifests.Candidate(PurposeManifests.WithoutIndex());
            var skipLegacy = ContentPurposeIndex.Select(PurposeManifests.Scene("Shared"), new[] { legacy, B });
            AssertSelected(skipLegacy, 1, ContentPurposeVerdict.NoIndex, ContentPurposeVerdict.Contains);

            var versionZero = PurposeManifests.Candidate(PurposeManifests.WithRawIndex("{\"version\":0,\"entries\":[]}"));
            var onlyUnindexed = ContentPurposeIndex.Select(PurposeManifests.Scene("Shared"), new[] { versionZero, legacy });
            AssertNotFound(onlyUnindexed, ContentPurposeVerdict.NoIndex, ContentPurposeVerdict.NoIndex);
        }

        [Test]
        public void Select_StopsAtAValidationFailureEvenIfALaterCandidateContains()
        {
            var digestMismatch = new ContentPurposeCandidate(PurposeManifests.Bytes(PurposeManifests.A()), new string('0', 64));
            var broken = PurposeManifests.Candidate(PurposeManifests.WithRawIndex("{\"version\":\"1\",\"entries\":[]}"));
            var tooLarge = PurposeManifests.Candidate(Encoding.UTF8.GetBytes(
                PurposeManifests.Json(PurposeManifests.A()).PadRight(4 * 1024 * 1024 + 1)));
            foreach (var (candidate, failure) in new[]
                     {
                         (digestMismatch, ContentPurposeIndexFailure.DigestMismatch),
                         (broken, ContentPurposeIndexFailure.InvalidIndex),
                         (tooLarge, ContentPurposeIndexFailure.TooLarge),
                     })
            {
                var selection = ContentPurposeIndex.Select(PurposeManifests.Scene("Next"), new[] { A, candidate, B });
                Assert.That(selection.IsSuccess, Is.False);
                Assert.That(selection.Status, Is.EqualTo(ContentPurposeSelectionStatus.ValidationFailed));
                Assert.That(selection.SelectedIndex, Is.EqualTo(-1));
                Assert.That(selection.Candidates.Select(x => x.Verdict), Is.EqualTo(new[]
                    { ContentPurposeVerdict.DoesNotContain, ContentPurposeVerdict.ValidationFailed, ContentPurposeVerdict.NotEvaluated }));
                Assert.That(selection.Candidates[1].Failure, Is.EqualTo(failure));
            }
        }

        [Test]
        public void Select_StopsAtTheFirstContainingOrFailingCandidate()
        {
            var badB = new ContentPurposeCandidate(PurposeManifests.Bytes(PurposeManifests.B()), new string('0', 64));
            var containsFirst = ContentPurposeIndex.Select(PurposeManifests.Scene("Shared"), new[] { A, badB });
            AssertSelected(containsFirst, 0, ContentPurposeVerdict.Contains, ContentPurposeVerdict.NotEvaluated);

            var badA = new ContentPurposeCandidate(PurposeManifests.Bytes(PurposeManifests.A()), new string('0', 64));
            var failsFirst = ContentPurposeIndex.Select(PurposeManifests.Scene("Shared"), new[] { badA, B });
            Assert.That(failsFirst.Status, Is.EqualTo(ContentPurposeSelectionStatus.ValidationFailed));
            Assert.That(failsFirst.Candidates.Select(x => x.Verdict),
                Is.EqualTo(new[] { ContentPurposeVerdict.ValidationFailed, ContentPurposeVerdict.NotEvaluated }));

            var entry = ContentPurposeIndex.Select(PurposeManifests.Scene("Entry"), new[] { A, B });
            AssertSelected(entry, 0, ContentPurposeVerdict.Contains, ContentPurposeVerdict.NotEvaluated);
        }

        [Test]
        public void Candidate_CopiesTheBytes()
        {
            var bytes = PurposeManifests.Bytes(PurposeManifests.B());
            var candidate = PurposeManifests.Candidate(bytes);
            bytes[0] = (byte)' ';
            var selection = ContentPurposeIndex.Select(PurposeManifests.Scene("Next"), new[] { candidate });
            AssertSelected(selection, 0, ContentPurposeVerdict.Contains);
        }

        private static void AssertSelected(ContentPurposeSelection selection, int index, params ContentPurposeVerdict[] verdicts)
        {
            Assert.That(selection.Status, Is.EqualTo(ContentPurposeSelectionStatus.Selected));
            Assert.That(selection.IsSuccess, Is.True);
            Assert.That(selection.SelectedIndex, Is.EqualTo(index));
            Assert.That(selection.Candidates.Select(x => x.Verdict), Is.EqualTo(verdicts));
        }

        private static void AssertNotFound(ContentPurposeSelection selection, params ContentPurposeVerdict[] verdicts)
        {
            Assert.That(selection.Status, Is.EqualTo(ContentPurposeSelectionStatus.NotFound));
            Assert.That(selection.IsSuccess, Is.False);
            Assert.That(selection.SelectedIndex, Is.EqualTo(-1));
            Assert.That(selection.Candidates.Select(x => x.Verdict), Is.EqualTo(verdicts));
        }
    }
}
