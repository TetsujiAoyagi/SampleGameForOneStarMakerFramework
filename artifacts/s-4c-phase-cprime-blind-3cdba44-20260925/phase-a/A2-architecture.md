# S-4c A2 — アーキテクチャゲート

- 入力: A0 packet + A1 snapshot（他レビュー未読）
- 担当: 独立エージェント（同一セッション・Grok 系 inherit）
- 観点: architecture-gates.md 全文
- Verdict: **approve-with-changes**

## Findings

| id | 重大度 | 要旨 | 根拠 |
|---|---|---|---|
| F-1 | blocker | `UnityRenderEnvironmentSink` を internal のまま `AppInitializer` が `new` できない | InternalsVisibleTo は Tests/Editor のみ。Camera の Backend は public |
| F-2 | should-fix | 公開集合が A0 の 3 型より広いのに freeze されていない | Tests 差し替えと Composition Root のため必要なら明示 |
| F-3 | should-fix | App Dispose が generation を進めないと生き残り lease が RenderSettings を書ける | Camera handle は owner 参照を切る |
| F-4 | should-fix | matching Dispose で bound Light を手放す | Scene 寿命の Light を App 寿命 lease が保持しうる |
| F-5 | should-fix | 問いの「共通 Volume 所有」と URP 拒否の文言を一致させる | 本スライス GO は Volume.weight 操作を含まない |
| F-6 | should-fix | preset lookup を Acquire 前へ | 未知 identity で baseline Capture しない |
| F-7 | should-fix | `ReleaseRenderEnvironment` を Camera 解放に埋め込まない | 変更理由の混在 |
| F-8 | later-slice | SeasonLightingScene の探索+lease+preset | 今は Load/Unload orchestration のみ |
| F-9 | later-slice | 太陽 Light は Restore しない | S-5 遷移の空白 |

現況主張（URP 非参照、空 scaffold、PlayerScene が Game 層の唯一の RenderSettings 直書き）はコードと一致。
