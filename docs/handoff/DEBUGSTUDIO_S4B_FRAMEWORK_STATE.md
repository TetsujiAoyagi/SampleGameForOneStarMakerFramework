# DebugStudio S4b — 非 MonoBehaviour の Framework の状態を CLI から読む（最初の対象は Scene）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `A`（A0 と A1 初稿。A2 は未実施、A3 は未凍結。**実装キューではない**）
- 前提: [DEBUGSTUDIO_S4A_COMMAND_SURFACE.md](DEBUGSTUDIO_S4A_COMMAND_SURFACE.md)（コマンドの口）。本書は S4a の口（宣言付きの登録、`RegisterFramework`、`osm.commands`）の上に `osm.scenes` を 1 つ載せる。**S4a の Phase D の前に本書の Phase B を始めない。** S4a の A3 で口の形が変わったら、本書の A1 を合わせて直してから A2 に出す。
- branch: `claude/magical-gauss-7rycuj`（Cloud の計画 session の branch。PR の base は `develop`。PR は未作成）
- implementation base commit: `07cc2c975b63cf5b668130a1c45490dd3e77c5f0`（`origin/develop` の head、2026-11-25。本書の実測は全てこの base。S4a の merge の後に取り直す）
- implementation head commit: `未固定`
- A3 凍結記録: `未凍結`
- risk: `normal`（理由は §2.6）
- owner: root（依頼者）
- created: 2026-11-25
- expires: 2026-12-31、または A3 の凍結で置き換わった時点。期限時は owner が更新・廃止を判断する
- harvest to: `tools/DebugStudio/src/DebugStudio.Cli/README.md`（`osm.scenes` の送り方と出力の読み方）、`unity/Assets/Docs/Architecture/05-scene.md` §5.12（外から読める Scene の状態と、観測が寿命と状態を変えないこと。あわせて §5.12 の `SceneEvent` の欄の記述が code に遅れている点を直す）
- Phase A snapshot の取得案内・状態: 未生成
- Phase B result snapshot / evidence bundle / C' blind bundle の取得案内・状態: 未生成
- task: 未開始（`tools/workflow-task.ps1 start` は凍結の時に行う）
- 引継ぎ票:
  - 次の Phase・役割: Phase A の主担当。S4a の A3 の凍結を待つ。
  - 次の作業: (1) owner が §1.9 の U1 を確認する。(2) S4a の凍結版の口に合わせて §2.1 を確かめ、A2 の独立レビュー（目的レビュー 1 件、アーキテクチャゲート 1 件）を同じ入力版で行う。(3) A3 で採否を記録し、Draft PR で外部計画レビューを受けてから凍結する。
  - 読む範囲: 既定（[SKILL.md](../../.agents/skills/osm-workflow/SKILL.md#作業ごとの読み先) の表。Phase A は全節）と、S4a の §2.1。
  - 必要な権限: なし（文書のみ）。
  - 停止規則: S4a の凍結の前に A2 を始めない。S4a の Phase D の前に Phase B を始めない。
  - 引き渡さないもの: なし。

### 実測の方法

- base `07cc2c97` の code と文書を `grep`・`wc -l`・行範囲の読み取りで確かめた。Unity も DebugStudio も起動していない。
- 本書の作成環境（Cloud の Linux container）に `pwsh`・`dotnet`・Unity が無く、`tools/docs-audit.ps1` は未実行。link 先の存在は `git ls-files` で手で照合した。

## 1. 目的と対象外

### 1.1 目的と利用者（A0 で書いた。A2 の目的レビューへ渡す原文の候補）

- 目的: Editor の通常の Play 中に、GameObject を持たない Framework の状態を、表示のための GameObject や MonoBehaviour を作らずに、DebugStudio の CLI からその時点の値として読めるようにする。最初に読めるようにするのは SceneDirector が管理する Scene の一覧と各 Scene の `SceneState`、遷移の履歴である。
- 利用者: SampleGame を Editor で Play し、DebugStudio WPF を起動したまま CLI のコマンドを実行してその出力を読む開発者と AI エージェント（検証の driver を書く担当と、判定 C の実行担当を含む）。
- 目的・利用者の文言の出どころ: owner の 2026-11-25 の指示。「unity の MonoBehaviour でないクラスはデバッグ表示用の GameObject か MonoBehaviour 作ろうと思ってたが、まず DebugStudio で表示するのが素直だな」。同日の質問への回答で owner が決めた: 表示先は CLI の JSON だけ、実行形態は Editor の Play だけ、コマンドの口は S4a に分ける。

### 1.2 対象外

- コマンドの口そのもの（S4a）。
- WPF の表示（専用の tool window・store・GUI の変更）。protocol・wire の変更。CLI の新しいサブコマンド。既存の `send` に載せる。
- Player（Build した実行ファイル）と遠隔接続。`osm.scenes` は `EditorOnly` で宣言する。
- 書き込み。状態を変える操作（Scene の load・unload・遷移、`SceneState` の変更、履歴の変更）。
- Scene 以外の状態の読み取り（AssetManagement の所有者ごとの保持、UpdateSystem の layer と登録、Camera・Sound・VFX 等）。それぞれ別のコマンドとして同じ口に後から載せる（§2.5）。
- 状態の変化の購読・履歴の蓄積・差分の配信。読むのは要求した時点の値だけ。変化の流れは既存の telemetry（`SceneLoad` / `SceneUnload` / `SceneTransition` の span）が担う。
- 要求時解決の記録（`ScenePair.ContentRecord`、`GetContentLoadRecord`）の中身。program [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md) の S4 系が形を変えている最中なので出さない（§2.5）。
- hierarchy に DontDestroyOnLoad の GameObject を出すこと（S3 から移送された項目。§2.5）。
- `SceneState` の値・順序の変更、`SceneLifecycleManager` の遷移規則の変更、SceneDirector の挙動の変更。

### 1.3 現況（base `07cc2c97` の code）

**Scene の状態の在処（全て `Runtime/SceneSystem/`）:**

- Scene の木は実行時の状態ではなく静的な data。`SceneResource`（ScriptableObject、121 行）が `Parent`・`Children`・`LoadType`（`OnDemand` / `NecessaryAlways` / `IncrementalAlways`）を持ち、`SceneResourceMap.SceneResources` が全 node を並べる。SceneDirector は map を private の `_sceneResourceMap` に持つ。
- 実行時の状態は `SceneDirector.cs`（336 行。ほかに partial の `.Loading.cs` 747・`.Unloading.cs` 478・`.Transitions.cs` 256・`.FailedLoadCleanup.cs` 349）の private field にある。
  - `_currentScenes`: `Dictionary<string, ScenePair>`（:77）。親子の link は無い平らな辞書。`ScenePair` は private の入れ子 class で、`SceneBase`・`BackendSceneLoaded`・`LoadCts`（取消の窓。PoNR を過ぎると null）・`ContentRecord` を持つ。
  - `_pendingUnloads`: `HashSet<string>`（:78）。
  - 進行中の操作: `_inFlightAddScenes`・`_inFlightSceneBaseLoads`・`_inFlightUnitySceneLoads`・`_inFlightUnloads`（:79〜82、どれも `Dictionary<string, UniTaskCompletionSource>`）。
  - 履歴: `Stack<SceneHistoryEntry>`（:104。From / To の組、上限 `MaxSceneHistoryCount = 8`）。
  - 各 Scene の `SceneState` は `SceneBase.Lifecycle`（internal の `SceneLifecycleManager`、113 行）の `State`。`SceneLifecycleManager` だけが `_state` を書き、遷移を検査する。
- 読み取りの口: 公開は `ISceneQuery`（`GetLoadedScene`・`IsSceneLoaded`・`IsSceneStable`・`GetContentLoadRecord`）、`CanGoBack`、`OnSceneEvent`（R3 の `Subject<SceneEvent>`）。internal に「テスト用」の `ContainsScene`・`GetSceneState`・`HasPendingUnload`・`SceneHistoryCount`（:271〜283）。**全 Scene を列挙する口、まとめて写す口、履歴の中身を読む口は無い。**
- 変更は全て UniTask の async method の中で main thread で起きる（SceneSystem に thread pool への切替え・`lock` は無い）。await をまたいで操作が交互に進むので、frame の間に main thread で読むと thread の競合は無いが、操作の途中の状態（進行中の entry、`LoadCanceled` など）が見える。
- `SceneState` は 14 値（`None`=0, `PreLoading`, `PreLoaded`, `Loading`, `Loaded`, `WaitLoadChildScene`, `Initializing`, `LoadCanceled`=7, `Stable`=8, `PreUnloading`, `PreUnloaded`, `Unloading`, `Unloaded`, `AfterUnloading`=13）。

**生成と寿命（`Runtime/Bootstrap/AbstractApplicationInitializer.cs`、1486 行）:**

- DI container は無い（手動 DI。[03-di.md](../../unity/Assets/Docs/Architecture/03-di.md)）。SampleGame は `static readonly AppInitializer s_instance` を 1 つ持つ。
- BeforeSceneLoad で DebugSocket とコマンドの catalog を作る（S4a の後は Framework の登録 → Game の hook の順）。AfterSceneLoad で `SceneDirector` を作る（:439）。**catalog の登録は SceneDirector より先に終わる**ので、`osm.scenes` の handler は SceneDirector を実行の時に読む必要がある。App 寿命の service へ SceneDirector を後から渡す先例は `UpdateSystemHost.BindSceneDirector`（:450）と `Vfx.BindSceneLifecycle`（:451）。
- `ReleaseAll` は services（DebugSocket を含む）を Dispose してから（:1097）、`_sceneDirector` を Dispose して null にする（:1108〜1109）。initializer には `protected SceneDirector? SceneDirector`（:128）がある。
- Enter Play Mode Options が有効で Domain Reload が無効。Play ごとに service と SceneDirector を作り直す（S3 の HANDOFF の実測）。

**DebugSocket とコマンド:**

- `DebugSocketServices/*`・`DebugCommands/*`・`SceneSystem/*`・`Bootstrap/*` は全て `OneStarMaker.Runtime.asmdef`。SceneSystem を読むのに asmdef の参照の追加は要らず、循環も生じない。
- `Runtime/DebugCommands/` は System だけに依存する決まり（offline テストが Unity なしで compile し、folder の全 file の文字列を検査する。S4a §1.3）。**SceneSystem の型を参照するコマンドはこの folder に置けない。**
- コマンドの口・宣言・`osm.commands`・main thread での実行・結果の出力は S4a の §1.3 と §2.1 を正とする。
- Framework の状態を Inspector に出すための GameObject・MonoBehaviour は今は無い。Editor の `SceneGraph` 窓は SceneResource の asset を編集する道具で、実行中の SceneDirector を読まない。今 DebugStudio に届く非 GameObject の情報は telemetry の event の流れ（問い合わせ不可）と `runtime-diagnostics`（transport 自身の状態）だけ。

### 1.4 常時契約と既知の制約（本文へ転記）

- 依存は Game → Framework の一方向。asmdef の参照と package を追加しない（このスライスは要らない）。
- `SceneState` の 14 値を減らさず並べ替えない。状態の変更は `SceneLifecycleManager` が所有する。出力に整数値を出さず名前で出す（§2.1）。
- Unity 側の C# で `record` を使わない。新規・編集する Unity 側の `.cs` の先頭に `#nullable enable`。
- 破棄されうる `UnityEngine.Object` は `== null` / `!= null` で判定する。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。
- Editor のコードを Runtime の asmdef に置かない。`Runtime/DebugCommands/` を System だけに保つ。
- path は `tools/path-budget.json` の予算（repo 相対 150 文字）に従う。
- Unity テスト・Build は Phase C。Phase B は Editor での compile 確認まで。Unity 終了時の native stall は `tools/Harness/README.md` の停止規則に従う。
- S2 / S3 / S4a の契約を壊さない。
- 05-scene.md §5.12: `OnSceneEvent` の購読者は SceneDirector の挙動に影響してはならない。観測が状態を変えないという要求はこれと同じ向き。
- program の停止規則: Runtime を触るスライスの Phase B を `RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` の Phase B と同時に走らせない。本スライスは `AbstractApplicationInitializer.cs` の Framework の登録の箇所に 1 行足すので当たる（§2.6）。
- 人間が開いた Editor を強制終了しない。共有の Editor を触らない。並行の S4-V・S4-3a の Unity の実行と時間を分ける。

### 1.5 原資料

- S3 の HANDOFF（commit `d0e4c03c` の親の `docs/handoff/DEBUGSTUDIO_S3.md`）§2.4: 汎用編集・別の操作コマンド、Play をまたぐ指定の識別、DontDestroyOnLoad の表示、inspect の自己応答保証を「DebugStudio S4 以降（未起票）」に送った。本スライスはそのどれも引き受けない（§2.5）。

### 1.6 A0 確認

未実施。経路（CLI の `send` → WPF → Runtime の catalog → main thread → `CommandResult`）は S3 の判定 C で成立し、S4a の判定 C で `osm.commands` についても確かめる。本スライスは同じ口に新しいコマンドを 1 つ足すだけなので、未知の操作経路は無いと見る。

### 1.7 UC ごとの利用者の入力

| UC | 利用者が選ぶ・入れるもの | 機械が決めるもの | 誰がいつ | 目的からの正当化 |
|---|---|---|---|---|
| UC1 Scene の状態を読む | コマンド名 `osm.scenes`（`osm.commands` の一覧と README にある固定値）、payload `{}` | 出力の中身（§2.1 の 2）、読んだ時点の frame の番号 | 実行時の操作 | 「Scene の一覧と各 Scene の `SceneState`、遷移の履歴」 |
| UC2 読めないとき | 新しい入力は無い | `unavailable: …` の区別（§2.1 の 3） | 実行時の操作 | 「その時点の値として読める」。読めない時に古い値や空を正常と誤読させない |

A1 自己点検: 準備の操作も開発時の宣言も無い（SampleGame の Scene・設定 asset・config・code は変えない）。新しい入力はコマンド名だけで、`osm.commands` で見つけられる。

### 1.8 スライスの分割の検討（A1 の終わり）

- 契機: なし。口は S4a に分けた（owner 決定、2026-11-25）。本書に残るのは「写す」と「コマンドとして出す」で、片方だけでは利用者が観測できる成立条件が作れない。
- 分けたもの: Scene 以外の状態、WPF、Player は対象外にして §2.5 へ送った。

### 1.9 未決事項（owner の確認待ち）

- **U1 進行中の操作と取消の窓を出すか。** 出すと「途中の状態が見える」ことを利用者が出力だけで区別できる（`LoadCanceled` などを誤読しない）。出さないと出力が小さい。A1 は出す案で書いた（§2.1 の 2）。
- 解消した事項: 汎用の「状態の source の表」は作らない（S4a の `osm.commands` が読める物の一覧を担うので、source ごとに `osm.<名前>` のコマンドを口に載せれば足りる）。コマンド名は `osm.scenes`（`osm.` は S4a で Framework の予約）。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: SampleGame の通常の Editor の Play 1 回の中で、GameObject・MonoBehaviour・購読を作らずに、DebugStudio の既存の `send` だけで、SceneDirector が管理する Scene の一覧・各 Scene の `SceneState`・遷移の履歴をその時点の値として読め、読む前後で Framework と Game の状態が変わらないか。
- 次スライスへ進む最低条件: §2.2 の不変条件 I1〜I4 と成立条件 E1〜E3 を同じ GO 候補 head で満たす。

### 2.1 統合設計（A1 初稿。A2 の対象）

1. **登録（`AbstractApplicationInitializer`）**
   - S4a の「Framework のコマンドを登録する」箇所で、`osm.scenes`（`Read`、`EditorOnly`、payload の例 `{}`）を `RegisterFramework` で登録する。handler には `() => SceneDirector`（initializer の protected accessor）を渡し、SceneDirector そのものを登録の時に捕まえない（BeforeSceneLoad では null。`ReleaseAll` の後も null。§10.5 の「Scene 所有の object の強参照の登録は非対応」と同じ考え）。
2. **写し（`Runtime/SceneSystem/`）**
   - 新規 partial `SceneDirector.DebugSnapshot.cs` に `internal SceneDirectorDebugSnapshot CaptureDebugSnapshot()` を置く。main thread で、await を挟まず一度に private field を読み、不変の data に写す。field に書かない、event を出さない、`SceneLifecycleManager` を遷移させない、`SceneBase` の method を呼ばない（`Lifecycle.State` と `SceneResource` の読み取りだけ）。
   - 写す物（出力の JSON の形は A1 の案。A2 の対象）:
     - `capturedFrame`（`Time.frameCount`）。
     - `scenes`: `SceneResourceMap` の各 node について `id`・`parent`・`children`・`loadType`（名前）と、管理下なら `state`（`SceneState` の名前。整数は出さない）・`pendingUnload`・`cancelWindowOpen`（`LoadCts != null`）・`inFlight`（add / sceneBase / unityScene / unload のうち進行中の物の名前の列）・`backendSceneLoaded`。管理下でなければ `state` は `null`。
     - `unmappedScenes`: `_currentScenes` にあって map に無い id（Editor で開いていた Scene の再利用など）を同じ形で。
     - `history`: 新しい順の `{from, to}` の列と `canGoBack`。
   - `ContentRecord` は出さない（§1.2）。
3. **コマンド（新規 `Runtime/SceneSystem/Diagnostics/`）**
   - `SceneDirectorDebugSnapshot.cs`（写しの型）と `ScenesDebugCommand.cs`（internal static。handler と JSON 化）を置く。`Runtime/DebugCommands/` は System だけに保つ決まりなので、SceneSystem の型を読むこのコマンドはそこに置かない。JSON の文字列の escape は S4a が `DebugCommands/` に置く escape の規則を使う（S4a の A3 で internal の helper として公開されれば呼ぶ。されなければ同じ規則をここに持つ。A2 の対象）。
   - 結果（README に書く。CLI の動作は変えない）:
     - 成功: `success=true message=captured`、payload が上の JSON（exit 0、stdout）。`capturedFrame` の時点の値で、続けて読んだ時の値と同じとは限らない。進行中の操作がある時は途中の状態が出る（`inFlight` と `cancelWindowOpen` で区別できる）。
     - `success=false`、message が `unavailable: scene-director-not-created`（exit 1、stderr）: AfterSceneLoad の前・起動の失敗の後・`ReleaseAll` の後。古い写しを返さない。
     - Player では未登録（S4a の規則）。それ以外（handler の例外、期限切れ、`CommandResult` の行が無い終わり方）は S3 と同じく結果不明として読む。読み取りなので送り直してよい。

### 2.2 不変条件と成立条件

**不変条件（起きてはいけないこと）**

- **I1 読み取りは状態を変えない。** `osm.scenes` を何回送っても、SceneDirector の管理下の Scene の集合と各 `SceneState`・pending の unload・進行中の操作・履歴、Unity の読み込み済みの Scene の集合、hierarchy、AssetManagement の保持数が、送らなかった場合と同じ。`OnSceneEvent` に event が出ない。
  - 主張を崩しうる既存の経路: (a) `SceneBase` の property の getter が副作用を持つ（`Lifecycle` は internal の field の読み取り。他の property は未確認）。(b) `SceneResource` の `Children` などが lazy に解決して asset を読む（`SceneResource.cs:61〜79`。A1 は serialized field の読み取りと見たが、`LoadType` が `SceneAssetDescription` から取られる箇所は未確認）。(c) `_inFlight*` の `UniTaskCompletionSource` を読む時に continuation を走らせる method を呼ぶ（読むのは辞書の key だけにする）。(d) JSON 化で `UnityEngine.Object` の `name` を読む時に破棄済みの object に触る（`== null` で判定する）。
  - 探索した境界: `SceneDirector*.cs`・`SceneLifecycleManager.cs`・`SceneResource.cs` の宣言部。`SceneBase` の全 property の getter と `SceneAssetDescription` は未確認（A2 で確かめる）。
- **I2 観測が寿命を延ばさない。** `ReleaseAll` の後（Play の停止の後）に、catalog と handler のどれも SceneDirector・`SceneBase`・`ScenePair` を参照していない。停止の後の次の Play で読んだ値に前の Play の Scene が出ない。
  - 崩しうる経路: (a) 登録の時に SceneDirector を closure に捕まえる（§2.1 の 1 で禁止）。(b) 写しを cache して次の要求で返す（cache を持たない）。(c) Domain Reload が無効なので static に置いた物が Play をまたいで残る（static の状態を持たない）。
- **I3 一度の読み取りの値は一つの時点の値である。** 1 回の応答の中の Scene の集合・各 `SceneState`・履歴は、main thread の同じ時点（await を挟まない一続きの読み取り）から取る。
  - 崩しうる経路: 写しの途中で await する。catalog は handler を同期で呼ぶので、handler の中に await が無ければ保たれる。
- **I4 S4a までの契約を変えない。** `osm.commands` の一覧は `osm.scenes` が増えるだけで、他の項目と組込みの欄は変わらない。`debug.set-active` の挙動、Player の構成で書き込みが無いことも変えない。

**成立条件（起きなければならないこと）**

- **E1** 通常の SampleGame の Editor の Play（Title から起動）で、`hasSnapshot: true` になった後に `send --command osm.scenes --payload '{}'` が exit 0 で、出力の Scene の集合と各 `SceneState` が、同じ frame の近くに main thread で `ContainsScene` / `GetSceneState` で読んだ値と一致する。
- **E2** SampleGame の操作で 1 回 Scene を切り替えた（遷移の後に Stable になった）後に読むと、切り替え先の Scene が `Stable`、切り替え元が管理下に無いか unload 系の状態、`history` の先頭が今の遷移の `{from, to}`、`canGoBack` が true。
- **E3** `osm.commands` の一覧に `osm.scenes` が宣言どおり（`Read`、`EditorOnly`）に出る。SceneDirector の無い時点の読み取りは `unavailable: scene-director-not-created`（exit 1）。

### 2.3 受け入れ条件（A1 初稿）

| AC | 検証する条件 | 内容 | 方法 |
|---|---|---|---|
| AC1 | E1・I3 | fake の factory で 3 段の木（root → 子 → 孫）を AddScene した SceneDirector の `CaptureDebugSnapshot` が、各 node の `state`・`parent`・`children`・`loadType` を正しく写す | EditMode 単体（既存 `SceneDirectorTestBase` の上） |
| AC2 | I3・U1 | AddScene の await の途中（取消の窓の中）で写すと、対象が `cancelWindowOpen: true` と `inFlight` を持ち、窓を過ぎた後は `false` | EditMode 単体 |
| AC3 | E2 | `SwitchScene` の後の写しの `history` の先頭と `canGoBack` | EditMode 単体 |
| AC4 | I1 | 写しの前後で `_currentScenes` の key 集合・各 `SceneState`・`_pendingUnloads`・`SceneHistoryCount`・`OnSceneEvent` の受信数が同じ。写しを 100 回続けても同じ | EditMode 単体 |
| AC5 | I1（失敗経路） | 写しの途中で `SceneBase` の GameObject が破棄済みでも例外にならず、その node が読める | EditMode 単体 |
| AC6 | E3・I2（失敗経路） | handler に渡した delegate が null を返す（SceneDirector が無い）と `unavailable: scene-director-not-created`。JSON の escape | EditMode 単体 |
| AC7 | I2・I4 | Play を模した 2 回の初期化で、2 回目の写しに 1 回目の Scene が出ない。`osm.commands` に `osm.scenes` が出る | EditMode（既存 Bootstrap の test の流儀） |
| AC8 | E1・E2・E3・I4 | 実 Editor の Play と Release の WPF・CLI で §5 の V1〜V5 | 実機（Phase C） |

### 2.4 判定定義

- GO: I1〜I4 と E1〜E3 を同じ GO 候補 head で満たし、判定必須テスト（§5）が全て通る。
- NO-GO: 上のどれかが満たせない。

### 2.5 ここでは答えない問いと所有する後続スライス

- assets（AssetManagement の所有者ごとの保持。既存の `IAssetDiagnostics` と internal の `AssetRegistry.GetAllLoadedAssets` が材料）、update（UpdateSystem の layer と登録数。`UpdateCoordinator` に列挙の口が無い）のコマンド: DebugStudio S5 以降（未起票）。同じ口に `osm.assets` / `osm.update` として載せる。
- WPF の表示と、専用の CLI サブコマンド・protocol の message 種別: DebugStudio S5 以降（未起票）。
- Player での読み取り: DebugStudio S5 以降（未起票）。program の S4 系の Phase D の後に owner が時期を決める。
- 要求時解決の記録（`ContentRecord`）を出すこと: program の S5（`EDITOR_WORKFLOW_AND_OBSERVABILITY`、policy と record の観測 UI）。同じ口にコマンドとして足せるかは S5 の A1 が判断する。
- hierarchy に DontDestroyOnLoad を出すこと、Play をまたぐ NodeId の識別、inspect の自己応答保証: S3 から移送された項目のまま、DebugStudio S5 以降（未起票）。
- 状態の変化の購読（push）: 未所有。要る時に owner が置く。

### 2.6 risk の理由

`normal`。公開 API・asmdef・`SceneState`・Addressables・寿命の契約を変えない。SceneDirector には internal の読み取り method を 1 つ足すだけで、挙動は変えない。`AbstractApplicationInitializer.cs` を 1 行触るので、program の停止規則（§1.4）により `RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` の Phase B と同時に B を走らせない。S4-V（`tools/` だけ）・S4-3a（主に `tools/` と文書）とは触る file が重ならない見込みで、Unity の実行の時間だけ分ける。

- A3 後の例外承認: なし
- 決定記録: なし

## 3. 責務マップ（A1 初稿）

| file | 責務 | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|---|
| `Runtime/SceneSystem/SceneDirector.DebugSnapshot.cs`（新規 partial） | SceneDirector の private な状態を一つの時点の不変の data に写す | Scene の状態の持ち方が変わった時 | SceneDirector（initializer が所有、App の Play 1 回） | SceneSystem の内部だけ | internal | EditMode 単体（Unity の Scene の load は fake） | 0 → 約 90 |
| `Runtime/SceneSystem/Diagnostics/SceneDirectorDebugSnapshot.cs`（新規） | 写しの data の型（node・history） | 写す項目が変わった時 | 値（寿命を持たない） | なし | internal | 上と同じ | 0 → 約 70 |
| `Runtime/SceneSystem/Diagnostics/ScenesDebugCommand.cs`（新規） | `osm.scenes` の handler、読めない時の文言、写しの JSON 化 | 出力の形・文言が変わった時 | catalog（App の Play 1 回） | SceneSystem、DebugCommands | internal | EditMode 単体（delegate を差し替える） | 0 → 約 130 |
| `Runtime/Bootstrap/AbstractApplicationInitializer.cs` | Framework の登録に `osm.scenes` を 1 行足す | 配線だけ | 既存 | 既存 | 変えない | 既存の Bootstrap の EditMode | S4a 後 + 約 3 |
| `tools/DebugStudio/src/DebugStudio.Cli/README.md`、`05-scene.md` §5.12 | harvest（Phase B が書く） | — | — | — | 文書 | — | 追記 |

- 行数警報: `AbstractApplicationInitializer.cs` は既に 500 行を超える。非分割の理由は S4a と同じ（配線の 1 行だけ）。
- 配置の理由: 写しは SceneDirector の private field を読むので SceneDirector の partial に置く（internal の accessor を増やして外から組み立てるより公開面が小さい）。写しの型とコマンドは Scene の診断という別の変更理由を持ち、SceneDirector の生存の規則（lifecycle）と混ぜないため `SceneSystem/Diagnostics/` に分ける。`DebugCommands/` は System だけの決まりがあるので置かない。`DebugSocketServices/` は transport と protocol の責務なので Scene の語彙を入れない。
- 新規 file の path は予算（repo 相対 150 文字）の範囲。最長は `Runtime/SceneSystem/Diagnostics/SceneDirectorDebugSnapshot.cs.meta`（repo 相対で 100 文字）。

## 4. 実装計画（A1 初稿）

- 順序: (1) 写しの型と `CaptureDebugSnapshot` と AC1〜AC5。(2) `ScenesDebugCommand` と AC6。(3) initializer の登録と AC7。(4) README と §5.12。
- Phase B から Phase A へ差し戻す条件: `SceneBase` の getter や `SceneResource` の読み取りに副作用があって I1 を守るには SceneDirector の挙動を変える必要がある。S4a の口で `RegisterFramework` が使えない。asmdef の参照が要る。public の API が要る。
- 対象外を維持する方法: protocol の YAML・`tools/DebugStudio/src` の code・SampleGame の code と asset を変えない（README だけ）。`ContentRecord` を写しに入れない。コマンドを `osm.scenes` の 1 つだけにする。

## 5. テストとレビュー計画（A1 初稿）

- 単体テスト: AC1〜AC7。新しい test の class は `unity/Assets/OneStarMaker/Tests/Scene/`（写しとコマンド）に置く。
- 差し戻し中の起点 `-Filter`: `SceneDirector|ScenesDebugCommand|DebugCommand|FrameworkServiceSet|BeforeSceneLoadServiceStaging`（C が根拠付きで変えてよい）。
- 判定必須テスト: 上の新規 test、既存の `SceneDirector*Tests`、`DebugCommandCatalogTests`、`SetActiveDebugCommandTests`、`DebugSocket*Tests`、`DebugCommandOfflineTests`、最終の全 EditMode 回帰。
- 全 EditMode 回帰の適用除外: なし。
- `tools/DebugStudio` の .NET テスト: code を変えないので判定必須にしない（README だけ）。
- 操作・実行時の条件の検証経路（AC8。実行担当は判定 C 用に開いた session。S3 の §5.4 と同じ環境の作り方）:
  - V0: clean な検証用 checkout を GO 候補 head に detach し、その `unity/` を自分で開いた Editor 6000.6.9f1。DebugStudio の App・CLI を Release で build し、App を `--listen` で起動して PID を記録する。
  - V1: SampleGame の Title から Play を始め、`hierarchy` が `hasSnapshot: true` になるまで読み直す（上限 60 秒）。`send --command osm.scenes --payload '{}'` の stdout / stderr / exit を記録し、同じ frame の近くで eval により `ContainsScene` / `GetSceneState` を読んで突き合わせる（E1）。eval は読むだけで、何も作らない・書かない。
  - V2: 同じ読み取りを 5 回続け、各回の前後で eval の読み取りと `hierarchy` が変わらないことを記録する（I1）。
  - V3: SampleGame の操作で 1 回 Scene を切り替える（具体の操作と切り替え先は A3 までに SampleGame の Title の画面の操作から決める。未決）。Stable の後に読んで `history` と `canGoBack` を記録する（E2）。
  - V4: `send --command osm.commands --payload '{}'` で `osm.scenes` の宣言を記録する（E3）。Play を止めた後に `osm.scenes` を送って、結果不明（接続の失敗）になることを記録する。
  - V5: `send --command debug.set-active` を S3 の手順で 1 往復し、従来どおり動くことを記録する（I4）。Play を止め、WPF に通常の終了を要求し、Scene が clean か、tracked の変更が無いかを記録する。
  - 保存: 各段のコマンド、開始・終了の UTC、stdout / stderr / exit、eval の戻り値。Git に入れず、[レビュー証拠](../../.agents/skills/osm-workflow/references/review-evidence.md#保存先とリモートへの配布) に従って保存する。
- 未知の操作経路の疎通結果: なし（§1.6）。V3 の Scene の切り替えの操作だけは A3 までに決める。
- 人間の判断が必要な条件: なし。
- 高コスト証拠: なし。
- 判定 C の照合 script の方式: なし（V1〜V5 は出力と eval の読み取りの目視の照合で足りる見込み。A2 が要ると判断したら A3 で決める）。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。
- A0/A1 主担当: Cloud の計画 session（モデル名は書かない）。
- C' 用に予約した担当: 未定（A2 の割当と同時に決める。A0/A1 の系列を C' の候補から外す）。

### 5.1 A2 の独立レビューと A3 の採否

未実施。

## 6. Phase B 実装結果

未到達。

## 7. Phase C

未到達。

## 8. Phase C'

未到達。

## 9. Phase D

未到達。
