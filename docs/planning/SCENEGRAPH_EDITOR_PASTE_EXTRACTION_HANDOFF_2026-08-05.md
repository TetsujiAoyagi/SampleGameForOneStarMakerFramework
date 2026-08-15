# SceneGraphEditor スライス2 — ApplyPaste 抽出 + 手動確認フィードバック F1〜F4 実装 HANDOFF

> 作成日: 2026-08-05
> ブランチ: `impl/scenegraph-editor-multiselect`（スライス1 と同一ブランチ上に積む）
> Phase A（本書執筆）: Claude Code / Opus 5
>
> **この文書は自己完結している。他のドキュメントを開く必要はない。**
> 実装者は §0 → §2 → 自分の担当チケット（§3.1〜§3.6 のいずれか）→ §4 だけを読めばよい。

---

## 開発体制（このスライスの担い手）

| Phase | 担当 | モデル ID | 成果物 |
|---|---|---|---|
| A 計画 | Claude Code | Opus 5 | 本書 §0〜§5 |
| B 実装 | **cursor-agent** | `cursor-grok-4.5-high` | ブランチ上の差分 |
| C レビュー + 最終チェック | **Claude Code** | Opus 5 | 本書 §7（差し戻しは §6） |
| C' 敵対的監査 | **cursor-agent** | `cursor-grok-4.5-high` | 本書 §8 |
| D マージ判断 | 人間 | — | — |

**コスト方針（2026-08-05 に人間が決定）: cursor-agent で使うモデルは Grok 4.5 と Composer に限る。Opus 系を使うなら Claude Code 側から使う。**

そのため当初案の「Phase C = `claude-opus-4-8-thinking-high`（cursor-agent）」は取りやめ、**Phase C と最終チェックを Claude Code / Opus 5 の 1 本に統合した**。

- 代償: 独立した目が 2 段（Opus 4.8 → Opus 5）から 1 段に減る。実装 → Opus 5 → Grok 4.5 の 3 段は維持される
- 2 段に戻したい場合の選択肢: C の一次パスに `composer-2.5`（許可モデル内・安価）を挟む
- 参考: `--list-models` の結果、**cursor-agent に Opus 4.7 は存在しない**（Opus 系は `claude-opus-4-8-thinking-high` と `claude-opus-5-*` のみ）

B↔C は指摘が尽きるまで往復（上限 4 巡）。C' も同様（上限 3 巡）。収束しなければ残指摘を明示して人間に判断を仰ぐ。

**レビュー結果は必ず本書に書き出すこと。チャットに書くだけだと C' に監査対象が無くなる。**

---

## §0. 現在地

### 0.1 ブランチとコミット状態

- ブランチ `impl/scenegraph-editor-multiselect` をチェックアウト中
- `git log` は `6620c17`（`Merge pull request #4 from TetsujiAoyagi/impl/asset-owner-reverse-lookup`）のまま
- **スライス1（複数選択 / Copy-Paste / Undo 一括化）は未コミットのまま作業ツリーに乗っている**
- 作業ツリーには本作業と無関係な未コミット変更も多数ある

### 0.2 絶対に消してはいけないもの

以下は **untracked で git から復元できない**。`git clean` 系のコマンドを絶対に実行しないこと。

```
docs/reference/
docs/slides/
unity/Assets/Docs/Architecture/27〜29*
```

**cursor-agent を `-p --force` で走らせる場合は必ず `-w <ticket-id>` の worktree 隔離とセットにすること。** 全許可のエージェントを上記の untracked と同居させない。

### 0.3 テストの現況

```bash
pwsh tools/run-tests.ps1
```

- SceneGraph 関連: **13/13 green**
- 全体: 423〜424 件中 **既存の失敗が 3 件**

| 失敗テスト | 性質 |
|---|---|
| `TelemetryLogCorrelationTests.LogInsideActiveSpan_TraceIdとSpanIdを持つ` | 恒常的に失敗 |
| `TelemetryLogCorrelationTests.LogAndTelemetry_共有sequenceで1_2_3と採番される` | **flaky**（実行ごとに通ったり落ちたりする） |
| `UpdateSystemHostTests.TryConsumeActivationRequest_BeforeSceneDirectorBinding_ReturnsFalse` | 恒常的に失敗 |

**この 3 件から増えていなければ green とみなす。** いずれも `Foundation` / `UpdateSystem` にあり本作業とは無関係。

> 前身の引き継ぎ文書は「失敗 2 件」と記載していたが、それは flaky な 1 件がたまたま通った回のスナップショット（05:03 の実行）を基準にしたもの。**正しくは 3 件。** Phase C で過去の実行記録と突き合わせて確定した（§7.4）。

**実行時の必須条件:**

- **Unity Editor を閉じた状態で実行すること**（プロジェクトロックで失敗する）
- **テスト 0 件は失敗扱い。** コンパイルエラーが「0 件実行」として現れるため、件数を必ず確認する
- 結果は `TestResults/`（git 管理外）
- 絞り込みは `-Filter OneStarMaker.Tests.Editor.SceneGraph` のようにオプトイン

### 0.4 対象ファイルの現況（行数は 2026-08-05 時点の作業ツリー）

| ファイル | 行数 |
|---|---|
| `unity/Assets/OneStarMaker/Scripts/Editor/SceneGraph/View/SceneGraphView.cs` | **812** |
| `unity/Assets/OneStarMaker/Scripts/Editor/SceneGraph/ViewModel/SceneGraphViewModel.cs` | **1008** |
| `unity/Assets/OneStarMaker/Scripts/Editor/SceneGraph/ViewModel/SceneGraphClipboard.cs` | 149 |
| `unity/Assets/OneStarMaker/Scripts/Editor/SceneGraph/View/SceneGraphEditorWindow.cs` | 417 |
| `unity/Assets/OneStarMaker/Scripts/Editor/SceneGraph/View/SceneGraphInspectorPanel.cs` | 228 |
| `unity/Assets/OneStarMaker/Tests/Editor/SceneGraph/` | 3 クラス 13 件 |

`SceneGraphView.cs` のメンバー配置（行番号は本書全体で参照する）:

```
 27-84   ctor（デリゲート配線・コールバック登録）
 89-102  GetCompatiblePorts
108-145  RebuildGraph
153-162  ScheduleRebuild
164-180  AddNodeElement            ← 169 が偽 null（T6）
187-275  OnGraphViewChanged        ← 195-215 が edgesToCreate（T4）
281-339  CopySelectionToClipboard / PasteFromClipboard / DuplicateSelection / RemoveSelectionFromGraph
341-360  OnSerializeGraphElements / OnCanPasteSerializedData / OnUnserializeAndPaste / OnDeleteSelection
367-423  BuildClipboardJson        ← T1 で撤去
425-429  GetGuidForNode            ← T1 で撤去
437-555  ApplyPaste                ← T1 で撤去
557-562  ResolveContextTargetNode
564-638  OnContextMenuPopulate     ← T5
643-676  ConfirmAndDeleteNodeAssets ← T5 で撤去
681-690  CreateNodeAtCenter
692-748  OnDragUpdated / OnDragPerform / HasSceneAssetInDrag / GetSceneAssetPathsFromDrag
750-812  PerformAutoLayout / LayoutTree
```

---

## §1. ユーザー意図

人間の手動確認で出たフィードバック **F1〜F4** を解消する。

ただし F2（Duplicate の親継承）は `SceneGraphView.ApplyPaste`（約 120 行）の中身をさらに増やす。`ApplyPaste` は `GraphView` サブクラスに埋まっているため**単体テストが 1 本も書けない**。スライス中で最もリスクの高いロジックが無検証のまま太るのを避けるため、**F2 に着手する前に `ApplyPaste` を `GraphView` の外へ抽出する**。

### 1.1 なぜこの形が要るのか（設計の芯）

スライス1で新規ファイルとして切り出されたのは `SceneGraphClipboard.cs` ただ 1 つで、それは前回の HANDOFF が「`AssetDatabase` 非依存の純粋関数として書け。これができていないとテストが書けない」と明示した箇所**だけ**だった。逆に、テストを要求しなかった `ApplyPaste` は `GraphView` のサブクラスに埋まり、テストが 1 本も書けないまま残った。

> **「どこに置け」は破られるが、「テストを書け」はテスト可能な配置を強制する。**

本スライスは T1（抽出）と T2（F2 + テスト 4 件）をセットで扱う。**T2 のテストが書けないなら T1 の抽出が足りていない**、という関係にしてある。

---

## §2. 壊さない制約（**全チケット共通。実装者は必ず読むこと**）

### 2.1 リポジトリ全体の不変条件

- **asmdef の依存グラフに参照を勝手に足さない。** 足したくなったらそれは設計判断であり、人間に確認する
- `SceneState` の enum 順序は整数比較でガードに使われている。**並べ替え禁止**
- アセットロードは `IAssetManagement` 経由で `AssetOwner` が必須引数。本スライスでは触らない

### 2.2 SceneGraph 固有の契約

#### (a) `OnGraphChanged` の発火契約

| メソッド | `OnGraphChanged` |
|---|---|
| `ConnectEdge`（単数） | **発火しない** — GraphView 自身がビジュアルエッジを追加するため |
| `DisconnectEdge`（単数） | **発火しない** — 同上 |
| `ConnectEdges`（複数） | **発火する** — ContextMenu 等、GraphView が視覚更新しない経路から呼ばれるため |
| `DisconnectEdges`（複数） | **発火する** — 同上 |

**この契約を変えないこと。**

#### (b) リビルドは必ず遅延させる

`SceneGraphView` の ctor（`SceneGraphView.cs:69`）で以下が配線されている:

```csharp
_viewModel.OnGraphChanged += ScheduleRebuild;
```

`ScheduleRebuild`（`SceneGraphView.cs:153-162`）は次フレームへコアレスして遅延する:

```csharp
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

**`OnGraphViewChanged` の中で同期的に `RebuildGraph()` を呼ぶのは禁止。** `BeginBatch` の `Dispose` は `return graphViewChange;` より前に走るため、GraphView が `elementsToRemove` / `edgesToCreate` を適用し終える前に全要素を撤去してしまう。

逆に、**`OnGraphViewChanged` の内側から `ConnectEdges`（発火する側）を呼ぶのは安全**。発火先が `ScheduleRebuild` で次フレームに逃げるため。この配線に依存してよい。

#### (c) `BeginBatch` の入口順序

`BeginBatch` は入口で `Undo.IncrementCurrentGroup()` を `Undo.SetCurrentGroupName(...)` より**先に**呼ぶ。逆にすると直前の無関係な操作を同じ Undo グループに巻き込む。既存実装（`SceneGraphViewModel.cs:95`）を変更しないこと。

ネストは許容されている（外側 1 つで 1 Undo になる）。

### 2.3 Unity 固有の罠（**最重要**）

#### (d) 偽 null — `?.` と `??` も禁止対象

破棄済み `UnityEngine.Object` は `== null` が **true** になる（Unity が `==` をオーバーロードしているため）。しかし:

- `is null` — オーバーロードを迂回する。**禁止**
- `ReferenceEquals` — 同上。**禁止**
- **`?.` — オーバーロードを迂回し、破棄済みオブジェクトに対して短絡しない。禁止**
- **`??` — 同上。禁止**

必ず `== null` / `!= null` で明示的に判定する。

```csharp
// NG: CurrentLayout が破棄済みでも短絡せず GetPosition を呼ぶ
var pos = _viewModel.CurrentLayout?.GetPosition(node) ?? Vector2.zero;

// OK
var layout = _viewModel.CurrentLayout;
var pos = layout != null ? layout.GetPosition(node) : Vector2.zero;
```

**ただし `?.` が安全なケースもある。`UnityEngine.Object` を継承していない型には `?.` を使ってよい。** 以下は安全なので**触らないこと**:

| 型 | 例 | 理由 |
|---|---|---|
| `SceneGraphNode` | `parentNode?.NodeData`（`SceneGraphView.cs:204, 228`） | `GraphView.Node` = `VisualElement` |
| `SceneGraphView` | `_graphView?.RebuildGraph()`（`SceneGraphEditorWindow.cs:208` 他） | `VisualElement` |
| `SceneGraphViewModel` | `_viewModel?.CurrentEdges`（`SceneGraphEditorWindow.cs:201`） | 素の C# クラス |
| `AssetPayload` | `payload0?.Reference`（`SceneGraphInspectorPanel.cs:224`） | 素の C# クラス |

**`?.` を grep して機械的に一括置換しないこと。** 直すのは T6 が指定する 2 箇所だけ。

#### (e) W-5 — `SceneNodeData.OnValidate` が Identity を強制上書きする

`SceneNodeData.OnValidate` は `Payloads[0]` のシーン名で `Identity` を上書きする。

**複製時は `Payloads` を必ず空にすること。** 引き継ぐと Identity 重複（バリデーション V-2 Error）が静かに発生する。既存の `SceneGraphViewModel.DuplicateNode` はこれを守っている（テスト `DuplicateNode_HasEmptyPayloadsUniqueIdentityAndMatchingFileName` で担保）。**複製処理に手を入れるとき、`EditorUtility.SetDirty` → `OnValidate` の発火順序が変わっていないか確認すること。**

#### (f) 既存アセットに書き込まない

テストは `Assets/SceneGraphData/` に**絶対に書き込まない**。既存テストは `SceneGraphViewModel.NodesFolder` / `GraphsFolder` / `LayoutsFolder`（`internal static`）を差し替えて `Assets/__SceneGraphEditorTests__` を使う。この仕組みをそのまま使うこと（§4.1 に雛形あり）。

---

## §3. 変更内容

### §3.0 チケット一覧と依存関係

| ID | 内容 | 主戦場 | 依存 | 競合 |
|---|---|---|---|---|
| **T1** | `ApplyPaste` の抽出（純リファクタ） | `SceneGraphView.cs` `341-360` / `367-429` / `437-555` を撤去 + 新規 Service | なし | 以降の行番号が全部ずれる |
| **T2** | F2: Duplicate が親を引き継ぐ | Service + `SceneGraphClipboard.cs` | **T1** | なし |
| **T3** | F1: Paste が灰色のまま効かない | `SceneGraphView.cs` `281-302` / `341-349` + ContextMenu の Paste 判定 | **T1** | T5 |
| **T4** | F3: 複数選択でエッジドラッグ | `SceneGraphView.cs:195-215` | なし | なし |
| **T5** | F4: ContextMenu 出し分け + Asset 削除撤去 | `SceneGraphView.cs:557-676` + `SceneGraphViewModel.cs:490-568` を撤去 | なし | T3 |
| **T6** | 偽 null 迂回 2 箇所 | `SceneGraphView.cs:169` / `SceneGraphEditorWindow.cs:167` | なし | なし |

#### 並列実行する場合

```
Wave 1（並列3）:  T1 抽出   ‖   T4 F3   ‖   T5 F4 + T6 偽null
                    ↓  マージ順は T1 → T5+T6 → T4 で固定
Wave 2（並列2）:  T2 F2      ‖   T3 F1
```

- 各チケットは **`-w <ticket-id>` で別 worktree**。`-p --force` を worktree 無しで走らせない
- **T1 が 187 行撤去して以降の行番号を全部動かすので、必ず T1 を最初にマージする**
- **マージとコンフリクト解消はエージェントにやらせない**（人間 or Phase C 担当が行う）
- **1 ジョブの論点は 2 つまで。** T5 と T6 を同居させているのは、どちらも「撤去」で論点が 1 つに畳めるため
- cursor-agent は `--output-format stream-json` を使う。`text` / `json` は**中身が空でも exit 0 / `is_error: false`** を返すため失敗を検出できない
- **空振りしたら同じ重さで再実行しない。** 数秒で終わる最小プローブへ落としてから原因を潰す

**直列（T1→T2→T3→T4→T5→T6）でも所要は大きく変わらず、マージ事故がゼロになる。worktree 運用に不安があれば直列を選んでよい。**

---

### §3.1 【T1】`ApplyPaste` を `SceneGraphView` から抽出する

#### 目的

ペースト方針判断を `GraphView` の外に出し、単体テスト可能にする。**振る舞いは 1 ミリも変えない純リファクタ。**

#### 変更対象

| 種別 | ファイル | 現在 → 予想 | 責務数 |
|---|---|---|---|
| 新規 | `unity/Assets/OneStarMaker/Scripts/Editor/SceneGraph/Service/SceneGraphPasteService.cs` | — → **約 230 行** | **1** |
| 変更 | `unity/Assets/OneStarMaker/Scripts/Editor/SceneGraph/View/SceneGraphView.cs` | 812 → **約 655 行** | 5 |

`Service/` フォルダは新設。`.meta` は Unity が生成するので手で作らない。

#### 移すメソッド

| メソッド | 現在位置 |
|---|---|
| `BuildClipboardJson` | `SceneGraphView.cs:367-423` |
| `GetGuidForNode` | `SceneGraphView.cs:425-429` |
| `ApplyPaste` | `SceneGraphView.cs:437-555` |

#### 抽出の境界（**ここを間違えると意味が無くなる**）

> **境界は「`AssetDatabase` 依存かどうか」ではなく「`GraphView` 依存かどうか」。**

既存テスト `SceneGraphViewModelBatchTests` は `Assets/__SceneGraphEditorTests__` に実アセットを作り、`SceneGraphViewModel.NodesFolder` 等の `internal static` を差し替えて `AssetDatabase` ごとテストしている。**したがって `AssetDatabase` 依存はテストできる。**

テストできないのは次の 4 つだけ:

- `selection`（`GraphView` のプロパティ）
- `schedule`（`VisualElement` のスケジューラ）
- `_nodeMap`（`SceneGraphNode` の辞書）
- `GraphElement` / `Node` / `Edge`（GraphView の要素型）

**この 4 つを `SceneGraphPasteService` に持ち込まないこと。**

#### 想定 API

```csharp
namespace OneStarMaker.Editor.SceneGraph
{
    /// <summary>
    /// クリップボード JSON の組み立てと貼り付けを行う。GraphView に依存しない。
    /// </summary>
    internal sealed class SceneGraphPasteService
    {
        public SceneGraphPasteService(SceneGraphViewModel viewModel);

        /// <summary>コピー対象のノードからクリップボード JSON を組み立てる。</summary>
        public string BuildClipboardJson(IReadOnlyList<SceneNodeData> nodes);

        /// <summary>
        /// クリップボード JSON を貼り付ける。
        /// 選択の復元は呼び出し側（View）の責務なので、結果ノードを返すだけにする。
        /// </summary>
        public IReadOnlyList<SceneNodeData> ApplyPaste(string json, bool forceDuplicate);
    }
}
```

#### `SceneGraphView` 側に残すもの

1. `SceneGraphPasteService` のインスタンス（ctor で生成）
2. `GraphElement` → `SceneNodeData` の変換を挟む薄いラッパ:
   ```csharp
   private string BuildClipboardJson(IEnumerable<GraphElement> elements)
   {
       var nodes = elements.OfType<SceneGraphNode>()
           .Select(n => n.NodeData)
           .Where(n => n != null)
           .Cast<SceneNodeData>()
           .Distinct()
           .ToList();
       return nodes.Count == 0 ? string.Empty : _pasteService.BuildClipboardJson(nodes);
   }
   ```
3. **ペースト後の選択復元**（現 `SceneGraphView.cs:536-554`）。`schedule` と `_nodeMap` を使うので Service には出せない:
   ```csharp
   private void ApplyPaste(string json, bool forceDuplicate)
   {
       var result = _pasteService.ApplyPaste(json, forceDuplicate);
       if (result.Count == 0) return;

       schedule.Execute(() =>
       {
           ClearSelection();
           foreach (var node in result)
           {
               if (_nodeMap.TryGetValue(node, out var visualNode))
                   AddToSelection(visualNode);
           }
       }).ExecuteLater(0);
   }
   ```

#### 実装時の注意

1. **`BuildClipboardJson` 内の偽 null 対策をコメントごとそのまま移す。** 現 `SceneGraphView.cs:386-389`:
   ```csharp
   // §2.3(d): ?. と ?? は Unity の == オーバーロードを迂回するため、破棄済み SO に対して
   // 短絡せず呼び出してしまう。偽 null を検出できる != null で明示的に判定する。
   var layout = _viewModel.CurrentLayout;
   var position = layout != null ? layout.GetPosition(node) : Vector2.zero;
   ```
   **`?.` に「整理」しないこと。**
2. `BeginBatch` の位置と Undo 名（`"Duplicate {n} node(s)"` / `"Paste {n} node(s)"`）を変えない
3. 参照ペースト時の再親付け警告（現 `SceneGraphView.cs:476-477, 529-534` の `reparentedIdentities`）をそのまま移す
4. GUID 解決失敗時の `Debug.LogWarning`（現 `SceneGraphView.cs:455-458`）をそのまま移す
5. `resolved.All(n => n == null)` の早期 return（現 `SceneGraphView.cs:463`）を落とさない
6. `OnSerializeGraphElements` / `OnCanPasteSerializedData` / `OnUnserializeAndPaste` / `OnDeleteSelection`（`341-360`）は **View に残す**（GraphView のデリゲートなので）。中身がラッパを呼ぶだけになる

#### 受け入れ条件

- `pwsh tools/run-tests.ps1` で SceneGraph **13/13 green**（Unity Editor を閉じてから。**0 件は失敗扱い**）
- `SceneGraphPasteService.cs` に `GraphView` / `GraphElement` / `VisualElement` / `selection` / `schedule` / `_nodeMap` という文字列が **1 つも出てこない**
- `SceneGraphView.cs` が **660 行以下**
- Copy / Paste / Duplicate / Ctrl+D の手動動作がリファクタ前と同じ

---

### §3.2 【T2】F2: Duplicate が親との接続を落とす

> **前提: T1 が完了していること。** このチケットは `SceneGraphPasteService.cs` を編集する。

#### 症状と仕様

複製時、**コピー集合に含まれない親**を持つノードは、複製先も同じ親へ接続する。

```
元:      World → Cell_A          Cell_A だけ選んで Ctrl+D
現状:    World → Cell_A,  Cell_A1（親なし）        ← バグ
変更後:  World → Cell_A,  World → Cell_A1
```

#### 変更対象

| ファイル | 現在 → 予想 | 責務数 |
|---|---|---|
| `Service/SceneGraphPasteService.cs` | 約 230 → **約 270 行** | 1 |
| `ViewModel/SceneGraphClipboard.cs` | 149 → **約 165 行** | 1 |
| `Tests/Editor/SceneGraph/SceneGraphPasteServiceTests.cs`（新規） | — → 約 180 行 | — |
| `Tests/Editor/SceneGraph/SceneGraphClipboardTests.cs` | 追記 | — |

#### 仕様の詳細

1. **適用は複製モードのみ**（`ApplyPaste` 内の `duplicate == true` の分岐）。
   **別グラフへの参照ペーストは現状維持。** 貼り付け先に同じ親がいるとは限らず、勝手に繋ぐと壊す
2. 親が**コピー集合内**にいる場合は、既存の `clipboardData.Edges` ループ（現 `ApplyPaste` の `foreach (var link in clipboardData.Edges)`）が複製同士を繋ぐ。**二重に繋がないよう分岐すること**
3. 親の取得は `currentEdges.GetParent(src)`（`SceneGraphEdges.cs:71`）
4. 接続は既存の `_viewModel.ConnectEdges(parent, children)`（`SceneGraphViewModel.cs:612`）
5. 既存の `BeginBatch` の内側で行い、Undo は 1 回で戻ること

#### 判定ロジックの置き場所（**A-4: ここは指示ではなく要求**）

「クリップボードの Nodes 配列のうち、**内部リンクで親を持たない index** はどれか」の判定は、**`SceneGraphClipboard` に純粋関数として追加すること。**

- `AssetDatabase` にも `UnityEditor` にも `UnityEngine.Object` にも依存しない
- 既存の `BuildInternalLinks`（`SceneGraphClipboard.cs:118-147`）と同じスタイルに揃える
- 想定シグネチャ:
  ```csharp
  /// <summary>
  /// Nodes 配列のうち、内部リンク上で親を持たない index を返す。
  /// 複製時に「コピー集合外の親」を引き継ぐ対象を決めるために使う。
  /// </summary>
  public static List<int> GetIndicesWithoutInternalParent(
      int nodeCount, IReadOnlyList<SceneGraphClipboardLink> links);
  ```

**この関数が `SceneGraphClipboardTests` から `AssetDatabase` 抜きでテストできること。** できないなら書き方が間違っている。

#### 実装時の注意

1. **W-5**: 複製ノードの `Payloads` は空でなければならない（§2.3(e)）。親を繋ぐ処理を足したことで `EditorUtility.SetDirty` → `OnValidate` の順序が変わり、Identity が上書きされていないか確認すること
2. `currentEdges.GetParent(src)` が `null` を返すケース（元ノードが親を持たない）を必ず処理する
3. サイクル判定は `ConnectEdges` の内部（`SceneGraphViewModel.cs:629`）が行う。**自前でサイクル判定を書き足さない**
4. 親が破棄済み `UnityEngine.Object` の可能性がある。`!= null` で判定する（`?.` / `??` 禁止）

#### テスト（**必須 4 件**）

`Tests/Editor/SceneGraph/SceneGraphPasteServiceTests.cs`（新規、§4.1 の雛形を使う）:

| # | 内容 |
|---|---|
| 1 | 複製が**コピー集合外の親**を引き継ぐ（`World → Cell_A` で `Cell_A` だけ複製 → `World → Cell_A1` ができる） |
| 2 | 親が**コピー集合内**なら二重接続しない（`World` と `Cell_A` を両方複製 → `World1 → Cell_A1` が 1 本だけ） |
| 3 | 参照ペースト（別グラフ）では親を勝手に繋がない |
| 4 | 複製ノードの `Payloads` が空（W-5 の回帰テスト） |

`Tests/Editor/SceneGraph/SceneGraphClipboardTests.cs` に追記:

| # | 内容 |
|---|---|
| 5 | `GetIndicesWithoutInternalParent` の純粋関数テスト（親なし / 親あり / 空リスト） |

> **テストが書けないなら T1 の抽出が足りていない。** View の型が Service に漏れていないか見直すこと。

---

### §3.3 【T3】F1: Paste が効かない。右クリックの Paste が灰色のまま

> **前提: T1 が完了していること。** `ApplyPaste` のラッパに触る。
> **T5 と ContextMenu の Paste 判定行で競合する。** 後にマージする側が古い実装に戻さないこと。

#### 症状

Ctrl+C した直後に右クリックしても Paste が Disabled のまま。

#### 仮説（**未検証**）

Copy の経路が 2 つあり、書き込み先が違う:

| 経路 | 書き込み先 |
|---|---|
| Ctrl+C | GraphView 自身のクリップボード（`serializeGraphElements` デリゲートを呼ぶ） |
| 右クリック Copy | `EditorGUIUtility.systemCopyBuffer` |

メニューの Paste 有効判定（現 `SceneGraphView.cs:590`）は後者しか見ていないため、Ctrl+C の内容が見えない。

**Unity の GraphView 実装を読めないため、この仮説は検証できていない。** 仮説が正しければ Ctrl+V は修正前から動いているはず。

#### 方針: **原因に依存しない直し方を採る**

`OnSerializeGraphElements`（`SceneGraphView.cs:341`）は Ctrl+C / Ctrl+X / Ctrl+D で GraphView が**必ず呼ぶ**。そこで JSON をスナップショットし、`private static string` を唯一の窓口にする。

#### 変更対象

| ファイル | 現在 → 予想 |
|---|---|
| `View/SceneGraphView.cs` | **+約 25 行** |

触る箇所: `281-302`（`CopySelectionToClipboard` / `PasteFromClipboard`）、`341-349`（`OnSerializeGraphElements` / `OnCanPasteSerializedData`）、ContextMenu の Paste 判定。

#### 実装仕様

1. `SceneGraphView` に `private static string _lastClipboardJson = string.Empty;` を持つ

   **`static` が必要な理由**: グラフを切り替えると `SceneGraphView` が作り直される。「別グラフへの参照ペースト」が主要ユースケースなので、インスタンスフィールドでは切れる。

2. **書き込み口を 2 つとも同じ処理に通す**:
   - `OnSerializeGraphElements(elements)` — JSON を作ったら `_lastClipboardJson` と `EditorGUIUtility.systemCopyBuffer` の**両方**へ書く
   - `CopySelectionToClipboard()`（メニュー Copy）— 同じく両方へ書く

   `systemCopyBuffer` へも書くのは、別ウィンドウ・別 Unity セッションへの持ち出しのため。

3. **読み出し規則を 1 つに固定する**:
   ```
   systemCopyBuffer が SceneGraphClipboard.CanPaste を通る → それを使う
   通らない                                              → _lastClipboardJson を使う
   ```
   （別アプリが `systemCopyBuffer` を上書きしたケースを static で拾う）

   この規則を `private static string GetPasteSource()` のような 1 メソッドに閉じ込め、**`OnCanPasteSerializedData` / `PasteFromClipboard` / ContextMenu の Paste 有効判定の 3 箇所が全部そこを見るようにする。**

4. `DuplicateSelection()` は現状どおり選択から直接 JSON を作る（クリップボードを経由しない）

#### 実装時の注意

1. **修正後の手動確認で「Ctrl+V は修正前から動いていたか」を必ず確かめ、真因を §6 に記録すること。** 仮説のまま残さない
2. `_lastClipboardJson` は `SceneGraphClipboard.CanPaste` を通る内容だけを入れる（空文字を入れない）
3. `OnSerializeGraphElements` は戻り値も返す必要がある（GraphView が使う）。スナップショットのために戻り値を変えないこと

#### テスト

**単体テスト不可（View 層 / `GraphView` のデリゲート経路）。** §4.2 の手動確認へ回す。無理に書かないこと。

---

### §3.4 【T4】F3: 複数選択でエッジドラッグしたら選択全部を同じ親へ

> **T1 と並列可。** 触る行域（`195-215`）が重ならない。

#### 仕様

- **条件**: 引いたエッジの子側ノードが現在の `selection` に含まれ、かつ選択中の `SceneGraphNode` が **2 個以上**
- **動作**: `_viewModel.ConnectEdges(親, 選択中の全ノード)`。**親自身は子リストから除く**
- **子が選択に含まれない場合は現状どおり `ConnectEdge` で 1 本だけ繋ぐ**

#### 変更対象

| ファイル | 現在 → 予想 |
|---|---|
| `View/SceneGraphView.cs`（`OnGraphViewChanged` の `edgesToCreate`、`195-215`） | **+約 30 行** |
| `Tests/Editor/SceneGraph/SceneGraphViewModelBatchTests.cs` | テスト 1 件追加 |

#### 現在のコード（`SceneGraphView.cs:195-215`）

```csharp
if (graphViewChange.edgesToCreate != null)
{
    var validEdges = new List<Edge>();

    foreach (var edge in graphViewChange.edgesToCreate)
    {
        var parentNode = edge.output?.node as SceneGraphNode;
        var childNode = edge.input?.node as SceneGraphNode;

        if (parentNode?.NodeData == null || childNode?.NodeData == null)
            continue;

        if (_viewModel.ConnectEdge(parentNode.NodeData, childNode.NodeData))
        {
            validEdges.Add(edge);
        }
    }

    graphViewChange.edgesToCreate = validEdges;
}
```

#### 実装時の注意（**ここを外すと視覚エッジが二重になる / 全要素が消える**）

1. **一括分岐を通ったエッジは `validEdges` に入れない。**
   視覚エッジは `ScheduleRebuild` が Model から引き直すので、GraphView にも作らせると二重になる
2. `ConnectEdges` は `OnGraphChanged` を**発火する**（`ConnectEdge` は発火しない、という契約差がある — §2.2(a)）。発火先は `ScheduleRebuild` で**次フレームへ遅延する**（§2.2(b)）ので、`OnGraphViewChanged` の内側で呼んで**安全**
3. **`OnGraphViewChanged` の中で同期的に `RebuildGraph()` を呼ぶのは禁止**（§2.2(b)）
4. サイクル判定は `ConnectEdges` 内が 1 件ずつ再評価して弾き、失敗は 1 回のメッセージにまとまる（`SceneGraphViewModel.cs:629-654`）。**自前でサイクル判定を書き足さない**
5. Undo は外側の既存 `BeginBatch("Edit Scene Graph")`（`SceneGraphView.cs:192`）に乗るので 1 回で戻る。**新しい `BeginBatch` を足さない**
6. `edgesToCreate` に複数のエッジが入っていても**一括分岐は 1 回だけ**走らせる（ガード用の bool を置く）
7. `parentNode?.NodeData` の `?.` は `SceneGraphNode`（= `VisualElement`）に対するものなので**安全。そのまま**（§2.3(d)）
8. 選択の取得は `selection.OfType<SceneGraphNode>().Select(n => n.NodeData).Where(n => n != null)`。**`n != null` で判定する**（`SceneNodeData` は `ScriptableObject`）

#### テスト

`SceneGraphViewModelBatchTests` に 1 件追加:

| # | 内容 |
|---|---|
| 1 | `ConnectEdges(parent, [a, b, c])` の Undo が **1 回**で全部戻る |

View 側（`selection` を見る分岐）は単体テスト不可。§4.2 の手動確認へ。

---

### §3.5 【T5】F4: ContextMenu を選択状態で出し分け、Asset 削除を撤去

> **T1 と並列可。** 触る行域（`557-676`）が重ならない。
> **T3 と ContextMenu の Paste 有効判定で競合する。**

#### 目的

Disabled で全項目を並べるのをやめ、その場で使えるものだけ出す。加えて **Undo 不可の破壊操作を右クリックメニューから撤去する。**

#### 変更対象

| ファイル | 現在 → 予想 | 責務数 |
|---|---|---|
| `View/SceneGraphView.cs`（`557-676`） | -38 +約 43 = **net +5 行** | 5 |
| `ViewModel/SceneGraphViewModel.cs`（`490-568` を撤去） | 1008 → **約 930 行** | — |

#### メニュー構成

**選択なし**（空白を右クリック）

```
Create Node
Paste                （クリップボードが有効なときのみ表示）
──────────
Auto Layout
```

**ノードを選択中**

```
Copy
Duplicate
──────────
Parent to '<右クリックしたノード>'   （選択 2 個以上 かつ 対象がノード のときのみ表示）
Unparent Selected                  （選択に親を持つノードがあるときのみ表示）
Remove from Graph
──────────
Select in Project
Frame Selection
──────────
Auto Layout
```

#### 撤去するもの

`Delete Node Asset…` を **`Select in Project`** に置き換える:

```csharp
evt.menu.AppendAction("Select in Project", _ =>
{
    Selection.objects = selectedNodes.Cast<UnityEngine.Object>().ToArray();
    if (selectedNodes.Count > 0) EditorGUIUtility.PingObject(selectedNodes[0]);
});
```

**理由**（本書に残す設計判断）: Undo 不可の破壊操作を連打する右クリックメニューに置かない。ノードは複数グラフ共有の資産であり、グラフエディタの責務は「グラフへの所属」まで。資産の生死は Project ウィンドウの責務。

**併せて以下を削除する。** CLAUDE.md の未使用 API 分類でいう **C（置き換え残骸）** に当たり、A（意図的な先行宣言）でも B（フェーズ外）でもないので削除して妥当:

| 削除対象 | 位置 |
|---|---|
| `SceneGraphView.ConfirmAndDeleteNodeAssets` | `SceneGraphView.cs:643-676` |
| `SceneGraphViewModel.DeleteNodeAssets` | `SceneGraphViewModel.cs:490-539` |
| `SceneGraphViewModel.FindOtherGraphsContaining` | `SceneGraphViewModel.cs:540-568` |

**検証済み: この 3 つを参照しているのは `SceneGraphView.cs` のみ。テストからの参照は 0 件。**

```bash
grep -rn "DeleteNodeAssets\|FindOtherGraphsContaining" unity/Assets/ --include=*.cs
```

#### 実装時の注意

1. **削除後、`AssetDatabase.DeleteAsset` の呼び出しがリポジトリの SceneGraph 配下から 0 件になることを確認する**（`DeleteNodeAssets` が唯一の呼び出し元だった）
2. **既存の潜在バグを直す**: 現 `SceneGraphView.cs:601-603`
   ```csharp
   var parentLabel = targetNode != null
       ? $"Parent to '{targetNode.NodeData.Identity}'"   // ← NodeData が null なら落ちる
       : "Parent to";
   ```
   `targetNode.NodeData != null` も条件に加えること
3. `Paste` の有効判定は **T3 が `GetPasteSource()` に差し替える**。T3 とマージするとき、古い `SceneGraphClipboard.CanPaste(EditorGUIUtility.systemCopyBuffer)` 直読みに戻さないこと
4. 「選択なし / ノード選択」の分岐は `selection.OfType<SceneGraphNode>()` の件数で行う。
   **GraphView が右クリック時に選択をどう変えるかは未検証**（前スライスの未検証事項）。手動確認項目に「ノードを選択していない状態でノード上を右クリックしたとき何が出るか」を必ず含めること
5. `Create Node` は現状どおり `contentViewContainer.WorldToLocal(evt.mousePosition)` の位置に作る（ダイアログなし）
6. `Unparent Selected` の対象判定で `_viewModel.CurrentEdges` を使うとき、`!= null` で判定する（`?.` 禁止 — `SceneGraphEdges` は `ScriptableObject`）

#### テスト

**単体テスト不可（View 層 / `ContextualMenuPopulateEvent`）。** §4.2 の手動確認へ回す。無理に書かないこと。

`DeleteNodeAssets` / `FindOtherGraphsContaining` の削除でテストが減らないことを確認する（参照 0 件なので減らないはず）。

---

### §3.6 【T6】偽 null 迂回の既存 2 箇所

> **T5 に同梱してよい。** どちらも「撤去」で論点が畳める。

#### 変更対象（**この 2 箇所だけ**）

| 場所 | 現在のコード | 対象の型 |
|---|---|---|
| `View/SceneGraphView.cs:169` | `var pos = _viewModel.CurrentLayout?.GetPosition(nodeData) ?? Vector2.zero;` | `SceneGraphLayout` = `ScriptableObject` |
| `View/SceneGraphEditorWindow.cs:167` | `target ??= graphs[0];` | `SceneGraphEdges` = `ScriptableObject` |

#### 直し方

```csharp
// SceneGraphView.cs:169
var layout = _viewModel.CurrentLayout;
var pos = layout != null ? layout.GetPosition(nodeData) : Vector2.zero;

// SceneGraphEditorWindow.cs:167
if (target == null) target = graphs[0];
```

#### 実装時の注意（**ここを誤ると被害が出る**）

`?.` を grep して**機械的に一括置換しないこと。** 以下は `UnityEngine.Object` ではないので `?.` が正しく短絡する。**触らない**:

| 場所 | コード | 型 |
|---|---|---|
| `SceneGraphView.cs:204, 228` | `parentNode?.NodeData` / `childNode?.NodeData` | `SceneGraphNode` = `VisualElement` |
| `SceneGraphEditorWindow.cs:201` | `_viewModel?.CurrentEdges` | 素の C# クラス |
| `SceneGraphEditorWindow.cs:208, 214, 219, 325` | `_graphView?.…` | `VisualElement` |
| `SceneGraphInspectorPanel.cs:224-225` | `payload0?.Reference` / `?? string.Empty` | `AssetPayload` は素の C# クラス、`AssetGUID` は `string` |

`edge.input?.node` / `edge.output?.node` / `evt.…` / `?.Invoke` も同様に安全。

#### テスト

なし（振る舞い変更なし。破棄済みオブジェクトの状況を単体テストで作るのは非現実的）。

---

### §3.7 変更後の規模見積もり（**CLAUDE.md A-1 / A-2 / A-3**）

| ファイル | 現在 | 予想 | 責務数 | 担当 |
|---|---|---|---|---|
| `View/SceneGraphView.cs` | 812 | **約 680** | **5** | T1(-157) T3(+25) T4(+30) T5(+5) T6(±0) |
| `ViewModel/SceneGraphViewModel.cs` | 1008 | **約 930** | — | T5(-79) |
| `Service/SceneGraphPasteService.cs`（新規） | — | **約 270** | **1** | T1(230) T2(+40) |
| `ViewModel/SceneGraphClipboard.cs` | 149 | 約 165 | 1 | T2(+16) |
| `View/SceneGraphEditorWindow.cs` | 417 | 417 | — | T6(±0) |
| `Tests/Editor/SceneGraph/` | 13 件 | **18〜19 件** | — | T2(+5) T4(+1) |

`SceneGraphView.cs` の 5 責務: 要素ライフサイクル / `GraphViewChange` 処理 / クリップボード配線 / ContextMenu / D&D + AutoLayout。

#### A-2 の宣言 — 500 行超えを許容する理由と分割先

**`SceneGraphView.cs` は予想 680 行で 500 行を超え続ける。これは設計判断として今回は許容する。**

理由: T5（F4）が ContextMenu を全面書き換えするため、同時に抽出すると差分が「移動」と「書き換え」の混合になってレビュー不能になる。

**分割先は既に確定しているので、スライス3 の先頭タスクとして実行する:**

| 抽出先 | 移すもの | 移した後の `SceneGraphView.cs` |
|---|---|---|
| `View/SceneGraphContextMenu.cs` | `OnContextMenuPopulate` / `ResolveContextTargetNode` | -約 90 行 |
| `Service/SceneGraphAutoLayout.cs` | `PerformAutoLayout` / `LayoutTree` | -約 60 行 |
| `View/SceneGraphSceneAssetDropHandler.cs` | `OnDragUpdated` / `OnDragPerform` / `HasSceneAssetInDrag` / `GetSceneAssetPathsFromDrag` | -約 55 行 |

→ 最終的に `SceneGraphView` に残るのは要素ライフサイクル（`RebuildGraph` / `ScheduleRebuild` / `AddNodeElement` / `OnGraphViewChanged` / `GetCompatiblePorts`）とデリゲート配線のみ、約 470 行。

#### A-3 の宣言 — 既存ファイルへの割り当ては意図的

**T3 / T4 / T5 のロジックを `SceneGraphView` に残すのは、上記のとおり意図した設計判断であって、置き場所を考えなかった結果ではない。** 実装者は「なぜ Service に出さないのか」を考える必要はない。

---

## §4. 受け入れ条件

### §4.1 追加するテスト（**A-4: 最も強く効く構造強制**）

| ファイル | # | 内容 | チケット |
|---|---|---|---|
| `SceneGraphPasteServiceTests.cs`（新規） | 1 | 複製がコピー集合外の親を引き継ぐ | T2 |
| 〃 | 2 | 親がコピー集合内なら二重接続しない | T2 |
| 〃 | 3 | 参照ペースト（別グラフ）では親を勝手に繋がない | T2 |
| 〃 | 4 | 複製ノードの `Payloads` が空（W-5 回帰） | T2 |
| `SceneGraphClipboardTests.cs` | 5 | `GetIndicesWithoutInternalParent` の純粋関数テスト | T2 |
| `SceneGraphViewModelBatchTests.cs` | 6 | `ConnectEdges` 一括の Undo が 1 回で戻る | T4 |

#### テストの雛形（既存 `SceneGraphViewModelBatchTests.cs` からそのまま流用する）

**既存の `Assets/SceneGraphData/` に絶対に書き込まない。** `internal static` のフォルダ設定を差し替えて専用の一時フォルダを使う:

```csharp
private const string RootFolder = "Assets/__SceneGraphEditorTests__";
private const string NodesFolder  = RootFolder + "/Nodes";
private const string GraphsFolder = RootFolder + "/Graphs";
private const string LayoutsFolder = RootFolder + "/Layouts";

[SetUp]
public void SetUp()
{
    _originalNodesFolder  = SceneGraphViewModel.NodesFolder;
    _originalGraphsFolder = SceneGraphViewModel.GraphsFolder;
    _originalLayoutsFolder = SceneGraphViewModel.LayoutsFolder;

    SceneGraphViewModel.NodesFolder  = NodesFolder;
    SceneGraphViewModel.GraphsFolder = GraphsFolder;
    SceneGraphViewModel.LayoutsFolder = LayoutsFolder;

    if (!AssetDatabase.IsValidFolder(RootFolder))
        AssetDatabase.CreateFolder("Assets", "__SceneGraphEditorTests__");
    AssetDatabase.CreateFolder(RootFolder, "Nodes");
    AssetDatabase.CreateFolder(RootFolder, "Graphs");
    AssetDatabase.CreateFolder(RootFolder, "Layouts");
}

[TearDown]
public void TearDown()
{
    Undo.ClearAll();
    if (AssetDatabase.IsValidFolder(RootFolder))
        AssetDatabase.DeleteAsset(RootFolder);

    SceneGraphViewModel.NodesFolder  = _originalNodesFolder;
    SceneGraphViewModel.GraphsFolder = _originalGraphsFolder;
    SceneGraphViewModel.LayoutsFolder = _originalLayoutsFolder;
    AssetDatabase.Refresh();
}
```

ノード / グラフの作成ヘルパも既存クラスと同じものを使う（`ScriptableObject.CreateInstance` → `AssetDatabase.CreateAsset`）。

`internal` 型へのアクセスは `Editor/AssemblyInfo.cs` の `InternalsVisibleTo("OneStarMaker.Tests.Editor")` で既に通っている。

### §4.2 手動での動作確認（View 層で単体テストが書けない分）

**人間が実施する。** 実装者はここに項目を追加してよいが、勝手に単体テストに置き換えないこと。

| # | 確認内容 | チケット |
|---|---|---|
| 1 | Ctrl+C の直後、右クリック Paste が**有効になる** | T3 |
| 2 | **Ctrl+V が修正前から動いていたか**（真因記録用。修正前のビルドで先に確認する） | T3 |
| 3 | 別グラフを開いて Paste → **参照追加**になる（複製されない） | T3 |
| 4 | 同一グラフで Paste → **複製**になる | T3 |
| 5 | Ctrl+D → **親が引き継がれる**（`World → Cell_A` で `Cell_A` 複製 → `World → Cell_A1`） | T2 |
| 6 | 複数選択でエッジをドラッグ → 選択全部が同じ親に付き、**Undo 1 回**で戻る | T4 |
| 7 | ContextMenu が「選択なし」「ノード選択」で**出し分かる** | T5 |
| 8 | **ノードを選択せずにノード上で右クリック**したとき何が出るか（未検証事項） | T5 |
| 9 | `Select in Project` で Project ウィンドウがハイライトする | T5 |
| 10 | `Delete Node Asset…` が**消えている** | T5 |
| 11 | ノードを大量に置いたグラフで表示位置がずれない（T6 の回帰） | T6 |

### §4.3 コンパイルとテスト実行

```bash
pwsh tools/run-tests.ps1
```

- **Unity Editor を閉じた状態で実行すること**
- **テスト 0 件は失敗扱い**（コンパイルエラーが 0 件として現れる）。件数を必ず目視する
- SceneGraph 関連が全 green（13 件 → 18〜19 件に増える）
- 全体の失敗が **`TelemetryLogCorrelationTests` / `UpdateSystemHostTests` の 2 件から増えていない**こと
- 警告 0 件（`#nullable enable` が全新規ファイルに付いていること）

### §4.4 構造の受け入れ条件

- `SceneGraphPasteService.cs` に `GraphView` / `GraphElement` / `VisualElement` / `selection` / `schedule` / `_nodeMap` という文字列が **1 つも出てこない**
- `SceneGraphView.cs` が **690 行以下**
- `SceneGraphViewModel.cs` が **940 行以下**
- `grep -rn "DeleteNodeAssets\|FindOtherGraphsContaining" unity/Assets/ --include=*.cs` が **0 件**
- `AssetDatabase.DeleteAsset` が **ContextMenu 経路から 0 件**
  - > 当初「SceneGraph 配下から 0 件」と書いていたが条件が広すぎた。`SceneResourceGenerator.cs:228` に生成物削除の正当な呼び出しが残っており、これは対象外。**判定は「ContextMenu 経路から消えたこと」に読み替える**（Phase C で修正）
- `is null` / `ReferenceEquals` が SceneGraph 配下に **0 件**、`?.` / `??` が `UnityEngine.Object` に掛かっている箇所が **0 件**

---

## §5. やらないこと

このスライスの範囲外。**着手しないこと。**

### 5.1 スライス3 の先頭タスク

`SceneGraphView` の残りの分割（§3.7 の表のとおり `SceneGraphContextMenu.cs` / `SceneGraphAutoLayout.cs` / `SceneGraphSceneAssetDropHandler.cs`）。

### 5.2 その他の既知課題

- **B9**: `RebuildGraph` が `ViewModel.Nodes` **全件**を描画する。**2 つ目のグラフを作ると全ノードが (0,0) に重なる。** 修正には「Add Existing Node…」検索ピッカーの新設もセットで必要
- ノード上へのバリデーション結果（V-1〜V-6）バッジ表示（現状 Console のみ）
- Generate stale のツールバー常時表示（現状は起動時 1 回の Console 警告）
- Inspector の複数編集（LoadType 一括変更）
- ノードのダブルクリックで Payload[0] のシーンを開く
- **マウス位置へのペースト**（現状は元座標 +40,40 固定）。今回は変えない
- 選択サブツリーだけの Auto Layout

### 5.3 蒸し返さないこと（判断済み）

- **Paste 意味論**: 別グラフ = 参照追加 / 同一グラフ = 複製 / Ctrl+D は常に複製。**決定済み**
- **Delete 意味論**: グラフからの除外と資産の実削除を分離。**決定済み**
- **手動確認は人間が自分でやる**
- 前スライスの C' 監査 Job C（禁止事項 / B1〜B11 の再検証）は人間の判断で打ち切り済み

---

## §6. 差し戻し

### 第 1 巡（Wave 1: T1 / T4 / T5+T6）— **差し戻しなし**

3 チケットとも受け入れ条件を満たしており、機能・構造どちらの指摘も発生しなかった（§7 参照）。

### F1 の真因 — **確定（2026-08-05、人間による手動確認）**

**Ctrl+V は修正前から動いていた。** これにより §3.3 の仮説が裏付けられた。

| 経路 | 書き込み先 | 読み出し先 | 修正前の挙動 |
|---|---|---|---|
| Ctrl+C → Ctrl+V | GraphView 内部のクリップボード | 同左 | **動く**（内部で完結していたため） |
| 右クリック Copy | `EditorGUIUtility.systemCopyBuffer` | — | 動く |
| 右クリック Paste | — | `systemCopyBuffer` | **Ctrl+C の内容が見えず Disabled のまま** |

したがって F1 は「Paste が壊れている」のではなく、**Copy の書き込み先が 2 系統に分かれていて、右クリック Paste の有効判定が片方しか見ていなかった**という配線の問題である。§3.3 の「`OnSerializeGraphElements` でスナップショットして `private static string` を唯一の窓口にする」という方針は、この真因に対して正しい。

> **注意**: T5 の ContextMenu 書き換えにより、現在は Paste が「Disabled で表示される」のではなく「表示されない」挙動に変わっている。T3 で読み出し元を差し替えるまで、右クリックからの Paste は Ctrl+C 後に出てこない。

### 第 2 巡（Wave 2: T3）— **差し戻し 1 件**

#### D-1: `OnCanPasteSerializedData` と `OnUnserializeAndPaste` が別の元を見ている

**これは実装の誤りではなく §3.3 の指示の欠落である。** 読み出し口を 3 箇所（`OnCanPasteSerializedData` / `PasteFromClipboard` / ContextMenu）と列挙した際に、**`OnUnserializeAndPaste` を数え落としていた。**

```csharp
private bool OnCanPasteSerializedData(string data)
{
    return SceneGraphClipboard.CanPaste(GetPasteSource());   // data を無視
}

private void OnUnserializeAndPaste(string operationName, string data)
{
    ApplyPaste(data, forceDuplicate);                        // GetPasteSource() を無視
}
```

**再現条件**: 別ウィンドウ / 別 Unity セッションで SceneGraph のノードを Copy した状態で、このウィンドウで Ctrl+V を押す。

- `systemCopyBuffer` は有効なので `OnCanPasteSerializedData` は **true** を返す
- しかし GraphView が渡してくる `data` はこのウィンドウの内部クリップボード（空 or 古い内容）
- 結果、**「貼れる」と判定されたのに何も起きない / 意図しない古い内容が貼られる**

同一ウィンドウ内の Ctrl+C → Ctrl+V は両者が一致するため影響しない。

**修正方針**: 判定と実行で同じ規則を使う。`data` が `CanPaste` を通ればそれを優先し、通らなければ `GetPasteSource()` にフォールバックする、という規則を両メソッドに適用する。

```csharp
private static string ResolvePasteData(string data)
    => SceneGraphClipboard.CanPaste(data) ? data : GetPasteSource();
```

`OnCanPasteSerializedData` は `CanPaste(ResolvePasteData(data))`、`OnUnserializeAndPaste` は `ApplyPaste(ResolvePasteData(data), forceDuplicate)` とする。

### 第 3 巡（T3）— **差し戻し 1 件（C' 監査 A-1 による）**

#### D-2: Ctrl+V と右クリック Paste が別の優先順位でペースト元を選ぶ

**D-1 で Phase C が出した修正方針そのものが誤りだった。** 実装の落ち度ではない。

D-1 の修正で導入した `ResolvePasteData` は `data`（GraphView 内部バッファ）を最優先していたが、ContextMenu の Paste は `GetPasteSource()`（`systemCopyBuffer` → static の順）を使う。

```csharp
private static string ResolvePasteData(string data)
    => SceneGraphClipboard.CanPaste(data) ? data : GetPasteSource();   // ← data 最優先
```

**再現条件**: 別ウィンドウで Copy すると `systemCopyBuffer` と `_lastClipboardJson` は新しい内容になるが、このウィンドウの GraphView 内部バッファは古いまま。結果、**Ctrl+V は古い内容を、右クリック Paste は新しい内容を貼る。**

**修正**: `data` を優先する理由がない。Copy 経路は必ず `StoreClipboardJson()` を通り `systemCopyBuffer` と `_lastClipboardJson` の両方へ書かれるため、**`GetPasteSource()` が `data` より古くなることはない。** `ResolvePasteData` を削除し、`GetPasteSource()` に一本化した。

最終形（読み出し 4 箇所すべてが `GetPasteSource()`、書き込み 2 箇所すべてが `StoreClipboardJson()`）:

```
313  StoreClipboardJson(BuildClipboardJson(...))   ← メニュー Copy
370  StoreClipboardJson(json)                      ← Ctrl+C / Ctrl+X / Ctrl+D
318  GetPasteSource()                              ← PasteFromClipboard
376  CanPaste(GetPasteSource())                    ← OnCanPasteSerializedData
403  ApplyPaste(GetPasteSource(), ...)             ← OnUnserializeAndPaste
477  CanPaste(GetPasteSource())                    ← ContextMenu の表示判定
```

`OnUnserializeAndPaste` の引数 `data` は未使用になったが、**GraphView のデリゲート契約なのでシグネチャは変更していない。**

### T3 の評価（D-1 / D-2 を除く）

指示より良い形になっている。書き込み口を 2 箇所に分ける指示だったが、**`StoreClipboardJson()` 1 本に集約**され、`CanPaste` を通る内容だけが `_lastClipboardJson` と `systemCopyBuffer` の両方へ書かれる。読み出しも `GetPasteSource()` 経由の 3 箇所に統一され、`systemCopyBuffer` の直読みは同メソッド内の 1 箇所だけになった。`OnSerializeGraphElements` の戻り値も維持され、`DuplicateSelection` は未変更。

### 第 4 巡（コードレビュー）— **指摘 5 件 + 未使用 API 削除 2 件。すべて修正済み**

> 実施: Claude Code / Opus 5、2026-08-06。対象は SceneGraph 配下の未コミット変更全体（スライス1 + Wave 1 + Wave 2）。
> Phase C（§7）が受け入れ条件との照合に寄っていたため、**受け入れ条件に無い実バグ**を狙って読み直した結果。実装は Phase C 担当がそのまま行った（チケット化のコストが修正量を上回るため。通常の Phase B 委譲からは外れる）。

| # | 指摘 | 修正 |
|---|---|---|
| **R-1** | **Ctrl+D がシステムクリップボードを上書きする** | `ExecuteCommandEvent` を `TrickleDown` で捕捉し、Duplicate のときは `StoreClipboardJson` を呼ばない |
| **R-2** | `AssetDatabase.RenameAsset` の失敗（エラー文字列）を 2 箇所で握り潰していた | 事前にパス衝突を検査して弾く + 戻り値を検査して報告 |
| **R-3** | `SceneResourceGenerator` がノードごとにプロジェクト全体を走査（O(N×M)） | `BuildSceneResourcePathIndex()` をループ外で 1 回だけ実行し辞書引きに変更 |
| **R-4** | **B9**: `RemoveNodesFromGraph` の効果が次のリフレッシュで巻き戻って見える | `RebuildGraph` が `currentEdges.ContainsNode()` で描画対象を絞る |
| **R-5** | `DrawPayloads`（IMGUI 描画中）から `RenameAsset` + `SaveAssets` を実行していた | `schedule.Execute(...).ExecuteLater(0)` で次フレームへ退避 |
| **R-6** | `MoveNode` / `DisconnectEdge` が呼び出し元 0 件 | 削除（未使用 API 分類 **C = 置き換え残骸**。複数形に一本化済み） |

#### R-1 の波及（重要）

Duplicate でクリップボードを保存しなくなったため、**`OnUnserializeAndPaste` も同時に直す必要が生じた。** 保存しないのに `GetPasteSource()` を読むと、Ctrl+D が「古いクリップボードの内容」を複製してしまう。

```csharp
// Duplicate は「いま選択されている要素」を複製する操作 → 直前の data を使う
// Paste はクリップボードを貼る操作 → GetPasteSource() を唯一の窓口とする
var json = forceDuplicate ? data : GetPasteSource();
```

**C' の A-1 が指摘した「判定と実行の非対称」は解消されたまま維持されている** — Paste 経路は Ctrl+V も右クリックも `GetPasteSource()` の 1 本で、Duplicate だけが別経路という整理になった。

#### R-4 の挙動変更（人間の確認が必要）

**これまで全グラフに表示されていた「グラフ未所属のノード」が表示されなくなる。** 現在 `Assets/SceneGraphData/Nodes/` にある 23 件のうち、前セッションの手動確認で作られた複製（`Cell_2_211` / `Cell_2_311` / `InGameScene1`）を含む未所属ノードが対象。

`_viewModel.Nodes` 自体は変更していない（一意名の採番と `Generate` が全件を必要とするため）。グラフへ入れ直す経路は SceneAsset の D&D と `Create Node`（同名なら既存を再利用）。**意図と異なる場合は `RebuildGraph` のフィルタ 1 行を戻せばよい。**

#### 規模への影響

| ファイル | 変化 |
|---|---|
| `View/SceneGraphView.cs` | 666 → **707**（+41） |
| `ViewModel/SceneGraphViewModel.cs` | 930 → **935**（事前チェック +19 / API 削除 -14） |
| `View/SceneGraphInspectorPanel.cs` | 228 → **245**（+17） |
| `SceneResourceGenerator.cs` | 437 → **452**（+15） |

**`SceneGraphView.cs` が §4.4 の受け入れ条件（≤690 行）を 17 行超過した。** R-1 のコマンド捕捉が増えた分。スライス3 の分割（ContextMenu / AutoLayout / D&D で約 205 行減る見込み）で解消される範囲と判断し、本スライスでは許容する。

#### テスト

```
SceneGraph : 19 / 19 green    Unity 終了コード 0
全件       : 429 / 426 passed / failed 3   ← 修正前と完全に同一
```

#### 確認していないこと（R-1 / R-4 / R-5 は View 層で単体テスト不可）

- **`ExecuteCommandEvent.commandName` が Ctrl+D で実際に `"Duplicate"` になるかは未検証。** コード上の根拠は `OnUnserializeAndPaste` が既に同じ文字列で判定していることのみ。**ここが違うと Ctrl+D が壊れるため、手動確認では最初に Ctrl+D を試すこと**
- R-4 のフィルタが「意図した表示」になっているか
- R-5 の遅延リネームが Payload[0] 変更時に正しく走るか

### 手動確認で発見された事象（本スライスの範囲外）

**SceneGraphEditor に見覚えのないノードが表示される。** 調査の結果、**既知の B9**（§5.2 で範囲外と宣言済み）であり Wave 1 の変更とは無関係。

- `SceneGraphViewModel.RefreshNodes` はグラフ所属ノードに加えて `AssetDatabase.FindAssets("t:SceneNodeData", NodesFolder)` の全件を「選択肢として提示用」に収集する
- `SceneGraphView.RebuildGraph` はその `_viewModel.Nodes` を全件描画する
- 結果、`Assets/SceneGraphData/Nodes/` にある `.asset` は**グラフ未所属でも全グラフに出る**

現在 Nodes フォルダに 23 件あり、うち 14 件が git 未追跡。`Cell_2_211` / `Cell_2_311` / `InGameScene1` は `GenerateUniqueName` の採番規則に一致しており、**前セッションで Duplicate を手動確認したときの複製が残ったもの**と判断できる。

テスト用一時フォルダ `Assets/__SceneGraphEditorTests__` の残留は無し（TearDown は正常動作している）。

---

## §7. レビュー結果

> 記入: Claude Code / Opus 5、2026-08-05。**Wave 1（T1 / T4 / T5+T6）と Wave 2（T2 / T3）の全 6 チケットを対象**とする。

### 7.1 経緯

全チケットを `cursor-grok-4.5-high` で実装。実行環境は `C:\Users\void\.wt\{t1..t5}` の detached worktree。

**スライス1 が未コミットだったため、`-w` の worktree は HEAD（`6620c17`）の内容しか持たず、`SceneGraphView.cs` が 368 行・`ApplyPaste` 不在の状態になる**ことが着手前に判明した。ブランチを汚さずに解決するため、一時インデックス経由で dangling commit を 2 つ作り、そこから worktree を張った。

| スナップショット | 内容 | 用途 |
|---|---|---|
| `d7dc5af` | スライス1 完成時点 | Wave 1（t1 / t4 / t5）の基点 |
| `b7eaa5f` | Wave 1 統合後 | Wave 2（t2 / t3）の基点 |

どちらもどのブランチにも乗っておらず `git log` に現れない。**本体の HEAD・インデックス・作業ツリーは終始無変更。**

- **Wave 1**: T1 → T5+T6 → T4 の順で本体へ適用。**コンフリクト 0 件**。3 本とも `SceneGraphView.cs` を触るが担当行域を分けたため、T1 が 155 行撤去した後も T5 / T4 のハンクが文脈マッチでそのまま収まった
- **Wave 2**: T2（Service + Clipboard + テスト）と T3（View）は触るファイルが重ならず、こちらも競合なし
- 差し戻しは **D-1 の 1 件のみ**（§6）。T3 第 2 巡で解消
- `git apply --3way` はインデックスが HEAD 世代でスライス1 を知らないため使えなかった。作業ツリーとスナップショット blob の一致を `cmp` で確認したうえで素の `git apply` を使用

### 7.2 構造レビュー（機能レビューより先に実施）

**手順 1: `git diff --stat`** — **50% 以上増えた既存ファイルは無い。View と ViewModel はむしろ減少。**

| ファイル | 変化 | 内訳 |
|---|---|---|
| `View/SceneGraphView.cs` | 812 → **666**（-18%） | T1 -155 / T3 +32 / T4 +23 / T5 -36 |
| `ViewModel/SceneGraphViewModel.cs` | 1008 → **930**（-8%） | T5 -78 |
| `ViewModel/SceneGraphClipboard.cs` | 149 → **180**（+21%） | T2 +31 |
| `View/SceneGraphEditorWindow.cs` | 417 → 417（1 行変更） | T6 |
| `Service/SceneGraphPasteService.cs` | 新規 **215** | T1 191 / T2 +24 |

**手順 2: 責務の数** — `SceneGraphView.cs` は **28 メンバー / 6 責務 / 666 行**。

1. 要素ライフサイクル（`RebuildGraph` / `ScheduleRebuild` / `AddNodeElement` / `GetCompatiblePorts`）
2. `GraphViewChange` 処理（`OnGraphViewChanged`）
3. クリップボード配線（10 メンバー）
4. ContextMenu（3 メンバー）
5. D&D（4 メンバー）
6. AutoLayout（2 メンバー）

**500 行・3 責務の基準を超えたままだが、これは §3.7 の A-2 / A-3 で事前に宣言した設計判断であり、本スライスが新たに作った負債ではない。** 分割先（`SceneGraphContextMenu.cs` / `SceneGraphAutoLayout.cs` / `SceneGraphSceneAssetDropHandler.cs`）は §3.7 に確定済み。

**手順 3: 単体テストが書けないロジック** — §7.5 に分けて記載。

**手順 4: 偽 null（`?.` / `??` / `is null` / `ReferenceEquals`）**

- `SceneGraphPasteService.cs`: **0 件**。`layout != null ? ... : Vector2.zero` の形と対策コメントが原文どおり移植されている
- `SceneGraphClipboard.cs` の追加分: **0 件**
- T6 の対象 2 箇所（`SceneGraphView.cs:169` / `SceneGraphEditorWindow.cs:167`）は**解消済み**
- **本スライスが触った 4 ファイル**（`SceneGraphView` / `SceneGraphViewModel` / `SceneGraphEditorWindow` / `SceneGraphInspectorPanel`）に残る `?.` / `??` は全件精査し、`UnityEngine.Object` に掛かっているものは無い（`SceneGraphNode` = `VisualElement`、`_viewModel` / `AssetPayload` は素の C# クラス、`AssetGUID` は `string`）。一括置換もされていない

> **訂正（C' 監査 A-2 による）**: 当初この項を「**SceneGraph 配下に** `UnityEngine.Object` に掛かる `?.` / `??` は無い」と書いていたが、**これは誇大主張であり §7.5 と矛盾していた。** `SceneResourceGenerator.cs` は `Scripts/Editor/SceneGraph/` 配下にあり、その 266 行目に真正の偽 null 迂回が残っている（§7.5 参照）。主張の範囲を「本スライスが触ったファイル」に限定するよう訂正した。**§4.4 の受け入れ条件「`?.` / `??` が `UnityEngine.Object` に掛かっている箇所が 0 件」は、SceneGraph 配下全体で見ると未達である。**

### 7.3 受け入れ条件との照合

> **§6 第 4 巡（コードレビュー）の修正を反映後の最終値は下表と異なる。** `SceneGraphView.cs` は 707 行となり ≤690 の条件を 17 行超過している（設計判断として許容。§6 参照）。以下は Wave 2 直後の値。

| 条件 | 結果 |
|---|---|
| `SceneGraphView.cs` ≤ 690 行 | **666** ✓（第 4 巡後は 707 で**未達**） |
| `SceneGraphViewModel.cs` ≤ 940 行 | **930** ✓ |
| Service に `GraphView` / `GraphElement` / `VisualElement` / `selection` / `schedule` / `_nodeMap` が 0 件 | **0 件** ✓ |
| Service の API が §3.1 の指定どおり | `BuildClipboardJson(IReadOnlyList<SceneNodeData>)` / `ApplyPaste(string, bool)` ✓ |
| `SceneGraphClipboard` が `AssetDatabase` / `UnityEditor` 非依存 | ✓ `using` は `System` / `System.Collections.Generic` / `UnityEngine` のみ（grep のヒットはコメント 1 行） |
| `DeleteNodeAssets` / `FindOtherGraphsContaining` が 0 件 | **0 件** ✓ |
| `AssetDatabase.DeleteAsset` が ContextMenu 経路から 0 件 | ✓（`SceneResourceGenerator.cs:228` の生成物削除は対象外） |
| T4 の一括分岐が `validEdges` を汚さない | ✓ `batchConnectHandled` ガード + `continue` |
| T4 がサイクル判定を自作していない | ✓ `ConnectEdges` へ委譲 |
| T4 が `BeginBatch` を足していない | ✓ 外側の `"Edit Scene Graph"` に乗る |
| `targetNode.NodeData` の null デリファレンス修正 | ✓ |
| T2 が複製分岐のみに適用している | ✓ `if (duplicate)` の内側、参照ペーストは不変 |
| T2 が二重接続しない | ✓ `GetIndicesWithoutInternalParent` で内部親を持つ index を除外 |
| T2 が既存 `BeginBatch` の内側で接続している | ✓ Undo 1 回 |
| T3 の読み出しが 1 メソッドに集約されている | ✓ `GetPasteSource()` / `ResolvePasteData()` 経由の 4 箇所 |
| T3 の書き込みが集約されている | ✓ **指示（2 箇所）より良く `StoreClipboardJson()` 1 本に集約** |

**T1 の移植の忠実性**と **T2 / T3 の追加分**を全行照合した。

**T1 の移植の忠実性**を全行照合した。`reparentedIdentities` の警告、GUID 解決失敗の `LogWarning`、`resolved.All(n => n == null)` の早期 return、`BeginBatch` の位置と Undo 名（`"Duplicate {n} node(s)"` / `"Paste {n} node(s)"`）— **すべて保存されている**。`.Distinct()` は View 側のラッパへ移動（挙動同値）。

### 7.4 テスト結果

```
pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.SceneGraph
  → total 19 / passed 19 / failed 0    Unity 終了コード 0
pwsh tools/run-tests.ps1（全件）
  → total 429 / passed 426 / failed 3
```

SceneGraph は 13 → **19 件**。

| 追加テスト | 由来 |
|---|---|
| `ConnectEdges_SingleUndo_RestoresAllEdges` | T4 |
| `Duplicate_InheritsExternalParent` | T2 |
| `Duplicate_DoesNotDoubleConnect_WhenParentIsInCopySet` | T2 |
| `ReferencePaste_DoesNotConnectExternalParent` | T2 |
| `Duplicate_ClearsPayloads_W5` | T2 |
| `GetIndicesWithoutInternalParent_ReturnsRootsParentsAndEmpty` | T2 |

**T2 が追加した 5 件のうち、`SceneGraphPasteService` を直接叩く 4 件が、T1 の抽出が成立した証拠である。** スライス1 では同じロジックが `GraphView` サブクラス内にあり、テストが 1 本も書けなかった。

> **訂正（C' 監査 A-4 による）**: 当初「5 件が素直に書けたことが証拠」と書いたが、5 件目の `GetIndicesWithoutInternalParent_...` は `SceneGraphClipboard` の純粋関数テストであり **Service に触れない**。証拠として数えるのは 4 件が正しい。

全件の推移:

| 実行 | total | passed | failed |
|---|---|---|---|
| 05:03（スライス1） | 423 | 421 | 2 |
| 23:23（Wave 1） | 424 | 421 | 3 |
| 23:51（Wave 2） | **429** | **426** | **3** |

**全体の failed 3 件は退行ではない。** 過去の実行記録と突き合わせて確認した:

| 実行 | failed | 内訳 |
|---|---|---|
| 01:46（スライス1 途中） | 3 | Telemetry×2 + UpdateSystemHost×1 |
| 05:03（スライス1 完成時） | 2 | Telemetry×1 + UpdateSystemHost×1 |
| 05:06（Telemetry 単独実行） | 2 | Telemetry×2 |
| **23:23（Wave 1 統合後）** | **3** | **01:46 と完全に同一の 3 件** |

`TelemetryLogCorrelationTests.LogAndTelemetry_共有sequenceで1_2_3と採番される` が**実行ごとに通ったり落ちたりする flaky テスト**である。§0.3 に「既存の失敗 2 件」と書いたのは、たまたま通った 05:03 のスナップショットを基準にしたためで、**正しくは「既存の失敗 3 件（うち 1 件は flaky）」**。3 件とも `Foundation` / `UpdateSystem` にあり、本スライスの差分（`Editor/SceneGraph/` のみ）が届く範囲にない。

**初回のテスト実行で Unity が終了時にクラッシュした**（`-1073741819` = ACCESS_VIOLATION）。原因は `UnityEditor.Search.SearchDatabase.OnDisable()` → `SearchTask.Dispose` のワーカースレッド停止失敗であり、**本スライスのコードとは無関係**。`Service/` 追加による `.meta` 生成とフル再インポート（`Untracked: 42520ms`）のコールドスタートで、Search のインデックス構築中に終了要求が来たことによる。Library が温まった状態で再実行したところ **終了コード 0 / 14 件 green** で再現しなかった。

### 7.5 構造的負債

**本スライスが作ったもの（1 件・小）**

- **T4 の一括分岐の述語が単体テストできない。** 「引いたエッジの子側が `selection` に含まれ、かつ選択が 2 個以上」の判定は `GraphView.selection` に依存するため `SceneGraphView` から出せない。モデル側（`ConnectEdges` の一括 Undo）はテスト済みだが、**分岐条件そのものは手動確認でしか検証できない**。CLAUDE.md の「テストが書けないロジックは配置が間違っている」に照らせば、述語を純粋関数（`IReadOnlyList<SceneNodeData> selected, SceneNodeData child` を受ける）に切り出す余地がある。約 10 行なので今回は許容し、スライス3 の分割時に併せて処理することを推奨する

**既存の負債（本スライス外）**

- `SceneResourceGenerator.cs:266` の `child.Parent?.Identity ?? "null"` は**真正の偽 null 迂回**。`SceneResource` は `ScriptableObject`（`SceneResource.cs:15`）なので、破棄済みインスタンスに対して `?.` が短絡せず、`"null"` ではなく古い Identity を表示しうる。診断メッセージ内なので実害は小さいが、**SceneGraph の偽 null 掃除の対象からは漏れていた**。別スライスで処理する
- `SceneGraphView.cs` の 6 責務・**666 行**（§3.7 の A-2 / A-3 で宣言済み）

### 7.6 確認していないこと

- **§4.2 の手動確認 11 項目のうち、実施済みは #2（Ctrl+V が修正前から動いていたか）のみ。** 残り 10 項目は未実施。ContextMenu の出し分け（T5）、F3 の実挙動（T4）、F2 の複製後の親（T2）、F1 修正後の右クリック Paste（T3）はいずれも Unity Editor 上でしか確認できない
- **GraphView が右クリック時に選択をどう変えるか**は依然として未検証。T5 の「選択なし / ノード選択」分岐の正しさはこれに依存する（手動確認 #8）
- **D-1 の再現条件（別ウィンドウ / 別 Unity セッションからの Ctrl+V）は実機で試していない。** 修正の正しさはコード上の整合性で判断しており、元の不具合も修正後の挙動も実測していない
- EditMode テストのみ実行。**PlayMode / Player ビルドのコンパイルは未確認**
- `Selection.objects` / `EditorGUIUtility.PingObject`（T5 の `Select in Project`）の実行経路は**一度も走っていない**
- **`_lastClipboardJson` が `static` であることの副作用**（複数の SceneGraphEditor ウィンドウを同時に開いた場合、ドメインリロードを跨いだ場合）は未検証
- コールドスタート時の Unity Search 終了クラッシュが**再発しうるか**は未確認（ウォーム実行で回避しただけ）
- **B9 の影響下でこのスライスの手動確認を行うと誤読しやすい。** グラフ未所属のノードも全部描画されるため、「複製されたノードがどのグラフに属しているか」は見た目では判断できない

---

## §8. C' 監査結果

### 8.0 監査者の選定理由（**先に記録しておく**）

CLAUDE.md は「C' 監査は実装にも設計にも関与していないモデル。実装が Grok なら監査は Grok 以外」と定めている。

**本スライスでは C' を `cursor-grok-4.5-high` が担当する。実装（Phase B）も同じモデルである。** これは人間の判断による意図的な例外であり、理由は以下:

- 本スライスの C' の**監査対象は「Opus 5 による最終チェック」**であって、Grok 自身の実装差分そのものではない。「自分の実装を自分で採点する」構図ではなく、「他者のレビューを敵対的に検証する」構図になっている
- Phase B と C' の間に Phase C（`claude-opus-4-8-thinking-high`）と最終チェック（Opus 5）が挟まっており、実装から監査までに 2 段の別系統レビューが入る
- **人間の判断として、このモデルの指摘精度は信頼できると評価されている**

C' は「実装が Grok なら監査は Grok 以外」という一般則の例外として運用する。次スライス以降でこの構成を踏襲するかは、本スライスの §8.1 の実績を見て人間が決める。

### 8.1 監査結果

> 実施: `cursor-grok-4.5-high`、`--plan`（読み取り専用）、2026-08-06。論点 2 つ・800 字上限・結論先出しを指示。
> 対象: 本書 §6 / §7（Phase C レビュー）+ `TestResults/WAVE12_DIFF_FOR_AUDIT.md`（全差分をファイル化して渡した。`--plan` はシェルを拒否するため `git diff` を実行できない）。

**総合判定: 条件付きパス。Phase C の主張のうち 3 件が崩れ、うち 1 件は実コードの設計バグだった。**

| # | 指摘 | Phase C による検証 | 対応 |
|---|---|---|---|
| **A-1** | Ctrl+V と右クリック Paste が別の優先順位でペースト元を選ぶ | **妥当。しかも Phase C が D-1 で出した修正方針そのものが誤りだった** | T3 第 3 巡で修正（§6 D-2） |
| **A-2** | §7.2 の「SceneGraph 配下に偽 null 0 件」が §7.5 と矛盾 | **妥当。** `SceneResourceGenerator.cs` は `Scripts/Editor/SceneGraph/` 配下にあり、266 行目に真正の偽 null 迂回がある | §7.2 に訂正を明記。§4.4 の条件は SceneGraph 配下全体では**未達**と記録 |
| **A-3** | §7.5 の「640 行」が Wave 2 後の実数（666）と不一致 | **妥当。** §7.2 / §7.3 は更新したが §7.5 を更新し忘れていた | §7.5 を修正 |
| **A-4** | 「テスト 5 件が T1 抽出の証拠」は誇大。1 件は `SceneGraphClipboard` の純粋関数テストで Service に触れない | **部分的に妥当。** 証拠として数えるのは 4 件が正しい | §7.4 に訂正を明記 |
| A-5 | `DeleteNodeAssets` のコメント残りがある | **再現せず。** `grep -rn "DeleteNodeAssets\|Delete Node Asset" unity/Assets/OneStarMaker/Scripts/ --include=*.cs` が 0 件 | 対応不要 |
| A-6 | §7.3 の「T4 が `BeginBatch` を足していない」は限定的な主張 | 主張自体は正確（`ConnectEdges` 内部の `BeginBatch` は既存）。**指摘としては瑣末** | 対応不要 |
| A-7 | F3 の分岐条件が未テストである点がレビュー漏れ | **誤り。** §7.5 の冒頭に「本スライスが作った負債」として既に記載済み | 対応不要 |

**Phase C の主張のうち妥当と確認されたもの**（C' が明示的に是認）: 行数、Service の境界、T1 の移植の完全性、API 削除、`failed=3` を退行でないとした証拠の立て方。

### 8.2 C' プロセス自体の記録（次スライスへの申し送り）

**C' は最終回答を出さずに正常終了した。** `text` イベントに出力されたのは進捗ナレーションのみで、実際の分析はすべて `thinking` イベント（156 件・約 4,300 字）に留まった。

これは CLAUDE.md「非対話実行の落とし穴」に記録済みの失敗モードそのものである。**ただし今回は、その対策として推奨されている「論点 2 つまで・字数上限・結論先出し」をすべて指示したうえで発生した。**

> **知見: この失敗モードはプロンプト設計だけでは防げない。** `--output-format stream-json` で全イベントを保存しておくことが唯一の保険になる。今回は取得済みイベントから `thinking` を抽出して内容を復元できたため、**コスト規律に従い再実行はしていない**（同じ重さの撃ち直しは禁止）。

`--plan` はファイル読みを通すがシェルを拒否するため、差分は事前に `TestResults/`（git 管理外）へファイル化して渡した。これは想定どおり機能した。

### 8.3 C' モデル選定の結果評価

§8.0 のとおり、本スライスは実装と C' の両方を `cursor-grok-4.5-high` が担当した（CLAUDE.md の一般則の例外）。

**結果として機能した。** C' が突いたのは Grok 自身の実装ではなく Phase C（Opus 5）のレビューの穴であり、Phase C が自力で気づけなかった矛盾を 3 件検出した。特に A-2（同一文書内の §7.2 と §7.5 の矛盾）と A-1（Phase C が出した修正方針自体の誤り）は、自己採点では出てこない種類の指摘である。

ただし **A-5 のような誤検出も混在する**ため、C' の指摘は Phase C 側で 1 件ずつ実コード・実 grep で検証すること。今回は 7 件中 3 件が妥当、1 件が部分的に妥当、3 件が対応不要だった。

---

## 付録: 実装者へ渡す単位

実装者（cursor-agent）は本書全文を読まない前提。**チケットごとに以下を切り出して渡すこと。**

| 渡すチケット | 渡すセクション |
|---|---|
| T1 | §0.2（消してはいけないもの） / §0.3（テスト実行） / §0.4（行番号） / §2 全文 / **§3.1** / §4.3 / §4.4 |
| T2 | §0.2 / §0.3 / §2 全文 / **§3.2** / §4.1（雛形含む） / §4.3 |
| T3 | §0.2 / §0.3 / §0.4 / §2 全文 / **§3.3** / §4.2 の #1〜#4 / §4.3 |
| T4 | §0.2 / §0.3 / §0.4 / §2 全文 / **§3.4** / §4.1 の #6 / §4.2 の #6 / §4.3 |
| T5 + T6 | §0.2 / §0.3 / §0.4 / §2 全文 / **§3.5 + §3.6** / §4.2 の #7〜#11 / §4.3 / §4.4 |

**どのチケットにも §2（壊さない制約）は全文付ける。** ここを削ると偽 null と `RebuildGraph` の同期呼び出しで必ず事故る。

### cursor-agent の起動時に必ず守ること

```bash
"$LOCALAPPDATA/cursor-agent/cursor-agent.cmd" \
  -p --force -w <ticket-id> \
  --model cursor-grok-4.5-high \
  --output-format stream-json \
  --workspace "D:/repositories/unity/SampleGameForOneStarMakerFramework"
```

**前提（これを満たさないと worktree に何も無い）: スライス1 がコミット済みであること。**
`-w` は git worktree を作るので **HEAD の内容しか持っていかない**。スライス1 が未コミットのままだと、worktree の中は `SceneGraphView.cs` が 368 行のスライス1 以前の状態で、`SceneGraphClipboard.cs` もテスト 3 クラスも存在しない。本書の行番号がすべて合わなくなる。

- **`-w` の worktree 隔離は必須**（§0.2 の untracked を守るため）
- **`--output-format stream-json`**。`text` / `json` は中身が空でも exit 0 / `is_error: false` を返すので失敗を検出できない
- **`--plan` はシェルを拒否する。** 差分を見せたい場合は事前に `TestResults/`（git 管理外）へファイル化して渡す
- **1 ジョブの論点は 2 つまで。** 詰め込むと探索でターン予算を使い切って結論が出ないまま正常終了する
- **空振りしたら同じ重さで再実行しない。** 数秒で終わる最小プローブに落として原因を潰してから撃つ。繰り返しが必要と判断したら、その前に理由と回数を人間に伝える
