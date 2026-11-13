#nullable enable

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnityEngine;

namespace OneStarMaker.Foundation.Logging
{
    /// <summary>Vector3 の計算用プロパティを巡回せず、保存する三成分だけを明示する。</summary>
    internal sealed class Vector3JsonConverter : JsonConverter<Vector3>
    {
        public override void Write(Utf8JsonWriter writer, Vector3 value, JsonSerializerOptions options)
        {
            // NaN / Infinity を文字列や null に置き換えると、数値を記録できたように見える。
            // 全成分を先に検査し、通常の変換例外として独立通知へ渡す。
            if (!Finite(value.x) || !Finite(value.y) || !Finite(value.z))
                throw new JsonException("Vector3 components must be finite.");

            writer.WriteStartObject();
            writer.WriteNumber("x", value.x);
            writer.WriteNumber("y", value.y);
            writer.WriteNumber("z", value.z);
            writer.WriteEndObject();
        }

        // ログは出力専用。入力形式まで暗黙に公開すると別の契約が増えるため扱わない。
        public override Vector3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => throw new NotSupportedException("Log Vector3 conversion is write-only.");

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
