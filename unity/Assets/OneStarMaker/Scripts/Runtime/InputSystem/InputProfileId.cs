#nullable enable

using System;

namespace OneStarMaker.Runtime.InputSystem
{
    /// <summary>
    /// 同じアクションに別バインドを当てるためのプロファイル枠。
    /// このスライスが受け付けるのは <see cref="Default"/> だけである。
    /// 片手用の中身、リマップ、触覚はここに置かない。未知の ID を選んでも既定バインドのふりをしない。
    /// </summary>
    public readonly struct InputProfileId : IEquatable<InputProfileId>
    {
        /// <summary>既存アセットの作者付けバインドを指す、唯一の受理 ID。</summary>
        public const string DefaultValue = "default";

        public static readonly InputProfileId Default = new InputProfileId(DefaultValue);

        private readonly string? _value;

        public InputProfileId(string? value)
        {
            _value = value;
        }

        public string Value => _value ?? string.Empty;

        public bool IsEmpty => string.IsNullOrEmpty(_value);

        public bool Equals(InputProfileId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return obj is InputProfileId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }
    }
}
