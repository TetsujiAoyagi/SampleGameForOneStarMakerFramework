# BuildSystem 単独の入力の入口（`BUILDSYSTEM_STANDALONE_ENTRY`）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。

- type: `slice`
- status: `A`（**A1 初稿。A2 未実施、A3 未凍結。実装キューではない**）
- 位置付け: program [ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md) §4.3 の X6 と、§6 D4 の順序 (2)。X7（出力先と target の固定）は本書 §2 の分割の判断で別の小スライスへ分けた
- branch: `claude/fervent-ritchie-zhhe6k`（`origin/develop` の `9e7d4148` から切った計画ブランチ。PR の base は `develop`）
- implementation base commit: `9e7d4148`（2026-11-26 の `origin/develop` の head。§1 の現況はこの commit で測り直した）
- implementation head commit: 未固定
- A3 凍結記録: 未凍結
- risk: `high`（公開 API を増やす。[Phase A の内部フロー](../../.agents/skills/osm-workflow/references/phases-and-handoff.md#phase-a-の内部フロー) の高リスクの例に当たる）
- owner: root（依頼者）
- created: 2026-11-26
- expires: 2026-12-31、または置換 revision。親の program の期限（2026-11-30）より後なので、§2 未決事項 U4 で owner に確認する
- harvest to: `unity/Assets/Docs/Architecture/18-asset-description.md`（build の入力の入口の節。SceneResourceMap からの入口と並べて、一般の入口の契約を書く）と、program §2.2 X6・§2.3 の U-B の行の状態
- Phase A snapshot の取得案内・状態: 未生成（A3 で凍結する時に作る。A1 の時点で Git 外の補助 file は無い）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割と作業: A2（独立レビュー）。アーキテクチャゲート 1 件、目的レビュー 1 件、高リスクなので A0（§1 の目的・利用者・対象外・現況と §2 の未決事項）だけを読んで代替構成を出す担当 1 件。その後 A3 で owner が未決事項 U2〜U4 を裁定して凍結する（U1 は 2026-11-26 に裁定済み）。
  - 次の担当が読む範囲: 既定どおり全節。
  - A2 の入力版: この票を含む commit を Git 配布の例外 commit として使う（本書は Cloud で書いた。[Phase A の commit と snapshot](../../.agents/skills/osm-workflow/references/phases-and-handoff.md#phase-a-の-commit-と-snapshot)）。固定 base は `9e7d4148`。本書の sha256 は A2 を起動する担当がその commit の file から取る。
  - 必要な権限: A2 は無し（文書と code の読み取りだけ）。P 検査の再実施と Unity のテストは Phase C で、Unity Editor のある環境が要る。
  - 停止規則: A3 の凍結の前に Phase B へ入らない。`RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md` の A1 は本スライスの Phase D まで始めない（program §0）。
  - 引き渡さないもの: 目的レビューには §1 の「目的」と「利用者」の原文だけを渡し、本書の他の部分を渡さない。

## 1. 目的と対象外

- 目的: BuildSystem を使う開発者が、OSM の SceneResourceMap を用意せず、OSM の内部向けの抜け道（`InternalsVisibleTo`）にも頼らずに、public な API だけで、自分で選んだ asset を Content Directory として build する入力を組み立てて build し、その生成物を読める。既存の SceneResourceMap からの build の結果は変えない。
- 利用者: BuildSystem を自分の Unity プロジェクトの build script から呼ぶ開発者。OSM の SceneResourceMap と Scene の仕組みを使っていない。
- 対象外:
  - 出力先と target の固定を解くこと（X7）。本スライスの build は、今の coordinator が許す root の配下にだけ出す。別の小スライスへ分けた（§2）。
  - 実行時の検証済みの索引（`ContentDirectoryIndex`）と install 済み revision の検証器（`InstalledRevisionVerifier`）を public にすること。測り直しで、今の public な API だけで読み・検証の経路が辿れると分かったため（下の現況）。要否は §2 未決事項 U2。
  - asmdef の分割、asmdef 参照の追加（production）、`InternalsVisibleTo` の削除、SampleGame の合成（`Bs4FixtureComposer`、`SampleGameBootstrapContentComposer`）を新しい入口へ移すこと。
  - 欠けを許す materialize（上書きの build 用）の一般の入口。Addressables 経路の Variant Build。
  - 別の Unity プロジェクトでの実証（program §5.2 段階 4）。
- 現況（base `9e7d4148` で測った。path は `unity/Assets/OneStarMaker/Scripts/` からの相対）:
  - **入力の入口は program の実測のまま閉じている。** `Editor/Build/Materialization/BuildMaterializationSnapshot.cs` のコンストラクタは 3 つとも internal（21・28・37 行。3 つ目は欠けの記録を受ける版で、program の base の後に増えた）。public な型のうち snapshot を返すのは `SceneResourceContentMaterializer` の `Materialize(SceneResourceMap)` と `MaterializeAllowingAbsentSources(SceneResourceMap)` だけ。tag の provider の識別子は同 file 77 行で `osm.scene-resource-map.representation.v1` の固定値。
  - **`InternalsVisibleTo`。** `Materialization/AssemblyInfo.cs` は `OneStarMaker.Tests.Editor`、`SampleGame.DependOnAll.Editor`、`SampleGame.Tests.Editor` の 3 つに開く（program の時点から `SampleGame.Tests.Editor` が増えた）。`Content/AssemblyInfo.cs` は test の 2 つ、`Selection/AssemblyInfo.cs` は同じ 3 つ。
  - **internal なコンストラクタの呼び出し元。** `new BuildMaterializationSnapshot(` は、Framework の production が 1 file（`SceneResourceContentMaterializer.cs`）、SampleGame の production が 2 file（`Bs4FixtureComposer.cs`、`SampleGameBootstrapContentComposer.cs`）、Framework の test が 11 file・19 箇所、SampleGame の test が 9 file・13 箇所。SampleGame の合成は internal な `new BuildPlan(...)` と `AssetDependencySnapshotBuilder` も直接使う。
  - **snapshot 以外の入力は既に public。** 候補（`BuildContentCandidate`）、由来（`BuildProvenance`）、tag（`BuildTag`）、requirement（`BuildContentRequirement`）、graph（`BuildGraphNode`）、依存の閉包（`BuildDependencySnapshot`、`BuildDependencyEntry`）は public なコンストラクタを持つ。選択（`BuildTagSelector.Select`、`BuildPurposeResolver.Resolve`）も public で、`BuildPlan` は選択の結果として得られる。`OneStarMaker.Build.Selection` の asmdef は参照 0 のまま。
  - **閉包を作る規則は internal。** `AssetDependencySnapshotBuilder` と `IAssetDatabaseGateway` / `UnityAssetDatabaseGateway` は internal。閉包に入れる file の規則（`Assets/` の配下、`.meta`・`.cs`・`.dll`・`.asmdef`・`.asmref` を除く、root 自身を含める、GUID と path の往復の一致）はこの builder だけが持つ。
  - **build の照合は snapshot の中身だけを見る。** `BuildContentProjection.Create` は、候補の `PhysicalKey` と閉包の `RootGuid` の一致、root が閉包に含まれること、path が `Assets/` で始まることを確かめ、表現は tag の `Representation` の dimension から読む（provider の名前と Scene の語彙は読まない）。entry の種別は root の path の拡張子（`.unity` なら Scene）で決まる。
  - **実行時に読む・検証する経路は、program の記述より開いている。** program §2.3 は「`ContentDirectoryIndex` と `InstalledRevisionVerifier` が internal なので、実行時に成果物を検証する public 経路も無い」とするが、次は public である: `BuildContentRoot`（`Entries`、`BuildIdentity`、`Target`、`SchemaVersion`）と `BuildContentEntry`（`LogicalKey`、`Representation`、`Kind`、`SceneId`、`Object`）。配布は `ContentTransportPublisher.Publish`、install は `ContentInstaller.InstallAsync`、install 済み revision の検証は `ContentCacheStore.ValidateInstalled`（内部で `InstalledRevisionVerifier` を呼ぶ。`ContentCacheStore.cs` と `ContentInstaller.cs` は program の base から変わっていない）、manifest の索引は `ContentPurposeIndex`（program の base の後に増えた）。directory の登録と root の取得は Unity の `ContentLoadManager` の public API で、連携部分の `UnityContentDirectoryBackend` も同じ API を呼ぶだけである。internal のままなのは、root の検査（schema・identity・target・entry の完全性）と解決の規則（表現が無い時の Full への fallback）を持つ `ContentDirectoryIndex` / `ContentEntryMatch`、上書きの層を重ねる `ContentDirectoryLayers`、`InstalledRevisionVerifier` 本体。
  - **target の名前は public な定数が無い。** `BuildContentCoordinator.TargetName` は internal、`ContentCacheStore` の `SupportedTarget` は private で、値はどちらも `StandaloneWindows64-Player`。
  - **出力先（X7）。** `BuildContentCoordinator.Build`（`Editor/Build/Content/BuildContentCoordinator.cs` 44〜56 行）は、root が Unity project の親の `artifacts/bs2b` か `TestResults/bs3-fixture` の配下でなければ `ArgumentException` を投げる。reparse point の検査（`ClearWorkspace`、`RejectReparsePoint`）は Full の build が workspace を空にする経路だけに掛かり、コメントも「汎用の filesystem 防御ではなく、この削除経路のための検査に限る」とする。report・staging・final への書き込みは、root が上の 2 つの配下であることだけに守られている。上書きの計画（`ContentOverlayPlanner`）と再利用の記録（`ContentOverlayReuse`）も同じ root を受け取る。
  - **program の base（`d4861882`）からの増分。** `develop` は 660 commit 進み、対象の folder で 50 file、+3,463 / −255 行が変わった。`Editor/Build/Content/` に上書きの build の 8 file（`ContentOverlay*`、`ContentSourceClosureRecord`、`ContentStagingMove`）、`Runtime/BuildContent/` に `ContentDirectoryLayers`・`ContentOverlayDigest`・`BuildContentOverlayBase`・`Distribution/ContentPurposeIndex` が増えた。`BuildContentCoordinator.cs` は 271 行になり、program が引いた行番号（33・45〜52 行）はずれている。
  - **`using` で見た向き（補助。P 検査の代わりではない）。** `Runtime/BuildContent/` で BuildContent の外の OSM の namespace を `using` するのは、`BuildContentRoot.cs`（`AssetType` のため）、連携の 2 file（`ContentDirectorySession.cs`、`UnityContentDirectoryBackend.cs`）、OSM 固有の 2 file（`PlayerContentConfiguration.cs`、`PlayerBakedContentConfiguration.cs`）だけ。`Editor/Build/Materialization/` で `OneStarMaker.Runtime.*` を `using` するのは OSM 固有の 2 file（`SceneResourceContentMaterializer.cs`、`ScenePayloadMappingPolicy.cs`）だけ。`OneStarMaker.Build.Content` と `OneStarMaker.Build.Materialization` は `OneStarMaker.Runtime` の `InternalsVisibleTo` に入っていないので、Runtime の internal は使えない。
  - **未確認。** P 検査（program §0）は Unity 同梱の `csc` と Git 外の script を使うので、この A1 を書いた環境では再実施していない。program の base の後に増えた file を含めた U-B の集合が今も閉じているかは、実測していない。Unity のテストも実行していない。
  - **open な PR との照合（2026-11-26）。** PR #214（branch `claude/trusting-ramanujan-kxphtr`、head `d8bd66b3`）と PR #216（branch `claude/magical-gauss-7rycuj`）の差分に、本スライスが変える予定の既存 file（§3）は無い。#214 は `Editor/Build/Content/` に 1 file、`Runtime/BuildContent/Distribution/` に 7 file を新しく足し、SampleGame の `SampleGameContentBatch.cs`・`VerifyPlayLauncher.cs` 等を変える。順序の制約は §2「本文へ転記した実装制約」。
- UC ごとの利用者の入力:

  **UC1 build の入力を組む**（開発時に build script へ書く）

  | 入力 | 誰がいつ決めるか | 目的からの正当化 |
  |---|---|---|
  | build する root の asset（GUID） | 利用者・開発時の宣言 | 「自分で選んだ asset」を選ぶのは利用者だけ |
  | 候補の logical key | 利用者・開発時の宣言 | 実行時に生成物から引く名前。利用者の code が使う |
  | 候補の stable key と由来（`BuildProvenance`） | 利用者・開発時の宣言 | 候補は選択の public な入力（`BuildTagSelector.Select`）で、利用者は選択のために同じ候補を作る。build は plan の候補と snapshot の候補を stable key・logical key・physical key で照合するので、同じ物を渡す必要がある。由来は issue の帰属に出る |
  | 表現の tag（任意） | 利用者・開発時の宣言。無ければ表現なし | 同じ logical key に複数の表現を持たせる時だけ要る |
  | requirement と graph（任意） | 利用者・開発時の宣言。既定は空 | 件数の規則や到達規則（`BuildPurposeResolver`）を使う時だけ要る |
  | 依存の閉包 | 機械・自動導出（root の GUID から `AssetDatabase`） | 利用者は入れない。規則は SceneResourceMap の経路と同じ builder が持つ |
  | tag の provider の識別子 | 機械・既定値（一般の入口の固定値） | 利用者は入れない。issue の帰属に出るだけで、利用者が選ぶ理由が無い |

  **UC2 build する**

  | 入力 | 誰がいつ決めるか | 目的からの正当化 |
  |---|---|---|
  | plan | 機械・自動導出（public な選択の結果） | 利用者は選択の要求（tag）を渡すだけ |
  | content set の名前 | 利用者・開発時の宣言 | workspace と配布の識別子 |
  | 出力先の root | 利用者・実行時の引数。本スライスでは今の許可 root の配下だけ | 置き場所を選ぶこと自体は X7 のスライスの問い |
  | workspace の扱い（`BuildContentMode`） | 利用者・実行時の引数。既定は `Incremental` | 配布する build は `Full` にする（既存の契約） |

  **UC3 生成物を読む**

  | 入力 | 誰がいつ決めるか | 目的からの正当化 |
  |---|---|---|
  | directory の path | 機械・自動導出（build の結果の `ContentPath`） | 利用者は結果を渡すだけ |
  | 引く logical key（と表現） | 利用者・実行時の操作 | UC1 で自分が付けた名前 |

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: BuildSystem の public な API だけで（SceneResourceMap も `InternalsVisibleTo` も使わずに）、利用者が選んだ asset を Content Directory として build する入力を組み立て、build し、生成物を読む最小経路を、既存の SceneResourceMap からの build の結果を変えずに成立させられるか。
- 進める最低条件:
  - 不変条件（起きてはいけないこと）:
    - **I1 既存の build の結果が変わる。** SceneResourceMap からの経路で作る snapshot の中身（候補、tag、provider の識別子、requirement、閉包、graph、欠けの記録）と、それを使う build の preflight・outcome・root の entry が、変更の前後で違ってはならない。
      - 崩しうる経路: (a) provider の識別子を引数にする時、既存の 3 つの internal なコンストラクタの既定値が今の固定値からずれる。(b) コンストラクタの overload を足すと、既存の 35 箇所の呼び出し（上の現況）の overload 解決が変わる、または曖昧になる。(c) 一般の入口のために `AssetDependencySnapshotBuilder` の規則や戻り方を変える（SceneResourceMap の経路と上書きの経路が同じ builder を使う）。(d) `BuildMaterializationResult` の issue の並びを決める規則に手を入れる。
      - 探索した境界: `Editor/Build/Materialization/` の全 file、`Editor/Build/Content/` の `BuildContentProjection.cs`・`BuildContentCoordinator.cs`・`BuildContentReport.cs` の入力の読み方、`Editor/Build/Selection/` で `StableProviderKey` を読む箇所（`BuildTagSelector.cs`、`BuildPurposeResolver.cs`）。
      - 未確認: `BuildContentReport` と `ContentTransportPublisher` が provider の識別子を file に書くか（`StableProviderKey` の grep では Selection の中だけに出た。issue の文言を経由して report に出る経路は追っていない）。
    - **I2 既存の公開面が壊れる・不用意に広がる。** 既存の public な型・メンバーの宣言を変えない、減らさない。`BuildMaterializationSnapshot` のコンストラクタを public にしない。増やす public は §3 に挙げた物だけ。
      - 崩しうる経路: internal なコンストラクタの昇格、`AssetDependencySnapshotBuilder` や gateway の昇格、既存の enum の値の挿入（末尾以外）や改名。
      - 探索した境界: 上の現況で挙げた型の可視性の一覧（`Editor/Build/` の 3 folder と `Runtime/BuildContent/`、`Runtime/AssetBuildContracts/`）。
    - **I3 一般の入口が OSM 固有の物を持ち込む。** 新しい入口の file は `SceneSystem`、`AssetDescriptions`、`AssetManagement`、`Foundation` の型を参照しない。一般の入口で作った snapshot の provider の識別子は OSM の SceneResourceMap の値にならない。U-B（Editor）の P 検査の集合（program §5.1）は、新しい file を含めて閉じたままである。
      - 崩しうる経路: 候補の作り方を `ScenePayloadMappingPolicy` と共通化する、provider の識別子の既定値を共有する、新しい file を OSM 固有の 2 file と同じ責務に混ぜる。
      - 未確認: 現在の U-B の集合が base `9e7d4148` で閉じているか（上の現況）。
    - **I4 不完全な入力が build に渡る。** issue が 1 件でもあれば snapshot を返さない。閉包を作れなかった root（GUID が解決しない、file が無い、依存の file が無い）を、閉包無しの候補として通さない。
      - 崩しうる経路: 欠けを許す版（`BuildAllowingAbsence`）を一般の入口が使う、issue を集めた後に部分的な snapshot を返す。
    - **I5 依存が増える。** production の asmdef の参照と `InternalsVisibleTo` を増やさない。足す asmdef は、U1 の裁定で認められた test 用の 1 つ（§3）だけである。
  - 成立条件（起きなければならないこと）:
    - **S1** public な入口 1 つで、SceneResourceMap を渡さずに、候補・tag・requirement・graph から `BuildMaterializationResult`（成功なら snapshot）を得られる。閉包は機械が導く。
    - **S2** S1 の snapshot と、public な選択で得た plan から、public な `BuildContentCoordinator` の `Build` が成功し、Content Directory ができる。
    - **S3** S2 の生成物を、public な API だけで読める（root が 1 件、identity が build の結果と一致、UC1 で付けた logical key の entry があり、load した物が元の asset の型である）。
    - **S4** 既存の回帰が通る（全 EditMode、`contract-audit`）。U-A・U-B の P 検査が成功する。
- 受け入れ条件:
  - **AC1（S1、I2）** `Editor/Build/Materialization/` に public sealed class `BuildCandidateMaterializer` があり、public なメンバーは引数無しのコンストラクタと `Materialize` 1 つだけである（署名は §3）。`BuildMaterializationSnapshot` のコンストラクタは全て internal のままである。
  - **AC2（S1、I4）** 単体テスト（fake の gateway）: 候補 1 件と解決できる root で、閉包 1 件・候補 1 件・issue 0 件の snapshot を返す。閉包は root 自身を含み、`.cs` と `.meta` を含まない。
  - **AC3（I4、失敗経路）** 単体テスト: 次のそれぞれで `Snapshot` が null、`HasErrors` が true で、issue の code が括弧の値である。GUID の形でない physical key（`InvalidRootGuid`）、解決しない GUID（`UnresolvedRootGuid`）、root の file が無い（`MissingRootAsset`）、依存の file が無い（`MissingDependencyAsset`）、stable key の重複（`DuplicateStableKey`）、別の候補と同じ physical key（`PhysicalKeyCollision`）、gateway の例外（`GatewayFailure`）。正常な候補と不正な候補を混ぜても snapshot は null である。
    - 合格例: 候補 2 件のうち 1 件だけ GUID が解決しない → `Snapshot == null`、issue 1 件。不合格例: 解決した 1 件だけの snapshot を返す。
  - **AC4（I3、失敗経路）** 単体テスト: 一般の入口の snapshot の `TagProviders[0].StableProviderKey` は `buildsystem.candidate-tags.v1` で、`osm.scene-resource-map.representation.v1` ではない。`BuildCandidateMaterializer.cs` は `OneStarMaker.Runtime` で始まる namespace を `using` せず、完全修飾でも参照しない（判定 C が file を読んで確かめる。最終の証明は AC9 の P 検査）。
  - **AC5（I1）** 既存の `SceneResourceContentMaterializerTests`、`MaterializeAllowingAbsentSourcesTests`、`BuildContentProjectionTests`、`BuildContentReportTests` を assertion を変えずに通す。加えて、SceneResourceMap の経路の snapshot の `StableProviderKey` が `osm.scene-resource-map.representation.v1` であることを固定するテストを 1 件足す（今は値を固定するテストが無い）。
    - 不合格例: 既存のテストの期待値を新しい値に書き換えて通す。
  - **AC6（I1、I2、失敗経路）** 既存の `new BuildMaterializationSnapshot(` の呼び出し 35 箇所（production 3 file、test 20 file）を書き換えずにコンパイルが通る。書き換えが要るなら I1 (b) に当たるので B は Phase A へ戻す。
  - **AC7（S2、S3、I5）経路テスト** EditMode のテスト 1 件が、次を public な型とメンバーだけで行う: test が所有する一時 folder に prefab を 1 つ作る → `BuildContentCandidate` と tag を組む → `BuildCandidateMaterializer.Materialize` → `BuildTagSelector.Select` で plan を得る → `new BuildContentCoordinator().Build`（root は `TestResults/bs3-fixture` の配下の一意な folder、`Full`）→ 結果の `ContentPath` を `ContentLoadManager.RegisterContentDirectory` で登録 → `GetRootAssets<BuildContentRoot>` が 1 件で、`BuildIdentity` が結果の `Identity` と一致 → logical key の entry を `Entries` から引き、`Object` を load して型を確かめる → 解放して登録を外す → 一時 folder と出力を消す。SceneResourceMap、SampleGame の asset、SampleGame の型を使わない。このテストは、`InternalsVisibleTo` の相手でない test 用の asmdef `OneStarMaker.Tests.BuildStandalone.Editor`（§3。U1 の裁定）に置く。internal を使うとコンパイルが通らないので、「public だけ」はコンパイラが強制する。
  - **AC8（S4）** GO 候補 head で、全 EditMode 回帰（`pwsh tools/run-tests.ps1`、filter 無し）と `pwsh tools/contract-audit.ps1` が合格する。
  - **AC9（S4、I3）** GO 候補 head で、U-A・U-B（Runtime）・U-B（Editor）の P 検査（program §5.1 の集合。U-B（Editor）は新しい file を含む）がコンパイルに成功する。P 検査の script と実行環境が判定 C で用意できない場合の扱いは §5。
  - **AC10（I5）** `git diff` で、production の `.asmdef` と `AssemblyInfo.cs` に差分が無い。増えた `.asmdef` は `OneStarMaker.Tests.BuildStandalone.Editor` の 1 つだけで、その参照は §3 のとおりである。
- ここでは答えない問いと所有する後続スライス:
  - 利用者が選んだ安全な出力先で build できるか、「安全な出力先の検証」と「OSM が許す置き場所の規約」を分けられるか、target の名前を public にするか（X7）→ 新規の小スライス `BUILDSYSTEM_OUTPUT_ROOT`（仮称、未作成。program §4.3 の X7 の行、D5 の「別の小スライス」）。
  - 実行時の検証済みの索引と解決の規則を public にするか → U2 の裁定で所有を決める。既定の案は asmdef 分割スライス（program §2.2 X4 の「BuildSystem 本体の internal 7 件」の裁定と同じ場所）。
  - SampleGame の合成を public な入口へ移し、`Materialization` の `InternalsVisibleTo("SampleGame.DependOnAll.Editor")` を外せるか → asmdef 分割スライス。既存の snapshot に候補を足す合成と、欠けの記録の引き継ぎが要るので、本スライスの入口 1 つでは足りない。
  - 欠けを許す materialize の一般の入口 → 要求が出た時に program へ足す（今は SampleGame の上書きの経路だけが使う）。
  - Content Directory 起動の facade → `RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md`。
  - 別プロジェクトでの単独利用の実証 → program §5.2 段階 4。
- スライス分割の判断:
  - 契機: 独立した変更理由の混在（X6 の入力の入口、X6 の実行時の読み口、X7 の出力先）と、高リスクで独立して失敗しうる未知が複数あること。
  - **X7 は分ける。** 理由は次の 4 つ。(1) 変更の種類が違う。X6 は public を足すだけで既存の挙動を変えない（不変条件は「何も変わらない」で書ける）。X7 は、処理を拒む既存の契約（許可 root の外を `ArgumentException` で拒む）を変える。(2) 「安全」の定義が未決である。今の reparse point の検査は workspace を空にする経路だけに掛かり、report・staging・final への書き込みは許可 root の配下であることだけに守られている（上の現況）。利用者が選んだ root では、その前提が無くなるので、何を検査すれば安全かを先に決める必要がある。これは path の安全性を専門に見るレビューが要る別の問いである。(3) Full の build は root の配下の workspace を消すので、失敗した時の影響（利用者の directory の削除）が X6 と比べて大きく、X6 の合否と独立に失敗しうる。(4) X7 は OSM の置き場所の規約をどこへ移すかで SampleGame の呼び手（`SampleGameContentBatch.cs`、`VerifyPlayLauncher.cs`）に触れる可能性があり、この 2 file は open な PR #214 が変えている。分ければ、X6 は #214 と file が重ならないまま進められる。
  - 分けても program の D5（「X7 は同じスライスか別の小スライス」「asmdef スライスより前」）の範囲内である。順序の扱いは U3。
  - **X6 の実行時の読み口は本スライスに入れない（U2 で owner に確認）。** program は「最小の入口 1 つから始める」とし、測り直しで読み・検証の経路が今の public だけで辿れると分かった。本スライスは AC7 の経路テストでそれを証拠にし、検証済みの索引を public にするかは、その証拠を見てから決められる。
- 判定定義: GO は AC1〜AC10 が全て合格。NO-GO は 1 つでも不合格、または I1〜I5 の違反。スパイクではないので CONDITIONAL ACCEPT は使わない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし（A3 未凍結）
- 本文へ転記した実装制約:
  - Unity側の `.cs` は先頭に `#nullable enable` を置く。`record` を使わない。Editor の code を Runtime の assembly に置かない。
  - 公開 API に `ZLogger*` の型を出さない。
  - asmdef の参照の追加は設計判断で、勝手に行わない。足してよい asmdef は U1 で裁定済みの test 用の 1 つ（§3 の名前・置き場所・参照）だけで、それ以外の asmdef の追加と参照の追加は Phase A へ戻す。
  - 参照 0 を削除の理由にしない。internal なコンストラクタと `InternalsVisibleTo` は残す。
  - テストで `Task.Delay` / `Thread.Sleep` を使わない。
  - 新しい file の path は `tools/path-budget.json` の予算に従う。
  - **PR #214 との順序。** 本スライスは #214 が変える既存 file に触れないので、どちらが先に merge されてもよい。ただし #214 は U-B の集合に入る folder（`Runtime/BuildContent/Distribution/`、`Editor/Build/Content/`）へ新しい file を足すので、AC9 の P 検査は GO 候補 head の時点の `develop` を含む tree で行う。#214 が先に入った後に P 検査が失敗し、原因が #214 の file なら、本スライスの欠陥ではなく program への入力として扱う。Phase B は `SampleGameContentBatch.cs`・`VerifyPlayLauncher.cs`・`PlayEntry*.cs` を変えない。
  - `docs/handoff/PLAY_CONTENT_RESOLUTION_PROGRAM.md` と DebugStudio の HANDOFF は他のラインが編集するので、本スライスは変えない。
- 裁定済みの事項:
  - **U1 経路テスト用の asmdef を足す（owner、2026-11-26、A1 初稿の Draft PR の後の回答）。** `InternalsVisibleTo` の相手でない test 用の asmdef を 1 つ足し、AC7 の経路テストをそこに置く。名前・置き場所・参照は §3 のとおり。採らなかった案は、asmdef を足さずに `OneStarMaker.Tests.Editor` に置き、判定 C が file を読んで「public だけ」を確かめる案（既存の test の assembly は `InternalsVisibleTo` の相手なので、internal を使ってもコンパイルが通り、強制力が弱い）。この asmdef は `OneStarMaker.Runtime` 全体を参照するので、型の閉包は示さない（閉包は P 検査、経路はテスト。program §5.2 段階 2）。
- 未決事項（A3 の前に owner が裁定する）:
  - **U2 実行時の読み口を本スライスから外してよいか。** program §4.3 の X6 は「実行時に成果物を読む・検証する public 入口（`ContentDirectoryIndex` 等）」を含む。測り直しでは、検証（`ContentCacheStore.ValidateInstalled`、`ContentInstaller.InstallAsync`）と読み（Unity の API と `BuildContentRoot`）は既に public で、internal に残るのは root の検査と Full への fallback の規則である。本書は「外して、AC7 の証拠の後に asmdef 分割スライスで決める」案で書いた。入れる場合は、`ContentDirectoryIndex`（`TryCreate`、`TryResolveObject`、`TryResolveScene`）と `ContentDirectoryIssue` の 2 型を public にする 2 つ目の入口になり、A1 を改訂してから A2 へ進む。外す場合、U-B の利用者は fallback の規則を自分で書くことになり、program の D5 の「単独利用の完成条件」にこの欠けを含めるかも合わせて決める。
  - **U3 分けた X7 のスライスの順序。** 案: D4 の列を (2) 本スライス → (2') `BUILDSYSTEM_OUTPUT_ROOT` → (3) `CONTENT_DIRECTORY_STARTUP` → (4) asmdef とし、(3) の A1 の開始条件は今のまま「(2) の Phase D の後」にする（X7 は起動の facade が触る file と重ならないので、(2') と (3) は並行できる）。(2') は PR #214 の merge の後に A1 を始める。
  - **U4 期限。** program の期限は 2026-11-30 で、あと 4 日である。本スライスの A2・A3・B・C はそれまでに終わらない。program の期限を更新するか、置換 revision にするかは owner の判断で、本書と `docs/README.md` は期限を書き換えていない。本書の期限（2026-12-31）も仮の値で、owner が確認する。

## 3. 責務マップ

path は `unity/Assets/OneStarMaker/` からの相対。

| file | 責務 | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|---|
| `Scripts/Editor/Build/Materialization/BuildCandidateMaterializer.cs`（新規） | 利用者が渡した候補・tag・requirement・graph を検査し、root ごとの閉包を導いて snapshot にする公開入口（orchestration と入力の検査。Unity の I/O は gateway 越し） | 一般の入力の入口（S1） | 状態を持たない。呼び出しごとに結果を返す | 入力: `OneStarMaker.Build.Selection` の public 型、同 folder の `AssetDependencySnapshotBuilder`・`IAssetDatabaseGateway`・`BuildMaterializationSnapshot`・`BuildMaterializationIssue`。`OneStarMaker.Runtime.*` は使わない | public: 型 1、コンストラクタ 1、メソッド 1。internal: gateway を受けるコンストラクタ | fake の gateway で Unity の `AssetDatabase` 無しに全分岐を検査できる | 0 → 約 130 |
| `Scripts/Editor/Build/Materialization/BuildMaterializationSnapshot.cs` | 候補一式の不変の入れ物 | provider の識別子を internal な引数で受ける（既存の 3 コンストラクタは今の固定値を渡す） | 変更なし | 変更なし | 変更なし（internal の増分だけ） | 既存のテスト + AC5 の 1 件 | 100 → 約 112 |
| `Scripts/Editor/Build/Materialization/BuildMaterializationIssue.cs` | issue の code と対象 | 候補を対象にする issue のため、`BuildMaterializationSubject` の末尾に `Candidate` を足す | 変更なし | 変更なし | public な enum の値 1 つ（末尾への追加） | AC3 | 53 → 約 54 |
| `Tests/Editor/Build/BuildCandidateMaterializerTests.cs`（新規） | 入口の単体テスト（AC2〜AC4） | — | — | `OneStarMaker.Tests.Editor`（internal なコンストラクタを使う） | — | — | 0 → 約 220 |
| `Tests/Editor/Build/SceneResourceContentMaterializerTests.cs` | 既存 | AC5 の 1 件を足す | — | — | — | — | 追加 約 15 |
| `Tests/Editor/BuildStandalone/BuildStandalonePathTests.cs`（新規） | AC7 の経路テスト | — | 一時 folder と出力は test が所有し、終わりに消す | 下の asmdef の参照だけ。public な型とメンバーだけを使う | — | 実 Unity の Content Directory build を 1 回行う | 0 → 約 180 |

- test 用の asmdef（U1 の裁定。B が設計を足さないために固定する）:
  - 置き場所と名前: `Tests/Editor/BuildStandalone/OneStarMaker.Tests.BuildStandalone.Editor.asmdef`。既存の `Tests/Editor/TestObservation/OneStarMaker.Tests.TestObservation.Editor.asmdef` と同じ形（`Tests/Editor/` の下の folder に専用の test の asmdef を置く）。repo 相対 path は `.meta` を含めて最長 108 字で、`tools/path-budget.json` の上限 150 に収まる。
  - 参照: `OneStarMaker.Build.Selection`、`OneStarMaker.Build.Materialization`、`OneStarMaker.Build.Content`、`OneStarMaker.Runtime`、`nunit.framework.dll`。Unity のテストの実行に要る TestRunner の参照は、既存の test の asmdef に合わせて B が足してよい。SampleGame、`OneStarMaker.Editor`、`OneStarMaker.Tests.Editor`、`OneStarMaker.TestSupport` は参照しない。
  - 設定: Editor 専用、`UNITY_INCLUDE_TESTS`、`autoReferenced` は false。
  - この asmdef を参照する asmdef は無い。どの `AssemblyInfo.cs` の `InternalsVisibleTo` にもこの名前を足さない（足すと目的が無くなる）。
  - `unity/Assets/README.md` の folder の図と依存図に、TestObservation の行と同じ書き方で各 1 行を足す。
- 新しい入口の署名（B が設計を足さないために固定する）:

  ```csharp
  namespace OneStarMaker.Editor.Build.Materialization
  {
      public sealed class BuildCandidateMaterializer
      {
          public BuildCandidateMaterializer();
          internal BuildCandidateMaterializer(IAssetDatabaseGateway gateway);

          public BuildMaterializationResult Materialize(
              IEnumerable<BuildContentCandidate> candidates,
              IReadOnlyDictionary<string, IReadOnlyList<BuildTag>> tags,          // stable key → tag。無い候補は tag 無し
              IEnumerable<BuildContentRequirement>? requirements = null,
              IEnumerable<BuildGraphNode>? graph = null);
      }
  }
  ```

  - 候補の `PhysicalKey` を root の GUID として読み、閉包を `AssetDependencySnapshotBuilder.Build`（欠けを許さない版）で導く。
  - 引数の誤り（`candidates` か `tags` が null、`tags` の key が候補に無い stable key）は `ArgumentException` 系で投げる。入力の中身の不整合と `AssetDatabase` の結果は issue にし、issue があれば snapshot は null（既存の `BuildMaterializationResult` の規則）。
  - issue の code は既存の値だけを使う。新しい code が要ると分かったら B は Phase A へ戻す。
  - provider の識別子は `buildsystem.candidate-tags.v1` の固定値。
- 配置の理由: `SceneResourceContentMaterializer` と同じ folder・namespace・asmdef（`OneStarMaker.Build.Materialization`）。同じ「materialize の公開入口」で、同じ internal（builder、gateway、snapshot のコンストラクタ）を使う。別の folder にすると internal を越えるために `InternalsVisibleTo` か昇格が要る。program §2.1 の分類では BuildSystem 本体（Materialization の一般部分）に入り、OSM 固有の 2 file とは依存（`SceneSystem`、`AssetDescriptions`）が違うので file を分ける。
- 行数警報: 該当なし（500 行・3 責務・50% 増のどれにも当たらない。`BuildMaterializationSnapshot.cs` は 12% 増）。
- 採らなかった構成:
  - **コンストラクタを public にする。** program §4.3 が禁じている。provider の識別子が OSM の値のまま漏れ、閉包の規則を利用者が再実装することになる。
  - **閉包を利用者が渡す factory（program §4.3 の文言どおり）。** 閉包は root の GUID から機械が導けるので、利用者の入力にする理由を目的から説明できない。渡させると、閉包の規則（上の現況）を public にするか、利用者が再実装するかのどちらかになる。program の「候補・依存の閉包・tag・graph を利用者が組み立てられる factory」とはこの点で違うので、A2 で見てほしい。
  - **候補も機械が作る薄い入力の型（logical key・GUID・表現だけを受ける）。** 利用者の入力は減るが、public な型が 1 つ増える。候補は選択の public な入力として既に利用者が作る物なので、今回は足さない。

## 4. 実装計画

- 変更対象: §3 の表の file。
- 順序:
  1. `BuildMaterializationSnapshot` の provider の識別子を internal な引数にし、既存の 3 コンストラクタが今の固定値を渡す形にする。AC5 のテストを足し、既存のテストが変更無しで通ることを確かめる（I1）。
  2. `BuildCandidateMaterializer` と単体テスト（AC1〜AC4）。
  3. test 用の asmdef（§3）と経路テスト（AC7）。`unity/Assets/README.md` の folder の図と依存図に各 1 行を足す。
  4. `git diff` で AC6・AC10 を確かめる。
- Phase B から Phase A へ差し戻す条件: §3 に無い public な型・メンバー・enum の値が要る。既存の呼び出しの書き換えが要る（AC6）。production の asmdef の参照か `InternalsVisibleTo` の追加が要る。test 用の asmdef に §3 に無い参照が要る。経路テストが public だけでは書けない（S3 の反証で、U2 の前提が崩れる）。`AssetDependencySnapshotBuilder` の規則の変更が要る。
- 対象外を維持する方法: `Editor/Build/Content/`、`Runtime/BuildContent/`、`Runtime/AssetBuildContracts/`、SampleGame の production に差分を出さない（`git diff --stat` で確かめる）。出力先は今の許可 root の配下だけを使う。

## 5. テストとレビュー計画

- 単体テスト: `BuildCandidateMaterializerTests`（fake の gateway。AC2〜AC4）、`SceneResourceContentMaterializerTests` の追加 1 件（AC5）。
- 差し戻し中の起点 `-Filter`: test class の `BuildCandidateMaterializerTests`、`SceneResourceContentMaterializerTests`、`MaterializeAllowingAbsentSourcesTests`、`BuildContentProjectionTests`、`BuildContentReportTests`、`BuildStandalonePathTests`。書式は `pwsh tools/run-tests.ps1 -Filter` に従う。
- 判定必須テスト（GO 候補 head）: 全 EditMode 回帰（`pwsh tools/run-tests.ps1`、filter 無し）、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、P 検査（AC9）。
- 全 EditMode 回帰の適用除外: なし
- 統合・Unity テスト: AC7 の経路テスト（実 Unity の Content Directory build を 1 回）。既存の `SampleGame/Tests/Editor/Build/BuildContentDirectoryIntegrationTests.cs` は U-C の非退行として全回帰に含まれる。同テストは build の所要時間で `[Timeout(360000)]` を超えたことがある（`RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md` §2）。経路テストは prefab 1 つだけを build するが、timeout の値は B が既存の統合テストに合わせて付ける。
- 操作・実行時・目視条件の検証経路: 該当する条件は無い（全ての条件は Unity のテストと P 検査で閉じる）。動作確認は `対象なし`。
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点:
  - EditMode のテストから実 Unity の Content Directory build を行い、同じ process で `ContentLoadManager` に登録して load する経路は、既存の `BuildContentDirectoryIntegrationTests` と `UnityContentDirectoryAdapterTests` が使っている既知の経路である。違いは、session を通さずに Unity の API を直接呼ぶ点だけ。
  - 未確認。初回確認は Phase C: (1) session を通さない登録・load・解除が EditMode で成立するか。(2) P 検査の script（ASSET_CYCLE で凍結した `p-check.ps1`、Git 外）を判定 C の環境で取得して実行できるか。理由: この A1 を書いた環境に Unity と Git 外の script が無い。担当: Phase C の実行担当。不成立時: (1) は B へ戻し、session 無しで読めないなら U2 の前提（今の public だけで読める）が崩れるので Phase A を再開する。(2) は script を取得できなければ AC9 を `未実行` として人間に返し、P 検査を取り直す方法（program §0 の手順の再作成）を A3 で合意しておく。
- 人間の判断が必要な条件と合意した担当・証拠の受け入れ方: なし
- 高コスト証拠: なし
- 判定 C の照合 script の方式: なし（判定はテストの結果と `git diff` で行う。P 検査は ASSET_CYCLE で凍結済みの script を使い、本スライスでは新しい照合 script を作らない。集合の定義は program §5.1 のままで、新しい file は folder の規則で U-B（Editor）に入る）。
- 機械検査: `pwsh tools/docs-audit.ps1`、`pwsh tools/contract-audit.ps1`。A1 の commit の時点で両方を実行した（結果は PR 本文）。
- A0/A1 主担当・モデル・ベンダー: Cloud の計画 session。モデルの識別子は公開文書に書かない（起動元の記録にある）。
- C' 用に予約した担当・モデル・ベンダー: 未定。A2 で使う系列を決める時に、A・B・C に関与しない系列を 1 つ残す。
- 独立性の強化条件を満たせない場合の理由: 未定（A2 の割当の後に書く）

### 5.1 A2 の独立レビューと A3 の採否

- A2 独立レビューごとの観点・担当・モデル・ベンダー、入力版: 未実施。予定する観点は、アーキテクチャゲート（特に §3 の「採らなかった構成」と、入口を `Materialization` に置く判断）、目的レビュー、A0 だけからの代替構成。各不変条件を破る順序を 1 つ挙げられるかも A2 で確かめる。
- A3 統合担当・モデル・採否: 未実施

## 6. Phase B 実装結果

未着手

### 動作確認

未着手（検証経路に該当する条件が無いので、Phase B の後は `対象なし` と記録する予定）。

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手
