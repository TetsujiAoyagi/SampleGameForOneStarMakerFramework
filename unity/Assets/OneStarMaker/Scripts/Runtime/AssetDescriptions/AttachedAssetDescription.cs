#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace OneStarMaker.Runtime.AssetDescriptions
{
    /// <summary>
    /// SceneResource の node が所有し、その node か子孫の Scene が名前で読む付随 asset（VFX の source、共有 material 等）の宣言。
    /// SceneResource に埋め込む Serializable で、ScriptableObject にはしない（SceneAssetDescription と同じ方針）。
    /// <para>
    /// 所有者は宣言した node ではなく、常に読む Scene（<c>AssetOwner.Scene(読む Scene の identity)</c>）。
    /// 宣言は「どの node に到達すればこの asset が必要か」を BuildSystem へ伝え、Runtime には名前から参照を引く表を与える。
    /// 同じ physical asset を複数の node が宣言してはならない（共有は祖先に 1 回宣言し、子孫が所有者を指定して読む）。
    /// </para>
    /// </summary>
    [Serializable]
    public class AttachedAssetDescription : AssetDescription
    {
        [SerializeField]
        private string _name = string.Empty;

        /// <summary>Variant 付きの参照の一覧。空の Variant が既定（Full）。</summary>
        [SerializeField]
        private List<AssetPayload> _payloads = new();

        public AttachedAssetDescription() { }

        public AttachedAssetDescription(string name, List<AssetPayload> payloads)
        {
            _name = name;
            _payloads = payloads;
        }

        /// <summary>所有 node の中で一意な名前。Scene class はこの名前で読む（address や path は知らない）。</summary>
        public string Name => _name;

        /// <inheritdoc />
        public override IReadOnlyList<AssetPayload> Payloads => _payloads;

        /// <inheritdoc />
        public override string DisplayName => "AttachedAssetDescription:" + _name;

        /// <summary>
        /// Content Directory と build の選択で付随 asset を指す logical key。所有 node の identity と名前を ':' でつなぐ。
        /// Editor 側の選択 core（Runtime を参照しない）も同じ形を組み立てるので、形を変えるときは両方を変える。
        /// </summary>
        public static string LogicalKey(string ownerIdentity, string name) => ownerIdentity + ":" + name;

        internal override AssetReference? ResolveReference(string variant) => FindPayload(variant)?.Reference;

        /// <summary>指定 Variant の Payload。無ければ既定（空文字）へ fallback する（Scene と同じ規則）。</summary>
        private AssetPayload? FindPayload(string variant)
        {
            foreach (var payload in _payloads)
            {
                if (payload.Variant == variant)
                {
                    return payload;
                }
            }

            if (!string.IsNullOrEmpty(variant))
            {
                return FindPayload(string.Empty);
            }

            return null;
        }
    }
}
