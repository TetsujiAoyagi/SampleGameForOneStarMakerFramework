# Streaming 空間政策 M-4 HANDOFF — セル型を SampleGame へ

> ステータス: **Phase A 完了・Phase B 未着手。M-3 の後に着手する。S-4 と同時可。**
> 上位計画: [STREAMING_SPATIAL_MIGRATION.md](STREAMING_SPATIAL_MIGRATION.md)
> 到着契約: [§34 OnDemand の空間政策](../../unity/Assets/Docs/Architecture/34-ondemand-spatial-policy.md)
> 現状仕様: [STREAMING_CURRENT_SPEC.md](../streaming/STREAMING_CURRENT_SPEC.md)
> harvest 先: 所在は `STREAMING_CURRENT_SPEC.md`。マージ時に本書を削除する。

問 5b は移行 HANDOFF で閉じた。セル固有型を SampleGame へ下ろす。
FW 逆参照を作らない。本番セルは動かさない。

---

## 1. 目的

`Runtime/SceneSystem/Cells/` と生成器入力の格子型を FW 公開面から下ろす。
`.cs` と `.meta` を一緒に移し GUID を維持する。
`GameSceneFactory` の `IsCellId` 分岐は参照先だけ変わり、挙動は同じ。

---

## 2. 対象外

- factory の `IsCellId` 廃止（S-4）
- `CellScene` ctor の `Cell_{x}_{y}` 要求の緩和（S-4）
- `HandEditProbe` の Format 意味の変更（S-4。ファイルは一緒に移す）
- Environment 子の命名変更
- 本番 Scene / Prefab / Addressables / `WorldGridDefinition.asset` の YAML 手編集
- テスト asmdef の新設・参照追加
- Creator の namespace 変更と `BatchMethod` 文字列の変更
- `World/Cells/` への C# 配置
- `WorldCellGenerator.cs` の 6 型分割（M-2 の A-2 例外を維持。ファイルごと移す）

Phase B は移動と using / namespace / ZString 置換と HANDOFF 実績欄。
Unity.exe / `run-tests.ps1` / Addressables ビルドを実行しない。

---

## 3. 決定

### 3.1 置き場

```text
SampleGame/InGame/InGameSession/World/CellScenes/
  CellIdentity / CellGridConfig / CellScene
  DemoCellScene / EnvironmentIdentity / EnvironmentScene

SampleGame/DependOnAll/Editor/Streaming/Cells/
  WorldCellStreamingSliceCreator / HandEditProbe
  Generation/  WorldCellGenerationTarget / WorldGridDefinition / WorldCellGenerator
  Planning/    CellAuthoringPolicy / CellPopulationPlan
  State/       WorldCellExistingStateCollector / WorldCellFolderReconciler
```

- `World/Cells/` に C# を置かない（生成済みセル資産の隣にコードを混ぜない）。
- **フォルダは動かす。Creator の namespace は `SampleGame.DependOnAll.Editor` のまま。**
  `BatchMethod` =
  `"SampleGame.DependOnAll.Editor.WorldCellStreamingSliceCreator.CreateFromBatch"`
  を変えない。
- Runtime 型の namespace は `SampleGame.InGame.World` または既存の
  `SampleGame.InGame.Streaming` に寄せる。新しい公開契約を増やさない。
- Editor 生成器型は `SampleGame.DependOnAll.Editor.Streaming.Cells` 配下でよい
  （Creator 本体の namespace だけ固定）。

### 3.2 参照と grep

- 本番の Game→FW 逆参照を足さない。
- テスト asmdef は既に SampleGame を指している
  （`OneStarMaker.Tests` → `SampleGame.InGame`、
  `Tests.Editor` → `SampleGame.DependOnAll.Editor`）。
  using を SampleGame に向け直せば参照追加は不要。
- 静的受入:
  - 型定義（`class CellIdentity` 等）が `unity/Assets/OneStarMaker/Scripts/` に 0
  - Tests は SampleGame の型を using してよい
  - `SceneDirectorTestBase` の `CellIdentity.Format` はリテラル `"Cell_0_0"` にするか、
    SampleGame の Format を使う

### 3.3 CellRect / アセット / メニュー

- `CellRect` の統一先は **`WorldCellCatalog` 側（`SampleGame.InGame.Streaming`）**。
  Editor に 3 個目を作らない。
- `WorldGridDefinition.Rectangles` の戻り型名前空間は変わる。
  serialized field は `SerializedCellRect` のまま。
- `WorldGridDefinition.asset` のロードテストは `EnsureGridDefinition` を通さない。
- `CreateAssetMenu` は `OneStarMaker/Streaming/World Grid Definition` から
  `SampleGame/Streaming/World Grid Definition` へ直す（FW 語彙を残さない）。

### 3.4 CellIdentity.Format

`SampleGame.InGame` は ZString.dll を参照していない。
`ZString.Format` を `string.Concat`（または同等）へ置換する。asmdef に ZString を足さない。

---

## 4. A-1〜A-4

行数は移動なのでほぼ不変。上限は「移動後にロジックを足さない」。

| ファイル | 現在 | 予想上限 | 責務 / 判断 |
|---|---:|---:|---|
| `Scripts/Runtime/SceneSystem/Cells/CellIdentity.cs` | 102 | 110 | SampleGame へ移動 + ZString 除去 |
| `Scripts/Runtime/SceneSystem/Cells/CellGridConfig.cs` | 33 | 33 | 移動のみ |
| `Scripts/Runtime/SceneSystem/Cells/CellScene.cs` | 50 | 50 | 移動のみ |
| `Scripts/Editor/Streaming/WorldCellGenerator.cs` | 604 | 625 | ファイルごと移動。分割しない |
| `Scripts/Editor/Streaming/WorldGridDefinition.cs` | 189 | 200 | 移動 + `CellRect` 削除 + メニューパス |
| `Scripts/Editor/Streaming/WorldCellGenerationTarget.cs` | M-2 新設 | 90 | generator と一緒に移動 |
| `InGame/.../Streaming/WorldCellCatalog.cs` | 216 | 216 | `CellRect` を唯一の定義として残す |
| `DependOnAll/Editor/WorldCellStreamingSliceCreator.cs` | M-2 後 | 1180 | フォルダ移動可。namespace / `BatchMethod` 不変 |
| `DependOnAll/GameSceneFactory.cs` | 76 | 76 | using のみ |
| `Tests/Scene/SceneDirectorTestBase.cs` | 200 | 210 | Format → リテラルまたは SampleGame |
| `Tests/Streaming/WorldCellCatalogTests.cs` | （既存） | +40 | asset を `EnsureGridDefinition` なしでロード |

A-2: generator 604 行は分割しない（M-2 と同じ例外。今分割すると移送が二重）。
A-3: 新責務なし。置き場の選定理由は §3.1（生成済み `World/Cells/` とフラット root を避ける）。
A-4: §6。既存テストの using 修正が主。asset ロード 1 本を追加。

---

## 5. 受け入れ条件

1. `OneStarMaker/Scripts` に `CellIdentity` / `CellGridConfig` / `CellScene` /
   `WorldCellGenerator` / `WorldGridDefinition` の型定義が 0。
2. `WorldGridDefinition.asset` が GUID 維持のまま新型でロードできる
   （`EnsureGridDefinition` を通さない）。
3. factory の `IsCellId` 分岐は参照先だけ変わり、挙動は同じ。
4. Creator の `BatchMethod` 文字列が生きている。
5. `CellRect` 定義は `SampleGame.InGame.Streaming` に 1 つ。
6. `CellIdentity.Format` が ZString を使わない。
7. 本番 Scene / asset / Addressables に差分がない（`.meta` GUID は維持）。
8. テスト asmdef を新設しない。本番の Game→FW 逆参照を足さない。

---

## 6. テスト要求

- 既存 EditMode の using を SampleGame に向け、コンパイルと挙動を維持する。
- `WorldGridDefinition.asset` を AssetDatabase から直接ロードし、
  origin / cellSize / rectangles / output path が Catalog 上書き前の値と一致することを証明する。
- factory が `Cell_0_0` で `DemoCellScene` を返す既存テストがあれば残す（挙動不変）。

Phase C: 構造レビュー（`.cs` 数、namespace、`BatchMethod` grep）→
対象テスト → `docs-audit.ps1` → 全 EditMode。

---

## 6.1 実装制約

M-2 HANDOFF §6.1 と同じ。GUID を落とさない。YAML 手編集しない。
衝突したら Phase B を止める。

---

## 6.2 モデル運用

移行 HANDOFF に従う。Phase C は **Grok 4.6**。writer 1 名。無応答だけで再送しない。
C' に Grok 系列を使わない。満たせなければ独立監査済みと書かない。

---

## 6.3 Phase B 実績

- 担当 / モデル:
- 実装結果:
- 実行しなかった事項:
- HANDOFF との差異:

---

## 7. Phase C 実績

未着手

---

## 8. Phase C' 実績

未着手
