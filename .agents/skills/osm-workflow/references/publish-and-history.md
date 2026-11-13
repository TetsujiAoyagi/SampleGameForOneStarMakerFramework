# 公開点とGit履歴

- **push は配布であり保存ではない。** リモートにしか居ない読者が居る時だけ push する。同一 PC の担当同士の引継ぎは、ローカルの commit SHA と、必要なら `evidence stage` の取得案内で行う。
- **公開点**は (a) PR 作成、(b) レビュー依頼（`@cursoragent` のコメントを含む）と、レビュー 1 巡ごとの修正 1 commit、(c) マージ前の最終（harvest と HANDOFF 削除）とする。公開点以外で push してよい例外は、Cloud・別 host の担当が割り当てられた Phase 境界と、人間の明示指示に限る。(b) は公開 PR のレビューに限る規則で、Phase A の A2 レビューは公開点ではなく、A2 中の改訂を巡ごとに commit しない（[Phase A の commit と snapshot](phases-and-handoff.md#phase-a-の-commit-と-snapshot)）。
- 順序は「B ↔ 発見 C → 未公開の試行錯誤 commit の整理 → GO 候補 implementation head の固定 → 判定 C → C' → PR 作成・push」とする。履歴の整理は判定 C の前に済ませる。整理で head が変わったら [判定後の head 変更](review-evidence.md#判定後の-head-変更) に従う。
- 判定 evidence を取った head は、push しないローカルの軽量 tag `evidence/<taskId>/<n>` で到達可能に保ち、対象 SHA を引継ぎ票に書く。tag は旧 head の到達可能性だけを担い、整理後の別 head に旧判定を移す根拠にならない。判定 evidence を取っていない中間 head は整理してよい。
- 証拠の保存先と配布は [レビュー証拠](review-evidence.md#保存先とリモートへの配布) に従う。証拠の配布方法と、試行錯誤の実装コミットをPR履歴へ積まないことは別の条件として扱う。差し戻し中の中間コミットは判定用headの固定には使えても、そのまま全て公開する理由にはならない。
- PR公開後の修正は、レビュー1ラウンドにつき原則1つの修正コミットにまとめる。独立してレビューすべき変更を分ける場合は、その理由をPRに示す。Git配布が認められない証拠を未pushの中間コミットへ誤って入れた場合は公開前に履歴から除く。公開済みの違反は履歴の書き換えでなかったことにしない。
- 整理の対象は B 以降の実装の試行錯誤コミットに限る。Phase A の 2 点の commit（A2 入力版・A3 凍結版）は整理しない。
- push前に、前回公開headから増えるコミットを `git log --oneline <前回公開head>..HEAD` で確認する。数がレビュー上必要な単位を超えていれば、未公開分を整理してからpushする。あわせて `pwsh tools/docs-audit.ps1` を通し、公開面のリンク切れと手元層（git 管理外）への参照が無いことを確認する。機微な値の確認は次項の差分本文の点検で行う。公開済み履歴は明示的な合意なく書き換えない。既存履歴の大規模rebaseを対象外とする判断は、未公開の中間コミットを整理しない理由にならない。
- push 前に `git log --name-status <前回公開head>..HEAD` で、前回公開 head から HEAD までの全 commit で追加・変更・削除された file を確認し、`git log -p <前回公開head>..HEAD` で変更 file の差分本文を確認する。HEAD の tree だけを見る `git diff` では、途中 commit で追加して後で消した file の blob を見落とすので代用しない。判定 C 用の期待値（key 一覧・baseline）と照合 script の本体、ローカル path、秘密、reference・packageSha256 の実値が file にも本文にも含まれていれば push せず、HEAD で消すだけでなく公開予定の履歴から除くか Artifact Storage へ移す。未 push の A2 入力版・A3 凍結版の commit 自体に含まれる場合は、その commit を捨てずに対象だけを外して切り直す（レビュー入力の同一性は内容 sha256 で保つので commit hash が変わってよい。引継ぎの照合に使う SHA は tree の外で切り直し後の値に更新する）。人間が読む導出の規則と、実装から導ける恒久 fixture は対象外とする（[置き場所](review-evidence.md#判定-c-の照合-script)）。実害は commit ではなく push で起きるので、この確認を commit 時の点検で代用しない。
