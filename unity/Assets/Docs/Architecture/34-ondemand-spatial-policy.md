# 34. OnDemand の空間政策

> ステータス: **到着契約。未実装。** 実装値の正本ではない。
> 現状（今動いている格子キー）: [§21](21-scene-streaming.md) / [STREAMING_CURRENT_SPEC.md](../../../../docs/streaming/STREAMING_CURRENT_SPEC.md)
> 対照: [STREAMING_CURRENT_VS_IDEAL.md](../../../../docs/streaming/STREAMING_CURRENT_VS_IDEAL.md)
> 関連: [§5 LoadType](05-scene.md)（親に対する引っ張り。本章は触らない）/ [§33](33-sample-demonstration-boundaries.md)（SampleGame の実証。世界構図は作業台）
> [ARCHITECTURE.md](../../ARCHITECTURE.md) に戻る

UpdateSystem の二枚立て（[現状仕様](../../../../docs/updater/UPDATER_CURRENT_SPEC.md) / [時間権威](26-update-async-time-authority.md)）と同じ役割分担である。本章は到着点であり、現状の写しではない。

---

## 0. 一文

**OnDemand シーンの desired set は、メトリック＋ヒステリシス＋候補集合で決める。**
シーンは「自分が載るか」を答えない。identity は不透明なキーである。
空間メトリックの入力は各シーンが持つ体積（AABB または球）であり、名前から格子座標を復元することではない。

---

## 1. 語彙の置き場

混ぜると、作業単位の焼き方がランタイムの空間プロトコルになる。

| 層 | 持ってよいもの | 持ってはいけないもの |
|---|---|---|
| **フレームワーク** | 不透明な identity、体積、候補集合、ヒステリシス、`maxInFlight`、距離順 priority | 格子座標を desired / retain / policy のキーにすること。identity 文法。季節・矩形・空隙 |
| **SampleGame** | セル（人が開く作業単位。フォルダ、職種分割、制作規約） | FW の公開面に格子文法の型を置くこと |
| **生成器** | 均一格子から AABB を焼く入力。辞書キーは **identity 文字列** | 座標を既存収集 / policy の主キーにすること |

セルは「人が並走する大きさ」である。名前から座標が戻る型ではない。

`Runtime/SceneSystem/Cells/`（`CellIdentity` / `CellGridConfig` / `CellScene`）は現状 FW にある。到着点では SampleGame または Editor へ下ろす。公開面に格子文法を残さない。

---

## 2. なぜこの契約か

現状の距離計算は **座標が主キーで identity が派生物** である（[現状仕様](../../../../docs/streaming/STREAMING_CURRENT_SPEC.md)）。

```
座標列 → 名前の Format → 格子定数で中心を組み立て → 点距離
```

名前から座標をパースする経路は距離の外にもある（SceneBase 結線、子 identity の導出、生成器の既存収集）。座標を主キーにしたまま名前文法を足すと、同じ体積を複数 identity が占める使い方が先に生成器で潰れる。修飾パース・デコレータ・qualifier は、このチェーンを延長する部分解である。体積をデータにした瞬間に捨て仕事になる。

ストリーミングが要るのは次の 2 つだけである。

- **候補集合**（今の親コンテナの子、など）
- 各候補の **ワールド体積** と注視点の距離

格子座標は生成器が体積を書くときの入力である。ランタイムが identity から復元するキーではない。

---

## 3. LoadType は親に対する引っ張り専用

[§5.7](05-scene.md) の 3 値を増やして距離を表さない。

| LoadType | 判定 |
|---|---|
| `NecessaryAlways` | なし。親に同期 |
| `IncrementalAlways` | なし。親に非同期で先読み |
| `OnDemand` | 親は触らない。政策または明示 `AddScene` が desired に入れる |

Pause / Result のような明示 Add は、距離政策の候補に入っていない OnDemand である。距離政策の interface に載せない。

---

## 4. 政策が desired を決める。シーンは答えない

`bool ShouldLoad(this)` を `SceneAssetDescription` や SceneBase 派生に置かない。
却下済みの自律判断（[§21 D-3](21-scene-streaming.md) 却下案 1）に戻る。距離は全候補と in-flight 上限と優先度を同時に見る。

形（契約。署名は移行 HANDOFF）:

```
候補集合 + 各候補の体積
  → 注視点からのスカラー距離
  → desired = distance <= loadThreshold
  → retain  = distance <= unloadThreshold
  → 近い順に maxInFlight
  → AddScene(identity) / UnloadScene(identity)
```

identity は不透明。政策層は名前を組み立てない。

コンテキストは政策ごとに別でよい。空間は注視点。将来の物語先読みは今のノード。
1 つの `LoadContext` に全部詰めない。Director は政策を知らない（今の Backend 委譲と同じ）。

政策 / メカニズム分離（§21 D-3 / D-4）は維持する。

---

## 5. 体積はデータの正本

評価器ではない。空（またはフラグ off）なら空間に属さない（Title / Pause など）。

持つもの（最小）:

- 体積: AABB、または中心＋半径（球）
- 距離は体積の**中心**への XZ 距離。表面距離は採らない（現状のセル中心距離と同値になり、移行の「同等の集合」が成立する）
- 距離政策の候補か: フラグ（仮称 `StreamByDistance`）。true のときだけ政策の候補。画面遷移禁止（現状 R-3）は名前文法ではなくこのフラグで見る

**グリッド座標はランタイムのキーにしない。** 生成器の入力・HUD 表示用なら局所に残してよい。policy / 既存収集 / desired のキーは identity 文字列か体積そのもの。

置き場は移行が選ぶ。どれでもよく、避けたいのは identity 文法を増やすことと、座標を第二の主キーにすること。

| 候補 | 向き |
|---|---|
| `SceneAssetDescription` に埋め込む | LoadType と同じ「いつ載せるか」の隣。Build の Payload 列挙には不要なフィールドが混ざる |
| Description の隣の値型 | Payload 列挙と空間を分ける。`SceneResource` の YAML は 1 ファイルのまま |
| `SceneResource` 直下 | シーングラフのノードが世界のどこを占めるか、という話 |

---

## 6. 候補集合と体積は別

同じ AABB を複数 identity が持ってよい。体積は membership ではない。

- 候補 = 呼び出し側が渡す identity 列（今いる親コンテナの子、など）
- 距離 = 各候補の体積と注視点
- 集合の差し替え = 候補リストを替えて政策を作り直す。id の翻訳層は作らない

SampleGame の四季は、この契約の**使い方**である（同じ体積・候補だけ排他）。FW は季節を知らない。

職種分割の子シーン（現状の Environment）は距離政策の候補に入れない。距離の単位は人が開く作業単位（セル）のまま。子は親 Stable 後の明示 Add。親子の引っ張りを空間メトリックに混ぜない。

---

## 7. 距離以外（予約。本章で実装しない）

同じ殻（スカラー距離＋ヒステリシス＋候補集合）に乗るもの:

| 距離の出どころ | 地図 | 例 |
|---|---|---|
| ユークリッド / AABB / 球 | 生成器が焼いた体積 | 今の空間政策 |
| グラフのホップ | 章・部屋・クエストの有向グラフ | ノベル先読み、インドアのポータル |
| 時間 | タイムライン | カットシーンの先読み |
| 論理 | フラグ | アンロック。集合差し替えの方がきれいならそちら |

グラフは**政策が読むカタログ**である。シーンツリーを DAG にしない。親は 1 つ。[§5](05-scene.md) の「依存 DAG や Requires リストは導入しない」は維持する。
寿命が木と一致しない BGM / 立ち絵は `AssetManagement` か親スコープのサービス。

予算と `maxInFlight` は「載せるか」ではなく制限器。メトリックの外側に付ける。

---

## 8. 維持するもの

- セルは人が開く作業単位（フォルダ、職種分割）。**SampleGame の制作規約**であり FW の型ではない
- 作業単位シーンが UIView を持たない（現状 §21 R-2。SampleGame）
- 生成器が均一格子から AABB を焼くこと
- LoadType（親が載ったときの引っ張り）
- 政策 / メカニズム分離。シーンは判断しない
- desired / retain / ヒステリシス / maxInFlight / 距離順 priority
- Full ティアは SceneDirector のシーンツリー（§21 D-2）。撤退ライン（Backend 差し替え）も維持
- OnDemand を `SwitchScene` / 履歴 / `TransitionPlan` に乗せない。検出は名前文法ではなく距離政策の候補フラグ
- FW に季節の語彙を出さない

---

## 9. やらないこと（本章の本文）

- 実装ファイル一覧、API の確定署名、テスト関数名（移行 HANDOFF）
- 現行レイアウトの寸法・本番セルの移動・生成器メニュー手順
- 修飾パース、id 翻訳デコレータ、qualifier を Config に足すこと
- グラフメトリック / ノベル / HLOD（§22）
- [§5](05-scene.md) の LoadType 3 値を増やすこと
- `SceneState` 14 値、asmdef 一方向

格子寸法・`Cell_{x}_{y}`・矩形集合を本章の主語にしない。それらは現状または生成器入力である。

---

## 10. 公開面との関係

| 文書 | 役割 |
|---|---|
| 本章 | 到着契約 |
| [§21](21-scene-streaming.md) | 現状の設計記録（チケット履歴・生成器の非破壊契約・受入値）。到着点ではない |
| [STREAMING_CURRENT_SPEC.md](../../../../docs/streaming/STREAMING_CURRENT_SPEC.md) | 今動いている実装値 |
| [STREAMING_CURRENT_VS_IDEAL.md](../../../../docs/streaming/STREAMING_CURRENT_VS_IDEAL.md) | 対照表 |
| 作業台 `docs/handoff/` | 格子キーを殺す移行。世界構図 |

コードを書くセッションは本章をひっくり返さない。現行レイアウトで口を通す手順は移行側にだけ置く。
