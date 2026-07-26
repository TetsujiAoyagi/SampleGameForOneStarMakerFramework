#nullable enable

using NUnit.Framework;
using SampleGame.InGame.Streaming;
using SampleGame.InGame.World;
using UnityEngine;

namespace OneStarMaker.Tests.Streaming
{
    /// <summary>
    /// Cell 子シーン萌芽の純関数テスト。
    /// 「Cell Add だけでは子が載らない」「Stable 後に明示 Add」の判定契約を固定する。
    /// </summary>
    [TestFixture]
    public sealed class CellChildLoadRulesTests
    {
        [Test]
        public void ShouldAddChild_OnlyWhenCellStableAndChildMissing()
        {
            Assert.That(
                CellChildLoadRules.ShouldAddChild(
                    cellIsStable: true,
                    childExistsInMap: true,
                    childIsLoaded: false),
                Is.True,
                "Cell Stable かつ子未ロードなら明示 Add する");

            Assert.That(
                CellChildLoadRules.ShouldAddChild(
                    cellIsStable: false,
                    childExistsInMap: true,
                    childIsLoaded: false),
                Is.False,
                "Cell 未 Stable のあいだは Add しない（引っ張られない）");

            Assert.That(
                CellChildLoadRules.ShouldAddChild(
                    cellIsStable: true,
                    childExistsInMap: false,
                    childIsLoaded: false),
                Is.False,
                "萌芽のない葉 Cell には Add しない");

            Assert.That(
                CellChildLoadRules.ShouldAddChild(
                    cellIsStable: true,
                    childExistsInMap: true,
                    childIsLoaded: true),
                Is.False,
                "既に載っている子を二重 Add しない");
        }

        [Test]
        public void EnvironmentIdentity_FormatAndParse_RoundTrip()
        {
            var id = EnvironmentIdentity.Format(3, 2);
            Assert.That(id, Is.EqualTo("Environment_3_2"));
            Assert.That(EnvironmentIdentity.IsEnvironmentId(id), Is.True);
            Assert.That(EnvironmentIdentity.TryParse(id, out var coordinate), Is.True);
            Assert.That(coordinate, Is.EqualTo(new Vector2Int(3, 2)));
        }

        [Test]
        public void EnvironmentIdentity_TryFromCellId_MapsParentCell()
        {
            Assert.That(EnvironmentIdentity.TryFromCellId("Cell_1_0", out var envId), Is.True);
            Assert.That(envId, Is.EqualTo("Environment_1_0"));
            Assert.That(EnvironmentIdentity.TryFromCellId("World", out _), Is.False);
            Assert.That(EnvironmentIdentity.TryFromCellId("Environment_1_0", out _), Is.False);
        }

        [Test]
        public void EnvironmentIdentity_DoesNotCollideWithCellIdentity()
        {
            Assert.That(OneStarMaker.Runtime.SceneSystem.CellIdentity.IsCellId("Environment_0_0"), Is.False);
            Assert.That(EnvironmentIdentity.IsEnvironmentId("Cell_0_0"), Is.False);
        }
    }
}
