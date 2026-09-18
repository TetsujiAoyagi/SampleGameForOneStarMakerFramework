#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using OneStarMaker.Runtime.AssetManagement;

namespace OneStarMaker.Runtime.BuildContent
{
    /// <summary>登録済み root を I/O を伴わない解決表へ投影する。root の検証は登録前に完了する。</summary>
    internal sealed class ContentDirectoryIndex
    {
        private readonly Dictionary<string, BuildContentEntry> _entries;

        private ContentDirectoryIndex(Dictionary<string, BuildContentEntry> entries) => _entries = entries;

        internal static ContentDirectoryIndex Create(BuildContentRoot root, string expectedIdentity, string expectedTarget)
        {
            if (root == null) throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidRoot, "Content root is null.");
            if (root.SchemaVersion is not 1 and not 2)
                throw new ContentDirectoryException(ContentDirectoryFailureCode.UnsupportedSchema, "Unsupported content root schema.", expectedIdentity, expectedTarget);
            if (!string.Equals(root.BuildIdentity, expectedIdentity, StringComparison.Ordinal))
                throw new ContentDirectoryException(ContentDirectoryFailureCode.IdentityMismatch, "Content root identity does not match the requested directory.", expectedIdentity, expectedTarget);
            if (!string.Equals(root.Target, expectedTarget, StringComparison.Ordinal))
                throw new ContentDirectoryException(ContentDirectoryFailureCode.TargetMismatch, "Content root target does not match the requested directory.", expectedIdentity, expectedTarget);

            var entries = new Dictionary<string, BuildContentEntry>(StringComparer.Ordinal);
            var stable = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in root.Entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.LogicalKey) || string.IsNullOrWhiteSpace(entry.StableKey)
                    || (entry.Representation != null && entry.Representation.Length != 0 && string.IsNullOrWhiteSpace(entry.Representation)))
                    throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidRoot, "Content root contains an incomplete entry.", expectedIdentity, expectedTarget);
                if (!stable.Add(entry.StableKey))
                    throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidRoot, "Content root contains a duplicate stable key.", expectedIdentity, expectedTarget);
                if (!Enum.IsDefined(typeof(BuildContentKind), entry.Kind))
                    throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidRoot, "Content entry kind is invalid.", expectedIdentity, expectedTarget, entry.LogicalKey, entry.Representation);
                if (entry.Kind == BuildContentKind.Scene && entry.SceneId.Equals(default(Unity.Loading.LoadableSceneId)))
                    throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidRoot, "Scene entry has no loadable scene locator.", expectedIdentity, expectedTarget, entry.LogicalKey, entry.Representation);
                // Unity serializer は Scene entry の未使用 Loadable field を default wrapper として
                // 復元することがある。有効な Object ID が入った場合だけ kind 不整合とみなす。
                if (entry.Kind == BuildContentKind.Scene
                    && ((entry.Object != null && !entry.Object.LoadableObjectId.Equals(default(Unity.Loading.LoadableObjectId)))
                        || !string.IsNullOrEmpty(entry.FileGuid) || entry.LocalFileId != 0))
                    throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidRoot, "Scene entry contains an object locator.", expectedIdentity, expectedTarget, entry.LogicalKey, entry.Representation);
                if (entry.Kind == BuildContentKind.Object && entry.Object == null)
                    throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidRoot, "Object entry has no loadable locator.", expectedIdentity, expectedTarget, entry.LogicalKey, entry.Representation);
                if (entry.Kind == BuildContentKind.Object && !entry.SceneId.Equals(default(Unity.Loading.LoadableSceneId)))
                    throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidRoot, "Object entry contains a scene locator.", expectedIdentity, expectedTarget, entry.LogicalKey, entry.Representation);
                if (entry.Kind == BuildContentKind.Object && entry.Object != null
                    && entry.Object.LoadableObjectId.Equals(default(Unity.Loading.LoadableObjectId)))
                    throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidRoot, "Object entry has an invalid Unity loadable locator.", expectedIdentity, expectedTarget, entry.LogicalKey, entry.Representation);
                if (root.SchemaVersion == 2 && entry.Kind == BuildContentKind.Object
                    && (!IsCanonicalGuid(entry.FileGuid) || entry.LocalFileId == 0))
                    throw new ContentDirectoryException(ContentDirectoryFailureCode.InvalidRoot, "Schema v2 object entry has no file/object locator.", expectedIdentity, expectedTarget, entry.LogicalKey, entry.Representation);
                var key = Key(entry.LogicalKey, EffectiveRepresentation(entry.Representation), entry.Kind);
                if (!entries.TryAdd(key, entry))
                    throw new ContentDirectoryException(ContentDirectoryFailureCode.EntryAmbiguous, "Content root contains duplicate logical entries.", expectedIdentity, expectedTarget, entry.LogicalKey, entry.Representation);
            }
            return new ContentDirectoryIndex(entries);
        }

        private static bool IsCanonicalGuid(string value)
        {
            if (value == null || value.Length != 32) return false;
            foreach (var ch in value)
                if (!((ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f'))) return false;
            return true;
        }

        internal BuildContentEntry ResolveObject(string logicalKey, string representation)
            => Resolve(logicalKey, representation, BuildContentKind.Object, allowFullFallback: false);

        internal BuildContentEntry ResolveScene(string logicalKey, string representation)
            => Resolve(logicalKey, representation, BuildContentKind.Scene, allowFullFallback: true);

        private BuildContentEntry Resolve(string logicalKey, string representation, BuildContentKind kind, bool allowFullFallback)
        {
            if (_entries.TryGetValue(Key(logicalKey, representation, kind), out var entry)) return entry;
            if (allowFullFallback && !string.Equals(representation, "Full", StringComparison.Ordinal)
                && _entries.TryGetValue(Key(logicalKey, "Full", kind), out entry)) return entry;
            throw new ContentDirectoryException(ContentDirectoryFailureCode.EntryMissing, "No matching content entry is registered.", logicalKey: logicalKey, representation: representation);
        }

        internal static string CacheKey(string path, string identity, string target, BuildContentEntry entry)
            => string.Concat(CachePrefix(path, identity, target),
                Part(entry.LogicalKey), ":", Part(EffectiveRepresentation(entry.Representation)), ":", entry.Kind, ":",
                Part(entry.FileGuid), ":", entry.LocalFileId, ":",
                Part(entry.Object?.LoadableObjectId.ToString() ?? string.Empty));

        internal static string CachePrefix(string path, string identity, string target)
            => string.Concat("directory:", Part(path), ":", Part(identity), ":", Part(target), ":");

        // 利用者の logical key に区切り文字が含まれても別 revision の token を共有しない。
        private static string Part(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

        // 旧 build の未タグ entry は空文字で保存される。Runtime ではそれを Full として
        // 索引化し、Object でも「選択した表現への厳密一致」という規則を維持する。
        private static string EffectiveRepresentation(string value) => string.IsNullOrEmpty(value) ? "Full" : value;

        private static string Key(string logicalKey, string representation, BuildContentKind kind)
            => string.Concat(logicalKey, "\u001f", representation, "\u001f", kind);
    }
}
