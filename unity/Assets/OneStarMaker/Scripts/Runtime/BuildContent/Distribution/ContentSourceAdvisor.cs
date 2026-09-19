#nullable enable
using System;using System.Collections.Generic;using System.IO;using System.Linq;
namespace OneStarMaker.Runtime.BuildContent.Distribution
{
    public enum ContentSourceStatus{Complete,Missing,Changed}
    public sealed class ContentSourceAdvice{internal ContentSourceAdvice(ContentSourceStatus status,IReadOnlyList<string> paths){Status=status;Paths=paths;}public ContentSourceStatus Status{get;}public IReadOnlyList<string> Paths{get;}}
    public static class ContentSourceAdvisor
    {
        public static ContentSourceAdvice Inspect(string projectRoot,IEnumerable<ContentSourceFile> files)
        {var missing=new List<string>();var changed=new List<string>();foreach(var file in files){var path=ContentDeliveryFiles.Resolve(projectRoot,file.path);if(!File.Exists(path))missing.Add(file.path);else if(ContentDeliveryFiles.Sha256(path)!=file.sha256)changed.Add(file.path);}return missing.Count>0?new ContentSourceAdvice(ContentSourceStatus.Missing,missing):changed.Count>0?new ContentSourceAdvice(ContentSourceStatus.Changed,changed):new ContentSourceAdvice(ContentSourceStatus.Complete,Array.Empty<string>());}
    }
}
