#nullable enable

using UnityEngine;

namespace OneStarMaker.Runtime.Rendering.Vfx
{
    /// <summary>
    /// 何もしない handle。非対応環境と、拒否・失敗した起動で返す。backend を一切呼ばない。
    /// 状態を持たないので全呼び出し元で共有する。
    /// </summary>
    internal sealed class NullVfxHandle : IVfxHandle
    {
        internal static readonly NullVfxHandle Instance = new();

        private NullVfxHandle()
        {
        }

        public bool IsActive => false;

        public bool Play() => false;

        public void Stop()
        {
        }

        public void StopAllowingParticlesToFinish()
        {
        }

        public bool SetFloat(string name, float value) => false;

        public bool SetInt(string name, int value) => false;

        public bool SetBool(string name, bool value) => false;

        public bool SetVector3(string name, Vector3 value) => false;

        public bool SetVector4(string name, Vector4 value) => false;

        public void Dispose()
        {
        }
    }
}
