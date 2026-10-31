#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Build.Selection;
using OneStarMaker.Runtime.AssetDescriptions;

namespace OneStarMaker.Editor.Build.Materialization
{
    // SceneResource の payload を Selection の candidate と tag に写す唯一の規則。
    internal static class ScenePayloadMappingPolicy
    {
        public const string Dimension = BuildRepresentationSelection.Dimension;
        public static string StableKey(string logicalKey, string representation, string guid) =>
            "osm-build-candidate-v1|" + Part(logicalKey) + Part(representation) + Part(guid);

        public static BuildContentCandidate Candidate(string logicalKey, string representation, string guid, string path) =>
            new BuildContentCandidate(StableKey(logicalKey, representation, guid), logicalKey, guid,
                new BuildProvenance("SceneResourceMap", logicalKey, new[]
                {
                    new KeyValuePair<string, string>("rootGuid", guid),
                    new KeyValuePair<string, string>("rootPath", path)
                }));

        // 付随 asset の候補。stable key の規則は Scene payload と同じで、logical key が `<所有 node>:<名前>` になる。
        // 所有 node は tag ではなく候補の metadata（Owner）に置く。tag にすると宣言が要求しない dimension として落ちる。
        public static BuildContentCandidate AttachedCandidate(string ownerIdentity, string name, string representation, string guid, string path)
        {
            var logicalKey = AttachedAssetDescription.LogicalKey(ownerIdentity, name);
            return new BuildContentCandidate(StableKey(logicalKey, representation, guid), logicalKey, guid,
                new BuildProvenance("SceneResourceMap.AttachedAsset", logicalKey, new[]
                {
                    new KeyValuePair<string, string>("rootGuid", guid),
                    new KeyValuePair<string, string>("rootPath", path),
                    new KeyValuePair<string, string>("owner", ownerIdentity)
                }), ownerIdentity);
        }

        public static BuildTag Tag(string representation) => new BuildTag(Dimension, representation);
        public static string Representation(string variant) => variant.Length == 0 ? "Full" : variant;

        // Runtime の LoadType を Selection の 3 値へ写す。Selection は Runtime を参照しない。
        public static BuildGraphLoad Load(LoadType loadType) => loadType switch
        {
            LoadType.NecessaryAlways => BuildGraphLoad.Always,
            LoadType.IncrementalAlways => BuildGraphLoad.Incremental,
            LoadType.OnDemand => BuildGraphLoad.OnDemand,
            _ => throw new ArgumentOutOfRangeException(nameof(loadType), loadType, null)
        };

        // 長さを前置して連結境界を一意にし、異なる入力が同じ stable key にならないようにする。
        private static string Part(string value) => value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + value;
    }
}
