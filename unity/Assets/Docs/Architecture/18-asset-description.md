# 18. AssetDescription — 目的・有用性・実装

本書は `OneStarMaker.Runtime.AssetDescriptions` の `AssetDescription` 系について、**何のために存在し、なぜ有用で、どう実装されているか**を整理する。通常の接続先は Content Directory の materialization / selection / build である。旧 Variant フィルタ BuildScript と Addressables checkout の残存範囲は [§20](20-variant-checkout-workflow.md) を参照する。

---

## 1. 目的

`AssetDescription` は「1 つの論理アセットに対して、複数の差し替え候補（Variant 付き Addressables 参照）をまとめて宣言する」ための仕組みである。

ロード処理の薄い wrapper ではなく、次を担う。

- 1 つの論理アセットに対する複数の `AssetReference` を **Variant 付き**で保持する。
- Editor / Build / Runtime が **同じ Payload 定義**を参照できるようにする。
- Build 時に Payload を列挙できる **共通 API**（`IAssetPayloadProvider`）を提供する。
- ソース（`.asset`）上では **全 Variant を保持**し、通常ビルドでは materialization snapshot と project selection policy から同梱する候補を選ぶ。`BuildVariantProfile` の whitelist は旧 Addressables 互換データであり、通常 Content Directory の選択入力ではない。

### Variant とは何か

Variant は「同じ論理アセットに対する制作・検証用の差し替え候補」を区別する**自由ラベル**。着想は USD の Variant。Framework は名前に意味を持たせず、プロジェクト側が運用を決める。

| 役割 | 使う Variant の例 |
|---|---|
| レベルデザイナー | ホワイトボックスの軽い Scene/Prefab（`Whitebox`） |
| アニメーター | ライティング/重い環境を抜いた Scene（`NoLighting`） |
| ライティングアーティスト | フルセット（`Full`） |
| 実装中 | 仮 Scene / 仮 Prefab / 軽量 Prefab（`Temp`, `Proxy`） |

ソースの空文字 `""` がデフォルト Variant で、Content Directory への materialization では `Representation=Full` に写す。通常の Scene load は directory 内の要求表現が無い場合だけ同じ directory の Full entry へ fallback する。`SceneAssetDescription.Load` の空文字 fallback は Addressables 互換ロード側の処理である。

**重要:** Variant の第一目的は **編集ワークフローの差し替え**であり、実行中の切替 UI ではない。起動時に一度だけ Scene payload Variant を決める配線は実装済み（[§4.8](04-app-startup.md#48-起動時-scene-variant-と職種-companion-set)）。実行中の表現切替は提供しない。

**第二用途: 手元範囲のタグ。** Variant を「どの開発領域のアセットを手元に置くか」を示すタグとしても使える。実行の通常入口は DIST の検証済み installed revision であり、sourceFiles の Missing / Changed は編集可否の案内である。リモート Addressables カタログから欠損を埋めて Play する手順は旧経路である（詳細は [20. Variant チェックアウト厳選ワークフロー](20-variant-checkout-workflow.md)）。

ただし Variant の**本来の軸**は品質・制作段階（`Whitebox` / `Full` 等）であり、領域軸（`OutGame` 等）と 1 つの文字列に無秩序に混在させると運用が破綻しうる。Framework は Variant 名を**完全一致**でしか解釈しないため、命名規約はプロジェクト側で統一すること。

| 用途 | 命名の例 |
|---|---|
| 領域タグ（単独） | `OutGame`, `InGame` |
| 領域 + 品質の複合 | `OutGame_Whitebox`, `InGame_Full` |

本機構の通常入口は Content Directory の選択と起動時の表現固定である。ランタイムで作業者が Variant を切り替える UI ではない。起動時に一度だけ Scene payload Variant を決めて `SceneDirector` へ渡す配線は実装済み（[§4.8](04-app-startup.md#48-起動時-scene-variant-と職種-companion-set)、[§5.4](05-scene.md#54-iloadingdisplayローディング表示)）。実行中の切替口は無い。旧 Addressables カタログ構成は残ファイルとしてあり、通常手順ではない。

---

## 2. 有用性

### メリット

- **Payload 列挙の共通口を持つ。** `IAssetPayloadProvider.Payloads` に参照を集める。通常の production materializer が走査するのは SceneResourceMap であり、新しい Description 型が自動で build 対象になるわけではない。
- **作業者ごとの `.asset` を分けずに済む。** 差し替え候補を Payload リストへ並べ、通常の同梱内容は外側の project selection policy で制御する。
- **欠損や選択不整合を build 前に拒否する。** materialization / selection の構造化 Error があれば成功 snapshot / BuildPlan を公開しない（§4）。
- **ソースは全 Variant を保持。** Git 差分を汚さず、Editor/開発中は全 Variant を参照可能（IK-B3）。

### 限界・注意

- Variant 名の規約は Framework が強制しない。命名はプロジェクト規約として別途決める必要がある。
- Payload は **primary GUID のみ**を宣言する。通常 build は materializer が AssetDatabase 依存閉包を snapshot に固定し、Unity の Content Directory build へ渡す。Addressables 依存解決は互換側の話であり、通常 build の依存収集とは分ける。
- 起動表現と互換 Scene Variant は [§4.8](04-app-startup.md#48-起動時-scene-variant-と職種-companion-set) が所有する。実行中の切替口ではない。
- Editor の World Workspace は runtime fallback を使わず、`SceneResource.GetPayloads()` の ordinal 完全一致だけを要求する。必須 payload が無いときは一件も開かない。S-4b で全216 Cellに空文字と `Whitebox` の payloadを設定済みである。

---

## 3. 実装構造

```
IAssetPayloadProvider   (interface)        BuildSystem が Payload を列挙する共通口
        ▲
        │ implements
AssetDescription        (abstract, [Serializable], NOT ScriptableObject)
        ▲
        │ inherits
SceneAssetDescription   ([Serializable])    SceneResource に埋め込まれる
```

### 型一覧

| 型 | 形態 | 役割 | ファイル |
|---|---|---|---|
| `AssetPayload` | `[Serializable]` class | `AssetReference Reference` + `string Variant`。`[FormerlySerializedAs("SceneReference")]` 付き | `AssetPayload.cs` |
| `IAssetPayloadProvider` | interface | `IReadOnlyList<AssetPayload> Payloads` + `string DisplayName` | `IAssetPayloadProvider.cs` |
| `AssetDescription` | abstract `[Serializable]` class | Payload 列挙の共通基底。**SO ではない**（埋め込み用） | `AssetDescription.cs` |
| `SceneAssetDescription` | `[Serializable]` class（基底継承） | シーン Payload 宣言 + Addressables 互換ロード | `SceneAssetDescription.cs` |
| `ScenePayload` | `[Obsolete]` alias（→ `AssetPayload`） | 後方互換 alias。通常の新規 API には使わない | `ScenePayload.cs` |
| `LoadType` | enum | OnDemand / NecessaryAlways / IncrementalAlways | `LoadType.cs` |

### なぜ ScriptableObject ではなく `[Serializable]` 基底なのか（設計の要）

`SceneAssetDescription` は `SceneResource`（ScriptableObject）に **埋め込まれる**（`SceneResource.cs:22-23`）。
基底を abstract ScriptableObject にすると、`SceneResource` を生成するたびに別ファイルの `.asset` を切り出す必要が生じ、`SceneResourceGenerator` のフローと既存 `.asset` の YAML 構造が壊れる。
そのため `AssetDescription` は **abstract `[Serializable]` クラス**として埋め込み型のまま統一している。独立 SO 化が必要なアセット種別が将来出たら、その型だけを SO として追加すればよい（現状は SceneResourceMap 経由の埋め込みのみ）。

### シリアライズ後方互換

既存 `.asset`（例: `OneStarMakerCommon/SceneMap/Title.asset`）には旧フィールド名 `SceneReference:` が残る。`AssetPayload.Reference` に `[FormerlySerializedAs("SceneReference")]` を付与しているため、リネーム後もデシリアライズで参照が失われない。
注意: `SerializedProperty` のパス（Editor の `FindPropertyRelative`）は `FormerlySerializedAs` の影響を受けないため、Generator 等のコード側は新名 `Reference` を使う必要がある。

---

## 4. BuildSystem との接続

通常の流れは `SceneResourceMap → production materialization → project selection → BuildContentCoordinator → Content Directory` である。Runtime の登録・ロード・寿命、BS4 Player、DIST は後述の各境界が所有する。

旧 `BuildVariantProfile → Collector → VariantWhitelistBuilder → group snapshot/filter → Packed build` は通常入口から切断済みである。残存ファイル・案内のみのメニューは [§20](20-variant-checkout-workflow.md) に集約し、ここから再実行を指示しない。

### ホワイトリスト規則（要点）

以下は旧 whitelist の互換データ規則で、通常 build の選択規則ではない。

- whitelist は完全一致、空ならデフォルト Variant のみ。
- 複数 Variant は同時同梱を表し、fallback の指定ではない。
- 必須 Description の選択を空にしない。空 GUID / null Reference は除外・診断する。
- これらの旧規則を、通常の Content Directory selection policy の代わりにしない。

### Pure content-selection core（現況）

`Editor/Build/Selection/` には、Unity APIやbuild backendから独立した
`OneStarMaker.Build.Selection` assemblyがある。Editor限定だが
`noEngineReferences: true`、`autoReferenced: false`、assembly参照0であり、
`OneStarMaker.Build.Materialization`と`OneStarMaker.Tests.Editor`から参照される。

このcoreは、呼出側がmaterializeした`BuildContentCandidate`、project定義の
`BuildTagSchema`、`BuildSelectionPolicy`、`IBuildTagProvider`を受け取り、
immutableな`BuildPlanResult`を生成する。candidate探索、AssetDatabase、
Addressables、Content Directories、VCSへのアクセスは行わない。

- tagを持たないcandidateはneutral contentとして選択対象になる。
- 同一dimension内のrequest値はOR、candidateが持つ複数dimensionはANDで照合する。
- unknown dimension/value、candidate identity衝突、tag競合、required group欠落、
  cardinality違反は構造化issueになる。
- Errorが1件でもあれば`BuildPlan`を公開しない。Warningだけならplanを公開する。
- request、candidate、provider、tagの入力順に依存せず、plan、issue、provenanceを
  ordinal順のcanonical snapshotとして保持する。
- FrameworkはSeason、Scene role等のproject固有語彙を解釈しない。

### SceneResource の production materialization（現況）

`Editor/Build/Materialization/` の `OneStarMaker.Build.Materialization` は Editor-only の
production adapter である。`SceneResourceContentMaterializer` が `SceneResourceMap` と
AssetDatabase の現況を検証し、成功時に `BuildMaterializationSnapshot` を返す。
Selection core は引き続き Unity / AssetDatabase / Addressables を参照しない。

- `SceneResource.Identity` を logical key、canonical lowercase 32桁 GUID を physical key とする。
  stable key は version付きの長さ前置 tuple で、列挙順や表示文言に依存しない。
- 空の payload variant は `Representation=Full`、非空は大小文字を変えずそのままタグへ写す。
  有効な各 SceneResource に `ExactlyOne` requirement を置き、provenance に root GUID と path を保持する。
- root 自身を含む AssetDatabase 依存閉包を root ごとに正規化・整列して snapshot に固定する。
  GUID/path の不一致、欠損、衝突などは構造化 issue になり、issue があれば部分 snapshot を公開しない。
- AssetDatabase I/O は狭い gateway に隔離し、mapping・依存閉包の正規化とは責務を分ける。
  tag provider は snapshot 所有で、未知 candidate には空のタグ集合を返す。

既存の `VariantWhitelistBuilder` と Addressables build 経路は通常入口から切断した。メニューと CLI は
置換案内のみで group mutation を実行しない。SampleGame の Editor 入口は本番の `SceneResourceMap` を
materialize し、後述の project policy で選択して Content Directory build へ渡す。季節 policy のあと
bootstrap Scene と SceneResourceMap を compose する。Runtime の directory 管理は BS3、Player build と
Player bootstrap は BS4、配信は DIST で実装済みである。
materialization や Editor build の成功だけをこれらの成立と同一視しない。
`FindDependency` の lookup key は canonical lowercase GUID を渡す契約である。

### 選択済み content の Content Directory build（現況）

`Editor/Build/Content/` の `BuildContentCoordinator` は成功した `BuildPlan` と対応する
`BuildMaterializationSnapshot` を受け取る。純粋な projection が stable / logical / physical key、
root と依存閉包を照合し、欠損・衝突を build 前の構造化 issue にする。選択 policy と依存閉包は
再計算しない。Unity API を使う root 生成と build は Editor adapter に隔離している。

- 固定 target は StandaloneWindows64 Player（`BuildContentCoordinator.TargetName` は `StandaloneWindows64-Player`）。
  `artifacts/bs2b/work/StandaloneWindows64-Player/<content-set>/` を再 build 用 workspace とし、成功した directory 一式を build identity ごとの
  `artifacts/bs2b/content/<identity>/` へ staging を経て公開する。失敗成果物は成功 path として返さない。
- build mode（`BuildContentRequest.Mode`、既定 `Incremental`）。`Full` は adapter を呼ぶ前に workspace の中身を消す
  （workspace 自体は残す）。approved root から workspace までの各 segment と中の directory に reparse point
  （junction・symbolic link 等）が 1 つでもあれば、何も消さずに build 前の失敗にする。他 process の lock で
  消せない場合も Unity の build を始めない。mode は `outcome.json` の `buildMode` に記録する。
  `Incremental` は workspace を再利用する日常の build 用で、publish には使えない（次節）。
- 生成した単一 `BuildContentRoot` に schema version、build identity、target と entry 配列を保存する。
  entry は logical key、stable key、`Representation`、Scene / Object 種別と Unity の
  `LoadableSceneId` / `Loadable<UnityEngine.Object>` を持つ。複数表現を同一 directory に格納できる。
  ファイル GUID 単独を Object の load identity としない。
- preflight report は選択・除外理由・root・閉包・issue、outcome report は成否・summary・公開 path・
  `BuildManifestHash.txt` と metadata directory を同じ identity に結び付ける。Unity 内部ファイルを
  OSM の公開 protocol として解釈しない。
- Scene、Prefab、Texture と同じ logical key の High / Low 表現を含む fixture で実 build を確認した。
  同じ workspace で 2 回 build し、公開 directory の移設後に PlayMode で単一 root を登録・発見した。
  この build 側 fixture だけでは本番非 Scene Description、型付き load、サブアセット一般化、
  Runtime の所有・解放、Player への接続を証明しない。型付き Runtime load と寿命は次節の BS3 で検証した。

### Runtime の Content Directory 解決とロード（現況）

`Runtime/BuildContent/` の `ContentDirectorySession` は、移設済み local directory を
登録して単一の `BuildContentRoot` を発見する。schema、build identity、target、entry と
Unity locator を登録時に検証し、`ContentDirectoryIndex` は選択済み entry だけを
`logical key + representation + kind` で索引化する。schema v1 の未記録 representation
は `Full` として扱う。Scene は要求表現が欠ける場合だけ `Full` entry へ fallback し、
Object は完全一致で解決する。Unity の実型を load 後に検証し、欠損・曖昧選択・型不一致は
`ContentDirectoryException` の reason code で返す。source の AssetDatabase や
materialization を Runtime で再実行しない。

`IAssetManagement` の `LoadContentAssetAsync<T>`、`LoadContentSceneAsync`、
`InstantiateContentAsync` が型付きの入口である。既存の Addressables 入口は維持する。
`AssetManagement` が owner 台帳と resident cache を所有し、directory token は session の
利用権を保持する。取消した caller の代わりに native operation の終端まで session が追跡し、
明示 close では live resource の解放を待ってから unregister する。同一 process の
revision 削除は `ContentRevisionGate.TryAcquireDelete` の lease を必要とする。

Editor Play は Delivery の verified pair、または明示 `content:runtimeMode=directory` で
この経路を使い、起動時に一度選んだ表現を Scene lifecycle に渡す。未指定は pair が無ければ失敗する。
明示 `addressables` だけが Addressables 互換口である。
directory path、build identity、representation の欠損や不整合は起動失敗であり、
Addressables へ暗黙 fallback しない。SampleGame の Content build は季節選択のあと、UICommon Scene と
SceneResourceMap を bootstrap content entry として同じ directory に合成する。DIST の transport
`files` 規則と install レイアウトは変えない。
directory Player は build transaction が生成した bootstrap Scene、
graph metadata、runtime config を使い、対応 Content BuildReport directory を Player build へ渡す。
Player Scene は bootstrap 一件に固定し、選択済み content root GUID の packed assets 混入を拒否する。
publish 後の shipping tree は content root 一件だけを持ち、Unity の backup directory は除外する。
物理削除、取得済み成果物だけからの起動、別 process の OS lease は DIST が所有する。disk cache と known-good の契約は `13-resource-system.md`、installed override と Player 起動は `04-app-startup.md` を正とする。
同一 session 内で Whitebox 要求を Full entry へ fallback した後に、同じ Scene identity を
Full として別要求すると台帳の要求表現が異なり `EntryAmbiguous` になり得る。現行の
Editor Play と directory Player は起動時に表現を固定する。同一 session の表現切替は提供しない。
切替が必要なら別スライスで設計する。

### 配信用 transport の公開境界

`ContentTransportPublisher` は成功した Content Directory build result と preflight の identity を照合し、
公開 directory の opaque files と OSM transport manifest v1 を staging へ生成して公開する。
publish は outcome の `buildMode` が `Full` の result だけを受け付け、それ以外（欄の無い旧 outcome を含む）は
publish 先も staging も作らずに `InvalidOperationException` で拒否する。staging は
`<final の親>/.staging/<guid32>` に作り、staging の各 path が final の同じ file の path より長くならないようにする
（同じ volume の move で確定し、失敗時は staging だけを消す）。
BuildReport の内容を配信層で独自に再検証するものではない。manifest の `files` は配信する content subtree の
完全な集合であり、各 path、size、SHA-256 を記録する。`sourceFiles` は build 時の選択依存閉包を
path/hash で案内する metadata であり、転送先 path、Unity load identity、Runtime 起動条件には使わない。

consumer は manifest の contentSet/revision、固定 target `StandaloneWindows64-Player`、互換 schema と
受信 bytes の digest を照合する。配信層は logical key、Variant、Unity Object の依存解決を再実装せず、
install 後の `content` directory を既存の `ContentDirectorySession` へ渡す。manifest の revision は
既存 BuildIdentity と同値であり、revision 文字列の大小を鮮度の根拠にしない。

source 診断は取得済み install の固定 digest を再検証してから `Assets/` / `Packages/` の案内対象を
Missing / Changed / Complete に分類する。Missing / Changed は source を編集できるかの案内であり、
検証済み content からの Player 起動を拒否する条件ではない。未 checkout Scene を Hierarchy で
直接編集できることは意味しない。

### 目的の宣言からの選択（現況）

build の目的は `BuildPurposeDeclaration`（`OneStarMaker.Build.Selection`、pure）1 つで表す。欄は `Name`（contentSet と同じ規則）、`Roots`（logical key の列）、`Filter`（OnDemand の子に適用する tag の述語）、`Representation`（優先順の列と `First` / `All`）、`RuntimeDefaults`（Game が決める不透明な key=value。`content:` で始まる key は拒否）である。target・compression・defines 等の設定は宣言の欄にしない。

`BuildPurposeResolver` は graph node（`BuildGraphNode`。SceneGraph に固有ではない）と node 水準の tag（`IBuildNodeTagProvider`。Game が供給）と宣言から requirement を作る。

- 到達規則: `required(n) ⇔ n ∈ roots ∨ (required(parent) ∧ (LoadType ≠ OnDemand ∨ filter(tags(n))))`。filter の述語は `BuildTagSelector` の request と実装を共有し、2 つ目の解釈を作らない。payload の無い中間 node も到達の経路になる。
- graph の検証: identity の重複、root の不在、orphan、親子リンクの不一致（多親）、全 node から祖先をたどるサイクル検査。到達しない島のサイクルも issue で止め、`Unreachable` として黙って通さない。
- requirement は (logical key, 表現) ごとの `ExactlyOne`。表現は node の宣言済み payload から選ぶ（`First` は優先順で最初の 1 つ、`All` は優先順にある全て）。期待集合は宣言から、存在と一意性は materialize 済み候補で検査するので、候補が欠けても合格しない。
- requirement ごとに root からの到達経路を持ち、`preflight.json` の後方互換の追加項目 `requirements[]`（`logicalKey` / `representation` / `path`）に「なぜ入っているか」が残る。除外は `excluded` の理由 code（`Unreachable`、`RepresentationNotPreferred`）に残る。
- selector 側は `BuildSelectionPolicy(excludeUnrequired, representationDimension)` で、requirement の無い logical key の候補を `Unreachable` で除外し、cardinality を (logical key, 表現) で数える。

宣言は `BuildContentRoot.Declaration` と transport manifest の `declaration`（どちらも任意 field。schema version は変えない）へ写る。directory mode の起動はそれを config と突き合わせる（[§4](04-app-startup.md)）。Framework は `content:*` 以外の key の意味を解釈しない。

### 付随 asset（現況）

Scene payload ではない asset（`.vfx`、共有 material 等）は、所有する `SceneResource` に `AttachedAssetDescription`（名前と payload 列。埋め込み `[Serializable]`）として宣言する。payload の無い node（例: `InGameSession`）も宣言できる。

- materializer は付随 asset ごとに logical key `<所有 identity>:<名前>` の候補と閉包を作る。所有 identity は候補の `Owner` metadata で、selector の tag にはしない。所有 node が到達すれば選ばれ、Content Directory に `Object` entry として入る。
- 同じ physical GUID を宣言できる node は 1 つだけ。共有は祖先に 1 回宣言し、子孫が所有者を指定して読む。名前の重複・空名・参照欠損・node identity との衝突は materialize の issue になる。
- 読む側は `SceneBase.LoadAttachedAssetAsync<T>(name)` / `(ownerIdentity, name)`。`ownerIdentity` は自分か祖先に限る（読む Scene が到達するなら祖先も到達するので、directory に必ずある）。directory mode では `LoadContentAssetAsync(<owner>:<name>, sceneVariant, Scene(自分))`、Addressables mode では `AssetKey.FromDescription` 経由で同じ呼び出しが解決する。所有者は常に読む Scene で、寿命と回収順序は従来の `Scene(id)` 所有と同じ。
- `ContentDirectoryIndex.TryResolveObject` は Scene と同じ Full fallback を持つ。
- `.vfx` と material の Addressables entry は Addressables mode の読み口が要するので残している。

### SampleGame の宣言と Editor build（現況）

`SampleGame/DependOnAll/Editor/Build/` の `SampleGameBuildPurposes` が 6 つの宣言（`all-full`、`spring-full`、`spring-whitebox`、`spring-full-whitebox`、`bs4-spring-full`、`spring-planner-whitebox`）を持ち、`SeasonSceneTagPolicy` が node 水準の tag を導く。`SampleGameContentBuild` は宣言を受けて本番 `SceneResourceMap` の複製、materialization、resolver、Content Directory build を接続する。Framework の selection core と成果物 schema に季節名を持ち込まない。

- root は全宣言で `InGameScene` / `OutGameScene`。filter は `Season` と `Role` で、tag の無い dimension は中立。`Season` は祖先の `Season_*` から、`Role` は SampleGame.InGame の `CellCompanionRoleClassifier` から導き、規則を複製しない。
- 各宣言の Role の filter は `RuntimeDefaults` の `world:cellCompanionSet` から `SeasonSceneTagPolicy.RolesIncludedBy`（SampleGame.InGame の `CellCompanionPolicy` から導出）で作る。`spring-planner-whitebox` は Planner（Role = Events のみ）、表現 `First [Whitebox, Full]` で、Environment・Cell Lighting・Cell VFX を含まない 70 件になる。
- 表現の対応: Full = `First [Full]`、Whitebox = `First [Whitebox, Full]`、Full と Whitebox の同梱 = `All [Full, Whitebox]`。
- Editor メニュー `Tools/OSM/Content/Build <contentSet>` が宣言ごとにある。BS4 coordinator は `bs4-spring-full` 宣言を引数に呼び、Player の baked config（`cellCompanionSet`・representation）を宣言から写す。bootstrap composer と BS4 の fixture composer は resolver の後に合成する。
- 付随 asset の実例は `Spring_Events_4_2:Burst`・`Spring_Event_MultiCellProof:Burst`（`.vfx`）と `InGameSession:Lit`（`DemoCellLit.mat`）。
- menu の `Build <contentSet>` は `Incremental` で build し、publish しない。BS4 coordinator と batch 入口は `Full` で build して publish する。
- batch 入口 `SampleGameContentBatch.BuildAndPublish` は `-batchmode -executeMethod … -osmPurpose <宣言名>` で呼び、宣言を `SampleGameBuildPurposes.Find` で引いて `Full` build と publish（`artifacts/bs2b/transport/<identity>`）を行う。結果 file `artifacts/bs2b/batch/last-result.json`（identity・publishPath・manifestSha256・declaration・buildMode・exitCode）は開始時に消し、一時 file から move で確定する。終了コードは 0 成功、2 引数の誤り、3 選択の issue、4 Unity build の失敗、5 publish の失敗、1 その他で、全ての分岐で `EditorApplication.Exit(code)` を呼ぶ。0 以外では結果 file に `exitCode` と理由だけを書き、その実行が作った published directory だけを消す（結果 file の確定の失敗を含む）。
- 成否の正は process の終了コードで、呼ぶ側は終了コード 0 の時だけ結果 file を読む（Unity が method を呼ぶ前に終わる場合、たとえば project の lock や compile error では、前回の結果 file が残る）。結果 file の欄は BS5b-2 の From published directory が期待値として読む。pwsh の wrapper は持たない。
- **既知の制限（2026-11-03）:** 成功した batch の後に Unity process が終了しないことがある（Unity 6000.6.0f1 の既知の native 終了 stall。[Harness README](../../../../tools/Harness/README.md#unity終了時の既知native-stallと調査の停止規則)）。clean checkout の batch（約 23〜29 分）では、結果 file の確定と publish の log の後、30 分待っても process handle が signal しなかった。CI が成功した batch を終了コードだけで判定できることは未実証である。失敗経路の実 process の終了コード（未知の宣言名で 2）は観測済み。

この入口は Editor の content 生成用である。生成 directory の Runtime 登録・ロード・寿命管理と Editor Play 接続は BS3、固定 Windows x64 Player build と bootstrap は BS4 の上記経路が担う。batch 入口と publish の `Full` 必須は BS5b-1、宣言からの Play 経路は BS5b-2（下記）で上の経路に入った。

### 宣言からの Play 経路（現況）

同じ宣言を日常 Play・検証 Play・published directory の受け取りに接続する。宣言を 1 つ選ぶと mode・表現・runtime 既定値・成果物が手入力なしに揃い、揃わない時は別の経路で黙って動かずに拒否する。経路は全て `SampleGame/DependOnAll/Editor/Build/` にあり、Framework は宣言の意味を知らない。

- **Play As**（`Tools/OSM/Play As/<宣言名>`、`PlayAsLauncher`）: `PurposeLaunchSettings.PlayAs` が宣言を config の key=value と環境変数名（`SAMPLEGAME_` + key の `:` を `__` にした大文字）へ写し、`content:runtimeMode=addressables`・表現（`Full` は `assets:sceneVariant` を置かない）・runtime 既定値を Editor process の環境に置いて Play する。Play の前に、Play mode・compile 中、Variant profile の選択（Production の `""` を含む）、制御する key の外の値、コマンドラインの同じ key、`Full` で app-config の下位の層が空でないこと、Addressables の Play Mode が `BuildScriptFastMode` でないことを拒否する（[§4.8](04-app-startup.md#48-起動時-scene-variant-と職種-companion-set)）。
- **共通の ledger**（`ContentDeliveryPlayBridge`、EditorPrefs `OSM.ContentDelivery.PlayBridge.v1`）: Delivery 窓の「Use For Next Play」（手で戻す）と Play As・検証 Play（1 回の Play で戻す、欄 `resetAfterOnePlay`）が同じ ledger に元の値を残す。1 回の Play で戻す ledger は `EnteredEditMode`、または `isPlaying` と `isPlayingOrWillChangePlaymode` が両方偽の idle で戻す（`ExitingPlayMode` の間は `isPlaying` だけが真なので戻さない）。手で戻す ledger が残っている間は 1 回の Play の経路を拒否し、欄の無い旧 ledger は手で戻す ledger と読む。
- **検証 Play**（`Tools/OSM/Content/Verify Play/<宣言名>`、`VerifyPlayLauncher`）: `VerifyPlaySequence`（Unity 非依存の段の列）が `Full` build → publish → manifest の宣言の照合 → install → bridge → `EnterPlaymode` の順に進み、bridge 以降の失敗では ledger を戻す。2 つの入口は 1 つずつしか走らない。開始前に、起動時に開いている Scene が content directory の代わりに再利用される場合（SceneResourceMap の Scene 名、`UICommon`）を拒否する（menu からは Scene を閉じて空 Scene で続けるかを確認する）。install の後と bridge の直前に取り消しを確かめる。
- **From published directory**（Delivery 窓の Published directory モード）: batch の結果 file（`last-result.json`）を選び、`transport.json` の SHA-256 → identity → 宣言の順に照合してから install し、検証 Play と同じ段で Play する。照合に落ちた directory は install しない。窓は標準経路の実効 cache root を表示し、窓を閉じると進行中の受け取りを取り消す。
- **初回 Scene:** 宣言ごとの起動 Scene は App が決める（`SampleGameBuildPurposes.FirstSceneOf`。今は 6 宣言とも `Title`）。検証 Play の bridge は `content:firstScene` を置き、Runtime の Editor directory 分岐がそれをロードする（[§4.6](04-app-startup.md#46-play-from-any-sceneeditor-対応)）。BS4 coordinator も同じ関数を使う。
- **観測の注意:** `eval` で Editor を操作して検証する時、build 中や Play mode の移行中に送った `eval` は main thread が塞がれて 5 秒で timeout し、Pipeline が `Failed to handle /api/exec request: Main thread operation timed out after 5000ms` を Console の Error として出す。印で切った区間の Error を数える時はこの行を App の Error と区別して読む（BS5b-2 の判定で採った読み）。


### BuildSystem の後続（BS5a・BS5b-1 から送った事項）

- **S-4d5b から（Planner 実証未達、2026-11-03）:** Content Directory の上の Planner Play（検証 Play `spring-planner-whitebox`: `content:representation=Whitebox`・`world:cellCompanionSet=Planner`）の実証は NO-GO で閉じた。S-4 の目的を達成した完了ではない。
  - 引き取った後続: BuildSystem program §7 の「directory mode の rig の spawn と Planner 実証」の行（2026-11-07 完了、PR #160）。
  - 未達の条件: Title の Start の後に `InGameSession` と Spring `(0,4)` が Stable になり、`Player ready at Cell stream spawn` が出て、rig の `Rigidbody.position` が `WorldCellCatalog.SpawnPosition()` から 1 m 以内にあること。
  - 観測した欠陥: 1 回の検証 Play で、rig が `(0,0,0)` に留まり（spawn `(125,28,1125)` から 1132 m）、`Spring_Cell_0_4` が loaded の直後に unload されて `Spring_Cell_0_0` が載り、`Player ready` が約 3 分出なかった。Start は reflection で `TitleView.OnStartRequested` を呼んで発火していた。rig を spawn に置き直した後の診断では、Whitebox の Cell (4,2) と Events の load、local trigger の VFX 1 件の出現と Cell の unload での回収、major Event `Spring_Event_MultiCellProof` の VFX 2 件の出現と完了での回収が動き、Environment / Cell 単位の Lighting・VFX は build の選択にもロードにも現れなかった（診断であって実証ではない）。一次証拠は判定を行った PC の `artifacts/s-4d5b-phase-c-verdict/`（git 管理外、`SHA256SUMS.txt` 付き。identity `20261103T091410930Z-d377543307574cafa829b30d94255025`、製品は `fb97da0d`）。経緯は commit `771bf66a` 時点の S-4d5b の HANDOFF の §4.2・§5・§7.3（Phase D で削除済み）。
  - 原因（後続の A1 で特定）: `FlyController.Teleport` が `transform.position` だけを書き、補間つき（`Interpolate`）の Rigidbody の位置を書かなかった。Teleport の次のフレームで transform が Rigidbody の古い位置（原点）へ戻り、距離 streaming の最初の Tick が原点を Focus として読んで `Spring_Cell_0_4` を外し `Spring_Cell_0_0` を載せた。Title の `start-button` の submit でも reflection の Start でも、directory mode でも Addressables mode でも同じ現象で、mode に依らない。transform の書き込みが Rigidbody に入らない内部の機構は未解明。`Teleport` は `Rigidbody.position` と `transform.position` の両方を書くように直し、補間つきの Rigidbody で Teleport の後の毎フレームの位置を確かめる EditMode テスト（`FlyControllerTeleportTests`）を足した。
  - `Player ready` が出なかった原因（同じ A1）: bootstrap は止まっておらず（`Flight registered` と入力 ON は起きていた）、行がログから落ちていた。`PlayerScene` がこの行の値に `Vector3` を渡し、App のログの JSON formatter が値を書けずに行ごと黙って落とす（機構は [§12 §3.2](12-telemetry.md#32-ギャップ) の G5）。S-4d5b の Play と後続の A1 の全てのログで、この行は 1 度も出ていない。行の値は 3 成分の `float` に直した（文言は変えていない）。直した後の下の再検証の Play では、この行が 3 成分つきで App のログに出た。
  - 再検証の条件: 後続の修正を含む head で、untitled の空 Scene から検証 Play `spring-planner-whitebox` を 1 回行い、上の未達の条件に加え、`preflight.json` で `Spring_Cell_4_2` が Whitebox の 1 件だけ、local trigger と major Event の VFX の出現と回収、Environment と Cell 単位の Lighting・VFX が選択にもロードにも 0 件、`ContentDeclarationMismatch` と App の Error が 0 件であることを、同じ 1 回の Play で確かめる。App の Error の 0 件は、上の formatter の欠落が残る間は「観測できた行の Error が 0 件」の意味に限る。
  - 再検証の観測（program §7 の行の作業、2026-11-06。製品コードは上の 2 つの修正を含む head）: 上の条件どおりの 1 回の検証 Play（Title の `start-button` の submit で Start）で、次を観測した。`IsSceneStable("InGameSession")` が true、`Spring_Cell_0_4` が Stable（App のログの `OnStabledImpl`）で、Start から最初の診断の移動までの間に unload が 0 件、`Spring_Cell_0_0` の load が全区間で 0 件。`Player ready at Cell stream spawn (125.0, 28.0, 1125.0)` が出て、その直後と約 1.8 秒後の 2 点で rig の `Rigidbody.position` と `transform.position` が spawn と一致した。`preflight.json` の `Spring_Cell_4_2` は Whitebox の 1 件だけで、Full は `RepresentationNotPreferred` で除外された。local trigger の VFX（`Spring_Events_4_2_Burst`）と major Event の VFX（`Spring_Event_MultiCellProof_Burst`）が trigger への進入から 1 秒以内に現れ、rig を戻した後と完了の後に消えた。Cell 単位の Environment・Lighting・VFX は選択にもロードにも 0 件、`ContentDeclarationMismatch`・封鎖の拒否・観測できた App の Error 以上は 0 件。scene の load / unload は Play に入る直前に掛けた Editor の hook で数えたので、その登録より前の区間は観測していない（登録は Title の load より前）。「全ての streaming の Tick で Focus が spawn に在った」ことは観測していない。
- **BS5b-2 から（凍結条件の違反ではない、2026-11-03）:**
  - テストの隙間: AC2 の 2 つの拒否（profile の選択、Addressables の Play Mode が `BuildScriptFastMode` でない）を呼ぶテストが無い。`RunsPlayerContentSmoke` のテストは恒等で分岐の反転を落とさず、Editor の初回 Scene のロード失敗（別 Scene へ逃がさない）は注入テストが無い。初回 Scene のテストが `"Title"` と等値で比べる（「決まっている」までに弱めてよい）。窓の `OnDisable` の取り消しは EditMode が無い。
  - 局所の整理: `ContentDeliveryPlayBridge.Reset` の Play 状態の検査が `isPlayingOrWillChangePlaymode` だけで、`Apply` と揃っていない（`ExitingPlayMode` の窓で明示の Reset が通る）。`SampleGameBuildPurposes.FirstSceneOf` が `public`（呼び手は同じ asmdef）。`PurposeLaunchSettings` と `ContentDeliveryPlayBridge` が class の間で互いを参照する。Play As の app-config の下位の層の検査は相対 path で読み、file が無ければ空として通す。宣言の照合（`CompareDeclaration`）は記録側の重複 key と手元の別 key の欠落の組を通す（今の宣言の形では起きない）。`OnUpdate` が手で戻す ledger の間も毎 update で JSON を parse する。menu の経路で、直せない拒否より先に Scene を閉じる・ledger を戻す dialog が出る。
  - 既定 cache root の `installed/<contentSet>` が空になっていた観測（From published directory の検証 Play の後。install は成功していた）: 元の観測の手順と、見た root の絶対 path は残っておらず、事後に因果は確定できない。事実として、既定 cache root（`%LOCALAPPDATA%\OSM\ContentCache`）には空の `installed\spring-full` だけがあり、その子の作成・更新時刻が同じ日の EditMode 全件回帰で `PublishedDirectoryReceiveTests` の既定 root への install のテストが走った時刻と一致した。このテストは既定 root に `spring-full/<テスト用 revision>` を install し、teardown で自分の revision だけを消すので、空の `spring-full` が残る。最有力の仮説は、テストが既定 root に残した跡を From published directory の結果と読み違えたこと。install した revision の寿命は [§13「DIST transport と disk cache の境界」](13-resource-system.md#dist-transport-と-disk-cache-の境界) のとおり、次の install の容量確保と明示の Evict でだけ消え、Play の終了・ledger の Reset・窓を閉じることでは消えない。S-5 の W-6（検証済み installed revision だけを登録）の前提とは、この条件つきで矛盾しない。テストは既定 root へ install しないように直した（`EmptyCacheRoot_ResolvesToTheDefaultRoot` は解決の値の比較だけ）。このため保証は、既定 path の解決（このテストと `PurposeLaunchSettingsTests`）と、任意に指定した root への install（Framework の `ContentInstallerTests` 等の一時 root）の組み合わせに縮み、**既定 root への実 install はどのテストも確かめない**。program §7 の「directory mode の rig の spawn と Planner 実証」の行の作業の検証 Play（上の S-4d5b の行の再検証の観測と同じ Play。cache root は Delivery 窓に設定した非既定の root）では、`VerifyPlayLauncher` が install と報告した revision root が、Play を止めた後も `receipt.json` とともに残っていた。この観測は、その 1 件の revision root が停止後も在ったことに限り、全ての revision の寿命や、既定 root での振る舞いは主張しない。
  - 未調査の観測: From published directory の検証 Play の間に `There are 2 audio listeners in the scene` の Warning が多数出た（検証 Play `spring-full` の run では 0 件）。clean worktree の batch の 1 回目が CoreCLR のメモリ確保失敗（`0x8007000E`）で落ち、同じコマンドの 2 回目は通った。
- **CI 組み込み（BS5b-1 から）:** 成功した batch の後に Unity process が終わらない場合の、呼ぶ側の終了監視・timeout・結果 file と manifest の照合。R2・HTTP への publish と別 PC での受け取り。`.staging` と `work/` の残置の清掃は Storage program §6「ローカルの残置の清掃」。
- **入力 identity:** 宣言と選択候補の source 状態と設定 snapshot の hash、`content/index`、build を skip する再利用。revision の形式は変えていない。
- **未着手の問い:** 経路の連鎖（source → local の object store → LAN / R2）、object 単位の格納と差分更新、再 build の決定性、Player の実行時差し替え、複数 directory と cross-directory 参照、設定の宣言化と Player の Debug の剥離、known-good の pin の呼び手、日常 Play で宣言に無い content を読んだ時の検出、`outcome.json` の選択・閉包の記録形式、directory に無い OnDemand content を要求した時の振る舞い。
- **検証の残り:** directory mode の封鎖と宣言の不一致 Warning の配線には自動テストが無い（Editor Play と BS4 Player のログで観測済み）。Player の `content/` の manifest は `Object` entry を `ObjectIdHash` でしか持たないので、付随 asset を名前で同定する材料が無い。

---

## 5. 拡張ガイド

### 新しい Variant を運用する

Scene 側の Payload リストに参照と Variant を宣言し、通常経路では materialization と project selection policy が要求する表現を選択できるか確認する。現在の SampleGame の標準入口は Full / Whitebox（§4）であり、任意の名前を旧 whitelist に足すだけでは通常 build / Play へ接続されない。新しい選択規則が必要なら別スライスで採否を決める。選択済み content を build・install し、起動表現を固定する手順は [§20](20-variant-checkout-workflow.md) と [§4.8](04-app-startup.md#48-起動時-scene-variant-と職種-companion-set) を使う。

### Scene 以外のアセット種別を AssetDescription 化したくなったら

現行の production materialization は SceneResourceMap を入力にする。Prefab / Texture を含む fixture build の成功は、本番の非 Scene Description の探索・選択が実装済みであることを意味しない。実需要が出たら、型と探索 adapter、logical / physical identity、依存閉包、選択要件の境界を別の実装 HANDOFF で決める。旧 `AssetDescriptionCollector` / `IAssetDescriptionSource` への登録だけを通常 Content Directory の拡張手順として使わない。
- 注意: 計画段階で Prefab/Audio/Texture/Generic の個別 Description 型を作ったが、**実需要が出るまで作らない方針で剪定済み**。「あるけど使われない型」を増やさないこと。
  AssetType 自体は `AssetKey` のメタ情報として採用済みで、カテゴリ別 cache / budget の次パスで使用する。

### 起動時の Scene Variant

起動時の一回解決は実装済み（[§4.8](04-app-startup.md#48-起動時-scene-variant-と職種-companion-set)）。通常は `content:representation`、`SceneAssetDescription.Load(variant)` は Addressables 互換側である。実行中切替や第二の解決源は作らない。Play / Player の再起動が切替である。

---

## 6. 既知の制約・落とし穴

- transport v1 の SHA-256 pin は bytes 同一性を保証するが、配信元の真正性を単独では保証しない。署名、認証、latest channel、delta/resume、CDN、他 target は後続範囲である。
- sourceFiles は案内 metadata である。Runtime は source AssetDatabase、materialization、checkout report を再走査しない。
- `AssetDescription` を SO に変えてはいけない（埋め込み構造が壊れる、§3 参照）。
- フィールド名変更時は `[FormerlySerializedAs]` を必ず付け、Editor 側の `FindPropertyRelative` は新名へ追従させる。
- Payload は primary GUID のみ宣言。通常 build の子依存は §4 の materialization snapshot と Content Directory build で扱う。
- 旧 group snapshot/filter は通常入口から切断済み。旧経路の中断時堅牢性はここで検証済みとせず、再利用する場合も別途確認する。通常 build の前提作業として旧経路を復活させない。
- `ScenePayload`（Obsolete alias）は実質未使用。削除候補。

---

## 7. 関連ファイル

- Runtime: `unity/Assets/OneStarMaker/Scripts/Runtime/AssetDescriptions/`
- BuildSystem: `unity/Assets/OneStarMaker/Scripts/Editor/Build/`
- Pure selection: `unity/Assets/OneStarMaker/Scripts/Editor/Build/Selection/`
- Production materialization: `unity/Assets/OneStarMaker/Scripts/Editor/Build/Materialization/`
- Content Directory build: `unity/Assets/OneStarMaker/Scripts/Editor/Build/Content/`
- Build content root と Runtime directory owner / index / gate: `unity/Assets/OneStarMaker/Scripts/Runtime/BuildContent/`
- SampleGame selection と Editor 入口: `unity/Assets/SampleGame/DependOnAll/Editor/Build/`
- Scene 連携: `unity/Assets/OneStarMaker/Scripts/Runtime/SceneSystem/SceneResource.cs`, `SceneResourceMap.cs`
- 既存資料: [13. リソースシステム](13-resource-system.md)（AssetType は cache 用メタとして採用済み。AssetResidentCache(常駐キャッシュ + per-category budget)実装済み）
- ワークフロー: [20. Variant チェックアウトワークフロー](20-variant-checkout-workflow.md)

> 注: 旧「17. Variant BuildScript レビュー」はファイル未保存のまま失われたため欠番（git 履歴にも存在しない）。
