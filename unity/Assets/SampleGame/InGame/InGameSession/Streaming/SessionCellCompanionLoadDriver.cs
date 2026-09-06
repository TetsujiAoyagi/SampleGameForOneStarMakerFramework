#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.SceneSystem;
using SampleGame.InGame.World;
using ZLogger;

namespace SampleGame.InGame.Streaming
{
    /// <summary>resident Cell の structural children を一つの profile policy で明示ロードする。</summary>
    internal sealed class SessionCellCompanionLoadDriver : IDisposable
    {
        private readonly ISceneController _sceneController;
        private readonly ISceneQuery _sceneQuery;
        private readonly Func<IReadOnlyList<string>> _residentCellsProvider;
        private readonly CellCompanionSet _companionSet;
        private readonly Microsoft.Extensions.Logging.ILogger _logger;
        private readonly HashSet<string> _inFlightAdds = new(StringComparer.Ordinal);
        private CancellationTokenSource? _loopCts;
        private bool _disposed;

        internal SessionCellCompanionLoadDriver(
            ISceneController sceneController,
            ISceneQuery sceneQuery,
            Func<IReadOnlyList<string>> residentCellsProvider,
            CellCompanionSet companionSet,
            Microsoft.Extensions.Logging.ILogger logger)
        {
            _sceneController = sceneController ?? throw new ArgumentNullException(nameof(sceneController));
            _sceneQuery = sceneQuery ?? throw new ArgumentNullException(nameof(sceneQuery));
            _residentCellsProvider = residentCellsProvider ?? throw new ArgumentNullException(nameof(residentCellsProvider));
            _companionSet = companionSet;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        internal bool IsRunning => !_disposed && _loopCts is { IsCancellationRequested: false };

        internal IReadOnlyList<string> GetLoadedCompanionIdentities()
        {
            var loaded = new List<string>();
            var residents = _residentCellsProvider();
            for (var i = 0; i < residents.Count; i++)
            {
                var parent = _sceneQuery.GetLoadedScene(residents[i]) as CellScene;
                if (parent == null)
                {
                    continue;
                }

                var children = parent.SceneResource.Children;
                for (var j = 0; j < children.Count; j++)
                {
                    var child = children[j];
                    if (child == null
                        || child.Parent == null
                        || !child.Parent.StreamByDistance
                        || !CellCompanionRoleClassifier.TryClassify(child.Identity, out var role)
                        || !CellCompanionPolicy.Includes(_companionSet, role)
                        || !_sceneQuery.IsSceneStable(child.Identity))
                    {
                        continue;
                    }
                    loaded.Add(child.Identity);
                }
            }
            return loaded.Count == 0 ? Array.Empty<string>() : loaded.ToArray();
        }

        internal void Start()
        {
            ThrowIfDisposed();
            if (_loopCts != null)
            {
                return;
            }

            _loopCts = new CancellationTokenSource();
            RunLoopAsync(_loopCts.Token).Forget();
            _logger.ZLogInformation($"SessionCellCompanionLoadDriver loop started");
        }

        internal void Stop()
        {
            if (_loopCts == null || _loopCts.IsCancellationRequested)
            {
                return;
            }
            _loopCts.Cancel();
            _logger.ZLogInformation($"SessionCellCompanionLoadDriver loop stopped");
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            Stop();
            _loopCts?.Dispose();
            _loopCts = null;
            _inFlightAdds.Clear();
            _disposed = true;
        }

        internal async UniTask ReconcileOnceAsync(CancellationToken ct)
        {
            if (_disposed || ct.IsCancellationRequested)
            {
                return;
            }

            var residents = _residentCellsProvider();
            for (var i = 0; i < residents.Count && !ct.IsCancellationRequested; i++)
            {
                var parentIdentity = residents[i];
                var parentToken = _sceneQuery.GetLoadedScene(parentIdentity) as CellScene;
                if (parentToken == null || !_sceneQuery.IsSceneStable(parentIdentity))
                {
                    continue;
                }

                var selected = SelectChildren(parentToken.SceneResource.Children);
                for (var j = 0; j < selected.Count && !ct.IsCancellationRequested; j++)
                {
                    await TryAddAsync(parentIdentity, parentToken, selected[j], ct);
                }
            }
        }

        private IReadOnlyList<string> SelectChildren(IReadOnlyList<SceneResource> children)
        {
            var byRole = new Dictionary<CellCompanionRole, List<string>>();
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (child == null)
                {
                    continue;
                }
                if (child.Parent == null || !child.Parent.StreamByDistance)
                {
                    _logger.ZLogWarning($"Non-structural resource found in Cell Children: {child.Identity}");
                    continue;
                }
                if (!CellCompanionRoleClassifier.TryClassify(child.Identity, out var role))
                {
                    _logger.ZLogWarning($"Unknown Cell companion role: {child.Identity}");
                    continue;
                }
                if (!CellCompanionPolicy.Includes(_companionSet, role))
                {
                    continue;
                }
                if (!byRole.TryGetValue(role, out var identities))
                {
                    identities = new List<string>();
                    byRole.Add(role, identities);
                }
                identities.Add(child.Identity);
            }

            var result = new List<string>();
            foreach (var pair in byRole)
            {
                if (pair.Value.Count != 1)
                {
                    _logger.ZLogWarning($"Duplicate Cell companion role {pair.Key}; none selected: {string.Join(",", pair.Value)}");
                    continue;
                }
                result.Add(pair.Value[0]);
            }
            return result;
        }

        private async UniTask TryAddAsync(string parentIdentity, SceneBase parentToken, string childIdentity, CancellationToken ct)
        {
            if (!CellCompanionLoadRules.ShouldAdd(
                    _sceneQuery.IsSceneStable(parentIdentity),
                    roleIsIncluded: true,
                    _sceneQuery.IsSceneLoaded(childIdentity),
                    _inFlightAdds.Contains(childIdentity))
                || ct.IsCancellationRequested)
            {
                return;
            }

            if (!_inFlightAdds.Add(childIdentity))
            {
                return;
            }

            try
            {
                if (!_sceneQuery.IsSceneStable(parentIdentity) || ct.IsCancellationRequested)
                {
                    return;
                }

                await _sceneController.AddScene(
                    childIdentity,
                    afterOnLoadedTask: null,
                    ct: ct,
                    loadingDisplay: LoadingDisplayType.None);

                var freshResidents = _residentCellsProvider();
                var stillResident = ContainsIdentity(freshResidents, parentIdentity);
                var currentParent = _sceneQuery.GetLoadedScene(parentIdentity);
                if ((!stillResident
                     || !_sceneQuery.IsSceneStable(parentIdentity)
                     || !ReferenceEquals(currentParent, parentToken)
                     || ct.IsCancellationRequested)
                    && _sceneQuery.IsSceneLoaded(childIdentity))
                {
                    await _sceneController.UnloadScene(childIdentity, LoadingDisplayType.None);
                }
            }
            catch (OperationCanceledException)
            {
                // session teardown during the cancellable PreLoad window
            }
            catch (Exception ex)
            {
                _logger.ZLogWarning(ex, $"Cell companion AddScene failed: {childIdentity}");
            }
            finally
            {
                _inFlightAdds.Remove(childIdentity);
            }
        }

        private async UniTaskVoid RunLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await ReconcileOnceAsync(ct);
                    await UniTask.Delay(TimeSpan.FromSeconds(WorldCellCatalog.TickIntervalSeconds), cancellationToken: ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.ZLogError(ex, $"SessionCellCompanionLoadDriver loop failed");
            }
        }

        private static bool ContainsIdentity(IReadOnlyList<string> identities, string expected)
        {
            for (var i = 0; i < identities.Count; i++)
            {
                if (string.Equals(identities[i], expected, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SessionCellCompanionLoadDriver));
        }
    }
}
