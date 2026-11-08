#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Tests.Editor.Spikes.ContentDemandRouting
{
    /// <summary>
    /// 由来マーカー。fixture の各 Scene に root GameObject を 1 つ置き、その名前に値を持たせる。
    /// 実行中の Scene の由来は Scene の名前・path ではなくこの値で確かめる（HANDOFF §2 用語）。
    /// component にしないのは、この実験 code が Editor 専用 asmdef にあり、Editor の MonoBehaviour は
    /// Scene の GameObject に付けられず、build した directory にも入らないため（asmdef を足さない制約、N6）。
    /// </summary>
    internal static class OriginMarker
    {
        internal const string Prefix = "OriginMarker|";
        internal const string Original = "original";
        internal const string FixtureBuild = "build:fixture";

        internal static GameObject Create(string value)
        {
            Validate(value);
            return new GameObject(Prefix + value);
        }

        /// <summary>Scene の root にある全マーカーの値。0 個や 2 個以上も観測としてそのまま返す。</summary>
        internal static List<string> ReadAll(Scene scene)
        {
            var values = new List<string>();
            if (!scene.IsValid() || !scene.isLoaded) return values;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root != null && root.name.StartsWith(Prefix, StringComparison.Ordinal))
                    values.Add(root.name.Substring(Prefix.Length));
            }
            return values;
        }

        internal static string? ReadSingle(Scene scene)
        {
            var values = ReadAll(scene);
            return values.Count == 1 ? values[0] : null;
        }

        /// <summary>
        /// 書き出した snapshot の file の中のマーカーだけを書き換える。snapshot を Editor で開いて保存すると
        /// Editor の Scene setup が変わり、それ自体が N1 の比較を乱す（N1 の経路 (g)）ので、file を直接書き換える。
        /// 対象は harness が作った一時 asset だけで、利用者の Scene には使わない。
        /// </summary>
        internal static void RewriteInFile(string fullPath, string value)
        {
            Validate(value);
            var text = File.ReadAllText(fullPath);
            var pattern = new Regex(@"m_Name: " + Regex.Escape(Prefix) + @"[^\r\n]*");
            var matches = pattern.Matches(text).Count;
            if (matches != 1)
                throw new InvalidOperationException($"Snapshot must contain exactly one origin marker, found {matches}: {fullPath}");
            File.WriteAllText(fullPath, pattern.Replace(text, "m_Name: " + Prefix + value));
        }

        private static void Validate(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOfAny(new[] { '\r', '\n', '"', '\'' }) >= 0)
                throw new ArgumentException("Origin marker value must be a single plain line.", nameof(value));
        }
    }
}
