#nullable enable

#if UNITY_EDITOR
using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.AssetManagement;

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>
    /// 既ロードの経路（<see cref="ContentLoadPath.AlreadyLoadedScene"/>）の候補を返す供給元が、<see cref="ContentRequestLoader"/> に答える口。
    /// loader は AssetManagement の public だけを使うので、Scene の台帳に預ける internal の口は供給元（連携部分）が呼ぶ。
    /// </summary>
    /// <remarks>
    /// <para>
    /// public にしない。照合と load を答えない外部の供給元の既ロードの候補は、今までどおり読まない（<see cref="ContentLoadRejection.LoadPathUnsupported"/>）。
    /// </para>
    /// <para>
    /// Editor の build だけ（既ロードの候補を返すのは Editor の Play の複製だけ）。
    /// </para>
    /// </remarks>
    internal interface IContentSceneLoadBinding
    {
        /// <summary>
        /// この供給元が <paramref name="sceneIdentity"/> の Scene を宣言しているか（その identity の要求に、既ロードの経路の候補を必ず返すか）。
        /// SceneDirector は真の identity に限り、load 済みの同名の Scene を名前で再利用する前に loader に尋ねる。
        /// </summary>
        bool Declares(string sceneIdentity);

        /// <summary>
        /// <paramref name="candidate"/> がこの供給元の候補で、<paramref name="assetManagement"/> がこの供給元の Scene を預かる実物か。
        /// 同期で、例外を投げずに偽を返す（偽なら load を始めない）。
        /// </summary>
        bool IsLoadedBy(ContentCandidate candidate, IAssetManagement assetManagement);

        /// <summary>
        /// 候補の Scene を Scene の台帳に預けて handle を返す。返した Scene は load を終えた時点で照合を通った物（名前・複製の path・同名が他に無い）で、
        /// 解放は <see cref="IAssetManagement.UnloadSceneAsync"/> → <see cref="IAssetManagement.ReleaseScene"/>。
        /// 採れない時の例外は Scene の identity と理由を文に持ち、loader は包まない。
        /// </summary>
        UniTask<ISceneHandle> LoadSceneAsync(ContentCandidate candidate, IAssetManagement assetManagement, SceneLoadOptions options, CancellationToken ct);
    }
}
#endif
