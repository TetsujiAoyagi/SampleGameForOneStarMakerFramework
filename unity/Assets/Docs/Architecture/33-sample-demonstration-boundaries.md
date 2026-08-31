# 33. SampleGame 実証境界 — Season / Tunnel / 4 動詞

> ステータス: **部分退役。** 世界は作業台の世界稿。空間の到着契約は [§34](34-ondemand-spatial-policy.md)。現状実装は [§21](21-scene-streaming.md) / [STREAMING_CURRENT_SPEC.md](../../../../docs/streaming/STREAMING_CURRENT_SPEC.md)。4 動詞の検証マトリクスも作業台が新しい。本文は harvest まで残す。
> **コードは本章から書き始めない。** 本文の空隙レイアウト（D-1 / §7）に戻らない。
> [ARCHITECTURE.md](../../ARCHITECTURE.md) に戻る
> 関連: [18-asset-description.md](18-asset-description.md)、[20-variant-checkout-workflow.md](20-variant-checkout-workflow.md)、[21-scene-streaming.md](21-scene-streaming.md)、[27-folder-structure.md](27-folder-structure.md)、[05-scene.md](05-scene.md)

本章は **SampleGame が何をどこで実証するか** を所有する。フレームワークの契約ではなく、**検証用ゲームの構造の契約**である。

### 退役表（本文より優先する）

| 本文 | 退役理由 | 今の正本 |
|---|---|---|
| D-1 空隙で季節矩形を離す。`CellIdentity` 不変 | 四季は同じ座標の四変奏。identity 文法を空間プロトコルにしない | §34。世界構図は作業台 |
| §5 季節↔動詞、季節別 policy | 実証の目的は全変奏×全ワークフロー | 作業台の検証マトリクス |
| §10「`CellIdentity` 書式は不変」「Controller のポリシー実装は変更不要」 | 到着点では政策のキーは identity、体積はデータ | §34 |
| §12 S-3 を「レイアウト確定 + 矩形集合化」とする行 | S-3 は矩形集合化まで（現状）。構図は混ぜない | 現状仕様。移行は作業台 |
| §7 空隙の幾何、O-1 を S-3 で座標確定 | 計画セッションに構図を確定させない | 作業台の世界構図 |

季節（Season）という語が出てくるが、これはコンテンツのテーマではない。**独立に出荷でき、独立にチェックアウトでき、独立に人が触れる単位**に名前を付けたものである。テーマを剥がしても構造は残る。

Cell ストリーミングそのものの設計は [§21](21-scene-streaming.md) が持つ。本章は §21 を前提に、その上へ 3 つの境界を足す。

---

## 目次

1. [一文](#1-一文)
2. [目的・スコープ](#2-目的スコープ)
3. [用語 — 境界 / 継ぎ目 / 実証担当](#3-用語--境界--継ぎ目--実証担当)
4. [4 動詞と境界の対応](#4-4-動詞と境界の対応)
5. [季節ごとの実証責務](#5-季節ごとの実証責務)
6. [設計判断](#6-設計判断)
7. [空間配置 — 幾何の導出](#7-空間配置--幾何の導出)
8. [シーン木と寿命](#8-シーン木と寿命)
9. [動詞ごとの境界の実体](#9-動詞ごとの境界の実体)
10. [既存契約に触れないこと](#10-既存契約に触れないこと)
11. [今やらない](#11-今やらない)
12. [実装スライス（S-3 以降）](#12-実装スライスs-3-以降)
13. [オープン論点](#13-オープン論点)

---

## 1. 一文

**SampleGame は Streaming のデモではない。Build / Commit / Checkout / Streaming の 4 つを一度に試せる器である。**

4 つの動詞はそれぞれ**別の性質の境界**を要求する。境界が 1 本しかないと 1 つしか実証できない。現況（2026-08-27）はフラットな `InGameSession → World → Cell_x_y` の 1 グリッド、Addressables グループ 1 個、`AssetPayload.Variant` は全て空文字であり、**実証できているのは Streaming だけ**である。

---

## 2. 目的・スコープ

**本章が証明すること（将来）:**

- 同じワールドの上で、4 つの動詞が互いを壊さずに同居する
- 「生成物が正本の領域」と「手編集が正本の領域」が同居する（[§21 §6](21-scene-streaming.md) の非破壊契約の回収先）
- チェックアウトしていない領域があっても遊べる。取得は境界の単位で起きる

**非スコープ:**

- HLOD / Proxy ティア（§22 予約。[§21 §12](21-scene-streaming.md)）
- ゲームとしての面白さ。季節はテーマではなく単位の名前
- `CellIdentity` 書式の変更、`WorldStreamingController` のポリシー変更（[§10](#10-既存契約に触れないこと)）

**前史:** 季節 Level は過去に一度実装され、明示的に取り外された（`IInGameSessionServices` が「旧 LevelStreamCoordinator / トンネル演出口は廃止済み」と書いている）。**現行のフラット構成は季節構想の「前」ではなく「後」である。** 本章は新規設計ではなく、外したものを設計しなおして戻す。

---

## 3. 用語 — 境界 / 継ぎ目 / 実証担当

**この 3 つを混ぜないこと。** 過去に混ぜて実際にレビューで問題になった。

| 語 | 意味 | 実体 |
|---|---|---|
| **境界** | 独立に出荷 / 取得 / 編集できる単位 | Season / Cell / Environment |
| **継ぎ目 (seam)** | 境界と境界のあいだを渡す仕掛け | Tunnel |
| **実証担当** | どの動詞をどこで見せるか | 季節（春 / 夏 / 秋 / 冬） |

**Tunnel は Build 単位ではない。** 継ぎ目であって境界ではないので、Addressables グループも Variant タグも持たない。季節ごとに入口 / 出口を作らない（[D-4](#6-設計判断)）。

---

## 4. 4 動詞と境界の対応

| 動詞 | 要求する境界の性質 | 担い手 |
|---|---|---|
| **Streaming** | 距離で頻繁に跨ぐ・小さい・多数 | `Cell_{x}_{y}`（250m 角・`LoadType.OnDemand`） |
| **Build** | バンドル / グループに一致し、独立に出荷できる | Season Level（季節ごとの Addressables グループ） |
| **Checkout** | 一部だけ sparse-checkout しても Play できる | Season の `AssetPayload.Variant` タグ |
| **Commit** | 同じ空間を複数の職種が同時に触れる | 同一 Cell フォルダ内の**職種別 `.unity` 分割** |

Cell を増やしても Streaming の証明力は上がらない（受入条件は「半径を跨ぐ移動」で成立する）。**Cell を増やす提案は却下してよい。**

---

## 5. 季節ごとの実証責務

**四季を同じものの 4 コピーにしない。** 各季節に別の責務を割り当てる。こうするとコンテンツ量は 1 グリッド + トンネルで足り、README から「どの動詞がどこで実証されているか」を指させる。

| 季節 | 実証担当 | 要る大きさ | 正本 policy |
|---|---|---|---|
| 春 | **Commit** — 同一 Cell を地形担当と追加職種が同時に触る | 2×2 で足りる | `HandAuthored` |
| 夏 | **Streaming** — ロード / アンロード半径を跨ぐ飛行。[§21 §9](21-scene-streaming.md) の A-1〜A-5 はここで測る | **4×4 が要る**（半径を跨ぐ移動距離のため） | `Generated` |
| 秋 | **Checkout** — sparse-checkout していない季節がリモートから来る | 2×2 で足りる | `Generated` |
| 冬 | **Build** — この季節だけ差し替えビルドし、他季節のバンドルを触らない | 2×2 で足りる | `Generated` |

**矩形の寸法は季節ごとに違ってよい。** 実証責務が要求する最小で足りる。合計 4 + 16 + 4 + 4 = 28 セル。

> **「二人が別々の Cell を触る」は Commit の証明にならない。**
> Cell が別ファイルなのは Streaming 境界を切った副産物にすぎない。証明すべきは**同一 Cell 内で職種が衝突しない**ことである（[§9.3](#93-commit-境界--職種別-unity)）。

---

## 6. 設計判断

| # | 決定 | 理由 |
|---|---|---|
| **D-1** | **単一座標空間**に 4 つの季節矩形を、`UnloadRadius` を超える空隙で離して置く。`CellIdentity`（`Cell_{x}_{y}`）は不変 | 季節ごとに別座標空間を持ち identity に季節名を入れると、`CellIdentity` の書式と [§21 R-3](21-scene-streaming.md) の SwitchScene ガードに手が入る。**FW 側の型に Game の運用概念（季節）が漏れる**（[§21 §6](21-scene-streaming.md)「policy データの所在」と同じ理由） |
| **D-2** | シーン木は `InGameSession → Season_* → Cell_{x}_{y}`。**現行の `World` を Season_* が置き換える**（World が 4 つになる） | `World` は「セルの親」以上の意味を持っていない。季節を挟むと中間コンテナが 2 段になり、寿命の意味が重複する |
| **D-3** | Season Level の `LoadType` は `OnDemand`。Cell は Season の子で `OnDemand` のまま | トンネル滞在中に次の季節を明示 `AddScene` する。Season の Unload で配下 Cell が再帰破棄されるのは現行の World Unload と同じ機構であり、新しい寿命規則を足さない |
| **D-4** | Tunnel は `InGameSession` 直下・`LoadType.NecessaryAlways` の**常設 1 本**。季節ごとの入口 / 出口を作らない | 継ぎ目を境界と同数にすると、継ぎ目自体が出荷単位に見えてしまう。季節ごとの見た目の差が要るなら Scene を増やさず Variant で分ける |
| **D-5** | 未 Checkout の季節はリモートカタログから解決する（[§20](20-variant-checkout-workflow.md) の RemoteResolve 経路）。解決不能ならトンネル出口で**明示的に失敗**し、直前の季節へ戻す | 新機構を足さずに §20 の既存経路へ載せる。暗黙のフォールバック（春などへの既定遷移）は作らない — 「取得していない」ことが観測できなくなる |
| **D-6** | 正本 policy は春 = `HandAuthored`、夏 / 秋 / 冬 = `Generated` | [§21 §6](21-scene-streaming.md) の非破壊契約は、この割り当てのために作られている。既存の `CellAuthoringPolicy`（南辺 4 枚を `HandAuthored`）は春の矩形へ移す |

### 却下: 1 グリッドを 4 行に割る案

既存の 4×4 グリッドの行 `y=0..3` を四季に割り当て、Scene ノードを増やさずタグだけで季節を表す案は**採らない**。

`WorldCellCatalog` は `CellSize = 250f` / `LoadRadius = 375f` である。**隣接行のセル中心は 250m 先で、load 半径 375m の内側にある。** 季節を隣接行に置くと、春を飛んでいるだけで夏のセルが desired set に入る。未 Checkout の季節があると「トンネル明けに始まる」のではなく、**春の飛行中に存在しないセルを要求して落ちる。**

**トンネルは演出ではなく、未取得の季節を load 半径の外へ隔離する装置である。** これを外すと Checkout の実演は構造的に不可能になり、残るのはグループ名と Variant 文字列というメタデータだけになる。

---

## 7. 空間配置 — 幾何の導出

季節矩形どうしの空隙は、**最も近いセル中心間の距離が `UnloadRadius` を超える**ように取る。desired set に入れない（`LoadRadius`）だけでなく、retain にも残さない（`UnloadRadius`）ことを条件にする。

現行定数（`WorldCellCatalog`）で計算すると:

| 空隙（空きセル数） | 最近セル中心間距離 | `UnloadRadius = 550m` を超えるか |
|---:|---:|---|
| 1 | 500m | ✗ |
| 2 | 750m | ✓ |
| **3** | **1000m** | ✓（採用。定数を動かしたときの余裕を持たせる） |

```
 ┌─────────┐  空隙   ┌───────────────┐  空隙   ┌─────────┐  空隙   ┌─────────┐
 │ 春 2×2  │◄─ 3 ──►│    夏 4×4     │◄─ 3 ──►│ 秋 2×2  │◄─ 3 ──►│ 冬 2×2  │
 │ Commit  │ セル分  │   Streaming   │ セル分  │ Checkout│ セル分  │  Build  │
 └─────────┘         └───────────────┘         └─────────┘         └─────────┘
      ▲                    ▲                        ▲                   ▲
      └────────────────────┴────────────────────────┴───────────────────┘
        Tunnel（InGameSession 直下・常設 1 本）が、どの空隙も同じ 1 本で渡す
```

- 矩形の並びと座標は S-3 で確定する。**本章が固定するのは「空隙 > `UnloadRadius`」という条件だけ**であり、レイアウトそのものではない
- `WorldCellCatalog` の dense な `GridWidth × GridHeight` は「**季節矩形の集合**」へ一般化する必要がある。グリッド寸法の正本は引き続き `WorldCellCatalog` の const 側であり、`WorldGridDefinition.asset` はその写しである（[§21 §6](21-scene-streaming.md)）

> ⚠️ **既存 16 セルの座標は変わる。** フォルダ名が identity なので、季節配置を決めた時点で「グリッド範囲外」の定義も変わる。生成器の破壊経路 3（範囲外 Cell フォルダの削除）が発火し、`Generated` な既存セルは消える。`HandAuthored` は範囲外でも守られる（[§21 R-6](21-scene-streaming.md)）が、**移送の手順は S-3 の最初の論点**である。

---

## 8. シーン木と寿命

```
Main (ルート)
  └── InGameScene (コンテナ)
        └── InGameSession
              ├── Tunnel (LoadType.NecessaryAlways)        ← 継ぎ目。常設 1 本
              ├── Season_Spring (OnDemand)                 ← 出荷・取得・編集の境界
              │     ├── Cell_0_0 (OnDemand)                ← 距離ストリーミング境界
              │     │     └── Environment_0_0 (OnDemand)   ← 職種作業単位（引っ張られない）
              │     └── ...
              ├── Season_Summer (OnDemand)
              ├── Season_Autumn (OnDemand)
              └── Season_Winter (OnDemand)
```

| 寿命の規則 | 内容 |
|---|---|
| 季節の入場 | トンネル滞在中に次の Season を明示 `AddScene`。ロード隠蔽はトンネルが担う |
| 季節の退場 | `UnloadScene("Season_*")` で配下 Cell / Environment が再帰破棄される |
| InGame 退出 | `InGameSession` ごと Unload。Season も Tunnel も一緒に落ちる |
| Tunnel の寿命 | `InGameSession` と同じ。季節をまたいでも生き続ける |
| Cell の出入り | 従来どおり `WorldStreamingController` のみが決める。**Controller は Cell identity だけを見る**（[§21 §4 CCS](21-scene-streaming.md)） |

Controller は「今いる季節の矩形」を desired set の母集合として受け取る。**季節をまたぐ距離計算はしない** — 空隙が `UnloadRadius` を超えているので、隣の季節のセルは幾何的に候補へ入らない。したがって Controller のポリシー実装は変更不要である（[§10](#10-既存契約に触れないこと)）。

> **現行との差:** 今の `World` は `NecessaryAlways` で、`InGameSession` の親ロード時に必ず載っている（`InGameSessionScene.OnStabledImpl` のコメントがそう書いている）。Season_* は `OnDemand` なので、**InGameSession に入っただけではどの季節も載らない。** 初回季節を誰が明示 Add するか（トンネルから始めるか、Session が初回だけ Ensure するか）は S-4 の論点である。

---

## 9. 動詞ごとの境界の実体

### 9.1 Build 境界 — 季節ごとの Addressables グループ

現況、セルの Addressables 登録は全て `settings.DefaultGroup` へ入る。これを**季節ごとのグループ**へ分ける。

- 冬だけを差し替えビルドしても、他季節のバンドルが変わらないことを示す
- グループ境界 = 季節境界 = 出荷単位、を一致させる。3 つがずれると「独立に出荷できる」が言えなくなる
- Tunnel は継ぎ目なので専用グループを持たない（[§3](#3-用語--境界--継ぎ目--実証担当)）

### 9.2 Checkout 境界 — 季節の `AssetPayload.Variant` タグ

`AssetPayload.Variant` は Framework が意味を解釈せず、`BuildVariantProfile` の whitelist と完全一致で判定される（[§18](18-asset-description.md)、[§20](20-variant-checkout-workflow.md)）。ここに**季節名を入れる**。現況は全て空文字である。

実演の形:

1. 秋を sparse-checkout していない開発者が Editor Play する
2. トンネルに入り、秋へ抜ける
3. 秋のセルがリモートバンドルから解決されて始まる（[§20 §3.3](20-variant-checkout-workflow.md) のハイブリッド Play 経路）
4. リモートも解決できなければ、トンネル出口で明示的に失敗し、直前の季節へ戻る（D-5）

**未 Checkout の季節が load 半径の内側に居ないことが前提**なので、[§7](#7-空間配置--幾何の導出) の空隙条件はこの実演の必要条件である。

### 9.3 Commit 境界 — 職種別 `.unity`

同一 Cell フォルダの中を職種で割る。これは既に [§21 CCS](21-scene-streaming.md) で構造だけ存在している（南辺 4 枚に Environment の萌芽がある）。

```
Cells/Cell_0_0/
  Cell_0_0.unity          ← 地形担当が正本
  Environment_0_0.unity   ← 追加職種が正本
```

- **2 人が同じ Cell を同時に触っても、触るファイルが違うので衝突しない。** これが Commit の証明であって、「二人が別々の Cell を触る」ことではない
- 春は `HandAuthored` なので、生成器を再実行しても両方の `AuthoredRoot` が保持される（[§21 R-6](21-scene-streaming.md)）
- `.gitattributes` は `*.unity merge=unityyamlmerge` を宣言しているが、**マージドライバは未設定で効いていない。** 職種分割はドライバに依存せずに衝突を避ける設計であり、ドライバ設定は前提条件ではない（あれば なお良い、という位置づけ）

---

## 10. 既存契約に触れないこと

本章の設計は、次を**変更しない**。実装スライスがこれらに手を入れたくなったら、それは設計が間違っている合図である。

| 契約 | 所在 |
|---|---|
| `CellIdentity` の書式（`Cell_{x}_{y}`） | `SampleGame/InGame/InGameSession/World/CellScenes/` |
| セル制作規約 R-1〜R-6 | [§21 §7](21-scene-streaming.md) |
| `WorldStreamingController` のポリシー仕様（desired set・ヒステリシス・in-flight 上限） | [§21 §8](21-scene-streaming.md) |
| `SceneState` の 14 状態と enum 順序 | [§5](05-scene.md) |
| asmdef 依存方向（`SampleGame.InGame` ↔ `OutGame` 禁止、FW → Game 禁止） | [ARCHITECTURE.md](../../ARCHITECTURE.md) |
| `CellAuthoringPolicy` を FW 側に置かない | [§21 §6](21-scene-streaming.md)「policy データの所在」 |

変わるのは **`WorldCellCatalog`（dense グリッド → 季節矩形の集合）**、**生成器の Addressables グループ指定**、**シーン木に Season / Tunnel が増えること** の 3 点である。

---

## 11. 今やらない

| 項目 | 理由 |
|---|---|
| HLOD / Proxy ティア | §22 予約。季節境界とは独立 |
| 季節ごとの入口 / 出口シーン | D-4。継ぎ目は 1 本 |
| `CellIdentity` 書式の変更 | D-1。FW へ季節が漏れる |
| ランタイム Variant 選択の配線 | [§18](18-asset-description.md) の未配線項目。Checkout の実演は**ビルド時 / Play 時のカタログ構成**で成立するので要らない |
| 季節ごとのゲームルールの差 | サンプルの目的は境界の実証であってゲームではない |
| 実装 | 本章はコード 0 行。S-3 以降 |

---

## 12. 実装スライス（S-3 以降）

**受入条件は各スライスの着手時に確定させる。** ここでは分解と順序だけを固定する。

| # | 内容 | 前提 |
|---|---|---|
| S-3 | 季節矩形のレイアウト確定 + `WorldCellCatalog` の一般化（dense → 矩形の集合）+ 既存 16 セルの移送手順 | 本章。**破壊経路 3 の再確認から始める** |
| S-4 | Season Level の復活（D-2 / D-3）。生成器が Season ノードを吐き、`World` を置き換える | S-3 |
| S-5 | Tunnel（D-4）。常設 1 本、ロード隠蔽と明示 `AddScene` | S-4 |
| S-6 | 季節ごとの Addressables グループ（[§9.1](#91-build-境界--季節ごとの-addressables-グループ)）= **Build 実証（冬）** | S-4 |
| S-7 | 季節の Variant タグ + 未 Checkout 経路（[§9.2](#92-checkout-境界--季節の-assetpayloadvariant-タグ)）= **Checkout 実証（秋）** | S-5, S-6 |
| S-8 | 春の職種別コンテンツ投入 = **Commit 実証**。ここで `HandEditProbe` と生成器のスキャフォールド宣言を退役させる | S-3 |
| S-9 | 夏で [§21](21-scene-streaming.md) の T-07〜T-09（実証スライス・テレメトリ・受入判定）を回す = **Streaming 実証** | S-4 |

**§21 の T-07〜T-09 を S-9 まで動かさないこと。** 中身の薄いセルで A-1（フレームスパイク）・A-2（ロード p95）を測っても、数値が季節化のあとで取り直しになる。

---

## 13. オープン論点

| # | 論点 | 現状 |
|---|---|---|
| O-1 | 季節矩形の具体的なレイアウトと座標 | S-3 で確定。本章は空隙条件のみ固定 |
| O-2 | 既存 16 セルのうち `Generated` な 12 枚を移送するか捨てるか | 捨てるほうが安い可能性がある（生成物なので再生成できる）。S-3 で判断 |
| O-3 | トンネルの長さ = 次の季節のロードを隠せる時間 | 実測が要る。[§21](21-scene-streaming.md) のセルロード p95 が出てから決める |
| O-4 | Season Level 自身が中身を持つか（空のコンテナか） | 空コンテナ寄り。持たせると Season と Cell の役割が曖昧になる |
| O-5 | `unityyamlmerge` ドライバを設定するか | [§9.3](#93-commit-境界--職種別-unity)。職種分割の前提条件ではないが、あると Commit の実演が強くなる |
