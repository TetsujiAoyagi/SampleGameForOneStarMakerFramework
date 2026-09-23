#nullable enable

using System;

namespace SampleGame.DependOnAll.Editor.WorldAuthoring
{
    internal sealed class WorldCompanionCreationPlan
    {
        // 実 Cell / Environment は Architecture §27 どおり InGameSession/Seasons 配下。
        // World/ は Scene 用 C# の置き場であり、親 Cell フォルダではない。
        private const string SessionRoot = "Assets/SampleGame/InGame/InGameSession";
        private const string NodeRoot = "Assets/SceneGraphData/Nodes/Cells";
        private const string ResourceOutputFolder = "Assets/OneStarMakerCommon/SceneMap";
        private const string MapPath = "Assets/OneStarMakerCommon/SceneMap/SceneResourceMap.asset";

        private WorldCompanionCreationPlan(
            string identity,
            string parentIdentity,
            string scenePath,
            string resourcePath,
            string nodePath,
            string resourceOutputFolder,
            string mapPath,
            string sourceSearchRoot)
        {
            Identity = identity;
            ParentIdentity = parentIdentity;
            ScenePath = scenePath;
            ResourcePath = resourcePath;
            NodePath = nodePath;
            ResourceOutputPath = resourceOutputFolder;
            SceneResourceMapPath = mapPath;
            SourceSearchRoot = sourceSearchRoot;
        }

        internal string Identity { get; }
        internal string ParentIdentity { get; }
        internal string ScenePath { get; }
        internal string ResourcePath { get; }
        internal string NodePath { get; }
        internal string ResourceOutputPath { get; }
        internal string SceneResourceMapPath { get; }
        internal string SourceSearchRoot { get; }
        internal string Variant => string.Empty;

        internal static WorldCompanionCreationPlan Create(WorldWorkspaceSelection selection)
        {
            if (selection == null) throw new ArgumentNullException(nameof(selection));
            var roleToken = selection.Role switch
            {
                WorldWorkspaceRole.Lighting => "Lighting",
                WorldWorkspaceRole.Vfx => "VFX",
                WorldWorkspaceRole.Planner => "Events",
                _ => throw new InvalidOperationException("Only optional Lighting, VFX, and Events companions can be created."),
            };
            var identity = selection.CompanionIdentity(roleToken);
            var parentIdentity = selection.CellIdentity;
            var folder = $"{SessionRoot}/Seasons/{selection.Season}/Cells/{parentIdentity}/{identity}";
            return new WorldCompanionCreationPlan(
                identity,
                parentIdentity,
                $"{folder}/{identity}.unity",
                $"{folder}/{identity}.asset",
                $"{NodeRoot}/{identity}.asset",
                ResourceOutputFolder,
                MapPath,
                string.Empty);
        }

        internal static WorldCompanionCreationPlan CreateForTests(
            string identity,
            string parentIdentity,
            string parentCellFolder,
            string nodeRoot,
            string resourceOutputFolder,
            string mapPath,
            string sourceSearchRoot)
        {
            if (string.IsNullOrWhiteSpace(identity)) throw new ArgumentException("Identity is required.", nameof(identity));
            if (string.IsNullOrWhiteSpace(parentIdentity)) throw new ArgumentException("Parent identity is required.", nameof(parentIdentity));
            var folder = $"{parentCellFolder}/{identity}";
            return new WorldCompanionCreationPlan(
                identity,
                parentIdentity,
                $"{folder}/{identity}.unity",
                $"{folder}/{identity}.asset",
                $"{nodeRoot}/{identity}.asset",
                resourceOutputFolder,
                mapPath,
                sourceSearchRoot);
        }
    }
}
