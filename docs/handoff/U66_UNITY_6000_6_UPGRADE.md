# U66 — Unity 6.6 migration Phase A HANDOFF

## 0. メタデータ

- type: `slice`
- status: `A3 revision 2 frozen / B retained / C FAIL / C' FAIL complete / D pending` — 2026-09-13 の人間判断で既存6.6実装を保持したままA2/A3を完了。Phase Cは既存whitelist不整合でAddressables/Player buildが停止したためFAIL。Grokによるblind Phase C'も移行PASS不可と判定した。Player/Season互換は主張せず、Phase Dの人間判断を待つ。
- branch: `codex/u66-phase-a`（この U66 スライスを継続。PR base は `develop`）
- implementation base commit: `3244f3635c6e18fffc1bbc40d3126e6d1eee009a`
- implementation head commit: `4263a33ee0d5e75b67e82e6260ff556f12fd1dce`
- risk: `high`（Editor/package/serialization/Player の移行）
- owner: Phase A 主担当 Codex / GPT-6（OpenAI）、A3・D 判断は人間
- created: 2026-09-12 JST。PR #50との追加照合: 2026-09-13 JST。
- expires: U66 Phase D で harvest 後削除。未凍結のまま 2026-09-26 に達した場合、または base/package/採用 Editor を変更する場合、A0 を再検証する。
- harvest to: `AGENTS.md`、`README.md`、`unity/Assets/README.md`（実際の Editor/package/セットアップ）、`unity/Assets/Docs/Architecture/20-variant-checkout-workflow.md`（検証済みの既存 build 運用だけ）、`docs/streaming/STREAMING_CURRENT_SPEC.md`（検証済みの回帰結果）、必要な wrapper 修正は `tools/run-tests.ps1` 自身。`docs/README.md` の作業台一覧は削除時にも更新する。
- Phase A snapshot: A1初稿は `artifacts/u66-phase-a/A1-snapshot.md`。A2 2件と凍結済みrevision 2は `artifacts/u66-phase-a/A2-architecture.md`、`A2-packages.md`、`A3-frozen.md`。生成時刻とSHA-256は `artifacts/u66-phase-a/manifest.json`。実装入力の正本は `A3-frozen.md` とする。
- Phase B result snapshot: `artifacts/u66-phase-c/B-result.md`（2026-09-13 JST、hashは `artifacts/u66-phase-c/manifest.json`）。
- evidence bundle: `artifacts/u66-phase-c/evidence.md`（2026-09-13 JST、hashは同manifest）。
- C' blind bundle: `artifacts/u66-phase-c-prime/blind-input.md`、監査結果 `result.md`。生成時刻とSHA-256は同ディレクトリの `manifest.json`。

## 1. 目的・対象外・A0 現況

### 目的

`6000.5.0f1 (88b47c5e7076)` から **`6000.6.0f1 (f7f8ed4d1e24)`** へ移行し、既存 OSM の compile、テスト、代表 Play、制作操作、既存 Addressables/Player build を再検証する。次の CD0 を開始できる Editor と再現可能な project baseline を残す。Content Directories の実用性証明は CD0 の責任とする。

### 対象外

Content Directories API 呼出し・spike・package 3.x schema 導入、BuildTag、BuildVariantProfile/VariantWhitelistBuilder/Addressables pipeline 再設計、SceneResource payload 変更、bootstrap/配信/cache の再構築、旧 API 削除、世界再生成、全 asset の任意 reserialize、描画最適化、light bake backend 切替、CI 新設、全プラットフォーム対応は含めない。Reference は問題設定の参考に留め、コード・設計のコピーをしない。OSM の境界・寿命・再現性を評価軸とする。

### A0 固定入力と観測

- 2026-09-12 に `git fetch origin develop` 成功。HEAD / origin/develop は上記 base と一致。着手前差分はユーザーの Pre-Phase A v3 文書が untracked のみ。専用ブランチを作成した。ローカル計画書は全文読了し、必要条件は本書へ転記したため、別 checkout に原本は不要。
- `AGENTS.md`、workflow 全文、および phases-and-handoff / docs-policy / handoff-template / architecture-gates / review-evidence と `docs/README.md` を確認。Editor 操作契約も確認した。本ターンに Editor 接続・起動・更新・テストは行っていない。
- `unity/ProjectSettings/ProjectVersion.txt`、manifest/lock を現況の正本とする。README の UniTask 2.5.10、R3 1.3.0 は現物より古い。ローカル PackageCache の表示は UniTask 2.5.11、LitMotion 2.0.2、CsprojModifier 1.3.0、NuGetForUnity 4.5.0。ただし再現のキーは下記 lock commit であり、cache の表示だけを保証にしない。
- Scene は Assets 全体 661。現行構成は SampleGame 659、Season 652（四季×54 Cell の Full 216 / Whitebox 216 / Environment 216、Lighting 4）。移行前に tracked 全 path/GUID リストも保存し、件数だけの一致で代替しない。
- Spring の spawn は `Spring_Cell_0_4`、各 Season の候補は直下の `StreamByDistance` 子54。距離政策は XZ 中心距離、load 375 / unload 550 / maxInFlight 2。全661 Sceneを同時ロードする仕様ではない。
- `assets:sceneVariant` は既定 `""`（Full）と `Whitebox` の**起動時選択**。`world:cellCompanionSet` の既定は `Full`。payload と companion は別の設定軸であり、実行中 Variant 切替を U66 の要件にしない。
- Runtime asmdef は Foundation、UniTask、Unity.Addressables、Unity.ResourceManager、LitMotion、Unity.InputSystem、UniTask.Addressables、Unity.Cinemachine を参照。Editor asmdef は Editor 限定で Runtime と Addressables Editor を参照。全12 asmdef の現物を baseline に保存する。
- URP は `Assets/Settings/PC_RPAsset.asset` / `PC_Renderer.asset` と Mobile 対、GlobalSettings、VolumeProfile。GraphicsSettings の pipeline GUID は `4b83569d67af61e458304325a23e5dfd`。QualitySettings 側の割当も保存する。
- EditorSettings は text serialization、inline mapping 有効、EnterPlayModeOptionsEnabled=1 / Options=1（domain reload 無効）。新 Input System（activeInputHandler=1）。Android のみ scriptingBackend=1 の明示があり、Windows の backend は C preflight で有効値を採取する。
- `VariantPlayerBuild` は `Assets/Scenes/SampleScene.unity` 1枚を Player に指定し、`Assets/SampleGame/Config/app-config.json` を build 中 overlay、finally で元 bytes に復元する。U66 はこれを改善しない。
- `EditorBuildSettings.asset` の有効SceneはTitle1枚で、上記build経路と異なる。`assetCheckout:firstSceneIdentify` は書き込み側しかなく、runtime読者は未実装（source検索とArchitecture §20を照合）。U66のPlayer回帰は既存SampleScene起動到達点を基準とし、Spring到達を新機能として要求しない。Spring実動作はEditor側で検証する。
- 公開 streaming 文書は 2026-09-12 の679/679、failed 0 を報告する。一方ローカル `TestResults/results-all-20260909-014308.xml` は681/681。異なる実行日・入力を同一 evidence と見なさない。base に紐づく679件の生 XML/log/hash は今回未確定。Play実証 T-07〜T-09 は文書上未了であり「既に全て動いていた」とは断定できない。
- ローカル比較資料からは検証の再現性という観点だけを参考にした。外部 CI の採用や比較資料への依存は本 HANDOFF に持ち込まない。

### 公式情報との照合・Pre-Phase A の訂正

調査日は2026-09-12。根拠 URL は §10。

1. 6000.6.0f1 の2026-08-31公開は公式で確認できた。公式 releases API の6.6一覧先頭も同版、recommended=true、revision=f7f8ed4d1e24。今回確認した一覧に newer stable はないためこの exact 版を選ぶ。凍結後に自動で latest へ追随しない。
2. release notes の package list はこの project の解決結果ではない。「Addressables built-in 2.11.2」と呼ぶのは不正確。registry package と Editor core package を区別する。current lock は Addressables を registry と記録している。
3. Pre-Phase の主要5 packageだけでは足りない。Cinemachine core化（package番号6.6.0）、Test Framework 1.8.0、Collections 6.6.0、Burst 2.0.0、Timeline 6.6.0、uGUI 2.6.0、Performance testing 6.6.0 も差分対象。release list の旧番号を current 番号に転記しない（例: current IAP 5.4.2 を release list の4.15.0へ戻さない）。
4. migration guide は YAML wrapping変更を記すが、本 project は既に inline mapping 有効。661 Scene の全再保存を当然視しない。Terrain は manifest に直接宣言済みなので、RP Core の Terrain 依存削除を理由に新しい dependency edge は不要。
5. Full/Whitebox は runtime hot switch ではない。Spring216 Cell という表現も不正確で、Springは54、四季合計216。本文の代表検証は現行契約に合わせた。
6. 「Player build が可能なら」は証拠なしの完了を許すため強化する。CでWindows build/runを必須 gate とし、pre-existing blocker は migration regression と分けて A3/A再開へ返す。新 build architecture で埋めない。
7. Content Directories の紹介は確認したが、pre-phase の細かいAPI名・寿命・増分hash・HTTP再配置成立を U66 の確定契約へ転記しない。CD0 の検証課題としてのみ残す。

### PR #50（Grok案）の参照結果

参照したのは [PR #50](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/50)、head `0d96cd5de4e813d701512b12a7f3020b3641f404`。マージ/cherry-pickはしない。独立に作られたA1案であり、この初稿をレビューしたA2結果ではない。以下は主担当の暫定採否で、人間のA3凍結ではない。

- 採用: firstSceneIdentify読者未実装、EditorBuildSettingsとPlayer起点の違い、root READMEのharvest漏れを補う。sourceと現況文書で確認した。PlayerでSpring到達を要求していた本初稿T8は既存起動点との比較に訂正した。
- 採用: Timeline core化。公式What's Newで確認。Managed Code Variantは自前コードに旧defineがなくてもpackage診断に影響するため、CのPlayer証拠に実効variantを追加。既定Releaseで診断が減ることを、単なる成功として見逃さない。診断維持の設定変更が必要ならA3で具体的target/値を固定する。
- 不採用（根拠不足）: 「2.11.2にCD schemaあり」。What's Newのschema/convertリンク先は `com.unity.addressables@4.0` であり、release listの2.11.2と合成して機能保証はできない。2.11.2での有無は未検証のままCD0へ渡す。3.xのみというpre-phaseの断定も採用しない。
- 不採用: Phase B開始時の任意patch差替え、具体的mapなしのasmdef名変更、compile目的ならGit hash bump可、PoCを対象外から除く表現。いずれもA再開を必要とし、U66でCD PoCは行わない。
- 不採用: rollbackをbranch不使用だけで完了とする、旧baseline欠如を絶対gateだけで代替する、Playerを無条件optionalとする。Git外journal/cacheの保全、旧版への復帰確認、既存到達点でのbuild/run証拠を本書のまま維持する。
- 補足: 現在の6000.5版表記は移行前には正しく、それ自体をstaleとは扱わない。6.6実装が成立した時点で公開面を更新する。

## 2. 受け入れ条件・制約

### Acceptance criteria

- AC1: exact Editor/revision の初回 import と再openが完了し、compile error / unresolved package / Safe Mode がない。manifest+UPM生成lockから同じGit commitとpackage版を復元できる。
- AC2: §3 の移行表と実解決結果を全 dependency について照合。NuGet25件の版、plugin importer、analyzer、asmdefを保存し、予定外更新や重複 assembly がない。
- AC3: Cで全 EditMode を標準 wrapper・filterなしで実行。1件以上、failed 0、unexpected skip 0。baselineとの差のテスト名一覧を説明し、679という件数を偽装して合わせない。C# compiler、Burst、Player側compileも別々に確認する。
- AC4: 全661 Sceneの path/GUID、SceneResource/Graph/Map/Addressables 関係、payload参照、親子、volume/stream flag が baseline と一致。全件機械検査と代表操作を組み合わせ、全件目視したとは書かない。変更assetに missing script/参照切れがない。
- AC5: §5 のSpring起動、streaming往復、Full/Whitebox別起動、World Workspace、入力/Camera/URP/UI、停止再開、telemetryを実機で通す。new Error/Exceptionなし。移行前に再現する障害は別 ledger で管理し、暗黙の免除をしない。
- AC6: Cで既存 Addressables packed build とWindows x64 Player build/runを完了し、移行前のSampleScene起動到達点から退行しない。firstSceneIdentify読者やPlayerからのSpring到達は新設しない。使用profile、content集合、出力、build report、Player logを保存し、成功/失敗時ともgroup/configの復元を確認する。既存経路の制約でpacked contentの実ロードまで確認できない場合、その範囲を未検証としてA3に明記し、Editor AssetDatabase成功で代替しない。
- AC7: 6.5/6.6のLibrary、build出力、catalog/cacheを混用せず復帰手順を実証できる。破損や未復元journalを残さない。
- AC8: CD0用にexact Editor、compile可能な依存、Windows build環境、既存 baseline evidenceを揃えたと記録する。Content Directory build/register/loadは未検証と明示し、U66で本実装しない。

### 本文へ転記した実装契約

依存は Game → Framework、全体配線は DependOnAll。公開資産操作は IAssetManagement と AssetOwner(App/Manual/Scene(id)/Bind(go))。SceneState14値と順序、SceneLifecycleManagerによる変更所有を維持する。公開ログは ILogger<T>。UpdateはUpdateSystemRuntimeへ登録し、ActivatePendingRegistrations → RunUpdate → RunLateUpdate → ApplyMainThreadChanges → ApplyStructuralChangesの順序と、単一system例外による他Tick停止禁止を維持する。

EditorコードはEditor asmdefに隔離。新規/編集Unity C#先頭は `#nullable enable`、Unity側record禁止、破棄されうるUnityEngine.Objectの判定に `?.` / `??` / `is null` / ReferenceEquals を使わず `== null` / `!= null`。テストにTask.Delay/Thread.Sleepを導入しない。参照0だけで削除しない。

**本revisionで許可する公開API変更・asmdef edge追加は0件。** compiler互換修理は既存classの内部に限定するが、具体的なcompile障害・対象ファイル・最小修理差分をAへ戻して責務マップを追補してから実装する。obsolete warningだけを理由とする一括置換はしない。package名変更に伴うasmdef修理も独断で行わない。API Updaterの自動結果も同じ審査対象。

Phase BはUnity.exeを起動せず、run-tests.ps1/Addressables buildを実行しない。人間が開いたtarget Editorがreadyなら、実際に公開されている名前付きPipeline command優先、足りない場合のみeval。操作前にosm-unity-editorとunity-cliの該当規約を読む。接続可能Editorの `.unity/.prefab/.asset` はYAML手編集しない。Safe ModeをYAML編集で迂回しない。CloudではUnity CLIを呼ばない。world生成器を復活・再実行しない。Cでも `unity test` / `unity run` を使用しない。

## 3. Package migration table と責務マップ

### Package migration table

「target」はA1の提案pinであり、このターンに更新していない。release-note lineと実UPM結果を別列で記録する。lockは手編集・削除による全更新をしない。Git URLは既存pathを保持して `#<下記commit>` を付け、UPMにlock生成を任せる。以下以外のtransitive差分は依存元・要求版・選択理由を列挙し、非coreの予定外major変更はAへ返す。

| package / 現在 | 6.6公式line / U66提案 | 検証・リスク |
|---|---|---|
| Addressables 2.9.1 | 2.11.2 / 更新 | registry。既存packed/Hybrid/catalog/restore、UniTask.Addressablesを検証。3.x禁止 |
| SBP 2.6.1 transitive | 3.0.3 / Addressablesから解決 | major更新。custom BuildScriptのcompileとcontent build必須。直接pin追加はしない |
| URP/Core/ShaderGraph/Universal-config 17.5.0 | 17.6.0 / Editor対応lineへ | PC/Mobile renderer、Volume、shader stripping。HDRP/VFX packageを新規追加しない |
| Input System 1.19.0 | 1.20.0 / 更新 | keyboard/mouse移動・UI・再Playで重複購読なし |
| AI Navigation 2.0.13 | 2.0.14 / 更新 | compile/既存NavMesh参照検査。用途がなければその事実を記録、デモ新設なし |
| Cinemachine 3.1.7 | 6.6.0 / core版 | package番号変更。Cinemachine 2→3変換を適用しない。Unity.Cinemachine参照・camera stack/blend検証 |
| Test Framework 1.7.0 | 1.8.0 / core版 | 全EditMode discovery・XML・skip差分。既存テストを削除して通さない |
| Collections 6.5.0 | 6.6.0 / core版 | transitive。UpdateSystem/LitMotionのcompileとdispose |
| Burst 1.8.29 | 2.0.0 / 6.6line提案 | transitive major。既存minimum指定とUPM実解決を確認、LitMotion/Jobs/Player AOT検証。暗黙に1.8系を保証しない |
| Performance tests 3.5.0 | 6.6.0 / core版 | transitive、Collections経由。発見件数とcompile確認 |
| Timeline 1.8.12 | 6.6.0 / core版 | 自動変更を記録、既存serialized参照検査 |
| uGUI 2.5.0 | 2.6.0 / core版 | UI表示・Blocker・TMP・入力 |
| Splines 2.8.4 | 2.9.0 / Cinemachine依存解決提案 | transitive。lockの依存要求を確認 |
| LitMotion 2.0.2(cache表示) | Unity指定なし / commit `2053ef5c23f2ae755dd85d5865b698c542c351b6` 維持 | Burst/Collections minimumは6.6互換保証ではない。tween完了/cancel/dispose |
| CsprojModifier 1.3.0(cache表示) | Unity指定なし / `3c9be1a827ce7a2e0d9518e34905fc2fc7a6d5df` 維持 | csproj再生成・analyzer参照。IDE生成成功とUnity compileを別検証 |
| UniTask 2.5.11(cache表示) | Unity指定なし / `e5acc106ee196bc5a32fb14cdf2987b0f96d11e0` 維持 | PlayerLoop、domain reload無効で2回Play、cancel/Addressables await |
| NuGetForUnity 4.5.0(cache表示) | Unity指定なし / `c2af83c9d4f8cdaada9d4a0e94de2f195d8e1d01` 維持 | fresh restore、plugin importer、MessagePack analyzer。upstream READMEはexact6.6認証ではない |
| Pipeline 0.4.0-exp.1 | exact6.6推奨未確認 / 維持 | human初回open後ready/command discovery。接続不能を新Editor起動で回避しない |
| Multiplayer Center 1.0.1 builtin | 2.0.1 / UPM `updateDependencies`結果をA3採用 | Editor解決値を固定。runtime multiplayer導入なし |
| Ads 4.16.4、collab-proxy 2.12.4、VisualScripting 1.9.11 | 4.19.0 / 2.13.6 / 1.9.12 / UPM `updateDependencies`結果をA3採用 | 人間の実装保持判断とEditor logのexact一覧に基づく。機能追加はしない |
| IAP 5.4.2、Analytics 3.8.2 | list IAP4.15.0は移行指示に使わない / 維持 | downgradeしない。services.analytics6.3.0、services.core1.18.0維持 |
| Rider3.0.39、VS2.0.26、Auditor rules1.0.3、XR legacy3.0.1 | current維持 | IDE連携/compile確認。非使用を理由に削除しない |
| builtin modules1.0.0、2D sprite/tilemap1.0.0 | Editor同梱値。tetgen 1.0.0追加をA3採用 | Terrain/TerrainPhysics明示依存を維持。tetgenは6.6 UPM `updateDependencies`結果のみを保持し、新機能利用はしない |

NuGet `Assets/packages.config` の25件は全維持: MessagePack/Annotations/Analyzer3.1.7、Bcl.AsyncInterfaces/TimeProvider8.0.0、Extensions.DependencyInjection/Abstractions/Logging/Logging.Abstractions/Options/Primitives8.0.0、NET.StringTools17.11.4、ObservableCollections3.3.4、R3 1.3.1、Collections.Immutable8.0.0、ComponentModel.Annotations5.0.0、Diagnostics.DiagnosticSource8.0.0、Text.Encodings.Web8.0.0、Text.Json8.0.5、Threading.Channels8.0.0、Utf8StringInterpolation1.3.1、VContainer1.0.2、ZLogger2.5.10、ZString2.6.0、ZStringFormatExtension0.0.6。復元で違う版や新dependencyが必要なら停止する。

### 責務マップ・変更候補ファイル

新しい中核ロジック・class・namespace・所有者・寿命は作らない。以下の行数はA0現物。予想増分は修理予算であり、使い切る目標ではない。

| ファイル（repo相対）/ 現在行数 | 責務・変更理由 / 予想増分 | 所有者・依存・公開面・検証境界 |
|---|---|---|
| unity/ProjectSettings/ProjectVersion.txt /2 | Editor pin /0 | Unity生成、project寿命。version/revision一致 |
| unity/Packages/manifest.json /63 | direct dependency宣言、Git SHA固定 /0〜5 | project、UPM入力。公開APIなし、JSON/resolve検証 |
| unity/Packages/packages-lock.json /670 | 解決結果 /0〜100目安 | UPM生成専用。source/depth/hash/dependency比較。500行超でも生成graphなので分割しない |
| unity/ProjectSettings/ProjectSettings.asset /950 | platform serialization /0〜50目安 | Unity生成設定。backend/stripping/graphicsの意味を保存。宣言的単一assetなので分割しない |
| unity/ProjectSettings/GraphicsSettings.asset /69、QualitySettings.asset /135、EditorSettings.asset /53 | unavoidable upgrade /各0〜20 | Editor所有、pipeline/品質/Play設定の意味比較。方針変更は禁止 |
| unity/Assets/Settings/ の既存7 asset | URP/renderer/volumeのschema upgrade /既定0、必要時のみ | 現在行数: PC_RPAsset143 / PC_Renderer95 / Mobile_RPAsset143 / Mobile_Renderer52 / UniversalRenderPipelineGlobalSettings449 / SampleSceneProfile159 / DefaultVolumeProfile982。Editor I/O、project資産。982行のProfileは宣言assetで分割しない。スクリーンショット比較 |
| unity/Assets のScene/Prefab/SceneResource/AddressableAssetsData | 必須自動serializationのみ /既定0 | GUID・親子・payload・寿命は変更不可。変更集合の全件検査、代表操作 |
| unity/Assets/packages.config /28 と Packages plugin群 | 復元境界 /0 | NuGet所有。版・importer・analyzerを検証。通常は変更なし |
| tools/run-tests.ps1 /258 | test runner /既定0 | Cのprocess/XML I/O。versionは既にProjectVersionから導出。hard-code修理不要 |
| Build/Variants/VariantPlayerBuild.cs /192、BuildVariantProfile.cs /115、VariantWhitelistBuilder.cs /172（OneStarMaker/Scripts/Editor配下） | 既存orchestration/SO/selection /既定0 | Editor境界を維持。公開面変更禁止。必要な互換修理はA再開で個別固定 |
| 全12 asmdef、Runtime/Camera・AssetManagement・UpdateSystem、SampleGame/DependOnAll | package利用境界 /既定0 | Game→FW・既存App/Scene/Bind寿命。compile失敗時に対象を特定してA追補 |

asset行数は変更が発生した時に凍結前のinventoryへ追補する。500行/3責務/50%増加の警報は自動分割命令にしない。修理が新policyやmanagerを必要としたらA再開。新中核ロジックを予定しないため新しい純粋単体テストは原則不要。互換修理を承認した場合のみ既存境界の回帰テストを追加する。

## 4. 実装順序・停止・rollback

1. **A3前 preflight**: 6.5の同一baseで全テスト生証拠を確保する担当を別セッションに置く（Phase Bではない）。Spring/Variant/Workspace/既存content+Player buildの移行前成否、profile GUID、backend、graphics API、使用config、実行手順を記録する。既存profileが未配備なら検証用profileの具体的な一時fixtureとcleanupをAへ提示し、production配備や再設計はしない。未決のままBへ渡さない。
2. 人間の編集中Sceneとdirty stateを保存・分離し、6.5の元checkoutを残す。専用6.6 worktreeにはLibrary/Tempを共有しない。Git外のuser config、DeveloperVariantSettings、Addressables snapshot、World Workspace journalも保存する。
3. Bはmanifestの承認済みpinを準備。人間がHubでexact EditorとWindows moduleをinstallし、対象worktreeの `unity/` を初回openする。人間がupgrade/API Updater表示を記録。Bは起動しない。
4. UPM/import完了後のlockと全tracked diffを取得。serialization、package、source互換、環境設定を別々に説明する。Editor操作はreadyとproject path/versionを確認してから行う。
5. 必須自動upgradeのasset差分だけを含める。GUID/fileID、参照、profile、色/品質、scene tree、volume/stream flag、AuthoredRoot transformの意味が変われば停止。任意のForceReserializeAssets、全Scene save、lighting/navmesh再bake、renderer作り直しをしない。
6. Bはcompile修理の新判断をAへ返す。承認済み範囲の編集後は `pwsh tools/contract-audit.ps1`、文書編集には `pwsh tools/docs-audit.ps1`。B result snapshotにUnity tests/Addressables build未実行を明記し、Cへ渡す。

### Phase B 停止条件

未凍結HANDOFF、baseの実質変更、package表外のmajor/update/removal、third-party Git hash drift、NuGet版変更、Pipeline未接続、初回open未実施、Safe Mode/UPM循環、asmdef edge/API/owner/lifetime/state追加、説明不能な大量serialization差分、missing script/GUID再生成、URP見た目の破損、wrapper起動不能、既存build不成立を迂回する新pipeline、CD API導入要求で停止しAへ返す。実装者が「後で消すから」で削除・テスト弱化しない。

### Rollback / failure handling

- 6.5と6.6のEditorを同一projectに同時接続しない。復帰は6.5の未移行checkout＋元manifest/lock＋同版専用cacheで行う。6.6で更新したLibraryを6.5へ流用せず、branch切替だけでdowngrade完了としない。
- import/compile crashはEditor.log、UPM log、version、diff、最後の操作を保存して中止。再試行で差分を覆わない。別の空cacheで再現するか確認する場合も人間のopenとCのテスト責任を守る。
- `Library/OneStarMaker/VariantFilteringBuildSnapshot.json` と `Library/OneStarMaker/WorldWorkspace/pending-companion-create.json` はGit外の復旧状態。存在時は未完了transactionを確認して既存復旧経路で解決する。証拠保全なしにLibraryごと削除しない。
- build出力/catalog/Addressables cacheはversion別directoryへ隔離。6.5 bundleを6.6 Playerに混ぜない。共有remote配信先へpublishしない。group snapshotとapp-config元bytesのhashをbuild前後で照合する。
- 失敗時にuser差分をgit reset/cleanで消さない。削除が必要ならEditor終了・絶対pathの対象worktree内包含・保存済み証拠を確認し、対象を限定する。merge後のrollbackはdevelop直接resetではなく専用revert PRで人間判断。

## 5. テスト・レビュー計画

### Cでの回帰手順と証拠

- T1: Editorを閉じて `pwsh tools/run-tests.ps1`（filterなし、exact Editorを解決）。必要なgraphics testのみ `-WithGraphics` を理由付きで使用。exit0のみでなくXMLのtotal/failed/skipped、compile出力を保存。`0xC0000005` は完成XMLとログ末尾で判定し、crash自体を隠さない。
- T2: 全testにはScene/AssetManagement/UpdateSystem、Camera/CinemachineBackend、Bootstrap/SceneVariantResolver/InvalidCompanionConfigStartup、Streaming/SeasonCandidateSelection/SessionSeasonController/PlayerWorldReadySequence、Editor/Buildの3 Variant test、Editor/WorldAuthoringのselection/transaction/recoveryを含むことをtest名で確認する。filtered成功を全件成功に代用しない。
- T3: 全661Sceneおよび関連Resource/Graph/Map/Addressablesの機械inventoryをbaseと比較し、4季×54 membership、Full/Whitebox/Environment各216、Lighting4を確認。全assetのGUID、参照先、missing script、Scene treeを検査。代表としてSpring spawnと隣接Cell、Whitebox、Environment、Lighting、bootstrap/UIを開く。
- T4: 人間が起動した6.6 EditorでFull起動し、Spring WorldReady→camera/player操作→隣接Cell往復→unload半径外→戻るを同じ経路で検証。loaded identity/active season/in-flight、load/unload終端、telemetryを記録し、初期点と停止後に残留Scene/handle/二重driverがないことを確認する。
- T5: Stop後にWhiteboxで別起動し、同じlogical Cellで別payloadが選ばれることを確認。companion setはpayloadと別に記録。domain reload無効の現行設定でPlay/Stopを2回繰り返す。入力二重購読、UniTask PlayerLoop/tween残留、DebugSocketの重複を確認。設定変更で不具合を隠さない。
- T6: `OneStarMaker/World Workspace/Open Window` から代表Spring CellをFull/Whiteboxで開き、lighting/companion/active sceneの構成と通常close復帰を確認。dirty scene中のcancelは元scene setupを維持。破壊的生成を既存世界で試さず、transaction/recovery失敗経路は既存Editor testで確認する。
- T7: 同じcamera/quality/graphics APIでFull/Whiteboxの前後画像を保存。pink shader、欠落material、Volume/camera blend、UI overlay/Inputを確認。PC描画必須、Mobile rendererはasset/compileまで（mobile Player未検証と明記）。NavMeshは現存component/データの有無をinventoryし、使用箇所があれば代表loadを追加する。
- T8: C担当が既存packed buildと `OneStarMaker/Build/Build Player (Active Variant)` をWindows x64で実施し、生成Playerを起動、移行前SampleSceneと同じ到達点でnew errorがないことを確認。Full/Whiteboxのpacked selected集合は別々に比較し、未実装firstSceneIdentify経路でSpring起動を強制しない。profile/selected集合/build report/hash/Player log/backend/Managed Code Variant実効値を保存。consoleで例外が捕捉されるためmenuが戻っただけでは成功としない。成功・既存testの失敗注入でconfigとgroupの復元を検証する。移行前のbuild fixtureが未決ならAへ戻す。
- T9: DebugSocket接続・telemetry開始と切断/再起動を確認。受信側未起動なら想定の接続失敗とUnityのnew exceptionを分ける。外部DebugStudio本体の改修は行わない。
- T10: `pwsh tools/contract-audit.ps1` / `pwsh tools/docs-audit.ps1`、diffの構造レビューを実施。source修理があれば原因を再現する既存境界のテストを追加し、純logicをEditor I/Oへ移さない。

各実行にbase/head、UTC/JST時刻、Editor revision、OS/GPU/API/backend、manifest/lock hash、実行command/config/profile GUID、期待値/観測値、raw XML/log/画像/build reportのpath/hashを付ける。表示上の成功や文書の件数のみは証拠としない。U66で性能最適化はしないが、同じ経路のload時間/フリーズがbaselineから悪化したら数字を残しAへ返す。

### Review plan / 未決事項

- A0/A1主担当: Codex / GPT-6 / OpenAI。本稿はU66の初稿成果物。A0だけからrollback/回帰境界を検討する独立担当を使用し、A1本文へのアンカリングを避ける。
- A0代替検討実績: `/root/a0_boundary_review`（主担当と同一モデル系列、新しい入力でA0のみ閲覧）。Git外Addressables/WorldWorkspace journalと版別出力の隔離、既存build復元の提案を採用。sourceの新実装やUnity実行なし。複数モデルA2やA3の代替とはしない。
- A2完了: architecture担当 `/root/u66_a2_architecture`（GPT-6 Astra / OpenAI）とpackage/test/rollback担当 `/root/u66_a2_packages`（GPT-5.6 Luna / OpenAI）が、同じA1 snapshotとbase/headを別contextで独立レビュー。Phase C結論は入力から除外した。成果物とhashはPhase A manifestに記録。
- A3完了: 2026-09-13、人間が「Phase Aを実施し、既存実装は差し戻さない」と判断。主担当がA2指摘を統合し `artifacts/u66-phase-a/A3-frozen.md` をrevision 2として凍結した。package exact集合、3 serialization asset、TMP修理、manifest+lock復元契約を採用。Git URL SHA直書きとwrapper改修は別スライスへdefer。6.5 raw baseline欠如は残存riskとしてacceptし、Play/build gateはCに残す。
- CはBと異なるモデル・新規セッション。C'はB/Cと異なるモデルの新規セッション、可能ならAにも未関与の系列/ベンダーを予約する。現時点の予約は未割当。代替として未関与の人間に依頼し、本人の確認範囲・所見・残存リスク・判定を記録する。事前関与/先にC結論閲覧があれば独立性制約ありとする。
- C/C'開始前に同一implementation base/headの完全diff/stat/name-status、凍結A snapshot、B result、生test結果、C以前の機械出力からbundleを作る。各path/id/生成時刻/hashをmanifestへ記録。C'には可変HANDOFF全文、C結論・findings・疑念候補を渡さない。
- implementation head変更ならbundleとC/C'結果を無効化し同じ新headで再実施。review記録だけのcommitはimplementation headを変えない。findingsにはseverity/category/unique-or-duplicate/採否/Phase/担当/根拠/修正commitまたは不採用理由を残す。

## 6. Phase B 実装結果

2026-09-13 の人間指示により、すでに作業ツリーへ生じていた6.6 Editor/UPM移行をPhase B結果として固定した。implementation baseは `3244f3635c6e18fffc1bbc40d3126e6d1eee009a`、implementation headは `4263a33ee0d5e75b67e82e6260ff556f12fd1dce`。詳細は `artifacts/u66-phase-c/B-result.md`。

- ProjectVersionをexact `6000.6.0f1 (f7f8ed4d1e24)`へ更新し、manifest/lock、URP Global Settings、Project Auditor、Addressables settingsのEditor/UPM生成差分を固定した。
- `DebugProfilerView.cs` の6.6 compile errorは `enableWordWrapping = false` を同等の `textWrappingMode = TextWrappingModes.NoWrap` へ置換した。新規責務、公開API、asmdef edge、owner/lifetime変更はない。
- contract audit、docs audit、JSON parse、diff checkはPASS。Unity test、Play、Addressables/Player buildはBでは未実行。
- 計画との差: A2/A3未完のまま移行が先行した。revision 2のA3で、Editor logのUPM `updateDependencies` provenanceと人間の実装保持判断に基づき、Ads 4.19.0、collab-proxy 2.13.6、VisualScripting 1.9.12、Multiplayer Center 2.0.1、tetgen 1.0.0を含むexact解決集合とTMP修理を事後採用した。履歴上の越境自体は消さない。
- 担当・モデル: Codex / GPT-5（OpenAI）。

## 7. Phase C

revision 2に対するPhase Cを2026-09-13に完了し、判定は **FAIL**。compile/EditModeと基本Play/Workspaceは通過したが、既存content/profileのwhitelist不整合によりAddressablesとWindows Playerを生成できない。U66の範囲では既存ビルド系を修理せず、失敗をbaseline制約として記録する。

- evidence bundle: `artifacts/u66-phase-c/evidence.md`、manifest: `artifacts/u66-phase-c/manifest.json`（2026-09-13 JST）。base/head完全diff、機械検査、raw test path/hashを記録。
- 構造適合: Scene/Prefab/asmdef変更なし。661 Scene（SampleGame 659）、12 asmdefを確認。唯一のC#変更は既存Profiler UI設定の互換置換で、責務・依存・寿命・公開面の増加なし。URP/Addressables/ProjectAuditor差分は宣言assetのversion/schema更新に限定。
- テスト結果: Editor process不在を確認後、implementation headに対し `pwsh tools/run-tests.ps1` をfilterなしで実行。Unity `6000.6.0f1 (f7f8ed4d1e24)`、679 total / 679 passed / 0 failed / 0 skipped、exit 0。XML/logとSHA-256はevidence bundleに記録。要求カテゴリのtest名も検出。初回testでAddressables必須serializationを検出・commitしたため、新headで全件を再実行しtracked tree cleanを確認した。
- machine checks: contract audit PASS、docs audit PASS、manifest/lock JSON parse PASS、diff check PASS、最終logのC# compile errorなし。
- finding P0 / semantic / resolved by A3 revision 2: 未凍結越境を履歴上の事実として残し、現在のexact実装を人間判断で明示採用。実装は巻き戻さない。
- finding P1 / semantic / resolved by A3 revision 2: optional package更新とtetgenはEditor logの `Update Mode: updateDependencies` exact結果および人間の保持判断を根拠に採用。
- finding P1 / semantic / resolved by A3 revision 2: TMP CS0619修理を既存Debug UI責務内の一行互換変更として明示採用。
- Full Playは2回、Whitebox Playは1回enter/stopし、各回Console error 0。FullはUIScene追加loadとstop後の解放を確認。Play中にAudioListener二重警告が反復したが、post-load queryでは`/Main Camera`の1件だったためopen findingとする。
- World Workspaceはメニューから正常起動しConsole error 0。生成/saveは行っていない。
- Production/WorldWhiteboxはいずれも、Home/InGame/各Seasonを含む8 SceneResourceMap IDにwhitelist一致payloadがなくpacked Addressablesが停止。Production Windows PlayerもAddressables prepareで同じ理由によりFAILし、実行物なし。app-configはfinallyで復元され、検証用builder/profile差分と自動生成assetは除去した。
- 人間の2026-09-13指示により、もともと未成立のPlayer buildをU66で直すこと、およびSeason Scene/Cellの実動作修理・確認は後続作業へ延期する。従ってPlayer/Season compatibilityは主張しない。
- 未確認事項: 6.5 raw baseline、NuGet/analyzer、全asset参照/missing script、直接input/camera/UI操作、telemetry/DebugSocket payload、Spring traversal、画像比較、mobile Player、Player run、rollback failure injection。
- 担当・モデル: Phase C実行 Codex / GPT-5（OpenAI）。証拠の独立再点検は `/root/u66_phase_c_final_review`（GPT-6 Astra / OpenAI、別context）が担当し、FAIL判定を妥当と確認。結論を含むbundleを入力したためblind Phase C'とは扱わない。指摘されたmanifest hash、A3参照、生ログ固定、未実施gate表現を修正した。

## 8. Phase C'

2026-09-13にGrokの新規read-onlyセッションでblind監査を実施。

- 担当方式: AI。固定A3、B-result、同一base/headの完全diff、生XML/log、Phase C前の機械検査情報だけを指定したblind inputを使用。
- blind audit bundle id / hash: `artifacts/u66-phase-c-prime/blind-input.md` / `18DC289583776F161D2791A67FDE90EC24C5FCF58A835886370E3BDBE200CA73`。結果とmanifestは同ディレクトリ。
- 確認範囲・方法: implementation 11 pathの構造/契約、Editor/package/generated settings/TMP差分、raw EditMode XML/log、受け入れ条件の証拠充足をread-onlyで監査。Unityは起動せず、実装変更なし。
- 判定: **FAIL（移行PASS不可）**。固定実装の構造はA3許可範囲に適合し追加実装欠陥は見つからないが、blind inputのEditMode証拠だけではPlay/制作/packed Addressables/Player gateを証明できない。
- findings: High—Gate 2〜6のlive証拠不足。Medium—Burst 2.0/Cinemachine 6.6/SBP 3/URP generated resourceのruntime failure path未検証。Low—license handshake環境ノイズ、監査環境のshell hook拒否によりSHA-256独立再計算不能。
- 残存リスク: 6.5 raw baselineなし、rollback/旧Library分離未実証、Mobile未検証、Git URLはlock依存、Player AOT/packed runtime/pink material/camera blend未確認。
- 監査できなかった範囲: Unity Editor/Playerの再実行、監査側でのhash再計算。
- 独立性: Phase B/CのCodex GPT-5系と異なるGrok、新規セッション、Phase C結論とレビューコメントを隔離したためAI最低条件を満たす。凍結A1が別PRのGrok案へ言及するため強化条件は`独立性制約あり`。
- Phase C結論事前閲覧・設計実装関与: 未閲覧・未関与。PRコメント、Phase C evidence/result、HANDOFF §7/§8を読ませていない。
- 担当・モデル: Cursor Agent / `cursor-grok-4.6-xhigh`（Cursor Grok 4.6）。

## 9. Phase D

未実施。人間がC/C'を突合してマージ判断する。§0のharvest先には検証済み現況のみ反映し、U66 HANDOFFを削除する。CD0にはexact Editor/lock/既存baseline/残存riskを入力として渡す。Content Directoriesの成立やBuildSystem redesign完了とは報告しない。

## 10. 外部一次資料（2026-09-12確認）

- [Unity 6000.6.0f1 release notes / Final package changes](https://unity.com/releases/editor/whats-new/6000.6.0f1): 採用版・package line・known issues。UUM-149540（PlayerのUnloadUnusedAssets遅延）、UUM-149781（D3D12 shader unload crash）を代表load/unload観測で留意。存在だけでproject該当と断定しない。
- [Unity releases API](https://services.api.unity.com/unity/editor/release/v1/releases?version=6000.6&limit=10): exact revision/公開日/6.6一覧を照合。web取得失敗後、読み取りHTTPで確認。
- [Upgrade to Unity 6.6](https://docs.unity3d.com/6000.6/Documentation/Manual/UpgradeGuideUnity66.html): Cinemachine core化、YAML、Terrain依存、旧Rendering Debugger API。既存sourceのDebugState/DebugUIDrawer/serializeInlineMappingsOnOneLine/dynamicBatching検索はヒットなし。第三者packageの影響まで否定する根拠ではない。
- [Addressables 2.11 changelog](https://docs.unity3d.com/Packages/com.unity.addressables@2.11/changelog/CHANGELOG.html): SBP更新、domain reload無効時の再初期化修正があり、Editor再Playとpacked buildを別々に確認する。
- [Cinemachine 6.6 changelog](https://docs.unity3d.com/Packages/com.unity.cinemachine@6.6/changelog/CHANGELOG.html): camera依存更新の参照先。
- [New in Unity 6.6](https://docs.unity3d.com/6000.6/Documentation/Manual/WhatsNewUnity66.html): Timeline core化、Addressables CD説明のリンク先は4.0。Editor版とregistry package機能の対応を推測しない。
- [Content Directories introduction](https://docs.unity3d.com/6000.6/Documentation/Manual/content-directories-introduction.html): 次CD0の入口。U66はAPIを導入しない。
- [UniTask upstream](https://github.com/Cysharp/UniTask)、[LitMotion upstream](https://github.com/annulusgames/LitMotion)、[CsprojModifier upstream](https://github.com/Cysharp/CsprojModifier)、[NuGetForUnity upstream](https://github.com/GlitchEnzo/NuGetForUnity): 各用途/依存の一次資料。exact lock commitと6.6の組合せは本projectで未実証。
