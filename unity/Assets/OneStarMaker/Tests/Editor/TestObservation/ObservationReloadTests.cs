#nullable enable
using System.Collections;
using NUnit.Framework;
using OneStarMaker.Editor.TestObservation;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.Editor.TestObservation
{
    public sealed class ObservationReloadTests
    {
        [UnityTest]
        public IEnumerator RealDomainReloadRoundTrip()
        {
            if (TestObservationBootstrap.IsActive) TestObservationBootstrap.PrepareReloadSettings();
            try
            {
                yield return new EnterPlayMode();
                yield return new ExitPlayMode();
            }
            finally
            {
                // fixture が再開しなくても quitting が private recovery record から戻す。
                if (TestObservationBootstrap.IsActive) TestObservationBootstrap.RestoreReloadSettings();
            }
            Assert.Pass();
        }
    }

    public sealed class ObservationFaultFixture
    {
        [Test]
        public void IntentionalMissingCallback()
        {
            // observer の明示 filter によって started だけを落とす。テスト XML は Passed のままになる。
            Assert.Pass();
        }
    }
}
