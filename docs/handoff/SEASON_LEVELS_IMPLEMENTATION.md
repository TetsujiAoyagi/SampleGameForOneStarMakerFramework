# 四季 Level と 4 動詞実証の実装計画 ハンドオフ (2026-08-26 / 2026-08-27 改訂 / 2026-08-29 退役注記)

> **2026-08-29:** S-3 機構は `develop` にマージ済み。世界構図の正本は [SEASON_WORLD_DESIGN.md](SEASON_WORLD_DESIGN.md)。空間プロトコルは [SCENE_WORLD_BOUNDS.md](SCENE_WORLD_BOUNDS.md)。退役した構図・policy・空隙・Fable 依頼は本書に本文を残さない。既存 16 セルは S-4 で全廃し、移送しない。
> **残す理由:** §0.3 の実測表と §2〜§3 / §4.1 / §5.1 の S-3 記録。harvest 後に `git rm`。
>
> Phase A（計画）: Cursor Cloud Agent
> 対象スライス: **S-3「`WorldCellCatalog` を矩形集合へ一般化する」のみ**（完了）。
> 分解と順序の正本: 世界は SEASON_WORLD_DESIGN、空間は SCENE_WORLD_BOUNDS。§33 の harvest は Bounds の口が通ってから。
> **実装・検証はローカルマシンの Cursor Editor セッションで行う**（発注者決定 2026-08-26）。§0.3 の実測はコード読解と `git grep` によるもので、Play / テスト実行は一切していない
>
> **2026-08-27 改訂:** 初版 §1.3 の座標確定と §1.5 の季節テーマ表を撤回した。理由は §0.4。
>
> **2026-08-27 Cloud 改訂:** Unity シーン操作の手順、CLI / Skills、変更対象の欠落、A-1〜A-3、テスト主体を詰めた。

---

## 0. 1分で把握

### 0.1 何をするか

§33 が設計した Season / Tunnel / 4 動詞を実装へ落とす作業のうち、**S-3（矩形集合化）の記録**が本書に残っている。世界構図と空間は隣の 2 ファイルが正本。

S-3 は機構だけを先に通し、構図を混ぜない（§0.4）。これは完了済み。

### 0.2 発注者決定（§33 の記述より新しく、優先する）

| 日付 | 決定 |
|---|---|
| 2026-08-26 | **Level コンテンツは生成器で大まかに作ってよいが、人または AI の手による編集を正とする。** 2 段 policy の正本は SEASON_WORLD_DESIGN §4 |
| 2026-08-26 | **実装・検証はローカルの Cursor Editor セッションで行う。** クラウドエージェントは計画とコード下書きまで（Unity が無いため生成器実行・テスト実行・Play 確認ができない） |
| 2026-08-27 | **大きさと置き方を計画側で確定しない。** 構図の正本は SEASON_WORLD_DESIGN（Fable 稿を改訂済み） |
| 2026-08-27 | **開いている Editor への Unity CLI（`unity command` / `unity eval`）は可。** Unity.exe 起動・`run-tests.ps1`・`unity test` / `unity run`・Addressables ビルドは禁止。接続できる Editor があるとき `.unity` / `.prefab` / `.asset` の YAML 手直しは禁止。`com.unity.pipeline` は manifest 宣言のみ（lock はローカル Editor が書く） |

### 0.3 現況の実測（2026-08-26 クラウド側で確認済み。やり直さないこと）

| 領域 | 実測 | 所在 |
|---|---|---|
| グリッド | dense 4×4 の const が正本。`WorldGridDefinition.asset` は写し（`EnsureGridDefinition` が毎回上書き） | `SampleGame/InGame/InGameSession/Streaming/WorldCellCatalog.cs` |
| Controller | `0..GridWidth × 0..GridHeight` の dense 全走査で desired / retain を計算。`StreamingConfig` が `GridWidth` / `GridHeight` を保持 | `OneStarMaker/Scripts/Runtime/Streaming/` |
| 飛行速度 | `FlyController._moveSpeed = 42` m/s（ブースト 2.4 倍で約 100 m/s） | `SampleGame/InGame/InGameSession/PlayerScene/FlyController.cs` |
| 正本 policy | 南辺 4 枚 `(0,0)(1,0)(2,0)(3,0)` = `HandAuthored`、他 12 枚 = `Generated` | `SampleGame/DependOnAll/Editor/Cells/CellAuthoringPolicy.cs` |
| 南辺ハードコード | `HandAuthoredCells` / `EnvironmentSproutCells` / `HandEditProbe` / 生成器完了ログの 4 箇所が南辺 4 枚を直書き | `CellAuthoringPolicy.cs` / `WorldCellStreamingSliceCreator.cs` / `HandEditProbe.cs` |
| セル実体 | 16 フォルダ。Environment `.unity` は南辺 4 枚のみ。中身は Cube 地面 + モチーフ 4 種の Primitive | `SampleGame/InGame/InGameSession/World/Cells/` |
| Variant | `.asset` の `Variant:` は **52 ファイル全て空文字**。非空値ゼロ | SceneMap / Cells / SceneGraphData |
| Addressables | グループは `Default Local Group` **1 個**（28 エントリ）。`Remote.LoadPath` 未定義。`RemoteFull.asset` / `VariantHybridPlayModeScript.asset` はメニュー実行待ちで未生成 | `AddressableAssetsData/` |
| §20 の機構 | `VariantFilteringBuildScript` / whitelist / Hybrid Play / `TryLoadRemoteCatalogAsync` / `RemoteCatalogRuntimeBridge` は**実装済み**。S-7 は新機構ではなくデータ（タグ・グループ・プロファイル）を流し込むスライス | `OneStarMaker/Scripts/Editor/Build/Variants/` ほか |
| テスト | WSC 10 + MultiFocus 3 + 統合 6 / 生成器 7 / `CellPopulationPlan` 13 ほか。全て EditMode。CI（GitHub Actions）は DebugStudio の `dotnet test` のみで、Unity テストはローカル `pwsh tools/run-tests.ps1` | `OneStarMaker/Tests/` |

### 0.4 初版 §1.3 / §1.5 を撤回する理由

初版は O-1（§33 が「S-3 で確定」と開けておいた構図）を、次のレイアウトで埋めた。

```
春 2×2 (x 0..1) — 空隙 3 列 — 夏 4×4 (x 5..8) — 空隙 3 列 — 秋 2×2 (x 12..13) — 空隙 3 列 — 冬 2×2 (x 17..18)
```

これは空隙条件を満たす**最安の合法解**であって、世界の構図ではない。問題は 3 段ある。

1. **§33 の「要る大きさ」を設計寸法にした。** 「2×2 で足りる」は Commit / Checkout / Build の証明に必要な最小で、500 m 四方（飛行 42 m/s で横断約 12 秒）は庭である。3 季節が同じアスペクトの切手、夏だけが「本物の場所」に見える
2. **算術的に置き、一列に並べた。** 座標 `0, 5, 12, 17` は計算しやすいだけで、地図として読む理由が無い。トンネルは隔離装置であり往来の街道ではないのに、街道のように見える
3. **創造の枠を計画が先に埋めた。** 桜 / 紅葉 / 雪だるまは四季ポスターの既定解である。確定方針にした瞬間、Fable が設計する余地が消える。計画セッションに「全部書いて完成に見せる」を求めると、開いた論点はこうして潰れる

**教訓（依頼の仕方）:** 計画セッションに構図を確定させるな。制約と品質バーと「答えてはいけない既定解」を渡し、設計セッションに構図を出させ、人が図を見てから座標をコードへ落とす。

---

## 1. 確定方針（Phase A の設計判断。実装で変えないこと）

### 1.1 スライス分解。1 スライス = 1 ブランチ = 1 HANDOFF

本書は **S-3 の HANDOFF を兼ね、S-3C の依頼文を同梱する**。S-4 以降は着手時に新しい HANDOFF を切り、本節の方針を引き継ぐ。

| # | 内容 | 前提 | 誰が |
|---|---|---|---|
| **S-3** | Catalog / Config / 生成器入力を「矩形の集合」へ一般化する。**本番レイアウトは現行 4×4 を矩形 1 個のまま残す。セルは動かさない** | §33 | ローカル（機構） |
| **S-3C** | 世界構図。正本は [SEASON_WORLD_DESIGN.md](SEASON_WORLD_DESIGN.md)。空間は [SCENE_WORLD_BOUNDS.md](SCENE_WORLD_BOUNDS.md) が先 | S-3 | 設計済み。実装は Bounds の口のあと |
| S-4 | Season Level 復活。生成器が `Season_*` を吐き `World` を置き換える。初回季節の Ensure 問題（§33 §8 注記）もここで裁定 | Bounds の口 + S-3C 構図 | ローカル |
| S-5 | Tunnel 常設 1 本。滞在中の明示 `AddScene` と D-5 の失敗経路 | S-4 | ローカル |
| S-6 | 季節ごとの Addressables グループ | S-4 | ローカル。手順は SEASON_WORLD_DESIGN |
| S-7 | 季節 Variant タグ + 未 Checkout 経路 | S-5, S-6 | ローカル。手順は SEASON_WORLD_DESIGN |
| S-8 | 演奏レイヤ。春（S-8a）で 4 動詞は出荷可能 | Bounds + S-4 | ローカル。手順は SEASON_WORLD_DESIGN |
| S-9 | 夏の背コリドーで [§21](../../unity/Assets/Docs/Architecture/21-scene-streaming.md) の T-07〜T-09 | S-4 | ローカル（実測）。y=4 未昇格が前提 |

**§21 の T-07〜T-09 を S-9 まで動かさないこと**（§33 §12。数値が季節化のあとで取り直しになる）。

S-3 と S-3C を混ぜないこと。混ぜると、機構を通すためにまた最安レイアウトが確定する（§0.4 の再発）。

### 1.2 退役した構図指定

季節別 policy、空隙付き矩形 4 つ、Fable 依頼文、S-3C の「矩形 4 つへ移送」は退役した。正本は隣の 2 ファイル。既存 16 セルは S-4 で全廃し、移送しない。

- 構図・policy: [SEASON_WORLD_DESIGN.md](SEASON_WORLD_DESIGN.md)
- 距離政策: [SCENE_WORLD_BOUNDS.md](SCENE_WORLD_BOUNDS.md)

S-3 の本番データは現行 4×4 を矩形 1 個として残す（完了済み）。複数矩形の挙動はテスト用フィクスチャ（§3 T-A / T-B）。
AI の編集も `AuthoredRoot` 配下に置くこと、は生きている（R-6）。

### 1.6 本スライス（S-3）でやらないこと

- 季節矩形の本番座標・寸法の確定（S-3C）
- 既存セルの全廃（S-4。移送しない。Bounds 中は 16 枚を残す）
- `Season_*` / Tunnel ノード（S-4 / S-5）。**S-3 のシーン木は `InGameSession → World → Cell` のまま**
- Addressables グループ分割（S-6）、Variant タグ付与（S-7）
- 季節テーマの焼き込み・春の職種別コンテンツ（S-3C / S-8）
- テレメトリ計測・受入判定（S-9。T-07〜T-09 凍結）
- `unityyamlmerge` ドライバ設定（O-5。職種分割の前提条件ではない）
- 初回季節の Ensure 問題（§33 §8 注記。S-4 の論点）

### 1.7 Unity Editor 操作境界（Cloud / ローカル）

| 誰 | やってよい | やってはいけない |
|---|---|---|
| Cloud（本書を書いた環境） | C#・HANDOFF・manifest 宣言・Skills | `unity` バイナリのインストール、生成器実行、テスト実行、Play |
| ローカル実装 | 人間が開いた Editor への `unity status` / `unity command` / `unity eval`。生成器 1 回は既存メニュー | Unity.exe 起動、`pwsh tools/run-tests.ps1`、`unity test`、`unity run`、Addressables ビルド、接続中の YAML 手直し |
| Phase C | `pwsh tools/run-tests.ps1` 全緑。偽 null grep | — |

ローカルで CLI が未導入なら、人間が beta チャネルで入れる（Windows）:

```powershell
$env:UNITY_CLI_CHANNEL='beta'; irm https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.ps1 | iex
```

プロジェクトパスはリポジトリの `unity/`。`unity status` が `ready` になるまでシーンを触らない。繋がらなければ Safe Mode（コンパイルエラー）を先に疑う。YAML フォールバックは書かない。

名前付き Pipeline command（0.4 系の `move_asset` / `open_scene` / `save_scene` / `set_transform` / `menu`）を先に使い、足りないときだけ `eval`。コマンド名は `unity command` でその Editor の公開面を見てから叩く。

`com.unity.pipeline` は `unity/Packages/manifest.json` に `0.4.0-exp.1` を宣言してある。実験パッケージなので、ローカル Editor が解決したバージョンが違うなら `packages-lock.json` を正とする。lock は手で書かない。

スキル: `.cursor/skills/osm-unity-editor/SKILL.md` がこのリポジトリの上書き。公式 `.cursor/skills/unity-cli/SKILL.md` より優先する。

---

## 2. 変更対象ファイル一覧（S-3）

S-3 は「矩形 1 個 = 今の 4×4」でも「矩形 4 個」でも同じコード経路になるようにする。本番は前者。

規模は workflow A-1（現在行数 → 予想行数 / 責務）。予想は「足す量の上限」であり、超えたら §7 で構造レビューする。

| ファイル | 現在 | 予想 | 責務 | 変更 |
|---|---|---|---|---|
| `SampleGame/InGame/InGameSession/Streaming/WorldCellCatalog.cs` | 123 | ~165 | 1（格子定数 + 列挙） | dense 4×4 定数 → **矩形の集合**（`CellRect` リスト + `EnumerateCells`）。本番は `{ origin=(0,0), size=(4,4) }` の 1 要素。`TryGetCoordinate` は集合の membership。`SpawnPosition` / `CornerSpawn` は現行どおり 4×4 の隅 |
| `OneStarMaker/Scripts/Runtime/Streaming/StreamingConfig.cs` | 83 | ~110 | 1（ポリシーパラメータ） | `GridWidth` / `GridHeight` を削除し、展開済み **セル座標集合** を持つ。空集合は例外。**FW は矩形も季節も知らない**（D-1）。列挙は呼び出し側の責務 |
| `OneStarMaker/Scripts/Runtime/Streaming/WorldStreamingController.cs` | 302 | ~310 | 1（desired / retain ポリシー） | dense 二重ループ → `Config.Cells` 走査。**ポリシー（desired / retain / ヒステリシス / in-flight / priority）は不変**（§33 §10） |
| `SampleGame/InGame/InGameSession/Streaming/SessionWorldStreamingDriver.cs` | 209 | ~220 | 1（Catalog → Config 配線） | Catalog の全セル列挙を Config へ渡す。S-4 で「今いる季節の矩形」に絞る布石。S-3 は現行 16 セル全部 |
| `OneStarMaker/Scripts/Editor/Streaming/WorldGridDefinition.cs` + `.asset` | 61 | ~90 | 1（生成器入力 SO） | `_gridWidth` / `_gridHeight` → 矩形リスト。`EnsureGridDefinition` による「const 正本 → アセット写し」の関係は不変 |
| `OneStarMaker/Scripts/Editor/Streaming/WorldCellGenerator.cs` | 602 | ~620 | 1（計画 + 適用） | `ComputePlan` の走査を矩形集合に。分割しない |
| `SampleGame/DependOnAll/Editor/Cells/CellPopulationPlan.cs` | 270 | ~310 | 1（Populate / Skip / 削除の純関数） | `CellGridSpec` を矩形集合へ。範囲外判定・削除可否・keep 判定は同じ計画経由のまま |
| `SampleGame/DependOnAll/Editor/WorldCellStreamingSliceCreator.cs` | 1380 | ~1400 | 足場（削除候補） | **コンパイル追随のみ。** `EnsureGridDefinition` の `_gridWidth` / `_gridHeight` 書き込みと `CellGridSpec(definition.GridWidth, …)` を新 API に合わせる。クラス自身が「構造化しない」と書いてある。**A-2 例外（一度きりの生成スクリプト）。新責務を足さない（A-3）** |
| `OneStarMaker/Tests/Streaming/WorldStreamingControllerTests.cs` | 436 | 追随 + T-B | テスト | 単一矩形入力へ書き換え、既存 10 本相当を残す |
| `OneStarMaker/Tests/Streaming/WorldStreamingControllerMultiFocusTests.cs` | 184 | 追随 | テスト | `StreamingConfig` 生成を集合 API へ |
| `OneStarMaker/Tests/Streaming/StreamingIntegrationTests.cs` | 650 | 追随 | テスト | 同上 |
| `OneStarMaker/Tests/Streaming/CameraStreamingFocusAdapterTests.cs` | 156 | 追随 | テスト | 同上 |
| `OneStarMaker/Tests/Editor/WorldCellGeneratorTests.cs` | 291 | 追随 | テスト | 矩形入力へ |
| `OneStarMaker/Tests/Editor/CellPopulationPlanTests.cs` | 304 | 追随 + T-C | テスト | T-7 / T-11 を矩形集合の範囲外でも同じ保護に |
| 新規テスト（既存ファイルに足してよい） | — | T-A / T-D | テスト | §3 |

S-3 では触らない: `CellAuthoringPolicy`（南辺 4 枚のまま）、`EnvironmentSproutCells` の座標そのもの、`HandEditProbe`、セル `.unity` の中身。これらは S-4 の全廃と同時に動かす（正本は SEASON_WORLD_DESIGN §6。移送しない）。`WorldCellStreamingSliceCreator` の sprout 配列の**値**は動かさない。触るのは定義 API への追随だけ。

**FW（`OneStarMaker`）に「季節」の語彙を入れないこと**（型名・identity・コメントとも）。矩形は幾何としてのみ FW に渡る。`CellIdentity` 書式・`SceneState`・asmdef 依存方向は §33 §10 のとおり不変。

### 2.1 API 署名（実装で変えない）

`record` 禁止（`IsExternalInit` が無い）。矩形型は SampleGame Catalog と Editor の `WorldGridDefinition` に置く。Runtime の `StreamingConfig` は矩形を知らない。

```csharp
public readonly struct CellRect
{
    public CellRect(Vector2Int origin, Vector2Int size);
    public Vector2Int Origin { get; }
    public Vector2Int Size { get; } // x = 幅, y = 高さ。どちらも 1 以上
}

// WorldCellCatalog（SampleGame）
public static readonly CellRect[] Rectangles; // 本番: 1 要素 {(0,0),(4,4)}
public static IReadOnlyList<Vector2Int> EnumerateCells();
public static bool TryGetCoordinate(Vector3 worldPosition, out Vector2Int coordinate);
// TryGetCoordinate: Origin / CellSize で floor したあと集合 membership。
// AABB 内でも空隙なら false。

// StreamingConfig（FW）
public StreamingConfig(
    CellGridConfig grid,
    IReadOnlyList<Vector2Int> cells,
    float loadRadius,
    float unloadRadius,
    int maxInFlight);
public IReadOnlyList<Vector2Int> Cells { get; }
// cells が null / 空 → 例外。GridWidth / GridHeight は削除。
```

- 矩形の重なり・サイズ 0 以下は Catalog / `WorldGridDefinition` の検証で例外。重複座標は重なりを禁じた結果として出ない
- `CellGridSpec` は `GridWidth` / `GridHeight` をやめ、矩形集合（または展開済み座標）を取る
- `.asset` のシリアライズ形が変わる。`EnsureGridDefinition` が Catalog 正本から写し直す（手で YAML を書き換えない）。`packages-lock.json` と同様、生成物の差分はローカル Editor が書く

---

## 3. 単体テストの要求（必須）

前提: `OneStarMaker.Tests` は `SampleGame.InGame` を、`OneStarMaker.Tests.Editor` は `SampleGame.DependOnAll.Editor` を参照済み（S-1 で追加）。**着手時に asmdef を開いて再確認すること。**

| # | テスト | 検証内容 |
|---|---|---|
| T-A | **空隙ガード（フィクスチャ）**: 矩形 2 つ以上の合成レイアウトで「異なる矩形の最近セル中心間距離 > UnloadRadius」。違反レイアウトを与えるとテスト側で検出できること | 本番 4×4 単体ではペアが 0 なので、**テスト専用の矩形集合**で書く。S-3C 後に本番 Catalog へ同じ assert を足す |
| T-B | Controller 集合版: (1) 現行相当の 4×4 単一矩形で既存 10 本と同等の desired / retain / priority (2) 空隙を挟んだ 2 矩形の中間に focus を置くと desired が空 | 一般化の挙動保存。既存 10 本は「単一矩形を渡す」形へ書き換えて全部残す |
| T-C | `CellPopulationPlan` 矩形集合版: 空隙セルは Populate に現れない。範囲外 `HandAuthored` は削除不可・`Generated` は削除可（既存 T-6 / T-7 / T-11 の一般化） | 破壊経路 3 の保護が矩形集合でも同じに効く |
| T-D | `WorldCellCatalog.TryGetCoordinate`: 現行 4×4 の四隅で true、外側で false。フィクスチャで空隙座標が false | membership の境界 |

- **`record` を使わないこと**（`IsExternalInit` が無く、プロジェクト全体がコンパイル不能になる）。`readonly struct` か `sealed class`
- テストコードは実装より先に書く。EditMode の個別レッド確認は、ローカルで Editor が開いていれば `unity command eval` で呼んでよい
- **全件ランナー（`unity test` / `pwsh tools/run-tests.ps1`）は Phase C。** 実装者は走らせない。報告は「実装完了。テスト未実行」
- **テスト 0 件は失敗扱い**（コンパイルエラーが 0 件として現れる）

---

## 4. 実装順序

### 4.1 S-3（機構。ローカル。Cloud では走らせない）

1. `develop` からブランチを切る（§33 のブランチがマージ済みであることを先に確認）
2. **破壊経路 3 の再確認から始める**（§33 §12）: `CellPopulationPlanTests` の T-7 / T-11 を読み、矩形集合の範囲外でも同じ保護になることを §3 T-C のテストコードで先に固定する
3. `StreamingConfig` / `WorldStreamingController` を集合走査へ。既存 WSC テストを単一矩形入力に書き換え、T-B を足す
4. `WorldCellCatalog` / `WorldGridDefinition` / `WorldCellGenerator` / `CellPopulationPlan` を矩形集合へ。本番 Catalog は 4×4 の矩形 1 個。`WorldCellStreamingSliceCreator` は `EnsureGridDefinition` と `CellGridSpec` のコンパイル追随のみ
5. 人間がこのプロジェクトの Editor を開く。エージェントは `unity status` で `ready` を確認する
6. 生成器を **1 回**: 既存メニュー `OneStarMaker/Sample/Create World + Cell Streaming Slice`（`WorldCellStreamingSliceCreator.CreateFromMenu`）を `unity command menu` があればそれで、無ければ eval で叩く。現行 16 セルが削除も増設もされないこと（差分は Generated の焼き込み再出力が出ても、HandAuthored 南辺 4 + Environment 4 が `git status` に出なければよい）
7. PR（base: `develop`）→ cursor[bot] → 本書 §7 / §8 のうち S-3 分を埋める。**本書は Bounds と季節スライスの harvest まで `git rm` しない**（§0.3 実測と S-3 記録のため）
8. **`pwsh tools/run-tests.ps1` は実装者が走らせない。** Phase C が Unity を閉じてから全緑を取る

### 4.2 S-4 以降

構図の実装手順は [SEASON_WORLD_DESIGN.md](SEASON_WORLD_DESIGN.md) §6（既存 16 セルは全廃。移送しない）。
空間の口は [SCENE_WORLD_BOUNDS.md](SCENE_WORLD_BOUNDS.md)。矩形 4 つ + 空隙への Catalog 書き込みは行わない。

---

## 5. 受入条件

### 5.1 S-3

| # | 条件 | 判定方法 |
|---|---|---|
| A-1 | 全テスト緑 | Phase C が `pwsh tools/run-tests.ps1`。failed 0。**テスト 0 件は失敗扱い。実装者は走らせない** |
| A-2 | 本番セルが動いていない | 南辺 4 Cell + Environment 4 の identity / フォルダが S-3 前後で同じ |
| A-3 | 複数矩形はテストでしか存在しない | 本番 Catalog の矩形数 = 1。T-A / T-B の 2 矩形はテストフィクスチャ |
| A-4 | FW に季節の語彙が漏れていない | `unity/Assets/OneStarMaker/` を Season / 季節で grep → 0 件（D-1） |
| A-5 | 既存 Streaming テストが残っている | WSC 10 本相当が単一矩形入力で緑 |

### 5.2 S-4 以降

受入は [SEASON_WORLD_DESIGN.md](SEASON_WORLD_DESIGN.md) §10 と [SCENE_WORLD_BOUNDS.md](SCENE_WORLD_BOUNDS.md) §4。空隙へ出て desired が空、は同座標構図では使わない。

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
