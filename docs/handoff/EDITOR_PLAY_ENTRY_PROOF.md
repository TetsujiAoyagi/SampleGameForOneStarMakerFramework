# S0-c Play ボタンからの Editor Play の入口の実証（EDITOR_PLAY_ENTRY_PROOF）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。

- type: `slice`
- status: `D`（**head `f87e7806` で判定 C は GO、C' は 2 巡目で指摘なし。マージ判断と Phase D は未実施**。経緯は §9。A3 凍結済み、revision 1。A2 入力版は commit `6164626b`。A3 候補版の commit `1066293a`、`47f40315`、`f6808f6d` は、owner の指示で外部レビュー用にドラフト PR へ公開したもので、Phase A の commit を 2 点に限る規則の、人間の指示による例外）
- branch: `claude/editor-play-entry-proof`（PR の base は `develop`）
- implementation base commit: `7b28ebad`（`develop` の head、2026-11-09）
- implementation head commit: `f87e7806e50edf47eeb43fbbe3b615f42d9cd50a`（B 適応 C-1 の修正後。前の head は `9bcbb3f422d8cda0e41d76f3565b6dcf558308d2`）
- risk: `normal`（program §8 の分類。Runtime、asmdef、`SceneState`、Addressables の設定形式を変えない。Editor の Play 移行に割り込むが、状態は Editor process の中に閉じる）
- owner: root（依頼者）
- created: 2026-11-09
- expires: 2026-12-31、または program [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md) の改訂でこのスライスが置き換わった時点
- harvest to: `unity/Assets/Docs/Architecture/18-asset-description.md` §4「宣言からの Play 経路」（Play ボタンの入口、U4 の境界の文言、§1 現況の「誤記」の訂正）、`04-app-startup.md` §4.6・§4.7（Play ボタンが key を補う経路）、program §4.2（use case と予防の表）・§8・§10 U4。`README.md`「試す」は program の完了時
- 関連: program §2・§3.1・§4.1〜§4.4・§6・§8 S0-c 行・§9 G18・§10 U3・U4・M-04・M-08（**本スライスの A0**）、[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)（所有境界の正本）
- Phase A snapshot の取得案内・状態: 該当なし（機械向けの期待値・script・fixture を持たない。C / C' が読む凍結版は A3 凍結版 commit の本書）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: ローカル固定済み（未昇格）。判定用 bundle と C / C' の所見の bundle の 2 つ。取得案内は同一 PC のローカルの担当が持つ
- C' blind bundle の取得案内・状態: ローカル固定済み（未昇格）。取得案内は同一 PC のローカルの担当が持つ
- 引継ぎ票:
  - 次の Phase・役割: Phase D（owner のマージ判断）。判定 C と C' は head `f87e7806` で完了。凍結した Phase A は revision 1。
  - 次の作業: `review-evidence.md` の保存・引渡し確認、harvest（§0 の harvest to）、§2 決定記録の移送事項（C-2 は S0-d、Warning の文言は S1）を program へ書く、HANDOFF の削除、`workflow-task.ps1 end`。
  - 動作確認の `未実施`: なし（AC9、AC10、AC12、AC14 は判定 C の実行担当が取得した。§9）。
  - 引き継ぐ commit: この票を含む commit（SHA は起動指示で渡す）。
  - 必要な権限: Artifact Storage の取得（同一 PC）。Unity Editor は不要。
  - 停止規則: implementation head を変える修正が必要になったら、`review-evidence.md` の「判定後の head 変更」に従い、判定必須テストと C / C' をやり直す。
  - 引き渡さないもの: なし（C' は完了）。

### 実測の方法

- 名前付き参照と行数は `grep` と行数の読み取り（base `7b28ebad`）。
- **Phase A の疎通（2026-11-09、Unity 6000.6.0f1、Domain Reload 無効の project 既定、`Title` を開いた Editor、file を残さない `eval`）。** 各 1 回の観測で、製品 code は無変更。結果は §5「未知の操作経路の疎通結果」。
- `tools/docs-audit.ps1` は commit 前に実行する。

## 1. 目的と対象外

- 目的: SampleGame を host とする OSM の project で、Unity の Play ボタンを押すだけで、利用者が選んだ RunPurpose の source-only Play か directory-only Play が始まること、利用者が明示した設定を書き換えないこと、始められない時と始めた時にどの設定と content で何が起きたかを Console と観測 UI で説明できることを、Runtime を変更せず、実 Editor の操作の証拠で判定する。
- 利用者: (1) 編集する開発者。source を持ち、普段は Scene を開いて Play で確認する。(2) 検証者。build 済み content の Play を確認する。(3) develop を clone した初見の人。README を読む前に Scene を開いて Play を押す。(4) 後続スライス（S1、S3、S4、S5）の A1 担当。結果を Play 入口の前提として使う。
- 対象外:
  - `unity/Assets/OneStarMaker/Scripts/Runtime/` の変更。Editor snapshot tier と snapshot の機構 (i) / (ii) の選定（S0-d）。要求時解決、`ContentRequest` との接続（S3）。
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
  - **N1 明示設定を書き換えない。** 明示設定があるとき、入口は環境変数・JSON・ledger・Variant profile を変えず、Play 後もその値が残る。崩しうる経路: `ApplyValues` が一回の ledger を `ResetCore` で消す経路、`--content.runtimeMode` のコマンドライン層、環境変数名の大文字小文字、空文字の値（`ContainsKey` は真）、mode 無しで pair だけある暗黙の directory（`AbstractApplicationInitializer.cs:856`）。JSON の検査は disk を読むが、起動が読む bytes の一致は保証しない（`PlayAsLauncher.cs:165` の既存の注意。明示 `addressables` では Script を変えず、§6 D-7 のとおり record と Warning に出す）。
  - **N2 未検証の content で directory Play を始めない。** Built の経路は、その Play の介入の中で `ValidateInstalled` を通った revision だけを使い、候補が無い・検証に失敗した時に別の revision や Source へ黙って切り替えない。崩しうる経路: `Inspect` の snapshot と検証の間の削除、receipt 破損、manifest の declaration が宣言と違う cache の entry（`CompareDeclaration`）。検証の後から起動までの差し替えは入口では防がない。起動は渡された pair を `InstalledRevisionVerifier.Verify` で再検証し（`AbstractApplicationInitializer.cs:888`）、一致しなければ fail-closed になるので、本条件の保証は「検証した pair だけを渡す」までとする。
  - **N3 directory-only Play に Editor の Scene を混ぜない（G18）。** 実効 mode が directory になる Play で、起動が再利用しうる Scene が開いていれば、Play を始めない。崩しうる経路: `RegisterAlreadyLoadedScenes` と `PerformUnitySceneLoad` の名前再利用、手動 ledger + `Title` を開いた Play（現行は無検査）、環境変数・コマンドラインだけの directory、pair だけの暗黙 directory、検査と再 Play の間に Scene を開く、Verify Play 以外の一回 ledger。
  - **N4 別の仕組みの Play に割り込まない・無限に割り込まない。** バッチ実行、Test Runner の PlayMode テストの Play、`Play As` / `Verify Play` が ledger を置いた Play を取り消さない。EditMode テストの `EnterPlayMode` は通常の Play と同じ判定に通す（§6 D-3）。再 Play は 1 回で、2 回押しても計画は 1 つ。崩しうる経路: `EnterPlaymode` が `ExitingEditMode` を**同期で**発火する（疎通で確認）ので ledger を置く前の呼び出し順、対話の Test Runner（`TestRunnerApi` に public の実行中判定が無い）、`TestObservationBootstrap`、`eval` の Play。
  - **N5 置いた値が残らない。** 介入で置いた値は、Play の終了、Play に至らなかった時のどちらでも元へ戻る。崩しうる経路: 取り消しでは `EnteredEditMode` が来ない（疎通で確認）ので `HandleIdle` の経路に頼る点、`EnterPlaymode` の例外、Play 中の再 compile の domain reload。Editor の異常終了は値が process と共に消えるので残らない（現況）。
  - **N6 開いている Scene と追跡 file を変えない。** 取り消し・再 Play で Scene の保存・閉じる・Dirty の変更をしない。利用者が回復操作を選んだ時だけ Scene を置き換え、未保存の内容は Unity の保存確認を通し、拒まれたら置き換えず Play もしない。設定の永続は git 管理外だけに置く。崩しうる経路: `EditorSceneManager.NewScene` は確認なしで未保存を捨てる。Play Mode Script の変更先は `Library/AddressablesConfig.dat`（`ProjectConfigData`）で追跡 file ではない（§6 D-6）。`EditorUserSettings` の保存先。
  - **N7 Runtime と Player を変えない。** `Scripts/Runtime/` と Player の経路に差分が無く、新しい code は Editor asmdef に閉じる（program I10）。
- 成立条件:
  - **P1** 明示設定が無く経路が Source のとき、Play を押すと `content:runtimeMode=addressables` と宣言の値で Play に入り、起動が key 未指定で失敗しない（U4 (a)）。
  - **P2** 経路が Built で検証を通る候補があり再利用しうる Scene が開いていないとき、Play を押すと directory mode で Play に入る（U4 (b)）。
  - **P3** 始められない時は Play に入らず、理由と次の操作が Console と観測 UI に出る。modal の dialog で止めない（毎回の popup を出さない、program D6）。
  - **P4** Console の 1 行と観測 UI から、policy と record を別々に読める。両者は同じ `PlayEntryRecord` から作り、record には観測した事実だけを書く。Runtime を変えずに観測できない項目は `unknown` と出す（推測で埋めない）。record の状態は `EnteredPlay`（Play に入った）、`Blocked`、`StartupFailed`（起動の失敗を観測した）で、initializer の成功は Runtime の log だけでは判定できないので `unknown` と出す。観測 UI は成功・拒否のどちらでも Game view の使用中に見える場所に 1 つある（Toolbar の要素に加えて、focus を奪わない詳細窓を Editor セッションごとに 1 回開く。拒否の時は前面に出す）。明示設定で素通りした Play でも、Variant profile など結果に効く事実は record に出す。
  - **P5** RunPurpose と経路は Editor を閉じても残り、初回は host の既定になる。
  - **P6** program §4.2 の use case と予防の一覧（§4 に載せる）と、U4 の境界の文言（§4）が揃う。
- 受け入れ条件（単体。詳細は §5）:
  - **AC1**（N1, N3, N4, P1, P2, P3）判定表の全行を `PlayEntryDecisionTests` が実データで確かめる。行は §4「判定の順序」。失敗経路: コマンドライン層だけの明示、空文字、pair だけ、手動 ledger + 再利用 Scene、**一回の ledger（directory）+ 再利用 Scene**、Variant profile、外の環境変数、候補なし、検証失敗、明示 `addressables` + 非 Fast Mode（拒否せず Warning）。
  - **AC2**（N4, N5）`PlayEntryTransactionTests` が状態機械を実時間なしで確かめる。失敗経路: 取り消し中の 2 度目の `ExitingEditMode`、再 Play の同期発火（再入で計画が増えず、再入の時点で再利用 Scene があれば拒否する）、`EnterPlaymode` の例外、計画の適用中の例外。どの経路でも入口は `ContentDeliveryPlayBridge.Reset` を呼ばず、次の idle tick の `HandleIdle` が一回 ledger だけを戻し、手動 ledger は変わらない（§4「ledger の戻し方」）。回復操作の保存確認は注入し、保存を拒まれた時に Scene を置き換えず Play しないことを確かめる。
  - **AC3**（N2）候補の選択を一時 cache root の実 file で確かめる（宣言違い・target 違い・破損・最新の選択）。
- 受け入れ条件（実 Editor。経路は §5。各 1 回）:
  - **AC4**（P1, N5, N6）初見: 保存値なし・key なし・`Title` を開いた Editor で Play。合格: record が `EnteredPlay` になり、`EnteredPlayMode` から 15 秒の観測窓で起動の Error / Exception が 0 件（`StartupFailed` にならない）、Play 後の環境変数が Play 前と同じ、`git status` が空。不合格の例: Play に入ったが `content:runtimeMode` の例外が出た。
  - **AC5**（N6）`Title` の `.unity` の SHA-256 を先に採り、`EditorSceneManager.MarkSceneDirty` で Dirty にして Play。合格: 保存の確認 UI が出ず（Unity は Play の移行で確認しない）、停止後も Dirty で、SHA-256 が同じ。確認 UI が出たら不合格として止める。
  - **AC6**（N1, P4）外の環境変数 `addressables` を置いて Play。合格: 入口は取り消さず、値が残る。値が `foo`（不正）の時も書き換えず、Runtime の fail-closed の失敗が record に `StartupFailed` で出る。値が `addressables` で Play Mode Script が Fast Mode でない時は、取り消さず Script も変えず、record と Warning に「既存 build を読みうる」が出る。
  - **AC7**（N1, N3）手動 ledger（directory）+ `Title` で Play。合格: Play に入らず ledger の値が変わらず `Title` が開いたまま。空 Scene へ変えて Play すると directory で入り、停止後も手動 ledger が残る。
  - **AC8**（N2, P3）経路 Built・候補なし。合格: Play に入らず、Source へ切り替わらず、理由と次の操作が出る。
  - **AC9**（N2, P2, N5）経路 Built・候補あり・空 Scene。合格: AC4 と同じ観測窓で `StartupFailed` にならず、封じの log と install 済み revision が record に出て、停止後の環境変数が Play 前と同じ。
  - **AC10**（N3）経路 Built・候補あり・`Title` を開いた状態。合格: Play に入らず、`Title` を閉じない。回復操作「空 Scene で Play」を明示して選ぶと AC9 と同じになる。`Title` が Dirty の時は保存確認が出て、保存を拒むと `Title` が Dirty のまま開いていて Play に入らない（保存確認は modal で `eval` から答えられないので、拒否の経路は AC2 の注入テストで確かめ、実 UI は §6 D-4 の担当が行う）。
  - **AC11**（N1）Variant profile を選択して Play。合格: Play に入らず profile が変わらない。
  - **AC12**（N4）`Play As` の menu で Play。合格: 再 Play が起きず 1 回で入る。対話の Test Runner の window と `TestRunnerApi.Execute` の 2 経路で PlayMode テストを各 1 件実行。合格: どちらも取り消されない。EditMode の `[UnityTest]` で `runtimeMode` を明示して `yield return new EnterPlayMode()` を実行（既存のテストでよい）。合格: Scene 名では Test Runner と判定されないが、行 4 で素通りして取り消されない（検出漏れの反例。§6 D-3）。`InitTestScene` で始まらない通常の Scene の Play は介入され、`InitTestScene` で始まる利用者の Scene は素通りして record に判定の理由が出る（誤除外の反例。D-3 で受け入れた範囲）。1 経路の成功で一般化しない。
  - **AC13**（P1）Domain Reload を有効にした設定で AC4 を繰り返し、設定を元に戻す。合格: AC4 と同じ。
  - **AC14**（P1, P4）**実際の Toolbar の Play ボタン**を 1 回クリックして AC4 の結果になり、Game view を見ている状態で観測 UI の表示が見える。Toolbar の要素が非表示の状態から始めた成功の Play でも、詳細窓で policy / record が見える（§5）。
  - **AC16**（N6, D-6）Play Mode Script を Fast Mode 以外にして、拒否になる条件で Play する。合格: Script が変わらない。経路 Source で Play する。合格: Fast Mode に揃い 1 行出て、起動の Error / Exception が 0 件で、`git status` が空。
- 受け入れ条件（機械）:
  - **AC15**（N7）`git diff --name-only <base>` に `Scripts/Runtime/`、`.asmdef`、`SceneState` を含まない。`pwsh tools/contract-audit.ps1` が通る。全 EditMode 回帰は XML の failed 0 と root result `Passed`（VFX の Inconclusive で `step.json` は `failed` になるので exit code で判定しない）。
- ここでは答えない問いと所有スライス:
  - 実際の Play ボタン経由で snapshot の load を封じの前に成立させられるか（S0-b の移送）: **S0-d `EDITOR_SNAPSHOT_STARTUP_PROOF`**（本スライスの Phase D の後）。封じは `BeforeSceneLoad` の中で同期的に走る（`AbstractApplicationInitializer.cs:245`）ので非同期の load は先に完了できず、`RegisterAlreadyLoadedScenes`（同 `:402`）は `isLoaded` の Scene しか見ない。起動順序の変更か契約の緩和が要る、独立に失敗しうる問いである。
  - snapshot の機構 (i) / (ii) の選定（program U2）と snapshot の渡し方（U3）: **S0-d の A1**。本スライスの結果はどちらの機構にも依らない。
  - 実 Editor の異常終了後の snapshot の残骸、実 process 2 つの衝突: **S0-d**。本スライスの ledger は process と共に消える（§1 現況）ので、ledger については問いが残らない。
  - 非互換の digest / target / schema の判定: **S1**。publish 済み・LAN の候補と prefetch: **S4**。menu の統合と README: **S5**。
  - Framework への移設と SampleGame 無しの最小経路: 宣言を持つ host が 2 つになるか S5 の A1 で判断する（§3「配置」）。
- スライス分割の判断: 契機（独立した未知が複数）を検討した。未知の中心だった「取り消し → 再 Play」は Phase A の疎通で通ったので、残る未知（Test Runner の判定、観測 UI の既定の見え方）は 1 つの最低条件に束ねても別々に判定できる（AC12、AC14）。**分割しない。**
- 判定定義: GO は N1〜N7 と P1〜P6 を、AC1〜AC16 で満たす。NO-GO はいずれかの未達。CONDITIONAL ACCEPT は使わない。
- どうすれば誤って YES を出せるか（反例）: (1) `eval` の `EnterPlaymode` だけを見て Play ボタンの経路とする（AC14 で塞ぐ）。(2) Fast Mode と key が既に揃った Editor で試す（AC4 は保存値なし・key なし）。(3) 「Play に入った」だけで見て、起動の例外を数えない（AC4 は Error 0 件）。(4) 判定表の単体だけで G18 を確かめる（AC7、AC10 は実 Editor）。(5) directory Play で `Title` が開いたまま入り、封じの log だけを見る（AC9 は空 Scene、AC10 で混入を見る）。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。`RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` は Runtime を触るスライスとの同時実行を禁じるが、本スライスは Runtime を触らず、同スライスは A1 未着手である。
- A3 後の例外承認: なし
- 決定記録: v1（owner、2026-11-10、head `f87e7806` に適用。どの条件の意味・合否も変えない）。(1) `PlayEntryDecision` の「Several verified revisions ... using the latest」の Warning の文言（C' の指摘 3）は現スライスで直さず S1 へ移送する。文言は policy の warnings にあり P4 の record の規定の対象ではなく、`record.revision` は実際に使う検証済み revision を示し、飛ばした revision は別の Warning に出るので N2 に反しない。候補の選択は S1 で互換判定に置き換える。(2) §9 の C-2（観測が閉じないまま残る経路）は S0-d へ移送する。発火する順序の実測が無く、AC の合否を変えない。S0-d は同じ取り消しと再 Play の経路を使う。
- 本文へ転記した実装制約:
  - 依存は Game → Framework。`SampleGame.DependOnAll.Editor` に閉じ、asmdef の参照を足さない。新規・編集する `.cs` の先頭に `#nullable enable`。`record` を使わない。Unity の偽 null に注意する。
  - `ApplyForOnePlay` は `isPlayingOrWillChangePlaymode` の間に呼ばない。取り消しの後の `EditorApplication.update` の tick で呼ぶ。`EnterPlaymode` は呼び出しの中で `ExitingEditMode` を同期で発火するので、ledger を置いてから呼ぶ。
  - テストで `Task.Delay` / `Thread.Sleep` を使わない。tick と状態は注入する。
- 未決事項: なし（§6 は A3 で確定）。

## 3. 責務マップ

すべて `unity/Assets/SampleGame/DependOnAll/Editor/Build/`、asmdef `SampleGame.DependOnAll.Editor`、namespace `SampleGame.DependOnAll.Editor.Build`、公開面は `internal`。

`PlayEntryDecision` の入力 `PlayEntryFacts` は値だけの snapshot で、バッチ / Test Runner の真偽、一回 / 手動 ledger の有無、各層（JSON・環境変数・コマンドライン）の 3 key の有無と値、競合の理由文字列、再利用しうる Scene の一覧、Variant profile の有無、Play Mode Script が Fast Mode か、選択（RunPurpose・経路）、検証済み候補（無ければ null、複数かの真偽）。出力 `PlayEntryPlan` は素通り / 介入（環境変数の列と Fast Mode に揃えるかの真偽）/ 拒否（理由と回復操作）。`PlayEntryInterceptor` だけが facts の採取、`isPlaying` の変更、`ApplyForOnePlay`、`EnterPlaymode`、Fast Mode への変更、record の書き込みを行う。

| ファイル | 責務 / 変更理由 | 所有者・寿命 | 依存 | 種別・テスト境界 | 行数 |
|---|---|---|---|---|---|
| `PlayEntryDecision.cs`（新規） | facts から計画（素通り / 介入 / 拒否）を決める。判定の規則が変わる時に変える | 状態なし | `PurposeLaunchSettings` の値のみ。`UnityEditor` なし | Policy。Unity なしの単体 | +180 |
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
| 1 | バッチ実行、Test Runner の PlayMode テストの Play | 素通り（バッチは record なし。Scene 名で Test Runner と判定した時は誤除外を説明できるよう record に理由を残す） |
| 2 | 実効 mode が directory（一回 / 手動の ledger、mode `directory`、pair だけ）で、再利用しうる Scene が開いている | 拒否（G18、回復: 空 Scene で Play）。一回の ledger より先に見るので、行 1 を除くどの経路の directory Play にも同じ保証が掛かる |
| 3 | 一回の ledger がある（`Play As` / `Verify Play` / 入口自身の再 Play） | 素通り |
| 4 | 明示設定がある | 素通り。値は書かない。record に「明示」と出す。値の妥当性は Runtime の fail-closed に任せる。実効 mode が addressables で Play Mode Script が Fast Mode でなければ、拒否せず record と Warning に「既存 build を読みうる」と出す（§6 D-7） |
| 5 | Variant profile、外の環境変数、コマンドラインの制御 key、下位層の非空（`CheckLaunchConflicts` / `CheckEmptyLowerLayer`） | 拒否（理由をそのまま出す） |
| 6 | 経路 Source | 介入（`PurposeLaunchSettings.PlayAs(...).PlacedEnvironment` を置く。Play Mode Script が Fast Mode でなければ、tick の再判定を通った直後に揃えて 1 行出す。永続の変更で、元に戻さない。§6 D-6） |
| 7 | 経路 Built、検証を通る候補なし | 拒否（回復: Verify Play で build、経路を Source に変える）。**黙って Source にしない** |
| 8 | 経路 Built、候補あり、再利用しうる Scene が開いている | 拒否（回復: 空 Scene で Play） |
| 9 | 経路 Built、候補あり | 介入（`PurposeLaunchSettings.DirectoryPlay`）。候補が複数なら最新を選び非ブロックの Warning を出す |

候補なし（program の「U4 の mode の書き込みか、ブロックか」）への答え: **経路 Source なら書き込み、経路 Built ならブロック。** 利用者の選んだ経路を入口が変えない。

行 5 の競合検査を行 4 の明示設定に適用しない理由: 行 5 の検査は「入口がこれから置く値と外の値の衝突」を見るもので、行 4 は入口が何も置かない。明示の設定を競合として拒否すると、利用者の明示操作で Play できなくなる（N1 の逆）。Variant profile など結果に効く事実は、行 4 では拒否せず record に出す（P4）。Play Mode Script はこの衝突の検査ではなく source Play の前提なので、行 4 で別に扱う。

回復操作「空 Scene で Play」: 開いている Scene を置き換える前に `EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo` を通す（現行 `OfferCloseScenes` と同じ）。保存を拒まれたら Scene を残したまま Play しない。この保存確認は利用者が選んだ回復の中の Unity の確認で、P3 が禁じる拒否の modal ではない。

### 介入の順序（Play 疎通の観測に基づく）

`ExitingEditMode` で計画を作る → 素通り以外は `isPlaying = false`（Unity は `ExitingPlayMode` を同期で出し `EnteredEditMode` は来ない）→ 次の `update` の tick（idle を確認）→ 拒否なら表示して終わり / 介入なら `ApplyForOnePlay` → `EnterPlaymode`（同期で次の `ExitingEditMode`。行 2 を通らなければ行 3 で素通り）。tick の時点で同じ `PlayEntryFacts` を取り直して判定し直し、**その結果を最終の判断とする**（`ExitingEditMode` の判定は取り消すかどうかにだけ使う）。再判定、`ApplyForOnePlay`、`EnterPlaymode` は 1 つの同期呼び出しの中で続けて行い、間に他の Editor callback を挟まない。

ledger の戻し方: 入口は `ContentDeliveryPlayBridge.Reset` を呼ばない。`Reset` は移行中に例外を投げ、手動 ledger も消すので、行 2 の拒否（手動 ledger + 開いた Scene を含む）には使えない。拒否、`EnterPlaymode` の例外、適用中の例外のどれでも、`isPlaying = false` または失敗のあと、次の idle tick で既存の `HandleIdle` が一回 ledger だけを戻す。Bridge は変更しない。

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
- 対象外を維持する方法: AC15。Toolbar の要素が既定で見えない時に詳細窓だけで P4 を満たすのは B 適応（program D6 が UI の細部を裁量にしている）。

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
  - 観測: `PlayEntryRecord` の JSON（policy / record）、Error / Exception の件数と封じの log（record が `Application.logMessageReceived` で採る）、Play 前後の対象環境変数、ledger の状態、開いている Scene と Dirty、`git status --short`。構造・値の検査で判断する。AC14 だけ画面。
  - 観測の起点と終了: 1 回の `eval` は Play の移行を待たない（移行中の `eval` は 5 秒で timeout して捨てられる。18 §4「観測の注意」）。製品の `PlayEntryRecord` は `SessionState` に書くので Play と domain reload を跨いで残る。実行担当は、準備の `eval` → Play の `eval` → record の `state` が `EnteredPlay` / `Blocked` / `StartupFailed` のどれかになるまで短い `eval` で読む、の順で行う。`EnteredPlay` の後は 15 秒の観測窓を置いて（疎通の 12 秒の観測より長い固定値）`StartupFailed` への遷移と Error / Exception を読む。窓の中で失敗が無いことは「観測窓で失敗を観測しなかった」の意味に限り、initializer の成功とは記録しない（`unknown`）。起点は Play の `eval` の直前の record の消去、終了は観測窓の後の読み取りと `ExitPlaymode` の後の `EnteredEditMode`。Phase A の疎通はこの形（`SessionState` への記録を後から読む）で動いた。AC14 は自動操作と分ける（§6 D-4）。
  - 証拠: 条件ごとの観測を 1 file にして evidence bundle へ収録する。C / C' はそれを読む。
- 未知の操作経路の疎通結果（Phase A、各 1 回）:
  - **`ExitingEditMode` の中の `isPlaying = false` で Play は始まらない。** `ExitingPlayMode` が同期で 1 回出て、`EnteredEditMode` は出ず、次の tick で idle に戻った。
  - **取り消しの次の tick の `EnterPlaymode` で Play に入れる。** `EnterPlaymode` の中で `ExitingEditMode` が同期で発火した。`SAMPLEGAME_CONTENT__RUNTIMEMODE=addressables` を tick で置いた `Title` の Play は約 9 秒で `EnteredPlayMode` に至り、12 秒の間に Error / Exception 0 件、停止後に Dirty は false のまま。**実 Runtime と同じ流れで、取り消しを挟んでも key の補完が効く。**
  - **`Edit/Play Mode/Play` の menu も同じ取り消し可能な `ExitingEditMode` を出す**（`Edit/Play` は無効）。
  - `UnityEditor.Toolbars.MainToolbar` と `MainToolbarElementAttribute` は public で存在する。既定で見えるかは未確認（初回確認は B の動作確認）。
  - `TestRunnerApi` に public の実行中判定は無い（`IsRunActive` は internal）。PlayMode テストの Scene が `InitTestScene` で始まることは Test Framework の code で確認した（`CreateBootstrapSceneTask`）が、対話の Test Runner での実行は未確認。Unity テストの実行を伴うので、初回確認は Phase C、担当は判定 C の実行担当、確認地点は AC12。不成立時の対応は §6 D-3。
- 人間の判断が必要な条件: AC14（実クリックと表示の確認）と AC10 の保存確認の実 UI。§6 D-4。
- 高コスト証拠: なし（AC9・AC10 の準備の Content Directory build は Phase C の通常の Build として 1 回行う）。
- 判定 C の照合 script の方式: なし（テスト XML と観測 file を判定 C の担当が読む）。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。
- A0/A1 主担当・モデル・ベンダー: A0 は program（複数担当）。A1 は Claude Opus 5.5（Anthropic）。
- A2 独立レビュー: §7 に記録する。
- A3 統合担当・モデル・採否: Claude Opus 5.5（Anthropic）と owner。採否は §6 と §7。owner が 2026-11-09 に凍結を指示した。
- C' 用に予約した担当・モデル・ベンダー: なし。A2 で Grok 系を予約したが、PR の外部レビューで cursor[bot] がその予約を使ったと報告した（担当のモデルは本書の作成者からは確認できない）。normal risk で強化条件は必須でないので、C' の開始時に B / C と異なるモデルの新規 session を選ぶ。PR の外部レビューは C' に数えない。
- 独立性の強化条件を満たせない場合の理由: 該当なし（normal）。

## 6. A3 判断

**D-1〜D-7 は owner が判断し、A3 で凍結した（2026-11-09。D-2、D-4、D-5 は下の推奨どおり）。**

- **D-1 snapshot の実 initializer の gate と機構 (i) / (ii) の選定（S0-b の移送）。決定済み（owner、2026-11-09）: S0-c の直後の別スライス S0-d へ送る。** 「Runtime を変えない」は常時契約でも owner 決定でもなく、revision 2 で「採否を戻せる粒度で前進する」ために置かれた範囲の選択である。S0-c がこれを維持する理由は、snapshot の起動順序が Play 入口とは独立に失敗しうる未知で、S3 は既に high で Runtime 接続・Player 非退行・移送分を抱えているため。S0-d は S0-c の取り消し → 再 Play の経路を使う。
- **D-2 Play 入口の置き場所。** 推奨: **SampleGame の Editor asmdef**（§3）。再利用する検査と宣言がすべて SampleGame にあり host は 1 つなので、今は port と asmdef を作らない。program D2 は resolver の配置の決定と読む。代案: `OneStarMaker` の Editor に新設し host の interface を切る。
- **D-3 対話の Test Runner の除外（A2 F-05、E-02、E-10）。決定済み（owner、2026-11-09）: 開いている Scene 名 `InitTestScene` の前方一致だけで判定し、asmdef を増やさない。** 対象は PlayMode テストで、これは必ず `Assets/InitTestScene<guid>.unity` で始まる。`ICallbacks` は採らない。PlayMode テストの `RunStarted` は Play に入った後に発火するので `ExitingEditMode` の判定に使えず、EditMode の検出のためだけに参照と domain reload をまたぐ実行中フラグを増やさない。EditMode テストの `EnterPlayMode` は Scene 名で検出できないので通常の Play と同じ判定に通す。既存の 8 箇所はどれも `runtimeMode` を明示しており、取り消されない。key を置かない EditMode テストは介入されるが、batch では fail-closed で落ちる形である。directory と再利用 Scene が揃えば行 2 で拒否する。AC12 で不成立なら Phase A の新 revision にする。誤検知（同じ接頭辞の利用者の Scene）は素通りになり、従来の Play と同じ失敗をするだけなので受け入れる。
- **D-4 AC14（実 Toolbar の Play のクリックと観測 UI の表示）と AC10 の保存確認の実 UI の担当。** 推奨: **判定 C の実行担当が画面操作で行い、できなければ owner が 1 回確認して観察記録を受理する**。画像の独立再評価までは求めない。
- **D-5 目的レビューの「Play 前の割り込み・設定の自動補完・新しい観測 UI を作らない」の扱い（§7 P-2）。** 推奨: **不採用**。program §4.3 と U4 (a)（owner の A10 と R3 採用）が Play ボタンでの介入と key の補完と観測 UI を必須にしている。目的と利用者の文言は変えない（曖昧と指摘された語は §2 の用語と §1 の表で定義済み）。文言を変える場合は目的レビューのやり直しが要る。
- **D-6 Play Mode Script の自動切替（E-04）。決定済み（owner、2026-11-09）: 永続の変更を仕様にする。** program §4.3 が黙って直してよい前提条件に挙げている。切り替えるのは tick の再判定を通った直後だけで、拒否の時は変えない。切り替える property は `ActivePlayModeDataBuilderIndex` で、保存先は `Library/AddressablesConfig.dat` で追跡 file ではない（`ActivePlayerDataBuilderIndex` は設定 asset を dirty にするので使わない）。Console に 1 行出す。元に戻さない（`EnterPlaymode` の例外の時も Fast Mode のまま）。AC16。
- **D-7 明示 `addressables` + 非 Fast Mode（E-06）。決定済み（owner、2026-11-09）: 拒否せず記録する。** Use Existing Build を意図した明示設定を止めない。record と Warning に「既存 build を読みうる」を出す。Script も変えない。AC6。

主担当の判断で進めた事項（A3 で異論なし）: 経路 Built の候補は検証を通った最新の install を使う（known-good の pin は呼び手が無い。複数なら Warning。S1 で互換判定に置き換える）。既定の RunPurpose は `spring-full`。§7 F-03 の不採用。§1 の利用者の欄の例示（S1、S3、S4、S5）は S0-d を足さない（文言の変更は目的レビューのやり直しになり、S0-d の A1 担当は本書の結果を program §8 から受け取れる）。

## 7. A2 記録

入力版: commit `6164626b`（`git archive` の本書の内容 sha256 `6fd5f0c2f691b5cb33dbed6079b1fa59415b5ec2aef1b0ca33094d8eff8e9022`）。指摘の相互の受け渡しはしていない。

- **アーキテクチャゲートと不変条件の反証:** Codex（OpenAI、gpt-6-luna）。packet は本書、program、ゲート定義、AGENTS.md、関係する source の写し。**記録の制約:** 担当の書いた出力 file が残らず、同じ session を再開して本文を再出力させた。担当自身が「file を確認できない状態での再構成で、全件の照合は確認できない」と書いている。下の採否は再構成の本文に対するもの。
- **目的レビュー:** 未関与の subagent（Anthropic、Sonnet）。入力は §1 の「目的」と「利用者」の原文だけ（sha256 `4aa74d978b7ec7c6fc7005af1dc382a09e49fad746eebb9ef45eceebb501fa1d`）。

### アーキテクチャゲートの採否（候補）

| ID | 重大度 | 要旨 | 採否 | 理由と反映 |
|---|---|---|---|---|
| F-01 | major | 責務マップに入出力の境界が無い | 採用 | §3 に facts / plan と副作用の所有者を追記 |
| F-02 | major | 再判定の入力と優先が未定 | 採用 | §4 介入の順序。tick の判定が最終 |
| F-03 | major | 明示設定が競合検査を迂回する | **不採用**（一部採用） | 競合検査は入口が置く値との衝突を見るもので、行 4 は何も置かない。拒否すると N1 に反する。結果に効く事実は record に出す（P4、§4） |
| F-04 | major | 検査と再 Play の間に Scene が開く | 採用（文言） | 再判定・適用・`EnterPlaymode` を 1 つの同期呼び出しに固定（§4）。追加の検査は要らない |
| F-05 | blocker | Test Runner の除外が未決 | 採用 | §6 D-3 で第一案と不成立時を A3 で合意する形に |
| F-06 | major | Play を跨ぐ観測の方法が不明 | 採用 | §5 観測の起点と終了 |
| F-07 | major | AC4 が N1 を証明しない | 対応不要 | AC4 の紐付けは初稿から P1, N5, N6 で N1 を含まない |
| F-08 | major | AC5 の基準 bytes が未定 | 採用 | AC5 |
| F-09 | major | 検証後の差し替え | 採用（保証の限定） | N2 に、起動の再検証（`AbstractApplicationInitializer.cs:888`）が担うと明記 |
| F-10 | minor | UI と担当が未定 | 採用 | P4、§6 D-4 |

### 目的レビューの採否（候補）

| ID | 要旨 | 採否 | 理由と反映 |
|---|---|---|---|
| P-1 | 説明は実際に起きたことと一致し、Console と UI が矛盾しない | 採用 | P4 |
| P-2 | Play 前の割り込み、設定の自動補完、新しい観測 UI を作らない | **不採用** | §6 D-5（owner 裁定） |
| P-3 | 不正な設定で始められない場合を含める | 採用 | AC6 |
| P-4 | RunPurpose と content の食い違い | 既に対象 | AC3 の宣言違い |
| P-5 | Runtime から情報が出ない時の扱いが不明 | 採用 | P4 の `unknown` |
| P-6 | 自動ビルドをしない、3 つ目の経路や利用者別の機構を作らない | 既に対象 | §1 対象外、§4 行 7（Build は明示の回復） |
| P-7 | 「選んだ」「明示」「始められない」等の語が曖昧 | 文言は変えない | §2 用語と §1 UC 表で定義。§6 D-5（owner 裁定） |

### PR の外部レビュー（2026-11-09、owner 経由と cursor[bot]、PR #175、対象 commit `1066293a`）

3 件のコメントの F-01〜F-08 を、A2 の番号と区別するため E-01〜E-08 と記す。担当のモデルは本書の作成者からは確認できない。

| ID | 重大度 | 要旨 | 採否 | 反映 |
|---|---|---|---|---|
| E-01 | 重要 | 一回 ledger の素通りが G18 より先 | 採用 | §4 の行 2 と行 3 を入れ替え。AC1、AC2。既存経路に実害は見つからなかったが、保証を各経路の実装に頼らない |
| E-02 | 確認 | Test Runner の Scene 名判定の両方向の反例 | 採用 | AC12、§6 D-3 |
| E-03 | 確認 | Play に入ったことと initializer の成功の区別 | 採用 | P4、AC4、AC9、§5 の観測窓 |
| E-04 | 重要 | Fast Mode の切替の寿命が未規定 | 採用 | §6 D-6、N6、AC16 |
| E-05 | 確認 | Toolbar が非表示の成功時に観測 UI が見えない | 採用 | P4、AC14 |
| E-06 | 重要 | 明示 `addressables` が Play Mode Script を見ない | 採用 | §4 行 4、§6 D-7、AC6 |
| E-07 | 重要 | 回復操作で未保存の Scene を失う | 採用 | §4 回復操作、N6、AC2、AC10 |
| E-08 | 確認 | 行 6 は `PlacedEnvironment` | 採用 | §4 行 6 |

### PR の外部レビュー 2 巡目（2026-11-09、cursor[bot]、PR #175、対象 commit `47f40315`）

| ID | 重大度 | 要旨 | 採否 | 反映 |
|---|---|---|---|---|
| E-09 | 重要 | 行 2 の拒否で ledger を戻す手段が未固定（`Reset` は移行中に例外、手動 ledger も消す） | 採用 | §4「ledger の戻し方」、AC2 |
| E-10 | 確認 | AC12 の検出漏れの反例が実質ない、行 2 と N4 の関係 | 採用。調査で D-3 の `ICallbacks` の代替が PlayMode テストに効かないと判明し、owner が Scene 名の判定だけに決定 | §6 D-3、N4、§4 行 1・行 2、§5、AC12 |
| E-11 | 確認 | N1 の括弧が D-7 と矛盾、切り替える property | 採用 | N1、§6 D-6 |

### PR の最終確認（2026-11-09、owner 経由、PR #175、対象 commit `f6808f6d`）

判定は A3 凍結 GO、追加の blocker なし。計画の凍結可であり機能の成立ではない、AC12・AC14・AC16 を含む証拠は Phase B / C で採る、という留保が付いた。本文の変更なし。目的と利用者の欄は A2 入力版から変えていないので、目的レビューのやり直しは要らない。

## 8. Phase B 実装結果

- 実装: §3 の 7 file（`PlayEntryDecision` 222 行、`PlayEntryTransaction` 135、`PlayEntryInterceptor` 174、`PlayEntryCandidates` 102、`PlayEntryRecord` 192、`PlayEntrySettings` 36、`PlayEntryWindow` 164）と `VerifyPlayLauncher.FindReusableLoadedScenes` の `internal` 化。単体テスト 4 class 71 件。Runtime、asmdef、`SceneState`、`ContentDeliveryPlayBridge` は変更なし。
- HANDOFF との差（いずれも B 適応。契約・責務配置は変えない）:
  - **候補の検証失敗の扱い。** 新しい revision から順に検証し、最初に通ったものを使う。検証に失敗して飛ばした revision は Warning に名前を出す。N2 の「黙って別の revision へ切り替えない」と UC3 の「検証を通った最新の install」の両方を満たす読み。全滅なら行 7 の拒否。
  - **一回の ledger を見た行 2 の拒否だけは `ExitingEditMode` で確定する。** §4 は tick の判定を最終とするが、次の tick までに `HandleIdle` が ledger を戻すと前提が消え、directory の Play が別の source Play に化ける。再入の非素通りも同じく確定し、再 Play を増やさない（N3、N4、AC2）。
  - **候補の content の検証は tick の最終判定だけで行う。** `Verify` は content ツリー全体を見る。`ExitingEditMode` の判定は取り消すかどうかにしか使わず、経路 Built は行 7〜9 のどれでも取り消すので結果が変わらない。
  - 詳細窓を開き直すために menu `Tools/OSM/Play Entry` を足した。Toolbar の要素は既定で表示されないことがあるため（§4「対象外を維持する方法」、下の動作確認）。
  - 行数は §3 の見積もりより Decision が +42、Interceptor が +44、Record が +122、Window が +44 多い。Record は policy / record の 2 型と観測の状態遷移を持つ分。各 500 行未満。
- B 適応（発見 C 1 巡目の C-1、commit `f87e7806`）: `AlignFastMode` で `AddressableAssetSettingsDefaultObject.Settings ?? throw` を `Settings` の取得と `== null` の判定に変えた。`Settings` は `UnityEngine.Object` で、`??` は破棄された object を null と見なさないため、AGENTS.md の偽 null 規則に違反していた。契約・責務・Runtime は変えない。
- 未実行: Unity の単体テストと全 EditMode 回帰（Phase C）。Build（AC9・AC10 の準備）も未実行。
- 機械検査: `pwsh tools/contract-audit.ps1` は `f87e7806` で errors=0 / warnings=0。`git diff --name-only 7b28ebad HEAD` に `Scripts/Runtime/`、`.asmdef`、`SceneState` を含まない（修正前の確認。修正は `PlayEntryInterceptor.cs` の 1 箇所）。
- 実 Editor のコンパイル: `9bcbb3f4` の時点では成功（`scriptCompilationFailed=false`）。**`f87e7806` の修正後は、B の session では未確認だった（発見 C 2 巡目で成功を確認。§9）。** B の session の確認では、`tools/unity-editor.cmd` の起動（`unity open` の Unity 6000.6.0f1）で Editor が起動し、ライセンス接続までは進んだものの、以後 8 分以上ログが進まず、メインウィンドウも出ず、`status` の接続も得られなかった。起動したこの Editor（PID 12992）は、Scene を編集していないため強制停止した。原因は未調査（既知の終了 stall とは別の起動の停止。再調査は 1 回に限る方針で、本記録では原因を特定しない）。次の担当は、Editor の起動が正常に進む時にだけ compile 確認を行い、結果を §8 に追記する。
- implementation head commit: §0 の欄
- Phase B 担当・モデル・ベンダー: Claude Opus 5.5（Anthropic）

### 動作確認

判定の入力にしない（B の session の観測）。合否は書かない。

- 対象 head: `9bcbb3f422d8cda0e41d76f3565b6dcf558308d2`
- 実行担当・環境: Phase B の session。Windows、Unity 6000.6.0f1、Domain Reload は project 既定（AC13 以外）。`tools/unity-editor.cmd eval` で §5 の手順（`SessionState` の record を消す → `Edit/Play Mode/Play` → record を読む → 15 秒 → `ExitPlaymode`）。保存値の 2 key は開始時に消した。
- 開始時の環境: この Editor は `UserSettings` に Variant profile（Production）が選択済みで、そのままの Play は行 5 で拒否された（`variantProfile=True`、AC11 と同じ）。AC4〜AC8、AC13、AC16 の間だけ選択を外し、最後に元へ戻した。既定 cache root の `spring-full` は空。
- 生の観測:

| 条件 | 操作 | 観測 |
|---|---|---|
| AC4 | Title・key なし | `Intervene` / `SourceRoute`、`EnteredPlay`、15 秒後も Error 0・Exception 0、`initializer=unknown`。停止後に環境変数は null、ledger なし、`git status` 空 |
| AC5 | Title を Dirty で Play | SHA-256 は Play の前後で同じ。停止後 `dirty=True`。確認 UI は eval の応答が返り続けたので出ていない |
| AC6 addressables | 外の環境変数 | `PassThrough` / `ExplicitSetting`、`EnteredPlay`、値は残る、ledger なし |
| AC6 `foo` | 外の環境変数 | `PassThrough`、`StartupFailed`（Exception 1、Error 1、`content:runtimeMode must be addressables or directory.`）、値は `foo` のまま |
| AC6 非 Fast | `Use Existing Build` + addressables | `PassThrough`、Warning に「既存 build を読みうる」。Script は変わらない。この環境は未 build なので Addressables が Play を拒否し（Error 1）、`StartupFailed` で Play に入らず |
| AC7 Title | 手動 ledger（`directory`、擬似の pair）+ Title | `Reject` / `DirectoryWithReusableScenes`、回復 `EmptyScene`、Play に入らず ledger と値は不変、Title のまま |
| AC7 空 Scene | 同じ ledger + 空 Scene | `PassThrough` / `ExplicitSetting`。pair が擬似なので Runtime が拒否（`StartupFailed`）。停止後も手動 ledger が残る。実 content の directory Play は未観測 |
| AC8 | `spring-whitebox` / Built、候補なし | `Reject` / `NoVerifiedCandidate`、回復 `VerifyPlayBuild`・`SwitchToSource`。Play に入らず、経路は Built のまま |
| AC11 | Production profile 選択 | `Reject` / `ConflictingState`、`variantProfile=True`、profile は不変 |
| AC13 | Domain Reload 有効 | `EnteredPlay`、15 秒後も Error 0・Exception 0、停止後は環境変数 null。設定は既定に戻した |
| AC16 拒否 | `Use Existing Build` + Built・候補なし | `Blocked`、Script は `Use Existing Build` のまま |
| AC16 Source | `Use Existing Build` + Source | Fast Mode に揃い `[PlayEntry] Addressables Play Mode Script set to 'Use Asset Database'` が 1 行、`EnteredPlay`、Error 0・Exception 0、`git status` 空 |
| Toolbar | Main Toolbar の visual tree を検索 | 要素は tree に見つからない（既定では表示されない）。詳細窓は `Blocked` の時に開き（utility window、docked=False）、前面表示 |

- 検証経路の期待と違う観測と、B への差し戻し: なし。AC6 非 Fast は Addressables の前提で Play に入らなかった点だけが §5 の想定より手前で止まった。AC13 の Editor 設定の保存で `unity/ProjectSettings/EditorSettings.asset` が再シリアライズされて差分が出たので（値の変更ではなく欄の増減）、`git restore` で戻した。実行担当は AC13 の後に同じ確認が要る。
- 保存先・状態: 未保存（B の session の観測で判定の入力にしない。生の出力は session の一時領域のみ）。
- `未実施` の条件と理由・担当: AC9・AC10（Content Directory の Build が要る。判定 C の実行担当）、AC12（Unity テストの実行が要る。判定 C の実行担当）、AC14（実クリックと表示の確認。§6 D-4 の判定 C の実行担当、できなければ owner）。

## 9. 発見 C 記録（1 巡目）

- 種別: 発見 C（GO 判定ではない。C' は起動しない）。対象: implementation base `7b28ebad`、implementation head `9bcbb3f422d8cda0e41d76f3565b6dcf558308d2`。
- 担当・モデル・ベンダー: Claude Sonnet 5.5（Anthropic）。B の担当（Claude Opus 5.5）と別モデル。
- 構造適合（§3 の責務マップと照合）: 7 file は表どおりの責務・配置・依存で、各 500 行未満（B の説明のとおり +42〜+122 の超過は Record の 2 型と観測の遷移など）。`PlayEntryDecision` は `UnityEditor` を持たず、副作用は `PlayEntryInterceptor` だけが持つ。asmdef、Runtime、`SceneState`、`ContentDeliveryPlayBridge` に差分はない。`git diff --name-only 7b28ebad 9bcbb3f4` に `Scripts/Runtime/`、`.asmdef`、`SceneState` を含まない（AC15 の差分条件）。
- 失敗経路の読解（不変条件）: N1〜N5 は、判定表（行 1〜9）、状態機械（取り消し中の 2 度目、再入、一回 ledger + 再利用 Scene の即時確定、`EnterPlaymode` と適用の例外、`Reset` を呼ばず `HandleIdle` に任せる）が §4 のとおり実装されている。経路ごとに対応する単体テストがある。N6 は取り消しで Scene に触れず、回復操作だけが保存確認を通す。
- 実行したテスト: `pwsh tools/run-tests.ps1 -Filter` に §5 の起点 7 class（`PlayEntryDecisionTests`、`PlayEntryTransactionTests`、`PlayEntryCandidatesTests`、`PlayEntryRecordTests`、`ContentDeliveryPlayBridgeTests`、`PurposeLaunchSettingsTests`、`VerifyPlaySequenceTests`）を指定した。結果は XML で 113 件 passed、failed 0、inconclusive 0（`PlayEntry*` 4 class は計 75 件。§8 の「71 件」は実測と違う）。`contract-audit` は errors=0 / warnings=0、`docs-audit` は errors=0。
- 違反根拠を伴う現在の問いを阻害する指摘（B 適応）:
  - **C-1 `AlignFastMode` が `UnityEngine.Object` に `??` を使っている。** `PlayEntryInterceptor.AlignFastMode` の `AddressableAssetSettingsDefaultObject.Settings ?? throw new InvalidOperationException(...)`。違反する契約は `AGENTS.md`「Unity の偽 null を忘れない」（破棄されうる `UnityEngine.Object` に `?.` / `??` / `is null` を使わず `== null` で判定する）で、静的な契約違反なので失敗した条件と一次証拠で足りる。`Settings` は `ScriptableObject` で、`PlayEntryInterceptor.CollectFacts` や `PlayAsLauncher.CheckAddressablesPlayMode` は `== null` で判定しており、同じ型の扱いが揃っていない。実害は小さい（破棄された `Settings` では次行の `DataBuilders` が例外になり、`OnTick` の `catch` で `Blocked` になる）。修正は `var settings = ...Settings; if (settings == null) throw ...;` の 1 箇所。B 適応で足り、Phase A は再開しない。
- 後続・判断へ移送する確認事項（blocker にしない。凍結済みのどの条件にも違反しないため）:
  - **C-2 観測が閉じないまま残る経路。** `BeginPlanned` で `observing=true` にした後、`EnteredPlayMode` に至らず `EnteredEditMode` も来ない失敗（例: 取り消しが効いて Play が始まらない）では、観測が開いたままになり、後の無関係な Error が前の Play の record を `StartupFailed` に書き換えうる（`enteredPlayTime` が 0 の間は観測窓の上限が掛からない）。P4 の「観測した事実だけを書く」に照らすと弱いが、AC4・AC6 の合否と、B の動作確認で得た観測には影響しない。次の Play の `BeginPlanned` で record は置き換わる。直すかは owner の判断（直すなら B 適応で `Planned` のまま idle へ戻った時に観測を閉じる）。
  - **C-3 N2 の読み方。** 新しい revision の検証に失敗した時、B は次に新しい検証済み revision を使い、Warning に飛ばした revision を出す（§8）。N2 は「黙って別の revision へ切り替えない」で、Warning と record の revision で黙ってはいないので、現行の読みは凍結条件に反しない。ただし「失敗した時は拒否する」という厳しい読みもありうるので、判定 C の担当が AC3 の観測と照らして確認する。
- 発見 C で未確認（判定 C の範囲）: 全 EditMode 回帰（空 filter）、実 Editor の AC9・AC10・AC12・AC14、AC13 後の `EditorSettings.asset` の差分確認（§8 動作確認）、Toolbar と詳細窓の見え方。
- 動作確認の `未実施`（判定 C の完了前に解消）: 変更なし（AC9、AC10、AC12、AC14）。

## 9. 発見 C 記録（2 巡目）

- 種別: 発見 C（GO 判定ではない。C' は起動しない）。対象: implementation base `7b28ebad`、implementation head `f87e7806e50edf47eeb43fbbe3b615f42d9cd50a`。引き継ぎ commit `73fcc6bb6b6976257d3aa0473ce5b32ac221da4f`（worktree の HEAD と一致を確認。`f87e7806` から HEAD までの差分は本書だけ）。
- 担当・モデル・ベンダー: Claude Sonnet 5.5（Anthropic）。1 巡目と同じ。B の担当（Claude Opus 5.5）と別モデル。
- **Editor のコンパイル確認（§8 の未了の解消）。** `unity open` でこの project の Editor を起動し（Unity 6000.6.0f1、PID 33520。起動時のアセットのインポートと 2 回の Domain Reload に数分かかったが進行した）、ready 後に `eval` で確認した: `scriptCompilationFailed=False`、`isCompiling=False`、`SampleGame.DependOnAll` と `SampleGame.DependOnAll.Editor` がロード済み。`unity/Logs/Editor.log` に `error CS` は 0 件。**コンパイルは `f87e7806` で成功。** 開いていた Scene は無題で Dirty なし。Editor は `EditorApplication.Exit` で終了を確認した（自分で起動した Editor のみ）。起動時に Editor が書き換えた `UniversalRenderPipelineGlobalSettings.asset` は本スライスと無関係の再シリアライズなので `git restore` で戻した。
- C-1 の修正の確認: `AlignFastMode` は `Settings` を変数に取り、`settings == null` で判定して例外にする形になった。違反していた AGENTS.md「Unity の偽 null」の指摘は解消。差分は `PlayEntryInterceptor.cs` の 1 箇所（+3 / -2）で、Runtime、asmdef、`SceneState`、`ContentDeliveryPlayBridge` に差分はない。`PlayEntry*.cs` 全 file の `??` / `?.` / `is null` を洗い直し、残りはすべて `UnityEngine.Object` でない型（string、delegate、自前の class）で、偽 null の違反は無い。`CollectFacts` の `addressables == null` / `!= null` は元から正しい。
- 影響経路: `AlignFastMode` は `PlayEntryPorts` から注入され、単体テストは fake で置き換えるため、この修正は単体テストの対象外。実経路は AC16 の Source（§8 動作確認、`9bcbb3f4` で観測済み）で、`Settings` が破棄されていない通常の値では結果が変わらない。破棄された値の時は「`DataBuilders` の例外」から「明示の `InvalidOperationException`」になっただけで、`OnTick` の `catch` による `Blocked` の経路は同じ。
- 実行した検査: `pwsh tools/contract-audit.ps1` は違反なし。`pwsh tools/docs-audit.ps1` は errors=0（警告 2 件は `PLAY_CONTENT_RESOLUTION_PROGRAM.md` の §7(C)・§8(C') の harvest に関する既存の警告で、本スライスの変更によらない）。**単体テストは再実行していない**: 修正は注入された port の中の 1 箇所で、起点 7 class のどれも通らない。1 巡目の 113 件 passed は `9bcbb3f4` の結果で、`f87e7806` で再取得した値ではない。
- 現在の問いを阻害する指摘: **なし。** 新しい blocker も、凍結条件への違反も見つからなかった。
- 引き続き移送する確認事項（1 巡目から変更なし。blocker ではない）: C-2（観測が閉じないまま残る経路。直すかは owner の判断）、C-3（N2 の読み方。判定 C が AC3 の観測と照らす）。
- 発見 C で未確認（判定 C の範囲）: 全 EditMode 回帰（空 filter）、実 Editor の AC9・AC10・AC12・AC14、AC13 後の `EditorSettings.asset` の差分確認、Toolbar と詳細窓の見え方。
- 動作確認の `未実施`（判定 C の完了前に解消）: 変更なし（AC9、AC10、AC12、AC14）。
- 巡の数え方: 1 巡目の差し戻し（C-1）が B と発見 C の間の 1 巡。2 巡目で阻害する指摘が無くなり、往復は収束した（上限 4 巡のうち 1 巡）。

## 9. 判定 C 記録

- 種別: 判定 C。対象: implementation base `7b28ebad`、implementation head `f87e7806e50edf47eeb43fbbe3b615f42d9cd50a`。
- 担当・モデル・ベンダー: Codex CLI、gpt-6-luna（OpenAI）。B（Claude Opus 5.5）・発見 C（Claude Sonnet 5.5）と別モデル。実行担当は判定 C の準備の session と、追加取得の session（どちらも Claude Opus 5.5、Phase C の実行担当で判断はしない）。
- evidence: ローカル固定済み（未昇格）。追加取得の後に同じ head から判定用と C' 用の bundle を作り直した。取得と展開を確認した。
- **判定: GO（§2 の判定定義どおり。例外なし）。** 現在の問いを阻害する指摘は無い。
- 判定の経緯: 8 巡。1〜6 巡目は NO-GO または判定 C 未完了で、理由はいずれも証拠の不足（全 EditMode 回帰の省略、AC4〜AC16 の再取得、AC12・AC13・P5 など）。実装の欠陥は無く、head は変わっていない。7 巡目は「AC12 の PlayMode 2 経路は owner 受容の例外付き GO」だったが、§2 は CONDITIONAL ACCEPT を禁じ、合否を変える裁定は決定記録にできないので、**受容の例外を取り下げ、AC12 の 2 経路を実測した**。8 巡目は例外なしで AC12・AC14 を判定し GO。
- 判定必須テスト: 全 EditMode 回帰（空 filter、`pwsh tools/run-tests.ps1`、`f87e7806`）は total 1735 / passed 1710 / failed 0 / inconclusive 25（VFX）/ skipped 0、root `Passed`。既知の Unity 終了 stall のため `step.json` と終了コードは無い（AC15 は XML で判定する）。起点 filter は 124 件 failed 0。`contract-audit` は errors=0 / warnings=0。`docs-audit` は errors=0。
- 実 Editor の AC4〜AC14・AC16・P5 は `f87e7806` で判定 C の実行担当が取得した。**AC14 は 2026-11-10 に再観察した:** 新しい Editor process の最初の Play で、owner が Game view を前面にして実際の Toolbar の Play を 1 回クリックし、Game view のまま詳細窓の policy と record を読めた（owner の観察記録）。機械の観測: Toolbar に入口の要素なし、詳細窓は 0 → 1、Play 中 26.5 秒の時点で `EnteredPlay` / Error 0 / Exception 0、停止後の環境変数は Play 前と同じ（空）、`git status` は空。
- **AC12 の PlayMode 2 経路（2026-11-10）:** project に PlayMode テストが無いので、一時の asmdef とテスト 1 件を未 commit で置いて実行し、後で消した（`git status` は空）。`TestRunnerApi.Execute` と対話の Test Runner window（owner の実クリック）の両方で、テストが 1 件実行され、record は `TestRunnerScene` / `PassThrough` で取り消されなかった。window 経路は results.xml で total 1 / passed 1。**観測上の注意:** Reload Domain 無効の project では、コンパイルまたは script reload の後の最初の PlayMode 実行だけテストが走り、以降は経路によらず 0 件になった（理由は未調査、本スライスの問いではない）。
- 構造適合: §3 の責務マップどおり（発見 C の確認から head の差分は C-1 の 1 箇所のみ）。
- 後続スライスの入力: C-2（§2 決定記録、S0-d）。C-3 は Warning と record で revision を示すので N2 に反しない読みとし、より厳しい仕様が要るなら S1 の判断とする。判定 C の 8 巡目は API 経路の results.xml が後の実行で上書きされて残っていない点を挙げた（実行の根拠は Editor.log の行）。
- 未確認: Editor の異常系全般は §2 の AC の範囲外。

## 9. Phase C' 記録

- 種別: 最終 C'（高コスト証拠の分類なし）。対象: 判定 C と同じ base / head。blind bundle はローカル固定済み（未昇格）で、1 巡目と 2 巡目で作り直した。
- 担当・モデル・ベンダー: cursor-agent、grok-4.7-high（xAI）。新規 session、B / C と別モデル。独立性: 発見 C・判定 C の結論を含まない blind bundle のみ。normal risk で強化条件は必須でない（A2 と PR の外部レビューの Grok 系の使用は §5 のとおり）。
- **判定: 2 巡目で指摘なし。** 阻害する指摘は無い。
- 1 巡目の指摘と解消:

| 指摘 | 条件 | bucket | 解消 |
|---|---|---|---|
| 1. AC12 の PlayMode 2 経路が未実施 | AC12、N4 | 現在の問いを阻害 | 同じ head で 2 経路を実測（§9 判定 C）。2 巡目で閉じたと確認 |
| 2. AC14 の観察記録と 15 秒・停止後の値が足りない | AC14、P4 | 現在の問いを阻害 | 同じ head で再観察。2 巡目で閉じたと確認 |
| 3. Warning の「verified」「latest」が検証失敗の経路で事実と合わない | P4 の説明の範囲 | 後続スライスの入力 | 決定記録 v1 で S1 へ移送。2 巡目で裁定への異議なし |

- 巡の数え方: C' の指摘による往復 1 巡（上限 3 巡）。実装の変更は無い。
- 残存リスク: 上の C-2 と Warning の文言。
- 監査できなかった範囲: program §4.2 の未変更本文、`PurposeLaunchSettings` と Runtime の initializer の本体、全 EditMode の `unity.log` 全体、Editor 画面そのもの。
- C と C' の突き合わせ: AC12 は事実で一致（判定 C の 7 巡目は受容、C' は blocker）し、実測で解消した。AC14 は判定 C の 7 巡目が「満たす」、C' が不足で、C' が正しかった（再観察で解消）。Warning の文言は C' だけが、C-2 は発見 C と判定 C だけが挙げた。
