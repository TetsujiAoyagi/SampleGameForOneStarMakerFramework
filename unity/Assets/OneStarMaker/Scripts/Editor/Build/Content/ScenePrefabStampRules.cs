#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace OneStarMaker.Editor.Build.Content
{
    // 閉包の 1 つの path について、AssetDatabase から引いた「印の対象か」と「鍵の値」。
    // 規則はこの組だけから印の値を決め、asset の file も AssetDatabase も読まない（単体 test の境界）。
    internal readonly struct StampProbe
    {
        public StampProbe(bool isTarget, string key) { IsTarget = isTarget; Key = key; }
        public bool IsTarget { get; }
        public string Key { get; }
    }

    // Scene の .meta の userData の値の分類。Other は「止める理由」になる（自分の印でない値、複数行の値、行が無い・2 行以上）。
    internal enum UserDataState { Empty, OwnStamp, Other }

    // .meta の text の中の userData の行。値は "userData:" の直後から行末（改行の前）まで。
    internal readonly struct UserDataLine
    {
        public UserDataLine(UserDataState state, int valueStart, int valueLength, string value)
        { State = state; ValueStart = valueStart; ValueLength = valueLength; Value = value; }
        public UserDataState State { get; }
        public int ValueStart { get; }
        public int ValueLength { get; }
        public string Value { get; }
    }

    // 印を戻す時の判断。AlreadyOriginal は書く物が無い（build が書いた分を誰かが既に戻した、または書けなかった）。
    internal enum StampRestoreKind { AlreadyOriginal, RestoreOriginal, RestoreLineOnly, LeaveForeign, Missing }

    // Scene に焼き込む prefab の版の印の純粋な規則。
    // 印は build の間だけ Scene の .meta の userData に書き、build の後に元のバイトへ戻す（HANDOFF CONTENT_PREFAB_STAMP §2.7）。
    // Unity の差分 build は Scene の file と .meta のバイトを再利用の鍵にし、prefab の状態を鍵に入れないため、
    // prefab の版を .meta に一時的に写して Scene を作り直させる。
    internal static class ScenePrefabStampRules
    {
        // 印の文字列は「接頭辞 + 元の行の末尾の空白の有無（n/s）+ 64 桁の小文字 16 進」。英数字と '-' だけにして、
        // 引用なしの YAML の scalar として有効にし、Unity が .meta を書き直す理由を作らない。
        // n/s は 16 進に無い文字なので、接頭辞の後の 1 文字で一意に読める。
        internal const string Prefix = "osm-prefab-stamp-";
        private const char NoTrailingSpace = 'n';
        private const char TrailingSpace = 's';
        private const string Key = "userData:";
        private const int HashLength = 64;

        public static bool IsScene(string path) => path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);

        // 閉包の path の列から印の値を作る。印の対象が 1 つも無ければ null（その Scene には印を書かない）。
        // path の序数順に「path と鍵」を連ねるので、閉包の並びの順に依らない。鍵の値が PC ごとに違っても、
        // cache が当たらないだけで出力の正しさは変わらない。
        public static string? StampHash(IEnumerable<string> closurePaths, Func<string, StampProbe> probe)
        {
            if (closurePaths == null) throw new ArgumentNullException(nameof(closurePaths));
            if (probe == null) throw new ArgumentNullException(nameof(probe));
            var text = new StringBuilder();
            var any = false;
            foreach (var path in closurePaths.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
            {
                var result = probe(path);
                if (!result.IsTarget) continue;
                any = true;
                text.Append(path).Append('\n').Append(result.Key).Append('\n');
            }
            return any ? Sha256Hex(Encoding.UTF8.GetBytes(text.ToString())) : null;
        }

        public static string Sha256Hex(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(bytes).Select(x => x.ToString("x2")));
        }

        // .meta を 1 バイト = 1 文字の text として扱う。文字コードの解釈を挟まないので、userData の行以外のバイト
        // （BOM、改行の形、ASCII でない文字）を変えずに往復できる。
        public static string ToText(byte[] bytes)
        {
            var chars = new char[bytes.Length];
            for (var i = 0; i < bytes.Length; i++) chars[i] = (char)bytes[i];
            return new string(chars);
        }

        public static byte[] ToBytes(string text)
        {
            var bytes = new byte[text.Length];
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] > 0xFF) throw new ArgumentException("Text is not a byte-preserving .meta text.", nameof(text));
                bytes[i] = (byte)text[i];
            }
            return bytes;
        }

        // userData の行を探して値を分類する。行は先頭の空白の後が "userData:" で始まる物で、ちょうど 1 行でなければ Other。
        // 空の形は「直後が行末」か「空白 1 つで行末」だけにする（印を戻す時に元の行がバイトで一意に決まる）。
        public static UserDataLine FindUserData(string text)
        {
            var found = 0;
            var valueStart = -1;
            var valueLength = 0;
            var position = 0;
            while (position <= text.Length)
            {
                var end = text.IndexOf('\n', position);
                var lineEnd = end < 0 ? text.Length : end;
                var contentEnd = lineEnd > position && text[lineEnd - 1] == '\r' ? lineEnd - 1 : lineEnd;
                var indent = position;
                while (indent < contentEnd && text[indent] == ' ') indent++;
                if (indent + Key.Length <= contentEnd && string.CompareOrdinal(text, indent, Key, 0, Key.Length) == 0)
                {
                    found++;
                    valueStart = indent + Key.Length;
                    valueLength = contentEnd - valueStart;
                }
                if (end < 0) break;
                position = end + 1;
            }
            if (found != 1) return new UserDataLine(UserDataState.Other, -1, 0, "");
            var value = text.Substring(valueStart, valueLength);
            var state = value.Length == 0 || value == " " ? UserDataState.Empty
                : IsOwnStampValue(value) ? UserDataState.OwnStamp : UserDataState.Other;
            return new UserDataLine(state, valueStart, valueLength, value);
        }

        // userData の値（"userData:" の直後から行末まで）が自分の印の形か。接頭辞が同じでも形が違えば false。
        public static bool IsOwnStampValue(string value)
        {
            var stamp = " " + Prefix;
            if (value.Length != stamp.Length + 1 + HashLength || !value.StartsWith(stamp, StringComparison.Ordinal)) return false;
            var flag = value[stamp.Length];
            if (flag != NoTrailingSpace && flag != TrailingSpace) return false;
            for (var i = stamp.Length + 1; i < value.Length; i++)
            {
                var c = value[i];
                if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')) return false;
            }
            return true;
        }

        // 空の userData の行の値だけを印に置き換える。他の行と改行の形は変えない。
        public static string WriteStamp(string text, string hash)
        {
            if (hash == null || hash.Length != HashLength || hash.Any(c => !(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')))
                throw new ArgumentException("Stamp hash must be 64 lowercase hex digits.", nameof(hash));
            var line = FindUserData(text);
            if (line.State != UserDataState.Empty) throw new InvalidOperationException("userData is not empty: " + line.State);
            var flag = line.Value.Length == 0 ? NoTrailingSpace : TrailingSpace;
            return Replace(text, line, " " + Prefix + flag + hash);
        }

        // 自分の印の行だけを元の空の形（印が持つ末尾の空白の有無）に戻す。他の行は触らない。
        public static string RestoreStampLine(string text)
        {
            var line = FindUserData(text);
            if (line.State != UserDataState.OwnStamp) throw new InvalidOperationException("userData is not this build's stamp: " + line.State);
            var flag = line.Value[1 + Prefix.Length];
            return Replace(text, line, flag == TrailingSpace ? " " : "");
        }

        // 戻しの判断（build の後と、次の build の始めで同じ規則）。
        // (1) build が書いた後のバイトのままなら元のバイトに戻す。既に元のバイトなら書かない。
        // (2) 違っても userData の行が自分の印なら、その行だけを戻す（build の間に他の行を変えた人の編集を残す）。
        // (3) 自分の印でなければ触らない。(4) file が無ければ何もしない。(2)〜(4) は呼び手が Warning を出す。
        public static StampRestoreKind DecideRestore(bool exists, string currentSha256, string originalSha256,
            string writtenSha256, UserDataState currentState)
        {
            if (!exists) return StampRestoreKind.Missing;
            if (currentSha256 == writtenSha256) return StampRestoreKind.RestoreOriginal;
            if (currentSha256 == originalSha256) return StampRestoreKind.AlreadyOriginal;
            return currentState == UserDataState.OwnStamp ? StampRestoreKind.RestoreLineOnly : StampRestoreKind.LeaveForeign;
        }

        // 判断に従って戻した後の text。null は書かない（今の text のまま）。
        public static string? Restored(StampRestoreKind kind, string current, string original) => kind switch
        {
            StampRestoreKind.RestoreOriginal => original,
            StampRestoreKind.RestoreLineOnly => RestoreStampLine(current),
            _ => null,
        };

        // 前回の印が残った Scene を今回も印の対象にする時の「元の text」。戻した後の text を file に書かず import もせずに
        // 今回の記録の元にし、file には今回の印だけを書く（中間の書き込みと import を挟まない）。
        public static string EffectiveOriginal(StampRestoreKind kind, string current, string original) =>
            Restored(kind, current, original) ?? current;

        private static string Replace(string text, UserDataLine line, string value) =>
            text.Substring(0, line.ValueStart) + value + text.Substring(line.ValueStart + line.ValueLength);
    }
}
