#nullable enable

using System;
using OneStarMaker.Foundation.Core;
using OneStarMaker.Foundation.DebugSocket;

namespace OneStarMaker.Foundation.Telemetry
{
    /// <summary>
    /// 旧フラット metadata（Contract v3 では deprecated）。
    /// 段階移行（案 A）のため wire / 既存 producer 互換として残す。
    /// 新規の意味づけは <see cref="TelemetryPayload"/> 側へ置くこと。
    /// </summary>
    /// <remarks>
    /// Scene/Memory フィールドと Camera フィールドは用途が異なり、混在させない。
    /// 未設定の 0 / -1 センチネルは旧契約の名残であり、v3 消費者は Payload の省略を正とする。
    /// </remarks>
    public readonly struct Metadata
    {
        // 値を持たせたいタグに対応する追加フィールド
        public readonly float CpuTime;        // CpuTimeOver
        public readonly float GpuTime;        // GpuTimeOver
        public readonly long ManagedMem;      // ManagedMemoryOver
        public readonly long NativeMem;       // NativeMemoryOver
        public readonly int SceneFrom;     // SceneTransition 等
        public readonly int SceneTo;

        /// <summary>CameraSystem snapshot: 管理中 View 総数。-1 = 未設定。</summary>
        public readonly int CameraTotalViewCount;

        /// <summary>CameraSystem snapshot: MainView を除く追加 View 数。-1 = 未設定。</summary>
        public readonly int CameraAdditionalViewCount;

        /// <summary>CameraSystem snapshot: ブレンド中 View 数。-1 = 未設定。</summary>
        public readonly int CameraBlendingViewCount;

        /// <summary>CameraSystem snapshot: 全 View の最大スタック深度。-1 = 未設定。</summary>
        public readonly int CameraMaxStackDepthTotal;

        /// <summary>CameraSwitch span: 対象 ViewId。-1 = 未設定。</summary>
        public readonly int CameraViewId;

        /// <summary>CameraSwitch span: ActiveCamera.Id の決定的 FNV-1a 32bit hash。-1 = 未設定。</summary>
        public readonly int CameraActiveCameraHash;

        public Metadata(
            float cpuTime = 0,
            float gpuTime = 0,
            long managedMem = 0,
            long nativeMem = 0,
            int sceneFrom = -1,
            int sceneTo = -1,
            int cameraTotalViewCount = -1,
            int cameraAdditionalViewCount = -1,
            int cameraBlendingViewCount = -1,
            int cameraMaxStackDepthTotal = -1,
            int cameraViewId = -1,
            int cameraActiveCameraHash = -1)
        {
            CpuTime = cpuTime;
            GpuTime = gpuTime;
            ManagedMem = managedMem;
            NativeMem = nativeMem;
            SceneFrom = sceneFrom;
            SceneTo = sceneTo;
            CameraTotalViewCount = cameraTotalViewCount;
            CameraAdditionalViewCount = cameraAdditionalViewCount;
            CameraBlendingViewCount = cameraBlendingViewCount;
            CameraMaxStackDepthTotal = cameraMaxStackDepthTotal;
            CameraViewId = cameraViewId;
            CameraActiveCameraHash = cameraActiveCameraHash;
        }
    }

    /// <summary>
    /// ワークロード固有の軽量スパン。GCAlloc を最小化するため struct で実装。
    /// 実際の ID は16進文字列で保持し、ToString 呼び出しは必要なときのみ。
    /// </summary>
    public readonly struct TelemetrySpan
    {
        public long TraceId { get; }
        public long SpanId { get; }
        public long ParentSpanId { get; }
        public TelemetryStartType Name { get; }
        internal long StartTimestampUtcTicks { get; }

        /// <summary>span 開始時に観測した player-loop frame。非 main thread では null。</summary>
        internal int? UnityFrameAtStart { get; }

        public TelemetryTagType? Tags { get; }

        public string NameString => Name.ToStartTypeString();
        internal TelemetrySpan(long traceId, 
                               long spanId,
                               long parentSpanId,
                               TelemetryStartType name,
                               TelemetryTagType? tags,
                               int? unityFrameAtStart)
        {
            TraceId = traceId;
            SpanId = spanId;
            ParentSpanId = parentSpanId;
            Name = name;
            Tags = tags;
            UnityFrameAtStart = unityFrameAtStart;
            StartTimestampUtcTicks = DateTime.UtcNow.Ticks;
        }
    }

    /// <summary>
    /// テレメトリ基盤。OneStarMaker 用に簡素化した Trace/Span 管理を行う。
    /// </summary>
    public static class AppTelemetry
    {
        private static readonly TelemetrySpanContext s_spanContext = new();
        private static readonly TelemetrySinkRegistry s_sinkRegistry = new();
        private static readonly TelemetryAlertNotifier s_alertNotifier = new();

        /// <summary>現在のトレース ID。スパン不在時は null。</summary>
        public static long? CurrentTraceId => s_spanContext.CurrentTraceId;
        /// <summary>現在のスパン ID。スパン不在時は null。</summary>
        public static long? CurrentSpanId => s_spanContext.CurrentSpanId;

        // ─── Configuration ───

        /// <summary>
        /// テレメトリ出力レベル。
        /// <see cref="TelemetryLevel.Verbose"/> で全フェーズ出力、
        /// <see cref="TelemetryLevel.Summary"/> で遷移完了サマリのみ、
        /// <see cref="TelemetryLevel.Off"/> で無効。
        /// </summary>
        public static TelemetryLevel Level { get; set; } = TelemetryLevel.Verbose;

        /// <summary>
        /// テレメトリが有効か。
        /// </summary>
        public static bool IsEnabled => Level != TelemetryLevel.Off;

        /// <summary>
        /// ボトルネック検出閾値。<see cref="Config.AppConfig"/> から読み込む。
        /// null の場合は閾値チェックを行わない。
        /// </summary>
        public static TelemetryThresholds? Thresholds
        {
            get => s_alertNotifier.Thresholds;
            set => s_alertNotifier.Thresholds = value;
        }

        // ─── Bottleneck Notification ───

        /// <summary>
        /// ボトルネック検出時に発火するイベント。
        /// Debug 層の <c>DebugProfilerView</c> が購読し、画面上に警告を表示する。
        /// Foundation → Runtime/Debug の単方向依存を維持するため、静的イベントを使用する。
        /// </summary>
        public static TelemetryAlertStream AlertStream => s_alertNotifier.AlertStream;

        /// <summary>
        /// 旧来互換のイベント面。
        /// 実体は <see cref="TelemetryAlertStream"/> 側で持ち、
        /// `AppTelemetry` はその façade だけを残す。
        /// </summary>
        public static event Action<string>? OnBottleneckDetected
        {
            add => s_alertNotifier.AlertStream.AlertRaised += value;
            remove => s_alertNotifier.AlertStream.AlertRaised -= value;
        }

        /// <summary>
        /// ボトルネック検出を通知する。
        /// </summary>
        /// <param name="message">警告メッセージ。</param>
        public static void NotifyBottleneck(string message)
        {
            s_alertNotifier.Notify(message);
        }

        // ─── Sink Management ───

        /// <summary>
        /// Sink を登録する。複数登録可。
        /// 追加時にスナップショットを再生成する（コピーオンライト）。
        /// </summary>
        public static void AddSink(ITelemetrySink sink)
        {
            s_sinkRegistry.AddSink(sink);
        }

        /// <summary>
        /// Sink を登録解除する。
        /// 削除時にスナップショットを再生成する（コピーオンライト）。
        /// </summary>
        public static void RemoveSink(ITelemetrySink sink)
        {
            s_sinkRegistry.RemoveSink(sink);
        }

        /// <summary>
        /// 全 Sink をフラッシュして登録解除する。アプリ終了時に呼ぶ。
        /// </summary>
        public static void Shutdown()
        {
            s_sinkRegistry.FlushAndClear();
            s_alertNotifier.Reset();
        }

        // ─── Span Lifecycle ───

        /// <summary>
        /// 新しいスパンを開始する。トレース ID は自動生成される。
        /// テレメトリが Off の場合は null を返す。
        /// </summary>
        /// <param name="name">スパン名（例: "SwitchScene", "Scene.PreLoad"）。</param>
        /// <returns>開始された <see cref="TelemetrySpan"/>。テレメトリ Off 時は null。</returns>
        public static TelemetrySpan? StartSpan(TelemetryStartType name, TelemetryTagType? tags)
        {
            if (!IsEnabled) return null;
            return s_spanContext.StartRootSpan(name, tags);
        }

        /// <summary>
        /// 親スパンを明示指定して子スパンを開始する。
        /// </summary>
        /// <param name="name">スパン名。</param>
        /// <param name="parent">親スパン。</param>
        /// <returns>開始された <see cref="TelemetrySpan"/>。テレメトリ Off 時は null。</returns>
        public static TelemetrySpan? StartChildSpan(TelemetryStartType name, TelemetryTagType? tags, in TelemetrySpan parent)
        {
            if (!IsEnabled) return null;
            return s_spanContext.StartChildSpan(name, tags, parent);
        }

        /// <summary>
        /// スパンを完了し、テレメトリレコードを全 Sink に書き込む。
        /// </summary>
        /// <param name="span">完了するスパン。null の場合は何もしない。</param>
        /// <param name="metadata">旧フラット metadata（段階移行の併記用）。</param>
        /// <param name="isSuccess">成功したか。</param>
        /// <param name="level">このレコードのテレメトリレベル。</param>
        /// <param name="tags">追加タグ。</param>
        /// <param name="payload">Contract v3 の用途固有ペイロード（正本）。未指定なら空。</param>
        /// <remarks>
        /// span 経路の kind は常に <see cref="TelemetryKindRules.InferKind"/> で決める（明示 override 不可）。
        /// kind を明示したい点イベントは <see cref="WriteRecord"/> を使う。
        /// </remarks>
        public static double FinishSpan(
            in TelemetrySpan? span,
            in Metadata metadata,
            bool isSuccess = true,
            TelemetryLevel level = TelemetryLevel.Verbose,
            TelemetryTagType? tags = null,
            in TelemetryPayload payload = default)
        {
            if (!span.HasValue) return 0.0f;
            var s = span.Value;

            var elapsed = (DateTime.UtcNow.Ticks - s.StartTimestampUtcTicks) / (double)TimeSpan.TicksPerMillisecond;
            s_spanContext.ClearIfCurrent(s.SpanId);

            if (level < Level) return elapsed;

            var mergedTags = MergeTags(s, tags);
            if (s_alertNotifier.IsThresholdExceeded(s.Name, elapsed))
            {
                mergedTags = MergeTags(mergedTags, TelemetryTagType.Bottleneck);
            }

            var record = CreateCorrelatedRecord(
                traceId: s.TraceId,
                spanId: s.SpanId,
                parentSpanId: s.ParentSpanId,
                name: s.Name,
                startTimestampUtcTicks: s.StartTimestampUtcTicks,
                endTimestampUtcTicks: DateTime.UtcNow.Ticks,
                elapsedMs: elapsed,
                isSuccess: isSuccess,
                metadata: metadata,
                tags: mergedTags,
                level: level,
                unityFrameAtStart: s.UnityFrameAtStart,
                unityFrameAtEnd: UnityPlayerLoopFrameObservation.TryGetCurrentFrame(),
                kind: TelemetryKindRules.InferKind(s.Name),
                payload: payload);

            s_alertNotifier.CheckThreshold(record, s.Name);
            s_sinkRegistry.Write(record);

            return elapsed;
        }

        /// <summary>
        /// スパンを使わずに直接レコードを書き込む。
        /// Profiler サマリ等、スパンを持たないデータの出力用。
        /// </summary>
        public static void WriteRecord(in TelemetryRecord record)
        {
            if (record.Level < Level) return;

            // WriteRecord 経路は FinishSpan を通らないため、sink に渡す前に producer-owned
            // correlation を必ず検証する。sequence だけが存在しても session が空/別物なら、
            // 過去 record や外部 producer の値を混ぜることになるため安全ではない。
            // frame は worker thread で null が正しい値なので、完全性の判定には含めない。
            // instant record では start/end frame は同一観測点（または null）になる。
            var observedFrame = UnityPlayerLoopFrameObservation.TryGetCurrentFrame();
            var sessionId = UnitySessionCorrelationContext.SessionId;
            var hasCurrentProducerCorrelation =
                record.ProducerSequence > 0 &&
                string.Equals(record.SessionId, sessionId, StringComparison.Ordinal);
            var enriched = hasCurrentProducerCorrelation
                ? record
                : CreateCorrelatedRecord(
                    traceId: record.TraceId,
                    spanId: record.SpanId,
                    parentSpanId: record.ParentSpanId,
                    name: record.Name,
                    startTimestampUtcTicks: record.StartTimestampUtcTicks,
                    endTimestampUtcTicks: record.EndTimestampUtcTicks,
                    elapsedMs: record.ElapsedMs,
                    isSuccess: record.IsSuccess,
                    metadata: record.MetadataValue,
                    tags: record.Tags,
                    level: record.Level,
                    unityFrameAtStart: record.UnityFrameAtStart ?? observedFrame,
                    unityFrameAtEnd: record.UnityFrameAtEnd ?? observedFrame,
                    kind: record.Kind,
                    payload: record.Payload);

            s_sinkRegistry.Write(enriched);
        }

        // ─── Helpers ───

        /// <summary>
        /// スレッドセーフな一意 ID を生成する。アロケーションなし。
        /// </summary>
        public static long GenerateId()
            => s_spanContext.GenerateId();

        private static TelemetryTagType? MergeTags(
            TelemetrySpan span,
            TelemetryTagType? extraTags)
        {
            if (span.Tags == null) return extraTags;
            if (extraTags == null ) return span.Tags;
            return span.Tags | extraTags;
        }

        private static TelemetryTagType? MergeTags(
            TelemetryTagType? currentTags,
            TelemetryTagType extraTag)
        {
            if (currentTags == null)
            {
                return extraTag;
            }

            return currentTags | extraTag;
        }

        private static TelemetryRecord CreateCorrelatedRecord(
            long traceId,
            long spanId,
            long parentSpanId,
            TelemetryStartType name,
            long startTimestampUtcTicks,
            long endTimestampUtcTicks,
            double elapsedMs,
            bool isSuccess,
            Metadata metadata,
            TelemetryTagType? tags,
            TelemetryLevel level,
            int? unityFrameAtStart,
            int? unityFrameAtEnd,
            TelemetryKind? kind = null,
            TelemetryPayload payload = default)
        {
            return new TelemetryRecord(
                traceId: traceId,
                spanId: spanId,
                parentSpanId: parentSpanId,
                name: name,
                startTimestampUtcTicks: startTimestampUtcTicks,
                endTimestampUtcTicks: endTimestampUtcTicks,
                elapsedMs: elapsedMs,
                isSuccess: isSuccess,
                tags: tags,
                level: level,
                metadata: metadata,
                sessionId: UnitySessionCorrelationContext.SessionId,
                producerSequence: UnitySessionCorrelationContext.NextProducerSequence(),
                unityFrameAtStart: unityFrameAtStart,
                unityFrameAtEnd: unityFrameAtEnd,
                kind: kind,
                payload: payload);
        }


        // TelemetrySpanContext に移譲したため、この facade からは採番フィールドを外した。
        // 既存 public API は維持しつつ、責務だけを内側へ分離している。
    }

    /// <summary>
    /// 現在スレッド/非同期文脈に紐づく span 状態と ID 発行を管理する。
    /// </summary>
    internal sealed class TelemetrySpanContext
    {
        private readonly System.Threading.AsyncLocal<TelemetrySpan?> _currentSpan = new();
        private long _idSeed = Environment.TickCount;

        public long? CurrentTraceId => _currentSpan.Value?.TraceId;

        public long? CurrentSpanId => _currentSpan.Value?.SpanId;

        public TelemetrySpan StartRootSpan(TelemetryStartType name, TelemetryTagType? tags)
        {
            var traceId = GenerateId();
            var spanId = GenerateId();
            var span = new TelemetrySpan(
                traceId,
                spanId,
                -1,
                name,
                tags,
                UnityPlayerLoopFrameObservation.TryGetCurrentFrame());
            _currentSpan.Value = span;
            return span;
        }

        public TelemetrySpan StartChildSpan(TelemetryStartType name, TelemetryTagType? tags, in TelemetrySpan parent)
        {
            var spanId = GenerateId();
            var span = new TelemetrySpan(
                parent.TraceId,
                spanId,
                parent.SpanId,
                name,
                tags,
                UnityPlayerLoopFrameObservation.TryGetCurrentFrame());

            // child span は explicit に親を受け取って完結させる。
            // ここで ambient current を child へ差し替えると、
            // Finish 時に parent を復元する追加状態が必要になり、
            // 現行の zero-allocation 前提と衝突しやすい。
            //
            // そのため current は root/外側 span のまま維持し、
            // child は「明示的 parent を持つ独立 record」として扱う。
            // これで nested API を公開したまま、parent 復元漏れで trace が切れる事故を防ぐ。
            return span;
        }

        public void ClearIfCurrent(long spanId)
        {
            if (_currentSpan.Value.HasValue && _currentSpan.Value.Value.SpanId == spanId)
            {
                _currentSpan.Value = null;
            }
        }

        public long GenerateId()
        {
            return System.Threading.Interlocked.Increment(ref _idSeed);
        }
    }

    /// <summary>
    /// telemetry sink の登録/解除/書き込みを一箇所へ閉じ込める registry。
    /// </summary>
    internal sealed class TelemetrySinkRegistry
    {
        private readonly System.Collections.Generic.List<ITelemetrySink> _sinks = new();
        private readonly object _sinkLock = new();
        private volatile ITelemetrySink[] _sinkSnapshot = Array.Empty<ITelemetrySink>();

        public void AddSink(ITelemetrySink sink)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));

            lock (_sinkLock)
            {
                _sinks.Add(sink);
                _sinkSnapshot = _sinks.ToArray();
            }
        }

        public void RemoveSink(ITelemetrySink sink)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));

            lock (_sinkLock)
            {
                _sinks.Remove(sink);
                _sinkSnapshot = _sinks.ToArray();
            }
        }

        public void FlushAndClear()
        {
            lock (_sinkLock)
            {
                foreach (var sink in _sinks)
                {
                    try { sink.Flush(); }
                    catch { /* best effort */ }
                }

                _sinks.Clear();
                _sinkSnapshot = Array.Empty<ITelemetrySink>();
            }
        }

        public void Write(in TelemetryRecord record)
        {
            var snapshot = _sinkSnapshot;
            foreach (var sink in snapshot)
            {
                try
                {
                    sink.Write(record);
                }
                catch
                {
                    // Sink の障害がアプリケーションを巻き込まないようにする
                }
            }
        }
    }

    /// <summary>
    /// 閾値ベースの bottleneck 判定と通知イベントを担当する。
    /// </summary>
    internal sealed class TelemetryAlertNotifier
    {
        public TelemetryThresholds? Thresholds { get; set; }
        public TelemetryAlertStream AlertStream { get; } = new();

        public void Notify(string message)
        {
            AlertStream.Publish(message);
        }

        public void Reset()
        {
            Thresholds = null;
            AlertStream.Reset();
        }

        public void CheckThreshold(in TelemetryRecord record, TelemetryStartType telemetryStartType)
        {
            var threshold = GetThresholdMilliseconds(telemetryStartType);

            if (threshold >= 0.0 && record.ElapsedMs > threshold)
            {
                Notify($"{telemetryStartType} took {record.ElapsedMs:F1} ms (threshold: {threshold} ms)");
            }
        }

        public bool IsThresholdExceeded(TelemetryStartType telemetryStartType, double elapsedMs)
        {
            var threshold = GetThresholdMilliseconds(telemetryStartType);
            return threshold >= 0.0 && elapsedMs > threshold;
        }

        private double GetThresholdMilliseconds(TelemetryStartType telemetryStartType)
        {
            if (Thresholds == null)
            {
                return -1.0;
            }

            return telemetryStartType switch
            {
                TelemetryStartType.SceneTransition => (double)Thresholds.SceneLoadMs,
                TelemetryStartType.AppStartup => (double)Thresholds.AppStartupPhaseMs,
                TelemetryStartType.SceneLoad => (double)Thresholds.SceneLoadMs,
                TelemetryStartType.SceneUnload => (double)Thresholds.SceneLoadMs,
                _ => -1.0,
            };
        }
    }

    /// <summary>
    /// alert 通知だけを扱う専用ストリーム。
    ///
    /// <para>
    /// telemetry record の書き込みとは分離し、
    /// Debug UI 側はこのストリームを購読するだけにする。
    /// </para>
    /// </summary>
    public sealed class TelemetryAlertStream
    {
        public event Action<string>? AlertRaised;

        public void Publish(string message)
        {
            AlertRaised?.Invoke(message);
        }

        public void Reset()
        {
            AlertRaised = null;
        }
    }
}
