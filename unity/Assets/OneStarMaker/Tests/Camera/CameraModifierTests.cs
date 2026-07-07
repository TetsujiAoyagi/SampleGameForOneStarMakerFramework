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
    /// CAM-04: Modifier スタックのレッドテスト。
    /// </summary>
    [TestFixture]
    public class CameraModifierTests
    {
        // ═══════════════════════════════════════════
        //  テスト用ヘルパー
        // ═══════════════════════════════════════════

        private static CameraPose BasePose =>
            new()
            {
                Position = Vector3.zero,
                Rotation = Quaternion.identity,
                FieldOfViewDegrees = 60f,
                NearClip = 0.3f,
                FarClip = 100f,
                Aspect = 16f / 9f,
            };

        [Test]
        public void Apply_ModifiersRunInRegistrationOrder()
        {
            var stack = new CameraModifierStack();
            using (stack.AddModifier(new ScaleXModifier(2f)))
            using (stack.AddModifier(new OffsetModifier(new Vector3(1f, 0f, 0f))))
            {
                var pose = BasePose;
                stack.Apply(ref pose, 0.016f);

                // 乗算→加算なら x=1、逆順（加算→乗算）なら x=2。非可換にして登録順の退行を検出する。
                Assert.That(pose.Position, Is.EqualTo(new Vector3(1f, 0f, 0f)));
            }
        }

        [Test]
        public void Apply_ReturnsFalse_ModifierAutoRemoved()
        {
            var stack = new CameraModifierStack();
            var modifier = new SingleUseModifier();
            stack.AddModifier(modifier);

            var pose = BasePose;
            Assert.That(modifier.ApplyCount, Is.EqualTo(0));

            stack.Apply(ref pose, 0.016f);
            Assert.That(modifier.ApplyCount, Is.EqualTo(1));

            stack.Apply(ref pose, 0.016f);
            Assert.That(modifier.ApplyCount, Is.EqualTo(1));
        }

        [Test]
        public void Apply_ReturnsFalse_RemovalDoesNotSkipNextModifier()
        {
            var stack = new CameraModifierStack();
            var removingModifier = new SingleUseModifier();
            var trailingModifier = new CountingModifier();
            stack.AddModifier(removingModifier);
            stack.AddModifier(trailingModifier);

            var pose = BasePose;
            stack.Apply(ref pose, 0.016f);

            // 先頭の Modifier が自己除去されても、同じフレームで後続 Modifier が実行されることを保証する。
            Assert.That(removingModifier.ApplyCount, Is.EqualTo(1));
            Assert.That(trailingModifier.ApplyCount, Is.EqualTo(1));

            pose = BasePose;
            stack.Apply(ref pose, 0.016f);

            Assert.That(removingModifier.ApplyCount, Is.EqualTo(1));
            Assert.That(trailingModifier.ApplyCount, Is.EqualTo(2));
        }

        [Test]
        public void Handle_Dispose_RemovesModifier()
        {
            var stack = new CameraModifierStack();
            var modifier = new OffsetModifier(new Vector3(5f, 0f, 0f));
            var handle = stack.AddModifier(modifier);

            var pose = BasePose;
            stack.Apply(ref pose, 0.016f);
            Assert.That(pose.Position.x, Is.EqualTo(5f));

            handle.Dispose();
            handle.Dispose();

            pose = BasePose;
            stack.Apply(ref pose, 0.016f);
            Assert.That(pose.Position, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Apply_DoesNotAccumulate_BasePoseReappliedEachFrame()
        {
            var stack = new CameraModifierStack();
            using (stack.AddModifier(new OffsetModifier(new Vector3(1f, 0f, 0f))))
            {
                var first = BasePose;
                var second = BasePose;

                stack.Apply(ref first, 0.016f);
                stack.Apply(ref second, 0.016f);

                Assert.That(first.Position, Is.EqualTo(second.Position));
                Assert.That(first.Position, Is.EqualTo(new Vector3(1f, 0f, 0f)));
            }
        }

        [Test]
        public void ShakeModifier_DecaysToZero_ThenSelfRemoves()
        {
            var stack = new CameraModifierStack();
            var modifier = new ShakeModifier(new Vector3(1f, 1f, 1f), duration: 0.05f);
            stack.AddModifier(modifier);

            var pose = BasePose;
            var appliedFrames = 0;
            var maxOffset = 0f;

            for (var i = 0; i < 20; i++)
            {
                pose = BasePose;
                stack.Apply(ref pose, 0.016f);
                maxOffset = Mathf.Max(maxOffset, pose.Position.magnitude);
                if (pose.Position.sqrMagnitude > 0f)
                {
                    appliedFrames++;
                }
            }

            Assert.That(appliedFrames, Is.GreaterThan(0));
            Assert.That(maxOffset, Is.GreaterThan(0f));

            pose = BasePose;
            stack.Apply(ref pose, 0.016f);
            Assert.That(pose.Position, Is.EqualTo(Vector3.zero));
        }

        private sealed class OffsetModifier : ICameraPoseModifier
        {
            private readonly Vector3 _offset;

            public OffsetModifier(Vector3 offset) => _offset = offset;

            public bool Apply(ref CameraPose pose, float deltaTime)
            {
                pose = pose.WithPosition(pose.Position + _offset);
                return true;
            }
        }

        private sealed class ScaleXModifier : ICameraPoseModifier
        {
            private readonly float _scale;

            public ScaleXModifier(float scale) => _scale = scale;

            public bool Apply(ref CameraPose pose, float deltaTime)
            {
                var position = pose.Position;
                position.x *= _scale;
                pose = pose.WithPosition(position);
                return true;
            }
        }

        private sealed class SingleUseModifier : ICameraPoseModifier
        {
            public int ApplyCount { get; private set; }

            public bool Apply(ref CameraPose pose, float deltaTime)
            {
                ApplyCount++;
                return false;
            }
        }

        private sealed class CountingModifier : ICameraPoseModifier
        {
            public int ApplyCount { get; private set; }

            public bool Apply(ref CameraPose pose, float deltaTime)
            {
                ApplyCount++;
                return true;
            }
        }
    }
}
