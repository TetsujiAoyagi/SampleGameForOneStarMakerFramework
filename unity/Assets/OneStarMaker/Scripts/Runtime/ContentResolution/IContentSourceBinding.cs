#nullable enable

using OneStarMaker.Runtime.AssetManagement;

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>
    /// 供給元が「自分の返した候補は、渡された <see cref="IAssetManagement"/> が今読む directory の物か」を答える口。
    /// <see cref="ContentRequestLoader"/> は採用した候補の供給元にこれを尋ね、真の時だけ content 入口を呼ぶ。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 照合を供給元に置く理由: 供給元は自分の登録（session）を知っているが、loader は AssetManagement の public だけを使い、
    /// AssetManagement は登録中の directory の identity を公開しない。identity を public に出すと「directory は 1 つ」を
    /// 公開面に固定することになるため、登録を知る側が答える。session ごとに答えるので、directory が増えても形は変わらない。
    /// </para>
    /// <para>
    /// 同期で答える。loader は照合と content 入口の呼び出しの間に <c>await</c> を置かず、content 入口は最初の <c>await</c> より前に
    /// その時点の directory を掴む。照合を非同期にすると、その間に directory が入れ替わりうる。
    /// 実物の AssetManagement 以外（fake、wrapper、別実装）を渡された時は、今読む directory を確かめられないので偽を返す。
    /// </para>
    /// <para>
    /// public にしない。供給元の実装は同じ assembly の連携部分にだけ置き、照合を答えない外部の供給元の候補は読まない
    /// （<see cref="ContentLoadRejection.SourceBindingUnavailable"/>）。
    /// </para>
    /// </remarks>
    internal interface IContentSourceBinding
    {
        /// <summary>
        /// <paramref name="candidate"/> がこの供給元の候補で、<paramref name="assetManagement"/> が今この供給元の directory を
        /// 読み、その directory が受付中で、候補の identity がその directory の検証済みの値と全欄一致する時だけ true。
        /// 例外を投げずに偽を返す（照合できないことは「一致しない」と同じ扱いで、load を始めない）。
        /// </summary>
        bool IsLoadedBy(ContentCandidate candidate, IAssetManagement assetManagement);
    }
}
