#nullable enable

using System;
using NUnit.Framework;
using OneStarMaker.Foundation.DebugSocket;
using OneStarMaker.Runtime.DebugSocketServices;
using OneStarMaker.Runtime.DebugSocketServices.Commands;

namespace OneStarMaker.Tests.Foundation
{
    /// <summary>
    /// 組み込み debug コマンドの応答 JSON 契約を検証する。
    ///
    /// <para>
    /// 受信側（DebugStudio）が解釈する形なので、payload の形・null の扱い・
    /// 文字列エスケープは実装都合で変えられない。別名とケース差も受理する。
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class DebugSocketBuiltInCommandHandlerTests
    {
        [Test]
        public void TryHandle_Ping_ReturnsSuccessWithExpectedPayloadShape()
        {
            var command = new DebugCommandEnvelopeV1
            {
                CommandType = "ping",
            };

            var handled = DebugSocketBuiltInCommandHandler.TryHandle(
                command,
                CreateDiagnosticsSnapshot(),
                out var result);

            Assert.IsTrue(handled);
            Assert.IsTrue(result.Success);
            Assert.AreEqual("DebugSocket ping succeeded.", result.Message);
            Assert.IsTrue(
                result.PayloadJson.StartsWith("{\"service\":\"debugsocket\",\"status\":\"ok\",\"timestampUnixTimeMilliseconds\":", StringComparison.Ordinal),
                "ping payload のプロパティ名と順序が既存互換であること");
            Assert.IsTrue(result.PayloadJson.EndsWith("}", StringComparison.Ordinal));

            var timestampStart = result.PayloadJson.LastIndexOf(':', result.PayloadJson.Length - 2) + 1;
            var timestampEnd = result.PayloadJson.Length - 1;
            var timestampText = result.PayloadJson.Substring(timestampStart, timestampEnd - timestampStart);
            Assert.IsTrue(long.TryParse(timestampText, out var timestampUnixTimeMilliseconds));
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Assert.That(timestampUnixTimeMilliseconds, Is.InRange(now - 5_000, now + 5_000));
        }

        [Test]
        public void TryHandle_Ping_AcceptsAliasAndIgnoresCase()
        {
            foreach (var commandType in new[] { "debugsocket.ping", "PING", "DebugSocket.Ping" })
            {
                var command = new DebugCommandEnvelopeV1
                {
                    CommandType = commandType,
                };

                var handled = DebugSocketBuiltInCommandHandler.TryHandle(
                    command,
                    CreateDiagnosticsSnapshot(),
                    out var result);

                Assert.IsTrue(handled, $"commandType={commandType}");
                Assert.IsTrue(result.Success);
                Assert.AreEqual("DebugSocket ping succeeded.", result.Message);
            }
        }

        [Test]
        public void TryHandle_RuntimeDiagnostics_WithNullSessionIdAndLastStartError_PreservesJsonContract()
        {
            var snapshot = new DebugSocketService.RuntimeDiagnosticsSnapshot(
                transportMode: "listen",
                configuredEndpoint: "ws://localhost:8080",
                listenerPrefix: "http://localhost:8080/debugsocket/",
                autoStart: true,
                isRunning: true,
                hasActiveSession: false,
                sessionId: null,
                pendingQueueLength: 0,
                maxQueueLength: 100,
                droppedBeforeSessionCount: 5,
                droppedQueueOverflowCount: 2,
                lastStartError: null);

            var command = new DebugCommandEnvelopeV1
            {
                CommandType = "runtime-diagnostics",
            };

            var handled = DebugSocketBuiltInCommandHandler.TryHandle(command, snapshot, out var result);

            Assert.IsTrue(handled);
            Assert.IsTrue(result.Success);
            Assert.AreEqual("Runtime diagnostics snapshot captured.", result.Message);
            Assert.AreEqual(
                "{\"transportMode\":\"listen\",\"configuredEndpoint\":\"ws://localhost:8080\",\"listenerPrefix\":\"http://localhost:8080/debugsocket/\",\"autoStart\":true,\"isRunning\":true,\"hasActiveSession\":false,\"sessionId\":null,\"pendingQueueLength\":0,\"maxQueueLength\":100,\"droppedBeforeSessionCount\":5,\"droppedQueueOverflowCount\":2,\"lastStartError\":null}",
                result.PayloadJson);
        }

        [Test]
        public void TryHandle_RuntimeDiagnostics_EscapesStringValuesAndAcceptsAlias()
        {
            var snapshot = new DebugSocketService.RuntimeDiagnosticsSnapshot(
                transportMode: "path\\mode",
                configuredEndpoint: "endpoint\"name",
                listenerPrefix: "prefix\\path\"",
                autoStart: false,
                isRunning: false,
                hasActiveSession: true,
                sessionId: "session\"id",
                pendingQueueLength: 3,
                maxQueueLength: 16,
                droppedBeforeSessionCount: 1,
                droppedQueueOverflowCount: 4,
                lastStartError: "start\\error\"message");

            var command = new DebugCommandEnvelopeV1
            {
                CommandType = "debugsocket.runtime-diagnostics",
            };

            var handled = DebugSocketBuiltInCommandHandler.TryHandle(command, snapshot, out var result);

            Assert.IsTrue(handled);
            Assert.AreEqual(
                "{\"transportMode\":\"path\\\\mode\",\"configuredEndpoint\":\"endpoint\\\"name\",\"listenerPrefix\":\"prefix\\\\path\\\"\",\"autoStart\":false,\"isRunning\":false,\"hasActiveSession\":true,\"sessionId\":\"session\\\"id\",\"pendingQueueLength\":3,\"maxQueueLength\":16,\"droppedBeforeSessionCount\":1,\"droppedQueueOverflowCount\":4,\"lastStartError\":\"start\\\\error\\\"message\"}",
                result.PayloadJson);
        }

        [Test]
        public void TryHandle_UnknownCommand_ReturnsFalseWithDefaultResult()
        {
            var command = new DebugCommandEnvelopeV1
            {
                CommandType = "unknown-command",
            };

            var handled = DebugSocketBuiltInCommandHandler.TryHandle(
                command,
                CreateDiagnosticsSnapshot(),
                out var result);

            Assert.IsFalse(handled);
            Assert.IsFalse(result.Success);
            Assert.AreEqual(string.Empty, result.Message);
            Assert.AreEqual(string.Empty, result.PayloadJson);
            Assert.AreEqual(string.Empty, result.RequestId);
        }

        private static DebugSocketService.RuntimeDiagnosticsSnapshot CreateDiagnosticsSnapshot()
        {
            return new DebugSocketService.RuntimeDiagnosticsSnapshot(
                transportMode: "listen",
                configuredEndpoint: "ws://localhost:8080",
                listenerPrefix: "http://localhost:8080/debugsocket/",
                autoStart: true,
                isRunning: false,
                hasActiveSession: false,
                sessionId: "session-1",
                pendingQueueLength: 0,
                maxQueueLength: 100,
                droppedBeforeSessionCount: 0,
                droppedQueueOverflowCount: 0,
                lastStartError: "none");
        }
    }
}
