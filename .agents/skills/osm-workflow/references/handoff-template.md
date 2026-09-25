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

## 2. 意思決定と受け入れ境界

- このスライスが答える問い:
- 進める最低条件:
- 受け入れ条件（進める最低条件を構成する観測可能な詳細。別の完了バーにしない）:
- ここでは答えない問いと所有する後続スライス（HANDOFF / program 名）:
- 判定定義（GO / NO-GO。スパイクの場合だけ CONDITIONAL ACCEPT も定義）:
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。スパイクは上記で定義した場合だけ CONDITIONAL ACCEPT で終了できる。
- A3 後の例外承認（人間、理由、置き換える既存条件または期限・検証予算。無ければ `なし`）:
- 本文へ転記した実装制約:
- 未決事項:

未検証事項を網羅しない。A3 後の blocker は、凍結済みの進める最低条件、受け入れ条件、または常時契約への違反を示すものに限り、違反根拠を記録する。それ以外の新しい不確実性は重要度にかかわらず所有する後続スライスへ移送し、実装を追加しない。現スライスへ例外的に取り込む場合は人間の明示承認と Phase A revision を要する。CONDITIONAL ACCEPT はスパイクで判定基準を定義した場合だけ正常な終了として扱う。

## 3. 責務マップ

ファイルごとに責務、変更理由、所有者・寿命、依存、公開面、テスト境界、配置理由、現在行数と予想増分を書く。行数警報が発火する場合は分割または非分割理由を書く。

## 4. 実装計画

- 変更対象:
- 順序:
- Phase B から Phase A へ差し戻す条件:
- 対象外を維持する方法:

## 5. テストとレビュー計画

- 単体テスト:
- 差し戻し中の起点 `-Filter`（C が根拠付きで変更可。受け入れ条件の追加ではない）:
- 判定必須テスト（GO 候補 head。実装変更スライスは最終全 EditMode 回帰が標準）:
- 全 EditMode 回帰の適用除外（理由と代替証拠。無ければ `なし`）:
- 統合・Unity テスト:
- 操作・実行時・目視条件の検証経路（条件ごとの担当、環境・初期状態、操作、観測と合否、対象版・保存証拠・C / C' への受け渡し。既存テストは参照で可）:
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点（未知経路が無い場合だけ `なし`。Phase C へ委譲する場合は「未確認。初回確認は Phase C」と理由・担当・確認地点、不成立時の対応）:
- 人間の判断が必要な条件と合意した担当・証拠の受け入れ方（観察記録の受理 / 画像の独立再評価。無ければ `なし`）:
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

- 種別: 発見 / 判定
- evidence bundle id / hash:
- 構造適合:
- 現在の問いを阻害する findings（違反する凍結済み条件 / 常時契約を併記）:
- 後続スライスへ移送する findings:
- 実行したテストコマンドと `-Filter`、対象を選んだ理由:
- テスト結果（XML 上の実行テスト名と件数）:
- 判定必須のうち未実行:
- 重い検証を発見段階で限定実行した場合の理由と範囲:
- 未確認事項:
- 担当・モデル:

## 8. Phase C'

- 担当方式: 人間 / AI
- blind audit bundle id / hash:
- 確認範囲・方法（全件機械検査 / 代表箇所の目視・操作等）:
- 判定（人間担当は本人の明示回答まで未実施）:
- 現在の問いを阻害する findings（違反する凍結済み条件 / 常時契約を併記）:
- 後続スライスへ移送する findings:
- 残存リスク:
- 監査できなかった範囲:
- 独立性:
- 発見 C / 判定 C 結論の事前閲覧・設計実装への関与:
- 担当・モデル:

## 9. Phase D

- C / C' の突合:
- マージ判断:
- harvest:
- 削除確認:
