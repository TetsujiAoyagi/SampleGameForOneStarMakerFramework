using System;

namespace OneStarMaker.Foundation.UpdateSystem
{
    /// <summary>
    /// main thread apply 時に mirror へ渡す最小コンテキスト。
    /// 今は dirty handle のみを保持し、
    /// native state 本体の参照や lease 管理は次段階の native pipeline 接続で拡張する。
    /// </summary>
    public readonly struct MainThreadApplyContext
    {
        public MainThreadApplyContext(UpdateHandle handle)
        {
            if (!handle.IsValid)
            {
                throw new ArgumentException("A valid handle is required.", nameof(handle));
            }

            Handle = handle;
        }

        public UpdateHandle Handle { get; }
    }
}
