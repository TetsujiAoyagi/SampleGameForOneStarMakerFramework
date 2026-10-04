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
        public void Dispatcher_CopiesTheCatalogResultOntoTheEnvelope()
        {
            var catalog = new DebugCommandCatalog();
            catalog.Register("sample.echo", payload => DebugCommandResult.Ok("echo", payload));
            var dispatcher = new CatalogDebugCommandDispatcher(catalog);
            var command = new DebugCommandEnvelopeV1
            {
                RequestId = "req-1",
                CommandType = "sample.echo",
                PayloadJson = "payload",
            };

            var result = dispatcher.DispatchAsync(command, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(result.RequestId, Is.EqualTo("req-1"));
            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Is.EqualTo("echo"));
            Assert.That(result.PayloadJson, Is.EqualTo("payload"));
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
