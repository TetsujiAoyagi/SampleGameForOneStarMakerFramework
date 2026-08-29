# 移行 M-1 着手時 HANDOFF — 距離政策が identity＋体積を読む口を通す

> ステータス: **作業台。M-1 の 1 スライス専用。**
> 正本の順序は [STREAMING_SPATIAL_MIGRATION.md](STREAMING_SPATIAL_MIGRATION.md)、到着契約は [§34](../../unity/Assets/Docs/Architecture/34-ondemand-spatial-policy.md)。
> 本書は §34 をひっくり返さない。移行 HANDOFF §2 の A-1〜A-4 を満たす着手時指示書である。
> **期限:** M-1 が緑になり、移行 HANDOFF §3 の問い 1 / 2 / 3 / 5a に決定が書き戻された時点で `git rm`。
>
> `docs-audit.ps1` 検査3 の対象にしないため §7 / §8 は欠番（移行 HANDOFF と同じ理由）。

---

## 0. 一文

**現行 4×4 のまま、政策層が「identity ＋ 体積」しか読まない状態にする。**

本番 16 セルは動かさない。9×6 も `Season_*` も修飾パースも書かない。

---

## 1. 発注者裁定（実装はここを再解釈しない）

移行 HANDOFF §3 の問い 1〜3 と、世界稿 §11 の N-7 の答え。

移行 HANDOFF が想定していた「生成器が現行格子定数から AABB を焼いて埋め込む」は**却下**。採るのは:

> **Scene の編集があったら自動で AABB を計算して格納する。**

| # | 問い | 決定 |
|---|---|---|
| 1 | 体積の置き場 | **`SceneResource` 直下**（§34 §5 の第 3 候補） |
| 2 | `StreamingConfig` が持つもの | **寿命で 2 つに割って名前を捨てる**（下記 §2） |
| 3 | 生成器が AABB をいつ書くか | **シーン保存フック ＋ 全件再計算メニュー**。生成器は完了時に 1 回呼ぶだけ |
| 5a | `CellScene.Coordinate` | **残す**（HUD / テスト用。距離判断からは外れる）。`ComputeBounds` も残す |

追加の裁定:

- **セルの体積 = そのシーン ＋ 距離政策の候補でない子（Environment）の合併。**
- **取り出し口は新規 1 メソッドの `ISceneVolumeQuery`**（`SceneDirector` が実装）。`ISceneQuery` には足さない — あれは「**ロード済み**シーンへの読み取り専用アクセス」を自称しており、政策が体積を要るのは**未ロード**の候補についてだから、足すと文書が嘘になる。
- **`StreamByDistance` フラグは `SceneResource` 側**（`StreamingCandidate` には持たせない）。候補列に「候補でない」フラグを混ぜるのは矛盾。フラグは §34 §5 のとおりシーンのデータとして持ち、Editor の合併規則（候補でない子だけ親へ畳む）で M-1 から実際に使われる。M-3 はこのフラグを R-3 が読むようにするだけで済む。
- **体積が引けない候補は起動時に例外。** 暗黙フォールバックを作らない（黙って原点に潰れる方が悪い）。
- **候補集合の出どころは M-1 では `WorldCellCatalog` のまま。** 親コンテナの子から組むのは S-4。

### 受入 2（現行 4×4 と同等の集合）が成立する根拠

距離は体積の**中心** XZ（§34 §5。表面距離は採らない）なので、中心が一致すれば箱の大きさが違っても desired / retain 集合は今と同一になる。

| | 内容 | 自動計算した AABB |
|---|---|---|
| 葉セル 12 枚 | `Ground` 245m 立方がセル中心、Marker も中心、Prop は最大半径 118m < 半セル 125m | Ground の箱 = **セル中心** |
| 南辺 4 枚（sprout） | `Ground` が Environment 子側。Cell 本体は Marker ＋ 非対称な Prop のみ | 子を合併すれば **セル中心**。合併しないと中心が数十 m ずれ、境界セルが desired を出入りする |

**合併規則が受入条件そのものである。**

---

## 2. `StreamingConfig` を寿命で 2 つに割る

| 新型 | 持つもの | 寿命 |
|---|---|---|
| `StreamingCandidateSet` | 候補列（identity ＋ 体積） | **季節スワップのたびに丸ごと差し替わる** |
| `StreamingPolicySettings` | `LoadRadius` / `UnloadRadius` / `MaxInFlight` | **ずっと不変**。本当にチューニング値であるもの |

`Config` という語をやめる理由: 現状の 1 型は寿命の違う 2 つを同居させており、S-4 で候補集合だけ差し替えたいときに半径まで道連れで作り直すことになる。呼び出し箇所は 5 つ（テスト 4 ＋ Driver）しかなく、M-1 でどうせ全部書き換えるので、割るなら今しかない。

`CellGridConfig` も `Vector2Int` 列も政策層から消える。

---

## 3. A-1 変更ファイル（着手時の見積 → 実測）

「上限」は着手時に置いた**足す量の上限**、「実測」は実装後の行数。超えた行には理由を書く。

### FW Runtime

| ファイル | 現 | 上限 | 実測 | 責務 |
|---|---:|---:|---:|---|
| `Runtime/Streaming/StreamingCandidate.cs` | 新規 | 70 | 52 | identity ＋ Bounds の値型。空 identity / 空体積を弾く |
| `Runtime/Streaming/StreamingCandidateSet.cs` | 新規 | 100 | 71 | 候補列。1 件以上・identity 重複禁止・防御的コピー |
| `Runtime/Streaming/StreamingPolicySettings.cs` | 新規 | 80 | 54 | 半径 2 つ ＋ maxInFlight。既存の引数検証をここへ移す |
| `Runtime/Streaming/StreamingConfig.cs` | 85 | **削除** | 削除 | 上 2 つへ分割 |
| `Runtime/Streaming/WorldStreamingController.cs` | 300 | 295 | **301** | ctor が `(candidates, settings, backend)`。`Format` / `GetCellCenter` 削除。距離は `candidate.Volume.center`。**+6**: ctor 引数が 1 → 2 になり公開プロパティも 1 → 2 に増えた分が、`GetCellCenter` 削除分を上回った |
| `Runtime/SceneSystem/SceneResource.cs` | 67 | 100 | 98 | `_volume`(Bounds) / `_streamByDistance`(bool) ＋ internal setter |
| `Runtime/SceneSystem/ISceneVolumeQuery.cs` | 新規 | 35 | 33 | `bool TryGetSceneVolume(string identity, out Bounds volume)` |
| `Runtime/SceneSystem/SceneDirector.cs` | 228 | 250 | 259 | `ISceneVolumeQuery` 実装（既存の private `_sceneResourceMap` を引く）。**+9**: 偽 null を避けるため `?.` を使えず、3 条件を早期 return で分けた |

### FW Editor

| ファイル | 現 | 上限 | 実測 | 責務 |
|---|---:|---:|---:|---|
| `Editor/SceneGraph/SceneVolumeMath.cs` | 新規 | 90 | 94 | **純関数**。Bounds 列の合併 / 親と候補でない子の合併 / 空判定 |
| `Editor/SceneGraph/SceneVolumeRecalculator.cs` | 新規 | 260 | 288 | 合併の走査・体積の SerializedProperty 書き込み・全件メニュー。候補フラグは読むだけ |
| `Editor/SceneGraph/SceneVolumeSceneReader.cs`（**計画外の追加**） | 新規 | — | 180 | アセットと `.unity` の読み取り（資産探索・シーンパス解決・Renderer 収集） |
| `Editor/SceneGraph/SceneVolumeSaveHook.cs` | 新規 | 60 | 49 | `EditorSceneManager.sceneSaved` → 該当 SceneResource ＋ 祖先を再計算 |

### SampleGame

| ファイル | 現 | 上限 | 実測 | 責務 |
|---|---:|---:|---:|---|
| `InGame/InGameSession/Streaming/SessionWorldStreamingDriver.cs` | 207 | 250 | 241 | 候補列の組み立て（identity は Catalog、体積は `ISceneVolumeQuery`）。欠落は例外 |
| `InGame/InGameSession/Streaming/WorldCellCatalog.cs` | 210 | 215 | 216 | 変更は `CreateGridConfig` の remark 追加のみ（参照 0 になったが**意図的な先行宣言**として残す） |
| `DependOnAll/Editor/WorldCellStreamingSliceCreator.cs` | 1384 | 1392 | 1401 | 生成完了時に再計算を 1 回呼ぶ。**+9**: 生成中の保存フック停止（try/finally）を足した |

### テスト

| ファイル | 現 | 上限 | 実測 |
|---|---:|---:|---:|
| `Tests/Streaming/StreamingCandidateFixtures.cs`（新規・共有ヘルパー） | 新規 | 80 | **141** |
| `Tests/Streaming/WorldStreamingControllerTests.cs` | 464 | 470 | 406 |
| `Tests/Streaming/WorldStreamingControllerMultiFocusTests.cs` | 197 | 200 | 112 |
| `Tests/Streaming/StreamingIntegrationTests.cs` | 658 | 665 | 611 |
| `Tests/Streaming/CameraStreamingFocusAdapterTests.cs` | 169 | 175 | 129 |
| `Tests/Streaming/StreamingCandidateSetTests.cs`（新規） | 新規 | 90 | 108 |
| `Tests/Editor/SceneGraph/SceneVolumeMathTests.cs`（新規） | 新規 | 130 | 144 |

フィクスチャが見積の 1.8 倍になったのは、4 ファイルに散っていた `DenseCells` / `CellCenter` / `XzDistance` / `NearestFocusDistance` / `ComputeCellsWithinRadius` / `ComputeUnionDesired` を 1 本へ寄せたため。既存 4 ファイルは合計 1488 → 1258 行に減っており、テスト全体では差し引き 89 行の減。

### 着手時 HANDOFF から外れた 2 点（実装中に判明。M-2 以降が再解釈しないため記録する）

1. **候補フラグは導出をやめ、生成器が焼く決定にした。** 着手時は「`_streamByDistance` は『体積が空でない』で決まる」と書いていたが、それでは Environment（Ground を持つ）まで候補になる。次に「体積が空でなく、かつ候補である祖先を持たない」へ直したが、**これも Editor で実測して誤爆した** — `PlayerScene`（プレイヤーのカプセルに Renderer がある）が候補フラグ 1 で焼かれた。今日は無害だが、M-3 で R-3 がこのフラグを読み始めた瞬間に `SwitchScene("PlayerScene")` が理由なく弾かれる。
   - **結論: 「距離政策の候補か」は幾何から導出できる事実ではなく決定である**（§34 §5 が「フラグ」と呼んでいるのはそういう意味だった）。それを知っているのは作業単位を焼く生成器だけである。
   - 着地: `WorldCellGenerator.ConfigureSceneResource` が Cell に `true`、`EnsureEnvironmentResource` が Environment に `false` を焼く。`SceneVolumeRecalculator` は**体積だけ**を書き、フラグは合併判断のために**読むだけ**。
   - 副作用: 新しく作業単位を足したときは生成器を回さないとフラグが付かない。再計算メニューだけでは付かない。これは正しい — フラグは決定であって観測ではない。
2. **Editor 側を 3 ファイルに割った。** `SceneVolumeRecalculator` を 1 ファイルで書くと 437 行・3 責務（アセット探索 / 走査規則 / 書き込み）になったため、`.unity` とアセットの読み取りを `SceneVolumeSceneReader` へ抜いた。A-2 の「純関数と I/O を分ける」の延長であり、使い捨てスクリプトではないので分割の方を選んだ。

### レビューで挙がった残件（M-1 では直さない。所有者ごと）

| 残件 | 所有者 |
|---|---|
| **体積の収集範囲がそのまま政策の入力になった。** `SceneVolumeSceneReader` は全 `Renderer` を `includeInactive: true` で拾う。今の Ground 箱では中心が格子に乗るが、Particle / Trail / 無効化したデバッグメッシュをセルに足して保存すると中心が跳ね、境界セルが desired を出入りし得る。Collider だけ・Renderer 無しのオブジェクトは逆に寄与しない。表面距離は採らない契約（§34 §5）なので今日は同等集合のままだが、**体積が正本になった以上ここは政策の入力そのもの**である | **S-4**（収集範囲に規約が要るなら、谷を焼くときに決める） |
| **`ISceneVolumeQuery` は失敗理由を返さない。** 未登録 / フラグ off / 体積が空 の 3 つを 1 つの `false` に畳んでいる。M-1 では Driver 側の例外文に 3 つとも並べて誤診を防いだ | **M-3**（R-3 がこの口を本番の起動失敗として使うなら、そのとき enum へ署名を開ける。**M-1 で先回りしない**） |
| **`WorldStreamingController.Candidates` に差し替え口が無い。** 候補集合は丸ごと作り直す型なので今は正しい | **S-4**（in-flight を抱えたまま集合だけ替えたくなったら WSC 側に口を足す） |

## 4. A-2 分割先

`SceneVolumeMath`（純関数）と `SceneVolumeRecalculator`（Editor I/O）を最初から分ける。前者だけがテスト対象で、後者は Unity を開かないと動かない。**使い捨てスクリプトではないので分割する。**

実装では I/O 側がさらに 2 つに割れた（`SceneVolumeRecalculator` = 走査規則と書き込み / `SceneVolumeSceneReader` = アセットと `.unity` の読み取り）。理由は §3 末尾の逸脱 2。

## 5. A-3 新責務

**あり。** 「シーンが自分の占める体積をデータとして持ち、編集時に自動で更新される」。FW Runtime に体積フィールドと問い合わせ口、FW Editor に再計算機構が増える。これは §34 §5「体積はデータの正本」の実装であり、M-1 の本題そのもの。

## 6. A-4 テスト要求

- **残す（入力の与え方だけ変える）**: WSC 10 本相当 ＋ T-B 空隙 1 / MultiFocus 3 / 統合 6 / CameraStreamingFocusAdapter 3。期待値の算出は「セル中心 = 体積中心」なので**数値は 1 つも変わらない**。
- **新規**: `SceneVolumeMath` 4〜6 本（合併 / 空 / 候補でない子だけ畳む / 候補の子は畳まない）、`StreamingCandidateSet` 検証 2 本（空列 / identity 重複）、`StreamingPolicySettings` 検証 2 本（半径の順序 / maxInFlight）。
- `Task.Delay` / `Thread.Sleep` 禁止。全件実行は Phase C。実装者は「実装完了。テスト未実行」と報告する。

---

## 9. 実装手順

### 9.1 FW Runtime — 体積をデータにする

`SceneResource.cs` に 2 フィールドを足す。既存の `Identity` と同じ `internal set` 方式（Editor は `SerializedProperty` 経由で書く。`WorldCellGenerator.ConfigureSceneResource` が既にその流儀）。

```
[SerializeField] private Bounds _volume;             // ワールド AABB
[SerializeField] private bool _streamByDistance;     // 距離政策の候補か（§34 §5）
```

`ISceneVolumeQuery.cs` を新規作成し、`SceneDirector` に実装を足す。`_sceneResourceMap.GetSceneResource(identity)` を引き、`resource != null && resource.StreamByDistance && volume.size != Vector3.zero` のときだけ true。

> **偽 null**: `SceneResource` は `UnityEngine.Object`。`?.` / `??` / `is null` / `ReferenceEquals` を使わず `== null` / `!= null` で書く（`SceneResourceMap.BuildDictionary` が既にこの流儀）。

### 9.2 FW Runtime — 政策層から座標を抜く

3 つの新規型を作り、`StreamingConfig.cs` を削除する。

```
public readonly struct StreamingCandidate      // identity ＋ 体積
public sealed class StreamingCandidateSet      // 季節ごとに作り直す
public sealed class StreamingPolicySettings    // ずっと不変
```

引数検証は既存 `StreamingConfig` のものを寿命どおりに振り分ける（1 件以上 → CandidateSet、半径の順序と maxInFlight → Settings）。CandidateSet には **identity 重複の検証**を足す（W-2 の芽。四季が同じ体積を共有する以上、重複は必ず設定ミス）。

`WorldStreamingController` の ctor を `(StreamingCandidateSet candidates, StreamingPolicySettings settings, ISceneStreamingBackend backend)` にする。差分発火・ヒステリシス・in-flight・priority・G-6 再照合の**ロジックは 1 行も変えない**。`GetCellCenter` と `CellIdentity.Format` は削除。

**着地の目印**: `unity/Assets/OneStarMaker/Scripts/Runtime/Streaming/` から `CellIdentity` / `CellGridConfig` の参照が 0 になる。M-4 の下準備でもある。

### 9.3 FW Editor — 自動計算

`SceneVolumeMath`（純関数、テスト対象）:

- `TryUnion(IReadOnlyList<Bounds> parts, out Bounds result)` — 空なら false
- `Merge(Bounds own, IReadOnlyList<(Bounds volume, bool streamByDistance)> children)` — `streamByDistance == false` の子だけ合併する（§34 §6: 子は距離政策の候補にしない。だが空間的には同じ作業単位）

`SceneVolumeRecalculator`（Editor I/O）:

- 1 シーン分: `.unity` を Additive で開き、全ルートの `Renderer.bounds` を集めて `TryUnion` → 閉じる（`WorldCellStreamingSliceCreator.PopulateSingleCellScene` と同じ開閉の流儀）
- 子を持つ場合は、**保存済みの子の体積**を `Merge` で畳む（子のシーンを開き直さない）
- 書き込みは `SerializedProperty`。**書くのは `_volume` だけ**で、`_streamByDistance` には触らない（§3 末尾の逸脱 1）。フラグは合併規則の入力として読むだけである
- メニュー: 全件をボトムアップで再計算する 1 項目
- **名前文法を一切使わない。** 親子は `SceneResource.Parent` / `Children`、identity 引きは `SceneResourceMap.GetSceneResource`

`SceneVolumeSaveHook`: `EditorSceneManager.sceneSaved` で、保存されたシーン名を identity として Map を引き、自分 → 親と遡って再計算する。

`WorldCellStreamingSliceCreator` の生成完了時にも全件再計算を 1 回呼ぶ。

### 9.4 SampleGame — 候補列を組む

`SessionWorldStreamingDriver` の ctor で、`WorldCellCatalog.EnumerateCells()` の identity 列（`CellIdentity.Format`。SampleGame 側なので M-1 では可）に対し `ISceneVolumeQuery.TryGetSceneVolume` を引き、`StreamingCandidateSet` を作る。`StreamingPolicySettings` は `WorldCellCatalog` の半径定数から別に作る。

**1 件でも引けなければ、再計算メニュー名を含めた例外を投げて落とす。** `GetResidentCellIdentities` も候補列の identity を舐める形にして `Format` を消す。

### 9.5 ドキュメント（同一ブランチ）

- `docs/streaming/STREAMING_CURRENT_SPEC.md` — §1 の一文、§2 の実装値、§3 の型表、§4 の「名前文法が空間になっている箇所」を M-1 後の実態へ。**新旧を併存させない**
- `docs/handoff/STREAMING_SPATIAL_MIGRATION.md` — §3 の問い 1 / 2 / 3 / 5a を「決定済み」にして落とす。M-1 の行に決定内容を 1 行で残す
- 索引に触る必要がある箇所を `pwsh tools/docs-audit.ps1` で確認する

---

## 10. 検証

**実装者は Unity を起動しない**（Editor 操作境界。正本は `.agents/skills/osm-unity-editor/SKILL.md`）。実装完了時は「実装完了。テスト未実行」と報告する。

### 静的（実装者がやる）

```bash
grep -rn "CellIdentity\|CellGridConfig" unity/Assets/OneStarMaker/Scripts/Runtime/Streaming/
```

0 件であること（M-1 受入 1）。

```bash
grep -rnE "Season|Spring|Summer|Autumn|Winter|季節" unity/Assets/OneStarMaker/
```

0 件であること（W-1 / M-1 受入 5）。

新規・変更した全 `.cs` の先頭に `#nullable enable` があること。`record` を 1 つも書いていないこと（`IsExternalInit` が無く、書いた瞬間にプロジェクト全体がコンパイル不能になる）。破棄されうる `UnityEngine.Object` に対して `?.` / `??` / `is null` / `ReferenceEquals` を使っていないこと。

### テスト（Phase C。Unity Editor を閉じてから）

```bash
pwsh tools/run-tests.ps1
```

exit 0 かつ 1 件以上実行され failed 0。**テスト 0 件は失敗扱い**。終了コード `0xC0000005` はシャットダウン時クラッシュなので、コード変更を疑う前にログ末尾と `TestResults/` の XML を見る。

絞り込むなら `-Filter OneStarMaker.Tests.Streaming`。

### Editor（人間が開いた Editor で 1 回）

1. 全件再計算メニューを実行 → 現行 20 枚前後の `.asset` に体積が入り、Cell 16 枚の `_streamByDistance` が true、`_volume.center` がセル中心（Y 以外）になる
2. Play → 現行 4×4 が今までと同じ順序・同じ枚数で載る（M-1 受入 2）
3. `SwitchScene("Cell_0_0")` が今どおり失敗する（M-1 受入 4。M-3 まで名前文法のままでよい）
4. 適当なセルの `.unity` に大きな箱を 1 個足して保存 → その SceneResource の `_volume` が自動で広がる（自動計算の実演）

### 受入対応表

| 移行 HANDOFF §4 | 満たし方 |
|---|---|
| 1. Tick が `Format` を呼ばない | §9.2 ＋ 静的 grep |
| 2. 現行 4×4 と同等の集合 | 体積中心 = セル中心（Environment 合併で南辺 4 枚も成立）＋ Editor 手順 2 |
| 3. 既存 WSC / MultiFocus / 統合が緑 | `run-tests.ps1`。期待値の数値は不変 |
| 4. R-3 は今どおり失敗 | 触らない。Editor 手順 3 |
| 5. FW に季節語が無い | 静的 grep |

---

## 11. 将来の空間索引（M-1 では入れない。形だけ塞がないことを確認する）

OctTree 等は §34 §7 の「距離の出どころの差し替え」ではなく、**同じユークリッド距離の加速構造**である。新しいメトリック interface は要らない。

将来の 1 Tick の形（M-1 の線形走査と**同じ順序**。中身だけ差し替わる）:

```
Settings.UnloadRadius（大きい方）を引数に、CandidateSet へ 1 回だけ問う
  → [(identity, 距離), ...]                     ← retain 候補 = desired の上位集合
  → Controller が LoadRadius / UnloadRadius で切り分け、距離昇順、maxInFlight で頭を切る
  → Backend.RequestAdd(identity, priority)
```

- **半径は検索の引数であって、検索後の条件ではない。** `UnloadRadius > LoadRadius` は不変条件なので、大きい方で 1 回引けば desired / retain の両方が作れる。2 回引かない
- **identity は「あとから引き直す」ものではない。** 索引のノードが最初から `(identity, 体積)` を持ち、体積は距離計算にだけ使い、identity はそのまま Backend へ渡る。体積や座標から identity を復元する経路を作った瞬間、§34 が殺した `座標 → Format → id` と同じ第二の主キーが方向違いで復活する
- **置き場は `StreamingCandidateSet`。** 候補集合が変わったら作り直す型なので、索引の寿命と候補集合の寿命が最初から一致する（§34 §6）。中身が線形走査から索引に変わるだけで **interface は 1 本も増えない**
- **落とし穴: 索引だけでは Tick は sub-linear にならない。** 現 `Tick` は全候補に `Backend.IsLoaded` を撃って `loaded` を作っている（G-6 再照合）。近傍だけ走査すると「ロード済みだが遠くへ行った候補」が見えず Unload が漏れる。sub-linear にする日に触るのは **(1) `StreamingCandidateSet` の索引（interface 不要） ＋ (2) Backend に「ロード済み identity 列」を訊く口を足す（唯一の interface 変更）** の 2 点セット
- **いつ: 実測で痛くなってから。** 216 セル × 5Hz = 毎秒 1080 回の距離計算は無視できる

M-1 の形（`StreamingCandidateSet` が不変 / 候補列がインデックス可能な `IReadOnlyList` / Controller が体積中心の距離しか見ない）は上の 2 点をどちらも塞がない。**M-1 で先回りして索引や Backend の口を作らない。**

---

## 12. やらないこと

9×6 の焼き込み / `Season_*` / トンネル / 既存 16 セルの移動・全廃、修飾パース・`SeasonScopedStreamingBackend`・`StreamingConfig.cellIdQualifier`、生成器の既存収集と policy のキー剥がし（**M-2**）、R-3 をフラグへ移すこと（**M-3**）、`Runtime/SceneSystem/Cells/` を FW から下ろすこと（**M-4**）、`GameSceneFactory` / `CellScene` ctor / `TryFromCellId` の修飾対応（S-4）、§21 / §33 本文の全面改稿。

`CellScene.Coordinate` と `ComputeBounds` は残す（HUD / テスト用。距離判断からは既に外れる）。**参照が無いことを根拠に何かを削除しない。**
