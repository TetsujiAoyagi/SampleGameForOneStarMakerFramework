# Streaming 空間政策の移行 HANDOFF (2026-08-29)

> ステータス: **作業台。** 到着契約は公開面 [§34](../../unity/Assets/Docs/Architecture/34-ondemand-spatial-policy.md)。現状は [STREAMING_CURRENT_SPEC.md](../streaming/STREAMING_CURRENT_SPEC.md)。
> 本書は格子キーを殺す順序だけを書く。契約をひっくり返さない。
> 現行レイアウト（4×4）で口を通す手順は**ここだけ**に置く。§34 の主語にしない。
>
> **harvest 先:** 口が通ったら実装値を `STREAMING_CURRENT_SPEC.md` に移し、§21 の現状記述を追随させる。契約の追加判断があれば §34 へ。
> **期限:** Controller が `Format` せず、体積がデータであり、生成器の既存収集 / policy のキーが identity 文字列になった時点。そのあと本書を `git rm`。
> **世界構図:** [SEASON_WORLD_DESIGN.md](SEASON_WORLD_DESIGN.md)。S-4（216 セル）は本書の口が通ってから。
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
| M-1 | 体積をデータとして持ち、Controller が座標も `Format` も使わない。候補は identity 列 | 動かさない |
| M-2 | 生成器の既存収集 / policy のキーを identity 文字列へ | 動かさない |
| M-3 | R-3 を名前文法から外す。factory の `IsCellId` 結線は S-4 と同時でよい | 動かさない（無修飾のまま） |
| S-4 以降 | 谷の生成・Season_*。正本は [SEASON_WORLD_DESIGN.md](SEASON_WORLD_DESIGN.md) | **全廃**（移送しない） |

M-1 が通るまで M-2 / M-3 / S-4 に入らない。M-2 は M-1 と同ブランチでもよい（座標キーのままだと後で 4 季節が潰れるため、早めに寄せる）。

退役して復活させない: 修飾パース、`SeasonScopedStreamingBackend`、`StreamingConfig.cellIdQualifier`、矩形 4 つ＋空隙の Catalog。

---

## 3. M-1 で決めること（契約は §34。署名はここ）

§34 をひっくり返さない。次だけを分解する。

1. 体積の置き場（§34 の 3 候補。避けたいのは identity 文法と座標の第二キー）
2. `StreamingConfig` が持つもの（identity＋体積の列か、SceneResource 参照か）。`Vector2Int` 列と `CellGridConfig` による中心組み立ては捨てる
3. 生成器が AABB をいつ書くか（現行格子定数から焼いて埋め込む。ランタイムは焼かない）
4. R-3 の検出をフラグへ移す範囲（`CellIdentity.IsCellId` を残す過渡か、一括か）。**factory の SceneBase 結線（`IsCellId` → `DemoCellScene`）とは別口。** M-1 は無修飾 4×4 のまま factory を動かさない
5. `CellScene.Coordinate` を残すか（HUD 用。距離判断からは外す）。型そのものを SampleGame へ下ろすタイミングは M-1 では必須ではない
6. 既存テストの入力を体積列へ移す手順（本番 4×4 は動かさない）
7. 生成器の policy 解決と既存収集のキーを identity 文字列へ移す範囲（§34。現行無修飾でもフォルダ名照合に寄せて証明する）

Environment は距離政策の候補に入れない（距離の単位は Cell 作業単位）。子は親 Stable 後の明示 Add のまま。

---

## 4. M-1 の受入（現行 4×4 で証明する）

本番セルは動かさない。名前は今の `Cell_0_0` のままでよい。
既存 16 枚の全廃は [SEASON_WORLD_DESIGN.md](SEASON_WORLD_DESIGN.md) の S-4。

1. Controller の Tick が `CellIdentity.Format` を呼ばない
2. desired / retain は候補の体積と LoadRadius / UnloadRadius で、現行 4×4 と同等の集合になる（距離は体積中心の XZ）
3. 既存 WSC 10 本相当 / MultiFocus / 統合が緑（入力の与え方が座標列から体積列へ変わる）
4. R-3: `SwitchScene("Cell_0_0")` は今どおり失敗する。検出が名前文法以外になってもよい（M-3 まで名前文法のままでも M-1 は通せる）
5. FW に季節語が無い（W-1）
6. 生成器が既存収集で座標キーに潰さない（現行無修飾でも、フォルダ名を identity として照合する）— M-2 と同時ならここで見る

これが通るまで S-4 に入らない。

---

## 5. やらないこと

- §34 の契約を現行レイアウトの語彙で書き直すこと
- 9×6 の焼き込み、Season_* ノード、トンネル、既存 16 セルの全廃
- `GameSceneFactory` / `CellScene` ctor / `TryFromCellId` の修飾対応（S-4）
- グラフメトリック / ノベル / HLOD
- §21 / §33 / §5 の本文全面改稿（§33 の退役表と §21 の現状バナーは公開面で済み）
- Unity.exe 起動、テスト全件実行（実装者は走らせない）
- `record` の使用（`IsExternalInit` が無い）

Editor 操作境界は他スライスと同じ。人間が開いた Editor への `unity status` / `unity command` / `unity eval` のみ可。YAML 手編集禁止。偽 null 禁止。テストで `Task.Delay` / `Thread.Sleep` 禁止。

---

## 6. 旧稿からの吸収

`SCENE_WORLD_BOUNDS.md` の契約本文は §34 へ移した。分解問と 4×4 証明は本書へ移した。
`SEASON_LEVELS_IMPLEMENTATION.md` の S-3 実測は `STREAMING_CURRENT_SPEC.md` へ移した。
どちらも git 履歴に残る。本文は復活させない。
