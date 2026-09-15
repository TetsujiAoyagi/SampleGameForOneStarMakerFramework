# BS1 — Pure content-selection core

## 0. メタデータ

- type: `slice`
- status: `Phase C/C' GO / Phase D ready`
- branch: `codex/cd0-u66-phase-d-bs1-phase-a`
- implementation base commit: `bda2ed7`
- implementation head commit: `977ee14bb4f5e704ad85e5e9b2ad5dad689bbcc0`
- risk: `high`（新しいBuildSystemの中核型、公開境界、依存方向を定める）
- owner: Phase A主担当 Codex / GPT-6 Astra（OpenAI）。A3採否は人間。B/C/C'は開始時に記録する。
- created: 2026-09-15 JST
- expires: 2026-09-29 JST、またはBuildSystem入力、SceneResource graph、VCS境界の前提が変わった時点
- harvest to: `unity/Assets/Docs/Architecture/18-asset-description.md` と新BuildSystemの現況を説明する既存Architecture文書。BS1 Phase Dでharvest後削除。
- Phase A snapshot path / id: A1はgit commit `84f316f`。A3 frozen snapshotはgit commit `633b86b`の本ファイル。
- Phase A snapshot generated at: 2026-09-15 JST
- Phase A snapshot hash: A1 `84f316f` / A3 `633b86b`
- Phase B result snapshot path / id: implementation code commits `5daadcb`, `686eeda`, `977ee14`
- Phase B result snapshot generated at: 2026-09-15 JST
- Phase B result snapshot hash: `977ee14`
- evidence bundle path / id: `TestResults/BS1-PhaseC-977ee14-pass-20260915-233148/`
- evidence bundle generated at: 2026-09-15T23:38:57+09:00
- evidence bundle hash: manifest SHA-256 `343851238a4ea474ac1be37454c41075476f892228d0494ca94da0ea3afa275d`
- C' blind bundle path / id: `TestResults/BS1-CPrime-Blind-977ee14-pass-20260915-233148/`
- C' blind bundle generated at: 2026-09-15T23:38:57+09:00
- C' blind bundle hash: blind-manifest SHA-256 `e18fa6c9c259937a4ce9888b61b77bbda413431c2a67b0180df029f295908e33`

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
  3. candidateのmaterialize、tag導出、選択policy、結果snapshotの責務が分離され、selectorはsource I/Oを呼ばない。
  4. duplicate/conflict、unknown/unselected dimension、required logical content欠落を構造化issueとして表現し、error時に実行可能planを返さない。
  5. Framework coreがSpring等のGame語彙、Scene role、Unity API、AssetDatabase、Addressables、Content Directories、VCS APIを知らない。
- 受け入れ条件:
  - AC1: tagなし、`Season=Spring`、`Representation=Whitebox`、両tagのtruth tableを単体テストで固定する。
  - AC2: requestの同一dimension複数値はOR、candidateがtagを持つdimension間はAND、candidateがtagを持たないrequest dimensionはneutralとして通す。
  - AC3: `BuildTagSchema`はrequestと独立した必須policy入力。match前にrequestと全candidate tagを検証し、unknown dimension/value、空selection、null/空/前後空白をerrorにする。比較はordinal case-sensitiveで暗黙trimしない。schema上正しいがrequestに選択がないdimensionのtagを持つcandidateは非選択とする。
  - AC4: Frameworkはdimension名による特例を持たない。全dimensionでcandidateが持てるvalueは高々1つ。同一tag重複はdeduplicateしてwarning、同一dimensionの異なるvalueはerror。requestの同一dimension複数valueだけをORとし、複数dimensionはANDで照合する。同じdimensionの複数valueへ参加するcontentは、そのdimensionを持たないneutral candidateにするかphysical candidateを分ける。同一stable candidate keyはmergeせずerror。同一physical keyを異なるlogical/stable keyが共有する場合も、将来の明示alias機構なしではerror。
  - AC5: required/cardinalityはcandidateでなく独立した`BuildContentRequirement`として宣言する。候補自体が0件の場合と全候補がfilterされた場合の両方をerrorにできる。cardinalityはidentity検証後のdistinct candidate recordを数え、少なくとも`OneOrMore`と`ExactlyOne`を持つ。重複・矛盾するrequirement宣言はerror。
  - AC6: `SceneRole=Lighting`等を暗黙のBuildTagへ変換しない。providerが明示的に返したtagだけを選択に使う。
  - AC7: `BuildPlanResult`がcanonicalなissuesと成功時planを保持し、Errorが1件でもあればplanを公開しない。Warningだけならplanを持つ。成功`BuildPlan`はrequest、選択content、structured reason付き除外content、immutable provenanceを防御的copyで保持する。
  - AC8: source/provider/candidate/tag/request入力順が結果snapshotを変えない。candidateはstable key、tagはdimension/value、issueはseverity/code/subject/dimension/value、provenanceはstable provenance keyのordinal順。display messageは将来のhash入力にしない。
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
  - GO: 最低条件とAC1–AC9をpure unit testsで満たす。
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
- 未決事項: なし。型名の軽微な表記調整は、凍結した責務・意味論・公開面を変えない範囲でPhase Bに許す。

## 3. 責務マップ

配置は`unity/Assets/OneStarMaker/Scripts/Editor/Build/Selection/`、namespaceは
`OneStarMaker.Build.Selection`。ここにEditor-onlyかつ`noEngineReferences: true`の専用
`OneStarMaker.Build.Selection.asmdef`を置く。`includePlatforms: ["Editor"]`、`autoReferenced: false`、
`noEngineReferences: true`とし、Runtime、既存Editor、Addressables、Unity packageへの参照を0にする。
BS1では`OneStarMaker.Tests.Editor`だけがselection assemblyを参照する。既存`OneStarMaker.Editor`と将来の
SampleGame Editor adapterからのproduction参照・配線はBS2 Phase Aで決め、selection側からの逆参照は禁止する。

| ファイル / 現在行数 / 予想増分 | 責務・変更理由 | 所有者・寿命・依存・公開面・テスト境界 |
|---|---|---|
| `BuildTag.cs` / 0 / 50–80 | ordinal比較するDimension/Value値、空白・null拒否 | plan生成中の値。Systemのみ。immutable value。pure test |
| `BuildRequest.cs` / 0 / 60–100 | dimensionごとの選択値をimmutable化 | caller demandのsnapshot。project policyを所有しない。pure test |
| `BuildContentCandidate.cs` / 0 / 100–150 | stable key、logical key、physical key、pure provenanceを保持 | materialize済み入力。required/cardinalityを持たず、Unity objectを保持しない。pure test |
| `IBuildTagProvider.cs` / 0 / 30–50 | candidateごとに独立immutable tag attributionを返す | 引数はpure candidateだけ。context、Unity object、delegate、mutable bufferを渡さない。過去provider出力を観測・変更できず、stable provider key必須 |
| `BuildTagSchema.cs` / 0 / 60–100 | dimensionと許可valueを宣言し、unknown/空白を検証する | Framework固有dimension名を持たないimmutable schema。pure test |
| `BuildSelectionPolicy.cs` / 0 / 60–100 | schemaとlogical group requirementを一つのimmutable policy snapshotにする | caller所有policy。requestから独立。pure test |
| `BuildContentRequirement.cs` / 0 / 40–70 | logical keyごとのmin/max（OneOrMore/ExactlyOne） | candidateが0件でもrequiredを表現。pure test |
| `BuildValidationIssue.cs` / 0 / 45–75 | code/severity/subject/dimension/value/message data | subject kindは`Request` / `Candidate` / `Requirement`、subject keyはordinal比較可能なstable string。provider由来時はstable provider keyをattributionとして持つ。ログや表示をしない |
| `BuildValidationCode.cs` / 0 / 35–60 | 凍結したissue code集合 | `InvalidRequestSelection`, `UnknownDimension`, `UnknownValue`, `DuplicateTag`, `ConflictingCandidateValues`, `DuplicateCandidateKey`, `PhysicalKeyCollision`, `DuplicateRequirement`, `ConflictingRequirement`, `RequiredGroupMissing`, `CardinalityViolation`。Bで追加・意味変更しない |
| `BuildExclusionReasonCode.cs` / 0 / 15–30 | 成功planの構造化除外理由 | `UnrequestedDimension`, `ValueNotSelected`のみ。dimension/valueを別fieldで保持し、display messageをhash入力にしない |
| `BuildProvenance.cs` / 0 / 45–75 | source kind/idとordinal string pairのimmutable snapshot | object/delegate/stream/Unity object/native handle/mutable辞書を禁止 |
| `BuildPlan.cs` / 0 / 90–140 | 成功時のrequest/selected/structured exclusion/provenance immutable snapshot | Editor build invocation寿命。backendへ渡す唯一の選択結果。pure test |
| `BuildPlanResult.cs` / 0 / 45–75 | canonical issuesと成功時planを分離し、error時plan使用を禁止 | selector戻り値。pure test |
| `BuildTagSelector.cs` / 0 / 180–280 | materialize済みcandidateへprovider適用、normalize、validate、match、group validation、stable snapshot化 | source列挙/I/Oをしないstateless policy。Systemのみ。全分岐pure test |
| `BuildTagSelectorTests.cs` / 0 / 400–600 | truth table、全identity衝突、schema matrix、順序置換、欠落、immutability、full snapshot同値 | fake providerとin-memory値のみ。AssetDatabase/Unity build不要。600行でも宣言的test matrixとして非分割妥当 |
| `OneStarMaker.Build.Selection.asmdef` / 0 / 15–25 | pure境界を機械的に強制 | Editor only、autoReferenced false、noEngineReferences、外部参照0 |
| `OneStarMaker.Tests.Editor.asmdef` / 既存 / +1参照 | BS1 testからselection assemblyを参照 | selectionへの片方向追加。production assemblyへの逆参照なし |

予想production増分は900–1,415行、test増分400–600行。test fileだけ500行警報の可能性があるが、
同じselector契約の宣言的table matrixで依存・寿命・変更理由が同じため非分割を既定とする。
スライス全体は3責務を超えるように見えるが、materialized candidate/providerは入力・拡張境界、schema/issueは入力検証、selector/planは
同一の「選択snapshot生成」という変更理由に凝集する。Unity adapterとbackendをBS2へ分離しており、BS1内の追加分割は行数だけを理由に行わない。

## 4. 実装計画

1. 凍結済みplacement、result/error model、schema/requirement ownerに従って専用assemblyを作る。
2. value型とimmutable request/candidate/policy/requirement/issue/provenanceを実装する。
3. `IBuildTagProvider`だけを公開拡張口として実装する。`IBuildContentSource`は追加しない。
4. selectorをtag attribution → normalize → identity/schema validation → match → group validation → stable snapshotの順で実装する。source列挙は行わない。
5. fakeだけを使うunit testsを追加する。
6. 既存Addressables/Variantコードを変更していないことをdiffで確認する。
7. `pwsh tools/contract-audit.ps1`を実行し、Phase B結果と未実行テストを記録する。

Phase BからPhase Aへ差し戻す条件:

- UnityEngine/UnityEditor/Addressables/Content Directories型をcore modelまたはselectorへ入れる必要が出る。
- SampleGameのSeason名やScene roleをFramework coreで解釈する必要が出る。
- HANDOFF外のasmdef参照、公開runtime API、所有者、VCS I/Oが必要になる。
- error時にも実行可能planを返さないとcallerを構成できない。
- selectorが500行を超える、またはI/O・表示・永続化を同居させないと進まない。
- `IBuildContentSource`やAssetDatabase materializationがBS1に必要になる。

対象外は、production adapter、既存builder接続、Unity asset変更をdiffに含めないことで維持する。

## 5. テストとレビュー計画

- 単体テスト: neutral/common、dimension内OR・dimension間AND、空selection、request/candidate双方のunknown dimension/value、全stable/logical/physical identity衝突、candidate同一dimension異値conflict、同一tag重複、required group自体なし/全filter、ExactlyOne/OneOrMoreの0/1/2、Full/Whitebox、Spring/Summer、role非混入、複数errorのcanonical順、全入力順置換のfull snapshot同値、nested collection/provider buffer変更後の防御的copy、warning時plan有/error時plan無。
- 統合・Unityテスト: Phase Cで既存Editor test全件。BS1固有testはUnity I/Oなしだが、repo標準runnerから実行する。Content/Addressables/Player buildは対象外。
- 機械検査: Phase B/Cの`tools/contract-audit.ps1`、Phase Cの`tools/run-tests.ps1`、`git diff --check`。
- A0/A1主担当・モデル・ベンダー: Codex / GPT-6 Astra / OpenAI。
- A2独立レビュー:
  - architecture gate: placement/asmdef、責務、依存、所有者、test境界。
  - semantics/test gate: truth table、schema、conflict/cardinality、determinism、後続BS2-4との境界。
- A3統合担当・モデル・採否: Codex / GPT-6 Astraが統合。2026-09-15、人間がA2統合案を採用し、追加の凍結補記を示してA3凍結を承認。
- C'用に予約した担当・モデル・ベンダー: Phase B/C開始時に未関与の系列またはベンダーを選定する。Phase Aで使い切らない。
- 独立性の強化条件を満たせない場合の理由: A2 architecture/semanticsはいずれもOpenAI/GPT系列で、A0だけから代替構成を出す別系列レビューは未実施。人間が独立にHANDOFFと現行実装を照合して補記を提示した。C'用の未関与系列/ベンダーは予約したままとする。

### A2独立レビュー結果とA3統合案

- architecture gate: `/root/bs1_a2_architecture`、Codex / GPT-6 Astra / OpenAI。A1や実装に未関与。他レビュー所見を未閲覧。
  - 採用: selectorからsource列挙を除外し、materializeはBS2へ送る。
  - 採用: required/cardinalityをcandidateから独立policyへ移す。
  - 採用: providerの共有`ICollection`を廃止し、stable provider key付き独立attributionにする。
  - 採用: provenanceをpure immutable DTOに限定する。
  - 採用:専用Editor-only/no-engine asmdefで境界を機械化する。
- semantics/test gate: `/root/bs1_a2_semantics`、Codex / GPT-6 Astra / OpenAI。A1や実装に未関与。他レビュー所見を未閲覧。
  - 採用: logical group requirementを独立宣言し、候補0件も検出する。
  - 採用: stable/logical/physical key衝突、cardinality計数単位を固定する。
  - 採用: schemaをrequest外の必須policyとし、match前に全入力を厳格検証する。
  - 採用: plan、issue、provenance、excluded reasonを含むfull snapshotのcanonical順を保証する。
  - 採用: `BuildPlanResult`にissueを置き、Error時はplanを公開しない。
- 不採用: なし。
- 保留: `IBuildContentSource`型そのもの。BS1では不要なので追加せず、BS2 Phase Aでproduction adapterの入力portとして必要性と配置を決める。
- 人間レビューで追加採用した凍結補記:
  - `BuildTagSchema.cs`を独立ファイルとして責務マップへ追加。
  - dimension名によるexclusive特例を廃止し、全candidateをdimensionあたり高々1 valueに統一。
  - BS1の公開portは`IBuildTagProvider`だけとし、source portをBS2へ保留。
  - asmdefを`autoReferenced: false`とし、BS1の参照追加はTests側だけに限定。
  - provider入力をpure candidateだけに限定し、未定義contextを削除。
  - issue codeとexclusion reasonの閉じた集合を凍結。
  - GOからBS2でしか証明できない「後続が再実装しない」を削除。
  - 責務マップ見出しから「A1案」を削除。
- A3判断: 全A2指摘と人間補記を採用。不採用なし。`IBuildContentSource`はBS2へ保留。
- A3 frozen snapshot: git commit `633b86b`。以後、意味論・責務・公開面を変える場合はPhase Aを新revisionで再開する。

## 6. Phase B 実装結果

- 実装内容:
  - `unity/Assets/OneStarMaker/Scripts/Editor/Build/Selection/` に、Editor-only、
    `autoReferenced: false`、`noEngineReferences: true`、外部参照0の
    `OneStarMaker.Build.Selection` assemblyを追加した。
  - pureなrequest / candidate / provenance / schema / requirement / issue / plan model、
    公開拡張口`IBuildTagProvider`、statelessな`BuildTagSelector`を追加した。
  - neutral選択、dimension内OR・dimension間AND、schema検証、tag競合とidentity衝突、
    required/cardinality、canonical ordering、防御的copy、Error時plan非公開を実装した。
  - `OneStarMaker.Tests.Editor`だけにselection assembly参照を追加し、in-memory fake providerだけを使う
    `BuildTagSelectorTests`を追加した。既存`VariantWhitelistBuilder`とAddressables経路は変更していない。
- HANDOFFとの差: なし。production adapter、`OneStarMaker.Editor`からの参照、
  `IBuildContentSource`、Unity/AssetDatabase materializationは追加していない。
- 機械検査: `pwsh tools/contract-audit.ps1` exit 0（機械で判定できる契約に違反なし）。
  `git diff --check`も指摘なし。
- 未実行事項: Phase B契約に従い、Unity.exe起動、Unity Editor接続、`tools/run-tests.ps1`、
  Unity tests、Addressables build、Content build、Player buildは未実行。テスト実行とコンパイル確認はPhase Cへ送る。
- implementation head commit: `b4b43d3`
- Phase B担当・モデル・ベンダー: Codex / GPT-5 / OpenAI。

### Phase B final implementation snapshot

- 空requestを`InvalidRequestSelection` errorにし、candidate tagのnull/空/前後空白matrixを含めた。
- warning・exclusion・全provenance・複数errorを含むfull snapshot permutation testsを含めた。
- implementation code commit: `5daadcb`
- `pwsh tools/contract-audit.ps1` exit 0。Unity testsはPhase C責任のため未実行。
- NUnitのenum assertion overloadを修正したimplementation code commit: `686eeda`。
  開いているUnity Editorのrecompile結果は`completed / failed:false / errors:[]`。
- candidate tag list内のnull要素を、凍結済み`UnknownDimension` code、Candidate subject、
  provider attributionを持つ構造化errorへ変換した。ordinal case-sensitive比較と
  SceneRole非推論をpure testsで固定したimplementation code commit: `977ee14`。
- final implementation head: `977ee14bb4f5e704ad85e5e9b2ad5dad689bbcc0`。

## 7. Phase C

- evidence id: `BS1-PhaseC-977ee14-pass-20260915-233148`、manifest SHA-256
  `343851238a4ea474ac1be37454c41075476f892228d0494ca94da0ea3afa275d`。
- 判定: **GO**。現在の問いを阻害する欠陥は0件。最低条件1–5とAC1–AC9を満たす。
- 構造適合:
  - pure selection coreは専用Editor-only assemblyにあり、`autoReferenced: false`、
    `noEngineReferences: true`、外部参照0。参照追加は`OneStarMaker.Tests.Editor`だけ。
  - materialized candidate、provider、policy、selector、planの責務が分離され、selectorはI/Oを持たない。
  - Unity/Game固有語彙、Unity API、Addressables、Content Directories、VCS、production adapterはcoreにない。
  - 既存`VariantWhitelistBuilder`とAddressables経路は無変更。
- 機械検査: `pwsh tools/contract-audit.ps1` exit 0、`git diff --check` exit 0。
- Unity test: `pwsh tools/run-tests.ps1` exit 0。Unity 6000.6.0f1 EditModeは
  `726 total / 726 passed / 0 failed / 0 skipped`。BS1固有47件を含む。
- 後続スライスへの入力:
  - null短絡後の`Trim()`に対するCS8602 warningが4箇所ある。静的確認上null逆参照はなく、
    `#nullable enable`契約にも適合するためBS1 blockerではない。BS2の保守入力候補。
  - Burst compiler serverのnamed-pipe起動失敗が生ログに残るが、Tundra compile、完全XML、
    726件、runner exit 0は成立した。Burst依存検証前のtest-infra入力。
- 未確認事項: 対象外のproduction adapter、Content/Addressables/Player build、runtime、VCS。
- 担当・モデル・ベンダー: `/root/bs1_phase_c_977ee14` / Codex / GPT-6 Astra / OpenAI。
  Phase Bと異なる割当モデル、新規セッションで実施。

## 8. Phase C'

- blind bundle: `BS1-CPrime-Blind-977ee14-pass-20260915-233148`、blind-manifest SHA-256
  `e18fa6c9c259937a4ce9888b61b77bbda413431c2a67b0180df029f295908e33`。
- 判定: **GO**。現在の問いを阻害する欠陥は0件。
- 監査結果: bundle内全hash、固定base/head、完全diff、凍結Phase A、Phase B結果、
  contract audit、生Unity log/XMLを確認。pure selection意味論、canonical ordering、防御的copy、
  Error時plan非公開、asmdef境界、既存経路無変更に反例なし。
- 後続スライスへの入力・残存リスク:
  - providerのnull collection返却・例外送出は構造化resultでなく例外脱出する。凍結契約外のため、
    BS2 Phase Aでproduction providerの失敗契約を明示する。
  - provider key不正、schema空value集合、未定義enum値のhardening要否はBS2以降で判断する。
  - contract auditの表示基点`origin/develop`とreview implementation baseは目的が異なるため、
    証拠上の基点追跡を将来のtest-infra改善候補とする。
- 監査できなかった範囲: 対象外のproduction adapter、AssetDatabase/SceneResource materialization、
  Content Directory、runtime解決、Player build。
- 独立性: Phase C所見と旧headレビューを含まないblind bundleだけを使用。実装未関与の新規セッション。
  担当・モデル・ベンダーは`/root/bs1_cprime_977ee14` / Codex / GPT-5.6 Luna / OpenAI。
  Phase BのGPT-5、Phase CのGPT-6 Astraと異なる割当モデルを使用し、AI C'最低条件を満たす。

## 9. Phase D

未実施
