# S-4 フルスペック World 制作基盤

> type: program
> status: 発注者承認済み。S-4a は develop へマージ済み（公開面: Architecture §04 / §05 / §18 / §20 / §21 / §27 と STREAMING_CURRENT_SPEC）。S-4b 着手時 HANDOFF の入力正本。
> branch: `codex/s-4-full-spec-plan`
> implementation base commit: `1502ffc`
> risk: high
> owner: 発注者 / S-4 各スライス担当
> created: 2026-09-06
> expires: S-4d の Phase D 完了時
> harvest to: Architecture §05（Scene）、§18（Variant）、§24（Rendering）、§27（Folder）と Streaming 現状仕様

本書は [SEASON_WORLD_DESIGN.md](SEASON_WORLD_DESIGN.md) の世界構図を変更せず、
S-4 を実際の分業制作に耐える World authoring 基盤へ具体化する program 計画である。
各スライスの Phase A は本書から対象部分を自己完結した HANDOFF へ転記し、凍結後はその
HANDOFF を Phase B の正本とする。本書を4スライス共通の実装 HANDOFF として直接使わない。

---

## 1. 目的と裁定

四季 World の生成だけでなく、レベルデザイナー、環境アーティスト、ライティングアーティスト、
VFX アーティスト、プランナーが同じ Cell を別 Scene で並行制作できる状態を作る。

確定事項:

- 四季は同じ AABB を共有する。各季節 `9 x 6 = 54` Cell、合計 216 Cell。
- Spring を起動時に自動ロードする。スポーンは Spring `(0,4)`。
- Environment は全 Cell に作る。Lighting / VFX / Events は必要な Cell だけに作る。
- Cell は距離ストリーミング境界。職種別の子 Scene は距離候補にしない。
- Full Cell と Whitebox Cell は同じ論理 `SceneResource` の Variant とする。
- Whitebox は Editor の閲覧だけでなく Play 時にも選択できる。
- 通常イベントは Cell 単位の Events Scene に束ね、大型イベントだけを独立 Scene にする。
- Lighting は Season-global と Cell-local に分け、代表する隣接2 Cellだけを S-4 でベイクする。
- VFX Graph を導入する。PC は描画必須、Mobile は非対応時に安全停止し、ParticleSystem fallback は作らない。
- S-4 は S-4a〜d の4スライス、4ブランチ、4 HANDOFF に分ける。
- 大量生成器は一度だけ使用し、生成後に削除する。日常用の小さい authoring tool は残す。
- 生成時間を理由に 9x6 を縮小しない。時間は実測値として記録するだけとする。

対象外:

- S-5 の Tunnel 内装と季節遷移演出。
- S-6 の1季節1 Addressables groupへの再編。
- S-7 の季節別 checkout / remote catalog 実証。
- 全216 Cellの完成ライティングとベイク。
- S-8 の演奏レイヤ作り込みと HandAuthored 昇格。
- S-9 の全域ストリーミング性能判断。

---

## 2. Scene とフォルダの制作契約

### 2.1 SceneGraph

```text
InGameSession
├─ Tunnel                                  S-5 / NecessaryAlways
├─ Season_Spring                           logical / OnDemand
│  ├─ Spring_Lighting                      NecessaryAlways
│  ├─ Spring_AtmosphereVFX                 IncrementalAlways / optional
│  ├─ Spring_Cell_{x}_{y}                  OnDemand / StreamByDistance
│  │  ├─ Spring_Environment_{x}_{y}        OnDemand / resident companion
│  │  ├─ Spring_Lighting_{x}_{y}           OnDemand / optional companion
│  │  ├─ Spring_VFX_{x}_{y}                OnDemand / optional companion
│  │  └─ Spring_Events_{x}_{y}             OnDemand / optional companion
│  └─ Spring_Event_{slug}                  OnDemand / major event
├─ Season_Summer                           同型
├─ Season_Autumn                           同型
└─ Season_Winter                           同型
```

`Season_*` は Scene payload を持たない論理ノードとする。`SceneDirector` が既に許容する
論理ノードのライフサイクルを利用し、Global Lighting の実体を職種 Scene へ分ける。

Cell 配下の全職種 Scene は `StreamByDistance = false`、`LoadType.OnDemand` とする。
親 Cell が Stable になった後、SampleGame の companion loader が選択中の制作プロファイルに
応じて明示 `AddScene` する。Unload は親 Cell の再帰破棄へ委ねる。

### 2.2 職種ごとの所有物

- **Cell**: 地形、衝突、移動可能面、ストリーミング AABB。ランタイム距離判断の唯一の単位。
- **Environment**: 建物、植生、岩、装飾物などの set dressing。全 Cell に存在する。
- **Cell Lighting**: local point / spot light、Reflection Probe、local Volume、局所ベイク調整。
- **Cell VFX**: 霧、煙、流水など、その Cell が常駐中は継続する環境エフェクト。
- **Cell Events**: trigger、spawn、encounter anchor、局所的な進行制御。
- **Season Lighting**: Directional Light、sky、ambient、fog、全 View 共通 Volume、LightingSettings。
- **Season Atmosphere VFX**: 季節全域の常時エフェクト。必要な季節だけ作る。
- **Major Event**: 複数 Cell に跨る、起点 Cell より長寿命、または Timeline / Cinemachine を独立管理するイベント。

VFX Scene にゲーム進行 trigger を置かない。Events Scene が VFX の開始・停止を要求する。
Scene を跨ぐ `GameObject` の直接参照は禁止し、event id、world position、service query、
Addressable prefab で接続する。

### 2.3 Event Scene の粒度

次のいずれにも該当しないイベントは `*_Events_{x}_{y}` に含める。

- 複数 Cell に効果が及ぶ。
- 起点 Cell の Unload 後も継続する必要がある。
- 専用 Timeline / Cinemachine staging を独立ファイルとして所有したい。
- 別担当が Cell bundle と独立して変更する必要がある規模である。

いずれかに該当するときだけ `Season_Event_{slug}` とする。小イベント1件ごとに Scene を
増やさず、Season 単位の巨大な Events Scene にも集約しない。

### 2.4 VFX の粒度と寿命

- Cell 常駐中ずっと必要な複数エフェクトは、1 Cell 1 VFX Scene に束ねる。
- 季節全域のエフェクトは Season Atmosphere VFX に置く。
- ヒット、破裂、短い演出など一時的な VFX は Scene にせず prefab / `VisualEffectAsset` にする。
- 一時 VFX は `IAssetManagement` でロードし、`AssetOwner.Scene(eventSceneIdentity)` または
  `AssetOwner.Bind(go)` で寿命を宣言する。

### 2.5 物理フォルダ

論理 SceneGraph と物理フォルダの親子を合わせる。例:

```text
Seasons/Spring/
├─ Spring_Lighting/
│  └─ Spring_Lighting.unity
├─ Cells/
│  └─ Spring_Cell_4_2/
│     ├─ Spring_Cell_4_2.unity
│     ├─ Variants/Whitebox/Spring_Cell_4_2.unity
│     ├─ Spring_Environment_4_2/Spring_Environment_4_2.unity
│     ├─ Spring_Lighting_4_2/Spring_Lighting_4_2.unity
│     ├─ Spring_VFX_4_2/Spring_VFX_4_2.unity
│     └─ Spring_Events_4_2/Spring_Events_4_2.unity
└─ Events/
   └─ Spring_Event_MultiCellProof/Spring_Event_MultiCellProof.unity
```

Full / Whitebox は同じ Scene 名を使う。これにより、Editor で Whitebox Scene が既に開かれて
いる場合も `SceneManager.GetSceneByName(logicalIdentity)` が同一論理 Scene として検出できる。

---

## 3. S-4a — Runtime Variant と職種 Workspace

> **完了。** 現況は [§4.8](../../unity/Assets/Docs/Architecture/04-app-startup.md#48-起動時-scene-variant-と職種-companion-set)、[§5.13](../../unity/Assets/Docs/Architecture/05-scene.md#513-cell-と職種-companion-の分類)、[§18](../../unity/Assets/Docs/Architecture/18-asset-description.md)、[§20](../../unity/Assets/Docs/Architecture/20-variant-checkout-workflow.md)、[§21](../../unity/Assets/Docs/Architecture/21-scene-streaming.md)、[§27](../../unity/Assets/Docs/Architecture/27-folder-structure.md)、[STREAMING_CURRENT_SPEC.md](../streaming/STREAMING_CURRENT_SPEC.md)。以下は後続スライスが前提として読む要約であり、実装 HANDOFF ではない。

### 3.1 Runtime Variant

- `SceneDirector` に起動時固定の Scene Variant を渡す。
- `PerformUnitySceneLoad` は空文字固定をやめ、その値を `IAssetManagement.LoadSceneAsync` へ渡す。
- AppConfig key は `assets:sceneVariant`。空文字が Production、`Whitebox` が開発用。
- Editor は active `BuildVariantProfile.SceneVariant` を config より優先して注入する。
- Player は config file、環境変数、コマンドラインの既存優先順位を使う。
- Variant は起動後に切り替えない。切替には Play / Player の再起動を要求する。
- Cell の `SceneAssetDescription` は `""` と `"Whitebox"` の2 payloadを持つ。
- `WorldWhitebox` BuildVariantProfile は `""` と `"Whitebox"` を含める。
- Production Profile は Whitebox を含めない。

既存の「指定 Variant が無ければ空文字へ fallback」を維持する。そのため Whitebox 指定時も
Lighting / Events 等は通常 payloadを使用できる。一方、全 Cell に Whitebox payload があることは
S-4b の aggregate validation で必須にする。

### 3.2 Companion profile

AppConfig key `world:cellCompanionSet` に次の値を定義する。

- `Full`: Environment + Lighting + VFX + Events
- `Planner`: Events のみ
- `Lighting`: Environment + Lighting
- `VFX`: Environment + Lighting + VFX

既定値は `Full`。不明な値は暗黙に Full へ戻さず、設定エラーとして起動を失敗させる。

`SessionCellCompanionLoadDriver` は、ロード済み Cell の `SceneResource.Children` を列挙する。
親 identity から Environment 名を組み立てない。
role判定は SampleGame の制作規約に閉じ、Framework は Environment / Lighting / VFX / Events を知らない。
role identity はロード対象選別だけに使い、座標、bounds、辞書keyの導出には使わない。

### 3.3 identity依存の除去

- `CellScene` は修飾付き identity の `TryParse` を要求しない。
- Cell 判定は `SceneResource.StreamByDistance` を使用する。
- bounds は `SceneResource.Volume` を正とする。
- 生成時の座標や見た目パラメータが必要なら、Scene内のbindingsへ焼く。
- GameSceneFactoryは Cell / Cell child をSceneResource構造で分類する。
- OneStarMaker Runtimeへ Spring / Summer / Autumn / Winter の語を入れない。

### 3.4 World Workspace

Editorに season、座標、職種を選ぶ World Workspace を置く。

- Level: FullまたはWhitebox Cellを開く。
- Environment: Cell + Environmentを開く。
- Lighting: Cell + Environment + Season Lighting + optional Cell Lightingを開く。
- VFX: Cell + Environment + Season Lighting + optional Cell VFXを開く。
- Planner: Whitebox Cell + optional Cell Eventsだけを開く。

存在しないLighting / VFX / Eventsを作る操作は、`.unity`、`SceneResource`、親子link、Map、
Addressables entryを同じEditor transactionで作る。EnvironmentはS-4bで全件作るため日常作成対象外。
Scene / asset YAML は直接編集しない。

---

## 4. S-4b — 四季 World 生成

### 4.1 生成物

- logical Season Resource: 4（Scene実体なし）
- Season Lighting Scene / Resource: 4
- Full Cell Scene: 216
- Whitebox Cell Scene: 216
- Environment Scene: 216
- 基本 Scene 実体合計: 652

Whiteboxは全216 Cellに作る。Environment制作前でも谷の線、床、通路、主要シルエットを確認できる
軽量な proxy とし、Events authoring の衝突・移動面を提供する。

初期 policy は全件 Generated。S-8a より前に HandAuthored へ昇格しない。

### 4.2 旧Worldの廃止

旧 `World`、旧16 Cell、旧4 Environmentを明示ワイプする。移送、座標補正、stampによる生存判定、
旧南辺4 Cellの昇格は行わない。

### 4.3 起動

Springを追加し、そのSeason Resource直下の `StreamByDistance` childrenから候補集合を作る。
Spring `(0,4)` CellがStableになるまでPlayer入力を有効にしない。その後に通常の距離Tickを開始する。

Active Seasonは最大1つ。候補集合はSeason枝ごと交換し、identity文字列を季節間で翻訳しない。
S-4ではSpring初期化と排他controllerの入口までを作り、Tunnel遷移シーケンスはS-5に残す。

### 4.4 一時生成器

ブランチ内で次のcommitを分け、非squash mergeで履歴を保存する。

1. 一時生成器とdry-run / aggregate validation。
2. 生成されたScene / SceneResource / Addressables差分。
3. 一時生成器、旧policy、reconciler、probe、generator専用tests、`WorldGridDefinition.asset`の削除。

最終状態に大量生成器を残さない。日常利用するWorld Workspaceと職種Scene1件の作成機能だけを残す。

---

## 5. S-4c — World Lighting

### 5.1 Global owner

Architecture §24のREN-08をこのスライスで前倒しし、`IRenderEnvironment`、
`RenderEnvironmentState`、所有leaseをFrameworkへ追加する。

- App lifetimeのRenderEnvironmentは、同時に1つのSeason ownerだけを受け付ける。
- `SeasonLightingScene`がLoad時にleaseを取得し、Unload時に解放する。
- 二つ目のownerを有効化しようとした場合は即失敗する。
- 古いleaseの遅延Disposeが新ownerを消さないよう、owner tokenを照合する。
- PlayerScene等から`RenderSettings`直接変更を除き、global stateをRenderEnvironmentへ集約する。
- Camera固有VolumeはCameraSystem、全View共通VolumeはRenderEnvironmentが所有する。

純C# policyはpresetからsun rotation / color / intensity、ambient、fog、global Volume weightを算出する。
URP adapterだけが`Light`、`RenderSettings`、`Volume`へ反映する。

### 5.2 Local Lighting

Cell Lightingはlocal light、Reflection Probe、local Volumeを所有し、global sun、sky、fog、
global Volumeを書き換えない。必要なCellだけに作る。

### 5.3 代表ベイク

Spring `(4,2)` と `(5,2)` を代表区画にする。Spring Lighting、両Cell、Environment、
Cell Lightingを同時に開き、multi-scene bakeする。

Unityのmulti-scene bakeは、同時に開いたSceneを跨ぐshadow / GI bounceを計算し、
lightmap / realtime GI dataをScene単位でロード・アンロードする。一方、同時ベイクしたLight Probe dataは
共有される。この性質を受け入れ条件とS-9のメモリ計測項目に明記する。

- Unity Manual: <https://docs.unity3d.com/ja/6000.0/Manual/bakemultiplescenes.html>
- Unity Scripting API: <https://docs.unity3d.com/ja/6000.0/ScriptReference/LightingSettings-bakedGI.html>

S-4でcommitするのは代表2 Cellのベイクデータだけ。全216 CellのベイクとProbe方式の再評価はS-9へ送る。

---

## 6. S-4d — VFX Graph と Planner Events

### 6.1 VFX Graph

- `com.unity.visualeffectgraph` `17.5.0`を追加し、URP package versionと揃える。
- 必要なRuntime assembly referenceだけをSampleGame側asmdefへ追加する。
- PC Qualityで描画を必須受入とする。
- Mobileでは起動時にcapabilityを判定し、非対応なら`VisualEffect`を有効化せず、一度だけ診断ログを出す。
- Mobile用ParticleSystem fallbackは作らない。

URP / mobile対応には制約が残るため、PC必須・Mobile安全停止を正式な製品判断とする。

- Unity Manual: <https://docs.unity3d.com/ja/6000.0/Manual/com.unity.visualeffectgraph.html>

### 6.2 実証Scene

春に次の6 Scene / Resourceを追加する。

- Cell Lighting: `Spring_Lighting_4_2`, `Spring_Lighting_5_2`
- VFX: `Spring_AtmosphereVFX`, `Spring_VFX_4_2`
- Events: `Spring_Events_4_2`, `Spring_Event_MultiCellProof`

基本652 Sceneと合わせ、S-4完了時のScene実体は658件を想定する。

`Spring_Events_4_2`はCell内triggerからlocal VFXを起動する。
`Spring_Event_MultiCellProof`はCell bundleとは独立してAdd / Unloadでき、Season Unloadで必ず回収される。

Planner Playは `assets:sceneVariant=Whitebox` と `world:cellCompanionSet=Planner` を使い、
Environmentをロードしない状態でlocal / major eventの両方を実証する。

---

## 10. 公開APIと依存

Framework公開面の追加は次に限定する。

- SceneDirectorの起動時Scene Variant指定。
- AbstractApplicationInitializerのScene Variant解決口とEditor runtime bridge。
- `IRenderEnvironment`、`RenderEnvironmentState`、所有lease。

Frameworkはseason名、Cell座標文法、職種roleを知らない。Game → Frameworkの依存方向を維持する。
VFX Graph依存はSampleGame側に閉じ、OneStarMaker Runtimeの公開APIへ`VisualEffect`型を出さない。

`SceneState`の14値は変更しない。新しいLoadTypeも追加しない。asmdef参照追加はS-4d HANDOFFで
`Unity.VisualEffectGraph.Runtime`の実assembly名をEditor import後に確認してから固定する。

---

## 11. 検証方針

658 Sceneを個別に目視しない。破壊的生成と制作契約に必要な集約ゲートだけを行う。

### S-4a

- 空文字 / Whiteboxのvariant伝播と既存default fallback。
- companion profileごとのrole選別。
- Cell Stable前は子をAddしない。
- 親CellのUnload中またはAdd完了競合時に子を残さない。
- 修飾付きidentityを座標parseせずCellとして生成できる。

### S-4b

- 4 Season、216 Cell、216 Whitebox payload、216 Environment、4 Season Lightingの件数。
- identity、GUID、Map、親子、LoadType、`StreamByDistance`、Addressables entryの一意性。
- 旧World / 旧Cell / 旧Environmentが残っていない。
- Spring `(0,4)`がStableになってからPlayer入力が始まる。
- 生成1回の実時間を記録する。時間による縮小判定は行わない。

### S-4c

- RenderEnvironmentの単一ownerとstale lease耐性の純C#テスト。
- Spring代表2 Cellで明白なlightmap seamがない。
- Cell unload / reload後にLighting stateとbaked dataが復帰する。

### S-4d

- PCでSeason / Cell VFXを表示できる。
- Mobile非対応時に例外、未解放handle、無限retryがない。
- Planner profileでEnvironmentをロードせずWhitebox上のEventが動く。
- local EventはCell unload、major Eventは完了またはSeason unloadで回収される。

各スライスのPhase B担当はUnityテストとAddressables buildを実行せず、終了時に
`pwsh tools/contract-audit.ps1`まで行う。Phase CはEditorが閉じていることを確認し、
`pwsh tools/run-tests.ps1`を実行する。Scene / SceneResource / Addressables操作は、人間が既に開いた
EditorへUnity CLIで接続して行い、`.unity` / `.asset` YAMLを直接編集しない。

Unity CLIは導入済みであり、再インストールしない。

```text
C:\Users\void\AppData\Local\Unity\bin\unity.exe
version: 1.0.0-beta.6
```

---

## 12. スライス境界とharvest

順序は `S-4a -> S-4b -> S-4c -> S-4d -> S-5`。

各スライスは着手時に専用HANDOFFを作り、責務マップ、現在行数、予想増分、公開API、所有者、寿命、
単体テスト境界を固定する。500行、3責務、50%以上増加の警報が出た場合は、分割または非分割の理由を
HANDOFFへ書く。本書にない公開API、依存、状態、所有者が必要になったらPhase Bで決めずPhase Aへ戻す。

S-4dのPhase Dで、現況となった契約をArchitecture §05 / §18 / §24 / §27とStreaming現状仕様へharvestし、
本書と完了済みS-4a〜d HANDOFFを削除する。後続S-5が必要とするSeason排他controllerの公開面だけは、
harvest後のArchitecture文書を参照する。
