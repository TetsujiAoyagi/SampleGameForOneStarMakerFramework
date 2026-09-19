#nullable enable
using System;using System.IO;using NUnit.Framework;using OneStarMaker.Runtime.BuildContent;using OneStarMaker.Runtime.BuildContent.Distribution;
namespace OneStarMaker.Tests.BuildContent.Distribution
{
    public sealed class ContentDeliveryFailureMatrixTests
    {
        [TestCase("set","other","StandaloneWindows64-Player","6000.6.0f1",ContentDeliveryFailureCode.IdentityMismatch)]
        [TestCase("set","revision","OtherTarget","6000.6.0f1",ContentDeliveryFailureCode.TargetMismatch)]
        [TestCase("set","revision","StandaloneWindows64-Player","other",ContentDeliveryFailureCode.CompatibilityMismatch)]
        public void ManifestMismatch_ReturnsStructuredFailure(string set,string revision,string target,string unity,ContentDeliveryFailureCode expected)
        {var value=Manifest();var request=new ContentInstallRequest(Hash,set,revision,target,unity,2,100);var ex=Assert.Throws<ContentDeliveryException>(()=>ContentManifestValidation.Validate(value,Hash,request));Assert.That(ex!.Code,Is.EqualTo(expected));}

        [Test]public void CacheTransaction_ConcurrentInspectorReturnsBusy()
        {var root=Temp();try{var store=new ContentCacheStore(root);using(store.AcquireTransaction()){var ex=Assert.Throws<ContentDeliveryException>(()=>store.Inspect());Assert.That(ex!.Code,Is.EqualTo(ContentDeliveryFailureCode.Busy));}}finally{Directory.Delete(root,true);}}

        [Test]public void CorruptReceipt_StopsEvictionInsteadOfBecomingADeleteCandidate()
        {var root=Temp();try{var revision=Path.Combine(root,"installed","set","revision");Directory.CreateDirectory(revision);File.WriteAllText(Path.Combine(revision,"receipt.json"),"{");var ex=Assert.Throws<ContentDeliveryException>(()=>new ContentCacheStore(root).Evict(0));Assert.That(ex!.Code,Is.EqualTo(ContentDeliveryFailureCode.InstallConflict));}finally{Directory.Delete(root,true);}}

        [Test]public void ReadLease_BlocksDeleteAndReleaseAllowsRetry()
        {var path=Path.Combine(Temp(),"content");Directory.CreateDirectory(path);try{using(ContentRevisionGate.AcquireRead("revision","StandaloneWindows64-Player",path)){Assert.That(ContentRevisionGate.TryAcquireDelete("revision","StandaloneWindows64-Player",path,out _,out var rejection),Is.False);Assert.That(rejection,Is.EqualTo(ContentDirectoryFailureCode.RevisionBusy));}Assert.That(ContentRevisionGate.TryAcquireDelete("revision","StandaloneWindows64-Player",path,out var lease,out _),Is.True);lease!.Dispose();}finally{Directory.Delete(Directory.GetParent(path)!.FullName,true);}}

        [Test]public void DeleteFailure_LeavesOwnedTombstoneAndNextEvictionRetriesCleanup()
        {var root=Temp();try{var revision=Path.Combine(root,"installed","set","revision");Directory.CreateDirectory(Path.Combine(revision,"content"));File.WriteAllText(Path.Combine(revision,"content","held.bin"),"bytes");ContentDeliveryFiles.WriteJson(Path.Combine(revision,"receipt.json"),new ContentInstallReceipt{contentSet="set",revision="revision",target="StandaloneWindows64-Player",manifestSha256=Hash,installedUtc=DateTime.UtcNow.ToString("O")});var store=new ContentCacheStore(root);using(var held=new FileStream(Path.Combine(revision,"content","held.bin"),FileMode.Open,FileAccess.Read,FileShare.Read)){var failure=Assert.Throws<ContentDeliveryException>(()=>store.Evict(0));Assert.That(failure!.Code,Is.EqualTo(ContentDeliveryFailureCode.BudgetUnsatisfied));}var retry=store.Evict(0);Assert.That(retry,Has.Some.Property("Status").EqualTo(ContentDeleteStatus.Deleted));Assert.That(Directory.GetDirectories(Path.Combine(root,"tombstone")),Is.Empty);}finally{if(Directory.Exists(root))Directory.Delete(root,true);}}

        [Test]public void VerificationToReservationGap_DeleteCanWinButNativeRegistrationDoesNotStart()
        {var root=Temp();try{var content=Path.Combine(root,"content");Directory.CreateDirectory(content);var payload=new byte[]{1};File.WriteAllBytes(Path.Combine(content,"file"),payload);var manifest=Manifest();manifest.files[0].sha256=ContentDeliveryFiles.Sha256(payload);var bytes=System.Text.Encoding.UTF8.GetBytes(UnityEngine.JsonUtility.ToJson(manifest));var digest=ContentDeliveryFiles.Sha256(bytes);File.WriteAllBytes(Path.Combine(root,"transport.json"),bytes);ContentDeliveryFiles.WriteJson(Path.Combine(root,"receipt.json"),new ContentInstallReceipt{contentSet="set",revision="revision",target="StandaloneWindows64-Player",manifestSha256=digest,installedUtc=DateTime.UtcNow.ToString("O")});var verified=InstalledRevisionVerifier.Verify(root,digest,"set","revision","StandaloneWindows64-Player");Assert.That(ContentRevisionGate.TryAcquireDelete("revision","StandaloneWindows64-Player",content,out var deletion,out _),Is.True);Directory.Delete(root,true);deletion!.Dispose();Assert.Throws<ContentDeliveryException>(()=>ContentDirectorySession.RegisterVerified(verified,null!));}finally{if(Directory.Exists(root))Directory.Delete(root,true);}}

        private const string Hash="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private static string Temp(){var path=Path.Combine(Path.GetTempPath(),"osm-dist-matrix",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);return path;}
        private static ContentTransportManifest Manifest()=>new(){version=1,product="OneStarMaker",contentSet="set",revision="revision",target="StandaloneWindows64-Player",unityVersion="6000.6.0f1",rootSchemaVersion=2,playerConfigSchemaVersion=1,files=new[]{new ContentTransportFile{path="file",size=1,sha256=Hash}}};
    }
}
