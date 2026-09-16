#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Build.Selection;

namespace OneStarMaker.Editor.Build.Materialization
{
    internal static class ScenePayloadMappingPolicy
    {
        public const string Dimension = "Representation";
        public static string StableKey(string logicalKey, string representation, string guid) =>
            "osm-build-candidate-v1|" + Part(logicalKey) + Part(representation) + Part(guid);

        public static BuildContentCandidate Candidate(string logicalKey, string representation, string guid, string path) =>
            new BuildContentCandidate(StableKey(logicalKey, representation, guid), logicalKey, guid,
                new BuildProvenance("SceneResourceMap", logicalKey, new[]
                {
                    new KeyValuePair<string, string>("rootGuid", guid),
                    new KeyValuePair<string, string>("rootPath", path)
                }));
        public static BuildTag Tag(string representation) => new BuildTag(Dimension, representation);
        public static string Representation(string variant) => variant.Length == 0 ? "Full" : variant;
        private static string Part(string value) => value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + value;
    }
}
