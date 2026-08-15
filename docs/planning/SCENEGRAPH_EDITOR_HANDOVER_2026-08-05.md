# SceneGraphEditor 作業 — 次セッションへの引き継ぎ

> 作成日: 2026-08-05
> 対象: 次に SceneGraphEditor を触るセッション（人間 / エージェント）
> **まずこの文書を読み、次に正本 [SCENEGRAPH_EDITOR_MULTISELECT_HANDOFF_2026-08-05.md](SCENEGRAPH_EDITOR_MULTISELECT_HANDOFF_2026-08-05.md) を読むこと。**

---

## 0. 今の状態

- ブランチ `impl/scenegraph-editor-multiselect`（チェックアウト中・**未コミット**）
- `git log` は `6620c17` のまま。**前セッションでコミットは一切していない**
- 作業ツリーには本作業と無関係の未コミット変更が多数ある。**untracked（`docs/reference/`, `docs/slides/`, `unity/Assets/Docs/Architecture/27〜29*`）は git で復元できないので絶対に消さないこと**
- テスト: SceneGraph **13/13 green**。全体 423 件中 421 passed
  - 失敗 2 件（`TelemetryLogCorrelationTests` / `UpdateSystemHostTests`）は**既存の失敗**。単独実行でも落ちることを確認済みで、本作業とは無関係

正本は `SCENEGRAPH_EDITOR_MULTISELECT_HANDOFF_2026-08-05.md`（§0〜§8 記入済み）。

---

## 1. 完了したこと（スライス1）

複数選択 / Copy-Paste / Undo 一括化。実装は Claude Code サブエージェント（Sonnet）、レビューは Claude Code（Opus）が 2 巡、C' 監査は cursor-agent の Grok 4.5。

| 変更 | ファイル |
|---|---|
| バッチスコープ `BeginBatch`、複数選択、一括コマンド、Delete 意味論の分離、一意 Identity 採番 | `Editor/SceneGraph/ViewModel/SceneGraphViewModel.cs` |
| クリップボード DTO + 直列化（純粋関数） | `Editor/SceneGraph/ViewModel/SceneGraphClipboard.cs`（新規） |
| クリップボード配線、ContextMenu 拡張、`OnGraphViewChanged` の一括化、`ScheduleRebuild` | `Editor/SceneGraph/View/SceneGraphView.cs` |
| 複数選択ポーリング、Delete 経路統一、`OnUndoRedo` 強化 | `Editor/SceneGraph/View/SceneGraphEditorWindow.cs` |
| 選択イベント追随、3 状態表示 | `Editor/SceneGraph/View/SceneGraphInspectorPanel.cs` |
| `InternalsVisibleTo("OneStarMaker.Tests.Editor")` | `Editor/AssemblyInfo.cs`（新規） |
| テスト 3 クラス 13 件 | `Tests/Editor/SceneGraph/`（新規） |

バグ B1〜B8・B10・B11 解消。**B3 は「Undo 可能になった」のではなく `SyncAssetNamesToIdentity` による事後補正**。B9 は意図的に未修正（§2-C 参照）。

---

## 2. 残タスク

### A. 手動確認フィードバック F1〜F4 — 仕様確定済み・未着手

**ここから始めるのが自然。** 人間の手動確認で出た 4 件。同じブランチで直す。着手時は正本 HANDOFF の §6 に「第 2 巡（手動確認フィードバック）」として転記すること。

#### F1: Paste が効かない。右クリックの Paste が灰色のまま

Copy の経路が 2 つあり、書き込み先が違う:

| 経路 | 書き込み先 |
|---|---|
| Ctrl+C | GraphView 自身のクリップボード（`serializeGraphElements` を呼ぶ） |
| 右クリック Copy | `EditorGUIUtility.systemCopyBuffer` |

メニューの Paste 有効判定は後者しか見ていないため、Ctrl+C の内容が見えない。**この仮説は未検証**（Unity の GraphView 実装を読めないため）。仮説が正しければ Ctrl+V は動いているはず。

**原因に依存しない直し方を採る。** `OnSerializeGraphElements` は Ctrl+C / Ctrl+X / Ctrl+D で GraphView が必ず呼ぶので、そこで JSON をスナップショットし `private static string` を唯一の窓口にする。メニュー Copy もそこへ書き、Paste の有効判定と実行もそこを見る。`systemCopyBuffer` へは併せて書く（別ウィンドウ・別セッションへの持ち出し用）。

**`static` が必要な理由**: グラフを切り替えると `SceneGraphView` が作り直される。§3.7.1 の「別グラフへの参照ペースト」が主要ユースケースなので、インスタンスフィールドでは切れる。

**修正後の手動確認で「Ctrl+V は修正前から動いていたか」を必ず確かめ、真因を §6 に記録すること。**

#### F2: Duplicate が親との接続を落とす

複製時、**コピー集合に含まれない親**を持つノードは、複製先も同じ親へ接続する。

```
元:      World → Cell_A          Cell_A だけ複製
現状:    World → Cell_A,  Cell_A1（親なし）
変更後:  World → Cell_A,  World → Cell_A1
```

親がコピー集合内にいる場合は既存の内部リンク処理が複製同士を繋ぐので、**二重に繋がないよう分岐する**こと。

適用は**複製モードのみ**（Ctrl+D と同一グラフ内 Paste）。別グラフへの参照ペーストは現状維持 — 貼り付け先に同じ親がいるとは限らず、勝手に繋ぐと壊す。

実装箇所は `SceneGraphView.ApplyPaste` の複製分岐。親の取得は `CurrentEdges.GetParent(src)`、接続は既存の `ConnectEdges`。

#### F3: 複数選択でエッジドラッグしたら選択全部を同じ親へ

`OnGraphViewChanged` の `edgesToCreate` 処理を変更する。

- **条件**: 引いたエッジの子側ノードが現在の選択に含まれ、かつ選択中の `SceneGraphNode` が 2 個以上
- **動作**: `ConnectEdges(親, 選択中の全ノード)`。親自身は子リストから除く。サイクルは既存の逐次判定で弾かれ 1 回のメッセージにまとまる
- 視覚エッジは `ScheduleRebuild` が Model から引き直すので GraphView に N 本作らせる必要はない
- Undo は `OnGraphViewChanged` を包む既存の `BeginBatch` に乗るので 1 回で戻る
- **子が選択に含まれない場合は現状どおり 1 本だけ繋ぐ**

#### F4: ContextMenu を選択状態で出し分け、Asset 削除は撤去

Disabled で並べるのをやめ、その場で使えるものだけ出す。

**選択なし**（空白を右クリック）

```
Create Node
Paste                （クリップボードが有効なときのみ）
──────────
Auto Layout
```

**ノードを選択中**

```
Copy
Duplicate
──────────
Parent to '<右クリックしたノード>'   （選択 2 個以上 かつ 対象がノード）
Unparent Selected                  （選択に親を持つノードがある）
Remove from Graph
──────────
Select in Project
Frame Selection
──────────
Auto Layout
```

**`Delete Node Asset…` は撤去**し `Select in Project`（`EditorGUIUtility.PingObject` + `Selection.activeObject`）に置き換える。

理由: Undo 不可の破壊操作を連打する右クリックメニューに置かない。ノードは複数グラフ共有の資産であり、グラフエディタの責務は「グラフへの所属」まで。資産の生死は Project ウィンドウの責務。

**併せて `SceneGraphViewModel.DeleteNodeAssets` と `FindOtherGraphsContaining` を削除する。** 呼び出し元が消えるため。CLAUDE.md の未使用 API 分類でいう **C（置き換え残骸）** に当たり削除候補として妥当。

#### 追加テスト

`SceneGraphViewModelBatchTests` に追加:

- 複製が親を引き継ぐ（F2）
- 親がコピー集合内なら二重接続しない（F2）
- `ConnectEdges` 一括の Undo 1 回（F3 のモデル側）

**F1 と F4 は View 層なので単体テストが書けない。** これは §7.5 で指摘済みの構造問題そのもの。無理に書かず、§4.2 の手動確認項目に追記すること。

---

### B. スライス2 最優先 — `SceneGraphView` の分割

**スライス1が作った負債。** `SceneGraphView.cs` は 368 → 809 行（+120%）、27 メンバー 6 責務。`SceneGraphViewModel.cs` も 550 → 1008 行。

**問題は行数ではなく置き場所。** `ApplyPaste`（約 120 行）にペースト方針判断というドメインロジックが埋まっており、`GraphView` サブクラスにあるため**単体テストが 1 本も書けない**。スライス中で最もリスクの高いロジックが無検証で残っている。

分割先は正本 HANDOFF の §5.1 に具体化済み:

| 抽出先 | 移すもの |
|---|---|
| `Service/SceneGraphPasteService.cs` | `ApplyPaste` / `BuildClipboardJson` / `GetGuidForNode` ← **最重要** |
| `View/SceneGraphContextMenu.cs` | `OnContextMenuPopulate` / `ResolveContextTargetNode` |
| `Service/SceneGraphAutoLayout.cs` | `PerformAutoLayout` / `LayoutTree` |
| `View/SceneGraphSceneAssetDropHandler.cs` | D&D 4 メソッド |

`SceneGraphView` に残すのは要素ライフサイクル（`RebuildGraph` / `ScheduleRebuild` / `AddNodeElement` / `OnGraphViewChanged` / `GetCompatiblePorts`）とデリゲート配線のみ。

**抽出と同時にペースト方針の単体テストを必須とすること。テストが書けないなら抽出が足りていない。**

> **A を先にやると `SceneGraphView` はさらに膨らむ。着手前に規模を見積もり、B の前倒しを判断すること**（CLAUDE.md の Phase A チェック A-1 / A-2）。

---

### C. スライス2 その他（正本 HANDOFF §5.2）

- **B9**: `RebuildGraph` が `ViewModel.Nodes` 全件を描画する。**2 つ目のグラフを作ると全ノードが (0,0) に重なる。** 修正には「Add Existing Node…」検索ピッカーの新設もセットで必要
- **既存の `?.` / `??` による偽 null 迂回**: `SceneGraphView.cs:169`、`SceneGraphEditorWindow.cs:167`。あわせて §2.3(d) の記述にも `?.` / `??` を追加すること
- ノード上へのバリデーション結果（V-1〜V-6）バッジ表示（現状 Console のみ）
- Generate stale のツールバー常時表示（現状は起動時 1 回の Console 警告）
- Inspector の複数編集（LoadType 一括変更）
- ノードのダブルクリックで Payload[0] のシーンを開く
- マウス位置へのペースト（現状は元座標 +40,40 固定）
- 選択サブツリーだけの Auto Layout

---

### D. 未検証事項（正本 HANDOFF §7.4）

- **§4.2 の手動確認 11 項目は人間が実施中。** Duplicate は「概ね良好」と確認済み
- `schedule.Execute(...).ExecuteLater(0)` を 2 つ積んだときの FIFO 保証は未検証。外れてもデータは壊れず「ペースト直後に選択されない」だけ
- GraphView の右クリック時に選択がどう変化するか未検証（`Parent to` の有効条件に影響）
- `Delete Node Asset` の実削除経路は未実行 — **F4 で撤去するので今後も未実行のままでよい**

---

### E. 判断済み・蒸し返さないこと

- **C' 監査の Job C（§5 禁止事項 / B1〜B11 の再検証）は人間の判断で打ち切り。** §8.3 に記録済み
- **手動確認は人間が自分でやる**
- Paste 意味論（別グラフ = 参照追加 / 同一グラフ = 複製、Ctrl+D は常に複製）は決定済み
- Delete 意味論（グラフからの除外と資産の実削除を分離）は決定済み

---

## 3. 次に触る人が踏みやすい地雷

正本 HANDOFF の §2.3 に詳述。特に:

1. **W-5**: `SceneNodeData.OnValidate` が Payload[0] のシーン名で Identity を強制上書きする。**複製では Payloads を必ず空にする。** 引き継ぐと Identity 重複（V-2 Error）が静かに発生する
2. **偽 null**: 破棄済み `UnityEngine.Object` は `== null` で判定する。**`is null` / `ReferenceEquals` だけでなく `?.` / `??` も禁止**（Unity の `==` オーバーロードを迂回して短絡しない）
3. **`BeginBatch`**: 入口で `Undo.IncrementCurrentGroup()` を `SetCurrentGroupName` より先に呼ぶ。落とすと直前の無関係な操作を同じ Undo グループに巻き込む
4. **`ConnectEdge` / `DisconnectEdge` は `OnGraphChanged` を発火しない契約**（`ConnectEdges` / `DisconnectEdges` は発火してよい）
5. **リビルドは `ScheduleRebuild` で次フレームへ遅延させる。** `OnGraphViewChanged` の中で同期的に `RebuildGraph()` すると、GraphView が `elementsToRemove` / `edgesToCreate` を適用し終える前に全要素を撤去してしまう（`BeginBatch` の Dispose は `return graphViewChange;` より前に走る）
6. **`tools/run-tests.ps1` は Unity Editor を閉じてから実行する。** テスト 0 件は失敗扱い
7. **cursor-agent は空振りしたときに本番を再実行しない。** 詳細は CLAUDE.md の「コスト規律」「非対話実行の落とし穴」

---

## 4. 前セッションで恒久化したもの

### `CLAUDE.md`（開発体制節）

- **Phase A のチェック**: 変更後の規模見積もり欄（現在行数 → 予想行数 / 責務数）、500 行 or 3 責務を超える見込みなら分割先を先に明記、既存ファイルへの割り当ては設計判断だと明記、リスクの高いロジックにはテストを要求
- **Phase C の構造レビュー**: `git diff --stat` で 1 ファイルが 50% 以上増えていたら機能レビューより先に構造レビュー。テストが書けないロジックは配置が間違っている
- **偽 null チェックに `?.` / `??` を含める**
- **cursor-agent CLI**: フルパス、`-p` / `--plan` / `--force` / `--model` の意味、コスト規律、非対話実行の落とし穴 4 種

### なぜこれらが要ったか

スライス1で `SceneGraphView.cs` が 809 行に膨らんだ原因は実装側ではなく **HANDOFF の書き方**だった。§3.11「変更対象ファイル一覧」が新責務をすべて既存ファイルへ割り当てており、それが実質的な配置の指示になっていた。規模見積もりの欄も無く、Phase C のレビュー観点も機能に偏っていた（R1〜R5 は全て機能バグで設計指摘ゼロ）。

**最大の学び**: スライス1で新規ファイルとして切り出されたのは `SceneGraphClipboard.cs` ただ 1 つで、それは HANDOFF が「テストが書けないとダメ」と明示した箇所**だけ**だった。逆にテストを要求しなかった `ApplyPaste` は View に埋まったまま残った。

> **「どこに置け」は破られるが、「テストを書け」はテスト可能な配置を強制する。**
