#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using OneStarMaker.Runtime.AssetDescriptions;
using SampleGame.InGame.Streaming;
using UnityEngine;

namespace SampleGame.DependOnAll.Editor.WorldAuthoring
{
    /// <summary>S-4b P1 の一回限りの値計画。AssetDatabase / Scene I/O は持たない。</summary>
    internal sealed class SeasonWorldGenerationPlan
    {
        internal const string Root = "Assets/SampleGame/InGame/InGameSession/Seasons";
        internal const string OldRoot = "Assets/SampleGame/InGame/InGameSession/World";
        internal const string MaterialPath = Root + "/Materials/DemoCellLit.mat";
        internal const string OldMaterialPath = OldRoot + "/Materials/DemoCellLit.mat";
        internal const string MapPath = "Assets/OneStarMakerCommon/SceneMap/SceneResourceMap.asset";
        internal const string TotalPath = "Assets/SceneGraphData/Graphs/Total.asset";
        internal const string LayoutPath = "Assets/SceneGraphData/Layouts/Total_Layout.asset";
        internal const string SessionNodePath = "Assets/SceneGraphData/Nodes/InGameSession.asset";
        internal const string SessionResourcePath = "Assets/OneStarMakerCommon/SceneMap/InGameSession.asset";
        internal static readonly IReadOnlyList<string> Seasons = Array.AsReadOnly(new[] { "Spring", "Summer", "Autumn", "Winter" });
        private static readonly Vector2Int[] Line =
        {
            new(0, 4), new(1, 4), new(2, 3), new(3, 3), new(4, 2),
            new(5, 2), new(6, 2), new(7, 1), new(8, 1),
        };

        internal sealed class Entry
        {
            internal Entry(string identity, string parent, string folder, string season, int x, int y, bool cell, bool lighting)
            {
                Identity = identity; Parent = parent; Season = season; X = x; Y = y;
                IsCell = cell; IsLighting = lighting;
                ResourcePath = folder + "/" + identity + ".asset";
                NodePath = "Assets/SceneGraphData/Nodes/Seasons/" + identity + ".asset";
                ScenePath = x < 0 && !lighting ? string.Empty : folder + "/" + identity + ".unity";
                WhiteboxPath = cell ? folder + "/Variants/Whitebox/" + identity + ".unity" : string.Empty;
            }
            internal string Identity { get; }
            internal string Parent { get; }
            internal string Season { get; }
            internal int X { get; }
            internal int Y { get; }
            internal bool IsCell { get; }
            internal bool IsLighting { get; }
            internal string ResourcePath { get; }
            internal string NodePath { get; }
            internal string ScenePath { get; }
            internal string WhiteboxPath { get; }
            internal LoadType LoadType => IsLighting ? LoadType.NecessaryAlways : LoadType.OnDemand;
            internal float HueOffset => Array.IndexOf(Seasons.ToArray(), Season) * 0.08f;
        }

        internal readonly struct Shape
        {
            internal Shape(string name, Vector3 center, Vector3 size, bool collider, float yaw = 0)
            { Name = name; Center = center; Size = size; Collider = collider; Yaw = yaw; }
            internal string Name { get; }
            internal Vector3 Center { get; }
            internal Vector3 Size { get; }
            internal bool Collider { get; }
            internal float Yaw { get; }
        }

        internal IReadOnlyList<Entry> Entries { get; }
        internal IReadOnlyList<string> NewAssetPaths { get; }

        internal SeasonWorldGenerationPlan()
        {
            var entries = new List<Entry>();
            foreach (var season in Seasons)
            {
                var folder = Root + "/" + season;
                var seasonId = SeasonCellNames.Season(season);
                entries.Add(new Entry(seasonId, "InGameSession", folder, season, -1, -1, false, false));
                var lighting = SeasonCellNames.Lighting(season);
                entries.Add(new Entry(lighting, seasonId, folder + "/" + lighting, season, -1, -1, false, true));
                for (var y = 0; y < 6; y++)
                for (var x = 0; x < 9; x++)
                {
                    var cell = SeasonCellNames.Cell(season, x, y);
                    var cellFolder = folder + "/Cells/" + cell;
                    entries.Add(new Entry(cell, seasonId, cellFolder, season, x, y, true, false));
                    var env = SeasonCellNames.Environment(season, x, y);
                    entries.Add(new Entry(env, cell, cellFolder + "/" + env, season, x, y, false, false));
                }
            }
            Entries = entries.AsReadOnly();
            NewAssetPaths = entries.SelectMany(e => new[] { e.ResourcePath, e.NodePath, e.ScenePath, e.WhiteboxPath })
                .Where(p => p.Length > 0).OrderBy(p => p, StringComparer.Ordinal).ToList().AsReadOnly();
        }

        internal static IReadOnlyList<Shape> Shapes(Entry entry, bool whitebox)
        {
            var shapes = new List<Shape>();
            if (entry.X < 0) return shapes;
            var center = new Vector3(entry.X * 250 + 125, 0, entry.Y * 250 + 125);
            var motif = WorldCellCatalog.GetMotifIndex(entry.X, entry.Y);
            if (!entry.IsCell)
            {
                if (!whitebox)
                    for (var i = 0; i < 1 + motif % 3; i++)
                        shapes.Add(new Shape("EnvProp_" + i, center + new Vector3(40 + i * 22, 2 + i, 40), new Vector3(8, 4 + i * 2, 8), false));
                return shapes;
            }
            shapes.Add(new Shape("Ground", center + Vector3.up * 0.5f, new Vector3(245, 1, 245), true));
            var index = Array.IndexOf(Line, new Vector2Int(entry.X, entry.Y));
            if (index >= 0)
            {
                var from = Line[Math.Max(0, index - 1)];
                var to = Line[Math.Min(Line.Length - 1, index + 1)];
                var direction = new Vector2(to.x - from.x, to.y - from.y).normalized;
                // Rotated strip stays inside the cell; floor remains the walking surface.
                var length = (245 - 40 * Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.y)))
                    / Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.y));
                shapes.Add(new Shape("Line", center + Vector3.up * 2, new Vector3(40, 2, length), false,
                    Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg));
            }
            if (entry.X == 4 && entry.Y == 2)
                shapes.Add(new Shape("Witness", center + Vector3.up * 25, new Vector3(12, 48, 12), false));
            if (entry.Y == 5)
                shapes.Add(new Shape("NorthWall", center + new Vector3(0, 13, 112.5f), new Vector3(245, 24, 20), false));
            if (!whitebox)
                for (var i = 0; i < 2 + motif; i++)
                    shapes.Add(new Shape("Prop_" + i, center + new Vector3(-80 + i * 30, 4 + i, -65), new Vector3(8, 6 + i * 2, 8), false));
            return shapes;
        }
    }
}
