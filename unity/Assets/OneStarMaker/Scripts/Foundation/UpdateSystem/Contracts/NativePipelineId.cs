using System;

namespace OneStarMaker.Foundation.UpdateSystem
{
    /// <summary>
    /// world が native pipeline を論理名で識別するための ID。
    ///
    /// これまでは registry instance 自体を識別子として扱っていたため、
    /// 「どの state 系統をどの layer / backend へ結びつけたか」が
    /// 呼び出し側から読み取りにくかった。
    /// Phase C ではまず ID を独立させ、
    /// registry 実体とは別に pipeline の論理的な所属先を表現できるようにする。
    /// </summary>
    public readonly struct NativePipelineId : IEquatable<NativePipelineId>
    {
        public NativePipelineId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(value));
            }

            Value = value.Trim();
        }

        public string Value { get; }

        public bool Equals(NativePipelineId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is NativePipelineId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value != null
                ? StringComparer.Ordinal.GetHashCode(Value)
                : 0;
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(NativePipelineId left, NativePipelineId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NativePipelineId left, NativePipelineId right)
        {
            return !left.Equals(right);
        }
    }
}
