#nullable enable

using System;
using System.Collections.Generic;
using Unity.Loading;
using UnityEngine;

namespace OneStarMaker.Runtime.BuildContent
{
    public enum BuildContentKind { Scene, Object }

    [Serializable]
    public sealed class BuildContentEntry
    {
        [SerializeField] private string _logicalKey = "";
        [SerializeField] private string _stableKey = "";
        [SerializeField] private string _representation = "";
        [SerializeField] private BuildContentKind _kind;
        [SerializeField] private LoadableSceneId _sceneId;
        [SerializeField] private Loadable<UnityEngine.Object>? _object;

        public string LogicalKey => _logicalKey;
        public string StableKey => _stableKey;
        public string Representation => _representation;
        public BuildContentKind Kind => _kind;
        public LoadableSceneId SceneId => _sceneId;
        public Loadable<UnityEngine.Object>? Object => _object;

        public BuildContentEntry(string logicalKey, string stableKey, string representation,
            LoadableSceneId sceneId)
        {
            _logicalKey = logicalKey; _stableKey = stableKey; _representation = representation;
            _kind = BuildContentKind.Scene; _sceneId = sceneId;
        }

        public BuildContentEntry(string logicalKey, string stableKey, string representation,
            Loadable<UnityEngine.Object> loadable)
        {
            _logicalKey = logicalKey; _stableKey = stableKey; _representation = representation;
            _kind = BuildContentKind.Object; _object = loadable;
        }
    }

    public sealed class BuildContentRoot : ScriptableObject
    {
        public const int CurrentSchemaVersion = 1;
        [SerializeField] private int _schemaVersion;
        [SerializeField] private string _buildIdentity = "";
        [SerializeField] private string _target = "";
        [SerializeField] private BuildContentEntry[] _entries = Array.Empty<BuildContentEntry>();

        public int SchemaVersion => _schemaVersion;
        public string BuildIdentity => _buildIdentity;
        public string Target => _target;
        public IReadOnlyList<BuildContentEntry> Entries => Array.AsReadOnly(_entries);

        public void Initialize(string buildIdentity, string target, IReadOnlyList<BuildContentEntry> entries)
        {
            if (string.IsNullOrEmpty(buildIdentity) || string.IsNullOrEmpty(target) || entries == null)
                throw new ArgumentException("Build root requires identity, target and entries.");
            _schemaVersion = CurrentSchemaVersion;
            _buildIdentity = buildIdentity;
            _target = target;
            _entries = new BuildContentEntry[entries.Count];
            for (var i = 0; i < entries.Count; i++)
                _entries[i] = entries[i] ?? throw new ArgumentException("Null content entry.", nameof(entries));
        }
    }
}
