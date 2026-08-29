# SceneWorldBounds — OnDemand の空間政策 Plan (2026-08-29)

> ステータス: **Plan。** 実装チケットへの分解は別セッションが行う。この文書からコードを書き始めない。
> 位置づけ: [SEASON_WORLD_DESIGN.md](SEASON_WORLD_DESIGN.md) の構図（同座標の四変奏）が前提とする空間プロトコル。
> 公開面: 通ったあと [§21](../../unity/Assets/Docs/Architecture/21-scene-streaming.md) へ harvest する。今は §21 を書き換えない。
> 関連: [§5 LoadType](../../unity/Assets/Docs/Architecture/05-scene.md)（親に対する引っ張り。本書は触らない）/ [§33](../../unity/Assets/Docs/Architecture/33-sample-demonstration-boundaries.md)（harvest は口が通ってから）

---

## 0. 一文

**OnDemand シーンの desired set は、メトリック＋ヒステリシス＋候補集合で決める。**
シーンは「自分が載るか」を答えない。identity は不透明なキーである。
空間メトリックの入力は各シーンが持つ体積（AABB または球）であり、名前から格子座標を復元することではない。

四季の谷（9×6×4）を焼くのは、この口が現行 4×4 で通ってから。

---

## 1. なぜ今か

現行の距離計算は次のチェーンである。

```
Cell_4_2 → CellIdentity.TryParse → (4,2) → CellGridConfig → AABB → 注視点との距離
```

`WorldStreamingController` は毎 Tick `CellIdentity.Format(x, y)` で無修飾 id を自前生成する。
`CellScene.ComputeBounds` も名前と格子定数から体積を組み立てる。格子のうえでは足りる。

四季を同じ座標に載せると、`(4,2)` から名前は一意に戻らない。修飾パース・デコレータ・`cellIdQualifier` は、このチェーンを延長する部分解である。それを FW 契約にすると、体積をデータにした瞬間に捨て仕事になる。

ストリーミングが要るのは次の 2 つだけである。

- **候補集合**（今いる季節の子、など）
- 各候補の **ワールド体積** と注視点の距離

格子座標は生成器が体積を書くときの入力である。

---

## 2. 現況（この Plan が置き換えるもの）

| 部分解 | 所在 | 問題 |
|---|---|---|
| identity が `Cell_{x}_{y}` | `CellIdentity` / R-3 の `IsCellId` | 文法が空間プロトコルになっている |
| Controller が `Format(x,y)` | `WorldStreamingController` | 候補が座標列。修飾付きファイル名と二層になる |
| バウンズを毎回組み立てる | `CellScene.ComputeBounds(CellGridConfig)` | 体積がデータの正本ではない |
| 生成器が parse → 座標をキー | `WorldCellStreamingSliceCreator.CollectExistingStates` 等 | 修飾を足すと 4 季節が同一キーに潰れる |

残すもの:

- セルは人が開く作業単位（フォルダ、Cell / Environment 分割）
- `CellScene` の「UIView を持たない」（R-2）
- 生成器が均一格子から AABB を焼くこと
- LoadType（親が載ったときの引っ張り）
- 政策 / メカニズム分離（§21 D-3 / D-4）。シーンは判断しない
- desired / retain / ヒステリシス / maxInFlight / 距離順 priority

---

## 3. 決めること（Plan の契約。署名は分解セッション）

### 3.1 LoadType は親に対する引っ張り専用

| LoadType | 判定 |
|---|---|
| `NecessaryAlways` | なし。親に同期 |
| `IncrementalAlways` | なし。親に非同期で先読み |
| `OnDemand` | 親は触らない。政策または明示 `AddScene` が desired に入れる |

LoadType の値を増やして距離を表さない。Pause / Result のような明示 Add は、距離政策の候補に入っていない OnDemand である。

### 3.2 政策が desired を決める。シーンは答えない

`bool ShouldLoad(this)` を `SceneAssetDescription` や `CellScene` に置かない。
却下済みの自律セル（§21 D-3 却下案 1）に戻る。距離は全候補と in-flight 上限と優先度を同時に見る。

形（草案。分解セッションが署名を決める）:

```
候補集合 + 各候補の体積
  → 注視点からのスカラー距離
  → desired = distance <= loadThreshold
  → retain  = distance <= unloadThreshold
  → 近い順に maxInFlight
  → AddScene(identity) / UnloadScene(identity)
```

identity は不透明。Controller は `Format` しない。

コンテキストは政策ごとに別でよい。空間は注視点。将来の物語先読みは今のノード。
1 つの `LoadContext` に全部詰めない。Director は政策を知らない（今の Backend 委譲と同じ）。

### 3.3 `SceneWorldBounds` は空間メトリックのパラメータ

評価器ではない。空（またはフラグ off）なら空間に属さない（Title / Pause / Tunnel）。

持つもの（最小）:

- 体積: AABB、または中心＋半径（球）。ランタイムの距離は XZ でよいか、分解時に現行 `GetXzDistance` と揃える
- 距離政策の候補か: `StreamByDistance`（仮）。true のときだけ Controller の候補。R-3（`SwitchScene` 禁止）は名前文法ではなくこのフラグ（または「距離政策の候補」）で見る

**グリッド座標 `Vector2Int` はランタイムのキーにしない。** 生成器の入力・HUD 表示用なら局所に残してよい。policy / 既存収集 / desired のキーは identity 文字列か体積そのもの。

置き場は分解セッションが選ぶ（N-7）:

| 候補 | 向き |
|---|---|
| `SceneAssetDescription` に埋め込む | LoadType と同じ「いつ載せるか」の隣。Build の Payload 列挙には不要なフィールドが混ざる |
| Description の隣の値型 | Payload 列挙と空間を分ける。`SceneResource` の YAML は 1 ファイルのまま |
| `SceneResource` 直下 | シーングラフのノードが世界のどこを占めるか、という話 |

どれでもよく、避けたいのは identity 文法を増やすことと、座標を第二の主キーにすること。

### 3.4 候補集合と体積は別

同じ AABB を複数 identity が持ってよい（四季）。体積は membership ではない。

- 候補 = 今いる季節コンテナの子セル（または Driver が渡す identity リスト）
- 距離 = 各候補の AABB と注視点
- 季節切替 = 候補リストを差し替えて Controller を作り直す。id の翻訳層は作らない

Environment は距離政策の候補に入れない（CCS: 距離の単位は常に Cell）。子は親 Stable 後の明示 Add のまま（現行 `SessionCellChildLoadDriver`）。これは遅延した LoadType に近く、空間メトリックに混ぜない。

### 3.5 距離以外（予約。このスライスで実装しない）

同じ殻（スカラー距離＋ヒステリシス＋候補集合）に乗るもの:

| 距離の出どころ | 地図 | 例 |
|---|---|---|
| ユークリッド / AABB / 球 | 格子が焼いた体積 | 今のセル（この Plan） |
| グラフのホップ | 章・部屋・クエストの有向グラフ | ノベル先読み、インドアのポータル |
| 時間 | タイムライン | カットシーンの先読み |
| 論理 | フラグ | アンロック。四季は候補集合の差し替えの方がきれい |

グラフは**政策が読むカタログ**である。`WorldCellCatalog` が格子であることの対応物。
シーンツリーを DAG にしない。親は 1 つ。§5 の「依存 DAG や Requires リストは導入しない」は維持する。
寿命が木と一致しない BGM / 立ち絵は `AssetManagement` か親スコープのサービス。

予算と `maxInFlight` は「載せるか」ではなく制限器。メトリックの外側に付ける。

明示 Add（Pause）と親子縛り（Environment）は、このスライスの interface に載せない。

---

## 4. 通したあとの形（現行 4×4 で証明する）

本番セルは動かさない（S-3 と同じ）。名前は今の `Cell_0_0` のままでよい。

受入（分解セッションがテストに落とす。ここでは条件だけ）:

1. Controller の Tick が `CellIdentity.Format` を呼ばない
2. desired / retain は候補の AABB と LoadRadius / UnloadRadius で、現行 4×4 と同等の集合になる
3. 既存 WSC 10 本相当 / MultiFocus / 統合が緑（入力の与え方が座標列から体積列へ変わる）
4. R-3: `SwitchScene("Cell_0_0")` は今どおり失敗する。検出が名前文法以外になってもよい
5. FW に季節語が無い（W-1）
6. 生成器が既存収集で座標キーに潰さない（現行無修飾でも、フォルダ名を identity として照合する）

これが通るまで [SEASON_WORLD_DESIGN.md](SEASON_WORLD_DESIGN.md) の S-4（216 セル）に入らない。

---

## 5. やらないこと（この Plan の本文）

- 実装ファイル一覧、API の確定署名、テスト関数名（別セッション）
- 修飾 `TryParse`、`SeasonScopedStreamingBackend`、`StreamingConfig.cellIdQualifier`
- 9×6 の焼き込み、Season_* ノード、トンネル
- グラフメトリック / ノベル / HLOD（§22）
- §21 / §33 / §5 の本文改稿（harvest は口が通ってから）
- Unity.exe 起動、テスト全件実行（実装分解後も実装者は走らせない）

---

## 6. 分解セッションへ渡す問い

別セッションは次だけを分解すればよい。本文の契約をひっくり返さない。

1. Bounds の置き場（§3.3 の 3 候補）
2. `StreamingConfig` が持つもの（identity+Bounds の列か、SceneResource 参照か）
3. 生成器が AABB をいつ書くか（現行格子定数から焼いて埋め込む）
4. R-3 の検出をフラグへ移す範囲（`CellIdentity.IsCellId` を残す過渡か、一括か）
5. `CellScene.Coordinate` を残すか（HUD 用。距離判断からは外す）
6. 既存テストの入力を体積列へ移す手順（本番 4×4 は動かさない）

Editor 操作境界・`record` 禁止・偽 null 禁止・実装者はテスト未実行、は他スライスと同じ。
