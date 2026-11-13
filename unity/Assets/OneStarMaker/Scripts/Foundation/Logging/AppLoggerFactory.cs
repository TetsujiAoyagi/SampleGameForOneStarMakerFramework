#nullable enable

using System;
using System.IO;
using Microsoft.Extensions.Logging;
using ZLogger;
using ZLogger.Formatters;
using ZLogger.Providers;

namespace OneStarMaker.Foundation.Logging
{
    /// <summary>
     /// ZLogger ベースの <see cref="ILogger"/> を生成するファクトリ。
    /// ローカル rolling file と、別経路で確立した Stream へのリアルタイム出力を構成する。
    ///
    /// <para>
    /// ここでは「どこへ送るか」だけを決め、リアルタイム転送の接続そのもの
    /// （WebSocket 接続確立や NamedPipe 接続など）は外側で行う。
    /// </para>
    ///
    /// <para>
    /// つまり本クラスは Logging Infrastructure の責務だけを持ち、
    /// Telemetry の表現や transport のライフサイクル管理には踏み込まない。
    /// </para>
    /// </summary>
    public sealed class AppLoggerFactory : ILoggerFactory
    {
        private const string LogDirectoryName = "OneStarMakerLocal\\Log";
        private const int DefaultRollingSizeKb = 1024;

        private readonly ILoggerFactory _inner;
        private readonly LoggerFailureFile _failures;

        /// <summary>
        /// 送信 payload に埋め込むアプリケーション名。
        /// 受信側で複数アプリのログを同時に扱うときの識別子として使う。
        /// </summary>
        public string ApplicationName { get; }

        /// <summary>
        /// ログ出力ディレクトリの絶対パス。
        /// </summary>
        public string LogDirectoryPath { get; }

        /// <summary>
        /// リアルタイム転送先の Stream。
        /// WebSocket 接続や NamedPipe 接続そのものは呼び出し側で確立して渡す。
        /// </summary>
        public Stream? RealtimeStream { get; }

        /// <summary>
        /// リアルタイム転送に使うフォーマット。
        /// ローカル file は常に JSON のまま維持し、ここは realtime stream 側だけに効く。
        /// </summary>
        public RealtimeLogFormat RealtimeLogFormat { get; }

        /// <summary>
        /// ロガーファクトリを構築する。
        ///
        /// <para>
        /// 既定では以下の二系統を構成する:
        /// </para>
        /// <list type="number">
        /// <item><description>ローカル解析・保守用の rolling file(JSON)</description></item>
        /// <item><description>接続済み stream へ流す realtime output(JSON または MessagePack)</description></item>
        /// </list>
        ///
        /// <para>
        /// realtimeStream が null の場合は rolling file のみを構成する。
        /// </para>
        /// </summary>
        public AppLoggerFactory(
            Stream? realtimeStream = null,
            LogLevel minimumLevel = LogLevel.Trace,
            int rollingSizeKb = DefaultRollingSizeKb,
            RealtimeLogFormat realtimeLogFormat = RealtimeLogFormat.MessagePack)
            : this(GetLogDirectoryPath(), realtimeStream, minimumLevel, rollingSizeKb, realtimeLogFormat)
        {
        }

        // テストは一時 directory だけを指定し、本番と同じ配線・通知先導出・終了順を通す。
        // processor や通知先を差し替える seam は設けず、実 provider の失敗を観測する。
        internal AppLoggerFactory(
            string logDirectoryPath,
            Stream? realtimeStream = null,
            LogLevel minimumLevel = LogLevel.Trace,
            int rollingSizeKb = DefaultRollingSizeKb,
            RealtimeLogFormat realtimeLogFormat = RealtimeLogFormat.MessagePack)
        {
            if (rollingSizeKb <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rollingSizeKb));
            }

            ApplicationName = UnityEngine.Application.productName;
            RealtimeStream = realtimeStream;
            RealtimeLogFormat = realtimeLogFormat;
            LogDirectoryPath = logDirectoryPath;
            EnsureDirectory(LogDirectoryPath);

            // 全 provider がファイルを開く前に一度だけ実施する。実行中には清掃せず、
            // 通常ログと通知の両方を同じ起動時刻・保持 policy で判断する。
            var startupUtc = DateTime.UtcNow;
            LogDirectoryMaintenance.Clean(LogDirectoryPath, startupUtc);
            _failures = new LoggerFailureFile(LogDirectoryPath, rollingSizeKb);

            // producer 相関（sequence / trace / span）はログ呼び出しスレッドで採取する必要があるため、
            // 生成した factory を必ずデコレータで包む。詳細は LogProducerCorrelation を参照。
            _inner = new ProducerCorrelationLoggerFactory(LoggerFactory.Create(builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(minimumLevel);

                // ローカル保存は人間が直接読めることを優先し、常に JSON rolling file を使う。
                builder.AddZLoggerRollingFile(options =>
                {
                    options.FilePathSelector = (dt, sequenceNumber) =>
                        Path.Combine(
                            LogDirectoryPath,
                            LogFileNames.Normal(dt.ToLocalTime().DateTime, sequenceNumber));
                    options.RollingInterval = RollingInterval.Day;
                    options.RollingSizeKB = rollingSizeKb;
                    ConfigureJsonFormatter(options, "rolling-json");
                });

                if (RealtimeStream != null)
                {
                    // realtime stream は「低遅延で別アプリへ渡す」用途なので、
                    // file 側とは別フォーマットに切り替えられるようにしている。
                    switch (RealtimeLogFormat)
                    {
                        case RealtimeLogFormat.Json:
                            // デバッグや一時切り分け用。可読性は高いが MessagePack の恩恵はない。
                            builder.AddZLoggerStream(RealtimeStream,
                                options => ConfigureJsonFormatter(options, "realtime-json"));
                            break;
                        case RealtimeLogFormat.MessagePack:
                            // 本命経路。1 レコード = 1 MessagePack payload とし、
                            // formatter 側で length-prefix frame まで作って stream に流す。
                            builder.AddZLoggerStream(
                                RealtimeStream,
                                options => MessagePackZLoggerFormatter.Configure(options, ApplicationName));
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(
                                nameof(realtimeLogFormat),
                                RealtimeLogFormat,
                                "Unsupported realtime log format.");
                    }
                }
            }));
        }

        /// <summary>
        /// カテゴリ付きロガーを生成する。
        /// </summary>
        public ILogger<T> CreateLogger<T>()
        {
            return _inner.CreateLogger<T>();
        }

        /// <summary>
        /// 文字列カテゴリのロガーを生成する。
        /// </summary>
        public ILogger CreateLogger(string categoryName)
        {
            return _inner.CreateLogger(categoryName);
        }

        /// <summary>
        /// provider 追加の責務も標準 <see cref="ILoggerFactory"/> と同じ形で公開する。
        /// これにより、消費側は AppLoggerFactory 固有 API ではなく抽象に依存できる。
        /// </summary>
        public void AddProvider(ILoggerProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            _inner.AddProvider(provider);
        }

        public void Dispose()
        {
            // ZLogger はキューを drain して渡された realtime stream も閉じる。
            // 最後の変換失敗も通知できるよう、その後に独立通知を閉じる。
            _inner.Dispose();
            _failures.Dispose();
        }

        private void ConfigureJsonFormatter(ZLoggerOptions options, string provider)
        {
            // JSON 側はローカルファイル観測が主目的なので、
            // timestamp と property をできるだけ落とさず保持する。
            // producer correlation は ProducerCorrelationLoggerFactory が呼び出し時に採取するため、
            // provider をまたいでも採番は 1 回で済む。ただし rolling file の出力形式を変えないよう、
            // ここでは IncludeScopes を有効にせず L0 Unity Log JSON へ相関値を載せない。
            // Telemetry は JsonFileTelemetrySink が producer-owned 値を structured property
            // として明示的に渡すので、L0 でも同じ session/frame 軸を検索できる。
            options.InternalErrorLogger = exception => _failures.Write(provider, exception);
            options.UseFormatter(() =>
            {
                var formatter = new SystemTextJsonZLoggerFormatter
                {
                    UseUtcTimestamp = true,
                    IncludeProperties = IncludeProperties.All
                };
                // 既定の null 省略・escaping 等を残したまま、値変換だけを追加する。
                // wrapper と buffer は provider ごとに生成し、その寿命内だけで再利用する。
                formatter.JsonSerializerOptions.Converters.Add(new Vector3JsonConverter());
                return new BufferedJsonFormatter(formatter);
            });
        }

        private static string GetLogDirectoryPath()
        {
            // Unity プロジェクトのルート配下に OneStarMakerLocal\Log を切る。
            // Assets 配下に出すと source 管理や Unity の asset import と干渉しやすいため、
            // project root 直下に逃がしている。
            var projectRoot = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName
                ?? throw new InvalidOperationException(
                    $"Failed to resolve project root from Application.dataPath: {UnityEngine.Application.dataPath}");

            return Path.Combine(projectRoot, LogDirectoryName);
        }

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }
}
