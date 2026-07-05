#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OneStarMaker.Editor.Build
{
    /// <summary>
    /// Project Settings に開発者ローカルの Variant プロファイル選択 UI を提供する。
    /// </summary>
    internal static class DeveloperVariantSettingsProvider
    {
        /// <summary>
        /// Project Settings の OneStarMaker / Variant ページを生成する。
        /// </summary>
        /// <returns>Variant Checkout 用の SettingsProvider。</returns>
        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Project/OneStarMaker/Variant", SettingsScope.Project)
            {
                label = "Variant Checkout",
                keywords = new HashSet<string>(new[]
                {
                    "Variant",
                    "Checkout",
                    "Addressables",
                    "Remote",
                    "OneStarMaker",
                    "BuildVariantProfile",
                }),
                guiHandler = DrawGui,
            };
        }

        /// <summary>
        /// Variant Checkout 設定 UI を描画する。
        /// </summary>
        /// <param name="searchContext">Settings 検索コンテキスト (未使用)。</param>
        static void DrawGui(string searchContext)
        {
            EditorGUILayout.LabelField(
                "開発者ローカルの Variant プロファイル選択 (UserSettings 保存・VCS 外)",
                EditorStyles.boldLabel);
            EditorGUILayout.Space();

            var profiles = LoadAllProfiles();
            var profileGuids = profiles
                .Select(profile => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(profile)))
                .ToList();

            var activeGuid = DeveloperVariantSettings.instance.ActiveProfileGuid;
            var selectedIndex = 0;
            if (!string.IsNullOrEmpty(activeGuid))
            {
                var index = profileGuids.IndexOf(activeGuid);
                if (index >= 0)
                {
                    selectedIndex = index + 1;
                }
            }

            var labels = new List<string> { "(None)" };
            labels.AddRange(profiles.Select(profile => profile.name));

            EditorGUI.BeginChangeCheck();
            var newIndex = EditorGUILayout.Popup("Active Profile", selectedIndex, labels.ToArray());
            if (EditorGUI.EndChangeCheck())
            {
                var newGuid = newIndex <= 0
                    ? string.Empty
                    : profileGuids[newIndex - 1];
                DeveloperVariantSettings.instance.SetActiveProfileGuid(newGuid);
            }

            EditorGUILayout.Space();

            var activeProfile = DeveloperVariantSettings.instance.GetActiveProfile();
            if (activeProfile == null)
            {
                EditorGUILayout.HelpBox(
                    "未選択。Default 動作(デフォルト Variant のみ、リモートフォールバック無効)になります",
                    MessageType.Warning);
                return;
            }

            DrawReadOnlyProfileDetails(activeProfile);
        }

        /// <summary>
        /// プロジェクト内の全 <see cref="BuildVariantProfile"/> を読み込む。
        /// </summary>
        /// <returns>アセット名順にソートしたプロファイル一覧。</returns>
        static List<BuildVariantProfile> LoadAllProfiles()
        {
            var guids = AssetDatabase.FindAssets("t:BuildVariantProfile");
            var profiles = new List<BuildVariantProfile>(guids.Length);

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                var profile = AssetDatabase.LoadAssetAtPath<BuildVariantProfile>(path);
                if (profile != null)
                {
                    profiles.Add(profile);
                }
            }

            profiles.Sort((left, right) => string.Compare(left.name, right.name, System.StringComparison.Ordinal));
            return profiles;
        }

        /// <summary>
        /// 選択中プロファイルの主要設定を読み取り専用で表示する。
        /// </summary>
        /// <param name="profile">表示対象のプロファイル。</param>
        static void DrawReadOnlyProfileDetails(BuildVariantProfile profile)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.LabelField("Profile Details", EditorStyles.boldLabel);

                var whitelist = profile.VariantWhitelist
                    .Select(variant => string.IsNullOrEmpty(variant) ? "(default)" : variant);
                EditorGUILayout.TextField("Variant Whitelist", string.Join(", ", whitelist));

                var remoteCatalogUrl = string.IsNullOrEmpty(profile.RemoteCatalogUrl)
                    ? "(none / local only)"
                    : profile.RemoteCatalogUrl;
                EditorGUILayout.TextField("Remote Catalog URL", remoteCatalogUrl);

                var firstSceneIdentify = string.IsNullOrEmpty(profile.FirstSceneIdentify)
                    ? "(default)"
                    : profile.FirstSceneIdentify;
                EditorGUILayout.TextField("First Scene Identify", firstSceneIdentify);
            }
        }
    }
}
