# DebugStudio S4a — Framework と Game が一つの口にデバッグコマンドを登録し、CLI から一覧できるようにする

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `A`（A0 と A1 初稿。A2 は未実施、A3 は未凍結。**実装キューではない**）
- branch: `claude/magical-gauss-7rycuj`（Cloud の計画 session の branch。PR の base は `develop`。PR は未作成）
- implementation base commit: `07cc2c975b63cf5b668130a1c45490dd3e77c5f0`（`origin/develop` の head、2026-11-25。本書の実測は全てこの base）
- implementation head commit: `未固定`
- A3 凍結記録: `未凍結`
- risk: `high`（公開 API を変える。理由は §2.6）
- owner: root（依頼者）
- created: 2026-11-25
- expires: 2026-12-31、または A3 の凍結で置き換わった時点。期限時は owner が更新・廃止を判断する
- harvest to: `unity/Assets/Docs/Architecture/12-telemetry.md` §10.5（コマンドの口の契約。今の「incoming control の任意 catalog」の節を置き換える）、`tools/DebugStudio/src/DebugStudio.Cli/README.md`（`osm.commands` の送り方と出力の読み方。`debug.set-active` の節の登録の説明）
- 後続: [DEBUGSTUDIO_S4B_FRAMEWORK_STATE.md](DEBUGSTUDIO_S4B_FRAMEWORK_STATE.md)（この口に Scene の状態を読む `osm.scenes` を載せる。本スライスの Phase D の後に B を始める）
- Phase A snapshot の取得案内・状態: 未生成（A3 で凍結版を commit し、その SHA を tree の外で渡す）
- Phase B result snapshot / evidence bundle / C' blind bundle の取得案内・状態: 未生成
- task: 未開始（`tools/workflow-task.ps1 start` は凍結の時に行う）
- 引継ぎ票:
  - 次の Phase・役割: Phase A の主担当（owner の確認 → A2 → A3）。
  - 次の作業: (1) owner が §1.9 の未決事項を確認する。(2) A1 を確定し、A2 の独立レビューを同じ入力版で行う。高リスクなので、目的レビュー・アーキテクチャゲートに加えて、A0（§1）だけを読んで別の構成を出すレビューを 1 件置く（§5.1）。(3) A3 で採否を記録し、Draft PR で外部計画レビューを受けてから凍結する。
  - 読む範囲: 既定（[SKILL.md](../../.agents/skills/osm-workflow/SKILL.md#作業ごとの読み先) の表。Phase A は全節）。
  - 必要な権限: なし（文書のみ）。
  - 停止規則: owner の確認の前に A2 を始めない。凍結の記録が無いまま Phase B を始めない。
  - 引き渡さないもの: なし。

### 実測の方法

- base `07cc2c97` の code と文書を `grep`・`wc -l`・行範囲の読み取りで確かめた。Unity も .NET も起動していない。
- 本書の作成環境（Cloud の Linux container）に `pwsh`・`dotnet`・Unity が無く、`tools/docs-audit.ps1` と `DebugCommandOfflineTests` は未実行。link 先の存在は `git ls-files` で手で照合した。

## 1. 目的と対象外

### 1.1 目的と利用者（A0 で書いた。A2 の目的レビューへ渡す原文の候補）

- 目的: Framework と Game のどちらも、同じ一つの口にデバッグコマンドを登録でき、DebugStudio の CLI から、今送れるコマンドの名前・説明・読み取りか書き込みか・payload の例を一覧で読めるようにする。Player に書き込みのコマンドが出ないことは、各コマンドの登録の書き方ではなく口が保証する。
- 利用者: (a) デバッグコマンドを足す Framework と Game の開発者（AI エージェントを含む）。(b) SampleGame を Editor で Play し、DebugStudio WPF を起動したまま CLI でコマンドを送る開発者と AI エージェント。
- 目的・利用者の文言の出どころ: owner の 2026-11-25 の指示「これを機に framework 側のコマンドの口考えてもいいのでは」。同日の質問への回答で owner が決めた: (1) 口と Scene の状態の読み取りを 2 つのスライスに分ける（本書と S4b）。(2) 既存の `CreateDebugCommandDispatcher()` の override は新しい hook へ移して廃止する。(3) Scene 寿命のコマンドは含めず、後から足せる余地だけを残す。

### 1.2 対象外

- Scene 寿命のコマンド（Scene がいる間だけ登録され unload で外れる）。余地は残すが作らない（§2.5）。
- Framework の状態を読むコマンド（`osm.scenes` など）。S4b が本書の口に載せる。
- 組込みの `ping` / `runtime-diagnostics` の経路の変更。transport の層で main thread を待たずに応答する理由があるので router に残す。一覧には固定の項目として載せる（§2.1 の 3）。
- payload の解析の共通化（各コマンドの JSON の読み方）、payload の schema の検査。
- WPF の表示、protocol・wire の変更、CLI の新しいサブコマンド。既存の `send` に載せる。
- Player の build と実機での確認。Player の挙動は単体テストで閉じる（§5）。
- `debug.set-active` の挙動（payload・結果・拒否の文言）の変更。登録の書き方だけを新しい口へ移す。
- S2 HANDOFF（`DEBUGSTUDIO_S2_SIMPLE_A3_CANDIDATE.md`）の Phase D。

### 1.3 現況（base `07cc2c97`）

**口の構成:**

- `AbstractApplicationInitializer.CreateDebugSocketService(AppConfig)`（`Runtime/Bootstrap/AbstractApplicationInitializer.cs:1428〜1437`、protected virtual）は、`debugSocket:enabled` が false なら null を返し、true なら `new DebugSocketService(options, CreateDebugCommandDispatcher())` を返す。BeforeSceneLoad の中（:299〜302）で呼ばれる。
- `CreateDebugCommandDispatcher()`（:1443、protected virtual）の既定は `NullDebugCommandDispatcher.Instance`。**口はこの 1 本で、Game が override すると dispatcher を丸ごと作る。Framework が自分のコマンドを足す口は無い。**
- SampleGame の `AppInitializer.CreateDebugCommandDispatcher()`（`unity/Assets/SampleGame/DependOnAll/AppInitializer.cs:230〜240`）は自分の `DebugCommandCatalog` を作り、`SetActiveDebugCommand.Register(catalog, Application.isEditor, () => DebugSocket)` が true なら `CatalogDebugCommandDispatcher` を、false なら base を返す。
- `SetActiveDebugCommand.Register`（`unity/Assets/SampleGame/DependOnAll/DebugCommands/SetActiveDebugCommand.cs:45〜68`、187 行）は `isEditor` が false なら登録せず false を返す。**Player に書き込みを出さない規則は、このコマンドの登録の書き方が持っている。**
- `DebugSocketService` の constructor は dispatcher を `MainThreadDebugCommandDispatcher`（internal、52 行）で包む。router（`DebugSocketServices/Protocol/DebugSocketInboundMessageRouter.cs`、277 行）は組込みの `ping` / `debugsocket.ping` と `runtime-diagnostics` / `debugsocket.runtime-diagnostics` を main thread に切り替えずに処理し（`Commands/DebugSocketBuiltInCommandHandler.cs`、81 行）、それ以外は capability の交渉の後に dispatcher へ渡す。

**catalog（`Runtime/DebugCommands/`）:**

- `DebugCommandCatalog`（public sealed、75 行）: `Register(string name, DebugCommandHandler handler)`、`TryExecute(string name, string? payloadJson, out DebugCommandResult result)`、`Count`。名前は Ordinal の完全一致。重複は `InvalidOperationException`。未登録は固定の失敗（`Debug command is not registered.`）。名前・説明・種別などの情報は持たない。**登録済みの名前を列挙する口は無い。**
- `DebugCommandHandler`（public delegate、`DebugCommandResult (string payloadJson)`）、`DebugCommandResult`（public readonly struct、37 行。`Ok(message, payloadJson)` / `Fail(message)`）。
- `CatalogDebugCommandDispatcher`（public sealed、64 行）と `NullDebugCommandDispatcher`（public sealed、38 行）は `Runtime/DebugSocketServices/`。
- `DebugCommandCatalog.cs` と `DebugCommandResult.cs` は .NET の offline テスト `tools/DebugCommandOfflineTests`（CI `.github/workflows/offline-checks.yml` の `dotnet-offline-tests`、windows-latest）に直接 link されて Unity なしで compile される。同じテストが `Runtime/DebugCommands/` の全 `.cs` に `ScriptMachine`・`DebugStudio`・`SampleGame`・`record ` の文字列が無いことを検査し、`TryExecute` が allocate しないことも測る。
- 契約は [12-telemetry.md](../../unity/Assets/Docs/Architecture/12-telemetry.md) §10.5「incoming control の任意 catalog」: catalog は opt-in、登録は初期化の時だけ、利用中の登録と並行アクセスは非対応、closure は App 寿命の依存だけを持ち Scene 所有の object の強参照の登録は非対応、`TryExecute` は呼出し側の thread で同期実行、Framework は書き込み口を持たない、catalog は System だけに依存。

**利用者:**

- `CreateDebugCommandDispatcher` を override するのは SampleGame の `AppInitializer` だけ。`CatalogDebugCommandDispatcher` / `NullDebugCommandDispatcher` / `DebugCommandCatalog` を使うのは、他に Framework の test（`Tests/DebugCommands/DebugCommandCatalogTests.cs` 189 行、`Tests/Foundation/DebugSocketServiceHierarchyLookupTests.cs`）、SampleGame の test（`SampleGame/Tests/DebugCommands/SetActiveDebugCommandTests.cs` 359 行）、`tools/DebugCommandOfflineTests/Program.cs`。
- CLI の利用者が送れるコマンドを知る手段は README だけ。

### 1.4 常時契約と既知の制約（本文へ転記）

- 依存は Game → Framework の一方向。asmdef の参照と package を追加しない（このスライスは要らない）。
- `Runtime/DebugCommands/` の file は System だけに依存させる（offline テストが Unity なしで compile する）。この folder の file に `ScriptMachine`・`DebugStudio`・`SampleGame`・`record ` の文字列を書かない（コメントを含む）。
- Unity 側の C# で `record` を使わない。新規・編集する Unity 側の `.cs` の先頭に `#nullable enable`。
- 破棄されうる `UnityEngine.Object` は `== null` / `!= null` で判定する。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。
- Editor のコードを Runtime の asmdef に置かない。
- path は `tools/path-budget.json` の予算（repo 相対 150 文字）に従う。
- 参照 0 の API を削除の理由にしない。本スライスが消すのは、owner が「新しい hook へ移して廃止」と決めた置き換えの残骸（`CreateDebugCommandDispatcher`）だけ。`NullDebugCommandDispatcher` と `CatalogDebugCommandDispatcher` は test が使っていて置き換えの残骸と確かめていないので消さない。
- Unity テスト・Build は Phase C。Phase B は Editor での compile 確認と offline テストまで。Unity 終了時の native stall は `tools/Harness/README.md` の停止規則に従う。
- S2 / S3 の契約（`--listen` の待受け、`send` / `hierarchy` / `inspect` の出力と exit、`debug.set-active` の payload・結果・拒否の文言、Player では書き込みのコマンドを登録しないこと）を壊さない。
- program の停止規則: Runtime を触るスライスの Phase B を `RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` の Phase B と同時に走らせない。本スライスは `AbstractApplicationInitializer.cs` の DebugSocket の生成の箇所（:1428〜1444）を触るので当たる。
- 人間が開いた Editor を強制終了しない。共有の Editor を触らない。並行の S4-V・S4-3a の Unity の実行と時間を分ける。

### 1.5 原資料

- S3 の HANDOFF（commit `d0e4c03c` の親の `docs/handoff/DEBUGSTUDIO_S3.md`）§2.1: `CreateDebugCommandDispatcher` の override と `SetActiveDebugCommand.Register` の配線、Player で登録しない理由。§1.5: 非 bool の `active` が拒否されない実測。本スライスはこの挙動を変えない。

### 1.6 A0 確認

未実施。`send` → WPF → Runtime の catalog → main thread → `CommandResult` の経路は S3 の判定 C（2026-11-23）で成立している。本スライスは同じ経路の catalog の作り方を変え、新しい名前のコマンドを 1 つ足すだけなので、未知の操作経路は無いと見る。A2 が否定したら A3 の前に S3 の §1.6 と同じ形の小さな確認を行う。

### 1.7 UC ごとの利用者の入力

| UC | 利用者が選ぶ・入れるもの | 機械が決めるもの | 誰がいつ | 目的からの正当化 |
|---|---|---|---|---|
| UC1 コマンドを足す（開発者） | 名前、一行の説明、読み取りか書き込みか、使える場（Editor だけ / Editor と Player）、payload の例、handler | 今の実行の形（Editor か Player か）で登録するかどうか。書き込みを Player に出さないこと。`osm.` の名前の予約 | 開発時の宣言 | 「同じ一つの口に登録でき」「一覧で読める」。説明・種別・例は一覧に出す物そのもの。使える場は「Player に書き込みが出ないことを口が保証する」ための宣言 |
| UC2 送れるコマンドを知る | コマンド名 `osm.commands`（README の固定値）、payload `{}` | 一覧の中身（登録された物と組込みの 2 つ） | 実行時の操作 | 「今送れるコマンドの…一覧で読める」 |
| UC3 コマンドを送る | 既存どおり（`send --command <name> --payload <json>`） | 既存どおり | 実行時の操作 | 既存の経路を変えない |

A1 自己点検: UC1 の宣言の 5 項目は、どれも一覧に出すか、口が保証する規則の判定に使う。目的から正当化できない項目は無い。Player か Editor かは機械が決め、開発者は書かない（今の `isEditor` 引数を無くす）。

### 1.8 スライスの分割の検討（A1 の終わり）

- 契機: 「登録の口（hook と catalog の宣言）」と「一覧のコマンド」は独立して変わる理由を持つ。
- 判断: 分けない。一覧は口が持つ宣言をそのまま出す物で、口だけでは利用者が観測できる成立条件が作れない（Editor の Play で確かめる物が無い）。どちらも同じ Editor の Play の 1 回で判定でき、独立して失敗しうる未知は無い。
- 分けたもの: Scene の状態の読み取りは S4b に分けた（owner 決定、2026-11-25）。

### 1.9 未決事項（owner の確認待ち）

- **U1 `osm.commands` を Player でも使えるようにするか。** A1 は「Editor だけ」で書いた。Player の挙動を今と同じに保ち、Player の実機の確認を要らなくするため。一覧は読み取りで害は無いので、Player でも使える方が筋は良い。Player でも使えるようにするなら、Player の build での確認が判定に入る（S4-V の基盤が使えるようになってからが安い）。
- **U2 宣言の項目。** A1 の案は名前・一行の説明・種別（読み取り / 書き込み）・使える場・payload の例の 5 つ。足すなら A3 まで（例: 結果の payload の例、所有者の名前）。
- **U3 宣言の違反の扱い。** 書き込みで「Editor と Player」を宣言した登録、Game からの `osm.` の名前の登録は、初期化の時に例外にする案で書いた（§2.1 の 2）。BeforeSceneLoad の中なので、起動が止まる。Editor で同じ code が先に走るので、Player だけで初めて落ちることは無いと見ている。黙って登録しない案もある。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: SampleGame の通常の Editor の Play 1 回の中で、Framework と Game が同じ口に宣言付きでコマンドを登録し、CLI の `send --command osm.commands` が登録された全てのコマンドと組込みの 2 つを宣言どおりに一覧し、一覧に載った名前が全て受け付けられ、`debug.set-active` が S3 と同じに動き、Player の構成では書き込みのコマンドが一つも登録されないか。
- 次スライスへ進む最低条件: §2.2 の不変条件 I1〜I4 と成立条件 E1〜E3 を同じ GO 候補 head で満たす。

### 2.1 統合設計（A1 初稿。A2 の対象）

1. **宣言（`Runtime/DebugCommands/`、System だけ）**
   - 新規 `DebugCommandDescriptor`（public sealed class。`record` を使わない）: `Name`、`Summary`、`Access`（新規 enum `DebugCommandAccess`: `Read` / `Write`）、`Availability`（新規 enum `DebugCommandAvailability`: `EditorOnly` / `EditorAndPlayer`）、`PayloadExample`。constructor で空の名前・空の説明を拒否する。
2. **catalog（`DebugCommandCatalog` を改める）**
   - constructor に今の実行の形（`isEditor`）を受け取る。Unity に依存しないため、`Application.isEditor` は呼出し側（initializer）が読んで渡す。
   - `Register(DebugCommandDescriptor descriptor, DebugCommandHandler handler)`（public）: 名前が `osm.` で始まれば `ArgumentException`（Framework の予約）。`Write` かつ `EditorAndPlayer` なら `ArgumentException`（Player に書き込みを出さない）。`EditorOnly` で今が Player なら登録せず false を返す。登録したら true。重複は今どおり `InvalidOperationException`。
   - `RegisterFramework(...)`（internal）: `osm.` の名前を登録する Framework 用の入口。他の規則は同じ。
   - `Describe()`（public）: 登録順の宣言の読み取り専用の列。
   - 旧 `Register(string, DebugCommandHandler)` は消す。宣言の無い登録は一覧に出せず、口の保証（Player に書き込みを出さない）を迂回できるため。利用者は SampleGame と test だけ（§1.3）。
   - `TryExecute` と未登録の固定の失敗、allocate しないことは変えない。
3. **一覧のコマンド（`Runtime/DebugCommands/`、System だけ）**
   - 新規 `CommandListDebugCommand`（internal static、仮称）が `osm.commands`（`Read`、`EditorOnly`。U1）を `RegisterFramework` で登録する。payload は無視する（`{}` を README で示す）。
   - 結果: `success=true message=listed`、payload は `{"commands":[{"name","summary","access","availability","payloadExample"}…],"transportBuiltIns":["debugsocket.ping","debugsocket.runtime-diagnostics"]}`。`commands` は `Describe()` の順。JSON は手で書く（System だけ。文字列の escape は既存の `DebugSocketBuiltInCommandHandler.EscapeJsonString` と同じ規則を `DebugCommands/` の中に持つ。DebugSocketServices を参照しない）。
   - 組込みの 2 つは router が処理して catalog に無いので、一覧では別の欄に固定の名前で出す。別名（`ping`・`runtime-diagnostics`）は出さない。
4. **hook（`AbstractApplicationInitializer`）**
   - `CreateDebugCommandDispatcher()` を消し、`protected virtual void RegisterDebugCommands(DebugCommandCatalog catalog)`（既定は何もしない）を足す。
   - `CreateDebugSocketService` の中で、enabled のときだけ: catalog を `Application.isEditor` で作る → Framework のコマンドを登録する → `RegisterDebugCommands(catalog)` を呼ぶ → `new CatalogDebugCommandDispatcher(catalog)` を `DebugSocketService` に渡す。enabled が false なら今どおり null を返し、hook を呼ばない。`CreateDebugSocketService` の signature は変えない。
   - catalog は initializer の field に持たない（DebugSocketService が dispatcher を通して持ち、service の Dispose で手放す）。Play をまたいで残る物を作らない。
   - Scene 寿命の余地: 登録も実行も main thread の上で起きるので、後から登録の解除の handle を足しても並行の問題は生じない。本スライスは `Register` の戻り値を bool にしておき、後続が handle を返す overload を足せる形にする（§2.5）。
5. **SampleGame の移行**
   - `AppInitializer`: `CreateDebugCommandDispatcher` の override を `RegisterDebugCommands` の override に置き換え、`SetActiveDebugCommand.Register(catalog, () => DebugSocket)` を呼ぶ。
   - `SetActiveDebugCommand.Register`: `isEditor` の引数を消し、宣言（`debug.set-active`、`Write`、`EditorOnly`、payload の例 `{"nodeId":2,"active":false}`）で登録する。Player で登録しないのは catalog の規則が行う。handler の中身は変えない。

### 2.2 不変条件と成立条件

**不変条件**

- **I1 Player の構成で書き込みのコマンドが一つも登録されない。** `isEditor=false` の catalog に `Write` の宣言が残らない。どの登録の書き方でも迂回できない。
  - 主張を崩しうる経路: (a) 宣言の無い登録の入口が残る（旧 `Register(string, …)` を消す）。(b) `Write` + `EditorAndPlayer` の宣言（例外にする）。(c) Game が `CreateDebugSocketService` を override して自分の dispatcher を渡す（`CreateDebugSocketService` は protected virtual のまま。これは塞がない。§10.5 と README に「この override は口の保証の外」と書く）。(d) `RegisterFramework` が Game から呼べる（internal。`InternalsVisibleTo` は test と Editor の asmdef だけで、SampleGame の Runtime の asmdef は含まない。`Runtime/AssemblyInfo.cs` で確かめる）。
  - 探索した境界: `Runtime/DebugCommands/*`、`DebugSocketServices/*Dispatcher*.cs`、`AbstractApplicationInitializer.cs` の DebugSocket の生成の箇所、SampleGame の `AppInitializer.cs` と `SetActiveDebugCommand.cs`、`Runtime/AssemblyInfo.cs`。他の asmdef（`OneStarMaker.Debug` など）からの catalog の利用は未確認（grep で 0 件、A2 で確かめる）。
- **I2 既存のコマンドの挙動を変えない。** `debug.set-active` の payload・結果・拒否の文言・exit、組込みの `ping` / `runtime-diagnostics`、`debugSocket:enabled=false` で DebugSocket を作らないことと hook を呼ばないこと、Player の構成で `debug.set-active` が未登録の固定の失敗を返すこと。
  - 崩しうる経路: handler の closure の作り方が変わる（`() => DebugSocket` を登録の時に読む）、`MainThreadDebugCommandDispatcher` の包み方が変わる、catalog の未登録の文言が変わる。
- **I3 一覧と実際に受け付ける物が一致する。** `osm.commands` の `commands` の全ての名前は `TryExecute` で解決でき、解決できる名前は全て一覧にある。今が Player なら `EditorOnly` の物は一覧にも無い。
  - 崩しうる経路: 登録しなかった（false を返した）宣言を `Describe()` に入れる、登録の後に一覧を cache する。
- **I4 Framework の名前を Game が上書き・遮蔽できない。** Game の `Register` は `osm.` の名前を例外にし、Framework の登録は Game の hook より先に行う。

**成立条件**

- **E1** 通常の SampleGame の Editor の Play で、`hasSnapshot: true` になった後に `send --command osm.commands --payload '{}'` が exit 0 で、`commands` が `osm.commands` と `debug.set-active` を宣言どおりに含み、`transportBuiltIns` が 2 つの名前を含む。
- **E2** 同じ Play で S3 の手順（`hierarchy` → `debug.set-active` false → `inspect` → true → `inspect`）が S3 と同じ出力・exit で動く。
- **E3** `isEditor=false` で作った catalog に SampleGame と Framework の登録を同じ順で行うと、`Describe()` に `Write` の物が無く、`osm.commands` は登録されず、`debug.set-active` の `TryExecute` は未登録の固定の失敗を返す（単体テストで閉じる。U1 で Player でも使うと決めたら Player の確認を足す）。

### 2.3 受け入れ条件（A1 初稿）

| AC | 検証する条件 | 内容 | 方法 |
|---|---|---|---|
| AC1 | I1（失敗経路）・I4 | `Write` + `EditorAndPlayer` の宣言、`osm.` で始まる Game の登録、空の名前・空の説明がそれぞれ例外になり、catalog に何も残らない | offline テスト（`DebugCommandOfflineTests`）と EditMode |
| AC2 | I1・E3 | `isEditor=false` の catalog で `EditorOnly` の登録が false を返し、`Describe()` と `TryExecute` のどちらにも現れない | offline テスト |
| AC3 | I3 | 登録の後の `Describe()` の名前の集合と `TryExecute` で解決できる名前の集合が一致し、順は登録順 | offline テスト |
| AC4 | E1・I3 | `osm.commands` の JSON が `Describe()` と一致し、説明・例の中の `"`・`\`・改行が escape される | offline テスト |
| AC5 | I2 | `TryExecute` が今どおり allocate しない（既存の `Catalog_ExecuteDoesNotAllocate` を宣言付きの登録で通す） | offline テスト |
| AC6 | I2・I4 | initializer の配線: enabled のとき Framework の登録 → hook の順で、hook の中の例外は BeforeSceneLoad の失敗として今の起動の失敗の経路に乗る。enabled=false で hook が呼ばれない | EditMode（既存 `BeforeSceneLoadServiceStagingTests` の流儀） |
| AC7 | I2 | `SetActiveDebugCommandTests` を新しい登録で通す（payload・結果・拒否の文言は変えない） | EditMode |
| AC8 | E1・E2・I2 | 実 Editor の Play と Release の WPF・CLI で §5 の V1〜V3 | 実機（Phase C） |

### 2.4 判定定義

- GO: I1〜I4 と E1〜E3 を同じ GO 候補 head で満たし、判定必須テスト（§5）が全て通る。
- NO-GO: 上のどれかが満たせない。

### 2.5 ここでは答えない問いと所有する後続スライス

- Scene の状態の読み取り（`osm.scenes`）: [DEBUGSTUDIO_S4B_FRAMEWORK_STATE.md](DEBUGSTUDIO_S4B_FRAMEWORK_STATE.md)。
- Scene 寿命のコマンド（登録の解除の handle、Scene の unload での自動の解除）: DebugStudio S5 以降（未起票）。本スライスは `Register` の戻り値と main thread の上の登録という形で余地だけを残す。
- payload の解析の共通化と schema の検査、結果の payload の宣言: DebugStudio S5 以降（未起票）。
- Player での `osm.commands`（U1 で「Editor だけ」のまま決まった場合）、WPF での一覧の表示: DebugStudio S5 以降（未起票）。
- `CreateDebugSocketService` の override で口を迂回できること（I1 の経路 (c)）を塞ぐか: 未所有。今は §10.5 と README に書くだけ。
- S3 から移送された項目（DontDestroyOnLoad の表示、Play をまたぐ NodeId の識別、inspect の自己応答保証）: そのまま DebugStudio S5 以降（未起票）。

### 2.6 risk の理由

`high`。公開 API を変える（`AbstractApplicationInitializer.CreateDebugCommandDispatcher` の削除と `RegisterDebugCommands` の追加、`DebugCommandCatalog` の constructor と `Register` の signature の変更、新しい public 型 3 つ）。Game の初期化の経路（BeforeSceneLoad）で例外を投げる規則を足す。`SceneState`・asmdef・Addressables・寿命の契約は変えない。high なので A2 に A0 だけから別の構成を出すレビューを足し、C' に A・B・C に関与しない系列を予約する。

- A3 後の例外承認: なし
- 決定記録: なし

## 3. 責務マップ（A1 初稿）

| file | 責務 | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|---|
| `Runtime/DebugCommands/DebugCommandDescriptor.cs`（新規。2 つの enum を同じ file に置くか別 file にするかは A2 で決める） | コマンドの宣言の値 | 宣言の項目が変わった時 | 値 | System | public | offline テスト | 0 → 約 70 |
| `Runtime/DebugCommands/DebugCommandCatalog.cs` | 宣言付きの登録、口の規則（予約・Player の書き込み・使える場）、実行、宣言の列挙 | 口の規則が変わった時 | DebugSocketService の dispatcher（App の Play 1 回） | System | public（signature を変える） | offline テスト | 75 → 約 140 |
| `Runtime/DebugCommands/CommandListDebugCommand.cs`（新規） | `osm.commands` の登録と一覧の JSON | 一覧の形が変わった時 | 同上 | System | internal | offline テスト | 0 → 約 90 |
| `Runtime/Bootstrap/AbstractApplicationInitializer.cs` | catalog を作り、Framework の登録 → hook の順に呼び、dispatcher を渡す | 配線だけ | 既存 | 既存 | `CreateDebugCommandDispatcher` を消し `RegisterDebugCommands` を足す | EditMode | 1486 → 約 1495 |
| `unity/Assets/SampleGame/DependOnAll/AppInitializer.cs` | hook の override | 配線だけ | 既存 | 既存 | 変えない | EditMode | 468 → 約 465 |
| `unity/Assets/SampleGame/DependOnAll/DebugCommands/SetActiveDebugCommand.cs` | 宣言付きの登録に移す | 登録の書き方だけ | 既存 | 既存 | internal | EditMode（既存） | 187 → 約 185 |
| `tools/DebugCommandOfflineTests/*` | 新しい file の link と AC1〜AC5 | — | — | — | — | offline | `Program.cs` に追記 |
| test（`Tests/DebugCommands/DebugCommandCatalogTests.cs`、`SampleGame/Tests/DebugCommands/SetActiveDebugCommandTests.cs`、`Tests/Foundation/DebugSocketServiceHierarchyLookupTests.cs`） | 新しい signature への追従と AC6・AC7 | — | — | — | — | EditMode | 追従 |
| 文書（§10.5、CLI README） | harvest（Phase B が書く） | — | — | — | — | — | 置換・追記 |

- 行数警報: `AbstractApplicationInitializer.cs` は既に 500 行を超える。非分割の理由: 足すのは既存の service の生成の並びに沿った数行の配線で、規則と一覧は `DebugCommands/` の新規・既存 file に置く。initializer の分割は program の `RUNTIME_REFACTOR_*` 系の所有。
- 配置の理由: 規則は catalog に置く（登録の入口が一つで、Player の書き込みの判定を迂回できない場所）。一覧のコマンドは catalog の宣言だけを読むので System だけで書け、offline テストで閉じる。`DebugSocketServices/` は transport と protocol の責務なので触らない（`CatalogDebugCommandDispatcher` は変えない）。

## 4. 実装計画（A1 初稿）

- 順序: (1) 宣言の型と catalog の規則と AC1〜AC3・AC5（offline テストを先に通す）。(2) `osm.commands` と AC4。(3) initializer の hook と AC6。(4) SampleGame の移行と AC7。(5) §10.5 と CLI README。
- Phase B から Phase A へ差し戻す条件: catalog の規則を System だけで書けない。`CatalogDebugCommandDispatcher` か router を変える必要がある。`InternalsVisibleTo` を変えないと `RegisterFramework` を Framework から呼べない、または Game から呼べてしまう。asmdef の参照が要る。
- 対象外を維持する方法: `DebugSocketServices/` と protocol の YAML と `tools/DebugStudio/src` の code を変えない（README だけ）。`debug.set-active` の handler の中身を変えない。

## 5. テストとレビュー計画（A1 初稿）

- 単体テスト: AC1〜AC5 は `tools/DebugCommandOfflineTests`（Unity なし）。AC6・AC7 は EditMode。
- 差し戻し中の起点: `dotnet run --project tools/DebugCommandOfflineTests`、EditMode の `-Filter` `DebugCommand|SetActiveDebugCommand|BeforeSceneLoadServiceStaging|FrameworkServiceSet|DebugSocketServiceHierarchyLookup`（C が根拠付きで変えてよい）。
- 判定必須テスト: offline テストの全件、上の EditMode、最終の全 EditMode 回帰。
- 全 EditMode 回帰の適用除外: なし。
- `tools/DebugStudio` の .NET テスト: code を変えないので判定必須にしない。
- 操作・実行時の条件の検証経路（AC8。実行担当は判定 C 用に開いた session。S3 の §5.4 と同じ環境の作り方）:
  - V0: clean な検証用 checkout を GO 候補 head に detach し、その `unity/` を自分で開いた Editor 6000.6.9f1。DebugStudio の App・CLI を Release で build し、App を `--listen` で起動して PID を記録する。
  - V1: Play を始め、`hierarchy` が `hasSnapshot: true` になるまで読み直す（上限 60 秒）。`send --command osm.commands --payload '{}'` の stdout / stderr / exit を記録する（E1）。
  - V2: S3 の §5.4 と同じ手順で `debug.set-active` を false → true と送り、各回の `inspect` を記録する（E2）。
  - V3: Play を止め、WPF に通常の終了を要求し、Scene が clean か、tracked の変更が無いかを記録する。
  - 保存: 各段のコマンド、開始・終了の UTC、stdout / stderr / exit。Git に入れず、[レビュー証拠](../../.agents/skills/osm-workflow/references/review-evidence.md#保存先とリモートへの配布) に従って保存する。
- 未知の操作経路の疎通結果: なし（§1.6）。
- 人間の判断が必要な条件: なし。
- 高コスト証拠: なし。
- 判定 C の照合 script の方式: なし（V1〜V3 は出力の目視の照合で足りる見込み）。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、CI の `offline-checks`。
- A0/A1 主担当: Cloud の計画 session（モデル名は書かない）。
- C' 用に予約した担当: 未定。A0/A1 の系列と A2 の担当の系列を外し、A・B・C に関与しない系列を A2 の割当と同時に予約する。

### 5.1 A2 の独立レビューと A3 の採否

未実施。予定: (1) 目的レビュー（§1.1 の目的・利用者の原文だけ）。(2) アーキテクチャゲート（初稿全体）。(3) 高リスクの追加: A0（§1）だけを読んで別の構成を出すレビュー。

## 6. Phase B 実装結果

未到達。

## 7. Phase C

未到達。

## 8. Phase C'

未到達。

## 9. Phase D

未到達。
