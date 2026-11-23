#nullable enable

using System.Reflection;
using NUnit.Framework;
using OneStarMaker.Foundation.DebugSocket;
using OneStarMaker.Runtime.DebugSocketServices;
using UnityEngine;

namespace OneStarMaker.Tests.Foundation
{
    /// <summary>
    /// <see cref="DebugSocketService.TryGetHierarchyGameObject"/> の Dispose 後の契約を固定する。
    /// lookup 本体の挙動（未知・prune・reset・破棄済み）は <see cref="DebugSocketRuntimeNodeRegistryTests"/> が持つ。
    /// </summary>
    [TestFixture]
    public sealed class DebugSocketServiceHierarchyLookupTests
    {
        private GameObject _gameObject = null!;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("DebugSocketServiceLookupTestObject");
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
        public void TryGetHierarchyGameObject_AfterDispose_ReturnsFalseEvenIfMappingRemains()
        {
            // 守る契約: Dispose の後は、registry に対応が残っていても false を返すこと。
            // Dispose は _disposed を立ててから片付けを非同期で始めるので、対応表が残る間がある（ここでは transport を
            // 起動していないので片付けで対応表は消えない）。
            // 退行時の障害: 終わりかけの service が古い対応で GameObject を返し、ゲーム側のデバッグコマンドが書き込む。
            var service = new DebugSocketService(new DebugSocketOptions(), NullDebugCommandDispatcher.Instance);
            try
            {
                var nodeId = GetRegistry(service).CreateRuntimeNodeIdUnsafe(_gameObject);
                Assert.That(service.TryGetHierarchyGameObject(nodeId, out var beforeDispose), Is.True);
                Assert.That(beforeDispose == _gameObject, Is.True);

                service.Dispose();

                Assert.That(service.TryGetHierarchyGameObject(nodeId, out var afterDispose), Is.False);
                Assert.That(afterDispose == null, Is.True);
                Assert.That(GetRegistry(service).TryFindGameObjectByNodeId(nodeId, out _, out _), Is.True,
                    "前提: Dispose の直後も registry の対応は残っている");
            }
            finally
            {
                service.Dispose();
            }
        }

        // 対応表は capture でしか作られず、capture には接続中のセッションが要る。テストでは registry へ直接登録する。
        private static DebugSocketRuntimeNodeRegistry GetRegistry(DebugSocketService service)
        {
            var field = typeof(DebugSocketService).GetField(
                "_runtimeNodeRegistry",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "DebugSocketService._runtimeNodeRegistry が見つからない");
            return (DebugSocketRuntimeNodeRegistry)field!.GetValue(service);
        }
    }
}
