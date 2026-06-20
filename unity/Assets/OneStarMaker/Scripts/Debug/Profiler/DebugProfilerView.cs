#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OneStarMaker.Foundation.Telemetry;
using OneStarMaker.Runtime;
using OneStarMaker.Runtime.UISystem;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ZLogger;


namespace OneStarMaker.Debug
{
    /// <summary>
    /// Debug レイヤーに配置する FPS / フレームタイム プロファイラ UIView。
    /// 画面左上にグラフ + 数値テキストを表示する。
    /// </summary>
    public sealed class DebugProfilerView : UIView
    {
        // ── 定数 ──
        private const int GraphWidth = 300;
        private const int GraphHeight = 80;
        private const int SamplerCapacity = 300;

        // ── 内部コンポーネント（Awake で自動生成） ──
        private FrameTimeSampler _sampler = null!;
        private FrameTimeGraphRenderer _graphRenderer = null!;
        private RawImage _graphImage = null!;
        private TextMeshProUGUI _textDisplay = null!;
        private TextMeshProUGUI _warningDisplay = null!;
        private ILogger<DebugProfilerView> _logger = null!;

        // ── GC スパイク検出 ──
        private int _lastGcCount;

        // ── UI 描画コスト計測 ──
        private ProfilerUiCostCollector _uiCostCollector = null!;

        // ── 警告行管理 ──
        private const float WarningDisplayDuration = 5f;
        private const int MaxWarningLines = 4;
        private string?[] _warningLines = null!;
        private float[] _warningTimers = null!;

        // ── 公開 ──

        public override UILayer GetUILayer() => UILayer.Debug;

        /// <summary>
        /// ロガーを外部から注入する。注入しない場合は <see cref="NullLogger{TCategoryName}"/> が使われる。
        /// </summary>
        public void Initialize(ILogger<DebugProfilerView>? logger = null)
        {
            _logger = logger;
        }

        // ── Unity ライフサイクル ──

        private void Awake()
        {
            _sampler = new FrameTimeSampler(SamplerCapacity);
            _graphRenderer = new FrameTimeGraphRenderer(GraphWidth, GraphHeight);
            _logger ??= new NullLogger<DebugProfilerView>();

            // GC ベースライン
            _lastGcCount = GC.CollectionCount(0);

            // UI 描画コスト collector。
            // View は snapshot を読むだけにして、recorder の寿命管理を分離する。
            _uiCostCollector = new ProfilerUiCostCollector();

            // 警告行バッファ
            _warningLines = new string?[MaxWarningLines];
            _warningTimers = new float[MaxWarningLines];

            // ボトルネック通知購読（Foundation 層の静的イベント）
            AppTelemetry.AlertStream.AlertRaised += OnBottleneckDetected;

            BuildUI();
        }

        private void Update()
        {
            _sampler.Sample();
            _graphRenderer.Render(_sampler);
            _graphImage.texture = _graphRenderer.Texture;

            // テキスト更新（毎フレーム最新値 + サマリ）— ZString.Format でアロケーション最小化
            _sampler.GetSample(_sampler.Count - 1, out float cpuMs, out float gpuMs);
            float fps = cpuMs > 0f ? 1000f / cpuMs : 0f;

            UpdateProfilerText(fps, cpuMs, gpuMs);

            // ── GC スパイク検出（毎フレーム）──
            DetectGcSpike();

            // ── UI 描画コスト検出 ──
            DetectUiCost();

            // ── 警告テキスト更新 ──
            UpdateWarningDisplay();

            // ── 1 秒サマリログ ──
            if (_sampler.SummaryUpdated)
            {
                _sampler.SummaryUpdated = false;
                LogSummary();
            }
        }

        private void OnDestroy()
        {
            AppTelemetry.AlertStream.AlertRaised -= OnBottleneckDetected;
            _uiCostCollector.Dispose();
            _graphRenderer?.Dispose();
        }

        // ── UIView ──

        public override UniTask ViewIn(CancellationToken ct)
        {
            gameObject.SetActive(true);
            return UniTask.CompletedTask;
        }

        public override UniTask ViewOut()
        {
            gameObject.SetActive(false);
            return UniTask.CompletedTask;
        }

        // ── UI 構築 ──

        private void BuildUI()
        {
            // ルート RectTransform（左上アンカー）
            var rt = GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(8f, -8f);
            rt.sizeDelta = new Vector2(GraphWidth + 16f, GraphHeight + 90f); // +60f for warning lines

            // 背景パネル
            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.6f);
            bg.raycastTarget = false;

            // グラフ用 RawImage
            var graphGo = new GameObject("Graph", typeof(RectTransform), typeof(RawImage));
            graphGo.transform.SetParent(transform, false);
            _graphImage = graphGo.GetComponent<RawImage>();
            _graphImage.raycastTarget = false;
            var graphRt = graphGo.GetComponent<RectTransform>();
            graphRt.anchorMin = new Vector2(0f, 1f);
            graphRt.anchorMax = new Vector2(0f, 1f);
            graphRt.pivot = new Vector2(0f, 1f);
            graphRt.anchoredPosition = new Vector2(8f, -4f);
            graphRt.sizeDelta = new Vector2(GraphWidth, GraphHeight);

            // テキスト
            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(transform, false);
            _textDisplay = textGo.GetComponent<TextMeshProUGUI>();
            _textDisplay.fontSize = 12f;
            _textDisplay.color = Color.white;
            _textDisplay.alignment = TextAlignmentOptions.BottomLeft;
            _textDisplay.raycastTarget = false;
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0f, 0f);
            textRt.anchorMax = new Vector2(1f, 0f);
            textRt.pivot = new Vector2(0f, 0f);
            textRt.anchoredPosition = new Vector2(8f, 2f);
            textRt.sizeDelta = new Vector2(-16f, 22f);

            // 警告テキスト（メインテキストの上に配置）
            var warnGo = new GameObject("Warnings", typeof(RectTransform), typeof(TextMeshProUGUI));
            warnGo.transform.SetParent(transform, false);
            _warningDisplay = warnGo.GetComponent<TextMeshProUGUI>();
            _warningDisplay.fontSize = 10f;
            _warningDisplay.color = new Color(1f, 0.9f, 0.2f); // 警告色（黄）
            _warningDisplay.alignment = TextAlignmentOptions.BottomLeft;
            _warningDisplay.raycastTarget = false;
            _warningDisplay.enableWordWrapping = false;
            _warningDisplay.overflowMode = TextOverflowModes.Truncate;
            var warnRt = warnGo.GetComponent<RectTransform>();
            warnRt.anchorMin = new Vector2(0f, 0f);
            warnRt.anchorMax = new Vector2(1f, 0f);
            warnRt.pivot = new Vector2(0f, 0f);
            warnRt.anchoredPosition = new Vector2(8f, 24f);
            warnRt.sizeDelta = new Vector2(-16f, 60f);
            _warningDisplay.text = string.Empty;
        }

        // ── ログ出力 ──

        private void LogSummary()
        {
            float avgFps = _sampler.CpuAvgMs > 0f ? 1000f / _sampler.CpuAvgMs : 0f;

            // ZString でゼロアロケーション文字列構築
            var msg = _sampler.IsGpuTimingAvailable
                ? ZString.Format(
                    "[Profiler] FPS={0:F0} CPU avg={1:F2}ms min={2:F2}ms max={3:F2}ms GPU avg={4:F2}ms min={5:F2}ms max={6:F2}ms",
                    avgFps, _sampler.CpuAvgMs, _sampler.CpuMinMs, _sampler.CpuMaxMs,
                    _sampler.GpuAvgMs, _sampler.GpuMinMs, _sampler.GpuMaxMs)
                : ZString.Format(
                    "[Profiler] FPS={0:F0} CPU avg={1:F2}ms min={2:F2}ms max={3:F2}ms GPU=N/A",
                    avgFps, _sampler.CpuAvgMs, _sampler.CpuMinMs, _sampler.CpuMaxMs);

            _logger.LogInformation(msg);

            // ── テレメトリ: 1秒サマリをレコードとして出力 ──
            if (AppTelemetry.IsEnabled)
            {
                WriteProfilerTelemetry(
                    Foundation.Core.TelemetryStartType.ProfilerSummary,
                    tags: RuntimeTelemetryMetadataFactory.ClassifyFrameRate(avgFps),
                    level: TelemetryLevel.Verbose);
            }
        }

        /// <summary>
        /// 毎フレームの profiler 表示は TMP の数値フォーマット API を優先し、
        /// 中間 string の生成を減らす。
        /// anomaly 時の warning 文言と違い、ここは常時通る hot path なので
        /// 「読みやすさ」より allocation 削減を優先する。
        /// </summary>
        private void UpdateProfilerText(float fps, float cpuMs, float gpuMs)
        {
            if (_sampler.IsGpuTimingAvailable)
            {
                _textDisplay.SetText("FPS: {0:0}  CPU: {1:0.0}ms  GPU: {2:0.0}ms", fps, cpuMs, gpuMs);
                return;
            }

            _textDisplay.SetText("FPS: {0:0}  CPU: {1:0.0}ms  GPU: N/A", fps, cpuMs);
        }

        // ── ボトルネック検出 ──

        /// <summary>
        /// 毎フレーム GC.CollectionCount(0) の差分を取り、GC スパイクを検出する。
        /// GC.CollectionCount はゼロアロケーション呼び出しのため毎フレームのコストは無視できる。
        /// </summary>
        private void DetectGcSpike()
        {
            int gcCount = GC.CollectionCount(0);
            int gcDelta = gcCount - _lastGcCount;
            _lastGcCount = gcCount;

            if (gcDelta <= 0) return;
            if (AppTelemetry.Thresholds is not { } th) return;
            if (gcDelta <= th.GcPerFrame) return;

            var sceneName = SceneManager.GetActiveScene().name;
            var msg = ZString.Format(
                "[\u26a0 GC] {0} collections @ frame {1} ({2})",
                gcDelta, Time.frameCount, sceneName);

            PushWarning(msg);
            _logger.LogWarning(
                ZString.Format(
                    "[Telemetry] GC spike: {0} collections in frame {1} (scene: {2})",
                    gcDelta, Time.frameCount, sceneName));

            // テレメトリレコード出力
            if (AppTelemetry.IsEnabled)
            {
                WriteProfilerTelemetry(
                    Foundation.Core.TelemetryStartType.GcSpike,
                    Foundation.Core.TelemetryTagType.AllocSpike | Foundation.Core.TelemetryTagType.Bottleneck,
                    TelemetryLevel.Summary);
            }
        }

        /// <summary>
        /// Canvas Rebuild 回数とバッチ数を ProfilerRecorder で取得し、閾値超過を検出する。
        /// ProfilerRecorder は Development Build / Editor でのみ有効。
        /// </summary>
        private void DetectUiCost()
        {
            if (AppTelemetry.Thresholds is not { } th) return;
            var snapshot = _uiCostCollector.Capture();
            if (!snapshot.IsAvailable) return;

            long rebuilds = snapshot.CanvasRebuildCount;
            long batches = snapshot.BatchCount;

            if (rebuilds <= th.CanvasRebuildPerFrame && batches <= th.BatchCount) return;

            var msg = ZString.Format(
                "[\u26a0 UI] {0} rebuilds, {1} batches",
                rebuilds, batches);
            PushWarning(msg);

            // テレメトリレコード出力
            if (AppTelemetry.IsEnabled)
            {
                WriteProfilerTelemetry(
                    Foundation.Core.TelemetryStartType.UiCost,
                    Foundation.Core.TelemetryTagType.Bottleneck,
                    TelemetryLevel.Summary);
            }
        }

        /// <summary>
        /// DebugProfilerView が持っているサンプラ値だけで telemetry record を組み立てる。
        /// 毎フレームでも追加ヒープ確保を増やさないため、DTO 化せず struct をその場で書き出す。
        /// </summary>
        private void WriteProfilerTelemetry(
            Foundation.Core.TelemetryStartType startType,
            Foundation.Core.TelemetryTagType? tags,
            TelemetryLevel level)
        {
            var now = DateTime.UtcNow.Ticks;
            var metadata = RuntimeTelemetryMetadataFactory.CreateProfilerMetadata(
                cpuTime: _sampler.CpuAvgMs,
                gpuTime: _sampler.IsGpuTimingAvailable ? _sampler.GpuAvgMs : 0f);

            var telemetryRecord = new TelemetryRecord(
                traceId: AppTelemetry.GenerateId(),
                spanId: AppTelemetry.GenerateId(),
                parentSpanId: 0,
                name: startType,
                startTimestampUtcTicks: now,
                endTimestampUtcTicks: now,
                elapsedMs: 0,
                isSuccess: true,
                tags: tags,
                level: level,
                metadata: metadata);

            AppTelemetry.WriteRecord(telemetryRecord);
        }

        // ── 警告表示管理 ──

        /// <summary>
        /// AppTelemetry.OnBottleneckDetected から呼ばれるコールバック。
        /// Runtime 層（SceneDirector 等）からのボトルネック通知を受け取り、画面に表示する。
        /// </summary>
        private void OnBottleneckDetected(string message)
        {
            PushWarning(message);
        }

        /// <summary>
        /// 警告行をリングバッファに追加する。
        /// </summary>
        private void PushWarning(string message)
        {
            // 空きスロットを探す。なければ最も古い（タイマー最小）を上書き
            int targetIdx = 0;
            float minTimer = float.MaxValue;
            for (int i = 0; i < MaxWarningLines; i++)
            {
                if (_warningLines[i] == null || _warningTimers[i] <= 0f)
                {
                    targetIdx = i;
                    break;
                }
                if (_warningTimers[i] < minTimer)
                {
                    minTimer = _warningTimers[i];
                    targetIdx = i;
                }
            }

            _warningLines[targetIdx] = message;
            _warningTimers[targetIdx] = WarningDisplayDuration;
        }

        /// <summary>
        /// 警告テキストのタイマーを減算し、表示テキストを更新する。
        /// 警告がなければ空文字をセットする。
        /// </summary>
        private void UpdateWarningDisplay()
        {
            bool hasAny = false;
            var sb = ZString.CreateStringBuilder();
            try
            {
                for (int i = 0; i < MaxWarningLines; i++)
                {
                    if (_warningLines[i] == null || _warningTimers[i] <= 0f)
                    {
                        _warningLines[i] = null;
                        continue;
                    }

                    _warningTimers[i] -= Time.deltaTime;
                    if (_warningTimers[i] <= 0f)
                    {
                        _warningLines[i] = null;
                        continue;
                    }

                    if (hasAny) sb.Append('\n');
                    sb.Append(_warningLines[i]);
                    hasAny = true;
                }

                _warningDisplay.text = hasAny ? sb.ToString() : string.Empty;
            }
            finally
            {
                sb.Dispose();
            }
        }
    }

    /// <summary>
    /// UI コスト監視用の ProfilerRecorder を束ねる collector。
    ///
    /// <para>
    /// recorder の開始/停止責務を View から切り離し、
    /// 表示ロジックは軽量 snapshot の解釈だけに留める。
    /// </para>
    /// </summary>
    internal sealed class ProfilerUiCostCollector : IDisposable
    {
        private ProfilerRecorder _canvasRebuildRecorder;
        private ProfilerRecorder _batchCountRecorder;

        public ProfilerUiCostCollector()
        {
            _canvasRebuildRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "UI.Canvas.RebuildBatchedCount");
            _batchCountRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "UI.Canvas.BatchCount");
        }

        public ProfilerUiCostSnapshot Capture()
        {
            return new ProfilerUiCostSnapshot(
                isAvailable: _canvasRebuildRecorder.Valid || _batchCountRecorder.Valid,
                canvasRebuildCount: _canvasRebuildRecorder.Valid ? _canvasRebuildRecorder.LastValue : 0,
                batchCount: _batchCountRecorder.Valid ? _batchCountRecorder.LastValue : 0);
        }

        public void Dispose()
        {
            _canvasRebuildRecorder.Dispose();
            _batchCountRecorder.Dispose();
        }
    }

    /// <summary>
    /// UI コスト collector から取得する軽量 snapshot。
    /// 値型にして毎フレーム取得でもヒープ確保を増やさない。
    /// </summary>
    internal readonly struct ProfilerUiCostSnapshot
    {
        public readonly bool IsAvailable;
        public readonly long CanvasRebuildCount;
        public readonly long BatchCount;

        public ProfilerUiCostSnapshot(bool isAvailable, long canvasRebuildCount, long batchCount)
        {
            IsAvailable = isAvailable;
            CanvasRebuildCount = canvasRebuildCount;
            BatchCount = batchCount;
        }
    }
}
