#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SampleGame.DependOnAll.Editor.Build
{
    /// <summary>
    /// 遊べる Player に焼き込む content 以外の入力の file を集めて hash を取る唯一の口（D11）。遊べる Player の build と、上書きを重ねて
    /// Player を起動する入口の両方がここを呼ぶ（集め方が 2 つに分かれると、同じ project でも指紋が食い違う）。
    /// 集める規則（除外）は <see cref="PlayerCompatibility"/> にあり、ここは Unity と disk の I/O だけを持つ。
    /// </summary>
    internal static class PlayerInputFiles
    {
        /// <summary>project の root（<c>unity/</c>）からの相対 path の file の列と、各 file の hash。</summary>
        internal static IReadOnlyList<PlayerInputFileEntry> Collect()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var candidates = new List<string>();

            // ProjectSettings は folder も再帰する（package が足す設定の folder を含む）。
            candidates.AddRange(FilesUnder(projectRoot, "ProjectSettings"));
            // package の版。package の中の file は入れない（Library/PackageCache の path は checkout ごとに違う）。
            candidates.Add("Packages/manifest.json");
            candidates.Add("Packages/packages-lock.json");
            candidates.AddRange(RenderPipelineClosure());
            // Resources という名前の folder は Assets の下のどこにあっても Player に入る。
            foreach (var resources in Directory.GetDirectories(Path.Combine(projectRoot, "Assets"), "Resources", SearchOption.AllDirectories))
                candidates.AddRange(FilesUnder(projectRoot, Relative(projectRoot, resources)));
            if (Directory.Exists(Path.Combine(projectRoot, "Assets", "StreamingAssets")))
                candidates.AddRange(FilesUnder(projectRoot, "Assets/StreamingAssets"));
            candidates.Add(SampleGamePlayerBuildCoordinator.PlayableStartScene);
            candidates.Add(SampleGamePlayerBuildCoordinator.PlayableStartScene + ".meta");

            var selected = PlayerCompatibility.Select(candidates);
            var entries = new List<PlayerInputFileEntry>(selected.Count);
            foreach (var path in selected)
            {
                var full = Path.Combine(projectRoot, path.Replace('/', Path.DirectorySeparatorChar));
                // 起点に挙げた file が無いのは project の壊れで、指紋から黙って抜くと比較が漏れるので止める。
                if (!File.Exists(full)) throw new FileNotFoundException("A Player input file is missing: " + path, full);
                entries.Add(new PlayerInputFileEntry(path, Sha256(full)));
            }
            return entries;
        }

        /// <summary>
        /// render pipeline の asset の依存の閉包のうち Assets の下の file と、それぞれの隣の .meta（import の設定）。
        /// 起点は GraphicsSettings の既定、QualitySettings の全ての level（Standalone を除外する level も入れる側に倒す）、
        /// GraphicsSettings の render pipeline ごとの global settings の表。表は guid の参照で持つので、名前で探さず参照を辿る。
        /// </summary>
        private static IEnumerable<string> RenderPipelineClosure()
        {
            var roots = new List<UnityEngine.Object>();
            var defaultPipeline = GraphicsSettings.defaultRenderPipeline;
            if (defaultPipeline != null) roots.Add(defaultPipeline);
            for (var level = 0; level < QualitySettings.count; level++)
            {
                var pipeline = QualitySettings.GetRenderPipelineAssetAt(level);
                if (pipeline != null) roots.Add(pipeline);
            }
            roots.AddRange(GlobalSettingsMapReferences());

            var rootPaths = roots.Select(AssetDatabase.GetAssetPath).Where(x => !string.IsNullOrEmpty(x)).Distinct(StringComparer.Ordinal).ToArray();
            if (rootPaths.Length == 0) return Array.Empty<string>();
            var files = new List<string>();
            foreach (var path in AssetDatabase.GetDependencies(rootPaths, true))
            {
                if (!path.StartsWith("Assets/", StringComparison.Ordinal)) continue;
                files.Add(path);
                files.Add(path + ".meta");
            }
            return files;
        }

        private static IEnumerable<UnityEngine.Object> GlobalSettingsMapReferences()
        {
            var graphicsSettings = GraphicsSettings.GetGraphicsSettings();
            if (graphicsSettings == null) yield break;
            using var serialized = new SerializedObject(graphicsSettings);
            var map = serialized.FindProperty("m_RenderPipelineGlobalSettingsMap");
            if (map == null) yield break;
            var end = map.GetEndProperty();
            var cursor = map.Copy();
            if (!cursor.Next(true)) yield break;
            while (!SerializedProperty.EqualContents(cursor, end))
            {
                if (cursor.propertyType == SerializedPropertyType.ObjectReference && cursor.objectReferenceValue != null)
                    yield return cursor.objectReferenceValue;
                if (!cursor.Next(true)) yield break;
            }
        }

        private static IEnumerable<string> FilesUnder(string projectRoot, string relativeDirectory)
        {
            var full = Path.Combine(projectRoot, relativeDirectory.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(full)) return Array.Empty<string>();
            return Directory.GetFiles(full, "*", SearchOption.AllDirectories).Select(x => Relative(projectRoot, x));
        }

        private static string Relative(string projectRoot, string fullPath) =>
            PlayerCompatibility.Normalize(Path.GetFullPath(fullPath).Substring(projectRoot.TrimEnd(Path.DirectorySeparatorChar).Length));

        private static string Sha256(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var hash = SHA256.Create();
            return string.Concat(hash.ComputeHash(stream).Select(x => x.ToString("x2")));
        }
    }
}
