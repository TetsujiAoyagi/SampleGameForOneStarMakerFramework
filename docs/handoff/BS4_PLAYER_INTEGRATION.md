# BS4 — Player integration HANDOFF

- type: slice
- status: A3 revision 2 frozen（Phase B input。revision 1 は ownership gap で再開）
- branch: `codex/bs4-player-integration`
- implementation base commit: `ea7acf7` (`develop`, 2026-09-19)
- implementation head commit: 未作成
- risk: high（起動、Player packaging、code stripping と AOT）
- owner: BS4 主担当
- created: 2026-09-19
- expires: BS4 Phase D。2026-10-19 までに継続条件を再確認する。
- harvest to: `unity/Assets/Docs/Architecture/04-app-startup.md`、`13-resource-system.md`、`18-asset-description.md`
- Phase A snapshot: `TestResults/bs4/review/a3-frozen/phase-a.md`（tracked HANDOFF の A3 frozen commit と同内容）

## A0 — 同一入力 packet

### この slice が答える問い

対応する成功 Content BuildReport から組み立てた固定 target の Player が、選択 content を本体へ二重同梱せず、独立した local directory を登録して論理初回 Scene と代表非 Scene content を利用・解放して終了できるか。

### 現況と根拠

- `develop` は `ea7acf7`、PR #63 の BS3 実装と #64 のレビュー artifacts 削除を含む。開始時 tracked / untracked 差分はない。
- BS2b の `BuildContentResult` は identity、成功 content path、preflight/outcome report、manifest/metadata path を返す。成功 directory は `StandaloneWindows64-Player` 用。現在の SampleGame content menu は `BuildContentResult` をログに出すだけで Player build に渡さない。
- BS3 `ContentDirectorySession` は単一 root の登録・index・native load の寿命を持ち、`IAssetManagement` の型付き load と owner/cache が利用権を保持する。同一 process revision 削除は `ContentRevisionGate` の lease を使う。この契約を変更しない。
- `AbstractApplicationInitializer` の directory mode は Editor Play 限定。config、UICommon、SceneResourceMap は先に Addressables から取得される。`RegisterAlreadyLoadedScenes` は既にロードされた Unity Scene を map の名前で照合するだけ。Player の論理初回 Scene を content から新規に開始する動作はない。
- 旧 `VariantPlayerBuild` は active target、`SampleScene.unity` と共有 `app-config.json` の一時書換を使う。BS4 はこの新経路を再利用しない。
- 公開 Architecture §4 は従来の Scene 0 と Addressables bootstrap を説明し、§13・§18 は BS3 の Editor directory 経路までを実装済みとして記す。Player 成立済みとは記さない。
- Unity の公式 stripping 説明では content-only build の型は Player から除去され得る。Addressables の自動 link.xml と CD の保持を同一視せず、直接参照・明示的な preserve と実 Player 検証で因果を確認する。

### 要求、制約、最低条件の入力

- 固定 target は Windows Standalone 64 bit。bootstrap Scene だけを Player Scene 一覧へ入れ、選択された content Scene を直接同梱しない。
- build 要求から専用 runtime config を生成し、local directory、build identity、logical first scene、起動時 representation を渡す。共有 `app-config.json` の mutation は不要にする。
- 対応する成功 Content BuildReport と Player の関係を診断可能に固定し、mismatch / 欠損 / 部分失敗では起動・build を失敗させる。BS3 の登録・利用寿命・取消後 drain 契約を保つ。
- 必要 code を High stripping で保持し、IL2CPP/AOT の固定 target Player を実行して bootstrap→登録→論理初回 Scene→代表非 Scene fixture の load/release→終了を確認する。単なる report 成功や Editor Play は GO の代替にしない。
- 最終全 EditMode 回帰、必要な PlayMode、content build と Player 実行を判定証拠とする。必須 Player 検証が環境不足でできなければ NO GO。
- Unity C# は `#nullable enable`、`record` 不使用、偽 null 判定遵守。Runtime→Editor 依存、新 asmdef 参照、公開 API の無断追加を避ける。Scene / Prefab / Addressables 等は Editor で操作し、Phase B は対象限定・コンパイルまで、テストと Build は Phase C に置く。

### 既知の未決事項（A1 で調査して閉じる）

1. Addressables bootstrap の必要最小限を Player へどう残し、SceneResource graph metadata の source 不在利用をどう成立させるか。UICommon 等の既存経路との境界。
2. runtime config の配置、読み取り順、identity / target / path 検証、local directory の移設と Player 配布形態。
3. Content BuildReport と Player の照合点、build 順序、失敗時の staging と既存成功 Player 保護。
4. Content Scene の二重同梱の機械的検査と、stripping / IL2CPP / AOT の明示保持方法。
5. 固定 target 実機での自動成功信号、代表非 Scene fixture、終了と native resource release の確認方法。
6. 起動時表現固定の範囲と同一 session 内の別表現要求の扱い。

### ここでは答えない問い

- HTTP/LAN 配信、install・hash 検証、別 process 排他、物理削除は DIST。
- 旧 Addressables build/workflow、旧 Player overlay の廃止は RET。BS4 は新 Player 経路の成立を確認する。
- 通常 Editor Play 停止時の完全 drain は RET。Scene 以外の本番 Description 型と Mesh サブアセットは実需要時の非 Scene slice。
- 同一 session 中の任意の表現切替は後続の専用 slice。BS4 の Player は起動時一回固定を守り、別表現要求の失敗境界を確認する。

## A1 — 設計初稿（A2 入力）

### 進める最低条件と判定

1. **対応付けと単一同梱:** 同じ実行で得た成功 `BuildContentResult` の identity / target / outcome / manifest / metadata を検証し、`BuildPlayerOptions.previousBuildReportDirectories` へ対応 metadata を渡した Windows x64 Player を作る。Player Scene 一覧は既存の `UIScene` 一件。selected content Scene の GUID が Player の `BuildReport.packedAssets` に無く、Player 側の Addressables content と `StreamingAssets` に第二の copy が無いことを証拠化する。content は Player 出力の sibling directory に一回だけ配置する。
2. **起動と寿命:** 専用生成 config から relative local directory、identity、target、representation、論理初回 Scene を読み、source asset / AssetDatabase 無しで graph metadata を得る。登録して SceneDirector から初回 Scene を明示 load し、代表 Prefab `GameObject` を型付きで load / release（可能なら instantiate / destroy）して、終了時に BS3 session の利用権を正しく解放する。欠損・不一致・別表現要求を黙って Addressables に切り替えない。
3. **code と実機:** IL2CPP、High managed stripping、`StandaloneWindows64` Player を実 build・実行し、上記の順序と終了を Player の生ログ・検証 receipt・process exit で確認する。対応する content 側の code type 情報を Player build に渡すこと、必要な実型・generic 呼出しの保持を実挙動で示す。必須 Player 実行が不能なら NO GO。

受け入れ詳細: config に schema version / build identity / target / relative path / first scene / representation を固定する。path は install root に対して canonical 化し、root 外・絶対パス・欠損を拒否する。`ContentDirectorySession.Register` が root identity / target を再照合する。Player receipt は content report path と identity、Player build GUID、backend、stripping、scene 一覧、排除した GUID の検査件数、smoke 各段階を残す。build 失敗時は成功済み Player を上書きせず staging に失敗情報を残し、部分出力を成功として公開しない。

GO は上記三条件、判定必須テストと C/C' の blocker 無しを全て満たした場合のみ。CONDITIONAL ACCEPT は用いない。最低条件達成後、DIST / RET / 非 Scene 本番拡張の問いを追加せず終了する。

### 技術判断と比較

- **bootstrap:** `Assets/OneStarMaker/Scenes/UISystem/UIScene.unity` は既に `UICommon` と EventSystem を持つ。これを唯一の Player Scene 0 とし、directory Player では `LoadUICommonAsync` がロード済み component を使うことを必須にする。未発見時に Addressables load へ逃げない。新たな本番 Scene 作成や既存 Scene の広範再生成を要しない。
- **graph metadata:** build 前に production `SceneResourceMap` を BS4 所有の一時 `Resources/BS4/SceneResourceMap` asset へ Editor の `AssetDatabase.CopyAsset` で複写する。map と参照する `SceneResource` 定義は Player に保持し、Unity Scene payload は Player Scene 一覧に含めない。`AssetReference` は GUID locator であって直接 Unity Scene 参照ではないという仮説を build report の GUID 検査で証明する。もし選択 Scene が混入するなら build を失敗させ、Phase A revision で薄い graph catalog へ切り替える。`SceneDirector` の graph / lifecycle 契約は変えない。実行時に source path や AssetDatabase は要求しない。
- **設定:** 共有 `Assets/SampleGame/Config/app-config.json` は読み書きしない。`OSM_BS4_PLAYER` を `BuildPlayerOptions.extraScriptingDefines` に付け、Game composition root から `StreamingAssets/bs4-runtime.json` を必須の plain-file provider として先頭に加える。環境変数・コマンドラインが上書き可能な既存順序は維持する。ただし identity、target、path、first scene、representation の検証は上書き後の値に対して行い、不正なら起動失敗。Player 専用 JSON は Editor build の一時 staging から生成し、build 後に生成元のみ消す。`runtimeMode=directory` を必須にし、Player 以外の既定は Addressables のまま。
- **build の対応:** SampleGame Editor に新 Player coordinator を置き、呼出側は `BuildContentResult` をそのまま渡す。preflight と outcome を読み、schema / identity / target / success path / manifest の一致を確認し、Report のみ・directory のみの任意入力を受けない。`previousBuildReportDirectories` は Unity が content 型保持の情報を Player 側へ渡す正規口。成功後に Player と content を一つの publish directory に収め、receipt を書いて staging→final の同一 volume rename。旧 `VariantPlayerBuild` と一時 overlay を使わない。
- **二重同梱:** Player Scene 配列一件を検査し、`BuildReport.packedAssets[].contents[].sourceAssetGUID` で preflight の selected scene GUID と交差しないことを検査する。既存 `StreamingAssets` の未知ファイルや Addressables の Player build 自動生成は preflight で拒否または限定抑止し、復元する。判定 C は publish directory のファイル一覧と GUID 検査結果を固定する。metadata だけに頼らず Player build report を見る。
- **stripping/AOT:** BuildProfile は IL2CPP / High / x64 を明示して build 後に Editor 設定を復元する。`previousBuildReportDirectories` で Unity の content 使用型を linker へ渡す。静的な generic `LoadContentAssetAsync<GameObject>` と Prefab instantiate を smoke 経路に置く。実 Player で欠ける型だけ `link.xml` または明示 code root を追加し、その理由をコメントと HANDOFF に記録する。CD0 Mono 成功、BuildReport 成功、`link.xml` の存在単独では成立としない。
- **失敗:** Player 前の検証失敗は Player build を開始しない。Unity build 失敗、dedup 検査失敗、copy / receipt / publish 失敗は失敗 outcome に identity と stage を残す。以前の成功ディレクトリは不変。起動時 config / directory / graph / first scene の失敗は stage 付き error と非ゼロ smoke exit を残し、Addressables へ fallback しない。session 登録後の失敗は BS3 の close / drain に従う。
- **起動時表現:** config の一値を SceneDirector と smoke object request に固定する。同一 session で異なる表現を要求する通常 UI は作らない。BS3 の `EntryAmbiguous` になるケースを隠さず失敗として確認し、任意の切替は後続専用 slice とする。

### 責務マップと規模

- `AbstractApplicationInitializer.cs`（938 行、+約 70）: Framework bootstrap orchestration。plain-file config provider の注入、Player directory 許可、graph hook、論理初回 Scene の明示 load、失敗 cleanup。App が所有し `Application.quitting` まで。既存 500 行警報は起動順序の同一所有者による部分変更であり、Parser / I/O を同ファイルへ増やさず別クラスに置く。新 protected hook は限定的に宣言し、Editor/Addressables 既定を保つ。
- 新 `Runtime/Config/RequiredJsonFileConfigProvider.cs`（約 90 行）: file I/O と JSON flatten。起動一回の短寿命、`IConfigProvider` 実装。Unity Editor 依存なし。欠損・不正 JSON を明示失敗させる。pure path/field validation は別にテスト可能にする。
- 新 `Runtime/BuildContent/PlayerContentConfiguration.cs`（約 130 行）: schema / target / relative path / identity / first scene / representation の純粋な検証・resolve。外部公開は最小、`App` 寿命の immutable 値。Unity API と Editor API を使わない単体検査境界。
- `AppInitializer.cs`（310 行、+約 55）: SampleGame の path と graph resource を選ぶ composition root、Player smoke hook。App 寿命。Framework→Game の依存は追加しない。
- `SampleGame/DependOnAll/Editor/Build/SampleGameContentBuild.cs`（94 行、+約 15）: content build result を返す選択入口を増やし、Player への接続を提供。project の季節 policy を維持する。
- 新 `SampleGame/DependOnAll/Editor/Build/PlayerBuildInputValidator.cs`（約 140 行）: report JSON と request の純粋照合。Editor I/O なしで path / metadata の整合をテスト。SampleGame Editor 内部。
- 新 `.../SampleGamePlayerBuild.cs`（約 230 行）: Unity Editor adapter と build lifecycle の単一 owner。`BuildPipeline.BuildPlayer`、一時 generated Resources/config、Addressables 自動 build 抑止、PlayerSettings 復元、stage/publish。I/O を含むため fake port で失敗経路をテスト。予想 3 責務になるなら `PlayerBuildPublisher` へ分割し、Phase A revision で責務配置を再確認する。
- 新 `.../PlayerBuildReportVerifier.cs`（約 100 行）: build report の Scene GUID 交差と file list、receipt 生成。Editor API はこの adapter 側に閉じ、policy は pure set 比較へ分離。
- Unity Editor tests（新 200〜350 行）: config/path/report 失敗と fake publish、GUID dedup。Player 実 build / run の harness は Phase C 用であり、fixture 生成以外を全 EditMode 本体へ埋め込まない。

新 asmdef 参照は予定しない。新規生成 asset は BS4 所有 path と marker を持ち、既存 production scene/prefab/Addressables 設定の永続変更をしない。想定実装差分約 900〜1200 行の高リスク slice として独立レビューを行う。

### 実装順序と Phase B 停止規則

1. 純粋 config / report validator と failure tests。2. Runtime bootstrap hooks と SampleGame 配線。3. Player adapter / staging / dedup / receipt。4. 実 fixture harness。5. Editor compile と contract audit。Phase B はテスト・Build を実行しない。

map copy の直接依存が content Scene を Player へ取り込む、`previousBuildReportDirectories` が現行 Unity API で型保持できない、新 asmdef edge・公開 API・owner / 寿命の追加が必要、既存 SceneDirector に graph 形式の変更が必要と判明したら Phase A revision へ戻す。失敗を Player 成功へ読み替えない。

### テスト計画

- 差し戻し中の起点 filter: `PlayerContentConfiguration` / `PlayerBuildInputValidator` / `PlayerBuildReportVerifier` の EditMode、BS3 の `ContentDirectorySessionTests` と `ContentDirectoryIndexTests`。必要に応じて違反根拠を記録して拡大する。
- 判定必須: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、最終 head の全 EditMode 回帰（空 filter）、BS3 の directory PlayMode 経路、固定 Windows x64 IL2CPP / High Player build と実行。Unity バッチテストは Editor を閉じ、初回から sandbox 外の承認済み `pwsh tools/run-tests.ps1` を使う。XML の件数・対象名を確認し、0 件を失敗とする。
- Player harness は production graph に合う初回 Scene と、同じ directory の代表 Prefab fixture を用意する。選択結果・report・Player receipt と固定 implementation head を対にし、成功 signal の前に load / release / shutdown stage を検査する。source fixture を Player 実行時に参照しない。欠損 directory、identity / target mismatch、別表現要求は非ゼロ終了で確認する。
- A2 は同一 A1 版から architecture と runtime/build failure の独立レビューを別モデルで行う。A0 だけの代替構成も独立に取得済み。A3 で採否を記録し、C' 用に A/B/C 未関与モデルを残す。

## A2 — 独立レビュー

入力は commit `f82e98f` / SHA256 `D758CBEC2091F61DF74A758A4012F52181DFFDDC74A7798F43B94D1894F3BB04` で固定した。

- A0 代替構成（Terra）: UIScene 単体では config/map の Addressables 先行依存と初回 Scene load 不足を解けない、plain config・source 非依存 graph・明示 AddScene・High/IL2CPP Player proof が必要。採用。薄い catalog 新設は現行 SceneFactory が `SceneResource` を要求するため即採用せず、複写 graph closure と BuildReport 排除検査を先に使う。
- architecture gate（Sol）: 成功時 awaitable close 不在、Editor build/publish/orchestration の責務混在、一時 Resources/StreamingAssets の crash recovery 不在、protected API 未確定を blocker。全て採用。
- runtime/build failure（Astra）: Player config provider 列に Addressables が残る、成功 close/Before failure exit 不在、GameObject だけでは content-only managed type の stripping を証明しない、Object root の重複検査不足、production + fixture 合成責務不足を指摘。全て採用。

独立性: 三担当は互いの所見を見ず、architecture/runtime は同じ A1 commit を使用。C' にはこれらと Phase B/C に未関与の担当を使う。

## A3 — 統合・凍結

program の委任に基づき上記を全件採用し、次を A1 の上書き条件として凍結する。

### 起動 state machine と公開境界

Framework に追加する protected 面は次だけとする。名称の微調整は可、意味・順序を変える場合は Phase A を再開する。

- `UseRequiredPlayerFileConfiguration`（bool）: true のとき provider 列は **必須 plain JSON → environment → command line**。既存 Addressables JSON providerを作らず、remote catalog も読まない。生成 JSON は通常 app-config の必要値（debug無効、telemetry設定、world companion set等）も含む。
- `GetRequiredPlayerConfigurationPath()` / `LoadPlayerSceneResourceMap()`: SampleGame composition root が `Application.streamingAssetsPath/bs4-runtime.json` と `Resources.Load<SceneResourceMap>(...)` を供給する。directory Player の UICommon は Scene 0 の component を必須とし、Addressables fallbackを禁止する。
- `OnPlayerContentReadyAsync(IAssetManagement, CancellationToken)` と `OnPlayerContentStartupFailedAsync(stage, exception)`: Game側 smoke policy。前者は Director が logical first scene を stable にした後に一度だけ呼ぶ。後者は Before / After の両失敗を stage付きで受ける。Editor / addressables mode は既定 no-op。
- Framework は config の `content:firstScene` を directory modeで必須とし、session install→director構築→`AddScene(firstScene)` の順に実行する。Game側は Framework state を直接変更しない。

成功 smoke owner は SampleGame `PlayerContentSmoke`（App lifetime）。順序は Prefab load→instantiate→content-only componentのserialized値/動作確認→instance destroy完了→handle release→論理Scene unload→`AssetManagement.CloseContentDirectoryAsync()`成功→success receiptをatomic write→`Application.Quit(0)`。失敗は新規 load 停止、所有済み資源を逆順解放、close/drainを試行し failure receipt、`Application.Quit(nonzero)`。`Application.quitting` / synchronous shutdown は非常時 fallbackで、success signal に使わない。外部 harness timeout / receipt欠損 / process crash は失敗。

### Editor build の責務分離と一時状態

- `SampleGamePlayerBuildCoordinator`（orchestration、約120行）: content build→input validation→project mutation→BuildPipeline adapter→verification→publish を順序付けるだけ。
- `PlayerBuildProjectMutation`（Unity Editor transaction、約180行）: 固定 path `Assets/Resources/BS4/<identity>/SceneResourceMap.asset` と `Assets/StreamingAssets/bs4-runtime.json`、PlayerSettings backend/stripping、Addressables BuildWithPlayer、definesを所有。identity marker が一致する self-owned stale stateだけを開始前cleanupし、未知/marker不一致は拒否。全設定とassetを finallyで復元し、interrupt/re-entryを fake/Editor testする。
- `UnityPlayerBuildAdapter`（約100行）: bootstrap Scene一件、DetailedBuildReport、fixed Windows64 Player、`previousBuildReportDirectories` と IL2CPP/High を確認して `BuildPipeline.BuildPlayer`。BuildPipeline port は internal fake可能。
- `PlayerBuildPublisher`（filesystem transaction、約140行）: identity別未存在 staging/final、content 一回 copy、config/receipt/outcome、同一volume rename。旧成功を変更しない。filesystem port は internal fake可能。
- `PlayerBuildReportVerifier` は selected **全 root GUID（Scene/Object）** と Player packed contents の交差を検査し、DetailedBuildReport / packed情報が空なら失敗。bootstrapとの共有 dependency は許すが selected root 自体の重複は許さない。検査集合を receipt に保存。

一時 graph は map と参照する全 `SceneResource` closure を BS4 pathへ複写し参照を複写先へ張り直す。payload `AssetReference` は locator として残す。selected Scene/Object root GUID が Playerへ混入した時点で Phase A revisionへ戻し、薄い catalogを設計する。null/external graph定義は build前に拒否する。本番 assetを編集しない。

### fixture と code 保持

BS4専用 Editor fixture adapter が production materialization snapshot に一つの Prefab candidateを合成する。ownership marker、logical key `bs4:fixture:prefab`、requested representation、期待 serialized token を固定し、production Description型は追加しない。Prefabは bootstrap/map/Resourcesから参照されない `Bs4ContentOnlyProbe` componentを持つ。smokeは component型をcompile-timeで直接参照せず、component名とserialized token、SendMessageで動作を観測し、偶然の静的root化を避ける。`previousBuildReportDirectories` の型情報、Player stripping report、実Player挙動を保持証拠とする。明示link.xmlは実 buildで不足が判明した型だけ追加する。

論理初回 Scene は Spring Full build の `Title`（graph上の関係と選択存在をpreflightで検証）とする。別表現要求は専用 failure run で `EntryAmbiguous` 等の構造化失敗を確認する。

### failure / cleanup table

- config前: sessionなし。failure receiptと非ゼロ終了。
- register後 / install前: session `CloseAsync`。
- install / director後: Director dispose、AssetManagement close。
- first scene後: SceneDirector正式 unload後にclose。
- fixture load/instance後: instance→handle→scene→closeの逆順。
- Editor build mutation中: self-owned asset/settingsをfinally復元。異常終了後は次回marker一致時だけ回収。
- publish中: stagingのみcleanup可能、final/過去成功は不変。receipt完成前は成功扱いしない。

### 凍結後の境界

新 asmdef edgeは追加しない。Editor型はinternal。通常 Player / EditorのAddressables既定を維持する。新しい graph abstraction、公開API、owner、永続schema、fixture合成方式が上記で成立しない場合、Phase B内で代案を決めずPhase A revisionへ戻す。A3後の例外承認はなし。

### Phase A revision 2 — graceful close ownership（2026-09-19）

Phase B runtime commit `3e667d8` 後、SampleGame callback が受け取る `IAssetManagement` には directory close がなく、実装型の `CloseContentDirectoryAsync` は Framework internal であると判明した。SampleGameへ public close APIまたは friend accessを追加すると、session ownerをFrameworkに固定したBS3契約とA3に反する。この finding は「成功 receipt 前の awaited close」という最低条件を阻害するため Phase A を再開した。

採用する ownership は次のとおり。

1. `OnPlayerContentReadyAsync` は代表 content の load / instantiate / component動作確認 / instance destroy / handle releaseだけを担当し、成功なら戻る。directory sessionやSceneDirectorを所有しない。
2. Framework `AbstractApplicationInitializer` が callback成功後、起動した logical first Scene を `SceneDirector.UnloadScene` の正式経路で unloadし、次に実装型 `AssetManagement.CloseContentDirectoryAsync()` を awaitする。session fieldをnullにした後で `OnPlayerContentShutdownCompletedAsync()` を呼ぶ。
3. SampleGame は completion hookで success receiptをatomic writeし `Application.Quit(0)`。failure hookはFrameworkが逆順cleanup / awaited closeを試した後に呼び、failure receiptと非ゼロexitを行う。close自体が失敗した場合もsuccess completionへ進まない。
4. Frameworkは `IAssetManagement` や `AssetManagement` のpublic面を増やさない。追加protected hookは completion通知だけで、resource owner操作を派生へ公開しない。Editor/addressables modeはno-opで従来どおり継続する。
5. BeforeSceneLoad failureは例外を保持し、AfterSceneLoad callbackからfailure hookを非同期起動する。failure hookがreceipt/exitを完了するまでFrameworkが別の起動処理を開始しない。

独立再レビュー: revision 1 の architecture担当（Sol）は、Framework が既に concrete AssetManagement / session / Director / first scene を所有するため、public API・friend・asmdef edge無しで実行可能として PASS。runtime担当（Astra）も順序を PASS とし、次を必須実装条件として追加した。

- directory Player の `OperationCanceledException` は成功扱いにせず、逆順cleanup、failure receipt、非ゼロexitへ送る。通常 Editor/addressables shutdownの既存意味は維持する。
- fixture callbackは失敗時も `finally` で instance destroy完了とhandle releaseを済ませてからthrowする。live tokenをFramework closeへ残さない。
- Before failure例外は部分 `ReleaseAll` で消さず、failure hook通知後または次のSubsystemRegistrationで消す。
- shutdown stageを scene unload / directory close / completion receipt に分け、close成功後だけsession fieldをnullにする。二次cleanup失敗は元のstage/例外を隠さない。

主担当は全て採用。revision 2 を凍結し、他のA3条件は変更しない。

### Phase A revision 3 — fixture composition boundary（2026-09-19）

Phase Bで、`BuildMaterializationSnapshot` と `BuildPlan` の constructor がそれぞれ別assemblyのinternalであり、SampleGame Editorのfixture adapterはproduction結果へcandidateを正規合成できないと判明した。projectionはplan/snapshotの完全対応を要求し、Season policyは全candidateにScene graph nodeを要求するため、reflectionやproduction graph改変で迂回しない。この finding は代表非Sceneを同一directoryでPlayer検証する最低条件を阻害するためPhase Aを再開した。

採用案: `OneStarMaker.Build.Selection` と `OneStarMaker.Build.Materialization` の各 `AssemblyInfo.cs` に `[InternalsVisibleTo("SampleGame.DependOnAll.Editor")]` を追加する。SampleGame Editor内の `Bs4FixtureComposer` が既存結果を列挙し、新fixture candidate/tag/requirement/dependencyを加えた新snapshotと、同じcandidateをselectedへ加えた新planをconstructorで再構築する。

- asmdef参照は既にSampleGame Editor→両Framework Editor assemblyに存在し、新edgeは作らない。Runtime/Game→Frameworkの方向を変えない。
- friendはEditor composition root一assemblyに限定し、Runtime/public APIを増やさない。Framework側にproject固有fixture語彙を置かない。
- composerはproduction plan/snapshotを変更せずimmutableな新結果を返し、同一stable/logical/physical key、requirement、tag、closureの衝突を明示検査する。fixtureは常にselectedで、projectionが全対応を再検証する。
- friend面の利用はこのcomposer一箇所に限定する機械検査をPhase Cへ加える。汎用source合成APIは非Scene本番需要の後続sliceへ送り、BS4では公開しない。

代替のContent coordinatorへのfixture特例、reflection、Scene graphへの偽node、constructorのpublic化は不採用。責務または公開面を広げ、production policyをfixture都合で汚すため。

revision 3独立再レビューはarchitecture担当（Sol）、runtime/build担当（Astra）ともPASS。次を必須実装条件として採用する。

- composerはstable/logical/root physical GUID、requirement logical key、dependency root GUIDの所有衝突とtag dimension/value競合を先に拒否する。共有依存はGUID/path一致なら許可する。
- production request / selected / excluded / provenance / tags / requirements / closuresを保持し、Player要求representationのfixture一件だけをselectedへ追加する。composer自身がfixtureの常時採用とExactlyOneを保証する。projectionは対応・閉包を再検査するがselection policyを再実行した証拠にはしない。
- reportはfixtureをBS4 composerによる明示追加と記録し、Season policyが非Sceneを選択したとは主張しない。
- friend利用箇所は `Bs4FixtureComposer` だけであることをPhase Cの機械検査で確認する。

主担当は全て採用しrevision 3を凍結。他の凍結条件は変更しない。

## Phase B / C / C' / D

各 Phase の実績、固定 commit と証拠、未実行事項、判定を順次記入する。Phase D のマージ判断はユーザーへ渡す。
