# 四季 Level と 4 動詞実証の実装計画 ハンドオフ (2026-08-26)

> Phase A（計画）: Cursor Cloud Agent（Claude Fable 5）
> 対象スライス: **S-3「季節矩形レイアウト + `WorldCellCatalog` 一般化 + 既存セル移送」**。S-4〜S-9 は §1 に方針だけ確定させ、実装はしない
> 分解と順序の正本: [§33](../../unity/Assets/Docs/Architecture/33-sample-demonstration-boundaries.md)。前スライス: S-1「生成器の非破壊化」（harvest 済み → [§21 §6 / R-6](../../unity/Assets/Docs/Architecture/21-scene-streaming.md)）
> **実装・検証はローカルマシンの Cursor Editor セッションで行う**（発注者決定 2026-08-26）。本書を書いたクラウド VM に Unity は無く、Phase A は静的調査のみ。§0.3 の実測はコード読解と `git grep` によるもので、Play / テスト実行は一切していない

---

## 0. 1分で把握

### 0.1 何をするか

§33 が設計した Season / Tunnel / 4 動詞（Build / Commit / Checkout / Streaming）を実装へ落とす。本書はその最初のスライス S-3 の作業指示と、S-4〜S-9 の確定方針を持つ。

### 0.2 発注者決定（2026-08-26。§33 の記述より新しく、優先する）

1. **Level コンテンツは生成器で大まかに作ってよいが、人または AI の手による編集を正とする。** → §1.2 で D-6 を改訂する
2. **実装・検証はローカルの Cursor Editor セッションで行う。** クラウドエージェントは計画とコード下書きまで（Unity が無いため生成器実行・テスト実行・Play 確認ができない）

### 0.3 現況の実測（2026-08-26 クラウド側で確認済み。やり直さないこと）

| 領域 | 実測 | 所在 |
|---|---|---|
| グリッド | dense 4×4 の const が正本。`WorldGridDefinition.asset` は写し（`EnsureGridDefinition` が毎回上書き） | `SampleGame/InGame/InGameSession/Streaming/WorldCellCatalog.cs` |
| Controller | `0..GridWidth × 0..GridHeight` の dense 全走査で desired / retain を計算。`StreamingConfig` が `GridWidth` / `GridHeight` を保持 | `OneStarMaker/Scripts/Runtime/Streaming/` |
| 正本 policy | 南辺 4 枚 `(0,0)(1,0)(2,0)(3,0)` = `HandAuthored`、他 12 枚 = `Generated` | `SampleGame/DependOnAll/Editor/Cells/CellAuthoringPolicy.cs` |
| 南辺ハードコード | `HandAuthoredCells` / `EnvironmentSproutCells` / `HandEditProbe` / 生成器完了ログの 4 箇所が南辺 4 枚を直書き | `CellAuthoringPolicy.cs` / `WorldCellStreamingSliceCreator.cs` / `HandEditProbe.cs` |
| セル実体 | 16 フォルダ。Environment `.unity` は南辺 4 枚のみ | `SampleGame/InGame/InGameSession/World/Cells/` |
| Variant | `.asset` の `Variant:` は **52 ファイル全て空文字**。非空値ゼロ | SceneMap / Cells / SceneGraphData |
| Addressables | グループは `Default Local Group` **1 個**（28 エントリ）。`Remote.LoadPath` 未定義。`RemoteFull.asset` / `VariantHybridPlayModeScript.asset` はメニュー実行待ちで未生成 | `AddressableAssetsData/` |
| §20 の機構 | `VariantFilteringBuildScript` / whitelist / Hybrid Play / `TryLoadRemoteCatalogAsync` / `RemoteCatalogRuntimeBridge` は**実装済み**。S-7 は新機構ではなくデータ（タグ・グループ・プロファイル）を流し込むスライス | `OneStarMaker/Scripts/Editor/Build/Variants/` ほか |
| テスト | WSC 10 + MultiFocus 3 + 統合 6 / 生成器 7 / `CellPopulationPlan` 13 ほか。全て EditMode。CI（GitHub Actions）は DebugStudio の `dotnet test` のみで、Unity テストはローカル `pwsh tools/run-tests.ps1` | `OneStarMaker/Tests/` |

---

## 1. 確定方針（Phase A の設計判断。実装で変えないこと）

### 1.1 スライス分解は §33 §12 に従う。1 スライス = 1 ブランチ = 1 HANDOFF

本書は **S-3 の HANDOFF を兼ねる**。S-4 以降は着手時に新しい HANDOFF を切り、本節の方針を引き継ぐ。

| # | 内容 | 前提 | 実行 |
|---|---|---|---|
| S-3 | レイアウト確定 + `WorldCellCatalog` 一般化 + 既存セル移送（本書 §2〜§5） | §33 | ローカル |
| S-4 | Season Level 復活。生成器が `Season_*` を吐き `World` を置き換える。初回季節の Ensure 問題（§33 §8 注記）もここで裁定 | S-3 | ローカル |
| S-5 | Tunnel 常設 1 本。滞在中の明示 `AddScene` と D-5 の失敗経路 | S-4 | ローカル |
| S-6 | 季節ごとの Addressables グループ = **Build 実証（冬）** | S-4 | ローカル |
| S-7 | 季節 Variant タグ + 未 Checkout 経路 = **Checkout 実証（秋）** | S-5, S-6 | ローカル |
| S-8 | 春の職種別コンテンツ = **Commit 実証**。`HandEditProbe` と生成器のスキャフォールド宣言を退役 | S-3 | ローカル |
| S-9 | 夏で [§21](../../unity/Assets/Docs/Architecture/21-scene-streaming.md) の T-07〜T-09 = **Streaming 実証** | S-4 | ローカル（実測） |

**§21 の T-07〜T-09 を S-9 まで動かさないこと**（§33 §12。数値が季節化のあとで取り直しになる）。

クラウドエージェントに切り出せるのは「コード + テストコードの下書き PR」まで。生成器実行・テスト・Play を伴う確定はすべてローカル。

### 1.2 正本 policy の改訂 — 「編集が正」を既定にし、夏だけ `Generated` に残す（D-6 改訂）

発注者決定 1 を §21 §6 の既存機構に載せる。**新しい policy 種別は作らない。** `HandAuthored` の意味論（「`AuthoredRoot` があれば触らない。無ければ初回スキャフォールドとして生成する」）が「生成器で大まかに作り、以後の編集を正とする」そのものである。

| 季節 | 正本 policy（改訂後） | §33 D-6 からの変化 |
|---|---|---|
| 春 | `HandAuthored` | 変化なし |
| 夏 | **`Generated`** | 変化なし |
| 秋 | `HandAuthored` | `Generated` → 変更 |
| 冬 | `HandAuthored` | `Generated` → 変更 |

**夏を `Generated` に残す理由（全季節 `HandAuthored` 化は却下）:**

1. §21 §6 は「どちらか一方に決める必要はなく、**決めてもいけない** — 両方を同居させられることがサンプルの証明対象」と明記している。全季節を編集正本にすると、この証明の `Generated` 側が世界から消える
2. 夏は Streaming 計測（A-1 / A-2）の場である。手編集でセルごとの密度が揺れると、計測値が編集履歴に依存して再現しなくなる。均質な量産グリッドのまま保つ

**AI の編集も「手編集」である。** 生成器が守るのは `AuthoredRoot` 配下だけなので、AI セッションが `.unity` を編集するときも必ず `AuthoredRoot` 配下に置くこと。外に置いたものは次の生成で消える。

**§33 への反映:** S-3 の Phase D で §33 の D-6 行と §5 表の「正本 policy」列をこの割り当てへ改訂する（harvest 項目）。それまで §33 と本書が食い違う期間は本書 §1 が優先。

### 1.3 季節矩形レイアウト（O-1 の提案）

単一座標空間（D-1）に横一列。空隙は各 3 セル列（最近セル中心間 1000m > `UnloadRadius` 550m。§33 §7 の条件を余裕込みで満たす）。`CellIdentity` の非負制約にも収まる。

| 季節 | セル範囲 | 大きさ | セル数 |
|---|---|---|---|
| 春 | x 0..1, y 0..1 | 2×2 | 4 |
| 夏 | x 5..8, y 0..3 | 4×4 | 16 |
| 秋 | x 12..13, y 0..1 | 2×2 | 4 |
| 冬 | x 17..18, y 0..1 | 2×2 | 4 |

空隙列: x = 2..4 / 9..11 / 14..16（合計 28 セル。§33 §5 の 4 + 16 + 4 + 4 と一致）。

```
 ┌─────────┐  空隙   ┌───────────────┐  空隙   ┌─────────┐  空隙   ┌─────────┐
 │ 春 2×2  │◄─ 3 ──►│    夏 4×4     │◄─ 3 ──►│ 秋 2×2  │◄─ 3 ──►│ 冬 2×2  │
 │ x 0..1  │  列     │    x 5..8     │  列     │ x 12..13│  列     │ x 17..18│
 │ y 0..1  │         │    y 0..3     │         │ y 0..1  │         │ y 0..1  │
 └─────────┘         └───────────────┘         └─────────┘         └─────────┘
```

§33 §7 が固定するのは「空隙 > `UnloadRadius`」だけなので、実装中に座標をずらす必要が出たら条件を守って再配置してよい。**ただし空隙条件は §3 T-A のテストで固定し、目視に頼らないこと。**

### 1.4 既存 16 セルの移送（O-2 の確定）

- **`Generated` 12 枚は捨てる。** 生成物なので再生成できる。破壊経路 3（範囲外 Cell フォルダの削除）に削除させる
- **`HandAuthored` 南辺 4 枚は春矩形へ移送する:** `(0,0)` `(1,0)` は identity・座標とも据え置き。**`Cell_2_0` → `Cell_0_1`、`Cell_3_0` → `Cell_1_1`**（Environment も同時に）
- **順序は移送 → 生成器**（§4）。逆にすると `(2,0)` `(3,0)` が「範囲外だが保持」の孤児として残る（消えはしない。R-6）
- **ワールド座標の補正が要る。** 焼き込み・手編集とも `AuthoredRoot` 配下にワールド座標で置かれている。移送 2 枚はどちらも Δ(X −500m, Z +250m)。`AuthoredRoot` 自体がセル原点に立っているならルート移動 1 回で済む — 実装時にシーンを開いて確認
- 追随が要るもの: フォルダ / `.unity` / `.asset` のファイル名、`.asset` 内 identity、SceneGraph ノード、Addressables address（= assetPath。生成器が再登録する）、§0.3 の南辺ハードコード 4 箇所

### 1.5 季節テーマ（創造面の確定方針）

生成器の「大まか」= 初回スキャフォールドで地面 + 季節ベースカラー + 最小限のモチーフまで。作り込みは編集（人 / AI）が正本。テーマは**動詞の実証が見た目で読める**ように選ぶ:

| 季節 | テーマ | 実証との結びつき |
|---|---|---|
| 春 | 桜の丘。パステル。丘 + 小道 + 桜並木 + 東屋 | Commit: 地形担当 = 丘と小道（`Cell_x_y.unity`）、置き物担当 = 桜と東屋（`Environment_x_y.unity`）。「地形 vs 置き物」で職種分割が読みやすい |
| 夏 | 濃緑の草原 + 水面。生成器モチーフの均質量産 | Streaming: 編集しない。計測の再現性を最優先 |
| 秋 | 紅葉の渓谷。赤 / 橙 / 黄 | Checkout: リモート解決で「来た」ことが色で一目で分かる |
| 冬 | 雪原と氷。白 / 青 | Build: 差し替えビルドの前後差分（例: 雪だるまの有無）が見た目で分かる |

Tunnel は無地 1 本（D-4）。季節ごとの見た目差が要る場合も Scene を増やさず Variant で分ける。

テーマの実装時期: スキャフォールドの季節ベースカラーは S-4（生成器が Season を知る時点）、作り込みは S-8 以降。**S-3 では現行モチーフのまま座標だけ変える。**

### 1.6 本スライス（S-3）でやらないこと

- `Season_*` / Tunnel ノード（S-4 / S-5）。**S-3 のシーン木は `InGameSession → World → Cell` のまま**。変わるのは座標集合と policy 割り当てだけ
- Addressables グループ分割（S-6）、Variant タグ付与（S-7）
- 季節テーマの焼き込み・春の職種別コンテンツ（S-8）
- テレメトリ計測・受入判定（S-9。T-07〜T-09 凍結）
- `unityyamlmerge` ドライバ設定（O-5。職種分割の前提条件ではない）
- 初回季節の Ensure 問題（§33 §8 注記。S-4 の論点）

---

## 2. 変更対象ファイル一覧（S-3）

| ファイル | 変更 |
|---|---|
| `SampleGame/InGame/InGameSession/Streaming/WorldCellCatalog.cs` | dense 4×4 定数 → **季節矩形の集合**（矩形リスト + 全セル列挙）。`TryGetCoordinate` を矩形集合の membership に、`CornerSpawn(0..3)` を「各季節矩形の中心上空」へ。`SpawnPosition`（春 `Cell_0_0`）は不変。正本が const 側である関係も不変 |
| `OneStarMaker/Scripts/Runtime/Streaming/StreamingConfig.cs` | `GridWidth` / `GridHeight` → **セル座標集合**（`IReadOnlyList<Vector2Int>` 相当）。空集合は例外。**FW は矩形も季節も知らない**（D-1）。列挙は呼び出し側の責務 |
| `OneStarMaker/Scripts/Runtime/Streaming/WorldStreamingController.cs` | dense 二重ループ → 集合走査。**ポリシー（desired / retain / ヒステリシス / in-flight / priority）は不変**（§33 §10） |
| `SampleGame/InGame/InGameSession/Streaming/SessionWorldStreamingDriver.cs` | Catalog の全セル列挙を Config へ渡す。§33 §8「今いる季節の矩形を母集合として受け取る」への布石で、**S-3 は全 28 セル、S-4 で現季節に絞る** |
| `OneStarMaker/Scripts/Editor/Streaming/WorldGridDefinition.cs` + `.asset` | N×N → 矩形リスト。`EnsureGridDefinition` による「const 正本 → アセット写し」の関係は不変 |
| `OneStarMaker/Scripts/Editor/Streaming/WorldCellGenerator.cs` | `ComputePlan` の走査を矩形集合に |
| `SampleGame/DependOnAll/Editor/Cells/CellAuthoringPolicy.cs` | 南辺 4 枚 → **春 / 秋 / 冬矩形 = `HandAuthored`、夏矩形 = `Generated`**（§1.2） |
| `SampleGame/DependOnAll/Editor/Cells/CellPopulationPlan.cs` | グリッド入力を矩形集合へ一般化。範囲外（空隙含む）判定・削除可否・keep 判定は同じ計画経由のまま |
| `SampleGame/DependOnAll/Editor/WorldCellStreamingSliceCreator.cs` | `EnvironmentSproutCells` → 春矩形 4 枚。完了ログ文字列。焼き込みは現行モチーフ流用 |
| `SampleGame/DependOnAll/Editor/HandEditProbe.cs` | stamp / verify 対象を春矩形へ追随（S-8 で退役予定なので最小限） |
| `OneStarMaker/Tests/` 各所 | `StreamingConfig` / グリッド生成箇所の追随 + §3 の新規テスト |

**FW（`OneStarMaker`）に「季節」の語彙を入れないこと**（型名・identity・コメントとも）。矩形は幾何としてのみ FW に渡る。`CellIdentity` 書式・`SceneState`・asmdef 依存方向は §33 §10 のとおり不変。

---

## 3. 単体テストの要求（必須）

前提: `OneStarMaker.Tests` は `SampleGame.InGame` を、`OneStarMaker.Tests.Editor` は `SampleGame.DependOnAll.Editor` を参照済み（S-1 で追加）。**着手時に asmdef を開いて再確認すること。**

| # | テスト | 検証内容 |
|---|---|---|
| T-A | **レイアウト回帰ガード**: Catalog の矩形集合の全ペアについて「異なる矩形に属する最近セル中心間距離 > `UnloadRadius`」 | §33 §7 の空隙条件の構造的強制。レイアウトや半径定数を動かすと落ちる。**本スライスで最も恒久価値が高いテスト** |
| T-B | Controller 集合版: 空隙の中間に focus を置くと desired が空。単一矩形を渡すと従来 dense と同じ desired / retain / priority | 一般化の挙動保存。既存 10 本は「4×4 単一矩形」を渡す形へ書き換えて全部残す |
| T-C | `CellPopulationPlan` 矩形集合版: 空隙セルは Populate に現れない。範囲外 `HandAuthored` は削除不可・`Generated` は削除可（既存 T-6 / T-7 / T-11 の一般化） | 破壊経路 3 の保護が矩形集合でも同じに効く |
| T-D | `WorldCellCatalog.TryGetCoordinate`: 空隙座標・矩形外で false、各矩形の四隅で true | membership の境界 |

- **`record` を使わないこと**（`IsExternalInit` が無く、プロジェクト全体がコンパイル不能になる）。`readonly struct` か `sealed class`
- TDD で回すこと: スケルトン + レッドを確認してから実装
- **テスト 0 件は失敗扱い**（コンパイルエラーが 0 件として現れる）

---

## 4. 実装順序（S-3。ローカル Cursor Editor セッション向け）

1. `develop` からブランチを切る（§33 のブランチがマージ済みであることを先に確認）
2. **破壊経路 3 の再確認から始める**（§33 §12）: `CellPopulationPlanTests` の T-7 / T-11 を読み、「矩形集合の範囲外」でも同じ保護になることを §3 T-C のレッドテストで先に固定する
3. `HandEditProbe.StampHandEdits` で現南辺 4 枚に stamp（移送前ベースライン）
4. **移送**（§1.4）: `git mv` で `Cell_2_0` → `Cell_0_1`、`Cell_3_0` → `Cell_1_1`。identity 文字列・SceneGraph ノードを追随、`AuthoredRoot` のワールド座標を Δ(X −500, Z +250) 補正。**移送だけで 1 コミット**（レビューが diff で読めるように）
5. `WorldCellCatalog` / `StreamingConfig` / `WorldStreamingController` / `SessionWorldStreamingDriver` の一般化（§3 レッド → グリーン）
6. `CellAuthoringPolicy` / `CellPopulationPlan` / `WorldGridDefinition` / `WorldCellGenerator` / `WorldCellStreamingSliceCreator` の一般化
7. 生成器を実行 → 旧 `Generated` 12 フォルダの削除・新 28 セルの生成を確認 → **もう一度実行して差分 0**
8. `HandEditProbe.VerifyHandEdits` → 8 / 8 生存
9. `pwsh tools/run-tests.ps1` 全緑（Unity を閉じてから）
10. Editor Play: 春スポーン → 夏矩形へ飛行。空隙で新規ロードが発生せず、通過後は夏セルのみ常駐
11. §33 を改訂する: D-6 / §5 表（§1.2）、§7 のレイアウト確定値（§1.3）、O-1 / O-2 を畳む。`pwsh tools/docs-audit.ps1`
12. PR（base: `develop`）→ cursor[bot] レビュー → 本書 §7 / §8 を埋め、Phase D で harvest して本書を `git rm`

---

## 5. 受入条件（S-3）

| # | 条件 | 判定方法 |
|---|---|---|
| A-1 | 全テスト緑 | `pwsh tools/run-tests.ps1`。failed 0。**テスト 0 件は失敗扱い** |
| A-2 | **移送した手編集が消えない（本スライスの核心）** | 手順 3 の stamp が、移送 + 生成器 2 回実行のあと `VerifyHandEdits` で 8 / 8 生存 |
| A-3 | 新レイアウト 28 セルが存在し、旧 `Generated` 12 枚がフォルダ・SceneGraph ノードとも消えている | Explorer / grep。`SceneResourceMap` の Cell 系エントリ数が新レイアウトと一致 |
| A-4 | 空隙条件がテストで固定されている | §3 T-A が存在して緑 |
| A-5 | 生成器 2 回目実行で `HandAuthored` 側に差分が出ない | クリーンな作業ツリーから実行 → `git status --porcelain` に春 4 Cell + Environment 4 枚が現れない。`Generated` 側の差分は受入失敗にしない（生成物は捨てる前提。S-1 の A-2 と同じ判定） |
| A-6 | Editor Play で春 → 夏の飛行が成立 | 空隙で desired が空（新規ロード無し）、夏矩形でストリーミング再開、例外 0 |
| A-7 | FW に季節の語彙が漏れていない | `unity/Assets/OneStarMaker/` を Season / 季節で grep → 0 件（D-1） |

偽 null チェック（`?.` / `??` / `is null` / `ReferenceEquals`）の grep をレビュー時に行うこと（S-1 の教訓。破棄済み `UnityEngine.Object` は `?.` / `??` が短絡しない）。

---

## 6.0 Phase B からの設計指摘

（未記入）

---

## 6. Phase C からの差し戻し

（未記入）

---

## 7. Phase C レビュー

（未記入）

---

## 8. Phase C' 監査

（未記入）
