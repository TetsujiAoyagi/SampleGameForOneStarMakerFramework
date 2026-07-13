#nullable enable

using NUnit.Framework;
using OneStarMaker.Runtime.UpdateSystem.Hosting;

namespace OneStarMaker.Tests.UpdateSystem
{
    /// <summary>
    /// SceneDirector の状態を知らない bootstrap 初期区間で、通常 Element が更新を開始しないことを検証する。
    /// Application 常駐サービスは Coordinator を明示操作して例外的に起動するため、この gate の対象外である。
    /// </summary>
    [TestFixture]
    public sealed class UpdateSystemHostTests
    {
        [Test]
        public void TryConsumeActivationRequest_BeforeSceneDirectorBinding_ReturnsFalse()
        {
            var host = new UpdateSystemHost();
            try
            {
                Assert.That(host.TryConsumeActivationRequest(), Is.False);
            }
            finally
            {
                host.Dispose();
            }
        }
    }
}
