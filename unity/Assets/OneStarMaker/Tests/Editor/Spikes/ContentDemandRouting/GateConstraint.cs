#nullable enable

using System;
using System.IO;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.AssetManagement.Internal;
using OneStarMaker.Runtime.BuildContent;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.Spikes.ContentDemandRouting
{
    /// <summary>
    /// 現行製品の制約の観測（HANDOFF §2 判定定義 (ii) の OSM 層）。既存の `ContentDirectorySession.Register` で
    /// 2 件目を登録し、`ContentRevisionGate` が拒否することを record に残す。これは製品の制約の証拠で、
    /// Unity の機構の不成立と同一視しない（cell の値には使わない）。
    /// gate は native の登録より前に予約で拒否するので、native は偽物で足りる。
    /// </summary>
    internal static class GateConstraint
    {
        internal static void Observe(HarnessState state, ExperimentRecord record, Cell cell)
        {
            var lease = state.Lease() ?? throw new InvalidOperationException("Temp root lease was lost.");
            var first = lease.EnsureDiskChild("gate-a");
            var second = lease.EnsureDiskChild("gate-b");
            ContentDirectorySession? firstSession = null;
            var firstRoot = ScriptableObject.CreateInstance<BuildContentRoot>();
            var secondRoot = ScriptableObject.CreateInstance<BuildContentRoot>();
            try
            {
                firstRoot.Initialize("cdr-gate-a", FixtureFactory.TargetName, Array.Empty<BuildContentEntry>());
                secondRoot.Initialize("cdr-gate-b", FixtureFactory.TargetName, Array.Empty<BuildContentEntry>());
                firstSession = ContentDirectorySession.Register(first, "cdr-gate-a", FixtureFactory.TargetName, new FakeNative(firstRoot));
                string outcome;
                try
                {
                    var secondSession = ContentDirectorySession.Register(second, "cdr-gate-b", FixtureFactory.TargetName, new FakeNative(secondRoot));
                    outcome = "second-registration-accepted";
                    secondSession.CompletePlayStop();
                }
                catch (ContentDirectoryException ex) { outcome = "rejected:" + ex.Code; }
                var assets = new Runtime.AssetManagement.AssetManagement(new CountingAddressablesBackend());
                assets.InstallContentDirectory(firstSession);
                string install;
                try { assets.InstallContentDirectory(new RoutingBackend(record, cell)); install = "second-install-accepted"; }
                catch (InvalidOperationException ex) { install = "rejected:" + ex.Message; }
                record.Write(cell, "osm.secondRegistration", ("contentRevisionGate", outcome), ("assetManagementInstall", install),
                    ("note", "product constraint evidence; not a cell value of mechanism (ii)"));
            }
            finally
            {
                firstSession?.CompletePlayStop();
                foreach (var routing in new System.Collections.Generic.List<RoutingBackend>(RoutingBackend.Unclosed)) routing.ReclaimFromPreviousRound();
                UnityEngine.Object.DestroyImmediate(firstRoot);
                UnityEngine.Object.DestroyImmediate(secondRoot);
                if (Directory.Exists(first)) Directory.Delete(first, true);
                if (Directory.Exists(second)) Directory.Delete(second, true);
            }
        }

        private sealed class FakeNative : IContentNativeDirectory
        {
            private readonly BuildContentRoot _root;
            internal FakeNative(BuildContentRoot root) => _root = root;
            public bool Register(string path) => true;
            public BuildContentRoot[] GetRoots() => new[] { _root };
            public void Unregister() { }
            public UniTask<IBackendAsset> LoadObjectAsync<T>(BuildContentEntry entry) where T : UnityEngine.Object => throw new NotSupportedException();
            public UniTask<IBackendScene> LoadSceneAsync(BuildContentEntry entry, SceneLoadOptions options) => throw new NotSupportedException();
            public UniTask<IBackendInstance> InstantiateAsync(BuildContentEntry entry, Transform? parent, bool worldSpace) => throw new NotSupportedException();
            public void Release(IBackendAsset asset) { }
            public UniTask UnloadSceneAsync(IBackendScene scene) => UniTask.CompletedTask;
        }
    }
}
