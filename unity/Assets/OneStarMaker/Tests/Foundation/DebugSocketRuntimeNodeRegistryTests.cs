#nullable enable

using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using OneStarMaker.Runtime.DebugSocketServices;
using UnityEngine;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.Foundation
{
    /// <summary>
    /// DS-04: stable token registry の契約を固定する。
    /// </summary>
    [TestFixture]
    public sealed class DebugSocketRuntimeNodeRegistryTests
    {
        private DebugSocketRuntimeNodeRegistry _registry = null!;
        private GameObject _gameObject = null!;

        [SetUp]
        public void SetUp()
        {
            _registry = new DebugSocketRuntimeNodeRegistry();
            _gameObject = new GameObject("DebugSocketRegistryTestObject");
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
        public void CreateRuntimeNodeIdUnsafe_SameGameObject_ReusesToken()
        {
            // 守る契約: 同一 GameObject への再登録は同じ token を返すこと。
            // 退行時の障害: hierarchy delta が毎回 Remove/Upsert を繰り返し、viewer が tree を再構築し続ける。
            var first = _registry.CreateRuntimeNodeIdUnsafe(_gameObject);
            var second = _registry.CreateRuntimeNodeIdUnsafe(_gameObject);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void Reset_ClearsMappings_ButDoesNotResetCounter()
        {
            // 守る契約: Reset は辞書だけ消し、採番値は戻さないこと。
            // 退行時の障害: 旧セッションの遅延 query が新オブジェクトへ alias する。
            var beforeReset = _registry.CreateRuntimeNodeIdUnsafe(_gameObject);

            _registry.Reset();

            Assert.That(
                _registry.TryFindGameObjectByNodeId(beforeReset, out _, out _),
                Is.False,
                "Reset 後は旧 token を解決できないこと");

            var afterReset = _registry.CreateRuntimeNodeIdUnsafe(_gameObject);

            Assert.That(afterReset, Is.GreaterThan(beforeReset));
        }

        [Test]
        public void TryFindGameObjectByNodeId_ReturnsSceneAndGameObject()
        {
            // 守る契約: inspector target 解決に scene と GameObject の両方が必要なこと。
            // 退行時の障害: inspector metadata の Scene 名が欠落する。
            var nodeId = _registry.CreateRuntimeNodeIdUnsafe(_gameObject);

            Assert.That(
                _registry.TryFindGameObjectByNodeId(nodeId, out var scene, out var resolved),
                Is.True);
            Assert.That(resolved, Is.SameAs(_gameObject));
            Assert.That(scene, Is.EqualTo(_gameObject.scene));
        }

        [UnityTest]
        public IEnumerator TryFindGameObjectByNodeId_DestroyedGameObject_ReturnsFalseAndPrunesMapping()
        {
            // 守る契約: 破棄済み GameObject の token は NotFound へ落ち、stale mapping を残さないこと。
            // 退行時の障害: 無効参照が残り、後続 query が誤った inspector detail を返す。
            var nodeId = _registry.CreateRuntimeNodeIdUnsafe(_gameObject);
            Object.DestroyImmediate(_gameObject);
            _gameObject = null!;

            yield return null;

            Assert.That(
                _registry.TryFindGameObjectByNodeId(nodeId, out _, out var resolved),
                Is.False);
            Assert.That(resolved, Is.Null);
        }

        [Test]
        public void PruneRuntimeNodeMappingsUnsafe_RemovesUnseenTokens()
        {
            // 守る契約: full capture に現れなかった token は prune で掃除されること。
            // 退行時の障害: 破棄済みノードの token が inspector へ誤着弾する。
            var kept = new GameObject("Kept");
            var removed = new GameObject("Removed");
            try
            {
                var keptId = _registry.CreateRuntimeNodeIdUnsafe(kept);
                var removedId = _registry.CreateRuntimeNodeIdUnsafe(removed);
                var seen = new HashSet<long> { keptId };

                _registry.PruneRuntimeNodeMappingsUnsafe(seen);

                Assert.That(
                    _registry.TryFindGameObjectByNodeId(keptId, out _, out var keptObject),
                    Is.True);
                Assert.That(keptObject, Is.SameAs(kept));
                Assert.That(
                    _registry.TryFindGameObjectByNodeId(removedId, out _, out _),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(kept);
                Object.DestroyImmediate(removed);
            }
        }
    }
}
