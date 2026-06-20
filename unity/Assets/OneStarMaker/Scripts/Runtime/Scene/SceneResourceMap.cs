#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneStarMaker.Runtime.SceneSystem
{
    /// <summary>
    /// SceneResource の一覧を保持し、identity から検索できるようにする ScriptableObject。
    /// SceneGraph Editor の Generate で生成される。直接作成禁止（CreateAssetMenu 削除済み）。
    /// </summary>
    public class SceneResourceMap : ScriptableObject
    {
        [SerializeField]
        private List<SceneResource> _sceneResources = new();

        /// <summary>
        /// Generate 時の中間データハッシュ（W-3: Generate 忘れ検出用）。
        /// Editor 側で中間データから再計算したハッシュと比較し、不一致なら Generate 忘れを警告する。
        /// </summary>
        [SerializeField]
        private string _generateHash = string.Empty;

        /// <summary>Generate 時に書き込まれたハッシュ。</summary>
        public string GenerateHash => _generateHash;

        /// <summary>登録済み SceneResource 一覧。</summary>
        public IReadOnlyList<SceneResource> SceneResources => _sceneResources;

        private Dictionary<string, SceneResource>? _dictionary;

        /// <summary>
        /// identity からシーンリソースを取得する。
        /// </summary>
        /// <param name="identity">シーンの一意識別子。</param>
        /// <returns>対応するシーンリソース。見つからなければ null。</returns>
        public SceneResource? GetSceneResource(string identity)
        {
            EnsureDictionary();
            _dictionary!.TryGetValue(identity, out var resource);
            return resource;
        }

        private void OnEnable()
        {
            BuildDictionary();
        }

        private void EnsureDictionary()
        {
            if (_dictionary == null)
            {
                BuildDictionary();
            }
        }

        private void BuildDictionary()
        {
            _dictionary = new Dictionary<string, SceneResource>(_sceneResources.Count);
            foreach (var resource in _sceneResources)
            {
                if (resource == null) continue;

                if (!_dictionary.TryAdd(resource.Identity, resource))
                {
                    Debug.LogWarning(
                        $"[SceneResourceMap] Duplicate identity: {resource.Identity}");
                }
            }
        }
    }
}
