#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Materialization;
using OneStarMaker.Runtime.BuildContent.Distribution;
using UnityEngine;

namespace OneStarMaker.Editor.Build.Content
{
    /// <summary>
    /// install した base の manifest と手元の file だけから、開発者の入力なしに上書きの計画を決める入口。
    /// 編集を検出し、root を「上書きに入れる」「他の供給元が届ける」「作り直せない」に分け、上書きの入力の鍵を計算する。
    /// build を起こさず、file を書かない（利用者の source、installed cache、base の directory を含む）。
    /// </summary>
    /// <remarks>
    /// 呼び手の順は「欠けを許す materialize（<see cref="SceneResourceContentMaterializer.MaterializeAllowingAbsentSources"/>）→ 選択 →
    /// 合成 → <see cref="Plan"/>」。合成は欠けの記録（<see cref="BuildMaterializationSnapshot.Absences"/>）を引き継ぐ。引き継がない合成は、
    /// 閉包の無い selected の root で例外になる。計画は disk の file の内容で決めるので、保存していない編集を file に書き、
    /// import を済ませる（<c>AssetDatabase.Refresh</c>）のは呼び手の前提である。計画から build が終わるまで source を変えないことも前提で、
    /// build は Unity の後に閉包の file を hash し直し、計画の時と違えば失敗にする。
    /// 閉包が持たない依存（script、<c>Packages/</c>、import 時の依存）、削除（消した asset は未 checkout と区別できず source の欠けになる）、
    /// 記録の無い base で依存の file だけが欠けて Unity がその path を返さない root は、検出に現れない。
    /// </remarks>
    public static class ContentOverlayPlanner
    {
        private const string SceneKind = "Scene";
        private const string ObjectKind = "Object";

        /// <param name="baseInstall">上書きを重ねる base（install と検証を終えた revision）。その <c>transport.json</c> を 1 回読む。</param>
        /// <param name="plan">宣言から作った手元の選択の成功 plan（合成の後）。</param>
        /// <param name="snapshot"><paramref name="plan"/> と対の snapshot（欠けを許す materialize の結果を合成した物）。</param>
        /// <param name="suppliedSceneKeys">他の供給元（開いている Scene を届ける snapshot など）が届ける Scene の node の logical key。</param>
        /// <param name="overlayContentSet">上書きの contentSet（base と別の名前）。</param>
        /// <param name="artifactsRoot">上書きを build する成果物の root。再利用の記録もこの下に置く。</param>
        /// <param name="declaration">base と同じ宣言（合成だけの build では null）。</param>
        /// <param name="requirements">到達経路（preflight の記録専用）。</param>
        /// <exception cref="InvalidOperationException">
        /// base の manifest が上限を超える・digest が install と違う・JSON として読めない、contentSet・revision・target が install と違う。
        /// 閉包の記録の「壊れている」には読み替えない（<c>sourceFiles</c> も使わない）。
        /// </exception>
        /// <exception cref="ArgumentException">selected の root に閉包も欠けの記録も無い（欠けの記録を落とした合成など）。</exception>
        public static ContentOverlayPlan Plan(ContentInstallResult baseInstall, BuildPlan plan, BuildMaterializationSnapshot snapshot,
            IEnumerable<string> suppliedSceneKeys, string overlayContentSet, string artifactsRoot,
            BuildPurposeDeclaration? declaration, IReadOnlyList<BuildPurposeRequirement>? requirements)
        {
            if (baseInstall == null) throw new ArgumentNullException(nameof(baseInstall));
            var bytes = File.ReadAllBytes(Path.Combine(baseInstall.RevisionRoot, "transport.json"));
            return Plan(bytes, baseInstall.ManifestSha256, baseInstall.ContentSet, baseInstall.Revision, baseInstall.Target,
                Input(plan, snapshot, suppliedSceneKeys, overlayContentSet, artifactsRoot, declaration, requirements),
                new UnityContentOverlayLocalSource());
        }

        internal static ContentOverlayPlanInput Input(BuildPlan plan, BuildMaterializationSnapshot snapshot, IEnumerable<string> suppliedSceneKeys,
            string overlayContentSet, string artifactsRoot, BuildPurposeDeclaration? declaration, IReadOnlyList<BuildPurposeRequirement>? requirements)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (suppliedSceneKeys == null) throw new ArgumentNullException(nameof(suppliedSceneKeys));
            if (string.IsNullOrWhiteSpace(overlayContentSet)) throw new ArgumentException("An overlay content set is required.", nameof(overlayContentSet));
            if (string.IsNullOrWhiteSpace(artifactsRoot)) throw new ArgumentException("An artifacts root is required.", nameof(artifactsRoot));
            return new ContentOverlayPlanInput(plan, snapshot, suppliedSceneKeys, overlayContentSet, artifactsRoot, declaration, requirements);
        }

        internal static ContentOverlayPlan Plan(byte[] manifestBytes, string digest, string contentSet, string revision, string target,
            ContentOverlayPlanInput input, IContentOverlayLocalSource local)
        {
            var manifest = ReadBase(manifestBytes, digest, contentSet, revision, target);
            var baseIdentity = new ContentOverlayBaseIdentity(contentSet, revision, target, digest,
                string.IsNullOrEmpty(manifest.declaration?.name) ? null : manifest.declaration!.name);
            var record = ContentSourceClosureRecord.Read(manifest);
            var roots = LocalRoots(input, record);
            var detection = ContentOverlayEditDetection.Detect(manifest, record, roots, local);
            return Classify(input, baseIdentity, record, roots, detection, local);
        }

        // base の manifest を照合してから読む。照合に落ちた bytes は記録の「壊れている」に読み替えず、計画ごと止める
        // （記録だけが壊れていると読むと sourceFiles で比べ始め、install と違う base の値で編集を決めてしまう）。
        private static ContentTransportManifest ReadBase(byte[] bytes, string digest, string contentSet, string revision, string target)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            // 上限・digest・UTF-8 の JSON の object かは、選択の読み口と同じ規則で見る（install と同じ上限）。
            var check = ContentPurposeIndex.Read(bytes, digest ?? "");
            if (check.State == ContentPurposeIndexState.Invalid)
                throw new InvalidOperationException("The base manifest does not match the install (" + check.Failure + "): " + check.Detail);
            ContentTransportManifest? manifest;
            try { manifest = JsonUtility.FromJson<ContentTransportManifest>(new System.Text.UTF8Encoding(false, true).GetString(bytes)); }
            catch (Exception ex) { throw new InvalidOperationException("The base manifest cannot be read: " + ex.Message, ex); }
            if (manifest == null) throw new InvalidOperationException("The base manifest cannot be read.");
            if (!string.Equals(manifest.contentSet, contentSet, StringComparison.Ordinal) || !string.Equals(manifest.revision, revision, StringComparison.Ordinal)
                || !string.Equals(manifest.target, target, StringComparison.Ordinal))
                throw new InvalidOperationException("The base manifest identity does not match the install result.");
            return manifest;
        }

        // 手元の選択の selected の root と、その閉包か欠けの記録、閉包の記録の対応する root。
        private static IReadOnlyList<LocalRoot> LocalRoots(ContentOverlayPlanInput input, ContentSourceClosureReadResult record)
        {
            var snapshot = input.Snapshot;
            var result = new List<LocalRoot>();
            foreach (var candidate in input.Plan.SelectedContent.OrderBy(x => x.StableKey, StringComparer.Ordinal))
            {
                var closures = snapshot.Dependencies.Where(x => string.Equals(x.RootGuid, candidate.PhysicalKey, StringComparison.Ordinal)).ToArray();
                var absences = snapshot.Absences.Where(x => string.Equals(x.StableKey, candidate.StableKey, StringComparison.Ordinal)).ToArray();
                if (closures.Length + absences.Length != 1)
                    throw new ArgumentException("Selected candidate '" + candidate.StableKey + "' has " + closures.Length + " closures and " + absences.Length +
                                                " absence records; pass the snapshot that pairs with the plan and keeps the absence records.", nameof(input));
                var closure = closures.SingleOrDefault();
                var absence = absences.SingleOrDefault();
                var tags = snapshot.TagProviders.SelectMany(x => x.GetTags(candidate))
                    .Where(x => x.Dimension == BuildRepresentationSelection.Dimension).Select(x => x.Value).Distinct(StringComparer.Ordinal).ToArray();
                var representation = tags.Length == 1 ? tags[0] ?? "" : "";
                var path = closure?.RootPath ?? absence?.RootPath;
                // 表現が複数ある node で別の表現の閉包を当てないよう、記録の root は logical key・表現・root の GUID で引く。
                var matches = record.Roots.Where(x => string.Equals(x.Guid, candidate.PhysicalKey, StringComparison.Ordinal)
                    && string.Equals(x.LogicalKey, candidate.LogicalKey, StringComparison.Ordinal)
                    && string.Equals(x.Representation, representation, StringComparison.Ordinal)
                    && (path == null || string.Equals(x.Kind, KindOf(path), StringComparison.Ordinal))).ToArray();
                var matched = matches.Length == 1 ? matches[0] : null;
                // root の path が手元で分からない（GUID が解決しない）時、kind は記録から取る。記録にも無ければ不明（空）のまま。
                var kind = path != null ? KindOf(path) : matched?.Kind ?? "";
                result.Add(new LocalRoot(candidate, new ContentOverlayRootIdentity(candidate.LogicalKey, kind, representation, candidate.PhysicalKey,
                    candidate.StableKey), closure, absence, matched));
            }
            return result;
        }

        private static ContentOverlayPlan Classify(ContentOverlayPlanInput input, ContentOverlayBaseIdentity baseIdentity,
            ContentSourceClosureReadResult record, IReadOnlyList<LocalRoot> roots, ContentOverlayEditDetection detection, IContentOverlayLocalSource local)
        {
            var present = record.State == ContentSourceClosureState.Present;
            var overlay = new List<(LocalRoot Root, List<ContentOverlayEdit> Edits, bool NotInBase)>();
            var supplied = new List<ContentOverlaySuppliedRoot>();
            var unbuildable = new List<ContentOverlayUnbuildableRoot>();
            var hit = new HashSet<ContentOverlayEdit>();
            var matchedRecordRoots = new HashSet<ContentSourceClosureRootRecord>();

            foreach (var root in roots)
            {
                if (root.Record != null) matchedRecordRoots.Add(root.Record);
                var edits = detection.Edits.Where(x => detection.HitsLocal(x, root.Closure) || detection.HitsRecord(x, root.Record)).ToList();
                var unresolved = root.Record?.Assets.Where(x => detection.Unresolved.Contains(x.Guid)).Select(x => x.Path).ToArray() ?? Array.Empty<string>();
                var sourceAbsent = root.Absence != null || unresolved.Length != 0;
                var rootFilePresent = root.Absence == null || root.Absence.Reason == BuildMaterializationAbsenceReason.DependencyMissing;
                // 記録が「あり」で集合に無い root は手元で足した root で、編集が無くても上書きに入れる（source が欠ければ作り直せない）。
                // 記録が無い・未対応・壊れている base では、欠けた selected の root を、編集が 0 件に見えても全て影響ありとする
                // （記録の閉包が無いので、欠けた file に当たる編集を見分けられない）。
                var notInBase = present && root.Record == null;
                var affected = edits.Count != 0 || notInBase || (!present && sourceAbsent);
                if (!affected) continue;
                hit.UnionWith(edits);
                var paths = edits.Select(x => x.Path);
                var missing = (root.Absence?.MissingPaths ?? Array.Empty<string>()).Concat(unresolved);
                if (!rootFilePresent)
                    unbuildable.Add(new ContentOverlayUnbuildableRoot(root.Identity, Reason(record.State, root.Record != null), paths, missing));
                // 開いている Scene は依存が欠けていても snapshot が手元の版で届けるので、root の file があれば他の供給元に入れる。
                else if (root.Identity.Kind == SceneKind && input.SuppliedSceneKeys.Contains(root.Identity.LogicalKey))
                    supplied.Add(new ContentOverlaySuppliedRoot(root.Identity, paths));
                else if (sourceAbsent)
                    unbuildable.Add(new ContentOverlayUnbuildableRoot(root.Identity, Reason(record.State, root.Record != null), paths, missing));
                else overlay.Add((root, edits, notInBase));
            }

            // 閉包の記録にあって手元の選択に無い root（手元の map が古い、選択の規則が違う）。記録の閉包に編集を含めば黙って base の版で
            // 動くので、作り直せない root にする。
            foreach (var recordRoot in record.Roots.Where(x => !matchedRecordRoots.Contains(x)))
            {
                var edits = detection.Edits.Where(x => detection.HitsRecord(x, recordRoot)).ToList();
                if (edits.Count == 0) continue;
                hit.UnionWith(edits);
                var identity = new ContentOverlayRootIdentity(recordRoot.LogicalKey, recordRoot.Kind, recordRoot.Representation, recordRoot.Guid, null);
                if (recordRoot.Kind == SceneKind && local.ResolveGuid(recordRoot.Guid) != null && input.SuppliedSceneKeys.Contains(recordRoot.LogicalKey))
                    supplied.Add(new ContentOverlaySuppliedRoot(identity, edits.Select(x => x.Path)));
                else
                    unbuildable.Add(new ContentOverlayUnbuildableRoot(identity, ContentOverlayUnbuildableReason.NotInLocalSelection,
                        edits.Select(x => x.Path), recordRoot.Assets.Where(x => detection.Unresolved.Contains(x.Guid)).Select(x => x.Path)));
            }
            var unmatched = detection.Edits.Where(x => !hit.Contains(x)).ToArray();

            if (overlay.Count == 0)
                return new ContentOverlayPlan(input, baseIdentity, detection.Edits, Array.Empty<ContentOverlayPlannedRoot>(), supplied, unbuildable, unmatched,
                    null, null, new SortedDictionary<string, string?>(StringComparer.Ordinal), null);

            var overlayPlan = input.Plan.WithSelectedSubset(overlay.Select(x => x.Root.Candidate.StableKey));
            var scope = roots.Where(x => x.Closure != null).Select(x => x.Candidate).ToArray();
            var selection = ContentOverlaySelection.Result(overlayPlan, input.Snapshot, detection.Edits.Select(x => x.Path),
                overlay.Select(x => new ContentOverlayRootReason(x.Root.Candidate, x.Edits.Select(e => e.Path).Distinct(StringComparer.Ordinal))),
                unmatched.Select(x => new ContentOverlayUnmatchedEdit(x.Path, ContentOverlayUnmatchedReason.NotInAnyClosure)), scope);
            var closureEntries = overlay.SelectMany(x => x.Root.Closure!.Entries).GroupBy(x => x.Path, StringComparer.Ordinal).Select(x => x.First())
                .OrderBy(x => x.Path, StringComparer.Ordinal).ToArray();
            var hashes = ContentSourceHashes.Capture(closureEntries.Select(x => x.Path), local.Sha256);
            var key = ContentOverlayReuse.ComputeKey(baseIdentity, input.OverlayContentSet, BuildContentCoordinator.TargetName, Application.unityVersion,
                input.Declaration, overlay.Select(x => (x.Root.Candidate, x.Root.Identity.Representation, x.Root.Closure!.RootPath)), closureEntries, hashes);
            return new ContentOverlayPlan(input, baseIdentity, detection.Edits,
                overlay.Select(x => new ContentOverlayPlannedRoot(x.Root.Candidate, x.Edits.Select(e => e.Path), x.NotInBase)),
                supplied, unbuildable, unmatched, overlayPlan, selection, hashes, key);
        }

        private static ContentOverlayUnbuildableReason Reason(ContentSourceClosureState state, bool inRecord) => state switch
        {
            ContentSourceClosureState.Present => inRecord ? ContentOverlayUnbuildableReason.SourceAbsent : ContentOverlayUnbuildableReason.NotInBase,
            ContentSourceClosureState.Missing => ContentOverlayUnbuildableReason.RecordMissing,
            ContentSourceClosureState.Unsupported => ContentOverlayUnbuildableReason.RecordUnsupported,
            _ => ContentOverlayUnbuildableReason.RecordInvalid,
        };

        // directory の entry と同じ規則（root の path の拡張子）。
        private static string KindOf(string path) => path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ? SceneKind : ObjectKind;

        internal sealed class LocalRoot
        {
            internal LocalRoot(BuildContentCandidate candidate, ContentOverlayRootIdentity identity, BuildDependencySnapshot? closure,
                BuildMaterializationAbsence? absence, ContentSourceClosureRootRecord? record)
            { Candidate = candidate; Identity = identity; Closure = closure; Absence = absence; Record = record; }
            public BuildContentCandidate Candidate { get; }
            public ContentOverlayRootIdentity Identity { get; }
            public BuildDependencySnapshot? Closure { get; }
            public BuildMaterializationAbsence? Absence { get; }
            public ContentSourceClosureRootRecord? Record { get; }
        }
    }
}
