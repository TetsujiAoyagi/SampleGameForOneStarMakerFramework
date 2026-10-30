#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.Rendering.Vfx;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.UISystem;
using UnityEngine;
using ZLogger;
using Object = UnityEngine.Object;

namespace SampleGame.InGame.World
{
    /// <summary>
    /// Season 直下の major Event Scene（例: Spring_Event_MultiCellProof）の no-UI lifecycle scene。
    /// Add / Unload は Season 枝の major Event driver が所有し、この class は Event の中身だけを持つ。
    /// <list type="bullet">
    /// <item><description>Load: anchor の VFX source をこの Scene 所有（<c>Scene(id)</c>）でロードする。</description></item>
    /// <item><description>Stable: anchor ごとに VFX をこの Scene 所有で起動する。完了の受付を始める。</description></item>
    /// <item><description>完了: 受付の開始後の最初の進入で、driver が付けた delegate を 1 回呼ぶ。unload は driver が進める。</description></item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 回収は Framework の <c>Scene(id)</c> 所有の経路に任せ、ここでは <c>Stop</c> しない: unload の開始（PreUnloading）で
    /// instance が壊れ、その後で Unity Scene の unload と source の解放（ReleaseScene）が進む。
    /// 親の生存は確かめない。Season の unload 中に Stable に達して起動しても、保留された unload の同じ経路が壊す。
    /// </para>
    /// <para>
    /// Stable の時点で既に完了の体積の中にいる物体は、出て入り直すまで完了にしない（受付の前の進入は捨てる）。
    /// </para>
    /// </remarks>
    public sealed class SeasonMajorEventScene : SceneBase
    {
        private readonly ILogger<SeasonMajorEventScene> _logger;
        private readonly IVfxSystem _vfx;
        private readonly List<(Vector3 Position, Quaternion Rotation, string Address)> _anchors = new();
        private readonly Dictionary<string, Object> _sources = new(StringComparer.Ordinal);
        private Action? _onCompleted;
        private bool _accepting;
        private bool _completed;

        public SeasonMajorEventScene(
            SceneResource sceneResource,
            ISceneQuery sceneQuery,
            ISceneController sceneController,
            ILoggerFactory loggerFactory,
            IVfxSystem vfx)
            : base(sceneResource, sceneQuery, sceneController)
        {
            if (sceneResource.Parent == null || sceneResource.Parent.StreamByDistance)
            {
                throw new ArgumentException("SeasonMajorEventScene requires a non-streaming Season parent.", nameof(sceneResource));
            }
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _vfx = vfx ?? throw new ArgumentNullException(nameof(vfx));
            _logger = loggerFactory.CreateLogger<SeasonMajorEventScene>();
            _logger.ZLogInformation($"Create SeasonMajorEventScene {sceneResource.Identity}");
        }

        /// <summary>
        /// 完了の受け口を付ける。driver が個体を捕捉した時点（Add の最初の await より前）に呼ぶので、Stable より先に付いている。
        /// </summary>
        internal void SetCompletionHandler(Action onCompleted)
        {
            _onCompleted = onCompleted ?? throw new ArgumentNullException(nameof(onCompleted));
        }

        protected sealed override UIView? SearchUIView() => null;

        protected override void OnInitialize()
        {
            foreach (var root in RootObjects)
            {
                if (root == null)
                {
                    continue;
                }

                foreach (var anchor in root.GetComponentsInChildren<MajorEventVfxAnchor>(includeInactive: true))
                {
                    var transform = anchor.transform;
                    _anchors.Add((transform.position, transform.rotation, anchor.VfxAddress));
                }

                foreach (var trigger in root.GetComponentsInChildren<MajorEventCompletionTrigger>(includeInactive: true))
                {
                    trigger.Attach(OnCompletionEntered);
                }
            }
        }

        protected override async UniTask OnLoadedImpl(CancellationToken ct)
        {
            // OnLoadedImpl はキャンセル窓の外なので、この Scene の unload はロードの完了まで保留され、
            // ReleaseScene がロードの完了より前に起きない。handle は個別に Release せず、Scene の ReleaseScene に任せる。
            foreach (var (_, _, address) in _anchors)
            {
                if (_sources.ContainsKey(address))
                {
                    continue;
                }

                Object? source;
                try
                {
                    source = (await LoadSceneScopedAssetAsync<Object>(AssetKey.FromAddress(address), ct)).Value;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    // VFX は演出なので Event の失敗にしない（Content Directory を使う実行では .vfx が directory に無い）。
                    _logger.ZLogError(exception, $"Major event VFX source failed to load and stays inactive: {SceneResource.Identity} ({address})");
                    continue;
                }

                if (source == null)
                {
                    _logger.ZLogError($"Major event VFX source loaded nothing and stays inactive: {SceneResource.Identity} ({address})");
                    continue;
                }

                _sources[address] = source;
            }
        }

        protected override UniTask OnStabledImpl()
        {
            foreach (var (position, rotation, address) in _anchors)
            {
                if (_sources.TryGetValue(address, out var source))
                {
                    _vfx.Spawn(new VfxSpawnRequest(source, position, rotation), SceneAssetOwner).Play();
                }
            }

            _accepting = true;
            return UniTask.CompletedTask;
        }

        protected override UniTask OnPreUnLoadedImpl()
        {
            // unload の開始の後は完了を通知しない（driver が既に unload を進めているか、枝・Session の終了による回収）。
            _accepting = false;
            _sources.Clear();
            return UniTask.CompletedTask;
        }

        private void OnCompletionEntered()
        {
            // 物理の callback の中では delegate を呼ぶだけ。unload は driver が非同期に進める。
            if (!_accepting || _completed || _onCompleted == null)
            {
                return;
            }

            _completed = true;
            _onCompleted();
        }
    }
}
