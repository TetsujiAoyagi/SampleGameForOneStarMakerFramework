# DebugStudio Validation / Player UI Route

## 0. メタデータと引継ぎ票

- type: `slice`
- status: `A`（A0/A1 r1 初稿。未凍結・実装未着手）
- branch: `codex/debugstudio-player-ui-route-a01`（`origin/develop` 起点）
- implementation base commit: `3f79639c12eac16da77d06608675564e906f9a49`
- implementation head commit: 未固定。計画文書の commit を実装 head としない。
- Phase A revision: `r1-draft`
- risk: `high`（Player 起動・fixture 寿命・WPF UIA とプロセス所有の境界）
- owner: リポジトリ owner（依頼者）
- created: 2026-10-12
- expires: 2026-11-12 または置換 revision の成立時。期限を実装許可にしない。
- harvest to: `tools/DebugStudio/validation/README.md`（実装時に作る操作手順）と `unity/Assets/README.md` の検証入口。実証後の現況のみ。
- task: `debugstudio-player-ui-route`。Workflow start 済み。今回の Phase 停止を task 終了に変換しない。
- 上位の決定: `codex/debugstudio-property-write-a01` の `docs/handoff/DEBUGSTUDIO_PROPERTY_WRITE.md`、A3 r3（凍結 commit `876a37c14353951f560552c13b30718056c527d0`）の D0/D5/D6。こちらのブランチへ Command の実装や HANDOFF を複製しない。
- Phase A snapshot の取得案内・状態: 本スライスの凍結 snapshot は未生成。次の担当は本ブランチの commit 済み初稿を使い、内容 hash を固定する。
- Phase B result / evidence / C' blind bundle: 未生成。各 Phase の担当が既存 Evidence 経路へ保存し、この会話に取得案内を渡す。
- PR: 未作成。作成は今回の範囲外。将来の base は `develop`。

**引継ぎ票 — 次は owner の本スライス A3 採否判断。** 今回は A0/A1 の作成まで。§2.5 の推奨案と未確認事項を入力にする。owner の追加モデル点検終了指示を引き継ぎ、今回も追加のモデル点検は行わない。独立 A2 を済ませたとは扱わず、A3 の採否も先取りしない。Command 側の凍結承認は、この先行スライスの未提示だった詳細の凍結や B 開始を意味しない。

A0/A1 はコードと文書の読取り・HANDOFF 編集だけ。Unity Editor / Player / WPF / UIA / Build / 実装テストは起動しない。Claude の別 task の checkout、process、port、出力を変更しない。`tools/Artifacts` と `docs/handoff/BUILD_SYSTEM_ARTIFACT_STORAGE_*` は編集しない。旧 PR #98〜#103 / `cursor/debug-go-*-575e` は入力に使わない。

## 1. A0 — 問いと境界

### 1.1 このスライスが答える問い

**同じ Windows host の AI が、実 WPF Studio の listener 開始・既存 ping の Send・既存 hierarchy の選択・inspector の再読取りと export を操作し、通常の bootstrap を通った Windows Player 上の無更新 fixture を、SessionId / NodeId と構造化値を対応付けて記録できるか。**

書込み機能に先立って、実 UI から Player へ届く入口と観測経路を成立させる。ping は既存の無変更 command だけを使う。ActiveSelf / LocalPosition の遠隔書込み、command codec、write guard は作らない。fixture の初期値設定は検証場面の生成であって、遠隔書込みの証明ではない。

次へ送る成果物は、再利用できる AI の操作手順、必要最小限の AutomationId と UIA driver、fixture の生成・寿命契約、同じ版の Player / Studio を識別できる証拠。Command 本体はこれを使って別途書込みの E2E を取得し、本スライスの ping 成功を代用しない。

### 1.2 読取りで確認した現況

- `DebugStudio.App` は .NET 8 WPF。`AppCompositionRoot` が app を組み立て、CLI control listener（既定5012）を起動するが、本線は実 UI の Start Listening で開始する。main window と Session panel の両方に同じ listener 操作がある。
- `MainWindow.xaml` は AvalonDock の shell。`ShellLayoutDefinitions` に session / hierarchy / inspector / commands 等の既存 ContentId がある。保存済み layout はユーザー共通の LocalAppData/DebugStudio/shell-layout.xml、log / telemetry persistence もユーザー共通である。検証を並行起動すると保存先を共有する。
- `CommandPanel.xaml` は CommandType / PayloadJson / Send / LastRequestId / LatestResult / LatestPayloadJson / History を既に表示する。`debugsocket.ping` は Runtime の built-in handler が成功と JSON の service=debugsocket / status=ok を返す。アプリ固有 dispatcher は不要。
- `Resources/ViewModelTemplates.xaml` が hierarchy / inspector の UI を直接持つ。Hierarchy は NodeId 列と選択 binding、Inspector は property 表示と export を持つ。ListView は virtualization / recycling が有効。現在 AutomationId は未設定であり、ID 追加だけで UIA の全 pattern が成立したとは言えない。
- Hierarchy の export は nodeId / parentId / name / type / revision / selectedNodeId を含む。Inspector の export は targetId / revision / state / path / rawValue を含むが **SessionId は含まない**。`NdjsonInspectorExportWriter` は camelCase で書く。別ノード→元ノードの再選択が再queryの入口で、再query前の retained document を export できてしまうため、revision と targetId の確認が必要。
- `DebugSocketRuntimeNodeRegistry` が唯一の NodeId 正本。再接続で対応表を消し、採番は戻さない。Welcome.SessionId は Player プロセス単位であり、同じ Player への再接続では変わらない。run 記録では接続回数も観測し、古い export と結ばない。
- `DebugSocketHierarchyPublisher.CaptureUnsafe` は loaded scene の root と inactive child も辿る。更新起点は capability 初回 snapshot、scene イベント、明示 NotifyHierarchyChanged。単なる実行時生成だけを更新通知とは扱わない。DontDestroyOnLoad 側への移動を fixture の可視性の根拠にしない。
- `AppInitializer.After` は現在 `BootstrapAfterSceneLoad` を呼ぶだけ。Framework は UICommon → map → DebugSocket 起動 → app services → SceneDirector → first scene → `OnPlayerContentReadyAsync` の順。したがって After 内で Framework の async bootstrap を呼ぶ**前**に bootstrap scene 内へ fixture を作れば、初回の接続 snapshot より前に用意できる。
- BS4 の `SampleGamePlayerBuildCoordinator` は Windows64 / IL2CPP / High stripping の Player と build receipt を作る。既定の baked config は debugSocket.enabled=false。`AppConfig` は command line > environment > file の順で、既存の `--debugSocket.enabled=true` 等を使える。
- `PlayerContentSmoke` は prefab の load / instantiate / verify 後、`content:holdSignalRoot` 指定時に ready.signal を書き、release.signal を最大120秒待つ。release 後は destroy / release → first scene unload → receipt → Quit。既存の hold を流用し、独自の永久待機を加えない。
- A0/A1 時点の確認は静的読取りのみ。対象 Editor は ProjectVersion.txt の 6000.6.0f1。UIA、Windows interactive desktop、IL2CPP / stripping 下の fixture・DebugSocket は未確認。

### 1.3 最低条件と対象外

最低条件は、他 task を動かさずに対象 process を起動・識別し、実 UI で既存 ping が往復し、fixture の各対象を既存 NodeId で選び、最新の Inspector 値と選択の対応を保存・再閲覧できること。§2 の I/S と AC に限定する。

後続の所有先:

- `DebugStudio Command / ActiveSelf Write`（上位 HANDOFF）: setter / codec / 失効・型・取消・適用順序 / 書込み成功と拒否の E2E。
- `DebugStudio Command / LocalPosition Write`: 数値入力と数値書込み。
- `DebugStudio Command / Write Draft UX`: Inspector の Path / RawValue 列と payload ひな形。
- `DebugStudio Validation / General UI Automation`: 任意ウィンドウ操作、画像認識、全画面・全 dock layout・全 DPI の網羅、非 Windows 対応。
- `DebugStudio Validation / Long Session`: 120秒を超える対話運用、再接続耐久、継続的な fixture 再生成。
- Build / Artifact Storage の機能追加、汎用テスト基盤、公開 RPC、新規 identifier / selection store、既存 Scene / Prefab / Addressables の恒久編集は対象外。

## 2. A1 — 推奨する契約と受け入れ条件（未凍結）

### 2.1 不変条件と成立条件

**不変条件**

- I1 検証 mode 無効時に fixture、待機、保存先変更を発生させない。既存の通常起動・BS4 smoke の振舞いを変えない。
- I2 AI が操作・終了するのは、この run が起動して記録した PID / 開始時刻 / 実行ファイルの process だけ。別 task の Editor / Studio / Player、使用中 port、既存の出力・layout を上書き・終了しない。
- I3 selection と読取りは既存 hierarchy / inspector の正本と wire を使う。instanceId、選択 store、検証用 query / command RPC を追加しない。書込み command を実装・送信しない。
- I4 証拠の run / process / SessionId / 接続状態 / NodeId / revision を対応付ける。古い snapshot、空 export、timeout、UIA 不成立を成功にしない。取得経路の失敗とアプリの値の不一致を区別する。
- I5 Game → Framework、Runtime / Editor の境界を保ち、asmdef edge を追加しない。fixture は main thread で生成・破棄し、無更新とする。Unity の偽 null を正しく判定する。
- I6 失敗・取消でも検証側のイベント購読・process handle・fixture を所有者が後始末する。待機は有限で、完了証拠なしに無期限待機や自動再試行を行わない。

**成立条件**

- S1 実 Studio の Start Listening と Send を AI が操作し、通常起動の Windows Player から既存 ping の対応する requestId・成功結果を観測できる。
- S2 同名の fixture 2個と初期 inactive 対象を既存 Hierarchy で識別・選択できる。A→B→A の再選択で新しい detail を export し、固定の初期値を確認できる。
- S3 操作ログ、UIの観測値、NDJSON、生画像、build identity と head の対応を保存し、次スライスの担当が操作手順を再利用できる。画像だけで値の合格を決めない。

### 2.2 Player の起動・fixture・寿命

**推奨:** BS4 Spring Player の既存 build と hold を使う。build / content 基盤、Framework bootstrap、DebugSocket の公開面を変更しない。

- 専用 flag は `debugStudio:validationFixture=true`、観測用 `debugStudio:validationRunId=<32hex>`。通常は false。既存 Config を読む。flag 有効時は Windows Player / BS4 mode / loaded bootstrap scene UICommon / DebugSocket enabled + connect + loopback / 非空の既存 holdSignalRoot を条件とし、条件不足なら検証起動を失敗にする。runId は証拠フォルダと fixture 名を対応付ける文字列であり、object の wire ID や選択キーにしない。
- `AppInitializer.After` から、`BootstrapAfterSceneLoad` の前に専用 fixture owner を一度だけ生成する。root は loaded UICommon scene に置き、DontDestroyOnLoad へ移さない。Framework の private service を露出・再生成せず、NotifyHierarchyChanged の新たな配線も作らない。
- root 名は `OSM.DebugStudio.Validation.<runId>`。子は `A/Target`、`B/Target`、`Inactive/Target`。A と B の Target は activeSelf=true、Inactive の Target だけ false。親と root は active。Transform は A=(1,2,3)、B=(-1,0,2)、Inactive=(0,0,0)、local rotation=identity / scale=one。GameObject / Transform だけの無更新 fixture とし、Renderer / Collider / asset load を追加しない。
- これらの値は初期値の独立期待値であって、遠隔 setter の試験ではない。後続 Command は A を変更し B を比較対象として使える。
- `AppInitializer` が owner を保持し、SubsystemRegistration、初期化失敗、OnPlayerContentReadyAsync の finally、Application.quitting で冪等に解放する。破棄済み root は `== null` で判定する。実行中の scene / prefab asset を変更しない。
- 既存 PlayerContentSmoke をそのまま実行し、ready.signal を検証操作の開始条件にする。run ごとに空の専用 hold root を作り、古い release.signal を使わない。AI は操作後または失敗時にその run の release.signal を作り、既存の smoke cleanup / receipt / 正常終了を待つ。120秒の Player 側上限は変更しない。
- startup の待機上限180秒、ready 後の採取全体90秒、1 UI 操作15秒を初稿の推奨とする。期限は終了理由を返すためのもので、成功を推測する根拠にしない。Player が期限切れ・起動失敗なら、その run を成功証拠にしない。
- 強制終了が必要なら新規起動した自己所有 PID を開始時刻込みで再照合し、失敗記録を残して対象だけ終了する。既知 Unity native 終了 stall の調査回数をリセットしない。

起動引数の例（まだ実行しない。exe / paths / port は当該 run で明示）:

~~~text
SampleGame.exe
  --debugSocket.enabled=true
  --debugSocket.autoStart=true
  --debugSocket.mode=connect
  --debugSocket.connectUri=ws://127.0.0.1:<port>/debugsocket/
  --debugSocket.allowRemote=false
  --debugStudio.validationFixture=true
  --debugStudio.validationRunId=<runId>
  --content.holdSignalRoot=<fresh-run-root>/signals
  -logFile <fresh-run-root>/player.log
~~~

backend は IL2CPP、stripping は High を維持する。実 Player は windowed の通常起動とし、Editor Play / headless stub で成立を代替しない。build receipt の identity と head は別物なので、C が build 実行時の git head・receipt hash・配布物の hash を結ぶ。既存 Build を新しい head の証拠にはしない。

### 2.3 Studio の観測入口と UIA

**製品側の変更は観測に必要な範囲だけ。** 選択・query・send・export の service / store の意味を変えない。

1. 検証用 `--validation-root <absolute fresh directory>` を Studio 起動 options に追加する案。明示された場合だけ layout / log / telemetry persistence をその配下へ向け、CLI control listener の自動起動を省く。通常起動の defaults は維持する。root を既存ユーザープロファイルへ差し替えず、空の run 配下だけを渡す。これで共通 layout と5012を別 task と競合させない。
2. AutomationId は main window、main header の URI / Start / Stop、Session identity / connection status、Commands の入力 / Send / requestId / result / payload / history、Hierarchy の list / row / SelectionSummary / export path / export / status、Inspector の target / summary / status / export path / export / status に限定する。例: `DebugStudio.Main`、`Session.Start`、`Commands.Send`、`Hierarchy.Nodes`、`Inspector.Export`。重複する Session panel と main header には異なる prefix を付ける。row ID は既存 NodeId を表示上で使う `Hierarchy.Node.<NodeId>` とし、別の registry を持たない。
3. AI の入口は PowerShell 7 の run orchestrator と、Windows PowerShell 5.1 / STA の限定 UIA driver を推奨する。既存 Windows の UIAutomationClient / UIAutomationTypes を使い、外部 UI automation package や新サーバーは導入しない。対象 HWND / PID を固定し、ValuePattern、InvokePattern、SelectionItemPattern、ScrollItemPattern、必要時 VirtualizedItemPattern のみで操作する。
4. AvalonDock の既存 tool tab を選択してから対象 control を探す。virtualized row は実体化・scroll 後に再取得し、recycling 前の AutomationElement を保持しない。ID / pattern / row が取れない場合は `uia-unavailable` として停止する。座標 click、ViewModel の直接操作、CLI send へ黙って切り替えない。
5. UIA イベントと process exit / file 完成を signal として待つ。テストは時間と signal を注入し、Task.Delay / Thread.Sleep は使わない。イベント購読→状態の再確認で取りこぼしを防ぎ、有限 timer と cancellation で閉じる。raw driver は操作成否・観測値を返し、最終 GO 判定はしない。
6. 後続スライスが使う最小操作は listen、既存 tab activation、select NodeId、fill raw command / Send、read result、export hierarchy / inspector、capture、release。今回の run recipe で送るのは `debugsocket.ping` / `{}` のみ。汎用シナリオ DSL や新たな command authoring UI は作らない。

### 2.4 受け入れ条件

- AC1（I1/I5）flag 無効時は fixture 0、既存 BS4 smoke の stage / cleanup と通常 Studio の保存先・CLI 起動が従来どおり。専用 mode 有効時だけ分離される。wrong scene / Editor / 非loopback / hold 未指定の validation 起動は失敗し、不完全な fixture を残さない。
- AC2（I2/I6）run root の既存 file、port 使用中、別 PID / 開始時刻違い、曖昧な対象 window を拒否する。別 task の process・共通 layout/log の before/after は不変。失敗時の release / 自己所有 process 後始末、イベント解除を fake adapter で検証する。実 UI run でも所有 PID を記録する。
- AC3（I3/I5/S2）fixture は service 起動前に UICommon 内で生成され、初回 snapshot に3対象が現れる。同名 Target は既存 parentId と NodeId で分け、正の異なる NodeId を持つ。Inactive の Target も表示・選択でき、Inspector ActiveSelf rawValue は false。新しい identifier / query / asmdef edge は0。
- AC4（S1/I4）実 UI で Start Listening → Player capability 成立 → CommandType=debugsocket.ping / PayloadJson={} → Send。新しい requestId が履歴の成功行と対応し、payload の service=debugsocket / status=ok を保存する。前回の LatestResult の再利用、CLIだけの応答、requestId 不明は不合格。
- AC5（S2/I4）UI で A→B→A の順に選択し、各選択後に Hierarchy の SelectionSummary（NodeId）と Inspector の Ready / revision を確認して export し、export 内の targetId が選択した NodeId と一致することを照合する。2回目の A は1回目より新しい revision、同じ NodeId / SessionId / 継続接続。A/B の ActiveSelf と localPosition が §2.2 の定数に一致する。inactive も別途 export する。古い revision、空 file、state が Ready 以外は不合格。
- AC6（I4/S3）Inspector NDJSON にない SessionId を捏造して追加しない。UIで読んだ SessionId と接続観測、targetId / revision、操作順、各 export hash を sidecar に記録する。run 中に切断・接続交換が起きたら当該採取を無効にして停止する。同じ SessionId だけで継続接続とは判断しない。
- AC7（I2/I4/S3）少なくとも Send の入力・結果と、fixture の選択・Inspector の画面を自己所有 window から採取する。run.json、operations.ndjson、hierarchy.ndjson、各 inspector NDJSON、command の観測 JSON、画像、process/build情報と生ログの明示 file 集合を保存する。画像は補助、値は構造化値で判断する。
- AC8（I1/I6）正常 release、採取途中の失敗、取消、Player 側 hold timeout の各経路で fixture owner / handle / UIA 購読が解放される。通常 BS4 cleanup と終了 receipt を確認する。timeout で成功 receipt を合成しない。
- AC9（S3）操作手順・期待値・採取物の例を validation README に記載し、同じ Windows host の次 session が正規 Evidence 経路で固定入力を fetch / 閲覧できる。送信側の読戻しだけで引渡し完了にしない。
- AC10（I3/I5、構造）変更は §3 の責務内。Framework Runtime / protocol / generated DTO / NodeId registry に変更0、Game→Framework 一方向、新規 asmdef 参照0。UIA は UI 操作だけで、app store を読む裏口を持たない。

### 2.5 owner が A3 で決める未決事項

- D0 経路: **BS4 Spring / Windows64 / IL2CPP / High と既存120秒 hold** を推奨。別の Player builder や永久待機は作らない。
- D1 fixture: **AfterSceneLoad の Framework bootstrap 呼出し前に UICommon scene へ生成**する案を推奨。接続後の動的生成＋新規通知 API 案は採らない。初回 snapshot と寿命は実 Player で確認する。
- D2 Studio 分離: **明示 validation-root に限る保存先の分離と CLI listener 省略**を推奨。他 task の共通設定ファイルを退避・復元する方式にはしない。
- D3 AI 操作: **限定 UIA driver、PID固定、ID / pattern による操作**を推奨。初回到達性は未確認。C の限定 WPF 疎通で不成立なら経路の問題として停止し、silent fallback はしない。
- D4 期限と採取: §2.2 の180秒 / 90秒 / 15秒と §2.4 の最小 file 集合を推奨。timeout を短くするために成立条件を削らない。
- D5 照合: **C 担当の offline script** を推奨。資格情報・本番設定・通信へ到達しない隔離環境で原記録の複製を判定する。確保できない場合は実行せず、A1 での script 作成・レビュー・凍結方式へ A を戻す。B は最終合否 script を作らない。
- D6 追加モデル点検なしで owner 判断へ渡す。独立 A2 は未実施として記録する。高リスクの独立性不足を A3 で扱い、現在の計画作成だけを凍結としない。

### 2.6 停止規則

I1〜I6 / S1〜S3 と判定 C / 最終 C' が揃ったときだけ GO。CONDITIONAL ACCEPT は使わない。操作経路の不成立を人間の手動操作へ暗黙移送しない。scope、fixture 生成時点、Framework 公開面・依存、待機寿命、証拠経路を変える必要が出たら A に返す。Command 実装は本スライスへ取り込まない。

## 3. 責務マップ

新規名は配置案。既存ファイルの行数は base の物理行数。規模は分割判断の材料で、成功の根拠ではない。

### 3.1 Unity / SampleGame

- `unity/Assets/SampleGame/DependOnAll/DebugStudioValidationFixture.cs`（新規約100〜160行）: Config から検証条件と recipe を受け、GameObject の生成・所有・冪等破棄を担う internal クラス。owner は AppInitializer、寿命はこの Player run。asset をロードしない。Unity API 境界をここに置き、fixture 構造と mode 条件を EditMode で試験する。
- `unity/Assets/SampleGame/DependOnAll/AppInitializer.cs`（422行、+35〜60）: After の生成呼出し、field、SubsystemRegistration / 初期化失敗 / PlayerContentReady finally / quitting の解放配線だけ。生成ロジックや wait loop を置かない。500行へ近づくが、既存 camera / render / telemetry の一括分割は本スライスで行わない。
- `PlayerContentSmoke.cs`（160行）: 既存の holdSignalRoot / cleanup を再利用し、原則変更0。fixture は専用 owner が保持し、この既存 prefab handle と混同しない。
- 依存: `SampleGame.DependOnAll → OneStarMaker.Runtime / Foundation / UniTask` の既存 edge 内。Framework、Editor assembly、asmdef は変更しない。Unity C# は #nullable enable、record 禁止、ILogger<T> と偽 null の契約を守る。新規 Update は作らない。

### 3.2 Studio / .NET 8

- `src/DebugStudio.App/Core/Composition/ValidationLaunchOptions.cs`（新規約60〜100行）: validation-root の明示入力と検証。root や layout の file I/O は既存 policy に委譲。任意 app 操作を持たない。
- `src/DebugStudio.App/App.xaml.cs`（+5〜10）と `Core/Composition/AppCompositionRoot.cs`（283行、+25〜45）: 起動 options の受渡し、既存 layout / LogPersistencePathPolicy / TelemetryPersistencePathPolicy への root 注入、validation 時の CLI listener 省略。通常の public 入口の互換を保つ。
- `MainWindow.xaml`（87行）、`Features/Commands/CommandPanel.xaml`、`Features/Session/SessionPanel.xaml`、`Resources/ViewModelTemplates.xaml`（116行）: 合計+30〜60行程度の AutomationId / row binding。必要な既存 control に限定し、typed editor や新しい選択 UI を作らない。UIA pattern 不足が XAML だけで解消しなければ、B は arbitrary helper を追加せず A に返す。
- Command / Session / Hierarchy / Inspector の service、store、DTO は変更0。export 形式を変えず、SessionId は検証側 sidecar に持つ。

### 3.3 操作支援・試験・文書

- `tools/DebugStudio/validation/Invoke-PlayerUiRoute.ps1`（新規約140〜220行）: 明示された Studio exe / Player exe / receipt / head / run root / port から process を起動し、所有の記録、ready / release、UI driver 呼出し、finally を制御する。新規 directory と自己所有 PID だけを扱う。
- `tools/DebugStudio/validation/PlayerUiAutomation.psm1` と小さな STA entry（計約180〜280行）: 対象 UIA tree、有限 event 待機、限定 pattern 操作・観測。process 起動と合否判定を混ぜない。script の shell は明記し、UIA だけ Windows PowerShell 5.1、orchestration は PowerShell 7 とする。
- `tools/DebugStudio/validation/RouteObservation.psm1`（新規約70〜110行）: 観測した SessionId / NodeId / revision / requestId と file hash を JSON / NDJSON に保存する。値を生成して期待値へ合わせない。最終の合否 script は C が別に書く。
- `tools/DebugStudio/validation/README.md`（実装後の操作手順）: control ID、起動と採取・後始末、未確認/失敗の返し方、Command 側の再利用手順。
- `unity/Assets/SampleGame/Tests/DebugStudio/DebugStudioValidationFixtureTests.cs`（新規約120〜200行）: mode 条件、fixture 定数、inactive、二重生成防止、cleanup の実 GameObject テスト。既存 SampleGame.Tests asmdef 内。
- `tools/DebugStudio/tests/DebugStudio.App.Tests/ValidationLaunchOptionsTests.cs` と既存 ShellComposition / ShellLayoutPersistence の限定追加（計約100〜180行）: default 不変、分離した root、CLI省略の配線。既存の依存注入を用い、実 process 起動を単体試験に混ぜない。
- `tools/DebugStudio/validation/tests/PlayerUiRoute.Tests.ps1`（新規約120〜200行）: fake process / UIA / clock・signal / writer による失敗・取消・stale観測・対象外不変。任意 code execution を外から注入する製品APIは作らない。

本体・script 約650〜1,050行、試験約340〜580行、文書は別。上位の400〜700行は粗い分割時の見込みであり、process / 保存先の分離と失敗経路を含む本初稿の合算へ更新する。startup、UIA、証拠生成を一つの巨大 script に詰めず、上記境界で試験可能にする。

## 4. 後続の実装順序（未承認）

A3 後に、(1) fixture と条件・cleanup の試験、(2) Studio の validation-root / AutomationId、(3) UIA driver と観測 adapter、(4) orchestrator の所有・期限・後始末、(5) README と機械検査、の順で B を進める案。

B は対象を限定した Editor コンパイル確認まで。Unity test / Player Build と本番 E2E は C。H1/CURRENT、B 限定 Unity test は今回提案しない。未知の UIA pattern、stripping、fixture 欠落は推測で成功にせず、C の限定観測と既存の A再開/B適応/スパイク/後続分類で扱う。

## 5. 検証と証拠

### 5.1 安い検査と必須試験

- 今回の文書更新: `pwsh tools/docs-audit.ps1`、`git diff --check` だけ。実装テスト未実行。
- C の差し戻し中の Unity 起点 filter: `SampleGame.Tests.DebugStudio.DebugStudioValidationFixtureTests;OneStarMaker.Tests.BuildContent.PlayerContentSmokeSequenceTests`（新規 namespace は A3 で固定）。追加は影響根拠を示す。
- .NET 起点: ValidationLaunchOptions、ShellComposition、ShellLayoutPersistence、HierarchyInspectorStore、InspectorExportService、CommandCorrelation の対象試験。
- 判定 C 必須: `dotnet test tools/DebugStudio/DebugStudio.sln`（TRX）、新規 script の offline suite（登録/実行件数>0・failed=0）、`pwsh tools/run-tests.ps1 -Platform EditMode -Filter ''`（全件XML、件数>0・failed=0と必要集合）、contract-audit / docs-audit、§5.2〜5.3。全 EditMode の適用除外なし。
- UIA adapter の fake 成功や XAML の ID 列挙は実 UI の成功を証明しない。実 Player で1件以上の E2E 証拠が必要。
- Unity test は最初から承認済み sandbox 外の標準 runner。`unity test` / `unity run` は使わない。Editor の起動・終了は osm-unity-editor の所有者確認に従い、既存の人間/Claude の Editor を終了しない。

### 5.2 未知の操作経路と実 E2E 手順

**未確認。初回確認は C。** A0/A1 では起動禁止、AutomationId / fixture 自体も未実装である。B の成果物を固定した後、C が同一ユーザーの Windows interactive desktop と UIAutomation assembly を使えることを確認する。remote/headless session を暗黙の代替にしない。

C は Player Build の前に対象 WPF だけの限定疎通を行い、自己所有 PID、tab の選択、入力・Invoke・virtualized list の観測経路が利用可能か確認する。これは未知経路の切り分けであり、Player成立や最終GOの証拠ではない。不成立なら raw UIA tree と失敗箇所を記録して基盤/計画へ返し、重い build を反復しない。GUI 操作を人間に委ねる場合は別の明示合意が必要。

最終の取得手順案:

1. C が対象 head で既存 `Tools/OSM/Content/Build BS4 Spring Player` を実行し、Build receipt / identity / IL2CPP / High と git head・配布物 hash を記録。Studio も同じ head からビルドした exe を明示する。対象以外の build / cache を上書きしない。
2. 新規 run root・runId を作り、port が他 process の使用中でないことを確認。Studio を validation-root 付きで起動し、自己所有 HWND 内の header URI に明示 loopback URI を入力し Start Listening を Invoke。listen 成立を確認してから §2.2 の Player を起動する。
3. capability 成立、SessionIdentityText、既存 ready.signal を観測する。通常 bootstrap が失敗していれば停止する。Hierarchy NDJSON を UI export し、fixture root の配下にある各 Target の既存 NodeId / parentId を取得する。
4. Commands tab を選び既存 ping を実 Send。新しい requestId、対応する履歴行の成功、LatestPayloadJson を読む。同時にこの window の画像を採取する。
5. export で特定した NodeId の row を実体化し、UIで A→B→A と inactive を選択する。各回、Hierarchy の SelectionSummary（NodeId）と Inspector の Ready / 新しい revision を確認し、固有名のファイルへ Inspector Export を Invoke。TargetId は export の targetId で照合する（Inspector の TargetTitle は同名対象を区別できない）。export完了表示と file の完成を確認する。
6. 操作ごとに SessionId と接続状態を観測し、sidecar へ record。途中の切断・交換は採取失敗。NDJSONそのものを書換えず、path=GameObject.ActiveSelf / Transform.LocalPosition の rawValue を C の独立期待値へ照合する。
7. 画像と生観測を保存し、finally でこの run の release.signal を作る。Player の正常 receipt と終了、Studio の window close を確認する。終了失敗は取得成功と分けて記録し、I6 未達のまま GO にしない。

### 5.3 高コスト証拠と保存

Player Build と実 WPF→Player 往復を高コスト証拠とする。安い判定 C → 暫定 C' → 阻害指摘のない head で1回取得 → 生証拠を加えた判定 C / 最終 C' の順。限定疎通は §5.2 の未確認箇所だけで、最終往復の重複取得を目的にしない。

必須の原記録は、明示入力と head / build対応の run.json、operations.ndjson、UI観測の command JSON、hierarchy / inspector NDJSON、対象 window の画像、Studio / Player log、build / smoke receipt、XML / TRX / offline結果。SessionId は sidecar の実観測を正とし、exportに元から含まれると推測しない。

固定 manifest に生成時刻・内容hash・採取版・必須 file 集合を持たせる。非秘密と確認した明示 file 集合だけを既存 Artifact Storage の Evidence publish / fetch へ渡す。資格情報、共通のローカル log directory、他 task の証拠を丸ごと含めない。取得案内はこの会話から次担当へ渡し、別 session で hash 照合と必須内容の閲覧を確認する。tracked HANDOFF に内部 reference / package hash / catalog の実値を転載しない。

### 5.4 C の照合方式と独立期待値

C 担当が隔離した offline 環境で原記録の複製だけを読む script を書く。B の観測 helper が出した pass を最終判定に使わない。A3 で次の合成 fixture の期待値を固定する。

- 必須操作と相関が揃い、pingの service/status が所定値、A/B=true・inactive=false、localPosition が §2.2 の値、Aの再query revisionが増加 → pass。
- requestId不一致、別PID/別head、SessionId不一致または途中切断、TargetId違い、再queryのrevision不変 → fail。
- UIの Invoke 記録欠落、CLIのみ、空NDJSON、Ready以外のdetail、原画像/必須file欠落、file hash不一致 → fail。
- Player の hold timeout / smoke失敗 / cleanup未了、別 task の設定変更 → fail。

raw boolean は小文字true/false、vectorは既存RawValueの数値として比較する。NodeId / requestId / SessionId の期待値を定数で捏造せず、同じ run の別々の観測間の相関で判定する。

### 5.5 担当・未確認

- A0/A1: Codex（GPT-6 系 / OpenAI）。コード・文書の読取りと主担当点検のみ。subagent / 他モデルのレビューは今回使わない。
- 独立 A2: 未実施。owner の追加モデル点検終了指示を引き継ぐ。
- A3: 未実施。D0〜D6 の owner 判断待ち。
- C / C': 未割当・未実施。C は B と異なるモデル、新 session。C' は blind bundle を用いる別担当または人間で、独立性を正直に記録する。
- 未確認: UIA pattern / virtualization / interactive desktop、fixture初回snapshot、Player stripping、起動〜採取の時間、Cのoffline環境、次sessionでの取得。これらを確認済みに変更していない。

## 6. Phase B 実装結果

未着手。コード・script・fixture・AutomationId は未作成。implementation head 未固定。

## 7. Phase C

未実施

## 8. Phase C'

未実施

## 9. Phase D

未到達。PR・merge・harvest・task終了は行わず、本 HANDOFF を未凍結の A0/A1 として保持する。
