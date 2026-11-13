#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Materialization;

namespace OneStarMaker.Editor.Build.Content
{
    /// <summary>編集した path が、どの選択 root の上書きの理由にもならなかった理由。</summary>
    public enum ContentOverlayUnmatchedReason
    {
        /// <summary>
        /// path が ordinal の <c>Assets/</c> で始まらない（<c>Packages/</c> の下、<c>assets/</c> のような大小違いを含む）。
        /// 依存閉包は <c>Assets/</c> の下の file だけで作られるので、どの root にも届かない。
        /// </summary>
        OutsideAssets,
        /// <summary>
        /// どの選択 root の依存閉包にも現れない。script（閉包が除く拡張子）、存在しない path、folder、どの root も使わない asset が
        /// ここに入る。これらは file system を読まずには区別できないので、1 つの理由にまとめる。
        /// </summary>
        NotInAnyClosure
    }

    /// <summary>上書きに入らなかった編集 1 件と、その理由。</summary>
    public sealed class ContentOverlayUnmatchedEdit
    {
        internal ContentOverlayUnmatchedEdit(string path, ContentOverlayUnmatchedReason reason) { Path = path; Reason = reason; }
        /// <summary>正規化した path（区切りは <c>/</c>、<c>.meta</c> は外した形）。</summary>
        public string Path { get; }
        public ContentOverlayUnmatchedReason Reason { get; }
    }

    /// <summary>上書きに入れた root 1 つと、それを入れた理由になった編集（その root の依存閉包にある編集。root 自身を含む）。</summary>
    public sealed class ContentOverlayRootReason
    {
        private readonly ReadOnlyCollection<string> _edits;
        internal ContentOverlayRootReason(BuildContentCandidate root, IEnumerable<string> edits)
        {
            Root = root;
            _edits = Array.AsReadOnly(edits.OrderBy(x => x, StringComparer.Ordinal).ToArray());
        }
        public BuildContentCandidate Root { get; }
        public IReadOnlyList<string> Edits => _edits;
    }

    /// <summary>
    /// 上書きを重ねる base の目的 build の識別（登録契約 C6 の材料）。上書きは、作った時の base と同じ base にだけ重ねる。
    /// 空の欄は「base が分からない記録」になり、上書きでない build と区別できないので受け付けない。
    /// </summary>
    public sealed class ContentOverlayBaseIdentity
    {
        /// <param name="contentSet">base の contentSet（workspace の名前）。</param>
        /// <param name="revision">base の revision（publish した transport manifest の revision。base の build identity）。</param>
        /// <param name="target">base の target（transport manifest の target）。</param>
        /// <param name="manifestDigest">base の transport manifest の digest（呼び手が決めた hash の文字列）。</param>
        /// <param name="declarationName">base の宣言の名前。宣言を持たない base では null（空文字は受け付けない）。</param>
        public ContentOverlayBaseIdentity(string contentSet, string revision, string target, string manifestDigest, string? declarationName)
        {
            ContentSet = Require(contentSet, nameof(contentSet));
            Revision = Require(revision, nameof(revision));
            Target = Require(target, nameof(target));
            ManifestDigest = Require(manifestDigest, nameof(manifestDigest));
            if (declarationName != null && string.IsNullOrWhiteSpace(declarationName))
                throw new ArgumentException("A declaration name must be null (no declaration) or non-empty.", nameof(declarationName));
            DeclarationName = declarationName;
        }

        public string ContentSet { get; }
        public string Revision { get; }
        public string Target { get; }
        public string ManifestDigest { get; }
        public string? DeclarationName { get; }

        private static string Require(string? value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Base identity fields must not be empty.", name);
            return value!;
        }
    }

    /// <summary>
    /// 逆引きの結果。上書きが要る時は、絞った plan と、逆引きに使った snapshot をそのまま持つ。
    /// 上書きの request はこの結果 1 つから plan と snapshot を取るので、記録の理由と build の入力が食い違わない。
    /// </summary>
    public sealed class ContentOverlaySelectionResult
    {
        private readonly ReadOnlyCollection<string> _edits;
        private readonly ReadOnlyCollection<ContentOverlayRootReason> _roots;
        private readonly ReadOnlyCollection<ContentOverlayUnmatchedEdit> _unmatched;
        private readonly ReadOnlyCollection<string> _sharedAssets;

        internal ContentOverlaySelectionResult(BuildPlan? plan, BuildMaterializationSnapshot snapshot, IEnumerable<string> edits,
            IEnumerable<ContentOverlayRootReason> roots, IEnumerable<ContentOverlayUnmatchedEdit> unmatched, IEnumerable<string> sharedAssets)
        {
            Plan = plan;
            Snapshot = snapshot;
            _edits = Array.AsReadOnly(edits.ToArray());
            _roots = Array.AsReadOnly(roots.ToArray());
            _unmatched = Array.AsReadOnly(unmatched.ToArray());
            _sharedAssets = Array.AsReadOnly(sharedAssets.ToArray());
        }

        /// <summary>
        /// false なら上書きは要らない（編集が無い、またはどの root にも当たらない）。その時は build せず、
        /// <see cref="Unmatched"/> を利用者へ出すのは呼び手の責任である（preflight は作られない）。
        /// </summary>
        public bool IsOverlayRequired => Plan != null;
        /// <summary>上書きの root だけを selected に残した plan。上書き不要の時は null。</summary>
        public BuildPlan? Plan { get; }
        /// <summary>逆引きに使った snapshot。上書きの build もこれを使う。</summary>
        public BuildMaterializationSnapshot Snapshot { get; }
        /// <summary>正規化した編集の集合（ordinal 順）。</summary>
        public IReadOnlyList<string> Edits => _edits;
        /// <summary>上書きに入れた root と理由（stable key の ordinal 順）。</summary>
        public IReadOnlyList<ContentOverlayRootReason> Roots => _roots;
        /// <summary>どの root にも当たらなかった編集と理由（path の ordinal 順）。</summary>
        public IReadOnlyList<ContentOverlayUnmatchedEdit> Unmatched => _unmatched;
        /// <summary>
        /// 上書きの root の閉包の和集合と、上書きに入らなかった選択 root の閉包の和集合の共通部分（path の ordinal 順）。
        /// base と上書きを重ねた Play で、両方の root が直接参照すれば実体が 2 つになりうる asset の一覧（登録契約 C9）。
        /// 閉包は <c>Assets/</c> の下だけなので、package の shader 等は出ない。
        /// </summary>
        public IReadOnlyList<string> SharedAssets => _sharedAssets;
    }

    /// <summary>上書きの request が持つ記録。逆引きの結果と、重ねる base の識別。</summary>
    public sealed class ContentOverlayRecord
    {
        internal ContentOverlayRecord(ContentOverlaySelectionResult selection, ContentOverlayBaseIdentity baseIdentity)
        {
            Selection = selection;
            BaseIdentity = baseIdentity;
        }
        public ContentOverlaySelectionResult Selection { get; }
        public ContentOverlayBaseIdentity BaseIdentity { get; }
    }

    /// <summary>
    /// 編集した asset の集合から、上書きに入れる選択 root を決める逆引き。I/O を持たず、渡された plan と snapshot の閉包だけで決める。
    /// </summary>
    /// <remarks>
    /// 保証は「渡された編集について、古い版を黙って残さない」まで: 選択 root のうち、snapshot の依存閉包に編集の file を
    /// 1 つでも含む root は必ず上書きに入り、編集は「入れた理由」か「当たらなかった理由」のどちらかに必ず現れる。
    /// 判定は snapshot を作った時点の閉包で行うので、snapshot から上書きの build が終わるまで source を変えないのは呼び手の前提である。
    /// 閉包が持たない依存（import 時の依存など）は判定に現れない。
    /// </remarks>
    public static class ContentOverlaySelection
    {
        private const string AssetsPrefix = "Assets/";
        private const string MetaSuffix = ".meta";

        /// <param name="plan">目的の宣言から作った成功 plan（bootstrap 等の合成を終えた物）。</param>
        /// <param name="snapshot"><paramref name="plan"/> と対の snapshot（合成の後の候補と閉包を持つ物）。</param>
        /// <param name="editedPaths">編集した asset の path。<c>\</c> 区切りと <c>.meta</c> を受け付ける。</param>
        /// <exception cref="ArgumentException">
        /// 選択 root に依存閉包がちょうど 1 つ対応しない（合成の前の snapshot を渡したなど）。編集の path に null がある。
        /// </exception>
        public static ContentOverlaySelectionResult Select(BuildPlan plan, BuildMaterializationSnapshot snapshot, IEnumerable<string> editedPaths)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (editedPaths == null) throw new ArgumentNullException(nameof(editedPaths));

            // 選択 root と閉包の対応を先に確かめる。閉包の無い root を「当たらない」と読むと、合成で足された root が
            // 黙って上書きから外れる（I-1）。projection と同じく physical key（root の GUID）で閉包を引く。
            var selected = plan.SelectedContent.OrderBy(x => x.StableKey, StringComparer.Ordinal).ToArray();
            var closures = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var candidate in selected)
            {
                var matches = snapshot.Dependencies.Where(x => string.Equals(x.RootGuid, candidate.PhysicalKey, StringComparison.Ordinal)).ToArray();
                if (matches.Length != 1)
                    throw new ArgumentException("Selected candidate '" + candidate.StableKey + "' has " + matches.Length +
                                                " dependency closures in the snapshot; pass the snapshot that pairs with the plan.", nameof(snapshot));
                // 閉包との突き合わせだけ大文字小文字を無視する（Windows の VCS が大小違いの path を返しうる）。
                closures[candidate.StableKey] = new HashSet<string>(matches[0].Entries.Select(x => x.Path), StringComparer.OrdinalIgnoreCase);
            }

            var edits = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var raw in editedPaths)
            {
                if (raw == null) throw new ArgumentException("Edited paths must not contain null.", nameof(editedPaths));
                edits.Add(Normalize(raw));
            }

            var reasons = selected.ToDictionary(x => x.StableKey, _ => new List<string>(), StringComparer.Ordinal);
            var unmatched = new List<ContentOverlayUnmatchedEdit>();
            foreach (var edit in edits)
            {
                // 接頭辞は閉包を作る側（IsContent）と同じ ordinal で判定する。ここを大小無視にすると、閉包に決して現れない path を
                // 「閉包に無い」と別の理由で報告することになる。
                if (!edit.StartsWith(AssetsPrefix, StringComparison.Ordinal))
                {
                    unmatched.Add(new ContentOverlayUnmatchedEdit(edit, ContentOverlayUnmatchedReason.OutsideAssets));
                    continue;
                }
                var hit = false;
                foreach (var candidate in selected)
                {
                    if (!closures[candidate.StableKey].Contains(edit)) continue;
                    reasons[candidate.StableKey].Add(edit);
                    hit = true;
                }
                if (!hit) unmatched.Add(new ContentOverlayUnmatchedEdit(edit, ContentOverlayUnmatchedReason.NotInAnyClosure));
            }

            var overlay = selected.Where(x => reasons[x.StableKey].Count != 0).ToArray();
            if (overlay.Length == 0)
                return new ContentOverlaySelectionResult(null, snapshot, edits, Array.Empty<ContentOverlayRootReason>(), unmatched, Array.Empty<string>());

            // 共有 asset は path の ordinal で数える（閉包の path は materialization が正規化した canonical な形）。
            var overlayKeys = new HashSet<string>(overlay.Select(x => x.StableKey), StringComparer.Ordinal);
            var overlayFiles = new HashSet<string>(StringComparer.Ordinal);
            var remainingFiles = new HashSet<string>(StringComparer.Ordinal);
            foreach (var candidate in selected)
                (overlayKeys.Contains(candidate.StableKey) ? overlayFiles : remainingFiles).UnionWith(closures[candidate.StableKey]);
            var shared = overlayFiles.Where(remainingFiles.Contains).OrderBy(x => x, StringComparer.Ordinal).ToArray();

            return new ContentOverlaySelectionResult(plan.WithSelectedSubset(overlayKeys), snapshot, edits,
                overlay.Select(x => new ContentOverlayRootReason(x, reasons[x.StableKey])), unmatched, shared);
        }

        // 区切りを / にし、X.meta を X に読み替える（.meta だけの変更は import の設定の変更で、asset 本体の版を変える）。
        // 大文字小文字は変えない（Assets/ の接頭辞の判定を ordinal に保つため）。
        private static string Normalize(string path)
        {
            var normalized = path.Replace('\\', '/');
            return normalized.EndsWith(MetaSuffix, StringComparison.OrdinalIgnoreCase)
                ? normalized.Substring(0, normalized.Length - MetaSuffix.Length)
                : normalized;
        }
    }
}
