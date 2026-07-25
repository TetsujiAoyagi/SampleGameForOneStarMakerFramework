#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Foundation.Telemetry;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.UISystem;
using OneStarMaker.Tests.SceneSystem.Helpers;
using OneStarMaker.Tests.SceneSystem.TestDoubles;
using UnityEngine;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.SceneSystem
{
    /// <summary>
    /// T-04: CellScene 基底 + セル identity バリデータのテスト。
    /// - CellIdentity: `Cell_{x}_{y}` 形式の判定・座標解析
    /// - CellScene: 座標・バウンズのメタデータ運搬（判断ロジックなし）
    /// - R-2: CellScene は UIView を検索・登録しない（構造的強制）
    /// - R-3/G-4: セル identity を SwitchScene に乗せたら明示的に失敗する
    /// </summary>
    [TestFixture]
    public class CellSceneTests : SceneDirectorTestBase
    {
        private readonly List<GameObject> _createdGameObjects = new();

        [TearDown]
        public void CellTearDown()
        {
            foreach (var go in _createdGameObjects)
            {
                if (go != null)
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }
            _createdGameObjects.Clear();
        }

        // ═══════════════════════════════════════════
        //  CellIdentity
        // ═══════════════════════════════════════════

        [Test]
        public void CellIdentity_IsCellId_Detection()
        {
            Assert.IsTrue(CellIdentity.IsCellId("Cell_0_0"));
            Assert.IsTrue(CellIdentity.IsCellId("Cell_12_34"));

            Assert.IsFalse(CellIdentity.IsCellId("Title"));
            Assert.IsFalse(CellIdentity.IsCellId("Cell_x"));
            Assert.IsFalse(CellIdentity.IsCellId("CellFoo"));
            Assert.IsFalse(CellIdentity.IsCellId("Cell_1"), "座標が 1 要素だけの identity はセルではない");
            Assert.IsFalse(CellIdentity.IsCellId("Cell_1_2_3"), "座標が 3 要素の identity はセルではない");
            Assert.IsFalse(CellIdentity.IsCellId("Cell_-1_0"), "負の座標はセルではない（グリッドは非負）");
            Assert.IsFalse(CellIdentity.IsCellId(null));
            Assert.IsFalse(CellIdentity.IsCellId(string.Empty));
        }

        [Test]
        public void CellIdentity_Format_RoundTrips()
        {
            var identity = CellIdentity.Format(3, 5);

            Assert.AreEqual("Cell_3_5", identity);
            Assert.IsTrue(CellIdentity.IsCellId(identity));
            Assert.IsTrue(CellIdentity.TryParse(identity, out var coordinate));
            Assert.AreEqual(new Vector2Int(3, 5), coordinate);
        }

        // ═══════════════════════════════════════════
        //  CellScene: メタデータ運搬
        // ═══════════════════════════════════════════

        [Test]
        public void CellScene_ParsesCellCoordinate_FromIdentity()
        {
            var scene = CreateCellScene("Cell_3_5");

            Assert.AreEqual(new Vector2Int(3, 5), scene.Coordinate);
        }

        [Test]
        public void CellScene_NonCellIdentity_ThrowsArgumentException()
        {
            var resource = SceneTestHelper.CreateSceneResource("Title");
            CreatedSOs.Add(resource);

            Assert.Throws<ArgumentException>(
                () => new CellScene(resource, new NullSceneQuery(), new NullSceneController()),
                "Cell_{x}_{y} 形式でない identity の CellScene 生成は即失敗すべき");
        }

        [Test]
        public void CellScene_Bounds_ComputedFromGridConfig()
        {
            var scene = CreateCellScene("Cell_3_5");
            var grid = new CellGridConfig(
                origin: new Vector3(-500f, 0f, -500f),
                cellSize: 100f,
                height: 50f);

            var bounds = scene.ComputeBounds(grid);

            // min = origin + (3*100, 0, 5*100)、size = (100, 50, 100)
            Assert.AreEqual(new Vector3(-200f, 0f, 0f), bounds.min);
            Assert.AreEqual(new Vector3(-100f, 50f, 100f), bounds.max);
        }

        // ═══════════════════════════════════════════
        //  R-2: CellScene は UIView を検索・登録しない
        // ═══════════════════════════════════════════

        [UnityTest]
        public IEnumerator CellScene_Load_DoesNotRegisterUIView() => UniTask.ToCoroutine(async () =>
        {
            var (director, factory) = SetupCellAwareDirector("Cell_0_0");
            director.RootObjectsFactory = _ => new[] { CreateRootWithUIView("Cell_0_0_Root") };

            await director.AddScene("Cell_0_0", null, CancellationToken.None);

            Assert.AreEqual(SceneState.Stable, director.GetSceneState("Cell_0_0"));
            Assert.IsInstanceOf<CellScene>(factory.Created["Cell_0_0"]);
            Assert.IsNull(factory.Created["Cell_0_0"].UIView,
                "CellScene はルートに UIView が存在しても検索・保持してはならない（R-2）");
            Assert.IsNull(UICommon.GetUIView("Cell_0_0"),
                "CellScene のロードで UICommon に UIView が登録されてはならない（R-2）");
        });

        /// <summary>
        /// ハーネス健全性: 同じハーネスで通常の SceneBase をロードすると UIView が登録される。
        /// これにより上のテストが「そもそも UIView が見つからない」ことで空振りグリーンに
        /// なっていないことを保証する。
        /// </summary>
        [UnityTest]
        public IEnumerator SceneBase_Load_WithUIView_RegistersUIView_HarnessSanity()
            => UniTask.ToCoroutine(async () =>
        {
            var (director, factory) = SetupCellAwareDirector("Plain");
            director.RootObjectsFactory = _ => new[] { CreateRootWithUIView("Plain_Root") };

            await director.AddScene("Plain", null, CancellationToken.None);

            Assert.IsNotNull(factory.Created["Plain"].UIView,
                "通常の SceneBase はルートの UIView を検索・保持するはず（ハーネス健全性）");
            Assert.IsNotNull(UICommon.GetUIView("Plain"),
                "通常の SceneBase の UIView は UICommon に登録されるはず（ハーネス健全性）");
        });

        // ═══════════════════════════════════════════
        //  R-3/G-4: セル identity の画面遷移ガード
        // ═══════════════════════════════════════════

        [UnityTest]
        public IEnumerator SwitchScene_WithCellIdentity_ThrowsInvalidOperation()
            => UniTask.ToCoroutine(async () =>
        {
            var director = SetupTitleAndCell();

            // to 側にセル identity
            try
            {
                await director.SwitchScene(null, "Cell_0_0", CancellationToken.None);
                Assert.Fail("セル identity への SwitchScene は InvalidOperationException を投げるべき（R-3/G-4）");
            }
            catch (InvalidOperationException)
            {
            }

            Assert.IsFalse(director.ContainsScene("Cell_0_0"),
                "ガードはシーンを一切ロードせずに失敗すべき");

            // from 側にセル identity
            try
            {
                await director.SwitchScene("Cell_0_0", "Title", CancellationToken.None);
                Assert.Fail("セル identity からの SwitchScene は InvalidOperationException を投げるべき（R-3/G-4）");
            }
            catch (InvalidOperationException)
            {
            }

            Assert.IsFalse(director.ContainsScene("Title"),
                "ガードはシーンを一切ロードせずに失敗すべき");
        });

        // ═══════════════════════════════════════════
        //  Helpers / TestDoubles
        // ═══════════════════════════════════════════

        private CellScene CreateCellScene(string identity)
        {
            var resource = SceneTestHelper.CreateSceneResource(identity);
            CreatedSOs.Add(resource);
            return new CellScene(resource, new NullSceneQuery(), new NullSceneController());
        }

        private GameObject CreateRootWithUIView(string name)
        {
            var go = new GameObject(name);
            go.AddComponent<TestUIView>();
            _createdGameObjects.Add(go);
            return go;
        }

        /// <summary>単一 identity（セル or 通常）の CellAware ディレクタを構築する。</summary>
        private (RootObjectsSceneDirector Director, CellAwareSceneFactory Factory)
            SetupCellAwareDirector(string identity)
        {
            var resource = SceneTestHelper.CreateSceneResource(identity);
            CreatedSOs.Add(resource);
            Map = SceneTestHelper.CreateSceneResourceMap(resource);
            CreatedSOs.Add(Map);

            var factory = new CellAwareSceneFactory();
            var director = new RootObjectsSceneDirector(factory, UICommon, Map, AssetManagement);
            Director = director;
            return (director, factory);
        }

        /// <summary>Title（通常）+ Cell_0_0（セル）の 2 リソース構成。ガードテスト用。</summary>
        private TestableSceneDirector SetupTitleAndCell()
        {
            var titleRes = SceneTestHelper.CreateSceneResource("Title");
            var cellRes = SceneTestHelper.CreateSceneResource("Cell_0_0");
            CreatedSOs.Add(titleRes);
            CreatedSOs.Add(cellRes);
            Map = SceneTestHelper.CreateSceneResourceMap(titleRes, cellRes);
            CreatedSOs.Add(Map);

            Director = new TestableSceneDirector(Factory, UICommon, Map, AssetManagement);
            return Director;
        }

        /// <summary>テスト用の最小 UIView 実装（既定レイヤー Normal、アニメーションなし）。</summary>
        private sealed class TestUIView : UIView
        {
        }

        private sealed class NullSceneQuery : ISceneQuery
        {
            public SceneBase? GetLoadedScene(string identity) => null;
            public bool IsSceneLoaded(string identity) => false;
        }

        private sealed class NullSceneController : ISceneController
        {
            public UniTask AddScene(string sceneIdentify, Func<UniTask>? afterOnLoadedTask, CancellationToken ct, SceneContext? context = null, IProgress<SceneLoadProgress>? progress = null, LoadingDisplayType loadingDisplay = LoadingDisplayType.None, IReadOnlyDictionary<string, string>? telemetryTags = null, int priority = 100, TelemetryLevel telemetryLevel = TelemetryLevel.Summary)
            {
                return UniTask.CompletedTask;
            }

            public void ClearHistory()
            {
            }

            public UniTask GoBack(CancellationToken ct, SceneContext? context = null, LoadingDisplayType loadingDisplay = LoadingDisplayType.BlackScreen, IReadOnlyDictionary<string, string>? telemetryTags = null)
            {
                return UniTask.CompletedTask;
            }

            public UniTask SwitchScene(string? fromSceneIdentify, string toSceneIdentify, CancellationToken ct, SceneContext? context = null, LoadingDisplayType loadingDisplay = LoadingDisplayType.BlackScreen, IReadOnlyDictionary<string, string>? telemetryTags = null)
            {
                return UniTask.CompletedTask;
            }

            public UniTask UnloadScene(string sceneIdentify, LoadingDisplayType loadingDisplay = LoadingDisplayType.None, IReadOnlyDictionary<string, string>? telemetryTags = null, TelemetryLevel telemetryLevel = TelemetryLevel.Summary)
            {
                return UniTask.CompletedTask;
            }
        }

        /// <summary>
        /// identity のプレフィックスで CellScene / SceneBase を出し分けるファクトリ。
        /// 生成インスタンスを保持し、テストから UIView の有無を観測できるようにする。
        /// </summary>
        private sealed class CellAwareSceneFactory : ISceneFactory
        {
            public Dictionary<string, SceneBase> Created { get; } = new();

            public SceneBase? CreateSceneClass(SceneResource sceneResource, ISceneQuery sceneQuery, ISceneController sceneController)
            {
                SceneBase scene = sceneResource.Identity.StartsWith(CellIdentity.Prefix, StringComparison.Ordinal)
                    ? new CellScene(sceneResource, sceneQuery, sceneController)
                    : new SceneBase(sceneResource, sceneQuery, sceneController);
                Created[sceneResource.Identity] = scene;
                return scene;
            }
        }

        /// <summary>
        /// PerformUnitySceneLoad が任意の RootObjects を返せる TestableSceneDirector。
        /// UIView 検索（SceneBase.Initialize）の入力を作るために使う。
        /// </summary>
        private sealed class RootObjectsSceneDirector : TestableSceneDirector
        {
            public Func<string, GameObject[]>? RootObjectsFactory { get; set; }

            public RootObjectsSceneDirector(
                ISceneFactory sceneFactory,
                UICommon uiCommon,
                SceneResourceMap sceneResourceMap,
                IAssetManagement assetManagement)
                : base(sceneFactory, uiCommon, sceneResourceMap, assetManagement)
            {
            }

            protected override async UniTask<(bool AddressablesLoaded, GameObject[] RootObjects)>
                PerformUnitySceneLoad(string sceneIdentify, SceneResource sceneResource, int priority)
            {
                await base.PerformUnitySceneLoad(sceneIdentify, sceneResource, priority);
                var roots = RootObjectsFactory?.Invoke(sceneIdentify) ?? Array.Empty<GameObject>();
                return (false, roots);
            }
        }
    }
}
