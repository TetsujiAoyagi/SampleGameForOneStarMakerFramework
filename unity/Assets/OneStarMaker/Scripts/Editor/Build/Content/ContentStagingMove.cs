#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace OneStarMaker.Editor.Build.Content
{
    /// <summary>
    /// content の build と transport の publish が、揃った staging を同じ volume の final へ 1 回の rename で公開する移動。
    /// Windows で、変更の通知を受けて新しい file を一瞬開く監視（開発者の PC に普通にある tool）が staging の中の file を開いていると、
    /// directory の rename は <c>FILE_SHARE_DELETE</c> 付きの handle でも拒まれる。その一時的な失敗に限り、短い上限で移し直す。
    /// </summary>
    /// <remarks>
    /// 契約（変える時は呼び手の catch・finally と読み手の「final があれば公開済み」を確かめる）:
    /// <list type="bullet">
    /// <item>成功で戻るのは移動そのものが成功した時だけ。失敗の後の状態（staging が無く final がある等）から成功を推定しない。
    /// Windows の同期の rename は失敗を返した時点で rename をしていないので、その final はこの呼び出しの物だと確かめられない。</item>
    /// <item>移し直すのは、失敗が一時的で、待ちの前と待ちの後（次の移動の前）の両方で staging があり final が無い時だけ。
    /// それ以外は移動を呼ばずに最後の失敗をそのまま投げる（既にある final へ入れない・混ぜない。staging の片付けは呼び手の仕事のまま）。</item>
    /// <item>上限は試行の前と待ちの前に時計で見る（待ちが上限をまたがない）。時計が進まなくても試行は <see cref="MaxAttempts"/> 回まで。
    /// 上限を超えたら、最後の失敗を内側に持つ <see cref="IOException"/> を投げる。型を変えないのは、呼び手の catch の振り分けを今と同じにするため。
    /// 文言に staging と final の path を入れるのは、全回帰の log から staging の失敗を数えられるようにするため。</item>
    /// <item>1 回目が成功する時は待ちも時計も使わない（監視の無い環境の振る舞いを変えない）。</item>
    /// </list>
    /// 特定の監視の tool を検出・名指しする分岐は作らない。判定は失敗の種類と staging / final の状態だけで行う。
    /// </remarks>
    internal static class ContentStagingMove
    {
        internal const int WaitMilliseconds = 50;
        internal const int BudgetMilliseconds = 2000;
        // 時計が進まない時の上限。待ちは最大 MaxAttempts - 1 回で、その合計は BudgetMilliseconds に等しい。
        internal const int MaxAttempts = 41;
        internal const string LogPrefix = "[ContentStagingMove]";

        internal static void Move(string staging, string final)
            => Move(staging, final, Directory.Move, Thread.Sleep, null, UnityEngine.Debug.Log,
                Environment.OSVersion.Platform == PlatformID.Win32NT);

        // 注入した操作はこの呼び出しだけのもの。test は時間を進め、移動の失敗と、その間の staging / final の変化を起こす。
        // windows は一時的な失敗の判定を有効にするか（本番は実行中の OS。Windows 以外の CI でも分岐を確かめられるよう注入できる）。
        internal static void Move(string staging, string final, Action<string, string> move, Action<int> wait,
            Func<long>? elapsedMilliseconds, Action<string> log, bool windows)
        {
            var clock = Stopwatch.StartNew();
            long Elapsed() => elapsedMilliseconds?.Invoke() ?? clock.ElapsedMilliseconds;
            Exception? lastFailure = null;
            for (var attempt = 1; ; attempt++)
            {
                if (attempt > 1 && Elapsed() >= BudgetMilliseconds)
                    throw Exhausted(staging, final, attempt - 1, lastFailure!);
                try
                {
                    move(staging, final);
                }
                catch (Exception error) when (windows && IsTransient(error))
                {
                    lastFailure = error;
                    // 誰かが final を作った・staging が消えた時は、その状態の帰属を確かめられないので移し直さない。
                    if (!ReadyToMove(staging, final)) throw;
                    if (attempt >= MaxAttempts || Elapsed() + WaitMilliseconds > BudgetMilliseconds)
                        throw Exhausted(staging, final, attempt, error);
                    wait(WaitMilliseconds);
                    if (!ReadyToMove(staging, final)) ExceptionDispatchInfo.Capture(error).Throw();
                    continue;
                }
                if (attempt > 1) log(LogPrefix + " moved after " + attempt + " attempts: " + final);
                return;
            }
        }

        // 一時的な lock とみなす失敗。.NET の Directory.Move は ERROR_ACCESS_DENIED を IOException（HResult 0x80070005）で投げ、
        // 実装によっては UnauthorizedAccessException になるので両方を受ける。共有違反・lock 違反も同じ扱いにする。
        // Win32 の値を載せない IOException（別の volume、path が無い等の既定値）は入れない。入れると恒久の失敗も上限まで待つ。
        internal static bool IsTransient(Exception error)
        {
            if (error is UnauthorizedAccessException) return true;
            if (!(error is IOException)) return false;
            return error.HResult == unchecked((int)0x80070005) ||
                   error.HResult == unchecked((int)0x80070020) ||
                   error.HResult == unchecked((int)0x80070021);
        }

        private static bool ReadyToMove(string staging, string final)
            => Directory.Exists(staging) && !Directory.Exists(final) && !File.Exists(final);

        private static IOException Exhausted(string staging, string final, int attempts, Exception lastFailure)
            => new IOException("The staging directory could not be moved after " + attempts + " attempt(s): '" + staging + "' -> '" + final
                + "'. Last failure: " + lastFailure.Message, lastFailure);
    }
}
