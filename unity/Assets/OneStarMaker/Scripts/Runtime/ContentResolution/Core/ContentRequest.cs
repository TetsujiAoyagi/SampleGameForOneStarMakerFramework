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
        public ContentRequest(
            string logicalKey,
            ContentKind kind,
            string representation,
            ContentIdentity? expectedIdentity = null,
            string? correlationId = null)
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

            LogicalKey = logicalKey;
            Kind = kind;
            Representation = representation ?? throw new ArgumentNullException(nameof(representation));
            ExpectedIdentity = expectedIdentity;
            CorrelationId = correlationId ?? string.Empty;
        }

        public string LogicalKey { get; }

        public ContentKind Kind { get; }

        /// <summary>空なら「指定なし」。</summary>
        public string Representation { get; }

        /// <summary>最初の pin。無ければ null。</summary>
        public ContentIdentity? ExpectedIdentity { get; }

        public string CorrelationId { get; }
    }
}
