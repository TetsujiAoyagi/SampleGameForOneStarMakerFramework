# 18. AssetDescription — 目的・有用性・実装

本書は `OneStarMaker.Runtime.AssetDescriptions` の `AssetDescription` 系について、**何のために存在し、なぜ有用で、どう実装されているか**を整理する。Variant フィルタ BuildScript（[17](17-variant-build-system-review.md)）の前提知識でもある。

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

**重要:** Variant の第一目的は **編集ワークフローの差し替え**であり、ランタイム機能ではない。ランタイムで Variant を選ぶ拡張は可能だが、現時点で BuildSystem の必須要件ではない。

**第二用途: チェックアウト厳選タグ。** 上記に加え、Variant を「どの開発領域のアセットを手元に置くか」を示すタグとしても活用できる。`DeveloperVariantSettings` で選択した `BuildVariantProfile` の whitelist に一致する Payload をローカル Checkout 対象とし、未取得分はリモート Addressables カタログからストリーミングする開発ワークフローが本リポジトリに実装済みである（詳細は [20. Variant チェックアウト厳選ワークフロー](20-variant-checkout-workflow.md)）。

ただし Variant の**本来の軸**は品質・制作段階（`Whitebox` / `Full` 等）であり、領域軸（`OutGame` 等）と 1 つの文字列に無秩序に混在させると運用が破綻しうる。Framework は Variant 名を**完全一致**でしか解釈しないため、命名規約はプロジェクト側で統一すること。

| 用途 | 命名の例 |
|---|---|
| 領域タグ（単独） | `OutGame`, `InGame` |
| 領域 + 品質の複合 | `OutGame_Whitebox`, `InGame_Full` |

本機構は **Build / Play 時の Addressables カタログ構成**で完結する。ランタイムで Variant 文字列を選ぶ配線ではない（§2「ランタイム Variant 選択は現状未配線」のとおり）。

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
- ランタイム Variant 選択は現状未配線。ランタイムで使うなら別途仕組みが要る。

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

### ランタイムで Variant を選びたくなったら

`SceneAssetDescription.Load(variant)` は既に variant 引数を取る。AppConfig 等から variant 文字列を解決して渡す配線を追加すれば成立する（現状未配線）。

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
- Scene 連携: `unity/Assets/OneStarMaker/Scripts/Runtime/Scene/SceneResource.cs`, `SceneResourceMap.cs`
- 既存資料: [13. リソースシステム](13-resource-system.md)（AssetType は cache 用メタとして採用済み。AssetResidentCache(常駐キャッシュ + per-category budget)実装済み）
- レビュー/修正指示: [17. Variant BuildScript レビュー](17-variant-build-system-review.md)
