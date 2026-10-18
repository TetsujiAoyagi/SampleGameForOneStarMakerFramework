#nullable enable

using OneStarMaker.Runtime.AssetManagement;

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
    /// </summary>
    public interface IVfxSystem
    {
        IVfxHandle Spawn(in VfxSpawnRequest request, AssetOwner owner);
    }
}
