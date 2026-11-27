#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Foundation.DebugSocket;
using OneStarMaker.Runtime.DebugCommands;
using OneStarMaker.Runtime.DebugSocketServices;

namespace OneStarMaker.Tests.DebugCommands
{
    /// <summary>
    /// Framework のコマンドを Game の dispatcher の前で振り分ける dispatcher が、Framework の名前だけを自分で処理し、
    /// それ以外を Game へ今と同じ command と token で 1 回だけ渡すことを固定する（AC7）。受信スレッドや実ソケットは開かない。
    /// </summary>
    [TestFixture]
    public sealed class FrameworkFirstDebugCommandDispatcherTests
    {
        private const string FrameworkName = "osm.scenes";

        [Test]
        public void FrameworkName_IsHandledHere_AndDoesNotReachTheGame()
        {
            var game = new RecordingDispatcher();
            var handlerCalls = 0;
            var dispatcher = new FrameworkFirstDebugCommandDispatcher(game);
            dispatcher.Register(FrameworkName, payload =>
            {
                handlerCalls++;
                return DebugCommandResult.Ok("framework", "{\"echo\":" + payload + "}");
            });

            var result = Dispatch(dispatcher, Command(FrameworkName, "r1", "{}"), CancellationToken.None);

            Assert.That(handlerCalls, Is.EqualTo(1));
            Assert.That(game.Calls, Is.Empty, "Framework の名前は Game を呼ばない");
            Assert.That(result.RequestId, Is.EqualTo("r1"));
            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Is.EqualTo("framework"));
            Assert.That(result.PayloadJson, Is.EqualTo("{\"echo\":{}}"));
        }

        [Test]
        public void FrameworkName_PreCanceled_ReturnsCanceledWithoutCallingHandlerOrGame()
        {
            var game = new RecordingDispatcher();
            var handlerCalls = 0;
            var dispatcher = new FrameworkFirstDebugCommandDispatcher(game);
            dispatcher.Register(FrameworkName, _ =>
            {
                handlerCalls++;
                return DebugCommandResult.Ok("framework", null);
            });

            var result = Dispatch(dispatcher, Command(FrameworkName, "r2"), new CancellationToken(true));

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.EqualTo("Debug command was canceled."));
            Assert.That(result.RequestId, Is.EqualTo("r2"));
            Assert.That(handlerCalls, Is.EqualTo(0));
            Assert.That(game.Calls, Is.Empty, "cancel 済みの Framework のコマンドを Game へ渡さない");
        }

        [TestCase("debug.set-active", false)]
        [TestCase("debug.set-active", true)]
        [TestCase("osm.foo", false)]
        [TestCase("OSM.SCENES", false)]
        [TestCase("osm.scenes ", false)]
        [TestCase("", false)]
        public void OtherNames_GoToTheGameOnceWithTheSameCommandAndToken(string name, bool preCanceled)
        {
            var expected = new DebugCommandResultEnvelopeV1 { RequestId = "game", Success = true, Message = "from-game" };
            var game = new RecordingDispatcher { Result = expected };
            var dispatcher = CreateWithFrameworkCommand(game);
            var command = Command(name, "r3");
            using var cts = new CancellationTokenSource();
            if (preCanceled)
            {
                cts.Cancel();
            }

            var result = Dispatch(dispatcher, command, cts.Token);

            Assert.That(game.Calls.Count, Is.EqualTo(1), "Game を 1 回だけ呼ぶ");
            Assert.That(game.Calls[0].Command, Is.SameAs(command), "同じ command を渡す");
            Assert.That(game.Calls[0].Token, Is.EqualTo(cts.Token), "同じ token を渡す（cancel 済みもそのまま）");
            Assert.That(result, Is.SameAs(expected), "Game の結果をそのまま返す");
        }

        [Test]
        public void NullCommand_GoesToTheGameOnce()
        {
            var expected = new DebugCommandResultEnvelopeV1 { Message = "from-game" };
            var game = new RecordingDispatcher { Result = expected };
            var dispatcher = CreateWithFrameworkCommand(game);

            var result = Dispatch(dispatcher, null!, CancellationToken.None);

            Assert.That(game.Calls.Count, Is.EqualTo(1));
            Assert.That(game.Calls[0].Command, Is.Null);
            Assert.That(result, Is.SameAs(expected));
        }

        [Test]
        public void GameException_PropagatesUnchanged()
        {
            var thrown = new InvalidOperationException("game failed");
            var game = new RecordingDispatcher { Exception = thrown };
            var dispatcher = CreateWithFrameworkCommand(game);

            var caught = Assert.Throws<InvalidOperationException>(
                () => Dispatch(dispatcher, Command("debug.set-active", "r4"), CancellationToken.None));

            Assert.That(caught, Is.SameAs(thrown));
            Assert.That(game.Calls.Count, Is.EqualTo(1));
        }

        [Test]
        public void FrameworkHandlerException_Propagates()
        {
            var thrown = new InvalidOperationException("handler failed");
            var game = new RecordingDispatcher();
            var dispatcher = new FrameworkFirstDebugCommandDispatcher(game);
            dispatcher.Register(FrameworkName, _ => throw thrown);

            var caught = Assert.Throws<InvalidOperationException>(
                () => Dispatch(dispatcher, Command(FrameworkName, "r5"), CancellationToken.None));

            Assert.That(caught, Is.SameAs(thrown), "router が失敗の envelope にするよう、例外を伝播させる");
            Assert.That(game.Calls, Is.Empty);
        }

        [Test]
        public void ThroughTheRealGameCatalogDispatcher_EnvelopesAreIdenticalToCallingTheGameDirectly()
        {
            // Game 側に実の CatalogDebugCommandDispatcher（debug.set-active 相当を登録した catalog）を置き、
            // 振り分けを通した envelope と、Game を直接呼んだ envelope が全ての欄で同じことを見る。
            var catalog = new DebugCommandCatalog();
            catalog.Register("debug.set-active", payload => DebugCommandResult.Ok("applied", "{\"payload\":" + payload + "}"));
            var gameDispatcher = new CatalogDebugCommandDispatcher(catalog);
            var dispatcher = CreateWithFrameworkCommand(gameDispatcher);

            var cases = new List<(string Label, DebugCommandEnvelopeV1? Command, CancellationToken Token)>
            {
                ("null command", null, CancellationToken.None),
                ("unregistered", Command("game.unknown", "u1", "{}"), CancellationToken.None),
                ("pre-canceled", Command("debug.set-active", "c1", "{}"), new CancellationToken(true)),
                ("registered", Command("debug.set-active", "s1", "{\"nodeId\":1}"), CancellationToken.None),
            };

            foreach (var (label, command, token) in cases)
            {
                var direct = Dispatch(gameDispatcher, command!, token);
                var routed = Dispatch(dispatcher, command!, token);

                Assert.That(routed.RequestId, Is.EqualTo(direct.RequestId), label);
                Assert.That(routed.Success, Is.EqualTo(direct.Success), label);
                Assert.That(routed.Message, Is.EqualTo(direct.Message), label);
                Assert.That(routed.PayloadJson, Is.EqualTo(direct.PayloadJson), label);
            }
        }

        [Test]
        public void GameRegisteringTheSameName_StillGetsTheFrameworkResult()
        {
            var catalog = new DebugCommandCatalog();
            catalog.Register(FrameworkName, _ => DebugCommandResult.Ok("game-version", null));
            var dispatcher = new FrameworkFirstDebugCommandDispatcher(new CatalogDebugCommandDispatcher(catalog));
            dispatcher.Register(FrameworkName, _ => DebugCommandResult.Ok("framework-version", null));

            var result = Dispatch(dispatcher, Command(FrameworkName, "d1"), CancellationToken.None);

            Assert.That(result.Message, Is.EqualTo("framework-version"), "Editor では Framework の名前が先に引かれる");
        }

        // ─── helpers ───

        private static FrameworkFirstDebugCommandDispatcher CreateWithFrameworkCommand(IDebugCommandDispatcher game)
        {
            var dispatcher = new FrameworkFirstDebugCommandDispatcher(game);
            dispatcher.Register(FrameworkName, _ => DebugCommandResult.Ok("framework", null));
            return dispatcher;
        }

        private static DebugCommandEnvelopeV1 Command(string name, string requestId, string payloadJson = "")
            => new() { CommandType = name, RequestId = requestId, PayloadJson = payloadJson };

        private static DebugCommandResultEnvelopeV1 Dispatch(
            IDebugCommandDispatcher dispatcher,
            DebugCommandEnvelopeV1 command,
            CancellationToken token)
            => dispatcher.DispatchAsync(command, token).GetAwaiter().GetResult();

        /// <summary>呼ばれた command と token を記録し、決めた結果を返すか例外にする Game の dispatcher。</summary>
        private sealed class RecordingDispatcher : IDebugCommandDispatcher
        {
            public List<(DebugCommandEnvelopeV1? Command, CancellationToken Token)> Calls { get; } = new();

            public DebugCommandResultEnvelopeV1 Result { get; set; } = new();

            public Exception? Exception { get; set; }

            public UniTask<DebugCommandResultEnvelopeV1> DispatchAsync(
                DebugCommandEnvelopeV1 command,
                CancellationToken cancellationToken)
            {
                Calls.Add((command, cancellationToken));
                return Exception != null
                    ? UniTask.FromException<DebugCommandResultEnvelopeV1>(Exception)
                    : UniTask.FromResult(Result);
            }
        }
    }
}
