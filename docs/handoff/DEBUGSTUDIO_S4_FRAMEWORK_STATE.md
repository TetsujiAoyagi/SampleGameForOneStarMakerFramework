# DebugStudio S4 — SceneDirector の Scene の状態を CLI から読む `osm.scenes`

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `A`（A1 初稿 revision 3。A2 は未実施、A3 は未凍結。**実装キューではない**）
- branch: `claude/magical-gauss-7rycuj`（Cloud の計画 session の branch。PR の base は `develop`。PR は未作成）
- implementation base commit: `07cc2c975b63cf5b668130a1c45490dd3e77c5f0`（`origin/develop` の head、2026-11-25。本書の実測は全てこの base）
- implementation head commit: `未固定`
- A3 凍結記録: `未凍結`
- risk: `normal`（理由は §2.6）
- owner: root（依頼者）
- created: 2026-11-25
- expires: 2026-12-31、または A3 の凍結で置き換わった時点。期限時は owner が更新・廃止を判断する
- harvest to: `unity/Assets/Docs/Architecture/05-scene.md` §5.12（外から読める Scene の状態、出力の意味、観測が状態と寿命を変えないこと。あわせて §5.12 の `SceneEvent` の欄の記述が code に遅れている点を直す）、`unity/Assets/Docs/Architecture/12-telemetry.md` §10.5（Framework が Editor でだけ持つ内部の catalog と振り分けの dispatcher）、`tools/DebugStudio/src/DebugStudio.Cli/README.md`（`osm.scenes` の送り方と出力の読み方）
- 関連: [DEBUG_COMMAND_VISION.md](DEBUG_COMMAND_VISION.md)（デバッグコマンドの基盤の構想。未承認。本スライスが作らない物と、将来の観測の形の置き場）
- Phase A snapshot の取得案内・状態: 未生成（A3 で凍結版を commit し、その SHA を tree の外で渡す）
- Phase B result snapshot / evidence bundle / C' blind bundle の取得案内・状態: 未生成
- task: 未開始（`tools/workflow-task.ps1 start` は凍結の時に行う）
- 引継ぎ票:
  - 次の Phase・役割: Phase A の主担当（owner の確認 → A2 → A3）。
  - 次の作業: (1) owner が §1.9 を確認する。(2) A1 を確定し、A2 の独立レビュー（目的レビュー 1 件、アーキテクチャゲート 1 件）を同じ入力版で行う。(3) A3 で採否を記録し、Draft PR で外部計画レビューを受けてから凍結する。
  - 読む範囲: 既定（[SKILL.md](../../.agents/skills/osm-workflow/SKILL.md#作業ごとの読み先) の表。Phase A は全節）。
  - 必要な権限: なし（文書のみ）。
  - 停止規則: owner の確認の前に A2 を始めない。凍結の記録が無いまま Phase B を始めない。
  - 引き渡さないもの: なし。

### 実測の方法

- base `07cc2c97` の code と文書を `grep`・`wc -l`・行範囲の読み取りで確かめた。Unity も .NET も起動していない。
- 本書の作成環境（Cloud の Linux container）に `pwsh`・`dotnet`・Unity が無く、`tools/docs-audit.ps1` は未実行。link 先の存在は `git ls-files` で手で照合した。

### 改訂の経緯

- revision 1（commit `f662d9ae`）: 状態の source の表と、Framework のコマンドを Game の dispatcher の前で振り分ける合成。
- 分割版（commit `1d1ad645`）: コマンドの口（宣言・一覧・予約の強制）を S4a、Scene の読み取りを S4b に分けた。
- revision 2（commit `b3f06b7f`）: 1 スライスに戻し、hook の置き換えを共通の口とし、デバッグコマンドの将来の契約を「今決める D1〜D9 / 後から足す L1〜L10」に分けた。
- revision 3（本書）: owner の補足（2026-11-25、§1.1）で目的を「Hierarchy と同じように Scene の存在・状態・寿命を観測する方向の最初の一歩」に戻し、基盤の整備を目的から外した。owner の判断「共通の口にしたのが無理をさせてるならやめてもいい」（2026-11-25）で共通の口（hook の置き換え）をやめ、revision 1 と同じ形の内部の振り分けに戻した。これにより、owner が 2026-11-25 に決めた「既存の `CreateDebugCommandDispatcher()` の override は新しい hook へ移して廃止」は取り消した。D1〜D9 / L1〜L10 の表は [DEBUG_COMMAND_VISION.md](DEBUG_COMMAND_VISION.md) へ移した。

## 1. 目的と対象外

### 1.1 目的と利用者（A2 の目的レビューへ渡す原文の候補）

- 目的: 既存の Hierarchy で GameObject の存在や状態を観測できるのと同様に、SceneDirector が管理する Scene の存在・状態・ライフサイクルを DebugStudio から観測できるようにする。その最初の一歩として、Editor の通常の Play 中に、SceneDirector が管理する Scene の一覧と各 Scene の `SceneState`、遷移の履歴を、表示のための GameObject や MonoBehaviour を作らずに、DebugStudio の CLI からその時点の値として読めるようにする。最初の一歩は、ライフサイクルの変化を観測する将来の形と矛盾しないこと。
- 利用者: SampleGame を Editor で Play し、DebugStudio WPF を起動したまま CLI のコマンドを実行してその出力を読む開発者と AI エージェント（検証の driver を書く担当と、判定 C の実行担当を含む）。
- 文言の出どころ（owner、2026-11-25）: 「既存の Hierarchy で GameObject の存在や状態を観測できるのと同様に、SceneDirector が管理する Scene の存在・状態・ライフサイクルを DebugStudio から観測できるようにしたい、というのが本来の目的である。デバッグコマンド基盤の整備自体が目的ではない」「最初のスライスは CLI から Scene の現在状態を読めるだけでも構わない。ただし、Scene の寿命を可視化するという本来の方向性と矛盾しないこと」。それ以前に owner が決めたこと: 表示先は CLI の JSON だけ、実行形態は Editor の Play だけ。

### 1.2 対象外

- デバッグコマンドの共通の登録の口、宣言・一覧・名前空間の強制、Player への公開、Scene 寿命のコマンド、read の出力の形の統一（[DEBUG_COMMAND_VISION.md](DEBUG_COMMAND_VISION.md) の構想。本スライスでは作らない）。
- ライフサイクルの変化の観測（`OnSceneEvent` の流れの配信、Hierarchy と同じ push と WPF の保持、時系列の表示）。本スライスは写しだけを作り、将来の形と矛盾しない条件（§1.6）だけを守る。
- WPF の表示、protocol・wire の変更、CLI の新しいサブコマンド。既存の `send` に載せる。
- Player（Build した実行ファイル）と遠隔接続。Player では何も変えない。
- 書き込み。Scene の load・unload・遷移、`SceneState` の変更、履歴の変更。
- `osm.scenes` の追加の項目（静的な木の parent / children / loadType、進行中の操作、取消の窓、要求時解決の記録）。足せる形にはする（§2.1 の 4）。
- Scene 以外の状態（AssetManagement、UpdateSystem など）。
- SampleGame の code と asset、`DebugCommandCatalog`、組込みの `ping` / `runtime-diagnostics` の変更。
- `SceneState` の値・順序、`SceneLifecycleManager` の遷移規則、SceneDirector の挙動（履歴の Push の時点を含む）。
- hierarchy に DontDestroyOnLoad を出すこと、Play をまたぐ NodeId の識別、inspect の自己応答保証（S3 から移送された項目）。

### 1.3 現況（base `07cc2c97`）

**Hierarchy の観測の仕組み（比較の基準）:**

- Runtime の `DebugSocketService` が `SceneManager` の読み込み済みの Scene を走査して GameObject の木を写し（`DebugSocketServices/Hierarchy/DebugSocketHierarchyPublisher.cs`、320 行）、接続の握手の後に全体の写しを、`sceneLoaded` などの Unity の callback の後に revision 付きの差分を WPF へ push する。
- WPF は受けた写しを `HierarchyStore` に保持し、CLI の `hierarchy` は Unity に問い合わせずに保持分を `status/reason/data` の JSON で返す（`hasSnapshot`、revision、capture の時刻、接続の状態を含む）。`inspect` は Unity へ問い合わせる。
- 正本は Unity の Scene と Transform で、Framework の外の構造。NodeId は service ごとの採番で Play をまたいで同じ番号が別の object を指しうる。

**Scene の状態（全て `Runtime/SceneSystem/`）:**

- 実行時の状態は `SceneDirector.cs`（336 行。ほかに partial の `.Loading.cs` 747・`.Unloading.cs` 478・`.Transitions.cs` 256・`.FailedLoadCleanup.cs` 349）の private field: `_currentScenes`（`Dictionary<string, ScenePair>`、:77。親子の link は無い）、`_pendingUnloads`（`HashSet<string>`、:78）、進行中の操作の 4 つの辞書（:79〜82）、`_sceneHistory`（`Stack<SceneHistoryEntry>`、:104。上限 8）。
- 各 Scene の `SceneState` は `SceneBase.Lifecycle`（`internal SceneLifecycleManager Lifecycle => _lifecycle;`、`SceneBase.cs:221`）の `State`（`public SceneState State => _state;`、`SceneLifecycleManager.cs:20`）。どちらも field を返すだけ。`SceneLifecycleManager` だけが `_state` を書く。
- 読み取りの口: 公開は `ISceneQuery`（`GetLoadedScene`・`IsSceneLoaded`・`IsSceneStable`・`GetContentLoadRecord`）、`CanGoBack`、`OnSceneEvent`。internal に「テスト用」の `ContainsScene`・`GetSceneState`・`HasPendingUnload`・`SceneHistoryCount`（:271〜283）。**全 Scene を列挙する口と履歴の中身を読む口は無い。**
- 履歴: `SwitchScene` は PoNR を過ぎた後、`UnloadScene` と `AddScene` の前に `_sceneHistory` へ Push する（`SceneDirector.Transitions.cs` の「履歴記録（PoNR 通過後に記録するため…）」の箇所）。**履歴は成功した遷移だけの記録ではなく、戻り先の管理用の stack である。** `GoBack` は遷移の成功の後にだけ Pop する。`CanGoBack` は `_sceneHistory.Count > 0`。
- 変更は全て main thread の UniTask の async method の中で起きる（thread pool への切替え・`lock` は無い）。frame の間に main thread で読むと thread の競合は無いが、操作の途中の状態が見える。
- `SceneState` は 14 値（`None`=0 … `LoadCanceled`=7, `Stable`=8 … `AfterUnloading`=13）。

**`OnSceneEvent`（ライフサイクルの変化の既存の通知）:**

- `SceneEvent`（readonly struct）は Type（`StateChanged` / `Added` / `Removed` / `CancelCleanedUp`）、SceneIdentify、State、ElapsedMs（直前の段の所要時間）、TimestampUtcTicks を持つ。R3 の `Subject`。05-scene.md §5.12 は購読者が SceneDirector の挙動に影響してはならないと定める。
- 状態の遷移のほとんどは `TransitionSceneState`（`SceneDirector.cs:326〜334`）が `Lifecycle.TransitionTo` と `StateChanged` を対で出す。`SceneBase` が自分で遷移する `PreLoading` / `PreLoaded` / `PreUnloading` / `PreUnloaded` は SceneDirector が前後で手で通知する（`Loading.cs:437〜441`、`Unloading.cs:234〜244`）。`PreLoading` と `PreUnloading` の通知は実際の遷移の直前に出る。`AfterUnloading` は単独の `StateChanged` を持たず、`Removed`（`Unloading.cs:294`）に載る。再開の経路と失敗の後始末の経路（`FailedLoadCleanup.cs`）で全ての遷移が通知されるかは未確認。
- event に通し番号は無い（Hierarchy の revision に当たる物が無い）。
- 別に telemetry の span（`SceneLoad`・`SceneUnload`・`SceneTransition`）が DebugStudio へ流れる（接続中だけ、問い合わせ不可）。

**コマンドの経路:**

- `AbstractApplicationInitializer.CreateDebugSocketService(AppConfig)`（`Runtime/Bootstrap/AbstractApplicationInitializer.cs:1428〜1437`、protected virtual、1486 行の file）は enabled が false なら null、true なら `new DebugSocketService(options, CreateDebugCommandDispatcher())`。`CreateDebugCommandDispatcher()`（:1443、protected virtual）の既定は `NullDebugCommandDispatcher.Instance`。**Framework が自分のコマンドを足す口は無い。** SampleGame の `AppInitializer`（`AppInitializer.cs:230〜240`）は override して自分の catalog に Editor のときだけ `debug.set-active` を登録する。
- `DebugSocketService` は受けた dispatcher を `MainThreadDebugCommandDispatcher` で包んで readonly の `_dispatcher` に持つ（`DebugSocketService.cs:129`、:151）。`Dispose()`（:340）は `_dispatcher` を手放さず、`StopAsync().Forget()`（:351）で停止を非同期に始める。**Dispose が返った時点で停止が完了しているとは限らず、旧 service から dispatcher への参照は残りうる。**
- `CatalogDebugCommandDispatcher`（`DebugSocketServices/`、public sealed、64 行）: command が null なら固定の失敗、事前に cancel 済みなら固定の失敗、それ以外は `catalog.TryExecute` の結果を envelope に写す（RequestId・Success・Message・PayloadJson）。handler の例外は伝播し、router が失敗の envelope にする。
- `DebugCommandCatalog.TryExecute` は名前が解決できたかを bool で返す（未登録なら false と固定の失敗の結果）。名前は Ordinal の完全一致。
- 未登録の文言: `NullDebugCommandDispatcher` は `Debug command '<名前>' is not registered.`、catalog は `Debug command is not registered.`。
- 組込みの `ping` / `debugsocket.ping` / `runtime-diagnostics` / `debugsocket.runtime-diagnostics` は router が大小文字を区別せずに先に処理する（`DebugSocketBuiltInCommandHandler.cs`）。
- CLI の `send` の出力は `CommandResult request=… success=… message=…` の行と payload の行で、`hierarchy` / `inspect` の `status/reason/data` の JSON とは形が違う。

**生成と寿命:**

- DI container は無い（手動 DI。[03-di.md](../../unity/Assets/Docs/Architecture/03-di.md)）。SampleGame は `static readonly AppInitializer s_instance` を 1 つ持ち、Play をまたいで同じ initializer を使う。
- BeforeSceneLoad で DebugSocket（と dispatcher）を作り、AfterSceneLoad で `SceneDirector` を作る（:439）。initializer には `protected SceneDirector? SceneDirector`（:128）がある。`ReleaseAll` は services を Dispose してから（:1097）、`_sceneDirector` を Dispose して null にする（:1108〜1109）。
- Enter Play Mode Options が有効で Domain Reload が無効。Play ごとに service と SceneDirector を作り直す。
- 全て `OneStarMaker.Runtime.asmdef` の中で、asmdef の参照の追加は要らない。`Runtime/DebugCommands/` は .NET の offline テスト（`tools/DebugCommandOfflineTests`）が Unity なしで compile する System だけの folder（本書は触らない）。

### 1.4 常時契約と既知の制約（本文へ転記）

- 依存は Game → Framework の一方向。asmdef の参照と package を追加しない。
- `SceneState` の 14 値を減らさず並べ替えない。状態の変更は `SceneLifecycleManager` が所有する。出力に整数値を出さず名前で出す。
- Unity 側の C# で `record` を使わない。新規・編集する Unity 側の `.cs` の先頭に `#nullable enable`。
- 破棄されうる `UnityEngine.Object` は `== null` / `!= null` で判定する。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。
- Editor のコードを Runtime の asmdef に置かない。
- path は `tools/path-budget.json` の予算（repo 相対 150 文字）に従う。
- Unity テスト・Build は Phase C。Phase B は Editor での compile 確認まで。Unity 終了時の native stall は `tools/Harness/README.md` の停止規則に従う。
- S2 / S3 の契約（`--listen` の待受け、`send` / `hierarchy` / `inspect` の出力と exit、`debug.set-active` の挙動、Player では書き込みのコマンドを登録しないこと）を壊さない。
- 05-scene.md §5.12: `OnSceneEvent` の購読者は SceneDirector の挙動に影響してはならない。本スライスは購読もしない。
- program の停止規則: Runtime を触るスライスの Phase B を `RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` の Phase B と同時に走らせない。本スライスは `AbstractApplicationInitializer.cs` の DebugSocket の生成の箇所（:1428〜1437）を触るので当たる。
- 人間が開いた Editor を強制終了しない。共有の Editor を触らない。並行の S4-V・S4-3a の Unity の実行と時間を分ける。

### 1.5 原資料と外部の意見の採否

- S3 の HANDOFF（commit `d0e4c03c` の親の `docs/handoff/DEBUGSTUDIO_S3.md`）: §2.1 の配線、§2.4 の後続へ送った項目。
- 外部の意見は 3 件で、どれも owner が別途依頼して要旨を本 session へ渡した。A2 に数えない。
  - 意見 X（2026-11-25、分割版 `1d1ad645` が対象）: 宣言・一覧は今は要らない、1 スライスに戻す、Player の未登録の文言が変わる不整合、escape の規則の食い違い。revision 2 で反映済み。
  - 意見 Y（2026-11-25、revision 2 `b3f06b7f` が対象。owner の補足とは別の設計レビュー）: P1-1〜P1-7、P2-1・P2-2。
- 意見 Y の採否（owner の補足「今回の目的に直接関係する指摘と、将来の基盤整備に関する指摘を分けて」に従う）:

| 指摘 | 分類 | 採否 | 反映先 |
|---|---|---|---|
| P1-6 出力の意味（履歴は戻り先の管理用の stack、途中・失敗でも entry がある、`canGoBack` は成功の保証ではない、`scenes` は管理集合） | 目的に直接 | 採用。履歴の Push の時点は code で確かめた | §1.3、§2.1 の 4 |
| P1-7 独立した正本での照合（集合の完全一致、安定と途中の分離、履歴の順と内容、空と未生成の区別、機械判定） | 目的に直接 | 採用 | §2.3 E1・E2、§5 |
| P1-4 寿命の主張の正確さ（Dispose は dispatcher を手放さない、`StopAsync().Forget()`、`() => SceneDirector` は initializer を捕まえる） | 目的に直接（読み取りの値が今の Play の物であること） | 採用。Dispose の挙動は code で確かめた。読み取りでは「旧 Play の値を出さない」を契約にし、旧 handler が書き込みを行う問題は構想へ送る | §1.3、§2.3 I2、§5 V5、構想 |
| P1-5 登録の失敗で起動が止まること | 共通の口をやめたので該当しない | 不要（名前の衝突は振り分けで Framework が優先し、例外を投げない） | §2.1 の 1 |
| P1-1 登録の機構と公開の方針の分離 | 将来の基盤 | 構想へ。本書では「Editor でだけ Framework の catalog を作る」を composition root の方針として書く | §2.1 の 2、構想 |
| P1-2 コマンドを足す開発者の体験 | 将来の基盤 | 構想へ。本書の配置（`SceneSystem/Diagnostics/`）を最初の例として書く | §3、構想 |
| P1-3 名前空間と組込みの大小文字 | 将来の基盤 | 構想へ。本書では README に組込みの名前が先に処理されることを書く | 構想 |
| P2-1 機械可読な入出力 | 一部が目的に直接 | `unavailable:` の接頭辞を本コマンドの暫定の出力とし、全体の契約にしない。基本契約は構想へ | §2.1 の 4、構想 |
| P2-2 発見のしやすさ | 将来の基盤 | 構想へ。本書では README に載せる | 構想 |
| 総評「将来の Framework と Game の開発者がコマンドを自然に足せる設計かを A2 の独立観点に」 | 将来の基盤 | 不採用。owner の補足「デバッグコマンド基盤の整備自体が目的ではない」を優先する | — |

### 1.6 観測の構図（Hierarchy との比較と将来の形）

| | Hierarchy（既存） | Scene の観測（本スライスと将来） |
|---|---|---|
| 正本 | Unity の Scene と Transform（Framework の外） | SceneDirector の private な field（列挙の口が無い） |
| 今の値 | 写し（接続時に全体） | 写し（本スライスは要求時に pull。`CaptureDebugSnapshot`） |
| 変化 | Unity の callback → revision 付きの差分を push | `OnSceneEvent`（既存。所要時間と時刻を持つ。通し番号は無い）。本スライスは使わない |
| 保持と読み | WPF が保持、CLI は保持分を読む | 本スライスは保持しない（毎回 Runtime へ問い合わせる） |
| 時間軸 | 無い | ある（14 状態の列と各段の所要時間）。寿命の可視化の本体 |

- 写しは「今どう在るか」（管理集合、各状態、unload の待ち、履歴）、event は「どう変わったか・どれだけかかったか」を担う。寿命の可視化の主役は event の時系列で、写しは途中から見始めた時の初期値になる。将来の形は Hierarchy と同じ「接続時の写し + 以後の event」。
- 本スライスが将来の形と矛盾しないための条件:
  - **C1** 写しのモデル（`SceneDirectorDebugSnapshot`）は SceneSystem に置き、コマンドと JSON から独立させる。将来 push の message にする時は同じモデルを使う。
  - **C2** 写しの語彙を `SceneEvent` と揃える（Scene の id は `SceneIdentify` と同じ文字列、状態は `SceneState` の名前）。event を写しに当てて更新できる。
  - **C3** 写しと event の前後を決める通し番号は今は作らず、後から写しの項目と `SceneEvent` の項目として足せる形にする（出力は項目を足すだけ。§2.1 の 4）。
- 本スライスで分かった将来の課題（構想へ送る）: `PreLoading` / `PreUnloading` の通知が実際の遷移より前に出ること、`AfterUnloading` が `Removed` に載ること、再開と失敗の後始末の経路の通知が未確認であること。event だけで寿命を再構成する前に確かめる必要がある。

### 1.7 UC ごとの利用者の入力

| UC | 利用者が選ぶ・入れるもの | 機械が決めるもの | 誰がいつ | 目的からの正当化 |
|---|---|---|---|---|
| UC1 Scene の状態を読む | コマンド名 `osm.scenes`（README の固定値）、payload `{}` | 出力の中身（§2.1 の 4）、読んだ時点の frame の番号 | 実行時の操作 | 「Scene の一覧と各 Scene の `SceneState`、遷移の履歴を…読める」 |
| UC2 読めないとき | 新しい入力は無い | `unavailable: …` の区別 | 実行時の操作 | 「その時点の値として」。読めない時に古い値や空を正常と誤読させない |

A1 自己点検: 準備の操作も開発時の宣言も無い。SampleGame の code・Scene・設定 asset・config は変えない。Game の開発者の作業は増えない。

### 1.8 スライスの分割の検討（A1 の終わり）

- 契機: 「Framework がコマンドを持つための内部の振り分け」と「Scene の写しとコマンド」は独立して変わる理由を持つ。
- 判断: 分けない。振り分けは `osm.scenes` を届けるためだけの内部の部品で、単独では観測できる成立条件が無い。どちらも同じ Editor の Play の 1 回で判定でき、独立して失敗しうる未知は無い。

### 1.9 未決事項（owner の確認待ち）

- **U1 `osm.scenes` の出力の項目。** 案は id・`SceneState`・unload の待ち・履歴・`canGoBack`・`capturedFrame` だけ。静的な木、進行中の操作、取消の窓は後から足す（足しても既存の読み手は壊れない）。最初から欲しい物があれば A3 までに。
- **U2 Framework の名前が Game の名前を遮ること。** Editor では Framework の catalog が先に引かれるので、Game が `osm.scenes` と同じ名前を登録すると Editor では届かなくなる（今そういう Game のコマンドは無い）。README と §10.5 に書くだけにする案で書いた。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: SampleGame の通常の Editor の Play 1 回の中で、GameObject・MonoBehaviour・購読を作らずに、CLI の `send --command osm.scenes` で、SceneDirector が管理する Scene の集合・各 `SceneState`・遷移の履歴を、その時点の値として正本と完全に一致する形で読め、読む前後で状態が変わらず、それ以外のコマンドと Player の挙動が今と同じか。
- 次スライスへ進む最低条件: §2.3 の不変条件 I1〜I4 と成立条件 E1〜E4 を同じ GO 候補 head で満たす。

### 2.1 統合設計（A1 初稿 revision 3。A2 の対象）

1. **振り分けの dispatcher（新規 `Runtime/DebugSocketServices/FrameworkFirstDebugCommandDispatcher.cs`、internal sealed、仮称）**
   - constructor で Framework の `DebugCommandCatalog` と Game の `IDebugCommandDispatcher` を受ける。
   - `DispatchAsync`: command が null、または事前に cancel 済みなら、Game の dispatcher へそのまま渡す（今と同じ扱いになる）。それ以外は Framework の catalog の `TryExecute` で名前を引き、解決できたらその結果を envelope に写して返す（写し方は `CatalogDebugCommandDispatcher` と同じ。共有するか同じ規則を持つかは A2 の対象）。解決できなければ Game の dispatcher へ同じ command と token を渡し、その結果をそのまま返す。
   - handler の例外は伝播させる（router が今どおり失敗の envelope にする）。Game の dispatcher の結果・例外は変えない。
   - Scene の語彙を持たない。既存の dispatcher の仲間として `DebugSocketServices/` に置く。
2. **配線（`AbstractApplicationInitializer.CreateDebugSocketService`）**
   - enabled が false なら今どおり null。Player（`!Application.isEditor`）なら今どおり `new DebugSocketService(options, CreateDebugCommandDispatcher())`。
   - Editor なら、Framework の catalog を作って `osm.scenes` を登録し、`new FrameworkFirstDebugCommandDispatcher(frameworkCatalog, CreateDebugCommandDispatcher())` を渡す。`CreateDebugCommandDispatcher` の signature・既定値・呼ばれる回数と時点は変えない。
   - 「Editor でだけ Framework のコマンドを持つ」は composition root（initializer）の公開の方針であり、catalog の意味ではない。
   - `CreateDebugSocketService` を override した Game では Framework のコマンドは無い（§10.5 と README に書く）。
   - Editor / Player の判定は、テストで差し替えられる形にする（形は A2 の対象。公開 API を増やさない）。
3. **写し（`Runtime/SceneSystem/`）**
   - 新規 partial `SceneDirector.DebugSnapshot.cs` に `internal SceneDirectorDebugSnapshot CaptureDebugSnapshot()`。main thread で、await を挟まず一度に `_currentScenes` の key と各 `SceneBase.Lifecycle.State`、`_pendingUnloads`、`_sceneHistory`（列挙だけ）を読んで不変の data に写す。field に書かない、event を出さない、`SceneBase` の method を呼ばない、`SceneResource` を読まない。
   - 写しのモデル `SceneDirectorDebugSnapshot` は新規 `Runtime/SceneSystem/Diagnostics/` に置き、JSON とコマンドを知らない（§1.6 の C1）。
4. **`osm.scenes`（新規 `Runtime/SceneSystem/Diagnostics/ScenesDebugCommand.cs`、internal static）**
   - initializer が `() => SceneDirector` を渡し、handler が実行の時に読む。SceneDirector を登録の時に捕まえない。payload は読まない（README では `{}` を示す）。
   - 成功: `success=true message=captured`、payload は出力用の `[Serializable]` の DTO を `JsonUtility.ToJson` した JSON の object:
     - `capturedFrame`（`Time.frameCount`）。
     - `scenes`: `{id, state, pendingUnload}` の列。SceneDirector の管理集合（Unity の全 Scene ではない）。`state` は `SceneState` の名前。順は id の Ordinal 順。管理集合が空なら空の列。
     - `history`: 新しい順の `{from, to}` の列。SceneDirector の戻り先の管理用の stack で、遷移の途中や遷移の失敗の後にも entry がありうる。
     - `canGoBack`: SceneDirector の既存の property の値（履歴があるか）。`GoBack` が成功することの保証ではない。
   - SceneDirector が無い（AfterSceneLoad の前・起動の失敗の後・`ReleaseAll` の後）: `success=false message=unavailable: scene-director-not-created`。古い写しを返さない。この接頭辞は本コマンドの暫定の出力で、デバッグコマンド全体の契約ではない。
   - 出力の規則（このコマンドの物）: 項目は足すだけで、名前を変えない・消さない・意味を変えない。列挙値は名前で出す。将来の項目（静的な木、進行中の操作、取消の窓、event との通し番号）はこの規則で足す。
   - README に書く読み方: `capturedFrame` の時点の値で、続けて読んだ時の値と同じとは限らない。操作の途中の状態（`LoadCanceled` など）がそのまま出ることがある。組込みの名前は router が大小文字を区別せずに先に処理する。結果不明の読み方は S3 と同じで、読み取りなので送り直してよい。

### 2.2 寿命の整理（意見 Y の P1-4 への答え）

| 寿命 | 本スライスでの扱い |
|---|---|
| Framework の catalog の登録 | `CreateDebugSocketService` の中で作り、DebugSocketService の dispatcher を通してだけ参照される。initializer の field に持たない |
| 接続の session | 既存のまま。CLI の要求は今の service にだけ届く |
| App・Play | Play ごとに service と SceneDirector を作り直す（Domain Reload は無効、initializer は static の 1 つ） |
| handler が対象を解決する時点 | 実行の時に `() => SceneDirector` で今の initializer の SceneDirector を読む。登録の時には読まない |
| Dispose の後 | 旧 service の dispatcher への参照は残りうる（`Dispose` が手放さない）。旧 service で handler が動いた場合も、読むのは今の SceneDirector か null で、旧 Play の Scene を出さない |

本スライスの契約は「出力に旧 Play の Scene が出ない」（I2）。GC の到達不能性や、旧 service の handler が一切動かないことは保証しない。書き込みのコマンドで同じことが起きる問題（旧 Play の要求が新しい Play に当たる）は構想へ送る。

### 2.3 不変条件と成立条件

**不変条件（起きてはいけないこと）**

- **I1 読み取りは状態を変えない。** `osm.scenes` を何回送っても、SceneDirector の管理集合と各 `SceneState`・unload の待ち・進行中の操作・履歴、Unity の読み込み済みの Scene の集合、hierarchy、AssetManagement の保持数が、送らなかった場合と同じ。`OnSceneEvent` に event が出ない。
  - 主張を崩しうる既存の経路: (a) 読む getter が副作用を持つ（`SceneBase.Lifecycle` と `SceneLifecycleManager.State` は field を返すだけと確かめた。他の getter は読まない）。(b) `_sceneHistory` を変える method を呼ぶ（列挙だけにする）。(c) `JsonUtility` が DTO の getter を呼ぶ（DTO は field だけにする）。
  - 探索した境界: `SceneDirector.cs` の field の宣言、`SceneBase.cs:221`、`SceneLifecycleManager.cs:20`、`SceneDirector.Transitions.cs` の履歴の操作。
- **I2 出力に旧 Play の Scene が出ない。** Play を止めて次の Play を始めた後の読み取りの出力は、次の Play の SceneDirector の管理集合だけを含む。
  - 崩しうる経路: (a) 登録の時に SceneDirector を closure に捕まえる（禁止）。(b) 写しを cache する（持たない）。(c) static の状態を持つ（持たない）。(d) 旧 service の dispatcher が残る（§2.2。読むのは今の SceneDirector なので出力には旧 Play の Scene が出ない）。
- **I3 一度の読み取りの値は一つの時点の値である。** 1 回の応答の値は main thread の同じ時点（await を挟まない一続きの読み取り）から取る。
  - 崩しうる経路: 写しの途中で await する。catalog は handler を同期で呼ぶので、handler の中に await が無ければ保たれる。
- **I4 `osm.scenes` 以外の挙動を変えない。** (a) Player: Game の dispatcher をそのまま渡し、全てのコマンドの結果と文言が今と同じ。(b) Editor: `osm.scenes` 以外の名前は Game の dispatcher に今と同じ command と token で 1 回だけ渡り、結果・文言・例外が今と同じ（Game が override していない Editor の未登録の文言も Null の名前入りのまま）。(c) `debug.set-active`、組込みの `ping` / `runtime-diagnostics`、enabled=false の扱い。(d) `CreateDebugCommandDispatcher` が呼ばれる回数と時点。
  - 崩しうる経路: 振り分けが null の command や cancel 済みの command を自分で処理して文言が変わる（Game へ渡す）、Framework の catalog の未解決の結果を返してしまう、Player の判定を誤る。

**成立条件（起きなければならないこと）**

- **E1 安定した状態での完全一致。** 通常の SampleGame の Editor の Play（Title から起動）で、`hasSnapshot: true` になった後、Scene の操作をしていない間に、`send --command osm.scenes --payload '{}'` が exit 0 で、`scenes` の id の集合と各 `state`・`pendingUnload`、`history` の列が、同じ Play の main thread で正本（§5 の oracle）から読んだ値と完全に一致する（余計な Scene も欠けた Scene も無い）。
- **E2 遷移の後の履歴。** SampleGame の操作で Scene を 1 回切り替え、全ての管理下の Scene が `Stable` になった後に読むと、`history` の列（順と内容）と `canGoBack` が oracle と一致し、先頭が今の遷移の `{from, to}`。
- **E3 再 Play。** Play を止めて同じ Editor で再び Play し、E1 と同じ読み取りをすると、出力が 2 回目の Play の oracle と完全に一致し、1 回目の Play だけにあった Scene が出ない（1 回目と 2 回目で管理集合が同じになる場面なら、履歴の内容で区別する。場面は A3 までに決める）。
- **E4 既存のコマンド。** 同じ Play で S3 の手順（`hierarchy` → `debug.set-active` false → `inspect` → true → `inspect`）が S3 と同じ出力・exit で動く。

### 2.4 受け入れ条件（A1 初稿）

| AC | 検証する条件 | 内容 | 方法 |
|---|---|---|---|
| AC1 | E1・I3 | fake の factory で親子の Scene を AddScene した SceneDirector の写しが、管理集合と各 `state`・`pendingUnload` を完全に写し、順が id の Ordinal 順。管理集合が空の SceneDirector では空の列 | EditMode 単体（既存 `SceneDirectorTestBase` の上） |
| AC2 | E2 | `SwitchScene` を 2 回した後の写しの `history` の列の順と内容、`canGoBack`。PoNR の後で遷移が失敗した場合も entry が残ること（今の挙動の記録） | EditMode 単体 |
| AC3 | I1 | 写しの前後で管理集合・各 `SceneState`・unload の待ち・`SceneHistoryCount`・`OnSceneEvent` の受信数が同じ。写しを 100 回続けても同じ | EditMode 単体 |
| AC4 | I3・I1（失敗経路） | AddScene の await の途中（途中の状態や `LoadCanceled` を含む）で写しても例外にならず、途中の状態の名前がそのまま出て、写しの後も操作が完了する | EditMode 単体 |
| AC5 | I2（失敗経路） | handler に渡した delegate が null を返すと `unavailable: scene-director-not-created`。delegate が別の SceneDirector を返すように差し替えると、その SceneDirector の値だけが出る | EditMode 単体 |
| AC6 | I4 | 振り分けの dispatcher: Framework で解決した名前は Game を呼ばない。未解決の名前は Game を同じ command と token で 1 回だけ呼び、結果と例外をそのまま返す。null の command と cancel 済みは Game に渡る | EditMode 単体（fake の Game の dispatcher） |
| AC7 | I4（失敗経路） | 配線: Player の判定のとき Game の dispatcher をそのまま渡し、振り分けを作らない。enabled=false で null。`CreateDebugCommandDispatcher` は各場合に 1 回だけ呼ばれる | EditMode（Editor / Player の判定を差し替える） |
| AC8 | I4 | 既存の `SetActiveDebugCommandTests` を変更なしで通す | EditMode |
| AC9 | E1〜E4・I1・I2・I4 | 実 Editor の Play と Release の WPF・CLI で §5 の V1〜V6 | 実機（Phase C） |

### 2.5 判定定義

- GO: I1〜I4 と E1〜E4 を同じ GO 候補 head で満たし、判定必須テスト（§5）が全て通る。
- NO-GO: 上のどれかが満たせない。

### 2.6 risk の理由

`normal`。公開 API・asmdef・`SceneState`・Addressables・寿命の契約を変えない。SampleGame の code を変えない。SceneDirector には internal の読み取り method を 1 つ足すだけで、挙動は変えない。新しい部品（振り分けの dispatcher）は internal で、Editor の配線にだけ入る。`AbstractApplicationInitializer.cs` を触るので、program の停止規則（§1.4）により `RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` の Phase B と同時に B を走らせない。S4-V（`tools/` だけ）・S4-3a（主に `tools/` と文書）とは触る file が重ならない見込みで、Unity の実行の時間だけ分ける。

- A3 後の例外承認: なし
- 決定記録: なし

### 2.7 ここでは答えない問いと所有する後続

- ライフサイクルの変化の観測（event の配信、通し番号、Hierarchy と同じ push と WPF の保持、時系列の表示）、`OnSceneEvent` の通知と実際の遷移のずれの確認: [DEBUG_COMMAND_VISION.md](DEBUG_COMMAND_VISION.md) の段階に置いた。所有するスライスは未起票。
- デバッグコマンドの共通の口、宣言・一覧、名前空間、Player への公開、Scene 寿命のコマンド、書き込みのコマンドの Play の世代、read の出力の統一、失敗の方針: 同じ構想。未起票。
- `osm.scenes` の追加の項目と、Scene 以外の状態のコマンド: 同じ構想。未起票。要求時解決の記録（`ContentRecord`）は program [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md) の S5。
- S3 から移送された項目（DontDestroyOnLoad の表示、Play をまたぐ NodeId の識別、inspect の自己応答保証）: そのまま未起票。

## 3. 責務マップ（A1 初稿）

| file | 責務 | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|---|
| `Runtime/DebugSocketServices/FrameworkFirstDebugCommandDispatcher.cs`（新規） | Framework の catalog を先に引き、未解決を Game の dispatcher へ渡す | 振り分けの規則が変わった時 | DebugSocketService（Play 1 回） | DebugCommands、wire DTO、UniTask | internal | EditMode 単体（fake の dispatcher） | 0 → 約 60 |
| `Runtime/Bootstrap/AbstractApplicationInitializer.cs` | Editor のとき Framework の catalog を作って `osm.scenes` を登録し、振り分けを渡す | 配線だけ | 既存 | 既存 | 変えない | EditMode | 1486 → 約 1505 |
| `Runtime/SceneSystem/SceneDirector.DebugSnapshot.cs`（新規 partial） | SceneDirector の private な状態を一つの時点の不変の data に写す | Scene の状態の持ち方が変わった時 | SceneDirector（Play 1 回） | SceneSystem の内部だけ | internal | EditMode 単体 | 0 → 約 60 |
| `Runtime/SceneSystem/Diagnostics/SceneDirectorDebugSnapshot.cs`（新規） | 写しのモデル（将来の push と共有する語彙。§1.6 C1・C2） | 観測する項目が変わった時 | 値 | なし | internal | 上と同じ | 0 → 約 50 |
| `Runtime/SceneSystem/Diagnostics/ScenesDebugCommand.cs`（新規） | `osm.scenes` の handler、出力の DTO と JSON、読めない時の文言 | コマンドの出力が変わった時 | Framework の catalog（Play 1 回） | SceneSystem、DebugCommands、`JsonUtility` | internal | EditMode 単体（delegate を差し替える） | 0 → 約 90 |
| test（新規の Scene と dispatcher の test、Bootstrap の test） | AC1〜AC7 | — | — | — | — | EditMode | 新規 |
| 文書（§5.12、§10.5、CLI README） | harvest（Phase B が書く） | — | — | — | — | — | 追記 |

- 行数警報: `AbstractApplicationInitializer.cs` は既に 500 行を超える。非分割の理由: 足すのは DebugSocket の生成の箇所の十数行の配線で、判断は新規 file に置く。initializer の分割は program の `RUNTIME_REFACTOR_*` 系の所有。
- 配置の理由: 写しは SceneDirector の private field を読むので partial に置く。写しのモデルとコマンドは「Scene の診断」という別の変更理由を持つので `SceneSystem/Diagnostics/` に分ける。機能が自分の診断を持ち、コマンドは薄い adapter、登録は composition root、transport（`DebugSocketServices/`）は機能を知らない、という置き方の最初の例になる（構想の原則 2）。`Runtime/DebugCommands/` は System だけの決まりがあり、`JsonUtility` と SceneSystem を使うこのコマンドは置けない。
- 新規 file の path は予算（repo 相対 150 文字）の範囲。最長は `Runtime/DebugSocketServices/FrameworkFirstDebugCommandDispatcher.cs.meta`（repo 相対で 106 文字）。

## 4. 実装計画（A1 初稿）

- 順序: (1) 写しのモデルと `CaptureDebugSnapshot` と AC1〜AC4。(2) `ScenesDebugCommand` と AC5。(3) 振り分けの dispatcher と AC6。(4) initializer の配線と AC7・AC8。(5) §5.12、§10.5、CLI README。
- Phase B から Phase A へ差し戻す条件: I1 を守るには SceneDirector の挙動を変える必要がある。Editor / Player の判定を差し替えられる形にするのに公開 API が要る。`DebugCommandCatalog`・router・SampleGame を変える必要がある。asmdef の参照が要る。
- 対象外を維持する方法: `DebugCommandCatalog`・router・protocol の YAML・`tools/DebugStudio/src` の code・SampleGame を変えない（README だけ）。`osm.scenes` の出力を §2.1 の 4 の項目に留める。`OnSceneEvent` を購読しない。

## 5. テストとレビュー計画（A1 初稿）

- 単体テスト: AC1〜AC8（EditMode）。新しい test の class は `unity/Assets/OneStarMaker/Tests/Scene/`（写しとコマンド）、`Tests/Foundation/` か DebugSocket の test の置き場（振り分け）、Bootstrap の test の folder（配線）。
- 差し戻し中の起点 `-Filter`: `SceneDirector|ScenesDebugCommand|FrameworkFirstDebugCommandDispatcher|SetActiveDebugCommand|BeforeSceneLoadServiceStaging|FrameworkServiceSet|DebugCommand`（C が根拠付きで変えてよい）。
- 判定必須テスト: 上の新規 test、既存の `SceneDirector*Tests`、`DebugCommandCatalogTests`、`SetActiveDebugCommandTests`、`DebugSocket*Tests`、最終の全 EditMode 回帰。
- 全 EditMode 回帰の適用除外: なし。
- `tools/DebugStudio` と `tools/DebugCommandOfflineTests`: code を変えないので判定必須にしない。
- 実機の正本（oracle）: Unity Editor の eval で、reflection により SceneDirector の private field（`_currentScenes` の key と各 `SceneBase` の `Lifecycle.State`、`_pendingUnloads`、`_sceneHistory` の列）を直接読み、JSON にして返す。新しい code（`CaptureDebugSnapshot`）を通らない独立した読み取りで、何も書かない。eval の C# の本文は A3 で固定する。
- 操作・実行時の条件の検証経路（AC9。実行担当は判定 C 用に開いた session。S3 の §5.4 と同じ環境の作り方）:
  - V0: clean な検証用 checkout を GO 候補 head に detach し、その `unity/` を自分で開いた Editor 6000.6.9f1。DebugStudio の App・CLI を Release で build し、App を `--listen` で起動して PID を記録する。
  - V1（E1）: SampleGame の Title から Play を始め、`hierarchy` が `hasSnapshot: true` になるまで読み直す（上限 60 秒）。Scene の操作をしないまま、oracle → `osm.scenes` → oracle の順に読む。前後の oracle が一致した時だけ比較を有効とし（一致しなければ自然な進行による変化として読み直す。上限 3 回）、`osm.scenes` の出力と完全一致を照合する。
  - V2（I1）: V1 の読み取りを 5 回続け、各回の前後の oracle と `hierarchy` が変わらないことを記録する。
  - V3（E2）: SampleGame の操作で 1 回 Scene を切り替える（具体の操作と切り替え先は A3 までに SampleGame の Title の画面の操作から決める。未決）。oracle で全ての管理下の Scene が `Stable` になったことを確かめてから V1 と同じ順で読み、`history` と `canGoBack` を照合する。
  - V4（E4）: S3 の §5.4 と同じ手順で `debug.set-active` を false → true と送り、各回の `inspect` を記録する。
  - V5（E3・I2）: Play を止め、同じ Editor で再び Play し、V1 と同じ照合を行う。2 回目の oracle と完全一致し、1 回目だけの Scene または 1 回目の履歴が出ないことを記録する。
  - V6: Play を止め、WPF に通常の終了を要求し、Scene が clean か、tracked の変更が無いかを記録する。
  - 照合の機械判定: `osm.scenes` の payload と oracle の JSON を、`scenes`（id の集合と各値）と `history`（列）で突き合わせる小さな比較を判定 C の offline で行う（方式は A3 で凍結。実行担当が結果を見て期待値を決めない）。stdout / stderr / exit も記録して判定に使う。
  - 遷移の途中の状態の検証は実機で行わず、AC4 で閉じる。
  - 保存: 各段のコマンド、開始・終了の UTC、stdout / stderr / exit、oracle の戻り値、比較の結果。Git に入れず、[レビュー証拠](../../.agents/skills/osm-workflow/references/review-evidence.md#保存先とリモートへの配布) に従って保存する。
- 未知の操作経路の疎通結果: なし（`send` の経路は S3 で成立済み）。V3 の Scene の切り替えの操作と、V5 の再 Play の場面だけは A3 までに決める。
- 人間の判断が必要な条件: なし。
- 高コスト証拠: なし。
- 判定 C の照合 script の方式: 判定 C の offline 実行（上の比較）。期待値は oracle から実行時に得るので、機械向けの期待値 file は作らない。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。
- A0/A1 主担当: Cloud の計画 session（モデル名は書かない）。
- C' 用に予約した担当: 未定。A0/A1 の系列、A2 の担当の系列、外部の意見（§1.5）を出した系列を外し、A2 の割当と同時に予約する。

### 5.1 A2 の独立レビューと A3 の採否

未実施。§1.5 の外部の意見は A2 の前の非公式の意見で、A2 に数えない。

## 6. Phase B 実装結果

未到達。

## 7. Phase C

未到達。

## 8. Phase C'

未到達。

## 9. Phase D

未到達。
