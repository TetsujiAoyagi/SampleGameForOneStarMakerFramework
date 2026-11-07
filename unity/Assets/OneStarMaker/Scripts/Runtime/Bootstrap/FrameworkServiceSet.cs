#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Foundation.Logging;
using OneStarMaker.Foundation.Telemetry;
using OneStarMaker.Runtime.DebugSocketServices;
using OneStarMaker.Runtime.Rendering.Vfx;
using OneStarMaker.Runtime.UpdateSystem.Hosting;
using UnityEngine;

namespace OneStarMaker.Runtime
{
    /// <summary>
    /// Framework が App 寿命で持つ常駐サービス（logger factory / DebugSocket / UpdateSystemHost / VfxRuntimeHost）の集合と、
    /// その開始・停止の順序。
    /// <para>
    /// 生成は initializer の <c>Create*</c> hook が行い、この集合は受け取って持つだけにする（派生クラスが override する場所を変えない）。
    /// initializer は BeforeSceneLoad の最初の <c>Create*</c> より前に空のインスタンスを作って field に入れ、hook の戻り値を
    /// 1 つ作るたびにここへ入れる。こうすると (1) 後の hook が前に作ったサービスを protected accessor で読め、
    /// (2) 途中の hook が投げても、作り済みのサービスは ReleaseAll の <see cref="Dispose"/> で破棄される。
    /// 全部揃ってから入れる形にすると、どちらも成り立たない。
    /// </para>
    /// <para>
    /// BeforeSceneLoad ごとに作り直し、使い回さない。DebugSocket の telemetry sink を一度だけ登録した印もインスタンスが持つ。
    /// <c>AbstractApplicationInitializer.ActiveVfxRuntimeHost</c>（テスト用の static）の設定・解除は setter が initializer に
    /// あるので initializer が行う。解除は <see cref="Dispose"/> より前。
    /// </para>
    /// </summary>
    internal sealed class FrameworkServiceSet
    {
        private bool _debugSocketTelemetrySinkRegistered;

        internal AppLoggerFactory? LoggerFactory { get; set; }

        internal DebugSocketService? DebugSocket { get; set; }

        internal UpdateSystemHost? UpdateSystemHost { get; set; }

        internal VfxRuntimeHost? Vfx { get; set; }

        /// <summary>
        /// Framework が標準で持つサービス群を起動する。
        /// 今回は DebugSocketService だけだが、将来の長寿命サービスもここへ集約できる。
        /// </summary>
        internal async UniTask StartAsync(CancellationToken ct)
        {
            if (DebugSocket == null)
            {
                Debug.LogWarning("[AppInit] DebugSocket は無効です。DebugStudio 接続を受けるには debugSocket:enabled=true が必要です。");
                return;
            }

            Debug.Log(
                $"[AppInit] Starting DebugSocket transport. mode={DebugSocket.Options.TransportMode}, endpoint={DebugSocket.Options.EndpointDisplayName}, autoStart={DebugSocket.Options.AutoStart}.");
            await DebugSocket.StartAsync(ct);
            // await の間に ReleaseAll が走ると DebugSocket は null になる。initializer の field を読んでいたときと同じ挙動に
            // するため、局所変数に退避せず都度プロパティを読み直す（退避すると、破棄済みの service へ sink を登録しうる）。
            Debug.Log(
                $"[AppInit] DebugSocket transport start sequence finished. mode={DebugSocket!.Options.TransportMode}, isRunning={DebugSocket.IsRunning}.");

            // Telemetry は AppTelemetry から流れてくるため、
            // socket service へ橋渡しする sink を一度だけ登録する。
            if (DebugSocket.Options.SendTelemetry && !_debugSocketTelemetrySinkRegistered)
            {
                AppTelemetry.AddSink(DebugSocket.CreateTelemetrySink());
                _debugSocketTelemetrySinkRegistered = true;
            }
        }

        /// <summary>
        /// VFX → DebugSocket → sink の登録印 → UpdateSystemHost → logger factory の順に破棄する。未設定のものは飛ばし、2 回呼んでも安全。
        /// <para>
        /// VFX が先なのは、回収時の診断ログを出せ、生きている coordinator から登録解除でき、source asset が instance の破棄要求より
        /// 後に解放されるため（logger・UpdateSystemHost・SceneDirector・AssetManagement より前）。
        /// 破棄の例外はここで握らない。initializer の ReleaseAll にあったときから try/catch は無く、後続を破棄しないまま呼び出し元へ出る。
        /// 移動で挙動を変えないため、ここでも足さない。
        /// </para>
        /// </summary>
        internal void Dispose()
        {
            // 破棄中に Vfx accessor から見えないよう、先に参照を外してから Dispose する。
            var vfx = Vfx;
            Vfx = null;
            vfx?.Dispose();

            DebugSocket?.Dispose();
            DebugSocket = null;
            _debugSocketTelemetrySinkRegistered = false;

            UpdateSystemHost?.Dispose();
            UpdateSystemHost = null;

            LoggerFactory?.Dispose();
            LoggerFactory = null;
        }
    }
}
