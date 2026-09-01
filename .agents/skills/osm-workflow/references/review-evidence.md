# レビュー証拠と指摘記録

## 対象を固定する

Phase C と C' は同じ実装を評価する。レビュー開始時に次を固定する。

- implementation base commit SHA
- implementation head commit SHA
- implementation base から head までの完全 diff、stat、name-status
- implementation head に対して実行したテストの生ログと XML
- 実行した機械検査、そのコマンド、結果
- evidence bundle の生成時刻と識別子または hash

implementation head はレビュー対象の実装差分を固定する値であり、HANDOFF に C/C' の結果だけを追記した review-record commit とは分ける。staged diff や可変な作業ツリーだけを正本にしない。差し戻し等で実装対象の diff が変わった場合は新しい implementation head で bundle を作り直し、古い結果と混ぜない。

Phase A snapshot、Phase B result snapshot、evidence bundle、C' blind audit bundle は、それぞれ path / id、生成時刻、hash を manifest に記録する。C と C' の入力 bundle は、どちらかのレビューを開始する前に同じ snapshot と evidence から生成する。

## Phase C の入力

- 凍結した Phase A snapshot
- Phase B の実装結果と未実行事項
- 固定した evidence bundle

Phase C は構造、受け入れ条件、失敗経路、テスト結果を確認し、指摘を HANDOFF の Phase C 欄へ書く。

## Phase C' の blind audit bundle

C' へ渡す入力は Phase C の入力から生成するが、次を含めない。

- Phase C の結論、指摘、要約
- root や他モデルが挙げた疑念候補
- facade、旧 identifier、factory test gap のような探索先の例示
- Phase C 後に追加された誘導的な説明

C' は凍結した Phase A snapshot、Phase B の実装結果、Phase C と同じ evidence bundle だけから独立に findings を出す。監査完了後に人間または Phase D 担当が C と C' を初めて突き合わせる。

差し戻し等でレビュー対象の implementation head または対象 diff が変わった場合は、その対象に対する旧 C/C' 結果を無効とする。新しい snapshot と bundle を作り、両レビューを新しい対象へやり直す。HANDOFF へのレビュー記録だけを変更した場合は evidence 対象を更新しない。

## 機械検査と意味レビュー

決定的な検査は1回だけ実行し、モデルごとに再探索させない。ただし単純 grep が意味判定を必要とする場合、その結果は failure ではなく review flag とする。

- 明確な hard gate の例: 編集した Unity `.cs` の `#nullable enable`、Unity 側の `record`、テスト内の `Task.Delay` / `Thread.Sleep`
- 意味判定が必要な flag の例: 破棄されうる `UnityEngine.Object` に対する偽 null パターン、公開 API に露出したログ実装型

GUID、asmdef、保護 YAML などの検査は、変更種類と過去の実害に応じて実行する。全スライスへ無条件に増やさない。

## findings ledger

レビュー価値を時間や token だけで評価しない。各指摘に次を記録する。

- severity
- category: machine / obvious / semantic
- unique または他レビューとの duplicate
- accepted / rejected / false positive
- 発見した Phase、担当、モデル
- 根拠となるファイル・行・契約
- 修正 commit、または不採用理由

外部レビューは一つの依頼へ論点を詰め込まず、観点を限定して findings-first で出力させる。モデル可用性や quota は重い本依頼の前に小さい probe で確認し、空振りを同じ重さで反復しない。
