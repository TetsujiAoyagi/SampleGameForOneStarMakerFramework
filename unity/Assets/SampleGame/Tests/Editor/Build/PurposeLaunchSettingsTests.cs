#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OneStarMaker.Runtime.BuildContent.Distribution;
using SampleGame.DependOnAll.Editor.Build;
using UnityEditor;

namespace SampleGame.Tests.Editor.Build
{
    /// <summary>宣言から Play の config を作る変換、宣言ごとの menu と初回 Scene、競合の検査の pure な部分。Play には入らない。</summary>
    public sealed class PurposeLaunchSettingsTests
    {
        // ═══ AC1: Play As の config ═══

        [Test]
        public void PlayAs_EveryDeclaration_PlacesAddressablesVariantAndRuntimeDefaults()
        {
            foreach (var declaration in SampleGameBuildPurposes.All)
            {
                var settings = PurposeLaunchSettings.PlayAs(declaration);
                var placed = settings.Placed.ToDictionary(x => x.Key, x => x.Value);
                Assert.That(placed["content:runtimeMode"], Is.EqualTo("addressables"), declaration.Name);
                foreach (var pair in declaration.RuntimeDefaults)
                    Assert.That(placed[pair.Key], Is.EqualTo(pair.Value), declaration.Name);
                Assert.That(settings.Controlled, Does.Contain("assets:sceneVariant"), declaration.Name);
                if (declaration.Representation.Primary == "Full")
                {
                    // 空文字の環境変数は key の削除と同じなので置かず、下位の層の空で実効値を空にする。
                    Assert.That(placed.ContainsKey("assets:sceneVariant"), Is.False, declaration.Name);
                    Assert.That(settings.RequiresEmptyLowerLayer, Is.EqualTo(new[] { "assets:sceneVariant" }), declaration.Name);
                }
                else
                {
                    Assert.That(placed["assets:sceneVariant"], Is.EqualTo("Whitebox"), declaration.Name);
                    Assert.That(settings.RequiresEmptyLowerLayer, Is.Empty, declaration.Name);
                }
            }
        }

        [Test]
        public void PlayAs_Planner_UsesPrefixedEnvironmentNames()
        {
            var environment = PurposeLaunchSettings.PlayAs(SampleGameBuildPurposes.SpringPlannerWhitebox).PlacedEnvironment
                .ToDictionary(x => x.Key, x => x.Value);
            Assert.That(environment, Is.EquivalentTo(new Dictionary<string, string>
            {
                ["SAMPLEGAME_CONTENT__RUNTIMEMODE"] = "addressables",
                ["SAMPLEGAME_ASSETS__SCENEVARIANT"] = "Whitebox",
                ["SAMPLEGAME_WORLD__CELLCOMPANIONSET"] = "Planner",
            }));
        }

        [TestCase("Full", ExpectedResult = "")]
        [TestCase("Whitebox", ExpectedResult = "Whitebox")]
        public string SceneVariant_MapsFullToEmpty(string primary) => PurposeLaunchSettings.SceneVariantOf(primary);

        [Test]
        public void SceneVariant_OtherRepresentations_AreRejected() =>
            Assert.Throws<ArgumentException>(() => PurposeLaunchSettings.SceneVariantOf("Lit"));

        // ═══ AC7・AC9: directory の検証 Play の key 集合 ═══

        [Test]
        public void DirectoryPlay_PlacesContentPairRepresentationDefaultsAndFirstScene()
        {
            var values = PurposeLaunchSettings.DirectoryPlay("C:/cache/installed/set/rev", "abc", "rev", "Full",
                    new[] { new KeyValuePair<string, string>("world:cellCompanionSet", "Planner") }, "Title")
                .ToDictionary(x => x.Key, x => x.Value);
            Assert.That(values, Is.EquivalentTo(new Dictionary<string, string>
            {
                ["SAMPLEGAME_CONTENT__RUNTIMEMODE"] = "directory",
                ["SAMPLEGAME_CONTENT__INSTALLEDREVISIONPATH"] = "C:/cache/installed/set/rev",
                ["SAMPLEGAME_CONTENT__MANIFESTSHA256"] = "abc",
                ["SAMPLEGAME_CONTENT__BUILDIDENTITY"] = "rev",
                // directory の representation は主値の文字列のまま（Full を空にしない）。
                ["SAMPLEGAME_CONTENT__REPRESENTATION"] = "Full",
                ["SAMPLEGAME_CONTENT__FIRSTSCENE"] = "Title",
                ["SAMPLEGAME_WORLD__CELLCOMPANIONSET"] = "Planner",
            }));
        }

        [Test]
        public void FirstScene_SyntheticValue_ReachesBothVerifyAndReceiveBridges()
        {
            // production の宣言の一覧には合成の宣言を登録せず、初回 Scene の決定だけを差し替える。content build はしない。
            Func<string, string> firstSceneOf = _ => "OutGameScene";
            var settings = new ContentDeliveryWindow.Settings();
            var published = new PublishedContent("C:/published", "abc", "rev", "spring-full", "spring-full", "Full",
                new[] { new KeyValuePair<string, string>("world:cellCompanionSet", "Full") });
            var installed = new InstalledContent("C:/cache/installed/spring-full/rev", "abc", "rev");
            var verify = new VerifyPlayLauncher.PurposeStages(SampleGameBuildPurposes.SpringFull, false, firstSceneOf, settings);
            var receive = new VerifyPlayLauncher.ReceivedStages("C:/last-result.json", () => null, false, firstSceneOf, settings);
            foreach (var stages in new VerifyPlayLauncher.DirectoryPlayStages[] { verify, receive })
                Assert.That(stages.BridgeValues(published, installed).Single(x => x.Key == "SAMPLEGAME_CONTENT__FIRSTSCENE").Value,
                    Is.EqualTo("OutGameScene"), stages.GetType().Name);
        }

        [Test]
        public void FirstScene_IsDefinedForEveryDeclaration_AndUnknownIsRejected()
        {
            foreach (var declaration in SampleGameBuildPurposes.All)
                Assert.That(SampleGameBuildPurposes.FirstSceneOf(declaration.Name), Is.EqualTo("Title"), declaration.Name);
            Assert.Throws<ArgumentException>(() => SampleGameBuildPurposes.FirstSceneOf("unknown-purpose"));
        }

        // ═══ AC19: 宣言ごとの menu ═══

        [TestCase(typeof(PlayAsLauncher), PlayAsLauncher.MenuRoot)]
        [TestCase(typeof(VerifyPlayLauncher), VerifyPlayLauncher.MenuRoot)]
        public void Menus_ExistOncePerDeclaration(Type launcher, string root)
        {
            var paths = launcher.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                .SelectMany(m => m.GetCustomAttributes<MenuItem>())
                .Select(a => a.menuItem)
                .Where(p => p.StartsWith(root, StringComparison.Ordinal))
                .Select(p => p.Substring(root.Length))
                .OrderBy(x => x, StringComparer.Ordinal).ToArray();
            Assert.That(paths, Is.EqualTo(SampleGameBuildPurposes.All.Select(d => d.Name).ToArray()),
                "宣言を足したら " + launcher.Name + " の menu も足す");
        }

        // ═══ AC2: コマンドラインと下位の層 ═══

        [Test]
        public void CommandLine_ControlledKeys_AreFoundCaseInsensitively()
        {
            var args = new[] { "Unity.exe", "-projectPath", "D:/p", "--content.runtimeMode=addressables", "--World.CellCompanionSet", "Full" };
            Assert.That(PlayAsLauncher.FindCommandLineConflicts(args, new[] { "content:runtimeMode", "world:cellCompanionSet", "assets:sceneVariant" }),
                Is.EqualTo(new[] { "content:runtimeMode", "world:cellCompanionSet" }));
            Assert.That(PlayAsLauncher.FindCommandLineConflicts(new[] { "Unity.exe", "-projectPath", "D:/p" }, new[] { "content:runtimeMode" }),
                Is.Empty);
        }

        [Test]
        public void CommandLine_DirectoryPlayKeys_IncludeFirstSceneAndDefaults()
        {
            var keys = PurposeLaunchSettings.DirectoryPlayConfigKeys(SampleGameBuildPurposes.SpringFull.RuntimeDefaults);
            Assert.That(PlayAsLauncher.FindCommandLineConflicts(new[] { "--content.runtimeMode=addressables" }, keys),
                Is.EqualTo(new[] { "content:runtimeMode" }));
            Assert.That(keys, Does.Contain("content:firstScene").And.Contain("world:cellCompanionSet"));
        }

        [Test]
        public void LowerLayer_NonEmptySceneVariant_IsRejected()
        {
            var keys = new[] { "assets:sceneVariant" };
            Assert.That(PlayAsLauncher.CheckEmptyLowerLayer(keys, "{ \"assets:sceneVariant\" : \"\", \"world:cellCompanionSet\" : \"Full\" }"), Is.Null);
            Assert.That(PlayAsLauncher.CheckEmptyLowerLayer(keys, "{ \"assets\" : { \"sceneVariant\" : \"Whitebox\" } }"), Does.Contain("assets:sceneVariant"));
            Assert.That(PlayAsLauncher.CheckEmptyLowerLayer(Array.Empty<string>(), "{ \"assets:sceneVariant\" : \"Whitebox\" }"), Is.Null);
        }

        [Test]
        public void LowerLayer_ProjectAppConfig_KeepsSceneVariantEmpty()
        {
            // 今の app-config の値。Full の Play As はこの空に依る。
            var json = System.IO.File.ReadAllText(PurposeLaunchSettings.AppConfigPath);
            Assert.That(PlayAsLauncher.CheckEmptyLowerLayer(new[] { "assets:sceneVariant" }, json), Is.Null);
        }

        // ═══ AC5: 起動が再利用しうる Scene ═══

        [Test]
        public void ReusableScenes_MapNamesUiCommon_AreRejected_OthersPass()
        {
            var map = new HashSet<string>(StringComparer.Ordinal) { "Spring_Cell_0_4", "Summer_Cell_0_0", "Title" };
            Assert.That(VerifyPlayLauncher.FindReusableScenes(new[]
            {
                ("Spring_Cell_0_4", "Assets/x/Variants/Whitebox/Spring_Cell_0_4.unity"),
                ("Summer_Cell_0_0", "Assets/x/Summer_Cell_0_0.unity"),
                ("UIScene", "Assets/OneStarMaker/Scenes/UISystem/UIScene.unity"),
                ("UICommon", "Assets/y/UICommon.unity"),
            }, map), Has.Count.EqualTo(4));
            Assert.That(VerifyPlayLauncher.FindReusableScenes(new[] { ("", "") }, map), Is.Empty, "untitled の空 Scene");
            Assert.That(VerifyPlayLauncher.FindReusableScenes(new[] { ("TestOnly", "Assets/Tests/TestOnly.unity") }, map), Is.Empty);
        }

        [Test]
        public void ReusableScenes_RealMap_CoversScenesOutsideTheSelection()
        {
            // directory に入る map そのもの。宣言の選択では絞られないので、spring の宣言でも Summer の Scene は再利用されうる。
            var map = AssetDatabase.LoadAssetAtPath<OneStarMaker.Runtime.SceneSystem.SceneResourceMap>(
                "Assets/OneStarMakerCommon/SceneMap/SceneResourceMap.asset");
            Assert.That(map != null, Is.True);
            var names = new HashSet<string>(map!.SceneResources.Where(x => x != null).Select(x => x.Identity), StringComparer.Ordinal);
            Assert.That(VerifyPlayLauncher.FindReusableScenes(new[]
            {
                ("Spring_Cell_0_4", "Assets/any/Spring_Cell_0_4.unity"),
                ("Summer_Cell_0_0", "Assets/any/Summer_Cell_0_0.unity"),
            }, names), Has.Count.EqualTo(2));
        }

        // ═══ AC7・AC15: manifest の宣言の照合 ═══

        [Test]
        public void Declaration_RecordedFieldsMustMatchLocal()
        {
            var local = SampleGameBuildPurposes.SpringPlannerWhitebox;
            ContentManifestDeclaration Manifest(string name, string representation, string companion) => new()
            {
                name = name, representation = representation,
                runtimeDefaults = new[] { new ContentManifestDeclarationValue { key = "world:cellCompanionSet", value = companion } },
            };
            Assert.That(VerifyPlayLauncher.CompareDeclaration(Manifest("spring-planner-whitebox", "Whitebox", "Planner"), local), Is.Null);
            Assert.That(VerifyPlayLauncher.CompareDeclaration(Manifest("spring-whitebox", "Whitebox", "Planner"), local), Is.Not.Null);
            Assert.That(VerifyPlayLauncher.CompareDeclaration(Manifest("spring-planner-whitebox", "Full", "Planner"), local), Is.Not.Null);
            Assert.That(VerifyPlayLauncher.CompareDeclaration(Manifest("spring-planner-whitebox", "Whitebox", "Full"), local), Is.Not.Null);
        }

        // ═══ AC20: cache root ═══

        [Test]
        public void CacheRoot_EmptyResolvesOutsideRepository()
        {
            var resolved = VerifyPlayLauncher.ResolveCacheRoot("");
            Assert.That(System.IO.Path.IsPathRooted(resolved), Is.True);
            Assert.That(resolved, Does.EndWith(System.IO.Path.Combine("OSM", "ContentCache")));
            Assert.That(VerifyPlayLauncher.ResolveCacheRoot("D:/cache"), Is.EqualTo("D:/cache"));
        }
    }
}
