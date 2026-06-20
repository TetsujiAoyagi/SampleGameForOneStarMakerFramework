# 11. Scene Graph Editor（Editor 拡張）

> [ARCHITECTURE.md](../../ARCHITECTURE.md) に戻る

---

## 11.1 概要

シーン親子関係をグラフノードで視覚的に編集し、`SceneResource` / `SceneResourceMap` を自動生成する Editor 拡張ツール。
UIToolkit（GraphView）+ MVVM アーキテクチャで構築する。

### 暗黙知の言語化

| # | 暗黙知 | 必要な理由 |
|---|---|---|
| IK-1 | SceneResource はランタイム資産。エディタ専用データ（ノード位置等）を持たせない | ビルドサイズ増加 + 責務混在の防止 |
| IK-2 | SceneResourceMap は導出データ。手編集ではなく Generate で生成する | 手編集は SceneResource との不整合を招く |
| IK-3 | 親子関係 = ツリー。DAG / サイクル不可 | SceneDirector の再帰 PreLoad がスタックオーバーフローする |
| IK-4 | ノードドラッグ（位置変更）は最頻変更だが意味的価値はゼロ | Git diff/blame 汚染の防止 |
| IK-5 | Unity Undo は SerializedObject 前提 | 自前 Undo は ROI が合わない |
| IK-6 | YAML SerializedObject のリスト型はマージ困難 | ファイル分割が合理的 |
| IK-7 | SceneAssetDescription は SceneResource 専用の合成型 | SceneNodeData はエディタ中間データなので直接持たない |
| IK-8 | SceneNodeData の Identity と SceneAsset.name は 1:1 対応させるのが自然 | D&D 操作の直感性と命名一貫性 |

### 施行ルール

| # | ルール | 施行方法 | 強制力 |
|---|---|---|---|
| E-1 | SceneResource は直接作成しない | `CreateAssetMenu` を削除し Generate のみで生成 | 構造的強制 |
| E-2 | SceneResourceMap は手編集しない | Generate 時に自動再構築 | 構造的強制 |
| E-3 | サイクル禁止 | Edge 作成時に DFS で到達可能性チェック → 拒否 | 構造的強制 |
| E-4 | 最大1親（ツリー制約） | Edge 作成時に既存親があれば旧 Edge 削除 | 構造的強制 |
| E-5 | Identity 一意 | ノード作成/リネーム時に重複チェック → 拒否。同名ノードは再利用 | 構造的強制 |
| E-6 | 位置変更は階層データに影響しない | ファイル物理分離 | 構造的強制 |
| E-7 | Generate 後の整合性保証 | Generate 完了時にバリデーション実行 | 自動検証 |
| E-8 | SceneAsset D&D 時は Identity を SceneAsset.name に自動ロック | Payload[0] 変更監視 + TextField 無効化 | 構造的強制 |

## 11.2 トレードオフ判定

| 決定 | 採用 | 却下理由 |
|---|---|---|
| 保存形式 | **ScriptableObject (YAML)** | JSON は Undo 自作コストが大きすぎる |
| ファイル分割 | **ノード独立 + エッジ + レイアウトの3層** | 単一ファイルはマージ地獄 |
| Undo | **Unity Undo** | 自前は ROI が合わない |
| Graph UI | **GraphView (Experimental)** | 自前 VisualElement は車輪の再発明 |
| エッジ保存 | **グラフ単位の専用ファイル** | ノード内包はマージ衝突が増える |
| バリデーション | **操作時（軽量） + Generate 時（全網羅）** | Generate のみでは不正ステートが残る |
| ノード作成 | **右クリック即座作成 + D&D** | ダイアログはクリック数が多く UX が悪い |
| 同名ノード | **既存ノードの再利用** | 重複エラーは複数グラフ運用で邪魔 |
| 空 Payload | **許容（Info）** | 構造設計段階で Payload 未設定は自然 |

## 11.3 ファイル構成

### 中間データ（Editor 専用）

```
Assets/
  SceneGraphData/                         ← 中間データルート
    Nodes/                                ← 1シーン = 1ファイル
      Main.asset                          ← SceneNodeData
      OutGame.asset
      Title.asset
    Graphs/                               ← エッジ（グラフ単位）
      MainGraph.asset                     ← SceneGraphEdges
    Layouts/                              ← 位置情報（グラフ単位）
      MainGraph_Layout.asset              ← SceneGraphLayout
```

### 生成データ（ランタイム資産）

```
Assets/
  OneStarMaker/Runtime/
    SceneMap/                             ← 生成先
      SceneResourceMap.asset              ← SceneResource 一覧
      Resources/
        Main.asset                        ← SceneResource（Identity + Parent/Children + SceneAssetDescription）
        OutGame.asset
        Title.asset
```

### Editor 拡張ソースコード

```
Assets/OneStarMaker/Scripts/Editor/SceneGraph/
  Data/
    SceneNodeData.cs                      ← 中間データ（1ノード = 1ファイル）
    SceneGraphEdges.cs                    ← エッジ + ノードメンバーシップ
    SceneGraphLayout.cs                   ← ノード位置情報
  ViewModel/
    SceneGraphViewModel.cs                ← MVVM ViewModel（全コマンド + Undo）
  View/
    SceneGraphEditorWindow.cs             ← メインウィンドウ + ツールバー
    SceneGraphView.cs                     ← GraphView 本体（右クリック作成 + D&D）
    SceneGraphNode.cs                     ← GraphView Node（ポート + LoadType 表示）
    SceneGraphInspectorPanel.cs           ← 詳細パネル（Identity/LoadType/Payloads 編集）
    SceneGraphView.uss                    ← スタイルシート
  SceneGraphValidator.cs                  ← バリデーション（6チェック）
  SceneResourceGenerator.cs              ← Generate パイプライン
```

## 11.4 データモデル

```csharp
// ── 中間データ（Editor 専用）──

// ノード（1シーン = 1ファイル）
public class SceneNodeData : ScriptableObject
{
    string _identity;                  // シーンの一意識別子
    LoadType _loadType;                // ロードタイミング種別（Editor 側の権威）
    List<ScenePayload> _payloads;      // Addressable シーン参照リスト
    // ※ SceneAssetDescription は持たない。Generate 時に組み立てる。
}

// エッジ（グラフ単位1ファイル）
public class SceneGraphEdges : ScriptableObject
{
    string _graphName;
    List<SceneNodeData> _nodes;        // グラフに所属するノード一覧
    List<Edge> _edges;                 // Edge = { SceneNodeData Parent, SceneNodeData Child }
    // クエリ: GetParent / GetChildren / GetRoots / WouldCreateCycle / ContainsNode
}

// レイアウト（グラフ単位1ファイル）
public class SceneGraphLayout : ScriptableObject
{
    List<NodePosition> _positions;     // { SceneNodeData Node, Vector2 Position }
}

// ── ランタイム資産（Generate で生成）──

// SceneResource = Identity + LoadType/Payloads（via SceneAssetDescription）+ 親子ツリー
public class SceneResource : ScriptableObject
{
    string _identity;
    SceneAssetDescription _sceneAssetDescription;  // LoadType + Payloads を包含
    SceneResource _parent;
    List<SceneResource> _children;
}

// SceneAssetDescription = LoadType + Payloads → Addressables ロード実行
[Serializable]
public class SceneAssetDescription
{
    LoadType _loadType;
    List<ScenePayload> _payloads;
    // Load(variant) → Addressables.LoadSceneAsync()
}

// ScenePayload = AssetReference + バリアント名
[Serializable]
public class ScenePayload
{
    AssetReference SceneReference;
    string Variant;
}
```

Edge 内の Parent/Child は ScriptableObject 参照（YAML 上は GUID）。リネーム時に自動追従する。

## 11.5 MVVM アーキテクチャ

```
┌──────────────────────────────────────────────────────────────┐
│  View                                                        │
│  SceneGraphEditorWindow  … メインウィンドウ、ツールバー、GUID 永続化 │
│  SceneGraphView          … GraphView（右クリック即座作成、D&D）     │
│  SceneGraphNode          … Node（ポート、LoadType ラベル）         │
│  SceneGraphInspectorPanel … 詳細パネル（Identity ロック、Payload 監視）│
│  ── イベント／コマンドバインド ──                                  │
├──────────────────────────────────────────────────────────────┤
│  ViewModel                                                   │
│  SceneGraphViewModel                                         │
│  ── 状態 ── SelectedNode, Nodes, CurrentEdges, CurrentLayout │
│  ── コマンド ──                                               │
│    CreateNode(identity, position)     … 同名なら再利用（R-6）   │
│    CreateNodeWithSceneAsset(path, pos) … D&D 用（R-3）         │
│    DeleteNode    ConnectEdge    DisconnectEdge    MoveNode     │
│    RenameNode    CreateGraph    LoadGraph    Generate          │
│    GenerateUniqueName()   … 自動命名（R-2）                    │
│  ── イベント ── OnGraphChanged, OnSelectionChanged             │
├──────────────────────────────────────────────────────────────┤
│  Model                                                       │
│  SceneNodeData (ScriptableObject)                            │
│  SceneGraphEdges (ScriptableObject)                          │
│  SceneGraphLayout (ScriptableObject)                         │
│  SceneGraphValidator      … 6 チェック                        │
│  SceneResourceGenerator   … Generate パイプライン              │
└──────────────────────────────────────────────────────────────┘
```

ViewModel は View と Model の仲介。Undo/Redo は ViewModel のコマンド経由で `Undo.RecordObject` を呼ぶ。

## 11.6 ノード作成ワークフロー

### 11.6.1 右クリック即座作成（R-2）

1. GraphView 上を右クリック → "Create Node"
2. `GenerateUniqueName()` が "NewScene", "NewScene1", "NewScene2"… を生成
3. `CreateNode(name, mousePos)` で SceneNodeData アセット作成 + グラフ登録

### 11.6.2 SceneAsset ドラッグ＆ドロップ（R-3/R-4）

1. Project ウィンドウから .unity ファイルを GraphView にドラッグ
2. `DragUpdatedEvent` で SceneAsset を検出 → ドロップ可能表示
3. `DragPerformEvent` で `CreateNodeWithSceneAsset(path, pos)` 実行
4. Identity = SceneAsset.name、Payload[0] = AssetReference(GUID)
5. 複数ファイル D&D → 個別ノードとして Y+200px オフセットで配置（R-4）

### 11.6.3 同名ノード再利用（R-6）

1. `CreateNode` が呼ばれると `FindExistingNode(identity)` で検索
2. 既存ノードが見つかれば `ReuseNode()` → 現在のグラフに追加（新規作成しない）
3. Console に `[SceneGraph] Reusing existing node 'XXX'.` をログ出力

### 11.6.4 Identity 自動同期とロック（R-5/R-7）

1. InspectorPanel の Payloads 編集時、`ApplyModifiedProperties()` 後に Payload[0] の AssetGUID 変更を検知
2. GUID が変わったら `AssetDatabase.GUIDToAssetPath()` → ファイル名から Identity を同期
3. Payload[0] に SceneAsset がセットされている間は Identity TextField を無効化（readonly）
4. Payload[0] がクリアされたらアンロック

## 11.7 バリデーション

| # | チェック | タイミング | 重大度 |
|---|---|---|---|
| V-1 | サイクル検出 | Edge 作成時 + Generate 時 | Error |
| V-2 | Identity 重複 | ノード作成/リネーム時 + Generate 時 | Error |
| V-3 | Identity 空文字 | ノード作成時 + Generate 時 | Error |
| V-4 | 空 Payloads（シーン参照なし） | Generate 時 | **Info** |
| V-5 | 孤立ノード | Generate 時 | Warning |
| V-6 | 壊れた SO 参照 | Graph ロード時 | Error |

## 11.8 Generate パイプライン

```
SceneNodeData[] + SceneGraphEdges[]
  │
  ├─ Step 1: ValidateAll() → Error あれば中止
  ├─ Step 2: EnsureDirectoryExists (出力先)
  ├─ Step 3: ノードごとに SceneResource を生成/更新
  │    ├─ _identity     ← SceneNodeData.Identity
  │    └─ _sceneAssetDescription
  │         ├─ _loadType ← SceneNodeData.NodeLoadType
  │         └─ _payloads ← SceneNodeData.Payloads (要素単位コピー)
  ├─ Step 4: Edge から Parent/Children 参照を設定
  ├─ Step 5: SceneResourceMap に全 SceneResource を登録（Identity 昇順）
  │    └─ _generateHash ← ComputeCurrentHash() で SHA-256 先頭8バイト
  ├─ Step 6: AssetDatabase.SaveAssets()
  └─ Step 7: VerifyGeneratedIntegrity() → Parent/Children 双方向整合チェック
```

Generate は冪等。同じ中間データから同一の出力を保証する。
孤児 SceneResource は `CleanupOrphanedResources()` で削除される。

### 11.8.1 安全機構

| # | 機構 | 内容 | 実装箇所 |
|---|---|---|---|
| **W-1** | 要素単位コピー | `ScenePayload` の `AssetReference` を `m_AssetGUID`/`m_SubObjectName`/`m_SubObjectType` 単位で転記。`boxedValue` の暗黙的ディープコピーに依存しない | `CopyPayloadsElementWise()` |
| **W-2** | 生成後整合チェック | Generate 完了後に Parent/Children の双方向参照を検証。不整合があれば Warning をログ出力 | `VerifyGeneratedIntegrity()` |
| **W-3** | Generate 忘れ検出 | 中間データ（Identity/LoadType/Payloads/Edges）の SHA-256 ハッシュを SceneResourceMap に保存。Editor 起動時に再計算して不一致なら警告 | `ComputeCurrentHash()` + `IsGenerateStale()` |
| **W-5** | Payload→Identity 自動同期 | `SceneNodeData.OnValidate()` で Payload[0] の SceneAsset 名と Identity を自動同期。Inspector 直接編集・Undo/Redo にも対応 | `SceneNodeData.OnValidate()` |

## 11.9 SceneAssetDescription 生成の流れとシーン遷移との関係

### 11.9.1 データフロー全景

```
[Editor 時]
  SceneNodeData                SceneGraphEdges
  ┌──────────────┐            ┌──────────────────┐
  │ Identity     │            │ Parent → Child    │
  │ LoadType     │            │ Parent → Child    │
  │ Payloads[]   │            │ ...               │
  │  └ AssetRef  │            └──────────────────┘
  │  └ Variant   │                    │
  └──────────────┘                    │
         │                            │
         └──────┬─────────────────────┘
                │  Generate
                ▼
  SceneResource (ランタイム SO)
  ┌────────────────────────────────┐
  │ Identity                       │
  │ SceneAssetDescription          │
  │  ├ LoadType  ← NodeLoadType    │
  │  └ Payloads[]← Payloads[]     │
  │     └ AssetReference(GUID)     │
  │     └ Variant                  │
  │ Parent  → SceneResource        │←── Edge 由来
  │ Children[] → SceneResource[]   │←── Edge 由来
  └────────────────────────────────┘
         │
         │  SceneResourceMap.GetSceneResource(identity)
         ▼
[Runtime 時]
  SceneDirector.AddScene(identity)
         │
         ├─ SceneResourceMap → SceneResource 検索
         ├─ SceneResource.Parent で親シーンを再帰収集
         ├─ 親シーンを先にロード（依存解決）
         └─ SceneResource.Load(variant)
              → SceneAssetDescription.Load()
                → ScenePayload.FindPayload(variant)
                  → Addressables.LoadSceneAsync(AssetReference)
```

### 11.9.2 SceneAssetDescription の責務境界

```
エディタ側（中間データ）           ランタイム側（生成データ）
─────────────────────           ──────────────────────
SceneNodeData                   SceneResource
  ._loadType                      ._sceneAssetDescription._loadType
  ._payloads[]                    ._sceneAssetDescription._payloads[]
  (フラットに保持)                  (SceneAssetDescription に包含)
```

**設計意図:**
- SceneNodeData は Editor UI で直接 SerializedProperty 編集するため、フラットなフィールド構成が望ましい。
- SceneResource は SceneAssetDescription を介して Addressables ロードを実行するため、LoadType + Payloads を1オブジェクトに集約する。
- Generate が両者の構造差分を吸収するマッピング層として機能する。

### 11.9.3 ランタイムシーン遷移パイプライン

```
SceneDirector.AddScene("InGame")
  │
  ├─ [1] SceneResourceMap.GetSceneResource("InGame")
  │       → SceneResource (Identity="InGame", Parent="Main", Children=[])
  │
  ├─ [2] 親シーンの再帰収集
  │       CollectNecessaryScenes(Parent)
  │       → ["Main"] がまだ未ロードなら追加対象
  │
  ├─ [3] キャンセル可能窓: SceneBase PreLoad
  │       親 → 子の順で SceneBase を生成（ISceneFactory 経由）
  │       この段階では Unity Scene はロードされていない
  │       外部 CancellationToken でキャンセル可能
  │
  ├─ [4] ★ ポイント・オブ・ノーリターン
  │       LoadCts をクリア → 以降キャンセル不可
  │
  ├─ [5] Unity Scene ロード
  │       SceneResource.Load(variant)
  │         → SceneAssetDescription.Load(variant)
  │           → Addressables.LoadSceneAsync(AssetReference, Additive)
  │       親 → 子の順で Additive ロード
  │
  └─ [6] afterOnLoadedTask → Completed
```

## 11.10 マージ戦略

```
変更シナリオ                    → 影響ファイル              → コンフリクト確率
─────────────────────────────────────────────────────────────────
A: LoadType 変更               → Nodes/Title.asset         → 別ノードなら安全
B: ノード新規追加              → Nodes/X.asset + Edges + Layout → Edge行追加同士
A: ノードドラッグ              → Layouts/ のみ             → 階層(Edges)は無傷
B: エッジ変更                  → Graphs/ のみ              → レイアウトは無傷
A: Payload SceneAsset 変更     → Nodes/X.asset のみ        → 別ノードなら安全
B: D&D で新ノード追加          → Nodes/X.asset + Edges     → ノード名が異なれば安全
```
