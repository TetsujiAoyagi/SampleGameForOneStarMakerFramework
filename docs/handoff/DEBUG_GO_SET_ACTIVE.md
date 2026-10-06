# S2: GameObject の set-active

## 0. メタデータ

- type: `slice`
- status: `B`
- branch: `cursor/debug-go-set-active-575e`
- implementation base commit: `2d66bdab634402aaef10166e3c6bcc4a536aada7`
- implementation head commit: `604c06ffc1b0b86fff64471fee60a3fdd30abc6a`
- risk: `normal`
- owner: 発注者（採否）。A1 文面は 2026-10-06 の実装担当。
- created: 2026-10-06
- expires: 2026-12-15、または program の置換 revision
- harvest to: 恒久契約は `unity/Assets/Docs/Architecture/12-telemetry.md` の incoming control catalog 節。プログラムは [DEBUG_GAMEOBJECT_COMMANDS.md](DEBUG_GAMEOBJECT_COMMANDS.md)。S1 は [DEBUG_GO_LIST_SELECT.md](DEBUG_GO_LIST_SELECT.md)。
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

A2 と A3 は行っていない。GO は未判定。製品境界はプログラムに転記済みの set-active だけを実装する。

implementation base は S1 の結果記録コミットである。S1 の implementation head は `b3c7c514ad93a13036049d6c36b135b378826bf4`。

## 1. 目的と対象外

- 目的: 明示登録した `go.set-active` が、対象 GameObject の `SetActive` だけを変え、親が非アクティブなら `activeInHierarchy` が false のままであることを catalog の結果で示す。
- 対象外: transform、Renderer、SampleGame の起動、既定 dispatcher、プロトコル、汎用 Component 編集、選択の付け替え、親の active を直すこと。
- 現況: S1 が `go.list` と `go.select` を catalog に登録する。`go.set-active` は未登録。世界インターフェースは読み取りだけである。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 凍結した set-active が、親の非アクティブを変えず、現在の選択を勝手に付け替えずに実行できるか。
- 進める最低条件:
  1. `RegisterListAndSelect` だけでは `go.set-active` は未登録のままである。
  2. `RegisterSetActive` のあと、同じ catalog の TryExecute が `go.set-active` を解決する。
  3. `active` が無い、型が違う、未知プロパティ、数値の `instanceId` は失敗し、世界を呼ばず、選択を変えない。
  4. `instanceId` が無いときは現在の選択を使う。選択も無いときは `GameObject is not selected.` で失敗し、世界を呼ばない。
  5. 成功しても選択 id は変わらない。
  6. 対象が現在の選択で、かつ生きていないときだけ選択を消す。別 id の不在では選択を残す。
  7. 要求が true で結果の `activeInHierarchy` が false のとき、メッセージは `Set activeSelf. An inactive parent still keeps activeInHierarchy false.` である。それ以外の成功は `Set GameObject active.`
- 受け入れ条件（進める最低条件を構成する観測可能な詳細。別の完了バーにしない）:
  1. 上記 1 から 7 を、偽世界を登録した offline の TryExecute で観測する。
  2. 成功 payload は S1 の行 JSON である。`activeSelf` は要求値、`activeInHierarchy` は世界が返した値である。
  3. Unavailable は `GameObject inspection is unavailable.` で、選択を消さない。
  4. 壊れている対象のメッセージは S1 と同じ `GameObject is not alive.`
  5. Unity 実装は見つかった GameObject にだけ `SetActive` を呼ぶ。親は呼ばない。EditMode では、非アクティブな親の子を active にしたあと、子の `activeSelf` が true、子の `activeInHierarchy` が false、親の `activeSelf` が false であることを見る。
- ここでは答えない問いと所有する後続スライス（HANDOFF / program 名）:
  - local transform: `DEBUG_GO_TRANSFORM.md`
  - Renderer: `DEBUG_GO_RENDERER.md`
  - サンプルへの接続: `DEBUG_GO_APP_OPT_IN.md`
- 判定定義（GO / NO-GO。スパイクの場合だけ CONDITIONAL ACCEPT も定義）: 最低条件が固定 head の offline と判定必須テストで成り、常時契約違反が無いとき GO。欠けるとき NO-GO。CONDITIONAL ACCEPT は無い。A2/A3 が未実施のあいだは GO を記録しない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。スパイクは上記で定義した場合だけ CONDITIONAL ACCEPT で終了できる。今回はスパイクではない。
- A3 後の例外承認（人間、理由、置き換える既存条件または期限・検証予算。無ければ `なし`）: なし
- 本文へ転記した実装制約:
  - S1 の catalog、選択の `ulong` のみ、シーン参照を呼び出し後に残さない、既定 dispatcher を変えない、を維持する。
  - `SetActive` は対象の GameObject だけ。親を active にしない。
  - インターフェースへ足すのは `TrySetActive` だけである。第二のバスは作らない。
  - `#nullable enable`、`record` 禁止、Unity オブジェクトは `== null`、`GetInstanceID()` は使わない。
  - テストに `Task.Delay` と `Thread.Sleep` を使わない。Phase B は Unity バッチを実行しない。XML を作らない。
- 未決事項: なし

## 3. 責務マップ

- `IDebugGameObjectWorld`（`DebugGameObjectWorld.cs`、現在 86 行、予想 +15 行）
  - 責務: 読み取りに加え、1オブジェクトの active 変更を同じ状態機械で返す。
  - 所有者と寿命は S1 のまま。公開面にメソッドを1つ足す。
- `DebugGameObjectCommands.cs`（現在 970 行、予想 +120 行）
  - 責務: `go.set-active` の payload と選択規則。list / select の未知プロパティ規則は変えない。`active` は set-active の読み取りモードでだけ既知キーにする。
  - 非分割: S1 で記録したとおり、payload と handler は同じ契約である。本スライスの追加もそのファイルに置く。
- `UnityDebugGameObjectWorld.cs`（現在 293 行、予想 +40 行）
  - 責務: 見つかったオブジェクトへ `SetActive` し、変更後の行を返す。親は触らない。
- `tools/DebugCommandOfflineTests/GameObjectCommandOffline.cs` の偽世界に `TrySetActive` を足す。新しいケースは隣のファイルに置く。
- `DebugGameObjectCommandTests.cs` に、親が非アクティブな子の EditMode ケースを足す。Phase B では実行しない。

500 行警報は `DebugGameObjectCommands.cs` が既に発火している。分割しない理由は S1 節 6 と同じで、本スライスはその判断を変えない。

## 4. 実装計画

- 変更対象: 節 3。initializer とプロトコルは触らない。
- 順序: インターフェース、Unity 実装、登録と JSON、offline、未実行の EditMode。
- Phase B から Phase A へ差し戻す条件: 親の active を変える、選択を成功時に付け替える、または list の未知プロパティ失敗を緩める必要が出たとき。
- 対象外を維持する方法: 登録メソッドは `go.set-active` だけを追加する。transform と Renderer の名前は登録しない。

## 5. テストとレビュー計画

- 単体テスト: `dotnet run --project tools/DebugCommandOfflineTests/DebugCommandOfflineTests.csproj`
- 差し戻し中の起点 `-Filter`（C が根拠付きで変更可。受け入れ条件の追加ではない）: `DebugGameObjectCommandTests`
- 判定必須テスト（GO 候補 head。実装変更スライスは最終全 EditMode 回帰が標準）: 起点 filter のあと、空 filter の全 EditMode。`pwsh tools/run-tests.ps1`
- 全 EditMode 回帰の適用除外（理由と代替証拠。無ければ `なし`）: なし。Phase B では実行しない。
- 統合・Unity テスト: EditMode が親の非アクティブを state で見る。見た目判定は無い。
- 操作・実行時・目視条件の検証経路: 合否は JSON と `activeSelf` / `activeInHierarchy` の値。offline の標準出力を節 6 に残す。EditMode の XML は Phase C が作る。
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点: Unity の `SetActive` と親の関係は未確認。初回確認は Phase C。確認地点は `DebugGameObjectCommandTests`。0 件や失敗を XML の手作成で埋めない。
- 人間の判断が必要な条件と合意した担当・証拠の受け入れ方: なし
- 機械検査: `pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1`
- A0/A1 主担当・モデル・ベンダー: 本セッション。製品判断はプログラムから転記。
- A2 独立レビューごとの観点・担当・モデル・ベンダー: 未実施
- A3 統合担当・モデル・採否: 未実施
- C' 用に予約した担当・モデル・ベンダー: 予約しない
- 独立性の強化条件を満たせない場合の理由: C' を開始していない

## 6. Phase B 実装結果

- 実装: `RegisterSetActive` が `go.set-active` だけを既存 catalog へ足す。`TrySetActive` は見つかった GameObject にだけ `SetActive` する。成功は選択を付け替えない。現在の選択そのものが生きていないときだけ選択を消す。要求が true で `activeInHierarchy` が false のときだけ、親が非アクティブである旨のメッセージを返す。
- HANDOFF との差: `DebugGameObjectCommands.cs` は 1133 行、`UnityDebugGameObjectWorld.cs` は 326 行、`DebugGameObjectWorld.cs` は 91 行。非分割の理由は節 3 のまま。list が `active` を未知プロパティとして拒否することは offline で確認した。
- 未実行: EditMode の `DebugGameObjectCommandTests`（list/select と、親が非アクティブな子の set-active）、空 filter の全 EditMode、Player、ソケット往復、Unity Editor のコンパイル確認。結果 XML は作っていない。A2、A3、Phase C、Phase C' は未実施。GO は記録しない。
- 実行した確認: `dotnet run --project tools/DebugCommandOfflineTests/DebugCommandOfflineTests.csproj` は 19 passed, 0 failed, 19 executed。S1 の 15 件に set-active の 4 件を加えた。`pwsh tools/contract-audit.ps1 -BaseRef origin/develop` は実装コミット前に errors 0、warnings 0。`pwsh tools/docs-audit.ps1` は本節の追記後に errors 0、warnings 0。
- implementation head commit: `604c06ffc1b0b86fff64471fee60a3fdd30abc6a`。本節だけの後続コミットは implementation head に含めない。
- Phase B 担当・モデル・ベンダー: 本セッションの実装担当。A2/A3 を経ていない。

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
