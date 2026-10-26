#nullable enable

using System;
using OneStarMaker.Runtime.AssetDescriptions;

namespace SampleGame.DependOnAll.Editor.WorldAuthoring
{
    /// <summary>
    /// 作成の種類。回復 journal に整数で残るので、値を並べ替えない。
    /// 0 を Cell companion にしてあるのは、種類の項目が無い既存の journal を従来どおり
    /// Cell companion として回復するため（LoadType.OnDemand が 0 であることには頼らない）。
    /// </summary>
    internal enum WorldCompanionCreationKind
    {
        CellCompanion = 0,
        SeasonAtmosphere = 1,
    }

    internal sealed class WorldCompanionCreationPlan
    {
        // 実 Cell / Environment は Architecture §27 どおり InGameSession/Seasons 配下。
        // World/ は Scene 用 C# の置き場であり、親 Cell フォルダではない。
        private const string SessionRoot = "Assets/SampleGame/InGame/InGameSession";
        private const string NodeRoot = "Assets/SceneGraphData/Nodes/Cells";
        private const string SeasonNodeRoot = "Assets/SceneGraphData/Nodes/Seasons";
        private const string ResourceOutputFolder = "Assets/OneStarMakerCommon/SceneMap";
        private const string MapPath = "Assets/OneStarMakerCommon/SceneMap/SceneResourceMap.asset";
        private const string SeasonAtmosphereSuffix = "_AtmosphereVFX";

        private WorldCompanionCreationPlan(
            WorldCompanionCreationKind kind,
            string identity,
            string parentIdentity,
            string scenePath,
            string resourcePath,
            string nodePath,
            string resourceOutputFolder,
            string mapPath,
            string sourceSearchRoot)
        {
            Kind = kind;
            Identity = identity;
            ParentIdentity = parentIdentity;
            ScenePath = scenePath;
            ResourcePath = resourcePath;
            NodePath = nodePath;
            ResourceOutputPath = resourceOutputFolder;
            SceneResourceMapPath = mapPath;
            SourceSearchRoot = sourceSearchRoot;
        }

        internal WorldCompanionCreationKind Kind { get; }
        internal string Identity { get; }
        internal string ParentIdentity { get; }
        internal string ScenePath { get; }
        internal string ResourcePath { get; }
        internal string NodePath { get; }
        internal string ResourceOutputPath { get; }
        internal string SceneResourceMapPath { get; }
        internal string SourceSearchRoot { get; }
        internal string Variant => string.Empty;
        internal LoadType LoadType => LoadTypeFor(Kind);

        /// <summary>
        /// 種類ごとの node / SceneResource の LoadType。回復時は journal の種類からこれを引き直す。
        /// Season の Atmosphere を NecessaryAlways にするのは、IncrementalAlways の子の保留 unload が
        /// 消費されない Framework の既知の不具合を避け、Season の Stable と枝の交換の待ちに入れるため。
        /// </summary>
        internal static LoadType LoadTypeFor(WorldCompanionCreationKind kind) => kind switch
        {
            WorldCompanionCreationKind.CellCompanion => LoadType.OnDemand,
            WorldCompanionCreationKind.SeasonAtmosphere => LoadType.NecessaryAlways,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        internal static string SeasonAtmosphereIdentity(WorldSeason season)
        {
            if (!Enum.IsDefined(typeof(WorldSeason), season)) throw new ArgumentOutOfRangeException(nameof(season));
            return $"{season}{SeasonAtmosphereSuffix}";
        }

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
                WorldCompanionCreationKind.CellCompanion,
                identity,
                parentIdentity,
                $"{folder}/{identity}.unity",
                $"{folder}/{identity}.asset",
                $"{NodeRoot}/{identity}.asset",
                ResourceOutputFolder,
                MapPath,
                string.Empty);
        }

        /// <summary>
        /// Season 直下の Atmosphere VFX の計画。GameSceneFactory はこの命名規則
        /// （identity {Season}_AtmosphereVFX、親 Season_{Season}）で Scene class を選ぶので、片方だけ変えない。
        /// </summary>
        internal static WorldCompanionCreationPlan CreateSeasonAtmosphere(WorldSeason season)
        {
            var identity = SeasonAtmosphereIdentity(season);
            var folder = $"{SessionRoot}/Seasons/{season}/{identity}";
            return new WorldCompanionCreationPlan(
                WorldCompanionCreationKind.SeasonAtmosphere,
                identity,
                $"Season_{season}",
                $"{folder}/{identity}.unity",
                $"{folder}/{identity}.asset",
                $"{SeasonNodeRoot}/{identity}.asset",
                ResourceOutputFolder,
                MapPath,
                string.Empty);
        }

        /// <param name="parentCellFolder">作成する folder の親。Season Atmosphere では Season の folder。</param>
        internal static WorldCompanionCreationPlan CreateForTests(
            string identity,
            string parentIdentity,
            string parentCellFolder,
            string nodeRoot,
            string resourceOutputFolder,
            string mapPath,
            string sourceSearchRoot,
            WorldCompanionCreationKind kind = WorldCompanionCreationKind.CellCompanion)
        {
            if (string.IsNullOrWhiteSpace(identity)) throw new ArgumentException("Identity is required.", nameof(identity));
            if (string.IsNullOrWhiteSpace(parentIdentity)) throw new ArgumentException("Parent identity is required.", nameof(parentIdentity));
            var folder = $"{parentCellFolder}/{identity}";
            return new WorldCompanionCreationPlan(
                kind,
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
