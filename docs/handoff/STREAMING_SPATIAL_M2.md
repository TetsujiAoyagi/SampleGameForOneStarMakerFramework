# Streaming 空間政策 M-2 HANDOFF — 生成器の identity 主キー化

> ステータス: **Phase A 完了・Phase B 未着手。**
> ブランチ: `codex/streaming-spatial-m2`（`develop` 起点）
> 上位計画: [STREAMING_SPATIAL_MIGRATION.md](STREAMING_SPATIAL_MIGRATION.md)
> 到着契約: [§34 OnDemand の空間政策](../../unity/Assets/Docs/Architecture/34-ondemand-spatial-policy.md)
> 現状仕様: [STREAMING_CURRENT_SPEC.md](../streaming/STREAMING_CURRENT_SPEC.md)
> harvest 先: 実装値は `STREAMING_CURRENT_SPEC.md`、恒久契約の追加があれば §34。マージ時に本書を削除する。

本書だけで M-2 を実装できるよう、対象・境界・署名・受入・検証を固定する。
上位計画に未決と書かれた M-2 問7は、本書の決定で閉じる。

---

## 1. 目的

現行 4×4 の生成結果と HandAuthored 保護を変えず、WorldCell 生成経路の主キーを
`Vector2Int` から不透明な identity 文字列へ変更する。

`(identity, coordinate)` の対象列を一度だけ構築し、policy、既存収集、生成計画、
既存 asset adoption、SceneGraph、Map / World children、Addressables、範囲外削除へ
同じ identity を流す。座標は見た目と格子配置を焼くためのメタデータとしてだけ残す。

このスライスで、同じ座標を複数 identity が占めても辞書上書きや削除判定で潰れない口を作る。

---

## 2. 対象外

- 9×6、`Season_*`、修飾付き Environment、トンネル、既存16セルの移動・全廃
- `CellIdentity.TryParse` の修飾対応、`StreamingConfig.cellIdQualifier`、identity 翻訳層
- M-3 の R-3 変更、M-4 の型・ファイル移送、S-4 の factory 結線変更
- Scene / Prefab / SceneResource / Addressables asset の生成・書き換え
- asmdef 参照の追加、§21 / §33 / §5 の全面改稿
- M-2 と無関係な `WorldCellStreamingSliceCreator` の分割

Phase B は C# と HANDOFF の実績欄だけを変更する。Unity Editor、`unity test`、
`unity run`、`pwsh tools/run-tests.ps1`、Addressables ビルドを実行しない。

---

## 3. 決定した API とデータフロー

### 3.1 対象列

`OneStarMaker.Editor.Streaming` に `WorldCellGenerationTarget.cs` を新設する。

```csharp
public readonly struct WorldCellGenerationTarget
{
    public WorldCellGenerationTarget(string identity, Vector2Int coordinate);
    public string Identity { get; }
    public Vector2Int Coordinate { get; }
}
```

- identity は null / 空白、`/`、`\` を拒否する。
- target 列の重複 identity は `StringComparer.Ordinal` で検出し、処理開始前に例外にする。
- 同一 coordinate の別 identity は許可する。
- 現行 M-2 は `WorldGridDefinition.EnumerateCells()` と `CellIdentity.Format` から対象列を一度だけ作る。
  `Format` を呼んでよいのはこの現行レイアウト境界だけである。
- この型と generator は M-4 で SampleGame Editor へ一緒に移す。

### 3.2 WorldCellGenerator

次の入口へ同じ target 列を追加する。

```csharp
WorldCellGenerator.ComputePlan(definition, targets, existingState)
WorldCellGenerator.Generate(definition, targets, map, parentResource)
WorldCellExistingState.FromMap(map, targets)
```

- `ComputePlan`、出力フォルダ、SceneResource identity、Skip 判定は target.Identity を使う。
- `FromMap` は `CellIdentity.IsCellId` で選別せず、target identity と完全一致する Map entry だけを集める。
- `AdoptExistingResourceAssets` は target 列の identity / path を使い、内部で `CellIdentity.Format` しない。
- target に無い SceneResource を `AllCellResources` へ混ぜない。
- `ApplyPlan` / `ApplySceneFiles` は plan entry を正とし、追加の名前解析を行わない。

### 3.3 CellPopulationPlan と policy

`CellGridSpec` は廃止し、次の入口へ置換する。

```csharp
CellPopulationPlan.Compute(
    IReadOnlyList<WorldCellGenerationTarget> targets,
    IReadOnlyList<CellExistingState> existingStates)
```

- existing state は identity 辞書へ入れる。重複 identity は黙って上書きせず例外にする。
- `CellExistingState` の coordinate は判定に使わない。不要なら削除する。
- `CellPopulationEntry.Coordinate` は visual / Environment の配置用に残す。
- `CellDeletionEntry` は identity を削除判断の正本にする。
- `CellAuthoringPolicy` は現在の南辺4件を identity 文字列で保持し、
  `Resolve(string identity)` だけを公開する。未登録 identity は `Generated`。
- `ShouldPopulateEnvironment(string identity)`、`IsDeletable(string identity)` に統一し、座標 overload は残さない。

### 3.4 AssetDatabase I/O と削除

`WorldCellStreamingSliceCreator` から M-2 が触る I/O を次へ抜く。

- `DependOnAll/Editor/Streaming/Cells/State/WorldCellExistingStateCollector.cs`
  - target identity と同名のフォルダを完全一致で収集する。
  - Cell scene は `{folder}/{identity}.unity` で引く。
  - Environment 状態は同じフォルダ内の scene / SceneResource を探索し、座標から子名を再構築しない。
- `DependOnAll/Editor/Streaming/Cells/State/WorldCellFolderReconciler.cs`
  - plan の deletion identity と同名フォルダだけを削除候補にする。
  - 削除前にフォルダ内の全 SceneResource identity を収集する。
  - Map、World children、SceneGraph から収集済み identity を除去してからフォルダを削除する。
  - 保持判定、orphan pruning、再リンクで `CellIdentity.TryParse` / `EnvironmentIdentity.TryParse` をキーにしない。

Creator は orchestration と現行4×4の target 構築を担当し、収集・削除アルゴリズムを持たない。
SceneGraph の Cell node、World children、セル Addressables 登録は target identity 集合で選ぶ。
現行 Environment の生成文法そのものは S-4 所有であり、このスライスでは変更しない。

---

## 4. A-1〜A-4: 規模と配置

予想行数は上限。実装後は実数と責務を本書へ追記する。

| ファイル | 現在 | 予想上限 | 責務 / 判断 |
|---|---:|---:|---|
| `Scripts/Editor/Streaming/WorldCellGenerationTarget.cs`（新規） | 0 | 90 | identity＋coordinate と入力検証。新責務は別ファイル |
| `Scripts/Editor/Streaming/WorldCellGenerator.cs` | 604 | 625 | target を計画・adoptionへ通す。既存2責務を増やさない |
| `DependOnAll/Editor/WorldCellStreamingSliceCreator.cs` | 1403 | 1180 | I/O 抽出で縮小。orchestration / 現行レイアウト配線に限定 |
| `Editor/Streaming/Cells/State/WorldCellExistingStateCollector.cs`（新規） | 0 | 220 | AssetDatabase から identity keyed state を読む |
| `Editor/Streaming/Cells/State/WorldCellFolderReconciler.cs`（新規） | 0 | 320 | folder / Map / World / Graph の削除整合 |
| `Editor/Cells/CellPopulationPlan.cs` | 290 | 330 | identity keyed の純計画 |
| `Editor/Cells/CellAuthoringPolicy.cs` | 59 | 55 | identity → policy の純関数 |
| `Tests/Editor/WorldCellGeneratorTests.cs` | 295 | 390 | arbitrary identity / duplicate / adoption |
| `Tests/Editor/CellPopulationPlanTests.cs` | 368 | 490 | 同座標複数 identity と既存受入 |

`WorldCellGenerator` と Creator は既に 500 行超である。M-2 は前者へ型を追加せず、後者から
触る責務を2ファイルへ抜く。残る Creator の全面分割は別スライスとし、M-2 で新しい責務を積まない。

フォルダ規則:

- 既存の `DependOnAll/Editor` 直下へ新規 C# を足さない。
- 状態収集・削除は `Editor/Streaming/Cells/State/` に置く。
- Phase C は変更前後の各対象ディレクトリ直下 `.cs` 数を記録し、フラットな root の増加を構造指摘にする。

---

## 5. 受け入れ条件

1. policy、existing state、Skip、Populate、Environment、削除、Graph / Map / World 再リンクの主キーが identity 文字列である。
2. `WorldCellGenerator` が target identity を受け、計画・出力 path・adoption で `CellIdentity.Format` / `IsCellId` を使わない。
3. 同一座標の複数 identity が別 target / existing state / population entry として共存し、片方の状態が他方へ伝播しない。
4. duplicate target / existing identity は例外。大小文字差は Ordinal で別 identity。
5. 現行無修飾4×4の target、生成 path、HandAuthored 南辺、Environment Populate/Skip、範囲外保護が従来と同じ。
6. 本番 Scene / asset / Addressables 設定に差分がない。FW に季節語を追加しない。
7. 対象テストと全 EditMode 回帰が1件以上実行、failed 0。docs audit が成功。

---

## 6. テスト要求

既存 `CellPopulationPlanTests` の全受入を identity API へ移し、次を追加する。

- 同一座標・別 identity の2件が別 PopulationEntry になる。
- 一方だけ AuthoredRoot 有りでも、他方の action は変わらない。
- target と同一座標だが別 identity の existing state は target の既存扱いにならない。
- target / existing の duplicate identity が例外。
- `Cell_0_0` と大小文字違いを別キーにする。
- arbitrary identity の policy が parser なしで解決される。
- deletion、`ShouldPopulateEnvironment`、`IsDeletable` が identity 単位。

`WorldCellGeneratorTests` には次を追加する。

- arbitrary identity の plan / scene path / resource path が target identity から作られる。
- `FromMap` が名前文法ではなく target membership で収集する。
- arbitrary identity の既存 asset adoption と Skip が成立する。
- duplicate target が副作用前に失敗する。

Phase C の順序:

1. 構造レビューと target grep
2. `pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.CellPopulationPlanTests`
3. `pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.WorldCellGeneratorTests`
4. `pwsh tools/docs-audit.ps1`
5. `pwsh tools/run-tests.ps1`（全 EditMode）

exit 0 でも total 0 は失敗。`0xC0000005` は完成済み XML とログ末尾で判定する。

---

### 6.1 実装制約

- 新規・編集する Unity C# は先頭に `#nullable enable`。
- Unity C# で `record` を使わない。
- 破棄されうる `UnityEngine.Object` は `== null` / `!= null`。`?.` / `??` / `is null` / `ReferenceEquals` を使わない。
- Editor コードを Runtime asmdef に置かない。asmdef 参照を追加しない。
- `SceneState` の14値を減らさず、並べ替えない。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。
- `.unity` / `.prefab` / `.asset` YAML を手編集しない。
- HANDOFF とコードが衝突する、新しい設計判断が必要、または現行4×4の asset 変更が必要なら Phase B を止める。

---

### 6.2 モデル分離と反復

- Phase B 実装・修正: `gpt-5.6-luna`
- Phase C 構造／機能レビューとテスト: `gpt-5.6-terra` の新規セッション
- Phase C' 独立監査: `gpt-5.5` の新規セッション
- 最終統合チェック: 親セッション

writer は常に1名。構造レビューとテストだけを並列化してよい。
Phase C の指摘は Luna へ戻し、修正後に Phase C 全体を再実行する。
C が指摘0になってから C' を行い、C' 指摘後も C からやり直す。

同じ prompt / CLI command を無応答だけを理由に再送しない。起動時刻、phase、branch、最後の出力を記録し、
status / log / cursor を確認する。一度中断して別セッションへ切り替える場合も writer を重複させない。

---

### 6.3 Phase B 実績（実装セッションが記入）

### Phase B

- 担当 / モデル:
- 実装結果:
- 実行しなかった事項:
- HANDOFF との差異:

---

## 7. Phase C 実績（レビュー／テストセッションが記入）

未着手

---

## 8. Phase C' 実績（独立監査セッションが記入）

未着手
