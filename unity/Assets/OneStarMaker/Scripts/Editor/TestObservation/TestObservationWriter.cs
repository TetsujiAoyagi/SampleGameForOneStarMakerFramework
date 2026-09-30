#nullable enable
using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace OneStarMaker.Editor.TestObservation
{
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
            var state = JsonUtility.FromJson<TestObservationState>(File.ReadAllText(ProgressPath, Encoding.UTF8));
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

        internal static void AtomicWrite(string path, byte[] bytes)
        {
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                WriteNew(temp, bytes);
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
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
