#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using SampleGame.InGame;
using SampleGame.InGame.Player;
using SampleGame.InGame.Streaming;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace OneStarMaker.Tests.Streaming
{
    /// <summary>
    /// S-4b: PlayerWorldReadySequence の入力順と失敗時 Focus 解除。Unity GO は使わない。
    /// </summary>
    [TestFixture]
    public sealed class PlayerWorldReadySequenceTests
    {
        [Test]
        public void Run_TeleportThenRegisterThenInputOn()
        {
            var session = new FakeSession();
            var actor = new FakeActor();

            PlayerWorldReadySequence.Run(session, actor, new Vector3(1f, 2f, 3f));

            Assert.That(actor.Log, Is.EqualTo(new[] { "teleport", "inputOn" }));
            Assert.That(actor.Position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(actor.InputEnabled, Is.True);
            Assert.That(session.Registered, Is.SameAs(actor));
        }

        [Test]
        public void Run_RegisterThrows_UnregistersAndKeepsInputOff()
        {
            var session = new FakeSession { ThrowOnRegister = true };
            var actor = new FakeActor();

            Assert.Throws<InvalidOperationException>(
                () => PlayerWorldReadySequence.Run(session, actor, Vector3.zero));

            Assert.That(actor.InputEnabled, Is.False);
            Assert.That(session.Registered, Is.Null);
            Assert.That(actor.Log, Is.EqualTo(new[] { "teleport", "inputOff" }));
        }

        [Test]
        public void Run_InputOnThrows_UnregistersAndForcesInputOff()
        {
            var session = new FakeSession();
            var actor = new FakeActor { ThrowOnInputOn = true };

            Assert.Throws<InvalidOperationException>(
                () => PlayerWorldReadySequence.Run(session, actor, Vector3.zero));

            Assert.That(actor.InputEnabled, Is.False);
            Assert.That(session.Unregistered, Is.SameAs(actor));
        }

        private sealed class FakeActor : IPlayerReadyActor
        {
            internal List<string> Log { get; } = new();
            internal bool ThrowOnInputOn { get; set; }
            private bool _inputEnabled;

            public Vector3 Position { get; private set; }
            public bool InputEnabled
            {
                get => _inputEnabled;
                set
                {
                    if (value && ThrowOnInputOn)
                    {
                        throw new InvalidOperationException("input on failed");
                    }

                    _inputEnabled = value;
                    Log.Add(value ? "inputOn" : "inputOff");
                }
            }

            public void Teleport(Vector3 worldPosition, Vector3 lookForward)
            {
                Position = worldPosition;
                Log.Add("teleport");
            }
        }

        private sealed class FakeSession : IInGameSessionServices
        {
            internal bool ThrowOnRegister { get; set; }
            internal IFlightReadModel? Registered { get; private set; }
            internal IFlightReadModel? Unregistered { get; private set; }

            public IFlightReadModel? Flight => Registered;
            public Vector3? FocusWorldPosition => Registered?.Position;
            public string? CurrentCellIdentity => null;
            public IReadOnlyList<string> ResidentCellIdentities => Array.Empty<string>();
            public IReadOnlyList<string> LoadedChildSceneIdentities => Array.Empty<string>();
            public bool IsStreamingActive => false;
            public bool IsWorldReady => true;
            public UniTask WaitUntilWorldReady(CancellationToken ct) => UniTask.CompletedTask;
            public void StopWorldOperations()
            {
            }

            public void RegisterFlight(IFlightReadModel flight)
            {
                if (ThrowOnRegister)
                {
                    throw new InvalidOperationException("register failed");
                }

                Registered = flight;
            }

            public void UnregisterFlight(IFlightReadModel flight)
            {
                Unregistered = flight;
                if (ReferenceEquals(Registered, flight))
                {
                    Registered = null;
                }
            }
        }
    }
}
