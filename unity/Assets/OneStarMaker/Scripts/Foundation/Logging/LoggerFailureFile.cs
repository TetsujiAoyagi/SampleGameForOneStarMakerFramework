#nullable enable

using System;
using System.IO;
using System.Text;

namespace OneStarMaker.Foundation.Logging
{
    /// <summary>ログ provider 自身へ戻らず、最小限の障害情報だけを独立したファイルへ残す。</summary>
    internal sealed class LoggerFailureFile : IDisposable
    {
        private readonly object _gate = new object();
        private readonly string _directory;
        private readonly long _sizeBytes;
        private readonly Func<DateTimeOffset> _clock;
        private bool _disposed;

        internal LoggerFailureFile(string directory, int rollingSizeKb, Func<DateTimeOffset>? clock = null)
        {
            if (rollingSizeKb <= 0) throw new ArgumentOutOfRangeException(nameof(rollingSizeKb));
            _directory = directory;
            _sizeBytes = (long)rollingSizeKb * 1024;
            _clock = clock ?? (() => DateTimeOffset.UtcNow);
            // 構築時には開かない。factory の清掃後、最初の callback からのみ I/O を行う。
        }

        internal void Write(string provider, Exception exception)
        {
            lock (_gate)
            {
                if (_disposed) return;
                try
                {
                    var now = _clock();
                    var bytes = CreateLine(now, provider, exception);
                    Directory.CreateDirectory(_directory);
                    var day = now.LocalDateTime.Date;
                    long latest = -1;
                    string? latestPath = null;
                    foreach (var path in Directory.EnumerateFiles(_directory, "*", SearchOption.TopDirectoryOnly))
                    {
                        if (LogFileNames.TryParse(Path.GetFileName(path), out var date, out var sequence, out var notice)
                            && notice && date == day && sequence > latest)
                        {
                            latest = sequence;
                            latestPath = path;
                        }
                    }

                    var sequenceToUse = Math.Max(0, latest);
                    var target = latestPath ?? Path.Combine(_directory, LogFileNames.Notice(day, sequenceToUse));
                    // 日付ごとに既存最大連番を見るため、再起動・日付変更でも上書きしない。
                    // 世代数で消さず、保持期間の清掃は次の factory 起動だけが担当する。
                    if (File.Exists(target) && new FileInfo(target).Length > _sizeBytes - bytes.Length)
                    {
                        sequenceToUse = checked(sequenceToUse + 1);
                        target = Path.Combine(_directory, LogFileNames.Notice(day, sequenceToUse));
                    }
                    using var stream = new FileStream(target, FileMode.Append, FileAccess.Write, FileShare.Read);
                    stream.Write(bytes, 0, bytes.Length);
                }
                catch
                {
                    // open / rotation / write / close すべてを隔離する。Logger や Unity main thread
                    // へ戻らず、この回の到達は諦める。失敗を latch せず、次 callback は再試行する。
                }
            }
        }

        private static byte[] CreateLine(DateTimeOffset now, string provider, Exception exception)
        {
            // provider は内部の二つの固定名だけ。例外 Message / StackTrace / Data / inner や
            // 元ログ値は機密を含み得るので、型名以外には一切触れない。
            var source = provider == "rolling-json" ? "rolling-json" : "realtime-json";
            var type = exception.GetType().FullName ?? "Exception";
            var safe = new StringBuilder(128);
            foreach (var c in type)
            {
                if (!char.IsControl(c) && c != '|') safe.Append(c);
                if (safe.Length == 128) break;
            }
            var prefix = now.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture)
                + "|" + source + "|provider-write-error|";
            var bytes = Encoding.UTF8.GetBytes(prefix + safe + "\n");
            return bytes.Length <= 512 ? bytes : Encoding.UTF8.GetBytes(prefix + "Exception\n");
        }

        public void Dispose()
        {
            // factory は先に provider の drain を完了する。ここは file handle を持たず、
            // lock で最後の callback と終了境界だけを直列化する。
            lock (_gate) _disposed = true;
        }
    }
}
