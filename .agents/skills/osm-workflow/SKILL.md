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
3. 専用ブランチで作業する。`main` / `develop` に直接コミットしない。
4. PR の base は `develop` とする。

Phase A、C、C'、HANDOFF の作成・更新では [Phase と HANDOFF](references/phases-and-handoff.md) を全文読む。ドキュメントを変更する場合は [ドキュメント方針](references/docs-policy.md) も全文読む。

## Phase A: 計画と設計レビュー

- A0 で現況、要求、制約、対象外、未決事項に加え、このスライスが答える問い、次スライスへ進む最低条件、後続スライスへ送る問いを同じ planning packet に固定する。
- A1 で主担当が進める最低条件、その詳細である受け入れ条件、責務配置、規模、テスト方針、停止規則を含む初稿を作る。
- A2 で変更規模に応じて複数モデルへ独立レビューを依頼する。各レビューは同じ入力版を使い、互いの指摘を見せない。
- 少なくとも1件は [アーキテクチャゲート](references/architecture-gates.md) に従い、フォルダ境界、クラス責務、依存、所有者、寿命、テスト可能性を専門に見る。
- 高リスク変更では、初稿を見ずに A0 だけから代替構成を出すレビューを追加する。C' の独立性が必要なら、Phase A で利用可能な全モデル系列・ベンダーを使い切らず、未関与の監査担当を予約する。
- A3 で主担当と人間が指摘を採用・不採用・保留に分類し、理由を記録して HANDOFF と受け入れ境界を凍結する。凍結後に設計判断を変える場合は Phase A を新しい revision として再開する。
- A3 後の finding が現スライスを阻害できるのは、凍結済みの進める最低条件、受け入れ条件、または常時契約への違反を示す場合だけとする。それ以外の新しい不確実性は重要度にかかわらず後続スライスへ移送する。現スライスへ例外的に取り込む場合は、人間が置き換える既存条件または期限・検証予算を明示して承認し、Phase A を新しい revision として再開する。
- HANDOFF は [テンプレート](references/handoff-template.md) を基準にする。テスト方針には発見用 `-Filter` と判定必須テストを分けて書く。分けないと Phase C は差し戻しのたびに長い Unity / Player を繰り返す。

## Phase B: 実装

- HANDOFF がある場合は本文を正とし、書かれていない設計文書を読み込んで独自に再設計しない。
- HANDOFF と他の記述が衝突したら実装を止め、判断を返す。
- 設計判断が新たに必要になった場合も実装を止め、Phase A に戻す。
- 計画外の状態、依存、所有者、寿命、公開 API が必要になった場合、または計画した配置では中核ロジックを単体テストできない場合も実装を止める。Phase B 内で便宜的な Helper / Manager へ押し込まない。
- コメントには、コードだけでは復元しにくい契約、判断理由、変更時の注意を残す。特に寿命・所有者・選択数・失敗境界や、一見省けそうな例外処理の理由を説明する。処理をそのまま言い換えるコメントや行数を満たすためのコメントは増やさない。読者に通じないスライス略号だけで説明せず、対象と理由を通常の言葉で書く。
- Unity Editor、Scene、Prefab、Addressables等を扱う場合は `../osm-unity-editor/SKILL.md` を先に読む。
- 対象を限定した Editor 操作とコンパイル確認は Phase B で行ってよい。Unity バッチテストと Addressables ビルドの実行・判定は Phase C の責任とし、Phase B では実行しない。完了時に未実行を明記する。
- 実装を終えたら `pwsh tools/contract-audit.ps1` を実行する。Editor のコンパイル確認を行えなかった場合は、その未確認を明記する。

## Phase C: レビューとテスト

- [レビュー証拠](references/review-evidence.md) に従い、implementation base / head commit を固定した evidence bundle を作る。staged 状態や可変な作業ツリーだけをレビュー対象にしない。
- HANDOFF の進める最低条件、その詳細である受け入れ条件、差分を照合し、機能レビューより先に構造レビューを行う。スライスの終了可否は進める最低条件で判定する。
- Phase A の責務マップと実際のメンバー、依存、配置、テスト境界を照合する。行数と増加率は分割命令ではなく、構造判断の説明を要求するトリガーとして扱う。
- 指摘を「凍結済み条件または常時契約への違反により現在の問いを阻害する欠陥」と「後続スライスの入力」に分け、根拠となる凍結済み条件または常時契約を記録する。レビュー担当は欠陥を指摘できるが、後者を理由に現スライスの実装や受け入れ条件を自動拡張しない。
- 変更量、独立した変更理由の混在、単体テスト可能性、Unity の偽 null を確認する。
- 重要な判断を半年後の担当者がコードと公開文書から復元できるか確認する。復元できない箇所は理由のコメントを求め、コメント量そのものは評価しない。
- `pwsh tools/contract-audit.ps1` を実行する。機械で判定できる契約はこれで済ませ、構造レビューは設計判断に集中する。
- Phase C の Unity テストは発見と判定を分ける。詳細は [レビュー証拠](references/review-evidence.md) の「発見 C と判定 C」を正とする。
- **発見 C:** 構造レビューと機械検査のあと、凍結失敗経路を一通り返して ledger に書く。最初の blocker で止めない。構造または常時契約だけで NO-GO なら Unity を起動しない。Unity が要る場合は HANDOFF が書いた安い `-Filter` だけを、可能な限り 1 プロセスで実行する。発見 C の XML は GO 証拠にしない。C' も起動しない。
- **判定 C:** GO 候補の implementation head に対し、HANDOFF が列挙した判定必須テストを実行する。空 filter の全 EditMode は毎回の必須ではない。必須集合を 1 回の `pwsh tools/run-tests.ps1` 起動にまとめられるなら分割しない。PlayMode 往復・Content Directory build・Player / IL2CPP は判定 C に属し、発見の差し戻しでは繰り返さない。
- Unity Editor が閉じていることを確認してから `pwsh tools/run-tests.ps1` を実行する。自分で起動した Editor は未保存の変更を確認して正常終了させる。既存の人間所有 Editor を無断で強制終了しない。絞り込みは `-Filter` を使う。`OneStarMaker.Tests` のような親 namespace だけを filter にすると `OneStarMaker.Tests.Editor` が静かに漏れる。
- Windows の Phase C Unity バッチテストは、**最初の実行から sandbox 外の承認済み経路**で `pwsh tools/run-tests.ps1` を起動する。sandbox 内では Unity Licensing Client の named-pipe IPC (`LicenseClient-void`) が成立せず、Unity が再接続を無期限に繰り返した実測がある。CLI/tool の `require_escalated` 等で通常の権限昇格承認を取得し、承認できない場合はテスト未実行として止める。`run-tests.ps1` 自体は昇格しない。license file の返却・削除・再発行を回避策にしない。
- 起動後にライセンス接続成功とテスト進行をログで確認する。`LicenseClient-void` 不在・`com.unity.editor.headless` 不在の再接続が続き、数分間進行せず XML もない場合は、無期限に待たず、今回起動した Unity PID だけを確認して終了し、生ログと未実行判定を残す。Editor が正常に進行中なら所要時間だけで中断しない。
- Phase C でも `unity test` / `unity run` は使わない。
- exit 0 は1件以上実行かつ failed 0。0件は失敗として扱う。
- `0xC0000005` でも結果 XML が完成していれば、ログ末尾と XML を基に判定する。
- 確認結果と未確認事項を HANDOFF に記録する。

## Phase C': 独立監査

- 人間または AI が担当する。AI は新規セッションで開始する。判定 C の結論、指摘、疑念候補を含まない blind audit bundle を読み、可変な HANDOFF 全文をそのまま入力にしない。
- 判定 evidence が揃った head でのみ開始する。発見 C の安い XML だけでは独立監査済みとしない。
- blind audit bundle は、凍結した Phase A snapshot、Phase B の実装結果、判定 C と同じ implementation base / head の完全 diff、判定必須テストの生結果、判定 C より前に生成した機械検査出力だけで構成する。
- AI 担当のモデル相違条件、人間担当の確認記録と独立性の扱いは [Phase と HANDOFF](references/phases-and-handoff.md) に従う。人間の回答前に AI が PASS や完了を記録しない。
- 受け入れ条件だけでなく、契約違反、構造劣化、未検証の失敗経路、Phase C 自体の見落としを探す。
- 指摘を「凍結済み条件または常時契約への違反により現在の問いを阻害する欠陥」と「後続スライスの入力」に分け、根拠を記録する。後者を理由に現スライスを自動拡張しない。
- 指摘、残存リスク、監査できなかった範囲、使用したモデルを HANDOFF の Phase C' 欄へ記録する。

## 完了

- マージ済みの HANDOFF は、恒久的に残すべき知見だけを公開ドキュメントへ反映して削除する。
- PRを作る場合は差分と検証結果を要約し、base が `develop` であることを確認する。
