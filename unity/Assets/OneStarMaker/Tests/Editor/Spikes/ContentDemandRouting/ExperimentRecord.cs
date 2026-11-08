#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.Spikes.ContentDemandRouting
{
    /// <summary>判定の最小単位（HANDOFF §2 cell）を record の各行に添える識別子。</summary>
    [Serializable]
    internal sealed class Cell
    {
        public string mechanism = "";
        public string epm = "";
        public string scenario = "";
        public int iteration;
        public int play;

        internal static Cell Of(string mechanism, string epm, string scenario, int iteration = 0, int play = 0)
            => new() { mechanism = mechanism, epm = epm, scenario = scenario, iteration = iteration, play = play };
    }

    /// <summary>
    /// 実験の record。1 行 1 観測の JSON Lines を test の出力 folder（repo の TestResults）に追記する。
    /// Domain Reload で managed の状態が消えても観測を失わないよう、行ごとに file へ書いて閉じる。
    /// 時刻順は file の行順と seq で表す。
    /// seq を file の行番号と一致させるため、同じ domain の中では path ごとに 1 つの instance を共有する。
    /// Play 中に作った instance（RoutingBackend・DirectorHarness が持つ）と Edit で開き直した instance が別々に seq を持つと、
    /// 同じ file へ交互に書いた時に seq が重複・逆転する（AC11・U-b4 は seq で順序を読む）。Domain Reload で共有が消えた後は、
    /// 最初に開いた時に既存の行数から再開する。
    /// cell の値（PASS / FAIL / UNTESTED / N/A）はここで決めない。判断担当が行を読んで決める（HANDOFF §5）。
    /// </summary>
    internal sealed class ExperimentRecord
    {
        private static readonly Dictionary<string, ExperimentRecord> Shared = new(StringComparer.OrdinalIgnoreCase);

        private long _seq;

        private ExperimentRecord(string path)
        {
            FilePath = path;
            _seq = File.Exists(path) ? File.ReadAllLines(path).Length : 0;
        }

        internal string FilePath { get; }
        internal long LineCount => _seq;

        internal static string OutputRoot()
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "TestResults", "ContentDemandRouting"));

        internal static ExperimentRecord Open(string path)
        {
            var fullPath = Path.GetFullPath(path);
            if (Shared.TryGetValue(fullPath, out var shared)) return shared;
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            var record = new ExperimentRecord(fullPath);
            Shared[fullPath] = record;
            return record;
        }

        internal void Write(Cell? cell, string kind, params (string Key, object? Value)[] fields)
        {
            var builder = new StringBuilder(256);
            builder.Append('{');
            AppendPair(builder, "seq", ++_seq, true);
            AppendPair(builder, "utc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture), false);
            AppendPair(builder, "frame", Time.frameCount, false);
            AppendPair(builder, "playing", Application.isPlaying, false);
            AppendPair(builder, "kind", kind, false);
            if (cell != null)
            {
                AppendPair(builder, "mechanism", cell.mechanism, false);
                AppendPair(builder, "epm", cell.epm, false);
                AppendPair(builder, "scenario", cell.scenario, false);
                AppendPair(builder, "iteration", cell.iteration, false);
                AppendPair(builder, "play", cell.play, false);
            }
            foreach (var (key, value) in fields) AppendPair(builder, key, value, false);
            builder.Append('}');
            builder.Append('\n');
            File.AppendAllText(FilePath, builder.ToString(), new UTF8Encoding(false));
        }

        private static void AppendPair(StringBuilder builder, string key, object? value, bool first)
        {
            if (!first) builder.Append(',');
            AppendString(builder, key);
            builder.Append(':');
            AppendValue(builder, value, 0);
        }

        private static void AppendValue(StringBuilder builder, object? value, int depth)
        {
            // 観測の値に循環は無い想定だが、誤って Unity の Object を渡した時に無限に辿らない。
            if (depth > 8) { AppendString(builder, "<depth-limit>"); return; }
            switch (value)
            {
                case null: builder.Append("null"); return;
                case string s: AppendString(builder, s); return;
                case bool b: builder.Append(b ? "true" : "false"); return;
                case int or long or short or byte or uint or ulong:
                    builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture)); return;
                case float f: builder.Append(f.ToString("R", CultureInfo.InvariantCulture)); return;
                case double d: builder.Append(d.ToString("R", CultureInfo.InvariantCulture)); return;
                case Enum e: AppendString(builder, e.ToString()); return;
                case Exception ex: AppendString(builder, ex.GetType().FullName + ": " + ex.Message); return;
                case IDictionary<string, object?> map:
                    builder.Append('{');
                    var firstEntry = true;
                    foreach (var pair in map)
                    {
                        if (!firstEntry) builder.Append(',');
                        firstEntry = false;
                        AppendString(builder, pair.Key);
                        builder.Append(':');
                        AppendValue(builder, pair.Value, depth + 1);
                    }
                    builder.Append('}');
                    return;
                case IEnumerable sequence:
                    builder.Append('[');
                    var firstItem = true;
                    foreach (var item in sequence)
                    {
                        if (!firstItem) builder.Append(',');
                        firstItem = false;
                        AppendValue(builder, item, depth + 1);
                    }
                    builder.Append(']');
                    return;
                default:
                    AppendString(builder, value.ToString() ?? string.Empty); return;
            }
        }

        private static void AppendString(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (var ch in value)
            {
                switch (ch)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (ch < 0x20) builder.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                        else builder.Append(ch);
                        break;
                }
            }
            builder.Append('"');
        }
    }
}
