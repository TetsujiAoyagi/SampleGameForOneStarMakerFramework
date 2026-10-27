#nullable enable

using OneStarMaker.Runtime.UpdateSystem.Api;
using UnityEngine;

namespace OneStarMaker.Runtime.Rendering.Vfx
{
    /// <summary>
    /// VFX の起動要求。位置と回転は world 座標。
    /// <para>
    /// <see cref="Source"/> は呼び出し側が <c>IAssetManagement</c> でロードした asset をそのまま渡す。
    /// VfxSystem は asset handle を保持も Release もしないので、呼び出し側は、その source から作った
    /// instance が回収されるまで source を有効に保つ。asset の所有者と VFX の所有者を同じ
    /// <c>AssetOwner</c> にしても解放順は保証されない（同じ GameObject に付く破棄通知の順序が決まっていない）。
    /// asset の所有者が VFX の所有者より長く生きる組み合わせ（例: Scene 所有で asset をロードし、
    /// その Scene の中の GameObject に Bind した VFX を起動する）を使う。
    /// </para>
    /// <para>
    /// 親の有無は <see cref="HasParent"/> で持つ。破棄済みの Transform は Unity の偽 null になり、
    /// 参照の比較では「未指定」と区別できないため。親ありで <see cref="Parent"/> が破棄済みなら起動は拒否される。
    /// <c>default(VfxSpawnRequest)</c> は <see cref="Source"/> が null なので拒否される。
    /// </para>
    /// <para>
    /// 時間の領域は既定でゲームの世界の時間（<see cref="UpdateLayerIds.Gameplay"/> の Layer）で、その Layer の
    /// pause・time scale に再生が追従する。UI の演出など、ゲームが止まっても動かし続けるものだけ
    /// <see cref="WithUnscaledTime"/> で明示して外す。
    /// </para>
    /// </summary>
    public readonly struct VfxSpawnRequest
    {
        // default(VfxSpawnRequest) でも gameplay の領域になるよう、unscaled の側を印にする。
        private readonly bool _unscaledTime;

        /// <summary>親なしで起動する。どの Unity Scene に置くかは backend が決める。</summary>
        public VfxSpawnRequest(Object source, Vector3 position, Quaternion rotation)
        {
            Source = source;
            Position = position;
            Rotation = rotation;
            HasParent = false;
            Parent = null;
            _unscaledTime = false;
        }

        /// <summary>親の下に起動する。<paramref name="parent"/> が破棄済みなら起動は拒否される。</summary>
        public VfxSpawnRequest(Object source, Vector3 position, Quaternion rotation, Transform parent)
        {
            Source = source;
            Position = position;
            Rotation = rotation;
            HasParent = true;
            Parent = parent;
            _unscaledTime = false;
        }

        private VfxSpawnRequest(in VfxSpawnRequest other, bool unscaledTime)
        {
            Source = other.Source;
            Position = other.Position;
            Rotation = other.Rotation;
            HasParent = other.HasParent;
            Parent = other.Parent;
            _unscaledTime = unscaledTime;
        }

        /// <summary>
        /// 所属する時間の領域の Layer id。既定は <see cref="UpdateLayerIds.Gameplay"/>。
        /// null は「Layer に属さず実時間（unscaled）で進む」を表す（<see cref="WithUnscaledTime"/> で作った要求）。
        /// </summary>
        public string? TimeLayerId => _unscaledTime ? null : UpdateLayerIds.Gameplay;

        /// <summary>
        /// 同じ要求を、実時間（unscaled）で進むものとして返す。どの Layer の pause・time scale にも追従しない。
        /// 消え切り待ちの timeout も実時間で数える。
        /// </summary>
        public VfxSpawnRequest WithUnscaledTime() => new(this, unscaledTime: true);

        public Object? Source { get; }

        public Vector3 Position { get; }

        public Quaternion Rotation { get; }

        public bool HasParent { get; }

        public Transform? Parent { get; }
    }
}
