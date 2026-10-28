#nullable enable

using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.UpdateSystem.Api;
using Object = UnityEngine.Object;

namespace OneStarMaker.Runtime.Rendering.Vfx
{
    /// <summary>
    /// Game が VFX を起動する窓口。
    /// <para>
    /// <paramref name="owner"/> の寿命で instance が回収される。<c>Scene(id)</c> はその Scene の Unload 開始
    /// （Unity Scene の unload より前）またはロードのキャンセルで、<c>Bind(go)</c> は go の破棄で、
    /// <c>App</c> / <c>Manual</c> は VfxSystem の Dispose で回収される。回収するのは再生中の instance で、
    /// source asset の寿命は <c>IAssetManagement</c> に残る。
    /// </para>
    /// <para>
    /// 非対応環境、<c>default(AssetOwner)</c>、終端中・未ロードの Scene、破棄済みの go・source・親を渡した場合は、
    /// 例外を投げずに何もしない handle を返す。Game 側で対応可否を分岐する必要はない。
    /// </para>
    /// <para>
    /// 時間: 全ての VFX は時間の領域を 1 つ持ち、その Layer の pause・time scale に、gameplay のコードが受け取る
    /// delta と同じ時間で追従する。止まっている間は開始もしない（<see cref="IVfxHandle.Play"/> は受理して保留する）。
    /// 消え切り待ちの timeout もその領域の時間で数えるので、止まっている間は進まない。実時間での出口は所有者の終端で、
    /// 時間が止まり続けると <c>App</c> / <c>Manual</c> 所有の消え切り待ちは VfxSystem の Dispose まで残る。
    /// </para>
    /// </summary>
    public interface IVfxSystem
    {
        IVfxHandle Spawn(in VfxSpawnRequest request, AssetOwner owner);

        /// <summary>
        /// Scene に配置済みの VFX を登録し、動的な VFX と同じ規則（時間の追従・回収・非対応環境での停止）に乗せる。
        /// <para>
        /// <paramref name="placed"/> は配置済みの VFX の component（Framework は型を知らない）。無効で保存しておく。
        /// 登録だけで開始はせず、時間の領域が止まっていない最初の反映点で自動的に開始する（その環境の標準の有効化）。
        /// <paramref name="owner"/> は配置した Scene の <c>Scene(id)</c> を渡す。Unload の開始で回収され、
        /// 回収は無効化と登録解除だけで GameObject を破棄しない。回収は終端で、同じ handle から再び有効にはならない。
        /// </para>
        /// <para>
        /// 同じ component が登録中なら 2 件目は拒否して何もしない handle を返す。非対応環境、破棄済みの
        /// <paramref name="placed"/>、受理できない所有者でも何もしない handle を返し、component は無効のまま残る。
        /// </para>
        /// </summary>
        /// <param name="timeLayerId">
        /// 所属する時間の領域の Layer id。null は実時間（unscaled）。<c>UpdateLayerIds.Vfx</c> と空白は引数の誤りとして
        /// <see cref="System.ArgumentException"/> を投げる。
        /// </param>
        IVfxHandle Adopt(Object placed, AssetOwner owner, string? timeLayerId = UpdateLayerIds.Gameplay);
    }
}
