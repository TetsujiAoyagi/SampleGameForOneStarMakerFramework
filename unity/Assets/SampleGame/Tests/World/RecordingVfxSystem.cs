#nullable enable

using System.Collections.Generic;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.Rendering.Vfx;
using OneStarMaker.Runtime.UpdateSystem.Api;
using UnityEngine;

namespace SampleGame.Tests.World
{
    /// <summary>
    /// <see cref="IVfxSystem"/> への要求を記録するだけの偽物。handle の生存（<see cref="RecordingVfxHandle.IsActive"/>）は
    /// test が決める（消え切りの回収は <c>IsActive = false</c> で表す。Tick は無い）。
    /// 全 handle への操作は <see cref="Calls"/> に順に残るので、「新しい Spawn の前に古い handle を止めた」等の順序を判定できる。
    /// </summary>
    internal sealed class RecordingVfxSystem : IVfxSystem
    {
        public List<(VfxSpawnRequest Request, AssetOwner Owner, RecordingVfxHandle Handle)> Spawns { get; } = new();

        /// <summary>Spawn と handle 操作の順序（"Spawn#1"、"Play#1"、"Stop#1" の形。番号は Spawn の通し番号）。</summary>
        public List<string> Calls { get; } = new();

        public IVfxHandle Spawn(in VfxSpawnRequest request, AssetOwner owner)
        {
            var handle = new RecordingVfxHandle(Spawns.Count + 1, Calls);
            Spawns.Add((request, owner, handle));
            Calls.Add("Spawn#" + handle.Number);
            return handle;
        }

        public IVfxHandle Adopt(Object placed, AssetOwner owner, string? timeLayerId = UpdateLayerIds.Gameplay)
        {
            throw new System.NotSupportedException("局所 VFX の trigger は Adopt を使わない");
        }
    }

    internal sealed class RecordingVfxHandle : IVfxHandle
    {
        private readonly List<string> _calls;

        public RecordingVfxHandle(int number, List<string> calls)
        {
            Number = number;
            _calls = calls;
        }

        public int Number { get; }

        public bool IsActive { get; set; } = true;

        public int PlayCount { get; private set; }

        public int StopCount { get; private set; }

        public int StopAllowingCount { get; private set; }

        public bool Play()
        {
            PlayCount++;
            _calls.Add("Play#" + Number);
            return IsActive;
        }

        // 即時の回収なので、呼ばれた時点で回収済みにする。
        public void Stop()
        {
            StopCount++;
            _calls.Add("Stop#" + Number);
            IsActive = false;
        }

        public void StopAllowingParticlesToFinish()
        {
            StopAllowingCount++;
            _calls.Add("StopAllowing#" + Number);
        }

        public bool SetFloat(string name, float value) => false;

        public bool SetInt(string name, int value) => false;

        public bool SetBool(string name, bool value) => false;

        public bool SetVector3(string name, Vector3 value) => false;

        public bool SetVector4(string name, Vector4 value) => false;

        public void Dispose() => Stop();
    }
}
