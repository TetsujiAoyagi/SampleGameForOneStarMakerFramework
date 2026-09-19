#nullable enable
using System;using System.IO;using System.Text;using UnityEngine;
namespace OneStarMaker.Runtime.BuildContent.Distribution
{
    internal sealed class VerifiedInstalledRevision
    { internal VerifiedInstalledRevision(string root,string content,string identity,string set,string target,string digest){RevisionRoot=root;ContentPath=content;Identity=identity;ContentSet=set;Target=target;ManifestSha256=digest;} internal string RevisionRoot{get;}internal string ContentPath{get;}internal string Identity{get;}internal string ContentSet{get;}internal string Target{get;}internal string ManifestSha256{get;} }
    internal static class InstalledRevisionVerifier
    {
        internal static VerifiedInstalledRevision Verify(string revisionRoot,string expectedDigest,string expectedSet,string expectedRevision,string expectedTarget)
        {
            var root=Path.GetFullPath(revisionRoot);var content=Path.Combine(root,"content");
            using(ContentRevisionGate.AcquireRead(expectedRevision,expectedTarget,content)) return VerifyInsideLease(root,expectedDigest,expectedSet,expectedRevision,expectedTarget);
        }
        internal static VerifiedInstalledRevision VerifyInsideLease(string root,string expectedDigest,string expectedSet,string expectedRevision,string expectedTarget)
        {
            var receipt=ContentDeliveryFiles.ReadJson<ContentInstallReceipt>(Path.Combine(root,"receipt.json"));var manifestPath=Path.Combine(root,"transport.json");var bytes=File.ReadAllBytes(manifestPath);var digest=ContentDeliveryFiles.Sha256(bytes);
            if(receipt.version!=1||receipt.manifestSha256!=expectedDigest||digest!=expectedDigest||receipt.contentSet!=expectedSet||receipt.revision!=expectedRevision||receipt.target!=expectedTarget)throw new ContentDeliveryException(ContentDeliveryFailureCode.IntegrityMismatch,"Installed receipt does not match the requested revision.");
            var dto=JsonUtility.FromJson<ContentTransportManifest>(Encoding.UTF8.GetString(bytes))??throw new ContentDeliveryException(ContentDeliveryFailureCode.InvalidManifest,"Installed manifest is invalid.");
            var validated=ContentManifestValidation.Validate(dto,digest,new ContentInstallRequest(digest,expectedSet,expectedRevision,expectedTarget,dto.unityVersion,dto.rootSchemaVersion,long.MaxValue));ContentDeliveryFiles.VerifyTree(Path.Combine(root,"content"),validated);
            return new VerifiedInstalledRevision(root,Path.Combine(root,"content"),expectedRevision,expectedSet,expectedTarget,digest);
        }
        internal static bool LooksManaged(string contentPath)
        { var parent=Directory.GetParent(Path.GetFullPath(contentPath));return parent!=null&&(File.Exists(Path.Combine(parent.FullName,"receipt.json"))||File.Exists(Path.Combine(parent.FullName,"transport.json"))); }
    }
}
