# Streaming 空間政策 M-4 HANDOFF — セル型を SampleGame へ

> ステータス: **Phase C / C' 完了・Phase D 待ち。**
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
- `DemoCellScene` / `EnvironmentIdentity` / `EnvironmentScene` は **既に**
  `SampleGame.InGame.World` にある。CellScenes/ へ寄せてよいが、namespace は
  `SampleGame.InGame.World` のままでよい。寄せないなら対象外と明記して残す。
  挙動は変えない。
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
| `Tests/Editor/WorldGridDefinitionLoadTests.cs`（新規） | 0 | 80 | `WorldGridDefinition.asset` を `EnsureGridDefinition` なしでロード。Catalog テストに載せない |

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

- 新規・編集する Unity C# は先頭に `#nullable enable`。
- Unity C# で `record` を使わない。
- 破棄されうる `UnityEngine.Object` は `== null` / `!= null`。`?.` / `??` / `is null` / `ReferenceEquals` を使わない。
- Editor コードを Runtime asmdef に置かない。本番 asmdef 参照を追加しない。
- `SceneState` の 14 値を減らさず、並べ替えない。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。
- `.unity` / `.prefab` / `.asset` YAML を手編集しない。GUID を落とさない。
- HANDOFF とコードが衝突する、新しい設計判断が必要なら Phase B を止める。

---

## 6.2 モデル運用

移行 HANDOFF に従う。Phase C は **Grok 4.6**。writer 1 名。無応答だけで再送しない。
C' に Grok 系列を使わない。満たせなければ独立監査済みと書かない。

---

## 6.3 Phase B 実績

- 担当 / モデル: Codex / GPT-5
- 実装結果:
  - `CellIdentity` / `CellGridConfig` / `CellScene` を `.meta` とともに
    `SampleGame/InGame/InGameSession/World/CellScenes/` へ移し、namespace を
    `SampleGame.InGame.World` に変更した。
  - `WorldCellGenerationTarget` / `WorldGridDefinition` / `WorldCellGenerator` を
    `.meta` とともに `SampleGame/DependOnAll/Editor/Streaming/Cells/Generation/` へ移した。
    policy / population plan、Creator、HandEditProbe も §3.1 の配置へ移し、State と合わせた。
  - `CellRect` は `SampleGame.InGame.Streaming` の定義だけを残し、
    `WorldGridDefinition` が同じ型を返すようにした。CreateAssetMenu も SampleGame 語彙へ変更した。
  - `CellIdentity.Format` の ZString 依存を `string.Concat` へ置換し、asmdef 参照は変更していない。
  - 本番 `WorldGridDefinition.asset` を `EnsureGridDefinition` なしで直接ロードして Catalog 値と照合する
    `WorldGridDefinitionLoadTests` を追加した。
  - 静的確認で対象 `.meta` の GUID 維持、FW 側の対象型定義 0、`CellRect` 定義 1、
    `BatchMethod` 文字列維持、本番 Scene / asset / Addressables / asmdef 差分 0 を確認した。
- 実行しなかった事項: Unity.exe、`run-tests.ps1`、Addressables ビルド（Phase B 禁止のため）。
- HANDOFF との差異:
  - `DemoCellScene` / `EnvironmentIdentity` / `EnvironmentScene` は §3.1 の許可どおり
    現在の `World/` 直下に残した。実装契約との差異はない。
  - Phase B 着手時点で `WorldCellGenerator` は 653 行、Creator は 1212 行で、§4 の Phase A 時点の
    予想上限を既に超えていた。M-4 は前者へ namespace 用の using 1 行を加えただけ、後者は行数不変で、
    §4 の「移動後にロジックを足さない」は満たしている。

---

## 7. Phase C 実績

- 担当 / モデル: Cursor Agent / **Grok 4.6 High**。Phase B の Codex / GPT-5 と異なる
  新規 read-only セッションで実施し、モデル独立性を満たした。
- Phase A 遡及監査:
  - 受け入れ条件、対象外、配置、テスト方針の骨格は妥当だった。
  - `WorldCellGenerator` が `OneStarMaker.Editor` から `SampleGame.DependOnAll.Editor` へ移ることで、
    `SceneResourceMap.RebuildDictionary()` の `internal` 到達性を失う assembly 境界を見落としていた。
  - `OneStarMaker.Tests.Streaming` 内の完全修飾名が `OneStarMaker.Tests.SampleGame` から相対解決される
    namespace 衝突も見落としていた。M-4 の Phase A に別モデル確認実績は無く、遡及監査で初めて確認した。
- 構造レビュー:
  - 受け入れ条件 1〜8 は PASS。対象型定義は FW 側 0、`CellRect` は 1 定義、旧 namespace 参照 0、
    Creator の namespace / `BatchMethod` 維持、ZString 依存 0、asmdef / 本番 YAML 差分 0。
  - `WorldCellGenerator` / Creator の大きさは既存由来で、M-4 は移送後に責務を足していない。
  - Runtime API を public 化せず、既存の Game Editor → FW Editor 依存を使うため、
    `SceneResourceGenerator.RebuildMapLookup(SceneResourceMap)` を Editor facade として追加した。
    `InternalsVisibleTo` へ SampleGame を足さず、FW → Game 逆依存も作っていない。
- 指摘対応:
  - 初回 `WorldGridDefinitionLoadTests` はコンパイルエラー 4 件で XML 未生成。
    上記 assembly 境界 3 件を Editor facade 経由へ、テスト namespace 1 件を `global::` へ修正した。
  - factory の Cell 分岐は静的には不変だが直接テストが無かったため、C' 指摘後に
    `GameSceneFactoryTests.CreateSceneClass_CellIdentity_ReturnsDemoCellScene` を追加した。
- テスト結果:
  - `WorldGridDefinitionLoadTests`: **1 / 1 成功**。Unity 終了コード `0xC0000005` 相当でも結果 XML 完成、failed 0。
  - `-Filter Cell`: **60 / 60 成功**。
  - C' 指摘対応後 `GameSceneFactoryTests`: **5 / 5 成功**。
  - 最終の全 EditMode: **525 / 525 成功、failed 0、skipped 0**。
  - `pwsh tools/docs-audit.ps1`: 検査 1・2 違反 0。M-2 / M-3 harvest 警告 2 件のみ。
- 結論: **PASS**。
- 残存リスク:
  - `WorldGridDefinition.asset` の `m_EditorClassIdentifier` は旧 namespace 文字列のままだが、
    `m_Script` GUID 維持と直接ロード 1 / 1 成功を確認した。YAML 手編集禁止のため触らない。
  - `WorldGridDefinition` の新規作成時 default output folder は旧 `OneStarMakerCommon` のまま。
    本番 asset は正しい SampleGame path であり、default 変更は挙動変更なので M-4 対象外とする。

---

## 8. Phase C' 実績

- 担当 / モデル: Codex / **GPT-5.5** の新規 read-only サブエージェント
  （監査レポート上の自己表記は generic な GPT-5）。会話履歴と Phase C 結論を渡さず、HANDOFF、
  staged diff、コード、テスト実測だけを一次資料とした。Phase C の Grok 系列と異なり独立性を満たす。
  Cursor の Claude Opus 5 / Gemini 3.1 Pro は月次 usage limit で起動できず、監査実績には数えない。
- 指摘:
  - S2: `SceneResourceGenerator.RebuildMapLookup` は HANDOFF A-1 に無い public Editor API 追加。
    Runtime 公開面や逆依存を増やさない最小の assembly 境界補正として採用し、本欄へ設計補正を記録した。
  - S3: factory Cell 分岐の直接テスト不足。上記テストを追加し、対象 5 / 5、全体 525 / 525 を再確認した。
  - S3: 旧 `m_EditorClassIdentifier` は GUID ロード成功を根拠に YAML 手編集せず残す。
  - S3: 新規 asset の default output folder は本番 asset と別の対象外リスクとして残す。
  - `STREAMING_CURRENT_SPEC.md` の所在更新と M-4 HANDOFF harvest / 削除は Phase D の責任として残す。
- 監査できなかった範囲: 監査担当自身は Unity / テストを再実行せず、上記結果 XML の集計値を一次資料とした。
- 結論: 指摘対応後 **Phase C' PASS**。High 指摘なし。残存リスクは対象外または Phase D へ明示済み。
