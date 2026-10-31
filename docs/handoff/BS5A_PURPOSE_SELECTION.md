# BS5a 目的からの最小選択（中核）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `A`（A3 統合版、分割後の第 2 版。未凍結。発注者の凍結指示待ち）
- branch: `claude/bs5-purpose-selection`
- implementation base commit: `840b6609`（`develop`、S-4d5a の PR #149 のマージ）
- implementation head commit: 未固定
- risk: `high`（Framework の選択 core・materializer と、Runtime の `AssetManagement` の backend 封鎖、`SceneResource` / `SceneBase` / `BuildContentRoot` の公開面に触れる。既存の 5 つの content build 入口と BS4 Player coordinator が同じ経路を通る）
- owner: 発注者 / BS5a 担当
- created: 2026-10-31
- expires: BS5a の Phase D 完了時
- harvest to: Architecture §18（§4 の選択 core・materialization・Content Directory build の現況、§5「Scene 以外のアセット種別」）、§13（Object の表現 fallback、directory mode の fail-closed）、§04 §4.7 / §4.8（directory mode の backend 封鎖と宣言の既定値）、§24 §16.7 / §16.9 / §16.10（`.vfx` の経路と「address でロード」の記述）、S-4 program §6.3 の BS5a 行と d5b 行
- workflow task: `bs5a-purpose-selection`（未開始。最初に Evidence を固定する時点で `pwsh tools/workflow-task.ps1 start` する）
- Phase A snapshot の取得案内・状態: 未生成（凍結した HANDOFF 本体は A3 凍結 commit で固定する）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票: Phase A 作業中。分割後の第 1 版 `782f8d9f` に、ソース突き合わせ評価からの指摘 R1〜R7（§5）と主担当の自己点検を反映した（封鎖の前提と範囲の明文化、サイクル検査の明示、高コスト証拠の前の workspace 削除、不一致検査の欠損 key の扱い、`AssetReference` の事実）。発注者の決定（2026-10-31）で BS5 を 3 つに分けた。本文書は **BS5a（中核: 宣言からの選択と、できた directory 単独の成立性）**。運用経路（Play As・検証 Play の 1 操作・batch・publish の `Full` 必須・Delivery 窓）は **BS5b** として BS5a の後に別 HANDOFF で計画し、入力 identity・設定 snapshot・index は **後続** に送った（§2.5）。分割前の A3 第 3 版は `7e31e58d`（A2 入力版は第 4 版 `dbb87de3`、内容 sha256 `c89f4010fc358e97bf7e9a2e7fc103f6094f17daed76ab6b0a9c499a560e5de1`）。発注者が凍結を指示するまで Phase B に入らない。

---

## 1. 目的と対象外

- **目的:** 「何が必要か」を宣言 1 つで表し、宣言から必要十分な Scene と付随 asset の集合が決定的に導かれ、その集合から作った Content Directory だけで代表経路が動く BuildSystem の中核を作る。(a) 宣言（目的 = Runtime が実際にロードする範囲、Variant = 表現、runtime の既定値）から、必要十分な集合が決定的に導かれる。「必要」は宣言と SceneGraph と付随 asset の宣言が定義する要求集合に対する必要で、実際のロード履歴の最小ではない。SceneGraph と付随 asset の宣言を「その目的で要求しうる content の仕様」とみなすのが設計上の前提で、動的ロードがそこから漏れた時は検証 Play の fail-closed が失敗として見せる。(b) その集合だけで動くことを、Addressables に抜けない fail-closed の検証 Play（既存の Delivery 窓の手入力）で示せる。(c) directory が自分の宣言を持ち、Runtime が宣言と config の不一致を検出できる。既存 5 build の選択を壊さず、BS4 Player に付随 asset と宣言の既定値が内包される。
- **対象外:**
  - **BS5b（運用経路）:** 宣言を選んで build 無しに Play する Play As、宣言 1 つからの build → publish → install → 検証 Play、From published directory、開いている Scene と ledger の拒否、batch 入口、publish の `Full` 必須と build mode、transport staging の path 予算、Delivery 窓の宣言対応。BS5a は既存の menu と Delivery 窓の手入力で判定する。
  - **後続（identity）:** 入力 identity、設定 snapshot、index、`priorRevisions`。BS5a の決定性（I3）は選択集合と閉包の一致で判定し、hash は持たない。
  - 経路の連鎖（key ごとに source → local の object → LAN / R2 の順で探す）、格納と更新の最小性、Player の実行時の content 差し替え、複数 Content Directory の同時登録。
  - InGame の Spring に入る Player の build 経路（BS4 の Player は Title の smoke の後に `Application.Quit(0)` する）。BS4 の経路は壊さないが、Player の動作は変えない。
  - 設定（target、compression、options、defines）を宣言で変える口。設定別の組合せと最小性は未検証のまま残る。
  - World Workspace の transaction への付随 asset の authoring の組み込み。
  - Addressables の退役、旧 Variant 経路の整理。Addressables mode の Editor Play の読み口は従来どおり。
  - §24 §16.7 の所有スライス未定の事項のうち本スライスの問いに関係しない事項。
- **現況:** §1.3（A0、`840b6609` で確認）。

### 1.1 Phase A の判断基準（発注者、2026-10-27。S-4 program §0 から転記）

- **主目的は BuildSystem。** VFX（S-4d）はそのつなぎ込みの題材である。各案で、成果物が content build・Player build の経路に乗るかと、Build 側の既知の課題（BS4 の Player は smoke の後に終了して InGame の Spring に入らない、transport staging の path が予算の外）を踏むかを書く。Build 側の課題は「やらない」で片付けず、BuildSystem の作業の候補として扱う。
- **残すコードは SampleGame に必要なものだけ。** 生成や authoring の道具を残すかは「SampleGame にそのコードが必要か」で決める。不要なら一時スクリプトで作り commit の前に消す。
- **checkout のルート。** Player を build するスライスは、ルートが 96 文字以下の checkout で作業する。この worktree のルートは 31 文字。本スライスの判定で BS4 Player build を高コスト証拠として 1 回取得する（§5）。
- **問いの立て方（発注者、2026-10-31）。** BuildSystem の問いは「目的別・設定別・Variant 別が絡み合い、なおかつ必要最小限になる Build 成果物ができ、必要最小限に格納される」こと、そして SampleGame に引っ張られない汎用的な System になること。問いは「必要なものが分かる」と「必要なものを提供できる経路を作る」に割れ、BS5a は前者の中核を答える。
- **参照者は 4 者（発注者、2026-10-31）。** Editor（日常）・Editor（成果物の検証）・Player・別 PC / Cloud が、それぞれ「必要なものをどう知るか」「何を持っているか」「足りないものをどこから得るか」を持つ（§1.4）。Editor に source があって編集しているなら source を読み、日常の Play に build は要らない。Runtime の key 解決や判定の読み口は参加者の内側の機構で、参照者ではない。判定は難しくしない。
- **実ユースケースで通す（発注者、2026-10-31）。** 選択の規則だけでなく、§1.5 のユースケースのうち BS5a の列が成り立つことを確かめる。証明の経路が Addressables に抜けていないことを先に確かめる。
- **分割（発注者、2026-10-31）。** 変更理由で BS5a（中核）・BS5b（運用経路）・identity 後続に分ける。高リスクの Runtime 変更だけを先に判定・C' する。

### 1.2 S-4 program から転記した制約

- **SceneGraph（program §2.1）。** `InGameSession` の下に `Season_{Season}`（OnDemand）、その下に `{Season}_Lighting`・`{Season}_AtmosphereVFX`（NecessaryAlways）、`{Season}_Cell_{x}_{y}`（OnDemand、StreamByDistance）、`{Season}_Event_{slug}`（OnDemand）。Cell の下に `{Season}_Environment_{x}_{y}`（OnDemand、resident companion）、`{Season}_Lighting_{x}_{y}`・`{Season}_VFX_{x}_{y}`・`{Season}_Events_{x}_{y}`（OnDemand、optional companion）。
- **Planner Play（program §3.1 / §6.2）。** `content:representation=Whitebox` と `world:cellCompanionSet=Planner` を使い、Environment をロードしない状態で local / major Event を実証する。companion set は Cell の companion だけを選別し、Season の子（Lighting・Atmosphere・Event）は対象外。
- **d5b の行（program §6.3）。** 前提は BS5a の Phase D 完了。d5b は今日と同じ手順（環境変数と profile を手で置く source の Play）で進められ、Play As（BS5b）を待たない。
- **VFX の寿命（program §2.4、Architecture §24 §16.9 / §16.10）。** 一時 VFX の source asset は Scene が `AssetOwner.Scene(identity)` で所有してロードし、Scene の unload で VFX を回収してから asset を解放する。本スライスは所有者と順序を変えず、asset の指し方だけを変える（§2.4 D5）。
- **公開 API と依存（program §10）。** Framework は season 名、Cell 座標文法、職種 role を知らない。Game → Framework の依存方向を維持する。`SceneState` の 14 値と `LoadType` を変えない。
- **検証方針（program §11）。** `unity test` / `unity run`、Scene / asset YAML の手編集は禁止。Phase B は Editor 操作・compile 確認まで、Unity テストと Build は Phase C。

### 1.3 A0 の現況（`840b6609`）

**Editor の日常 Play の経路。** 起動時の mode は config の `content:runtimeMode` で決まる。**未指定の Editor Play は、Delivery の verified pair が無ければ `BeforeSceneLoad` で失敗する契約**（Architecture §04 / §20、`InvalidCompanionConfigStartupTests` が同じ文言を期待する）で、source を読む経路は **明示 `addressables` だけ**が口になる。`app-config.json` は `content:runtimeMode` を持たず、Play テストは環境変数 `SAMPLEGAME_CONTENT__RUNTIMEMODE=addressables` を置いて source を読む。明示 addressables の Play は Addressables backend + 既定の Play Mode script（AssetDatabase 直読み）で source をそのまま読み、build は要らない。**`app-config.json` 自体も `JsonFileConfigProvider` が Addressables 経由（`LoadAppAssetSync(AssetKey.FromAddress)`）で読む**ので、Editor では mode が決まる前に Addressables の初期化が必ず走る（Player の BS4 経路は `StreamingAssets` の required JSON で Addressables を使わない）。表現は `ResolveSceneVariant` が **Editor の active profile（`DeveloperVariantSettings`、`UserSettings/OneStarMaker/` に永続化。profile は `Default`（空）・`Production`（空）・`WorldWhitebox`（Whitebox））を config の `assets:sceneVariant` より優先**し、profile 未選択の時だけ config を読む。companion set は `world:cellCompanionSet`。config は `app-config.json` → 環境変数（prefix `SAMPLEGAME_`、`__` を `:` に写す）→ command line の順に重なる。発注者の「日常 Play は source を読み build は要らない」は現状ではなく要件で、今は mode の明示・profile の切替・companion set の環境変数の 3 つを手で揃える必要がある（BS5b の Play As が担う）。

**build の経路。** source → `SceneResourceContentMaterializer`（`SceneResourceMap` の Scene payload だけを候補にする）→ `SeasonSceneSelectionPolicy`（季節・表現の request と requirement）→ `BuildTagSelector`（pure）→ bootstrap composer（UICommon と `SceneResourceMap.asset` を固定 content として合成）→ `BuildContentProjection`（閉包の照合）→ `UnityContentDirectoryAdapter`（`BuildContentRoot` を生成して `BuildPipeline.BuildContentDirectory`。workspace は `work/<target>/<contentSet>` を再利用するので Unity 側は incremental）→ `BuildContentCoordinator`（workspace を丸ごと staging へ複写し `artifacts/bs2b/content/<identity>/` へ移して公開。workspace は contentSet ごとに再利用され、複写は全 file なので、Unity が古い出力を消さなければ前回の選択の file が成果物に混ざる。Unity が消すかは未確認。BS5b の `Full` 必須がこれを断つ）→ `ContentTransportPublisher.Publish`（BS4 だけが呼ぶ）→ DIST install → Editor の検証 Play（Delivery 窓の Use For Next Play）または BS4 Player。

**選択 core（`BuildTagSelector`、302 行、pure）。** 候補は `StableKey / LogicalKey / PhysicalKey`。request は dimension ごとの OR、dimension 間の AND。候補の tag が request に無い dimension なら `UnrequestedDimension`、値が選ばれていなければ `ValueNotSelected` で除外。tag の無い dimension は中立。requirement は logical key ごとの `ExactlyOne` / `OneOrMore` で、選ばれた候補の数だけを数える。**requirement の無い logical key の候補を除外する手段は無い。**

**季節 policy（`SeasonSceneSelectionPolicy`、243 行、SampleGame）。** graph を複製して `Season` を祖先から導き、in-scope の全 node を requirement にする（最小ではなく季節の全部）。`ValidateGraph` が直接検査するのは identity の重複・root の存在・orphan・親子リンクの相互一致の 4 つで、**サイクルを止めているのは季節判定 `FindSeason` の祖先走査の `visited` だけ**。Editor 側の `SceneGraphValidator.ValidateCycles` は自己ループしか検出せず、Runtime の `CollectNecessaryScenes` はサイクルに入ると止まらない。Whitebox 選択は `SeasonalMode` tag（切替可能な group にだけ付ける）で Full 専用の補助 Scene を残す。候補と node の有無が一致しないと "Payload/candidate mismatch" で止める。`FullAndWhitebox` は `OneOrMore` と `VerifyDual` で両表現を要求する。

**目的の不在。** 目的に相当するのは runtime の `CellCompanionSet`（Full / Planner / Lighting / Vfx）と `CellCompanionRoleClassifier`（identity の末尾 3 token 目で Environment / Lighting / VFX / Events を判定。`SampleGame.InGame` の internal）だけで、build 側に dimension が無い。Planner 用の Whitebox build は Planner が読まない Environment 54 件・Cell Lighting 2 件・Cell VFX 1 件を含む。逆に、Planner 用の directory を companion set Full で Play すると Environment の Add が 54 回失敗して Warning になり（`SessionCellCompanionLoadDriver` は失敗を Warning で飲む）、representation を Full にすると Cell の Whitebox は Full へ fallback しないので Scene が欠ける。**directory は自分が何の目的で作られたかを持っていない。**

**実測の graph（`SceneResourceMap`、457 node）。** OutGame: `OutGameScene`（NecessaryAlways、root）、`Title`・`HpGauge`・`OutGameSession`（OnDemand）、`ConfirmDialog`（HpGauge の子）、`HomeScene`（OutGameSession の子、payload 無し）。InGame: `InGameScene`（root、payload 無し）、`InGameSession`（NecessaryAlways、payload 無し）、`InGameUI`・`PlayerScene`（NecessaryAlways）、`Result`（OnDemand）、`Season_*` 4 件（OnDemand、payload 無し）。各 Season: `{S}_Lighting`（NecessaryAlways）、Spring だけ `Spring_AtmosphereVFX`（NecessaryAlways）と `Spring_Event_MultiCellProof`（OnDemand）、Cell 54 件（OnDemand、Variant は空と Whitebox）。各 Cell: Environment 1 件、Spring の (4,2)(5,2) に Lighting、(4,2) に VFX と Events。`Tunnel` は無い。

**実測の成果物（2026-10-30 の `spring-whitebox`）。** selected 124 件 = Scene payload 122 件 + bootstrap 2 件。Unity の出力は hash 名の `.cf` / `.resS` と hash 名の manifest `.json`、固定名 `BuildManifestHash.txt`、計 233 file・6.2 MB。manifest は `RootAssets 1 / Loadables 2 / LoadableScenes 123 / SerializedFiles 196`。`AssetDatabase.GetDependencies` による source 側の閉包は 608 file（Unity の SerializedFiles とは単位が違う）。Unity の manifest は `SerializedFiles` ごとに `ContentHash` と `SerializedFileDependencies` を持ち、Scene 1 件の推移的な file 閉包は平均 7.3・最大 37、50 Scene 以上から共有される file は 6 件。on-disk の content build は 1 件だけで、再 build の bytes 比較はできていない。

**build identity。** `DateTime.UtcNow + Guid`（`yyyyMMddTHHmmssfffZ-<guid32>`）で、入力から導出されない。`outcome.json` は identity・path・summary だけで、選択と閉包は `preflight.json` が正本。transport の `revision` は identity、manifest の SHA-256 は install の照合に使う。この形式は tools/Artifacts の 5 箇所（`ContentContract.psm1` の `Assert-ContentRevision`、`ContentCatalog.psm1`、`BuildContract.psm1`、`Transport/R2ArtifactTransport.psm1` と `ArtifactReadback.ps1` の key の allow-list）が正規表現で固定しており、Runtime の `ContentManifestValidation` は revision を安全な path segment としてだけ検査する。形式を変えると Storage program（完了、保留 list 付き）を再開することになる。Runtime の `ContentManifestValidation` は manifest の `unityVersion` を定数 `6000.6.0f1` と等値で検査するので、Unity を上げると定数を直さない限り install が全て拒否される。DIST の `ContentCacheStore.MarkKnownGood`（known-good pin）は製品コードから呼ばれておらず（テストだけ）、Architecture §13 は「register / load の成功後に pin」と書くが呼ぶ側が無い。起動は known-good を自動適用しない契約なので、pin は eviction からの保護にだけ効く。

**閉包と動的ロード。** `AssetDependencySnapshotBuilder` が `AssetDatabase.GetDependencies` で root ごとに閉包を取る。Scene 内に置いた `.vfx`（`Spring_AtmosphereVFX`・`Spring_VFX_4_2`）は閉包で同梱される。**address で動的にロードする asset は 3 箇所ある:** `Spring_Events_4_2_Burst.vfx`（`CellCompanionScene`、所有者 `Scene(Events)`）、`Spring_Event_MultiCellProof_Burst.vfx`（`SeasonMajorEventScene`、所有者 `Scene(Event)`）、**`DemoCellLit.mat`（`SeasonScene.OnPreLoadedImpl`、4 つの Season が同じ material を各 `Scene(Season)` 所有でロード）**。material は Cell の閉包で directory に入っているが、key で引ける `Object` entry ではない。`.vfx` 2 件は閉包にも入らない。`BuildContentRoot` の `Object` entry を本番 build で埋める経路は bootstrap composer（`SceneResourceMap.asset`）と BS4 の `Bs4FixtureComposer`（probe prefab）だけで、どちらも選択 policy を迂回する合成。

**`SceneResourceMap` の参照の形。** `AssetPayload` は `AssetReference`（GUID 文字列）で payload を指し、hard reference ではない。だから map の `AssetDatabase.GetDependencies` 閉包に Scene や payload は入らず、bootstrap で同梱する map が全 Scene を引きずらない。付随 asset を同じ `AssetPayload` で宣言すれば、到達しない node の付随 asset が map 経由で全 directory に混ざることも無い。

**Runtime の backend 構成。** `AbstractApplicationInitializer` は mode に関係なく `new AssetManagement()` を作り、これは常に `AddressableBackend`（`IAssetBackend` の実装。`_backend` は `readonly`）を持つ。**`LoadAssetAsync` / `LoadAppAssetSync` は backend を呼ぶ前に registry（ロード済み）と resident cache を見て、同じ canonical key が in-flight なら backend を呼ばずにその task を共有する。** つまり backend を差し替えても、封じる前に取得した key は registry と cache から引き続き取れる。**したがって directory mode の Editor Play でも `AssetKey.FromAddress` のロードは AssetDatabase から静かに成功する。** `IAssetBackend` の取得系は `LoadAssetAsync` / `LoadAssetSync` / `LoadSceneAsync` / `InstantiateAsync`、teardown は `Release` / `UnloadSceneAsync`。`AssetManagement.InstantiateAsync(AssetKey)` も backend へ直接届く。検証 Play は「directory だけで動く」ことを証明しない。Player では Addressables の catalog を build しない（`DoNotBuildWithPlayer`）ので、BS4 Player は Season に入った時点で `DemoCellLit.mat` のロードで落ちる見込み（Title の smoke で止まるので顕在化していない）。directory の登録と解除は `ContentLoadManager.RegisterContentDirectory` / `UnregisterContentDirectory` の runtime API で、`ContentDirectorySession` が rollback 込みで包む。

**開いている Scene の再利用。** `PerformUnitySceneLoad` は `SceneManager.GetSceneByName` でロード済みの Scene を見つけるとロードせずそれを使い、`LoadUICommonAsync` も同様。Editor で宣言の Scene や UICommon を開いたまま Play に入ると、directory mode でも source の Scene がそのまま使われる。BS5a では検証 Play の操作手順で閉じることを要求し、機械の拒否は BS5b。

**Runtime の読み口。** `SceneBase.LoadSceneScopedAssetAsync(AssetKey)` だけがあり、directory の `IAssetManagement.LoadContentAssetAsync(logicalKey, representation, owner)` は Scene から届かない。`ContentDirectoryIndex` の Object 解決には Scene にある Full fallback が無い。`SceneDirector` は `_useContentDirectory` と `_sceneVariant` で Scene のロードを分岐し、`SceneBase` には `BindAssets(IAssetManagement)` だけを注入する。`AssetManagement` は canonical key で backend の asset を共有し、所有者ごとに参照を数える。

**検証 Play の publish と install。** `Tools/OSM/Content/Build ...` 4 つは Content Directory を生成するだけ。`ContentTransportPublisher.Publish` を Editor 向けに呼ぶ入口は無く、§20 / §4.8 / SEASON design が「未整備」と書く。Delivery 窓は source・manifest SHA-256・contentSet・revision・cache root・representation・budget の 7 欄を手で入れて Prepare → Use For Next Play する。`ContentDeliveryPlayBridge` は `SAMPLEGAME_CONTENT__*` の環境変数 5 つを process scope に置き、以前の値を Unity の `SessionState` の ledger に残す。`world:cellCompanionSet` と表現の profile は扱わない。manifest の `version == 1`・`rootSchemaVersion == 2` は固定で要求され、`JsonUtility` は未知の field を無視する。BS5a はこの窓を変えず、判定 C が手入力で使う。

**既存テスト。** `BuildTagSelectorTests`（481）、`SceneResourceContentMaterializerTests`（431）、`BuildContentProjectionTests`（111）、`SeasonSceneSelectionPolicyTests`（181）、`ContentDirectorySessionTests`（763）、`ContentDirectoryIndexTests`（83）、`BuildContentDirectoryIntegrationTests`（490、SampleGame、`TestResults/bs3-fixture` へ実 directory を build する）、`Bs4FixtureComposerTests`、`LocalVfxEventPlayTests`、`SeasonMajorEventPlayTests`（394 + 343）、`PlayerContentConfigurationTests`、`ContentDeliveryFailureMatrixTests`。

### 1.4 参照者と経路（A0）

4 者が「必要・所持・経路」を持つ。BS5a は「必要をどう知るか」の列を宣言で埋め、BS5b がその宣言を各参照者の操作に接続する。

| 誰 | 必要なものをどう知るか | 持っているもの | 足りないものの経路 | BS5a 後 | BS5b 後 |
|---|---|---|---|---|---|
| Editor（日常） | 開いている Scene graph と、選んだ宣言の runtime 既定値 | AssetDatabase の source（明示 addressables の経路） | （後続）LAN / R2 の object | 変えない。手で mode・profile・companion set を置く | 宣言を選んで Play する（Play As） |
| Editor（成果物の検証） | 宣言 | installed directory | なし。fail-closed | menu の `Build` と Delivery 窓の手入力で、封じた directory の検証 Play ができる | 宣言 1 つから build → publish → install → 検証 Play |
| Player | baked の宣言 | 内包 / installed | （後続）R2 の object の差分 | BS4 Player に付随 asset と宣言の既定値が内包される | 変えない |
| 別 PC / Cloud（build する側） | 宣言名 | clean checkout の source と Unity | なし | menu | batch で build と publish |
| 別 PC / Cloud（受け取る側） | published directory の manifest の宣言 | published directory、または cache | （後続）R2 の object | manifest が宣言を持つ | From published directory |

### 1.5 ユースケースと足りないもの（A0）

| # | 誰が何をする | 今 | BS5a 後 | BS5b / 後続 |
|---|---|---|---|---|
| UC1 | 企画者が Whitebox Cell や Event を直してそのまま Play | `content:runtimeMode=addressables` を明示すれば source route で動く。未指定は失敗する契約 | 契約は変えない（I8） | — |
| UC2 | 企画者が Planner profile（Environment 無し）で Play | mode の環境変数・表現の profile・companion set の環境変数を手で揃える | 変えない。d5b はこの手順で進める | Play As（BS5b） |
| UC3 | 企画者が自分の持たない asset を含む Scene を Play | 不可 | 変えない。必要集合は宣言から出る | 経路の連鎖と object store（後続） |
| UC4 | 担当が「この目的の成果物だけで動くか」を Editor で確かめる | 窓に 7 欄を手入力。Addressables に抜ける | 同じ手入力で、Addressables に抜けない fail-closed の検証 Play になる（D10、AC15） | 1 操作、開いている Scene の拒否（BS5b） |
| UC5 | QA / release 向けに目的 X の Player を作る | BS4 は Spring の全部を内包、`.vfx` は入らない、companion set は手書き | 付随 asset が入り、既定値は宣言から（D5・D11、AC17）。確かめるのは smoke と内包内容まで | Player の実動作（後続） |
| UC6 | CI が clean checkout で目的 X を build し publish する | menu しか無い | 変えない | batch と `Full` 必須（BS5b） |
| UC7 | 別 PC が published directory を受け取った後、Editor で検証 Play | 窓に手入力 | 変えない | From published directory（BS5b） |
| UC8 | 同じ source・同じ宣言で 2 回 build する | 2 つの directory ができる | 変えない。選択集合は `preflight.json` で比べられる（I3） | 入力 identity と index（後続） |
| UC9 | 同じ Scene を複数の目的で build する | 目的ごとに全部格納 | 変えない | object 単位の格納（後続） |
| UC10 | Player が動いたまま content を差し替える | 不可 | 変えない | 実行時差し替え（後続） |
| UC11 | 企画者が Scene に address ロードを足し、宣言に書き忘れる | 日常 Play では気づかず、Player で落ちる | 検証 Play（UC4）で fail-closed に当たる | 日常 Play での宣言外ロードの検出（後続） |

---

## 2. 意思決定と受け入れ境界（A1）

- **このスライスが答える問い:** BuildSystem は、目的・表現・runtime 既定値の宣言から、その目的で発生しうる content 要求（宣言と SceneGraph と付随 asset の宣言が定義する要求集合）を決定的に導き、その要求を満たす自己完結した Content Directory を作れるか。宣言に対する選択の過不足を静的に検証でき、成果物が他の backend へ抜けずに動くことを観測でき、既存の build を壊さず、Player に付随 asset と宣言の既定値が内包されるか。

### 2.0 証明の範囲

| 主張 | 何で示すか | 示さないこと |
|---|---|---|
| 静的な選択の正しさ: 宣言の要求集合に対して過不足が無い | AC1〜AC6・AC8・AC20（到達集合、選択集合、閉包、禁止要素の不在、理由の記録） | SceneGraph と付随 asset の宣言に書かれていない Runtime 要求の網羅 |
| 成果物の自己完結: 観測した実行経路で成果物の外へ抜けずに動く | AC13（代表経路の検証 Play。Delivery 窓の手入力）、AC16・AC21（封鎖と欠落の負例）、AC19（宣言と config の不一致の検出） | 到達可能な全 Scene が実際にロードできること（総当たりの Play は行わない。未証明と記録する） |
| 出力の最小性 (1): 選択外の logical root を build に渡さない | AC1〜AC5（selected だけが `BuildContentRoot` の entry になる） | — |
| 出力の最小性 (2): publish の成果物に前回の workspace の残骸が混ざらない | BS5b（`Full` 必須） | 今回の build が余剰の物理 file を出していないこと（物理 file 単位の最小性は未証明） |
| 出力の最小性 (3): 選択内容と依存閉包が妥当 | AC8・AC9・AC14（付随 asset が `Object` entry になり解決できる） | bytes の決定性、revision 間の重複の無さ |
| Player | AC14（BS4 の smoke と、root・manifest・baked config の内包内容） | Spring の実動作 |

### 2.1 不変条件（起きてはいけないこと）

- **I1（必要なものを落とさない）:** 宣言の root から、`NecessaryAlways` / `IncrementalAlways` の子を全て、`OnDemand` の子を目的の filter が通すものだけたどって到達する node は、payload があれば必ず選ばれる。到達する node の付随 asset も必ず選ばれる。graph にサイクル・多親・片側リンク・orphan があれば plan を公開せず issue で止め、`Unreachable` として黙って除外しない。
- **I2（不要なものを入れない）:** 到達しない node の候補と、到達しない node の付随 asset は選ばれない。表現の優先順で選ばれなかった候補も選ばれない。
- **I3（決定的）:** 同じ宣言と同じ source からは、同じ選択（stable key の集合）と同じ閉包が得られる。候補・provider・node の入力順に依存しない。
- **I4（既存の成果物を変えない）:** 既存 4 つの content build と BS4 の `bs4-spring-full` は、新しい宣言に置き換えた後も selected の stable key 集合が `840b6609` の policy と一致する（付随 asset の追加分を除く）。
- **I5（Framework は語彙を知らない）:** Framework の選択・materialization・Runtime の読み口・宣言の運搬は、season 名・Cell 座標文法・職種 role・`CellCompanionSet` を参照しない。宣言の runtime 値は Framework にとって不透明な key=value。Game → Framework の依存方向を維持する。
- **I6（寿命と順序を変えない）:** 付随 asset は従来どおり読む Scene が `AssetOwner.Scene(identity)` で所有し、VFX の回収 → asset の解放の順序、d4b / d5a の既存テストの結果を変えない。`SceneState`、`LoadType`、asmdef 参照を変えない。
- **I7（directory の外を読まない）:** directory mode の検証 Play と Player は、mode が directory と決まった後、Addressables backend から取得しない。取得系の backend 呼び出し（asset の同期・非同期ロード、Scene ロード、instantiate）は入口の数え方によらず全て失敗し、解放と unload は通る。封じる前に取得済みの key（現状は config の `TextAsset` 1 件）は registry から引き続き取得でき、それ以外は封じた後に registry・cache・in-flight のどれからも出てこない。例外は mode を決めるために config を読む 1 回だけ。検証 Play の開始時に宣言の Scene と UICommon を Editor で閉じておくのは操作手順の要件（機械の拒否は BS5b）。
- **I8（日常 Play の契約を変えない）:** Addressables mode の Editor Play の読み口・起動順序・config の読み方・mode の契約（未指定は verified pair が無ければ失敗、明示 `addressables` だけが source の口）は変えない。
- **I9（識別子の形式を変えない）:** build identity（revision）、transport の `revision`、DIST の `installed/<contentSet>/<revision>`、Storage の key の形式は変えない。

### 2.2 成立条件（起きなければならないこと）

- **E1（宣言）:** 目的・Variant と runtime の既定値を 1 つの宣言型で表せ、SampleGame の 5 つの content set（`all-full`、`spring-full`、`spring-whitebox`、`spring-full-whitebox`、`bs4-spring-full`）と新しい `spring-planner-whitebox` が宣言として存在する。
- **E2（最小選択）:** `spring-planner-whitebox` の選択は、Spring の Cell 54 件（Whitebox）、`Spring_Events_4_2`、`Spring_Event_MultiCellProof`、`Spring_Lighting`、`Spring_AtmosphereVFX`、InGame / OutGame の到達 Scene、bootstrap 2 件、付随 asset 3 件で、Environment・Cell Lighting・Cell VFX・他季節を含まない（§2.3 AC4 の件数）。
- **E3（付随 asset と共有 asset）:** `Spring_Events_4_2` と `Spring_Event_MultiCellProof` の `.vfx`、`InGameSession` が所有する `DemoCellLit.mat` が、所有 node が到達する全ての宣言の Content Directory に `Object` entry として入り、読む Scene から所有者と名前で解決できる。Addressables mode の Editor Play でも同じ呼び出しで解決できる。
- **E4（宣言の運搬）:** 宣言の名前・表現の主値・runtime の key=value が `BuildContentRoot` と `transport.json` に載り、Runtime は directory mode の起動で宣言と config の不一致を Warning で出し、BS4 Player の baked config は宣言から写される。
- **E5（fail-closed の実証）:** 既存の Delivery 窓の手入力で `spring-full` の directory を Use For Next Play した検証 Play で、Cell (4,2) の Events の trigger の VFX と Season の Lit が解決され、Error ログが出ず、config の後に Addressables 経路が封じられ、宣言と config の不一致の Warning が無い。

### 2.3 受け入れ条件（検証する条件を併記）

単体 = Unity に依存しない純 C# テスト。EditMode = AssetDatabase や合成 fixture を使うテスト。Play = Play Mode テスト。操作 = Phase C の実行。

| AC | 内容 | 条件 | 種別 |
|---|---|---|---|
| AC1 | 合成 graph（root、NecessaryAlways / IncrementalAlways / OnDemand の子、payload 無しの中間 node、付随 asset 付きの node）で、到達規則どおりの requirement 集合が得られる。OnDemand の子は filter の dimension ごとの OR・dimension 間の AND・tag 無し中立・未要求 dimension の tag は拒否、の規則で判定される。payload の無い到達 node の付随 asset も requirement になる。requirement ごとに root からの到達経路が付く | I1・I2 | 単体 |
| AC2 | root でない node、到達しない subtree の node、到達しない node の付随 asset の候補は `Unreachable` の理由で除外される。到達する node の候補を欠くと `CardinalityViolation` で plan が公開されない。graph の検証: 2 node の相互親サイクル、root から到達しないサイクルの島、多親、片側リンク、orphan、root の欠落は plan を公開せず issue で止まる（サイクルの島を `Unreachable` で通すのは不合格） | I1・I2 | 単体（失敗経路） |
| AC3 | requirement は (logical key, 表現) ごとの `ExactlyOne` で、期待集合は node の宣言済み payload から作る。`First` と優先順 `[Whitebox, Full]` で、Whitebox を宣言した key は Whitebox だけ、Full しか宣言しない key は Full が選ばれる。`All` は優先順にあって node が宣言する各表現を 1 つずつ要求する。負例: node が Full と Whitebox を宣言し materializer の Whitebox 候補だけが欠けている（`CardinalityViolation`）、同じ表現の候補が 2 つある（`CardinalityViolation`）。候補集合から片方を消すだけのテストと混同しない。優先順に無い表現の候補は除外される | I1・I2 | 単体 |
| AC4 | 実 `SceneResourceMap` で `spring-planner-whitebox` の selected が、Role ∈ {Environment, Lighting, Vfx} の Cell companion を 1 件も含まず、Spring の Cell 54 件を全て Whitebox で含み、`Spring_Events_4_2`・`Spring_Event_MultiCellProof`・`Spring_Lighting`・`Spring_AtmosphereVFX` と付随 asset 3 件（`.vfx` 2 件、`InGameSession:Lit`）を含む。件数は Scene payload 65 + bootstrap 2 + 付随 3 = 70（65 は §1.3 の実測 122 から導いた概算。`Result`・`ConfirmDialog`・`Spring_Event_MultiCellProof` のどれか 1 件が payload を持たないことを含意する。A3 の前に logical key の一覧（§5 の (iii)）を書いて件数を確定し、一覧が正で件数は従う） | E2 | EditMode |
| AC5 | 実 `SceneResourceMap` で `spring-whitebox` の selected が、§1.3 の実測 124 件の stable key 集合 + 付随 asset 3 件に一致する。`spring-full`・`all-full`・`spring-full-whitebox`・`bs4-spring-full` も `840b6609` の policy の選択 + 付随 asset に一致する。期待値は出所を分けて凍結する: `840b6609` の Editor で実測した旧 policy の stable key 集合（5 件）、付随 asset 3 件の stable key（`osm-build-candidate-v1` 規則と GUID から手で導く）、`spring-planner-whitebox` の 70 件（新しい選択規則の仕様から導いた期待。§5 の照合 script） | I4 | EditMode + 判定 C の照合 |
| AC6 | 候補・provider・node を逆順・シャッフルで与えても、plan の selected / excluded の列と到達経路が同じ | I3 | 単体 |
| AC7 | `BuildPurposeDeclaration` は `RuntimeDefaults` に `content:` で始まる key があると `ArgumentException`。名前は contentSet の規則、root は 1 つ以上、表現の優先順は空でない | E1 | 単体 |
| AC8 | materializer が任意の node（payload の無い node を含む）の付随 asset から候補（logical key `<identity>:<name>`、Representation は payload の Variant、所有 node の identity は候補の metadata で tag ではない）と閉包を作る。所有者 metadata は selector の判定に現れない。名前の重複、参照欠損、同じ physical GUID を 2 つの node が宣言する、所有 node の identity と衝突する名前、は issue で止まる | E3 | EditMode（既存 materializer テストと同じ偽 gateway） |
| AC9 | 合成 fixture directory（`TestResults/bs3-fixture`）に Scene 1 件と付随 `Object` 1 件（Full）を build し、representation `Whitebox` で Scene を登録した session から付随 asset が Full fallback で解決・ロード・解放できる。存在しない名前は `EntryMissing`。同じ entry を 2 つの所有者で取得して片方を解放しても、もう片方は有効 | E3 | EditMode（既存 integration テストの拡張） |
| AC10 | `SceneBase.LoadAttachedAssetAsync<T>(name)` と `LoadAttachedAssetAsync<T>(ownerIdentity, name)` は、directory モードでは `LoadContentAssetAsync(<owner>:<name>, sceneVariant, Scene(自分の identity))`、それ以外では `LoadAssetAsync(AssetKey.FromDescription(所有 node の付随 desc, sceneVariant), Scene(自分の identity))` を呼ぶ。`ownerIdentity` は自分か祖先に限り、それ以外・未宣言の名前は `ArgumentException`。所有者は常に読む Scene | E3・I6 | 単体（偽 `IAssetManagement`） |
| AC11 | 既存の `LocalVfxEventPlayTests` と `SeasonMajorEventPlayTests` が、trigger / anchor の address を付随 asset の名前に替えた後も通る（Addressables モード、`-WithGraphics`） | I6・I8 | Play |
| AC12 | `Spring_Events_4_2`・`Spring_Event_MultiCellProof` の `SceneResource` に付随 asset `Burst`、`InGameSession` に付随 asset `Lit`（`DemoCellLit.mat`）が宣言され、Variant は空。trigger / anchor の名前が `Burst`、`SeasonScene` が `InGameSession:Lit` を読む | E3 | EditMode |
| AC13 | 宣言の Scene と UICommon を閉じた状態で、BS4 Player build（AC14）が publish した `bs4-spring-full` の published directory（`spring-full` の選択 + probe prefab。Editor 向けの publish 入口は BS5b なので、BS5a では publisher を通る唯一の経路）を Delivery 窓の手入力で Prepare → Use For Next Play し、Play して Spring (0,4) が Stable になり、Cell (4,2) へ移動して Events の trigger に入ると VFX が起動する。Console に Error が無く、起動ログに Addressables 経路を封じた 1 行があり、宣言と config の不一致の Warning（AC19）が無い（root の宣言は `bs4-spring-full` の `cellCompanionSet=Full`・表現 Full で、Editor の `app-config.json` と窓の representation に一致する）。この AC が示すのは「観測した経路で成果物の外へ抜けずに動いた」ことで、全 Scene のロード可能性ではない（§2.0） | E5・I7 | 操作（Phase C）、証拠は Console ログ |
| AC14 | BS4 の `Build BS4 Spring Player` が成功し、Player の smoke receipt が `completed`。Player 内 `content/` に付随 asset 3 件の `Object` entry が入る（manifest の `Loadables` が 2 + 3 = 5）。`bs4-runtime.json` の `cellCompanionSet` と `representation` が宣言の値 | I4・E3・E4 | 高コスト証拠（§5） |
| AC15 | 最終の全 EditMode 回帰と、Streaming・Vfx の PlayMode が通る | I6・I8 | 回帰 |
| AC16 | `AssetManagement.SealAddressables()` の後、`IAssetBackend`（internal）の取得系の member 全部（`LoadAssetAsync` / `LoadAssetSync` / `LoadSceneAsync` / `InstantiateAsync`。テストが reflection で列挙し、`Release` / `UnloadSceneAsync` 以外を取得系とみなす。列挙は将来の member 追加の分類漏れを拾うためで、封鎖の証明は負例）が拒否 backend で `InvalidOperationException` になる。`IAssetManagement` の公開面では `LoadAssetAsync(FromAddress / FromDescription)`・`LoadAppAssetSync`・`LoadSceneAsync(desc)`・`InstantiateAsync(AssetKey)` が失敗し、封じる前に取得した handle（config の `TextAsset`）は有効なまま解放でき、封じる前にロードした Scene は unload できる。directory の `LoadContentAssetAsync` / `LoadContentSceneAsync` / `InstantiateContentAsync` は動く。負例: 封じる時に in-flight のロードがあると `SealAddressables()` は `InvalidOperationException`。封じる前に registry に無い address key は、封じた後に別の所有者が `LoadAssetAsync` しても取得できない。封じる前に resident cache へ戻した Addressables 由来の asset は、封じた後に `TryTake` で出てこない（封じる時に cache から落とす）。封じる前に registry にある key（config の `TextAsset`）は、封じた後に別の所有者が取得できる（許可。記録した key の集合と一致する）。`AbstractApplicationInitializer` は `ReadContentDirectoryConfiguration` が directory を返した直後に封じ、addressables では封じない | I7・I8 | 単体（reflection の列挙 + 公開面と registry / cache / in-flight の負例）+ EditMode（既存 `ContentDirectorySessionTests` の構成） |
| AC17 | `InGameSession` の付随 asset `Lit` は候補 1 件で、4 つの `SeasonScene` が `LoadAttachedAssetAsync("InGameSession", "Lit")` で同じ backend asset を各 `Scene(Season)` 所有で取得し、1 つの Season を unload しても他の Season の handle は有効 | E3・I6 | 単体（偽 `IAssetManagement` で呼び出しの形）+ AC9（共有の解放） |
| AC18 | `BuildContentRoot.Declaration` と manifest の `declaration` に宣言の名前・表現の主値・runtime の key=value が入り、無い root / manifest は従来どおり受理される（`ContentManifestValidation` と `JsonUtility` の往復）。Player の `PlayerContentConfiguration.Resolve` は baked config に無い key を宣言から補わない | E4・I5 | 単体 + `ContentManifestValidationTests`・`PlayerContentConfigurationTests` |
| AC19 | directory mode の起動で、`BuildContentRoot.Declaration` の runtime key=value のうち config の値が違う key があれば、key と両方の値を Warning 1 行で出す（Framework は key を解釈しない。宣言の無い root は何も出さない）。config に key が無い場合は値 `<absent>` として不一致に数える（Runtime の既定値が宣言と違いうるため）。合格例: `world:cellCompanionSet` が宣言 Planner・config Full で Warning 1 行、config に key が無くても Warning 1 行。不合格例: 一致しているのに出る、違うのに出ない、欠損を一致と扱う | E4・I5 | 単体（合成 root と config） |
| AC20 | SampleGame の 6 宣言で、`RuntimeDefaults` の `world:cellCompanionSet` と filter の Role の対応が Game の規則どおり（Planner なら Role ∈ {Environment, Lighting, Vfx} を filter が通さない、Full なら全 Role を通す）。対応表は `SampleGameBuildPurposes` が持ち、宣言を足す時にテストが対応を要求する | E1・I5 | EditMode（`SampleGameBuildPurposesTests`） |
| AC21 | 合成 fixture directory（AC9）から付随 `Object` entry を 1 件欠いた root で session を作り、`LoadAttachedAssetAsync` が `EntryMissing` の `ContentDirectoryException` で失敗し、Addressables backend は呼ばれない（偽 backend の呼び出し回数 0）。失敗の logical key と representation が例外に入る | I7・E3 | EditMode（AC9 の拡張） |

AC4 の読み方: 「Role ∈ {...} を含まない」は selected の logical key に `_Environment_`・`_Lighting_{x}_{y}`・`_VFX_{x}_{y}` の Cell companion が無いこと。`Spring_Lighting`（Season の NecessaryAlways）は含む。合格例: 70 件で Environment 0 件。不合格例: Environment が 1 件でも入る、Cell が 53 件、`InGameSession:Lit` が無い。

AC13 の読み方: 「Addressables 経路を封じた 1 行」は `content:runtimeMode=directory` の Play で config の後に backend が拒否 backend に差し替わったことを示す起動ログ（D10）。ログは補助で、封鎖の証拠は AC16 と AC21 の負例テスト。合格例: その行があり、不一致の Warning が無く、Lit と VFX が解決される。不合格例: その行が無い、Warning が出ている（宣言と違う設定で Play している）、宣言の Scene を開いたまま Play した。

### 2.4 A1 の設計

- **D1（宣言型）:** `BuildPurposeDeclaration`（`OneStarMaker.Build.Selection`、pure）。`Name`（contentSet と同じ規則の ASCII）、`Roots`（logical key の列）、`Filter`（`BuildRequest`。OnDemand の子に適用する tag の述語）、`Representation`（優先順の列と `First` / `All`）、`RuntimeDefaults`（Game が決める不透明な key=value 列。例 `world:cellCompanionSet=Planner`。`content:` で始まる key は拒む。表現は `Representation` 欄から、mode は読む側が決めるので、同じ値を 2 箇所に持たない）。Framework は値の意味を知らない。設定（target、compression、options、defines、Unity version）は本スライスでは adapter の定数のままで、宣言の欄にしない（設定 snapshot は identity の後続）。
- **D2（到達規則）:** `BuildPurposeResolver`（pure）。入力は graph node（identity、parent、children、LoadType、宣言済み payload の表現列（`SceneResource` の payload から。候補からは導かない）、付随 asset の名前列）、node ごとの tag、候補と candidate tag provider、宣言。出力は requirement（(logical key, 表現) ごとの `ExactlyOne`。`First` は優先順で node の宣言済み payload に最初にある表現 1 つ、`All` は優先順にあって node の宣言済み payload にある各表現。期待集合は宣言から、存在と一意性の検査は materialize 済み候補で行うので、候補が欠けても「存在しない表現」として合格しない。`OneOrMore` は使わない）、Representation の request、到達しない候補と優先順に無い表現の候補の除外、requirement ごとの到達経路（root からの node 列。`preflight.json` に「なぜ入っているか」として残る。除外は既存の理由 code で「なぜ入っていないか」が残る）。規則: `required(n) ⇔ n ∈ roots ∨ (required(parent) ∧ (LoadType ∈ {NecessaryAlways, IncrementalAlways} ∨ filter(tags(n))))`。filter の意味は既存 `BuildTagSelector` の request の意味と同じで、述語の実装（`FindRejection` 相当）を selector と共有し、2 つ目の解釈を作らない。payload の無い中間 node（`InGameSession`、`Season_*`）も到達の経路になり、付随 asset を持てる。graph の検証は既存 `SeasonSceneSelectionPolicy.ValidateGraph` の 4 つの規則（identity の重複、root の存在、orphan、親子リンクの相互一致 = 多親の拒否）に加えて、**季節に依存しない明示的なサイクル検査**（全 node から祖先を走査し `visited` を見る。現行はこの検査が `FindSeason` の中にだけあり、季節を外して移すと消える）を Framework の resolver が持つ。到達しないサイクルの島も issue で止め、`Unreachable` で通さない。Editor 側の `SceneGraphValidator` の修正は後続。resolver の入力 `BuildGraphNode` は SceneGraph に固有ではない。`SceneResourceMap` からそれを作るのは materializer 側の adapter で、選択 core が最終的に扱うのは candidate・requirement・policy だけ。非 Scene の root は本スライスでは扱わないが、core の形はそれを排除しない。
- **D3（選択 core の拡張）:** `BuildSelectionPolicy` に `ExcludeUnrequired`（requirement の無い logical key の候補を `BuildExclusionReasonCode.Unreachable` で除外）を足し、`BuildContentRequirement` に任意の `Representation` を足して cardinality を (logical key, 表現) で数える（表現の無い requirement は従来どおり logical key だけで数える）。`SeasonalMode` tag と `VerifyDual` は、表現ごとの `ExactlyOne` が同じ検査を selector の中で行うので不要になり、SampleGame の policy から消す。tag を node 水準（Season、Role）と candidate 水準（Representation）に分け、node 水準は Game が供給する。
- **D4（入力 identity）:** 後続。BS5a は revision（`UtcNow + Guid`）も `preflight.json` の形も変えない。
- **D5（付随 asset と共有 asset）:** Runtime に `AttachedAssetDescription : AssetDescription`（`Name` と `AssetPayload` の列。`[Serializable]`、埋め込み）を足し、`SceneResource` に `_attachedAssets` と `AttachedAssets` / `FindAttachedAsset(name)` を足す。payload の無い node（`InGameSession`）も宣言できる。materializer は付随 asset ごとに候補（logical key `<identity>:<name>`、stable key は既存の `osm-build-candidate-v1` 規則、Representation は Variant から既存規則で写す、所有 identity は候補の metadata（`BuildContentCandidate.Owner`。selector の tag にはしない。tag にすると未要求 dimension として落ちる）、provenance `SceneResourceMap.AttachedAsset`）と閉包を作る。同じ physical GUID は 1 つの node だけが宣言できる（共有は祖先に 1 回宣言し、子孫が所有者を指定して読む）。`SceneBase` に `LoadAttachedAssetAsync<T>(string name, ct)` と `LoadAttachedAssetAsync<T>(string ownerIdentity, string name, ct)` を足し、`SceneDirector` が `BindAssets` と同じ時点で `useContentDirectory`・`sceneVariant`・自分と祖先の付随 asset の宣言だけを `SceneContentBinding`（internal の値型）として注入する（`SceneResourceMap` 全体は渡さない）。`ownerIdentity` は自分か祖先に限る。読む Scene が到達するなら祖先も到達するので、I1 が構造で保たれる。directory モードは `LoadContentAssetAsync(<owner>:<name>, sceneVariant, Scene(自分))`、それ以外は `LoadAssetAsync(AssetKey.FromDescription(desc, sceneVariant), Scene(自分))`。所有者は常に読む Scene なので、共有 asset は `AssetManagement` の canonical key の共有と所有者ごとの参照で従来どおり数えられる。`ContentDirectoryIndex.TryResolveObject` に Scene と同じ Full fallback を与える。trigger / anchor の `_vfxAddress` は `_vfxAsset`（付随 asset の名前）に替え、`CellCompanionScene` / `SeasonMajorEventScene` は `LoadAttachedAssetAsync(name)`、`SeasonScene` は `LoadAttachedAssetAsync("InGameSession", "Lit")` を呼ぶ。`.vfx` と material の Addressables entry は Addressables モードの `AssetReference` が要するので残す。所有者・回収順序・ロード失敗の扱いは変えない。
- **D6（SampleGame の policy）:** `SeasonSceneSelectionPolicy` を、node 水準の tag を導く `SeasonSceneTagPolicy`（`Season` は祖先から、`Role` は `CellCompanionRoleClassifier` で。classifier は `InternalsVisibleTo("SampleGame.DependOnAll.Editor")` で参照し、規則を複製しない）と、宣言の表 `SampleGameBuildPurposes`（6 件。root は `OutGameScene`・`InGameScene`、filter は `Season` と `Role`、表現の優先順、`RuntimeDefaults` に `world:cellCompanionSet`。表現は `Representation` 欄。Planner と Role の対応表を持つ（AC20））に置き換える。`SampleGameContentBuild` は宣言を引数に取り、menu は宣言ごとの `Build`（既存 4 件 + `spring-planner-whitebox`）。bootstrap composer と BS4 の fixture composer は resolver の後に従来どおり合成する。
- **D7（Play As）:** BS5b。
- **D8（検証 Play の 1 操作、From published directory、拒否）:** BS5b。BS5a の判定は既存の Delivery 窓の手入力で行い、宣言の Scene と UICommon を閉じることを操作手順に書く（AC13）。
- **D9（authoring）:** 3 つの `SceneResource`（`Spring_Events_4_2`・`Spring_Event_MultiCellProof`・`InGameSession`）への付随 asset の宣言と trigger / anchor の名前は、Phase B が Editor の Inspector または一時スクリプトで行い、一時スクリプトは commit の前に消す。World Workspace の transaction は変えない。
- **D10（directory mode の fail-closed。config の後に封じる）:** 初期化は mode を決める前に `AssetManagement` を作り、Editor では config をその Addressables 経由で読む（§1.3）ので、backend 無しで作ることはできない。代わりに `AssetManagement` に internal の `SealAddressables()` を足し、`AbstractApplicationInitializer` は `ReadContentDirectoryConfiguration` が directory を返した直後に呼ぶ。封じ方は入口を数えて拒否するのではなく、**`_backend` を `SealedAssetBackend`（既存の internal interface `IAssetBackend` の decorator）に差し替える**。`AssetManagement._backend` は現在 `readonly` なので `readonly` を外し、差し替えは `SealAddressables()` だけが 1 回行う（2 回目は no-op）。decorator は取得系（`LoadAssetSync` / `LoadAssetAsync` / `LoadSceneAsync` / `InstantiateAsync`）を `InvalidOperationException`（「directory mode では Addressables を使わない」）で拒否し、`Release` / `UnloadSceneAsync` は内側へ通す。`AssetResidentCache` が構築時に受け取った内側 backend の `Release` delegate と、封じる前に始まった in-flight のロードは内側への参照を保持したまま完了・解放でき、既に取得した handle（config の `TextAsset`）は有効なまま所有者の解放で返せる。封じた後の **backend 呼び出し**は、どの公開 member から到達しても（`InstantiateAsync(AssetKey)` を含む）拒否される。backend を経由しない経路（registry のロード済み、resident cache、in-flight の共有）は差し替えでは閉じないので、`SealAddressables()` が次を行う: in-flight が 1 件でもあれば例外（封じる時点を初期化の同期区間に限る）、resident cache の Addressables 由来の entry を落とす、封じる時点で registry にある address key の集合を記録し、それ以外の address key は registry 経由でも返さない（現状の集合は config の `TextAsset` 1 件。AC16 の負例）。安全性が呼ぶ時点の状態に依存することを、この assert と記録で明示する。directory の経路と `CreateVfxBackend` 等の他の初期化は変えない。起動ログに封じた 1 行を出す（AC13 の読み方）。Addressables mode では封じない。BS4 Player は config を required JSON から読むので、封じる前に Addressables を使わない。Addressables mode と directory の経路の回帰は既存テスト（`ContentDirectorySessionTests`、起動テスト）で課す。`UICommon` の bootstrap や App 常駐の初期化が封じた後に Addressables を要する経路が残っていれば B は止めて A へ返す（§4.2）。
- **D11（宣言の運搬）:** `BuildContentRoot` に `Declaration`（name、representation の主値、runtime の key=value 列）を任意の serialized field として足し、adapter が宣言から写す。`ContentTransportManifest` に同じ `declaration` を任意 field として足す。schema version（root 2、manifest 1）は変えず、宣言の無い root / manifest は従来どおり既定値なしとして扱う（既存の installed revision の再検証を壊さない）。Runtime の読み手: `AbstractApplicationInitializer` は directory mode の起動で root の `Declaration` の runtime key=value と config を突き合わせ、違う key があれば key と両方の値を Warning 1 行で出す（AC19。不透明な文字列の比較だけで、値の意味は解釈しない。目的違いの directory を Play した時に Warning 54 回ではなく 1 行で分かる）。BS4 の `RuntimeJson` は `bs4-spring-full` 宣言の `RuntimeDefaults` と `Representation` から `cellCompanionSet` と representation を写す（手書きの定数をやめる）。Framework は `content:*` 以外の key を解釈しない。Editor 側で manifest の宣言を環境変数へ写す口は BS5b。
- **D12（batch 入口）:** BS5b。
- **D13（既存 build 入口）:** 既存 4 つの menu は宣言ごとの `Build` に置き換え、名前（contentSet）は変えない。`bs4-spring-full` は BS4 coordinator が宣言を引数に呼ぶ。
- **D14（build mode と publish の `Full` 必須）:** BS5b。BS5a の coordinator は workspace の扱いを変えない。

### 2.5 ここでは答えない問いと所有する後続

| 問い | 事実 | 所有 |
|---|---|---|
| 同じ宣言を日常 Play・検証 Play・Player・batch に接続する: Play As（mode の明示・表現 profile・companion set の環境変数を宣言から置く）、宣言 1 つからの build → publish → install → 検証 Play、From published directory、Play As の ledger と開いている Scene の拒否、batch 入口と終了コード、publish の `Full` 必須と build mode、transport staging の path 予算、Delivery 窓の宣言対応、manifest の宣言を環境変数へ写す bridge | BS5a の宣言型・`BuildContentRoot.Declaration`・manifest の `declaration` を参照する。A0 の事実（mode の契約、profile の優先、ledger の `SessionState`、開いている Scene の再利用、workspace の再利用）は本文書 §1.3 に固定済みで、やり直さない | **BS5b**（BS5a の Phase D の後に別 HANDOFF で Phase A） |
| 入力 identity（宣言と選択候補の source 状態と設定 snapshot の hash）、設定 snapshot（project settings の file hash を含む）、`content/index`、`priorRevisions`、build を skip する再利用とその前提の成果物の健全性の確認 | BS5a の I3 は選択集合と閉包の一致で判定する。「今誰が何を判断するのに必要か」に答えが無い。revision の形式は変えない。安い布石として、判定 C の照合 script が計算する「sorted stable key の SHA-256」を後続が `preflight.json` の欄にすれば、入力 identity はそれと設定 snapshot の合成になる（BS5a ではコードにしない） | BuildSystem 後続 |
| 経路の連鎖: key ごとに source → local の object store → LAN / R2 の順で探し、Editor が自分の持たない asset を built content から読む（UC3） | Unity は content directory を複数登録できるが directory 間は孤立し、同じ asset が source と directory の両方にあると別 object として二重に載る。必要集合は本スライスの宣言と選択から出る | BuildSystem 後続 |
| object 単位の格納と差分更新（UC6・UC9）: 同じ bytes を revision・目的をまたいで一度だけ転送・格納し、差分だけを更新する | Unity の manifest が file ごとの `ContentHash` と依存 graph を持つ（§1.3）。transport の manifest は file ごとの SHA-256 を持つ。DIST は revision 丸ごとの複写、Storage は revision/file の key | BuildSystem 後続（Storage program の保留項目と接続） |
| 同じ宣言の再 build が同じ file 名と bytes を出すか | 決定性は格納の最小性にだけ効く | BuildSystem 後続（spike） |
| Player の実行時差し替え（UC10） | 登録と解除は runtime API で、session が rollback 込みで包む。旧 directory から読んだ Scene が解除後も生きるかは未測定 | BuildSystem 後続 |
| 複数 directory の同時登録、共通 base と目的差分への層化、cross-directory 参照 | 上の孤立の制約 | BuildSystem 後続（spike） |
| 設定（target、compression、defines、project settings）を宣言で変える。Runtime の `SupportedUnityVersion` 定数との関係（Unity を上げる手順）。Player の Debug の剥離 | Runtime 側の定数は別に直す（§1.3）。Player には DebugSocket が既定で有効なまま入り（`app-config.json` の `debugSocket:enabled: true`）、defines による剥離は未実装（`[Conditional]` / `DEVELOPMENT_BUILD` は 0 件、`OneStarMaker.Debug.asmdef` の defineConstraints は空） | BuildSystem 後続 |
| 検証 Play や Player の起動成功後に installed revision を known-good に pin する呼び手（`MarkKnownGood` は未使用） | 起動は known-good を自動適用しない契約。pin は eviction からの保護 | DIST / BuildSystem 後続 |
| 日常 Play（source）で宣言に無い Scene / asset を読んだ時の検出（UC11） | 宣言の必要集合を build 無しに Runtime へ渡す口が要る | BuildSystem 後続 |
| InGame の Spring に入る Player の build 経路、Player での VFX 描画 | — | BuildSystem 後続 / S-9 |
| `outcome.json` の選択・閉包の記録形式（§24 §16.7） | — | BuildSystem 後続 |
| Planner profile の Whitebox 上の Event の動作、S-4 program の harvest と削除 | 今日と同じ手順（環境変数と profile を手で置く source の Play）で実証できる | S-4d5b |
| Addressables の退役（`.vfx`・material の entry を含む）、旧 Variant 経路の整理 | — | ADDR-RETIRE / ADDR-HYGIENE（§20 §9） |
| 付随 asset の World Workspace transaction への組み込み、Variant 別の付随 asset、非 Scene の root | — | 需要が出たスライス |
| directory に無い OnDemand content を要求した時の Runtime の振る舞い（Cell の欠落、companion の Warning の集約） | 本スライスは宣言と config の不一致を Warning 1 行で見せるだけ | 需要が出たスライス |
| §24 §16.7 の Framework の欠落（親の unload 開始後の子の Add、`LoadAssetAsync` の await 後の所有者確認、Addressables mode の dedup が先発の ct を共有するため共有 asset `Lit` を同時にロード中の Season を片方 unload すると後発も `OperationCanceledException` になる。directory mode は gate で直列化されるので起きない） | — | 所有スライス未定のまま |

- **判定定義:** 不変条件 I1〜I9 と成立条件 E1〜E5 を AC1〜AC21 で確かめ、全て成立で GO。1 つでも不成立なら NO-GO。
- **停止規則:** 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。スパイクではないので CONDITIONAL ACCEPT は使わない。
- **A3 後の例外承認:** なし。
- **決定記録:** 2026-10-31（Phase A）: (1) revision の形式を変えず、宣言から導く identity は欄として運ぶ案を経て、identity 自体を後続へ送った（D4）。理由は tools/Artifacts の形式固定（§1.3）と、skip 再利用の安全が別の問いであること。(2) BS5 を BS5a（中核）・BS5b（運用経路）・identity 後続に分割（発注者）。理由は、A2 と外部レビューの blocker が全て中核に集中し、d5b が BS5b を待たずに進められること。
- **本文へ転記した実装制約:** §1.1・§1.2。Unity 側 `.cs` は `#nullable enable`、`record` と `init` accessor（`IsExternalInit` は `OneStarMaker.Runtime` の internal）の禁止、破棄されうる `UnityEngine.Object` の `== null` 判定、公開 API のログ抽象は `ILogger<T>`。
- **未決事項:** §2.6。

### 2.6 未決事項

- **U1（規模）:** 見積もりは製品コード 800〜1,000 行、テスト 900〜1,200 行で、500 行の警報を超える。分割は決定済み（決定記録 (2)）。**差し戻しの引き金:** Phase B で Runtime 側（D5・D10・D11 の Runtime 部分）が、次に列挙した面の外に新しい所有者・寿命・状態・依存方向の概念を要する場合は B を止め、A へ返す。行数（Runtime 側の見積もりは約 280 行）は警報で、引き金ではない。列挙した面: `AttachedAssetDescription`、`SceneResource.AttachedAssets` / `FindAttachedAsset`、`SceneBase.LoadAttachedAssetAsync`（2 overload で 1 点）、`AssetManagement.SealAddressables`（internal）、`BuildContentRoot.Declaration`（serialized）、`ContentTransportManifest` の `declaration`（serialized）。
- **U2（tag の水準）:** 推奨は D3 の二水準（node と candidate）。代案は候補の tag だけで到達を判定する（Representation の違う候補で node の tag が揺れるので、payload ごとに同じ tag を重複して付けることになる）。
- **U3（Object の Full fallback）:** 推奨は Scene と同じ fallback（D5）。代案は宣言側で付随 asset に表現ごとの payload を要求する（SampleGame の 3 件は Full のみで、Whitebox Play で必ず欠ける）。
- **U4（fail-closed の置き場）:** 推奨は D10（config の後に backend を拒否 decorator に差し替える。入口を数えず、backend の interface で網羅する）。代案は config の読み方を Editor では file I/O に変える（config の責務を本スライスで変えることになる）。backend 無しの構成は Editor が config を Addressables で読むため不可能（A2）。
- **U5（宣言の運搬の経路）:** 推奨は D11（`BuildContentRoot` と manifest の両方に載せ、Runtime は root を読んで不一致を検出し、Player は baked config を読む）。代案は manifest だけ（Runtime の読み手が無くなる）。

---

## 3. 責務マップ（A1）

| ファイル | 責務・変更理由 | 所有者・寿命 | 依存・公開面 | テスト境界 | 行数（現在 → 予想） |
|---|---|---|---|---|---|
| 新規 `Editor/Build/Selection/Model/BuildPurposeDeclaration.cs` | 目的・Variant・runtime 既定値の宣言（immutable、`content:` key の拒否） | 呼び出しごと | pure。public | 単体（AC7） | 0 → 90〜110 |
| 新規 `Editor/Build/Selection/Model/BuildGraphNode.cs` | 到達規則の入力 node（宣言済み payload の表現列を含む） | 呼び出しごと | pure。public。`LoadType` は Runtime の enum を参照しない（`BuildGraphLoad` の 3 値に写す） | 単体 | 0 → 60 |
| 新規 `Editor/Build/Selection/BuildPurposeResolver.cs` | graph の検証、到達規則、requirement と request と除外と到達経路の導出 | 呼び出しごと | pure。public。`BuildTagSelector` の述語を呼ぶ | 単体（AC1〜AC3・AC6） | 0 → 200〜250 |
| `Editor/Build/Selection/BuildTagSelector.cs` | `ExcludeUnrequired` と (logical key, 表現) の cardinality、述語の共有 | — | pure | `BuildTagSelectorTests` 拡張 | 302 → 350〜370 |
| `Selection/Policy/BuildSelectionPolicy.cs`・`BuildContentRequirement.cs`・`Results/BuildExclusionReasonCode.cs` | policy の flag、requirement の `Representation`、`Unreachable`・`RepresentationNotPreferred` の理由 | — | pure | 単体 | 各 +10〜30 |
| `Editor/Build/Materialization/SceneResourceContentMaterializer.cs` | graph snapshot（宣言済み payload の表現列を含む）の出力、付随 asset の候補・閉包・所有者 metadata、physical GUID の重複宣言の検出 | — | `SceneResourceMap`・`AttachedAssetDescription` | `SceneResourceContentMaterializerTests` 拡張（AC8） | 147 → 230〜260 |
| `Materialization/BuildMaterializationSnapshot.cs`・`Selection/Model/BuildContentCandidate.cs` | `Graph` の保持、候補の `Owner` metadata | — | Editor / pure | 偽 gateway | 66 → 80、+10 |
| `Editor/Build/Content/BuildContentReport.cs` | preflight への到達経路 | — | Editor I/O | 既存 | 97 → 115 |
| `Editor/Build/Content/UnityContentDirectoryAdapter.cs` | `BuildContentRoot.Declaration` の書き込み | — | Editor I/O | — | 166 → 175 |
| `Editor/Build/Content/ContentTransportPublisher.cs` | manifest への `declaration` | — | Editor I/O | manifest 往復の単体（AC18） | 108 → 115 |
| `Runtime/BuildContent/BuildContentRoot.cs`・`Distribution/ContentTransportManifest.cs`・`ContentManifestValidation.cs` | 宣言の運搬（任意 field。schema version は据え置き） | — | public（serialized） | `ContentManifestValidationTests` 拡張（AC18） | 88 → 108、133 → 148、293 → 298 |
| 新規 `Runtime/AssetDescriptions/AttachedAssetDescription.cs` | node が所有して子孫が読む付随 asset の宣言 | SceneResource に埋め込み | `AssetDescription`。public | — | 0 → 50 |
| `Runtime/SceneSystem/SceneResource.cs` | 付随 asset の列と検索 | — | public | EditMode（AC12） | 98 → 115 |
| `Runtime/SceneSystem/SceneBase.cs`・`SceneDirector.cs`・`SceneDirector.Loading.cs` | `LoadAttachedAssetAsync`（2 overload）、`SceneContentBinding` の注入 | Scene(identity) | `IAssetManagement`。protected 2 件、internal 1 件 | 単体（AC10・AC17） | 274 → 325、280 → 292、683 → 690 |
| `Runtime/AssetManagement/AssetManagement.cs`（+ `.ContentDirectory.cs`） | `SealAddressables()`（`_backend` の `readonly` を外して 1 回だけ差し替え） | App | internal 1 件 | 単体（AC16） | +15 |
| 新規 `Runtime/AssetManagement/Internal/SealedAssetBackend.cs` | 取得系を拒否し解放と unload を通す `IAssetBackend` の decorator | App | internal | 単体（AC16、負例 + reflection の列挙） | 0 → 40〜50 |
| `Runtime/Bootstrap/AbstractApplicationInitializer.cs` | directory と決まった直後の封印と起動ログ 1 行、root の宣言と config の不一致の Warning | App | 既存 | 単体（AC19 の比較を関数に切り出す）+ EditMode（既存の起動テスト） | +35 |
| `Runtime/BuildContent/ContentDirectoryIndex.cs` | Object の Full fallback | — | internal | `ContentDirectoryIndexTests` | 129 → 131 |
| 新規 `SampleGame/DependOnAll/Editor/Build/SeasonSceneTagPolicy.cs` | node 水準の `Season`・`Role` tag | — | `CellCompanionRoleClassifier`（InternalsVisibleTo） | 単体 | 0 → 100〜130 |
| 新規 `SampleGame/DependOnAll/Editor/Build/SampleGameBuildPurposes.cs` | 6 件の宣言と Planner / Role の対応表 | — | pure | EditMode（AC4・AC5・AC20） | 0 → 100〜120 |
| `SampleGame/DependOnAll/Editor/Build/SampleGameContentBuild.cs` | 宣言ごとの `Build` menu | — | Editor I/O | 操作（AC13 の build） | 131 → 150〜170 |
| 削除 `SeasonSceneSelectionPolicy.cs`・`SeasonSceneSelectionPolicyTests.cs` | 置き換え | — | — | — | 243 → 0、181 → 0 |
| `SampleGame/DependOnAll/Editor/Build/SampleGamePlayerBuildCoordinator.cs` | `bs4-spring-full` 宣言の利用、RuntimeJson を宣言から写す | — | 既存 | `Bs4FixtureComposerTests`、AC14 | 145 → 155 |
| `SampleGame/InGame/.../World/LocalVfxEventTrigger.cs`・`MajorEventVfxAnchor.cs`・`LocalVfxEventBinder.cs`・`CellCompanionScene.cs`・`SeasonMajorEventScene.cs`・`SeasonScene.cs` | address → 付随 asset 名、`LoadAttachedAssetAsync` の利用 | 既存 | 既存 | AC11・AC12・AC17 | 各 ±5〜10 |
| `SampleGame/InGame/Properties/AssemblyInfo.cs` | `InternalsVisibleTo` 1 行 | — | — | — | +1 |
| asset（3 つの SceneResource、Scene 内の trigger / anchor の値） | 付随 asset の宣言 | — | — | AC12 | — |
| テスト（新規 `BuildPurposeResolverTests`、`BuildPurposeDeclarationTests`、`SeasonSceneTagPolicyTests`、`SampleGameBuildPurposesTests`、`SceneBaseAttachedAssetTests`、`SealedAssetBackendTests`、`DeclarationMismatchTests`、既存の拡張） | — | — | — | — | 0 → 900〜1,200 |

- **警報と判断:** 合計は 500 行を超える。分割は決定済みで、差し戻しの引き金は §2.6 U1。`SceneDirector.Loading.cs`（683 行）は注入 1 行の追加に留める。`BuildTagSelector` は既存の責務（照合）の拡張で、優先の規則は requirement の属性として置き、policy 判断を selector に足さない。resolver・materializer・selector は分けない。
- **配置:** 到達規則と宣言は `OneStarMaker.Build.Selection`（pure、Runtime に依存しない）。付随 asset の宣言と宣言の運搬は Runtime（§18 §3 の埋め込み型の方針、`BuildContentRoot` は既に serialized の運搬役）。SampleGame の語彙（Season、Role、宣言の表）は `SampleGame.DependOnAll` / `.Editor`。新しい asmdef・依存 edge は無い。

## 4. 実装計画（A1）

### 4.1 順序

1. Selection: 宣言型、graph node、resolver（表現ごとの `ExactlyOne`、到達経路）、selector の拡張、単体テスト（AC1〜AC3・AC6・AC7）。
2. Materialization: graph snapshot、付随 asset の候補と所有者 metadata、report の到達経路、テスト（AC8）。
3. SampleGame の policy と宣言の表、menu の `Build`、BS4 の接続、テスト（AC4・AC5・AC20）。この時点で既存 4 build の選択が一致することを EditMode で確かめる。
4. Runtime の付随 asset と fail-closed: `AttachedAssetDescription`、`SceneResource`、`SceneBase` / `SceneDirector`、index の fallback、`SealedAssetBackend` と `SealAddressables`、`AbstractApplicationInitializer` の封印と不一致の Warning、テスト（AC9・AC10・AC16・AC17・AC19・AC21）。ここで U1 の引き金を確かめる。
5. 宣言の運搬: `BuildContentRoot`・manifest・validation・adapter・publisher、BS4 の RuntimeJson、テスト（AC18）。
6. SampleGame の Scene class と trigger / anchor、asset の宣言（Inspector または一時スクリプト）、Play テストの更新（AC11・AC12）。
7. `pwsh tools/contract-audit.ps1`、Editor の compile 確認。Phase B の終わりに Addressables mode の Editor Play（source。Editor 操作の範囲）を 1 回軽く Play して AC11 の前提を確かめる。Phase B は content build・検証 Play を実行しない（Phase C）。

### 4.2 Phase B から Phase A へ差し戻す条件

- U1 に列挙した Framework の公開面の外に、新しい依存方向・所有者・寿命・状態の概念、または asmdef 参照が要る（数ではなく概念の増加で判定する）。
- 到達規則を pure に保てない（`SceneResourceMap` や AssetDatabase を resolver から読む必要が出る）。
- 既存 4 build の選択が `840b6609` の policy と一致しない原因が、宣言で表せない規則にある。
- Object の Full fallback が既存 `ContentDirectorySessionTests` の契約を破る。
- 封じた後に `UICommon` の bootstrap や他の App 常駐の初期化が Addressables を要して起動できない、または config 以外に封じる前の Addressables 利用が見つかる（D10 の前提が崩れる）。
- 任意 field として足した `declaration` を DIST の validation か `JsonUtility` が拒否し、version を上げる必要が出る。

### 4.3 対象外を維持する方法

- transport・DIST・Storage の key と revision の形式を変えない（I9）。manifest の追加 field は読む側が無視できる形にする。coordinator の workspace・staging・identity の扱いを変えない。
- Player の bootstrap・smoke・`Application.Quit` に触れない。RuntimeJson の値の出どころだけを宣言に替える。
- `AssetManagement` の Addressables 側の経路と `AssetKey` を変えない。config の後に backend を拒否 decorator へ差し替える口を足すだけ。
- Delivery 窓と `ContentDeliveryPlayBridge` に触れない（BS5b）。

## 5. テストとレビュー計画（A1）

- **単体テスト:** `BuildPurposeResolverTests`（AC1〜AC3・AC6）、`BuildPurposeDeclarationTests`（AC7）、`BuildTagSelectorTests` の拡張、`SeasonSceneTagPolicyTests`、`SceneBaseAttachedAssetTests`（偽 `IAssetManagement`、AC10・AC17）、`SealedAssetBackendTests`（AC16）、`DeclarationMismatchTests`（AC19）、manifest の往復（AC18）。
- **差し戻し中の起点 `-Filter`:** `BuildPurposeResolverTests`、`BuildPurposeDeclarationTests`、`BuildTagSelectorTests`、`SceneResourceContentMaterializerTests`、`SampleGameBuildPurposesTests`、`SeasonSceneTagPolicyTests`、`ContentDirectoryIndexTests`、`ContentDirectorySessionTests`、`BuildContentDirectoryIntegrationTests`、`SceneBaseAttachedAssetTests`、`SealedAssetBackendTests`、`DeclarationMismatchTests`、`ContentManifestValidationTests`、`PlayerContentConfigurationTests`、`LocalVfxEventPlayTests`、`SeasonMajorEventPlayTests`、`Bs4FixtureComposerTests`。
- **判定必須テスト:** 最終の全 EditMode 回帰（空 filter）、PlayMode の `LocalVfxEventPlayTests`・`SeasonMajorEventPlayTests`・`VfxScenePlayTests`・Streaming の PlayMode（`-WithGraphics` を併用）。Content Directory build 2 件（menu の `Build spring-full`、`Build spring-planner-whitebox`。Phase C の Editor 操作）を行い、`preflight.json` を照合 script にかける（AC4・AC5）。判定は §2.0 の主張ごとに書く。
- **全 EditMode 回帰の適用除外:** なし。
- **統合・Unity テスト:** `BuildContentDirectoryIntegrationTests` の拡張（AC9・AC21）、`Bs4FixtureComposerTests`、`ContentManifestValidationTests`・`ContentDeliveryFailureMatrixTests` の既存経路。
- **操作・実行時・目視条件の検証経路:**
  - AC13（担当: Phase C、高コスト証拠の回（AC14 の BS4 build が publish した directory を使う）。環境: 宣言の Scene と UICommon を閉じ、`bs4-spring-full` の published directory を Delivery 窓に手入力（Published directory・manifest SHA-256・contentSet・revision・cache root・representation・budget）で Prepare → Use For Next Play、Play。Delivery 窓は `transport.json` のある published directory か HTTP だけを受け付け、menu の `Build` の出力は受け付けない。観測: 起動ログの封印の 1 行、不一致 Warning の不在、(0,4) Stable、WASD で (4,2) へ移動し Events の trigger 位置へ入る、Console の Error の有無、VFX の表示。証拠: Console ログの保存）。
- **未知の操作経路:** (1) `UICommon` の bootstrap や App 常駐の初期化が、封じた後に Addressables を要さず起動できるか。未確認。初回確認は Phase B の順序 4（担当: B）。不成立なら §4.2 で A へ返す。(2) `JsonUtility` と DIST の validation が任意 field を無視して受理するか。初回確認は B の順序 5 の単体テスト。(3) なし（Delivery 窓は published directory か HTTP だけを受け付けることをコードで確認済み。AC13 は BS4 の published directory を使う）。
- **人間の判断が必要な条件:** なし（AC13 の VFX の表示は Console のログ `LocalVfxEventSession` の起動記録で代替する）。
- **高コスト証拠:** BS4 Player build 1 回（AC14。その published directory で AC13 も行う）。手順: ルート 96 文字以下の checkout で、**build の前に `artifacts/bs2b/work/<target>/bs4-spring-full` を削除し、削除したこと（存在しない状態）を証拠に残す**（workspace の再利用で以前の build の file が published directory に混ざるのを防ぐ。`Full` 必須は BS5b）。その後 `Tools/OSM/Content/Build BS4 Spring Player` を実行し、Player を起動して smoke receipt を取る。独立した期待値: receipt が `completed`、Player 内 `content/` の `BuildContentRoot` の entry に `Spring_Events_4_2:Burst`・`Spring_Event_MultiCellProof:Burst`・`InGameSession:Lit` の `Object` entry がある（manifest の `Loadables` が 5）、`bs4-runtime.json` の `cellCompanionSet` と `representation` が宣言の値。判定 C → 暫定 C' → 高コスト証拠 → 最終証拠ラウンドの順（review-evidence の規則）。
- **判定 C の照合 script の方式:** A3 凍結 script（offline の pwsh）。入力は GO 候補 head で menu の `Build` が出した `preflight.json` 2 件と、A3 で凍結する期待値（宣言ごとの件数と sorted stable key 列の SHA-256）。期待値の出所は 3 つに分けて別々に保存し script が合成する: (i) `840b6609` の Editor で旧 menu 4 件と BS4 を実行して得た `preflight.json` の selected（実測）、(ii) 付随 asset 3 件の stable key（規則と GUID から導いた値。実装の出力を期待値にしない）、(iii) `spring-planner-whitebox` の 70 件（仕様から導いた期待。到達規則を手で適用した logical key の一覧を A3 の前に書き、実装後の出力で上書きしない）。script は selected の stable key を sorted して件数と SHA-256 を比べ、`_Environment_` 等の禁止 pattern の不在と、requirement ごとの到達経路が root から始まることを確かめる。(i) の採取は A3 の主担当が Editor 操作だけで行う（Unity テストは伴わない）。本 branch は docs だけを変えており `unity/` は `840b6609` と同一なので、採取はこの worktree の Editor で行える（採取の前に `git diff --stat 840b6609 -- unity` が空であることを記録する）。
- **判定 C と C' に足す観点:** HANDOFF の「全て」「どの…からも」「必ず」の主張ごとに、反例になる経路をコードで 1 本ずつ探す（Phase A のレビュー 5 回は設計の文言に集中し、既存コードの早期 return や検査の置き場所（R1・R2）を拾えなかった）。対象の主張は I1・I2・I7・D2 のサイクル検査・D10 の封鎖・AC16 の負例。
- **機械検査:** `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。
- **A0/A1 主担当・モデル・ベンダー:** この session の主担当（Anthropic 系）。
- **レビューの履歴（分割前の BS5 に対するもの。当時の文言（「backend 無し」「skip 再利用」等）を含む。現行の設計は §2 が正）:**
  - 問いのレビュー（A2 の前、2026-10-31。第 3 版 `915314ef`）: (1) A0 だけを読んで問いと最小構成を出す担当（Anthropic 系、主担当とは別のモデル）、(2) 問いの批評と §1.3 の事実確認（OpenAI 系）。採用: 目的の絞り込み、Player の範囲の限定、別 PC / Cloud の 2 分、UC1 の現況の訂正（未指定の mode は失敗する契約、profile が config より優先）、workspace の再利用による混入、数え違い 3 件、UC11。
  - A2 独立レビュー（2026-10-31。第 4 版 `dbb87de3`、内容 sha256 `c89f4010fc358e97bf7e9a2e7fc103f6094f17daed76ab6b0a9c499a560e5de1`。同じ入力版、互いの指摘は見せない）: (a) アーキテクチャゲート（Anthropic 系、主担当とは別のモデル。条件付きで通る、blocker 3 件: backend 無しの構成は初期化順と両立しない、`BuildContentRoot.Declaration` の読み手、`RuntimeDefaults` と `Representation` の重複）、(b) 不変条件の反例と再利用の安全（xAI の Grok 系。I1: 所有者 tag が selector で落ちる。I7: 開いている Scene の再利用、config の Addressables 読み。再利用: settings の変更）、(c) 実ユースケースの通し（OpenAI 系。batch の既定の矛盾、終了コード、Reset 無しの Verify、AC16 の観測、From published directory の出所）。全て採用し D5・D8・D10・D11・D12・D14 に反映（運用経路の分は BS5b が引き継ぐ）。
  - 外部レビュー第 1 弾（発注者経由、2026-10-31。A3 第 1 版 `572e118b`。OpenAI 系）: 問いの再定義、`InstantiateAsync(AssetKey)` の封じ漏れ、AC15 の言い過ぎと負例、skip 再利用の安全、`All` の意味、Incremental publish、宣言と runtime の対応、Scene 中心の core、分割の推奨。全て採用（分割は第 2 弾の後に発注者が決定）。
  - 外部レビュー第 2 弾（発注者経由、2026-10-31。A3 第 2 版 `1612b4d2`。OpenAI 系）: B1 backend の型（`IAssetBackend`、`readonly`）、B2 Phase B の権限、B3 出力の最小性の 3 分、B4 期待集合の出所、B5 baseline の 3 分、B6 数から概念へ、B7 履歴の分離、分割の推奨。全て採用。
  - ソース突き合わせ評価からの指摘（2026-10-31。分割後の第 1 版 `782f8d9f`。Anthropic 系、主担当と同じモデル系列で別 session）: R1（封鎖は backend の差し替えだけでは registry・cache・in-flight を閉じない → D10 の文言を「backend 呼び出し」に限定し、in-flight の assert・cache の drop・registry の key 記録を足し、AC16 に負例）、R2（サイクル検査が `FindSeason` にだけある → D2 に明示的な検査、AC2 に負例、I1 に明記）、R3（高コスト証拠の前に workspace 削除）、R4（dedup の ct 共有 → §16.7 の行）、R5（sorted stable key の SHA-256 を後続の布石として記録）、R6（Player の Debug 剥離を設定の後続に）、R7（判定 C / C' の観点）。全て採用。R5 はコードにしない。
- **A3 統合担当・モデル・採否:** この session の主担当（Anthropic 系）。本版は分割後の第 2 版（未凍結）。
- **C' 用に予約した担当・モデル・ベンダー:** Phase A に関与した系列は Anthropic 系・OpenAI 系・xAI の Grok 系。C' は Phase A〜C に関与していない系列を Phase C' の開始時に利用可能なものから選び、どれも使えない場合は `独立性制約あり` として理由と残存リスクを記録し、発注者が採否を判断する。
- **独立性の強化条件を満たせない場合の理由:** 未確定。

## 6. Phase B 実装結果

未着手

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手
