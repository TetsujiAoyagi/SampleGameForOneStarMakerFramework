# S5: GameObject コマンドの明示登録

## 0. メタデータ

- type: `slice`
- status: `A`
- branch: `cursor/debug-go-app-opt-in-575e`
- implementation base commit: `d0a4b3267b57f99fc4548b3407214e295a7a5a7f`
- implementation head commit: 未実施
- risk: `normal`
- owner: 発注者（採否）。A1 文面は 2026-10-06 の実装担当。
- created: 2026-10-06
- expires: 2026-12-15、または program の置換 revision
- harvest to: 恒久契約は `unity/Assets/Docs/Architecture/12-telemetry.md` の incoming control catalog 節。プログラムは [DEBUG_GAMEOBJECT_COMMANDS.md](DEBUG_GAMEOBJECT_COMMANDS.md)。
- Phase A snapshot path / id: 未生成
- Phase A snapshot generated at: 未生成
- Phase A snapshot hash: 未生成
- Phase B result snapshot path / id: 未生成
- Phase B result snapshot generated at: 未生成
- Phase B result snapshot hash: 未生成
- evidence bundle path / id: 未生成
- evidence bundle generated at: 未生成
- evidence bundle hash: 未生成
- C' blind bundle path / id: 未生成
- C' blind bundle generated at: 未生成
- C' blind bundle hash: 未生成

A2 と A3 は行っていない。GO は未判定。プログラムは SampleGame の起動が catalog を載せるとは書いていない。このスライスも起動時の opt-in にはしない。

## 1. 目的と対象外

- 目的: 空の catalog へ既存の GameObject コマンドをまとめて登録する明示経路を置き、既定の `NullDebugCommandDispatcher` を置き換えなくても同じ catalog で list から Renderer までが解決できることを示す。
- 対象外: `CreateDebugCommandDispatcher` の既定実装、`NullDebugCommandDispatcher`、SampleGame の `AppInitializer`、DebugSocket のコンストラクタとプロトコル、UpdateSystem、sceneLoaded、ポーリング、DebugStudio の画面、毎フレーム走査。
- 現況: S1 から S4 が個別の `Register*` を持つ。既定 dispatcher は `NullDebugCommandDispatcher.Instance`。SampleGame は `CreateDebugCommandDispatcher` を override していない。公開面は、catalog が opt-in であり、SampleGame に配線されているという意味ではない、と書いている。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 明示の起動経路が、既定 dispatcher を置き換えずに GameObject コマンドを載せられるか。
- 進める最低条件:
  1. `RegisterGameObjectCommands` は、同じ world と同じ selection で、`go.list`、`go.select`、`go.set-active`、`go.get-transform`、`go.set-transform`、`go.set-renderer-enabled` だけを1つの catalog へ登録する。登録中は世界を読まない。
  2. `CreateRegistration` は新しい catalog と新しい selection を作り、その6コマンドを登録して返す。null の world は登録前に拒否する。
  3. 登録していない catalog では、その6名は未登録のままである。
  4. 登録した catalog では、選択を共有したまま list と set-active が既存の TryExecute で解決する。
  5. `AbstractApplicationInitializer.CreateDebugCommandDispatcher` は `NullDebugCommandDispatcher.Instance` を返したままである。SampleGame の `AppInitializer` と `DebugSocketService` は GameObject コマンドを登録しない。
- 受け入れ条件（進める最低条件を構成する観測可能な詳細。別の完了バーにしない）:
  1. 上記 1 から 4 を偽世界の offline TryExecute で観測する。
  2. 上記 5 は、対象ソースに既定の return が残り、GameObject 登録の呼び出しが無いことで観測する。
  3. 同じ catalog へ二度目の `RegisterGameObjectCommands` は、既存の重複登録と同じく失敗する。失敗後も既定 dispatcher は変えない。
- ここでは答えない問いと所有する後続（この program の対象外）:
  - DebugStudio の画面
  - ランタイムや Studio のポーリング
  - ソケット往復
- 判定定義（GO / NO-GO。スパイクの場合だけ CONDITIONAL ACCEPT も定義）: 最低条件が固定 head の offline と判定必須テストで成り、常時契約違反が無いとき GO。欠けるとき NO-GO。CONDITIONAL ACCEPT は無い。A2/A3 が未実施のあいだは GO を記録しない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。スパイクは上記で定義した場合だけ CONDITIONAL ACCEPT で終了できる。今回はスパイクではない。
- A3 後の例外承認（人間、理由、置き換える既存条件または期限・検証予算。無ければ `なし`）: なし
- 本文へ転記した実装制約:
  - 既存 catalog の TryExecute だけを使う。第二のバス、asmdef 参照、既定 dispatcher の変更をしない。
  - 選択は `ulong` の instanceId のみ。登録オブジェクトは catalog、selection、渡された world を App 寿命の根として持つ。シーンオブジェクトは持たない。
  - `GetInstanceID()` を使わない。Unity 側 C# に `record` を使わない。新規ファイルの先頭は `#nullable enable`。
  - テストに `Task.Delay` と `Thread.Sleep` を使わない。Phase B は Unity バッチを実行しない。XML を作らない。
  - 登録メソッドは `DebugGameObjectCommands` の新しい partial に置く。1133 行の本体、transform、Renderer のスキャナーは変えない。
  - `AbstractApplicationInitializer.cs` は編集しない。SampleGame の `AppInitializer` も編集しない。
- 未決事項: なし

## 3. 責務マップ

- `DebugGameObjectOptIn.cs`（新規、予想 120 行以下）
  - 責務: 6コマンドの一括登録と、catalog / selection / world をまとめる登録オブジェクト。
  - 公開面は `RegisterGameObjectCommands`、`CreateRegistration`、`DebugGameObjectCommandRegistration`。
  - 所有者は呼び出し側。寿命は App。dispatcher は作らない。
  - 配置理由: 本体は 1133 行、transform は 801 行、Renderer は 699 行である。有効化は別の変わる理由なので、それらのスキャナーに足さない。
- offline の `AppOptInCommandOffline.cs`
  - 責務: 一括登録、未登録 catalog、選択の共有、ソース上の既定 dispatcher と SampleGame と socket サービスが登録を呼ばないこと。
- `AbstractApplicationInitializer.cs`（1196 行）、SampleGame の `AppInitializer`（423 行）、`NullDebugCommandDispatcher`、`DebugSocketService` は変更しない。

## 4. 実装計画

- 変更対象: 節 3 の新規 partial と offline の csproj、`Program.cs` の呼び出し。initializer とプロトコルは触らない。
- 順序: 一括登録、登録オブジェクト、offline。
- Phase B から Phase A へ差し戻す条件: 6コマンドを載せるために既定 dispatcher か SampleGame の起動を変える必要が出たとき。登録名の追加だけでは戻さない。
- 対象外を維持する方法: 新規型は dispatcher を返さず、socket も UpdateSystem も参照しない。SampleGame のファイルは差分に含めない。

## 5. テストとレビュー計画

- 単体テスト: `dotnet run --project tools/DebugCommandOfflineTests/DebugCommandOfflineTests.csproj`
- 差し戻し中の起点 `-Filter`（C が根拠付きで変更可。受け入れ条件の追加ではない）: `DebugGameObjectCommandTests`
- 判定必須テスト（GO 候補 head。実装変更スライスは最終全 EditMode 回帰が標準）: 起点 filter のあと、空 filter の全 EditMode。`pwsh tools/run-tests.ps1`
- 全 EditMode 回帰の適用除外（理由と代替証拠。無ければ `なし`）: なし。Phase B では実行しない。このスライスはシーン操作を足さない。
- 統合・Unity テスト: 新規の EditMode ケースは置かない。既存の未実行ケースは S1 から S4 のまま残る。
- 操作・実行時・目視条件の検証経路: 合否は catalog の結果とソース文字列。見た目判定は無い。offline の標準出力を節 6 に残す。XML は作らない。
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点: ソケット往復は未確認。初回確認は、この program の対象外として残す。確認地点は socket の既存 router。0 件や失敗を XML の手作成で埋めない。
- 人間の判断が必要な条件と合意した担当・証拠の受け入れ方: なし
- 機械検査: `pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1`
- A0/A1 主担当・モデル・ベンダー: 本セッション。製品判断はプログラムから転記。SampleGame を起動時に opt-in しない判断は、プログラムが公開面の非配線を維持していることと、この依頼が既定 dispatcher の全体置換を禁じたことによる。
- A2 独立レビューごとの観点・担当・モデル・ベンダー: 未実施
- A3 統合担当・モデル・採否: 未実施
- C' 用に予約した担当・モデル・ベンダー: 予約しない
- 独立性の強化条件を満たせない場合の理由: C' を開始していない

## 6. Phase B 実装結果

- 実装: 未実施
- HANDOFF との差: 未実施
- 未実行: 未実施
- implementation head commit: 未実施
- Phase B 担当・モデル・ベンダー: 未実施

## 7. Phase C

- 種別: 未実施
- evidence bundle: 未実施
- 構造適合: 未実施
- findings: 未実施
- テスト: 未実施
- 未確認: 未実施
- 担当: 未実施

## 8. Phase C'

- 担当方式: 未実施
- blind audit bundle: 未実施
- 判定: 未実施
- findings: 未実施
- 残存リスク: 未実施
- 独立性: 未実施
- 担当: 未実施

## 9. Phase D

- 突合: 未実施
- マージ判断: 未実施
- harvest: 未実施
- 削除確認: 未実施
