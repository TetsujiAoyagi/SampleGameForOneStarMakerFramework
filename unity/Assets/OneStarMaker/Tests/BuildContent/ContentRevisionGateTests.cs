#nullable enable

using System;
using System.Collections.Generic;
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

        // ─── 層 2 つ（base と上書き）を 1 件の登録として予約する ───

        private const string Target = "StandaloneWindows64";

        private static IReadOnlyList<ContentRevisionLayer> Layers(string basePath, string overlayPath,
            string baseIdentity = "base", string overlayIdentity = "overlay")
            => new[] { new ContentRevisionLayer(baseIdentity, Target, basePath), new ContentRevisionLayer(overlayIdentity, Target, overlayPath) };

        private static string NewPath() => "C:\\content-directory-gate-" + Guid.NewGuid().ToString("N");

        [Test]
        public void TwoLayers_AreOneRegistration_AndBlockDeletionOfEitherLayer()
        {
            var basePath = NewPath();
            var overlayPath = NewPath();
            using (ContentRevisionGate.Reserve(Layers(basePath, overlayPath)))
            {
                foreach (var (identity, path) in new[] { ("base", basePath), ("overlay", overlayPath) })
                {
                    Assert.That(ContentRevisionGate.TryAcquireDelete(identity, Target, path, out var lease, out var reason), Is.False, identity);
                    Assert.That(lease, Is.Null);
                    Assert.That(reason, Is.EqualTo(ContentDirectoryFailureCode.RevisionBusy), "同じ revision と path は RevisionBusy: " + identity);

                    // 別 revision で同じ path を削る抜け道は PathInUse。
                    Assert.That(ContentRevisionGate.TryAcquireDelete("other", Target, path, out lease, out reason), Is.False, identity);
                    Assert.That(reason, Is.EqualTo(ContentDirectoryFailureCode.PathInUse), "どれかの層と path が同じで revision が違う: " + identity);

                    // 同じ revision を別の path で削る道は RevisionBusy。
                    Assert.That(ContentRevisionGate.TryAcquireDelete(identity, Target, path + "-moved", out lease, out reason), Is.False, identity);
                    Assert.That(reason, Is.EqualTo(ContentDirectoryFailureCode.RevisionBusy), identity);
                }

                // 層を持つ登録の後の別の登録は今までどおり拒む（1 層でも 2 層でも）。
                var single = Assert.Throws<ContentDirectoryException>(() => ContentRevisionGate.Reserve("third", Target, NewPath()));
                Assert.That(single!.Code, Is.EqualTo(ContentDirectoryFailureCode.RevisionBusy));
                var layered = Assert.Throws<ContentDirectoryException>(() => ContentRevisionGate.Reserve(Layers(NewPath(), NewPath(), "third", "fourth")));
                Assert.That(layered!.Code, Is.EqualTo(ContentDirectoryFailureCode.RevisionBusy));
                var samePath = Assert.Throws<ContentDirectoryException>(() => ContentRevisionGate.Reserve(Layers(NewPath(), overlayPath, "third", "fourth")));
                Assert.That(samePath!.Code, Is.EqualTo(ContentDirectoryFailureCode.PathInUse), "どれかの層と path が同じで revision が違う登録は PathInUse");
            }

            // 予約を返した後は両方とも削除を許可する。
            foreach (var (identity, path) in new[] { ("base", basePath), ("overlay", overlayPath) })
            {
                Assert.That(ContentRevisionGate.TryAcquireDelete(identity, Target, path, out var released, out var reason), Is.True, reason.ToString());
                released!.Dispose();
            }
        }

        [Test]
        public void TwoLayers_SharingAPathOrARevision_AreRejectedBeforeReservation()
        {
            var basePath = NewPath();
            var samePath = Assert.Throws<ContentDirectoryException>(() => ContentRevisionGate.Reserve(Layers(basePath, basePath)));
            Assert.That(samePath!.Code, Is.EqualTo(ContentDirectoryFailureCode.InvalidConfiguration));
            var caseAlias = Assert.Throws<ContentDirectoryException>(() => ContentRevisionGate.Reserve(Layers(basePath, basePath.ToLowerInvariant() + "\\")));
            Assert.That(caseAlias!.Code, Is.EqualTo(ContentDirectoryFailureCode.InvalidConfiguration), "正規化した path が同じ");
            var sameIdentity = Assert.Throws<ContentDirectoryException>(() => ContentRevisionGate.Reserve(Layers(basePath, NewPath(), "same", "same")));
            Assert.That(sameIdentity!.Code, Is.EqualTo(ContentDirectoryFailureCode.InvalidConfiguration));

            // 予約は残っていない。
            using (ContentRevisionGate.Reserve("base", Target, basePath)) { }
        }

        [Test]
        public void TwoLayers_SecondOsLeaseFailure_ReturnsTheFirstAndLeavesNoReservation()
        {
            var basePath = NewPath();
            var overlayPath = NewPath();
            // 別 process の排他削除に当たる状態を、上書きの層の OS の lease を排他で持つことで作る（process 内の台帳には載らない）。
            Assert.That(RevisionProcessLease.TryAcquireDelete("overlay", Target, overlayPath, out var foreign), Is.True);
            try
            {
                var failure = Assert.Throws<ContentDirectoryException>(() => ContentRevisionGate.Reserve(Layers(basePath, overlayPath)));
                Assert.That(failure!.Code, Is.EqualTo(ContentDirectoryFailureCode.DeletionInProgress));
            }
            finally { foreign!.Dispose(); }

            // 1 つ目の層の OS の lease も返っている（排他の削除の許可が取れる）。予約も 0 件（次の登録が通る）。
            Assert.That(ContentRevisionGate.TryAcquireDelete("base", Target, basePath, out var deletion, out var reason), Is.True, reason.ToString());
            deletion!.Dispose();
            using (ContentRevisionGate.Reserve(Layers(basePath, overlayPath))) { }
        }
    }
}
