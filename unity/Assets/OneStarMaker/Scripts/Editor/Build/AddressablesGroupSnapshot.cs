#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace OneStarMaker.Editor.Build
{
    /// <summary>
    /// Addressables グループ変更のスナップショットと復元。
    /// Variant フィルタビルド中だけ Addressables グループを一時変更し、
    /// ビルド完了後（または中断後）に Editor 上の設定を元に戻す。
    /// </summary>
    internal sealed class AddressablesGroupSnapshot : IDisposable
    {
        /// <summary>
        /// 中断時の復元用スナップショット保存先。
        /// Library 配下なので Git 管理外。Editor クラッシュ後も残る。
        /// </summary>
        private const string SnapshotFilePath = "Library/OneStarMaker/VariantFilteringBuildSnapshot.json";

        private readonly AddressableAssetSettings _settings;
        /// <summary>今回のビルドで行った追加/削除の差分。Dispose 時に逆操作する。</summary>
        private readonly SnapshotFileData _data = new();
        private bool _disposed;

        private AddressablesGroupSnapshot(AddressableAssetSettings settings)
        {
            _settings = settings;
        }

        public static AddressablesGroupSnapshot Capture(AddressableAssetSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var snapshot = new AddressablesGroupSnapshot(settings);
            // 空の snapshot ファイルを先に作り、以降の Record* で逐次更新する。
            snapshot.WriteSnapshotFile();
            return snapshot;
        }

        /// <summary>
        /// 前回ビルドが中断された場合に残った snapshot を復元する。
        /// 通常ビルドの冒頭で呼び、Editor 上の Addressables 設定を正常化する。
        /// </summary>
        public static void RestorePending(AddressableAssetSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (!File.Exists(SnapshotFilePath))
            {
                return;
            }

            try
            {
                var data = ReadSnapshotFile();
                if (data == null)
                {
                    DeleteSnapshotFile();
                    return;
                }

                RestoreData(settings, data);
                settings.SetDirty(
                    AddressableAssetSettings.ModificationEvent.BatchModification,
                    null,
                    postEvent: true,
                    settingsModified: true);
                AssetDatabase.SaveAssets();
                DeleteSnapshotFile();

                Debug.LogWarning(
                    "[VariantFilteringBuildScript] Restored pending Addressables group snapshot from a previous interrupted build.");
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[VariantFilteringBuildScript] Failed to restore pending Addressables group snapshot: {ex}");
            }
        }

        /// <summary>whitelist 同期で新規追加した GUID を記録する。</summary>
        public void RecordAdded(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return;
            }

            if (_data.AddedGuids.Contains(guid))
            {
                return;
            }

            _data.AddedGuids.Add(guid);
            WriteSnapshotFile();
        }

        /// <summary>
        /// whitelist 同期で一時削除した entry を記録する。
        /// address / group / labels も保存し、復元時に元の状態へ戻す。
        /// </summary>
        public void RecordRemoved(AddressableAssetEntry entry)
        {
            if (entry?.parentGroup == null)
            {
                return;
            }

            if (_data.RemovedEntries.Exists(item => item.Guid == entry.guid))
            {
                return;
            }

            _data.RemovedEntries.Add(new RemovedEntrySnapshot
            {
                Guid = entry.guid,
                Address = entry.address,
                GroupGuid = entry.parentGroup.Guid,
                ReadOnly = entry.ReadOnly,
                Labels = new List<string>(entry.labels),
            });
            WriteSnapshotFile();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                RestoreData(_settings, _data);

                _settings.SetDirty(
                    AddressableAssetSettings.ModificationEvent.BatchModification,
                    null,
                    postEvent: true,
                    settingsModified: true);
                AssetDatabase.SaveAssets();
                DeleteSnapshotFile();
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[VariantFilteringBuildScript] Failed to restore Addressables group snapshot: {ex}");
            }
            finally
            {
                _disposed = true;
            }
        }

        /// <summary>記録済み差分を逆操作して Addressables 設定を元に戻す。</summary>
        private static void RestoreData(AddressableAssetSettings settings, SnapshotFileData data)
        {
            // 追加した entry を先に除去する。
            for (var i = data.AddedGuids.Count - 1; i >= 0; i--)
            {
                settings.RemoveAssetEntry(data.AddedGuids[i], postEvent: false);
            }

            // 削除した entry を元のグループ・address・labels 付きで復元する。
            foreach (var removed in data.RemovedEntries)
            {
                RestoreRemovedEntry(settings, removed);
            }
        }

        private static void RestoreRemovedEntry(AddressableAssetSettings settings, RemovedEntrySnapshot removed)
        {
            if (settings.FindAssetEntry(removed.Guid) != null)
            {
                return;
            }

            // 元グループが消えていた場合は DefaultGroup へフォールバック。
            var group = FindGroupByGuid(settings, removed.GroupGuid);
            if (group == null)
            {
                group = settings.DefaultGroup;
            }

            if (group == null)
            {
                return;
            }

            var entry = settings.CreateOrMoveEntry(removed.Guid, group, removed.ReadOnly, postEvent: false);
            if (entry == null)
            {
                return;
            }

            entry.SetAddress(removed.Address, postEvent: false);
            entry.labels.Clear();
            foreach (var label in removed.Labels)
            {
                entry.SetLabel(label, enable: true, postEvent: false, force: true);
            }
        }

        private static AddressableAssetGroup? FindGroupByGuid(
            AddressableAssetSettings settings,
            string groupGuid)
        {
            foreach (var group in settings.groups)
            {
                if (group != null && group.Guid == groupGuid)
                {
                    return group;
                }
            }

            return null;
        }

        private void WriteSnapshotFile()
        {
            try
            {
                var directory = Path.GetDirectoryName(SnapshotFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(SnapshotFilePath, JsonUtility.ToJson(_data, prettyPrint: true));
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[VariantFilteringBuildScript] Failed to persist Addressables group snapshot: {ex}");
            }
        }

        private static SnapshotFileData? ReadSnapshotFile()
        {
            var json = File.ReadAllText(SnapshotFilePath);
            return string.IsNullOrWhiteSpace(json)
                ? null
                : JsonUtility.FromJson<SnapshotFileData>(json);
        }

        private static void DeleteSnapshotFile()
        {
            if (File.Exists(SnapshotFilePath))
            {
                File.Delete(SnapshotFilePath);
            }
        }

        /// <summary>Library へ JSON 永続化する差分データ。</summary>
        [Serializable]
        private sealed class SnapshotFileData
        {
            /// <summary>今回のビルドで CreateOrMoveEntry した GUID 一覧。</summary>
            public List<string> AddedGuids = new();

            /// <summary>今回のビルドで RemoveAssetEntry した entry の復元情報。</summary>
            public List<RemovedEntrySnapshot> RemovedEntries = new();
        }

        /// <summary>RemoveAssetEntry 前に保存する entry のスナップショット。</summary>
        [Serializable]
        private sealed class RemovedEntrySnapshot
        {
            public string Guid = string.Empty;

            public string Address = string.Empty;

            /// <summary>所属していた AddressableAssetGroup の GUID。</summary>
            public string GroupGuid = string.Empty;

            public bool ReadOnly;

            public List<string> Labels = new();
        }
    }
}
