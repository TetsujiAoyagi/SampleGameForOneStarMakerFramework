#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using OneStarMaker.Editor.Build.Materialization;
using OneStarMaker.Runtime.BuildContent.Distribution;

namespace OneStarMaker.Editor.Build.Content
{
    /// <summary>閉包の記録の読みの結果。</summary>
    internal enum ContentSourceClosureState
    {
        /// <summary>版 1 の記録を読めた。<see cref="ContentSourceClosureReadResult.Roots"/> が root ごとの閉包。</summary>
        Present,
        /// <summary>欄が無いか版 0（記録しない publish、上限を超えて記録を落とした publish）。</summary>
        Missing,
        /// <summary>2 以上の版。この読み口は値を解釈しない。</summary>
        Unsupported,
        /// <summary>値が規則に合わない。理由は <see cref="ContentSourceClosureReadResult.Detail"/>。</summary>
        Invalid,
    }

    /// <summary>記録の root 1 つと、その閉包（base の build の時点の GUID と path）。</summary>
    internal sealed class ContentSourceClosureRootRecord
    {
        private readonly ReadOnlyCollection<BuildDependencyEntry> _assets;
        internal ContentSourceClosureRootRecord(string logicalKey, string kind, string representation, string guid,
            IEnumerable<BuildDependencyEntry> assets)
        {
            LogicalKey = logicalKey; Kind = kind; Representation = representation; Guid = guid;
            _assets = Array.AsReadOnly(assets.Select(x => new BuildDependencyEntry(x.Guid, x.Path))
                .OrderBy(x => x.Path, StringComparer.Ordinal).ThenBy(x => x.Guid, StringComparer.Ordinal).ToArray());
        }
        public string LogicalKey { get; }
        /// <summary>BuildContentKind の名前（"Scene" / "Object"）。</summary>
        public string Kind { get; }
        public string Representation { get; }
        public string Guid { get; }
        public IReadOnlyList<BuildDependencyEntry> Assets => _assets;
        // index の entry と突き合わせる値。表現は生の値のまま比べる（publisher が index に書く値と同じ）。
        internal string EntryKey => LogicalKey + "\n" + Kind + "\n" + Representation;
    }

    internal sealed class ContentSourceClosureReadResult
    {
        internal ContentSourceClosureReadResult(ContentSourceClosureState state, string detail, IEnumerable<ContentSourceClosureRootRecord>? roots = null)
        {
            State = state;
            Detail = detail;
            Roots = Array.AsReadOnly((roots ?? Array.Empty<ContentSourceClosureRootRecord>()).ToArray());
        }
        public ContentSourceClosureState State { get; }
        public string Detail { get; }
        /// <summary><see cref="ContentSourceClosureState.Present"/> の時だけ中身がある。他の状態では空で、閉包を返さない。</summary>
        public IReadOnlyList<ContentSourceClosureRootRecord> Roots { get; }
    }

    /// <summary>
    /// manifest の <c>sourceClosure</c> の値の規則を 1 か所に持つ。publisher が build の閉包から記録を作り、計画が base の manifest から読む。
    /// install と再検証はこの欄を見ないので、ここが読み手の唯一の規則である。
    /// </summary>
    internal static class ContentSourceClosureRecord
    {
        internal const int Version = 1;

        /// <summary>build の root ごとの閉包から版 1 の記録を作る。並びは値から決まる（同じ入力から同じ bytes）。</summary>
        internal static ContentSourceClosure Create(IEnumerable<ContentSourceClosureRootRecord> roots)
        {
            var ordered = roots.OrderBy(x => x.LogicalKey, StringComparer.Ordinal).ThenBy(x => x.Kind, StringComparer.Ordinal)
                .ThenBy(x => x.Representation, StringComparer.Ordinal).ToArray();
            var members = new SortedDictionary<string, (string Guid, SortedSet<int> Roots)>(StringComparer.Ordinal);
            for (var i = 0; i < ordered.Length; i++)
                foreach (var asset in ordered[i].Assets)
                {
                    if (!members.TryGetValue(asset.Path, out var member))
                        members[asset.Path] = member = (asset.Guid, new SortedSet<int>());
                    member.Roots.Add(i);
                }
            var groups = new List<int[]>();
            var groupIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            var assets = new List<ContentSourceClosureAsset>();
            foreach (var pair in members)
            {
                var signature = string.Join(",", pair.Value.Roots);
                if (!groupIndex.TryGetValue(signature, out var index))
                {
                    index = groups.Count;
                    groupIndex.Add(signature, index);
                    groups.Add(pair.Value.Roots.ToArray());
                }
                assets.Add(new ContentSourceClosureAsset { guid = pair.Value.Guid, path = pair.Key, group = index });
            }
            return new ContentSourceClosure
            {
                version = Version,
                roots = ordered.Select(x => new ContentSourceClosureRoot
                    { logicalKey = x.LogicalKey, kind = x.Kind, representation = x.Representation, guid = x.Guid }).ToArray(),
                assets = assets.ToArray(),
                groups = groups.Select(x => new ContentSourceClosureGroup { roots = x }).ToArray(),
            };
        }

        /// <summary>
        /// 記録を読む。渡す manifest は、計画が install の結果（digest・contentSet・revision・target）と照合し終えた bytes から読んだ物に限る。
        /// 照合に落ちた manifest をここへ渡して「壊れている」と読ませない（計画が先に例外にする）。
        /// </summary>
        internal static ContentSourceClosureReadResult Read(ContentTransportManifest manifest)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            var record = manifest.sourceClosure;
            if (record == null || record.version == 0) return new ContentSourceClosureReadResult(ContentSourceClosureState.Missing, "No source closure record.");
            if (record.version < 0) return Invalid("The record version is negative.");
            if (record.version > Version)
                return new ContentSourceClosureReadResult(ContentSourceClosureState.Unsupported, "Record version " + record.version + " is not supported.");
            if (record.roots == null || record.assets == null || record.groups == null) return Invalid("A record table is missing.");

            var roots = record.roots;
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var rootGuids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (root == null || string.IsNullOrWhiteSpace(root.logicalKey) || root.representation == null
                    || (root.kind != "Scene" && root.kind != "Object"))
                    return Invalid("Root " + i + " has a missing or unknown value.");
                if (!IsGuid(root.guid)) return Invalid("Root " + i + " has a malformed GUID.");
                if (!keys.Add(root.logicalKey + "\n" + root.kind + "\n" + root.representation) || !rootGuids.Add(root.guid))
                    return Invalid("Root " + i + " is duplicated.");
            }
            var groups = record.groups;
            for (var g = 0; g < groups.Length; g++)
            {
                var members = groups[g]?.roots;
                if (members == null || members.Length == 0 || members.Any(x => x < 0 || x >= roots.Length)
                    || members.Distinct().Count() != members.Length)
                    return Invalid("Group " + g + " names a root out of range.");
            }

            var sourcePaths = new HashSet<string>((manifest.sourceFiles ?? Array.Empty<ContentSourceFile>())
                .Where(x => x != null).Select(x => x.path), StringComparer.Ordinal);
            var guids = new HashSet<string>(StringComparer.Ordinal);
            // path の重複は大文字小文字を無視して数える（Windows では同じ file になる）。
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var closures = roots.Select(_ => new List<BuildDependencyEntry>()).ToArray();
            foreach (var asset in record.assets)
            {
                if (asset == null || !IsGuid(asset.guid)) return Invalid("An asset has a malformed GUID.");
                if (!guids.Add(asset.guid)) return Invalid("Asset GUID " + asset.guid + " is duplicated.");
                if (string.IsNullOrWhiteSpace(asset.path) || !paths.Add(asset.path)) return Invalid("Asset path '" + asset.path + "' is blank or duplicated.");
                if (!sourcePaths.Contains(asset.path)) return Invalid("Asset path '" + asset.path + "' is not in sourceFiles.");
                if (asset.group < 0 || asset.group >= groups.Length) return Invalid("Asset " + asset.guid + " names a group out of range.");
                foreach (var member in groups[asset.group].roots) closures[member].Add(new BuildDependencyEntry(asset.guid, asset.path));
            }

            // root の集合は publish の索引の entry の集合と等しい（同じ build の projection の root から作るので）。
            var index = manifest.index;
            var entryKeys = index != null && index.version == 1 && index.entries != null
                ? index.entries.Where(x => x != null).Select(x => x.logicalKey + "\n" + x.kind + "\n" + x.representation).ToArray()
                : Array.Empty<string>();
            if (entryKeys.Length != keys.Count || !keys.SetEquals(entryKeys)) return Invalid("The record roots differ from the index entries.");

            var result = new List<ContentSourceClosureRootRecord>();
            for (var i = 0; i < roots.Length; i++)
            {
                if (!closures[i].Any(x => string.Equals(x.Guid, roots[i].guid, StringComparison.Ordinal)))
                    return Invalid("Root " + roots[i].logicalKey + " is absent from its own closure.");
                result.Add(new ContentSourceClosureRootRecord(roots[i].logicalKey, roots[i].kind, roots[i].representation, roots[i].guid, closures[i]));
            }
            return new ContentSourceClosureReadResult(ContentSourceClosureState.Present, "Source closure record is readable.", result);
        }

        // materialize の正規化と同じ形（小文字 16 進 32 字）だけを受ける。
        internal static bool IsGuid(string? value) => value != null && value.Length == 32
            && value.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));

        private static ContentSourceClosureReadResult Invalid(string detail) => new(ContentSourceClosureState.Invalid, detail);
    }
}
