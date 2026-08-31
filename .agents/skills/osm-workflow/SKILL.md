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

- A0 で現況、要求、制約、対象外、未決事項を同じ planning packet に固定する。
- A1 で主担当が受け入れ条件、責務配置、規模、テスト方針を含む初稿を作る。
- A2 で変更規模に応じて複数モデルへ独立レビューを依頼する。各レビューは同じ入力版を使い、互いの指摘を見せない。
- 少なくとも1件は [アーキテクチャゲート](references/architecture-gates.md) に従い、フォルダ境界、クラス責務、依存、所有者、寿命、テスト可能性を専門に見る。
- 高リスク変更では、初稿を見ずに A0 だけから代替構成を出すレビューを追加する。C' の独立性が必要なら、Phase A で利用可能な全モデル系列・ベンダーを使い切らず、未関与の監査担当を予約する。
- A3 で主担当と人間が指摘を採用・不採用・保留に分類し、理由を記録して HANDOFF を凍結する。凍結後に設計判断を変える場合は Phase A を新しい revision として再開する。
- HANDOFF は [テンプレート](references/handoff-template.md) を基準にする。

## Phase B: 実装

- HANDOFF がある場合は本文を正とし、書かれていない設計文書を読み込んで独自に再設計しない。
- HANDOFF と他の記述が衝突したら実装を止め、判断を返す。
- 設計判断が新たに必要になった場合も実装を止め、Phase A に戻す。
- 計画外の状態、依存、所有者、寿命、公開 API が必要になった場合、または計画した配置では中核ロジックを単体テストできない場合も実装を止める。Phase B 内で便宜的な Helper / Manager へ押し込まない。
- Unity Editor、Scene、Prefab、Addressables等を扱う場合は `../osm-unity-editor/SKILL.md` を先に読む。
- Unity バッチテストと Addressables ビルドは Phase C の責任とし、Phase B では実行しない。完了時に未実行を明記する。

## Phase C: レビューとテスト

- [レビュー証拠](references/review-evidence.md) に従い、base / head commit を固定した evidence bundle を作る。staged 状態や可変な作業ツリーだけをレビュー対象にしない。
- HANDOFF の受け入れ条件と差分を照合し、機能レビューより先に構造レビューを行う。
- Phase A の責務マップと実際のメンバー、依存、配置、テスト境界を照合する。行数と増加率は分割命令ではなく、構造判断の説明を要求するトリガーとして扱う。
- 変更量、独立した変更理由の混在、単体テスト可能性、Unity の偽 null を確認する。
- テストに `Task.Delay` / `Thread.Sleep` が入っていないことを確認する。
- Unity Editor が閉じていることを確認して `pwsh tools/run-tests.ps1` を実行する。絞り込みは `-Filter` を使う。
- Phase C でも `unity test` / `unity run` は使わない。
- exit 0 は1件以上実行かつ failed 0。0件は失敗として扱う。
- `0xC0000005` でも結果 XML が完成していれば、ログ末尾と XML を基に判定する。
- 確認結果と未確認事項を HANDOFF に記録する。

## Phase C': 独立監査

- 新規セッションで開始し、Phase C の結論、指摘、疑念候補を含まない blind audit bundle を読む。可変な HANDOFF 全文をそのまま入力にしない。
- blind audit bundle は、凍結した Phase A snapshot、Phase B の実装結果、Phase C と同じ base / head の完全 diff、生のテスト結果、Phase C より前に生成した機械検査出力だけで構成する。
- Phase B、Cと異なるモデルを使う条件は [Phase と HANDOFF](references/phases-and-handoff.md) に従う。
- 受け入れ条件だけでなく、契約違反、構造劣化、未検証の失敗経路、Phase C 自体の見落としを探す。
- 指摘、残存リスク、監査できなかった範囲、使用したモデルを HANDOFF の Phase C' 欄へ記録する。

## 完了

- マージ済みの HANDOFF は、恒久的に残すべき知見だけを公開ドキュメントへ反映して削除する。
- PRを作る場合は差分と検証結果を要約し、base が `develop` であることを確認する。
