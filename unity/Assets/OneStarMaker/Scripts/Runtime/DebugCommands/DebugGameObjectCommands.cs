#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OneStarMaker.Runtime.DebugCommands
{
    /// <summary>
    /// go.list と go.select を既存の catalog へ登録する。
    /// 登録時には世界を読まない。実行は呼び出し側スレッドで同期のままである。
    /// </summary>
    public static partial class DebugGameObjectCommands
    {
        public const string ListName = "go.list";
        public const string SelectName = "go.select";
        public const string SetActiveName = "go.set-active";
        public const int DefaultPageSize = 64;
        public const int MaxPageSize = 128;
        internal const int MaxDisplaySegments = 32;

        private const string ListedMessage = "Listed GameObjects.";
        private const string SelectedMessage = "Selected GameObject.";
        private const string NotAliveMessage = "GameObject is not alive.";
        private const string InvalidPayloadMessage = "Debug GameObject command payload is invalid.";
        private const string PageOutOfRangeMessage = "Debug GameObject page is out of range.";
        private const string UnavailableMessage = "GameObject inspection is unavailable.";
        private const string NotSelectedMessage = "GameObject is not selected.";
        private const string SetActiveMessage = "Set GameObject active.";
        private const string ParentInactiveMessage = "Set activeSelf. An inactive parent still keeps activeInHierarchy false.";

        /// <summary>
        /// list と select だけを登録する。set-active 以降はこのメソッドに入れない。
        /// </summary>
        public static void RegisterListAndSelect(
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

            catalog.Register(ListName, payload => ExecuteList(payload, world, selection));
            catalog.Register(SelectName, payload => ExecuteSelect(payload, world, selection));
        }

        /// <summary>
        /// go.set-active だけを登録する。list と select は登録しない。
        /// 選択を共有するなら、list / select と同じ selection を渡す。
        /// </summary>
        public static void RegisterSetActive(
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

            catalog.Register(SetActiveName, payload => ExecuteSetActive(payload, world, selection));
        }

        /// <summary>
        /// leaf から root へ並んだ名前から表示パスを作る。33段以上は先頭を /... にし、近い32段だけを残す。
        /// </summary>
        internal static string FormatDisplayPath(List<string> leafToRoot)
        {
            if (leafToRoot == null)
            {
                throw new ArgumentNullException(nameof(leafToRoot));
            }

            var truncated = leafToRoot.Count > MaxDisplaySegments;
            var take = truncated ? MaxDisplaySegments : leafToRoot.Count;
            var builder = new StringBuilder();
            if (truncated)
            {
                builder.Append("/...");
            }

            for (var index = take - 1; index >= 0; index--)
            {
                builder.Append('/');
                builder.Append(leafToRoot[index] ?? string.Empty);
            }

            if (take == 0)
            {
                builder.Append('/');
            }

            return builder.ToString();
        }

        private static DebugCommandResult ExecuteList(
            string payload,
            IDebugGameObjectWorld world,
            DebugGameObjectSelection selection)
        {
            if (!PayloadReader.TryReadList(payload, out var offset, out var limit, out var payloadStatus))
            {
                return DebugCommandResult.Fail(PayloadMessage(payloadStatus));
            }

            var cleared = false;
            if (selection.TryGet(out var selectedId))
            {
                var described = world.TryDescribe(selectedId, out _, out _);
                if (described == DebugGameObjectReadStatus.Unavailable)
                {
                    return DebugCommandResult.Fail(UnavailableMessage);
                }

                if (described == DebugGameObjectReadStatus.NotFound)
                {
                    selection.Clear();
                    cleared = true;
                }
            }

            var rows = new List<DebugGameObjectRow>(limit);
            var collected = world.TryCollectPage(offset, limit, rows, out var hasMore, out _);
            if (collected != DebugGameObjectReadStatus.Ok)
            {
                return DebugCommandResult.Fail(UnavailableMessage);
            }

            var hasSelection = selection.TryGet(out var currentId);
            return DebugCommandResult.Ok(
                ListedMessage,
                PayloadWriter.WriteList(offset, limit, rows, hasMore, cleared, hasSelection, currentId));
        }

        private static DebugCommandResult ExecuteSelect(
            string payload,
            IDebugGameObjectWorld world,
            DebugGameObjectSelection selection)
        {
            if (!PayloadReader.TryReadSelect(payload, out var instanceId, out var payloadStatus))
            {
                return DebugCommandResult.Fail(PayloadMessage(payloadStatus));
            }

            var described = world.TryDescribe(instanceId, out var row, out _);
            if (described == DebugGameObjectReadStatus.Unavailable)
            {
                return DebugCommandResult.Fail(UnavailableMessage);
            }

            if (described != DebugGameObjectReadStatus.Ok)
            {
                selection.Clear();
                return DebugCommandResult.Fail(NotAliveMessage);
            }

            selection.Replace(row.InstanceId);
            return DebugCommandResult.Ok(SelectedMessage, PayloadWriter.WriteRow(row));
        }

        private static DebugCommandResult ExecuteSetActive(
            string payload,
            IDebugGameObjectWorld world,
            DebugGameObjectSelection selection)
        {
            if (!PayloadReader.TryReadSetActive(payload, out var hasInstance, out var requestedId, out var active, out var payloadStatus))
            {
                return DebugCommandResult.Fail(PayloadMessage(payloadStatus));
            }

            ulong target;
            if (hasInstance)
            {
                target = requestedId;
            }
            else if (!selection.TryGet(out target))
            {
                return DebugCommandResult.Fail(NotSelectedMessage);
            }

            var changed = world.TrySetActive(target, active, out var row, out _);
            if (changed == DebugGameObjectReadStatus.Unavailable)
            {
                return DebugCommandResult.Fail(UnavailableMessage);
            }

            if (changed != DebugGameObjectReadStatus.Ok)
            {
                if (selection.TryGet(out var current) && current == target)
                {
                    selection.Clear();
                }

                return DebugCommandResult.Fail(NotAliveMessage);
            }

            var message = active && !row.ActiveInHierarchy ? ParentInactiveMessage : SetActiveMessage;
            return DebugCommandResult.Ok(message, PayloadWriter.WriteRow(row));
        }

        private static string PayloadMessage(PayloadStatus status)
        {
            return status == PayloadStatus.PageOutOfRange ? PageOutOfRangeMessage : InvalidPayloadMessage;
        }

        private enum PayloadStatus
        {
            Ok = 0,
            Invalid = 1,
            PageOutOfRange = 2,
        }

        private static class PayloadReader
        {
            public static bool TryReadList(string payload, out int offset, out int limit, out PayloadStatus status)
            {
                offset = 0;
                limit = DefaultPageSize;
                var reader = new Reader(payload);
                if (reader.IsEmpty)
                {
                    status = PayloadStatus.Ok;
                    return true;
                }

                if (!reader.TryReadObject(out var syntax) || !syntax)
                {
                    status = PayloadStatus.Invalid;
                    return false;
                }

                if (reader.UnknownOrDuplicate)
                {
                    status = PayloadStatus.Invalid;
                    return false;
                }

                if (reader.OffsetState == FieldState.Bad || reader.LimitState == FieldState.Bad)
                {
                    status = PayloadStatus.PageOutOfRange;
                    return false;
                }

                offset = reader.OffsetState == FieldState.Present ? reader.Offset : 0;
                limit = reader.LimitState == FieldState.Present ? reader.Limit : DefaultPageSize;
                if (offset < 0 || limit < 1 || limit > MaxPageSize)
                {
                    status = PayloadStatus.PageOutOfRange;
                    return false;
                }

                status = PayloadStatus.Ok;
                return true;
            }

            public static bool TryReadSelect(string payload, out ulong instanceId, out PayloadStatus status)
            {
                instanceId = 0;
                var reader = new Reader(payload);
                if (reader.IsEmpty || !reader.TryReadObject(out var syntax) || !syntax)
                {
                    status = PayloadStatus.Invalid;
                    return false;
                }

                if (reader.UnknownOrDuplicate || reader.InstanceState != FieldState.Present)
                {
                    status = PayloadStatus.Invalid;
                    return false;
                }

                instanceId = reader.InstanceId;
                status = PayloadStatus.Ok;
                return true;
            }

            public static bool TryReadSetActive(
                string payload,
                out bool hasInstance,
                out ulong instanceId,
                out bool active,
                out PayloadStatus status)
            {
                hasInstance = false;
                instanceId = 0;
                active = false;
                var reader = new Reader(payload, ObjectMode.SetActive);
                if (reader.IsEmpty || !reader.TryReadObject(out var syntax) || !syntax)
                {
                    status = PayloadStatus.Invalid;
                    return false;
                }

                if (reader.UnknownOrDuplicate || reader.ActiveState != FieldState.Present || reader.InstanceState == FieldState.Bad)
                {
                    status = PayloadStatus.Invalid;
                    return false;
                }

                active = reader.Active;
                if (reader.InstanceState == FieldState.Present)
                {
                    hasInstance = true;
                    instanceId = reader.InstanceId;
                }

                status = PayloadStatus.Ok;
                return true;
            }

            private enum FieldState
            {
                Absent = 0,
                Present = 1,
                Bad = 2,
            }

            private enum ObjectMode
            {
                List = 0,
                SetActive = 1,
            }

            private sealed class Reader
            {
                private readonly string _text;
                private readonly ObjectMode _mode;
                private int _index;

                public Reader(string text)
                    : this(text, ObjectMode.List)
                {
                }

                public Reader(string text, ObjectMode mode)
                {
                    _text = text ?? string.Empty;
                    _mode = mode;
                }

                public bool IsEmpty
                {
                    get
                    {
                        SkipWhitespace();
                        return _index >= _text.Length;
                    }
                }

                public bool UnknownOrDuplicate { get; private set; }

                public FieldState OffsetState { get; private set; }

                public FieldState LimitState { get; private set; }

                public FieldState InstanceState { get; private set; }

                public FieldState ActiveState { get; private set; }

                public bool Active { get; private set; }

                public int Offset { get; private set; }

                public int Limit { get; private set; }

                public ulong InstanceId { get; private set; }

                public bool TryReadObject(out bool syntax)
                {
                    syntax = false;
                    SkipWhitespace();
                    if (!Take('{'))
                    {
                        return false;
                    }

                    SkipWhitespace();
                    if (Take('}'))
                    {
                        syntax = Finish();
                        return true;
                    }

                    while (true)
                    {
                        if (!TryReadString(out var key))
                        {
                            return false;
                        }

                        SkipWhitespace();
                        if (!Take(':'))
                        {
                            return false;
                        }

                        SkipWhitespace();
                        if (!TryConsumeValue(key))
                        {
                            return false;
                        }

                        SkipWhitespace();
                        if (Take('}'))
                        {
                            syntax = Finish();
                            return true;
                        }

                        if (!Take(','))
                        {
                            return false;
                        }

                        SkipWhitespace();
                    }
                }

                private bool Finish()
                {
                    SkipWhitespace();
                    return _index >= _text.Length;
                }

                private bool TryConsumeValue(string key)
                {
                    if (_mode == ObjectMode.SetActive)
                    {
                        if (key == "active")
                        {
                            return TakeActiveField();
                        }

                        if (key == "instanceId")
                        {
                            return TakeInstanceField();
                        }

                        UnknownOrDuplicate = true;
                        return SkipValue();
                    }

                    if (key == "offset")
                    {
                        return TakePageField(isOffset: true);
                    }

                    if (key == "limit")
                    {
                        return TakePageField(isOffset: false);
                    }

                    if (key == "instanceId")
                    {
                        return TakeInstanceField();
                    }

                    UnknownOrDuplicate = true;
                    return SkipValue();
                }

                private bool TakeActiveField()
                {
                    if (ActiveState != FieldState.Absent)
                    {
                        UnknownOrDuplicate = true;
                    }

                    if (TryTakeLiteral("true"))
                    {
                        Active = true;
                        ActiveState = FieldState.Present;
                        return true;
                    }

                    if (TryTakeLiteral("false"))
                    {
                        Active = false;
                        ActiveState = FieldState.Present;
                        return true;
                    }

                    ActiveState = FieldState.Bad;
                    return SkipValue();
                }

                private bool TakePageField(bool isOffset)
                {
                    var state = isOffset ? OffsetState : LimitState;
                    if (state != FieldState.Absent)
                    {
                        UnknownOrDuplicate = true;
                    }

                    if (!TryClassifyNumber(out var kind, out var integer))
                    {
                        return false;
                    }

                    if (kind == NumberKind.Integer)
                    {
                        if (isOffset)
                        {
                            Offset = integer;
                            OffsetState = FieldState.Present;
                        }
                        else
                        {
                            Limit = integer;
                            LimitState = FieldState.Present;
                        }
                    }
                    else
                    {
                        if (isOffset)
                        {
                            OffsetState = FieldState.Bad;
                        }
                        else
                        {
                            LimitState = FieldState.Bad;
                        }
                    }

                    return true;
                }

                private bool TakeInstanceField()
                {
                    if (InstanceState != FieldState.Absent)
                    {
                        UnknownOrDuplicate = true;
                    }

                    if (Peek('{') || Peek('['))
                    {
                        InstanceState = FieldState.Bad;
                        return SkipValue();
                    }

                    if (TryTakeLiteral("true") || TryTakeLiteral("false") || TryTakeLiteral("null"))
                    {
                        InstanceState = FieldState.Bad;
                        return true;
                    }

                    if (Peek('-') || PeekDigit())
                    {
                        if (!SkipNumber())
                        {
                            return false;
                        }

                        InstanceState = FieldState.Bad;
                        return true;
                    }

                    if (!TryReadString(out var text))
                    {
                        return false;
                    }

                    if (ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
                    {
                        InstanceId = parsed;
                        InstanceState = FieldState.Present;
                    }
                    else
                    {
                        InstanceState = FieldState.Bad;
                    }

                    return true;
                }

                private bool TryClassifyNumber(out NumberKind kind, out int integer)
                {
                    kind = NumberKind.NotPlainInteger;
                    integer = 0;
                    var start = _index;
                    if (!SkipNumber())
                    {
                        if (Peek('"'))
                        {
                            kind = NumberKind.NotPlainInteger;
                            return TryReadString(out _);
                        }

                        if (TryTakeLiteral("true") || TryTakeLiteral("false") || TryTakeLiteral("null") || Peek('{') || Peek('['))
                        {
                            if (Peek('{') || Peek('['))
                            {
                                return SkipValue();
                            }

                            kind = NumberKind.NotPlainInteger;
                            return true;
                        }

                        return false;
                    }

                    var token = _text.Substring(start, _index - start);
                    if (token.IndexOf('.') >= 0 || token.IndexOf('e') >= 0 || token.IndexOf('E') >= 0)
                    {
                        kind = NumberKind.NotPlainInteger;
                        return true;
                    }

                    if (!int.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out integer))
                    {
                        kind = NumberKind.NotPlainInteger;
                        return true;
                    }

                    kind = NumberKind.Integer;
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

                    if (start < _text.Length && _text[start] != '-' && _text[start] == '0' && _index - start > 1 && char.IsDigit(_text[start + 1]))
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

            private enum NumberKind
            {
                Integer = 0,
                NotPlainInteger = 1,
            }
        }

        private static class PayloadWriter
        {
            public static string WriteList(
                int offset,
                int limit,
                List<DebugGameObjectRow> rows,
                bool hasMore,
                bool selectionCleared,
                bool hasSelection,
                ulong selectionId)
            {
                var builder = new StringBuilder(128);
                builder.Append("{\"offset\":");
                AppendInt(builder, offset);
                builder.Append(",\"limit\":");
                AppendInt(builder, limit);
                builder.Append(",\"count\":");
                AppendInt(builder, rows.Count);
                builder.Append(",\"hasMore\":");
                AppendBool(builder, hasMore);
                builder.Append(",\"selectionCleared\":");
                AppendBool(builder, selectionCleared);
                builder.Append(",\"selectionInstanceId\":");
                if (hasSelection)
                {
                    AppendEscaped(builder, selectionId.ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    builder.Append("null");
                }

                builder.Append(",\"items\":[");
                for (var index = 0; index < rows.Count; index++)
                {
                    if (index > 0)
                    {
                        builder.Append(',');
                    }

                    AppendRow(builder, rows[index]);
                }

                builder.Append("]}");
                return builder.ToString();
            }

            public static string WriteRow(DebugGameObjectRow row)
            {
                var builder = new StringBuilder(96);
                AppendRow(builder, row);
                return builder.ToString();
            }

            private static void AppendRow(StringBuilder builder, DebugGameObjectRow row)
            {
                builder.Append("{\"instanceId\":");
                AppendEscaped(builder, row.InstanceId.ToString(CultureInfo.InvariantCulture));
                builder.Append(",\"parentInstanceId\":");
                if (row.HasParent)
                {
                    AppendEscaped(builder, row.ParentInstanceId.ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    builder.Append("null");
                }

                builder.Append(",\"name\":");
                AppendEscaped(builder, Cap(row.Name));
                builder.Append(",\"sceneName\":");
                AppendEscaped(builder, Cap(row.SceneName));
                builder.Append(",\"displayPath\":");
                AppendEscaped(builder, Cap(row.DisplayPath));
                builder.Append(",\"activeSelf\":");
                AppendBool(builder, row.ActiveSelf);
                builder.Append(",\"activeInHierarchy\":");
                AppendBool(builder, row.ActiveInHierarchy);
                builder.Append(",\"siblingIndex\":");
                AppendInt(builder, row.SiblingIndex);
                builder.Append(",\"childCount\":");
                AppendInt(builder, row.ChildCount);
                builder.Append('}');
            }

            private static string Cap(string value)
            {
                if (value.Length <= 256)
                {
                    return value;
                }

                var keep = 253;
                if (char.IsHighSurrogate(value[keep - 1]))
                {
                    keep--;
                }

                return value.Substring(0, keep) + "...";
            }

            private static void AppendInt(StringBuilder builder, int value)
            {
                builder.Append(value.ToString(CultureInfo.InvariantCulture));
            }

            private static void AppendBool(StringBuilder builder, bool value)
            {
                builder.Append(value ? "true" : "false");
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
                        case '\b':
                            builder.Append("\\b");
                            break;
                        case '\f':
                            builder.Append("\\f");
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
        }
    }
}
