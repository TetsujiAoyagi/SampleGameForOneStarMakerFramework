# OSM pstack 補助 Skill 導入

## 0. メタデータ

- type: `slice`
- status: `C`
- branch: `codex/osm-pstack-skills`
- implementation base commit: `c94d084`
- implementation head commit: `4e6ba86b55362052eb53a66e9322a4d3ad8a4f47`
- risk: `normal`
- owner: OSM maintainer
- created: `2026-09-16`
- expires: Phase D
- harvest to: `.agents/skills/osm-pstack/` と第三者表示へ必要事項を反映後、この HANDOFF を削除する
- Phase A snapshot path / id: この文書の Phase A 部分
- Phase A snapshot generated at: `2026-09-16T00:00:00+09:00`
- Phase A snapshot hash: `4e6ba86b55362052eb53a66e9322a4d3ad8a4f47` 内の本節
- Phase B result snapshot path / id: 本文 §6
- Phase B result snapshot generated at: `2026-09-16T07:24:44+09:00`
- Phase B result snapshot hash: `4e6ba86b55362052eb53a66e9322a4d3ad8a4f47`
- evidence bundle path / id: `c94d084..4e6ba86` の完全 diff、stat、name-status と本節の検査結果
- evidence bundle generated at: `2026-09-16T07:24:44+09:00`
- evidence bundle hash: `8d2510dcd6f56fde64a3c61883cf1de83c9f4d10`
- C' blind bundle path / id:
- C' blind bundle generated at:
- C' blind bundle hash:

## 1. 目的と対象外

- 目的: pstack の調査、設計補助、レビュー、実行証拠、文章品質の playbook を、特定 Agent の API やモデルへ依存しない任意の補助 Skill として OSM に導入する。
- 対象外: pstack plugin 全体、`poteto-mode`、sticky mode、モデル設定、固定 subagent、自動 PR、自律実行、OSM Phase と HANDOFF の再定義、Unity の操作・テスト契約の変更。
- 現況: `.agents/skills/` には `osm-workflow`、`osm-unity-editor`、`unity-cli` がある。pstack の一次資料は `cursor/plugins` commit `c1c0a32802223f4be824112dd83d33ad29a8b26c`。MIT License、Copyright (c) 2026 Lauren Tan。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: pstack の有用な playbook を OSM 契約の下で選択的に使え、特定 Agent に依存せず、ディレクトリ単位で除去できる Skill 群として導入できるか。
- 進める最低条件:
  - `.agents/skills/osm-pstack/` だけで補助機能と pstack 由来ライセンスを所有する。
  - OSM の優先順位と Phase B の実行禁止事項を本文に明記する。
  - 中核手順から Agent API、固定モデル、固定ベンダー、IDE mode、PR／automation API を除く。
  - 調査、設計選択、レビュー、runtime／trace 診断、文章品質、判断ログの第一候補を覆う。
  - pstack の MIT 表示と由来を保持する。
  - 指定監査と Skill validator が成功する。
- 受け入れ条件:
  - 入口 `SKILL.md` の trigger は明示利用または対象 playbook の具体的要求に限定され、通常の実装や文章作成へ常時発火しない。
  - `AGENTS.md > osm-workflow / osm-unity-editor > 凍結済み HANDOFF > osm-pstack` の優先順位が書かれている。
  - `how`、`why`、`blast-radius`、`architect`、`arena`、`interrogate`、`runtime-forensics`、`trace-forensics`、`unslop`、`technical-writing`、`show-me-your-work` の能力を一つの入口から必要時だけ読む reference に再構成する。
  - `architect`、`interrogate`、`show-me-your-work` は Phase A2、C、C'、evidence bundle を置換しない。
  - `LICENSE.pstack` に MIT 原文、`THIRD_PARTY_NOTICES.md` に参照 URL、著作権者、取得 commit、再構成範囲がある。
  - すべての相対リンクが存在し、Agent 固有語彙の禁止リストに違反しない。
  - `pwsh tools/docs-audit.ps1`、`pwsh tools/contract-audit.ps1`、Skill validator が exit 0。
- ここでは答えない問いと所有する後続スライス:
  - 補助 Skill の実利用による改善度評価: 将来の Skill eval スライス。
  - IDE ごとの呼び出し adapter: 必要になった IDE adapter スライス。
  - OSM の既存 Phase 契約変更: `osm-workflow` 改訂スライス。
- 判定定義: 全受け入れ条件を満たせば GO。既存 Skill の意味変更、Agent 固有の中核依存、ライセンス欠落が必要なら NO-GO。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。
- A3 後の例外承認: なし。
- 本文へ転記した実装制約:
  - 既存 OSM Skill と HANDOFF を正とし、補助 Skill は選択的に使う。
  - Phase B では Unity.exe、`tools/run-tests.ps1`、Addressables build、Player build を実行しない。
  - pstack 原文を大量複製せず、OSM に必要な判断手順へ再構成する。
  - `PRE_PHASE_A_BUILDSYSTEM_REBUILD_UNITY66_V3.md` を変更、追加、削除しない。
- 未決事項: なし。

## 3. 責務マップ

- `.agents/skills/osm-pstack/SKILL.md`: 明示的な入口、優先順位、playbook routing。状態なし。`.agents/skills/` 内の既存契約にのみ依存する。予想 80 行未満。
- `.agents/skills/osm-pstack/references/investigation.md`: `how`、`why`、blast radius の証拠ベース調査。状態なし。予想 140 行未満。
- `.agents/skills/osm-pstack/references/design-and-review.md`: architect、arena、interrogate の補助観点。Phase を所有しない。予想 150 行未満。
- `.agents/skills/osm-pstack/references/forensics.md`: live runtime と既存 trace の診断。修正を行わない。予想 100 行未満。
- `.agents/skills/osm-pstack/references/writing-and-evidence.md`: unslop、technical writing、任意 decision log。evidence bundle を所有しない。予想 150 行未満。
- `.agents/skills/osm-pstack/LICENSE.pstack`: upstream MIT 原文。pstack 由来部分と同じ寿命。
- `.agents/skills/osm-pstack/THIRD_PARTY_NOTICES.md`: upstream 由来、取得 commit、再構成範囲。pstack 由来部分と同じ寿命。

全ファイルは判断用 Markdown であり、Unity、asmdef、Runtime／Editor、公開 API、状態、寿命へ変更を加えない。各 reference は独立して変わる利用場面で分割する。500 行、3責務、50% 増加の警報は発火しない見込み。

## 4. 実装計画

- 変更対象: 上記の新規ディレクトリとこの HANDOFF のみ。
- 順序: Phase A 独立レビューと A3 統合、Skill 作成、静的検査、validator、指定 audit、実装 commit、Phase C 用 evidence 固定。
- Phase B から Phase A へ差し戻す条件: 既存 Skill の意味変更、新しい常時契約、別ディレクトリへの adapter、禁止された Agent 固有依存が必要になった場合。
- 対象外を維持する方法: `.agents/skills/osm-pstack/` 外を編集せず、OSM の Phase と実行コマンドを reference 側で再定義しない。

## 5. テストとレビュー計画

- 単体テスト: なし。実行コードを追加しない。
- 統合・Unity テスト: Phase B では実行しない。Unity の挙動を変えないため Phase C でも不要と判断する。
- 機械検査: Skill validator、`rg` による禁止語とリンク確認、`pwsh tools/docs-audit.ps1`、`pwsh tools/contract-audit.ps1`。
- A0/A1 主担当・モデル・ベンダー: Codex、GPT 系、OpenAI。
- A2 独立レビューごとの観点・担当・モデル・ベンダー: Skill trigger、OSM 境界、ライセンス、除去容易性を Grok 系の独立実行主体がレビューする。
- A3 統合担当・モデル・採否: Codex、GPT 系。各 finding を本文へ採否記録する。
- C' 用に予約した担当・モデル・ベンダー: 人間。AI の C' はこのセッションでは予約しない。
- 独立性の強化条件を満たせない場合の理由: normal risk の Markdown Skill 変更であり、A2 の異系列レビューと人間 C' を使う。

### A2 findings と A3 採否

- A2 実行: `cursor-grok-4.6-high` を読み取り専用で2回起動した。どちらも exit 0 で所見本文を返さなかったため、独立レビュー未実施として扱う。
- 採用: trigger を一つの入口へ限定し、用途別 reference を必要時だけ読む構成。既存 OSM Skill を編集しない。ライセンスと notice を同じ除去単位へ置く。
- 不採用: なし。A2 は所見を返さなかったため、独立 finding は存在しない。
- 保留: Skill の実利用による行動評価。後続の Skill eval スライスが所有する。
- A3 凍結: ユーザーが提示した境界と受け入れ条件を planning packet の正本として採用する。独立レビュー不足を残存リスクとして保持し、独立レビュー済みとは記録しない。

## 6. Phase B 実装結果

- 実装: `.agents/skills/osm-pstack/` に入口、4つの用途別 reference、MIT 原文、第三者 notice を追加した。
- HANDOFF との差: なし。
- 未実行: Unity.exe、`tools/run-tests.ps1`、Addressables build、Player build。Markdown Skill のみのため実行対象外。
- implementation head commit: `4e6ba86b55362052eb53a66e9322a4d3ad8a4f47`
- Phase B 担当・モデル・ベンダー: Codex、GPT 系、OpenAI。

## 7. Phase C

- evidence bundle id / hash: `c94d084..4e6ba86` / `8d2510dcd6f56fde64a3c61883cf1de83c9f4d10`
- 構造適合: Phase A の責務マップどおり、入口、4用途の reference、ライセンス、notice に分離した。既存 Skill、Unity、asmdef、公開 API、状態、寿命、依存 edge は変更していない。
- 現在の問いを阻害する findings: なし。
- 後続スライスへ移送する findings: 実利用による trigger と行動品質の評価は将来の Skill eval スライスへ送る。
- テスト結果: Skill validator `Skill is valid!`。`docs-audit.ps1` は違反なし。`contract-audit.ps1` は違反なし。相対リンク5件は存在。禁止した Agent 固有語彙は中核手順に0件。`git diff --check` は違反なし。
- 未確認事項: Phase A2 の独立レビューは所見未返却のため未実施。Unity とゲームテストは対象外として未実行。
- 担当・モデル: Codex、GPT 系。

## 8. Phase C'

- 担当方式:
- blind audit bundle id / hash:
- 確認範囲・方法:
- 判定:
- 現在の問いを阻害する findings:
- 後続スライスへ移送する findings:
- 残存リスク:
- 監査できなかった範囲:
- 独立性:
- Phase C 結論の事前閲覧・設計実装への関与:
- 担当・モデル:

## 9. Phase D

- C / C' の突合:
- マージ判断:
- harvest:
- 削除確認:
