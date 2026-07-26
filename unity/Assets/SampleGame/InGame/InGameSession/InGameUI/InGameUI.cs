#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.SceneSystem;
using SampleGame.InGame.UI;
using UnityEngine;
using ZLogger;

namespace SampleGame.InGame
{
    /// <summary>
    /// InGame の UI シーン。飛行 HUD を UIToolkit + MVVM で持つ。
    /// PlayerScene とは兄弟であり、やり取りは親 <see cref="IInGameSessionServices"/> 経由のみ。
    /// </summary>
    public class InGameUI : SceneBase
    {
        private readonly ILogger<InGameUI> _logger;
        private IInGameSessionServices? _session;
        private InGameHudViewModel? _viewModel;
        private CancellationTokenSource? _pollCts;

        public InGameUI(
            SceneResource sceneResource,
            ISceneQuery sceneQuery,
            ISceneController sceneController,
            ILoggerFactory loggerFactory)
            : base(sceneResource, sceneQuery, sceneController)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            // Scene ごとのカテゴリを維持するため、文字列カテゴリではなく型付き logger を使用する。
            _logger = loggerFactory.CreateLogger<InGameUI>();
            _logger.ZLogInformation($"Create InGameUI");
        }

        /// <inheritdoc />
        protected override async UniTask OnLoadedImpl(CancellationToken ct)
        {
            // この時点ではまだ AddUIView 前なので Root / ViewModel は未生成。
            // 型の存在だけ確認し、配線は OnStabled（AddUIView 完了後）で行う。
            if (UIView is not InGameHudView)
            {
                throw new InvalidOperationException(
                    "InGameUI.unity に InGameHudView がありません。Editor メニューでシーンを再生成してください。");
            }

            await UniTask.CompletedTask;
        }

        /// <inheritdoc />
        protected override UniTask OnStabledImpl()
        {
            // SceneDirector は OnLoaded → AddUIView（Root 生成）→ Stable → OnStabled の順。
            // ViewModel は AddUIView で Root が触られた後に初めて存在する。
            if (UIView is not InGameHudView hudView)
            {
                throw new InvalidOperationException("InGameHudView が消失しています。");
            }

            _viewModel = hudView.ViewModel
                ?? throw new InvalidOperationException("InGameHudViewModel が未生成です。AddUIView の順序を確認してください。");

            // 親 Session / Player の Stable と順序が前後し得るため、ここで配線しポーリングを開始する。
            _session = ResolveSessionServices();
            _pollCts = new CancellationTokenSource();
            PollHudAsync(_pollCts.Token).Forget();
            _logger.ZLogInformation($"InGameUI bound to session hub (Cell Streaming HUD)");
            return UniTask.CompletedTask;
        }

        /// <inheritdoc />
        protected override UniTask OnPreUnLoadedImpl()
        {
            _pollCts?.Cancel();
            _session = null;
            _viewModel = null;
            return UniTask.CompletedTask;
        }

        /// <inheritdoc />
        protected override UniTask OnAfterUnLoadedImpl()
        {
            _pollCts?.Dispose();
            _pollCts = null;
            return UniTask.CompletedTask;
        }

        private IInGameSessionServices ResolveSessionServices()
        {
            var parent = SceneResource.Parent
                ?? throw new InvalidOperationException("InGameUI には InGameSession 親が必要です。");

            if (SceneQuery.GetLoadedScene(parent.Identity) is not IInGameSessionServices services)
            {
                throw new InvalidOperationException(
                    $"親シーン '{parent.Identity}' は IInGameSessionServices を提供していません。");
            }

            return services;
        }

        /// <summary>
        /// Focus / 常駐セルを間引いて ViewModel へ流す。
        /// 高頻度 Pos を毎フレーム Reactive 更新しすぎないよう、約 10Hz に制限する。
        /// </summary>
        private async UniTaskVoid PollHudAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var session = _session;
                    var vm = _viewModel;
                    if (session != null && vm != null)
                    {
                        var current = session.CurrentCellIdentity ?? "(waiting)";
                        var residents = session.ResidentCellIdentities;
                        var loaded = residents.Count == 0
                            ? "-"
                            : string.Join(", ", residents);
                        var children = session.LoadedChildSceneIdentities;
                        var loadedChildren = children.Count == 0
                            ? "-"
                            : string.Join(", ", children);
                        vm.SetStreamingState(
                            current,
                            loaded,
                            loadedChildren,
                            isBusy: !session.IsStreamingActive);

                        if (session.Flight != null)
                        {
                            vm.SetPosition(session.Flight.Position);
                        }
                    }

                    await UniTask.Delay(100, cancellationToken: ct);
                }
            }
            catch (OperationCanceledException)
            {
                // teardown
            }
        }
    }
}
