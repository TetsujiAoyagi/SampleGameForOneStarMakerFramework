#nullable enable

#if UNITY_EDITOR
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace OneStarMaker.Runtime.AssetManagement.Internal
{
    /// <summary>
    /// AssetManagement の外で読んだ Scene を Scene の台帳に預ける側（預け元）の口。load と unload の実体は預け元が持ち、
    /// 台帳は identity・同時 load の gate・解放の入口だけを持つ。台帳は預け元が何から Scene を読むか（Editor の Play の複製など）を知らない。
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unity の <c>Scene</c> ではなく <see cref="IBackendScene"/> を返すので、test は Unity の Scene なしに fake を作れる。
    /// </para>
    /// <para>
    /// Editor の build だけの口である（Player には預ける Scene が無い）。1 つの AssetManagement に据える預け元は 1 つで、
    /// 据えた時点の <see cref="ReservedIdentities"/> は 1 回の Play の間変わらない前提で読む。
    /// </para>
    /// </remarks>
    internal interface IAdoptedSceneSource
    {
        /// <summary>
        /// 預け元が供給する Scene の identity。台帳はこの identity を directory の content 入口から読ませない（予約）。
        /// 据えた後に変えない。
        /// </summary>
        IReadOnlyCollection<string> ReservedIdentities { get; }

        /// <summary>
        /// <paramref name="sceneIdentity"/> の Scene を取得する（load 済みなら採り、無ければ読む）。返す Scene は預け元が照合を済ませた物で、
        /// 採れない時は例外を投げ、自分が読んだ Scene は unload してから投げる。取消を受けない（Unity の Scene の load は途中で止まらない）。
        /// </summary>
        UniTask<IBackendScene> AcquireAsync(string sceneIdentity);

        /// <summary>
        /// <see cref="AcquireAsync"/> が返した Scene を unload する。load 済みの Scene を unload できなかった時は例外を投げる
        /// （台帳は未 unload のまま残り、呼び手はもう一度 unload できる）。
        /// </summary>
        UniTask UnloadAsync(IBackendScene scene);
    }
}
#endif
