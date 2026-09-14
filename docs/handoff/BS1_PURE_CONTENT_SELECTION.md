# BS1 — Pure content-selection core

## 0. メタデータ

- type: `slice`
- status: `A1 draft / A2 pending / A3 not frozen`
- branch: `codex/cd0-u66-phase-d-bs1-phase-a`
- implementation base commit: `bda2ed7`
- implementation head commit: 未到達
- risk: `high`（新しいBuildSystemの中核型、公開境界、依存方向を定める）
- owner: Phase A主担当 Codex / GPT-6 Astra（OpenAI）。A3採否は人間。B/C/C'は開始時に記録する。
- created: 2026-09-15 JST
- expires: 2026-09-29 JST、またはBuildSystem入力、SceneResource graph、VCS境界の前提が変わった時点
- harvest to: `unity/Assets/Docs/Architecture/18-asset-description.md` と新BuildSystemの現況を説明する既存Architecture文書。BS1 Phase Dでharvest後削除。
- Phase A snapshot path / id: `artifacts/bs1-phase-a/A1-snapshot.md`（生成予定）
- Phase A snapshot generated at: 未生成
- Phase A snapshot hash: 未生成
- Phase B result snapshot path / id: 未到達
- Phase B result snapshot generated at: 未到達
- Phase B result snapshot hash: 未到達
- evidence bundle path / id: 未到達
- evidence bundle generated at: 未到達
- evidence bundle hash: 未到達
- C' blind bundle path / id: 未到達
- C' blind bundle generated at: 未到達
- C' blind bundle hash: 未到達

## 1. 目的と対象外

### 目的

Build backendから独立したpure C#の選択モデルを追加し、build要求とcontent candidateのtagから、
選択済みphysical contentとvalidation issueを持つimmutableな`BuildPlan`を決定できるようにする。
Unity Content Directoriesと既存Addressablesのどちらにも依存させず、後続BS2が同じplanを消費できる境界を固定する。

### 対象外

- `BuildPipeline.BuildContentDirectory`、root ScriptableObject、build出力、hash、incremental build
- runtime directory登録、asset/Scene load、取消、cleanup、`IAssetManagement`統合
- Player build、bootstrap、stripping、IL2CPP/AOT
- HTTP/LAN配信、cache、Git/SVNのcheckout・認証・同期
- 現行Addressables BuildSystemの修理、置換、削除
- 652/658 Sceneへのtag手入力、Scene/Prefab/Addressables assetの変更
- BuildTagをruntime VariantやScene roleと統合すること

### 現況

- Unityは`6000.6.0f1`。U66 Phase Dで移行を採用したが、現行Addressables Player buildは
  `BuildVariantProfile.SceneVariant`とwhitelistの既存不整合で停止する。BS1はこれを修理しない。
- 現行選択は`BuildVariantProfile.VariantWhitelist`と`VariantWhitelistBuilder`がflat string完全一致で
  GUID集合を作る。`AssetDescriptionCollector` / `IAssetDescriptionSource`はUnity/AssetReference型に結合している。
- `SceneResourceMapSource`がSceneResource graphを走査し、`AssetPayload.Variant`を選択入力にする。
- FrameworkはSeason語彙を知らない。Game → Frameworkの一方向を維持する。
- CD0 Phase DでContent Directories direct APIをbackend候補として制約付き採用した。最小fixtureでは
  build/load/unload/release/unregister/retry/relocation/Mono High stripping Playerが成立した。
- 重いTexture/Meshの一部を将来SVN管理する方針がある。path、repository、revision、`.meta`所有は未決。
  BS1はVCSを読まず、呼出側から渡されたcandidate provenanceを不透明値として保持する。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: **Unity/build backend/VCSから独立した一つの選択規則で、neutral content、複数dimension、project固有tag導出、競合、必須content欠落を再現可能な`BuildPlan`へ変換できるか。**
- 進める最低条件:
  1. pure C#入力だけから同じrequest/candidate/provider順に対して同じplanを生成する。
  2. untagged dimensionをneutral/commonとして含め、複数値選択と複数dimensionのANDを満たす。
  3. candidate列挙、tag導出、選択policy、結果snapshotの責務が分離される。
  4. duplicate/conflict、unknown/unselected dimension、required logical content欠落を構造化issueとして表現し、error時に実行可能planを返さない。
  5. Framework coreがSpring等のGame語彙、Scene role、Unity API、AssetDatabase、Addressables、Content Directories、VCS APIを知らない。
- 受け入れ条件:
  - AC1: tagなし、`Season=Spring`、`Representation=Whitebox`、両tagのtruth tableを単体テストで固定する。
  - AC2: requestの同一dimension複数値はOR、candidateがtagを持つdimension間はAND、candidateがtagを持たないrequest dimensionはneutralとして通す。
  - AC3: requestに選択がないdimensionのtagを持つcandidateはerrorではなく非選択とする。typo検出は宣言済みdimension/value schemaによるvalidation issueで行う。
  - AC4: `Representation`等のexclusive dimensionで同一candidateに複数valueが導出された場合はerror。tag重複自体はdeduplicateしdiagnosticを保持する。
  - AC5: required logical content groupに選択candidateが0件ならerror。同一logical contentに複数physical candidateが残ることを許すかはcardinality policyで明示し、exclusive groupならerrorにする。
  - AC6: `SceneRole=Lighting`等を暗黙のBuildTagへ変換しない。providerが明示的に返したtagだけを選択に使う。
  - AC7: `BuildPlan`はrequest、選択content、除外content、issues、provenanceを防御的copyで保持し、生成後の入力collection変更で変化しない。
  - AC8: provider順が結果集合を変えない。出力順はstable candidate key、dimension、valueのordinal順で決定し、report/hash入力に使える。
  - AC9:既存`VariantWhitelistBuilder`とAddressables経路は無変更で全既存テストを維持する。
- ここでは答えない問いと所有する後続スライス:
  - SceneResourceMap/AssetDatabaseからproduction candidateを組み立てるadapterとdependency closure: BS2
  - default payloadを`Representation=Full`とするproduction mapping: BS2 Phase A
  - Content Directory root、partition、build target、incremental/hash: BS2
  - runtimeでのlogical/physical variant解決とnative取消後drain: BS3
  - Player report、bootstrap、stripping、IL2CPP/AOT: BS4
  - Git/SVN checkout、revision取得、`.meta`配置、配信cache: DISTまたは専用VCS入力スライス
  - 現行Addressables whitelist不整合の修復/retirement: RET。BS2-4成立前に削除しない。
- 判定定義:
  - GO: 最低条件とACをpure unit testsで満たし、後続backendが`BuildPlan`以外の選択判断を再実装する必要がない。
  - NO-GO: Game固有語彙またはUnity I/Oをcoreに入れないと必要な選択を表現できない、あるいは一意で再現可能なplanを作れない。
- 停止規則: 最低条件を満たし、現在の問いへの致命的反証がなければGOで終了する。BS2以降の未実証事項を理由にBS1を拡張しない。
- A3後の例外承認: なし。
- 本文へ転記した実装制約:
  - Game → Framework一方向。SampleGame固有providerはFrameworkから参照しない。全体配線は`DependOnAll`へ集約する。
  - asmdef参照追加は本HANDOFFで凍結したものだけ。Editor codeをRuntime assemblyへ置かない。
  - 新規/編集Unity C#は`#nullable enable`。`record`禁止。公開ログは`ILogger<T>`だがBS1はログを持たない。
  - `SceneState`、`IAssetManagement`、`AssetOwner`、Update順序は変更しない。
  - testで`Task.Delay` / `Thread.Sleep`を使わない。
  - Phase BはUnity.exe、Unity test、`run-tests.ps1`、Addressables/Content buildを実行せず、最後に`tools/contract-audit.ps1`だけ実行する。
- 未決事項:
  - A2で、coreを既存`OneStarMaker.Editor`内のpure namespaceに置く案と、Unity参照なしの専用Editor-only asmdefへ隔離する案を比較する。
  - errorを持つ`BuildPlan`を返すか、`BuildPlanResult`に分離して成功時のみplanを公開するか。
  - schemaでunknown valueをerrorにするためのdimension/value宣言をrequest所有にするか、別policy所有にするか。

## 3. 責務マップ（A1案）

配置候補は`unity/Assets/OneStarMaker/Scripts/Editor/Build/Selection/`、namespaceは
`OneStarMaker.Editor.Build.Selection`。全型はUnity APIを参照しない。専用asmdef案はA2後にA3で決める。

| ファイル / 現在行数 / 予想増分 | 責務・変更理由 | 所有者・寿命・依存・公開面・テスト境界 |
|---|---|---|
| `BuildTag.cs` / 0 / 50–80 | ordinal比較するDimension/Value値、空白・null拒否 | plan生成中の値。Systemのみ。immutable value。pure test |
| `BuildRequest.cs` / 0 / 80–120 | dimensionごとの選択値と宣言schemaをimmutable化 | caller所有入力のsnapshot。System.Collectionsのみ。pure test |
| `BuildContentCandidate.cs` / 0 / 90–140 | stable key、logical key、physical key、required/cardinality、opaque provenanceを保持 | sourceが生成、planがcopy。Unity objectを保持しない。pure test |
| `IBuildContentSource.cs` / 0 / 20–40 | candidate列挙口 | adapter差替え境界。sourceのI/Oはinterface外実装の責任 |
| `IBuildTagProvider.cs` / 0 / 25–45 | candidate/contextからtagをdestinationへ追加 | policy chain。Game固有providerを注入可能。coreは語彙を知らない |
| `BuildTagSchema.cs` / 0 / 60–100 | dimension、許可value、exclusive性を宣言しvalidate | request単位のpolicy snapshot。pure test |
| `BuildValidationIssue.cs` / 0 / 45–75 | code/severity/candidate key/message data | plan/report間の構造化診断。ログや表示をしない |
| `BuildPlan.cs` / 0 / 70–110 | 成功時のrequest/selected/excluded/provenance immutable snapshot | build invocation所有。backendへ渡す唯一の選択結果。pure test |
| `BuildPlanResult.cs` / 0 / 40–70 | issuesと成功時planを分離し、error時plan使用を禁止 | selector戻り値。pure test |
| `BuildTagSelector.cs` / 0 / 180–260 | source収集、provider適用、normalize、validate、select、stable sort、required/cardinality検査を順序化 | stateless policy。Systemのみ。全分岐pure test |
| `BuildTagSelectorTests.cs` / 0 / 300–450 | truth table、順序、競合、欠落、immutable性 | fake source/providerのみ。AssetDatabase/Unity build不要 |

予想production増分は660–1,040行、test増分300–450行。1ファイル500行警報は発火しない見込み。
スライス全体は3責務を超えるように見えるが、source/providerは拡張境界、schema/issueは入力検証、selector/planは
同一の「選択snapshot生成」という変更理由に凝集する。Unity adapterとbackendをBS2へ分離しており、BS1内の追加分割は行数だけを理由に行わない。

## 4. 実装計画

1. A3でplacement、result/error model、schema ownerを凍結する。
2. value型とimmutable request/candidate/schema/issueを実装する。
3. source/provider interfaceを実装する。
4. selectorをnormalize → validate → match → group validation → stable snapshotの順で実装する。
5. fakeだけを使うunit testsを追加する。
6. 既存Addressables/Variantコードを変更していないことをdiffで確認する。
7. `pwsh tools/contract-audit.ps1`を実行し、Phase B結果と未実行テストを記録する。

Phase BからPhase Aへ差し戻す条件:

- UnityEngine/UnityEditor/Addressables/Content Directories型をcore modelまたはselectorへ入れる必要が出る。
- SampleGameのSeason名やScene roleをFramework coreで解釈する必要が出る。
- HANDOFF外のasmdef参照、公開runtime API、所有者、VCS I/Oが必要になる。
- error時にも実行可能planを返さないとcallerを構成できない。
- selectorが500行を超える、またはI/O・表示・永続化を同居させないと進まない。

対象外は、production adapter、既存builder接続、Unity asset変更をdiffに含めないことで維持する。

## 5. テストとレビュー計画

- 単体テスト: neutral/common、dimension内OR・dimension間AND、unknown dimension/value、duplicate、exclusive conflict、required missing、logical cardinality、Full/Whitebox、Spring/Summer、role非混入、provider順独立、stable order、防御的copy、error時plan非公開。
- 統合・Unityテスト: Phase Cで既存Editor test全件。BS1固有testはUnity I/Oなしだが、repo標準runnerから実行する。Content/Addressables/Player buildは対象外。
- 機械検査: Phase B/Cの`tools/contract-audit.ps1`、Phase Cの`tools/run-tests.ps1`、`git diff --check`。
- A0/A1主担当・モデル・ベンダー: Codex / GPT-6 Astra / OpenAI。
- A2独立レビュー:
  - architecture gate: placement/asmdef、責務、依存、所有者、test境界。
  - semantics/test gate: truth table、schema、conflict/cardinality、determinism、後続BS2-4との境界。
- A3統合担当・モデル・採否: Codex / GPT-6 Astraが統合案を作り、人間が採否を明示する。
- C'用に予約した担当・モデル・ベンダー: Phase B/C開始時に未関与の系列またはベンダーを選定する。Phase Aで使い切らない。
- 独立性の強化条件を満たせない場合の理由: 現時点なし。

## 6. Phase B 実装結果

未到達。

## 7. Phase C

未到達。

## 8. Phase C'

未到達。

## 9. Phase D

未到達。
