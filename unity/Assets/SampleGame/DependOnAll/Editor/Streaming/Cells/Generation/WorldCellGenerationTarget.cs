#nullable enable

using System;
using System.Collections.Generic;
using SampleGame.InGame.World;
using UnityEngine;

namespace SampleGame.DependOnAll.Editor.Streaming.Cells.Generation
{
    /// <summary>
    /// 生成対象の不透明な identity と、視覚配置に使う格子座標を対にした値。
    /// </summary>
    public readonly struct WorldCellGenerationTarget
    {
        public WorldCellGenerationTarget(string identity, Vector2Int coordinate)
        {
            if (string.IsNullOrWhiteSpace(identity))
            {
                throw new ArgumentException("Cell identity は空白以外である必要があります。", nameof(identity));
            }

            if (identity.IndexOf('/') >= 0 || identity.IndexOf('\\') >= 0)
            {
                throw new ArgumentException("Cell identity にパス区切り文字は使えません。", nameof(identity));
            }

            Identity = identity;
            Coordinate = coordinate;
        }

        public string Identity { get; }

        public Vector2Int Coordinate { get; }

        /// <summary>公開 target 列入口が共有する identity / duplicate 検証。</summary>
        public static void Validate(IReadOnlyList<WorldCellGenerationTarget> targets)
        {
            if (targets == null) throw new ArgumentNullException(nameof(targets));
            var identities = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < targets.Count; i++)
            {
                var identity = targets[i].Identity;
                if (string.IsNullOrWhiteSpace(identity))
                    throw new ArgumentException("生成対象 identity は空白以外である必要があります。", nameof(targets));
                if (!identities.Add(identity))
                    throw new ArgumentException($"生成対象 identity が重複しています: {identity}", nameof(targets));
            }
        }

        /// <summary>
        /// Grid の座標列を生成対象へ変換する。Cell identity を Format する唯一の入口。
        /// </summary>
        public static IReadOnlyList<WorldCellGenerationTarget> FromGrid(WorldGridDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var coordinates = definition.EnumerateCells();
            var targets = new List<WorldCellGenerationTarget>(coordinates.Count);
            var identities = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < coordinates.Count; i++)
            {
                var coordinate = coordinates[i];
                var identity = CellIdentity.Format(coordinate.x, coordinate.y);
                if (!identities.Add(identity))
                {
                    throw new InvalidOperationException($"生成対象 identity が重複しています: {identity}");
                }

                targets.Add(new WorldCellGenerationTarget(identity, coordinate));
            }

            return targets;
        }
    }
}
