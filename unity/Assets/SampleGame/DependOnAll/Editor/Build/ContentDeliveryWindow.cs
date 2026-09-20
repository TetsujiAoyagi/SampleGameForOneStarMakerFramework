#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        private Vector2 _scroll;

        [MenuItem("Tools/OSM/Content/Delivery")]
        private static void Open() => GetWindow<ContentDeliveryWindow>("OSM Content Delivery");

        private void OnEnable()
        {
            if (File.Exists(SettingsPath))
                _settings = JsonUtility.FromJson<Settings>(File.ReadAllText(SettingsPath)) ?? new Settings();
        }

        private void OnDisable()
        {
            var operation = _cts;
            _cts = null;
            operation?.Cancel();
            // operation は finally で自分を dispose する。旧 continuation に UI 所有権を残さない。
        }

        private void OnGUI()
        {
            using (new EditorGUI.DisabledScope(_cts != null || EditorApplication.isPlayingOrWillChangePlaymode))
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
                if (GUILayout.Button("Prepare")) _ = Prepare();
                if (GUILayout.Button("Use For Next Play"))
                {
                    try { UsePrepared(); }
                    catch (Exception ex) { _status = ex.Message; }
                }
                if (GUILayout.Button("Reset")) ContentDeliveryPlayBridge.Reset();
            }
            if (_cts != null && GUILayout.Button("Cancel download")) _cts.Cancel();
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(120));
            EditorGUILayout.SelectableLabel(_status, EditorStyles.wordWrappedLabel,
                GUILayout.MinHeight(Math.Max(120, _status.Count(c => c == '\n') * 18)));
            EditorGUILayout.EndScrollView();
        }

        private async Task Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _status = "Stop Play before preparing content.";
                return;
            }
            _cts?.Cancel();
            var operation = new CancellationTokenSource();
            _cts = operation;
            var token = operation.Token;
            // await 後に編集済み input を混ぜず、一回の request が持つ選択を固定する。
            var selected = JsonUtility.FromJson<Settings>(JsonUtility.ToJson(_settings));
            try
            {
                Save();
                var store = new ContentCacheStore(selected.cacheRoot);
                ContentInstallResult result;
                if (selected.sourceMode == 2)
                    result = store.ValidateInstalled(selected.installedRoot, selected.digest,
                        selected.contentSet, selected.revision, "StandaloneWindows64-Player");
                else
                {
                    using IContentArtifactSource source = selected.sourceMode == 1
                        ? new HttpContentArtifactSource(new Uri(selected.httpBase))
                        : new LocalContentArtifactSource(selected.sourceRoot);
                    var request = new ContentInstallRequest(selected.digest, selected.contentSet,
                        selected.revision, "StandaloneWindows64-Player", Application.unityVersion, 2, selected.budget);
                    result = await new ContentInstaller(store).InstallAsync(request, source, token);
                }
                if (_cts != operation || token.IsCancellationRequested || this == null) return;
                _settings.installedRoot = result.RevisionRoot;
                _settings.preparedRoot = result.RevisionRoot;
                _settings.preparedDigest = result.ManifestSha256;
                _settings.preparedSet = result.ContentSet;
                _settings.preparedRevision = result.Revision;
                // source 診断 I/O が失敗しても検証済み install の値を reload で失わない。
                Save();
                _status = $"Prepared revision {result.Revision} matches the requested revision.\n{result.RevisionRoot}\n";
                try
                {
                    var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                    var advice = ContentSourceAdvisor.InspectInstalled(result, projectRoot);
                    _status += "Source status: " + advice.Status + "\n" + string.Join("\n", advice.Paths);
                }
                catch (ContentDeliveryException ex) when (ex.Code == ContentDeliveryFailureCode.IoFailure)
                {
                    _status += "Source diagnosis unavailable: " + ex.Message + "\nVerified content remains prepared.";
                }
                _status += "\nSource files are needed for editing. Usual Editor Play uses the prepared install via Use For Next Play. "
                    + "A matching BS4 Player can run without source files using SAMPLEGAME_CONTENT__INSTALLEDREVISIONPATH="
                    + result.RevisionRoot + " and SAMPLEGAME_CONTENT__MANIFESTSHA256=" + result.ManifestSha256 + ".";
            }
            catch (Exception ex)
            {
                if (_cts == operation && this != null) _status = ex.Message;
            }
            finally
            {
                if (_cts == operation) _cts = null;
                operation.Dispose();
                if (this != null) Repaint();
            }
        }

        private void UsePrepared()
        {
            if (_settings.preparedRoot != _settings.installedRoot || _settings.preparedDigest != _settings.digest
                || _settings.preparedSet != _settings.contentSet || _settings.preparedRevision != _settings.revision)
                throw new InvalidOperationException("Inputs changed after Prepare; prepare the selected revision again.");
            ContentDeliveryPlayBridge.Apply(_settings.preparedRoot, _settings.preparedDigest,
                _settings.preparedRevision, _settings.preparedSet, _settings.representation);
        }

        private void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonUtility.ToJson(_settings, true));
        }

        [Serializable]
        private sealed class Settings
        {
            public int sourceMode;
            public string sourceRoot = "";
            public string httpBase = "";
            public string digest = "";
            public string contentSet = "bs4-spring-full";
            public string revision = "";
            public string cacheRoot = "";
            public string representation = "Full";
            public string installedRoot = "";
            public string preparedRoot = "";
            public string preparedDigest = "";
            public string preparedSet = "";
            public string preparedRevision = "";
            public long budget = 10L * 1024 * 1024 * 1024;
        }
    }
}
