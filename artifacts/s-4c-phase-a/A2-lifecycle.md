# S-4c A2 — 寿命 / lease / bake

- 入力: A0 + A1（他レビュー未読）
- 担当: 独立エージェント（同一セッション・Grok 系 inherit）
- Verdict: **このまま凍結は不可**（blocker 1 件を直してから凍結可）

## Findings

| id | 重大度 | 要旨 |
|---|---|---|
| S4C-A2-01 | blocker | `OnLoadedImpl` 例外は `OnPreUnLoadedImpl` を通らない。Acquire 後は本物の try/catch で Dispose |
| S4C-A2-02 | should-fix | Environment.Dispose は Restore 1 回 + generation 進め。active lease の後続 Dispose は no-op。テスト必須 |
| S4C-A2-03 | later-slice | 直列切替の baseline フラッシュは許容。S-5 へ |
| S4C-A2-04 | later-slice | `Light?` が `?.` を誘う。コメントで禁ずる |
| S4C-A2-05 | should-fix | stale の Arrange を 4 手で固定。旧 Apply 後の新 owner、BindSun 二度目、Environment.Dispose |
| S4C-A2-06 | should-fix | Cell Lighting の Directional 0 を判定 C の EditMode で数える。Factory で `Spring_Lighting_4_2` が companion |
| S4C-A2-07 | should-fix | bake は Single で 7 Scene だけ。Whitebox / Workspace 4 Scene で焼かない。commit ファイル列挙 |
| S4C-A2-08 | should-fix | 条件 7 は「Cell unload 中も fog/sun は残る。戻して baked 床が載る」 |
| S4C-A2-09 | 確認 | BindSun は lease が正しい。FogMode を state に入れないのも妥当 |
| S4C-A2-10 | 02 に合流 | quit 時の強制 Restore は必要。世代契約が足りない |
