#nullable enable

using System;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SampleGame.DependOnAll.Editor.WorldAuthoring
{
    [Serializable]
    internal sealed class WorldCompanionSceneSetupData
    {
        public string path = string.Empty;
        public bool isLoaded;
        public bool isActive;
    }

    [Serializable]
    internal sealed class WorldCompanionJournalData
    {
        public string identity = string.Empty;
        public string parentIdentity = string.Empty;
        public string scenePath = string.Empty;
        public string resourcePath = string.Empty;
        public string nodePath = string.Empty;
        public string companionFolderPath = string.Empty;
        public string resourceOutputPath = string.Empty;
        public string sceneResourceMapPath = string.Empty;
        public string sourceSearchRoot = string.Empty;
        public string startingSourceHash = string.Empty;
        public string parentNodeGuid = string.Empty;
        public string graphGuid = string.Empty;
        public string sceneGuid = string.Empty;
        public string resourceGuid = string.Empty;
        public string nodeGuid = string.Empty;
        public string companionFolderGuid = string.Empty;
        public string sceneFingerprint = string.Empty;
        public string resourceFingerprint = string.Empty;
        public string nodeFingerprint = string.Empty;
        public string addressableFingerprint = string.Empty;
        public string originalActiveScenePath = string.Empty;
        public WorldCompanionSceneSetupData[] originalSceneSetup = Array.Empty<WorldCompanionSceneSetupData>();
        public int completedRecoveryBarrier;
    }

    internal static class WorldCompanionRecoveryJournal
    {
        internal static string JournalPath
        {
            get
            {
                var projectRoot = Path.GetDirectoryName(Application.dataPath)
                    ?? throw new InvalidOperationException("Project root is unavailable.");
                return Path.Combine(projectRoot, "Library", "OneStarMaker", "WorldWorkspace", "pending-companion-create.json");
            }
        }

        internal static bool Exists => File.Exists(JournalPath);

        internal static WorldCompanionJournalData Create(
            WorldCompanionCreationPlan plan,
            string startingSourceHash,
            string parentNodeGuid,
            string graphGuid,
            SceneSetup[] setup,
            string activeScenePath)
        {
            var data = new WorldCompanionJournalData
            {
                identity = plan.Identity,
                parentIdentity = plan.ParentIdentity,
                scenePath = plan.ScenePath,
                resourcePath = plan.ResourcePath,
                nodePath = plan.NodePath,
                companionFolderPath = Path.GetDirectoryName(plan.ScenePath)!.Replace('\\', '/'),
                resourceOutputPath = plan.ResourceOutputPath,
                sceneResourceMapPath = plan.SceneResourceMapPath,
                sourceSearchRoot = plan.SourceSearchRoot,
                startingSourceHash = startingSourceHash,
                parentNodeGuid = parentNodeGuid,
                graphGuid = graphGuid,
                originalActiveScenePath = activeScenePath,
                originalSceneSetup = new WorldCompanionSceneSetupData[setup.Length],
            };
            for (var i = 0; i < setup.Length; i++)
            {
                data.originalSceneSetup[i] = new WorldCompanionSceneSetupData
                {
                    path = setup[i].path,
                    isLoaded = setup[i].isLoaded,
                    isActive = setup[i].isActive,
                };
            }
            Save(data);
            return data;
        }

        internal static WorldCompanionJournalData Load()
        {
            var json = File.ReadAllText(JournalPath);
            var data = JsonUtility.FromJson<WorldCompanionJournalData>(json);
            if (data == null
                || string.IsNullOrWhiteSpace(data.identity)
                || string.IsNullOrWhiteSpace(data.parentIdentity)
                || !IsAssetPath(data.scenePath)
                || !IsAssetPath(data.resourcePath)
                || !IsAssetPath(data.nodePath)
                || !IsAssetPath(data.companionFolderPath)
                || !IsAssetPath(data.resourceOutputPath)
                || !IsAssetPath(data.sceneResourceMapPath)
                || (!string.IsNullOrEmpty(data.sourceSearchRoot) && !IsAssetPath(data.sourceSearchRoot))
                || !string.Equals(Path.GetDirectoryName(data.scenePath)!.Replace('\\', '/'), data.companionFolderPath, StringComparison.Ordinal)
                || !string.Equals(Path.GetDirectoryName(data.resourcePath)!.Replace('\\', '/'), data.companionFolderPath, StringComparison.Ordinal)
                || !string.Equals(Path.GetFileNameWithoutExtension(data.scenePath), data.identity, StringComparison.Ordinal)
                || !string.Equals(Path.GetFileNameWithoutExtension(data.resourcePath), data.identity, StringComparison.Ordinal)
                || !string.Equals(Path.GetFileNameWithoutExtension(data.nodePath), data.identity, StringComparison.Ordinal)
                || !IsHex(data.startingSourceHash, 16)
                || !IsGuid(data.parentNodeGuid)
                || !IsGuid(data.graphGuid)
                || !IsOptionalGuid(data.sceneGuid)
                || !IsOptionalGuid(data.resourceGuid)
                || !IsOptionalGuid(data.nodeGuid)
                || !IsOptionalGuid(data.companionFolderGuid)
                || !IsOptionalFingerprint(data.sceneFingerprint)
                || !IsOptionalFingerprint(data.resourceFingerprint)
                || !IsOptionalFingerprint(data.nodeFingerprint)
                || !IsOptionalFingerprint(data.addressableFingerprint)
                || data.originalSceneSetup == null
                || data.completedRecoveryBarrier < 0
                || data.completedRecoveryBarrier > (int)WorldCompanionRecoveryBarrier.Saved)
            {
                throw new InvalidDataException($"Corrupt World Workspace journal: {JournalPath}");
            }
            return data;
        }

        internal static void Save(WorldCompanionJournalData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var path = JournalPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data, prettyPrint: true));
            if (File.Exists(path))
            {
                var backup = path + ".bak";
                File.Replace(temp, path, backup, ignoreMetadataErrors: true);
                if (File.Exists(backup)) File.Delete(backup);
            }
            else
            {
                File.Move(temp, path);
            }
        }

        internal static void Delete()
        {
            if (File.Exists(JournalPath)) File.Delete(JournalPath);
        }

        internal static SceneSetup[] RestoreSetup(WorldCompanionJournalData data)
        {
            var result = new SceneSetup[data.originalSceneSetup.Length];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = new SceneSetup
                {
                    path = data.originalSceneSetup[i].path,
                    isLoaded = data.originalSceneSetup[i].isLoaded,
                    isActive = data.originalSceneSetup[i].isActive,
                };
            }
            return result;
        }

        internal static bool SetupMatches(SceneSetup[] actual, WorldCompanionSceneSetupData[] expected)
        {
            if (actual.Length != expected.Length) return false;
            for (var i = 0; i < actual.Length; i++)
                if (!string.Equals(actual[i].path, expected[i].path, StringComparison.Ordinal)
                    || actual[i].isLoaded != expected[i].isLoaded || actual[i].isActive != expected[i].isActive) return false;
            return true;
        }

        private static bool IsAssetPath(string value)
            => !string.IsNullOrWhiteSpace(value)
               && value.StartsWith("Assets/", StringComparison.Ordinal)
               && !value.Contains("..", StringComparison.Ordinal)
               && value.IndexOf('\\') < 0;

        private static bool IsOptionalGuid(string value) => string.IsNullOrEmpty(value) || IsGuid(value);

        private static bool IsOptionalFingerprint(string value) => string.IsNullOrEmpty(value) || IsHex(value, 64);

        private static bool IsGuid(string value) => IsHex(value, 32);

        private static bool IsHex(string value, int length)
        {
            if (value.Length != length) return false;
            for (var i = 0; i < value.Length; i++)
                if (!Uri.IsHexDigit(value[i])) return false;
            return true;
        }
    }
}
