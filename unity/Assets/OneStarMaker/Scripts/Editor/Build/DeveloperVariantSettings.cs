#nullable enable

using UnityEditor;
using UnityEngine;

namespace OneStarMaker.Editor.Build
{
    /// <summary>
    /// 開発者ローカルの Variant プロファイル選択を UserSettings 配下に永続化する設定。
    /// </summary>
    /// <remarks>
    /// VCS 外 (UserSettings) に保存するため、各開発者が自分の Checkout 用プロファイルを
    /// Project Settings から選択できる。選択結果は GUID 文字列として保持する。
    /// </remarks>
    [FilePath("UserSettings/OneStarMaker/DeveloperVariantSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class DeveloperVariantSettings : ScriptableSingleton<DeveloperVariantSettings>
    {
        /// <summary>
        /// 選択中 <see cref="BuildVariantProfile"/> の Asset GUID。
        /// </summary>
        /// <remarks>
        /// UserSettings から Assets 配下への直接参照 (Object 参照) は、
        /// プロジェクト移動やアセット再インポートで参照切れしやすいため、GUID 文字列で保持する。
        /// </remarks>
        [SerializeField]
        private string _activeProfileGuid = string.Empty;

        /// <summary>
        /// 現在選択中の <see cref="BuildVariantProfile"/> の Asset GUID。
        /// 未選択時は空文字。
        /// </summary>
        public string ActiveProfileGuid => _activeProfileGuid;

        /// <summary>
        /// 選択中プロファイルの GUID を更新し、UserSettings へ永続化する。
        /// </summary>
        /// <param name="guid">
        /// 対象 <see cref="BuildVariantProfile"/> の Asset GUID。
        /// null は空文字として正規化される。
        /// </param>
        public void SetActiveProfileGuid(string guid)
        {
            _activeProfileGuid = guid ?? string.Empty;
            Save(true);
        }

        /// <summary>
        /// 現在選択中の <see cref="BuildVariantProfile"/> を解決して返す。
        /// </summary>
        /// <returns>
        /// GUID 未設定、パス解決失敗、または参照切れの場合は null。
        /// </returns>
        public BuildVariantProfile? GetActiveProfile()
        {
            if (string.IsNullOrEmpty(_activeProfileGuid))
            {
                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(_activeProfileGuid);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<BuildVariantProfile>(path);
        }

        /// <summary>
        /// 選択中プロファイルのリモート Addressables カタログ URL を返す。
        /// </summary>
        /// <returns>
        /// プロファイル未選択、または URL 未設定時は空文字。
        /// </returns>
        public string GetActiveRemoteCatalogUrl()
        {
            return GetActiveProfile()?.RemoteCatalogUrl ?? string.Empty;
        }
    }
}
