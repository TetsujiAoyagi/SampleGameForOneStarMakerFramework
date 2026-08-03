#nullable enable

using System;
using Cysharp.Text;

namespace OneStarMaker.Foundation.Telemetry
{
    /// <summary>
    /// テレメトリの1レコード。
    /// immutable な struct として定義し、GC 負荷を最小化する。
    ///
    /// <para>
    /// Contract v3: 共通エンベロープ（kind / elapsed / tags / session 等）と
    /// 用途固有 <see cref="Payload"/> を分離する。
    /// 旧 <see cref="Metadata"/> は段階移行（案 A）のため併記し、消費者は Payload を正とする。
    /// </para>
    /// </summary>
    public readonly struct TelemetryRecord
    {
        // ─── Trace Context ───

        /// <summary>トレース全体を一意に識別する ID。1 回の SwitchScene 操作等に対応。</summary>
        public long TraceId { get; }

        /// <summary>このレコード自身のスパン ID。</summary>
        public long SpanId { get; }

        /// <summary>親スパンの ID。ルートスパンの場合は空文字。</summary>
        public long ParentSpanId { get; }

        // ─── Content ───

        /// <summary>観測名（StartType）。例: SceneLoad, ProfilerSummary。</summary>
        public Core.TelemetryStartType Name { get; }

        /// <summary>
        /// Contract v3 の観測種別（span / sample / event）。
        /// 「何を測ったか」ではなく「どんな形の観測か」。
        /// </summary>
        public TelemetryKind Kind { get; }

        /// <summary>開始時刻 (UTC ticks)。</summary>
        public long StartTimestampUtcTicks { get; }

        /// <summary>終了時刻 (UTC ticks)。</summary>
        public long EndTimestampUtcTicks { get; }

        /// <summary>
        /// 所要時間 (ms)。
        /// Kind=Span では必須の意味を持つ。Sample では wire 互換のため 0 を置き得るが、
        /// export 側ではキーを省略する（0 を「計測結果」と読ませない）。
        /// </summary>
        public double ElapsedMs { get; }

        /// <summary>成功したか。</summary>
        public bool IsSuccess { get; }

        /// <summary>任意のタグ。シーン ID、遷移元/先、ユーザー操作等。</summary>
        public Core.TelemetryTagType? Tags { get; }

        /// <summary>テレメトリレベル。Sink 側のフィルタリングに使用。</summary>
        public TelemetryLevel Level { get; }

        /// <summary>
        /// 旧フラット metadata（deprecated・段階移行の併記用）。
        /// 新規消費者は <see cref="Payload"/> を読むこと。
        /// </summary>
        public Metadata MetadataValue { get; }

        /// <summary>Contract v3 の用途固有ペイロード（正本）。</summary>
        public TelemetryPayload Payload { get; }

        /// <summary>
        /// Unity 起動単位の session ID。DebugSocket handshake Welcome と同一。
        /// producer が wire 化前に付与し、export 側での後付けは行わない。
        /// </summary>
        public string SessionId { get; }

        /// <summary>
        /// session 内で Log / Telemetry が共有する単調増加 sequence。
        /// 同一 frame 内の全体順序を再構成するための producer 側順序キー。
        /// </summary>
        public long ProducerSequence { get; }

        /// <summary>
        /// span 開始時点の Unity player-loop frame。main thread 以外では null。
        /// async span は複数 frame にまたがるため start/end を分けて保持する。
        /// </summary>
        public int? UnityFrameAtStart { get; }

        /// <summary>
        /// span 終了時点の Unity player-loop frame。main thread 以外では null。
        /// </summary>
        public int? UnityFrameAtEnd { get; }

        public TelemetryRecord(
                long traceId,
                long spanId,
                long parentSpanId,
                Core.TelemetryStartType name,
                long startTimestampUtcTicks,
                long endTimestampUtcTicks,
                double elapsedMs,
                bool isSuccess,
                Core.TelemetryTagType? tags,
                TelemetryLevel level,
                Metadata metadata,
                string sessionId = "",
                long producerSequence = 0,
                int? unityFrameAtStart = null,
                int? unityFrameAtEnd = null,
                TelemetryKind? kind = null,
                TelemetryPayload payload = default)
        {
            TraceId = traceId;
            SpanId = spanId;
            ParentSpanId = parentSpanId;
            Name = name;
            // kind 未指定時は StartType から既定推論（既存呼び出しを壊さない）
            Kind = kind ?? TelemetryKindRules.InferKind(name);
            StartTimestampUtcTicks = startTimestampUtcTicks;
            EndTimestampUtcTicks = endTimestampUtcTicks;
            ElapsedMs = elapsedMs;
            IsSuccess = isSuccess;
            Tags = tags;
            Level = level;
            MetadataValue = metadata;
            Payload = payload;
            SessionId = sessionId ?? string.Empty;
            ProducerSequence = producerSequence;
            UnityFrameAtStart = unityFrameAtStart;
            UnityFrameAtEnd = unityFrameAtEnd;
        }


        /// <summary>
        /// 開始時刻を <see cref="DateTime"/> として返す。
        /// </summary>
        public DateTime StartTimeUtc => new(StartTimestampUtcTicks, DateTimeKind.Utc);

        /// <summary>
        /// 終了時刻を <see cref="DateTime"/> として返す。
        /// </summary>
        public DateTime EndTimeUtc => new(EndTimestampUtcTicks, DateTimeKind.Utc);

        /// <summary>
        /// デバッグ中の簡易表示用文字列。
        /// 永続化 schema や transport payload として使うことは想定しない。
        /// </summary>
        public override string ToString()
            => ZString.Format("[Telemetry] {0}/{1} {2:F1}ms (trace={3}.. span={4}..)",
                Kind.ToWireString(), Name, ElapsedMs,
                TraceId,
                SpanId);
    }
}
