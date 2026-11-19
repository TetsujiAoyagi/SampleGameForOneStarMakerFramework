#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
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
        private readonly ContentEntryTable<BuildContentEntry> _entries;
        private readonly BuildContentEntry[] _all;

        private ContentDirectoryIndex(ContentEntryTable<BuildContentEntry> entries, BuildContentEntry[] all)
        { _entries = entries; _all = all; }

        /// <summary>索引に入れた全ての entry（root の並びのまま）。上書きの層の id による影を作る時に読む。</summary>
        internal IReadOnlyList<BuildContentEntry> Entries => _all;

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

            var entries = new ContentEntryTable<BuildContentEntry>();
            var all = new List<BuildContentEntry>();
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

                if (!entries.TryAdd(entry.LogicalKey, entry.Representation, entry.Kind, entry))
                { issue = new(ContentDirectoryFailureCode.EntryAmbiguous, "Content root contains duplicate logical entries.", entry.LogicalKey, entry.Representation); return false; }
                all.Add(entry);
            }
            index = new ContentDirectoryIndex(entries, all.ToArray());
            issue = default;
            return true;
        }

        // 照合の規則（Object も Scene と同じ Full fallback）は ContentEntryTable にある。宣言した表現の候補の存在は build 側が検査する。
        internal bool TryResolveObject(string logicalKey, string representation, out BuildContentEntry? entry, out ContentDirectoryIssue issue)
            => TryResolve(logicalKey, representation, BuildContentKind.Object, out entry, out issue);

        internal bool TryResolveScene(string logicalKey, string representation, out BuildContentEntry? entry, out ContentDirectoryIssue issue)
            => TryResolve(logicalKey, representation, BuildContentKind.Scene, out entry, out issue);

        /// <summary>
        /// 実効の表現が完全に一致する entry だけを引く（Full への fallback をしない）。上書きを重ねた登録の層の解決は、
        /// 要求の表現を両層で引いてから Full を両層で引くので、1 層の中の fallback を先に効かせない。
        /// </summary>
        internal bool TryGetExact(string logicalKey, string representation, BuildContentKind kind, out BuildContentEntry? entry)
        {
            if (_entries.TryGetExact(logicalKey, representation, kind, out var found)) { entry = found; return true; }
            entry = null;
            return false;
        }

        private bool TryResolve(string logicalKey, string representation, BuildContentKind kind,
            out BuildContentEntry? entry, out ContentDirectoryIssue issue)
        {
            if (_entries.TryResolve(logicalKey, representation, kind, out var found))
            { entry = found; issue = default; return true; }
            entry = null;
            issue = new(ContentDirectoryFailureCode.EntryMissing, "No matching content entry is registered.", logicalKey, representation);
            return false;
        }

        internal static string CacheKey(string path, string identity, string target, BuildContentEntry entry)
            => CacheKey(CachePrefix(path, identity, target), entry);

        // prefix は directory（上書きを重ねた登録では base の前置きの後に層の部分を続けた物）。entry の部分は層によらず同じ形。
        internal static string CacheKey(string prefix, BuildContentEntry entry)
            => string.Concat(prefix, Part(entry.LogicalKey), ":",
                Part(ContentEntryMatch.EffectiveRepresentation(entry.Representation)), ":", entry.Kind, ":",
                Part(entry.FileGuid), ":", entry.LocalFileId, ":",
                Part(entry.Object?.LoadableObjectId.ToString() ?? string.Empty));

        internal static string CachePrefix(string path, string identity, string target)
            => string.Concat("directory:", Part(path), ":", Part(identity), ":", Part(target), ":");

        // 上書きの層の cache の前置き。session に共通の前置き（base）の後に続けるので、close の退避（前置き 1 つ）が両方の層に届く。
        // "~" は Base64 の字に無いので、base の entry の部分（Base64 で始まる）と衝突しない。
        internal static string OverlayLayerPrefix(string sessionPrefix, string path, string identity)
            => string.Concat(sessionPrefix, "~overlay:", Part(path), ":", Part(identity), ":");

        private static bool IsCanonicalGuid(string value)
        {
            if (value == null || value.Length != 32) return false;
            foreach (var ch in value)
                if (!((ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f'))) return false;
            return true;
        }

        // 区切り文字を含む logical key でも別 revision の token と衝突させない。
        private static string Part(string value) => ContentEntryMatch.Part(value);
    }

    // 「要求 (logical key, kind, 表現) を entry の表が含むか」の唯一の規則。登録した directory の索引（ContentDirectoryIndex）と、
    // publish した manifest の索引（ContentPurposeIndex）の両方がこれを使う。規則を 2 箇所に書くと、manifest の索引が
    // 「含む」と答えた build を登録すると解決できない、という食い違いが起きうるため、片方だけを変えない。
    internal static class ContentEntryMatch
    {
        internal const string Full = "Full";

        // 旧 build の未タグ entry と、表現を指定しない要求は空文字。どちらも Full として扱う。
        internal static string EffectiveRepresentation(string? value) => string.IsNullOrEmpty(value) ? Full : value!;

        internal static string Key(string logicalKey, string? representation, BuildContentKind kind)
            // 入力に区切り文字を含めても組キーが別組と衝突しない形式にする。
            => string.Concat(Part(logicalKey), ":", Part(EffectiveRepresentation(representation)), ":", kind);

        internal static string Part(string? value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
    }

    // key は (logical key, 実効表現, kind)。同じ key の 2 件目は TryAdd が false を返し、呼び手が曖昧として拒む。
    internal sealed class ContentEntryTable<T>
    {
        private readonly Dictionary<string, T> _entries = new(StringComparer.Ordinal);

        internal bool TryAdd(string logicalKey, string? representation, BuildContentKind kind, T value)
            => _entries.TryAdd(ContentEntryMatch.Key(logicalKey, representation, kind), value);

        // 実効表現の完全一致だけを引き、Full へ fallback しない。上書きを重ねた session の層の解決は、
        // 要求の表現を両方の層で引いてから Full を両方の層で引くので、fallback を層ごとに行うと順が崩れる。
        internal bool TryGetExact(string logicalKey, string? representation, BuildContentKind kind, [MaybeNullWhen(false)] out T value)
            => _entries.TryGetValue(ContentEntryMatch.Key(logicalKey, representation, kind), out value);

        // 要求した表現の entry が無いときだけ Full の entry へ fallback する（Scene も Object も同じ）。
        // Object も fallback するのは、付随 asset（VFX の source、共有 material）は Full だけを宣言するのが普通で、
        // Whitebox の Play で読めなければ Scene は載るのに演出だけが欠けるため。
        // 表現を指定しない要求は Full だけに当たり、Whitebox だけの entry には当たらない。
        internal bool TryResolve(string logicalKey, string? representation, BuildContentKind kind, [MaybeNullWhen(false)] out T value)
        {
            var requested = ContentEntryMatch.EffectiveRepresentation(representation);
            if (_entries.TryGetValue(ContentEntryMatch.Key(logicalKey, requested, kind), out value)) return true;
            if (!string.Equals(requested, ContentEntryMatch.Full, StringComparison.Ordinal)
                && _entries.TryGetValue(ContentEntryMatch.Key(logicalKey, ContentEntryMatch.Full, kind), out value)) return true;
            value = default;
            return false;
        }
    }
}
