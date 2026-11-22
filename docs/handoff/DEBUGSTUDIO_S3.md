# DebugStudio S3 — CLI から検証用 GameObject の activeSelf を切り替える

## 0. メタデータ

- type: slice
- status: A（A3 revision 2 候補。外部計画レビュー 1 件の反映案。owner の裁定待ち。未凍結）
- branch: codex/debugstudio-s3
- implementation base commit: 未固定（候補は develop `d176033a06ebcd1f481aa9082ed5d1053a2a03b2`。凍結時に固定する）
- implementation head commit: 未固定
- A3 凍結記録（承認者・日付・対象 revision・承認の根拠。凍結前は `未凍結`）: 未凍結
- risk: high（理由は §2.6）
- owner: root（依頼者）
- created: 2026-11-20 / updated: 2026-11-23（A3 revision 2 候補）
- expires: 2026-12-31 または置換 revision
- harvest to: `tools/DebugStudio/src/DebugStudio.Cli/README.md`（`set-active` の使い方、検証対象の名前、target、outcome と exit、不明のときの扱い）、`unity/Assets/Docs/Architecture/12-telemetry.md` §10.5（SampleGame の catalog 配線、`debug.set-active` の契約、`TryResolveHierarchyNode` の Play 照合、hierarchy が DontDestroyOnLoad を出さないこと）
- Phase A snapshot の取得案内・状態: 未生成。照合 script の有無にかかわらず、owner の凍結後に凍結仕様をローカル固定し、C と C′ に取得案内を渡す。
- Phase B result snapshot / evidence bundle / C' blind bundle の取得案内・状態: 未生成
- 基準動作の Evidence: 116 ファイルをローカル固定（未昇格）。別の Claude CLI セッションが取得し、両 Play の hierarchy・inspect・Editor 独立値、WPF 終了と観測票を閲覧した。取得案内は task の担当者と owner 向け手元引渡し先に保管済み。
- task: `debugstudio-s3`（開始済み）
- A2 の入力版: 計画開始点 `a629f9c19bdfe55b24fcac2f1756e25824d9e453` の A1 初稿（§5.5）。revision 1 は最新 develop `d176033a` で照合し直した統合候補、revision 2 はその上で外部計画レビューを反映した候補。
- 引継ぎ票: 次は §5.6 の順序。(1) 本書の固定 copy で構造観点の限定再確認を行う（範囲は §5.6）。(2) 外部所見の採否と再確認の結果を添えて、revision 2 を Draft PR #204 へ 1 commit で反映する。(3) owner の凍結判断・対象 revision・次担当 B を §0 に記録して凍結版を commit し、その SHA を票の外で渡す。凍結後に Phase A snapshot を固定する。必要な権限: 再確認と外部計画レビューは読取りだけ。停止規則: 凍結前に実装・Unity テスト・Build をしない。目的・利用者の 2 欄は変えない。
- 再開位置: revision 2 の構造再確認完了。Draft PR #204 の候補について owner の凍結判断を待つ。

## 1. 目的と対象外

### 1.1 目的と利用者（目的レビューへ渡した原文。変えていない）

- 目的: Editor の Play 中に、DebugStudio CLI の hierarchy で SampleGame の検証用 GameObject を 1 つ特定し、名前付きコマンドでその activeSelf を切り替え、inspect で結果を読み返せるようにする。Play をまたいで古い指定を使っても別の対象を変更しないこと、切断やタイムアウトで実行されたか分からないときに利用者がそれを成功や失敗と取り違えないことを含む。
- 利用者: SampleGame を Editor で Play し、DebugStudio WPF を起動したまま CLI のコマンドを実行してその出力を読む開発者と AI エージェント。

### 1.2 対象外

- activeSelf 以外の field・component・メソッドへの書き込み（汎用プロパティ編集）。
- 検証用 GameObject 以外の GameObject（ゲームの GameObject を含む）への書き込み。複数の検証対象。
- Player（Build した実行ファイル）での動作と遠隔接続。
- activeSelf の切り替え以外の操作コマンド。
- GUI から書き込む操作。WPF の画面の変更。
- inspect の自己応答保証（要求 ID による相関）。S2 の TargetId 一致の best-effort はそのまま。
- 結果不明の後の自動再実行、操作履歴の永続化や復旧の仕組み。
- SampleGame の Scene・Prefab・設定 asset の変更。
- S2 HANDOFF（`docs/handoff/DEBUGSTUDIO_S2_SIMPLE_A3_CANDIDATE.md`）の Phase D。

### 1.3 現況（develop `d176033a` のコードの事実）

`a629f9c1` から `d176033a` までの差分は Content overlay（S3-4a）と Bootstrap の Editor snapshot 系で、DebugSocket・DebugStudio・protocol・SampleGame の `AppInitializer` に差分は無い。

- CLI の `send --command <name> --payload <json>` は、WPF の control（`DebugStudioCliControlService.DispatchCommandAsync`）が `CommandService.SendAsync` で `DebugCommandEnvelopeV1` を Runtime へ送り、同じ requestId の `CommandResult` を待つ。結果は Completed / TimedOut / Failed。Failed は送る前の失敗と送った後の切断を区別しない。CLI の client（`DebugCommandControlPlaneClient`）は、control への接続中の期限切れも応答待ちの期限切れも同じ TimedOut にする。`send` の出力は text で、成功 0、Runtime 失敗と Failed が 1、TimedOut が 2。
- Runtime のコマンド経路: router は組込みの `ping` / `runtime-diagnostics` を先に処理し、それ以外は DebugCommand capability の交渉を確かめてから App の dispatcher へ渡す。dispatcher は `MainThreadDebugCommandDispatcher` で包まれ、main thread の文脈が無ければ Success=false を返し、あれば main thread へ切り替えてから中の dispatcher を呼ぶ。
- コマンド登録経路: `CreateDebugCommandDispatcher()` の既定は `NullDebugCommandDispatcher`。任意の `DebugCommandCatalog`（Ordinal 完全一致、同期実行、handler の例外は伝播）と `CatalogDebugCommandDispatcher` がある。SampleGame は配線していない。dispatcher は DebugSocketService より先に作られる。
- NodeId は `DebugSocketRuntimeNodeRegistry` が service ごとに 1 から採番する。同じ service の中では採番値を戻さない。WPF の接続が替わると対応表は `Reset` され、同じ GameObject に新しい NodeId が付く。DebugSocketService は Play ごとに作り直される。
- hierarchy の capture（`DebugSocketHierarchyPublisher.CaptureUnsafe`）は `SceneManager.sceneCount` / `GetSceneAt` の scene だけを走査する。DontDestroyOnLoad の scene はこの列挙に含まれない。非アクティブな root と子も capture に入る。
- Welcome の SessionId は `UnitySessionCorrelationContext.SessionId`（public static）。`BootstrapSubsystemRegistration` が Play ごとに新しい GUID にする。hierarchy の snapshot と CLI の hierarchy 出力には SessionId が無い。
- UICommon は `BootstrapUiCommonLoader` が AfterSceneLoad で読む App 常駐の scene で、SceneDirector の管理外。Play の終わり（`ReleaseAll`）まで残る。読み込みと DebugSocket の起動が済んだ後に `OnServicesInitializing`（protected virtual）が呼ばれ、後段の失敗では `OnAfterSceneLoadInitializationFailed` が呼ばれる。SampleGame は後者だけを override している。`UICommon` は public MonoBehaviour。
- `DebugSocketService.NotifyHierarchyChanged()` は public。NodeId から GameObject を引く処理は private。`AbstractApplicationInitializer` に DebugSocketService の accessor は無い。
- Inspector は `GameObject.ActiveSelf` と `GameObject.ActiveInHierarchy` を別の property として返す。CLI の inspect 出力の `rawValue` は文字列（`"true"` / `"false"`）。
- `OneStarMaker.Runtime` と `SampleGame.DependOnAll` の asmdef は `overrideReferences: true`。Unity 側で JSON の解析に使えるのは `JsonUtility`。

### 1.4 現行 base の基準動作（2026-11-22 観測済み。再実行しない）

別担当が `d176033a` の専用 worktree で観測した（Editor 6000.6.9f1、Release の WPF と CLI）。新機能は未実装の状態の観測である。

- Release build は exit 0。WPF を `--listen` で直接起動し、Play 前の `hierarchy` は `no-snapshot`、Play 後に `hasSnapshot: true`。
- 通常の Editor Play の hierarchy は、開いていた scene の root（`Main Camera`、`Directional Light`）と UICommon の root（`[UICommon]` と子 4 つ）だった。DontDestroyOnLoad に残る CameraSystemHost などは出なかった（§1.3 の走査範囲と一致）。今の SampleGame には検証用の GameObject も書き込みのコマンドも無い。
- 観測では、検証支援の eval が fixture（root と子 `Target`）を作った。Play1 と Play2 の両方で root が NodeId 8、`Target` が NodeId 9 になり、別の GameObject に同じ NodeId が再び振られた。Play2 で Play1 の NodeId 9 を `inspect` すると Ready で、同じ名前の Play2 の `Target` を返した。
- 各 Play で Editor の eval が読んだ、その Play の `Target` の activeSelf は両方 true で、それぞれの Play の inspect と一致した。これは各 Play の中の一致であり、2 つの Play の対象が同じことを示すものではない。
- `runtime-diagnostics` の sessionId は Play1 `9c33066a…`、Play2 `e8ff2503…` で異なった。
- `send --command debugsocket.set-active --payload '{}'` は `success=false`、`... is not registered.`、exit 1。
- WPF に通常の終了を要求し、process handle の signal と exit 0。Editor は Play 停止・Scene clean・tracked 変更なしで残った（観測担当が専用 worktree で開いた Editor）。

### 1.5 要求

- 通常の SampleGame の Editor Play に、検証専用で無害な GameObject が 1 つあり、人手や eval の準備なしに、CLI だけで hierarchy から特定し、無効化・再有効化し、inspect で読み返せる。
- Play をまたいだ古い指定で別の対象を変更しない。
- 切断・タイムアウトで実行有無が分からない結果を、成功・失敗と取り違えない形で返す。
- 既存の hierarchy / send / inspect とコマンド登録経路を起点にする。
- 現行 develop の Release WPF と実 Editor で確かめる。Editor で独立に読む値は検証だけに使う。

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
| UC0 検証対象を用意する | 無し | SampleGame が Editor Play の起動時に `[DebugStudioVerificationTarget]` を 1 つ作る | 自動（開発時の宣言は SampleGame のコード） | 「SampleGame の検証用 GameObject」。人手や eval の準備を求めない |
| UC1 対象を特定する（`hierarchy`） | 無し（既定値で使える） | 各 node の `target`（Runtime が snapshot に載せた sessionId と NodeId を CLI が 1 つの文字列にする）、NodeId・名前・Flags | 自動導出 | 「hierarchy で 1 つ特定」。名前は README に固定 |
| UC2 切り替える（`set-active`） | `target`（UC1 の 1 行から写す 1 値）、`--active true\|false` | コマンド名 `debug.set-active`、requestId、payload、outcome の分類 | 実行時の操作 | target: 特定した 1 つを、どの Play の hierarchy から来たかと一緒に 1 値で渡す（Play の識別子を別に入力させない）。値: 反転にすると、不明の後に望む状態へ戻すための再実行で逆に戻りうる |
| UC3 読み返す（`inspect`） | NodeId（既存） | 既存どおり | 実行時の操作 | 「inspect で読み返す」。変更なし |
| UC4 不明のとき | 新しい入力は無い（UC1 から UC2 をやり直す） | CLI は自動で再送しない | 実行時の操作 | 「取り違えない」。§2.1 の 6 |

A3 自己点検: 利用者の入力は target・値の 2 つで、準備の操作は無い。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 同じ WPF を `--listen` で起動したまま、通常の SampleGame の Editor Play で CLI だけで、hierarchy で特定した検証用 GameObject の activeSelf を名前付きコマンドで切り替えて inspect で読み返せ、Play をまたいだ古い指定では何も変更せず、実行有無が分からない結果を確定した結果と区別して返せるか。
- 次スライスへ進む最低条件: §2.2 の I1〜I5 と E1〜E4 を同じ GO 候補 head で満たす。

### 2.1 統合設計

書き換えの処理と対象は SampleGame がコマンド登録経路で持ち、対象は SampleGame が所有する検証専用の GameObject 1 つに限る。Framework は「現在の Play でこの指定が指す GameObject を返す」解決だけを公開する。CLI は既存の send の要求経路に薄い入口を足す。

1. **検証対象（SampleGame）:**
   - 新規 `DebugVerificationTargetHost`（plain class、`SampleGame.DependOnAll.DebugCommands`）が、名前 `[DebugStudioVerificationTarget]` の GameObject を 1 つ作り、所有し、破棄する。component は Transform だけで、描画・入力・ゲームの処理を持たない。
   - 置き場所は UICommon の scene。`AppInitializer` が `OnServicesInitializing` を override し、`Application.isEditor` のときだけ、`UICommon`（`FindAnyObjectByType<UICommon>()`）の `gameObject.scene` を host に渡して作らせ、`SceneManager.MoveGameObjectToScene` で移す。作った後に `DebugSocket?.NotifyHierarchyChanged()` を呼ぶ。理由: hierarchy は DontDestroyOnLoad を出さない。ゲームの scene は SceneDirector が入れ替える。UICommon は App 常駐で SceneDirector の管理外なので、Play の間ずっと hierarchy に出る。Scene asset は変えない。UICommon の root の子にはせず、別の root にする（Framework の GameObject の所有を混ぜない）。
   - 重複させない: host は生きた GameObject を持っていれば作らない。`AppInitializer` は既存の Camera などと同じく、`SubsystemRegistration`（次の Play の始め）と `OnAfterSceneLoadInitializationFailed` で host を解放する。Play の終わりに Unity が破棄した GameObject は `== null` で判定する。
   - 非アクティブでも再特定できる: 非アクティブな root も capture に入り、同じ service の中では NodeId が変わらないので、同じ target と同じ名前で hierarchy に出続ける。
   - UICommon が見つからなければ作らず、warning を出す（E1 が成り立たないことで検出される）。
2. **target（対象指定）:**
   - Runtime が `HierarchySnapshotEnvelopeV1` に `SessionId`（string、既定 `""`、追加のみ）を入れる。値は snapshot を作る時点の `UnitySessionCorrelationContext.SessionId`。delta には入れない。
   - WPF の `HierarchyStore` は snapshot の SessionId を nodes・revision と同じ lock で保持し、delta では保ち、`Clear` で空に戻す。`ControlHierarchySnapshotV1` に `SessionId`（追加のみ）を加え、`CliReadService` がそのまま載せる。WPF は SessionId を後付けしない。
   - CLI の `hierarchy` の `data` に `sessionId` を、各 node に `target`（`<sessionId>:<NodeId>`）を加える。sessionId が空なら `target` は null。既存の field と NodeId はそのまま。
3. **Framework の追加（公開面はこの 2 つと enum）:**
   - `DebugSocketService.TryResolveHierarchyNode(string sessionId, long nodeId, out GameObject? gameObject)` が `DebugSocketNodeLookup`（`Resolved` / `StaleSession` / `NotFound`）を返す。main thread で呼ぶ。最初に `sessionId` を `UnitySessionCorrelationContext.SessionId` と ordinal で比べ、違えば対応表を引かずに `StaleSession`。一致すれば `_gate` の中で既存の対応表を引き、無い・破棄済み・scene が unload なら `NotFound`。
   - `AbstractApplicationInitializer` に `protected DebugSocketService? DebugSocket => _services?.DebugSocket;`（既存の `LoggerFactory` 等と同じ形）。
   - 接続の現行性は照合しない。理由: Play が違えば session の照合で拒否する。同じ Play の中では、接続が替わると対応表が `_gate` の中で `Reset` され採番値は戻らないので、旧接続の要求の NodeId は同じ GameObject に解決するか `NotFound` になり、別の GameObject には解決しない。
4. **コマンド（SampleGame、コマンド登録経路）:**
   - `AppInitializer` が `CreateDebugCommandDispatcher()` を override し、`DebugCommandCatalog` に `debug.set-active` を登録して `CatalogDebugCommandDispatcher` を返す。登録は `Application.isEditor` のときだけ。handler は `() => DebugSocket` と `() => host.Target` で実行時の値を読む。
   - handler の順序: (1) payload を解析し、不正なら `invalid-payload`。(2) `TryResolveHierarchyNode` が `StaleSession` なら `stale-session`、`NotFound` なら `target-not-found`。(3) 解決した GameObject が host の GameObject と同じでなければ（`==`）`not-verification-target`。(4) `SetActive(active)`、後の `activeSelf` を読み、`NotifyHierarchyChanged()`。(1)〜(3) は SetActive より前に決める。SetActive の後の例外は catch せず router の既存の catch に任せる（payload の無い Success=false になり、CLI は unknown にする）。
   - 要求 payload: `{"sessionId":"<文字列>","nodeId":"<10 進の正の Int64 文字列>","active":"true"|"false"}`。3 つとも JSON 文字列で、欠落・空・範囲外・`true` / `false` 以外は `invalid-payload`。
   - 結果: 適用は `DebugCommandResult.Ok` に `{"outcome":"applied","reason":"applied","activeSelf":<bool>}`。拒否は Success=false と payload を指定できる既存のコンストラクタで `{"outcome":"rejected","reason":"<invalid-payload|stale-session|target-not-found|not-verification-target>"}`。
5. **CLI:** `debugstudio-cli set-active <target> --active true|false [--control-uri URI] [--timeout-seconds N]`（timeout は既定 15 秒、0 超〜60 秒）。target を `<sessionId>:<NodeId>` として検査し、合わなければ usage。既存の `DebugCommandControlPlaneClient` で 1 回だけ送る。client の結果に「control への WebSocket 接続が確立したか」を property として足す（send の出力は変えない）。JSON 1 object を stdout に出す: `status`（`ok` / `error`）、`outcome`、`reason`、`data`（requestId、target、`applied` なら activeSelf、失敗時の detail）。

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
6. **応答と不明の扱い（README と usage に書く）:** CLI は 1 回の実行で 1 回だけ送り、自動で再送しない。期限切れの後に届いた応答で返した結果を変えない。WPF の control service は変えない（WPF の送る前の失敗は CLI からは区別できないので unknown）。`unknown` は、その要求が適用されたかどうかが分からず、後から確定しないことを意味する。後の `inspect` や `hierarchy` は読んだ時点の現在値で、その要求の実行の証拠にはならない。望む状態にしたいときは `hierarchy` を読み直し、新しい target で `set-active` を実行する。その結果はその要求のことだけを表す。前の target を再利用しない（同じ Play でも WPF が再接続すると `target-not-found`、Play が替われば `stale-session`）。

**比べた代替と採らない理由:**

- 検証対象を印（marker component）で表し、印の付いた GameObject を何でも書き換えられるようにする（revision 1）: 通常の Play に対象が無く eval の準備が要る（外部所見）。印を付ければ任意の GameObject に広がる。
- 検証対象を Scene asset に置く: 依頼の範囲外の Scene 変更になり、開く scene によっては無い。
- DontDestroyOnLoad に置く: hierarchy に出ない（§1.3、§1.4）。hierarchy の走査範囲を広げると Framework の出力が全体に変わる。
- ゲームの scene に置く: SceneDirector の入れ替えで消え、作り直しの仕組みが要る。
- DebugSocket の組込みコマンド（A1 初稿）: App の allowlist を迂回し、対象が全 GameObject になる（構造レビュー F3）。
- SampleGame が名前で対象を探す（target を使わない）: hierarchy の特定と古い指定の照合が成り立たない。名前は handler の照合に使わない（照合は所有する GameObject との参照の一致）。
- sessionId を別の引数で入力させる（A1 初稿）、NodeId を Play 間で衝突しない採番にする、既存の `send` だけで済ませる、WPF→Runtime の境界でも送る前と後を分ける、同じ Play の再接続で前の target を再利用する、反転（toggle）: revision 1 と同じ理由で採らない（§5.5）。

### 2.2 進める最低条件

#### 不変条件

- **I1: hierarchy が出した target は、その Play の中でしか GameObject に解決されない。** Play が違う、または指定が失効した target による要求では、どの GameObject の activeSelf も変わらない。
  - 主張を崩しうる既存の経路: (a) NodeId の採番が service ごとに 1 から始まり、Play が替わると同じ NodeId が別の GameObject を指す（基準観測で再利用を確認）。(b) WPF は切断後も hierarchy の保持値を残す。(c) SessionId が Play 間で変わらない場合（Play ごとに `ResetForNewPlayerSession`、domain reload では新しい GUID）。(d) 同じ Play の中で DebugSocketService が作り直される場合（製品の経路で `InitializeBeforeSceneLoad` を呼ぶのは SampleGame の BeforeSceneLoad 入口だけ）。(e) DebugCommand の経路は main thread へ切り替えた後に現在の接続を確かめない（§2.1 の 3 の理由で I1 は崩れない）。(f) WPF が SessionId を受信時に後付けすると旧 nodes に新しい SessionId が付きうる（producer が載せて経路を無くす）。(g) 旧 Play の要求が新しい Play の main thread で実行される（session の照合で拒否）。(h) 利用者が別々の出力から sessionId と NodeId を手で組み合わせた target を作る（target を 1 値にして手順から無くす。手で組み立てた target は Runtime が NodeId の世代を持たないので区別できない。検証対象は Play ごとに 1 つなので、そうした target が解決できても書き換わるのはその Play の検証対象だけである。保証の範囲外として README に書く）。
  - 探索した境界: Runtime の DebugSocketServices（router、service の Session / Inbound / Hierarchy / Inspector、registry、publisher、dispatcher 群）、Bootstrap の SubsystemRegistration・BeforeSceneLoad・AfterSceneLoad、`UnitySessionCorrelationContext`、`DebugCommandCatalog`、WPF の SessionMessageRouter・HierarchyStore・SessionResetPolicy・control service、CLI の parser と client。
- **I2: 実行有無が分からない結果を、適用済みや未適用として報告しない。** CLI は構造化された結果を受け取ったときだけ `applied` / `rejected` を出し、control への接続が確立する前に終わったときだけ `not-sent` とする。それ以外はすべて `unknown`（exit 3）で、自動で再送しない。
  - 主張を崩しうる既存の経路: Failed が送る前と後を区別しない。client が接続中と応答待ちの期限切れを同じ TimedOut にする。router の catch は SetActive の後の例外も Success=false（payload なし）にする。`MainThreadDebugCommandDispatcher` は文脈が無いと Success=false を返す。catalog は未登録を Success=false（payload なし）で返す。期限切れの後に届いた CommandResult は CommandStore に入るが CLI へは返らない。
  - 探索した境界: CLI の client と結果処理、WPF の control service と TransportCommandSender、Runtime の router・dispatcher 群・catalog。
- **I3: 既存の契約を変えない。** `--listen` なしの起動、`send` / `compare` の出力と exit、`hierarchy` / `inspect` の既存 field と exit、GUI の動作、S2 の不変条件。変更は field・サブコマンド・property の追加だけ。
  - 主張を崩しうる既存の経路: `CliArgumentParser.Parse` の先頭分岐、`ReadCommand.Serialize`、MessagePack の key 番号と既定値、`Program.WriteResult`、hierarchy の NDJSON export（出力を変えない）。
  - 既知の観測できる変化: (1) SampleGame で未登録の名前を送ったときのメッセージが catalog の `Debug command is not registered.` に変わる（Success=false と exit は同じ。文言は契約ではない）。(2) Editor Play の hierarchy に `[DebugStudioVerificationTarget]` の root が 1 つ増える。
- **I4: SampleGame の検証対象 1 つ以外の GameObject の activeSelf を変えない。** ゲームの GameObject、Framework の GameObject、同じ名前を付けた別の GameObject を含む。
  - 主張を崩しうる経路: target は hierarchy のどの GameObject も指しうる。GUI のコマンド画面や `send` から同じコマンド名を任意の payload で送れる（どの経路でも handler の参照の照合を通る）。名前は誰でも付けられる（照合に名前を使わない）。
- **I5: Editor 以外では検証対象を作らず、コマンドも登録しない。**
  - 主張を崩しうる経路: DebugSocket を有効にした Player Build でも `CreateDebugCommandDispatcher()` と `OnServicesInitializing` は呼ばれる。

#### 成立条件（現行 base では未実装。§1.4 は実装前の基準動作）

- **E1:** 人手や eval の準備をしない通常の SampleGame の Editor Play で、hierarchy に `[DebugStudioVerificationTarget]` がちょうど 1 つ出る。
- **E2:** Release の WPF を `--listen` で起動し、その node の target で `set-active` を実行すると、検証対象の activeSelf が指定した値（false と true の両方向）になり、CLI の `activeSelf`、Editor で独立に読んだ activeSelf、`inspect` の `GameObject.ActiveSelf` の rawValue（`"true"` / `"false"`）が一致する。非アクティブの間も hierarchy に同じ名前・同じ target のまま 1 つだけ出る。
- **E3:** 同じ WPF プロセスのまま Play を停止・再開した後、新しい `hierarchy` の target で E1・E2 がもう一度成り立ち、検証対象は 1 つのまま。
- **E4:** 旧 Play の target、現在の hierarchy に無い NodeId、検証対象でない GameObject、不正な引数・payload、接続できない control、期限切れ、応答前の切断、構造化されていない応答が、それぞれ §2.1 の表の outcome・reason・exit で返る。

### 2.3 受け入れ条件

- **AC1（I3）:** 既存の .NET 回帰が通る（send 成功・失敗・timeout の出力と exit、compare、hierarchy / inspect の既存 field と exit、usage 2）。hierarchy の JSON に `sessionId` と各 node の `target` が加わり、既存 field は変わらない。Contracts の roundtrip で `HierarchySnapshotEnvelopeV1.SessionId` と `ControlHierarchySnapshotV1.SessionId` が往復し、field を持たない payload は `""` で読める。`pwsh tools/protocol-codegen/generate.ps1 --check` が一致する。
- **AC2（I1、失敗経路）:** Unity EditMode:
  - `TryResolveHierarchyNode` は、sessionId が現在と違えば、同じ NodeId が対応表にあっても `StaleSession` を返し、GameObject を返さない。
  - 対応表に無い NodeId、破棄した GameObject の NodeId は `NotFound`。対応表の `Reset` の後、Reset 前の NodeId は `NotFound` で、同じ GameObject に付いた新しい NodeId は `Resolved`。
  - handler は `stale-session` / `target-not-found` で検証対象の activeSelf を変えない。
  - 実 Editor（§5.3 V6）: Play2 で Play1 の検証対象の target による `set-active` が `rejected` / `stale-session`（exit 1）で、Play2 の検証対象の activeSelf が Editor で独立に読んで変わっていない。合格例: Play1 と Play2 で検証対象の NodeId が同じでも `stale-session` で変わらない。不合格例: `target-not-found` が返った（照合の順序が逆で I1 を判定できていない）、または Play2 の検証対象が変わった。
- **AC3（I2、E4）:** .NET の CLI テストで、偽の client 結果ごとに §2.1 の表の outcome・reason・exit になる（接続中の期限切れの合格例・不合格例を含む）。どの場合も送信は 1 回。Success=true で payload が `rejected`、Success=false で payload が `applied`、Success=false で payload なし（未登録の応答）は `unknown` / `unstructured-result`。client のテストで、接続前と接続後の期限切れで「接続が確立したか」が分かれる。
- **AC4（I1、target の出所）:** Unity EditMode: snapshot の `SessionId` が `UnitySessionCorrelationContext.SessionId` と同じ。.NET: `HierarchyStore` が snapshot の SessionId を保持し、delta の後も保ち、`Clear` で空に戻り、`GetRetainedSnapshot` が nodes・revision と一緒に返す。`CliReadService` の応答に載る。CLI の hierarchy は sessionId が空なら各 node の `target` を null にする。
- **AC5（E1〜E3）:** 実 Editor（§5.3 V0〜V9）で、eval で何も作らずに、Play1 と Play2 のそれぞれで hierarchy に `[DebugStudioVerificationTarget]` がちょうど 1 つあり、その target で false にしてから true に戻し、各回で CLI の `applied` と `activeSelf`、Editor で独立に読んだ値、`inspect` の rawValue が一致する。false の間も hierarchy に同じ名前・同じ target で 1 つだけあり、ActiveSelf flag が無い。true に戻すと flag が戻る。WPF のプロセスは 2 回の Play を通して同じで、GUI のボタンを押さない。合格例: Play2 の 2 回目の切り替えまで同じ PID・eval なしで成立。不合格例: eval で対象を作った、Play2 を WPF の再起動後に確かめた、検証対象が 2 つ出た。
- **AC6（I4）:** Unity EditMode: 検証対象でない GameObject（同じ名前を付けた別の GameObject を含む）への要求は `not-verification-target` で activeSelf が変わらない。実 Editor（§5.3 V5）: 検証対象でない node（開いていた scene の root の 1 つ）の target で `not-verification-target`（exit 1）、その GameObject の activeSelf が変わらない。
- **AC7（E1、host の寿命）:** Unity EditMode: host は scene を受け取って GameObject を 1 つ作り、生きている間に 2 回目を求めても作らない。解放で破棄し、破棄済みの GameObject を持っていても解放は失敗しない。作った GameObject の component は Transform だけ。
- **AC8（I5）:** Unity EditMode: 登録の処理に Editor でないことを渡すと catalog に `debug.set-active` が無く、検証対象も作らない。Editor なら有り、作る。

### 2.4 ここでは答えない問いと所有する後続スライス

- 汎用プロパティ編集、別の操作コマンド、GUI からの書き込み、複数対象、ゲームの GameObject への書き込み: DebugStudio S4 以降（未起票）
- Player と遠隔での書き込み: DebugStudio S4 以降（未起票）
- inspect の自己応答保証: DebugStudio S4 以降（未起票）
- WPF→Runtime の境界で送る前の失敗を未送信と確定すること: DebugStudio S4 以降（未起票）
- NodeId に Play の世代を持たせ、手で組み立てた target も区別すること: DebugStudio S4 以降（未起票）
- hierarchy に DontDestroyOnLoad を出すこと: DebugStudio S4 以降（未起票）
- S2 から移送された client / server の timeout 起点と client 離脱後の占有: DebugStudio S4 以降（未起票）
- S2 HANDOFF の Phase D: S2 の担当

### 2.5 スライス分割の判断

- 契機の確認: (1) 変更理由の混在: 検証対象（SampleGame）、target の出所（wire と WPF）、解決（Framework）、書き換え（SampleGame）、分類（CLI）は、どれもこの問いに必要で、どれか一つでは答えられない。(2) 独立に失敗しうる未知: S2 の読取りは §1.4 で成立を観測した。残る未知は、検証対象の配置と新しい書き込み経路で、どちらも同じ実機手順の前半（V1〜V2）と後半（V3 以降）で別に判定できる。(3) 別の問いの受け入れ条件: 混じっていない。
- 判断: 分割しない。

### 2.6 risk と判定

- risk: high。理由: Framework の公開 API（`TryResolveHierarchyNode`、`DebugSocketNodeLookup`、protected `DebugSocket`）と、Unity・DebugStudio 両 surface の wire field を足す。Play をまたいだ照合と、main thread の切り替え・接続の差し替えの競合が主張の中心にある。SampleGame の起動経路に GameObject の生成を足す。
- 判定定義: GO は I1〜I5 と E1〜E4 を同じ GO 候補 head で満たすこと。NO-GO はいずれかの未達。CONDITIONAL ACCEPT は使わない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし

### 2.7 本文へ転記した実装制約

- §1.6 の常時契約と既知の制約。
- asmdef 参照と package を追加しない。Unity 側の payload 解析は `JsonUtility` か手書き。
- Framework の公開面の追加は §2.1 の 3 と wire field だけ。router、組込みコマンド、`MainThreadDebugCommandDispatcher`、`DebugCommandCatalog`、hierarchy の走査範囲、UICommon の loader を変えない。
- 既存の send / compare / hierarchy / inspect の出力・exit を変えない。追加は field・サブコマンド・property だけ。
- WPF の control service、AutoListen、終了経路、GUI を変えない。
- SampleGame の Scene・Prefab・設定 asset を変えない。検証対象は SampleGame のコードが Editor でだけ作る。
- 検証対象は 1 つ。component は Transform だけ。handler の照合は host が所有する GameObject との参照の一致で、名前・印・パスを使わない。
- 拒否は SetActive より前に決める。SetActive の後の例外を `rejected` にしない。
- 書き込みを再現するための遅延・切断の操作を製品の CLI に足さない。

### 2.8 人間が凍結前に判断すること

- 外部所見の採用（§5.5）: 検証対象を SampleGame が Editor でだけ自動で 1 つ作り、UICommon の scene に別の root として置くこと。revision 1 の印（marker）と eval での準備を廃止すること。
- 書き換えの置き場所を SampleGame のコマンド登録経路とし、Framework の公開 API を 2 つ足すこと。
- 手で組み立てた target を保証の範囲外とし、後続へ送ること。
- WPF→Runtime の境界の送る前の失敗を unknown に含めること。

## 3. 責務マップ

行数は `d176033a` の現在値。増分は見込み。変更対象は Unity 側 7 file（新規 3）、protocol yaml 2 と codegen の生成物、DebugStudio 側 9 file（新規 1）、文書 2。テストは別に数える。

| file | 責務・変更理由 | 所有者・寿命 | 依存・公開面 | テスト境界 | 現在 / 増分 |
|---|---|---|---|---|---|
| `unity/Assets/OneStarMaker/Scripts/Runtime/DebugSocketServices/DebugSocketService.Hierarchy.cs` | `TryResolveHierarchyNode` | DebugSocketService（Play） | public method | EditMode | 87 / +25〜40 |
| `unity/.../DebugSocketServices/DebugSocketNodeLookup.cs`（新規） | 解決結果の enum | — | public enum | — | 新規 10〜20 |
| `unity/.../DebugSocketServices/Hierarchy/DebugSocketHierarchyPublisher.cs` | snapshot に SessionId | service | Foundation の `UnitySessionCorrelationContext` | 既存の publisher テストに追加 | 320 / +2〜5 |
| `unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs` | protected `DebugSocket` accessor | initializer（App） | protected property | — | 1305 / +3〜6 |
| `protocol/debugsocket/envelopes/hierarchy_snapshot_envelope_v1.yaml`、`control_hierarchy_snapshot_v1.yaml` と codegen 出力 | `SessionId` の追加 | protocol 正本 | 両側の生成型 | codegen --check、roundtrip | 各 +1 と生成差分 |
| `unity/Assets/SampleGame/DependOnAll/DebugCommands/DebugVerificationTargetHost.cs`（新規、internal） | 検証対象の生成（1 つ・scene へ移す）・所有・解放 | AppInitializer が所有。GameObject は Play の寿命（UICommon scene の中） | internal | EditMode（scene を渡して生成・重複・解放） | 新規 40〜80 |
| `unity/Assets/SampleGame/DependOnAll/DebugCommands/SetActiveDebugCommand.cs`（新規、internal static） | 登録（Editor のときだけ）、payload の解析、解決・参照の照合・SetActive、結果 JSON | catalog は Play ごと。closure は App 寿命の initializer だけを持つ | internal | 解決と対象を delegate で差し替えて EditMode | 新規 100〜160 |
| `unity/Assets/SampleGame/DependOnAll/AppInitializer.cs` | `CreateDebugCommandDispatcher()` と `OnServicesInitializing` の override、host の解放（SubsystemRegistration・初期化失敗） | initializer | — | — | 431 / +20〜35 |
| `tools/DebugStudio/src/DebugStudio.App/Core/Stores/HierarchyStore.cs`、`HierarchyStoreSnapshot.cs` | SessionId の保持・delta で保つ・Clear | WPF | — | HierarchyInspectorStoreTests | 189 / +8〜15、14 / +1〜2 |
| `.../Core/Services/CliReadService.cs` | 応答に SessionId | WPF | — | CliReadServiceTests | 119 / +1〜3 |
| `tools/DebugStudio/src/DebugStudio.Client/DebugCommandControlPlaneClient.cs`、`DebugCommandRoundtripResult.cs` | 接続が確立したかを結果に持たせる | CLI 要求ごと | 公開型に property | DebugCommandRoundtripClientTests | 148 / +5〜12、47 / +3〜8 |
| `tools/DebugStudio/src/DebugStudio.Cli/SetActiveCommand.cs`（新規） | target の検査、payload、送信、分類、JSON | CLI | Client | 送信関数を注入 | 新規 90〜150 |
| `.../DebugStudio.Cli/CliArgumentParser.cs`、`Program.cs`、`ReadCommand.cs` | 解析と usage、入口、hierarchy の `sessionId` と `target` | CLI | — | CliArgumentParserTests、ReadCommandTests | 216 / +30〜50、71 / +3〜6、45 / +3〜8 |
| `tools/DebugStudio/src/DebugStudio.Cli/README.md`、`unity/Assets/Docs/Architecture/12-telemetry.md` | 使い方と契約。§10.5 の「SampleGame に catalog が配線されているという意味ではない」を現状に直す | 公開文書 | — | docs-audit | 91 / +20〜35、515 / +10〜20 |

配置の理由: 検証対象はゲームの内容ではなく SampleGame の debug 配線なので、composition root の `DependOnAll` に置く。所有者は `AppInitializer`（既存の Camera・RenderEnvironment と同じ解放の型）、寿命は Play。入れ物の scene は Framework が所有する App 常駐の UICommon で、その寿命は検証対象以上に長い。

規模の警報: `AbstractApplicationInitializer` は既に 500 行を超えるが、既存の accessor と同じ形の 1 行で責務を増やさないので分割しない。`AppInitializer` は約 465 行になるが、生成と所有は host へ出し、`AppInitializer` には呼出しと解放だけを置く。他に警報に当たる file は無い。

新しい中核ロジックの単体テスト: 解決は Framework の EditMode、host と handler は SampleGame.Tests の EditMode、分類は CLI の .NET テスト。

## 4. 実装計画

- 順序: (1) protocol yaml と codegen。(2) Framework: publisher の SessionId、`TryResolveHierarchyNode` と enum、protected accessor、EditMode テスト。(3) SampleGame: host、handler、`AppInitializer` の override と解放、EditMode テスト。Editor で compile を確かめる。(4) WPF の HierarchyStore と CliReadService。(5) Client と CLI。(6) README と §10.5。
- Phase B から Phase A へ差し戻す条件: asmdef 参照・package が要る。§2.1 の 3 以外の Framework の公開 API が要る。router・組込みコマンド・catalog・hierarchy の走査範囲・UICommon の loader・control service・AutoListen・WPF の終了経路を変える必要がある。wire の契約（§2.1 の 2 と 4 の payload・結果）を変える必要がある。SampleGame の Scene・設定を変える必要がある。UICommon の scene に置けない事実が見つかる。
- 対象外を維持する方法: 汎用の property 書き込みや別のコマンドの土台を作らない。検証対象を増やす仕組み（印、一覧、登録 API）を作らない。GUI に操作を足さない。

## 5. テストとレビュー計画

### 5.1 テスト

- 単体テスト: §3 の各テスト境界。
- 差し戻し中の起点 `-Filter`（C が根拠付きで変更可）:
  - Unity EditMode: `TryResolveHierarchyNode` のテスト、`DebugSocketHierarchyPublisherTests`、`DebugSocketRuntimeNodeRegistryTests`、`DebugSocketProtocolRoundtripTests`、SampleGame の `DebugVerificationTargetHostTests`・`SetActiveDebugCommandTests`。
  - .NET: CLI の `SetActiveCommandTests`・`CliArgumentParserTests`・`ReadCommandTests`・`DebugCommandRoundtripClientTests`、App の `HierarchyInspectorStoreTests`・`CliReadServiceTests`・`CliControlPlaneRoundtripTests`、Contracts の `ProtocolRoundtripTests`・`ControlReadRoundtripTests`・`ProtocolGoldenCrossContractTests`。
- 判定必須テスト（GO 候補 head）: .NET の App・CLI・Contracts の各 test project の全件（Release）、`pwsh tools/protocol-codegen/generate.ps1 --check`、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、最終の全 EditMode 回帰（標準の `run-tests.ps1`。VFX の Inconclusive で exit が 1 になる既知事象があるため、合否は XML の件数と失敗 0 で判定する）、§5.3 の実機手順。
- 全 EditMode 回帰の適用除外: なし。PlayMode は要しない。
- 人間の判断が必要な条件: なし。高コスト証拠: なし（Player・別 host・R2 を使わない）。

### 5.2 未知の操作経路

- 既存の読取り経路: §1.4 で成立を観測した。
- 検証対象の配置（V1〜V2）と新しい書き込み経路（V3 以降）: 未確認。初回確認は Phase C。理由: 未実装。担当: 判定 C の実行担当。確認地点: AC5・AC2・AC6 の実機部分。不成立時は、待受け・接続、検証対象の生成と配置、登録、解決、CLI の分類のどこが原因かを分けて B へ戻し、受け入れ条件を黙って減らさない。B の出口の動作確認で同じ手順を 1 回軽く実行してよい（B の観測は判定の入力にしない）。

### 5.3 実機検証の手順（Phase C。V0〜V9）

GO 候補 head で判定 C の実行担当が行う。各 wrapper の呼出しは別の command にする。Editor が開いていればその所有者を確かめ、重複して起動しない。検証対象を eval で作らない。Editor の eval は独立値の読取りだけに使う。

- V0: HEAD・clean・Editor の版を記録する。`dotnet build tools/DebugStudio/src/DebugStudio.App/DebugStudio.App.csproj -c Release` と `dotnet build tools/DebugStudio/src/DebugStudio.Cli/DebugStudio.Cli.csproj -c Release`。App の Release 出力の exe を `--listen` で直接起動して PID を記録する。CLI は Release 出力の `debugstudio-cli.exe` を使う。
- V1: Play1 を開始する（`tools/unity-editor.cmd command` で公開名を確かめ、無ければ eval で `UnityEditor.EditorApplication.isPlaying = true;`）。`hierarchy` を `hasSnapshot: true` かつ `[DebugStudioVerificationTarget]` が現れるまで読み直す（総計 15 秒以内）。
- V2: hierarchy の `[DebugStudioVerificationTarget]` の node の数（1 を期待）、target（T1）、NodeId、flags を記録する。検証対象でない node として開いていた scene の root の 1 つ（基準動作では `Directional Light`）の target（X1）を記録する。Editor の独立値は次の eval で読む（非アクティブでも引けるよう scene の root を走査する）。

  ```csharp
  var ui = UnityEngine.Object.FindAnyObjectByType<OneStarMaker.Runtime.UISystem.UICommon>();
  foreach (var go in ui.gameObject.scene.GetRootGameObjects())
      if (go.name == "[DebugStudioVerificationTarget]") return go.activeSelf;
  throw new System.InvalidOperationException("verification target missing");
  ```

- V3: `set-active T1 --active false` → `applied`、`activeSelf: false`、exit 0 を期待する。Editor の独立値、`inspect <NodeId>` の `GameObject.ActiveSelf` の rawValue、読み直した `hierarchy` の検証対象の node の数・target・flags を記録する。
- V4: `set-active T1 --active true` → 同様に true 側を記録する。
- V5: `set-active X1 --active false` → `rejected` / `not-verification-target`、exit 1 を期待し、その GameObject の独立値（`GameObject.Find("<名前>").activeSelf`）が true のままか記録する。現在の hierarchy に無い正の Int64 の NodeId で作った target（T1 の sessionId と組み合わせる）で `set-active` → `rejected` / `target-not-found`、exit 1。
- V6: Play を停止し、Play2 を開始する（WPF は同じ、GUI のボタンを押さない、eval で何も作らない）。V1・V2 と同じく Play2 の検証対象の target（T2）・NodeId・数を記録し、Play1 の NodeId との異同を記録する。Play2 の独立値を読み、`set-active T1 --active false` → `rejected` / `stale-session`、exit 1 を期待し、独立値が変わっていないことを記録する。
- V7: Play2 で V3・V4 を T2 で繰り返す（E3）。
- V8: Play を停止する。
- V9: WPF に通常の終了を要求し（`CloseMainWindow`）、process handle の signal と実終了コードを記録する。強制終了した場合はそのとおり記録し、正常終了とは扱わない。Editor の Scene が clean か、tracked 変更が無いかを記録する。
- 保存するもの: 各段のコマンド、stdout / stderr / exit、HEAD、Editor の版、WPF の PID と終了コード、各 Play の hierarchy、各 `set-active` の JSON、Editor の独立値。取得時刻は診断情報で、相関の保証ではない。不明（unknown）の実機再現は求めない（AC3 の offline で判定する）。

### 5.4 判定 C の照合方式

- 照合 script を使わず、判定 C が原記録を直接照合する。理由: 照合する値は 2 回の Play で十数個（各 `set-active` の outcome・reason・activeSelf・exit、inspect の rawValue、Editor の独立値、hierarchy の node 数・flag）で、どれも等値の比較で閉じる。script を使う場合に規定された実行環境（資格情報・本番設定・通信から遮断し、偽 transport を注入できる環境）を判定 C 用に用意する経路が無く、用意する理由も無い。判定 C は、条件ごとに原記録の該当箇所と期待（§2.1 の表、Editor の独立値）を対にした表を判定記録に残す。期待は実装からではなく、Editor の独立値と §2.1 の表から導く。
- Phase A snapshot はこの方式と関係なく、凍結後に固定する（§0）。
- 機械検査: codegen --check、contract-audit、docs-audit。

### 5.5 A2・限定再レビュー・外部計画レビューと採否

**入力版と担当（実績）:**

| 観点 | 入力版 | 担当・モデル・ベンダー | 備考 |
|---|---|---|---|
| A0/A1 主担当 | — | Claude Opus 5.5 High（Anthropic） | A3 統合（revision 1・2）も同じ担当 |
| 目的レビュー | §1.1 の 2 欄の原文（sha256 `1699e1dd34d533f41442a7fd971e488f48665f0af7e0aeab49b195e6079f0196`。統合担当が入力 file から再計算して一致を確認） | GPT-6.2-sol High（OpenAI、新規セッション） | 初稿・A0 を読む前に回答 |
| 高リスク代替レビュー | A0 固定 copy（SHA-256 `67647af6420f3fe123f8c280549b8f7a130aaaa2e236b6bf8f3bbee91ec435ab`。担当が一致を報告） | GPT-6.2-sol High（OpenAI、目的レビューの回答保存後に同じ担当へ A0 だけを渡した） | 初稿・コードは読んでいないと報告 |
| アーキテクチャゲート（構造） | 固定 base `a629f9c1` と A1 copy（SHA-256 `474ad1e9a9c2e8614654a48b3d4be72a19a52736335aa0fe6861d6099fa1cfc1`） | Grok 4.6 High（xAI、cursor-agent の ask モード） | shell が拒否されたため、checkout の HEAD は worktree の HEAD file を直接読んで確かめ、clean と A1 copy の hash は送信側の計測記録を読んだだけで再計算していない |
| 構造の限定再レビュー（revision 1） | 固定 base `d176033a` と revision 1 の固定 copy | Grok 4.6 High（xAI、新規 cursor-agent セッション） | §2.1〜§2.3・§3 と該当コードを確認し、SampleGame の登録口と Framework の解決責務の分離、Play をまたぐ指定の拒否に阻害指摘なし。受領プロセスが起動直前に clean・base・固定 copy の SHA-256 を照合した。担当は HEAD を直接読み、ask モードの制約で hash を自分では再計算していない |
| 外部計画レビュー（revision 1） | 公開 Draft PR #204 head `4f17e54b` | 記録は PR とオーケストレーターの側にある | 所見 1 件（下記） |

**目的レビューの採否:**

| 指摘 | 採否 | 反映 |
|---|---|---|
| hierarchy 出力の指定で一意に特定し、CLI で完結する | 採用 | target（§2.1 の 2）、準備不要の検証対象（§2.1 の 1） |
| activeSelf を切り替え、非アクティブ後も同じ対象を読み返す。activeInHierarchy と取り違えない | 採用 | 結果は activeSelf だけを返す。AC は `GameObject.ActiveSelf` を名指しで照合 |
| 変更時点で Play と対象の同一性を確かめ、再利用・同名で別対象に解決しない。照合できなければ拒否 | 採用 | I1、I4、`TryResolveHierarchyNode`、参照の一致 |
| 確定・拒否・不明を区別し、不明後の inspect は過去の実行の証明にしない | 採用 | I2、§2.1 の 6 |
| Play ID などを手入力させない | 採用 | target 1 値 |
| 名前・パスの推測、一括、任意プロパティ、シーン編集を作らない | 採用 | §1.2、§2.7 |
| 自動再実行・永続履歴・復旧を作らない | 採用 | §2.1 の 6、§1.2 |

**代替レビューの採否:**

| 指摘 | 採否 | 理由・反映 |
|---|---|---|
| 識別を Runtime で確定し、コピー可能な対象指定を出す。SessionId の無い snapshot から書き込み用の指定を作らない | 採用 | §2.1 の 2 |
| 同じ Play の再接続では同じ対象を照合できる要求に限り再利用できる | 不採用 | 対応表の `Reset` を変える必要がある。失効と hierarchy の読み直しで足りる |
| 操作の意味と対象の選定は SampleGame が既存の登録経路で持ち、Framework は照合と解決を持つ。検証対象の根拠は狭い宣言にし、名前・パスに依存させない | 採用 | §2.1 の 1・3・4。根拠は host が所有する 1 つの GameObject との参照の一致。接続の現行性の照合は §2.1 の 3 の理由で入れない |
| 反転（toggle） | 不採用 | 不明の後の再実行で逆に戻りうる。値を明示する |
| WPF→Runtime と CLI→WPF の両境界で未実行と不明を分ける | 一部採用 | CLI→WPF だけ（接続確立前）。WPF 側は保守的に unknown とし後続へ |
| 専用の CLI 名を send の要求経路への薄い入口にする | 採用 | §2.1 の 5 |
| 失敗順序 1〜4 | 採用 | I1 (b)(f)(g)、I2、AC2・AC3 |
| Player は登録を Editor に限る | 採用 | I5、AC8（検証対象の生成も Editor に限る） |

**構造レビューの採否:**

| 指摘 | 採否 | 反映 |
|---|---|---|
| F1 sessionId と NodeId の異世代の組み合わせで I1 が破れる | 採用 | target を 1 値にした。手で組み立てた target は保証の範囲外（I1 (h)）、NodeId の世代は後続へ |
| F2 接続確立前の期限切れの分類が二重 | 採用 | 接続確立前は not-sent、確立後は unknown。合格例・不合格例と AC3 |
| F3 目的の範囲と Framework の書き換え面が合わない | 採用 | 組込みをやめ SampleGame の登録経路へ。revision 2 で対象を検証対象 1 つに限った |
| F4 予約 prefix と分岐規則の不一致 | 解消 | 組込みをやめたので予約 prefix は不要 |
| F5 不明の後の再実行手順が同じ Play の再接続と噛み合わない | 採用 | 不明の後は hierarchy を読み直した新しい target で再実行する |
| F6 main thread の文脈の検査、Editor 判定の配置 | 解消 | 登録経路の既存の検査を使う。Editor 判定は SampleGame の登録時と生成時 |

**外部計画レビュー（PR #204 head `4f17e54b`）の採否:**

| 所見 | 採否 | 反映 |
|---|---|---|
| 通常の SampleGame の Editor Play には印付きの対象が無く、検証の eval で毎回作らないと hierarchy → 名前付き操作 → inspect が完結しない。Editor でだけ SampleGame が Scene 資産を触らずに印付きの GameObject を 1 つ用意し、名前を README に固定し、V2 を生成から特定に変える | 採用を推奨（owner の裁定待ち） | §2.1 の 1、E1・E3、AC5・AC7、§5.3 V1・V2。印（marker）は使わず、SampleGame が所有する 1 つの GameObject との参照の一致にした（印を残すと任意の GameObject に広がるため）。置き場所は、hierarchy が DontDestroyOnLoad を出さないことを基準動作で確かめた上で UICommon の scene にした |

### 5.6 再レビューと凍結までの順序

- 目的レビュー: やり直さない。§1.1 の 2 欄を変えていない。revision 2 は外部所見に従い、目的の「SampleGame の検証用 GameObject を 1 つ特定し」を通常の Play で満たすための変更である。
- 代替レビュー: やり直さない。revision 2 の検証対象は、代替レビューが提案した「Game 側の狭い宣言」の具体化である。
- 構造（アーキテクチャゲート）: 限定して再確認する。範囲は revision 1 からの差分（§2.1 の 1 と 4 の (3)、I4・I5、E1〜E3、AC5〜AC8、§3 の SampleGame の 3 file、§5.3 V1〜V2）。見る点: 検証対象の配置（`DependOnAll`）・所有者（`AppInitializer` と host）・寿命（Play、入れ物は UICommon の scene）、Framework の所有する scene に別の root を置くことの妥当性、`OnServicesInitializing` を使う依存の向き、重複しないことと解放の経路、`FindAnyObjectByType<UICommon>()` を Framework の accessor の代わりに使うこと。入力は本書の固定 copy と固定 base `d176033a`。
- 構造だけで足りる理由: revision 2 で変わったのは、検証対象をどこに置き、誰が作り、いつ破棄するかと、handler の対象の照合を印から参照の一致に替えたことで、どちらもアーキテクチャゲートの観点（フォルダ境界、所有者、寿命、依存、テスト可能性）に入る。目的・利用者の文言、利用者の入力（target と値）、Framework の公開面、wire の契約、Play の照合（I1）、不明の分類（I2）は revision 1 と同じで、目的・代替の観点の入力は変わっていない。新しい engine の前提（hierarchy が DontDestroyOnLoad を出さない）は基準動作で観測済みで、UICommon の常駐はコードの事実である。
- 順序: (1) 本書の固定 copy と SHA-256 を作り、構造の限定再確認を行う。(2) 外部所見の採否と再確認の結果を添えて revision 2 を Draft PR #204 へ 1 commit で反映する。外部所見の採否は owner が裁定する。(3) owner の凍結判断・対象 revision・次担当 B を §0 に記録し、凍結版を commit する。implementation base は凍結時の base に固定し、凍結後に Phase A snapshot を固定する。
- C′ の担当: grok-4.7-xhigh（xAI、新規セッション）。Phase A の構造レビューと限定再レビューが Grok 4.6 High なので、モデルは違っても同じ Grok 系列であり、`独立性制約あり` と記録する。Phase C に Grok 系を使う場合も同じ制約を記録する。

**revision 2 の限定再確認実績。** 新規の cursor-agent（Grok 4.6 High）が現在の固定 base のコードと revision 2 の固定 copy を照合した。SampleGame の所有と UICommon scene への配置、初期化・解放の順序、非アクティブな root の再特定と同一対象の参照照合について、凍結を阻害する指摘は無かった。受領プロセスが起動直前に clean・HEAD・copy の SHA-256 を確認した。実装後は破棄時の参照解放、hierarchy 更新通知の時点、非アクティブな実対象の再掲を確かめる。外部所見 1 件の採用は owner の裁定待ち。

## 6. Phase B 実装結果

未到達。

## 7. Phase C

未到達。

## 8. Phase C'

未到達。

## 9. Phase D

未到達。
