#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OneStarMaker.Runtime.BuildContent.Distribution
{
    internal static class ContentManifestValidation
    {
        internal const int MaxManifestBytes = 4 * 1024 * 1024;
        internal const int MaxFiles = 100000;
        internal const string SupportedUnityVersion = "6000.6.0f1";
        internal const int SupportedRootSchemaVersion = 2;

        internal static void RequireObjectFields(string json, params string[] names)
            => RequireFields(new JsonShapeReader(json).ReadObject(), names);

        internal static void ValidateRequiredFields(string json)
        {
            // JsonUtility の既定値では欠落 size と正当な 0 byte を区別できない。
            // raw JSON の構造だけを先に検査し、hash はこの再表現から計算しない。
            var root = new JsonShapeReader(json).ReadObject();
            RequireFields(root, "version", "product", "contentSet", "revision", "target", "unityVersion",
                "rootSchemaVersion", "playerConfigSchemaVersion", "files", "sourceFiles");
            foreach (var key in new[] { "version", "rootSchemaVersion", "playerConfigSchemaVersion" })
                RequireInteger(root[key]);
            foreach (var key in new[] { "product", "contentSet", "revision", "target", "unityVersion" })
                if (!(root[key] is string)) Fail(ContentDeliveryFailureCode.InvalidManifest, "Manifest field must be a string: " + key);
            ValidateEntries(root["files"], true);
            ValidateEntries(root["sourceFiles"], false);
        }

        private static void ValidateEntries(object? value, bool payload)
        {
            if (!(value is List<object?> entries))
                throw new ContentDeliveryException(ContentDeliveryFailureCode.InvalidManifest, "Manifest files must be arrays.");
            foreach (var entry in entries)
            {
                if (!(entry is Dictionary<string, object?> fields))
                    throw new ContentDeliveryException(ContentDeliveryFailureCode.InvalidManifest, "Manifest file must be an object.");
                RequireFields(fields, "path", "sha256");
                if (!(fields["path"] is string) || !(fields["sha256"] is string))
                    Fail(ContentDeliveryFailureCode.InvalidManifest, "File path and hash must be strings.");
                if (payload)
                {
                    RequireFields(fields, "size");
                    RequireInteger(fields["size"]);
                }
            }
        }

        private static void RequireInteger(object? value)
        {
            if (!(value is JsonNumber number) || !long.TryParse(number.Text,
                    System.Globalization.NumberStyles.AllowLeadingSign,
                    System.Globalization.CultureInfo.InvariantCulture, out _))
                Fail(ContentDeliveryFailureCode.InvalidManifest, "Manifest integer is missing or out of range.");
        }

        private static void RequireFields(Dictionary<string, object?> fields, params string[] names)
        {
            foreach (var name in names)
                if (!fields.ContainsKey(name))
                    Fail(ContentDeliveryFailureCode.InvalidManifest, "Required metadata field is missing: " + name);
        }

        internal static ValidatedContentManifest Validate(ContentTransportManifest value, string digest, ContentInstallRequest request)
        {
            if (value.version != 1 || value.product != "OneStarMaker" || value.playerConfigSchemaVersion != 1)
                Fail(ContentDeliveryFailureCode.CompatibilityMismatch, "Unsupported transport protocol.");
            if (value.unityVersion != SupportedUnityVersion || value.rootSchemaVersion != SupportedRootSchemaVersion)
                Fail(ContentDeliveryFailureCode.CompatibilityMismatch, "Manifest is not compatible with this runtime.");
            if (!Segment(value.contentSet) || !Segment(value.revision))
                Fail(ContentDeliveryFailureCode.InvalidManifest, "Content set or revision is invalid.");
            if (value.contentSet != request.ContentSet || value.revision != request.Revision)
                Fail(ContentDeliveryFailureCode.IdentityMismatch, "Requested content identity does not match the manifest.");
            if (value.target != request.Target)
                Fail(ContentDeliveryFailureCode.TargetMismatch, "Requested target does not match the manifest.");
            if (value.unityVersion != request.UnityVersion || value.rootSchemaVersion != request.RootSchemaVersion)
                Fail(ContentDeliveryFailureCode.CompatibilityMismatch, "Manifest compatibility does not match this consumer.");
            if (!Hash(digest) || digest != request.ManifestSha256)
                Fail(ContentDeliveryFailureCode.IntegrityMismatch, "Manifest digest does not match the requested bytes.");
            if (value.files == null || value.files.Length == 0 || value.files.Length > MaxFiles
                || value.sourceFiles == null || value.sourceFiles.Length > MaxFiles)
                Fail(ContentDeliveryFailureCode.InvalidManifest, "Manifest file count is invalid.");

            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long total = 0;
            foreach (var file in value.files)
            {
                if (file == null || !Relative(file.path) || file.size < 0 || !Hash(file.sha256) || !paths.Add(file.path))
                    Fail(ContentDeliveryFailureCode.InvalidManifest, "Manifest file entry is invalid.");
                try { total = checked(total + file.size); }
                catch (OverflowException) { Fail(ContentDeliveryFailureCode.InvalidManifest, "Manifest byte total overflowed."); }
            }
            // 各 prefix を照合するため file 数の二乗の探索を避ける。親が file なら作成順で救済しない。
            foreach (var path in paths)
                for (var slash = path.IndexOf('/'); slash >= 0; slash = path.IndexOf('/', slash + 1))
                    if (paths.Contains(path.Substring(0, slash)))
                        Fail(ContentDeliveryFailureCode.InvalidManifest, "Manifest contains a file/directory collision.");
            foreach (var file in value.sourceFiles)
                if (file == null || string.IsNullOrEmpty(file.path)
                    || !(file.path.StartsWith("Assets/", StringComparison.Ordinal) || file.path.StartsWith("Packages/", StringComparison.Ordinal))
                    || !Relative(file.path) || !Hash(file.sha256))
                    Fail(ContentDeliveryFailureCode.InvalidManifest, "Source file entry is invalid.");
            if (request.DiskBudgetBytes <= 0 || total > request.DiskBudgetBytes)
                throw new ContentDeliveryException(ContentDeliveryFailureCode.BudgetUnsatisfied, "Manifest exceeds the disk budget.",
                    requiredBytes: total, availableBytes: request.DiskBudgetBytes);
            return new ValidatedContentManifest(digest, value, (ContentTransportFile[])value.files.Clone(), total);
        }

        internal static bool Relative(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\')
                || Uri.UnescapeDataString(path) != path) return false;
            return path.Split('/').All(part => part.Length != 0 && part != "." && part != ".."
                && !part.EndsWith(".", StringComparison.Ordinal) && !part.EndsWith(" ", StringComparison.Ordinal)
                && part.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && !Reserved(part));
        }

        private static bool Reserved(string value)
        {
            var stem = value.Split('.')[0];
            return new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }.Contains(stem, StringComparer.OrdinalIgnoreCase);
        }

        internal static bool Hash(string value)
            => value != null && value.Length == 64 && value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');

        internal static bool Segment(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 128 || value == "." || value == ".."
                || value.EndsWith(".", StringComparison.Ordinal)) return false;
            return value.All(c => c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c is '-' or '_' or '.')
                && !Reserved(value);
        }

        private static void Fail(ContentDeliveryFailureCode code, string message) => throw new ContentDeliveryException(code, message);

        private sealed class JsonNumber
        {
            internal JsonNumber(string text) => Text = text;
            internal string Text { get; }
        }

        // protocol DTO とは分離した presence/type 検査。未知 optional field は読み飛ばせるが、
        // duplicate key と過深な構造は parser 間の解釈差を作るため受け付けない。
        private sealed class JsonShapeReader
        {
            private readonly string _json;
            private int _position;
            internal JsonShapeReader(string json) => _json = json;

            internal Dictionary<string, object?> ReadObject()
            {
                var value = ReadValue(0);
                WhiteSpace();
                if (_position != _json.Length || !(value is Dictionary<string, object?> result))
                    throw Invalid();
                return result;
            }

            private object? ReadValue(int depth)
            {
                if (depth > 64) throw Invalid();
                WhiteSpace();
                if (_position >= _json.Length) throw Invalid();
                switch (_json[_position])
                {
                    case '{':
                        _position++;
                        var fields = new Dictionary<string, object?>(StringComparer.Ordinal);
                        if (Take('}')) return fields;
                        do
                        {
                            WhiteSpace();
                            var key = ReadString();
                            if (!Take(':') || fields.ContainsKey(key)) throw Invalid();
                            fields.Add(key, ReadValue(depth + 1));
                        } while (Take(','));
                        if (!Take('}')) throw Invalid();
                        return fields;
                    case '[':
                        _position++;
                        var values = new List<object?>();
                        if (Take(']')) return values;
                        do { values.Add(ReadValue(depth + 1)); } while (Take(','));
                        if (!Take(']')) throw Invalid();
                        return values;
                    case '"': return ReadString();
                    case 't': Literal("true"); return true;
                    case 'f': Literal("false"); return false;
                    case 'n': Literal("null"); return null;
                    default: return ReadNumber();
                }
            }

            private JsonNumber ReadNumber()
            {
                var start = _position;
                if (_json[_position] == '-') _position++;
                if (_position >= _json.Length) throw Invalid();
                if (_json[_position] == '0') _position++;
                else
                {
                    if (_json[_position] < '1' || _json[_position] > '9') throw Invalid();
                    Digits();
                }
                if (_position < _json.Length && _json[_position] == '.')
                {
                    _position++;
                    var digits = _position;
                    Digits();
                    if (_position == digits) throw Invalid();
                }
                if (_position < _json.Length && (_json[_position] == 'e' || _json[_position] == 'E'))
                {
                    _position++;
                    if (_position < _json.Length && (_json[_position] == '+' || _json[_position] == '-')) _position++;
                    var digits = _position;
                    Digits();
                    if (_position == digits) throw Invalid();
                }
                return new JsonNumber(_json.Substring(start, _position - start));
            }

            private void Digits()
            {
                while (_position < _json.Length && _json[_position] >= '0' && _json[_position] <= '9') _position++;
            }

            private string ReadString()
            {
                if (_position >= _json.Length || _json[_position++] != '"') throw Invalid();
                var text = new StringBuilder();
                while (_position < _json.Length)
                {
                    var c = _json[_position++];
                    if (c == '"') return text.ToString();
                    if (c < 0x20) throw Invalid();
                    if (c != '\\') { text.Append(c); continue; }
                    if (_position >= _json.Length) throw Invalid();
                    c = _json[_position++];
                    switch (c)
                    {
                        case '"': case '\\': case '/': text.Append(c); break;
                        case 'b': text.Append('\b'); break;
                        case 'f': text.Append('\f'); break;
                        case 'n': text.Append('\n'); break;
                        case 'r': text.Append('\r'); break;
                        case 't': text.Append('\t'); break;
                        case 'u':
                            if (_position + 4 > _json.Length || !ushort.TryParse(_json.Substring(_position, 4),
                                    System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var code))
                                throw Invalid();
                            text.Append((char)code);
                            _position += 4;
                            break;
                        default: throw Invalid();
                    }
                }
                throw Invalid();
            }

            private void Literal(string literal)
            {
                if (_position + literal.Length > _json.Length
                    || string.CompareOrdinal(_json, _position, literal, 0, literal.Length) != 0) throw Invalid();
                _position += literal.Length;
            }

            private bool Take(char expected)
            {
                WhiteSpace();
                if (_position >= _json.Length || _json[_position] != expected) return false;
                _position++;
                return true;
            }

            private void WhiteSpace()
            {
                while (_position < _json.Length && _json[_position] is ' ' or '\r' or '\n' or '\t') _position++;
            }

            private static ContentDeliveryException Invalid()
                => new(ContentDeliveryFailureCode.InvalidManifest, "Metadata JSON structure is invalid.");
        }
    }
}
