#nullable enable

using System;
using System.Collections.Generic;
using Unity.Loading;
using UnityEngine;

namespace OneStarMaker.Runtime.BuildContent
{
    // Unity Content Directory に保存する entry の load 先。型付き解決は BS3 で扱う。
    public enum BuildContentKind { Scene, Object }

    [Serializable]
    // logical key と選択済み表現から、Unity が生成した loadable ID へ渡す最小の対応表。
    // StableKey は build 入力との照合・診断用で、物理 GUID だけを load ID として扱わない。
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
            // Scene と Object は同じ entry 配列に入るが、使う ID は Kind で明示する。
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

    // Content Directory に厳密に1件同梱する root asset。
    // 登録後の Runtime はこの serialized 情報だけで logical→load target を復元できる。
    // AssetOwner による登録・解放の寿命管理は BS3 の責務であり、この型は対応 metadata のみ保持する。
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
            // Editor adapter が asset 化する直前に呼ぶ。配列を複写して呼出側の collection 変更を切り離す。
            // identity は OSM report と Unity build name に一致させ、移設後にも照合可能にする。
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
