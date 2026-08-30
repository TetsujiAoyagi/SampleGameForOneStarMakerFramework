# Streaming 空間政策 M-2 HANDOFF — 生成器の identity 主キー化

> ステータス: **Phase C' 完了。ローカル commit 待ち。**
> ブランチ予定: `codex/streaming-spatial-m2` から stacked（実装は専用ブランチ）
> 上位計画: [STREAMING_SPATIAL_MIGRATION.md](STREAMING_SPATIAL_MIGRATION.md)
> 到着契約: [§34 OnDemand の空間政策](../../unity/Assets/Docs/Architecture/34-ondemand-spatial-policy.md)
> 現状仕様: [STREAMING_CURRENT_SPEC.md](../streaming/STREAMING_CURRENT_SPEC.md)
> harvest 先: 実装値は `STREAMING_CURRENT_SPEC.md`、恒久契約の追加があれば §34。マージ時に本書を削除する。

本書だけで M-2 を実装する。上位計画の問 7 は本書の決定で閉じる。
「同じ列を渡す」とだけ書いて所属判定を名前文法のまま残す部分解は不合格。

---

## 1. 目的

現行 4×4 の生成結果と南辺 HandAuthored を変えず、WorldCell 生成経路の主キーを
`Vector2Int` から不透明な identity 文字列へ変更する。

`(identity, coordinate)` の対象列を **1 箇所だけ** 構築し、policy、既存収集、生成計画、
既存 asset adoption、SceneGraph、Map / World children、Addressables、範囲外削除へ
同じ identity を流す。座標は見た目と格子配置を焼くためのメタデータとしてだけ残す。

同じ座標を複数 identity が占めても、辞書上書きや削除判定で潰れない口を作る。

---

## 2. 対象外

- 9×6、`Season_*`、修飾付き Environment、トンネル、既存 16 セルの移動・全廃
- `CellIdentity.TryParse` の修飾対応、`StreamingConfig.cellIdQualifier`、identity 翻訳層
- M-3 の R-3、M-4 の型・ファイル移送、S-4 の factory 結線
- `GameSceneFactory` / `CellScene` ctor / `TryFromCellId`（S-4。M-2 で触るとスライスが膨らむ）
- `HandEditProbe` の `CellIdentity.Format`（ファイル移送は M-4、Format 自体は S-4）
- Environment 子の命名（`EnvironmentIdentity.Format` / `EnsureEnvironmentResource`）。S-4
- Scene / Prefab / SceneResource / Addressables asset の生成・書き換え
- asmdef 参照の追加、§21 / §33 / §5 の全面改稿
- Creator の全面分割（収集 / 削除以外は残す）
- `WorldCellGenerator.cs` の 6 型分割（A-2 例外。M-4 でファイルごと移す）

Phase B は C# と HANDOFF の実績欄だけを変更する。Unity Editor、`unity test`、
`unity run`、`pwsh tools/run-tests.ps1`、Addressables ビルドを実行しない。

証明の閉じ方: **生成器・Map・adoption・計画の Editor 証明に閉じる。**
arbitrary identity を焼いても Play では `GameSceneFactory` が `IsCellId` で null を返す。
factory / `CellScene` ctor を直して Play まで通そうとしない。

---

## 3. 決定した API とデータフロー

### 3.1 対象列（Format してよい唯一の口）

`OneStarMaker.Editor.Streaming` に `WorldCellGenerationTarget.cs` を新設する。

```csharp
public readonly struct WorldCellGenerationTarget
{
    public WorldCellGenerationTarget(string identity, Vector2Int coordinate);
    public string Identity { get; }
    public Vector2Int Coordinate { get; }

    public static IReadOnlyList<WorldCellGenerationTarget> FromGrid(WorldGridDefinition definition);
}
```

- identity は null / 空白 / `/` / `\` を拒否する。
- target 列の重複 identity は `StringComparer.Ordinal` で検出し、処理開始前に例外にする。
- 同一 coordinate の別 identity は許可する。
- **`CellIdentity.Format` を呼んでよいのは `FromGrid` の 1 箇所だけ。**
  S-4 が取り換える口はそこだけにする。Creator / `ComputePlan` / `Generate` /
  adoption / Relink / Addressables / 削除 / `CellPopulationPlan` は Format しない。
- 旧署名 `ComputePlan(definition, existingState)` を残して内部で `FromGrid` / `Format`
  するフォールバックは置かない。監査が直した点が復活する。
- この型と generator は M-4 で SampleGame Editor へ一緒に移す。

Creator は `FromGrid(definition)` の結果を受け取り、自分では Format しない。

### 3.2 WorldCellGenerator — target 列は必須引数

旧署名を削除し、次だけを残す。

```csharp
WorldCellGenerator.ComputePlan(definition, targets, existingState)
WorldCellGenerator.Generate(definition, targets, map, parentResource)
WorldCellExistingState.FromMap(map, targets)
AdoptExistingResourceAssets(definition, targets, map, parentResource)
```

- `ComputePlan`、出力フォルダ、SceneResource identity、Skip 判定は `target.Identity` を使う。
  `definition.EnumerateCells()` のあと `CellIdentity.Format` してはならない。
- `FromMap` は `CellIdentity.IsCellId` で選別しない。target identity と完全一致する
  Map entry だけを集める。重複 identity は `HashSet.Add` で黙殺せず **throw**。
- `AdoptExistingResourceAssets` は target 列の identity / path を使い、内部で Format しない。
  旧署名 `(definition, map, parent)` を残して `EnumerateCells` + Format するフォールバックは置かない。
- target に無い SceneResource を `AllCellResources` へ混ぜない。
- `ApplyPlan` / `ApplySceneFiles` は plan entry を正とし、追加の名前解析を行わない。
- 既存テストの `ComputePlan(definition, existingState)` 呼び出しは全て新署名へ直す。

### 3.3 CellPopulationPlan — 範囲外は identity 集合差

`CellGridSpec` は廃止する。`grid.Contains(coordinate)` も `definition.Contains(coord)` も使わない。

```csharp
CellPopulationPlan.Compute(
    IReadOnlyList<WorldCellGenerationTarget> targets,
    IReadOnlyList<CellExistingState> existingStates)
```

- existing state は identity 辞書（`StringComparer.Ordinal`）。重複 identity は上書きせず例外。
- `CellExistingState.Coordinate` は判定に使わない。**削除する。**
  既存の座標はフォルダ名パースから復元しない。座標が要る処理は target 側を見る。
- `CellPopulationEntry.Coordinate` は visual / Environment 配置用に残す（target からコピー）。
- `CellDeletionEntry` の正本は identity。座標フィールドは残さない
  （範囲外 existing はパースしないので座標が無い）。
- 範囲外 = **既存 identity が target 集合に無い。**
- `ShouldPopulateEnvironment(string identity)` / `IsDeletable(string identity)` に統一する。
  座標 overload は残さない。

### 3.4 CellAuthoringPolicy — 文字列だけ

```csharp
CellAuthoringPolicy.Resolve(string identity)
```

- 南辺は `"Cell_0_0"` / `"Cell_1_0"` / `"Cell_2_0"` / `"Cell_3_0"` の文字列。
- `Resolve(int, int)` / `Resolve(Vector2Int)` は **削除**。
- identity から座標をパースして policy を引かない。
- 未登録 identity は `Generated`。比較は `StringComparer.Ordinal`
  （`Cell_0_0` と `cell_0_0` は別キー。後者は未登録 → Generated）。

### 3.5 Creator 経路の置換表（部分解禁止）

「同じ列を渡す」対象はここである。所属判定が名前文法のままだと arbitrary identity が落ちる。
`CellPopulationPlan` だけ直して Creator が `IsCellId` のまま、は不合格。

| 経路 | 今の判定 | M-2 の判定 |
|---|---|---|
| `RelinkWorldChildren` | `CellIdentity.IsCellId` | target identity 集合 |
| `RelinkMapFromDisk` | `IsCellId` \|\| `IsEnvironmentId` | Cell は target 所属。Environment は S-4 まで `IsEnvironmentId` でよい |
| `RegisterCellAddressables` | `IsCellId(fileName)` | target の identity（フォルダ名 / ファイル名） |
| `RegisterEnvironmentAddressables` | `IsEnvironmentId` | S-4 まで現状維持と明記。M-2 では触らない |
| `DeleteOutOfGridCellFolders` | `TryParse` + `definition.Contains` | フォルダ名 = identity。削除計画の identity 集合 |
| `CollectExistingStates` | `TryParse(folderName)` 失敗で捨てる | フォルダ名を identity として採用。パース失敗で捨てない |
| `SyncSceneGraph` | `Format(x, y)` | `target.Identity` |
| `CreateEnvironmentSprouts` の所属と cellId | 南辺座標 + `Format` | 南辺 identity 集合。cellId は集合の文字列。座標は **一致する target.Coordinate**。Cell 側を Format しない |
| `Relink*` / `RegisterCellAddressables` / `SyncSceneGraph` / 削除の引数 | `definition` と座標 membership | **targets または target identity 集合**を受け取る。`definition.Contains` で所属を見ない |

SceneGraph の孤立ノード掃除・Map 除去も、収集済み identity 集合を正とし
`TryParse` / `Format` でフォルダを組み立て直さない。

`DeleteOutOfGridCellFolders` 内の Environment 掃除も Format しない。
削除フォルダ内の全 SceneResource identity を集めれば `Environment_*` は一緒に落ちる。

### 3.6 Environment 収集と sprout

「座標から子名を再構築しない」の代替を固定する。

収集（`WorldCellExistingStateCollector`）:

- Cell scene は `{folder}/{folderName}.unity`（folderName = identity）。
- Environment 状態は次の **どちらか** で取る。両方見てよい。座標 Format は使わない。
  1. Cell の `SceneResource.Children`
  2. 同じフォルダ内で、**フォルダ名と一致しない** SceneResource
- フォルダ内の全 `.unity` を Environment とみなさない（Cell 本体を二重カウントする）。
- SceneResource が無い孤立 `.unity` は Environment 候補にしない。これは §3.6 の対象外 orphan として残し、名前からの推測で補完しない。

作成はまだ座標キーである。対象外にせず、衝突だけ閉じる。

- `EnsureEnvironmentResource` / `EnsureEnvironmentSceneFile` の子名は
  `EnvironmentIdentity.Format(x, y)` のまま（S-4）。
- `EnvironmentSproutCells` を座標配列から **identity 集合** へ変える。
  今の南辺だけ: `"Cell_0_0"` … `"Cell_3_0"`。
- ループは identity を回す。`cellId` はその文字列。配置座標は **targets から同じ identity を引いた `Coordinate`**。
  `CellIdentity.Format` で cellId を組み直さない。見つからない identity は sprout しない（例外にしない）。
- `ShouldPopulateEnvironment` も identity で見る。
- 同一座標の 2 identity がどちらも sprout すると子 identity `Environment_0_0` が衝突する。
  sprout を identity 集合にしたので、南辺 identity 以外は sprout しない。
- 同一座標・別 identity のテストは、sprout 集合に入る identity を **1 つまで** にする
  （両方 `"Cell_0_0"` 相当にしない。片方は arbitrary 名）。

`Cells/` 直下のサブフォルダはすべて Cell 作業単位の identity とみなす。
今の本番は `Cell_*` だけなので 4×4 は不変。未知フォルダは target に無く、
HandAuthored 文字列でもなければ削除計画に乗る（パース失敗で黙殺しない）。

`HandEditProbe` の `Format` は M-2 では触らない。所有者は S-4（命名）。ファイル移送は M-4。

### 3.7 AssetDatabase I/O の抜き先

`WorldCellStreamingSliceCreator` から M-2 が触る I/O を次へ抜く。

- `DependOnAll/Editor/Streaming/Cells/State/WorldCellExistingStateCollector.cs`
  - §3.5 / §3.6 の収集規則。
- `DependOnAll/Editor/Streaming/Cells/State/WorldCellFolderReconciler.cs`
  - plan の deletion identity と同名フォルダだけを削除候補にする。
  - 削除前にフォルダ内の全 SceneResource identity を収集する。
  - Map、World children、SceneGraph から収集済み identity を除去してからフォルダを削除する。
  - 保持判定、orphan pruning、再リンクで `CellIdentity.TryParse` / `EnvironmentIdentity.TryParse` をキーにしない。

Creator に残す責務（ここ以外を Creator に足さない）:

- Menu / `CreateFromBatch`（`BatchMethod` 文字列は変えない）
- パス定数と現行 4×4 の配線（`EnsureGridDefinition` 含む）
- オーケストレーション（collector → plan → generate → reconciler → sprout → relink → Addressables → Graph）
- Environment 子の作成手続き（命名は S-4 のまま）
- Cell / Environment シーンへの視覚書き込み

---

## 4. A-1〜A-4: 規模と配置

Phase A の baseline / HEAD 行数を保持し、Phase B の実装実数と再検証済み上限を分けて記録する。

| ファイル | Phase A baseline / HEAD | Phase B 実装実数 | 再検証済み上限 | 責務 / 判断 |
|---|---:|---:|---:|---|
| `Scripts/Editor/Streaming/WorldCellGenerationTarget.cs`（新規） | 0 | 78 | 90 | identity＋coordinate と入力検証。`FromGrid` が Format の唯一口。新責務は別ファイル |
| `Scripts/Editor/Streaming/WorldCellGenerator.cs` | 604 | 653 | 670 | target を計画・adoption へ通し、Editor 境界で Map compact / dictionary 再構築。6 型同居は維持（A-2 例外） |
| `DependOnAll/Editor/WorldCellStreamingSliceCreator.cs` | 1403 | 1212 | 1270 | I/O 抽出で縮小。§3.7 の残責務に限定 |
| `DependOnAll/Editor/Streaming/Cells/State/WorldCellExistingStateCollector.cs`（新規） | 0 | 196 | 220 | AssetDatabase から identity keyed state を読む。FW Editor に置かない |
| `DependOnAll/Editor/Streaming/Cells/State/WorldCellFolderReconciler.cs`（新規） | 0 | 200 | 320 | folder / Map / World / Graph の削除整合。Map 編集は FW Editor 境界へ委譲し、FW Editor に置かない |
| `DependOnAll/Editor/Cells/CellPopulationPlan.cs` | 290 | 209 | 330 | identity keyed の純計画。`CellGridSpec` 削除 |
| `DependOnAll/Editor/Cells/CellAuthoringPolicy.cs` | 59 | 37 | 55 | identity → policy の純関数 |
| `Tests/Editor/WorldCellGeneratorTests.cs` | 295 | 318 | 390 | arbitrary identity / duplicate / adoption。旧署名呼び出しを全置換 |
| `Tests/Editor/WorldCellGenerationIdentityTests.cs`（新規分割） | 0 | 230 | 390 | arbitrary identity の path / adoption / Skip / duplicate side-effect / target validation 受入 |
| `Tests/Editor/WorldCellExistingStateCollectorTests.cs`（新規分割） | 0 | 166 | 220 | Children / sibling resource / payload fallback / AuthoredRoot 収集受入 |
| `Tests/Editor/WorldCellFolderReconcilerTests.cs`（新規分割） | 0 | 197 | 240 | Graph / Map / World 参照除去後のフォルダ削除、保持リソースと辞書 compact 受入 |
| `Tests/Editor/CellPopulationPlanTests.cs` | 368 | 205 | 490 | 同座標複数 identity と既存受入。`Resolve(int,int)` 呼び出しを全置換 |

### A-2 `WorldCellGenerator` を分割しない例外

既に 500 行超で 6 型が同居している
（`WorldCellPlanAction` / `WorldCellGenerationEntry` / `WorldCellGenerationPlan` /
`WorldCellExistingState` / `WorldCellGenerationResult` / `WorldCellGenerator`）。

M-2 で分割しない。理由: M-4 がファイルごと SampleGame へ移す。今分割すると移送が二重になる。
M-2 は型をこのファイルへ足さない。target 列を通すだけ。Phase A 再検証で、復元した公開 XML と受入根拠を含む上限を 670 行へ暫定承認した。

Creator は既に 500 行超。触る責務（収集 / 削除）だけ 2 ファイルへ抜く。
残る全面分割は別スライス。M-2 で新しい責務を Creator に積まない。

### A-3 新責務

あり。`WorldCellGenerationTarget`（列の正本）、collector、reconciler。
置き場は既存の `DependOnAll/Editor` 直下へ新規 C# を足さない契約に従う。

### A-4 テスト

§6。既存受入を identity API へ移し、同座標複数 identity / duplicate / Ordinal / arbitrary を追加。

フォルダ規則:

- 状態収集・削除は `Editor/Streaming/Cells/State/` に置く。
- Phase C は変更前後の各対象ディレクトリ直下 `.cs` 数を記録し、フラットな root の増加を構造指摘にする。

### Phase A 再検証記録

Phase A の規範追加（§3.6 の Environment 候補を Children / 同一フォルダの非 Cell
SceneResource に限定する規則、§4 の責務分割と実装実数欄、§6 の Phase C 検証順序の拡張）を
Phase A 契約として再承認した。A-2 の型同居は M-4 まで維持し、復元した根拠コメントを含む暫定上限を
`WorldCellGenerator.cs` 670 行、`WorldCellStreamingSliceCreator.cs` 1270 行とする。

---

## 5. 受け入れ条件

1. policy、existing state、Skip、Populate、Environment sprout 所属、削除、Graph / Map / World 再リンク、Addressables の主キーが identity 文字列である。
2. `ComputePlan` / `Generate` / adoption / Relink / Addressables / 削除が、単一の target 列以外から Cell identity を組み立てない。
3. `grep CellIdentity.Format` が生成器内部（`WorldCellGenerator` / `CellPopulationPlan` / Creator の Cell 経路 / collector / reconciler）で 0。例外は `WorldCellGenerationTarget.FromGrid` の 1 箇所。`EnvironmentIdentity.Format` と `HandEditProbe` は対象外（§2）。
4. 同一座標の 2 identity が別 target / existing / population entry。policy / AuthoredRoot / 削除が独立。片方の状態が他方へ伝播しない。
5. `StringComparer.Ordinal` で `Cell_0_0` と `cell_0_0` は別キー。
6. target / existing / `FromMap` の identity 重複は例外。`HashSet` 黙殺は禁止。
7. 現行無修飾 4×4 の target、生成 path、南辺 HandAuthored、Environment Populate/Skip、範囲外保護が従来と同じ。本番 Scene / asset / Addressables に差分がない。
8. FW に季節語を追加しない。factory / `CellScene` ctor を変えない。
9. 対象テストと全 EditMode 回帰が 1 件以上実行、failed 0。docs audit が成功。

---

## 6. テスト要求

既存 `CellPopulationPlanTests` の全受入を identity API へ移し、次を追加する。

- 同一座標・別 identity の 2 件が別 PopulationEntry になる。sprout 集合に入るのは高々 1 件。
- 一方だけ AuthoredRoot 有りでも、他方の action は変わらない。
- target と同一座標だが別 identity の existing state は target の既存扱いにならない。
- target / existing の duplicate identity が例外。
- `Cell_0_0` と大小文字違いを別キーにする。
- arbitrary identity の policy が parser なしで解決される（未登録 → Generated）。
- deletion / `ShouldPopulateEnvironment` / `IsDeletable` が identity 単位。
- パース不能なフォルダ名の existing が捨てられず、target に無ければ削除候補になる
  （HandAuthored 文字列に一致しなければ削除可）。

`WorldCellGeneratorTests` には次を追加する。

- arbitrary identity の plan / scene path / resource path が target identity から作られる。
- `FromMap` が名前文法ではなく target membership で収集する。
- arbitrary identity の既存 asset adoption と Skip が成立する。
- duplicate target が副作用前に失敗する。
- 旧署名 `ComputePlan(definition, existingState)` が存在しない（コンパイルできない）。

Phase C の順序:

1. 構造レビューと target grep（§5.3 の Format 0、§3.5 の `IsCellId` 残存）
2. `pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.CellPopulationPlanTests`
3. `pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.WorldCellGeneratorTests`
4. `pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.WorldCellGenerationIdentityTests`
5. `pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.WorldCellExistingStateCollectorTests`
6. `pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.WorldCellFolderReconcilerTests`
7. `pwsh tools/docs-audit.ps1`
8. `pwsh tools/run-tests.ps1`（全 EditMode）

exit 0 でも total 0 は失敗。`0xC0000005` は完成済み XML とログ末尾で判定する。

---

## 6.1 実装制約

- 新規・編集する Unity C# は先頭に `#nullable enable`。
- Unity C# で `record` を使わない。
- 破棄されうる `UnityEngine.Object` は `== null` / `!= null`。`?.` / `??` / `is null` / `ReferenceEquals` を使わない。
- Editor コードを Runtime asmdef に置かない。asmdef 参照を追加しない。
- `SceneState` の 14 値を減らさず、並べ替えない。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。
- `.unity` / `.prefab` / `.asset` YAML を手編集しない。
- HANDOFF とコードが衝突する、新しい設計判断が必要、または現行 4×4 の asset 変更が必要なら Phase B を止める。

---

## 6.2 モデル運用（このキュー。将来の固定表ではない）

担当は Phase 開始時に選ぶ。モデル名を恒久割り当てとして複製しない。

今回の Phase C は人間が **Grok 4.6** を選定した。M-2 / M-3 / M-4 で Phase C を始めるとき、この選定を引き継いでよい。実績欄には使ったモデルを書く。

- Phase B: 開始時に選ぶ。C と異なるモデル。
- Phase C: **Grok 4.6**。新規セッション。構造レビューとテスト。
- Phase C': 開始時に選ぶ。C と異なるモデル。可能なら異なる系列またはベンダー。C が Grok 4.6（xAI）なので C' に Grok 系列を使わない。条件を満たせない場合は独立監査済みと書かない。
- writer は常に 1 名。構造レビューとテストだけを並列化してよい。
- Phase C の指摘は Phase B 担当へ戻し、修正後に C 全体を再実行する。C が指摘 0 になってから C' を行い、C' 指摘後も C からやり直す。
- 同じ prompt / CLI command を無応答だけを理由に再送しない。起動時刻、phase、branch、最後の出力を記録し、status / log / cursor を確認する。中断して別セッションへ切り替える場合も writer を重複させない。

---

## 6.3 Phase B 実績（実装セッションが記入）

### Phase B

- 担当 / モデル: Codex Phase B writer / GPT-5.6 Luna
- 実装結果: `WorldCellGenerationTarget` を新設し、`FromGrid` を Cell identity の唯一の Format 入口とした。`WorldCellGenerator` の ComputePlan / Generate / FromMap / adoption を target 列必須へ変更し、`CellPopulationPlan` / `CellAuthoringPolicy` を Ordinal identity keyed へ移行した。Creator の収集・削除 I/O を `Streaming/Cells/State/WorldCellExistingStateCollector.cs` と `WorldCellFolderReconciler.cs` へ抽出し、SceneGraph、World children、Map、Cell Addressables、Environment sprout の所属判定へ同じ target identity 列を渡すよう変更した。EditMode テストを新 API へ移行し、arbitrary identity、同座標複数 identity、duplicate、Ordinal、adoption/FromMap/Skip、identity deletion を追加した。Phase C 指摘に対応し、reconciler は Graph / World children の参照除去後に `WorldCellGenerator.RemoveSceneResourcesFromMap`（OneStarMaker.Editor 境界）で Map を compact / dictionary 再構築し、その後フォルダを削除する。Runtime の `SceneResourceMap.RebuildDictionary` は internal のままで、Runtime 公開 API は拡張していない。全新規 C# とフォルダの `.meta` を追加した。再レビュー指摘には、永続化した parent/resource を `AssetDatabase` で cleanup し、Refresh 後に path から再読込して identity / parent / Map / Skip を検証する adoption → Generate → Skip テストと、範囲外 Environment false テストを追加した。C' 指摘には、Environment 候補を Cell.Children または同一フォルダの非 Cell SceneResource に限定し、無関係な `.unity` を無視する collector と専用受入テストを追加した。追加指摘には、collector の target identity 重複・default target 検証、reconciler の参照除去順を検証する reflection テスト、保持リソース付き compact / dictionary 検証、Generate の duplicate target side-effect 無し検証、payload 不在 / dangling 時の identity path fallback、任意 identity の AuthoredRoot 収集、Environment sprout の座標重複検証を加えた。reconciler の受入は test-only hook を廃し、参照除去段階とフォルダ削除段階を分離して順序を検証する。実装後の行数は `WorldCellGenerationTarget.cs` 78、`WorldCellGenerator.cs` 653、Creator 1212、collector 196、reconciler 200、`CellPopulationPlan.cs` 209、`CellAuthoringPolicy.cs` 37、`WorldCellGeneratorTests.cs` 318、`WorldCellGenerationIdentityTests.cs` 230、`WorldCellExistingStateCollectorTests.cs` 166、`WorldCellFolderReconcilerTests.cs` 197、`CellPopulationPlanTests.cs` 205。
- Phase C 引継ぎ用の対象ディレクトリ直下 `.cs` 数（変更前→変更後）は、`Scripts/Editor/Streaming` 2→3、`Tests/Editor` 2→5、`DependOnAll/Editor` 5→5、`DependOnAll/Editor/Streaming/Cells/State` 0→2。既存 root の増加は target と受入テスト分割のみで、State は新責務配置どおり。
- 実行しなかった事項: Phase B 制約に従い Unity Editor 起動・接続、`unity test` / `unity run`、`pwsh tools/run-tests.ps1`、Addressables build、`pwsh tools/docs-audit.ps1` は実行していない。静的確認として `git diff --check` と対象経路の `CellIdentity.Format` / `TryParse` / `IsCellId` grep を実施した。補助的な `dotnet build` は Unity 生成 csproj の Windows SDK パス権限エラーでコンパイルに到達しなかった。
- HANDOFF との差異: C' 指摘修正を反映済み。Phase C は修正後の構造レビュー、対象テスト、docs audit、全 EditMode 回帰を再実行する。

---

## 7. Phase C 実績（レビュー／テストセッションが記入）

- 担当 / モデル: Cursor Agent 新規セッション / Grok 4.6
- 構造レビュー: 指摘 0。Runtime 公開 API 拡張なし、Editor 境界での Map compact / dictionary 再構築、保持リソース付き再読込、全 target 入口の検証、collector source 規則、削除順、行数予算を確認。
- 対象テスト: `CellPopulationPlanTests` 13/13、`WorldCellGeneratorTests` 8/8、`WorldCellGenerationIdentityTests` 9/9、`WorldCellExistingStateCollectorTests` 2/2、`WorldCellFolderReconcilerTests` 1/1。全て total > 0、failed 0。先頭フィルタの Unity exit は `0xC0000005` だったが、完成 XML により成功判定。
- 全 EditMode / docs audit: 517/517 pass、docs audit 50 Markdown / 11048 行、違反 0。§7 / §8 記入済みの harvest 警告 1 件は仕様どおりで exit 0。
- 未確認事項: Addressables build と本番 asset 生成は対象外のため未実行。
- 結論: 修正後の新規 Grok 4.6 セッションで Phase C PASS。ブロッキング指摘 0。Phase C' 再実行待ち。

---

## 8. Phase C' 実績（独立監査セッションが記入）

- 担当 / モデル: Claude CLI 新規セッション / `claude-opus-5`（Claude Opus 5、Anthropic）。Phase B の GPT-5.6 Luna、Phase C の Grok 4.6 と異なるモデル・系列・ベンダーで、会話履歴を使わず独立性を充足。
- 監査結果: **PASS**。受け入れ条件 1–9、旧 C' 指摘の修正、Phase A baseline / 実装実数 / 暫定上限、Runtime 公開境界、identity / Ordinal / duplicate / default target / collector / Map compact / deletion / adoption、meta / 行数 / 対象外を一次資料から再検証。ブロッキング指摘 0。
- テスト証拠: C' は Unity を起動せず、Phase C の XML を直接確認して 13/13、8/8、9/9、2/2、1/1、全 517/517 failed 0 と、実装・テストの更新時刻が実行前であることを確認。docs audit は C' が読み取り専用で再実行し、50 Markdown、検査1・2違反 0、harvest 警告 1、exit 0 を再現。
- 非ブロッキング指摘: Creator の一部メンバー間空行、collector の説明コメント位置、Creator クラス doc の一部設計注記、reconciler のテストシーム説明、Generator 公開 XML の一部説明密度。既存由来の Creator 偽 null は M-2 対象外。
- 残存リスク: グリッド縮小時、保持 HandAuthored セルが Map / Graph に残りつつ World children から外れる相互作用と、保持セルの Cell Addressables 登録範囲は未受入。現行 4×4 では到達せず、M-3 以降へ引き継ぐ。Addressables build、本番 asset 生成、Play 経路は対象外で未検証。
- 結論: **Phase C' PASS**。M-2 はローカル commit 可能。Claude 側のリポジトリ編集は無い。
