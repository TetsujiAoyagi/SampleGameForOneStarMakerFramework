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
    /// <summary>シーン定義の読み取りから Content Directory の生成までをつなぐ Editor 入口。</summary>
    public static class SampleGameContentBuild
    {
        private const string MapPath = "Assets/OneStarMakerCommon/SceneMap/SceneResourceMap.asset";
        private const string LogPrefix = "[SampleGameContentBuild] ";

        [MenuItem("Tools/OSM/Content/Build All Seasons Full")]
        public static void BuildAllFull() => Build(new[] { "Spring", "Summer", "Autumn", "Winter" },
            SeasonContentMode.Full, "all-full");

        [MenuItem("Tools/OSM/Content/Build Spring Full")]
        public static void BuildSpringFull() => Build(new[] { "Spring" }, SeasonContentMode.Full, "spring-full");

        [MenuItem("Tools/OSM/Content/Build Spring Whitebox")]
        public static void BuildSpringWhitebox() => Build(new[] { "Spring" }, SeasonContentMode.Whitebox,
            "spring-whitebox");

        [MenuItem("Tools/OSM/Content/Build Spring Full And Whitebox")]
        public static void BuildSpringBoth() => Build(new[] { "Spring" }, SeasonContentMode.FullAndWhitebox,
            "spring-full-whitebox");

        public static void Build(IReadOnlyList<string> seasons, SeasonContentMode mode, string contentSet)
        {
            // 実アセットから候補を作り、シーン階層の複製で季節を決めてから選択する。
            // 途中で失敗した計画はビルド処理へ渡さない。
            var map = AssetDatabase.LoadAssetAtPath<SceneResourceMap>(MapPath);
            if (map == null) throw new InvalidOperationException("SceneResourceMap is missing: " + MapPath);
            var materialized = new SceneResourceContentMaterializer().Materialize(map);
            if (materialized.Snapshot == null)
                throw new InvalidOperationException("Materialization failed: " +
                    string.Join("; ", materialized.Issues.Select(x => x.Code + ":" + x.SubjectKey)));
            var graph = CopyGraph(map);
            var detail = new SeasonSceneSelectionPolicy().SelectDetailed(graph,
                materialized.Snapshot.Candidates, materialized.Snapshot.TagProviders, seasons, mode);
            var selection = detail.Selection;
            if (selection.Plan == null)
                throw new InvalidOperationException("Selection failed: " +
                    string.Join("; ", selection.Issues.Select(x => x.Code + ":" + x.SubjectKey)));
            var plan = selection.Plan;
            // 必須群と除外理由を残し、ビルドログから選択根拠を追えるようにする。
            Debug.Log(LogPrefix + "request=" + string.Join(",", plan.Request.Selections.Select(x => x.Dimension + "=" + x.Value)) +
                " required=" + detail.Requirements.Count + " selected=" + plan.SelectedContent.Count +
                " excluded=" + plan.ExcludedContent.Count);
            foreach (var requirement in detail.Requirements)
                Debug.Log(LogPrefix + "required " + requirement.LogicalKey + " / " + requirement.Cardinality);
            foreach (var exclusion in plan.ExcludedContent)
                Debug.Log(LogPrefix + "excluded " + exclusion.Candidate.LogicalKey + " / " +
                    exclusion.Dimension + "=" + exclusion.Value + " / " + exclusion.ReasonCode);

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var artifactsRoot = Path.GetFullPath(Path.Combine(projectRoot, "..", "artifacts", "bs2b"));
            var result = new BuildContentCoordinator().Build(
                new BuildContentRequest(plan, materialized.Snapshot, contentSet, artifactsRoot));
            if (!result.IsSuccess)
                throw new InvalidOperationException("Content Directory build failed: " + result.OutcomePath + " / " + result.Summary);
            Debug.Log(LogPrefix + "Content Directory: " + result.ContentPath + " / outcome: " + result.OutcomePath);
        }

        private static IReadOnlyList<SeasonSceneNode> CopyGraph(SceneResourceMap map)
        {
            // 選択処理には Unity オブジェクトを渡さず、親子 ID と内容の有無だけを固定する。
            var result = new List<SeasonSceneNode>();
            foreach (var resource in map.SceneResources)
            {
                if (resource == null) throw new InvalidOperationException("SceneResourceMap contains a null resource.");
                var children = new List<string>();
                foreach (var child in resource.Children)
                {
                    if (child == null) throw new InvalidOperationException("Scene graph has a null child: " + resource.Identity);
                    children.Add(child.Identity);
                }
                result.Add(new SeasonSceneNode(resource.Identity,
                    resource.Parent == null ? null : resource.Parent.Identity,
                    children, resource.GetPayloads().Count != 0));
            }
            return result;
        }
    }
}
