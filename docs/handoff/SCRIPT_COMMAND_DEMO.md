# ScriptSystem command demo — C# / headless checkpoint

## 0. メタデータ

- type: `slice`
- status: `B checkpoint complete / C discovery reviewed`（C# / headless 先行範囲を実装・検証。確認済みの阻害指摘なし。UI接続・Unity最終検証・全体GOは未完了）
- branch: `codex/script-usage-sample`
- implementation base commit: `f0b4b1d022a213603d8ce52d036f31795a9eb2cd`（最新 develop）。B 開始用統合 head は `06ef1b09669122f587dc9749243a11a7a881d57c`。旧 planning base からの新着は Input / Sound のみで、Script / HP と常時契約に差分なし
- implementation head commit: `0fea030efa6a91ca1091c6eeee5f359461194d6f`（tree `659ad976d1a833b23cf4db8c5affbd044f3840f4`）。以後のレビュー記録のみのcommitは実装headと区別する
- A1 published draft: PR #94。r2 公開 head `1b992671`（文書のみ）。本 r3 はその責務分離 revision
- risk: `high`（Framework 公開 API と view 所有の実行寿命を追加）
- owner: root。A1 作成担当: plan_script_command_demo / OpenAI
- created: 2026-10-04 UTC
- expires: 2026-10-11 UTC または置換 revision。期限は再確認点であり自動承認ではない
- harvest to: `unity/Assets/Docs/Architecture/27-folder-structure.md` §2.3。実装・検証した契約とサンプルの入口だけ反映し、完了時に本 HANDOFF を削除
- Phase A snapshot: bundle内 `a3-r3/A3_FROZEN.md`、SHA-256 `65d5c64d56b00b9fa9c46af6fc61af49771166b0fc29c6c754f5c26880eb3235`（2026-10-05固定）
- Phase B result / evidence: 下記checkpoint bundleへ保存。C' blind bundleは未作成（添付はC発見所見を含むためblind用途ではない）
- 作業方式: 従来の tracked HANDOFF。H1 / H2 / external-current-v1 は採用しない

## 1. A0 — 目的、現況、対象外

目的は、既存 ScriptSystem が数値計算だけでなく、アプリ所有の型付き操作を順番に依頼する使い方を、既存 HpGauge で学習・検証できる最小例を作ることである。2026-10-05 の人間指示に従い、実行・待機・停止の共通機構は Framework、HP 操作・命令列・UI 配線は app に分ける。普通のゲームイベントに VM が必要、あるいは直接の型付き C# より優れるという実証ではない。

現況:

- VM は9数値命令。`ScriptProgram` は不変コピー、`ScriptRegisters` は呼出側所有の long 配列。Machine は純 C# で PC・状態・終端ラッチを持つ。host 呼出しも時間命令もない。
- `ScriptUpdateElement` は Update ごとに固定予算の Tick を1回呼ぶだけで、host request を処理できない。
- HpGaugeView は既存 ViewModel の Damage / Heal をボタンから呼び、HP の変化を既存 Tween / Flash / Shake に接続する。Damage は5〜25の乱数、Heal は20、HP は0〜100。これを変更しない。
- `UIToolkitView.Track` の disposable は ViewModel より先に Dispose される。`OnViewDestroy` は ViewModel Dispose 後なので停止の主入口に使えない。
- `HpGaugeScene.OnPreUnLoadedImpl` は現在ダイアログ要求の解除のみ。ここにも demo 停止を加える。
- `UpdateSystemRuntime.UnregisterElement` の active 要素除去は遅延される。解除要求だけでは同フレームの呼出しを防げない。
- OutGame / SampleGame.Tests の既存 asmdef は Runtime / Foundation を参照済み。新規 asmdef / 参照追加は不要。
- 既存の共通 UI / OutGame update layer は確認できない。Gameplay を流用せず、アプリ固有 layer を選ぶ。
- HpGauge.unity / UXML はあるが、現チェックアウトでこの画面への稼働中の入口は未確認。旧 scene creator の案内だけを入口成立の証拠にしない。

対象外:

- DSL、parser、外部ファイルロード、保存 bytecode ABI、register mailbox、動的 registry、reflection、汎用 Event / HostSubsystem。
- Tasks / async callback 基盤、並列 command、global request ID、永続化、戻り値レジスタ、ネットワーク・remote console・DebugCommandCatalog の統合。
- 新しいゲーム画面、HP ルールや既存演出の変更、HLOD、既存9命令の再設計、SceneState / asmdef / UpdateSystem 基盤の変更。
- 本番 Scene / Prefab / SceneResource / Addressables の再生成・YAML 編集。入口に新規配線が必要なら UI 段の前に限定範囲を追加合意する。
- 新着 develop の Input / Sound を自動的に script command に接続すること。取り込みは既存変更との整合確認に限る。

## 2. 意思決定と受け入れ境界

このスライスが答える問い: 純 VM とアプリの明示的な要求 / 完了境界を通し、HpGauge 上で Run → Damage → Wait 0.5秒 → Heal → Wait 0.5秒を3回 → Halt を、停止・再実行・view 破棄時にも重複副作用なく動かせるか。

進める最低条件とその詳細:

- M1: host 待機 / 完了 / 故障が純 VM テストで確定し、従来9命令・ゼロ予算の契約が保たれる。
  - 要求到達時は PC を動かさず WaitingForHost。繰返し Tick で同じ要求を返し、副作用・再発行なし。
  - 正しい要求の完了だけを1度受理。成功時に PC を1つ進め、失敗時は当該 PC で終端故障。古い・別 machine・二重完了は無変更で拒否。
  - 正でない予算は待機中・Ready・終端後にも RejectedBudget を先に返し、保存状態を変えない。
- M2: 実際の C# 組立 program を Framework の共通 runner が実行し、Damage / Wait / Heal / Wait を3回だけ順に dispatch して Halt する。
  - VM 内に Unity / delegate / 時計 / gameplay API を入れない。WaitMilliseconds の意味は Framework、HP command の種類・引数の意味と操作はアプリが持つ。
  - 1 Update に Tick は最大1回、host command 開始は最大1件。Start / LateUpdate では進めない。直ちに完了する操作も次の命令は次 Update 以降。
  - Wait は時間入力で進み、Tick 予算やフレーム数を秒に読み替えない。paused / 停止中は進まない。
- M3: opt-in の既存 HP 画面で動作と寿命を確認する。
  - 初期状態は Idle。Run / Stop と小さな実行状態表示だけ追加。既存 Damage / Heal / Open Dialog はそのまま利用可能。
  - Run 中の再 Run は無処理。Stop は即座に gate を閉じ、以後の tick / 完了通知は無処理。Halt / fault / Stop 後の Run は新規 machine / registers / runner。program だけ共有。
  - scene pre-unload と View の Track cleanup は停止 gate → unregister 要求 → 参照解放の順。解除遅延期間でも旧 runner が ViewModel を触らない。
  - 手動 Damage / Heal と demo は同じ既存 ViewModel を使う。乱数値・最終 HP 一致ではなく command 順序・回数・範囲と表示変化を観測する。
- M4: GO 候補の最終 head で標準 runner の全 EditMode 回帰と必要な visible flow の証拠が揃う。cloud の純テストだけで M3 / M4 を通過扱いしない。


判定: M1〜M4 が成立して GO。未達・未検証は NO-GO / blocked と区別して記録し、CONDITIONAL ACCEPT は設けない。満たした後は後続の言語・汎用化を理由に拡張しない。

cloud での M1 / M2 の純実行証拠は中間 checkpoint であり、M3 / M4 の完了ではない。2026-10-05 03:43 UTC の人間指示は、共通 Framework / app 分離を条件に、C# VM 境界・HP command・cloud 検証を先行し、Unity UI 接続と最終検証を未完で残して進める承認として扱う。この承認は全 EditMode の免除、全体 GO、merge の承認ではない。r3 の A2 指摘を既承認の境界内で採否・反映し、A3 欄へ凍結結果を記録した後、同じ bounded B に重ねて確認を求めない。新しい機能・責務・入口整備が必要ならその変更だけ判断へ返す。

ここでは答えない問いの所有先（未承認の後続候補。今回の実装キューではない）:

- テキスト言語 / 外部保存形式 / 入力検証一般化: 「Script Authoring」後続候補。
- 戻り値、複数引数、非同期・並列操作、汎用 host: 「Script Host Expansion」後続候補。
- 他画面での価値や直 C# との比較: 「Script Usage Evaluation」後続候補。
- HpGauge 到達のための恒久 navigation / asset 整備が必要な場合: 「HpGauge Entry」候補として A3 で今回への最小編入か問いの分離を決める。

未決事項: Unity の実行権限・正しい project / Editor、既存 HpGauge への到達と観測・保存経路。これは UI 段と M3 / M4 の blocker であり、承認済み C# / headless 段の着手 blocker にはしない。既存アクセス確認の重複質問や暗黙の人間代行はしない。UI 段開始前に担当・証拠・不成立時の扱いを解決する。

## 3. 最小 API と実行契約

### Framework の要求 / 完了境界

- `ScriptOpcode.HostCommand = 9` を末尾追加。既存番号を変えない。
- `ScriptInstruction.HostCommand(int commandId, long argument)`。既存オペランドの Destination を commandId、Immediate を argument として使う。Left / Right は未使用。commandId は register index ではない。FromRaw も同じ実行意味。
- 純 ScriptMachine は commandId / argument の合法性を知らない。共通 runner が Framework command、app host が app command を検査し、未知 command / 不正引数を失敗完了にする。未知 opcode 自体は従来の InvalidOpcode。
- 新規 `ScriptHostRequest` は sealed な不変純 C# オブジェクト。公開 getter は CommandId / Argument、生成は machine 内部。要求オブジェクトそのものの参照同一性を one-use completion token とし、別 token 型・連番・global ID を追加しない。
- `ScriptMachine.PendingHostRequest` は nullable request。host opcode に初めて到達したとき1個作り、待機中は同じ object。数値命令経路の追加 allocation はない。1 host 境界につき小さい allocation があることを明示し、allocation-free host を目標にしない。
- 状態を末尾追加: WaitingForHost、Ready、HostCommandFailed。Ready は成功 ack 後・次 Tick 前を表す。Yielded は引き続き予算消費だけを表す。
- `bool TryCompleteHostCommand(ScriptHostRequest request, bool succeeded)` は pending の同一参照だけ受理する。受理時に pending を消す。成功なら PC++ / Ready（非終端）、失敗なら PC 不変 / HostCommandFailed（終端）。null・別 request・完了済み request・終端後は false / 完全無変更。受理処理は次命令を実行しない。
- positive Tick の優先順は既存終端 → pending 待機 → 命令実行。非正予算の拒否はこれらすべてより先。HostCommand 到達は1命令分の予算を消費し、直ちに WaitingForHost を返す。待機確認で予算や PC を消費しない。
- Tick、register 変更、完了は呼出側が同一スレッドで逐次化する。キャンセル API / reset API は追加せず、新規 machine で再 Run する。HpGauge の Run / Stop / Tick / dispatch / 完了 / UI通知は Unity メインスレッド専用。Damage / Heal から reactive binding 経由で UI を変更するため worker thread 実行を許さない。
- `ScriptUpdateElement` の動作は変えない。host のない既存利用を継続し、host request があれば外側が完了するまで進まないことをコメントで明示する。demo runner と同じ machine を二重登録しない。

### Framework の共通実行と app command

- 既存 `OneStarMaker.Runtime.ScriptSystem` に `ScriptCommandRunner`、`IScriptCommandHost`、`ScriptCommandResult`、`ScriptCommandRunState`、`ScriptCommandTimeSource`、`ScriptCommands` を置く。runner は純 C# の `IUpdateElement` / `IDisposable` で、Foundation の値 context だけを参照する。UnityEngine、SampleGame、UI、UpdateSystemRuntime の登録 API を参照しない。別の adapter / registry / ServiceContainer は作らない。
- command ID は共通 `ScriptCommands.WaitMillisecondsCommandId = -1`、app は0以上。-2以下は予約済みとして runner が拒否し、host に渡さない。動的登録・文字列名・global registry・保存 ABI は作らない。この区分は共通 runner の契約で、純 Machine は全 int を opaque なまま要求する。
- `ScriptCommands.WaitMilliseconds(long milliseconds)` は非負を検査して HostCommand(-1, milliseconds) を返す。runner も raw 要求の負数を副作用前に拒否し、非負 long を double 秒へ変換する。Wait は host に渡さず、共通 runner が待機を所有する。HP host に Wait の switch / 時間処理を複製しない。
- `HpGaugeScriptCommands` は `IScriptCommandHost` を実装し、app enum `Damage = 0 / Heal = 1` のみ解釈する。引数は0のみ。未知 ID / 不正引数は操作前に Rejected。constructor の型付き `Action` をそれぞれ1回呼び Completed を返す。例外は runner 境界へ伝える。後の UI 段で View が既存 ViewModel.Damage / Heal を渡す。
- `HpGaugeScriptProgram` は共有する不変 program を1個組み立てる。r0=3、r1=1、Damage(0)、共通 WaitMilliseconds(500)、Heal(0)、共通 WaitMilliseconds(500)、Sub(r0,r0,r1)、JumpIfNotZero(r0, Damage位置)、Halt。9命令、2 registers。数値制御は VM が行い、C# 側で3回を再現しない。
- app Action が例外を投げた場合、runner がまだ live で同じ要求を所有するときだけ失敗完了させて終了する。Action 内で Stop / Dispose された後の return / throw は停止を上書きせず無処理。副作用後の例外でも rollback / retry はしない。以後の host request を実行せず、他 Update 要素は継続する。

### 公開 API と runner の寿命

公開面は次だけとする。テスト専用 machine 注入・register 公開・汎用 event 基盤を追加しない。

| API | 契約 |
|---|---|
| `IScriptCommandHost.Execute(int commandId, long argument)` | 同期的に `ScriptCommandResult` を返す。enum は `Rejected = 0 / Completed = 1`。runner は Completed 以外（不正 enum 値含む）を拒否する。app 操作だけの最小契約で、非同期完了・待機結果を公開しない。 |
| `ScriptCommands.WaitMillisecondsCommandId` / `WaitMilliseconds(long milliseconds)` | Framework 所有の ID と命令 factory。負数は ArgumentOutOfRangeException。 |
| `ScriptCommandRunner(ScriptProgram program, int registerCount, IScriptCommandHost host, ScriptCommandTimeSource timeSource, int instructionBudgetPerUpdate, Action<ScriptCommandRunner, ScriptCommandRunState>? stateChanged = null, Action<ScriptCommandRunner>? releaseRegistration = null)` | null program / host、負の本数、1未満の予算、不正 timeSource を構築時拒否。timeSource は `Scaled / Unscaled` の必須指定。毎回 private な新規 machine とゼロ初期化 registers を作り、外部へ返さない。program だけ共有可。構築時は Running、実行・通知・登録はしない。 |
| `ScriptCommandRunState State { get; }` | read-only enum `Running / Waiting / Halted / Stopped / Failed`。Idle は runner がない View の表示。setter、Machine / Registers の getter は設けない。 |
| `void Stop()` / `void Dispose()` | 同じ1回限りの停止処理。live なら Stopped、既に終端なら状態維持。gate は同期的に閉じ、通知・解除 callback の例外を外へ投げない。再開不可。再 Run は owner が新 instance を作る。 |
| `stateChanged` / `releaseRegistration` | 単一 observer と単一 owner callback。変更後の状態を通知し、終端時の解除要求は別 callback で必ず1回試みる。headless では両方省略可。登録 API / layer は呼出側が所有する。 |
| `HpGaugeScriptCommands(Action damage, Action heal)` | null は構築時拒否。Run ごとに生成し、既存の型付き操作を借用する。 |
| `HpGaugeScriptProgram.Program` / `RegisterCount` | 共有する不変 ScriptProgram と必要本数2の getter。 |

- 1 runner instance が1 Run の machine / registers、現在要求、待機残量、停止 gate と状態通知を所有する。既存 ScriptUpdateElement は内包・登録しない。Start / LateUpdate は無処理。共通 runner のスレッド安全性は提供せず、owner が同一スレッドで逐次化する。HP の UI 接続後は Unity メインスレッド専用。
- OnElementUpdate は停止 / pause / 同一 runner の更新再入 gate を先に見る。Action / observer からの再入 Update は無処理とし、更新 gate は finally で戻す。待機中は指定 timeSource に対応する正の有限 delta（Scaled は `context.DeltaTime`、Unscaled は `context.UnscaledDeltaTime`） だけを残量から減らし、満了時は live と同一 pending 要求を確認して成功完了し、runner側の要求 / wait を消し、Waiting → Running に変更する。まだ live なら状態通知して return し、同じ Update で次の Tick は行わない。成功 ack 後は machine pending が null になることが正常なので、通知時に ack 前の pending 同一性を再要求しない。負・NaN・infinity は進行に使わない。待機を開始したフレームの delta をさかのぼって加算しない。
- それ以外は構築時の固定予算で Tick を最大1回行う。新規要求を処理済みとして捕捉してから、共通 Wait または app host を処理する。dispatch の return と catch の両方で、live gate と捕捉要求 / 現在要求 / machine pending の参照同一性を再確認してから ack・待機設定・通知する。失効した場合は何もせず return。通知から戻った後に処理を続ける場合は live と現在の処理段階を再確認する。ack 前の要求処理なら pending 同一性も必要だが、ack 後は pending が消えるという成功後条件で判断する。即時完了後も return。Wait(0) も開始時に成功完了して return し、一時的な Waiting 通知は出さない。余剰 delta の持越し・一括 catch-up はない。

- 終端 / Stop / Dispose は runner の単一 close 処理へ集約する。最初に gate を閉じ、終端 State を確定し、現在要求 / wait を無効化する。その後、終端通知を try、`releaseRegistration(this)` を finally 内の別 try、host・両 callback 参照の解除を最内の finally で行う。各 callback の例外を捕捉し、片方の失敗でも後続 cleanup を省略しない。close 再入は無処理。実行中 callback のローカル参照はその呼出しの unwind で解放される。
- 状態 observer は constructor では呼ばず、State 変更時に同期呼出しする。通知再入中の追加通知は抑止し、Stop / Dispose の gate と cleanup 自体は即実行する。observer が throw した場合、まだ live なら Failed として close（失敗した observer の再通知なし）、既に停止済みなら終端状態を維持する。通知失敗を command の再 dispatch / retry にしない。通常更新の observer 例外と終端 cleanup callback 例外は `OnElementUpdate` の外へ逃がさない。実 scheduler が要素例外を隔離するとの仮定を置かない。

### 後続の UI / 登録配線（今回の C# / headless 先行実装には含めない）

- HP は runner 構築時に `Unscaled` と予算16を選ぶ。他アプリは自身の clock policy / 固定予算を選べる。Framework は UI 用の既定値を選ばず、timeSource にかかわらず layer pause を尊重する。
- 登録 layer はアプリ固有の `HpGaugeScriptDemo`、layerOrder=0 / executionOrder=0。既定 scale=1 の独立 layer とし Gameplay pause を借用しない。UI の実時間用 unscaled delta を使うが、この layer 自体の pause は尊重する。layer の保持者は既存 Coordinator で App 寿命、view が所有するのは runner 登録だけである。停止時に layer 自体を削除しない。view が layer pause / scale を勝手に変更しない。標準 RegisterElement が layer を作成するため AppInitializer 変更は不要。layer 定義自体は Coordinator/App 寿命で空のまま残ってよく、runner / view の残存とは区別する。
- Wait の500msは蓄積入力時間の下限であり、実表示はフレーム単位に量子化される。開始や完了後の別 Update 境界があるため、全体が厳密に3.000秒で終わるとは保証しない。
- View は1つの active runner を保持し Run 時に生成・登録する。登録失敗なら停止・破棄して Failed 表示とし、直 Tick / MonoBehaviour.Update の迂回をしない。
- View は `releaseRegistration` の唯一の所有者として、渡された旧 runner そのものを Runtime API へ unregister 要求する。callback は try で unregister、finally で current が同じ旧 instance の場合だけ View の保持参照を解除する。通知では current instance 同一性で古い runner を拒否し、Run は current が未解除なら終端通知中も無処理。View の Stop / shutdown / Track cleanup も runner.Stop / Dispose を呼び、別経路で二重 unregister しない。登録失敗でも同じ停止境界を使い、登録成立前なら解除要求は不要。
- unregister が throw した場合も gate と app 参照解放は成立させ、失敗は View の既存診断経路で記録する（診断失敗も runner の callback 境界で隔離）。実登録の除去成功は保証しない。停止済み runner が scheduler に残る可能性を cleanup 未達として記録し、基盤を修正済みと扱わない。非同期 completion callback / Tasks は設けない。
- `HpGaugeView` は一度だけ Track に lifetime cleanup を登録する。scene pre-unload は view の shutdown 入口を呼び、新しい Run も拒否する。基底 OnDestroy を隠さず、OnViewDestroy だけを停止入口にしない。
- manual Stop は再 Run 可、shutdown / Dispose は再 Run 不可。状態表示は Idle / Running / Waiting / Halted / Stopped / Failed の最小表示。既存 HP 表示バインディングへ command の個別 UI 処理を足さない。

## 4. 責務マップと変更予算

既存 namespace / asmdef を維持する。新規 Unity C# は #nullable enable、record 禁止、必要な .meta は authorized Editor import で確認する。

| 対象 | 責務 / 所有 / 依存 / テスト境界 | 現在行数 → 予想増分 |
|---|---|---|
| Runtime/ScriptSystem/ScriptMachine.cs | 純 machine 状態遷移。caller の program/registers を借用、要求を所有。Unity / app 参照なし | 249 → +60〜90 |
| 同 ScriptInstruction.cs / ScriptOpcode.cs / ScriptMachineStatus.cs | 明示 host 境界の値 API。メモリ上のみ | 87 / 21 / 21 → 合計 +25〜40 |
| 同 ScriptHostRequest.cs（新規） | 不変 request と completion identity。machine 待機寿命 | 0 → 25〜40 |
| 同 ScriptUpdateElement.cs | 既存動作維持、外側の完了責務のコメントだけ | 50 → +2〜5 |
| OutGame/HpGauge/HpGaugeScriptProgram.cs（新規） | sample の不変命令列。app 全体で共有可能、ゲーム内容の変更でだけ変わる | 0 → 30〜45 |
| 同 HpGaugeScriptCommands.cs（新規） | Damage / Heal の引数検査・型付き app dispatch。借用 Action、Run 寿命。Unity 無し | 0 → 40〜65 |
| Runtime/ScriptSystem/ScriptCommandRunner.cs（新規） | bounded Update / 共通 Wait / fail / stop。Run 所有。Foundation context と純 ScriptSystem、host interface のみ | 0 → 250〜350 |
| 同 IScriptCommandHost.cs / ScriptCommandResult.cs（新規） | 同期 app 境界。値契約のみ、資源所有なし | 0 → 合計25〜40 |
| 同 ScriptCommandRunState.cs / ScriptCommandTimeSource.cs（新規） | 共通 runner の観測状態 / caller の時間選択。HP 語彙なし | 0 → 合計25〜35 |
| 同 ScriptCommands.cs（新規） | 共通 command ID / Wait 命令 factory。静的・状態なし | 0 → 25〜40 |
| OutGame/HpGauge/HpGaugeView.cs（UI 段） | UI binding と登録・view寿命接続。UI / Runtime API の外側 adapter | 64 → +65〜90 |
| OutGame/HpGauge/HpGaugeScene.cs / HpGauge.uxml（UI 段） | pre-unload停止 / opt-in操作表示 | 52 / 11 → +5〜10 / +6〜10 |
| Tests/ScriptSystem/ScriptHostCommandTests.cs（新規） | 純 VM request/ack/予算/故障。Framework test asmdef | 0 → 160〜220 |
| Tests/ScriptSystem/ScriptCommandRunnerTests.cs（新規） | fake host / 注入 context の時間・停止・再入・cleanup。Framework test asmdef | 0 → 200〜280 |
| SampleGame/Tests/App/HpGaugeScriptProgramTests.cs（新規） | 実 sample program / HP host の順序・3巡・引数検査。既存 SampleGame.Tests asmdef | 0 → 80〜120 |
| SampleGame/Tests/Editor/OutGame/HpGaugeScriptViewTests.cs（新規） | UXML import / button / lifecycle / 実 ViewModel の接続。既存 Editor test asmdef | 0 → 100〜150 |
| tools/ScriptCommandOfflineTests/（新規 csproj + Program.cs） | .NET 8 dependency-free linked source の cloud 純テスト。既存 DebugCommandOfflineTests と同形式 | 0 → 180〜230合計 |

app の2中核型は既存 HpGauge namespace、共通型は既存 Runtime/ScriptSystem namespace 内の限定された public API とする（独立 test assembly から直接利用）。新しい依存 edge / friend assembly / asmdef は不要。app → Runtime → Foundation の既存方向を守る。host interface は Framework が定義し app が実装するが、サービス探索・DI container は導入しない。program / HP dispatch はゲーム内容、runner / Wait は共通実行契約が変更理由となるため分離する。class 数だけの新フォルダは作らない。

50% 増加警報は小さい enum と View に該当する。enum は単一値契約のため非分割。View は UI/lifetime adapter のままにし、命令・dispatch は app 2型、時間・実行は共通 runner へ分離するので非分割。既存 ScriptMachineTests は524行なので host テストを新規ファイルに分ける。新規 runner が予想範囲を越え、独立した複数の変更理由を持ち始めたら便宜的 Manager を作らず A に返す。

現 Phase A で作成する tracked 差分は本 HANDOFF だけ。docs/README.md の一覧は program/research 専用のため slice 行を足さない。この r3 編集では実装しない。A3 後の先行 B は表の純 C# / tests / offline target まで。View / Scene / UXML / Editor view tests / Editor import は保留し、UI 段の経路成立後に扱う。

## 5. テスト、検証経路、実装順序

発見用起点 filter:

`OneStarMaker.Tests.ScriptSystem|SampleGame.Tests.HpGaugeScriptProgramTests|SampleGame.Tests.Editor.OutGame.HpGaugeScriptViewTests`

実装時は新規 test の namespace を上記へ合わせる。既存 namespace 全件性は filter だけで推定せず XML の名前・件数で確認する。

検証の最小集合（1〜6 と7〜8の純 runner 部分を先行。ボタン、登録、View 参照、scene 寿命の実接続は UI 段で検証し、cloud 結果へ混在させない）:

1. runner 構築時の null / 本数 / 固定予算 / timeSource 検査。request 発行の PC / 同一性、positive Tick 再入、host 命令直前の予算切れ、全状態の0/負予算拒否。
2. success ack と Ready、次 Tick だけが次命令を進める、failure latch、null / 他 machine / 古い / 二重 ack の無変更拒否。
3. raw HostCommand の ID/argument 使用・未使用欄無視、未知 opcode の従来 fault、終端と自然終端。
4. 実 sample program の3巡 / 3 Damage / 3 Heal / 6 Wait / Halt。fake Actions で順序を確定する。
5. HP 以外の fake host でも共通 Wait と runner を利用でき、HP host には Wait が到達しない。0.49秒では未完、0.5秒境界、Wait(0)、pause、unscaled と scaled の相違、無効 delta、開始フレームの delta 不採用、長いフレームでも command 一括実行なし。
6. 共通 ID -1 の Wait（factory / raw 両方の負引数拒否）、予約負 ID / app 未知 ID / 不正引数 / host の不正 result enum / Action 例外の fail-closed。副作用を1回行ってから throw しても変更は1回のみ、rollback / retry / 後続 command なし。Damage / Heal の Action が Stop→return、Stop→throw（Dispose も同じ）した場合は Stopped を維持し、ack / Waiting / Failed への上書き通知なし。
7. Stop 中の待機、Stop→Run、active Run の二重押下、登録待ちの Stop、active 解除遅延中の Tick、古い通知、dispose後呼出し。新 machine/registers と同じ immutable program を確認（公開可変 getter を増やさず、同じ program の再実行が初期値から独立して進むことを観測）。
8. Running / Waiting / 終端 observer の throw・Stop / Dispose 再入・Update 再入。通知失敗でも解除 callback が1回、app参照が解放され、終了状態を上書きしない。解除 callback の throw でも参照解放が成立し、実除去成功とは主張しない。例外隔離なしの同一更新ループで runner の次の別要素が呼ばれることを確認し、View 側では実 unregister と current 解除の finally 境界を検証する。

cloud の範囲:

- A1: source・契約・差分確認、docs-audit。Unity 起動 / test は行わない。
- A3 後の B: 最新 develop 取り込みと base 固定後、共通機構と app 2型の C# 実装、上記 linked-source target の pure core 検査を行う。対象は ScriptSystem の純型（ScriptUpdateElement は除く）、Foundation の IUpdateElement / UpdateFrameContext、共通 runner / host契約 / Wait factory と app 2純型。Unity 型を stub して合格扱いしない。
- 親が cloud の既知 .NET 8 toolchain で既存純6sourceをコンパイルし、予算 / branch / Halt / 0予算の route probe に合格した（warning/error 0）。当時の証拠は cloud 作業領域の入替で現環境から参照できない。現在は公式hashを確認した同SDK / PowerShellを再配置し、compiler smokeの成功を確認済み。今回の実装については新たな固定headで検証・保存し直す。これは既存 source の実行経路成立であり、未実装 host / 新 runner の合格ではない。
- offline target は VM/runner 検査を Unity 無しで再現する最小補助で、Unity import・実画面・全 EditMode の代替ではない。新たな Harness profile / H2 の B限定Unity例外は導入しない。

Phase C の判定必須（GO候補の固定 implementation head）:

- `pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1`。
- `dotnet run --project tools/ScriptCommandOfflineTests/ScriptCommandOfflineTests.csproj --configuration Release`。実 source をリンクした結果を保存。
- `pwsh tools/run-tests.ps1 -Platform EditMode -Filter ''` で最終全 EditMode。適用除外はなし。必要 test 名 / 件数を XML で確認。
- authorized Unity 6000.6.0f1 / 当該 project の compile と visible flow。対象画面到達後、Idle / Run→3巡→Halt、途中Stop→再Run、manual Damage/Heal、Open Dialog、scene離脱中Wait停止、再入場Idleを観測。値の確認と表示・操作の観察を分ける。
- 保存証拠は exact head、入口と操作、command 順序・回数、停止後の無操作、consoleエラー、画面の代表画像または短い連続記録。原記録を C/C' に同じ版で渡す。画像だけで命令回数や unload 完了を証明しない。

検証経路の成立:

- 現 cloud に Unity はなく、親の確認では desktop は接続済みでも task authorization が false、saved environment はなし。既存アクセス確認への回答待ち。画面到達 / 操作 / 保存は未確認。
- UI 段開始前に authorized 担当 AI が project/Editor接続、既存 HpGauge入口、操作・撮影の疎通を最小確認する。Unity test/Build が必要な初回確認は Phase C 担当に渡し「未確認。初回確認は Phase C」と明記し、人間と不成立時の基盤整備または問いの分離を合意する。
- 入口が無ければ古い scene creator の全再生成で埋めない。必要な最小 Editor 配線または一時検証 fixture の対象を UI 段の追加合意で限定する。入口未成立のまま画面段を開始しない。
- 人間の手動テストを既定の代替にしない。今回は美観の新規基準を作らず、既存表示と操作の接続確認を AI が担当する想定。実行権限・証拠取得の合意がない間は UI 段 / M3 / M4 が blocked。C# / headless 段は先行可能。
- native終了stallの既知調査予算は消費済み。新規診断・session変更による回数リセットなし。既存規約で raw 結果と終了確認・受容判断を分離する。

公開チェックポイントは、(1) 純VM境界、(2) 共通 runner/Wait と app program/commands の headless実行、(3)許可済みUnityでのUI接続、と分ける。(1)/(2)の公開をM3/M4の合格や画面から利用可能な完成品とは扱わない。UI段を別の受け入れスライスへ分離する場合は人間A3で問いと必須検証を変更し、本計画の全EditMode条件を暗黙に除外しない。

A3 後の実装順序: 最新 develop 取り込み / base 固定 → 純 VM 境界 → 共通 runner/Wait/host契約 → app program/commands → pure tests → B機械検査・C# checkpoint の C発見。ここで UI 段未実装・Unity 未検証を明示する。UI 経路成立後に UI/lifecycle 配線・import → 最終headで判定必須 → blind C' → 人間D。

B→A停止条件: 上記外の API/状態/依存/所有者、複数command実行、別の時間意味、非同期/並列 host、Scene/asset配線拡大、予定の純単位でテスト不能が必要になった場合。単なる実装不具合は凍結条件内の B適応で直す。

## 6. Phase A レビュー状況

- A1: r1 / r2 は Draft PR #94 に公開済み。本 r3 は人間の共通 Framework / app 分離指示と C# / headless 先行承認を反映。r2 の request identity、停止優先、observer / cleanup 例外隔離は維持する。
- A2: r1 の固定本文 SHA-256 `e42640feee3b7080984d09d0942a4422eb3d4b8923021734c9e1bf387590e841` を対象に独立レビュー済み。公開 runner 構築 / read-only 観測 / dispatch 結果の未固定と、Action・状態通知の再入 / 例外時の停止優先・cleanup 境界の不足を採用候補とし、r2 の §3 / §5 で具体化した。M2 / M3 と他 Update 継続の常時契約に必要な補完で、汎用化や受入条件の免除はしない。モデル指定 gpt-6-astra の architecture 担当と gpt-6-sol の contract 担当が別セッションで r2固定本文（SHA-256 `0a29f1a0a47ff6dbecef453994ef22b350c9a0d12f799a85e7676a8d50b930b4`）を再確認し、各指摘の解消を確認した。追加の Waiting → Running / ack後pending消去の説明も反映。モデル名は起動時指定、実行側IDは未検証。これは設計/ソースの確認のみで、人間 A3 が採否を確定する。
- r3 A2: 固定本文 SHA-256 `0c97aa27e65848eec168c2ce0a9a958d1a0b23c190e4488f1e63d1623adfaa6b` を、モデル指定 gpt-6-astra の architecture 担当と gpt-6-sol の contract 担当が独立再確認。共通 API / ID 所有 / 時間選択 / FW→app 非依存 / headless 先行境界に阻害指摘なし。r2 の停止優先・cleanup契約も維持と確認。Unity実行の合格を意味しない。
- A3: 2026-10-05 03:43 UTC、人間は、アプリ固有処理と再利用するFramework実装を分離し、詳細を主担当に任せて進めるよう指示。直前に提示した C# VM 境界・HP command・cloud 検証先行、Unity UI / 最終検証未完の進め方への条件付き承認である。主担当は r3 A2 の阻害指摘なしという結果を採用し、2026-10-05 03:56 UTC に C# / headless の既承認範囲を凍結した。承認済み境界を変えない詳細は委任の範囲で統合し、UI 経路未成立を C# 段の再承認要求へ戻さない。
- C' の担当モデルは A2 / B / C 選定時に独立性を確保して予約し、推測で固定しない。

## 7. C# / headless checkpoint の実装・検証記録

- B: 共通Frameworkのhost境界、runner / Wait / 時間選択 / 停止と、appのHP命令列 / Damage・Heal hostを実装した。UI / Scene / UXML / asmdef / assetは変更していない。実装担当のモデル指定はgpt-6.1-sol。実行側モデルIDは独立には検証していない。
- 公開順: VM境界 `f6b43e75` → 共通runner `2e5a399a` → appとoffline target `0fea030e`。実装baseから23ファイル。testsの読みやすさのためoffline executableを目的別partial sourceへ分割し、production責務やAPIは増やしていない。
- 最終公開headへfetchした後に再実行: .NET 8 / C# 9のoffline 17群、失敗0。実命令列の独立した2実行が各3 Damage / 3 Heal / 6 Wait / Halted。buildはwarning/error 0。
- 18 production sourceをC# 9 / netstandard2.1でコンパイルしwarning/error 0。これはUnityコンパイルの代替ではない。
- contract-auditは620 C#ファイル、docs-auditは111文書、どちらもerrors/warnings 0。通常diff / base→head固定diffのcheckも成功。
- 補助B証拠: host probe 85 checks、runner probe 144 checks。source hashを最終sourceと照合したが、最終headの17群実行と混同しない。
- 発見C: モデル指定gpt-6-astraの新規レビューが固定A3、完全diff、実source、最終headのraw、source hashesを確認。凍結M1/M2または常時契約への確認済み違反は0件、修正要求なし。報告SHA-256 `926ebe110f1717816b082d30623864100220906906701dcd2d436e96c5092968`。
- M3 / M4: UI接続、稼働入口、Unity import / 新規.cs.meta、全EditMode回帰、操作・表示証拠は未実施。NUnit sourceは追加したがUnityで実行していない。判定C / C' / D、全体GO、merge承認はない。

### 証拠の取得

- bundle ID: `script-command-headless-0fea030e`。配布ファイル `ScriptCommand-Headless-0fea030e.zip`、51 payload + manifest、81,462 bytes。
- ZIP SHA-256: `4ebbd718deb83ff6f527e316b10ace49dc0bc5898cddc3d06d52a4da87806297`。
- manifest SHA-256: `0ef543e3360499e42adfa6d12f334bde37d28a7c18bfbe2cb65ce3da0ee09ea8`。
- 利用者へのChatGPT添付として保管・受け渡す。cloud作業領域は永続保存とは扱わない。第三者agentがこの私有添付を取得できることは未確認であり、必要なら利用者が受け渡し先を指定して転送後に再検証する。
- 展開後にmanifestの各path / size_bytes / sha256を照合する。最終再実行は `final/verification/`、固定A3は `a3-r3/`、発見所見は `discovery/`。sourceは上記実装commitから取得する。
- 保管担当は主担当、予定保持期限2026-11-05（自動削除はしない）。C'用にこの所見付きarchiveを再利用せず、UI段の最終head・必須rawが揃ってから新しいblind bundleを作る。
