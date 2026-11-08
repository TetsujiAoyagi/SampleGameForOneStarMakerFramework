#nullable enable

using System.Threading;
using System.Threading.Tasks;

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>
    /// 供給元の差込口。要求に対して候補があるかを答えるだけで、bytes の取得・install・Build はしない。
    /// 実装は供給元の adapter が持ち、寿命は policy の持ち主に従う。
    /// </summary>
    public interface IContentProvider
    {
        /// <summary>policy の中で一意な id（ordinal）。返す候補の ProviderId はこの値に一致しなければ採用されない。</summary>
        string Id { get; }

        /// <summary>この供給元の由来。返す候補の SourceKind はこの値に一致しなければ採用されない。</summary>
        ContentSourceKind SourceKind { get; }

        /// <summary>
        /// 候補を探す。取消は token で伝える。token が取消された後に完了した回答は、resolver が採用しない。
        /// token が取消されていないのに <see cref="System.OperationCanceledException"/> を投げると fault として記録される。
        /// </summary>
        ValueTask<ContentAvailability> FindAsync(ContentRequest request, CancellationToken cancellationToken);
    }
}
