# DebugStudio: NodeId と Inspector path による単一 property 書込み

## 0. メタデータと引継ぎ票

- type: `slice`
- status: `A`（A3 r3 凍結済み。2026-10-12 owner 承認。実装は未着手・開始待機）
- branch: `codex/debugstudio-property-write-a01`（`origin/develop` 起点）
- implementation base commit: `3f79639c12eac16da77d06608675564e906f9a49`
- implementation head commit: 未固定（実装未着手。本文だけの commit を implementation head にしない）
- Phase A revision: `r3`（凍結）。r1（commit `dc2d55a`、内容 SHA-256 `9549237404d65415f750f9e9e9fd76a1e707a2cc8c8d5b6fa67fa873c4eee7f8`）に対する A2 指摘を反映し、§1.5 の分割方針に合わせて範囲を **ActiveSelf の Runtime 書込み経路と、その実 UI 往復** に絞った。r3 は r2 の追加点検を統合し、取消・完了・配線と検証の境界を修正した。今回の作業指示は凍結と先行スライスの A0/A1 までであり、B は開始しない。
- risk: `high`（Player の状態変更、session と object の寿命、受信と Update の並行境界）
- owner: リポジトリ owner（依頼者）。§2.7 に D0〜D7 の採用・凍結を記録済み。
- created: 2026-10-12
- expires: 2026-11-12 または置換 revision の成立時。期限で自動採用・実装しない。
- harvest to: `protocol/debugsocket/README.md` の command 契約、`unity/Assets/README.md` の DebugStudio 案内。実装後の現況だけを反映する。
- task: `debugstudio-property-write`。通常 Workflow の開始を記録済み。今回の Phase 停止を task 全体の終了に変換しない。
- Phase A snapshot の取得案内・状態: r3 凍結正本は本ブランチの A3 commit。本担当がそこから固定 snapshot と manifest を生成し、既存 Evidence 保存経路へ渡す。保存・取得案内はこの会話で引き渡す（現時点は生成・保存前）。C' 用には §5.5〜5.6 の所見を含めない。
- Phase B result snapshot / evidence bundle / C' blind bundle の取得案内・状態: 全て未生成。作成 Phase の担当が既存引渡し先へ案内する。
- PR: 未作成（依頼範囲外）。後日作る場合も base は `develop`。

**引継ぎ票 — 本スライスは B 開始待機。先に Player UI Route の A0/A1。** owner が §2.6 D0〜D7 と残存リスクを含む r3 推奨案を承認し、§2.7 で凍結した。先行スライスは `codex/debugstudio-player-ui-route-a01` の `docs/handoff/DEBUGSTUDIO_PLAYER_UI_ROUTE.md` に分離する。本スライスの B は今回開始せず、後日の実装指示後に凍結 r3 を入力にする。高コスト証拠は先行スライス GO 後。追加のモデル点検は行わず、独立性の未充足は完了へ読み替えない（§5.5）。

今回の成果物は計画文書のみ。Unity・Player Build・実 UI の起動権限を今回の引継ぎから推測しない。今回 A3 の凍結まで実施し、実装、Unity 起動、実装テスト、Build、PR 作成は行わない。`tools/Artifacts` と `docs/handoff/BUILD_SYSTEM_ARTIFACT_STORAGE_*` を変更しない。旧 PR #98〜#103 / `cursor/debug-go-*-575e` のコード・設計を入力として渡さない。

## 1. A0 — 目的、現況、対象外

### 1.1 このスライスが答える問い

**既存 hierarchy の NodeId と inspector path `GameObject.ActiveSelf` を指定する一つの command を、Studio の既存 raw Commands から接続中の Windows Player へ送り、Update 契約に従って適用し、requestId 付きの結果と既存 inspector の読み戻しまで成立させられるか。**

要求の背景は Windows Player 上の GameObject の activeSelf、local transform、Renderer.enabled 等を DebugStudio から変更したいことである。r2 ではこれを §1.5 の縦切りスライスへ分け、本スライスは「書込み経路そのものの安全性」を最小の property 1つで閉じる。読取りと選択は既存 hierarchy / inspector が担当し、別の instanceId、検索一覧、選択 store、component ID 系列を導入しない。

旧計画が閉じられた理由は依頼者の入力として固定する。既存 NodeId と inspector があるのに instanceId・選択状態を二重化し、起動と Studio UI への配線も無く end-to-end で呼べなかった。旧スタックは参照・流用・cherry-pick しない。分割後も**各スライスは実 UI からの end-to-end 証拠で閉じる**ことで、配線無しで閉じた旧計画の失敗を繰り返さない。

### 1.2 現況の読取結果

r1 の読取りに、A2 で一次確認した事実（※）を加える。

- Studio は `tools/DebugStudio/src/DebugStudio.App` の .NET 8 WPF。`Start Listening` が本線 listener（既定 `ws://127.0.0.1:5011/debugsocket/`）を開始する。CLI control listener（5012）だけでは本線の開始にならない。
- Commands は raw `CommandType` / `PayloadJson` 入力、Send、requestId 付き履歴、結果 JSON 表示を既に持つ。`CommandWindowViewModel → CommandService → TransportCommandSender → SessionService → DebugStudioServerSessionTransport` が送信し、`SessionMessageRouter → CommandStore` が応答を相関する。
- Hierarchy UI に NodeId 列がある。Session パネルと status bar は `Unity: <name> / Session: <SessionId>` を表示する（※ `SessionWindowViewModel.SessionIdentityText`）。raw payload に必要な値は既存 UI から読める。WPF の XAML には AutomationId が一つも無い（※）。
- Inspector の再queryは別ノード→元ノードの再選択で発行される。Studio は `InspectorQueryFlags.IncludeRawValues` 付きで query する（※ `InspectorQueryService.cs`）。NDJSON export は Path / RawValue を保持する。
- Player の `DebugSocketService` は単一 current session を所有する。`DebugSocketRuntimeNodeRegistry` は hierarchy と inspector 共用の NodeId を発行する。**session 交換時に registry の対応表を `Reset` で消すが、採番値は戻さない**（※ `ActivateSessionAsync`）。したがって再接続後に旧 NodeId は解決できず、新しい object へ alias しない。
- **Welcome の `SessionId` は接続ごとの値ではなく、プロセス単位の `UnitySessionCorrelationContext.SessionId`（static、lazy 初期化1回）である**（※）。同じ Player 実行内の再接続では同じ値になり、Player の再起動で変わる。
- 受信は `DebugSocketClientSession.ReceiveLoopAsync` が1件ずつ `HandleInboundMessageAsync` を await する逐次処理で、router も `DispatchCommandAsync` の完了を await する（※）。書込みの完了待ちは、その session の ping / InspectorQuery / 後続 command を同じ時間だけ止める。
- `DebugSocketClientSession.CloseCoreAsync` は session の cancellation token を cancel してから loop の終了を待ち、最後の cleanup で `OnSessionClosed` を通知する。受信が書込み結果を await している間の閉鎖を、`OnSessionClosed` だけでは検知できない。
- router は受信 session が current でない message を、応答せずに捨てる。current 判定は `session is DebugSocketClientSession` と `ReferenceEquals(_currentSession, …)` で、具象型に依存する（※）。既存テストで `DebugSocketService` を構築しているものは無い（※）。
- `AbstractApplicationInitializer` は BeforeSceneLoad で UpdateSystemHost → DebugSocketService の順に生成し、AfterSceneLoad で SceneDirector を bind してから service を開始する。既定 `CreateDebugCommandDispatcher` は Null、SampleGame に override は無い。
- `UpdateCoordinator.EnqueueMainThreadApply` は ConcurrentQueue に積むだけで、どの thread からでも呼べる。`UpdaterDriver.LateUpdate` は毎フレーム `RunLateUpdate → ApplyMainThreadChanges → ApplyStructuralChanges` を呼び、**layer の pause や element の activation に関係なく** command buffer を実行する（※）。`RegisterElement` の activation は SceneDirector bind と scene 安定を待つが、それは初回の activation だけである。App 常駐サービスは Bootstrap が Coordinator を直接操作して activation する慣習がある（※ `UpdateSystemHost` のコメント）。
- `MainThreadApplyProcessor.Apply` と `MainThreadApplyCommandBuffer.ApplyAll` は例外を隔離しない。他 element の `ApplyMainThread` が例外を投げると、その frame の command buffer と `ApplyStructuralChanges` は実行されない（※ 既存の挙動）。
- Studio 側の hierarchy 更新は scene の loaded / unloaded / activeSceneChanged と明示的な `NotifyHierarchyChanged` だけが起点となる。SampleGame から service に触れる経路は無い（※）。実行時に生成した fixture は、生成時点が capability hello より後なら Hierarchy に出ない。ActiveSelf を書いても Hierarchy の flag 表示は更新されない。
- `DebugSocketInspectorBuilder` の `GameObject.ActiveSelf` は Boolean で、bool overload が RawValue に小文字 `true` / `false` を設定する（表示の ValueText は `True` / `False`）。inactive object も hierarchy に残る（flags で表現）。
- System.Text.Json 8.0.5 は Foundation の ZLogger 経由で既に Player に含まれ、plugin 設定は Any platform 有効（※）。`OneStarMaker.Runtime.asmdef` は overrideReferences=true で、このDLLを参照していない。.NET 8 の `JsonDocument` は**重複 key を拒否しない**（※ 既知の仕様）。
- wire の正本は `protocol/debugsocket`。既存 DebugCommand / CommandResult envelope の JSON payload で今回の command を表せる。generated DTO、MessagePack Key、MessageType の追加は不要。

### 1.3 次へ進める最低条件と制約

既存の一つの選択・識別経路のまま、ActiveSelf の成功と拒否を Studio UI から確認でき、Player が実行時点の対象・型・寿命を検査し、適用開始期限と Update 契約に従って書込みを確定するか、書かずに拒否することを最低条件とする。詳細は §2 に限定する。

Unity 版は `unity/ProjectSettings/ProjectVersion.txt` の 6000.6.0f1。Windows Player / WPF 実 UI の証拠は未確認で、§1.5 の先行スライスが操作経路を、本スライスの Phase C が書込み往復を担う。Editor 成功、mock、CLI 送信だけで実 UI の成立を代用しない。

### 1.4 対象外と後続の問い

次の各名称は所有スライス名であり、本 HANDOFF では作らない。§1.5 の分割で新設した名称を含む。

- `DebugStudio Validation / Player UI Route`（先行。§1.5-1）: AutomationId、UIA 操作支援、fixture と Hierarchy 出現、Player 起動引数、証拠の保存形。
- `DebugStudio Command / LocalPosition Write`（§1.5-3）: 数値配列の codec、有限値検査、RawValue の数値比較。
- `DebugStudio Command / Write Draft UX`（§1.5-4）: Inspector の Path / RawValue 列、選択中の NodeId / SessionId からの payload ひな形作成。
- `DebugStudio Command / Property Coverage`: LocalEulerAngles / LocalScale、Renderer.enabled の読取り追加、同型 component 複数の指定、その他 property の許可拡大。
- `DebugStudio Command / Batch Write`: 複数 property / 複数 object の一括更新、原子性、途中失敗の扱い。
- `DebugStudio Command / Undo`: Undo / Redo、履歴の寿命、ゲーム進行との競合。
- `DebugStudio Command / Record Replay`: 記録・再生、再接続を跨ぐ identifier、冪等性・再送保証。
- `DebugStudio Command / Authoring UX`: 汎用 typed editor、schema discovery、property ごとの capability、任意 reflection、スクリプト評価。
- `DebugStudio Command / Remote Access`: LAN / 認証 / 複数 Player・複数操作者。今回の証拠は同一 Windows host の loopback に限定する。
- `DebugStudio Command / Write Backpressure`: Apply 停止中に反復要求された場合の buffer 件数制限、admission の停止・復帰（§5.6）。
- 既存 UpdateSystem の apply 段階の例外隔離（§1.2 の既存挙動）。本スライスは自 command の例外を漏らさないことだけを担う。所有は UpdateSystem 側の後続とし、名称は owner が A3 で決める。

Asset Storage、ビルド配布基盤、SceneState、AssetOwner の設計変更、既存 Scene / Prefab / Addressables の広範な変更も対象外。最低条件未達を、上記へ書き移すだけで現スライスの成功にはしない。

### 1.5 分割方針（r2 で追加、D0）

r1 は、Runtime の書込み経路、2種類の値型、Studio の UI 補助、UIA 操作支援、fixture、Player 起動を一つの最低条件に束ねていた。これらは独立して失敗しうる未知であり（操作経路が無い、fixture が見えない、型変換が誤る、寿命 guard が漏れる）、一つ失敗すると他の成立を判定できない。そこで**縦切り**に分け、各スライスを実 UI の end-to-end 証拠で閉じる。横切り（Runtime だけ / UI だけ）にはしない。それは旧計画と同じ「配線無しで閉じる」形になるからである。

| 順 | スライス | 答える問い | 主な中身 | 規模見込み |
|---|---|---|---|---|
| 1 | `DebugStudio Validation / Player UI Route`（先行・別 HANDOFF） | AI が実 WPF Studio と Windows Player を起動し、fixture を Hierarchy で選択し、SessionId・NodeId・Inspector NDJSON を構造化して保存できるか（書込み無し） | 必要 control の AutomationId、UIA 限定 script、fixture の生成と Hierarchy 出現（生成時点または通知経路の設計判断を含む）、Player 起動引数、証拠の保存形 | 400〜700行 |
| 2 | **本 HANDOFF** | §1.1 | Runtime の書込み経路（codec・期限・guard・apply）、router 分岐、ActiveSelf の writer、契約文書。Studio のコード変更は無し | 本体約500〜850行、テスト約600〜950行 |
| 3 | `DebugStudio Command / LocalPosition Write` | 数値ベクトル値を同じ経路で安全に書けるか | 数値配列 codec、非有限・overflow の拒否、RawValue の数値比較。経路・guard は 2 を再利用 | 200〜350行 |
| 4 | `DebugStudio Command / Write Draft UX` | 人が raw JSON を書かずに、選択中の対象へ誤爆なく送れるか | Path / RawValue 列、ひな形作成（送信とは別操作、選択変更で黙って差し替えない） | 250〜400行 |

順序: 1 と 2 は A〜B を並行してよい。**2 の高コスト証拠（§5.2）は 1 の GO 後に取得する。** 3 と 4 は 2 の GO 後に着手する。各スライスは HANDOFF・ブランチを分け、着手時に作る。

**スライス 2 をさらに分けない判断:** 本スライスの内部（codec、gate、apply、router 配線）は、これ以上分けない。理由は次のとおり。

- I2 / I4 は admission → 適用確定 → setter → 完了通知の全区間に掛かる。分割すると、どの断片も単独では不変条件の合否を判定できない。
- 残る切り方は「臨界区間と単体試験だけ」と「配線と E2E」の横切りだけで、前者は E2E で閉じられない。これは旧計画の「配線無しで閉じる」形に戻る。
- 操作経路はスライス 1 へ、値型の拡張と入力補助は 3 / 4 へ分離した。残る並行性は gate の決定的な試験と service 配線の構造確認で評価し、Player 上での成立は本スライスの E2E で判定する。単体試験だけで実配線や Player の成立を保証しない。
- 規模は、本体約500〜850行、テスト約600〜950行、合計約1,100〜1,800行を見込む（契約文書は別）。行数やレビュー回数を成功の根拠にしない。現時点では、各責務を独立クラスと試験へ分けたまま、一つの書込み契約として受け入れるのが妥当と判断する。

分割の再検討は、計画した責務・試験境界で最低条件を満たせない一次証拠が得られたときに行う。D2 が承認されなければ A で decoder の方式を決め直す。codec だけのスライスを自動追加しない。臨界区間の実現可能性に具体的な疑義が残る場合は、owner が対象と終了条件を決めた限定スパイクを候補とする。

スライス 1 の遅延は分割の理由にしない。本スライスは B と発見 C まで進め、高コスト証拠の取得だけを待つ。

r1 にあった LocalPosition の契約案（`[x,y,z]` の JSON number 3要素、各要素の有限 float 検査、文字列数値・要素数違い・NaN / Infinity / overflow の拒否、`transform.localPosition` への一度の代入、invariant RawValue の数値比較で成分誤差 1e-5 以内）は、スライス 3 の A0 入力として引き継ぐ。

## 2. 意思決定と受け入れ境界（A3 r3 凍結）

以下の推奨案は §2.7 の owner 承認で採用済み。受け入れ条件と責務の意味は承認入力から変更しない。

### 2.1 進める最低条件

**不変条件**

- I1 許可していない path、欠落・不正・型不一致の value は setter に到達しない。入力の一部だけを適用しない。
- I2 **適用確定点**（§2.3-5。`_gate` 下で検査と `Pending → Applying` の遷移を一体で行う時点）より前に、接続の交換・終了、service の停止、別の Player 実行の payload、存在しない・失効した NodeId、破棄済み object、unloaded scene が検知された要求は、setter に到達しない。別 object を再探索・代入しない。適用確定点より後に起きた接続交換・切断は、確定済みの書込みを止めない。その結果は受理した接続にだけ返し、届かなければ破棄する。新しい接続へ付け替えない。
- I3 Unity API の対象解決と setter は main thread の `ApplyMainThreadChanges` 内だけで行う。受信 thread、単なる SynchronizationContext 切替、Update / LateUpdate の途中で値を変えない。
- I4 未適用の要求を成功と返さない。期限・取消・停止で失敗を返した要求は、後で適用しない。1要求の失敗・例外が他の UpdateSystem の Tick、後続 apply、構造変更を止めない。**確定前は適用開始期限で取消を試み、確定後は同じ Apply 呼出し内で setter・読戻し・要求の完了確定を同期的に行う。** 次 frame を要求の完了条件にしない。タイマーの発火・継続のスケジューリング・送信は実時間の上限保証ではなく、確定後のゲーム側 callback の実行時間にも固定上限を約束しない。
- I5 identifier と selection の正本を二重化しない。Game → Framework、Runtime → Foundation、Runtime / Editor の境界を破らない。

**成立条件**

- S1 `GameObject.ActiveSelf` を、通常の起動配線を通った Windows Player に実 WPF の Send から書ける。既存 command result に同じ requestId と成功が返り、既存 inspector の再queryで値を確認できる。
- S2 接続が生きている拒否要求は、同じ requestId の `Success=false` と機械可読 code を UI に返す。接続断・Studio 側 timeout は結果不明として扱い、無変更や成功と推測しない。
- S3 §1.5-1 の操作経路で、起動・選択・送信・再読取り・証拠保存を AI が行える。経路そのものの成立は先行スライスが判定し、本スライスは書込み往復の証拠だけを担う。

### 2.2 許可 property と一コマンドの契約

コマンド名の推奨は `debugsocket.set-property`（Ordinal 完全一致）。本スライスの許可は **`GameObject.ActiveSelf` の1 path のみ**。値は JSON boolean で、Unity の `GameObject.SetActive(value)` を一度呼ぶ。ActiveSelf=false の後も、同じ NodeId の inactive object に true を書けることを含める。書込みはその時点の代入であり、ゲームロジックが後で上書きしないことや Undo は保証しない。

既存 `DebugCommandEnvelopeV1` の PayloadJson は以下の形。

~~~json
{"version":1,"sessionId":"<Welcome.SessionId>","nodeId":42,"propertyPath":"GameObject.ActiveSelf","value":false}
~~~

- `sessionId` は **Player 実行の識別子**として扱う（§1.2: プロセス単位の値）。役割は、Player の再起動で NodeId の採番が 1 から戻ったとき、旧実行で得た NodeId を別 object に当てないこと。接続単位の古さは、受信 session の current 判定と、Apply 時の接続世代の比較（§2.3）で扱う。同じ実行内の再接続後に旧 payload を送った場合は、registry の Reset により `target-not-found` になる。
- `nodeId` は既存 NodeId の正の Int64。`propertyPath` は完全一致。`version` は JSON 契約の版で、wire schema version は変えない。
- トップレベルは一 object で、上記5 field が必須。重複 field（`JsonDocument` は拒否しないため、`EnumerateObject` で検出する）、未知 field、共通 field の null・型不正、余分な後続 JSON、不正な version / nodeId / sessionId は `invalid-payload`。value の null・非 boolean は `type-mismatch`。payload は UTF-8 4 KiB 以下、最大深さ4。

結果は既存 `DebugCommandResultEnvelopeV1` の RequestId / Success / Message / PayloadJson に返す。JSON は `version`、`code` を必須とし、解釈済みなら sessionId / nodeId / propertyPath を付け、成功時は setter 直後に読み取った `appliedValue` を付ける。Message は人間向けで、code 判定には使わない。

- `ok`: setter と直後の読取りが例外なく完了した。Success=true。**`appliedValue` が要求値と一致することは条件にしない**（要求値と直後の観測値は別々に記録する）。判定側は appliedValue と読み戻しで値を確認する。直後に自身が破棄されて読取りできない場合は `apply-failed`。
- `invalid-payload` / `property-not-allowed`（case 違い、他の GameObject・Transform・component path を含む）/ `type-mismatch`。
- `stale-session`: payload の sessionId が現在の Player 実行と不一致（受信側で判定）。**この code はこの1経路だけで返す。** 接続の交換・終了で無効になった要求は `cancelled`。受信 session 自体が current でない message は、router と admission が応答せず捨てる（既存挙動に合わせる）。
- `target-not-found`: unknown / Reset 済み / destroyed / scene 無効・unloaded。現行 resolver に合わせて統合し、区別のための tombstone を追加しない。
- `write-unavailable`: main thread context または `UpdateSystemRuntime.Coordinator` が無い。
- `busy`: 同じ service に未完了の書込みが既にある（§2.3-2）。
- `apply-timeout`: 適用開始期限までに適用確定点に到達しなかった。**setter に未到達であることを保証する。** 適用確定後に期限が来ても、この code は返さない（`ok` か `apply-failed` になる）。
- `cancelled`: 適用確定点より前に、停止・Dispose・接続の交換・終了（接続世代の不一致を含む）を検知した。setter 未到達。
- `apply-failed`: setter または直後の確認で例外、または確認不能。Success=false でも変更が全く無かったとは保証しない。自動 rollback / retry はしない。

優先順は decode・構造 → sessionId → 実行基盤 → path → value → admission。Apply 時は終端済みの要求を除き、`_gate` 下で 停止状態・受信 token の取消 → 接続世代 → NodeId / scene / 偽 null → `Pending → Applying` の CAS（適用確定点。期限・取消に負けていれば何もしない）→ lock 解放 → setter。接続が閉じて応答を届けられない場合、UI は既存の切断・timeout 表示を使い、成功を合成しない。

### 2.3 実行、所有者、起動

r1 の「専用 UpdateElement を登録し、LateUpdate から1件ずつ Apply queue へ送る」案は採らない。理由は §1.2 の通り、command buffer が毎フレーム layer と無関係に実行されるので element 無しで I3 を満たせるからである。加えて element は、layer pause 時に完了しない経路、登録・解除の寿命、App 常駐サービスの既存慣習との不一致を持ち込む。また受信が逐次 await なので、同時要求は事実上1件であり、r1 の上限32の FIFO queue は不要になる。

1. **受付（受信側）:** router は capability hello と DebugCommand の交渉後にだけ書込み専用 callback を呼び、受信 session と受信側の cancellation token を渡す。既存の ping / runtime-diagnostics 分岐、アプリ固有 catalog / dispatcher とは別の内部経路とし、一般 command の挙動を広げない。受信側では JSON 検証、sessionId 比較、admission だけを行い、Unity API と GameObject 参照に触れない。
2. **admission:** 未完了の書込み要求は service 全体で**最大1件**。未完了要求が既にあれば `busy`。admission は `_gate` 下で一体に行う。(a) 受信 session がまだ current か確認し、違えば応答せず捨てる（router の current 判定から admission までの間に交換された場合を塞ぐ）。(b) その時点の接続世代を要求に記録する。(c) 未完了 slot に登録する。lock を外してから世代を読むと、交換の直後に旧接続の要求へ新しい世代を付けてしまうので、3つを分けない。受信 token が既に取消済みなら要求を適用可能な状態で登録しない。受理後はその token の取消を `Pending → Cancelled` の CAS に接続し、登録直前・直後の取消も取りこぼさない。`OnSessionClosed` の通知は loop 終了後なので、それだけを取消の起点にしない。要求は、受理時の接続世代と完了 source を持つ `PropertyWriteRequest` とする。接続世代は単調増加の整数で、接続の交換・終了と Stop のたびに `_gate` 下で進める。session 参照そのものは guard に持ち込まない（テスト可能性のため。§3）。slot は要求が終端状態（Completed / TimedOut / Cancelled）に達したら再利用できる。解放は `_gate` 下で slot が当該要求と同じ場合だけ行い、遅れた旧要求の cleanup が新要求の slot を消さない。admission 時にも既存 slot の終端状態を確認する。期限切れの後に buffer に残った command は、resolver や setter を呼ばず何もしない。
3. **enqueue:** 受理した要求を包む `IMainThreadApplyCommand` を、注入された coordinator 取得元（既定は `UpdateSystemRuntime.Coordinator`）の `EnqueueMainThreadApply` へ直接積む。coordinator が null なら `write-unavailable`。UpdateElement は登録しない。scene 遷移中に Apply が来た場合も、resolver が unloaded / invalid scene を拒否する以外は通常どおり扱う（遷移中の書込みを禁止する契約は持たない）。
4. **待ちを2段に分ける:**
   - **適用開始期限（確定前）:** 受理と同時に開始する。期限の source は注入可能とし、既定は `CancellationTokenSource.CancelAfter`（推奨 2 秒）。期限到達は `Pending → TimedOut` の CAS を試み、成功すれば router は直ちに `apply-timeout` を返す。テストは明示的な cancel で期限到達を作り、`Task.Delay` / `Thread.Sleep` を使わない。
   - **結果待ち（確定後）:** CAS が `Applying` に負けた場合、期限では取り消せない。router は完了まで待つ。ただし確定から完了までは、同じ `ApplyMainThreadChanges` 呼出しの中の同期処理だけで構成する（`await`・yield・次 frame への持越しを禁じる。B の構造で確認する）。同じ呼出し内で終端結果を確定するが、router の再開や応答の到着が同じ frame になるとは保証しない。SetActive の同期 callback が長引けば結果待ちも長引く。ゲーム側 callback が戻らない場合は保証外とし、確定後の中断や別経路の ping 応答を今回追加しない。
5. **適用確定点と状態遷移:** `PropertyWriteRequest` は `Pending → Applying → Completed` と `Pending → TimedOut / Cancelled` を Interlocked の CAS で一度だけ遷移させる。**適用確定点は、Apply が `_gate` 下で (1) 停止状態・受信 token の取消、(2) 受理時の接続世代と現在の世代の一致、(3) NodeId・scene・偽 null の解決、を検査し、全て通った直後に同じ lock の中で `Pending → Applying` の CAS に成功した時点**と定義する。接続の交換・終了と Stop / Dispose も、`_gate` 下で世代を進めてから未完了要求の `Pending → Cancelled` を試みる。両者が同じ lock で直列化されるので、ある要求について「交換が先（setter 0、`cancelled`）」か「確定が先（setter に進む）」のどちらかに必ず決まる。期限・受信 token の取消は lock を取らずに CAS で競合し、勝った終端結果だけを完了通知する。Apply はまず終端済みかを確認し、終端済みなら resolver も呼ばない。検査で失敗した場合も `Pending → Completed` の CAS を経て失敗結果を確定し、先に勝った timeout / cancel を上書きしない。確定前の通常拒否・enqueue 不能もこの終端経路を使う。この臨界区間（admission、世代の前進と取消、適用確定）は §3 の `PropertyWriteGate` 一つに集め、service はその lock object として自分の `_gate` を渡す。service 側は `ActivateSessionAsync`・`OnSessionClosed`・`StopAsync` の既存 lock ブロックから世代を進め、受信 token を要求に渡す。停止・再開・Dispose の経路も AC8b で照合する。
6. **Apply（main thread、`ApplyMainThreadChanges` 内）:** 適用確定後に GameObject 参照を持ったまま **lock を解放してから** setter を呼ぶ。ゲーム側の OnEnable / OnDisable を service の臨界区間に抱え込まず、別 thread の接続交換等を長時間遮らないためである。同一 thread の lock は再入可能であり、lock 外にすること自体は callback による hierarchy 更新を防がない。guard は setter 前に完了させ、callback 後に registry の列挙や旧対応表が維持されていると仮定しない。確定後から setter 完了までに接続交換・切断が起きても、書込みは止めない（I2 の「確定後」に当たる）。完了時に、結果を受理した接続へだけ返す。その接続が既に閉じていれば結果は破棄され、Studio 側は既存の切断・timeout 表示になる。新しい接続へは付け替えない。
7. GameObject は取得時と使用直前に Unity の `== null` / `!= null` で確認する。`?.` / `??` / `is null` / `ReferenceEquals` を UnityEngine.Object に使わない。
8. command 自身が全例外を `apply-failed` に変換する。既存 buffer は例外を吸収しないので、ここから漏らすと同 frame の後続 command と `ApplyStructuralChanges` を止める。
9. **完了通知を Apply 段階の中で継続実行させない:** 完了 source は、待っている router の継続（直列化・`session.Enqueue`・次の受信の開始）を、完了を設定した呼出しの中で同期実行しない方式にする（例: `TaskCreationOptions.RunContinuationsAsynchronously` を指定した TaskCompletionSource。継続を同期実行する完了 source は使わない）。これは結果処理を Apply の呼出しスタックへ再入させないための境界である。別 thread の継続が Apply の return より先に並行して動くことまで禁止しない。timeout / cancel の完了待ちを Unity の次 frame に依存させない。
10. 接続の交換・終了、Stop / Dispose は、確定前の要求を `cancelled` へ遷移させる（手順5）。既に buffer にある command は、Apply 時の CAS に負けて何もしない。確定後の要求は手順6のとおり完了させる。**どの経路でも要求の完了 source は必ず終端の結果で完了させ、待っている router を取り残さない。** 破棄するのは、閉じた接続への結果の送信だけである。Dispose は確定後の要求の完了を待たないが、その要求は同じ Apply 呼出しの中で完了 source を完了させる。期限・受信 token の購読と timer は終端時に後始末する。callback の終了を待ちうる購読解除は `_gate` 外で行い、取消 callback と lock の相互待機を作らない。終端結果の確定・完了通知を cleanup の遅延で取り残さない。再接続後の自動再送は無い。

SetActive に伴う callback の作用全体を transaction にすることは対象外。拒否が setter 前なら値は不変、setter 到達後の例外は `apply-failed` で結果不明になり得る。この区別を I1 / I4 と矛盾させない。

### 2.4 受け入れ条件

- AC1（I1・S2）valid target へ未許可 `GameObject.Name`、case 違い `gameobject.activeself`、`Transform.LocalPosition`、未知 component path を送る → `property-not-allowed`、setter 呼出し0、before/after 一致。value に文字列 `"false"`・数値 0・null → `type-mismatch`。field 欠落・重複・未知 field・後続 JSON・4 KiB 超 → `invalid-payload`。いずれも無変更。
- AC2（I2・S2）未知 NodeId、Reset 済み NodeId、enqueue 後の DestroyImmediate、scene unload を試す → `target-not-found` で無変更。同名・新規生成の別 object へ fallback しない。未知と破棄済みを同じ code で返すのが合格。
- AC3（I2・I4・S2）(a) enqueue → 接続交換 → Apply で setter 0・`cancelled`、旧結果を新 session に載せない。(b) payload の sessionId を別実行の値にする → `stale-session`。(c) 同じ実行内の再接続後に旧 payload を送る → `target-not-found`。(d) **確定後の交換:** 適用確定 → setter 中（OnDisable から明示 signal で接続交換を起こす）→ setter 完了 の順を作る → setter は1回だけ実行され、結果は旧接続にだけ送られ、新接続には何も届かない。(e) **確定前の交換と確定の競合:** 同じ lock を巡る両方の順序を明示 signal で作り、各順序で「`cancelled` かつ setter 0」または「setter 1回かつ結果は旧接続のみ」のどちらかになる。不合格例: (a) で setter が呼ばれる、新接続に結果が届く、(d) で setter が中断・再実行される、(e) で `cancelled` を返したのに値が変わる。
- AC4（I3・I4）coordinator を段階駆動する。受理から `RunLateUpdate` までは値不変で、`ApplyMainThreadChanges` でのみ変更・完了し、続く `ApplyStructuralChanges` まで到達する。故障する要求（setter が例外）の後でも、別 element の Tick と次の正常要求が進む。背景 thread から setter を呼ぶ実装は不合格。
- AC5（I4・S2）coordinator null、未完了1件中の2件目（`busy`）、期限切れ直後の次の要求（slot が空いて受理され、buffer に残った旧 command は resolver / setter を呼ばない）、enqueue 後の Stop / Dispose、Apply 前の取消を試す。成功通知なし、終了した要求の後日適用なし。**全ての経路で完了 source が終端の結果で完了し、待っている側が残らない**。期限・受信 token の購読が解除されている。確定後に setter callback から Dispose した場合も、完了 source が一度だけ終端結果になり、送信だけが破棄される。成功を enqueue 時に返す実装は不合格。
- AC5b（I3・I4）**継続の位置:** 要求の完了設定を呼ぶ thread と呼出しスタックへの同期再入を thread-local マーカーで検査する。router 側の直列化・送信を、その完了設定の呼出し内で同期実行したら不合格。別 thread が Apply の return 前に並行実行するのは合格であり、全 thread 共通の「Apply 中」フラグでは判定しない。
- AC5c（I2）**admission と交換の競合:** router の current 判定の後、admission の前に接続交換を起こす → 要求は登録されず、応答もない。不合格例: 旧接続の要求が新しい世代を持って受理される。
- AC5d（I2・I4）**遅れた後始末:** 旧要求を timeout → 新要求を受理 → 旧 command を drain / 旧要求の cleanup を実行 → 3件目を送る、の順で3件目は `busy`、新要求は取消されず適用できる。接続交換後の旧 session の `OnSessionClosed` も新要求を取り消さない。
- AC5e（I2・I4）**接続閉鎖の早期取消:** `OnSessionClosed` をまだ呼ばず、受信 token だけを cancel → Apply と進め、`cancelled`・setter 0・待機完了を確認する。admission 前から取消済み、購読登録中の取消、適用確定後の取消も試す。確定後は `ok` / `apply-failed` で一度だけ完了し、新接続へ結果を付け替えない。
- AC6（I4）**期限:** enqueue 後に `ApplyMainThreadChanges` を呼ばずに期限を到達させる → `apply-timeout`。その後に `ApplyMainThreadChanges` を呼んでも setter 0。期限後、同じ session の次の message（ping）が処理される。期限到達と適用確定が競合する順序を明示 signal で両方作り、どちらでも「結果が `ok` なら値が変わり、`apply-timeout` なら値不変」が成り立つ。**確定後の期限:** setter 中（OnDisable から明示 signal）に期限を到達させる → `apply-timeout` は返らず、setter 完了後に同じ `ApplyMainThreadChanges` 呼出しの中で `ok` が確定する（追加の frame 駆動なしで完了 source が完了している）。不合格例: 確定後に `apply-timeout` を返す、完了に次の frame の駆動が必要になる。
- AC7（I3）**再入:** OnDisable で `DebugSocketService.NotifyHierarchyChanged` を呼ぶ component を持つ対象に false を書く → deadlock なし、`ok`、hierarchy 正本が一貫している（次の snapshot / delta が対象を含む）。結果伝達は偽 writer が要求値 false に対して観測値 true を返す試験で、`ok` かつ `appliedValue=true` を確認する。実 Unity の OnDisable 内で自身を再有効化できることを合格の前提にしない。
- AC8（I5）同名ノード2個のうち既存 Hierarchy で選んだ NodeId だけへ書く。新しい選択 store / instanceId の wire field / reflection 検索が無いことを構造で確認する。Framework→Game、Runtime→Editor の参照増加0。§2.6 で未承認の依存追加は不合格。
- AC8b（I2・I4、構造）**service の配線:** `ActivateSessionAsync`・`OnSessionClosed`・`StopAsync` の各々で、`_currentSession` を書き換える既存の `_gate` ブロックの中から `PropertyWriteGate` の世代の前進を呼んでいること、gate の lock object が service の `_gate` と同一であることを、C の構造レビューで確認する。具象 WebSocket を使う service 単位の交換テストは作らず、臨界区間は AC3 / AC5c の gate 単体試験で閉じる。`OnSessionClosed` の世代前進は、その session が current の分岐内だけで行う。Stop の早期 return / Dispose も取消を漏らさず、正規の再 Start 時には新接続の受付が復帰する。router から受信 token が要求まで渡り、`OnSessionClosed` より先の取消が配線されていることも照合する。この service 配線は単体試験でなく構造確認で判定する案とする。
- AC9（S1・S3）実 WPF で fixture を選択し、ActiveSelf を true→false→true と各1 command で往復する。各 Send の requestId、`ok` / appliedValue、再queryした同 NodeId・現実行の RawValue を照合する。比較対象の同名ノードは不変。
- AC10（S1・S2・S3）実 UI から少なくとも未許可 path と型不一致を送信し、requestId 付きの failure と読み戻し無変更を保存する。UI 入力→Send が観測できること。CLI / fake transport だけの成功は AC9/10 の代用不可。破棄・接続交換・期限の決定的順序は AC2/3/6 の Unity テストで閉じる。

### 2.5 判定と停止規則

スライスの GO は I1〜I5 と S1〜S3 の証拠が全て揃い、判定 C と最終 C' が揃う場合だけ。未実装・未検証・操作経路未成立は NO-GO / 未判定として残す。この通常実装スライスに CONDITIONAL ACCEPT は使わない。最低条件の達成後は §1.4 / §1.5 の機能を追加せず終了する。§1.5-1 が GO に到達しない間、本スライスは高コスト証拠を取得できず、GO にならない。その状態を本スライスの成功へ読み替えない。

A3 後の blocker は、凍結条件または常時契約と一次証拠を明記する。不変条件への違反は具体的な順序も示す。それ以外は後続入力にする。条件・所有者・依存・公開面の変更が必要なら Phase A を新 revision で再開する。A3 後の例外承認・決定記録は現在なし（未到達）。

### 2.6 A3 の採用案（D0〜D7）

- D0 分割: **§1.5 の4スライス縦切りと順序を推奨**。スライス 2（本 HANDOFF）の内部はこれ以上分けない（理由と再検討条件は §1.5）。採らない場合は、owner が優先したい問いを基に境界を改稿する。r1 の広い範囲へ自動的に戻さない。
- D1 許可集合: **ActiveSelf の1つ**（D0 採用時）。LocalPosition は §1.5-3 へ。
- D2 JSON decode の依存: **同梱 System.Text.Json 8.0.5 の JsonDocument を内部 codec で使う案を推奨**。DLL は ZLogger 経由で既に Player に含まれており、増えるのは `OneStarMaker.Runtime.asmdef` の precompiledReferences への `System.Text.Json.dll` の edge を候補とする。追加候補はまず `System.Text.Json.dll` に限定する。「直接必要なら任意に追加」を承認範囲にしない。A0/A1 ではコンパイルしていないため、追加の DLL edge が必要と判明した場合は B から A に返す。A3 で owner がこの候補と停止規則を承認する。公開 API に JsonElement を出さない。重複 key は自前で検出する。JsonUtility だけの型補完で欠落を false と誤適用しない。承認されない場合は decoder の配置・方式を A へ戻し、無断で参照追加・独自汎用 JSON 基盤・BuildContent 内部 parser への横断依存を作らない。
- D3 実行経路: **§2.3 の直接 enqueue、未完了最大1件、適用開始期限（既定 2 秒）と確定後の同期完了の2段の待ち、`_gate` 下の適用確定点、lock 外 setter、接続世代 guard を推奨**。受信 token の取消も確定前の取消へ接続する。Studio の既定 `CommandWindowViewModel.PendingTimeout` = 15 秒に対し、適用開始期限は 2 秒を推奨する。通信・スケジューリング・確定後の callback があるため、必ず Studio timeout より先に結果が届くという保証にはしない。
- D4 UI 範囲: **Studio のコード変更なし**（D0 採用時）。raw Commands に、UI に表示された SessionId と NodeId を使って JSON を入力する。ひな形と Path / RawValue 列は §1.5-4。
- D5 操作経路: **§1.5-1 の GO を本スライスの高コスト証拠の前提にする**ことを推奨。本スライスでは AutomationId・UIA script・fixture を作らない。
- D6 Player / fixture: §1.5-1 の成果物を使う。r1 の推奨（既存 BS4 Spring Player の起動流用、専用 flag で無更新 fixture を DependOnAll から作る）は §1.5-1 の A0 入力として引き継ぐ。§1.2 の Hierarchy 出現条件をそこで解く。
- D7 判定 C の照合: §5.4 の offline 実行方式を推奨。資格情報・本番設定・通信へ到達しない環境を担当が確保できるか A3 で確定する。できなければ A1 で照合 script を書く方式へ改稿し、B に判定 script を書かせない。

### 2.7 A3 凍結記録

- 決定者: リポジトリ owner（依頼者）。2026-10-12、この会話の「その推奨案でA3を凍結して、まず先行の『DebugStudio Validation / Player UI Route』のA0/A1を別ブランチ・別HANDOFFで作って。まだ実装には進まないで」に基づく。
- 承認入力: commit `afd40a945bdd0955f3fde819f5b1a8c7e67d8626` の r3-draft。主担当は Codex（GPT-6 系 / OpenAI）。
- **採用:** D0 の4分割、D1 の ActiveSelf のみ、D2 の System.Text.Json.dll 参照候補と追加依存判明時の A 差戻し、D3 の直接 enqueue / 2秒の適用開始期限 / gate / 接続世代・受信 token / 同期完了、D4 の既存 raw UI、D5 の先行 GO、D6 の先行スライスへの起動・fixture 分離、D7 の判定 C による offline 照合方式。
- **固定する境界:** §1 の問い・範囲・順序、I1〜I5 / S1〜S3、AC1〜AC10（枝番を含む）、§3 の責務・依存、§4 の停止規則、§5.1〜5.4 の試験・E2E・証拠方式。既存点検の対策は r3 の本文に統合された形で採用する。
- **不採用:** instanceId / 選択 store の新設、専用 UpdateElement、複数 property と入力補助の同時実装、完了の実時間上限保証、単体試験だけによる実 UI / Player の合格。
- **後続へ移送・残存リスク受容:** §1.4 の問いと §5.6 の Write Backpressure。取消済み entry の反復蓄積や長時間 callback は本スライスの保証外。単一要求の取消後不適用と完了通知は必須のまま。
- **独立性:** 同一版への複数独立 A2 は未充足、A0 のみの代替構成レビューは未実施。owner の追加モデル点検終了指示と今回の凍結承認に基づき進める。独立レビュー済みの件数・範囲を水増ししない。将来の C / C' を省略した承認ではない。
- **未検証と担当:** 実 UI 操作経路は未確認。先行スライス C が成立を判定し、本スライス C が変更済み head で書込みを検証する。D7 の隔離環境は C 担当が実行前に成立を確認する。成立しなければ未実行として停止し、方式変更は A の新 revision へ返す。
- **開始範囲:** 今回はこの凍結と先行スライス A0/A1 のみ。実装・起動・Build の開始指示ではない。凍結後に条件の意味を変える場合は新 revision とする。

## 3. 責務マップ

パスは repository 相対。行数は implementation base の物理行数、増分は概算。新規名は配置案でまだ存在しない。Unity 側は `OneStarMaker.Runtime` と既存 Foundation の Update 契約を使う。

**テスト seam の原則（r2）:** guard・状態遷移・codec は `DebugSocketService` と `DebugSocketClientSession`（具象 WebSocket）に依存しない独立クラスに置く。臨界区間は `PropertyWriteGate` に集め、lock object（service の `_gate`）を注入する。service はそれらを組み立て、registry 解決・coordinator 取得を callback で渡し、既存 lock ブロックから gate の世代前進を呼ぶだけにする。これで AC3〜AC6 を本物の socket なしで、`new UpdateCoordinator()` を段階駆動して試験できる。service 側の世代前進・停止復帰と受信 token の配線は AC8b の構造確認で判定する。

### 3.1 Unity Runtime（prefix: unity/Assets/OneStarMaker/Scripts/Runtime/）

- `DebugSocketServices/Commands/PropertyWriteCodec.cs`（新規0→100〜160行）: JSON の形・型・許可 path・結果 code の policy。無状態、internal。Unity object / session / 通信を参照しない。重複 key・後続 JSON・深さ・サイズ・型不一致を単体で閉じる。
- `DebugSocketServices/Commands/PropertyWriteRequest.cs`（新規0→60〜100行）: 1要求の状態遷移（CAS）、受理時の接続世代、完了 source（継続を非同期実行する方式）、期限・受信 token の購読と解除。service 所有、要求寿命。Unity 非依存で、期限と確定の競合を明示 signal で試験する。
- `DebugSocketServices/Commands/PropertyWriteGate.cs`（新規0→80〜130行）: 臨界区間の所有者。注入された lock object（service の `_gate`）の下で、admission（current 確認・世代記録・slot 登録）、世代の前進と未完了要求の取消、適用確定（停止・世代・解決 callback → CAS）を行う。停止状態、接続世代、未完了 slot を所有する（service 寿命）。Unity 非依存で、解決は callback で受ける。AC3 / AC5 / AC5c / AC6 の競合を、本物の socket なしに明示 signal で試験する。
- `DebugSocketServices/Commands/PropertyWriteApplyCommand.cs`（新規0→60〜90行）: `IMainThreadApplyCommand` の実装。gate の適用確定（lock 内で guard → CAS）→ lock 外で writer 呼出し → 例外の `apply-failed` 化 → 完了 source の完了。注入した gate と偽 resolver、実 GameObject の両方で試験する。
- `DebugSocketServices/Commands/GameObjectPropertyWriter.cs`（新規0→40〜70行）: ActiveSelf の setter と直後の読取り。状態を持たない Unity I/O、internal。GameObject を呼出し中だけ受け取り、保持しない。後続スライス 3 は、ここに LocalPosition を足す。
- `DebugSocketServices/DebugSocketService.PropertyWrite.cs`（新規0→60〜100行）: codec・gate・apply command・writer の組立て、受信 token の受け渡しと、registry 解決 callback（`_gate` 下で呼ばれる前提）、coordinator 取得元と期限 source の保持。service partial 内の private 接続に限定し、public resolver を作らない。codec・setter・状態遷移・臨界区間の本体は置かない。
- `DebugSocketServices/DebugSocketService.cs`（391、+15〜30）: coordinator 取得元と期限 source を受ける **internal** constructor overload（public constructor は維持し、既定値で委譲）、`StopAsync` の既存 lock ブロックからの gate 世代前進（停止）、Dispose 経路。
- `DebugSocketServices/DebugSocketService.Session.cs`（170、+5〜15）: `ActivateSessionAsync` / `OnSessionClosed` の既存 lock ブロックから gate の世代前進を呼ぶ（AC8b）。session の所有者は変えない。
- `DebugSocketServices/DebugSocketService.Inbound.cs`（120、+10〜20）と `DebugSocketServices/Protocol/DebugSocketInboundMessageRouter.cs`（277、+20〜40）: capability 確認後の書込み専用分岐と session / 受信 token の callback への受け渡し。router は fake `IDebugSocketInboundSession` で、未交渉拒否・requestId・期限までの await を試験する。
- `OneStarMaker.Runtime.asmdef`（30、+1〜3 の候補）: D2 の precompiled DLL 参照。Framework assembly 間の edge、Game 参照、Editor 参照は追加しない。

再利用し原則変更しない: registry（140）、Inspector builder（296）、MainThread dispatcher（52）、built-in handler（81）、UpdateSystemRuntime（67）、UpdateCoordinator（470）、AbstractApplicationInitializer（1196）。bootstrap は既に500行超だが、今回は新責務を積まない。

### 3.2 Studio

変更なし（D4）。既存の CommandService / SessionService / Inspector 経路を使う。

### 3.3 検証・文書

- 新規 `unity/Assets/OneStarMaker/Tests/Foundation/` 配下に、責務別の `PropertyWriteCodecTests.cs`（120〜200行）、`PropertyWriteRequestTests.cs`（100〜160行）、`PropertyWriteGateTests.cs`（150〜250行、admission・交換・停止・期限・確定の競合）、`PropertyWriteApplyTests.cs`（150〜250行、実 GameObject・coordinator 段階駆動・再入・継続の位置）を置く。router の分岐は既存方針に沿って fake session で試験する。
- 実装後に `protocol/debugsocket/README.md` へ JSON command 契約を記載する（生成 envelope は無変更）。

追加総量は、本体約500〜850行、テスト約600〜950行、計約1,100〜1,800行＋契約文書を見込む。個別ファイルの増分を合算し直した概算であり、テストを含め750〜1,100行に収まるとは見積もらない。

## 4. 後続の実装計画（設計凍結済み・着手待機）

A3 凍結後の B は、(1) codec と単体試験、(2) request の状態遷移・期限・受信 token の取消と gate の臨界区間、(3) apply command と writer、(4) service / router / session 交換・停止の配線、(5) 契約文書と機械検査、の順を推奨する。読取り / selection を新設する作業は無い。

B は編集する Unity C# の先頭に `#nullable enable` を置き、Unity の record 禁止、ILogger<T> の公開契約、偽 null の契約を守る。SceneState の既存14値と順序・所有者を変えず、例外隔離を保つ。

B で計画外の状態、依存、owner、寿命、公開 API や reader / component 識別が必要になったら、停止して A へ返す。D2 未決のまま依存を追加しない。Unity Editor 操作とコンパイル確認は B の許容範囲、Unity テストと Build は C。今回は H1/CURRENT や B 限定 Unity test の適用を提案しない。

## 5. テストとレビュー計画

### 5.1 安い確認と判定必須テスト

本文書の更新で行うのは `pwsh tools/docs-audit.ps1` と `git diff --check` のみ。実装テストの合格は記録しない。

- Unity 単体: codec、request の遷移・期限・取消、registry 失効・偽 null、実 GameObject の setter と再入、coordinator 段階駆動での実行時点と例外隔離。`Task.Delay` / `Thread.Sleep` を使わず、明示 signal と段階駆動で順序を作る。
- 差し戻し中の Unity 起点 filter: `OneStarMaker.Tests.Foundation.PropertyWriteCodecTests;OneStarMaker.Tests.Foundation.PropertyWriteRequestTests;OneStarMaker.Tests.Foundation.PropertyWriteGateTests;OneStarMaker.Tests.Foundation.PropertyWriteApplyTests;OneStarMaker.Tests.Foundation.DebugSocketRuntimeNodeRegistryTests;OneStarMaker.Tests.Foundation.DebugSocketInspectorBuilderTests`（新規名は A3 で固定）。影響根拠があれば BuiltInCommandHandlerTests と UpdateCoordinator / MainThreadApplyProcessor のテストを追加する。
- .NET: Studio は変更しないため、判定必須は `dotnet test tools/DebugStudio/DebugStudio.sln` の回帰（TRX 保存）だけとする。
- 判定必須: 上記 .NET 回帰、`pwsh tools/run-tests.ps1 -Platform EditMode -Filter ''`（最終全件。XML の件数>0・failed=0 と対象名を確認）、contract-audit / docs-audit、§5.2 の実 Player / UI 証拠。全 EditMode の適用除外なし。
- B は Unity test / Build を実行しない。C の Unity test は最初から承認済みの sandbox 外経路で標準 runner を使う。`unity test` / `unity run` は使わない。既存の人間 / Claude 所有の Editor を無断で終了しない。native 終了 stall は既知の停止規則を引き継ぎ、今回から調査回数をリセットしない。

### 5.2 end-to-end の取得経路

**前提: §1.5-1 `DebugStudio Validation / Player UI Route` の GO。** それまでは未確認として扱う。経路（listener 開始、Player 起動引数、fixture の Hierarchy 出現、選択、Inspector export、AutomationId と UIA script）は先行スライスの成果物を使う。本スライスで足す手順は次だけである。

1. 対象 head の Player と Studio を、先行スライスの手順で起動・接続する。既存 Player を、変更済み head の証拠として流用しない。
2. UI から SessionId（`SessionIdentityText`）と、fixture 片方の NodeId を読み、比較対象の別 NodeId も記録する。
3. Commands に `debugsocket.set-property` と payload を入力し、実 Send ボタンを Invoke する。AC9 の各要求について、入力 JSON・requestId・結果 JSON を保存する。
4. 別 node → 対象 node の再選択で再queryし、新しい revision の detail を NDJSON export する。false 後も対象が Hierarchy に残り、true へ戻せることを確認する。
5. AC10 の拒否を実 UI で実施し、対象・比較対象の無変更を読み戻す。破棄・接続交換・期限の race はこの手順で作らず、決定的な Unity テストの証拠を使う。CLI `send` は補助で、UI 経路の代用にしない。
6. 保存は、対象 commit、Player Build と receipt の対応、操作時の PID・URI、操作手順ログ、入力 / 結果 JSON、before/after の Inspector NDJSON、必要 control と結果が見える画面、テストログ / XML / TRX。値の合否は構造化値で判断し、画面は補助証拠とする。

どこが不成立かを、経路（先行スライスの範囲）と書込み（本スライスの範囲）に分けて報告する。経路の不成立を本スライスで直さず、先行スライスへ返す。

### 5.3 高コスト証拠と受け渡し

Windows Player Build と実 WPF→Player 往復を高コスト証拠とする。判定 C の安い必須試験 → 暫定 C' → 阻害指摘の無い head で高コスト証拠を一回取得 → 高コスト証拠を含む判定 C と最終 C'、の順。欠陥で head が変わったら旧 head の合格を流用しない。

独立した期待値は AC9 の定数（true→false→true）と、AC10 の所定 failure・無変更。期待値を実装の出力から生成しない。比較対象ノードの不変も before/after で判断する。

C / C' 用の snapshot・生証拠は、生成時刻と内容 hash を固定 manifest に持たせ、既存 Artifact Storage の Evidence 保存・取得規約に従って非秘密の file 集合を渡す。tracked 本文には取得案内の担当・状態だけを追記し、reference、package hash、内部 catalog、機微なローカル path を転記しない。

### 5.4 判定 C の照合 script の方式

推奨は **判定 C の offline 実行**。C 担当が資格情報・本番設定・通信へ到達しない環境で、原記録の複製だけを入力とする script を書いて実行する。B は最終合否 script を作らない。

A3 で凍結する合成 fixture の期待値: (a) AC9 の定数と正しい相関・再queryが全てある→pass、(b) value 不一致→fail、(c) `ok` でも Inspector が前 revision のまま→fail、(d) requestId / sessionId / NodeId の対応違い→fail、(e) 拒否ケースで値が変化→fail、(f) 必須要求または UI 操作記録の欠落→fail。全 fixture を通してから原記録へ適用する。

### 5.5 担当と実績

- A0/A1 r1: Codex（GPT-6 系 / OpenAI）。同じ担当系統の調査 subagent が Studio と Runtime のコード読取を分担した。独立レビューには数えない。
- A2（r1 対象）: Claude Opus 5.5（Anthropic）、2026-10-12。入力は r1（commit `dc2d55a`、内容 SHA-256 `9549237404d65415f750f9e9e9fd76a1e707a2cc8c8d5b6fa67fa873c4eee7f8`）。観点はアーキテクチャゲートと、寿命・失敗順序・検証経路の兼任。A1 初稿を読んでいるので、A0 のみからの代替構成レビューには数えない。
- A1 r2: 同じ Claude session が、owner の指示により r1 の A2 指摘を反映し、R1〜R4・P1〜P7 の追加点検を反映した。改稿担当の自己点検であり、独立 A2 の追加件数には数えない。
- A1 r3: Codex（GPT-6 系 / OpenAI）の主担当が既存の点検結果とコードを照合し統合。owner の 2026-10-12 の指示「他モデルに点検してもらうのはこれで最後」に従い、追加のモデル点検を終了する。この主担当点検も独立 A2 には数えない。高リスクに対する同一版の複数独立レビューは未充足。A0 のみの代替構成レビューは検討したが今回追加せず未実施とし、達成済みに読み替えない。
- A3: 2026-10-12 owner が r3 推奨案を承認。主担当が §2.7 に採否・理由・独立性・開始範囲を記録し凍結。
- C' 予約: 未割当。A では OpenAI 系と Anthropic 系を使用済み。強化条件のため、それ以外の系列・ベンダーか人間を残す。

#### r1 A2 指摘と r2 での扱い

採否は owner の指示「修正してよい」に基づく暫定のもので、A3 で owner が確定する。

| # | 指摘（r1 に対して） | r2 での扱い |
|---|---|---|
| 1 | 書込み完了の await が逐次受信ループを止め、layer pause 等で永久に待つ。上限32 queue は同時1件の実態に合わない | 反映: 期限と `apply-timeout`、CAS による後日適用の禁止、未完了最大1件と `busy`（§2.2, §2.3-2/4/5, AC6） |
| 2 | UpdateElement 経由は不要で、pause 依存と寿命管理を持ち込み、App 常駐サービスの既存慣習とずれる | 反映: 直接 enqueue に変更し element を廃止（§2.3） |
| 3 | guard が具象 session と static coordinator に依存し、AC3〜5 を単体試験できない | 反映: 接続世代、独立クラス、internal ctor での注入（§2.3-2, §3 冒頭） |
| 4 | 実行時に生成した fixture が Hierarchy に出ない恐れ。Hierarchy flag は書込み後に古い | 移送: §1.5-1 の問いに含める（D5/D6）。flag の件は §1.2 に注記 |
| 5 | setter を `_gate` 内で呼ぶと OnEnable/OnDisable の再入で正本を書き換える | 反映: lock 外 setter と、受け入れる窓の明記（§2.3-6, AC7） |
| 6 | `ok` と appliedValue≠要求値の関係が未定義 | 反映（§2.2） |
| 7 | 非 current session への `stale-session` は返らない | 反映（§2.2） |
| 8 | `JsonDocument` は重複 key を拒否しない | 反映（§2.2, D2） |
| 9 | 既存 apply 段階の例外非隔離 | 移送: §1.4 の UpdateSystem 側後続。残存リスクとして記録 |
| 10 | Welcome.SessionId はプロセス単位で、接続の古さを判定できない（改稿中の追加確認） | 反映: sessionId を Player 実行識別子として再定義し、接続は世代で判定（§1.2, §2.2, AC3） |
| 11 | 範囲が独立した未知を束ねている | 反映: §1.5 の分割を D0 として提案 |

#### r2 草稿への所見と扱い

owner 経由で受け取った r2 草稿への所見。r2 の改稿で次の4点を反映した。後続の主担当統合は §5.6。

| # | 所見 | 扱い |
|---|---|---|
| R1 | 「受信停止は最大2秒」は保証できない。`Applying` に進むと期限で取り消せず、SetActive の同期 callback が長引けば待ち続ける | 反映: 待ちを「適用開始期限（確定前）」と「同期完了（確定後、frame を跨がない）」に分け、I4 と `apply-timeout` の意味をそれに合わせた。ゲーム側 callback が戻らない場合は保証外と明記（§2.1 I4, §2.2, §2.3-4, AC6） |
| R2 | 旧接続への書込み禁止と、交換後に適用される窓の受容が食い違う | 反映: 適用確定点を `_gate` 下の検査と CAS の一体として定義し、I2 を確定前・確定後で書き分けた。交換・停止も同じ lock で直列化し、確定前は `cancelled`、確定後は完了させ旧接続にだけ返す（§2.1 I2, §2.3-5/6/10, AC3 (d)(e)） |
| R3 | Dispose で「完了 source を解放」は曖昧。待っている router を取り残す | 反映: 全経路で完了 source を終端の結果で完了させ、破棄は閉じた接続への送信だけと明記（§2.3-10, AC5） |
| R4 | 責務マップの apply command の順序（遷移確定 → guard）が本文と逆 | 反映: guard → CAS で確定 → lock 外 setter に修正（§3.1） |

#### A3 前の事前点検（凍結 → B → C/C' を想定した自己点検）

R1〜R4 の反映後に、実装後の C/C' で突かれうる穴を同じ担当が洗った結果。独立レビューではない。

| # | 穴 | 扱い |
|---|---|---|
| P1 | admission で受信 session の current 確認と世代の読取りが分かれていると、交換の直後に旧接続の要求が新しい世代を持って受理される | 反映: 3操作を `_gate` 下で一体化（§2.3-2, AC5c） |
| P2 | 継続を同期実行する完了 source だと、router と受信ループのコードが `ApplyMainThreadChanges` の中で走り、I3 と例外隔離が崩れる | 反映: 継続の非同期実行を必須化（§2.3-9, AC5b） |
| P3 | 「独立クラスで試験」と言いつつ臨界区間（`_gate` 下の検査と CAS）が service partial に残り、具象 session なしに試験できない | 反映: 臨界区間を `PropertyWriteGate` に集約し、lock object を注入。service の配線は構造確認で判定する案（§2.3-5, §3.1, AC8b）。r3 で受信 token の配線も追加 |
| P4 | 世代不一致に `stale-session` と `cancelled` の2つの code が割り当たっていた | 反映: `stale-session` は payload の sessionId 不一致だけ（§2.2） |
| P5 | 期限切れ後の slot の解放時期と、buffer に残った旧 command の扱いが未定義 | 反映: 終端状態で解放し、旧 command は CAS に負けて何もしない（§2.3-2, AC5） |
| P6 | 期限値が A2 送りで、凍結時に未確定だった | 反映: Studio の既定 15 秒を確認し、適用開始期限 2 秒を推奨（D3、A3 未凍結） |
| P7 | AC5 が廃止した「登録」の保持を検査対象に残していた | 反映: 削除（AC5） |

### 5.6 主担当の見立てと r3 の統合判断

2026-10-12。以下は採用を勧める主担当の判断であり、owner の A3 凍結記録ではない。

- **4分割を支持する。** 先行の操作経路、本体の ActiveSelf、数値型拡張、入力補助は別の問いとして判定できる。ActiveSelf 本体はこれ以上分けず、codec から実 UI 往復までを一つの受け入れ単位にする。小さいのは許可 property の集合であって、並行性や寿命の設計リスクではない。
- **r2 の R1〜R4 / P1〜P7 の対策を採る。** ただし「単体試験で全て証明」「残る未知なし」「必ず2秒以内に受信再開」は採らない。実配線、Unity callback、Player 上の DLL / stripping、UI 経路はそれぞれの確認地点で初めて判断できる。
- **取消の起点を補う。** 一次根拠は `Transport/DebugSocketClientSession.cs` の `CloseCoreAsync → loop 終了待ち → CleanupManagedResources → OnSessionClosed`。受信 token の取消を request に接続し、閉鎖中に未確定の書込みを進めない（AC5e）。旧 session の終了通知で新接続の世代を進めない（AC5d / AC8b）。
- **終端と後始末を分けて明確化する。** 拒否も CAS で終端化し、結果を上書きしない。slot の解放は要求の同一性を確認し、購読の後始末は lock 外で行う。Dispose 後も要求の完了通知は成立させる（AC5 / AC5d）。lock 外 setter はゲーム側 callback を臨界区間から外すためであり、callback の再入を禁止する仕組みではない。
- **計画の説明と規模を修正する。** AC5b は別 thread との時間的重なりではなく、完了設定への同期再入を検査する。ActiveSelf の RawValue は既に小文字 boolean。要求値と観測値の違いは偽 writer で試験し、未確認の OnDisable 内の再有効化挙動を前提にしない。総量はテストを含め約1,100〜1,800行＋文書を見込む。D2 は承認する DLL edge を限定し、未確認の追加依存を包括承認にしない。
- **残存リスクを受け渡す。** 未完了 slot 1件は物理 buffer の件数上限ではない。`MainThreadApplyCommandBuffer` は取消済み entry を取り除く API を持たないため、Apply が止まったまま人が要求を繰り返すと no-op entry が蓄積する。今回は自動 retry・負荷耐性・長期停止中の反復運用を保証しない。bounded queue / 停止中の admission 方針は `DebugStudio Command / Write Backpressure` へ送る。単一要求の取消後不適用は本スライスの必須条件のままにする。owner は D3 の採否時にこの残存リスクも判断する。
- **進め方:** 操作経路スライスを先に立ち上げ、本体は A3 後にその共有境界を使って実装できる。E2E の未成立を理由に本体だけで GO にしない。追加モデル点検は依頼せず、この案と独立性の未充足を owner の A3 に渡す。将来の C / C' は未実施の計画として残し、今回の指示をその合格や省略に読み替えない。

## 6. Phase B 実装結果

未着手。implementation head 未固定。成果物・取得案内は未生成。

## 7. Phase C

未実施

## 8. Phase C'

未実施

## 9. Phase D

未到達。マージ・harvest・Evidence 保存引渡し・task 終了を行わない。本 HANDOFF は凍結 r3 として保持し、本スライスの B は開始待機。
