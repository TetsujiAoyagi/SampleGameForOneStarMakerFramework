# BS2a — Production content materialization

## 0. メタデータ

- type: `slice`
- status: `Phase A1 draft`（A2独立レビューと人間によるA3凍結は未実施）
- branch: `codex/bs2a-production-materialization`
- implementation base commit: `af4f9686a1097bda27ccb7593289b08d0f4d81e3`
- implementation head commit:
- risk: `high`（既存Unity asset graphを新BuildSystemのpure selection境界へ接続し、新しいproduction assembly依存を追加する）
- owner: Phase A主担当 Codex / GPT-5（OpenAI）。A3採否は人間。B/C/C'は開始時に記録する。
- created: 2026-09-16 JST
- expires: 2026-09-30 JST、または`AssetPayload`、`SceneResourceMap`、BS1 selection API、Content Directories採用判断の前提が変わった時点
- harvest to: `unity/Assets/Docs/Architecture/18-asset-description.md`。BS2a Phase Dで現況をharvest後、本HANDOFFを削除する。
- Phase A snapshot path / id: A1はgit commit `9f1d487ca83ba630ee78cb6cd1dbf3226e98f323`の本ファイル。A3 frozen snapshotはA2/A3後に記録する。
- Phase A snapshot generated at: 2026-09-16 JST
- Phase A snapshot hash: A1 `9f1d487`
- Phase B result snapshot path / id:
- Phase B result snapshot generated at:
- Phase B result snapshot hash:
- evidence bundle path / id:
- evidence bundle generated at:
- evidence bundle hash:
- C' blind bundle path / id:
- C' blind bundle generated at:
- C' blind bundle hash:

## 1. 目的と対象外

### 目的

`SceneResourceMap`と`AssetPayload`から、BS1の`BuildContentCandidate`、`IBuildTagProvider`、
`BuildContentRequirement`をproduction入力として再現可能に組み立てるEditor adapterを追加する。
選択対象rootのAssetDatabase identityと依存閉包を検証し、成功時だけBS1 selectorへ渡せるimmutableな
materialization snapshotを生成する。空のpayload variantは明示的に`Representation=Full`へ写像する。

旧BS2はproduction adapterとContent Directories build orchestrationを同じスライスとしていたが、
両者は変更理由、依存、所有する状態、テスト方法が異なる。BS2aは入力materializationまで、BS2bは
成功した`BuildPlan`をContent Directory root/partition/outputへ変換する責務として分ける。

### 対象外

- `BuildPipeline.BuildContentDirectory`呼出し、root ScriptableObject生成、partition、output path、build name、incremental/hash
- runtime directory登録、logical/physical variant解決、load/unload/release/unregister、取消後drain
- Player build、bootstrap、stripping、IL2CPP/AOT、配信、cache
- Git/SVN checkout、repository/revision解決、`.meta`所有または生成
- 現行`VariantWhitelistBuilder`、Addressables build、`BuildVariantProfile`の修理・置換・削除
- `AssetPayload`、`SceneResource`、`SceneResourceMap`のserialized shape変更
- Scene/Prefab/Addressables assetの変更または再生成
- SampleGame固有のSeason、職種、Scene roleをFrameworkで解釈すること

### 現況（A0）

- BS1は`OneStarMaker.Build.Selection`にUnity非依存のselection coreを実装済み。47件の固有テストを含む
  EditMode全726件がPhase Cで通り、2026-09-16に`develop`へマージされた。
- BS1のproduction portは`IBuildTagProvider`だけで、source/materializer portはBS2へ保留された。
- 現行`AssetDescriptionCollector` / `SceneResourceMapSource`は`BuildVariantProfile`から
  `IAssetPayloadProvider`を列挙するが、provider object identityで重複排除し、文字列messageで失敗を表す旧Addressables経路である。
- `AssetPayload`はAddressables `AssetReference`とflatな`Variant`を持つ。空variantは現行defaultで、BS1時に
  production mappingを`Representation=Full`とする判断をBS2 Phase Aへ送った。
- `AssetDependencyClosure.Compute`はGUIDからAssetDatabase依存を再帰展開するが、返却順、欠損表現、
  immutable snapshot、materialization resultとの統合は新BuildSystem契約になっていない。
- Unity 6.6 Content Directories direct APIはCD0でbackend候補として限定実証済みだが、BS2aはAPIを呼ばない。
- 現行Addressables Player buildは`BuildVariantProfile.SceneVariant`とwhitelistの既存不整合があり、
  修復または後続BuildSystemへの置換までPlayer build可を前提にしない。
- FrameworkはSeason等のGame語彙を知らず、Game → Frameworkの依存方向を維持する。

### A0の未決事項

1. materialization failureを、BS1の閉じた`BuildValidationCode`へ追加せず別result型で表す具体的なcode集合。
2. `IBuildContentSource`を公開portとして追加するか、BS2aのsourceを`SceneResourceMap`専用のinternal adapterに留めるか。
3. stable/logical keyを`SceneResource.Identity`、physical keyをGUIDとした際の、同一logical identity内の複数payload表現。
4. 依存閉包をcandidateごとに保持するか、selected rootからBS2bが再計算するか。再現性と重複I/O回避のため、A1では
   materialization snapshotにroot GUIDごとのcanonical closureを保持する案を採る。
5. non-empty legacy variantをそのまま`Representation=<Variant>`へ写像できるか。A1では写像するが、schemaに未知値なら
   BS1 validation errorとする。Season/roleへの推測変換はしない。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: **既存serialized assetを変更せず、SceneResource graphとAssetDatabaseだけから、
  BS1へ渡せる決定的なproduction candidate/tag/requirement/dependency snapshotを構築できるか。**
- 進める最低条件:
  1. 同じ`SceneResourceMap`とAssetDatabase状態から、列挙順に依存しない同一snapshotを生成する。
  2. 各payloadをstable logical identity、GUID physical identity、明示的なRepresentation tagへ変換し、空variantをFullへ写像する。
  3. 必須SceneResourceごとに`ExactlyOne` requirementを生成し、materialization errorがあればselectorを実行可能な入力を公開しない。
  4. root GUIDと依存閉包の欠損・衝突・不正を構造化issueとして返し、Unity/AssetDatabase I/Oとpure mapping policyを分離する。
  5. selection coreはUnity/Addressables/AssetDatabase/Content Directoriesを参照せず、FrameworkはGame固有語彙を解釈しない。
- 受け入れ条件:
  - AC1: `SceneResource.Identity`をlogical key、payload indexに依存しない`logical key + Representation value + GUID`をstable key、
    `AssetReference.AssetGUID`をphysical keyとしてcanonical candidateを作る。キー連結のescaping規則はA2で固定する。
  - AC2: payload variantが空なら`Representation=Full`、非空ならordinal case-sensitiveで
    `Representation=<Variant>`を返す。trim、case fold、Season/SceneRole推測はしない。
  - AC3: 各SceneResourceの必須descriptionに`ExactlyOne` requirementを1件生成する。optional概念を新規推測せず、
    現行SceneResource graph外のadditional sourceは対象外にする。
  - AC4: null resource、null description、null payload、空/不正GUID、GUID解決不能、root asset欠損、dependency欠損、
    logical/stable/physical key衝突を構造化materialization issueにする。Errorが1件でもあれば成功snapshotを公開しない。
  - AC5: AssetDatabase accessは注入可能な狭いgatewayに隔離し、key/tag/requirement/provenance/orderingとclosure正規化は
    Unityなしの単体テストで検証できる。
  - AC6: provenanceは少なくともsource kind、SceneResource identity、root GUID、asset pathをimmutableなordinal snapshotで保持する。
    display messageはidentity/hash入力に使わない。
  - AC7: root GUIDごとのclosureはroot自身を含み、`Assets/`配下のcontentだけをcanonical path/GUID順で保持する。
    Packages、script/asmdef/dll、Unity built-in resourceは除外し、同じ依存が複数rootに現れてもroot別snapshotは決定的である。
  - AC8: materializerのsource/resource/payload/dependency列挙順を置換しても、candidate、tag attribution、requirement、issue、closure、
    provenanceを含むfull snapshotが同一になる。
  - AC9: materialization成功結果を既存BS1 selectorへ渡し、Full/Whitebox選択、neutral候補、ExactlyOne違反を既存contractのまま検出できる。
  - AC10: 既存Addressables/Variant経路、Runtime serialized型、SampleGame assetを変更せず、既存Editor testを維持する。
- ここでは答えない問いと所有する後続スライス:
  - Content Directory root asset、partition、active target/subtarget、output、build name、incremental/hash、report: `BS2b_CONTENT_DIRECTORY_BUILD`
  - runtime logical/physical variant解決、directory handleのowner、native取消後drain: `BS3_RUNTIME_CONTENT_DIRECTORY`
  - Player report、bootstrap、stripping、IL2CPP/AOT: `BS4_PLAYER_INTEGRATION`
  - Git/SVN checkout、revision、`.meta`配置、配信cache: `DIST`または専用VCS入力スライス
  - 現行Addressables whitelist不整合の修復/retirement: `RET`。BS2b〜BS4成立前に削除しない。
  - SceneResource以外のproduction source、optional content、alias/shared physical assetの明示契約: 専用拡張スライス
- 判定定義:
  - GO: 最低条件とAC1〜AC10を満たし、成功snapshotを既存BS1 selectorへ接続できる。
  - NO-GO: serialized asset変更、Game固有語彙、Content Directories API、またはselection coreへのUnity依存を入れなければ
    production入力を一意に構築できない。
- 停止規則: 最低条件を満たし、現在の問いへの致命的反証がなければGOで終了する。BS2b以降のbuild/runtime成立を理由に拡張しない。
- A3 後の例外承認: なし。
- 本文へ転記した実装制約:
  - Game → Frameworkの一方向を維持し、全体配線は`DependOnAll`へ集約する。asmdef参照追加はA3で凍結したものだけ。
  - Editor codeをRuntime assemblyへ置かない。新規/編集Unity C#は`#nullable enable`、`record`禁止。
  - 破棄されうる`UnityEngine.Object`のnull判定は`== null` / `!= null`を使い、`?.` / `??` / `is null` / `ReferenceEquals`を使わない。
  - 公開APIのログ抽象は`ILogger<T>`。BS2aはログを持たず構造化resultを返す。
  - `SceneState`、`IAssetManagement`、`AssetOwner`、Update順序を変更しない。
  - testで`Task.Delay` / `Thread.Sleep`を使わない。
  - Phase BはUnity.exe、Unity test、`run-tests.ps1`、Addressables/Content buildを実行しない。最後に`pwsh tools/contract-audit.ps1`だけを実行する。
- 未決事項: A0の5項目。A2で閉じ、人間がA3で採否するまでPhase Bへ進まない。

## 3. 責務マップ（A1案）

production adapterは`unity/Assets/OneStarMaker/Scripts/Editor/Build/Materialization/`、namespaceは
`OneStarMaker.Editor.Build.Materialization`へ置く。新しいEditor-only assembly
`OneStarMaker.Build.Materialization`は`OneStarMaker.Build.Selection`、`OneStarMaker.Runtime`、Unity Editor/Engineと
Addressables runtime型を参照する。selection assemblyからの逆参照は禁止する。既存`OneStarMaker.Editor`へ混在させず、
BS2bがmaterialization結果だけを参照できる依存境界にする。

| ファイル / 現在行数 / 予想増分 | 責務・変更理由 | 所有者・寿命・依存・公開面・テスト境界 |
|---|---|---|
| `Materialization/Model/BuildMaterializationIssue.cs` / 0 / 60–90 | source/asset/dependency failureのseverity、code、subject、stable fields | build invocation寿命のimmutable value。Systemのみ。表示・ログなし。pure test |
| `Materialization/Model/BuildDependencySnapshot.cs` / 0 / 60–90 | root GUID/pathとcanonical dependency GUID/pathのsnapshot | invocation寿命。AssetDatabase objectを保持しない。pure ordering/immutability test |
| `Materialization/Model/BuildMaterializationSnapshot.cs` / 0 / 80–120 | candidates、tag provider、requirements、closures、provenanceの成功snapshot | BS1 selectorとBS2bへ渡す公開境界。防御的copy。Unity objectを保持しない。pure test |
| `Materialization/Model/BuildMaterializationResult.cs` / 0 / 40–70 | issuesと成功snapshotを分離し、error時snapshotを非公開にする | invocation寿命。BS1 `BuildPlanResult`と混同しない。pure test |
| `Materialization/IAssetDatabaseGateway.cs` / 0 / 30–50 | GUID/path/recursive dependency取得と存在確認の狭いport | production実装はEditor I/O、fakeはpure test。Unity objectを返さない |
| `Materialization/UnityAssetDatabaseGateway.cs` / 0 / 70–110 | AssetDatabase/File I/Oをportへ適合 | stateless Editor infrastructure。internal。Editor integration test |
| `Materialization/SceneResourceContentMaterializer.cs` / 0 / 220–320 | SceneResource graphを走査し、mapping policyとgatewayを使って成功snapshotまたはissuesを生成 | orchestrationのみ。SceneResourceMapは借用し保持しない。Editor testとfake gateway test |
| `Materialization/ScenePayloadMappingPolicy.cs` / 0 / 100–150 | identity、Full mapping、tag、requirement、provenanceを決定 | pure policy。BS1型とplain inputだけへ依存。Unity/AssetDatabaseなしで単体テスト |
| `Materialization/AssetDependencySnapshotBuilder.cs` / 0 / 120–180 | gateway結果をcontent filter、欠損検出、canonical closureへ変換 | mappingとI/Oを分離。gateway portのみ。pure fake test |
| `Materialization/OneStarMaker.Build.Materialization.asmdef` / 0 / 20–30 | production adapterの依存方向を機械化 | Editor only、autoReferenced false。selection/runtime/Editor APIへの片方向参照 |
| `Tests/Editor/Build/SceneResourceContentMaterializerTests.cs` / 0 / 350–500 | null/error matrix、identity collision、順序置換、BS1接続 | ScriptableObject fixture＋fake gateway。500行到達時も同一orchestration contractなら非分割、変更理由が分かれればpolicy testを分ける |
| `Tests/Editor/Build/ScenePayloadMappingPolicyTests.cs` / 0 / 180–260 | Full/legacy variant mapping、stable key、requirement、provenance | Unityなしのpure test |
| `Tests/Editor/Build/AssetDependencySnapshotBuilderTests.cs` / 0 / 180–280 | filter、missing、canonical ordering、重複 | fake gatewayによるpure test |
| `OneStarMaker.Tests.Editor.asmdef` / 既存 / +1参照 | materialization assemblyのtest参照 | productionへの片方向追加。既存参照は維持 |

予想production増分800〜1,210行、test増分710〜1,040行。orchestration、pure mapping、Editor I/O、dependency normalizationは
依存とテスト方法が異なるため分割する。`SceneResourceContentMaterializer`が500行へ達する、またはmapping/closure policyを内包する場合は
Phase Aへ戻し、行数だけで委譲classを増やさない。

既存`AssetDependencyClosure`は旧checkout/Addressables経路のpublic utilityであり、BS2aのcanonical snapshot/error contractと責務が異なる。
A1既定では変更せず、新materialization側からも直接呼ばない。共通化はA2で意味論一致を証明できた場合だけ採用する。

## 4. 実装計画

1. A2 architecture gateでassembly配置、source port要否、公開面、既存`AssetDependencyClosure`との境界をレビューする。
2. A2 semantics/test gateでstable key encoding、issue code、null/error matrix、Full mapping、closure再現性をレビューする。
3. 人間がA2 findingsを採否し、未決事項と責務マップをA3として凍結する。
4. 凍結後、materialization result/snapshot/issueとAssetDatabase gatewayを実装する。
5. pure payload mappingとdependency snapshot builderを実装し、fake gatewayの単体テストを先に固定する。
6. SceneResource materializerを実装し、成功snapshotを既存`BuildTagSelector`へ接続するテストを追加する。
7. 既存Addressables/Variant/Runtime assetとserialized assetが無変更であることをdiffで確認する。
8. Phase Bは`pwsh tools/contract-audit.ps1`を実行し、Unity tests/build未実行を記録する。

Phase BからPhase Aへ差し戻す条件:

- selection coreまたはRuntime serialized型へUnity/Editor都合の変更が必要になる。
- SampleGame語彙やScene roleをFramework mappingで解釈する必要が出る。
- A3にないasmdef edge、公開runtime API、mutable/global state、root asset生成、Content Directories APIが必要になる。
- issue時に部分snapshotをbackendへ渡さなければcallerを構成できない。
- stable identityがpayload index、AssetDatabase列挙順、display messageに依存する。
- 計画した配置ではmapping/closureの中核ロジックをUnityなしで単体テストできない。
- materializerが500行を超える、またはpolicy、I/O、build orchestrationを同居させないと進まない。

対象外は、既存build経路、Runtime型、asset、BS2b以降のAPIをdiffへ含めないことで維持する。

## 5. テストとレビュー計画

- 単体テスト:
  - empty → Full、non-empty variantのordinal mapping、trim/case foldなし、Season/role非推測
  - null resource/description/payload、空/不正/未解決GUID、missing root/dependency、全identity衝突
  - Packages/script/asmdef/dll/built-in除外、root包含、dependency重複、canonical ordering
  - resource/payload/dependency順の全置換でfull snapshot同値
  - gateway/provider buffer変更後の防御的copy、error時snapshot無、warningだけならsnapshot有
  - 既存BS1 selectorとのFull/Whitebox/ExactlyOne統合
- 統合・Unityテスト: Phase CでBS2a固有Editor testsと既存Editor tests全件。Content Directories、Addressables、Player buildは対象外。
- 機械検査: Phase B/Cの`pwsh tools/contract-audit.ps1`、Phase Cの`pwsh tools/run-tests.ps1`、`git diff --check`、asmdef依存確認。
- A0/A1主担当・モデル・ベンダー: Codex / GPT-5 / OpenAI。
- A2独立レビューごとの観点・担当・モデル・ベンダー:
  - architecture gate: placement/asmdef、責務、依存、所有者、source port、公開面、BS2b境界。未実施。
  - semantics/test gate: stable identity、Full mapping、issue model、closure/filter、determinism、BS1接続。未実施。
  - 高リスク代替案: A0だけから「BS2a/BS2b分割」「既存collector拡張」「別adapter assembly」を比較。未実施。
- A3統合担当・モデル・採否: Codex / GPT-5が統合案を作り、人間が採否・凍結する。未実施。
- C'用に予約した担当・モデル・ベンダー: Phase B/C開始時に未関与の系列またはベンダーを選び、Phase Aで使い切らない。
- 独立性の強化条件を満たせない場合の理由: 現時点ではA1のみ。A2/C'担当確定時に記録する。

## 6. Phase B 実装結果

## 7. Phase C

## 8. Phase C'

## 9. Phase D
