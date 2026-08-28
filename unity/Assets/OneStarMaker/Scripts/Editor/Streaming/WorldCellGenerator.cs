#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.SceneSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OneStarMaker.Editor.Streaming
{
    /// <summary>
    /// 生成計画におけるセル 1 件分のアクション種別。
    /// 冪等性は <see cref="Skip"/> で「既存分は変更しない」を表現する。
    /// </summary>
    public enum WorldCellPlanAction
    {
        /// <summary>新規 SceneResource（および未存在なら .unity）を生成する。</summary>
        Create,

        /// <summary>既存エントリと一致するため生成・Map 更新を行わない。</summary>
        Skip,
    }

    /// <summary>
    /// セル 1 件分の生成計画エントリ（純粋データ）。
    /// </summary>
    public sealed class WorldCellGenerationEntry
    {
        /// <summary>
        /// 生成計画エントリを構築する。
        /// </summary>
        /// <param name="identity">セル identity（Cell_{x}_{y}）。</param>
        /// <param name="coordinate">グリッド座標。</param>
        /// <param name="parentIdentity">親シーン identity。</param>
        /// <param name="loadType">ロードタイミング種別。</param>
        /// <param name="action">Create / Skip。</param>
        /// <param name="sceneAssetPath">計画上の .unity 出力パス。</param>
        /// <param name="sceneResourceAssetPath">計画上の SceneResource .asset 出力パス。</param>
        public WorldCellGenerationEntry(
            string identity,
            Vector2Int coordinate,
            string parentIdentity,
            LoadType loadType,
            WorldCellPlanAction action,
            string sceneAssetPath,
            string sceneResourceAssetPath)
        {
            Identity = identity;
            Coordinate = coordinate;
            ParentIdentity = parentIdentity;
            LoadType = loadType;
            Action = action;
            SceneAssetPath = sceneAssetPath;
            SceneResourceAssetPath = sceneResourceAssetPath;
        }

        /// <summary>セル identity（Cell_{x}_{y}）。</summary>
        public string Identity { get; }

        /// <summary>グリッド座標。</summary>
        public Vector2Int Coordinate { get; }

        /// <summary>親シーン identity。</summary>
        public string ParentIdentity { get; }

        /// <summary>ロードタイミング種別。</summary>
        public LoadType LoadType { get; }

        /// <summary>Create / Skip。</summary>
        public WorldCellPlanAction Action { get; }

        /// <summary>計画上の .unity 出力パス。</summary>
        public string SceneAssetPath { get; }

        /// <summary>計画上の SceneResource .asset 出力パス。</summary>
        public string SceneResourceAssetPath { get; }
    }

    /// <summary>
    /// グリッド定義から算出したセル生成計画（純関数の出力）。
    /// </summary>
    public sealed class WorldCellGenerationPlan
    {
        /// <summary>
        /// 生成計画を構築する。
        /// </summary>
        /// <param name="entries">全セル分の計画エントリ（Create + Skip）。</param>
        public WorldCellGenerationPlan(IReadOnlyList<WorldCellGenerationEntry> entries)
        {
            Entries = entries ?? throw new ArgumentNullException(nameof(entries));
        }

        /// <summary>全セル分の計画エントリ（グリッド走査順、Create と Skip を含む）。</summary>
        public IReadOnlyList<WorldCellGenerationEntry> Entries { get; }

        /// <summary>新規生成対象のエントリのみ。</summary>
        public IEnumerable<WorldCellGenerationEntry> EntriesToCreate
            => Entries.Where(e => e.Action == WorldCellPlanAction.Create);

        /// <summary>スキップ（既存一致）のエントリのみ。</summary>
        public IEnumerable<WorldCellGenerationEntry> EntriesToSkip
            => Entries.Where(e => e.Action == WorldCellPlanAction.Skip);

        /// <summary>新規生成対象の件数。</summary>
        public int CreateCount => Entries.Count(e => e.Action == WorldCellPlanAction.Create);

        /// <summary>スキップ件数。</summary>
        public int SkipCount => Entries.Count(e => e.Action == WorldCellPlanAction.Skip);
    }

    /// <summary>
    /// 生成前の既存状態（純関数 <see cref="WorldCellGenerator.ComputePlan"/> の入力）。
    /// 既存セル identity の集合で冪等性（Skip 判定）を表現する。
    /// </summary>
    public sealed class WorldCellExistingState
    {
        /// <summary>既存セルが 1 件もない初期状態。</summary>
        public static WorldCellExistingState Empty { get; } = new(Array.Empty<string>(), null);

        /// <summary>
        /// 既存状態を構築する。
        /// </summary>
        /// <param name="existingCellIdentities">既に存在するセル identity の集合。</param>
        /// <param name="map">参照用 SceneResourceMap（任意）。Apply 後の再計画に使用。</param>
        public WorldCellExistingState(
            IReadOnlyCollection<string> existingCellIdentities,
            SceneResourceMap? map)
        {
            ExistingCellIdentities = existingCellIdentities ?? throw new ArgumentNullException(nameof(existingCellIdentities));
            Map = map;
        }

        /// <summary>既に存在するセル identity の集合。</summary>
        public IReadOnlyCollection<string> ExistingCellIdentities { get; }

        /// <summary>参照用 SceneResourceMap（任意）。</summary>
        public SceneResourceMap? Map { get; }

        /// <summary>
        /// SceneResourceMap からセル identity を収集して既存状態を構築する。
        /// </summary>
        /// <param name="map">走査対象の Map。</param>
        /// <returns>Map 内の Cell_* identity を ExistingCellIdentities に含む状態。</returns>
        public static WorldCellExistingState FromMap(SceneResourceMap map)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (var resource in map.SceneResources)
            {
                if (resource == null)
                {
                    continue;
                }

                if (CellIdentity.IsCellId(resource.Identity))
                {
                    identities.Add(resource.Identity);
                }
            }

            return new WorldCellExistingState(identities, map);
        }
    }

    /// <summary>
    /// <see cref="WorldCellGenerator.ApplyPlan"/> の適用結果。
    /// </summary>
    public sealed class WorldCellGenerationResult
    {
        /// <summary>
        /// 適用結果を構築する。
        /// </summary>
        /// <param name="createdOrUpdatedResources">今回 Create した SceneResource。</param>
        /// <param name="skippedIdentities">Skip した identity。</param>
        /// <param name="allCellResources">Map 登録後の全セル SceneResource（Create + 既存）。</param>
        public WorldCellGenerationResult(
            IReadOnlyList<SceneResource> createdOrUpdatedResources,
            IReadOnlyList<string> skippedIdentities,
            IReadOnlyList<SceneResource> allCellResources)
        {
            CreatedOrUpdatedResources = createdOrUpdatedResources
                ?? throw new ArgumentNullException(nameof(createdOrUpdatedResources));
            SkippedIdentities = skippedIdentities
                ?? throw new ArgumentNullException(nameof(skippedIdentities));
            AllCellResources = allCellResources
                ?? throw new ArgumentNullException(nameof(allCellResources));
        }

        /// <summary>今回新規 Create した SceneResource。</summary>
        public IReadOnlyList<SceneResource> CreatedOrUpdatedResources { get; }

        /// <summary>Skip した identity 一覧。</summary>
        public IReadOnlyList<string> SkippedIdentities { get; }

        /// <summary>適用後の全セル SceneResource。</summary>
        public IReadOnlyList<SceneResource> AllCellResources { get; }
    }

    /// <summary>
    /// グリッド定義の矩形集合からセルシーン + SceneResource + SceneResourceMap 登録を量産するエディタツール。
    /// 生成ロジック（計画）は純関数として分離し、.unity I/O は <see cref="ApplySceneFiles"/> に隔離する。
    /// </summary>
    public static class WorldCellGenerator
    {
        /// <summary>
        /// グリッド定義と既存状態から生成計画を算出する（純関数・テスト対象）。
        /// 既存 identity は <see cref="WorldCellPlanAction.Skip"/> で表現し、2 回目以降の差分なしを保証する。
        /// </summary>
        /// <param name="definition">グリッド定義。</param>
        /// <param name="existingState">生成前の既存セル identity 集合。</param>
        /// <returns>全セル分の生成計画。</returns>
        public static WorldCellGenerationPlan ComputePlan(
            WorldGridDefinition definition,
            WorldCellExistingState existingState)
        {
            _ = definition ?? throw new ArgumentNullException(nameof(definition));
            _ = existingState ?? throw new ArgumentNullException(nameof(existingState));
            ValidateDefinition(definition);

            var existingIdentities = new HashSet<string>(
                existingState.ExistingCellIdentities,
                StringComparer.Ordinal);
            var parentIdentity = definition.ParentSceneIdentity;
            var sceneFolder = NormalizeAssetPath(definition.SceneOutputFolder);
            var resourceFolder = NormalizeAssetPath(definition.SceneResourceOutputFolder);
            var cells = definition.EnumerateCells();
            var entries = new List<WorldCellGenerationEntry>(cells.Count);

            for (var i = 0; i < cells.Count; i++)
            {
                var coordinate = cells[i];
                var x = coordinate.x;
                var y = coordinate.y;
                var identity = CellIdentity.Format(x, y);
                var action = existingIdentities.Contains(identity)
                    ? WorldCellPlanAction.Skip
                    : WorldCellPlanAction.Create;
                // フォルダ = 実行環境境界（CCS-00）:
                // 「この Cell を動かすのに何が要るか」を Explorer で同名サブフォルダを開けば把握できるようにする。
                // 例: Cells/Cell_0_0/Cell_0_0.unity + Cell_0_0.asset（+ 任意で Environment_0_0.*）
                var sceneAssetPath = $"{sceneFolder}/{identity}/{identity}.unity";
                var sceneResourceAssetPath = $"{resourceFolder}/{identity}/{identity}.asset";

                entries.Add(new WorldCellGenerationEntry(
                    identity,
                    new Vector2Int(x, y),
                    parentIdentity,
                    LoadType.OnDemand,
                    action,
                    sceneAssetPath,
                    sceneResourceAssetPath));
            }

            return new WorldCellGenerationPlan(entries);
        }

        /// <summary>
        /// 生成計画を SceneResource / SceneResourceMap に適用する（テスト対象）。
        /// .unity への書き込みは行わない（GUID 解決のため <see cref="AssetDatabase.AssetPathToGUID(string)"/> の
        /// 読み取りのみ行う。.unity 未生成なら payload は空になる）。
        /// HpGaugeSliceSceneCreator / SceneResourceGenerator の Map 登録・親子設定パターンに準拠。
        /// </summary>
        /// <param name="definition">グリッド定義。</param>
        /// <param name="plan">適用する生成計画。</param>
        /// <param name="map">登録先 SceneResourceMap。</param>
        /// <param name="parentResource">全セルの親 SceneResource（identity == definition.ParentSceneIdentity）。</param>
        /// <returns>適用結果。</returns>
        public static WorldCellGenerationResult ApplyPlan(
            WorldGridDefinition definition,
            WorldCellGenerationPlan plan,
            SceneResourceMap map,
            SceneResource parentResource)
        {
            _ = definition ?? throw new ArgumentNullException(nameof(definition));
            _ = plan ?? throw new ArgumentNullException(nameof(plan));
            _ = map ?? throw new ArgumentNullException(nameof(map));
            _ = parentResource ?? throw new ArgumentNullException(nameof(parentResource));

            if (!string.Equals(parentResource.Identity, definition.ParentSceneIdentity, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"親 SceneResource の identity が一致しません: expected={definition.ParentSceneIdentity}, actual={parentResource.Identity}",
                    nameof(parentResource));
            }

            var createdResources = new List<SceneResource>();
            var skippedIdentities = new List<string>();
            var allCellResources = new List<SceneResource>(plan.Entries.Count);

            foreach (var entry in plan.Entries)
            {
                if (entry.Action == WorldCellPlanAction.Create)
                {
                    var resource = ScriptableObject.CreateInstance<SceneResource>();
                    ConfigureSceneResource(resource, entry);
                    AddChildToParent(parentResource, resource);
                    UpsertSceneResourceInMap(map, resource);
                    createdResources.Add(resource);
                    allCellResources.Add(resource);
                }
                else
                {
                    skippedIdentities.Add(entry.Identity);
                    var existing = map.GetSceneResource(entry.Identity);
                    if (existing == null)
                    {
                        throw new InvalidOperationException(
                            $"Skip 対象のセル {entry.Identity} が SceneResourceMap に存在しません。");
                    }

                    allCellResources.Add(existing);
                }
            }

            map.RebuildDictionary();

            return new WorldCellGenerationResult(
                createdResources,
                skippedIdentities,
                allCellResources);
        }

        /// <summary>
        /// 生成計画に基づき .unity シーンファイルを作成・更新する（テスト対象外）。
        /// 既存ファイルはスキップし、冪等に保つ。
        /// </summary>
        /// <param name="definition">グリッド定義。</param>
        /// <param name="plan">適用する生成計画（EntriesToCreate のみ処理）。</param>
        public static void ApplySceneFiles(
            WorldGridDefinition definition,
            WorldCellGenerationPlan plan)
        {
            _ = definition ?? throw new ArgumentNullException(nameof(definition));
            _ = plan ?? throw new ArgumentNullException(nameof(plan));

            foreach (var entry in plan.EntriesToCreate)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(entry.SceneAssetPath) != null)
                {
                    continue;
                }

                EnsureAssetFolder(Path.GetDirectoryName(entry.SceneAssetPath)!);

                // Additive で作成 → 保存 → クローズ。Single だと Editor で開いている
                // 作業中シーンを破棄してしまい、最後のセルシーンが開いたまま残る。
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                EditorSceneManager.SaveScene(scene, entry.SceneAssetPath);
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        /// <summary>
        /// グリッド定義に基づき計画算出 → .unity 生成 → SceneResource / Map 登録を一括実行する（テスト対象外）。
        /// </summary>
        /// <param name="definition">グリッド定義。</param>
        /// <param name="map">登録先 SceneResourceMap。</param>
        /// <param name="parentResource">全セルの親 SceneResource。</param>
        /// <returns>true: 成功。</returns>
        public static bool Generate(
            WorldGridDefinition definition,
            SceneResourceMap map,
            SceneResource parentResource)
        {
            _ = definition ?? throw new ArgumentNullException(nameof(definition));
            _ = map ?? throw new ArgumentNullException(nameof(map));
            _ = parentResource ?? throw new ArgumentNullException(nameof(parentResource));

            // 副作用（取り込みによる Map・親子の書き換え）より前に定義を検証し、
            // 不正定義で中途半端な変更が残らないようにする。
            ValidateDefinition(definition);

            // Map に載っていないがディスク上に既存の SceneResource .asset があるケースを先に取り込む。
            // これを怠ると ApplyPlan が未保存インスタンスを新規作成して Map に挿入する一方、
            // CreateAsset は既存 .asset を見てスキップし、Map が未保存インスタンスを指してしまう。
            AdoptExistingResourceAssets(definition, map, parentResource);

            var existingState = WorldCellExistingState.FromMap(map);
            var plan = ComputePlan(definition, existingState);

            ApplySceneFiles(definition, plan);
            var result = ApplyPlan(definition, plan, map, parentResource);

            foreach (var resource in result.CreatedOrUpdatedResources)
            {
                var entry = plan.Entries.First(e => e.Identity == resource.Identity);
                if (AssetDatabase.LoadAssetAtPath<SceneResource>(entry.SceneResourceAssetPath) == null)
                {
                    // CCS-00 でパスが {folder}/{identity}/{identity}.asset になったため、
                    // 親フォルダ 1 段だけでは足りない。.unity 側（ApplySceneFiles）と同じく
                    // エントリごとのディレクトリを作る。
                    EnsureAssetFolder(Path.GetDirectoryName(entry.SceneResourceAssetPath)!);
                    AssetDatabase.CreateAsset(resource, entry.SceneResourceAssetPath);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return true;
        }

        /// <summary>
        /// SceneResource の identity / SceneAssetDescription を SerializedProperty の
        /// 要素単位書き込みで設定する（W-1: boxedValue の暗黙的ディープコピーに依存しない。
        /// 11-scene-graph-editor.md §W-1 / SceneResourceGenerator と同方針）。
        /// </summary>
        private static void ConfigureSceneResource(SceneResource resource, WorldCellGenerationEntry entry)
        {
            var so = new SerializedObject(resource);
            so.FindProperty("_identity").stringValue = entry.Identity;

            var sadProp = so.FindProperty("_sceneAssetDescription");
            sadProp.FindPropertyRelative("SceneIdentity").stringValue = entry.Identity;
            sadProp.FindPropertyRelative("_loadType").enumValueIndex = (int)entry.LoadType;

            var payloadsProp = sadProp.FindPropertyRelative("_payloads");
            payloadsProp.ClearArray();

            // OnlyExistingAssets: 既定は削除直後のアセットの GUID も返すため、
            // 存在しない .unity の GUID を payload に埋めないよう明示する。
            var sceneGuid = AssetDatabase.AssetPathToGUID(
                entry.SceneAssetPath, AssetPathToGUIDOptions.OnlyExistingAssets);
            if (!string.IsNullOrEmpty(sceneGuid))
            {
                payloadsProp.InsertArrayElementAtIndex(0);
                var payloadProp = payloadsProp.GetArrayElementAtIndex(0);
                payloadProp.FindPropertyRelative("Variant").stringValue = string.Empty;
                payloadProp
                    .FindPropertyRelative("Reference")
                    .FindPropertyRelative("m_AssetGUID").stringValue = sceneGuid;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// ディスク上に存在するが Map 未登録のセル SceneResource .asset を Map / 親子関係へ取り込む。
        /// 取り込んだセルは以降の ComputePlan で Skip 判定になる。
        /// 取り込みは登録のみで、SceneAssetDescription の中身（payload GUID 等）は検証・更新しない割り切り。
        /// </summary>
        private static void AdoptExistingResourceAssets(
            WorldGridDefinition definition,
            SceneResourceMap map,
            SceneResource parentResource)
        {
            var resourceFolder = NormalizeAssetPath(definition.SceneResourceOutputFolder);
            var adopted = false;
            var cells = definition.EnumerateCells();

            for (var i = 0; i < cells.Count; i++)
            {
                var coordinate = cells[i];
                var identity = CellIdentity.Format(coordinate.x, coordinate.y);
                if (map.GetSceneResource(identity) != null)
                {
                    continue;
                }

                var assetPath = $"{resourceFolder}/{identity}/{identity}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<SceneResource>(assetPath);
                if (existing == null)
                {
                    continue;
                }

                // ファイル名から仮定した identity と .asset 内部の _identity が食い違う場合は
                // 取り込まない（食い違ったまま upsert すると ComputePlan が Create 判定し、
                // Map が未保存インスタンスを指す不整合が再発するため）。
                if (!string.Equals(existing.Identity, identity, StringComparison.Ordinal))
                {
                    Debug.LogWarning(
                        $"[WorldCellGenerator] 既存アセット {assetPath} の identity '{existing.Identity}' が " +
                        $"ファイル名由来の '{identity}' と一致しないため取り込みをスキップします。");
                    continue;
                }

                AddChildToParent(parentResource, existing);
                UpsertSceneResourceInMap(map, existing);
                adopted = true;
            }

            if (adopted)
            {
                map.RebuildDictionary();
            }
        }

        private static void AddChildToParent(SceneResource parent, SceneResource child)
        {
            var parentSo = new SerializedObject(parent);
            var childrenProp = parentSo.FindProperty("_children");

            for (var i = 0; i < childrenProp.arraySize; i++)
            {
                if (childrenProp.GetArrayElementAtIndex(i).objectReferenceValue == child)
                {
                    parentSo.ApplyModifiedPropertiesWithoutUndo();
                    SetChildParent(child, parent);
                    return;
                }
            }

            childrenProp.InsertArrayElementAtIndex(childrenProp.arraySize);
            childrenProp.GetArrayElementAtIndex(childrenProp.arraySize - 1).objectReferenceValue = child;
            parentSo.ApplyModifiedPropertiesWithoutUndo();
            SetChildParent(child, parent);
        }

        private static void SetChildParent(SceneResource child, SceneResource parent)
        {
            var childSo = new SerializedObject(child);
            childSo.FindProperty("_parent").objectReferenceValue = parent;
            childSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void UpsertSceneResourceInMap(SceneResourceMap map, SceneResource resource)
        {
            var mapSo = new SerializedObject(map);
            var listProp = mapSo.FindProperty("_sceneResources");

            for (var i = 0; i < listProp.arraySize; i++)
            {
                var existing = listProp.GetArrayElementAtIndex(i).objectReferenceValue as SceneResource;
                if (existing != null && existing.Identity == resource.Identity)
                {
                    listProp.GetArrayElementAtIndex(i).objectReferenceValue = resource;
                    mapSo.ApplyModifiedPropertiesWithoutUndo();
                    return;
                }
            }

            listProp.InsertArrayElementAtIndex(listProp.arraySize);
            listProp.GetArrayElementAtIndex(listProp.arraySize - 1).objectReferenceValue = resource;
            mapSo.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// グリッド定義の妥当性を検証する（不正矩形・空フォルダを
        /// AssetDatabase の不可解な失敗ではなく明示的な例外にする）。
        /// 矩形レイアウトの例外は <see cref="WorldGridDefinition"/> に委譲する。
        /// </summary>
        private static void ValidateDefinition(WorldGridDefinition definition)
        {
            // 矩形の空・サイズ・重なりは WorldGridDefinition 側で例外にする。
            _ = definition.Rectangles;

            if (definition.CellSize <= 0f)
            {
                throw new ArgumentException(
                    $"セルサイズは正の値が必要です: {definition.CellSize}",
                    nameof(definition));
            }

            if (string.IsNullOrWhiteSpace(definition.ParentSceneIdentity))
            {
                throw new ArgumentException("親シーン identity が未設定です。", nameof(definition));
            }

            if (string.IsNullOrWhiteSpace(definition.SceneOutputFolder)
                || string.IsNullOrWhiteSpace(definition.SceneResourceOutputFolder))
            {
                throw new ArgumentException("出力フォルダが未設定です。", nameof(definition));
            }
        }

        private static string NormalizeAssetPath(string path)
            => path.Replace('\\', '/').TrimEnd('/');

        private static void EnsureAssetFolder(string folderPath)
        {
            var normalized = NormalizeAssetPath(folderPath);
            if (AssetDatabase.IsValidFolder(normalized))
            {
                return;
            }

            var parts = normalized.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
