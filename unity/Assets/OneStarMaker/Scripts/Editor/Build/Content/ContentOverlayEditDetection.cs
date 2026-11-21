#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using OneStarMaker.Editor.Build.Materialization;
using OneStarMaker.Runtime.BuildContent.Distribution;

namespace OneStarMaker.Editor.Build.Content
{
    /// <summary>
    /// base の記録と手元の file を比べて編集を見つける（計画の一部。分類は <see cref="ContentOverlayPlanner"/>）。
    /// 閉包の記録がある base では GUID で突き合わせるので、移動が「追加」と「欠け」に割れない。記録の無い・未対応・壊れている base では、
    /// selected の root の手元の閉包の file を path で <c>sourceFiles</c> と比べる。どちらも、除外した候補の閉包だけにある file は見ない。
    /// </summary>
    internal sealed class ContentOverlayEditDetection
    {
        private readonly Dictionary<BuildDependencySnapshot, (HashSet<string> Guids, HashSet<string> Paths)> _local = new();
        private readonly Dictionary<ContentSourceClosureRootRecord, HashSet<string>> _record = new();

        private ContentOverlayEditDetection(IReadOnlyList<ContentOverlayEdit> edits, ISet<string> unresolved)
        {
            Edits = edits;
            Unresolved = unresolved;
        }

        public IReadOnlyList<ContentOverlayEdit> Edits { get; }
        /// <summary>閉包の記録の asset のうち、手元で解決しない（GUID が file を指さない）物の GUID。記録の無い base では空。</summary>
        public ISet<string> Unresolved { get; }

        // 手元の閉包に当たるか。GUID か path（大文字小文字は無視。Windows では同じ file）で見る。
        public bool HitsLocal(ContentOverlayEdit edit, BuildDependencySnapshot? closure)
        {
            if (closure == null) return false;
            if (!_local.TryGetValue(closure, out var sets))
                _local[closure] = sets = (new HashSet<string>(closure.Entries.Select(x => x.Guid), StringComparer.Ordinal),
                    new HashSet<string>(closure.Entries.Select(x => x.Path), StringComparer.OrdinalIgnoreCase));
            return (edit.Guid != null && sets.Guids.Contains(edit.Guid)) || sets.Paths.Contains(edit.Path);
        }

        // 記録の閉包に当たるか。GUID だけで見る（移動して編集した asset も、source の欠けた root の記録の閉包に当たる）。
        public bool HitsRecord(ContentOverlayEdit edit, ContentSourceClosureRootRecord? root)
        {
            if (root == null || edit.Guid == null) return false;
            if (!_record.TryGetValue(root, out var guids))
                _record[root] = guids = new HashSet<string>(root.Assets.Select(x => x.Guid), StringComparer.Ordinal);
            return guids.Contains(edit.Guid);
        }

        internal static ContentOverlayEditDetection Detect(ContentTransportManifest manifest, ContentSourceClosureReadResult record,
            IReadOnlyList<ContentOverlayPlanner.LocalRoot> roots, IContentOverlayLocalSource local)
        {
            var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in manifest.sourceFiles ?? Array.Empty<ContentSourceFile>())
                if (file != null && !string.IsNullOrEmpty(file.path) && !sources.ContainsKey(file.path)) sources[file.path] = file.sha256;
            var selected = roots.Where(x => x.Closure != null).SelectMany(x => x.Closure!.Entries).ToArray();
            var edits = new List<ContentOverlayEdit>();
            var unresolved = new HashSet<string>(StringComparer.Ordinal);

            if (record.State == ContentSourceClosureState.Present)
            {
                var known = new HashSet<string>(StringComparer.Ordinal);
                foreach (var asset in record.Roots.SelectMany(x => x.Assets).GroupBy(x => x.Guid, StringComparer.Ordinal).Select(x => x.First())
                             .OrderBy(x => x.Path, StringComparer.Ordinal))
                {
                    known.Add(asset.Guid);
                    var path = local.ResolveGuid(asset.Guid);
                    // 手元に無い asset は編集に数えない（消したのか checkout していないのかを区別できない）。source の欠けとして分類が使う。
                    if (path == null) { unresolved.Add(asset.Guid); continue; }
                    if (!string.Equals(path, asset.Path, StringComparison.OrdinalIgnoreCase))
                        edits.Add(new ContentOverlayEdit(path, ContentOverlayEditKind.Moved, asset.Guid, asset.Path));
                    if (Changed(path, asset.Path, sources, local))
                        edits.Add(new ContentOverlayEdit(path, ContentOverlayEditKind.Changed, asset.Guid, null));
                }
                foreach (var entry in selected.Where(x => !known.Contains(x.Guid)).GroupBy(x => x.Guid, StringComparer.Ordinal).Select(x => x.First()))
                    edits.Add(new ContentOverlayEdit(entry.Path, ContentOverlayEditKind.Added, entry.Guid, null));
            }
            else
            {
                foreach (var entry in selected.GroupBy(x => x.Path, StringComparer.OrdinalIgnoreCase).Select(x => x.First()))
                {
                    if (!sources.ContainsKey(entry.Path))
                        edits.Add(new ContentOverlayEdit(entry.Path, ContentOverlayEditKind.Added, null, null));
                    else if (Changed(entry.Path, entry.Path, sources, local))
                        edits.Add(new ContentOverlayEdit(entry.Path, ContentOverlayEditKind.Changed, null, null));
                }
            }
            return new ContentOverlayEditDetection(edits, unresolved);
        }

        // file か .meta の SHA-256 が base の値と違う。base に .meta の行が無く手元にも無いなら等しい。
        private static bool Changed(string localPath, string basePath, IReadOnlyDictionary<string, string> sources, IContentOverlayLocalSource local)
        {
            sources.TryGetValue(basePath, out var file);
            sources.TryGetValue(basePath + ContentSourceHashes.MetaSuffix, out var meta);
            return !string.Equals(local.Sha256(localPath), file, StringComparison.Ordinal)
                   || !string.Equals(local.Sha256(localPath + ContentSourceHashes.MetaSuffix), meta, StringComparison.Ordinal);
        }
    }
}
