---
name: osm-workflow
description: >-
  Use for planning, HANDOFF creation, implementation, code review, independent
  audit, Unity test execution, documentation changes, branches, or pull requests
  in the OneStarMaker / SampleGame repository.
---

# OSM 開発ワークフロー

`AGENTS.md` の常時契約を前提に、作業の Phase と責任を分離する。使用するモデルやサービスは固定しない。

## 作業開始時

1. 現在のブランチ、差分、対象 HANDOFF を確認する。
2. 自分が担当する Phase と成果物を特定する。
3. 専用ブランチで作業する。`main` / `develop` に直接コミットしない。A3で `external-current-v1` を採用した作業は `pwsh tools/harness.ps1 current -Task <id>` を唯一の作業入口とし、過去RESULTを探して補完しない。未移行作業は従来のHANDOFFを使う。
4. PR の base は `develop` とする。
5. 前の Phase から引き継ぐときは、起動時に渡された引き継ぐ commit の SHA と自分の worktree の HEAD、§0 の implementation head 欄を [引継ぎ票](references/phases-and-handoff.md#引継ぎ票) の手順で照合し、食い違えば引き継がずに停止する。C' は PR を読まない。
6. HANDOFF は下の「作業ごとの読み先」の HANDOFF の節から読み、引継ぎ票に「読む範囲」があればそれに従う。必要なら範囲外も読む。コードは検索で場所を絞って必要な行範囲を読み、長いコマンド出力は絞ってから見る。依拠する内容は担当が自分で読み、要約に置き換えない。この項目は各 Phase の入力の定めを変えない。C' は所定の blind 入力だけを読み、レビュー対象として固定した完全 diff と、全文読むと定めた文書は絞らない。

[Phase と HANDOFF](references/phases-and-handoff.md) は、下の「作業ごとの読み先」が指定する節を全文読み、指定外の節は依拠するときに読む。指定の無い作業で HANDOFF を作成・更新する場合は全文読む。ドキュメントを変更する場合は [ドキュメント方針](references/docs-policy.md) も全文読む。cursor-agent CLI へジョブを投げる場合は [cursor-agent](references/cursor-agent.md) を先に読む。規則を緩める・消すかを判断するときは [導入理由の実測記録](references/workflow-rationale.md) を確認する。

## 作業ごとの読み先

担当する作業の文書を、着手前に全文読む。担当しない作業の文書は読まなくてよい。

| 作業 | 読む文書 | Phase と HANDOFF の読む節 | HANDOFF の読む節 |
|---|---|---|---|
| Phase A（計画と設計レビュー） | [phase-a.md](references/phase-a.md) | 全文 | 全節 |
| Phase B（実装） | [phase-b.md](references/phase-b.md) | Phase の責任、新しい事実の分類、引継ぎ票、実行担当と判断担当、HANDOFF の必須内容 | §0〜§5。差し戻しでは §6 と、差し戻した Phase の節（§7 か §8）も |
| 動作確認（Phase B の後） | [operation-check.md](references/operation-check.md) | Phase の責任、検証経路の成立、動作確認（Phase B の後）、引継ぎ票、実行担当と判断担当、HANDOFF の必須内容 | §0、§5、§6 |
| Phase C（発見 C・判定 C） | [phase-c.md](references/phase-c.md) | 「Phase A の内部フロー」「PR 状態ブロック」「Cloud の担当」以外の全節 | §0〜§8 |
| Phase C'（独立監査） | [phase-c-prime.md](references/phase-c-prime.md) | Phase の責任、動作確認（Phase B の後）、意思決定境界とスコープ停止、モデル選定と独立性、引継ぎ票、HANDOFF の必須内容、構造レビュー、レビュー証拠と C' の盲検性 | なし（所定の blind 入力だけ） |
| commit の整理、tag、push、PR の作成・更新 | [publish-and-history.md](references/publish-and-history.md) | 指定なし | §0 |

節を指定した行では、`##` の節は配下の `###` を含み、PR が存在する間は「PR 状態ブロック」、Cloud の担当が割り当てられているなら「Cloud の担当」も読む。「モデル選定と独立性」の強化条件を見るときは、「Phase A の内部フロー」の高リスクの例の段落も読む。HANDOFF の節は [テンプレート](references/handoff-template.md) の番号で、`##` の節は配下の `###` を含む。「全節」以外の行は §5.1（A2 の独立レビューと A3 の採否）を含まない。

## Phase D: 保存・引渡しと後始末

- [保存・引渡し確認](references/review-evidence.md#phase-d-の保存引渡し確認) に従い、必要なEvidenceの保存と次の担当への取得案内を確認してから、harvest・HANDOFF削除・通常のtask終了・依頼範囲の後始末へ進む。tracked HANDOFF / H1 CURRENTのどちらでもこの確認を省略しない。
- 保存済みの同一bundleは確認結果を再利用する。元証拠をworktreeに保全しただけ、またはtaskがcompletedで配送待ち0というだけでは、保存・引渡しや後始末の完了としない。未完了事項は実装・マージの状態と分けて報告する。

## task の開始・再開・終了

- 通常 task の開始は pwsh tools/workflow-task.ps1 start -Task <id>、活動状態と配送待ちの確認は status を使う。別 worktree でも同じ taskId を使い、終了済み task の保存・利用前には resume -ExpectedVersion <statusの版> -Decision <信頼済み再開record-id> を完了する。
- 人間の通常の task 全体完了／打切り指示を処理する最終手順で、完了 receipt と同じ Decision から end -Reason completed|cancelled -ExpectedVersion <statusの版> -Decision <信頼済み指示record-id> を呼ぶ。Phase D の完了処理もこれを使い、Evidence 専用承認を作らない。
- Agent 停止、pause、timeout、失敗、C/C′ 合格、PR 作成、merge 検知、Harness close を終了に推測変換しない。status の pending-finalization は同じ Decision／内容で再実行して回復する。recorded/delivery-pending は event 記録済みであり、決定 ID を変えて再終了しない。時刻／version を再試行時刻へ置き換えない。
- task の終了意味・store・配送の現況は [Workflow README](../../../tools/Workflow/README.md) に従う。
## 完了

- マージ済みの HANDOFF は、上記の保存・引渡し確認後に、恒久的に残すべき知見だけを公開ドキュメントへ反映して削除する。
- PRを作る場合は差分と検証結果を要約し、base が `develop` であることを確認する。タイトルには Phase を入れず、本文先頭の状態ブロックを各 Phase の出口と implementation head の固定時に更新する（[PR 状態ブロック](references/phases-and-handoff.md#pr-状態ブロック)）。マージ前に状態ブロックと本文の要約が最終 head と一致することを確認する。
