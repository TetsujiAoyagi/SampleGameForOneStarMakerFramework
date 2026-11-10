# S0-d Editor snapshot を実 initializer の起動に載せる実証（EDITOR_SNAPSHOT_STARTUP_PROOF）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。

- type: `slice`
- status: `A`（**A3 候補版。未凍結。** owner が 2026-11-10 に §6 の D-1〜D-11 を推奨どおりと決め、D-2 に縮小の規則を足した。owner の指示で、外部レビュー用にドラフト PR へ公開する。外部レビューの 2 点（X-01、X-02。§7）を採用して反映した。Phase A の commit を 2 点に限る規則の、人間の指示による例外。A2 入力版は commit `8716a840`）
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
  - 次の作業: owner が外部レビューの反映（§7 X-01、X-02）を確かめる。追加の指摘があれば主担当が採否を §7 に足す。目的・利用者の文言が変わったら目的レビューをやり直す（X-01、X-02 では変わっていない）。owner の凍結の指示で A3 凍結版を commit する。
  - 引き継ぐ commit: この票を含む commit（SHA は起動指示で渡す）。
  - 必要な権限: なし（文書のみ）。Phase B 以降は Unity Editor の起動と操作。
  - 停止規則: 外部レビューで D-1 (a) が成り立たないと分かったら本書の §3・§4 は成り立たない。owner へ返す。
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
  - **N2 失敗時に別の供給元へ切り替えない。** 宣言した snapshot が 1 つでも読めない、または宣言と一致しない時、起動は initializer の既存の失敗の境界（log、回収、`ReleaseAll`。`:417-425`）で終わり、同名の directory の Scene も元の Scene も代わりに実行されない。Play mode 自体は既存の起動失敗と同じく抜けない（record が `StartupFailed` になる）。**取り込みの段が途中で失敗した時（1 つ目は load でき、2 つ目が読めない。全部 load した後の照合が合わない）は、その段が load した複製を、例外を既存の catch へ出す前に段自身が全て unload する**（§3、§4 の 4。snapshot の Scene が 1 つも loaded でない Play が残る）。この時点の複製は SceneDirector に未登録で、既存の `CleanupAfterStartupFailureAsync`（`:479-531`）も `ReleaseAll` も Unity の Scene を unload しないので、段のほかに回収する者がいない。Unity の callback（`Awake` 等）が物理 load で呼ばれることは、OSM の lifecycle が依存しないので違反に数えない。取り込みの段を抜けた後の失敗は既存の境界のまま（Editor は Play の終了に任せる。`:481-482`）で、複製は loaded のまま残りうるが、同名の directory の Scene は読まれない。崩しうる経路: 取り込みが失敗を握って先へ進むと、`LoadEditorFirstSceneAsync` や後の `AddScene` が `PerformUnitySceneLoad` で同名を directory から読む（`SceneDirector.Loading.cs:644-654`）。宣言が domain reload で失われて「snapshot 無し」に見える。探索した境界: `InitializeAfterSceneLoad` の catch と `CleanupAfterStartupFailureAsync`。
  - **N3 Player と、snapshot を宣言しない Play を変えない。** Player の build に取り込みの code が入らず、protected config の照合（`:843-852`、`PlayerContentConfiguration.cs:24-25`）に差分が無い。経路 Source、経路 Built、バッチ、Test Runner、明示設定の Play の起動順序と結果が base と同じ。`content:` の key を足さない（program U3）。崩しうる経路: `#if UNITY_EDITOR` の外に置いた参照、bridge の残留（前の Play の宣言が次の Play に効く）、複製の path を config に置く実装（config に置くのは §4 の真偽の目印 1 つだけ）。
  - **N4 開いている Scene を変えない（I11）。** Play の前後で、開いている Scene の file の bytes、Dirty、hierarchy、active Scene、並び順が同じ。追跡 file を変えない。崩しうる経路: `saveAsCopy` を付けない `SaveScene`、`playModeStartScene` の戻し忘れ（**疎通で、Play を止めても値が残ると確認**）、利用者が先に設定していた `playModeStartScene` の上書き（混成の経路は非 null なら拒否する。§4）、複製の import が開いている Scene を reload する、取り消し・起動失敗・Play 中の再 compile の経路で戻しが走らない。
  - **N5 同じ identity を 2 つ実行しない（G21）。** snapshot で載せた identity について、同じ Play の起動で同名の Scene がもう 1 つ load されない。崩しうる経路: `content:firstScene` が snapshot と同名（**疎通で、`Title` の複製と firstScene `Title` は 1 個だけになると確認**。`AddSceneCore` の active の検査、`SceneDirector.Loading.cs:126-137`）、`BootstrapUiCommonLoader` は active の `UICommon` だけを借りる（`BootstrapUiCommonLoader.cs:75-103`）ので additive の UICommon の複製は 2 つになる、開いている Scene に同名が 2 つある。
  - **N6 由来を偽らない。** record は snapshot から載せた Scene を directory 由来と書かず、その逆も書かない。起動の後に snapshot の Scene が unload され、同名が別の供給元から読み直された時は、Warning と record に出る（黙って切り替わらない）。崩しうる経路: `PerformUnitySceneUnload` の後の再要求（現況）、directory から読んだ Scene の `scene.path` は元の asset の path を返す（疎通で確認）ので path だけでは区別できない。
  - **N7 残骸が次の Play に入らず、追跡 file を汚さない。** 複製の置き場は git 管理外で、Play の終了・取り消し・起動の失敗・Editor の異常終了のどの後でも、次の Play の準備より前に回収され、前の Play の複製が別の Play の snapshot として採用されない。崩しうる経路: 異常終了では `SessionState` が消える、置き場を作った直後の異常終了（記録 file に頼ると孤児になるので、生死は folder 名の pid と開始時刻だけで判定する）、pid の再利用、`.meta` の残り、回収前の再 Play。
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
  - **AC1**（N4, N5, P6。EditMode テストで、実際の Scene と `AssetDatabase` を使う）複製の書き出し: Dirty の Scene の複製が in-memory の内容を持ち、元の file の SHA-256 と Dirty が変わらない。失敗経路: 新規未保存、同名 2 つ、書き込み先が既にある、途中の失敗（書いた物を消す）。
  - **AC2**（N7。一時 folder を使う EditMode テスト。生死の判定は folder 名と process の一覧を引数に取る純粋な関数にする）置き場: 生きている process の folder は回収しない。死んだ pid、pid が同じで開始時刻が違う folder、中身が空か途中の folder は `.meta` ごと回収する。失敗経路: 名前の形が違う folder（触らず Warning）、回収中の削除の失敗（残った物を次回に回収し、今回の Play には使わない）。
  - **AC3**（N2, N5）取り込みの照合（Unity の Scene を使わない純粋な判定）: 宣言と load 済みの一覧の一致。失敗経路: 1 件欠ける、path が違う、同名が 2 つ、宣言に無い同名が既に loaded。途中の失敗で unload する対象の選び方も同じ file の純粋な関数にする: 対象は path が宣言の複製と一致する loaded の Scene だけで、開始 Scene、UI の Scene、宣言に無い Scene を含まない。
  - **AC4**（N3, N8, P6）判定表: 混成の経路の全行と、既存の行が変わらないこと。失敗経路: 明示の directory + 開いた Scene は従来どおり拒否、経路 Built + 開いた Scene は従来どおり拒否、UICommon、Scene reload 無効、`playModeStartScene` が非 null、JSON の層に `content:firstScene` がある。最後の 1 つは facts を手で作らず、**JSON の本文 `{"content":{"firstScene":"Title"}}` から理由を作る関数（§4）を通して**拒否になることを固定する（値が空文字、key が無い時は理由が出ない）。
  - **AC5**（N4, N7, N8）transaction: 複製の準備 → 宣言 → ledger → 再 Play の順序。失敗経路: 準備の例外（Play に入らず、書いた複製と `playModeStartScene` を戻す）、`EnterPlaymode` の例外、再入、Play に至らず idle に戻った時（宣言と複製を回収し、record の観測を閉じる。§6 D-8）。
  - **AC6**（N6, P3）record: 宣言と取り込みの結果が別の欄に出る。再読込の検出（同名が別の path で load された）で Warning と record が変わる。Warning は 1 回の Play で identity ごとに 1 回だけ出す。
- 受け入れ条件（実 Editor。経路は §5。各 1 回）:
  - **AC7**（P1, N1, N5）混成の経路・`Title` を開いて Play。合格: record が `EnteredPlay`、封じの log が record にある、loaded の `Title` が 1 個でその path が複製の置き場の下、取り込みの log に `Title` が出て、その後の `EnteredPlayMode` から 15 秒の観測窓を終えた時点の record で Error / Exception が 0 件（取り込みの log が窓の中に出なければ不合格）。不合格の例: `Title` が 2 個、`Title` の path が元の asset。
  - **AC8**（N4, N7）AC7 の前に `Title` の `.unity` の SHA-256 を採り、`MarkSceneDirty` で Dirty にする。合格: 停止の後、SHA-256 が同じ、Dirty のまま、active Scene と並びが同じ、`playModeStartScene` が Play の前の値、複製の置き場が無い、入口が置く環境変数（`PurposeLaunchSettings.DirectoryPlayConfigKeys` の key と `editor:playSnapshotExpected`）が Play の前と同じ、`git status --short` が準備の直後に採った出力と同じ。読むのは `isPlaying` が false になり idle の tick を 1 回過ぎた後。
  - **AC9**（P2）`Title` と、実行担当が作る保存済みの空 Scene（map に無い）を additive に開いて Play。合格: 両方が複製から load され（map に無い Scene も物理的に loaded でよい）、SceneDirector に登録されるのは `Title` だけで、active Scene が開いていた時と同じ。不合格の例: map に無い Scene が登録される。
  - **AC10**（N2）再 Play の直前に複製の file を 1 つ消す（実行担当が `eval` で `ExitingEditMode` の handler を足す）。合格: 15 秒の観測窓を終えた時点で record が `StartupFailed`、`Title` という名前の Scene が loaded に無い、理由に snapshot が出る。不合格の例: directory の `Title` で Play が続く。
  - **AC18**（N2, N7。X-01）AC9 と同じ 2 Scene（`Title` が先、map に無い保存済みの空 Scene が後）で、再 Play の直前に **後の Scene の複製だけ**を消す。合格: 15 秒の観測窓を終えた時点で record が `StartupFailed`、loaded の Scene に path が置き場の下の物が空の開始 Scene のほかに無い（先に load できた `Title` の複製が unload されている）、`Title` という名前の Scene が loaded に無い、理由に読めなかった Scene の名前が出る。停止の後は AC8 と同じく置き場が無い。不合格の例: `Title` の複製が loaded のまま残る、directory の `Title` で Play が続く。
  - **AC11**（N6）AC7 の Play の中で、SceneDirector に `Title` の unload と再要求を出す。合格: 再要求の load が終わった後に読むと、Warning が 1 行あり、record の `Title` の由来が「snapshot の後 directory」に変わっている。同じ操作をもう 1 回行っても Warning は増えない。
  - **AC12**（P4）Domain Reload を有効にして AC7 と AC8 を繰り返し、設定を元に戻す。
  - **AC13**（P5, N7）AC7 の Play の中で、実行担当が自分で起動した Editor の process を強制終了する。Editor を開き直す。合格: 起動の後に残骸の回収の log が 1 行あり、置き場が無く、`playModeStartScene` が null、`git status --short` が空。続けて AC7 が合格する。別に、Editor を開いたまま同じ project へ 2 つ目の Unity（batch）を起動し、project lock で拒否される log を採る。
  - **AC14**（P6, N3, N8）経路 Built + `Title`（従来どおり拒否、置き場を作らない）、経路 Source + `Title`（従来どおり Play、置き場を作らない）、混成 + 新規未保存の Scene、混成 + UICommon。合格: それぞれの結果と理由が record に出る。
  - **AC15**（P1）**実際の Toolbar の Play ボタン**を 1 回クリックして AC7 の結果になる（§6 D-9）。
- 受け入れ条件（機械）:
  - **AC16**（N3）`git diff --name-only <base>` が §3 の表の file と、その test、`.meta`、`.gitignore`、文書に限られる。`.asmdef` と `SceneState` を含まない。`Scripts/Runtime/` の追加行は `#if UNITY_EDITOR` の中か、全体が `#if UNITY_EDITOR` の file にある。`content:` で始まる新しい key が無い。`PlayEntryDecision.ModeKeys` が 3 key のまま（X-02）。`pwsh tools/contract-audit.ps1` が通る。全 EditMode 回帰は XML の failed 0 と root result `Passed`（VFX の Inconclusive で `step.json` は `failed` になるので exit code で判定しない）。
  - **AC17**（P7）§4「機構の比較」の表が、AC7・AC9 の実測の所要で埋まる。
- ここでは答えない問いと所有スライス:
  - 起動の後の再要求を snapshot から読み直す、snapshot を resolver の候補として扱う、`ContentRequest` との接続、loaded で未要求の Scene の非参加（G23）: **S3 `RUNTIME_PROVIDER_INTEGRATION`**。
  - 機構 (ii) の製品化と複数 directory: 本スライスが (i) で GO なら不要。NO-GO なら owner へ返し、新しいスライスを切る。
  - 参照先 asset の overlay（G22）: **S2**。
  - 互換判定と候補の選び方: **S1**。経路の UI の整理: **S5**。
  - Framework への Play 入口の移設: S5 の A1（S0-c §3「配置」の判断を引き継ぐ）。
- スライス分割の判断: 契機（高リスクの初回実機スライスで、独立して失敗しうる未知が複数）に当たる。未知は (a) 実 initializer の下の取り込み、(b) Domain Reload 有効の時の宣言の受け渡し、(c) 実 process の異常終了の後の回収、の 3 つ。(a) は Phase A の疎通で中心の部分（Unity が読んだ複製が封じの下で 1 個だけ動く）が通った。(b) と (c) は AC12 と AC13 で別々に判定でき、同じ置き場の記録の code を使う。回収だけを別スライスにすると、残骸を残す code が先に develop に入る。**分割しない。** 規模の見積もりは §3（製品 code 約 +640 行、test 約 +600 行）で、S0-c（約 +730 行）と同じ程度。Phase B で取り込みの段が §3 の配置に収まらなければ A へ戻す。
- 判定定義: GO は N1〜N8 と P1〜P7 を AC1〜AC18 で満たす。GO の時、機構 (i) を製品の候補とする。NO-GO はいずれかの未達。CONDITIONAL ACCEPT は使わない。
- どうすれば誤って YES を出せるか（反例）: (1) 開いている Scene をそのまま Play に持ち込み（Unity 標準の Play）、「Title が動いた」を snapshot の成立とする（AC7 は path が置き場の下であることを見る）。(2) 経路 Source で試す（封じが無い。AC7 は封じの log を要求する）。(3) `eval` の `EnterPlaymode` だけで Play ボタンの経路とする（AC15）。(4) 模擬の lease だけで異常終了を確かめる（AC13 は実 process を止める）。(5) 取り込みの失敗を単体だけで確かめる（AC10 は実 Editor）。1 Scene の失敗だけで N2 とする（AC18 は 1 つ目が load できた後の失敗を見る）。(6) 起動の直後だけ見て由来を説明済みとする（AC11）。(7) Domain Reload 無効だけで試す（AC12）。
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
- 未決事項: なし（§6 は owner が決定済み。外部レビューの指摘は §7 に足す）。

## 3. 責務マップ

Framework の Editor（`unity/Assets/OneStarMaker/Scripts/Editor/PlaySnapshot/`、asmdef `OneStarMaker.Editor`、namespace `OneStarMaker.Editor.PlaySnapshot`）に複製と置き場と宣言を置き、Runtime には取り込みの照合と bridge だけを置く。SampleGame の入口は経路の判定と呼び出しだけを足す。

| ファイル | 責務 / 変更理由 | 所有者・寿命 | 依存 | 種別・テスト境界 | 行数 |
|---|---|---|---|---|---|
| `Runtime/Bootstrap/EditorPlaySnapshotBridge.cs`（新規、全体が `#if UNITY_EDITOR`） | Editor が宣言と load の delegate を置く口。宣言の型（identity、複製の path）。起動が読む形が変わる時に変える | static。1 回の Play の間。Play の終了で入口が外す | なし（`UnityEngine` の `AsyncOperation` だけ） | 受け渡し。値の単体 | +50 |
| `Runtime/Bootstrap/EditorPlaySnapshotIntake.cs`（新規、同上） | 宣言と loaded の一覧を照合して合否と理由を返す。照合の規則が変わる時に変える | 状態なし | なし | Policy。Unity の Scene なしの単体（AC3） | +70 |
| `Runtime/Bootstrap/AbstractApplicationInitializer.cs` | `register-loaded-scenes` の直前に、`#if UNITY_EDITOR` の段 `intake-editor-snapshots` を足す。directory mode で宣言がある時だけ、宣言の順に 1 つずつ load を待ち、照合し、Scene ごとに 1 行 log を出し、active Scene を合わせる。失敗した時は、この段が load した複製を `SceneManager.UnloadSceneAsync` で全て unload してから、元の例外を既存の catch へ出す（unload の失敗は Error の log にし、元の例外を隠さない） | 既存。load した複製の一覧は段の local で、段を抜けるまで | 上の 2 つ | Orchestration。実 Editor（AC7、AC10、AC18） | 1062、+60 |
| `Editor/PlaySnapshot/SceneSnapshotWriter.cs`（新規） | 開いている Scene を複製に書き、SHA-256 と Dirty を記録する。失敗時は書いた物を消す | 呼び出しの間 | `EditorSceneManager`、`AssetDatabase` | Unity / Editor I/O。EditMode の単体（AC1） | +110 |
| `Editor/PlaySnapshot/SnapshotRoot.cs`（新規） | 置き場の folder（名前に pid と開始時刻）。生死の判定と回収。記録 file は持たない | disk。Editor の process をまたぐ | `System.IO`、`System.Diagnostics` | 永続。判定は純粋な関数、回収は一時 folder の EditMode テスト（AC2） | +110 |
| `Editor/PlaySnapshot/PlaySnapshotSession.cs`（新規、公開 API） | 準備（複製、開始 Scene の設定、宣言を bridge と `SessionState` に置く）と解除（bridge、開始 Scene、複製を戻す）、再読込の検出、`[InitializeOnLoad]` での宣言の置き直しと残骸の回収 | static の配線。宣言は `SessionState`（process 寿命）、複製は disk | 上の 2 つ、bridge | Orchestration。実 Editor（AC8、AC12、AC13）と、順序の単体（AC5 の一部） | +150 |
| `SampleGame/.../PlayEntryDecision.cs` | 経路の 3 つ目の値と行、拒否の理由 | 状態なし | 既存 | Policy。単体（AC4） | 222、+45 |
| `SampleGame/.../PlayEntryTransaction.cs` | tick の再判定の後、ledger の前に準備を呼ぶ。失敗と idle 復帰の時に解除を呼ぶ | 既存 | port（delegate） | Orchestration。単体（AC5） | 135、+30 |
| `SampleGame/.../PlayEntryInterceptor.cs` | facts（Scene reload の設定、開いている Scene の一覧、JSON の層の `content:firstScene` の理由）と port の配線、取り込みの log の採取 | 既存 | `PlaySnapshotSession`、`PlayAsLauncher.CheckEmptyLowerLayer` | Unity I/O。実 Editor。JSON の理由は単体（AC4） | 175、+40 |
| `SampleGame/.../PlayEntryRecord.cs` | policy に宣言、record に取り込みの結果と由来の変化 | 既存 | なし | 永続 + 表示の素材。単体（AC6） | 192、+45 |
| `SampleGame/.../PlayEntrySettings.cs`・`PlayEntryWindow.cs`・`PurposeLaunchSettings.cs` | 経路の値、popup、由来の行、初回 Scene を置かない directory の値 | 既存 | 既存 | 表示・値。単体と実 Editor | 36 / 164 / 101、計 +40 |
| `.gitignore` | 置き場と `.meta` を無視する | — | — | AC8 の `git status` | +3 |

- 状態の所有（Editor の側の状態は、作る者と消す者がどれも `PlaySnapshotSession` だけ。他の class は値を受け取るだけ。Play の中で load した Scene だけは、load した取り込みの段が失敗時の回収を持つ）:

| 状態 | 置き場所・寿命 | 作る時点 | 消す時点 |
|---|---|---|---|
| 取り込みの段が load した複製（物理的に loaded な Scene） | Play の中の Unity の Scene。段の local の一覧 | 取り込みの段（initializer） | 段が失敗した時は段自身が unload。成功した後は既存の境界（map にある物は SceneDirector、その他は Play の終了） |
| 複製と空の開始 Scene | disk（置き場の folder。名前は pid と process の開始時刻） | 準備 | 解除。異常終了の後は `[InitializeOnLoad]` と次の準備の最初の回収 |
| 宣言 | `SessionState`（process 寿命） | 準備 | 解除 |
| bridge | Runtime の static | 準備。domain reload の後は `[InitializeOnLoad]` が宣言から置き直す | 解除 |
| `playModeStartScene` | Editor の設定 | 準備（元の値は必ず null。非 null なら入口が拒否する） | 解除で null。異常終了の後は回収が「置き場の下を指していれば null」にする |

  解除は `EnteredEditMode`、Play に至らなかった idle の tick、準備の例外、`[InitializeOnLoad]`（宣言が残っていて Play 中でない時）のどれからも同じ method を呼ぶ。何度呼んでも同じ結果になる。回収は準備より前に終える。
- 由来の変化の観測: `PlaySnapshotSession` が Play の間 `SceneManager.sceneUnloaded` / `sceneLoaded` を購読する。宣言の identity と同じ名前の Scene が、複製の path 以外で load されたら、public の event で identity を知らせる。SampleGame の `PlayEntryInterceptor` がそれを受けて record と Warning を書く。SceneDirector には触れない。
- 行数の警報: `AbstractApplicationInitializer.cs` は既に 1062 行で、+60 行（6%）。段は 1 つの private method にし、照合は `EditorPlaySnapshotIntake` へ出すので、initializer に入るのは順序だけである。分割しない。新規は各 500 行未満。
- 公開面: `PlaySnapshotSession` の準備と解除、宣言の読み取り、由来の変化の event だけを public にする（host の Editor asmdef から呼ぶため。`SampleGame.DependOnAll.Editor.asmdef` は既に `OneStarMaker.Editor` を参照しているので、参照は足さない）。writer と root は internal。Runtime の bridge は `OneStarMaker.Editor` からだけ使うので internal にする（`SceneVariantRuntimeBridge` は host からも読むので public だが、こちらは host が読まない）。
- 配置の理由: 複製・置き場・取り込みは SampleGame の宣言や Scene 名を知らない（program D2、D10、§4.4 の Editor adapter）。Runtime の internal に届くのは `OneStarMaker.Editor` だけである。経路の選択と拒否の理由は S0-c の入口（SampleGame）にあるので、そこは動かさない（S0-c §3「配置」）。
- テスト境界: 単体は `unity/Assets/OneStarMaker/Tests/Editor/PlaySnapshot/`（SampleGame の型を使わない）と、既存の `unity/Assets/SampleGame/Tests/Editor/Build/PlayEntry*Tests.cs`。

## 4. 実装計画

### 起動への載せ方

封じはそのままの位置に置く。snapshot は `IAssetManagement` を通らない別の口から入れる。

1. 入口（混成の経路、tick の再判定の後）: 開いている全 Scene を置き場へ複製し、置き場に空の開始 Scene を 1 つ作り、`playModeStartScene` をそれにする。宣言を bridge と `SessionState` に置く。directory の値は `content:firstScene` を含めずに ledger に置く（環境変数は空文字を置けないので、下の層に値が無いことを判定表で確かめる。base の `app-config.json` に `content:firstScene` は無い）。`EnterPlaymode`。
2. Unity: 空の開始 Scene だけを読む（開いている Scene は Play に入らない。Unity が停止時に元へ戻す）。
3. `BeforeSceneLoad`: 既存どおり。directory なので封じる。
4. `AfterSceneLoad`: directory の登録、UICommon、SceneResourceMap、SceneDirector の作成までは既存どおり。`register-loaded-scenes` の直前の段で、宣言の Scene を並び順に 1 つずつ additive で load して完了を待ち（bridge の delegate。中身は `EditorSceneManager.LoadSceneAsyncInPlayMode`。前の load が終わるまで次を始めないので、失敗の時点で進行中の load は無い）、全部を照合し、active Scene を合わせ、Scene ごとに 1 行 log を出す。delegate が null を返す、完了しても Scene が有効でない、照合が合わない、のどれも失敗にする。失敗した時は、この段が load した複製を全て unload し終えてから例外を出す（空の開始 Scene が残るので、最後の 1 Scene の unload にはならない）。Play の終了による cancel では unload しない（Unity が片付ける）。
5. `RegisterAlreadyLoadedScenes`: 既存どおり。map に名前がある複製を `AddScene` する。
6. 停止（`EnteredEditMode`）と、Play に至らなかった idle: 入口が bridge を外し、`playModeStartScene` を元の値に戻し、複製と置き場を消す。

Domain Reload が有効な時は static が消えるので、`[InitializeOnLoad]` が `SessionState` の宣言から bridge を置き直す。置き直しに失敗した Play が「snapshot 無し」の directory Play として黙って進まないよう（N2）、入口は 1 回の ledger に `editor:playSnapshotExpected=true` を足す。取り込みの段は、この key があるのに bridge が無い時を起動の失敗にする。key は真偽だけを運び、複製の path は運ばない。`content:` の名前空間に置かない（program U3）ので Player の protected config の照合の対象に入らず、読むのは `#if UNITY_EDITOR` の段だけである。

### 判定の行（`PlayEntryDecision`。既存の行は変えない）

| 条件 | 結果 |
|---|---|
| 混成の経路、Scene reload が無効 | 拒否（理由: この経路は Scene reload が要る） |
| 混成の経路、`playModeStartScene` が既に非 null | 拒否（理由: 開始 Scene の設定を上書きしない） |
| 混成の経路、JSON の層に空でない `content:firstScene` がある | 拒否（理由: 初回 Scene が開いている Scene と重なりうる）。**`facts.JsonLayer` では検出できない**（`PlayEntryConfigLayer.From` は mode の 3 key だけを残す。`PlayEntryDecision.cs:44-51`）。facts の採取が、混成の経路の時に既存の `PlayAsLauncher.CheckEmptyLowerLayer`（`PlayAsLauncher.cs:152-161`）へ `content:firstScene` と app-config の本文を渡し、返った理由を既存の `ConflictReason` に入れる。判定は既存の `ConflictingState` の行で拒否する。`ModeKeys` は広げない（広げると JSON に `content:firstScene` だけがある Play が「明示設定」になり、既存の経路の結果が変わる。N3）。環境変数とコマンドラインの層は既存の `CheckLaunchConflicts` が拒否する（`DirectoryPlayConfigKeys` が `content:firstScene` を含む。`PurposeLaunchSettings.cs:81-82`） |
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
- 順序: Runtime の照合と単体 → writer と root と単体 → session → Decision / Transaction / Record と単体 → Interceptor / Window → 実 Editor のコンパイル確認 → 動作確認（AC7、AC8、AC10、AC12、AC13、AC18 を各 1 回）。
- Phase B から Phase A へ差し戻す条件: 封じの時点か許可 key を変える必要が出た時。取り込みが `IAssetManagement` か `SceneDirector` の変更を要する時。asmdef の参照が要る時。Domain Reload 有効の時に bridge を `AfterSceneLoad` より前に置けない時（AC12 の不成立）。`playModeStartScene` を使わずに開いている Scene を Play から外す別の手段が要る時。
- 対象外を維持する方法: AC16。再要求の読み直しは実装せず、検出と Warning だけにする（N6）。

## 5. テストとレビュー計画

- 単体テスト: `OneStarMaker/Tests/Editor/PlaySnapshot/` に `SceneSnapshotWriterTests`（AC1）、`SnapshotRootTests`（AC2）、`EditorPlaySnapshotIntakeTests`（AC3）。`SampleGame/Tests/Editor/Build/` の `PlayEntryDecisionTests`（AC4）、`PlayEntryTransactionTests`（AC5）、`PlayEntryRecordTests`（AC6）へ追加。
- 差し戻し中の起点 `-Filter`: 上の 6 class と既存の `ContentDeliveryPlayBridgeTests`、`PurposeLaunchSettingsTests`。
- 判定必須テスト: 全 EditMode 回帰（空 filter）、XML の failed 0 と root result `Passed`。
- 全 EditMode 回帰の適用除外: なし。**Player の build は判定必須にしない。** 理由: Runtime の追加が `#if UNITY_EDITOR` の中に限られることを AC16 の diff の検査で確かめる。代替証拠はその検査の出力。
- 統合・Unity テスト: 実 Editor の AC7〜AC15 と AC18。
- 操作・実行時・目視条件の検証経路:
  - 担当: 実行担当（判定 C）。動作確認は B の出口で AC7、AC8、AC10、AC12、AC13、AC18 を同じ手順で行う。環境: `unity/` の Editor（`tools/unity-editor.cmd eval`）。**`eval` の前に Editor の window を前面にする**（非フォーカスの Editor は main thread の操作が 5 秒で timeout する。疎通で確認）。
  - 準備: `Verify Play` で `spring-full` を 1 回実行して installed の候補を作る（Phase C の通常の Build）。Variant profile の選択を外す（選択があると入口が拒否する。疎通で確認。実行担当は元の選択を記録して最後に戻す）。入口の保存値 2 key を消し、経路を混成にする。
  - 操作: `EditorApplication.ExecuteMenuItem("Edit/Play Mode/Play")`（AC15 だけ実クリック）→ record の `state` が `EnteredPlay` / `Blocked` / `StartupFailed` になるまで短い `eval` で読む → 15 秒の観測窓 → `EditorApplication.ExitPlaymode()`。移行中の `eval` は捨てられるので、停止の後は `isPlaying` が false になってから次を送る。
  - 観測: `PlayEntryRecord` の JSON、loaded の Scene の名前と path と個数、active Scene、`playModeStartScene`、置き場の有無、対象の環境変数、開いている Scene の SHA-256 と Dirty、`git status --short`。構造と値で判断する。AC15 だけ画面。
  - AC10 の手順: 実行担当が `eval` で `playModeStateChanged` に handler を足し、`ExitingEditMode` の 2 回目（再 Play）で複製の `Title.unity` を `File.Delete` する。Domain Reload 無効の設定で行う（handler が残る）。AC18 は同じ handler で、AC9 の 2 Scene を開き、後の Scene の複製だけを消す。
  - AC11 の手順: `eval` の reflection で initializer の SceneDirector を取り、`Title` の unload と `AddScene("Title")` を呼ぶ。B が実際に動いた `eval` を §6 Phase B 実装結果に残し、判定 C はそれを使う。
  - AC13 の手順: 実行担当が Unity CLI で Editor を起動し、その PID を記録する。AC7 の Play の中で `Stop-Process -Id <その PID> -Force`。人間が開いた Editor には行わない。開き直した後、`unity/Logs/Editor.log` の回収の 1 行と状態を読む。2 つ目の process は `Unity.exe -batchmode -quit -projectPath unity` を Editor を開いたまま起動し、終了 code と log の project lock の行を採る。
  - 証拠: 条件ごとの観測を 1 file にして evidence bundle へ収録する。C / C' はそれを読む。
- 未知の操作経路の疎通結果（Phase A、各 1 回）:
  - **Unity が読んだ複製は、封じた directory の実 initializer の下で 1 個だけ動く。** `Title` を `SaveScene(saveAsCopy)` で別の folder へ複製し（GUID は元と別）、開いている Scene を空の新規に替え、`playModeStartScene` を複製にし、installed の `spring-full`（2026-11-07 の revision、入口の検証を通った）の値を明示の環境変数に置いて Play の menu を実行した。結果: `EnteredPlay`、実効 mode `directory`、封じの log あり、Error / Exception 0 件、loaded は `Title`（path は複製）、`UIScene`、`OutGameScene` の 3 つで、`Title` は 1 個（`content:firstScene=Title` と重ならなかった）、active は `Title`。**確かめたのは「複製が開始 Scene の時」で、§4 の「additive の取り込みの段」そのものではない**（製品 code が要る。初回確認は B の動作確認の AC7）。
  - `SaveScene(saveAsCopy)` の後、元の `Title` の path と Dirty は変わらず、`git status` に出たのは複製の folder だけだった。
  - **`playModeStartScene` は Play を止めても設定したまま残る。** 戻すのは入口の責務（N4）。Editor の再起動の後に残るかは未確認（初回確認は AC13）。
  - 入口は Variant profile の選択がある Editor で経路 Built を拒否した（`ConflictingState`）。検証の準備に含める。
  - 非フォーカスの Editor では `eval` が timeout した。window を前面にすると通った。
  - **未確認。初回確認は Phase B の動作確認:** Domain Reload 有効の時に `[InitializeOnLoad]` が `AfterSceneLoad` より前に bridge を置けるか（AC12。不成立なら A へ戻す）。`LoadSceneAsyncInPlayMode` で additive に読んだ複製が `RegisterAlreadyLoadedScenes` の前に `isLoaded` になるか（AC7）。Play 中の強制終了の後の置き場と `playModeStartScene`（AC13）。file の無い path を `LoadSceneAsyncInPlayMode` に渡した時の戻り値（null、例外、完了しても無効な Scene のどれか。AC10。段は 3 つとも失敗として扱う）。取り込みの失敗の中での `UnloadSceneAsync` の完了（AC18）。
- 人間の判断が必要な条件: AC15（実クリック）。§6 D-9。
- 高コスト証拠: なし（準備の Content Directory build は Phase C の通常の Build として 1 回行う）。
- 判定 C の照合 script の方式: なし（テスト XML と観測 file を判定 C の担当が読む）。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC16 の diff の検査。
- A0/A1 主担当・モデル・ベンダー: A0 は program（複数担当）。A1 は Claude Fable 5.2（Anthropic）。
- A2 独立レビュー: §7 に記録する。
- A3 統合担当・モデル・採否: 未実施。
- C' 用に予約した担当・モデル・ベンダー: Grok 系（xAI、cursor-agent）。Phase A では使わない。
- 独立性の強化条件を満たせない場合の理由: 該当なし（予約あり）。

## 6. A3 判断

**D-1〜D-11 は owner が 2026-11-10 に下の推奨どおりと決めた。** D-1 は (a)（additive の複数 Scene を含む。(a') と D-11 の分割は採らない）。D-2 には下の縮小の規則が付く。program §5.2 の「封じの後に AssetDatabase / Addressables で複製を読む案は比較に入れない」は、この決定で「`IAssetManagement` を通して読む案は比較に入れない」に改める（harvest で program と `04-app-startup.md` §4.7 に反映する）。

- **D-1 起動への載せ方と、封じの文言。** 実 initializer では封じより前に Scene を読み終えられない（§1 現況）。program §5.2 の「封じの後に AssetDatabase / Addressables で複製を読む案は比較に入れない」をそのまま保つと、機構 (i) は実 initializer に載らない。選択肢は 3 つ。
  - (a) **推奨:** 封じの位置と強さは変えず、封じの対象を「`IAssetManagement` の Addressables の取得経路」と明記し、Editor の snapshot は「Play の前に宣言した Scene だけを、照合つきで、失敗時は起動の失敗として読む」別の口にする（§4）。program §5.2 の除外の文と `04-app-startup.md` §4.7 の封じの段落を改める。Player に口は無い。
  - (b) 封じを取り込みの後へ動かす。`BeforeSceneLoad` から取り込みまでの間、Scene の `Awake` や `Create*` hook が Addressables を使える窓ができる（I10 を弱める）。採らない。
  - (c) 機構 (ii)。Runtime は directory だけを見るので封じの文言は変わらないが、複数 directory の製品化が先に要り、Play のたびに約 21〜25 秒掛かる。(a) を採らない時の経路。
  - (a') (a) のうち、**開いている Scene が 1 つの時だけ**を対象にし、複製を `playModeStartScene` にして Unity に読ませる（A2 の代替構成の案）。Phase A の疎通で通った形そのもので、Runtime の取り込みの段と bridge が要らない（由来の log だけ）。additive の複数 Scene は D-11 の後続へ送る。封じの文言の改訂が要る点は (a) と同じ。
- **D-2 複製を明示に作るか、Unity 標準の Play の複製をそのまま使うか。** Unity は Play の開始時に開いている Scene を退避し、停止時に戻す。これを snapshot とみなせば、複製の file・置き場・異常終了の回収・`playModeStartScene` が全て要らず、G18 の拒否を混成の経路で外すだけになる。推奨: **明示の複製**（owner 決定 A1 を維持）。理由: Unity の退避は API から読み直せないので、unload の後の再要求（Title → Game → Title）で編集中の内容に戻れず、S3 で snapshot を候補として扱う足場にならない。SHA-256 の記録も複製の bytes が要る。代案を採るなら本書は大きく縮む（N7、P5、AC2、AC13 が消える）。
  - **縮小の規則（owner、2026-11-10）:** 明示の複製が成り立たないと分かったら（Phase B の動作確認か Phase C で、複製の取り込み・置き場の回収・`playModeStartScene` の戻しのどれかが N1〜N8 を満たせない時）、明示の複製を諦め、Unity 標準の Play の退避を snapshot として使う形へ縮小する。縮小は凍結条件の意味を変えるので、決定記録ではなく Phase A の新しい revision で行う（N7、P5、AC2、AC13 と §3 の複製・置き場の行を外す）。代案の採否そのものは決定済みなので、その revision で owner に問い直さない。機構 (ii) へは進まない。
- **D-3 経路の 3 つ目の値。** 推奨: **足す**（名前の案: `Built + open scenes`）。開いている Scene があることから「混ぜたい」を機械が導けない（検証者は directory だけを見たい）。経路 Built の拒否はそのまま残す。代案: 経路 Built で Scene が開いていれば自動で混成にする（R2 F-03 の「明示的に区別する」に反する）。
- **D-4 起動の後の再要求。** 推奨: **検出して Warning と record に出すだけ**にし、snapshot からの読み直しは S3。本スライスの問いは起動で、読み直しは SceneDirector か resolver の変更が要る。編集者には「Title へ戻ると build 済みの Title になる」が Warning 付きで見える。代案: 再要求を失敗にする（通常のゲーム進行が止まる）。
- **D-5 混成の経路では `content:firstScene` を置かない。** 推奨: **置かない**（経路 Source と同じく、開いている Scene から始まる）。置くと、開いている Scene と宣言の初回 Scene の両方が load される。
- **D-6 対象の Scene と拒否。** 推奨: 開いている全 Scene を複製する。新規未保存、UICommon、同名 2 つは拒否する。map に無い Scene は複製から load するが登録しない（現行の Source の Play と同じ）。
- **D-7 Scene reload 無効の設定。** 推奨: **混成の経路では拒否**（G26 の「対応外の設定を診断できる」）。Scene reload 無効では Unity が開いている Scene をそのまま Play に使い、開始 Scene の差し替えが効く保証が無い。project の既定は有効。
- **D-8 S0-c から移送された C-2（`Planned` のまま Play に至らない時、record の観測が閉じない）。** 推奨: **含める**。本スライスは同じ経路に準備の失敗を足すので、「idle へ戻った時に観測を閉じる」を AC5 の 1 行として入れる。代案: S5 へ送る。
- **D-9 AC15（実クリック）の担当。** 推奨: S0-c の D-4 と同じ。判定 C の実行担当が画面操作で行い、できなければ owner が 1 回確認して観察記録を受理する。
- **D-10 置き場。** 推奨: `unity/Assets/OsmPlaySnapshot/<pid と乱数>/`、`.gitignore` に folder と `.meta` を足す。`LoadSceneAsyncInPlayMode` と `playModeStartScene` は asset の path が要るので `Assets/` の下に置く。名前は運用上の選択（担当の判断）。

- **D-11 分割（A2 の代替構成の提案）。** 案: S0-d1 = 1 Scene（D-1 (a')）と I11、残骸、異常終了。S0-d2 = additive の複数 Scene の取り込みの段と (i) / (ii) の最終の選定。推奨: **分割しない**（§2「スライス分割の判断」）。ただし owner が D-1 で (a') を選ぶなら、それが分割と同じ結果になり、§3 の Runtime の 3 行と AC3、AC9 が後続へ移る。規模は製品 code が約 +640 行から約 +450 行になる。

## 7. A2 記録

入力版: commit `8716a840`（`git show` の本書の内容 sha256 `3cc2a6ad332d6334fa675e39bc644648c9d74cfae9f1734d41311db05e5d5f44`）。指摘の相互の受け渡しはしていない。下の採否は主担当の分類に owner の 2026-11-10 の決定を当てたもの。外部レビューの指摘は末尾の「外部レビューとの突き合わせ」に足した。

- **アーキテクチャゲートと不変条件の反証:** Codex（OpenAI、gpt-6-luna）。packet は本書、program、ゲート定義、AGENTS.md、関係する source の写し。枠（§3・§4 の変更は実装済みと仮定）を守った。
- **目的レビュー:** 未関与の subagent（Anthropic、Claude Opus）。入力は §1 の「目的」と「利用者」の原文だけ（sha256 `c88525d186683dec2696528e0d8b3bb3201a41596284c7ee67bf1812896d86b1`）。
- **A0 だけからの代替構成:** 未関与の subagent（Anthropic、Claude Opus）。入力は A0（問い、program、現況の事実、疎通の結果）と現行 code。本書は読んでいない。

### アーキテクチャゲートの採否（候補）

| ID | 重大度 | 要旨 | 採否 | 理由と反映 |
|---|---|---|---|---|
| F-01 | blocker | 混成の経路で `content:firstScene` を ledger から省いても、下の層に値があれば同名が 2 つ実行される（N5） | 採用 | 環境変数は空文字で上書きできない。JSON の層に値があれば拒否する行を足した（§4、AC4）。環境変数とコマンドラインは既存の検査が拒否する。base の `app-config.json` に値は無い。検出の手段は X-02 で直した |
| F-02 | major | SampleGame の Editor asmdef から `OneStarMaker.Editor` に届くかが示されていない | 解消（事実の確認） | `SampleGame.DependOnAll.Editor.asmdef` は既に `OneStarMaker.Editor` を参照している。§3 に書いた。packet に asmdef を入れていなかった |
| F-03 | major | `playModeStartScene` の元の値の保持と復元が無い。元が非 null だと戻せない（N4 を破る順序あり） | 採用 | 非 null なら混成の経路を拒否し、戻す値を常に null にした（§3 の状態の表、§4、AC4） |
| F-04 | major | 「起動は失敗で終わる」が Play を抜ける意味か不明 | 採用（文言） | 既存の起動失敗と同じ境界で、Play mode は抜けないと N2 に書いた |
| F-05 | major | 再要求の検出を誰がどう観測し、record へどう渡すかが無い | 採用 | `SceneManager` の event を `PlaySnapshotSession` が購読し、public の event で入口へ渡す（§3）。SceneDirector は変えない |
| F-06 | major | 置き場を作った直後の異常終了で、記録の無い孤児が残る（N7 を破る順序あり） | 採用 | 記録 file をやめ、生死を folder 名の pid と開始時刻で判定する。`.meta` ごと回収し、回収を準備より前に終える（N7、AC2、§3） |
| F-07 | major | bridge、宣言、置き場、開始 Scene の所有者が複数の class に分かれ、異常経路の最終の回収者が追えない | 採用 | 作る者と消す者を `PlaySnapshotSession` だけにし、状態の表と解除の契機を §3 に書いた |
| F-08 | major | AC1 を「単体」と呼ぶが Editor I/O を伴う | 採用（文言） | AC1・AC2 は EditMode テスト、AC3 は純粋な判定と書き分けた |
| F-09 | minor | 責務の分割は概ね適合。F-05 の観測が収まっていない | F-05 で反映 | — |
| F-10 | minor | 現況と疎通の主張に source と矛盾する物は無い。packet に無い物は未検証 | 記録 | F-01 と F-02 は主担当が repo で確かめた |
| 曖昧 | — | AC7、AC8、AC9、AC10、AC11 の時点と境界 | 採用 | 各 AC に読む時点、比べる対象、回数を足した |

反証の結果: N1・N8 は破れなかった。N2・N3・N6 は上の F-04・F-07・F-05 の不足として指摘された。N4（F-03）、N5（F-01）、N7（F-06）は破る順序が示され、上のとおり塞いだ。

### 代替構成との突き合わせ（候補）

| ID | 要旨 | 採否 | 理由と反映 |
|---|---|---|---|
| A-1 | 複製を `playModeStartScene` で Unity に読ませる案を独立の選択肢として扱い、owner の裁定を求める | 不採用（owner、§6 D-1 は (a)） | 1 Scene なら Runtime の段が要らない。additive は扱えない |
| A-2 | directory mode では、snapshot として登録した Scene だけを名前で再利用し、他の既ロードの Scene は Runtime で拒否する | 一部採用 | 宣言のある Play の中では AC3 が拒否する。宣言の無い directory Play へ広げると既存の挙動が変わる（N3）ので、S3 へ送る |
| A-3 | 分割（1 Scene と残骸 / 複数 Scene と最終の選定） | 不採用（owner、§6 D-11） | — |
| A-4 | Enter Play Mode の 4 設定すべてで確かめる | 不採用（owner、§6 D-7） | Scene reload 無効は拒否して診断する（G26 が許す）。Domain Reload の有無は AC12 で見る |
| A-5 | 2 つの checkout で一時 path が衝突しないこと | 不採用 | 置き場は project の中にあり、checkout ごとに別になる。同じ project の 2 process は AC13 で見る |
| A-6 | 反例（封じの log だけで I10 の維持としない、空の Scene で I11 を測らない、正常終了で残骸を見ない 等） | 反映済み | §2 の反例 (1)〜(7) と AC8・AC13 が同じ点を塞いでいる。「Title が 1 個」だけで G21 としない点は、AC7 が path を見ることで塞ぐ |

### 目的レビューとの突き合わせ（候補）

| ID | 要旨 | 採否 | 理由と反映 |
|---|---|---|---|
| P-1 | 複製を有効にする toggle や mode の切り替えを作らない。Scene を開いているかどうかで 2 つの利用形態を区別できる | 不採用（owner、§6 D-3） | 初稿は 3 つ目の経路を足す。program §4.3（R2 F-03）は「snapshot を優先する policy と厳密な directory-only を明示的に区別する」と求めていて、指摘と逆。利用者の入力を 1 つ増やすかどうかなので owner に返す |
| P-2 | 複製する Scene を選ぶ UI、書き戻し、掃除ボタン、再 build、外部の観測ツール、S1・S3 向けの API の先取りを作らない | 初稿と一致 | どれも作らない |
| P-3 | 2 案を同じ観点で比べた材料を出す。結果の範囲と確かめていない点を明記する | 採用 | §4 の比較の表。確かめていない点は harvest の時に program §5.2 へ書く |
| P-4 | 保証の一覧（実際の起動処理、開いている Scene を変えない、保護 3 つ、失敗時に切り替えない、Scene を開かない Play が同じ、由来と解放、残骸、実 Editor の証拠） | 初稿と一致 | N1〜N8 と P1〜P7 に対応する |

### 外部レビューとの突き合わせ

owner が依頼した外部レビュー（web の GPT。PR のレビューとして 2026-11-10 に受領）。入力は A3 候補版（owner 決定を反映した版）の本書。文書だけのレビューで、実 Editor の結果は見ていない。2 点とも主担当が現行 code で確かめた。目的・利用者の文言は変えていないので、目的レビューはやり直さない。

| ID | 重大度 | 要旨 | 採否 | 理由と反映 |
|---|---|---|---|---|
| X-01 | major | additive の 2 つ目で取り込みが失敗した時、先に load した複製を誰が unload するかが無い。AC10 は 1 Scene の失敗しか見ない（N2、N7） | 採用 | 指摘のとおり、`CleanupAfterStartupFailureAsync` も `ReleaseAll` も Unity の Scene を unload しない。取り込みの段が、自分の load した複製を例外の前に unload する（N2、§3 の initializer の行と状態の表、§4 の 4）。load を 1 つずつにして、失敗の時点で進行中の load を無くした。AC18 と AC3 の 1 行を足した。Unity の callback の発火は違反に数えない（レビューの前提と同じ） |
| X-02 | moderate | `facts.JsonLayer` は mode の 3 key しか残さないので、JSON の `content:firstScene` を拾えない（F-01 の反映が成り立っていない） | 採用 | 指摘のとおり（`PlayEntryDecision.cs:44-51`、`:132`）。既存の `CheckEmptyLowerLayer` を使って理由を `ConflictReason` に入れる形に直し、`ModeKeys` は広げない（§4 の判定の行、§3 の Interceptor の行）。AC4 に JSON の本文から拒否までを通す case を、AC16 に `ModeKeys` が 3 key のままの検査を足した |
