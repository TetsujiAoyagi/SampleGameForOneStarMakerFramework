#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SampleGame.Common.Rendering.Vfx;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

namespace SampleGame.Tests.Rendering.Vfx
{
    /// <summary>
    /// Scene に配置した VFX の authoring の約束を検査する（Play には入らない）。
    /// <para>
    /// 約束: 全ての <see cref="VisualEffect"/> は無効で保存し、同じ GameObject に <see cref="PlacedVfxActivator"/> を持ち、
    /// asset の参照が外れていない。有効のまま保存すると、非対応環境でも deserialize の時点で有効化が走り、
    /// 時間の領域が止まっていても開始してしまう（有効化は VfxSystem に登録した後の反映点で行う）。
    /// VFX 専用の Scene には Collider とゲーム進行・カメラ追従の script を置かない。
    /// </para>
    /// <para>
    /// 全ての <c>.vfx</c> は Update Mode を Delta Time + Ignore Time Scale にする。VFX Graph が <c>Time.timeScale</c> を
    /// 内部で効かせると Framework が書く再生速度と二重に効き、Fixed Delta Time では 1/60 の刻みになってフレームごとに
    /// gameplay の時間と一致しない。
    /// </para>
    /// <para>
    /// 対象は、必須の 2 Scene と、Seasons 配下で依存に .vfx を含む Scene の和集合。必須側を別に持つのは、
    /// .vfx の参照が外れた Scene が依存の走査から落ちて検査を素通りするのを防ぐため。描画テスト用の fixture は含めない。
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class PlacedVfxAuthoringTests
    {
        private const string SeasonsRoot = "Assets/SampleGame/InGame/InGameSession/Seasons";
        private const string AtmospherePath = SeasonsRoot + "/Spring/Spring_AtmosphereVFX/Spring_AtmosphereVFX.unity";
        private const string CellVfxPath = SeasonsRoot + "/Spring/Cells/Spring_Cell_4_2/Spring_VFX_4_2/Spring_VFX_4_2.unity";

        private const string ShortLivedFixturePath = "Assets/SampleGame/Tests/Rendering/Vfx/Fixtures/VfxShortLived.vfx";

        private static readonly string[] RequiredScenePaths = { AtmospherePath, CellVfxPath };

        private SceneSetup[]? _setup;

        [SetUp]
        public void SetUp()
        {
            _setup = EditorSceneManager.GetSceneManagerSetup();
        }

        [TearDown]
        public void TearDown()
        {
            if (_setup != null && _setup.Any(scene => scene.isLoaded && scene.isActive))
                EditorSceneManager.RestoreSceneManagerSetup(_setup);
            else
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void RequiredVfxScenes_EachHavePlacedVfxThatFollowsTheContract()
        {
            foreach (var path in RequiredScenePaths)
            {
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null, Is.True, $"必須の VFX Scene が無い: {path}");
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var effects = AllVisualEffects(scene);
                Assert.That(effects, Is.Not.Empty, $"必須の VFX Scene に VisualEffect が無い: {path}");
                Assert.That(Inspect(scene, vfxOnlyScene: true), Is.Empty, $"authoring の違反: {path}");
            }
        }

        [Test]
        public void ScenesThatDependOnVfx_FollowTheContract()
        {
            var targets = TargetScenePaths();
            TestContext.Out.WriteLine("targets=" + string.Join(",", targets));
            Assert.That(targets, Is.SupersetOf(RequiredScenePaths));
            var violations = new List<string>();
            foreach (var path in targets)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                violations.AddRange(Inspect(scene, vfxOnlyScene: RequiredScenePaths.Contains(path)).Select(v => path + ": " + v));
            }

            Assert.That(violations, Is.Empty);
        }

        // Delta Time（1）と Ignore Time Scale（2）の組。.vfx の VisualEffectResource に m_UpdateMode として保存される。
        private const int DeltaTimeIgnoringTimeScale = 3;

        private const string VfxSearchRoot = "Assets/SampleGame";

        [Test]
        public void AllVfxAssets_UseDeltaTimeIgnoringTimeScale()
        {
            var paths = AssetDatabase.FindAssets("t:VisualEffectAsset", new[] { VfxSearchRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".vfx", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
            TestContext.Out.WriteLine("vfx=" + string.Join(",", paths));
            Assert.That(paths, Is.Not.Empty, "前提: .vfx がある");

            var violations = paths
                .Select(path => (path, violation: InspectUpdateMode(File.ReadAllText(path))))
                .Where(entry => entry.violation != null)
                .Select(entry => entry.path + ": " + entry.violation)
                .ToList();
            Assert.That(violations, Is.Empty);
        }

        [TestCase("    m_UpdateMode: 3\n", null)]
        [TestCase("    m_UpdateMode: 0\n", "Update Mode が 0")]
        [TestCase("    m_UpdateMode: 2\n", "Update Mode が 2")]
        [TestCase("    m_Other: 1\n", "m_UpdateMode が無い")]
        [TestCase("    m_UpdateMode: 3\n    m_UpdateMode: 3\n", "m_UpdateMode が 2 件")]
        public void InspectUpdateMode_ReportsViolations(string text, string? expected)
        {
            var violation = InspectUpdateMode(text);
            if (expected == null)
                Assert.That(violation, Is.Null);
            else
                Assert.That(violation, Does.Contain(expected));
        }

        // ─── 失敗経路: 検査関数が各違反を返すこと ───

        [Test]
        public void Inspect_EnabledVisualEffect_IsViolation()
        {
            var scene = NewTemporaryScene();
            var effect = AddPlaced(scene, "Enabled");
            effect.enabled = true;

            Assert.That(Inspect(scene, vfxOnlyScene: true), Has.Some.Contains("有効のまま"));
        }

        [Test]
        public void Inspect_VisualEffectWithoutActivator_IsViolation()
        {
            var scene = NewTemporaryScene();
            var effect = AddPlaced(scene, "NoActivator");
            Object.DestroyImmediate(effect.GetComponent<PlacedVfxActivator>());

            Assert.That(Inspect(scene, vfxOnlyScene: true), Has.Some.Contains("PlacedVfxActivator"));
        }

        [Test]
        public void Inspect_EnabledVisualEffectOnInactiveObject_IsViolation()
        {
            var scene = NewTemporaryScene();
            var effect = AddPlaced(scene, "InactiveEnabled");
            effect.enabled = true;
            effect.gameObject.SetActive(false);

            Assert.That(Inspect(scene, vfxOnlyScene: true), Has.Some.Contains("有効のまま"));
        }

        [Test]
        public void Inspect_MissingAssetAndColliderInVfxScene_AreViolations()
        {
            var scene = NewTemporaryScene();
            var effect = AddPlaced(scene, "MissingAsset");
            effect.visualEffectAsset = null;
            effect.gameObject.AddComponent<BoxCollider>();

            var violations = Inspect(scene, vfxOnlyScene: true);
            Assert.That(violations, Has.Some.Contains("visualEffectAsset"));
            Assert.That(violations, Has.Some.Contains("Collider"));
        }

        [Test]
        public void Inspect_ConformingPlacedVfx_HasNoViolation()
        {
            var scene = NewTemporaryScene();
            AddPlaced(scene, "Conforming");

            Assert.That(Inspect(scene, vfxOnlyScene: true), Is.Empty);
        }

        // ─── 検査 ───

        /// <summary>
        /// Scene の全ての VisualEffect（非 active を含む）の約束を検査し、違反を文で返す。
        /// <paramref name="vfxOnlyScene"/> のときは、VFX 専用の Scene に置かないもの（Collider・他の script）も見る。
        /// </summary>
        internal static List<string> Inspect(Scene scene, bool vfxOnlyScene)
        {
            var violations = new List<string>();
            foreach (var effect in AllVisualEffects(scene))
            {
                var name = HierarchyPath(effect.transform);
                if (effect.enabled)
                    violations.Add($"{name}: VisualEffect が有効のまま保存されている");
                if (effect.GetComponent<PlacedVfxActivator>() == null)
                    violations.Add($"{name}: 同じ GameObject に PlacedVfxActivator が無い");
                if (effect.visualEffectAsset == null)
                    violations.Add($"{name}: visualEffectAsset が null");
            }

            if (!vfxOnlyScene)
                return violations;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var collider in root.GetComponentsInChildren<Collider>(includeInactive: true))
                    violations.Add($"{HierarchyPath(collider.transform)}: VFX Scene に Collider がある");
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(includeInactive: true))
                {
                    // 配置済み VFX の marker 以外の script（ゲーム進行・カメラ追従等）は VFX Scene に置かない。
                    if (behaviour == null)
                        violations.Add($"{root.name}: 参照の外れた script がある");
                    else if (behaviour is not PlacedVfxActivator)
                        violations.Add($"{HierarchyPath(behaviour.transform)}: VFX Scene に script {behaviour.GetType().Name} がある");
                }
            }

            return violations;
        }

        /// <summary>
        /// .vfx の本文から Update Mode を読み、約束に従わなければ違反を文で返す（従えば null）。
        /// 値は読むだけで、.vfx を書き換えない（Update Mode の変更は Editor 操作で行う）。
        /// </summary>
        internal static string? InspectUpdateMode(string vfxText)
        {
            var matches = Regex.Matches(vfxText, @"^\s*m_UpdateMode:\s*(\d+)\s*$", RegexOptions.Multiline);
            if (matches.Count == 0)
                return "m_UpdateMode が無い";
            if (matches.Count > 1)
                return $"m_UpdateMode が {matches.Count} 件ある";

            var mode = int.Parse(matches[0].Groups[1].Value);
            return mode == DeltaTimeIgnoringTimeScale
                ? null
                : $"Update Mode が {mode}（Delta Time + Ignore Time Scale = {DeltaTimeIgnoringTimeScale} にする）";
        }

        private static List<string> TargetScenePaths()
        {
            var result = new SortedSet<string>(RequiredScenePaths, StringComparer.Ordinal);
            foreach (var guid in AssetDatabase.FindAssets("t:SceneAsset", new[] { SeasonsRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.GetDependencies(path, recursive: true)
                    .Any(dependency => dependency.EndsWith(".vfx", StringComparison.OrdinalIgnoreCase)))
                    result.Add(path);
            }

            return result.ToList();
        }

        private static VisualEffect[] AllVisualEffects(Scene scene)
            => scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<VisualEffect>(includeInactive: true))
                .ToArray();

        private static string HierarchyPath(Transform transform)
            => transform.parent == null ? transform.name : HierarchyPath(transform.parent) + "/" + transform.name;

        private static Scene NewTemporaryScene()
            => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        private static VisualEffect AddPlaced(Scene scene, string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(ShortLivedFixturePath);
            Assert.That(asset != null, Is.True, "fixture の .vfx が無い");
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            var effect = go.AddComponent<VisualEffect>();
            effect.enabled = false;
            effect.visualEffectAsset = asset;
            go.AddComponent<PlacedVfxActivator>();
            return effect;
        }
    }
}
