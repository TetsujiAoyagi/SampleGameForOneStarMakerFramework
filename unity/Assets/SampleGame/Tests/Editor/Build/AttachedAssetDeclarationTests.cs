#nullable enable

using System.Linq;
using NUnit.Framework;
using OneStarMaker.Runtime.SceneSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SampleGame.Tests.Editor.Build
{
    /// <summary>
    /// 付随 asset の authoring（AC12）。3 つの SceneResource が付随 asset を Variant 空で宣言し、
    /// Scene の trigger / anchor は address ではなくその名前を持つ。Scene は開いて読む（Play はしない）。
    /// </summary>
    public sealed class AttachedAssetDeclarationTests
    {
        private const string SeasonsRoot = "Assets/SampleGame/InGame/InGameSession/Seasons/Spring";
        private const string EventsStem = SeasonsRoot + "/Cells/Spring_Cell_4_2/Spring_Events_4_2/Spring_Events_4_2";
        private const string EventStem = SeasonsRoot + "/Events/Spring_Event_MultiCellProof/Spring_Event_MultiCellProof";
        private const string SessionResourcePath = "Assets/OneStarMakerCommon/SceneMap/InGameSession.asset";
        private const string EventsVfxPath = EventsStem + "_Burst.vfx";
        private const string EventVfxPath = EventStem + "_Burst.vfx";
        private const string LitPath = "Assets/SampleGame/InGame/InGameSession/Seasons/Materials/DemoCellLit.mat";

        [TestCase(EventsStem + ".asset", "Burst", EventsVfxPath)]
        [TestCase(EventStem + ".asset", "Burst", EventVfxPath)]
        [TestCase(SessionResourcePath, "Lit", LitPath)]
        public void SceneResource_DeclaresAttachedAssetWithEmptyVariant(string resourcePath, string name, string assetPath)
        {
            var resource = AssetDatabase.LoadAssetAtPath<SceneResource>(resourcePath);
            Assert.That(resource != null, Is.True, "SceneResource が無い: " + resourcePath);
            var attached = resource!.FindAttachedAsset(name);
            Assert.That(attached, Is.Not.Null, $"{resourcePath} が付随 asset '{name}' を宣言していない");
            Assert.That(resource.AttachedAssets.Count, Is.EqualTo(1), "宣言は 1 件");
            Assert.That(attached!.Payloads.Count, Is.EqualTo(1));
            Assert.That(attached.Payloads[0].Variant, Is.Empty, "Variant は空（Full）");
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            Assert.That(guid, Is.Not.Empty, "asset が無い: " + assetPath);
            Assert.That(attached.Payloads[0].Reference?.AssetGUID, Is.EqualTo(guid));
        }

        [Test]
        public void EventsScene_TriggerNamesTheDeclaredAttachedAsset()
        {
            var components = ReadVfxAssetNames(EventsStem + ".unity", "LocalVfxEventTrigger");
            Assert.That(components, Has.Length.EqualTo(1));
            Assert.That(components[0], Is.EqualTo("Burst"));
        }

        [Test]
        public void MajorEventScene_AnchorsNameTheDeclaredAttachedAsset()
        {
            var components = ReadVfxAssetNames(EventStem + ".unity", "MajorEventVfxAnchor");
            Assert.That(components, Has.Length.EqualTo(2));
            Assert.That(components.All(name => name == "Burst"), Is.True);
        }

        [Test]
        public void SeasonScene_ReadsLitFromInGameSessionDeclaration()
        {
            // SeasonScene が読む所有者と名前は定数。宣言側（InGameSession:Lit）と一致することを source で見る。
            var source = System.IO.File.ReadAllText("Assets/SampleGame/InGame/InGameSession/World/SeasonScene.cs");
            Assert.That(source, Does.Contain("SharedLitOwner = \"InGameSession\""));
            Assert.That(source, Does.Contain("SharedLitName = \"Lit\""));
            Assert.That(source, Does.Not.Contain("DemoCellLit.mat"));
        }

        // この asmdef は SampleGame.InGame を参照しないので、component は型名で選び、値は serialized property から読む。
        private static string[] ReadVfxAssetNames(string scenePath, string componentTypeName)
        {
            var previous = EditorSceneManager.GetSceneManagerSetup();
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                return scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Component>(true))
                    .Where(component => component != null && component.GetType().Name == componentTypeName)
                    .Select(component => new SerializedObject(component).FindProperty("_vfxAsset")?.stringValue ?? string.Empty)
                    .ToArray();
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.Length != 0 && previous.Any(setup => setup.isLoaded)) EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }
    }
}
