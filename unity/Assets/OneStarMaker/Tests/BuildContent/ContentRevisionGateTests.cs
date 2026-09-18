#nullable enable

using System;
using NUnit.Framework;
using OneStarMaker.Runtime.BuildContent;

namespace OneStarMaker.Tests.BuildContent
{
    public sealed class ContentRevisionGateTests
    {
        [Test]
        public void DeleteLease_RejectsConcurrentRegistrationAndReleasesInFinallyScope()
        {
            const string path = "C:\\content-directory-gate-test";
            Assert.That(ContentRevisionGate.TryAcquireDelete("build", "StandaloneWindows64", path, out var lease, out var rejection), Is.True, rejection.ToString());
            try
            {
                var exception = Assert.Throws<ContentDirectoryException>(() => ContentRevisionGate.Reserve("build", "StandaloneWindows64", path));
                Assert.That(exception!.Code, Is.EqualTo(ContentDirectoryFailureCode.DeletionInProgress));
            }
            finally
            {
                lease!.Dispose();
            }

            using (ContentRevisionGate.Reserve("build", "StandaloneWindows64", path)) { }
        }

        [Test]
        public void ActiveRevision_BlocksDeletionByIdentityAndPhysicalPath()
        {
            var path = "C:\\content-directory-gate-" + Guid.NewGuid().ToString("N");
            using (ContentRevisionGate.Reserve("build-a", "StandaloneWindows64", path))
            {
                Assert.That(ContentRevisionGate.TryAcquireDelete("build-a", "StandaloneWindows64",
                    path, out var sameLease, out var sameReason), Is.False);
                Assert.That(sameLease, Is.Null);
                Assert.That(sameReason, Is.EqualTo(ContentDirectoryFailureCode.RevisionBusy));

                // identity を偽っても同じ物理 path の削除許可を得られない。
                Assert.That(ContentRevisionGate.TryAcquireDelete("build-b", "StandaloneWindows64",
                    path, out var otherLease, out var otherReason), Is.False);
                Assert.That(otherLease, Is.Null);
                Assert.That(otherReason, Is.EqualTo(ContentDirectoryFailureCode.PathInUse));

                Assert.That(ContentRevisionGate.TryAcquireDelete("build-a", "StandaloneWindows64",
                    path + "-moved", out var movedLease, out var movedReason), Is.False);
                Assert.That(movedLease, Is.Null);
                Assert.That(movedReason, Is.EqualTo(ContentDirectoryFailureCode.RevisionBusy));
            }
            Assert.That(ContentRevisionGate.TryAcquireDelete("build-a", "StandaloneWindows64",
                path, out var releasedLease, out _), Is.True);
            releasedLease!.Dispose();
        }

        [Test]
        public void TrailingSeparatorAndWindowsCaseAlias_StayOnOneRevisionPath()
        {
            var path = "C:\\Content-Directory-Gate-" + Guid.NewGuid().ToString("N");
            using (ContentRevisionGate.Reserve("build-a", "StandaloneWindows64", path))
            {
                Assert.That(ContentRevisionGate.TryAcquireDelete("build-b", "StandaloneWindows64",
                    path + "\\", out var aliasLease, out var aliasReason), Is.False);
                Assert.That(aliasLease, Is.Null);
                Assert.That(aliasReason, Is.EqualTo(ContentDirectoryFailureCode.PathInUse));

                Assert.That(ContentRevisionGate.TryAcquireDelete("build-b", "StandaloneWindows64",
                    path.ToLowerInvariant(), out var caseLease, out var caseReason), Is.False);
                Assert.That(caseLease, Is.Null);
                Assert.That(caseReason, Is.EqualTo(ContentDirectoryFailureCode.PathInUse));
            }
        }

        [Test]
        public void UnrelatedRevision_CanBeDeletedWhileAnotherRevisionIsRegisteredOrDeleting()
        {
            var path = "C:\\content-directory-gate-" + Guid.NewGuid().ToString("N");
            var otherPath = "C:\\content-directory-gate-" + Guid.NewGuid().ToString("N");
            using (ContentRevisionGate.Reserve("build-a", "StandaloneWindows64", path))
            {
                // 登録中でも別 identity/path の削除は許可する。削除中の path の登録は拒否する。
                Assert.That(ContentRevisionGate.TryAcquireDelete("build-b", "StandaloneWindows64", otherPath,
                    out var lease, out _), Is.True);
                try
                {
                    Assert.That(ContentRevisionGate.TryAcquireDelete("build-c", "StandaloneWindows64", otherPath,
                        out var blocked, out var reason), Is.False);
                    Assert.That(blocked, Is.Null);
                    Assert.That(reason, Is.EqualTo(ContentDirectoryFailureCode.DeletionInProgress));
                }
                finally { lease!.Dispose(); }
            }
        }
    }
}
