# BS5b 宣言の運用経路

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `A`（A2 入力版。A2 独立レビュー前）
- branch: `claude/bs5b-operation-routes`
- implementation base commit: `26f34046`（`develop`、PR #152 のマージ。§1.3 の現況は `6fe18810` で確認し、`26f34046` までの差分は workflow 文書だけ）
- implementation head commit: 未到達
- risk: `high`（全ての content build が通る `BuildContentCoordinator` の workspace の扱いを変えて削除を伴う。Runtime の起動（`AbstractApplicationInitializer`）の Editor directory 経路に初回 Scene を足す。Editor Play の process 環境変数を宣言から置く）
- owner: 発注者 / BS5b 担当
- created: 2026-11-01
- expires: BS5b の Phase D 完了時
- harvest to: Architecture §18 §4（「SampleGame の宣言と Editor build」「BuildSystem の後続」）、§04 §4.6 / §4.7 / §4.8（Editor directory Play の初回 Scene、Play As の config の置き方）、[Harness README](../../tools/Harness/README.md#起動前の-path-予算検査) の「既知の予算外」、[BuildSystem program §7](BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md#7-buildsystem-の他の系列の所在) の BS5b 行
- workflow task: 未 start（最初に Evidence を固定する時点で start する）
- Phase A snapshot の取得案内・状態: 未生成（A2 入力版の時点では機械向けの期待値・script は無い。§5 の照合 script は判定 C の offline 実行方式で、A3 で合成 fixture と期待出力を Phase A snapshot に入れて凍結する）
- Phase B result snapshot の取得案内・状態: 未到達
- evidence bundle の取得案内・状態: 未到達
- C' blind bundle の取得案内・状態: 未到達
- 引継ぎ票: **A2 入力版（2026-11-01）。** 次は A2 独立レビュー。引き継ぐ commit はこの票を含む commit（SHA は起動指示で渡す）。A2 中の改訂は commit せず Phase A snapshot で渡す。権限は読み取りだけ（Unity・資格情報・通信は不要）。少なくとも 1 件はアーキテクチャゲート、高リスクなので A0 だけから代替構成を出すレビューを 1 件検討する。C' 用に未関与の系列を予約する。

### PR 状態ブロック

PR を作る前は §0 を唯一の正本とする。

---

## 1. 目的と対象外

- **目的:** BS5a が作った宣言（`BuildPurposeDeclaration`）を、日常 Play・成果物の検証 Play・publish・別 PC の受け取り・batch の各参照者の操作に接続する。宣言 1 つを選べば、その操作に必要な mode・表現・runtime 既定値・成果物が手入力無しに揃い、揃わない時は黙って別の経路で動かずに拒否される。
- **対象外:**
  - **d5b（S-4d5b）の実証:** Planner profile で Environment を読まずに Content Directory の Whitebox 上で Event が動くこと。d5b は今日と同じ手順（環境変数と profile を手で置く source の Play）で進め、BS5b を待たない（[S-4 program §6.3](S-4_FULL_SPEC_WORLD_AUTHORING.md)）。BS5b の受け入れ条件は Planner 宣言の Event の動作を判定しない。
  - **identity 後続:** 入力 identity（宣言と選択候補の source 状態と設定 snapshot の hash）、設定 snapshot、`content/index`、`priorRevisions`、build を skip する再利用。revision（`UtcNow + Guid`）の形式は変えない。所在は [BuildSystem program §7](BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md#7-buildsystem-の他の系列の所在)。
  - 経路の連鎖、object 単位の格納と差分更新、再 build の bytes の決定性、Player の実行時差し替え、複数 directory の同時登録、設定の宣言化と Player の Debug の剥離、known-good の pin の呼び手、日常 Play で宣言に無い content を読んだ時の検出（UC11）、`outcome.json` の記録形式の整理（いずれも Architecture §18 §4「BuildSystem の後続」の未着手の問い）。
  - InGame の Spring に入る Player の build 経路、BS4 Player の動作の変更。
  - R2・HTTP への publish（BS5b の publish は local の published directory まで）、DIST の install と cache の契約の変更。
  - directory mode の Editor Play で flight rig が spawn に置かれない観測の切り分け（Architecture §18 §4「検証の残り」）。
  - Addressables の退役、旧 Variant profile の整理。
- **現況:** §1.3（A0、`6fe18810` で確認）。

### 1.1 Phase A の判断基準（発注者。S-4 program §0 と BS5a の Phase A から転記）

- **主目的は BuildSystem。** Build 側の既知の課題（transport staging の path が予算の外）は「やらない」で片付けず、BuildSystem の作業の候補として扱う。BS5b はこのうち transport staging の path を範囲に含む。
- **残すコードは SampleGame に必要なものだけ。** 生成や authoring の道具は不要なら一時スクリプトで作り commit の前に消す。
- **checkout のルート。** BS4 Player を build するスライスは、ルートが 96 文字以下の checkout で作業する（transport staging の path の制約。本スライスが直すまで有効）。この worktree のルートは 31 文字。
- **参照者は 4 者。** Editor（日常）・Editor（成果物の検証）・Player・別 PC / Cloud が、それぞれ「必要なものをどう知るか」「何を持っているか」「足りないものをどこから得るか」を持つ（§1.4）。判定は難しくしない。
- **実ユースケースで通す。** 規則だけでなく §1.5 のユースケースの BS5b の列が成り立つことを確かめる。
- **分割（2026-10-31）。** BS5 は変更理由で BS5a（中核）・BS5b（運用経路）・identity 後続に分けた。BS5b は d5b と並行できる。

### 1.2 常時契約と program から転記した制約

- Game → Framework の一方向。Framework は season 名、Cell 座標文法、職種 role、`CellCompanionSet` を知らない。宣言の runtime 値は Framework にとって不透明な key=value。asmdef 参照を足さない（足す必要が出たら A へ返す）。
- `SceneState` の 14 値と `LoadType` を変えない。
- Editor コードを Runtime アセンブリに置かない。Unity 側の `.cs` は `#nullable enable`、`record` と `init` accessor を使わない、破棄されうる `UnityEngine.Object` は `== null` で判定する、公開 API のログ抽象は `ILogger<T>`。
- path は `tools/path-budget.json` の予算に従う（repo 相対 150 文字。ルート + 150 ≤ 259）。
- `unity test` / `unity run` を使わない。Unity テストは `pwsh tools/run-tests.ps1`。Phase B は Editor 操作と compile 確認まで、Unity テスト・content build・Player build・batch の実行と判定は Phase C。Scene / asset YAML の手編集は禁止。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。

### 1.3 A0 の現況（`6fe18810`）

BS5a 以前の事実のうち BS5a が変えていないものは BS5a の A0（`git show b4ae3d78^:docs/handoff/BS5A_PURPOSE_SELECTION.md` §1.3）で確認済みで、ここでは BS5b の判断に要るものだけを再確認して転記する。

**宣言（BS5a 後）。** `SampleGameBuildPurposes` が 6 宣言（`all-full`、`spring-full`、`spring-whitebox`、`spring-full-whitebox`、`bs4-spring-full`、`spring-planner-whitebox`）を持つ。欄は `Name`・`Roots`・`Filter`・`Representation`（優先順と `First` / `All`、主値 `Primary`）・`RuntimeDefaults`（`world:cellCompanionSet` だけ。`content:` で始まる key は拒否）。宣言は `BuildContentRoot.Declaration` と transport manifest の `declaration`（どちらも任意 field）に写る。directory mode の起動は root の宣言を config と突き合わせ、違いを `ContentDeclarationMismatch` の Warning 1 行で出す（Architecture §04 §4.7）。BS4 coordinator は `bs4-spring-full` 宣言から baked config の `cellCompanionSet`・`representation` を写し、`firstScene = "Title"` は定数（`SampleGamePlayerBuildCoordinator.cs:115`）。

**Editor の日常 Play（UC1・UC2）。** 未指定の Editor Play は Delivery の verified pair が無ければ起動失敗する契約で、source を読むのは明示 `content:runtimeMode=addressables` だけ（§04 §4.7、BS5a の I8 で維持）。表現は Editor の active Variant profile（`DeveloperVariantSettings`、`UserSettings/OneStarMaker/` に永続）が config の `assets:sceneVariant` より優先し、profile 未選択（resolver が `null`）の時だけ config を読む（§04 §4.8）。companion set は `world:cellCompanionSet`。config は `app-config.json` → 環境変数（prefix `SAMPLEGAME_`、`__` を `:` に写す。`AppInitializer.GetEnvironmentVariablePrefix`）→ command line の順に重なる。Planner の Play は今、mode・profile・companion set の 3 つを手で揃える。Editor Play は開いている Scene を初期登録の入力にする（§04 §4.6）。

**検証 Play（UC4）。** menu `Tools/OSM/Content/Build <contentSet>`（宣言ごと 5 件）は Content Directory を `artifacts/bs2b/content/<identity>/` に作るだけで publish しない。`ContentTransportPublisher.Publish(result, publishRoot)` の呼び手は BS4 coordinator だけ（`SampleGamePlayerBuildCoordinator.cs:46`、publish 先 `artifacts/bs4/transport/<identity>`）。Delivery 窓（`Tools/OSM/Content/Delivery`）は source の種類（Published directory / HTTP / Installed revision root）と、manifest SHA-256・contentSet・revision・cache root・representation（既定 `Full`）を手で入れ、Prepare（`ContentInstaller` で DIST cache に install、または installed の再検証）→ Use For Next Play（`ContentDeliveryPlayBridge.Apply`）する。窓は宣言を表示せず、representation も宣言から取らない。bridge は `SAMPLEGAME_CONTENT__` の 5 key（`RUNTIMEMODE=directory`、`INSTALLEDREVISIONPATH`、`MANIFESTSHA256`、`BUILDIDENTITY`、`REPRESENTATION`）を process scope に置き、以前の値と置いた値を `SessionState` の ledger（`OSM.ContentDelivery.PlayBridge.v1`）に残す。Reset は自分が置いた値のままの key だけを戻す。Play の終了で自動では戻さない。`world:cellCompanionSet` は置かない。

**開いている Scene の再利用。** `PerformUnitySceneLoad` と `LoadUICommonAsync` は `SceneManager.GetSceneByName` でロード済みの Scene を見つけるとロードせずに使う。directory mode でも、Editor で開いている source の Scene がそのまま使われる。BS5a は検証 Play の操作手順で閉じることを要求しただけで、機械の拒否は無い。

**空 Scene からの directory Play。** Editor directory 経路の `ContentDirectoryConfiguration.FirstScene` は常に空（`AbstractApplicationInitializer.cs` の `ReadContentDirectoryConfiguration`、Editor 分岐）で、初回 Scene をロードするのは Player の baked `firstScene` だけ。しかもその分岐（同 file の `load-player-first-scene` 以降）は初回 Scene の後に Player の smoke（代表 Object の検証、初回 Scene の unload、directory の close、receipt）まで続ける。だから開いている Scene を閉じた（空 Scene の）directory Play は何も起動しない。BS5a の AC13 は判定の操作で `SceneDirector.AddScene("Title")` を手で呼び、Title の Start の event を発火して進めた（BS5a §7.7、後続の入力）。

**workspace と publish（最小性 (2) の保留）。** `BuildContentCoordinator` は Unity の出力先 workspace を `<root>/work/<target>/<contentSet>` で contentSet ごとに再利用し、成功後に workspace を丸ごと staging へ複写して `content/<identity>` へ移す（`BuildContentCoordinator.cs`、`CopyDirectory` は全 file）。adapter は `BuildContentOptions.None` で `BuildPipeline.BuildContentDirectory` を呼ぶ（`UnityContentDirectoryAdapter.cs`）。Unity が workspace の古い出力を消すかは未確認なので、前回の選択の file が今回の成果物と publish に混ざりうる。BS5a はこの最小性 (2)「publish の成果物に前回の workspace の残骸が混ざらない」を BS5b（publish の `Full` 必須）へ保留した。BS5a の高コスト証拠では build の前に workspace を手で消して回避した。

**transport staging の path。** `ContentTransportPublisher` は publish 先の sibling に `<final>.<guid32>.staging` を作る。BS4 の publish 先 `artifacts/bs4/transport/<identity>`（identity は 51 文字）の staging の中の Unity の file は repo 相対 163 文字になり、ルート 97 文字以上の checkout で 260 文字を超えて `DirectoryNotFoundException` で失敗する（2026-10-27 に S-4d3 で観測、未修正。Harness README「既知の予算外」）。final 側の同じ file は 122 文字前後。coordinator 自身の staging（`<root>/staging/<identity>`）は final（`<root>/content/<identity>`）と同じ長さ。

**batch。** content build と publish を Editor の外から起動する入口は無い。`tools/unity-editor.cmd` は起動中の Editor の Pipeline server への `status` / `command` / `eval` / `pipeline list` だけを通し、`menu` を通さない。`eval` は 5 秒で timeout を返すが同期の content build は main thread で最後まで走る（1 build 約 4.5 分）。clean checkout（`Library/` 無し）からの batch build の所要時間は未測定。

**宣言と config の不一致の Warning の配線**には自動テストが無い（BS5a の Editor Play と BS4 Player のログで観測済み。Architecture §18 §4「検証の残り」）。

### 1.4 参照者と経路（BS5a の表を BS5b の列で更新）

| 誰 | 必要なものをどう知るか | 持っているもの | 足りないものの経路 | BS5a 後（現況） | BS5b 後 |
|---|---|---|---|---|---|
| Editor（日常） | 開いている Scene graph と、選んだ宣言の runtime 既定値 | AssetDatabase の source（明示 addressables の経路） | （後続）LAN / R2 の object | 手で mode・profile・companion set を置く | 宣言を選んで Play する（Play As） |
| Editor（成果物の検証） | 宣言 | installed directory | なし。fail-closed | menu の `Build` と Delivery 窓の手入力で、封じた directory の検証 Play ができる | 宣言 1 つから build → publish → install → 検証 Play |
| Player | baked の宣言 | 内包 / installed | （後続）R2 の object の差分 | BS4 Player に付随 asset と宣言の既定値が内包される | 変えない |
| 別 PC / Cloud（build する側） | 宣言名 | clean checkout の source と Unity | なし | menu | batch で build と publish |
| 別 PC / Cloud（受け取る側） | published directory の manifest の宣言 | published directory、または cache | （後続）R2 の object | manifest が宣言を持つ | From published directory |

### 1.5 ユースケース（BS5b の行）

| # | 誰が何をする | 今（BS5a 後） | BS5b 後 |
|---|---|---|---|
| UC2 | 企画者が Planner profile（Environment 無し）で Play | mode の環境変数・表現の profile・companion set の環境変数を手で揃える。d5b はこの手順で進める | Play As（宣言を選ぶと、source の Play に mode・表現・companion set が置かれる。合わない profile は拒否） |
| UC4 | 担当が「この目的の成果物だけで動くか」を Editor で確かめる | 窓に手入力し、宣言の Scene と UICommon を手で閉じ、空 Scene では初回 Scene を手で足す。Addressables には抜けない | 1 操作（宣言 1 つから build → publish → install → 検証 Play）。宣言の Scene・UICommon が開いていれば拒否。初回 Scene は directory から起動する |
| UC6 | CI が clean checkout で目的 X を build し publish する | menu しか無い | batch 入口と終了コード。publish は `Full` の build だけ |
| UC7 | 別 PC が published directory を受け取った後、Editor で検証 Play | 窓に手入力 | From published directory（directory と manifest SHA-256 だけを入れ、contentSet・revision・表現・runtime 既定値は manifest と宣言から） |

UC1・UC3・UC5・UC8〜UC11 は BS5b で変えない（BS5a の A0 §1.5 のまま）。

---

## 2. 意思決定と受け入れ境界（A1）

- **このスライスが答える問い:** 宣言 1 つを選ぶだけで、日常 Play・成果物の検証 Play・batch の build と publish・published directory の受け取りが、手入力の設定無しに宣言どおりの mode・表現・runtime 既定値・成果物で動き、宣言と食い違う状態（合わない profile、開いている source の Scene、残った他の設定、前回の build の残骸）では黙って別の経路で動かずに拒否されるか。

### 2.0 証明の範囲

| 主張 | 何で示すか | 示さないこと |
|---|---|---|
| Play As が宣言どおりの config を置き、Play の後に戻す | AC1〜AC4（変換と ledger の単体・EditMode）、AC17（操作） | source の Play で宣言に無い content を読まないこと（UC11、後続） |
| 検証 Play の 1 操作が宣言どおりの directory を作って使う | AC5〜AC9、AC17 | 到達可能な全 Scene のロード（総当たりしない）。Planner 宣言の Event の動作（d5b） |
| 最小性 (2): publish の成果物に前回の workspace の残骸が混ざらない | AC10・AC11（番兵 file）、HC1 | 今回の build の物理 file 単位の最小性、bytes の決定性 |
| batch で clean checkout から build と publish ができ、失敗が終了コードで分かる | AC13・AC14、HC1 | CI 基盤への組み込み、別 PC の実機 |
| published directory を受け取って検証 Play できる | AC15、HC2 | R2・HTTP の取得、別 PC の実機（同じ PC の別 checkout で代える） |
| transport staging が path の予算に収まる | AC12 | DIST cache の install 先の path（cache root は利用者の指定） |

### 2.1 不変条件（起きてはいけないこと）

- **I1（宣言と食い違う Play をしない）:** Play As と検証 Play は、置いた config と宣言（表現の主値・runtime 既定値・mode）が一致しない状態で Play に入らない。active Variant profile が config より優先して表現を変える状態（profile が選ばれている）、置くべき key が自分の ledger 以外の値で既に process 環境にある状態では、Play に入らずに理由と key を示して止まる。
- **I2（source に抜けない）:** 検証 Play（1 操作・From published directory）は、宣言の選択に入る Scene payload か UICommon が Editor で開いている（ロード済みの）時は Play に入らない。開いている Scene の再利用で source の Scene が directory の代わりに使われる状態を作らない。
- **I3（設定を残さない）:** Play As と検証 Play が置いた process 環境変数は、その Play の終了（Edit mode に戻った時点）で置く前の値へ戻る。domain reload を挟んでも戻る。他者が後から変えた値は巻き戻さない（既存 bridge の規則）。User / Machine scope に書かない。
- **I4（残骸を publish しない）:** publish される成果物（`transport.json` の `files` と published directory の `content/`）は、その build の Unity の出力だけからなる。publish は `Full` で build した結果だけを受け付け、それ以外は publish 先を作らずに失敗する。
- **I5（失敗を成功と見せない）:** batch 入口は、宣言名の誤り・選択の issue・Unity build の失敗・publish の失敗のどれでも 0 以外の終了コードで終わり、成功の結果 file も published directory も残さない。Editor の 1 操作も、途中の段で失敗すれば Play に入らない。
- **I6（既存の契約を変えない）:** 明示 `addressables` だけが source の口、未指定は verified pair が無ければ失敗する mode の契約（§04 §4.7）、directory mode の Addressables 封鎖、宣言の不一致 Warning、BS4 Player の baked config と smoke の段の順序、build identity と transport manifest の schema・`files` 規則・DIST の install 配置、既存の Delivery 窓の手入力経路は変えない。
- **I7（Framework は語彙を知らない）:** Framework 側に足すもの（build mode、publish の拒否、staging、Editor directory の初回 Scene）は season 名・Cell 座標文法・職種 role・`CellCompanionSet`・`SAMPLEGAME_` prefix・`Title` を参照しない。asmdef 参照を変えない。

### 2.2 成立条件（起きなければならないこと）

- **E1（Play As）:** 6 宣言のどれかを選んで Play As すると、その Play の config が `content:runtimeMode=addressables`、`assets:sceneVariant` = 表現の主値の Variant（`Full` は空、`Whitebox` は `Whitebox`）、宣言の `RuntimeDefaults` の各 key=value になり、Edit mode に戻ると置く前の値に戻る。
- **E2（検証 Play の 1 操作）:** 宣言 1 つを選ぶ 1 操作で、`Full` の content build → `artifacts/bs2b/transport/<identity>` への publish → DIST cache への install → bridge（content の 5 key と宣言の `RuntimeDefaults`、表現は宣言の主値）→ Play が順に行われ、空 Scene から directory の初回 Scene（SampleGame は `Title`）が Stable になる。起動ログに封鎖の 1 行があり、宣言の不一致 Warning が無い。
- **E3（`Full` 必須）:** build に mode（`Incremental` / `Full`）があり、`Full` は Unity の build の前に workspace を空にする。mode は `outcome.json` に記録され、publish は `Full` を要求する。検証 Play の 1 操作・batch・BS4 coordinator は `Full` で build する。menu の `Build <contentSet>` は従来どおり `Incremental`（publish しない）。
- **E4（batch）:** Unity の `-batchmode -executeMethod` で呼べる入口が、宣言名を引数に `Full` build と publish を行い、結果（identity・publish 先・manifest SHA-256・宣言名）を固定 path の JSON に書き、終了コードで成否を返す。
- **E5（From published directory と Delivery 窓の宣言対応）:** published directory と manifest SHA-256 の 2 つを入れると、contentSet・revision は manifest から、表現と runtime 既定値は manifest の `declaration` から決まり、install → bridge → Play ができる。Delivery 窓は Prepare した revision の宣言（名前・表現の主値・runtime 既定値）を表示し、Use For Next Play は宣言の `RuntimeDefaults` も置く。宣言の無い manifest は従来どおり手入力の representation で動き、runtime 既定値は置かない。
- **E6（staging の path）:** transport の publish が作る staging の中の各 path は、同じ file の final の path より長くならない。ルート 109 文字（= 259 − 150）の checkout で BS4 の publish が成功する長さになる。

### 2.3 受け入れ条件（検証する条件を併記）

単体 = Unity に依存しない純 C# テスト（Editor asmdef の pure class を含む）。EditMode = AssetDatabase・`SessionState`・合成 fixture を使うテスト。操作 = Phase C の実行。HC = 高コスト証拠（§5）。

| AC | 内容 | 条件 | 種別 |
|---|---|---|---|
| AC1 | 宣言から Play As の config 集合を作る変換が、6 宣言それぞれで `content:runtimeMode=addressables`・`assets:sceneVariant`（主値 `Full` → 空文字、`Whitebox` → `Whitebox`）・`RuntimeDefaults` の全 key=value を返す。環境変数名は prefix + key の `:` を `__` に写したもの（例 `SAMPLEGAME_WORLD__CELLCOMPANIONSET`）。`Full` / `Whitebox` 以外の主値は変換が失敗する | E1 | 単体 |
| AC2 | Play As は active Variant profile が選ばれている（resolver が非 `null`。`Production` の `""` を含む）時に拒否し、process 環境を変えない。置く key が ledger に無い非 `null` の値で既にあれば拒否し、key 名を示す。負例: 環境に `SAMPLEGAME_WORLD__CELLCOMPANIONSET=Full` が外から置かれた状態で Planner 宣言を Play As → 拒否、環境は変わらない。合格例: profile 未選択・該当 key 無し → 置く。不合格例: 外の値を上書きして Play に入る | I1 | EditMode |
| AC3 | 共通の ledger（Play As・検証 Play・Delivery 窓が共有）が、置いた key・以前の値・置いた値を `SessionState` に持ち、`EnteredEditMode` で Reset する。Reset は置いた値のままの key だけを以前の値（無ければ削除）に戻す。domain reload 後に `EnteredEditMode` が来ても戻る（ledger を `SessionState` から読む）。負例: Play 中に他者が key を変えた → その key は戻さない、他の key は戻る | I3 | EditMode（`SessionState` と process 環境。Play には入らない） |
| AC4 | 既存の Delivery 窓の Use For Next Play も同じ ledger を使い、従来どおり手で Reset でき、加えて Play の終了で Reset される。従来の 5 key の値と検証（`ValidateInstalled`）は変わらない | I3・I6 | EditMode |
| AC5 | 検証 Play の開始前検査が、宣言の選択（`SampleGameContentBuild.Resolve` の selected）の Scene payload の path と UICommon の path のどれかが `EditorSceneManager` でロード済みなら拒否し、開いている path を列挙する。build も publish もしない。負例: `Spring_Cell_0_4`（Whitebox payload）を開いて `spring-planner-whitebox` を選ぶ → 拒否。合格例: 選択に入らない Scene（他季節の `Summer_Cell_*` の 1 件）だけが開いている → 通す、untitled の空 Scene → 通す | I2 | EditMode |
| AC6 | 検証 Play の 1 操作の段の順序と停止: Play mode 中の拒否 → 開始前検査（AC5・AC2 の ledger 検査）→ `Full` build → publish → install → bridge → `EnterPlaymode`。どの段が失敗しても以降の段を実行せず、環境は変えず（bridge 前の失敗）、Play に入らない。段ごとの失敗を偽の段で注入して確かめる | I5・E2 | 単体（段を注入した orchestrator） |
| AC7 | 検証 Play の bridge は content の 5 key（表現は宣言の主値）に加えて manifest の `declaration` の `RuntimeDefaults` を置く。宣言の名前が選んだ宣言と違う manifest は拒否する | I1・E2 | 単体 + EditMode |
| AC8 | Editor directory Play で config `content:firstScene` があれば、`RegisterAlreadyLoadedScenes` の後に `SceneDirector.AddScene(firstScene)` で初回 Scene をロードし、Player の smoke の段（代表 Object の検証、初回 Scene の unload、directory の close、receipt）は行わない。Player の経路の段の順序と receipt は変わらない。`content:firstScene` が無い Editor directory Play は従来どおり何もロードしない。addressables mode は `content:firstScene` を読まない | E2・I6 | EditMode（起動の分岐の単体化）+ 既存の Player content テスト |
| AC9 | 検証 Play の 1 操作が置く `SAMPLEGAME_CONTENT__FIRSTSCENE` は SampleGame の定数（BS4 coordinator と同じ `Title`。1 箇所に定義し BS4 と共有）。Framework は値を知らない | E2・I7 | 単体 |
| AC10 | `BuildContentRequest` に mode を足し、`Full` は adapter を呼ぶ前に workspace を空にする（workspace 自体は残す）。番兵 file を workspace に置いて `Full` で build（偽 adapter）→ 成果物 `content/<identity>` に番兵が無い。`Incremental` では従来どおり残る（偽 adapter で現状を固定）。workspace が approved root の外を指す経路は既存の検査で拒否され、削除しない | I4・E3 | 単体（偽 adapter と一時 root） |
| AC11 | `outcome.json` に `buildMode` が入り（後方互換の追加項目）、`ContentTransportPublisher.Publish` は `buildMode` が `Full` でない result（欄の無い旧 outcome を含む）を、publish 先と staging を作らずに `InvalidOperationException` で拒否する。BS4 coordinator は `Full` で build する | I4・E3 | 単体 + EditMode（`BuildContentReport` の往復） |
| AC12 | publisher の staging を `<publishRoot の親>/.staging/<8 桁>` に変え、staging 内の各 file の path 長が final の同じ file 以下になる。テストは識別子 51 文字・Unity の file 名の最長を想定した合成の directory で、ルートの長さ 109 の時に staging と final の全 path が 259 以下であることを計算で確かめる。staging は同じ volume の move で final になり、失敗時に staging だけを消す（既存の規則） | E6 | 単体（一時 directory） |
| AC13 | batch 入口 `SampleGameContentBatch.BuildAndPublish` は引数 `-osmPurpose <宣言名>` で `Full` build と publish を行い、`artifacts/bs2b/batch/last-result.json`（identity・publishPath・manifestSha256・declaration・buildMode・exitCode）を書いて `EditorApplication.Exit(0)`。終了コード: 0 成功、2 引数の誤り（宣言名の欠落・未知）、3 選択の issue（preflight の失敗）、4 Unity build の失敗、5 publish の失敗、1 その他の例外。0 以外では結果 file に `exitCode` と理由だけを書き、published directory を残さない | E4・I5 | 単体（段を注入）+ HC1 |
| AC14 | batch の失敗経路の実観測: 未知の宣言名で batch を起動すると終了コード 2 で、`artifacts/bs2b/batch/last-result.json` の `exitCode` が 2、新しい `transport/` の directory が増えない | I5 | 操作（Phase C。content build を伴わない） |
| AC15 | From published directory: published directory の path と manifest SHA-256 を入れると、`transport.json` の contentSet・revision・`declaration` を読み、manifest SHA-256 が一致しなければ install せずに拒否する。一致すれば install → bridge（表現は宣言の主値、`RuntimeDefaults`）→ AC5 と同じ開始前検査（宣言は manifest の宣言名で `SampleGameBuildPurposes` から引く。無ければ拒否）→ Play。宣言の無い manifest は拒否し、Delivery 窓の手入力経路を案内する | E5・I1・I2 | EditMode（合成の published directory）+ HC2 |
| AC16 | Delivery 窓は Prepare した revision の manifest の宣言（名前・表現の主値・runtime 既定値）を status に表示し、窓の representation が宣言の主値と違えば Use For Next Play の前に違いを表示する（拒否はしない。手入力経路の互換）。宣言の無い manifest では表示せず従来どおり | E5・I6 | EditMode |
| AC17 | 操作: (a) `spring-planner-whitebox` を Play As（profile 未選択、開いている Scene は `OutGameScene`）→ 起動ログの config に `world:cellCompanionSet=Planner` と `assets:sceneVariant=Whitebox`、mode addressables が出て、Play を止めた後の process 環境が置く前と同じ。Event の動作は観測しない（d5b）。(b) `spring-full` の検証 Play の 1 操作（空 Scene から）→ `Title` が Stable、起動ログに封鎖の 1 行、不一致 Warning 無し、Console の Error 0、Play を止めた後の環境が戻る。(c) `Spring_Cell_0_4` を開いた状態で `spring-full` の検証 Play → 拒否、build されない | E1・E2・I1・I2・I3 | 操作（Phase C）、証拠は Console ログと前後の環境の記録 |
| AC18 | 最終の全 EditMode 回帰が通る。BS3 / BS4 の既存テスト（`ContentDirectorySessionTests`、`BuildContentDirectoryIntegrationTests`、`PlayerContentConfigurationTests`、`ContentDeliveryFailureMatrixTests`）の結果を変えない | I6 | 回帰 |

AC2 の読み方: 「profile が選ばれている」は `SceneVariantRuntimeBridge.EditorSceneVariantResolver` が `null` 以外を返すこと。`Production`（`""`）も選択に含める（config より優先して表現を固定するので）。

AC5 の読み方: 判定は path の一致で、Scene 名だけの一致にしない。payload の無い node（`InGameSession` 等）は Scene が無いので対象外。`spring-full-whitebox` のような `All` の宣言では両表現の payload が対象。

### 2.4 A1 の設計

- **D1（Play As の置き場と形）:** `SampleGame/DependOnAll/Editor/Build/` に `PlayAsLauncher`（menu `Tools/OSM/Play As/<宣言名>`、6 件）を置く。宣言から config の key=value を作る変換 `PurposeLaunchSettings`（pure、internal）は同じ folder に置き、Play As と検証 Play と From published directory が共有する。prefix `SAMPLEGAME_` と `Full`→空の写像は SampleGame が持つ（Framework の `ResolveSceneVariant` は `assets:sceneVariant` を読むだけで、写像は持たない）。Framework に「宣言を config に写す」層は作らない（実装は 1 つ）。
- **D2（共通 ledger）:** 既存 `ContentDeliveryPlayBridge` を、任意の key 集合を置ける ledger に広げる（`Apply(IReadOnlyList<(key, value)>)`、`Reset()`、`AssertNoForeignValues(keys)`）。Delivery 窓の既存 `Apply(root, digest, identity, set, representation)` は内部で 5 key を作って同じ経路を通る。`[InitializeOnLoad]` で `EditorApplication.playModeStateChanged` の `EnteredEditMode` に Reset を登録する。ledger の key は `OSM.ContentDelivery.PlayBridge.v1` のまま（形は互換。`keys`・`previous`・`installed` の 3 配列）。
- **D3（profile の扱い）:** Play As は profile を変えない（`UserSettings` の永続値を Play As が書き換えると、Play の後に戻す責務と失敗時の不整合が増える）。profile が選ばれていれば拒否し、Variant 設定の窓で選択を外すよう案内する。
- **D4（検証 Play の 1 操作）:** `VerifyPlayLauncher`（menu `Tools/OSM/Content/Verify Play/<宣言名>`）。段は AC6 の順。build は既存の `SampleGameContentBuild.BuildResult(declaration, mode: Full)`、publish は `ContentTransportPublisher.Publish(result, artifacts/bs2b/transport/<identity>)`、install は Delivery 窓と同じ `ContentInstaller` + `LocalContentArtifactSource`（cache root は Delivery 窓の設定 `UserSettings/OSMContentDelivery.json` の `cacheRoot`。空なら拒否）、bridge は D2 に `FIRSTSCENE` と `RuntimeDefaults` を足した key 集合。orchestrator は段を interface で受け、AC6 を単体で確かめる。build は同期（Editor が止まる。既存 menu と同じ）、install は async で、完了後に `EnterPlaymode`。
- **D5（Editor directory の初回 Scene）:** Runtime の `ReadContentDirectoryConfiguration` の Editor 分岐で `content:firstScene` を読み、`ContentDirectoryConfiguration.FirstScene` に渡す。起動の `load-player-first-scene` の分岐を、初回 Scene のロード（Editor と Player の共通）と Player の smoke（`UseRequiredPlayerFileConfiguration` の時だけ）に分ける。Editor では初回 Scene を unload しない。新しい状態・所有者・寿命は足さない（`SceneDirector.AddScene` の既存の寿命）。U1 に代案。
- **D6（build mode）:** Framework の `BuildContentRequest` に `BuildContentMode Mode`（既定 `Incremental`）を足し、coordinator は `Full` の時に adapter を呼ぶ前に workspace の中身を消す（`Child` で approved root の中と確かめた workspace だけ。workspace の外の file を消さない）。`BuildContentReport.WriteOutcome` が `buildMode` を書く。`ContentTransportPublisher.Publish` は outcome の `buildMode` を読み、`Full` 以外を拒否する。coordinator の workspace の再利用（`Incremental`）は menu の日常の build の速さのために残す。
- **D7（staging）:** `ContentTransportPublisher` の staging を `Path.Combine(Path.GetDirectoryName(final), ".staging", <guid の先頭 8 桁>)` に変える。同じ親の下なので move は同じ volume。`.staging` の残骸の回収は既存の失敗時の削除のまま（異常終了の残置は Storage program §6 の保留項目と同じ扱い）。
- **D8（batch 入口）:** `SampleGame/DependOnAll/Editor/Build/SampleGameContentBatch.cs` に `BuildAndPublish()`。引数は `Environment.GetCommandLineArgs()` から読む。結果 file は `artifacts/bs2b/batch/last-result.json`（上書き）。`-quit` に頼らず `EditorApplication.Exit(code)` で終える。pwsh の wrapper は作らない（Unity の起動の仕方は呼ぶ側の CI の責務。判定の起動コマンドは §5 に書く）。
- **D9（From published directory と Delivery 窓）:** `VerifyPlayLauncher` に From published directory の入口（小さな EditorWindow の 2 欄と実行 button。または Delivery 窓の Published directory モードに「宣言から」の button を足す）。推奨は Delivery 窓に足す（入力と cache root の設定が既にあり、窓を増やさない）。manifest の読み取りは既存 `ContentTransportManifest`（Runtime の Distribution）を使う。
- **D10（宣言名の引き方）:** manifest の `declaration.name` から `SampleGameBuildPurposes` の宣言を名前で引く関数を足す（AC15）。無い名前は拒否（別の版の宣言で作った directory を今の宣言で検証しない）。

### 2.5 ここでは答えない問いと所有する後続

| 問い | 所有 |
|---|---|
| Planner 宣言で Environment を読まずに Whitebox 上の Event が動くか、S-4 program の harvest と削除 | S-4d5b |
| 入力 identity、設定 snapshot、`content/index`、`priorRevisions`、build を skip する再利用 | identity 後続（[BuildSystem program §7](BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md#7-buildsystem-の他の系列の所在)） |
| R2・HTTP への publish、別 PC の実機での受け取り、CI 基盤への組み込み | BuildSystem 後続（Storage program §6「取得経路と配布」と接続） |
| 日常 Play（source）で宣言に無い content を読んだ時の検出 | BuildSystem 後続 |
| 宣言の不一致 Warning と封鎖の配線の自動テスト、付随 asset の entry を名前で同定する材料 | BuildSystem 後続（Architecture §18 §4「検証の残り」） |
| directory mode の Editor Play で flight rig が spawn に置かれない | 所有スライス未定（Architecture §18 §4「検証の残り」） |
| `.staging` と `work/` の残置の清掃 | Storage program §6「ローカルの残置の清掃」 |
| InGame の Spring に入る Player、Player の実動作 | BuildSystem 後続 / S-9 |

- **判定定義:** 不変条件 I1〜I7 と成立条件 E1〜E6 を AC1〜AC18 と HC1・HC2 で確かめ、全て成立で GO。1 つでも不成立なら NO-GO。
- **停止規則:** 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。スパイクではないので CONDITIONAL ACCEPT は使わない。
- **A3 後の例外承認:** なし。
- **決定記録:** なし。
- **本文へ転記した実装制約:** §1.2。
- **未決事項:** §2.6。

### 2.6 未決事項

- **U1（初回 Scene の置き場）:** 推奨は D5（Runtime の Editor directory 分岐で `content:firstScene` を読み、Player の smoke と分ける。Player と同じ呼び出しで、テストで固定できる）。代案は Editor 側の `playModeStateChanged` で Play に入った後に `SceneDirector.AddScene` を呼ぶ（Runtime を変えないが、Editor が起動完了を待って Runtime の内部に触る口が要る）。`AbstractApplicationInitializer` は 1,277 行で、変更は分岐の分離（約 20 行）に留める。
- **U2（規模）:** 見積もりは製品コード 600〜800 行（Editor 約 520、Framework Editor 約 60、Runtime 約 20）、テスト 700〜900 行。500 行の警報を超える。変更理由は 4 つ（Play As と ledger、検証 Play と受け取り、build mode と publish、batch）で、互いに ledger・build mode を共有するので 1 スライスとする。Phase B で Runtime 側が D5 の分岐の分離を超える状態・所有者・寿命を要したら B を止めて A へ返す。
- **U3（From published directory の入口）:** D9 の推奨（Delivery 窓に足す）と代案（別の窓）。
- **U4（`Incremental` の残し方）:** 推奨は D6（menu は `Incremental` のまま、publish する経路だけ `Full`）。代案は全経路 `Full`（workspace の再利用をやめる。menu の build が毎回 Unity の full build になり遅くなる）。

---

## 3. 責務マップ（A1）

| file | 責務 | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|---|
| `SampleGame/DependOnAll/Editor/Build/PurposeLaunchSettings.cs`（新規） | 宣言 → config key=value・環境変数名の変換 | Play As と検証 Play の入力 | 静的・pure | `BuildPurposeDeclaration` | internal | 単体（AC1・AC7・AC9） | 0 → 70 |
| `.../ContentDeliveryPlayBridge.cs` | 共通 ledger（Apply・Reset・外の値の検査・Play 終了で Reset） | ledger を任意の key 集合に広げる | `SessionState`。Editor session の寿命 | UnityEditor | internal | EditMode（AC2〜AC4） | 98 → 150 |
| `.../PlayAsLauncher.cs`（新規） | Play As の menu、profile の拒否、`EnterPlaymode` | Play As | 静的 | bridge、`PurposeLaunchSettings`、Variant resolver | menu | EditMode（AC2） | 0 → 80 |
| `.../VerifyPlayLauncher.cs`（新規） | 検証 Play の 1 操作の orchestrator、開始前検査、From published directory | 検証 Play | 1 操作の間 | build・publish・install・bridge（段は interface） | menu / internal | 単体（AC6）、EditMode（AC5・AC15） | 0 → 220 |
| `.../ContentDeliveryWindow.cs` | 宣言の表示、From published directory の button、共通 ledger の利用 | 宣言対応 | 窓の寿命 | 上記 | menu | EditMode（AC16） | 168 → 210 |
| `.../SampleGameContentBatch.cs`（新規） | batch 入口と終了コード | batch | 1 process | build・publish | `public static` | 単体（AC13） | 0 → 100 |
| `.../SampleGameContentBuild.cs` | build mode の引数、宣言を名前で引く | `Full` | 静的 | coordinator | public | 既存 + AC10 | 126 → 140 |
| `.../SampleGamePlayerBuildCoordinator.cs` | `Full` で build、`firstScene` 定数の共有 | `Full` 必須 | 1 build | 同上 | 既存 | 既存 | 151 → 155 |
| `OneStarMaker/Scripts/Editor/Build/Content/BuildContentCoordinator.cs` | `Full` の workspace の削除 | `Full` | 1 build | adapter | public（request の欄） | 単体（AC10） | 147 → 170 |
| `.../BuildContentReport.cs` | `buildMode` の記録 | `Full` | 静的 | — | internal | 往復（AC11） | 142 → 150 |
| `.../ContentTransportPublisher.cs` | `Full` の要求、staging の path | `Full`・staging | 1 publish | outcome | public | 単体（AC11・AC12） | 118 → 135 |
| `OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs` | Editor directory の初回 Scene、Player smoke との分離 | 初回 Scene | 起動 | 既存 | なし（private） | EditMode（AC8） | 1,277 → 1,300 |

行数の警報: `AbstractApplicationInitializer.cs` は既存で 500 行を超える。本スライスは分岐の分離だけで、分割はしない（起動順序の所有を 1 箇所に保つ既存の判断を変えない）。`VerifyPlayLauncher.cs` は 220 行の見込みで、From published directory を同じ file に置く（同じ段の列を共有する）。

## 4. 実装計画（A1）

### 4.1 順序

1. Framework: build mode・`buildMode` の記録・publish の拒否・staging（AC10〜AC12 の単体を先に書く）。
2. SampleGame: `SampleGameContentBuild` と BS4 coordinator に mode を通す。
3. 共通 ledger と `PurposeLaunchSettings`、Play As。
4. Runtime の初回 Scene（D5）。
5. 検証 Play の orchestrator、開始前検査、From published directory、Delivery 窓。
6. batch 入口。
7. compile 確認と `pwsh tools/contract-audit.ps1`。Unity テスト・content build・batch・Play は実行しない（Phase C）。

### 4.2 Phase B から Phase A へ差し戻す条件

- asmdef 参照の追加が要る。
- D5 の分岐の分離を超えて、Runtime の起動に新しい状態・所有者・寿命・公開 API が要る。
- `Full` の workspace の削除が approved root の外に及ぶ経路を断てない。
- 既存の Delivery 窓の手入力経路・BS4 Player の receipt の形を変えないと条件を満たせない。

### 4.3 対象外を維持する方法

Planner 宣言の Event・Environment の観測を AC に入れない（AC17 (a) は config の値と環境の復元だけ）。identity・index の欄を足さない。R2・HTTP に publish しない。

## 5. テストとレビュー計画（A1）

- **単体:** AC1・AC6・AC7・AC9〜AC13（Editor asmdef の pure class と偽の段・偽 adapter）。
- **差し戻し中の起点 `-Filter`:** 新しいテスト class と、`ContentDirectorySessionTests`・`BuildContentDirectoryIntegrationTests`・`PlayerContentConfigurationTests`・`ContentDeliveryFailureMatrixTests`・起動の EditMode テスト（`InvalidCompanionConfigStartupTests` を含む）。
- **判定必須テスト:** 最終の全 EditMode 回帰（空 filter）。PlayMode は対象外（Runtime の変更は Editor directory 分岐だけで、Player の smoke は既存の EditMode / Player content テストが固定する）。content build を伴う `BuildContentDirectoryIntegrationTests` は全 EditMode に含まれる。
- **全 EditMode 回帰の適用除外:** なし。
- **操作・実行時・目視条件の検証経路:**
  - **AC14（batch の失敗経路）:** 担当は判定 C の実行担当。この worktree で Editor を閉じ、`<Unity.exe> -batchmode -nographics -projectPath unity -executeMethod SampleGame.DependOnAll.Editor.Build.SampleGameContentBatch.BuildAndPublish -osmPurpose no-such-purpose -logFile <log>` を起動。観測: process の終了コード、`artifacts/bs2b/batch/last-result.json`、起動前後の `artifacts/bs2b/transport/` の一覧。保存: log・結果 file・一覧を判定 evidence bundle へ。
  - **AC17（Play As・検証 Play・拒否）:** 担当は判定 C の実行担当（Editor を `tools/unity-editor.cmd` の `eval` で操作。menu は `eval` から menu の static method を呼ぶ）。初期状態: Editor 起動、Variant profile 未選択、Delivery 窓の cache root を設定済み。(a) `OutGameScene` を開き Play As `spring-planner-whitebox` → Play 開始後のログ、Play 停止後の `SAMPLEGAME_*` の process 環境。(b) 空 Scene で Verify Play `spring-full` → build・publish・install の完了ログ、`Title` の Stable、封鎖の 1 行、Warning / Error の一覧、停止後の環境。build は約 4.5 分で、`eval` の timeout の後も走る（§1.3）。完了は `artifacts/bs2b/transport/<identity>` の出現と Play mode への遷移で判定する。(c) `Spring_Cell_0_4` を開き Verify Play `spring-full` → 拒否のログ、`artifacts/bs2b/content/` に新しい identity が無い。保存: Console ログ（Editor.log の該当区間）、環境の前後の記録、directory の一覧。
  - 動作確認（Phase B の後）: AC14 と AC17 (a)(c) を B の完了 head で各 1 回軽く行う（content build を伴う (b) は対象外。Build を伴うので Phase C）。
- **未知の操作経路の疎通結果と、必要な検証支援・確認地点:** `eval` から menu の static method を呼んで `EnterPlaymode` に入り、Play 中のログと停止後の process 環境を取る経路は、BS5a の AC13 で Play への遷移と Console の取得までは成立した。Play 停止後に Editor process の環境変数を `eval` で読む経路は未確認。初回確認は動作確認（AC17 (a)）。不成立なら `eval` で `Environment.GetEnvironmentVariable` を呼ぶ代わりに、bridge が Reset 時に Console へ出す 1 行（戻した key の一覧）を観測にする（B 適応の範囲）。clean checkout からの batch build の所要時間は未測定。初回確認は HC1 で Phase C。
- **人間の判断が必要な条件:** なし。
- **高コスト証拠:**
  - **HC1（clean checkout からの batch build と publish）:** 判定 C の実行担当が、GO 候補 head から新しい detached worktree をルート 109 文字以下（予算の上限の確認を兼ねて、可能なら 97 文字以上）の path に作り（`Library/` 無し）、Editor を開かずに AC14 と同じ形で `-osmPurpose spring-full` を起動する。観測: 終了コード、`last-result.json`、published directory の `transport.json`・`content/` の一覧、`outcome.json` の `buildMode`、`preflight.json` の selected、log。期待値の導出規則: (i) 終了コード 0、`buildMode` は `Full`。(ii) `transport.json` の `declaration` は `SampleGameBuildPurposes.SpringFull` の欄（名前 `spring-full`、表現の主値 `Full`、`world:cellCompanionSet=Full`）。(iii) `transport.json` の `files` の path 集合が published directory の `content/` の実 file 集合と一致し、その全 file の SHA-256 が `files` と一致する。(iv) build の前に worktree の `artifacts/bs2b/work/<target>/spring-full/` に番兵 file `bs5b-sentinel.txt` を置き（新しい worktree なので directory を作る）、published directory に番兵が無い。(v) `preflight.json` の selected の stable key 集合が、同じ head の Editor で `SampleGameContentBuild.Resolve(SpringFull)` が返す集合と一致する（Resolve の出力は判定 C が同じ head で取る。BS5a の I4 で `840b6609` の policy と一致することは確かめ済み）。(vi) 最長の path（published directory と staging の中）が 259 文字以下。
  - **HC2（From published directory の検証 Play）:** HC1 の published directory を、この worktree（別 checkout）の Editor で From published directory（path と HC1 の `last-result.json` の manifest SHA-256）から検証 Play する。観測と期待: install の成功、`Title` が Stable、封鎖の 1 行、不一致 Warning 無し、Error 0、停止後の環境の復元（AC17 (b) と同じ記録の項目）。負例を 1 回: manifest SHA-256 の 1 文字を変えて入れる → install されず拒否。
  - 順序は review-evidence の「高コスト証拠と最終証拠ラウンド」に従う（判定 C → 暫定 C' → HC1・HC2 → 最終判定 C と最終 C'）。
- **判定 C の照合 script の方式:** **判定 C の offline 実行。** 照合するのは HC1 の原記録（`transport.json`・`content/` の一覧と hash・`outcome.json`・`preflight.json`・`last-result.json`）の file 検査だけで、通信も資格情報も要らない（publish は local）。判定 C の担当が script を書き、原記録の複製に対して、資格情報・本番設定・通信に届かない環境（sandbox 内の pwsh）で実行する。script は判定 evidence bundle に置く。原記録へ適用する前に通す合成 fixture は、A3 で次の 4 件と期待出力を作って Phase A snapshot に収録し凍結する: (F1) 合格（`files` と実 file が一致、番兵無し、`buildMode=Full`、宣言一致）、(F2) 実 file に `files` に無い 1 file がある → 不合格、(F3) 番兵がある → 不合格、(F4) `outcome.json` の `buildMode` が欠落 → 不合格。期待値の導出は上の (i)〜(vi) の規則で、機械向けの期待値 file と script 本体は git に入れない。
- **機械検査:** `pwsh tools/contract-audit.ps1`（`#nullable enable`、Editor コードの配置、path 予算等）。
- **A0/A1 主担当・モデル・ベンダー:** Claude Opus 5.5（Anthropic）、2026-11-01。
- **A2 独立レビューごとの観点・担当・モデル・ベンダー、入力版:** 未実施。
- **A3 統合担当・モデル・採否:** 未実施。
- **C' 用に予約した担当・モデル・ベンダー:** 未定（A2 の担当を選ぶ時点で、未関与の系列を残す）。
- **独立性の強化条件を満たせない場合の理由:** 未定。

## 6. Phase B 実装結果

未到達。

### 動作確認

未到達。

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未到達。
