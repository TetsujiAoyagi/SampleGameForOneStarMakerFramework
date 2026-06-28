#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Tests.AssetManagement;
using OneStarMaker.Tests.SceneSystem.Helpers;
using OneStarMaker.Tests.SceneSystem.TestDoubles;
using OneStarMaker.Runtime.UISystem;
using UnityEngine;

namespace OneStarMaker.Tests.SceneSystem
{
    /// <summary>
    /// SceneDirector テストの共通基底クラス。
    /// SetUp / TearDown とセットアップヘルパーを提供する。
    /// </summary>
    public abstract class SceneDirectorTestBase
    {
        protected TestableSceneDirector Director = null!;
        protected FakeSceneFactory Factory = null!;
        protected UICommon UICommon = null!;
        protected SceneResourceMap Map = null!;

        /// <summary>
        /// FakeAssetBackend 入り AssetManagement。
        /// SceneDirector テスト全体で共有し、Addressables ビルドなしで実行する。
        /// </summary>
        protected Runtime.AssetManagement.AssetManagement AssetManagement = null!;
        protected readonly List<ScriptableObject> CreatedSOs = new();

        private GameObject _uiCommonGo = null!;

        [SetUp]
        public void SetUp()
        {
            _uiCommonGo = new GameObject("UICommon_Test");
            UICommon = _uiCommonGo.AddComponent<UICommon>();
            Factory = new FakeSceneFactory();
            // Addressables 直叩きを排除した AssetManagement を全 SceneDirector テストで共有
            AssetManagement = new Runtime.AssetManagement.AssetManagement(new FakeAssetBackend());
        }

        [TearDown]
        public void TearDown()
        {
            Director?.Dispose();

            foreach (var so in CreatedSOs)
            {
                if (so != null)
                {
                    Object.DestroyImmediate(so);
                }
            }
            CreatedSOs.Clear();

            if (_uiCommonGo != null)
            {
                Object.DestroyImmediate(_uiCommonGo);
            }
        }

        // ─── Setup helpers ───

        /// <summary>単一シーンの SceneDirector を構築する。</summary>
        protected TestableSceneDirector SetupSingleScene(
            string identity = "TestScene",
            LoadType loadType = LoadType.OnDemand)
        {
            var resource = SceneTestHelper.CreateSceneResource(identity, loadType);
            CreatedSOs.Add(resource);

            Map = SceneTestHelper.CreateSceneResourceMap(resource);
            CreatedSOs.Add(Map);

            Director = new TestableSceneDirector(Factory, UICommon, Map, AssetManagement);
            return Director;
        }

        /// <summary>親子2階層の SceneDirector を構築する。</summary>
        protected TestableSceneDirector SetupParentChild(
            string parentId = "Parent",
            string childId = "Child",
            LoadType childLoadType = LoadType.NecessaryAlways)
        {
            var parentRes = SceneTestHelper.CreateSceneResource(parentId);
            var childRes = SceneTestHelper.CreateSceneResource(childId, childLoadType, parentRes);
            SceneTestHelper.AddChild(parentRes, childRes);

            CreatedSOs.Add(parentRes);
            CreatedSOs.Add(childRes);

            Map = SceneTestHelper.CreateSceneResourceMap(parentRes, childRes);
            CreatedSOs.Add(Map);

            Director = new TestableSceneDirector(Factory, UICommon, Map, AssetManagement);
            return Director;
        }

        /// <summary>親 → 子 → 孫 の3階層を構築する。</summary>
        protected TestableSceneDirector SetupThreeLevel(
            string rootId = "Root",
            string midId = "Mid",
            string leafId = "Leaf",
            LoadType midLoadType = LoadType.NecessaryAlways,
            LoadType leafLoadType = LoadType.NecessaryAlways)
        {
            var rootRes = SceneTestHelper.CreateSceneResource(rootId);
            var midRes = SceneTestHelper.CreateSceneResource(midId, midLoadType, rootRes);
            var leafRes = SceneTestHelper.CreateSceneResource(leafId, leafLoadType, midRes);
            SceneTestHelper.AddChild(rootRes, midRes);
            SceneTestHelper.AddChild(midRes, leafRes);

            CreatedSOs.Add(rootRes);
            CreatedSOs.Add(midRes);
            CreatedSOs.Add(leafRes);

            Map = SceneTestHelper.CreateSceneResourceMap(rootRes, midRes, leafRes);
            CreatedSOs.Add(Map);

            Director = new TestableSceneDirector(Factory, UICommon, Map, AssetManagement);
            return Director;
        }
    }
}
