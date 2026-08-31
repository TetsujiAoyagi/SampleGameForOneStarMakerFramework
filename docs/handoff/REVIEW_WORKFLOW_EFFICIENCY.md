# 外部モデルレビューの実行効率 — 調査記録と次回検討事項

> ステータス: **2026-09-01 に採否を決定し、普遍契約を Skill へ harvest 済み。実行方式の比較だけ未完。**
> 本書は次の Phase C で残る仮説を検証するための一時記録であり、現在の Phase 契約の正本ではない。
> harvest 先: 普遍契約は `.agents/skills/osm-workflow/`、現在のモデル・CLI割当は git 管理外の `docs/agents/`。
> 削除期限: 次のスライスの Phase C で `stream-json` と findings 分類を実測し、採否を記録した時点。独立評価とともに削除する。
> ⚠ **§2 の診断のうち問題1・問題2は既存の実測記録と衝突している。原因分析は `REVIEW_WORKFLOW_EFFICIENCY_ASSESSMENT.md` を正とする。**

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

## 1.5 2026-09-01 の採否

採用して Skill へ harvest:

- Phase A は A0 入力固定、A1 初稿、A2 複数モデル独立レビュー、A3 人間統合と凍結に分ける。
- Phase A の少なくとも1レビューは、責務、依存、所有者、寿命、フォルダ、テスト境界を専門に見る。高リスクでは初稿を見ない代替構成も検討する。
- 行数、責務数、増加率は自動分割条件ではなく、分割または非分割の説明を要求するトリガーとする。
- Phase C と C' は base / head commit を固定した同じ evidence bundle を使う。
- C' には C の所見を含まない blind audit bundle だけを渡し、Phase D で初めて突き合わせる。
- 時間、token、tool calls だけでなく、unique finding、重複、誤検知、重大度を記録する。

ローカル実行プロファイルへ harvest:

- Cursor Agent は Grok 系だけに使い、Claude CLI は Opus / Sonnet に使う。
- 高リスク変更では Claude を Phase A〜C に参加させず、C' の未関与ベンダーとして予約する。

不採用:

- `--mode ask` への即時変更
- subagent / delegation の全面禁止
- 60〜80% 削減を目標値にすること
- GitHub Issue をレビュー判断の正本にすること
- 変更種類と実害を分類せず、全検査を先にスクリプト化すること

未決・次回実測:

- mode を維持したまま `--output-format stream-json` だけを変える比較
- findings 分類に基づく機械検査の最小範囲

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

## 3. 元の改善候補

以下は調査時点の候補である。現在の採否は §1.5 を正とする。

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

これは普遍的な Phase 契約ではなく、現在のローカル実行プロファイルである。2026-09-01 に `docs/agents/workflow.md` へ統合した。Skill 側はモデル名を固定せず、役割と独立性だけを持つ。
