#nullable enable

using NUnit.Framework;
using OneStarMaker.Runtime.CameraSystem;
using UnityEngine;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.CameraSystem.Core;
using OneStarMaker.Runtime.CameraSystem.Effects;
using OneStarMaker.Runtime.CameraSystem.Geometry;
using OneStarMaker.Runtime.CameraSystem.Hosting;
using OneStarMaker.Runtime.CameraSystem.Modifiers;
using OneStarMaker.Runtime.CameraSystem.Stacking;
using OneStarMaker.Runtime.CameraSystem.Telemetry;

namespace OneStarMaker.Tests.CameraSystem
{
    /// <summary>
    /// CAM-02: CameraPose / フラスタム計算 / CameraViewSnapshot のレッドテスト。
    /// </summary>
    [TestFixture]
    public class CameraFrustumTests
    {
        // ═══════════════════════════════════════════
        //  テスト用ヘルパー
        // ═══════════════════════════════════════════

        private static CameraPose CreatePose(
            Vector3 position,
            Quaternion rotation,
            float fov = 60f,
            float near = 0.3f,
            float far = 100f,
            float aspect = 1f)
        {
            return new CameraPose
            {
                Position = position,
                Rotation = rotation,
                FieldOfViewDegrees = fov,
                NearClip = near,
                FarClip = far,
                Aspect = aspect,
            };
        }

        [Test]
        public void Frustum_PointInFrontWithinFov_IsInside()
        {
            var pose = CreatePose(Vector3.zero, Quaternion.identity);
            var frustum = CameraFrustum.FromPose(pose);
            var point = new Vector3(0f, 0f, 10f);

            Assert.That(frustum.ContainsPoint(point), Is.True);
        }

        [Test]
        public void Frustum_PointBehindCamera_IsOutside()
        {
            var pose = CreatePose(Vector3.zero, Quaternion.identity);
            var frustum = CameraFrustum.FromPose(pose);
            var point = new Vector3(0f, 0f, -1f);

            Assert.That(frustum.ContainsPoint(point), Is.False);
        }

        [Test]
        public void Frustum_PointBeyondFarPlane_IsOutside()
        {
            var pose = CreatePose(Vector3.zero, Quaternion.identity, far: 50f);
            var frustum = CameraFrustum.FromPose(pose);
            var point = new Vector3(0f, 0f, 100f);

            Assert.That(frustum.ContainsPoint(point), Is.False);
        }

        [Test]
        public void Frustum_PointNearerThanNearPlane_IsOutside()
        {
            var pose = CreatePose(Vector3.zero, Quaternion.identity, near: 1f);
            var frustum = CameraFrustum.FromPose(pose);
            var point = new Vector3(0f, 0f, 0.5f);

            Assert.That(frustum.ContainsPoint(point), Is.False);
        }

        [Test]
        public void Frustum_AspectAffectsHorizontalPlanes()
        {
            var narrowPose = CreatePose(Vector3.zero, Quaternion.identity, aspect: 1f);
            var widePose = CreatePose(Vector3.zero, Quaternion.identity, aspect: 2f);
            var narrowFrustum = CameraFrustum.FromPose(narrowPose);
            var wideFrustum = CameraFrustum.FromPose(widePose);

            var offAxisPoint = new Vector3(8f, 0f, 10f);
            Assert.That(narrowFrustum.ContainsPoint(offAxisPoint), Is.False);
            Assert.That(wideFrustum.ContainsPoint(offAxisPoint), Is.True);
        }

        [Test]
        public void Frustum_RotatedPose_PlanesFollow()
        {
            var pose = CreatePose(Vector3.zero, Quaternion.Euler(0f, 90f, 0f));
            var frustum = CameraFrustum.FromPose(pose);
            var pointInFront = new Vector3(10f, 0f, 0f);
            var pointBehind = new Vector3(-1f, 0f, 0f);

            Assert.That(frustum.ContainsPoint(pointInFront), Is.True);
            Assert.That(frustum.ContainsPoint(pointBehind), Is.False);
        }

        [Test]
        public void Frustum_MatchesUnityGeometryUtility()
        {
            var pose = CreatePose(
                new Vector3(1f, 2f, 3f),
                Quaternion.Euler(10f, 20f, 0f),
                fov: 55f,
                near: 0.5f,
                far: 200f,
                aspect: 1.6f);

            var actual = CameraFrustum.FromPose(pose).ToArray();
            var expected = CalculateFrustumPlanesWithUnityCamera(pose);

            Assert.That(actual.Length, Is.EqualTo(6));
            Assert.That(expected.Length, Is.EqualTo(6));
            for (var i = 0; i < actual.Length; i++)
            {
                // 内外判定だけでは平面の順序・距離のズレを見逃すため、Unity と同じ配列順で係数を比較する。
                AssertPlaneMatches(actual[i], expected[i], i);
            }
        }

        [Test]
        public void Snapshot_ContainsPoseAndVelocity()
        {
            var previous = CreatePose(Vector3.zero, Quaternion.identity);
            var current = CreatePose(new Vector3(0f, 0f, 5f), Quaternion.identity);
            const float deltaTime = 0.5f;

            var snapshot = CameraViewSnapshot.Create(current, previous, deltaTime);

            Assert.That(snapshot.Pose, Is.EqualTo(current));
            Assert.That(snapshot.Velocity, Is.EqualTo(new Vector3(0f, 0f, 10f)));
            Assert.That(snapshot.Frustum.ContainsPoint(new Vector3(0f, 0f, 10f)), Is.True);
        }

        private static Plane[] CalculateFrustumPlanesWithUnityCamera(in CameraPose pose)
        {
            var gameObject = new GameObject("FrustumCrossCheckCamera");
            try
            {
                var camera = gameObject.AddComponent<Camera>();
                camera.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
                camera.fieldOfView = pose.FieldOfViewDegrees;
                camera.nearClipPlane = pose.NearClip;
                camera.farClipPlane = pose.FarClip;
                camera.aspect = pose.Aspect;

                var planes = GeometryUtility.CalculateFrustumPlanes(camera);
                return planes;
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private static void AssertPlaneMatches(Plane actual, Plane expected, int index)
        {
            const float tolerance = 1e-3f;
            Assert.That(actual.normal.x, Is.EqualTo(expected.normal.x).Within(tolerance), $"plane[{index}] normal.x");
            Assert.That(actual.normal.y, Is.EqualTo(expected.normal.y).Within(tolerance), $"plane[{index}] normal.y");
            Assert.That(actual.normal.z, Is.EqualTo(expected.normal.z).Within(tolerance), $"plane[{index}] normal.z");
            Assert.That(actual.distance, Is.EqualTo(expected.distance).Within(tolerance), $"plane[{index}] distance");
        }
    }
}
