using System;
using System.Collections.Generic;

namespace OneStarMaker.Foundation.UpdateSystem
{
    /// <summary>
    /// managed fallback path が backend へ渡す dispatch 単位。
    /// scheduler 時代の「IReadOnlyList をそのまま投げる」設計をやめ、
    /// phase / context / handle / element のまとまりとして境界を固定する。
    /// </summary>
    public readonly struct ManagedExecutionBatch
    {
        public ManagedExecutionBatch(
            UpdateExecutionPhase phase,
            IReadOnlyList<UpdateHandle> handles,
            IReadOnlyList<IUpdateElement> elements,
            in UpdateFrameContext context)
        {
            Phase = phase;
            Handles = handles ?? throw new ArgumentNullException(nameof(handles));
            Elements = elements ?? throw new ArgumentNullException(nameof(elements));
            Context = context;
        }

        public UpdateExecutionPhase Phase { get; }

        public IReadOnlyList<UpdateHandle> Handles { get; }

        public IReadOnlyList<IUpdateElement> Elements { get; }

        public UpdateFrameContext Context { get; }

        public int Count => Elements.Count;
    }
}
