#nullable enable

using NUnit.Framework;
using OneStarMaker.Runtime.BuildContent.Distribution;
using UnityEngine;

namespace OneStarMaker.Tests.BuildContent.Distribution
{
    public sealed class ContentManifestValidationTests
    {
        [Test]
        public void Validate_ExactCompatibility_AcceptsImmutableSnapshot()
        {
            var manifest = Valid();
            var result = ContentManifestValidation.Validate(manifest, new string('a', 64), Request());
            manifest.files[0].path = "changed";
            Assert.That(result.Files[0].path, Is.EqualTo("content.bin"));
        }

        [TestCase("../escape")]
        [TestCase("dir\\file")]
        [TestCase("CON")]
        [TestCase("file.")]
        [TestCase("a/b/../c")]
        [TestCase("a%2fb")]
        [TestCase("a:stream")]
        public void Validate_UnsafePath_IsRejected(string path)
        {
            var manifest = Valid();
            manifest.files[0].path = path;
            Assert.Throws<ContentDeliveryException>(() => ContentManifestValidation.Validate(manifest, new string('a', 64), Request()));
        }

        [TestCase(".")]
        [TestCase("..")]
        [TestCase("set.")]
        [TestCase("CON")]
        [TestCase("日本語")]
        public void Validate_UnsafeIdentitySegment_IsRejected(string value)
        {
            var manifest = Valid();
            manifest.contentSet = value;
            Assert.Throws<ContentDeliveryException>(() => ContentManifestValidation.Validate(manifest, new string('a', 64), Request(value)));
        }

        [Test]
        public void RequiredFields_NestedFieldCannotSupplyMissingRootField()
        {
            var json = JsonUtility.ToJson(Valid()).Replace("\"version\":1,", "\"extra\":{\"version\":1},");
            Assert.Throws<ContentDeliveryException>(() => ContentManifestValidation.ValidateRequiredFields(json));
        }

        [Test]
        public void RequiredFields_MissingSizeDoesNotBecomeZeroByteFile()
        {
            var json = JsonUtility.ToJson(Valid()).Replace("\"size\":1,", "");
            Assert.Throws<ContentDeliveryException>(() => ContentManifestValidation.ValidateRequiredFields(json));
        }

        [Test]
        public void RequiredFields_DuplicateAndWrongTypeAreRejected()
        {
            var json = JsonUtility.ToJson(Valid());
            Assert.Throws<ContentDeliveryException>(() => ContentManifestValidation.ValidateRequiredFields(
                json.Replace("\"version\":1", "\"version\":1,\"version\":1")));
            Assert.Throws<ContentDeliveryException>(() => ContentManifestValidation.ValidateRequiredFields(
                json.Replace("\"size\":1", "\"size\":\"1\"")));
        }

        [Test]
        public void RequiredFields_UnknownOptionalStructureAndZeroSizeAreAllowed()
        {
            var manifest = Valid();
            manifest.files[0].size = 0;
            var json = JsonUtility.ToJson(manifest);
            json = "{\"optional\":{\"text\":\"braces } ] and quote \\\"\",\"array\":[null,true,1.5]}," + json.Substring(1);
            Assert.DoesNotThrow(() => ContentManifestValidation.ValidateRequiredFields(json));
        }

        private static ContentInstallRequest Request(string set = "set")
            => new(new string('a', 64), set, "revision", "StandaloneWindows64-Player", "6000.6.0f1", 2, 100);

        private static ContentTransportManifest Valid() => new()
        {
            version = 1,
            product = "OneStarMaker",
            contentSet = "set",
            revision = "revision",
            target = "StandaloneWindows64-Player",
            unityVersion = "6000.6.0f1",
            rootSchemaVersion = 2,
            playerConfigSchemaVersion = 1,
            files = new[] { new ContentTransportFile { path = "content.bin", size = 1, sha256 = new string('b', 64) } },
        };
    }
}
