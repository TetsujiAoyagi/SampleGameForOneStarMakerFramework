# S0-c Play ボタンからの Editor Play の入口の実証（EDITOR_PLAY_ENTRY_PROOF）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。

- type: `slice`
- status: `A`（A1 初稿、A2 入力版）
- branch: `claude/editor-play-entry-proof`（PR の base は `develop`）
- implementation base commit: `7b28ebad`（`develop` の head、2026-11-09）
- implementation head commit: `未固定`
- risk: `normal`（program §8 の分類。Runtime、asmdef、`SceneState`、Addressables の設定形式を変えない。Editor の Play 移行に割り込むが、状態は Editor process の中に閉じる）
- owner: root（依頼者）
- created: 2026-11-09
- expires: 2026-12-31、または program [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md) の改訂でこのスライスが置き換わった時点
- harvest to: `unity/Assets/Docs/Architecture/18-asset-description.md` §4「宣言からの Play 経路」（Play ボタンの入口、U4 の境界の文言、§1 現況の「誤記」の訂正）、`04-app-startup.md` §4.6・§4.7（Play ボタンが key を補う経路）、program §4.2（use case と予防の表）・§8・§10 U4。`README.md`「試す」は program の完了時
- 関連: program §2・§3.1・§4.1〜§4.4・§6・§8 S0-c 行・§9 G18・§10 U3・U4・M-04・M-08（**本スライスの A0**）、[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)（所有境界の正本）
- Phase A snapshot の取得案内・状態: 該当なし（機械向けの期待値・script・fixture を持たない。C / C' が読む凍結版は A3 凍結版 commit の本書）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: A2 の独立レビュー、その後 A3（主担当と owner が採否を決めて凍結）。
  - 次の作業: 本書と §5 の担当で A2 を行う。
  - 引き継ぐ commit: この票を含む commit（SHA は起動指示で渡す）。
  - 必要な権限: なし（文書のみ）。
  - 停止規則: §2 の停止規則に従う。A3 の凍結と Phase B は owner の判断の後。
  - 引き渡さないもの: 目的レビューへ本書（§1 の「目的」「利用者」の原文以外）を渡さない。A2 の担当どうしの指摘を渡さない。

### 実測の方法

- 名前付き参照と行数は `grep` と行数の読み取り（base `7b28ebad`）。
- **Phase A の疎通（2026-11-09、Unity 6000.6.0f1、Domain Reload 無効の project 既定、`Title` を開いた Editor、file を残さない `eval`）。** 各 1 回の観測で、製品 code は無変更。結果は §5「未知の操作経路の疎通結果」。
- `tools/docs-audit.ps1` は commit 前に実行する。

## 1. 目的と対象外

- 目的: SampleGame を host とする OSM の project で、Unity の Play ボタンを押すだけで、利用者が選んだ RunPurpose の source-only Play か directory-only Play が始まること、利用者が明示した設定を書き換えないこと、始められない時と始めた時にどの設定と content で何が起きたかを Console と観測 UI で説明できることを、Runtime を変更せず、実 Editor の操作の証拠で判定する。
- 利用者: (1) 編集する開発者。source を持ち、普段は Scene を開いて Play で確認する。(2) 検証者。build 済み content の Play を確認する。(3) develop を clone した初見の人。README を読む前に Scene を開いて Play を押す。(4) 後続スライス（S1、S3、S4、S5）の A1 担当。結果を Play 入口の前提として使う。
- 対象外:
  - `unity/Assets/OneStarMaker/Scripts/Runtime/` の変更、Editor snapshot tier、要求時解決、`ContentRequest` との接続（S3）。snapshot の機構 (i) / (ii) の選定（§2「ここでは答えない問い」）。
  - 互換判定（S1）、install されていない publish 済み成果物と LAN からの取得・prefetch（S4）。候補は installed cache にある物だけとする。
  - `Play As` と `Verify Play` の 12 menu の統合・削除、menu root の整理、Delivery 窓の格下げ（S5）。本スライスでは既存の入口を残し、それらが置いた設定を「明示」として扱う。
  - Framework（`OneStarMaker`）への Play 入口の移設（§3「配置」）。
  - source を持たない利用者の source-only Play（M-04: source-only は成立条件を満たす利用者への選択肢で、必ず救済できる手段ではない）。
- 現況（base `7b28ebad`）:
  - `Title` を開いて Play を押すと、`ReadContentDirectoryConfiguration`（`AbstractApplicationInitializer.cs:837`）が key 未指定で例外を投げて止まる。Config の優先順位は JSON → 環境変数 → コマンドライン（同 `:660`）。
  - `ContentDeliveryPlayBridge.ApplyForOnePlay`（`ContentDeliveryPlayBridge.cs:74`）は `isPlayingOrWillChangePlaymode` の間は例外を投げる。`ExitingEditMode` の中からは使えず、取り消しの後に呼ぶ必要がある。
  - ledger は `SessionState` と process 環境変数で、**どちらも Editor process と共に消える**。`18-asset-description.md` §4 は ledger を EditorPrefs と書いているが、code（同 `:29`, `:191`）は `SessionState` である（**誤記**、harvest で直す）。
  - `PlayAsLauncher.CheckLaunchConflicts`・`CheckEmptyLowerLayer`・`CheckAddressablesPlayMode`、`VerifyPlayLauncher.FindReusableScenes`、`PurposeLaunchSettings.PlayAs`・`DirectoryPlay` が前提条件の検査と値の組み立てを既に持つ。
  - 候補の材料: `ContentCacheStore.Inspect()` は installed の entry（contentSet・revision・target・digest・installedUtc）を返し、`ValidateInstalled` が再検証する。contentSet は宣言名と同じ。known-good の pin は呼び手が無い（`MarkKnownGood`、18 §4「未着手の問い」）。
  - `AppInitializer` は Editor の全 Play で動くので、key の無い Play は常に失敗する。バッチの PlayMode テストは個々に環境変数を置いている（`VfxPlayTestFixture.cs:48` 等）。
- UC ごとの利用者の入力:

| UC | 利用者が選ぶ・入れるもの | 機械が決めるもの | 誰がいつ決めるか | 目的からの正当化 |
|---|---|---|---|---|
| UC1 編集者が Play する | なし（Play を押すだけ） | RunPurpose（前回値）、経路（前回値）、config の値、Addressables の Play Mode Script | 実行時に自動導出 | 「Play ボタンを押すだけで」 |
| UC2 RunPurpose を変える（週数回） | RunPurpose 1 つ | 表現・runtime 既定値・初回 Scene | 実行時の操作（永続）。候補は開発時の宣言（`SampleGameBuildPurposes`） | 「利用者が選んだ RunPurpose」。Play のたびに選ばない（program §4.3） |
| UC3 検証者が build 済みで確認する | 経路（Source / Built）を 1 回 | 候補の revision（検証を通った最新の install）、pair の値 | 実行時の操作（永続）。候補は自動導出 | 候補があることは「built を見たい」の意図を示さない（編集中の人に古い content を黙って見せる）ので、機械が導出できない |
| UC4 初見が Play する | なし | RunPurpose の既定（`spring-full`）、経路の既定（Source） | 既定値 | 「Play ボタンを押すだけで」 |
| UC5 止められた | 表示された回復操作を選ぶか、原因を自分で直す | 理由と次の操作 | 実行時の操作 | 「始められない時…説明できる」 |

- 既定の `spring-full`: host が宣言する値で、運用上の選択（担当の判断）。source-only では宣言の差は config だけなので、どの宣言でも Play できる。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: **Runtime を変えずに、Play ボタンを押すだけで RunPurpose の source-only / directory-only Play が始まり、明示設定を書き換えず、失敗と由来を説明できるか。**
- 用語:
  - **明示設定:** `content:runtimeMode`、`content:installedRevisionPath`、`content:manifestSha256` のいずれかが JSON・環境変数・コマンドラインのどれかの層にある（空文字の値を含む）状態。Variant profile、手動の ledger も利用者の明示操作として扱う（program §4.3）。
  - **介入:** 入口が Play を取り消し、1 回の Play で戻す ledger を置いて再 Play すること。
  - **由来:** 本スライスでは Play 全体の経路（source / directory と revision）。Scene ごとの由来は S3。
  - **policy / record:** policy は Play の前に決めた規則と計画（RunPurpose、経路、書いた key、理由）。record はその Play で起きた事実（Play に入ったか、起動の失敗、directory の封じの log、revision）。別の型にする（program §3.1 の 7）。
- 不変条件:
  - **N1 明示設定を書き換えない。** 明示設定があるとき、入口は環境変数・JSON・ledger・Variant profile を変えず、Play 後もその値が残る。崩しうる経路: `ApplyValues` が一回の ledger を `ResetCore` で消す経路、`--content.runtimeMode` のコマンドライン層、環境変数名の大文字小文字、空文字の値（`ContainsKey` は真）、mode 無しで pair だけある暗黙の directory（`AbstractApplicationInitializer.cs:856`）。JSON の検査は disk を読むが、起動が読む bytes の一致は保証しない（`PlayAsLauncher.cs:165` の既存の注意。Script は Fast Mode に揃える）。
  - **N2 未検証の content で directory Play を始めない。** Built の経路は、その Play の介入の中で `ValidateInstalled` を通った revision だけを使い、候補が無い・検証に失敗した時に別の revision や Source へ黙って切り替えない。崩しうる経路: `Inspect` の snapshot と検証の間の削除、receipt 破損、manifest の declaration が宣言と違う cache の entry（`CompareDeclaration`）。
  - **N3 directory-only Play に Editor の Scene を混ぜない（G18）。** 実効 mode が directory になる Play で、起動が再利用しうる Scene が開いていれば、Play を始めない。崩しうる経路: `RegisterAlreadyLoadedScenes` と `PerformUnitySceneLoad` の名前再利用、手動 ledger + `Title` を開いた Play（現行は無検査）、環境変数・コマンドラインだけの directory、pair だけの暗黙 directory、検査と再 Play の間に Scene を開く、Verify Play 以外の一回 ledger。
  - **N4 別の仕組みの Play に割り込まない・無限に割り込まない。** バッチ実行、Test Runner の Play、`Play As` / `Verify Play` が ledger を置いた Play を取り消さない。再 Play は 1 回で、2 回押しても計画は 1 つ。崩しうる経路: `EnterPlaymode` が `ExitingEditMode` を**同期で**発火する（疎通で確認）ので ledger を置く前の呼び出し順、対話の Test Runner（`TestRunnerApi` に static の実行中判定が無い）、`TestObservationBootstrap`、`eval` の Play。
  - **N5 置いた値が残らない。** 介入で置いた値は、Play の終了、Play に至らなかった時のどちらでも元へ戻る。崩しうる経路: 取り消しでは `EnteredEditMode` が来ない（疎通で確認）ので `HandleIdle` の経路に頼る点、`EnterPlaymode` の例外、Play 中の再 compile の domain reload。Editor の異常終了は値が process と共に消えるので残らない（現況）。
  - **N6 開いている Scene と追跡 file を変えない。** 取り消し・再 Play で Scene の保存・閉じる・Dirty の変更をしない（利用者が回復操作を選んだ時を除く）。設定の永続は git 管理外だけに置く。崩しうる経路: Addressables の Play Mode Script の変更先、`EditorUserSettings` の保存先。
  - **N7 Runtime と Player を変えない。** `Scripts/Runtime/` と Player の経路に差分が無く、新しい code は Editor asmdef に閉じる（program I10）。
- 成立条件:
  - **P1** 明示設定が無く経路が Source のとき、Play を押すと `content:runtimeMode=addressables` と宣言の値で Play に入り、起動が key 未指定で失敗しない（U4 (a)）。
  - **P2** 経路が Built で検証を通る候補があり再利用しうる Scene が開いていないとき、Play を押すと directory mode で Play に入る（U4 (b)）。
  - **P3** 始められない時は Play に入らず、理由と次の操作が Console と観測 UI に出る。modal の dialog で止めない（毎回の popup を出さない、program D6）。
  - **P4** Console の 1 行と観測 UI から、policy と record を別々に読める。観測 UI は Game view の使用中にも見える場所に 1 つある。
  - **P5** RunPurpose と経路は Editor を閉じても残り、初回は host の既定になる。
  - **P6** program §4.2 の use case と予防の一覧（§4 に載せる）と、U4 の境界の文言（§4）が揃う。
- 受け入れ条件（単体。詳細は §5）:
  - **AC1**（N1, N3, N4, P1, P2, P3）判定表の全行を `PlayEntryDecisionTests` が実データで確かめる。行は §4「判定の順序」。失敗経路: コマンドライン層だけの明示、空文字、pair だけ、手動 ledger + 再利用 Scene、Variant profile、外の環境変数、候補なし、検証失敗。
  - **AC2**（N4, N5）`PlayEntryTransactionTests` が状態機械を実時間なしで確かめる。失敗経路: 取り消し中の 2 度目の `ExitingEditMode`、再 Play の同期発火、`EnterPlaymode` の例外で ledger を戻す、計画の適用中の例外。
  - **AC3**（N2）候補の選択を一時 cache root の実 file で確かめる（宣言違い・target 違い・破損・最新の選択）。
- 受け入れ条件（実 Editor。経路は §5。各 1 回）:
  - **AC4**（P1, N5, N6）初見: 保存値なし・key なし・`Title` を開いた Editor で Play。合格: Play に入り起動の Error 0 件、Play 後の環境変数が Play 前と同じ、`git status` が空。不合格の例: Play に入ったが `content:runtimeMode` の例外が出た。
  - **AC5**（N6）`Title` を Dirty にして Play。合格: 停止後も Dirty で、`.unity` の bytes が変わらない。
  - **AC6**（N1）外の環境変数 `addressables` を置いて Play。合格: 入口は取り消さず、値が残る。
  - **AC7**（N1, N3）手動 ledger（directory）+ `Title` で Play。合格: Play に入らず ledger の値が変わらず `Title` が開いたまま。空 Scene へ変えて Play すると directory で入り、停止後も手動 ledger が残る。
  - **AC8**（N2, P3）経路 Built・候補なし。合格: Play に入らず、Source へ切り替わらず、理由と次の操作が出る。
  - **AC9**（N2, P2, N5）経路 Built・候補あり・空 Scene。合格: 封じの log と install 済み revision が record に出て、停止後の環境変数が Play 前と同じ。
  - **AC10**（N3）経路 Built・候補あり・`Title` を開いた状態。合格: Play に入らず、`Title` を閉じない。回復操作「空 Scene で Play」を明示して選ぶと AC9 と同じになる。
  - **AC11**（N1）Variant profile を選択して Play。合格: Play に入らず profile が変わらない。
  - **AC12**（N4）`Play As` の menu で Play。合格: 再 Play が起きず 1 回で入る。対話の Test Runner で PlayMode テスト 1 件を実行。合格: 取り消されない。
  - **AC13**（P1）Domain Reload を有効にした設定で AC4 を繰り返し、設定を元に戻す。合格: AC4 と同じ。
  - **AC14**（P1, P4）**実際の Toolbar の Play ボタン**を 1 回クリックして AC4 の結果になり、観測 UI の表示が見える（§5）。
- 受け入れ条件（機械）:
  - **AC15**（N7）`git diff --name-only <base>` に `Scripts/Runtime/`、`.asmdef`、`SceneState` を含まない。`pwsh tools/contract-audit.ps1` が通る。全 EditMode 回帰は XML の failed 0 と root result `Passed`（VFX の Inconclusive で `step.json` は `failed` になるので exit code で判定しない）。
- ここでは答えない問いと所有スライス:
  - 実際の Play ボタン経由で snapshot の load を封じの前に成立させられるか（S0-b の移送）: **S3**。Runtime の initializer の順序を変える必要があり、本スライスの問い（Runtime を変えない）の外にある。
  - snapshot の機構 (i) / (ii) の選定（program U2）と snapshot の渡し方（U3）: **S3 の A1**。本スライスの結果はどちらの機構にも依らない。
  - 実 Editor の異常終了後の snapshot の残骸、実 process 2 つの衝突: **S3**。本スライスの ledger は process と共に消える（§1 現況）ので、ledger については問いが残らない。
  - 非互換の digest / target / schema の判定: **S1**。publish 済み・LAN の候補と prefetch: **S4**。menu の統合と README: **S5**。
  - Framework への移設と SampleGame 無しの最小経路: 宣言を持つ host が 2 つになるか S5 の A1 で判断する（§3「配置」）。
- スライス分割の判断: 契機（独立した未知が複数）を検討した。未知の中心だった「取り消し → 再 Play」は Phase A の疎通で通ったので、残る未知（Test Runner の判定、観測 UI の既定の見え方）は 1 つの最低条件に束ねても別々に判定できる（AC12、AC14）。**分割しない。**
- 判定定義: GO は N1〜N7 と P1〜P6 を、AC1〜AC15 で満たす。NO-GO はいずれかの未達。CONDITIONAL ACCEPT は使わない。
- どうすれば誤って YES を出せるか（反例）: (1) `eval` の `EnterPlaymode` だけを見て Play ボタンの経路とする（AC14 で塞ぐ）。(2) Fast Mode と key が既に揃った Editor で試す（AC4 は保存値なし・key なし）。(3) 「Play に入った」だけで見て、起動の例外を数えない（AC4 は Error 0 件）。(4) 判定表の単体だけで G18 を確かめる（AC7、AC10 は実 Editor）。(5) directory Play で `Title` が開いたまま入り、封じの log だけを見る（AC9 は空 Scene、AC10 で混入を見る）。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。`RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` は Runtime を触るスライスとの同時実行を禁じるが、本スライスは Runtime を触らず、同スライスは A1 未着手である。
- A3 後の例外承認: なし
- 決定記録: なし
- 本文へ転記した実装制約:
  - 依存は Game → Framework。`SampleGame.DependOnAll.Editor` に閉じ、asmdef の参照を足さない。新規・編集する `.cs` の先頭に `#nullable enable`。`record` を使わない。Unity の偽 null に注意する。
  - `ApplyForOnePlay` は `isPlayingOrWillChangePlaymode` の間に呼ばない。取り消しの後の `EditorApplication.update` の tick で呼ぶ。`EnterPlaymode` は呼び出しの中で `ExitingEditMode` を同期で発火するので、ledger を置いてから呼ぶ。
  - テストで `Task.Delay` / `Thread.Sleep` を使わない。tick と状態は注入する。
- 未決事項: §6（A3 判断候補）。

## 3. 責務マップ

すべて `unity/Assets/SampleGame/DependOnAll/Editor/Build/`、asmdef `SampleGame.DependOnAll.Editor`、namespace `SampleGame.DependOnAll.Editor.Build`、公開面は `internal`。

| ファイル | 責務 / 変更理由 | 所有者・寿命 | 依存 | 種別・テスト境界 | 行数 |
|---|---|---|---|---|---|
| `PlayEntryDecision.cs`（新規） | 事実（各層の key、ledger の種類、競合、Scene、候補、選択）から計画（素通り / 介入 / 拒否）を決める。判定の規則が変わる時に変える | 状態なし | `PurposeLaunchSettings` の値のみ。`UnityEditor` なし | Policy。Unity なしの単体 | +180 |
| `PlayEntryTransaction.cs`（新規） | 取り消し → tick → 適用 → 再 Play の順序と 2 度押しの扱い。移行の手順が変わる時に変える | 1 回の介入の間のメモリ。process 内で再 compile されると消えて Play に至らないだけ | 事実の取得と副作用は delegate で注入 | Orchestration。状態機械の単体 | +110 |
| `PlayEntryInterceptor.cs`（新規） | `[InitializeOnLoad]`。`playModeStateChanged` / `update` の配線、Editor の事実の取得、record の採取 | static の配線。Editor 寿命 | `UnityEditor`、既存の検査 | Unity I/O。実 Editor で確認 | +130 |
| `PlayEntryCandidates.cs`（新規） | installed cache から宣言の候補を選び検証する | 呼び出しの間 | `ContentCacheStore`、`CompareDeclaration` | I/O。一時 root の単体 | +80 |
| `PlayEntryRecord.cs`（新規） | 直近の policy と record の保持と Console の 1 行 | `SessionState`（process 寿命） | なし | 永続 + 表示の素材。単体 | +70 |
| `PlayEntrySettings.cs`（新規） | RunPurpose と経路の永続と既定 | `EditorUserSettings`（git 管理外） | `SampleGameBuildPurposes` | 永続。単体 | +40 |
| `PlayEntryWindow.cs`（新規） | Toolbar の要素と詳細窓（選択、policy / record、回復操作） | Editor 寿命 | `MainToolbar` | 表示。実 Editor で確認 | +120 |
| `VerifyPlayLauncher.cs` | `FindReusableLoadedScenes` を `internal` にする | 既存 | 既存 | 既存 | 499、+0 |

- 行数の警報: 新規は各 500 行未満。`VerifyPlayLauncher` は可視性だけを変える。
- 既存の `Play As`・`Verify Play`・`ContentDeliveryPlayBridge` は変更しない。入口はこれらの検査と ledger を呼ぶだけで、規則を複製しない。
- 配置: Play 入口は program §4.4 で Editor adapter の責務で、D2 は Framework 側の任意層を指す。一方、再利用する検査と宣言（`PlayAsLauncher`、`SampleGameBuildPurposes`、`SampleGameBootstrapContentComposer`）はすべて SampleGame にあり、host は 1 つである。この時点で port と asmdef を作らない（§6 D-2）。`PlayEntryDecision` は SampleGame の型に依存しないので、host が増えた時に移せる。

## 4. 実装計画

### 判定の順序（`PlayEntryDecision`、上から最初に当たった行）

| # | 条件 | 結果 |
|---|---|---|
| 1 | バッチ実行、Test Runner の Play | 素通り（record なし） |
| 2 | 一回の ledger がある（`Play As` / `Verify Play` / 入口自身の再 Play） | 素通り |
| 3 | 明示設定があり、実効 mode が directory（手動 ledger、mode `directory`、pair だけ）で、再利用しうる Scene が開いている | 拒否（G18、回復: 空 Scene で Play） |
| 4 | 明示設定がある | 素通り。値は書かない。record に「明示」と出す。値の妥当性は Runtime の fail-closed に任せる |
| 5 | Variant profile、外の環境変数、コマンドラインの制御 key、下位層の非空（`CheckLaunchConflicts` / `CheckEmptyLowerLayer`） | 拒否（理由をそのまま出す） |
| 6 | 経路 Source | 介入（`PurposeLaunchSettings.PlayAs`。Play Mode Script が Fast Mode でなければ揃えて 1 行出す） |
| 7 | 経路 Built、検証を通る候補なし | 拒否（回復: Verify Play で build、経路を Source に変える）。**黙って Source にしない** |
| 8 | 経路 Built、候補あり、再利用しうる Scene が開いている | 拒否（回復: 空 Scene で Play） |
| 9 | 経路 Built、候補あり | 介入（`PurposeLaunchSettings.DirectoryPlay`）。候補が複数なら最新を選び非ブロックの Warning を出す |

候補なし（program の「U4 の mode の書き込みか、ブロックか」）への答え: **経路 Source なら書き込み、経路 Built ならブロック。** 利用者の選んだ経路を入口が変えない。

### 介入の順序（Play 疎通の観測に基づく）

`ExitingEditMode` で計画を作る → 素通り以外は `isPlaying = false`（Unity は `ExitingPlayMode` を同期で出し `EnteredEditMode` は来ない）→ 次の `update` の tick（idle を確認）→ 拒否なら表示して終わり / 介入なら `ApplyForOnePlay` → `EnterPlaymode`（同期で次の `ExitingEditMode`、行 2 で素通り）。tick の時点で事実を取り直し、変わっていれば計画を作り直す。

### use case と予防（program §4.2。M-08 に従い汎用の例から。host の例は括弧）

| use case | 問題 | 最も安い予防 | 所有 |
|---|---|---|---|
| clone 直後で供給が無い | key が無く起動できない | 既定の経路 Source（source-only）。**実装する** | S0-c |
| Built で互換な候補が無い | 始められない | 拒否と明示の回復（Verify Play の build）。**実装する** | S0-c |
| 開いた Scene と directory の衝突（Title を開いて Built） | 名前で source を再利用する | 拒否と「空 Scene で Play」。**実装する** | S0-c |
| `Menu → Game` で次の content が手元に無い（Title の Start と Spring） | ゲーム中に止まる | Play 入口で RunPurpose 相当を先読み | S4 |
| VCS から Scene を更新した（SVN の Workset） | 周辺 content が無い | checkout の hook で Content も取得 | S4、S5 |
| streaming で新しい領域に入った | cold load | 先読みと保温、粒度は実測で | S2、S4 |
| Built の Play を繰り返す | 毎回の再検証の待ち | 測ってから決める（AC9 で所要を記録するだけ。閾値を置かない） | S4 |

### U4 の境界（18 §4 へ harvest する文言の案）

> §4.7 の「known-good の自動適用はしない」は、Runtime が未指定時に cache の pin を見ないことを指す。Editor の Play 入口は別で、(a) どの層にも key が無い時にその Play の間だけ `content:runtimeMode=addressables` を置くことは directory を選ばないので自動適用ではなく、(b) 利用者が Built の経路を選んだ時に、その Play の中で再検証した revision の pair を 1 回の ledger で置き record に残すことは、利用者の選択の下で候補を使う行為である。入口は JSON や既定値を commit せず、明示設定を書き換えない。

- 変更対象: §3 の表。
- 順序: Decision と単体 → Transaction と単体 → Candidates → Settings / Record → Interceptor → Window → 実 Editor のコンパイル確認。
- Phase B から Phase A へ差し戻す条件: Runtime の変更、asmdef の参照の追加（§6 D-3 の採否による）、`ContentDeliveryPlayBridge` の規則の変更が必要になった時。
- 対象外を維持する方法: AC15。Toolbar の要素が既定で見えなければ窓に置き換えるのは B 適応（program D6 が UI の細部を裁量にしている）。

## 5. テストとレビュー計画

- 単体テスト: `unity/Assets/SampleGame/Tests/Editor/Build/` に `PlayEntryDecisionTests`、`PlayEntryTransactionTests`、`PlayEntryCandidatesTests`、`PlayEntryRecordTests`。
- 差し戻し中の起点 `-Filter`: 上の 4 class と既存の `ContentDeliveryPlayBridgeTests`、`PurposeLaunchSettingsTests`、`VerifyPlaySequenceTests`。
- 判定必須テスト: 全 EditMode 回帰（空 filter）、XML の failed 0 と root result `Passed`。
- 全 EditMode 回帰の適用除外: なし
- 統合・Unity テスト: 実 Editor の AC4〜AC14。
- 操作・実行時・目視条件の検証経路:
  - 担当: 実行担当（判定 C）。動作確認は B の出口で同じ手順。環境: `unity/` の Editor（`tools/unity-editor.cmd eval`）、Domain Reload は project 既定（AC13 以外）。
  - 準備: AC8 は既定 cache root に対象宣言の entry が無い状態、AC9・AC10 は `Verify Play` の `spring-planner-whitebox` を 1 回実行した状態。保存値は `EditorUserSettings` の入口の 2 key を消す。
  - 操作: `EditorApplication.ExecuteMenuItem("Edit/Play Mode/Play")`（Play の menu。AC14 だけ実クリック）→ `EnteredPlayMode` または拒否の record を待つ → `EditorApplication.ExitPlaymode()`。
  - 観測: `PlayEntryRecord` の JSON（policy / record）、`Application.logMessageReceived` で採った Error / Exception の件数と封じの log、Play 前後の対象環境変数、ledger の状態、開いている Scene と Dirty、`git status --short`。構造・値の検査で判断する。AC14 だけ画面。
  - 証拠: 条件ごとの観測を 1 file にして evidence bundle へ収録する。C / C' はそれを読む。
- 未知の操作経路の疎通結果（Phase A、各 1 回）:
  - **`ExitingEditMode` の中の `isPlaying = false` で Play は始まらない。** `ExitingPlayMode` が同期で 1 回出て、`EnteredEditMode` は出ず、次の tick で idle に戻った。
  - **取り消しの次の tick の `EnterPlaymode` で Play に入れる。** `EnterPlaymode` の中で `ExitingEditMode` が同期で発火した。`SAMPLEGAME_CONTENT__RUNTIMEMODE=addressables` を tick で置いた `Title` の Play は約 9 秒で `EnteredPlayMode` に至り、12 秒の間に Error / Exception 0 件、停止後に Dirty は false のまま。**実 Runtime と同じ流れで、取り消しを挟んでも key の補完が効く。**
  - **`Edit/Play Mode/Play` の menu も同じ取り消し可能な `ExitingEditMode` を出す**（`Edit/Play` は無効）。
  - `UnityEditor.Toolbars.MainToolbar` と `MainToolbarElementAttribute` は public で存在する。既定で見えるかは未確認（初回確認は B の動作確認）。
  - `TestRunnerApi` に static の実行中判定は無い。対話の Test Runner の検出は未確認。初回確認は Phase B の動作確認、担当は実行担当、AC12。不成立時の対応は §6 D-3。
- 人間の判断が必要な条件: AC14（実クリックと表示の確認）。§6 D-4。
- 高コスト証拠: なし（AC9・AC10 の準備の Content Directory build は Phase C の通常の Build として 1 回行う）。
- 判定 C の照合 script の方式: なし（テスト XML と観測 file を判定 C の担当が読む）。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。
- A0/A1 主担当・モデル・ベンダー: A0 は program（複数担当）。A1 は Claude Opus 5.5（Anthropic）。
- A2 独立レビュー: §7 に記録する。
- A3 統合担当・モデル・採否: 未実施。
- C' 用に予約した担当・モデル・ベンダー: Grok 系（xAI、cursor-agent）を A2 で使わず予約する。
- 独立性の強化条件を満たせない場合の理由: 該当なし（normal）。

## 6. A3 判断候補

A2 の結果を反映して owner の判断事項に更新する。初稿の時点の候補:

- **D-1:** snapshot の real initializer の gate と機構の選定を S3 へ送る（§2）。推奨: 送る。program §8 の問いの「Runtime を変えずに」と両立しないため。
- **D-2:** Play 入口の置き場所を SampleGame の Editor にする（§3）。推奨: SampleGame。D2 は resolver の話で、host が 1 つの間は port を作らない。
- **D-3:** 対話の Test Runner の検出。第一案は scene 名（`InitTestScene`）の判定で asmdef を増やさない。不成立なら `UnityEditor.TestRunner` の参照を足してよいか。推奨: 不成立時に限り事前承認。
- **D-4:** AC14 の担当。実行担当の画面操作ができなければ owner が 1 回確認する、でよいか。

## 7. A2 記録

未実施。
