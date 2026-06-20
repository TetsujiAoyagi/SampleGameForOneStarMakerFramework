#nullable enable

using System;
using System.Collections.Generic;
using Cysharp.Text;

namespace OneStarMaker.Foundation.Telemetry
{
    /// <summary>
    /// テレメトリの1レコード。
    /// immutable な struct として定義し、GC 負荷を最小化する。
    /// OpenTelemetry Span モデルに準拠したフィールドを持つ。
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

        /// <summary>スパン名。例: "SwitchScene", "ScenePhase.PreLoad"。</summary>
        public Core.TelemetryStartType Name { get; }

        /// <summary>開始時刻 (UTC ticks)。</summary>
        public long StartTimestampUtcTicks { get; }

        /// <summary>終了時刻 (UTC ticks)。</summary>
        public long EndTimestampUtcTicks { get; }

        /// <summary>所要時間 (ms)。</summary>
        public double ElapsedMs { get; }

        /// <summary>成功したか。</summary>
        public bool IsSuccess { get; }

        /// <summary>任意のタグ。シーン ID、遷移元/先、ユーザー操作等。</summary>
        public Core.TelemetryTagType? Tags { get; }

        /// <summary>テレメトリレベル。Sink 側のフィルタリングに使用。</summary>
        public TelemetryLevel Level { get; }

        public Metadata MetadataValue { get; }

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
                Metadata metadata)
        {
            TraceId = traceId;
            SpanId = spanId;
            ParentSpanId = parentSpanId;
            Name = name;
            StartTimestampUtcTicks = startTimestampUtcTicks;
            EndTimestampUtcTicks = endTimestampUtcTicks;
            ElapsedMs = elapsedMs;
            IsSuccess = isSuccess;
            Tags = tags;
            Level = level;
            MetadataValue = metadata;
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
            => ZString.Format("[Telemetry] {0} {1:F1}ms (trace={2}.. span={3}..)",
                Name, ElapsedMs,
                TraceId,
                SpanId);
    }
}
