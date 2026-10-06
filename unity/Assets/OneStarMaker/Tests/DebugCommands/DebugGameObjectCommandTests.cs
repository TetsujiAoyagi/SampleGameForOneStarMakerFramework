#nullable enable

using System.Globalization;
using NUnit.Framework;
using OneStarMaker.Runtime.DebugCommands;
using UnityEngine;

namespace OneStarMaker.Tests.DebugCommands
{
    /// <summary>
    /// 実シーンの走査だけを見る。エディタ上の他オブジェクトは、作った instanceId の完全一致で避ける。
    /// </summary>
    public sealed class DebugGameObjectCommandTests
    {
        [Test]
        public void ListAndSelect_UseInstanceId_AndDropDestroyedObject()
        {
            var root = new GameObject("DebugGoListRoot");
            root.SetActive(false);
            var child = new GameObject("DebugGoListChild");
            child.transform.SetParent(root.transform, false);
            var hidden = new GameObject("DebugGoListHidden");
            hidden.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var world = new UnityDebugGameObjectWorld();
                var selection = new DebugGameObjectSelection();
                var catalog = new DebugCommandCatalog();
                DebugGameObjectCommands.RegisterListAndSelect(catalog, world, selection);
                var childId = IdOf(child);
                var hiddenId = IdOf(hidden);

                Assert.That(FindInPages(catalog, childId), Is.True, "created child was not listed");
                Assert.That(
                    catalog.TryExecute(DebugGameObjectCommands.SelectName, "{\"instanceId\":\"" + childId + "\"}", out var selected),
                    Is.True);
                Assert.That(selected.Success, Is.True, selected.Message);
                Assert.That(selected.PayloadJson, Does.Contain("\"name\":\"DebugGoListChild\""));
                Assert.That(selected.PayloadJson, Does.Contain("\"activeSelf\":true"));
                Assert.That(selected.PayloadJson, Does.Contain("\"activeInHierarchy\":false"));
                Assert.That(selected.PayloadJson, Does.Contain("\"displayPath\":\"/DebugGoListRoot/DebugGoListChild\""));
                Assert.That(selection.TryGet(out var chosen), Is.True);
                Assert.That(chosen.ToString(CultureInfo.InvariantCulture), Is.EqualTo(childId));

                Assert.That(
                    catalog.TryExecute(DebugGameObjectCommands.SelectName, "{\"instanceId\":\"" + hiddenId + "\"}", out var hiddenResult),
                    Is.True);
                Assert.That(hiddenResult.Success, Is.False);
                Assert.That(hiddenResult.Message, Is.EqualTo("GameObject is not alive."));
                Assert.That(selection.TryGet(out _), Is.False);

                UnityEngine.Object.DestroyImmediate(child);
                Assert.That(
                    catalog.TryExecute(DebugGameObjectCommands.SelectName, "{\"instanceId\":\"" + childId + "\"}", out var dead),
                    Is.True);
                Assert.That(dead.Success, Is.False);
                Assert.That(dead.Message, Is.EqualTo("GameObject is not alive."));
            }
            finally
            {
                if (child != null)
                {
                    UnityEngine.Object.DestroyImmediate(child);
                }

                if (hidden != null)
                {
                    UnityEngine.Object.DestroyImmediate(hidden);
                }

                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void SetActive_LeavesInactiveParentInactive()
        {
            var root = new GameObject("DebugGoActiveRoot");
            root.SetActive(false);
            var child = new GameObject("DebugGoActiveChild");
            child.transform.SetParent(root.transform, false);
            child.SetActive(false);
            try
            {
                var world = new UnityDebugGameObjectWorld();
                var selection = new DebugGameObjectSelection();
                var catalog = new DebugCommandCatalog();
                DebugGameObjectCommands.RegisterSetActive(catalog, world, selection);
                var childId = IdOf(child);
                Assert.That(
                    catalog.TryExecute(
                        DebugGameObjectCommands.SetActiveName,
                        "{\"instanceId\":\"" + childId + "\",\"active\":true}",
                        out var changed),
                    Is.True);
                Assert.That(changed.Success, Is.True, changed.Message);
                Assert.That(changed.Message, Is.EqualTo("Set activeSelf. An inactive parent still keeps activeInHierarchy false."));
                Assert.That(child.activeSelf, Is.True);
                Assert.That(child.activeInHierarchy, Is.False);
                Assert.That(root.activeSelf, Is.False);
                Assert.That(selection.TryGet(out _), Is.False);
            }
            finally
            {
                if (child != null)
                {
                    UnityEngine.Object.DestroyImmediate(child);
                }

                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void SetTransform_WritesChildLocalPosition_AndLeavesParentPosition()
        {
            var root = new GameObject("DebugGoTransformRoot");
            root.transform.position = new Vector3(10f, 0f, 0f);
            var child = new GameObject("DebugGoTransformChild");
            child.transform.SetParent(root.transform, false);
            try
            {
                var world = new UnityDebugGameObjectWorld();
                var selection = new DebugGameObjectSelection();
                var catalog = new DebugCommandCatalog();
                DebugGameObjectCommands.RegisterTransform(catalog, world, selection);
                var childId = IdOf(child);
                Assert.That(
                    catalog.TryExecute(
                        DebugGameObjectCommands.SetTransformName,
                        "{\"instanceId\":\"" + childId + "\",\"localPosition\":{\"x\":1,\"y\":2,\"z\":3}}",
                        out var changed),
                    Is.True);
                Assert.That(changed.Success, Is.True, changed.Message);
                Assert.That(changed.Message, Is.EqualTo("Set local transform."));
                Assert.That(child.transform.localPosition.x, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(child.transform.localPosition.y, Is.EqualTo(2f).Within(0.0001f));
                Assert.That(child.transform.localPosition.z, Is.EqualTo(3f).Within(0.0001f));
                Assert.That(root.transform.position.x, Is.EqualTo(10f).Within(0.0001f));
                Assert.That(root.transform.position.y, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(root.transform.position.z, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(selection.TryGet(out _), Is.False);

                Assert.That(
                    catalog.TryExecute(
                        DebugGameObjectCommands.GetTransformName,
                        "{\"instanceId\":\"" + childId + "\"}",
                        out var read),
                    Is.True);
                Assert.That(read.Success, Is.True, read.Message);
                Assert.That(read.Message, Is.EqualTo("Read local transform."));
                Assert.That(read.PayloadJson, Does.Contain("\"localPosition\":{\"x\":1,\"y\":2,\"z\":3}"));
            }
            finally
            {
                if (child != null)
                {
                    UnityEngine.Object.DestroyImmediate(child);
                }

                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static bool FindInPages(DebugCommandCatalog catalog, string instanceId)
        {
            var needle = "\"instanceId\":\"" + instanceId + "\"";
            var offset = 0;
            for (var page = 0; page < 200; page++)
            {
                var request = "{\"offset\":" + offset.ToString(CultureInfo.InvariantCulture) + ",\"limit\":128}";
                Assert.That(catalog.TryExecute(DebugGameObjectCommands.ListName, request, out var listed), Is.True);
                Assert.That(listed.Success, Is.True, listed.Message);
                if (listed.PayloadJson.Contains(needle))
                {
                    return true;
                }

                if (!listed.PayloadJson.Contains("\"hasMore\":true"))
                {
                    return false;
                }

                offset += 128;
            }

            return false;
        }

        private static string IdOf(GameObject gameObject)
        {
            return EntityId.ToULong(gameObject.GetEntityId()).ToString(CultureInfo.InvariantCulture);
        }
    }
}
