#nullable enable
using System;
using System.IO;
using UnityEngine;
using OneStarMaker.Editor.Build.Content;

namespace SampleGame.DependOnAll.Editor.Build
{
    [Serializable] internal sealed class PlayerContentReportData
    {
        public string identity = ""; public string target = ""; public string contentPath = "";
        public string manifestPointer = ""; public string metadataPath = ""; public string[] selected = Array.Empty<string>();
        public string[] roots = Array.Empty<string>(); public string[] issues = Array.Empty<string>();
    }

    internal sealed class ValidatedPlayerBuildInput
    {
        internal ValidatedPlayerBuildInput(BuildContentResult result, PlayerContentReportData preflight, PlayerContentReportData outcome)
        { Result = result; Preflight = preflight; Outcome = outcome; }
        internal BuildContentResult Result { get; } internal PlayerContentReportData Preflight { get; } internal PlayerContentReportData Outcome { get; }
    }

    internal static class PlayerBuildInputValidator
    {
        internal static ValidatedPlayerBuildInput Validate(BuildContentResult result, string preflightJson, string outcomeJson)
        {
            if (result == null || !result.IsSuccess || result.ContentPath == null || result.ManifestPointer == null || result.MetadataPath == null)
                throw new InvalidOperationException("A successful content result is required.");
            var preflight = Parse(preflightJson, "preflight"); var outcome = Parse(outcomeJson, "outcome");
            if (preflight.identity != result.Identity || outcome.identity != result.Identity ||
                preflight.target != "StandaloneWindows64-Player" || outcome.target != "StandaloneWindows64-Player" ||
                outcome.issues.Length != 0 || !Same(outcome.contentPath, result.ContentPath) ||
                !Same(outcome.manifestPointer, result.ManifestPointer) || !Same(outcome.metadataPath, result.MetadataPath))
                throw new InvalidOperationException("Content report and result do not describe the same successful build.");
            if (!Directory.Exists(result.ContentPath) || !File.Exists(result.ManifestPointer) || !Directory.Exists(result.MetadataPath))
                throw new InvalidOperationException("Content output or build metadata is missing.");
            return new ValidatedPlayerBuildInput(result, preflight, outcome);
        }
        private static PlayerContentReportData Parse(string json, string name)
        { var value = JsonUtility.FromJson<PlayerContentReportData>(json); return value ?? throw new InvalidOperationException("Invalid " + name + " report."); }
        private static bool Same(string a, string b) => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
    }
}
