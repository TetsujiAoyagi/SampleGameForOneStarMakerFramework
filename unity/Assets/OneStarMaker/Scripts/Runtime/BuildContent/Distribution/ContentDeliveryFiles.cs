#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace OneStarMaker.Runtime.BuildContent.Distribution
{
    [Serializable] internal sealed class ContentInstallReceipt
    { public int version=1; public string manifestSha256=""; public string contentSet=""; public string revision=""; public string target=""; public string installedUtc=""; }

    internal static class ContentDeliveryFiles
    {
        internal static string Sha256(byte[] bytes) { using var hash=SHA256.Create(); return Hex(hash.ComputeHash(bytes)); }
        internal static string Sha256(string path) { using var stream=File.OpenRead(path); using var hash=SHA256.Create(); return Hex(hash.ComputeHash(stream)); }
        private static string Hex(byte[] bytes)=>string.Concat(bytes.Select(x=>x.ToString("x2")));
        internal static string Resolve(string root,string relative)
        {
            if(!ContentManifestValidation.Relative(relative)) throw new ContentDeliveryException(ContentDeliveryFailureCode.InvalidManifest,"Invalid relative path.");
            var normalized=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            var value=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar)));
            if(!value.StartsWith(normalized,StringComparison.OrdinalIgnoreCase)) throw new ContentDeliveryException(ContentDeliveryFailureCode.InvalidManifest,"Path escapes its root.");
            return value;
        }
        internal static void RejectReparseAncestors(string root,string path)
        {
            var current=new DirectoryInfo(Path.GetFullPath(path)); var boundary=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            while(current!=null && current.FullName.StartsWith(boundary,StringComparison.OrdinalIgnoreCase))
            { if(current.Exists && (current.Attributes&FileAttributes.ReparsePoint)!=0) throw new ContentDeliveryException(ContentDeliveryFailureCode.InvalidManifest,"Managed content cannot traverse a reparse point."); current=current.Parent; }
        }
        internal static void WriteJson<T>(string path,T value)
        { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path,JsonUtility.ToJson(value,true),new UTF8Encoding(false)); }
        internal static T ReadJson<T>(string path) where T:class
        { try { return JsonUtility.FromJson<T>(File.ReadAllText(path,Encoding.UTF8)) ?? throw new FormatException(); }
          catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or FormatException or ArgumentException)
          { throw new ContentDeliveryException(ContentDeliveryFailureCode.InvalidManifest,"JSON metadata is invalid.",ex); } }
        internal static void VerifyTree(string contentRoot,ValidatedContentManifest manifest)
        {
            RejectReparseAncestors(contentRoot,contentRoot);
            foreach(var directory in Directory.GetDirectories(contentRoot,"*",SearchOption.AllDirectories))
                if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)
                    throw new ContentDeliveryException(ContentDeliveryFailureCode.IntegrityMismatch,"Installed content contains a reparse-point directory.");
            foreach(var file in Directory.GetFiles(contentRoot,"*",SearchOption.AllDirectories))
                if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)
                    throw new ContentDeliveryException(ContentDeliveryFailureCode.IntegrityMismatch,"Installed content contains a reparse-point file.");
            var expected=manifest.Files.Select(x=>x.path).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToArray();
            var actual=Directory.Exists(contentRoot)?Directory.GetFiles(contentRoot,"*",SearchOption.AllDirectories).Select(x=>Path.GetRelativePath(contentRoot,x).Replace('\\','/')).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToArray():Array.Empty<string>();
            if(!expected.SequenceEqual(actual,StringComparer.OrdinalIgnoreCase)) throw new ContentDeliveryException(ContentDeliveryFailureCode.IntegrityMismatch,"Installed file set differs from the manifest.");
            foreach(var entry in manifest.Files)
            { var path=Resolve(contentRoot,entry.path); var info=new FileInfo(path); if(!info.Exists) throw new ContentDeliveryException(ContentDeliveryFailureCode.MissingFile,"Manifest file is missing.");
              if(info.Length!=entry.size || Sha256(path)!=entry.sha256) throw new ContentDeliveryException(ContentDeliveryFailureCode.IntegrityMismatch,"Installed file failed size or digest validation."); }
        }
    }
}
