# 遊べる Player の起動の順を Editor の directory Play と揃える

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S4-1a）
- status: `A`（Phase A の revision 0。A1 の初稿、A2 の独立レビュー、A3 の統合案、外部計画レビュー 2 巡の採否、§2.5 の owner の決定まで。**凍結していない**）
- branch: `claude/gracious-noether-o0vn1y`（PR の base は `develop`）
- implementation base commit: `116b00918dbdefa9f2dff5aeec7e43ef4a8b4a30`（`develop` の head、2026-11-23。§1 の現況と A2 の固定 base もこれ）
- implementation head commit: 未固定
- A3 凍結記録: 未凍結
- risk: `high`（Framework の起動の段の順（`AbstractApplicationInitializer`、`BootstrapUiCommonLoader`）と SampleGame の Player の構成の判定、Player の build を変える。UICommon を Player の build から外した IL2CPP・High の Player は初めてで、独立して失敗しうる未知が複数ある（§5 の u1〜u7））
- owner: root（依頼者）
- created: 2026-11-24
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: [04 章](../../unity/Assets/Docs/Architecture/04-app-startup.md) §4.1・§4.2・§4.6・§4.7（Player の 2 つの種類の起動の段、Player の構成の latch）、[18 章](../../unity/Assets/Docs/Architecture/18-asset-description.md)の Player が bootstrap の Scene と graph metadata を使うと書く箇所、repo の README の「試す」5 の確認の記録、[実測の事実](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)（UICommon を Player の build から外した時の strip の実測）、[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S4-1a の行、S4-2 へ送る入力、§10 の U11（§2.3 の所有の無い項目）
- 関連: [program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S4-1・S4-1a・S4-2 の行、「S4-1 から移送した入力」、§10 の U11。入力は [PR #212](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/212) のコメント「owner との会話で決まったことと、S4-1a の入力」（2026-11-24）と、同 PR の S4-2 の統合案（未凍結）が S4-1a について書く箇所
- PR #212 との順: この branch は `develop` から切り、program の S4-1a の行だけを足した。PR #212 も同じ位置に S4-1a の行を足すので、後から merge する側で 1 行の衝突が出る。本書へのリンクを持つこの branch の行を残す。
- Phase A snapshot の取得案内・状態: 該当なし（機械向けの期待値・照合 script は無い。§5。レビューの入力は本書と program だけ）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: Phase A の A3（外部計画レビューの指摘の採否、owner の確認、owner の凍結判断）。
  - 次の作業: (1) 追加の外部計画レビューがあれば、指摘を §5.1 の A3 の表へ採否として足す（設計・受け入れ条件を変えたら、影響する A2 観点の再レビューの要否と理由も書く）。(2) owner の凍結判断を受ける。凍結するなら A3 凍結記録・本票（次の担当を Phase B へ）・PR の状態ブロックを改めて、凍結版を commit する。§2.5 の (A)〜(F) は owner が決めた（2026-11-24）。
  - 読む範囲: 全節。
  - 引き継ぐ commit: この票を含む commit（SHA は起動指示と PR の状態ブロックで渡す）。
  - 必要な権限: 文書だけ。push は公開点の規則に従う。
  - 停止規則: 目的・利用者の欄を変える指摘は owner が裁定し、変えたら目的レビューをやり直す（§5.1）。
  - 引き渡さないもの: なし。
- task の開始（`tools/workflow-task.ps1 start`）: 未実行。Phase A の担当は Windows の store root の無い Cloud の session で、実行できなかった。ローカルで引き継ぐ担当が行う。

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
  - **Player の構成の判定（`SampleGame/DependOnAll/AppInitializer.cs:64-70,137-139`）:** `BeforeSceneLoad` の冒頭で「Editor でなく、active な Scene の名前が ordinal に `UICommon`」の時に `_useBs4PlayerMode` を真にし、`UseRequiredPlayerFileConfiguration` がそれを返す。code の注記は、生成した bootstrap の Scene の名前を config とは独立した成果物の marker として読み、config が欠けた時も通常の Addressables へ戻さず required provider で fail-closed にする、と書く。baked（`bs4-runtime.json`）はこの判定が真の時にだけ読まれるので、種類（`content:playerKind`）は判定の材料にできない。環境変数と command line では有効にならない（04 章 §4.7）。
  - **`BeforeSceneLoad`（`OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs:255-322`）:** `BuildConfig`（`:984-1012`。Player の構成なら `StreamingAssets/bs4-runtime.json` を必須で読み、種類を latch し、baked を固定する）→ `ReadContentDirectoryConfiguration`（`:1170-1241`。Player の分岐は protected の比較と installed の pair の照合。pair が無ければ exe の横の同梱 directory）→ directory なら Addressables を封じる（`:275-284`）。この順は 2 つの種類で同じで、このスライスでは変えない。
  - **`AfterSceneLoad` の順（`:338-520`）:**

    | 段 | Editor の directory Play | Player（今。2 つの種類で同じ） |
    |---|---|---|
    | 1 | `register-content-directory`（`:353-357`。条件は directory の設定があり Player の構成でない） | `load-ui-common`: active な Scene の UICommon を借用（`BootstrapUiCommonLoader.cs:75-88`）。無ければ `The Player bootstrap scene must contain UICommon; Addressables fallback is forbidden.`（`:90-91`） |
    | 2 | `load-ui-common`: logical key で `LoadContentSceneAsync`（`BootstrapUiCommonLoader.cs:94-104`）→ `EnsureEventSystem`（`:160-170`） | `load-scene-resource-map`: `LoadPlayerSceneResourceMap`（SampleGame は `Resources.Load("BS4/<identity>/SceneResourceMap")`。`AppInitializer.cs:141-146`） |
    | 3 | `load-scene-resource-map`: `LoadDirectorySceneResourceMapAsync`（`:934-947`。logical key で `LoadContentAssetAsync`） | サービスの開始、scene factory |
    | 4 | サービスの開始、scene factory | `register-content-directory`（`:405-409`） |
    | 5 | 要求時解決の配線、`SceneDirector`、既ロードの登録か snapshot、初回 Scene（`RouteFirstScene`） | `SceneDirector`（配線なし）、既ロードの登録、初回 Scene（`Smoke` は動作確認の尾、`Playable` は load したまま終える） |

    Editor の directory の session を UICommon と map より先に登録することは、04 章 §4.7 が公開文書として書いている。
  - **登録（`:817-849`）:** `InstallConfiguredContentDirectoryAsync` は 2 つの位置から同じ method を呼ぶ。上書きの無い登録は log を出さない（上書きの時だけ 1 行）。
  - **失敗と終わり（`:501-514,705-754,1021-1100`）:** `AfterSceneLoad` の例外は、Player の構成なら初回 Scene の unload、directory の close（`AssetManagement.CloseContentDirectoryAsync` は台帳の directory の Scene を unload してから close する。`AssetManagement.ContentDirectory.cs:35-56`。Object の handle が生きていれば close は失敗し、log に出して次へ進む）、派生の後始末と失敗の通知（別々の try）、`ReleaseAll` の順。SampleGame の失敗の通知は遊べる Player なら receipt を書かずに `Application.Quit(1)`（`AppInitializer.cs:160-178`）。窓を閉じると `Application.quitting` → `ReleaseAll` が directory を `CompletePlayStop` で解除する（`:1073-1078`）。S4-1 は、UICommon が借用（content の物でない）の遊べる Player で、Title と InGame の上の quit が exit 0 になることを確かめた。UICommon と map を content から保持したままの quit は、Editor の Play の停止では通っているが、Player では通っていない。
  - **content の build（`SampleGame/DependOnAll/Editor/Build/SampleGameContentBuild.cs:83`、`SampleGameBootstrapContentComposer.cs`）:** どの宣言の build でも、UICommon の Scene（`Assets/OneStarMaker/Scenes/UISystem/UIScene.unity`、logical key `samplegame:bootstrap:uicommon`）と SceneResourceMap（`Assets/OneStarMakerCommon/SceneMap/SceneResourceMap.asset`、logical key `samplegame:bootstrap:scene-resource-map`）を表現 `Full` の root として合成する。遊べる Player の同梱の `content/` と、同じ build の installed revision のどちらにも入っている。content から読んだ UICommon の Scene の Unity の上の名前は `UIScene` で、map に無いので既ロードの登録は飛ばす。
  - **Player の build（`SampleGamePlayerBuildCoordinator.cs:70-114`、`PlayerBuildProjectMutation.cs`、`UnityPlayerBuildAdapter.cs`、`PlayerBuildReportVerifier.cs`）:** 2 つの種類で同じく、mutation の root `Assets/Resources/BS4/<identity>/` に SceneResourceMap と各 `SceneResource` を `AssetDatabase.CopyAsset` で写して参照を書き換え（`CopyGraphClosure`。写しは新しい GUID を持つ）、UICommon の Scene を別の GUID の `UICommon.unity` として写し（`CopyBootstrapScene`。UICommon の root が 1 つで EventSystem があることを検査する）、その Scene 1 つだけを Player の Scene にして build する（`previousBuildReportDirectories` に content の build report の folder）。検査は、content の selected の root の GUID（元の UICommon の Scene と元の map を含む）が Player の packed assets に無いこと、元の UICommon の Scene が入っていないこと、生成した Scene がちょうど 1 つの pack にあること。`build-receipt.json` は `scenes`（生成した Scene の path）と `bootstrapSceneName`（定数 `UICommon`）を持つ。Artifacts の `build collect` が読むのは 7 つの欄（`tools/Artifacts/BuildContract.psm1:156`）で、この 2 欄を読む code は repo に無い。
  - **strip の材料:** `SampleGame/DependOnAll/link.xml` は `UnityEngine.UIElementsModule` の `UnityEngine.UIElements.*` を全て残す（content の UXML だけが使う型が strip された時に足した）。`BootstrapUiCommonLoader.EnsureEventSystem` は `EventSystem` と `InputSystemUIInputModule` を `AddComponent` で code から参照する。UICommon の Scene は `UICommon`（`OneStarMaker.Runtime`）、`PanelRenderer`（native の class）、`EventSystem`、`InputSystemUIInputModule` の component を持つ。今の Player は UICommon の Scene と、map・`SceneResource` の写しを build に含むので、これらの型は Player の build からも保持される。build から外した時に何が strip されるかは測っていない。
  - **判定の test（`SampleGame/Tests/Editor/Build/PlayerKindEndToEndTests.cs`、`[Explicit]`）:** S4-1 の build 2 件と起動 7 件（`LaunchSmoke`・`LaunchSmokeOverride`・`LaunchPlayable`・`LaunchPlayableKindOverride`・`LaunchPlayableAddressables`・`LaunchPlayableMissingInstall`・`LaunchPlayableHuman`）。出力は `TestResults/s41/<S41_RUN_ID>/`。種類ごとに 1 つの Player の写し（`<run>/<種類>/player/`）を全ての起動の case が使う。起動の前に継承した `SAMPLEGAME_*` を消す。
  - **公開文書:** 04 章 §4.1・§4.2・§4.6・§4.7 と 18 章は、Player は生成した UICommon の bootstrap Scene から始まり graph metadata を使う、と 2 つの種類を分けずに書く。README の「試す」5 は起動の順に触れない。
  - Unity 上では何も実行していない（Phase A の担当は Unity も Windows も無い Cloud の container）。
- UC ごとの利用者の入力（どれも既存で、このスライスは入力を足さない・変えない）:

| UC | 利用者が選ぶ・入れるもの | 機械が決めるもの | 誰がいつ決めるか | 目的からの正当化 |
|---|---|---|---|---|
| UC1 遊べる Player を build する | menu `Tools/OSM/Content/Build Spring Playable Player`（既存）を選ぶ | 開始 Scene（commit した空の Scene）、UICommon と map を Player に写さないこと、他は S4-1 のまま | 選ぶのは実行時の操作。他は開発時の宣言（coordinator の種類ごとの値）と自動導出 | 目的の「利用者の操作と入力を増やさない」。起動の順を選ばせないのは、順が種類（baked）で決まり、protected config を弱めないため |
| UC2 遊べる Player を起動して遊ぶ | exe の起動、窓の中の操作、窓を閉じる（既存） | 起動の順、UICommon と map を読む先（登録した content） | 実行時の操作。他は build の時に baked | 目的の「登録した content から読む」。読む先を起動の引数で選ばせない |
| UC3 検証済みの install した revision から起動する（任意） | `content:installedRevisionPath` と `content:manifestSha256`（既存） | 照合。UICommon と map もその revision から読む | 実行時の操作 | 既存の口。S4-2 がこの形の上に上書きを重ねる（成功の起動の確認は S4-2。§2.3） |

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 遊べる Player が、UICommon ではない空の Scene から始まり、Editor の directory Play と同じ順（content を登録 → UICommon → SceneResourceMap。どちらも content から）で起動できるか。動作確認の Player（`smoke`）の receipt と段の順は変えない（program §8 の S4-1a の行）。
- 判定定義: **GO** は §2.1 の不変条件と成立条件を全て満たした時。**NO-GO** はどれかを満たさない時。スパイクではないので CONDITIONAL ACCEPT は無い。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし。
- 決定記録: なし。

### 2.1 進める最低条件

語: **写しの順** は今の Player の順（UICommon を開始 Scene から借用し、map を `Resources` の写しから読み、サービスの開始の後に登録する）、**content が先の順** は Editor の directory Play の順（登録 → UICommon → map。どちらも content から）を指す。「同梱」は exe の横の `content/` にだけ使う。

**設計の要点（A1 の案と A3 の改訂。§3・§4 の根拠）:**

- **D1 起動の構成を 1 つの判定で選ぶ（Framework の policy）:** 新しい internal static の `BootstrapSourcePolicy`（Unity を呼ばない）に、判定を 2 つ置く。
  - `Plan(Player の構成か, 種類, directory か)` は、登録を先にするか・UICommon の規則・map の読み口の 3 つを一度に返す。

    | Player の構成 | 種類 | directory | 登録を先に | UICommon の規則 | map の読み口 |
    |---|---|---|---|---|---|
    | 偽 | — | 真 | 真 | `BorrowOrLoad` | content |
    | 偽 | — | 偽 | 偽 | `BorrowOrLoad` | Addressables |
    | 真 | `Smoke` | 真・偽 | 偽 | `BorrowOnly` | Player の `Resources`（`LoadPlayerSceneResourceMap`） |
    | 真 | `Playable` | 真 | 真 | `ContentOnly` | content |
    | 真 | `Playable` | 偽 | 失敗（`A playable Player requires a content directory.`） | — | — |

    上の 3 行は今の分岐と同じ結果である。種類は `RouteFirstScene` と同じく検証済みの baked の値（`ReadBakedPlayerKind`）で、重ねた後の値ではない。
  - `DecideUiCommon(規則, active な Scene に UICommon があるか, directory か)` は、借用・content・Addressables・失敗（理由つき）を返す。`BorrowOrLoad` は (あり) → 借用、(なし, directory) → content、(なし, directory でない) → Addressables（今の Editor と Addressables 互換の挙動）。`BorrowOnly` は (あり) → 借用、(なし) → 今の message の失敗。`ContentOnly` は (あり) → 失敗（`A playable Player must not start from a scene that contains UICommon.`）、(なし, directory) → content、(なし, directory でない) → 失敗。
  - `InitializeAfterSceneLoad` は try の最初に stage `plan-bootstrap` で `Plan` を 1 回呼び（失敗は起動の失敗）、下の D2〜D4 の 3 か所でその結果を使う。新しい baked の key、新しい hook、新しい公開 API は足さない。
- **D2 登録の位置:** 最初の登録（`:353-357`）の条件を「`Plan` が登録を先にと返した」にする。遊べる Player は Editor と同じく `AfterSceneLoad` の最初に `register-content-directory` を通る。後ろの登録（`:405-409`）は session が無い時だけ走る今の条件のままで、動作確認の Player だけが通る。`InstallConfiguredContentDirectoryAsync` は、session を field に入れた後（method の最後）に 1 行の log `[AppInit] Content directory registered: identity=<identity> from=<installed|directory>` を足す（`installed` は設定が installed の pair を持つ時、`directory` は path の指定で、同梱の directory を含む。Editor と 2 つの種類の Player のどれでも出る）。
- **D3 UICommon の読み口:** `BootstrapUiCommonSource` の Player の入力（今は `Func<bool> UseRequiredPlayerFileConfiguration`）を、`Plan` が返した規則の値に変える。loader は active な Scene の検査（名前が `UICommon` で、root `UICommon` に component がある。今のまま）の結果と規則を `DecideUiCommon` に渡し、結果のとおりに動く。`LoadAsync` は UICommon と読んだ先を一緒に返し（loader の状態は増やさない）、initializer が `[AppInit] Bootstrap UICommon: source=<active-scene|content|addressables>` の 1 行を出す。content・Addressables の読み方、`EnsureEventSystem`、所有と `Dispose` は変えない。2 つの hook（logical key と address）は今と同じく、読む経路が決まった後にだけ評価する。**派生の getter の評価の時点は変わる（外部 1 巡目の P2）。** 今の code は、directory の設定が無い起動では最初の条件（`:353`）が短絡して getter を読まず、active な Scene の UICommon を借用する経路では loader も読まないので、その 2 つの経路での最初の評価は UICommon の後（`:369`）である。`Plan` を最初に呼ぶと、どの経路でも UICommon より前に 1 回読む。これを許すために、protected virtual `UseRequiredPlayerFileConfiguration` の契約を doc コメントに明記する: 「`BeforeSceneLoad` が始まってから同じ起動の間は、副作用なしに同じ値を返す。Framework は起動の間に何度でも、どの時点でも読む」（owner の決定。§2.5 の (F)）。Framework は今も `BeforeSceneLoad`（`BuildConfig`）でこの値から baked を読むかを決め、`AfterSceneLoad` で 10 回前後読み直して同じ値を前提にしているので、不変は既に暗黙の前提である。base でこの getter を override するのは SampleGame の `AppInitializer`（field を返す）だけで、test の initializer に override は無く、評価の回数に依る test も無い。loader の doc コメント（getter を先に評価しない）は書き直す。`WaitForPanelReadyAsync` の `required` は今のまま Player の構成で真。
- **D4 SceneResourceMap の読み口:** `Plan` が返した読み口で選ぶ（Player の `Resources`、content、Addressables）。読んだ後に `[AppInit] Bootstrap SceneResourceMap: source=<player-resources|content|addressables>` の 1 行を出す。protected virtual の `GetDirectoryBootstrapSceneLogicalKey`・`GetDirectoryBootstrapMapLogicalKey` は遊べる Player でも呼ばれるようになる（signature は変えず、doc コメントと、2 つの読み口の例外の文言の「Directory Editor Play」を直す）。`LoadPlayerSceneResourceMap` は写しの順の Player だけが呼ぶ。
- **D5 開始 Scene と Player の構成の判定（SampleGame）:** 遊べる Player の開始 Scene は、commit した空の Scene の asset `SampleGame/DependOnAll/PlayerStart/PlayablePlayerStart.unity`（root の GameObject が 0）にする。開始 Scene の名前を成果物の marker にする今の形を保ち、`IsBs4PlayerMode` を「Editor でなく、active な Scene の名前が ordinal に `UICommon`（動作確認の Player）か `PlayablePlayerStart`（遊べる Player）」にし、表の test のために internal にする。名前の定数は build（Editor の asmdef）と共有するので、public の `DirectoryBootstrapKeys` に `PlayableStartSceneName` として置く。
  - marker を file（`bs4-runtime.json` の有無）に変えない理由: file が欠けた exe が Player の構成でなくなり、Addressables の起動へ落ちる（今の fail-closed を弱める）。
  - 開始 Scene の名前と baked の種類が食い違う exe（file を手で書き換えた時だけ起きる）は、D1 の規則が両方の向きで起動の失敗にする（UICommon を持つ Scene と `playable` → `ContentOnly` の失敗。空の Scene と `smoke` → `BorrowOnly` の失敗）。表の test（AC11）に加えて、build した Player の写しの baked の種類だけを逆に書き換えた起動（AC14）で、latch・baked の読み・policy・loader をつないだ経路を確かめる（外部 1 巡目の P1）。
  - 開始 Scene を build のたびに生成しない理由: 空の Scene を足す Editor の API（`NewScene` の additive）は、untitled の Scene が開いている時に使えない見込みで（batch の test と、Editor を開いた直後がこの状態）、今の build が使う `OpenScene` の additive はその状態でも動いている。commit した asset なら GUID が固定で、中身は build の時に検査できる。
- **D6 Player の build（SampleGame の Editor）:** 種類ごとの値（`PlayerBuildKindSettings`）に「bootstrap を Player に写すか」を足す（`smoke` 真、`playable` 偽）。偽の時、coordinator は `CopyGraphClosure` と `CopyBootstrapScene` を呼ばず、開始 Scene の asset を additive で開いて root の GameObject が 0 であることを確かめて閉じ（0 でなければ build の失敗）、その Scene 1 つだけを Player の Scene にして build する。mutation（root と所有の印の作成、`bs4-runtime.json`、IL2CPP と High の設定、戻し）は 2 つの種類で今のまま変えない（遊べる Player の root には所有の印だけが入る）。`PlayerBuildReportVerifier` は、遊べる Player の時だけ次を足す: (a) 渡された「Player に入ってはいけない GUID」（map が参照する各 `SceneResource`。元の UICommon の Scene と map は今の検査が見る）が packed assets に無い（渡された GUID が 0 件なら、検査が空振りなので build の失敗にする）、(b) packed assets の元の path に mutation の root の下の物が無い、(c) 開始 Scene の GUID を持つ pack が 1 つ以下（空の Scene の pack が Scene の GUID の行を持つかは測っていないので、0 を許す）。`build-receipt.json` の `scenes` は開始 Scene の path、`bootstrapSceneName` は `PlayablePlayerStart` になる（欄は足さない・消さない）。動作確認の Player の build の呼び出しの列・検査・出力は変えない。
- **D7 変えないもの:** baked の key と値、protected の比較、種類の latch、`RouteFirstScene`、成功の 1 行（`Player content first scene`）の文言、失敗の通知の順（`RunStartupFailureHooksAsync`）、失敗時の初回 Scene の unload、`WireContentResolution` の条件、`ContentOverlayStartup.Decide`、`PlayerContentStage` の列（動作確認の Player の receipt だけが読む。遊べる Player の列は観測に使わない）。

**不変条件（起きてはいけないこと）:**

- **I1 遊べる Player は、UICommon と SceneResourceMap を、登録した content 以外（exe に焼いた写し、Addressables）から読んで動かない。**
  - 主張を崩しうる経路: (a) loader の借用の分岐（active な Scene の名前が `UICommon` なら規則を見ずに借用する今の順）。(b) map の読み口の取り違え（Player の構成だけを見て `LoadPlayerSceneResourceMap` に入る、directory でない遊べる Player が Addressables の読み口に落ちる）。(c) 遊べる Player の build が UICommon か map の写しを含み続ける（coordinator が種類を見ずに `CopyGraphClosure`・`CopyBootstrapScene` を呼ぶ。写しは新しい GUID なので、元の GUID の検査では見つからない）。(d) 開始 Scene の名前と baked の種類の食い違い（D5。AC11 と AC14）。(e) content に UICommon か map の root が無い directory（今の content の build は必ず合成する。無ければ `load-ui-common`・`load-scene-resource-map` の失敗になる）。(f) 登録より前に UICommon を読む順（登録の前の content の load は失敗する）。(g) 開始 Scene の asset に物が置かれる（D6 の build の検査が拒む）。
  - 探索した境界: `InitializeAfterSceneLoad` の全ての分岐、`BootstrapUiCommonLoader`、`AppInitializer` の Player の override（`Resources.Load("BS4/…")` は 1 か所）、coordinator・mutation・verifier。`IAssetManagement.LoadContentSceneAsync`・`LoadContentAssetAsync` の中（層の選び方）は 13 章の契約に依る。A2 の反証で、実行時に content 以外から読んで動く順序は見つからなかった（§5.1）。
  - 未確認: IL2CPP・High の Player で、content から読んだ UICommon の component が strip で欠けたまま動く形（u1。I1 の違反ではなく E1・E2 の不成立として見える）。
- **I2 動作確認の Player の契約は変わらない。** `smoke` の Player は、今と同じ receipt（file 名、schema v2、成功の stage `completed` と 10 段の順、失敗の stage と完了した段）と exit code で終わり、UICommon を生成した Scene から借用し、map を `Resources` の写しから読み、サービスの開始の後に content を登録する。
  - 経路: (a) `Plan` が `Smoke` を content が先の順にする取り違え。(b) loader の規則の変更が借用の経路の検査と所有を変える。(c) coordinator の分岐が `smoke` の `CopyGraphClosure`・`CopyBootstrapScene`・検査・receipt の欄を変える。(d) verifier に足す検査が `smoke` の検査を変える。(e) 足す log の行と stage `plan-bootstrap` が receipt の段か失敗の stage を変える（`Plan` は `Smoke` で失敗しないので、receipt に `plan-bootstrap` は出ない）。(f) latch の条件の変更が `UICommon` の名前の判定を落とす。
  - 探索した境界: I1 と同じ file と `PlayerContentSmoke`。A2 の反証で破る順序は見つからなかった。
- **I3 起動に失敗した遊べる Player は動き続けない。** 新しい順でも、`BeforeSceneLoad` の失敗、登録の失敗（UICommon より前）、UICommon・map の load の失敗、その後の段の失敗のどれでも、Player は log に失敗の stage と例外を残して exit 1 で終わる。「失敗」は起動の段が例外を投げた時で、hang は含まない（対象外）。
  - 経路: (a) 登録が UICommon とサービスの開始より前に落ちる時の後始末（session も `SceneDirector` も UICommon も無い状態で `CleanupAfterStartupFailureAsync` と通知が動く）。(b) content から読んだ UICommon と map を持ったままの失敗の後始末（directory の close は UICommon の Scene を unload するが、map の handle が生きているので失敗する見込み。失敗は log に出して通知へ進み、`ReleaseAll` が解除する）。(c) latch の条件の変更で、遊べる Player が Player の構成と判定されず、config の欠けが Addressables の起動へ落ちる（D5。AC14 (b) は空の開始 Scene の exe が Player の構成として失敗することを見る）。(d) `ReadBakedPlayerKind` を `AfterSceneLoad` の最初に読む時、baked が無ければ例外（try の中で読み、失敗の経路に乗せる）。(e) content から読んだ UICommon の panel が ready にならない時、`WaitForPanelReadyAsync` は例外ではなく待ち続ける（hang の形。I3 の外だが、u1 の不成立はこの形で現れうる。AC4 の 120 秒で不合格になる）。
  - 探索した境界: `BootstrapAfterSceneLoad`、`InitializeAfterSceneLoad` の try/catch、`CleanupAfterStartupFailureAsync`、`AssetManagement.CloseContentDirectoryAsync`、`BootstrapUiCommonLoader.Dispose`（unload 済みの GameObject は `!= null` の判定で破棄を飛ばす）、SampleGame の失敗の override。
- **I4 Player の構成と起動の順を、環境変数・command line で切り替えられない。** 遊べる Player に重ねる値で、写しの順・別の種類・Addressables・別の content に切り替えて動き続けることは無い。config（`bs4-runtime.json`）が欠けた遊べる Player は Addressables で始めず、起動の失敗になる。
  - 経路: (a) latch の材料（開始 Scene の名前）は build の成果物で、config の層に無い。(b) 順を決める種類を重ねた後の値から読む取り違え。(c) `content:playerKind` の override（今の protected の比較が拒む）。(d) exe の横の `bs4-runtime.json` の種類の書き換え（config の層ではなく成果物の改変。別の種類として動き続けず、D1 の規則で起動の失敗になる。AC14）。
- **I5 Editor の起動の段の順と結果は変わらない。** directory Play（経路 Built、検証 Play、snapshot、上書きの Play）は今と同じく登録 → UICommon → map の順で、Addressables 互換の起動は content を登録しない。
  - 経路: (a) `Plan` が Player の構成でない入力に今と違う結果を返す。(b) 規則 `BorrowOrLoad` が今の Editor の挙動と違う結果を返す。(c) 足す log の行を既存の観測が誤って拾う。
  - 探索した境界: `VerifyPlayLauncher` と Play の入口の record が照合する log の marker（`Addressables backend sealed` の部分一致と、`Content overlay registered`・`Editor content directory first scene`・`Content resolution:` の前方一致。新しい 3 行はどれにも当たらない）。Editor の実際の Play で観測するのは AC12 の検証 Play の 1 経路だけで、snapshot・上書き・Addressables 互換の Play は取り直さない（守りは AC11 の表と全 EditMode 回帰）。

**成立条件（起きなければならないこと）:**

- **E1** 遊べる Player を同梱の content から起動すると、空の開始 Scene から始まり、Player の log に「登録 → UICommon を content から → map を content から → 初回 Scene の安定」がこの順で出て、Title が安定し、Player は自分では終わらずに動き続ける。
- **E2** その Player の窓で Title の「スタート」を押すと Home を経て InGame へ進み、`Player ready at Cell stream spawn` になる（content から読んだ UICommon の UI が入力を受け付け、content から読んだ map の `SceneResource` の参照が解ける）。
- **E3** 遊べる Player の窓を閉じると、UICommon と map を content から保持したまま exit 0 で終わる（Title の上と InGame の上の両方）。
- **E4** 動作確認の Player は 6000.6.9f1 で今と同じ成功の receipt と exit 0 で終わる。
- **E5** 遊べる Player の出力は、Player の Scene が空の開始 Scene 1 つで、UICommon の Scene・SceneResourceMap・`SceneResource` も、それらの写しも含まない。構成（IL2CPP・High・development build でない）は S4-1 のまま。

### 2.2 受け入れ条件

全て §5 の判定必須テストか検証経路で閉じる。Player の起動を伴う条件の読み方は S4-1 の受け入れ条件と同じにする: 時間の制限を超えた起動は不合格で kill する。game の log は process が終わった後に読む。kill した起動の game の log は「観測なし」。Player の log と game の log は `[AppInit] Application quitting.` の行で閉じる前と閉じた後に分け、閉じた後の行は record に残すだけで合否に使わない（閉じる条件の合否は exit code と時間）。strip の検出語は S4-1 の判定で確定した読み方（`MissingMethodException`、`stripped`、`is missing`、`Could not produce class`。`stripped` は、`Shader 'Hidden/Universal Render Pipeline/` で始まり `is not supported or has been stripped from the build` を含む行には当てない。owner の決定、2026-11-23）を使う。game の log の「Error 以上の行が無い」は、record の `<unparsed>` の行も 0 であることを含む。log の行の順は record の行番号で読み、順を見る `[AppInit]` の行（`Content directory registered:`・`Bootstrap UICommon:`・`Bootstrap SceneResourceMap:`・初回 Scene の行）は、その起動の log にちょうど 1 回ずつ出ること（2 回出たら不合格）。失敗の起動（AC2・AC7〜AC10・AC14）の log には例外の行があるので、`Exception` と strip の検出語が無いことは成功の起動（AC4・AC6）にだけ求める。遊べる Player の窓の引数は `-screen-fullscreen 0 -screen-width 1280 -screen-height 720`。

| AC | 内容 | 検証する条件 |
|---|---|---|
| AC1 | 動作確認の Player の成功（S4-1 の AC1 と同じ起動）: `-batchmode` で 300 秒以内に exit 0、`bs4-smoke-receipt.json` は schema 2・`succeeded` 真・`stage` `completed`・10 段を今の順に全て持つ。transport manifest の `unityVersion` が `6000.6.9f1`。加えて Player の log に `Bootstrap UICommon: source=active-scene`、`Bootstrap SceneResourceMap: source=player-resources`、`Content directory registered: … from=directory` がこの順にある。record に game の log の Error 以上の行を全て残す（AC4・AC6 の基準）。 | I2、E4 |
| AC2 | 動作確認の Player の protected（S4-1 の AC2 と同じ）: `SAMPLEGAME_CONTENT__PLAYERKIND=playable` で 60 秒以内に exit 1、receipt は `succeeded` 偽・`stage` `before-scene-load`・`completedStages` 空・`error` に `Protected Player`。 | I2、I4 |
| AC3 | build の record: (a) 遊べる Player は、`build-receipt.json` の `scenes` が開始 Scene の asset 1 つで `bootstrapSceneName` が `PlayablePlayerStart`。verifier が D6 の (a)(b)(c) と今の検査（selected の root の重複、元の UICommon の Scene）を行って通ったこと（record に、(a) に渡した GUID の件数が map の `SceneResources` の件数と同じで 1 以上であること、(c) の pack の数が 0 か 1 であることを残す）。Unity の build の直前の mutation の root の下の file の一覧が所有の印だけであること。開始 Scene の root の GameObject が 0 だったこと。出力に `GameAssembly.dll` があり `SampleGame_Data/Managed/` が無いこと、build report の options に `Development` が無いこと、build の間の managed stripping が High だったこと。(b) 動作確認の Player は、`scenes` が生成した `UICommon.unity` 1 つで `bootstrapSceneName` が `UICommon`（今のまま）。(c) 2 つの build の後で `Assets/Resources/BS4/<identity>/` と `Assets/StreamingAssets/bs4-runtime.json` が残っていない（build の case が build の直後に 2 つの path の有無を record に書く。空の親の folder と `.meta` の残りは「S4-1 から移送した入力」の (6) の既知の形で、違反に数えない）。 | E5、I1、I2 |
| AC4 | 遊べる Player の起動: 窓で起動すると 120 秒以内に、Player の log に次の 4 行がこの順に出る: `[AppInit] Content directory registered: identity=<build の identity> from=directory`、`[AppInit] Bootstrap UICommon: source=content`、`[AppInit] Bootstrap SceneResourceMap: source=content`、`[AppInit] Player content first scene: scene=Title … kind=playable stable=True`。最後の行の後 60 秒の間 Player は終わらない。閉じる前の Player の log に `source=active-scene`、`source=player-resources`、`run-player-content-smoke`、`unload-player-first-scene`、`close-player-content-directory`、`AfterSceneLoad failed`、`Exception`、strip の検出語が無く、exe の横に receipt が無い。AC5 で終わった後、閉じる前の game の log に `TitleScene` の `Loaded.` があり、Error 以上の行が無い（AC1 の基準と同じ category・message の行は既存として record に書いて owner へ返し、違反に数えない）。合格例: 4 行がこの順。不合格例: UICommon の行が登録の行より前、`source=active-scene`、60 秒の間に exit した、120 秒で初回 Scene の行が出ない。 | E1、I1 |
| AC5 | AC4 の Player の窓に閉じる要求（主窓への `WM_CLOSE`）を送ると、120 秒以内に exit 0 で終わる。 | E3 |
| AC6 | 初回 Scene の先と InGame での終わり: 同じ build の遊べる Player を起動し、Phase C の実行担当（AI）が desktop の操作（画面を取り込み、その画像の上の位置を click する）で、Title の画面を file に保存し、Title の「スタート」を 1 回 click し、InGame の画面が出たら file に保存して、窓の × を click して閉じる（× を click できなければ主窓への `WM_CLOSE`）。(1) `[AppInit] Application quitting.` を test が見た時刻から 120 秒以内に exit 0。(2) 閉じる前の game の log に `OnStabledImpl HomeScene`、`[InGameSession] initial season ensure started`、`Player ready at Cell stream spawn` がこの順。(3) 閉じる前の game の log に Error 以上の行が無い（基準の扱いは AC4 と同じ。InGame の行は全て違反に数える）。(4) 閉じる前の Player の log に `AfterSceneLoad failed`、`Exception`、strip の検出語が無く、AC4 の 4 行がある。(5) 保存した 2 枚の画像で、Title と InGame の画面が出て真っ黒・マゼンタでなく、Title の画像に「スタート」の button が見える。(1)〜(4) は record の log で、(5) は画像で、判定 C が判定する。画像は evidence に入れ、C' が自分で読み直せるようにする。 | E2、E3 |
| AC7 | 遊べる Player の protected（S4-1 の AC7 と同じ）: (a) `SAMPLEGAME_CONTENT__PLAYERKIND=smoke`、(b) `SAMPLEGAME_CONTENT__RUNTIMEMODE=addressables` でそれぞれ起動すると、60 秒以内に exit 1、Player の log に `Protected Player` を含む行があり、`Content directory registered:`・`Bootstrap UICommon:`・`Player content first scene` の行が無く、receipt が無い。 | I4、I3 |
| AC8 | `BeforeSceneLoad` の content の失敗（S4-1 の AC8 と同じ）: 存在しない `SAMPLEGAME_CONTENT__INSTALLEDREVISIONPATH` と 64 桁の `SAMPLEGAME_CONTENT__MANIFESTSHA256` で起動すると、60 秒以内に exit 1、Player の log に `ContentDeliveryException` の行と `BeforeSceneLoad が失敗したため AfterSceneLoad をスキップします` の行があり、`Content directory registered:`・`Player content first scene` の行が無く、receipt が無い。 | I3 |
| AC9 | 登録の失敗（UICommon より前）: この case だけが、publish した遊べる Player の folder（build の record の Player の directory）を自分の case の folder へ写し直し（他の case が使う写しは変えない。写した path の長さは今の検査に通す）、その写しの `content/` の下の file と folder を全て消して `content/` の folder だけを残し、起動する。60 秒以内に exit 1、Player の log に `AfterSceneLoad failed at stage 'register-content-directory'` を含む行があり、`[AppInit] AfterSceneLoad: loading UICommon.`・`Content directory registered:`・`Player content first scene` の行が無く、receipt が無い。 | I3 |
| AC10 | UICommon と map を content から読んだ後の失敗: 遊べる Player を `SAMPLEGAME_WORLD__CELLCOMPANIONSET=Broken`（protected でない key。`CreateSceneFactory` が `FormatException` を投げる）で起動する。120 秒以内に exit 1、Player の log に `Bootstrap UICommon: source=content` と `Bootstrap SceneResourceMap: source=content` の行の後に `AfterSceneLoad failed at stage 'create-scene-factory'` を含む行があり、`Player content first scene` の行が無く、receipt が無い。後始末の失敗の行（`Content directory cleanup after startup failure also failed`）は出る見込みで（map の handle が生きている）、record に全文を残し、合否は exit 1 と時間で読む。 | I3 |
| AC11 | 単体テスト（表）: (1) `BootstrapSourcePolicy.Plan` の D1 の表の 5 行（Player の構成でない 2 行は種類が `Smoke` でも `Playable` でも同じ結果）。(2) `DecideUiCommon` の D1 の 8 通り。(3) SampleGame の latch: (Editor, どの名前) → 偽、(Player, `UICommon`) → 真、(Player, `PlayablePlayerStart`) → 真、(Player, `uicommon`・末尾に空白のある `PlayablePlayerStart `・`Title`・空) → 偽。(4) verifier: 入ってはいけない GUID が pack にある → 拒む、mutation の root の下の path の物が pack にある → 拒む、開始 Scene が 2 つの pack にある → 拒む、開始 Scene が 0 か 1 つの pack → 通る、足した入力を渡さない呼び出し（`smoke`）は今の 3 つの検査だけで今の結果。(5) `SettingsOf`: `smoke` は bootstrap を写す、`playable` は写さない。今の値（宣言、probe、出力先、`playerKind`）と `RuntimeJson` の表は変わらない。 | I1、I2、I4、I5 |
| AC12 | Editor の非退行: 検証 Play（`spring-full`）を 1 回行い、Editor の log に `Content directory registered: … from=installed`、`Bootstrap UICommon: source=content`、`Bootstrap SceneResourceMap: source=content`、`Editor content directory first scene: scene=Title … stable=True` がこの順にあり、`AfterSceneLoad failed` が無い。その前に `[VerifyPlay] verify spring-full: Play requested.` の行があり、止める前に読んだ `EditorApplication.isPlaying` が真であること。Play に入ってから止めるまでの `[AppInit]` の行を `TestResults/s41/<run id>/ac12/` に保存する。 | I5 |
| AC13 | 回帰: 判定必須の全 EditMode 回帰（§5）。 | I1〜I5 |
| AC14 | 開始 Scene と baked の種類の食い違い（外部 1 巡目の P1）: 2 つの case が、それぞれ publish した Player の folder を自分の case の folder へ写し直し（AC9 と同じ扱い）、写しの `SampleGame_Data/StreamingAssets/bs4-runtime.json` の `content.playerKind` の値だけを逆の種類に書き換えて起動する。(a) 動作確認の Player（開始 Scene は UICommon）を `playable` にして `-batchmode` で起動: 60 秒以内に exit 1、Player の log に `Content directory registered:` の行の後に `AfterSceneLoad failed at stage 'load-ui-common'` と `A playable Player must not start from a scene that contains UICommon.` を含む行があり、`Bootstrap UICommon:`・`Player content first scene` の行が無く、receipt が無い（latch が `playable` なので書かない）。(b) 遊べる Player（開始 Scene は空）を `smoke` にして窓で起動: 60 秒以内に exit 1、Player の log に `AfterSceneLoad failed at stage 'load-ui-common'` と `The Player bootstrap scene must contain UICommon` を含む行があり、`Content directory registered:`・`Bootstrap UICommon:`・`Player content first scene` の行が無く、receipt は `succeeded` 偽・`stage` `load-ui-common`・`completedStages` 空（latch が `smoke` なので書く）。(a)(b) とも、Player の log に `Addressables backend sealed` の行があり、`source=addressables` の行が無い。 | I1、I3、I4 |

### 2.3 ここでは答えない問いと所有する後続

- 上書きを Player に重ねること、UICommon と map の編集が上書きで Player に届くことの確認（上書きの root に入る、上書きの map が上書きの側の `SceneResource` を指す）: S4-2（Player は S4-2 まで上書きの key を拒むので、このスライスの Player では測れない）。
- installed の pair を渡した遊べる Player の成功の起動（UICommon と map をその revision から読む。log は `from=installed`）: S4-2（上書きなしで pair から起動する条件と、上書きつきの成功の条件が、同じ Player の同じ経路を通す）。このスライスが pair で見るのは失敗（AC8）だけである。
- 開始 Scene の asset は Player に焼き込む入力になる。S4-2 の「Player の入力の指紋」の集合に入れるかは S4-2 が決める（入力として送る）。
- 動作確認の Player の起動の順を content が先の順に揃え、`CopyGraphClosure`・`CopyBootstrapScene`・`Resources/BS4` を無くすこと: 所有する後続なし（owner の決定で変えない。変える時は receipt の段の順の契約を改める必要がある）。Phase D で program §10 の U11 に足す。
- Player での要求時解決の配線と record: S4-2 の統合案が U11 へ送っている。
- 起動の段の時間の上限（hang）: U11 (a)。
- `bs4` の名の整理（遊べる Player は `Resources/BS4/<identity>/` に所有の印しか置かなくなる）と、`bootstrapSceneName` の欄の意味の整理: 所有する後続なし。Phase D で U11 に足す。
- content の UICommon の形（root が 1 つで EventSystem がある）の build の時の検査は、遊べる Player の build では走らなくなる（`CopyBootstrapScene` の中にある）。Runtime の loader が root と component を検査し、EventSystem が無ければ作る。content の build の側に検査を足すかは、所有する後続なし（U11）。
- Android・iOS: S4-5。
- 遊べる Player の UI の見た目の細部（描画の正しさ）: 対象外。AC6 (5) が見るのは画面が出て真っ黒・マゼンタでなく、Title の UI が見えることだけである。

### 2.4 スライス分割の判断

契機: 高リスクの初回実機スライスで、独立して失敗しうる未知が複数ある（UICommon を build から外した時の strip、空の Scene から始まる Player、content から保持した UICommon と map の quit、新しい順の失敗の見え方）。

判断: **分けない。** 理由: (1) どの未知も、この変更を入れて build した遊べる Player でしか観測できず、Framework の順の選択と SampleGame の build の変更のどちらか一方だけでは起動できる Player にならない（順だけを変えると、今の build の Player は `ContentOnly` で失敗する）。(2) 未知ごとの成立は、成立条件 E1（起動と順）・E2（UI の入力と map の参照）・E3（quit）と I3 の AC9・AC10（失敗の見え方）で別々に判定でき、1 つの最低条件に束ねていない。(3) 先に測るだけのスパイクを置いても、測る対象の Player は製品の変更とほぼ同じ物になる（変更は順の分岐と build の写しの有無で、実験 code として捨てる部分が無い）。strip が不成立の時の対応（`link.xml`）は §4 の B 適応に置く。A2 の 2 件（A0 だけからの代替構成、アーキテクチャゲート）も分けない判断に同意した。凍結の前に試しの build を置くかは §2.5 の (C)。

### 2.5 A3 の決定と owner の決定

**A3 の案（決めた。運用上の選択）:**

1. **Player の構成の marker:** 開始 Scene の名前のまま（D5）。空の開始 Scene の名前を `UICommon` のままにして latch を変えない案（A2）は採らない。
2. **開始 Scene:** commit した空の asset（D5）。build のたびに生成しない。
3. **順の選び方:** baked の種類から Framework の policy が選ぶ。新しい baked の key、新しい hook、receipt の新しい欄は足さない（D1・D6）。
4. **順の印:** 3 つの log の行を、Editor と 2 つの種類の Player のどれでも出す（D2〜D4）。成功の 1 行と失敗の見え方は変えない。
5. **installed の pair の成功の起動:** S4-2 へ送る（§2.3）。
6. **動作確認の Player:** 変えない（owner の決定、2026-11-24）。

**owner の決定（2026-11-24、会話。(A)〜(F) とも A3 の案のとおり。(A) は、成り立たない時の扱いを owner が改めた）:**

- **(A) AC6 の押下と撮影は AI が行う（owner が不在になるので、owner の押下を前提にしない）。** Phase C の実行担当（AI）が desktop の操作（画面の取り込みと click）で「スタート」を押し、画面を保存し、窓を閉じる（AC6、§5 の u7）。人間の押下が要らないので、AC6 は高コスト証拠にせず、他の起動の case と同じく GO 候補 head ごとに取る。この手段は未確認なので、**Phase B の最初に、今ある S4-1 の遊べる Player で疎通を確かめる**（§4、§5 の u7）。成り立たない時は、owner の押下へ自動では戻さず、担当が AC6 の取り方の別の案を出して owner に返す（owner の決定。取り方を変えるのは Phase A の新しい revision）。owner は、不在の間に担当が desktop を操作する許可を事前に与えておく。
- **(B) 「上書きの map が上書きの側の `SceneResource` を指すか」を S4-2 へ送る**（§2.3。入力のコメントは未測定として挙げたが、Player が上書きを受け付けるのは S4-2 からである）。
- **(C) 凍結の前に strip の試しの build は置かない。**（u1 を Phase C の最初の build と起動で確かめ、型の欠けは `link.xml` の B 適応、それで足りなければ Phase A へ戻す）。A0 だけからの代替構成は、設計を凍結する前に遊べる Player を 1 つ試しに build して strip の行を見ることを勧めたが、その build はこのスライスの変更をほぼそのまま入れた物になるので、採らない。
- **(D) 目的と利用者の文言は変えない。** 目的レビューは、文言からは決まらない点として「Editor の directory Play と同じ順」の基準、「登録した content」の指す物、「protected config」「fail-closed」の中身、成功の「log の 1 行」に行を足してよいか、を挙げた。基準は 04 章 §4.7、指す物は §1 の現況、行を足す扱いは上の 4 のとおりとする（owner の裁定）。
- **(E) C' は Grok の系列か人間が担当する。** A2 は Anthropic の系列、外部計画レビューの 1 巡目は OpenAI の系列だった。C' は Grok の系列か人間が担当する（§5）。
- **(F) protected virtual `UseRequiredPlayerFileConfiguration` の契約を明記する**（D3。「同じ起動の間は副作用なしに同じ値を返す。Framework はどの時点でも読む」）。signature は変えないが、Framework の hook の契約を文にして狭めるので owner が決めた。`Plan` を遅らせて評価する形は採らない。

## 3. 責務マップ

| file（`unity/Assets/` から） | 責務（変更理由） | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|
| `OneStarMaker/Scripts/Runtime/Bootstrap/BootstrapSourcePolicy.cs`（新規） | 起動の時に、登録の位置・UICommon の規則・map の読み口と、UICommon の読む先を決める policy（D1 の 2 つの判定と、その値の型） | 状態なし | `PlayerContentKind` | internal | 新しい `BootstrapSourcePolicyTests`（表。Unity を使わない） | 0 → 90 |
| `OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs` | 起動の 3 フェーズの順と失敗境界（`Plan` を呼び、登録・UICommon・map の 3 か所をその結果で選ぶ。log 3 行。hook の doc コメント（`UseRequiredPlayerFileConfiguration` の契約を含む）と例外の文言） | App | policy、`BootstrapUiCommonLoader` | 変えない（protected virtual の signature は同じ） | AC1・AC4・AC7〜AC10・AC12 | 1425 → 1445。500 行を超えるが、起動の orchestration の 1 責務で、足すのは配線と log だけ（判断は policy に置く）。非分割 |
| `OneStarMaker/Scripts/Runtime/Bootstrap/BootstrapUiCommonLoader.cs` | UICommon の取得と所有（Player の入力を規則の値に変え、判定を policy に任せ、読んだ先を戻り値で返す。doc コメント） | App（initializer の field。今のまま） | policy、`IAssetManagement`、`SceneManager` | internal | policy の表（Unity の I/O は判定の外に残す） | 172 → 185 |
| `SampleGame/DependOnAll/AppInitializer.cs` | SampleGame の起動の hook（Player の構成の latch に開始 Scene の名前を足す。`IsBs4PlayerMode` を internal に） | App | `DirectoryBootstrapKeys` | 変えない | 新しい latch の表の test（`SampleGame.Tests`） | 460 → 465 |
| `SampleGame/DependOnAll/DirectoryBootstrapKeys.cs` | build と Runtime が共有する bootstrap の名前の定数（開始 Scene の名前を足す。summary を直す） | 状態なし | なし | public（定数 1 つを足す） | AC11 (3) | 12 → 16 |
| `SampleGame/DependOnAll/PlayerStart/PlayablePlayerStart.unity`（新規の asset） | 遊べる Player の空の開始 Scene（root の GameObject が 0） | commit した asset | なし | — | AC3 (a) の build の検査 | — |
| `SampleGame/DependOnAll/Editor/Build/SampleGamePlayerBuildCoordinator.cs` | Player の build の手順（種類ごとの値に「bootstrap を写すか」を足し、写さない時は開始 Scene を検査して build し、verifier に足した入力を渡し、receipt の 2 欄を開始 Scene の値にする。`PlayerBuildResult` に判定の record 用の値を足す） | Editor の操作 | mutation、adapter、verifier | menu は変えない | `Bs4FixtureComposerTests`（`SettingsOf`）、判定の build | 229 → 270 |
| `SampleGame/DependOnAll/Editor/Build/PlayerBuildReportVerifier.cs` | Player の build report の検査（D6 の (a)(b)(c) を足す。pack の data に元の path を持たせる） | 状態なし | なし | internal | `PlayerBuildIntegrationPolicyTests` | 62 → 90 |
| `SampleGame/DependOnAll/Editor/Build/PlayerBuildProjectMutation.cs` | 変えない（root と所有の印、`bs4-runtime.json`、設定、戻し。遊べる Player では 2 つの `Copy…` を呼ばれなくなるだけ） | — | — | — | — | 変更なし |
| `SampleGame/Tests/Editor/Build/PlayerKindEndToEndTests.cs` | 判定の Player の build・起動・観測（新しい log の行の照合、AC9・AC10・AC14 の起動 4 件と、case 専用の folder へ Player を写す helper、AC3 の record の欄、冒頭の doc の件数） | test | 上の全て | test | — | 462 → 590。500 行を超える。判定の case の宣言的な列の 1 責務で、足すのは case 4 件と写しの helper と照合の語だけ。非分割 |
| `OneStarMaker/Tests/Bootstrap/BootstrapSourcePolicyTests.cs`（新規）、`SampleGame/Tests/Bootstrap/PlayerModeLatchTests.cs`（新規）、`SampleGame/Tests/Editor/Build/PlayerBuildIntegrationPolicyTests.cs`、`SampleGame/Tests/Editor/Build/Bs4FixtureComposerTests.cs` | AC11 の表 | test | — | test | — | 各 +15〜80 |
| `SampleGame/DependOnAll/Editor/Build/SampleGameBootstrapContentComposer.cs` | summary の「Editor directory Play 用」を直すだけ（code は変えない） | — | — | — | — | +0 |
| `unity/Assets/Docs/Architecture/04-app-startup.md`、`18-asset-description.md` | 公開文書（Phase D の harvest で直す。Phase B は変えない） | — | — | — | docs-audit | — |

policy を initializer に足さず新しい file にする理由: 判定は Unity を呼ばない policy で、initializer（orchestration）や loader（Unity の I/O と所有）とは変わる理由もテストの方法も違う。置き場は同じ Bootstrap の folder・namespace（`ContentOverlayStartup` と同じ形）。

namespace・asmdef は既存の file と同じ。新しい asmdef 参照、新しい Framework の公開 API、新しい `SceneState`、新しい config の key は足さない。新規の `.cs` と編集する `.cs` は先頭に `#nullable enable` を置き、`record` を使わない。

## 4. 実装計画

- 順序: (0) u7 の疎通（§5。実装の前に、今ある S4-1 の遊べる Player で、AI の desktop の操作による click が Unity に届くかを確かめて §6 に記録する。Unity の test も build も使わない）。(1) `BootstrapSourcePolicy` と表。(2) loader の入力と戻り値。(3) initializer の配線と log。(4) SampleGame の latch と定数と表、開始 Scene の asset。(5) verifier の検査と表。(6) coordinator の分岐、receipt。(7) 判定の test の case と照合の語。
- 本文へ転記した実装制約: Unity の偽 null（`UnityEngine.Object` は `== null`・`!= null` で判定する）。Editor の code を Runtime の asmdef に置かない。公開 API の log の抽象は `ILogger<T>`（起動の `Debug.Log` の行は今の initializer と同じ形で、公開 API ではない）。テストで `Task.Delay`・`Thread.Sleep` を使わない（判定の test の待ちは今の `WaitForExit`・`WaitForLine` の形を使う）。path は `tools/path-budget.json` の予算に従う。開始 Scene の asset は Editor で作る（`.unity` の YAML を手で書かない。Editor の無い環境の B は asset を作らず、その旨を §6 に書いて引き継ぐ）。Phase B は Unity の test と build を実行しない。
- Phase B から Phase A へ差し戻す条件: 遊べる Player を content が先の順にするのに、新しい baked の key・新しい Framework の公開 API・新しい hook が要る。`ContentDirectorySession`・`AssetManagement` の登録・close・quit の経路の変更が要る（u3・u4 の不成立）。動作確認の Player の順か receipt を変えないと成り立たない。空の開始 Scene では Player が起動しない（u2）。content から読んだ map の参照が Player で解けない（u5）。新しい asmdef 参照が要る。 u7 の疎通が成り立たない（AC6 の取り方の別の案を §6 に書いて owner に返す。AC6 に関わらない実装（順序の (1)〜(6)）は続けてよく、取り方が決まるまで AC6 の case の変更だけを止める）。
- B 適応の例: IL2CPP・High で bootstrap の型が欠けた時に `SampleGame/DependOnAll/link.xml` へ名指しで足す、UICommon の Scene が持つ component の型と `SceneResourceMap`・`SceneResource` の型（u1。足した型と、動作確認の Player の build にも効くことを §6 に記録する）。開始 Scene の検査の Editor API の呼び方。判定の test の log の読み直しの間隔。例に当たるだけで許可済みとはせず、A3 と常時契約に照合する。
- 対象外を維持する方法: `smoke` の build と起動の呼び出しの列、`PlayerBuildProjectMutation`、`PlayerContentConfiguration.Resolve`、`ContentOverlayStartup`、`WireContentResolution` の条件、`RuntimeJson`、Editor の Play の入口の code を変えない。

## 5. テストとレビュー計画

- 単体テスト: AC11 の 5 つの表（§3 の test の file）と、既存の `PlayerContentConfigurationTests`・`PlayerContentSmokeSequenceTests`・`Bs4FixtureComposerTests`・`PlayerBuildIntegrationPolicyTests`。
- 差し戻し中の起点 `-Filter`: `OneStarMaker.Tests.BuildContent.PlayerContentConfigurationTests;OneStarMaker.Tests.BuildContent.PlayerContentSmokeSequenceTests;OneStarMaker.Tests.Bootstrap.BootstrapSourcePolicyTests;OneStarMaker.Tests.Editor.Build.Bs4FixtureComposerTests;OneStarMaker.Tests.Editor.Build.PlayerBuildIntegrationPolicyTests` と latch の表の test class（名前は B が決めて §6 に書く）。
- 判定必須テスト（GO 候補 head）: 次を順に、それぞれ別の Unity process で実行する（環境変数 `S41_RUN_ID` を全てで同じ値にする。`T` は `OneStarMaker.Tests.Editor.Build.PlayerKindEndToEndTests`。Unity の `-testFilter` は正規表現の部分一致なので、1 case だけを走らせる filter は末尾に `$` を付け、XML の total が 1 であることを確かめる）。(1) 起点 filter。(2) `pwsh tools/run-tests.ps1 -Filter 'T.BuildSmoke$'`。(3) `T.BuildPlayable$`（AC3）。(4) `T.LaunchSmoke$`（AC1）。(5) `T.LaunchSmokeOverride$`（AC2）。(6) `T.LaunchPlayable$`（AC4・AC5）。(7) `T.LaunchPlayableKindOverride$`（AC7 (a)）。(8) `T.LaunchPlayableAddressables$`（AC7 (b)）。(9) `T.LaunchPlayableMissingInstall$`（AC8）。(10) AC9 の case。(11) AC10 の case。(12) AC14 (a) の case。(13) AC14 (b) の case（(10)〜(13) の case 名は B が決めて §6 に書く）。(14) `T.LaunchPlayableHuman$`（AC6。実行担当が desktop を操作する。下の検証経路）。(15) AC12（Editor の検証 Play。下の検証経路）。(16) Editor を閉じてから、全 EditMode 回帰（`pwsh tools/run-tests.ps1`）。
- 判定の読み方: S4-1 と同じにする。(2)(3) は Project Auditor の post-build 監査で `step.json` が `failed` になりうるので、合否は XML と record で読む。(4)〜(14) の XML の `Passed` は record を書き終えたことだけを示し、AC の合否は record の行で読む。起動の case の再実行は別の `S41_RUN_ID` で (2) からやり直し、両方の record を残す。全 EditMode の合否は XML で読み（Inconclusive があると `step.json` は `failed` になる）、Failed が 0 で、Inconclusive は namespace `SampleGame.Tests.Rendering.Vfx` の test だけを受け入れる（`-nographics` の既知の形。S4-1 の判定 C では 25 件。それ以外の Inconclusive は不合格）。staging の `Directory.Move` の access denied で落ちた test は、その test だけを 1 回取り直し、通れば受け入れて record に書く（program の S5-0、owner の既知の受容）。新しい worktree の最初の Unity の起動が書き換える `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` は、build の前に戻す（「S4-1 から移送した入力」の (7)）。
- Unity の test と build を実行する担当は、実行の前に [osm-unity-editor](../../.agents/skills/osm-unity-editor/SKILL.md) と [Harness README の停止規則](../../tools/Harness/README.md#unity終了時の既知native-stallと調査の停止規則) を読む。
- 全 EditMode 回帰の適用除外: なし。
- 統合・Unity テスト: 上の (2)〜(15)。
- 操作・実行時・目視条件の検証経路:
  - AC1〜AC5・AC7〜AC10・AC14: 上の (2)〜(13) で閉じる。担当は Phase C の実行担当（Unity の batch と Player の起動。窓を出せる、利用者の desktop の session で実行する）。初期状態: Editor を閉じ、Active Build Target が StandaloneWindows64、作業ツリーが clean、他の Unity と `SampleGame.exe` が動いていない。事前に `git status --porcelain` を取る。観測は record・各 case の Player の log と game の log・receipt・build の結果・XML。事後に `git status --porcelain` を取り（mutation の戻しの確認）、record の pid が全て終わっていることを CIM で確かめる。証拠は `TestResults/s41/<run id>/` の全て（写した Player の本体を除く）と XML を evidence bundle に入れる。
  - AC6: 担当は Phase C の実行担当（AI。利用者の desktop の session で、画面の取り込みと mouse の操作ができること）。実行担当は `T.LaunchPlayableHuman$` を background で起動し（test は Player を起動して exit を最大 1800 秒待つ）、case の folder に `started.json` が出たら操作を始める。順は、Player の窓を前面にする → 画面を取り込んで Title が出ていることを確かめ、画像を case の folder に保存する → 画像の上の「スタート」の button の位置を 1 回 click する → 画面を取り込み直して InGame の画面が出るのを待ち（game の log の `Player ready at Cell stream spawn` を目安にする）、画像を保存する → 窓の × を click する（閉じなければ主窓へ `WM_CLOSE`）。click は 1 回だけで、押し直さない（効かなければ、その起動は不合格として record に残す）。判定 C は (1)〜(4) を record の行で、(5) を保存した画像で判定し、click した時刻、窓を閉じた時刻、対象の build identity を §7 に記録する。発見 C でも同じ手順で限定実行してよい（入力の不成立を早く見つけるため）。
  - AC12: 担当は Phase C の実行担当。batch の test と AC6（(2)〜(14)）が終わってから Editor を開き（[osm-unity-editor](../../.agents/skills/osm-unity-editor/SKILL.md) に従う）、開いている Scene を untitled の空の Scene 1 つだけにする（起動が再利用しうる Scene が開いていると検証 Play は拒む）。`tools/unity-editor.cmd` の eval で、dialog を出さない入口 `VerifyPlayLauncher.VerifyAsync(SampleGameBuildPurposes.Find("spring-full"), interactive: false)` を呼ぶ（internal なので reflection で呼ぶ。content の build・publish・install の後に Play に入る）。**この入口は `Task` を返す。eval は Task を始めるだけにして、`Result`・`Wait()`・`GetAwaiter().GetResult()` で待たない**（Editor の main thread を塞ぐと、install の後の Play への移行の続きが進まず止まりうる。外部 2 巡目）。結果は Task からではなく Editor の log（`Editor.log`）で読む: 入口は終わる時に `[VerifyPlay] verify spring-full: Play requested.` か、拒否・失敗・取消の `[VerifyPlay] verify spring-full: …` の Error の 1 行を自分で出す。実行担当は log を読み直して待ち、(i) `Play requested.` の行の後に初回 Scene の行か `AfterSceneLoad failed` の行が出たら、別の eval で `EditorApplication.isPlaying` の値を読んで記録してから、eval（`EditorApplication.isPlaying = false`）で Play を止める。(ii) 拒否・失敗・取消の行が出たら Play には入っていないので、その行と `EditorApplication.isPlaying` の値を記録して終える（AC12 は不合格か、初期状態の不備なら直して別の run でやり直す）。待つ上限は入口を呼んでから 30 分（content の build を含む）で、超えたら時間切れとして記録する。`[VerifyPlay]` の行、読んだ `isPlaying` の値、Play に入ってから止めるまでの `[AppInit]` の行を `TestResults/s41/<run id>/ac12/` に保存する。自分で開いた Editor を閉じてから (16) へ進む。検証 Play は repo の外の content の cache へ install する（既存の経路の作用）。
  - 動作確認（Phase B の後）: `対象なし`（offline のテストで閉じない条件は全て Player の build か Editor の Play を要し、Phase C が担当する）。
- 未知の操作経路の疎通: 次は未確認で、**初回確認は Phase C**（理由: Unity の test と Build の実行は Phase C の責任で、Phase A の担当は Unity を動かせない。担当: Phase C の実行担当）。
  - (u1) UICommon の Scene と map・`SceneResource` の写しを build から外した IL2CPP・High の Player で、content から読んだ UICommon（`UICommon`・`PanelRenderer`・`EventSystem`・`InputSystemUIInputModule`）と `SceneResourceMap`・`SceneResource` が strip で欠けずに動くか。確認地点: AC4（panel の ready、map の load、Title の安定。panel が ready にならない時は 120 秒の時間切れとして現れる）と AC6（「スタート」の入力）。不成立時: 欠けた型を `link.xml` に名指しで足すのは B 適応（§4）。それで足りなければ Phase A へ戻す。
  - (u2) root の GameObject が 0 の Scene 1 つだけを Scene にした Player が起動し、`BeforeSceneLoad` で開始 Scene の名前を読め、`BeforeSceneLoad` で作る Camera と `AfterSceneLoad` の段が動くか。確認地点: AC4 と AC6 (5)。不成立時: Phase A へ戻す（開始 Scene に物を置くかは設計判断）。
  - (u3) UICommon と map を content から保持したままの quit が Player で exit 0 になるか。確認地点: AC5 と AC6 (1)。不成立時: Phase A へ戻す（`ContentDirectorySession` の終了の経路を変えるかは設計判断）。
  - (u4) 新しい順の失敗（登録の失敗、UICommon と map を content から読んだ後の失敗）で、後始末と通知が exit 1 に届くか。確認地点: AC9・AC10。不成立時: 後始末の順の変更は Phase A へ戻す。`content/` を空にしても登録が失敗しない時は、壊し方（消す file）を変えるのは B 適応で、stage の名前が変わる時は決定記録で読み方を確定する。
  - (u5) content から読んだ map の `SceneResource` の参照が Player で解けるか（今の Player は参照を書き換えた写しを使っている。Editor の directory Play では解けている）。確認地点: AC4（Title）と AC6 (2)（Home・InGame）。不成立時: Phase A へ戻す。
  - (u6) `tools/unity-editor.cmd` の eval から、internal の `VerifyAsync` を reflection で呼んで（Task を待たずに）検証 Play に入り、Editor の log を読んで Play を止められるか（AC12）。不成立時: menu `Tools/OSM/Content/Verify Play/spring-full` で同じことを行う（失敗の時だけ dialog が出るので、出たら実行担当が文を record に写して閉じる）。どちらでも Play に入れない時は、原因を記録して owner へ返す（AC12 を外すのは Phase A の判断）。
  - (u7) 担当の AI が、利用者の desktop の session で、Player の窓の画面を取り込み、画像の上の位置を click して Unity の入力（Input System）に届かせ、取り込んだ画面を file に保存できるか（AC6）。owner の経験では、別の session が画面を取り込んで click する操作は成立した。この repo の遊べる Player では未確認。**初回確認は Phase B の最初**（owner の決定、2026-11-24。Unity の test も build も要らないので B が行える）: S4-1 で build した遊べる Player（`artifacts/playable/players/<identity>/SampleGame.exe`）を AC6 と同じ窓の引数で起動し、AC6 と同じ手順で画面を保存して「スタート」を 1 回 click し、窓を閉じた後に game の log に `OnStabledImpl HomeScene` が出ているかを見る。対象の exe、click した時刻、log の行の有無、保存した画像の有無を §6 に記録する（合否は書かない）。その Player が残っていない時と、B の環境が desktop を操作できない時は、確かめられなかった理由を §6 に書き、初回確認を Phase C の最初の遊べる Player の build の直後へ送る（B は build しない）。不成立時: 画像を file に保存できないだけなら、保存の手段（shell からの画面の取り込み）を変えるのは担当の権限内。click が Unity に届かない、または desktop を操作する許可が得られない時は、担当が AC6 の取り方の別の案（例: 判定の test が OS の入力を送って click する、owner が戻ってから押す）を、それぞれの要る変更と一緒に出して owner に返す。owner の押下へ自動では戻さない。取り方を変えるのは Phase A の新しい revision で、決まるまで AC6 を未実施のまま判定 C を完了と記録しない。必要な権限: desktop の操作（画面の取り込みと mouse）。owner が不在の間に許可の確認で止まらないよう、事前に許可を与えておく。
  - S4-1 が確かめた経路（製品の coordinator を test から呼ぶ build、窓を出した Player の起動と `WM_CLOSE`、追記中の log の読み直し）は既知で、再確認しない。Player の publish の `Directory.Move` の access denied（S4-1 の u1b）は、落ちた時に S4-1 と同じ再実行の規則に従う。
- 人間の判断が必要な条件: なし（AC6 の操作と撮影は AI が行い、画面の判定は判定 C が画像で行う）。u7 が成り立たない時の取り方は owner が決める（§2.5 の (A)）。
- 高コスト証拠: なし。AC6 は AI が操作するので、他の AC と同じく GO 候補 head ごとに取る（Player の build は外部への作用を持たずこの PC で繰り返せる。build 2 回、各約 6 分の見込み。S4-0・S4-1 の実測）。u7 が成り立たず取り方を改める時は、その revision で分類を決め直す。
- 判定 C の照合 script の方式: なし。判定 C は record と receipt と log を AC ごとに読む。
- 機械検査: `contract-audit`、`docs-audit`。
- A0/A1 主担当: Cloud の Claude Code の session（Anthropic の系列）。A0 は program §8 の S4-1a の行（PR #212 の branch）、PR #212 のコメント「owner との会話で決まったことと、S4-1a の入力」、同 PR の S4-2 の統合案が S4-1a について書く箇所、program の「S4-1 から移送した入力」と U11、04 章、S4-1 の HANDOFF（git の履歴）を入力とした。
- C' 用に予約した担当: Grok の系列（`cursor-agent` を使う場合は AGENTS.md の契約どおり Grok 系を明示する）か人間。Phase A には Anthropic の系列（A1・A2・A3）と OpenAI の系列（外部計画レビューの 1 巡目）が関与したので、この 2 つは C' に使わない。この予約を崩さないため、外部計画レビューの 2 巡目以降と Phase B・C に Grok の系列を使わない。
- 独立性の強化条件を満たせない場合の理由: C' を起こす時に予約した系列が使えなければ、人間が C' を担当するか、`独立性制約あり` として理由と残存リスクを §8 に記録して owner が採否を決める。

### 5.1 A2 の独立レビューと A3 の採否

**A2 の独立レビュー（実施 2026-11-24。互いの指摘を見せていない。全て Anthropic の系列で、別ベンダーは外部計画レビューと C' 用に残した。モデルの識別子は、公開物に書かない運用の session なのでここに書かず、owner への報告で伝える）:**

- (1) **目的レビュー:** 主担当の session が起動した新しい subagent（主担当とは別のモデル）。リポジトリを読まず、ツールを使わずに回答した。入力は §1 の目的と利用者の欄の原文だけで、sha256 は `226fa9f1ef59bf9ec29586a48b6c5db9817c447a3daf741aee184aa3431d69e6`（目的欄の本文、改行、利用者欄の本文。UTF-8、LF、末尾改行なし）。問いは「この目的のために何を保証すべきか」「何を作らないべきか」の 2 つだけ。A3 で 2 欄の文言は変えていない。
- (2) **A0 だけからの代替構成:** (1) と同じ subagent が、目的レビューの回答を記録した後に続けた。入力は A0 の packet（program の S4-1a の行、PR #212 のコメントの原文、S4-2 の統合案が S4-1a について書く箇所、「S4-1 から移送した入力」と U11 の原文、依頼文。sha256 `b738e426c70cc2c1a31080a5560a36bf4d39ecf9305b646b6ba7820afc5f26a3`）と、固定 base `116b00918dbdefa9f2dff5aeec7e43ef4a8b4a30` の clean な detached checkout。本書は読んでいない。
- (3) **アーキテクチャゲート兼、各不変条件を破る順序・受け入れ条件の曖昧さ・検証経路の実行可能性・分割・常時契約:** 主担当の session が起動した別の新しい subagent（主担当と同じ設定の別 session）。入力は A2 入力版の本書（内容 sha256 `3da4d7e3b7b232a5546103adbe0ae06fcde144139d3fe7df327f84b4a4812eb0`。commit `6011fb1d` の本書とは、§2.3 の 2 か所に「Phase D で」の語が無い点だけが違う）と (2) と同じ固定 base の checkout。枠は「§3・§4 の計画した code は実装済みと仮定し、現在の code は既存の前提の確認にだけ使う」に固定した。program の S4-1a の行は A2 入力版の後にこの branch へ足した（下の M-5）。

**A3 の採否（統合担当: 主担当の session。目的と利用者の欄は変えていない。文言への指摘は owner が「変えない」と裁定した。§2.5 の (D)）:**

(1) 目的レビュー。保証すべき 9 点と作らないべき 7 点のうち、初稿と違った・弱かったもの:

| 指摘 | 採否 | 理由と反映 |
|---|---|---|
| 読み元が content であることを、たまたま同じ中身だったという理由で合格にしない | 採用 | 読んだ先の log（AC4）に加えて、Player が写しを含まないことを build で検査する（D6 の (a)(b)、AC3 (a)。(3) の B-2） |
| content の UICommon か map を編集して作り直した Player にその変更が出ることで確かめる | 不採用 | 作り直すと写しも作り直されるので、読み元の区別にならない。写しが Player に無いことの検査で示す |
| 読めない時に exe の写しへ黙って戻らない | 初稿のとおり | 遊べる Player の build は写しを持たず、規則 `ContentOnly` は借用も Addressables も選ばない（D1） |
| 成功と失敗を伝える新しい経路（追加の log の行など）を作らない | 一部不採用 | 成功の 1 行、失敗の log と exit code は変えない（D7）。順と読んだ先の 3 行は、E1 と I1・I2 を外から判定する印として足す（§2.5 の 4 と (D)） |
| exe に焼き込んだ写しを build から外すかは原文から決まらない | 採用（設計の判断として記録） | 外す（D6、E5）。入力のコメントが `CopyBootstrapScene`・`CopyGraphClosure` が要らなくなると書き、写しを残すと I1 を build で示せない |
| 新しい menu・起動引数・設定・環境変数を足さない、`smoke` と Editor に手を入れない、上書きを作らない | 初稿のとおり | §1 の対象外、UC の表 |
| 文言の曖昧さ（同じ順の基準、登録した content、protected config と fail-closed の中身、S4-2 についての主張） | owner の裁定（文言を変えない） | §2.5 の (D)。基準の所在（04 章 §4.7）を §1 の現況に足した |

(2) A0 だけからの代替構成。推奨案（開始 Scene の名前を 2 つ認める、新しい key を足さず baked の種類から Framework が順を選ぶ、Editor が使っている content の読み口で読む、写しを build に入れない、分けない）は初稿と同じ骨格だった。違った点の採否:

| 指摘 | 採否 | 理由と反映 |
|---|---|---|
| 空の開始 Scene を commit した asset にする（生成しない） | 採用 | D5・D6。(3) の M-1 と同じ結論 |
| 開始 Scene と種類の照合を、新しい protected virtual の hook で `BeforeSceneLoad` に置く | 不採用 | Framework の公開 API が増える。食い違いは D1 の規則が `load-ui-common` の段で両方の向きとも失敗にする |
| 遊べる Player では借用を評価せずに content へ進む | 不採用 | active な Scene に UICommon がある遊べる Player は食い違いなので、黙って通さず失敗にする（D1 の `ContentOnly`） |
| 遊べる Player の mutation は `Resources/BS4/<identity>` を作らない | 不採用 | mutation の所有の印と戻しを種類で分けない（I2 の経路を増やさない）。root の下の物が Player に入らないことは verifier が見る（D6 の (b)） |
| map が参照する `SceneResource` の GUID が pack に無いことを検査する | 採用 | D6 の (a) |
| receipt に `bootstrapSource` の欄を足す | 不採用 | 欄を足さない。`bootstrapSceneName` の値で種類を見分けられる |
| 登録と読み元の log を遊べる Player の順でだけ出す | 不採用 | 3 行をどの起動でも出す（log のための分岐を作らない。AC1 と AC12 が同じ行で動作確認の Player と Editor を見る）。既存の照合に当たらないことは (3) が確かめた |
| 凍結の前に strip の試しの build を置く | 不採用（owner の決定） | §2.5 の (C) |
| content から読んだ map の `SceneResource` の参照が Player で解けるかは未知 | 採用 | u5、E2 |
| 判定の filter の末尾の `$`、`WM_CLOSE`、`<unparsed>` の件数 | 採用 | §2.2 の冒頭、§5 |
| 起動の時間の伸びを record に残す | 不採用 | 現在の問いの判定に要らない |
| 動作確認の Player も同じ順にする案、`bs4` の名、`bootstrapSceneName` の意味 | 後続 | §2.3 |

(3) アーキテクチャゲート兼反証。凍結を止める指摘 2 件、重要 8 件、軽微 14 件。不変条件 I1〜I5 を実行時に破る順序は見つからなかった。常時契約の違反は見つからなかった。

| ID | 指摘 | 採否 | 理由と反映 |
|---|---|---|---|
| B-1 | AC9 が、起動の case が共有する遊べる Player の写しを壊し、後の AC10 と AC6 が成立しない | 採用 | AC9 の case だけが自分の folder へ写し直して壊す（AC9）。消す範囲も書いた |
| B-2 | 写しは新しい GUID なので、元の GUID の検査では「写しを含まない」を検出できない。coordinator が写し続けてもどの AC も落ちない | 採用 | verifier が pack の元の path で mutation の root の下の物が無いことを検査し、build の直前の root の下の file の一覧を record に残す（D6 の (b)、AC3 (a)、I1 の経路 (c)） |
| M-1 | 空の Scene を additive の `NewScene` で生成する作り方は、untitled の Scene が開いている時の実績が無い | 採用 | 生成をやめて commit した asset にした（D5・D6）。build は今と同じ additive の `OpenScene` で検査する |
| M-2 | 「ちょうど 1 つの pack」の検査が、GameObject が 0 の Scene で通るか分からない | 採用 | 遊べる Player では「1 つ以下」にした（D6 の (c)、AC11 (4)） |
| M-3 | map の 3 分岐が純粋な関数でも表でもなく、遊べる Player を Addressables の読み口へ落とせる形 | 採用 | 登録の位置・UICommon の規則・map の読み口を 1 つの policy の `Plan` が返し、directory でない遊べる Player は失敗にして表に入れた（D1、AC11 (1)、§3 の新しい file） |
| M-4 | installed の pair の成功の起動が AC にも §2.3 にも無い | 採用（所有先を書く） | S4-2 へ送る（§2.3、UC3、§2.5 の 5）。このスライスに起動の case を足さない |
| M-5 | base の program に S4-1a の行が無く、A2 が問いの出所を照合できない。未凍結の S4-2 の AC の番号を指している | 採用 | program に S4-1a の行を足した（この branch）。PR #212 との merge の順を §0 に書いた。S4-2 の AC の番号への参照をやめた |
| M-6 | 空の開始 Scene の名前を `UICommon` のままにすれば、latch の変更と public の定数が要らない | 不採用 | `UICommon` という名前の Scene に UICommon が無い形になり、04 章が書く marker の意味（生成した bootstrap の Scene の名前）が読めなくなる。receipt の `bootstrapSceneName` で種類を見分けられなくなる。Editor の Play の入口は開いている Scene の名前 `UICommon` を特別に扱う（`VerifyPlayLauncher`・`PlayEntryDecision`）ので、commit した asset をその名前にすると入口の判定に当たる。latch の変更は 1 行と表の test で済む（§2.5 の 1） |
| M-7 | AC12 の手順が実行できる粒度に足りない。I5 の他の経路の根拠が無い | 採用 | §5 の AC12 の手順（初期状態、menu、log、Play の止め方）。I5 に、実際の Play で観測するのは検証 Play の 1 経路だけで残りは表と回帰で守ると書いた |
| M-8 | 全 EditMode の Inconclusive の許容集合が固定されていない | 採用 | namespace の規則にした（§5） |
| m-1 | AC10 の 60 秒は、登録と UICommon・map・サービスの後の失敗には短い | 採用 | 120 秒 |
| m-2 | 閉じた後の log の読み方が無い | 採用 | §2.2 の冒頭（record に残すだけ） |
| m-3 | log の `verified=` は設定から決めると実際の session と食い違いうる | 採用 | `from=<installed|directory>`（設定が pair を持つか）に変えた（D2） |
| m-4 | 規則を値で渡すことは、今の doc コメント（getter を先に評価しない）を覆す | 採用 | D3 に理由を書き、doc コメントを直す。2 つの hook は遅延のまま |
| m-5 | 読んだ先は `LoadAsync` の戻り値に入れれば loader の状態が増えない | 採用 | D3 |
| m-6 | 「同梱の順」の「同梱」が同梱の `content/` と同じ語になる | 採用 | 「写しの順」「content が先の順」（§2.1 の語） |
| m-7 | 「未確認」のうち code から読める物（unload の後の `Dispose`、AC10 の close の失敗） | 採用 | I3、AC10 |
| m-8 | 責務マップと harvest の漏れ（18 章、composer の summary、test の doc、`CopyBootstrapScene` の検査が走らなくなること） | 採用 | §0 の harvest 先、§3、§2.3 |
| m-9 | u1 の対象に `SceneResourceMap`・`SceneResource` と native の `PanelRenderer` が無い | 採用 | u1、§4 の B 適応 |
| m-10 | 開始 Scene と種類の食い違いは表だけで閉じ、実機の AC が無い | 採用（明記）→ 外部 1 巡目の P1 で実機の AC を足した | D5、AC14 |
| m-11 | 「U11 に足す」を誰がいつ行うか | 採用 | Phase D（§0 の harvest 先、§2.3） |
| m-12 | 登録の log の置き場所 | 採用 | D2（method の最後） |
| m-13 | AC6 の screenshot を test が撮れるかを検討していない | 採用（owner の希望で改めた） | 初めは S4-1 と同じ owner の撮影のままにしたが、owner の希望（2026-11-24）で、押下と撮影を AI の desktop の操作にした（AC6、u7、§2.5 の (A)） |
| m-14 | Unity の終了時の stall の停止規則への参照が無い | 採用 | §5 |

A2 の観点の再レビュー: 要らない。A3 の改訂は、(3) が指摘した形そのもの（policy への集約、build の検査、commit した asset、AC の作り方）で、新しい状態・依存・公開 API を足していない。目的と利用者の欄は変えていないので、目的レビューのやり直しも要らない。

**外部計画レビュー 1 巡目（2026-11-24、PR #213 の review。OpenAI の系列。owner の account から投稿された。対象は head `43b089b4`。判定は COMMENT で、凍結は 2 点の採否と risk の記録の後を勧める。大枠（1 つの policy で順を選ぶ、遊べる Player だけ開始 Scene と写しを変える、失敗を登録前と読込後に分ける）には賛成）。採否は A3 の案で、owner が 2026-11-24 に案のとおりと裁定した:**

| ID | 指摘 | 採否 | 理由と反映 |
|---|---|---|---|
| P1 | 開始 Scene と baked の種類の食い違いは、別々の表の test では latch → baked の読み → policy → loader の配線を通らない。Player の写しの `bs4-runtime.json` の種類だけを逆に書き換えて、2 つの向きを実機で起動する AC を足す | 採用 | AC14 (a)(b)、D5、I1 (d)・I3 (c)・I4 (d)、§5 の (12)(13)。期待する stage と receipt の有無は、D1 の規則と今の失敗の通知（latch した種類で receipt を書くかを決める）から導いた |
| P2 | 「getter の評価の時点・回数が実質変わらない」は、directory の無い起動と借用の経路では成り立たない。getter は純粋で同じ起動の間は不変と明記して早い評価を許すか、今の短絡と借用に合わせて遅らせるかを決める | 採用（契約を明記する） | D3 の説明を事実に合わせて直し、契約の文を足した。owner が決めた（§2.5 の (F)）。Editor の回帰の守りは、評価の回数に依る test が base に無いことの確認と全 EditMode 回帰である |

**owner の希望（2026-11-24、会話）:** owner が不在になるので、AC6 の「スタート」の押下と撮影を AI が行う形にする。AC6・§5 の検証経路と u7・高コスト証拠・人間の判断の欄を改めた。続けて owner は、§2.5 の (A)〜(F) を案のとおりと決め、u7 を Phase B の最初に確かめ、成り立たなければ担当が別の案を出す、と決めた（owner の押下へ自動では戻さない）。押下の前提が変わっただけで、判定する log の行は変えていない。

この巡で足した Phase C の読み方（指摘ではなく、判定の担当が迷う所の明確化）: log の行の順は record の行番号で読み、順を見る行はちょうど 1 回ずつ（§2.2）。verifier の検査の空振りを build の失敗にし、件数を record に残す（D6、AC3）。AC12 を dialog の出ない入口で行う手順と保存先、batch の test との順（§5、u6）。全 EditMode の合否を XML で読むこと（§5）。

**外部計画レビュー 2 巡目（2026-11-24、PR #213 の review。1 巡目と同じ OpenAI の系列。対象は head `6cfd127c`。判定は COMMENT。1 巡目の P1・P2 は解消として受け入れ、AC6・u7 の戻し方と AC12 の入口の選び方も妥当とした。新しい指摘 1 件は実行の手順の補強で、凍結を止めるものではないとした）:**

| ID | 指摘 | 採否 | 理由と反映 |
|---|---|---|---|
| P2 | AC12 の `VerifyAsync` は `Task` を返す。eval の中で同期に待つと Editor の main thread を塞いで止まりうる。eval は Task を始めるだけにして完了を非同期に観測し、`PlayRequested` と拒否・失敗の区別と `EditorApplication.isPlaying` の実際の値を記録してから止める | 採用 | §5 の AC12 の手順（待たない、結果は入口が自分で出す `[VerifyPlay]` の行で読む、`isPlaying` を読んでから止める、待つ上限）、AC12、u6。製品の設計は変えていない |

A2 の観点の再レビュー（外部 1 巡目と 2 巡目の後）: 要らない。AC14 は既にある設計（D1 の規則）の実機の検証を足すだけで、設計は変えていない。1 巡目の P2 は説明の訂正と doc コメントの契約で、責務マップの file は増えていない。2 巡目の P2 は Phase C の実行の手順である。目的と利用者の欄は変えていない。

## 6. Phase B 実装結果

未着手。

## 7. Phase C

未着手。

## 8. Phase C'

未着手。

## 9. Phase D

未着手。
