#nullable enable

using NUnit.Framework;
using OneStarMaker.Foundation.DebugSocket;

namespace OneStarMaker.Tests.Foundation
{
    /// <summary>
    /// Capture 前でも属性読取が安全であることを固定する。
    /// </summary>
    [TestFixture]
    public sealed class UnitySessionAttributesTests
    {
        [SetUp]
        public void SetUp()
        {
            UnitySessionAttributes.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            UnitySessionAttributes.ResetForTests();
        }

        [Test]
        public void Capture前に読んでも例外にならず空文字が返る()
        {
            Assert.AreEqual(string.Empty, UnitySessionAttributes.ProductName);
            Assert.AreEqual(string.Empty, UnitySessionAttributes.BuildVersion);
            Assert.AreEqual(string.Empty, UnitySessionAttributes.Platform);
            Assert.AreEqual(string.Empty, UnitySessionAttributes.DeviceModel);
            Assert.AreEqual(string.Empty, UnitySessionAttributes.OsVersion);
            Assert.AreEqual(string.Empty, UnitySessionAttributes.UnityVersion);
        }
    }
}
