# content directory から起動して遊び続けられる Windows の Player

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S4-1）
- status: `A`（Phase A の revision 0。A1 の初稿、A2 の独立レビュー 3 件、A3 の統合案まで。**凍結していない**。外部計画レビューの前）
- branch: `claude/playable-content-player`（PR の base は `develop`）
- implementation base commit: `9de34cc2ec2dfe82b782f122b0012b902e98f277`（`develop` の head、2026-11-23。§1 の現況と A2 の固定 base もこれ）
- implementation head commit: 未固定
- A3 凍結記録: 未凍結
- risk: `high`（Framework の起動（`AbstractApplicationInitializer`）と Player の protected config を変える。動作確認の Player（BS4）の契約を壊すと、BS4 を起動して receipt を読む担当と Artifacts の Player の保存の経路が黙って変わる。Player を遊べる形で起動するのはこの project で初めてで、独立して失敗しうる未知が複数ある。§5）
- owner: root（依頼者）
- created: 2026-11-23
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: [04 章](../../unity/Assets/Docs/Architecture/04-app-startup.md) §4.1・§4.6・§4.7（Player の 2 つの種類、遊べる Player の起動の段と終わり方、protected config の key、`bs4` の名を持つ file・folder・latch を改名しない理由）、`unity/Assets/README.md` か repo の README の「試す」（遊べる Player の build と起動の手順。置き場は Phase D が既存の記述に合わせて決める）、[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S4-1 の行と、§2.3 の後続へ送る入力（S4-2 の行と §10）
- 関連: [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md)（§8 の S4-1・S4-2 の行、「S4 の A1 への入力（2026-11-23）」の (1)、「S4-0 の A1 が振り分けた入力」と「S4-0 から移送した入力」の S4-1 の項目）、[CONTENT_DIRECTORY_BUILD_FACTS.md](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)「Windows の Player での上書きの登録（6000.6.9f1）」（test からの Player の build と Project Auditor の事実）
- Phase A snapshot の取得案内・状態: 該当なし（機械向けの期待値・照合 script は無い。§5。レビューの入力は本書と program だけ）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: Phase A の A3（外部計画レビューの指摘の採否と、owner の凍結判断）。
  - 次の作業: Draft PR の外部計画レビューの指摘を §5.1 の A3 の表へ採否として足し、§2.5 の owner の判断が要る 4 点を owner に確かめる。凍結するなら A3 凍結記録・本票（次の担当を Phase B へ）・PR の状態ブロックを改めて、凍結版を commit する。
  - 読む範囲: 全節。
  - 引き継ぐ commit: この票を含む commit（SHA は起動指示と PR の状態ブロックで渡す）。
  - 必要な権限: なし（文書だけ）。push は公開点の規則に従う。
  - 停止規則: 目的・利用者の欄を変える指摘は owner が裁定し、変えたら目的レビューをやり直す（§5.1）。
  - 引き渡さないもの: なし。

## 1. 目的と対象外

- 目的: Windows の開発者が、製品の build 経路で build した Player を、同梱した content directory（または検証済みの install した revision）から起動し、初回 Scene から先へ操作を続けて遊べるようにする。その Player は、動作確認の Player（BS4）と同じ protected config と fail-closed の規則で起動し、起動に失敗した時は動き続けずに理由を残して終わる。動作確認の Player の receipt と exit code の契約は今のまま残す。開発用の Player の構成（scripting backend、managed stripping の水準、development build）を 1 つに決め、後続（S4-2: 上書きを Player に重ねる）がその Player を土台にできるようにする。
- 利用者: SampleGame を Windows の Player で遊んで確かめる開発者（content を作る人、S4-2 以降で Player に上書きを重ねる開発者）。build を始めるのは Unity の menu、遊ぶのは build された exe の起動と、その窓の中の操作である。
- 対象外（所有する後続は §2.3）:
  - 上書きを Player に重ねること（`ContentOverlayStartup.Decide` は Player の構成で上書きの key を拒んだままにする。13 章「base と上書きの登録契約」の「範囲」の禁止も解かない）。
  - Player での要求時解決の配線（`WireContentResolution` は Player の構成では呼ばないままにする。record も付かない）。
  - Mono、他の stripping の水準、development build の Player。backend・stripping・development build を選ばせる menu や設定。
  - Android・iOS。
  - 遊べる Player を Artifacts の Build の保存系列（`build collect`）へ入れること。
  - 遊べる Player に動作確認の Player と同じ形の判定の出力（receipt）を持たせること。
  - Player の中で content や revision を選ぶ UI、起動の失敗の再試行・代わりの content への fallback。
  - build を CLI・CI から呼ぶ新しい入口（判定の test の `[Explicit]` の入口は検証用で、利用者の入口ではない）。
  - InGame の world の中身の遊び心地、性能、streaming の所要。
  - 製品の起動の設定を Addressables から読む経路（Player の構成に入らない Player。S4-0 の J1 の失敗の形）。
- 現況（base `9de34cc2`）:
  - **owner が受けた入力（program §8「S4 の A1 への入力（2026-11-23）」の (1)）:** 起動が動作確認（`AbstractApplicationInitializer.RunPlayerContentSmokeAsync`: 初回 Scene の load → fixture → 初回 Scene の unload → directory の close → 完了通知）に入るのは、directory の設定があり、初回 Scene が空でなく、`UseRequiredPlayerFileConfiguration` が真の 3 つが揃う時である。SampleGame の BS4 の Player は 3 つとも満たし、完了通知の override（`AppInitializer.OnPlayerContentShutdownCompletedAsync`）が `Application.Quit` で終える。初回 Scene を置く Player には、content を載せたまま操作を続ける構成が無い。
  - **program §8 の S4-1 の行:** 問いは「content directory から起動して、動作確認をして終了するのではなく操作を続けられる Player の構成を、動作確認の Player（BS4）の receipt と exit code の契約、protected config、fail-closed を弱めずに持てるか。開発用の Player の構成（backend、stripping、development build）をどうするか」。gate は「遊べる Player で初回 Scene から操作を続けられる。動作確認の Player の契約が変わらない。Player の実機の非退行（S3-1 の配線と、S3-2 の session・gate の層の化を含めて、6000.6.9f1 で確かめる）」。S4-2 の行は、S4-1 が決めた開発用の Player の構成が S4-0 と違う時に S4-0 の harness で測り直す、と書く。
  - **S4-0 から移送した入力（S4-1 分）:** Addressables の data の無い IL2CPP・High の Player で、製品の起動の設定の読み込み（`JsonFileConfigProvider` → `AddressableBackend.LoadAssetSync`）が `ResourceManager+CompletedOperation<TextAsset>` の既定の constructor の strip で `MissingMethodException` になった。
  - **Player の構成の latch（`SampleGame/DependOnAll/AppInitializer.cs:66,134-136`）:** `BeforeSceneLoad` の冒頭で、Editor でなく active Scene の名前が `UICommon` の時に `_useBs4PlayerMode` を真にし、`UseRequiredPlayerFileConfiguration` がそれを返す。環境変数や command line では有効にならない（04 章 §4.7）。真の時、`BuildConfig`（`AbstractApplicationInitializer.cs:907-932`）は `StreamingAssets/bs4-runtime.json` を `RequiredJsonFileConfigProvider` で必須に読み（Addressables を使わない）、その値を `PlayerBakedContentConfiguration` に固定し、環境変数（`SAMPLEGAME_`）と command line（`-` で始まる引数）をその上に重ねる。key は大小文字を区別せず、`.` と `:` を同じに扱う。
  - **protected config（`OneStarMaker/Scripts/Runtime/BuildContent/PlayerContentConfiguration.cs`、`AbstractApplicationInitializer.ReadContentDirectoryConfiguration`）:** `ReadContentDirectoryConfiguration` は、上書きの key を `ContentOverlayStartup.Decide` で見た後、Player の構成なら `content:runtimeMode` を baked と比べて違えば `InvalidConfiguration`（message `Protected Player runtime mode was overridden.`。addressables への override で検査を迂回させない）、installed の pair の片方だけも拒む。その後 `PlayerContentConfiguration.Resolve` が baked の 9 つの key（`schemaVersion`・`runtimeMode`・`buildIdentity`・`contentSet`・`target`・`representation`・`firstScene`・`relativeDirectory`・`probeToken`）を重ねた後の値と ordinal に比べ、1 つでも違えば `InvalidConfiguration`（message `Protected Player content configuration was overridden.`）。installed の pair があれば `InstalledRevisionVerifier.Verify(root, digest, baked.ContentSet, baked.BuildIdentity, …)` で照合する。照合は receipt の revision が baked の build identity と同じことを求める（`InstalledRevisionVerifier.cs:78-82`）ので、使えるのは Player と同じ build identity の revision だけである。pair が無ければ exe の横の `relativeDirectory` を使う。これらは全て `BeforeSceneLoad` の中で起きる。
  - **起動の段（`AbstractApplicationInitializer.InitializeAfterSceneLoad`）:** Player の構成では、UICommon（生成した bootstrap Scene の借用）→ ready の待ち → `LoadPlayerSceneResourceMap`（`Resources` の graph metadata）→ framework の service の開始 → scene factory → directory の登録（`register-content-directory`）→ `SceneDirector` の生成（要求時解決は配線しない。`:400-406`）→ `RegisterAlreadyLoadedScenes` → `RunsPlayerContentSmoke(UseRequiredPlayerFileConfiguration)` が真なので `RunPlayerContentSmokeAsync`（`:465-469`）。Editor の directory Play は同じ場所で `LoadEditorFirstSceneAsync`（初回 Scene を load したまま Play を続け、1 行の log を出す。`:499-508`）に入る。`RunsPlayerContentSmoke` と `ShouldNotifyPlayerContentFailure` はどちらも引数をそのまま返す internal static（`:1234-1239`）で、`PlayerContentConfigurationTests` が表で固定している。
  - **失敗の経路:** `BeforeSceneLoad` の例外は log に出して `ReleaseAll` で回収し（`:193-202`）、`AfterSceneLoad` は Error を 1 行出して、Player の構成なら `OnPlayerContentStartupFailedAsync("before-scene-load", …)` を同期で待つ（`:210-227`）。`AfterSceneLoad` の例外は `CleanupAfterStartupFailureAsync`（Player の構成なら初回 Scene の unload（`:657`、条件は `RunsPlayerContentSmoke`）、directory の close、派生への通知）の後に `ReleaseAll`。後始末の中の例外は log に出すだけ（`:699-704`）。Player の構成では `OperationCanceledException` も失敗として扱う（`:473`）。SampleGame の失敗の override（`AppInitializer.cs:157-165`）は `PlayerContentSmoke.WriteReceipt(false, stage, …)` の後に `Application.Quit(1)` を呼ぶ（receipt の書き込みが投げると Quit に届かない）。
  - **receipt の契約（`SampleGame/DependOnAll/PlayerContentSmoke.cs:144-158`）:** exe の横の `bs4-smoke-receipt.json` に schema v2（`schemaVersion`・`succeeded`・`stage`・`completedStages`・`error`）を `.tmp` から置き換え（無ければ `File.Move`）で書く。成功は stage `completed` と 10 段（`ui-common-ready` … `content-directory-closed`）で exit 0、失敗は到達した stage と完了した段で exit 1。この file を読む code は repo に無い（読むのは人と、BS4 を起動する担当。repo の中に BS4 の Player を起動する code も無い。Delivery の窓は Editor の Verify Play に環境変数を渡すだけである。`ContentDeliveryPlayBridge.cs:181-204`）。
  - **game の log:** game の code の `ILogger`（ZLogger）は Player の `-logFile` には出ず、exe の横の `OneStarMakerLocal/Log/` の rolling file に出る（`AppLoggerFactory.cs:113-123,217-`）。`TitleScene` は load で `Loaded.`、Start の後の遷移の失敗で Error を出し（`TitleScene.cs:44,91`）、`HomeScene` は安定で `OnStabledImpl HomeScene` を出す（`HomeScene.cs:37`）。
  - **終了時の解放:** `Application.quitting` → `ReleaseAll`。directory が登録されたままなら `AssetManagement.CompleteContentDirectoryPlayStop` → `ContentDirectorySession.CompletePlayStop`（受付を止め、cache を落とし、pending の native の unload を待たずに解除する。quit 中に PlayerLoop を待つと同じ main thread で死鎖するため）。動作確認の Player は quit の前に directory を close しているので、Player でこの経路を通ったことは無い（Editor の Play の停止では通る）。
  - **Player の build（`SampleGame/DependOnAll/Editor/Build/`）:** `SampleGamePlayerBuildCoordinator.Build()`（menu `Tools/OSM/Content/Build BS4 Spring Player` だけが呼ぶ）が、`bs4-spring-full` の宣言（`spring-full` と同じ選択に probe の prefab 1 件を合成）で content を Full で build し、transport へ publish し、`PlayerBuildProjectMutation` の中で graph metadata と bootstrap Scene を `Assets/Resources/BS4/<identity>/` に写し、`bs4-runtime.json` を `Assets/StreamingAssets/` に書き、scripting backend を IL2CPP・managed stripping を High・Addressables の「Player と一緒に build する」を off にして `BuildPipeline.BuildPlayer`（StandaloneWindows64、`DetailedBuildReport`、`previousBuildReportDirectories` に content の build report）を呼び、終わりに全て戻す。build report の packed assets で content の root の重複を検査し、`PlayerBuildPublisher` が `artifacts/bs4/players/<identity>/`（exe、`content/`、`build-receipt.json`）へ置く。development build にはしない。`build-receipt.json` の `backend`・`stripping` は定数を書く（実際の設定を読まない。`:66`）。初回 Scene は `SampleGameBuildPurposes.FirstSceneOf`（今の目的は全て `Title`）。`RuntimeJson` の既存のテストは `Bs4FixtureComposerTests.cs:66-79`。
  - **build receipt を読む側:** Artifacts の `build collect`（`tools/Artifacts/BuildContract.psm1:142-160`）は `build-receipt.json` の 7 つの欄を名前で読み（他の欄は拒まない）、保存系列を target と `backend`・`stripping` の組（`IL2CPP`・`High` → `il2cpp-high`）で決める。contentSet は transport manifest から読む。
  - **Player の証拠の版:** BS4 の Player の build と起動の証拠は全て 6000.6.0f1 のものである。S3-1（要求時解決の配線。Player では配線しない分岐を足した）と S3-2（session の層の列と gate）は Player の実機で確かめていない。S4-0 は 6000.6.9f1 で、EditMode の test から IL2CPP の Player を `run-tests.ps1` の batch で build できること（約 6 分）、その後に Project Auditor の post-build 監査が同じ Unity で走り `step.json` を `failed` にすること（XML は `Passed`）、`-batchmode` の Player 18 回が全て exit 0 で終わったことを測った。窓を出した起動は測っていない。
  - **初回 Scene の先:** `Title` の Scene（`SampleGame/OutGame/Title/`）は UI Toolkit の Start の button の click で `SceneController.SwitchScene` を呼び、`Home` へ移る（`TitleScene.cs:69-84`）。Player で `Title` より先へ進んだ記録は無い。`SampleGame/DependOnAll/link.xml` は `UnityEngine.UIElementsModule` の `UnityEngine.UIElements.*` を全て残す。repo の code は `DEVELOPMENT_BUILD` と `Debug.isDebugBuild` に依らない。
  - **Addressables の封じ:** directory mode の起動は `SealAddressables` で Addressables の取得経路を封じる。動作確認の Player は設定を Addressables から読まないので、封じる前にも使わない。Editor の directory Play（経路 Built）は同じ封じの下で遊べている。
  - Unity 上では何も実行していない。
- UC ごとの利用者の入力:

| UC | 利用者が選ぶ・入れるもの | 機械が決めるもの | 誰がいつ決めるか | 目的からの正当化 |
|---|---|---|---|---|
| UC1 遊べる Player を build する | menu `Tools/OSM/Content/Build Spring Playable Player` を選ぶこと（1 つ） | 宣言（`spring-full`）、初回 Scene（`FirstSceneOf`）、表現、Player の種類 `playable`、backend IL2CPP・stripping High・development build でない、出力先 `artifacts/playable/players/<identity>/` | 選ぶのは実行時の操作（開発者）。他は開発時の宣言（coordinator の定数と宣言の表）と自動導出 | 目的の「製品の build 経路で build した Player」。種類を menu の名前で分けるのは、動作確認の Player を build する menu を今のまま残すため（目的の「契約は今のまま」）。宣言や構成を選ばせないのは、目的が構成を 1 つに決めることを求めるため |
| UC2 遊べる Player を起動して遊ぶ | exe を起動すること、窓の中の操作（Title の Start など）、窓を閉じること | 読む content（同梱の `content/`）、初回 Scene、protected config の値 | 開発者の実行時の操作。他は build の時に baked | 目的の「起動し、初回 Scene から先へ操作を続けて遊べる」。content や初回 Scene を起動の引数で選ばせないのは protected config を弱めないため |
| UC3 検証済みの install した revision から起動する（任意） | 環境変数か command line の `content:installedRevisionPath` と `content:manifestSha256` | 照合（`InstalledRevisionVerifier`、baked の contentSet と build identity）。使えるのはその Player と同じ build identity の revision だけ | 開発者の実行時の操作 | 既にある動作確認の Player の口をそのまま使う（新しい入力ではない）。目的の「検証済みの install した revision から起動」 |

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 製品の build 経路で build した Windows の Player に、動作確認をして終わるのではなく、content directory から起動して初回 Scene から先へ操作を続けられる種類を、動作確認の Player の receipt と exit code の契約、protected config、fail-closed を弱めずに足せるか。その開発用の Player の構成を何にするか。
- 判定定義: **GO** は §2.1 の不変条件と成立条件を全て満たした時。**NO-GO** はどれかを満たさない時。スパイクではないので CONDITIONAL ACCEPT は無い。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし。
- 決定記録: なし。

### 2.1 進める最低条件

**決めること（設計）:**

- Player の種類を build の時に baked config の新しい protected key `content:playerSession`（値は `smoke` か `playable`）で決める。key が無い baked config は `smoke` と読む（今の動作確認の Player の bytes の意味を変えない）。それ以外の値は `InvalidConfiguration`。種類は Framework が持つ語彙で、`smoke` は今の `RunPlayerContentSmokeAsync`、`playable` は初回 Scene を load したまま起動を終える。分岐が読むのは baked の値（`_bakedPlayerContent`）で、重ねた後の値ではない。Player の構成でない起動（Editor）は baked が無く、種類の分岐に入らない。
- 遊べる Player は receipt を書かない。起動の成功は Player の log の 1 行、失敗は Player の log（stage と例外）と exit 1 で示す。SampleGame の失敗の override は、baked の種類が `playable` なら receipt を書かず、`smoke`（baked を読めなかった時を含む）なら今と同じ receipt を書く。どちらの種類でも `Application.Quit(1)` を `finally` で必ず呼ぶ（receipt の書き込みが投げても Quit に届かせる。§2.5 の (4)）。
- 失敗時の後始末で初回 Scene を unload する条件は今の意味（Player の構成かどうか）のままにし、両方の種類で unload する。種類で分けるのは成功の末尾の分岐だけにする。
- 開発用の Player の構成は、動作確認の Player と S4-0 が測った Player と同じ IL2CPP・managed stripping High・development build でない、にする（理由は §2.4 の (c)）。

**不変条件**（起きてはいけないこと）:

- **I1 動作確認の Player の契約は変わらない。** 今の menu で build した動作確認の Player は、今と同じ receipt（file 名、schema v2 の欄、成功の stage `completed` と 10 段の順、失敗の stage と完了した段）と exit code（成功 0、失敗 1）で終わり、成功したら自分で終了する。主張を崩しうる既存の経路: (a) `RunsPlayerContentSmoke` の分岐を種類で変える所（`smoke` を `playable` と読む誤りで smoke の尾を飛ばす）。(b) baked config の読み（`PlayerBakedContentConfiguration` の ctor）に key を足す所。key の無い値の既定。(c) protected の比較（`PlayerContentConfiguration.Resolve`）に key を足す所で、今の 9 つの比較を変えること。(d) coordinator の `Build` を種類で分ける所で、動作確認の Player の宣言・probe・`RuntimeJson`（`playerSession` の追加を除く）・出力先・build の設定を変えること。(e) SampleGame の失敗の override を種類で分ける所（`smoke` で receipt を書かなくなる誤り）。(f) `CleanupAfterStartupFailureAsync` の Player の分岐（初回 Scene の unload の条件）。(g) S3-1・S3-2 が足した、Player では通らない分岐と session の層（Player の実機では未確認）。(h) B 適応で `link.xml` に型を足すと、動作確認の Player の build にも効くこと（型を残すだけで、契約の観測は変えない）。探索した境界: `AbstractApplicationInitializer` の Player の構成に関わる分岐の全て、`AppInitializer` の Player の override、`PlayerContentSmoke`、coordinator・mutation・adapter・publisher、Artifacts の `BuildContract.psm1` の receipt の読み、`ContentDeliveryPlayBridge`。未確認: repo の外で BS4 の Player を起動して receipt を読む手順（repo に起動の code は無い）。
- **I2 protected config を重ねる値で迂回できない。** どちらの種類の Player でも、環境変数と command line で baked の値（新しい `content:playerSession` を含む）と違う値を置くと、起動は `InvalidConfiguration` で失敗し、別の種類・別の content・Addressables で動き続けない。主張を崩しうる経路: (a) 重ねる順（JSON → 環境変数 → command line）で後の値が勝つこと。(b) `ReadContentDirectoryConfiguration` の `content:runtimeMode` の早い比較の後、`addressables` で `return null` する分岐（Player では比較が先にあるので今は塞がっている）。(c) 種類を決める所が、`Resolve` の比較より前に重ねた後の値を読むこと（分岐は baked を読むので塞がる。比較の前に分岐すると、override した種類で動いてから拒まれる）。(d) installed の pair を置いた時に `Resolve` が baked の contentSet と build identity で照合する所。(e) `ContentOverlayStartup.Decide` が Player の構成で上書きの key を拒むこと。探索した境界: config の 3 つの provider、`ReadContentDirectoryConfiguration`、`PlayerContentConfiguration`、`ContentOverlayStartup`。A2 の反証で破る順序は見つからなかった（§5.1）。
- **I3 起動に失敗した遊べる Player は動き続けない。** 遊べる Player の起動が失敗した時（baked config の欠け・不正な値、protected の不一致、content directory が無い・照合が合わない、directory の登録・初回 Scene の load の失敗、成功の通知の失敗）、Player は log に失敗の stage と例外を残して exit 1 で終わり、content の無い状態や Addressables・別の Scene で操作を受け付け続けない。主張を崩しうる経路: (a) `BeforeSceneLoad` の失敗後の通知が `ShouldNotifyPlayerContentFailure`（Player の構成）にだけ依ること。(b) `AfterSceneLoad` の `catch (OperationCanceledException) when (!UseRequiredPlayerFileConfiguration)`。(c) 遊べる Player の成功の通知（新しい hook）が投げた時の扱い（catch に入り失敗の経路を通る）。(d) `CleanupAfterStartupFailureAsync` が遊べる Player の初回 Scene を unload すること（上の決定）。(e) 失敗の override の中の書き込みが投げて `Application.Quit(1)` に届かないこと（A2 の B-1。`finally` で塞ぐ）。(f) 起動を終えた後（初回 Scene が安定した後）の game の code の失敗は起動の失敗ではない（この不変条件の外。§2.3）。探索した境界: `BootstrapAfterSceneLoad`、`InitializeAfterSceneLoad` の try/catch、`CleanupAfterStartupFailureAsync`、SampleGame の失敗の override。未確認: 失敗の後に `Application.Quit(1)` が Player の窓を閉じ切るまでの間の入力（Quit の後の frame）。
- **I4 遊べる Player は成功しても、起動の段として動作確認の尾を走らせない。** 遊べる Player が初回 Scene を安定させた後、利用者の操作が無い間に、起動の段として初回 Scene の unload、fixture の load、directory の close、`Application.Quit` は起きない（利用者の操作による Scene の遷移は含まない）。主張を崩しうる経路: (a) `RunsPlayerContentSmoke` の分岐。(b) SampleGame の `OnPlayerContentShutdownCompletedAsync`（`Application.Quit(0)`）が遊べる Player の経路から呼ばれること。(c) `RegisterAlreadyLoadedScenes` と初回 Scene の load の順。探索した境界: `InitializeAfterSceneLoad` の末尾、`AppInitializer` の override。

**成立条件**（起きなければならないこと）:

- **E1** 新しい menu で build した遊べる Player を同梱の content から起動すると、初回 Scene（`Title`）が安定し、Player は自分では終わらずに動き続ける。起動の到達を外から読める印（Player の log の 1 行）を残す。
- **E2** 遊べる Player の窓で、Title の Start を押すと Home の Scene へ移り、操作を続けられる。
- **E3** 遊べる Player の窓を閉じると、Player は exit 0 で終わる（directory を登録したままの quit の解放が、終わらない・exit が 0 でない、にならない）。
- **E4** 動作確認の Player の実機の非退行を 6000.6.9f1 で確かめる（今の menu の Player が成功の receipt と exit 0 で終わる。S3-1・S3-2 の後の起動を Player で通す）。
- **E5** 遊べる Player の構成（backend、stripping、development build）が §2.1 の決定どおりであることを、build の出力と build report で確かめる。

### 2.2 受け入れ条件

全て §5 の判定必須テストか検証経路で閉じる。Player の起動を伴う条件は、Phase C の実行担当が §5 のコマンドで取る。時間の制限を超えて終わらない起動は不合格で、record に `timeout` と書き、process を kill する。

- **AC1（I1・E4）動作確認の Player の成功:** 今の menu と同じ coordinator の経路（種類 `smoke`）で build した Player を `-batchmode` で起動すると、300 秒以内に exit 0 で終わり、`bs4-smoke-receipt.json` は `schemaVersion` 2、`succeeded` 真、`stage` `completed`、`completedStages` は 10 段を今の順に全て持つ。record にその Player の Unity の版（transport manifest の `unityVersion`）が `6000.6.9f1` と残る。合格例: 10 段が全てある。不合格例: `first-scene-stable` の後に `fixture-*` が無い、exit 0 だが receipt が無い。
- **AC2（I1・I2）動作確認の Player の protected:** 同じ Player を `-batchmode` で、環境変数 `SAMPLEGAME_CONTENT__PLAYERSESSION=playable` を置いて起動すると、60 秒以内に exit 1 で終わり、receipt は `succeeded` 偽、`stage` `before-scene-load`、`completedStages` は空、`error` に `Protected Player` を含む。
- **AC3（I1）baked config:** 動作確認の Player の `RuntimeJson` が持つ key と値は、今の 12 の値（`content` の 9 つ、`debugSocket:enabled`、`telemetry:profiler:enabled`、`world:cellCompanionSet`）に `content:playerSession` = `smoke` を足しただけである。遊べる Player の `RuntimeJson` は `contentSet` `spring-full`、`probeToken` 空、`playerSession` `playable`。key が無い baked config は `smoke` と読み、他の値は `InvalidConfiguration`（単体テスト）。
- **AC4（E1・I4）遊べる Player の起動:** 新しい coordinator の経路（種類 `playable`）で build した Player を窓を出して（`-batchmode` なし）起動すると、120 秒以内に Player の log に `[AppInit] Player content first scene: scene=Title … session=playable stable=True` の 1 行が出て、game の log（`OneStarMakerLocal/Log/`）に `TitleScene` の `Loaded.` が出る。その行の後 60 秒の間 Player は終わらず、Player の log に `run-player-content-smoke`、`unload-player-first-scene`、`close-player-content-directory`、`AfterSceneLoad failed` の語が出ず、exe の横に `bs4-smoke-receipt.json` が無い。不合格例: 60 秒の間に exit した、receipt がある。
- **AC5（E3）遊べる Player の終わり:** AC4 の Player の窓に閉じる要求（`Process.Refresh()` の後に `Process.CloseMainWindow()`）を送ると、120 秒以内に exit 0 で終わる。
- **AC6（E2）初回 Scene の先:** AC4 と同じ build の遊べる Player を窓を出して起動し、人間が Title の Start を 1 回押す。game の log に `OnStabledImpl HomeScene` の行が出て、Start を押してから窓を閉じるまでの game の log に Error 以上の行が無く、Player の log に `AfterSceneLoad failed`、`MissingMethodException`、`was stripped` の語が無い。判定は AI が log で行い、人間の役割は Start を押すことと、押したことの記録である（§5「人間の判断が必要な条件」）。
- **AC7（I2・I3）遊べる Player の protected:** 遊べる Player を窓を出して、(a) 環境変数 `SAMPLEGAME_CONTENT__PLAYERSESSION=smoke`、(b) `SAMPLEGAME_CONTENT__RUNTIMEMODE=addressables` を置いて、それぞれ起動すると、どちらも 60 秒以内に exit 1 で終わり、Player の log に `Protected Player` を含む例外の行があり、`Player content first scene` の行が無く、receipt が無い。
- **AC8（I3）遊べる Player の content の失敗:** 遊べる Player を窓を出して、存在しない path の `SAMPLEGAME_CONTENT__INSTALLEDREVISIONPATH` と 64 桁の 16 進の `SAMPLEGAME_CONTENT__MANIFESTSHA256` を置いて起動すると、60 秒以内に exit 1 で終わり、Player の log に `BeforeSceneLoad が失敗したため AfterSceneLoad をスキップします` の行があり、`Player content first scene` の行が無い。
- **AC9（I1・I2・I3）単体テスト:** `content:playerSession` の読み（無い → `smoke`、`smoke`・`playable` はそのまま、それ以外は `InvalidConfiguration`）、`Resolve` の比較が `content:playerSession` の override を `InvalidConfiguration` で拒むこと（文字列の ordinal 比較）、今の 9 つの key の override の拒みが変わらないこと、`RunsPlayerContentSmoke(bool, PlayerContentSession)` が Player の構成かつ `smoke` の時だけ真、`NotifiesPlayerContentStarted(bool, PlayerContentSession)` が Player の構成かつ `playable` の時だけ真、失敗時の初回 Scene の unload の判定が Player の構成だけで決まること、を表の単体テストで固定する。
- **AC10（E5）遊べる Player の構成:** 遊べる Player の build の record に、出力に `GameAssembly.dll` があり `SampleGame_Data/Managed/` が無いこと（IL2CPP）、build report の options に `Development` が無いこと、build の間の managed stripping の水準が High だったこと（mutation の中で読んだ値）が残る。
- **AC11（I1・I2・I3）回帰:** 判定必須の全 EditMode 回帰（§5）。

### 2.3 ここでは答えない問いと所有する後続

- 上書きを Player に重ね、照合の失敗を理由つきで示すこと: **S4-2**（program §8）。遊べる Player の `playable` の経路が土台になる。**S4-2 への入力:** 今の installed の pair の照合は Player と同じ build identity の revision しか通さない（`InstalledRevisionVerifier.cs:78-82`）ので、別に build した base を遊べる Player に載せる形は、この照合のままでは成り立たない。
- Player での要求時解決の配線と record: **S4-2**（重ねるには session の層と要求時解決が要る。配線するかを S4-2 の A1 が決める）。
- Player の構成に入らない Player の起動で、設定の Addressables からの読みが strip で `MissingMethodException` になる形（S4-0 の J1）: **S4-2**（S4-0 の harness を測り直す時に同じ形に当たる。製品の起動の外の Player を扱うのは S4-2 の harness だけ）。
- Mono、他の stripping の水準、development build、`-nographics` の Player: 所有する後続なし（**program §10 に未所有として足す**）。要る時に owner が行を置く。
- 遊べる Player を Artifacts の保存系列へ入れること（今の系列は target と backend・stripping だけで決まり、種類を区別しない）: [BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md](BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md) の入力。遊べる Player は `artifacts/playable/` に出し、`artifacts/bs4/` と混ぜない。
- 起動を終えた後の game の code の失敗（InGame の world、streaming、VFX、Spring 以外の Season への操作）を Player で網羅すること、性能: 所有する後続なし（program §10 に未所有として足す）。AC6 が見るのは Title → Home までである。
- Android・iOS の Player: **S4-5**。
- 遊べる Player の DebugSocket（DebugStudio の接続）: 遊べる Player の baked config は動作確認の Player と同じく `debugSocket:enabled` を既定で偽にする（protected ではないので環境変数で真にできる。保護は足さない）。DebugStudio からの操作を検証に使うことは、所有する後続なし（program §10 に未所有として足す）。

### 2.4 スライス分割の判断

- **契機:** 高リスクの初回実機スライスで独立して失敗しうる未知が複数ある（(u1) 6000.6.9f1 の動作確認の Player、(u2) Title の先の game の code の strip、(u3) directory を登録したままの quit、(u4) 窓を出した起動）。
- **判断: 分けない。** (a) (u1) は E4 で、S4-1 の行の gate そのものであり、S4-1 が足す分岐の非退行の証拠でもある（I1）。分けると、S4-1 の変更を入れた head の非退行をもう一度取る必要がある。(b) (u2)〜(u4) は同じ遊べる Player の build 1 つで取れ、片方の失敗がもう片方の観測を妨げない（AC4・AC5 と AC6 は別の起動）。(c) 失敗の写像を §4 の差し戻し条件に分けて書く（(u1) の失敗は既存の退行か環境の不備、(u2) の SampleGame の型の欠けは B 適応、(u3) は Phase A へ戻す）。(d) **分ける条件:** (u2) で SampleGame の型の欠けの B 適応が 3 件を超えるか、Framework・Unity の組み込みの型の欠けが出たら、Title の先の strip の修正を別のスライスに分けるかを owner に返す。
- **(c) 開発用の Player の構成を IL2CPP・High・development build でない、にする理由:** 動作確認の Player と S4-0 が測った Player と同じにすると、S4-2 の最初の gate の「S4-1 が決めた構成が S4-0 と違う時の測り直し」が要らなくなり、Artifacts の保存系列の値（`il2cpp-high`）の外に出ない。strip の失敗（J1 の形）は、配布する Player と同じ構成で見つける方が、配布の前に見つけられる（Mono にすると隠れる）。development build の利点は主に stack と profiler の接続で、repo の code はそれに依らないが、S4-0 が測っていない構成になり S4-2 の測り直しを持ち込む。

### 2.5 未決事項（A3 で owner に確かめる）

- **(1) AC6 の Start を人間が 1 回押すこと:** Title の Start は UI Toolkit の button の click で、AI が外から押す既存の経路は無い。A3 は「人間は押すだけ、判定は AI が game の log の `OnStabledImpl HomeScene` と Error の有無で行う」とした。入力を注入する検証支援は作らない（A2 の 3 件とも同じ意見）。
- **(2) 遊べる Player は receipt を書かない:** A1 は 2 つの種類で同じ receipt を書く形だったが、A2 の目的レビューと代替構成がどちらも「遊ぶ用途に判定の出力は要らない」とし、BS4 の証拠と混ざらない形を推したので、A3 は書かない形にした。起動の成功は Player の log の 1 行、失敗は log と exit 1 で示す。`bs4-runtime.json`・`Resources/BS4/`・`.bs4-owner`・latch の名前など、`bs4` の名は改名しない（I1 のため。harvest に書く）。
- **(3) 遊べる Player の content を probe なしの `spring-full` にすること:** probe は動作確認だけが使う fixture なので、遊べる Player には要らない。A1 は「Delivery が publish した `spring-full` の install を使えるようにするため」と書いたが、照合は同じ build identity しか通さないので（UC3）、この理由は誤りだった（A2 の M-1）。
- **(4) 失敗の override の Quit を `finally` にすること（動作確認の Player にも効く）:** 今は receipt の書き込みが投げると `Application.Quit(1)` に届かず、Player が Title などを出したまま動き続けうる（A2 の B-1）。`finally` にすると、動作確認の Player では「固まって終わらない」場合が「receipt なしの exit 1」に変わる。receipt を書ける時の観測（file 名、schema、stage、exit code）は変わらないので I1 の契約は変えないと読むが、owner に確かめる。

## 3. 責務マップ

| file | 責務（一文） | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 配置の理由 | 行数（今 → 予想） |
|---|---|---|---|---|---|---|---|---|
| `OneStarMaker/Scripts/Runtime/BuildContent/PlayerBakedContentConfiguration.cs` | baked config の Player の値を固定して持つ | 種類の key を足し、値を `PlayerSession`（internal enum `PlayerContentSession { Smoke, Playable }`）として読む。無い → `Smoke`、他の値 → `InvalidConfiguration` | initializer の `_bakedPlayerContent`（`BeforeSceneLoad` から `ReleaseAll` まで） | `AppConfig` | internal（enum も internal） | 単体（`AppConfig` を作って ctor を呼ぶ） | baked の値の読みと同じ変更理由。新しい file を作らない | 11 → 25 |
| `OneStarMaker/Scripts/Runtime/BuildContent/PlayerContentConfiguration.cs` | Player の content の指定を照合して正規化する | protected の比較に `content:playerSession` を足す（他の 9 つと同じ `GetString` の ordinal 比較） | 呼び出しの間だけ | `AppConfig`、baked | internal | 単体（今の `PlayerContentConfigurationTests`） | protected の比較の正本 | 55 → 57 |
| `OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs` | 3 フェーズの起動の順序、hook、失敗境界 | (1) `RunsPlayerContentSmoke` を (Player の構成, 種類) の 2 引数にし、成功の末尾だけで使う。(2) `playable` の時は初回 Scene を load したまま log の 1 行を出し、新しい protected virtual `OnPlayerContentStartedAsync(IReadOnlyList<PlayerContentStage>)`（既定は何もしない）を呼んで起動を終える（stage `load-player-first-scene` → `notify-player-content-started`）。呼ぶかの判定を internal static `NotifiesPlayerContentStarted(bool, PlayerContentSession)` にする。(3) 失敗時の初回 Scene の unload の条件を Player の構成だけの internal static にする（今の意味のまま）。(4) baked を読んだ時に種類を field `_latchedPlayerSession` に写し（`BootstrapBeforeSceneLoad` の冒頭で `Smoke` に戻し、`ReleaseAll` では消さない）、protected `bool IsPlayablePlayerSession` で派生に見せる（失敗の通知は `ReleaseAll` の後に呼ばれ、baked は既に消えているため） | 既存（initializer は App 寿命） | 既存のまま（新しい依存なし） | protected virtual 1 つ、protected property 1 つ（bool。internal enum を公開面に出さない）、internal static 2 つ | 判定は単体（internal static）、起動の段は Player の実機（§5） | 起動の順序の正本。分岐は `RunPlayerContentSmokeAsync` の隣に置く | 1326 → 1370（行数警報: 500 を超える既存 file。**非分割**。足すのは既存の Player の分岐の隣の数十行で、起動の順序と同じ変更理由・同じ寿命。分けると順序が 2 つの file に割れる。既存の大きさはこのスライスが作ったものではない） |
| `SampleGame/DependOnAll/AppInitializer.cs` | SampleGame の起動の hook の実装 | 失敗の override を「`IsPlayablePlayerSession` が偽なら今と同じ receipt を書く。`finally` で `Application.Quit(1)`」にする。`OnPlayerContentStartedAsync` は override しない（既定の何もしない） | 既存 | 既存のまま | なし | Player の実機（§5） | Player の override が並ぶ所 | 431 → 437 |
| `SampleGame/DependOnAll/PlayerContentSmoke.cs` | 動作確認の fixture の列と receipt の書き込み | 変えない | — | — | — | 今の `PlayerContentSmokeSequenceTests` | — | 160 → 160 |
| `SampleGame/DependOnAll/Editor/Build/SampleGamePlayerBuildCoordinator.cs` | SampleGame の Player の build の手順 | `Build()` を `Build(PlayerContentKind)`（`Smoke`・`Playable`、Editor の internal enum）にし、content の build・mutation・adapter・publish の共通部分を 1 つの関数にする。`Smoke` は今と同じ（宣言 `bs4-spring-full`、probe、出力 `artifacts/bs4`）で `RuntimeJson` に `playerSession` = `smoke` を足す。`Playable` は宣言 `spring-full`、probe を作らず合成しない、`probeToken` は空、`playerSession` = `playable`、出力 `artifacts/playable`。menu を 1 つ足す。`BuildReceipt` は変えない。build の結果（Player の directory、build report の options、mutation の中で読んだ stripping の水準）を返す internal の値を足す（AC10 の record 用） | 呼び出しの間 | 既存のまま | menu 1 つ。internal | `RuntimeJson` と種類ごとの宣言・出力先の選びは単体。build は Phase C の実機 | Player の build の正本 | 152 → 200 |
| `SampleGame/Tests/Editor/Build/Bs4FixtureComposerTests.cs` | 既存の `RuntimeJson` の単体テスト | `RuntimeJson` の引数の変更に合わせ、AC3 の 2 つの種類のテストをここに足す | test | 既存 | — | Unity なし | `RuntimeJson` の既存のテストの所 | 85 → 120 |
| `OneStarMaker/Tests/BuildContent/PlayerContentConfigurationTests.cs` | Player の構成の単体テスト | AC9 | test | 既存 | — | Unity なし | 既存の表のテストの隣 | 174 → 230 |
| `SampleGame/Tests/Editor/Build/PlayerKindEndToEndTests.cs`（新規、`[Explicit]`。namespace は `Bs4FixtureComposerTests` と同じ `OneStarMaker.Tests.Editor.Build`） | 2 つの種類の Player を build・起動して観測する | AC1・AC2・AC4〜AC8・AC10 と AC6 の準備 | test。出力は `TestResults/s41/<run id>/` | `SampleGame.DependOnAll.Editor`、`System.Diagnostics.Process` | — | Unity の batch と Player の process（Phase C） | 製品の coordinator を呼ぶので SampleGame の Editor の test。`[Explicit]` で通常の回帰に混ぜない | 0 → 300 |
| `unity/Assets/Docs/Architecture/04-app-startup.md`（Phase B は触らない。harvest） | — | — | — | — | — | — | — | — |

- **asmdef と依存:** 新しい asmdef・参照は無い。Game → Framework の向きは変わらない（Framework は種類の語彙と分岐を持ち、SampleGame は receipt の書き方と build の手順を持つ）。
- **`PlayerContentSession` を Framework に置く理由:** protected の比較と起動の分岐が同じ値を読む。App に置くと、App が Framework の分岐（smoke の尾）を種類で止める hook が要り、protected の比較の外に値が出る。
- **`PlayerContentKind`（Editor）と `PlayerContentSession`（Runtime）を分ける理由:** coordinator の enum は build の手順（宣言・probe・出力先）を選び、Runtime の enum は起動の分岐を選ぶ。Editor の asmdef から Runtime の internal は見えない（`InternalsVisibleTo` の相手は `OneStarMaker.Editor` と test）。coordinator は baked の文字列（`smoke`・`playable`）を書くだけにする。2 つの対応は AC3 の単体テストと AC1・AC4 の実機で閉じる。

## 4. 実装計画

- 変更対象: §3 の表の file。
- 順序: (1) `PlayerBakedContentConfiguration` と単体テスト。(2) `PlayerContentConfiguration.Resolve` の比較と単体テスト。(3) `AbstractApplicationInitializer` の分岐、hook、log、latch、判定の internal static と単体テスト。(4) `AppInitializer` の失敗の override。(5) coordinator の種類、menu、`RuntimeJson`、build の結果の値と単体テスト。(6) `PlayerKindEndToEndTests`（`[Explicit]`）。(7) Unity の Editor でコンパイルを確かめる（[osm-unity-editor](../../.agents/skills/osm-unity-editor/SKILL.md)）。Unity の test の実行、content と Player の build、Player の起動は行わない（Phase C）。
- **`PlayerKindEndToEndTests` の形:** 1 つの test が 1 つの Unity process で 1 つのことをする。全て `[Test, Explicit, Timeout(7200000)]`。
  - (a) `BuildSmoke`・`BuildPlayable`: coordinator の `Build(kind)` を呼び、返った Player の directory と build の結果（AC10 の観測、transport manifest の `unityVersion`）を `TestResults/s41/<run id>/<kind>.json` に書く（run id は環境変数 `S41_RUN_ID`、無ければ失敗）。
  - (b) `LaunchSmoke`・`LaunchSmokeOverride`・`LaunchPlayable`・`LaunchPlayableSessionOverride`・`LaunchPlayableAddressables`・`LaunchPlayableMissingInstall`: (a) の値を読み、Player の directory を `TestResults/s41/<run id>/<短い case 名>/` へ写してから（同じ build の起動が receipt と game の log を上書きし合わないように）、`-logFile <case>/player.log` を付けて起動する。`SAMPLEGAME_` で始まる環境変数を除いた環境に case の環境変数だけを置く。写した先の最長の path が 259 文字を超えるなら、起動の前に test を失敗させる（Unity 同梱の Mono と Player の path の制限。起動前の path の予算の検査の対象外のため）。動作確認の Player は `-batchmode`、遊べる Player は窓を出す。
  - 待つのは process の exit（`WaitForExit(timeout)`）、file の出現（`FileSystemWatcher` の Created と Renamed を購読し、有効にした後に `File.Exists` を見直してから `ManualResetEventSlim.Wait(timeout)`）、Player の log の行の出現（同じ watcher で Changed を受けて読み直す）だけで、`Task.Delay` と `Thread.Sleep` は使わない。「60 秒の間終わらない」は `WaitForExit(60000)` が偽を返すことで見る。遊べる Player の窓を閉じる前に `Process.Refresh()` を呼ぶ。
  - 終わりに pid の process が残っていれば kill し、record（case、pid、exit code、`timeout` の有無、receipt の有無と全文、Player の log と game の log の該当行、build の結果の値）を `TestResults/s41/<run id>/record.jsonl` に書く。XML の合格は record を書き終えたことだけを示し、AC の合否は record で判定 C が読む。
  - (c) `PrepareHumanCheck`（AC6 の準備）: (a) の遊べる Player を `<run>/human/` へ写して窓を出して起動し、pid と path を record に書いて、Player の exit を待つ（上限 1800 秒）。人間が Start を押して窓を閉じた後、game の log と Player の log の AC6 の語の有無を record に書く。
- **Phase B から Phase A へ差し戻す条件:** §3 に無い file・asmdef・参照・公開 API が要る。`content:playerSession` を protected の比較に入れずに済ませる形にしたくなる。種類の分岐を App の hook だけで実装したくなる（Framework の smoke の尾を App が止める形）。`ContentDirectorySession` か `AssetManagement` の終了の経路を変える必要が出る（(u3)）。
- **B 適応として許すもの:** Phase C で Title の先に SampleGame の asmdef の型の strip による欠けが見つかった時、`SampleGame/DependOnAll/link.xml` に、その型（SampleGame が持つ assembly の型だけ）を足す。動作確認の Player の build にも効くが、契約の観測（receipt、exit code）は変えない（I1 (h)）。3 件を超えたら §2.4 (d)。Framework・Unity の組み込みの module の型の欠けは A 再開。
- **禁止:** 動作確認の Player の `RuntimeJson` の今の値（`playerSession` の追加を除く）、宣言、probe、出力先、build の設定、`BuildReceipt` を変えること。receipt の schema と file 名を変えること。`bs4` の名を持つ file・folder・latch を改名すること。`ContentOverlayStartup` と `WireContentResolution` の Player の分岐を変えること。Delivery の窓と Artifacts の code を変えること。入力を注入する検証支援を製品に足すこと。
- 対象外を維持する方法: 上の禁止と、AC3 の単体テスト（動作確認の Player の baked config の不変）と、Phase C の構造レビューでの diff の確認。

## 5. テストとレビュー計画

- 単体テスト: `PlayerContentConfigurationTests`（AC9）、`Bs4FixtureComposerTests`（AC3）、既存の `PlayerContentSmokeSequenceTests`。
- 差し戻し中の起点 `-Filter`: `OneStarMaker.Tests.BuildContent.PlayerContentConfigurationTests;OneStarMaker.Tests.BuildContent.PlayerContentSmokeSequenceTests;OneStarMaker.Tests.Editor.Build.Bs4FixtureComposerTests`。
- 判定必須テスト（GO 候補 head）: 次を順に、それぞれ別の Unity process で実行する（環境変数 `S41_RUN_ID` を全てで同じ値にする。`T` は `OneStarMaker.Tests.Editor.Build.PlayerKindEndToEndTests`）。(1) 起点 filter。(2) `pwsh tools/run-tests.ps1 -Filter T.BuildSmoke`。(3) `T.BuildPlayable`。(4) `T.LaunchSmoke`（AC1）。(5) `T.LaunchSmokeOverride`（AC2）。(6) `T.LaunchPlayable`（AC4・AC5）。(7) `T.LaunchPlayableSessionOverride`（AC7 (a)）。(8) `T.LaunchPlayableAddressables`（AC7 (b)）。(9) `T.LaunchPlayableMissingInstall`（AC8）。(10) `T.PrepareHumanCheck`（AC6）。(11) 全 EditMode 回帰（`pwsh tools/run-tests.ps1`）。(2)(3) は Project Auditor の post-build 監査で `step.json` が `failed` になりうる（S4-0 の J2）ので、合否は XML（`Passed` と実行した test 名・件数）と record で読む。全 EditMode 回帰は既知の staging の `Directory.Move` の access denied（program §8 の S5-0）で落ちうる。落ちた test が他の回で通るかの読みは判定 C の担当が record に書く（このスライスで読み方を変えない）。
- 全 EditMode 回帰の適用除外: なし。
- 統合・Unity テスト: 上の (2)〜(10)。
- 操作・実行時・目視条件の検証経路:
  - AC1・AC2・AC4・AC5・AC7・AC8・AC10: 上の (2)〜(9) で閉じる。担当は Phase C の実行担当（Unity の batch と Player の起動。窓を出せる対話の session で実行する）。初期状態: Editor を閉じ、Active Build Target が StandaloneWindows64、作業ツリーが clean、他の Unity と `SampleGame.exe` が動いていない。事前に `git status --porcelain` を取る。観測は record・各 case の Player の log と game の log・receipt・build の結果・XML。事後に `git status --porcelain` を取り（`PlayerBuildProjectMutation` の戻しの確認）、record の pid が全て終わっていることを CIM で確かめる（`Get-Process` は見えないことがある）。証拠は `TestResults/s41/<run id>/` の全て（写した Player の本体を除く）と XML を evidence bundle に入れる。
  - AC6: 担当は人間（owner）と Phase C の実行担当。実行担当が (10) を起動し、人間が Title の Start を 1 回押し、Home の画面に移ったら窓を閉じる。判定は実行担当が record の log の行で行う。人間は、押したこと、日時、対象の build identity を §7 に記録する（Home に移ったかの判定を人間の観察に置き換えない）。
  - 動作確認（Phase B の後）: `対象なし`（offline のテストで閉じない条件は全て Player の build を要し、Phase C が担当する）。
- 未知の操作経路の疎通: 次は未確認で、**初回確認は Phase C**（理由: Unity の test と Build の実行は Phase C の責任。担当: Phase C の実行担当）。
  - (u1) 製品の coordinator を EditMode の test から呼んで、6000.6.9f1 で動作確認の Player を build・起動できるか（S4-0 は自作の runner の Player を test から build した。製品の coordinator の test からの build は前例が無い）。確認地点: (2) と (4)。不成立時: 環境の不備（toolchain 等）は実行担当が原因を記録し、担当の権限で直せない物は owner へ返す。test から coordinator を呼べないことが原因なら Phase A へ戻す。AC1 の失敗が S4-1 の変更に依らない退行（S3-1・S3-2 の後の起動）かを切り分けるには、base `9de34cc2` の別の worktree で、menu と同じ `SampleGamePlayerBuildCoordinator.Build()` を Editor の eval（[osm-unity-editor](../../.agents/skills/osm-unity-editor/SKILL.md)）で呼んで build し、同じ起動（`-batchmode`、環境変数なし）で receipt と exit code を見る。base でも同じなら owner へ返す（このスライスで直すかは owner が決める）。
  - (u2) Title の先（Home）が IL2CPP・High で動くか。確認地点: AC6。不成立時: SampleGame の asmdef の型の欠けは B 適応（§4）、それ以外は Phase A へ戻す。
  - (u3) directory を登録したままの quit が Player で exit 0 になるか。確認地点: AC5。不成立時: Phase A へ戻す（`ContentDirectorySession` の終了の経路を変えるかは設計判断）。Unity の Editor の終了の stall（[Harness README の停止規則](../../tools/Harness/README.md#unity終了時の既知native-stallと調査の停止規則)）とは分けて記録する。
  - (u4) 窓を出した Player を Unity の batch の test から起動し、`CloseMainWindow` で閉じられるか。確認地点: (6)。不成立時: 窓の無い環境なら実行担当が記録して owner へ返す。
- 人間の判断が必要な条件: AC6 の Start の 1 回の押下だけ。担当は owner。判定は AI が log で行う（観察記録で判定を置き換えない）。§2.5 の (1)。
- 高コスト証拠: なし。Player の build は外部への作用が無く、この PC で繰り返せる（2 回、各約 6 分の見込み。S4-0 の実測）。
- 判定 C の照合 script の方式: なし。判定 C は record と receipt と log を AC ごとに読む。
- 機械検査: `contract-audit`、`docs-audit`。
- A0/A1 主担当: 本 session（Claude Opus 5.5、Anthropic）。A0 は program §8 の S4-1・S4-2 の行、「S4 の A1 への入力（2026-11-23）」の (1)、「S4-0 の A1 が振り分けた入力」と「S4-0 から移送した入力」の S4-1 の項目、実測の事実の Player の節、04 章を入力とした。
- C' 用に予約した担当: Phase A・B・C に関与していないベンダー（`cursor-agent` を使う場合は AGENTS.md の契約どおり Grok 系だけ）。A2 では Anthropic の系列だけを使った。
- 独立性の強化条件を満たせない場合の理由: 凍結前の外部計画レビューが使った系列は Phase A に関与した系列として §5.1 に記録する。C' の充足は起こす時に確かめて §8 に記録する。

### 5.1 A2 の独立レビューと A3 の採否

**A2 の独立レビュー（実施 2026-11-23。互いの指摘を見せていない。全て Anthropic の系列で、別ベンダーは外部計画レビューと C' 用に残した）:**

- (1) **目的レビュー:** 本 session が起動した新しい subagent（Claude Opus 5.5、Anthropic）。リポジトリを読まず、ツールを使わずに回答した。入力は §1 の目的と利用者の欄の原文だけで、sha256 は `59f93324c209af2912eae588f1c987ae2ed0cd9b0c5282d592d1815730ec3123`（目的欄の本文、改行、利用者欄の本文。UTF-8、LF、末尾改行なし）。問いは「この目的のために何を保証すべきか」「何を作らないべきか」の 2 つだけ。
- (2) **A0 だけからの代替構成:** (1) と同じ subagent が、目的レビューの回答を記録した後に続けた。入力は A0 の packet（program の S4-1・S4-2 の行、「S4 の A1 への入力」の (1)、「S4-0 から移送した入力」の S4-1 の項目の原文と、読んでよい範囲。sha256 `0c6acffc28e80e5cd374a45cf3f710904e83076158a2790182e8d92f0e88207e`）と、固定 base `9de34cc2ec2dfe82b782f122b0012b902e98f277` の clean な detached checkout。本書は読んでいない。
- (3) **アーキテクチャゲート兼、各不変条件を破る順序・受け入れ条件の曖昧さ・検証経路の実行可能性・分割・未決事項:** 本 session が起動した別の新しい subagent（Claude Opus 5.5、Anthropic。A1 の主担当と同じモデルの別 session）。入力は A2 入力版の本書（内容 sha256 `db15ab00a8bcfe0ccc34ea58ae9277acfc1faeb97753eabb7d9329c528411b76`）、変更後の program の写し（sha256 `17169bd607911e1bc3a7f9bb78bc42a81bf4eb54d75950f8f0153160781f4a61`）、(2) と同じ固定 base の checkout。枠は「§3・§4 の計画した code は実装済みと仮定し、現在の code は既存の前提の確認にだけ使う」に固定した。

**A3 の採否（統合担当: 本 session。目的と利用者の欄は A1 の後に変えていない。目的レビューのやり直しは無い）:**

(1) 目的レビュー。保証すべき 10 点のうち、初稿に無かった・弱かったものと、作らないべき 10 点のうち初稿と食い違ったもの:

| 指摘 | 採否 | 理由と反映 |
|---|---|---|
| 失敗の理由を、exe を直に起動した開発者が見つけられる場所に残す | 採用 | 遊べる Player の失敗は Player の log（stage と例外）と exit 1 で示す（§2.1、AC7・AC8）。game の log の置き場も §1 の現況に書いた |
| どの構成・どの content revision で作られたかを後から確かめられる | 一部採用 | 構成は AC10 の record で確かめる。製品の Player に新しい記録の欄は足さない（build receipt の identity と transport manifest で足りる） |
| S4-2 が上書きを重ねる位置を塞がない | 採用 | `playable` の経路を土台とし、照合が同じ build identity しか通さないことを S4-2 への入力に書いた（§2.3） |
| 遊ぶ Player に BS4 の receipt と同じ形の判定出力を持たせない | 採用 | A1 の「同じ receipt を書く」を落とした（§2.1、§2.5 の (2)、§1 の対象外）。(2) の代替構成も同じ |
| backend・stripping・development build を選ぶ UI・menu の分岐を作らない | 初稿のとおり | §1 の対象外に明記した |
| content の出所を任意の path・URL で指定する引数を作らない | 初稿のとおり | 既存の口（installed の pair）だけ。新しい口は無い |
| protected の検証を緩める開発用の逃げ道、失敗の再試行・fallback を作らない | 初稿のとおり | §1 の対象外に明記した |
| CLI・CI の build の入口を新設しない | 採用（明記） | 判定の test の `[Explicit]` の入口は検証用で利用者の入口ではない、と §1 の対象外に書いた |
| Windows 以外の抽象化、installer・配信、revision を選ぶ UI を作らない | 初稿のとおり | どれも作らない |

(2) A0 だけからの代替構成。推奨案（同じ生成 bootstrap と同じ protected config の経路で、build の時に焼いた protected key 1 つで smoke と play を分ける、key が無ければ smoke、`RunsPlayerContentSmoke` の判定だけを変え `ShouldNotifyPlayerContentFailure` は変えない、IL2CPP・High・development build でない、probe なしの `spring-full`、Title より先は owner の手）は初稿と同じ骨格だった。違った点の採否:

| 指摘 | 採否 | 理由と反映 |
|---|---|---|
| 遊べる Player の失敗は receipt を書かずに `Quit(1)`（BS4 の証拠と混ぜない） | 採用 | §2.1、§2.5 の (2)。成功も receipt を書かない |
| 遊べる Player の出力を BS4 と別の root にする | 初稿のとおり | `artifacts/playable/` |
| build receipt に backend・stripping・development の 3 つを記録する | 不採用 | 今の欄は定数を書くだけで観測にならない（(3) の m-5）。構成は Player の出力と build report で AC10 として確かめ、`BuildReceipt` は変えない |
| 成功の観測に Title の `Loaded.` を使う | 採用 | AC4。game の log にある（(3) の M-3） |
| 失敗の例に `RUNTIMEMODE=addressables` の override を足す | 採用 | AC7 (b) |
| coordinator の共通部分を関数に括り出す | 採用 | §3 |
| 起動の途中で窓を閉じると cancel が失敗として `Quit(1)` になる | 採用（記録） | 害は小さい。I3 (b) の経路として残す |
| Title の先の strip の修正が数件を超えたら分ける | 採用 | §2.4 (d)（3 件） |
| 生成する bootstrap Scene の名前で種類を分ける | 不採用（代替の検討で本人も不採用） | 判定が SampleGame の Scene 名と Framework に割れる |
| key の名前 `content:playerRun`、値 `play` | 不採用 | 名前だけの違い。`content:playerSession`・`playable` のまま |

(3) アーキテクチャゲート兼反証。blocker 1 件、major 3 件、minor 11 件。

| ID | 指摘 | 採否 | 理由と反映 |
|---|---|---|---|
| B-1 | 失敗の override の receipt の書き込みが投げると `Quit(1)` に届かず、遊べる Player が Title を出したまま動き続ける | 採用 | `finally` で必ず Quit する（§2.1、I3 (e)、§3）。動作確認の Player にも効くので §2.5 の (4) で owner に確かめる |
| M-1 | `spring-full` を選ぶ理由（Delivery の install を使える）が、照合が同じ build identity しか通さない code と食い違う | 採用 | 理由を直し（§2.5 の (3)）、UC3 と §1 の現況に制約を書き、S4-2 への入力にした（§2.3） |
| M-2 | 失敗時の初回 Scene の unload の条件が 2 引数の判定で変わり、遊べる Player で Title を残したまま close に入る | 採用 | unload の条件は Player の構成のまま（§2.1、§3、AC9） |
| M-3 | AC6 の log の検査が ZLogger の出力を見ていない | 採用 | game の log（`OneStarMakerLocal/Log/`）の `OnStabledImpl HomeScene` と Error を AI が読む形にし、人間の役割を押すことだけに狭めた（AC4・AC6、§5） |
| m-1 | Delivery の窓は Player を起動しない。Artifacts は欄の追加を拒まない | 採用 | §0 の risk、§1 の現況、I1 の未確認を直した |
| m-2 | 既存の `RuntimeJson` のテストが §3 に無い | 採用 | `Bs4FixtureComposerTests` を §3 に足し、AC3 のテストをそこに置く |
| m-3 | 同じ folder の namespace が 2 種類ある | 採用 | 新しい file は `OneStarMaker.Tests.Editor.Build` と明記し、filter を合わせた |
| m-4 | watcher の Renamed、`Timeout` の明示、`Refresh()`、写した path の長さ | 採用 | §4 |
| m-5 | build receipt の backend・stripping は定数で観測にならない | 採用 | AC10（`GameAssembly.dll`、`Managed/` の不在、build report の options、mutation の中の stripping） |
| m-6 | 時間の制限と `-batchmode` の有無が AC ごとに無い | 採用 | 全ての起動の AC に制限と起動の形を書いた |
| m-7 | I4 の「unload は起きない」が E2 の Title → Home と字面で矛盾する | 採用 | I4 を「操作の無い間に、起動の段として」に限った |
| m-8 | 分岐は baked を読むと明記。比較は文字列の ordinal にする | 採用 | §2.1、§3 |
| m-9 | E4 の版が record に残らない | 採用 | AC1 に transport manifest の `unityVersion` |
| m-10 | u1 の不成立時に base で確かめる手順が無い | 採用 | base の worktree で menu と同じ `Build()` を Editor の eval で呼ぶ手順（§5 の (u1)） |
| m-11 | DebugSocket は環境変数で真にできる | 採用 | 「既定で偽」に直し、保護は足さない（§2.3） |
| 所見 7 | `BuildReceipt.playerSession` は要らない | 採用 | `BuildReceipt` を変えない（§3、§4 の禁止） |
| 所見 5 | B 適応の `link.xml` が動作確認の Player の build にも効く | 採用 | I1 (h)、§4 の B 適応に明記 |

**外部計画レビュー:** 未実施（Draft PR で行う）。
