#nullable enable
using System;using System.Collections.Generic;using System.Linq;
namespace OneStarMaker.Runtime.BuildContent.Distribution
{ internal static class ContentEvictionPolicy { internal static IReadOnlyList<ContentCacheEntry> Select(IReadOnlyList<ContentCacheEntry> entries,ISet<string> pins,long required)=>required<=0?Array.Empty<ContentCacheEntry>():entries.Where(x=>!pins.Contains(x.ContentSet+"\n"+x.Revision)).OrderBy(x=>x.InstalledUtc).ThenBy(x=>x.ContentSet,StringComparer.Ordinal).ThenBy(x=>x.Revision,StringComparer.Ordinal).ToArray(); } }
