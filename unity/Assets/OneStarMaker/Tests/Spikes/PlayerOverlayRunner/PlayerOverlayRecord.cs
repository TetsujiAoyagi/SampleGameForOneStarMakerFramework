#nullable enable

using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;

namespace OneStarMaker.Tests.Spikes.PlayerOverlayRunner
{
    /// <summary>
    /// round の record（JSONL）。1 つの process が 1 つの file に書き、行ごとに開いて閉じる（process が落ちても、書いた行までが残る。
    /// HANDOFF §2 の record）。各行は書いた時刻（at）、通し番号（seq）、種類（type）、その時の段（stage）、round と label を持つ。
    /// 欄の名前は Editor 側の PovLine（PlayerOverlayInventory.cs）と揃える。Unity の log は購読の間、発生順のまま段を付けて書く。
    /// </summary>
    internal sealed class PlayerOverlayRecord
    {
        private readonly string _path;
        private readonly string _round;
        private readonly string _label;
        private readonly object _lock = new object();
        private readonly int _mainThread = Thread.CurrentThread.ManagedThreadId;
        private int _seq;
        private string _stage = "start";
        private bool _capturing;

        public PlayerOverlayRecord(string path, string round, string label)
        {
            _path = path;
            _round = round;
            _label = label;
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        }

        public string Stage
        {
            get { lock (_lock) return _stage; }
            set { lock (_lock) _stage = value; }
        }

        /// <summary>1 行を書く。fields に stage があれば、その時の段の代わりにそれを書く（held の確認点の行）。</summary>
        public void Write(string type, params (string key, object? value)[] fields)
        {
            lock (_lock)
            {
                var b = new StringBuilder("{");
                void Field(string key, object? value) { if (b.Length > 1) b.Append(','); b.Append(Json(key)).Append(':').Append(Json(value)); }
                Field("at", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
                Field("seq", _seq++);
                Field("type", type);
                if (!fields.Any(x => x.key == "stage")) Field("stage", _stage);
                Field("round", _round);
                Field("label", _label);
                foreach (var (key, value) in fields) Field(key, value);
                File.AppendAllText(_path, b.Append("}\n").ToString());
            }
        }

        public void StartLogCapture()
        {
            if (_capturing) return;
            _capturing = true;
            Application.logMessageReceivedThreaded += OnLog;
        }

        public void StopLogCapture()
        {
            if (!_capturing) return;
            _capturing = false;
            Application.logMessageReceivedThreaded -= OnLog;
        }

        // 他の thread の log も来るので、行の書き込みは lock の中で行う（Write が lock を取る）。
        private void OnLog(string message, string stackTrace, LogType type)
        {
            try { Write("log", ("logType", type.ToString()), ("message", message), ("mainThread", Thread.CurrentThread.ManagedThreadId == _mainThread)); }
            catch (IOException) { /* record に書けない log は Player の log の file に残る（一次証拠）。ここで投げると Unity の log の配送を壊す。 */ }
        }

        private static string Json(object? value) => value switch
        {
            null => "null",
            bool x => x ? "true" : "false",
            string x => "\"" + Escape(x) + "\"",
            int or long or ulong or double or float => Convert.ToString(value, CultureInfo.InvariantCulture)!,
            IEnumerable x => "[" + string.Join(",", x.Cast<object?>().Select(Json)) + "]",
            _ => Json(value.ToString()),
        };

        private static string Escape(string s)
        {
            var b = new StringBuilder();
            foreach (var c in s)
                b.Append(c switch
                {
                    '"' => "\\\"", '\\' => "\\\\", '\n' => "\\n", '\r' => "\\r", '\t' => "\\t",
                    _ => c < 0x20 ? "\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture) : c.ToString(),
                });
            return b.ToString();
        }
    }
}
