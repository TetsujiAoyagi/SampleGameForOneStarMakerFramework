#nullable enable

using System;
using UnityEngine;

namespace OneStarMaker.Runtime.Rendering.Vfx
{
    /// <summary>
    /// 起動した 1 instance への操作。VFX 実装の型に依存しない。
    /// <para>
    /// 状態は 生成済み → 再生中 → 消え切り待ち → 回収済み の一方向で、回収済みは終端。
    /// 所有者が終端に入ると、呼び出し側が何もしなくても回収される。回収済みの handle への操作は何もしない。
    /// 非対応環境や拒否された起動では、最初から回収済みの何もしない handle が返る。
    /// </para>
    /// <para>
    /// <see cref="IDisposable.Dispose"/> は <see cref="Stop"/> と同じ即時の回収で、消え切り待ちの途中でも
    /// 待ちを打ち切る。<c>using</c> で囲むと消え切り待ちは打ち切られる。
    /// </para>
    /// </summary>
    public interface IVfxHandle : IDisposable
    {
        /// <summary>回収済みでなければ true。消え切り待ちの間も true。</summary>
        bool IsActive { get; }

        /// <summary>
        /// 発射する。再生中なら再トリガする。backend が再生できなかった場合と、消え切り待ち・回収済みでは false。
        /// </summary>
        bool Play();

        /// <summary>即時に止めて回収する。消え切り待ちの途中でも待ちを打ち切る。</summary>
        void Stop();

        /// <summary>
        /// 放出を止め、生存中の粒子が消え切った時点（または timeout）で回収する。
        /// 再生前なら粒子が無いので即時に回収する。消え切り待ち中に再び呼んでも待ちを延長しない。
        /// </summary>
        void StopAllowingParticlesToFinish();

        /// <summary>名前付きパラメータを設定し、backend の成否を返す。名前が null・空なら何もせず false。</summary>
        bool SetFloat(string name, float value);

        bool SetInt(string name, int value);

        bool SetBool(string name, bool value);

        bool SetVector3(string name, Vector3 value);

        bool SetVector4(string name, Vector4 value);
    }
}
