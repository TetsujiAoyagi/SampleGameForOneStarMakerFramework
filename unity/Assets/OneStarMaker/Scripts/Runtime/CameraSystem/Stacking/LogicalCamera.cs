#nullable enable

using UnityEngine;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.CameraSystem.Core;
using OneStarMaker.Runtime.CameraSystem.Effects;
using OneStarMaker.Runtime.CameraSystem.Geometry;
using OneStarMaker.Runtime.CameraSystem.Hosting;
using OneStarMaker.Runtime.CameraSystem.Modifiers;
using OneStarMaker.Runtime.CameraSystem.Stacking;
using OneStarMaker.Runtime.CameraSystem.Telemetry;

namespace OneStarMaker.Runtime.CameraSystem.Stacking
{
    /// <summary>
    /// ゲーム側が扱う「論理的な 1 台のカメラ」。レンズ設定と VolumeProfile を持つだけの純データで、
    /// 実際の Cinemachine カメラとのひも付けは Backend が Id 参照で管理する。同一 Id でも参照が別なら別カメラとして扱う。
    /// </summary>
    public sealed class LogicalCamera
    {
        public LogicalCamera(string id)
        {
            Id = id ?? throw new System.ArgumentNullException(nameof(id));
        }

        public string Id { get; }

        public float FieldOfViewDegrees { get; set; } = 60f;
        public float NearClip { get; set; } = 0.3f;
        public float FarClip { get; set; } = 1000f;
        public float Aspect { get; set; } = 16f / 9f;
        public int CullingMask { get; set; } = -1;

        public Object? VolumeProfile { get; set; }

        public CameraPose ToPose(Vector3 position, Quaternion rotation) =>
            new()
            {
                Position = position,
                Rotation = rotation,
                FieldOfViewDegrees = FieldOfViewDegrees,
                NearClip = NearClip,
                FarClip = FarClip,
                Aspect = Aspect,
            };
    }
}
