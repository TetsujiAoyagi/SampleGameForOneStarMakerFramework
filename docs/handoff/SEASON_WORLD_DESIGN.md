# 世界設計「主題と変奏」— S-3C 構図の正本 (2026-08-29 改訂)

> type: program
> ステータス: **構図は発注者承認済み。**
> **世界は本書。空間の到着契約は [§34](../../unity/Assets/Docs/Architecture/34-ondemand-spatial-policy.md)。格子キーを外す M-1〜M-4 は完了し、現況は [STREAMING_CURRENT_SPEC.md](../streaming/STREAMING_CURRENT_SPEC.md)。§33 本文はまだ（空隙レイアウトのまま）。世界についても空間についても、§33 から書き始めない。**
> S-3 の矩形集合化は `develop` にマージ済み。記録は公開面 [STREAMING_CURRENT_SPEC.md](../streaming/STREAMING_CURRENT_SPEC.md)。**実装指示ではない。**
> 旧稿（修飾パース / デコレータ / S-3D を `CellIdentity` の本題にする / 空隙矩形）は git 履歴にある。**本文には残さない。**
> §33 の D-1（空隙で季節矩形を離す）/ 季節↔動詞 / identity に季節名を入れない、とは食い違う。世界については本書が新しい。§33 には退役表がある。本文 harvest は移行の口が通ってから行い、それまで §33 本文は書き換えない。
> §7 / §8 は欠番（HANDOFF の Phase C / C' と番号を重ねない。スライス S-8 と節番号を混同しない）。harvest 期限は §12。`docs-audit.ps1` 検査3の対象にしない。

実装エージェントへ: 本書は構図・実証・スライス順序の正本である。`CellIdentity.TryParse` を修飾対応すること、`StreamingConfig` に qualifier を足すこと、Backend デコレータで id を翻訳することは、**本書の指示ではない。** 距離判断は §34。格子キーを外す M-1〜M-4 は完了済み。既存 16 セルは **S-4b で全廃済み**。

---

## 0. 一文

**世界はひとつの谷（9×6 セル）であり、四季は同じ座標に載る四つの変奏である。**
ディスク上の区別は SampleGame のフォルダ名（`Spring_Cell_4_2` 等）で行う。
季節の入れ替えはトンネル滞在中の Unload → Add で隠す。ワールドは原点直上の
2.25km × 1.5km に収まり、季節間の座標オフセットもセル座標の写像も無い。

距離判断が読むのは identity 文字列ではなく、各シーンが持つ体積（[§34](../../unity/Assets/Docs/Architecture/34-ondemand-spatial-policy.md)）である。

---

## 1. 発注者裁定ログ（旧前提のどれが死んだか）

| 日付 | 裁定 | 効果 |
|---|---|---|
| 2026-08-26 | 生成器で大まかに作り、人/AI の編集を正とする | 既存 `HandAuthored`（`AuthoredRoot` 保護）。新 policy 種別なし。**ただし初期配置は Generated**（§4） |
| 2026-08-29 | 各季節をもっと大きく。予算上限（軟 16 / 硬 64）撤廃 | 総セル 216 を目標寸法とする。S-4 冒頭で生成コストを測り、維持か縮小かを裁定する（§6） |
| 2026-08-29 | **季節ごとの動詞割当を廃止**。実証の目的は「多人数・職種別の同時編集、単独ビルド、単独チェックアウト、イテレーションが世界のどこでも簡単」であること | §33 §4 / §5 の季節↔動詞表は退役。§4 の検証マトリクスに置換 |
| 2026-08-29 | 座標帯オフセット（原点から 25km〜75km）は float 精度・物理の理由で却下 | 撤回済み。象限配置も不要になり撤回 |
| 2026-08-29 | **四季は同じ座標を共有する。** ディスク上は接頭辞付きフォルダ名 | FW が季節語を読んではならない。名前から座標を復元する契約を FW に足さない（空間は §34） |
| 2026-08-29 | テーマ「主題と変奏」（時制 × 楽譜の混合）を承認 | §2 |
| 2026-08-29 | 距離の正本を identity 文法にしない。空間の到着契約を公開面へ | [§34](../../unity/Assets/Docs/Architecture/34-ondemand-spatial-policy.md)。M-1〜M-4 は完了済み |

上表は日付付きの裁定記録である。その後、[S-4 program](S-4_FULL_SPEC_WORLD_AUTHORING.md)で9×6×4を固定し、一回生成後に生成器を撤去した。現在の手順・完了状態は§6 / §9 / §11を参照する。

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
4. 昇格済み identity の演奏レイヤ（`AuthoredRoot` 配下）が 1 個も消えないという編集保護の要求は維持する。対象操作と判定手順はW-4で確定する。旧生成器による判定は撤去後に実行できないため、**S-8a 以降のW-4は判定方法未決**であり、合格・免除にはしない。

S-4bの初期配置は全件Generatedで、昇格の対象・順序は§4を維持する。生成器・制作policyコードは撤去済みであり、変奏調整のために再実行しない。将来の一括生成が必要ならN-8に従う。

---

## 3. 幾何とディスク上の名前

### 3.1 楽譜マップ（全変奏共通、局所座標 9×6）

```
      x0 x1 x2 x3 x4 x5 x6 x7 x8      1 セル = 250m。谷全体 2250m × 1500m
 y5 |  ^  ^  ^  ^  ^  ^  ^  ^  ^     ^ = 背（北の高まり。Generated の計測コリドー）
 y4 |  ~  ~  .  .  .  .  .  .  .     ~ = 線（源流は北西）
 y3 |  .  .  ~  ~  .  .  .  .  .
 y2 |  .  .  .  .  ◇  ~  ~  .  .     ◇ = 見証（曲がり角、局所 (4,2)。頂きが変奏の署名）
 y1 |  .  .  .  .  .  .  .  ~  ~     線は南東へ抜ける
 y0 |  .  .  .  .  .  .  .  .  .
```

- 線セル: `(0,4)(1,4)(2,3)(3,3)(4,2)(5,2)(6,2)(7,1)(8,1)`
- 見証セル: `(4,2)`（線の曲がり角。tall vertical、遠くから同定できるシルエット）
- 背: `y=5` の行 9 セル
- グリッド定数は [現状仕様](../streaming/STREAMING_CURRENT_SPEC.md) の写し: `Origin = (0,0,0)` / `CellSize 250` / `LoadRadius 375` / `UnloadRadius 550` / `MaxInFlight 2`
- スポーン座標は春（変奏 I）の源流セル `(0,4)` 中心上空（構図の定点）。S-4b で `WorldCellCatalog.SpawnPosition` と初期 Ensure を同時に移行済み
- 品質バー 1 の判定地点は演奏レイヤがある線上（見証 `(4,2)`、または線の途中 `(2,3)`）。スポーン `(0,4)` ではない。`(0,4)` は S-9 まで Generated（§4）

この座標は **採用済みの9×6（M）**。S-4bで生成済みであり、縮小判定を後続スライスへ再発注しない（§9）。

格子座標は**SampleGameの制作配置の入力**であり、ランタイムがidentityから復元するキーではない。AABBはEditorがSceneのRendererから計算する（N-7）。

### 3.2 ディスク上の identity（SampleGame のフォルダ規約）

FW は季節語を知らない。次は SampleGame のファイル名の約束である。

```
無修飾:   Cell_{x}_{y}                      （旧 World。S-4b で撤去済み）
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

- 旧 `World` ノードは S-4b で Season_* 4 つに置き換え済み（§33 D-2 どおり）
- **常駐する季節はトンネル遷移中を除き常に 1 つ。** 全季節が同じ AABB を占めるため、
  遷移は必ず「旧季節 Unload 完了 → 新季節 Add」の順。重畳を作らない
- 実行時の不変条件: `Season_*` が Stable なのは高々 1 つ。破ったら失敗（ログ受入だけにしない）
- S-5での季節遷移要求はトンネル経由に限定する（デバッグ経路も同じ遷移口を通す）。S-4bで実装済みのSession初回Spring Ensure（N-1）とは区別する

### 3.4 空間プロトコルは本書の外

距離・ヒステリシス・候補集合の持ち方は [§34](../../unity/Assets/Docs/Architecture/34-ondemand-spatial-policy.md)。
本書が固定するのは「四季は同じ AABB を共有し、候補集合だけが排他」という**使い方**である。

M-1〜M-4 の境界を戻さず、S-4b で 9×6×4 と修飾付き identity を SampleGame 内へ着地させた。

---

## 4. 検証マトリクス — このサンプルが証明する表

季節ごとに別の動詞を陳列するのではなく、**全変奏 × 全ワークフロー**を証明する。
ただし **春（変奏 I）の演奏レイヤが入った時点で 4 動詞は出荷可能** とする（スライス S-8a）。
他変奏の作り込みは品質バー（W-7）であり、動詞の証明条件ではない。

| ワークフロー | 実現手段（全変奏共通） |
|---|---|
| 2 職種の同時編集 | 全セルに床 `*_Cell_*.unity`（地形職）+ 印 `*_Environment_*.unity`（置き物職）。同じ地点を 2 人が同時に触ってもファイルが違うので衝突しない。**ただし衝突しないのは中身。** `SceneResourceMap.asset`、`Season_*.asset` の `_children` は構造変更のたびに全員が触る 1 ファイル。構造編集の担当は単独に固定し、現在の構造追加はWorld Workspaceのtransactionを使う。共有Map / 親子linkは競合対象として扱い、並行する構造編集で競合しないとは主張しない。旧 Addressables 設定も残るが通常 Content build 入口ではない |
| 編集保護 | 昇格済みidentityの演奏レイヤを全て保護する要求は維持する。旧R-6生成器 / policyによる判定は撤去済みのため使わない。**S-8aでW-4の判定方法を決定するまで未達** |
| 単独ビルド | 1 つの contentSet は 1 回の選択。現行入口は All Seasons Full / Spring Full / Spring Whitebox / Spring Full And Whitebox。その再 build は別 contentSet または別 build identity の公開 directory を書き換えない。同一 contentSet の成功成果物は identity ごとの公開先に残る。DIST は同じ revision の内容差し替えを拒否する。共有 Lit / Primitive / Tunnel は選択に含まれればその directory に入る。同一 directory 内の季節グループ単位ハッシュ不変は現行契約にない。旧 Addressables グループのハッシュ独立とも等価ではない。delta 配信は DIST 後続で未所有 |
| 取得済み content からの実行 | DIST が検証した installed revision だけを登録する。sourceFiles の Missing / Changed は編集可否の案内であり、リモート Addressables カタログから欠損を埋めて Play しない。その revision に含まれない季節への遷移は明示失敗とし、出し方と旧季節復帰は S-5（D-5）。Framework は VCS checkout を代行しない。部分 Checkout + リモート補完は未所有。隔離は空隙ではなく **候補集合の排他**（常駐季節が 1 つ） |
| ストリーミング | 全域で動く。S-9 は純政策ベンチマークと実コンテンツ横断を分け、**実コンテンツ計測（§21 A-1〜A-5）は変奏 II（夏）の背コリドー**で取る |
| イテレーション | 印を1個編集 → 保存 → 選んだcontentSetを再build → 新revisionをverified installしてPlay。旧生成器は再実行しない。編集保護の独立した判定はW-4に残す |

**制作状態の方針（2 段。撤去済みpolicyコードの再導入指示ではない）:**

| 段階 | 領域 | policy | 備考 |
|---|---|---|---|
| 初期（S-4 生成直後） | 全セル（216） | `Generated` | S-4b生成時の状態。生成器の再実行口は無い。S-8の目視より先に昇格しない |
| 昇格後 | 人が手を入れた identity のみ | `HandAuthored` | 目安は各変奏の線沿い 8〜10 + 見証周辺。**キーは座標ではなく修飾付き identity**。春の `(4,2)` を昇格しても夏の `(4,2)` は Generated のまま |
| 固定 Generated | 各季節の背 `y=5` | 昇格禁止 | 計測の均質性 |
| S-9 まで Generated | 各季節の `y=4` 行 | 昇格禁止（S-9 完了まで） | **中心距離**で y=5 中心から y=4 中心は 250m ≤ LoadRadius 375m。表面距離だと y=3 も desired に入り、線セル `(2,3)(3,3)` の昇格と W-8 が衝突する。距離の基準は [§34](../../unity/Assets/Docs/Architecture/34-ondemand-spatial-policy.md) §5。源流 `(0,4)(1,4)` の演奏は S-9 のあと |

`HandAuthored` は「全セルを手作業で作る」ではない。初回は生成器のスキャフォールド、
手を入れたidentityだけ昇格する。Generated → 編集 → 昇格の実演と編集保護の証明は後続の要求であり、
記録方法・判定手順をW-4で決める。撤去済みのpolicy型が現在も存在するとは扱わない。

W-8 の前提: **実コンテンツの計測フライト中の desired はすべて Generated。** 純政策ベンチマークはシーンをロードせず、候補数と制御面の費用だけを変える。

---

## 5. トンネルと季節遷移

トンネルは未取得季節を空隙で隔離する装置ではない。ロード隠蔽用の滞在空間であり、
次の変奏を要求する入口である。取得・installはDISTの責務であり、トンネルがVCS checkoutや旧リモートカタログ補完を行う契約ではない。

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

順序: **M-1〜M-4（完了）→ S-4 → S-5 → S-8a（春）→ S-9 → S-8b〜d（他変奏）**。S-6 / S-7 実装スライスは WCD で廃止。現行 Content build と DIST が担う。
1 スライス = 1 ブランチ = 1 HANDOFF（着手時に切り出す）。

Editor操作は [AGENTS.md](../../AGENTS.md) と [osm-unity-editor](../../.agents/skills/osm-unity-editor/SKILL.md) を正とする。ローカルでは対象project / 版 / pathを確認し、必要ならEditorを起動してよい。人間の手動起動は前提でない。Phase BはEditor操作・compile確認まで、UnityテストとBuildはPhase C。`unity test` / `unity run`、CloudのUnity CLI、Scene / asset YAML手編集は禁止し、人間のEditorを強制終了しない。`record` 禁止・`#nullable enable`・破棄されうる `UnityEngine.Object` への `?.` / `??` 禁止も維持する。

**退役:** S-3D（`CellIdentity` の修飾パース）。§34 が identity を不透明キーにすれば不要。復活させない。

| # | 内容 | 補足 |
|---|---|---|
| 前提（完了） | M-1〜M-4: 体積の口、生成器 identity key、候補フラグによる R-3、セル型の SampleGame 移動 | 現況は [STREAMING_CURRENT_SPEC.md](../streaming/STREAMING_CURRENT_SPEC.md)。完了済み HANDOFF は harvest 後に削除済み |
| S-4 | 制作基盤（a / b / c完了、d未着手） | [S-4 program](S-4_FULL_SPEC_WORLD_AUTHORING.md)。4 Season・9×6×4・旧16 Cell撤去・Spring初回Ensure・名前文法からの分離・最小RenderEnvironment / 代表bakeは完了。候補フラグによるR-3を維持する。残るVFX / EventsはS-4dの着手時HANDOFFで扱う |
| S-5 | トンネルと季節遷移 | §5 の契約。N-3 の内装はここで実測。1 Player で四季を持つか、`all-full` 起動か複数登録かは S-5 の A0 |
| S-6 | 廃止（WCD） | 1 変奏 = 1 Addressables グループは現行契約にない。Content Directory の contentSet 選択が担う |
| S-7 | 廃止（WCD） | 未チェックアウト経路は DIST。部分 Checkout 再燃は Architecture §20 どおり未所有 |
| S-8a | 春の演奏レイヤ | 線沿い + 見証。4動詞の出荷判定にはW-4の判定方法の確定と実証が必要。`HandEditProbe`はS-4bで撤去済みで、復活させない |
| S-9 | Streaming の計測と撤退判断（下記 S-9a〜c） | 純政策ベンチマーク → 変奏 II 背コリドーで §21 T-07〜T-09 → 結果に基づく最適化・撤退判断。y=4 未昇格を確認してから S-9b を測る。それまで T-07〜T-09 凍結 |
| S-8b〜d | 夏・秋・冬の演奏レイヤ | 品質バー W-7。動詞の証明条件ではない |

### S-9 — Streaming の計測を 3 段に分ける

Megacity は大きな workload の証拠であり、OSM の性能を代弁しない。S-9 の合否は「Megacity より速い」ではなく、OSM の control plane と実コンテンツが着手時 HANDOFF で固定した予算を超えないこととする。DOTS / Jobs / Burst への移行を先に決めず、現行 managed 実装を基準値として測る。

| 段 | 目的 | workload / 計測 | 完了条件 |
|---|---|---|---|
| **S-9a 純政策ベンチマーク** | `WorldStreamingController` 自体の候補数スケールと収束を、シーンロードの重さから分離する | FakeBackend で **1,000 / 10,000 候補**。単一 / 複数 Focus、静止 / 等速移動 / テレポート、desired 疎 / 密を分ける。Tick 時間、1 Tick の GC allocation、`IsLoaded` 照会数、最終 `desired = resident` までの時間、in-flight 上限時の backlog と最古要求待ち時間、starvation、duplicate request / stale completion / cancel 後残留を取る | workload ごとに反復数・中央値・p95 / p99を記録し、着手時 HANDOFF の control-plane 予算内。予算外なら S-9c の判断材料にし、S-9a 中に索引や Jobs を先回り実装しない |
| **S-9b 実コンテンツ横断** | SceneDirector / IAssetManagement / Content Directoryの通常経路とasset payloadを含む実証 | 変奏 II（夏）の背 `y=5` を等速と高速で往復。§21 A-1〜A-5に加え、ロード時間 p50 / p95 / p99、停止後の収束時間、常駐 / in-flight / cancel / pending-unload、managed / native / asset memory peak と復帰、通常経路のtoken / native resource / sessionの解放と残留を取る（互換Addressablesを別途測る場合はhandle残留も分けて記録する） | `y=4` が未昇格で、計測中 desired がすべて Generated。A-1〜A-5と着手時 HANDOFF の数値予算を満たし、例外・集合不一致・リークが 0 |
| **S-9c 撤退・最適化判断** | 数値から次の実装を選び、推測で設計を増やさない | S-9a / b の結果を、政策計算、`IsLoaded` 全件再照合、SceneDirector状態遷移、Content Directory / asset payloadに分解する。明示Addressables互換の測定は通常経路と混ぜない | 現状維持 / 空間索引＋ロード済み identity 列の取得口 / managed・native backend 比較 / SceneDirector 撤退ライン、のいずれかを根拠付きで決定。空間索引だけを入れて遠方 resident の Unload を漏らさない |

S-9a / b の結果には、比較可能性のため次の **workload manifest** を必ず添える。

- 候補数、resident 数、同時 desired 数、Focus 数
- `LoadRadius` / `UnloadRadius` / `maxInFlight`、Focus 速度と経路
- contentSet / revision、選択表現、配信bytesとロード後メモリ（S-9b）。従来要求のセル別容量・メモリ帰属は、共有依存を含むContent Directoryでの観測方法をS-9b Phase Aで固定する。未定義のまま検査済みにしない
- Unity / 使用backend・関連packageのバージョン、Editor / Player、quality tier、解像度、対象ハードウェア
- cold / warm cache、測定時間、反復数、Development Build / Profiler 接続の有無

S-9 の着手時 HANDOFF は S-9a〜c を 1 ブランチに詰め込まない。少なくとも「測定器と純政策ベンチマーク」「実コンテンツ計測」「判断記録」を責務として見積もり、500 行または 3 責務を超える見込みなら別スライス / 別ブランチへ切る。閾値は測定を見て後付けせず、各測定スライスの開始時にハードウェアと workload manifest とともに固定する。

**S-4b で既存 16 セルは全廃済み。** 谷は新規生成し、移送も座標補正も行わなかった。
`move_asset` も `set_transform` によるワールド Δ も、破壊経路 3 に旧 12 枚を任せる手順も使っていない。
旧 `Cell_0_0`（南辺の手編集）を `Spring_Cell_0_4` へ移して昇格する案は採らなかった。源流 `(0,4)` は新規 Generated。stamp で生存を見ていない。y=4 行は S-9 まで昇格禁止（§4）。

M-1〜M-4 の移行中は旧 16 枚を動かさず、S-4b で全廃した。

S-4b の一時 `CellPopulationPlan` は target identity の集合で削除対象を決めた。旧南辺4枚を含む16枚は明示ワイプし、修飾付き identity の生成後に plan / policy / generator を撤去した。S-4c 以降で復活させない。

実施記録:

1. M-1〜M-4 の受入が現行 4×4 で通っていること（完了済み）
2. 旧 `HandAuthored` 4 枚を含む `Cell_*` / `Environment_*` を明示ワイプ
3. 南辺4座標を持っていた旧 policy を生成前に解消
4. 修飾付き identity の四季アセットを一回生成
5. 全件 Generated を確認後、一時生成器と policy を撤去。昇格は S-8a

旧 `CellAuthoringPolicy.HandAuthoredCells`、`WorldCellStreamingSliceCreator.EnvironmentSproutCells`、`HandEditProbe.TargetCells` は一時生成器とともに撤去済み。南辺4座標の旧ハードコードを S-4c 以降の入力にしない。

**S-4b で名前文法を外した口:**

- `SessionWorldStreamingDriver`: active Season から渡された候補集合を駆動する。`GameSceneFactory` / `CellScene` の結線は S-4a で `StreamByDistance` / `Parent` へ移し、`EnvironmentIdentity` は削除済み
- R-3 は M-3 で `SceneResource.StreamByDistance` の検査へ移行済み。S-4b の SceneResource に候補フラグを設定し、修飾付き identity でもガードを維持した

スポーン: `WorldCellCatalog.SpawnPosition` は S-4b で春の源流 `(0,4)` 中心へ移行済み。

**Catalog / Driver（SampleGame 側）:** 谷は矩形 1 つ `{ origin=(0,0), size=(9,6) }`。局所54セルは制作座標であり、距離政策の入力ではない。
**距離政策の候補は identity、体積は AABB**（[§34](../../unity/Assets/Docs/Architecture/34-ondemand-spatial-policy.md)）。Catalog の矩形・格子列挙を desired にしない。
アクティブ季節が候補集合を決める。FW に季節語を出さない。
S-4b で Season_* 4ノードが `World` を置き換え、全セルに Environment を配置済み。生成器は撤去済みである。

**テスト:** 既存 WSC / MultiFocus / 統合に加え、Season 起動・切替・companion の寿命境界を残す。撤去済み生成器のテストは復活させない。
入力は完了済み M-1〜M-4 の identity／体積契約に追随させる（現況は `STREAMING_CURRENT_SPEC.md`）。
旧 T-A（矩形間空隙ガード）の本番 assert は不要（本番は単一矩形 × 共有座標）。フィクスチャとしては残してよい。
テストで `Task.Delay` / `Thread.Sleep` 禁止。全件実行は Phase C。実装者は「実装完了。テスト未実行」と報告する。

---

（§7 / §8 は欠番。スライス番号の S-8 と節番号を混同しない。「スライス S-8a」は春の演奏レイヤ。次は §9。）

## 9. スケール（Mを採用・S-4bで生成済み）

次の表は2026-08-29の比較時点の概算であり、現在のScene総数や再実行手順ではない。値は履歴として保持する。

| 案 | 季節寸法 | 総セル | 長軸横断 (42 m/s) | `.unity` Cell+Env | 生成器のシーン開閉（目安） |
|---|---|---:|---:|---:|---:|
| S | 6×4 | 96 | 36s | 192 | ~192 |
| **M** | **9×6** | **216** | **54s** | **432** | **~432**（生成器 2 回ならその倍） |
| L | 12×8 | 384 | 71s | 768 | ~768 |

旧 4×4 は Cell 16 + Environment 4 + World ほかで `.unity` 20 枚前後だった。S-4b の現況は 9×6×4、652 Scene。
`SceneResourceMap.asset` は 1 ファイルの平坦リストで、M ではエントリが数百になる（R-6 の構造衝突）。

現在は [S-4 program](S-4_FULL_SPEC_WORLD_AUTHORING.md) §1 / §4で固定した9×6×4を生成済みで、一時生成器は撤去済みである。S-4bの基本652 Sceneは当時の生成結果で、後続の職種Scene追加後の総数ではない。生成時間による縮小判定や旧生成器の再実行を後続の受け入れ条件に戻さない。寸法変更や再生成が必要ならN-8の新しい制作スライスで判断する。

---

## 10. 受入条件（プログラム全体）

| # | 条件 | 判定 |
|---|---|---|
| W-1 | FW に季節の語彙が無い | `unity/Assets/OneStarMaker/` を `Season\|Spring\|Summer\|Autumn\|Winter\|季節` で grep → 0 件 |
| W-2 | identity 重複 0 | SceneResourceMap 生成時の Duplicate 警告 0 |
| W-3 | 遷移の排他 | 旧季節の in-flight 0 → 新季節 Add。重畳 0。同時に Stable な `Season_*` は 1 つ。desired が完全に入れ替わる |
| W-4 | 編集が消えない | **判定方法未決・未達。** 昇格済みの演奏レイヤとEnvironment増加分を全て保護する要求は維持する。旧「生成器2回＋stamp全生存」はN-8の撤去により実行不能。S-8a Phase Aで対象・前後比較・成功条件を固定するまで、合格にも免除にも扱わない |
| W-5 | 単独ビルド | 選んだ contentSet の再 build が別 identity の公開 directory を書き換えない。グループ単位ハッシュ不変は主張しない |
| W-6 | 取得済み content からの実行 | 検証済み installed revision だけを登録。欠損はリモートカタログで埋めない。含まれない季節は明示失敗（S-5 / D-5） |
| W-7 | 品質バー | §2 の 4 項目を人が目視（自動化しない）。S-8a 時点では春について見る。全変奏は S-8d |
| W-8 | 計測 | S-9a の 1,000 / 10,000 候補で control-plane 予算内。S-9b の変奏 II 背コリドーで §21 A-1〜A-5と着手時 HANDOFF の数値予算を満たす。実コンテンツ計測中の desired はすべて Generated。workload manifest と S-9c の判断記録がある |
| W-9 | 空間の口 | 完了済み M-1〜M-4 の境界を維持し、名前から座標を復元して desired を組んでいない。修飾付き候補にも R-3 が効く |

レビュー時の grep: `?.` / `??` / `is null` / `ReferenceEquals`（破棄されうる `UnityEngine.Object` 対象）。

---

## 11. 未決事項と解決済みの前提

| # | 論点 | 決定時期 |
|---|---|---|
| N-1 | **解決済み:** Sessionの初回Spring Ensureと`WorldCellCatalog.SpawnPosition`の`(0,4)`移行はS-4bで実装済み。Tunnel遷移演出はS-5に残る | S-4b。現況はS-4 program §4.3 / STREAMING_CURRENT_SPEC |
| N-2 | **解決済み:** SeasonLightingSceneがLoad時にleaseを取得し、Unity sinkがLight / RenderSettingsへ適用する。Unload時に解放・baseline復元する | S-4c。Architecture §24 §9.1 |
| N-3 | トンネルの内装・滞在時間（ロード隠蔽の実測。距離条件は §5 で固定済み） | S-5 |
| N-4 | Environment を距離政策の候補にしないこと（CCS: 距離の単位は Cell）。子は親 Stable 後の明示 Add のまま | 現状仕様で確認済み。S-4 でも維持する |
| N-5 | 第三声部（照明職 `*_Lighting_*.unity`）を標準装備にするか | 全Cellへの標準装備はS-8までに発注者判断。S-4cは代表2 Cellに追加済みであり、未実装扱いにも全域採用扱いにもしない |
| N-6 | `unityyamlmerge` ドライバ設定（前提条件ではない） | 任意 |
| N-7 | ~~AABB の置き場~~ **決定済み（M-1）: `SceneResource` 直下**（`_volume` ＋ `_streamByDistance`）。値は生成器が格子定数から焼くのではなく、Editor がシーン保存フックと全件メニューで `.unity` から自動計算する | [STREAMING_CURRENT_SPEC.md](../streaming/STREAMING_CURRENT_SPEC.md) に harvest 済み |
| N-8 | **解決済み:** S-4b の生成器と制作 policy は生成後に撤去した。実証項目として復活させない。将来の一括生成は新しい制作スライスで扱う | S-4b |
| N-9 | 体積収集の範囲。現状は全 `Renderer`（`includeInactive: true`）。Particle / 無効デバッグメッシュで中心が跳ね得る。追加のVFX等が体積へ影響する場合の除外規約は未決 | S-4d等、該当する制作スライスのPhase A |
| N-10 | `WorldStreamingController.Candidates` 差し替え口。候補集合は丸ごと作り直す型。現行はSeason枝ごとにdriver / 候補集合を作り直す。in-flightを保持した集合差し替えは未実装の拡張案であり、自動的に追加しない | 必要性が出たスライスのPhase A |

W-4の未決はこの項目だけを保留する。日常のWorld Workspace編集・保存・content再buildを対象に保護を実証するか、一括生成自体が再び必要でN-8の新規制作スライスを先行させるかを所有者が裁定する。ここではいずれも採用しない。昇格対象・非昇格コリドー・編集保護要求を緩和しない。

---

## 12. harvest 方針（§33 本文は今は書き換えない。期限を混ぜない）

空間契約は [§34](../../unity/Assets/Docs/Architecture/34-ondemand-spatial-policy.md) へ移した。S-3 実測は [STREAMING_CURRENT_SPEC.md](../streaming/STREAMING_CURRENT_SPEC.md) へ移した。`SCENE_WORLD_BOUNDS.md` と `SEASON_LEVELS_IMPLEMENTATION.md` は git 履歴に残し、本文は復活させない。

§33 本文の世界構図に関する harvest は S-4 で行う。空間移行 M-1〜M-4 の現況は公開面へ harvest 済み:

- §33 D-1: 空隙配置 → 同座標 + 候補集合の排他 + §34。identity 文法を FW 契約にしない
- §33 D-6 / §5 表: 季節↔動詞・季節別 policy → §4 の検証マトリクスと 2 段 policy
- §33 §7: 空隙の幾何 → §3.3 / §5
- §33 §8: シーン木の identity 例を修飾付きフォルダ名へ（FW は読まない、と注記）
- `pwsh tools/docs-audit.ps1` を通す

移行 M-1〜M-4 の HANDOFF と横断計画は、実装値を現状仕様へ移したうえで削除済み。
本書は全スライス harvest 後に `git rm`。
