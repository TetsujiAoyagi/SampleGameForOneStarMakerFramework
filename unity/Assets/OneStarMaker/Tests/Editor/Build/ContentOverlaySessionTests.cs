#nullable enable

using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.BuildContent;
using UnityEngine;
using Fixture = OneStarMaker.Tests.Editor.Build.OverlayRegistrationFixture;

namespace OneStarMaker.Tests.Editor.Build
{
    /// <summary>
    /// base に上書きを 1 つ重ねた登録の口と、層 2 つの session の解除の順。base は実際に install して検証し、native は層ごとの fake
    /// （登録・解除を共通の log に書く）。照合が一致する時だけ層 2 つの session を返し、失敗は上書き → base の順に巻き戻すこと、
    /// close・自動の解除・Play 停止・同期終了が上書き → base の順に解除し、上書きの解除が失敗したら base を解除しないことを見る。
    /// </summary>
    public sealed class ContentOverlaySessionTests
    {
        private const string GuidA = "0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a";
        private const string GuidB = "0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b";
        private const string GuidC = "0c0c0c0c0c0c0c0c0c0c0c0c0c0c0c0c";

        private static readonly string[] Registered = { "base register", "overlay register" };

        private static void ArrangeDefault(Fixture fx, BuildContentOverlayBase? record = null, string? declaration = null)
            => fx.Arrange(
                new[] { Fixture.ObjectEntry("BaseOnly", "Full", GuidA), Fixture.ObjectEntry("Edited", "Full", GuidB) },
                new[] { Fixture.ObjectEntry("Edited", "Full", GuidC) },
                record, declaration);

        // ─── AC1: 登録の口と照合 ───

        [Test]
        public Task MatchingRecord_RegistersBaseThenOverlay_AndReturnsTwoLayers()
            => Fixture.WithAsync(async fx =>
        {
            ArrangeDefault(fx);
            var session = fx.Register();
            Assert.That(fx.Log, Is.EqualTo(Registered), "native の登録は base → 上書きの順に 1 回ずつ");
            Assert.That(session.OverlayLayer, Is.Not.Null);
            Assert.That(session.BuildIdentity, Is.EqualTo(Fixture.Revision), "session の identity は base");
            Assert.That(session.VerifiedRevision!.ManifestSha256, Is.EqualTo(fx.Digest));
            Assert.That(fx.ReservationHeld(), Is.True);
            await session.CloseAsync();
            Assert.That(fx.ReservationHeld(), Is.False);
        });

        [Test]
        public Task MatchingRecord_WithBaseDeclaration_Registers()
            => Fixture.WithAsync(async fx =>
        {
            ArrangeDefault(fx, declaration: "purpose_a");
            var session = fx.Register();
            Assert.That(session.Declaration!.Name, Is.EqualTo("purpose_a"), "宣言は base の root の物");
            await session.CloseAsync();
        });

        [TestCase("contentSet")]
        [TestCase("revision")]
        [TestCase("target")]
        [TestCase("manifestSha256")]
        [TestCase("declarationName-overlay-only")]
        [TestCase("declarationName-base-only")]
        [TestCase("declarationName-different")]
        public Task MismatchedRecord_IsOverlayBaseMismatch_AndRollsBackOverlayThenBase(string field)
            => Fixture.WithAsync(async fx =>
        {
            string? baseDeclaration = null;
            BuildContentOverlayBase record;
            switch (field)
            {
                case "contentSet": record = fx.Record(contentSet: "other"); break;
                case "revision": record = fx.Record(revision: "other"); break;
                case "target": record = fx.Record(target: "Android-Player"); break;
                case "manifestSha256": record = fx.Record(digest: new string('f', 64)); break;
                case "declarationName-overlay-only": record = fx.Record(declarationName: "purpose_a"); break;
                case "declarationName-base-only": record = fx.Record(); baseDeclaration = "purpose_a"; break;
                default: record = fx.Record(declarationName: "purpose_b"); baseDeclaration = "purpose_a"; break;
            }
            fx.Arrange(new[] { Fixture.ObjectEntry("BaseOnly", "Full", GuidA) }, new[] { Fixture.ObjectEntry("Edited", "Full", GuidC) },
                record, null);
            if (baseDeclaration != null)
                fx.BaseNative.Root = fx.Root(Fixture.Revision, new[] { Fixture.ObjectEntry("BaseOnly", "Full", GuidA) }, declarationName: baseDeclaration);

            var failure = Assert.Throws<ContentDirectoryException>(() => fx.Register());
            Assert.That(failure!.Code, Is.EqualTo(ContentDirectoryFailureCode.OverlayBaseMismatch));
            Assert.That(failure.Message, Does.Contain(field.Split('-')[0]), "理由の文に違う欄の名前");
            Assert.That(fx.Log, Is.EqualTo(new[] { "base register", "overlay register", "overlay unregister", "base unregister" }));
            Assert.That(fx.ReservationHeld(), Is.False, "予約 0 件");
            Assert.That(ContentDirectoryLayers.HasPendingRollback, Is.False);
            await Task.CompletedTask;
        });

        [Test]
        public Task RootWithoutRecord_IsOverlayBaseMissing()
            => Fixture.WithAsync(async fx =>
        {
            ArrangeDefault(fx);
            fx.OverlayNative.Root = fx.Root(Fixture.OverlayIdentity, new[] { Fixture.ObjectEntry("Edited", "Full", GuidC) });
            var failure = Assert.Throws<ContentDirectoryException>(() => fx.Register());
            Assert.That(failure!.Code, Is.EqualTo(ContentDirectoryFailureCode.OverlayBaseMissing));
            Assert.That(fx.Log, Is.EqualTo(new[] { "base register", "overlay register", "overlay unregister", "base unregister" }));
            Assert.That(fx.ReservationHeld(), Is.False);
            await Task.CompletedTask;
        });

        [Test]
        public Task OverlayNativeFailures_UseTheExistingValues_AndRollBack()
            => Fixture.WithAsync(async fx =>
        {
            // 無効な handle: 上書きは登録されていないので解除しない。base は解除する。
            ArrangeDefault(fx);
            fx.OverlayNative.InvalidHandle = true;
            Assert.That(Assert.Throws<ContentDirectoryException>(() => fx.Register())!.Code, Is.EqualTo(ContentDirectoryFailureCode.RegistrationFailed));
            Assert.That(fx.Log, Is.EqualTo(new[] { "base register", "overlay register", "base unregister" }));
            Assert.That(fx.ReservationHeld(), Is.False);

            // root が 1 つでない。
            fx.Log.Clear();
            fx.OverlayNative.InvalidHandle = false;
            fx.OverlayNative.RootCount = 0;
            Assert.That(Assert.Throws<ContentDirectoryException>(() => fx.Register())!.Code, Is.EqualTo(ContentDirectoryFailureCode.InvalidRoot));
            Assert.That(fx.Log, Is.EqualTo(new[] { "base register", "overlay register", "overlay unregister", "base unregister" }));

            // 索引の作成の失敗（root の identity が渡した上書きの identity と違う）。
            fx.Log.Clear();
            fx.OverlayNative.RootCount = 1;
            fx.OverlayNative.Root = fx.Root("other-identity", new[] { Fixture.ObjectEntry("Edited", "Full", GuidC) }, fx.Record());
            Assert.That(Assert.Throws<ContentDirectoryException>(() => fx.Register())!.Code, Is.EqualTo(ContentDirectoryFailureCode.IdentityMismatch));
            Assert.That(fx.Log, Is.EqualTo(new[] { "base register", "overlay register", "overlay unregister", "base unregister" }));
            Assert.That(fx.ReservationHeld(), Is.False);
            await Task.CompletedTask;
        });

        [Test]
        public Task SamePathOrIdentityAsBase_IsRejectedBeforeReservation()
            => Fixture.WithAsync(async fx =>
        {
            ArrangeDefault(fx);
            var samePath = Assert.Throws<ContentDirectoryException>(() =>
                ContentDirectorySession.RegisterVerifiedWithOverlay(fx.Verified, fx.BaseNative, fx.Verified.ContentPath, Fixture.OverlayIdentity, fx.OverlayNative));
            Assert.That(samePath!.Code, Is.EqualTo(ContentDirectoryFailureCode.InvalidConfiguration));
            var sameIdentity = Assert.Throws<ContentDirectoryException>(() =>
                ContentDirectorySession.RegisterVerifiedWithOverlay(fx.Verified, fx.BaseNative, fx.OverlayPath, Fixture.Revision, fx.OverlayNative));
            Assert.That(sameIdentity!.Code, Is.EqualTo(ContentDirectoryFailureCode.InvalidConfiguration));
            Assert.That(fx.Log, Is.Empty, "native の登録を始めない");
            Assert.That(fx.ReservationHeld(), Is.False);
            await Task.CompletedTask;
        });

        [Test]
        public Task RollbackOverlayUnregisterFailure_KeepsBaseAndReservation_UntilTheNextRegistrationRetries()
            => Fixture.WithAsync(async fx =>
        {
            ArrangeDefault(fx, fx.Record(revision: "other"));
            fx.OverlayNative.FailUnregisterCount = 1;
            var failure = Assert.Throws<ContentDirectoryException>(() => fx.Register());
            Assert.That(failure!.Code, Is.EqualTo(ContentDirectoryFailureCode.RegistrationFailed), "巻き戻しの失敗");
            Assert.That(fx.Log, Is.EqualTo(new[] { "base register", "overlay register", "overlay unregister-failed" }), "base を解除しない");
            Assert.That(fx.ReservationHeld(), Is.True, "予約を残す");
            Assert.That(ContentDirectoryLayers.HasPendingRollback, Is.True);

            // 次の登録の前の再試行が、上書き → base の順に解除してから登録を始める。
            fx.Log.Clear();
            ArrangeDefault(fx);
            var session = fx.Register();
            Assert.That(fx.Log, Is.EqualTo(new[] { "overlay unregister", "base unregister", "base register", "overlay register" }));
            Assert.That(ContentDirectoryLayers.HasPendingRollback, Is.False);
            await session.CloseAsync();
        });

        [Test]
        public Task RollbackBaseUnregisterFailure_RetriesOnlyTheBase()
            => Fixture.WithAsync(async fx =>
        {
            ArrangeDefault(fx, fx.Record(revision: "other"));
            fx.BaseNative.FailUnregisterCount = 1;
            Assert.That(Assert.Throws<ContentDirectoryException>(() => fx.Register())!.Code, Is.EqualTo(ContentDirectoryFailureCode.RegistrationFailed));
            Assert.That(fx.Log, Is.EqualTo(new[] { "base register", "overlay register", "overlay unregister", "base unregister-failed" }));
            Assert.That(fx.ReservationHeld(), Is.True);

            fx.Log.Clear();
            ArrangeDefault(fx);
            var session = fx.Register();
            Assert.That(fx.Log, Is.EqualTo(new[] { "base unregister", "base register", "overlay register" }), "再試行は base だけを解除する");
            await session.CloseAsync();
        });

        [Test]
        public Task PendingRetryFailure_RejectsTheNextRegistrationBeforeNativeRegistration()
            => Fixture.WithAsync(async fx =>
        {
            ArrangeDefault(fx, fx.Record(revision: "other"));
            fx.OverlayNative.FailUnregisterCount = 2;
            Assert.Throws<ContentDirectoryException>(() => fx.Register());
            fx.Log.Clear();
            ArrangeDefault(fx);
            var failure = Assert.Throws<ContentDirectoryException>(() => fx.Register());
            Assert.That(failure!.Code, Is.EqualTo(ContentDirectoryFailureCode.RegistrationFailed));
            Assert.That(fx.Log, Is.EqualTo(new[] { "overlay unregister-failed" }), "native の登録を始めない");
            Assert.That(ContentDirectoryLayers.HasPendingRollback, Is.True);
            await Task.CompletedTask;
        });

        [Test]
        public Task RecordBearingRoot_RegisteredAlone_IsInvalidRoot()
            => Fixture.WithAsync(async fx =>
        {
            ArrangeDefault(fx);
            // 上書きの root を base（検証済みの登録）として渡す。
            var asBase = new Fixture.LayerNative("as-base", fx.Log) { Root = fx.Root(Fixture.Revision, new[] { Fixture.ObjectEntry("Edited", "Full", GuidC) }, fx.Record()) };
            Assert.That(Assert.Throws<ContentDirectoryException>(() => ContentDirectorySession.RegisterVerified(fx.Verified, asBase))!.Code,
                Is.EqualTo(ContentDirectoryFailureCode.InvalidRoot));
            // 上書きの directory を今の path 指定の登録で単独に登録する。
            var alone = new Fixture.LayerNative("alone", fx.Log) { Root = fx.Root(Fixture.OverlayIdentity, new[] { Fixture.ObjectEntry("Edited", "Full", GuidC) }, fx.Record()) };
            Assert.That(Assert.Throws<ContentDirectoryException>(() => ContentDirectorySession.Register(fx.OverlayPath, Fixture.OverlayIdentity, Fixture.Target, alone))!.Code,
                Is.EqualTo(ContentDirectoryFailureCode.InvalidRoot));
            // 上書きを重ねた登録の base に記録つきの root を渡す。
            fx.BaseNative.Root = fx.Root(Fixture.Revision, new[] { Fixture.ObjectEntry("BaseOnly", "Full", GuidA) }, fx.Record());
            Assert.That(Assert.Throws<ContentDirectoryException>(() => fx.Register())!.Code, Is.EqualTo(ContentDirectoryFailureCode.InvalidRoot));
            Assert.That(fx.OverlayNative.LoadCount, Is.Zero);
            Assert.That(fx.Log.Count(x => x.EndsWith(" register")), Is.EqualTo(fx.Log.Count(x => x.EndsWith(" unregister"))), "登録した層は全て解除した");
            Assert.That(fx.ReservationHeld(), Is.False);
            await Task.CompletedTask;
        });

        [Test]
        public void SessionAndLayers_HaveNoMemberToAddOrRemoveALayer()
        {
            var flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var type in new[] { typeof(ContentDirectorySession), typeof(ContentDirectoryLayers) })
            {
                var names = type.GetMethods(flags).Select(x => x.Name).ToArray();
                foreach (var verb in new[] { "Add", "Remove", "Attach", "Detach", "Push", "Pop", "Insert", "Append" })
                    Assert.That(names.Where(x => x.StartsWith(verb, StringComparison.Ordinal) || x.Contains(verb + "Layer") || x.Contains(verb + "Overlay")),
                        Is.Empty, type.Name + " has a member that changes the layers: " + verb);
                // session の層の列は constructor でだけ決まる（instance の field は readonly）。
                // static の field は見ない: ContentDirectoryLayers._pendingRollback は、巻き戻しに失敗した層の列を
                // process に 1 つだけ保留して次の登録の前に再試行するための書き換える field で（I-2）、
                // どの session の層の列にも層を足さないため。
                var instanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                foreach (var field in type.GetFields(instanceFields).Where(x => x.FieldType == typeof(ContentDirectoryLayers) || x.FieldType == typeof(ContentDirectoryLayers.Layer[])))
                    Assert.That(field.IsInitOnly, Is.True, type.Name + "." + field.Name);
            }
        }

        // ─── AC3: 解除の順 ───

        [Test]
        public Task Close_UnregistersOverlayThenBase_AndReturnsTheReservationLast()
            => Fixture.WithAsync(async fx =>
        {
            ArrangeDefault(fx);
            var session = fx.Register();
            bool? heldDuringBaseUnregister = null;
            fx.BaseNative.OnUnregister = () => heldDuringBaseUnregister = fx.ReservationHeld();
            await session.CloseAsync();
            Assert.That(fx.Log, Is.EqualTo(new[] { "base register", "overlay register", "overlay unregister", "base unregister" }));
            Assert.That(heldDuringBaseUnregister, Is.True, "base の解除までは予約を持つ");
            Assert.That(fx.ReservationHeld(), Is.False);
            Assert.That(session.IsClosed, Is.True);
        });

        [TestCase(true)]
        [TestCase(false)]
        public Task AutoRelease_WaitsForTokensOfBothLayers(bool overlayFirst)
            => Fixture.WithAsync(async fx =>
        {
            ArrangeDefault(fx);
            fx.BaseNative.TextureFactory = fx.NewTexture;
            fx.OverlayNative.TextureFactory = fx.NewTexture;
            var session = fx.Register();
            var fromOverlay = await session.LoadAssetAsync<Texture2D>("Edited", "Full", CancellationToken.None);
            var fromBase = await session.LoadAssetAsync<Texture2D>("BaseOnly", "Full", CancellationToken.None);
            Assert.That(fx.Log.Skip(2), Is.EqualTo(new[] { "overlay load Edited/Full", "base load BaseOnly/Full" }), "選ばれた層の native で読む");

            await session.StopAndDrainAsync();
            session.Release(overlayFirst ? fromOverlay : fromBase);
            Assert.That(fx.Log.Any(x => x.Contains("unregister")), Is.False, "片方の層の token が残る間はどちらも解除しない");
            Assert.That(fx.ReservationHeld(), Is.True);

            session.Release(overlayFirst ? fromBase : fromOverlay);
            Assert.That(fx.Log.Where(x => x.Contains("unregister")), Is.EqualTo(new[] { "overlay unregister", "base unregister" }));
            Assert.That(fx.Log.Count(x => x == "overlay release"), Is.EqualTo(1), "token の解放は載せた層の native へ返す");
            Assert.That(fx.Log.Count(x => x == "base release"), Is.EqualTo(1));
            Assert.That(session.IsClosed, Is.True);
            Assert.That(fx.ReservationHeld(), Is.False);
        });

        [Test]
        public Task CloseOverlayUnregisterFailure_KeepsBase_AndRetryUnregistersOverlayThenBase()
            => Fixture.WithAsync(async fx =>
        {
            ArrangeDefault(fx);
            var session = fx.Register();
            fx.OverlayNative.FailUnregisterCount = 1;
            var failure = Assert.ThrowsAsync<ContentDirectoryException>(async () => await session.CloseAsync());
            Assert.That(failure!.Code, Is.EqualTo(ContentDirectoryFailureCode.OperationFailed));
            Assert.That(fx.Log.Skip(2), Is.EqualTo(new[] { "overlay unregister-failed" }), "base を解除しない");
            Assert.That(fx.ReservationHeld(), Is.True);
            Assert.That(session.IsClosed, Is.False);

            await session.CloseAsync();
            Assert.That(fx.Log.Skip(2), Is.EqualTo(new[] { "overlay unregister-failed", "overlay unregister", "base unregister" }));
            Assert.That(fx.ReservationHeld(), Is.False);
            Assert.That(session.IsClosed, Is.True);
        });

        [Test]
        public Task CloseBaseUnregisterFailure_RetryUnregistersOnlyTheBase()
            => Fixture.WithAsync(async fx =>
        {
            ArrangeDefault(fx);
            var session = fx.Register();
            fx.BaseNative.FailUnregisterCount = 1;
            Assert.ThrowsAsync<ContentDirectoryException>(async () => await session.CloseAsync());
            Assert.That(fx.Log.Skip(2), Is.EqualTo(new[] { "overlay unregister", "base unregister-failed" }));
            Assert.That(fx.ReservationHeld(), Is.True);
            Assert.That(session.IsClosed, Is.False);

            await session.CloseAsync();
            Assert.That(fx.Log.Skip(2), Is.EqualTo(new[] { "overlay unregister", "base unregister-failed", "base unregister" }));
            Assert.That(session.IsClosed, Is.True);
            Assert.That(fx.ReservationHeld(), Is.False);
        });

        [Test]
        public Task PlayStop_UnregistersOverlayThenBase_WithLiveTokens()
            => Fixture.WithAsync(async fx =>
        {
            ArrangeDefault(fx);
            fx.OverlayNative.TextureFactory = fx.NewTexture;
            var session = fx.Register();
            await session.LoadAssetAsync<Texture2D>("Edited", "Full", CancellationToken.None);
            session.CompletePlayStop();
            Assert.That(fx.Log.Where(x => x.Contains("unregister")), Is.EqualTo(new[] { "overlay unregister", "base unregister" }), "token を待たない");
            Assert.That(session.IsClosed, Is.True);
            Assert.That(fx.ReservationHeld(), Is.False);
        });

        [Test]
        public Task PlayStopOverlayFailure_KeepsBase_AndTheNextRegistrationRetries_AndAResumedCloseUnregistersEachLayerOnce()
            => Fixture.WithAsync(async fx =>
        {
            ArrangeDefault(fx);
            var session = fx.Register();
            fx.OverlayNative.FailUnregisterCount = 1;
            session.CompletePlayStop();
            Assert.That(fx.Log.Skip(2), Is.EqualTo(new[] { "overlay unregister-failed" }), "上書きの解除が失敗したら base を解除しない");
            Assert.That(fx.ReservationHeld(), Is.True);
            Assert.That(session.IsClosed, Is.False);
            Assert.That(ContentDirectoryLayers.HasPendingRollback, Is.True);

            // 止まっていた明示 close が再開する。解除済みでない層だけを順に解除し、各層の成功した解除は 1 回。
            await session.CloseAsync();
            Assert.That(fx.Log.Skip(2), Is.EqualTo(new[] { "overlay unregister-failed", "overlay unregister", "base unregister" }));
            Assert.That(session.IsClosed, Is.True);
            Assert.That(fx.ReservationHeld(), Is.False);

            // 保留中の再試行は、済んだ層の列に対しては何も解除せずに外れる。
            fx.Log.Clear();
            ArrangeDefault(fx);
            var next = fx.Register();
            Assert.That(fx.Log, Is.EqualTo(Registered), "再試行は同じ層を 2 回解除しない");
            Assert.That(ContentDirectoryLayers.HasPendingRollback, Is.False);
            await next.CloseAsync();
        });

        [Test]
        public Task PlayStopOverlayFailure_NextRegistrationRetriesOverlayThenBase()
            => Fixture.WithAsync(async fx =>
        {
            ArrangeDefault(fx);
            var session = fx.Register();
            fx.OverlayNative.FailUnregisterCount = 1;
            session.CompletePlayStop();

            fx.Log.Clear();
            ArrangeDefault(fx);
            var next = fx.Register();
            Assert.That(fx.Log, Is.EqualTo(new[] { "overlay unregister", "base unregister", "base register", "overlay register" }));
            Assert.That(session.IsClosed, Is.True, "再試行が済んだ層の列の session は closed");
            Assert.That(ContentDirectoryLayers.HasPendingRollback, Is.False);
            await next.CloseAsync();
        });

        [Test]
        public Task SynchronousShutdown_UnregistersOverlayThenBase()
            => Fixture.WithAsync(async fx =>
        {
            ArrangeDefault(fx);
            var session = fx.Register();
            session.BeginSynchronousShutdown();
            Assert.That(fx.Log.Skip(2), Is.EqualTo(new[] { "overlay unregister", "base unregister" }));
            Assert.That(session.IsClosed, Is.True);
            Assert.That(fx.ReservationHeld(), Is.False);
            await Task.CompletedTask;
        });
    }
}
