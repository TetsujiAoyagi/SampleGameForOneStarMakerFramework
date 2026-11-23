# 遊べる Player の起動の順を Editor の directory Play と揃える

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S4-1a）
- status: `A`（Phase A の revision 0。A1 の初稿。**凍結していない**）
- branch: `claude/gracious-noether-o0vn1y`（PR の base は `develop`）
- implementation base commit: `116b00918dbdefa9f2dff5aeec7e43ef4a8b4a30`（`develop` の head、2026-11-23。§1 の現況と A2 の固定 base もこれ）
- implementation head commit: 未固定
- A3 凍結記録: 未凍結
- risk: `high`（Framework の起動の段の順（`AbstractApplicationInitializer`、`BootstrapUiCommonLoader`）と SampleGame の Player の構成の判定、Player の build の mutation を変える。UICommon を Player の build から外した IL2CPP・High の Player は初めてで、独立して失敗しうる未知が複数ある（§5 の u1〜u5））
- owner: root（依頼者）
- created: 2026-11-24
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: [04 章](../../unity/Assets/Docs/Architecture/04-app-startup.md) §4.1・§4.2・§4.6・§4.7（Player の 2 つの種類の起動の段、Player の構成の latch）、repo の README の「試す」5 の確認の記録、[実測の事実](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)（UICommon を Player の build から外した時の strip の実測）、[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S4-1a の行と S4-2 へ送る入力
- 関連: [program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S4-1・S4-2 の行、「S4-1 から移送した入力」、§10 の U11。入力は [PR #212](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/212) のコメント「owner との会話で決まったことと、S4-1a の入力」（2026-11-24）と、同 PR の S4-2 の統合案（未凍結）が S4-1a について書く箇所（前提のスライス、§2.5 の 8、AC12・AC14）
- Phase A snapshot の取得案内・状態: 該当なし（機械向けの期待値・照合 script は無い。§5。レビューの入力は本書と program だけ）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: Phase A の A2（独立レビュー）と A3（採否の統合、Draft PR での外部計画レビュー、owner の凍結判断）。
  - 次の作業: A2 の 3 件（目的レビュー、A0 だけからの代替構成、アーキテクチャゲート兼反証）を起こし、A3 で採否を §5.1 に書く。
  - 読む範囲: 全節。
  - 引き継ぐ commit: この票を含む commit（SHA は起動指示と PR の状態ブロックで渡す）。
  - 必要な権限: 文書だけ。
  - 停止規則: 目的・利用者の欄を変える指摘は owner が裁定し、変えたら目的レビューをやり直す。
  - 引き渡さないもの: なし。

## 1. 目的と対象外

- 目的: Windows の遊べる Player が、Editor の directory Play と同じ順（content の登録 → UICommon → SceneResourceMap）で起動し、UICommon と SceneResourceMap を、Player の exe に焼き込んだ写しからではなく、登録した content から読むようにする。これにより、遊べる Player が読む UICommon と SceneResourceMap は content の物になり、後続（S4-2: 上書きを Player に重ねる）が content に重ねる上書きが、それらの編集（Scene の物を動かす・asset を足すといった普通の編集で変わる）にも届く土台になる。遊べる Player の起動の成功・失敗・終わり方の見え方（成功は Player の log の 1 行、失敗は Player の log と exit 1、窓を閉じて exit 0）、protected config、起動の fail-closed は弱めない。動作確認の Player（`smoke`）の receipt・exit code・起動の段の順は変えない。
- 利用者: SampleGame を Windows の遊べる Player で確かめる開発者（S4-2 以降で Player に上書きを重ねる開発者を含む）。build を始めるのは今ある Unity の menu（`Tools/OSM/Content/Build Spring Playable Player`）、遊ぶのは build された exe の起動と窓の中の操作で、このスライスは利用者の操作と入力を増やさず、変えない。
- 対象外（所有する後続は §2.3）:
  - 上書きを Player に重ねること（`ContentOverlayStartup.Decide` は Player の構成で上書きの key を拒んだままにする。S4-2）。
  - 動作確認の Player（`smoke`）の起動の段の順・receipt・出力（今のまま、生成した UICommon の Scene から始まり、map を `Resources` の写しから読み、サービスの開始の後に content を登録する）。
  - Player での要求時解決の配線（`WireContentResolution` は Player の構成では呼ばないまま）。
  - Editor の起動（directory Play、Addressables 互換、snapshot の Play、上書きの Play）の段の順と結果。
  - `bs4` の名の改名（`bs4-runtime.json`、`Resources/BS4/`、`.bs4-owner`、`_useBs4PlayerMode`。04 章 §4.7 の理由のまま）。
  - Player の構成（IL2CPP・managed stripping High・development build でない）、出力先（`artifacts/playable/`）、baked config の key と値、protected の比較、種類の latch。
  - 起動の段の時間の上限（hang を失敗にすること。program §10 の U11 (a)）。
  - content の build が UICommon と map を root に合成する規則（今のまま）。
- 現況（base `116b0091`。path は `unity/Assets/` からの相対）:
  - **Player の構成の判定（`SampleGame/DependOnAll/AppInitializer.cs:64-70,137-139`）:** `BeforeSceneLoad` の冒頭で「Editor でなく、active な Scene の名前が ordinal に `UICommon`」の時に `_useBs4PlayerMode` を真にし、`UseRequiredPlayerFileConfiguration` がそれを返す。code の注記は、生成した bootstrap の Scene の名前を config とは独立した成果物の marker として読み、config が欠けた時も通常の Addressables へ戻さず required provider で fail-closed にする、と書く。環境変数と command line では有効にならない（04 章 §4.7）。
  - **`BeforeSceneLoad`（`OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs:255-322`）:** `BuildConfig`（`:984-1012`。Player の構成なら `StreamingAssets/bs4-runtime.json` を必須で読み、種類を latch し、baked を固定する）→ `ReadContentDirectoryConfiguration`（`:1170-1241`。Player の分岐は protected の比較と installed の pair の照合。pair が無ければ exe の横の同梱 directory）→ directory なら Addressables を封じる（`:275-284`）。この順は 2 つの種類で同じで、このスライスでは変えない。
  - **`AfterSceneLoad` の順（`:338-520`）:**

    | 段 | Editor の directory Play | Player（今。2 つの種類で同じ） |
    |---|---|---|
    | 1 | `register-content-directory`（`:353-357`。条件は directory の設定があり Player の構成でない） | `load-ui-common`: active な Scene の UICommon を借用（`BootstrapUiCommonLoader.cs:75-88`）。無ければ `The Player bootstrap scene must contain UICommon; Addressables fallback is forbidden.`（`:90-91`） |
    | 2 | `load-ui-common`: logical key で `LoadContentSceneAsync`（`BootstrapUiCommonLoader.cs:94-104`）→ `EnsureEventSystem`（`:160-170`） | `load-scene-resource-map`: `LoadPlayerSceneResourceMap`（SampleGame は `Resources.Load("BS4/<identity>/SceneResourceMap")`。`AppInitializer.cs:141-146`） |
    | 3 | `load-scene-resource-map`: `LoadDirectorySceneResourceMapAsync`（`:934-947`。logical key で `LoadContentAssetAsync`） | サービスの開始、scene factory |
    | 4 | サービスの開始、scene factory | `register-content-directory`（`:405-409`） |
    | 5 | 要求時解決の配線、`SceneDirector`、既ロードの登録か snapshot、初回 Scene（`RouteFirstScene`） | `SceneDirector`（配線なし）、既ロードの登録、初回 Scene（`Smoke` は動作確認の尾、`Playable` は load したまま終える） |

  - **登録（`:817-849`）:** `InstallConfiguredContentDirectoryAsync` は 2 つの位置から同じ method を呼ぶ。上書きの無い登録は log を出さない（上書きの時だけ 1 行）。
  - **失敗と終わり（`:501-514,705-754,1021-1100`）:** `AfterSceneLoad` の例外は、Player の構成なら初回 Scene の unload、directory の close（`AssetManagement.CloseContentDirectoryAsync` は台帳の directory の Scene を全て unload してから close する。`AssetManagement.ContentDirectory.cs:35-56`）、派生の後始末と失敗の通知（別々の try）、`ReleaseAll` の順。SampleGame の失敗の通知は遊べる Player なら receipt を書かずに `Application.Quit(1)`（`AppInitializer.cs:160-178`）。窓を閉じると `Application.quitting` → `ReleaseAll` が directory を `CompletePlayStop` で解除する（`:1073-1078`）。S4-1 は、UICommon が借用（content の物でない）の遊べる Player で、Title と InGame の上の quit が exit 0 になることを確かめた。UICommon と map を content から保持したままの quit は、Editor の Play の停止では通っているが、Player では通っていない。
  - **content の build（`SampleGame/DependOnAll/Editor/Build/SampleGameContentBuild.cs:83`、`SampleGameBootstrapContentComposer.cs`）:** どの宣言の build でも、UICommon の Scene（`Assets/OneStarMaker/Scenes/UISystem/UIScene.unity`、logical key `samplegame:bootstrap:uicommon`）と SceneResourceMap（`Assets/OneStarMakerCommon/SceneMap/SceneResourceMap.asset`、logical key `samplegame:bootstrap:scene-resource-map`）を表現 `Full` の root として合成する。遊べる Player の同梱の `content/` と、同じ build の installed revision のどちらにも入っている。
  - **Player の build（`SampleGamePlayerBuildCoordinator.cs:70-114`、`PlayerBuildProjectMutation.cs`、`UnityPlayerBuildAdapter.cs`、`PlayerBuildReportVerifier.cs`）:** 2 つの種類で同じく、mutation の root `Assets/Resources/BS4/<identity>/` に SceneResourceMap と各 `SceneResource` を写して参照を書き換え（`CopyGraphClosure`）、UICommon の Scene を別の GUID の `UICommon.unity` として写し（`CopyBootstrapScene`。UICommon の root が 1 つで EventSystem があることを検査する）、その Scene 1 つだけを Player の Scene にして build する（`previousBuildReportDirectories` に content の build report の folder）。検査は、content の selected の root の GUID が Player の packed assets に無いこと、元の UICommon の Scene が入っていないこと、生成した Scene がちょうど 1 つの pack にあること。`build-receipt.json` は `scenes`（生成した Scene の path）と `bootstrapSceneName`（定数 `UICommon`）を持つ。Artifacts の `build collect` が読むのは 7 つの欄（`identity`・`target`・`transportPath`・`transportManifestSha256`・`playerBuildGuid`・`backend`・`stripping`。`tools/Artifacts/BuildContract.psm1:156`）で、この 2 欄は読まない。
  - **strip の材料:** `SampleGame/DependOnAll/link.xml` は `UnityEngine.UIElementsModule` の `UnityEngine.UIElements.*` を全て残す（content の UXML だけが使う型が strip された時に足した）。`BootstrapUiCommonLoader.EnsureEventSystem` は `EventSystem` と `InputSystemUIInputModule` を `AddComponent` で code から参照する。UICommon の Scene は `UICommon`（`OneStarMaker.Runtime`）、`PanelRenderer`、`EventSystem`、`InputSystemUIInputModule` の component を持つ。今の Player はこの Scene を build に含むので、これらの型は Scene からも保持される。Scene を build から外した時に何が strip されるかは測っていない。
  - **判定の test（`SampleGame/Tests/Editor/Build/PlayerKindEndToEndTests.cs`、`[Explicit]`）:** S4-1 の build 2 件と起動 7 件（`LaunchSmoke`・`LaunchSmokeOverride`・`LaunchPlayable`・`LaunchPlayableKindOverride`・`LaunchPlayableAddressables`・`LaunchPlayableMissingInstall`・`LaunchPlayableHuman`）。出力は `TestResults/s41/<S41_RUN_ID>/`。起動の前に継承した `SAMPLEGAME_*` を消す。
  - **公開文書:** 04 章 §4.1・§4.2・§4.6・§4.7 は、Player は生成した UICommon の bootstrap Scene から始まり graph metadata を使う、と 2 つの種類を分けずに書く。README の「試す」5 は起動の順に触れない。
  - Unity 上では何も実行していない（本 session は Unity も Windows も無い Cloud の container）。
- UC ごとの利用者の入力（どれも既存で、このスライスは入力を足さない・変えない）:

| UC | 利用者が選ぶ・入れるもの | 機械が決めるもの | 誰がいつ決めるか | 目的からの正当化 |
|---|---|---|---|---|
| UC1 遊べる Player を build する | menu `Tools/OSM/Content/Build Spring Playable Player`（既存）を選ぶ | 開始 Scene（空の Scene を生成する）、UICommon と map を Player に写さないこと、他は S4-1 のまま | 選ぶのは実行時の操作。他は開発時の宣言（coordinator の種類ごとの値）と自動導出 | 目的の「利用者の操作と入力を増やさない」。起動の順を選ばせないのは、順が種類（baked）で決まり、protected config を弱めないため |
| UC2 遊べる Player を起動して遊ぶ | exe の起動、窓の中の操作、窓を閉じる（既存） | 起動の順、UICommon と map を読む先（登録した content） | 実行時の操作。他は build の時に baked | 目的の「登録した content から読む」。読む先を起動の引数で選ばせない |
| UC3 検証済みの install した revision から起動する（任意） | `content:installedRevisionPath` と `content:manifestSha256`（既存） | 照合。UICommon と map もその revision から読む | 実行時の操作 | 既存の口。S4-2 がこの形の上に上書きを重ねる |

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 遊べる Player が、UICommon ではない空の Scene から始まり、Editor の directory Play と同じ順（content を登録 → UICommon → SceneResourceMap。どちらも content から）で起動できるか。動作確認の Player（`smoke`）の receipt と段の順は変えない（program §8 の S4-1a の行）。
- 判定定義: **GO** は §2.1 の不変条件と成立条件を全て満たした時。**NO-GO** はどれかを満たさない時。スパイクではないので CONDITIONAL ACCEPT は無い。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし。
- 決定記録: なし。

### 2.1 進める最低条件

**設計の要点（A1 の案。§3・§4 の根拠）:**

- **D1 順を種類で選ぶ（Framework）:** internal static `UsesBundledBootstrap(bool isPlayerConfiguration, PlayerContentKind kind)` を足す。Player の構成で種類が `Playable` でない時だけ真（今の Player の順）。Editor と遊べる Player は偽（content が先の順）。種類は `RouteFirstScene` と同じく検証済みの baked の値（`ReadBakedPlayerKind`）で、重ねた後の値ではない。`InitializeAfterSceneLoad` はこの値を最初に 1 回決め、下の D2〜D4 の 3 か所で使う。新しい baked の key、新しい hook、新しい公開 API は足さない。
- **D2 登録の位置:** 最初の登録（`:353-357`）の条件を「directory の設定があり、`UsesBundledBootstrap` が偽」にする。遊べる Player は Editor と同じく `AfterSceneLoad` の最初に `register-content-directory` を通る。後ろの登録（`:405-409`）は session が無い時だけ走る今の条件のままで、動作確認の Player だけが通る。登録の method（`InstallConfiguredContentDirectoryAsync`）は変えず、成功の後に 1 行の log `[AppInit] Content directory registered: identity=<identity> verified=<True|False>` を足す（Editor と 2 つの種類の Player のどれでも出る。起動の順を外から読む印。`verified` は設定が検証済みの installed revision を持つか。同梱の directory は `False`）。
- **D3 UICommon の読み口:** `BootstrapUiCommonSource` の Player の入力（今は `Func<bool> UseRequiredPlayerFileConfiguration`）を、initializer が決めた 3 値の規則（値）に変える。`BorrowOrLoad`（Player の構成でない。今の Editor と Addressables 互換の挙動）、`BorrowOnly`（同梱の順の Player。借用できなければ今の message で失敗）、`ContentOnly`（遊べる Player。active な Scene に UICommon があれば起動の失敗、無ければ logical key で content から読む。directory の設定が無ければ失敗）。規則と「active な Scene に UICommon があるか」「directory か」から、借用・content・Addressables・失敗（理由つき）を返す判定を internal static の純粋な関数にして表で固定する。loader は判定の結果（読んだ先）を internal で持ち、initializer が `[AppInit] Bootstrap UICommon: source=<active-scene|content|addressables>` の 1 行を出す。借用の検査（active な Scene の名前が `UICommon` で、root `UICommon` に component がある）と、content・Addressables の読み方、`EnsureEventSystem`、所有と `Dispose` は変えない。`WaitForPanelReadyAsync` の `required` は今のまま Player の構成で真。
- **D4 SceneResourceMap の読み口:** `UsesBundledBootstrap` が真なら `LoadPlayerSceneResourceMap`（今のまま）、偽で directory なら `LoadDirectorySceneResourceMapAsync`、どちらでもなければ `LoadSceneResourceMapAsync`。読んだ後に `[AppInit] Bootstrap SceneResourceMap: source=<player-resources|content|addressables>` の 1 行を出す。protected virtual の `GetDirectoryBootstrapSceneLogicalKey`・`GetDirectoryBootstrapMapLogicalKey` は遊べる Player でも呼ばれるようになる（signature は変えず、doc コメントと、2 つの読み口の例外の文言の「Directory Editor Play」を直す）。`LoadPlayerSceneResourceMap` は同梱の順の Player だけが呼ぶ。
- **D5 Player の構成の判定（SampleGame）:** 生成した開始 Scene の名前を成果物の marker にする今の形を保つ。`IsBs4PlayerMode` を「Editor でなく、active な Scene の名前が ordinal に `UICommon`（動作確認の Player）か `PlayablePlayerStart`（遊べる Player の開始 Scene）」にし、表の test のために internal にする。名前の定数は build（Editor の asmdef）と共有するので、public の `DirectoryBootstrapKeys` に `PlayableStartSceneName` として置く。marker を file（`bs4-runtime.json` の有無）に変えない理由: file が欠けた exe が Player の構成でなくなり、Addressables の起動へ落ちる（今の fail-closed を弱める）。開始 Scene の名前と baked の種類が食い違う exe（file を手で書き換えた時だけ起きる）は、D3 の規則が両方の向きで起動の失敗にする（`UICommon` の Scene と `playable` → `ContentOnly` の失敗。空の Scene と `smoke` → `BorrowOnly` の失敗）。
- **D6 Player の build（SampleGame の Editor）:** 種類ごとの値（`PlayerBuildKindSettings`）に「bootstrap を Player に写すか」を足す（`smoke` 真、`playable` 偽）。偽の時、coordinator は `CopyGraphClosure` と `CopyBootstrapScene` を呼ばず、mutation の新しい method が root の下に空の Scene `PlayablePlayerStart.unity` を生成し（`EditorSceneManager.NewScene(EmptyScene, Additive)` → 保存 → 閉じる。`PlaySnapshotSession.WriteEmptyStartScene` と同じ作り方。root の GameObject が 0 であることを検査する）、その Scene 1 つだけを Player の Scene にして build する。`PlayerBuildReportVerifier` は、今の 3 つの検査に加えて、渡された「Player に入ってはいけない GUID」（遊べる Player では SceneResourceMap と、map が参照する各 `SceneResource`。元の UICommon の Scene は今の引数のまま）が packed assets に無いことを検査する。`build-receipt.json` の `scenes` は開始 Scene の path、`bootstrapSceneName` は `PlayablePlayerStart` になる（欄は足さない・消さない）。mutation の所有の印・戻し・`bs4-runtime.json`・IL2CPP と High の設定は変えない。動作確認の Player の build の呼び出しの列と出力は変えない。
- **D7 変えないもの:** baked の key と値、protected の比較、種類の latch、`RouteFirstScene`、失敗の通知の順（`RunStartupFailureHooksAsync`）、失敗時の初回 Scene の unload、`WireContentResolution` の条件、`ContentOverlayStartup.Decide`、`PlayerContentStage` の列（動作確認の Player の receipt だけが読む。遊べる Player の列は観測に使わない）。

**不変条件（起きてはいけないこと）:**

- **I1 遊べる Player は、UICommon と SceneResourceMap を、登録した content 以外（exe に焼いた写し、Addressables）から読んで動かない。**
  - 主張を崩しうる経路: (a) loader の借用の分岐（active な Scene の名前が `UICommon` なら規則を見ずに借用する今の順）。(b) map の 3 分岐の条件の取り違え（Player の構成だけを見て `LoadPlayerSceneResourceMap` に入る）。(c) 遊べる Player の build が UICommon か map の写しを含み続ける（coordinator が種類を見ずに `CopyGraphClosure`・`CopyBootstrapScene` を呼ぶ、mutation の root の `Resources` に前の build の残りがある）。(d) 開始 Scene の名前と baked の種類の食い違い（D5）。(e) content に UICommon か map の root が無い directory（今の content の build は必ず合成する。無ければ `load-ui-common`・`load-scene-resource-map` の失敗になる）。(f) 登録より前に UICommon を読む順（D2 の条件の取り違え。登録の前の content の load は失敗する）。
  - 探索した境界: `InitializeAfterSceneLoad` の全ての分岐、`BootstrapUiCommonLoader`、`AppInitializer` の Player の override、coordinator・mutation・verifier。`IAssetManagement.LoadContentSceneAsync`・`LoadContentAssetAsync` の中（層の選び方）は 13 章の契約に依る。
  - 未確認: IL2CPP・High の Player で、content から読んだ UICommon の component が strip で欠けたまま動く形（u1。I1 の違反ではなく E1・E2 の不成立として見える）。
- **I2 動作確認の Player の契約は変わらない。** `smoke` の Player は、今と同じ receipt（file 名、schema v2、成功の stage `completed` と 10 段の順、失敗の stage と完了した段）と exit code で終わり、UICommon を生成した Scene から借用し、map を `Resources` の写しから読み、サービスの開始の後に content を登録する。
  - 経路: (a) `UsesBundledBootstrap` が `Smoke` を偽にする取り違え。(b) loader の規則の変更が借用の経路の検査と所有を変える。(c) coordinator の分岐が `smoke` の `CopyGraphClosure`・`CopyBootstrapScene`・検査・receipt の欄を変える。(d) verifier に足す引数が `smoke` の検査を変える。(e) 登録の後の新しい log の 1 行が receipt の段を変える（変えない。log だけ）。(f) latch の条件の変更が `UICommon` の名前の判定を落とす。
  - 探索した境界: I1 と同じ file と `PlayerContentSmoke`。
- **I3 起動に失敗した遊べる Player は動き続けない。** 新しい順でも、`BeforeSceneLoad` の失敗、登録の失敗（UICommon より前）、UICommon・map の load の失敗、その後の段の失敗のどれでも、Player は log に失敗の stage と例外を残して exit 1 で終わる。「失敗」は起動の段が例外を投げた時で、hang は含まない（対象外）。
  - 経路: (a) 登録が UICommon とサービスの開始より前に落ちる時の後始末（session が無く、UICommon も無い状態で `CleanupAfterStartupFailureAsync` と通知が動く）。(b) content から読んだ UICommon を持ったままの失敗の後始末（directory の close が UICommon の Scene を unload する。close の失敗は log に出して通知へ進む）。(c) latch の条件の変更で、遊べる Player が Player の構成と判定されず、config の欠けが Addressables の起動へ落ちる（D5）。(d) `ReadBakedPlayerKind` を `AfterSceneLoad` の最初に読む時、baked が無ければ例外（try の中で読み、失敗の経路に乗せる）。
  - 探索した境界: `BootstrapAfterSceneLoad`、`InitializeAfterSceneLoad` の try/catch、`CleanupAfterStartupFailureAsync`、`AssetManagement.CloseContentDirectoryAsync`、SampleGame の失敗の override。
  - 未確認: 失敗の後始末で UICommon の Scene を unload した後の `_uiCommonLoader.Dispose`（破棄済みの GameObject への `Destroy`）が Player で例外を出さないこと（Editor の directory Play の失敗では同じ順を通る。u4 の確認地点 AC10）。
- **I4 Player の構成と起動の順を、環境変数・command line で切り替えられない。** 遊べる Player に重ねる値で、同梱の順・別の種類・Addressables・別の content に切り替えて動き続けることは無い。config（`bs4-runtime.json`）が欠けた遊べる Player は Addressables で始めず、起動の失敗になる。
  - 経路: (a) latch の材料（開始 Scene の名前）は build の成果物で、config の層に無い。(b) 順を決める種類を重ねた後の値から読む取り違え。(c) `content:playerKind` の override（今の protected の比較が拒む）。
- **I5 Editor の起動の段の順と結果は変わらない。** directory Play（経路 Built、検証 Play、snapshot、上書きの Play）は今と同じく登録 → UICommon → map の順で、Addressables 互換の起動は content を登録しない。
  - 経路: (a) `UsesBundledBootstrap` が Editor で真になる取り違え。(b) loader の規則 `BorrowOrLoad` が今の Editor の挙動（借用できれば借用、できなければ directory か Addressables）と違う結果を返す。(c) 足す log の行を既存の観測（検証 Play の照合、`LogAssert`）が誤って拾う。
  - 探索した境界: `VerifyPlayLauncher` と Play の入口が照合する log の marker（`Content overlay registered`、`Editor content directory first scene`、`Content resolution:`。新しい 3 行はどれとも前方一致しない）。

**成立条件（起きなければならないこと）:**

- **E1** 遊べる Player を同梱の content から起動すると、空の開始 Scene から始まり、Player の log に「登録 → UICommon を content から → map を content から → 初回 Scene の安定」がこの順で出て、Title が安定し、Player は自分では終わらずに動き続ける。
- **E2** その Player の窓で Title の「スタート」を押すと Home を経て InGame へ進み、`Player ready at Cell stream spawn` になる（content から読んだ UICommon の UI が入力を受け付ける）。
- **E3** 遊べる Player の窓を閉じると、UICommon と map を content から保持したまま exit 0 で終わる（Title の上と InGame の上の両方）。
- **E4** 動作確認の Player は 6000.6.9f1 で今と同じ成功の receipt と exit 0 で終わる。
- **E5** 遊べる Player の出力は、Player の Scene が空の開始 Scene 1 つで、UICommon の Scene・SceneResourceMap・`SceneResource` の写しを含まない。構成（IL2CPP・High・development build でない）は S4-1 のまま。

### 2.2 受け入れ条件

全て §5 の判定必須テストか検証経路で閉じる。Player の起動を伴う条件の読み方（時間の制限を超えた起動は不合格で kill する、game の log は process が終わった後に読む、kill した起動の game の log は「観測なし」、Player の log と game の log を `[AppInit] Application quitting.` の行で閉じる前と閉じた後に分ける）は S4-1 の受け入れ条件と同じにする。strip の検出語は S4-1 の判定で確定した読み方（`MissingMethodException`、`stripped`、`is missing`、`Could not produce class`。`stripped` は、`Shader 'Hidden/Universal Render Pipeline/` で始まり `is not supported or has been stripped from the build` を含む行には当てない。owner の決定、2026-11-23）を使う。遊べる Player の窓の引数は `-screen-fullscreen 0 -screen-width 1280 -screen-height 720`。

| AC | 内容 | 検証する条件 |
|---|---|---|
| AC1 | 動作確認の Player の成功（S4-1 の AC1 と同じ起動）: `-batchmode` で 300 秒以内に exit 0、`bs4-smoke-receipt.json` は schema 2・`succeeded` 真・`stage` `completed`・10 段を今の順に全て持つ。transport manifest の `unityVersion` が `6000.6.9f1`。加えて Player の log に `Bootstrap UICommon: source=active-scene`、`Bootstrap SceneResourceMap: source=player-resources`、`Content directory registered:` がこの順にある。record に game の log の Error 以上の行を全て残す（AC4・AC6 の基準）。 | I2、E4 |
| AC2 | 動作確認の Player の protected（S4-1 の AC2 と同じ）: `SAMPLEGAME_CONTENT__PLAYERKIND=playable` で 60 秒以内に exit 1、receipt は `succeeded` 偽・`stage` `before-scene-load`・`completedStages` 空・`error` に `Protected Player`。 | I2、I4 |
| AC3 | build の record: (a) 遊べる Player は、`build-receipt.json` の `scenes` が開始 Scene 1 つで `bootstrapSceneName` が `PlayablePlayerStart`、verifier が「入ってはいけない GUID」（元の UICommon の Scene、SceneResourceMap、map が参照する `SceneResource` の全て）と selected の root の重複を検査して通ったこと、生成した開始 Scene の root の GameObject が 0 だったこと、出力に `GameAssembly.dll` があり `SampleGame_Data/Managed/` が無いこと、build report の options に `Development` が無いこと、build の間の managed stripping が High だったこと。(b) 動作確認の Player は、`scenes` が生成した `UICommon.unity` 1 つで `bootstrapSceneName` が `UICommon`（今のまま）。(c) 2 つの build の後で `Assets/Resources/BS4/<identity>/` と `Assets/StreamingAssets/bs4-runtime.json` が残っていない。 | E5、I1、I2 |
| AC4 | 遊べる Player の起動: 窓で起動すると 120 秒以内に、Player の log に次の 4 行がこの順に出る: `[AppInit] Content directory registered: identity=<build の identity> verified=False`、`[AppInit] Bootstrap UICommon: source=content`、`[AppInit] Bootstrap SceneResourceMap: source=content`、`[AppInit] Player content first scene: scene=Title … kind=playable stable=True`。最後の行の後 60 秒の間 Player は終わらない。閉じる前の Player の log に `source=active-scene`、`source=player-resources`、`run-player-content-smoke`、`unload-player-first-scene`、`close-player-content-directory`、`AfterSceneLoad failed`、`Exception`、strip の検出語が無く、exe の横に receipt が無い。AC5 で終わった後、閉じる前の game の log に `TitleScene` の `Loaded.` があり、Error 以上の行が無い（AC1 の基準と同じ category・message の行は既存として record に書いて owner へ返し、違反に数えない）。合格例: 4 行がこの順。不合格例: UICommon の行が登録の行より前、`source=active-scene`、60 秒の間に exit した。 | E1、I1 |
| AC5 | AC4 の Player の窓に閉じる要求を送ると、120 秒以内に exit 0 で終わる。 | E3 |
| AC6 | 初回 Scene の先と InGame での終わり（S4-1 の AC6 と同じ形）: 同じ build の遊べる Player を起動し、owner が Title の「スタート」を 1 回押し、InGame の画面が出たら窓の × で閉じる。(1) `[AppInit] Application quitting.` を test が見た時刻から 120 秒以内に exit 0。(2) 閉じる前の game の log に `OnStabledImpl HomeScene`、`[InGameSession] initial season ensure started`、`Player ready at Cell stream spawn` がこの順。(3) 閉じる前の game の log に Error 以上の行が無い（基準の扱いは AC4 と同じ。InGame の行は全て違反に数える）。(4) 閉じる前の Player の log に `AfterSceneLoad failed`、`Exception`、strip の検出語が無く、AC4 の 4 行がある。(5) owner の観察記録に、Title と InGame の画面が出て真っ黒・マゼンタでなく、Title の UI（「スタート」の button）が見えていたことと、両方の screenshot。(1)〜(4) は AI が record の log で判定する。(5) は人間の観察記録として受理する。 | E2、E3 |
| AC7 | 遊べる Player の protected（S4-1 の AC7 と同じ）: (a) `SAMPLEGAME_CONTENT__PLAYERKIND=smoke`、(b) `SAMPLEGAME_CONTENT__RUNTIMEMODE=addressables` でそれぞれ起動すると、60 秒以内に exit 1、Player の log に `Protected Player` を含む行があり、`Content directory registered:`・`Bootstrap UICommon:`・`Player content first scene` の行が無く、receipt が無い。 | I4、I3 |
| AC8 | `BeforeSceneLoad` の content の失敗（S4-1 の AC8 と同じ）: 存在しない `SAMPLEGAME_CONTENT__INSTALLEDREVISIONPATH` と 64 桁の `SAMPLEGAME_CONTENT__MANIFESTSHA256` で起動すると、60 秒以内に exit 1、Player の log に `ContentDeliveryException` の行と `BeforeSceneLoad が失敗したため AfterSceneLoad をスキップします` の行があり、`Content directory registered:`・`Player content first scene` の行が無く、receipt が無い。 | I3 |
| AC9 | 登録の失敗（UICommon より前）: 判定の test が写した遊べる Player の folder の同梱の `content/` の中の file を全て消し（folder は残す）、起動する。60 秒以内に exit 1、Player の log に `AfterSceneLoad failed at stage 'register-content-directory'` を含む行があり、`[AppInit] AfterSceneLoad: loading UICommon.`・`Content directory registered:`・`Player content first scene` の行が無く、receipt が無い。 | I3 |
| AC10 | UICommon と map を content から読んだ後の失敗: 遊べる Player を `SAMPLEGAME_WORLD__CELLCOMPANIONSET=Broken`（protected でない key。`CreateSceneFactory` が `FormatException` を投げる）で起動する。60 秒以内に exit 1、Player の log に `Bootstrap UICommon: source=content` と `Bootstrap SceneResourceMap: source=content` の行の後に `AfterSceneLoad failed at stage 'create-scene-factory'` を含む行があり、`Player content first scene` の行が無く、receipt が無い。後始末の失敗の行（`also failed`）は record に全文を残す（あっても exit 1 と時間で合否を読む）。 | I3 |
| AC11 | 単体テスト（表）: (1) `UsesBundledBootstrap`: (Editor, `Smoke`)・(Editor, `Playable`) → 偽、(Player, `Smoke`) → 真、(Player, `Playable`) → 偽。(2) UICommon の判定: `BorrowOrLoad` は (UICommon あり) → 借用、(なし, directory) → content、(なし, directory でない) → Addressables。`BorrowOnly` は (あり) → 借用、(なし) → 今の message の失敗。`ContentOnly` は (あり) → 失敗、(なし, directory) → content、(なし, directory でない) → 失敗。規則の選び方: Player の構成でない → `BorrowOrLoad`、`UsesBundledBootstrap` が真 → `BorrowOnly`、遊べる Player → `ContentOnly`。(3) SampleGame の latch: (Editor, どの名前) → 偽、(Player, `UICommon`) → 真、(Player, `PlayablePlayerStart`) → 真、(Player, `uicommon`・`PlayablePlayerStart ` ・`Title`・空) → 偽。(4) verifier: 入ってはいけない GUID が pack にある → 拒む、開始 Scene が 2 つの pack にある → 拒む、入ってはいけない GUID が空の呼び出し（`smoke`）は今の 3 つの検査だけで今の結果。(5) `SettingsOf`: `smoke` は bootstrap を写す、`playable` は写さない。今の値（宣言、probe、出力先、`playerKind`）と `RuntimeJson` の表は変わらない。 | I1、I2、I4、I5 |
| AC12 | Editor の非退行: 検証 Play（menu `Tools/OSM/Content/Verify Play/spring-full`）を 1 回行い、Editor の log に `Content directory registered: … verified=True`、`Bootstrap UICommon: source=content`、`Bootstrap SceneResourceMap: source=content`、`Editor content directory first scene: scene=Title … stable=True` がこの順にあり、`AfterSceneLoad failed` が無い。 | I5 |
| AC13 | 回帰: 判定必須の全 EditMode 回帰（§5）。 | I1〜I5 |

### 2.3 ここでは答えない問いと所有する後続

- 上書きを Player に重ねること、UICommon と map の編集が上書きで Player に届くことの確認（上書きの root に入る・上書きの map が上書きの側の `SceneResource` を指す）: S4-2（その AC12。Player は S4-2 まで上書きの key を拒むので、このスライスの Player では測れない）。
- 動作確認の Player の起動の順を content が先の順に揃えること: 所有する後続なし（owner の決定で変えない。変える時は receipt の段の順の契約を改める必要がある）。Phase D で program §10 の U11 に足す。
- Player での要求時解決の配線と record: S4-2 の §2.3 のまま（U11）。
- 起動の段の時間の上限（hang）: U11 (a)。
- `Resources/BS4/` などの `bs4` の名の整理（遊べる Player は `Resources/BS4/<identity>/` に開始 Scene と所有の印しか置かなくなる）: 所有する後続なし。Phase D で U11 に足す。
- Android・iOS: S4-5。
- 遊べる Player の UI の見た目の細部（描画の正しさ）: 対象外。AC6 (5) が見るのは画面が出て真っ黒・マゼンタでなく、Title の UI が見えることだけである。

### 2.4 スライス分割の判断

契機: 高リスクの初回実機スライスで、独立して失敗しうる未知が複数ある（UICommon を build から外した時の strip、空の Scene から始まる Player、content から保持した UICommon と map の quit、新しい順の失敗の見え方）。

判断: **分けない。** 理由: (1) どの未知も、この変更を入れて build した遊べる Player でしか観測できず、Framework の順の選択と SampleGame の build の変更のどちらか一方だけでは起動できる Player にならない。(2) 未知ごとの成立は、成立条件 E1（起動と順）・E2（UI の入力）・E3（quit）と I3 の AC9・AC10（失敗の見え方）で別々に判定でき、1 つの最低条件に束ねていない。(3) 先に測るだけのスパイクを置いても、測る対象の Player は製品の変更とほぼ同じ物になる（変更は順の分岐と build の写しの有無で、実験 code として捨てる部分が無い）。strip が不成立の時の対応（`link.xml`）は §4 の B 適応に置く。

### 2.5 A3 で決める点（A1 の案）

1. **Player の構成の marker:** 開始 Scene の名前のまま（D5）。
2. **順の印:** 3 つの log の行を足す（D2〜D4。Editor の起動でも出る）。
3. **owner の確認 (A) AC6 の「スタート」の押下と画面の観察を owner が 1 回行うこと**（S4-1 の AC6 と同じ担当の形。UICommon を content から読んだ UI が入力を受け付けるかは、押下でしか確かめられない）。
4. **owner の確認 (B) 「上書きの map が上書きの側の `SceneResource` を指すか」を S4-2 へ送ること**（§2.3。入力のコメントは未測定として挙げたが、Player が上書きを受け付けるのは S4-2 からである）。

## 3. 責務マップ

| file（`unity/Assets/` から） | 責務（変更理由） | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|
| `OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs` | 起動の 3 フェーズの順と失敗境界（`UsesBundledBootstrap` の判定を足し、登録・UICommon・map の 3 か所の分岐をその値で選ぶ。順の印の log 3 行。hook の doc コメント） | App | `BootstrapUiCommonLoader`、`PlayerContentKind` | 変えない（protected virtual の signature は同じ。doc だけ） | `PlayerContentConfigurationTests`（表）、AC1・AC4・AC7〜AC10・AC12 | 1425 → 1450。500 行を超えるが、起動の orchestration の 1 責務で、足すのは分岐の配線と log だけ（新しい判断は internal static の 1 関数と loader の判定に置く）。非分割 |
| `OneStarMaker/Scripts/Runtime/Bootstrap/BootstrapUiCommonLoader.cs` | UICommon の取得と所有（Player の入力を 3 値の規則に変え、借用・content・Addressables・失敗の判定を純粋な関数に出す。読んだ先を持つ） | App（initializer の field。今のまま） | `IAssetManagement`、`SceneManager` | internal | 新しい `BootstrapUiCommonDecisionTests`（表。Unity の Scene を使わない） | 172 → 215 |
| `SampleGame/DependOnAll/AppInitializer.cs` | SampleGame の起動の hook（Player の構成の latch に開始 Scene の名前を足す。`IsBs4PlayerMode` を internal に） | App | `DirectoryBootstrapKeys` | 変えない | 新しい latch の表の test（`SampleGame.Tests`） | 460 → 465 |
| `SampleGame/DependOnAll/DirectoryBootstrapKeys.cs` | build と Runtime が共有する bootstrap の名前の定数（開始 Scene の名前を足す。summary を直す） | 状態なし | なし | public（定数 1 つを足す） | AC11 (3) | 12 → 16 |
| `SampleGame/DependOnAll/Editor/Build/SampleGamePlayerBuildCoordinator.cs` | Player の build の手順（種類ごとの値に「bootstrap を写すか」を足し、写さない時は開始 Scene を作って build し、verifier に入ってはいけない GUID を渡し、receipt の 2 欄を開始 Scene の値にする。`PlayerBuildResult` に判定の record 用の値を足す） | Editor の操作 | mutation、adapter、verifier | menu は変えない | `Bs4FixtureComposerTests`（`SettingsOf`）、判定の build | 229 → 265 |
| `SampleGame/DependOnAll/Editor/Build/PlayerBuildProjectMutation.cs` | Player の build の project-global mutation（空の開始 Scene を root に生成する method を足す。所有の印と戻しは変えない） | build の 1 回（`using`） | `EditorSceneManager` | internal | 判定の build（AC3） | 148 → 175 |
| `SampleGame/DependOnAll/Editor/Build/PlayerBuildReportVerifier.cs` | Player の build report の検査（入ってはいけない GUID の検査を足す） | 状態なし | なし | internal | `PlayerBuildIntegrationPolicyTests` | 62 → 75 |
| `SampleGame/Tests/Editor/Build/PlayerKindEndToEndTests.cs` | 判定の Player の build・起動・観測（新しい log の行の照合、AC9・AC10 の起動 2 件、AC3 の record の欄） | test | 上の全て | test | — | 462 → 540。500 行を超える。判定の case の宣言的な列の 1 責務で、足すのは case 2 件と照合の語だけ。非分割 |
| `OneStarMaker/Tests/BuildContent/PlayerContentConfigurationTests.cs`、`OneStarMaker/Tests/Bootstrap/BootstrapUiCommonDecisionTests.cs`（新規）、`SampleGame/Tests/Bootstrap/PlayerModeLatchTests.cs`（新規）、`SampleGame/Tests/Editor/Build/PlayerBuildIntegrationPolicyTests.cs`、`SampleGame/Tests/Editor/Build/Bs4FixtureComposerTests.cs` | AC11 の表 | test | — | test | — | 各 +15〜60 |
| `unity/Assets/Docs/Architecture/04-app-startup.md` | 公開文書（Phase D の harvest で §4.1・§4.2・§4.6・§4.7 を直す。Phase B は変えない） | — | — | — | docs-audit | — |

namespace・asmdef は既存の file と同じ。新しい asmdef 参照、新しい Framework の公開 API、新しい `SceneState`、新しい config の key は足さない。新規の `.cs` と編集する `.cs` は先頭に `#nullable enable` を置き、`record` を使わない。

## 4. 実装計画

- 順序: (1) `UsesBundledBootstrap` と表。(2) loader の規則と判定の関数と表。(3) initializer の 3 か所の分岐と log。(4) SampleGame の latch と定数と表。(5) verifier の検査と表。(6) mutation の開始 Scene の生成、coordinator の分岐、receipt。(7) 判定の test の case と照合の語。
- 本文へ転記した実装制約: Unity の偽 null（`UnityEngine.Object` は `== null`・`!= null` で判定する）。Editor の code を Runtime の asmdef に置かない。公開 API の log の抽象は `ILogger<T>`（起動の `Debug.Log` の行は今の initializer と同じ形で、公開 API ではない）。テストで `Task.Delay`・`Thread.Sleep` を使わない（判定の test の待ちは今の `WaitForExit`・`WaitForLine` の形を使う）。path は `tools/path-budget.json` の予算に従う。
- Phase B から Phase A へ差し戻す条件: 遊べる Player を content が先の順にするのに、新しい baked の key・新しい Framework の公開 API・新しい hook が要る。`ContentDirectorySession`・`AssetManagement` の登録・close・quit の経路の変更が要る（u3・u4 の不成立）。動作確認の Player の順か receipt を変えないと成り立たない。空の開始 Scene では Player が起動しない（u2）。新しい asmdef 参照が要る。
- B 適応の例: IL2CPP・High で UICommon の型が欠けた時に `SampleGame/DependOnAll/link.xml` へ足す、UICommon の Scene が持つ component の型（u1。足した型と、動作確認の Player の build にも効くことを §6 に記録する）。開始 Scene の生成の Editor API の呼び方。判定の test の log の読み直しの間隔。例に当たるだけで許可済みとはせず、A3 と常時契約に照合する。
- 対象外を維持する方法: `smoke` の build と起動の呼び出しの列、`PlayerContentConfiguration.Resolve`、`ContentOverlayStartup`、`WireContentResolution` の条件、`RuntimeJson`、Editor の Play の入口の code を変えない。

## 5. テストとレビュー計画

- 単体テスト: AC11 の 5 つの表（§3 の test の file）と、既存の `PlayerContentConfigurationTests`・`PlayerContentSmokeSequenceTests`・`Bs4FixtureComposerTests`・`PlayerBuildIntegrationPolicyTests`。
- 差し戻し中の起点 `-Filter`: `OneStarMaker.Tests.BuildContent.PlayerContentConfigurationTests;OneStarMaker.Tests.BuildContent.PlayerContentSmokeSequenceTests;OneStarMaker.Tests.Bootstrap.BootstrapUiCommonDecisionTests;OneStarMaker.Tests.Editor.Build.Bs4FixtureComposerTests;OneStarMaker.Tests.Editor.Build.PlayerBuildIntegrationPolicyTests` と latch の表の test class（名前は B が決めて §6 に書く）。
- 判定必須テスト（GO 候補 head）: 次を順に、それぞれ別の Unity process で実行する（環境変数 `S41_RUN_ID` を全てで同じ値にする。`T` は `OneStarMaker.Tests.Editor.Build.PlayerKindEndToEndTests`）。(1) 起点 filter。(2) `pwsh tools/run-tests.ps1 -Filter T.BuildSmoke`。(3) `T.BuildPlayable`（AC3）。(4) `T.LaunchSmoke`（AC1）。(5) `T.LaunchSmokeOverride`（AC2）。(6) `T.LaunchPlayable`（AC4・AC5）。(7) `T.LaunchPlayableKindOverride`（AC7 (a)）。(8) `T.LaunchPlayableAddressables`（AC7 (b)）。(9) `T.LaunchPlayableMissingInstall`（AC8）。(10) AC9 の case。(11) AC10 の case（(10)(11) の case 名は B が決めて §6 に書く）。(12) 全 EditMode 回帰（`pwsh tools/run-tests.ps1`）。AC6 の `T.LaunchPlayableHuman` は高コスト証拠で、下の順で取る。
- 判定の読み方: S4-1 と同じにする。(2)(3) は Project Auditor の post-build 監査で `step.json` が `failed` になりうるので、合否は XML と record で読む。(4)〜(11) の XML の `Passed` は record を書き終えたことだけを示し、AC の合否は record の行で読む。起動の case の再実行は別の `S41_RUN_ID` で (2) からやり直し、両方の record を残す。全 EditMode の合否は Failed が 0 で、Inconclusive は S4-1 の判定 C（run `jc5`）の全 EditMode の XML で Inconclusive だった test の名前の集合（VFX の 25 件）に含まれる物だけを受け入れる。staging の `Directory.Move` の access denied で落ちた test は、その test だけを 1 回取り直し、通れば受け入れて record に書く（program の S5-0、owner の既知の受容）。
- 全 EditMode 回帰の適用除外: なし。
- 統合・Unity テスト: 上の (2)〜(11) と AC6・AC12。
- 操作・実行時・目視条件の検証経路:
  - AC1〜AC5・AC7〜AC10: 上の (2)〜(11) で閉じる。担当は Phase C の実行担当（Unity の batch と Player の起動。窓を出せる、利用者の desktop の session で実行する）。初期状態: Editor を閉じ、Active Build Target が StandaloneWindows64、作業ツリーが clean、他の Unity と `SampleGame.exe` が動いていない。事前に `git status --porcelain` を取る。観測は record・各 case の Player の log と game の log・receipt・build の結果・XML。事後に `git status --porcelain` を取り（mutation の戻しの確認）、record の pid が全て終わっていることを CIM で確かめる。証拠は `TestResults/s41/<run id>/` の全て（写した Player の本体を除く）と XML を evidence bundle に入れる。
  - AC6: 担当は owner と Phase C の実行担当。実行担当が `T.LaunchPlayableHuman` を起動して owner に知らせ、owner が Title の screenshot を撮ってから「スタート」を 1 回押し、InGame の画面が出たら screenshot を撮って窓の × で閉じる。(1)〜(4) の判定は判定 C が record の行で行う。owner は、押した時刻、窓を閉じた時刻、対象の build identity、(5) の観察を §7 に記録する。
  - AC12: 担当は Phase C の実行担当。[osm-unity-editor](../../.agents/skills/osm-unity-editor/SKILL.md) に従って Editor を開き、menu を `tools/unity-editor.cmd` で呼び、Play に入って初回 Scene の行が出たら Play を止め、Editor の log の該当の行を record に写す。検証 Play は content の build と install を伴う（既存の経路）。
  - 動作確認（Phase B の後）: `対象なし`（offline のテストで閉じない条件は全て Player の build か Editor の Play を要し、Phase C が担当する）。
- 未知の操作経路の疎通: 次は未確認で、**初回確認は Phase C**（理由: Unity の test と Build の実行は Phase C の責任で、本 session は Unity を動かせない。担当: Phase C の実行担当）。
  - (u1) UICommon の Scene を build から外した IL2CPP・High の Player で、content から読んだ UICommon（`UICommon`・`PanelRenderer`・`EventSystem`・`InputSystemUIInputModule`）が strip で欠けずに動くか。確認地点: AC4（panel の ready と Title の安定）と AC6（「スタート」の入力）。不成立時: UICommon の Scene が持つ component の型を `link.xml` に足すのは B 適応（§4）。それで足りなければ Phase A へ戻す。
  - (u2) root の GameObject が 0 の Scene 1 つだけを Scene にした Player が起動し、`BeforeSceneLoad` で作る Camera と `AfterSceneLoad` の段が動くか。確認地点: AC4。不成立時: Phase A へ戻す（開始 Scene に物を置くかは設計判断）。
  - (u3) UICommon と map を content から保持したままの quit が Player で exit 0 になるか。確認地点: AC5 と AC6 (1)。不成立時: Phase A へ戻す（`ContentDirectorySession` の終了の経路を変えるかは設計判断）。
  - (u4) 新しい順の失敗（登録の失敗、UICommon を content から読んだ後の失敗）で、後始末と通知が exit 1 に届くか。確認地点: AC9・AC10。不成立時: 後始末の順の変更は Phase A へ戻す。同梱の `content/` を空にしても登録が失敗しない時は、壊し方（消す file）を変えるのは B 適応で、stage の名前が変わる時は決定記録で読み方を確定する。
  - (u5) mutation の中で `NewScene(EmptyScene, Additive)` → 保存 → 閉じる、が batch の test の中（開いている Scene が untitled の時を含む）で動くか。確認地点: (3)。不成立時: 生成の Editor API の呼び方を変えるのは B 適応。
  - S4-1 が確かめた経路（製品の coordinator を test から呼ぶ build、窓を出した Player の起動と閉じる要求、追記中の log の読み直し）は既知で、再確認しない。Player の publish の `Directory.Move` の access denied（S4-1 の u1b）は、落ちた時に S4-1 と同じ再実行の規則に従う。
- 人間の判断が必要な条件: AC6 の「スタート」の 1 回の押下、窓を閉じること、(5) の画面の観察と screenshot。担当は owner。(1)〜(4) の判定は AI が log で行う（観察記録で判定を置き換えない）。(5) は観察記録として受理し、監査担当が画像を再評価できることまでは求めない（screenshot は evidence に入れる）。§2.5 の 3。
- 高コスト証拠: AC6（owner の押下と観察）だけ。取得は暫定 C' の阻害指摘が無くなった head で 1 回。期待値は AC6 の文で、実装から独立している。他の AC は Player の build が外部への作用を持たずこの PC で繰り返せるので、GO 候補 head ごとに取る（build 2 回、各約 6 分の見込み。S4-0・S4-1 の実測）。
- 判定 C の照合 script の方式: なし。判定 C は record と receipt と log を AC ごとに読む。
- 機械検査: `contract-audit`、`docs-audit`。
- A0/A1 主担当: 本 session（Cloud の Claude Code の session、Anthropic の系列）。A0 は program §8 の S4-1a の行（PR #212 の branch）、PR #212 のコメント「owner との会話で決まったことと、S4-1a の入力」、同 PR の S4-2 の統合案が S4-1a について書く箇所、program の「S4-1 から移送した入力」と U11、04 章、S4-1 の HANDOFF（git の履歴）を入力とした。
- C' 用に予約した担当: Phase A・B・C に関与していないベンダー（OpenAI の系列か Grok の系列のうち、外部計画レビューに使わなかった方。`cursor-agent` を使う場合は AGENTS.md の契約どおり Grok 系だけ）。A2 では Anthropic の系列だけを使う。
- 独立性の強化条件を満たせない場合の理由: C' を起こす時に予約した系列が使えなければ、人間が C' を担当するか、`独立性制約あり` として理由と残存リスクを §8 に記録して owner が採否を決める。

### 5.1 A2 の独立レビューと A3 の採否

未実施。

## 6. Phase B 実装結果

未着手。

## 7. Phase C

未着手。

## 8. Phase C'

未着手。

## 9. Phase D

未着手。
