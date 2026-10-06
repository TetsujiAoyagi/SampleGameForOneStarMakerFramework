# S1: GameObject の list と select

## 0. メタデータ

- type: `slice`
- status: `B`
- branch: `cursor/debug-go-list-select-575e`（計画コミットは `cursor/debug-go-commands-plan-575e` にもある）
- implementation base commit: `6d804ca637cf42fb876e602c8ccc0656cfbd255d`
- implementation head commit: `b3c7c514ad93a13036049d6c36b135b378826bf4`
- risk: `normal`
- owner: 発注者（採否）。A1 文面は 2026-10-06 の実装担当。
- created: 2026-10-06
- expires: 2026-12-15、または program の置換 revision
- harvest to: 恒久契約は `unity/Assets/Docs/Architecture/12-telemetry.md` の incoming control catalog 節。プログラム計画の索引は `docs/README.md`。hierarchy スキーマへは harvest しない。
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

A2 と A3 は行っていない。Phase A snapshot は凍結版が無いので生成しない。Phase B の文章は節 6 にあり、別ファイルの result snapshot は作っていない。evidence と C' bundle は未生成。GO は未判定。

プログラム境界は [DEBUG_GAMEOBJECT_COMMANDS.md](DEBUG_GAMEOBJECT_COMMANDS.md)。本スライスはその S1 だけを実装する。

## 1. 目的と対象外

- 目的: 既存の `DebugCommandCatalog.TryExecute` に、明示登録した `go.list` と `go.select` を足し、`instanceId` で1ページの列挙と選択・破棄クリアができることを offline で示す。
- 対象外: `go.set-active`、transform、Renderer、SampleGame の `AppInitializer`、既定 dispatcher の変更、DebugSocket プロトコル、hierarchy registry、DebugStudio の画面、UI アクセシビリティ、毎フレーム走査、第二のコマンドバス。
- 現況: `DebugCommandCatalog`（75行）は名前から同期 handler を呼ぶだけである。`CatalogDebugCommandDispatcher` が封筒へ転写する。ソケットは `MainThreadDebugCommandDispatcher` で main thread に戻してから dispatcher を呼ぶ。組込 `ping` と `runtime-diagnostics` は router が dispatcher より前に処理する。既定の `CreateDebugCommandDispatcher` は `NullDebugCommandDispatcher`。SampleGame はこれを override していない。公開面は、catalog が opt-in であり、closure がシーン所有オブジェクトを強参照しない、と書いている。`GetInstanceID()` は契約検査が拒否する。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 明示登録した一つの catalog が、要求時だけ、ページ上限つきの `go.list` と、生存確認つきの `go.select` を、既存の TryExecute だけで返せるか。
- 進める最低条件:
  1. 未登録の catalog では `go.list` と `go.select` は未登録失敗のままである。
  2. 登録メソッドのあと、同じ catalog の TryExecute が両コマンドを解決する。
  3. `go.list` は offset / limit の契約と `hasMore` を守り、表示パスを検索キーにしない。
  4. `go.select` は十進文字列の `instanceId` だけを受け、見つかれば選択をその id に置き、見つからなければ選択を消して失敗する。
  5. 壊れた payload、未知プロパティ、ページ範囲外、世界の利用不可では選択を変えない。
  6. 登録処理自身は世界を読まない。コマンド未登録のとき、シーンを歩く常駐処理を追加しない。
- 受け入れ条件（進める最低条件を構成する観測可能な詳細。別の完了バーにしない）:
  1. 新規 catalog に対する `go.list` / `go.select` / `go.set-active` の TryExecute は false で、メッセージは既存の `Debug command is not registered.` である。
  2. `DebugGameObjectCommands.RegisterListAndSelect` のあと、catalog の Count は登録前より 2 だけ増える。`go.set-active` は未登録のまま。
  3. 登録に null の catalog、world、selection を渡すと ArgumentNullException。同名の再登録は catalog 既存の InvalidOperationException。
  4. `go.list` の空文字、空白、`{}` は offset 0、limit 64。応答 JSON のキー順は `offset`、`limit`、`count`、`hasMore`、`selectionCleared`、`selectionInstanceId`、`items`。
  5. `limit` が 0 または 129、`offset` が負、`limit` や `offset` が文字列や小数のとき、Success は false、メッセージは `Debug GameObject page is out of range.`、選択は不変、世界の列挙は呼ばれない。
  6. 未知プロパティ、配列、壊れた JSON、数値の `instanceId` は Success false、メッセージは `Debug GameObject command payload is invalid.`、選択は不変。
  7. 列挙結果は渡した順を保つ。ページを超える次要素があるときだけ `hasMore` が true。offset が末尾を超えると count 0 かつ hasMore false。
  8. 行のキー順は `instanceId`、`parentInstanceId`、`name`、`sceneName`、`displayPath`、`activeSelf`、`activeInHierarchy`、`siblingIndex`、`childCount`。親が無い行の `parentInstanceId` は null。`instanceId` は十進文字列。名前に含まれる引用符とバックスラッシュは JSON としてエスケープされる。
  9. 同名で `displayPath` が同じ二つの行は、それぞれの `instanceId` で select すると別の name ではなく、その id の行が返る。select の成功メッセージは `Selected GameObject.`、payload はその行1件。
  10. 選択中に世界が NotFound を返すと、`go.select` はメッセージ `GameObject is not alive.` で失敗し選択は空になる。続く `go.list` は成功し `selectionCleared` が false（すでに空）で `selectionInstanceId` は null。死んだ選択を抱えたまま `go.list` したときは、ページ成功のまま `selectionCleared` true かつ選択は空。
  11. 世界が Unavailable を返した list と select は、メッセージ `GameObject inspection is unavailable.` で失敗し、選択を消さない。
  12. `RegisterListAndSelect` の呼び出し中、世界の列挙も検索も 0 回である。
- ここでは答えない問いと所有する後続スライス（HANDOFF / program 名）:
  - set-active と親が非アクティブなときの文言: `DEBUG_GO_SET_ACTIVE.md`（program 表の S2）
  - local transform: `DEBUG_GO_TRANSFORM.md`（S3）
  - Renderer の先頭または index: `DEBUG_GO_RENDERER.md`（S4）
  - サンプル起動への接続: `DEBUG_GO_APP_OPT_IN.md`（S5）
  - id 再利用の強参照以外の世代: 本プログラムは強参照を禁止したまま残リスクとする。専用 HANDOFF は再利用が観測されてから切る。
- 判定定義（GO / NO-GO。スパイクの場合だけ CONDITIONAL ACCEPT も定義）: 進める最低条件の 1 から 6 が、固定した implementation head の offline テストと、Phase C の判定必須テストで同時に成り、常時契約への違反が無いとき GO。一つでも欠ける、または契約違反があるとき NO-GO。CONDITIONAL ACCEPT は定義しない。**A2/A3 が未実施の head では、条件を満たして見えても GO を記録しない。**
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。スパイクは上記で定義した場合だけ CONDITIONAL ACCEPT で終了できる。今回はスパイクではない。GO の記録は A2/A3 と判定 C のあとだけにする。
- A3 後の例外承認（人間、理由、置き換える既存条件または期限・検証予算。無ければ `なし`）: なし。A3 は未実施。発注者の作業指示は、合意済みの製品判断を実装してよいと述べ、同時に A2/A3 が無かったことを記録するよう求めている。これは指摘の採否セッションではない。
- 本文へ転記した実装制約:
  - 依存は Game から Framework への一方向。asmdef 参照を足さない。新しいコマンドバスを作らない。
  - Unity 側 C# に `record` を使わない。新規ファイルの先頭は `#nullable enable`。
  - 破棄され得る `UnityEngine.Object` は `== null` / `!= null` で見る。
  - `GetInstanceID()` を呼ばない。`instanceId` は `EntityId.ToULong(GetEntityId())` の十進文字列。
  - 公開ログ抽象を増やさない。ZLogger 型を公開面へ出さない。
  - Editor 用コードを Runtime アセンブリへ置かない。
  - UpdateSystem へ登録しない。1フレームの走査順を変えない。
  - catalog は System のみに依存したままにする。Unity 世界のファイルは offline プロジェクトの Compile Include に入れない。
  - closure はシーン所有オブジェクトをコマンド終了後に強参照で持たない。走査バッファは呼び出しの finally でクリアする。
  - 既定の `NullDebugCommandDispatcher` と `AbstractApplicationInitializer.CreateDebugCommandDispatcher` は編集しない。`AbstractApplicationInitializer.cs` は既に 1196 行であり、本スライスの追記先にしない。
  - テストに `Task.Delay` と `Thread.Sleep` を使わない。
  - `SceneState` の14値を変えない。
  - H1 の外部 CURRENT は使わない。Unity バッチテストは Phase C まで実行しない。結果 XML を作らない。
- 未決事項: なし。上の後続へ送った問いは未決のまま実装しない。

## 3. 責務マップ

ファイルごとに、責務、変更理由、所有者・寿命、依存、公開面、テスト境界、配置理由、現在行数と予想増分を書く。

- `unity/Assets/OneStarMaker/Scripts/Runtime/DebugCommands/DebugCommandCatalog.cs`（現在 75 行、予想増分 0）
  - 責務: 名前の登録と同期実行。本スライスでは変えない。
  - 理由: 第二のバスを避け、既存 TryExecute を使うため、目録型へ GameObject 知識を入れない。
  - 所有者: App が初期登録後に渡す instance。寿命は App。
  - 依存: System のみ。公開面は現行のまま。
  - テスト: 既存 offline と EditMode の catalog テストを壊さない。

- `unity/Assets/OneStarMaker/Scripts/Runtime/DebugCommands/DebugGameObjectCommands.cs`（新規、予想 220 行以下）
  - 責務: `go.list` と `go.select` の payload 判定、選択の更新規則、応答 JSON の組み立て、catalog への登録。
  - 理由: コマンド名と失敗境界は世界の実装と独立して変わる。
  - 所有者: 状態を持たない静的登録。選択と世界は引数の所有者。
  - 依存: catalog、selection、world インターフェース。UnityEngine には依存しない。
  - 公開面: `RegisterListAndSelect`、コマンド名定数、ページ上限定数。
  - テスト: offline が catalog 経由で観測する。
  - 配置: 既存 `Runtime/DebugCommands`。asmdef は `OneStarMaker.Runtime` のまま。

- `unity/Assets/OneStarMaker/Scripts/Runtime/DebugCommands/DebugGameObjectSelection.cs`（新規、予想 50 行以下）
  - 責務: 選択中 `instanceId` の有無だけを保持する。
  - 理由: 寿命が App 側の明示オブジェクトであり、コマンド手続きやシーン走査とは別である。
  - 所有者: 登録を呼ぶ App。寿命は catalog と同じ App 寿命。static ではない。
  - 依存: System のみ。シーンオブジェクトは持たない。
  - 公開面: 生成、置換、クリア、現在値の読み取り。
  - テスト: コマンド結果の `selectionInstanceId` と `selectionCleared` で観測する。

- `unity/Assets/OneStarMaker/Scripts/Runtime/DebugCommands/DebugGameObjectWorld.cs`（新規、予想 80 行以下）
  - 責務: 行 DTO、読み取り状態（Ok / NotFound / Unavailable）、列挙と単体検索のインターフェース。
  - 理由: Unity 走査と offline の偽世界を同じハンドラへ差し替えるため。
  - 所有者: 実装の所有者は呼び出し側。インターフェース自体は状態を持たない。
  - 依存: System のみ。
  - 公開面: `IDebugGameObjectWorld`、`DebugGameObjectRow`、`DebugGameObjectReadStatus`。
  - テスト: 偽実装を offline テスト内に置く。

- `unity/Assets/OneStarMaker/Scripts/Runtime/DebugCommands/DebugGameObjectThreadGate.cs`（新規、予想 30 行以下）
  - 責務: 生成スレッドと呼び出しスレッドが同じかを返す。
  - 理由: Unity 世界がシーン API の前に拒む条件を、Unity 無しでテストする。
  - 所有者: それを持つ世界インスタンス。寿命は世界と同じ。
  - 依存: System のみ。
  - 公開面: コンストラクタと呼び出し可否。
  - テスト: offline で、生成スレッドは許可、別スレッドは拒否。待機はシグナルで行う。

- `unity/Assets/OneStarMaker/Scripts/Runtime/DebugCommands/UnityDebugGameObjectWorld.cs`（新規、予想 260 行以下）
  - 責務: ロード済みシーンの反復走査、表示行の作成、スレッドゲート、バッファの解放。
  - 理由: UnityEngine と SceneManagement に依存する I/O を、JSON と選択規則から分ける。
  - 所有者: App が登録時に生成する。寿命は App。シーンオブジェクトは呼び出し中だけ触り、finally でバッファを空にする。
  - 依存: UnityEngine、上のインターフェースとスレッドゲート。DebugSocket の hierarchy 型には依存しない。
  - 公開面: 具象クラス1つ。
  - テスト: offline の Compile 対象外。EditMode テストを書くが、本スライスの Phase B では実行しない。
  - 配置: 同じ Runtime フォルダ。Editor asmdef には置かない。

- `tools/DebugCommandOfflineTests/Program.cs`（現在 199 行、予想 +250 行、合計 500 行未満）
  - 責務: 既存 catalog 契約の offline 実行に、S1 の受け入れ条件を足す。
  - 理由: Unity を起動しない証明がここにある。
  - テスト境界: 偽世界。シーン走査の証明には使わない。
  - 500 行に近づく理由: 期待 JSON のケースが並ぶため。ケースを別クラスへ分けても失敗境界は増えないので、まず1ファイルに置く。500 行を超えたらケース定義だけを隣のファイルへ移す。

- `tools/DebugCommandOfflineTests/DebugCommandOfflineTests.csproj`（現在 14 行、予想 +数行）
  - 責務: Unity に依存しないソースだけを Compile Include する。
  - `UnityDebugGameObjectWorld.cs` は含めない。

- `unity/Assets/OneStarMaker/Tests/DebugCommands/DebugGameObjectCommandTests.cs`（新規、予想 180 行以下）
  - 責務: EditMode で、実 GameObject の list ページに id が出ること、select、破棄後の NotFound、非アクティブの flag を見る。
  - 理由: offline ではシーン走査を証明できない。
  - 配置: 既存 `OneStarMaker.Tests`。asmdef は変えない。
  - 実行: Phase C。Phase B では走らせず、XML も作らない。

行数警報: 変更対象に、現時点で 500 行超・3責務混在・50% 超の増加を計画しているファイルは無い。`AbstractApplicationInitializer.cs` は対象外のままにする。`Program.cs` が 500 行を超えた場合の分割は上に書いた。

新しい中核（payload 判定、選択クリア、ページ契約）は Unity 無しで単体テストできる。シーン走査だけが Unity ファイルに残る。

## 4. 実装計画

- 変更対象: 節 3 の新規ファイル、offline の csproj と Program、EditMode テスト。既存 catalog、dispatcher、router、initializer は変更しない。
- 順序:
  1. Unity 無しの DTO、選択、スレッドゲート、登録と JSON。
  2. offline テストをその公開面へ接続する。
  3. Unity 世界と、未実行の EditMode テストを追加する。
- Phase B から Phase A へ差し戻す条件: 節 2 の実装制約で禁止した状態、依存、所有者、寿命、公開 API が無いと最低条件を満たせないとき。失敗したテストの文言調整だけでは戻さない。
- 対象外を維持する方法: 登録メソッドは list と select の2名だけを Register する。set-active 用のメソッドは宣言しない。initializer とプロトコル生成物に diff を出さない。

### 呼び出し契約

`IDebugGameObjectWorld.TryDescribe` と `TryCollectPage` は `DebugGameObjectReadStatus` を返す。

- `Ok`: 行を書けた。
- `NotFound`: スコープ内に生きた id が無い。選択クリアの対象。
- `Unavailable`: シーンを読む権限が無い。選択は維持する。

`TryCollectPage` は destination をクリアしてから、offset を飛ばし、limit 件まで追加する。次の1件を見たら `hasMore` を true にして走査を止める。失敗時は destination を空にする。

`UnityDebugGameObjectWorld` はコンストラクタでスレッドゲートを作る。別スレッドでは Unavailable を返し、シーン API を呼ばない。走査は反復で、ロード済みの有効シーン、root 順、子は sibling 順の preorder。`HideFlags.HideAndDontSave` の部分木は積まない。表示パスは root から `/` で結び、先頭にも `/` を付ける。祖先が 32 段を超える表示は、末尾 32 段の前に `/...` を付ける。256 コード単位の打ち切りはパス組み立てのあとで行う。

`instanceId` の文字列化は `CultureInfo.InvariantCulture` の十進である。

### 応答文言

- list 成功: `Listed GameObjects.`
- select 成功: `Selected GameObject.`
- 生存していない: `GameObject is not alive.`
- payload 不正: `Debug GameObject command payload is invalid.`
- ページ範囲: `Debug GameObject page is out of range.`
- 利用不可: `GameObject inspection is unavailable.`

handler は世界実装の例外を捕らない。catalog の既存契約どおり呼び出し側へ伝播する。

## 5. テストとレビュー計画

- 単体テスト: `dotnet run --project tools/DebugCommandOfflineTests/DebugCommandOfflineTests.csproj`。偽世界で節 2 の受け入れ条件をすべて観測する。既存の catalog offline ケースも同じプロセスで残す。
- 差し戻し中の起点 `-Filter`（C が根拠付きで変更可。受け入れ条件の追加ではない）: `DebugGameObjectCommandTests`
- 判定必須テスト（GO 候補 head。実装変更スライスは最終全 EditMode 回帰が標準）: 起点 filter のあと、空 filter の全 EditMode。標準ランナーは `pwsh tools/run-tests.ps1`。`unity test` は使わない。
- 全 EditMode 回帰の適用除外（理由と代替証拠。無ければ `なし`）: なし。ただし本スライスの着手時点では A2/A3 が無く、Phase B は Unity バッチを実行しない。未実行は GO の根拠にしない。
- 統合・Unity テスト: EditMode の `DebugGameObjectCommandTests` が、テスト内で作った GameObject の id をページから見つけ、select し、`DestroyImmediate` のあと NotFound になることを見る。エディタに他のオブジェクトが居ても、id の完全一致で判定する。DontDestroyOnLoad シーンの実在確認は Player 残りとする。EditMode では `DontDestroyOnLoad` を呼ばない。
- 操作・実行時・目視条件の検証経路: 本スライスの合否は JSON の構造と値である。見た目の判定は無い。offline の標準出力を実装結果へ残す。EditMode の XML は Phase C が `run-tests.ps1` で作ったものだけを証拠にする。
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点: Unity 世界のシーン走査は未確認。初回確認は Phase C。担当は判定 C。確認地点は `DebugGameObjectCommandTests` の実行 XML。ここでテストが 0 件、または対象テストが赤なら NO-GO であり、XML を補完しない。Player 上の DontDestroyOnLoad とソケット往復は S5 と判定 C の残りで、S1 の最低条件を Player 成功に置き換えない。
- 人間の判断が必要な条件と合意した担当・証拠の受け入れ方: なし
- 機械検査: `pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1`
- A0/A1 主担当・モデル・ベンダー: 本セッションの実装担当。A0 の現況は節 1、A1 の境界は節 2 から節 5。
- A2 独立レビューごとの観点・担当・モデル・ベンダー: 未実施。アーキテクチャゲート担当も未実施。複数モデルへ同じ入力を渡していない。
- A3 統合担当・モデル・採否: 未実施。採用・不採用・保留の分類は無い。
- C' 用に予約した担当・モデル・ベンダー: 予約しない。A2 が無い状態で監査担当だけを先に消費しない。
- 独立性の強化条件を満たせない場合の理由: 強化条件の対象となる C' を開始していない。

## 6. Phase B 実装結果

- 実装: `DebugGameObjectCommands.RegisterListAndSelect` が `go.list` と `go.select` だけを既存 catalog へ登録する。選択は `DebugGameObjectSelection` の `ulong` のみ。Unity 走査は `UnityDebugGameObjectWorld` で、呼び出しの finally でシーン参照を外す。既定 dispatcher、initializer、プロトコルは変更していない。
- HANDOFF との差: `DebugGameObjectCommands.cs` は 970 行で、予想の 220 行を超える。payload の読み書きは同じファイルの private nested type に閉じ、公開面は登録メソッドとコマンド名、ページ上限のままである。分割しない理由は、キー順と「不正」と「ページ範囲」の分類が handler と同じ契約で、ファイルを分けても所有者・寿命・依存・テスト境界が変わらないため。ケース本体は予想どおり `GameObjectCommandOffline.cs`（361行）へ移し、`Program.cs` は 200 行のままである。`UnityDebugGameObjectWorld.cs` は 293 行。
- 未実行: EditMode の `DebugGameObjectCommandTests`、空 filter の全 EditMode、Player、ソケット往復、DontDestroyOnLoad シーンの実在確認、Unity Editor のコンパイル確認。結果 XML は作っていない。A2、A3、Phase C、Phase C' は未実施。GO は記録しない。
- 実行した確認: `dotnet run --project tools/DebugCommandOfflineTests/DebugCommandOfflineTests.csproj` は 15 passed, 0 failed, 15 executed。既存 6 件と S1 の 9 件。`pwsh tools/contract-audit.ps1 -BaseRef origin/develop` は errors 0、warnings 0。`pwsh tools/docs-audit.ps1` は本節の追記後に errors 0、warnings 0。
- implementation head commit: `b3c7c514ad93a13036049d6c36b135b378826bf4`。本節だけの後続コミットは implementation head に含めない。
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
