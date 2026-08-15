# SceneGraphEditor 複数選択 / Copy-Paste / Undo 一括化 — 実装 HANDOFF

> 作成日: 2026-08-05
> 対象: 実装担当（次セッションの人間 / エージェント）
> ブランチ: `impl/scenegraph-editor-multiselect` を切ってから作業すること
> **この文書だけで実装できるように書いてある。他の docs を読みに行かないこと。**
> 設計判断が必要になったら、実装せず停止して報告すること。

---

## 0. 今どこまで終わっているか

対象は `unity/Assets/OneStarMaker/Scripts/Editor/SceneGraph/` の Scene Graph Editor（Unity の `UnityEditor.Experimental.GraphView` ベース、MVVM）。

| 項目 | 状態 |
|---|---|
| ノードの作成 / 削除 / 移動 / 親子付け / 親子解除（**単一**） | **済** |
| SceneAsset の D&D でノード作成 | **済** |
| バリデーション（V-1〜V-6）と Generate パイプライン | **済** |
| Undo（単一操作、ただし穴あり — §2.3） | 一部 |
| **複数選択** | **未着手（本 HANDOFF の主題）** |
| **Copy / Paste / Duplicate** | **未着手。GraphView のクリップボードデリゲートが 1 つも代入されていない** |
| **複数ノードへの一括操作（移動 / 削除 / 親子付け / 親子解除 / コピー）** | **未着手** |
| **一括操作の Undo を 1 グループに畳む仕組み** | **未着手** |
| ContextMenu の項目 | `Create Node` と `Auto Layout` の 2 つだけ |
| SceneGraph 関連の EditMode テスト | **1 件も無い**（本 HANDOFF で新設する） |

### 0.1 ファイル構成（触る範囲）

```
unity/Assets/OneStarMaker/Scripts/Editor/SceneGraph/
  Data/
    SceneNodeData.cs          76行   1ノード = 1アセット（Identity / LoadType / Payloads）
    SceneGraphEdges.cs       201行   グラフ単位の親子エッジ + 所属ノード一覧
    SceneGraphLayout.cs       74行   ノード座標のみ（エッジと別ファイル）
  ViewModel/
    SceneGraphViewModel.cs   550行   全コマンド + 選択状態 + Undo 記録の中心
  View/
    SceneGraphEditorWindow.cs 410行  EditorWindow 本体、ツールバー、選択ポーリング、Undo フック
    SceneGraphView.cs        368行   GraphView 派生。描画 / graphViewChanged / 右クリック / D&D
    SceneGraphNode.cs         82行   Node 派生。Input(Single) / Output(Multi) ポート
    SceneGraphInspectorPanel.cs 200行 左 280px の選択ノード詳細パネル
    SceneGraphView.uss        26行
  SceneGraphValidator.cs     220行   静的バリデータ
  SceneResourceGenerator.cs  437行   中間データ → ランタイム SceneResource 生成（本作業では触らない）
```

名前空間はすべて `OneStarMaker.Editor.SceneGraph`。asmdef は `unity/Assets/OneStarMaker/Scripts/Editor/OneStarMaker.Editor.asmdef`（SceneGraph 専用 asmdef は無い）。

データアセットは `unity/Assets/SceneGraphData/` 配下:

```
SceneGraphData/
  Nodes/        … SceneNodeData 30 件（うち Cells/ サブフォルダに 20 件）
  Graphs/       … Total.asset （SceneGraphEdges。ノード 30・エッジ 28）※現在グラフは 1 つだけ
  Layouts/      … Total_Layout.asset （SceneGraphLayout）
```

---

## 1. ユーザー意図（ここが正）

**「複数ノードをまとめて動かす・消す・繋ぐ・剥がす・コピーする、そのすべてが 1 回の Ctrl+Z で戻る」状態にすること。**

具体的な要求:

1. Copy/Paste が機能していないので機能するようにする
2. 複数選択して一括操作できるようにする — 移動 / 削除 / Copy / 親子付け / 親子付け解除
3. Undo/Redo を壊さず、**一括操作も 1 回の Undo/Redo で戻せる**ようにする
4. 上記に伴い、親子付け解除を ContextMenu から選べるようにする
5. バグがあれば直す

必要ならテストを追加してよい（むしろ追加してほしい）。

### 1.1 なぜこの形が要るのか（設計の芯）

SceneGraph は「画面遷移の分類図」ではなく、**ロード済みサービスを共有し、破棄時点も決めるスコープ木**として設計されている。親ノードは「そのスコープに入った全ての子が必要とするもの」を Ready にする責務を持つ。

つまり木の形そのものが仕様であり、**木を安全に組み替えられること**がこのエディタの第一の存在理由になる。組み替えが単一ノード単位でしかできず、しかも間違えても戻せない（Undo に穴がある）現状は、この目的に対して機能不足である。

---

## 2. 壊さない制約

### 2.1 リポジトリ全体の不変条件（本作業に関係する分）

- **`.asmdef` の `references` を追加しない。** 本作業は `OneStarMaker.Editor` の中だけで完結する。参照を足したくなったら設計判断なので停止して報告する
- すべての `.cs` の先頭に `#nullable enable`
- **Unity を起動しない。`tools/run-tests.ps1` を実行しない。** Unity バッチ実行は `unity/Library/`（git 管理外・破損すると再インポートに長時間）や `unity/Temp/UnityLockfile`（残留すると以降の実行を塞ぐ）に触れ、ブランチを捨てても戻らない。**テスト実行はレビュー担当が行う。** 実装が終わったら「実装完了・テスト未実行」と報告すればよい
- 新規 `.cs` を追加したら `.meta` は生成しなくてよい（レビュー担当が Unity を開いたときに生成される）

### 2.2 SceneGraph 固有の不変条件（施行ルール）

これらは既存コードが構造的に強制している。**壊さないこと。**

| # | ルール | 現在の施行方法 |
|---|---|---|
| E-3 | **サイクル禁止**（ツリーであって DAG ではない） | Edge 作成時に `SceneGraphEdges.WouldCreateCycle` の DFS で拒否。ランタイムの `SceneDirector` は再帰 PreLoad するのでサイクルはスタックオーバーフローになる |
| E-4 | **最大 1 親** | `SceneGraphNode` の Input ポートが `Port.Capacity.Single`、かつ `ConnectEdge` が接続前に `RemoveEdgeByChild(child)` を呼ぶ |
| E-5 | **Identity は一意** | `SceneGraphValidator.IsValidIdentity` + `ValidateIdentities`（重複は Error） |
| E-6 | **位置変更は階層データに影響しない** | `Layouts/*.asset` と `Graphs/*.asset` のファイル物理分離。ノードをドラッグしただけで `Graphs/` が dirty になってはいけない |
| E-8 | **SceneAsset D&D 時は Identity を SceneAsset 名に自動ロック** | `SceneNodeData.OnValidate` + Inspector の TextField 無効化 |

### 2.3 本作業に固有の罠（最重要）

#### (a) `SceneNodeData.OnValidate` が Identity を Payload[0] のシーン名で上書きする

これが Copy/Paste 設計の分岐点になる。実際のコード（`Data/SceneNodeData.cs:44-74`）:

```csharp
/// <summary>
/// W-5: Payload[0] の SceneAsset が変更されたとき Identity を自動同期する。
/// Inspector 直接編集や Undo/Redo 時にも反応する。
/// </summary>
private void OnValidate()
{
    if (_payloads.Count == 0) return;

    var payload0 = _payloads[0];
    if (payload0?.Reference == null) return;

    var guid = payload0.Reference.AssetGUID;
    if (string.IsNullOrEmpty(guid)) return;

    var assetPath = AssetDatabase.GUIDToAssetPath(guid);
    if (string.IsNullOrEmpty(assetPath)) return;

    var assetName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
    if (string.IsNullOrEmpty(assetName)) return;

    // Identity が既に一致していれば何もしない
    if (_identity == assetName) return;

    _identity = assetName;
    name = assetName;
    EditorUtility.SetDirty(this);
}
```

**したがって「Payloads ごとノードを複製する」と、複製先の Identity が次の `OnValidate` で元ノードと同じ名前へ戻り、Identity 重複（E-5 違反 / V-2 Error）が静かに発生する。**

→ **複製時は Payloads を必ず空にすること。** LoadType は継承してよい。これは仕様であり、Console に理由を 1 行ログすること。

#### (b) `SceneNodeData` は複数グラフから共有される資産である

`CreateNode` は同名ノードを見つけると新規作成せず既存アセットを再利用する（R-6）。実際のコード（`ViewModel/SceneGraphViewModel.cs:224-229`）:

```csharp
// R-6: 同名ノードが存在すれば再利用
var existing = FindExistingNode(identity);
if (existing != null)
{
    return ReuseNode(existing, position);
}
```

`FindExistingNode` は `_nodes` に加えて `Assets/SceneGraphData/Nodes` 全体を検索する。つまり**同じ SceneNodeData アセットが複数のグラフに所属しうる**のが正常な状態である。

**この帰結として、あるグラフのエディタからアセットを実削除すると、別のグラフからは「勝手に消えた」ことになる。** §3.2 の Delete 意味論分離はこれも直す。

#### (c) エッジ / レイアウトは Identity 文字列ではなくオブジェクト参照で持つ

`Graphs/Total.asset` の実際の中身:

```yaml
_nodes:
- {fileID: 11400000, guid: b5502d478be8a9043b77f4b09867e67e, type: 2}
_edges:
- Parent: {fileID: 11400000, guid: b227859e1e46ae5499316864281c8f71, type: 2}
  Child:  {fileID: 11400000, guid: ae76a02156e84380890dd69ab66f22c3, type: 2}
```

したがって Identity をリネームしてもエッジは壊れない。逆に `Dictionary<SceneNodeData, ...>` / `HashSet<SceneNodeData>` は**参照等価**で回る（例: `SceneGraphView._nodeMap`）。同じアセットを 1 グラフに 2 回入れることはできない。

#### (d) Unity の破棄済みオブジェクトは「偽 null」になる

Undo で作成が取り消された `SceneNodeData` は、C# 参照としては生きているが `node == null` が true になる。`new SerializedObject(destroyedNode)` は例外を投げる。**選択リストと `_nodes` からは必ず `== null` チェックで除外すること。** `is null` / `ReferenceEquals` は使わない（偽 null を検出できない）。

#### (e) `ConnectEdge` / `DisconnectEdge` は意図的に `OnGraphChanged` を発火しない

GraphView 側がビジュアルエッジを追加/削除するため。この契約は維持すること（発火させると二重にエッジが増える）。既存コードのコメント（`SceneGraphViewModel.cs:374`）:

```csharp
// ⮿ OnGraphChanged は発火しない（GraphView がビジュアルエッジを追加する）
```

---

## 3. 変更内容

### 3.0 全体像

背骨は **ViewModel のバッチスコープ**。これを先に作ってから、他をその上に載せる。

```
BeginBatch("Paste 3 nodes")
   ├ Undo.IncrementCurrentGroup() → SetCurrentGroupName() → GetCurrentGroup()
   ├ 期間中の OnGraphChanged は保留フラグに落とす（RebuildGraph の連打・再入を防ぐ）
   ├ 期間中の AssetDatabase.SaveAssets() は呼ばない
   └ Dispose:
        Undo.CollapseUndoOperations(groupIndex)
        AssetDatabase.SaveAssets()
        保留していれば OnGraphChanged を 1 回だけ発火
```

### 3.1 `SceneGraphViewModel` — バッチスコープ

対象: `unity/Assets/OneStarMaker/Scripts/Editor/SceneGraph/ViewModel/SceneGraphViewModel.cs`

追加するもの:

```csharp
private int _batchDepth;
private int _batchGroupIndex;
private bool _graphChangedPending;

/// <summary>
/// 一括操作スコープ。Undo を 1 グループへ畳み、OnGraphChanged を Dispose 時に 1 回だけ発火し、
/// AssetDatabase.SaveAssets() も 1 回に集約する。ネスト可（深さカウント）。
/// </summary>
public IDisposable BeginBatch(string undoName);

/// <summary>OnGraphChanged の発火。バッチ中は保留する。既存の直接 Invoke はすべてこれに置き換える。</summary>
private void RaiseGraphChanged();
```

実装要件:

- `BeginBatch` の入口（深さ 0 → 1 のときのみ）で **`Undo.IncrementCurrentGroup()` を必ず呼んでから** `Undo.SetCurrentGroupName(undoName)` → `Undo.GetCurrentGroup()` を保存する
  - **これは既存バグの修正でもある。** 現行 `DeleteNode`（`SceneGraphViewModel.cs:313-314`）と `SceneGraphView.OnDragPerform`（`SceneGraphView.cs:275-276`）は `IncrementCurrentGroup` 無しで `SetCurrentGroupName` + `GetCurrentGroup` を呼んでおり、直前の無関係な操作を同じグループへ巻き込む
- Dispose は深さ 1 → 0 のときだけ実効処理をする。ネストした内側の Dispose は何もしない
- 例外が起きても Dispose が走るよう `using` で使う
- **既存の単一コマンドもすべて内部で `BeginBatch` を通す**（`CreateNode` / `DeleteNode`→`RemoveNodesFromGraph` / `MoveNode` / `ConnectEdge` / `DisconnectEdge` / `RenameNode` / `CreateNodeWithSceneAsset`）。そうしないと単一と一括で Undo の粒度が変わる
- 各コマンド内に散っている `AssetDatabase.SaveAssets()`（現状 `SceneGraphViewModel.cs:208` / `264` / `298`）は削除し、バッチ末尾の 1 回に集約する。30 ノードのペーストで 30 回走るのを避ける

### 3.2 `SceneGraphViewModel` — Delete 意味論の分離

**現状（`SceneGraphViewModel.cs:309-345`）:**

```csharp
public void DeleteNode(SceneNodeData node)
{
    if (node == null) return;

    Undo.SetCurrentGroupName($"Delete Node '{node.Identity}'");
    var groupIndex = Undo.GetCurrentGroup();

    if (_currentEdges != null)
    {
        Undo.RecordObject(_currentEdges, "Remove node from graph");
        _currentEdges.RemoveNode(node);
        EditorUtility.SetDirty(_currentEdges);
    }

    if (_currentLayout != null)
    {
        Undo.RecordObject(_currentLayout, "Remove layout");
        _currentLayout.RemovePosition(node);
        EditorUtility.SetDirty(_currentLayout);
    }

    _nodes.Remove(node);
    var path = AssetDatabase.GetAssetPath(node);
    if (!string.IsNullOrEmpty(path))
    {
        AssetDatabase.DeleteAsset(path);      // ← ここが Undo で戻らない
    }

    if (_selectedNode == node)
        SelectedNode = null;

    Undo.CollapseUndoOperations(groupIndex);
    OnGraphChanged?.Invoke();
}
```

`AssetDatabase.DeleteAsset` は Undo 対象外。Undo するとエッジとレイアウトだけ復元され、**破棄済みオブジェクトへの参照が残る**（V-6「壊れた SO 参照」Error になる）。加えて §2.3(b) の共有資産事故もある。

**変更後 — 2 つに分ける:**

```csharp
/// <summary>
/// ノードを現在のグラフから除外する。関連エッジとレイアウトも消すが、
/// アセットファイルは削除しない（＝完全に Undo 可能）。
/// ノードは複数グラフから共有されるため、グラフからの除外と資産の削除は別操作とする。
/// </summary>
public void RemoveNodesFromGraph(IReadOnlyList<SceneNodeData> nodes);

/// <summary>
/// ノードアセットを実削除する。Undo 不可。呼び出す前に View 側で確認ダイアログを出すこと。
/// </summary>
public void DeleteNodeAssets(IReadOnlyList<SceneNodeData> nodes);

/// <summary>
/// 指定ノードを所属させている、現在のグラフ以外のグラフ名を列挙する。
/// 「Delete Node Asset…」の確認ダイアログで使う。
/// </summary>
public IReadOnlyList<string> FindOtherGraphsContaining(SceneNodeData node);
```

- 既存の `public void DeleteNode(SceneNodeData node)` は削除し、呼び出し側を `RemoveNodesFromGraph` に付け替える（呼び出し元は `SceneGraphView.cs:202`、`SceneGraphEditorWindow.cs:218`、`SceneGraphEditorWindow.cs:288` の 3 箇所）
- `RemoveNodesFromGraph` は `BeginBatch($"Remove {n} node(s) from graph")` で囲む
- `DeleteNodeAssets` は Undo 不可なので `BeginBatch` を使わない。削除前に `Undo.ClearAll()` は**呼ばないこと**（他の履歴まで消える）。代わりに Console に「この操作は Undo できません」を 1 行出す
- `FindOtherGraphsContaining` は `GetAvailableGraphs()`（既存・`SceneGraphViewModel.cs:60`）を再利用し、`ContainsNode` で判定する

### 3.3 `SceneGraphViewModel` — 複数選択

**現状（`SceneGraphViewModel.cs:31-53`）:**

```csharp
private SceneNodeData? _selectedNode;
public event Action<SceneNodeData?>? OnSelectionChanged;

public SceneNodeData? SelectedNode
{
    get => _selectedNode;
    set
    {
        if (_selectedNode == value) return;
        _selectedNode = value;
        OnSelectionChanged?.Invoke(_selectedNode);
    }
}
```

**変更後:**

```csharp
private readonly List<SceneNodeData> _selectedNodes = new();

/// <summary>選択中のノード（GraphView の選択順）。</summary>
public IReadOnlyList<SceneNodeData> SelectedNodes => _selectedNodes;

/// <summary>単一選択時のみ非 null。Inspector の単一編集用。</summary>
public SceneNodeData? SelectedNode => _selectedNodes.Count == 1 ? _selectedNodes[0] : null;

public event Action<IReadOnlyList<SceneNodeData>>? OnSelectionChanged;

/// <summary>選択を差し替える。破棄済みオブジェクトは除外する。内容が同じなら何もしない。</summary>
public void SetSelection(IReadOnlyList<SceneNodeData> nodes);
```

- `SelectedNode` の **setter は廃止**（`SetSelection` に一本化）
- `SetSelection` の中で `node == null`（Unity の偽 null）を除外する — §2.3(d)
- 比較は「順序込みの逐次比較」。同一なら `OnSelectionChanged` を発火しない
- `RemoveNodesFromGraph` / `DeleteNodeAssets` は、消したノードを選択から外す

### 3.4 `SceneGraphViewModel` — 一括コマンド

追加する public API:

```csharp
/// <summary>複数ノードの位置をまとめて更新する。Undo は 1 グループ。</summary>
public void MoveNodes(IReadOnlyList<(SceneNodeData Node, Vector2 Position)> moves);

/// <summary>
/// children をすべて parent の子にする。個別にサイクル判定し、
/// 失敗したものは 1 つのメッセージへまとめて報告する（ダイアログ連打を防ぐ）。
/// </summary>
/// <returns>接続に成功した数。</returns>
public int ConnectEdges(SceneNodeData parent, IReadOnlyList<SceneNodeData> children);

/// <summary>指定ノードの親エッジをまとめて切断する。</summary>
public void DisconnectEdges(IReadOnlyList<SceneNodeData> children);

/// <summary>
/// 既存ノードを現在のグラフへまとめて追加する（参照ペースト）。
/// 既に所属しているノードはスキップする。
/// </summary>
/// <returns>実際に追加されたノード。</returns>
public IReadOnlyList<SceneNodeData> AddExistingNodesToGraph(
    IReadOnlyList<(SceneNodeData Node, Vector2 Position)> entries);

/// <summary>
/// ノードを新しいアセットとして複製する。
/// Identity は一意な新名、LoadType は継承、Payloads は空（§2.3(a) の W-5 対策）。
/// </summary>
public SceneNodeData? DuplicateNode(SceneNodeData source, Vector2 position);
```

実装要件:

- すべて `BeginBatch` で囲む
- `ConnectEdges` / `AddExistingNodesToGraph` は **必ず `_currentEdges.AddNode(node)` で membership を保証する**
  - 現行 `ConnectEdge`（`SceneGraphViewModel.cs:353-376`）は `AddEdge` するだけで `AddNode` を呼んでいない。グラフ未所属のノードに繋ぐと、`GetRoots()` が `_nodes` しか走査しないため孤立検出（V-5）とルート抽出が壊れる。**`ConnectEdge`（単一版）にも同じ修正を入れること**
- `ConnectEdges` は `WouldCreateCycle` を **1 件接続するたびに再評価する**（先に繋いだ結果でサイクルになる組み合わせがあるため、最初に一括判定してはいけない）
- `MoveNodes` は `_currentLayout` に対する `Undo.RecordObject` を **バッチの先頭で 1 回だけ**行えばよい（同一オブジェクトへの複数記録は無駄）

### 3.5 `SceneGraphViewModel` — 一意 Identity 採番の共通化

**現状のバグ（`SceneGraphViewModel.cs:237-243`）:**

```csharp
var path = $"{NodesFolder}/{identity}.asset";
if (AssetDatabase.AssetPathExists(path))
{
    // rename
    path = AssetDatabase.GenerateUniqueAssetPath(path);
}
AssetDatabase.CreateAsset(node, path);
```

`node.Identity` / `node.name` は元の `identity` のままなので、**ファイル名だけ `Xxx 1.asset` になり Identity と食い違う。** 以後 `RenameNode` の `AssetDatabase.RenameAsset` と噛み合わなくなる。

**変更後 — 共通ヘルパを作り、`CreateNode` と `DuplicateNode` の両方が通す:**

```csharp
/// <summary>
/// _nodes と NodesFolder のアセットパスの両方が空くまでループし、
/// identity == アセットファイル名 を保証した組を返す。
/// AssetDatabase.GenerateUniqueAssetPath は "Title 1.asset"（空白入り）を返しうるので使わない。
/// </summary>
private bool TryGenerateUniqueIdentity(string baseName, out string identity, out string assetPath);
```

既存の `GenerateUniqueName`（`SceneGraphViewModel.cs:138-153`。"NewScene", "NewScene1", "NewScene2"… を返す）の採番規則をそのまま使い、**加えて `{NodesFolder}/{candidate}.asset` が存在しないことも条件に加える**。`GenerateUniqueName` 自体は ContextMenu の "Create Node" から使われているので public のまま残してよい。

### 3.6 新規ファイル: `ViewModel/SceneGraphClipboard.cs`

```csharp
#nullable enable

namespace OneStarMaker.Editor.SceneGraph
{
    [Serializable]
    internal sealed class SceneGraphClipboardEntry
    {
        public string NodeGuid = string.Empty;   // コピー元 SceneNodeData のアセット GUID
        public string Identity = string.Empty;   // 表示・複製時のベース名に使う
        public int LoadType;                     // (int)LoadType
        public Vector2 Position;                 // コピー時点の座標（絶対）
    }

    [Serializable]
    internal sealed class SceneGraphClipboardLink
    {
        public int ParentIndex;                  // Nodes 配列の index
        public int ChildIndex;
    }

    [Serializable]
    internal sealed class SceneGraphClipboardData
    {
        public const string TypeTag = "OneStarMaker.SceneGraph.Clipboard";
        public const int CurrentVersion = 1;

        public string Type = TypeTag;            // 他ツールの GraphView データを弾くためのマジック
        public int Version = CurrentVersion;
        public string SourceGraphGuid = string.Empty;
        public List<SceneGraphClipboardEntry> Nodes = new();
        public List<SceneGraphClipboardLink> Edges = new();
    }

    internal static class SceneGraphClipboard
    {
        public static string Serialize(SceneGraphClipboardData data);
        public static SceneGraphClipboardData? TryDeserialize(string json);
        public static bool CanPaste(string json);
    }
}
```

**テスト可能性のための必須要件:**

`Serialize` / `TryDeserialize` / `CanPaste`、および「コピー集合の内部エッジだけを抽出する」判定は、**`AssetDatabase` にも `UnityEditor` にも依存しない純粋関数として書くこと。** GUID → `SceneNodeData` の解決は呼び出し側（ViewModel / View）に置く。これができていないと §4.3 のテストが書けない。

- 直列化は `JsonUtility.ToJson` / `JsonUtility.FromJson<T>`
- `TryDeserialize` は例外を握りつぶして null を返す（他ツールの JSON や壊れた文字列が来る）
- `CanPaste` は `Type == TypeTag && Version == CurrentVersion && Nodes.Count > 0` を確認する
- エッジは**両端がコピー集合に含まれているものだけ**を入れる

### 3.7 `SceneGraphView` — クリップボード配線と Delete 経路の統一

対象: `View/SceneGraphView.cs`。コンストラクタで `graphViewChanged` を代入している箇所（`SceneGraphView.cs:67`）の直後に追加する。

```csharp
graphViewChanged        = OnGraphViewChanged;      // 既存
serializeGraphElements  = OnSerializeGraphElements;
canPasteSerializedData  = OnCanPasteSerializedData;
unserializeAndPaste     = OnUnserializeAndPaste;
deleteSelection         = OnDeleteSelection;
```

この 5 つが GraphView 標準の Ctrl+C / Ctrl+V / Ctrl+X / Ctrl+D / Delete を受ける口になる。**現在これらのうち `graphViewChanged` しか代入されていないため、Copy/Paste は完全な no-op になっている。**

#### 3.7.1 Copy / Paste の意味論（決定済み — 変更しないこと）

| 操作 | 条件 | 挙動 |
|---|---|---|
| **Paste** (`operationName == "Paste"`) | 解決できたノードのうち **1 つでも現在のグラフに未所属** | **参照ペースト**: 同じアセットを現在のグラフへ追加 + コピー集合内のエッジを再現 + 相対位置を維持 |
| **Paste** | 全ノードが既に現在のグラフに所属（＝同一グラフ内でのコピー） | **複製**: 新しい一意 Identity で新規アセットを作る。LoadType 継承、**Payloads は空** |
| **Duplicate** (`operationName == "Duplicate"`, Ctrl+D) | 常に | **複製**（上と同じ） |

`unserializeAndPaste` の第 1 引数 `operationName` で Paste と Duplicate を分岐する。

この分岐にした理由（実装者向け）: 同一グラフへの参照ペーストは「既に所属しているので何も起きない」になってしまい無意味。一方で異なるグラフへの複製は、共有資産である `SceneNodeData` をいたずらに増やすだけで意味がない。§2.3(a)(b) を踏まえるとこの組み合わせしか成立しない。

#### 3.7.2 実装の細部

- **自前メソッドを作り、デリゲートはそこへ委譲する。**

  ```csharp
  public void CopySelectionToClipboard();
  public void PasteFromClipboard();
  public void DuplicateSelection();
  public void DeleteSelection();
  ```

  ContextMenu の項目もツールバーもこの public メソッドを呼ぶ。Unity の `CopySelectionCallback()` 等は版によってアクセシビリティが違うので**依存しない**
- クリップボード文字列の保管は `EditorGUIUtility.systemCopyBuffer`（GraphView 標準の `serializeGraphElements` 経路を使う場合は Unity が自動でやる。自前メソッドから呼ぶときは明示的に書く）
- 貼り付け位置は**元座標 +(40, 40) の一律オフセット**。マウス位置ペーストはやらない（§5）
- GUID から `SceneNodeData` を解決できなかったエントリ（コピー後にアセットが消された等）はスキップし、Console に警告を 1 行出す
- ペースト後は生成/追加されたノードを選択状態にする（`ClearSelection()` → 各ノードを `AddToSelection`）
- 参照ペースト・複製とも `_viewModel.BeginBatch("Paste N node(s)")` の内側で行い、**1 回の Ctrl+Z で全部戻る**こと

#### 3.7.3 `OnGraphViewChanged` の一括化

**現状（`SceneGraphView.cs:159-221`）は要素ごとに ViewModel を呼んでいる:**

```csharp
if (graphViewChange.elementsToRemove != null)
{
    foreach (var element in graphViewChange.elementsToRemove)
    {
        if (element is Edge edge) { ... _viewModel.DisconnectEdge(childNode.NodeData); }
        else if (element is SceneGraphNode node) { _viewModel.DeleteNode(node.NodeData); }
    }
}
```

`DeleteNode` は `OnGraphChanged` を発火し、それが `RebuildGraph()` を呼ぶ。**GraphView が `elementsToRemove` を処理している最中に全要素を撤去する再入**になる。`_isRebuilding` フラグ（`SceneGraphView.cs:24`）はハンドラの再入は止めるが、この撤去自体は止められない。ノードを 5 個まとめて消すと 5 回リビルドが走る。

**変更後:** ハンドラ全体を 1 つの `BeginBatch` で囲み、削除対象・切断対象・移動対象をそれぞれリストに集めてから一括コマンドを 1 回ずつ呼ぶ。`OnGraphChanged` はバッチ Dispose 時の 1 回だけになるので、GraphView の処理が終わってからリビルドされる。

```csharp
private GraphViewChange OnGraphViewChanged(GraphViewChange graphViewChange)
{
    if (_isRebuilding) return graphViewChange;

    using (_viewModel.BeginBatch("Edit Scene Graph"))
    {
        // edgesToCreate → 1 件ずつ ConnectEdge（サイクル判定のため）。失敗したエッジは validEdges から除外
        // elementsToRemove → Edge と Node を別リストに集め、DisconnectEdges / RemoveNodesFromGraph を 1 回ずつ
        // movedElements → (node, pos) を集めて MoveNodes を 1 回
    }
    return graphViewChange;
}
```

#### 3.7.4 `deleteSelection` への一本化

現在 Delete 経路が 2 本ある:

- `SceneGraphEditorWindow.OnKeyDown`（`SceneGraphEditorWindow.cs:283-291`）— `_viewModel.SelectedNode` **1 件だけ**削除して `StopPropagation()`
- GraphView 標準の削除 → `graphViewChanged` の `elementsToRemove` → **選択全件**削除

同じ Delete キーで挙動が食い違う。**Window 側の `OnKeyDown` の Delete 分岐を削除し**、`deleteSelection` デリゲートに一本化する。`OnKeyDown` の登録自体（`SceneGraphEditorWindow.cs:63`）は他のキーを足す余地があるので残してよいが、中身が空になるなら登録ごと消す。

`OnDeleteSelection(string operationName, GraphView.AskUser askUser)` は「グラフから除外」（§3.2）を行う。アセットの実削除はここからは**絶対に呼ばない**。

### 3.8 `SceneGraphView` — ContextMenu 拡張

対象: `OnContextMenuPopulate`（`SceneGraphView.cs:223-240`）。現状は `Create Node` と `Auto Layout` の 2 項目だけ。

右クリック対象のノードは `evt.target` から解決する（`evt.target as SceneGraphNode`、ダメなら `VisualElement.GetFirstAncestorOfType<SceneGraphNode>()`）。

**適用できない項目は非表示にせず `DropdownMenuAction.Status.Disabled` にすること**（項目の位置が動くとミスクリックの元になる）。

```
Create Node                                  常に有効
──────────────
Copy                                         選択 ≥ 1
Paste                                        クリップボードが有効（SceneGraphClipboard.CanPaste）
Duplicate                                    選択 ≥ 1
──────────────
Parent to '<右クリックしたノードの Identity>'   選択 ≥ 2 かつ 右クリック対象がノード
Unparent Selected                            選択の中に親を持つノードが 1 つ以上ある
──────────────
Remove from Graph                            選択 ≥ 1          ← Undo 可
Delete Node Asset…                           選択 ≥ 1          ← 確認ダイアログ / Undo 不可
──────────────
Auto Layout                                  常に有効
Frame Selection                              選択 ≥ 1
```

- **「Parent to」の親は右クリックしたノードとする。** 選択の順序には依存させない（GraphView の選択順は D&D やショートカットで直感に反することがある）。子になるのは「選択されているノードのうち、右クリック対象自身を除いたもの」
- 「Unparent Selected」は `_viewModel.DisconnectEdges(選択のうち親を持つもの)`
- 「Delete Node Asset…」は `EditorUtility.DisplayDialog` で確認する。ダイアログ本文には
  - 削除するノード名（多い場合は先頭 10 件 + "…and N more"）
  - `FindOtherGraphsContaining` で見つかった**他グラフでの使用箇所**
  - **「この操作は Undo できません」**

  を必ず含める。OK ボタンのラベルは "Delete Assets"、キャンセルは "Cancel"

### 3.9 `SceneGraphEditorWindow`

対象: `View/SceneGraphEditorWindow.cs`

#### 3.9.1 選択ポーリングを複数対応にする

**現状（`SceneGraphEditorWindow.cs:296-313`）:**

```csharp
private SceneNodeData? _lastPolledSelection;

private void PollGraphViewSelection()
{
    if (_graphView == null || _viewModel == null) return;

    var selectedNodes = _graphView.selection
        .OfType<SceneGraphNode>()
        .ToList();

    var current = selectedNodes.Count == 1 ? selectedNodes[0].NodeData : null;   // ← ここで潰れている

    if (current != _lastPolledSelection)
    {
        _lastPolledSelection = current;
        _viewModel.SelectedNode = current;
    }
}
```

**変更後:** 選択されている `SceneGraphNode` の `NodeData` を**全件**リストにし、前回と順序込みで比較して変化があれば `_viewModel.SetSelection(list)` を呼ぶ。

**ポーリング方式（100ms、`SceneGraphEditorWindow.cs:67`）はそのまま維持すること。** `GraphView.AddToSelection` / `RemoveFromSelection` / `ClearSelection` を override する方式に乗り換えたくなるが、矩形選択が `ClearSelection` → N 回の `AddToSelection` を発行するなど発火パターンが版依存で、今回のリスクに見合わない。

#### 3.9.2 Undo/Redo 後の同期を強化する

**現状（`SceneGraphEditorWindow.cs:315-319`）:**

```csharp
private void OnUndoRedo()
{
    _viewModel?.RefreshNodes();
    _graphView?.RebuildGraph();
}
```

追加すること:

1. **破棄済みノードを選択から外す** — `RefreshNodes()` の直後に `_viewModel.SetSelection(現在の選択のうち生きているもの)`。これをやらないと Inspector の `DrawPayloads` が `new SerializedObject(destroyedNode)` で例外を投げる（`SceneGraphInspectorPanel.cs:145`）
2. **Identity とアセットファイル名の再同期** — `RenameNode`（`SceneGraphViewModel.cs:414-436`）は `AssetDatabase.RenameAsset` を Undo 非対応のまま呼んでいる。Undo すると `node.Identity` だけ戻ってファイル名が残る。ViewModel に

   ```csharp
   /// <summary>Undo/Redo 後に Identity とアセットファイル名の食い違いを直す。</summary>
   public void SyncAssetNamesToIdentity();
   ```

   を追加し、`_nodes` を走査して `Path.GetFileNameWithoutExtension(path) != node.Identity` のものだけ `AssetDatabase.RenameAsset` する。`OnUndoRedo` から呼ぶ

#### 3.9.3 その他

- ツールバーの `Delete` ボタン（`OnDeleteSelected`, `SceneGraphEditorWindow.cs:214-220`）→ `_graphView?.DeleteSelection()` に変更。ラベルは `Remove from Graph` に改名する（意味論が変わったため）
- `OnKeyDown` の Delete 分岐を削除（§3.7.4）

### 3.10 `SceneGraphInspectorPanel`

対象: `View/SceneGraphInspectorPanel.cs`

- `OnSelectionChanged` のシグネチャ変更に追随（`Action<SceneNodeData?>` → `Action<IReadOnlyList<SceneNodeData>>`）
- 表示は 3 状態:
  - 0 件 → 現状の "No node selected"
  - 1 件 → 現状どおり（Identity / LoadType / Payloads）
  - **2 件以上 → "N nodes selected" のラベルのみ**。編集フィールドは隠す（複数編集は §5 のとおり今回やらない）
- `DrawPayloads`（`SceneGraphInspectorPanel.cs:140`）の先頭に `if (node == null) return;` の**偽 null チェック**を入れる（現状は `SelectedNode == null` しか見ておらず、破棄済みオブジェクトを通してしまう）

### 3.11 変更対象ファイル一覧

| ファイル | 変更 |
|---|---|
| `ViewModel/SceneGraphViewModel.cs` | バッチスコープ、複数選択、一括コマンド、Delete 分離、採番ヘルパ、`SyncAssetNamesToIdentity`、`ConnectEdge` の membership 保証 |
| `ViewModel/SceneGraphClipboard.cs` | **新規**（純粋 DTO + 直列化） |
| `View/SceneGraphView.cs` | クリップボード 3 デリゲート + `deleteSelection`、ContextMenu 拡張、`OnGraphViewChanged` の一括化 |
| `View/SceneGraphEditorWindow.cs` | 複数選択ポーリング、Delete 経路統一、`OnUndoRedo` 強化、ツールバー改名 |
| `View/SceneGraphInspectorPanel.cs` | 選択イベントのシグネチャ追随 + 偽 null ガード |
| `Tests/Editor/SceneGraph/*.cs` | **新規**（§4.3） |

**`unity/Assets/OneStarMaker/Scripts/Editor/AssemblyInfo.cs` は作成済み。触らないこと。**

```csharp
#nullable enable

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("OneStarMaker.Tests.Editor")]
```

これがあるので §3.6 の `SceneGraphClipboard` を `internal` のままテストから参照できる。`Foundation` / `Runtime` にも同じパターンの `AssemblyInfo.cs` が既にある。

`Data/*.cs` と `SceneResourceGenerator.cs` / `SceneGraphValidator.cs` は原則触らない。触る必要が出たら停止して報告すること。

---

## 4. 受け入れ条件

### 4.1 修正されているべきバグ

| # | 内容 | 確認方法 |
|---|---|---|
| B1 | Delete がアセット実削除で Undo 不可 | ノードを Delete → `Assets/SceneGraphData/Nodes/` にファイルが残っている → Ctrl+Z でグラフに復帰 |
| B2 | `CreateNode` が Undo グループ化されていない | ノードを 1 つ作る → Ctrl+Z **1 回**でノード・エッジ・レイアウトすべて消える |
| B3 | `RenameNode` の `AssetDatabase.RenameAsset` が Undo 非対応 | リネーム → Ctrl+Z → Identity とアセットファイル名の**両方**が元に戻る |
| B4 | Undo 後に破棄済み SO が選択に残り Inspector が例外を投げる | ノード作成 → 選択したまま Ctrl+Z → Console に例外が出ない |
| B5 | `DeleteNode` が `elementsToRemove` 走査中に `RebuildGraph` を再入させる | 5 ノードを選択して Delete → Console に例外・警告が出ない。リビルドは 1 回 |
| B6 | Delete 経路が二重（Window の単一削除と GraphView の全件削除） | 3 ノード選択 → Delete キー → **3 つとも**消える |
| B7 | `CreateNode` のパス衝突時に Identity とファイル名がずれる | 複製を繰り返しても、常に `Identity == アセットファイル名` |
| B8 | `SetCurrentGroupName` を `IncrementCurrentGroup` 無しで呼び直前操作を巻き込む | ノード A を移動 → ノード B を削除 → Ctrl+Z **1 回**で B の削除だけ戻り、A の移動は戻らない |
| B10 | `ConnectEdge` が membership を保証しない | 接続後に `Graphs/Total.asset` の `_nodes` に両端が入っている |
| B11 | 全コマンドが個別に `SaveAssets()` を呼ぶ | 10 ノードのペーストで `SaveAssets` が 1 回だけ |

### 4.2 手動での動作確認

Unity Editor で `OneStarMaker → Scene Graph Editor` を開き、`Total` グラフで:

1. 矩形選択で 3 ノード → まとめてドラッグ移動 → **Ctrl+Z 1 回**で 3 つとも元位置に戻る
2. 同じ 3 ノード（親子関係を含むもの）を Ctrl+C → Ctrl+V
   - 新しい Identity の 3 ノードができる
   - **内部の親子エッジが再現されている**
   - 複製されたノードの **Payloads は空**
   - **Ctrl+Z 1 回**で 3 つとも消える
3. Ctrl+D（Duplicate）でも 2 と同じ結果になる
4. `New Graph` で 2 つ目のグラフを作り、1 で選んだ 3 ノードを Ctrl+C → 新グラフで Ctrl+V
   - **同じアセットが参照追加**される（`Nodes/` にファイルが増えない）
   - 内部エッジが再現される
   - 元の `Total` グラフは無傷
5. 複数選択 → ノード上で右クリック → `Parent to '<node>'` → 選択が一括で子になる → **Ctrl+Z 1 回**で戻る
6. サイクルになる組み合わせで 5 を実行 → その分だけ拒否され、**1 つのダイアログ**にまとめて報告される（連打されない）
7. 複数選択 → 右クリック → `Unparent Selected` → 一括で親が外れる → **Ctrl+Z 1 回**で戻る
8. Delete キー → グラフから消えるが `Assets/SceneGraphData/Nodes/*.asset` は**残っている** → Ctrl+Z で復帰
9. 右クリック → `Delete Node Asset…` → 確認ダイアログに他グラフでの使用箇所と「Undo できません」が表示される
10. ノードをドラッグしただけの状態で `git status` → `SceneGraphData/Layouts/` だけが変更され、`Graphs/` は変更されていない（E-6）
11. ツールバーの `Validate` → 作業前と比べて新規の Error / Warning が増えていない

### 4.3 追加するテスト

配置: `unity/Assets/OneStarMaker/Tests/Editor/SceneGraph/`
`unity/Assets/OneStarMaker/Tests/Editor/OneStarMaker.Tests.Editor.asmdef` は既に `OneStarMaker.Editor` を参照しているので **asmdef の変更は不要**。

| テストクラス | 依存 | 内容 |
|---|---|---|
| `SceneGraphClipboardTests` | なし（純粋） | ① 往復直列化で Nodes / Edges / Position が保たれる ② 他ツールの JSON（`Type` 不一致）を `CanPaste` が false にする ③ 壊れた JSON / 空文字で例外を投げず false ④ **コピー集合の片端しか含まないエッジが除外される** |
| `SceneGraphEdgesTests` | `ScriptableObject.CreateInstance` のみ | ① `WouldCreateCycle` の自己ループ（A→A）② 多段（A→B→C のとき C→A）③ `RemoveEdgeByChild` ④ `RemoveNode` で当該ノードのエッジも消える ⑤ `GetRoots` |
| `SceneGraphViewModelBatchTests` | `AssetDatabase`（一時フォルダ） | ① `RemoveNodesFromGraph` 複数 → `Undo.PerformUndo()` **1 回**で membership・エッジ・レイアウトが全て復元 ② 参照ペースト相当（`AddExistingNodesToGraph` + `ConnectEdges`）が Undo **1 回**で戻る ③ `DuplicateNode` の結果が Payloads 空・Identity 一意・`Identity == ファイル名` |

`SceneGraphViewModelBatchTests` は `[SetUp]` で `Assets/__SceneGraphEditorTests__` を作り、`[TearDown]` で `AssetDatabase.DeleteAsset` する。**既存の `Assets/SceneGraphData/` には絶対に書き込まないこと。** `SceneGraphViewModel` のフォルダ定数（`SceneGraphViewModel.cs:22-24`）はハードコードされているので、テスト用にフォルダを差し替えられるようにする必要がある場合は

```csharp
private const string NodesFolder = "Assets/SceneGraphData/Nodes";
```

を `internal static string NodesFolder { get; set; }` 相当にする、程度の最小変更に留めること。**それ以上の抽象化レイヤ（`IAssetDatabase` 等）は導入しない。**

### 4.4 コンパイル

- 新規・変更したすべての `.cs` の先頭に `#nullable enable` がある
- nullable 警告が新規に出ていない

---

## 5. やらないこと

以下は**このスライスの範囲外**。手を出さないこと。次のスライスで別 HANDOFF を切る。

### 5.1 次スライスの最優先項目: `SceneGraphView` の分割

> この項目はレビュー（§7.5）で追記されたもの。**本スライスが生んだ負債**であり、次スライスの筆頭に置く。

`SceneGraphView.cs` は 368 → 809 行、27 メンバー 6 責務に膨らんだ。最大の問題は `ApplyPaste`（約 120 行）にペースト方針判断というドメインロジックが埋まっており、**単体テストが書けないこと**。

| 抽出先 | 移すもの | ねらい |
|---|---|---|
| `Service/SceneGraphPasteService.cs` | `ApplyPaste` / `BuildClipboardJson` / `GetGuidForNode` | **最重要。** 参照追加 vs 複製の判定・再親付け検出・オフセットをテスト可能にする。入出力を `GraphElement` ではなく `SceneNodeData` + 座標のリストにすれば、`AssetDatabase` 依存は GUID 解決だけに封じ込められる |
| `View/SceneGraphContextMenu.cs` | `OnContextMenuPopulate` / `ResolveContextTargetNode` / `ConfirmAndDeleteNodeAssets` | メニュー構成と確認ダイアログは描画と無関係 |
| `Service/SceneGraphAutoLayout.cs` | `PerformAutoLayout` / `LayoutTree` | 純粋な木配置アルゴリズム。単体テストが書ける |
| `View/SceneGraphSceneAssetDropHandler.cs` | `OnDragUpdated` / `OnDragPerform` / `HasSceneAssetInDrag` / `GetSceneAssetPathsFromDrag` | D&D は独立した入力経路 |

`SceneGraphView` に残すのは要素ライフサイクル（`RebuildGraph` / `ScheduleRebuild` / `AddNodeElement` / `OnGraphViewChanged` / `GetCompatiblePorts`）とデリゲート配線のみ。

`SceneGraphViewModel`（550 → 1008 行）の分割も併せて検討する（バッチスコープは独立クラスに切り出せる）。

**抽出と同時に、ペースト方針の単体テストを必須とすること。** テストが書けないなら抽出が足りていない。

### 5.2 その他

- **既存コードに残る `?.` / `??` の偽 null 迂回**（§8.2 で C' 監査が検出）— `View/SceneGraphView.cs:169`、`View/SceneGraphEditorWindow.cs:167`。本スライスが新規に入れた `SceneGraphView.cs:386` のみ修正済み。既存分は別スライスで一括対応する。あわせて **§2.3(d) の記述に `?.` / `??` を追加**すること
- **`RebuildGraph` がグラフ未所属ノードまで描画する問題（B9）** — `SceneGraphView.cs:115-119` は `_viewModel.Nodes`（= `Nodes` フォルダの全 30 アセット）を走査しており、現在のグラフに所属していないノードまで描画する。レイアウト未登録なので (0,0) に重なる。**現在は `Total` グラフに 30 件すべてが所属しているため顕在化していないが、2 つ目のグラフを作った瞬間に出る。** 修正には「Add Existing Node…」検索ピッカーの新設もセットで必要なので、次スライスに回す
  - ただし §4.2-4（2 つ目のグラフでの参照ペースト確認）を行う際にこの症状が見えるはずなので、**見えても直さずそのままにしておくこと**
- ノード上へのバリデーション結果（V-1〜V-6）バッジ表示
- Generate stale のツールバー常時表示（現在は起動時 1 回の Console 警告）
- Inspector の複数編集（LoadType の一括変更）
- ノードのダブルクリックで Payload[0] のシーンを開く / Select in Project
- マウス位置へのペースト（今回は元座標 +(40,40) 固定）
- 選択サブツリーだけの Auto Layout
- 選択ポーリング（100ms）を GraphView の選択イベント経路へ置き換えること
- `SceneResourceGenerator.cs` / `SceneGraphValidator.cs` の変更
- `AssetDatabase` を抽象化するテスト用レイヤの導入
- **git commit / push。** 作業ツリーには本作業と無関係の未コミット変更が多数あるので、勝手にコミットしないこと

---

## 6. 差し戻し

### 第 1 巡（2026-08-05 / レビュー: Claude Code）

実装の水準は高い。§3 の項目はほぼ網羅されており、W-5（複製の Payloads 空）と偽 null 判定（`== null`）と `Undo.IncrementCurrentGroup()` はいずれも正しく入っている。以下 5 件を修正すること。

---

#### R1（要修正・高）B5 が実際には直っていない。リビルドが GraphView の処理中に走る (fixed)

`SceneGraphView.OnGraphViewChanged` で `using (_viewModel.BeginBatch(...))` のブロックが **`return graphViewChange;` より前に閉じる**。そのため:

```
EndBatch() → OnGraphChanged → RebuildGraph() で全 Node/Edge を RemoveElement
   ↓
ハンドラが return
   ↓
GraphView が graphViewChange の続きを適用する
   ├ elementsToRemove … 既に消えた要素を再度削除しようとする
   └ edgesToCreate    … 既にデタッチされた古い SceneGraphNode のポートに繋がる Edge を AddElement
                        → 宙に浮いたエッジが残る
```

要素ごとの連打（N 回リビルド）は解消されているが、**再入ハザードそのものは残っている**。

**これは HANDOFF §3.7.3 の記述ミス。** 「`OnGraphChanged` はバッチ Dispose 時の 1 回だけになるので、GraphView の処理が終わってからリビルドされる」と書いたが、Dispose は `return` の前に起きるので後段の処理は終わっていない。実装は仕様に忠実に従った結果であり、実装側の落ち度ではない。

**修正方法** — リビルドを次フレームへ遅延させる。`SceneGraphView` のコンストラクタで

```csharp
_viewModel.OnGraphChanged += RebuildGraph;
```

を

```csharp
_viewModel.OnGraphChanged += ScheduleRebuild;
```

に変え、コアレスするスケジューラを足す:

```csharp
private bool _rebuildScheduled;

/// <summary>
/// リビルドを次フレームへ遅延させる。graphViewChanged ハンドラの内側で同期的に
/// RebuildGraph を走らせると、GraphView が elementsToRemove / edgesToCreate を
/// 適用し終える前に全要素を撤去してしまう（B5）。
/// </summary>
private void ScheduleRebuild()
{
    if (_rebuildScheduled) return;
    _rebuildScheduled = true;
    schedule.Execute(() =>
    {
        _rebuildScheduled = false;
        RebuildGraph();
    }).ExecuteLater(0);
}
```

`PerformAutoLayout` と `SceneGraphEditorWindow.OnUndoRedo` からの `RebuildGraph()` 直接呼び出しは GraphView の処理中ではないのでそのままでよい。

**併せて必要な追従**: `ApplyPaste` の末尾でペースト結果を選択し直す処理は `_nodeMap` が更新済みであることを前提にしている。リビルドが遅延すると `_nodeMap` はまだ古い。**選択処理も同じく `schedule.Execute(...).ExecuteLater(0)` に載せる**こと（リビルドより後にキューされるので順序は保たれる）。

**対応内容**: `SceneGraphView` に `_rebuildScheduled` フィールドと `ScheduleRebuild()` を提案どおりに追加し、コンストラクタの購読を `_viewModel.OnGraphChanged += ScheduleRebuild;` に変更した。`PerformAutoLayout`（R3 対応後）と `SceneGraphEditorWindow.OnUndoRedo` からの直接 `RebuildGraph()` 呼び出しは指示どおりそのまま残した。`ApplyPaste` 末尾の選択復元処理も `schedule.Execute(...).ExecuteLater(0)` に載せ替え、`ScheduleRebuild` のスケジュールより後にキューされる（＝実行順序が保たれる）ことを確認した。

---

#### R2（要修正・中）`public new void DeleteSelection()` が基底メンバを隠している (fixed)

`SceneGraphView.cs` の

```csharp
public new void DeleteSelection()
```

は `GraphView` の同名メンバを `new` で隠している。基底側の経路から呼ばれた場合はこちらではなく基底実装が動くため、意図が保証されない。

**`RemoveSelectionFromGraph()` にリネームすること。** 呼び出し側は `OnDeleteSelection`（デリゲート）と `SceneGraphEditorWindow.OnDeleteSelected`（ツールバー）の 2 箇所。

**対応内容**: `public new void DeleteSelection()` を `public void RemoveSelectionFromGraph()` にリネーム（`new` 修飾子も削除）。呼び出し側 2 箇所（`SceneGraphView.OnDeleteSelection` デリゲートハンドラ、`SceneGraphEditorWindow.OnDeleteSelected`）を追従させた。`deleteSelection`（GraphView 標準デリゲートのフィールド名）自体は変更していない。

---

#### R3（要修正・中）`PerformAutoLayout` が `BeginBatch` を通っていない (fixed)

§3.1 は「既存の単一コマンドもすべて `BeginBatch` を通す」と定めているが、`SceneGraphView.PerformAutoLayout` は `Undo.RecordObject` を直接呼んでいる。**`IncrementCurrentGroup` が無いので B8（直前の無関係な操作を巻き込む）が Auto Layout にだけ残っている。**

`using (_viewModel.BeginBatch("Auto Layout"))` で囲むこと。ブロック内では `RebuildGraph()` の直接呼び出しをやめ、`RaiseGraphChanged` 経由（= R1 の `ScheduleRebuild`）に任せる。ViewModel 側に公開の再描画要求が無ければ、`PerformAutoLayout` の末尾でブロックを抜けた後に `ScheduleRebuild()` を呼ぶ形でよい。

**対応内容**: `PerformAutoLayout` の `_viewModel.CurrentEdges`/`CurrentLayout` をローカル変数へ捕まえた上で `using (_viewModel.BeginBatch("Auto Layout"))` で囲み、ブロック内の直接 `RebuildGraph()` 呼び出しを削除、ブロックを抜けた直後に `ScheduleRebuild()` を呼ぶ形にした。`LayoutTree`（再帰ヘルパ）は元のまま `_viewModel.CurrentEdges`/`CurrentLayout` を都度参照している（変更不要な既存コードのため触っていない）。

---

#### R4（要修正・低）`CanPaste` の Type チェックが実際には効いていない (fixed)

`JsonUtility.FromJson<T>` は **JSON に無いキーはフィールド初期化子の値のまま**にする。`SceneGraphClipboardData.Type` は

```csharp
public string Type = TypeTag;
```

と初期化されているため、**`"Type"` キーを持たない他ツールの JSON を食わせると `Type == TypeTag` が成立してしまう**。現状これを弾いているのは実質 `Nodes.Count == 0` のチェックだけで、`Nodes` 配列を持つ形の JSON なら通ってしまう。

既存テスト `CanPaste_ReturnsFalse_ForOtherToolsJsonWithMismatchedType` は `"Type"` を**明示的に持つ** JSON なので、この穴を踏んでいない。

**修正**: `Type` の初期化子を `string.Empty` にし、`Serialize` で明示的に `data.Type = TypeTag;` を設定する（あるいは同等の手当て）。**併せて「`Type` キーを持たない JSON を `CanPaste` が false にする」テストを 1 本足すこと。**

**対応内容**: `SceneGraphClipboardData.Type` の初期化子を `string.Empty` に変更し、`SceneGraphClipboard.Serialize` の先頭で `data.Type = SceneGraphClipboardData.TypeTag;` を明示的に設定するようにした。`SceneGraphClipboardTests.cs` に `CanPaste_ReturnsFalse_ForJsonWithoutTypeKey`（`"Type"` キーを持たない JSON で `CanPaste` が false になることを検証）を追加した。

---

#### R5（要修正・低）参照ペーストが既存の親子関係を黙って壊す (fixed)

`ApplyPaste` の参照ペースト経路は、クリップボード内のリンクを `ConnectEdges` で再現する。`ConnectEdges` は接続前に `RemoveEdgeByChild(child)` を呼ぶ（ツリー制約 E-4 のため正しい）。

その結果、**貼り付け先グラフに既に居るノードが別の親を持っていた場合、その親子関係が警告なしに差し替わる**。混在ケース（一部のノードだけ未所属で参照ペーストになる）で起きる。

挙動自体は「木の形ごと貼る」という意味論として許容するが、**黙って壊すのはやめること。** 既存の親が置き換わったノードがあれば、まとめて 1 行 Console に警告を出す（例: `[SceneGraph] Paste: re-parented N existing node(s): X, Y`）。

**対応内容**: `ApplyPaste` の参照ペースト経路（`!duplicate`）で、`ConnectEdges` を呼ぶ直前に `currentEdges.GetParent(child)` で既存の親を取得し、それが非 null かつ新しい親と異なる場合は Identity を `reparentedIdentities` に集約するようにした。複製ノードは新規アセットで既存の親を持ち得ないため、判定は参照ペースト時のみ行う。バッチを抜けた後に `[SceneGraph] Paste: re-parented N existing node(s): X, Y` の形式で 1 行にまとめて `Debug.LogWarning` する。

---

### 確認済み・修正不要（再確認の手間を省くため明記）

- W-5 対策（`DuplicateNode` が Payloads を空にする）— 正しい
- 偽 null 判定が全箇所 `== null` — 正しい。`is null` / `ReferenceEquals` の使用は無し
- `BeginBatch` 入口の `Undo.IncrementCurrentGroup()` — 正しい
- `ConnectEdge` / `DisconnectEdge` が `OnGraphChanged` を発火しない契約 — 維持されている
- `ConnectEdges` のサイクル判定を 1 件ごとに再評価 — 正しい
- `TryGenerateUniqueIdentity` が `identity == ファイル名` を保証（B7）— 正しい
- §5 の禁止事項（B9 / `SceneResourceGenerator.cs` / `SceneGraphValidator.cs` / `.asmdef`）— いずれも未変更
- `DeleteNodeAssets` が全グラフから membership とレイアウトを除去する判断 — **仕様より広いが妥当**。放置すると他グラフに null 参照（V-6 Error）が残るため、この解釈を採用する
- 確認ダイアログが英語であること — 既存 UI に合わせた判断として妥当
- テスト 3 クラスの構成と一時フォルダの扱い — 妥当

---

## 7. レビュー結果

> Phase C / 2026-08-05 / Claude Code（Opus 5）
> 実装は Claude Code サブエージェント（Sonnet）。2 巡で収束。

### 7.1 経緯

| 巡 | 内容 |
|---|---|
| 1 | 実装完了。レビューで R1〜R5 を指摘（§6） |
| 2 | R1〜R5 すべて修正を確認。**追加指摘なし → 収束** |

R1（B5 が実際には直っていない）は **HANDOFF §3.7.3 の記述ミスが原因**で、実装側は仕様どおりに書いていた。`BeginBatch` の Dispose は `return graphViewChange;` より前に走るため「GraphView の処理が終わってからリビルドされる」は誤り。`ScheduleRebuild`（次フレームへコアレス遅延）で修正済み。

### 7.2 §4.1 のバグ 10 件 — 差分と突き合わせた結果

| # | 状態 | 根拠 |
|---|---|---|
| B1 Delete が Undo 不可 | **解消** | `AssetDatabase.DeleteAsset` の呼び出しは `SceneGraphViewModel.cs` 内で 1 箇所（`DeleteNodeAssets` 内）のみ。Delete キー / ツールバー / ContextMenu「Remove from Graph」は `RemoveNodesFromGraph` へ通じ、アセットに触れない |
| B2 `CreateNode` が未グループ化 | **解消** | `BeginBatch($"Create Node '{identity}'")` で全体を包含 |
| B3 `RenameAsset` が Undo 非対応 | **緩和** | `SyncAssetNamesToIdentity()` を新設し `OnUndoRedo` から呼ぶ。Undo 直後に Identity とファイル名を再同期する。**Undo 可能になったのではなく、事後補正である**点に注意 |
| B4 破棄済み SO で Inspector が例外 | **解消** | `SetSelection` が偽 null を除外。`OnUndoRedo` が選択を再設定。Inspector も `liveNodes` で再フィルタ |
| — | **§8.2 で訂正** | 本レビューは偽 null の確認を `is null` / `ReferenceEquals` の有無だけで行っており、**`?.` と `??` を見ていなかった**。C' 監査がこれを検出した。§2.3(d) の本文もこの 2 演算子に言及していない。次回スライスの HANDOFF では §2.3(d) に `?.` / `??` を明記すること |
| B5 `RebuildGraph` の再入 | **解消** | `ScheduleRebuild`（R1 で修正） |
| B6 Delete 経路の二重化 | **解消** | Window の `OnKeyDown` を削除。`deleteSelection` デリゲート経由の 1 本に統一 |
| B7 Identity とファイル名の乖離 | **解消** | `TryGenerateUniqueIdentity` が両方空くまでループ。テスト `DuplicateNode_HasEmptyPayloadsUniqueIdentityAndMatchingFileName` でカバー |
| B8 `IncrementCurrentGroup` 欠落 | **解消** | `BeginBatch` 入口に追加。`PerformAutoLayout` も R3 でバッチ経由に変更（唯一の抜け穴を塞いだ） |
| B9 未所属ノードの描画 | **未修正（意図どおり）** | §5 のとおりスライス 2 |
| B10 `ConnectEdge` の membership 未保証 | **解消** | `ConnectEdge` / `ConnectEdges` の両方で `AddNode(parent)` / `AddNode(child)` |
| B11 `SaveAssets` の連打 | **解消** | コマンド内の `SaveAssets` を撤去し `EndBatch` の 1 回に集約。残る呼び出しは `LoadGraph`（レイアウト新規作成時）/ `CreateGraph` / `DeleteNodeAssets`（いずれもバッチ外の妥当な箇所） |

### 7.3 テスト結果

`pwsh tools/run-tests.ps1`（フィルタなし・全 EditMode 回帰）:

```
total 423 / passed 421 / failed 2 / skipped 0
error CS: 0 件
SceneGraph / Scripts.Editor 配下の warning CS: 0 件（CS0109 も無し）
```

**SceneGraph 新規テストは 13 件すべて Passed:**

- `SceneGraphClipboardTests` 5 件
- `SceneGraphEdgesTests` 5 件
- `SceneGraphViewModelBatchTests` 3 件

§8.2 の修正（`SceneGraphView.cs:386`）を入れた後に SceneGraph 絞り込みで再実行し、**13/13 Passed・exit 0** を確認済み。

**失敗 2 件は本作業と無関係の既存失敗:**

- `OneStarMaker.Tests.Foundation.TelemetryLogCorrelationTests.LogInsideActiveSpan_TraceIdとSpanIdを持つ`
- `OneStarMaker.Tests.UpdateSystem.UpdateSystemHostTests.TryConsumeActivationRequest_BeforeSceneDirectorBinding_ReturnsFalse`

根拠: **それぞれ単独実行しても同じく失敗する**ことを確認した（`-Filter` で個別実行）。本作業が触ったのは `OneStarMaker.Editor` アセンブリのみで、これらは `OneStarMaker.Tests`（Foundation / Runtime 依存）側にある。なお Telemetry のクラスは単独だと 2 件落ちる（全体実行では 1 件）ため、**あのスイート自体に元から順序依存がある**。本作業とは別件。

### 7.4 確認していないこと

**ここが本レビューで最も重要な節。以下はいずれも未検証であり、通ったとは言えない。**

1. **§4.2 の手動確認 11 項目は 1 つも実施していない。** Unity Editor 上での実操作が必要なため人間に委ねる。したがって「Ctrl+C / Ctrl+V / Ctrl+D / Delete が実際に効くか」は**確認できていない**。コンパイルが通り、デリゲートが代入されていることを目視したにすぎない
2. **`schedule.Execute(...).ExecuteLater(0)` を 2 つ積んだときの実行順序が UIElements スケジューラで FIFO 保証されるか未検証。** `ApplyPaste` のペースト後選択復元は「リビルドが先に走る」前提で `_nodeMap` を引く。外れた場合の影響は **「ペースト直後に選択状態にならない」だけでデータは壊れない**（`TryGetValue` が失敗して何もしない）
3. **GraphView が右クリック時に選択をどう変化させるか未検証。** ContextMenu の「Parent to '<node>'」は「選択 ≥ 2 かつ 右クリック対象がノード」で有効化するが、GraphView が右クリック時に選択を単一へ置き換える実装なら、この項目はほとんど有効にならない可能性がある
4. **`DeleteNodeAssets` の実削除経路は一度も実行していない。** 破壊的なのでテストを書いていない。確認ダイアログの文面と、他グラフからの membership 除去が実際に効くかは未検証
5. **`FrameSelection()` / `GraphView.AskUser` など Unity 側 API のシグネチャは、コンパイルが通ったことで間接的にしか確認していない**
6. **B9 は未修正のまま。** §5 のとおり意図的だが、**2 つ目のグラフを作ると全ノードが (0,0) に重なって表示される**。スライス 2 まで残る既知の症状

### 7.5 構造的負債 — 本スライスが作ったもの

**機能面は収束したが、ファイル構成は明確に劣化した。これは既存の負債ではなく本スライスが生んだものであり、そう記録する。**

| ファイル | 変更前 | 変更後 |
|---|---|---|
| `View/SceneGraphView.cs` | 368 行 | **809 行（+120%）** |
| `ViewModel/SceneGraphViewModel.cs` | 550 行 | **1008 行（+83%）** |

`SceneGraphView` は 27 メンバーが 6 責務にまたがる: ① GraphView 要素ライフサイクル ② クリップボード（約 190 行）③ ContextMenu + 確認ダイアログ（約 125 行）④ SceneAsset D&D ⑤ AutoLayout アルゴリズム ⑥ 削除経路。

**問題は行数ではなく置き場所である。** `ApplyPaste`（約 120 行）には「別グラフなら参照追加 / 同一グラフなら複製」という**ペーストの方針判断そのもの**が入っている。これはドメインロジックであり、`GraphView` のサブクラスに埋まっているために**単体テストが 1 本も書けていない**。

本スライスのテストがクリップボードの純粋 DTO 層（`SceneGraphClipboard`）しか検証できていないのは、この配置が直接の原因である。**結果として、スライス中で最もリスクの高いロジックが無検証のまま残った。**

分割案は §5 に具体化して記載した。**本スライスでは分割しない**（テストが green の状態で引き渡し、分割は独立してレビュー可能な単位にするため）。

---

## 8. C' 監査結果

> Phase C' / 2026-08-05 / cursor-agent `cursor-grok-4.5-high`（`--plan` 読み取り専用）
> 実装・設計・レビューのいずれにも関与していないモデル。**一部のみ完了**（下記 8.3）。

### 8.1 Job A — 複製の Payloads / 発火契約 → **問題なし**

- `DuplicateNode` は新規 `SceneNodeData` を作り `NodeLoadType` のみ継承。`Payloads` への代入・コピーは無し（`SceneGraphViewModel.cs:758-778`）。呼び出しは `SceneGraphView.cs:485` のみで、同経路に別の Payloads 引き継ぎ無し
- `ConnectEdge` / `DisconnectEdge` は `RaiseGraphChanged` を呼ばない（`SceneGraphViewModel.cs:569-601, 666-678`）。`ConnectEdges` / `DisconnectEdges` は発火あり（`646, 703`）で仕様どおり

### 8.2 Job B — 偽 null / BeginBatch → **1 件指摘あり**

**指摘: `?.` と `??` が `UnityEngine.Object` に掛かっている箇所がある。**

`?.` / `??` は Unity の `==` オーバーロードを迂回するため、**破棄済みオブジェクトに対して短絡せず呼び出してしまう**。§2.3(d) が禁じた `is null` / `ReferenceEquals` と同じ穴だが、§2.3(d) の本文はこの 2 演算子に言及しておらず、Phase C のレビューでも見落とした。

| 箇所 | 素性 | 対応 |
|---|---|---|
| `View/SceneGraphView.cs:386`（`BuildClipboardJson`） | **本スライスが新規に追加** | **修正済み。** `layout != null ? layout.GetPosition(node) : Vector2.zero` に変更 |
| `View/SceneGraphView.cs:169`（`AddNodeElement`） | 既存（HEAD の 143 行目） | 未修正。スライス 2 送り |
| `View/SceneGraphEditorWindow.cs:167`（`target ??= graphs[0]`） | 既存 | 未修正。スライス 2 送り |
| `SceneResourceGenerator.cs:266` | 既存かつ §5 の対象外 | 触らない |

`is null` / `ReferenceEquals` の使用は無し（`SceneGraphViewModel.cs:67` はコメントのみ）— §7 の主張を追認。

**BeginBatch は問題なし**: `IncrementCurrentGroup` が `SetCurrentGroupName` より先（`SceneGraphViewModel.cs:100→101`）。全呼び出しが `using` で、`BatchScope.Dispose`（`146-150`）→ `EndBatch`。例外で深度が残る経路なし。

### 8.3 Job C — §5 禁止事項 / B1〜B11 判定 → **未実施（人間の判断で打ち切り）**

1 回目は結論に到達せず終了。**再実行しないことを人間が決定した**（2026-08-05）。したがって **C' 監査としてはこの範囲を検証していない。**

ただし同じ範囲は Phase C（§7.2）で差分と 1 件ずつ突き合わせ済みであり、§5 の禁止事項も機械的に確認している（`SceneResourceGenerator.cs` はセッション開始時から同一の未コミット変更のまま・`.asmdef` 無変更・B9 未修正）。**独立した第三者の目が入っていない、という一点が残る。**

### 8.4 監査プロセス自体の記録

当初、全論点を 1 ジョブに詰めて実行したが、**探索でターン予算を使い切り結論を出さずに正常終了した**（thinking 497 件・ツール呼び出し 92 回に対し可視出力は進捗 5 行のみ、exit 0 / `is_error: false`）。論点を 2 つに絞り字数上限を課したところ Job A / B は機能した。

**この試行錯誤で従量課金を無駄に消費したため、Job C の再実行は行わず未完了のまま記録する。** 再実行の判断は人間に委ねる。手順上の教訓は `CLAUDE.md` の「非対話実行の落とし穴」に反映済み。

---

## 正本ポインタ（人間用・実装時に読む必要はない）

- `unity/Assets/Docs/Architecture/11-scene-graph-editor.md` — Scene Graph Editor の設計正本（IK-1〜IK-8 / E-1〜E-8 / W-1〜W-5）
- `docs/planning/SCENEGRAPH_AS_SCOPE_TREE_2026-07-19.md` — SceneGraph を「スコープ木」として扱う判断
- `unity/Assets/Docs/Architecture/05-scene.md` — Scene ライフサイクル（14 状態）
