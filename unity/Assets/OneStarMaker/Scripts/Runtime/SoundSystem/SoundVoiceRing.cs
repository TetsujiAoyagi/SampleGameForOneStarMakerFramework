#nullable enable

namespace OneStarMaker.Runtime.SoundSystem
{
    /// <summary>
    /// 固定本数の再生スロットを古い順に回す。優先度では選ばない。
    /// 本数を超えた再生は、最も古く使い始めたスロットを止めて使う。
    /// </summary>
    internal static class SoundVoiceRing
    {
        public static int Next(ref int cursor, int capacity)
        {
            var index = cursor;
            var advanced = index + 1;
            cursor = advanced == capacity ? 0 : advanced;
            return index;
        }
    }
}
