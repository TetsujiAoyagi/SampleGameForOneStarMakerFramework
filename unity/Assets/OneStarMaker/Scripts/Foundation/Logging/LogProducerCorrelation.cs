#nullable enable

using System;
using Microsoft.Extensions.Logging;
using OneStarMaker.Foundation.DebugSocket;
using OneStarMaker.Foundation.Telemetry;
using ZLogger;

namespace OneStarMaker.Foundation.Logging
{
    /// <summary>
    /// ログ呼び出しスレッド上で採取した producer 相関値のスナップショット。
    ///
    /// <para>
    /// ZLogger は entry を背景スレッドで format するため、formatter 内で
    /// <see cref="UnitySessionCorrelationContext.NextProducerSequence"/> や
    /// <see cref="AppTelemetry.CurrentTraceId"/> を読むと以下の 2 点が壊れる:
    /// </para>
    /// <list type="number">
    /// <item><description>
    /// sequence が「ログが起きた順」ではなく「flush された順」で採番される。
    /// Telemetry 側は呼び出し時に採番しているため、両者を突き合わせた全体順序が入れ替わる。
    /// </description></item>
    /// <item><description>
    /// trace / span context は <c>AsyncLocal</c> で流れており、background thread には伝播しない。
    /// active span 内で出したログの traceId が常に null になる。
    /// </description></item>
    /// </list>
    ///
    /// <para>
    /// そこで採取は呼び出しスレッド上で 1 回だけ行い、ZLogger の scope に載せて entry に運ぶ。
    /// scope state は <c>ILogger.Log</c> の時点で materialize されるため、
    /// formatter が背景スレッドで読んでも呼び出し時の値が得られる。
    /// </para>
    ///
    /// <para>
    /// 1 回のログ呼び出しにつき採番が 1 回で済むのも、この配置の利点である。
    /// formatter 内で採番すると、同じログが rolling file と realtime stream の
    /// 2 provider を通るときに別々の番号が振られてしまう。
    /// </para>
    /// </summary>
    internal sealed class LogProducerCorrelation
    {
        private LogProducerCorrelation(string sessionId, long producerSequence, long? traceId, long? spanId)
        {
            SessionId = sessionId;
            ProducerSequence = producerSequence;
            TraceId = traceId;
            SpanId = spanId;
        }

        internal string SessionId { get; }

        internal long ProducerSequence { get; }

        internal long? TraceId { get; }

        internal long? SpanId { get; }

        /// <summary>
        /// 現在のスレッドの相関値を採取する。producer sequence はここで 1 つ消費する。
        /// </summary>
        internal static LogProducerCorrelation Capture()
        {
            return new LogProducerCorrelation(
                UnitySessionCorrelationContext.SessionId,
                UnitySessionCorrelationContext.NextProducerSequence(),
                AppTelemetry.CurrentTraceId,
                AppTelemetry.CurrentSpanId);
        }

        /// <summary>
        /// ZLogger の scope state から相関値を取り出す。載っていない場合は null。
        ///
        /// <para>
        /// 呼び出し側が独自に <c>BeginScope</c> している場合もあるため、
        /// key ではなく型で判別する。最も内側（＝直前に push した相関値）を採用する。
        /// </para>
        /// </summary>
        internal static LogProducerCorrelation? TryFind(LogScopeState scopeState)
        {
            if (scopeState == null || scopeState.IsEmpty)
            {
                return null;
            }

            LogProducerCorrelation? found = null;
            foreach (var property in scopeState.Properties)
            {
                if (property.Value is LogProducerCorrelation correlation)
                {
                    found = correlation;
                }
            }

            return found;
        }
    }

    /// <summary>
    /// すべてのログ呼び出しに <see cref="LogProducerCorrelation"/> の scope を被せる
    /// <see cref="ILoggerFactory"/> デコレータ。
    ///
    /// <para>
    /// 呼び出し側のコードには一切手を入れずに相関値を運ぶための唯一の口であり、
    /// ZLogger provider 側で <c>ZLoggerOptions.IncludeScopes</c> が true になっていて初めて機能する。
    /// 両者は <see cref="ZLoggerOptionsProducerCorrelationExtensions.EnableProducerCorrelation"/> で対に保つ。
    /// </para>
    /// </summary>
    internal sealed class ProducerCorrelationLoggerFactory : ILoggerFactory
    {
        private readonly ILoggerFactory _inner;

        internal ProducerCorrelationLoggerFactory(ILoggerFactory inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public ILogger CreateLogger(string categoryName)
            => new ProducerCorrelationLogger(_inner.CreateLogger(categoryName));

        public void AddProvider(ILoggerProvider provider) => _inner.AddProvider(provider);

        public void Dispose() => _inner.Dispose();

        private sealed class ProducerCorrelationLogger : ILogger
        {
            private readonly ILogger _inner;

            internal ProducerCorrelationLogger(ILogger inner)
            {
                _inner = inner;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
                => _inner.BeginScope(state);

            public bool IsEnabled(LogLevel logLevel) => _inner.IsEnabled(logLevel);

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                // 出力されないログで sequence を消費すると、番号に穴が空いて
                // 「欠番＝取りこぼし」の判定ができなくなる。
                if (!_inner.IsEnabled(logLevel))
                {
                    return;
                }

                // telemetry record は AppTelemetry が生成時点で採番済み。
                // ここで ILogger 経由の書き出しにも採番すると 1 record に 2 番号を消費してしまう。
                if (IsTelemetryEntry(eventId))
                {
                    _inner.Log(logLevel, eventId, state, exception, formatter);
                    return;
                }

                using (_inner.BeginScope(LogProducerCorrelation.Capture()))
                {
                    _inner.Log(logLevel, eventId, state, exception, formatter);
                }
            }

            /// <summary>
            /// telemetry 経路の entry かどうか。判定は
            /// <c>MessagePackZLoggerFormatter</c> の抑制判定と同じ述語に揃える。
            /// </summary>
            private static bool IsTelemetryEntry(EventId eventId)
            {
                return eventId.Id == TelemetryZLoggerConstants.EventId.Id
                    && string.Equals(
                        eventId.Name,
                        TelemetryZLoggerConstants.EventId.Name,
                        StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    /// producer 相関を運ぶために ZLogger provider 側で必要な設定。
    /// </summary>
    internal static class ZLoggerOptionsProducerCorrelationExtensions
    {
        /// <summary>
        /// scope state を entry に載せるよう ZLogger provider を設定する。
        /// <see cref="ProducerCorrelationLoggerFactory"/> と必ず対で使う。
        /// </summary>
        internal static void EnableProducerCorrelation(this ZLoggerOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            options.IncludeScopes = true;
        }
    }
}
