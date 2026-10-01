#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace OneStarMaker.Editor.TestObservation
{
    internal static class LoadedAssemblyObservation
    {
        internal static List<AssemblyObservation> Capture(int domain, string utc, long ticks, IEnumerable<string> testAssemblies)
        {
            var names = new HashSet<string>(testAssemblies, StringComparer.Ordinal);
            var result = new List<AssemblyObservation>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var shortName = assembly.GetName().Name ?? "";
                if (!shortName.StartsWith("OneStarMaker.", StringComparison.Ordinal) &&
                    !shortName.StartsWith("SampleGame.", StringComparison.Ordinal) &&
                    shortName != "UnityEditor.TestRunner" && shortName != "UnityEngine.TestRunner" &&
                    !names.Contains(shortName)) continue;
                var item = new AssemblyObservation { domainOrdinal = domain, observedAtUtc = utc, ticks = ticks,
                    fullName = assembly.FullName ?? "", status = "unavailable" };
                try
                {
                    // disk hash は取得時のファイル値。ロード済みメモリ bytes の同一性は主張しない。
                    item.location = assembly.Location;
                    if (assembly.IsDynamic || string.IsNullOrEmpty(item.location)) throw new IOException("動的または Location 空の assembly です");
                    item.loadedModuleVersionId = assembly.ManifestModule.ModuleVersionId.ToString("D");
                    using (var stream = File.OpenRead(item.location))
                    using (var sha = SHA256.Create()) item.diskSha256 = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                    item.status = "observed";
                }
                catch (Exception ex) { item.failure = ex.GetType().Name + ": " + ex.Message; }
                result.Add(item);
            }
            return result;
        }
    }
}
