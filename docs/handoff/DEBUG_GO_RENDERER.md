# S4: GameObject の Renderer enabled

## 0. メタデータ

- type: `slice`
- status: `B`
- branch: `cursor/debug-go-renderer-575e`
- implementation base commit: `a3e978abd3151306600a69c79349f4182816f3b0`
- implementation head commit: `5bcad1446894bcc6a888c4e784a6fe0ba7cc0ee1`
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

A2 と A3 は行っていない。GO は未判定。実装するのはプログラムに転記済みの、1 つの Renderer の `enabled` だけである。

## 1. 目的と対象外

- 目的: `go.set-renderer-enabled` が、対象 GameObject 自身の Renderer を 1 つだけ有効または無効にし、範囲外では何も変えず、Renderer が無いことをオブジェクトの死と扱わないことを catalog で示す。
- 対象外: 子の Renderer、全 Renderer の一括切り替え、MeshRenderer だけの走査、汎用 Component 編集、material やその他プロパティ、SampleGame の起動、既定 dispatcher、プロトコル。
- 現況: S1 が list / select、S2 が set-active、S3 が local transform を同じ catalog に登録する。Renderer コマンドは未登録。`DebugGameObjectCommands.cs` は 1133 行、transform の partial は 801 行である。

## 複数 Renderer の方針

- 1 回の成功は、対象 GameObject に付いている Renderer のうち 1 つだけを変える。
- 見る集合は `GetComponents<Renderer>()` の戻り順である。子は含まない。MeshRenderer だけに絞り込まない。
- `rendererIndex` が無いときは 0。これは component 順の先頭であり、「全部」でも「最初の MeshRenderer」でもない。
- `rendererIndex` があるときは、その index の 1 つだけを変える。他の Renderer と、子の Renderer は変えない。
- index が 0 以上で、件数より大きい、または Renderer が 0 件のときは失敗する。どの `enabled` も変えない。この失敗はオブジェクトが生きているので、選択を消さない。メッセージは `Renderer was not found.`
- 負数、整数でない数、文字列の index は payload 不正であり、世界を呼ばない。
- `enabled` を false にしても、その Renderer は component 順から消えない。同じ index は同じ Renderer を指し続ける。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 先頭または明示 index の 1 Renderer だけを切り替えられるか。
- 進める最低条件:
  1. `RegisterListAndSelect`、`RegisterSetActive`、`RegisterTransform` だけでは `go.set-renderer-enabled` は未登録である。
  2. `RegisterRenderer` のあと、同じ catalog の TryExecute がそのコマンドを解決する。登録中は世界を読まない。
  3. 主キーは十進文字列の `instanceId`。数値の `instanceId`、未知プロパティ、壊れた JSON は失敗し、世界を呼ばず、選択を変えない。
  4. `instanceId` が無いときは現在の選択を使う。選択も無いときは `GameObject is not selected.` で失敗し、世界を呼ばない。
  5. `enabled` は必須の JSON 真偽値。欠けるときは失敗し、世界を呼ばない。
  6. 成功しても選択 id は変わらない。対象オブジェクトが現在の選択で生きていないときだけ選択を消す。別 id の不在では選択を残す。
  7. Renderer が無い、または index が件数以上のときは `Renderer was not found.` で失敗し、どの Renderer も変えず、選択を消さない。
  8. 成功は指定 index の `enabled` だけを変える。他の index と子は変えない。
- 受け入れ条件（進める最低条件を構成する観測可能な詳細。別の完了バーにしない）:
  1. 上記を偽世界の offline TryExecute で観測する。
  2. 成功メッセージは `Set Renderer enabled.`
  3. 成功 payload のキー順は `instanceId`、`rendererIndex`、`rendererCount`、`enabled`、`typeName`。`instanceId` は十進文字列、`typeName` は具体型の名前である。
  4. index を省略した成功は 0 番だけを変える。
  5. Unavailable は `GameObject inspection is unavailable.` で、選択を消さない。
  6. 生きていない対象は `GameObject is not alive.`
  7. list の payload に `rendererIndex` があっても、未知プロパティとして失敗する。
  8. Unity 実装は対象の `GetComponents<Renderer>()` だけを使い、子は見ない。範囲外では `enabled` を代入しない。EditMode では index 1 を無効にしても index 0 と子の Renderer が有効なままであることを見る。Phase B ではそのテストを実行しない。
- ここでは答えない問いと所有する後続スライス（HANDOFF / program 名）:
  - サンプルへの接続: `DEBUG_GO_APP_OPT_IN.md`
- 判定定義（GO / NO-GO。スパイクの場合だけ CONDITIONAL ACCEPT も定義）: 最低条件が固定 head の offline と判定必須テストで成り、常時契約違反が無いとき GO。欠けるとき NO-GO。CONDITIONAL ACCEPT は無い。A2/A3 が未実施のあいだは GO を記録しない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。スパイクは上記で定義した場合だけ CONDITIONAL ACCEPT で終了できる。今回はスパイクではない。
- A3 後の例外承認（人間、理由、置き換える既存条件または期限・検証予算。無ければ `なし`）: なし
- 本文へ転記した実装制約:
  - 既存 catalog の TryExecute だけを使う。第二のバス、asmdef 参照、既定 dispatcher の変更をしない。
  - 選択は `ulong` の instanceId のみ。シーンオブジェクトを呼び出し後に保持しない。
  - `GetInstanceID()` を使わない。`instanceId` は `EntityId.ToULong(GetEntityId())` の十進文字列。
  - Unity 側 C# に `record` を使わない。新規ファイルの先頭は `#nullable enable`。破棄され得るオブジェクトは `== null` で見る。
  - Renderer 不在を `DebugGameObjectReadStatus.NotFound` に載せない。NotFound は生きていない対象であり、現在の選択を消す理由になる。Renderer 不在は末尾に足す `MissingRenderer` とし、選択を変えない。既存の 0、1、2 は並べ替えない。
  - テストに `Task.Delay` と `Thread.Sleep` を使わない。Phase B は Unity バッチを実行しない。XML を作らない。
  - Renderer の payload は `DebugGameObjectCommands` の新しい partial に置く。list / select の reader と transform の reader には Renderer のキーを既知キーとして足さない。
- 未決事項: なし

## 3. 責務マップ

- `DebugGameObjectWorld.cs`（現在 176 行、予想 +40 行）
  - 責務: Renderer 1 件の応答値と、`MissingRenderer`。読み取り状態の既存 3 値は並べ替えない。
  - 所有者は呼び出し側。Unity オブジェクトは持たない。
- `DebugGameObjectRendererCommands.cs`（新規、予想 500 行以下）
  - 責務: `go.set-renderer-enabled` の payload、選択規則、応答 JSON、catalog 登録。
  - `DebugGameObjectCommands` の partial。公開面は `RegisterRenderer` とコマンド名。
  - 配置理由: 本体は 1133 行、transform partial は 801 行である。Renderer の JSON は別の変わる理由なので、どちらのスキャナーにも足さない。
- `UnityDebugGameObjectWorld.cs`（現在 429 行、予想 +70 行）
  - 責務: 見つかった GameObject の `GetComponents<Renderer>()` から、指定 index の `enabled` だけを書く。範囲外では代入しない。
- offline の偽世界に、id ごとの Renderer 列と set を足す。ケースは `RendererCommandOffline.cs`。
- `DebugGameObjectCommandTests.cs` に、index 1 だけを無効化し、index 0 と子を変えない EditMode ケースを足す。Phase B では実行しない。

`DebugGameObjectCommands.cs` 本体と transform partial は変えない。

## 4. 実装計画

- 変更対象: 節 3 と offline の csproj。initializer とプロトコルは触らない。
- 順序: 状態値とインターフェース、partial の登録と JSON、偽世界、未実行の EditMode。
- Phase B から Phase A へ差し戻す条件: Renderer 不在をオブジェクトの死と分けられないとき、または 1 件だけを変える実装が子の走査を必要とするとき。失敗した JSON 文言の調整だけでは戻さない。
- 対象外を維持する方法: 登録メソッドは `go.set-renderer-enabled` だけを Register する。material 名や子を含む名前は登録しない。

## 5. テストとレビュー計画

- 単体テスト: `dotnet run --project tools/DebugCommandOfflineTests/DebugCommandOfflineTests.csproj`
- 差し戻し中の起点 `-Filter`（C が根拠付きで変更可。受け入れ条件の追加ではない）: `DebugGameObjectCommandTests`
- 判定必須テスト（GO 候補 head。実装変更スライスは最終全 EditMode 回帰が標準）: 起点 filter のあと、空 filter の全 EditMode。`pwsh tools/run-tests.ps1`
- 全 EditMode 回帰の適用除外（理由と代替証拠。無ければ `なし`）: なし。Phase B では実行しない。
- 統合・Unity テスト: EditMode が index 0、index 1、子の `enabled` を値で見る。見た目判定は無い。
- 操作・実行時・目視条件の検証経路: 合否は JSON と `enabled` の値。offline の標準出力を節 6 に残す。EditMode の XML は Phase C が作る。
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点: `GetComponents<Renderer>()` の実順は未確認。初回確認は Phase C。確認地点は `DebugGameObjectCommandTests`。0 件や失敗を XML の手作成で埋めない。
- 人間の判断が必要な条件と合意した担当・証拠の受け入れ方: なし
- 機械検査: `pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1`
- A0/A1 主担当・モデル・ベンダー: 本セッション。製品判断はプログラムから転記。
- A2 独立レビューごとの観点・担当・モデル・ベンダー: 未実施
- A3 統合担当・モデル・採否: 未実施
- C' 用に予約した担当・モデル・ベンダー: 予約しない
- 独立性の強化条件を満たせない場合の理由: C' を開始していない

## 6. Phase B 実装結果

- 実装: `RegisterRenderer` が `go.set-renderer-enabled` だけを既存 catalog へ足す。payload は `DebugGameObjectRendererCommands` に置き、list の reader は `rendererIndex` を未知プロパティのまま拒否する。省略した index は 0、明示した index はその 1 件だけを変える。Renderer 不在は `MissingRenderer` で、選択を消さない。生きていない対象だけが、現在の選択と一致するとき選択を消す。
- HANDOFF との差: `DebugGameObjectRendererCommands.cs` は 699 行で、予想の 500 行を超えた。list と transform の reader とスキャナーを共有しないためである。payload、選択規則、応答のキー順が同じ Renderer 規則なので、ここではさらに分割していない。本体と transform partial は変えていない。`DebugGameObjectWorld.cs` は 216 行、`UnityDebugGameObjectWorld.cs` は 484 行。Unity 実装は `GetComponents<Renderer>()` だけを使い、範囲外では `enabled` を代入しない。
- 未実行: EditMode の `DebugGameObjectCommandTests`（list/select、set-active、local transform、index 1 だけの Renderer）、空 filter の全 EditMode、Player、ソケット往復、Unity Editor のコンパイル確認。結果 XML は作っていない。A2、A3、Phase C、Phase C' は未実施。GO は記録しない。
- 実行した確認: `dotnet run --project tools/DebugCommandOfflineTests/DebugCommandOfflineTests.csproj` は 29 passed, 0 failed, 29 executed。S3 の 24 件に Renderer の 5 件を加えた。`pwsh tools/contract-audit.ps1 -BaseRef origin/develop` は実装コミット前に errors 0、warnings 0。`pwsh tools/docs-audit.ps1` は本節の追記後に errors 0、warnings 0。
- implementation head commit: `5bcad1446894bcc6a888c4e784a6fe0ba7cc0ee1`。本節だけの後続コミットは implementation head に含めない。
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
