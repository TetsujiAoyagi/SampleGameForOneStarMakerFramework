# 18. AssetDescription — 目的・有用性・実装

本書は `OneStarMaker.Runtime.AssetDescriptions` の `AssetDescription` 系について、**何のために存在し、なぜ有用で、どう実装されているか**を整理する。Variant フィルタ BuildScript（[20. Variant チェックアウトワークフロー](20-variant-checkout-workflow.md) で使用）の前提知識でもある。

---

## 1. 目的

`AssetDescription` は「1 つの論理アセットに対して、複数の差し替え候補（Variant 付き Addressables 参照）をまとめて宣言する」ための仕組みである。

ロード処理の薄い wrapper ではなく、次を担う。

- 1 つの論理アセットに対する複数の `AssetReference` を **Variant 付き**で保持する。
- Editor / Build / Runtime が **同じ Payload 定義**を参照できるようにする。
- Build 時に Payload を列挙できる **共通 API**（`IAssetPayloadProvider`）を提供する。
- ソース（`.asset`）上では **全 Variant を保持**し、ビルド時だけ `BuildVariantProfile` のホワイトリストで catalog に入る Variant を制限する。

### Variant とは何か

Variant は「同じ論理アセットに対する制作・検証用の差し替え候補」を区別する**自由ラベル**。着想は USD の Variant。Framework は名前に意味を持たせず、プロジェクト側が運用を決める。

| 役割 | 使う Variant の例 |
|---|---|
| レベルデザイナー | ホワイトボックスの軽い Scene/Prefab（`Whitebox`） |
| アニメーター | ライティング/重い環境を抜いた Scene（`NoLighting`） |
| ライティングアーティスト | フルセット（`Full`） |
| 実装中 | 仮 Scene / 仮 Prefab / 軽量 Prefab（`Temp`, `Proxy`） |

空文字 `""` が「デフォルト Variant」。`SceneAssetDescription.Load` は指定 Variant が見つからなければ空文字にフォールバックする（`SceneAssetDescription.cs:71-88`）。

**重要:** Variant の第一目的は **編集ワークフローの差し替え**であり、実行中の切替 UI ではない。起動時に一度だけ Scene payload Variant を決める配線は実装済み（[§4.8](04-app-startup.md#48-起動時-scene-variant-と職種-companion-set)）。本機構（Build / カタログ）の必須要件ではない。

**第二用途: チェックアウト厳選タグ。** 上記に加え、Variant を「どの開発領域のアセットを手元に置くか」を示すタグとしても活用できる。`DeveloperVariantSettings` で選択した `BuildVariantProfile` の whitelist に一致する Payload をローカル Checkout 対象とし、未取得分はリモート Addressables カタログからストリーミングする開発ワークフローが本リポジトリに実装済みである（詳細は [20. Variant チェックアウト厳選ワークフロー](20-variant-checkout-workflow.md)）。

ただし Variant の**本来の軸**は品質・制作段階（`Whitebox` / `Full` 等）であり、領域軸（`OutGame` 等）と 1 つの文字列に無秩序に混在させると運用が破綻しうる。Framework は Variant 名を**完全一致**でしか解釈しないため、命名規約はプロジェクト側で統一すること。

| 用途 | 命名の例 |
|---|---|
| 領域タグ（単独） | `OutGame`, `InGame` |
| 領域 + 品質の複合 | `OutGame_Whitebox`, `InGame_Full` |

本機構は **Build / Play 時の Addressables カタログ構成**で完結する。ランタイムで作業者が Variant を切り替える UI ではない。起動時に一度だけ Scene payload Variant を決めて `SceneDirector` へ渡す配線は実装済み（[§4.8](04-app-startup.md#48-起動時-scene-variant-と職種-companion-set)、[§5.4](05-scene.md#54-iloadingdisplayローディング表示)）。実行中の切替口は無い。

---

## 2. 有用性

### メリット

- **BuildScript が型を知らずに全参照を列挙できる。** `IAssetPayloadProvider.Payloads` だけ見ればよいので、対象アセットの種類が増えても BuildScript 側は変更不要。
- **作業者ごとの `.asset` を分けずに済む。** 同じ論理アセットの差し替え候補を 1 箇所（Payload リスト）に並べ、ビルド内容は外側（`BuildVariantProfile`）で制御。
- **登録漏れがビルド時に Error として出る。** Collector が走査経路を一本化し、必須 Description が whitelist 適用後に 0 件なら Error（`VariantWhitelistBuilder.cs:81-85`）。
- **ソースは全 Variant を保持。** Git 差分を汚さず、Editor/開発中は全 Variant を参照可能（IK-B3）。

### 限界・注意

- Variant 名の規約は Framework が強制しない。命名はプロジェクト規約として別途決める必要がある。
- 子依存（Material/Texture 等）は whitelist に含めず、Addressables の dependency resolution に委譲（IK-B5）。Payload は **primary GUID のみ**を宣言する。
- 起動時 Scene Variant は [§4.8](04-app-startup.md#48-起動時-scene-variant-と職種-companion-set) が所有する。本機構（checkout / packed カタログ）は実行中の切替口ではない。
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
| `SceneAssetDescription` | `[Serializable]` class（基底継承） | シーンの Addressables ロード + Variant 対応 | `SceneAssetDescription.cs` |
| `ScenePayload` | `[Obsolete]` alias（→ `AssetPayload`） | 後方互換 alias。実質未使用（[17 DESIGN-4]） | `ScenePayload.cs` |
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

```
BuildVariantProfile (SO)
  ├─ VariantWhitelist: ["", "Full"] ...    同梱を許可する Variant 名
  ├─ SceneResourceMap                      走査対象マップ
  ├─ AlwaysIncludedAssets: AssetReference[] Variant 無関係に必ず同梱（Bootstrap 等）
  └─ TargetAddressablesGroupName           whitelist 同期先グループ
        │
        ▼
AssetDescriptionCollector
  └─ SceneResourceMapSource                 SceneResource → SceneAssetDescription.Payloads を列挙
        │ (IAssetPayloadProvider の列挙)
        ▼
VariantWhitelistBuilder
  ├─ payload.Variant が whitelist 一致 → IncludedGuids
  ├─ 不一致 → ExcludedGuids（managed - included）
  ├─ AlwaysIncludedAssets → 無条件 IncludedGuids
  └─ 必須 Description が 0 件同梱 → Error
        │
        ▼
AddressablesGroupSnapshot (capture)
        │
AddressablesGroupSyncFilter
  ├─ Included だが未登録 GUID → target group に一時追加
  └─ managed かつ Excluded の entry → 一時削除
        │
        ▼
BuildScriptPackedMode.BuildDataImplementation（標準ビルドへ委譲）
        │
        ▼
AddressablesGroupSnapshot.Dispose (restore)  Editor の設定を元に戻す
```

### ホワイトリスト規則（要点）

- whitelist は **完全一致**のみ。名前の意味は解釈しない（`VariantWhitelistBuilder.ResolveVariantWhitelist`）。
- whitelist 空 = `{""}`（デフォルト Variant のみ）。最も安全な既定。
- 複数 Variant 指定はフォールバックではなく **同時同梱**（一致した Payload は全部残す）。
- 各必須 Description から最低 1 Payload が残ること。残らなければ Build Error。
- 空 GUID / null Reference は Warning + 除外。

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

既存の `VariantWhitelistBuilder` と Addressables build 経路は変更していない。
SampleGame の Editor 入口は本番の `SceneResourceMap` を materialize し、後述の project policy で選択して
Content Directory build へ渡す。Runtime の directory 管理と Player build は未実装である。
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
  これは consumer 境界の検証であり、本番非 Scene Description、型付き load、サブアセット一般化、
  Runtime の所有・解放、Player への接続を証明しない。

### SampleGame の季節選択と Editor build（現況）

`SampleGame/DependOnAll/Editor/Build/` の `SeasonSceneSelectionPolicy` が project 固有の選択を担い、
`SampleGameContentBuild` が本番 `SceneResourceMap` の複製、materialization、selection、Content Directory build を接続する。
Framework の selection core と成果物 schema に季節名を持ち込まない。

- `InGameScene` / `OutGameScene` を graph root とし、親子関係を相互検証する。祖先の `Season_Spring` / `Summer` / `Autumn` / `Winter` から所属を導出し、季節祖先のない content は共通扱いにする。循環、片側だけの親子リンク、異なる季節への重複所属、payload と候補の不一致は選択前に失敗する。階層で表せない所属の明示的な override 入力はあり、通常は空である。
- materializer の `Representation=Full`（空 Variant）と `Whitebox` を使う。季節内で Whitebox 候補がある logical group にだけ `SeasonalMode` を付け、Full のみの補助シーンと共通 content は Whitebox build にも残す。
- requirement は要求した季節と共通 content の payload group に限る。通常は `ExactlyOne`。Full と Whitebox の両候補がある季節 group を同梱する場合だけ `OneOrMore` にし、両表現の候補が採用されたことも照合する。
- Editor メニュー `Tools/OSM/Content/` に全季節 Full、Spring Full、Spring Whitebox、Spring Full And Whitebox の入口がある。選択した必須 group、候補、除外理由をログへ出し、成功 plan と対応 snapshot を既存の `BuildContentCoordinator` へ渡す。専用 EditMode テストと、本番 graph による四つの Content Directory build を確認済み。

この入口は Editor の content 生成用である。生成 directory の Runtime 登録・ロード・寿命管理、Editor Play 接続、Player bootstrap は後続工程が担う。

---

## 5. 拡張ガイド

### 新しい Variant を運用する

`BuildVariantProfile._variantWhitelist` に名前を追加するだけ。Framework 側のコード変更は不要。Scene 側は `SceneAssetDescription` の Payload リストに該当 Variant の `AssetReference` を足す。

### Scene 以外のアセット種別を AssetDescription 化したくなったら

1. `AssetDescription` を継承した `[Serializable]` クラス、または独立 SO を作る。`Payloads` を実装。
2. その型を走査する `IAssetDescriptionSource` を追加（独立 SO なら `AssetDatabase.FindAssets` ベース）。
3. `AssetDescriptionCollector.DefaultSources` に登録、または `Build(profile, additionalSources)` で注入。
   - BuildScript / Whitelist ロジックは変更不要（`IAssetPayloadProvider` 経由のため）。
- 注意: 計画段階で Prefab/Audio/Texture/Generic の個別 Description 型を作ったが、**実需要が出るまで作らない方針で剪定済み**。「あるけど使われない型」を増やさないこと。
  AssetType 自体は `AssetKey` のメタ情報として採用済みで、カテゴリ別 cache / budget の次パスで使用する。

### 起動時の Scene Variant

起動時の一回解決は実装済み（[§4.8](04-app-startup.md#48-起動時-scene-variant-と職種-companion-set)）。`SceneAssetDescription.Load(variant)` は既に variant 引数を取る。実行中切替や第二の解決源は作らない。Play / Player の再起動が切替である。

---

## 6. 既知の制約・落とし穴

- `AssetDescription` を SO に変えてはいけない（埋め込み構造が壊れる、§3 参照）。
- フィールド名変更時は `[FormerlySerializedAs]` を必ず付け、Editor 側の `FindPropertyRelative` は新名へ追従させる。
- Payload は primary GUID のみ宣言。子依存は Addressables 任せ。
- ビルド時の Addressables グループ変更は一時的（Snapshot で復元）。中断時の堅牢化は [17 DESIGN-2] が未対応。
- `ScenePayload`（Obsolete alias）は実質未使用。削除候補。

---

## 7. 関連ファイル

- Runtime: `unity/Assets/OneStarMaker/Scripts/Runtime/AssetDescriptions/`
- BuildSystem: `unity/Assets/OneStarMaker/Scripts/Editor/Build/`
- Pure selection: `unity/Assets/OneStarMaker/Scripts/Editor/Build/Selection/`
- Production materialization: `unity/Assets/OneStarMaker/Scripts/Editor/Build/Materialization/`
- Content Directory build: `unity/Assets/OneStarMaker/Scripts/Editor/Build/Content/`
- Build content root: `unity/Assets/OneStarMaker/Scripts/Runtime/BuildContent/`
- SampleGame selection と Editor 入口: `unity/Assets/SampleGame/DependOnAll/Editor/Build/`
- Scene 連携: `unity/Assets/OneStarMaker/Scripts/Runtime/SceneSystem/SceneResource.cs`, `SceneResourceMap.cs`
- 既存資料: [13. リソースシステム](13-resource-system.md)（AssetType は cache 用メタとして採用済み。AssetResidentCache(常駐キャッシュ + per-category budget)実装済み）
- ワークフロー: [20. Variant チェックアウトワークフロー](20-variant-checkout-workflow.md)

> 注: 旧「17. Variant BuildScript レビュー」はファイル未保存のまま失われたため欠番（git 履歴にも存在しない）。
