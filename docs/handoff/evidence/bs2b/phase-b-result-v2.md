# BS2b — Content Directory build

- type: slice
- status: Phase A3 凍結（実装前）
- branch: `codex/bs2b-content-directory-build`
- implementation base commit: `ef3ad21`
- implementation head commit: `c0fff31`
- risk: high（生成物形式、Editor build、Runtime 受渡し）
- owner: BS2b 主担当 / Codex
- created: 2026-09-17
- expires: 2026-10-17。Phase D で判断し、残す契約を公開面へ harvest する。
- harvest to: `unity/Assets/Docs/Architecture/18-asset-description.md`、`13-resource-system.md`、`20-variant-checkout-workflow.md`
- Phase A snapshot: `docs/handoff/evidence/bs2b/phase-a-snapshot.md`、2026-09-17 07:58 JST、SHA-256 `d18f4af428c18cb4ac1ee3807d9f8ea051d3f1906972c8685875ac0888f7da41`
- Phase B result snapshot: このファイル。hash は HANDOFF と evidence manifest に記録する。
- evidence / C' snapshot: 各 Phase で固定後に path、時刻、SHA-256 を記録する。

## 1. 目的、現在地、対象外

問いは「成功した BuildPlan と対応する BuildMaterializationSnapshot から、Scene と代表非 Scene を含む Content Directory を生成できるか」。BS1 は選択を、BS2a は SceneResource の候補と依存閉包を生成済み。Build 経路と Runtime 経路は未接続。旧 Addressables build は維持する。

対象外は SampleGame の本番選択 policy（BS2c）、Runtime 登録・解決・寿命（BS3）、Player（BS4）、配信と cache（DIST）、旧経路廃止（RET）、非 Scene Description の本番実装、同一ファイル内サブアセットの汎用 locator。`CD0Spike` は復元しない。

## 2. A0: 入力、制約、未決事項

- 入力は成功済み `BuildPlan` と同一候補集合を持つ `BuildMaterializationSnapshot`。選択をやり直さず、`AssetDatabase.GetDependencies` で閉包を再計算しない。snapshot は Scene 専用の tag provider 名を持つが、build consumer はそれを解釈しない。
- 候補は `StableKey`、`LogicalKey`、ファイル GUID の `PhysicalKey` を持つ。閉包は root GUID / path と各依存 GUID / path。異なる root の共有依存は許し、二つの候補が同じ physical key を所有することは許さない。
- 既存 snapshot constructor は internal。非 Scene の Prefab / Texture fixture は既存 friend test assembly で作り、本番 factory や source 登録を増やさない。
- 常時契約: Game → Framework、一方向依存、asmdef edge は設計判断、Editor コードを Runtime に置かない、Unity C# の `#nullable enable`、`record` 禁止、偽 null、SceneState と AssetOwner の維持。
- 未決事項は build root の参照表現、Content BuildReport の保存形式、生成・出力 path の所有、固定 target/subtarget、増分保証、論理対応 metadata の形式。以下 A1 で決め、A2 で検証する。

## 3. A1: 意思決定境界

### 進める最低条件

固定 target/subtarget の一つの Content Directory を、成功した plan と対応 snapshot から Scene・Prefab・Texture fixture について実生成する。build consumer は Scene 固有情報を使わない。選択された logical/representation/load target と成果物および対応する Content BuildReport が同じ build identity に紐づくことを検証する。

### 受け入れ条件

1. Unity I/O のない中核が plan と snapshot の候補対応を stable/physical/logical key で検証し、選択 root と既存閉包だけを canonical 化する。欠損、余分な selected candidate、root/path/GUID 不整合、同一 physical key 衝突を build 前の構造化 issue にする。入力順序で結果を変えない。
2. Scene と非 Scene の選択候補を同じ中核で扱う。非選択 Variant の root は含めず、選択 root が共有する依存ファイルは保持する。closure の共有と候補 physical key の衝突を区別する。
3. Editor adapter が生成 root を作り、選択 root の Scene `LoadableSceneId` と Object `LoadableObjectId` を格納する。root は build identity を持つ。catalog 内のロード用 identity は Unity が作る reference とし、ファイル GUID 単独を任意 Object のロード identity と呼ばない。
4. 同一 logical asset の複数表現を一つの directory に格納できる形式とする。表現は候補の `Representation` tag を snapshot から読み取って metadata に保存するが、tag provider 名と Scene 語彙には依存しない。tag の無い候補は空表現として保存する。BS2b は複数表現の選択 policy を変更しない。
5. runtime 側の単一 `BuildContentRoot : ScriptableObject` に schema version、build identity、target と entry 配列を保持する。entry は logical key、stable key、表現値、種類（Scene/Object）、その index に対応する `LoadableSceneId` / `Loadable<UnityEngine.Object>` を持つ。Object の Unity reference は file ID を含む `LoadableObjectId` から Editor で構築する。BS3 は local directory の登録後に `ContentLoadManager.GetRootAssets<BuildContentRoot>(handle)` が厳密に 1 件であることを確かめ、この root だけで logical→load target を復元する。本番 source 再走査や別 sidecar は要求しない。サブアセット一般化と型検証は BS3。
6. build 前 report は選択、除外理由、issue、root/closure、target、identity を機械可読で示す。失敗時は成功出力を公開しない。成功時は Unity `Content BuildReport` と同じ identity を持つ root、summary、Unity manifest pointer (`BuildManifestHash.txt`) と report/metadata directory location を返す。Unity 内部ファイル一覧を解釈しない。
7. 実 directory の Scene、Prefab、Texture root と非選択 root の不在、共有依存、root metadata の対応を統合検証する。別 logical の共有依存に加え、同じ logical の異なる physical key を持つ 2 候補を `OneOrMore` の非 Scene fixture で選択し、異なる表現、root entry と loadable ID が同じ directory にあることを検証する。directory を新しい local path へ複写し、登録・単一 root discovery・identity 照合を source 再走査なしで確認する。固定 target/subtarget に限定し、incremental は同じ build workspace への再 build 成功と report の対応まで保証する。content hash の安定性や変更 matrix は保証しない。

### 成果物・I/O の凍結案

- partition は選択済み content の単一 directory。固定 target と content set 名に対応する `artifacts/bs2b/work/<target>/<content-set>/` を Unity の再 build workspace として反復利用する。成功後、完全な directory 一式を `artifacts/bs2b/content/<build-id>/` へ staging 経由で複写・原子的に publish する。build-id は UTC timestamp と乱数の衝突しない識別子。content set 名は呼出側が指定する安定した ASCII 識別子で、path segment として検証する。指定できる root は `artifacts/bs2b` 配下へ正規化・制限する。既存成功 directory を上書きしない。
- `Assets/OneStarMakerGenerated/BS2b/<build-id>/` の生成 root は Editor adapter が所有する一時資産。成功・失敗いずれも AssetDatabase で削除し、孤児は次回開始時に ownership marker を確認して限定 cleanup する。非生成 assets は削除しない。
- target は Windows Standalone 64 bit、subtarget は Player。Editor の active target が一致しない場合は build 前に失敗させ、自動切替しない。compression は Uncompressed、BuildContentOptions.None。対象 platform が無い環境では NO-GO と記録する。
- `Content BuildReport` は Unity 返却 object と生成 report/metadata directory の存在を検査し、summary を記録する。Unity 内部形式を OSM 外部 protocol にしない。build identity は root、OSM report と Unity build `name` に一致させる。Player metadata location は BS4 へ渡すが、Player build への注入自体は BS4 の責務。
- report は preflight と outcome を別ファイルへ書く。Unity build は固定 workspace で行い、成功と metadata 検証後に全 content file を staging へ複写して final directory へ移す。失敗した workspace は診断と再試行のため保持するが成功 path として返さない。失敗 staging だけを限定 cleanup する。出力先の containment と既存 directory 保護を行う。

### 判定と停止規則

GO は最低条件と全受け入れ条件が同一固定 target の生テスト、実 build、report/manifest 検査で成立し、現在の問いに致命的反証がない場合。未達は NO-GO。スパイクではないため CONDITIONAL ACCEPT は使わない。条件成立後は後続課題を owner に渡して止める。A3 後の追加は凍結済み条件または常時契約違反に限り、他は後続 slice へ移す。

## 4. 責務マップと配置案

| ファイル / assembly | 責務、変更理由、所有者・寿命、依存、テスト境界 | 現在 / 予想行数 |
| --- | --- | --- |
| `Editor/Build/Content/BuildContentProjection.cs` / 新 Editor asmdef | `OneStarMaker.Editor.Build.Content` の internal pure core。plan/snapshot 照合、closure canonical 化、issue。呼出し中だけの immutable data。Selection と Materialization に依存し Unity I/O 無し。friend tests で全分岐。 | 新規 / 250 |
| `Editor/Build/Content/BuildContentCoordinator.cs` / 同 asmdef | 同 namespace の public build 入口と internal `IContentDirectoryAdapter` port。preflight→Unity build→検査→publish と失敗 staging cleanup。1 run の所有者。projection と port に依存。fake port で失敗経路を検証。 | 新規 / 200 |
| `Editor/Build/Content/UnityContentDirectoryAdapter.cs` / 同 asmdef | 同 namespace の internal adapter。AssetDatabase、root 生成、Unity BuildPipeline、report。Editor I/O と生成 root の限定 cleanup の所有。実 build test。 | 新規 / 250 |
| `Editor/Build/Content/BuildContentReport.cs` / 同 asmdef | 同 namespace の public request/result/report と internal JSON serializer。preflight issue、選択・除外・closure、outcome の immutable snapshot。1 run 寿命。純粋テスト。 | 新規 / 150 |
| `Runtime/BuildContent/BuildContentRoot.cs` / Runtime asmdef | `OneStarMaker.Runtime.BuildContent` の public sealed root と entry 読取 API、Editor adapter が呼ぶ public 初期化口。Unity に serialize する Scene/Object reference と identity。build artifact 寿命。Editor 依存なし。 | 新規 / 150 |
| `Tests/Editor/Build/BuildContentProjectionTests.cs` / 既存 friend asmdef | Scene/Prefab/Texture fixture、欠損・衝突・順序・共有依存。 | 新規 / 250 |
| `Tests/Editor/Build/BuildContentDirectoryIntegrationTests.cs` / 既存 friend asmdef | 一時 fixture の実 build、root/report 確認、複数表現と移設後の discovery、cleanup。 | 新規 / 220 |

新しい Editor asmdef は `Selection`、`Materialization`、`Runtime` を参照する。`Tests.Editor` から新 asmdef への参照を追加する。いずれもこの Phase A の明示判断とし、他の edge は増やさない。Runtime asmdef の中に Editor API は置かない。root の serialized field は Unity artifact lifetime。`AssetOwner` は Runtime load の BS3 で扱い、build 中の一時資源とは混同しない。500 行警報は現計画では発火しないが、実装時に増えるなら再評価する。

## 5. 実装、テスト、レビュー

順序: projection と純粋テスト → runtime 受渡し型 → Editor adapter / coordinator → 実 build fixture → Phase C。SceneResourceMap の走査や policy を consumer に入れない。中核で Unity API が必要、追加 public API / asmdef edge / owner が必要、root 形式が成立しない場合は Phase A revision へ戻す。

Phase C は実装 base/head の完全 diff と生ログを固定し、構造レビューを機能レビューより先に行う。`pwsh tools/contract-audit.ps1`、Editor を閉じて sandbox 外の `pwsh tools/run-tests.ps1`、必要な実 build を実行する。C' は新規セッションかつ B/C と異なるモデルの blind audit とする。

- A0/A1 主担当: Codex / GPT-6 Astra（OpenAI）
- A2 architecture: GPT-5.6 Sol / OpenAI、A1 を読んだ独立レビュー。root discovery 未固定、複数表現の実証不足、file/API/port map 不足を採用し、§3-4 に反映。
- A2 alternative: GPT-5.6 Terra / OpenAI、A0 と現行 contract のみを読む独立構成案。固定 build workspace の再利用、Unity manifest pointer と report/Player metadata location、active target の検査、成功 directory の全量 publish を採用。Player build 実行と metadata 注入は BS4 のため BS2b に入れない。Unity 内部 manifest の parse は採用しない。
- A3: program §4 の 2026-09-17 不在時委任に基づき主担当 GPT-6 Astra が上記採否を統合して凍結。高リスクのため A2 2件を実施。両レビューは OpenAI 系列なので強化条件の異ベンダー予約は満たさず、C' で可能なら未関与系列を使い、不可なら独立性制約と記録する。
- Phase B/C/C': 未着手。C' 用に A/B/C 未関与モデルを予約する。

## 6. Phase B 実装結果

- 実装: `340376f` と `78cd163`。Unity I/O を使わない projection、構造化 issue、Editor build orchestration / adapter、単一 Runtime root、preflight/outcome report、Prefab/Texture fixture を含むテストを追加した。新 asmdef は Phase A の依存方向に配置した。Selection assembly の internal constructor は既存 Editor test assembly へ限定公開し、無効な plan fixture も検証可能にした。
- HANDOFF との差: build report の受渡し位置は Unity content 出力 directory を用いる。Unity 6.6 の `previousBuildReportDirectories` は build 出力 folder も受け付ける。Player 注入は BS4 のまま。
- 未実行: Unity Editor コンパイル確認、バッチテスト、実 Content Directory build。Editor は未接続で、sandbox 内の PowerShell 起動が停止したため、Phase C の batch 経路で確認する。
- 機械検査: `pwsh -NoProfile -File tools/contract-audit.ps1` を sandbox 外で実行、exit 0、変更 Unity `.cs` 9件、違反なし。`git diff --check` exit 0。
- Phase B 担当・モデル: 独立 context の subagent、GPT-5.6 Sol / OpenAI。commit 固定は主担当 GPT-6 Astra。

## 7. Phase C

未着手。

## 8. Phase C'

未着手。

## 9. Phase D

人間の develop マージ判断を待つ。

## Phase B addendum for implementation head c0fff31

- Projection-only data types ProjectedContent and BuildContentProjectionResult are internal.
- The integration test builds the same content set twice using the fixed workspace, verifies distinct published identities and both reports/manifests, then copies the published directory and checks the registered root in PlayMode.
- Focused Unity tests: 5 passed, 0 failed. Full Unity EditMode: 761 passed, 0 failed, 0 skipped. Raw results and log are in the evidence bundle.
