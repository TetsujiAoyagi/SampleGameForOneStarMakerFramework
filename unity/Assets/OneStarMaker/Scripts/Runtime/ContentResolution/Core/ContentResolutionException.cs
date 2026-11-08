#nullable enable

using System;

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>
    /// 要求を解決できなかった、または採用した候補を loader がロードの前に拒んだことを呼び出し側へ返す例外。
    /// 呼び出し側は <see cref="Rejection"/> で 2 つを区別する:
    /// resolver の失敗では <see cref="Rejection"/> が None で <see cref="Result"/> は失敗の結果、
    /// loader の拒否では <see cref="Rejection"/> が理由を持ち <see cref="Result"/> は resolver の成功結果のまま。
    /// AssetManagement のロード失敗はこの例外で包まず、そのまま呼び出し側へ通る（reason code の所有を変えないため）。
    /// </summary>
    public sealed class ContentResolutionException : InvalidOperationException
    {
        public ContentResolutionException(ContentResolutionResult result, ContentLoadRejection rejection)
            : base(BuildMessage(result, rejection))
        {
            Result = result;
            Rejection = rejection;
        }

        public ContentResolutionResult Result { get; }

        public ContentLoadRejection Rejection { get; }

        private static string BuildMessage(ContentResolutionResult result, ContentLoadRejection rejection)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (!Enum.IsDefined(typeof(ContentLoadRejection), rejection))
            {
                throw new ArgumentException("Unknown load rejection.", nameof(rejection));
            }

            // resolver の失敗と loader の拒否を取り違えた組は作らせない（呼び出し側が 2 つを区別できなくなる）。
            if (rejection == ContentLoadRejection.None)
            {
                if (result.IsResolved)
                {
                    throw new ArgumentException("A resolved result needs a load rejection reason.", nameof(rejection));
                }

                return $"Content request was not resolved: {result.FailureCode} after {result.Probes.Count} probe(s).";
            }

            if (!result.IsResolved)
            {
                throw new ArgumentException("A load rejection applies only to a resolved result.", nameof(rejection));
            }

            var candidate = result.Candidate!;
            return $"Adopted candidate from provider '{candidate.ProviderId}' was rejected before loading: {rejection} (load path {candidate.LoadPath}).";
        }
    }
}
