#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

namespace OneStarMaker.Editor.TestObservation
{
    [Serializable] internal sealed class ReloadSettingsRecord
    {
        public string invocationId = "", projectPath = "";
        public bool enterPlayModeOptionsEnabled, restored;
        public int enterPlayModeOptions;
    }

    internal sealed class TestObservationWriter
    {
        private readonly string directory;
        internal string ProgressPath => Path.Combine(directory, "observation-progress.json");
        internal string TerminalPath => Path.Combine(directory, "observation.json");
        internal string RecoveryPath => Path.Combine(directory, "reload-settings.json");

        internal TestObservationWriter(string path)
        {
            directory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? throw new IOException("observation path に親 directory がありません");
            if (!string.Equals(Path.GetFullPath(path), TerminalPath, StringComparison.OrdinalIgnoreCase))
                throw new IOException("observation terminal path が規定名ではありません");
            if (!Directory.Exists(directory)) throw new IOException("invocation directory がありません");
        }

        internal TestObservationState? Read(string invocation, string project, int pid, long frequency)
        {
            if (!string.Equals(Path.GetFileName(directory), invocation, StringComparison.OrdinalIgnoreCase))
                throw new IOException("GUID directory と invocation が一致しません");
            if (!File.Exists(ProgressPath))
            {
                if (File.Exists(TerminalPath)) throw new IOException("terminal のみが存在します");
                return null;
            }
            var raw = File.ReadAllText(ProgressPath, Encoding.UTF8);
            var state = JsonUtility.FromJson<TestObservationState>(raw);
            // Writer が保存した形だけを復帰する。JsonUtility は未知 field を黙って捨てるので、
            // 凍結 v1 の外のキーや欠落値を含む旧 progress を正常状態へ洗い替えさせない。
            if (state == null || !string.Equals(raw, JsonUtility.ToJson(state, true) + "\n", StringComparison.Ordinal))
                throw new IOException("progress の閉じた schema または保存形式が一致しません");
            if (state == null || state.schemaVersion != 1 || state.recordKind != "unity-test-observation" ||
                state.invocationId != invocation || state.processId != pid || state.clock == null ||
                state.clock.frequency != frequency || state.projectPath != project || state.@sealed ||
                state.sequence < 0 || state.domains == null || state.failure == null)
                throw new IOException("progress の schema または identity が一致しません");
            if (File.Exists(TerminalPath)) throw new IOException("復帰前に terminal が存在します");
            return state;
        }

        internal void Progress(TestObservationState state)
        {
            // 同じ GUID directory 内で flush 後に置換し、reload が半書込を旧 state と誤認しないようにする。
            var bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(state, true) + "\n");
            AtomicWrite(ProgressPath, bytes);
        }

        internal void Terminal(TestObservationState state)
        {
            if (!state.@sealed) throw new IOException("seal 前の terminal を保存できません");
            var bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(state, true) + "\n");
            WriteNew(TerminalPath, bytes);
        }

        internal void CreateRecovery(ReloadSettingsRecord record)
        {
            // 設定変更より先に復旧値を追加専用で確定し、二度目の準備による元値の上書きを拒否する。
            WriteNew(RecoveryPath, new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(record)));
        }

        internal ReloadSettingsRecord? ReadRecovery(string invocation, string project, bool required)
        {
            if (!File.Exists(RecoveryPath))
            {
                if (required) throw new IOException("準備済み reload settings record がありません");
                return null;
            }
            var raw = File.ReadAllText(RecoveryPath, Encoding.UTF8);
            var record = JsonUtility.FromJson<ReloadSettingsRecord>(raw);
            // この private record は Writer 自身が常に compact な全5項目を保存する。
            // 再シリアライズとの一致で、JsonUtility が欠落bool/intを既定値へ補ってしまう破損も拒否する。
            if (record == null || !string.Equals(raw, JsonUtility.ToJson(record), StringComparison.Ordinal))
                throw new IOException("reload settings record の必須項目または型が不正です");
            if (record.invocationId != invocation || record.projectPath != project)
                throw new IOException("reload settings record の identity が一致しません");
            return record;
        }

        internal void SaveRecovery(ReloadSettingsRecord record)
        {
            AtomicWrite(RecoveryPath, new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(record)));
        }

        internal ReloadSettingsRecord ReadRestoredRecovery(string invocation, string project)
        {
            var record = ReadRecovery(invocation, project, true);
            if (record == null || !record.restored) throw new IOException("reload settings の復元完了記録がありません");
            return record;
        }

        internal static void AtomicWrite(string path, byte[] bytes)
        {
            AtomicWrite(path, bytes, (temp, destination) => File.Replace(temp, destination, null),
                Thread.Sleep, null, File.Delete);
        }

        // The injected operations belong to this call only. Tests can move time forward and cause
        // replacement faults without changing another observation writer's publication path.
        internal static void AtomicWrite(string path, byte[] bytes, Action<string, string> replace,
            Action<int> wait, Func<long>? elapsedMilliseconds, Action<string> deleteTemp)
        {
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                WriteNew(temp, bytes);
                if (File.Exists(path))
                {
                    // The first decision is final: a failed replacement must never publish by Move.
                    // Known ReplaceFile failures retain both pathnames; partial failures do not.
                    var clock = Stopwatch.StartNew();
                    IOException? lastFailure = null;
                    for (var attempt = 1; ; attempt++)
                    {
                        if (attempt > 1 && (elapsedMilliseconds?.Invoke() ?? clock.ElapsedMilliseconds) >= 100)
                            throw lastFailure!;
                        try
                        {
                            replace(temp, path);
                            break;
                        }
                        catch (IOException error) when (IsRetryableReplacement(error))
                        {
                            lastFailure = error;
                            if (attempt >= 5 || !File.Exists(temp) || !File.Exists(path) ||
                                (elapsedMilliseconds?.Invoke() ?? clock.ElapsedMilliseconds) >= 100)
                                throw;
                            wait(20);
                            if (!File.Exists(temp) || !File.Exists(path) ||
                                (elapsedMilliseconds?.Invoke() ?? clock.ElapsedMilliseconds) >= 100)
                                throw;
                        }
                    }
                }
                else File.Move(temp, path);
            }
            catch (Exception publicationError)
            {
                try { if (File.Exists(temp)) deleteTemp(temp); }
                catch (Exception cleanupError)
                {
                    // Neither a failed publication nor a failed cleanup may disappear from the result.
                    throw new AggregateException(publicationError, cleanupError);
                }
                throw;
            }
            if (File.Exists(temp)) deleteTemp(temp);
        }

        private static bool IsRetryableReplacement(IOException error)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return false;
            return error.HResult == unchecked((int)0x80070020) ||
                   error.HResult == unchecked((int)0x80070021) ||
                   error.HResult == unchecked((int)0x80070497);
        }

        internal static void WriteNew(string path, byte[] bytes)
        {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }
    }
}
