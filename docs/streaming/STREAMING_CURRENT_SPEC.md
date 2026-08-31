# Streaming — 現状仕様

> ステータス: **今動いている実装の正本**（2026-09-01）。到着点ではない。
> 到着契約: [§34 OnDemand の空間政策](../../unity/Assets/Docs/Architecture/34-ondemand-spatial-policy.md)
> 対照: [STREAMING_CURRENT_VS_IDEAL.md](STREAMING_CURRENT_VS_IDEAL.md)
> 設計記録・チケット履歴: [§21](../../unity/Assets/Docs/Architecture/21-scene-streaming.md)
> UpdateSystem の [UPDATER_CURRENT_SPEC.md](../updater/UPDATER_CURRENT_SPEC.md) に相当する。

本書は格子キーの一般化先ではない。S-3（矩形集合化）と移行 M-1〜M-4 を含む**現況**である。**実装指示ではない。**

---

## 1. 一文

**desired set は、候補（identity ＋ 体積）を注視点との XZ 点距離で切る。**

```
StreamingCandidateSet（identity ＋ Bounds）→ 体積の中心 → 注視点との XZ 点距離
```

`WorldStreamingController` は identity を組み立てない。格子座標も格子定数も読まない。体積は `SceneResource` のデータであり、Editor が `.unity` の Renderer から自動計算して焼く（§34 §5）。

生成器の既存収集・policy は identity 文字列をキーにし、R-3 は候補フラグで検出する。セル型と生成器格子型は SampleGame へ移り、FW の公開面に格子文法の型は残っていない。名前文法が残る箇所は §4 に限定する。

---

## 2. 実装値（S-3 ＋ M-1〜M-4 後）

| 項目 | 値 | 所在 |
|---|---|---|
| 本番レイアウト | 矩形 1 個 `{ origin=(0,0), size=(4,4) }`。展開すると 16 セル | `WorldCellCatalog.Rectangles` |
| 格子定数 | `Origin = (0,0,0)` / `CellSize = 250` / `CellHeight = 96`。**生成器入力・スポーン・HUD 用。距離政策は読まない** | `WorldCellCatalog` |
| 体積 | `SceneResource._volume`（ワールド AABB）。`.unity` の全 Renderer の合併 ＋ 候補でない子の合併 | `SceneResource` |
| 体積の収集範囲 | `SceneVolumeSceneReader` は全 `Renderer` を `includeInactive: true` で拾う。Particle / Trail / 無効デバッグメッシュを足して保存すると中心が跳ね得る。Collider のみは寄与しない。規約を足すなら S-4（世界稿 N-9） | 同上 |
| 候補フラグ | `SceneResource._streamByDistance`。**生成器が焼く決定**。Cell に true、Environment に false を書くのは SampleGame Editor の生成器。幾何からは導出しない | 同上 |
| 体積の焼き直し | シーン保存フック ＋ メニュー `OneStarMaker/Scene Volume/Recalculate All` ＋ 生成完了時。**書くのは体積だけ**でフラグには触らない | `SceneVolumeRecalculator` |
| 半径 | `LoadRadius = 375` / `UnloadRadius = 550` / `MaxInFlight = 2` | 同上 |
| Tick | 0.2s（5Hz 相当） | 同上 |
| スポーン | `Cell_0_0` 中心上空（高さ 28） | `WorldCellCatalog.SpawnPosition` |
| 飛行速度 | `FlyController._moveSpeed = 42` m/s（ブースト 2.4 倍で約 100 m/s） | `FlyController.cs` |
| 正本 policy | 南辺 4 枚 `(0,0)(1,0)(2,0)(3,0)` = `HandAuthored`、他 12 枚 = `Generated` | `CellAuthoringPolicy.cs` |
| セル実体 | 16 フォルダ。Environment `.unity` は南辺 4 枚のみ | `SampleGame/.../World/Cells/` |
| Variant | `.asset` の `Variant:` は **52 ファイル全て空文字**。非空値ゼロ | SceneMap / Cells / SceneGraphData |
| Addressables | グループは `Default Local Group` **1 個**（32 エントリ。うち `.unity` は 29）。`.unity` を持つ SceneResource 26 本は全て登録済み（`OutGameScene` / `InGameUI` / `PlayerScene` / `Result` の 4 本は未登録のままで、Title からの Play が `InvalidKeyException` で落ちていた。M-1 の Play 検証時に補った）。`Remote.LoadPath` 未定義。`RemoteFull.asset` / `VariantHybridPlayModeScript.asset` はメニュー実行待ちで未生成 | `AddressableAssetsData/` |
| シーン木 | `InGameSession → World → Cell_{x}_{y} → Environment_{x}_{y}` | `World` は `NecessaryAlways` |

グリッド寸法の正本は `WorldCellCatalog` の const。`WorldGridDefinition.asset` はその写し（`EnsureGridDefinition` が毎回上書き）。アセット側だけを書き換えてもランタイムは追従しない。

---

## 3. ランタイム経路

| 型 | アセンブリ | 役割 |
|---|---|---|
| `StreamingCandidate` | FW Runtime | identity ＋ `Bounds` の値型。空 identity / 空体積は例外 |
| `StreamingCandidateSet` | FW Runtime | 候補列。**差し替えるときは丸ごと作り直す側。** 空集合と identity 重複は例外。防御的コピー |
| `StreamingPolicySettings` | FW Runtime | 半径 2 つ ＋ `maxInFlight`。**ずっと不変な側。** 半径の順序と正値を検証 |
| `WorldStreamingController` | FW Runtime | 毎 Tick 候補列を走査し、体積中心と注視点の距離で切る。desired / retain / ヒステリシス / in-flight / 距離順 priority。current は持たず `IsLoaded` で再照合（G-6）。**格子も名前文法も知らない**。`Candidates` 差し替え口は無い（集合は丸ごと作り直す。口が要るなら S-4 / N-10） |
| `ISceneStreamingBackend` | FW Runtime | `RequestAdd` / `RequestRemove` / `IsLoaded`。SceneDirector 委譲 |
| `ISceneVolumeQuery` | FW Runtime | `TryGetSceneVolume(identity, out Bounds)`。**未ロード**候補の体積を引く口。`ISceneQuery`（ロード済み専用）とは別。未登録 / フラグ off / 空体積を 1 つの `false` に畳む。失敗理由 enum は **開かない**（R-3 は query を使わない） |
| `SceneResource` | FW Runtime | `_volume` / `_streamByDistance` を持つ。体積が空 = 空間に属さない（Title / Pause / Tunnel） |
| `SceneVolumeMath` / `SceneVolumeRecalculator` / `SceneVolumeSceneReader` / `SceneVolumeSaveHook` | FW Editor | 合併規則（純関数）／体積の走査と書き込み／`.unity` 読み取り／保存フック。候補フラグは読むだけ |
| `CellIdentity` | SampleGame Runtime | `Cell_{x}_{y}` の判定・解析・整形。SceneBase 結線と SampleGame の identity 組み立てに残る。**距離経路と R-3 からは外れた** |
| `CellGridConfig` / `CellScene` | SampleGame Runtime | 原点・セルサイズ・高さ。`CellScene.ComputeBounds` はテストのみ（本番経路からは呼ばれない） |
| `WorldCellCatalog` | SampleGame | 矩形集合の展開・membership・スポーン・tint。`CreateGridConfig` は距離経路から外れて参照 0（意図的に残す） |
| `SessionWorldStreamingDriver` | SampleGame | Catalog の identity 列に `ISceneVolumeQuery` の体積を突き合わせて候補集合を作る。1 件でも引けなければ起動時に例外 |

S-3 が変えたのは走査範囲だけだった。M-1 が距離政策のキーを変え、M-2 が生成器のキー、M-3 が R-3 の検出、M-4 が型の所有境界を着地させた。`Vector2Int` 列と `CellGridConfig` は政策層から消え、Controller は identity を組み立てない。

`TryGetCoordinate` は Origin / CellSize で floor したあと集合 membership。AABB 内でも空隙なら false。本番は矩形 1 個なので空隙は無い。複数矩形の挙動はテストフィクスチャだけ。

---

## 4. 名前文法が空間になっている箇所

**距離経路からは消えた（M-1）。残っているのはその外だけである。**

| 箇所 | 何をしているか | 片付ける先 |
|---|---|---|
| `GameSceneFactory.IsCellId` / `EnvironmentIdentity.IsEnvironmentId` | SceneBase 結線 | S-4 |
| `CellScene` の ctor | `Cell_{x}_{y}` でなければ throw | S-4 |
| `EnvironmentIdentity.TryFromCellId` | 親名から子名を組み立て | S-4 |
| `SessionWorldStreamingDriver` | 候補列の identity を `CellIdentity.Format` で組み立て | S-4（候補の出どころが親コンテナの子になる） |

Editor の体積再計算（`SceneVolumeRecalculator`）は名前文法を使わない。親子は `SceneResource.Parent` / `Children`、シーンの所在は payload の GUID で引く。

R-3 は `SceneResource.StreamByDistance`、生成器の既存収集と policy は Ordinal な identity 文字列で着地済みである。同一座標の異なる identity は独立に扱う。

---

## 5. 生成器と非破壊契約

判定は `CellPopulationPlan`（純関数）に閉じる。policy データと `WorldGridDefinition` は SampleGame Editor にある。**`SceneResource` に生成器 policy（`Generated` / `HandAuthored`）を足さない** — `_volume` / `_streamByDistance` は空間のデータであって生成器の判定材料ではない。

| policy | 再生成時 |
|---|---|
| `Generated` | Cell / Environment とも常に上書き |
| `HandAuthored` | `AuthoredRoot` があれば触らない |

手編集を壊し得る経路は 3 つ。すべて計画経由。

1. Cell シーンへの書き込み
2. Environment シーンへの書き込み
3. target identity に含まれない Cell フォルダの削除 — `HandAuthored` は target 外でも削除しない

南辺ハードコードが 4 箇所ある（`HandAuthoredCells` / `EnvironmentSproutCells` / `HandEditProbe` / 生成器完了ログ）。`EnvironmentSproutCells` は二役（Environment 子を作る / Ground を置かない）。

§20 の Variant 機構（`VariantFilteringBuildScript` / whitelist / Hybrid Play / `TryLoadRemoteCatalogAsync` / `RemoteCatalogRuntimeBridge`）は実装済み。所在は `OneStarMaker/Scripts/Editor/Build/Variants/`。データ（タグ・グループ・プロファイル）は流し込まれていない。

---

## 6. テストと計測

- テストは全て EditMode。WSC / MultiFocus / 統合 / 生成器 / `CellPopulationPlan` のほか、M-1〜M-4 の候補集合・体積・identity key・R-3・所有境界を検証する
- 直近の全件実行（2026-08-31）は **525 / 525 passed・failed 0**。M-1〜M-4 の受入を満たしている
- CI（GitHub Actions）は DebugStudio の `dotnet test` のみ。Unity テストはローカル `pwsh tools/run-tests.ps1`
- [§21](../../unity/Assets/Docs/Architecture/21-scene-streaming.md) の T-07〜T-09（Play 実証・テレメトリ・受入判定）は未了。季節化のあとに取る

---

## 7. 維持してよい現状判断（到着点でも残る）

政策 / メカニズム分離、LoadType 3 値、ヒステリシス、`maxInFlight`、距離順 priority は**現状でも既にそうなっている**。到着点で残すものの一覧は [§34 §8](../../unity/Assets/Docs/Architecture/34-ondemand-spatial-policy.md) が持つ。ここへ逐語で写さない。
