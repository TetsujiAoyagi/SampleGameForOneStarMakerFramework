#nullable enable

using System;
using System.IO;

namespace OneStarMaker.Foundation.Logging
{
    /// <summary>起動前の一回だけ、直下にある期限切れ管理ファイルを清掃する。</summary>
    internal static class LogDirectoryMaintenance
    {
        internal static void Clean(string directory, DateTime startupUtc)
        {
            try
            {
                // root が junction 等なら、列挙そのものが別領域へ入る。子だけの検査では足りない。
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) return;
                foreach (var path in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        if (!LogFileNames.TryParse(Path.GetFileName(path), out _, out _, out _)) continue;
                        var attributes = File.GetAttributes(path);
                        if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0) continue;
                        if (LogRetentionPolicy.IsExpired(File.GetLastWriteTimeUtc(path), startupUtc))
                            File.Delete(path);
                    }
                    catch
                    {
                        // Windows の delete sharing 拒否もここへ来る。利用中ファイルを残し、
                        // 他の候補は続ける。個別の失敗状態を永久保存せず次の起動で再試行する。
                    }
                }
            }
            catch
            {
                // 列挙や root metadata の失敗でも通常ログの起動を止めない。
                // hostile な差替えとの原子性や、他 OS の利用中保護までは保証しない。
            }
        }
    }
}
