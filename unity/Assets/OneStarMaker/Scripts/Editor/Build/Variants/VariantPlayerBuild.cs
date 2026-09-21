#nullable enable

using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OneStarMaker.Editor.Build
{
    internal interface IVariantPlayerBuildBackend
    {
        void Build();
    }

    /// <summary>active profile の JSON overlay を build 中だけ適用する。</summary>
    public static class VariantPlayerBuild
    {
        internal const string AppConfigAssetPath = "Assets/SampleGame/Config/app-config.json";
        internal const string FirstSceneIdentifyConfigKey = "assetCheckout:firstSceneIdentify";
        internal const string SceneVariantConfigKey = "assets:sceneVariant";

        [MenuItem("OneStarMaker/Build/Build Player (Active Variant)")]
        public static void BuildActiveVariant()
        {
            Debug.LogWarning(
                "[VariantPlayerBuild] Active Variant Player overlay is retired. " +
                "Use the BS4 Player coordinator. app-config.json is not mutated.");
        }

        internal static void BuildWithOverlay(BuildVariantProfile profile, string configFullPath, IVariantPlayerBuildBackend backend)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (configFullPath == null) throw new ArgumentNullException(nameof(configFullPath));
            if (backend == null) throw new ArgumentNullException(nameof(backend));

            profile.ThrowIfSceneVariantInvalid();
            var originalBytes = File.ReadAllBytes(configFullPath);
            try
            {
                var json = DecodeUtf8(originalBytes);
                json = UpsertTopLevelString(json, FirstSceneIdentifyConfigKey, profile.FirstSceneIdentify);
                json = UpsertTopLevelString(json, SceneVariantConfigKey, profile.SceneVariant);
                File.WriteAllText(configFullPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                AssetDatabase.ImportAsset(AppConfigAssetPath);
                backend.Build();
            }
            finally
            {
                File.WriteAllBytes(configFullPath, originalBytes);
                AssetDatabase.ImportAsset(AppConfigAssetPath);
            }
        }

        internal static string UpsertTopLevelString(string json, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("JSON is empty.", nameof(json));
            var close = FindRootObjectEnd(json);
            var valueRange = FindTopLevelStringValue(json, key, close);
            var escaped = EscapeJsonString(value);
            if (valueRange.HasValue)
            {
                return json.Substring(0, valueRange.Value.Start) + escaped + json.Substring(valueRange.Value.End);
            }

            var open = json.IndexOf('{');
            var body = json.Substring(open + 1, close - open - 1);
            var separator = string.IsNullOrWhiteSpace(body) ? string.Empty : ",";
            var entry = $"{separator}\n    \"{EscapeJsonString(key)}\" : \"{escaped}\"\n";
            return json.Substring(0, close) + entry + json.Substring(close);
        }

        private static (int Start, int End)? FindTopLevelStringValue(string json, string key, int rootEnd)
        {
            var depth = 0;
            for (var i = 0; i < rootEnd; i++)
            {
                if (json[i] == '{') { depth++; continue; }
                if (json[i] == '}') { depth--; continue; }
                if (json[i] != '"') continue;
                var tokenStart = i + 1;
                var tokenEnd = SkipString(json, i);
                if (depth == 1 && string.Equals(UnescapeSimple(json.Substring(tokenStart, tokenEnd - tokenStart)), key, StringComparison.Ordinal))
                {
                    var cursor = tokenEnd + 1;
                    while (cursor < rootEnd && char.IsWhiteSpace(json[cursor])) cursor++;
                    if (cursor >= rootEnd || json[cursor] != ':') { i = tokenEnd; continue; }
                    cursor++;
                    while (cursor < rootEnd && char.IsWhiteSpace(json[cursor])) cursor++;
                    if (cursor >= rootEnd || json[cursor] != '"') throw new ArgumentException($"Top-level property '{key}' is not a string.", nameof(json));
                    var valueEnd = SkipString(json, cursor);
                    return (cursor + 1, valueEnd);
                }
                i = tokenEnd;
            }
            return null;
        }

        private static int FindRootObjectEnd(string json)
        {
            var open = json.IndexOf('{');
            if (open < 0) throw new ArgumentException("Root object is missing.", nameof(json));
            var depth = 0;
            for (var i = open; i < json.Length; i++)
            {
                if (json[i] == '"') { i = SkipString(json, i); continue; }
                if (json[i] == '{') depth++;
                else if (json[i] == '}' && --depth == 0) return i;
            }
            throw new ArgumentException("Root object is incomplete.", nameof(json));
        }

        private static int SkipString(string text, int quote)
        {
            for (var i = quote + 1; i < text.Length; i++)
            {
                if (text[i] == '\\') { i++; continue; }
                if (text[i] == '"') return i;
            }
            throw new ArgumentException("JSON string is incomplete.", nameof(text));
        }

        private static string DecodeUtf8(byte[] bytes)
        {
            var offset = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf ? 3 : 0;
            return new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);
        }

        private static string EscapeJsonString(string value)
        {
            var escaped = new StringBuilder(value.Length);
            foreach (var character in value)
            {
                switch (character)
                {
                    case '\\': escaped.Append("\\\\"); break;
                    case '"': escaped.Append("\\\""); break;
                    case '\b': escaped.Append("\\b"); break;
                    case '\f': escaped.Append("\\f"); break;
                    case '\n': escaped.Append("\\n"); break;
                    case '\r': escaped.Append("\\r"); break;
                    case '\t': escaped.Append("\\t"); break;
                    default:
                        if (character < ' ') escaped.Append("\\u").Append(((int)character).ToString("x4"));
                        else escaped.Append(character);
                        break;
                }
            }
            return escaped.ToString();
        }

        private static string UnescapeSimple(string value)
            => value.Replace("\\\"", "\"").Replace("\\\\", "\\");

        private static string GetFullPath(string assetPath)
            => Path.Combine(Path.GetDirectoryName(Application.dataPath)!, assetPath);

        private sealed class UnityPlayerBuildBackend : IVariantPlayerBuildBackend
        {
            public void Build()
            {
                var output = Path.Combine(Path.GetDirectoryName(Application.dataPath)!, "Builds/ActiveVariant");
                Directory.CreateDirectory(output);
                var target = EditorUserBuildSettings.activeBuildTarget;
                var executable = target is BuildTarget.StandaloneWindows or BuildTarget.StandaloneWindows64 ? "SampleGame.exe" : "SampleGame";
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Scenes/SampleScene.unity" },
                    locationPathName = Path.Combine(output, executable),
                    target = target,
                    targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                    options = BuildOptions.None,
                });
                if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException($"Build result: {report.summary.result}");
            }
        }
    }
}
