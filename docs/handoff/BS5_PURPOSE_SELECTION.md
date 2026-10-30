# BS5 目的からの最小選択

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `A`（A2 入力版 第 3 版。未凍結）
- branch: `claude/bs5-purpose-selection`
- implementation base commit: `840b6609`（`develop`、S-4d5a の PR #149 のマージ）
- implementation head commit: 未固定
- risk: `high`（Framework の選択 core・materializer・build identity と、Runtime の `AssetManagement` の backend 構成、`SceneResource` / `SceneBase` の公開面に触れる。既存の 5 つの content build 入口と BS4 Player coordinator が同じ経路を通る）
- owner: 発注者 / BS5 担当
- created: 2026-10-31
- expires: BS5 の Phase D 完了時
- harvest to: Architecture §18（§4 の選択 core・materialization・Content Directory build の現況、§5「Scene 以外のアセット種別」）、§20 §3（Editor Play の目的の選択、検証 Play の手順）、§13（Object の表現 fallback、directory mode の fail-closed）、§04 §4.7 / §4.8（directory mode の backend 構成と宣言の既定値）、§24 §16.7 / §16.9 / §16.10（`.vfx` の経路と「address でロード」の記述）、S-4 program §6.3 の BS5 行と d5b 行
- workflow task: `bs5-purpose-selection`（未開始。最初に Evidence を固定する時点で `pwsh tools/workflow-task.ps1 start` する）
- Phase A snapshot の取得案内・状態: 未生成（凍結した HANDOFF 本体は A3 凍結 commit で固定する）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票: Phase A 作業中。本版は A2 の入力版（第 3 版。第 2 版 `485a51a4` を、参照者を 4 者に置き直したゼロベースの見直しで書き換えた。主な変更: build 無しの Editor Play に宣言を効かせる D7 の追加、revision の形式を変えず入力 identity を欄として運ぶ D4、検証 Play を日常の Play と分けた §1.4 / §1.5、判定の単純化）。A2 の独立レビューを統合した A3 版を作り、発注者が凍結を指示するまで Phase B に入らない。

---

## 1. 目的と対象外

- **目的:** 「何が必要か」を宣言 1 つで表し、Editor の日常 Play・Editor の検証 Play・Player・別 PC / Cloud の batch が同じ宣言を使う BuildSystem にする。宣言（目的 = Runtime が実際にロードする範囲、設定 = Unity の build 条件、Variant = 表現、runtime の既定値）から、必要十分な Scene と付随 asset の集合が決定的に導かれ、その成果物だけで検証 Play と Player が動き、build 無しの日常 Play でも宣言の runtime 既定値が効き、同じ build を batch から起動できる。S-4d5b（Planner profile の Whitebox 上の Event 実証）の前提を満たす。
- **対象外:**
  - 経路の連鎖（key ごとに source → local の object → LAN / R2 の順で探す）。Editor が自分の持たない asset を built content から読む形は後続（§2.5）。本スライスの Runtime は従来どおり Addressables mode か directory mode のどちらか 1 つで動く。
  - 格納と更新の最小性（同じ bytes を revision や目的をまたいで一度だけ転送・格納し、差分だけを更新する）。後続（§2.5）。本スライスは build mode（§2.4 D14）と manifest を従来どおり残すだけ。
  - Player の実行時の content 差し替え、複数 Content Directory の同時登録、共通 base と目的差分への層化。
  - InGame の Spring に入る Player の build 経路（BS4 の Player は Title の smoke の後に `Application.Quit(0)` する）。BS4 の経路は壊さないが、Player の動作は変えない。
  - 設定（target、compression、options、defines）を宣言で変える口。本スライスは値を固定したまま識別子に含め、filter の tag の供給源になりうることだけを定める（D1）。
  - World Workspace の transaction への付随 asset の authoring の組み込み。
  - Addressables の退役、旧 Variant 経路の整理。Addressables mode の Editor Play の読み口は従来どおり。
  - §24 §16.7 の所有スライス未定の事項のうち本スライスの問いに関係しない事項。
- **現況:** §1.3（A0、`840b6609` で確認）。

### 1.1 Phase A の判断基準（発注者、2026-10-27。S-4 program §0 から転記）

- **主目的は BuildSystem。** VFX（S-4d）はそのつなぎ込みの題材である。各案で、成果物が content build・Player build の経路に乗るかと、Build 側の既知の課題（BS4 の Player は smoke の後に終了して InGame の Spring に入らない、transport staging の path が予算の外）を踏むかを書く。Build 側の課題は「やらない」で片付けず、BuildSystem の作業の候補として扱う。
- **残すコードは SampleGame に必要なものだけ。** 生成や authoring の道具を残すかは「SampleGame にそのコードが必要か」で決める。不要なら一時スクリプトで作り commit の前に消す。
- **checkout のルート。** Player を build するスライスは、ルートが 96 文字以下の checkout で作業する。この worktree のルートは 31 文字。本スライスの判定で BS4 Player build を高コスト証拠として 1 回取得する（§5）。
- **問いの立て方（発注者、2026-10-31）。** BuildSystem の問いは「目的別・設定別・Variant 別が絡み合い、なおかつ必要最小限になる Build 成果物ができ、必要最小限に格納される」こと、そして SampleGame に引っ張られない汎用的な System になること。問いは「必要なものが分かる」と「必要なものを提供できる経路を作る」に割れ、本スライスは前者を答える。
- **参照者は 4 者（発注者、2026-10-31）。** Editor（日常）・Editor（成果物の検証）・Player・別 PC / Cloud が、それぞれ「必要なものをどう知るか」「何を持っているか」「足りないものをどこから得るか」を持つ（§1.4）。Editor に source があって編集しているなら source を読み、日常の Play に build は要らない。Runtime の key 解決や判定の読み口は参加者の内側の機構で、参照者ではない。判定は難しくしない。
- **実ユースケースで通す（発注者、2026-10-31）。** 選択の規則だけでなく、§1.5 のユースケースが同じスライスで成り立つことを確かめる。証明の経路が Addressables に抜けていないことを先に確かめる。

### 1.2 S-4 program から転記した制約

- **SceneGraph（program §2.1）。** `InGameSession` の下に `Season_{Season}`（OnDemand）、その下に `{Season}_Lighting`・`{Season}_AtmosphereVFX`（NecessaryAlways）、`{Season}_Cell_{x}_{y}`（OnDemand、StreamByDistance）、`{Season}_Event_{slug}`（OnDemand）。Cell の下に `{Season}_Environment_{x}_{y}`（OnDemand、resident companion）、`{Season}_Lighting_{x}_{y}`・`{Season}_VFX_{x}_{y}`・`{Season}_Events_{x}_{y}`（OnDemand、optional companion）。
- **Planner Play（program §3.1 / §6.2）。** `content:representation=Whitebox` と `world:cellCompanionSet=Planner` を使い、Environment をロードしない状態で local / major Event を実証する。companion set は Cell の companion だけを選別し、Season の子（Lighting・Atmosphere・Event）は対象外。
- **d5b の行（program §6.3）。** 前提は BS5 の Phase D 完了。
- **VFX の寿命（program §2.4、Architecture §24 §16.9 / §16.10）。** 一時 VFX の source asset は Scene が `AssetOwner.Scene(identity)` で所有してロードし、Scene の unload で VFX を回収してから asset を解放する。本スライスは所有者と順序を変えず、asset の指し方だけを変える（§2.4 D5）。
- **公開 API と依存（program §10）。** Framework は season 名、Cell 座標文法、職種 role を知らない。Game → Framework の依存方向を維持する。`SceneState` の 14 値と `LoadType` を変えない。
- **検証方針（program §11）。** `unity test` / `unity run`、Scene / asset YAML の手編集は禁止。Phase B は Editor 操作・compile 確認まで、Unity テストと Build は Phase C。

### 1.3 A0 の現況（`840b6609`）

**Editor の日常 Play の経路。** 既定の Editor Play は `AbstractApplicationInitializer` が Addressables backend で `AssetManagement` を構成し、Addressables の Play Mode script は既定（AssetDatabase 直読み）なので、source をそのまま読む。build は要らない。表現は config の `assets:sceneVariant`、companion set は `world:cellCompanionSet` から読み、config は `app-config.json` と環境変数（prefix `SAMPLEGAME_`、`:` を `__` に写す）で与える。**Planner profile で Play するには環境変数 2 つを手で置く必要があり、宣言から既定値を得る口が無い。**

**build の経路。** source → `SceneResourceContentMaterializer`（`SceneResourceMap` の Scene payload だけを候補にする）→ `SeasonSceneSelectionPolicy`（季節・表現の request と requirement）→ `BuildTagSelector`（pure）→ bootstrap composer（UICommon と `SceneResourceMap.asset` を固定 content として合成）→ `BuildContentProjection`（閉包の照合）→ `UnityContentDirectoryAdapter`（`BuildContentRoot` を生成して `BuildPipeline.BuildContentDirectory`。workspace は `work/<target>/<contentSet>` を再利用するので Unity 側は incremental）→ `BuildContentCoordinator`（workspace を丸ごと `artifacts/bs2b/content/<identity>/` へ複写して公開）→ `ContentTransportPublisher.Publish`（BS4 だけが呼ぶ）→ DIST install → Editor の検証 Play（Delivery 窓の Use For Next Play）または BS4 Player。

**選択 core（`BuildTagSelector`、302 行、pure）。** 候補は `StableKey / LogicalKey / PhysicalKey`。request は dimension ごとの OR、dimension 間の AND。候補の tag が request に無い dimension なら `UnrequestedDimension`、値が選ばれていなければ `ValueNotSelected` で除外。tag の無い dimension は中立。requirement は logical key ごとの `ExactlyOne` / `OneOrMore` で、選ばれた候補の数だけを数える。**requirement の無い logical key の候補を除外する手段は無い。**

**季節 policy（`SeasonSceneSelectionPolicy`、243 行、SampleGame）。** graph を複製して `Season` を祖先から導き、in-scope の全 node を requirement にする（最小ではなく季節の全部）。Whitebox 選択は `SeasonalMode` tag（切替可能な group にだけ付ける）で Full 専用の補助 Scene を残す。候補と node の有無が一致しないと "Payload/candidate mismatch" で止める。`FullAndWhitebox` は `OneOrMore` と `VerifyDual` で両表現を要求する。

**目的の不在。** 目的に相当するのは runtime の `CellCompanionSet`（Full / Planner / Lighting / Vfx）と `CellCompanionRoleClassifier`（identity の末尾 3 token 目で Environment / Lighting / VFX / Events を判定。`SampleGame.InGame` の internal）だけで、build 側に dimension が無い。Planner 用の Whitebox build は Planner が読まない Environment 54 件・Cell Lighting 2 件・Cell VFX 1 件を含む。逆に、Planner 用の directory を companion set Full で Play すると Environment の Add が 54 回失敗して Warning になり（`SessionCellCompanionLoadDriver` は失敗を Warning で飲む）、representation を Full にすると Cell の Whitebox は Full へ fallback しないので Scene が欠ける。**directory は自分が何の目的で作られたかを持っていない。**

**実測の graph（`SceneResourceMap`、457 node）。** OutGame: `OutGameScene`（NecessaryAlways、root）、`Title`・`HpGauge`・`OutGameSession`（OnDemand）、`ConfirmDialog`（HpGauge の子）、`HomeScene`（OutGameSession の子、payload 無し）。InGame: `InGameScene`（root、payload 無し）、`InGameSession`（NecessaryAlways、payload 無し）、`InGameUI`・`PlayerScene`（NecessaryAlways）、`Result`（OnDemand）、`Season_*` 4 件（OnDemand、payload 無し）。各 Season: `{S}_Lighting`（NecessaryAlways）、Spring だけ `Spring_AtmosphereVFX`（NecessaryAlways）と `Spring_Event_MultiCellProof`（OnDemand）、Cell 54 件（OnDemand、Variant は空と Whitebox）。各 Cell: Environment 1 件、Spring の (4,2)(5,2) に Lighting、(4,2) に VFX と Events。`Tunnel` は無い。

**実測の成果物（2026-10-30 の `spring-whitebox`）。** selected 124 件 = Scene payload 122 件 + bootstrap 2 件。Unity の出力は hash 名の `.cf` / `.resS` と hash 名の manifest `.json`、固定名 `BuildManifestHash.txt`、計 233 file・6.2 MB。manifest は `RootAssets 1 / Loadables 2 / LoadableScenes 123 / SerializedFiles 196`。閉包は 608 file。Unity の manifest は `SerializedFiles` ごとに `ContentHash` と `SerializedFileDependencies` を持ち、Scene 1 件の推移的な file 閉包は平均 7.3・最大 37、50 Scene 以上から共有される file は 6 件。file 単位の content hash と依存 graph は Unity 側に既にある（後続の差分更新の入力。§2.5）。on-disk の content build は 1 件だけで、再 build の bytes 比較はできていない。

**build identity。** `DateTime.UtcNow + Guid`（`yyyyMMddTHHmmssfffZ-<guid32>`）で、入力から導出されない。同じ入力の build は別 identity、別 directory になる。`outcome.json` は identity・path・summary だけで、選択と閉包は `preflight.json` が正本。transport の `revision` は identity、manifest の SHA-256 は install の照合に使う。**この形式は tools/Artifacts の 4 箇所（`ContentContract.psm1` の `Assert-ContentRevision`、`ContentCatalog.psm1`、`BuildContract.psm1`、`Transport/R2ArtifactTransport.psm1` と `ArtifactReadback.ps1` の key の allow-list）が正規表現で固定しており、Runtime の `ContentManifestValidation` は revision を安全な path segment としてだけ検査する。** 形式を変えると Storage program（完了、保留 list 付き）を再開することになる。

**閉包と動的ロード。** `AssetDependencySnapshotBuilder` が `AssetDatabase.GetDependencies` で root ごとに閉包を取る。Scene 内に置いた `.vfx`（`Spring_AtmosphereVFX`・`Spring_VFX_4_2`）は閉包で同梱される。**address で動的にロードする asset は 3 箇所ある:** `Spring_Events_4_2_Burst.vfx`（`CellCompanionScene`、所有者 `Scene(Events)`）、`Spring_Event_MultiCellProof_Burst.vfx`（`SeasonMajorEventScene`、所有者 `Scene(Event)`）、**`DemoCellLit.mat`（`SeasonScene.OnPreLoadedImpl`、4 つの Season が同じ material を各 `Scene(Season)` 所有でロード）**。material は Cell の閉包で directory に入っているが、key で引ける `Object` entry ではない。`.vfx` 2 件は閉包にも入らない。`BuildContentRoot` の `Object` entry を本番 build で埋める経路は bootstrap composer（`SceneResourceMap.asset`）と BS4 の `Bs4FixtureComposer`（probe prefab）だけで、どちらも選択 policy を迂回する合成。

**Runtime の backend 構成。** `AbstractApplicationInitializer` は mode に関係なく `new AssetManagement()` を作り、これは常に `AddressableBackend` を持つ。**したがって directory mode の Editor Play でも `AssetKey.FromAddress` のロードは AssetDatabase から静かに成功する。** 検証 Play は「directory だけで動く」ことを証明しない。Player では Addressables の catalog を build しない（`DoNotBuildWithPlayer`）ので、BS4 Player は Season に入った時点で `DemoCellLit.mat` のロードで落ちる見込み（Title の smoke で止まるので顕在化していない）。directory の登録と解除は `ContentLoadManager.RegisterContentDirectory` / `UnregisterContentDirectory` の runtime API で、`ContentDirectorySession` が rollback 込みで包む（実行時差し替えの出発点。§2.5）。

**Runtime の読み口。** `SceneBase.LoadSceneScopedAssetAsync(AssetKey)` だけがあり、directory の `IAssetManagement.LoadContentAssetAsync(logicalKey, representation, owner)` は Scene から届かない。`ContentDirectoryIndex` の Object 解決には Scene にある Full fallback が無い。`SceneDirector` は `_useContentDirectory` と `_sceneVariant` で Scene のロードを分岐し、`SceneBase` には `BindAssets(IAssetManagement)` だけを注入する。`AssetManagement` は canonical key で backend の asset を共有し、所有者ごとに参照を数える。

**検証 Play の publish と install。** `Tools/OSM/Content/Build ...` 4 つは Content Directory を生成するだけ。`ContentTransportPublisher.Publish` を Editor 向けに呼ぶ入口は無く、§20 / §4.8 / SEASON design が「未整備」と書く。Delivery 窓は source・manifest SHA-256・contentSet・revision・cache root・representation・budget を手で入れて Prepare → Use For Next Play する（`transport.json` は contentSet と revision を持つが窓は読まない）。`ContentDeliveryPlayBridge` は `SAMPLEGAME_CONTENT__*` の環境変数 5 つを process scope の ledger で管理し、`world:cellCompanionSet` は扱わない。publisher の staging は `<final>.<guid32>.staging` で、BS4 では repo 相対 163 文字になり、ルートが 97 文字以上の checkout で `DirectoryNotFoundException` になる（Harness README）。DIST の install は revision を丸ごと staging へ複写して rename し、HTTP source は file ごとに stream で取る（resume・部分取得は無い）。manifest の `version == 1`・`rootSchemaVersion == 2` は固定で要求され、`JsonUtility` は未知の field を無視する。

**batch の入口。** content build を起動できるのは menu だけ。`-executeMethod` の入口は退役済みの `VariantRemoteBuildBatch` しか無い。`tools/unity-editor.ps1` は Editor の起動・状態確認を持つ。

**既存テスト。** `BuildTagSelectorTests`（481）、`SceneResourceContentMaterializerTests`（431）、`BuildContentProjectionTests`（111）、`SeasonSceneSelectionPolicyTests`（181）、`ContentDirectorySessionTests`（763）、`ContentDirectoryIndexTests`（83）、`BuildContentDirectoryIntegrationTests`（490、SampleGame、`TestResults/bs3-fixture` へ実 directory を build する）、`Bs4FixtureComposerTests`、`LocalVfxEventPlayTests`、`SeasonMajorEventPlayTests`（394 + 343）、`PlayerContentConfigurationTests`、`ContentDeliveryFailureMatrixTests`。

### 1.4 参照者と経路（A0）

4 者が「必要・所持・経路」を持つ。本スライスは「必要をどう知るか」の列を宣言で埋め、「経路」の列は現状の 2 つ（source、directory）を変えない。

| 誰 | 必要なものをどう知るか | 持っているもの | 足りないものの経路 | 本スライス後 |
|---|---|---|---|---|
| Editor（日常） | 開いている Scene graph と、選んだ宣言の runtime 既定値 | AssetDatabase の source | （後続）LAN / R2 の object | 宣言を選んで Play する（D7）。build は要らない |
| Editor（成果物の検証） | 宣言 | installed directory | なし。fail-closed | 宣言 1 つから build → publish → install → 検証 Play（D8・D10） |
| Player | baked の宣言 | 内包 / installed | （後続）R2 の object の差分、実行時差し替え | baked config の既定値を宣言から写す（D11） |
| 別 PC / Cloud | 宣言名 | 無い、または cache | published directory、（後続）R2 の object | batch で build と publish（D12）。Editor は published directory から検証 Play（D8） |

### 1.5 ユースケースと足りないもの（A0）

| # | 誰が何をする | 今 | 本スライス後 | 残る後続 |
|---|---|---|---|---|
| UC1 | 企画者が Whitebox Cell や Event を直してそのまま Play | source route で動く | 変えない（I8） | — |
| UC2 | 企画者が Planner profile（Environment 無し）で Play | 環境変数 2 つを手で置く | 宣言 `spring-planner-whitebox` を選んで Play（D7、AC16） | — |
| UC3 | 企画者が自分の持たない asset（別部署の Environment 等）を含む Scene を Play | 不可。Addressables が source を要する | 変えない。必要集合は宣言から出る | 経路の連鎖と object store（§2.5） |
| UC4 | 担当が「この目的の成果物だけで動くか」を Editor で確かめる | 窓に 5 欄を手入力。Addressables に抜ける | 1 操作で build → 検証 Play、fail-closed（D8・D10、AC15） | — |
| UC5 | QA / release 向けに目的 X の Player を作る | BS4 は Spring の全部を内包、`.vfx` は入らない、companion set は手書き | 付随 asset が入り、既定値は宣言から（D5・D11、AC17） | Spring 進入、差分更新（§2.5） |
| UC6 | CI が clean checkout で目的 X を build し publish する | menu しか無い | batch 入口（D12、AC23）、Full / Incremental（D14） | R2 へ object 単位で置く（§2.5） |
| UC7 | 別 PC が published directory を受け取り Editor で検証 Play | 窓に手入力 | From published directory（D8、AC22） | — |
| UC8 | 同じ source・同じ宣言で 2 回 build する | 2 つの directory ができる | 入力 identity が一致すれば既存を返す（D4、AC8） | bytes の決定性の測定（§2.5） |
| UC9 | 同じ Scene を複数の目的で build する（spring-full と spring-whitebox） | 目的ごとに全部格納 | 変えない | object 単位の格納（§2.5） |
| UC10 | Player が動いたまま content を差し替える | 不可 | 変えない | 実行時差し替え（§2.5） |

足りないと分かったもの: UC2（build 無しで宣言を効かせる口）が第 2 版まで無かった。UC3・UC9・UC10 は本スライスで作らないが、宣言と選択の形（logical key の集合と所有者）がその入力になるように決める（D1・D5）。

---

## 2. 意思決定と受け入れ境界（A1）

- **このスライスが答える問い:** 目的・設定・Variant・runtime 既定値の宣言から、必要十分な Scene と付随 asset の集合が決定的に導かれ、同じ宣言が build 無しの日常 Play・検証 Play・Player・batch で同じ既定値として働き、その成果物だけで検証 Play と Player が動くか。

### 2.1 不変条件（起きてはいけないこと）

- **I1（必要なものを落とさない）:** 宣言の root から、`NecessaryAlways` / `IncrementalAlways` の子を全て、`OnDemand` の子を目的の filter が通すものだけたどって到達する node は、payload があれば必ず選ばれる。到達する node の付随 asset も必ず選ばれる。
- **I2（不要なものを入れない）:** 到達しない node の候補と、到達しない node の付随 asset は選ばれない。表現の優先順で選ばれなかった候補も選ばれない。
- **I3（決定的）:** 同じ宣言と同じ source からは、同じ選択（stable key の集合）、同じ閉包、同じ入力 identity が得られる。候補・provider・node の入力順と build mode（§2.4 D14）に依存しない。
- **I4（既存の成果物を変えない）:** 既存 4 つの content build と BS4 の `bs4-spring-full` は、新しい宣言に置き換えた後も selected の stable key 集合が `840b6609` の policy と一致する（付随 asset の追加分を除く）。
- **I5（Framework は語彙を知らない）:** Framework の選択・materialization・identity・Runtime の読み口・宣言の運搬は、season 名・Cell 座標文法・職種 role・`CellCompanionSet` を参照しない。宣言の runtime 値は Framework にとって不透明な key=value。Game → Framework の依存方向を維持する。
- **I6（寿命と順序を変えない）:** 付随 asset は従来どおり読む Scene が `AssetOwner.Scene(identity)` で所有し、VFX の回収 → asset の解放の順序、d4b / d5a の既存テストの結果を変えない。`SceneState`、`LoadType`、asmdef 参照を変えない。
- **I7（directory の外を読まない）:** directory mode の検証 Play と Player は、Addressables backend からロードしない。`AssetKey.FromAddress` / `FromDescription` によるロードは directory mode では失敗する。
- **I8（日常 Play を変えない）:** Addressables mode の Editor Play の読み口・起動順序・config の読み方は変えない。宣言を選ばない Play は従来どおり動く。
- **I9（識別子の形式を変えない）:** transport の `revision`、DIST の `installed/<contentSet>/<revision>`、Storage の key の形式は変えない。入力 identity は欄として運び、名前にしない。

### 2.2 成立条件（起きなければならないこと）

- **E1（宣言）:** 目的・設定・Variant と runtime の既定値を 1 つの宣言型で表せ、SampleGame の 5 つの content set（`all-full`、`spring-full`、`spring-whitebox`、`spring-full-whitebox`、`bs4-spring-full`）と新しい `spring-planner-whitebox` が宣言として存在する。
- **E2（最小選択）:** `spring-planner-whitebox` の選択は、Spring の Cell 54 件（Whitebox）、`Spring_Events_4_2`、`Spring_Event_MultiCellProof`、`Spring_Lighting`、`Spring_AtmosphereVFX`、InGame / OutGame の到達 Scene、bootstrap 2 件、付随 asset 3 件で、Environment・Cell Lighting・Cell VFX・他季節を含まない（§2.3 AC4 の件数）。
- **E3（付随 asset と共有 asset）:** `Spring_Events_4_2` と `Spring_Event_MultiCellProof` の `.vfx`、`InGameSession` が所有する `DemoCellLit.mat` が、所有 node が到達する全ての宣言の Content Directory に `Object` entry として入り、読む Scene から所有者と名前で解決できる。Addressables mode の Editor Play でも同じ呼び出しで解決できる。
- **E4（入力 identity と再利用）:** 入力 identity は宣言と選択済み候補の source 状態から計算され、`preflight.json`・`BuildContentRoot`・manifest に載る。同じ入力 identity の 2 回目の build は build を実行せず既存の revision を返す。revision の形式は従来どおり。
- **E5（宣言の運搬）:** 宣言の名前・表現の主値・runtime の key=value が `BuildContentRoot` と `transport.json` に載り、検証 Play の Use For Next Play と Player の baked config がそれを既定値にする。
- **E6（build 無しの日常 Play）:** 宣言を選ぶ 1 操作で、次の Addressables mode の Play に宣言の runtime 既定値（表現、companion set）が効き、解除で元に戻る。
- **E7（検証 Play の 1 操作と fail-closed）:** 宣言ごとの 1 操作で content build → transport publish → DIST install → Use For Next Play まで進み、そのまま検証 Play できる。その Play で `spring-full` の directory から Cell (4,2) の Events の trigger の VFX と Season の Lit が解決され、Error ログが出ず、Addressables backend は作られていない。Delivery 窓は published directory から manifest SHA-256・contentSet・revision を読める。publish の staging path は path 予算の範囲に収まる。
- **E8（batch）:** 宣言名を引数に取る batch 入口から content build と publish を起動でき、終了コードと `preflight.json` / `outcome.json` で結果を読める。判定 C の build はこの入口で行う。

### 2.3 受け入れ条件（検証する条件を併記）

単体 = Unity に依存しない純 C# テスト。EditMode = AssetDatabase や合成 fixture を使うテスト。Play = Play Mode テスト。操作 = 動作確認と Phase C の実行。

| AC | 内容 | 条件 | 種別 |
|---|---|---|---|
| AC1 | 合成 graph（root、NecessaryAlways / IncrementalAlways / OnDemand の子、payload 無しの中間 node、付随 asset 付きの node）で、到達規則どおりの requirement 集合が得られる。OnDemand の子は filter の dimension ごとの OR・dimension 間の AND・tag 無し中立・未要求 dimension の tag は拒否、の規則で判定される。payload の無い到達 node の付随 asset も requirement になる | I1・I2 | 単体 |
| AC2 | root でない node、到達しない subtree の node、到達しない node の付随 asset の候補は `Unreachable` の理由で除外される。到達する node の候補を欠くと `CardinalityViolation` で plan が公開されない | I1・I2 | 単体（失敗経路） |
| AC3 | 表現の優先順 `[Whitebox, Full]` で Whitebox 候補がある key は Whitebox だけ、Full しか無い key は Full が選ばれる。`All` では両方が選ばれ requirement は `OneOrMore`。優先順に無い表現の候補は除外される | I2 | 単体 |
| AC4 | 実 `SceneResourceMap` で `spring-planner-whitebox` の selected が、Role ∈ {Environment, Lighting, Vfx} の Cell companion を 1 件も含まず、Spring の Cell 54 件を全て Whitebox で含み、`Spring_Events_4_2`・`Spring_Event_MultiCellProof`・`Spring_Lighting`・`Spring_AtmosphereVFX` と付随 asset 3 件（`.vfx` 2 件、`InGameSession:Lit`）を含む。件数は Scene payload 65 + bootstrap 2 + 付随 3 = 70 | E2 | EditMode |
| AC5 | 実 `SceneResourceMap` で `spring-whitebox` の selected が、§1.3 の実測 124 件の stable key 集合 + 付随 asset 3 件に一致する。`spring-full`・`all-full`・`spring-full-whitebox`・`bs4-spring-full` も `840b6609` の policy の選択 + 付随 asset に一致する（A3 で各集合の件数と sorted stable key 列の SHA-256 を凍結する。§5 の照合 script） | I4 | EditMode + 判定 C の照合 |
| AC6 | 候補・provider・node を逆順・シャッフルで与えても、plan の selected / excluded の列と入力 identity が同じ。build mode を `Full` / `Incremental` に変えても入力 identity は同じ | I3 | 単体 |
| AC7 | 入力 identity は宣言（名前・root・filter・表現の優先順・設定・runtime の既定値）と、選ばれた候補の stable key・physical GUID・source の依存 hash の正準化文字列の SHA-256（64 hex）。候補の依存 hash が 1 つ変わる、宣言の filter や runtime の既定値が変わる、設定が変わる、のどれでも変わる。順序の違いでは変わらない | I3・E4 | 単体（偽 gateway） |
| AC8 | 同じ入力 identity の成功した revision が index に在り、その `content/<revision>` が在れば、coordinator は adapter を呼ばず `outcome.json` に `reused=true` と既存 revision を書いて成功を返す。index だけが在って directory が無い、または staging だけが残る中途状態では再利用せず build する。revision は従来の `UtcNow + Guid` の形式のまま | E4・I9 | EditMode（偽 adapter） |
| AC9 | materializer が任意の node（payload の無い node を含む）の付随 asset から候補（logical key `<identity>:<name>`、Representation は payload の Variant、`Owner` tag に所有 node の identity）と閉包を作る。名前の重複、参照欠損、同じ physical GUID を 2 つの node が宣言する、所有 node の identity と衝突する名前、は issue で止まる | E3 | EditMode（既存 materializer テストと同じ偽 gateway） |
| AC10 | 合成 fixture directory（`TestResults/bs3-fixture`）に Scene 1 件と付随 `Object` 1 件（Full）を build し、representation `Whitebox` で Scene を登録した session から付随 asset が Full fallback で解決・ロード・解放できる。存在しない名前は `EntryMissing`。同じ entry を 2 つの所有者で取得して片方を解放しても、もう片方は有効 | E3 | EditMode（既存 integration テストの拡張） |
| AC11 | `SceneBase.LoadAttachedAssetAsync<T>(name)` と `LoadAttachedAssetAsync<T>(ownerIdentity, name)` は、directory モードでは `LoadContentAssetAsync(<owner>:<name>, sceneVariant, Scene(自分の identity))`、それ以外では `LoadAssetAsync(AssetKey.FromDescription(所有 node の付随 desc, sceneVariant), Scene(自分の identity))` を呼ぶ。未宣言の名前・未知の owner は `ArgumentException`。所有者は常に読む Scene | E3・I6 | 単体（偽 `IAssetManagement`） |
| AC12 | 既存の `LocalVfxEventPlayTests` と `SeasonMajorEventPlayTests` が、trigger / anchor の address を付随 asset の名前に替えた後も通る（Addressables モード、`-WithGraphics`） | I6・I8 | Play |
| AC13 | `Spring_Events_4_2`・`Spring_Event_MultiCellProof` の `SceneResource` に付随 asset `Burst`、`InGameSession` に付随 asset `Lit`（`DemoCellLit.mat`）が宣言され、Variant は空。trigger / anchor の名前が `Burst`、`SeasonScene` が `InGameSession:Lit` を読む | E3 | EditMode |
| AC14 | `Build And Publish` の後、published directory に `transport.json` と `content/` があり、manifest の files が directory の全 file と size・SHA-256 で一致し、manifest に宣言の名前・表現の主値・runtime の key=value・入力 identity が入る。staging path の repo 相対長が `tools/path-budget.json` の上限以内 | E4・E5・E7 | 操作（動作確認）+ EditMode（path 長の単体、manifest の往復の単体） |
| AC15 | `spring-full` の `Build And Verify Next Play` 1 操作の後、そのまま Play して Spring (0,4) が Stable になり、Cell (4,2) へ移動して Events の trigger に入ると VFX が起動する。Console に Error が無く、起動ログに Addressables backend 無しの構成の 1 行がある | E7・I7 | 操作（動作確認）、証拠は Console ログ |
| AC16 | build せずに `Play As spring-planner-whitebox` を選んで Play すると、Spring (0,4) が Stable になり、Environment が 1 件もロードされず、Cell は Whitebox で、Error が無い（環境変数を手で置かない）。解除の後の Play は従来どおり Full で Environment が載る。Event の動作は d5b で確かめる | E6・I8 | 操作（動作確認） |
| AC17 | BS4 の `Build BS4 Spring Player` が成功し、Player の smoke receipt が `completed`。Player 内 `content/` に付随 asset 3 件の `Object` entry が入る（manifest の `Loadables` が 2 + 3 = 5）。`bs4-runtime.json` の `cellCompanionSet` と `representation` が宣言の値 | I4・E3・E5 | 高コスト証拠（§5） |
| AC18 | 最終の全 EditMode 回帰と、Streaming・Vfx の PlayMode が通る | I6・I8 | 回帰 |
| AC19 | directory mode で構成した `AssetManagement` は Addressables backend を持たず、`LoadAssetAsync(AssetKey.FromAddress(...))` と `LoadSceneAsync(desc)` は `InvalidOperationException` で失敗する。directory の `LoadContentAssetAsync` / `LoadContentSceneAsync` は動く。Addressables mode の構成は従来どおり | I7・I8 | 単体 + EditMode（既存 `ContentDirectorySessionTests` の構成） |
| AC20 | `InGameSession` の付随 asset `Lit` は候補 1 件で、4 つの `SeasonScene` が `LoadAttachedAssetAsync("InGameSession", "Lit")` で同じ backend asset を各 `Scene(Season)` 所有で取得し、1 つの Season を unload しても他の Season の handle は有効 | E3・I6 | 単体（偽 `IAssetManagement` で呼び出しの形）+ AC10（共有の解放） |
| AC21 | `ContentDeliveryPlayBridge` が、(a) 宣言だけから（build 無し）`SAMPLEGAME_ASSETS__SCENEVARIANT` と宣言の runtime key=value（例 `SAMPLEGAME_WORLD__CELLCOMPANIONSET=Planner`）を、(b) published directory の manifest から従来の 5 つに加えて同じ key=value を、process scope の ledger に入れ、`Reset` で元へ戻す。(a) と (b) は同時に有効にならない。Framework 側は key を解釈せず、`SAMPLEGAME_` + `__` の変換規則は Game 側に置く。Player の `PlayerContentConfiguration.Resolve` は baked config に無い key を宣言から補わない | E5・E6・I5 | 単体（ledger の往復）+ `PlayerContentConfigurationTests` |
| AC22 | Delivery 窓の「From published directory」で、path 1 つから manifest SHA-256・contentSet・revision・representation・宣言名が埋まり、Prepare → Use For Next Play が成功する。manifest が無い path・SHA-256 の不一致は失敗で、ledger を変えない | E7 | 操作 + 単体（読み取りの失敗経路） |
| AC23 | `Unity.exe -batchmode -quit -executeMethod <入口> -osmPurpose spring-full -osmPublish` が content build と publish を行い、成功で 0、宣言名の誤り・build 失敗で非 0 を返し、`preflight.json` / `outcome.json` / published directory の path を log に出す。menu と同じ coordinator を通る | E8 | EditMode（引数の解釈の単体）+ 判定 C での実行 |

AC4 の読み方: 「Role ∈ {...} を含まない」は selected の logical key に `_Environment_`・`_Lighting_{x}_{y}`・`_VFX_{x}_{y}` の Cell companion が無いこと。`Spring_Lighting`（Season の NecessaryAlways）は含む。合格例: 70 件で Environment 0 件。不合格例: Environment が 1 件でも入る、Cell が 53 件、`InGameSession:Lit` が無い。

AC8 の読み方: 「中途状態」は `staging/<revision>` だけが在って `content/<revision>` が無い状態。合格例: index が指す `content/<revision>` が在れば adapter は呼ばれない。不合格例: staging の残骸を成功として返す、index の無い directory を名前の一致だけで再利用する。

AC15 の読み方: 「Addressables backend 無しの構成の 1 行」は `content:runtimeMode=directory` の Play で `AssetManagement` が backend 無しで構成されたことを示す起動ログ。合格例: その行があり、Lit と VFX が解決される。不合格例: その行が無い（Addressables から読めてしまう）。

AC16 の読み方: 「Environment がロードされない」は `SceneDirector` のロード済み一覧に `_Environment_` が無いこと。「Whitebox」は Cell の Scene 名が Whitebox payload のもの。合格例: 環境変数を置かずに両方が成り立つ。不合格例: companion set は Planner だが Cell が Full、または解除後も Planner のまま。

### 2.4 A1 の設計

- **D1（宣言型）:** `BuildPurposeDeclaration`（`OneStarMaker.Build.Selection`、pure）。`Name`（contentSet と同じ規則の ASCII）、`Roots`（logical key の列）、`Filter`（`BuildRequest`。OnDemand の子に適用する tag の述語）、`Representation`（優先順の列と `First` / `All`）、`Configuration`（target、compression、options、defines、Unity version、root schema version の正準化した key=value 列。本スライスでは adapter の定数と Editor の値を写すだけ）、`RuntimeDefaults`（Game が決める不透明な key=value 列。例 `world:cellCompanionSet=Planner`）。Framework は値の意味を知らない。設定の値は Game の tag policy が node 水準の tag（例 `Platform`）に写して filter の入力にもできる。宣言は build の入力であると同時に、build 無しの Play の既定値の供給源でもある（D7）。
- **D2（到達規則）:** `BuildPurposeResolver`（pure）。入力は graph node（identity、parent、children、LoadType、payload の有無、付随 asset の名前列）、node ごとの tag、候補と candidate tag provider、宣言。出力は requirement（到達 node の payload と付随 asset。`First` なら `ExactlyOne`、`All` なら `OneOrMore`）、Representation の request、到達しない候補と優先順に無い表現の候補の除外。規則: `required(n) ⇔ n ∈ roots ∨ (required(parent) ∧ (LoadType ∈ {NecessaryAlways, IncrementalAlways} ∨ filter(tags(n))))`。filter の意味は既存 `BuildTagSelector` の request の意味と同じ。payload の無い中間 node（`InGameSession`、`Season_*`）も到達の経路になり、付随 asset を持てる。graph の検証は既存 `SeasonSceneSelectionPolicy.ValidateGraph` の規則を Framework へ移す。
- **D3（選択 core の拡張）:** `BuildSelectionPolicy` に `ExcludeUnrequired`（requirement の無い logical key の候補を `BuildExclusionReasonCode.Unreachable` で除外）と、`BuildContentRequirement` に表現の優先（dimension、順序付き値、`First` / `All`）を足す。`SeasonalMode` tag と `VerifyDual` は不要になり、SampleGame の policy から消す。tag を node 水準（Season、Role）と candidate 水準（Representation）に分け、node 水準は Game が供給する。
- **D4（入力 identity と再利用。revision は変えない）:** `BuildContentIdentity`（`OneStarMaker.Build.Content`、pure + gateway）が正準化文字列（宣言の各欄（sorted、`RuntimeDefaults` を含む）、設定（sorted key=value）、選ばれた候補ごとの `stableKey|physicalGuid|dependencyHash`（stable key で sorted））の SHA-256（64 hex）を入力 identity として計算する。`dependencyHash` は `IAssetDatabaseGateway.GetDependencyHash(path)`（`AssetDatabase.GetAssetDependencyHash`）。**revision（directory 名、`BuildContentRoot.BuildIdentity`、transport の `revision`）は従来の `UtcNow + Guid` のままにし、入力 identity は `preflight.json`・`BuildContentRoot.InputIdentity`・manifest の `inputIdentity` の欄として運ぶ。** coordinator は成功時に `content/index/<inputIdentity>` へ revision 名を書き、次回は index が指す `content/<revision>` が在れば adapter を呼ばず再利用する（AC8）。この形なら Storage・DIST・transport の形式に触れず（I9）、同じ入力で bytes が違う build が 2 つできても別 revision として共存し、どちらが入力 identity の正か index が決める。build mode（D14）と時刻は `preflight.json` の metadata に残すだけで入力 identity に入れない。既知の限界: project settings（Graphics API、Quality、Lightmap 設定）は入力 identity に入らない（§2.6 U3）。
- **D5（付随 asset と共有 asset）:** Runtime に `AttachedAssetDescription : AssetDescription`（`Name` と `AssetPayload` の列。`[Serializable]`、埋め込み）を足し、`SceneResource` に `_attachedAssets` と `AttachedAssets` / `FindAttachedAsset(name)` を足す。payload の無い node（`InGameSession`）も宣言できる。materializer は付随 asset ごとに候補（logical key `<identity>:<name>`、stable key は既存の `osm-build-candidate-v1` 規則、Representation は Variant から既存規則で写す、`Owner` tag に所有 identity、provenance `SceneResourceMap.AttachedAsset`）と閉包を作る。同じ physical GUID は 1 つの node だけが宣言できる（共有は祖先に 1 回宣言し、子孫が所有者を指定して読む）。`SceneBase` に `LoadAttachedAssetAsync<T>(string name, ct)` と `LoadAttachedAssetAsync<T>(string ownerIdentity, string name, ct)` を足し、`SceneDirector` が `BindAssets` と同じ時点で `useContentDirectory`・`sceneVariant`・`SceneResourceMap` の検索口を `SceneContentBinding`（internal の値型）として注入する。directory モードは `LoadContentAssetAsync(<owner>:<name>, sceneVariant, Scene(自分))`、それ以外は `LoadAssetAsync(AssetKey.FromDescription(desc, sceneVariant), Scene(自分))`。所有者は常に読む Scene なので、共有 asset は `AssetManagement` の canonical key の共有と所有者ごとの参照で従来どおり数えられる。`ContentDirectoryIndex.TryResolveObject` に Scene と同じ Full fallback を与える。trigger / anchor の `_vfxAddress` は `_vfxAsset`（付随 asset の名前）に替え、`CellCompanionScene` / `SeasonMajorEventScene` は `LoadAttachedAssetAsync(name)`、`SeasonScene` は `LoadAttachedAssetAsync("InGameSession", "Lit")` を呼ぶ。`.vfx` と material の Addressables entry は Addressables モードの `AssetReference` が要するので残す。所有者・回収順序・ロード失敗の扱いは変えない。
- **D6（SampleGame の policy）:** `SeasonSceneSelectionPolicy` を、node 水準の tag を導く `SeasonSceneTagPolicy`（`Season` は祖先から、`Role` は `CellCompanionRoleClassifier` で。classifier は `InternalsVisibleTo("SampleGame.DependOnAll.Editor")` で参照し、規則を複製しない）と、宣言の表 `SampleGameBuildPurposes`（6 件。root は `OutGameScene`・`InGameScene`、filter は `Season` と `Role`、表現の優先順、`RuntimeDefaults` に `world:cellCompanionSet` と `content:representation`）に置き換える。`SampleGameContentBuild` は宣言を引数に取り、menu は宣言ごとの `Build`・`Build And Publish`・`Build And Verify Next Play` と、build を伴わない `Play As <宣言>` にする。bootstrap composer と BS4 の fixture composer は resolver の後に従来どおり合成する。
- **D7（build 無しの日常 Play に宣言を効かせる）:** `Tools/OSM/Content/Play As/<宣言名>` と `Play As/Reset`。`ContentDeliveryPlayBridge.ApplyDeclaration(declaration)` が、宣言の表現の主値を `SAMPLEGAME_ASSETS__SCENEVARIANT` に、`RuntimeDefaults` の各 key を Game の規則で環境変数名に写して、既存の process scope の ledger に入れる。`content:runtimeMode` は置かないので Addressables mode のまま source を読む。directory の `Apply` と同時に有効にならない（先に `Reset`）。Framework は変えず、`SampleGame.DependOnAll.Editor` の中で閉じる。企画者の Planner Play（UC2）はこれで足り、d5b の Event 実証は build を要しない。
- **D8（検証 Play の 1 操作と published directory）:** `SampleGameContentBuild.BuildAndPublish(declaration)` が coordinator の結果を `ContentTransportPublisher.Publish(result, <artifactsRoot>/published/<revision>)` へ渡す。`BuildAndVerifyNextPlay(declaration)` はさらに既定の cache root（Delivery 窓の保存値、無ければ `UserSettings` 配下）へ `ContentInstaller` で install し、`ContentDeliveryPlayBridge.Apply` を呼ぶ。Delivery 窓に「From published directory」を足し、`transport.json` から manifest SHA-256・contentSet・revision・表現の主値・宣言名を埋める（同一 PC の自分の成果物と、別 PC から運んだ成果物の両方。manifest の bytes を照合するので安全性は従来と同じ）。publisher の staging は `<final>.<8 hex>.tmp` にして repo 相対長を予算内にする。
- **D9（authoring）:** 3 つの `SceneResource`（`Spring_Events_4_2`・`Spring_Event_MultiCellProof`・`InGameSession`）への付随 asset の宣言と trigger / anchor の名前は、Phase B が Editor の Inspector または一時スクリプトで行い、一時スクリプトは commit の前に消す。World Workspace の transaction は変えない。
- **D10（directory mode の fail-closed）:** `AssetManagement` に backend 無しの構成（`AssetManagement.ForContentDirectory()` または `IAddressableBackend?` を受ける internal constructor）を足し、`AbstractApplicationInitializer` は `ReadContentDirectoryConfiguration` が directory を返した時だけそれを使う。backend 無しの `LoadAssetAsync` / `LoadSceneAsync` / `LoadAppAssetSync` は `InvalidOperationException`（「directory mode では Addressables を使わない」）で失敗し、`CreateVfxBackend` 等の他の初期化は変えない。起動ログに構成を 1 行出す（AC15 の読み方）。Addressables mode と Player の BS4 以外の経路は変えない。`UICommon` の bootstrap が Addressables を要する経路が残っていれば B は止めて A へ返す（§4.2）。
- **D11（宣言の運搬）:** `BuildContentRoot` に `Declaration`（name、representation の主値、runtime の key=value 列）と `InputIdentity` を任意の serialized field として足し、adapter が宣言から写す。`ContentTransportManifest` に同じ `declaration` と `inputIdentity` を任意 field として足す。schema version（root 2、manifest 1）は変えず、宣言の無い root / manifest は従来どおり既定値なしとして扱う（既存の installed revision の再検証を壊さない）。`ContentDeliveryPlayBridge.Apply` は manifest の runtime の key=value を環境変数へ写す。Framework は `content:*` 以外の key を解釈せず、Game の key を環境変数名に変換する規則（`SAMPLEGAME_` + `__` 区切り）は `AppInitializer` 側の既存規則に合わせる。BS4 の `RuntimeJson` は `bs4-spring-full` 宣言の `RuntimeDefaults` から `cellCompanionSet` と representation を写す（手書きの定数をやめる）。
- **D12（batch 入口）:** `SampleGameContentBuild.BuildFromCommandLine()`（`-executeMethod` の対象）。引数は `-osmPurpose <name>`、`-osmPublish`、`-osmBuildMode Full|Incremental`（既定 Incremental）。menu と同じ `BuildResult` を通り、終了コードは成功 0、引数不正 2、build 失敗 1。log に `preflight.json` / `outcome.json` / published directory の path を出す。`tools/unity-editor.ps1` の既存の起動経路から呼べる wrapper は Phase B の判断で足し、無ければ README にコマンド行を書く。
- **D13（既存 build 入口）:** 既存 4 つの menu は宣言ごとの `Build` に置き換え、名前（contentSet）は変えない。`bs4-spring-full` は BS4 coordinator が宣言を引数に呼ぶ。
- **D14（build mode）:** `BuildContentRequest` に `BuildMode`（`Incremental` = workspace を再利用、`Full` = workspace を空にしてから build）を足す。入力 identity には入れず `preflight.json` に記録する。本スライスは `Incremental` を既定にし、`Full` は CI の clean build と、後続の決定性の測定の入口。

### 2.5 ここでは答えない問いと所有する後続

| 問い | 事実 | 所有 |
|---|---|---|
| 経路の連鎖: key ごとに source → local の object store → LAN / R2 の順で探し、Editor が自分の持たない asset を built content から読む（UC3） | Unity は content directory を複数登録できるが directory 間は孤立し、同じ asset が source と directory の両方にあると別 object として二重に載る。共有 asset を持つ Scene を source と fetch で混ぜる時の扱いを先に決める必要がある。必要集合は本スライスの宣言と選択から出る | BuildSystem 後続 |
| object 単位の格納と差分更新（UC6・UC9）: 同じ bytes を revision・目的をまたいで一度だけ転送・格納し、差分だけを更新する。差分更新と全部更新は「手元に無い hash を取る」1 つのアルゴリズムで、全部更新は手元が空の場合 | Unity の manifest が file ごとの `ContentHash` と依存 graph を持つ（§1.3）。transport の manifest は file ごとの SHA-256 を持つ。DIST は revision 丸ごとの複写、Storage は revision/file の key | BuildSystem 後続（Storage program の保留項目「Contentの分離・取得・取り込み」「孤児回収」「不足 file の再利用」と接続） |
| 同じ宣言の再 build が同じ file 名と bytes を出すか（Incremental と Full の一致） | 正しさは入力 identity と index で保てる（D4）。決定性は格納の最小性にだけ効く | BuildSystem 後続（spike。D14 の `Full` が入口） |
| Player の実行時差し替え（UC10） | 登録と解除は runtime API で、session が rollback 込みで包む。旧 directory から読んだ Scene が解除後も生きるかは未測定 | BuildSystem 後続 |
| 複数 directory の同時登録、共通 base と目的差分への層化、cross-directory 参照 | 上の孤立の制約 | BuildSystem 後続（spike） |
| 設定（target、compression、defines、project settings）を宣言で変え、入力 identity に project settings を含める | — | BuildSystem 後続 |
| InGame の Spring に入る Player の build 経路、Player での VFX 描画 | — | BuildSystem 後続 / S-9 |
| `outcome.json` の選択・閉包の記録形式（§24 §16.7） | — | BuildSystem 後続 |
| Planner profile の Whitebox 上の Event の動作、S-4 program の harvest と削除 | `Play As spring-planner-whitebox`（D7）で build 無しに実証できる | S-4d5b |
| Addressables の退役（`.vfx`・material の entry を含む）、旧 Variant 経路の整理 | — | ADDR-RETIRE / ADDR-HYGIENE（§20 §9） |
| 付随 asset の World Workspace transaction への組み込み、Variant 別の付随 asset | — | 需要が出たスライス |
| directory に無い OnDemand content を要求した時の Runtime の振る舞い（Cell の欠落、companion の Warning の集約） | 本スライスは宣言の既定値で不一致を防ぐだけ | 需要が出たスライス |
| §24 §16.7 の Framework の欠落（親の unload 開始後の子の Add、`LoadAssetAsync` の await 後の所有者確認） | — | 所有スライス未定のまま |

- **判定定義:** 不変条件 I1〜I9 と成立条件 E1〜E8 を AC1〜AC23 で確かめ、全て成立で GO。1 つでも不成立なら NO-GO。
- **停止規則:** 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。スパイクではないので CONDITIONAL ACCEPT は使わない。
- **A3 後の例外承認:** なし。
- **決定記録:** 2026-10-31（Phase A）: revision の形式を変えず、宣言から導く identity は欄として運ぶ（D4）。理由は tools/Artifacts の形式固定（§1.3）と、同じ入力で bytes の違う build が共存できること。第 2 版の U8（Storage の正規表現を広げる案）はこれで不要になった。
- **本文へ転記した実装制約:** §1.1・§1.2。Unity 側 `.cs` は `#nullable enable`、`record` 禁止、破棄されうる `UnityEngine.Object` の `== null` 判定、公開 API のログ抽象は `ILogger<T>`。
- **未決事項:** §2.6。

### 2.6 未決事項

- **U1（分割）:** 見積もりは製品コード 1,200〜1,500 行、テスト 1,100〜1,500 行で、500 行の警報を超える。非分割の理由: 発注者の最初の決定（非 Scene asset を Build に乗せる = D5）、選択の最小性（D1〜D3・D6）、実ユースケース（D7・D8・D10〜D12）の 3 つが揃って初めて「その成果物だけで動く」と言え、入力 identity は選択の出力に依存する。**分割の引き金:** Phase B で Runtime 側（D5・D10・D11 の Runtime 部分）が Framework の公開面を `AttachedAssetDescription`・`SceneResource.AttachedAssets`・`SceneBase.LoadAttachedAssetAsync`（2 overload）・`AssetManagement` の backend 無し構成・`BuildContentRoot.Declaration` / `InputIdentity` の 5 点を超えて要する場合、または Runtime 側の製品コードが 400 行を超える場合は B を止め、Runtime 側を BS5b として A3 で分ける。
- **U2（tag の水準）:** 推奨は D3 の二水準（node と candidate）。代案は候補の tag だけで到達を判定する（Representation の違う候補で node の tag が揺れるので、payload ごとに同じ tag を重複して付けることになる）。
- **U3（再利用の安全）:** 推奨は AC8 の index による再利用。代案は再利用しない（同じ入力の再 build が常に新 revision を作る。正しいが遅い）。再利用の誤りの源（入力 identity に入らない project settings）は D4 の既知の限界として記録し、index は `Full` build（D14）で上書きできるようにする。
- **U4（Object の Full fallback）:** 推奨は Scene と同じ fallback（D5）。代案は宣言側で付随 asset に表現ごとの payload を要求する（SampleGame の 3 件は Full のみで、Whitebox Play で必ず欠ける）。
- **U5（fail-closed の置き場）:** 推奨は D10（backend 無しの構成。Addressables の初期化自体が走らない）。代案は `AssetManagement` に mode flag を持たせて `FromAddress` を拒否する（backend は作られ、Addressables の初期化が走る。拒否の網羅が実装依存になる）。
- **U6（宣言の運搬の経路）:** 推奨は D11（`BuildContentRoot` と manifest の両方に載せ、Editor は manifest、Player は baked config を読む）。代案は manifest だけ（Player が宣言を知る経路が無くなり、BS4 の RuntimeJson の手書きが残る）。
- **U7（Play As の置き場）:** 推奨は D7（`ContentDeliveryPlayBridge` の ledger を共用し、SampleGame の Editor に閉じる）。代案は Framework の Editor に汎用の「宣言 → 環境変数」を置く（環境変数名の規則は Game のものなので、Framework に置くと規則の注入が要る）。

---

## 3. 責務マップ（A1）

| ファイル | 責務・変更理由 | 所有者・寿命 | 依存・公開面 | テスト境界 | 行数（現在 → 予想） |
|---|---|---|---|---|---|
| 新規 `Editor/Build/Selection/Model/BuildPurposeDeclaration.cs` | 目的・設定・Variant・runtime 既定値の宣言（immutable、正準化） | 呼び出しごと | pure。public | 単体 | 0 → 100〜130 |
| 新規 `Editor/Build/Selection/Model/BuildGraphNode.cs` | 到達規則の入力 node | 呼び出しごと | pure。public。`LoadType` は Runtime の enum を参照しない（`BuildGraphLoad` の 3 値に写す） | 単体 | 0 → 50 |
| 新規 `Editor/Build/Selection/BuildPurposeResolver.cs` | graph の検証、到達規則、requirement と request と除外の導出 | 呼び出しごと | pure。public。`BuildTagSelector` を呼ぶ | 単体（AC1〜AC3・AC6） | 0 → 180〜240 |
| `Editor/Build/Selection/BuildTagSelector.cs` | `ExcludeUnrequired` と表現の優先の適用 | — | pure | `BuildTagSelectorTests` 拡張 | 302 → 350〜370 |
| `Selection/Policy/BuildSelectionPolicy.cs`・`BuildContentRequirement.cs`・`Results/BuildExclusionReasonCode.cs` | policy の flag、requirement の表現優先、`Unreachable`・`RepresentationNotPreferred` の理由 | — | pure | 単体 | 各 +10〜30 |
| `Editor/Build/Materialization/SceneResourceContentMaterializer.cs` | graph snapshot の出力、付随 asset の候補・閉包・`Owner` tag、physical GUID の重複宣言の検出 | — | `SceneResourceMap`・`AttachedAssetDescription` | `SceneResourceContentMaterializerTests` 拡張（AC9） | 147 → 230〜260 |
| `Materialization/BuildMaterializationSnapshot.cs`・`AssetDatabaseGateway.cs` | `Graph` の保持、`GetDependencyHash` | — | Editor | 偽 gateway | 66 → 85、+10 |
| 新規 `Editor/Build/Content/BuildContentIdentity.cs` | 入力 identity の正準化と hash | 呼び出しごと | pure + gateway | 単体（AC7） | 0 → 90〜120 |
| `Editor/Build/Content/BuildContentCoordinator.cs`・`BuildContentReport.cs` | 入力 identity の計算、index による再利用、build mode、preflight への正準化文字列・時刻・mode | — | Editor I/O | 偽 adapter（AC8） | 145 → 210、97 → 125 |
| `Editor/Build/Content/UnityContentDirectoryAdapter.cs` | `BuildContentRoot.Declaration` / `InputIdentity` の書き込み、`Full` mode の workspace 消去 | — | Editor I/O | — | 166 → 180 |
| `Editor/Build/Content/ContentTransportPublisher.cs` | staging 名の短縮、manifest への `declaration` / `inputIdentity` | — | Editor I/O | path 長と manifest 往復の単体（AC14） | 108 → 125 |
| `Runtime/BuildContent/BuildContentRoot.cs`・`Distribution/ContentTransportManifest.cs`・`ContentManifestValidation.cs` | 宣言と入力 identity の運搬（任意 field。schema version は据え置き） | — | public（serialized） | `ContentManifestValidationTests` 拡張 | 88 → 112、133 → 152、293 → 300 |
| 新規 `Runtime/AssetDescriptions/AttachedAssetDescription.cs` | node が所有して子孫が読む付随 asset の宣言 | SceneResource に埋め込み | `AssetDescription`。public | — | 0 → 50 |
| `Runtime/SceneSystem/SceneResource.cs` | 付随 asset の列と検索 | — | public | EditMode（AC13） | 98 → 115 |
| `Runtime/SceneSystem/SceneBase.cs`・`SceneDirector.cs`・`SceneDirector.Loading.cs` | `LoadAttachedAssetAsync`（2 overload）、`SceneContentBinding` の注入 | Scene(identity) | `IAssetManagement`。protected 2 件、internal 1 件 | 単体（AC11・AC20） | 274 → 325、280 → 292、683 → 690 |
| `Runtime/AssetManagement/AssetManagement.cs`（+ `.ContentDirectory.cs`） | backend 無しの構成と、Addressables 経路の `InvalidOperationException` | App | internal の構成口 1 件 | 単体（AC19） | +40 |
| `Runtime/Bootstrap/AbstractApplicationInitializer.cs` | directory mode での構成の選択と起動ログ 1 行 | App | 既存 | EditMode（既存の起動テスト） | +15 |
| `Runtime/BuildContent/ContentDirectoryIndex.cs` | Object の Full fallback | — | internal | `ContentDirectoryIndexTests` | 129 → 131 |
| 新規 `SampleGame/DependOnAll/Editor/Build/SeasonSceneTagPolicy.cs` | node 水準の `Season`・`Role` tag | — | `CellCompanionRoleClassifier`（InternalsVisibleTo） | 単体 | 0 → 100〜130 |
| 新規 `SampleGame/DependOnAll/Editor/Build/SampleGameBuildPurposes.cs` | 6 件の宣言（runtime 既定値を含む） | — | pure | EditMode（AC4・AC5） | 0 → 90〜110 |
| `SampleGame/DependOnAll/Editor/Build/SampleGameContentBuild.cs` | 宣言ごとの menu、Play As、Build And Publish、Build And Verify Next Play、batch 入口 | — | Editor I/O | 操作（AC14〜AC16・AC23）、引数の単体 | 131 → 240〜270 |
| `SampleGame/DependOnAll/Editor/Build/ContentDeliveryWindow.cs`・`ContentDeliveryPlayBridge.cs` | From published directory、`ApplyDeclaration`（build 無し）、manifest の runtime 既定値の適用と Reset | — | Editor I/O | 単体（ledger の往復、AC21・AC22） | 168 → 200、98 → 160 |
| 削除 `SeasonSceneSelectionPolicy.cs`・`SeasonSceneSelectionPolicyTests.cs` | 置き換え | — | — | — | 243 → 0、181 → 0 |
| `SampleGame/DependOnAll/Editor/Build/SampleGamePlayerBuildCoordinator.cs` | `bs4-spring-full` 宣言の利用、RuntimeJson を宣言から写す | — | 既存 | `Bs4FixtureComposerTests`、AC17 | 145 → 155 |
| `SampleGame/InGame/.../World/LocalVfxEventTrigger.cs`・`MajorEventVfxAnchor.cs`・`LocalVfxEventBinder.cs`・`CellCompanionScene.cs`・`SeasonMajorEventScene.cs`・`SeasonScene.cs` | address → 付随 asset 名、`LoadAttachedAssetAsync` の利用 | 既存 | 既存 | AC12・AC13・AC20 | 各 ±5〜10 |
| `SampleGame/InGame/Properties/AssemblyInfo.cs` | `InternalsVisibleTo` 1 行 | — | — | — | +1 |
| asset（3 つの SceneResource、Scene 内の trigger / anchor の値） | 付随 asset の宣言 | — | — | AC13 | — |
| テスト（新規 `BuildPurposeResolverTests`、`BuildContentIdentityTests`、`SeasonSceneTagPolicyTests`、`SampleGameBuildPurposesTests`、`SceneBaseAttachedAssetTests`、`AssetManagementDirectoryModeTests`、`ContentDeliveryPlayBridgeTests`、`ContentBuildCommandLineTests`、既存の拡張） | — | — | — | — | 0 → 1,100〜1,500 |

- **警報と判断:** 合計は 500 行を超える。非分割の理由と分割の引き金は §2.6 U1。`SceneDirector.Loading.cs`（683 行）は注入 1 行の追加に留める。`BuildTagSelector` は既存の責務（照合）の拡張で、優先の規則は requirement の属性として置き、policy 判断を selector に足さない。`SampleGameContentBuild` は menu・Play As・publish・install・batch の入口を 1 file に持つことになるので、240 行を超えたら batch 入口を `SampleGameContentBuildCommandLine` に分ける（変更理由が別: 引数の解釈）。
- **配置:** 到達規則と宣言は `OneStarMaker.Build.Selection`（pure、Runtime に依存しない）。入力 identity は `OneStarMaker.Build.Content`（gateway に依存）。付随 asset の宣言と宣言の運搬は Runtime（§18 §3 の埋め込み型の方針、`BuildContentRoot` は既に serialized の運搬役）。SampleGame の語彙（Season、Role、宣言の表、環境変数名の規則、Play As）は `SampleGame.DependOnAll` / `.Editor`。新しい asmdef・依存 edge は無い。

## 4. 実装計画（A1）

### 4.1 順序

1. Selection: 宣言型、graph node、resolver、selector の拡張、単体テスト（AC1〜AC3・AC6）。
2. Materialization と入力 identity: graph snapshot、付随 asset の候補、`GetDependencyHash`、`BuildContentIdentity`、coordinator の index と再利用と build mode、publisher の staging 名、テスト（AC7〜AC9・AC14 の単体）。
3. SampleGame の policy と宣言の表、menu、BS4 の接続、テスト（AC4・AC5）。この時点で既存 4 build の選択が一致することを EditMode で確かめる。
4. Play As（D7）: `ApplyDeclaration` と menu、ledger の単体（AC21 の (a)）。ここで Addressables mode の Editor Play を 1 回軽く Play して AC16 の前提（環境変数 2 つで Planner と Whitebox が効く）を確かめる。
5. Runtime の付随 asset と fail-closed: `AttachedAssetDescription`、`SceneResource`、`SceneBase` / `SceneDirector`、index の fallback、`AssetManagement` の backend 無し構成、`AbstractApplicationInitializer`、テスト（AC10・AC11・AC19・AC20）。ここで U1 の引き金を確かめる。
6. 宣言の運搬: `BuildContentRoot`・manifest・validation・adapter・publisher、PlayBridge と Delivery 窓（AC21 の (b)、AC22 の単体）、BS4 の RuntimeJson。
7. SampleGame の Scene class と trigger / anchor、asset の宣言（Inspector または一時スクリプト）、Play テストの更新（AC12・AC13）。
8. 検証 Play の 1 操作と batch 入口（D8・D12）、引数の単体テスト（AC23）。
9. `pwsh tools/contract-audit.ps1`、Editor の compile 確認。Phase B の終わりに Addressables mode の Editor Play を 1 回軽く Play して AC12 の前提と D10 の起動ログを確かめる。Phase B は content build・検証 Play を実行しない。

### 4.2 Phase B から Phase A へ差し戻す条件

- U1 の Framework 公開面 5 点を超える公開 API、新しい状態・所有者・寿命、asmdef 参照が要る。
- 到達規則を pure に保てない（`SceneResourceMap` や AssetDatabase を resolver から読む必要が出る）。
- 既存 4 build の選択が `840b6609` の policy と一致しない原因が、宣言で表せない規則にある。
- 入力 identity の入力に `GetAssetDependencyHash` 以外の Editor 状態が要る。
- Object の Full fallback が既存 `ContentDirectorySessionTests` の契約を破る。
- directory mode で backend 無しにすると、`UICommon` の bootstrap や他の App 常駐の初期化が Addressables を要して起動できない（D10 の前提が崩れる）。
- 任意 field として足した `declaration` / `inputIdentity` を DIST の validation か `JsonUtility` が拒否し、version を上げる必要が出る。
- 環境変数 `SAMPLEGAME_ASSETS__SCENEVARIANT` と `SAMPLEGAME_WORLD__CELLCOMPANIONSET` だけでは Addressables mode の Play で Whitebox と Planner にならない（D7 の前提が崩れる）。

### 4.3 対象外を維持する方法

- transport・DIST・Storage の key と revision の形式を変えない（I9）。入力 identity は欄で運び、manifest の追加 field は読む側が無視できる形にする。
- Player の bootstrap・smoke・`Application.Quit` に触れない。RuntimeJson の値の出どころだけを宣言に替える。
- `AssetManagement` の Addressables 側の経路と `AssetKey` を変えない。backend 無しの構成を足すだけ。Play As は環境変数だけで、Runtime に新しい読み口を足さない。

## 5. テストとレビュー計画（A1）

- **単体テスト:** `BuildPurposeResolverTests`（AC1〜AC3・AC6）、`BuildTagSelectorTests` の拡張、`BuildContentIdentityTests`（AC7）、`SeasonSceneTagPolicyTests`、`SceneBaseAttachedAssetTests`（偽 `IAssetManagement`、AC11・AC20）、`AssetManagementDirectoryModeTests`（AC19）、`ContentDeliveryPlayBridgeTests`（AC21・AC22 の失敗経路）、`ContentBuildCommandLineTests`（AC23 の引数）、publisher の staging 名の path 長と manifest の往復（AC14）。
- **差し戻し中の起点 `-Filter`:** `BuildPurposeResolverTests`、`BuildTagSelectorTests`、`SceneResourceContentMaterializerTests`、`BuildContentIdentityTests`、`BuildContentCoordinatorTests`、`SampleGameBuildPurposesTests`、`SeasonSceneTagPolicyTests`、`ContentDirectoryIndexTests`、`ContentDirectorySessionTests`、`BuildContentDirectoryIntegrationTests`、`SceneBaseAttachedAssetTests`、`AssetManagementDirectoryModeTests`、`ContentManifestValidationTests`、`PlayerContentConfigurationTests`、`ContentDeliveryPlayBridgeTests`、`LocalVfxEventPlayTests`、`SeasonMajorEventPlayTests`、`Bs4FixtureComposerTests`。
- **判定必須テスト:** 最終の全 EditMode 回帰（空 filter）、PlayMode の `LocalVfxEventPlayTests`・`SeasonMajorEventPlayTests`・`VfxScenePlayTests`・Streaming の PlayMode（`-WithGraphics` を併用）。Content Directory build 2 件（`spring-full`、`spring-planner-whitebox`）を batch 入口（AC23）で行い、`preflight.json` を照合 script にかける（AC4・AC5）。同じ 2 宣言をもう 1 回 batch で起動して `reused=true` を確かめる（AC8 の実環境）。
- **全 EditMode 回帰の適用除外:** なし。
- **統合・Unity テスト:** `BuildContentDirectoryIntegrationTests` の拡張（AC10）、`Bs4FixtureComposerTests`、`ContentManifestValidationTests`・`ContentDeliveryFailureMatrixTests` の既存経路。
- **操作・実行時・目視条件の検証経路:**
  - AC14（担当: 動作確認の実行担当。環境: Editor、`spring-full` と `spring-planner-whitebox` の `Build And Publish` menu。観測: published directory の `transport.json` と `content/`。合否は Phase C が manifest の files と directory の照合で決める）。
  - AC15（担当: 同上。環境: `spring-full` の `Build And Verify Next Play` 1 操作の後、Play。観測: 起動ログの構成 1 行、(0,4) Stable、WASD で (4,2) へ移動し Events の trigger 位置へ入る、Console の Error の有無、VFX の表示。証拠: Console ログの保存）。
  - AC16（担当: 同上。環境: build せず `Play As spring-planner-whitebox` を選んで Play、次に `Play As/Reset` して Play。観測: 1 回目は (0,4) Stable、ロード済み一覧に `_Environment_` が無く Cell が Whitebox、Error の有無。2 回目は Environment が載る。証拠: Console ログ）。
  - AC22（担当: 同上。環境: Delivery 窓の From published directory に AC14 の path を入れて Prepare。観測: 埋まった field と Prepare の結果）。
- **未知の操作経路:** (1) `UICommon` の bootstrap や App 常駐の初期化が Addressables backend 無しで起動できるか。未確認。初回確認は Phase B の順序 5（担当: B）。不成立なら §4.2 で A へ返す。(2) `JsonUtility` と DIST の validation が任意 field を無視して受理するか。初回確認は B の順序 6 の単体テスト。(3) `AssetDatabase.GetAssetDependencyHash` が Scene と `.vfx` で安定した値を返すか。未確認。初回確認は B の単体テスト作成時。(4) 環境変数 2 つだけで Addressables mode の Play が Whitebox と Planner になるか。初回確認は B の順序 4。
- **人間の判断が必要な条件:** なし（AC15 の VFX の表示は Console のログ `LocalVfxEventSession` の起動記録で代替する）。
- **高コスト証拠:** BS4 Player build 1 回（AC17）。手順: ルート 96 文字以下の checkout で `Tools/OSM/Content/Build BS4 Spring Player` を実行し、Player を起動して smoke receipt を取る。独立した期待値: receipt が `completed`、Player 内 `content/` の `BuildContentRoot` の entry に `Spring_Events_4_2:Burst`・`Spring_Event_MultiCellProof:Burst`・`InGameSession:Lit` の `Object` entry がある（manifest の `Loadables` が 5）、`bs4-runtime.json` の `cellCompanionSet` と `representation` が宣言の値。判定 C → 暫定 C' → 高コスト証拠 → 最終証拠ラウンドの順（review-evidence の規則）。
- **判定 C の照合 script の方式:** A3 凍結 script（offline の pwsh）。入力は GO 候補 head で batch 入口が出した `preflight.json` 2 件と、A3 で凍結する期待値（宣言ごとの件数と sorted stable key 列の SHA-256。`840b6609` の policy で得た `spring-whitebox` 124 件に付随 asset 3 件を加えた集合、`spring-planner-whitebox` 70 件）。script は selected の stable key を sorted して件数と SHA-256 を比べ、`_Environment_` 等の禁止 pattern の不在と、`inputIdentity` が 64 hex であることを確かめる。期待値の stable key 列は A3 の前に `840b6609` の Editor で 5 宣言分の `preflight.json` を取って凍結する（担当: A3 の主担当。Editor 操作だけで Unity テストは伴わない）。
- **機械検査:** `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。
- **A0/A1 主担当・モデル・ベンダー:** この session の主担当（Anthropic 系）。
- **A2 独立レビューごとの観点・担当:** 未実施。観点は (a) アーキテクチャゲート（Framework / SampleGame、pure / I/O、公開面の最小性。特に D10・D11 が Runtime に足す面）、(b) 到達規則と不変条件を破る順序の探索（I1・I2・I7 を破る graph・filter・mode の例を 1 つ挙げられるか）、(c) 入力 identity と index の再利用の安全（AC8 の誤再利用の経路、build mode との分離）、(d) 実ユースケースの通し（§1.5 の UC1〜UC8 で欠ける手順が残っていないか、Play As と検証 Play の ledger の競合）。入力版は本 commit の内容 sha256 を正とする。
- **A3 統合担当・モデル・採否:** 未実施。
- **C' 用に予約した担当・モデル・ベンダー:** Phase A・B・C に関与しない系列を進行役が選び、ここに記録する。
- **独立性の強化条件を満たせない場合の理由:** 未確定。

## 6. Phase B 実装結果

未着手

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手
