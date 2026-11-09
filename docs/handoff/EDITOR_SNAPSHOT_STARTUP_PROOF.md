# S0-d Editor snapshot を実 initializer の起動に載せる実証（EDITOR_SNAPSHOT_STARTUP_PROOF）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。

- type: `slice`
- status: `A`（A1 初稿。**未凍結**。§6 の D-1〜D-10 は owner の判断待ち）
- branch: `claude/editor-snapshot-startup-proof`（PR の base は `develop`）
- implementation base commit: `4167dca8`（`develop` の head、2026-11-10）
- implementation head commit: 未固定
- risk: `high`（program §8 の分類。Runtime の起動順序に Editor 専用の段を足し、`OneStarMaker.Editor` に公開 API を足し、封じの契約の文言を変える。asmdef と `SceneState` は変えない）
- owner: root（依頼者）
- created: 2026-11-10
- expires: 2026-12-31、または program [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md) の改訂でこのスライスが置き換わった時点
- harvest to: `unity/Assets/Docs/Architecture/04-app-startup.md` §4.6・§4.7（snapshot の取り込みの段、封じの対象の文言）、`18-asset-description.md` §4「宣言からの Play 経路」（3 つ目の経路）、program §5.2（機構の選定）・§8・§10 U2・U3
- 関連: program §3・§4.1・§4.3・§5.2・§6 I4・I10・I11・§8 S0-d 行と移送・§9 G18・G21・G26・§10 U2・U3（**本スライスの A0**）、[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)（所有境界の正本）
- Phase A snapshot の取得案内・状態: 該当なし（機械向けの期待値・script・fixture を持たない。C / C' が読む凍結版は A3 凍結版 commit の本書）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: Phase A の A3（owner と主担当）。**凍結していない。**
  - 次の作業: owner が §6 の D-1〜D-10 を判断する。判断を本文へ反映し、目的・利用者の文言が変わったら目的レビューをやり直し、A3 凍結版を commit する。
  - 引き継ぐ commit: この票を含む commit（SHA は起動指示で渡す）。
  - 必要な権限: なし（文書のみ）。Phase B 以降は Unity Editor の起動と操作。
  - 停止規則: D-1 が「採らない」になったら本書の §3・§4 は成り立たない。機構 (ii) へ進む場合は新しい A1 として書き直す。
  - 引き渡さないもの: なし。

### 実測の方法

- 名前付き参照と行数は `grep` と行数の読み取り（base `4167dca8`）。
- **Phase A の疎通（2026-11-10、Unity 6000.6.0f1、Domain Reload 無効の project 既定、file を残さない `eval`、製品 code は無変更、1 回の観測）。** 結果は §5「未知の操作経路の疎通結果」。
- `tools/docs-audit.ps1` は commit 前に実行する。

## 1. 目的と対象外

- 目的: SampleGame を host とする OSM の project で、Play ボタンから build 済み content の Play を始める時に、Editor で開いている Scene を Play 前の複製として同じ Play に載せられること、その Play が開いている Scene 自体と、build 済み content の Play が持つ起動時の保護（Addressables の封じ、失敗時に別の供給元へ切り替えないこと、Player の protected config）を変えないこと、どの Scene が複製から来て誰が解放するかを Console と観測 UI で説明できること、Editor の異常終了の後に複製の残骸が次の Play を妨げないことを、実際の起動処理を通る実 Editor の操作の証拠で判定する。あわせて、複製の作り方 2 案のどちらを製品の候補にするかを決める材料を出す。
- 利用者: (1) 編集する開発者。担当する Scene を開いて編集し、他は build 済み content のまま Play で確認する。(2) 検証者。Scene を開かず、build 済み content だけの Play を確認する。(3) 後続スライス（S1、S3）の A1 担当。結果を snapshot の前提として使う。
- 対象外:
  - 要求時解決と `ContentRequest` への接続、起動後に再要求された Scene を snapshot から読み直すこと、物理的に loaded で未要求の Scene を lifecycle に入れないこと（S3）。
  - 機構 (ii)（一時 Content Directory）の実装と、複数 directory の製品化（`InstallContentDirectory` の単一 install、`ContentRevisionGate` の単一登録）。本書は (ii) に要る変更を列挙するだけにする。
  - 参照先の prefab / material / ScriptableObject の複製（I11 の対象外、G22）。
  - 経路 Source と、既存の経路 Built（Scene を開かない directory だけの Play）の挙動の変更。
  - UICommon を開いたままの Play（従来どおり拒否する。§4）。
  - 互換判定（S1）、publish 済み・LAN の候補（S4）、menu の統合（S5）。
- 現況（base `4167dca8`）:
  - directory mode の封じは `InitializeBeforeSceneLoad` の中で、config の直後に同期で走る（`AbstractApplicationInitializer.cs:238-248`）。入口から封じまで await は無い。**Scene の load は最短でも次のフレームに完了するので、実 initializer では封じより前に snapshot を読み終える手段が無い。** S0-b の「封じの前に読む」は harness が自分で封じを呼んで作った順序だった（program §5.2）。
  - Unity が Play の開始時に読む Scene（開いている Scene、または `EditorSceneManager.playModeStartScene`）は `BeforeSceneLoad` と `AfterSceneLoad` の間に load される。封じは `SceneManager` に触れない（`AssetManagement.cs:74-102`）。
  - `RegisterAlreadyLoadedScenes`（同 `:640-658`）は loaded で `SceneResourceMap` に名前がある Scene を `AddScene` する。由来（Editor か directory か）は見ない。`PerformUnitySceneLoad`（`SceneDirector.Loading.cs:628-658`）は名前で再利用し、`BackendSceneLoaded` を false にする。そういう Scene の unload は `SceneManager.UnloadSceneAsync` を直接呼ぶ（`SceneDirector.Unloading.cs:465-475`）。unload の後に同じ名前を要求すると directory から読む。
  - S0-c の入口は、実効 mode が directory で再利用しうる Scene が開いていれば Play を始めない（`PlayEntryDecision` の `DirectoryWithReusableScenes` と `BuiltWithReusableScenes`、回復は「空 Scene で Play」）。
  - Editor から Runtime へ値を渡す先例は `SceneVariantRuntimeBridge`（Runtime の public static、`OneStarMaker.Editor` の `SceneVariantEditorInjector` が設定する）。`OneStarMaker.Runtime` の internal は `OneStarMaker.Editor` から見え、SampleGame の Editor asmdef からは見えない。
  - protected config の照合は Player だけで走る（同 `:843-852`、`UseRequiredPlayerFileConfiguration`）。
  - Play 入口の ledger と record は `SessionState` と process 環境変数にあり、Editor process と共に消える。disk に残る物は無い。
  - project の Enter Play Mode 設定は Domain Reload 無効・Scene reload 有効（`EditorSettings.asset` の `m_EnterPlayModeOptions: 1`）。
- UC ごとの利用者の入力:

| UC | 利用者が選ぶ・入れるもの | 機械が決めるもの | 誰がいつ決めるか | 目的からの正当化 |
|---|---|---|---|---|
| UC1 編集者が Scene を開いたまま build 済み content で Play する | 経路を 1 回選ぶ（3 つ目の値。§4）。以後は Play を押すだけ | 複製する Scene（開いている全 Scene）、複製の置き場、開始 Scene、解放の時点、初回 Scene を置かないこと | 経路は実行時の操作（永続）。他は自動導出 | 「開いている Scene を…同じ Play に載せられる」。directory だけの Play を確認したい検証者と区別が要り、開いている Scene があることは混ぜたい意図を示さない（program §4.3、R2 F-03） |
| UC2 検証者が Scene を開かずに Play する | なし（既存の経路 Built） | 既存どおり | 既存 | 「起動時の保護…を変えない」 |
| UC3 止められた | 表示された回復を選ぶか原因を直す（Scene を保存する、UICommon を閉じる） | 理由と次の操作 | 実行時の操作 | 「説明できる」 |
| UC4 Editor が異常終了した後に Play する | なし | 残骸の検出と回収 | 自動 | 「残骸が次の Play を妨げない」 |

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: **実際の Play ボタンから、実 initializer の順序の下で、Editor で開いている Scene の複製を directory mode の起動に載せ、封じ・fail-closed・protected config を弱めず、由来と解放の担当を説明できるか。複製の機構 (i) / (ii) のどちらを製品の候補にするか。**
- 用語:
  - **snapshot:** Play の前に、開いている Scene を `SaveScene(saveAsCopy)` で一時 asset に書いた複製（機構 (i)）。
  - **宣言:** 入口が Play の前に固定する snapshot の一覧（論理 identity = Scene 名、元の path と GUID、複製の path と GUID、Dirty、SHA-256）。
  - **取り込み:** 起動が宣言の Scene を Editor の load で読み、宣言と照合する段。`IAssetManagement` を通らない。
  - **混成の経路:** 経路の 3 つ目の値。build 済み content の Play に snapshot を載せる。既存の経路 Built は「directory だけ」のまま。
  - **由来:** Scene ごとの供給元（snapshot / directory）。
- 不変条件:
  - **N1 封じを変えない。** directory mode の Play で、`IAssetManagement` の Addressables の取得経路は config の読み込みの直後から閉じていて、snapshot の有無で封じの時点・対象・例外が変わらない。崩しうる経路: 封じの時点（`AbstractApplicationInitializer.cs:239-248`）を取り込みの後へ動かす実装、取り込みを `IAssetManagement.LoadSceneAsync` で行う実装、封じの許可 key の追加（`AssetManagement.cs:88-101`）。snapshot の Scene の `Awake` が Addressables を呼べば既存どおり例外になる（変えない）。探索した境界: `AssetManagement` と `SealedAssetBackend`、initializer の 2 段。未確認: SampleGame の各 Scene の `Awake` が封じの下で何を呼ぶか（疎通では `Title` で Error 0 件）。
  - **N2 失敗時に別の供給元へ切り替えない。** 宣言した snapshot が 1 つでも読めない、または宣言と一致しない時、起動は失敗で終わり、同名の directory の Scene も元の Scene も代わりに実行されない。崩しうる経路: 取り込みが失敗を握って先へ進むと、`LoadEditorFirstSceneAsync` や後の `AddScene` が `PerformUnitySceneLoad` で同名を directory から読む（`SceneDirector.Loading.cs:644-654`）。宣言が domain reload で失われて「snapshot 無し」に見える。探索した境界: `InitializeAfterSceneLoad` の catch と `CleanupAfterStartupFailureAsync`。
  - **N3 Player と、snapshot を宣言しない Play を変えない。** Player の build に取り込みの code が入らず、protected config の照合（`:843-852`、`PlayerContentConfiguration.cs:24-25`）に差分が無い。経路 Source、経路 Built、バッチ、Test Runner、明示設定の Play の起動順序と結果が base と同じ。`content:` の key を足さない（program U3）。崩しうる経路: `#if UNITY_EDITOR` の外に置いた参照、bridge の残留（前の Play の宣言が次の Play に効く）、複製の path を config に置く実装（config に置くのは §4 の真偽の目印 1 つだけ）。
  - **N4 開いている Scene を変えない（I11）。** Play の前後で、開いている Scene の file の bytes、Dirty、hierarchy、active Scene、並び順が同じ。追跡 file を変えない。崩しうる経路: `saveAsCopy` を付けない `SaveScene`、`playModeStartScene` の戻し忘れ（**疎通で、Play を止めても値が残ると確認**）、複製の import が開いている Scene を reload する、取り消し・起動失敗・Play 中の再 compile の経路で戻しが走らない。
  - **N5 同じ identity を 2 つ実行しない（G21）。** snapshot で載せた identity について、同じ Play の起動で同名の Scene がもう 1 つ load されない。崩しうる経路: `content:firstScene` が snapshot と同名（**疎通で、`Title` の複製と firstScene `Title` は 1 個だけになると確認**。`AddSceneCore` の active の検査、`SceneDirector.Loading.cs:126-137`）、`BootstrapUiCommonLoader` は active の `UICommon` だけを借りる（`BootstrapUiCommonLoader.cs:75-103`）ので additive の UICommon の複製は 2 つになる、開いている Scene に同名が 2 つある。
  - **N6 由来を偽らない。** record は snapshot から載せた Scene を directory 由来と書かず、その逆も書かない。起動の後に snapshot の Scene が unload され、同名が別の供給元から読み直された時は、Warning と record に出る（黙って切り替わらない）。崩しうる経路: `PerformUnitySceneUnload` の後の再要求（現況）、directory から読んだ Scene の `scene.path` は元の asset の path を返す（疎通で確認）ので path だけでは区別できない。
  - **N7 残骸が次の Play に入らず、追跡 file を汚さない。** 複製の置き場は git 管理外で、Play の終了・取り消し・起動の失敗・Editor の異常終了のどの後でも、次の Play の準備より前に回収され、前の Play の複製が別の Play の snapshot として採用されない。崩しうる経路: 異常終了では `SessionState` が消える（disk の記録が要る）、pid の再利用（開始時刻も比べる。S0-b の `TempRootLease`）、`.meta` の残り、回収前の再 Play。
  - **N8 別の仕組みの Play に割り込まない。** S0-c の N4（バッチ、Test Runner、既存の ledger の Play を取り消さない。再 Play は 1 回）を保つ。
- 成立条件:
  - **P1** 混成の経路で `Title` を開いて Play を押すと、directory mode で Play に入り、封じの log があり、`Title` は複製が 1 個だけ実行され、他の Scene（UI、後続）は directory から来る。
  - **P2** Dirty の Scene と、additive に開いた 2 Scene（map に載る Scene と載らない Scene）で P1 が成り立つ。active Scene と並び順は開いていた時と同じになる。
  - **P3** Console の 1 行と観測 UI に、Scene ごとの由来（identity、元の path、Dirty、SHA-256、複製の bytes を Play の後に残していないこと）と、解放の担当（Scene の unload は SceneDirector、複製の file は入口）が出る。
  - **P4** Domain Reload の有効と無効の両方で P1 が成り立つ。
  - **P5** Play 中に Editor の process を強制終了した後、次に Editor を開いて Play を押すと、残骸が回収されて P1 が成り立つ。同じ project を 2 つ目の Unity process で開けないことを実測で確かめる（置き場が project の中にあるので、衝突は Unity の project lock が先に止める）。
  - **P6** 始められない時（保存していない新規 Scene、UICommon が開いている、同名の Scene が 2 つ、Scene reload が無効、複製の失敗）は Play に入らず、理由と次の操作が出る。黙って経路 Built や Source にしない。
  - **P7** 機構の比較（(i) の実測の所要、(ii) に要る製品変更の一覧と S0-b の所要）が program §5.2 に書ける形で揃う。
- 受け入れ条件（単体。詳細は §5）:
  - **AC1**（N4, N5, P6）複製の書き出し: Dirty の Scene の複製が in-memory の内容を持ち、元の file の SHA-256 と Dirty が変わらない。失敗経路: 新規未保存、同名 2 つ、書き込み先が既にある、途中の失敗（書いた物を消す）。
  - **AC2**（N7）置き場の記録: 生きている process の記録は回収しない。死んだ pid、pid が同じで開始時刻が違う記録は回収する。失敗経路: 壊れた記録 file、回収中の削除の失敗（残った物を次回に回収し、今回の Play には使わない）。
  - **AC3**（N2, N5）取り込みの照合（Unity の Scene を使わない純粋な判定）: 宣言と load 済みの一覧の一致。失敗経路: 1 件欠ける、path が違う、同名が 2 つ、宣言に無い同名が既に loaded。
  - **AC4**（N3, N8, P6）判定表: 混成の経路の全行と、既存の行が変わらないこと。失敗経路: 明示の directory + 開いた Scene は従来どおり拒否、経路 Built + 開いた Scene は従来どおり拒否、UICommon、Scene reload 無効。
  - **AC5**（N4, N7, N8）transaction: 複製の準備 → 宣言 → ledger → 再 Play の順序。失敗経路: 準備の例外（Play に入らず、書いた複製と `playModeStartScene` を戻す）、`EnterPlaymode` の例外、再入、Play に至らず idle に戻った時（宣言と複製を回収する）。
  - **AC6**（N6, P3）record: 宣言と取り込みの結果が別の欄に出る。再読込の検出（同名が別の path で load された）で Warning と record が変わる。
- 受け入れ条件（実 Editor。経路は §5。各 1 回）:
  - **AC7**（P1, N1, N5）混成の経路・`Title` を開いて Play。合格: record が `EnteredPlay`、封じの log が record にある、loaded の `Title` が 1 個でその path が複製の置き場の下、`EnteredPlayMode` から 15 秒の観測窓で Error / Exception が 0 件、取り込みの log に `Title` が出る。不合格の例: `Title` が 2 個、`Title` の path が元の asset。
  - **AC8**（N4, N7）AC7 の前に `Title` の `.unity` の SHA-256 を採り、`MarkSceneDirty` で Dirty にする。合格: 停止の後、SHA-256 が同じ、Dirty のまま、active Scene と並びが同じ、`playModeStartScene` が Play の前の値、複製の置き場が無い、環境変数が Play の前と同じ、`git status --short` が空。
  - **AC9**（P2）`Title` と、実行担当が作る保存済みの空 Scene（map に無い）を additive に開いて Play。合格: 両方が複製から load され、`Title` だけが SceneDirector に登録され、active Scene が開いていた時と同じ。
  - **AC10**（N2）再 Play の直前に複製の file を 1 つ消す（実行担当が `eval` で `ExitingEditMode` の handler を足す）。合格: record が `StartupFailed`、`Title` という名前の Scene が loaded に無い、理由に snapshot が出る。不合格の例: directory の `Title` で Play が続く。
  - **AC11**（N6）AC7 の Play の中で、SceneDirector に `Title` の unload と再要求を出す。合格: Warning が 1 行出て、record の `Title` の由来が「snapshot の後 directory」に変わる。
  - **AC12**（P4）Domain Reload を有効にして AC7 と AC8 を繰り返し、設定を元に戻す。
  - **AC13**（P5, N7）AC7 の Play の中で、実行担当が自分で起動した Editor の process を強制終了する。Editor を開き直す。合格: 起動の後に残骸の回収の log が 1 行あり、置き場が無く、`playModeStartScene` が null、`git status --short` が空。続けて AC7 が合格する。別に、Editor を開いたまま同じ project へ 2 つ目の Unity（batch）を起動し、project lock で拒否される log を採る。
  - **AC14**（P6, N3, N8）経路 Built + `Title`（従来どおり拒否、置き場を作らない）、経路 Source + `Title`（従来どおり Play、置き場を作らない）、混成 + 新規未保存の Scene、混成 + UICommon。合格: それぞれの結果と理由が record に出る。
  - **AC15**（P1）**実際の Toolbar の Play ボタン**を 1 回クリックして AC7 の結果になる（§6 D-9）。
- 受け入れ条件（機械）:
  - **AC16**（N3）`git diff --name-only <base>` が §3 の表の file と、その test、`.meta`、`.gitignore`、文書に限られる。`.asmdef` と `SceneState` を含まない。`Scripts/Runtime/` の追加行は `#if UNITY_EDITOR` の中か、全体が `#if UNITY_EDITOR` の file にある。`content:` で始まる新しい key が無い。`pwsh tools/contract-audit.ps1` が通る。全 EditMode 回帰は XML の failed 0 と root result `Passed`（VFX の Inconclusive で `step.json` は `failed` になるので exit code で判定しない）。
  - **AC17**（P7）§4「機構の比較」の表が、AC7・AC9 の実測の所要で埋まる。
- ここでは答えない問いと所有スライス:
  - 起動の後の再要求を snapshot から読み直す、snapshot を resolver の候補として扱う、`ContentRequest` との接続、loaded で未要求の Scene の非参加（G23）: **S3 `RUNTIME_PROVIDER_INTEGRATION`**。
  - 機構 (ii) の製品化と複数 directory: 本スライスが (i) で GO なら不要。NO-GO なら owner へ返し、新しいスライスを切る。
  - 参照先 asset の overlay（G22）: **S2**。
  - 互換判定と候補の選び方: **S1**。経路の UI の整理: **S5**。
  - Framework への Play 入口の移設: S5 の A1（S0-c §3「配置」の判断を引き継ぐ）。
- スライス分割の判断: 契機（高リスクの初回実機スライスで、独立して失敗しうる未知が複数）に当たる。未知は (a) 実 initializer の下の取り込み、(b) Domain Reload 有効の時の宣言の受け渡し、(c) 実 process の異常終了の後の回収、の 3 つ。(a) は Phase A の疎通で中心の部分（Unity が読んだ複製が封じの下で 1 個だけ動く）が通った。(b) と (c) は AC12 と AC13 で別々に判定でき、同じ置き場の記録の code を使う。回収だけを別スライスにすると、残骸を残す code が先に develop に入る。**分割しない。** 規模の見積もりは §3（製品 code 約 +620 行、test 約 +600 行）で、S0-c（約 +730 行）と同じ程度。Phase B で取り込みの段が §3 の配置に収まらなければ A へ戻す。
- 判定定義: GO は N1〜N8 と P1〜P7 を AC1〜AC17 で満たす。GO の時、機構 (i) を製品の候補とする。NO-GO はいずれかの未達。CONDITIONAL ACCEPT は使わない。
- どうすれば誤って YES を出せるか（反例）: (1) 開いている Scene をそのまま Play に持ち込み（Unity 標準の Play）、「Title が動いた」を snapshot の成立とする（AC7 は path が置き場の下であることを見る）。(2) 経路 Source で試す（封じが無い。AC7 は封じの log を要求する）。(3) `eval` の `EnterPlaymode` だけで Play ボタンの経路とする（AC15）。(4) 模擬の lease だけで異常終了を確かめる（AC13 は実 process を止める）。(5) 取り込みの失敗を単体だけで確かめる（AC10 は実 Editor）。(6) 起動の直後だけ見て由来を説明済みとする（AC11）。(7) Domain Reload 無効だけで試す（AC12）。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。`RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` は Runtime を触るスライスとの Phase B の同時実行を禁じる。同スライスは A1 未着手で、本スライスの Phase B の開始時にもう一度確かめる。S3 の Phase B より前に本スライスを完了する（program §8）。
- A3 後の例外承認: なし
- 決定記録: なし
- 本文へ転記した実装制約:
  - 依存は Game → Framework。asmdef の参照を足さない（`OneStarMaker.Editor` は UniTask を参照していないので、bridge の型に UniTask を出さない）。新規・編集する `.cs` の先頭に `#nullable enable`。`record` を使わない。Unity の偽 null に注意する（`SceneAsset`、`Scene` の有効性は `IsValid()`）。
  - Editor の code を Runtime asmdef に置かない。Runtime の側は Editor の型を参照せず、delegate と値だけを受け取る。
  - 公開 API のログは `ILogger<T>`。initializer の既存の `Debug.Log` の目印（封じの 1 行）は変えない。
  - `ApplyForOnePlay` は `isPlayingOrWillChangePlaymode` の間に呼ばない。`EnterPlaymode` は `ExitingEditMode` を同期で発火する。準備・宣言・ledger を置いてから呼ぶ。
  - テストで `Task.Delay` / `Thread.Sleep` を使わない。
  - 置き場の path は `tools/path-budget.json`（repo 相対 150 文字）に収める。
- 未決事項: §6 の D-1〜D-10。

## 3. 責務マップ

Framework の Editor（`unity/Assets/OneStarMaker/Scripts/Editor/PlaySnapshot/`、asmdef `OneStarMaker.Editor`、namespace `OneStarMaker.Editor.PlaySnapshot`）に複製と置き場と宣言を置き、Runtime には取り込みの照合と bridge だけを置く。SampleGame の入口は経路の判定と呼び出しだけを足す。

| ファイル | 責務 / 変更理由 | 所有者・寿命 | 依存 | 種別・テスト境界 | 行数 |
|---|---|---|---|---|---|
| `Runtime/Bootstrap/EditorPlaySnapshotBridge.cs`（新規、全体が `#if UNITY_EDITOR`） | Editor が宣言と load の delegate を置く口。宣言の型（identity、複製の path）。起動が読む形が変わる時に変える | static。1 回の Play の間。Play の終了で入口が外す | なし（`UnityEngine` の `AsyncOperation` だけ） | 受け渡し。値の単体 | +50 |
| `Runtime/Bootstrap/EditorPlaySnapshotIntake.cs`（新規、同上） | 宣言と loaded の一覧を照合して合否と理由を返す。照合の規則が変わる時に変える | 状態なし | なし | Policy。Unity の Scene なしの単体（AC3） | +70 |
| `Runtime/Bootstrap/AbstractApplicationInitializer.cs` | `register-loaded-scenes` の直前に、`#if UNITY_EDITOR` の段 `intake-editor-snapshots` を足す。directory mode で宣言がある時だけ、load を待ち、照合し、Scene ごとに 1 行 log を出し、active Scene を合わせる。失敗は例外にして既存の catch へ出す | 既存 | 上の 2 つ | Orchestration。実 Editor（AC7、AC10） | 1062、+45 |
| `Editor/PlaySnapshot/SceneSnapshotWriter.cs`（新規） | 開いている Scene を複製に書き、SHA-256 と Dirty を記録する。失敗時は書いた物を消す | 呼び出しの間 | `EditorSceneManager`、`AssetDatabase` | Unity / Editor I/O。EditMode の単体（AC1） | +110 |
| `Editor/PlaySnapshot/SnapshotRoot.cs`（新規） | 置き場の folder と disk の記録（pid、開始時刻、token）。生死の判定と回収 | disk。Editor の process をまたぐ | `System.IO`、`System.Diagnostics` | 永続。一時 folder の単体（AC2） | +160 |
| `Editor/PlaySnapshot/PlaySnapshotSession.cs`（新規、公開 API） | 準備（複製、開始 Scene の設定、宣言を bridge と `SessionState` に置く）と解除（bridge、開始 Scene、複製を戻す）、再読込の検出、`[InitializeOnLoad]` での宣言の置き直しと残骸の回収 | static の配線。宣言は `SessionState`（process 寿命）、複製は disk | 上の 2 つ、bridge | Orchestration。実 Editor（AC8、AC12、AC13）と、順序の単体（AC5 の一部） | +150 |
| `SampleGame/.../PlayEntryDecision.cs` | 経路の 3 つ目の値と行、拒否の理由 | 状態なし | 既存 | Policy。単体（AC4） | 222、+45 |
| `SampleGame/.../PlayEntryTransaction.cs` | tick の再判定の後、ledger の前に準備を呼ぶ。失敗と idle 復帰の時に解除を呼ぶ | 既存 | port（delegate） | Orchestration。単体（AC5） | 135、+30 |
| `SampleGame/.../PlayEntryInterceptor.cs` | facts（Scene reload の設定、開いている Scene の一覧）と port の配線、取り込みの log の採取 | 既存 | `PlaySnapshotSession` | Unity I/O。実 Editor | 175、+35 |
| `SampleGame/.../PlayEntryRecord.cs` | policy に宣言、record に取り込みの結果と由来の変化 | 既存 | なし | 永続 + 表示の素材。単体（AC6） | 192、+45 |
| `SampleGame/.../PlayEntrySettings.cs`・`PlayEntryWindow.cs`・`PurposeLaunchSettings.cs` | 経路の値、popup、由来の行、初回 Scene を置かない directory の値 | 既存 | 既存 | 表示・値。単体と実 Editor | 36 / 164 / 101、計 +40 |
| `.gitignore` | 置き場と `.meta` を無視する | — | — | AC8 の `git status` | +3 |

- 行数の警報: `AbstractApplicationInitializer.cs` は既に 1062 行で、+45 行（4%）。段は 1 つの private method にし、照合は `EditorPlaySnapshotIntake` へ出すので、initializer に入るのは順序だけである。分割しない。新規は各 500 行未満。
- 公開面: `PlaySnapshotSession` の準備と解除、宣言の読み取りだけを public にする（host の Editor asmdef から呼ぶため）。writer と root は internal。Runtime の bridge は `OneStarMaker.Editor` からだけ使うので internal にする（`SceneVariantRuntimeBridge` は host からも読むので public だが、こちらは host が読まない）。
- 配置の理由: 複製・置き場・取り込みは SampleGame の宣言や Scene 名を知らない（program D2、D10、§4.4 の Editor adapter）。Runtime の internal に届くのは `OneStarMaker.Editor` だけである。経路の選択と拒否の理由は S0-c の入口（SampleGame）にあるので、そこは動かさない（S0-c §3「配置」）。
- テスト境界: 単体は `unity/Assets/OneStarMaker/Tests/Editor/PlaySnapshot/`（SampleGame の型を使わない）と、既存の `unity/Assets/SampleGame/Tests/Editor/Build/PlayEntry*Tests.cs`。

## 4. 実装計画

### 起動への載せ方

封じはそのままの位置に置く。snapshot は `IAssetManagement` を通らない別の口から入れる。

1. 入口（混成の経路、tick の再判定の後）: 開いている全 Scene を置き場へ複製し、置き場に空の開始 Scene を 1 つ作り、`playModeStartScene` をそれにする。宣言を bridge と `SessionState` に置く。directory の値は初回 Scene 無しで ledger に置く。`EnterPlaymode`。
2. Unity: 空の開始 Scene だけを読む（開いている Scene は Play に入らない。Unity が停止時に元へ戻す）。
3. `BeforeSceneLoad`: 既存どおり。directory なので封じる。
4. `AfterSceneLoad`: directory の登録、UICommon、SceneResourceMap、SceneDirector の作成までは既存どおり。`register-loaded-scenes` の直前の段で、宣言の Scene を並び順に additive で load し（bridge の delegate。中身は `EditorSceneManager.LoadSceneAsyncInPlayMode`）、全部の完了を待ち、照合し、active Scene を合わせ、Scene ごとに 1 行 log を出す。失敗は例外。
5. `RegisterAlreadyLoadedScenes`: 既存どおり。map に名前がある複製を `AddScene` する。
6. 停止（`EnteredEditMode`）と、Play に至らなかった idle: 入口が bridge を外し、`playModeStartScene` を元の値に戻し、複製と置き場を消す。

Domain Reload が有効な時は static が消えるので、`[InitializeOnLoad]` が `SessionState` の宣言から bridge を置き直す。置き直しに失敗した Play が「snapshot 無し」の directory Play として黙って進まないよう（N2）、入口は 1 回の ledger に `editor:playSnapshotExpected=true` を足す。取り込みの段は、この key があるのに bridge が無い時を起動の失敗にする。key は真偽だけを運び、複製の path は運ばない。`content:` の名前空間に置かない（program U3）ので Player の protected config の照合の対象に入らず、読むのは `#if UNITY_EDITOR` の段だけである。

### 判定の行（`PlayEntryDecision`。既存の行は変えない）

| 条件 | 結果 |
|---|---|
| 混成の経路、Scene reload が無効 | 拒否（理由: この経路は Scene reload が要る） |
| 混成の経路、検証を通る候補なし | 拒否（既存の経路 Built と同じ回復） |
| 混成の経路、開いている Scene に新規未保存・UICommon・同名 2 つがある | 拒否（理由と回復を出す） |
| 混成の経路、候補あり、開いている保存済みの Scene が 1 つ以上 | 介入（複製を準備し、初回 Scene 無しの directory の値を置く） |
| 混成の経路、候補あり、開いている Scene が空の新規 1 つだけ | 拒否（理由: 載せる Scene が無い。回復: 経路 Built へ） |

明示の directory 設定と経路 Built の「再利用しうる Scene が開いていれば拒否」（G18）は変えない。

### 機構の比較（P7。Phase B と C の実測で埋める）

| 観点 | (i) 一時 Scene asset（本スライスが実装） | (ii) 一時 Content Directory（実装しない） |
|---|---|---|
| Play の前の所要 | S0-b: 1 Scene 約 0.3〜0.9 秒。疎通: `Title` 1 個で 5 秒未満。AC7・AC9 で採る | S0-b: 約 21〜25 秒 |
| Runtime の変更 | Editor 専用の段 1 つと照合 | `InstallContentDirectory` の複数化、`ContentRevisionGate`、revision lease、close の順序、token の所有、publisher の `Full` 限定 |
| 封じとの関係 | 封じは変えない。snapshot は別の口（§6 D-1 の文言の改訂が要る） | 変えない。Runtime は directory しか見ない |
| 由来と解放 | Scene は SceneDirector が `SceneManager` で unload、複製の file は入口 | directory の token と同じ |
| 起動後の再読込 | S3（同じ複製の file から読み直せる） | directory として読める |

### その他

- 変更対象: §3 の表。
- 順序: Runtime の照合と単体 → writer と root と単体 → session → Decision / Transaction / Record と単体 → Interceptor / Window → 実 Editor のコンパイル確認 → 動作確認（AC7、AC8、AC10、AC12、AC13 を各 1 回）。
- Phase B から Phase A へ差し戻す条件: 封じの時点か許可 key を変える必要が出た時。取り込みが `IAssetManagement` か `SceneDirector` の変更を要する時。asmdef の参照が要る時。Domain Reload 有効の時に bridge を `AfterSceneLoad` より前に置けない時（AC12 の不成立）。`playModeStartScene` を使わずに開いている Scene を Play から外す別の手段が要る時。
- 対象外を維持する方法: AC16。再要求の読み直しは実装せず、検出と Warning だけにする（N6）。

## 5. テストとレビュー計画

- 単体テスト: `OneStarMaker/Tests/Editor/PlaySnapshot/` に `SceneSnapshotWriterTests`（AC1）、`SnapshotRootTests`（AC2）、`EditorPlaySnapshotIntakeTests`（AC3）。`SampleGame/Tests/Editor/Build/` の `PlayEntryDecisionTests`（AC4）、`PlayEntryTransactionTests`（AC5）、`PlayEntryRecordTests`（AC6）へ追加。
- 差し戻し中の起点 `-Filter`: 上の 6 class と既存の `ContentDeliveryPlayBridgeTests`、`PurposeLaunchSettingsTests`。
- 判定必須テスト: 全 EditMode 回帰（空 filter）、XML の failed 0 と root result `Passed`。
- 全 EditMode 回帰の適用除外: なし。**Player の build は判定必須にしない。** 理由: Runtime の追加が `#if UNITY_EDITOR` の中に限られることを AC16 の diff の検査で確かめる。代替証拠はその検査の出力。
- 統合・Unity テスト: 実 Editor の AC7〜AC15。
- 操作・実行時・目視条件の検証経路:
  - 担当: 実行担当（判定 C）。動作確認は B の出口で AC7、AC8、AC10、AC12、AC13 を同じ手順で行う。環境: `unity/` の Editor（`tools/unity-editor.cmd eval`）。**`eval` の前に Editor の window を前面にする**（非フォーカスの Editor は main thread の操作が 5 秒で timeout する。疎通で確認）。
  - 準備: `Verify Play` で `spring-full` を 1 回実行して installed の候補を作る（Phase C の通常の Build）。Variant profile の選択を外す（選択があると入口が拒否する。疎通で確認。実行担当は元の選択を記録して最後に戻す）。入口の保存値 2 key を消し、経路を混成にする。
  - 操作: `EditorApplication.ExecuteMenuItem("Edit/Play Mode/Play")`（AC15 だけ実クリック）→ record の `state` が `EnteredPlay` / `Blocked` / `StartupFailed` になるまで短い `eval` で読む → 15 秒の観測窓 → `EditorApplication.ExitPlaymode()`。移行中の `eval` は捨てられるので、停止の後は `isPlaying` が false になってから次を送る。
  - 観測: `PlayEntryRecord` の JSON、loaded の Scene の名前と path と個数、active Scene、`playModeStartScene`、置き場の有無、対象の環境変数、開いている Scene の SHA-256 と Dirty、`git status --short`。構造と値で判断する。AC15 だけ画面。
  - AC10 の手順: 実行担当が `eval` で `playModeStateChanged` に handler を足し、`ExitingEditMode` の 2 回目（再 Play）で複製の `Title.unity` を `File.Delete` する。Domain Reload 無効の設定で行う（handler が残る）。
  - AC11 の手順: `eval` の reflection で initializer の SceneDirector を取り、`Title` の unload と `AddScene("Title")` を呼ぶ。B が実際に動いた `eval` を §6 Phase B 実装結果に残し、判定 C はそれを使う。
  - AC13 の手順: 実行担当が Unity CLI で Editor を起動し、その PID を記録する。AC7 の Play の中で `Stop-Process -Id <その PID> -Force`。人間が開いた Editor には行わない。開き直した後、`unity/Logs/Editor.log` の回収の 1 行と状態を読む。2 つ目の process は `Unity.exe -batchmode -quit -projectPath unity` を Editor を開いたまま起動し、終了 code と log の project lock の行を採る。
  - 証拠: 条件ごとの観測を 1 file にして evidence bundle へ収録する。C / C' はそれを読む。
- 未知の操作経路の疎通結果（Phase A、各 1 回）:
  - **Unity が読んだ複製は、封じた directory の実 initializer の下で 1 個だけ動く。** `Title` を `SaveScene(saveAsCopy)` で別の folder へ複製し（GUID は元と別）、開いている Scene を空の新規に替え、`playModeStartScene` を複製にし、installed の `spring-full`（2026-11-07 の revision、入口の検証を通った）の値を明示の環境変数に置いて Play の menu を実行した。結果: `EnteredPlay`、実効 mode `directory`、封じの log あり、Error / Exception 0 件、loaded は `Title`（path は複製）、`UIScene`、`OutGameScene` の 3 つで、`Title` は 1 個（`content:firstScene=Title` と重ならなかった）、active は `Title`。**確かめたのは「複製が開始 Scene の時」で、§4 の「additive の取り込みの段」そのものではない**（製品 code が要る。初回確認は B の動作確認の AC7）。
  - `SaveScene(saveAsCopy)` の後、元の `Title` の path と Dirty は変わらず、`git status` に出たのは複製の folder だけだった。
  - **`playModeStartScene` は Play を止めても設定したまま残る。** 戻すのは入口の責務（N4）。Editor の再起動の後に残るかは未確認（初回確認は AC13）。
  - 入口は Variant profile の選択がある Editor で経路 Built を拒否した（`ConflictingState`）。検証の準備に含める。
  - 非フォーカスの Editor では `eval` が timeout した。window を前面にすると通った。
  - **未確認。初回確認は Phase B の動作確認:** Domain Reload 有効の時に `[InitializeOnLoad]` が `AfterSceneLoad` より前に bridge を置けるか（AC12。不成立なら A へ戻す）。`LoadSceneAsyncInPlayMode` で additive に読んだ複製が `RegisterAlreadyLoadedScenes` の前に `isLoaded` になるか（AC7）。Play 中の強制終了の後の置き場と `playModeStartScene`（AC13）。
- 人間の判断が必要な条件: AC15（実クリック）。§6 D-9。
- 高コスト証拠: なし（準備の Content Directory build は Phase C の通常の Build として 1 回行う）。
- 判定 C の照合 script の方式: なし（テスト XML と観測 file を判定 C の担当が読む）。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC16 の diff の検査。
- A0/A1 主担当・モデル・ベンダー: A0 は program（複数担当）。A1 は Claude Fable 5.2（Anthropic）。
- A2 独立レビュー: §7 に記録する。
- A3 統合担当・モデル・採否: 未実施。
- C' 用に予約した担当・モデル・ベンダー: Grok 系（xAI、cursor-agent）。Phase A では使わない。
- 独立性の強化条件を満たせない場合の理由: 該当なし（予約あり）。

## 6. A3 判断（未決。owner の判断待ち）

- **D-1 起動への載せ方と、封じの文言。** 実 initializer では封じより前に Scene を読み終えられない（§1 現況）。program §5.2 の「封じの後に AssetDatabase / Addressables で複製を読む案は比較に入れない」をそのまま保つと、機構 (i) は実 initializer に載らない。選択肢は 3 つ。
  - (a) **推奨:** 封じの位置と強さは変えず、封じの対象を「`IAssetManagement` の Addressables の取得経路」と明記し、Editor の snapshot は「Play の前に宣言した Scene だけを、照合つきで、失敗時は起動の失敗として読む」別の口にする（§4）。program §5.2 の除外の文と `04-app-startup.md` §4.7 の封じの段落を改める。Player に口は無い。
  - (b) 封じを取り込みの後へ動かす。`BeforeSceneLoad` から取り込みまでの間、Scene の `Awake` や `Create*` hook が Addressables を使える窓ができる（I10 を弱める）。採らない。
  - (c) 機構 (ii)。Runtime は directory だけを見るので封じの文言は変わらないが、複数 directory の製品化が先に要り、Play のたびに約 21〜25 秒掛かる。(a) を採らない時の経路。
- **D-2 複製を明示に作るか、Unity 標準の Play の複製をそのまま使うか。** Unity は Play の開始時に開いている Scene を退避し、停止時に戻す。これを snapshot とみなせば、複製の file・置き場・異常終了の回収・`playModeStartScene` が全て要らず、G18 の拒否を混成の経路で外すだけになる。推奨: **明示の複製**（owner 決定 A1 を維持）。理由: Unity の退避は API から読み直せないので、unload の後の再要求（Title → Game → Title）で編集中の内容に戻れず、S3 で snapshot を候補として扱う足場にならない。SHA-256 の記録も複製の bytes が要る。代案を採るなら本書は大きく縮む（N7、P5、AC2、AC13 が消える）。
- **D-3 経路の 3 つ目の値。** 推奨: **足す**（名前の案: `Built + open scenes`）。開いている Scene があることから「混ぜたい」を機械が導けない（検証者は directory だけを見たい）。経路 Built の拒否はそのまま残す。代案: 経路 Built で Scene が開いていれば自動で混成にする（R2 F-03 の「明示的に区別する」に反する）。
- **D-4 起動の後の再要求。** 推奨: **検出して Warning と record に出すだけ**にし、snapshot からの読み直しは S3。本スライスの問いは起動で、読み直しは SceneDirector か resolver の変更が要る。編集者には「Title へ戻ると build 済みの Title になる」が Warning 付きで見える。代案: 再要求を失敗にする（通常のゲーム進行が止まる）。
- **D-5 混成の経路では `content:firstScene` を置かない。** 推奨: **置かない**（経路 Source と同じく、開いている Scene から始まる）。置くと、開いている Scene と宣言の初回 Scene の両方が load される。
- **D-6 対象の Scene と拒否。** 推奨: 開いている全 Scene を複製する。新規未保存、UICommon、同名 2 つは拒否する。map に無い Scene は複製から load するが登録しない（現行の Source の Play と同じ）。
- **D-7 Scene reload 無効の設定。** 推奨: **混成の経路では拒否**（G26 の「対応外の設定を診断できる」）。Scene reload 無効では Unity が開いている Scene をそのまま Play に使い、開始 Scene の差し替えが効く保証が無い。project の既定は有効。
- **D-8 S0-c から移送された C-2（`Planned` のまま Play に至らない時、record の観測が閉じない）。** 推奨: **含める**。本スライスは同じ経路に準備の失敗を足すので、「idle へ戻った時に観測を閉じる」を AC5 の 1 行として入れる。代案: S5 へ送る。
- **D-9 AC15（実クリック）の担当。** 推奨: S0-c の D-4 と同じ。判定 C の実行担当が画面操作で行い、できなければ owner が 1 回確認して観察記録を受理する。
- **D-10 置き場。** 推奨: `unity/Assets/OsmPlaySnapshot/<pid と乱数>/`、`.gitignore` に folder と `.meta` を足す。`LoadSceneAsyncInPlayMode` と `playModeStartScene` は asset の path が要るので `Assets/` の下に置く。名前は運用上の選択（担当の判断）。

## 7. A2 記録

未実施。
