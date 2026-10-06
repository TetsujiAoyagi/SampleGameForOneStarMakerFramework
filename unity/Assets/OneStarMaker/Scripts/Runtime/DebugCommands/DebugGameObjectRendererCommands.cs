#nullable enable

using System;
using System.Globalization;
using System.Text;

namespace OneStarMaker.Runtime.DebugCommands
{
    /// <summary>
    /// go.set-renderer-enabled を既存の catalog へ登録する。
    /// 対象 GameObject 自身の Renderer を 1 件だけ変え、登録時には世界を読まない。
    /// </summary>
    public static partial class DebugGameObjectCommands
    {
        public const string SetRendererEnabledName = "go.set-renderer-enabled";

        private const string SetRendererMessage = "Set Renderer enabled.";
        private const string RendererMissingMessage = "Renderer was not found.";

        /// <summary>
        /// set-renderer-enabled だけを登録する。list、select、set-active、transform は登録しない。
        /// </summary>
        public static void RegisterRenderer(
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

            catalog.Register(SetRendererEnabledName, payload => ExecuteSetRenderer(payload, world, selection));
        }

        private static DebugCommandResult ExecuteSetRenderer(
            string payload,
            IDebugGameObjectWorld world,
            DebugGameObjectSelection selection)
        {
            if (!RendererPayload.TryRead(payload, out var hasInstance, out var requestedId, out var rendererIndex, out var enabled))
            {
                return DebugCommandResult.Fail(InvalidPayloadMessage);
            }

            if (!TryResolveTarget(hasInstance, requestedId, selection, out var target))
            {
                return DebugCommandResult.Fail(NotSelectedMessage);
            }

            var changed = world.TrySetRendererEnabled(target, rendererIndex, enabled, out var value, out _);
            if (changed == DebugGameObjectReadStatus.Unavailable)
            {
                return DebugCommandResult.Fail(UnavailableMessage);
            }

            if (changed == DebugGameObjectReadStatus.MissingRenderer)
            {
                return DebugCommandResult.Fail(RendererMissingMessage);
            }

            if (changed != DebugGameObjectReadStatus.Ok)
            {
                if (selection.TryGet(out var current) && current == target)
                {
                    selection.Clear();
                }

                return DebugCommandResult.Fail(NotAliveMessage);
            }

            return DebugCommandResult.Ok(SetRendererMessage, RendererPayload.Write(value));
        }

        /// <summary>
        /// Renderer 用の JSON。list と transform の reader とはキー集合を共有しない。
        /// 負の index は世界を呼ぶ前に拒否する。
        /// </summary>
        private static class RendererPayload
        {
            public static bool TryRead(
                string payload,
                out bool hasInstance,
                out ulong instanceId,
                out int rendererIndex,
                out bool enabled)
            {
                var reader = new Reader(payload);
                return reader.TryRead(out hasInstance, out instanceId, out rendererIndex, out enabled);
            }

            public static string Write(DebugGameObjectRenderer value)
            {
                var builder = new StringBuilder(96);
                builder.Append("{\"instanceId\":\"");
                builder.Append(value.InstanceId.ToString(CultureInfo.InvariantCulture));
                builder.Append("\",\"rendererIndex\":");
                builder.Append(value.RendererIndex.ToString(CultureInfo.InvariantCulture));
                builder.Append(",\"rendererCount\":");
                builder.Append(value.RendererCount.ToString(CultureInfo.InvariantCulture));
                builder.Append(",\"enabled\":");
                builder.Append(value.Enabled ? "true" : "false");
                builder.Append(",\"typeName\":");
                AppendEscaped(builder, value.TypeName);
                builder.Append('}');
                return builder.ToString();
            }

            private static void AppendEscaped(StringBuilder builder, string value)
            {
                builder.Append('"');
                for (var index = 0; index < value.Length; index++)
                {
                    var current = value[index];
                    switch (current)
                    {
                        case '"':
                            builder.Append("\\\"");
                            break;
                        case '\\':
                            builder.Append("\\\\");
                            break;
                        case '\n':
                            builder.Append("\\n");
                            break;
                        case '\r':
                            builder.Append("\\r");
                            break;
                        case '\t':
                            builder.Append("\\t");
                            break;
                        default:
                            if (current < ' ')
                            {
                                builder.Append("\\u");
                                builder.Append(((int)current).ToString("x4", CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                builder.Append(current);
                            }

                            break;
                    }
                }

                builder.Append('"');
            }

            private sealed class Reader
            {
                private readonly string _text;
                private int _index;
                private bool _rejected;
                private bool _hasInstance;
                private ulong _instanceId;
                private bool _hasIndex;
                private int _rendererIndex;
                private bool _hasEnabled;
                private bool _enabled;

                public Reader(string text)
                {
                    _text = text ?? string.Empty;
                }

                public bool TryRead(out bool hasInstance, out ulong instanceId, out int rendererIndex, out bool enabled)
                {
                    hasInstance = false;
                    instanceId = 0;
                    rendererIndex = 0;
                    enabled = false;
                    SkipWhitespace();
                    if (_index >= _text.Length || !Take('{'))
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
                    if (_index < _text.Length || _rejected || !_hasEnabled)
                    {
                        return false;
                    }

                    hasInstance = _hasInstance;
                    instanceId = _instanceId;
                    rendererIndex = _hasIndex ? _rendererIndex : 0;
                    enabled = _enabled;
                    return true;
                }

                private bool TryConsumeField(string key)
                {
                    if (key == "instanceId")
                    {
                        return TakeInstance();
                    }

                    if (key == "rendererIndex")
                    {
                        return TakeIndex();
                    }

                    if (key == "enabled")
                    {
                        return TakeEnabled();
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

                private bool TakeIndex()
                {
                    if (_hasIndex)
                    {
                        _rejected = true;
                    }

                    if (!TryReadPlainInteger(out var integer, out var accepted))
                    {
                        return false;
                    }

                    if (!accepted || integer < 0)
                    {
                        _rejected = true;
                        return true;
                    }

                    _rendererIndex = integer;
                    _hasIndex = true;
                    return true;
                }

                private bool TakeEnabled()
                {
                    if (_hasEnabled)
                    {
                        _rejected = true;
                    }

                    if (TryTakeLiteral("true"))
                    {
                        _enabled = true;
                        _hasEnabled = true;
                        return true;
                    }

                    if (TryTakeLiteral("false"))
                    {
                        _enabled = false;
                        _hasEnabled = true;
                        return true;
                    }

                    _rejected = true;
                    return SkipValue();
                }

                private bool TryReadPlainInteger(out int integer, out bool accepted)
                {
                    integer = 0;
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
                    if (token.IndexOf('.') >= 0 || token.IndexOf('e') >= 0 || token.IndexOf('E') >= 0)
                    {
                        return true;
                    }

                    if (!int.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out integer))
                    {
                        return true;
                    }

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
