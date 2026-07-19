#nullable enable

using MessagePack;

namespace OneStarMaker.Foundation.Logging
{
    /// <summary>
    /// sender と receiver の間で共有する realtime log contract v1。
    ///
    /// <para>
    /// 重要なのは「ログ表示に最低限必要なものだけ」を先に固定すること。
    /// ZLogger の内部表現をそのまま wire format に流すのではなく、
    /// 受信アプリが安定して扱える小さな DTO に落としている。
    /// </para>
    ///
    /// <para>
    /// 将来項目を増やすときは Key を追加し、既存番号は再利用しない。
    /// receiver 側との互換性維持のため、SchemaVersion も必ず送る。
    /// </para>
    /// </summary>
    [MessagePackObject]
    public sealed class LogEnvelopeV1
    {
        /// <summary>
        /// wire format のスキーマバージョン。
        /// receiver 側が sender の更新に追随できているかを判定する基準になる。
        /// </summary>
        [Key(0)]
        public int SchemaVersion { get; set; } = 1;

        /// <summary>
        /// どのアプリから飛んできたログかを識別するための名前。
        /// 受信アプリで複数 sender をまとめて表示するときに使う。
        /// </summary>
        [Key(1)]
        public string ApplicationName { get; set; } = string.Empty;

        /// <summary>
        /// UTC の UnixTimeMilliseconds。
        /// DateTime をそのまま送るより、receiver 側で時刻処理しやすい形にしている。
        /// </summary>
        [Key(2)]
        public long TimestampUnixTimeMilliseconds { get; set; }

        /// <summary>
        /// logger category。通常は型名や論理カテゴリ名が入る。
        /// </summary>
        [Key(3)]
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// <see cref="Microsoft.Extensions.Logging.LogLevel"/> を int 化した値。
        /// enum そのものではなく数値で送ることで、receiver 側実装を薄くできる。
        /// </summary>
        [Key(4)]
        public int LogLevel { get; set; }

        /// <summary>
        /// EventId.Id。
        /// </summary>
        [Key(5)]
        public int EventId { get; set; }

        /// <summary>
        /// EventId.Name。未設定の logger もあるため nullable。
        /// </summary>
        [Key(6)]
        public string? EventName { get; set; }

        /// <summary>
        /// 人間が読む本文。
        /// receiver 側 UI はまずこの値を中心に表示する。
        /// </summary>
        [Key(7)]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 例外全文。必要なときだけ詳細表示する想定。
        /// </summary>
        [Key(8)]
        public string? Exception { get; set; }

        /// <summary>
        /// sender 側スレッド ID。
        /// 非同期系の追跡や UI/worker の切り分けに使う。
        /// </summary>
        [Key(9)]
        public int ThreadId { get; set; }

        /// <summary>
        /// sender 側スレッド名。未設定のスレッドもあるため nullable。
        /// </summary>
        [Key(10)]
        public string? ThreadName { get; set; }

        /// <summary>
        /// 呼び出し元メンバー名。source generator / caller info がある場合に入る。
        /// </summary>
        [Key(11)]
        public string? MemberName { get; set; }

        /// <summary>
        /// 呼び出し元ファイルパス。receiver 側では通常フル表示せず補助情報として扱う。
        /// </summary>
        [Key(12)]
        public string? FilePath { get; set; }

        /// <summary>
        /// 呼び出し元行番号。
        /// </summary>
        [Key(13)]
        public int LineNumber { get; set; }

        /// <summary>
        /// Unity 起動単位の session ID。DebugSocket handshake Welcome と同一。
        /// export 側で後付けしないため wire 作成時に必ず埋める。
        /// </summary>
        [Key(14)]
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// session 内で Telemetry と共有する producer 順序。同一 frame 内の全体順序を確定する。
        /// </summary>
        [Key(15)]
        public long ProducerSequence { get; set; }

        /// <summary>
        /// formatter が envelope を組み立てた時点の Unity player-loop frame。
        /// main thread 以外では null。queue 遅延により事象発生 frame と一致しない場合がある。
        /// </summary>
        [Key(16)]
        public int? UnityFrameAtEmit { get; set; }

        /// <summary>
        /// active telemetry span がある場合のみコピー。span 外 Log は null のまま送る。
        /// </summary>
        [Key(17)]
        public long? TraceId { get; set; }

        /// <summary>
        /// active telemetry span がある場合のみコピー。span 外 Log は null のまま送る。
        /// </summary>
        [Key(18)]
        public long? SpanId { get; set; }
    }
}
