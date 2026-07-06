#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Foundation.Telemetry;
using OneStarMaker.Runtime.SceneSystem;

namespace OneStarMaker.Runtime.Streaming
{
    /// <summary>
    /// SceneDirector を差し替え可能な境界（<see cref="ISceneStreamingBackend"/>）へ接続する本実装。
    /// WorldStreamingController からの RequestAdd / RequestRemove / IsLoaded を
    /// SceneDirector の AddScene / UnloadScene / ライフサイクル観測へ委譲する。
    /// </summary>
    public sealed class SceneDirectorStreamingBackend : ISceneStreamingBackend
    {
        private readonly SceneDirector _sceneDirector;

        /// <summary>
        /// SceneDirectorStreamingBackend を構築する。
        /// </summary>
        /// <param name="sceneDirector">委譲先の SceneDirector。</param>
        public SceneDirectorStreamingBackend(SceneDirector sceneDirector)
        {
            _sceneDirector = sceneDirector ?? throw new ArgumentNullException(nameof(sceneDirector));
        }

        /// <inheritdoc />
        /// <remarks>
        /// <see cref="SceneDirector.AddScene"/> へ委譲する。
        /// セルは R-4 で <see cref="LoadingDisplayType.None"/>、H-3 で <see cref="TelemetryLevel.Verbose"/> を指定する。
        /// 完了は Stable 到達を保証しない（G-6）。例外・OCE は呼び出し側が観測する。
        /// </remarks>
        public UniTask RequestAdd(string cellId, int priority)
        {
            return _sceneDirector.AddScene(
                cellId,
                afterOnLoadedTask: null,
                ct: CancellationToken.None,
                loadingDisplay: LoadingDisplayType.None,
                priority: priority,
                telemetryLevel: TelemetryLevel.Verbose);
        }

        /// <inheritdoc />
        /// <remarks>
        /// <see cref="SceneDirector.UnloadScene"/> へ委譲する（H-3: <see cref="TelemetryLevel.Verbose"/>）。
        /// 窓内キャンセル・保留アンロードの収束は SceneDirector 側が担う。
        /// </remarks>
        public UniTask RequestRemove(string cellId)
        {
            return _sceneDirector.UnloadScene(cellId, telemetryLevel: TelemetryLevel.Verbose);
        }

        /// <inheritdoc />
        /// <remarks>
        /// セルが <see cref="SceneState.Stable"/> に到達した場合のみ <see langword="true"/> を返す。
        /// Loading / PreLoading / Initializing / アンロード中 / 未登録は <see langword="false"/>。
        /// <see cref="ISceneQuery.IsSceneLoaded"/>（Loading 中も true）は使用しない。
        /// </remarks>
        public bool IsLoaded(string cellId)
        {
            var scene = _sceneDirector.GetLoadedScene(cellId);
            return scene != null && scene.Lifecycle.State == SceneState.Stable;
        }
    }
}
