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

Phase A snapshot、Phase B result snapshot、evidence bundle、C' blind audit bundle は、それぞれ path / id、生成時刻、hash を manifest に記録する。判定 C と C' の入力 bundle は、どちらかのレビューを開始する前に同じ snapshot と判定 evidence から生成する。発見 C だけの XML を C' に渡さない。

## Phase C の入力

- 凍結した Phase A snapshot
- Phase B の実装結果と未実行事項
- 固定した evidence bundle

Phase C は構造、進める最低条件とその詳細である受け入れ条件、失敗経路、テスト結果を確認する。スライスの終了可否は進める最低条件で判定する。finding は次の二つに分類して HANDOFF の Phase C 欄へ書く。

- **現在の問いを阻害する欠陥:** 凍結済みの進める最低条件、受け入れ条件、または常時契約への違反を示し、違反する条件または契約を記録できるもの。
- **後続スライスの入力:** 上記の違反根拠を持たない新しい不確実性。重要度にかかわらず現スライスの実装や受け入れ条件を自動拡張しない。

A3 後に後者を現スライスへ取り込むには、人間が例外として明示承認し、置き換える既存条件または期限・検証予算を指定したうえで Phase A を新しい revision として再開する。

## Phase C' の blind audit bundle

C' へ渡す入力は Phase C の入力から生成するが、次を含めない。

- Phase C の結論、指摘、要約
- root や他モデルが挙げた疑念候補
- facade、旧 identifier、factory test gap のような探索先の例示
- Phase C 後に追加された誘導的な説明

C' は凍結した Phase A snapshot、Phase B の実装結果、判定 C と同じ evidence bundle だけから独立に findings を出す。監査完了後に人間または Phase D 担当が判定 C と C' を初めて突き合わせる。

差し戻し等でレビュー対象の implementation head または対象 diff が変わった場合は、その対象に対する旧判定 C / C' 結果を無効とする。新しい snapshot と判定 evidence を作り、判定 C と C' を新しい対象へやり直す。HANDOFF へのレビュー記録だけを変更した場合は evidence 対象を更新しない。発見 C の差し戻しだけでは C' を起動しない。

## 機械検査と意味レビュー

決定的な検査は1回だけ実行し、モデルごとに再探索させない。ただし単純 grep が意味判定を必要とする場合、その結果は failure ではなく review flag とする。

- 明確な hard gate の例: 編集した Unity `.cs` の `#nullable enable`、Unity 側の `record`、テスト内の `Task.Delay` / `Thread.Sleep`
- 意味判定が必要な flag の例: 破棄されうる `UnityEngine.Object` に対する偽 null パターン、公開 API に露出したログ実装型

GUID、asmdef、保護 YAML などの検査は、変更種類と過去の実害に応じて実行する。全スライスへ無条件に増やさない。

## 発見 C と判定 C

Phase C は検出を止めない。高いのは Unity 起動と PlayMode / Player であり、差し戻しのたびにそれを繰り返さない。

- **発見 C:** 構造レビューをテストより先に行う。凍結失敗経路を一通り返し、最初の blocker で止めない。構造または常時契約だけで NO-GO なら Unity を起動しない。Unity が要る場合のコマンドは HANDOFF の発見用 `-Filter` に限り、可能な限り 1 プロセスとする。発見 C は ledger を返す。GO にも独立監査済みにもしない。
- **判定 C:** 進める最低条件の証拠になるテストだけを、GO 候補 head で実行する。Phase A が判定必須の filter / Player / 統合を列挙する。空 filter の全 EditMode はリポジトリ全体回帰が明示されたときだけであり、毎回の Phase C 必須ではない。
- **C':** 判定 evidence が揃った head でのみ開始する。発見 C の安い XML だけを盲検入力にしない。
- **判定後の head 変更:** 実装差分が変わった head では HANDOFF の判定必須テストをやり直す。旧判定 XML と旧 C/C' は無効。影響範囲を理由に必須テストを省略しない。review-record だけの commit は head を動かさない。

BS4 の Player / IL2CPP / stripping は判定必須に属する。Editor の fake や session テストは発見に使える。必須 Player 検証の未実行は GO に置き換えない。

使ってはいけない implicit な読み替え:

- 発見 C の exit 0 をスライス GO にする。
- `OneStarMaker.Tests` だけを filter にして Editor 統合を実行済みとする。
- 判定テスト後に実装を直し、旧 XML で C' する。
- 構造レビューを飛ばして先に長い Unity を起動する。

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
