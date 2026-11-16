#nullable enable

using System;

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>
    /// 呼び出し側の要求。resolver も provider もこの値を変えない（不変の値）。
    /// 要求が持つのは制約だけで、候補が妥当かを決めるのは規則（<see cref="IContentCompatibilityRule"/>）である。
    /// ホスト固有の状態（Scene の親子や起動時特例など）はここに入れず、ホストの policy として登録する。
    /// </summary>
    public sealed class ContentRequest
    {
        /// <param name="logicalKey">
        /// 必須。空白だけの値も拒む。<c>AssetOwner.Scene</c> と同じ基準にして、成功経路で owner の表記を作るときに失敗させない。
        /// </param>
        /// <param name="kind">要求の種別。<see cref="ContentKind.None"/> は拒む。</param>
        /// <param name="representation">要求元が指定する表現。空は「指定なし」で、null は拒む。</param>
        /// <param name="expectedIdentity">
        /// 任意。探索の最初の pin。与えるなら 4 欄がそろった値だけで、空や一部の欄だけの値は拒む
        /// （部分的な制約を pin と読ませると、欄の欠けた候補が「一致」してしまうため）。
        /// </param>
        /// <param name="correlationId">任意。呼び出し側が要求を追跡するための opaque な値。</param>
        /// <param name="fallbackRepresentation">
        /// 任意。要求した表現の候補が無い時に採ってよい、もう 1 つの表現。null は「fallback しない」。
        /// 要求した表現を黙って変えないため、fallback は呼び手がここで明示した時だけ起きる。
        /// 表現を指定しない要求（<paramref name="representation"/> が空）、要求の表現と同じ値、空・空白だけの値は拒む
        /// （どれも「fallback しない」と区別できない書き方なので、意図の無い fallback を作らせない）。
        /// 既存の呼び手の位置引数をずらさないため、末尾の任意引数にしている。
        /// </param>
        public ContentRequest(
            string logicalKey,
            ContentKind kind,
            string representation,
            ContentIdentity? expectedIdentity = null,
            string? correlationId = null,
            string? fallbackRepresentation = null)
        {
            if (string.IsNullOrWhiteSpace(logicalKey))
            {
                throw new ArgumentException("Logical key is required.", nameof(logicalKey));
            }

            if (kind != ContentKind.Scene && kind != ContentKind.Object)
            {
                throw new ArgumentException("Content kind must be Scene or Object.", nameof(kind));
            }

            ContentIdentity.ValidateOptionalPin(expectedIdentity, nameof(expectedIdentity));

            if (representation == null)
            {
                throw new ArgumentNullException(nameof(representation));
            }

            if (fallbackRepresentation != null)
            {
                if (string.IsNullOrWhiteSpace(fallbackRepresentation))
                {
                    throw new ArgumentException("Pass null instead of an empty fallback representation.", nameof(fallbackRepresentation));
                }

                if (representation.Length == 0)
                {
                    throw new ArgumentException("A fallback representation needs a requested representation.", nameof(fallbackRepresentation));
                }

                if (string.Equals(fallbackRepresentation, representation, StringComparison.Ordinal))
                {
                    throw new ArgumentException("The fallback representation must differ from the requested one.", nameof(fallbackRepresentation));
                }
            }

            LogicalKey = logicalKey;
            Kind = kind;
            Representation = representation;
            ExpectedIdentity = expectedIdentity;
            CorrelationId = correlationId ?? string.Empty;
            FallbackRepresentation = fallbackRepresentation ?? string.Empty;
        }

        public string LogicalKey { get; }

        public ContentKind Kind { get; }

        /// <summary>空なら「指定なし」。</summary>
        public string Representation { get; }

        /// <summary>最初の pin。無ければ null。</summary>
        public ContentIdentity? ExpectedIdentity { get; }

        public string CorrelationId { get; }

        /// <summary>明示した fallback の表現。空なら「fallback しない」。</summary>
        public string FallbackRepresentation { get; }
    }
}
