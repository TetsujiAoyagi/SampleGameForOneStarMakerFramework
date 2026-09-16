#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OneStarMaker.Editor.Build.Materialization
{
    internal sealed class AssetDependencySnapshotBuilder
    {
        private readonly IAssetDatabaseGateway _gateway;
        public AssetDependencySnapshotBuilder(IAssetDatabaseGateway gateway) => _gateway = gateway;

        public BuildDependencySnapshot? Build(string rootGuid, string subjectKey, ICollection<BuildMaterializationIssue> issues)
        {
            var rootPath = Normalize(_gateway.GuidToPath(rootGuid));
            if (string.IsNullOrEmpty(rootPath))
            {
                Add(issues, BuildMaterializationIssueCode.UnresolvedRootGuid, subjectKey, rootGuid);
                return null;
            }
            if (!string.Equals(NormalizeGuid(_gateway.PathToGuid(rootPath)), rootGuid, StringComparison.Ordinal))
            {
                Add(issues, BuildMaterializationIssueCode.RootGuidRoundTripMismatch, subjectKey, rootGuid, rootPath);
                return null;
            }
            if (!IsContent(rootPath) || _gateway.IsFolder(rootPath))
            {
                Add(issues, BuildMaterializationIssueCode.InvalidDependencyPath, subjectKey, rootGuid, rootPath);
                return null;
            }
            if (!_gateway.FileExists(rootPath))
            {
                Add(issues, BuildMaterializationIssueCode.MissingRootAsset, subjectKey, rootGuid, rootPath);
                return null;
            }

            var paths = new HashSet<string>(StringComparer.Ordinal) { rootPath };
            foreach (var raw in _gateway.GetDependencies(rootPath) ?? Array.Empty<string>())
            {
                var path = Normalize(raw);
                if (IsContent(path) && !_gateway.IsFolder(path)) paths.Add(path);
            }

            var entries = new List<BuildDependencyEntry>();
            var guidPaths = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var path in paths.OrderBy(x => x, StringComparer.Ordinal))
            {
                if (!_gateway.FileExists(path))
                {
                    Add(issues, BuildMaterializationIssueCode.MissingDependencyAsset, subjectKey, rootGuid, path);
                    continue;
                }
                var guid = NormalizeGuid(_gateway.PathToGuid(path));
                if (guid == null)
                {
                    Add(issues, BuildMaterializationIssueCode.UnresolvedDependencyGuid, subjectKey, rootGuid, path);
                    continue;
                }
                if (guidPaths.TryGetValue(guid, out var previous) && !string.Equals(previous, path, StringComparison.Ordinal))
                {
                    issues.Add(new BuildMaterializationIssue(BuildMaterializationIssueCode.DependencyIdentityCollision,
                        BuildMaterializationSubject.Dependency, subjectKey, rootGuid, path, previous));
                    continue;
                }
                guidPaths[guid] = path;
                entries.Add(new BuildDependencyEntry(guid, path));
            }
            return new BuildDependencySnapshot(rootGuid, rootPath, entries);
        }

        internal static string Normalize(string? path) => (path ?? string.Empty).Replace('\\', '/');
        internal static string? NormalizeGuid(string? guid)
        {
            if (guid == null || guid.Length != 32 || guid.Any(c => !Uri.IsHexDigit(c))) return null;
            return guid.ToLowerInvariant();
        }
        internal static bool IsContent(string path)
        {
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) return false;
            var extension = Path.GetExtension(path);
            return !extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".dll", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".asmdef", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".asmref", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(path, "Resources/unity_builtin_extra", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(path, "Library/unity default resources", StringComparison.OrdinalIgnoreCase);
        }
        private static void Add(ICollection<BuildMaterializationIssue> issues, BuildMaterializationIssueCode code,
            string subjectKey, string? guid = null, string? path = null) => issues.Add(new BuildMaterializationIssue(
                code, BuildMaterializationSubject.Dependency, subjectKey, guid, path));
    }
}
