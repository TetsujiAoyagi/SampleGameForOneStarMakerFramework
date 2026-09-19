#nullable enable
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using OneStarMaker.Runtime.SceneSystem;

namespace SampleGame.DependOnAll.Editor.Build
{
    /// <summary>BS4 Player build の project-global mutation を一つの finally 境界で所有する。</summary>
    internal sealed class PlayerBuildProjectMutation : IDisposable
    {
        private const string Marker = ".bs4-owner";
        private readonly string _identity;
        private readonly string _assetRoot;
        private readonly string _configPath = "Assets/StreamingAssets/bs4-runtime.json";
        private readonly ScriptingImplementation _backend;
        private readonly ManagedStrippingLevel _stripping;
        private readonly AddressableAssetSettings.PlayerBuildOption _buildAddressables;
        private readonly AddressableAssetSettings _addressableSettings;
        private bool _disposed;

        internal PlayerBuildProjectMutation(string identity, string runtimeJson)
        {
            _identity = identity;
            _assetRoot = "Assets/Resources/BS4/" + identity;
            RejectOrCleanStale(_assetRoot, identity);
            if (File.Exists(_configPath)) throw new InvalidOperationException("StreamingAssets BS4 config is not self-owned.");
            _backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            _stripping = PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Standalone);
            var addressableSettings = AddressableAssetSettingsDefaultObject.Settings;
            if (addressableSettings == null)
                throw new InvalidOperationException("Addressables settings are missing.");
            _addressableSettings = addressableSettings;
            _buildAddressables = _addressableSettings.BuildAddressablesWithPlayerBuild;
            try
            {
                Directory.CreateDirectory(_assetRoot); File.WriteAllText(Path.Combine(_assetRoot, Marker), identity);
                Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!); File.WriteAllText(_configPath, runtimeJson);
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.High);
                _addressableSettings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
                AssetDatabase.Refresh();
            }
            catch { Dispose(); throw; }
        }

        internal string AssetRoot => _assetRoot;

        internal BootstrapSceneCopy CopyBootstrapScene(string sourcePath)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(sourcePath) == null)
                throw new InvalidOperationException("Production bootstrap Scene is missing: " + sourcePath);
            var sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            var targetPath = _assetRoot + "/UICommon.unity";
            if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
                throw new IOException("Failed to copy bootstrap Scene: " + sourcePath);
            var targetGuid = AssetDatabase.AssetPathToGUID(targetPath);
            if (string.IsNullOrEmpty(targetGuid) || string.Equals(sourceGuid, targetGuid, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Generated bootstrap Scene has no independent GUID.");

            var scene = EditorSceneManager.OpenScene(targetPath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var uiRoots = Array.FindAll(roots, root => root.name == "UICommon" && root.GetComponent<OneStarMaker.Runtime.UISystem.UICommon>() != null);
                var eventSystems = Array.FindAll(roots, root => root.GetComponentInChildren<EventSystem>(true) != null);
                if (uiRoots.Length != 1 || eventSystems.Length == 0)
                    throw new InvalidOperationException("Generated bootstrap Scene must contain exactly one UICommon root and an EventSystem.");
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
            return new BootstrapSceneCopy(targetPath, sourceGuid, targetGuid);
        }

        internal void CopyGraphClosure(string sourceMapPath)
        {
            var sourceMap = AssetDatabase.LoadAssetAtPath<SceneResourceMap>(sourceMapPath);
            if (sourceMap == null)
                throw new InvalidOperationException("Production SceneResourceMap is missing.");
            var replacements = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
            foreach (var source in sourceMap.SceneResources)
            {
                if (source == null) throw new InvalidOperationException("Production graph contains a null SceneResource.");
                var sourcePath = AssetDatabase.GetAssetPath(source);
                if (string.IsNullOrEmpty(sourcePath)) throw new InvalidOperationException("SceneResource is external to AssetDatabase.");
                var targetPath = _assetRoot + "/" + Path.GetFileName(sourcePath);
                if (!AssetDatabase.CopyAsset(sourcePath, targetPath)) throw new IOException("Failed to copy SceneResource: " + sourcePath);
                replacements.Add(source, AssetDatabase.LoadAssetAtPath<SceneResource>(targetPath));
            }
            var mapPath = _assetRoot + "/SceneResourceMap.asset";
            if (!AssetDatabase.CopyAsset(sourceMapPath, mapPath)) throw new IOException("Failed to copy SceneResourceMap.");
            replacements.Add(sourceMap, AssetDatabase.LoadAssetAtPath<SceneResourceMap>(mapPath));
            foreach (var copy in replacements.Values) RewriteReferences(copy, replacements);
            AssetDatabase.SaveAssets();
        }

        private static void RewriteReferences(UnityEngine.Object target, IReadOnlyDictionary<UnityEngine.Object, UnityEngine.Object> replacements)
        {
            var serialized = new SerializedObject(target); var property = serialized.GetIterator();
            if (property.Next(true)) do
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null &&
                    replacements.TryGetValue(property.objectReferenceValue, out var replacement)) property.objectReferenceValue = replacement;
            } while (property.Next(true));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            try
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, _backend);
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, _stripping);
                _addressableSettings.BuildAddressablesWithPlayerBuild = _buildAddressables;
            }
            finally
            {
                if (AssetDatabase.LoadMainAssetAtPath(_configPath) != null || File.Exists(_configPath)) AssetDatabase.DeleteAsset(_configPath);
                var marker = Path.Combine(_assetRoot, Marker);
                if (Directory.Exists(_assetRoot) && File.Exists(marker) && File.ReadAllText(marker) == _identity) AssetDatabase.DeleteAsset(_assetRoot);
                AssetDatabase.Refresh();
            }
        }

        private static void RejectOrCleanStale(string root, string identity)
        {
            if (!Directory.Exists(root)) return;
            var marker = Path.Combine(root, Marker);
            if (!File.Exists(marker) || File.ReadAllText(marker) != identity) throw new InvalidOperationException("BS4 generated asset ownership mismatch.");
            Directory.Delete(root, true);
        }
    }

    internal readonly struct BootstrapSceneCopy
    {
        internal BootstrapSceneCopy(string path, string sourceGuid, string generatedGuid)
        { Path = path; SourceGuid = sourceGuid; GeneratedGuid = generatedGuid; }
        internal string Path { get; }
        internal string SourceGuid { get; }
        internal string GeneratedGuid { get; }
    }
}
