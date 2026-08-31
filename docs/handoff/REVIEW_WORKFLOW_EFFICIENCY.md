# 外部モデルレビューの実行効率 — 調査記録と次回検討事項

> ステータス: **調査結果を記録済み。改善案は未承認・未適用。**
> 本書は次セッションで運用を検討するための作業台であり、現在の Phase 契約やモデル設定を変更しない。
> harvest 先: 人が採用を決めた項目だけを `.agents/skills/osm-workflow/SKILL.md` または `references/phases-and-handoff.md` へ反映する。会話の正本を GitHub Issue に移す案を採る場合は、その Issue も判断記録にする。
> 削除期限: 改善案を採用して正本へ harvest した時、または不採用を決めて記録が不要になった時。

## 1. 観測した実行

### Phase C（Cursor / Grok）

- 実行は `cursor-agent.cmd --print --mode plan --model cursor-grok-4.6-high --trust --sandbox disabled --output-format json`。
- Windows sandbox 有効の試行は約 4 秒で失敗。本実行は約 808 秒、その後の resume は約 123 秒で、合計は約 15 分 32 秒。
- `--mode plan` のため最終レビューではなく計画・途中経過で止まり、追加の resume が必要になった。
- 親は 74 tool calls。内部で 3 agent を起動し、それぞれ 72 / 65 / 12 calls。合計 223 tool calls。
- Phase A の遡及確認と Phase C を同じ依頼に入れ、型・namespace・GUID・テスト・契約・diff を広く再確認した。

### Phase C'（Codex）

- Claude Opus、Gemini Pro、Codex High を Cursor 経由で順に試し、いずれも quota で約 5 秒ずつ失敗した。その後 Codex GPT-5.5 xhigh の read-only subagent を使用した。
- 実行時間は約 8 分 23 秒、`exec_command` 91 回。
- 累積 usage は input 2,311,351（cached 2,173,568、約 94%）、output 21,672、reasoning output 7,160、total 2,333,023。
- 後半は各 turn 約 134k〜138k input の大きな文脈を繰り返し送っていた。
- それ以前の Claude CLI 試行は約 176 秒後に usage 制限で失敗し、生成結果はなかった。

数値は今回の実行ログから得た一例であり、将来の固定予算ではない。

## 2. 問題点

1. 読み取りレビューに Cursor の `plan` mode を使い、最終回答まで余分な往復が発生した。
2. 子 agent 禁止を明示しなかったため、外部レビュー自身が探索を三重に分割した。
3. Phase A の遡及確認と Phase C を混ぜ、設計・実装・決定的検査を一度に再走査した。
4. diff、GUID、asmdef、YAML、grep、テスト XML の決定的検査を root、Grok、Grok の子、C' が重複した。
5. C' に Phase C の疑念や候補（facade、旧 identifier、factory test gap、公開 API 等）を列挙し、独立監査をアンカリングした。
6. C' は大きな自動コンテキストと多数の細かな shell read を繰り返した。xhigh 単独より、文脈の再送と round trip 数の影響が大きい。
7. モデル可用性・quota を本依頼で初めて確認し、失敗する呼び出しを積み重ねた。
8. 会話だけを判断記録にすると、セッションを跨いだ前提と決定が見失われやすい。
9. モデルの呼び出し経路が利用者の意図と一致していなかった。Cursor は Grok 系、Claude CLI は Opus / Sonnet に限定する、という使い分けが必要。

## 3. 次セッションで検討する改善案

以下は候補であり、まだ運用へ反映しない。

- Cursor の read-only review は `--mode ask` を使い、subagent / delegation を明示的に禁止する。
- root が一度だけ evidence bundle を作る。内容は staged diff / stat / name-status、GUID map、asmdef・保護 YAML の差分、契約 grep、テスト XML 要約とする。
- 外部モデルは evidence bundle と変更ファイルの意味的リスク、責務、失敗経路に集中する。追加読取は変更ファイルと直接参照に限定し、まとめて読む。
- Phase A の外部確認は実装前に一度だけ行い、Phase C に遡及確認を混ぜない。
- Phase C' には HANDOFF、完全な diff、生のテスト結果だけを渡す。Phase C の結論・指摘・疑念リストは渡さず、findings-only の短い出力にする。
- reasoning/model を先に落とさず、まずコンテキスト量、重複検査、round trip を減らす。同じ性能帯で比較してから裁定する。
- 本依頼の前に最小 ping でモデルの可用性・quota を確認する。
- 会話の継続性が重要な検討は GitHub Issue を正本候補にする。採用するか、どの情報を repo 文書と Issue に分けるかは次回決める。

期待値として agent 消費を 60〜80% 減らせる可能性はあるが、保証値ではない。品質比較用に、同じ PR を現行方式と候補方式で監査し、重大指摘の再現率・所要時間・usage・tool calls を記録する。

## 4. モデル経路の利用者指定

- **Cursor Agent は Grok 系を使う時だけ使用する。**
- **Claude CLI は Opus / Sonnet を使う時に使用する。**
- Claude、Gemini、Codex を Cursor Agent 経由で呼ばない。別経路を増やす場合は先に利用者と合意する。

これは改善候補ではなく、今後の手動実行にも適用する利用者指定である。ただし今回、この指定を Skill・script・設定へ自動反映する変更は行わない。永続ルールへの置き場は次回検討する。
