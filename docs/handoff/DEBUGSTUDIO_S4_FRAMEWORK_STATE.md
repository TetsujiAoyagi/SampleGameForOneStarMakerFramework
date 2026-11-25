# DebugStudio S4 — 非 MonoBehaviour の Framework の状態を CLI から読む（最初の対象は Scene）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `A`（A0 と A1 初稿。A2 の独立レビューは未実施、A3 は未凍結。**実装キューではない**）
- branch: `claude/magical-gauss-7rycuj`（Cloud の計画 session の branch。PR の base は `develop`。PR は未作成）
- implementation base commit: `07cc2c975b63cf5b668130a1c45490dd3e77c5f0`（`origin/develop` の head、2026-11-25。本書の実測は全てこの base）
- implementation head commit: `未固定`
- A3 凍結記録: `未凍結`
- risk: `normal`（理由は §2.6）
- owner: root（依頼者）
- created: 2026-11-25
- expires: 2026-12-31、または A3 の凍結で置き換わった時点。期限時は owner が更新・廃止を判断する
- harvest to: `tools/DebugStudio/src/DebugStudio.Cli/README.md`（`osm.state` の送り方と出力の読み方）、`unity/Assets/Docs/Architecture/12-telemetry.md` §10.5（Framework が持つ読み取り専用のコマンドと dispatcher の合成の契約）、`unity/Assets/Docs/Architecture/05-scene.md` §5.12（外から読める Scene の状態と、観測が寿命と状態を変えないこと。あわせて §5.12 の `SceneEvent` の欄の記述が code に遅れている点を直す）
- Phase A snapshot の取得案内・状態: 未生成（A3 で凍結版を commit し、その SHA を tree の外で渡す）
- Phase B result snapshot / evidence bundle / C' blind bundle の取得案内・状態: 未生成
- task: 未開始（`tools/workflow-task.ps1 start` は凍結の時に行う）
- 引継ぎ票:
  - 次の Phase・役割: Phase A の主担当（owner の確認 → A2 → A3）。
  - 次の作業: (1) owner が §1.9 の未決事項 U1〜U4 を確認する。(2) 確認後に A1 を確定し、A2 の独立レビュー（目的レビュー 1 件、アーキテクチャゲート 1 件）を同じ入力版で行う。(3) A3 で採否を記録し、Draft PR で外部計画レビューを受けてから凍結する。
  - 読む範囲: 既定（[SKILL.md](../../.agents/skills/osm-workflow/SKILL.md#作業ごとの読み先) の表。Phase A は全節）。
  - 必要な権限: なし（文書のみ）。A0 確認（§1.6）を行う場合は、Windows の Editor 6000.6.9f1 と DebugStudio の Release build。
  - 停止規則: owner の U1 の確認の前に A2 を始めない。凍結の記録が無いまま Phase B を始めない。
  - 引き渡さないもの: なし。

### 実測の方法

- base `07cc2c97` の code と文書を `grep` と `wc -l` と行範囲の読み取りで確かめた。Unity も DebugStudio も起動していない。
- 本書の作成環境（Cloud の Linux container）に `pwsh`・`dotnet`・Unity が無く、`tools/docs-audit.ps1` は未実行。link 先の存在は `git ls-files` で手で照合した。PR の作成前に owner 側で通す。

## 1. 目的と対象外

### 1.1 目的と利用者（A0 で書いた。A2 の目的レビューへ渡す原文の候補）

- 目的: Editor の通常の Play 中に、GameObject を持たない Framework の状態を、表示のための GameObject や MonoBehaviour を作らずに、DebugStudio の CLI からその時点の値として読めるようにする。最初に読めるようにするのは SceneDirector が管理する Scene の一覧と各 Scene の `SceneState`、遷移の履歴である。
- 利用者: SampleGame を Editor で Play し、DebugStudio WPF を起動したまま CLI のコマンドを実行してその出力を読む開発者と AI エージェント（検証の driver を書く担当と、判定 C の実行担当を含む）。
- 目的・利用者の文言の出どころ: owner の 2026-11-25 の指示。「unity の MonoBehaviour でないクラスはデバッグ表示用の GameObject か MonoBehaviour 作ろうと思ってたが、まず DebugStudio で表示するのが素直だな」。あわせて、表示先は CLI の JSON だけ、実行形態は Editor の Play だけ、と owner が選んだ（2026-11-25 の質問への回答）。

### 1.2 対象外

- WPF の表示（専用の tool window・store・GUI の変更）。protocol・wire の変更（YAML・codegen・新しい message 種別・capability bit）。CLI の新しいサブコマンド。既存の `send` に載せる（§2.1）。
- Player（Build した実行ファイル）と遠隔接続。Player では新しいコマンドを登録しない。
- 書き込み。状態を変える操作（Scene の load・unload・遷移、`SceneState` の変更、履歴の変更）を DebugStudio から行うこと。
- Scene 以外の状態の読み取り（AssetManagement の所有者ごとの保持、UpdateSystem の layer と登録、Camera・Sound・VFX 等）。読み取りの口は後から足せる形にするが、このスライスでは Scene だけを載せる（U1）。
- 状態の変化の購読・履歴の蓄積・差分の配信。読むのは要求した時点の値だけ。変化の流れは既存の telemetry（`SceneLoad` / `SceneUnload` / `SceneTransition` の span）が担う。
- 要求時解決の記録（`ScenePair.ContentRecord`、`GetContentLoadRecord`）の中身。program [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md) の S4 系が形を変えている最中なので、このスライスでは出さない（§2.5）。
- hierarchy に DontDestroyOnLoad の GameObject を出すこと（S3 から移送された項目。別スライス。§2.5）。
- `SceneState` の値・順序の変更、`SceneLifecycleManager` の遷移規則の変更、SceneDirector の挙動の変更。
- S2 HANDOFF（`DEBUGSTUDIO_S2_SIMPLE_A3_CANDIDATE.md`）の Phase D。

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
- BeforeSceneLoad で `AssetManagement`（:270）・`UpdateSystemHost`（:291）・DebugSocket（:299〜302、`CreateDebugSocketService` → `new DebugSocketService(options, CreateDebugCommandDispatcher())`）を作る。
- AfterSceneLoad で `SceneDirector` を作り（:439）、`UpdateSystemHost.BindSceneDirector`（:450）と `Vfx.BindSceneLifecycle`（:451）で後から渡す。App 寿命の service へ SceneDirector を後から渡す先例はこの 2 つ。
- `ReleaseAll` は services（DebugSocket を含む）を Dispose してから（:1097）、`_sceneDirector` を Dispose して null にする（:1108〜1109）。initializer には `protected SceneDirector? SceneDirector`（:128）がある。
- Enter Play Mode Options が有効で Domain Reload が無効。Play ごとに service と SceneDirector を作り直す（S3 の HANDOFF の実測）。

**DebugSocket のコマンド経路:**

- `DebugSocketService`・`DebugSocketServices/*`・`DebugCommands/*`・`SceneSystem/*`・`Bootstrap/*` は全て `OneStarMaker.Runtime.asmdef` にある。SceneSystem を読むのに asmdef の参照の追加は要らず、循環も生じない。`DebugSocketServices` は今 SceneSystem を `using` していない。
- 経路: CLI の `send` → WPF の control（`ws://127.0.0.1:5012/cli-control/`）→ Runtime の router。router は組込みの `ping` / `runtime-diagnostics` を受信の経路で（main thread に切り替えずに）処理し、それ以外を `MainThreadDebugCommandDispatcher` 経由で App の dispatcher に main thread で渡す。結果は `CommandResult`（Success・Message・PayloadJson）。
- App の dispatcher は `AbstractApplicationInitializer.CreateDebugCommandDispatcher()`（protected virtual、既定は `NullDebugCommandDispatcher`）が作る。SampleGame の `AppInitializer` は override して自分の `DebugCommandCatalog` を作り、Editor のときだけ `debug.set-active` を登録する（`AppInitializer.cs:231〜240`）。**Framework 自身が catalog にコマンドを登録する口は無い。** Game が override すると、Framework が base で何かを登録しても Game の catalog に置き換わる。
- catalog の契約（[12-telemetry.md](../../unity/Assets/Docs/Architecture/12-telemetry.md) §10.5）: 登録は初期化時だけ、名前は Ordinal の完全一致、`TryExecute` は呼出し側の thread で同期実行、closure は App 寿命の依存だけを持ち Scene 所有の object の強参照の登録は非対応、Framework は書き込み口を持たない。
- CLI の `send` は `CommandResult` を受けたら `CommandResult request=… success=… message=…` と payload の JSON を、success=true なら stdout、false なら stderr へ出す（exit 0 / 1）。期限切れは exit 2。

**今 DebugStudio に届く非 GameObject の情報:**

- telemetry の event の流れ（`AppStartup`・`SceneLoad`・`SceneUnload`・`SceneTransition` などの span）。接続中だけ届き、問い合わせはできない。
- 問い合わせて今の値を返すのは `runtime-diagnostics`（DebugSocket の transport 自身の状態）だけ。hierarchy と inspect は GameObject だけ。SceneDirector の Scene の一覧や `SceneState` を運ぶ message・command・store は無い。
- Framework の状態を Inspector に出すための GameObject・MonoBehaviour は今は無い（`[UpdaterHost]` などは機能の host）。Editor の `SceneGraph` 窓は SceneResource の asset を編集する道具で、実行中の SceneDirector を読まない。

### 1.4 常時契約と既知の制約（本文へ転記）

- 依存は Game → Framework の一方向。asmdef の参照と package を追加しない（このスライスは要らない）。
- `SceneState` の 14 値を減らさず並べ替えない。状態の変更は `SceneLifecycleManager` が所有する。出力に整数値を出さず名前で出す（§2.1）。
- Unity 側の C# で `record` を使わない。新規・編集する Unity 側の `.cs` の先頭に `#nullable enable`。
- 破棄されうる `UnityEngine.Object` は `== null` / `!= null` で判定する。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。
- `ZLogger*` 型を公開面へ出さない。Editor のコードを Runtime の asmdef に置かない。
- path は `tools/path-budget.json` の予算に従う。
- 参照 0 の API（例: `IAssetDiagnostics`）を削除の理由にしない。
- Unity テスト・Build は Phase C。Phase B は Editor での compile 確認まで。Unity 終了時の native stall は `tools/Harness/README.md` の停止規則に従う。
- S2 / S3 の契約（`--listen` の待受け、`send` / `hierarchy` / `inspect` の出力と exit、`debug.set-active` の挙動、Player では書き込みのコマンドを登録しないこと）を壊さない。
- 05-scene.md §5.12: `OnSceneEvent` の購読者は SceneDirector の挙動に影響してはならない。観測が状態を変えないという要求はこれと同じ向き。
- program の停止規則: Runtime を触るスライスの Phase B を `RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` の Phase B と同時に走らせない。本スライスは `AbstractApplicationInitializer.cs` の dispatcher の生成の箇所（:1428〜1444 付近）を触るので、これに当たる（§2.6）。
- 人間が開いた Editor を強制終了しない。共有の Editor を触らない。並行の S4-V・S4-3a の Unity の実行と時間を分ける。

### 1.5 原資料

- S3 の HANDOFF（Phase D で削除。本文は commit `d0e4c03c` の親の `docs/handoff/DEBUGSTUDIO_S3.md`）の §2.4「ここでは答えない問いと所有する後続スライス」: 汎用編集・別の操作コマンド、Play をまたぐ指定の識別、DontDestroyOnLoad の表示、inspect の自己応答保証を「DebugStudio S4 以降（未起票）」に送った。本スライスはそのどれも引き受けない（§2.5）。

### 1.6 A0 確認

未実施。読み取りの経路（CLI の `send` → WPF → Runtime の catalog → main thread → `CommandResult`）は S3 の判定 C（2026-11-23、develop `9de34cc2` 相当）で `debug.set-active` について成立している。本スライスは同じ経路に新しい名前のコマンドを足すだけなので、未知の操作経路は無いと見る（§5 の「未知の操作経路」）。A2 がこの見方を否定したら、A3 の前に S3 の §1.6 と同じ形の小さな確認を別 session で行う。

### 1.7 UC ごとの利用者の入力

| UC | 利用者が選ぶ・入れるもの | 機械が決めるもの | 誰がいつ | 目的からの正当化 |
|---|---|---|---|---|
| UC1 読める状態の一覧を見る | コマンド名 `osm.state`（README の固定値）、payload `{}` | 載っている source の名前の一覧（登録の時に決まる） | 実行時の操作 | 「Framework の状態を…読めるようにする」。何が読めるかを出力だけで知るため。source が 1 つの間は省いても目的は満たす（U3） |
| UC2 Scene の状態を読む | コマンド名 `osm.state`、payload `{"source":"scenes"}` | 出力の中身（§2.1 の 3）、読んだ時点の frame の番号 | 実行時の操作 | 「Scene の一覧と各 Scene の `SceneState`、遷移の履歴」 |
| UC3 読めないとき | 新しい入力は無い | `rejected: unknown-source` / `unavailable: …` の区別（§2.1 の 4） | 実行時の操作 | 「その時点の値として読める」。読めない時に古い値や空を正常と誤読させない |

A1 自己点検: 準備の操作も開発時の宣言も無い（SampleGame の Scene・設定 asset・config は変えない）。新しい入力はコマンド名と source の名前だけで、どちらも README の固定値。Game が自分の dispatcher を override していても利用者の入力は変わらない（§2.1 の 1）。

### 1.8 スライスの分割の検討（A1 の終わり）

- 契機: 「Framework がコマンドを持つ口（dispatcher の合成）」と「Scene の状態を写す口」は独立して変わる理由を持つ。
- 判断: 分けない。前者は後者が無いと受け入れ条件で確かめる対象が無く（空の口を GO にできない）、後者は前者が無いと利用者に届かない。どちらも同じ Editor の Play の 1 回の読み取りで判定でき、独立して失敗しうる未知は無い（経路は S3 で成立済み）。
- 分けたもの: Scene 以外の source、WPF の表示、Player は対象外にして §2.5 へ送った。

### 1.9 未決事項（owner の確認待ち）

- **U1 汎用度。** A1 は「小さな汎用の口 + 最初の source として scenes」で書いた。2026-11-25 の質問で owner の回答は無かった。選択肢: (a) 本書のとおり、名前付きの読み取り専用の source を 1 つのコマンドで引く口を作り、scenes を 1 件だけ載せる。(b) 汎用の口を作らず scenes を読むコマンド 1 本だけにする。(c) 最初から scenes・assets・update の 3 つを載せる。主担当の推奨は (a)。2 件目（assets / update）を足すときに口を作り直さずに済み、追加の code は source の登録の表 1 つと名前の検査だけで済む見込み。(b) を選ぶなら §2.1 の 2 と UC1 を落とす。
- **U2 コマンド名。** 案は `osm.state`。既存の名前は Framework の組込みが `debugsocket.*`、SampleGame が `debug.*`。Framework が catalog 経由で持つコマンドの接頭辞を `osm.` に予約する（Game が同じ接頭辞で登録したら初期化で失敗させる。§2.1 の 1）。別名を望むなら A3 までに決める。
- **U3 一覧の UC1 を残すか。** source が 1 つの間は不要とも言える。残すと出力だけで読める物が分かる。A1 は残した。
- **U4 進行中の操作と取消の窓を出すか。** 出すと「途中の状態が見える」ことを利用者が出力だけで区別できる（`LoadCanceled` などを誤読しない）。出さないと出力が小さい。A1 は出す案で書いた（§2.1 の 3）。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: SampleGame の通常の Editor の Play 1 回の中で、GameObject・MonoBehaviour・購読を作らずに、DebugStudio の既存の `send` だけで、SceneDirector が管理する Scene の一覧・各 Scene の `SceneState`・遷移の履歴をその時点の値として読め、読む前後で Framework と Game の状態が変わらず、Game が自分の dispatcher を持っていてもその Game のコマンドが従来どおり動くか。
- 次スライスへ進む最低条件: §2.2 の不変条件 I1〜I4 と成立条件 E1〜E3 を同じ GO 候補 head で満たす。

### 2.1 統合設計（A1 初稿。A2 の対象）

Framework が「読み取り専用のコマンド」を Game の dispatcher と並べて持てるようにし、その上に名前付きの状態の source を 1 つ載せる。DebugStudio（WPF・CLI・protocol）は変えない。

1. **dispatcher の合成（Framework、`Runtime/DebugCommands/`）**
   - `AbstractApplicationInitializer.CreateDebugSocketService` で、Game の `CreateDebugCommandDispatcher()` の結果と、Framework の catalog の dispatcher を合成した dispatcher を `DebugSocketService` へ渡す。新規 `FrameworkFirstDebugCommandDispatcher`（internal sealed、仮称）: コマンド名が `osm.` で始まれば Framework の catalog だけで解決し、それ以外は Game の dispatcher へそのまま渡す。Game の dispatcher の結果・例外・`MainThreadDebugCommandDispatcher` による main thread の切替えは変えない（合成は `MainThreadDebugCommandDispatcher` の内側に入る）。
   - Framework の catalog は initializer が BeforeSceneLoad で作り、登録は初期化の中だけで行う（§10.5 の契約のまま）。Editor のとき（`Application.isEditor`）だけ登録する。Player では Framework の catalog は空で、合成した dispatcher は `osm.` で始まる名前を「未登録」として固定の失敗で返す。それ以外の名前は Game の dispatcher に渡るので、Player の挙動は今と同じ。
   - Game が `osm.` で始まる名前を自分の catalog に登録した場合の扱いは、Game の catalog の中身を Framework が見られない（dispatcher の interface に列挙が無い）ので検出しない。Game の dispatcher には `osm.` の名前が届かなくなることを §10.5 と README に書く（U2）。
   - `CreateDebugCommandDispatcher` の signature と既定値は変えない。Game の override はそのまま動く。
2. **状態の source の表（Framework、`Runtime/DebugCommands/`）**
   - 新規 `FrameworkStateDebugCommand`（internal static、仮称）が `osm.state` を Framework の catalog に登録する。payload は `{}` または `{"source":"<name>"}`。source は名前 → 「その時点の値を JSON にして返す delegate」の表で、初期化の中で登録し、以後変えない。
   - delegate は App 寿命の依存だけを閉じ込める。scenes の delegate は initializer の `() => SceneDirector`（protected accessor）を実行の時に読む。SceneDirector そのものを登録の時に捕まえない（AfterSceneLoad の前で null、`ReleaseAll` の後で null になる。§10.5 の「Scene 所有の object の強参照の登録は非対応」と同じ考え）。
3. **Scene の写し（Framework、`Runtime/SceneSystem/`）**
   - 新規 partial `SceneDirector.DebugSnapshot.cs` に `internal SceneDirectorDebugSnapshot CaptureDebugSnapshot()` を置く。main thread で、await を挟まず一度に private field を読んで、不変の data に写す。field に書かない、event を出さない、`SceneLifecycleManager` を遷移させない、`SceneBase` の method を呼ばない（`Lifecycle.State` と `SceneResource` の読み取りだけ）。
   - 写す物（出力の JSON の形は A1 の案。A2 の対象）:
     - `capturedFrame`（`Time.frameCount`）。
     - `scenes`: `SceneResourceMap` の各 node について `id`・`parent`・`children`・`loadType`（名前）と、管理下なら `state`（`SceneState` の名前。整数は出さない）・`pendingUnload`・`cancelWindowOpen`（`LoadCts != null`）・`inFlight`（add / sceneBase / unityScene / unload のうち進行中の物の名前の列）・`backendSceneLoaded`。管理下でなければ `state` は `null`。
     - `unmappedScenes`: `_currentScenes` にあって map に無い id（Editor で開いていた Scene の再利用など）を同じ形で。
     - `history`: 新しい順の `{from, to}` の列と `canGoBack`。
   - `ContentRecord` は出さない（§1.2）。
4. **出力と結果の読み方（README に書く。CLI の動作は変えない）**
   - 成功: `success=true message=captured`、payload が上の JSON（exit 0、stdout）。`capturedFrame` の時点の値で、続けて読んだ時の値と同じとは限らない。進行中の操作がある時は途中の状態が出る（`inFlight` と `cancelWindowOpen` で区別できる）。
   - `success=false`、message が `rejected: ` で始まる（exit 1、stderr）: `invalid-payload`（解析できない、`source` が文字列でない）、`unknown-source`（登録に無い名前）。
   - `success=false`、message が `unavailable: ` で始まる（exit 1、stderr）: source はあるが今は読めない。scenes は `scene-director-not-created`（AfterSceneLoad の前・起動の失敗の後・`ReleaseAll` の後）。古い写しを返さない。
   - 未登録（Player、または Editor でない）: 既存の catalog の固定の失敗（`Debug command 'osm.state' is not registered.`）。
   - それ以外（handler の例外、期限切れ、`CommandResult` の行が無い終わり方）は S3 と同じく結果不明として読む。読み取りなので送り直してよい。

### 2.2 不変条件と成立条件

**不変条件（起きてはいけないこと）**

- **I1 読み取りは状態を変えない。** `osm.state` を何回送っても、SceneDirector の管理下の Scene の集合と各 `SceneState`・pending の unload・進行中の操作・履歴、Unity の読み込み済みの Scene の集合、hierarchy、AssetManagement の保持数が、送らなかった場合と同じ。`OnSceneEvent` に event が出ない。
  - 主張を崩しうる既存の経路: (a) `SceneBase` の property の getter が副作用を持つ（`SceneBase.cs` の `Lifecycle` は internal の field の読み取り。他の property は未確認）。(b) `SceneResource` の `Children` などが lazy に解決して asset を読む（`SceneResource.cs:61〜79`。A1 は serialized field の読み取りと見たが、`LoadType` が `SceneAssetDescription` から取られる箇所は未確認）。(c) `_inFlight*` の `UniTaskCompletionSource` の状態を読む時に continuation を走らせる method を呼ぶ（読むのは辞書の key だけにする）。(d) JSON 化で `UnityEngine.Object` の `name` を読む時に破棄済みの object に触る（`== null` で判定する）。(e) 合成した dispatcher が Game のコマンドを二度呼ぶ・呼ばない。
  - 探索した境界: `SceneDirector*.cs`・`SceneLifecycleManager.cs`・`SceneResource.cs` の宣言部と、`DebugCommands/*`・`MainThreadDebugCommandDispatcher`。`SceneBase` の全 property の getter と `SceneAssetDescription` は未確認（A2 で確かめる）。
- **I2 観測が寿命を延ばさない。** `ReleaseAll` の後（Play の停止の後）に、Framework の catalog・source の表・合成した dispatcher のどれも SceneDirector・`SceneBase`・`ScenePair` を参照していない。停止の後の次の Play で読んだ値に前の Play の Scene が出ない。
  - 崩しうる経路: (a) 登録の時に SceneDirector を closure に捕まえる（§2.1 の 2 で禁止）。(b) 写しを cache して次の要求で返す（cache を持たない）。(c) Domain Reload が無効なので static に置いた表が Play をまたいで残る（表は initializer の instance の field にし、`ReleaseAll` で null にする）。
- **I3 一度の読み取りの値は一つの時点の値である。** 1 回の応答の中の Scene の集合・各 `SceneState`・履歴は、main thread の同じ時点（await を挟まない一続きの読み取り）から取る。
  - 崩しうる経路: 写しの途中で await する、または source の delegate が複数 frame にまたがる。`MainThreadDebugCommandDispatcher` は handler を同期で呼ぶので、handler の中に await が無ければ保たれる。
- **I4 既存の契約を変えない。** S2 / S3 の `send` / `hierarchy` / `inspect` の出力と exit、`debug.set-active` の挙動（Editor で登録、Player で未登録）、組込みの `ping` / `runtime-diagnostics`、protocol と WPF。Player では `osm.` の名前以外の挙動が今と同じ。
  - 崩しうる経路: 合成した dispatcher が `MainThreadDebugCommandDispatcher` の外側に入って main thread の切替えを失う、Game の dispatcher の例外を握りつぶす、`CreateDebugCommandDispatcher` の呼び出しの回数・時点が変わる。

**成立条件（起きなければならないこと）**

- **E1** 通常の SampleGame の Editor の Play（Title から起動）で、`hasSnapshot: true` になった後に `send --command osm.state --payload '{"source":"scenes"}'` が exit 0 で、出力の Scene の集合と各 `SceneState` が、同じ frame に main thread で `ContainsScene` / `GetSceneState` で読んだ値と一致する。
- **E2** SampleGame の操作で 1 回 Scene を切り替えた（遷移の後に Stable になった）後に読むと、切り替え先の Scene が `Stable`、切り替え元が管理下に無いか `Unloaded` 系、`history` の先頭が今の遷移の `{from, to}`、`canGoBack` が true。
- **E3** `send --command osm.state --payload '{}'` が source の一覧に `scenes` を含み、`{"source":"nope"}` が `rejected: unknown-source`、SceneDirector の無い時点の読み取りが `unavailable: scene-director-not-created` で、どれも exit 1 / 0 が §2.1 の 4 のとおり。

### 2.3 受け入れ条件（A1 初稿）

| AC | 検証する条件 | 内容 | 方法 |
|---|---|---|---|
| AC1 | E1・I3 | fake の factory で 3 段の木（root → 子 → 孫）を AddScene した SceneDirector の `CaptureDebugSnapshot` が、各 node の `state`・`parent`・`children`・`loadType` を正しく写す | EditMode 単体（既存 `SceneDirectorTestBase` の上） |
| AC2 | I3・U4 | AddScene の await の途中（取消の窓の中）で写すと、対象が `cancelWindowOpen: true` と `inFlight` を持ち、窓を過ぎた後は `false` | EditMode 単体 |
| AC3 | E2 | `SwitchScene` の後の写しの `history` の先頭と `canGoBack` | EditMode 単体 |
| AC4 | I1 | 写しの前後で `_currentScenes` の key 集合・各 `SceneState`・`_pendingUnloads`・`SceneHistoryCount`・`OnSceneEvent` の受信数が同じ。写しを 100 回続けても同じ | EditMode 単体 |
| AC5 | I1（失敗経路） | 写しの途中で `SceneBase` の GameObject が破棄済みでも例外にならず、その node が読める（名前を読む field があれば `== null` で扱う） | EditMode 単体 |
| AC6 | I4・I1 | 合成した dispatcher: `osm.` の名前は Framework だけ、他の名前は Game の dispatcher だけを 1 回呼ぶ。Game の dispatcher の例外はそのまま伝わる。Framework の catalog が空（Player）なら `osm.state` は未登録の固定の失敗 | EditMode 単体 |
| AC7 | E3・I2（失敗経路） | source の delegate が null を返す（SceneDirector が無い）と `unavailable: scene-director-not-created`、未知の source は `rejected: unknown-source`、壊れた payload は `rejected: invalid-payload`。一覧は登録順の名前の列 | EditMode 単体 |
| AC8 | I2 | `ReleaseAll` の後に表・catalog・合成した dispatcher への参照が initializer から外れている。次の初期化で表を作り直す | EditMode（既存 `FrameworkServiceSetTests` / `BeforeSceneLoadServiceStagingTests` の流儀） |
| AC9 | E1・E2・E3・I4 | 実 Editor の Play と Release の WPF・CLI で §5 の V1〜V5 | 実機（Phase C） |

### 2.4 判定定義

- GO: I1〜I4 と E1〜E3 を同じ GO 候補 head で満たし、判定必須テスト（§5）が全て通る。
- NO-GO: 上のどれかが満たせない。

### 2.5 ここでは答えない問いと所有する後続スライス

- assets（AssetManagement の所有者ごとの保持。既存の `IAssetDiagnostics` と internal の `AssetRegistry.GetAllLoadedAssets` が材料）、update（UpdateSystem の layer と登録数。`UpdateCoordinator` に列挙の口が無い）の source: DebugStudio S5 以降（未起票）。U1 で (c) を選んだら本スライスに入る。
- WPF の表示（専用の tool window。保存済み layout の ContentId の集合が変わる影響を含む）と、専用の CLI サブコマンド・protocol の message 種別: DebugStudio S5 以降（未起票）。
- Player での読み取り: DebugStudio S5 以降（未起票）。起動の経路に触れるので、program [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md) の S4 系の Phase D の後に owner が時期を決める。
- 要求時解決の記録（`ContentRecord`）を出すこと: program の S5（`EDITOR_WORKFLOW_AND_OBSERVABILITY`、policy と record の観測 UI）。本スライスの口に source として足せるかは S5 の A1 が判断する。
- hierarchy に DontDestroyOnLoad を出すこと、Play をまたぐ NodeId の識別、inspect の自己応答保証: S3 から移送された項目のまま、DebugStudio S5 以降（未起票）。
- 状態の変化の購読（push）: 未所有。要る時に owner が置く。

### 2.6 risk の理由

`normal`。公開 API・asmdef・`SceneState`・Addressables・寿命の契約を変えない。SceneDirector には internal の読み取り method を 1 つ足すだけで、挙動は変えない。dispatcher の合成は既存の override の signature を保ったまま内側に入るが、Game のコマンドの経路を通るので I4 を不変条件に置いた。`AbstractApplicationInitializer.cs` を触るので、program の停止規則（§1.4）により `RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` の Phase B と同時に B を走らせない。S4-V（`tools/` だけ）・S4-3a（主に `tools/` と文書）とは触る file が重ならない見込みで、Unity の実行の時間だけ分ける。

- A3 後の例外承認: なし
- 決定記録: なし

## 3. 責務マップ（A1 初稿）

| file | 責務 | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|---|
| `Runtime/SceneSystem/SceneDirector.DebugSnapshot.cs`（新規 partial） | SceneDirector の private な状態を一つの時点の不変の data に写す | Scene の状態の持ち方が変わった時 | SceneDirector（initializer が所有、App の Play 1 回） | SceneSystem の内部だけ | internal | EditMode 単体（Unity の Scene の load は fake） | 0 → 約 90 |
| `Runtime/SceneSystem/SceneDirectorDebugSnapshot.cs`（新規） | 写しの data の型（node・history） | 写す項目が変わった時 | 値（寿命を持たない） | なし | internal | 上と同じ | 0 → 約 70 |
| `Runtime/DebugCommands/FrameworkStateDebugCommand.cs`（新規） | `osm.state` の payload の解析、source の表の引き、結果の文言、写しの JSON 化 | 読める source・出力の形・拒否の文言が変わった時 | initializer の instance（Play 1 回） | DebugCommands、SceneSystem の写しの型 | internal | EditMode 単体（delegate を差し替える） | 0 → 約 150 |
| `Runtime/DebugCommands/FrameworkFirstDebugCommandDispatcher.cs`（新規） | `osm.` の名前を Framework の catalog へ、他を Game の dispatcher へ振り分ける | 予約の接頭辞・振り分けの規則が変わった時 | 同上 | DebugCommands、wire DTO、UniTask | internal | EditMode 単体（fake の dispatcher） | 0 → 約 60 |
| `Runtime/Bootstrap/AbstractApplicationInitializer.cs` | Framework の catalog と source の表を作って登録し、合成した dispatcher を渡す。`ReleaseAll` で外す | 配線だけ | 既存 | 既存 | 変えない（protected の accessor は既存の `SceneDirector` を使う） | 既存の Bootstrap の EditMode | 1486 → 約 1515 |
| `tools/DebugStudio/src/DebugStudio.Cli/README.md` | `osm.state` の送り方と結果の読み方 | — | — | — | 文書 | — | 追記 |
| `unity/Assets/Docs/Architecture/12-telemetry.md` §10.5、`05-scene.md` §5.12 | harvest（Phase B が書く） | — | — | — | 文書 | — | 追記 |

- 行数警報: `AbstractApplicationInitializer.cs` は既に 500 行を超える。非分割の理由: 足すのは既存の service の生成の並びに沿った配線の数行で、新しい判断（振り分け・解析・写し）は全て上の新規 file に置く。initializer の分割は本スライスの問いではなく、program の `RUNTIME_REFACTOR_*` 系の所有。
- 配置の理由: 写しは SceneDirector の private field を読むので SceneDirector の partial に置く（internal の accessor を増やして外から組み立てるより公開面が小さい）。コマンドと振り分けは既存の catalog と同じ `Runtime/DebugCommands/` に置く。`DebugSocketServices/` は transport と protocol の責務なので、Scene の語彙を入れない。
- 新規 file の path は予算（repo 相対 150 文字）の範囲。最長は `FrameworkFirstDebugCommandDispatcher.cs.meta` の 100 文字。

## 4. 実装計画（A1 初稿）

- 順序: (1) 写しの型と `CaptureDebugSnapshot` と AC1〜AC5。(2) `FrameworkStateDebugCommand` と AC7。(3) 振り分けと AC6。(4) initializer の配線と AC8。(5) README と §10.5・§5.12。
- Phase B から Phase A へ差し戻す条件: `SceneBase` の getter や `SceneResource` の読み取りに副作用があって I1 を守るには SceneDirector の挙動を変える必要がある。`MainThreadDebugCommandDispatcher` の内側に合成を入れられず router を変える必要がある。asmdef の参照が要る。public の API が要る。
- 対象外を維持する方法: protocol の YAML・`tools/DebugStudio/src` の code・SampleGame の code と asset を変えない（README だけ）。`ContentRecord` を写しに入れない。source を scenes の 1 件だけにする。

## 5. テストとレビュー計画（A1 初稿）

- 単体テスト: AC1〜AC8。新しい test の class は `unity/Assets/OneStarMaker/Tests/Scene/`（写し）と `Tests/DebugCommands/`（コマンドと振り分け）に置く。
- 差し戻し中の起点 `-Filter`: `SceneDirector|DebugCommand|FrameworkServiceSet|BeforeSceneLoadServiceStaging|SetActiveDebugCommand`（C が根拠付きで変えてよい）。
- 判定必須テスト: 上の新規 test、既存の `SceneDirector*Tests`、`DebugCommandCatalogTests`、`SetActiveDebugCommandTests`、`DebugSocket*Tests`、最終の全 EditMode 回帰。
- 全 EditMode 回帰の適用除外: なし。
- `tools/DebugStudio` の .NET テスト: code を変えないので判定必須にしない（README だけ）。
- 操作・実行時の条件の検証経路（AC9。実行担当は判定 C 用に開いた session。S3 の §5.4 と同じ環境の作り方）:
  - V0: clean な検証用 checkout を GO 候補 head に detach し、その `unity/` を自分で開いた Editor 6000.6.9f1。DebugStudio の App・CLI を Release で build し、App を `--listen` で起動して PID を記録する。
  - V1: SampleGame の Title から Play を始め、`hierarchy` が `hasSnapshot: true` になるまで読み直す（上限 60 秒）。`send --command osm.state --payload '{"source":"scenes"}'` の stdout / stderr / exit を記録し、同じ frame の近くで eval により `ContainsScene` / `GetSceneState` を読んで突き合わせる（E1）。eval は読むだけで、何も作らない・書かない。
  - V2: 同じ読み取りを 5 回続け、各回の前後で eval の読み取りと `hierarchy` が変わらないことを記録する（I1）。
  - V3: SampleGame の操作で 1 回 Scene を切り替える（具体の操作と切り替え先は A3 までに SampleGame の Title の画面の操作から決める。未決）。Stable の後に読んで `history` と `canGoBack` を記録する（E2）。
  - V4: `{}`・`{"source":"nope"}`・壊れた payload を送る（E3）。Play を止めた後に送って、結果不明（接続の失敗）になることを記録する。
  - V5: `send --command debug.set-active` を S3 の手順で 1 往復し、従来どおり動くことを記録する（I4）。Play を止め、WPF に通常の終了を要求し、Scene が clean か、tracked の変更が無いかを記録する。
  - 保存: 各段のコマンド、開始・終了の UTC、stdout / stderr / exit、eval の戻り値。Git に入れず、[レビュー証拠](../../.agents/skills/osm-workflow/references/review-evidence.md#保存先とリモートへの配布) に従って保存する。
- 未知の操作経路の疎通結果: なし（§1.6。経路は S3 で成立済み）。V3 の Scene の切り替えの操作だけは A3 までに決める。
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
