#nullable enable

using System;

namespace OneStarMaker.Runtime.ScriptSystem
{
    /// <summary>
    /// 呼び出し側が所有する int64 レジスタ配列。
    /// 配列の確保は構築時の一度だけで、VM は本数を変えない。
    /// tick は例外を避けるためインデクサではなく内部の成否付き読み書きを使う。
    /// </summary>
    public sealed class ScriptRegisters
    {
        private readonly long[] _values;

        public ScriptRegisters(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Register count must be zero or greater.");
            }

            _values = new long[count];
        }

        public int Count => _values.Length;

        public long this[int index]
        {
            get
            {
                ValidateIndex(index);
                return _values[index];
            }

            set
            {
                ValidateIndex(index);
                _values[index] = value;
            }
        }

        internal bool TryGet(int index, out long value)
        {
            if ((uint)index >= (uint)_values.Length)
            {
                value = 0;
                return false;
            }

            value = _values[index];
            return true;
        }

        internal bool TrySet(int index, long value)
        {
            if ((uint)index >= (uint)_values.Length)
            {
                return false;
            }

            _values[index] = value;
            return true;
        }

        private void ValidateIndex(int index)
        {
            if ((uint)index >= (uint)_values.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Register index is outside the caller-owned array.");
            }
        }
    }
}
