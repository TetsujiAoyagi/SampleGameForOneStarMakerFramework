#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.Rendering.Vfx;

namespace OneStarMaker.Tests.Rendering.Vfx
{
    /// <summary>レベル別の件数を数える偽 logger。診断ログの回数（一度だけ・handle ごとに一度だけ）の判定に使う。</summary>
    internal sealed class FakeVfxLogger : ILogger<VfxSystem>
    {
        private readonly Dictionary<LogLevel, int> _counts = new();

        public List<string> Messages { get; } = new();

        public int WarningCount => CountOf(LogLevel.Warning);

        public int ErrorCount => CountOf(LogLevel.Error);

        public int TotalCount => Messages.Count;

        public int CountOf(LogLevel level) => _counts.TryGetValue(level, out var count) ? count : 0;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _counts[logLevel] = CountOf(logLevel) + 1;
            Messages.Add(logLevel + ": " + formatter(state, exception));
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    }
}
