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
    /// CameraPose から算出した 6 平面の視錐台。内外判定（ContainsPoint）を Unity の Camera 抜きで行い、
    /// ストリーミングの可視判定やテストで決定的に使えるようにする。平面順序は Unity の CalculateFrustumPlanes に揃える。
    /// </summary>
    public readonly struct CameraFrustum : IEquatable<CameraFrustum>
    {
        public Plane Left { get; init; }
        public Plane Right { get; init; }
        public Plane Bottom { get; init; }
        public Plane Top { get; init; }
        public Plane Near { get; init; }
        public Plane Far { get; init; }

        public static CameraFrustum FromPose(in CameraPose pose)
        {
            var rotation = SanitizeRotation(pose.Rotation);
            var forward = rotation * Vector3.forward;
            var right = rotation * Vector3.right;
            var up = rotation * Vector3.up;
            var position = pose.Position;

            var halfFovRad = pose.FieldOfViewDegrees * 0.5f * Mathf.Deg2Rad;
            var tanHalfFov = Mathf.Tan(halfFovRad);

            var nearHalfHeight = pose.NearClip * tanHalfFov;
            var nearHalfWidth = nearHalfHeight * pose.Aspect;

            var nearCenter = position + forward * pose.NearClip;
            var farCenter = position + forward * pose.FarClip;

            var nearTopLeft = nearCenter + up * nearHalfHeight - right * nearHalfWidth;
            var nearTopRight = nearCenter + up * nearHalfHeight + right * nearHalfWidth;
            var nearBottomLeft = nearCenter - up * nearHalfHeight - right * nearHalfWidth;
            var nearBottomRight = nearCenter - up * nearHalfHeight + right * nearHalfWidth;

            return new CameraFrustum
            {
                Left = new Plane(position, nearTopLeft, nearBottomLeft),
                Right = new Plane(position, nearBottomRight, nearTopRight),
                Bottom = new Plane(position, nearBottomLeft, nearBottomRight),
                Top = new Plane(position, nearTopRight, nearTopLeft),
                Near = new Plane(forward, nearCenter),
                Far = new Plane(-forward, farCenter),
            };
        }

        public bool ContainsPoint(Vector3 point) =>
            Left.GetDistanceToPoint(point) >= 0f
            && Right.GetDistanceToPoint(point) >= 0f
            && Bottom.GetDistanceToPoint(point) >= 0f
            && Top.GetDistanceToPoint(point) >= 0f
            && Near.GetDistanceToPoint(point) >= 0f
            && Far.GetDistanceToPoint(point) >= 0f;

        public Plane[] ToArray() => new[] { Left, Right, Bottom, Top, Near, Far };

        public bool Equals(CameraFrustum other) =>
            Left.Equals(other.Left)
            && Right.Equals(other.Right)
            && Bottom.Equals(other.Bottom)
            && Top.Equals(other.Top)
            && Near.Equals(other.Near)
            && Far.Equals(other.Far);

        public override bool Equals(object? obj) => obj is CameraFrustum other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Left, Right, Bottom, Top, Near, Far);

        // 全成分 0 の Quaternion（未初期化 default 値）は回転として無効なので identity へ矯正し、
        // 平面計算が NaN 化するのを防ぐ。
        private static Quaternion SanitizeRotation(Quaternion rotation)
        {
            if (rotation.x == 0f && rotation.y == 0f && rotation.z == 0f && rotation.w == 0f)
            {
                return Quaternion.identity;
            }

            return rotation;
        }
    }
}
