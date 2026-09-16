#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OneStarMaker.Editor.Build.Materialization;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.SceneSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace OneStarMaker.Tests.Editor.Build
{
    public sealed class UnityAssetDatabaseGatewayTests
    {
        private string? _folder;
        private SceneResourceMap? _map;

        [TearDown]
        public void TearDown()
        {
            if (_map != null) UnityEngine.Object.DestroyImmediate(_map);
            _map = null;
            if (_folder != null && AssetDatabase.IsValidFolder(_folder))
                Assert.That(AssetDatabase.DeleteAsset(_folder), Is.True);
            _folder = null;
        }

        [Test]
        public void RealAssetDatabase_ResolvesRootAndRecursiveDependency()
        {
            _folder = "Assets/__BS2aGatewayTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", _folder.Substring("Assets/".Length));
            var childPath = _folder + "/Child.asset";
            var rootPath = _folder + "/Root.asset";

            var child = ScriptableObject.CreateInstance<SceneResource>();
            child.Identity = "Child";
            AssetDatabase.CreateAsset(child, childPath);
            var root = ScriptableObject.CreateInstance<SceneResource>();
            root.Identity = "Root";
            typeof(SceneResource).GetField("_parent", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(root, child);
            AssetDatabase.CreateAsset(root, rootPath);
            AssetDatabase.SaveAssets();

            var rootGuid = AssetDatabase.AssetPathToGUID(rootPath);
            var childGuid = AssetDatabase.AssetPathToGUID(childPath);
            var gateway = new UnityAssetDatabaseGateway();
            Assert.That(gateway.IsFolder(_folder), Is.True);
            Assert.That(gateway.FileExists(rootPath), Is.True);
            Assert.That(gateway.GuidToPath(rootGuid), Is.EqualTo(rootPath));
            Assert.That(gateway.PathToGuid(rootPath), Is.EqualTo(rootGuid));
            Assert.That(gateway.GetDependencies(rootPath), Does.Contain(childPath));

            var description = new SceneAssetDescription("Root", LoadType.OnDemand,
                new List<AssetPayload> { new AssetPayload(string.Empty, new AssetReference(rootGuid)) });
            typeof(SceneResource).GetField("_sceneAssetDescription", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(root, description);
            _map = ScriptableObject.CreateInstance<SceneResourceMap>();
            typeof(SceneResourceMap).GetField("_sceneResources", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(_map, new List<SceneResource> { root });

            var result = new SceneResourceContentMaterializer().Materialize(_map);
            Assert.That(result.Issues, Is.Empty);
            Assert.That(result.Snapshot, Is.Not.Null);
            var closure = result.Snapshot!.FindDependency(rootGuid);
            Assert.That(closure, Is.Not.Null);
            Assert.That(closure!.Entries.Select(x => x.Guid), Does.Contain(rootGuid));
            Assert.That(closure.Entries.Select(x => x.Guid), Does.Contain(childGuid));
        }
    }
}
