#nullable enable

namespace OneStarMaker.Runtime.DebugCommands
{
    /// <summary>
    /// 選択中の instanceId だけを持つ。シーンオブジェクトは持たない。
    /// static にはせず、catalog へ登録する側が App 寿命で1つ持つ。
    /// </summary>
    public sealed class DebugGameObjectSelection
    {
        private bool _has;
        private ulong _instanceId;

        public bool TryGet(out ulong instanceId)
        {
            instanceId = _instanceId;
            return _has;
        }

        public void Replace(ulong instanceId)
        {
            _has = true;
            _instanceId = instanceId;
        }

        public void Clear()
        {
            _has = false;
            _instanceId = 0;
        }
    }
}
