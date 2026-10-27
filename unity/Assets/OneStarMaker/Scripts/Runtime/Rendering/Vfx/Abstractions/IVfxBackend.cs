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
    /// <para>
    /// instance には 2 種類ある。<see cref="Create"/> で作る動的な instance と、Scene に配置済みの VFX を
    /// <see cref="Adopt"/> で登録した instance（adopted）である。回収は動的なら <see cref="Destroy"/>、adopted なら
    /// <see cref="Release"/> で、どちらか一方だけが id ごとに高々 1 回呼ばれる。
    /// </para>
    /// <para>
    /// 時間: VfxSystem はフレームごとの反映点（<c>Vfx</c> Layer の LateUpdate）で、値が変わったときだけ
    /// <see cref="SetPlayback"/> を呼ぶ。初めて呼ばれるまでの instance は「止まっていない、倍率 1」とみなされる
    /// （backend は作った・登録した instance をその状態で用意する）。
    /// 時間の領域が止まっている間は <see cref="Play"/> が呼ばれず、開始は止まっていない最初の反映点で
    /// <see cref="SetPlayback"/> の後に呼ばれる。
    /// </para>
    /// </summary>
    public interface IVfxBackend
    {
        /// <summary>この環境で VFX を描画できるか。false なら <paramref name="reason"/> に診断用の理由を入れる。</summary>
        bool QueryCapability(out string? reason);

        VfxInstanceId Create(in VfxSpawnRequest request);

        /// <summary>
        /// 発射する。再生中に呼ばれたら再トリガする。false は「再生できなかった」で、instance は残す。
        /// adopted の instance では、最初の 1 回が有効化（配置済み VFX が自動で再生を始める、その環境の標準の開始）で、
        /// 2 回目以降は動的な instance と同じ再トリガとする。
        /// </summary>
        bool Play(VfxInstanceId id);

        /// <summary>
        /// 再生状態を書く。<paramref name="paused"/> が true なら simulation を進めない。
        /// <paramref name="rate"/> は「このフレーム、effect が実時間（unscaled delta）に対して何倍で進むべきか」で、
        /// 0 以上の有限値。backend 自身の delta の扱い（delta の上限や更新方式）への補正は backend が持ち、
        /// 渡された倍率のとおりに effect の時間が進むようにする。
        /// </summary>
        void SetPlayback(VfxInstanceId id, bool paused, float rate);

        /// <summary>
        /// Scene に配置済みの VFX（その環境の VFX component）を登録して id を返す。登録だけで、有効化も開始もしない
        /// （開始は <see cref="Play"/>）。扱えない対象なら <see cref="VfxInstanceId.Invalid"/> か例外を返す。
        /// GameObject の所有は Scene に残り、backend は破棄しない。
        /// </summary>
        VfxInstanceId Adopt(Object placed);

        /// <summary>
        /// adopted の instance の登録を外す。無効化して登録から外すだけで、GameObject も component も破棄しない。
        /// 破棄済みの対象にも安全に完了する。外した後で同じ id へ他のメソッドは呼ばれない。
        /// </summary>
        void Release(VfxInstanceId id);

        /// <summary>新しい粒子の放出を止める。生存中の粒子は消えるまで残す。</summary>
        void StopEmitting(VfxInstanceId id);

        bool HasAliveParticles(VfxInstanceId id);

        bool SetFloat(VfxInstanceId id, string name, float value);

        bool SetInt(VfxInstanceId id, string name, int value);

        bool SetBool(VfxInstanceId id, string name, bool value);

        bool SetVector3(VfxInstanceId id, string name, Vector3 value);

        bool SetVector4(VfxInstanceId id, string name, Vector4 value);

        /// <summary>動的な instance を破棄する。adopted の instance には呼ばれない（<see cref="Release"/> を使う）。</summary>
        void Destroy(VfxInstanceId id);
    }
}
