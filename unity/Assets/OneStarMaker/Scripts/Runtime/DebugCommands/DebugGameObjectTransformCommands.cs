#nullable enable

using System;
using System.Globalization;
using System.Text;

namespace OneStarMaker.Runtime.DebugCommands
{
    /// <summary>
    /// go.get-transform と go.set-transform を既存の catalog へ登録する。
    /// local position / local euler / local scale だけを扱い、登録時には世界を読まない。
    /// </summary>
    public static partial class DebugGameObjectCommands
    {
        public const string GetTransformName = "go.get-transform";
        public const string SetTransformName = "go.set-transform";

        private const string ReadTransformMessage = "Read local transform.";
        private const string SetTransformMessage = "Set local transform.";

        /// <summary>
        /// get と set だけを登録する。list、select、set-active は登録しない。
        /// </summary>
        public static void RegisterTransform(
            DebugCommandCatalog catalog,
            IDebugGameObjectWorld world,
            DebugGameObjectSelection selection)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (selection == null)
            {
                throw new ArgumentNullException(nameof(selection));
            }

            catalog.Register(GetTransformName, payload => ExecuteGetTransform(payload, world, selection));
            catalog.Register(SetTransformName, payload => ExecuteSetTransform(payload, world, selection));
        }

        private static DebugCommandResult ExecuteGetTransform(
            string payload,
            IDebugGameObjectWorld world,
            DebugGameObjectSelection selection)
        {
            if (!TransformPayload.TryRead(payload, requireComponent: false, out var hasInstance, out var requestedId, out _))
            {
                return DebugCommandResult.Fail(InvalidPayloadMessage);
            }

            if (!TryResolveTarget(hasInstance, requestedId, selection, out var target))
            {
                return DebugCommandResult.Fail(NotSelectedMessage);
            }

            var read = world.TryGetTransform(target, out var value, out _);
            return FinishTransform(read, target, selection, value, ReadTransformMessage);
        }

        private static DebugCommandResult ExecuteSetTransform(
            string payload,
            IDebugGameObjectWorld world,
            DebugGameObjectSelection selection)
        {
            if (!TransformPayload.TryRead(payload, requireComponent: true, out var hasInstance, out var requestedId, out var change))
            {
                return DebugCommandResult.Fail(InvalidPayloadMessage);
            }

            if (!TryResolveTarget(hasInstance, requestedId, selection, out var target))
            {
                return DebugCommandResult.Fail(NotSelectedMessage);
            }

            var written = world.TrySetTransform(target, change, out var value, out _);
            return FinishTransform(written, target, selection, value, SetTransformMessage);
        }

        private static bool TryResolveTarget(
            bool hasInstance,
            ulong requestedId,
            DebugGameObjectSelection selection,
            out ulong target)
        {
            if (hasInstance)
            {
                target = requestedId;
                return true;
            }

            return selection.TryGet(out target);
        }

        private static DebugCommandResult FinishTransform(
            DebugGameObjectReadStatus status,
            ulong target,
            DebugGameObjectSelection selection,
            DebugGameObjectTransform value,
            string successMessage)
        {
            if (status == DebugGameObjectReadStatus.Unavailable)
            {
                return DebugCommandResult.Fail(UnavailableMessage);
            }

            if (status != DebugGameObjectReadStatus.Ok)
            {
                if (selection.TryGet(out var current) && current == target)
                {
                    selection.Clear();
                }

                return DebugCommandResult.Fail(NotAliveMessage);
            }

            return DebugCommandResult.Ok(successMessage, TransformPayload.Write(value));
        }

        /// <summary>
        /// transform 用の JSON。list / select の reader とはキー集合を共有しない。
        /// 不正な成分が1つでもあれば、世界を呼ぶ前に全体を拒否する。
        /// </summary>
        private static class TransformPayload
        {
            public static bool TryRead(
                string payload,
                bool requireComponent,
                out bool hasInstance,
                out ulong instanceId,
                out DebugGameObjectTransformChange change)
            {
                var reader = new Reader(payload, requireComponent);
                return reader.TryRead(out hasInstance, out instanceId, out change);
            }

            public static string Write(DebugGameObjectTransform value)
            {
                var builder = new StringBuilder(160);
                builder.Append("{\"instanceId\":\"");
                builder.Append(value.InstanceId.ToString(CultureInfo.InvariantCulture));
                builder.Append("\",\"localPosition\":");
                AppendVector(builder, value.LocalPosition);
                builder.Append(",\"localEulerAngles\":");
                AppendVector(builder, value.LocalEulerAngles);
                builder.Append(",\"localScale\":");
                AppendVector(builder, value.LocalScale);
                builder.Append('}');
                return builder.ToString();
            }

            private static void AppendVector(StringBuilder builder, DebugGameObjectVector value)
            {
                builder.Append("{\"x\":");
                AppendNumber(builder, value.X);
                builder.Append(",\"y\":");
                AppendNumber(builder, value.Y);
                builder.Append(",\"z\":");
                AppendNumber(builder, value.Z);
                builder.Append('}');
            }

            private static void AppendNumber(StringBuilder builder, double value)
            {
                builder.Append(value.ToString("R", CultureInfo.InvariantCulture));
            }

            private sealed class Reader
            {
                private readonly string _text;
                private readonly bool _requireComponent;
                private int _index;
                private bool _rejected;
                private bool _hasInstance;
                private ulong _instanceId;
                private bool _hasPosition;
                private bool _hasEuler;
                private bool _hasScale;
                private DebugGameObjectVector _position;
                private DebugGameObjectVector _euler;
                private DebugGameObjectVector _scale;

                public Reader(string text, bool requireComponent)
                {
                    _text = text ?? string.Empty;
                    _requireComponent = requireComponent;
                }

                public bool TryRead(out bool hasInstance, out ulong instanceId, out DebugGameObjectTransformChange change)
                {
                    hasInstance = false;
                    instanceId = 0;
                    change = default;
                    SkipWhitespace();
                    if (_index >= _text.Length)
                    {
                        return !_requireComponent;
                    }

                    if (!Take('{'))
                    {
                        return false;
                    }

                    SkipWhitespace();
                    if (!Take('}'))
                    {
                        while (true)
                        {
                            if (!TryReadString(out var key) || !SkipColon() || !TryConsumeField(key))
                            {
                                return false;
                            }

                            SkipWhitespace();
                            if (Take('}'))
                            {
                                break;
                            }

                            if (!Take(','))
                            {
                                return false;
                            }

                            SkipWhitespace();
                        }
                    }

                    SkipWhitespace();
                    if (_index < _text.Length || _rejected)
                    {
                        return false;
                    }

                    if (_requireComponent && !_hasPosition && !_hasEuler && !_hasScale)
                    {
                        return false;
                    }

                    hasInstance = _hasInstance;
                    instanceId = _instanceId;
                    change = new DebugGameObjectTransformChange(
                        _hasPosition,
                        _position,
                        _hasEuler,
                        _euler,
                        _hasScale,
                        _scale);
                    return true;
                }

                private bool TryConsumeField(string key)
                {
                    if (key == "instanceId")
                    {
                        return TakeInstance();
                    }

                    if (key == "localPosition")
                    {
                        return TakeVector(ref _hasPosition, ref _position);
                    }

                    if (key == "localEulerAngles")
                    {
                        return TakeVector(ref _hasEuler, ref _euler);
                    }

                    if (key == "localScale")
                    {
                        return TakeVector(ref _hasScale, ref _scale);
                    }

                    _rejected = true;
                    return SkipValue();
                }

                private bool TakeInstance()
                {
                    if (_hasInstance)
                    {
                        _rejected = true;
                    }

                    if (Peek('"'))
                    {
                        if (!TryReadString(out var text))
                        {
                            return false;
                        }

                        if (ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
                        {
                            _instanceId = parsed;
                            _hasInstance = true;
                        }
                        else
                        {
                            _rejected = true;
                        }

                        return true;
                    }

                    _rejected = true;
                    return SkipValue();
                }

                private bool TakeVector(ref bool hasComponent, ref DebugGameObjectVector destination)
                {
                    if (!_requireComponent || hasComponent)
                    {
                        _rejected = true;
                        return SkipValue();
                    }

                    if (!Peek('{'))
                    {
                        _rejected = true;
                        return SkipValue();
                    }

                    if (!TryReadVector(out var vector, out var valid))
                    {
                        return false;
                    }

                    if (!valid)
                    {
                        _rejected = true;
                        return true;
                    }

                    destination = vector;
                    hasComponent = true;
                    return true;
                }

                private bool TryReadVector(out DebugGameObjectVector vector, out bool valid)
                {
                    vector = default;
                    valid = false;
                    if (!Take('{'))
                    {
                        return false;
                    }

                    var hasX = false;
                    var hasY = false;
                    var hasZ = false;
                    var bad = false;
                    double x = 0;
                    double y = 0;
                    double z = 0;
                    SkipWhitespace();
                    if (Take('}'))
                    {
                        return true;
                    }

                    while (true)
                    {
                        if (!TryReadString(out var key) || !SkipColon())
                        {
                            return false;
                        }

                        if (!TryReadFinite(out var number, out var accepted))
                        {
                            return false;
                        }

                        if (!accepted)
                        {
                            bad = true;
                        }
                        else if (key == "x" && !hasX)
                        {
                            x = number;
                            hasX = true;
                        }
                        else if (key == "y" && !hasY)
                        {
                            y = number;
                            hasY = true;
                        }
                        else if (key == "z" && !hasZ)
                        {
                            z = number;
                            hasZ = true;
                        }
                        else
                        {
                            bad = true;
                        }

                        SkipWhitespace();
                        if (Take('}'))
                        {
                            valid = !bad && hasX && hasY && hasZ;
                            if (valid)
                            {
                                vector = new DebugGameObjectVector(x, y, z);
                            }

                            return true;
                        }

                        if (!Take(','))
                        {
                            return false;
                        }

                        SkipWhitespace();
                    }
                }

                private bool TryReadFinite(out double number, out bool accepted)
                {
                    number = 0;
                    accepted = false;
                    if (Peek('"') || Peek('{') || Peek('['))
                    {
                        return SkipValue();
                    }

                    if (TryTakeLiteral("true") || TryTakeLiteral("false") || TryTakeLiteral("null"))
                    {
                        return true;
                    }

                    var start = _index;
                    if (!SkipNumber())
                    {
                        return false;
                    }

                    var token = _text.Substring(start, _index - start);
                    if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                        || double.IsNaN(parsed)
                        || double.IsInfinity(parsed))
                    {
                        return true;
                    }

                    var narrowed = (float)parsed;
                    if (float.IsNaN(narrowed) || float.IsInfinity(narrowed))
                    {
                        return true;
                    }

                    number = parsed;
                    accepted = true;
                    return true;
                }

                private bool SkipColon()
                {
                    SkipWhitespace();
                    if (!Take(':'))
                    {
                        return false;
                    }

                    SkipWhitespace();
                    return true;
                }

                private bool SkipValue()
                {
                    if (Peek('"'))
                    {
                        return TryReadString(out _);
                    }

                    if (Peek('{'))
                    {
                        return SkipBlock('{', '}');
                    }

                    if (Peek('['))
                    {
                        return SkipBlock('[', ']');
                    }

                    if (TryTakeLiteral("true") || TryTakeLiteral("false") || TryTakeLiteral("null"))
                    {
                        return true;
                    }

                    return SkipNumber();
                }

                private bool SkipBlock(char open, char close)
                {
                    if (!Take(open))
                    {
                        return false;
                    }

                    var depth = 1;
                    var inString = false;
                    var escaped = false;
                    while (_index < _text.Length && depth > 0)
                    {
                        var current = _text[_index++];
                        if (inString)
                        {
                            if (escaped)
                            {
                                escaped = false;
                            }
                            else if (current == '\\')
                            {
                                escaped = true;
                            }
                            else if (current == '"')
                            {
                                inString = false;
                            }

                            continue;
                        }

                        if (current == '"')
                        {
                            inString = true;
                        }
                        else if (current == open)
                        {
                            depth++;
                        }
                        else if (current == close)
                        {
                            depth--;
                        }
                    }

                    return depth == 0;
                }

                private bool SkipNumber()
                {
                    var start = _index;
                    if (Peek('-'))
                    {
                        _index++;
                    }

                    if (!PeekDigit())
                    {
                        _index = start;
                        return false;
                    }

                    if (_text[_index] == '0')
                    {
                        _index++;
                    }
                    else
                    {
                        while (PeekDigit())
                        {
                            _index++;
                        }
                    }

                    if (Peek('.'))
                    {
                        _index++;
                        if (!PeekDigit())
                        {
                            _index = start;
                            return false;
                        }

                        while (PeekDigit())
                        {
                            _index++;
                        }
                    }

                    if (Peek('e') || Peek('E'))
                    {
                        _index++;
                        if (Peek('+') || Peek('-'))
                        {
                            _index++;
                        }

                        if (!PeekDigit())
                        {
                            _index = start;
                            return false;
                        }

                        while (PeekDigit())
                        {
                            _index++;
                        }
                    }

                    if (start < _text.Length && _text[start] == '0' && _index - start > 1 && char.IsDigit(_text[start + 1]))
                    {
                        _index = start;
                        return false;
                    }

                    if (_text[start] == '-' && start + 1 < _text.Length && _text[start + 1] == '0' && _index - start > 2 && char.IsDigit(_text[start + 2]))
                    {
                        _index = start;
                        return false;
                    }

                    return true;
                }

                private bool TryReadString(out string value)
                {
                    value = string.Empty;
                    if (!Take('"'))
                    {
                        return false;
                    }

                    var builder = new StringBuilder();
                    while (_index < _text.Length)
                    {
                        var current = _text[_index++];
                        if (current == '"')
                        {
                            value = builder.ToString();
                            return true;
                        }

                        if (current == '\\')
                        {
                            if (_index >= _text.Length)
                            {
                                return false;
                            }

                            var escaped = _text[_index++];
                            switch (escaped)
                            {
                                case '"':
                                case '\\':
                                case '/':
                                    builder.Append(escaped);
                                    break;
                                case 'b':
                                    builder.Append('\b');
                                    break;
                                case 'f':
                                    builder.Append('\f');
                                    break;
                                case 'n':
                                    builder.Append('\n');
                                    break;
                                case 'r':
                                    builder.Append('\r');
                                    break;
                                case 't':
                                    builder.Append('\t');
                                    break;
                                case 'u':
                                    if (!TryReadHex4(out var scalar))
                                    {
                                        return false;
                                    }

                                    builder.Append((char)scalar);
                                    break;
                                default:
                                    return false;
                            }

                            continue;
                        }

                        if (current < ' ')
                        {
                            return false;
                        }

                        builder.Append(current);
                    }

                    return false;
                }

                private bool TryReadHex4(out int scalar)
                {
                    scalar = 0;
                    if (_index + 4 > _text.Length)
                    {
                        return false;
                    }

                    for (var place = 0; place < 4; place++)
                    {
                        var hex = _text[_index++];
                        int digit;
                        if (hex >= '0' && hex <= '9')
                        {
                            digit = hex - '0';
                        }
                        else if (hex >= 'a' && hex <= 'f')
                        {
                            digit = hex - 'a' + 10;
                        }
                        else if (hex >= 'A' && hex <= 'F')
                        {
                            digit = hex - 'A' + 10;
                        }
                        else
                        {
                            return false;
                        }

                        scalar = (scalar << 4) + digit;
                    }

                    return true;
                }

                private bool TryTakeLiteral(string literal)
                {
                    if (_index + literal.Length > _text.Length)
                    {
                        return false;
                    }

                    for (var place = 0; place < literal.Length; place++)
                    {
                        if (_text[_index + place] != literal[place])
                        {
                            return false;
                        }
                    }

                    var end = _index + literal.Length;
                    if (end < _text.Length && IsTokenChar(_text[end]))
                    {
                        return false;
                    }

                    _index = end;
                    return true;
                }

                private static bool IsTokenChar(char value)
                {
                    return (value >= 'a' && value <= 'z') || (value >= 'A' && value <= 'Z') || (value >= '0' && value <= '9') || value == '_';
                }

                private bool Take(char expected)
                {
                    if (_index >= _text.Length || _text[_index] != expected)
                    {
                        return false;
                    }

                    _index++;
                    return true;
                }

                private bool Peek(char expected)
                {
                    return _index < _text.Length && _text[_index] == expected;
                }

                private bool PeekDigit()
                {
                    return _index < _text.Length && _text[_index] >= '0' && _text[_index] <= '9';
                }

                private void SkipWhitespace()
                {
                    while (_index < _text.Length)
                    {
                        var current = _text[_index];
                        if (current != ' ' && current != '\t' && current != '\n' && current != '\r')
                        {
                            return;
                        }

                        _index++;
                    }
                }
            }
        }
    }
}
