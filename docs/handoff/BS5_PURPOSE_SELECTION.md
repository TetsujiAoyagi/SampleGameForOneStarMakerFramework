# BS5 目的からの最小選択

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `A`（A2 入力版。未凍結）
- branch: `claude/bs5-purpose-selection`
- implementation base commit: `840b6609`（`develop`、S-4d5a の PR #149 のマージ）
- implementation head commit: 未固定
- risk: `high`（Framework の選択 core・materializer・build identity と、Runtime の `SceneResource` / `SceneBase` の公開面に触れる。既存の 5 つの content build 入口と BS4 Player coordinator が同じ経路を通る）
- owner: 発注者 / BS5 担当
- created: 2026-10-31
- expires: BS5 の Phase D 完了時
- harvest to: Architecture §18（§4 の選択 core・materialization・Content Directory build の現況、§5「Scene 以外のアセット種別」）、§20 §3（Editor Play の build → publish → install の手順）、§13（Object の表現 fallback）、§24 §16.7 / §16.9 / §16.10（`.vfx` の経路と「address でロード」の記述）、S-4 program §6.3 の BS5 行と d5b 行
- workflow task: `bs5-purpose-selection`（未開始。最初に Evidence を固定する時点で `pwsh tools/workflow-task.ps1 start` する）
- Phase A snapshot の取得案内・状態: 未生成（凍結した HANDOFF 本体は A3 凍結 commit で固定する）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票: Phase A 作業中。本版は A2 の入力版。A2 の独立レビューを統合した A3 版を作り、発注者が凍結を指示するまで Phase B に入らない。

---

## 1. 目的と対象外

- **目的:** 目的（Runtime が実際にロードする範囲）・設定（Unity の build 条件）・Variant（表現）の宣言から、必要十分な root 集合と閉包（Scene が動的にロードする付随 asset を含む）が決定的に導かれ、同じ宣言と同じ source からは同じ build identity が得られる BuildSystem にする。あわせて、Editor Play が Content Directory を使うまでの build → publish を文書の手順だけで再現できる入口を作る。S-4d5b（Planner profile の Whitebox 上の Event 実証）の前提を満たす。
- **対象外:**
  - 格納の最小性（同じ bytes を revision や目的をまたいで一度だけ転送・格納する）。Unity の出力の決定性の測定と content-addressed な転送・格納は後続（§2.5）。
  - 複数 Content Directory の同時登録と、共通 base と目的差分への層化。
  - InGame の Spring に入る Player の build 経路（BS4 の Player は Title の smoke の後に `Application.Quit(0)` する）。BS4 の経路は壊さないが、Player の動作は変えない。
  - 設定（target、compression、options、defines）を宣言で変える口。本スライスは値を固定したまま identity に含めるだけ。
  - World Workspace の transaction への付随 asset の authoring の組み込み。
  - Addressables の退役、`SeasonalMode` 以外の旧 Variant 経路の整理。
  - §24 §16.7 の所有スライス未定の事項のうち本スライスの問いに関係しない事項。
- **現況:** §1.3（A0、`840b6609` で確認）。

### 1.1 Phase A の判断基準（発注者、2026-10-27。S-4 program §0 から転記）

- **主目的は BuildSystem。** VFX（S-4d）はそのつなぎ込みの題材である。各案で、成果物が content build・Player build の経路に乗るかと、Build 側の既知の課題（BS4 の Player は smoke の後に終了して InGame の Spring に入らない、transport staging の path が予算の外）を踏むかを書く。Build 側の課題は「やらない」で片付けず、BuildSystem の作業の候補として扱う。
- **残すコードは SampleGame に必要なものだけ。** 生成や authoring の道具を残すかは「SampleGame にそのコードが必要か」で決める。不要なら一時スクリプトで作り commit の前に消す。
- **checkout のルート。** Player を build するスライスは、ルートが 96 文字以下の checkout で作業する。この worktree のルートは 31 文字。本スライスの判定で BS4 Player build を高コスト証拠として 1 回取得する（§5）。
- **問いの立て方（発注者、2026-10-31）。** BuildSystem の問いは「目的別・設定別・Variant 別が絡み合い、なおかつ必要最小限になる Build 成果物ができ、必要最小限に格納される」こと、そして SampleGame に引っ張られない汎用的な System になること。本スライスは前半（選択の最小性）を答え、格納の最小性は後続に送る。

### 1.2 S-4 program から転記した制約

- **SceneGraph（program §2.1）。** `InGameSession` の下に `Season_{Season}`（OnDemand）、その下に `{Season}_Lighting`・`{Season}_AtmosphereVFX`（NecessaryAlways）、`{Season}_Cell_{x}_{y}`（OnDemand、StreamByDistance）、`{Season}_Event_{slug}`（OnDemand）。Cell の下に `{Season}_Environment_{x}_{y}`（OnDemand、resident companion）、`{Season}_Lighting_{x}_{y}`・`{Season}_VFX_{x}_{y}`・`{Season}_Events_{x}_{y}`（OnDemand、optional companion）。
- **Planner Play（program §3.1 / §6.2）。** `content:representation=Whitebox` と `world:cellCompanionSet=Planner` を使い、Environment をロードしない状態で local / major Event を実証する。companion set は Cell の companion だけを選別し、Season の子（Lighting・Atmosphere・Event）は対象外。
- **d5b の行（program §6.3）。** 前提は「非 Scene asset の production 経路と Editor Play の publish 入口を所有する BuildSystem の HANDOFF の完了」。
- **VFX の寿命（program §2.4、Architecture §24 §16.9 / §16.10）。** 一時 VFX の source asset は Scene が `AssetOwner.Scene(identity)` で所有してロードし、Scene の unload で VFX を回収してから asset を解放する。本スライスは所有者と順序を変えず、asset の指し方だけを変える（§2.4 D5）。
- **公開 API と依存（program §10）。** Framework は season 名、Cell 座標文法、職種 role を知らない。Game → Framework の依存方向を維持する。`SceneState` の 14 値と `LoadType` を変えない。
- **検証方針（program §11）。** `unity test` / `unity run`、Scene / asset YAML の手編集は禁止。Phase B は Editor 操作・compile 確認まで、Unity テストと Build は Phase C。

### 1.3 A0 の現況（`840b6609`）

**build の経路。** source → `SceneResourceContentMaterializer`（`SceneResourceMap` の Scene payload だけを候補にする）→ `SeasonSceneSelectionPolicy`（季節・表現の request と requirement）→ `BuildTagSelector`（pure）→ bootstrap composer（UICommon と `SceneResourceMap.asset` を固定 content として合成）→ `BuildContentProjection`（閉包の照合）→ `UnityContentDirectoryAdapter`（`BuildContentRoot` を生成して `BuildPipeline.BuildContentDirectory`）→ `BuildContentCoordinator`（`artifacts/bs2b/content/<identity>/` へ公開）→ `ContentTransportPublisher.Publish`（BS4 だけが呼ぶ）→ DIST install → Editor Play（Delivery 窓の Use For Next Play）または BS4 Player。

**選択 core（`BuildTagSelector`、302 行、pure）。** 候補は `StableKey / LogicalKey / PhysicalKey`。request は dimension ごとの OR、dimension 間の AND。候補の tag が request に無い dimension なら `UnrequestedDimension`、値が選ばれていなければ `ValueNotSelected` で除外。tag の無い dimension は中立。requirement は logical key ごとの `ExactlyOne` / `OneOrMore` で、選ばれた候補の数だけを数える。**requirement の無い logical key の候補を除外する手段は無い。**

**季節 policy（`SeasonSceneSelectionPolicy`、243 行、SampleGame）。** graph を複製して `Season` を祖先から導き、in-scope の全 node を requirement にする（最小ではなく季節の全部）。Whitebox 選択は `SeasonalMode` tag（切替可能な group にだけ付ける）で Full 専用の補助 Scene を残す。候補と node の有無が一致しないと "Payload/candidate mismatch" で止める。`FullAndWhitebox` は `OneOrMore` と `VerifyDual` で両表現を要求する。

**目的の不在。** 目的に相当するのは runtime の `CellCompanionSet`（Full / Planner / Lighting / Vfx）と `CellCompanionRoleClassifier`（identity の末尾 3 token 目で Environment / Lighting / VFX / Events を判定。`SampleGame.InGame` の internal）だけで、build 側に dimension が無い。Planner 用の Whitebox build は Planner が読まない Environment 54 件・Cell Lighting 2 件・Cell VFX 1 件を含む。

**実測の graph（`SceneResourceMap`、457 node）。** OutGame: `OutGameScene`（NecessaryAlways、root）、`Title`・`HpGauge`・`OutGameSession`（OnDemand）、`ConfirmDialog`（HpGauge の子）、`HomeScene`（OutGameSession の子、payload 無し）。InGame: `InGameScene`（root、payload 無し）、`InGameSession`（NecessaryAlways、payload 無し）、`InGameUI`・`PlayerScene`（NecessaryAlways）、`Result`（OnDemand）、`Season_*` 4 件（OnDemand、payload 無し）。各 Season: `{S}_Lighting`（NecessaryAlways）、Spring だけ `Spring_AtmosphereVFX`（NecessaryAlways）と `Spring_Event_MultiCellProof`（OnDemand）、Cell 54 件（OnDemand、Variant は空と Whitebox）。各 Cell: Environment 1 件、Spring の (4,2)(5,2) に Lighting、(4,2) に VFX と Events。`Tunnel` は無い。

**実測の成果物（2026-10-30 の `spring-whitebox`）。** selected 124 件 = Scene payload 122 件 + bootstrap 2 件。Unity の出力は hash 名の `.cf` / `.resS` と hash 名の manifest `.json`、固定名 `BuildManifestHash.txt`、計 233 file・6.2 MB。manifest は `RootAssets 1 / Loadables 2 / LoadableScenes 123 / SerializedFiles 196`。on-disk の content build は 1 件だけで、再 build の bytes 比較はできていない。

**build identity。** `DateTime.UtcNow + Guid` で、入力から導出されない。同じ入力の build は別 identity、別 directory になる。`outcome.json` は identity・path・summary だけで、選択と閉包は `preflight.json` が正本。

**閉包。** `AssetDependencySnapshotBuilder` が `AssetDatabase.GetDependencies` で root ごとに閉包を取る。Scene 内に置いた `.vfx`（`Spring_AtmosphereVFX`・`Spring_VFX_4_2`）は閉包で同梱される。**動的にロードする `.vfx` 2 件（`Spring_Events_4_2_Burst.vfx`、`Spring_Event_MultiCellProof_Burst.vfx`）は Scene の依存でないので入らない。** `BuildContentRoot` の `Object` entry を本番 build で埋める経路は bootstrap composer（`SceneResourceMap.asset`）と BS4 の `Bs4FixtureComposer`（probe prefab）だけで、どちらも選択 policy を迂回する合成。

**Runtime の読み口。** `SceneBase.LoadSceneScopedAssetAsync(AssetKey)` だけがあり、`AssetKey.FromAddress` は常に Addressables backend へ行く。directory の `IAssetManagement.LoadContentAssetAsync(logicalKey, representation, owner)` は Scene から届かない。`ContentDirectoryIndex` の Object 解決には Scene にある Full fallback が無い。`SceneDirector` は `_useContentDirectory` と `_sceneVariant` で Scene のロードを分岐し、`SceneBase` には `BindAssets(IAssetManagement)` だけを注入する。trigger / anchor は `.vfx` の Addressables address（asset path）を文字列で持ち、`CellCompanionScene` / `SeasonMajorEventScene` が `FromAddress` でロードする。Content Directory を使う実行ではロードが失敗して Error を 1 回出し、VFX 無しで進む。

**Editor Play の publish。** `Tools/OSM/Content/Build ...` 4 つは Content Directory を生成するだけ。`ContentTransportPublisher.Publish` を Editor Play 向けに呼ぶ入口は無く、§20 / §4.8 / SEASON design が「未整備」と書く。publisher の staging は `<final>.<guid32>.staging` で、BS4 では repo 相対 163 文字になり、ルートが 97 文字以上の checkout で `DirectoryNotFoundException` になる（Harness README）。

**既存テスト。** `BuildTagSelectorTests`（481）、`SceneResourceContentMaterializerTests`（431）、`BuildContentProjectionTests`（111）、`SeasonSceneSelectionPolicyTests`（181）、`ContentDirectorySessionTests`（763）、`ContentDirectoryIndexTests`（83）、`BuildContentDirectoryIntegrationTests`（490、SampleGame、`TestResults/bs3-fixture` へ実 directory を build する）、`Bs4FixtureComposerTests`、`LocalVfxEventPlayTests`、`SeasonMajorEventPlayTests`（394 + 343）。

---

## 2. 意思決定と受け入れ境界（A1）

- **このスライスが答える問い:** 目的・設定・Variant の宣言から、必要十分な root 集合と閉包（動的ロードする付随 asset を含む）が決定的に導かれ、同じ宣言と同じ source からは同じ build identity が得られるか。

### 2.1 不変条件（起きてはいけないこと）

- **I1（必要なものを落とさない）:** 宣言の root から、`NecessaryAlways` / `IncrementalAlways` の子を全て、`OnDemand` の子を目的の filter が通すものだけたどって到達する node は、payload があれば必ず選ばれる。到達する Scene の付随 asset も必ず選ばれる。
- **I2（不要なものを入れない）:** 到達しない node の候補と、到達しない Scene の付随 asset は選ばれない。表現の優先順で選ばれなかった候補も選ばれない。
- **I3（決定的）:** 同じ宣言と同じ source からは、同じ選択（stable key の集合）、同じ閉包、同じ identity が得られる。候補・provider・node の入力順に依存しない。
- **I4（既存の成果物を変えない）:** 既存 4 つの content build と BS4 の `bs4-spring-full` は、新しい宣言に置き換えた後も selected の stable key 集合が `840b6609` の policy と一致する（付随 asset の追加分を除く）。
- **I5（Framework は語彙を知らない）:** Framework の選択・materialization・identity・Runtime の読み口は、season 名・Cell 座標文法・職種 role・`CellCompanionSet` を参照しない。Game → Framework の依存方向を維持する。
- **I6（寿命と順序を変えない）:** 付随 asset は従来どおり Scene が `AssetOwner.Scene(identity)` で所有し、VFX の回収 → asset の解放の順序、d4b / d5a の既存テストの結果を変えない。`SceneState`、`LoadType`、asmdef 参照を変えない。

### 2.2 成立条件（起きなければならないこと）

- **E1（宣言）:** 目的・設定・Variant を 1 つの宣言型で表せ、SampleGame の 5 つの content set（`all-full`、`spring-full`、`spring-whitebox`、`spring-full-whitebox`、`bs4-spring-full`）と新しい `spring-planner-whitebox` が宣言として存在する。
- **E2（最小選択）:** `spring-planner-whitebox` の選択は、Spring の Cell 54 件（Whitebox）、`Spring_Events_4_2`、`Spring_Event_MultiCellProof`、`Spring_Lighting`、`Spring_AtmosphereVFX`、InGame / OutGame の到達 Scene、bootstrap 2 件、付随 asset 2 件で、Environment・Cell Lighting・Cell VFX・他季節を含まない（§2.3 AC4 の件数）。
- **E3（付随 asset）:** `Spring_Events_4_2` と `Spring_Event_MultiCellProof` の `.vfx` が、所有 Scene が選ばれる全ての宣言の Content Directory に `Object` entry として入り、Scene から名前で解決できる。Editor Play の Addressables モードでも同じ呼び出しで解決できる。
- **E4（identity）:** identity は宣言と選択済み候補の source 状態から計算され、同じ入力の 2 回目の build は build を実行せず既存の成果物を返す。
- **E5（publish 入口）:** 宣言ごとの「Build And Publish」で、Content Directory build と transport publish が 1 操作で行われ、Delivery 窓の Local/LAN directory で Prepare → Use For Next Play → Play できる。publish の staging path は path 予算の範囲に収まる。
- **E6（directory の実行）:** `spring-full` の published directory を install した Editor Play で、Cell (4,2) の Events の trigger から VFX が起動し、Error ログが出ない。

### 2.3 受け入れ条件（検証する条件を併記）

単体 = Unity に依存しない純 C# テスト（`OneStarMaker.Build.Selection` 等）。EditMode = AssetDatabase や合成 fixture を使うテスト。Play = Play Mode テスト。操作 = 動作確認と Phase C の実行。

| AC | 内容 | 条件 | 種別 |
|---|---|---|---|
| AC1 | 合成 graph（root、NecessaryAlways / IncrementalAlways / OnDemand の子、payload 無しの中間 node、付随 asset 付きの node）で、到達規則どおりの requirement 集合が得られる。OnDemand の子は filter の dimension ごとの OR・dimension 間の AND・tag 無し中立・未要求 dimension の tag は拒否、の規則で判定される | I1・I2 | 単体 |
| AC2 | root でない node、到達しない subtree の node、到達しない Scene の付随 asset の候補は `Unreachable` の理由で除外される。到達する node の候補を欠くと `CardinalityViolation` で plan が公開されない | I1・I2 | 単体（失敗経路） |
| AC3 | 表現の優先順 `[Whitebox, Full]` で Whitebox 候補がある key は Whitebox だけ、Full しか無い key は Full が選ばれる。`All` では両方が選ばれ requirement は `OneOrMore`。優先順に無い表現の候補は除外される | I2 | 単体 |
| AC4 | 実 `SceneResourceMap` で `spring-planner-whitebox` の selected が、Role ∈ {Environment, Lighting, Vfx} の Cell companion を 1 件も含まず、Spring の Cell 54 件を全て Whitebox で含み、`Spring_Events_4_2`・`Spring_Event_MultiCellProof`・`Spring_Lighting`・`Spring_AtmosphereVFX` と付随 asset 2 件を含む。件数は Scene payload 65 + bootstrap 2 + 付随 2 = 69 | E2 | EditMode |
| AC5 | 実 `SceneResourceMap` で `spring-whitebox` の selected が、§1.3 の実測 124 件の stable key 集合 + 付随 asset 2 件に一致する。`spring-full`・`all-full`・`spring-full-whitebox`・`bs4-spring-full` も `840b6609` の policy の選択 + 付随 asset に一致する（A3 で各集合の件数と sorted stable key 列の SHA-256 を凍結する。§5 の照合 script） | I4 | EditMode + 判定 C の照合 |
| AC6 | 候補・provider・node を逆順・シャッフルで与えても、plan の selected / excluded の列と identity が同じ | I3 | 単体 |
| AC7 | identity は宣言（名前・root・filter・表現の優先順・設定）と、選ばれた候補の stable key・physical GUID・source の依存 hash の正準化文字列の SHA-256 から作られる。候補の依存 hash が 1 つ変わる、宣言の filter が変わる、設定が変わる、のどれでも identity が変わる。順序の違いでは変わらない | I3・E4 | 単体（偽 gateway） |
| AC8 | `content/<identity>/` が既に在れば coordinator は adapter を呼ばず、`outcome.json` に `reused=true` と既存 path を書いて成功を返す。staging だけが残る中途状態では再利用しない | E4 | EditMode（偽 adapter） |
| AC9 | materializer が `SceneResource` の付随 asset から候補（logical key `<identity>:<name>`、Representation は payload の Variant、`Owner` tag に所有 Scene の identity）と閉包を作る。名前の重複、参照欠損、所有 Scene の identity と衝突する名前は issue で止まる | E3 | EditMode（既存 materializer テストと同じ偽 gateway） |
| AC10 | 合成 fixture directory（`TestResults/bs3-fixture`）に Scene 1 件と付随 `Object` 1 件（Full）を build し、representation `Whitebox` で Scene を登録した session から付随 asset が Full fallback で解決・ロード・解放できる。存在しない名前は `EntryMissing` | E3 | EditMode（既存 integration テストの拡張） |
| AC11 | `SceneBase.LoadAttachedAssetAsync<T>(name)` は、directory モードでは `LoadContentAssetAsync(<identity>:<name>, sceneVariant, Scene(identity))`、それ以外では `LoadAssetAsync(AssetKey.FromDescription(付随 desc, sceneVariant), Scene(identity))` を呼ぶ。未宣言の名前は `ArgumentException`。所有者は常に `Scene(identity)` | E3・I6 | 単体（偽 `IAssetManagement`） |
| AC12 | 既存の `LocalVfxEventPlayTests` と `SeasonMajorEventPlayTests` が、trigger / anchor の address を付随 asset の名前に替えた後も通る（Addressables モード、`-WithGraphics`） | I6 | Play |
| AC13 | `Spring_Events_4_2`・`Spring_Event_MultiCellProof` の `SceneResource` に付随 asset `Burst` が宣言され、参照先が各 `.vfx`、Variant は空。trigger / anchor の名前が `Burst` | E3 | EditMode |
| AC14 | `Build And Publish` の後、published directory に `transport.json` と `content/` があり、manifest の files が directory の全 file と size・SHA-256 で一致する。staging path の repo 相対長が `tools/path-budget.json` の上限以内 | E5 | 操作（動作確認）+ EditMode（path 長の単体） |
| AC15 | `spring-full` の published directory を Delivery 窓で Prepare → Use For Next Play し、`world:cellCompanionSet` 未指定で Play して Spring (0,4) が Stable になり、Cell (4,2) へ移動して Events の trigger に入ると VFX が起動する。Console に Error が無い | E6 | 操作（動作確認）、証拠は Console ログと `preflight.json` |
| AC16 | `spring-planner-whitebox` の published directory を install し、`world:cellCompanionSet=Planner`・`content:representation=Whitebox` で Play して Spring (0,4) が Stable になり、Environment が 1 件もロードされず、Error が無い。Event の動作は d5b で確かめる | E2・E5 | 操作（動作確認） |
| AC17 | BS4 の `Build BS4 Spring Player` が成功し、Player の smoke receipt が `completed`。Player 内 `content/` に付随 asset 2 件の `Object` entry が入る | I4 | 高コスト証拠（§5） |
| AC18 | 最終の全 EditMode 回帰と、Streaming・Vfx の PlayMode が通る | I6 | 回帰 |

AC4 の読み方: 「Role ∈ {...} を含まない」は selected の logical key に `_Environment_`・`_Lighting_{x}_{y}`・`_VFX_{x}_{y}` の Cell companion が無いこと。`Spring_Lighting`（Season の NecessaryAlways）は含む。合格例: 69 件で Environment 0 件。不合格例: Environment が 1 件でも入る、Cell が 53 件。

AC8 の読み方: 「中途状態」は `staging/<identity>` だけが在って `content/<identity>` が無い状態。合格例: 既存の `content/<identity>` が在れば adapter は呼ばれない。不合格例: staging の残骸を成功として返す。

### 2.4 A1 の設計

- **D1（宣言型）:** `BuildPurposeDeclaration`（`OneStarMaker.Build.Selection`、pure）。`Name`（contentSet と同じ規則の ASCII）、`Roots`（logical key の列）、`Filter`（`BuildRequest`。OnDemand の子に適用する tag の述語）、`Representation`（優先順の列と `First` / `All`）、`Configuration`（target、compression、options、defines、Unity version、root schema version の正準化した key=value 列。本スライスでは adapter の定数と Editor の値を写すだけ）。Framework は値の意味を知らない。
- **D2（到達規則）:** `BuildPurposeResolver`（pure）。入力は graph node（identity、parent、children、LoadType、payload の有無、付随 asset の名前列）、node ごとの tag、候補と candidate tag provider、宣言。出力は requirement（到達 node の payload と付随 asset。`First` なら `ExactlyOne`、`All` なら `OneOrMore`）、Representation の request、到達しない候補と優先順に無い表現の候補の除外。規則: `required(n) ⇔ n ∈ roots ∨ (required(parent) ∧ (LoadType ∈ {NecessaryAlways, IncrementalAlways} ∨ filter(tags(n))))`。filter の意味は既存 `BuildTagSelector` の request の意味と同じ（dimension ごとの OR、dimension 間の AND、tag の無い dimension は中立、未要求 dimension の tag は拒否）。payload の無い中間 node（`InGameSession`、`Season_*`）も到達の経路になる。graph の検証（root の存在、片側 link、循環、重複）は既存 `SeasonSceneSelectionPolicy.ValidateGraph` の規則を Framework へ移す。
- **D3（選択 core の拡張）:** `BuildSelectionPolicy` に `ExcludeUnrequired`（requirement の無い logical key の候補を `BuildExclusionReasonCode.Unreachable` で除外。dimension は `LogicalKey`、value は key）と、`BuildContentRequirement` に表現の優先（dimension、順序付き値、`First` / `All`）を足す。`SeasonalMode` tag と `VerifyDual` は不要になり、SampleGame の policy から消す（`FullAndWhitebox` は `All` で表す）。tag を node 水準（Season、Role）と candidate 水準（Representation）に分け、node 水準は Game が `IReadOnlyDictionary<string, IReadOnlyList<BuildTag>>` で供給する。
- **D4（identity）:** `BuildContentIdentity`（`OneStarMaker.Build.Content`、pure + gateway）。正準化文字列は、宣言の各欄（sorted）、設定（sorted key=value）、選ばれた候補ごとの `stableKey|physicalGuid|dependencyHash`（stable key で sorted）。`dependencyHash` は `IAssetDatabaseGateway.GetDependencyHash(path)`（`AssetDatabase.GetAssetDependencyHash`）。SHA-256 の先頭 32 hex を identity（directory 名、`BuildContentRoot.BuildIdentity`、transport の `revision`）にし、完全な 64 hex と正準化文字列を `preflight.json` に残す。coordinator は `content/<identity>` が在れば adapter を呼ばず再利用する（AC8）。時刻は `preflight.json` の metadata に残すだけで identity に入れない。既知の限界: project settings（Graphics API、Quality、Lightmap 設定）と生成 `BuildContentRoot.asset` の GUID は identity に入らない。後者は再 build の bytes を変えうるので P2 の spike の入力（§2.5）。
- **D5（付随 asset）:** Runtime に `AttachedAssetDescription : AssetDescription`（`Name` と `AssetPayload` の列。`[Serializable]`、埋め込み）を足し、`SceneResource` に `_attachedAssets`（`List<AttachedAssetDescription>`）と `AttachedAssets` / `FindAttachedAsset(name)` を足す。materializer は付随 asset ごとに候補（logical key `<identity>:<name>`、stable key は既存の `osm-build-candidate-v1` 規則、Representation は Variant から既存規則で写す、`Owner` tag に所有 identity、provenance `SceneResourceMap.AttachedAsset`）と閉包を作る。`SceneBase` に `LoadAttachedAssetAsync<T>(string name, CancellationToken)` を足し、`SceneDirector` が `BindAssets` と同じ時点で `useContentDirectory` と `sceneVariant` を `SceneContentBinding`（internal の値型）として注入する。directory モードは `LoadContentAssetAsync(<identity>:<name>, sceneVariant, Scene(identity))`、それ以外は `LoadAssetAsync(AssetKey.FromDescription(desc, sceneVariant), Scene(identity))`。`ContentDirectoryIndex.TryResolveObject` に Scene と同じ Full fallback を与える（fallback は logical group の規則で、entry の種別の規則ではない）。trigger / anchor の `_vfxAddress` は `_vfxAsset`（付随 asset の名前）に替え、`CellCompanionScene` / `SeasonMajorEventScene` は `LoadAttachedAssetAsync` を呼ぶ。`.vfx` の Addressables entry は Addressables モードの `AssetReference` が要するので残す。所有者・回収順序・ロード失敗の扱い（Error 1 回、VFX 無しで進む）は変えない。
- **D6（SampleGame の policy）:** `SeasonSceneSelectionPolicy` を、node 水準の tag を導く `SeasonSceneTagPolicy`（`Season` は祖先から、`Role` は `CellCompanionRoleClassifier` で。classifier は `InternalsVisibleTo("SampleGame.DependOnAll.Editor")` で参照し、規則を複製しない）と、宣言の表 `SampleGameBuildPurposes`（6 件。root は `OutGameScene`・`InGameScene`、filter は `Season` と `Role`、表現の優先順）に置き換える。`SampleGameContentBuild` は宣言を引数に取り、menu は宣言ごとの `Build` と `Build And Publish` にする。bootstrap composer と BS4 の fixture composer は resolver の後に従来どおり合成する。
- **D7（publish 入口）:** `SampleGameContentBuild.BuildAndPublish(declaration)` が coordinator の結果を `ContentTransportPublisher.Publish(result, <artifactsRoot>/published/<identity>)` へ渡し、manifest の SHA-256・contentSet・revision・path を Console に出す。publisher の staging は `<final>.<8 hex>.tmp` にし、identity の短縮と合わせて repo 相対長を予算内にする（BS4 の `artifacts/bs4/transport/<identity>` も同じ publisher なので同時に収まる）。Delivery 窓は変えない。
- **D8（authoring）:** 2 つの `SceneResource` への付随 asset の宣言と trigger / anchor の名前は、Phase B が Editor の Inspector または一時スクリプトで行い、一時スクリプトは commit の前に消す。World Workspace の transaction は変えない。

### 2.5 ここでは答えない問いと所有する後続

| 問い | 所有 |
|---|---|
| 同じ宣言の再 build が同じ file 名と bytes を出すか（Unity 出力の決定性。生成 `BuildContentRoot.asset` の GUID を含む） | BuildSystem 後続（spike。本スライスの判定で得た 2 つの directory の比較を記録として残す） |
| 同じ bytes を revision・目的をまたいで一度だけ転送・格納する（content-addressed な transport と Storage の「不足 file の再利用」） | BuildSystem 後続（Storage program の保留項目「Contentの分離・取得・取り込み」と接続） |
| 複数 directory の同時登録、共通 base と目的差分への層化、cross-directory 参照の成立 | BuildSystem 後続（spike） |
| 設定（target、compression、defines、project settings）を宣言で変え、identity に project settings を含める | BuildSystem 後続 |
| InGame の Spring に入る Player の build 経路、Player での VFX 描画 | BuildSystem 後続 / S-9 |
| `outcome.json` の選択・閉包の記録形式（§24 §16.7） | BuildSystem 後続 |
| Planner profile の Whitebox 上の Event の動作、S-4 program の harvest と削除 | S-4d5b |
| Addressables の退役（`.vfx` の entry を含む）、旧 Variant 経路の整理 | ADDR-RETIRE / ADDR-HYGIENE（§20 §9） |
| 付随 asset の World Workspace transaction への組み込み、Variant 別の付随 asset | 需要が出たスライス |
| §24 §16.7 の Framework の欠落（親の unload 開始後の子の Add、`LoadAssetAsync` の await 後の所有者確認） | 所有スライス未定のまま |

- **判定定義:** 不変条件 I1〜I6 と成立条件 E1〜E6 を AC1〜AC18 で確かめ、全て成立で GO。1 つでも不成立なら NO-GO。
- **停止規則:** 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。スパイクではないので CONDITIONAL ACCEPT は使わない。
- **A3 後の例外承認:** なし。
- **決定記録:** なし。
- **本文へ転記した実装制約:** §1.1・§1.2。Unity 側 `.cs` は `#nullable enable`、`record` 禁止、破棄されうる `UnityEngine.Object` の `== null` 判定、公開 API のログ抽象は `ILogger<T>`。
- **未決事項:** §2.6。

### 2.6 未決事項

- **U1（分割）:** 見積もりは製品コード 900〜1,200 行、テスト 900〜1,300 行で、500 行の警報を超える。非分割の理由: 問いは到達規則と付随 asset の両方が揃って初めて「必要十分」になり、identity は選択の出力に依存する。片方だけでは d5b の前提も満たさない。**分割の引き金:** Phase B で Runtime の付随 asset（D5 の Runtime 側）が Framework の公開面を `AttachedAssetDescription`・`SceneResource.AttachedAssets`・`SceneBase.LoadAttachedAssetAsync` の 3 点を超えて要する場合、または Runtime 側の製品コードが 300 行を超える場合は B を止め、Runtime 側を BS5b として A3 で分ける。
- **U2（tag の水準）:** 推奨は D3 の二水準（node と candidate）。代案は候補の tag だけで到達を判定する（Representation の違う候補で node の tag が揺れるので、payload ごとに同じ tag を重複して付けることになる）。
- **U3（identity の長さ）:** 推奨は 32 hex（path 予算と `ValidateSegment` の 64 文字上限に収まる）。代案は 64 hex（directory 名が長くなり BS4 の transport path が再び予算に近づく）。
- **U4（再利用の安全）:** 推奨は AC8 の再利用。代案は従来どおり衝突で失敗（決定的な identity では、変更無しの再 build が常に失敗する）。再利用の誤りの源（identity に入らない project settings）は D4 の既知の限界として記録し、P2 の spike で測る。
- **U5（Object の Full fallback）:** 推奨は Scene と同じ fallback（D5）。代案は宣言側で付随 asset に表現ごとの payload を要求する（SampleGame の 2 件は Full のみで、Whitebox Play で必ず欠ける）。

---

## 3. 責務マップ（A1）

| ファイル | 責務・変更理由 | 所有者・寿命 | 依存・公開面 | テスト境界 | 行数（現在 → 予想） |
|---|---|---|---|---|---|
| 新規 `Editor/Build/Selection/Model/BuildPurposeDeclaration.cs` | 目的・設定・Variant の宣言（immutable、正準化） | 呼び出しごと | pure。public | 単体 | 0 → 80〜110 |
| 新規 `Editor/Build/Selection/Model/BuildGraphNode.cs` | 到達規則の入力 node（identity、parent、children、LoadType、payload の有無、付随 asset 名） | 呼び出しごと | pure。public。`LoadType` は Runtime の enum を参照しない（Selection asm は Runtime に依存しない。値は `BuildGraphLoad` の 3 値に写す） | 単体 | 0 → 50 |
| 新規 `Editor/Build/Selection/BuildPurposeResolver.cs` | graph の検証、到達規則、requirement と request と除外の導出 | 呼び出しごと | pure。public。`BuildTagSelector` を呼ぶ | 単体（AC1〜AC3・AC6） | 0 → 180〜240 |
| `Editor/Build/Selection/BuildTagSelector.cs` | `ExcludeUnrequired` と表現の優先の適用 | — | pure | `BuildTagSelectorTests` 拡張 | 302 → 350〜370 |
| `Selection/Policy/BuildSelectionPolicy.cs`・`BuildContentRequirement.cs`・`Results/BuildExclusionReasonCode.cs` | policy の flag、requirement の表現優先、`Unreachable`・`RepresentationNotPreferred` の理由 | — | pure | 単体 | 各 +10〜30 |
| `Editor/Build/Materialization/SceneResourceContentMaterializer.cs` | graph snapshot の出力、付随 asset の候補・閉包・`Owner` tag | — | `SceneResourceMap`・`AttachedAssetDescription` | `SceneResourceContentMaterializerTests` 拡張（AC9） | 147 → 220〜250 |
| `Materialization/BuildMaterializationSnapshot.cs`・`AssetDatabaseGateway.cs` | `Graph` の保持、`GetDependencyHash` | — | Editor | 偽 gateway | 66 → 85、+10 |
| 新規 `Editor/Build/Content/BuildContentIdentity.cs` | 正準化と hash | 呼び出しごと | pure + gateway | 単体（AC7） | 0 → 90〜120 |
| `Editor/Build/Content/BuildContentCoordinator.cs`・`BuildContentReport.cs` | identity の計算、既存成果物の再利用、preflight への正準化文字列と時刻 | — | Editor I/O | 偽 adapter（AC8） | 145 → 190、97 → 115 |
| `Editor/Build/Content/ContentTransportPublisher.cs` | staging 名の短縮 | — | Editor I/O | path 長の単体（AC14） | 108 → 110 |
| 新規 `Runtime/AssetDescriptions/AttachedAssetDescription.cs` | Scene が所有してロードする付随 asset の宣言 | SceneResource に埋め込み | `AssetDescription`。public | — | 0 → 50 |
| `Runtime/SceneSystem/SceneResource.cs` | 付随 asset の列と検索 | — | public | EditMode（AC13） | 98 → 115 |
| `Runtime/SceneSystem/SceneBase.cs`・`SceneDirector.cs`・`SceneDirector.Loading.cs` | `LoadAttachedAssetAsync`、`SceneContentBinding` の注入 | Scene(identity) | `IAssetManagement`。protected 1 件、internal 1 件 | 単体（AC11） | 274 → 310、280 → 290、683 → 690 |
| `Runtime/BuildContent/ContentDirectoryIndex.cs` | Object の Full fallback | — | internal | `ContentDirectoryIndexTests` | 129 → 131 |
| 新規 `SampleGame/DependOnAll/Editor/Build/SeasonSceneTagPolicy.cs` | node 水準の `Season`・`Role` tag | — | `CellCompanionRoleClassifier`（InternalsVisibleTo） | 単体 | 0 → 100〜130 |
| 新規 `SampleGame/DependOnAll/Editor/Build/SampleGameBuildPurposes.cs` | 6 件の宣言 | — | pure | EditMode（AC4・AC5） | 0 → 70〜90 |
| `SampleGame/DependOnAll/Editor/Build/SampleGameContentBuild.cs` | 宣言ごとの menu、Build And Publish | — | Editor I/O | 操作（AC14） | 131 → 160〜180 |
| 削除 `SeasonSceneSelectionPolicy.cs`・`SeasonSceneSelectionPolicyTests.cs` | 置き換え | — | — | — | 243 → 0、181 → 0 |
| `SampleGame/DependOnAll/Editor/Build/SampleGamePlayerBuildCoordinator.cs` | `bs4-spring-full` 宣言の利用 | — | 既存 | `Bs4FixtureComposerTests`、AC17 | 145 → 148 |
| `SampleGame/InGame/.../World/LocalVfxEventTrigger.cs`・`MajorEventVfxAnchor.cs`・`LocalVfxEventBinder.cs`・`CellCompanionScene.cs`・`SeasonMajorEventScene.cs` | address → 付随 asset 名、`LoadAttachedAssetAsync` の利用 | 既存 | 既存 | AC12・AC13 | 各 ±5〜10 |
| `SampleGame/InGame/Properties/AssemblyInfo.cs` | `InternalsVisibleTo` 1 行 | — | — | — | +1 |
| asset（`Spring_Events_4_2`・`Spring_Event_MultiCellProof` の SceneResource、Scene 内の trigger / anchor の値） | 付随 asset の宣言 | — | — | AC13 | — |
| テスト（新規 `BuildPurposeResolverTests`、`BuildContentIdentityTests`、`SeasonSceneTagPolicyTests`、`SampleGameBuildPurposesTests`、`SceneBaseAttachedAssetTests`、既存の拡張） | — | — | — | — | 0 → 900〜1,300 |

- **警報と判断:** 合計は 500 行を超える。非分割の理由と分割の引き金は §2.6 U1。`SceneDirector.Loading.cs`（683 行）は注入 1 行の追加に留める。`BuildTagSelector` は既存の責務（照合）の拡張で、優先の規則は requirement の属性として置き、policy 判断を selector に足さない。
- **配置:** 到達規則と宣言は `OneStarMaker.Build.Selection`（pure、Runtime に依存しない）。identity は `OneStarMaker.Build.Content`（gateway に依存）。付随 asset の宣言は Runtime の `AssetDescriptions`（§18 §3 の埋め込み型の方針）。SampleGame の語彙（Season、Role、宣言の表）は `SampleGame.DependOnAll.Editor`。新しい asmdef・依存 edge は無い。

## 4. 実装計画（A1）

### 4.1 順序

1. Selection: 宣言型、graph node、resolver、selector の拡張、単体テスト（AC1〜AC3・AC6）。
2. Materialization と identity: graph snapshot、付随 asset の候補、`GetDependencyHash`、`BuildContentIdentity`、coordinator の再利用、publisher の staging 名、テスト（AC7〜AC9・AC14 の単体）。
3. SampleGame の policy と宣言の表、menu、BS4 の接続、テスト（AC4・AC5）。この時点で既存 4 build の選択が一致することを EditMode で確かめる。
4. Runtime の付随 asset: `AttachedAssetDescription`、`SceneResource`、`SceneBase` / `SceneDirector`、index の fallback、テスト（AC10・AC11）。ここで U1 の引き金を確かめる。
5. SampleGame の Scene class と trigger / anchor、asset の宣言（Inspector または一時スクリプト）、Play テストの更新（AC12・AC13）。
6. `pwsh tools/contract-audit.ps1`、Editor の compile 確認。Phase B は content build・Play を実行しない。

### 4.2 Phase B から Phase A へ差し戻す条件

- D5 の Framework 公開面 3 点を超える公開 API、新しい状態・所有者・寿命、asmdef 参照が要る。
- 到達規則を pure に保てない（`SceneResourceMap` や AssetDatabase を resolver から読む必要が出る）。
- 既存 4 build の選択が `840b6609` の policy と一致しない原因が、宣言で表せない規則（例: `SeasonalMode` の特例）にある。
- identity の入力に `GetAssetDependencyHash` 以外の Editor 状態が要る。
- Object の Full fallback が既存 `ContentDirectorySessionTests` の契約を破る。

### 4.3 対象外を維持する方法

- transport・DIST・Storage の schema と key を変えない。識別子は revision 文字列の中身が変わるだけで、形式（`installed/<contentSet>/<revision>`）は変えない。
- Player の bootstrap・smoke・`Application.Quit` に触れない。
- `AssetManagement` の Addressables 側の経路と `AssetKey` を変えない。

## 5. テストとレビュー計画（A1）

- **単体テスト:** `BuildPurposeResolverTests`（AC1〜AC3・AC6）、`BuildTagSelectorTests` の拡張、`BuildContentIdentityTests`（AC7）、`SeasonSceneTagPolicyTests`、`SceneBaseAttachedAssetTests`（偽 `IAssetManagement`、AC11）、publisher の staging 名の path 長（AC14）。
- **差し戻し中の起点 `-Filter`:** `BuildPurposeResolverTests`、`BuildTagSelectorTests`、`SceneResourceContentMaterializerTests`、`BuildContentIdentityTests`、`BuildContentCoordinatorTests`、`SampleGameBuildPurposesTests`、`SeasonSceneTagPolicyTests`、`ContentDirectoryIndexTests`、`BuildContentDirectoryIntegrationTests`、`SceneBaseAttachedAssetTests`、`LocalVfxEventPlayTests`、`SeasonMajorEventPlayTests`、`Bs4FixtureComposerTests`。
- **判定必須テスト:** 最終の全 EditMode 回帰（空 filter）、PlayMode の `LocalVfxEventPlayTests`・`SeasonMajorEventPlayTests`・`VfxScenePlayTests`・Streaming の PlayMode（`-WithGraphics` を併用）。Content Directory build 2 件（`spring-full`、`spring-planner-whitebox`）を Editor で行い、`preflight.json` を照合 script にかける（AC4・AC5）。同じ 2 宣言をもう 1 回 build して AC8 の再利用（`reused=true`）を確かめる。
- **全 EditMode 回帰の適用除外:** なし。
- **統合・Unity テスト:** `BuildContentDirectoryIntegrationTests` の拡張（AC10）、`Bs4FixtureComposerTests`。
- **操作・実行時・目視条件の検証経路:**
  - AC14（担当: 動作確認の実行担当。環境: Editor、`spring-full` と `spring-planner-whitebox` の `Build And Publish` menu。観測: published directory の `transport.json` と `content/`、Console に出た manifest SHA-256。合否は Phase C が manifest の files と directory の照合で決める）。
  - AC15（担当: 同上。環境: Delivery 窓で Local/LAN directory に published directory、manifest SHA-256、contentSet、revision、cache root、budget を入れて Prepare → Use For Next Play。Play して (0,4) Stable、WASD で (4,2) へ移動し Events の trigger 位置へ入る。観測: Console の Error の有無、VFX の表示。証拠: Console ログの保存と screenshot 1 枚）。
  - AC16（担当: 同上。環境: `spring-planner-whitebox` を同様に install し、環境変数 `SAMPLEGAME_WORLD__CELLCOMPANIONSET=Planner` と Delivery の representation `Whitebox` で Play。観測: (0,4) Stable、`SceneDirector` のロード済み一覧に `_Environment_` が無い、Error の有無。証拠: Console ログ）。
  - Phase B の終わりに、trigger の名前変更と付随 asset の宣言が Addressables モードの Editor Play で動くことを 1 回軽く Play して確かめる（AC12 の前提）。
- **未知の操作経路:** (1) Delivery 窓の環境変数（`ContentDeliveryPlayBridge` の ledger）と `SAMPLEGAME_WORLD__CELLCOMPANIONSET` の併用が Editor Play で両方読まれるか。未確認。初回確認は Phase B の終わりの軽い Play（担当: B）。不成立なら AC16 の companion set の与え方を `AppConfig` の JSON に替える（B 適応）。(2) `AssetDatabase.GetAssetDependencyHash` が Scene と `.vfx` で安定した値を返すか（同じ Editor session で 2 回呼んで一致）。未確認。初回確認は B の単体テスト作成時。
- **人間の判断が必要な条件:** なし（AC15 の VFX の表示は Console のログ `LocalVfxEventSession` の起動記録で代替し、screenshot は補助）。
- **高コスト証拠:** BS4 Player build 1 回（AC17）。手順: ルート 96 文字以下の checkout で `Tools/OSM/Content/Build BS4 Spring Player` を実行し、Player を起動して smoke receipt を取る。独立した期待値: receipt が `completed`、`build-receipt.json` の `identity` が 32 hex、Player 内 `content/` の `BuildContentRoot` の entry に `Spring_Events_4_2:Burst` と `Spring_Event_MultiCellProof:Burst` の `Object` entry がある（manifest の `Loadables` が 2 + 2 = 4）。判定 C → 暫定 C' → 高コスト証拠 → 最終証拠ラウンドの順（review-evidence の規則）。
- **判定 C の照合 script の方式:** A3 凍結 script（offline の pwsh）。入力は GO 候補 head で build した `preflight.json` 2 件と、A3 で凍結する期待値（宣言ごとの件数と sorted stable key 列の SHA-256。`840b6609` の policy で得た `spring-whitebox` 124 件に付随 asset 2 件を加えた集合、`spring-planner-whitebox` 69 件）。script は selected の stable key を sorted して件数と SHA-256 を比べ、`_Environment_` 等の禁止 pattern の不在を確かめる。期待値の stable key 列は A3 の前に `840b6609` の Editor で 5 宣言分の `preflight.json` を取って凍結する（担当: A3 の主担当。Editor 操作だけで Unity テストは伴わない）。
- **機械検査:** `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。
- **A0/A1 主担当・モデル・ベンダー:** この session の主担当（Anthropic 系）。
- **A2 独立レビューごとの観点・担当:** 未実施。観点は (a) アーキテクチャゲート（Framework / SampleGame、pure / I/O、公開面の最小性）、(b) 到達規則と不変条件を破る順序の探索（I1・I2 を破る graph と filter の例を 1 つ挙げられるか）、(c) identity と再利用の安全（AC8 の誤再利用の経路）。入力版は本 commit の内容 sha256 を正とする。
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
