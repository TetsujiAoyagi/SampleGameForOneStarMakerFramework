#nullable enable

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace OneStarMaker.Editor.Build
{
    /// <summary>
    /// リモート配信ビルドに必要な Addressables 構成をワンショットで生成する Editor メニューコマンド。
    /// </summary>
    /// <remarks>
    /// <para>
    /// Remote プロファイル変数・リモート Addressables グループ・
    /// <see cref="BuildVariantProfile"/> (RemoteFull) をコードから作成し、
    /// Addressables 設定 YAML の手編集を避ける。
    /// </para>
    /// <para>
    /// Addressables のバージョン差やユーザー環境差で個々の API 呼び出しが失敗しうるため、
    /// 各ステップおよび各外部 API 呼び出しを try/catch で保護し、
    /// 部分的な失敗でも後続処理を継続する。
    /// </para>
    /// </remarks>
    internal static class VariantRemoteBuildSetup
    {
        private const string LogPrefix = "[VariantRemoteBuildSetup]";

        /// <summary>リモート配信用 Addressables グループ名。</summary>
        private const string RemoteGroupName = "Remote Distribution";

        /// <summary>RemoteFull BuildVariantProfile の保存先アセットパス。</summary>
        private const string RemoteFullProfileAssetPath = "Assets/OneStarMaker/Editor/BuildProfiles/RemoteFull.asset";

        /// <summary>SceneResourceMap / AlwaysIncludedAssets のコピー元 Production プロファイル。</summary>
        private const string ProductionProfileAssetPath = "Assets/OneStarMaker/Editor/BuildProfiles/Production.asset";

        /// <summary>BuildVariantProfile 保存先フォルダ。</summary>
        private const string BuildProfilesFolder = "Assets/OneStarMaker/Editor/BuildProfiles";

        /// <summary>Addressables Settings 上の Remote プロファイル表示名。</summary>
        private const string AddressablesProfileName = "Remote";

        /// <summary>Remote プロファイルの BuildPath 変数名。</summary>
        private const string RemoteBuildPathVariableName = "Remote.BuildPath";

        /// <summary>Remote プロファイルの LoadPath 変数名。</summary>
        private const string RemoteLoadPathVariableName = "Remote.LoadPath";

        /// <summary>
        /// リモート配信に必要な Addressables 構成を一括セットアップする。
        /// </summary>
        [MenuItem("OneStarMaker/Addressables/Setup Remote Distribution")]
        public static void Setup()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError(
                    $"{LogPrefix} AddressableAssetSettings が見つかりません。" +
                    "Addressables グループを作成してから再度実行してください。");
                return;
            }

            EnsureRemoteAddressablesProfile(settings);
            EnsureRemoteDistributionGroup(settings);
            EnsureRemoteFullProfile();
            SaveAddressablesSettings(settings);
            LogCompletionGuide();
        }

        /// <summary>
        /// Addressables プロファイル "Remote" と BuildPath / LoadPath 変数を作成または更新する。
        /// </summary>
        /// <param name="settings">Addressables Settings。</param>
        private static void EnsureRemoteAddressablesProfile(AddressableAssetSettings settings)
        {
            try
            {
                var profileSettings = settings.profileSettings;
                var profileNames = profileSettings.GetAllProfileNames();
                if (!profileNames.Contains(AddressablesProfileName))
                {
                    TryExecute(
                        $"AddressableAssetProfileSettings.AddProfile(\"{AddressablesProfileName}\")",
                        () => profileSettings.AddProfile(AddressablesProfileName, settings.activeProfileId));
                    LogSuccess($"Addressables プロファイル \"{AddressablesProfileName}\" を作成しました。");
                }
                else
                {
                    LogSkip($"Addressables プロファイル \"{AddressablesProfileName}\" は既に存在します。");
                }

                string profileId;
                try
                {
                    profileId = profileSettings.GetProfileId(AddressablesProfileName);
                }
                catch (Exception ex)
                {
                    LogFailure($"AddressableAssetProfileSettings.GetProfileId(\"{AddressablesProfileName}\")", ex);
                    return;
                }

                if (string.IsNullOrEmpty(profileId))
                {
                    LogFailure($"Addressables プロファイル \"{AddressablesProfileName}\" の ID を取得できませんでした。");
                    return;
                }

                SetProfileVariable(
                    profileSettings,
                    profileId,
                    RemoteBuildPathVariableName,
                    "ServerData/[BuildTarget]");
                SetProfileVariable(
                    profileSettings,
                    profileId,
                    RemoteLoadPathVariableName,
                    "http://localhost:8080/[BuildTarget]");

                LogSuccess(
                    "Remote プロファイル変数を設定しました。" +
                    " Remote.LoadPath は実際のリモート PC のホスト/IP に書き換えてください。");
            }
            catch (Exception ex)
            {
                LogFailure("ステップ1: Addressables プロファイル \"Remote\" の作成/確保", ex);
            }
        }

        /// <summary>
        /// 指定プロファイルに Addressables プロファイル変数を設定する。
        /// 変数が未定義の場合のみ <see cref="AddressableAssetProfileSettings.CreateValue"/> で作成する。
        /// </summary>
        /// <param name="profileSettings">Addressables プロファイル設定。</param>
        /// <param name="profileId">対象プロファイル ID。</param>
        /// <param name="variableName">変数名 (例: Remote.BuildPath)。</param>
        /// <param name="value">設定値。</param>
        private static void SetProfileVariable(
            AddressableAssetProfileSettings profileSettings,
            string profileId,
            string variableName,
            string value)
        {
            var variableNames = profileSettings.GetVariableNames();
            if (!variableNames.Contains(variableName))
            {
                TryExecute(
                    $"AddressableAssetProfileSettings.CreateValue(\"{variableName}\")",
                    () => profileSettings.CreateValue(variableName, value));
            }
            else
            {
                LogSkip($"プロファイル変数 \"{variableName}\" は既に存在します。値のみ更新します。");
            }

            TryExecute(
                $"AddressableAssetProfileSettings.SetValue(profileId, \"{variableName}\")",
                () => profileSettings.SetValue(profileId, variableName, value));
        }

        /// <summary>
        /// リモート配信用 Addressables グループ "Remote Distribution" を作成または確保する。
        /// </summary>
        /// <param name="settings">Addressables Settings。</param>
        private static void EnsureRemoteDistributionGroup(AddressableAssetSettings settings)
        {
            try
            {
                var existingGroup = settings.FindGroup(RemoteGroupName);
                if (existingGroup != null)
                {
                    LogSkip($"Addressables グループ \"{RemoteGroupName}\" は既に存在します。");
                    return;
                }

                AddressableAssetGroup? group = null;
                TryExecute(
                    $"AddressableAssetSettings.CreateGroup(\"{RemoteGroupName}\")",
                    () =>
                    {
                        group = settings.CreateGroup(
                            RemoteGroupName,
                            setAsDefaultGroup: false,
                            readOnly: false,
                            postEvent: false,
                            schemasToCopy: null,
                            typeof(BundledAssetGroupSchema));
                    });

                if (group == null)
                {
                    LogFailure($"Addressables グループ \"{RemoteGroupName}\" の作成に失敗しました。");
                    return;
                }

                BundledAssetGroupSchema? schema = null;
                TryExecute(
                    "AddressableAssetGroup.GetSchema<BundledAssetGroupSchema>()",
                    () => schema = group.GetSchema<BundledAssetGroupSchema>());

                if (schema == null)
                {
                    LogFailure($"グループ \"{RemoteGroupName}\" の BundledAssetGroupSchema を取得できませんでした。");
                    return;
                }

                TryExecute(
                    "BundledAssetGroupSchema.BuildPath.SetVariableByName(Remote.BuildPath)",
                    () => schema.BuildPath.SetVariableByName(settings, RemoteBuildPathVariableName));
                TryExecute(
                    "BundledAssetGroupSchema.LoadPath.SetVariableByName(Remote.LoadPath)",
                    () => schema.LoadPath.SetVariableByName(settings, RemoteLoadPathVariableName));

                LogSuccess(
                    $"Addressables グループ \"{RemoteGroupName}\" を作成し、" +
                    "BuildPath / LoadPath を Remote プロファイル変数にバインドしました。");
            }
            catch (Exception ex)
            {
                LogFailure("ステップ2: リモートグループ \"Remote Distribution\" の作成/確保", ex);
            }
        }

        /// <summary>
        /// リモート配信用 <see cref="BuildVariantProfile"/> (RemoteFull) を作成または確保する。
        /// </summary>
        private static void EnsureRemoteFullProfile()
        {
            try
            {
                if (AssetDatabase.LoadAssetAtPath<BuildVariantProfile>(RemoteFullProfileAssetPath) != null)
                {
                    LogSkip($"BuildVariantProfile \"{RemoteFullProfileAssetPath}\" は既に存在します。");
                    return;
                }

                BuildVariantProfile? profile = null;
                TryExecute(
                    "ScriptableObject.CreateInstance<BuildVariantProfile>()",
                    () => profile = ScriptableObject.CreateInstance<BuildVariantProfile>());

                if (profile == null)
                {
                    LogFailure("BuildVariantProfile インスタンスの作成に失敗しました。");
                    return;
                }

                profile.name = "RemoteFull";

                TryExecute(
                    "SerializedObject による RemoteFull フィールド設定",
                    () => ConfigureRemoteFullProfileFields(profile));

                TryExecute(
                    "Production プロファイルから SceneResourceMap / AlwaysIncludedAssets をコピー",
                    () => CopyProductionProfileReferences(profile));

                TryExecute(
                    $"BuildProfiles フォルダの確保 ({BuildProfilesFolder})",
                    EnsureBuildProfilesFolderExists);

                var created = false;
                TryExecute(
                    $"AssetDatabase.CreateAsset(profile, \"{RemoteFullProfileAssetPath}\")",
                    () =>
                    {
                        AssetDatabase.CreateAsset(profile, RemoteFullProfileAssetPath);
                        created = true;
                    });

                if (created)
                {
                    LogSuccess($"BuildVariantProfile \"{RemoteFullProfileAssetPath}\" を作成しました。");
                }
            }
            catch (Exception ex)
            {
                LogFailure("ステップ3: RemoteFull BuildVariantProfile の作成/確保", ex);
            }
        }

        /// <summary>
        /// RemoteFull プロファイルの Variant ホワイトリストとリモートグループ名を設定する。
        /// </summary>
        /// <param name="profile">設定対象の BuildVariantProfile。</param>
        private static void ConfigureRemoteFullProfileFields(BuildVariantProfile profile)
        {
            var serializedObject = new SerializedObject(profile);
            serializedObject.FindProperty("_remoteGroupName").stringValue = RemoteGroupName;

            var whitelistProperty = serializedObject.FindProperty("_variantWhitelist");
            var whitelistValues = new[] { string.Empty, "Full", "Whitebox" };
            whitelistProperty.arraySize = whitelistValues.Length;
            for (var index = 0; index < whitelistValues.Length; index++)
            {
                whitelistProperty.GetArrayElementAtIndex(index).stringValue = whitelistValues[index];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();

            LogSuccess(
                "RemoteFull の VariantWhitelist と RemoteGroupName を設定しました。" +
                " 必要に応じて Variant 名を追記してください。");
        }

        /// <summary>
        /// Production プロファイルから SceneResourceMap と AlwaysIncludedAssets を RemoteFull へコピーする。
        /// </summary>
        /// <param name="targetProfile">コピー先 BuildVariantProfile。</param>
        private static void CopyProductionProfileReferences(BuildVariantProfile targetProfile)
        {
            var productionProfile = AssetDatabase.LoadAssetAtPath<BuildVariantProfile>(ProductionProfileAssetPath);
            if (productionProfile == null)
            {
                LogSkip(
                    $"Production プロファイル \"{ProductionProfileAssetPath}\" が見つかりません。" +
                    " SceneResourceMap と AlwaysIncludedAssets を手動設定してください。");
                return;
            }

            var sourceObject = new SerializedObject(productionProfile);
            var targetObject = new SerializedObject(targetProfile);

            var sourceSceneResourceMap = sourceObject.FindProperty("_sceneResourceMap");
            var targetSceneResourceMap = targetObject.FindProperty("_sceneResourceMap");
            if (sourceSceneResourceMap != null && targetSceneResourceMap != null)
            {
                TryExecute(
                    "_sceneResourceMap のコピー",
                    () => targetSceneResourceMap.objectReferenceValue = sourceSceneResourceMap.objectReferenceValue);
            }

            var sourceAlwaysIncluded = sourceObject.FindProperty("_alwaysIncludedAssets");
            var targetAlwaysIncluded = targetObject.FindProperty("_alwaysIncludedAssets");
            if (sourceAlwaysIncluded != null && targetAlwaysIncluded != null)
            {
                TryExecute(
                    "_alwaysIncludedAssets のコピー",
                    () =>
                    {
                        targetAlwaysIncluded.arraySize = sourceAlwaysIncluded.arraySize;
                        for (var index = 0; index < sourceAlwaysIncluded.arraySize; index++)
                        {
                            targetAlwaysIncluded.GetArrayElementAtIndex(index).objectReferenceValue =
                                sourceAlwaysIncluded.GetArrayElementAtIndex(index).objectReferenceValue;
                        }
                    });
            }

            targetObject.ApplyModifiedPropertiesWithoutUndo();
            LogSuccess(
                $"Production プロファイルから SceneResourceMap / AlwaysIncludedAssets をコピーしました。" +
                $" ({ProductionProfileAssetPath})");
        }

        /// <summary>
        /// BuildVariantProfile 保存先フォルダが無ければ再帰的に作成する。
        /// </summary>
        private static void EnsureBuildProfilesFolderExists()
        {
            if (AssetDatabase.IsValidFolder(BuildProfilesFolder))
            {
                return;
            }

            const string parentFolder = "Assets/OneStarMaker/Editor";
            if (!AssetDatabase.IsValidFolder(parentFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/OneStarMaker"))
                {
                    TryExecute(
                        "AssetDatabase.CreateFolder(Assets, OneStarMaker)",
                        () => AssetDatabase.CreateFolder("Assets", "OneStarMaker"));
                }

                TryExecute(
                    "AssetDatabase.CreateFolder(Assets/OneStarMaker, Editor)",
                    () => AssetDatabase.CreateFolder("Assets/OneStarMaker", "Editor"));
            }

            TryExecute(
                "AssetDatabase.CreateFolder(Assets/OneStarMaker/Editor, BuildProfiles)",
                () => AssetDatabase.CreateFolder(parentFolder, "BuildProfiles"));
        }

        /// <summary>
        /// Addressables Settings の変更をディスクへ保存し、アセット DB を更新する。
        /// </summary>
        /// <param name="settings">Addressables Settings。</param>
        private static void SaveAddressablesSettings(AddressableAssetSettings settings)
        {
            try
            {
                TryExecute(
                    "AddressableAssetSettings.SetDirty(BatchModification)",
                    () => settings.SetDirty(
                        AddressableAssetSettings.ModificationEvent.BatchModification,
                        null,
                        postEvent: true,
                        settingsModified: true));
                TryExecute("AssetDatabase.SaveAssets()", AssetDatabase.SaveAssets);
                TryExecute("AssetDatabase.Refresh()", AssetDatabase.Refresh);
                LogSuccess("Addressables Settings とアセットを保存しました。");
            }
            catch (Exception ex)
            {
                LogFailure("ステップ4: Addressables Settings の保存", ex);
            }
        }

        /// <summary>
        /// セットアップ完了後にユーザーが行うべき手順をログ出力する。
        /// </summary>
        private static void LogCompletionGuide()
        {
            Debug.Log(
                $"{LogPrefix} セットアップ処理を完了しました。次の手順を確認してください:\n" +
                "1. Addressables の Remote プロファイルで Remote.LoadPath を実際のリモート PC の URL に変更\n" +
                "2. RemoteFull プロファイル (Assets/OneStarMaker/Editor/BuildProfiles/RemoteFull.asset) の " +
                "SceneResourceMap / AlwaysIncludedAssets を確認\n" +
                "3. VariantFilteringBuildScript の Active Profile に RemoteFull を割り当ててリモートビルド");
        }

        /// <summary>
        /// 外部 API 呼び出しを try/catch で保護して実行する。
        /// </summary>
        /// <param name="operationLabel">ログ用の操作名。</param>
        /// <param name="action">実行する処理。</param>
        /// <returns>例外なく完了した場合 true。</returns>
        private static bool TryExecute(string operationLabel, Action action)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception ex)
            {
                LogFailure(operationLabel, ex);
                return false;
            }
        }

        /// <summary>成功ログを出力する。</summary>
        /// <param name="message">メッセージ。</param>
        private static void LogSuccess(string message)
        {
            Debug.Log($"{LogPrefix} [成功] {message}");
        }

        /// <summary>スキップログを出力する。</summary>
        /// <param name="message">メッセージ。</param>
        private static void LogSkip(string message)
        {
            Debug.Log($"{LogPrefix} [スキップ] {message}");
        }

        /// <summary>失敗ログを出力する。</summary>
        /// <param name="message">メッセージ。</param>
        /// <param name="ex">捕捉した例外。省略可。</param>
        private static void LogFailure(string message, Exception? ex = null)
        {
            if (ex != null)
            {
                Debug.LogError($"{LogPrefix} [失敗] {message}: {ex}");
            }
            else
            {
                Debug.LogError($"{LogPrefix} [失敗] {message}");
            }
        }
    }
}
