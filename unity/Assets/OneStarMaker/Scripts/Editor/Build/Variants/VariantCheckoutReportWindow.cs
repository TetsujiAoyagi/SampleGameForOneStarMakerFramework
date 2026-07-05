#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OneStarMaker.Editor.Build
{
    /// <summary>
    /// アクティブな Variant プロファイルに対し、
    /// ローカル Checkout すべきアセット / リモート解決されるアセット / 解決不能なアセットを
    /// 開発者へ提示する Editor ウィンドウ。
    /// </summary>
    /// <remarks>
    /// <see cref="DeveloperVariantSettings"/> で選択中の <see cref="BuildVariantProfile"/> を起点に
    /// <see cref="VariantWhitelistBuilder"/> と <see cref="AssetDependencyClosure"/> を組み合わせ、
    /// sparse-checkout やローカル Play 前の不足検知に使うレポートを生成する。
    /// </remarks>
    public sealed class VariantCheckoutReportWindow : EditorWindow
    {
        /// <summary>
        /// GUID とプロジェクト内アセットパスの表示用ペア。
        /// パス未解決時は <c>&lt;unresolved&gt;</c> を保持する。
        /// </summary>
        private readonly struct GuidPathEntry
        {
            /// <summary>対象アセットの GUID。</summary>
            public string Guid { get; }

            /// <summary>
            /// <see cref="AssetDatabase.GUIDToAssetPath"/> で解決したパス。
            /// 空の場合は <c>&lt;unresolved&gt;</c>。
            /// </summary>
            public string Path { get; }

            /// <summary>表示用ペアを構築する。</summary>
            /// <param name="guid">アセット GUID。</param>
            /// <param name="path">解決済みパス。空なら <c>&lt;unresolved&gt;</c> に正規化される。</param>
            public GuidPathEntry(string guid, string path)
            {
                Guid = guid;
                Path = string.IsNullOrEmpty(path) ? "<unresolved>" : path;
            }
        }

        /// <summary>レポート生成済みかどうか。</summary>
        private bool _reportBuilt;

        /// <summary>アクティブプロファイルが未選択の場合 true。</summary>
        private bool _profileNotSelected;

        /// <summary>選択中プロファイルの表示名。</summary>
        private string _profileName = string.Empty;

        /// <summary>選択中プロファイルの RemoteCatalogUrl (生値)。</summary>
        private string _remoteCatalogUrl = string.Empty;

        /// <summary>RemoteCatalogUrl が非空の場合 true (リモートフォールバック有効)。</summary>
        private bool _remoteFallbackEnabled;

        /// <summary>依存閉包がローカルで完結する Included GUID 一覧。</summary>
        private List<GuidPathEntry> _localCompleteEntries = new();

        /// <summary>ローカル欠損だがリモートカタログで解決可能とみなす Included GUID 一覧。</summary>
        private List<GuidPathEntry> _remoteResolveEntries = new();

        /// <summary>ローカル欠損かつリモートフォールバックも無い Included GUID 一覧。</summary>
        private List<GuidPathEntry> _errorEntries = new();

        /// <summary>
        /// sparse-checkout 等で取得すべき <c>Assets/</c> 配下パス一覧 (重複排除・ソート済み)。
        /// </summary>
        private List<string> _requiredAssetPaths = new();

        /// <summary>whitelist 構築時のエラーメッセージ。</summary>
        private List<string> _whitelistErrors = new();

        /// <summary>whitelist 構築時の警告メッセージ。</summary>
        private List<string> _whitelistWarnings = new();

        /// <summary>LocalComplete リストのスクロール位置。</summary>
        private Vector2 _localCompleteScroll;

        /// <summary>RemoteResolve リストのスクロール位置。</summary>
        private Vector2 _remoteResolveScroll;

        /// <summary>Error リストのスクロール位置。</summary>
        private Vector2 _errorScroll;

        /// <summary>
        /// メニューから Checkout Report ウィンドウを開く。
        /// </summary>
        [MenuItem("OneStarMaker/Variant/Checkout Report")]
        public static void Open()
        {
            GetWindow<VariantCheckoutReportWindow>("Variant Checkout Report");
        }

        /// <summary>
        /// ウィンドウ有効化時に初回レポートを自動生成する。
        /// </summary>
        private void OnEnable()
        {
            BuildReport();
        }

        /// <summary>
        /// IMGUI でレポート内容を描画する。
        /// </summary>
        private void OnGUI()
        {
            if (!_reportBuilt)
            {
                BuildReport();
            }

            EditorGUILayout.Space(4f);

            if (GUILayout.Button("Refresh"))
            {
                BuildReport();
            }

            EditorGUILayout.Space(4f);

            if (_profileNotSelected)
            {
                EditorGUILayout.HelpBox(
                    "Project Settings > OneStarMaker > Variant でプロファイルを選択してください",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Profile", _profileName, EditorStyles.boldLabel);

            var remoteDisplay = string.IsNullOrEmpty(_remoteCatalogUrl)
                ? "(none / local only)"
                : _remoteCatalogUrl;
            EditorGUILayout.LabelField("Remote Catalog URL", remoteDisplay);

            EditorGUILayout.Space(4f);

            DrawWhitelistMessages();

            EditorGUILayout.Space(4f);

            EditorGUILayout.LabelField(
                "Summary",
                $"LocalComplete: {_localCompleteEntries.Count}  /  " +
                $"RemoteResolve: {_remoteResolveEntries.Count}  /  " +
                $"Error: {_errorEntries.Count}",
                EditorStyles.boldLabel);

            if (_errorEntries.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "以下のアセットはローカルにもリモート設定にも無く、ロードできません",
                    MessageType.Error);
            }

            if (_remoteFallbackEnabled && _remoteResolveEntries.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "RemoteResolve に分類されたアセットは、リモートカタログに全 Variant が同梱されている前提で解決可能とみなします。" +
                    "リモート実体の照合までは行いません。",
                    MessageType.Info);
            }

            EditorGUILayout.Space(4f);

            DrawGuidList("LocalComplete (ローカルで依存閉包が完結)", _localCompleteEntries, ref _localCompleteScroll);
            DrawGuidList("RemoteResolve (リモートカタログで解決可能とみなす)", _remoteResolveEntries, ref _remoteResolveScroll);
            DrawGuidList("Error (ローカル・リモートとも解決不可)", _errorEntries, ref _errorScroll);

            EditorGUILayout.Space(8f);

            if (GUILayout.Button("Copy required asset paths to clipboard"))
            {
                CopyRequiredPathsToClipboard();
            }
        }

        /// <summary>
        /// whitelist 構築結果の Errors / Warnings を HelpBox で列挙する。
        /// </summary>
        private void DrawWhitelistMessages()
        {
            if (_whitelistErrors.Count > 0)
            {
                EditorGUILayout.HelpBox(BuildMessageBlock("Whitelist Errors", _whitelistErrors), MessageType.Error);
            }

            if (_whitelistWarnings.Count > 0)
            {
                EditorGUILayout.HelpBox(BuildMessageBlock("Whitelist Warnings", _whitelistWarnings), MessageType.Warning);
            }
        }

        /// <summary>
        /// 見出し付きの複数行メッセージ文字列を組み立てる。
        /// </summary>
        /// <param name="title">HelpBox 先頭行の見出し。</param>
        /// <param name="messages">列挙するメッセージ群。</param>
        /// <returns>改行連結された表示用文字列。</returns>
        private static string BuildMessageBlock(string title, IReadOnlyList<string> messages)
        {
            var builder = new StringBuilder();
            builder.AppendLine(title);

            foreach (var message in messages)
            {
                builder.Append("- ");
                builder.AppendLine(message);
            }

            return builder.ToString().TrimEnd();
        }

        /// <summary>
        /// GUID / パス一覧をスクロール可能な ReadOnly テキスト領域で描画する。
        /// </summary>
        /// <param name="title">セクション見出し。</param>
        /// <param name="entries">表示対象エントリ。</param>
        /// <param name="scroll">スクロール位置 (呼び出し元で保持)。</param>
        private static void DrawGuidList(
            string title,
            IReadOnlyList<GuidPathEntry> entries,
            ref Vector2 scroll)
        {
            EditorGUILayout.LabelField($"{title} ({entries.Count})", EditorStyles.boldLabel);

            var content = entries.Count == 0
                ? "(none)"
                : string.Join("\n", entries.Select(e => $"{e.Guid}: {e.Path}"));

            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(160f));
            EditorGUILayout.TextArea(content, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// アクティブプロファイルと whitelist から Checkout レポートを再構築する。
        /// </summary>
        /// <remarks>
        /// 各 Included GUID について個別に <see cref="AssetDependencyClosure.Compute"/> を実行し、
        /// LocalComplete / RemoteResolve / Error の 3 分類を行う。
        /// さらに全 Included をまとめた閉包から sparse-checkout 用パス一覧を生成する。
        /// </remarks>
        private void BuildReport()
        {
            ResetReportState();

            var profile = DeveloperVariantSettings.instance.GetActiveProfile();
            if (profile == null)
            {
                _profileNotSelected = true;
                _reportBuilt = true;
                return;
            }

            _profileName = profile.name;
            _remoteCatalogUrl = profile.RemoteCatalogUrl ?? string.Empty;
            _remoteFallbackEnabled = !string.IsNullOrEmpty(_remoteCatalogUrl);

            var whitelistResult = VariantWhitelistBuilder.Build(profile);
            _whitelistErrors = new List<string>(whitelistResult.Errors);
            _whitelistWarnings = new List<string>(whitelistResult.Warnings);

            foreach (var guid in whitelistResult.IncludedGuids.OrderBy(g => g, StringComparer.Ordinal))
            {
                var closure = AssetDependencyClosure.Compute(new[] { guid });
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var entry = new GuidPathEntry(guid, path);

                if (closure.IsComplete)
                {
                    _localCompleteEntries.Add(entry);
                }
                else if (_remoteFallbackEnabled)
                {
                    _remoteResolveEntries.Add(entry);
                }
                else
                {
                    _errorEntries.Add(entry);
                }
            }

            _requiredAssetPaths = CollectRequiredAssetPaths(whitelistResult.IncludedGuids);
            _reportBuilt = true;
        }

        /// <summary>
        /// レポート用フィールドを初期状態へ戻す。
        /// </summary>
        private void ResetReportState()
        {
            _reportBuilt = false;
            _profileNotSelected = false;
            _profileName = string.Empty;
            _remoteCatalogUrl = string.Empty;
            _remoteFallbackEnabled = false;
            _localCompleteEntries = new List<GuidPathEntry>();
            _remoteResolveEntries = new List<GuidPathEntry>();
            _errorEntries = new List<GuidPathEntry>();
            _requiredAssetPaths = new List<string>();
            _whitelistErrors = new List<string>();
            _whitelistWarnings = new List<string>();
        }

        /// <summary>
        /// 全 Included GUID の依存閉包から、<c>Assets/</c> 配下の Checkout 必要パスを収集する。
        /// </summary>
        /// <param name="includedGuids">whitelist に含まれる GUID 集合。</param>
        /// <returns>重複排除・ソート済みのプロジェクト相対パス一覧。</returns>
        private static List<string> CollectRequiredAssetPaths(IEnumerable<string> includedGuids)
        {
            var combinedClosure = AssetDependencyClosure.Compute(includedGuids);
            var paths = new HashSet<string>(StringComparer.Ordinal);

            foreach (var guid in combinedClosure.ClosureGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                if (path.StartsWith("Assets/", System.StringComparison.Ordinal))
                {
                    paths.Add(path);
                }
            }

            return paths.OrderBy(p => p, StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// Checkout 必要パス一覧を改行連結してシステムクリップボードへコピーする。
        /// </summary>
        private void CopyRequiredPathsToClipboard()
        {
            var text = _requiredAssetPaths.Count == 0
                ? string.Empty
                : string.Join("\n", _requiredAssetPaths);

            EditorGUIUtility.systemCopyBuffer = text;
            ShowNotification(new GUIContent($"{_requiredAssetPaths.Count} asset path(s) copied"));
        }
    }
}
