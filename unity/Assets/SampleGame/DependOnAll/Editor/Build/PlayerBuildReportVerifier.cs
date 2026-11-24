#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Build.Reporting;

namespace SampleGame.DependOnAll.Editor.Build
{
    internal sealed class PlayerBuildVerification
    {
        internal PlayerBuildVerification(string buildGuid, string[] checkedGuids, string[] files, PlayablePackVerification? playable = null)
        { BuildGuid = buildGuid; CheckedGuids = checkedGuids; Files = files; Playable = playable; }
        internal string BuildGuid { get; } internal string[] CheckedGuids { get; } internal string[] Files { get; }
        /// <summary>遊べる Player の時だけ。足した検査の数（判定の record 用）。</summary>
        internal PlayablePackVerification? Playable { get; }
    }
    internal sealed class PlayerPackedAssetData
    {
        internal PlayerPackedAssetData(string packName, IEnumerable<string> guids, IEnumerable<string>? sourcePaths = null)
        { PackName = packName; Guids = guids.ToArray(); SourcePaths = sourcePaths?.ToArray() ?? Array.Empty<string>(); }
        internal string PackName { get; }
        internal string[] Guids { get; }
        /// <summary>pack の各行の元の asset の path（build report の sourceAssetPath）。遊べる Player の写しの検査が読む。</summary>
        internal string[] SourcePaths { get; }
    }

    /// <summary>
    /// 遊べる Player（bootstrap を Player に写さない build）にだけ足す検査の入力。
    /// 写しは新しい GUID を持つので元の GUID の検査では見つからない。そこで「入ってはいけない元の GUID」と「mutation の root の下の path」の
    /// 2 つの向きで、UICommon・map・SceneResource とその写しが Player に入っていないことを見る。
    /// </summary>
    internal sealed class PlayablePackChecks
    {
        internal PlayablePackChecks(IEnumerable<string> forbiddenGuids, string mutationRoot, string startSceneGuid)
        {
            ForbiddenGuids = forbiddenGuids.Where(x => !string.IsNullOrEmpty(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            MutationRoot = mutationRoot ?? throw new ArgumentNullException(nameof(mutationRoot));
            StartSceneGuid = startSceneGuid ?? throw new ArgumentNullException(nameof(startSceneGuid));
        }
        /// <summary>Player に入ってはいけない GUID（map が参照する各 SceneResource）。元の UICommon の Scene と map は今の検査が見る。</summary>
        internal string[] ForbiddenGuids { get; }
        /// <summary>mutation の root（<c>Assets/Resources/BS4/&lt;identity&gt;</c>）。この下の物は pack に入ってはいけない。</summary>
        internal string MutationRoot { get; }
        internal string StartSceneGuid { get; }
    }

    /// <summary>遊べる Player の検査の観測（判定の record 用）。</summary>
    internal sealed class PlayablePackVerification
    {
        internal PlayablePackVerification(int forbiddenGuidCount, int startScenePackCount)
        { ForbiddenGuidCount = forbiddenGuidCount; StartScenePackCount = startScenePackCount; }
        internal int ForbiddenGuidCount { get; }
        /// <summary>開始 Scene の GUID を持つ pack の数（0 か 1）。</summary>
        internal int StartScenePackCount { get; }
    }

    internal static class PlayerBuildReportVerifier
    {
        internal static PlayerBuildVerification Verify(BuildReport report, IEnumerable<string> selectedRootGuids, string sourceBootstrapGuid, string generatedBootstrapGuid)
        {
            var packs = PacksOf(report);
            var checkedGuids = VerifyPackedAssets(packs, selectedRootGuids, sourceBootstrapGuid, generatedBootstrapGuid);
            return new PlayerBuildVerification(report.summary.guid.ToString(), checkedGuids, FilesOf(report));
        }

        /// <summary>遊べる Player の build report の検査。今の検査（selected の root の重複、元の UICommon の Scene）に D6 の (a)(b)(c) を足す。</summary>
        internal static PlayerBuildVerification VerifyPlayable(BuildReport report, IEnumerable<string> selectedRootGuids, string sourceBootstrapGuid, PlayablePackChecks checks)
        {
            var packs = PacksOf(report);
            var (checkedGuids, playable) = VerifyPlayablePackedAssets(packs, selectedRootGuids, sourceBootstrapGuid, checks);
            return new PlayerBuildVerification(report.summary.guid.ToString(), checkedGuids, FilesOf(report), playable);
        }

        private static PlayerPackedAssetData[] PacksOf(BuildReport report)
        {
            if (report == null || report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Player build did not succeed.");
            if (report.packedAssets == null || report.packedAssets.Length == 0)
                throw new InvalidOperationException("Detailed packed asset data is required.");
            return report.packedAssets.Select(pack => new PlayerPackedAssetData(pack.shortPath,
                pack.contents.Select(content => content.sourceAssetGUID.ToString()),
                pack.contents.Select(content => content.sourceAssetPath ?? string.Empty))).ToArray();
        }

        private static string[] FilesOf(BuildReport report)
        {
            var files = report.GetFiles().Select(x => x.path).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            if (files.Length == 0) throw new InvalidOperationException("Player report contains no files.");
            return files;
        }

        internal static string[] VerifyPackedAssets(
            IEnumerable<PlayerPackedAssetData> packs,
            IEnumerable<string> selectedRootGuids,
            string sourceBootstrapGuid,
            string generatedBootstrapGuid)
        {
            var (selected, packedData) = VerifyCommon(packs, selectedRootGuids, sourceBootstrapGuid);
            // Detailed report は Scene 内 object ごとに同じ source Scene GUID を複数行返す。
            // GUID 行数ではなく格納先 pack 数を数え、Scene 入力と Resources の二重格納を区別する。
            var generatedPacks = PacksContaining(packedData, generatedBootstrapGuid);
            if (generatedPacks.Length != 1)
                throw new InvalidOperationException("Generated bootstrap Scene must belong to exactly one Player pack; packs=" + string.Join(",", generatedPacks) + ".");
            return selected;
        }

        internal static (string[] CheckedGuids, PlayablePackVerification Playable) VerifyPlayablePackedAssets(
            IEnumerable<PlayerPackedAssetData> packs,
            IEnumerable<string> selectedRootGuids,
            string sourceBootstrapGuid,
            PlayablePackChecks checks)
        {
            if (checks == null) throw new ArgumentNullException(nameof(checks));
            var (selected, packedData) = VerifyCommon(packs, selectedRootGuids, sourceBootstrapGuid);

            // (a) 入ってはいけない GUID。0 件は検査が空振りするので build の失敗にする（map の参照を集め損ねた時に通さない）。
            if (checks.ForbiddenGuids.Length == 0)
                throw new InvalidOperationException("Playable Player build has no SceneResource GUIDs to check; the check would be empty.");
            var packed = new HashSet<string>(packedData.SelectMany(x => x.Guids).Where(x => !string.IsNullOrEmpty(x)), StringComparer.OrdinalIgnoreCase);
            var forbidden = checks.ForbiddenGuids.Where(packed.Contains).ToArray();
            if (forbidden.Length != 0)
                throw new InvalidOperationException("SceneResources were packed into the playable Player: " + string.Join(",", forbidden));

            // (b) mutation の root の下の物（UICommon・map・SceneResource の写しが置かれる場所）。写しは新しい GUID なので path で見る。
            var rootPrefix = checks.MutationRoot.TrimEnd('/') + "/";
            var underRoot = packedData.SelectMany(x => x.SourcePaths)
                .Where(path => path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (underRoot.Length != 0)
                throw new InvalidOperationException("Assets under the Player build mutation root were packed into the playable Player: " + string.Join(",", underRoot));

            // (c) 開始 Scene は 1 つ以下の pack。空の Scene の pack が Scene の GUID の行を持つかは測っていないので 0 を許す。
            var startPacks = PacksContaining(packedData, checks.StartSceneGuid);
            if (startPacks.Length > 1)
                throw new InvalidOperationException("Playable start Scene must belong to at most one Player pack; packs=" + string.Join(",", startPacks) + ".");

            return (selected, new PlayablePackVerification(checks.ForbiddenGuids.Length, startPacks.Length));
        }

        private static (string[] Selected, PlayerPackedAssetData[] Packs) VerifyCommon(
            IEnumerable<PlayerPackedAssetData> packs, IEnumerable<string> selectedRootGuids, string sourceBootstrapGuid)
        {
            var selected = new HashSet<string>(selectedRootGuids.Where(x => !string.IsNullOrEmpty(x)), StringComparer.OrdinalIgnoreCase);
            var packedData = packs.ToArray();
            if (selected.Count == 0 || packedData.Length == 0)
                throw new InvalidOperationException("Detailed packed asset data is required.");
            var packed = new HashSet<string>(packedData.SelectMany(x => x.Guids).Where(x => !string.IsNullOrEmpty(x)), StringComparer.OrdinalIgnoreCase);
            var duplicates = selected.Where(packed.Contains).ToArray();
            if (duplicates.Length != 0) throw new InvalidOperationException("Selected content roots were duplicated into the Player: " + string.Join(",", duplicates));
            if (packed.Contains(sourceBootstrapGuid)) throw new InvalidOperationException("Production bootstrap Scene was packed into the Player.");
            return (selected.OrderBy(x => x, StringComparer.Ordinal).ToArray(), packedData);
        }

        private static string[] PacksContaining(IEnumerable<PlayerPackedAssetData> packs, string guid) => packs
            .Where(pack => pack.Guids.Any(x => string.Equals(x, guid, StringComparison.OrdinalIgnoreCase)))
            .Select(pack => pack.PackName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
