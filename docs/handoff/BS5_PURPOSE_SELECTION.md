# BS5 目的からの最小選択

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `A`（A2 入力版 第 2 版。未凍結）
- branch: `claude/bs5-purpose-selection`
- implementation base commit: `840b6609`（`develop`、S-4d5a の PR #149 のマージ）
- implementation head commit: 未固定
- risk: `high`（Framework の選択 core・materializer・build identity と、Runtime の `AssetManagement` の backend 構成、`SceneResource` / `SceneBase` の公開面に触れる。既存の 5 つの content build 入口と BS4 Player coordinator が同じ経路を通る）
- owner: 発注者 / BS5 担当
- created: 2026-10-31
- expires: BS5 の Phase D 完了時
- harvest to: Architecture §18（§4 の選択 core・materialization・Content Directory build の現況、§5「Scene 以外のアセット種別」）、§20 §3（Editor Play の build → publish → install の手順）、§13（Object の表現 fallback、directory mode の fail-closed）、§04 §4.7 / §4.8（directory mode の backend 構成と宣言の既定値）、§24 §16.7 / §16.9 / §16.10（`.vfx` の経路と「address でロード」の記述）、S-4 program §6.3 の BS5 行と d5b 行
- workflow task: `bs5-purpose-selection`（未開始。最初に Evidence を固定する時点で `pwsh tools/workflow-task.ps1 start` する）
- Phase A snapshot の取得案内・状態: 未生成（凍結した HANDOFF 本体は A3 凍結 commit で固定する）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票: Phase A 作業中。本版は A2 の入力版（第 2 版。第 1 版 `e9f38744` に、実ユースケースの通し読みで見つけた 5 件と補正 1 件を足した）。A2 の独立レビューを統合した A3 版を作り、発注者が凍結を指示するまで Phase B に入らない。

---

## 1. 目的と対象外

- **目的:** 目的（Runtime が実際にロードする範囲）・設定（Unity の build 条件）・Variant（表現）の宣言から、必要十分な root 集合と閉包（Scene が動的にロードする付随 asset と、祖先が所有する共有 asset を含む）が決定的に導かれ、同じ宣言と同じ source からは同じ build identity が得られる BuildSystem にする。宣言は成果物に載って Runtime の既定値になり、Editor Play が Content Directory だけを読む fail-closed の mode で 1 操作で Play でき、同じ build を batch から起動できる。S-4d5b（Planner profile の Whitebox 上の Event 実証）の前提を満たす。
- **対象外:**
  - 格納の最小性（同じ bytes を revision や目的をまたいで一度だけ転送・格納する）。Unity の出力の決定性の測定と content-addressed な転送・格納は後続（§2.5）。本スライスは後続が測れるように build mode（§2.4 D13）と manifest hash の記録だけを残す。
  - 複数 Content Directory の同時登録と、共通 base と目的差分への層化。
  - InGame の Spring に入る Player の build 経路（BS4 の Player は Title の smoke の後に `Application.Quit(0)` する）。BS4 の経路は壊さないが、Player の動作は変えない。
  - 設定（target、compression、options、defines）を宣言で変える口。本スライスは値を固定したまま identity に含め、filter の tag の供給源になりうることだけを定める（D1）。
  - World Workspace の transaction への付随 asset の authoring の組み込み。
  - Addressables の退役、旧 Variant 経路の整理。Addressables mode の Editor Play は従来どおり。
  - §24 §16.7 の所有スライス未定の事項のうち本スライスの問いに関係しない事項。
- **現況:** §1.3（A0、`840b6609` で確認）。

### 1.1 Phase A の判断基準（発注者、2026-10-27。S-4 program §0 から転記）

- **主目的は BuildSystem。** VFX（S-4d）はそのつなぎ込みの題材である。各案で、成果物が content build・Player build の経路に乗るかと、Build 側の既知の課題（BS4 の Player は smoke の後に終了して InGame の Spring に入らない、transport staging の path が予算の外）を踏むかを書く。Build 側の課題は「やらない」で片付けず、BuildSystem の作業の候補として扱う。
- **残すコードは SampleGame に必要なものだけ。** 生成や authoring の道具を残すかは「SampleGame にそのコードが必要か」で決める。不要なら一時スクリプトで作り commit の前に消す。
- **checkout のルート。** Player を build するスライスは、ルートが 96 文字以下の checkout で作業する。この worktree のルートは 31 文字。本スライスの判定で BS4 Player build を高コスト証拠として 1 回取得する（§5）。
- **問いの立て方（発注者、2026-10-31）。** BuildSystem の問いは「目的別・設定別・Variant 別が絡み合い、なおかつ必要最小限になる Build 成果物ができ、必要最小限に格納される」こと、そして SampleGame に引っ張られない汎用的な System になること。本スライスは前半（選択の最小性）を答え、格納の最小性は後続に送る。
- **実ユースケースで通す（発注者、2026-10-31）。** 選択の規則だけでなく、企画者が Scene を直して Play するまで、Player が directory だけで動くこと、判定を人の操作なしに再現できることを、同じスライスで成り立たせる。証明の経路が Addressables に抜けていないことを先に確かめる。

### 1.2 S-4 program から転記した制約

- **SceneGraph（program §2.1）。** `InGameSession` の下に `Season_{Season}`（OnDemand）、その下に `{Season}_Lighting`・`{Season}_AtmosphereVFX`（NecessaryAlways）、`{Season}_Cell_{x}_{y}`（OnDemand、StreamByDistance）、`{Season}_Event_{slug}`（OnDemand）。Cell の下に `{Season}_Environment_{x}_{y}`（OnDemand、resident companion）、`{Season}_Lighting_{x}_{y}`・`{Season}_VFX_{x}_{y}`・`{Season}_Events_{x}_{y}`（OnDemand、optional companion）。
- **Planner Play（program §3.1 / §6.2）。** `content:representation=Whitebox` と `world:cellCompanionSet=Planner` を使い、Environment をロードしない状態で local / major Event を実証する。companion set は Cell の companion だけを選別し、Season の子（Lighting・Atmosphere・Event）は対象外。
- **d5b の行（program §6.3）。** 前提は BS5 の Phase D 完了。
- **VFX の寿命（program §2.4、Architecture §24 §16.9 / §16.10）。** 一時 VFX の source asset は Scene が `AssetOwner.Scene(identity)` で所有してロードし、Scene の unload で VFX を回収してから asset を解放する。本スライスは所有者と順序を変えず、asset の指し方だけを変える（§2.4 D5）。
- **公開 API と依存（program §10）。** Framework は season 名、Cell 座標文法、職種 role を知らない。Game → Framework の依存方向を維持する。`SceneState` の 14 値と `LoadType` を変えない。
- **検証方針（program §11）。** `unity test` / `unity run`、Scene / asset YAML の手編集は禁止。Phase B は Editor 操作・compile 確認まで、Unity テストと Build は Phase C。

### 1.3 A0 の現況（`840b6609`）

**build の経路。** source → `SceneResourceContentMaterializer`（`SceneResourceMap` の Scene payload だけを候補にする）→ `SeasonSceneSelectionPolicy`（季節・表現の request と requirement）→ `BuildTagSelector`（pure）→ bootstrap composer（UICommon と `SceneResourceMap.asset` を固定 content として合成）→ `BuildContentProjection`（閉包の照合）→ `UnityContentDirectoryAdapter`（`BuildContentRoot` を生成して `BuildPipeline.BuildContentDirectory`。workspace は `work/<target>/<contentSet>` を再利用するので Unity 側は incremental）→ `BuildContentCoordinator`（workspace を丸ごと `artifacts/bs2b/content/<identity>/` へ複写して公開）→ `ContentTransportPublisher.Publish`（BS4 だけが呼ぶ）→ DIST install → Editor Play（Delivery 窓の Use For Next Play）または BS4 Player。

**選択 core（`BuildTagSelector`、302 行、pure）。** 候補は `StableKey / LogicalKey / PhysicalKey`。request は dimension ごとの OR、dimension 間の AND。候補の tag が request に無い dimension なら `UnrequestedDimension`、値が選ばれていなければ `ValueNotSelected` で除外。tag の無い dimension は中立。requirement は logical key ごとの `ExactlyOne` / `OneOrMore` で、選ばれた候補の数だけを数える。**requirement の無い logical key の候補を除外する手段は無い。**

**季節 policy（`SeasonSceneSelectionPolicy`、243 行、SampleGame）。** graph を複製して `Season` を祖先から導き、in-scope の全 node を requirement にする（最小ではなく季節の全部）。Whitebox 選択は `SeasonalMode` tag（切替可能な group にだけ付ける）で Full 専用の補助 Scene を残す。候補と node の有無が一致しないと "Payload/candidate mismatch" で止める。`FullAndWhitebox` は `OneOrMore` と `VerifyDual` で両表現を要求する。

**目的の不在。** 目的に相当するのは runtime の `CellCompanionSet`（Full / Planner / Lighting / Vfx）と `CellCompanionRoleClassifier`（identity の末尾 3 token 目で Environment / Lighting / VFX / Events を判定。`SampleGame.InGame` の internal）だけで、build 側に dimension が無い。Planner 用の Whitebox build は Planner が読まない Environment 54 件・Cell Lighting 2 件・Cell VFX 1 件を含む。逆に、Planner 用の directory を companion set Full で Play すると Environment の Add が 54 回失敗して Warning になり（`SessionCellCompanionLoadDriver` は失敗を Warning で飲む）、representation を Full にすると Cell の Whitebox は Full へ fallback しないので Scene が欠ける。**directory は自分が何の目的で作られたかを持っていない。**

**実測の graph（`SceneResourceMap`、457 node）。** OutGame: `OutGameScene`（NecessaryAlways、root）、`Title`・`HpGauge`・`OutGameSession`（OnDemand）、`ConfirmDialog`（HpGauge の子）、`HomeScene`（OutGameSession の子、payload 無し）。InGame: `InGameScene`（root、payload 無し）、`InGameSession`（NecessaryAlways、payload 無し）、`InGameUI`・`PlayerScene`（NecessaryAlways）、`Result`（OnDemand）、`Season_*` 4 件（OnDemand、payload 無し）。各 Season: `{S}_Lighting`（NecessaryAlways）、Spring だけ `Spring_AtmosphereVFX`（NecessaryAlways）と `Spring_Event_MultiCellProof`（OnDemand）、Cell 54 件（OnDemand、Variant は空と Whitebox）。各 Cell: Environment 1 件、Spring の (4,2)(5,2) に Lighting、(4,2) に VFX と Events。`Tunnel` は無い。

**実測の成果物（2026-10-30 の `spring-whitebox`）。** selected 124 件 = Scene payload 122 件 + bootstrap 2 件。Unity の出力は hash 名の `.cf` / `.resS` と hash 名の manifest `.json`、固定名 `BuildManifestHash.txt`、計 233 file・6.2 MB。manifest は `RootAssets 1 / Loadables 2 / LoadableScenes 123 / SerializedFiles 196`。閉包は 608 file。on-disk の content build は 1 件だけで、再 build の bytes 比較はできていない。

**build identity。** `DateTime.UtcNow + Guid` で、入力から導出されない。同じ入力の build は別 identity、別 directory になる。`outcome.json` は identity・path・summary だけで、選択と閉包は `preflight.json` が正本。transport の `revision` は identity、manifest の SHA-256 は install の照合に使う。

**閉包と動的ロード。** `AssetDependencySnapshotBuilder` が `AssetDatabase.GetDependencies` で root ごとに閉包を取る。Scene 内に置いた `.vfx`（`Spring_AtmosphereVFX`・`Spring_VFX_4_2`）は閉包で同梱される。**address で動的にロードする asset は 3 箇所ある:** `Spring_Events_4_2_Burst.vfx`（`CellCompanionScene`、所有者 `Scene(Events)`）、`Spring_Event_MultiCellProof_Burst.vfx`（`SeasonMajorEventScene`、所有者 `Scene(Event)`）、**`DemoCellLit.mat`（`SeasonScene.OnPreLoadedImpl`、4 つの Season が同じ material を各 `Scene(Season)` 所有でロード）**。material は Cell の閉包で directory に入っているが、key で引ける `Object` entry ではない。`.vfx` 2 件は閉包にも入らない。`BuildContentRoot` の `Object` entry を本番 build で埋める経路は bootstrap composer（`SceneResourceMap.asset`）と BS4 の `Bs4FixtureComposer`（probe prefab）だけで、どちらも選択 policy を迂回する合成。

**Runtime の backend 構成。** `AbstractApplicationInitializer` は mode に関係なく `new AssetManagement()` を作り、これは常に `AddressableBackend` を持つ。Addressables の Play Mode script は既定（AssetDatabase 直読み）。**したがって directory mode の Editor Play でも `AssetKey.FromAddress` のロードは AssetDatabase から静かに成功する。** Editor の directory Play は「directory だけで動く」ことを証明しない。Player では Addressables の catalog を build しない（`DoNotBuildWithPlayer`）ので、BS4 Player は Season に入った時点で `DemoCellLit.mat` のロードで落ちる見込み（Title の smoke で止まるので顕在化していない）。

**Runtime の読み口。** `SceneBase.LoadSceneScopedAssetAsync(AssetKey)` だけがあり、directory の `IAssetManagement.LoadContentAssetAsync(logicalKey, representation, owner)` は Scene から届かない。`ContentDirectoryIndex` の Object 解決には Scene にある Full fallback が無い。`SceneDirector` は `_useContentDirectory` と `_sceneVariant` で Scene のロードを分岐し、`SceneBase` には `BindAssets(IAssetManagement)` だけを注入する。`AssetManagement` は canonical key で backend の asset を共有し、所有者ごとに参照を数える。

**Editor Play の publish と install。** `Tools/OSM/Content/Build ...` 4 つは Content Directory を生成するだけ。`ContentTransportPublisher.Publish` を Editor Play 向けに呼ぶ入口は無く、§20 / §4.8 / SEASON design が「未整備」と書く。Delivery 窓は source・manifest SHA-256・contentSet・revision・cache root・representation・budget を手で入れて Prepare → Use For Next Play する（`transport.json` は contentSet と revision を持つが窓は読まない）。`ContentDeliveryPlayBridge` は `SAMPLEGAME_CONTENT__*` の環境変数 5 つを ledger で管理し、`world:cellCompanionSet` は扱わない。publisher の staging は `<final>.<guid32>.staging` で、BS4 では repo 相対 163 文字になり、ルートが 97 文字以上の checkout で `DirectoryNotFoundException` になる（Harness README）。

**識別子の形式の制約。** Runtime の `ContentManifestValidation` は `revision` を安全な path segment としてだけ検査し（32 hex は通る）、manifest の `version == 1`・`rootSchemaVersion == 2` を固定で要求する（`JsonUtility` は未知の field を無視するので、追加 field は version を変えずに運べる）。一方 **tools/Artifacts は revision を `[0-9]{8}T[0-9]{9}Z-[0-9a-f]{32}` の正規表現で 4 箇所が固定している**（`ContentContract.psm1` の `Assert-ContentRevision`、`ContentCatalog.psm1` の revision 列挙、`BuildContract.psm1` の identity 解析、`Transport/R2ArtifactTransport.psm1` と `ArtifactReadback.ps1` の key の allow-list）。宣言から導いた 32 hex の identity は、このままでは `content collect` と `build collect` が拒否する。Storage program は「完了（保留 list 付き）」で、再開条件 RC1 は「エンジン側スライスが Storage の不足で実際に止まった」。

**Unity の成果物の内部構造（P2 の入力）。** Unity の manifest `.json` は `SerializedFiles` ごとに `ContentHash` と `SerializedFileDependencies` を持ち、Scene は `SerializedFile` の index で file を指す。`spring-whitebox` では non-builtin 195 file、Scene 1 件の推移的な file 閉包は平均 7.3・最大 37、50 Scene 以上から共有される file は 6 件。file 単位の content hash と依存 graph は Unity 側に既にある。DIST の install は revision を丸ごと staging へ複写して rename し、HTTP source は file ごとに stream で取る（resume・部分取得は無い）。

**batch の入口。** content build を起動できるのは menu だけ。`-executeMethod` の入口は退役済みの `VariantRemoteBuildBatch` しか無い。`tools/unity-editor.ps1` は Editor の起動・状態確認を持つ。

**既存テスト。** `BuildTagSelectorTests`（481）、`SceneResourceContentMaterializerTests`（431）、`BuildContentProjectionTests`（111）、`SeasonSceneSelectionPolicyTests`（181）、`ContentDirectorySessionTests`（763）、`ContentDirectoryIndexTests`（83）、`BuildContentDirectoryIntegrationTests`（490、SampleGame、`TestResults/bs3-fixture` へ実 directory を build する）、`Bs4FixtureComposerTests`、`LocalVfxEventPlayTests`、`SeasonMajorEventPlayTests`（394 + 343）、`PlayerContentConfigurationTests`、`ContentDeliveryFailureMatrixTests`。

### 1.4 成果物を参照する者（A0）

本スライスが変える識別子と、それを読む者を固定する。後続の格納の最小性は、この参照の形を変えずに bytes の層を足す。

| 読む者 | 何を | 何で指すか |
|---|---|---|
| Editor Play（人） | published directory → DIST cache の installed revision | published path（同一 PC）。revision は宣言から導いた identity、照合は manifest SHA-256 |
| Player | Player 内 `content/` または `content:installedRevisionPath` + `content:manifestSha256` の pair | baked config の identity / 明示の pair |
| Runtime（Scene） | `BuildContentRoot` の entry | logical key（Scene identity、`<owner>:<name>` の付随 asset）と representation |
| transport / DIST | revision の file 集合 | manifest（`transport.json`）の SHA-256、file ごとの path・size・SHA-256 |
| Storage（`content collect`） | revision 1 件 | `<contentSet>/<target>/<revision>` |
| Phase C / batch | `preflight.json`・`outcome.json`・published directory | identity（宣言から決定的） |

---

## 2. 意思決定と受け入れ境界（A1）

- **このスライスが答える問い:** 目的・設定・Variant の宣言から、必要十分な root 集合と閉包（動的ロードする付随 asset と祖先が所有する共有 asset を含む）が決定的に導かれ、同じ宣言と同じ source からは同じ build identity が得られ、その成果物だけで Editor Play と Player が動くか。

### 2.1 不変条件（起きてはいけないこと）

- **I1（必要なものを落とさない）:** 宣言の root から、`NecessaryAlways` / `IncrementalAlways` の子を全て、`OnDemand` の子を目的の filter が通すものだけたどって到達する node は、payload があれば必ず選ばれる。到達する node の付随 asset も必ず選ばれる。
- **I2（不要なものを入れない）:** 到達しない node の候補と、到達しない node の付随 asset は選ばれない。表現の優先順で選ばれなかった候補も選ばれない。
- **I3（決定的）:** 同じ宣言と同じ source からは、同じ選択（stable key の集合）、同じ閉包、同じ identity が得られる。候補・provider・node の入力順と build mode（§2.4 D13）に依存しない。
- **I4（既存の成果物を変えない）:** 既存 4 つの content build と BS4 の `bs4-spring-full` は、新しい宣言に置き換えた後も selected の stable key 集合が `840b6609` の policy と一致する（付随 asset の追加分を除く）。
- **I5（Framework は語彙を知らない）:** Framework の選択・materialization・identity・Runtime の読み口・宣言の運搬は、season 名・Cell 座標文法・職種 role・`CellCompanionSet` を参照しない。宣言の runtime 値は Framework にとって不透明な key=value。Game → Framework の依存方向を維持する。
- **I6（寿命と順序を変えない）:** 付随 asset は従来どおり読む Scene が `AssetOwner.Scene(identity)` で所有し、VFX の回収 → asset の解放の順序、d4b / d5a の既存テストの結果を変えない。`SceneState`、`LoadType`、asmdef 参照を変えない。
- **I7（directory の外を読まない）:** directory mode の Editor Play と Player は、Addressables backend からロードしない。`AssetKey.FromAddress` / `FromDescription` によるロードは directory mode では失敗する。Addressables mode の Editor Play は従来どおり動く。

### 2.2 成立条件（起きなければならないこと）

- **E1（宣言）:** 目的・設定・Variant と runtime の既定値を 1 つの宣言型で表せ、SampleGame の 5 つの content set（`all-full`、`spring-full`、`spring-whitebox`、`spring-full-whitebox`、`bs4-spring-full`）と新しい `spring-planner-whitebox` が宣言として存在する。
- **E2（最小選択）:** `spring-planner-whitebox` の選択は、Spring の Cell 54 件（Whitebox）、`Spring_Events_4_2`、`Spring_Event_MultiCellProof`、`Spring_Lighting`、`Spring_AtmosphereVFX`、InGame / OutGame の到達 Scene、bootstrap 2 件、付随 asset 3 件で、Environment・Cell Lighting・Cell VFX・他季節を含まない（§2.3 AC4 の件数）。
- **E3（付随 asset と共有 asset）:** `Spring_Events_4_2` と `Spring_Event_MultiCellProof` の `.vfx`、`InGameSession` が所有する `DemoCellLit.mat` が、所有 node が到達する全ての宣言の Content Directory に `Object` entry として入り、読む Scene から所有者と名前で解決できる。Addressables mode の Editor Play でも同じ呼び出しで解決できる。
- **E4（identity）:** identity は宣言と選択済み候補の source 状態から計算され、同じ入力の 2 回目の build は build を実行せず既存の成果物を返す。
- **E5（宣言の運搬）:** 宣言の名前・表現の主値・runtime の key=value が `BuildContentRoot` と `transport.json` に載り、Editor Play の Use For Next Play と Player の baked config がそれを既定値にする。
- **E6（1 操作の loop）:** 宣言ごとの「Build And Use For Next Play」1 操作で、content build → transport publish → DIST install → Use For Next Play まで進み、そのまま Play できる。Delivery 窓は published directory から manifest SHA-256・contentSet・revision を読める。publish の staging path は path 予算の範囲に収まる。
- **E7（fail-closed の実証）:** directory mode の Editor Play で、`spring-full` の directory から Cell (4,2) の Events の trigger の VFX と Season の Lit が解決され、Error ログが出ない。同じ Play で Addressables backend は作られていない。
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
| AC6 | 候補・provider・node を逆順・シャッフルで与えても、plan の selected / excluded の列と identity が同じ。build mode を `Full` / `Incremental` に変えても identity は同じ | I3 | 単体 |
| AC7 | identity は宣言（名前・root・filter・表現の優先順・設定・runtime の既定値）と、選ばれた候補の stable key・physical GUID・source の依存 hash の正準化文字列の SHA-256 から作られる。候補の依存 hash が 1 つ変わる、宣言の filter や runtime の既定値が変わる、設定が変わる、のどれでも identity が変わる。順序の違いでは変わらない | I3・E4 | 単体（偽 gateway） |
| AC8 | `content/<identity>/` が既に在れば coordinator は adapter を呼ばず、`outcome.json` に `reused=true` と既存 path を書いて成功を返す。staging だけが残る中途状態では再利用しない | E4 | EditMode（偽 adapter） |
| AC9 | materializer が任意の node（payload の無い node を含む）の付随 asset から候補（logical key `<identity>:<name>`、Representation は payload の Variant、`Owner` tag に所有 node の identity）と閉包を作る。名前の重複、参照欠損、同じ physical GUID を 2 つの node が宣言する、所有 node の identity と衝突する名前、は issue で止まる | E3 | EditMode（既存 materializer テストと同じ偽 gateway） |
| AC10 | 合成 fixture directory（`TestResults/bs3-fixture`）に Scene 1 件と付随 `Object` 1 件（Full）を build し、representation `Whitebox` で Scene を登録した session から付随 asset が Full fallback で解決・ロード・解放できる。存在しない名前は `EntryMissing`。同じ entry を 2 つの所有者で取得して片方を解放しても、もう片方は有効 | E3 | EditMode（既存 integration テストの拡張） |
| AC11 | `SceneBase.LoadAttachedAssetAsync<T>(name)` と `LoadAttachedAssetAsync<T>(ownerIdentity, name)` は、directory モードでは `LoadContentAssetAsync(<owner>:<name>, sceneVariant, Scene(自分の identity))`、それ以外では `LoadAssetAsync(AssetKey.FromDescription(所有 node の付随 desc, sceneVariant), Scene(自分の identity))` を呼ぶ。未宣言の名前・未知の owner は `ArgumentException`。所有者は常に読む Scene | E3・I6 | 単体（偽 `IAssetManagement`） |
| AC12 | 既存の `LocalVfxEventPlayTests` と `SeasonMajorEventPlayTests` が、trigger / anchor の address を付随 asset の名前に替えた後も通る（Addressables モード、`-WithGraphics`） | I6 | Play |
| AC13 | `Spring_Events_4_2`・`Spring_Event_MultiCellProof` の `SceneResource` に付随 asset `Burst`、`InGameSession` に付随 asset `Lit`（`DemoCellLit.mat`）が宣言され、Variant は空。trigger / anchor の名前が `Burst`、`SeasonScene` が `InGameSession:Lit` を読む | E3 | EditMode |
| AC14 | `Build And Publish` の後、published directory に `transport.json` と `content/` があり、manifest の files が directory の全 file と size・SHA-256 で一致し、manifest に宣言の名前・表現の主値・runtime の key=value が入る。staging path の repo 相対長が `tools/path-budget.json` の上限以内 | E5・E6 | 操作（動作確認）+ EditMode（path 長の単体、manifest の往復の単体） |
| AC15 | `spring-full` の `Build And Use For Next Play` 1 操作の後、そのまま Play して Spring (0,4) が Stable になり、Cell (4,2) へ移動して Events の trigger に入ると VFX が起動する。Console に Error が無く、Addressables backend の生成ログが無い（AC19 の guard が有効な mode で動いている） | E6・E7 | 操作（動作確認）、証拠は Console ログと `preflight.json` |
| AC16 | `spring-planner-whitebox` の `Build And Use For Next Play` の後、環境変数を手で足さずに Play して Spring (0,4) が Stable になり、Environment が 1 件もロードされず、Error が無い（companion set と representation は宣言から既定値になる）。Event の動作は d5b で確かめる | E2・E5 | 操作（動作確認） |
| AC17 | BS4 の `Build BS4 Spring Player` が成功し、Player の smoke receipt が `completed`。Player 内 `content/` に付随 asset 3 件の `Object` entry が入る（manifest の `Loadables` が 2 + 3 = 5） | I4・E3 | 高コスト証拠（§5） |
| AC18 | 最終の全 EditMode 回帰と、Streaming・Vfx の PlayMode が通る | I6 | 回帰 |
| AC19 | directory mode で構成した `AssetManagement` は Addressables backend を持たず、`LoadAssetAsync(AssetKey.FromAddress(...))` と `LoadSceneAsync(desc)` は `InvalidOperationException` で失敗する。directory の `LoadContentAssetAsync` / `LoadContentSceneAsync` は動く。Addressables mode の構成は従来どおり | I7 | 単体 + EditMode（既存 `ContentDirectorySessionTests` の構成） |
| AC20 | `InGameSession` の付随 asset `Lit` は候補 1 件で、4 つの `SeasonScene` が `LoadAttachedAssetAsync("InGameSession", "Lit")` で同じ backend asset を各 `Scene(Season)` 所有で取得し、1 つの Season を unload しても他の Season の handle は有効 | E3・I6 | 単体（偽 `IAssetManagement` で呼び出しの形）+ AC10（共有の解放） |
| AC21 | `ContentDeliveryPlayBridge.Apply` が published directory の manifest から宣言の runtime の key=value（例 `SAMPLEGAME_WORLD__CELLCOMPANIONSET=Planner`）と表現の主値を ledger に入れ、`Reset` で元へ戻す。Framework 側は key を解釈せず、`SAMPLEGAME_` prefix 等の変換規則は Game 側に置く。Player の `PlayerContentConfiguration.Resolve` は baked config に無い key を宣言から補わない（Player の既定値は BS4 の RuntimeJson が宣言から写す） | E5・I5 | 単体（ledger の往復）+ `PlayerContentConfigurationTests` |
| AC22 | Delivery 窓の「From published directory」で、path 1 つから manifest SHA-256・contentSet・revision・representation が埋まり、Prepare → Use For Next Play が成功する。manifest が無い path・SHA-256 の不一致は失敗で、ledger を変えない | E6 | 操作 + 単体（読み取りの失敗経路） |
| AC23 | `Unity.exe -batchmode -quit -executeMethod <入口> -osmPurpose spring-full -osmPublish` が content build と publish を行い、成功で 0、宣言名の誤り・build 失敗で非 0 を返し、`preflight.json` / `outcome.json` / published directory の path を log に出す。menu と同じ coordinator を通る | E8 | EditMode（引数の解釈の単体）+ 判定 C での実行 |

AC4 の読み方: 「Role ∈ {...} を含まない」は selected の logical key に `_Environment_`・`_Lighting_{x}_{y}`・`_VFX_{x}_{y}` の Cell companion が無いこと。`Spring_Lighting`（Season の NecessaryAlways）は含む。合格例: 70 件で Environment 0 件。不合格例: Environment が 1 件でも入る、Cell が 53 件、`InGameSession:Lit` が無い。

AC8 の読み方: 「中途状態」は `staging/<identity>` だけが在って `content/<identity>` が無い状態。合格例: 既存の `content/<identity>` が在れば adapter は呼ばれない。不合格例: staging の残骸を成功として返す。

AC15 の読み方: 「guard が有効」は `content:runtimeMode=directory` の Play で `AssetManagement` が Addressables backend 無しで構成されたことを起動ログ 1 行で示す。合格例: その行があり、Lit と VFX が解決される。不合格例: その行が無い（Addressables から読めてしまう）。

### 2.4 A1 の設計

- **D1（宣言型）:** `BuildPurposeDeclaration`（`OneStarMaker.Build.Selection`、pure）。`Name`（contentSet と同じ規則の ASCII）、`Roots`（logical key の列）、`Filter`（`BuildRequest`。OnDemand の子に適用する tag の述語）、`Representation`（優先順の列と `First` / `All`）、`Configuration`（target、compression、options、defines、Unity version、root schema version の正準化した key=value 列。本スライスでは adapter の定数と Editor の値を写すだけ）、`RuntimeDefaults`（Game が決める不透明な key=value 列。例 `world:cellCompanionSet=Planner`）。Framework は値の意味を知らない。設定の値は Game の tag policy が node 水準の tag（例 `Platform`）に写して filter の入力にもできる。
- **D2（到達規則）:** `BuildPurposeResolver`（pure）。入力は graph node（identity、parent、children、LoadType、payload の有無、付随 asset の名前列）、node ごとの tag、候補と candidate tag provider、宣言。出力は requirement（到達 node の payload と付随 asset。`First` なら `ExactlyOne`、`All` なら `OneOrMore`）、Representation の request、到達しない候補と優先順に無い表現の候補の除外。規則: `required(n) ⇔ n ∈ roots ∨ (required(parent) ∧ (LoadType ∈ {NecessaryAlways, IncrementalAlways} ∨ filter(tags(n))))`。filter の意味は既存 `BuildTagSelector` の request の意味と同じ。payload の無い中間 node（`InGameSession`、`Season_*`）も到達の経路になり、付随 asset を持てる。graph の検証は既存 `SeasonSceneSelectionPolicy.ValidateGraph` の規則を Framework へ移す。
- **D3（選択 core の拡張）:** `BuildSelectionPolicy` に `ExcludeUnrequired`（requirement の無い logical key の候補を `BuildExclusionReasonCode.Unreachable` で除外）と、`BuildContentRequirement` に表現の優先（dimension、順序付き値、`First` / `All`）を足す。`SeasonalMode` tag と `VerifyDual` は不要になり、SampleGame の policy から消す。tag を node 水準（Season、Role）と candidate 水準（Representation）に分け、node 水準は Game が供給する。
- **D4（identity）:** `BuildContentIdentity`（`OneStarMaker.Build.Content`、pure + gateway）。正準化文字列は、宣言の各欄（sorted、`RuntimeDefaults` を含む）、設定（sorted key=value）、選ばれた候補ごとの `stableKey|physicalGuid|dependencyHash`（stable key で sorted）。`dependencyHash` は `IAssetDatabaseGateway.GetDependencyHash(path)`（`AssetDatabase.GetAssetDependencyHash`）。SHA-256 の先頭 32 hex を identity（directory 名、`BuildContentRoot.BuildIdentity`、transport の `revision`）にし、完全な 64 hex と正準化文字列を `preflight.json` に残す。coordinator は `content/<identity>` が在れば adapter を呼ばず再利用する（AC8）。build mode（D13）と時刻は `preflight.json` の metadata に残すだけで identity に入れない。既知の限界: project settings（Graphics API、Quality、Lightmap 設定）と生成 `BuildContentRoot.asset` の GUID は identity に入らない。後者は再 build の bytes を変えうるので P2 の spike の入力（§2.5）。
- **D5（付随 asset と共有 asset）:** Runtime に `AttachedAssetDescription : AssetDescription`（`Name` と `AssetPayload` の列。`[Serializable]`、埋め込み）を足し、`SceneResource` に `_attachedAssets` と `AttachedAssets` / `FindAttachedAsset(name)` を足す。payload の無い node（`InGameSession`）も宣言できる。materializer は付随 asset ごとに候補（logical key `<identity>:<name>`、stable key は既存の `osm-build-candidate-v1` 規則、Representation は Variant から既存規則で写す、`Owner` tag に所有 identity、provenance `SceneResourceMap.AttachedAsset`）と閉包を作る。同じ physical GUID は 1 つの node だけが宣言できる（共有は祖先に 1 回宣言し、子孫が所有者を指定して読む）。`SceneBase` に `LoadAttachedAssetAsync<T>(string name, ct)` と `LoadAttachedAssetAsync<T>(string ownerIdentity, string name, ct)` を足し、`SceneDirector` が `BindAssets` と同じ時点で `useContentDirectory`・`sceneVariant`・`SceneResourceMap` の検索口を `SceneContentBinding`（internal の値型）として注入する。directory モードは `LoadContentAssetAsync(<owner>:<name>, sceneVariant, Scene(自分))`、それ以外は `LoadAssetAsync(AssetKey.FromDescription(desc, sceneVariant), Scene(自分))`。所有者は常に読む Scene なので、共有 asset は `AssetManagement` の canonical key の共有と所有者ごとの参照で従来どおり数えられる。`ContentDirectoryIndex.TryResolveObject` に Scene と同じ Full fallback を与える。trigger / anchor の `_vfxAddress` は `_vfxAsset`（付随 asset の名前）に替え、`CellCompanionScene` / `SeasonMajorEventScene` は `LoadAttachedAssetAsync(name)`、`SeasonScene` は `LoadAttachedAssetAsync("InGameSession", "Lit")` を呼ぶ。`.vfx` と material の Addressables entry は Addressables モードの `AssetReference` が要するので残す。所有者・回収順序・ロード失敗の扱いは変えない。
- **D6（SampleGame の policy）:** `SeasonSceneSelectionPolicy` を、node 水準の tag を導く `SeasonSceneTagPolicy`（`Season` は祖先から、`Role` は `CellCompanionRoleClassifier` で。classifier は `InternalsVisibleTo("SampleGame.DependOnAll.Editor")` で参照し、規則を複製しない）と、宣言の表 `SampleGameBuildPurposes`（6 件。root は `OutGameScene`・`InGameScene`、filter は `Season` と `Role`、表現の優先順、`RuntimeDefaults` に `world:cellCompanionSet` と `content:representation`）に置き換える。`SampleGameContentBuild` は宣言を引数に取り、menu は宣言ごとの `Build`・`Build And Publish`・`Build And Use For Next Play` にする。bootstrap composer と BS4 の fixture composer は resolver の後に従来どおり合成する。
- **D7（publish 入口と 1 操作の loop）:** `SampleGameContentBuild.BuildAndPublish(declaration)` が coordinator の結果を `ContentTransportPublisher.Publish(result, <artifactsRoot>/published/<identity>)` へ渡す。`BuildAndUseForNextPlay(declaration)` はさらに既定の cache root（Delivery 窓の保存値、無ければ `UserSettings` 配下）へ `ContentInstaller` で install し、`ContentDeliveryPlayBridge.Apply` を呼ぶ。Delivery 窓に「From published directory」を足し、`transport.json` から manifest SHA-256・contentSet・revision・表現の主値を埋める（同一 PC の自分の成果物を指す用途。別 PC から運んだ場合も manifest の bytes を照合するので安全性は従来と同じ）。publisher の staging は `<final>.<8 hex>.tmp` にし、identity の短縮と合わせて repo 相対長を予算内にする。
- **D8（authoring）:** 3 つの `SceneResource`（`Spring_Events_4_2`・`Spring_Event_MultiCellProof`・`InGameSession`）への付随 asset の宣言と trigger / anchor の名前は、Phase B が Editor の Inspector または一時スクリプトで行い、一時スクリプトは commit の前に消す。World Workspace の transaction は変えない。
- **D9（directory mode の fail-closed）:** `AssetManagement` に backend 無しの構成（`AssetManagement.ForContentDirectory()` または `IAddressableBackend?` を受ける internal constructor）を足し、`AbstractApplicationInitializer` は `ReadContentDirectoryConfiguration` が directory を返した時だけそれを使う。backend 無しの `LoadAssetAsync` / `LoadSceneAsync` / `LoadAppAssetSync` は `InvalidOperationException`（「directory mode では Addressables を使わない」）で失敗し、`CreateVfxBackend` 等の他の初期化は変えない。起動ログに構成を 1 行出す（AC15 の読み方）。Addressables mode と Player の BS4 以外の経路は変えない。`UICommon` の bootstrap が Addressables を要する経路が残っていれば B は止めて A へ返す（§4.2）。
- **D10（宣言の運搬）:** `BuildContentRoot` に `Declaration`（name、representation の主値、runtime の key=value 列）を任意の serialized field として足し、adapter が宣言から写す。`ContentTransportManifest` に同じ `declaration` を任意 field として足す。schema version（root 2、manifest 1）は変えず、宣言の無い root / manifest は従来どおり既定値なしとして扱う（既存の installed revision の再検証を壊さない。`JsonUtility` は未知 field を無視する）。`ContentDeliveryPlayBridge.Apply` は manifest の runtime の key=value を環境変数へ写す。Framework は `content:*` 以外の key を解釈せず、Game の key を環境変数名に変換する規則（`SAMPLEGAME_` + `__` 区切り）は `AppInitializer` 側の既存規則に合わせる。BS4 の `RuntimeJson` は `bs4-spring-full` 宣言の `RuntimeDefaults` から `cellCompanionSet` と representation を写す（手書きの定数をやめる）。
- **D11（batch 入口）:** `SampleGameContentBuild.BuildFromCommandLine()`（`-executeMethod` の対象）。引数は `-osmPurpose <name>`、`-osmPublish`、`-osmBuildMode Full|Incremental`（既定 Incremental）。menu と同じ `BuildResult` を通り、終了コードは成功 0、引数不正 2、build 失敗 1。log に `preflight.json` / `outcome.json` / published directory の path を出す。`tools/unity-editor.ps1` の既存の起動経路から呼べる wrapper は Phase B の判断で足し、無ければ README にコマンド行を書く。
- **D12（既存 build 入口）:** 既存 4 つの menu は宣言ごとの `Build` に置き換え、名前（contentSet）は変えない。`bs4-spring-full` は BS4 coordinator が宣言を引数に呼ぶ。
- **D13（build mode）:** `BuildContentRequest` に `BuildMode`（`Incremental` = workspace を再利用、`Full` = workspace を空にしてから build）を足す。identity には入れず `preflight.json` に記録する。本スライスは `Incremental` を既定にし、`Full` は P2 の決定性の spike が「同じ identity の Incremental と Full の出力が一致するか」を測るための入口。

### 2.5 ここでは答えない問いと所有する後続

| 問い | 所有 |
|---|---|
| 同じ宣言の再 build が同じ file 名と bytes を出すか（Unity 出力の決定性。Incremental と Full の一致、生成 `BuildContentRoot.asset` の GUID を含む） | BuildSystem 後続（spike。本スライスの判定で得た directory の manifest SHA-256 を記録として残す） |
| 同じ bytes を revision・目的をまたいで一度だけ転送・格納し、差分だけを更新する（content-addressed な transport・cache・Storage。Unity の manifest が持つ file ごとの `ContentHash` と依存 graph を単位にし、revision は manifest、install は object store からの実体化にする） | BuildSystem 後続（Storage program の保留項目「Contentの分離・取得・取り込み」「孤児回収」と接続） |
| 複数 directory の同時登録、共通 base と目的差分への層化、cross-directory 参照の成立 | BuildSystem 後続（spike） |
| 設定（target、compression、defines、project settings）を宣言で変え、identity に project settings を含める | BuildSystem 後続 |
| InGame の Spring に入る Player の build 経路、Player での VFX 描画 | BuildSystem 後続 / S-9 |
| `outcome.json` の選択・閉包の記録形式（§24 §16.7） | BuildSystem 後続 |
| Planner profile の Whitebox 上の Event の動作、S-4 program の harvest と削除 | S-4d5b |
| Addressables の退役（`.vfx`・material の entry を含む）、旧 Variant 経路の整理 | ADDR-RETIRE / ADDR-HYGIENE（§20 §9） |
| 付随 asset の World Workspace transaction への組み込み、Variant 別の付随 asset | 需要が出たスライス |
| directory に無い OnDemand content を要求した時の Runtime の振る舞い（Cell の欠落、companion の Warning の集約） | 需要が出たスライス（本スライスは宣言の既定値で不一致を防ぐだけ） |
| §24 §16.7 の Framework の欠落（親の unload 開始後の子の Add、`LoadAssetAsync` の await 後の所有者確認） | 所有スライス未定のまま |

- **判定定義:** 不変条件 I1〜I7 と成立条件 E1〜E8 を AC1〜AC23 で確かめ、全て成立で GO。1 つでも不成立なら NO-GO。
- **停止規則:** 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。スパイクではないので CONDITIONAL ACCEPT は使わない。
- **A3 後の例外承認:** なし。
- **決定記録:** なし。
- **本文へ転記した実装制約:** §1.1・§1.2。Unity 側 `.cs` は `#nullable enable`、`record` 禁止、破棄されうる `UnityEngine.Object` の `== null` 判定、公開 API のログ抽象は `ILogger<T>`。
- **未決事項:** §2.6。

### 2.6 未決事項

- **U1（分割）:** 見積もりは製品コード 1,200〜1,500 行、テスト 1,100〜1,500 行で、500 行の警報を超える。非分割の理由: 問いは到達規則・付随 asset・fail-closed の 3 つが揃って初めて「その成果物だけで動く」と言え、identity は選択の出力に依存する。片方だけでは d5b の前提も満たさない。**分割の引き金:** Phase B で Runtime 側（D5・D9・D10 の Runtime 部分）が Framework の公開面を `AttachedAssetDescription`・`SceneResource.AttachedAssets`・`SceneBase.LoadAttachedAssetAsync`（2 overload）・`AssetManagement` の backend 無し構成・`BuildContentRoot.Declaration` の 5 点を超えて要する場合、または Runtime 側の製品コードが 400 行を超える場合は B を止め、Runtime 側を BS5b として A3 で分ける。
- **U2（tag の水準）:** 推奨は D3 の二水準（node と candidate）。代案は候補の tag だけで到達を判定する（Representation の違う候補で node の tag が揺れるので、payload ごとに同じ tag を重複して付けることになる）。
- **U3（identity の長さ）:** 推奨は 32 hex（path 予算と `ValidateSegment` の 64 文字上限に収まる）。代案は 64 hex。
- **U4（再利用の安全）:** 推奨は AC8 の再利用。代案は従来どおり衝突で失敗（決定的な identity では、変更無しの再 build が常に失敗する）。再利用の誤りの源（identity に入らない project settings）は D4 の既知の限界として記録し、P2 の spike で測る。
- **U5（Object の Full fallback）:** 推奨は Scene と同じ fallback（D5）。代案は宣言側で付随 asset に表現ごとの payload を要求する（SampleGame の 3 件は Full のみで、Whitebox Play で必ず欠ける）。
- **U6（fail-closed の置き場）:** 推奨は D9（backend 無しの構成。Addressables の初期化自体が走らない）。代案は `AssetManagement` に mode flag を持たせて `FromAddress` を拒否する（backend は作られ、Addressables の初期化が走る。拒否の網羅が実装依存になる）。
- **U7（宣言の運搬の経路）:** 推奨は D10（`BuildContentRoot` と manifest の両方に載せ、Editor は manifest、Player は baked config を読む）。代案は manifest だけ（Player が宣言を知る経路が無くなり、BS4 の RuntimeJson の手書きが残る）。
- **U8（revision の形式と Storage）:** 32 hex の identity は tools/Artifacts の 4 箇所の正規表現が拒否する（§1.3）。推奨は、BS5 を RC1 の観測事実（日付・task `bs5-purpose-selection`・拒否の reasonCode `invalid-content-identity`）として Storage program の保留 list に書き、同じ Phase B で 4 箇所を `[0-9a-f]{32}` も受理する形に広げ、Artifacts の offline suite を判定必須に加える（発注者の決定が要る）。代案は identity を `00000000T000000000Z-<32 hex>` の形にして正規表現を通す（時刻の桁に意味の無い 0 を置く偽装で、後で必ず直すことになる）。

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
| 新規 `Editor/Build/Content/BuildContentIdentity.cs` | 正準化と hash | 呼び出しごと | pure + gateway | 単体（AC7） | 0 → 90〜120 |
| `Editor/Build/Content/BuildContentCoordinator.cs`・`BuildContentReport.cs` | identity の計算、既存成果物の再利用、build mode、preflight への正準化文字列・時刻・mode | — | Editor I/O | 偽 adapter（AC8） | 145 → 200、97 → 125 |
| `Editor/Build/Content/UnityContentDirectoryAdapter.cs` | `BuildContentRoot.Declaration` の書き込み、`Full` mode の workspace 消去 | — | Editor I/O | — | 166 → 180 |
| `Editor/Build/Content/ContentTransportPublisher.cs` | staging 名の短縮、manifest への `declaration` | — | Editor I/O | path 長と manifest 往復の単体（AC14） | 108 → 125 |
| `Runtime/BuildContent/BuildContentRoot.cs`・`Distribution/ContentTransportManifest.cs`・`ContentManifestValidation.cs` | 宣言の運搬（schema version の更新、validation の受理） | — | public（serialized） | `ContentManifestValidationTests` 拡張 | 88 → 110、133 → 150、293 → 300 |
| 新規 `Runtime/AssetDescriptions/AttachedAssetDescription.cs` | node が所有して子孫が読む付随 asset の宣言 | SceneResource に埋め込み | `AssetDescription`。public | — | 0 → 50 |
| `Runtime/SceneSystem/SceneResource.cs` | 付随 asset の列と検索 | — | public | EditMode（AC13） | 98 → 115 |
| `Runtime/SceneSystem/SceneBase.cs`・`SceneDirector.cs`・`SceneDirector.Loading.cs` | `LoadAttachedAssetAsync`（2 overload）、`SceneContentBinding` の注入 | Scene(identity) | `IAssetManagement`。protected 2 件、internal 1 件 | 単体（AC11・AC20） | 274 → 325、280 → 292、683 → 690 |
| `Runtime/AssetManagement/AssetManagement.cs`（+ `.ContentDirectory.cs`） | backend 無しの構成と、Addressables 経路の `InvalidOperationException` | App | internal の構成口 1 件 | 単体（AC19） | +40 |
| `Runtime/Bootstrap/AbstractApplicationInitializer.cs` | directory mode での構成の選択と起動ログ 1 行 | App | 既存 | EditMode（既存の起動テスト） | +15 |
| `Runtime/BuildContent/ContentDirectoryIndex.cs` | Object の Full fallback | — | internal | `ContentDirectoryIndexTests` | 129 → 131 |
| 新規 `SampleGame/DependOnAll/Editor/Build/SeasonSceneTagPolicy.cs` | node 水準の `Season`・`Role` tag | — | `CellCompanionRoleClassifier`（InternalsVisibleTo） | 単体 | 0 → 100〜130 |
| 新規 `SampleGame/DependOnAll/Editor/Build/SampleGameBuildPurposes.cs` | 6 件の宣言（runtime 既定値を含む） | — | pure | EditMode（AC4・AC5） | 0 → 90〜110 |
| `SampleGame/DependOnAll/Editor/Build/SampleGameContentBuild.cs` | 宣言ごとの menu、Build And Publish、Build And Use For Next Play、batch 入口 | — | Editor I/O | 操作（AC14・AC15・AC23）、引数の単体 | 131 → 230〜260 |
| `SampleGame/DependOnAll/Editor/Build/ContentDeliveryWindow.cs`・`ContentDeliveryPlayBridge.cs` | From published directory、宣言の runtime 既定値の適用と Reset | — | Editor I/O | 単体（ledger の往復、AC21・AC22） | 168 → 200、98 → 140 |
| 削除 `SeasonSceneSelectionPolicy.cs`・`SeasonSceneSelectionPolicyTests.cs` | 置き換え | — | — | — | 243 → 0、181 → 0 |
| `SampleGame/DependOnAll/Editor/Build/SampleGamePlayerBuildCoordinator.cs` | `bs4-spring-full` 宣言の利用、RuntimeJson を宣言から写す | — | 既存 | `Bs4FixtureComposerTests`、AC17 | 145 → 155 |
| `SampleGame/InGame/.../World/LocalVfxEventTrigger.cs`・`MajorEventVfxAnchor.cs`・`LocalVfxEventBinder.cs`・`CellCompanionScene.cs`・`SeasonMajorEventScene.cs`・`SeasonScene.cs` | address → 付随 asset 名、`LoadAttachedAssetAsync` の利用 | 既存 | 既存 | AC12・AC13・AC20 | 各 ±5〜10 |
| `SampleGame/InGame/Properties/AssemblyInfo.cs` | `InternalsVisibleTo` 1 行 | — | — | — | +1 |
| asset（3 つの SceneResource、Scene 内の trigger / anchor の値） | 付随 asset の宣言 | — | — | AC13 | — |
| テスト（新規 `BuildPurposeResolverTests`、`BuildContentIdentityTests`、`SeasonSceneTagPolicyTests`、`SampleGameBuildPurposesTests`、`SceneBaseAttachedAssetTests`、`AssetManagementDirectoryModeTests`、`ContentBuildCommandLineTests`、既存の拡張） | — | — | — | — | 0 → 1,100〜1,500 |

- **警報と判断:** 合計は 500 行を超える。非分割の理由と分割の引き金は §2.6 U1。`SceneDirector.Loading.cs`（683 行）は注入 1 行の追加に留める。`BuildTagSelector` は既存の責務（照合）の拡張で、優先の規則は requirement の属性として置き、policy 判断を selector に足さない。`SampleGameContentBuild` は menu・publish・install・batch の 4 つの入口を 1 file に持つことになるので、230 行を超えたら batch 入口を `SampleGameContentBuildCommandLine` に分ける（変更理由が別: 引数の解釈）。
- **配置:** 到達規則と宣言は `OneStarMaker.Build.Selection`（pure、Runtime に依存しない）。identity は `OneStarMaker.Build.Content`（gateway に依存）。付随 asset の宣言と宣言の運搬は Runtime（§18 §3 の埋め込み型の方針、`BuildContentRoot` は既に serialized の運搬役）。SampleGame の語彙（Season、Role、宣言の表、環境変数名の規則）は `SampleGame.DependOnAll` / `.Editor`。新しい asmdef・依存 edge は無い。

## 4. 実装計画（A1）

### 4.1 順序

1. Selection: 宣言型、graph node、resolver、selector の拡張、単体テスト（AC1〜AC3・AC6）。
2. Materialization と identity: graph snapshot、付随 asset の候補、`GetDependencyHash`、`BuildContentIdentity`、coordinator の再利用と build mode、publisher の staging 名、テスト（AC7〜AC9・AC14 の単体）。
3. SampleGame の policy と宣言の表、menu、BS4 の接続、テスト（AC4・AC5）。この時点で既存 4 build の選択が一致することを EditMode で確かめる。
4. Runtime の付随 asset と fail-closed: `AttachedAssetDescription`、`SceneResource`、`SceneBase` / `SceneDirector`、index の fallback、`AssetManagement` の backend 無し構成、`AbstractApplicationInitializer`、テスト（AC10・AC11・AC19・AC20）。ここで U1 の引き金を確かめる。
5. 宣言の運搬: `BuildContentRoot`・manifest・validation・adapter・publisher、PlayBridge と Delivery 窓、BS4 の RuntimeJson、テスト（AC21・AC22 の単体）。
6. SampleGame の Scene class と trigger / anchor、asset の宣言（Inspector または一時スクリプト）、Play テストの更新（AC12・AC13）。
7. 1 操作の loop と batch 入口（D7・D11）、引数の単体テスト（AC23）。
8. `pwsh tools/contract-audit.ps1`、Editor の compile 確認。Phase B の終わりに Addressables mode の Editor Play を 1 回軽く Play して AC12 の前提と D9 の起動ログを確かめる。Phase B は content build・directory Play を実行しない。

### 4.2 Phase B から Phase A へ差し戻す条件

- U1 の Framework 公開面 5 点を超える公開 API、新しい状態・所有者・寿命、asmdef 参照が要る。
- 到達規則を pure に保てない（`SceneResourceMap` や AssetDatabase を resolver から読む必要が出る）。
- 既存 4 build の選択が `840b6609` の policy と一致しない原因が、宣言で表せない規則にある。
- identity の入力に `GetAssetDependencyHash` 以外の Editor 状態が要る。
- Object の Full fallback が既存 `ContentDirectorySessionTests` の契約を破る。
- directory mode で backend 無しにすると、`UICommon` の bootstrap や他の App 常駐の初期化が Addressables を要して起動できない（D9 の前提が崩れる）。
- 任意 field として足した `declaration` を DIST の validation か `JsonUtility` が拒否し、version を上げる必要が出る（既存の installed revision の再検証に影響する）。
- U8 の決定が無いまま Storage の正規表現に当たる（`content collect` が `invalid-content-identity` で止まる）。

### 4.3 対象外を維持する方法

- transport・DIST・Storage の key を変えない。識別子は revision 文字列の中身が変わるだけで、形式（`installed/<contentSet>/<revision>`）は変えない。manifest の追加 field は読む側が無視できる形にする。
- Player の bootstrap・smoke・`Application.Quit` に触れない。RuntimeJson の値の出どころだけを宣言に替える。
- `AssetManagement` の Addressables 側の経路と `AssetKey` を変えない。backend 無しの構成を足すだけ。

## 5. テストとレビュー計画（A1）

- **単体テスト:** `BuildPurposeResolverTests`（AC1〜AC3・AC6）、`BuildTagSelectorTests` の拡張、`BuildContentIdentityTests`（AC7）、`SeasonSceneTagPolicyTests`、`SceneBaseAttachedAssetTests`（偽 `IAssetManagement`、AC11・AC20）、`AssetManagementDirectoryModeTests`（AC19）、`ContentDeliveryPlayBridgeTests`（AC21・AC22 の失敗経路）、`ContentBuildCommandLineTests`（AC23 の引数）、publisher の staging 名の path 長と manifest の往復（AC14）。
- **差し戻し中の起点 `-Filter`:** `BuildPurposeResolverTests`、`BuildTagSelectorTests`、`SceneResourceContentMaterializerTests`、`BuildContentIdentityTests`、`BuildContentCoordinatorTests`、`SampleGameBuildPurposesTests`、`SeasonSceneTagPolicyTests`、`ContentDirectoryIndexTests`、`ContentDirectorySessionTests`、`BuildContentDirectoryIntegrationTests`、`SceneBaseAttachedAssetTests`、`AssetManagementDirectoryModeTests`、`ContentManifestValidationTests`、`PlayerContentConfigurationTests`、`ContentDeliveryPlayBridgeTests`、`LocalVfxEventPlayTests`、`SeasonMajorEventPlayTests`、`Bs4FixtureComposerTests`。
- **判定必須テスト:** 最終の全 EditMode 回帰（空 filter）、PlayMode の `LocalVfxEventPlayTests`・`SeasonMajorEventPlayTests`・`VfxScenePlayTests`・Streaming の PlayMode（`-WithGraphics` を併用）。Content Directory build 2 件（`spring-full`、`spring-planner-whitebox`）を batch 入口（AC23）で行い、`preflight.json` を照合 script にかける（AC4・AC5）。同じ 2 宣言をもう 1 回 batch で build して AC8 の再利用（`reused=true`）を確かめる。各 build の manifest SHA-256 を記録に残す（P2 の入力）。U8 を採用した場合は tools/Artifacts の Content 系 offline suite（`tools/Artifacts/` の `*.Tests.ps1`）を判定必須に加える。
- **全 EditMode 回帰の適用除外:** なし。
- **統合・Unity テスト:** `BuildContentDirectoryIntegrationTests` の拡張（AC10）、`Bs4FixtureComposerTests`、`ContentManifestValidationTests`・`ContentDeliveryFailureMatrixTests` の既存経路。
- **操作・実行時・目視条件の検証経路:**
  - AC14（担当: 動作確認の実行担当。環境: Editor、`spring-full` と `spring-planner-whitebox` の `Build And Publish` menu。観測: published directory の `transport.json` と `content/`、Console に出た manifest SHA-256。合否は Phase C が manifest の files と directory の照合で決める）。
  - AC15（担当: 同上。環境: `spring-full` の `Build And Use For Next Play` 1 操作の後、Play。観測: 起動ログの構成 1 行、(0,4) Stable、WASD で (4,2) へ移動し Events の trigger 位置へ入る、Console の Error の有無、VFX の表示。証拠: Console ログの保存と screenshot 1 枚）。
  - AC16（担当: 同上。環境: `spring-planner-whitebox` の `Build And Use For Next Play` の後、環境変数を足さずに Play。観測: (0,4) Stable、`SceneDirector` のロード済み一覧に `_Environment_` が無い、Error の有無。証拠: Console ログ）。
  - AC22（担当: 同上。環境: Delivery 窓の From published directory に AC14 の path を入れて Prepare。観測: 埋まった field と Prepare の結果）。
  - Phase B の終わりに、Addressables mode の Editor Play を 1 回軽く Play して trigger の名前変更と付随 asset の宣言が動くこと（AC12 の前提）と、directory mode の起動ログの行が出ることを確かめる。
- **未知の操作経路:** (1) `UICommon` の bootstrap や App 常駐の初期化が Addressables backend 無しで起動できるか。未確認。初回確認は Phase B の順序 4（担当: B）。不成立なら §4.2 で A へ返す。(2) `JsonUtility` と DIST の validation が任意 field `declaration` を無視して受理するか。初回確認は B の順序 5 の単体テスト（宣言の有無の両方の manifest を `Validate` に通す）。(3) `AssetDatabase.GetAssetDependencyHash` が Scene と `.vfx` で安定した値を返すか。未確認。初回確認は B の単体テスト作成時。(4) Delivery 窓の ledger と宣言の runtime 既定値の環境変数が Editor Play で両方読まれるか。未確認。初回確認は B の順序 5。
- **人間の判断が必要な条件:** なし（AC15 の VFX の表示は Console のログ `LocalVfxEventSession` の起動記録で代替し、screenshot は補助）。
- **高コスト証拠:** BS4 Player build 1 回（AC17）。手順: ルート 96 文字以下の checkout で `Tools/OSM/Content/Build BS4 Spring Player` を実行し、Player を起動して smoke receipt を取る。独立した期待値: receipt が `completed`、`build-receipt.json` の `identity` が 32 hex、Player 内 `content/` の `BuildContentRoot` の entry に `Spring_Events_4_2:Burst`・`Spring_Event_MultiCellProof:Burst`・`InGameSession:Lit` の `Object` entry がある（manifest の `Loadables` が 5）、`bs4-runtime.json` の `cellCompanionSet` と `representation` が宣言の値。判定 C → 暫定 C' → 高コスト証拠 → 最終証拠ラウンドの順（review-evidence の規則）。
- **判定 C の照合 script の方式:** A3 凍結 script（offline の pwsh）。入力は GO 候補 head で batch 入口が出した `preflight.json` 2 件と、A3 で凍結する期待値（宣言ごとの件数と sorted stable key 列の SHA-256。`840b6609` の policy で得た `spring-whitebox` 124 件に付随 asset 3 件を加えた集合、`spring-planner-whitebox` 70 件）。script は selected の stable key を sorted して件数と SHA-256 を比べ、`_Environment_` 等の禁止 pattern の不在と、`BuildMode`・identity の形式（32 hex）を確かめる。期待値の stable key 列は A3 の前に `840b6609` の Editor で 5 宣言分の `preflight.json` を取って凍結する（担当: A3 の主担当。Editor 操作だけで Unity テストは伴わない）。
- **機械検査:** `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。
- **A0/A1 主担当・モデル・ベンダー:** この session の主担当（Anthropic 系）。
- **A2 独立レビューごとの観点・担当:** 未実施。観点は (a) アーキテクチャゲート（Framework / SampleGame、pure / I/O、公開面の最小性。特に D9・D10 が Runtime に足す面）、(b) 到達規則と不変条件を破る順序の探索（I1・I2・I7 を破る graph・filter・mode の例を 1 つ挙げられるか）、(c) identity と再利用の安全（AC8 の誤再利用の経路、build mode と identity の分離）、(d) 実ユースケースの通し（企画者の loop、Player、batch で欠ける手順が残っていないか）。入力版は本 commit の内容 sha256 を正とする。
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
