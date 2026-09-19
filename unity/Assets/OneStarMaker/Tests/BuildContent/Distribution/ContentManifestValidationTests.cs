#nullable enable
using NUnit.Framework;using OneStarMaker.Runtime.BuildContent.Distribution;
namespace OneStarMaker.Tests.BuildContent.Distribution
{
    public sealed class ContentManifestValidationTests
    {
        [Test]public void Validate_ExactCompatibility_AcceptsImmutableSnapshot(){var manifest=Valid();var request=new ContentInstallRequest(new string('a',64),"set","revision","StandaloneWindows64-Player","6000.6.0f1",2,100);var result=ContentManifestValidation.Validate(manifest,new string('a',64),request);manifest.files[0].path="changed";Assert.That(result.Files[0].path,Is.EqualTo("content.bin"));}
        [TestCase("../escape")][TestCase("dir\\file")][TestCase("CON")][TestCase("file.")][TestCase("a/b/../c")]public void Validate_UnsafePath_IsRejected(string path){var manifest=Valid();manifest.files[0].path=path;Assert.Throws<ContentDeliveryException>(()=>ContentManifestValidation.Validate(manifest,new string('a',64),new ContentInstallRequest(new string('a',64),"set","revision","StandaloneWindows64-Player","6000.6.0f1",2,100)));}
        private static ContentTransportManifest Valid()=>new(){version=1,product="OneStarMaker",contentSet="set",revision="revision",target="StandaloneWindows64-Player",unityVersion="6000.6.0f1",rootSchemaVersion=2,playerConfigSchemaVersion=1,files=new[]{new ContentTransportFile{path="content.bin",size=1,sha256=new string('b',64)}}};
    }
}
