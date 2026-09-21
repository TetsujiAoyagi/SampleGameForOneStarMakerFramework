# S-4c A3 凍結記録

- frozen: 2026-09-21
- 正本: `docs/handoff/S-4c_WORLD_LIGHTING.md`
- A2 入力版: `artifacts/s-4c-phase-a/A1-snapshot.md`
- 人間承認: 本スライスは「A3 まで進めて人間が B をやる」依頼。A3 統合は主担当が行い、例外承認欄は `なし`

## 採用（本文へ反映済み）

1. **公開集合を Camera analog に合わせる。** `IRenderEnvironmentSink` と `UnityRenderEnvironmentSink` を public にする。`InternalsVisibleTo(DependOnAll)` は足さない。A0 の「3 型」は lease 契約の最小であり、Composition Root が `new` できない配置は常時契約違反になる。
2. **`RenderEnvironment.Dispose` は Restore 1 回 + generation 進行。** 生き残り lease の Apply は throw、Dispose は no-op。
3. **matching Dispose で bound Light を捨てる。**
4. **問いから共通 Volume の実行時所有を外す。** GO 判定に Volume.weight を使わない。
5. **Acquire 前に太陽探索と preset lookup。**
6. **`OnLoadedImpl` は Acquire 後だけ try/catch で Dispose。** PreUnLoad に頼らない。成功時は finally で Dispose しない。
7. **`ReleaseRenderEnvironment` を独立メソッドにする。**
8. **stale テストの Arrange を 4 手に固定し、Environment.Dispose / BindSun 二度目 / 新 owner 後の旧 Apply を最低ケースへ。**
9. **条件 7 を「Cell unload 中も fog/sun は残る。戻して baked 床が載る」に直す。**
10. **bake は Single で他を閉じてから 7 Scene。Whitebox と Workspace 4 Scene で焼かない。commit 対象を列挙。**
11. **Cell Lighting Directional 0 を B5 後の EditMode 1 件で数える。** `Spring_Lighting_4_2` は companion。
12. **Sink.Apply の Light は non-null。`?.` 禁止。**

## 不採用

**Play 用 `RenderEnvironmentHost`（DontDestroyOnLoad 太陽）。** A0 のみレビューの Alternative A。

理由:

- このスライスの問いの半分は multi-scene bake であり、Season Lighting の authored Directional が bake 光源になる。Play を別 Host にすると二重の太陽と「どちらが正本か」が残る。
- 空 scaffold は Phase B が `SeasonSun` を置く前提であり、欠落は fail-closed でよい。
- lease の単一 owner 試験は Fake sink で Unity Host なしに閉じる。BindSun 成功経路だけ EditMode の一時 `Light` を使う。
- 寿命レビュー（A1 を読んだ側）は BindSun を lease に置く判断を支持した。Host にすると公開 API から Light は消えるが、新しい Host 寿命が足る。
- A3 は「公開面を増やさない Host」より「bake と Play の光源一致」を現在の問いに選ぶ。

## 保留（後続スライス）

- SeasonLightingScene からの探索ヘルパー抽出（TimeOfDay / Volume bind が載ったとき）
- matching Dispose 後〜GO 破棄までの太陽値の空白、直列季節切替の baseline フラッシュ（S-5）
- URP `Volume.weight` 反映
- Camera skybox の季節連動
- Probe 方式とメモリ（S-9）
- Cell Lighting の見た目作り込み（S-4d は 2 Lighting を再作成しない）

## 凍結後に現スライスを阻害できるもの

凍結済み最低条件・受け入れ条件・常時契約への違反だけ。Host 太陽が「より Camera に似ている」は再開理由にしない。
