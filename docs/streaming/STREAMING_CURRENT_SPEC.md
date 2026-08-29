# Streaming — 現状仕様

> ステータス: **今動いている実装の正本**（2026-08-29）。到着点ではない。
> 到着契約: [§34 OnDemand の空間政策](../../unity/Assets/Docs/Architecture/34-ondemand-spatial-policy.md)
> 対照: [STREAMING_CURRENT_VS_IDEAL.md](STREAMING_CURRENT_VS_IDEAL.md)
> 設計記録・チケット履歴: [§21](../../unity/Assets/Docs/Architecture/21-scene-streaming.md)
> UpdateSystem の [UPDATER_CURRENT_SPEC.md](../updater/UPDATER_CURRENT_SPEC.md) に相当する。

本書は格子キーの一般化先ではない。S-3（矩形集合化）までを含む**現況**である。

---

## 1. 一文

**desired set は、セル座標列を名前に Format し、格子定数で中心を組み立て、注視点との XZ 点距離で切る。**

```
Config.Cells（Vector2Int）→ CellIdentity.Format → id
                ↘ GetCellCenter(x, y) → 注視点との XZ 点距離
```

`WorldStreamingController` は毎 Tick `Format` で無修飾 id を自前生成する。AABB を読まない。体積はデータの正本ではない。

---

## 2. 実装値（S-3 後）

| 項目 | 値 | 所在 |
|---|---|---|
| 本番レイアウト | 矩形 1 個 `{ origin=(0,0), size=(4,4) }`。展開すると 16 セル | `WorldCellCatalog.Rectangles` |
| 格子定数 | `Origin = (0,0,0)` / `CellSize = 250` / `CellHeight = 96` | `WorldCellCatalog` |
| 半径 | `LoadRadius = 375` / `UnloadRadius = 550` / `MaxInFlight = 2` | 同上 |
| Tick | 0.2s（5Hz 相当） | 同上 |
| スポーン | `Cell_0_0` 中心上空（高さ 28） | `WorldCellCatalog.SpawnPosition` |
| 飛行速度 | `FlyController._moveSpeed = 42` m/s（ブースト 2.4 倍で約 100 m/s） | `FlyController.cs` |
| 正本 policy | 南辺 4 枚 `(0,0)(1,0)(2,0)(3,0)` = `HandAuthored`、他 12 枚 = `Generated` | `CellAuthoringPolicy.cs` |
| セル実体 | 16 フォルダ。Environment `.unity` は南辺 4 枚のみ | `SampleGame/.../World/Cells/` |
| Variant | `.asset` の `Variant:` は **52 ファイル全て空文字**。非空値ゼロ | SceneMap / Cells / SceneGraphData |
| Addressables | グループは `Default Local Group` **1 個**（28 エントリ）。`Remote.LoadPath` 未定義。`RemoteFull.asset` / `VariantHybridPlayModeScript.asset` はメニュー実行待ちで未生成 | `AddressableAssetsData/` |
| シーン木 | `InGameSession → World → Cell_{x}_{y} → Environment_{x}_{y}` | `World` は `NecessaryAlways` |

グリッド寸法の正本は `WorldCellCatalog` の const。`WorldGridDefinition.asset` はその写し（`EnsureGridDefinition` が毎回上書き）。アセット側だけを書き換えてもランタイムは追従しない。

---

## 3. ランタイム経路

| 型 | アセンブリ | 役割 |
|---|---|---|
| `StreamingConfig` | FW Runtime | `CellGridConfig` + `IReadOnlyList<Vector2Int> Cells` + 半径 + `maxInFlight`。矩形も季節も知らない。空集合は例外 |
| `WorldStreamingController` | FW Runtime | 毎 Tick `Cells` を走査し `CellIdentity.Format` する。desired / retain / ヒステリシス / in-flight / 距離順 priority。current は持たず `IsLoaded` で再照合（G-6） |
| `ISceneStreamingBackend` | FW Runtime | `RequestAdd` / `RequestRemove` / `IsLoaded`。SceneDirector 委譲 |
| `CellIdentity` | FW Runtime | `Cell_{x}_{y}` の判定・解析・整形。R-3（`SwitchScene` 禁止）の検出もこれ |
| `CellGridConfig` / `CellScene` | FW Runtime | 原点・セルサイズ・高さ。`CellScene.ComputeBounds` はテストのみ（本番経路からは呼ばれない） |
| `WorldCellCatalog` | SampleGame | 矩形集合の展開・membership・スポーン・tint |
| `SessionWorldStreamingDriver` | SampleGame | Catalog の全セル列挙を Config へ渡す |

S-3 が変えたのは走査範囲だけである。dense `0..W × 0..H` を矩形の展開結果に替えた。キーは `Vector2Int` のまま、Controller は毎 Tick `Format` する。

`TryGetCoordinate` は Origin / CellSize で floor したあと集合 membership。AABB 内でも空隙なら false。本番は矩形 1 個なので空隙は無い。複数矩形の挙動はテストフィクスチャだけ。

---

## 4. 名前文法が空間になっている箇所

距離経路の外でも、座標が主キーである。

| 箇所 | 何をしているか |
|---|---|
| `GameSceneFactory.IsCellId` / `EnvironmentIdentity.IsEnvironmentId` | SceneBase 結線 |
| `CellScene` の ctor | `Cell_{x}_{y}` でなければ throw |
| `EnvironmentIdentity.TryFromCellId` | 親名から子名を組み立て |
| `ThrowIfCellIdentity`（R-3） | `IsCellId` で `SwitchScene` を拒否 |
| 生成器 `CollectExistingStates` / `CellPopulationPlan` | フォルダ名を `TryParse` し座標を辞書キー |
| `CellAuthoringPolicy.Resolve(Vector2Int)` | policy のキーが座標 |

到着点（§34）では、政策のキーは identity 文字列、R-3 は距離政策の候補フラグ、生成器の既存収集も identity 文字列である。本章はそれを実装していない。

---

## 5. 生成器と非破壊契約

判定は `CellPopulationPlan`（純関数）に閉じる。policy データは SampleGame。FW の `WorldGridDefinition` に policy を足さない。`SceneResource` にフラグを足さない。

| policy | 再生成時 |
|---|---|
| `Generated` | Cell / Environment とも常に上書き |
| `HandAuthored` | `AuthoredRoot` があれば触らない |

手編集を壊し得る経路は 3 つ。すべて計画経由。

1. Cell シーンへの書き込み
2. Environment シーンへの書き込み
3. **グリッド範囲外** Cell フォルダの削除 — `HandAuthored` は範囲外でも削除しない

南辺ハードコードが 4 箇所ある（`HandAuthoredCells` / `EnvironmentSproutCells` / `HandEditProbe` / 生成器完了ログ）。`EnvironmentSproutCells` は二役（Environment 子を作る / Ground を置かない）。

§20 の Variant 機構（`VariantFilteringBuildScript` / whitelist / Hybrid Play / `TryLoadRemoteCatalogAsync` / `RemoteCatalogRuntimeBridge`）は実装済み。所在は `OneStarMaker/Scripts/Editor/Build/Variants/`。データ（タグ・グループ・プロファイル）は流し込まれていない。

---

## 6. テストと計測

- テストは全て EditMode。**WSC 10 本相当（T-B 空隙を入れて 11）+ MultiFocus 3 + 統合 6 / 生成器 7 / `CellPopulationPlan` 14** ほか
- CI（GitHub Actions）は DebugStudio の `dotnet test` のみ。Unity テストはローカル `pwsh tools/run-tests.ps1`
- [§21](../../unity/Assets/Docs/Architecture/21-scene-streaming.md) の T-07〜T-09（Play 実証・テレメトリ・受入判定）は未了。季節化のあとに取る

---

## 7. 維持してよい現状判断（到着点でも残る）

政策 / メカニズム分離、LoadType 3 値、ヒステリシス、`maxInFlight`、距離順 priority は**現状でも既にそうなっている**。到着点で残すものの一覧は [§34 §8](../../unity/Assets/Docs/Architecture/34-ondemand-spatial-policy.md) が持つ。ここへ逐語で写さない。
