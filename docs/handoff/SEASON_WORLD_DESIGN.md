# 世界設計「主題と変奏」— S-3C 構図の正本 (2026-08-29 改訂)

> ステータス: **構図は発注者承認済み。** 距離ストリーミングの空間プロトコルは本書が決めない。
> 先に通す口: [SCENE_WORLD_BOUNDS.md](SCENE_WORLD_BOUNDS.md)（Plan。実装分解は別セッション）。
> S-3 の矩形集合化は `develop` にマージ済み。機構の記録は [SEASON_LEVELS_IMPLEMENTATION.md](SEASON_LEVELS_IMPLEMENTATION.md)。
> 旧稿（修飾パース / デコレータ / S-3D を `CellIdentity` の本題にする）は git 履歴にある。**本文には残さない。**
> §33 の D-1（空隙で季節矩形を離す）/ 季節↔動詞 / identity に季節名を入れない、とは食い違う。世界については本書が新しい。§33 への harvest は Bounds の口が通ってから行い、それまで §33 を書き換えない。
> §7 / §8 は欠番（HANDOFF の Phase C / C' と番号を重ねない）。harvest 期限は §12。`docs-audit.ps1` 検査3の対象にしない。

実装エージェントへ: 本書は構図・実証・スライス順序の正本である。`CellIdentity.TryParse` を修飾対応すること、`StreamingConfig` に qualifier を足すこと、Backend デコレータで id を翻訳することは、**本書の指示ではない。** 距離判断は Bounds Plan が先に決める。既存 16 セルの全廃は **S-4**。Bounds の口を通すあいだは現行 4×4 を動かさない。

---

## 0. 一文

**世界はひとつの谷（9×6 セル）であり、四季は同じ座標に載る四つの変奏である。**
ディスク上の区別は SampleGame のフォルダ名（`Spring_Cell_4_2` 等）で行う。
季節の入れ替えはトンネル滞在中の Unload → Add で隠す。ワールドは原点直上の
2.25km × 1.5km に収まり、季節間の座標オフセットもセル座標の写像も無い。

距離判断が読むのは identity 文字列ではなく、各シーンが持つ体積（Bounds Plan）である。

---

## 1. 発注者裁定ログ（旧前提のどれが死んだか）

| 日付 | 裁定 | 効果 |
|---|---|---|
| 2026-08-26 | 生成器で大まかに作り、人/AI の編集を正とする | 既存 `HandAuthored`（`AuthoredRoot` 保護）。新 policy 種別なし。**ただし初期配置は Generated**（§4） |
| 2026-08-29 | 各季節をもっと大きく。予算上限（軟 16 / 硬 64）撤廃 | 総セル 216 を目標寸法とする。S-4 冒頭で生成コストを測り、維持か縮小かを裁定する（§6） |
| 2026-08-29 | **季節ごとの動詞割当を廃止**。実証の目的は「多人数・職種別の同時編集、単独ビルド、単独チェックアウト、イテレーションが世界のどこでも簡単」であること | §33 §4 / §5 の季節↔動詞表は退役。§4 の検証マトリクスに置換 |
| 2026-08-29 | 座標帯オフセット（原点から 25km〜75km）は float 精度・物理の理由で却下 | 撤回済み。象限配置も不要になり撤回 |
| 2026-08-29 | **四季は同じ座標を共有する。** ディスク上は接頭辞付きフォルダ名 | FW が季節語を読んではならない。名前から座標を復元する契約を FW に足さない（空間は Bounds） |
| 2026-08-29 | テーマ「主題と変奏」（時制 × 楽譜の混合）を承認 | §2 |
| 2026-08-29 | 距離の正本を identity 文法にしない。`SceneWorldBounds` 系の Plan を先に通す | [SCENE_WORLD_BOUNDS.md](SCENE_WORLD_BOUNDS.md) |

---

## 2. テーマ「主題と変奏」

谷はひとつの**主題**（楽譜 = 座標の純関数）。四季はそれぞれの**変奏**で、
テンポ（密度）・調（光と霧）・アーティキュレーション（形の崩し方）を自由に解釈してよい。
ただし主題（線・見証・背）は必ず透けて見えること。

| 変奏 | 季節 | 性格 | 10 秒で見えるもの |
|---|---|---|---|
| I 素描 | 春 | 夜明け、薄い霧 | 主題が最も裸に近い。線は破線の床、杭と張り糸と灯が続きを予告する。書きかけであることが美しさ |
| II 密 | 夏 | 真昼、強い距離霧 | 最大密度の機械的な総奏。霧の縁 = ロード半径が天候として見える。世界はあなたの周りでだけ組み上がる |
| III 残響 | 秋 | 夕、琥珀の霧と長い影 | 主題は断片で鳴る。線の近くだけ完全な形、離れると台座と輪郭だけが残る。減衰が景色になる |
| IV 静止 | 冬 | 白、影のない光 | 全部あるのに彩度とコントラストが抜かれ、谷は記憶のように平たい。見証の頂きだけが濃い |

- 桜・紅葉・雪などの四季ポスター既定解は使わない
- 描画トーンは各季節の RenderSettings（霧の色/濃度・環境光・太陽角）+ 既存 tint 機構で作る。
  **新メッシュ・新シェーダ・新アセットパイプラインは投入しない**（Cube / Cylinder / Sphere / 共有 Lit のみ）
- 見証の頂きは各変奏の「署名」。その変奏だけを単独リビルドすると頂きが差し替わる（Build 実演の指差し先）

**品質バー（人の目視。自動テスト化しない）:**

1. どの変奏でも、最初の 3 秒で「同じ場所（主題）だ」と分かり、続く 3 秒で「違う変奏だ」と分かる
2. 判別の根拠は完成度パターンと光であり、色名ではない（グレースケール overhead でも I〜IV を区別できる）
3. どの季節のどのセルを開いても、床（`*_Cell_*.unity`）と印（`*_Environment_*.unity`）の 2 ファイルがあり、どちらを開くべきか迷わない
4. 生成器を再実行しても、昇格済み identity の演奏レイヤ（`AuthoredRoot` 配下）は 1 個も消えない（判定は **S-8a 以降**。S-4 時点は昇格 0。N-8 で実証項目から下ろすならこの項は無効）

変奏の密度・崩しを生成器パラメータで回す対象は、**まだ昇格していないセル**に限る。180 セルを先に `HandAuthored` にすると、パラメータは初回スキャフォールドにしか効かない。

---

## 3. 幾何とディスク上の名前

### 3.1 楽譜マップ（全変奏共通、局所座標 9×6）

```
      x0 x1 x2 x3 x4 x5 x6 x7 x8      1 セル = 250m。谷全体 2250m × 1500m
y5  |  ^  ^  ^  ^  ^  ^  ^  ^  ^     ^ = 背（北の高まり。Generated の計測コリドー）
y4  |  ~  ~  .  .  .  .  .  .  .     ~ = 線（源流は北西）
y3  |  .  .  ~  ~  .  .  .  .  .
y2  |  .  .  .  .  ◇  ~  ~  .  .     ◇ = 見証（曲がり角、局所 (4,2)。頂きが変奏の署名）
y1  |  .  .  .  .  .  .  .  ~  ~     線は南東へ抜ける
y0  |  .  .  .  .  .  .  .  .  .
```

- 線セル: `(0,4)(1,4)(2,3)(3,3)(4,2)(5,2)(6,2)(7,1)(8,1)`
- 見証セル: `(4,2)`（線の曲がり角。tall vertical、遠くから同定できるシルエット）
- 背: `y=5` の行 9 セル
- グリッド定数は現行どおり: `Origin = (0,0,0)` / `CellSize 250` / `LoadRadius 375` / `UnloadRadius 550` / `MaxInFlight 2`
- スポーン: 春（変奏 I）の源流セル `(0,4)` 中心上空。現行コードの `WorldCellCatalog.SpawnPosition` は `Cell_0_0`。S-4 で移す
- 品質バー 1 の判定地点は演奏レイヤがある線上（見証 `(4,2)`、または線の途中 `(2,3)`）。スポーン `(0,4)` ではない。`(0,4)` は S-9 まで Generated（§4）

この座標は **M 前提**（§9）。S-4 頭の実測で S（6×4）に落とすなら楽譜も書き直す。

格子座標は**生成器が AABB を焼く入力**であり、ランタイムが identity から復元するキーではない。

### 3.2 ディスク上の identity（SampleGame のフォルダ規約）

FW は季節語を知らない。次は SampleGame のファイル名の約束である。

```
無修飾:   Cell_{x}_{y}                      （現行 4×4。Bounds の口を通すあいだ存続）
修飾付き: {Qualifier}_Cell_{x}_{y}          例: Spring_Cell_4_2
Environment: {Qualifier}_Environment_{x}_{y} 例: Spring_Environment_4_2
季節コンテナ: Season_Spring 等
Qualifier: Spring / Summer / Autumn / Winter（SampleGame 側の値）
```

- フォルダ名 = シーン identity（現行規約を維持）
- 一意キーは **identity 文字列そのもの**。生成器・policy・既存収集は、フォルダ名 / `SceneResource.Identity` を文字列で照合する。`TryParse` して得た座標を辞書キーにしてはならない（4 季節が `(4,2)` に潰れる）
- `unity/Assets/OneStarMaker/` に `Season|Spring|Summer|Autumn|Winter|季節` を出さない（W-1）

### 3.3 シーン木

```
Main
  └── InGameScene
        └── InGameSession
              ├── Tunnel (LoadType.NecessaryAlways, 常設 1 本)
              ├── Season_Spring (OnDemand)
              │     └── Spring_Cell_{x}_{y} (OnDemand)
              │           └── Spring_Environment_{x}_{y} (OnDemand)
              ├── Season_Summer (OnDemand)  … 以下同型
              ├── Season_Autumn (OnDemand)
              └── Season_Winter (OnDemand)
```

- 現行の `World` ノードは Season_* 4 つに置き換わる（§33 D-2 どおり）
- **常駐する季節はトンネル遷移中を除き常に 1 つ。** 全季節が同じ AABB を占めるため、
  遷移は必ず「旧季節 Unload 完了 → 新季節 Add」の順。重畳を作らない
- 実行時の不変条件: `Season_*` が Stable なのは高々 1 つ。破ったら失敗（ログ受入だけにしない）
- トンネル以外からの季節 `AddScene` は禁止（デバッグ経路もこの関数を通す）

### 3.4 空間プロトコルは本書の外

距離・ヒステリシス・候補集合の持ち方は [SCENE_WORLD_BOUNDS.md](SCENE_WORLD_BOUNDS.md)。
本書が固定するのは「四季は同じ AABB を共有し、候補集合だけが排他」という**使い方**である。

S-4 で 9×6×4 を焼くのは、Bounds の口が現行 4×4 で通ってから。

---

## 4. 検証マトリクス — このサンプルが証明する表

季節ごとに別の動詞を陳列するのではなく、**全変奏 × 全ワークフロー**を証明する。
ただし **春（変奏 I）の演奏レイヤが入った時点で 4 動詞は出荷可能** とする（§6 の S-8）。
他変奏の作り込みは品質バー（W-7）であり、動詞の証明条件ではない。

| ワークフロー | 実現手段（全変奏共通） |
|---|---|
| 2 職種の同時編集 | 全セルに床 `*_Cell_*.unity`（地形職）+ 印 `*_Environment_*.unity`（置き物職）。同じ地点を 2 人が同時に触ってもファイルが違うので衝突しない。**ただし衝突しないのは中身。** `SceneResourceMap.asset`、`Season_*.asset` の `_children`、Addressables 設定は構造変更のたびに全員が触る 1 ファイル。構造は生成器が単独で触る |
| 再生成しても編集が残る | 昇格済み identity = `HandAuthored`（`AuthoredRoot` を R-6 が保護）/ それ以外 = `Generated`。**判定は S-8a 以降**（S-4 時点は昇格 0。N-8 で実証項目から下ろすならこの行は無効） |
| 単独ビルド | 1 変奏 = 1 Addressables グループ。見証の頂きを差し替え → その変奏だけ再ビルド → 他 3 変奏のバンドルはハッシュ不変。**共有 Lit / Primitive / Tunnel は季節グループに入れない**（Common 側） |
| 単独チェックアウト | 1 変奏 = 1 Variant タグ。手元に無い変奏はリモートカタログから解決、解決不能ならトンネル出口で明示失敗し旧季節へ復帰（D-5 継承）。隔離は空隙ではなく **候補集合の排他**（常駐季節が 1 つ） |
| ストリーミング | 全域で動く。**計測（§21 A-1/A-2）だけは変奏 II（夏）の背コリドー**で取る |
| イテレーション | ループ実演: 印を 1 個編集 → 保存 → 生成器再実行（昇格分は消えない）→ Play → 変奏単独の差分ビルド |

**正本 policy（2 段）:**

| 段階 | 領域 | policy | 備考 |
|---|---|---|---|
| 初期（S-4 生成直後） | 全セル（216） | `Generated` | 変奏パラメータを再実行で回せる。S-8 の目視より先に凍結しない |
| 昇格後 | 人が手を入れた identity のみ | `HandAuthored` | 目安は各変奏の線沿い 8〜10 + 見証周辺。**キーは座標ではなく修飾付き identity**。春の `(4,2)` を昇格しても夏の `(4,2)` は Generated のまま |
| 固定 Generated | 各季節の背 `y=5` | 昇格禁止 | 計測の均質性 |
| S-9 まで Generated | 各季節の `y=4` 行 | 昇格禁止（S-9 完了まで） | **中心距離**で y=5 中心から y=4 中心は 250m ≤ LoadRadius 375m。表面距離だと y=3 も desired に入り、線セル `(2,3)(3,3)` の昇格と W-8 が衝突する。距離の基準は [SCENE_WORLD_BOUNDS.md](SCENE_WORLD_BOUNDS.md) §3.3。源流 `(0,4)(1,4)` の演奏は S-9 のあと |

`HandAuthored` は「全セルを手作業で作る」ではない。初回は生成器のスキャフォールド、
手を入れた identity だけ昇格する。「再生成しても編集が残る」はテーブル上の宣言ではなく、
Generated → 編集 → 昇格、の遷移として実演する。

W-8 の前提: **計測フライト中の desired はすべて Generated。**

---

## 5. トンネルと季節遷移

トンネルは未取得季節を空隙で隔離する装置ではない。ロード隠蔽用の滞在空間であり、
Checkout の入口（次の変奏を要求する唯一の正規経路）である。

- `InGameSession` 直下・`NecessaryAlways`・常設 1 本（§33 D-4）
- 物理位置は谷 AABB の外、かつ谷 AABB から **UnloadRadius（550m）以上**離す。
  「重ならない」だけでは、滞在中に谷が先読みされて入れ替えが見える
- **プレイヤーはトンネルへ移動し、入れ替え後に谷へ戻る。**
  「入った場所と同じ座標に出る」は採らない。セル座標の写像は無い。プレイヤー移動はある
- 遷移シーケンス:
  1. プレイヤーがトンネルに入る
  2. 距離政策の Tick を止める
  3. 旧 Season を `UnloadScene`（配下セル再帰破棄）。**Director 上の当該枝の in-flight が 0 になるまで待つ**
  4. 新 Season を `AddScene`（解決不能なら D-5: 出口で明示失敗し旧季節を再 Add。暗黙フォールバック禁止）
  5. 新季節の候補集合で距離政策を再開
  6. 出口を開く
- 隠しきれなかった場合の第二解: トンネル内で明示的な `LoadingDisplay` に落とす。
  滞在時間の実測が外れてスライスを止めない
- テレポート写像（季節 A の `(x,y)` を季節 B の別座標へ送る関数）は存在しない

---

## 6. スライス順序

順序: **Bounds の口 → S-4 → S-5 → (S-6, S-7) → S-8a（春）→ S-9 → S-8b〜d（他変奏）**。
1 スライス = 1 ブランチ = 1 HANDOFF（着手時に切り出す）。

旧 HANDOFF の Editor 操作境界は全スライスに適用する（人間が開いた Editor への CLI のみ可、Unity.exe 起動・テスト実行・YAML 手編集は禁止、テストは Phase C）。`record` 禁止・`#nullable enable`・破棄されうる `UnityEngine.Object` への `?.` / `??` 禁止も同様。

**退役:** S-3D（`CellIdentity` の修飾パース）。Bounds が identity を不透明キーにすれば不要。復活させない。

| # | 内容 | 補足 |
|---|---|---|
| Bounds | 現行 4×4 で、距離政策が AABB を読む口を通す | [SCENE_WORLD_BOUNDS.md](SCENE_WORLD_BOUNDS.md)。**通るまで 9×6×4 を焼かない。現行 16 枚も動かさない。** 実装分解は別セッション |
| S-4 | 谷の生成と季節スワップ（本体） | Season_* 4 ノード、`World` を置き換え。接頭辞はファイル名。候補集合の差し替え。初期 policy は全 Generated。**既存 16 セルは全廃**（下節。移送しない）。identity → `SceneBase` の結線と R-3 ガードを名前文法から外す。スポーンを `(0,4)` へ。**頭で 1 季節 9×6 だけ生成して生成器 1 回の実時間を測り、M を維持するか裁定する** |
| S-5 | トンネルと季節遷移 | §5 の契約。N-3 の内装はここで実測 |
| S-6 | 1 変奏 = 1 Addressables グループ | Tunnel と共有 Lit / Primitive は専用季節グループに入れない |
| S-7 | 1 変奏 = 1 Variant タグ + 未チェックアウト経路 | §20 の既存機構にデータを流す。新機構なし |
| S-8a | 春の演奏レイヤ | 線沿い + 見証。ここで 4 動詞は出荷可能。`HandEditProbe` とスキャフォールド宣言の退役は春で開始してよい |
| S-9 | 変奏 II 背コリドーで §21 T-07〜T-09 | y=4 未昇格を確認してから測る。それまで T-07〜T-09 凍結 |
| S-8b〜d | 夏・秋・冬の演奏レイヤ | 品質バー W-7。動詞の証明条件ではない |

**S-4 の既存 16 セル: 全廃。** 谷は新規生成する。移送も座標補正も行わない。
`move_asset` も `set_transform` によるワールド Δ も、破壊経路 3 に旧 12 枚を任せる手順も、使わない。

**Bounds のあいだは現行 16 枚を動かさない。** 全廃は S-4 の仕事。

破壊経路 3（範囲外削除）では捨てられない。既存 16 枚は `(0,0)〜(3,3)` にあり、新谷 `{ origin=(0,0), size=(9,6) }` の**範囲内**。`CellPopulationPlan` の削除計画は `grid.Contains` なら continue する。`HandAuthoredCells` を空にして生成器を回すと、今の生成器は `CellIdentity.Format` のまま **`Cell_*` を 9×6 に拡張するだけ**で、Spring 谷にはならない。`CollectExistingStates` は `CellIdentity.TryParse(folderName)` 失敗を無視するので、`Spring_Cell_*` を先に焼いても旧 `Cell_*` が座標キーで残る。

手順:

1. Bounds の口が現行 4×4 で通っていること
2. 範囲内でも `Cell_*` / `Environment_*` を消す**明示ワイプ**（今の生成器にその口は無い。S-4 で足すか、Editor から消してから生成する）
3. ワイプの前に、南辺 4 座標 `(0,0)(1,0)(2,0)(3,0)` を別々の意味で持つ 3 配列を触る
4. 修飾付き identity を吐く生成器を回す（`CellIdentity.Format` のまま焼かない）
5. 初期 policy は全 Generated。昇格は S-8a

| # | 場所 | 何が起きるか |
|---|---|---|
| 1 | `CellAuthoringPolicy.HandAuthoredCells`（`CellPopulationPlan.cs` が `Resolve` する） | 範囲内の Skip。空にしないと南辺 4 枚は Populate されず残る（削除計画には載らない） |
| 2 | `WorldCellStreamingSliceCreator.EnvironmentSproutCells` | 二役。(a) Environment 子を作るセル (b) Ground を置かないセル（`includeGround = !sproutSet.Contains(...)`）。「全セルに Environment をスキャフォールド」で配列を全セルへ広げると、**谷全体から地面が消える**。二役を切り離すこと |
| 3 | `HandEditProbe.TargetCells` | 南辺 4 枚のハードコード。捨てたセルを stamp しにいく。春の演奏レイヤ（S-8a）の identity へ差し替えるまで無効 |

**S-4 で名前文法が外れる口（Bounds では触らない。現行 `Cell_0_0` のまま通す）:**

- `GameSceneFactory`: `IsCellId` / `IsEnvironmentId` が false → factory が null → Director が `SceneFactory returned null` で throw。`Spring_Cell_*` が `DemoCellScene` にならない
- `CellScene` ctor: `TryParse` 失敗で `ArgumentException`。factory を先に直すと即死
- `EnvironmentIdentity.TryFromCellId`: 親名から子名を組み立ててから Children 走査。修飾付きでは走査に届かず Environment を Add しない（無言）。子の解決は Children 走査へ寄せる
- R-3（`ThrowIfCellIdentity`）: `IsCellId("Spring_Cell_4_2")` は false。修飾付きになると遷移禁止が空洞化する。検出は名前文法ではなく距離政策の候補フラグへ（口の通しかたは Bounds の分解問 4。S-4 で名前が変わる瞬間に効いていなければならない）

スポーン: 現行 `WorldCellCatalog.SpawnPosition` は `Cell_0_0` 中心。S-4 で春の源流 `(0,4)` へ移す。

**Catalog / Driver（S-4 で SampleGame 側）:** 谷は矩形 1 つ `{ origin=(0,0), size=(9,6) }`。
列挙は局所 54 セル。アクティブ季節が候補集合を決める。FW に季節語を出さない。
生成器は Season_* 4 ノードを吐き、`World` を置き換える。楽譜関数は局所 `(x,y)` の純関数。
乱数を使うならシード固定。全セルに Environment をスキャフォールド（空でよい）。
スキャフォールドは sprout 配列の二役を切ったあとの口で行う（上表 2）。

**テスト:** 既存 WSC / MultiFocus / 統合 / 生成器 / `CellPopulationPlan` は残す。
入力は Bounds の口に追随させる（分解セッションが署名を決める）。
旧 T-A（矩形間空隙ガード）の本番 assert は不要（本番は単一矩形 × 共有座標）。フィクスチャとしては残してよい。
テストで `Task.Delay` / `Thread.Sleep` 禁止。全件実行は Phase C。実装者は「実装完了。テスト未実行」と報告する。

---

> §7 / §8 は欠番。HANDOFF の Phase C（§7）/ C'（§8）と番号を重ねない。`tools/docs-audit.ps1` 検査3は両方埋まっている HANDOFF を harvest 忘れと見るため、本書は対象にしない。harvest 期限は §12。

## 9. スケール（採用候補: M。S-4 頭で裁定）

| 案 | 季節寸法 | 総セル | 長軸横断 (42 m/s) | `.unity` Cell+Env | 生成器のシーン開閉（目安） |
|---|---|---:|---:|---:|---:|
| S | 6×4 | 96 | 36s | 192 | ~192 |
| **M** | **9×6** | **216** | **54s** | **432** | **~432**（生成器 2 回ならその倍） |
| L | 12×8 | 384 | 71s | 768 | ~768 |

現行 4×4 は Cell 16 + Environment 4 + World ほかで `.unity` は 20 枚前後。
`SceneResourceMap.asset` は 1 ファイルの平坦リストで、M ではエントリが数百になる（R-6 の構造衝突）。

`WorldCellStreamingSliceCreator` は AuthoredRoot 確認のためにセル / Environment を 1 枚ずつ開いて閉じる。
216 セルではこれが数百回になり、「イテレーションがどこでも簡単」のループ自体が重くなる。

**S-4 の頭:** 9×6 を 1 季節だけ生成して生成器 1 回の実時間を測る。許容できなければ S に落とし、楽譜セル座標を再定義する。測らずに M をディスクへ焼かない。

本書の楽譜座標は M 前提。S に落とすなら楽譜を書き直す。

---

## 10. 受入条件（プログラム全体）

| # | 条件 | 判定 |
|---|---|---|
| W-1 | FW に季節の語彙が無い | `unity/Assets/OneStarMaker/` を `Season\|Spring\|Summer\|Autumn\|Winter\|季節` で grep → 0 件 |
| W-2 | identity 重複 0 | SceneResourceMap 生成時の Duplicate 警告 0 |
| W-3 | 遷移の排他 | 旧季節の in-flight 0 → 新季節 Add。重畳 0。同時に Stable な `Season_*` は 1 つ。desired が完全に入れ替わる |
| W-4 | 編集が消えない | 生成器 2 回のあと、昇格済み stamp 全生存（Environment 増加分含む）。**判定は S-8a 以降**（S-4 時点は昇格 0 なので空振りする。N-8 で実証項目から下ろすならこの行は無効） |
| W-5 | 単独ビルド | 1 季節リビルドで他 3 季節バンドルのハッシュ不変。見証の頂きが変わる。共有 Lit は季節グループ外 |
| W-6 | 単独チェックアウト | ローカル欠落季節がリモート解決 or 明示失敗 + 旧季節復帰 |
| W-7 | 品質バー | §2 の 4 項目を人が目視（自動化しない）。S-8a 時点では春について見る。全変奏は S-8d |
| W-8 | 計測 | 変奏 II 背コリドーで A-1 / A-2 が §21 の受入値内。desired はすべて Generated |
| W-9 | 空間の口 | Bounds Plan の受入を満たしたうえで S-4 に入っている。名前から座標を復元して desired を組んでいない |

レビュー時の grep: `?.` / `??` / `is null` / `ReferenceEquals`（破棄されうる `UnityEngine.Object` 対象）。

---

## 11. 未決事項

| # | 論点 | 決定時期 |
|---|---|---|
| N-1 | 初回季節を誰が Ensure するか（トンネル始まりか、Session の初回 Ensure か） | S-4 |
| N-2 | RenderSettings の適用主体（Season シーンの Stable フックか、専用コンポーネントか） | S-4（N-1 と同時） |
| N-3 | トンネルの内装・滞在時間（ロード隠蔽の実測。距離条件は §5 で固定済み） | S-5 |
| N-4 | Environment を距離政策の候補にしないこと（CCS: 距離の単位は Cell）。子は親 Stable 後の明示 Add のまま | Bounds 分解時に確認 |
| N-5 | 第三声部（照明職 `*_Lighting_*.unity`）を標準装備にするか | S-8 までに発注者判断。今回は 2 声部 |
| N-6 | `unityyamlmerge` ドライバ設定（前提条件ではない） | 任意 |
| N-7 | AABB の置き場（`SceneAssetDescription` か隣の値型か `SceneResource` 直下か） | Bounds の実装分解セッション |
| N-8 | 生成器を**実証項目として**残すか。残すなら Generated / HandAuthored 同居・W-4・品質バー 4・イテレーション行は S-8a 以降も生きる。下ろすならそれらと `CellAuthoringPolicy` / `CellPopulationPlan` / `HandEditProbe` / R-6 の公開面分類が同時に動く（§21 / §33 は同居自体を実証対象と書いている）。S-4 で 216 セルを焼く装置自体は、どちらでも残る | S-4 頭 |

---

## 12. harvest 方針（Bounds の口が通ったあと。今は §33 を書き換えない）

- §33 D-1: 空隙配置 → 同座標 + 候補集合の排他 + Bounds。identity 文法を FW 契約にしない
- §33 D-6 / §5 表: 季節↔動詞・季節別 policy → §4 の検証マトリクスと 2 段 policy
- §33 §7: 空隙の幾何 → §3.3 / §5
- §33 §8: シーン木の identity 例を修飾付きフォルダ名へ（FW は読まない、と注記）
- `SEASON_LEVELS_IMPLEMENTATION.md`: S-3 記録と §0.3 実測以外は、全スライス harvest 後に `git rm`
- 本書と Bounds Plan も harvest 後に `git rm`
- `pwsh tools/docs-audit.ps1` を通す
