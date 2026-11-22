# DebugStudio S3 — CLI から検証用 GameObject の activeSelf を切り替える

## 0. メタデータ

- type: slice
- status: A（A3 revision 1、構造の限定再レビュー完了。未凍結）
- branch: codex/debugstudio-s3
- implementation base commit: 未固定（候補は develop `d176033a06ebcd1f481aa9082ed5d1053a2a03b2`。凍結時に固定する）
- implementation head commit: 未固定
- A3 凍結記録（承認者・日付・対象 revision・承認の根拠。凍結前は `未凍結`）: 未凍結
- risk: high（理由は §2.6）
- owner: root（依頼者）
- created: 2026-11-20 / updated: 2026-11-22（A3 統合候補）
- expires: 2026-12-31 または置換 revision
- harvest to: `tools/DebugStudio/src/DebugStudio.Cli/README.md`（`set-active` の使い方、target、outcome と exit、不明のときの扱い）、`unity/Assets/Docs/Architecture/12-telemetry.md` §10.5（SampleGame の catalog 配線、`debug.set-active` の契約、`TryResolveHierarchyNode` の Play 照合）
- Phase A snapshot の取得案内・状態: 未生成。owner の凍結後に凍結仕様をローカル固定し、C と C′ に取得案内を渡す。
- Phase B result snapshot / evidence bundle / C' blind bundle の取得案内・状態: 未生成
- 基準動作の Evidence: 116 ファイルをローカル固定（未昇格）。別の Claude CLI セッションが取得し、両 Play の hierarchy・inspect・Editor 独立値、WPF 終了と観測票を閲覧した。取得案内は task の担当者と owner 向け手元引渡し先に保管済み。
- task: `debugstudio-s3`（開始済み）
- A2 の入力版: 計画開始点 `a629f9c19bdfe55b24fcac2f1756e25824d9e453` の A1 初稿（§5.5）。本書は最新 develop `d176033a` で照合し直した統合候補で、A2 の入力版とは別の版である。
- 引継ぎ票: 次は §5.6 の順序。(1) 本書を固定 copy にし、構造観点の限定再レビューを行う。(2) 採否を反映した版を commit し、Draft PR で外部計画レビューを行う。(3) owner の凍結判断と対象 revision を記録して凍結版を commit し、その SHA を票の外で渡す。次担当は Phase B（凍結後）。必要な権限: 再レビューと外部計画レビューは読取りだけ。停止規則: 凍結前に実装・Unity テスト・Build をしない。目的・利用者の 2 欄は変えない。
- 再開位置: §5.6 の手順 (2)。Draft PR で外部計画レビューを受け、その採否とともに owner の凍結判断を待つ。

## 1. 目的と対象外

### 1.1 目的と利用者（目的レビューへ渡した原文。変えていない）

- 目的: Editor の Play 中に、DebugStudio CLI の hierarchy で SampleGame の検証用 GameObject を 1 つ特定し、名前付きコマンドでその activeSelf を切り替え、inspect で結果を読み返せるようにする。Play をまたいで古い指定を使っても別の対象を変更しないこと、切断やタイムアウトで実行されたか分からないときに利用者がそれを成功や失敗と取り違えないことを含む。
- 利用者: SampleGame を Editor で Play し、DebugStudio WPF を起動したまま CLI のコマンドを実行してその出力を読む開発者と AI エージェント。

### 1.2 対象外

- activeSelf 以外の field・component・メソッドへの書き込み（汎用プロパティ編集）。
- Player（Build した実行ファイル）での動作と遠隔接続。
- activeSelf の切り替え以外の操作コマンド。複数対象への一括操作。
- GUI から書き込む操作。WPF の画面の変更。
- inspect の自己応答保証（要求 ID による相関）。S2 の TargetId 一致の best-effort はそのまま。
- 結果不明の後の自動再実行、操作履歴の永続化や復旧の仕組み。
- S2 HANDOFF（`docs/handoff/DEBUGSTUDIO_S2_SIMPLE_A3_CANDIDATE.md`）の Phase D。

### 1.3 現況（develop `d176033a`）

`a629f9c1` から `d176033a` までの差分は Content overlay（S3-4a）と Bootstrap の Editor snapshot 系で、DebugSocket・DebugStudio・protocol・SampleGame の `AppInitializer` に差分は無い。`AbstractApplicationInitializer` の行番号だけが動いた（`ResetForNewPlayerSession` は 169 行、`services.DebugSocket = ...` は 287 行、`CreateDebugCommandDispatcher` は 1262 行）。

コードの事実:

- CLI の `send --command <name> --payload <json>` は、WPF の control（`DebugStudioCliControlService.DispatchCommandAsync`）が `CommandService.SendAsync` で `DebugCommandEnvelopeV1` を Runtime へ送り、同じ requestId の `CommandResult` を待つ。結果は Completed / TimedOut / Failed。Failed は送る前の失敗（未接続など）と送った後の切断を区別しない。CLI の client（`DebugCommandControlPlaneClient`）は、control への接続中の期限切れも応答待ちの期限切れも同じ TimedOut にする。`send` の出力は text で、成功 0、Runtime 失敗と Failed が 1、TimedOut が 2。
- Runtime のコマンド経路: router（`DebugSocketInboundMessageRouter.HandleDebugCommandAsync`）は組込みの `ping` / `runtime-diagnostics` を先に処理し、それ以外は DebugCommand capability の交渉を確かめてから App の dispatcher へ渡す。dispatcher は `MainThreadDebugCommandDispatcher` で包まれ、main thread の文脈が無ければ Success=false を返し、あれば main thread へ切り替えてから中の dispatcher を呼ぶ。
- コマンド登録経路: `AbstractApplicationInitializer.CreateDebugCommandDispatcher()` の既定は `NullDebugCommandDispatcher`。任意の `DebugCommandCatalog`（Ordinal 完全一致、同期実行、handler の例外は伝播）と `CatalogDebugCommandDispatcher` がある。SampleGame は override しておらず配線されていない。dispatcher は DebugSocketService より先に作られる（`new DebugSocketService(options, CreateDebugCommandDispatcher())`）。
- NodeId は `DebugSocketRuntimeNodeRegistry` が service ごとに 1 から採番する。同じ service の中では採番値を戻さない。WPF の接続が替わると対応表は `Reset` され、同じ GameObject に新しい NodeId が付く。DebugSocketService は Play ごとに作り直される。
- Welcome の SessionId は `UnitySessionCorrelationContext.SessionId`（public static）。`BootstrapSubsystemRegistration` が Play ごとに `ResetForNewPlayerSession()` で新しい GUID にする。Log と Telemetry はこの値を producer 側で載せる。hierarchy の snapshot と CLI の hierarchy 出力には SessionId が無い。
- `DebugSocketService` には `NotifyHierarchyChanged()`（public）がある。NodeId から GameObject を引く処理（`TryFindGameObjectByNodeId`）は private。`AbstractApplicationInitializer` には `LoggerFactory` / `Vfx` / `UpdateCoordinator` の protected accessor があるが、DebugSocketService の accessor は無い。
- Inspector は `GameObject.ActiveSelf` と `GameObject.ActiveInHierarchy` を別の property として返す。CLI の inspect 出力の `rawValue` は文字列（`"true"` / `"false"`）。
- `OneStarMaker.Runtime` と `SampleGame.DependOnAll` の asmdef は `overrideReferences: true`。Unity 側で JSON の解析に使えるのは `JsonUtility`。

### 1.4 現行 develop の実機読取り（2026-11-22 観測済み。再実行しない）

別担当が `d176033a` の専用 worktree で観測した（Editor 6000.6.9f1、Release の WPF と CLI。原記録は git 管理外の baseline-r2 の記録）。

- WPF と CLI の Release build は exit 0。WPF を `--listen` で直接起動し、Play 前の `hierarchy` は `no-snapshot`、Play 後に `hasSnapshot: true`。
- 更新した reflection 経路（`_services` の `DebugSocket`）の fixture eval は 2 回の Play とも成功し、fixture が hierarchy に現れた。
- `runtime-diagnostics` の sessionId は Play1 `9c33066a…`、Play2 `e8ff2503…` で異なった。
- **NodeId の再利用（hierarchy の事実）:** Play1 と Play2 のどちらでも、fixture の root が NodeId 8、`Target` が NodeId 9 だった。root の名前（token）は Play ごとに違い、別の GameObject である。
- **旧 NodeId の inspect:** Play2 で Play1 の NodeId 9 を `inspect` すると Ready で、名前 `Target`・ActiveSelf `"true"` の Play2 の GameObject を返した。名前が同じなので、inspect の出力だけからは対象が入れ替わったと分からない。
- **独立値:** 各 Play で Editor の eval が読んだ、その Play の fixture の `Target` の activeSelf は両方 true で、それぞれの Play の inspect と一致した。これは各 Play の中の一致であり、2 つの Play の対象が同じことを示すものではない。
- `send --command debugsocket.set-active --payload '{}'` は `success=false`、`Debug command 'debugsocket.set-active' is not registered.`、exit 1（今は書き込みのコマンドが無い）。
- WPF に通常の終了を要求し、process handle の signal と exit 0 を観測した。Editor は Play 停止・Scene clean・tracked 変更なしで残っている（観測担当が専用 worktree で開いた Editor。人間が開いたものではない）。

### 1.5 要求

- CLI の hierarchy で検証用 GameObject を 1 つ特定し、名前付きコマンドで activeSelf を切り替え、inspect で結果を読み返せる。
- Play をまたいだ古い指定で別の対象を変更しない。
- 切断・タイムアウトで実行有無が分からない結果を、成功・失敗と取り違えない形で返す。
- 既存の hierarchy / send / inspect とコマンド登録経路を起点にする。
- 現行 develop の Release WPF と実 Editor で確かめる。

### 1.6 常時契約と既知の制約（本文へ転記）

- 依存は Game → Framework の一方向。asmdef 参照を追加しない。
- Unity 側の C# で `record` を使わない。新規・編集する Unity 側 `.cs` の先頭に `#nullable enable`。`tools/DebugStudio` の .NET 8 には `record` の禁止を適用しない。
- 破棄されうる `UnityEngine.Object` は `== null` / `!= null` で判定する。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。
- `ZLogger*` 型を公開面へ出さない。Editor コードを Runtime asmdef に置かない。
- path は `tools/path-budget.json` の予算に従う。
- Unity テスト・Build は Phase C。Phase B は Editor での compile 確認まで。Unity 終了時の native stall は Harness README の停止規則に従う。
- S2 の不変条件（`--listen` なしの起動と既存 send / compare の契約、待受け loop は 1 本、inspect は GUI 選択を変えない、CLI / control が期限なく固着しない）を壊さない。
- 人間が開いた Editor を強制終了しない。重複した Editor や WPF を起動しない。

### 1.7 UC ごとの利用者の入力

| UC | 利用者が選ぶ・入れるもの | 機械が決めるもの | 誰がいつ | 目的からの正当化 |
|---|---|---|---|---|
| UC1 対象を特定する（`hierarchy`） | 無し（既定値で使える） | 各 node の `target`（Runtime が snapshot に載せた sessionId と NodeId を CLI が 1 つの文字列にする）、NodeId・名前・Flags | 自動導出 | 「hierarchy で 1 つ特定」。入力を増やさない |
| UC2 切り替える（`set-active`） | `target`（UC1 の 1 行から写す 1 値）、`--active true\|false` | コマンド名 `debug.set-active`、requestId、payload、outcome の分類 | target と値は実行時の操作。コマンド名は CLI の開発時の宣言 | target: 「特定した 1 つ」を、どの Play の hierarchy から来たかと一緒に 1 値で渡す。Play の識別子を別に入力させない（目的レビューの指摘）。値: 反転にすると、不明の後に望む状態へ戻すための再実行で逆に戻りうる。値を明示すれば再実行の結果が望む状態になる |
| UC3 読み返す（`inspect`） | NodeId（既存） | 既存どおり | 実行時の操作 | 「inspect で読み返す」。変更なし |
| UC4 不明のとき | 新しい入力は無い（UC1 から UC2 をやり直す） | CLI は自動で再送しない | 実行時の操作 | 「取り違えない」。§2.1 の 6 |
| SampleGame の開発時の宣言 | 検証用の印（`DebugVerificationTarget` component）を検証用 GameObject に付ける。今回は検証の eval が付ける | — | 検証時 | 「検証用 GameObject」。書き換えられる対象を印の付いたものに限る |

A3 自己点検: 利用者の入力は target・値の 2 つ。A1 初稿にあった `--session` の手入力は target に吸収した。印は製品の Scene に付けず、検証の eval だけが付ける。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 同じ WPF を `--listen` で起動したまま、Editor の Play 中に CLI だけで、hierarchy で特定した検証用 GameObject の activeSelf を名前付きコマンドで切り替えて inspect で読み返せ、Play をまたいだ古い指定では何も変更せず、実行有無が分からない結果を確定した結果と区別して返せるか。
- 次スライスへ進む最低条件: §2.2 の I1〜I5 と E1〜E3 を同じ GO 候補 head で満たす。

### 2.1 統合設計

最小構成として、書き換えの処理と対象の制限は SampleGame がコマンド登録経路で持ち、Framework は「現在の Play でこの指定が指す GameObject を返す」解決だけを公開する。CLI は既存の send の要求経路に薄い入口を足す。

1. **target（対象指定）:**
   - Runtime が `HierarchySnapshotEnvelopeV1` に `SessionId`（string、既定 `""`、追加のみ）を入れる。値は snapshot を作る時点の `UnitySessionCorrelationContext.SessionId`。delta には入れない。
   - WPF の `HierarchyStore` は snapshot の SessionId を nodes・revision と同じ lock で保持し、delta では保ち、`Clear` で空に戻す。`ControlHierarchySnapshotV1` に `SessionId`（追加のみ）を加え、`CliReadService` がそのまま載せる。WPF は SessionId を後付けしない。
   - CLI の `hierarchy` の `data` に `sessionId` を、各 node に `target`（`<sessionId>:<NodeId>`）を加える。sessionId が空なら `target` は null とし、その snapshot から書き込み用の指定を作らない。既存の field と NodeId はそのまま。
2. **Framework の追加（公開面はこの 2 つ）:**
   - `DebugSocketService.TryResolveHierarchyNode(string sessionId, long nodeId, out GameObject? gameObject)` が `DebugSocketNodeLookup`（`Resolved` / `StaleSession` / `NotFound`）を返す。main thread で呼ぶ。最初に `sessionId` を `UnitySessionCorrelationContext.SessionId` と ordinal で比べ、違えば対応表を引かずに `StaleSession`。一致すれば `_gate` の中で既存の対応表を引き、無い・破棄済み・scene が unload なら `NotFound`。
   - `AbstractApplicationInitializer` に `protected DebugSocketService? DebugSocket => _services?.DebugSocket;`（既存の `LoggerFactory` 等と同じ形）。
   - 接続の現行性は照合しない。理由: Play が違えば session の照合で拒否する。同じ Play の中では、接続が替わると対応表が `_gate` の中で `Reset` され、採番値は戻らないので、旧接続の要求の NodeId は同じ GameObject に解決するか `NotFound` になり、別の GameObject には解決しない。
3. **SampleGame（コマンド登録経路）:**
   - `AppInitializer` が `CreateDebugCommandDispatcher()` を override し、`DebugCommandCatalog` を作って `debug.set-active` を登録し、`CatalogDebugCommandDispatcher` を返す。登録は `Application.isEditor` が true のときだけ行う（Player には登録しない）。handler は `() => DebugSocket` で実行時に現在の service を読む。
   - 新規 `DebugVerificationTarget`（中身の無い MonoBehaviour）を検証用の印にする。
   - handler の順序: (1) payload を解析し、不正なら `invalid-payload`。(2) `TryResolveHierarchyNode` を呼び、`StaleSession` なら `stale-session`、`NotFound` なら `target-not-found`。(3) 印が無ければ `not-verification-target`。(4) `SetActive(active)` を呼び、後の `activeSelf` を読み、`NotifyHierarchyChanged()` を呼ぶ。(1)〜(3) の拒否は SetActive より前に決める。SetActive の後の例外は catch せず、router の既存の catch に任せる（構造化 payload の無い Success=false になり、CLI は unknown にする）。
   - 要求 payload: `{"sessionId":"<文字列>","nodeId":"<10 進の正の Int64 文字列>","active":"true"|"false"}`。3 つとも JSON 文字列で、欠落・空・範囲外・`true` / `false` 以外は `invalid-payload`。
   - 結果: 適用は `DebugCommandResult.Ok` に `{"outcome":"applied","reason":"applied","activeSelf":<bool>}`。拒否は Success=false に `{"outcome":"rejected","reason":"<invalid-payload|stale-session|target-not-found|not-verification-target>"}`。`DebugCommandResult.Fail` は payload を持てないので、Success=false と payload を指定できる既存のコンストラクタを使う。
4. **CLI:** `debugstudio-cli set-active <target> --active true|false [--control-uri URI] [--timeout-seconds N]`（timeout は既定 15 秒、0 超〜60 秒）。target を `<sessionId>:<NodeId>` として検査し、合わなければ usage。既存の `DebugCommandControlPlaneClient` で 1 回だけ送る。client の結果に「control への WebSocket 接続が確立したか」を追加の property として持たせる（send の出力は変えない）。JSON 1 object を stdout に出す: `status`（`ok` / `error`）、`outcome`、`reason`、`data`（requestId、target、`applied` なら activeSelf、失敗時の detail）。

   | 観測 | outcome | reason | exit |
   |---|---|---|---|
   | Completed、Success=true、payload が `applied` | applied | applied | 0 |
   | Completed、Success=false、payload が `rejected` | rejected | handler の reason | 1 |
   | control への接続が確立する前に終わった（拒否、接続中の期限切れ、接続中の例外） | not-sent | control-unreachable | 1 |
   | 接続確立後の期限切れ（WPF の TimedOut か CLI 自身の期限） | unknown | timeout | 3 |
   | 接続確立後の Failed、応答前の切断、例外 | unknown | no-result | 3 |
   | Completed だが payload が無い・解釈できない・Success と食い違う（未登録の応答を含む） | unknown | unstructured-result | 3 |
   | 引数の誤り | — | usage | 2 |

   接続中の期限切れの合格例: 接続確立前に CLI の期限が切れた → `not-sent` / exit 1。不合格例: それを `unknown` / exit 3 にする、または確立後の期限切れを `not-sent` にする。
5. **応答の扱い:** CLI は 1 回の実行で 1 回だけ送り、自動で再送しない。期限切れの後に届いた応答で返した結果を変えない。WPF の control service は変えない（WPF の送る前の失敗は CLI からは区別できないので unknown にする）。
6. **不明のときの扱い（README と usage に書く）:** `unknown` は、その要求が適用されたかどうかが分からず、後から確定しないことを意味する。後の `inspect` や `hierarchy` が示すのは読んだ時点の現在値で、その要求が実行されたかどうかの証拠にはならない（他の要求、GUI、ゲームの処理でも値は変わる）。望む状態にしたいときは、`hierarchy` を読み直し、その出力の新しい target で `set-active` を実行する。その結果はその要求のことだけを表す。前の target を再利用しない（同じ Play でも WPF が再接続すると `target-not-found` になる。Play が替わっていれば `stale-session` になる）。

**比べた代替と採らない理由:**

- DebugSocket の組込みコマンド（A1 初稿）: Framework の Runtime に App の allowlist を迂回する初めての書き換えを足し、対象が loaded scenes の全 GameObject になる。目的の「SampleGame の検証用 GameObject」より広く、依頼の「コマンド登録経路を起点」からも外れる（構造レビュー F3）。
- SampleGame が名前やパスで対象を探す: hierarchy の特定と結び付かず、名前は識別の保証にならない（実機でも Play2 に同じ名前の `Target` があった）。
- sessionId を別の引数で入力させる（A1 初稿）: 目的レビューが作らないものに挙げた手入力で、sessionId と NodeId を別々の出力から組み合わせる誤り（構造レビュー F1）を招く。
- NodeId を Play 間で衝突しない採番にする: 保証が確率的になり、GUI と inspect を含む全 NodeId の値が変わる。
- 既存の `send` だけで済ませる: send の exit は「確定した拒否」と「結果不明」を同じ 1 にし、S2 で出力・exit を変えない契約がある。新しいサブコマンドは send の要求経路への薄い入口に留める。
- WPF→Runtime の境界でも送る前と後を分ける（代替レビュー）: control の応答 DTO と control service の変更が要る。保守的に unknown とすれば目的は満たせる。後続へ送る。
- 同じ Play の再接続で前の target を再利用できるようにする（代替レビュー）: 対応表の `Reset` を変える必要があり、安全側の失効と hierarchy の読み直しで足りる。
- 反転（toggle）にする（代替レビュー）: 入力は 1 つ減るが、不明の後に望む状態へ戻すための再実行で逆に戻りうる。

### 2.2 進める最低条件

#### 不変条件

- **I1: hierarchy が出した target は、その Play の中でしか GameObject に解決されない。** Play が違う、または指定が失効した target による要求では、どの GameObject の activeSelf も変わらない。
  - 主張を崩しうる既存の経路: (a) NodeId の採番が service ごとに 1 から始まり、Play が替わると同じ NodeId が別の GameObject を指す（実機で NodeId 8・9 の再利用を観測）。(b) WPF は切断後も hierarchy の保持値を残す。(c) SessionId が Play 間で変わらない場合（`ResetForNewPlayerSession` は Play ごとに呼ばれ、domain reload では static が消えて新しい GUID になる）。(d) 同じ Play の中で DebugSocketService が作り直される場合（製品の経路で `InitializeBeforeSceneLoad` を呼ぶのは SampleGame の BeforeSceneLoad 入口だけ）。(e) DebugCommand の経路は main thread へ切り替えた後に現在の接続を確かめない（§2.1 の 2 の理由で I1 は崩れない）。(f) WPF が SessionId を受信時に後付けすると、旧 nodes に新しい SessionId が付きうる（producer が載せて経路を無くす）。(g) 旧 Play の要求が新しい Play の main thread で実行される（session の照合で拒否）。(h) 利用者が別々の hierarchy 出力から sessionId と NodeId を手で組み合わせた target を作る（target を 1 値にして手順から無くす。手で組み立てた target は、Runtime が NodeId の世代を持たないので区別できない。保証の範囲外として README に書く）。
  - 探索した境界: Runtime の DebugSocketServices（router、service の Session / Inbound / Hierarchy / Inspector、registry、publisher、dispatcher 群）、Bootstrap の SubsystemRegistration と BeforeSceneLoad、`UnitySessionCorrelationContext`、`DebugCommandCatalog`、WPF の SessionMessageRouter・HierarchyStore・SessionResetPolicy・control service、CLI の parser と client。
  - 未確認: なし（実機の前提は §1.4 で観測済み）。
- **I2: 実行有無が分からない結果を、適用済みや未適用として報告しない。** CLI は構造化された結果を受け取ったときだけ `applied` / `rejected` を出し、control への接続が確立する前に終わったときだけ `not-sent` とする。それ以外はすべて `unknown`（exit 3）で、自動で再送しない。
  - 主張を崩しうる既存の経路: Failed が送る前と後を区別しない。client が接続中と応答待ちの期限切れを同じ TimedOut にする。router の catch は SetActive の後の例外も Success=false（payload なし）にする。`MainThreadDebugCommandDispatcher` は文脈が無いと Success=false を返す。catalog は未登録を Success=false（payload なし）で返す。期限切れの後に届いた CommandResult は CommandStore に入るが CLI へは返らない。
  - 探索した境界: CLI の client と結果処理、WPF の control service と TransportCommandSender、Runtime の router・dispatcher 群・catalog。
- **I3: 既存の契約を変えない。** `--listen` なしの起動、`send` / `compare` の出力と exit、`hierarchy` / `inspect` の既存 field と exit、GUI の動作、S2 の不変条件。変更は field・サブコマンド・property の追加だけ。
  - 主張を崩しうる既存の経路: `CliArgumentParser.Parse` の先頭分岐、`ReadCommand.Serialize`、MessagePack の key 番号と既定値、`Program.WriteResult`、hierarchy の NDJSON export（出力を変えない）。
  - 既知の観測できる変化: SampleGame で未登録の名前を送ったときのメッセージが、`NullDebugCommandDispatcher` の文言から catalog の `Debug command is not registered.` に変わる（Success=false と exit は同じ）。文言は契約ではない。
- **I4: 検証用の印の無い GameObject の activeSelf は変えない。**
  - 主張を崩しうる経路: target は loaded scenes のどの GameObject も指しうる。GUI のコマンド画面や `send` から同じコマンド名を任意の payload で送れる（どの経路でも handler の印の検査を通る）。
- **I5: Editor 以外ではコマンドを登録しない。**
  - 主張を崩しうる経路: DebugSocket を有効にした Player Build でも `CreateDebugCommandDispatcher()` は呼ばれる。

#### 成立条件

- **E1:** Release の WPF を `--listen` で起動し、Editor の Play 中に、CLI の `hierarchy` の target で `set-active` を実行すると、印の付いた検証用 GameObject の activeSelf が指定した値（false と true の両方向）になり、CLI の `activeSelf`、Editor で独立に読んだ activeSelf、`inspect` の `GameObject.ActiveSelf` の rawValue（`"true"` / `"false"`）が一致する。
- **E2:** 同じ WPF プロセスのまま Play を停止・再開した後、新しい `hierarchy` の target で E1 がもう一度成り立つ。
- **E3:** 旧 Play の target、現在の hierarchy に無い NodeId、印の無い GameObject、不正な引数・payload、接続できない control、期限切れ、応答前の切断、構造化されていない応答が、それぞれ §2.1 の表の outcome・reason・exit で返る。

### 2.3 受け入れ条件

- **AC1（I3）:** 既存の .NET 回帰が通る（send 成功・失敗・timeout の出力と exit、compare、hierarchy / inspect の既存 field と exit、usage 2）。hierarchy の JSON に `sessionId` と各 node の `target` が加わり、既存 field は変わらない。Contracts の roundtrip で `HierarchySnapshotEnvelopeV1.SessionId` と `ControlHierarchySnapshotV1.SessionId` が往復し、field を持たない payload は `""` で読める。`pwsh tools/protocol-codegen/generate.ps1 --check` が一致する。
- **AC2（I1、失敗経路）:** Unity EditMode:
  - `TryResolveHierarchyNode` は、sessionId が現在と違えば、同じ NodeId が対応表にあっても `StaleSession` を返し、GameObject を返さない。
  - 対応表に無い NodeId、破棄した GameObject の NodeId は `NotFound`。
  - 対応表の `Reset` の後、Reset 前の NodeId は `NotFound` で、同じ GameObject に付いた新しい NodeId は `Resolved`。
  - handler は `stale-session` / `target-not-found` で対象の activeSelf を変えない。
  - 実 Editor（§5.3 V6）: Play2 で Play1 の target による `set-active` が `rejected` / `stale-session`（exit 1）で、Play2 の `Target`（Play1 と同じ NodeId の場合を含む）の activeSelf が Editor で独立に読んで変わっていない。合格例: Play2 で Play1 の target を使い、Play2 に同じ NodeId の印付き `Target` があっても `stale-session` で変わらない。不合格例: `target-not-found` が返った（照合の順序が逆で I1 を判定できていない）、または Play2 の `Target` が変わった。
- **AC3（I2、E3）:** .NET の CLI テストで、偽の client 結果ごとに §2.1 の表の outcome・reason・exit になる（接続中の期限切れの合格例・不合格例を含む）。どの場合も送信は 1 回。Success=true で payload が `rejected`、Success=false で payload が `applied`、Success=false で payload なし（未登録の応答）は `unknown` / `unstructured-result`。client のテストで、接続前の期限切れと接続後の期限切れで「接続が確立したか」が分かれる。
- **AC4（I1、target の出所）:** Unity EditMode: snapshot の `SessionId` が `UnitySessionCorrelationContext.SessionId` と同じ。.NET: `HierarchyStore` が snapshot の SessionId を保持し、delta の後も保ち、`Clear` で空に戻り、`GetRetainedSnapshot` が nodes・revision と一緒に返す。`CliReadService` の応答に載る。CLI の hierarchy は sessionId が空なら各 node の `target` を null にする。
- **AC5（E1、E2）:** 実 Editor（§5.3 V0〜V9）で、Play1 と Play2 のそれぞれで印付きの `Target` を false にしてから true に戻し、各回で CLI の `applied` と `activeSelf`、Editor で独立に読んだ値、`inspect` の `GameObject.ActiveSelf` の rawValue が一致する。hierarchy を読み直すと対象 node の ActiveSelf flag も変更後の値になる。現在の hierarchy に無い正の Int64 の NodeId で作った target は `target-not-found`（exit 1）。WPF のプロセスは 2 回の Play を通して同じで、GUI のボタンを押さない。合格例: Play2 の 2 回目の切り替えまで同じ PID で成立。不合格例: Play2 を WPF の再起動後に確かめた。
- **AC6（I4）:** Unity EditMode: 印の無い GameObject への要求は `not-verification-target` で activeSelf が変わらない。実 Editor（§5.3 V5）: fixture の root（印なし）の target で `not-verification-target`（exit 1）、root の activeSelf が変わらない。
- **AC7（I5）:** Unity EditMode: 登録の処理に Editor でないことを渡すと catalog に `debug.set-active` が無く、Editor なら有る。

### 2.4 ここでは答えない問いと所有する後続スライス

- 汎用プロパティ編集、別の操作コマンド、GUI からの書き込み、複数対象: DebugStudio S4 以降（未起票）
- Player と遠隔での書き込み: DebugStudio S4 以降（未起票）
- inspect の自己応答保証: DebugStudio S4 以降（未起票）
- WPF→Runtime の境界で送る前の失敗を未送信と確定すること: DebugStudio S4 以降（未起票）
- NodeId に Play の世代を持たせ、手で組み立てた target も区別すること: DebugStudio S4 以降（未起票）
- S2 から移送された client / server の timeout 起点と client 離脱後の占有: DebugStudio S4 以降（未起票）
- S2 HANDOFF の Phase D: S2 の担当

### 2.5 スライス分割の判断

- 契機の確認: (1) 変更理由の混在: target の出所（wire と WPF）、解決（Framework）、書き換えと制限（SampleGame）、分類（CLI）は、どれもこの問いに必要で、どれか一つでは答えられない。(2) 独立に失敗しうる未知: S2 の読取りと検証の eval は §1.4 で成立を観測した。残る未知は新しい書き込み経路だけ。(3) 別の問いの受け入れ条件: 混じっていない。
- 判断: 分割しない。

### 2.6 risk と判定

- risk: high。理由: Framework の公開 API（`TryResolveHierarchyNode`、`DebugSocketNodeLookup`、protected `DebugSocket`）と、Unity・DebugStudio 両 surface の wire field を足す。Play をまたいだ照合と、main thread の切り替え・接続の差し替えの競合が主張の中心にある。
- 判定定義: GO は I1〜I5 と E1〜E3 を同じ GO 候補 head で満たすこと。NO-GO はいずれかの未達。CONDITIONAL ACCEPT は使わない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし

### 2.7 本文へ転記した実装制約

- §1.6 の常時契約と既知の制約。
- asmdef 参照と package を追加しない。Unity 側の payload 解析は `JsonUtility` か手書き。
- Framework の公開面の追加は §2.1 の 2 の 2 つと wire field だけ。router、組込みコマンド、`MainThreadDebugCommandDispatcher`、`DebugCommandCatalog` を変えない。
- 既存の send / compare / hierarchy / inspect の出力・exit を変えない。追加は field・サブコマンド・property だけ。
- WPF の control service、AutoListen、終了経路、GUI を変えない。
- SampleGame の製品の Scene・設定・Prefab を変えない。印は検証の eval だけが付ける。
- 拒否は SetActive より前に決める。SetActive の後の例外を `rejected` にしない。
- 書き込みを再現するための遅延・切断の操作を製品の CLI に足さない。

### 2.8 人間が凍結前に判断すること

- 書き換えの置き場所を SampleGame のコマンド登録経路とし、Framework の公開 API を 2 つ足すこと（A1 初稿の組込みコマンドからの変更）。
- 書き換えの対象を検証用の印（`DebugVerificationTarget`）の付いた GameObject に限ること。
- 手で組み立てた target（別々の出力の sessionId と NodeId の組み合わせ）を保証の範囲外とし、後続へ送ること。
- WPF→Runtime の境界の送る前の失敗を unknown に含めること。

## 3. 責務マップ

行数は `d176033a` の現在値。増分は見込み。

| file | 責務・変更理由 | 所有者・寿命 | 依存・公開面 | テスト境界 | 現在 / 増分 |
|---|---|---|---|---|---|
| `unity/Assets/OneStarMaker/Scripts/Runtime/DebugSocketServices/DebugSocketService.Hierarchy.cs` | `TryResolveHierarchyNode`（session の照合、`_gate` の中の対応表の参照） | DebugSocketService（Play の寿命） | public method を 1 つ追加 | EditMode（service か registry を使う） | 87 / +25〜40 |
| `unity/.../DebugSocketServices/DebugSocketNodeLookup.cs`（新規） | 解決結果の enum | — | public enum | — | 新規 10〜20 |
| `unity/.../DebugSocketServices/Hierarchy/DebugSocketHierarchyPublisher.cs` | snapshot に SessionId を入れる | service | Foundation の `UnitySessionCorrelationContext` | 既存の publisher テストに追加 | 320 / +2〜5 |
| `unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs` | protected `DebugSocket` accessor | initializer（App） | protected property | — | 1305 / +3〜6 |
| `protocol/debugsocket/envelopes/hierarchy_snapshot_envelope_v1.yaml`、`control_hierarchy_snapshot_v1.yaml` と codegen 出力 | `SessionId` の追加 | protocol 正本 | 両側の生成型 | codegen --check、両側の roundtrip | 各 +1 と生成差分 |
| `unity/Assets/SampleGame/DependOnAll/DebugCommands/SetActiveDebugCommand.cs`（新規、internal static） | 登録（Editor のときだけ）、payload の解析、解決・印の検査・SetActive、結果 JSON | SampleGame（catalog は Play ごとに作る。closure は App 寿命の initializer だけを持つ） | Runtime の catalog・service。internal | 解決を delegate で差し替えて EditMode | 新規 100〜160 |
| `unity/Assets/SampleGame/DependOnAll/DebugCommands/DebugVerificationTarget.cs`（新規） | 検証用の印 | GameObject | public MonoBehaviour | — | 新規 8〜15 |
| `unity/Assets/SampleGame/DependOnAll/AppInitializer.cs` | `CreateDebugCommandDispatcher()` の override | initializer | — | — | 431 / +8〜14 |
| `tools/DebugStudio/src/DebugStudio.App/Core/Stores/HierarchyStore.cs`、`HierarchyStoreSnapshot.cs` | SessionId の保持・delta で保つ・Clear | WPF プロセス | — | HierarchyInspectorStoreTests | 189 / +8〜15、14 / +1〜2 |
| `.../Core/Services/CliReadService.cs` | 応答に SessionId | WPF | — | CliReadServiceTests | 119 / +1〜3 |
| `tools/DebugStudio/src/DebugStudio.Client/DebugCommandControlPlaneClient.cs`、`DebugCommandRoundtripResult.cs` | 接続が確立したかを結果に持たせる | CLI 要求ごと | 公開型に property を追加 | DebugCommandRoundtripClientTests | 148 / +5〜12、47 / +3〜8 |
| `tools/DebugStudio/src/DebugStudio.Cli/SetActiveCommand.cs`（新規） | target の検査、payload、送信、分類、JSON | CLI プロセス | Client | 送信関数を注入して単体テスト | 新規 90〜150 |
| `.../DebugStudio.Cli/CliArgumentParser.cs`、`Program.cs`、`ReadCommand.cs` | 解析と usage、入口、hierarchy の `sessionId` と `target` | CLI | — | CliArgumentParserTests、ReadCommandTests | 216 / +30〜50、71 / +3〜6、45 / +3〜8 |
| `tools/DebugStudio/src/DebugStudio.Cli/README.md`、`unity/Assets/Docs/Architecture/12-telemetry.md` | 使い方と契約。§10.5 の「SampleGame に catalog が配線されているという意味ではない」を現状に直す | 公開文書 | — | docs-audit | 91 / +20〜35、515 / +10〜20 |

規模の警報: `AbstractApplicationInitializer` は既に 500 行を超えるが、既存の accessor と同じ形の 1 行を足すだけで責務を増やさないので分割しない。他に 500 行・3 責務・50% 増に当たる file は無い。SampleGame の書き換えは 1 つの static class に置き、判定と Unity 操作を分ける層を作らない（実装が 1 つの段階で port を作らない）。

新しい中核ロジックの単体テスト: 解決は Framework の EditMode、handler は SampleGame.Tests の EditMode（解決を delegate で差し替え、実 GameObject で印と SetActive を確かめる）、分類は CLI の .NET テスト。

## 4. 実装計画

- 順序: (1) protocol yaml と codegen。(2) Framework: publisher の SessionId、`TryResolveHierarchyNode` と enum、protected accessor、EditMode テスト。(3) SampleGame: 印、handler、override、EditMode テスト。Editor で compile を確かめる。(4) WPF の HierarchyStore と CliReadService。(5) Client と CLI。(6) README と §10.5。
- Phase B から Phase A へ差し戻す条件: asmdef 参照・package が要る。§2.1 の 2 以外の Framework の公開 API が要る。router・組込みコマンド・catalog・control service・AutoListen・WPF の終了経路を変える必要がある。wire の契約（§2.1 の 1 と 3 の payload・結果）を変える必要がある。SampleGame の製品の Scene・設定を変える必要がある。
- 対象外を維持する方法: 汎用の property 書き込みや別のコマンドの土台を作らない。GUI に操作を足さない。

## 5. テストとレビュー計画

### 5.1 テスト

- 単体テスト: §3 の各テスト境界。
- 差し戻し中の起点 `-Filter`（C が根拠付きで変更可）:
  - Unity EditMode: `TryResolveHierarchyNode` のテスト、`DebugSocketHierarchyPublisherTests`、`DebugSocketRuntimeNodeRegistryTests`、`DebugSocketProtocolRoundtripTests`、SampleGame の `SetActiveDebugCommandTests`。
  - .NET: CLI の `SetActiveCommandTests`・`CliArgumentParserTests`・`ReadCommandTests`・`DebugCommandRoundtripClientTests`、App の `HierarchyInspectorStoreTests`・`CliReadServiceTests`・`CliControlPlaneRoundtripTests`、Contracts の `ProtocolRoundtripTests`・`ControlReadRoundtripTests`・`ProtocolGoldenCrossContractTests`。
- 判定必須テスト（GO 候補 head）: .NET の App・CLI・Contracts の各 test project の全件（Release）、`pwsh tools/protocol-codegen/generate.ps1 --check`、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、最終の全 EditMode 回帰（標準の `run-tests.ps1`。VFX の Inconclusive で exit が 1 になる既知事象があるため、合否は XML の件数と失敗 0 で判定する）、§5.3 の実機手順。
- 全 EditMode 回帰の適用除外: なし。PlayMode は要しない。
- 人間の判断が必要な条件: なし。高コスト証拠: なし（Player・別 host・R2 を使わない）。

### 5.2 未知の操作経路

- 既存の読取り経路と検証の eval: §1.4 で成立を観測した。
- 新しい書き込み経路（§5.3 の V3 以降）: 未確認。初回確認は Phase C。理由: 未実装。担当: 判定 C の実行担当。確認地点: AC5・AC2・AC6 の実機部分。不成立時は、待受け・接続、eval、登録、解決、CLI の分類のどこが原因かを分けて B へ戻し、受け入れ条件を黙って減らさない。B の出口の動作確認で同じ手順を 1 回軽く実行してよい（B の観測は判定の入力にしない）。

### 5.3 実機検証の手順（Phase C。V0〜V9）

GO 候補 head で判定 C の実行担当が行う。各 wrapper の呼出しは別の command にする。Editor が開いていればその所有者を確かめ、重複して起動しない。

- V0: HEAD・clean・Editor の版を記録する。`dotnet build tools/DebugStudio/src/DebugStudio.App/DebugStudio.App.csproj -c Release` と `dotnet build tools/DebugStudio/src/DebugStudio.Cli/DebugStudio.Cli.csproj -c Release`。App の Release 出力の exe を `--listen` で直接起動して PID を記録する。CLI は Release 出力の `debugstudio-cli.exe` を使う。
- V1: Play1 を開始する（`tools/unity-editor.cmd command` で公開名を確かめ、無ければ eval で `UnityEditor.EditorApplication.isPlaying = true;`）。`hierarchy` を `hasSnapshot: true` まで読み直す。
- V2: 次の C# を UTF-8 Base64 にして `tools/unity-editor.cmd eval <Base64>` に 1 引数で渡す。`hierarchy` を token の root と `Target` が現れるまで読み直し（総計 15 秒以内）、`Target` の target（T1）と NodeId、root の target（R1）を記録する。

  ```csharp
  if (!UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Play required");
  var flags = System.Reflection.BindingFlags.NonPublic;
  var initializer = typeof(SampleGame.DependOnAll.AppInitializer)
      .GetField("s_instance", flags | System.Reflection.BindingFlags.Static).GetValue(null);
  var services = typeof(OneStarMaker.Runtime.AbstractApplicationInitializer)
      .GetField("_services", flags | System.Reflection.BindingFlags.Instance).GetValue(initializer);
  if (services == null) throw new System.InvalidOperationException("services missing");
  var service = (OneStarMaker.Runtime.DebugSocketServices.DebugSocketService)services.GetType()
      .GetProperty("DebugSocket", flags | System.Reflection.BindingFlags.Instance).GetValue(services);
  if (service == null) throw new System.InvalidOperationException("DebugSocket service missing");
  var token = "S3Probe_" + System.Guid.NewGuid().ToString("N");
  var root = new UnityEngine.GameObject(token);
  var target = new UnityEngine.GameObject("Target");
  target.transform.SetParent(root.transform, false);
  target.AddComponent<SampleGame.DependOnAll.DebugCommands.DebugVerificationTarget>();
  service.NotifyHierarchyChanged();
  return token;
  ```

  Editor の独立値は別の eval で読む: `return UnityEngine.GameObject.Find("<token>").transform.Find("Target").gameObject.activeSelf;`（非アクティブでも引けるよう `Transform.Find` を使う）。root は `GameObject.Find("<token>").activeSelf`。
- V3: `set-active T1 --active false` → `applied`、`activeSelf: false`、exit 0 を期待する。Editor の独立値、`inspect <NodeId>` の `GameObject.ActiveSelf` の rawValue、読み直した `hierarchy` の対象 node の ActiveSelf flag を記録する。
- V4: `set-active T1 --active true` → 同様に true 側を記録する。
- V5: `set-active R1 --active false` → `rejected` / `not-verification-target`、exit 1 を期待し、root の独立値が true のままか記録する。現在の hierarchy に無い正の Int64 の NodeId で作った target（T1 の sessionId と組み合わせる）で `set-active` → `rejected` / `target-not-found`、exit 1。
- V6: Play を停止し、Play2 を開始する（WPF は同じ、GUI のボタンを押さない）。V2 の eval で新しい fixture を作り、`hierarchy` から Play2 の `Target` の target（T2）と NodeId を記録する。Play1 の `Target` の NodeId が Play2 のどの node にあるかを記録する。Play2 の `Target` の独立値を読み、`set-active T1 --active false` → `rejected` / `stale-session`、exit 1 を期待し、Play2 の `Target`（T1 と同じ NodeId の node があればその GameObject も）の独立値が変わっていないことを記録する。
- V7: Play2 で V3・V4 を T2 で繰り返す（E2）。
- V8: Play を停止する。
- V9: WPF に通常の終了を要求し（`CloseMainWindow`）、process handle の signal と実終了コードを記録する。強制終了した場合はそのとおり記録し、正常終了とは扱わない。Editor の Scene が clean か、tracked 変更が無いかを記録する。
- 保存するもの: 各段のコマンド、stdout / stderr / exit、HEAD、Editor の版、WPF の PID と終了コード、各 Play の hierarchy、各 `set-active` の JSON、Editor の独立値。取得時刻は診断情報で、相関の保証ではない。不明（unknown）の実機再現は求めない（AC3 の offline で判定する）。

### 5.4 判定 C の照合方式

- 照合 script を使わず、判定 C が原記録を直接照合する。理由: 照合する値は 2 回の Play で十数個（各 `set-active` の outcome・reason・activeSelf・exit、inspect の rawValue、Editor の独立値、hierarchy の flag）で、どれも等値の比較で閉じる。script を使う場合に規定された実行環境（資格情報・本番設定・通信から遮断し、偽 transport を注入できる環境）を判定 C 用に用意する経路が現時点で無く、用意する理由も無い。判定 C は、条件ごとに原記録の該当箇所と期待（§2.1 の表、Editor の独立値）を対にした表を判定記録に残す。期待は実装からではなく、Editor の独立値と §2.1 の表から導く。
- 機械検査: codegen --check、contract-audit、docs-audit。

### 5.5 A2 の独立レビューと A3 の採否

**入力版と担当（実績）:**

| 観点 | 入力版 | 担当・モデル・ベンダー | 備考 |
|---|---|---|---|
| A0/A1 主担当 | — | Claude Opus 5.5 High（Anthropic） | A3 統合も同じ担当 |
| 目的レビュー | §1.1 の 2 欄の原文（sha256 `1699e1dd34d533f41442a7fd971e488f48665f0af7e0aeab49b195e6079f0196`。統合担当が入力 file から再計算して一致を確認） | GPT-6.2-sol High（OpenAI、新規セッション） | 初稿・A0 を読む前に回答 |
| 高リスク代替レビュー | A0 固定 copy（SHA-256 `67647af6420f3fe123f8c280549b8f7a130aaaa2e236b6bf8f3bbee91ec435ab`。担当が一致を報告） | GPT-6.2-sol High（OpenAI、目的レビューの回答保存後に同じ担当へ A0 だけを渡した） | 初稿・コードは読んでいないと報告 |
| アーキテクチャゲート（構造） | 固定 base `a629f9c1` と A1 copy（SHA-256 `474ad1e9a9c2e8614654a48b3d4be72a19a52736335aa0fe6861d6099fa1cfc1`） | Grok 4.6 High（xAI、cursor-agent の ask モード） | shell が拒否されたため、checkout の HEAD は worktree の HEAD file を直接読んで確かめ、clean と A1 copy の hash は送信側の計測記録を読んだだけで再計算していない |

**目的レビューの採否:**

| 指摘 | 採否 | 反映 |
|---|---|---|
| hierarchy 出力の指定で一意に特定し、CLI で完結する | 採用 | target（§2.1 の 1） |
| activeSelf を切り替え、非アクティブ後も同じ対象を読み返す。activeInHierarchy と取り違えない | 採用 | 結果は activeSelf だけを返す。AC は `GameObject.ActiveSelf` を名指しで照合 |
| 変更時点で Play と対象の同一性を確かめ、再利用・同名で別対象に解決しない。照合できなければ拒否 | 採用 | I1、`TryResolveHierarchyNode` |
| 確定・拒否・不明を区別し、不明後の inspect は過去の実行の証明にしない | 採用 | I2、§2.1 の 6。A1 初稿の「再実行の `previousActiveSelf` で前回が効いたかを読める」を削除 |
| Play ID などを手入力させない | 採用 | `--session` を廃止し target 1 値に |
| 名前・パスの推測、一括、任意プロパティ、シーン編集を作らない | 採用 | §1.2、§2.7 |
| 自動再実行・永続履歴・復旧を作らない | 採用 | §2.1 の 5、§1.2 |

**代替レビューの採否:**

| 指摘 | 採否 | 理由・反映 |
|---|---|---|
| 識別を Runtime で確定し、コピー可能な対象指定を出す。SessionId の無い snapshot から書き込み用の指定を作らない | 採用 | §2.1 の 1 |
| 同じ Play の再接続では同じ対象を照合できる要求に限り再利用できる | 不採用 | 対応表の `Reset` を変える必要がある。失効と hierarchy の読み直しで足りる |
| 操作の意味と対象の選定は SampleGame が既存の登録経路で持ち、Framework は照合と解決を持つ | 採用 | §2.1 の 2・3。接続の現行性の照合は §2.1 の 2 の理由で入れない |
| 反転（toggle） | 不採用 | 不明の後の再実行で逆に戻りうる。値を明示する |
| WPF→Runtime と CLI→WPF の両境界で未実行と不明を分ける | 一部採用 | CLI→WPF だけ（接続確立前）。WPF 側は保守的に unknown とし後続へ |
| 専用の CLI 名を send の要求経路への薄い入口にする | 採用 | §2.1 の 4 |
| 失敗順序 1〜4（古い snapshot と新しい Welcome、main thread への引渡し中の切断・Play 終了、送信失敗の位置、変更後の返信消失） | 採用 | I1 (b)(f)(g)、I2、AC2・AC3 |
| Player は登録を Editor に限る | 採用 | I5、AC7 |

**構造レビューの採否:**

| 指摘 | 採否 | 反映 |
|---|---|---|
| F1 sessionId と NodeId の異世代の組み合わせで I1 が破れる | 採用 | target を 1 値にした。手で組み立てた target は保証の範囲外と明記し（I1 (h)）、NodeId の世代は後続へ |
| F2 接続確立前の期限切れの分類が二重 | 採用 | 接続確立前は not-sent、確立後は unknown。合格例・不合格例と AC3 の client テスト |
| F3 目的の範囲と Framework の書き換え面が合わない | 採用 | 組込みをやめ、SampleGame の登録経路と印へ |
| F4 予約 prefix と分岐規則の不一致 | 解消 | 組込みをやめたので予約 prefix は不要。catalog の Ordinal 完全一致をそのまま使う |
| F5 不明の後の再実行手順が同じ Play の再接続と噛み合わない | 採用 | 不明の後は hierarchy を読み直した新しい target で再実行する。`target-not-found` は指定の失効を含むと README に書く |
| F6 main thread の文脈の検査が無い、Editor 判定の配置 | 解消 | 登録経路の `MainThreadDebugCommandDispatcher` の既存の検査を使う。Editor 判定は SampleGame の登録時 |

### 5.6 再レビューと凍結までの順序

- 目的レビュー: やり直さない。§1.1 の 2 欄を変えていない。
- 代替レビュー: やり直さない。主要な提案を採用した。
- 構造（アーキテクチャゲート）: 限定して再レビューする。理由: 置き場所（組込み → SampleGame の登録経路）、Framework の公開面、所有者、target の形が変わった。範囲は §2.1、§2.2、§2.3、§3 と、この版で足した Framework の公開 API。入力は本書の固定 copy と固定 base `d176033a`。
- 順序: (1) 本書の固定 copy と SHA-256 を作り、構造の限定再レビューを行う。(2) 採否を反映した版を commit し、Draft PR で外部計画レビューを行う（公開後の修正は 1 巡につき原則 1 commit。外部指摘の採否は owner が裁定し、影響する A2 観点の再レビューの要否と理由を記録する）。(3) owner の凍結判断・対象 revision・次担当 B を §0 に記録し、凍結版を commit する。implementation base は凍結時の base に固定する。
- C' の予約: Phase A で Anthropic、OpenAI、xAI を使った。C' は Phase A〜C に関与しない系列・ベンダーを優先する。C' に Grok 系のモデルを使う場合は、モデルが Phase A の構造レビューと違っても同じ系列なので `独立性制約あり` と記録する（Phase C に Grok 系を使った場合も同じ）。

**構造の限定再レビュー実績。** 新規 cursor-agent セッション（Grok 4.6 High）が現行 base の §2.1〜§2.3・§3 と該当コードを確認した。SampleGame の既存登録口と Framework の解決責務の分離、Play をまたぐ指定の拒否に対する阻害指摘はなかった。受領プロセスが起動直前に clean・base・固定 copy の SHA-256 を照合した。レビュー担当は HEAD を直接読み、ask モードの制約により hash を自分では再計算していない。実機基準観測の収集と構造再レビューは別の担当が行った。

## 6. Phase B 実装結果

未到達。

## 7. Phase C

未到達。

## 8. Phase C'

未到達。

## 9. Phase D

未到達。
