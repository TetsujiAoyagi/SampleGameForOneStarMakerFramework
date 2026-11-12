#nullable enable

using System;

namespace OneStarMaker.Foundation.Logging
{
    /// <summary>保持期間の変更点を I/O と分離し、ログと通知で同じ期限判断を使う。</summary>
    internal static class LogRetentionPolicy
    {
        internal static readonly TimeSpan Retention = TimeSpan.FromDays(4);

        // 日付名ではなく最終追記 UTC を正とする。境界一致と未来時刻は保持する。
        internal static bool IsExpired(DateTime lastWriteUtc, DateTime startupUtc)
            => lastWriteUtc < startupUtc - Retention;
    }
}
