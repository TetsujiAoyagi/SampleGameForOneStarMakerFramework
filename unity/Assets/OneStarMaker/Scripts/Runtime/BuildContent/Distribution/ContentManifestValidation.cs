#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OneStarMaker.Runtime.BuildContent.Distribution
{
    internal static class ContentManifestValidation
    {
        internal const int MaxManifestBytes=4*1024*1024, MaxFiles=100000;
        internal static ValidatedContentManifest Validate(ContentTransportManifest value, string digest, ContentInstallRequest request)
        {
            if (value.version!=1 || value.product!="OneStarMaker" || value.playerConfigSchemaVersion!=1)
                Fail(ContentDeliveryFailureCode.CompatibilityMismatch, "Unsupported transport protocol.");
            if (!Segment(value.contentSet) || !Segment(value.revision)) Fail(ContentDeliveryFailureCode.InvalidManifest, "Content set or revision is invalid.");
            if (value.contentSet!=request.ContentSet || value.revision!=request.Revision) Fail(ContentDeliveryFailureCode.IdentityMismatch, "Requested content identity does not match the manifest.");
            if (value.target!=request.Target) Fail(ContentDeliveryFailureCode.TargetMismatch, "Requested target does not match the manifest.");
            if (value.unityVersion!=request.UnityVersion || value.rootSchemaVersion!=request.RootSchemaVersion)
                Fail(ContentDeliveryFailureCode.CompatibilityMismatch, "Manifest compatibility does not match this consumer.");
            if (!Hash(digest) || digest!=request.ManifestSha256) Fail(ContentDeliveryFailureCode.IntegrityMismatch, "Manifest digest does not match the requested bytes.");
            if (value.files==null || value.files.Length==0 || value.files.Length>MaxFiles || value.sourceFiles==null || value.sourceFiles.Length>MaxFiles)
                Fail(ContentDeliveryFailureCode.InvalidManifest, "Manifest file count is invalid.");
            var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase); long total=0;
            foreach(var file in value.files)
            {
                if(file==null || !Relative(file.path) || file.size<0 || !Hash(file.sha256) || !paths.Add(file.path)) Fail(ContentDeliveryFailureCode.InvalidManifest,"Manifest file entry is invalid.");
                try { total=checked(total+file.size); } catch(OverflowException){ Fail(ContentDeliveryFailureCode.InvalidManifest,"Manifest byte total overflowed."); }
            }
            var ordered=paths.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToArray();
            for(var i=0;i<ordered.Length;i++) for(var j=i+1;j<ordered.Length;j++)
                if(ordered[j].StartsWith(ordered[i]+"/",StringComparison.OrdinalIgnoreCase)) Fail(ContentDeliveryFailureCode.InvalidManifest,"Manifest contains a file/directory collision.");
            foreach(var file in value.sourceFiles)
                if(file==null || !(file.path.StartsWith("Assets/",StringComparison.Ordinal)||file.path.StartsWith("Packages/",StringComparison.Ordinal)) || !Relative(file.path) || !Hash(file.sha256))
                    Fail(ContentDeliveryFailureCode.InvalidManifest,"Source file entry is invalid.");
            if(request.DiskBudgetBytes<=0 || total>request.DiskBudgetBytes) throw new ContentDeliveryException(ContentDeliveryFailureCode.BudgetUnsatisfied,"Manifest exceeds the disk budget.",requiredBytes:total,availableBytes:request.DiskBudgetBytes);
            return new ValidatedContentManifest(digest,value,(ContentTransportFile[])value.files.Clone(),total);
        }
        internal static bool Relative(string path)
        {
            if(string.IsNullOrWhiteSpace(path)||path.Contains('\\')||path.StartsWith("/",StringComparison.Ordinal)||path.Contains(":")) return false;
            var parts=path.Split('/');
            foreach(var p in parts)
            { if(p.Length==0||p=="."||p==".."||p.EndsWith(".",StringComparison.Ordinal)||p.EndsWith(" ",StringComparison.Ordinal)||Reserved(p)) return false; }
            return !Path.IsPathRooted(path) && Uri.UnescapeDataString(path)==path;
        }
        private static bool Reserved(string value)
        { var stem=value.Split('.')[0]; return new[]{"CON","PRN","AUX","NUL","COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9","LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"}.Contains(stem,StringComparer.OrdinalIgnoreCase); }
        internal static bool Hash(string value)=>value!=null&&value.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');
        private static bool Segment(string value)=>!string.IsNullOrEmpty(value)&&value.Length<=128&&value.All(c=>char.IsLetterOrDigit(c)||c is '-' or '_' or '.');
        private static void Fail(ContentDeliveryFailureCode code,string message)=>throw new ContentDeliveryException(code,message);
    }
}
