#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Materialization;

namespace OneStarMaker.Editor.Build.Content
{
    /// <summary>上書きの計画の状態。</summary>
    public enum ContentOverlayPlanState
    {
        /// <summary>上書きに入れる root も作り直せない root も無い。build せず base だけで動かしてよい。</summary>
        NotRequired,
        /// <summary>上書きに入れる root がある。作り直せない root があれば、その集合を認めた計画だけが上書きを得る。</summary>
        BuildRequired,
        /// <summary>
        /// 上書きに入れる root が無く、作り直せない root だけがある（他の供給元が空とは限らない）。build は要らないが、
        /// 編集の影響を受ける root が base の版で動くので <see cref="NotRequired"/> と同じに扱わない。続けるか止めるかは呼び手が決める。
        /// </summary>
        UnbuildableOnly,
    }

    /// <summary>手元の file と base の記録を比べて見つけた編集の種類。削除は編集に数えない（手元に無い file は source の欠けとして扱う）。</summary>
    public enum ContentOverlayEditKind
    {
        /// <summary>file か <c>.meta</c> の SHA-256 が base の <c>sourceFiles</c> の値と違う（<c>.meta</c> だけの変更も本体の path で出る）。</summary>
        Changed,
        /// <summary>閉包の記録の asset（GUID）が、手元では base と違う path にある（大文字小文字だけの違いは移動に数えない）。</summary>
        Moved,
        /// <summary>
        /// 手元の選択の selected の root の閉包にあり、base に無い file。閉包の記録がある base では GUID が記録に無い file、
        /// 記録の無い base では path が <c>sourceFiles</c> に無い file。
        /// </summary>
        Added,
    }

    /// <summary>編集 1 件。同じ asset が移動して内容も変わった時は、同じ path の <see cref="ContentOverlayEditKind.Moved"/> と <see cref="ContentOverlayEditKind.Changed"/> の 2 件になる。</summary>
    public sealed class ContentOverlayEdit
    {
        internal ContentOverlayEdit(string path, ContentOverlayEditKind kind, string? guid, string? basePath)
        { Path = path; Kind = kind; Guid = guid; BasePath = basePath; }
        /// <summary>手元の path（区切りは <c>/</c>、<c>.meta</c> は外した形）。</summary>
        public string Path { get; }
        public ContentOverlayEditKind Kind { get; }
        /// <summary>asset の GUID。記録の無い base で path だけで比べた編集では null。</summary>
        public string? Guid { get; }
        /// <summary>移動した asset の base の path。移動でなければ null。</summary>
        public string? BasePath { get; }
    }

    /// <summary>計画が分類した root の識別（directory の entry の値と root の asset の GUID）。</summary>
    public sealed class ContentOverlayRootIdentity : IEquatable<ContentOverlayRootIdentity>
    {
        internal ContentOverlayRootIdentity(string logicalKey, string kind, string representation, string rootGuid, string? stableKey)
        { LogicalKey = logicalKey; Kind = kind; Representation = representation; RootGuid = rootGuid; StableKey = stableKey; }
        public string LogicalKey { get; }
        /// <summary>"Scene" か "Object"。</summary>
        public string Kind { get; }
        public string Representation { get; }
        public string RootGuid { get; }
        /// <summary>手元の選択の候補の stable key。閉包の記録にだけある root（手元の選択に無い root）では null。</summary>
        public string? StableKey { get; }

        // 認めの照合に使う同一性。stable key は記録にだけある root で持てないので入れない。
        public bool Equals(ContentOverlayRootIdentity? other) => other != null
            && string.Equals(LogicalKey, other.LogicalKey, StringComparison.Ordinal) && string.Equals(Kind, other.Kind, StringComparison.Ordinal)
            && string.Equals(Representation, other.Representation, StringComparison.Ordinal) && string.Equals(RootGuid, other.RootGuid, StringComparison.Ordinal);
        public override bool Equals(object? obj) => Equals(obj as ContentOverlayRootIdentity);
        public override int GetHashCode() => HashCode.Combine(LogicalKey, Kind, Representation, RootGuid);
        internal string SortKey => LogicalKey + "\n" + Kind + "\n" + Representation + "\n" + RootGuid;
    }

    /// <summary>作り直せない root の理由。</summary>
    public enum ContentOverlayUnbuildableReason
    {
        /// <summary>source の欠けた selected の root で、閉包の記録の閉包に編集を含む。</summary>
        SourceAbsent,
        /// <summary>source の欠けた selected の root で、閉包の記録に無い（base にも上書きにも無い root になる）。</summary>
        NotInBase,
        /// <summary>source の欠けた selected の root があり、base に閉包の記録が無い（編集が 0 件に見えても、欠けた selected の root の全て）。</summary>
        RecordMissing,
        /// <summary>同上で、閉包の記録が未対応の版。</summary>
        RecordUnsupported,
        /// <summary>同上で、閉包の記録が壊れている。</summary>
        RecordInvalid,
        /// <summary>閉包の記録にあって手元の選択に無い root で、記録の閉包に編集を含む。</summary>
        NotInLocalSelection,
    }

    /// <summary>作り直せない root 1 つ。当たった編集（path）と、手元に無い file（path）を持つ。</summary>
    public sealed class ContentOverlayUnbuildableRoot
    {
        private readonly ReadOnlyCollection<string> _edits;
        private readonly ReadOnlyCollection<string> _missing;
        internal ContentOverlayUnbuildableRoot(ContentOverlayRootIdentity root, ContentOverlayUnbuildableReason reason,
            IEnumerable<string> edits, IEnumerable<string> missingFiles)
        {
            Root = root; Reason = reason;
            _edits = Sorted(edits);
            _missing = Sorted(missingFiles);
        }
        public ContentOverlayRootIdentity Root { get; }
        public ContentOverlayUnbuildableReason Reason { get; }
        public IReadOnlyList<string> Edits => _edits;
        /// <summary>
        /// 手元に無い file。materialize が見た欠け（root の file、Unity が返した依存の path）と、閉包の記録の asset のうち手元で解決しない物の
        /// base の path。root の GUID が手元で解決しない時は記録の path で補う。記録が無い base では materialize が見た分だけ。
        /// </summary>
        public IReadOnlyList<string> MissingFiles => _missing;
        internal static ReadOnlyCollection<string> Sorted(IEnumerable<string> values) =>
            Array.AsReadOnly(values.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    /// <summary>他の供給元（開いている Scene を届ける snapshot など）が届ける root 1 つと、当たった編集。</summary>
    public sealed class ContentOverlaySuppliedRoot
    {
        private readonly ReadOnlyCollection<string> _edits;
        internal ContentOverlaySuppliedRoot(ContentOverlayRootIdentity root, IEnumerable<string> edits)
        { Root = root; _edits = ContentOverlayUnbuildableRoot.Sorted(edits); }
        public ContentOverlayRootIdentity Root { get; }
        public IReadOnlyList<string> Edits => _edits;
    }

    /// <summary>上書きに入れる root 1 つと理由。</summary>
    public sealed class ContentOverlayPlannedRoot
    {
        private readonly ReadOnlyCollection<string> _edits;
        internal ContentOverlayPlannedRoot(BuildContentCandidate root, IEnumerable<string> edits, bool notInBase)
        { Root = root; _edits = ContentOverlayUnbuildableRoot.Sorted(edits); NotInBase = notInBase; }
        public BuildContentCandidate Root { get; }
        /// <summary>その root の閉包（手元の閉包か、閉包の記録の閉包）に当たった編集。</summary>
        public IReadOnlyList<string> Edits => _edits;
        /// <summary>閉包の記録がある base で、その root の集合に無い root（手元で足した root）。編集が無くても上書きに入る。</summary>
        public bool NotInBase { get; }
    }

    /// <summary>
    /// 手元の編集から決めた上書きの計画（不変）。分類と理由の正本で、build の request（<see cref="BuildContentRequest(ContentOverlayPlan)"/>）と
    /// 前回の上書きの受け取り（<see cref="GetReusableResult"/>）はこの計画からだけ作る。
    /// </summary>
    /// <remarks>
    /// 作り直せない root が 1 つでもある計画は、その root の集合そのものを <see cref="Acknowledge"/> で認めた計画でなければ、
    /// request も前回の上書きも得られない。認めた集合は計画ごとに照合するので、作り直せない root が増えた計画に前の認めは流用できない。
    /// </remarks>
    public sealed class ContentOverlayPlan
    {
        private readonly ReadOnlyCollection<ContentOverlayEdit> _edits;
        private readonly ReadOnlyCollection<ContentOverlayPlannedRoot> _overlay;
        private readonly ReadOnlyCollection<ContentOverlaySuppliedRoot> _supplied;
        private readonly ReadOnlyCollection<ContentOverlayUnbuildableRoot> _unbuildable;
        private readonly ReadOnlyCollection<ContentOverlayEdit> _unmatched;
        private readonly ReadOnlyCollection<string> _shared;

        internal ContentOverlayPlan(ContentOverlayPlanInput input, ContentOverlayBaseIdentity baseIdentity,
            IEnumerable<ContentOverlayEdit> edits, IEnumerable<ContentOverlayPlannedRoot> overlay, IEnumerable<ContentOverlaySuppliedRoot> supplied,
            IEnumerable<ContentOverlayUnbuildableRoot> unbuildable, IEnumerable<ContentOverlayEdit> unmatched,
            BuildPlan? overlayPlan, ContentOverlaySelectionResult? selection, IReadOnlyDictionary<string, string?> planHashes, string? key)
        {
            Input = input;
            BaseIdentity = baseIdentity;
            _edits = Array.AsReadOnly(edits.OrderBy(x => x.Path, StringComparer.Ordinal).ThenBy(x => x.Kind).ToArray());
            _overlay = Array.AsReadOnly(overlay.OrderBy(x => x.Root.StableKey, StringComparer.Ordinal).ToArray());
            _supplied = Array.AsReadOnly(supplied.OrderBy(x => x.Root.SortKey, StringComparer.Ordinal).ToArray());
            _unbuildable = Array.AsReadOnly(unbuildable.OrderBy(x => x.Root.SortKey, StringComparer.Ordinal).ToArray());
            _unmatched = Array.AsReadOnly(unmatched.OrderBy(x => x.Path, StringComparer.Ordinal).ThenBy(x => x.Kind).ToArray());
            _shared = Array.AsReadOnly((selection?.SharedAssets ?? Array.Empty<string>()).ToArray());
            OverlayPlan = overlayPlan;
            Selection = selection;
            PlanHashes = planHashes;
            Key = key;
            State = _overlay.Count != 0 ? ContentOverlayPlanState.BuildRequired
                : _unbuildable.Count != 0 ? ContentOverlayPlanState.UnbuildableOnly : ContentOverlayPlanState.NotRequired;
        }

        private ContentOverlayPlan(ContentOverlayPlan source)
        {
            Input = source.Input; BaseIdentity = source.BaseIdentity; _edits = source._edits; _overlay = source._overlay;
            _supplied = source._supplied; _unbuildable = source._unbuildable; _unmatched = source._unmatched; _shared = source._shared;
            OverlayPlan = source.OverlayPlan; Selection = source.Selection; PlanHashes = source.PlanHashes; Key = source.Key;
            State = source.State; IsAcknowledged = true;
        }

        public ContentOverlayPlanState State { get; }
        /// <summary>見つけた編集の全て（path、種類の順）。</summary>
        public IReadOnlyList<ContentOverlayEdit> Edits => _edits;
        /// <summary>上書きに入れる root（stable key の ordinal 順）。</summary>
        public IReadOnlyList<ContentOverlayPlannedRoot> OverlayRoots => _overlay;
        /// <summary>他の供給元が届ける root。どの状態でも残る。</summary>
        public IReadOnlyList<ContentOverlaySuppliedRoot> Supplied => _supplied;
        /// <summary>作り直せない root。どの root にも上書きの版が無く、base の entry から載る。</summary>
        public IReadOnlyList<ContentOverlayUnbuildableRoot> Unbuildable => _unbuildable;
        /// <summary>どの root の閉包にも当たらなかった編集。状態を変えない。</summary>
        public IReadOnlyList<ContentOverlayEdit> UnmatchedEdits => _unmatched;
        /// <summary>上書きの root の閉包と、上書きに入らなかった selected の root の閉包の共通部分（登録契約の実体の重複の材料）。</summary>
        public IReadOnlyList<string> SharedAssets => _shared;
        /// <summary>上書きを重ねる base の識別（install の結果と照合した manifest から取った値）。</summary>
        public ContentOverlayBaseIdentity BaseIdentity { get; }
        /// <summary>上書きの入力の鍵（SHA-256）。<see cref="ContentOverlayPlanState.BuildRequired"/> の時だけ値がある。</summary>
        public string? Key { get; }
        /// <summary><see cref="Acknowledge"/> で作り直せない root の集合を認めた計画か。</summary>
        public bool IsAcknowledged { get; }

        internal ContentOverlayPlanInput Input { get; }
        internal BuildPlan? OverlayPlan { get; }
        internal ContentOverlaySelectionResult? Selection { get; }
        // 計画の時に hash した上書きの閉包の file と .meta（path → SHA-256、null は無い file）。build の後に取り直した値と比べる。
        internal IReadOnlyDictionary<string, string?> PlanHashes { get; }
        internal BuildMaterializationSnapshot Snapshot => Input.Snapshot;
        internal string OverlayContentSet => Input.OverlayContentSet;
        internal string ArtifactsRoot => Input.ArtifactsRoot;
        internal BuildPurposeDeclaration? Declaration => Input.Declaration;
        internal IReadOnlyList<BuildPurposeRequirement>? Requirements => Input.Requirements;

        /// <summary>
        /// 作り直せない root の集合を認めた計画を返す。<paramref name="roots"/> は、この計画の <see cref="Unbuildable"/> の root と
        /// 集合として完全に一致しなければならない（前の計画の集合や部分集合は受け付けない）。
        /// </summary>
        /// <exception cref="ArgumentException">集合が一致しない。</exception>
        public ContentOverlayPlan Acknowledge(IEnumerable<ContentOverlayRootIdentity> roots)
        {
            if (roots == null) throw new ArgumentNullException(nameof(roots));
            var given = new HashSet<ContentOverlayRootIdentity>();
            foreach (var root in roots)
            {
                if (root == null) throw new ArgumentException("Acknowledged roots must not contain null.", nameof(roots));
                given.Add(root);
            }
            if (!given.SetEquals(_unbuildable.Select(x => x.Root)))
                throw new ArgumentException("The acknowledged roots must equal this plan's unbuildable roots (" + _unbuildable.Count + ").", nameof(roots));
            return new ContentOverlayPlan(this);
        }

        /// <summary>
        /// 同じ鍵で作った前回の上書きを返す。前回の outcome が成功で、記録した成果物の file が全て同じ内容で残る時だけ返し、
        /// それ以外（記録が無い・読めない、鍵が違う、file が欠けた・書き換わった）は null（呼び手は build する）。
        /// 返す結果の preflight は前回の build の記録で、今回の分類と理由の正本はこの計画である。
        /// </summary>
        /// <exception cref="InvalidOperationException">build が要らない計画、または作り直せない root を認めていない計画。</exception>
        public BuildContentResult? GetReusableResult()
        {
            RequireBuildable();
            return ContentOverlayReuse.TryGet(ArtifactsRoot, OverlayContentSet, Key!);
        }

        // request と再利用の門。report も成果物も作る前に止める。
        internal void RequireBuildable()
        {
            if (State != ContentOverlayPlanState.BuildRequired)
                throw new InvalidOperationException("The plan needs no build (state " + State + "); there is no overlay to build or reuse.");
            if (_unbuildable.Count != 0 && !IsAcknowledged)
                throw new InvalidOperationException("The plan has " + _unbuildable.Count +
                                                    " unbuildable root(s); acknowledge exactly that set before building or reusing an overlay.");
        }
    }

    // 計画の入力（呼び手の値の写し）。request は計画からだけこれらを取る。
    internal sealed class ContentOverlayPlanInput
    {
        internal ContentOverlayPlanInput(BuildPlan plan, BuildMaterializationSnapshot snapshot, IEnumerable<string> suppliedSceneKeys,
            string overlayContentSet, string artifactsRoot, BuildPurposeDeclaration? declaration, IReadOnlyList<BuildPurposeRequirement>? requirements)
        {
            Plan = plan; Snapshot = snapshot; OverlayContentSet = overlayContentSet; ArtifactsRoot = artifactsRoot;
            Declaration = declaration; Requirements = requirements?.ToArray();
            SuppliedSceneKeys = new HashSet<string>(suppliedSceneKeys, StringComparer.Ordinal);
        }
        public BuildPlan Plan { get; }
        public BuildMaterializationSnapshot Snapshot { get; }
        public ISet<string> SuppliedSceneKeys { get; }
        public string OverlayContentSet { get; }
        public string ArtifactsRoot { get; }
        public BuildPurposeDeclaration? Declaration { get; }
        public IReadOnlyList<BuildPurposeRequirement>? Requirements { get; }
    }
}
