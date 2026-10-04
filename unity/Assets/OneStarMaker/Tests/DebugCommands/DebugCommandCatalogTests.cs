#nullable enable

using System;
using System.Threading;
using NUnit.Framework;
using OneStarMaker.Foundation.DebugSocket;
using OneStarMaker.Runtime.DebugCommands;
using OneStarMaker.Runtime.DebugSocketServices;

namespace OneStarMaker.Tests.DebugCommands
{
    /// <summary>
    /// 目録の実行と、DebugStudio 封筒への転写を検証する。受信スレッドや実ソケットはここでは開かない。
    /// </summary>
    [TestFixture]
    public sealed class DebugCommandCatalogTests
    {
        [Test]
        public void Catalog_ExecutesTheRegisteredName_AndRejectsUnknownNames()
        {
            var catalog = new DebugCommandCatalog();
            catalog.Register("sample.echo", payload => DebugCommandResult.Ok("echo", payload));

            Assert.That(catalog.TryExecute("sample.echo", "{\"n\":1}", out var result), Is.True);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Is.EqualTo("echo"));
            Assert.That(result.PayloadJson, Is.EqualTo("{\"n\":1}"));
            Assert.That(catalog.TryExecute("sample.other", "{}", out var missing), Is.False);
            Assert.That(missing.Success, Is.False);
            Assert.That(missing.Message, Is.EqualTo("Debug command is not registered."));
        }

        [Test]
        public void Catalog_RegistrationAndResolutionKeepTheirDistinctFailures()
        {
            var catalog = new DebugCommandCatalog();
            var calls = 0;
            DebugCommandHandler fail = payload => { calls++; return DebugCommandResult.Fail(payload); };
            Assert.Throws<ArgumentException>(() => catalog.Register(null!, fail));
            Assert.Throws<ArgumentException>(() => catalog.Register("", fail));
            Assert.Throws<ArgumentNullException>(() => catalog.Register("valid", null!));
            catalog.Register(" exact ", fail);
            catalog.Register(" ", fail);
            Assert.Throws<InvalidOperationException>(() => catalog.Register(" exact ", fail));
            foreach (var name in new[] { null!, "", "exact", " Exact ", "missing" })
            {
                Assert.That(catalog.TryExecute(name, "opaque", out var missing), Is.False);
                Assert.That(missing.Success, Is.False);
                Assert.That(missing.Message, Is.EqualTo("Debug command is not registered."));
                Assert.That(missing.PayloadJson, Is.Empty);
            }
            Assert.That(calls, Is.Zero);
            Assert.That(catalog.TryExecute(" exact ", "not JSON", out var failed), Is.True);
            Assert.That(failed.Success, Is.False);
            Assert.That(failed.Message, Is.EqualTo("not JSON"));
            Assert.That(catalog.TryExecute(" ", null, out var empty), Is.True);
            Assert.That(empty.Message, Is.Empty);
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(catalog.Count, Is.EqualTo(2));
        }

        [Test]
        public void Result_ConstructorNormalizesNull_WhileDefaultRemainsZeroInitialized()
        {
            var result = new DebugCommandResult(true, null, null);
            Assert.That(result.Message, Is.Empty);
            Assert.That(result.PayloadJson, Is.Empty);
            Assert.That(default(DebugCommandResult).Message, Is.Null);
            Assert.That(default(DebugCommandResult).PayloadJson, Is.Null);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Dispatcher_SharesTheInternalHandler_AndCopiesBusinessOutcome(bool success)
        {
            var catalog = new DebugCommandCatalog();
            var calls = 0;
            var callerThread = Thread.CurrentThread.ManagedThreadId;
            catalog.Register("shared", payload =>
            {
                Assert.That(Thread.CurrentThread.ManagedThreadId, Is.EqualTo(callerThread));
                calls++;
                return new DebugCommandResult(success, "handled", payload);
            });
            Assert.That(catalog.TryExecute("shared", "opaque", out var internalResult), Is.True);
            var dispatcher = new CatalogDebugCommandDispatcher(catalog);
            var result = dispatcher.DispatchAsync(new DebugCommandEnvelopeV1
            {
                RequestId = "shared-id", CommandType = "shared", PayloadJson = "opaque"
            }, CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(result.RequestId, Is.EqualTo("shared-id"));
            Assert.That(result.Success, Is.EqualTo(internalResult.Success));
            Assert.That(result.Message, Is.EqualTo(internalResult.Message));
            Assert.That(result.PayloadJson, Is.EqualTo(internalResult.PayloadJson));
        }

        [Test]
        public void Dispatcher_PreCanceledTokenPreventsHandlerSideEffects()
        {
            var catalog = new DebugCommandCatalog();
            var calls = 0;
            catalog.Register("side-effect", _ => { calls++; return DebugCommandResult.Ok("ran", ""); });
            var dispatcher = new CatalogDebugCommandDispatcher(catalog);
            var result = dispatcher.DispatchAsync(new DebugCommandEnvelopeV1
            {
                RequestId = "cancel-id", CommandType = "side-effect"
            }, new CancellationToken(true)).GetAwaiter().GetResult();
            Assert.That(calls, Is.Zero);
            Assert.That(result.RequestId, Is.EqualTo("cancel-id"));
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.EqualTo("Debug command was canceled."));
            Assert.That(result.PayloadJson, Is.Empty);
        }

        [Test]
        public void Dispatcher_NormalizesDefaultResultAndMissingCommand()
        {
            var catalog = new DebugCommandCatalog();
            catalog.Register("default", _ => default);
            var dispatcher = new CatalogDebugCommandDispatcher(catalog);
            var result = dispatcher.DispatchAsync(new DebugCommandEnvelopeV1
            {
                RequestId = null!, CommandType = "default"
            }, CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(result.RequestId, Is.Empty);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.Empty);
            Assert.That(result.PayloadJson, Is.Empty);
            var missing = dispatcher.DispatchAsync(null!, CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(missing.RequestId, Is.Empty);
            Assert.That(missing.Success, Is.False);
            Assert.That(missing.Message, Is.EqualTo("Debug command is missing."));
            Assert.That(missing.PayloadJson, Is.Empty);
            Assert.Throws<ArgumentNullException>(() => new CatalogDebugCommandDispatcher(null!));
        }

        [Test]
        public void HandlerException_PropagatesThroughBothDirectCallers()
        {
            var catalog = new DebugCommandCatalog();
            var expected = new InvalidOperationException("handler failed");
            catalog.Register("throw", _ => throw expected);
            Assert.That(Assert.Throws<InvalidOperationException>(
                () => catalog.TryExecute("throw", "", out _)), Is.SameAs(expected));
            var dispatcher = new CatalogDebugCommandDispatcher(catalog);
            Assert.That(Assert.Throws<InvalidOperationException>(() =>
                dispatcher.DispatchAsync(new DebugCommandEnvelopeV1 { CommandType = "throw" },
                    CancellationToken.None).GetAwaiter().GetResult()), Is.SameAs(expected));
        }
        [Test]
        public void Catalog_ExecuteDoesNotAllocate()
        {
            var catalog = new DebugCommandCatalog();
            var ok = DebugCommandResult.Ok("ok", "{}");
            catalog.Register("sample.ok", _ => ok);
            for (var i = 0; i < 16; i++)
            {
                catalog.TryExecute("sample.ok", "{}", out _);
            }

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 2000; i++)
            {
                catalog.TryExecute("sample.ok", "{}", out _);
            }

            var after = GC.GetAllocatedBytesForCurrentThread();
            Assert.That(after, Is.EqualTo(before));
        }

        [Test]
        public void Dispatcher_UnknownCommandIsAFailedEnvelope()
        {
            var dispatcher = new CatalogDebugCommandDispatcher(new DebugCommandCatalog());
            var command = new DebugCommandEnvelopeV1
            {
                RequestId = "req-2",
                CommandType = "missing",
            };

            var result = dispatcher.DispatchAsync(command, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(result.RequestId, Is.EqualTo("req-2"));
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.EqualTo("Debug command is not registered."));
        }
    }
}
