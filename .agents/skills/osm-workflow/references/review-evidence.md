# レビュー証拠と指摘記録

## 対象を固定する

Phase C と C' は同じ実装を評価する。レビュー開始時に次を固定する。

- implementation base commit SHA
- implementation head commit SHA
- implementation base から head までの完全 diff、stat、name-status
- 実行した機械検査、そのコマンド、結果。未実行ならその旨
- 実行した場合は、その head に対するテストの生ログと XML、コマンド、filter、対象集合
- evidence bundle の生成時刻と識別子または hash

テスト未実行でも、固定した diff と機械検査だけで欠陥発見レビューを開始してよい。その bundle では GO 判定も C' も開始しない。最終判定と C' に使う bundle には、判定必須テストの生ログと XML を含める。

implementation head はレビュー対象の実装差分を固定する値であり、HANDOFF に C/C' の結果だけを追記した review-record commit とは分ける。staged diff や可変な作業ツリーだけを正本にしない。差し戻し等で実装対象の diff が変わった場合は新しい implementation head で bundle を作り直し、古い結果を新 head の合格証拠に混ぜない。

Phase A snapshot、Phase B result snapshot、evidence bundle、C' blind audit bundle は、それぞれ path / id、生成時刻、hash を manifest に記録する。判定 C と C' の入力 bundle は、どちらかのレビューを開始する前に同じ snapshot と判定 evidence から生成する。発見 C の所見や安い XML だけを C' に渡さない。Phase B result へ発見 C の指摘を転載しない。

## Phase C の入力

- 凍結した Phase A snapshot
- Phase B の実装結果と未実行事項
- 固定した evidence bundle

Phase C は構造、進める最低条件とその詳細である受け入れ条件、失敗経路、テスト結果を確認する。スライスの終了可否は進める最低条件で判定する。finding は次の二つに分類して HANDOFF の Phase C 欄へ書く。

- **現在の問いを阻害する欠陥:** 凍結済みの進める最低条件、受け入れ条件、または常時契約への違反を示し、違反する条件または契約を記録できるもの。
- **後続スライスの入力:** 上記の違反根拠を持たない新しい不確実性。重要度にかかわらず現スライスの実装や受け入れ条件を自動拡張しない。

A3 後に後者を現スライスへ取り込むには、人間が例外として明示承認し、置き換える既存条件または期限・検証予算を指定したうえで Phase A を新しい revision として再開する。

## Phase C' の blind audit bundle

C' へ渡す入力は判定 C の入力から生成するが、次を含めない。

- 発見 C と判定 C の結論、指摘、要約
- root や他モデルが挙げた疑念候補
- facade、旧 identifier、factory test gap のような探索先の例示
- Phase C 後に追加された誘導的な説明
- Phase B result へ転載した発見 C の所見

C' は凍結した Phase A snapshot、所見を含まない Phase B の実装結果、判定 C と同じ evidence bundle だけから独立に findings を出す。同じテストの再実行は必須にしない。監査完了後に人間または Phase D 担当が判定 C と C' を初めて突き合わせる。

差し戻し等でレビュー対象の implementation head または対象 diff が変わった場合は、その対象に対する旧判定 C / C' 結果を無効とする。修正とレビューへ戻り、次の GO 候補が固まってから判定必須テストと C/C' をやり直す。修正 commit ごとに即座に全件実行しない。HANDOFF へのレビュー記録だけを変更した場合は evidence 対象を更新しない。発見 C の差し戻しだけでは C' を起動しない。

## 機械検査と意味レビュー

決定的な検査は1回だけ実行し、モデルごとに再探索させない。ただし単純 grep が意味判定を必要とする場合、その結果は failure ではなく review flag とする。

- 明確な hard gate の例: 編集した Unity `.cs` の `#nullable enable`、Unity 側の `record`、テスト内の `Task.Delay` / `Thread.Sleep`
- 意味判定が必要な flag の例: 破棄されうる `UnityEngine.Object` に対する偽 null パターン、公開 API に露出したログ実装型

GUID、asmdef、保護 YAML などの検査は、変更種類と過去の実害に応じて実行する。全スライスへ無条件に増やさない。

## 発見 C と判定 C

Phase C は欠陥発見と最終判定を分ける。検証範囲の縮小ではなく、重い検証を実施するタイミングをずらす。BS3 の実績は、全 EditMode を毎回走らせた記録でも、毎回 8 filter の記録でもない。関連 filter と重い統合検証を、複数の Unity 起動で繰り返していた。

- **発見 C:** 固定した実装差分の構造・契約・失敗経路を先行し、確認可能な指摘を凍結済み範囲でまとめる。テスト未実行の証拠で開始してよいが、GO 判定は行わない。差し戻し中のテストは、修正と影響範囲の確認に必要なものを `-Filter` 等で選び、対象・理由・結果・未確認範囲を記録する。Phase A の起点 filter を使うが、凍結済み条件または常時契約の確認に必要なら根拠付きで変更してよい。受け入れ条件の追加とは区別する。PlayMode 統合や Player 検証も、当該欠陥の再現・修正確認に必要な場合は理由と範囲を記録して限定実行できる。通常は最終判定時にまとめる。
- **判定 C:** 未解決の blocker がなくなった GO 候補 head で判定必須テストを実行する。実装変更を伴うスライスでは最終の全 EditMode 回帰を標準とする。適用除外は Phase A で理由と代替証拠を明示する。同じ platform / graphics 条件でまとめられるテストは 1 プロセスに集約する。プロセス分離自体が検証条件なら維持する。XML の実行テスト名と件数で、必要な集合が収録されたことを確認する。namespace filter は全件性を保証しない。
- **C':** 最終判定に用いる snapshot、完全 diff、生結果、機械検査を、判定 C と同じ head で評価する。発見 C と判定 C の所見・誘導情報は渡さない。同じテストの再実行は必須にしない。
- **判定後の head 変更:** 実装対象が変わった場合、旧結果を新 head の合格証拠に流用しない。修正とレビューが収束した新 head で判定必須テストと C/C' をやり直す。修正 commit ごとに即座に全件実行しない。レビュー結果の追記だけの commit は再実行条件にしない。

BS4 にも同じ順序を適用する。Player の target、stripping、IL2CPP/AOT の必須範囲は Phase A で固定し、最終候補で bootstrap → 登録 → 論理初回 Scene → 代表 content → 終了を確認する。検証した Player / content と実装 head の対応を記録する。Editor 成功を Player 成功の代用にせず、必須 Player 検証未実行を GO にしない。

使ってはいけない implicit な読み替え:

- 狭い filter の成功を全体成功にする。
- 最後の小修正を古い XML で通す。
- 発見 C の指摘を C' に渡す。Phase B result への転載も含む。
- Player 固有の問題を fake だけで直ったとする。
- 構造レビューを飛ばして先に長い Unity を起動する。
- 差し戻しが決まったあとに最終判定用の検証一式を実行する。

## findings ledger

レビュー価値を時間や token だけで評価しない。各指摘に次を記録する。

- severity
- category: machine / obvious / semantic
- unique または他レビューとの duplicate
- accepted / rejected / false positive
- 発見した Phase、担当、モデル
- 根拠となるファイル・行・契約
- bucket: 現在の問いを阻害する欠陥 / 後続スライスの入力。前者は違反する凍結済み条件または常時契約
- 修正 commit、または不採用理由

外部レビューは一つの依頼へ論点を詰め込まず、観点を限定して findings-first で出力させる。モデル可用性や quota は重い本依頼の前に小さい probe で確認し、空振りを同じ重さで反復しない。
