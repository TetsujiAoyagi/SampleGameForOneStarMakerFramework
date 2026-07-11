#nullable enable

using System.Collections;
using NUnit.Framework;
using OneStarMaker.Foundation.DebugSocket;
using OneStarMaker.Runtime.DebugSocketServices;
using UnityEngine;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.Foundation
{
    /// <summary>
    /// DS-06: hierarchy publisher の snapshot/delta 選択と published state 契約を固定する。
    /// </summary>
    [TestFixture]
    public sealed class DebugSocketHierarchyPublisherTests
    {
        private DebugSocketRuntimeNodeRegistry _registry = null!;
        private DebugSocketHierarchyPublisher _publisher = null!;
        private GameObject _gameObject = null!;

        [SetUp]
        public void SetUp()
        {
            _registry = new DebugSocketRuntimeNodeRegistry();
            _publisher = new DebugSocketHierarchyPublisher(_registry);
            _gameObject = new GameObject("DebugSocketHierarchyPublisherTestObject");
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
            {
                Object.DestroyImmediate(_gameObject);
            }
        }

        [Test]
        public void CreateSnapshotFrameUnsafe_FirstCapture_PublishesSnapshotWithScopeName()
        {
            // 守る契約: 初回 capture は snapshot を発行し revision と ScopeName を載せること。
            // 退行時の障害: handshake 後に hierarchy が届かず viewer が空 tree のままになる。
            var capture = _publisher.CaptureUnsafe();

            var framed = _publisher.CreateSnapshotFrameUnsafe(capture);

            Assert.That(
                DebugSocketProtocol.TryDeserializeEnvelope(framed, out var envelope),
                Is.True);
            Assert.That(envelope!.MessageType, Is.EqualTo((int)DebugSocketMessageType.HierarchySnapshot));
            Assert.That(
                DebugSocketProtocol.TryDeserializePayload(envelope, out HierarchySnapshotEnvelopeV1? snapshot),
                Is.True);
            Assert.That(snapshot!.Revision, Is.EqualTo(1));
            Assert.That(snapshot.ScopeName, Is.EqualTo("Loaded Scenes"));
            Assert.That(snapshot.Nodes.Length, Is.GreaterThan(0));
            Assert.That(_publisher.HasPublishedStateUnsafe(), Is.True);
        }

        [Test]
        public void TryCreateDeltaFrameUnsafe_WithoutPublishedState_ReturnsFalse()
        {
            // 守る契約: published 正本が無いときは delta を作らず snapshot フォールバックへ委ねること。
            // 退行時の障害: 初回接続で delta が送られ BaseRevision が不整合になる。
            var capture = _publisher.CaptureUnsafe();

            Assert.That(
                _publisher.TryCreateDeltaFrameUnsafe(capture, out var framedMessage),
                Is.False);
            Assert.That(framedMessage, Is.EqualTo(System.Array.Empty<byte>()));
            Assert.That(_publisher.HasPublishedStateUnsafe(), Is.False);
        }

        [Test]
        public void TryCreateDeltaFrameUnsafe_NoChanges_ReturnsFalseAndKeepsPublishedState()
        {
            // 守る契約: 差分 0 件では frame を作らず、無駄な enqueue を避けること。
            // 退行時の障害: 変更なしでも毎回 delta/snapshot が流れ viewer が再描画し続ける。
            var firstCapture = _publisher.CaptureUnsafe();
            _publisher.CreateSnapshotFrameUnsafe(firstCapture);

            var secondCapture = _publisher.CaptureUnsafe();

            Assert.That(
                _publisher.TryCreateDeltaFrameUnsafe(secondCapture, out var framedMessage),
                Is.False);
            Assert.That(framedMessage, Is.EqualTo(System.Array.Empty<byte>()));
            Assert.That(_publisher.HasPublishedStateUnsafe(), Is.True);
        }

        [UnityTest]
        public IEnumerator TryCreateDeltaFrameUnsafe_RenameNode_ReturnsUpsertDelta()
        {
            // 守る契約: ノード属性変化時は Upsert delta を生成し BaseRevision を維持すること。
            // 退行時の障害: rename が viewer に反映されず tree 表示が古いままになる。
            var firstCapture = _publisher.CaptureUnsafe();
            _publisher.CreateSnapshotFrameUnsafe(firstCapture);

            _gameObject.name = "RenamedHierarchyNode";
            yield return null;

            var secondCapture = _publisher.CaptureUnsafe();

            Assert.That(
                _publisher.TryCreateDeltaFrameUnsafe(secondCapture, out var framedMessage),
                Is.True);
            Assert.That(framedMessage, Is.Not.Null);

            Assert.That(
                DebugSocketProtocol.TryDeserializeEnvelope(framedMessage!, out var envelope),
                Is.True);
            Assert.That(envelope!.MessageType, Is.EqualTo((int)DebugSocketMessageType.HierarchyDelta));
            Assert.That(
                DebugSocketProtocol.TryDeserializePayload(envelope, out HierarchyDeltaEnvelopeV1? delta),
                Is.True);
            Assert.That(delta!.BaseRevision, Is.EqualTo(1));
            Assert.That(delta.Revision, Is.EqualTo(2));
            Assert.That(delta.ScopeName, Is.EqualTo("Loaded Scenes"));
            Assert.That(delta.Changes.Length, Is.EqualTo(1));
            Assert.That(delta.Changes[0].ChangeKind, Is.EqualTo(HierarchyChangeKind.Upsert));
            Assert.That(delta.Changes[0].Name, Is.EqualTo("RenamedHierarchyNode"));
        }

        [Test]
        public void ResetUnsafe_ClearsPublishedStateAndRegistryMappings()
        {
            // 守る契約: session 切替時に published 正本と token cache を同時に消すこと。
            // 退行時の障害: 旧 session の token が新 session の inspector query へ誤着弾する。
            var capture = _publisher.CaptureUnsafe();
            var framed = _publisher.CreateSnapshotFrameUnsafe(capture);
            Assert.That(
                DebugSocketProtocol.TryDeserializeEnvelope(framed, out var envelope),
                Is.True);
            Assert.That(
                DebugSocketProtocol.TryDeserializePayload(envelope!, out HierarchySnapshotEnvelopeV1? snapshot),
                Is.True);
            var nodeId = snapshot!.Nodes[0].NodeId;

            _publisher.ResetUnsafe();

            Assert.That(_publisher.HasPublishedStateUnsafe(), Is.False);
            Assert.That(
                _registry.TryFindGameObjectByNodeId(nodeId, out _, out _),
                Is.False);
        }
    }
}
