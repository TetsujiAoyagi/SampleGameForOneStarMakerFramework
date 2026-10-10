#nullable enable

namespace OneStarMaker.Runtime.ScriptSystem
{
    /// <summary>
    /// Machine が発行する不変の host 要求。ID と引数は Machine にとって不透明な値である。
    /// このオブジェクトの参照同一性が1回限りの完了 token であり、値が同じ要求とは交換できない。
    /// 1 host 境界につき小さい allocation があり、待機中は同じ要求を再利用する。
    /// </summary>
    public sealed class ScriptHostRequest
    {
        internal ScriptHostRequest(int commandId, long argument)
        {
            CommandId = commandId;
            Argument = argument;
        }

        public int CommandId { get; }

        public long Argument { get; }
    }
}
