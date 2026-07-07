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

namespace OneStarMaker.Runtime.CameraSystem.Geometry
{
    /// <summary>
    /// ある Tick で確定したカメラ観測結果。Pose と派生 Frustum に加え、前フレームとの差分から求めた速度を持つ。
    /// この Snapshot が View 外部（ストリーミング/テレメトリ）から参照される唯一の確定値になる。
    /// </summary>
    public readonly struct CameraViewSnapshot
    {
        /// <summary>確定した姿勢。</summary>
        public CameraPose Pose { get; init; }

        /// <summary>Pose から導出した視錐台。</summary>
        public CameraFrustum Frustum { get; init; }

        /// <summary>前フレーム位置との差分速度。初回フレームは 0。</summary>
        public Vector3 Velocity { get; init; }

        /// <summary>初回フレーム用。速度 0 で生成する。default 回転は identity に矯正する。</summary>
        public static CameraViewSnapshot CreateInitial(in CameraPose pose)
        {
            var safePose = pose;
            if (safePose.Rotation.x == 0f && safePose.Rotation.y == 0f
                && safePose.Rotation.z == 0f && safePose.Rotation.w == 0f)
            {
                safePose = safePose.WithRotation(Quaternion.identity);
            }

            return new CameraViewSnapshot
            {
                Pose = safePose,
                Frustum = CameraFrustum.FromPose(safePose),
                Velocity = Vector3.zero,
            };
        }

        /// <summary>前フレーム Pose と deltaTime から速度を求めて生成する。deltaTime が 0 以下なら速度 0。</summary>
        public static CameraViewSnapshot Create(in CameraPose pose, in CameraPose previousPose, float deltaTime)
        {
            var velocity = Vector3.zero;
            if (deltaTime > 0f)
            {
                velocity = (pose.Position - previousPose.Position) / deltaTime;
            }

            return new CameraViewSnapshot
            {
                Pose = pose,
                Frustum = CameraFrustum.FromPose(pose),
                Velocity = velocity,
            };
        }
    }
}
