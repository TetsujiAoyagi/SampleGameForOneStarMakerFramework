#nullable enable

using System;
using UnityEngine;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.CameraSystem.Core;
using OneStarMaker.Runtime.CameraSystem.Effects;
using OneStarMaker.Runtime.CameraSystem.Geometry;
using OneStarMaker.Runtime.CameraSystem.Hosting;
using OneStarMaker.Runtime.CameraSystem.Modifiers;
using OneStarMaker.Runtime.CameraSystem.Stacking;
using OneStarMaker.Runtime.CameraSystem.Telemetry;

namespace OneStarMaker.Runtime.CameraSystem.Geometry
{
    /// <summary>
    /// カメラの純粋な姿勢データ（位置・回転・レンズ）。Unity 型に依存する Camera とは切り離した値オブジェクトで、
    /// テストとブレンド計算を決定的に扱えるようにする。位置/回転だけ差し替える With 系を持つ。
    /// </summary>
    public readonly struct CameraPose : IEquatable<CameraPose>
    {
        public Vector3 Position { get; init; }
        public Quaternion Rotation { get; init; }
        public float FieldOfViewDegrees { get; init; }
        public float NearClip { get; init; }
        public float FarClip { get; init; }
        public float Aspect { get; init; }

        public CameraPose WithPosition(Vector3 position) =>
            new()
            {
                Position = position,
                Rotation = Rotation,
                FieldOfViewDegrees = FieldOfViewDegrees,
                NearClip = NearClip,
                FarClip = FarClip,
                Aspect = Aspect,
            };

        public CameraPose WithRotation(Quaternion rotation) =>
            new()
            {
                Position = Position,
                Rotation = rotation,
                FieldOfViewDegrees = FieldOfViewDegrees,
                NearClip = NearClip,
                FarClip = FarClip,
                Aspect = Aspect,
            };

        public bool Equals(CameraPose other) =>
            Position.Equals(other.Position)
            && Rotation.Equals(other.Rotation)
            && FieldOfViewDegrees.Equals(other.FieldOfViewDegrees)
            && NearClip.Equals(other.NearClip)
            && FarClip.Equals(other.FarClip)
            && Aspect.Equals(other.Aspect);

        public override bool Equals(object? obj) => obj is CameraPose other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Position, Rotation, FieldOfViewDegrees, NearClip, FarClip, Aspect);

        public static bool operator ==(CameraPose left, CameraPose right) => left.Equals(right);

        public static bool operator !=(CameraPose left, CameraPose right) => !left.Equals(right);
    }
}
