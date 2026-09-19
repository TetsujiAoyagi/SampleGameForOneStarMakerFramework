#nullable enable
using System;using System.IO;using System.Linq;using System.Text;using OneStarMaker.Runtime.BuildContent.Distribution;using UnityEngine;
namespace OneStarMaker.Editor.Build.Content
{
    public static class ContentTransportPublisher
    {
        public static string Publish(BuildContentResult result,string publishRoot)
        {
            if(result==null||!result.IsSuccess)throw new InvalidOperationException("Only a successful content build can be published.");
            var preflight=JsonUtility.FromJson<Preflight>(File.ReadAllText(result.PreflightPath))??throw new InvalidOperationException("Preflight report is invalid.");
            if(preflight.identity!=result.Identity||preflight.target!=BuildContentCoordinator.TargetName)throw new InvalidOperationException("Build result and preflight identity do not match.");
            var final=Path.GetFullPath(publishRoot);var sourceRoot=Path.GetFullPath(result.ContentPath!);if(final.Equals(sourceRoot,StringComparison.OrdinalIgnoreCase)||final.StartsWith(sourceRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Publish destination must be outside the build content source.");var staging=final+"."+Guid.NewGuid().ToString("N")+".staging";if(Directory.Exists(final))throw new IOException("Publish destination already exists.");
            try{Directory.CreateDirectory(Path.Combine(staging,"content"));foreach(var source in Directory.GetFiles(result.ContentPath!,"*",SearchOption.AllDirectories)){var relative=Path.GetRelativePath(result.ContentPath!,source);var target=Path.Combine(staging,"content",relative);Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(source,target);}
              var files=Directory.GetFiles(Path.Combine(staging,"content"),"*",SearchOption.AllDirectories).Select(path=>new ContentTransportFile{path=Path.GetRelativePath(Path.Combine(staging,"content"),path).Replace('\\','/'),size=new FileInfo(path).Length,sha256=Sha256(path)}).OrderBy(x=>x.path,StringComparer.Ordinal).ToArray();
              var sourcePaths=preflight.closure.Select(x=>x.Contains('|')?x.Substring(x.LastIndexOf('|')+1):x).SelectMany(x=>File.Exists(x+".meta")?new[]{x,x+".meta"}:new[]{x});
              var sources=sourcePaths.Where(File.Exists).Distinct(StringComparer.Ordinal).Select(x=>new ContentSourceFile{path=x.Replace('\\','/'),sha256=Sha256(x)}).OrderBy(x=>x.path,StringComparer.Ordinal).ToArray();
              var manifest=new ContentTransportManifest{version=1,product="OneStarMaker",contentSet=preflight.contentSet,revision=result.Identity,target=preflight.target,unityVersion=Application.unityVersion,rootSchemaVersion=2,playerConfigSchemaVersion=1,files=files,sourceFiles=sources};
              File.WriteAllText(Path.Combine(staging,"transport.json"),JsonUtility.ToJson(manifest,true),new UTF8Encoding(false));Directory.Move(staging,final);return final;
            }finally{if(Directory.Exists(staging))Directory.Delete(staging,true);}
        }
        private static string Sha256(string path){using var stream=File.OpenRead(path);using var hash=System.Security.Cryptography.SHA256.Create();return string.Concat(hash.ComputeHash(stream).Select(x=>x.ToString("x2")));}
        [Serializable]private sealed class Preflight{public string identity="";public string target="";public string contentSet="";public string[] closure=Array.Empty<string>();}
    }
}
