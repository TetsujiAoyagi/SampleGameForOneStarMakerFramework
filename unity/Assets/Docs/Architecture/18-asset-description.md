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

- 固定 target は StandaloneWindows64 Player。`artifacts/bs2b/work/<target>/<content-set>/` を
  再 build 用 workspace とし、成功した directory 一式を build identity ごとの
  `artifacts/bs2b/content/<identity>/` へ staging を経て公開する。失敗成果物は成功 path として返さない。
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

### SampleGame の季節選択と Editor build（現況）

`SampleGame/DependOnAll/Editor/Build/` の `SeasonSceneSelectionPolicy` が project 固有の選択を担い、
`SampleGameContentBuild` が本番 `SceneResourceMap` の複製、materialization、selection、Content Directory build を接続する。
Framework の selection core と成果物 schema に季節名を持ち込まない。

- `InGameScene` / `OutGameScene` を graph root とし、親子関係を相互検証する。祖先の `Season_Spring` / `Summer` / `Autumn` / `Winter` から所属を導出し、季節祖先のない content は共通扱いにする。循環、片側だけの親子リンク、異なる季節への重複所属、payload と候補の不一致は選択前に失敗する。階層で表せない所属の明示的な override 入力はあり、通常は空である。
- materializer の `Representation=Full`（空 Variant）と `Whitebox` を使う。季節内で Whitebox 候補がある logical group にだけ `SeasonalMode` を付け、Full のみの補助シーンと共通 content は Whitebox build にも残す。
- requirement は要求した季節と共通 content の payload group に限る。通常は `ExactlyOne`。Full と Whitebox の両候補がある季節 group を同梱する場合だけ `OneOrMore` にし、両表現の候補が採用されたことも照合する。
- Editor メニュー `Tools/OSM/Content/` に全季節 Full、Spring Full、Spring Whitebox、Spring Full And Whitebox の入口がある。選択した必須 group、候補、除外理由をログへ出し、成功 plan と対応 snapshot を既存の `BuildContentCoordinator` へ渡す。専用 EditMode テストと、本番 graph による四つの Content Directory build を確認済み。

この入口は Editor の content 生成用である。生成 directory の Runtime 登録・ロード・寿命管理と Editor Play 接続は BS3、固定 Windows x64 Player build と bootstrap は BS4 の上記経路が担う。

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
