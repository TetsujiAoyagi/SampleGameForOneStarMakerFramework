# DebugStudio S4 — Framework のデバッグコマンドの口と、Scene の状態を CLI から読む `osm.scenes`

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `A`（A1 初稿 revision 2。A2 は未実施、A3 は未凍結。**実装キューではない**）
- branch: `claude/magical-gauss-7rycuj`（Cloud の計画 session の branch。PR の base は `develop`。PR は未作成）
- implementation base commit: `07cc2c975b63cf5b668130a1c45490dd3e77c5f0`（`origin/develop` の head、2026-11-25。本書の実測は全てこの base）
- implementation head commit: `未固定`
- A3 凍結記録: `未凍結`
- risk: `high`（公開 API を 1 つ置き換える。理由と owner の判断の余地は §2.7）
- owner: root（依頼者）
- created: 2026-11-25
- expires: 2026-12-31、または A3 の凍結で置き換わった時点。期限時は owner が更新・廃止を判断する
- harvest to: `unity/Assets/Docs/Architecture/12-telemetry.md` §10.5（コマンドの口の契約。§2.2 の「今決めること」と「後から足せること」の足し方を含む）、`unity/Assets/Docs/Architecture/05-scene.md` §5.12（外から読める Scene の状態と、観測が寿命と状態を変えないこと。あわせて §5.12 の `SceneEvent` の欄の記述が code に遅れている点を直す）、`tools/DebugStudio/src/DebugStudio.Cli/README.md`（`osm.scenes` の送り方と出力の読み方）
- Phase A snapshot の取得案内・状態: 未生成（A3 で凍結版を commit し、その SHA を tree の外で渡す）
- Phase B result snapshot / evidence bundle / C' blind bundle の取得案内・状態: 未生成
- task: 未開始（`tools/workflow-task.ps1 start` は凍結の時に行う）
- 引継ぎ票:
  - 次の Phase・役割: Phase A の主担当（owner の確認 → A2 → A3）。
  - 次の作業: (1) owner が §1.9 の未決事項と §2.7 の risk を確認する。(2) A1 を確定し、A2 の独立レビュー（目的レビュー 1 件、アーキテクチャゲート 1 件。high のままなら A0（§1）だけから別の構成を出すレビューを 1 件）を同じ入力版で行う。(3) A3 で採否を記録し、Draft PR で外部計画レビューを受けてから凍結する。
  - 読む範囲: 既定（[SKILL.md](../../.agents/skills/osm-workflow/SKILL.md#作業ごとの読み先) の表。Phase A は全節）。
  - 必要な権限: なし（文書のみ）。
  - 停止規則: owner の確認の前に A2 を始めない。凍結の記録が無いまま Phase B を始めない。
  - 引き渡さないもの: なし。

### 実測の方法

- base `07cc2c97` の code と文書を `grep`・`wc -l`・行範囲の読み取りで確かめた。Unity も .NET も起動していない。
- 本書の作成環境（Cloud の Linux container）に `pwsh`・`dotnet`・Unity が無く、`tools/docs-audit.ps1` と `DebugCommandOfflineTests` は未実行。link 先の存在は `git ls-files` で手で照合した。

### 改訂の経緯

- revision 1（commit `f662d9ae`）: 状態の source の表と、Framework のコマンドを Game の dispatcher の前で振り分ける合成を置いた 1 スライス。
- 分割版（commit `1d1ad645`）: コマンドの口（宣言 5 項目、`osm.` の予約の強制、`osm.commands` の一覧）を S4a、Scene の読み取りを S4b に分けた。
- revision 2（本書）: owner が依頼した外部の意見（2026-11-25。要旨は §1.5）を受けて 1 スライスに戻し、owner の指示「今は決めなくていいもの、後から足せるもののバランスが取れるといい、デバッグコマンドは」（2026-11-25）に従って、§2.2 で各要素を「今決める」「後から足す」に分けた。宣言・一覧・予約の強制は後から足す側へ移した。

## 1. 目的と対象外

### 1.1 目的と利用者（A2 の目的レビューへ渡す原文の候補）

- 目的: Editor の通常の Play 中に、GameObject を持たない Framework の状態を、表示のための GameObject や MonoBehaviour を作らずに、DebugStudio の CLI からその時点の値として読めるようにする。最初に読めるようにするのは SceneDirector が管理する Scene の一覧と各 Scene の `SceneState`、遷移の履歴である。そのために Framework も Game と同じ口にデバッグコマンドを登録できるようにし、その口は後から機能を足しても既存の登録を書き直さずに済む形にする。
- 利用者: (a) SampleGame を Editor で Play し、DebugStudio WPF を起動したまま CLI のコマンドを実行してその出力を読む開発者と AI エージェント（検証の driver を書く担当と、判定 C の実行担当を含む）。(b) デバッグコマンドを足す Framework と Game の開発者（AI エージェントを含む）。
- 文言の出どころ（owner、2026-11-25）: 「unity の MonoBehaviour でないクラスはデバッグ表示用の GameObject か MonoBehaviour 作ろうと思ってたが、まず DebugStudio で表示するのが素直だな」「これを機に framework 側のコマンドの口考えてもいいのでは」「今は決めなくていいもの、後から足せるもののバランスが取れるといいと思うんすよ、デバッグコマンドは」。質問への回答で決めたこと: 表示先は CLI の JSON だけ、実行形態は Editor の Play だけ、既存の `CreateDebugCommandDispatcher()` の override は新しい hook へ移して廃止、Scene 寿命のコマンドは含めず余地だけ残す。

### 1.2 対象外

- 後から足す側に置いた物（§2.2 の表の「後から足す」の行）: コマンドの宣言（説明・種別・使える場・payload の例）、コマンドの一覧、`osm.` の予約の機械的な強制、Scene 寿命のコマンド、Player でのコマンド、read 系の出力の形の統一、WPF の表示、専用の CLI サブコマンド、protocol の message 種別、`osm.scenes` の追加の項目（静的な木、進行中の操作、取消の窓、要求時解決の記録）、Scene 以外の状態のコマンド。
- 書き込み。状態を変える操作（Scene の load・unload・遷移、`SceneState` の変更、履歴の変更）。
- 状態の変化の購読・履歴の蓄積・差分の配信。読むのは要求した時点の値だけ。変化の流れは既存の telemetry が担う。
- 組込みの `ping` / `runtime-diagnostics` の経路（transport の層で main thread を待たずに応答するので router に残す）。
- `debug.set-active` の挙動（payload・結果・拒否の文言）。登録の書き方だけを新しい hook へ移す。
- `DebugCommandCatalog` の変更。
- `SceneState` の値・順序、`SceneLifecycleManager` の遷移規則、SceneDirector の挙動。
- hierarchy に DontDestroyOnLoad を出すこと、Play をまたぐ NodeId の識別、inspect の自己応答保証（S3 から移送された項目）。
- S2 HANDOFF（`DEBUGSTUDIO_S2_SIMPLE_A3_CANDIDATE.md`）の Phase D。

### 1.3 現況（base `07cc2c97`）

**コマンドの口:**

- `AbstractApplicationInitializer.CreateDebugSocketService(AppConfig)`（`Runtime/Bootstrap/AbstractApplicationInitializer.cs:1428〜1437`、protected virtual、1486 行の file）は、`debugSocket:enabled` が false なら null、true なら `new DebugSocketService(options, CreateDebugCommandDispatcher())`。BeforeSceneLoad の中（:299〜302）で呼ばれる。
- `CreateDebugCommandDispatcher()`（:1443、protected virtual）の既定は `NullDebugCommandDispatcher.Instance`。**口はこの 1 本で、Game が override すると dispatcher を丸ごと作る。Framework が自分のコマンドを足す口は無い。** override するのは SampleGame の `AppInitializer`（`unity/Assets/SampleGame/DependOnAll/AppInitializer.cs:230〜240`、468 行）だけで、自分の `DebugCommandCatalog` を作り `SetActiveDebugCommand.Register(catalog, Application.isEditor, () => DebugSocket)` が true なら `CatalogDebugCommandDispatcher`、false なら base を返す。
- `SetActiveDebugCommand.Register`（`SampleGame/DependOnAll/DebugCommands/SetActiveDebugCommand.cs:45〜68`、187 行）は `isEditor` が false なら登録しない。**Player に書き込みを出さない規則は、このコマンドの登録の書き方が持っている。** payload の解析と結果の JSON は `JsonUtility` と `[Serializable]` の DTO。
- 未登録の文言: `NullDebugCommandDispatcher`（`Runtime/DebugSocketServices/`、38 行）は `Debug command '<名前>' is not registered.`（名前入り）。`DebugCommandCatalog` は `Debug command is not registered.`（名前なし）。SampleGame の Editor は catalog、Player は Null なので、今の Player の文言は名前入り。
- `DebugCommandCatalog`（`Runtime/DebugCommands/`、public sealed、75 行）: `Register(string name, DebugCommandHandler handler)`、`TryExecute`、`Count`。名前は Ordinal の完全一致、重複は `InvalidOperationException`。宣言の情報は持たず、列挙の口は無い。`DebugCommandCatalog.cs` と `DebugCommandResult.cs` は .NET の offline テスト `tools/DebugCommandOfflineTests`（CI `offline-checks.yml`）に直接 link され、同じテストが `Runtime/DebugCommands/` の全 `.cs` に `ScriptMachine`・`DebugStudio`・`SampleGame`・`record ` の文字列が無いことを検査する。**本書は catalog を変えないので、この検査に影響しない。**
- router（`DebugSocketServices/Protocol/DebugSocketInboundMessageRouter.cs`、277 行）は組込みの 2 つを main thread に切り替えずに処理し、それ以外を `MainThreadDebugCommandDispatcher` で包んだ dispatcher へ main thread で渡す。結果は `CommandResult`（Success・Message・PayloadJson）。CLI の `send` は `CommandResult request=… success=… message=…` の行と payload の行を、success=true なら stdout、false なら stderr へ出す（exit 0 / 1、期限切れ 2）。`hierarchy` / `inspect` の `status/reason/data` の JSON とは形が違う。
- 契約は [12-telemetry.md](../../unity/Assets/Docs/Architecture/12-telemetry.md) §10.5: 登録は初期化の時だけ、closure は App 寿命の依存だけを持ち Scene 所有の object の強参照の登録は非対応、`TryExecute` は呼出し側の thread で同期実行、Framework は書き込み口を持たない。

**Scene の状態（全て `Runtime/SceneSystem/`）:**

- 実行時の状態は `SceneDirector.cs`（336 行。ほかに partial の `.Loading.cs` 747・`.Unloading.cs` 478・`.Transitions.cs` 256・`.FailedLoadCleanup.cs` 349）の private field: `_currentScenes`（`Dictionary<string, ScenePair>`、:77。親子の link は無い）、`_pendingUnloads`（`HashSet<string>`、:78）、進行中の操作の 4 つの辞書（:79〜82）、`_sceneHistory`（`Stack<SceneHistoryEntry>`、:104。上限 8）。
- 各 Scene の `SceneState` は `SceneBase.Lifecycle`（`internal SceneLifecycleManager Lifecycle => _lifecycle;`、`SceneBase.cs:221`）の `State`（`public SceneState State => _state;`、`SceneLifecycleManager.cs:20`）。どちらも field を返すだけ。`SceneLifecycleManager` だけが `_state` を書く。
- Scene の親子と `LoadType` は静的な data（`SceneResource` / `SceneResourceMap`）。
- 読み取りの口: 公開は `ISceneQuery`・`CanGoBack`・`OnSceneEvent`。internal に「テスト用」の `ContainsScene`・`GetSceneState`・`HasPendingUnload`・`SceneHistoryCount`（:271〜283）。**全 Scene を列挙する口と履歴の中身を読む口は無い。**
- 変更は全て main thread の UniTask の async method の中で起きる（thread pool への切替え・`lock` は無い）。frame の間に main thread で読むと thread の競合は無いが、操作の途中の状態が見える。
- `SceneState` は 14 値（`None`=0 … `LoadCanceled`=7, `Stable`=8 … `AfterUnloading`=13）。

**生成と寿命:**

- DI container は無い（手動 DI。[03-di.md](../../unity/Assets/Docs/Architecture/03-di.md)）。BeforeSceneLoad で DebugSocket（と dispatcher）を作り、AfterSceneLoad で `SceneDirector` を作る（:439）。**コマンドの登録は SceneDirector より先に終わる。** initializer には `protected SceneDirector? SceneDirector`（:128）がある。
- `ReleaseAll` は services（DebugSocket を含む）を Dispose してから（:1097）、`_sceneDirector` を Dispose して null にする（:1108〜1109）。
- Enter Play Mode Options が有効で Domain Reload が無効。Play ごとに service と SceneDirector を作り直す。
- 全て `OneStarMaker.Runtime.asmdef` の中で、asmdef の参照の追加は要らない。

### 1.4 常時契約と既知の制約（本文へ転記）

- 依存は Game → Framework の一方向。asmdef の参照と package を追加しない（このスライスは要らない）。
- `SceneState` の 14 値を減らさず並べ替えない。状態の変更は `SceneLifecycleManager` が所有する。出力に整数値を出さず名前で出す。
- Unity 側の C# で `record` を使わない。新規・編集する Unity 側の `.cs` の先頭に `#nullable enable`。
- 破棄されうる `UnityEngine.Object` は `== null` / `!= null` で判定する。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。
- Editor のコードを Runtime の asmdef に置かない。`Runtime/DebugCommands/` を System だけに保つ（本書は触らない）。
- path は `tools/path-budget.json` の予算（repo 相対 150 文字）に従う。
- 参照 0 の API を削除の理由にしない。消すのは owner が「新しい hook へ移して廃止」と決めた `CreateDebugCommandDispatcher` だけ。
- Unity テスト・Build は Phase C。Phase B は Editor での compile 確認まで。Unity 終了時の native stall は `tools/Harness/README.md` の停止規則に従う。
- S2 / S3 の契約（`--listen` の待受け、`send` / `hierarchy` / `inspect` の出力と exit、`debug.set-active` の payload・結果・拒否の文言、Player では書き込みのコマンドを登録しないこと）を壊さない。
- 05-scene.md §5.12: `OnSceneEvent` の購読者は SceneDirector の挙動に影響してはならない。観測が状態を変えないという要求はこれと同じ向き。
- program の停止規則: Runtime を触るスライスの Phase B を `RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` の Phase B と同時に走らせない。本スライスは `AbstractApplicationInitializer.cs` の DebugSocket の生成の箇所（:1428〜1444）を触るので当たる。
- 人間が開いた Editor を強制終了しない。共有の Editor を触らない。並行の S4-V・S4-3a の Unity の実行と時間を分ける。

### 1.5 原資料

- S3 の HANDOFF（commit `d0e4c03c` の親の `docs/handoff/DEBUGSTUDIO_S3.md`）§2.1・§2.4: `CreateDebugCommandDispatcher` の override と `SetActiveDebugCommand.Register` の配線、Player で登録しない理由、後続へ送った項目。
- 外部の意見（owner が別途依頼し、2026-11-25 に本 session へ要旨を渡した。code を読んだだけで実行はしていない）: (1) 宣言 5 項目は今は要らない。3 つのコマンドが全て Editor だけで、種別は自己申告で検査できず、説明と例は README と二重になる。後から overload で足せるが、今入れると旧 `Register` の削除で破壊的になる。(2) 一覧のコマンドはスライスを成立させるために足されていた。観測できる最初の Framework のコマンドは `osm.scenes` で足りる。(3) 本命が「ついで」の Phase D 待ちになる順序は逆。(4) 代わりに 1 スライスで、catalog を変えず、Framework が先に登録して既存の重複の例外で上書きを防ぎ、出力を目的の文言まで絞り、JSON を `[Serializable]` と `JsonUtility` にする。(5) 分割版の不整合: Player の未登録の文言が名前入りから名前なしに変わる、一覧の JSON の escape の要求と既存の `EscapeJsonString`（`\` と `"` だけ）が食い違う。(6) `send` に read を載せると出力が `hierarchy` / `inspect` と別系統になる。read を増やすなら一覧より先に出力の形を揃える方が効く。主担当は (5) の 2 点を code で確かめた（`NullDebugCommandDispatcher.cs` の文言、`DebugSocketBuiltInCommandHandler.cs:74〜79`）。

### 1.6 A0 確認

未実施。`send` → WPF → Runtime の catalog → main thread → `CommandResult` の経路は S3 の判定 C（2026-11-23）で成立している。本スライスは同じ経路で catalog の作り手を変え、新しい名前のコマンドを 1 つ足すだけなので、未知の操作経路は無いと見る。A2 が否定したら A3 の前に S3 の §1.6 と同じ形の小さな確認を行う。

### 1.7 UC ごとの利用者の入力

| UC | 利用者が選ぶ・入れるもの | 機械が決めるもの | 誰がいつ | 目的からの正当化 |
|---|---|---|---|---|
| UC1 Scene の状態を読む | コマンド名 `osm.scenes`（README の固定値）、payload `{}` | 出力の中身（§2.1 の 3）、読んだ時点の frame の番号 | 実行時の操作 | 「Scene の一覧と各 Scene の `SceneState`、遷移の履歴」 |
| UC2 読めないとき | 新しい入力は無い | `unavailable: …` の区別 | 実行時の操作 | 「その時点の値として読める」。読めない時に古い値や空を正常と誤読させない |
| UC3 コマンドを足す（Game の開発者） | `RegisterDebugCommands` の override の中で名前と handler（今と同じ 2 つ） | Editor でだけ呼ばれること（Player に出ないこと） | 開発時の宣言 | 「Game と同じ口に」「既存の登録を書き直さずに済む」。今の `isEditor` の引数は要らなくなる |
| UC4 コマンドを足す（Framework の開発者） | initializer の Framework の登録の箇所で名前と handler | Game より先に登録されること | 開発時の宣言 | 「Framework も…登録できる」 |

A1 自己点検: 新しい利用者の入力は `osm.scenes` の名前だけ。開発者の入力は今と同じ名前と handler の 2 つで、増えない（今の `isEditor` を無くすので 1 つ減る）。目的の「後から機能を足しても既存の登録を書き直さずに済む」は、§2.2 の「今決めること」の各行で満たす。

### 1.8 スライスの分割の検討（A1 の終わり）

- 契機: 「hook の置き換え」と「`osm.scenes`」は独立して変わる理由を持つ。
- 判断: 分けない。hook の置き換えだけでは利用者が観測できる成立条件が作れず（Framework のコマンドが 1 つも無い）、観測できる最初の Framework のコマンドが `osm.scenes` である。どちらも同じ Editor の Play の 1 回で判定でき、独立して失敗しうる未知は無い。分割版（S4a / S4b）は、口を観測させるために一覧のコマンドを足す必要が生じ、本命が口の Phase D を待つ順序になったので戻した（§1.5）。

### 1.9 未決事項（owner の確認待ち）

- **U1 Player で Game の hook を呼ばないこと（§2.2 の D3）。** 今 Player で動くコマンドは無いので実害は無い。代わりに、Player で動くコマンドを Game が足したくなった時は、後から足す明示の登録（§2.2 の L5）を待つことになる。
- **U2 `osm.scenes` の出力の項目（§2.2 の D6）。** 案は id・`SceneState`・pending の unload・履歴だけ。静的な木（parent / children / loadType）、進行中の操作、取消の窓は後から足す（足しても既存の読み手は壊れない）。最初から欲しい物があれば A3 までに。
- **U3 risk（§2.7）。** 公開 API の変更は hook 1 本の置き換えだけ。規則の高リスクの例（公開 API）に当たるので `high` で書いた。owner が `normal` と判断するなら、A2 の別構成のレビューを省ける。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: SampleGame の通常の Editor の Play 1 回の中で、Framework が先に・Game が後に同じ catalog へコマンドを登録し、GameObject・MonoBehaviour・購読を作らずに、CLI の `send --command osm.scenes` で SceneDirector が管理する Scene の一覧・各 `SceneState`・遷移の履歴をその時点の値として読め、読む前後で状態が変わらず、`debug.set-active` と Player の挙動が今と同じか。
- 次スライスへ進む最低条件: §2.3 の不変条件 I1〜I4 と成立条件 E1〜E3 を同じ GO 候補 head で満たす。

### 2.1 統合設計（A1 初稿 revision 2。A2 の対象）

1. **hook（`AbstractApplicationInitializer`）**
   - `CreateDebugCommandDispatcher()` を消し、`protected virtual void RegisterDebugCommands(DebugCommandCatalog catalog)`（既定は何もしない）を足す。
   - `CreateDebugSocketService` の中で: enabled が false なら今どおり null。Player（`!Application.isEditor`）なら今どおり `NullDebugCommandDispatcher.Instance` を渡し、catalog を作らず hook を呼ばない。Editor なら catalog を作る → Framework のコマンドを登録する（今は `osm.scenes` だけ）→ `RegisterDebugCommands(catalog)` を呼ぶ → `new CatalogDebugCommandDispatcher(catalog)` を渡す。`CreateDebugSocketService` の signature は変えない。
   - catalog は initializer の field に持たない（DebugSocketService が dispatcher を通して持ち、service の Dispose で手放す）。
2. **SampleGame の移行**
   - `AppInitializer`: `CreateDebugCommandDispatcher` の override を `RegisterDebugCommands` の override に置き換え、`SetActiveDebugCommand.Register(catalog, () => DebugSocket)` を呼ぶ。
   - `SetActiveDebugCommand.Register`: `isEditor` の引数と bool の戻り値を消す（Editor でだけ呼ばれることは hook が保証する）。handler の中身は変えない。
3. **`osm.scenes`（`Runtime/SceneSystem/`）**
   - 新規 partial `SceneDirector.DebugSnapshot.cs` に `internal SceneDirectorDebugSnapshot CaptureDebugSnapshot()`。main thread で、await を挟まず一度に `_currentScenes` の key と各 `SceneBase.Lifecycle.State`、`_pendingUnloads`、`_sceneHistory` を読んで不変の data に写す。field に書かない、event を出さない、`SceneBase` の method を呼ばない、`SceneResource` を読まない。
   - 新規 `Runtime/SceneSystem/Diagnostics/ScenesDebugCommand.cs`（internal static）と写しの型・出力の DTO。initializer は `() => SceneDirector` を渡し、handler が実行の時に読む。SceneDirector を登録の時に捕まえない。
   - payload は読まない（README では `{}` を示す）。
   - 成功: `success=true message=captured`、payload は `[Serializable]` の DTO を `JsonUtility.ToJson` した JSON の object: `capturedFrame`（`Time.frameCount`）、`scenes`（`{id, state, pendingUnload}` の列。`state` は `SceneState` の名前。順は id の Ordinal 順）、`history`（新しい順の `{from, to}` の列）、`canGoBack`。
   - SceneDirector が無い（AfterSceneLoad の前・起動の失敗の後・`ReleaseAll` の後）: `success=false message=unavailable: scene-director-not-created`。古い写しを返さない。
   - 出力の読み方（README）: `capturedFrame` の時点の値で、続けて読んだ時の値と同じとは限らない。操作の途中の状態（`LoadCanceled` など）がそのまま出ることがある。結果不明の読み方は S3 と同じで、読み取りなので送り直してよい。

### 2.2 今決めること・後から足せること

**判定の基準:** 後から変えると、既存の Game の登録・利用者の script・README の手順・CLI の出力を読む側のどれかを書き直させる物は「今決める」。足しても既存の物が今と同じに動く物は「後から足す」で、足し方（既存を壊さない形）だけを今書いておく。決めずに置いておくと後で破壊的にしか足せなくなる物も「今決める」に入れる。

**今決めること（このスライスで固定し、§10.5 に書く）**

| # | 決めること | 決める内容 | 今決める理由（後から変えると何が壊れるか） |
|---|---|---|---|
| D1 | 登録の入口の形 | `RegisterDebugCommands(DebugCommandCatalog catalog)`。引数は catalog の object そのもの | 入口の signature は全ての Game の override が依存する。引数を catalog にしておけば、宣言付きの登録や登録の解除などを catalog の overload として足しても hook は変わらない（D1 を細い delegate にすると、足すたびに hook の signature が変わる） |
| D2 | 登録の順と上書き | Framework が先、Game が後。同じ名前は既存の重複の例外で初期化に失敗する | 順が後から逆になると、Game が Framework の名前を黙って奪える状態に変わる。既存の例外を使うので新しい規則は要らない |
| D3 | 今の `Register(name, handler)` の意味 | 「Editor でだけ有効」。Player では hook を呼ばず、今どおり Null の dispatcher を渡す | 将来 Player のコマンドを足す時に、既存の `Register` の登録が Player に漏れないようにするため。この意味を今決めておけば、Player 用は別の明示の登録として足せ（L5）、既存の Game の登録は何も変えずに Editor だけに留まる。決めずに「Player では呼ばない」実装だけにすると、後で Player でも hook を呼んだ瞬間に全ての既存の登録が Player に出る |
| D4 | 名前の規約 | Framework は `osm.` で始める。Game は `osm.` で始めない。CLI と README と script はこの名前で呼ぶ | 名前は利用者の script と README に直接書かれ、後から改名すると全て壊れる。強制（例外）は後から足せるので今は規約として書くだけ（L3） |
| D5 | 結果の文言の接頭辞 | 読めない時は `unavailable: <理由>`。既存の `rejected: <理由>`（S3）と並べる。理由の語は足してよいが変えない | 利用者と driver は message の接頭辞で分岐する。後から接頭辞を変えると分岐が壊れる |
| D6 | read の出力の規則 | payload は JSON の object（列にしない）。列挙値は名前で出す（整数を出さない）。項目は足すだけで、名前を変えない・消さない・意味を変えない。列の順を決めて書く（`scenes` は id の Ordinal 順、`history` は新しい順） | 出力を読む driver が依存する。object にしておけば項目の追加で済み、将来 read の出力を `status/reason/data` の形に揃える時（L6）も `data` にそのまま入る。整数で出すと `SceneState` の順に依存する |
| D7 | 入力の規則 | payload は JSON の object。`osm.scenes` は今は読まない。将来の引数は省略できる field として足し、省略した時は今と同じ結果にする | 後から必須の引数を足すと、今の `{}` の呼び出しが壊れる |
| D8 | handler の寿命 | handler は App 寿命の依存だけを持ち、Scene 寿命・Play 1 回の object（SceneDirector を含む）は実行の時に読む。static の状態を持たない | 間違えると Domain Reload が無効な Editor で Play をまたいで古い object を握る。後から直すと全ての handler の書き直しになる（§10.5 の既存の契約を Framework の handler にも当てる） |
| D9 | 古い `CreateDebugCommandDispatcher` | 消す（owner 決定） | 残すと口が 2 本になり、両方を使った時の優先の規則を後から決められなくなる |

**後から足せること（このスライスでは作らない。足し方だけ書く）**

| # | 足せる物 | 足し方（既存を壊さない形） | 足す契機の例 |
|---|---|---|---|
| L1 | コマンドの宣言（説明・種別・使える場・payload の例） | catalog に宣言付きの `Register` の overload を足す。今の `Register(name, handler)` は残し、宣言なしの登録として扱う | 一覧を出したくなった時、Player のコマンドを足す時（L5） |
| L2 | コマンドの一覧（`osm.commands`） | L1 の後に Framework のコマンドとして足す。宣言なしの登録は名前だけ出す | コマンドが増えて README で追えなくなった時 |
| L3 | `osm.` の予約の強制 | Game の登録の入口で `osm.` の名前を例外にする。D4 の規約を守っている Game は影響を受けない | 規約の違反が実際に起きた時 |
| L4 | Scene 寿命のコマンド | 登録の解除の handle を返す overload を catalog に足し、§10.5 の「登録は初期化の時だけ」を「main thread の上で、いつでも」に緩める（登録も実行も main thread なので並行の問題は生じない） | Scene ごとのデバッグ操作が要る時 |
| L5 | Player のコマンド | Player 用の明示の登録（宣言の「使える場」か専用の overload）を足し、Player でも hook を呼ぶ。D3 により既存の `Register` は Player では無効のまま | Player でのデバッグが要る時（program の S4 系の後） |
| L6 | read の出力の形の統一 | CLI に read 用の出力（`status/reason/data`）を足し、D6 の object を `data` に入れる。`send` の出力は変えない | read のコマンドが 2 つ以上になった時（例: `osm.assets`） |
| L7 | WPF の表示、専用の CLI サブコマンド、protocol の message 種別 | 同じ `osm.*` のコマンドを下で呼ぶ。名前と出力（D4・D6）はそのまま | 人が常に見たくなった時 |
| L8 | `osm.scenes` の追加の項目（静的な木の parent / children / loadType、進行中の操作、取消の窓、`ContentRecord`） | D6 により項目を足すだけ。`ContentRecord` は program の S5（`EDITOR_WORKFLOW_AND_OBSERVABILITY`）が形を決めた後 | 読み手が要ると言った時 |
| L9 | Scene 以外の状態（`osm.assets`、`osm.update` など） | Framework の登録の箇所にコマンドを足す。材料は `IAssetDiagnostics`・`AssetRegistry.GetAllLoadedAssets`（assets）、`UpdateCoordinator` への列挙の口の追加（update） | 個別に |
| L10 | payload の解析の共通化 | handler が使える helper を足す。既存の handler は変えない | 引数を持つコマンドが増えた時 |

**決めないことにした物（今決める必要も、足す予定も無い）:** コマンドの version の番号（D6 の「足すだけ」の規則で足りる。要るなら後から項目として足せる）。

### 2.3 不変条件と成立条件

**不変条件（起きてはいけないこと）**

- **I1 読み取りは状態を変えない。** `osm.scenes` を何回送っても、SceneDirector の管理下の Scene の集合と各 `SceneState`・pending の unload・進行中の操作・履歴、Unity の読み込み済みの Scene の集合、hierarchy、AssetManagement の保持数が、送らなかった場合と同じ。`OnSceneEvent` に event が出ない。
  - 主張を崩しうる既存の経路: (a) 読む getter が副作用を持つ（`SceneBase.Lifecycle` と `SceneLifecycleManager.State` は field を返すだけと確かめた。他の getter は読まない）。(b) `_sceneHistory` の列挙で `Stack` を変える method を呼ぶ（列挙だけにする）。(c) `JsonUtility` が DTO の getter を呼ぶ（DTO は field だけにする）。
  - 探索した境界: `SceneDirector.cs` の field の宣言、`SceneBase.cs:221`、`SceneLifecycleManager.cs:20`。`SceneResource` は読まないので経路から外した。
- **I2 観測が寿命を延ばさない。** `ReleaseAll` の後（Play の停止の後）に、catalog と handler のどれも SceneDirector・`SceneBase` を参照していない。次の Play で読んだ値に前の Play の Scene が出ない。
  - 崩しうる経路: (a) 登録の時に SceneDirector を closure に捕まえる（D8 で禁止）。(b) 写しを cache する（持たない）。(c) static に置いた物が Play をまたいで残る（D8 で禁止）。
- **I3 一度の読み取りの値は一つの時点の値である。** 1 回の応答の中の値は main thread の同じ時点（await を挟まない一続きの読み取り）から取る。
  - 崩しうる経路: 写しの途中で await する。catalog は handler を同期で呼ぶので、handler の中に await が無ければ保たれる。
- **I4 既存の挙動を変えない。** (a) Player: `debugSocket:enabled` が true の Player で、今と同じく Null の dispatcher が使われ、未登録の文言（名前入り）も同じ。(b) `debug.set-active` の payload・結果・拒否の文言・exit が S3 と同じ。(c) 組込みの `ping` / `runtime-diagnostics`。(d) enabled=false で DebugSocket を作らず hook を呼ばない。
  - 崩しうる経路: Player の判定を誤って catalog を作る、hook の中の例外の扱いが変わる、`() => DebugSocket` を登録の時に読む。
  - 既知の差（不変条件の外として記録）: Game が `CreateDebugCommandDispatcher` を override していない Editor では、未登録の文言が名前入り（Null）から名前なし（catalog）に変わる。SampleGame の Editor は既に catalog なので変わらない。

**成立条件（起きなければならないこと）**

- **E1** 通常の SampleGame の Editor の Play（Title から起動）で、`hasSnapshot: true` になった後に `send --command osm.scenes --payload '{}'` が exit 0 で、出力の Scene の集合と各 `SceneState` が、同じ frame の近くに main thread で `ContainsScene` / `GetSceneState` で読んだ値と一致する。
- **E2** SampleGame の操作で 1 回 Scene を切り替えた（遷移の後に Stable になった）後に読むと、切り替え先が `Stable`、`history` の先頭が今の遷移の `{from, to}`、`canGoBack` が true。
- **E3** 同じ Play で S3 の手順（`hierarchy` → `debug.set-active` false → `inspect` → true → `inspect`）が S3 と同じ出力・exit で動く。

### 2.4 受け入れ条件（A1 初稿）

| AC | 検証する条件 | 内容 | 方法 |
|---|---|---|---|
| AC1 | E1・I3 | fake の factory で親子の Scene を AddScene した SceneDirector の `CaptureDebugSnapshot` が、各 Scene の id・`state`・`pendingUnload` を写し、順が id の Ordinal 順 | EditMode 単体（既存 `SceneDirectorTestBase` の上） |
| AC2 | E2 | `SwitchScene` の後の写しの `history` の先頭と `canGoBack` | EditMode 単体 |
| AC3 | I1 | 写しの前後で `_currentScenes` の key 集合・各 `SceneState`・`_pendingUnloads`・`SceneHistoryCount`・`OnSceneEvent` の受信数が同じ。写しを 100 回続けても同じ | EditMode 単体 |
| AC4 | I3・I1（失敗経路） | AddScene の await の途中（`LoadCanceled` を含む途中の状態）で写しても例外にならず、途中の状態の名前がそのまま出て、写しの後も操作が完了する | EditMode 単体 |
| AC5 | I2（失敗経路）・D5 | handler に渡した delegate が null を返すと `unavailable: scene-director-not-created`。出力の JSON が D6 の形（object、名前の列挙値） | EditMode 単体 |
| AC6 | I2・D2・D8 | initializer の配線: Editor では Framework の登録 → hook の順。Game が `osm.scenes` を登録すると初期化で失敗する。2 回の初期化（Play の模擬）で 2 回目の写しに 1 回目の Scene が出ない | EditMode（既存 `BeforeSceneLoadServiceStagingTests` の流儀） |
| AC7 | I4（失敗経路）・D3 | Player の構成（`isEditor=false` を差し込む）で catalog を作らず hook を呼ばず、Null の dispatcher を渡す。enabled=false で hook を呼ばない | EditMode（initializer の判定を差し替えられる形にする。形は A2 の対象） |
| AC8 | I4・E3 | `SetActiveDebugCommandTests` を新しい登録で通す（payload・結果・拒否の文言は変えない） | EditMode |
| AC9 | E1・E2・E3・I4 | 実 Editor の Play と Release の WPF・CLI で §5 の V1〜V5 | 実機（Phase C） |

### 2.5 判定定義

- GO: I1〜I4 と E1〜E3 を同じ GO 候補 head で満たし、判定必須テスト（§5）が全て通る。
- NO-GO: 上のどれかが満たせない。

### 2.6 ここでは答えない問いと所有する後続スライス

- §2.2 の L1〜L10: どれも DebugStudio S5 以降（未起票）。ただし L8 の `ContentRecord` は program [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md) の S5、L5 の時期は同じ program の S4 系の Phase D の後に owner が決める。
- `CreateDebugSocketService` の override で口を迂回できること: 未所有。§10.5 に「この override は口の規則の外」と書くだけ。
- S3 から移送された項目（DontDestroyOnLoad の表示、Play をまたぐ NodeId の識別、inspect の自己応答保証）: そのまま DebugStudio S5 以降（未起票）。
- 状態の変化の購読（push）: 未所有。

### 2.7 risk の理由

`high` で書いた。公開 API の変更は `AbstractApplicationInitializer` の hook 1 本の置き換え（`CreateDebugCommandDispatcher` の削除と `RegisterDebugCommands` の追加）と、SampleGame の internal の `SetActiveDebugCommand.Register` の signature だけで、`DebugCommandCatalog`・`SceneState`・asmdef・Addressables・寿命の契約は変えない。規則の高リスクの例（公開 API）に当たるので high にしたが、影響は SampleGame の 1 箇所だけなので、owner が `normal` と判断する余地がある（U3）。high のままなら A2 に A0 だけから別の構成を出すレビューを足し、C' に A・B・C に関与しない系列を予約する。

`AbstractApplicationInitializer.cs` を触るので、program の停止規則（§1.4）により `RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` の Phase B と同時に B を走らせない。S4-V（`tools/` だけ）・S4-3a（主に `tools/` と文書）とは触る file が重ならない見込みで、Unity の実行の時間だけ分ける。

- A3 後の例外承認: なし
- 決定記録: なし

## 3. 責務マップ（A1 初稿）

| file | 責務 | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|---|
| `Runtime/Bootstrap/AbstractApplicationInitializer.cs` | Editor / Player の判定、catalog の作成、Framework の登録 → hook の順、dispatcher の受け渡し | 配線だけ | 既存 | 既存 | `CreateDebugCommandDispatcher` を消し `RegisterDebugCommands` を足す | EditMode | 1486 → 約 1500 |
| `Runtime/SceneSystem/SceneDirector.DebugSnapshot.cs`（新規 partial） | SceneDirector の private な状態を一つの時点の不変の data に写す | Scene の状態の持ち方が変わった時 | SceneDirector（initializer が所有、Play 1 回） | SceneSystem の内部だけ | internal | EditMode 単体 | 0 → 約 60 |
| `Runtime/SceneSystem/Diagnostics/SceneDirectorDebugSnapshot.cs`（新規） | 写しの型と出力の `[Serializable]` DTO | 出力の項目が変わった時（D6 で足すだけ） | 値 | なし | internal | 上と同じ | 0 → 約 60 |
| `Runtime/SceneSystem/Diagnostics/ScenesDebugCommand.cs`（新規） | `osm.scenes` の handler、読めない時の文言、DTO の JSON 化 | 出力の形・文言が変わった時 | catalog（Play 1 回） | SceneSystem、DebugCommands、`JsonUtility` | internal | EditMode 単体（delegate を差し替える） | 0 → 約 70 |
| `unity/Assets/SampleGame/DependOnAll/AppInitializer.cs` | hook の override | 配線だけ | 既存 | 既存 | 変えない | EditMode | 468 → 約 465 |
| `unity/Assets/SampleGame/DependOnAll/DebugCommands/SetActiveDebugCommand.cs` | 登録の引数を減らす | 登録の書き方だけ | 既存 | 既存 | internal | EditMode（既存） | 187 → 約 180 |
| test（`SampleGame/Tests/DebugCommands/SetActiveDebugCommandTests.cs`、Bootstrap の test、新規の Scene の test） | 追従と AC1〜AC8 | — | — | — | — | EditMode | 追従・新規 |
| 文書（§10.5、§5.12、CLI README） | harvest（Phase B が書く） | — | — | — | — | — | 追記・置換 |

- 行数警報: `AbstractApplicationInitializer.cs` は既に 500 行を超える。非分割の理由: 足すのは既存の service の生成の並びに沿った十数行の配線で、写しと出力は新規 file に置く。initializer の分割は program の `RUNTIME_REFACTOR_*` 系の所有。
- 配置の理由: 写しは SceneDirector の private field を読むので SceneDirector の partial に置く。写しの型とコマンドは Scene の診断という別の変更理由を持つので `SceneSystem/Diagnostics/` に分ける。`Runtime/DebugCommands/` は System だけの決まりがあり、`JsonUtility` と SceneSystem を使うこのコマンドは置けない。`DebugSocketServices/` は transport と protocol の責務なので Scene の語彙を入れない。
- 新規 file の path は予算（repo 相対 150 文字）の範囲。最長は `Runtime/SceneSystem/Diagnostics/SceneDirectorDebugSnapshot.cs.meta`（repo 相対で 100 文字）。

## 4. 実装計画（A1 初稿）

- 順序: (1) 写しの型と `CaptureDebugSnapshot` と AC1〜AC4。(2) `ScenesDebugCommand` と AC5。(3) initializer の hook と Player の判定と AC6・AC7。(4) SampleGame の移行と AC8。(5) §10.5（§2.2 の D1〜D9 と L1〜L10 の足し方）、§5.12、CLI README。
- Phase B から Phase A へ差し戻す条件: I1 を守るには SceneDirector の挙動を変える必要がある。Player の判定を差し替えられる形にするのに公開 API が要る。`DebugCommandCatalog` か `DebugSocketServices/` を変える必要がある。asmdef の参照が要る。
- 対象外を維持する方法: `DebugCommandCatalog`・`DebugSocketServices/`・protocol の YAML・`tools/DebugStudio/src` の code を変えない（README だけ）。`debug.set-active` の handler の中身を変えない。`osm.scenes` の出力を §2.1 の 4 項目に留める。

## 5. テストとレビュー計画（A1 初稿）

- 単体テスト: AC1〜AC8（EditMode）。新しい test の class は `unity/Assets/OneStarMaker/Tests/Scene/`（写しとコマンド）と Bootstrap の test の folder に置く。
- 差し戻し中の起点 `-Filter`: `SceneDirector|ScenesDebugCommand|SetActiveDebugCommand|BeforeSceneLoadServiceStaging|FrameworkServiceSet|DebugCommand`（C が根拠付きで変えてよい）。
- 判定必須テスト: 上の新規 test、既存の `SceneDirector*Tests`、`DebugCommandCatalogTests`、`SetActiveDebugCommandTests`、`DebugSocket*Tests`、`DebugCommandOfflineTests`（catalog を変えないことの確認）、最終の全 EditMode 回帰。
- 全 EditMode 回帰の適用除外: なし。
- `tools/DebugStudio` の .NET テスト: code を変えないので判定必須にしない。
- 操作・実行時の条件の検証経路（AC9。実行担当は判定 C 用に開いた session。S3 の §5.4 と同じ環境の作り方）:
  - V0: clean な検証用 checkout を GO 候補 head に detach し、その `unity/` を自分で開いた Editor 6000.6.9f1。DebugStudio の App・CLI を Release で build し、App を `--listen` で起動して PID を記録する。
  - V1: SampleGame の Title から Play を始め、`hierarchy` が `hasSnapshot: true` になるまで読み直す（上限 60 秒）。`send --command osm.scenes --payload '{}'` の stdout / stderr / exit を記録し、同じ frame の近くで eval により `ContainsScene` / `GetSceneState` を読んで突き合わせる（E1）。eval は読むだけで、何も作らない・書かない。
  - V2: 同じ読み取りを 5 回続け、各回の前後で eval の読み取りと `hierarchy` が変わらないことを記録する（I1）。
  - V3: SampleGame の操作で 1 回 Scene を切り替える（具体の操作と切り替え先は A3 までに SampleGame の Title の画面の操作から決める。未決）。Stable の後に読んで `history` と `canGoBack` を記録する（E2）。
  - V4: S3 の §5.4 と同じ手順で `debug.set-active` を false → true と送り、各回の `inspect` を記録する（E3）。
  - V5: Play を止めた後に `osm.scenes` を送って、結果不明（接続の失敗）になることを記録する。WPF に通常の終了を要求し、Scene が clean か、tracked の変更が無いかを記録する。
  - 保存: 各段のコマンド、開始・終了の UTC、stdout / stderr / exit、eval の戻り値。Git に入れず、[レビュー証拠](../../.agents/skills/osm-workflow/references/review-evidence.md#保存先とリモートへの配布) に従って保存する。
- Player の挙動（I4 の (a)）: AC7 の単体テストで閉じ、Player の build は行わない。
- 未知の操作経路の疎通結果: なし（§1.6）。V3 の Scene の切り替えの操作だけは A3 までに決める。
- 人間の判断が必要な条件: なし。
- 高コスト証拠: なし。
- 判定 C の照合 script の方式: なし（V1〜V5 は出力と eval の読み取りの目視の照合で足りる見込み）。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、CI の `offline-checks`。
- A0/A1 主担当: Cloud の計画 session（モデル名は書かない）。
- C' 用に予約した担当: 未定。A0/A1 の系列と A2 の担当の系列、外部の意見（§1.5）を出した系列を外し、A2 の割当と同時に予約する。

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
