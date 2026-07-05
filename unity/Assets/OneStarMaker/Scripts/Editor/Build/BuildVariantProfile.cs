#nullable enable

using System.Collections.Generic;
using OneStarMaker.Runtime.SceneSystem;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace OneStarMaker.Editor.Build
{
    /// <summary>
    /// Variant ホワイトリスト適用対象の Addressables ビルド設定。
    /// Unity Addressables の Profile 機能とは別物。
    /// </summary>
    [CreateAssetMenu(
        fileName = "BuildVariantProfile",
        menuName = "OneStarMaker/Build/Variant Profile")]
    public sealed class BuildVariantProfile : ScriptableObject
    {
        /// <summary>同梱を許可する Variant 名一覧。空リスト時はデフォルト Variant のみ。</summary>
        [SerializeField]
        private List<string> _variantWhitelist = new() { string.Empty };

        /// <summary>SceneResource 走査対象の SceneResourceMap。</summary>
        [SerializeField]
        private SceneResourceMap? _sceneResourceMap;

        /// <summary>Variant 判定対象外で必ず同梱する Bootstrap 等の AssetReference。</summary>
        [SerializeField]
        private List<AssetReference> _alwaysIncludedAssets = new();

        /// <summary>Included だが未登録の GUID を一時追加する Addressables グループ名。</summary>
        [SerializeField]
        private string _targetAddressablesGroupName = "Default Local Group";

        /// <summary>
        /// このプロファイル使用時にフォールバック先とするリモート Addressables カタログの URL。
        /// 空文字の場合はリモートフォールバックを無効にする。
        /// 例: <c>http://buildpc:8080/StandaloneWindows64/catalog.json</c>
        /// </summary>
        [SerializeField]
        private string _remoteCatalogUrl = string.Empty;

        /// <summary>
        /// この Variant でビルド / Play する際の論理初回シーンの識別子。
        /// 空文字の場合は AppInitializer の既定値（現状 <c>"Title"</c>）を使用する。
        /// Build Settings の Scene 0 を差し替えるのではなく、起動後にロードする論理シーンを差し替える点に注意。
        /// </summary>
        [SerializeField]
        private string _firstSceneIdentify = string.Empty;

        /// <summary>
        /// リモート配信ビルド時に Included アセットを同期する Addressables グループ名。
        /// 空文字の場合は従来通り <see cref="TargetAddressablesGroupName"/>（ローカルグループ）へ同期する。
        /// </summary>
        [SerializeField]
        private string _remoteGroupName = string.Empty;

        /// <summary>同梱を許可する Variant 名一覧。空リスト時はデフォルト Variant のみ。</summary>
        public IReadOnlyList<string> VariantWhitelist => _variantWhitelist;

        /// <summary>SceneResource 走査用マップ。</summary>
        public SceneResourceMap? SceneResourceMap => _sceneResourceMap;

        /// <summary>Variant に関係なく必ず Addressables build に含める Bootstrap 等の参照。</summary>
        public IReadOnlyList<AssetReference> AlwaysIncludedAssets => _alwaysIncludedAssets;

        /// <summary>whitelist 同期先 Addressables グループ名。</summary>
        public string TargetAddressablesGroupName => _targetAddressablesGroupName;

        /// <summary>
        /// リモート Addressables カタログのフォールバック URL。
        /// 空文字の場合はリモートフォールバック無効。
        /// </summary>
        public string RemoteCatalogUrl => _remoteCatalogUrl;

        /// <summary>
        /// 論理初回シーンの識別子。
        /// 空文字の場合は AppInitializer の既定（現状 <c>"Title"</c>）を使用。
        /// </summary>
        public string FirstSceneIdentify => _firstSceneIdentify;

        /// <summary>
        /// リモート配信時の Included アセット同期先 Addressables グループ名。
        /// 空文字の場合は <see cref="TargetAddressablesGroupName"/> へ同期。
        /// </summary>
        public string RemoteGroupName => _remoteGroupName;
    }
}
