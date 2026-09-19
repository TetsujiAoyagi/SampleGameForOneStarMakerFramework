#nullable enable
using System;
using System.IO;
using System.Threading;
using OneStarMaker.Runtime.BuildContent.Distribution;
using UnityEditor;
using UnityEngine;

namespace SampleGame.DependOnAll.Editor.Build
{
    internal sealed class ContentDeliveryWindow : EditorWindow
    {
        private const string SettingsPath = "UserSettings/OSMContentDelivery.json";
        private Settings _settings = new();
        private CancellationTokenSource? _cts;
        private string _status = "";

        [MenuItem("Tools/OSM/Content/Delivery")]
        private static void Open() => GetWindow<ContentDeliveryWindow>("OSM Content Delivery");

        private void OnEnable()
        {
            if (File.Exists(SettingsPath))
                _settings = JsonUtility.FromJson<Settings>(File.ReadAllText(SettingsPath)) ?? new Settings();
        }

        private void OnDisable() { _cts?.Cancel(); _cts?.Dispose(); }

        private void OnGUI()
        {
            _settings.sourceMode = EditorGUILayout.Popup("Source", _settings.sourceMode,
                new[] { "Local/LAN directory", "HTTP base URL", "Installed offline" });
            if (_settings.sourceMode == 0) _settings.sourceRoot = EditorGUILayout.TextField("Published directory", _settings.sourceRoot);
            else if (_settings.sourceMode == 1) _settings.httpBase = EditorGUILayout.TextField("HTTP base URL", _settings.httpBase);
            else _settings.installedRoot = EditorGUILayout.TextField("Installed revision root", _settings.installedRoot);
            _settings.digest = EditorGUILayout.TextField("Manifest SHA-256", _settings.digest);
            _settings.contentSet = EditorGUILayout.TextField("Content set", _settings.contentSet);
            _settings.revision = EditorGUILayout.TextField("Revision", _settings.revision);
            _settings.cacheRoot = EditorGUILayout.TextField("Cache root", _settings.cacheRoot);
            _settings.representation = EditorGUILayout.TextField("Representation", _settings.representation);
            _settings.budget = EditorGUILayout.LongField("Disk budget", _settings.budget);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            { if (GUILayout.Button("Prepare")) _ = Prepare(); if (GUILayout.Button("Use For Next Play")) UsePrepared(); if (GUILayout.Button("Reset")) ContentDeliveryPlayBridge.Reset(); }
            EditorGUILayout.HelpBox(_status, MessageType.Info);
        }

        private async System.Threading.Tasks.Task Prepare()
        {
            _cts?.Cancel(); _cts?.Dispose(); _cts = new CancellationTokenSource();
            try
            {
                Save();
                var store = new ContentCacheStore(_settings.cacheRoot);
                ContentInstallResult result;
                if (_settings.sourceMode == 2)
                    result = store.ValidateInstalled(_settings.installedRoot, _settings.digest,
                        _settings.contentSet, _settings.revision, "StandaloneWindows64-Player");
                else
                {
                    using IContentArtifactSource source = _settings.sourceMode == 1
                        ? new HttpContentArtifactSource(new Uri(_settings.httpBase))
                        : new LocalContentArtifactSource(_settings.sourceRoot);
                    var request = new ContentInstallRequest(_settings.digest, _settings.contentSet,
                        _settings.revision, "StandaloneWindows64-Player", Application.unityVersion, 2, _settings.budget);
                    result = await new ContentInstaller(store).InstallAsync(request, source, _cts.Token);
                    _settings.installedRoot = result.RevisionRoot;
                }
                // Apply は編集可能な入力でなく、Prepare が検証した値だけを使用する。
                _settings.preparedRoot = result.RevisionRoot; _settings.preparedDigest = result.ManifestSha256;
                _settings.preparedSet = result.ContentSet; _settings.preparedRevision = result.Revision;
                var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                var advice = ContentSourceAdvisor.InspectInstalled(result, projectRoot);
                Save();
                _status = $"Prepared and fully verified: {result.RevisionRoot}. Source status: {advice.Status} ({advice.Paths.Count} paths). Source status does not block Player launch.";
            }
            catch (Exception ex) { _status = ex.Message; }
            Repaint();
        }

        private void UsePrepared()
        {
            if (_settings.preparedRoot != _settings.installedRoot || _settings.preparedDigest != _settings.digest ||
                _settings.preparedSet != _settings.contentSet || _settings.preparedRevision != _settings.revision)
                throw new InvalidOperationException("Inputs changed after Prepare; prepare the selected revision again.");
            ContentDeliveryPlayBridge.Apply(_settings.preparedRoot, _settings.preparedDigest,
                _settings.preparedRevision, _settings.preparedSet, _settings.representation);
        }

        private void Save() { Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!); File.WriteAllText(SettingsPath, JsonUtility.ToJson(_settings, true)); }

        [Serializable] private sealed class Settings
        { public int sourceMode; public string sourceRoot="",httpBase="",digest="",contentSet="bs4-spring-full",revision="",cacheRoot="",representation="Full",installedRoot="",preparedRoot="",preparedDigest="",preparedSet="",preparedRevision=""; public long budget=10L*1024*1024*1024; }
    }
}
