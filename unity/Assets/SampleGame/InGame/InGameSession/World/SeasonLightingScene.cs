#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.Rendering.Environments;
using OneStarMaker.Runtime.SceneSystem;
using UnityEngine;
using ZLogger;

namespace SampleGame.InGame.World
{
    /// <summary>
    /// 季節 Lighting の寿命で RenderEnvironment の lease を持つ。
    /// 太陽探索と preset lookup に失敗したら lease を取らない。
    /// OnLoadedImpl の例外は PreUnLoad を通らないため、Acquire 後の失敗はここで返す。
    /// </summary>
    public sealed class SeasonLightingScene : SceneBase
    {
        private readonly ILogger<SeasonLightingScene> _logger;
        private readonly IRenderEnvironment _renderEnvironment;
        private RenderEnvironmentLease? _lease;

        public SeasonLightingScene(
            SceneResource sceneResource,
            ISceneQuery sceneQuery,
            ISceneController sceneController,
            ILoggerFactory loggerFactory,
            IRenderEnvironment renderEnvironment)
            : base(sceneResource, sceneQuery, sceneController)
        {
            _logger = loggerFactory.CreateLogger<SeasonLightingScene>();
            _renderEnvironment = renderEnvironment
                ?? throw new ArgumentNullException(nameof(renderEnvironment));
            _logger.ZLogInformation($"Create SeasonLightingScene {sceneResource.Identity}");
        }

        protected override UniTask OnLoadedImpl(CancellationToken ct)
        {
            var sun = FindSeasonSun();
            if (!SeasonLightingPresetTable.TryGet(SceneResource.Identity, out var state))
            {
                throw new InvalidOperationException(
                    $"Unknown season lighting identity: {SceneResource.Identity}.");
            }

            _lease = _renderEnvironment.Acquire(this);
            try
            {
                _lease.BindSun(sun);
                _lease.Apply(state);
            }
            catch
            {
                _lease.Dispose();
                _lease = null;
                throw;
            }

            return UniTask.CompletedTask;
        }

        protected override UniTask OnStabledImpl()
        {
            _logger.ZLogInformation($"OnStabledImpl SeasonLightingScene {SceneResource.Identity}");
            return UniTask.CompletedTask;
        }

        protected override UniTask OnPreUnLoadedImpl()
        {
            if (_lease != null)
            {
                _lease.Dispose();
                _lease = null;
            }

            return UniTask.CompletedTask;
        }

        protected override UniTask OnAfterUnLoadedImpl() => UniTask.CompletedTask;

        private Light FindSeasonSun()
        {
            foreach (var root in RootObjects)
            {
                if (root == null)
                {
                    continue;
                }

                var lights = root.GetComponentsInChildren<Light>(true);
                foreach (var light in lights)
                {
                    if (light == null)
                    {
                        continue;
                    }

                    if (light.gameObject.name != "SeasonSun")
                    {
                        continue;
                    }

                    if (light.type != LightType.Directional)
                    {
                        continue;
                    }

                    return light;
                }
            }

            throw new InvalidOperationException(
                $"SeasonSun Directional Light was not found in {SceneResource.Identity}.");
        }
    }
}
