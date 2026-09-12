# Streaming — 現状仕様

> ステータス: **今動いている実装の正本**（2026-09-12）。到着点ではない。
> 到着契約: [§34 OnDemand の空間政策](../../unity/Assets/Docs/Architecture/34-ondemand-spatial-policy.md)
> 対照: [STREAMING_CURRENT_VS_IDEAL.md](STREAMING_CURRENT_VS_IDEAL.md)
> 設計記録・チケット履歴: [§21](../../unity/Assets/Docs/Architecture/21-scene-streaming.md)
> UpdateSystem の [UPDATER_CURRENT_SPEC.md](../updater/UPDATER_CURRENT_SPEC.md) に相当する。

本書は格子キーの一般化先ではない。S-3（矩形集合化）、移行 M-1〜M-4、S-4b（四季 World 生成）後の**現況**である。**実装指示ではない。**

---

## 1. 一文

**desired set は、候補（identity ＋ 体積）を注視点との XZ 点距離で切る。**

```
StreamingCandidateSet（identity ＋ Bounds）→ 体積の中心 → 注視点との XZ 点距離
```

`WorldStreamingController` は identity を組み立てない。格子座標も格子定数も読まない。体積は `SceneResource` のデータであり、Editor が `.unity` の Renderer から自動計算して焼く（§34 §5）。

R-3 は候補フラグで検出する。セル型は SampleGame にあり、FW の公開面に格子文法の型は残っていない。名前文法が残る箇所は §4 に限定する。

---

## 2. 実装値（S-4b 後）

| 項目 | 値 | 所在 |
|---|---|---|
| 本番レイアウト | 四季それぞれ 9×6、54 Cell。active Season の直下にある `StreamByDistance` 子だけを候補にする | `SeasonCandidateSelection` / `WorldCellCatalog` |
| 格子定数 | `Origin = (0,0,0)` / `CellSize = 250` / `CellHeight = 96`。**制作座標・スポーン・HUD 用。距離政策は読まない** | `WorldCellCatalog` |
| 体積 | `SceneResource._volume`（ワールド AABB）。`.unity` の全 Renderer の合併 ＋ 候補でない子の合併 | `SceneResource` |
| 体積の収集範囲 | `SceneVolumeSceneReader` は全 `Renderer` を `includeInactive: true` で拾う。Particle / Trail / 無効デバッグメッシュを足して保存すると中心が跳ね得る。Collider のみは寄与しない。規約を足すなら S-4（世界稿 N-9） | 同上 |
| 候補フラグ | `SceneResource._streamByDistance`。Cell は true、Environment は false。幾何からは導出しない | 同上 |
| 体積の焼き直し | シーン保存フック ＋ メニュー `OneStarMaker/Scene Volume/Recalculate All`。**書くのは体積だけ**でフラグには触らない | `SceneVolumeRecalculator` |
| 半径 | `LoadRadius = 375` / `UnloadRadius = 550` / `MaxInFlight = 2` | 同上 |
| Tick | 0.2s（5Hz 相当） | 同上 |
| スポーン | `Spring_Cell_0_4` 中心上空（高さ 28） | `WorldCellCatalog.SpawnPosition` |
| 飛行速度 | `FlyController._moveSpeed = 42` m/s（ブースト 2.4 倍で約 100 m/s） | `FlyController.cs` |
| 制作 policy | S-4b 生成物は全216 Cellが `Generated`。一回限りの生成器と policy コードは生成後に撤去済み | git 履歴 / 生成物 |
| セル実体 | 四季×54。各 Cell は Full と Whitebox payload、Environment 子を持つ | `SampleGame/.../InGameSession/Seasons/` |
| Variant | 全 Cell の既定 payloadは空文字、別 pathに `Whitebox` payloadを持つ。実行中切替はしない | `BuildVariantProfile` / SceneResource payloads |
| Addressables | S-4b の652 Sceneを登録。生成後の検査は Scene/Resource/Graph/Map/Addressables の集合整合を対象とする | `AddressableAssetsData/` |
| シーン木 | `InGameSession → Season_* → *_Lighting / *_Cell_{x}_{y} → *_Environment_{x}_{y}` | SeasonはOnDemand、LightingはNecessaryAlways |

グリッド寸法の正本は `WorldCellCatalog` の const。旧 `WorldGridDefinition.asset` は生成器とともに撤去済みである。寸法変更は生成済みアセット全体に波及するため、新しい設計スライスで扱う。

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
| `CellIdentity` | SampleGame Runtime | `Cell_{x}_{y}` の判定・解析・整形。SampleGame の制作座標処理に残る。**距離経路と R-3、Cell / 職種の SceneBase 結線からは外れた** |
| `CellGridConfig` / `CellScene` | SampleGame Runtime | 原点・セルサイズ・高さ。`CellScene` は `StreamByDistance == true` を要求し、identity を parse しない。`ComputeBounds` はテストのみ（本番経路からは呼ばれない） |
| `WorldCellCatalog` | SampleGame | 9×6 制作座標・membership・`(0,4)` スポーン。runtime identity の正本ではない。`CreateGridConfig` は距離経路から外れて参照 0（意図的に残す） |
| `SeasonCandidateSelection` | SampleGame | active Season 直下の `StreamByDistance` 子と `ISceneVolumeQuery` から候補集合を作る純関数 |
| `SessionSeasonController` | SampleGame | Season枝を直列化し、発行操作と観測個体の終端、距離/companion driver、WorldReadyをSession寿命で所有する |
| `SessionWorldStreamingDriver` | SampleGame | 渡された active Season の候補集合で WSC を駆動する。identity は組み立てない |
| `SessionCellCompanionLoadDriver` | SampleGame | resident Cell の `Children` だけを列挙し、起動時 companion set に入る職種 child を OnDemand Add する。距離政策とは別口 |

S-3 が変えたのは走査範囲だけだった。M-1 が距離政策のキーを変え、M-2 が生成器のキー、M-3 が R-3 の検出、M-4 が型の所有境界を着地させた。`Vector2Int` 列と `CellGridConfig` は政策層から消え、Controller は identity を組み立てない。

`TryGetCoordinate` は Origin / CellSize で floor したあと集合 membership。AABB 内でも空隙なら false。本番は矩形 1 個なので空隙は無い。複数矩形の挙動はテストフィクスチャだけ。

---

## 4. 名前文法が空間になっている箇所

**距離経路からは消えた（M-1）。残っているのはその外だけである。**

| 箇所 | 何をしているか | 片付ける先 |
|---|---|---|
| なし | 距離経路は active Season の子 identity と保存済み体積を使う | 名前文法から分離済み |

`GameSceneFactory` と `CellScene` の結線は `StreamByDistance` / `Parent` に移した。旧 bulk generator、`LegacyWorldAuthoringNames`、無修飾 World identity は削除済み。

Editor の体積再計算（`SceneVolumeRecalculator`）は名前文法を使わない。親子は `SceneResource.Parent` / `Children`、シーンの所在は payload の GUID で引く。

R-3 は `SceneResource.StreamByDistance` で判定する。同一座標の異なる identity は独立に扱う。

---

## 5. 生成物と制作契約

S-4b の 216 Cell と関連 SceneResource は一回限りの生成で確定したアセットである。生成器、`CellPopulationPlan`、`CellAuthoringPolicy`、`WorldGridDefinition`、再実行メニューは最終 HEAD から撤去済みで、通常の制作経路として復活させない。

- Cell / Environment シーン、SceneResource、親子 Graph、SceneMap、Addressables 登録を一つの整合集合として扱う
- `_volume` と `_streamByDistance` は空間政策のデータであり、制作状態を表す `Generated` / `HandAuthored` フラグを `SceneResource` に追加しない
- 将来、格子の再生成や一括置換が必要になった場合は、新しい設計スライスで対象集合、手編集保護、検査、撤去までを定義する
- S-4b の生成時・撤去時の生出力は `artifacts/s-4b-p2/` と `artifacts/s-4b-p3/` に残す

§20 の Variant 機構（`VariantFilteringBuildScript` / whitelist / Hybrid Play / `TryLoadRemoteCatalogAsync` / `RemoteCatalogRuntimeBridge`）は実装済み。所在は `OneStarMaker/Scripts/Editor/Build/Variants/`。データ（タグ・グループ・プロファイル）は流し込まれていない。

---

## 6. テストと計測

- テストは全て EditMode。WSC / MultiFocus / 統合、起動時 Season、職種 companion、World Workspace の transaction/recovery を検証する
- 直近の全件実行（S-4b、2026-09-12）は **679 / 679 passed・failed 0**。Streaming 絞り込みは 122 / 122
- CI（GitHub Actions）は DebugStudio の `dotnet test` のみ。Unity テストはローカル `pwsh tools/run-tests.ps1`
- [§21](../../unity/Assets/Docs/Architecture/21-scene-streaming.md) の T-07〜T-09（Play 実証・テレメトリ・受入判定）は未了。季節化のあとに取る

---

## 7. 維持してよい現状判断（到着点でも残る）

政策 / メカニズム分離、LoadType 3 値、ヒステリシス、`maxInFlight`、距離順 priority は**現状でも既にそうなっている**。到着点で残すものの一覧は [§34 §8](../../unity/Assets/Docs/Architecture/34-ondemand-spatial-policy.md) が持つ。ここへ逐語で写さない。
