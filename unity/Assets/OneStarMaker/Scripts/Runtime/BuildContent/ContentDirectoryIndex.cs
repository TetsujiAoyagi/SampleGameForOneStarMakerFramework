#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using OneStarMaker.Runtime.AssetManagement;
using Unity.Loading;

namespace OneStarMaker.Runtime.BuildContent
{
    // root の検証と logical 解決は I/O/公開例外を持たない。session がこの issue を
    // 公開失敗型へ変換するため、純粋な索引を Unity native backend なしで検証できる。
    internal readonly struct ContentDirectoryIssue
    {
        internal ContentDirectoryIssue(ContentDirectoryFailureCode code, string message,
            string? logicalKey = null, string? representation = null)
        { Code = code; Message = message; LogicalKey = logicalKey; Representation = representation; }

        internal ContentDirectoryFailureCode Code { get; }
        internal string Message { get; }
        internal string? LogicalKey { get; }
        internal string? Representation { get; }
    }

    internal sealed class ContentDirectoryIndex
    {
        private readonly Dictionary<string, BuildContentEntry> _entries;

        private ContentDirectoryIndex(Dictionary<string, BuildContentEntry> entries) => _entries = entries;

        internal static bool TryCreate(BuildContentRoot? root, string expectedIdentity, string expectedTarget,
            out ContentDirectoryIndex? index, out ContentDirectoryIssue issue)
        {
            index = null;
            if (root == null)
            { issue = new(ContentDirectoryFailureCode.InvalidRoot, "Content root is null."); return false; }
            if (root.SchemaVersion is not 1 and not 2)
            { issue = new(ContentDirectoryFailureCode.UnsupportedSchema, "Unsupported content root schema."); return false; }
            if (!string.Equals(root.BuildIdentity, expectedIdentity, StringComparison.Ordinal))
            { issue = new(ContentDirectoryFailureCode.IdentityMismatch, "Content root identity does not match the requested directory."); return false; }
            if (!string.Equals(root.Target, expectedTarget, StringComparison.Ordinal))
            { issue = new(ContentDirectoryFailureCode.TargetMismatch, "Content root target does not match the requested directory."); return false; }

            var entries = new Dictionary<string, BuildContentEntry>(StringComparer.Ordinal);
            var stable = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in root.Entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.LogicalKey) || string.IsNullOrWhiteSpace(entry.StableKey)
                    || (entry.Representation != null && entry.Representation.Length != 0 && string.IsNullOrWhiteSpace(entry.Representation)))
                { issue = new(ContentDirectoryFailureCode.InvalidRoot, "Content root contains an incomplete entry."); return false; }
                if (!stable.Add(entry.StableKey))
                { issue = new(ContentDirectoryFailureCode.InvalidRoot, "Content root contains a duplicate stable key."); return false; }
                if (!Enum.IsDefined(typeof(BuildContentKind), entry.Kind))
                { issue = new(ContentDirectoryFailureCode.InvalidRoot, "Content entry kind is invalid.", entry.LogicalKey, entry.Representation); return false; }
                if (entry.Kind == BuildContentKind.Scene && entry.SceneId.Equals(default(LoadableSceneId)))
                { issue = new(ContentDirectoryFailureCode.InvalidRoot, "Scene entry has no loadable scene locator.", entry.LogicalKey, entry.Representation); return false; }
                // Unity serializer は Scene の未使用 Loadable field を default wrapper として復元する。
                // 実際に有効な Object ID を含む場合だけ kind 不整合とする。
                if (entry.Kind == BuildContentKind.Scene
                    && ((entry.Object != null && !entry.Object.LoadableObjectId.Equals(default(LoadableObjectId)))
                        || !string.IsNullOrEmpty(entry.FileGuid) || entry.LocalFileId != 0))
                { issue = new(ContentDirectoryFailureCode.InvalidRoot, "Scene entry contains an object locator.", entry.LogicalKey, entry.Representation); return false; }
                if (entry.Kind == BuildContentKind.Object && entry.Object == null)
                { issue = new(ContentDirectoryFailureCode.InvalidRoot, "Object entry has no loadable locator.", entry.LogicalKey, entry.Representation); return false; }
                if (entry.Kind == BuildContentKind.Object && !entry.SceneId.Equals(default(LoadableSceneId)))
                { issue = new(ContentDirectoryFailureCode.InvalidRoot, "Object entry contains a scene locator.", entry.LogicalKey, entry.Representation); return false; }
                if (entry.Kind == BuildContentKind.Object && entry.Object != null
                    && entry.Object.LoadableObjectId.Equals(default(LoadableObjectId)))
                { issue = new(ContentDirectoryFailureCode.InvalidRoot, "Object entry has an invalid Unity loadable locator.", entry.LogicalKey, entry.Representation); return false; }
                if (root.SchemaVersion == 2 && entry.Kind == BuildContentKind.Object
                    && (!IsCanonicalGuid(entry.FileGuid) || entry.LocalFileId == 0))
                { issue = new(ContentDirectoryFailureCode.InvalidRoot, "Schema v2 object entry has no file/object locator.", entry.LogicalKey, entry.Representation); return false; }

                var key = Key(entry.LogicalKey, EffectiveRepresentation(entry.Representation), entry.Kind);
                if (!entries.TryAdd(key, entry))
                { issue = new(ContentDirectoryFailureCode.EntryAmbiguous, "Content root contains duplicate logical entries.", entry.LogicalKey, entry.Representation); return false; }
            }
            index = new ContentDirectoryIndex(entries);
            issue = default;
            return true;
        }

        internal bool TryResolveObject(string logicalKey, string representation, out BuildContentEntry? entry, out ContentDirectoryIssue issue)
            => TryResolve(logicalKey, representation, BuildContentKind.Object, false, out entry, out issue);

        internal bool TryResolveScene(string logicalKey, string representation, out BuildContentEntry? entry, out ContentDirectoryIssue issue)
            => TryResolve(logicalKey, representation, BuildContentKind.Scene, true, out entry, out issue);

        private bool TryResolve(string logicalKey, string representation, BuildContentKind kind, bool allowFullFallback,
            out BuildContentEntry? entry, out ContentDirectoryIssue issue)
        {
            if (_entries.TryGetValue(Key(logicalKey, representation, kind), out entry))
            { issue = default; return true; }
            if (allowFullFallback && !string.Equals(representation, "Full", StringComparison.Ordinal)
                && _entries.TryGetValue(Key(logicalKey, "Full", kind), out entry))
            { issue = default; return true; }
            entry = null;
            issue = new(ContentDirectoryFailureCode.EntryMissing, "No matching content entry is registered.", logicalKey, representation);
            return false;
        }

        internal static string CacheKey(string path, string identity, string target, BuildContentEntry entry)
            => string.Concat(CachePrefix(path, identity, target), Part(entry.LogicalKey), ":",
                Part(EffectiveRepresentation(entry.Representation)), ":", entry.Kind, ":",
                Part(entry.FileGuid), ":", entry.LocalFileId, ":",
                Part(entry.Object?.LoadableObjectId.ToString() ?? string.Empty));

        internal static string CachePrefix(string path, string identity, string target)
            => string.Concat("directory:", Part(path), ":", Part(identity), ":", Part(target), ":");

        private static bool IsCanonicalGuid(string value)
        {
            if (value == null || value.Length != 32) return false;
            foreach (var ch in value)
                if (!((ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f'))) return false;
            return true;
        }

        // 区切り文字を含む logical key でも別 revision の token と衝突させない。
        private static string Part(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));

        // 旧 build の未タグ entry は空文字。Full として索引化し、Object の厳密一致を維持する。
        private static string EffectiveRepresentation(string value) => string.IsNullOrEmpty(value) ? "Full" : value;

        private static string Key(string logicalKey, string representation, BuildContentKind kind)
            // 入力に区切り文字を含めても組キーが別組と衝突しない形式にする。
            => string.Concat(Part(logicalKey), ":", Part(representation), ":", kind);
    }
}
