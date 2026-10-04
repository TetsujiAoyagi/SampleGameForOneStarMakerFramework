# ScriptSystem command demo — Phase A1 r1

## 0. メタデータ

- type: `slice`
- status: `A`（A1 初稿。A2 / 人間の A3 は未実施。実装着手指示ではない）
- branch: `codex/script-usage-sample`
- implementation base commit: `09242db5cd8cf2cd6695522dcd4cee5fcfc020f6`
- implementation head commit: 未作成
- risk: `high`（Framework 公開 API と view 所有の実行寿命を追加）
- owner: root。A1 作成担当: plan_script_command_demo / OpenAI
- created: 2026-10-04 UTC
- expires: 2026-10-11 UTC または置換 revision。期限は再確認点であり自動承認ではない
- harvest to: `unity/Assets/Docs/Architecture/27-folder-structure.md` §2.3。実装・検証した契約とサンプルの入口だけ反映し、完了時に本 HANDOFF を削除
- Phase A snapshot: A2 配布時に本文の固定コピー・生成時刻・SHA-256 を作業管理側で記録する
- Phase B result / evidence / C' blind bundle: 未作成
- 作業方式: 従来の tracked HANDOFF。H1 / H2 / external-current-v1 は採用しない

## 1. A0 — 目的、現況、対象外

目的は、既存 ScriptSystem が数値計算だけでなく、アプリ所有の型付き操作を順番に依頼する使い方を、既存 HpGauge で学習・検証できる最小例を作ることである。普通のゲームイベントに VM が必要、あるいは直接の型付き C# より優れるという実証ではない。

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
- 本番 Scene / Prefab / SceneResource / Addressables の再生成・YAML 編集。入口に新規配線が必要なら A3 前に限定範囲を追加合意する。

## 2. 意思決定と受け入れ境界

このスライスが答える問い: 純 VM とアプリの明示的な要求 / 完了境界を通し、HpGauge 上で Run → Damage → Wait 0.5秒 → Heal → Wait 0.5秒を3回 → Halt を、停止・再実行・view 破棄時にも重複副作用なく動かせるか。

進める最低条件とその詳細:

- M1: host 待機 / 完了 / 故障が純 VM テストで確定し、従来9命令・ゼロ予算の契約が保たれる。
  - 要求到達時は PC を動かさず WaitingForHost。繰返し Tick で同じ要求を返し、副作用・再発行なし。
  - 正しい要求の完了だけを1度受理。成功時に PC を1つ進め、失敗時は当該 PC で終端故障。古い・別 machine・二重完了は無変更で拒否。
  - 正でない予算は待機中・Ready・終端後にも RejectedBudget を先に返し、保存状態を変えない。
- M2: 実際の C# 組立 program を app runner が実行し、Damage / Wait / Heal / Wait を3回だけ順に dispatch して Halt する。
  - VM 内に Unity / delegate / 時計 / gameplay API を入れない。command の種類・引数の意味と操作はアプリが持つ。
  - 1 Update に Tick は最大1回、host command 開始は最大1件。Start / LateUpdate では進めない。直ちに完了する操作も次の命令は次 Update 以降。
  - Wait は時間入力で進み、Tick 予算やフレーム数を秒に読み替えない。paused / 停止中は進まない。
- M3: opt-in の既存 HP 画面で動作と寿命を確認する。
  - 初期状態は Idle。Run / Stop と小さな実行状態表示だけ追加。既存 Damage / Heal / Open Dialog はそのまま利用可能。
  - Run 中の再 Run は無処理。Stop は即座に gate を閉じ、以後の tick / 完了通知は無処理。Halt / fault / Stop 後の Run は新規 machine / registers / runner。program だけ共有。
  - scene pre-unload と View の Track cleanup は停止 gate → unregister 要求 → 参照解放の順。解除遅延期間でも旧 runner が ViewModel を触らない。
  - 手動 Damage / Heal と demo は同じ既存 ViewModel を使う。乱数値・最終 HP 一致ではなく command 順序・回数・範囲と表示変化を観測する。
- M4: GO 候補の最終 head で標準 runner の全 EditMode 回帰と必要な visible flow の証拠が揃う。cloud の純テストだけで M3 / M4 を通過扱いしない。


判定: M1〜M4 が成立して GO。未達・未検証は NO-GO / blocked と区別して記録し、CONDITIONAL ACCEPT は設けない。満たした後は後続の言語・汎用化を理由に拡張しない。

cloud での M1 / M2 の純実行証拠は中間 checkpoint であり、M3 / M4 の完了ではない。A3 で人間が「純境界と headless sample を先行スライス、UI 統合を別スライス」と分けることを選べるが、その場合は revision を作り、各最低条件と全 EditMode の扱いを改めて合意する。本 A1 自体は分割も Unity 回帰の免除も決定しない。

ここでは答えない問いの所有先（未承認の後続候補。今回の実装キューではない）:

- テキスト言語 / 外部保存形式 / 入力検証一般化: 「Script Authoring」後続候補。
- 戻り値、複数引数、非同期・並列操作、汎用 host: 「Script Host Expansion」後続候補。
- 他画面での価値や直 C# との比較: 「Script Usage Evaluation」後続候補。
- HpGauge 到達のための恒久 navigation / asset 整備が必要な場合: 「HpGauge Entry」候補として A3 で今回への最小編入か問いの分離を決める。

未決事項 / A3 blocker: Unity の実行権限・正しい project / Editor、既存 HpGauge への到達と観測・保存経路。親の既存アクセス確認に回答待ちであり、再質問や暗黙の人間代行はしない。A2 後にこの経路の担当・証拠・不成立時の扱いを人間と合意してから凍結する。

## 3. 最小 API と実行契約

### Framework の要求 / 完了境界

- `ScriptOpcode.HostCommand = 9` を末尾追加。既存番号を変えない。
- `ScriptInstruction.HostCommand(int commandId, long argument)`。既存オペランドの Destination を commandId、Immediate を argument として使う。Left / Right は未使用。commandId は register index ではない。FromRaw も同じ実行意味。
- Framework は commandId / argument の業務上の合法性を知らない。アプリが未知 command / 不正引数を失敗完了にする。未知 opcode 自体は従来の InvalidOpcode。
- 新規 `ScriptHostRequest` は sealed な不変純 C# オブジェクト。公開 getter は CommandId / Argument、生成は machine 内部。要求オブジェクトそのものの参照同一性を one-use completion token とし、別 token 型・連番・global ID を追加しない。
- `ScriptMachine.PendingHostRequest` は nullable request。host opcode に初めて到達したとき1個作り、待機中は同じ object。数値命令経路の追加 allocation はない。1 host 境界につき小さい allocation があることを明示し、allocation-free host を目標にしない。
- 状態を末尾追加: WaitingForHost、Ready、HostCommandFailed。Ready は成功 ack 後・次 Tick 前を表す。Yielded は引き続き予算消費だけを表す。
- `bool TryCompleteHostCommand(ScriptHostRequest request, bool succeeded)` は pending の同一参照だけ受理する。受理時に pending を消す。成功なら PC++ / Ready（非終端）、失敗なら PC 不変 / HostCommandFailed（終端）。null・別 request・完了済み request・終端後は false / 完全無変更。受理処理は次命令を実行しない。
- positive Tick の優先順は既存終端 → pending 待機 → 命令実行。非正予算の拒否はこれらすべてより先。HostCommand 到達は1命令分の予算を消費し、直ちに WaitingForHost を返す。待機確認で予算や PC を消費しない。
- Tick、register 変更、完了は呼出側が同一スレッドで逐次化する。キャンセル API / reset API は追加せず、新規 machine で再 Run する。HpGauge の Run / Stop / Tick / dispatch / 完了 / UI通知は Unity メインスレッド専用。Damage / Heal から reactive binding 経由で UI を変更するため worker thread 実行を許さない。
- `ScriptUpdateElement` の動作は変えない。host のない既存利用を継続し、host request があれば外側が完了するまで進まないことをコメントで明示する。demo runner と同じ machine を二重登録しない。

### アプリの command と program

- `HpGaugeScriptCommands` は固定の app enum（Damage / Heal / WaitMilliseconds）のみを解釈する。Damage / Heal は引数0のみ、WaitMilliseconds は非負 long のみ。未知 ID / 不正引数を副作用前に拒否する。
- Damage / Heal は constructor で渡された型付き `Action` をそれぞれ1回呼ぶ。View が既存 ViewModel.Damage / Heal を渡す。Wait は秒数へ変換した待機指示を runner に返し、commands 自身は時計・タイマーを持たない。結果は即時完了 / 待機 / 拒否を区別する小さい app enum と待機値で足りる。
- `HpGaugeScriptProgram` は不変 program を1個組み立てる。r0=3、r1=1、Damage(0)、WaitMilliseconds(500)、Heal(0)、WaitMilliseconds(500)、Sub(r0,r0,r1)、JumpIfNotZero(r0, Damage位置)、Halt。9命令、2 registers。数値制御は実際に VM が行い、C# 側で3回を再現しない。
- hp 改変例外は runner が現在要求を失敗完了させて終了する。副作用後の例外でも rollback / retry はしない。以後の host request を実行せず、他 Update 要素は継続する。

### runner と UI 寿命

- `HpGaugeScriptRunner` は純 C# の `IUpdateElement` / IDisposable。1 instance が1 Run の machine / registers、現在要求、待機残量、停止 gate と状態通知を所有する。Framework の ScriptUpdateElement は内包・登録しない。
- OnElementUpdate は停止 / pause gate を先に見る。待機中は正の有限 `context.UnscaledDeltaTime` だけを残量から減らし、満了なら同一要求を成功完了して return。負・NaN・infinity は進行に使わない。待機を開始したフレームの delta をさかのぼって加算しない。
- それ以外は固定予算16で Tick を最大1回行う。新規要求を処理済みとして捕捉してから app command を始める。即時完了後も return。Wait(0) も開始時に成功完了して return。余剰 delta の持越し・一括 catch-up はない。
- 登録 layer はアプリ固有の `HpGaugeScriptDemo`、layerOrder=0 / executionOrder=0。既定 scale=1 の独立 layer とし Gameplay pause を借用しない。UI の実時間用 unscaled delta を使うが、この layer 自体の pause は尊重する。layer の保持者は既存 Coordinator で App 寿命、view が所有するのは runner 登録だけである。停止時に layer 自体を削除しない。view が layer pause / scale を勝手に変更しない。標準 RegisterElement が layer を作成するため AppInitializer 変更は不要。layer 定義自体は Coordinator/App 寿命で空のまま残ってよく、runner / view の残存とは区別する。
- Wait の500msは蓄積入力時間の下限であり、実表示はフレーム単位に量子化される。開始や完了後の別 Update 境界があるため、全体が厳密に3.000秒で終わるとは保証しない。
- View は1つの active runner を保持し Run 時に生成・登録する。登録失敗なら停止・破棄して Failed 表示とし、直 Tick / MonoBehaviour.Update の迂回をしない。
- 終端通知と Stop は view の共通 cleanup を通す。runner 自身の停止 gate を閉じてから通知 / unregister / app参照解放。古い runner の通知は current instance 同一性で拒否。非同期 completion callback は設けない。
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
| 同 HpGaugeScriptCommands.cs（新規） | 固定3操作の引数検査・型付き app dispatch。借用 Action、Run 寿命。Unity 無し | 0 → 60〜85 |
| 同 HpGaugeScriptRunner.cs（新規） | bounded Update / wait / fail / stop の orchestration。Run 所有。Foundation context と ScriptSystem と app commands のみ | 0 → 140〜190 |
| 同 HpGaugeView.cs | UI binding と登録・view寿命接続。UI / Runtime API の外側 adapter | 64 → +65〜90 |
| 同 HpGaugeScene.cs / HpGauge.uxml | pre-unload停止 / opt-in操作表示 | 52 / 11 → +5〜10 / +6〜10 |
| Tests/ScriptSystem/ScriptHostCommandTests.cs（新規） | 純 VM request/ack/予算/故障。Framework test asmdef | 0 → 160〜220 |
| SampleGame/Tests/App/HpGaugeScriptRunnerTests.cs（新規） | fake Actions / 注入 context の順序・時間・停止。既存 SampleGame.Tests asmdef | 0 → 170〜240 |
| SampleGame/Tests/Editor/OutGame/HpGaugeScriptViewTests.cs（新規） | UXML import / button / lifecycle / 実 ViewModel の接続。既存 Editor test asmdef | 0 → 100〜150 |
| tools/ScriptCommandOfflineTests/（新規 csproj + Program.cs） | .NET 8 dependency-free linked source の cloud 純テスト。既存 DebugCommandOfflineTests と同形式 | 0 → 180〜230合計 |

app の3中核型は既存 HpGauge namespace 内の限定された public API とする（独立 test assembly から直接利用）。新たな汎用 service interface / friend assembly / asmdef は作らない。program、dispatch、runner は変更理由・依存・テストが異なるため分け、class 数だけの新フォルダは作らない。

50% 増加警報は小さい enum と View に該当する。enum は単一値契約のため非分割。View は UI/lifetime adapter のままにし、命令・時間・dispatch の本体は3新規型へ分離するので非分割。既存 ScriptMachineTests は524行なので host テストを新規ファイルに分ける。新規 runner が190行程度を越えて複数理由を持ち始めたら便宜的 Manager を作らず A に返す。

現 Phase A で作成する tracked 差分は本 HANDOFF だけ。docs/README.md の一覧は program/research 専用のため slice 行を足さない。上表は A3 後の候補であり今回実装しない。UI source 編集・Editor import の担当環境も A3 で確定する。

## 5. テスト、検証経路、実装順序

発見用起点 filter:

`OneStarMaker.Tests.ScriptSystem|SampleGame.Tests.HpGaugeScriptRunnerTests|SampleGame.Tests.Editor.OutGame.HpGaugeScriptViewTests`

実装時は新規 test の namespace を上記へ合わせる。既存 namespace 全件性は filter だけで推定せず XML の名前・件数で確認する。

純テストの最小集合:

1. request 発行の PC / 同一性、positive Tick 再入、host 命令直前の予算切れ、全状態の0/負予算拒否。
2. success ack と Ready、次 Tick だけが次命令を進める、failure latch、null / 他 machine / 古い / 二重 ack の無変更拒否。
3. raw HostCommand の ID/argument 使用・未使用欄無視、未知 opcode の従来 fault、終端と自然終端。
4. 実 sample program の3巡 / 3 Damage / 3 Heal / 6 Wait / Halt。fake Actions で順序を確定する。
5. 0.49秒では未完、0.5秒境界、Wait(0)、pause、unscaled と scaled の相違、無効 delta、開始フレームの delta 不採用、長いフレームでも command 一括実行なし。
6. command 未知 ID / 不正引数 / Action 例外の fail-closed。例外後再実行なし、後続 Update 要素継続。
7. Stop 中の待機、Stop→Run、active Run の二重押下、登録待ちの Stop、active 解除遅延中の Tick、古い通知、dispose後呼出し。新 machine/registers と同じ immutable program を確認。

cloud の範囲:

- A1: source・契約・差分確認、docs-audit。Unity 起動 / test は行わない。
- A3 後の B: C# 実装、上記 linked-source target の pure core 検査が可能な実行経路を確認する。対象は ScriptSystem の純型（ScriptUpdateElement は除く）、Foundation の IUpdateElement / UpdateFrameContext、app 3純型。Unity 型を stub して合格扱いしない。
- 親が cloud の既知 .NET 8 toolchain で既存純6sourceをコンパイルし、予算 / branch / Halt / 0予算の route probe に合格した（warning/error 0）。証拠は worktree 外の `script-usage-evidence/route-probe`。これは既存 source の実行経路成立であり、未実装 host / 新 runner の合格ではない。
- offline target は VM/runner 検査を Unity 無しで再現する最小補助で、Unity import・実画面・全 EditMode の代替ではない。新たな Harness profile / H2 の B限定Unity例外は導入しない。

Phase C の判定必須（GO候補の固定 implementation head）:

- `pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1`。
- `dotnet run --project tools/ScriptCommandOfflineTests/ScriptCommandOfflineTests.csproj --configuration Release`。実 source をリンクした結果を保存。
- `pwsh tools/run-tests.ps1 -Platform EditMode -Filter ''` で最終全 EditMode。適用除外はなし。必要 test 名 / 件数を XML で確認。
- authorized Unity 6000.6.0f1 / 当該 project の compile と visible flow。対象画面到達後、Idle / Run→3巡→Halt、途中Stop→再Run、manual Damage/Heal、Open Dialog、scene離脱中Wait停止、再入場Idleを観測。値の確認と表示・操作の観察を分ける。
- 保存証拠は exact head、入口と操作、command 順序・回数、停止後の無操作、consoleエラー、画面の代表画像または短い連続記録。原記録を C/C' に同じ版で渡す。画像だけで命令回数や unload 完了を証明しない。

検証経路の成立:

- 現 cloud に Unity はなく、親の確認では desktop は接続済みでも task authorization が false、saved environment はなし。既存アクセス確認への回答待ち。画面到達 / 操作 / 保存は未確認。
- A3 前に authorized 担当 AI が project/Editor接続、既存 HpGauge入口、操作・撮影の疎通を最小確認する。Unity test/Build が必要な初回確認は Phase C 担当に渡し「未確認。初回確認は Phase C」と明記し、人間と不成立時の基盤整備または問いの分離を合意する。
- 入口が無ければ古い scene creator の全再生成で埋めない。必要な最小 Editor 配線または一時検証 fixture の対象を A3 revision で限定する。入口未成立のまま既存画面に実装すれば終わると凍結しない。
- 人間の手動テストを既定の代替にしない。今回は美観の新規基準を作らず、既存表示と操作の接続確認を AI が担当する想定。実行権限・証拠取得の合意がない間は blocked。
- native終了stallの既知調査予算は消費済み。新規診断・session変更による回数リセットなし。既存規約で raw 結果と終了確認・受容判断を分離する。

公開チェックポイントは、(1) 純VM境界、(2) app program/commands/runnerとheadless実行、(3)許可済みUnityでのUI接続、と分ける。(1)/(2)の公開をM3/M4の合格や画面から利用可能な完成品とは扱わない。UI段を別の受け入れスライスへ分離する場合は人間A3で問いと必須検証を変更し、本計画の全EditMode条件を暗黙に除外しない。

A3 後の実装順序: 純 VM 境界 → app program/commands/runner → pure tests → authorized UI/lifecycle配線・import → B機械検査と未実行記録 → C発見 → 最終headで判定必須 → blind C' → 人間D。

B→A停止条件: 上記外の API/状態/依存/所有者、複数command実行、別の時間意味、汎用 host、Scene/asset配線拡大、予定の純単位でテスト不能が必要になった場合。単なる実装不具合は凍結条件内の B適応で直す。

## 6. Phase A レビュー状況

- A1: 本 r1。A2へ同じ固定本文を配布予定。
- A2: 未実施。公開 API / identity / 時間の独立レビューと、責務・寿命・依存・検証経路の architecture review を分担する。互いの findings を渡さない。
- A3: 未実施。人間の採否・未決経路の担当・証拠の扱いを記録するまで未凍結。
- C' の担当モデルは A2 / B / C 選定時に独立性を確保して予約し、推測で固定しない。

## 7. Phase B / C / C' / D

すべて未着手。implementation head、結果 snapshot、判定 bundle、blind bundle、各モデルと独立性、必須テストの実行/未実行、現在の条件に対する違反根拠と後続入力、D の人間判断は到達時に追記する。A1 作成を実装・検証・承認と読み替えない。
