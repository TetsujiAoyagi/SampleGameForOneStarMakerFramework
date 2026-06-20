#nullable enable

using System;
using System.Collections.Generic;
using System.Text;

namespace OneStarMaker.Foundation.Config
{
    /// <summary>
    /// JSON 文字列を ":" 区切りのフラットキーへ展開するユーティリティ。
    /// </summary>
    public static class JsonConfigFlattener
    {
        public static void Flatten(string json, Dictionary<string, string> store)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            if (store == null) throw new ArgumentNullException(nameof(store));

            var index = 0;
            SkipWhitespace(json, ref index);
            if (index < json.Length && json[index] == '{')
            {
                ParseObject(json, ref index, string.Empty, store);
            }
        }

        private static void ParseObject(
            string json, ref int index, string prefix, Dictionary<string, string> store)
        {
            Expect(json, ref index, '{');
            SkipWhitespace(json, ref index);

            if (index < json.Length && json[index] == '}')
            {
                index++;
                return;
            }

            while (true)
            {
                SkipWhitespace(json, ref index);
                var key = ParseString(json, ref index);
                SkipWhitespace(json, ref index);
                Expect(json, ref index, ':');
                SkipWhitespace(json, ref index);

                var fullKey = string.IsNullOrEmpty(prefix) ? key : $"{prefix}:{key}";
                ParseValue(json, ref index, fullKey, store);

                SkipWhitespace(json, ref index);
                if (index >= json.Length || json[index] != ',')
                {
                    break;
                }

                index++;
            }

            SkipWhitespace(json, ref index);
            Expect(json, ref index, '}');
        }

        private static void ParseArray(
            string json, ref int index, string prefix, Dictionary<string, string> store)
        {
            Expect(json, ref index, '[');
            SkipWhitespace(json, ref index);

            if (index < json.Length && json[index] == ']')
            {
                index++;
                return;
            }

            var arrayIndex = 0;
            while (true)
            {
                SkipWhitespace(json, ref index);
                var fullKey = $"{prefix}:{arrayIndex}";
                ParseValue(json, ref index, fullKey, store);
                arrayIndex++;

                SkipWhitespace(json, ref index);
                if (index >= json.Length || json[index] != ',')
                {
                    break;
                }

                index++;
            }

            SkipWhitespace(json, ref index);
            Expect(json, ref index, ']');
        }

        private static void ParseValue(
            string json, ref int index, string key, Dictionary<string, string> store)
        {
            SkipWhitespace(json, ref index);
            if (index >= json.Length)
            {
                throw new FormatException("Unexpected end of JSON");
            }

            var c = json[index];
            if (c == '{')
            {
                ParseObject(json, ref index, key, store);
            }
            else if (c == '[')
            {
                ParseArray(json, ref index, key, store);
            }
            else if (c == '"')
            {
                store[key] = ParseString(json, ref index);
            }
            else if (c == 't' || c == 'f')
            {
                store[key] = ParseLiteral(json, ref index);
            }
            else if (c == 'n')
            {
                ParseLiteral(json, ref index);
            }
            else if (c == '-' || char.IsDigit(c))
            {
                store[key] = ParseNumber(json, ref index);
            }
            else
            {
                throw new FormatException($"Unexpected character '{c}' at position {index}");
            }
        }

        private static string ParseString(string json, ref int index)
        {
            Expect(json, ref index, '"');
            var start = index;
            StringBuilder? sb = null;

            while (index < json.Length)
            {
                var c = json[index];
                if (c == '\\')
                {
                    sb ??= new StringBuilder();
                    sb.Append(json, start, index - start);
                    index++;
                    if (index >= json.Length)
                    {
                        throw new FormatException("Unterminated string escape");
                    }

                    var esc = json[index];
                    sb.Append(esc switch
                    {
                        '"' => '"',
                        '\\' => '\\',
                        '/' => '/',
                        'b' => '\b',
                        'f' => '\f',
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        _ => esc,
                    });
                    index++;
                    start = index;
                }
                else if (c == '"')
                {
                    var result = sb != null
                        ? sb.Append(json, start, index - start).ToString()
                        : json.Substring(start, index - start);
                    index++;
                    return result;
                }
                else
                {
                    index++;
                }
            }

            throw new FormatException("Unterminated string");
        }

        private static string ParseNumber(string json, ref int index)
        {
            var start = index;
            if (index < json.Length && json[index] == '-')
            {
                index++;
            }

            while (index < json.Length && char.IsDigit(json[index]))
            {
                index++;
            }

            if (index < json.Length && json[index] == '.')
            {
                index++;
                while (index < json.Length && char.IsDigit(json[index]))
                {
                    index++;
                }
            }

            if (index < json.Length && (json[index] == 'e' || json[index] == 'E'))
            {
                index++;
                if (index < json.Length && (json[index] == '+' || json[index] == '-'))
                {
                    index++;
                }

                while (index < json.Length && char.IsDigit(json[index]))
                {
                    index++;
                }
            }

            return json.Substring(start, index - start);
        }

        private static string ParseLiteral(string json, ref int index)
        {
            var span = json.AsSpan(index);
            if (span.StartsWith("true".AsSpan()))
            {
                index += 4;
                return "true";
            }

            if (span.StartsWith("false".AsSpan()))
            {
                index += 5;
                return "false";
            }

            if (span.StartsWith("null".AsSpan()))
            {
                index += 4;
                return "null";
            }

            throw new FormatException($"Unexpected literal at position {index}");
        }

        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index]))
            {
                index++;
            }
        }

        private static void Expect(string json, ref int index, char expected)
        {
            if (index >= json.Length || json[index] != expected)
            {
                throw new FormatException(
                    $"Expected '{expected}' at position {index}, got '{(index < json.Length ? json[index].ToString() : "EOF")}'");
            }

            index++;
        }
    }
}
