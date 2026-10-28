#nullable enable

using UnityEngine;

namespace OneStarMaker.Runtime.Rendering.Vfx
{
    /// <summary>
    /// Game が VFX backend を構成しなかった（<c>CreateVfxBackend</c> が null を返した）ことを示す backend。
    /// <para>
    /// Game が常に非 null の <see cref="IVfxSystem"/> を受け取れるように置く（Game に「VFX があるか」の分岐を書かせない）。
    /// 能力判定は常に false で、VfxSystem の診断ログ 1 件の理由文によって、非対応環境とは区別できる。
    /// false を返した backend は以後呼ばれない（<see cref="IVfxBackend"/> の契約）ので、他のメソッドは何もしない。
    /// </para>
    /// </summary>
    internal sealed class UnconfiguredVfxBackend : IVfxBackend
    {
        /// <summary>診断ログの理由文。非対応環境の理由と取り違えないよう、型名を含めて固定する。</summary>
        internal const string Reason =
            "UnconfiguredVfxBackend: the application initializer did not provide a VFX backend (CreateVfxBackend returned null).";

        public bool QueryCapability(out string? reason)
        {
            reason = Reason;
            return false;
        }

        public VfxInstanceId Create(in VfxSpawnRequest request) => VfxInstanceId.Invalid;

        public bool Play(VfxInstanceId id) => false;

        public void SetPlayback(VfxInstanceId id, bool paused, float rate)
        {
        }

        public VfxInstanceId Adopt(Object placed) => VfxInstanceId.Invalid;

        public void Release(VfxInstanceId id)
        {
        }

        public void StopEmitting(VfxInstanceId id)
        {
        }

        public bool HasAliveParticles(VfxInstanceId id) => false;

        public bool SetFloat(VfxInstanceId id, string name, float value) => false;

        public bool SetInt(VfxInstanceId id, string name, int value) => false;

        public bool SetBool(VfxInstanceId id, string name, bool value) => false;

        public bool SetVector3(VfxInstanceId id, string name, Vector3 value) => false;

        public bool SetVector4(VfxInstanceId id, string name, Vector4 value) => false;

        public void Destroy(VfxInstanceId id)
        {
        }
    }
}
