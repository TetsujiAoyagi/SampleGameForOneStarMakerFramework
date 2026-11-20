#nullable enable

using System;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Editor.PlaySnapshot;
using OneStarMaker.Runtime;
using UnityEditor;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.PlaySnapshot
{
    /// <summary>
    /// 起動の計画（取り込みより前に決める純粋な判定）と、Editor の session が起動時の要求の印を SessionState から bridge へ運ぶこと（AC4）。
    /// </summary>
    public sealed class EditorPlaySnapshotStartupPlanTests
    {
        private const string TitleCopy = "Assets/OsmPlaySnapshot/1_2_ab/Title.unity";
        private const string ExtraCopy = "Assets/OsmPlaySnapshot/1_2_ab/Extra.unity";
        private const string FieldCopy = "Assets/OsmPlaySnapshot/1_2_ab/Field.unity";

        private static readonly EditorPlaySnapshotScene[] Declaration =
        {
            Scene("Title", TitleCopy, request: true),
            Scene("Extra", ExtraCopy, request: false),
            Scene("Field", FieldCopy, request: true),
        };

        private static bool Mapped(string name) => name != "Unmapped";

        [Test]
        public void NoKey_KeepsTheLoadedSceneRegistration_EvenWithALeftoverDeclaration()
        {
            var plan = EditorPlaySnapshotIntake.PlanStartup(false, true, true, Declaration, Mapped);
            Assert.That(plan.Failure, Is.Null);
            Assert.That(plan.RequestsSnapshotScenes, Is.False);
            Assert.That(plan, Is.SameAs(EditorPlaySnapshotIntake.PlanStartup(false, false, false, null, Mapped)));
        }

        [Test]
        public void SnapshotPlay_RequestsOnlyMarkedScenes_InDeclarationOrder()
        {
            var plan = EditorPlaySnapshotIntake.PlanStartup(true, true, true, Declaration, Mapped);
            Assert.That(plan.Failure, Is.Null);
            Assert.That(plan.RequestsSnapshotScenes, Is.True);
            Assert.That(plan.Requested.Select(x => x.Identity), Is.EqualTo(new[] { "Title", "Field" }));
        }

        [Test]
        public void SnapshotPlay_WithNoMarkedScene_RequestsNothing()
        {
            var declaration = new[] { Scene("Title", TitleCopy, request: false) };
            var plan = EditorPlaySnapshotIntake.PlanStartup(true, true, true, declaration, Mapped);
            Assert.That(plan.Failure, Is.Null);
            Assert.That(plan.RequestsSnapshotScenes, Is.True);
            Assert.That(plan.Requested, Is.Empty);
        }

        [Test]
        public void KeyWithoutDirectoryMode_Fails()
        {
            var plan = EditorPlaySnapshotIntake.PlanStartup(true, false, true, Declaration, Mapped);
            Assert.That(plan.Failure, Does.Contain("content directory mode"));
            Assert.That(plan.RequestsSnapshotScenes, Is.False);
        }

        [Test]
        public void KeyWithoutTheSnapshotProvider_Fails()
        {
            var plan = EditorPlaySnapshotIntake.PlanStartup(true, true, false, Declaration, Mapped);
            Assert.That(plan.Failure, Does.Contain("verified installed revision"));
        }

        [Test]
        public void KeyWithoutDeclaration_Fails()
        {
            var plan = EditorPlaySnapshotIntake.PlanStartup(true, true, true, null, Mapped);
            Assert.That(plan.Failure, Does.Contain("not available"));
        }

        [Test]
        public void MarkedSceneMissingFromTheMap_Fails()
        {
            var declaration = new[] { Scene("Title", TitleCopy, request: true), Scene("Unmapped", ExtraCopy, request: true) };
            var plan = EditorPlaySnapshotIntake.PlanStartup(true, true, true, declaration, Mapped);
            Assert.That(plan.Failure, Does.Contain("Unmapped"));
            Assert.That(plan.Requested, Is.Empty);
        }

        [Test]
        public void UnmarkedSceneMissingFromTheMap_IsNotRequested_AndDoesNotFail()
        {
            var declaration = new[] { Scene("Title", TitleCopy, request: true), Scene("Unmapped", ExtraCopy, request: false) };
            var plan = EditorPlaySnapshotIntake.PlanStartup(true, true, true, declaration, Mapped);
            Assert.That(plan.Failure, Is.Null);
            Assert.That(plan.Requested.Select(x => x.Identity), Is.EqualTo(new[] { "Title" }));
        }

        [Test]
        public void IntakeLog_CarriesTheRequestMark()
        {
            Assert.That(EditorPlaySnapshotIntake.FormatIntakeLog(Declaration[1]),
                Is.EqualTo(EditorPlaySnapshotIntake.LogPrefix + "scene=Extra copy=" + ExtraCopy + " active=false requested=false"));
            Assert.That(EditorPlaySnapshotIntake.FormatRequestLog("Title", true),
                Is.EqualTo("[AppInit] Editor play snapshot request: scene=Title stable=true"));
        }

        [Test]
        public void DeclaredScenes_CarryTheRecordFacts()
        {
            var declared = EditorPlaySnapshotIntake.ToDeclaredScenes(new[]
            {
                new EditorPlaySnapshotScene("Title", TitleCopy, true, true, "Assets/SampleGame/Scenes/Title.unity", true, "abc"),
            }).Single();
            Assert.That(declared.Identity, Is.EqualTo("Title"));
            Assert.That(declared.CopyPath, Is.EqualTo(TitleCopy));
            Assert.That(declared.SourcePath, Is.EqualTo("Assets/SampleGame/Scenes/Title.unity"));
            Assert.That(declared.WasDirty, Is.True);
            Assert.That(declared.Sha256, Is.EqualTo("abc"));
        }

        [Test]
        public void Session_ReinstallsTheBridge_WithTheSavedRequestMarks()
        {
            var saved = SessionState.GetString(PlaySnapshotSession.SessionKey, string.Empty);
            var savedDeclaration = EditorPlaySnapshotBridge.Declaration;
            var savedLoad = EditorPlaySnapshotBridge.LoadScene;
            try
            {
                var entries = new[]
                {
                    Entry("Title", TitleCopy, request: true),
                    Entry("Extra", ExtraCopy, request: false),
                };
                // Document の形（folderAssetPath・startScenePath・scenes）の JSON を置く。Document は session の private な型。
                var json = "{\"folderAssetPath\":\"\",\"startScenePath\":\"\",\"scenes\":[" +
                           string.Join(",", entries.Select(x => JsonUtility.ToJson(x))) + "]}";
                SessionState.SetString(PlaySnapshotSession.SessionKey, json);
                EditorPlaySnapshotBridge.Clear();

                Assert.That(PlaySnapshotSession.InstallBridgeFromSession(), Is.True);

                var bridged = EditorPlaySnapshotBridge.Declaration!;
                Assert.That(bridged.Select(x => x.Identity), Is.EqualTo(new[] { "Title", "Extra" }));
                Assert.That(bridged.Select(x => x.RequestAtStartup), Is.EqualTo(new[] { true, false }), "置き直した印は保存した値と等しい");
                Assert.That(bridged[0].SourcePath, Is.EqualTo("Assets/SampleGame/Scenes/Title.unity"));
                Assert.That(bridged[0].Sha256, Is.EqualTo("sha-Title"));
                Assert.That(PlaySnapshotSession.Declaration.Select(x => x.RequestAtStartup), Is.EqualTo(new[] { true, false }));
            }
            finally
            {
                if (saved.Length == 0) SessionState.EraseString(PlaySnapshotSession.SessionKey);
                else SessionState.SetString(PlaySnapshotSession.SessionKey, saved);
                EditorPlaySnapshotBridge.Clear();
                if (savedDeclaration != null && savedLoad != null) EditorPlaySnapshotBridge.Set(savedDeclaration, savedLoad);
            }
        }

        private static EditorPlaySnapshotScene Scene(string identity, string copy, bool request)
            => new(identity, copy, identity == "Title", request, string.Empty, false, string.Empty);

        private static SceneSnapshotEntry Entry(string identity, string copy, bool request) => new()
        {
            identity = identity,
            sourcePath = "Assets/SampleGame/Scenes/" + identity + ".unity",
            copyPath = copy,
            wasDirty = false,
            sha256 = "sha-" + identity,
            isActive = identity == "Title",
            requestAtStartup = request,
        };
    }
}
