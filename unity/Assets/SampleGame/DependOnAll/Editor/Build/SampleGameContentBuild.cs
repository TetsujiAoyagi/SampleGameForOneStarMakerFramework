#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Content;
using OneStarMaker.Editor.Build.Materialization;
using OneStarMaker.Runtime.SceneSystem;
using UnityEditor;
using UnityEngine;

namespace SampleGame.DependOnAll.Editor.Build
{
    /// <summary>シーン定義の読み取りから、宣言による選択、Content Directory の生成までをつなぐ Editor 入口。</summary>
    public static class SampleGameContentBuild
    {
        private const string MapPath = "Assets/OneStarMakerCommon/SceneMap/SceneResourceMap.asset";
        private const string LogPrefix = "[SampleGameContentBuild] ";

        // menu は宣言ごとの Build。contentSet（名前）は旧 menu と同じで、bs4-spring-full は BS4 の Player build だけが使う。
        [MenuItem("Tools/OSM/Content/Build all-full")]
        public static void BuildAllFull() => Build(SampleGameBuildPurposes.AllFull);

        [MenuItem("Tools/OSM/Content/Build spring-full")]
        public static void BuildSpringFull() => Build(SampleGameBuildPurposes.SpringFull);

        [MenuItem("Tools/OSM/Content/Build spring-whitebox")]
        public static void BuildSpringWhitebox() => Build(SampleGameBuildPurposes.SpringWhitebox);

        [MenuItem("Tools/OSM/Content/Build spring-full-whitebox")]
        public static void BuildSpringBoth() => Build(SampleGameBuildPurposes.SpringFullWhitebox);

        [MenuItem("Tools/OSM/Content/Build spring-planner-whitebox")]
        public static void BuildSpringPlannerWhitebox() => Build(SampleGameBuildPurposes.SpringPlannerWhitebox);

        // menu の build は Incremental（workspace を再利用して速い、publish しない）。publish する経路は Full を渡す。
        internal const BuildContentMode MenuBuildMode = BuildContentMode.Incremental;

        public static void Build(BuildPurposeDeclaration declaration) => BuildResult(declaration, MenuBuildMode);

        /// <summary>
        /// 実 SceneResourceMap を materialize し、宣言で選択する。build は行わない（判定の照合や test が選択だけを見る入口）。
        /// bootstrap の合成は含まない。
        /// </summary>
        public static BuildPurposeResolution Resolve(BuildPurposeDeclaration declaration)
        {
            var materialized = Materialize();
            return Resolve(declaration, materialized.Snapshot!);
        }

        internal static BuildPurposeResolution Resolve(BuildPurposeDeclaration declaration, BuildMaterializationSnapshot snapshot) =>
            new BuildPurposeResolver().Resolve(declaration, snapshot.Graph, snapshot.Candidates, snapshot.TagProviders, new SeasonSceneTagPolicy());

        /// <summary>宣言から build する。選択・build のどちらの失敗も例外にする（menu と BS4 Player build の入口）。</summary>
        internal static BuildContentResult BuildResult(BuildPurposeDeclaration declaration, BuildContentMode mode,
            Func<BuildPlan, BuildMaterializationSnapshot, (BuildPlan Plan, BuildMaterializationSnapshot Snapshot)>? compose = null)
        {
            var result = BuildUnchecked(declaration, mode, compose);
            if (!result.IsSuccess)
                throw new InvalidOperationException("Content Directory build failed: " + result.OutcomePath + " / " + result.Summary);
            return result;
        }

        /// <summary>
        /// 宣言から build し、coordinator の失敗（preflight の issue、Unity build の失敗）は result のまま返す。
        /// 宣言による選択の失敗は <see cref="PurposeSelectionException"/>。batch が終了コードを分けるための入口。
        /// </summary>
        internal static BuildContentResult BuildUnchecked(BuildPurposeDeclaration declaration, BuildContentMode mode,
            Func<BuildPlan, BuildMaterializationSnapshot, (BuildPlan Plan, BuildMaterializationSnapshot Snapshot)>? compose = null)
        {
            if (declaration == null) throw new ArgumentNullException(nameof(declaration));
            // 実アセットから候補と graph を作り、宣言から到達と選択を導いてから build する。途中で失敗した計画はビルド処理へ渡さない。
            var materialized = Materialize();
            var snapshot = materialized.Snapshot!;
            var resolution = Resolve(declaration, snapshot);
            if (resolution.Selection.Plan == null)
                throw new PurposeSelectionException("Selection failed: " +
                    string.Join("; ", resolution.Selection.Issues.Select(x => x.Code + ":" + x.Subject + ":" + x.SubjectKey)));
            var plan = resolution.Selection.Plan;
            var bootstrap = ComposeBootstrap(snapshot, plan);
            plan = bootstrap.Plan;
            snapshot = bootstrap.Snapshot;
            if (compose != null)
            {
                var composed = compose(plan, snapshot);
                plan = composed.Plan;
                snapshot = composed.Snapshot;
            }
            // 必須群と除外理由を残し、ビルドログから選択根拠を追えるようにする。到達経路は preflight の requirements[] に残る。
            Debug.Log(LogPrefix + "declaration=" + declaration.Name +
                " request=" + string.Join(",", plan.Request.Selections.Select(x => x.Dimension + "=" + x.Value)) +
                " required=" + resolution.Requirements.Count + " selected=" + plan.SelectedContent.Count +
                " excluded=" + plan.ExcludedContent.Count);
            foreach (var exclusion in plan.ExcludedContent)
                Debug.Log(LogPrefix + "excluded " + exclusion.Candidate.LogicalKey + " / " +
                    exclusion.Dimension + "=" + exclusion.Value + " / " + exclusion.ReasonCode);

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var artifactsRoot = Path.GetFullPath(Path.Combine(projectRoot, "..", "artifacts", "bs2b"));
            var result = new BuildContentCoordinator().Build(
                new BuildContentRequest(plan, snapshot, declaration.Name, artifactsRoot, declaration, resolution.Requirements, mode));
            if (result.IsSuccess)
                Debug.Log(LogPrefix + "Content Directory (" + mode + "): " + result.ContentPath + " / outcome: " + result.OutcomePath);
            return result;
        }

        private static BuildMaterializationResult Materialize()
        {
            var map = AssetDatabase.LoadAssetAtPath<SceneResourceMap>(MapPath);
            if (map == null) throw new InvalidOperationException("SceneResourceMap is missing: " + MapPath);
            var materialized = new SceneResourceContentMaterializer().Materialize(map);
            if (materialized.Snapshot == null)
                throw new InvalidOperationException("Materialization failed: " +
                    string.Join("; ", materialized.Issues.Select(x => x.Code + ":" + x.SubjectKey)));
            return materialized;
        }

        private static SampleGameBootstrapComposition ComposeBootstrap(BuildMaterializationSnapshot snapshot, BuildPlan plan)
        {
            var uiGuid = RequireGuid(SampleGameBootstrapContentComposer.UiCommonPath);
            var mapGuid = RequireGuid(SampleGameBootstrapContentComposer.SceneResourceMapPath);
            var issues = new List<BuildMaterializationIssue>();
            var builder = new AssetDependencySnapshotBuilder(new UnityAssetDatabaseGateway());
            var uiClosure = builder.Build(uiGuid, SampleGameBootstrapContentComposer.UiCommonLogicalKey, issues);
            var mapClosure = builder.Build(mapGuid, SampleGameBootstrapContentComposer.SceneResourceMapLogicalKey, issues);
            if (issues.Count != 0 || uiClosure == null || mapClosure == null)
                throw new InvalidOperationException("Bootstrap materialization failed: " +
                    string.Join("; ", issues.Select(x => x.Code + ":" + x.SubjectKey)));
            return SampleGameBootstrapContentComposer.Compose(plan, snapshot, uiGuid, uiClosure, mapGuid, mapClosure);
        }

        private static string RequireGuid(string path)
        {
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
                throw new InvalidOperationException("Bootstrap asset is missing: " + path);
            return guid.ToLowerInvariant();
        }
    }

    /// <summary>宣言による選択が plan を作れなかった失敗。従来どおり <see cref="InvalidOperationException"/> として扱える。</summary>
    internal sealed class PurposeSelectionException : InvalidOperationException
    {
        public PurposeSelectionException(string message) : base(message) { }
    }
}
