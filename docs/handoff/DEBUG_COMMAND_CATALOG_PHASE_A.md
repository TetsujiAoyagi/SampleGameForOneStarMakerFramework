# DEBUG_COMMAND_CATALOG Phase A snapshot

type: slice

このファイルは凍結時点の計画である。実装結果とレビュー結果は載せない。

## 0. メタデータ

- type: slice
- status: A
- branch: cursor/debug-command-catalog-159b
- implementation base commit: 2c29c99806788406551affba6cc795e67e614748
- implementation head commit: 未実施
- risk: normal
- owner: 実装担当
- created: 2026-10-04
- expires: 未設定
- harvest to: なし
- Phase A snapshot path / id: docs/handoff/DEBUG_COMMAND_CATALOG_PHASE_A.md
- Phase A snapshot generated at: 2026-10-04
- Phase A snapshot hash: ライブの HANDOFF に記録する
- Phase B result snapshot path / id: 未実施
- Phase B result snapshot generated at: 未実施
- Phase B result snapshot hash: 未実施
- evidence bundle path / id: 未実施
- evidence bundle generated at: 未実施
- evidence bundle hash: 未実施
- C' blind bundle path / id: 未実施
- C' blind bundle generated at: 未実施
- C' blind bundle hash: 未実施

A2 と A3 はこのセッションでは行っていない。凍結の根拠は、デバッグコマンドを外部ツールの受信だけに閉じず、別経路からも同じ処理を実行できる土台が要る、という依頼である。

## 1. 目的と対象外

- 目的: 名前で登録したデバッグコマンドを、ソケットの受信 dispatcher からも、プロセス内からも、同じ目録で実行できる土台を置く。
- 対象外: ワイヤ契約の変更、新しいコマンドの業務処理、スクリプト機械への命令追加、外部ツールの画面と CLI、SampleGame への配線、起動時の既定 dispatcher の差し替え、実ソケットの疎通。
- 現況: 受信封筒と `IDebugCommandDispatcher` はある。未 override のアプリは未登録を返すだけである。コマンドの目録は無い。スクリプトの内側機械は別ドラフトで、宿主呼び出しは後続である。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: デバッグコマンドの実行を、受信経路に依存しない目録へ切り出せるか。
- 進める最低条件: 登録した名前だけが実行され、未登録は失敗結果になる。ソケット用 dispatcher はその結果を既存の結果封筒へ写す。目録はスクリプト機械も外部ツールの実装も参照しない。
- 受け入れ条件:
  - 名前は大文字小文字を区別する。空の名前と重複登録は登録時に拒否する。
  - 未登録と空の名前は実行しない。結果メッセージは固定で、実行のたびに新しい文言を作らない。
  - 登録済みコマンドの 2000 回実行で追加割り当てが 0 である。
  - dispatcher は RequestId、成否、メッセージ、payload を結果封筒へ写す。キャンセル済みならコマンドを呼ばない。
  - 既定の `CreateDebugCommandDispatcher` は変えない。アプリが目録 dispatcher を返したときだけ受信が目録へ届く。
- ここでは答えない問いと所有する後続スライス: スクリプトからの宿主呼び出し、起動時にどのコマンドを登録するか、外部ツールの送信画面、コマンド payload の型付き解釈。
- 判定定義: Unity 未実行のまま GO と書かない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 本文へ転記した実装制約: asmdef は増やさない。ワイヤの YAML と生成 DTO は変えない。record は使わない。新規ファイルの先頭は #nullable enable。スクリプト機械のファイルと外部ツールの比較 CLI には手を入れない。
- 未決事項: なし

## 3. 責務マップ

- DebugCommandResult: 成否、メッセージ、payload。経路を持たない。
- DebugCommandCatalog: 名前から処理への目録。登録は初期化、実行は辞書の参照。
- CatalogDebugCommandDispatcher: 受信封筒と目録の間。ソケットサービスそのものは変えない。
- 試験: オフラインで目録。EditMode で封筒への転写。

## 4. 実装計画

- 変更対象: Runtime の DebugCommands、DebugSocketServices の dispatcher 1 ファイル、EditMode 試験、オフライン試験、この計画とライブ HANDOFF。
- 順序: 結果、目録、dispatcher、試験。
- Phase B から Phase A へ差し戻す条件: ワイヤ項目の追加、スクリプト命令の追加、既定 dispatcher の差し替えが必要になったとき。
- 対象外を維持する方法: それらのファイルを変更しない。

## 5. テストとレビュー計画

- 単体テスト: オフライン実行ファイル。EditMode は目録と dispatcher。
- 差し戻し中の起点 -Filter: OneStarMaker.Tests.DebugCommands
- 判定必須テスト: 最終の全 EditMode 回帰。加えてオフライン実行ファイル。
- 全 EditMode 回帰の適用除外: なし
- 統合・Unity テスト: 実ソケットの往復は判定必須に入れない。
- 操作・実行時・目視条件の検証経路: なし
- 未知の操作経路の疎通結果: なし
- 人間の判断が必要な条件: なし
- 機械検査: pwsh tools/contract-audit.ps1 と pwsh tools/docs-audit.ps1
- A0/A1 主担当・モデル・ベンダー: この実装セッション。Grok。
- A2 独立レビューごとの観点・担当・モデル・ベンダー: 未実施
- A3 統合担当・モデル・採否: 未実施
- C' 用に予約した担当・モデル・ベンダー: 未予約
- 独立性の強化条件を満たせない場合の理由: A2 と C' をこのセッションでは開いていない。

## 6. Phase B 実装結果

- 実装: 未実施
- HANDOFF との差: 未実施
- 未実行: 未実施
- implementation head commit: 未実施
- Phase B 担当・モデル・ベンダー: 未実施

## 7. Phase C

- 種別: 未実施
- evidence bundle id / hash: 未実施
- 構造適合: 未実施
- 現在の問いを阻害する findings: 未実施
- 後続スライスへ移送する findings: 未実施
- 実行したテストコマンドと -Filter: 未実施
- テスト結果: 未実施
- 判定必須のうち未実行: 未実施
- 重い検証を発見段階で限定実行した場合の理由と範囲: 未実施
- 未確認事項: 未実施
- 担当・モデル: 未実施

## 8. Phase C'

- 担当方式: 未実施
- blind audit bundle id / hash: 未実施
- 確認範囲・方法: 未実施
- 判定: 未実施
- 現在の問いを阻害する findings: 未実施
- 後続スライスへ移送する findings: 未実施
- 残存リスク: 未実施
- 監査できなかった範囲: 未実施
- 独立性: 未実施
- 発見 C / 判定 C 結論の事前閲覧・設計実装への関与: 未実施
- 担当・モデル: 未実施

## 9. Phase D

- C / C' の突合: 未実施
- マージ判断: 未実施
- harvest: 未実施
- 削除確認: 未実施
