#nullable enable

using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CD0Spike
{
    internal sealed class Cd0ProbeRunner : MonoBehaviour
    {
        private const string CaseId = "local-happy-path";
        private const int Generation = 1;
        private readonly Cd0ContentSession _session = new();
        private readonly Cd0RunLedger _assetLedger = new(Generation, Cd0OperationKind.Asset);
        private readonly Cd0RunLedger _sceneLedger = new(Generation, Cd0OperationKind.Scene);

        [SerializeField] private string _contentDirectoryPath = string.Empty;

        private async void Start()
        {
            var sink = new Cd0UnityLogEventSink();
            try
            {
                var contentDirectoryPath = ResolveContentDirectoryPath();
                sink.Record(new Cd0ProbeEvent(CaseId, Generation, "directory", "issued", contentDirectoryPath));
                var root = _session.RegisterAndGetRoot(contentDirectoryPath);
                sink.Record(new Cd0ProbeEvent(CaseId, Generation, "directory", "completed", root.name));

                _assetLedger.MarkIssued(Generation);
                Cd0ProbeAsset asset;
                try
                {
                    asset = await _session.LoadAssetAsync(root);
                    _assetLedger.MarkCompleted(Generation, succeeded: true);
                }
                catch
                {
                    _assetLedger.MarkCompleted(Generation, succeeded: false);
                    throw;
                }
                sink.Record(new Cd0ProbeEvent(CaseId, Generation, "asset", _assetLedger.Accepted ? "accepted" : "abandoned", asset.Value));
                if (!_assetLedger.Accepted) return;

                _sceneLedger.MarkIssued(Generation);
                Scene scene;
                try
                {
                    scene = await _session.LoadSceneAsync(root);
                    _sceneLedger.MarkCompleted(Generation, succeeded: true);
                }
                catch
                {
                    _sceneLedger.MarkCompleted(Generation, succeeded: false);
                    throw;
                }
                sink.Record(new Cd0ProbeEvent(CaseId, Generation, "scene", _sceneLedger.Accepted ? "accepted" : "abandoned", scene.name));
            }
            catch (Exception exception)
            {
                sink.Record(new Cd0ProbeEvent(CaseId, Generation, "run", "failed", exception.ToString()));
            }
            finally
            {
                try
                {
                    await _session.CleanupAsync();
                    if (_sceneLedger.Completed) _sceneLedger.MarkCleanupDone(Generation);
                    if (_assetLedger.Completed) _assetLedger.MarkCleanupDone(Generation);
                    sink.Record(new Cd0ProbeEvent(CaseId, Generation, "run", "cleanupDone", string.Empty));
                }
                catch (Exception exception)
                {
                    if (_sceneLedger.Completed && !_sceneLedger.CleanupDone) _sceneLedger.MarkCleanupFailed(Generation);
                    if (_assetLedger.Completed && !_assetLedger.CleanupDone) _assetLedger.MarkCleanupFailed(Generation);
                    sink.Record(new Cd0ProbeEvent(CaseId, Generation, "cleanup", "failed", exception.ToString()));
                }
            }
        }

        private void OnDestroy()
        {
            if (_sceneLedger.Issued && !_sceneLedger.Completed) _sceneLedger.Abandon(Generation);
            if (_assetLedger.Issued && !_assetLedger.Completed) _assetLedger.Abandon(Generation);
        }

        private string ResolveContentDirectoryPath()
        {
            const string argumentPrefix = "--cd0-content-directory=";
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (argument.StartsWith(argumentPrefix, StringComparison.Ordinal))
                {
                    return Path.GetFullPath(argument.Substring(argumentPrefix.Length));
                }
            }

            if (!string.IsNullOrWhiteSpace(_contentDirectoryPath)) return Path.GetFullPath(_contentDirectoryPath);
            throw new InvalidOperationException($"Pass {argumentPrefix}<path> or author a non-empty fixture path.");
        }

    }
}
