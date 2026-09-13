#nullable enable

using System;
using UnityEngine;

namespace CD0Spike
{
    internal sealed class Cd0ProbeRunner : MonoBehaviour
    {
        private const string CaseId = "local-happy-path";
        private readonly Cd0ContentSession _session = new();

        [SerializeField] private string _contentDirectoryPath = string.Empty;

        private async void Start()
        {
            var sink = new Cd0UnityLogEventSink();
            try
            {
                sink.Record(new Cd0ProbeEvent(CaseId, 1, "directory", "issued", _contentDirectoryPath));
                var root = _session.RegisterAndGetRoot(_contentDirectoryPath);
                sink.Record(new Cd0ProbeEvent(CaseId, 1, "directory", "completed", root.name));

                var asset = await _session.LoadAssetAsync(root);
                sink.Record(new Cd0ProbeEvent(CaseId, 1, "asset", "completed", asset.Value));

                var scene = await _session.LoadSceneAsync(root);
                sink.Record(new Cd0ProbeEvent(CaseId, 1, "scene", "completed", scene.name));
            }
            catch (Exception exception)
            {
                sink.Record(new Cd0ProbeEvent(CaseId, 1, "run", "failed", exception.ToString()));
            }
            finally
            {
                try
                {
                    await _session.CleanupAsync();
                    sink.Record(new Cd0ProbeEvent(CaseId, 1, "run", "cleanupDone", string.Empty));
                }
                catch (Exception exception)
                {
                    sink.Record(new Cd0ProbeEvent(CaseId, 1, "cleanup", "failed", exception.ToString()));
                }
            }
        }

    }
}
