# BS5b-2 宣言からの Play 経路

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `B`（Phase B 完了・動作確認完了、2026-11-03。次は発見 C）
- branch: `claude/bs5b2-purpose-play-routes`
- implementation base commit: `cad03983`
- implementation head commit: `eb3df5fa`
- risk: `high`（Runtime の起動（`AbstractApplicationInitializer`）の Editor directory 経路に初回 Scene を足す。Editor Play の process 環境変数を宣言から置き、既存の Delivery 窓の ledger を共有にする）
- owner: 発注者 / BS5b-2 担当
- created: 2026-11-01（BS5b を 2026-11-02 に BS5b-1 と BS5b-2 に分けた）
- expires: BS5b-2 の Phase D 完了時
- harvest to: Architecture §18 §4（「SampleGame の宣言と Editor build」「BuildSystem の後続」）、§04 §4.6 / §4.7 / §4.8（Editor directory Play の初回 Scene、Play As の config の置き方）、[BuildSystem program §7](BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md#7-buildsystem-の他の系列の所在) の BS5b-2 行
- workflow task: 未 start
- Phase A snapshot の取得案内・状態: 生成しない（A3 凍結版に添える機械向けの期待値・照合 script が無い。§5）
- Phase B result snapshot の取得案内・状態: 生成しない（実装と §6 は引き継ぐ commit にあり、添える機械向けの期待値・照合 script が無い）
- evidence bundle の取得案内・状態: 未到達
- C' blind bundle の取得案内・状態: 未到達
- 引継ぎ票: **動作確認完了（2026-11-03）。次は発見 C。** 凍結した Phase A は revision 3（A3 凍結版 `c6dc8546`）。implementation base / head は上の欄を正とする（head は Phase B の `eb3df5fa`。この記録 commit は HANDOFF だけを変え、head を動かさない）。引き継ぐ commit はこの票を含む commit（SHA は起動指示で渡す）。動作確認の結果: AC17 (a)(c)(d)(e) と §5「未知の操作経路」の 2 点を `eb3df5fa` で各 1 回行い、期待との差なし、未実施なし（§6「動作確認」）。実効 config と停止後の環境は `eval` で読めたので、B 適応の観測ログは足していない。次の作業: 発見 C（implementation base / head の差分を固定し、構造・契約・凍結済み失敗経路を先に見る。GO 判定と C' の起動はしない）。判定 C の実行担当への注意は §6「動作確認」の末尾（Play への移行中に送った `eval` の timeout が Console の Error として区間に入る）。snapshot・bundle は無い（実装と記録は引き継ぐ commit にある）。Editor を開くと消える `MobileDependencyResolver` の `.pdb.meta` 5 件は実装の差分ではない（§6）。

### PR 状態ブロック

PR を作る前は §0 を唯一の正本とする。

---

## 1. 目的と対象外

- **目的:** BS5a が作った宣言（`BuildPurposeDeclaration`）を、日常 Play・成果物の検証 Play・published directory の受け取りの各参照者の操作に接続する。宣言 1 つを選べば、その操作に必要な mode・表現・runtime 既定値・成果物が手入力無しに揃い、揃わない時は黙って別の経路で動かずに拒否される。
- **分割（2026-11-02）:** BS5b は、成果物を作る側（BS5b-1（HANDOFF は PR #155 のマージで削除。最終版は commit `95e4a270` の `docs/handoff/BS5B1_FULL_BUILD_AND_BATCH.md`）：`Full` の build・publish、staging、batch）と、本スライスに分けた。本スライスは BS5b-1 の `Full` の build・publish と batch の結果 file（`last-result.json`）の上に乗る。A2 は分割前の BS5b 全体で行い、AC・I・E・D の番号は分割前のまま残す（A2 の記録と照合できるように）。
- **初回 Scene の責務（発注者、2026-11-03、revision 2）:** 初回 Scene は、その目的で Build・Play する時の起動地点として App 開発者が目的ごとに決める。Framework は目的の意味を知らず、App から渡された起動 Scene を汎用にロードする。今の `Title` は今の目的群に対する App の設定値で、Framework の固定の仕様でも全目的に共通の不変条件でもない（D12）。
- **対象外:**
  - BS5b-1 の範囲（build mode、publish の `Full` 必須、staging の path、batch 入口）。
  - **d5b（S-4d5b）の実証:** Planner profile で Environment を読まずに Content Directory の Whitebox 上で Event が動くこと。d5b は今日と同じ手順（環境変数と profile を手で置く source の Play）で進め、BS5b を待たない（[S-4 program §6.3](S-4_FULL_SPEC_WORLD_AUTHORING.md)）。本スライスの受け入れ条件は Planner 宣言の Event の動作を判定しない。
  - **identity 後続**（[BuildSystem program §7](BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md#7-buildsystem-の他の系列の所在)）。宣言の照合は manifest が記録する欄までにする（D10）。
  - Player の実行時差し替え、複数 directory の同時登録、設定の宣言化と Player の Debug の剥離、known-good の pin の呼び手、日常 Play で宣言に無い content を読んだ時の検出（UC11）（Architecture §18 §4「BuildSystem の後続」）。
  - R2・HTTP からの取得、DIST の install と cache の契約の変更。
  - directory mode の Editor Play で flight rig が spawn に置かれない観測の切り分け（Architecture §18 §4「検証の残り」）。
  - Addressables の退役、旧 Variant profile の整理。
- **現況:** §1.3（A0 は `6fe18810` で確認。A3 で BS5b-1 のマージ後の `cad03983` で取り直した）。

### 1.1 Phase A の判断基準（発注者。S-4 program §0 と BS5a の Phase A から転記）

- **主目的は BuildSystem。**
- **残すコードは SampleGame に必要なものだけ。** 生成や authoring の道具は不要なら一時スクリプトで作り commit の前に消す。
- **参照者は 4 者。** Editor（日常）・Editor（成果物の検証）・Player・別 PC / Cloud が、それぞれ「必要なものをどう知るか」「何を持っているか」「足りないものをどこから得るか」を持つ（§1.4）。判定は難しくしない。
- **実ユースケースで通す。** §1.5 のユースケースの列が成り立つことを確かめる。
- **手入力を減らす。** 宣言や manifest から一意に導ける値は機械が決め、利用者は目的と取得元を選ぶことに集中できるようにする（外部レビュー 2026-11-02、D11）。

### 1.2 常時契約と program から転記した制約

- Game → Framework の一方向。Framework は season 名、Cell 座標文法、職種 role、`CellCompanionSet` を知らない。宣言の runtime 値は Framework にとって不透明な key=value。asmdef 参照は A3 で決めた 1 本（`SampleGame.DependOnAll.Editor` → `OneStarMaker.Foundation`、#47）のほかに足さない（足す必要が出たら A へ返す）。
- `SceneState` の 14 値と `LoadType` を変えない。
- Editor コードを Runtime アセンブリに置かない。Unity 側の `.cs` は `#nullable enable`、`record` と `init` accessor を使わない、破棄されうる `UnityEngine.Object` は `== null` で判定する、公開 API のログ抽象は `ILogger<T>`。
- path は `tools/path-budget.json` の予算に従う（repo 相対 150 文字。ルート + 150 ≤ 259）。
- `unity test` / `unity run` を使わない。Unity テストは `pwsh tools/run-tests.ps1`。Phase B は Editor 操作と compile 確認まで、Unity テスト・content build・Player build・batch の実行と判定は Phase C。Scene / asset YAML の手編集は禁止。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。

### 1.3 A0 の現況（`6fe18810`。BS5b-1 の分は A3 で `cad03983` に取り直した）

BS5a 以前の事実のうち BS5a が変えていないものは BS5a の A0（`git show b4ae3d78^:docs/handoff/BS5A_PURPOSE_SELECTION.md` §1.3）で確認済みで、ここでは本スライスの判断に要るものだけを再確認して転記する。

**宣言（BS5a 後）。** `SampleGameBuildPurposes` が 6 宣言（`all-full`、`spring-full`、`spring-whitebox`、`spring-full-whitebox`、`bs4-spring-full`、`spring-planner-whitebox`）を持つ。欄は `Name`・`Roots`・`Filter`・`Representation`（優先順と `First` / `All`、主値 `Primary`）・`RuntimeDefaults`（`world:cellCompanionSet` だけ。`content:` で始まる key は拒否）。宣言は `BuildContentRoot.Declaration` と transport manifest の `declaration`（名前・表現の主値・`RuntimeDefaults`。どちらも任意 field）に写る。directory mode の起動は root の宣言を config と突き合わせ、違いを `ContentDeclarationMismatch` の Warning 1 行で出す（Architecture §04 §4.7）。BS4 coordinator の `firstScene = "Title"` は文字列の直書き（`SampleGamePlayerBuildCoordinator.cs:116`、Player の config の初期化子）。

**Editor の日常 Play（UC1・UC2）。** 未指定の Editor Play は Delivery の verified pair が無ければ起動失敗する契約で、source を読むのは明示 `content:runtimeMode=addressables` だけ（§04 §4.7、BS5a の I8 で維持）。表現は Editor の active Variant profile（`DeveloperVariantSettings`、`UserSettings/OneStarMaker/` に永続）が config の `assets:sceneVariant` より優先し、profile 未選択（resolver が `null`）の時だけ config を読む（§04 §4.8）。companion set は `world:cellCompanionSet`。config は `app-config.json` → 環境変数（prefix `SAMPLEGAME_`、`__` を `:` に写す。`AppInitializer.GetEnvironmentVariablePrefix`）→ command line（`CommandLineConfigProvider`、`--a.b=v` を `a:b` に写す。`AbstractApplicationInitializer.BuildConfig`）の順に重なり、後が勝つ。`CommandLineConfigProvider`（public）は `OneStarMaker.Foundation`（全 platform）にあり、`SampleGame.DependOnAll` は Foundation を参照するが `SampleGame.DependOnAll.Editor` は参照しない（A3 の #47 で足す）。Planner の Play は今、mode・profile・companion set の 3 つを手で揃える。Editor Play は開いている Scene を初期登録の入力にする（§04 §4.6）。`app-config.json` は `JsonFileConfigProvider` が Addressables の TextAsset として読む（address が `Assets/SampleGame/Config/app-config.json`）。Addressables の Play Mode（Use Asset Database か既存 build か）は project の asset ではなく利用者ごとの Editor 設定にある。project は Enter Play Mode Options で Play に入る時の domain reload を切っている（`ProjectSettings/EditorSettings.asset` の `m_EnterPlayModeOptions: 1`）。Play 中の script の再 compile では domain reload が起きる。

**検証 Play（UC4）と Delivery 窓。** Delivery 窓（`Tools/OSM/Content/Delivery`）は source の種類（Published directory / HTTP / Installed revision root）と、manifest SHA-256・contentSet・revision・cache root（既定は空）・representation（既定 `Full`）を手で入れ、Prepare（`ContentInstaller` で DIST cache に install、または installed の再検証）→ Use For Next Play（`ContentDeliveryPlayBridge.Apply`）する。窓は宣言を表示せず、representation も宣言から取らない。bridge は `SAMPLEGAME_CONTENT__` の 5 key（`RUNTIMEMODE=directory`、`INSTALLEDREVISIONPATH`、`MANIFESTSHA256`、`BUILDIDENTITY`、`REPRESENTATION`）を process scope に置き、以前の値と置いた値を `SessionState` の ledger（`OSM.ContentDelivery.PlayBridge.v1`）に残す。Reset は自分が置いた値のままの key だけを戻す。Play の終了で自動では戻さない。`world:cellCompanionSet` は置かない。DIST の install 先は `<cacheRoot>/installed/<contentSet>/<revision>/content/...`（`ContentCacheStore.cs:95`）。

**開いている Scene の再利用。** Scene 本体は `PerformUnitySceneLoad` が `SceneManager.GetSceneByName` でロード済みの Scene を見つけるとロードせずに使う（`SceneDirector.Loading.cs:631`）。`RegisterAlreadyLoadedScenes` はロード済みの Scene のうち名前が SceneResourceMap にあるものを `SceneDirector` へ登録する（`AbstractApplicationInitializer.cs:812-829`）。directory に入る SceneResourceMap は `Assets/OneStarMakerCommon/SceneMap/SceneResourceMap.asset` そのもので、宣言の選択では絞られない（`SampleGameBootstrapContentComposer.cs:29`。他季節の `Summer_Cell_0_0` も map にある）。Variant の Scene は元の Scene と同じ名前（例 `Spring_Cell_0_4/Variants/Whitebox/Spring_Cell_0_4.unity`）。UICommon は `LoadUICommonAsync` が active scene の名前が `UICommon` の時にその Scene から取る（`AbstractApplicationInitializer.cs:510-524`）。directory mode でも、Editor で開いている source の Scene がそのまま使われる。BS5a は検証 Play の操作手順で閉じることを要求しただけで、機械の拒否は無い。

**空 Scene からの directory Play。** Editor directory 経路の `ContentDirectoryConfiguration.FirstScene` は常に空（`AbstractApplicationInitializer.cs` の `ReadContentDirectoryConfiguration`、Editor 分岐）で、初回 Scene をロードするのは Player の baked `firstScene` だけ。しかもその分岐（同 file の `load-player-first-scene` 以降）は初回 Scene の後に Player の smoke（代表 Object の検証、初回 Scene の unload、directory の close、receipt）まで続け、失敗経路も `FirstScene` が空でなければ初回 Scene を unload する（`:420-434`）。だから開いている Scene を閉じた（空 Scene の）directory Play は、UICommon・SceneResourceMap・SceneDirector の構築までは走るが、初回 content Scene をロードしない。BS5a の AC13 は判定の操作で `SceneDirector.AddScene("Title")` を手で呼び、Title の Start の event を発火して進めた（BS5a §7.7、後続の入力）。

**宣言と config の不一致の Warning の配線**には自動テストが無い（BS5a の Editor Play と BS4 Player のログで観測済み。Architecture §18 §4「検証の残り」）。

**BS5b-1 が足したもの（`cad03983` で確認）。** BS5b-1 は上の宣言・bootstrap・`ContentDeliveryPlayBridge`・Delivery 窓・`AbstractApplicationInitializer` を変えていない（`6fe18810..cad03983` で差分なし）。

- build: `BuildContentRequest` の mode（`BuildContentMode.Incremental` / `Full`）と `outcome.json` の `buildMode`。menu の build は `Incremental`（`SampleGameContentBuild.MenuBuildMode`）。`SampleGameContentBuild.BuildResult(declaration, mode)`（internal）は選択・build の失敗を例外にし、`BuildUnchecked` は coordinator の失敗を result のまま返す。
- publish: `ContentTransportPublisher.Publish(result, publishRoot)` は outcome の file の `buildMode` が `Full` でなければ、publish 先と staging を作る前に拒否する。publish 先が既にあれば拒否する。staging は `<publish 先の親>/.staging/<guid32>` で、揃ってから rename で公開する。戻り値は公開した directory の path。
- batch: `SampleGameContentBatch.BuildAndPublish`（`-osmPurpose <宣言名>`、Full だけ）。publish 先は `artifacts/bs2b/transport/<identity>`。成否の正は process の終了コード（0 成功、1 想定外、2 引数、3 選択、4 build、5 publish）。結果 file `artifacts/bs2b/batch/last-result.json` は、成功なら `SuccessFile`（`identity`・`publishPath`（絶対 path）・`manifestSha256`（`transport.json` の SHA-256、小文字 hex）・`declaration`（宣言名）・`buildMode`・`exitCode`=0）、失敗なら `FailureFile`（`exitCode` と `reason` だけ）。どちらも `SampleGameContentBatch` の internal な入れ子の型で、本スライスの読み手と同じ asmdef にある。method の最初に前回の file を消すが、Unity がこの method を呼ぶ前に終わる（project が別の Editor で開かれている、compile error）と前回の file が残る。
- 既知の制限: 成功した batch の後に Unity process が正常に終了することは BS5b-1 で示せていない（BS5b-1 の最終版 HANDOFF §7.7、`95e4a270`）。

### 1.4 参照者と経路（BS5a の表を本スライスの列で更新）

| 誰 | 必要なものをどう知るか | 持っているもの | 足りないものの経路 | BS5a 後（現況） | BS5b-2 後 |
|---|---|---|---|---|---|
| Editor（日常） | 開いている Scene graph と、選んだ宣言の runtime 既定値 | AssetDatabase の source（明示 addressables の経路） | （後続）LAN / R2 の object | 手で mode・profile・companion set を置く | 宣言を選んで Play する（Play As） |
| Editor（成果物の検証） | 宣言 | installed directory | なし。fail-closed | menu の `Build` と Delivery 窓の手入力で、封じた directory の検証 Play ができる | 宣言 1 つから build → publish → install → 検証 Play |
| Player | baked の宣言 | 内包 / installed | （後続）R2 の object の差分 | BS4 Player に付随 asset と宣言の既定値が内包される | 変えない |
| 別 PC / Cloud（受け取る側） | published directory の manifest の宣言と、batch の結果 file | published directory、または cache | （後続）R2 の object | manifest が宣言を持つ | From published directory |

### 1.5 ユースケース（本スライスの行）

| # | 誰が何をする | 今（BS5a 後） | BS5b-2 後 |
|---|---|---|---|
| UC2 | 企画者が Planner profile（Environment 無し）で Play | mode の環境変数・表現の profile・companion set の環境変数を手で揃える。d5b はこの手順で進める | Play As（宣言を選ぶと、source の Play に mode・表現・companion set が置かれる。合わない profile は拒否） |
| UC4 | 担当が「この目的の成果物だけで動くか」を Editor で確かめる | 窓に手入力し、宣言の Scene と UICommon を手で閉じ、空 Scene では初回 Scene を手で足す。Addressables には抜けない | 1 操作（宣言 1 つから build → publish → install → 検証 Play）。起動が再利用しうる source の Scene が開いていれば拒否（menu からは閉じて続けられる）。初回 Scene は directory から起動する |
| UC7 | 別 PC が published directory を受け取った後、Editor で検証 Play | 窓に手入力 | From published directory（batch の結果 file を選び、manifest SHA-256 の期待値はそこから。published directory は結果 file から、手元に無ければ folder の選択。contentSet・revision・表現・runtime 既定値は manifest と宣言から。D11） |

UC6 は BS5b-1。UC1・UC3・UC5・UC8〜UC11 は BS5b で変えない（BS5a の A0 §1.5 のまま）。

---

## 2. 意思決定と受け入れ境界（A1）

- **このスライスが答える問い:** 宣言 1 つを選ぶだけで、日常 Play・成果物の検証 Play・published directory の受け取りが、手入力の設定無しに宣言どおりの mode・表現・runtime 既定値・成果物で動き、宣言と食い違う状態（合わない profile、開いている source の Scene、残った他の設定、コマンドラインの競合）では黙って別の経路で動かずに拒否されるか。

### 2.0 証明の範囲

| 主張 | 何で示すか | 示さないこと |
|---|---|---|
| Play As が宣言どおりの config を置き（制御する key の実効値。profile・外の値・コマンドラインの競合は拒否）、Play の後に戻す | AC1〜AC4（変換と ledger の単体・EditMode）、AC17（操作） | source の Play で宣言に無い content を読まないこと（UC11、後続） |
| 検証 Play の 1 操作が宣言どおりの directory を作って使い、App がその宣言に決めた初回 Scene から起動し、起動が再利用しうる source の Scene が開いていれば拒否する | AC5〜AC9、AC17、AC20 | 示すのは初回 Scene の Stable まで。目的に固有の content のロード、到達可能な全 Scene のロード（総当たりしない）、Planner 宣言の Event の動作（d5b）は示さない。初回 Scene から目的の content に到達できるよう宣言と App を構成するのは App 開発者の責務（D12） |
| published directory を受け取って検証 Play できる | AC15、HC2 | R2・HTTP の取得、別 PC の実機（同じ PC の別 checkout で代える）、manifest に記録されない宣言の欄（`Roots` 等）と source の同一性（identity 後続）。結果 file が最新の batch の結果であること（Unity が batch の method の前に終わると前回の file が残る。受け取りは結果 file と directory の組の一致を照合するだけで、組が古くても一致していれば受け取る。§2.5 の batch の起動 wrapper）。発行側と同じ初回 Scene で起動すること（manifest は初回 Scene を記録せず、受け取る側の App の定義を使う。App の版が違えば定義が違いうる。D12） |
| 宣言を足しても選べる | AC19 | menu を宣言から生成すること（§2.5） |

### 2.1 不変条件（起きてはいけないこと）

- **I1（宣言と食い違う Play をしない）:** Play As と検証 Play は、置いた config と宣言（表現の主値・runtime 既定値・mode）が一致しない状態で Play に入らない。active Variant profile が config より優先して表現を変える状態（profile が選ばれている）、置くべき key が自分の ledger 以外の値で既に process 環境にある状態、置くべき key が Editor process のコマンドライン（config の最上位の層）で与えられている状態では、Play に入らずに理由と key を示して止まる。保証するのは、この経路が制御する key の実効値が宣言どおりであることまでで、制御しない key の値は保証しない。
- **I2（source に抜けない）:** 検証 Play（1 操作・From published directory）は、起動が再利用しうる source の Scene が Editor でロード済みの時は Play に入らない。対象は起動の再利用の経路と同じにする: 名前が SceneResourceMap にある Scene（`RegisterAlreadyLoadedScenes` が登録し、`PerformUnitySceneLoad` が名前で再利用する。宣言の選択の内か外かを問わない）、名前が `UICommon` の Scene（`LoadUICommonAsync` が再利用する）、`UiCommonPath` の Scene。SceneResourceMap に無い Scene と untitled の空 Scene は起動が使わないので通す。Play As（source の Play）にはこの制約を課さない。
- **I3（設定を残さない）:** Play As と検証 Play が置いた process 環境変数は、Play を要求した後、Play に入って Edit mode に戻った時点、または Play に至らずに Edit mode に留まったと分かった時点（要求が受理されなかった、Play への移行が中断した）で、置く前の値へ戻る。Play 中・Play への移行中の domain reload を挟んでも戻る。Play を要求する前に段が失敗した時は、その場で戻してから止まる。他者が後から変えた値は巻き戻さない（既存 bridge の規則）。User / Machine scope に書かない。Delivery 窓の手入力の Use For Next Play は従来どおり手で Reset するまで残す（I6）。
- **I5（失敗を成功と見せない）:** Editor の 1 操作は、Play を要求する（`EnterPlaymode`）より前の段で失敗すれば Play を要求しない。Play を要求した後の起動の失敗（初回 Scene のロードの失敗等）は既存の起動の失敗のログで検証の失敗と判定し、別の Scene へ逃がして成功と見せない。起動後の失敗のために新しい lifecycle や receipt は作らない。
- **I6（既存の契約を変えない）:** 明示 `addressables` だけが source の口、未指定は verified pair が無ければ失敗する mode の契約（§04 §4.7）、directory mode の Addressables 封鎖、宣言の不一致 Warning、BS4 Player の baked config と smoke の段の順序、DIST の install 配置、既存の Delivery 窓の手入力経路は変えない。
- **I7（Framework は語彙を知らない）:** Framework 側に足すもの（Editor directory の初回 Scene）は season 名・Cell 座標文法・職種 role・`CellCompanionSet`・`SAMPLEGAME_` prefix・`Title` を参照しない。asmdef 参照を変えない。

### 2.2 成立条件（起きなければならないこと）

- **E1（Play As）:** 事前条件（Variant profile が未選択、Delivery 窓の ledger が無い、Editor のコマンドラインが置く key を与えていない、compile error が無い）を満たす時、`SampleGameBuildPurposes.All` の宣言のどれかを選んで Play As すると、その Play の config が `content:runtimeMode=addressables`、`assets:sceneVariant` = 表現の主値の Variant（`Full` は空、`Whitebox` は `Whitebox`）、宣言の `RuntimeDefaults` の各 key=value になり、Edit mode に戻ると置く前の値に戻る。
- **E2（検証 Play の 1 操作）:** 宣言 1 つを選ぶ 1 操作で、`Full` の content build → `artifacts/bs2b/transport/<identity>` への publish（BS5b-1 の経路）→ DIST cache への install → bridge（content の 5 key と宣言の `RuntimeDefaults`、表現は宣言の主値）→ Play が順に行われ、空 Scene から、App がその宣言に決めた初回 Scene（D12。今の SampleGame の値はどの宣言も `Title`）が directory から Stable になる。bridge は初回 Scene も置く。起動ログに封鎖の 1 行があり、宣言の不一致 Warning が無い。
- **E5（From published directory）:** batch の結果 file（`last-result.json`。published directory とは別の経路、CI の出力や渡した人から受け取る）を選ぶと、manifest SHA-256 の期待値と build identity がそこから決まり、published directory は結果 file の `publishPath` が手元にあればそれ、無ければ folder の選択で決まる。そのうえで、contentSet・revision は manifest から、表現と runtime 既定値は manifest の `declaration` から、初回 Scene は manifest の宣言名で引いた手元の宣言に App が決めた値（D12）から決まり、install → bridge → Play で空 Scene からその初回 Scene が Stable になる。宣言の無い manifest と結果 file の無い directory は、従来の Delivery 窓の手入力経路で受け取る（窓は変えない。窓の宣言対応は A3 で後続へ送った。§2.5）。
- **E7（利用者が選ぶもの）:** 標準の操作で利用者が選ぶ・入れるものは §2.4 D11 の表のものだけで、それ以外は宣言・manifest・既定値から決まる。

### 2.3 受け入れ条件（検証する条件を併記）

単体 = Unity に依存しない純 C# テスト（Editor asmdef の pure class を含む）。EditMode = AssetDatabase・`SessionState`・合成 fixture を使うテスト。操作 = Phase C の実行。HC = 高コスト証拠（§5）。

| AC | 内容 | 条件 | 種別 |
|---|---|---|---|
| AC1 | 宣言から Play As の config 集合を作る変換が、6 宣言それぞれで `content:runtimeMode=addressables`・`assets:sceneVariant`（主値 `Full` → 空文字、`Whitebox` → `Whitebox`）・`RuntimeDefaults` の全 key=value を返す。環境変数名は prefix + key の `:` を `__` に写したもの（例 `SAMPLEGAME_WORLD__CELLCOMPANIONSET`）。`Full` / `Whitebox` 以外の主値は変換が失敗する。主値 `Full` の空文字は、process の環境変数では「key を置かない」と同じになる（`Environment.SetEnvironmentVariable` は空文字で key を消す）ので、変換は `Full` を「`assets:sceneVariant` を置かない（実効値は下位の層）」として返す（AC2 の下位の層の検査と組む） | E1 | 単体 |
| AC2 | Play As は active Variant profile が選ばれている（resolver が非 `null`。`Production` の `""` を含む）時に拒否し、process 環境を変えない。置く key が ledger に無い非 `null` の値で既にあれば拒否し、key 名を示す。負例: 環境に `SAMPLEGAME_WORLD__CELLCOMPANIONSET=Full` が外から置かれた状態で Planner 宣言を Play As → 拒否、環境は変わらない。コマンドラインの検査: Editor process の引数を既存の `CommandLineConfigProvider` で読んだ結果（`SampleGame.DependOnAll.Editor` に `OneStarMaker.Foundation` の参照を足して Editor 側から直接使う。#47）に、置く key（大文字小文字を区別しない）があれば拒否し、key 名を示す（引数は注入できる形で受け、テストでは合成の引数を渡す）。負例: 引数 `--content.runtimeMode=addressables` がある状態で検証 Play → 拒否、build しない。Play As も compile error 中は拒否する（AC6 と同じ理由）。主値 `Full` の宣言の Play As でも `assets:sceneVariant` は制御する key で（環境には置かない）、外の process 環境とコマンドラインの検査はこの key にも掛かり、どちらかにあれば拒否する。そのうえで下位の層の `app-config.json`（`AppInitializer.GetConfigFilePath` の `Assets/SampleGame/Config/app-config.json`）の同じ key が空でなければ拒否する。上位の 2 層を拒否で除くので残る層は app-config だけで、その空が実効値の空になる（今の値は空。file の内容は注入して確かめる）。Play As は、Addressables の Play Mode が source を読むもの（Use Asset Database、`BuildScriptFastMode`）でなければ拒否する（source の Play の前提で、既存 build の mode では起動が古い bundle の `app-config.json` を読みうるので、その経路を塞ぐ。起動は `app-config.json` を Addressables の TextAsset として読むので、disk の file の検査は起動が読む値と同じであることまでは保証しない。実効値の証拠は AC17 (e) の起動した `AppConfig`）。合格例: profile 未選択・該当 key 無し → 置く。不合格例: 外の値を上書きして Play に入る | I1 | EditMode |
| AC3 | 共通の ledger（Play As・検証 Play・Delivery 窓が共有）が、置いた key・以前の値・置いた値と、Play で戻すかの印（Play As・検証 Play は「1 回の Play で戻す」、Delivery 窓は「手で戻す」）を `SessionState` に持つ。「1 回の Play で戻す」ledger は Play を要求するための ledger で、`EnteredEditMode` で Reset する（`ExitingEditMode` は Play への移行の開始で `EnteredPlayMode` への到達を意味しないので、Play に入ったかどうかを問わない）。Play に至らない場合: 「1 回の Play で戻す」ledger がある間の毎回の `EditorApplication.update` と、domain reload の後の初期化（`[InitializeOnLoad]`）で、その ledger があり `EditorApplication.isPlaying` と `EditorApplication.isPlayingOrWillChangePlaymode` がどちらも偽なら Reset する（要求が受理されなかった、移行が中断して Edit mode に留まった）。Play 中・Play への移行中・Play を抜ける途中（`ExitingPlayMode` では `isPlaying` が真のまま `isPlayingOrWillChangePlaymode` が偽になる）は戻さず、Play を抜けた時の戻しは `EnteredEditMode` で行う。Reset は置いた値のままの key だけを以前の値（無ければ削除）に戻す。新しい Apply は、既にある「1 回の Play で戻す」ledger なら先に Reset してから置き、「手で戻す」ledger（Delivery 窓）なら Reset せずに拒否して窓の Reset を案内する。印の無い v1 の ledger は「手で戻す」として読む。合格例: 「手で戻す」ledger は Apply → Play に入らずに再コンパイル（Edit mode の domain reload）→ 値は残る。「1 回の Play で戻す」ledger は Apply → Play → Play 中の domain reload → Edit へ戻る → 戻る。負例: `ExitingEditMode` の後に `EnteredPlayMode` に至らずに `EnteredEditMode` → 戻る。Apply の後に移行が始まらないまま update が来る → 戻る。`EnterPlaymode` の後の update ではフラグが真で、`ExitingEditMode` も domain reload も無いまま要求が取り消されてフラグが偽に戻る → その後の update で戻る。`ExitingPlayMode` の間の update（`isPlaying` が真、`isPlayingOrWillChangePlaymode` が偽）→ 戻さず、続く `EnteredEditMode` で戻る。Play 中に他者が key を変えた → その key は戻さない、他の key は戻る | I3 | EditMode（`SessionState` と process 環境。Play には入らず、mode 変化と update の handler を直接呼び、`isPlaying` と `isPlayingOrWillChangePlaymode` の 2 値を注入する） |
| AC4 | 既存の Delivery 窓の Use For Next Play も同じ ledger を「手で戻す」印で使い、従来どおり手で Reset でき、Play の終了では Reset されない。従来の 5 key の値と検証（`ValidateInstalled`）は変わらない。窓の ledger がある間の Play As・検証 Play・From published directory は、窓の値を消さずに拒否し、窓の Reset を案内する（menu からの操作では D11 の確認の dialog で Reset して続けられる。黙っては消さない）。窓の Use For Next Play は、「1 回の Play で戻す」ledger が残っていて Play 中でも移行中でもなければ（`isPlaying` と `isPlayingOrWillChangePlaymode` がどちらも偽）先に Reset してから置く | I3・I6 | EditMode |
| AC5 | 検証 Play の開始前検査が、ロード済みの各 Scene について、名前が SceneResourceMap（`SampleGameBootstrapContentComposer.SceneResourceMapPath`。directory に入る map と同じ asset）にある、名前が `UICommon`、または path が `UiCommonPath` のどれかなら拒否し、該当する Scene を列挙する。build も publish もしない。判定は Scene の一覧と map を入力に取る pure な関数に置く。menu からの操作では、拒否の代わりに D11 の確認の dialog を出してよい（閉じて空 Scene で続けるか、止めるか）。dialog の無い呼び出しは拒否で止まる。負例: `Spring_Cell_0_4` を開いて `spring-planner-whitebox` → 拒否。選択に入らない `Summer_Cell_*` の 1 件を開いて `spring-full` → 拒否（起動が名前で登録し再利用するので）。`UIScene.unity` を開いて `spring-full` → 拒否。合格例: untitled の空 Scene だけ → 通す。SceneResourceMap に無い Scene（テスト用の Scene 等）だけ → 通す | I2 | 単体（判定関数）+ EditMode |
| AC6 | 検証 Play の 1 操作の段の順序と停止: Play mode 中・script の compile error 中（`EditorUtility.scriptCompilationFailed`。`EnterPlaymode` が Play に入らず、印の無い ledger が残るのを防ぐ）の拒否 → 開始前検査（AC5、AC2 の profile・ledger・コマンドラインの検査、AC3・AC4 の窓の ledger の検査）→ `Full` build → publish → install → `EnterPlaymode` の直前の再検査（AC5、AC2 の可変な条件（active Variant profile、置く key の外の process 環境の値）、窓の ledger を含む ledger、compile error、compile 中（`EditorApplication.isCompiling`）、asset の更新中（`EditorApplication.isUpdating`）、既に Play 中か移行中（`isPlaying` か `isPlayingOrWillChangePlaymode`）。install の待ちの間に状態が変わりうるので）→ bridge → `EnterPlaymode`。どの段が失敗しても以降の段を実行せず、Play に入らない。bridge 前の失敗は環境を変えない。bridge 以降（`EnterPlaymode` を含む）の失敗は ledger を Reset してから止まる。Play を要求した後に Play に至らなければ AC3 の規則で戻る。段ごとの失敗を偽の段で注入して確かめ、開始前検査で拒否した時は build の段の呼び出しが 0 回であることも確かめる。直前の再検査の負例: install の待ちの間に profile が選ばれる、または置く key に外の値が置かれる → bridge の前に拒否し、外の値を変えず Play を要求しない（それまでの build・publish・install の成果物は残る。失敗した段より前の副作用として扱う）。**1 つずつ（single-flight）:** 検証 Play と From published directory は 1 つの「操作中」の印（static）を共有し、印がある間の新しい要求は build・publish・install に入る前に理由を示して拒否する。印は成功・失敗・取り消しのどの経路でも `finally` で外す。install の待ちの間の domain reload では操作ごと消え、bridge の前なので環境は変わらない（印も消える）。**取り消し:** From published directory は窓の寿命に従い、窓が閉じたら（`OnDisable`。既存の Prepare と同じ）取り消して Play を要求しない。menu からの検証 Play は窓を持たず取り消しの所有者が無いので、操作は続く。取り消した後の遅れた継続は bridge に達しない（bridge の直前に取り消しを確かめる）。偽の段で install を未完了に保ち（`TaskCompletionSource`。待ち時間は使わない）、2 件目の要求で build の段が呼ばれず 1 件目だけが bridge に達すること、取り消すとどちらも Play を要求しないことを確かめる | I1・I5・E2 | 単体（段を注入した orchestrator） |
| AC7 | 検証 Play の bridge は content の 5 key（表現は宣言の主値）に加えて manifest の `declaration` の `RuntimeDefaults` と初回 Scene（AC9）を置く。宣言の名前が選んだ宣言と違う manifest は拒否する | I1・E2 | 単体 + EditMode |
| AC8 | Editor directory Play で config `content:firstScene` があれば、`RegisterAlreadyLoadedScenes` の後に `SceneDirector.AddScene(firstScene)` で初回 Scene をロードし、Player の smoke の段（代表 Object の検証、初回 Scene の unload、directory の close、receipt）は行わない。Player の経路の段の順序と receipt は変わらない。失敗経路の初回 Scene の unload も `UseRequiredPlayerFileConfiguration` の時だけで、Editor の失敗では unload しない（Player の失敗時の unload と receipt の段は今の順序のまま）。`content:firstScene` が無い Editor directory Play は従来どおり初回 content Scene をロードしない。addressables mode は `content:firstScene` を読まない。負例: 初回 Scene のロードが失敗した Editor directory Play（directory にその Scene が無い等）は、起動の失敗として段と Scene 名をログに出し、別の Scene をロードしない（fallback しない） | E2・I5・I6 | EditMode（起動の分岐の単体化）+ 既存の Player content テスト |
| AC9 | 初回 Scene は App（SampleGame）が宣言ごとに決める（D12）。検証 Play の 1 操作と From published directory は同じ決定から `SAMPLEGAME_CONTENT__FIRSTSCENE` を置き、BS4 coordinator も同じ決定を使う。今の値は 6 宣言とも `Title`（実装の事実で、全目的に共通の契約ではない）。変換テスト: 合成の値として `Title` 以外の初回 Scene（例 `OutGameScene`）を変換に引数で渡すと（production の宣言の一覧には合成の宣言を登録しない）、検証 Play と From published directory の bridge の key 集合の両方がその値を持つ（content build はしない）。Framework は値を知らない | E2・E5・I7 | 単体 |
| AC15 | From published directory: 結果 file（`last-result.json`）を BS5b-1 の `SampleGameContentBatch.SuccessFile` の型で読む（読み手に別の型を作らない）。`exitCode` が 0 でない、または `identity`・`manifestSha256`・`declaration`・`publishPath` のどれかが空なら拒否し（失敗の `FailureFile` はここで拒否される）、`manifestSha256` と `identity` を期待値にする。結果 file の `declaration` と manifest の宣言名が違えば拒否する。published directory は結果 file の `publishPath` が手元に有ればそれ、無ければ folder の選択で入れる。`transport.json` の contentSet・revision・`declaration` を読み、段の順は、SHA-256 の照合（`transport.json` の SHA-256 が結果 file の期待値と違えば拒否）→ identity の照合（`transport.json` の `revision` が結果 file の `identity` と違えば拒否。組み違えた結果 file と directory を拒否する）→ 宣言の照合（manifest の宣言名で `SampleGameBuildPurposes.Find` から引く。無ければ拒否。manifest の `declaration` が記録する欄、すなわち表現の主値と `RuntimeDefaults` が手元の宣言の同じ欄と違えば拒否）→ AC5 と AC2 の開始前検査 → install → `EnterPlaymode` の直前の再検査（AC6 と同じ項目）→ bridge（表現は宣言の主値、`RuntimeDefaults`、手元の宣言に App が決めた初回 Scene（AC9））→ Play。どの拒否も install と環境の変更より前に起きる（直前の再検査を除く）。宣言の無い manifest は拒否し、Delivery 窓の手入力経路を案内する。テストの結果 file は手書きの JSON ではなく、`SuccessFile` / `FailureFile` を `JsonUtility` で書いたもの（BS5b-1 の書き手と同じ形）を使い、成功・`exitCode` 0 以外・SHA-256 の不一致・identity の不一致・宣言名の不一致・`publishPath` が無い時の folder の選択を確かめる | E5・I1・I2 | EditMode（合成の published directory と結果 file）+ HC2 |
| AC16 | （A3 で後続へ送った。#53。番号は A2 の記録と照合するために欠番で残す） | — | — |
| AC17 | 操作: (a) `spring-planner-whitebox` を Play As（profile 未選択、開いている Scene は `OutGameScene`）→ 起動後の実効 config が `world:cellCompanionSet=Planner`、`assets:sceneVariant=Whitebox`、mode addressables で、Play を止めた後の process 環境が置く前と同じ。Event の動作は観測しない（d5b）。(b) `spring-full` の検証 Play の 1 操作（空 Scene から）→ `SceneDirector.IsSceneStable("Title")` が真（D5 の 1 行のログ。Unity の Scene のロードや GameObject の存在では代えない）、起動ログに封鎖の 1 行、不一致 Warning 無し、Console の Error 0、Play を止めた後の環境が戻る。(c) `Spring_Cell_0_4` を開いた状態で `spring-full` の検証 Play → 拒否、build の段に入らない（`artifacts/bs2b/reports/` に新しい identity が無い。build は Unity の build の前に identity を決めて `preflight.json` を書くので、`content/` が増えないことだけでは build を呼んでいないと言えない）。(d) 選択に入らない `Summer_Cell_*` の 1 件を開いた状態で `spring-full` の検証 Play → (c) と同じ。(e) `spring-full` を Play As（(a) と同じ初期状態）→ 起動後の実効 config の `assets:sceneVariant` が空（`Full`）、Play を止めた後の環境が置く前と同じ | E1・E2・I1・I2・I3 | 操作（Phase C。(a)(c)(d)(e) は動作確認でも 1 回）、証拠は §5 の記録の表（run ごとの区間、実効 config、前後の環境の比較、identity） |
| AC18 | 最終の全 EditMode 回帰が通る。BS3 / BS4 / BS5b-1 の既存テスト（`ContentDirectorySessionTests`、`BuildContentDirectoryIntegrationTests`、`PlayerContentConfigurationTests`、`ContentDeliveryFailureMatrixTests`）の結果を変えない | I6 | 回帰 |
| AC19 | `SampleGameBuildPurposes.All` の各宣言に、Play As と Verify Play の menu（`MenuItem` の付いた static method）が 1 つずつあり、その method が同じ宣言を使う。テストは `MenuItem` の path を reflection で集めて宣言の集合・件数と突き合わせ、宣言を足して menu を足し忘れたら失敗する。各宣言に D12 の初回 Scene が決まっていることも確かめる。menu の本体がその宣言を使う結線は、menu の path と本体が同じ宣言名の定数を使う形にし、機械テストではなく発見 C のコードレビューで照合する（reflection は属性しか見ないので結線を示さない） | E1・E7 | EditMode（集合）+ レビュー（結線） |
| AC20 | 検証 Play と From published directory は、Delivery 窓の設定の `cacheRoot` が空なら既定の絶対 path（`<LocalApplicationData>/OSM/ContentCache`）に解決してから install する。解決は両経路が共有する 1 つの関数に置き、`cacheRoot=""` の設定から合成の published directory を install できることを確かめる。窓の手入力の Prepare は従来どおり（空を拒否する `ContentCacheStore` をそのまま呼ぶ。I6） | E7・I6 | EditMode |

AC2 の読み方: 「profile が選ばれている」は `SceneVariantRuntimeBridge.EditorSceneVariantResolver` が `null` 以外を返すこと。`Production`（`""`）も選択に含める（config より優先して表現を固定するので）。

AC5 の読み方: 対象は宣言の選択ではなく、起動が再利用しうる Scene（I2）。SceneResourceMap は名前で引くので、map の判定は名前、UICommon の Scene は名前と path の両方で見る。宣言の選択に依らないので、6 宣言のどれでも同じ判定になる。Variant の Scene は元の Scene と同じ名前なので、名前の判定で両表現を覆う。Scene のロードはどれも `SceneDirector` が map の名前で行うので（`SceneDirector.Loading.cs:631`）、この 3 つ以外に source の Scene を再利用する経路は無い。

### 2.4 A1 の設計

- **D1（Play As の置き場と形）:** `SampleGame/DependOnAll/Editor/Build/` に `PlayAsLauncher`（menu `Tools/OSM/Play As/<宣言名>`、`All` の宣言ごとに 1 件。今は 6 件）を置く。Unity の menu は static なので、宣言を `SampleGameBuildPurposes` に足す担当が、Play As と Verify Play の menu も同じ変更で足す。足し忘れは AC19 が見つける（menu の生成は後続。§2.5）。宣言から config の key=value を作る変換 `PurposeLaunchSettings`（pure、internal）は同じ folder に置き、Play As と検証 Play と From published directory が共有する。prefix `SAMPLEGAME_` と `Full`→空の写像は SampleGame が持つ（Framework の `ResolveSceneVariant` は `assets:sceneVariant` を読むだけで、写像は持たない）。空への写像は Play As の `assets:sceneVariant` だけに使い（環境には置かない。AC1・AC2）、検証 Play と From published directory の `content:representation` は主値の文字列（`Full` を含む）のまま置く。Framework に「宣言を config に写す」層は作らない（実装は 1 つ）。
- **D2（共通 ledger）:** 既存 `ContentDeliveryPlayBridge` を、任意の key 集合を置ける ledger に広げる（`Apply(IReadOnlyList<(key, value)>)`、`Reset()`、`AssertNoForeignValues(keys)`）。Delivery 窓の既存 `Apply(root, digest, identity, set, representation)` は内部で 5 key を作って同じ経路を通る。ledger は Play で戻すかの印を持ち、`[InitializeOnLoad]` で `EditorApplication.playModeStateChanged` に登録した handler、「1 回の Play で戻す」ledger がある間は毎回動く `EditorApplication.update` の handler（ledger が無ければ何もしない）、domain reload の後の初期化が AC3 の規則で Reset する（Play に至らなかった「1 回の Play で戻す」ledger も戻す。「手で戻す」ledger は Edit mode の domain reload で戻さない）。新しい Apply は、既にある ledger が「1 回の Play で戻す」なら先に Reset し、「手で戻す」なら拒否する（AC3・AC4。手で置いた値を別の経路が黙って消さない）。ledger の key は `OSM.ContentDelivery.PlayBridge.v1` のまま（`keys`・`previous`・`installed` の 3 配列に、戻し方の 1 欄を足す。欄の無い v1 は「手で戻す」）。汎用の transaction 基盤や状態機械は作らない（既存 bridge の Play 1 回分の寿命の管理に留める）。
- **D3（profile の扱い）:** Play As は profile を変えない（`UserSettings` の永続値を Play As が書き換えると、Play の後に戻す責務と失敗時の不整合が増える）。profile が選ばれていれば拒否し、Variant 設定の窓で選択を外すよう案内する。
- **D4（検証 Play の 1 操作）:** `VerifyPlayLauncher`（menu `Tools/OSM/Content/Verify Play/<宣言名>`）。段は AC6 の順。build は BS5b-1 の `SampleGameContentBuild.BuildResult(declaration, mode: Full)`、publish は `ContentTransportPublisher.Publish(result, artifacts/bs2b/transport/<identity>)`、install は Delivery 窓と同じ `ContentInstaller` + `LocalContentArtifactSource`（cache root は Delivery 窓の設定 `UserSettings/OSMContentDelivery.json` の `cacheRoot`。空なら既定の `<LocalApplicationData>/OSM/ContentCache` に解決し（両経路が共有する 1 つの関数。AC20）、標準の経路が使った実効の cache root として窓に表示する（手入力の Prepare で既定が使えるとは示さない。Prepare は空を拒否するまま）。repo の中の既定にしないのは、`installed/<contentSet>/<revision>/content/<Unity の file>` が repo 相対 150 文字の予算を超えるため）、bridge は D2 に `FIRSTSCENE` と `RuntimeDefaults` を足した key 集合。段の列は menu・`EditorSceneManager`・窓に依存しない pure なクラス（`VerifyPlaySequence`、段を interface で受ける）に置き、AC6 を単体で確かめる。menu・開始前検査・From published directory はそれを呼ぶ Editor 側（`VerifyPlayLauncher`）に置く。どちらも同じ file でよい。build は同期（Editor が止まる。既存 menu と同じ）、install は async で、完了後に開始前検査の再検査（AC6）を通してから bridge と `EnterPlaymode`。開始前検査は副作用（build・publish・install・環境の変更）より先に行う。
- **D5（Editor directory の初回 Scene）:** Runtime の `ReadContentDirectoryConfiguration` の Editor 分岐で `content:firstScene` を読み、`ContentDirectoryConfiguration.FirstScene` に渡す。起動の `load-player-first-scene` の分岐を、初回 Scene のロード（Editor と Player の共通）と Player の smoke（`UseRequiredPlayerFileConfiguration` の時だけ）に分ける。Editor では初回 Scene を unload しない。失敗経路の unload も同じ条件で分ける。新しい状態・所有者・寿命は足さない（`SceneDirector.AddScene` の既存の寿命）。初回 Scene の名前は config から受けるだけで、Framework は目的や値の意味を知らない（D12）。ロードの失敗は起動の失敗にし、別の Scene へ逃がさない（AC8）。Editor directory の初回 Scene のロードの後、`SceneDirector.IsSceneStable(firstScene)` の結果を build identity と Scene 名とともに 1 行ログに出す（Phase C の観測点。Player の段と receipt は変えない。Framework は Scene 名を config から受けたまま出すだけ）。BS5a の AC13 で `SceneDirector.AddScene("Title")` が directory から `Title` をロードできることは観測済み（Title の先へ進めるために Start の event を手で発火したのは別の話で、E2 は `Title` の Stable までを判定する）。
- **D9（From published directory）:** From published directory の入口は Delivery 窓の Published directory モードに 1 つの操作として足す（入力と cache root の設定が既にあり、窓を増やさない）。窓の既存の手入力経路（Prepare・Use For Next Play の値と表示）は変えない（窓の宣言対応は後続。#53）。manifest の読み取りは既存 `ContentTransportManifest`（Runtime の Distribution）を使う。manifest SHA-256 の期待値は、batch の結果 file（`last-result.json`）を file の選択で読んで得る。結果 file は published directory とは別の経路（CI の出力、渡した人）から来るものとし、その前提を窓に 1 行で示す。64 文字の手入力は求めない。published directory は結果 file の `publishPath` が手元に有ればそれを使い、無ければ folder の選択で入れる。published directory の中の manifest を hash して期待値にすることはしない（自己一致になり、取り違えも書き換えも見つからない）。結果 file の無い published directory は、従来の Delivery 窓の手入力経路で受け取る。
- **D10（宣言名の引き方）:** manifest の `declaration.name` から既存の `SampleGameBuildPurposes.Find` で宣言を引く（未知の名前は `ArgumentException`）。新しい引き関数は足さない。manifest の `declaration` が記録する欄（名前・表現の主値・`RuntimeDefaults`）が手元の宣言の同じ欄と違えば拒否する（AC15）。保証するのは記録された欄の一致までで、`Roots`・`Filter`・表現の優先順・`First` / `All` や source の状態の同一性は保証しない（identity 後続）。
- **D11（利用者が選ぶもの・機械が決めるもの）:** 新しい設定の型や catalog の仕組みは作らない。宣言は既存の `SampleGameBuildPurposes.All` を menu の候補にする。Framework の閉じた選択肢（`BuildContentMode` 等）は enum のまま、config・manifest の値は今の文字列のまま。App 固有の選択肢（companion set、表現）は宣言から決まるので、BS5b では App の enum を足さない。App が独自の選択肢の型を定義する仕組みも、需要が出るまで作らない（今の設計はそれを妨げない。#50）。

  | UC | 利用者が選ぶ・入れるもの | 宣言・manifest・既定値から決まるもの |
  |---|---|---|
  | UC2 Play As | 宣言（menu） | mode、表現（`assets:sceneVariant`）、`RuntimeDefaults` |
  | UC4 検証 Play | 宣言（menu） | build mode（`Full`）、publish 先、cache root（既定）、contentSet、revision、表現、`RuntimeDefaults`、初回 Scene（App が宣言ごとに決める。D12） |
  | UC7 From published directory | batch の結果 file（file の選択）。`publishPath` が手元に無い時だけ published directory（folder の選択） | manifest SHA-256 の期待値と identity（結果 file）、contentSet、revision、宣言、表現、`RuntimeDefaults`、cache root（既定）、初回 Scene（手元の宣言に App が決めた値。D12） |

  Delivery 窓の自由入力（contentSet・revision・representation・manifest SHA-256）は、宣言の無い manifest・結果 file の無い directory・診断のための低レベルの経路として残し、標準の操作には数えない。UC4 の manifest SHA-256 は同じ操作の中で publish した値をそのまま使う（期待値の出所はその操作自身の build で、外から受け取る値が無いので照合は要らない）。

  利用者が直せる状態で止まる拒否は、menu からの操作では確認の dialog を出し、利用者が選べばその場で直して続ける（`開いている Scene を閉じて空 Scene で続ける`：未保存の変更は Unity の保存の確認を通す。`Delivery 窓の設定を Reset して続ける`）。dialog の無い呼び出し（`eval`、テスト）は拒否で止まる。利用者が直すべきで経路が直さないもの（Variant profile の選択、コマンドラインの競合、compile error）は拒否と案内だけにする（D3）。Play 後に元の Scene の構成へ戻すことはしない（後続の候補）。

- **D12（初回 Scene は App が目的ごとに決める。revision 2）:** SampleGame は `SampleGameBuildPurposes` に宣言と並べて、宣言から初回 Scene を返す関数（宣言名で引く。未知の宣言は `ArgumentException`）を置き、検証 Play・From published directory・BS4 coordinator が同じ関数を使う。Play の経路は初回 Scene を引数で受け、テストは合成の値を渡す（AC9）。今はどの宣言も `Title` で、今の目的群に対する App の設定値であって全目的の契約ではない。宣言を足す担当は初回 Scene も決める（AC19 が欠けを見つける）。Framework の `BuildPurposeDeclaration` と manifest には足さない（Framework の公開 API と manifest の schema を変えない。Framework は渡された Scene 名を `SceneDirector` へ流すだけで、目的ごとの辞書を持たない）。その目的の成果物に初回 Scene と、そこから目的の content へ到達するのに要るものが入るよう宣言と App を構成するのは App 開発者の責務で、本スライスが示すのは初回 Scene の Stable まで。入っていなければ起動は失敗し、別の Scene へ逃がさない（AC8）。manifest は初回 Scene を記録しないので、From published directory は受け取る側の App の定義を使い、発行側と App の版が違えば同じ起動地点である保証は無い（§2.0。記録を足すなら別の設計判断）。同じ目的と成果物に別の起動地点を選ぶ拡張は妨げないが、今回は作らない。

### 2.5 ここでは答えない問いと所有する後続

| 問い | 所有 |
|---|---|
| Planner 宣言で Environment を読まずに Whitebox 上の Event が動くか、S-4 program の harvest と削除 | S-4d5b |
| 入力 identity、設定 snapshot、`content/index`、`priorRevisions`、build を skip する再利用 | identity 後続（[BuildSystem program §7](BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md#7-buildsystem-の他の系列の所在)） |
| R2・HTTP からの取得、別 PC の実機での受け取り | BuildSystem 後続（Storage program §6「取得経路と配布」と接続） |
| 日常 Play（source）で宣言に無い content を読んだ時の検出 | BuildSystem 後続 |
| 宣言の不一致 Warning と封鎖の配線の自動テスト、付随 asset の entry を名前で同定する材料 | BuildSystem 後続（Architecture §18 §4「検証の残り」） |
| directory mode の Editor Play で flight rig が spawn に置かれない | 所有スライス未定（Architecture §18 §4「検証の残り」） |
| 検証 Play の後に元の Scene の構成へ戻す | 後続の候補（D11） |
| batch の起動 wrapper（Unity を起動し、終了コードと結果 file で成否を決める。前回の結果 file を今回の結果と読まない。成功後に終了しない Unity は結果の確定の後に止める） | BuildSystem 後続（CI・運用。#51・#52） |
| DIST の cache root の path 予算の事前検査（既定・手入力の両方の install） | DIST・install の後続（#49） |
| 宣言から menu・操作の入口を生成する | BuildSystem 後続（#50。手で並べるのがつらくなった時） |
| Delivery 窓の手入力経路の宣言対応（Prepare した revision の宣言の表示、表現の違いの表示、Use For Next Play で `RuntimeDefaults` を置く） | BuildSystem 後続（#53） |

- **判定定義:** 不変条件 I1〜I3・I5〜I7 と成立条件 E1・E2・E5・E7 を AC1〜AC9・AC15・AC17〜AC20 と HC2 で確かめ、全て成立で GO。1 つでも不成立なら NO-GO。
- **停止規則:** 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。スパイクではないので CONDITIONAL ACCEPT は使わない。
- **A3 後の例外承認:** なし。
- **決定記録:** なし。
- **本文へ転記した実装制約:** §1.2。
- **未決事項:** §2.6。

### 2.6 未決事項

- **U1（初回 Scene の置き場）:** A2 で D5 に閉じた。代案（Editor の `playModeStateChanged` から `SceneDirector.AddScene` を呼ぶ）は不採用で、Phase B は D5 以外の置き場を選ばない。`AbstractApplicationInitializer` は 1,277 行で、変更は分岐の分離（約 20 行）に留める。
- **U2（規模）:** 見積もりは製品コード 400〜500 行（Editor 約 450、Runtime 約 20。A3 で SampleGame Runtime の helper と窓の宣言対応を外した）、テスト 450〜550 行。変更理由は 2 つ（Play As と ledger、検証 Play と受け取り）で、ledger と開始前検査を共有するので 1 スライスとする。Phase B で Runtime 側が D5 の分岐の分離を超える状態・所有者・寿命を要したら B を止めて A へ返す。
- **U3（From published directory の入口）:** D9 で閉じた（Delivery 窓に足す）。

---

## 3. 責務マップ（A1）

| file | 責務 | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|---|
| `SampleGame/DependOnAll/Editor/Build/PurposeLaunchSettings.cs`（新規） | 宣言 → config key=value・環境変数名の変換 | Play As と検証 Play の入力 | 静的・pure | `BuildPurposeDeclaration` | internal | 単体（AC1・AC7・AC9） | 0 → 70 |
| `.../ContentDeliveryPlayBridge.cs` | 共通 ledger（Apply・Reset・外の値の検査・Play 1 回分の Reset と、Play に至らなかった時の Reset） | ledger を任意の key 集合に広げる | `SessionState`。Editor session の寿命 | UnityEditor | internal | EditMode（AC2〜AC4） | 98 → 165 |
| `.../PlayAsLauncher.cs`（新規） | Play As の menu、profile・コマンドラインの競合の拒否、`EnterPlaymode` | Play As | 静的 | bridge、`PurposeLaunchSettings`、Variant resolver、`CommandLineConfigProvider`（Foundation） | menu | EditMode（AC2・AC19） | 0 → 90 |
| `.../VerifyPlayLauncher.cs`（新規） | `VerifyPlaySequence`（段の列、失敗時の Reset、1 つずつの印、取り消しの確認。pure）と `VerifyPlayLauncher`（menu、開始前検査、確認の dialog、From published directory。Editor I/O） | 検証 Play | 1 操作の間 | build・publish・install・bridge（段は interface）、結果 file は `SampleGameContentBatch.SuccessFile` | menu / internal | 単体（AC6、`VerifyPlaySequence`、AC5 の判定関数）、EditMode（AC5・AC15・AC19） | 0 → 240 |
| `.../ContentDeliveryWindow.cs` | From published directory の入口（結果 file の選択）と、窓が閉じた時のその取り消し、実効の cache root の表示、共通 ledger の利用。既存の手入力経路は変えない | 受け取り | 窓の寿命 | 上記 | menu | EditMode（AC4・AC15） | 168 → 195 |
| `.../SampleGamePlayerBuildCoordinator.cs` | 初回 Scene を D12 の関数から取る | 初回 Scene の決定 | 1 build | `SampleGameBuildPurposes` | 既存 | 既存 | 152 → 154 |
| `.../SampleGameBuildPurposes.cs` | 宣言ごとの初回 Scene（D12） | 初回 Scene の決定 | 静的 | — | internal | 単体（AC9・AC19） | 58 → 72 |
| `SampleGame/DependOnAll/Editor/SampleGame.DependOnAll.Editor.asmdef` | `OneStarMaker.Foundation` の参照を 1 本足す（#47。Game → Framework の向きのまま） | コマンドラインの競合の検査（AC2） | — | Foundation | — | compile | +1 |
| `OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs` | Editor directory の初回 Scene、Player smoke との分離（成功・失敗の両経路） | 初回 Scene | 起動 | 既存 | なし（private） | EditMode（AC8） | 1,277 → 1,300 |

行数の警報: `AbstractApplicationInitializer.cs` は既存で 500 行を超える。本スライスは分岐の分離だけで、分割はしない（起動順序の所有を 1 箇所に保つ既存の判断を変えない）。`VerifyPlayLauncher.cs` は 240 行の見込みで、段の列（pure）と Editor I/O を別クラスにして同じ file に置く（開始前検査の規則を変えても段の順序のクラスを触らない）。

## 4. 実装計画（A1）

### 4.1 順序

Phase B の中に確認地点を置く。各段の終わりに compile と差分の自己点検をし、次の段へ進む条件を確かめる。段ごとの証拠 bundle やレビューの依頼は足さない（判定は Phase C）。

1. 共通 ledger と `PurposeLaunchSettings`、asmdef の Foundation 参照、Play As、AC19 の menu 照合。確認地点: 宣言が唯一の入力、戻す責務が 1 つ、手の ledger を黙って消さない、コマンドラインの競合の拒否。
2. Runtime の初回 Scene（D5）。確認地点: Player の契約（smoke・receipt・失敗時の unload）を変えない。
3. 検証 Play の orchestrator、開始前検査、確認の dialog、D12 の初回 Scene、cache root の解決（AC20）。確認地点: 起動の再利用の経路を開始前検査が覆う、失敗段の副作用が限られる、初回 Scene を App の決定から受ける。
4. From published directory（Delivery 窓の入口）。確認地点: manifest と結果 file から導ける値を手入力させない。窓の既存の手入力経路を変えていない。
5. compile 確認と `pwsh tools/contract-audit.ps1`。Unity テスト・content build・batch・Play の判定はしない（Phase C）。

### 4.2 Phase B から Phase A へ差し戻す条件

- A3 で決めた `SampleGame.DependOnAll.Editor` → `OneStarMaker.Foundation` のほかに asmdef 参照の追加が要る。
- D5 の分岐の分離を超えて、Runtime の起動に新しい状態・所有者・寿命・公開 API が要る。
- 既存の Delivery 窓の手入力経路・BS4 Player の receipt の形を変えないと条件を満たせない。
- BS5b-1 の結果 file の欄・型・publish の API を変えないと条件を満たせない（BS5b-1 は凍結・マージ済みなので、変更は別の判断。`SuccessFile` を読み手から使うための可視性は既に internal で足りる）。

### 4.3 対象外を維持する方法

Planner 宣言の Event・Environment の観測を AC に入れない（AC17 (a) は config の値と環境の復元だけ）。identity・index の欄を足さない。build mode・publish・batch のコードに触らない（BS5b-1）。Delivery 窓の既存の手入力経路に宣言の表示や `RuntimeDefaults` を足さない（#53）。batch の起動 wrapper、cache root の path の事前検査、menu の生成を作らない（§2.5）。

## 5. テストとレビュー計画（A1）

- **単体:** AC1・AC5（判定関数）・AC6・AC7・AC9（合成の宣言の初回 Scene）、コマンドラインの読み取り（合成の引数）、`Full` の下位の層の検査（合成の `app-config.json` の内容。Editor asmdef の pure class と偽の段）。
- **差し戻し中の起点 `-Filter`:** 新しいテスト class と、`ContentDirectorySessionTests`・`PlayerContentConfigurationTests`・`ContentDeliveryFailureMatrixTests`・起動の EditMode テスト（`InvalidCompanionConfigStartupTests` を含む）。
- **判定必須テスト:** 最終の全 EditMode 回帰（空 filter）。PlayMode は対象外（Runtime の変更は Editor directory 分岐だけで、Player の smoke は既存の EditMode / Player content テストが固定する）。
- **全 EditMode 回帰の適用除外:** なし。
- **操作・実行時・目視条件の検証経路:**
  - **AC17（Play As・検証 Play・拒否）:** 担当は判定 C の実行担当（Editor を `tools/unity-editor.cmd` の `eval` で操作。menu は `eval` から menu の static method を呼ぶ）。初期状態: Editor 起動、Variant profile 未選択、Delivery 窓の ledger 無し（cache root は既定）。`eval` からは dialog の無い入口を呼ぶ（(c)(d) は拒否で止まることを見る）。**記録の表:** run ごとに 1 行、run ID・開始と終了の印（`eval` で Console へ `[BS5b2-evidence] begin <runId>` / `end <runId>` を出し、Editor.log の区間をこの印で切る。前の run のログを今回の結果と読まない）・宣言名・build identity・published path・installed revision root・初回 Scene・Play 前後の環境の比較・区間の Error / Warning の件数を同じ ID で残す。照合 script は作らない。**観測の正:** 実効 config は起動した Runtime が持つ `AppConfig`（環境変数や file の読み直しを実効値の証拠にしない）。環境の復元は、Play を止めた後に同じ Editor process の環境を読み直し、制御した key ごとに前と後が等しいか（`null` と空文字を区別）を残す（Reset の呼び出しのログは補助）。Stable は D5 の 1 行のログ。**Editor の状態:** project は Play に入る時の domain reload を切っている（§1.3）。build は同期で `eval` を塞ぐ。install の継続は Editor の tick で進むので、Editor が idle で進まない時は数十秒おきに `eval`（例 `EditorApplication.QueuePlayerLoopUpdate()`）を送って tick を起こす（観測の支援で、判定を変えない）。cache root（既定は `LocalApplicationData` の下）は Editor と同じ環境の pwsh で見る（仮想化された shell から見ない）。(a) `OutGameScene` を開き Play As `spring-planner-whitebox` → Play 開始後の実効 config、Play 停止後の `SAMPLEGAME_*` の process 環境。(e) 同じ初期状態で Play As `spring-full` → 実効 config の `assets:sceneVariant`、停止後の環境。(b) 空 Scene で Verify Play `spring-full` → build・publish・install の完了ログ、D5 の Stable のログ（`IsSceneStable("Title")` が真）、封鎖の 1 行、Warning / Error の一覧、停止後の環境。build は約 4.5 分で、`eval` の timeout の後も走る。完了は `artifacts/bs2b/transport/<identity>` の出現と Play mode への遷移で判定する。(c) `Spring_Cell_0_4` を開き Verify Play `spring-full` → 拒否のログ、run の前後で `artifacts/bs2b/reports/` と `content/` に新しい identity が無い。(d) `Summer_Cell_0_0` を開き Verify Play `spring-full` → (c) と同じ。保存: 記録の表、Console ログ（Editor.log の印の区間）、環境の前後の比較、directory の一覧。
  - 動作確認（Phase B の後）: AC17 (a)(c)(d)(e) を B の完了 head で各 1 回軽く行う。どれも Editor 操作で、batch も content build も伴わない。(b)（content build）は Phase C だけで行う。
- **未知の操作経路の疎通結果と、必要な検証支援・確認地点:** `eval` から menu の static method を呼んで `EnterPlaymode` に入り、Play 中のログと停止後の process 環境を取る経路は、BS5a の AC13 で Play への遷移と Console の取得までは成立した。Play 停止後に Editor process の環境変数を `eval` で読む経路と、起動後の実効 config を `eval` で読む経路は未確認（起動時に実効 config を出すログは今無い）。実効 config が `eval` で読めなければ、SampleGame の起動が、起動した `AppConfig` から制御する key の実効値を 1 行ログに出す（B 適応の範囲）。初回確認は動作確認（AC17 (a)）。停止後の環境が `eval` で読めなければ、bridge が Reset の後に同じ process の環境を読み直し、key ごとの前後の一致（真偽。値は出さない）を 1 行ログに出す（B 適応の範囲）。戻した key の一覧だけを復元の証拠にしない。`SceneDirector.IsSceneStable` は D5 のログで観測するので `eval` は要らない。どの観測も Phase C の前に動作確認で取得方法を確定し、取れなければ弱い代わり（key の一覧、Unity の Scene のロード）を成功の証拠にせず `未実施` とする。
- **人間の判断が必要な条件:** なし。
- **高コスト証拠:**
  - **HC2（From published directory の検証 Play）:** 判定 C の実行担当が、GO 候補 head から発行側の新しい detached worktree A（root の長さは `tools/path-budget.json` の予算内）を作り、Editor を開かずに BS5b-1 の batch で `-osmPurpose spring-full` を publish する。publish の完了は、結果 file が `exitCode` 0 の成功の形で現れ、その `publishPath` の directory に `transport.json` があることで判定し、Unity process の終了を待たない（成功後に終了しない既知の制限。#52）。結果 file の確定の後も process が残れば、今回起動した Unity の PID だけを確かめて終了させ、その事実を記録する（原因は調べない。AGENTS.md の既知の native 終了 stall の規則）。その `last-result.json` と published directory を、A とは別の checkout である受け取り側の本スライスの worktree B（同じ GO 候補 head）の Editor（開いている Scene は untitled の空 Scene だけ、Delivery 窓の ledger 無し）の From published directory で選び（`publishPath` は同じ PC で読めるのでそのまま使う）、検証 Play する。負例を先に 1 回: `last-result.json` の写しの `manifestSha256` の 1 文字を変えて選ぶ → 拒否、cache root に `installed/<contentSet>/<identity>` が現れない（正例の後では既に install 済みで見分けられないので、負例を先にする）。次に正例。観測と期待: install の成功、記録の表で `last-result.json` の identity と起動した directory の identity が一致、`SceneDirector.IsSceneStable("Title")` が真（D5 のログ）、封鎖の 1 行、不一致 Warning 無し、区間の Error 0、停止後の環境の復元（AC17 (b) と同じ記録の項目）。
  - 順序は review-evidence の「高コスト証拠と最終証拠ラウンド」に従う（判定 C → 暫定 C' → HC2 → 最終判定 C と最終 C'）。
- **判定 C の照合 script の方式:** 使わない（A3 で確定。HC2 と AC17 の判定は記録の表、印で切った Console ログの区間、環境の前後の比較の目視と、拒否の有無で、機械向けの期待値が無い）。
- **機械検査:** `pwsh tools/contract-audit.ps1`（`#nullable enable`、Editor コードの配置、path 予算等）。
- **A0/A1 主担当・モデル・ベンダー:** Claude Opus 5.5（Anthropic）、2026-11-01。
- **A2 独立レビューごとの観点・担当・モデル・ベンダー、入力版:** BS5b-1 の HANDOFF（最終版は commit `95e4a270`） §5 と同じ（分割前の BS5b 全体を 1 回で見た）。入力版は A2 入力版 `bb671da6` の分割前の BS5b の HANDOFF（内容 sha256 `8ba57df617087d29229b4d059d94b605ad402e500481a83555a5ceffb0f315a6`）。(R1) アーキテクチャゲート: Grok 4.7 high（xAI）。(R2) A0 だけからの代替構成: Claude Opus（Anthropic）。外部レビュー（2026-11-02）。
- **A3 統合担当・モデル・採否:** Claude Opus 5.5（Anthropic）が統合し、発注者が採否を確認した（2026-11-03）。採否は下の表の #35・#47〜#54。
- **revision 3（2026-11-03）:** 起点は revision 2 の A3 凍結版 `7b931743` への、Phase C / C' で反証が出る箇所を先回りした外部レビュー（発注者が受け取った。判定「設計の方向性は GO、P1 4 件は実装前に契約を小さく明確化」）と、主担当の自己点検（Editor の設定と `eval` の運用の実測に照らした）。採否は下の表の #66〜#80。その反映を revision 3 の A2 入力版 `15bfbe33`（HANDOFF の内容 sha256 `fe495229d95bebe5ad4b5c5f5d6cd83ca7659a3e54b2c13ec92be3459f48a05c`）とし、A2 は独立計画レビュー 1 件（発注者が受け取った外部レビュー、判定「設計方針は GO、P1 2 件の最小修正を推奨」）。発注者の方針で、不変条件に違反するか偽の主張になる指摘だけを採り、ほかは理由を付けて採らない。採否は #81〜#87。凍結前の差分の再レビューはしない（直したのは判定式 1 つと文言 2 箇所）。
- **revision 2（2026-11-03）:** 起点は revision 1 の A3 凍結版 `383852e6` への A3 後の独立レビュー（発注者が受け取った外部レビュー。計画・契約レビュー、判定「限定的な Phase A revision を推奨」）。採否は下の表の #55〜#60。その反映を revision 2 の A2 入力版 `51b9eb89`（HANDOFF の内容 sha256 `032f75ea414f562c3fe3269fd635a71565f98440862fe18e5447cce9c9bbe71e`）とした。A2 は差分レビュー 1 件: Grok 4.7 high（xAI、cursor-agent）。入力は A2 入力版と revision 1 の HANDOFF、その差分、起点のレビュー、関係ソースの写しで、計画対象は実装済みと仮定する枠を指示した。判定「修正後に凍結可」（blocker 2・minor 3）。Grok は revision 1 の A2 R1 に関与しているので**独立性制約あり**（Codex は 2026-11-04 22:42 まで使えない）。採否は下の表の #61〜#65。
- **C' 用に予約した担当・モデル・ベンダー:** OpenAI（Codex）。A2 では実行していない。BS5b-1 の C' は Grok で行ったので、Codex は本スライスに未関与のまま。利用上限で 2026-11-04 22:42 まで使えない（BS5b-1 の HANDOFF の記録）。C' の時点で使えなければ選び直す。revision 2 の A2 も Grok で、Codex は引き続き未関与。
- **独立性の強化条件を満たせない場合の理由:** R2 は主担当と同じ系列（Anthropic）。A0 だけを読む制約で初稿へのアンカリングを断った。

### A2 の指摘と採否（A3 統合の入力）

番号は分割前の BS5b の通し番号。本スライスに関わるものだけを置き、残りは BS5b-1 の HANDOFF（最終版は commit `95e4a270`） の同じ節にある。#1〜#21 は A2 の R1・R2、#22〜#33 は外部レビュー（`develop` の `04445d52` で照合）、#34〜#41 は A3 の自己点検、#47〜#50 は分割後の外部レビュー（S-、2026-11-02）、#51〜#54 は BS5b-2 の A3（BS5b-1 の実物の確認と、問いとは独立した要求の仕分け、2026-11-03）。

| # | 指摘（出所） | 採否 | 理由と反映先 |
|---|---|---|---|
| 1 | `EnteredEditMode` の Reset が Edit mode の domain reload でも走ると、Use For Next Play の直後の再コンパイルで値が消える（R1 blocker） | 採用（revision 2 で #56 に置き換え） | 当初は「Play に入った」印がある時だけ戻した。revision 2 では、Edit mode の domain reload で戻さないのは「手で戻す」ledger（窓）だけとし、「1 回の Play で戻す」ledger は Play に至らなければ戻す。I3・AC3・D2 |
| 2 | Delivery 窓の手入力に Play 終了の Reset を足すのは I6（手入力経路を変えない）に反する（R2） | 採用 | 窓は「手で戻す」のまま。AC4 |
| 3 | 経路どうしの値が混ざる（Play As の後に検証 Play 等）（R2） | 採用（#26 で見直し） | 「1 回の Play で戻す」ledger どうしは先に Reset。手の ledger は #26。AC3・AC4・D2 |
| 4 | UICommon は `Resolve` の selected に入らず、AC5 の検査から漏れる（R1 major） | 採用 | `UiCommonPath` を足し、`UIScene.unity` の負例。AC5 |
| 5 | U1 が未決のまま Runtime を変える配置で凍結しようとしている（R1 major） | 採用 | U1 を D5 で閉じ、代案は不採用。U1・D5 |
| 8 | §1.3 の UICommon の再利用と「空 Scene は何も起動しない」の記述が不正確（R1 minor） | 採用 | §1.3 |
| 9 | 失敗経路の初回 Scene の unload が Editor でも走る（R1 minor） | 採用 | AC8・D5 |
| 11 | 宣言名の引きは既存の `SampleGameBuildPurposes.Find` で足りる（R1 minor） | 採用 | D10 |
| 12 | `VerifyPlayLauncher` の段の列と Editor I/O の境界が無い（R1 minor） | 採用 | 段の列を pure な `VerifyPlaySequence` に。D4・§3 |
| 13 | bridge の後に `EnterPlaymode` が失敗すると値が残る（R1 minor、R2 も同旨） | 採用 | 失敗時に Reset。I3・AC6 |
| 14 | UC7 は名前だけでなく、手元の宣言と manifest の宣言の中身も照合する（R2） | 採用 | 表現・`RuntimeDefaults` が違えば拒否。AC15・D10 |
| 15 | `AddScene("Title")` だけで Title が進むかが最大の不確実点（R2） | 不採用（記録だけ） | BS5a の AC13 で `AddScene("Title")` による directory からのロードは観測済み。E2 は `Title` の Stable までで、先へ進むことは判定しない。D5 に記録 |
| 16 | profile が宣言の表現と一致すれば通す（R2） | 不採用 | profile は表現以外も固定しうる。選ばれていれば拒否する A1 の方が単純で、AC2 で閉じる |
| 17 | 検証 Play は「保存済み Scene を 1 つも開かない」に強め、経路が空 Scene を開く（R2） | 一部採用（#22・#36 で見直し） | 拒否の対象は起動の再利用の経路（#22）。経路が黙って Scene を閉じることはせず、menu からは確認の dialog で閉じて続ける（#36） |
| 19 | 宣言ごとの menu ではなく 1 つの窓にする（R2） | 不採用 | 既存の `Build <contentSet>` menu と揃える。From published directory は Delivery 窓に足す（D9） |
| 20 | 宣言と config の不一致 Warning の配線の自動テスト（R2） | 保留 | 後続（§2.5「検証の残り」）。E2 は Phase C の観測で Warning が無いことを見る |
| 22 | 選択外でも SceneResourceMap にある Scene が開いていれば、起動が名前で登録・再利用するので UC4 を証明できない（外部 R1。`RegisterAlreadyLoadedScenes`、SceneResourceMap の asset は選択で絞られない） | 採用（#17 を一部見直し） | 対象を起動の再利用の経路に揃える。I2・AC5・AC17 (d) |
| 23 | 環境変数を置いても、Editor のコマンドライン（config の最上位）が上書きしうる（外部 R2） | 採用 | 制御する key の競合を検査して拒否。I1・AC2・§2.0 |
| 24 | From published directory は副作用より先に拒否条件を検査する（外部 R3） | 採用 | SHA → identity → 宣言 → 開始前検査 → install → 直前の再検査 → bridge。AC6・AC15・D4 |
| 25 | 宣言の照合は manifest が記録する欄までで、別版の宣言の拒否とは言えない（外部 R4） | 採用 | 保証の言い方を記録された欄の一致に限る。D10・§2.0 |
| 26 | 手の ledger（Delivery 窓）を別の経路が自動で消すのは手の契約に反する（外部 R5。#3 の見直し） | 採用 | 手の ledger があれば拒否して Reset を案内。v1 は手として読む。AC3・AC4・D2 |
| 30 | 標準の操作の手入力を減らし、選ぶものと決まるものを UC ごとに書く。cache root の毎回の入力を要求しない（外部 §2） | 採用 | D11 の表、E7、cache root の既定（repo の外。repo 内は path の予算を超える）、結果 file の選択 |
| 31 | App が独自 enum を定義できる設計、型付きの選択肢の catalog（外部 §2.2） | 不採用（今回） | 宣言から決まるので BS5b に App の enum は要らない。新しい型の仕組みは作らない（D11） |
| 32 | `last-result.json` から SHA-256 を読み込んで入力を省く（外部 §2.1） | 採用（発注者の判断で不採用から変更） | 結果 file を published directory と別の経路の期待値とし、identity の照合も足す。64 文字の手入力をやめる。E5・AC15・D9・D11・HC2 |
| 33 | Phase B の中の段階と確認地点（外部 §3） | 採用（2 スライスへの分割に置き換え） | 発注者の判断で BS5b を BS5b-1 と BS5b-2 に分けた。本スライスの中の確認地点は §4.1 |
| 34 | compile error 中の `EnterPlaymode` は Play に入らず、印の無い ledger が残る（A3 の自己点検） | 採用 | Play As と検証 Play は compile error 中に拒否。AC2・AC6 |
| 35 | SampleGame の Editor asmdef は Foundation を参照せず、`CommandLineConfigProvider` を直接使えない（A3 の自己点検） | 採用（対応は #47 で置き換え） | 事実は採用。Runtime に helper を置く対応は #47 で取りやめた |
| 36 | UC4 で開いている Scene の拒否は、利用者が手で閉じて操作をやり直す（A3 の自己点検） | 採用 | menu からは確認の dialog で閉じて続けられる。dialog の無い呼び出しは拒否。AC5・D11 |
| 37 | Delivery 窓の ledger がある時の拒否も、窓を開いて Reset してからやり直す（A3 の自己点検） | 採用 | 同じ確認の dialog で Reset して続けられる。黙っては消さない。AC4・D11 |
| 38 | UC4 の manifest SHA-256 も自己一致ではないか（A3 の自己点検） | 変更なし | 期待値の出所は同じ操作の build で、外から受け取る値が無い。照合は不要と D11 に明記 |
| 39 | Variant profile の拒否・コマンドラインの競合・compile error も dialog で直せるか（A3 の自己点検） | 不採用 | profile は利用者の永続設定（D3）、コマンドラインと compile error は経路が直せない。拒否と案内だけ |
| 41 | 分割で、HC2 は BS5b-1 の HC1 の published directory を使えなくなる（A3 の自己点検） | 採用 | HC2 は自分の GO 候補 head の別 worktree で batch から publish してから受け取る。§5 |
| 47 | Editor 専用の検査のために Runtime の `SampleGame.DependOnAll` に public helper を足すのは、Player に入る公開面を Editor の都合で増やす（分割後の外部 S-P1-B） | 採用（発注者の判断、2026-11-03） | `SampleGame.DependOnAll.Editor` に `OneStarMaker.Foundation` の参照を 1 本足し、`CommandLineConfigProvider` を Editor から直接使う。Game → Framework の向きは変わらない。`SampleGameConfigLayers` は作らない。§1.2・§3・AC2 |
| 48 | BS5b-1 が実際に書いた成功・失敗の結果 file を、読み手で確かめる小さな境界テスト（分割後の外部 S-P1-C） | 採用 | 読み手は書き手と同じ `SuccessFile` の型で読み（同じ asmdef。型の定義を 1 つにする）、テストの結果 file は `SuccessFile` / `FailureFile` を書いて作る。AC の追加ではなく AC15 の検証方法。新しい schema の library は作らない。AC15 |
| 49 | 「1 操作」は事前条件（profile 未選択等）を満たす時だけ。既定の cache root の長さは環境で変わる（分割後の外部 S-P1-D） | 一部採用 | 事前条件は E1 に書く。cache root の path の事前検査と案内は、手入力の install にも同じく要る DIST 側の独立した要求なので、仕組みとして後続へ送る（§2.5）。この PC の既定の cache root は約 200 文字の見積もりで上限 259 に収まり、AC17 (b) と HC2 の install で実際に確かめる |
| 50 | 今 enum を足さないことと、将来 App 固有の enum を定義できない設計にすることは別。static な 6 件の menu は宣言を足しても自動では増えない（分割後の外部 S-P2-E） | 一部採用 | 宣言を足す担当が menu も足す責務を D1 に書き、足し忘れを見つける AC19 のテストを足す。menu の生成は後続（§2.5）。App の選択肢の型の仕組みは需要が出るまで作らない（D11） |
| 51 | 失敗の結果 file は `exitCode` と `reason` だけで、Unity が batch の method の前に終わると前回の file が残る（A3 の BS5b-1 の実物の確認） | 一部採用 | AC15 は `exitCode` 0 以外と欄の欠けを拒否する。結果 file が最新の実行の結果であることは保証しないと §2.0 に書く（受け取りは結果 file と directory の組の一致を照合する）。前回の file を今回の結果と読まないことは、Unity の起動の仕方の問題なので batch の起動 wrapper として後続へ送る（§2.5） |
| 52 | 成功した batch の後に Unity process が正常に終了することは BS5b-1 で示せていない（A3 の BS5b-1 の実物の確認） | 採用（HC2 の手順） | HC2 の publish の完了は結果 file と directory で判定し、process の終了を待たない。終了しない時の扱いの仕組みは batch の起動 wrapper（後続、§2.5） |
| 53 | Delivery 窓の宣言対応（宣言の表示、表現の違いの表示、Use For Next Play で `RuntimeDefaults` を置く。旧 AC16 と旧 E5 の後半）は、標準の操作に数えない手入力経路の改善で、このスライスの問いとは独立した要求。I6（手入力経路を変えない）とも近い（A3 の見直し、発注者確認） | 採用（後続へ移送） | AC16 を欠番にし、E5 を From published directory だけにした。宣言と食い違う config は既存の不一致 Warning が出るので黙っては通らない。§2.5 |
| 54 | D5（Editor directory Play の初回 Scene）は Framework の起動の独立した変更理由で、別スライスにできる（A3 の見直し） | 不採用（分けない） | E2 と UC7 の判定にそのまま要り、変更は分岐の分離の約 20 行。別スライスにすると Phase A を一周回す費用に見合わない。§4.2 の差し戻し条件で Runtime の膨らみを止める（revision 2 の外部レビューも同意） |
| 55 | UC7 の bridge が初回 Scene を置く契約が無く、AC8 だけでは From published directory の初回 Scene が空のまま。単一の `Title` の定数では、目的ごとの起動地点という App の責務を表せない（revision 2 外部 P1-A） | 採用 | 初回 Scene は App が宣言ごとに決める（D12、発注者の明確化）。SampleGame の `SampleGameBuildPurposes` に宣言から初回 Scene を返す関数を置き、検証 Play・From published directory・BS4 coordinator が使う。Framework と manifest には足さない。合成の宣言で `Title` 以外にした変換テスト（AC9）、ロードの失敗で fallback しない負例（AC8）、manifest が記録しない限界（§2.0）。§1・E2・E5・AC7〜AC9・AC15・AC19・D5・D11 |
| 56 | `ExitingEditMode` は Play への移行の開始で到達ではない。Play に至らない移行で ledger と環境が残りうる。`EnterPlaymode` の直前に compile 中・移行中も再検査する（revision 2 外部 P1-B） | 採用 | 「1 回の Play で戻す」ledger は `EnteredEditMode` で戻し、Play に至らない時は update・domain reload 後の初期化で `isPlayingOrWillChangePlaymode` が偽なら戻す。「Play に入った」印は使わない。直前の再検査に compile 中・asset の更新中・移行中を足す。状態機械や transaction 基盤は作らない。I3・AC3・AC6・D2 |
| 57 | `Full` の空文字を環境変数に置くと key の削除と同じになり、下位の `app-config.json` の値が使われる（revision 2 外部 P2-A） | 採用 | `Full` は「置かない」として扱い、下位の層の `app-config.json` の同じ key が空でなければ Play As を拒否する。実効 config の観測を AC17 (e) に足す。AC1・AC2・AC17 |
| 58 | 既存の窓の Prepare は `cacheRoot` を直接 `ContentCacheStore` に渡し、空を拒否する。標準の操作の既定の解決が実装の境界に無い（revision 2 外部 P2-B） | 採用 | 検証 Play と From published directory が共有する解決の関数と、`cacheRoot=""` から install できる EditMode（AC20）。窓の手入力の Prepare は変えない（I6）。D4 |
| 59 | UC4 の証明の範囲（初回 Scene の起動までか、目的に固有の content までか）を明示する（revision 2 外部 P2-C） | 採用（文言） | 示すのは初回 Scene の Stable まで。目的に固有の Scene を 1 件ロードする証拠は足さない（任意とされた。足すと問いの範囲が広がる）。§2.0・D12 |
| 60 | 「途中で失敗すれば Play に入らない」は Play を要求する前の段に限る。要求の後の起動の失敗は既存のログで判定する（revision 2 外部 P2-D） | 採用（文言） | I5 を限定し、起動後の失敗のための lifecycle や receipt は作らない。I5・AC8 |
| 61 | `EnterPlaymode` の後の update ではフラグが真で、その後 `ExitingEditMode` も domain reload も無いまま要求が取り消されると、update が 1 回だけでは one-shot の環境が残る（revision 2 A2 blocker） | 採用 | update の handler は「1 回の Play で戻す」ledger がある間は毎回見る。負例を AC3 に足した。I3・AC3・D2 |
| 62 | `Full` の Play As は `assets:sceneVariant` を置かないので、外の process 環境とコマンドラインの検査に掛からず、空の app-config の上にどちらかがあれば実効値が空にならない（revision 2 A2 blocker） | 採用 | 置かない key も制御する key として外の環境とコマンドラインの検査に掛け、app-config の空と合わせて実効値を固定する。AC2 |
| 63 | D1 の共有変換の `Full`→空が、検証 Play の `content:representation`（主値の文字列）に流れると directory 起動が拒否する（revision 2 A2 minor） | 採用 | 空への写像は Play As の `assets:sceneVariant` だけと D1 に書いた。D1 |
| 64 | AC4 の「未使用」の one-shot ledger は、#56 で消した印に依っていた（revision 2 A2 minor） | 採用 | 「残っていて Play 中でも移行中でもない」と定義した。AC4 |
| 65 | AC15 の直前の再検査が AC6 で足した項目を指していない（revision 2 A2 minor） | 採用 | AC6 と同じ項目と明記。AC15 |
| 66 | install の待ちの間に同じ・別の検証 Play や From published directory を始められ、どちらの成果物で Play するか、遅れた継続が後の環境を変えないかが決まっていない。窓が閉じた時の取り消しの所有者も無い（revision 3 外部 P1-1） | 採用 | 両経路で共有する 1 つずつの印（static、`finally` で外す）、窓が閉じたら From published directory を取り消す（既存の Prepare と同じ）、menu の検証 Play は続く、取り消し後の継続は bridge に達しない。汎用の job 基盤は作らない。AC6・§3 |
| 67 | `EnterPlaymode` の直前の再検査が profile と外の process 環境の値を含まず、install の待ちの間の変化で I1 を破りうる（revision 3 外部 P1-2） | 採用 | 直前の再検査に AC2 の可変な条件を足し、負例を足した。コマンドラインは process の間変わらないので再読しない。AC6 |
| 68 | AC17 (c)(d) の「build されない」を `content/` の無増加で見ると、build は Unity の build の前に identity と `preflight.json` を作るので反証される（revision 3 外部 P1-3） | 採用 | 観測を `reports/` の新しい identity の無さに変え、AC6 で build の段の呼び出し 0 回を確かめる。`content/` は補助。AC6・AC17・§5 |
| 69 | `Title` の Stable の観測点が決まっておらず、Player 用の内部段の名前はログに出ない。Unity の Scene のロードは `SceneDirector` の Stable と同じではない（revision 3 外部 P1-4） | 採用 | D5 で `SceneDirector.IsSceneStable(firstScene)` を identity と Scene 名とともに 1 行ログに出し、それを観測の正にする。D5・AC17・HC2 |
| 70 | `Full` の下位の層の検査は disk の `app-config.json` を見るが、起動は Addressables の TextAsset として読む（revision 3 外部 P2-1） | 採用 | Play As は Addressables の Play Mode が Use Asset Database でなければ拒否し、既存 build の古い bundle を読む経路を塞ぐ（bytes の一致は保証しない。revision 3 の A2 の #84 で文言を改めた）。実効 config は起動した `AppConfig` で観測する。AC2・§1.3・§5 |
| 71 | 戻した key の一覧のログだけでは、前の値へ実際に戻ったことを示さない（revision 3 外部 P2-2） | 採用 | 停止後に同じ process の環境を読み直し、key ごとの前後の一致（`null` と空文字を区別）を記録する。§5 |
| 72 | AC19 の reflection は menu の path があることしか示さず、本体の結線を示さない（revision 3 外部 P2-3） | 採用 | 集合・件数は EditMode、結線は path と本体が同じ定数を使う形にして発見 C のレビューで照合すると正確に書いた。AC19 |
| 73 | Phase C のログの run の境界が無く、前の run の成功を今回と読みうる。identity の一致も記録されない（revision 3 外部 P2-4） | 採用 | 記録の表（run ID、開始・終了の印、宣言、identity、path、初回 Scene、環境の比較、区間の Error / Warning）。照合 script は作らない。§5 |
| 74 | AC9 の合成の初回 Scene をどこに注入するかが不明（revision 3 外部 P3-1） | 採用（文言） | 変換に引数で渡し、production の宣言の一覧に合成の宣言を登録しない。AC9 |
| 75 | 窓に出す cache root が、手入力の Prepare でも既定が使えるように読める（revision 3 外部 P3-2） | 採用（文言） | 標準の経路が使った実効の cache root として表示。Prepare は変えない。D4 |
| 76 | project は Play に入る時の domain reload を切っている。AC3 の「Play 中の domain reload」と `[InitializeOnLoad]` の戻しは、Play 中の再 compile の時だけ働く（revision 3 自己点検） | 採用（事実の記録） | §1.3 に記録。AC3 は handler を直接呼ぶ EditMode で両方の場合を確かめ、操作は今の設定で行う。設定は変えない |
| 77 | `eval` から始めた検証 Play の install の継続は Editor の tick で進み、Editor が idle だと止まって見える（revision 3 自己点検。BS5a の運用で `delayCall` が idle で動かなかった実測） | 採用（手順） | 進まない時は `eval` で tick を起こすと §5 に書いた。判定は変えない |
| 78 | HC2 の負例を正例の後に行うと、同じ revision が既に install 済みで「install されない」を見分けられない（revision 3 自己点検） | 採用（手順） | 負例を先に行い、`installed/<contentSet>/<identity>` が現れないことを見る。§5 |
| 79 | cache root の既定は `LocalApplicationData` の下で、実行環境によっては shell から見える場所が Editor と違う（revision 3 自己点検） | 採用（手順） | Editor と同じ環境の pwsh で見ると §5 に書いた |
| 80 | HC2 の新しい worktree の root が長いと、Unity が長い path を開けない（revision 3 自己点検。常時契約の path 予算） | 採用（手順） | root の長さを `tools/path-budget.json` の予算内にすると §5 に書いた |
| 81 | `ExitingPlayMode` の間は `isPlaying` が真のまま `isPlayingOrWillChangePlaymode` が偽になり、AC3 の「偽なら戻す」が Edit mode に戻る前に環境を戻す。I3 と矛盾する（revision 3 A2 P1-A） | 採用 | 放棄された ledger を戻す条件を `isPlaying` と `isPlayingOrWillChangePlaymode` がどちらも偽の時に限り、Play を抜けた戻しは `EnteredEditMode` で行う。2 値を注入した負例を AC3 に足した。AC3・AC4・AC6 |
| 82 | HC2 の「この worktree（別 checkout）」が、発行に使った新しい worktree とも読め、§2.0 の「別 checkout で受け取る」を示せない（revision 3 A2 P1-B） | 採用（文言） | 発行は新しい detached worktree A、受け取りは本スライスの worktree B（同じ GO 候補 head）と書いた。§5 HC2 |
| 83 | 非同期の run の `end` の印を付ける時点が決まっていない（revision 3 A2 P2-A） | 不採用 | 印は区間を切るためのもので、観測を終えてから付けるのが自然な読み方。読み違いが出ても決定記録で読み方を確定すれば足り、合否を変えない |
| 84 | AC2 が「検査した file と起動が読む bytes を一致させる」と、Play Mode の検査だけでは示せない保証を書いている（revision 3 A2 P2-D） | 一部採用（文言） | 主張を弱め、Play Mode の検査は既存 build の古い bundle を読む経路を塞ぐだけ、実効値の証拠は AC17 (e) と書いた。検査を AssetDatabase の TextAsset に替えることはしない（新しい例外を足さない）。AC2・#70 |
| 85 | AC15 の `publishPath` が「無い」が、欄の空と、手元に path が無いことのどちらかが曖昧（revision 3 A2 P2-B） | 不採用 | 本文は「手元に有ればそれ、無ければ folder の選択」、空の欄は拒否と書き分けており、読めば決まる |
| 86 | 結果 file の `buildMode` が `Full` かを照合していない（revision 3 A2 P2-C） | 不採用 | 新しい検査になる。publisher は `Full` 以外を publish せず、受け取りは SHA と identity で結果 file を directory に結び付けているので実害が無い |
| 87 | AC6 の取り消しの期待を、窓が持つ From published directory と menu の検証 Play で分けて書く（revision 3 A2 P3） | 不採用 | AC6 は既に持ち主ごとに分けて書いている |

## 6. Phase B 実装結果

- 実装（§4.1 の順。各段の終わりに compile と差分の自己点検）:
  - 段 1: `PurposeLaunchSettings`（宣言 → config key=value・環境変数名。Play As は `Full` の `assets:sceneVariant` を置かずに `Controlled` と `RequiresEmptyLowerLayer` に挙げる。directory の key 集合 `DirectoryPlay` は content の 5 key・初回 Scene・runtime 既定値）。`ContentDeliveryPlayBridge` を共通 ledger に広げた（`Apply`（窓、手で戻す、従来の 5 key と `ValidateInstalled`）、`ApplyForOnePlay`、`FindForeignValues`、`HasManualLedger` / `HasOnePlayLedger`、`HandlePlayModeStateChanged`（`EnteredEditMode`）、`HandleIdle(isPlaying, isPlayingOrWillChangePlaymode)`（update と `[InitializeOnLoad]` の初期化から）。ledger の key は v1 のまま `resetAfterOnePlay` の 1 欄を足し、欄の無い v1 は手で戻す）。`PlayAsLauncher`（menu 6 件、Play mode・compile の拒否、Delivery 窓の ledger の確認 dialog、profile・外の値・コマンドライン（`CommandLineConfigProvider`）の拒否、app-config の下位の層の検査（`JsonConfigFlattener`）、Addressables の Play Mode が `BuildScriptFastMode` でなければ拒否）。asmdef に `OneStarMaker.Foundation` を 1 本足した（#47）。
  - 段 2: `AbstractApplicationInitializer` の Editor directory 分岐で `content:firstScene` を読み（`ReadEditorFirstScene`）、初回 Scene のロードを Editor（stage `load-editor-first-scene:<Scene 名>`、ロード後に `[AppInit] Editor content directory first scene: scene=… identity=… stable=…` の 1 行）と Player（従来の段と receipt）に分けた。失敗時の初回 Scene の unload は `RunsPlayerContentSmoke`（Player だけ）。新しい状態・所有者・寿命・公開 API は無い（internal static の判定 2 つ）。
  - 段 3: `VerifyPlayLauncher.cs` に `VerifyPlaySequence`（段の列、bridge 以降の失敗の Reset、共有の 1 つずつの印、bridge の直前の取り消しの確認）と `VerifyPlayLauncher`（menu 6 件、開始前検査（Scene の再利用の判定 `FindReusableScenes`、確認 dialog で閉じて空 Scene で続ける）、`PurposeStages`（`BuildResult(Full)` → `ContentTransportPublisher.Publish` → manifest の宣言の照合）、cache root の解決 `ResolveCacheRoot`）。`SampleGameBuildPurposes.FirstSceneOf`（D12、6 宣言とも `Title`）を足し、BS4 coordinator の直書きを置き換えた。
  - 段 4: `ReceivedStages`（結果 file を `SuccessFile` で読み、`ValidateResultFile` → `ResolvePublishedDirectory` → `ValidatePublished`（SHA-256 → identity → 宣言）→ 開始前検査）。Delivery 窓の Published directory モードに「Select batch result and Verify Play」と前提の 1 行、標準経路の実効 cache root の表示を足した。窓が閉じたら既存の `_cts` で取り消す。手入力経路（Prepare・Use For Next Play の値）は変えていない（Use For Next Play は残った「1 回の Play で戻す」ledger を先に戻すだけ）。
  - テスト（Phase B では未実行）: `PurposeLaunchSettingsTests`（AC1・AC5・AC7・AC9・AC19・AC20 の解決、コマンドライン・下位の層）、`ContentDeliveryPlayBridgeTests`（AC2 の外の値・AC3・AC4 の v1）、`VerifyPlaySequenceTests`（AC6）、`PublishedDirectoryReceiveTests`（AC15・AC20 の install・AC4 の窓と 1 回の Play の ledger）、`PlayerContentConfigurationTests` に AC8 の 2 件。
- HANDOFF との差:
  - 規模: 製品コードは見積もり 400〜500 行に対し約 1,000 行（新規 3 file 776 行、既存 7 file +285 / −36）。`VerifyPlayLauncher.cs` は見積もり 240 行に対し 499 行。増えたのは段の受け渡しの型（`PublishedContent`・`InstalledContent`・`VerifyPlayOutcome`。`ContentInstallResult` の ctor は internal で偽の段から作れないため）、From published directory の照合関数（AC15 を単体で確かめるために pure に分けた）、2 経路が共有する段の基底 `DirectoryPlayStages`（初回 Scene の決定を差し替えて両経路の bridge を AC9 で確かめるため）と、理由のコメント。段の順序のクラスは開始前検査の規則から独立したまま（§3 の分け方）で、ファイルの分割はしていない。Runtime は D5 の分岐の分離（+26 行）に留まった。
  - 段の interface は §2.4 D4 の「段を interface で受ける」を `IVerifyPlayStages` とした。開始前検査の共通部分（profile・ledger・外の値・コマンドライン）は §3 の配置どおり `PlayAsLauncher` の internal static に置き、`VerifyPlayLauncher` が呼ぶ。
  - AC5 の確認 dialog・AC4 の Reset の dialog は menu・窓からの呼び出しだけ（`interactive`）。`eval` は `PlayAsLauncher.Launch(decl, false)`・`VerifyPlayLauncher.VerifyAsync(decl, false)`・`VerifyPlayLauncher.ReceiveAsync(path, chooseFolder, false, token)` を呼ぶ。
  - B 適応の観測ログ（§5 の「必要な検証支援」）は足していない。実効 config と停止後の環境を `eval` で読めるかは動作確認で確かめる。
- 未実行: Unity テスト（新規・既存とも）、content build、publish、batch、Play の実行と判定（Phase C）。AC17 の操作は動作確認と Phase C。
- 実施した確認: Editor（6000.6.0f1、この worktree の `unity/`）で compile（`scriptCompilationFailed=False`、変更した file の warning 0）、menu `Tools/OSM/Play As/*` と `Tools/OSM/Content/Verify Play/*` が 6 件ずつ登録されること（`Unsupported.GetSubmenus`）、`pwsh tools/contract-audit.ps1`（errors=0 warnings=0）。
- 作業ツリーの注意: Editor を開くと `unity/Assets/MobileDependencyResolver/Editor/**/*.pdb.meta` 5 件が消える（この worktree に `.pdb` が無いため）。実装の差分ではないので commit に含めず、`git checkout` で戻した。
- implementation head commit: §0 の欄を正とする。
- Phase B 担当・モデル・ベンダー: Claude Opus 5.5（Anthropic）、2026-11-03。

### 動作確認

- 対象 head: `eb3df5fa`（Phase B の完了 head。implementation head と同じ）。実行担当: 動作確認の session（B とは別の session。Claude Opus 5.5（Anthropic））、2026-11-03。
- 経路: この worktree の `unity/` を Unity CLI の `open` で起動（6000.6.0f1）し、`tools/unity-editor.cmd eval` だけで操作した。入口は §6「HANDOFF との差」の dialog の無い 2 つ（`PlayAsLauncher.Launch(decl, false)`・`VerifyPlayLauncher.VerifyAsync(decl, false)`。internal なので reflection で呼んだ）。run ごとに Console へ `[BS5b2-evidence] begin/end <runId>` を出し、`unity/Logs/Editor.log` をこの印で切った。終了後に自分で起動した Editor を未保存変更が無いことを確かめて `EditorApplication.Exit(0)` で閉じた。
- 初期状態（全 run 共通、run の直前に `eval` で読んだ）: Play mode でない、compile error 無し、Variant profile の resolver が `null`（未選択）、Addressables の Play Mode が `BuildScriptFastMode`、ledger（`OSM.ContentDelivery.PlayBridge.v1`）が空、process 環境に `SAMPLEGAME_*` の key が 1 つも無い、Editor のコマンドラインに `--` で始まる引数が無い。
- **§5「未知の操作経路」の 2 点:** どちらも `eval` で読めた。B 適応の観測ログは要らない。
  - 起動後の実効 config: Play 中に `SampleGame.DependOnAll.AppInitializer.s_instance`（static）の基底の `_config`（`AbstractApplicationInitializer` の private field、`AppConfig`）を reflection で取り、`ContainsKey` / `GetString` で読む。Play に入った直後の数回は Play への移行で `eval` が 5 秒の timeout になり、その後に読めた。
  - 停止後の process 環境: `ExitPlaymode()` の後、`isPlaying=False` を確かめてから同じ Editor process の `Environment.GetEnvironmentVariable(key, Process)` を読む。`null` と空文字を区別して記録できる。

| run | 操作 | 生の観測 | 期待との差 |
|---|---|---|---|
| smoke-a（AC17 (a)） | `OutGameScene` を開き Play As `spring-planner-whitebox` | 戻り値 `null`（拒否なし）。ログ `[PlayAs] spring-planner-whitebox content:runtimeMode=addressables assets:sceneVariant=Whitebox world:cellCompanionSet=Planner`。Play 中の ledger は 3 key（previous は全て `<null>`、`resetAfterOnePlay:true`）。Play 中の実効 config: `content:runtimeMode='addressables'`、`assets:sceneVariant='Whitebox'`、`world:cellCompanionSet='Planner'`（`content:firstScene` は無い）。停止後: 8 key（content の 5 key・`ASSETS__SCENEVARIANT`・`WORLD__CELLCOMPANIONSET`・`CONTENT__FIRSTSCENE`）とも `<null>`、`SAMPLEGAME_*` の key 無し、ledger 空。区間の Error は `eval` の timeout（Pipeline の `Failed to handle /api/exec request`）2 件だけ、Warning は DebugStudio への接続失敗（`[DebugSocket] Failed to connect`）と IPC の 1 行 | なし |
| smoke-e（AC17 (e)） | 同じ初期状態で Play As `spring-full` | 戻り値 `null`。ログ `[PlayAs] spring-full content:runtimeMode=addressables world:cellCompanionSet=Full (empty from app-config: assets:sceneVariant)`。Play 中の環境に `SAMPLEGAME_ASSETS__SCENEVARIANT` は無く（`<null>`）、ledger は 2 key。Play 中の実効 config: `content:runtimeMode='addressables'`、`assets:sceneVariant` は key があり値 `''`、`world:cellCompanionSet='Full'`。停止後: 8 key とも `<null>`、`SAMPLEGAME_*` 無し、ledger 空 | なし |
| smoke-c（AC17 (c)） | `Spring_Cell_0_4` を開き Verify Play `spring-full` | task は同期で完了し、outcome `Rejected at Start: Open scenes that startup would reuse instead of the content directory: Assets/SampleGame/InGame/InGameSession/Seasons/Spring/Cells/Spring_Cell_0_4/Spring_Cell_0_4.unity`。同じ文の `[VerifyPlay] verify spring-full: Rejected at Start: …` の LogError 1 件が区間の唯一の Error。run の前後で `artifacts/bs2b/` の `reports`・`content`・`transport`・`staging` の一覧（名前と更新時刻）に差なし。前後の環境は 8 key とも `<null>`、ledger 空 | なし |
| smoke-d（AC17 (d)） | `Summer_Cell_0_0` を開き Verify Play `spring-full` | outcome `Rejected at Start: … Assets/SampleGame/InGame/InGameSession/Seasons/Summer/Cells/Summer_Cell_0_0/Summer_Cell_0_0.unity`。区間の Error は同じ拒否のログ 1 件だけ。`artifacts/bs2b/` の 4 つの一覧に差なし。前後の環境は 8 key とも `<null>`、ledger 空 | なし |

- 未実施: なし（対象は AC17 (a)(c)(d)(e) と §5「未知の操作経路」の 2 点。(b) と HC2 とテスト suite は対象外で Phase C）。
- 判定 C への注意（観測の事実。判定はしない）: Play への移行中・Play 直後に送った `eval` は 5 秒の timeout になり、Pipeline がそれを Console の Error（`Failed to handle /api/exec request: Main thread operation timed out after 5000ms`）として出す。この Error は印で切った区間に入るので、AC17 (b) と HC2 の「区間の Error 0」を読む時は、この行を App の Error と区別するか、Play への移行中に `eval` を送らない。
- 保全: 各 run の印で切った Editor.log の区間、`artifacts/bs2b/` の前後の一覧、使った `eval` の C# は担当の手元（git 管理外）に保全した。照合 script と機械向けの期待値は無い。
- 作業ツリー: Editor を開いて消えた `MobileDependencyResolver` の `.pdb.meta` 5 件は `git checkout` で戻した。Scene・asset の変更は無い。

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未到達。
