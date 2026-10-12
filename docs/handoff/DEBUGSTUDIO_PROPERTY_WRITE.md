# DebugStudio: NodeId と Inspector path による単一 property 書込み

## 0. メタデータと引継ぎ票

- type: `slice`
- status: `A`（A0 入力固定・A1 初稿まで。A2 未実施、A3 未凍結）
- branch: `codex/debugstudio-property-write-a01`（`origin/develop` 起点）
- implementation base commit: `3f79639c12eac16da77d06608675564e906f9a49`
- implementation head commit: 未固定（実装未着手。本文だけの commit を implementation head にしない）
- Phase A revision: `r1-draft`。§1 は A0 packet、§2〜5 は A1 推奨案。実装許可ではない。
- risk: `high`（Player の状態変更、session と object の寿命、受信と Update の並行境界）
- owner: リポジトリ owner（依頼者）。A3 の採否・凍結を決定する。
- created: 2026-10-12
- expires: 2026-11-12 または置換 revision の成立時。期限で自動採用・実装しない。
- harvest to: `protocol/debugsocket/README.md` の command 契約、`unity/Assets/README.md` の DebugStudio 案内、`tools/DebugStudio/src/DebugStudio.Cli/README.md` の既存送信経路。実装後の現況だけを反映する。
- task: `debugstudio-property-write`。通常 Workflow の開始を記録済み。今回の Phase 停止を task 全体の終了に変換しない。
- Phase A snapshot の取得案内・状態: A3 凍結 snapshot は未生成。A2 入力は本ブランチの commit 済み HANDOFF。A2 開始担当が取得版の内容 SHA-256 を正、commit を補助として同じ入力版を固定し各担当へ渡す。
- Phase B result snapshot / evidence bundle / C' blind bundle の取得案内・状態: 全て未生成。作成 Phase の担当が既存引渡し先へ案内する。現時点で保存・配布済みと扱わない。
- PR: 未作成（依頼範囲外）。後日作る場合も base は `develop`。

**引継ぎ票 — 次は A2。** 次の役割は独立設計レビュー担当。リモートの本ブランチと §0 の A1 状態を照合し、同一内容版の §1〜5 を入力にする。PR は存在しないため PR 状態ブロックの照合は適用外。少なくともアーキテクチャゲートと寿命・失敗順序・検証経路を分担し、互いの指摘を見せない。高リスクのため A0 のみからの代替構成レビューも検討する。A3 は owner が §2.6 を裁定する。

A2 に必要なのはリポジトリ読取とレビュー成果物の作成権限。Unity・Player Build・実 UI の起動権限を今回の引継ぎから推測しない。A0/A1 はコード・文書の読取と本文作成、文書検査、commit・push で停止する。A2、A3、実装、Unity 起動、テスト、Build、PR 作成をこの session で行わない。並行する Claude の作業ツリー・プロセスを操作しない。`tools/Artifacts` と `docs/handoff/BUILD_SYSTEM_ARTIFACT_STORAGE_*` を変更しない。旧 PR #98〜#103 / `cursor/debug-go-*-575e` のコード・設計を入力として渡さない。

## 1. A0 — 目的、現況、対象外

### 1.1 このスライスが答える問い

**既存 hierarchy の NodeId と inspector の許可した property path を指定する一つの command で、Studio UI → 接続中の Windows Player → 書込み結果 → 既存 inspector の読み戻しまで成立するか。**

要求の背景は Windows Player 上の GameObject の activeSelf、local transform、Renderer.enabled 等を DebugStudio から変更したいことである。今回の問いは任意 property 編集器を作ることではない。読取りと選択は既存 hierarchy / inspector が担当し、別の instanceId、検索一覧、選択 store、component ID 系列を導入しない。

旧計画が閉じられた理由は依頼者の入力として固定する。既存 NodeId と inspector があるのに instanceId・選択状態を二重化し、起動と Studio UI への配線も無く end-to-end で呼べなかった。旧スタックは参照・流用・cherry-pick しない。現況は上記 develop commit のコードだけから確認した。

### 1.2 現況の読取結果

- Studio は `tools/DebugStudio/src/DebugStudio.App` の .NET 8 WPF。CLI、Client、Server、Contracts、Export とテストが `tools/DebugStudio` にある。
- `AppCompositionRoot.CreateShellComposition` が `DebugStudioServerSessionTransport`、`SessionService`、store、view model を組み立てる。Session UI の `Start Listening` が本線 listener を開始する。既定は `ws://127.0.0.1:5011/debugsocket/`。CLI control listener（5012）が動いているだけでは本線の開始にならない。
- Commands は raw `CommandType` / `PayloadJson` 入力、Send、requestId 付き履歴、結果 JSON 表示を既に持つ。`CommandWindowViewModel → CommandService → TransportCommandSender → SessionService → DebugStudioServerSessionTransport` が送信し、`SessionMessageRouter → CommandStore` が応答を相関する。
- `HierarchyViewModel.SelectedNode → HierarchyStore.SelectedNodeId → InspectorQueryService.RequestDetailsAsync` が選択と読取りを所有する。UI に NodeId 列がある。`InspectorStore` は別 target / 古い revision の detail を拒否する。
- Inspector の DTO と `InspectorPropertyRecord` には `Path` と `RawValue` があるが、`InspectorPropertyItemViewModel` は両方を表示モデルへ渡していない。UI には Path 列がない。NDJSON export は Path / RawValue を保持する。同じ選択への hierarchy 更新だけでは再queryされず、現行 UI では別ノードを選び直して戻ることで再読取りする。
- Player の `DebugSocketService` は単一 current session を所有する。`DebugSocketRuntimeNodeRegistry` が hierarchy と inspector 共用の NodeId を発行し、Reset でも同じ service の採番値は戻さない。resolver は存在しない token、偽 null、invalid / unloaded scene を拒否する。service を作り直した別実行間では NodeId だけの永続一意性は保証しない。
- `AbstractApplicationInitializer` は UpdateSystemHost → DebugSocketService の順に生成し、AfterSceneLoad で service を開始する。既定 `CreateDebugCommandDispatcher` は Null、SampleGame に override は無い。Catalog は opt-in の同期 API。既存一般 dispatcher があることと、今回の書込みが起動から届くことは別である。
- `DebugSocketInboundMessageRouter` は ping / runtime-diagnostics を capability 判定前に処理する。書込みをこの無条件分岐へ加えない。一般 command callback は現在 session を受け取らない。`MainThreadDebugCommandDispatcher` は SynchronizationContext に戻すが Update phase を固定しない。
- `DebugSocketInspectorBuilder` の既存 path は `GameObject.ActiveSelf`、`Transform.LocalPosition`、`Transform.LocalEulerAngles`、`Transform.LocalScale` 等。Renderer は Behaviour ではなく、現在 Renderer.enabled の読取り path は無い。同型 component 複数への一意な書込みも現行 path だけでは約束できない。
- wire の正本は `protocol/debugsocket`。既存 DebugCommand / CommandResult envelope の JSON payload で今回の一コマンドを表せる。generated DTO、MessagePack Key、MessageType を追加・手編集する必要はない。

### 1.3 次へ進める最低条件と制約

既存の一つの選択・識別経路のまま、最小 property の成功と拒否を Studio UI から確認でき、Player が実行時点の対象・型・寿命を検査し、Update 契約に従って書くことを最低条件とする。詳細は §2 の不変条件・成立条件に限定する。

A0/A1 の実機疎通は行わない。Unity 版は `unity/ProjectSettings/ProjectVersion.txt` の 6000.6.0f1。Windows Player / WPF 実 UI での証拠は未確認で、§5 の Phase C に委譲する。Editor 成功、mock、CLI 送信だけで実 UI の成立を代用しない。

### 1.4 対象外と後続の問い

次の各名称は未着手の後続スライス候補の所有名であり、新規 program / HANDOFF を今回作らない。

- `DebugStudio Command / Property Coverage`: LocalEulerAngles / LocalScale、Renderer.enabled の読取り追加、同型 component 複数の指定、その他 property の許可拡大。
- `DebugStudio Command / Batch Write`: 複数 property / 複数 object の一括更新、原子性、途中失敗の扱い。
- `DebugStudio Command / Undo`: Undo / Redo、履歴の寿命、ゲーム進行との競合。
- `DebugStudio Command / Record Replay`: 記録・再生、再接続を跨ぐ identifier、冪等性・再送保証。
- `DebugStudio Command / Authoring UX`: 汎用 typed editor、schema discovery、property ごとの capability、任意 reflection、スクリプト評価。
- `DebugStudio Command / Remote Access`: LAN / 認証 / 複数 Player・複数操作者。今回の証拠は同一 Windows host の loopback に限定する。
- `DebugStudio Validation / Player UI Route`: §5 の限定支援でも実 UI 経路が成立しない場合の基盤整備候補。自動で新スライスへ着手しない。

Asset Storage、ビルド配布基盤、SceneState、AssetOwner の設計変更、既存 Scene / Prefab / Addressables の広範な変更も対象外。最低条件未達を、上記へ書き移すだけで現スライスの成功にはしない。

## 2. A1 — 意思決定と受け入れ境界（推奨・未凍結）

### 2.1 進める最低条件

**不変条件**

- I1 許可していない path、欠落・不正・型不一致の value は setter に到達しない。入力の一部だけを適用しない。
- I2 旧 session、存在しない・失効した NodeId、破棄済み object、unloaded scene に書かない。別 object を再探索・代入しない。
- I3 Unity API の対象解決と setter は main thread の `ApplyMainThreadChanges` 内だけで行う。受信 thread、単なる SynchronizationContext 切替、Update / LateUpdate の途中で値を変えない。
- I4 未適用の要求を成功と返さない。1要求の失敗・例外が他の UpdateSystem の Tick、後続 apply、構造変更を止めない。session / service 終了後の保留要求を新 session で実行しない。
- I5 identifier と selection の正本を二重化しない。Game → Framework、Runtime → Foundation、Runtime / Editor の境界を破らない。

**成立条件**

- S1 許可集合の各 property を、通常の起動配線を通った Windows Player に実 WPF の Send から書ける。既存 command result に同じ requestId と成功が返り、既存 inspector の再queryで値を確認できる。
- S2 接続が生きている拒否要求は、同じ requestId の `Success=false` と機械可読 code を UI に返す。接続断・timeout は結果不明として扱い、無変更や成功と推測しない。
- S3 AI が起動・選択・送信・再読取り・証拠保存を行える操作経路を持つ。§5 の未確認経路と支援範囲を A3 が引き受け、実機証拠と失敗経路テストで判定できる。

### 2.2 許可 property と一コマンドの契約

コマンド名の推奨は `debugsocket.set-property`（Ordinal 完全一致）。初回許可は次の **2 path のみ** とする。

- `GameObject.ActiveSelf`: JSON boolean。Unity の `GameObject.SetActive(value)` を一度呼ぶ。
- `Transform.LocalPosition`: JSON number 3要素の配列 `[x,y,z]`。各要素を有限な float に変換できることを検査し、`transform.localPosition = new Vector3(x,y,z)` を一度行う。NaN / Infinity / overflow、文字列数値、要素数違いを拒否する。

既存 inspector の path をそのまま使う。LocalPosition の inspector ValueTypeId は表示用 string で、書込み JSON の型とは別である。位置の読み戻しは丸めた ValueText でなく invariant の RawValue を数値化する。ActiveSelf=false の後も同じ NodeId の inactive object に true を書けることを含める。書込みはその時点の代入であり、ゲームロジックが後の frame に上書きしない保証や Undo は持たない。

既存 `DebugCommandEnvelopeV1` の PayloadJson は以下の形。sessionId は既存 CapabilityWelcome の値を使う寿命ガードで、第二の object ID ではない。nodeId は既存 NodeId の正の Int64、propertyPath は完全一致。version は JSON 契約の版であり wire schema version は変えない。

~~~json
{"version":1,"sessionId":"<現在のWelcome.SessionId>","nodeId":42,"propertyPath":"GameObject.ActiveSelf","value":false}
{"version":1,"sessionId":"<現在のWelcome.SessionId>","nodeId":42,"propertyPath":"Transform.LocalPosition","value":[1.25,2,-3.5]}
~~~

トップレベルは一 object、上記5 field が必須。重複 field、未知 field、共通 field の null・型不正、余分な後続 JSON、不正 version / nodeId / sessionId は `invalid-payload`。value の null は許可 path の型検査で `type-mismatch` とする。要求は1 propertyだけで配列 batch を受け付けない。payload は UTF-8 4 KiB 以下、最大深さ4を推奨する。

結果は既存 `DebugCommandResultEnvelopeV1` の RequestId / Success / Message / PayloadJson に返す。JSON は `version`、`code` を必須とし、解釈済みなら sessionId / nodeId / propertyPath、成功時は setter 直後の `appliedValue` を付ける。Message は人間向けで code 判定に使わない。

- `ok`: setter と直後の読取りが完了。Success=true。読取り前に自身が破棄された等で確認不能なら ok にしない。
- `invalid-payload`: JSON構造・共通 field 不正。
- `property-not-allowed`: 上記2 path以外。case違い、名前・親・world position・component pathも拒否。
- `type-mismatch`: 許可 path に対する値の JSON 型・形の不一致。
- `invalid-value`: 正しい数値形でも float 範囲外・非有限。
- `stale-session`: payload / 受信 session / 現在 session の不一致。旧sessionの応答を新sessionへ付け替えない。
- `target-not-found`: unknown / pruned / destroyed / scene無効・unloaded。現行 resolver に合わせて統合し、区別のための tombstone を追加しない。
- `write-unavailable`: main thread context / UpdateSystem 登録・起動が成立していない。
- `queue-full` / `cancelled`: 保留上限、または apply 前に終了・取消を検知。
- `apply-failed`: setter または直後の確認が例外・確認不能。Success=falseでも変更が全く無かったと保証しない。自動 rollback / retry はしない。

優先順は decode・構造 → session / 実行基盤 → path → value → admission、apply 直前は停止・取消 → session → NodeId / scene / fake null → setter。セッションが閉じて応答を届けられない場合、UI は既存切断・timeout表示を使い、成功を合成しない。

### 2.3 実行、所有者、起動

Framework 共通の内部 write 分岐を service に接続する推奨。新 command は capability hello と DebugCommand / CommandResult の交渉後にだけ受理する。既存 ping 分岐、アプリ固有 catalog / dispatcher とは別の内部経路とし、一般 command の挙動を広げない。

`DebugSocketService` が write queue、完了 source、専用 update element を所有する（App 内の service 寿命）。通常 bootstrap で service を作ると登録でき、UpdateSystem が無ければ write-unavailable とする。新しい public resolver や Game 側の catalog 登録を前提にしない。Bootstrap の既存 UpdateSystemHost 先行生成を使う。登録の失敗と停止時の解除まで接続する。

1. Router は受信 session を保持したまま専用 callback へ渡す。受信 thread は JSON検証と managed queue 操作のみ。GameObject 参照を queue に保持せず、既存 session と NodeId を保持する。
2. `UpdateSystemRuntime.RegisterElement` に専用 `IUpdateElement` を登録する。推奨 layer は内部名 `DebugSocketWrites`、order 100、executionOrder 0。OnElementStart で受付可能になったことを記録し、activation前は write-unavailable。既存 scene stability gate を迂回しない。
3. OnElementLateUpdate が frame ごとに最大1要求を取り、既存 `UpdateCoordinator.EnqueueMainThreadApply(IMainThreadApplyCommand)` に渡す。pending上限32（in-flightを含む）、FIFOを推奨する。受信 loop 自体が逐次 await する現況もあり、この上限は汎用並列コマンド基盤を作る理由にしない。
4. 実 setter は command.Apply 内だけ。順序は `ActivatePendingRegistrations → RunUpdate → RunLateUpdate → ApplyMainThreadChanges → ApplyStructuralChanges`。既存 dirty-handle apply の後に command buffer が実行される。このスライスは UpdateSystem 自体の順序を変更しない。
5. Apply 直前に service の `_gate` 下で session一致・停止状態・NodeId resolver を確認し、setter開始までを session置換に対して直列化する。setter内の OnEnable / OnDisable の同期 callback・再入も考慮し、lock 内で await や通信、continuation完了をしない。結果確定・continuation通知は guard を解放して行う。session置換は setter開始より前なら拒否、開始後なら適用済みになり得る境界とする。
6. GameObject / Transform は参照の取得時と使用直前に Unity の `== null` / `!= null` で確認する。`?.` / `??` / `is null` / `ReferenceEquals` を UnityEngine.Object に使わない。
7. command自身が例外を失敗に変換する。既存 MainThreadApplyCommandBuffer は例外を吸収しないため、ここから例外を漏らさない。queueの続行、後続 Tick / apply / structural changes を試験する。
8. session終了・置換は対応する未適用要求を無効化、Stop / Dispose は全要求の取消・完了と登録解除を行う。既に bufferへ移ったcommandもApply時のguardで拒否する。再接続後の自動再送は無い。

SetActive に伴う callback の作用全体を transaction にすることは対象外。拒否が setter前なら値は不変、setter到達後の例外は apply-failed で結果不明になり得る。この区別を I1 / I4 と矛盾させない。

### 2.4 受け入れ条件

- AC1（I1・S2）valid targetへ未許可 `Transform.Position`、case違い、未知component pathを送る。property-not-allowed、setter呼出し0、before/after一致。booleanに文字列 `"false"`、位置に2要素、null、field欠落・重複、overflowも所定codeで無変更。
- AC2（I2・S2）既存 registry の未知/prune済みNodeId、queue後のDestroyImmediate、scene unloadを試す。target-not-foundで無変更。同名・新規生成の別objectへfallbackしない。未知と破棄済みを同codeで返すのが合格。
- AC3（I2・I4・S2）enqueue → session置換 → Apply、service再生成で同じ数値NodeIdが現れるケース、再接続後に旧payloadを送るケースを試す。旧要求のsetter0、旧結果を新sessionに載せない。応答不能時は切断/timeoutが合格で、成功や「無変更確認済み」表示は不合格。
- AC4（I3・I4）coordinatorを段階駆動する。受信・activation・RunUpdate・RunLateUpdateまでは値不変、ApplyMainThreadChangesでのみ変更・完了、続くApplyStructuralChangesまで到達。故障する要求の後にも別elementのTickと次の正常要求が進む。背景threadからsetterを呼ぶ実装は不合格。
- AC5（I4・S2）登録前、登録失敗、queue-full、enqueue後Stop/Dispose、apply前取消を試す。成功通知なし、終了した要求の後日適用なし、subscription / registration / 完了sourceの保持なし。成功をenqueue時に返す実装は不合格。
- AC6（I5）同名ノード2個のうち既存Hierarchyで選んだNodeIdだけへ書く。新しい選択store / instanceId wire field / reflection検索が無いことを構造確認する。Framework→Game、Runtime→Editorの参照増加0。§2.6で未承認の依存追加は不合格。
- AC7（S1・S3）実WPFで fixture を選択し、ActiveSelf true→false→true、LocalPosition (0,0,0)→(1.25,2,-3.5)→(0,0,0) を各1commandで往復する。各SendのrequestId、ok/appliedValue、再queryした同NodeId・現sessionのRawValueを照合。比較位置誤差は各成分1e-5以内。制御対象にゲーム側の継続更新を掛けない。
- AC8（S1・S2・S3）実UIから少なくとも未許可pathと型不一致を送信し、requestId付きfailureと読み戻し無変更を保存する。UI入力→Sendが観測できること。CLI / fake transport だけの成功はAC7/8の代用不可。破棄・session raceの決定的順序はAC2/3のUnityテストで閉じる。

### 2.5 判定と停止規則

スライスの GO は I1〜I5 と S1〜S3 の証拠が全て揃い、判定Cと最終C'が揃う場合だけ。未実装・未検証・AI操作経路未成立は NO-GO / 未判定として残す。この通常実装スライスに CONDITIONAL ACCEPT は使わない。最低条件達成後は §1.4 の機能を追加せず終了する。

今回の依頼の完了は A0/A1 文書の作成・検査・commit・push と「次はA2」の引継ぎだけであり、上記の製品GOとは別である。

A3後のblockerは凍結条件または常時契約と一次証拠を明記する。不変条件への違反は具体的順序も示す。それ以外は後続入力にする。条件・所有者・依存・公開面の変更が必要ならPhase Aを新revisionで再開する。A3後の例外承認・決定記録は現在なし（未到達）。

### 2.6 owner が A3 で決める未決事項

- D1 許可集合: **ActiveSelf / LocalPositionの2つを推奨**。回転・scale・Rendererは§1.4へ。Rendererを初回に含める裁定なら、readerとcomponent指定の契約、AC、責務と規模を改訂してから凍結する。
- D2 JSON decodeの依存: **同梱 System.Text.Json 8.0.5 の JsonDocument を内部codecで使う案を推奨**。Runtime asmdef は overrideReferences=trueで現在このDLLを参照していない。precompiledReferencesへの `System.Text.Json.dll` と直接必要な既存DLLの明示追加は設計判断で、A2で必要集合を確認しA3でowner承認する。公開APIへJsonElementを出さない。Unity JsonUtilityだけの型補完で欠落falseを誤適用しない。承認されない場合は decoder配置/方式をAへ戻し、無断で参照追加・独自汎用JSON基盤・BuildContent内部parserへの横断依存を作らない。
- D3 実行時点・受付寿命: §2.3のservice内部経路、登録activation後受付、LateUpdateから1件ずつApply queueへ送る案、上限32、session guardの直列化境界を推奨。既存一般dispatcherやCatalogを全面改造しない。
- D4 UI範囲: **既存 raw Commands + Path/RawValue の透過表示 + 選択中NodeId/sessionからのpayloadひな形作成を推奨**。作成は送信とは別の明示操作。以後選択が変わってもraw payloadのtargetを黙って差し替えない。送信対象をraw JSONで確認し、Runtime側の寿命guardを正にする。再読取りは既存の別node→元nodeの再選択で足り、専用refreshや汎用フォームは初回必須にしない。
- D5 UI実行経路: Windows UI Automationの限定scriptと必要controlのAutomationIdを同スライス支援に含め、Cのdesktop sessionで初回疎通する案を推奨。不成立時の最小基盤修正か問い分離をownerが凍結時に選ぶ。人間への操作代行を暗黙に条件化しない。
- D6 Player / fixture: **既存BS4 Spring Player（Windows x64 / IL2CPP / High）の起動を流用し、専用起動flagで小さな無更新fixtureをDependOnAllから作る案を推奨**。既存build coordinatorや本番Sceneは変更しない。必要なC実行権限、初回確認地点と証拠の受け入れをA3で確定する。
- D7 判定Cの照合: §5.4のoffline実行方式を推奨。資格情報・本番設定・通信へ到達しない環境を担当が確保できるかA3で確定する。できなければA1で照合scriptを書く方式へ改稿し、Bに判定scriptを書かせない。

## 3. 責務マップ

パスはrepository相対。行数はimplementation baseの物理行数、増分は初稿の概算。新規名は配置案でありまだ存在しない。Unity側は `OneStarMaker.Runtime` と既存FoundationのUpdate契約を使う。WPF側は `DebugStudio.App → Client / Server / Contracts` の既存方向を守り、Unity assemblyを参照しない。

### 3.1 Unity Runtime（prefix: unity/Assets/OneStarMaker/Scripts/Runtime/）

- `DebugSocketServices/Commands/PropertyWriteCodec.cs`（新規0→150〜230行）: JSON形・型・許可path・結果codeのpolicy。無状態、managed値を入力/出力、internal。Unity object / session /通信を参照しない。型不一致と欠落の単体試験をここで閉じる。codecとUnity setterを分けるのは依存と試験方法が異なるため。
- `DebugSocketServices/Commands/PropertyWriteQueue.cs`（新規0→120〜180行）: admission、FIFO、上限、取消・完了状態。service所有、service/session寿命、対象参照を保持しない。internal、managed requestと完了通知だけ。偽target executor・明示signalで失敗/取消順序を試す。
- `DebugSocketServices/Commands/PropertyWriteUpdateElement.cs`（新規0→70〜110行）: 登録activation通知とLateUpdate→既存apply commandの橋渡し。service所有、UpdateSystemの既存interfaceに依存。Unity APIをTickへ置かず、coordinator段階駆動で時点を試験する。
- `DebugSocketServices/Commands/GameObjectPropertyWriter.cs`（新規0→80〜130行）: 明示2setterと適用直後の値検査。状態を持たないUnity I/O、internal。resolverで得たGameObjectを操作時だけ受け取り保持しない。実GameObjectを使うEditModeでfake null・inactiveを試す。
- `DebugSocketServices/DebugSocketService.PropertyWrite.cs`（新規0→100〜160行）: current session / registry / queueのorchestrationとguard。既存service partial内のprivate接続に限定しpublic resolverは作らない。registryとsessionの所有者がserviceであるためここに置く。codec・setter本体は置かない。
- `DebugSocketServices/DebugSocketService.cs`（391、+15〜30）: queue/elementの生成・Start/Stop/Disposeへの限定配線。既存App寿命。public constructorは維持し、Unity contextと既存UpdateSystemの成立を確認する。新責務本体を別classへ置き、partialで行数だけを隠さない。
- `DebugSocketServices/DebugSocketService.Inbound.cs`（121、+10〜20）: sessionを保持したwrite callbackの接続。transport orchestration、service寿命、既存Foundation envelope入力。未知commandは既存dispatcherへ。
- `DebugSocketServices/Protocol/DebugSocketInboundMessageRouter.cs`（277、+20〜40）: capability確認後のwrite専用分岐とcallback。session寿命、internal、Unity objectに依存しない。fake sessionで未交渉拒否とrequestIdを試す。
- `DebugSocketServices/DebugSocketService.Session.cs`（170、+5〜15）: current交換/終了時のwrite取消を既存resetに接続。sessionの所有者は変えない。queue後の置換を試す。
- `OneStarMaker.Runtime.asmdef`（30、+1〜3の候補）: D2のprecompiled DLL参照。Framework assembly間edge、Game参照、Editor参照は追加しない。実装時はA3で許可した集合だけを使う。

再利用し原則変更しない: registry（140）、Inspector builder（296）、Inspector partial（105）、MainThread dispatcher（52）、built-in handler（81）、UpdateSystemRuntime（67）、AbstractApplicationInitializer（1196）。bootstrapは既に500行超だが今回新責務を積まず、既存生成経路を使う。registry・readerを新系統へ複製しない。Foundation/Runtime間の新公開APIは提案しない。

### 3.2 Studio（prefix: tools/DebugStudio/src/DebugStudio.App/）

- `Features/Commands/PropertyWriteDraftFactory.cs`（新規0→60〜100行）: 現session/選択済みNodeId/pathからraw JSONひな形を作る純粋関数。storeを所有しない、.NET8内のinternal、schema例とserializerの単体試験。許可の最終判定はRuntime。
- `Features/Commands/CommandWindowViewModel.cs`（340、+30〜60）: ひな形作成の明示UI操作と既存Send/結果表示。既存HierarchyStore/CapabilityStateStoreのsnapshotを操作時に読み、別SelectedNodeや永続target stateを作らない。VM/window寿命、純粋payload作成はfactoryへ。既存timer/相関基盤は変更しない。
- `Features/Commands/CommandPanel.xaml`（128、+15〜25）: path選択とひな形ボタン、案内、AutomationId。raw authoringを使い続ける。表示とUI操作の責務、window寿命。
- `Core/Composition/AppCompositionRoot.cs`（283、+2〜6）: Commandsに既存HierarchyStoreを渡すだけ。App寿命の配線。property policyやUnity意味論は置かない。
- `Features/Inspector/InspectorPropertyItemViewModel.cs`（60、+6〜12）: Path / RawValue をrecordから透過保持。`Resources/ViewModelTemplates.xaml`（116、+8〜20）は表示列とHierarchy/Inspector操作用AutomationId。既存選択のbindingを維持、mapperとWPF操作で確認。
- `Features/Session/SessionPanel.xaml`（115、+3〜8）: listener開始などのAutomationIdだけ。接続の既存service/状態は維持する。

CommandService（51）、TransportCommandSender（104）、SessionService（223）、ServerSessionTransport（527）、HierarchyViewModel（255）、HierarchyStore（189）、InspectorViewModel（216）、InspectorStore（200）は既存経路を使う。500行超のtransportへproperty policyを置かない。小さなmapper等の50%増加警報が実績で出たら理由を記録する。新規クラスは各1責務で、追加総量は支援・テスト込み約1,300〜2,000行の見込み。既存大ファイルの一般整理は含めない。

### 3.3 Game fixture・検証・文書

- `unity/Assets/SampleGame/DependOnAll/DebugStudioPropertyWriteProbe.cs`（新規0→60〜100行）と同階層 `AppInitializer.cs`（422、+10〜20）: D6採用時のみ、明示flagのときにScene内に名前固定のrootと同名の2子を作る。root側が所有し、一方を編集・他方を無変更比較する。ActiveSelfを切る対象にservice/Updaterを置かない。通常起動では生成しない。Assetをloadしないfixtureであり、Game→Runtimeの既存参照だけ。Tick不要、Scene終了で破棄。全体配線はDependOnAllに留める。
- `tools/DebugStudio/validation/Invoke-PropertyWriteUi.ps1`（新規0→150〜250行）: own PIDのWPF UIA操作・観測・保存だけを行う検証支援。所有するtest run寿命、Windows UIAutomationClientを使用しsocket直接書込みでUIを迂回しない。C判定の照合scriptとは別。失敗時は操作段階を保存して止まり、他taskのプロセスへ接続・終了しない。
- 新規 `unity/Assets/OneStarMaker/Tests/Foundation/DebugSocketPropertyWriteTests.cs`（0→250〜400行）: codec、queue、実object/registry、Update stageのAC1〜6。責務別fixtureを分ける必要があれば同folder内のcodec/queue/writerテストへ分割し、1ファイルへ無理に集めない。
- 新規 `tools/DebugStudio/tests/DebugStudio.App.Tests/Features/Commands/PropertyWriteDraftFactoryTests.cs`（0→80〜140行）と既存mapper/command/storeテストの必要箇所: .NET側のひな形、target固定、Path/RawValue保持、requestId/拒否を確認。新asmdef不要。支援scriptの失敗・対象PID限定は偽UI adapterで検査する。
- 実装後に `protocol/debugsocket/README.md` へJSON command契約を記載する（生成envelopeは無変更）。本HANDOFF以外の文書変更も今回は行わない。

## 4. 後続の実装計画（未承認）

A3凍結後のBは、(1) codecとmanaged試験、(2) queueとUpdate接続、(3) service/router/停止配線、(4) Studioの既存表示・raw command補助、(5) fixture/UIA支援、(6) 契約文書と機械検査の順を推奨する。読取り/selectionを新設する作業は無い。

Bは `#nullable enable` を編集Unity C#の先頭に置き、Unityのrecord禁止、ILogger<T>公開契約、fake null契約を守る。SceneState既存14値と順序・所有者を変えず、例外隔離を保つ。asset読取が必要に変わるならIAssetManagementとAssetOwnerを使う計画へAを戻す。

Bで計画外の状態、依存、owner、寿命、公開APIやreader/component識別が必要なら停止してAへ返す。D2未決のまま依存を追加しない。凍結済み範囲内の実装適応は結果へ記録し、原因不明なら範囲を限定して観測する。Unity Editor操作・コンパイル確認は後続Bの許容範囲、UnityテストとBuildはC。今回はH1/CURRENTやB限定Unity testの適用を提案しない。

## 5. テストとレビュー計画

### 5.1 安い確認と判定必須テスト

A0/A1で行うのは `pwsh tools/docs-audit.ps1` と `git diff --check`、差分範囲/引継ぎ票の確認のみ。実装テストの合格を記録しない。

- Unity単体: codecの形・型・有限値、queueの取消/容量、registry失効・fake null、実GameObjectのsetter、coordinatorの実行時点と例外隔離。Task.Delay / Thread.Sleepを使わず、明示signalとcoordinator段階駆動で順序を作る。
- 差し戻し中のUnity起点filter: `OneStarMaker.Tests.Foundation.DebugSocketPropertyWriteTests;OneStarMaker.Tests.Foundation.DebugSocketRuntimeNodeRegistryTests;OneStarMaker.Tests.Foundation.DebugSocketInspectorBuilderTests`（新規名はA3で固定）。既存のBuiltInCommandHandlerTestsとUpdateCoordinator / MainThreadApplyProcessorテストを影響根拠がある場合に追加する。
- .NET起点: `dotnet test tools/DebugStudio/tests/DebugStudio.App.Tests/DebugStudio.App.Tests.csproj --filter "FullyQualifiedName~PropertyWrite|FullyQualifiedName~CommandCorrelationTests|FullyQualifiedName~HierarchyInspectorStoreTests|FullyQualifiedName~InspectorExportServiceTests"`。実装が異なるfixtureへ分割したら必要集合を実名で固定・照合する。
- 判定必須: `dotnet test tools/DebugStudio/DebugStudio.sln`（Windows / .NET8、TRX保存）、`pwsh tools/run-tests.ps1 -Platform EditMode -Filter ''`（最終全件、XMLの件数>0・failed=0と対象名を確認）、contract-audit / docs-audit、§5.2の実Player/UI証拠。全EditModeの適用除外なし。
- 必須のUnity失敗経路は上記EditModeで閉じる方針。実Playerでしか起きない欠陥は理由・範囲を記録した限定C観測へ進む。常に全PlayModeや複数backendを追加する条件にはしない。
- BはUnity test / Buildを実行しない。CのUnity testは最初から承認済みsandbox外経路で標準runnerを使う。`unity test` / `unity run` は使わない。既存人間/Claude所有Editorを無断終了しない。自分のproject/PID/lockを確認する。native終了stallは既知の停止規則を引継ぎ、今回から調査回数をリセットしない。

### 5.2 end-to-end の取得経路

**未確認。初回確認は Phase C。** A0/A1の起動禁止と、Player BuildをCに限定する契約による。本sessionはnative computer APIが無効であり、UIAが使えると実証したわけではない。以下はAI担当が実行する計画で、A3で担当・desktop権限・不成立時の扱いを凍結する。

1. C実行担当が対象headとcleanな専用worktreeを固定し、.NET WPFをbuildする。自分のWindows desktop sessionでlistener / hierarchy / Commands / inspector exportのUIAアクセスを確認する。必要AutomationIdと限定scriptはBの成果物。ここで不成立なら「経路不成立」と保存し、Playerの長いbuildや人間への反復依頼を始めない。
2. Unity Editor / BuildはCの別途許可された操作経路で行う。既存 `Tools/OSM/Content/Build BS4 Spring Player`（`SampleGamePlayerBuildCoordinator`）を使用する案。Windows x64、IL2CPP、High、同じheadのPlayer/content/receiptを揃える。既存coordinatorはdebugSocket無効のconfigを出すため起動引数で有効化する。Artifact Storageのコード/清掃/公開機構をこのために変更しない。
3. StudioのSession UIで空いているloopback portのlistenerを開始してから、対象Playerを `--debugSocket.enabled=true --debugSocket.autoStart=true --debugSocket.mode=connect --debugSocket.connectUri=ws://127.0.0.1:5011/debugsocket/` と起動する。衝突時は自分のlistener URIと引数を同時に変更する。D6採用時の `--debugStudio.propertyWriteProbe=true` は**未実装のfixture flag**。既存Playerを変更済みheadの証拠として流用しない。
4. capability welcome、本線接続、fixtureのHierarchy出現を待つ。単なるping成功で代用しない。既存選択で片方の同名nodeを選び、NodeId、session、InspectorのPath / RawValueを取得する。別nodeは比較対象にする。
5. Commandsでひな形を作成、valueを入力、実SendボタンをInvokeする。AC7の各要求について入力JSON、requestId、結果JSONを保存。次に別node→対象nodeへ再選択してInspectorQueryを発行し、現session/対象NodeIdの新しいrevisionのdetailを確認・NDJSON exportする。false後も対象がHierarchyに残りtrueへ戻せることを確認。
6. AC8の拒否を実UIで実施し、対象・比較対象の無変更を読み戻す。AC2/3の破棄raceはこの手動時間合わせで作らず、決定的Unityテストの証拠を使う。CLIの `send` は補助で、UI経路の代用にはしない。
7. 保存は対象commit、Player Build GUID / receiptとの対応、操作時のPID・URI、操作手順ログ、入力/結果JSON、before/after Inspector NDJSON、必要controlと結果が見える画面、テストログ/XML/TRX。画面は操作経路の補助証拠、値の合否は構造化値で判断する。UIAイベント/条件待ちと有界timeoutで実行しsleepに依存しない。自分の検証用プロセスを正常終了し、他taskは触らない。

UIA・起動・fixture出現・観測・保存のどこが不成立かを分けて報告する。Cが権限内の基盤不足を直せる場合だけ最小修正し、責務・依存・受け入れ条件が変わるならAへ戻す。証拠が取れない条件を無断除外しない。美観・遊び心地など人間だけが判定する条件は今回無し。A3の意思決定は残るが、検証操作を人間へ暗黙に割り当てない。

### 5.3 高コスト証拠と受け渡し

Windows Player Buildと実WPF→Player往復を高コスト証拠とする。判定Cの安い必須試験 → 暫定C' → 阻害指摘の無いheadで高コスト証拠を一回取得 → 高コスト証拠を含む判定Cと最終C' の順。欠陥によってheadが変わったら旧headの合格を流用しない。

独立した期待値はAC7の定数（false / true、[1.25,2,-3.5] / [0,0,0]）とAC8の所定failure・無変更。期待値を実装の出力から生成しない。比較対象ノードも不変であることをbefore/afterから判断する。

C / C'用snapshot・生証拠は生成時刻と内容hashを固定manifestに持たせ、既存Artifact StorageのEvidence保存・取得規約に従って非秘密file集合を渡す。tracked本文には取得案内の担当・状態だけを追記し、reference、package hash、内部catalog、機微なローカルpathを転記しない。受取側sessionが取得し必須内容を読めた範囲だけを引渡し確認済みとする。C'は所見を除いた独立bundleを使う。現在これらは未生成であり、A1初稿のpushを検証Evidenceの保存完了とはしない。

### 5.4 判定Cの照合scriptの方式

推奨は **判定Cのoffline実行**。C担当が資格情報・本番設定・通信へ到達しない環境で、原記録の複製だけを入力とするscriptを書き実行する。UIA支援は観測データを保存するだけで、Bが最終合否scriptを作らない。

A3で凍結する合成fixtureの期待値: (a) AC7定数と正しい相関/再queryが全てある→pass、(b) value不一致→fail、(c) okでもInspectorが前revisionのまま→fail、(d) requestId / session / NodeId対応違い→fail、(e) 拒否ケースで値変化→fail、(f) 必須要求またはUI操作記録欠落→fail。全fixtureを通してから原記録へ適用する。実行環境を確保できない場合の方式変更はD7でA3前に決める。

### 5.5 担当と実績

- A0/A1: Codex（GPT-6系 / OpenAI）。同じ担当系統の調査subagentがStudioとRuntimeのコード読取を分担した。独立レビューには数えない。
- A2: 未実施。担当・モデル・ベンダー・同一入力内容hashは開始時に記録。少なくともarchitecture / 寿命・失敗順序・UI検証経路を独立に見る。
- A3: 未実施。ownerのD1〜D7の採否と理由を記録してから凍結する。
- C'予約: 未割当。A/B/Cに未関与のモデル系列・ベンダーまたは人間を残す。強化条件を満たすと未確認のまま記載しない。
- A0/A1の機械検査: commit前にdocs-auditとdiff --checkを実行し、結果はこの依頼の完了報告に残す。Unity / Player / .NETテストは未実行。

## 6. Phase B 実装結果

未着手。implementation head未固定。成果物・取得案内未生成。

## 7. Phase C

未実施

## 8. Phase C'

未実施

## 9. Phase D

未到達。マージ・harvest・Evidence保存引渡し・task終了を行わない。本HANDOFFは次のA2のため保持する。
