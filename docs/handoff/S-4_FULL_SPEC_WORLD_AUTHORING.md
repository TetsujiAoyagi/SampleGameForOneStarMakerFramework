# S-4 フルスペック World 制作基盤

> type: program
> status: 発注者承認済み。S-4a / S-4b / S-4c / S-4d1 / S-4d2 / S-4d3 は完了（S-4d1〜S-4d3 の契約は Architecture §24 §16 へ harvest 済み）。S-4d は d1〜d5 の5スライスに分割し（§6.3、2026-10-17 発注者決定）、d4 をさらに d4a / d4b に分けた（2026-10-27 発注者決定）。各スライスは §0 の手順で着手し、本書と、S-4c から harvest 済みの Architecture §24 / §27 を入力にする。S-4b の現況は Architecture §18 / §21 / §27 と STREAMING_CURRENT_SPEC へ harvest 済み。
> branch: `codex/s-4-full-spec-plan`
> implementation base commit: `1502ffc`
> risk: high
> owner: 発注者 / S-4 各スライス担当
> created: 2026-09-06
> expires: S-4d5 の Phase D 完了時
> harvest to: Architecture §05（Scene）、§18（Variant）、§24（Rendering）、§27（Folder）と Streaming 現状仕様

本書は [SEASON_WORLD_DESIGN.md](SEASON_WORLD_DESIGN.md) の世界構図を変更せず、
S-4 を実際の分業制作に耐える World authoring 基盤へ具体化する program 計画である。
各スライスの Phase A は本書から対象部分を自己完結した HANDOFF へ転記し、凍結後はその
HANDOFF を Phase B の正本とする。本書をスライス共通の実装 HANDOFF として直接使わない。

---

## 0. S-4d スライスの着手手順

新しい session は「§0 に従い S-4dN を始める」という指示だけで始まる。会話履歴に頼らず次を行う。

1. `AGENTS.md` と `osm-workflow` Skill を読む。Phase A の参照文書も Skill の指示どおり全文読む。
2. 前提を確認する。`develop` が最新で、作業ツリーが clean であること。d1 以外は、直前のスライス（dN-1）の PR が `develop` にマージ済みで、その HANDOFF が削除済みであること。満たさなければ着手せず報告する。
3. `develop` から専用ブランチを作る。名前に `s-4dN` を含める。
4. `docs/handoff/S-4DN_<主題>.md` を [テンプレート](../../.agents/skills/osm-workflow/references/handoff-template.md) から作り、本書の §2.1〜§2.4（Scene 構成・所有物・Event 粒度・VFX 寿命の制作契約）、§3.2（companion profile）、§6.1〜§6.2、§6.3 の該当行、§6.4、§10、§11 の該当条件、§12 のうちスライスに必要なものを自己完結に転記する。本書へのリンクで済ませない。
5. Phase A（A0〜A3）を進める。A3 の凍結は人間の承認を待つ。同じ session で Phase B に入らない。
6. 以降の Phase は、そのスライス HANDOFF の §0 引継ぎ票を入口にする。本書の §0 は着手時だけに使う。

本書の §6.3 / §6.4 に無い公開 API、依存、所有者、寿命は、そのスライスの Phase A で決め、本書の該当行も Phase A の PR で更新する。Phase B で決めない（§12）。

**Phase A の判断基準（発注者、2026-10-27）。** 手順 4 の HANDOFF に転記し、A1 の各案と A3 の裁定の材料に明記する。

- **主目的は BuildSystem。** いまの主目的は、きちんとした BuildSystem を作ることで、VFX（S-4d）はそのつなぎ込みの題材である。各案で、スライスの成果物が content build・Player build の経路に乗るかと、Build 側の既知の課題（BS4 の Player は smoke の後に終了して InGame の Spring に入らない、transport staging の path が予算の外。[Architecture §24 §16.7](../../unity/Assets/Docs/Architecture/24-rendering-system.md#167-後続スライスへの送り)・[Harness README](../../tools/Harness/README.md#起動前の-path-予算検査)）を踏むかを書く。Build 側の課題は「やらない」で片付けず、BuildSystem の作業の候補として扱う。
- **残すコードは SampleGame に必要なものだけ。** 生成や authoring の道具を残すかは「SampleGame にそのコードが必要か（無いと SampleGame の検証が成り立たないか）」で決める。必要でなければ、一時スクリプトで作り commit の前に消す案を既定にする。「他の季節・Cell でも使える」「再利用できる」は残す理由にしない。
- **checkout のルート。** Player を build するスライスは、ルートが 96 文字以下の checkout で作業する（transport staging の path の制約。上の Harness README）。

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
- VFX Graph を導入する。PC は描画必須、Mobile は非対応時に安全停止する。ParticleSystem fallback は S-4d3 では作らず、要否・契約・実装は後続の独立スライスで決める（§6.1）。
- S-4 は S-4a〜d に分け、S-4d はさらに d1〜d5（d4 は d4a / d4b）に分ける（§6.3）。1スライス = 1ブランチ = 1 HANDOFF。
- 大量生成器は一度だけ使用し、生成後に削除する。日常用の小さい authoring tool は残す。
- 生成時間を理由に 9x6 を縮小しない。時間は実測値として記録するだけとする。

対象外:

- S-5 の Tunnel 内装と季節遷移演出。
- W-5 / W-6 の文面は `SEASON_WORLD_DESIGN.md` が正本。S-6 / S-7 実装スライスは廃止済み。季節 partition と部分 Checkout 再燃は対象外。
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
│  ├─ Spring_AtmosphereVFX                 NecessaryAlways / optional（S-4d3 で変更）
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

Season Atmosphere VFX は当初 `IncrementalAlways` としたが、S-4d3 の Phase A で `NecessaryAlways` に変えた。
Framework の `IncrementalAlways` の子は、ロード中に親が unload されると保留 unload が消費されずに残る
（修正は別の Framework スライス、所有 HANDOFF 未作成）。`NecessaryAlways` なら Season の Stable が
Atmosphere のロードを待ち、Season controller の観測個体にも入る。修正後に戻すかはその時点で判断する。

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
`IAssetManagement` で寿命を宣言してロードする prefab で接続する（§2.4）。

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

- 起動時に一度選んだ表現を `SceneDirector` の Scene lifecycle に固定する。実行中に切り替えず、変更時は Play / Player を再起動する。
- 通常の Content Directory 経路では `content:representation` の `Full` / `Whitebox` を使う。空の Scene payload Variant は build 時に `Representation=Full` へ写す。
- `Tools/OSM/Content/Build Spring Whitebox` または `Build Spring Full And Whitebox` は Content Directory を生成する。Editor Play で使うには、その後の transport publish と DIST の verified install が必要だが、Editor Play 向け publish 入口は未整備である。Player は BS4 coordinator が Player build 内で publish する固定 config / bootstrap 経路を使う。
- Scene の要求表現が無いときだけ、その directory 内の `Full` entry へ fallback する。Lighting / Events 等の Full のみの補助 Scene は Whitebox build にも含める。欠損を source や Addressables で埋めない。
- 全 Cell が既定と `Whitebox` の2 payloadを持つことは S-4b で検査済み。

`assets:sceneVariant` と active `BuildVariantProfile.SceneVariant` による選択、Production / WorldWhitebox の whitelist は明示 Addressables 互換経路の設定であり、通常 directory Play の選択手順ではない。起動の正本は [§4.8](../../unity/Assets/Docs/Architecture/04-app-startup.md#48-起動時-scene-variant-と職種-companion-set)、選択・取得・Player は [§18](../../unity/Assets/Docs/Architecture/18-asset-description.md) / [§20](../../unity/Assets/Docs/Architecture/20-variant-checkout-workflow.md)。

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
Season 直下の Atmosphere VFX（`{Season}_AtmosphereVFX`、`NecessaryAlways`）も同じ transaction で作れる（S-4d3）。
season を選んで作成して開く操作で、上の Role 別の open plan には入れない。
Scene / asset YAML は直接編集しない。

---

## 4. S-4b — 四季 World 生成

> **完了。** 以下は生成物と実施境界の記録であり、S-4c 以降の実行手順ではない。一時生成器、旧 policy、`WorldGridDefinition`、再実行メニューは撤去済みで、復活させない。

### 4.1 生成物

- logical Season Resource: 4（Scene実体なし）
- Season Lighting Scene / Resource: 4
- Full Cell Scene: 216
- Whitebox Cell Scene: 216
- Environment Scene: 216
- 基本 Scene 実体合計: 652

Whiteboxは全216 Cellに作る。Environment制作前でも谷の線、床、通路、主要シルエットを確認できる
軽量な proxy とし、Events authoring の衝突・移動面を提供する。

生成時 policy は全件 Generated とした。S-8a より前に HandAuthored へ昇格しない。

### 4.2 旧Worldの廃止

旧 `World`、旧16 Cell、旧4 Environmentは明示ワイプ済み。移送、座標補正、stampによる生存判定、
旧南辺4 Cellの昇格は行わなかった。

### 4.3 起動

Springを追加し、そのSeason Resource直下の `StreamByDistance` childrenから候補集合を作る。
Spring `(0,4)` CellがStableになるまでPlayer入力を有効にせず、その後に通常の距離Tickを開始する配線を実装済み。

Active Seasonは最大1つ。候補集合はSeason枝ごと交換し、identity文字列を季節間で翻訳しない。
S-4bではSpring初期化と排他controllerの入口までを実装した。Tunnel遷移シーケンスはS-5に残す。

### 4.4 一時生成器

ブランチ内で次のcommitを分け、非squash mergeで履歴を保存した。

1. 一時生成器とdry-run / aggregate validation。
2. 生成されたScene / SceneResource / Addressables差分。
3. 一時生成器、旧policy、reconciler、probe、generator専用tests、`WorldGridDefinition.asset`の削除。

最終状態に大量生成器は残していない。日常利用するWorld Workspaceと職種Scene1件の作成機能だけを残した。

---

## 5. S-4c — World Lighting

> **完了。** 以下は後続制作が維持する境界。実装・検証範囲と seam の証拠不足を含む受け入れ判断は [Architecture §24 §9.1](../../unity/Assets/Docs/Architecture/24-rendering-system.md#91-s-4c-で実証した最小実装) を正とし、追加実装指示として扱わない。REN-08 の TimeOfDay 全体や RenderWorld の完成ではない。

### 5.1 Global owner

`IRenderEnvironment`、`RenderEnvironmentState`、所有leaseの最小境界はFrameworkへ追加済み。
SeasonのpresetはSampleGameが所有し、Frameworkへ季節語を持ち込まない。

- App lifetimeのRenderEnvironmentは、同時に1つのSeason ownerだけを受け付ける。
- `SeasonLightingScene`がLoad時にleaseを取得し、Unload時に解放する。
- 二つ目のownerを有効化しようとした場合は即失敗する。
- 古いleaseの遅延Disposeが新ownerを消さないよう、owner tokenを照合する。
- PlayerScene等から`RenderSettings`直接変更を除き、global stateをRenderEnvironmentへ集約する。
- Camera固有VolumeはCameraSystem、全View共通VolumeはRenderEnvironmentが所有する。

実装済みのlease / sink境界では、Unity側のsinkだけが`Light`と`RenderSettings`へ反映し、lease解放で取得前のbaselineを復元する。
TimeOfDay、天候、Volume合成等の後続機能を、この最小実装の完了に含めない。

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

- `com.unity.visualeffectgraph` の導入方針は維持する。旧指定 `17.5.0` は現行manifestの URP `17.6.0` と一致しない。採用versionは S-4d2 の Phase A で、Unity 6000.6.0f1 同梱の `17.6.0`（URP `17.6.0` と依存の版が一致）に固定した。Editor の import と実描画の受入は S-4d2 の Phase C の判定対象。旧versionは追加しない。
- 必要なRuntime assembly referenceだけをSampleGame側asmdefへ追加する。
- PC Qualityで描画を必須受入とする。
- Mobileでは起動時にbackendがcapabilityを判定して対応可否だけを返し、非対応なら`VisualEffect`を有効化しない。診断ログは§6.4のとおりFrameworkが一度だけ出し、backendとScene側は出さない。
- S-4d3 では Mobile 向け ParticleSystem fallback は実装対象外とする。VFX Graph 非対応環境では配置済み VisualEffect を有効化せず、代替描画も行わない。ParticleSystem fallback の要否・契約・実装は後続の独立スライスで決定する（担当・時期未定）。この延期は S-4d3 の GO 条件を妨げない。

URP / mobile対応には制約が残るため、PC必須・Mobile安全停止を正式な製品判断とする。

- Unity Manual: <https://docs.unity3d.com/ja/6000.0/Manual/com.unity.visualeffectgraph.html>

### 6.2 実証Scene

春ではS-4cで作成済みのCell Lighting 2件を維持し、S-4dで残るVFX / Events 4件を追加する。

- Cell Lighting（S-4cで作成済み）: `Spring_Lighting_4_2`, `Spring_Lighting_5_2`
- VFX: `Spring_AtmosphereVFX`, `Spring_VFX_4_2`
- Events: `Spring_Events_4_2`, `Spring_Event_MultiCellProof`

基本652 Sceneと合わせ、S-4完了時のScene実体は658件を想定する。

`Spring_Events_4_2`はCell内triggerからlocal VFXを起動する。
`Spring_Event_MultiCellProof`はCell bundleとは独立してAdd / Unloadでき、Season Unloadで必ず回収される。

Planner Playは§3.1の経路で `content:representation=Whitebox` と
`world:cellCompanionSet=Planner` を使う。実証対象のEvents / VFXを含めたrevisionで、Environmentをロードしない状態のlocal / major event両方を実証する。

### 6.3 S-4d の分割

順序は `d1 -> d2 -> d3 -> d4a -> d4b -> d5`。前のスライスのマージ後に次を始める。各行は Phase A の A0 入力であり、凍結済みの条件ではない。

| スライス | 答える問い | 範囲 | 対象外（所有スライス） |
|---|---|---|---|
| **d1** Framework の VFX 抽象 | VFX の寿命、非対応環境での停止、例外の隔離を、VFX 実装の型に依存せず Framework の契約として保証できるか | §6.4 の3責務。中核ロジックは偽 backend で単体テストし、動作数は偽 backend が記録する呼び出し回数で確かめる（公開 API にしない）。**Phase A の必須検討:** Stop / Dispose / 所有者の unload / 消え切り待ちの遅延完了が競合したときの手順、per-frame 更新の要否（既存の lifecycle 通知で足りれば `UpdateSystemRuntime` に登録しない。登録する場合、エフェクト単位の例外隔離は `UpdateSystemRuntime` のシステム単位の隔離に加えた d1 の境界とする）、harvest 先の既存 Architecture 文書。**d1 の Phase A で決めたこと（現況は [Architecture §24 §16](../../unity/Assets/Docs/Architecture/24-rendering-system.md#16-vfx-抽象)）:** 配置は `OneStarMaker.Runtime` の `Rendering/Vfx/`（新しい asmdef なし）。Scene 所有者は `SceneDirector.OnSceneEvent` の `PreUnloading` / `LoadCanceled` で Unity Scene unload より前に回収し、`Bind(go)` は VFX 専用 notifier と `Tick` の偽 null 検査で回収する。消え切り待ちの完了と timeout は公開 `Tick` で進め、d1 は `UpdateSystemRuntime` に登録しない。harvest 先は Architecture §24 | パッケージ導入と実 backend（d2）、Scene（d3〜d5）、§6.4 で入れないとしたもの |
| **d2** VFX Graph 導入と SampleGame backend | 現行 project（URP `17.6.0`）に VFX Graph を入れ、d1 の抽象越しに PC で描画し、非対応環境で安全に止められるか | §6.1 の version 互換確認と固定、SampleGame 側 asmdef への Runtime assembly 参照（実名は Editor の import 後に確定）、起動時の capability 判定（§6.4 の backend 側）、backend 実装、Scene に配置済みの `VisualEffect` を止める仕組み（Component が有効化される前に適用できるかを検証する）、確認用の最小 asset。**d1 から送った必須事項:** `VfxSystem.Tick` の `UpdateSystemRuntime` 登録とフレーム順序、`VfxSystem` の生成場所・`BindSceneLifecycle`・`Dispose` と `SceneDirector.Dispose` / `AssetManagement.ReleaseAll` の順序、backend 未構成と Unsupported の区別、capability 判定結果の単一の出所（`VfxSystem` が1回だけ判定を呼び、backend はその結果を配置済み VFX の停止に共有する）、`IVfxBackend` 契約の実 backend での確認（`Create` 失敗時の後始末、`Destroy` の冪等性と破棄済み GameObject・失効した source への耐性、生存中の instance に同じ id を返さない）、消え切り待ち timeout の既定値（d1 は 10 秒）、`BindSceneLifecycle` を SceneDirector の生成直後・最初の Scene のロード前に呼ぶ配線順、`Destroy` 要求と native 実体の破棄完了の差（`Destroy` から戻った後に native 側が source を参照し続けても安全か）、`VfxSpawnRequest.Parent` が null のときの配置先。**d1 の C / C' から送った入力（採否は d2 の Phase A で決める）:** `VfxSystem.Tick` が instance 0 でも毎フレーム `List` を2つ確保すること（Stopping・`Bind` 所有が無いときの確保回避）、拒否（`default(AssetOwner)`・終端中 Scene・破棄済み go・null source）が何も記録せず Null handle を返すことへの診断の要否、`Tick` の全体 try/catch で backend 以外の例外が同じ Tick の残りの handle を飛ばすこと、Unsupported の `VfxSystem` も scene event を購読すること、`BindSceneLifecycle` の `Subscribe` が例外を投げると lifecycle が開いたまま購読なしで Scene 所有の `Spawn` を受理し続けること、`BindSceneLifecycle` の `ReleaseAll` が try の外にあり logger の例外が呼び出し元へ漏れること **d2 の Phase A で決めたこと（完了。契約は [Architecture §24 §16.5・§16.6](../../unity/Assets/Docs/Architecture/24-rendering-system.md#165-composition-と更新)）:** package は Unity 6000.6.0f1 同梱の `com.unity.visualeffectgraph` `17.6.0`（URP `17.6.0` と依存の版が一致）。Runtime の asmdef 名は `Unity.VisualEffectGraph.Runtime` だが、`VisualEffect` は engine module にあるので asmdef 参照の追加は 0 件の見込み（コンパイルが要求した場合だけ `SampleGame.Common` へ 1 件）。composition は Framework の `AbstractApplicationInitializer` が internal の `VfxRuntimeHost` で持つ（`BeforeSceneLoad` で生成、`new SceneDirector` の直後に `BindSceneLifecycle`、Tick は layer `UpdateLayerIds.Vfx`（order 55）に即時 active・unscaled delta、`ReleaseAll` では `_cts.Cancel` の直後で logger・SceneDirector・AM より前に Dispose）。Game の差し込みは `CreateVfxBackend()` 1 件で、null なら未構成の backend で診断 1 回。backend と判定規則・判定結果の単一の出所（`VfxCapabilityState`）・配置済み VFX の `PlacedVfxActivator`（`VisualEffect` を無効のまま保存し、対応時だけ有効化）は `SampleGame.Common`。Player での描画は d3、Mobile 実機は S-4 で行わない | 実証 Scene（d3〜d5） |
| **d3** VFX Scene 2件 | Season / Cell の VFX を §2 の制作契約どおり別 Scene として配置し、PC で表示でき、非対応環境では止まるか | **完了（2026-10-27）。** 現況は [Architecture §24 §16.6](../../unity/Assets/Docs/Architecture/24-rendering-system.md#166-samplegame-の-vfx-graph-backend)・[§05 §5.13](../../unity/Assets/Docs/Architecture/05-scene.md#513-cell-と職種-companion-の分類)。Player での描画は S-9 へ送った（BS4 Spring Player は smoke の後に終了して Spring に入らない。§24 §16.7）。以下は着手前の A0 入力: `Spring_AtmosphereVFX`（Season）、`Spring_VFX_4_2`（Cell）。d2 の停止の仕組みを実 Scene で確認する。**Phase A の必須検討:** Cell の体積（N-9）。体積の再計算は `StreamByDistance == false` の子だけを親へ畳み、収集は `Renderer`（`includeInactive: true`）に限る。Cell の子である `Spring_VFX_4_2` を保存したあと Cell の `_volume` が変わるかを確かめ、変わったときだけ除外規約を決める。`Spring_AtmosphereVFX` の畳み先は Season ノードで、Cell の AABB には入らない。d4 で VFX の時間の所属を導入するときに、d3 で配置した VFX の component や設定に後から変更が要るかは d4 が判断する（d3 の受け入れ条件は増やさない）。**Phase A の決定（A3 凍結）:** Atmosphere は `NecessaryAlways`（§2.1）、作成 transaction を Season Atmosphere へ拡張（§3.4）、Cell VFX の描画 bounds は親 Cell の体積の内側に収める authoring 規約、描画の画素検査は本番と同一の `.vfx` を使う専用テスト Scene、ParticleSystem fallback は後続（§6.1） | Event からの起動（d4）、ParticleSystem fallback（後続の独立スライス、担当未定） |
| **d4a** VFX の時間の領域と配置済み VFX の登録 | 動的・配置済みの全ての VFX が、属する時間の領域（gameplay layer）の Pause / Resume / 時間の倍率に、負荷スパイクを含めて gameplay のコードと同じ時間で追従し、止まっている間は開始もしないか | **Phase A 凍結（2026-10-27、revision 1）。** 正本は [S-4D4A_VFX_TIME_DOMAIN.md](S-4D4A_VFX_TIME_DOMAIN.md)。2026-10-27 の発注者決定で d4 を d4a / d4b に分けた。**Phase A の決定:** 時間は作り方ではなく属する世界で決める（配置済みも `VfxSystem` に `Adopt` で登録し同じ規則、timeout は effect 自身の時間）。時間の領域は既存の `UpdateBehaviourAdapter` の既定 layer `"Gameplay"`（order 0）を `UpdateLayerIds.Gameplay` に定数化したもの。反映点は `Vfx` layer の LateUpdate で、backend へは (paused, rate = 領域の時間 ÷ unscaled delta) を差分で書く。全 `.vfx` は Delta Time + Ignore Time Scale。止まっている間は開始（`Play`・配置済みの有効化）を保留する。配置済みの回収は `IVfxBackend.Release`（GameObject を壊さず、再有効化しない）、所有者は配置した Scene の `Scene(id)`。`VfxCapabilityState` は削除。凍結前に Unity 6000.6.0f1 / VFX Graph 17.6.0 の挙動を実測した（HANDOFF §1.4） | local Event（d4b）、gameplay のコードを gameplay layer へ移して一時停止を作ること（所有スライス未定）、Prefab で動的に置いた配置済み VFX の登録 |
| **d4b** local Event | Cell 内の trigger から VFX を起動し、Cell unload（と所有 go の破棄）で確実に回収できるか | d4a のマージ後に着手する。以下は A0 入力（d4 の行と、分割前の d4 の Phase A で用意したもの）。`Spring_Events_4_2`。trigger と、`AssetOwner.Bind(trigger の go)` で起動する VFX。asset は `SceneBase.LoadSceneScopedAssetAsync`（`AssetOwner.Scene(Events の identity)`）でロードし、unload の順序（PreUnloading → Unity Scene unload で `OnDestroy` → `ReleaseScene`）で「VFX の回収 → asset の解放」を保つ。Events 用の新しい Scene class を作らず、`CellCompanionScene` が root から trigger を集める（internal の `CellCompanionRoleClassifier` を DependOnAll から呼べないため、`GameSceneFactory` に role 分岐を足さない）。trigger 1 つ分の論理（準備前の進入はロード完了時に起動、再入で前の handle を即 `Stop`、退出で `StopAllowingParticlesToFinish`）は純 C# の session に置いて偽 `IVfxSystem` で単体テストする。trigger の `.vfx` の Addressables entry は Editor 操作で API から足す。Planner profile は Events だけを読むため、trigger は `Spring_VFX_4_2` に依存しない。trigger の VFX は gameplay layer に属する。**d1 から送った事項:** `Bind(go)` 用 notifier の `OnDestroy` が実 Scene unload と go の Destroy で回収を呼ぶことの Play Mode 確認、source asset のロード / `Spawn` / 所有者の終端 / source の Release の前後関係が見える実例、同じ identity の Scene の再ロード後の再受理を実 `TestableSceneDirector` で 1 往復（合成 `Subject` では `VfxSystemOwnerTests.AfterRemoved_StillRejected_UntilPreLoading_AndOldHandleDoesNotRevive` が確認済み）。**外部レビューから送った必須検討:** asset のロード中に Cell unload が始まり後からロードが完了しても、終了した trigger が Spawn せず source が残らないこと（`AssetManagement.LoadAssetAsync` は await の後に所有者の Scene の終端を確かめずに登録する。新しいキャンセル機構は作らず、既存の CancellationToken と所有者の終端で確かめ、足りなければ session が完了したロードの handle をすぐ Release する。Framework 側の欠落は所有スライス未定の後続の入力）。Events Scene が `Build Spring Full` と `Build Spring Whitebox` の両方に Scene payload として入ること（Player の動作の証明ではない）。trigger の VFX asset を Content Directory・Player build に乗せる経路は BuildSystem の実装系へ移した（2026-10-27 発注者決定）ので条件にしない | 独立 Scene の大型 Event（d5）、Content Directory の非 Scene asset の production 経路（BuildSystem の実装系、所有 HANDOFF 未作成） |
| **d5** major Event と S-4 の完了 | Cell bundle から独立した Event Scene を Add / Unload し、完了または Season unload で回収できるか。Planner profile で Environment を読まずに Event が動くか | `Spring_Event_MultiCellProof`、その寿命の所有者（S-4b の Season 排他 controller との関係を含め d5 の Phase A で決める）、§6.2 の Planner 実証、本書の harvest と削除。Phase A で major Event と Planner 実証・harvest の見積もりを分け、大きければ追加分割を判断する | S-5 の Tunnel 遷移 |

d5 完了時に Scene 実体は §6.2 の658件になる。

### 6.4 VFX 基盤の責務配置

2026-10-17 の発注者決定。

- **抽象は Framework、VFX Graph に依存する実装は SampleGame に置く。** CameraSystem は Cinemachine backend を Framework 内に置いているが、VFX Graph は URP / Mobile の制約が残るため Framework に依存を持たせない。Framework へ移すかは、実装が育った後に別途判断する。
- Framework が持つ責務は次の3つ。
  - **寿命の宣言と回収:** 起動時に `AssetOwner` のスコープを指定させ、所有者が unload されたら Framework が回収する。Framework が回収するのは再生中のインスタンスで、asset の寿命は §2.4 どおり `IAssetManagement` に残す。所有者の unload は消え切り待ちの Stop を打ち切って回収する。
  - **非対応環境での停止:** backend が起動時に1回、対応可否を返す（判定の実装は d2）。Framework は非対応なら何もしない handle を返し、診断ログを1回だけ出す。Game 側に分岐を書かせない。
  - **型に依存しない handle:** Play、即時 Stop、粒子が消え切るのを待つ Stop、名前付きパラメータ。`VisualEffect` 型を公開面に出さない。
- source asset の寿命（d1 の Phase A で決定）: `Spawn` は呼び出し側がロード済みの `UnityEngine.Object` を受け取り、Framework は asset handle を保持・Release しない。呼び出し側は、その source から作った instance が回収されるまで source を有効に保つ。asset と VFX に同じ `AssetOwner` を使っても解放順は保証されない。
- 回収の保証範囲（d1 の Phase A で決定）: Framework が保証するのは、所有者の終端の後に backend へ破棄を要求する時点と回数である。backend 自身の破棄失敗からの回復は backend の契約に置く。
- 最初は入れないもの: 同時数の上限、優先度、カメラ距離での間引き、pool / prewarm、DebugStudio での観測。必要になったスライスの Phase A で判断する。
- Scene に置く VFX（d3）の寿命は Scene の寿命で回収されるため、d1 の寿命管理の対象は動的に起動する VFX（d4b / d5）である。非対応時の停止は両方に適用する。何もしない handle は Scene に配置済みの `VisualEffect` を止めないので、配置済みのものを止める処理は SampleGame の backend が持つ（d2 で入れ、d3 で実 Scene を確認）。Scene 側は起動時の判定結果を見るだけで、追加の診断ログを出さない。

---

## 10. 公開APIと依存

Framework公開面の追加は次に限定する。

- SceneDirectorの起動時Scene Variant指定。
- AbstractApplicationInitializerのScene Variant解決口とEditor runtime bridge。
- `IRenderEnvironment`、`RenderEnvironmentState`、所有lease。
- VFX の抽象（§6.4 の handle、寿命の宣言、非対応時の停止、backend の差し込み口）。d1 の Phase A で、公開型を `IVfxSystem`、`VfxSystem`、`IVfxHandle`、`VfxSpawnRequest`、`IVfxBackend`、`VfxInstanceId` の6つに決めた（namespace `OneStarMaker.Runtime.Rendering.Vfx`）。d2 の Phase A で、`AbstractApplicationInitializer.CreateVfxBackend()`（protected virtual、既定 null）と `UpdateLayerIds.Vfx` / `VfxLayerOrder`（定数）の 2 件を加えた。d4a の Phase A で、`AbstractApplicationInitializer.Vfx`（protected、Game が `IVfxSystem` を受け取る経路）、`IVfxSystem.Adopt`、`IVfxBackend.SetPlayback` / `Adopt` / `Release`、`VfxSpawnRequest.TimeLayerId` / `WithUnscaledTime`、`UpdateLayerIds.Gameplay` / `GameplayLayerOrder`、`UpdateCoordinator.TryGetLayer` を加えた。

Frameworkはseason名、Cell座標文法、職種roleを知らない。Game → Frameworkの依存方向を維持する。
VFX Graph依存はSampleGame側に閉じ、OneStarMaker Runtimeの公開APIへ`VisualEffect`型を出さない。

`SceneState`の14値は変更しない。新しいLoadTypeも追加しない。asmdef参照追加はS-4d2 HANDOFFで
`Unity.VisualEffectGraph.Runtime`の実assembly名を確認し、0件の見込み（コンパイルが要求した場合だけ`SampleGame.Common`へ1件）と固定した。

---

## 11. 検証方針

658 Sceneを個別に目視しない。破壊的生成と制作契約に必要な集約ゲートだけを行う。
S-4a〜cは完了時の条件を残すもので、撤去済み生成器の再実行や完了HANDOFFの復活を要求しない。S-4cの判定範囲は§5冒頭のharvest先を参照する。

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

括弧内は主に確認するスライス。

- PCでSeason / Cell VFXを表示できる（d2 で1件、d3 で Season / Cell。d3 は Editor の Play。Player での描画は S-9）。
- Mobile非対応時に例外、未解放handle、無限retryがない（d1 は偽 backend、d2 は実 backend）。
- 非対応時、`Spring_AtmosphereVFX` / `Spring_VFX_4_2` をロードしても例外・有効な `VisualEffect`・追加の診断ログがない（d3）。
- VFX Scene の追加で Cell の距離ストリーミング体積が意図せず変わらない（d3）。
- Planner profileでEnvironmentをロードせずWhitebox上のEventが動く（d5）。
- local EventはCell unload、major Eventは完了またはSeason unloadで回収される（d4b / d5）。
- 動的・配置済みの VFX が gameplay layer の Pause / Resume / 時間の倍率に同じ規則で追従する（d4a）。

操作権限の正本は [AGENTS.md](../../AGENTS.md) と [osm-unity-editor](../../.agents/skills/osm-unity-editor/SKILL.md)、テスト担当は [osm-workflow](../../.agents/skills/osm-workflow/SKILL.md)。旧CLIの固定versionや個人pathを前提にしない。

ローカルでは `tools/unity-editor.cmd status` で対象projectを確認し、必要なら正しい版・pathでEditorを起動してよい。人間の手動起動を待たない。Phase BはEditor操作・compile確認までとし、UnityテストとBuildはPhase Cへ渡す。Phase CはEditorを適切に閉じ、標準の `pwsh tools/run-tests.ps1` と必要なBuildを実行する。人間のEditorを強制終了しない。

`unity test` / `unity run`、CloudでのUnity CLI、Scene / asset YAML手編集は禁止。CLI更新・command discovery・承認範囲はSkillに従い、旧大量生成器を復活させない。

---

## 12. スライス境界とharvest

順序は `S-4a -> S-4b -> S-4c -> S-4d1 -> … -> S-4d4a -> S-4d4b -> S-4d5 -> S-5`。

各スライスは着手時に専用HANDOFFを作り、責務マップ、現在行数、予想増分、公開API、所有者、寿命、
単体テスト境界を固定する。500行、3責務、50%以上増加の警報が出た場合は、分割または非分割の理由を
HANDOFFへ書く。本書にない公開API、依存、状態、所有者が必要になったらPhase Bで決めずPhase Aへ戻す。

d1〜d4b の各 Phase D は、そのスライスで現況になった契約を、Phase A で選んだ既存の Architecture 文書へ harvest し（新しい文書を起こす前に既存への統合を確認する）、自分の HANDOFF を削除する。本書は残し、§6.3 の該当行を完了に更新する。
d5 の Phase D で、残る S-4d 固有の契約を Architecture §05 / §18 / §24 / §27 と Streaming 現状仕様へ harvest し、
本書と d5 HANDOFF を削除する。S-4a〜c HANDOFFはharvest後に削除済みであり、復活させない。後続S-5が必要とするSeason排他controllerの公開面だけは、
harvest後のArchitecture文書を参照する。
