#nullable enable

using UnityEngine;

namespace OneStarMaker.Runtime.Rendering.Vfx
{
    /// <summary>
    /// VFX 実装（VFX Graph 等）への差し込み口。VFX 実装の型はこの面に出さない。
    /// VfxSystem は backend を借用するだけで、所有も Dispose もしない。
    /// <para>契約:</para>
    /// <list type="bullet">
    /// <item>Unity の main thread からだけ呼ばれる。</item>
    /// <item><see cref="QueryCapability"/> は VfxSystem の生涯で1回だけ呼ばれる。false なら以後は何も呼ばれない。</item>
    /// <item><see cref="Create"/> は instance を作るだけで発射しない。発射は <see cref="Play"/> だけが行う
    /// （VfxSystem は未再生の instance を消え切り待ちなしで即破棄する）。</item>
    /// <item><see cref="Create"/> が id を返す前に失敗したら、作りかけの資源は backend が後始末する。
    /// 失敗は例外か <see cref="VfxInstanceId.Invalid"/> で返す。</item>
    /// <item>同時に生存する instance に同じ id を返さない。</item>
    /// <item><see cref="Destroy"/> は id ごとに高々1回呼ばれる。破棄済み GameObject・失効した source に対しても
    /// 安全に完了し、失敗した場合の回復は backend の責任とする（VfxSystem は再試行しない）。</item>
    /// <item>どのメソッドが例外を投げても、VfxSystem はその instance だけを回収して続行する。</item>
    /// <item>backend はログを出さない。診断ログは VfxSystem が出す。</item>
    /// </list>
    /// </summary>
    public interface IVfxBackend
    {
        /// <summary>この環境で VFX を描画できるか。false なら <paramref name="reason"/> に診断用の理由を入れる。</summary>
        bool QueryCapability(out string? reason);

        VfxInstanceId Create(in VfxSpawnRequest request);

        /// <summary>発射する。再生中に呼ばれたら再トリガする。false は「再生できなかった」で、instance は残す。</summary>
        bool Play(VfxInstanceId id);

        /// <summary>新しい粒子の放出を止める。生存中の粒子は消えるまで残す。</summary>
        void StopEmitting(VfxInstanceId id);

        bool HasAliveParticles(VfxInstanceId id);

        bool SetFloat(VfxInstanceId id, string name, float value);

        bool SetInt(VfxInstanceId id, string name, int value);

        bool SetBool(VfxInstanceId id, string name, bool value);

        bool SetVector3(VfxInstanceId id, string name, Vector3 value);

        bool SetVector4(VfxInstanceId id, string name, Vector4 value);

        void Destroy(VfxInstanceId id);
    }
}
