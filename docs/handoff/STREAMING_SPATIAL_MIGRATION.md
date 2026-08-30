# Streaming 空間政策の移行 HANDOFF (2026-08-29)

> ステータス: **作業台。** 到着契約は公開面 [§34](../../unity/Assets/Docs/Architecture/34-ondemand-spatial-policy.md)。現状は [STREAMING_CURRENT_SPEC.md](../streaming/STREAMING_CURRENT_SPEC.md)。
> 本書は格子キーを殺す順序だけを書く。契約をひっくり返さない。
> 現行レイアウト（4×4）で口を通す手順は**ここだけ**に置く。§34 の主語にしない。
>
> **harvest 先:** 口が通ったら実装値を `STREAMING_CURRENT_SPEC.md` に移し、§21 の現状記述を追随させる。契約の追加判断があれば §34 へ。
> **期限（git rm）:** M-1〜M-4 が全て通った時点。Controller が `Format` せず、体積がデータであり、生成器の既存収集 / policy のキーが identity 文字列であり、R-3 が名前文法を見ず、`Runtime/SceneSystem/Cells/` が FW 公開面に無い。そのあと本書を `git rm`。M-1 + M-2 だけで消さない。**S-4 の開始条件ではない。**
> **S-4 のゲート:** **M-1 の受入**（体積の口）**と M-2 の受入**（生成器 / policy のキーが identity 文字列）。M-2 は M-1 と同ブランチでも、S-4 と同ブランチでもよい（同スライスなら修飾付き生成の前に通す）。M-3 は S-4 より前か同ブランチ。M-4 は S-4 と同時可。`git rm` の期限と混ぜない。
> **世界構図:** [SEASON_WORLD_DESIGN.md](SEASON_WORLD_DESIGN.md)。進行中正本は世界稿と本書の 2 つ。S-3 記録は [STREAMING_CURRENT_SPEC.md](../streaming/STREAMING_CURRENT_SPEC.md)（公開面。実装指示ではない）。
>
> `docs-audit.ps1` 検査3の対象にしないため §7 / §8 は欠番。

実装エージェントへ: **§34 を先に読む。** `CellIdentity.TryParse` を修飾対応すること、`StreamingConfig` に qualifier を足すこと、Backend デコレータで id を翻訳することは、本書の指示ではない。本番セルは動かさない。既存 16 枚の全廃は S-4。

---

## 0. 一文

**現行 4×4 のまま、距離政策が座標列ではなく identity＋体積を読む口を通す。**
通るまで 9×6×4 を焼かない。矩形 4 つ＋空隙も書かない。修飾パースも書かない。

---

## 1. なぜ移行が別文書か

§34 は到着契約であり、現行レイアウトを主語にしない。
ここに 4×4 を書くのは、口を通す証明の足場であって契約ではない。

混ぜると「現行セルを動かさずに口を通す」が到着条件になる。それが部分解の再発である。

---

## 2. スライス（1 本 = 1 ブランチ = 1 着手時 HANDOFF）

本書は複数スライスに跨る移行の正本である。着手時に短い指示書を切ってよい。切らないなら本書の該当節だけを実装対象にする。

| # | 内容 | 本番セル |
|---|---|---|
| M-1 | **実装済み・受入 1〜5 すべて充足（全件 505/505 passed）。** 体積は `SceneResource` 直下のデータで、Editor（保存フック ＋ 全件メニュー）が `.unity` から自動計算する。`StreamingConfig` は寿命で `StreamingCandidateSet` / `StreamingPolicySettings` に割った。取り出し口は新規 `ISceneVolumeQuery`。着手時 HANDOFF は harvest 済みで git 履歴にある | 動かさない |
| M-2 | 生成器の既存収集 / policy のキーを identity 文字列へ | 動かさない |
| M-3 | R-3 の**口を作る**。検出を距離政策の候補フラグへ。現行 `Cell_0_0` で `SwitchScene` が失敗し続けること | 動かさない（無修飾のまま） |
| M-4 | `Runtime/SceneSystem/Cells/`（`CellIdentity` / `CellGridConfig` / `CellScene`）を FW 公開面から下ろす。SampleGame または Editor へ | 動かさない |
| S-4 以降 | 谷の生成・Season_*。正本は [SEASON_WORLD_DESIGN.md](SEASON_WORLD_DESIGN.md) | **全廃**（移送しない） |

ゲート（次の実装がここで部分解を出さないための固定）:

- **S-4 の前提は M-1 + M-2**（体積の口と、生成器 / policy のキーが identity 文字列）。両方の受入が通るまで 9×6×4 を焼かない。M-2 は M-1 と同ブランチでも、S-4 と同ブランチでもよい。同スライスなら修飾付き生成の前にキーを identity 文字列にする（`CollectExistingStates` が `TryParse` → 座標キーのままだと `Spring_Cell_*` が見えず、`Format` が `Cell_*` を 9×6 に伸ばす）
- **M-2 / M-3 / M-4 も M-1 のあと。**
- **M-3 は S-4 より前か同ブランチ**（修飾付き identity で R-3 が空洞化しないため）
- **M-4 は M-3 の後。S-4 と同時でもよい**（factory が `IsCellId` をやめる瞬間に型を下ろせる）
- **本書を `git rm` する期限は M-1〜M-4 全部。** S-4 のゲートと混ぜない

R-3 の所有者: **M-3 が口を作る。S-4 が修飾付き名で効かせる。** どちらも「名前文法から外す」とだけ書くと二重所有になる。factory の SceneBase 結線（`IsCellId` → `DemoCellScene`）は S-4。M-3 は factory を動かさない。

着手時に短い指示書を切るときは、次を本文に入れる（切らない場合も A-1 だけは着手前に出す）。

| # | 必須 |
|---|---|
| A-1 | 変更ファイルの現在行数 → 予想行数 / 責務。予想は足す量の上限 |
| A-2 | 分割先。一度きりの生成スクリプトは例外として明示 |
| A-3 | 新責務を足すならその旨。足さないなら「新責務なし」 |
| A-4 | テスト要求（本数・何を残すか・新規） |

退役して復活させない: 修飾パース、`SeasonScopedStreamingBackend`、`StreamingConfig.cellIdQualifier`、矩形 4 つ＋空隙の Catalog。

---

## 3. 移行で決めること（契約は §34。署名は各スライス）

§34 をひっくり返さない。次だけを分解する。M-1 セッションは M-2 / M-3 / M-4 の問いを「決める」対象にしない。

| # | 問い | 所有者 | 決定 |
|---|---|---|---|
| 1 | 体積の置き場（§34 の 3 候補。避けたいのは identity 文法と座標の第二キー） | M-1 | **決定済み: `SceneResource` 直下**（第 3 候補）。`_volume`(Bounds) ＋ `_streamByDistance`(bool) |
| 2 | `StreamingConfig` が持つもの（identity＋体積の列か、SceneResource 参照か）。`Vector2Int` 列と `CellGridConfig` による中心組み立ては捨てる | M-1 | **決定済み: 寿命で 2 つに割って `StreamingConfig` を捨てた。** `StreamingCandidateSet`（identity ＋ 体積。差し替えるとき丸ごと作り直す）と `StreamingPolicySettings`（半径 ＋ maxInFlight。不変） |
| 3 | 生成器が AABB をいつ書くか（現行格子定数から焼いて埋め込む。ランタイムは焼かない） | M-1 | **決定済み: 想定を却下し、Scene の編集で自動計算する。** `EditorSceneManager.sceneSaved` フック ＋ 全件再計算メニュー ＋ 生成完了時に 1 回。値は `.unity` の Renderer の合併であって格子定数ではない |
| 4 | R-3 の検出をフラグへ移す範囲（`CellIdentity.IsCellId` を残す過渡か、一括か）。**factory の SceneBase 結線（`IsCellId` → `DemoCellScene`）とは別口。** | **M-3** | 未決 |
| 5a | `CellScene.Coordinate` を残すか（HUD 用。距離判断からは外す） | M-1 | **決定済み: 残す。** `ComputeBounds` も残す（テスト用）。距離判断からは外れた |
| 5b | 型そのもの（`CellIdentity` / `CellGridConfig` / `CellScene`）を FW から下ろす | **M-4** | 未決 |
| 6 | 既存テストの入力を体積列へ移す手順（本番 4×4 は動かさない） | M-1 | **決定済み: 共有フィクスチャ 1 本**（`StreamingCandidateFixtures`）が均一格子から体積を焼く。体積中心 = セル中心なので期待値の数値は不変 |
| 7 | 生成器の policy 解決と既存収集のキーを identity 文字列へ移す範囲（§34。現行無修飾でもフォルダ名照合に寄せて証明する） | **M-2** | **決定済み: `(identity, coordinate)` の target 列を1本の正本にする。** policy / existing / generator plan / adoption / path / Graph / Map / World children / Addressables / deletion に同じ target identity を流す。座標は生成メタデータのみ。同一座標の別 identity は許可、duplicate identity は Ordinal で例外。実装署名と分割は `STREAMING_SPATIAL_M2.md` |

M-1 が新たに決めたこと（上の 3 と不可分なので、M-2 以降が再解釈しないためにここへ残す）:

- **セルの体積 = そのシーン ＋ 距離政策の候補でない子（Environment）の合併。** 南辺 4 枚は `Ground` が Environment 側にあり、合併しないと中心が数十 m ずれて境界セルが desired を出入りする。**合併規則が受入 2 そのものである。**
- **候補フラグは幾何から導出しない。生成器が焼く決定である。** `WorldCellGenerator` が Cell に `true`、Environment 側に `false` を書き、Editor の再計算機構は体積だけを書いてフラグは読むだけ。導出（「体積が空でなければ候補」等）は Editor 実測で誤爆した — `PlayerScene` がカプセルの Renderer だけで候補になった。**M-3 で R-3 がこのフラグを読むので、誤爆したまま進めると `SwitchScene` が理由なく弾かれる。**
- **取り出し口は新規 `ISceneVolumeQuery`。** `ISceneQuery` へは足さない（あれは「ロード済みシーンへの読み取り専用アクセス」を自称しており、政策が体積を要るのは未ロードの候補についてだから）。
- **体積が引けない候補は起動時に例外。** 暗黙フォールバックを作らない。

Environment は距離政策の候補に入れない（距離の単位は Cell 作業単位）。子は親 Stable 後の明示 Add のまま。M-1 は無修飾 4×4 のまま factory を動かさない。

---

## 4. M-1 の受入（現行 4×4 で証明する）

本番セルは動かさない。名前は今の `Cell_0_0` のままでよい。
既存 16 枚の全廃は [SEASON_WORLD_DESIGN.md](SEASON_WORLD_DESIGN.md) の S-4。

1. Controller の Tick が `CellIdentity.Format` を呼ばない
2. desired / retain は候補の体積と LoadRadius / UnloadRadius で、現行 4×4 と同等の集合になる（距離は体積中心の XZ）
3. 既存 WSC 10 本相当 / MultiFocus / 統合が緑（入力の与え方が座標列から体積列へ変わる）
4. R-3: `SwitchScene("Cell_0_0")` は今どおり失敗する。検出が名前文法以外になってもよい（M-3 まで名前文法のままでも M-1 は通せる）
5. FW に季節語が無い（W-1）

M-1 は 1〜5 に閉じる。生成器の座標キー剥がしは M-2 の受入だけに置く。

**S-4 のゲートは M-1 の受入（1〜5）と M-2 の受入。** M-3 / M-4 の受入は本書の `git rm` 期限であり、S-4 開始条件ではない。ただし M-3 は修飾付き identity で R-3 が空洞化しないよう、S-4 より前か同ブランチで通す。

### M-2 の受入

生成器の既存収集 / policy のキーが identity 文字列である。座標キーで 4 季節が潰れないことを、現行無修飾 4×4 のフォルダ名照合で証明する。**S-4 のゲートに含む。** 修飾付き `Spring_Cell_*` を焼く前に通す（未了なら S-4 と同スライスで、生成の前に通す）。

実装境界は [STREAMING_SPATIAL_M2.md](STREAMING_SPATIAL_M2.md)。`CellPopulationPlan` だけを文字列辞書へ変えて終わりにしない。
`WorldCellGenerator` が内部で identity を再構築すると plan と実生成が分離するため、target 列を generator、既存 asset adoption、
SceneGraph、Map / World children、Addressables、削除まで通す。M-2 は本番4×4の asset を変更しない。

### M-3 の受入

R-3 が `CellIdentity.IsCellId` を見ない。`SwitchScene("Cell_0_0")` はフラグ（距離政策の候補）で失敗する。修飾付き名での着地は S-4（[SEASON_WORLD_DESIGN.md](SEASON_WORLD_DESIGN.md)）。

実装時の判定口は `ISceneVolumeQuery` ではなく、`SceneDirector` が持つ `SceneResourceMap` の
`SceneResource.StreamByDistance` を直接読む。Volume が空でも flag true なら拒否する。
ガードは from / to の双方を span、LoadingDisplay、履歴、Unload / Add より前に検査する。
未登録・破棄済み・flag off は R-3 では拒否しない。公開 API / DI / asmdef は増やさない。

テストは Cell 型に依存しない transition テストへ置き、任意名 flag true、`Cell_0_0` flag off、
空 Volume flag true、from / to、失敗時の履歴・表示・ロード状態不変を証明する。

### M-4 の受入

`unity/Assets/OneStarMaker/` に `Cell_{x}_{y}` 文法の型（`CellIdentity` / それを公開する `CellScene` / ランタイムの `CellGridConfig`）が無い。生成器入力の格子定数は Editor または SampleGame。

移送先はフラットな root と生成済み `World/Cells/` を避け、次で固定する。

```text
SampleGame/InGame/InGameSession/World/CellScenes/
  CellIdentity / CellGridConfig / CellScene
  DemoCellScene / EnvironmentIdentity / EnvironmentScene

SampleGame/DependOnAll/Editor/Streaming/Cells/
  WorldCellStreamingSliceCreator / HandEditProbe
  Generation/  WorldCellGenerationTarget / WorldGridDefinition / WorldCellGenerator
  Planning/    CellAuthoringPolicy / CellPopulationPlan
  State/       identity keyed の既存収集 / folder reconciliation
```

`.cs` と `.meta` を一緒に移し GUID を維持する。`WorldGridDefinition.asset` は YAML 手編集せず、
AssetDatabase から新しい型としてロードでき、origin / cellSize / rectangles / output path が保持されるテストを置く。
Framework と SampleGame に重複する `CellRect` は SampleGame 側へ統一し、serialized field は変えない。
`CellIdentity` の ZString は `string.Concat` 等へ置換し、asmdef 参照を足さない。

M-4 の静的受入 grep は production code の `unity/Assets/OneStarMaker/Scripts/` を対象にする。
`GameSceneFactory` の名前文法による `DemoCellScene` 選択は S-4 所有なので、M-4 は参照先を移すだけで挙動を変えない。

### Phase B / C / C' のモデル分離

各スライスの着手時 HANDOFF に実績欄を作り、次の分離を既定とする。将来の固定割り当てではなく、
今回の M-2〜M-4 実行キューに対する選定である。

- Phase B 実装・指摘修正: `gpt-5.6-luna`
- Phase C 構造／機能レビューとテスト: `gpt-5.6-terra` の新規セッション
- Phase C' 独立監査: `gpt-5.5` の新規セッション
- 最終統合チェック: 親セッション

writer は常に1名。依存する M-2 → M-3 → M-4 は直列にし、構造レビューとテスト等の read-only / 非競合検査だけを並列化する。
Phase C 指摘は Phase B 担当へ戻し、修正後に C を再実行する。C が指摘0になってから C' を行い、C' 指摘後も C からやり直す。
同じ prompt / CLI command を無応答だけを理由に再送せず、status / log / cursor を確認して writer の二重起動を防ぐ。

---

## 5. やらないこと

- §34 の契約を現行レイアウトの語彙で書き直すこと
- 9×6 の焼き込み、Season_* ノード、トンネル、既存 16 セルの全廃
- `GameSceneFactory` / `CellScene` ctor / `TryFromCellId` の修飾対応（S-4）
- グラフメトリック / ノベル / HLOD
- §21 / §33 / §5 の本文全面改稿（§33 の退役表と §21 の現状バナーは公開面で済み）
- Unity.exe 起動、テスト全件実行（実装者は走らせない）
- `record` の使用（`IsExternalInit` が無い）

Editor 操作境界（正本は `.agents/skills/osm-unity-editor/SKILL.md`）。人間が開いた Editor への `unity status` / `unity command` / `unity eval` のみ可。YAML 手編集禁止。偽 null 禁止。テストで `Task.Delay` / `Thread.Sleep` 禁止。Cloud では Unity CLI を叩かない。

---

## 6. 旧稿からの吸収

`SCENE_WORLD_BOUNDS.md` の契約本文は §34 へ移した。分解問と 4×4 証明は本書へ移した。
`SEASON_LEVELS_IMPLEMENTATION.md` の S-3 実測は `STREAMING_CURRENT_SPEC.md` へ移した。
どちらも git 履歴に残る。本文は復活させない。
