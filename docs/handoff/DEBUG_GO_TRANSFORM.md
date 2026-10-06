# S3: GameObject の local transform

## 0. メタデータ

- type: `slice`
- status: `A`
- branch: `cursor/debug-go-transform-575e`
- implementation base commit: `ecd91591c6c38f1f8b12a6d43d80aea15333f4de`
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

A2 と A3 は行っていない。GO は未判定。実装するのはプログラムに転記済みの local transform だけである。

## 1. 目的と対象外

- 目的: `go.get-transform` と `go.set-transform` が、local position / local euler angles / local scale だけを読み書きし、有限でない数では transform を変えず、選択を付け替えないことを catalog で示す。
- 対象外: world 座標、Rigidbody、quaternion API、Renderer、SampleGame の起動、既定 dispatcher、プロトコル、汎用 Component 編集。
- 現況: S1 が list / select、S2 が set-active を同じ catalog に登録する。transform コマンドは未登録。`DebugGameObjectCommands.cs` は 1133 行である。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: local transform の読み書きが、有限数以外を拒否し、選択を勝手に変えないか。
- 進める最低条件:
  1. `RegisterListAndSelect` と `RegisterSetActive` だけでは `go.get-transform` と `go.set-transform` は未登録である。
  2. `RegisterTransform` のあと、同じ catalog の TryExecute が両コマンドを解決する。登録中は世界を読まない。
  3. 主キーは十進文字列の `instanceId`。数値の `instanceId`、未知プロパティ、壊れた JSON は失敗し、世界を呼ばず、選択を変えない。
  4. `instanceId` が無いときは現在の選択を使う。選択も無いときは `GameObject is not selected.` で失敗し、世界を呼ばない。
  5. 成功しても選択 id は変わらない。対象が現在の選択で生きていないときだけ選択を消す。別 id の不在では選択を残す。
  6. set は渡された成分だけを書く。成分が1つも無いときは失敗し、世界を呼ばない。
  7. 各成分は `x` `y` `z` の有限数をすべて持つ。欠け、余分なキー、文字列、NaN、Infinity、float に入らない値は、その呼び出しではどの成分も書かない。
- 受け入れ条件（進める最低条件を構成する観測可能な詳細。別の完了バーにしない）:
  1. 上記を偽世界の offline TryExecute で観測する。
  2. get の成功メッセージは `Read local transform.`。set の成功メッセージは `Set local transform.`。
  3. 成功 payload のキー順は `instanceId`、`localPosition`、`localEulerAngles`、`localScale`。各ベクトルのキー順は `x`、`y`、`z`。
  4. position だけを書いたとき、euler と scale は呼び出し前の値のままである。応答は書いたあとの3成分すべてである。
  5. Unavailable は `GameObject inspection is unavailable.` で、選択を消さない。
  6. 生きていない対象は `GameObject is not alive.`。
  7. list の payload に `localPosition` があっても、S1 どおり未知プロパティとして失敗する。
  8. Unity 実装は `localPosition` / `localEulerAngles` / `localScale` だけを書く。`position` と Rigidbody は使わない。euler の読み戻しは Unity が保持した値である。EditMode では子の local を変えても親の position が変わらないことを見る。Phase B ではそのテストを実行しない。
- ここでは答えない問いと所有する後続スライス（HANDOFF / program 名）:
  - Renderer の先頭または index: `DEBUG_GO_RENDERER.md`
  - サンプルへの接続: `DEBUG_GO_APP_OPT_IN.md`
- 判定定義（GO / NO-GO。スパイクの場合だけ CONDITIONAL ACCEPT も定義）: 最低条件が固定 head の offline と判定必須テストで成り、常時契約違反が無いとき GO。欠けるとき NO-GO。CONDITIONAL ACCEPT は無い。A2/A3 が未実施のあいだは GO を記録しない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。スパイクは上記で定義した場合だけ CONDITIONAL ACCEPT で終了できる。今回はスパイクではない。
- A3 後の例外承認（人間、理由、置き換える既存条件または期限・検証予算。無ければ `なし`）: なし
- 本文へ転記した実装制約:
  - 既存 catalog の TryExecute だけを使う。第二のバス、asmdef 参照、既定 dispatcher の変更をしない。
  - 選択は `ulong` の instanceId のみ。シーンオブジェクトを呼び出し後に保持しない。
  - `GetInstanceID()` を使わない。`instanceId` は `EntityId.ToULong(GetEntityId())` の十進文字列。
  - Unity 側 C# に `record` を使わない。新規ファイルの先頭は `#nullable enable`。破棄され得るオブジェクトは `== null` で見る。
  - 数値は JSON 数値だけを受け、double として有限かつ float に入れて有限なものだけを書く。
  - テストに `Task.Delay` と `Thread.Sleep` を使わない。Phase B は Unity バッチを実行しない。XML を作らない。
  - transform の payload は `DebugGameObjectCommands` の partial ファイルに置く。list / select の reader に transform のキーを既知キーとして足さない。1133 行のファイルへスキャナーを足さない。
- 未決事項: なし

## 3. 責務マップ

- `DebugGameObjectWorld.cs`（現在 91 行、予想 +70 行）
  - 責務: local transform の値と、部分書き込みのフラグ。読み取り状態は既存の Ok / NotFound / Unavailable を使う。
  - 所有者は呼び出し側。Unity オブジェクトは持たない。
- `DebugGameObjectTransformCommands.cs`（新規、予想 450 行以下）
  - 責務: `go.get-transform` と `go.set-transform` の payload、選択規則、応答 JSON、catalog 登録。
  - `DebugGameObjectCommands` の partial。公開面は `RegisterTransform` と2つのコマンド名。
  - 配置理由: 既存ファイルは既に 500 行警報を超えている。同じ公開型のまま、変わる理由が transform の JSON である部分だけを分ける。
- `UnityDebugGameObjectWorld.cs`（現在 326 行、予想 +80 行）
  - 責務: 見つかった Transform の local 成分だけを読み、渡された成分だけを書く。読み戻しは代入後の Unity の値。
- offline の偽世界に get / set を足す。ケースは `TransformCommandOffline.cs`。
- `DebugGameObjectCommandTests.cs` に、親 position を変えない EditMode ケースを足す。Phase B では実行しない。

`DebugGameObjectCommands.cs` 本体は `partial` 宣言以外を変えない。500 行警報の非分割理由は S1 のまま、新規分はこの partial に逃がす。

## 4. 実装計画

- 変更対象: 節 3 と offline の csproj。initializer とプロトコルは触らない。
- 順序: 値型とインターフェース、partial の登録と JSON、偽世界、未実行の EditMode。
- Phase B から Phase A へ差し戻す条件: world 座標や Rigidbody が無いと最低条件を満たせないとき。失敗した JSON 文言の調整だけでは戻さない。
- 対象外を維持する方法: 登録メソッドは get と set の2名だけを Register する。Renderer 名は登録しない。

## 5. テストとレビュー計画

- 単体テスト: `dotnet run --project tools/DebugCommandOfflineTests/DebugCommandOfflineTests.csproj`
- 差し戻し中の起点 `-Filter`（C が根拠付きで変更可。受け入れ条件の追加ではない）: `DebugGameObjectCommandTests`
- 判定必須テスト（GO 候補 head。実装変更スライスは最終全 EditMode 回帰が標準）: 起点 filter のあと、空 filter の全 EditMode。`pwsh tools/run-tests.ps1`
- 全 EditMode 回帰の適用除外（理由と代替証拠。無ければ `なし`）: なし。Phase B では実行しない。
- 統合・Unity テスト: EditMode が子の localPosition と親の position を値で見る。見た目判定は無い。
- 操作・実行時・目視条件の検証経路: 合否は JSON と Transform の値。offline の標準出力を節 6 に残す。EditMode の XML は Phase C が作る。
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点: Unity が euler をどう保持するかは未確認。初回確認は Phase C。確認地点は `DebugGameObjectCommandTests`。0 件や失敗を XML の手作成で埋めない。
- 人間の判断が必要な条件と合意した担当・証拠の受け入れ方: なし
- 機械検査: `pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1`
- A0/A1 主担当・モデル・ベンダー: 本セッション。製品判断はプログラムから転記。
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
