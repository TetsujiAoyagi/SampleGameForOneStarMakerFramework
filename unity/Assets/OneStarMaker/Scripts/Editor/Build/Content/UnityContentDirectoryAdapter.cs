#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using OneStarMaker.Runtime.BuildContent;
using OneStarMaker.Runtime.AssetManagement;
using Unity.Loading;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OneStarMaker.Editor.Build.Content
{
    // Unity の Content Directory build の 1 回の結果。BuildReport の解釈は既定の呼び出しの中に閉じる。
    internal readonly struct UnityContentBuildResult
    {
        public UnityContentBuildResult(bool succeeded, string summary) { Succeeded = succeeded; Summary = summary; }
        public bool Succeeded { get; }
        public string Summary { get; }
    }

    // projection の root 1 件が directory の BuildContentRoot に書かれる entry の (logical key, kind, 表現)。
    // adapter が entry を作る時と、coordinator が publish の索引のために outcome へ記録する時の両方がこれを使う。
    // 2 つの計算に分けると、索引が「含む」と答えた要求をその directory で解決できない、という食い違いが起きうる。
    // 表現は projection の生の値（Representation の tag の無い root は空のまま）で、Full への読み替えはしない。
    internal readonly struct BuildContentRootKey
    {
        internal BuildContentRootKey(string logicalKey, BuildContentKind kind, string representation)
        { LogicalKey = logicalKey; Kind = kind; Representation = representation; }
        internal string LogicalKey { get; }
        internal BuildContentKind Kind { get; }
        internal string Representation { get; }
    }

    internal static class BuildContentRootKeys
    {
        // kind は root の path の拡張子で決まる（Scene は Unity の loadable scene ID、それ以外は Object ID で entry を作る）。
        internal static BuildContentRootKey Describe(ProjectedContent root)
            => new(root.Candidate.LogicalKey,
                root.Path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ? BuildContentKind.Scene : BuildContentKind.Object,
                root.Representation);
    }

    // Unity Editor API に依存する唯一の BS2b build adapter。
    // 一時的な BuildContentRoot asset を作って Content Directory に含め、build 後は生成元だけを消す。
    // 利用者の source（Scene の file と .meta を含む）は書かない。Scene に焼き込まれる prefab・model の編集が
    // 同じ Library の差分 build に入ることは Unity（6000.6.3f1 以降）に依拠する（契約は 18 章 §4）。
    internal sealed class UnityContentDirectoryAdapter : IContentDirectoryAdapter
    {
        // この subtree は adapter が所有する一時 asset。既存の Game/Framework asset は消さない。
        private const string GeneratedRoot = "Assets/OneStarMakerGenerated/BS2b";
        private const string Marker = "bs2b-owner.marker";
        private readonly Func<BuildContentDirectoryParameters, UnityContentBuildResult> _build;

        public UnityContentDirectoryAdapter() : this(BuildWithUnity) { }

        // test の継ぎ目: Unity の build の呼び出し（成功・失敗・例外を返せる）。
        internal UnityContentDirectoryAdapter(Func<BuildContentDirectoryParameters, UnityContentBuildResult> build)
        {
            _build = build ?? throw new ArgumentNullException(nameof(build));
        }

        public void ValidateTarget()
        {
            // BS2b は固定 target の証拠に限定する。自動切替は Library の状態や後続 build を変えるため行わない。
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64 ||
                EditorUserBuildSettings.standaloneBuildSubtarget != StandaloneBuildSubtarget.Player)
                throw new InvalidOperationException("BS2b requires active StandaloneWindows64 Player target.");
        }

        public ContentDirectoryBuild Build(BuildContentProjectionResult projection, string identity, string workspace,
            OneStarMaker.Build.Selection.BuildPurposeDeclaration? declaration)
        {
            ValidateTarget();
            // 前回の異常終了で残った所有物だけを掃除する。marker のない folder は触れない。
            CleanupOrphans();
            var folder = GeneratedRoot + "/" + identity;
            var markerPath = folder + "/" + Marker;
            var markerDisk = Path.Combine(Application.dataPath, "OneStarMakerGenerated", "BS2b", identity, Marker);
            Exception? failure = null;
            // この try は生成 folder の作成の前から始め、製品の Refresh・root の作成・build を囲む。
            // どこで例外が出ても、finally が生成物を消す。
            try
            {
                EnsureFolder(folder);
                File.WriteAllText(markerDisk, identity);
                AssetDatabase.Refresh();
                var entries = new List<BuildContentEntry>();
                foreach (var item in projection.Roots)
                {
                    // materialization 後に root の GUID/path が変わっていれば、別 asset を build しない。
                    // 依存閉包全体は再探索せず、選択 root の同一性だけを Editor 上で最終確認する。
                    if (AssetDatabase.AssetPathToGUID(item.Path) != item.Candidate.PhysicalKey)
                        throw new InvalidOperationException("Root GUID/path changed after materialization: " + item.Path);
                    var key = BuildContentRootKeys.Describe(item);
                    if (key.Kind == BuildContentKind.Scene)
                    {
                        // Scene は Unity の loadable scene ID を保存し、path/GUID 自体を Runtime の load ID にしない。
                        var sceneId = LoadableSceneIdEditorUtility.CreateLoadableSceneId(item.Path);
                        entries.Add(new BuildContentEntry(key.LogicalKey, item.Candidate.StableKey,
                            key.Representation, sceneId));
                    }
                    else
                    {
                        // Object ID は main asset の local file ID を含む Unity reference から作る。
                        // 現在の build 入力は main asset のみ。GUID と local file ID を同じ Object から取り、
                        // 将来 subasset を候補にしても file 単位の GUID だけで誤解決しない形で保存する。
                        var asset = AssetDatabase.LoadMainAssetAtPath(item.Path);
                        if (asset == null) throw new InvalidOperationException("Root asset missing: " + item.Path);
                        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var fileGuid, out long localFileId)
                            || string.IsNullOrWhiteSpace(fileGuid) || localFileId == 0)
                            throw new InvalidOperationException("Root asset has no stable file/object locator: " + item.Path);
                        var objectId = LoadableObjectIdEditorUtility.CreateLoadableObjectId(asset);
                        entries.Add(new BuildContentEntry(key.LogicalKey, item.Candidate.StableKey,
                            key.Representation, new Loadable<UnityEngine.Object>(objectId), fileGuid, localFileId,
                            ClassifyCategory(asset)));
                    }
                }
                var root = ScriptableObject.CreateInstance<BuildContentRoot>();
                // directory に同梱する単一 root。build identity と entry を同じ serialized asset に閉じ込める。
                // BS3 は登録後にこの root を一件だけ取得し、source を再走査せず対応を復元する。
                // 宣言は名前・表現の主値・runtime 既定値だけを写す。root が「何の目的で作られたか」を自分で持つための記録で、
                // Runtime は起動設定との不一致を Warning にするだけで、値の意味は解釈しない。
                var rootDeclaration = declaration == null ? null
                    : new BuildContentDeclaration(declaration.Name, declaration.Representation.Primary, declaration.RuntimeDefaults);
                root.Initialize(identity, BuildContentCoordinator.TargetName, entries, rootDeclaration);
                var rootPath = folder + "/BuildContentRoot.asset";
                AssetDatabase.CreateAsset(root, rootPath);
                EditorUtility.SetDirty(root);
                AssetDatabase.SaveAssetIfDirty(root);
                AssetDatabase.ImportAsset(rootPath, ImportAssetOptions.ForceUpdate);
                var loaded = AssetDatabase.LoadAssetAtPath<BuildContentRoot>(rootPath);
                if (loaded == null || loaded.BuildIdentity != identity || loaded.Entries.Count != entries.Count
                    || (rootDeclaration != null) != (loaded.Declaration != null)
                    || (rootDeclaration != null && loaded.Declaration!.Name != rootDeclaration.Name))
                    throw new InvalidOperationException("Generated root did not retain its serialized identities.");
                var parameters = new BuildContentDirectoryParameters {
                    // rootAssetPaths は生成 root のみ。そこから参照される Scene/Object を Unity が収集する。
                    // workspace は target/contentSet 固定なので、同じ場所への再 build が可能。
                    outputPath = workspace, rootAssetPaths = new[] { rootPath }, name = identity,
                    compression = BuildCompression.Uncompressed, options = BuildContentOptions.None,
                    extraScriptingDefines = Array.Empty<string>()
                };
                var built = _build(parameters);
                if (!built.Succeeded) throw new InvalidOperationException(built.Summary);
                var manifest = Path.Combine(workspace, "BuildManifestHash.txt");
                // Unity の内部 file 名や manifest 内容は OSM の公開 protocol として解釈しない。
                // pointer と metadata directory の存在だけを検証し、directory 一式を coordinator に渡す。
                if (!File.Exists(manifest)) throw new InvalidOperationException("Unity manifest pointer missing.");
                // Unity の previousBuildReportDirectories には content 出力 folder を渡せる。
                // summary.outputPath はその folder を指す保証がないため、検証済み workspace を記録する。
                var metadata = Path.GetFullPath(workspace);
                if (!Directory.Exists(metadata))
                    throw new InvalidOperationException("Unity content output directory missing: " + metadata);
                return new ContentDirectoryBuild(built.Summary, manifest, metadata);
            }
            catch (Exception ex)
            {
                failure = ex;
                throw;
            }
            finally
            {
                // 成否にかかわらず、この実行で生成した asset と空の親 folder だけを削除する。
                // published directory は Assets 外にあり、ここでは触れない。
                // 片付けの段は互いに止めない。1 段の例外で残りの削除を飛ばすと、source に build の物が残る。
                ExceptionDispatchInfo? cleanup = null;
                void Step(Action action)
                {
                    try { action(); }
                    catch (Exception ex) { cleanup ??= ExceptionDispatchInfo.Capture(ex); }
                }
                Step(() => { if (File.Exists(markerDisk)) AssetDatabase.DeleteAsset(markerPath); });
                Step(() => AssetDatabase.DeleteAsset(folder));
                Step(() => DeleteIfEmpty(GeneratedRoot));
                Step(() => DeleteIfEmpty("Assets/OneStarMakerGenerated"));
                // build の失敗を片付けの失敗で隠さない。片付けだけが失敗した build は、生成物が消えたと言えないので失敗にする。
                if (cleanup != null)
                {
                    if (failure == null) cleanup.Throw();
                    throw new InvalidOperationException(failure!.Message + " (cleanup also failed: " + cleanup.SourceException.Message + ")",
                        new AggregateException(failure, cleanup.SourceException));
                }
            }
        }

        // 既定の Unity の build の呼び出し。BuildReport の成否と要約だけを返す。
        private static UnityContentBuildResult BuildWithUnity(BuildContentDirectoryParameters parameters)
        {
            var report = BuildPipeline.BuildContentDirectory(parameters);
            if (report == null || report.summary.result != BuildResult.Succeeded)
                return new UnityContentBuildResult(false, "Unity Content BuildReport failed: " + report?.summary.result);
            return new UnityContentBuildResult(true,
                report.summary.result + "|" + report.summary.platform + "|" + report.summary.totalSize);
        }

        private static void DeleteIfEmpty(string assetFolder)
        {
            if (!AssetDatabase.IsValidFolder(assetFolder)) return;
            var absolute = Path.Combine(Application.dataPath, assetFolder.Substring("Assets/".Length));
            if (Directory.GetFileSystemEntries(absolute).Length == 0) AssetDatabase.DeleteAsset(assetFolder);
        }

        private static void CleanupOrphans()
        {
            // ownership marker と folder 名が一致する孤児だけを対象にする。
            // 別機能や人が置いた生成物を一括削除しないための制限。
            if (!AssetDatabase.IsValidFolder(GeneratedRoot)) return;
            foreach (var guid in AssetDatabase.FindAssets("t:DefaultAsset", new[] { GeneratedRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetDirectoryName(path)?.Replace('\\', '/') != GeneratedRoot ||
                    !AssetDatabase.IsValidFolder(path)) continue;
                var marker = path + "/" + Marker;
                var diskPath = Path.Combine(Application.dataPath, "OneStarMakerGenerated", "BS2b",
                    Path.GetFileName(path), Marker);
                if (File.Exists(diskPath) && File.ReadAllText(diskPath) == Path.GetFileName(path))
                    AssetDatabase.DeleteAsset(path);
            }
        }

        private static void EnsureFolder(string path)
        {
            // AssetDatabase を通して .meta を生成し、Unity が認識する一時 root にする。
            var parent = "Assets";
            foreach (var segment in path.Substring("Assets/".Length).Split('/'))
            {
                var next = parent + "/" + segment;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, segment);
                parent = next;
            }
        }

        private static AssetType ClassifyCategory(UnityEngine.Object asset)
        {
            if (asset is GameObject) return AssetType.Prefab;
            if (asset is Texture) return AssetType.Texture;
            if (asset is AudioClip) return AssetType.Audio;
            return AssetType.Other;
        }
    }
}
