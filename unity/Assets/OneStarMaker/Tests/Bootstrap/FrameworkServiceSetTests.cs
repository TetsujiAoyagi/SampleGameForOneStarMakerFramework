#nullable enable

using NUnit.Framework;
using OneStarMaker.Runtime;

namespace OneStarMaker.Tests.Bootstrap
{
    /// <summary>
    /// 常駐サービスの集合は BeforeSceneLoad の途中で失敗すると一部だけ埋まったまま ReleaseAll に渡り、
    /// ReleaseAll は SubsystemRegistration・quitting・失敗時と複数回呼ばれる。未設定と二重の Dispose が安全であることを固定する。
    /// </summary>
    [TestFixture]
    public sealed class FrameworkServiceSetTests
    {
        [Test]
        public void Dispose_WithNothingSet_DoesNotThrow()
        {
            var services = new FrameworkServiceSet();

            Assert.DoesNotThrow(services.Dispose);
            Assert.That(services.LoggerFactory, Is.Null);
            Assert.That(services.DebugSocket, Is.Null);
            Assert.That(services.UpdateSystemHost, Is.Null);
            Assert.That(services.Vfx, Is.Null);
        }

        [Test]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            var services = new FrameworkServiceSet();
            services.Dispose();

            Assert.DoesNotThrow(services.Dispose);
        }
    }
}
