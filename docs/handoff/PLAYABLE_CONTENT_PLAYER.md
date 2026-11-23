# content directory から起動して遊び続けられる Windows の Player

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S4-1）
- status: `C`（Phase B を 2026-11-23 に完了した。発見 C は未着手）
- branch: `claude/playable-content-player`（PR の base は `develop`）
- implementation base commit: `32d3e04306d49dac4a8055caaca0196d10df11ad`（`develop` の head、2026-11-23。凍結の前に本ブランチへ merge した。§1 の現況と A2 の固定 base は 1 つ前の `9de34cc2` で、2 つの差は S5-0（PR #210: content の build と transport の publish の staging → final の移動の再試行、`ContentOverlayReuse` の別名の file の報告、その harvest）だけである。§3 の file はどれも変わっていない）
- implementation head commit: `eb8e4e06007f44a04ddefe5609d0129fa5dafde9`（Phase B の出口の commit。その後の進行役の記録 commit は `docs/handoff/` だけを変える）
- A3 凍結記録: 承認者は root（owner）、2026-11-23、対象は revision 0（この記録を含む commit。外部計画レビューの 2 巡目まで統合した commit `68f71368` の本書に、凍結の記録・status・引継ぎ票・§5.1 の凍結の行と、A3 の統合で落としていたテンプレートの §6〜§9 の空欄を足し、凍結の前に merge した `develop`（S5-0）に合わせて implementation base・§5 の全 EditMode 回帰の読み方・(u1b)・§2.3 の 1 行を改めた物で（program の S4-1 の行の状態も改めた）、目的・条件・責務マップは変えていない）。承認の根拠: 外部計画レビューの 2 巡目までを A3 で統合した後に、owner が凍結を指示した（「Freeze it」）。§2.5 の 4 点と、2 巡目の後の追記（`content:playerKind` を必須にしたこと、名前の改め、C' の予約、AC6 (5)）も、この指示で承認された
- risk: `high`（Framework の起動（`AbstractApplicationInitializer`）と Player の protected config を変える。動作確認の Player（BS4）の契約を壊すと、BS4 を起動して receipt を読む担当と Artifacts の Player の保存の経路が黙って変わる。Player を遊べる形で起動するのはこの project で初めてで、独立して失敗しうる未知が複数ある。§5）
- owner: root（依頼者）
- created: 2026-11-23
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: [04 章](../../unity/Assets/Docs/Architecture/04-app-startup.md) §4.1・§4.6・§4.7（Player の 2 つの種類、遊べる Player の起動の段と終わり方、protected config の key、`bs4` の名を持つ file・folder・latch を改名しない理由、baked を読めなかった（file の欠け・壊れ）か `playerKind` が欠け・不正な値の Player は動作確認の receipt を書くこと、起動が終わらない（hang）時は fail-closed にならないこと）、`unity/Assets/README.md` か repo の README の「試す」（遊べる Player の build と起動の手順。置き場は Phase D が既存の記述に合わせて決める）、[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S4-1 の行と、§2.3 の後続へ送る入力（S4-2 の行と §10）
- 関連: [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md)（§8 の S4-1・S4-2 の行、「S4 の A1 への入力（2026-11-23）」の (1)、「S4-0 の A1 が振り分けた入力」と「S4-0 から移送した入力」の S4-1 の項目）、[CONTENT_DIRECTORY_BUILD_FACTS.md](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)「Windows の Player での上書きの登録（6000.6.9f1）」（test からの Player の build と Project Auditor の事実）
- Phase A snapshot の取得案内・状態: 該当なし（機械向けの期待値・照合 script は無い。§5。レビューの入力は本書と program だけ）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: 発見 C（一次レビュー。動作確認は `対象なし` なので Phase B の次は発見 C）。
  - 次の作業: 実装 diff（implementation base と implementation head の間）を §3 の責務マップ・§2 の条件・§4 の禁止と照らす構造レビューと、§5 の判定必須テストのうち発見 C で走らせる範囲の実行。Player の build と起動を伴う (2)〜(10) は判定 C の実行担当が凍結済みのコマンドで行う（§5。AC6 は owner が押す）。Phase B の実装の要点・HANDOFF との差・未実行は §6。
  - 開始時の照合: 起動指示で渡された SHA と自分の HEAD が一致し、作業ツリーが clean で、ブランチが `claude/playable-content-player` であること。implementation head 欄の commit と HEAD の差分（`git diff --stat <implementation head> HEAD`）が `docs/handoff/` の file だけであること（他があれば停止）。PR #211 の状態ブロックの `引き継ぐ commit` が渡された SHA と HEAD に一致すること。食い違えば引き継がずに止まる。task は開始済み（`pwsh tools/workflow-task.ps1 status -Task s4-1-playable-content-player`）。
  - 読む範囲: 既定（§0〜§8）。
  - 引き継ぐ commit: この票を含む commit（Phase B の出口の commit の後に、進行役が本票へ Phase C の担当の系列と AC6 の実施時期を足した記録 commit。SHA は起動指示と PR #211 の状態ブロックで渡す）。次の記録 commit を切る担当は、その欄に渡された SHA を書く（句を引き継がない）。
  - 必要な権限: Unity の batch テストと Player の build・起動（窓を出せる利用者の desktop の session）。push は公開点の規則に従う（C と C' の後）。
  - 停止規則: §4 の「Phase B から Phase A へ差し戻す条件」と §2.4 (d)。新しい事実は分類してから扱う。Phase C に OpenAI の系列を使わない（C' に予約。§5）。
  - 引き渡さないもの: なし。
  - Phase C の担当の系列（2026-11-23、owner の指示）: 発見 C・B の修正・判定 C（実行担当と判断担当）は Phase B と同じ Anthropic の系列（Claude Opus）が担当する。Phase C に OpenAI の系列を使えない（C' に予約）ため、発見 C と判定 C の担当は §7 に `独立性制約あり` と書き、理由（B と同じ系列）と残存リスク（同じ系列の見落としが重なりうる。C' の OpenAI の系列で補う）を記録する。
  - AC6 の実施時期（2026-11-23、owner の指示）: owner が PC の前に戻るまで (10) `T.LaunchPlayableHuman` を起動しない。判定 C の実行担当は (10) 以外を先に進め、(10) は進行役が owner の在席を確かめてから起動する。

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
  - 起動の段の時間の上限（起動が終わらない hang を失敗にすること。今の動作確認の Player にも無い）。
  - build を CLI・CI から呼ぶ新しい入口（判定の test の `[Explicit]` の入口は検証用で、利用者の入口ではない）。
  - InGame の world の中身の遊び心地、性能、streaming の所要。
  - 描画の細部の正しさ（lighting・VFX・post process の見た目）。AC6 (5) が見るのは、Title と InGame の画面が出て真っ黒・マゼンタでないことだけである。
  - 製品の起動の設定を Addressables から読む経路（Player の構成に入らない Player。S4-0 の J1 の失敗の形）。
- 現況（base `9de34cc2`。凍結の base `32d3e043` との差は §0 の implementation base の行）:
  - **owner が受けた入力（program §8「S4 の A1 への入力（2026-11-23）」の (1)）:** 起動が動作確認（`AbstractApplicationInitializer.RunPlayerContentSmokeAsync`: 初回 Scene の load → fixture → 初回 Scene の unload → directory の close → 完了通知）に入るのは、directory の設定があり、初回 Scene が空でなく、`UseRequiredPlayerFileConfiguration` が真の 3 つが揃う時である。SampleGame の BS4 の Player は 3 つとも満たし、完了通知の override（`AppInitializer.OnPlayerContentShutdownCompletedAsync`）が `Application.Quit` で終える。初回 Scene を置く Player には、content を載せたまま操作を続ける構成が無い。
  - **program §8 の S4-1 の行:** 問いは「content directory から起動して、動作確認をして終了するのではなく操作を続けられる Player の構成を、動作確認の Player（BS4）の receipt と exit code の契約、protected config、fail-closed を弱めずに持てるか。開発用の Player の構成（backend、stripping、development build）をどうするか」。gate は「遊べる Player で初回 Scene から操作を続けられる。動作確認の Player の契約が変わらない。Player の実機の非退行（S3-1 の配線と、S3-2 の session・gate の層の化を含めて、6000.6.9f1 で確かめる）」。S4-2 の行は、S4-1 が決めた開発用の Player の構成が S4-0 と違う時に S4-0 の harness で測り直す、と書く。
  - **S4-0 から移送した入力（S4-1 分）:** Addressables の data の無い IL2CPP・High の Player で、製品の起動の設定の読み込み（`JsonFileConfigProvider` → `AddressableBackend.LoadAssetSync`）が `ResourceManager+CompletedOperation<TextAsset>` の既定の constructor の strip で `MissingMethodException` になった。
  - **Player の構成の latch（`SampleGame/DependOnAll/AppInitializer.cs:66,134-136`）:** `BeforeSceneLoad` の冒頭で、Editor でなく active Scene の名前が `UICommon` の時に `_useBs4PlayerMode` を真にし、`UseRequiredPlayerFileConfiguration` がそれを返す。環境変数や command line では有効にならない（04 章 §4.7）。真の時、`BuildConfig`（`AbstractApplicationInitializer.cs:907-932`）は `StreamingAssets/bs4-runtime.json` を `RequiredJsonFileConfigProvider` で必須に読み（Addressables を使わない）、その値を `PlayerBakedContentConfiguration` に固定し、環境変数（`SAMPLEGAME_`、`__` を `:` に変える）と command line（`-` で始まる引数）をその上に重ねる。key は大小文字を区別せず、`.` と `:` を同じに扱う。`BuildConfig` は `ReadContentDirectoryConfiguration` より先に走る。
  - **protected config（`OneStarMaker/Scripts/Runtime/BuildContent/PlayerContentConfiguration.cs`、`AbstractApplicationInitializer.ReadContentDirectoryConfiguration`）:** `ReadContentDirectoryConfiguration` は、上書きの key を `ContentOverlayStartup.Decide` で見た後、Player の構成なら `content:runtimeMode` を baked と比べて違えば `InvalidConfiguration`（message `Protected Player runtime mode was overridden.`。addressables への override で検査を迂回させない）、installed の pair の片方だけも拒む。その後 `PlayerContentConfiguration.Resolve` が baked の 9 つの key（`schemaVersion`・`runtimeMode`・`buildIdentity`・`contentSet`・`target`・`representation`・`firstScene`・`relativeDirectory`・`probeToken`）の生の値（key が無ければ `""`）を、重ねた後の `GetString("content:X","")` と ordinal に比べ、1 つでも違えば `InvalidConfiguration`（message `Protected Player content configuration was overridden.`）。installed の pair があれば `InstalledRevisionVerifier.Verify(root, digest, baked.ContentSet, baked.BuildIdentity, …)` で照合する。照合は receipt の revision が baked の build identity と同じことを求める（`InstalledRevisionVerifier.cs:78-82`）ので、使えるのは Player と同じ build identity の revision だけである。pair が無ければ exe の横の `relativeDirectory` を使う。これらは全て `BeforeSceneLoad` の中で起きる。
  - **起動の段（`AbstractApplicationInitializer.InitializeAfterSceneLoad`）:** Player の構成では、UICommon（生成した bootstrap Scene の借用）→ ready の待ち → `LoadPlayerSceneResourceMap`（`Resources` の graph metadata）→ framework の service の開始 → scene factory → directory の登録（`register-content-directory`）→ `SceneDirector` の生成（要求時解決は配線しない。`:400-406`）→ `RegisterAlreadyLoadedScenes` → 末尾の分岐（`:465-469`）。末尾の分岐は「初回 Scene が空でなく `!RunsPlayerContentSmoke(UseRequired…)` なら `LoadEditorFirstSceneAsync`、そうでなく初回 Scene が空でなければ `RunPlayerContentSmokeAsync`」の 2 段の `if` で、Editor の directory Play は前者（初回 Scene を load したまま Play を続け、1 行の log を出す。`:499-508`）、Player は後者に入る。`RunsPlayerContentSmoke` と `ShouldNotifyPlayerContentFailure` はどちらも引数をそのまま返す internal static（`:1234-1239`）で、`PlayerContentConfigurationTests` が表で固定している。
  - **失敗の経路:** `BeforeSceneLoad` の例外は log に出して `ReleaseAll` で回収し（`:193-202`。`ReleaseAll` は `_bakedPlayerContent` も消す）、`AfterSceneLoad` は Error を 1 行出して、Player の構成なら `OnPlayerContentStartupFailedAsync("before-scene-load", …)` を同期で待つ（`:210-227`）。`AfterSceneLoad` の例外は `CleanupAfterStartupFailureAsync`（Player の構成なら初回 Scene の unload（`:657`、条件は `RunsPlayerContentSmoke`）、directory の close、派生への通知）の後に `ReleaseAll`。派生への通知は **1 つの try** の中で `OnAfterSceneLoadInitializationFailed`（SampleGame は Camera・RenderEnvironment・profiler の解放）→ `OnPlayerContentStartupFailedAsync` の順に呼ぶので、前者が投げると後者は呼ばれず、catch が log に出すだけで終わる（`:689-704`）。Player の構成では `OperationCanceledException` も失敗として扱う（`:473`）。SampleGame の失敗の override（`AppInitializer.cs:157-165`）は `PlayerContentSmoke.WriteReceipt(false, stage, …)` の後に `Application.Quit(1)` を呼ぶ（receipt の書き込みが投げると Quit に届かない）。起動の段に時間の上限は無く、どこかの await が終わらなければ Player は失敗も成功もしないまま動き続ける。
  - **receipt の契約（`SampleGame/DependOnAll/PlayerContentSmoke.cs:144-158`）:** exe の横の `bs4-smoke-receipt.json` に schema v2（`schemaVersion`・`succeeded`・`stage`・`completedStages`・`error`）を `.tmp` から置き換え（無ければ `File.Move`）で書く。成功は stage `completed` と 10 段（`ui-common-ready` … `content-directory-closed`）で exit 0、失敗は到達した stage と完了した段で exit 1。この file を読む code は repo に無い（読むのは人と、BS4 を起動する担当。repo の中に BS4 の Player を起動する code も無い。Delivery の窓は Editor の Verify Play に環境変数を渡すだけである。`ContentDeliveryPlayBridge.cs:181-204`）。
  - **game の log:** game の code の `ILogger`（ZLogger）は Player の `-logFile` には出ず、exe の横の `OneStarMakerLocal/Log/` の JSON の rolling file に出る（`AppLoggerFactory.cs:113-123,217-`。最低水準は Trace）。書き込みは非同期で、全てが file に出ている保証は factory の Dispose（quit の `ReleaseAll`）の後にしか無い。`TitleScene` は load で `Loaded.`、Start の後の遷移の失敗で Error を出す（`TitleScene.cs:44,91`）。Start で Home に移ると、`HomeScene.OnStabledImpl` は `OnStabledImpl HomeScene` を出した**後**に `SceneFlow.EnterInGame`（`OutGameScene` → `InGameSession` の `SwitchScene`）を待つ（`HomeScene.cs:35-41`）。`InGameSession` は安定で `[InGameSession] initial season ensure started` を出して初期の Season（Spring、Cell_0_4）の ensure を fire-and-forget し、失敗は `Season branch ensure failed` の Error（`SessionSeasonController.cs:145-165`）。`PlayerScene` は world ready の後に `Player ready at Cell stream spawn (…)` を出す（`PlayerScene.cs:166-176`。文言は既存の判定の照合が探すので変えない、と code の注記にある）。
  - **終了時の解放:** `Application.quitting` → `ReleaseAll`。directory が登録されたままなら `AssetManagement.CompleteContentDirectoryPlayStop` → `ContentDirectorySession.CompletePlayStop`（受付を止め、cache を落とし、pending の native の unload を待たずに解除する。quit 中に PlayerLoop を待つと同じ main thread で死鎖するため）。動作確認の Player は quit の前に directory を close しているので、Player でこの経路を通ったことは無い（Editor の Play の停止では通る）。
  - **Player の build（`SampleGame/DependOnAll/Editor/Build/`）:** `SampleGamePlayerBuildCoordinator.Build()`（menu `Tools/OSM/Content/Build BS4 Spring Player` だけが呼ぶ）が、`bs4-spring-full` の宣言（`spring-full` と同じ選択に probe の prefab 1 件を合成）で content を Full で build し、`artifactsRoot`（固定で `<repo>/artifacts/bs4`）の下の `transport/<identity>` へ publish し、`work/<identity>` に Player を build し、`PlayerBuildProjectMutation` の中で graph metadata と bootstrap Scene を `Assets/Resources/BS4/<identity>/` に写し、`bs4-runtime.json` を `Assets/StreamingAssets/` に書き、scripting backend を IL2CPP・managed stripping を High・Addressables の「Player と一緒に build する」を off にして `BuildPipeline.BuildPlayer`（StandaloneWindows64、`DetailedBuildReport`、`previousBuildReportDirectories` に content の build report）を呼び、終わりに全て戻す。build report の packed assets で content の root の重複を検査し、`PlayerBuildPublisher` が `players/<identity>/`（exe、`content/`、`build-receipt.json`）へ置く。`artifacts/` は git の ignore の対象である。development build にはしない。`build-receipt.json` の `backend`・`stripping` は定数を書く（実際の設定を読まない。`:66`）。初回 Scene は `SampleGameBuildPurposes.FirstSceneOf`（今の目的は全て `Title`）。`RuntimeJson` の既存のテストは `Bs4FixtureComposerTests.cs:66-79`。
  - **build receipt を読む側:** Artifacts の `build collect`（`tools/Artifacts/BuildContract.psm1:142-160`）は `build-receipt.json` の 7 つの欄を名前で読み（他の欄は拒まない）、保存系列を target と `backend`・`stripping` の組（`IL2CPP`・`High` → `il2cpp-high`）で決める。contentSet は transport manifest から読む。
  - **Player の証拠の版:** BS4 の Player の build と起動の証拠は全て 6000.6.0f1 のものである。S3-1（要求時解決の配線。Player では配線しない分岐を足した）と S3-2（session の層の列と gate）は Player の実機で確かめていない。S4-0 は 6000.6.9f1 で、EditMode の test から IL2CPP の Player を `run-tests.ps1` の batch で build できること（約 6 分）、その後に Project Auditor の post-build 監査が同じ Unity で走り `step.json` を `failed` にすること（XML は `Passed`）、`-batchmode` の Player 18 回が全て exit 0 で終わったことを測った。窓を出した起動は測っていない。`run-tests.ps1` は test の時間の上限を持たない（NUnit の `Timeout` が上限になる）。
  - **初回 Scene の先:** `Title` の Scene（`SampleGame/OutGame/Title/`）は UI Toolkit の Start の button の click で `SceneController.SwitchScene` を呼び、`Home` へ移る（`TitleScene.cs:69-84`）。Home はそのまま InGame へ進む（上の「game の log」）。Player で `Title` より先へ進んだ記録は無い。`SampleGame/DependOnAll/link.xml` は `UnityEngine.UIElementsModule` の `UnityEngine.UIElements.*` を全て残す。repo の code は `DEVELOPMENT_BUILD` と `Debug.isDebugBuild` に依らない。
  - **Addressables の封じ:** directory mode の起動は `SealAddressables` で Addressables の取得経路を封じる。動作確認の Player は設定を Addressables から読まないので、封じる前にも使わない。Editor の directory Play（経路 Built）は同じ封じの下で遊べている。
  - Unity 上では何も実行していない。
- UC ごとの利用者の入力:

| UC | 利用者が選ぶ・入れるもの | 機械が決めるもの | 誰がいつ決めるか | 目的からの正当化 |
|---|---|---|---|---|
| UC1 遊べる Player を build する | menu `Tools/OSM/Content/Build Spring Playable Player` を選ぶこと（1 つ） | 宣言（`spring-full`）、初回 Scene（`FirstSceneOf`）、表現、Player の種類 `playable`、backend IL2CPP・stripping High・development build でない、出力先 `artifacts/playable/`（transport・work・players） | 選ぶのは実行時の操作（開発者）。他は開発時の宣言（coordinator の定数と宣言の表）と自動導出 | 目的の「製品の build 経路で build した Player」。種類を menu の名前で分けるのは、動作確認の Player を build する menu を今のまま残すため（目的の「契約は今のまま」）。宣言や構成を選ばせないのは、目的が構成を 1 つに決めることを求めるため |
| UC2 遊べる Player を起動して遊ぶ | exe を起動すること、窓の中の操作（Title の Start、InGame の移動など）、窓を閉じること | 読む content（同梱の `content/`）、初回 Scene、protected config の値 | 開発者の実行時の操作。他は build の時に baked | 目的の「起動し、初回 Scene から先へ操作を続けて遊べる」。content や初回 Scene を起動の引数で選ばせないのは protected config を弱めないため |
| UC3 検証済みの install した revision から起動する（任意） | 環境変数か command line の `content:installedRevisionPath` と `content:manifestSha256` | 照合（`InstalledRevisionVerifier`、baked の contentSet と build identity）。使えるのはその Player と同じ build identity の revision だけ | 開発者の実行時の操作 | 既にある動作確認の Player の口をそのまま使う（新しい入力ではない）。目的の「検証済みの install した revision から起動」 |

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 製品の build 経路で build した Windows の Player に、動作確認をして終わるのではなく、content directory から起動して初回 Scene から先へ操作を続けられる種類を、動作確認の Player の receipt と exit code の契約、protected config、fail-closed を弱めずに足せるか。その開発用の Player の構成を何にするか。
- 判定定義: **GO** は §2.1 の不変条件と成立条件を全て満たした時。**NO-GO** はどれかを満たさない時。スパイクではないので CONDITIONAL ACCEPT は無い。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし。
- 決定記録: なし。

### 2.1 進める最低条件

**決めること（設計）:**

- **種類:** Player の種類を build の時に baked config の新しい protected key `content:playerKind`（値は `smoke` か `playable`）で決める。key は必須で、無ければ `InvalidConfiguration`（fail-closed）。`PlayerBakedContentConfiguration` は生の値（`PlayerKindRaw`）と、読んだ値（`PlayerKind`: `smoke` → `Smoke`、`playable` → `Playable`、無い・それ以外 → `InvalidConfiguration`）の両方を持つ。必須にしてよい理由: build identity は build ごとに新しく（時刻と GUID）、`bs4-runtime.json` は coordinator が同じ build の exe と対で書くので、この変更を含む exe が key の無い baked を読む場面は無い（古い baked は古い exe としか組にならない）。語の注記: `playerKind`・`PlayerContentKind` は Player の 1 回の起動の種類を指し、`ContentDirectorySession`（directory の登録の寿命）や `InGameSession`（Scene）とは別の語である（code のコメントにも書く）。
- **protected の比較:** `Resolve` は他の 9 つと同じく、baked の**生の値**と重ねた後の `GetString("content:playerKind","")` を ordinal に比べる（enum に直した値とは比べない。直した値と比べると、大小違いの override などで生の値と違う値が通りうる）。
- **末尾の分岐:** 今の 2 段の `if` を、internal static の 3 値の判定 `PlayerFirstSceneRoute RouteFirstScene(bool isPlayerConfiguration, PlayerContentKind kind)`（`Editor`・`Smoke`・`Playable`）に置き換える。`Editor` は `LoadEditorFirstSceneAsync`、`Smoke` は今の `RunPlayerContentSmokeAsync`、`Playable` は新しい `LoadPlayerFirstSceneAsync`（stage `load-player-first-scene:<Scene 名>`、`SceneDirector.AddScene` の後に `[AppInit] Player content first scene: scene=… identity=… kind=playable stable=…` の 1 行を出し、load したまま起動を終える）。初回 Scene が空の時は今と同じく何もしない。分岐が読む種類は baked の値で、重ねた後の値ではない。Player の構成でない起動（Editor）は baked が無く、`Editor` になる。遊べる Player には成功の hook を足さない（遊べる Player は成功を receipt に書かず、hook を呼ぶ者がいない）。
- **receipt:** 遊べる Player は receipt を書かない。起動の成功は Player の log の 1 行、失敗は Player の log（stage と例外）と exit 1 で示す。
- **失敗の通知:** (a) `CleanupAfterStartupFailureAsync` の派生への通知を、`OnAfterSceneLoadInitializationFailed` と `OnPlayerContentStartupFailedAsync` の**別々の** try にする（前者が投げても後者を必ず呼ぶ。後者が投げたら log に出す）。この順の保証を internal static `RunStartupFailureHooksAsync(Action derivedCleanup, Func<UniTask>? notifyPlayerFailure, Action<string, Exception> logFailure)` に置き、単体テストで固定する。(b) SampleGame の失敗の override は、latch した種類が `Playable` なら receipt を書かず、そうでなければ今と同じ receipt を書き、どちらでも `Application.Quit(1)` を `finally` で呼ぶ。(c) latch: `BuildConfig` が `bs4-runtime.json` を読んだ直後（`PlayerBakedContentConfiguration` の検証より前）に、生の `content:playerKind` が ordinal に `playable` なら field `_latchedPlayerKind` を `Playable` にする。field は `BootstrapBeforeSceneLoad` の冒頭で `Smoke` に戻し、`ReleaseAll` では消さない（失敗の通知は `ReleaseAll` の後に呼ばれ、baked は既に消えているため）。派生には protected `bool IsPlayablePlayerKind` で見せる（internal enum を公開面に出さない）。`bs4-runtime.json` が無い・壊れていて読めなかった時と、生の値が `playable` 以外（key の欠け、`Playable` の大小違いなどの不正な値を含む）の時は `Smoke` のままで、その Player は失敗の receipt を書く（harvest に書く）。
- **失敗時の初回 Scene の unload:** 条件は今の意味（Player の構成かどうか）のままにし、両方の種類で unload する。internal static `UnloadsFirstSceneOnStartupFailure(bool isPlayerConfiguration)` にして表で固定する。
- **構成:** 開発用の Player の構成は、動作確認の Player と S4-0 が測った Player と同じ IL2CPP・managed stripping High・development build でない、にする（理由は §2.4 の (c)）。
- **出力先:** 遊べる Player は coordinator の `artifactsRoot` を `<repo>/artifacts/playable` にし、transport・work・players を全てその下に置く（動作確認の Player の `artifacts/bs4` と混ぜない）。

**不変条件**（起きてはいけないこと）:

- **I1 動作確認の Player の契約は変わらない。** 今の menu で build した動作確認の Player は、今と同じ receipt（file 名、schema v2 の欄、成功の stage `completed` と 10 段の順、失敗の stage と完了した段）と exit code（成功 0、失敗 1）で終わり、成功したら自分で終了する。主張を崩しうる既存の経路: (a) 末尾の分岐を 3 値にする所（`Smoke` を `Playable` や `Editor` と取り違えて smoke の尾を飛ばす）。(b) baked config の読み（`PlayerBakedContentConfiguration` の ctor）に key を足す所。key を必須にする検査（動作確認の Player の baked にも `smoke` を書くので、書き忘れると動作確認の Player が起動できない）。(c) protected の比較（`PlayerContentConfiguration.Resolve`）に key を足す所で、今の 9 つの比較を変えること、生の値でなく直した値と比べること。(d) coordinator の `Build` を種類で分ける所で、動作確認の Player の宣言・probe・`RuntimeJson`（`playerKind` の追加を除く）・出力先・build の設定を変えること。(e) SampleGame の失敗の override を種類で分ける所（`Smoke` で receipt を書かなくなる誤り）。(f) `CleanupAfterStartupFailureAsync` の Player の分岐（初回 Scene の unload の条件、派生への通知の順）。(g) S3-1・S3-2 が足した、Player では通らない分岐と session の層（Player の実機では未確認）。(h) B 適応で `link.xml` に型を足すと、動作確認の Player の build にも効くこと（型を残すだけで、契約の観測は変えない）。(i) 失敗の override の `Quit` を `finally` にすること（receipt を書ける時の観測は変わらない。§2.5 の (4)）。探索した境界: `AbstractApplicationInitializer` の Player の構成に関わる分岐の全て、`AppInitializer` の Player の override、`PlayerContentSmoke`、coordinator・mutation・adapter・publisher、Artifacts の `BuildContract.psm1` の receipt の読み、`ContentDeliveryPlayBridge`。未確認: repo の外で BS4 の Player を起動して receipt を読む手順（repo に起動の code は無い）。
- **I2 protected config を重ねる値で迂回できない。** どちらの種類の Player でも、環境変数と command line で baked の値（新しい `content:playerKind` を含む）と違う値を置くと、起動は `InvalidConfiguration` で失敗し、別の種類・別の content・Addressables で動き続けない。主張を崩しうる経路: (a) 重ねる順（JSON → 環境変数 → command line）で後の値が勝つこと。(b) `ReadContentDirectoryConfiguration` の `content:runtimeMode` の早い比較の後、`addressables` で `return null` する分岐（Player では比較が先にあるので今は塞がっている）。(c) 種類を決める所が、`Resolve` の比較より前に重ねた後の値を読むこと（分岐と latch は baked を読むので塞がる）。(d) `content:playerKind` の比較を直した値で行うこと（上の「protected の比較」）。(e) installed の pair を置いた時に `Resolve` が baked の contentSet と build identity で照合する所。(f) `ContentOverlayStartup.Decide` が Player の構成で上書きの key を拒むこと。探索した境界: config の 3 つの provider、`ReadContentDirectoryConfiguration`、`PlayerContentConfiguration`、`ContentOverlayStartup`。A2 の反証で破る順序は見つからなかった（§5.1）。
- **I3 起動に失敗した遊べる Player は動き続けない。** 遊べる Player の起動が失敗した時（baked config の欠け・不正な値、protected の不一致、content directory が無い・照合が合わない、directory の登録・初回 Scene の load の失敗）、Player は log に失敗の stage と例外を残して exit 1 で終わり、content の無い状態や Addressables・別の Scene で操作を受け付け続けない。「失敗した時」は起動の段が例外を投げた時で、起動の段が終わらない（hang）時は含まない（§1 の対象外）。主張を崩しうる経路: (a) `BeforeSceneLoad` の失敗後の通知が `ShouldNotifyPlayerContentFailure`（Player の構成）にだけ依ること。(b) `AfterSceneLoad` の `catch (OperationCanceledException) when (!UseRequiredPlayerFileConfiguration)`（起動の途中で窓を閉じると失敗として exit 1 になる。害は小さく、そのまま残す）。(c) `CleanupAfterStartupFailureAsync` の派生への通知の前の `OnAfterSceneLoadInitializationFailed` が投げて、失敗の通知（とその中の `Quit(1)`）が呼ばれないこと（外部計画レビューの P1。上の「失敗の通知」の (a) で塞ぐ）。(d) 失敗の override の中の receipt の書き込みが投げて `Quit(1)` に届かないこと（A2 の B-1。(b) で塞ぐ）。(e) `CleanupAfterStartupFailureAsync` が遊べる Player の初回 Scene を unload すること（上の決定）。(f) 起動を終えた後（初回 Scene が安定した後）の game の code の失敗は起動の失敗ではない（この不変条件の外。E2 が別に見る）。探索した境界: `BootstrapAfterSceneLoad`、`InitializeAfterSceneLoad` の try/catch、`CleanupAfterStartupFailureAsync`、SampleGame の失敗の override と `OnAfterSceneLoadInitializationFailed`。未確認: 失敗の後に `Application.Quit(1)` が Player の窓を閉じ切るまでの間の入力（Quit の後の frame）。
- **I4 遊べる Player は成功しても、起動の段として動作確認の尾を走らせない。** 遊べる Player が初回 Scene を安定させた後、利用者の操作が無い間に、起動の段として初回 Scene の unload、fixture の load、directory の close、`Application.Quit` は起きない（利用者の操作による Scene の遷移は含まない）。主張を崩しうる経路: (a) 末尾の分岐。(b) SampleGame の `OnPlayerContentShutdownCompletedAsync`（`Application.Quit(0)`）が遊べる Player の経路から呼ばれること。(c) `RegisterAlreadyLoadedScenes` と初回 Scene の load の順。探索した境界: `InitializeAfterSceneLoad` の末尾、`AppInitializer` の override。

**成立条件**（起きなければならないこと）:

- **E1** 新しい menu で build した遊べる Player を同梱の content から起動すると、初回 Scene（`Title`）が安定し、Player は自分では終わらずに動き続ける。起動の到達を外から読める印（Player の log の 1 行）を残す。
- **E2** 遊べる Player の窓で、Title の Start を押すと Home を経て InGame へ進み、Player が操作を受け付ける状態（`Player ready at Cell stream spawn`）になり、Title と InGame の画面が出る。
- **E3** 遊べる Player の窓を閉じると、Player は exit 0 で終わる（directory を登録したままの quit の解放が、終わらない・exit が 0 でない、にならない）。Title の上と、InGame の上の両方で。
- **E4** 動作確認の Player の実機の非退行を 6000.6.9f1 で確かめる（今の menu の Player が成功の receipt と exit 0 で終わる。S3-1・S3-2 の後の起動を Player で通す）。
- **E5** 遊べる Player の構成（backend、stripping、development build）が §2.1 の決定どおりであることを、build の出力と build report で確かめる。

### 2.2 受け入れ条件

全て §5 の判定必須テストか検証経路で閉じる。Player の起動を伴う条件は、Phase C の実行担当が §5 のコマンドで取る。時間の制限を超えて終わらない起動は不合格で、record に `timeout` と書き、process を kill する。game の log は Player の process が終わった後に読む（非同期の書き込みが Dispose で全て出るため）。kill した起動の game の log の行は「観測なし」と扱い、その行を要る AC は不合格にする。遊べる Player の log は、Framework が quit の始めに出す `[AppInit] Application quitting.` の行（§3）を境に、**閉じる前**（起動から境まで）と**閉じた後**（境から exit まで）に分けて読む。game の log は、test がその行を最初に見た時刻（record の `quitObservedUtc`）と各行の時刻で同じように分ける。strip の検出語は `MissingMethodException`、`stripped`、`is missing`（`The referenced script … is missing!`）、`Could not produce class` とし、Phase C の最初の run で実際の log の文言と照らして §7 に書く（語を足すのは凍結条件の読み方の確定で、決定記録に残す）。

- **AC1（I1・E4）動作確認の Player の成功:** 今の menu と同じ coordinator の経路（種類 `smoke`）で build した Player を `-batchmode` で起動すると、300 秒以内に exit 0 で終わり、`bs4-smoke-receipt.json` は `schemaVersion` 2、`succeeded` 真、`stage` `completed`、`completedStages` は 10 段を今の順に全て持つ。その Player の transport manifest の `unityVersion` が `6000.6.9f1` である（版が違う run は AC1 の合格に数えず、追加の証拠として記録する）。record に、この起動の game の log の Error 以上の行を全て残す（AC4・AC6 の基準。合否には使わない）。合格例: 10 段が全てある。不合格例: `first-scene-stable` の後に `fixture-*` が無い、exit 0 だが receipt が無い、`unityVersion` が別の版。
- **AC2（I1・I2）動作確認の Player の protected:** 同じ Player を `-batchmode` で、環境変数 `SAMPLEGAME_CONTENT__PLAYERKIND=playable` を置いて起動すると、60 秒以内に exit 1 で終わり、receipt は `succeeded` 偽、`stage` `before-scene-load`、`completedStages` は空、`error` に `Protected Player` を含む。
- **AC3（I1・I2）baked config:** `RuntimeJson` が書く key と値の全集合を、2 つの種類それぞれ表で固定する（単体テスト）。動作確認の Player は、今の 12 の値（`content` の `schemaVersion` 1・`runtimeMode` `directory`・`buildIdentity`・`contentSet` `bs4-spring-full`・`target` `StandaloneWindows64-Player`・`relativeDirectory` `content`・`firstScene` `Title`・`representation`・`probeToken`、`debugSocket:enabled` 偽、`telemetry:profiler:enabled` 真、`world:cellCompanionSet`）に `content:playerKind` `smoke` を足した 13。遊べる Player は同じ 13 の key で、`contentSet` `spring-full`、`probeToken` `""`、`playerKind` `playable`、`firstScene` `Title`、`debugSocket:enabled` 偽、`world:cellCompanionSet` は `CellCompanionSetOf(spring-full)`、他は動作確認の Player と同じ規則の値。coordinator の種類ごとの `artifactsRoot`（`artifacts/bs4`・`artifacts/playable`）も同じテストで固定する。
- **AC4（E1・I4）遊べる Player の起動:** 新しい coordinator の経路（種類 `playable`）で build した Player を窓で（`-batchmode` なし、`-screen-fullscreen 0 -screen-width 1280 -screen-height 720`）起動すると、120 秒以内に Player の log に `[AppInit] Player content first scene: scene=Title … kind=playable stable=True` の 1 行が出る。その行の後 60 秒の間 Player は終わらない。閉じる前の Player の log に `run-player-content-smoke`、`unload-player-first-scene`、`close-player-content-directory`、`Editor content directory first scene`、`AfterSceneLoad failed`、`Exception`、strip の検出語が無く、exe の横に `bs4-smoke-receipt.json` が無い。AC5 で終わった後、閉じる前の game の log に `TitleScene` の `Loaded.` があり、Error 以上の行が無い（AC1 の基準と同じ category・message の行は、既存として record に書いて owner へ返し、この条件の違反に数えない）。不合格例: 60 秒の間に exit した、receipt がある、基準に無い Error がある。
- **AC5（E3）遊べる Player の Title での終わり:** AC4 の Player の窓に閉じる要求（`Process.Refresh()` の後に `Process.CloseMainWindow()`）を送ると、120 秒以内に exit 0 で終わる。閉じた後の log は record に残すだけで、合否は exit code と時間で読む。
- **AC6（E2・E3）初回 Scene の先と InGame での終わり:** AC4 と同じ build の遊べる Player を AC4 と同じ窓の引数で起動し、owner が Title の Start を 1 回押し、InGame の画面が出たら窓の × で閉じる。合格: (1) Player の log の `[AppInit] Application quitting.` を test が見た時刻から 120 秒以内に exit 0 で終わる。(2) 閉じる前の game の log に `OnStabledImpl HomeScene`、`[InGameSession] initial season ensure started`、`Player ready at Cell stream spawn` がこの順にある。(3) 閉じる前の game の log に Error 以上の行（`Season branch ensure failed` を含む）が無い（AC1 の基準の扱いは AC4 と同じ）。(4) 閉じる前の Player の log に `AfterSceneLoad failed`、`Exception`、strip の検出語が無い。(5) owner の観察記録に、Title と InGame の画面が出て、真っ黒・マゼンタ（shader・material の欠け）でなかったことと、両方の screenshot がある。(1)〜(4) は AI が record の log で判定する。(5) は人間の観察記録として受理する（監査担当が画像を再評価できることまでは求めないが、screenshot は evidence に入れる）。閉じた後の log は record に残すだけにする。不合格例: Home の行はあるが `Player ready` が無い、exit code が 0 でない、quitting の行から 120 秒で終わらない、画面がマゼンタ。
- **AC7（I2・I3）遊べる Player の protected:** 遊べる Player を AC4 と同じ窓の引数で、(a) 環境変数 `SAMPLEGAME_CONTENT__PLAYERKIND=smoke`、(b) `SAMPLEGAME_CONTENT__RUNTIMEMODE=addressables` を置いて、それぞれ起動すると、どちらも 60 秒以内に exit 1 で終わり、Player の log に `Protected Player` を含む行があり、`Player content first scene` の行が無く、receipt が無い。
- **AC8（I3）遊べる Player の content の失敗:** 遊べる Player を AC4 と同じ窓の引数で、存在しない path の `SAMPLEGAME_CONTENT__INSTALLEDREVISIONPATH` と 64 桁の 16 進の `SAMPLEGAME_CONTENT__MANIFESTSHA256` を置いて起動すると、60 秒以内に exit 1 で終わり、Player の log に installed の revision の照合で拒んだことを示す `ContentDeliveryException` の例外の行と、`BeforeSceneLoad が失敗したため AfterSceneLoad をスキップします` の行があり、`Player content first scene` の行が無く、receipt が無い。record に例外の行の全文を残す（failure code の文言は固定しない）。
- **AC9（I1・I2・I3・I4）単体テスト:** 次を表の単体テストで固定する。(1) `content:playerKind` の読み: `smoke` → `Smoke`、`playable` → `Playable`、無い・`""`・`Playable`・`x` → `InvalidConfiguration`。(2) `Resolve` の比較: baked `smoke`・override なし → 通る、baked `smoke`・override `playable` → 拒む、baked `playable`・override `smoke` → 拒む、baked `playable`・override `Playable`（大小違い）→ 拒む。今の 9 つの key の override の拒みは変わらない。(3) `RouteFirstScene`: (Editor, —) → `Editor`、(Player, `Smoke`) → `Smoke`、(Player, `Playable`) → `Playable`。(4) `UnloadsFirstSceneOnStartupFailure`: Player → 真、Editor → 偽。(5) `RunStartupFailureHooksAsync`: 派生の後始末が投げても失敗の通知は 1 回呼ばれ、両方の例外が log に出る。失敗の通知が投げたら log に出る。失敗の通知が null（Editor）なら呼ばない。(6) latch: 生 `playable` → `Playable`、生の key 無し・`smoke`・`Playable` → `Smoke`（latch を決める internal static の判定）。
- **AC10（E5）遊べる Player の構成:** 遊べる Player の build の record に、出力に `GameAssembly.dll` があり `SampleGame_Data/Managed/` が無いこと（IL2CPP）、build report の options に `Development` が無いこと、build の間の managed stripping の水準が High だったこと（mutation の中で読んだ値）が残る。
- **AC11（I1・I2・I3）回帰:** 判定必須の全 EditMode 回帰（§5）。

### 2.3 ここでは答えない問いと所有する後続

- 上書きを Player に重ね、照合の失敗を理由つきで示すこと: **S4-2**（program §8）。遊べる Player の `playable` の経路が土台になる。**S4-2 への入力:** 今の installed の pair の照合は Player と同じ build identity の revision しか通さない（`InstalledRevisionVerifier.cs:78-82`）ので、別に build した base を遊べる Player に載せる形は、この照合のままでは成り立たない。
- Player での要求時解決の配線と record: **S4-2**（重ねるには session の層と要求時解決が要る。配線するかを S4-2 の A1 が決める）。
- Player の構成に入らない Player の起動で、設定の Addressables からの読みが strip で `MissingMethodException` になる形（S4-0 の J1）: **S4-2**（S4-0 の harness を測り直す時に同じ形に当たる。製品の起動の外の Player を扱うのは S4-2 の harness だけ）。
- 起動の段の時間の上限（hang を失敗にすること）: 所有する後続なし（**program §10 に未所有として足す**）。動作確認の Player にも無い。
- Mono、他の stripping の水準、development build、`-nographics` の Player: 所有する後続なし（program §10 に未所有として足す）。要る時に owner が行を置く。
- 遊べる Player を Artifacts の保存系列へ入れること（今の系列は target と backend・stripping だけで決まり、種類を区別しない）: [BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md](BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md) の入力。遊べる Player は `artifacts/playable/` に出し、`artifacts/bs4/` と混ぜない。
- 起動を終えた後の game の code の失敗を Player で網羅すること（`Player ready` の先の移動、streaming の Cell の出入り、VFX、Spring 以外の Season への操作）、性能: 所有する後続なし（program §10 に未所有として足す）。AC6 が見るのは Title → Home → InGame の `Player ready` までである。
- `PlayerBuildPublisher` の staging → final の移動に S5-0 の再試行を通すこと: 所有する後続なし（program §8 の「S5-0 から移送した入力」と同じ形の BuildSystem の入力。program §10 に未所有として足す）。
- Android・iOS の Player: **S4-5**。
- 遊べる Player の DebugSocket（DebugStudio の接続）: 遊べる Player の baked config は動作確認の Player と同じく `debugSocket:enabled` を既定で偽にする（protected ではないので環境変数で真にできる。保護は足さない）。DebugStudio からの操作を検証に使うことは、所有する後続なし（program §10 に未所有として足す）。

### 2.4 スライス分割の判断

- **契機:** 高リスクの初回実機スライスで独立して失敗しうる未知が複数ある（(u1) 6000.6.9f1 の動作確認の Player、(u2) Title の先の game の code の strip、(u3) directory を登録したままの quit、(u4) 窓を出した起動）。
- **判断: 分けない。** (a) (u1) は E4 で、S4-1 の行の gate そのものであり、S4-1 が足す分岐の非退行の証拠でもある（I1）。分けると、S4-1 の変更を入れた head の非退行をもう一度取る必要がある。(b) (u2)〜(u4) は同じ遊べる Player の build 1 つで取れ、片方の失敗がもう片方の観測を妨げない（AC4・AC5 と AC6 は別の起動）。(c) 失敗の写像を §4 の差し戻し条件に分けて書く（(u1) の失敗は既存の退行か環境の不備、(u2) の SampleGame の型の欠けは B 適応、(u3) は Phase A へ戻す）。(d) **分ける条件:** (u2) で SampleGame の型の欠けの B 適応が 3 件を超えるか、Framework・Unity の組み込みの型の欠けが出たら、Title の先の strip の修正を別のスライスに分けるかを owner に返す。
- **(c) 開発用の Player の構成を IL2CPP・High・development build でない、にする理由:** 動作確認の Player と S4-0 が測った Player と同じにすると、S4-2 の最初の gate の「S4-1 が決めた構成が S4-0 と違う時の測り直し」が要らなくなり、Artifacts の保存系列の値（`il2cpp-high`）の外に出ない。strip の失敗（J1 の形）は、配布する Player と同じ構成で見つける方が、配布の前に見つけられる（Mono にすると隠れる）。development build の利点は主に stack と profiler の接続で、repo の code はそれに依らないが、S4-0 が測っていない構成になり S4-2 の測り直しを持ち込む。

### 2.5 owner の決定（2026-11-23、外部計画レビューの 1 巡目の後）

- **(1) AC6 の Start を人間が押す:** owner が担当する（「人間やります」）。判定は AI が game の log で行う。入力を注入する検証支援は作らない。
- **(2) 遊べる Player は receipt を書かない:** 推奨のとおり。起動の成功は Player の log の 1 行、失敗は log と exit 1。`bs4-runtime.json`・`Resources/BS4/`・`.bs4-owner`・latch の名前など、`bs4` の名は改名しない（I1 のため。harvest に書く）。
- **(3) 遊べる Player の content を probe なしの `spring-full` にする:** 推奨のとおり。理由は、probe は動作確認だけが使う fixture で遊べる Player には要らないこと（A1 の「Delivery の install を使える」は誤りだった。A2 の M-1）。
- **(4) 失敗の override の Quit を `finally` にする（動作確認の Player にも効く）:** 推奨のとおり。receipt を書ける時の観測（file 名、schema、stage、exit code）は変わらず、receipt の書き込みが投げた時だけ「固まって終わらない」が「receipt なしの exit 1」に変わる。I1 の契約は変えないと読む。外部計画レビューの P1 を受けて、通知の前の派生の後始末が投げた時も通知を呼ぶ形を足した（§2.1）。
- **外部計画レビュー 2 巡目の後の追記（owner の「推奨でよい」の範囲で A3 が決めた）:** `content:playerKind` を必須にした（無ければ `InvalidConfiguration`）。key の名前を `content:playerSession` から `content:playerKind` に、enum を `PlayerContentKind`（Runtime）と `PlayerBuildKind`（Editor）に改めた（既存の session の語と紛れるため）。C' は OpenAI の系列に予約した（§5）。AC6 に owner の画面の観察と screenshot を足した（(5)）。

## 3. 責務マップ

| file | 責務（一文） | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 配置の理由 | 行数（今 → 予想） |
|---|---|---|---|---|---|---|---|---|
| `OneStarMaker/Scripts/Runtime/BuildContent/PlayerBakedContentConfiguration.cs` | baked config の Player の値を固定して持つ | 種類の key を足し、生の値 `PlayerKindRaw` と読んだ値 `PlayerKind`（internal enum `PlayerContentKind { Smoke, Playable }`）を持つ。latch を決める internal static（生の値が ordinal に `playable` か）もここに置く | initializer の `_bakedPlayerContent`（`BeforeSceneLoad` から `ReleaseAll` まで） | `AppConfig` | internal（enum も internal） | 単体（`AppConfig` を作って ctor を呼ぶ） | baked の値の読みと同じ変更理由。新しい file を作らない | 11 → 30 |
| `OneStarMaker/Scripts/Runtime/BuildContent/PlayerContentConfiguration.cs` | Player の content の指定を照合して正規化する | protected の比較に `content:playerKind` を足す（baked の生の値と重ねた後の `GetString` の ordinal 比較） | 呼び出しの間だけ | `AppConfig`、baked | internal | 単体（今の `PlayerContentConfigurationTests`） | protected の比較の正本 | 55 → 57 |
| `OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs` | 3 フェーズの起動の順序、hook、失敗境界 | (1) 末尾の 2 段の `if` を internal static `RouteFirstScene`（3 値、internal enum `PlayerFirstSceneRoute`）で置き換え、`Playable` の `LoadPlayerFirstSceneAsync` と log の 1 行を足す。`RunsPlayerContentSmoke` は `RouteFirstScene` に置き換えて消す（呼ぶ所は末尾の分岐と失敗の unload の 2 つで、後者は (3)）。(2) `CleanupAfterStartupFailureAsync` の派生への通知を internal static `RunStartupFailureHooksAsync` に出し、2 つの hook を別々の try で呼ぶ。(3) 失敗時の初回 Scene の unload の条件を internal static `UnloadsFirstSceneOnStartupFailure(bool)` にする（今の意味のまま）。(4) `BuildConfig` の中で latch を決め、field `_latchedPlayerKind`（`BootstrapBeforeSceneLoad` の冒頭で戻し、`ReleaseAll` では消さない）と protected `bool IsPlayablePlayerKind` を足す。(5) `OnApplicationQuitting` の始めに `Debug.Log("[AppInit] Application quitting.")` の 1 行を足す（Player の log で閉じる前と閉じた後を分ける境。Editor の Play の停止でも出る） | 既存（initializer は App 寿命） | 既存のまま（新しい依存なし） | protected property 1 つ（bool）。internal static 3 つ、internal enum 1 つ | 判定は単体（internal static）、起動の段は Player の実機（§5） | 起動の順序と失敗境界の正本 | 1326 → 1380（行数警報: 500 を超える既存 file。**非分割**。足すのは既存の Player の分岐と失敗境界の隣の数十行で、起動の順序と同じ変更理由・同じ寿命。分けると順序が 2 つの file に割れる。既存の大きさはこのスライスが作ったものではない） |
| `SampleGame/DependOnAll/AppInitializer.cs` | SampleGame の起動の hook の実装 | 失敗の override を「`IsPlayablePlayerKind` が偽なら今と同じ receipt を書く。`finally` で `Application.Quit(1)`」にする | 既存 | 既存のまま | なし | Player の実機（§5） | Player の override が並ぶ所 | 431 → 437 |
| `SampleGame/DependOnAll/PlayerContentSmoke.cs` | 動作確認の fixture の列と receipt の書き込み | 変えない | — | — | — | 今の `PlayerContentSmokeSequenceTests` | — | 160 → 160 |
| `SampleGame/DependOnAll/Editor/Build/SampleGamePlayerBuildCoordinator.cs` | SampleGame の Player の build の手順 | `Build()` を `Build(PlayerBuildKind)`（`Smoke`・`Playable`、Editor の internal enum）にし、content の build・mutation・adapter・publish の共通部分を 1 つの関数にする。種類ごとの値（宣言、probe の有無、`artifactsRoot`、`RuntimeJson` の `playerKind` と `probeToken`）を internal static の 1 か所で選ぶ。`Smoke` は今と同じ（宣言 `bs4-spring-full`、probe、`artifacts/bs4`）で `RuntimeJson` に `playerKind` = `smoke` を足す。`Playable` は宣言 `spring-full`、probe を作らず合成しない、`probeToken` は空、`playerKind` = `playable`、`artifacts/playable`。menu を 1 つ足す。`BuildReceipt` は変えない。build の結果（Player の directory、transport の directory、build report の options、mutation の中で読んだ stripping の水準）を返す internal の値を足す（AC1・AC10 の record 用） | 呼び出しの間 | 既存のまま | menu 1 つ。internal | `RuntimeJson` と種類ごとの値の選びは単体。build は Phase C の実機 | Player の build の正本 | 152 → 205 |
| `SampleGame/Tests/Editor/Build/Bs4FixtureComposerTests.cs` | 既存の `RuntimeJson` の単体テスト | `RuntimeJson` の引数の変更に合わせ、AC3 の 2 つの種類の全集合と `artifactsRoot` のテストをここに足す | test | 既存 | — | Unity なし | `RuntimeJson` の既存のテストの所 | 85 → 140 |
| `OneStarMaker/Tests/BuildContent/PlayerContentConfigurationTests.cs` | Player の構成の単体テスト | AC9（`RunsPlayerContentSmoke` の今の表は `RouteFirstScene` の表に置き換える） | test | 既存 | — | Unity なし（(5) は `UniTask` の async test） | 既存の表のテストの隣 | 174 → 260 |
| `SampleGame/Tests/Editor/Build/PlayerKindEndToEndTests.cs`（新規、`[Explicit]`。namespace は `Bs4FixtureComposerTests` と同じ `OneStarMaker.Tests.Editor.Build`） | 2 つの種類の Player を build・起動して観測する | AC1・AC2・AC4〜AC8・AC10 と AC6 の起動と観測 | test。出力は `TestResults/s41/<run id>/` | `SampleGame.DependOnAll.Editor`、`System.Diagnostics.Process` | — | Unity の batch と Player の process（Phase C） | 製品の coordinator を呼ぶので SampleGame の Editor の test。`[Explicit]` で通常の回帰に混ぜない | 0 → 320 |
| `unity/Assets/Docs/Architecture/04-app-startup.md`（Phase B は触らない。harvest） | — | — | — | — | — | — | — | — |

- **asmdef と依存:** 新しい asmdef・参照は無い。Game → Framework の向きは変わらない（Framework は種類の語彙・分岐・失敗境界を持ち、SampleGame は receipt の書き方と build の手順を持つ）。
- **`PlayerContentKind` を Framework に置く理由:** protected の比較と起動の分岐が同じ値を読む。App に置くと、App が Framework の分岐（smoke の尾）を種類で止める hook が要り、protected の比較の外に値が出る。
- **`PlayerBuildKind`（Editor）と `PlayerContentKind`（Runtime）を分ける理由:** coordinator の enum は build の手順（宣言・probe・出力先）を選び、Runtime の enum は起動の分岐を選ぶ。Editor の asmdef から Runtime の internal は見えない（`InternalsVisibleTo` の相手は `OneStarMaker.Editor` と test）。coordinator は baked の文字列（`smoke`・`playable`）を書くだけにする。2 つの対応は AC3 の単体テストと AC1・AC4 の実機で閉じる。

## 4. 実装計画

- 変更対象: §3 の表の file。
- 順序: (1) `PlayerBakedContentConfiguration` と単体テスト。(2) `PlayerContentConfiguration.Resolve` の比較と単体テスト。(3) `AbstractApplicationInitializer` の末尾の分岐、失敗の通知、unload の判定、latch、log と単体テスト。(4) `AppInitializer` の失敗の override。(5) coordinator の種類、menu、`RuntimeJson`、種類ごとの値、build の結果の値と単体テスト。(6) `PlayerKindEndToEndTests`（`[Explicit]`）。(7) Unity の Editor でコンパイルを確かめる（[osm-unity-editor](../../.agents/skills/osm-unity-editor/SKILL.md)）。Unity の test の実行、content と Player の build、Player の起動は行わない（Phase C）。
- **`PlayerKindEndToEndTests` の形:** 1 つの test が 1 つの Unity process で 1 つのことをする。全て `[Test, Explicit, Timeout(7200000)]`。
  - (a) `BuildSmoke`・`BuildPlayable`: coordinator の `Build(kind)` を呼び、返った build の結果（Player の directory、AC10 の観測、transport manifest の `unityVersion`）を `TestResults/s41/<run id>/<kind>.json` に書く（run id は環境変数 `S41_RUN_ID`、無ければ失敗）。その後、Player の directory を `TestResults/s41/<run id>/<kind>/player/` へ 1 回だけ写す（種類ごとに 1 つ。起動の case は全てこの写しを使い、`artifacts/` の publish 済みの Player を起動しない）。写した先の最長の path が 259 文字を超えるなら test を失敗させる（Unity 同梱の Mono と Player の path の制限。起動前の path の予算の検査の対象外のため）。
  - (b) 起動の case: `LaunchSmoke`・`LaunchSmokeOverride`・`LaunchPlayable`・`LaunchPlayableKindOverride`・`LaunchPlayableAddressables`・`LaunchPlayableMissingInstall`・`LaunchPlayableHuman`。各 case は、写しの中に `bs4-smoke-receipt.json` と `OneStarMakerLocal/` が無いことを確かめてから（あれば失敗）、`-logFile <run>/<case>/player.log` を付けて起動する。`SAMPLEGAME_` で始まる環境変数を除いた環境に case の環境変数だけを置く。動作確認の Player は `-batchmode`、遊べる Player は窓で `-screen-fullscreen 0 -screen-width 1280 -screen-height 720` を付ける（project の設定は枠の無い全画面 `fullscreenMode: 1` なので、付けないと × が無く owner の画面を占有する。`-` で始まる引数は `CommandLineConfigProvider` が key として読むが、protected の比較は `content:*` だけなので当たらない）。終わった後（kill した時も）、receipt と `OneStarMakerLocal/` を `<run>/<case>/` へ移す。case は判定必須の順に 1 つずつ走らせ、同じ写しを 2 つの case が同時に使わない。
  - 待ち方: `process.WaitForExit(500)` を刻みにした loop の中で、戻るたびに Player の log を `FileShare.ReadWrite` で開いて読み直し、待つ行の出現・process の exit・全体の制限時間を見る。exit の signal を待つ形なので `Task.Delay`・`Thread.Sleep` は使わない。`FileSystemWatcher` の通知だけを起こし手にしない（追記中の file の変更通知は遅れうる）。「60 秒の間終わらない」は loop の中で exit を見ないまま 60 秒が過ぎることで見る。`[AppInit] Application quitting.` の行を最初に見た UTC の時刻を `quitObservedUtc` として record に書く。窓を閉じる前に `Process.Refresh()` を呼ぶ。
  - record（case、pid、起動の引数と置いた環境変数、exit code、`timeout` の有無、起動から終わりまでの秒数、`quitObservedUtc`、receipt の有無と全文、Player の log と game の log の該当行（行が無いことも）、build の結果の値）を `TestResults/s41/<run id>/record.jsonl` に case ごとに 1 行で書く。XML の合格は record を書き終えたことだけを示し、AC の合否は record で判定 C が読む。
  - `LaunchPlayableHuman`（AC6）: 遊べる Player を AC4 と同じ窓の引数で起動し、record に pid と開始の時刻を書いてから、上の loop で exit を待つ（上限 1800 秒）。owner が Start を押し、InGame の画面が出たら Title と InGame の screenshot を撮って（Title は押す前）、窓の × で閉じる。終わった後、上の record の項目と AC6 (1)〜(4) の行の有無を書く。owner の screenshot は `<run>/LaunchPlayableHuman/` に置く。
- **Phase B から Phase A へ差し戻す条件:** §3 に無い file・asmdef・参照・公開 API が要る。`content:playerKind` を protected の比較に入れずに済ませる形にしたくなる。種類の分岐を App の hook だけで実装したくなる（Framework の smoke の尾を App が止める形）。`ContentDirectorySession` か `AssetManagement` の終了の経路を変える必要が出る（(u3)）。
- **B 適応として許すもの:** Phase C で Title の先に SampleGame の asmdef の型の strip による欠けが見つかった時、`SampleGame/DependOnAll/link.xml` に、その型（SampleGame が持つ assembly の型だけ）を足す。動作確認の Player の build にも効くが、契約の観測（receipt、exit code）は変えない（I1 (h)）。3 件を超えたら §2.4 (d)。Framework・Unity の組み込みの module の型の欠けは A 再開。
- **禁止:** 動作確認の Player の `RuntimeJson` の今の値（`playerKind` の追加を除く）、宣言、probe、出力先、build の設定、`BuildReceipt` を変えること。receipt の schema と file 名を変えること。`bs4` の名を持つ file・folder・latch を改名すること。`ContentOverlayStartup` と `WireContentResolution` の Player の分岐を変えること。Delivery の窓と Artifacts の code を変えること。入力を注入する検証支援や起動の時間の上限を製品に足すこと。
- 対象外を維持する方法: 上の禁止と、AC3 の単体テスト（動作確認の Player の baked config の不変）と、Phase C の構造レビューでの diff の確認。

## 5. テストとレビュー計画

- 単体テスト: `PlayerContentConfigurationTests`（AC9）、`Bs4FixtureComposerTests`（AC3）、既存の `PlayerContentSmokeSequenceTests`。
- 差し戻し中の起点 `-Filter`: `OneStarMaker.Tests.BuildContent.PlayerContentConfigurationTests;OneStarMaker.Tests.BuildContent.PlayerContentSmokeSequenceTests;OneStarMaker.Tests.Editor.Build.Bs4FixtureComposerTests`。
- 判定必須テスト（GO 候補 head）: 次を順に、それぞれ別の Unity process で実行する（環境変数 `S41_RUN_ID` を全てで同じ値にする。`T` は `OneStarMaker.Tests.Editor.Build.PlayerKindEndToEndTests`）。(1) 起点 filter。(2) `pwsh tools/run-tests.ps1 -Filter T.BuildSmoke`。(3) `T.BuildPlayable`。(4) `T.LaunchSmoke`（AC1）。(5) `T.LaunchSmokeOverride`（AC2）。(6) `T.LaunchPlayable`（AC4・AC5）。(7) `T.LaunchPlayableKindOverride`（AC7 (a)）。(8) `T.LaunchPlayableAddressables`（AC7 (b)）。(9) `T.LaunchPlayableMissingInstall`（AC8）。(10) `T.LaunchPlayableHuman`（AC6）。(11) 全 EditMode 回帰（`pwsh tools/run-tests.ps1`）。
- **判定の読み方（Phase C の担当が迷わないように固定する）:**
  - (2)(3) は Project Auditor の post-build 監査で `step.json` が `failed` になりうる（S4-0 の J2）ので、合否は XML（`Passed` と実行した test 名・件数）と record で読む。`step.json` の `failed` だけを NO-GO の根拠にしない。XML が無い・`Passed` でないのは不合格。
  - (4)〜(10) の XML の `Passed` は record を書き終えたことだけを示す。各 AC の合否は record の行で読み、XML の合否で読み替えない。
  - 起動の case の再実行で前の結果を上書きしない。再実行する時は別の `S41_RUN_ID` で (2) からやり直し、両方の run の record を evidence に残し、どちらの run で判定したかと、前の run を判定に使わない理由を §7 に書く。
  - 全 EditMode 回帰の staging の `Directory.Move` の access denied は、S5-0（PR #210）が content の build と transport の publish に再試行を入れて、1 run で failed 0 になった。それでも落ちた時は、落ちた test と log の `[ContentStagingMove]` の行を record に書き、S5-0 の後続の入力として owner へ返す（このスライスで読み方を変えない）。
  - E4 の「6000.6.9f1」は AC1 の `unityVersion` で読み、6000.6.9f1 の run だけを AC1 の合格に数える。別の版の run は追加の証拠として記録し、その版の Player の非退行を言う時は program §8 の S4-1 の行の版の要求を owner が改める（このスライスで読み替えない）。
  - game の log の Error 以上の行: AC1 の基準と同じ category・message の行は既存として record に書いて owner へ返し、AC4・AC6 (3) の違反に数えない。基準に無い行は違反に数える。基準は起動から Title までしか無いので、InGame の行は全て違反に数える。
  - 閉じる前と閉じた後: §2.2 の冒頭の境で分け、閉じた後の行は E3 の参考として record に残すだけにする（合否は exit code と時間）。
- 全 EditMode 回帰の適用除外: なし。
- 統合・Unity テスト: 上の (2)〜(10)。
- 操作・実行時・目視条件の検証経路:
  - AC1・AC2・AC4・AC5・AC7・AC8・AC10: 上の (2)〜(9) で閉じる。担当は Phase C の実行担当（Unity の batch と Player の起動。窓を出せる、利用者の desktop の session で実行する）。初期状態: Editor を閉じ、Active Build Target が StandaloneWindows64、作業ツリーが clean、他の Unity と `SampleGame.exe` が動いていない。事前に `git status --porcelain` を取る。観測は record・各 case の Player の log と game の log・receipt・build の結果・XML。事後に `git status --porcelain` を取り（`PlayerBuildProjectMutation` の戻しの確認。`artifacts/` と `TestResults/` は ignore の対象）、record の pid が全て終わっていることを CIM で確かめる（`Get-Process` は見えないことがある）。証拠は `TestResults/s41/<run id>/` の全て（写した Player の本体を除く）と XML を evidence bundle に入れる。
  - AC6: 担当は人間（owner）と Phase C の実行担当。実行担当が (10) を起動して owner に知らせ、owner が Title の screenshot を撮ってから Start を 1 回押し、InGame の画面が出たら screenshot を撮って窓の × で閉じる。(1)〜(4) の判定は実行担当（判定 C）が record の行で行う。owner は、押した時刻、窓を閉じた時刻、対象の build identity、(5) の観察（Title と InGame の画面が出たか、真っ黒・マゼンタでないか）を §7 に記録する。InGame に進んだかの判定を人間の観察に置き換えない。
  - 動作確認（Phase B の後）: `対象なし`（offline のテストで閉じない条件は全て Player の build を要し、Phase C が担当する）。
- 未知の操作経路の疎通: 次は未確認で、**初回確認は Phase C**（理由: Unity の test と Build の実行は Phase C の責任。担当: Phase C の実行担当）。
  - (u1) 製品の coordinator を EditMode の test から呼んで、6000.6.9f1 で動作確認の Player を build・起動できるか（S4-0 は自作の runner の Player を test から build した。製品の coordinator の test からの build は前例が無い）。確認地点: (2) と (4)。不成立時: 環境の不備（toolchain 等）は実行担当が原因を記録し、担当の権限で直せない物は owner へ返す。test から coordinator を呼べないことが原因なら Phase A へ戻す。AC1 の失敗が S4-1 の変更に依らない退行（S3-1・S3-2 の後の起動）かを切り分けるには、base `9de34cc2` の別の worktree で、menu と同じ `SampleGamePlayerBuildCoordinator.Build()` を Editor の eval（[osm-unity-editor](../../.agents/skills/osm-unity-editor/SKILL.md)）で呼んで build し、同じ起動（`-batchmode`、環境変数なし）で receipt と exit code を見る。base でも同じなら owner へ返す（このスライスで直すかは owner が決める）。
  - (u1b) Player の publish（`PlayerBuildPublisher` の staging → `players/<identity>` の `Directory.Move`）は S5-0 の再試行を通らない（`ContentStagingMove` は internal で、`SampleGame.DependOnAll.Editor` からは見えない）。repo が監視される PC では、(2)(3) がこの移動の access denied で落ちうる。確認地点: (2)(3) の XML と log。落ちた時: 環境の失敗として record に書き、§5「判定の読み方」の再実行の規則（別の run id で build からやり直し、両方を残す）に従う。このスライスで publisher に再試行を足さない（参照の追加は設計判断。§2.3）。
  - (u2) Title の先（Home → InGame）が IL2CPP・High で動くか。確認地点: AC6。不成立時: SampleGame の asmdef の型の欠けは B 適応（§4）、それ以外は Phase A へ戻す。
  - (u3) directory を登録したままの quit が Player で exit 0 になるか（Title の上と、InGame の native の load・unload が動く上）。確認地点: AC5 と AC6 (1)。不成立時: Phase A へ戻す（`ContentDirectorySession` の終了の経路を変えるかは設計判断）。戻った時に検討する候補（採るかは戻った時に決める）: Player だけ `Application.wantsToQuit` で quit を保留し、正規の unload → `CloseContentDirectoryAsync` の後に `Application.Quit` を呼ぶ（Editor の Play 停止では使えないので `CompletePlayStop` は残す）。Unity の Editor の終了の stall（[Harness README の停止規則](../../tools/Harness/README.md#unity終了時の既知native-stallと調査の停止規則)）とは分けて記録する。
  - (u4) 窓を出した Player を Unity の batch の test から起動し、`CloseMainWindow` で閉じられるか。確認地点: (6)。不成立時: 窓の無い環境なら実行担当が記録して owner へ返す。
  - (u5) Unity の batch の test の中で、追記中の Player の log を `WaitForExit` の刻みで読み直して行の出現を拾えるか。確認地点: (6)。不成立時: 読み方の不備は B が直す（B 適応。`FileShare` と読み直しの間隔だけで、契約は変えない）。
- 人間の判断が必要な条件: AC6 の Start の 1 回の押下、窓を閉じること、(5) の画面の観察と screenshot。担当は owner。(1)〜(4) の判定は AI が log で行う（観察記録で判定を置き換えない）。(5) は観察記録として受理し、監査担当が画像を再評価できることまでは求めない。§2.5 の (1)。
- 高コスト証拠: なし。Player の build は外部への作用が無く、この PC で繰り返せる（2 回、各約 6 分の見込み。S4-0 の実測）。
- 判定 C の照合 script の方式: なし。判定 C は record と receipt と log を AC ごとに読む。
- 機械検査: `contract-audit`、`docs-audit`。
- A0/A1 主担当: 本 session（Claude Opus 5.5、Anthropic）。A0 は program §8 の S4-1・S4-2 の行、「S4 の A1 への入力（2026-11-23）」の (1)、「S4-0 の A1 が振り分けた入力」と「S4-0 から移送した入力」の S4-1 の項目、実測の事実の Player の節、04 章を入力とした。
- C' 用に予約した担当: OpenAI の系列（Codex CLI。Phase A・B・C に関与していない。Phase C 系の担当では、使うモデルと effort を起こす時に明示する）。Phase A には Anthropic（A1・A2・A3）と Grok（外部計画レビュー。PR Partner が owner の account で投稿した）が関与したので、Grok 系と Anthropic 系は C' に使わない。この予約を崩さないため、外部計画レビューの 3 巡目以降と Phase B・C に OpenAI の系列を使わない。
- 独立性の強化条件を満たせない場合の理由: C' を起こす時に Codex CLI が使えなければ、人間が C' を担当するか、`独立性制約あり` として理由と残存リスクを §8 に記録して owner が採否を決める。

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
| 遊べる Player の出力を BS4 と別の root にする | 初稿のとおり | `artifacts/playable/`（外部計画レビューの P3 で transport・work も含むと明記した） |
| build receipt に backend・stripping・development の 3 つを記録する | 不採用 | 今の欄は定数を書くだけで観測にならない（(3) の m-5）。構成は Player の出力と build report で AC10 として確かめ、`BuildReceipt` は変えない |
| 成功の観測に Title の `Loaded.` を使う | 採用 | AC4（game の log は Player の終わりの後に読む） |
| 失敗の例に `RUNTIMEMODE=addressables` の override を足す | 採用 | AC7 (b) |
| coordinator の共通部分を関数に括り出す | 採用 | §3 |
| 起動の途中で窓を閉じると cancel が失敗として `Quit(1)` になる | 採用（記録） | 害は小さい。I3 (b) の経路として残す |
| Title の先の strip の修正が数件を超えたら分ける | 採用 | §2.4 (d)（3 件） |
| 生成する bootstrap Scene の名前で種類を分ける | 不採用（代替の検討で本人も不採用） | 判定が SampleGame の Scene 名と Framework に割れる |
| key の名前 `content:playerRun`、値 `play` | 不採用 | 名前だけの違い。`content:playerKind`・`playable` のまま |

(3) アーキテクチャゲート兼反証。blocker 1 件、major 3 件、minor 11 件。

| ID | 指摘 | 採否 | 理由と反映 |
|---|---|---|---|
| B-1 | 失敗の override の receipt の書き込みが投げると `Quit(1)` に届かず、遊べる Player が Title を出したまま動き続ける | 採用 | `finally` で必ず Quit する（§2.1、I3 (d)、§3）。§2.5 の (4) で owner が承認した |
| M-1 | `spring-full` を選ぶ理由（Delivery の install を使える）が、照合が同じ build identity しか通さない code と食い違う | 採用 | 理由を直し（§2.5 の (3)）、UC3 と §1 の現況に制約を書き、S4-2 への入力にした（§2.3） |
| M-2 | 失敗時の初回 Scene の unload の条件が 2 引数の判定で変わり、遊べる Player で Title を残したまま close に入る | 採用 | unload の条件は Player の構成のまま（§2.1、§3、AC9 (4)） |
| M-3 | AC6 の log の検査が ZLogger の出力を見ていない | 採用 | game の log を AI が読む形にし、人間の役割を押すことだけに狭めた（AC4・AC6、§5）。外部計画レビューの P1 で終端を InGame の `Player ready` まで延ばした |
| m-1 | Delivery の窓は Player を起動しない。Artifacts は欄の追加を拒まない | 採用 | §0 の risk、§1 の現況、I1 の未確認を直した |
| m-2 | 既存の `RuntimeJson` のテストが §3 に無い | 採用 | `Bs4FixtureComposerTests` を §3 に足し、AC3 のテストをそこに置く |
| m-3 | 同じ folder の namespace が 2 種類ある | 採用 | 新しい file は `OneStarMaker.Tests.Editor.Build` と明記し、filter を合わせた |
| m-4 | watcher の Renamed、`Timeout` の明示、`Refresh()`、写した path の長さ | 採用 | §4 |
| m-5 | build receipt の backend・stripping は定数で観測にならない | 採用 | AC10（`GameAssembly.dll`、`Managed/` の不在、build report の options、mutation の中の stripping） |
| m-6 | 時間の制限と `-batchmode` の有無が AC ごとに無い | 採用 | 全ての起動の AC に制限と起動の形を書いた |
| m-7 | I4 の「unload は起きない」が E2 の Title → Home と字面で矛盾する | 採用 | I4 を「操作の無い間に、起動の段として」に限った |
| m-8 | 分岐は baked を読むと明記。比較は文字列の ordinal にする | 採用 | §2.1、§3（外部計画レビューの P2 で「baked の生の値と比べる」まで明記した） |
| m-9 | E4 の版が record に残らない | 採用 | AC1 に transport manifest の `unityVersion` |
| m-10 | u1 の不成立時に base で確かめる手順が無い | 採用 | base の worktree で menu と同じ `Build()` を Editor の eval で呼ぶ手順（§5 の (u1)） |
| m-11 | DebugSocket は環境変数で真にできる | 採用 | 「既定で偽」に直し、保護は足さない（§2.3） |
| 所見 7 | `BuildReceipt.playerSession` は要らない | 採用 | `BuildReceipt` を変えない（§3、§4 の禁止） |
| 所見 5 | B 適応の `link.xml` が動作確認の Player の build にも効く | 採用 | I1 (h)、§4 の B 適応に明記 |

**外部計画レビュー 1 巡目（PR head `9e47969c` が対象。2026-11-23。PR の review 2 件。P2・P3 の review は PR Partner（Grok Bot）が owner の account で投稿した、と 2 巡目の指摘が書く。P1 の review の担当は記録なし）:**

| ID | 指摘 | 採否 | 理由と反映 |
|---|---|---|---|
| P1-a（I3） | `CleanupAfterStartupFailureAsync` は派生の後始末と失敗の通知を 1 つの try で呼ぶので、`OnAfterSceneLoadInitializationFailed` が投げると失敗の通知（と `Quit(1)`）が呼ばれない。`finally` の Quit では届かない。検証も要る | 採用 | code で確かめた（`AbstractApplicationInitializer.cs:689-704`）。2 つの hook を別々の try にし、順の保証を internal static `RunStartupFailureHooksAsync` に置いて AC9 (5) の単体テストで固定する（§2.1、I3 (c)、§3）。実 Player で hook に例外を注入する口は製品に足さないので、実機の観測は無く、単体テストで閉じる |
| P1-b（AC6） | `OnStabledImpl HomeScene` は、その後の `SceneFlow.EnterInGame` の await より前に出るので、InGame への遷移が失敗しても合格しうる | 採用 | code で確かめた（`HomeScene.cs:35-41`）。AC6 の終端を `[InGameSession] initial season ensure started` と `Player ready at Cell stream spawn` まで延ばし、`Season branch ensure failed` を含む Error と、Player の log の `Exception` も見る（E2、AC6） |
| P2-a | `content:playerKind` の比較は baked の生の文字列と比べることを明記する。直した値と比べると、key の無い baked が override なしで拒まれる | 採用 | 生の値 `PlayerKindRaw` を持ち、それと比べる（§2.1、§3）。AC9 (2) に 2 行を足した |
| P2-b | 2 引数の `RunsPlayerContentSmoke` が偽を返すと、今の 2 段の `if` で playable が `LoadEditorFirstSceneAsync` に落ちる | 採用 | 3 値の internal static `RouteFirstScene` にし、AC9 (3) の表で 4 通りを固定する（§2.1、§3）。AC4 に `Editor content directory first scene` が出ないことも足した |
| P2-c | AC3 で遊べる Player の `RuntimeJson` の key を全て固定する | 採用 | AC3 を 2 つの種類の全集合（13 の key）の表にした |
| P2-d | E3 を Title 以外の Scene から閉じた時にも観測する。人間の起動の record に exit code と `timeout` が無い | 採用 | E3 を Title と InGame の両方にし、AC6 (1) に exit 0 を足し、record に exit code・`timeout`・秒数を書く（§4） |
| P3-a | 遊べる Player の時の transport と work の置き場 | 採用 | `artifactsRoot` を `artifacts/playable` にし、transport・work・players を全てその下に置く。AC3 の単体テストで固定する |
| P3-b | baked の `playerKind` が不正な値の遊べる Player は、latch が `Smoke` のままで動作確認の receipt を書く | 採用（記録） | latch を「生の値が ordinal に `playable` か」で決め、検証より前に置いた。不正な値（大小違いを含む）は `Smoke` のままで receipt を書く。harvest に書く（§0）。AC9 (6) で固定する |

**Phase C を見越した A3 の見直し（統合担当: 本 session。外部計画レビューの指摘に無かったもの）:**

| 項目 | 反映 |
|---|---|
| game の log（ZLogger）は非同期で、全てが file に出るのは quit の Dispose の後である。起動中に読むと行が欠けうる | game の log は Player の process が終わった後に読み、kill した起動の行は「観測なし」と扱う（§2.2 の冒頭、AC4 の `Loaded.` は AC5 の後） |
| case ごとに Player の全体を写すと、content を含む大きな directory を 8 回写す | 種類ごとに 1 回だけ写し、case を 1 つずつ走らせ、case の receipt と game の log をその都度 case の folder へ移す。起動の前に残りが無いことを確かめる（§4） |
| 起動の case の再実行で前の結果が消えると、判定 C が都合のよい run を選べる | 再実行は別の run id で build からやり直し、両方の record を残し、選んだ理由を §7 に書く（§5「判定の読み方」） |
| `step.json` の `failed`（Project Auditor）と XML の `Passed` のどちらで読むか、Launch の XML の `Passed` が AC の合格を意味するか | §5「判定の読み方」に固定した |
| 起動が終わらない（hang）時、製品は失敗にならず Player は動き続ける。I3 をそれで破られたと読まれうる | I3 の「失敗」を例外を投げた時に限り、hang は §1 の対象外・§2.3 の未所有の後続にした。検証では AC4 の時間の制限が拾う |
| 遊べる Player の成功の hook（`OnPlayerContentStartedAsync`）は、receipt を書かないと決めた後は呼ぶ者がいない | 足さない（§2.1、§3）。成功の観測は Framework の log の 1 行だけ |
| 窓を出す起動は、利用者の desktop の session で走らせる必要がある | §5 の検証経路の担当に書いた |

**外部計画レビュー 2 巡目（PR head `79aadb27` が対象。2026-11-23。行の指摘 14 件。投稿は owner の account で、PR Partner（Grok Bot）と Claude Code の生成の印があるものを含む）:**

| ID | 指摘 | 採否 | 理由と反映 |
|---|---|---|---|
| P1（AC4・AC6 (4)） | strip の検出語が代表的な症状（`The referenced script … is missing!`、`Could not produce class with ID …`）を拾わない | 採用 | 検出語を `MissingMethodException`・`stripped`・`is missing`・`Could not produce class` にし、Phase C の最初の run で実際の文言と照らす（§2.2 の冒頭）。語を足すのは決定記録で行う |
| P2（AC8） | negative test が目的の失敗で落ちたことを確かめていない | 採用 | `ContentDeliveryException` の例外の行を必須にし、全文を record に残す（AC8）。base で、存在しない root は `InstalledRevisionVerifier` が `ContentDeliveryException` で拒むことを確かめた |
| P2（判定の整合） | Unity の版が変わった時、AC1 と「版の変更だけを止める理由にしない」が食い違う | 採用 | 6000.6.9f1 の run だけを AC1 の合格に数え、別の版は追加の証拠にする（AC1、§5「判定の読み方」） |
| P2（u3） | 落ちた時の戻り先の候補を 1 行書く | 採用 | `Application.wantsToQuit` で quit を保留し正規の close の後に Quit する候補を (u3) に書いた（採否は戻った時） |
| P2（描画） | 描画の正しさが対象外にも AC にも無い | 採用（(b)） | AC6 (5) に owner の観察（真っ黒・マゼンタでない）と screenshot を足し、観察記録として受理する。描画の細部は §1 の対象外 |
| P2（u4） | 遊べる Player は全画面で出る（`fullscreenMode: 1`、`resizableWindow: 0`、1024x768） | 採用 | ProjectSettings で確かめた。起動の case に `-screen-fullscreen 0 -screen-width 1280 -screen-height 720` を付ける（§4、AC4） |
| P2（AC4） | `FileSystemWatcher` の通知だけで log の行を待つと偽の `timeout` になりうる | 採用 | `WaitForExit(500)` の刻みで読み直す loop にした（§4）。未知の経路 (u5) に足した |
| P2（基準） | game の log に Error が無いことを比べる基準が無い | 採用 | AC1 の動作確認の Player の game の log の Error を基準として record に残し、同じ行は既存として owner へ返す（AC1・AC4、§5「判定の読み方」） |
| P2（C' の独立性） | 外部計画レビューに Grok 系が関与したので、`cursor-agent`（Grok）の予約では強化条件を満たせない | 採用 | C' を OpenAI の系列（Codex CLI）に予約し、3 巡目以降と B・C に使わない。使えなければ人間か `独立性制約あり`（§5） |
| P3（AC6 (3)(4)・AC4） | 窓を閉じた後の終了処理の log を検査に含めるか | 採用 | Framework が quit の始めに出す `[AppInit] Application quitting.` を境に、閉じる前と閉じた後を分けて読む。閉じた後は記録だけ（§2.2 の冒頭、§3、§5） |
| P3（AC6 (1)） | 「窓を閉じてから 120 秒」の起点が人間の記録頼み | 採用（(b)） | quitting の行を test が見た時刻（`quitObservedUtc`）を起点にする。人間の時刻は照合だけに使う |
| P3（独立性の記録） | 1 巡目の P2/P3 の review と 2 巡目は PR Partner（Grok Bot）の投稿 | 採用 | §5.1 の 1 巡目の見出しと §5 の C' の予約に書いた |
| P3（key の既定） | key が無ければ `Smoke` が守る相手はいない（build identity は毎回新しく、baked は同じ build の exe と対） | 採用 | key を必須にした（§2.1、AC9 (1)(2)(6)）。「key の無い baked」の特別扱いが消えた |
| P3（latch の範囲） | latch が `Smoke` のまま残るのは不正な値より広い（file の欠け・壊れ） | 採用 | §2.1 の latch と §0 の harvest の文言を広げた。動作は変えない |
| P3（名前） | `PlayerContentSession` が既存の session の語と紛れる | 採用 | key を `content:playerKind`、enum を `PlayerContentKind`（Runtime）・`PlayerBuildKind`（Editor）に改め、語の注記を §2.1 に書いた。1 巡目までの表の旧名はそのまま残す |

**凍結（2026-11-23）:** owner が外部計画レビューの 2 巡目までの統合を見て凍結を指示した。3 巡目は行っていない。

## 6. Phase B 実装結果

- 実装: §3 の file を §4 の順序で実装した（新しい asmdef・参照・公開 API は無い。足した公開面は protected `IsPlayablePlayerKind` 1 つと menu 1 つ）。
  - `PlayerBakedContentConfiguration`: internal enum `PlayerContentKind { Smoke, Playable }`、`PlayerKindRaw`・`PlayerKind`、key 定数 `PlayerKindKey`、必須の読み `ParsePlayerKind`（無い・空・`smoke`/`playable` 以外は `ContentDirectoryException(InvalidConfiguration)`）、latch の判定 `LatchPlayerKind`（ordinal に `playable` の時だけ Playable）。
  - `PlayerContentConfiguration.Resolve`: 今の 9 つの比較の後に、`PlayerKindRaw` と重ねた後の `GetString("content:playerKind","")` の ordinal 比較を足した（message は今と同じ `Protected Player content configuration was overridden.`）。
  - `AbstractApplicationInitializer`: 末尾の 2 段の `if` を `RouteFirstScene`（internal enum `PlayerFirstSceneRoute`）の switch にし、`LoadPlayerFirstSceneAsync`（stage `load-player-first-scene:<Scene>`、log `[AppInit] Player content first scene: scene=… identity=… kind=playable stable=…`）を足した。分岐の種類は検証済みの baked から読む。`RunsPlayerContentSmoke` を消し、失敗時の unload を `UnloadsFirstSceneOnStartupFailure` に、派生への通知を `RunStartupFailureHooksAsync`（別々の try）にした。`BuildConfig` が baked の検証より前に `_latchedPlayerKind` を決め、`BootstrapBeforeSceneLoad` の冒頭で Smoke に戻す（`ReleaseAll` では消さない）。`OnApplicationQuitting` の始めに `[AppInit] Application quitting.` を出す。
  - `AppInitializer`（SampleGame）: 失敗の override を「`IsPlayablePlayerKind` が偽なら今と同じ receipt、`finally` で `Application.Quit(1)`」にした。
  - `SampleGamePlayerBuildCoordinator`: `Build(PlayerBuildKind)`、種類ごとの値を `SettingsOf`（宣言・probe の有無・出力先の folder 名・baked の `playerKind`）と `ArtifactsRootOf` の 1 か所で選ぶ。共通部分を `BuildPlayer` にした。menu `Tools/OSM/Content/Build Spring Playable Player` を足した（今の menu は `Build(Smoke)`）。`RuntimeJson` に `playerKind` の引数と欄を足した。build の結果 `PlayerBuildResult`（identity、publish 先、transport、build report の options、mutation の中で build の直前に読んだ backend と stripping）を返す。`BuildReceipt` は変えていない。
  - テスト: `PlayerContentConfigurationTests` に AC9 (1)〜(6)（今の 9 つの key の override の拒みを key ごとに含む）、`Bs4FixtureComposerTests` に AC3（2 つの種類の 13 の key の全集合を `JsonConfigFlattener` で平らにして比べる、`artifactsRoot`）。`PlayerKindEndToEndTests`（新規、`[Explicit, Timeout(7200000)]`）に §4 の build 2 つと起動 7 つ。
- HANDOFF との差（どれも設計判断を足していない）:
  - 派生への通知の失敗の log は、どちらの hook かを括弧で足した（`[AppInit] Cleanup after failed AfterSceneLoad initialization also failed (derived cleanup): …` と `(player content failure notification)`）。AC はこの文言を見ない。
  - AC9 の表の test は、internal の enum を public の test の引数・戻り値にできないので、種類を bool、結果を enum の名前の文字列で表した。
  - 種類ごとの値は coordinator の file の中の internal の `PlayerBuildKindSettings` と、build の結果は同じ file の internal の `PlayerBuildResult` に置いた（§3 の「internal static の 1 か所」「internal の値」）。
  - 行数: coordinator 152 → 229（予想 205）、`PlayerKindEndToEndTests` 0 → 445（予想 320）、`AbstractApplicationInitializer` 1326 → 1419（予想 1380）、`PlayerContentConfigurationTests` 174 → 320、`Bs4FixtureComposerTests` 85 → 158、`AppInitializer` 431 → 441。責務は §3 のまま。
  - `PlayerKindEndToEndTests` の観測の細部（判定 C が record を読む時に要る）: `quitObservedUtc` は test が 500 ms の刻みで log を読み直して初めて quitting の行を見た時刻。game の log は `OneStarMakerLocal/Log/` の全 file の `{` で始まる行を `JsonConfigFlattener` で読み、Error・Critical の行と `GameLogProbes` の message の行だけを record に書く（`Timestamp` と `quitObservedUtc` で `beforeQuit` を付ける）。Player の log は検出語ごとに当たった行（無ければ `line` 0 の 1 件）と、quitting の行より前かを書く。`LaunchPlayable` は初回 Scene の行が出ないか 60 秒の間に終わったら閉じる要求を送らない（kill は finally）。AC6 の起動は `<run>/LaunchPlayableHuman/started.json` に pid と開始の時刻を書き、test の進捗の出力に owner への手順を出す。同じ run id で build や case を 2 回走らせると test は失敗する（上書きしない）。
- 未実行: Unity の test（起点 filter、判定必須の (1)〜(11)）、content と Player の build、Player の起動は全て未実行（Phase C の担当）。単体テストもコンパイルを確かめただけで走らせていない。
- 実行した確認: Editor 6000.6.9f1 を `unity open` で起動して import を待ち、`EditorUtility.scriptCompilationFailed` が偽・`isCompiling` が偽であること、`PlayerKindEndToEndTests`・`PlayerBuildKind`・`PlayerContentKind` の型が load された assembly にあることを eval で確かめた。dirty な Scene が 0 であることを確かめて Editor を正常終了した（`git status` は実装の file と新しい test の `.cs`・`.meta` だけ）。`pwsh tools/contract-audit.ps1` は errors 0・warnings 0。
- implementation head commit: §0 の implementation head 欄のとおり（実装と本票を 1 つの commit で切った）。
- Phase B 担当・モデル・ベンダー: 本 session（Claude Opus 5.5、Anthropic）。

### 動作確認

- 対象 head: なし（動作確認は `対象なし`）
- 条件ごとの実行担当・環境・実行したコマンド: なし
- 生の観測（出力、事前・事後の観測。合否は書かない）と保存先・状態: なし
- 検証経路の期待と違う観測と、B への差し戻し: なし
- `未実施` の条件と理由・担当: `対象なし`（§5）

## 7. Phase C

未着手。

## 8. Phase C'

未着手。

## 9. Phase D

未着手。
