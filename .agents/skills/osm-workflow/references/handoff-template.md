# HANDOFF テンプレート

スライス用 HANDOFF はこの構造を基準にする。複数スライス計画や調査記録は `type` を変え、Phase 欄を持つスライス HANDOFF と混同しない。

## 0. メタデータ

- type: `slice` / `program` / `research`
- status: `A` / `B` / `C` / `C'` / `D`
- branch:
- implementation base commit:
- implementation head commit:
- risk: `low` / `normal` / `high`
- owner:
- created:
- expires:
- harvest to:
- Phase A snapshot path / id:
- Phase A snapshot generated at:
- Phase A snapshot hash:
- Phase B result snapshot path / id:
- Phase B result snapshot generated at:
- Phase B result snapshot hash:
- evidence bundle path / id:
- evidence bundle generated at:
- evidence bundle hash:
- C' blind bundle path / id:
- C' blind bundle generated at:
- C' blind bundle hash:

Phase A では implementation base commit と Phase A snapshot を記録する。implementation head、Phase B result、evidence、C' blind bundle は各成果物が生成された Phase で追記し、未到達 Phase の値を推測して埋めない。HANDOFF へのレビュー記録だけの commit は implementation head に含めない。

## 1. 目的と対象外

- 目的:
- 対象外:
- 現況:

## 2. 受け入れ条件と制約

- 受け入れ条件:
- 本文へ転記した実装制約:
- 未決事項:

## 3. 責務マップ

ファイルごとに責務、変更理由、所有者・寿命、依存、公開面、テスト境界、配置理由、現在行数と予想増分を書く。行数警報が発火する場合は分割または非分割理由を書く。

## 4. 実装計画

- 変更対象:
- 順序:
- Phase B の停止条件:
- 対象外を維持する方法:

## 5. テストとレビュー計画

- 単体テスト:
- 統合・Unity テスト:
- 機械検査:
- A0/A1 主担当・モデル・ベンダー:
- A2 独立レビューごとの観点・担当・モデル・ベンダー:
- A3 統合担当・モデル・採否:
- C' 用に予約した担当・モデル・ベンダー:
- 独立性の強化条件を満たせない場合の理由:

## 6. Phase B 実装結果

- 実装:
- HANDOFF との差:
- 未実行:
- implementation head commit:
- Phase B 担当・モデル・ベンダー:

## 7. Phase C

- evidence bundle id / hash:
- 構造適合:
- findings:
- テスト結果:
- 未確認事項:
- 担当・モデル:

## 8. Phase C'

- blind audit bundle id / hash:
- findings:
- 残存リスク:
- 監査できなかった範囲:
- 独立性:
- 担当・モデル:

## 9. Phase D

- C / C' の突合:
- マージ判断:
- harvest:
- 削除確認:
