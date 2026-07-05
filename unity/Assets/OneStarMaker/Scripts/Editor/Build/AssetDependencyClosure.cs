#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace OneStarMaker.Editor.Build
{
    /// <summary>
    /// アセットの依存閉包（dependency closure）を計算し、
    /// ローカル環境で完結して利用できるかを判定するユーティリティ。
    /// アセット単体ではなく、参照先を再帰的に含めた集合単位で欠損を検出する。
    /// チェックアウト検証や Editor Play のハイブリッドカタログ構築など、
    /// 「必要なコンテンツが手元に揃っているか」を確認する後続機能の中核部品として使う。
    /// </summary>
    public static class AssetDependencyClosure
    {
        /// <summary>
        /// 依存閉包の計算結果。
        /// ルート群から再帰展開したコンテンツ GUID 集合と、
        /// ディスク上に存在しないメンバーのパス一覧を保持する。
        /// </summary>
        public readonly struct AssetClosureResult
        {
            /// <summary>
            /// ルート群とその依存を再帰展開した「コンテンツ扱いすべき」GUID 集合。
            /// ルート自身も含む。重複は排除済み。
            /// </summary>
            public IReadOnlyCollection<string> ClosureGuids { get; }

            /// <summary>
            /// 閉包メンバのうち、プロジェクト内に実在しないアセットのプロジェクト相対パス一覧。
            /// パス解決できないルート GUID は <c>GUID:{guid} (path unresolved)</c> 形式で記録される。
            /// </summary>
            public IReadOnlyList<string> MissingAssetPaths { get; }

            /// <summary>
            /// 閉包内のコンテンツがすべてローカルに揃っているか。
            /// 欠損パスが 1 件もなければ true。
            /// </summary>
            public bool IsComplete => MissingAssetPaths.Count == 0;

            /// <summary>
            /// 計算結果を構築する。
            /// </summary>
            /// <param name="closureGuids">コンテンツ扱い GUID 集合。</param>
            /// <param name="missingAssetPaths">欠損アセットのパス一覧。</param>
            public AssetClosureResult(
                IReadOnlyCollection<string> closureGuids,
                IReadOnlyList<string> missingAssetPaths)
            {
                ClosureGuids = closureGuids;
                MissingAssetPaths = missingAssetPaths;
            }
        }

        /// <summary>
        /// 指定ルート GUID 群から依存閉包を計算し、ローカル完結性を判定する。
        /// 各ルートについて <see cref="AssetDatabase.GetDependencies"/> で再帰依存を取得し、
        /// <see cref="ShouldTreatAsContent"/> でフィルタしたうえで GUID 集合を構築する。
        /// 同一パスに対する重い依存取得はメモ化して二重実行を避ける。
        /// </summary>
        /// <param name="rootGuids">閉包展開の起点となるアセット GUID 列。</param>
        /// <returns>閉包 GUID 集合と欠損パス一覧を含む結果。</returns>
        public static AssetClosureResult Compute(IEnumerable<string> rootGuids)
        {
            var closureGuids = new HashSet<string>(StringComparer.Ordinal);
            var missingPaths = new HashSet<string>(StringComparer.Ordinal);
            var processedPaths = new HashSet<string>(StringComparer.Ordinal);

            foreach (var rootGuid in rootGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(rootGuid);
                if (string.IsNullOrEmpty(path))
                {
                    missingPaths.Add($"GUID:{rootGuid} (path unresolved)");
                    continue;
                }

                if (!processedPaths.Add(path))
                {
                    continue;
                }

                var dependencies = AssetDatabase.GetDependencies(path, recursive: true);
                foreach (var depPath in dependencies)
                {
                    if (!ShouldTreatAsContent(depPath))
                    {
                        continue;
                    }

                    var guid = AssetDatabase.AssetPathToGUID(depPath);
                    if (!string.IsNullOrEmpty(guid))
                    {
                        closureGuids.Add(guid);
                    }

                    if (!File.Exists(depPath))
                    {
                        missingPaths.Add(depPath);
                    }
                }
            }

            return new AssetClosureResult(closureGuids, new List<string>(missingPaths));
        }

        /// <summary>
        /// 指定パスを「チェックアウト対象のコンテンツ」として扱うべきか判定する。
        /// <see cref="AssetDatabase"/> に依存しない純粋関数であり、単体テスト可能にするため分離している。
        /// パッケージ共有物・スクリプト/アセンブリ・Unity 組み込みリソースは
        /// 全環境で常に利用可能とみなし、ローカル完結性の判定対象から除外する。
        /// </summary>
        /// <param name="assetPath">プロジェクト相対のアセットパス。</param>
        /// <returns>コンテンツとして扱うべきなら true。</returns>
        public static bool ShouldTreatAsContent(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            var normalized = assetPath.Replace('\\', '/');

            if (normalized.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var extension = Path.GetExtension(normalized);
            if (extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".dll", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".asmdef", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".asmref", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (normalized.Contains("Resources/unity_builtin_extra", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("Library/unity default resources", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase);
        }
    }
}
