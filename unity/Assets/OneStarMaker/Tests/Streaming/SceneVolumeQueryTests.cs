#nullable enable

using NUnit.Framework;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Tests.SceneSystem;
using OneStarMaker.Tests.SceneSystem.Helpers;
using OneStarMaker.Tests.SceneSystem.TestDoubles;
using UnityEngine;

namespace OneStarMaker.Tests.Streaming
{
    /// <summary>
    /// 移行 M-1: <see cref="ISceneVolumeQuery"/>（SceneDirector 実装）の 3 分岐。
    ///
    /// <para>
    /// false になる理由は「未登録 / 候補フラグ off / 体積が空」の 3 つで、
    /// 呼び出し側（Driver）は 1 件でも false なら起動時に落とす。
    /// M-3 で R-3 が同じフラグを読み始めるため、分岐を固定しておく。
    /// </para>
    /// </summary>
    [TestFixture]
    public class SceneVolumeQueryTests : SceneDirectorTestBase
    {
        private const string Identity = "TestScene";

        private static readonly Bounds SampleVolume =
            new(new Vector3(125f, 8.5f, 125f), new Vector3(245f, 23f, 245f));

        /// <summary>Map に 1 件だけ登録した Director を作り、その SceneResource を返す。</summary>
        private SceneResource SetupResource(Bounds volume, bool streamByDistance)
        {
            var resource = SceneTestHelper.CreateSceneResource(Identity);
            resource.Volume = volume;                     // internal set
            resource.StreamByDistance = streamByDistance; // internal set
            CreatedSOs.Add(resource);

            Map = SceneTestHelper.CreateSceneResourceMap(resource);
            CreatedSOs.Add(Map);

            Director = new TestableSceneDirector(Factory, UICommon, Map, AssetManagement);
            return resource;
        }

        [Test]
        public void TryGetSceneVolume_CandidateWithVolume_ReturnsVolume()
        {
            SetupResource(SampleVolume, streamByDistance: true);

            Assert.IsTrue(Director.TryGetSceneVolume(Identity, out var volume));
            Assert.AreEqual(SampleVolume.center, volume.center);
            Assert.AreEqual(SampleVolume.size, volume.size);
        }

        [Test]
        public void TryGetSceneVolume_UnknownIdentity_ReturnsFalse()
        {
            SetupResource(SampleVolume, streamByDistance: true);

            Assert.IsFalse(Director.TryGetSceneVolume("NotInMap", out var volume));
            Assert.AreEqual(default(Bounds), volume);
        }

        [Test]
        public void TryGetSceneVolume_NotStreamingCandidate_ReturnsFalse()
        {
            // 体積はあるが距離政策の候補ではないシーン（現状の Environment / Player）。
            SetupResource(SampleVolume, streamByDistance: false);

            Assert.IsFalse(Director.TryGetSceneVolume(Identity, out var volume));
            Assert.AreEqual(default(Bounds), volume);
        }

        [Test]
        public void TryGetSceneVolume_EmptyVolume_ReturnsFalse()
        {
            // フラグが立っていても体積が空なら候補にできない（§34 §5: 空 = 空間に属さない）。
            // 中心がどこにあっても寸法ゼロなら空。
            var empty = new Bounds(new Vector3(999f, 999f, 999f), Vector3.zero);
            SetupResource(empty, streamByDistance: true);

            Assert.IsFalse(Director.TryGetSceneVolume(Identity, out var volume));
            Assert.AreEqual(default(Bounds), volume);
        }
    }
}
