# Artifact Evidence offlineテスト可搬性

## 0. メタデータ

- type: slice
- status: B — 2026-10-07 JST、ownerの明示承認でA3凍結。Phase BからC/C′まで実施、D未承認。
- revision: portability-a3-frozen-r2
- branch: `codex/artifact-offline-portability-phase-a`
- implementation base commit: `c0f7d6e57ba1fb918863403f6b0077b4ea4c3509`（2026-10-07 JST、origin/developをls-remoteで照合）
- implementation head commit: 未生成。計画commitを実装判定headにしない。
- risk: normal（tests限定。本番selection/protectionの変更が必要ならAへ戻す）
- owner: OSM maintainer。Phase A主担当はこのCodex session。
- created: 2026-10-07
- expires: 2026-10-21または置換revision。programの残件期限2026-12-26とは別。
- harvest to: `tools/Artifacts/README.md` のoffline再現条件。D完了時に本HANDOFFを削除する。
- Phase A snapshot: `C:/Users/void/AppData/Local/OneStarMaker/Artifacts/transfers/907102fae5704867b0508d8b9b8f78e1/A3.md`。source commit `72de8cc889df40da891409c00d9033467bead3b2` の§0〜5、generated at `2026-10-07T00:32:50.6248551Z`、SHA-256 `46f01c8aba7be6cbb34c5f96458ccaa066ab85f46098dd1ceebfb360bd17a356`。A2初稿と承認仕様は区別する。
- Phase B result snapshot / evidence bundle / C′ blind bundle: 未生成。
- workflow: 今回は従来のtracked HANDOFF方式を提案。`external-current-v1`/H1/H2の適用は提案しない。CURRENT整備を可搬性2件の条件に混ぜない。

## 1. A0 — 固定入力と候補比較

### 入力版とcheckout

A0に使用する文書・codeはすべて上記base commitのGit blobを正とする。再開時はcurrent developへ読み替えず、`git show <base>:<path>`で取得する。READMEの最終変更commitは`5fb69787f4a6c73445dc51ac77601ad5bb525ba6`（PR #105）、programは`182f78b8e19d8a864c19b8d9f31b111672208ee8`（PR #104のD harvest）。program policyは現行r4。旧r1/r2/r3は歴史的入力であり実装開始条件へ採用しない。

全文確認した入力（path → Git blob ID）:

- `AGENTS.md` → `930ed664344826990f7c3b019e94802594243f08`
- `.agents/skills/osm-workflow/SKILL.md` → `999c4ecfd0dd07fb3230b1fd03a622b1f3dfc194`
- 同Skillの`references/phases-and-handoff.md` → `d0833b605e17591dfdc8dbf177320ba7742ad156`
- `references/docs-policy.md` → `79f2f5a844a6d9b741bcc8bb87eaf0f678fb9b03`
- `references/architecture-gates.md` → `b50e986549c629d38c8278202896b5fe2a33e4fd`
- `references/handoff-template.md` → `a6961783834649ffdf52410ac345e38d372d9c6d`
- `references/review-evidence.md` → `25e8f81c2520f057de91f67b2fceac7dc27dee90`
- `docs/README.md` → `00692fd8b0918c26829bd8cba499e440841d0ed7`
- `tools/Artifacts/README.md` → `089ab9a3e8b8903dff41dab641ac1396b64888f2`。checkout bytes SHA-256 `d03209d7f94fc3c3ef3d84f94134062f4099eec9a9b62c8d5e567ddd976c02c7`。
- `docs/handoff/BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` → `47824342e4dc1e9a2c727cd949066dd9eba7be37`。checkout bytes SHA-256 `c74a2879ae6db50627d8f9118c7f927c79b4b5088521e4aec360c8d501c983d9`。
- `tools/Artifacts/tests/ArtifactEvidence.Tests.ps1` → `c4d17a2b3fb6b9e46dcc985bac5248caa21761f0`
- `tools/Artifacts/EvidenceApplication.psm1` → `cbcdf3dc2de2d8c9d5411831575687f9483feae2`
- `tools/Artifacts/EvidenceLedger.psm1` → `6a73dc9f1ccfa96749b275abcf18aae7c839dc6f`
- `tools/Artifacts/ArtifactApplication.psm1` → `b99bb04ad1f46b366fe6debd8c6cfa4f56f2a885`

共有checkoutは`D:/repositories/unity/SampleGameForOneStarMakerFramework`、branch `develop`、HEADはbase。tracked/staged差分なし。untrackedのstatusにも表示差分なし。ただし既存`artifacts/route-proof-phase-c-8c1793e/live-run/`は権限拒否で列挙できず、内部の不在・cleanは主張しない。同checkoutを使う別chatがあり、使用権を独占と扱わずbranch切替・差分変更・退避・破棄を行わない。

専用worktreeは`C:/Users/void/.codex/worktrees/artifact-offline-phase-a-8865/SampleGameForOneStarMakerFramework`。固定baseから作成し、専用branchへ切替。開始時差分なし。今回の書込みは本HANDOFFとprivate/ignoredのA2原記録だけとし、実装・設定・Evidence送信・実Buildは行わない。完了HANDOFFを復活させない。

### 到達点と保持の正本

PR #96、#104、#105は完了済み。Evidence first useの判定済みimplementation headは`896eaea8a912248333704c80549d46ecf20c02c6`。#104 mergeは`5f2e5522083aff7e00034ab3552c00b92bcc538e`、#105 merge/baseは`c0f7d6e…`。文書/merge commitを実装判定headにしない。Cloud・実Buildの未成立はfirst useの未完了条件ではない。

保持期限の正本は[PR #104](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/104)が参照するappend-only record:
`C:/Users/void/AppData/Local/OneStarMaker/Artifacts/evidence-retention/8fe65074daf5379c4a311ec5166b21ff/retention.json`。
原bytesを読取り、SHA-256 `70032a5208f153d88f89f0470d2dcefd5ce9508c645871a2d0f456f89f0eba40`をPR台帳と照合した。owner closeは2026-10-07 08:17:25.6469389 JST、保存義務の下限は2026-11-06 08:17:25.6469389 JST（以後も参照中は保持）。元2ledgerのclose欄はnullのまま。

区別する三つの期限:

- 原観察/config: 最大24hの有効区間。期限後の同policy identityの再観察、新config、新immutable取得案内/期待hashはREADMEの既存経路で足りる。旧案内を更新しない。これはserver lock延長ではない。
- server lock: PUT-intent+30日の有限保護下限。E1 `2026-11-05T22:13:42.5023602Z`（11月6日07:13:42 JST）、E2 `2026-11-05T22:16:35.3325043Z`（同07:16:35 JST）。削除期限でも保存義務全期間の保証でもない。
- owner closeによる保存義務: 上記retention record+参照中。age lock終了でも無条件に読取りを拒否したり削除を許可したりしない。policy変更・server保護延長/移行は別Phase Aで扱う。有限lockと義務の差は既知リスクとして残す。

今回R2管理画面の再観察、実fetch、再PUT、DELETE、pruneは未実行。保持recordの読取照合は設定変更や保存義務の更新ではない。

### 最小候補比較

- **保持延長・期限後の取得:** 現在の不足はfinite server lockがclose+30日/長期参照の全期間を覆わないこと。観察期限後の同identity取得経路は実装済み。効果は将来の保護継続、依存はownerのpolicy判断・全適用rule/lifecycle再観察。期限は最早lock下限到来の11月6日07:13 JSTより前の判断。想定範囲は保護方針と運用/設定で、config更新コマンドを重複実装しない。今スライスには含めない。
- **offlineテスト可搬性（推奨）:** `reader-hash` / `handoff-identity-substitution`がprivate固定E1 sourceを読み、Git-only環境で34件suiteを再現できない。効果は所有者の実Evidenceを配布せずに別checkoutで同じreader拒否を検証できること。依存はWindows ownerのDPAPI/ACL、PowerShell7、.NET build dependencies。設定・Cloud・実Buildは不要。期限は次回Artifacts実装判定の前、本sliceは10月21日。範囲は2件のsetup、test fixture、関連runner選択、README。
- **ローカルBuildSystem接続:** 不足は既存出力とArtifact CLI間の接続/運用契約。効果はbuild成果物の固定受け渡し。先行依存はbackend結果の境界と実Build file集合/保存先の個別承認、容量/multipart/費用の測定。固定された緊急期限なし、program期限12月26日または着手revision。範囲は外部CLI/backend adapter/Artifact呼出しで、今回の2件より広い。
- **Cloud限定アクセス:** 不足はplatform別egress・runtime grant・無人更新・read/write/閲覧経路。効果はremote reviewer/producer利用。先行依存はplatform管理権限、非露出のgrant delivery検証。期限はCloud担当を必要とする作業開始前、program期限12月26日または着手revision。範囲はplatformアクセス/adapter/運用で、ローカルfirst useを待たせない。

推奨理由: 今の具体的な再現不能2件を、実Evidence/秘密/保護policyへ触れずに解消でき、後続のレビューに即効性がある。同じ残件見出しにある成功run重複抑止・一般エラー文言・inspect/pruneは別の変更理由なので同梱しない。保持保護の判断期限は別ownerへ明示して先送りの無期限化を避ける。

## 2. A1 — 意思決定と受け入れ境界

**答える問い:** Windows同一ユーザーのGit checkoutと非秘密dummy生成だけで、既存2件のreader hash/identity拒否とclose recordのassertionを、本番の固定E1/E2選択制約を緩めず再現できるか。

GOの最低条件と詳細:

- **M1 入力独立:** 対象2件はprivate source/原画像/ledger/取得copyへアクセスせず、一時root内に非秘密dummyを生成する。skip、expected-failure、件数削減を使わない。全7suiteの既存119 caseは保持する。非Windows成功やダミーACLをWindows実測へ読み替えない。
- **M2 元の検出力:** 両caseでvalid E1/E2 pairの案内が成功し、prompt hash違いは拒否、正しい案内hashでもpackage identityを差替えた案内は拒否。close recordは隔離rootへ生成され重複は拒否。E1 13+manifest+selection receiptの15entry、E2 4entry、source/outer hash、base/head、entrySet/openPathsを実際の生成bytesと照合する。dummy生成を実Evidence閲覧/送信成功の証拠へ扱わない。
- **M3 本番制約不変:** `tools/Artifacts/tests/`以外のcode、公開CLI、production E1 root/fixed bytes/hash/manifest、E2 HEAD、policy/ACL/retention、資格情報を変更しない。追加case `dummy-e1-selection-rejected`で任意dummy rootによる`Invoke-EvidencePublish`がnetwork到達前にfailed/no ledgerを返すことを確認する。通常suite・本番selector拒否確認ではE1定数をoverrideしない。限定例外は、対象reader 2件だけを選ぶ隔離processの`-MissingE1SourceProbe`でE1Rootのみを未作成sentinelへ一時差替えすること。E1Fixed/ManifestHash/validatorは変更せず、selectorはこのprobeで呼ばない。probe結果を本番selector成立の証拠に数えない。
- **M4 判定可能な証拠:** 最終headで全7suite（既存119+上記1件=想定120件）をskipなしで実行し、registered/selected/executed/failedの名前と件数、3 .NET buildのexit/raw、PowerShell parse、contract/docs audit、diffcheckとproduction-code無差分を固定する。新規fresh checkout/processの対象suite成功と入力がdummyだけである根拠を同headで残す。

GOはM1–M4全達成、未確認や0件を成功へ読み替えない。NO-GOは最低条件違反/検証不能。CONDITIONAL ACCEPTは採用しない。最低条件達成かつ致命的反証なしで終了し、残件へ実装を追加しない。

対象外: live R2/synthetic publishの再実行、実Evidence送信・再送・上書き/削除、policy移行/保持延長、資格情報操作、Cloud、実Build/Unity/H2d/H3、別hostの鍵移行、成功run全体の重複抑止、一般文言改善、inspect/prune、Unity native終了stall再調査。

停止規則: tests外code・新公開API・policy緩和・private source配布・新規file集合のR2送信が必要ならBを止めAへ戻す。auth/ACL/環境拒否は非秘密の未確認を記録し、stable osmの再入力/TokenDelete/設定緩和で迂回しない。新しい不確実性はM1–M4/常時契約違反根拠の有無で現欠陥と後続入力へ分類する。

## 3. 責務・依存・所有者・寿命

- `tools/Artifacts/tests/ArtifactEvidence.Tests.ps1`（現377行、予想+20〜50/一部置換）: assertionとfake transport/資格情報/pathsの隔離を所有するtest runner。2件だけprivate E1 publish setupをdummy prepared package setupへ置換、`-Case`選択（comma区切りのscalar文字列、既定`*`は全件）とResultPathのselected/executed集計を同runnerへ追加し、未知case/0件は非成功にする。`-MissingE1SourceProbe`はreader 2件の完全一致選択だけを許すtest-only switch。他case/全件/negativeと併用なら実行前に拒否する。production validatorをmockせずreader/commit/closeの実関数を通す。test rootとhookは1process寿命、finallyで解除する。既存のpolicy/selection/障害注入を移動せず、構成理由を増やさない。
- 新規 `tools/Artifacts/tests/EvidenceReaderFixture.ps1`（0行、予想100〜160行）: reader test専用のdummy provenance/package生成。13個の非秘密text/JSON、source manifest、selection receiptを組み立て、同bytes/hashの既存PackageIO/CreateVerifiedとprepared transfer入力を返す。source root、operation rootは呼出しtestが所有し、helperは独自の永続root/credential/network/validatorを持たない。これはE1 production selectorを経由しないreader/ledger unit setupであることを明記する。
- 依存: runner → fixture → 既存Packaging/ArtifactApplication prepared契約 → fake transport + dummy CredentialStore; runner → 既存EvidenceLedger（commit/reader/close）。実コードのprepared transferとcommitでprovenanceを確定し、handwritten成功ledgerのみでpositiveを作らない。fixtureはselection/lock/readerロジックを複製しない。2件の対象はreader hash/identityであり、本番E1 publish成立の再検証ではない。追加negativeで入口の閉じた境界を保持する。
- `tools/Artifacts/README.md`（161行、予想+3〜8）: 検証後の再現条件とdummyが証明しない範囲だけをharvest。旧119件の実績を新headの120件実績へ上書きしない。
- 本HANDOFF（Phase A）とprogramの残件owner/選択状況の小更新（D時）以外の設計文書は増やさない。Unity/asmdef/Game→Frameworkに新dependencyはない。test補助は既存tests直下へ置き、Helpers/Managers folderやprovider abstractionを作らない。

新helperの50%増加は新規test fixtureの生成責務によるもので、そのためだけに分割しない。ArtifactApplicationは既に522行だが変更対象外。runnerが500行/3責務へ増える見込みならA2の配置を再確認し、任意のロジックを足して行数だけ減らさない。

## 4. 実装と検証の計画（未着手）

人間のA3承認後に、fixture→2件置換/negative/selector→同head検証→READMEの順で進める。fixtureのpackage/selection receiptは既存schemaのdummy値で生成し、base/headとopenPathsの形は既存readerの閉じた契約を使う。本番fixed E1 bytes/hashをdummy値に置換しない。

検証環境: 所有Windowsユーザー、PowerShell7、.NET SDKとnet8.0 dependencies、Git。A1で`pwsh 7.6.5`/`.NET SDK 10.0.401`とsourceのcsproj/入口存在を確認した。新worktreeに生成DLLがないことは正常で、3 .NET projectをbuildして所定pathへ置く。依存restore/Windows ACL・DPAPI実行の成立はこの新checkoutでは未確認、初回確認は承認後のC担当。失敗時は環境とcode欠陥を区別し、実鍵やlive R2で補完しない。

差し戻し中の起点（`-Case`はこのsliceで追加予定、現baseには無い）:

```powershell
pwsh -NoProfile -File tools/Artifacts/tests/ArtifactEvidence.Tests.ps1 -Case reader-hash,handoff-identity-substitution,dummy-e1-selection-rejected -ResultPath <new-private-case-result.json>
```

影響がsetup/集計まで及ぶ場合はArtifactEvidence全suiteを確認する。prepared/packagingが疑わしい場合は既存ArtifactPackage/Transferを根拠付きで選ぶ。修正中に全7suiteや重いUnityを反復しない。

判定C必須（blocker解消後の最終implementation head）:

```powershell
dotnet build tools/Artifacts/Probe/R2RouteTransport.csproj -c Release -o tools/Artifacts/Probe/artifacts/route-transport
dotnet build tools/Artifacts/Packaging/ArtifactPackaging.csproj -c Release -o tools/Artifacts/Packaging/artifacts/package
dotnet build tools/Artifacts/Transport/R2ArtifactTransport.csproj -c Release -o tools/Artifacts/Transport/artifacts/transport
pwsh -NoProfile -File tools/Artifacts/tests/Credentials.Tests.ps1 -ResultPath <private-Credentials.json>
pwsh -NoProfile -File tools/Artifacts/tests/RouteProof.Tests.ps1 -ResultPath <private-RouteProof.json>
pwsh -NoProfile -File tools/Artifacts/tests/R2RouteTransport.Tests.ps1 -ResultPath <private-R2RouteTransport.json>
pwsh -NoProfile -File tools/Artifacts/tests/ArtifactPackage.Tests.ps1 -ResultPath <private-ArtifactPackage.json>
pwsh -NoProfile -File tools/Artifacts/tests/ArtifactTransfer.Tests.ps1 -ResultPath <private-ArtifactTransfer.json>
pwsh -NoProfile -File tools/Artifacts/tests/ArtifactRotation.Tests.ps1 -ResultPath <private-ArtifactRotation.json>
pwsh -NoProfile -File tools/Artifacts/tests/ArtifactEvidence.Tests.ps1 -ResultPath <private-ArtifactEvidence.json>
pwsh -NoProfile -File tools/contract-audit.ps1
pwsh -NoProfile -File tools/docs-audit.ps1
git diff --check
```

全EditMode回帰の適用除外（承認済み）: production/Unity/BuildSystem codeは無差分、test fixture/runnerのPowerShellのみ。代替は全7 offline suites、3 .NET builds、変更ps1のAST parse、base→headのproduction code無差分。Unity起動/実Build/PlayMode/native stallはこの問いへ証拠を追加しない。この除外と代替証拠をownerが今回のA3で採用した。

Git-only可搬性の証拠: C担当が最終headの別fresh worktreeから新processでbuild+ArtifactEvidence全suiteを実行する。テストはprivate E1をコピーせず生成dummyだけを読み、元E1固定rootを参照するcallが対象2件から除かれた完全diffを残す。全suiteと`dummy-e1-selection-rejected`は本番定数を差替えない通常processで行う。ownerホストには実E1が存在するため、fresh worktree成功だけを実E1不在の実測と呼ばない。不在時の経路は別の新規processで下記probeを実行する。テスト内でprivate moduleのE1Rootだけを隔離Temp root配下の未作成sentinelへ一時差替えし、開始前/終了後の不存在と対象2件の成功を観測する。production selector/他publish caseはこのprocessで実行しない。変更前定数をfinallyで戻し、fresh import時の本番定数不変も確認する。E1Fixed/ManifestHash/validatorはoverrideしない。sentinel操作はtest内部のみ、production API/CLIに注入経路を追加しない。新規source不在hostの実測の代替として、この限定不在経路を今回A3でownerが受容した。

```powershell
pwsh -NoProfile -File tools/Artifacts/tests/ArtifactEvidence.Tests.ps1 -Case reader-hash,handoff-identity-substitution -MissingE1SourceProbe -ResultPath <private-missing-E1-probe.json>
```

保存/受け渡し: Cが新private rootにA3 snapshot、所見を含まないB結果、同base/headの完全diff/stat/name-status、全raw stdout/stderr/exit・case JSON、DLL path/hash/MVID/deps、parse/auditを固定し、全file bytes/hash manifestを作る。原recordを維持、判定後実装head変更では旧結果を流用しない。同WindowsユーザーのC/C′は別processでmanifest期待hash→全entry検証→raw閲覧する。C/C′開始前に同bundleを渡せることを確認する。

この新file集合はE1/E2のR2事前承認対象ではない。ローカルprivate証拠固定だけを提案し、送信が必要なら集合/保存先の個別承認と別Phase Aへ返す。C所見は別rootに隔離する。承認後のA3仕様snapshotは本書§0〜5の確定仕様から生成し、§6のA2所見/採否/主担当の疑念を含めない。snapshotの対象commit、抽出範囲、原bytes/hash/時刻を固定する。C′へ可変HANDOFF全文/PR結論や誘導的所見を渡さない。C′は同判定rawを再実行せず監査できる。画像/人間目視を最低条件に持ち込まない。

## 5. 既存Evidenceと後続owner

既存E1/E2、台帳、取得案内、原画像、原source、失敗記録、intent、snapshot、locked witness、受信copyを保持する。stable osmはCLI内部だけで使い、今回は実鍵storeにアクセスしない。dummy test storeは隔離Temp rootだけ。TokenDelete・新token・rotation・再入力・秘密提示・暗号文配布を要求しない。有限lockを無期限と主張せず、保持正本recordは変更しない。設定延長や削除は設計承認とも別のowner判断である。

後続は問い・owner・期限/着手条件のみ（詳細設計は今行わない）:

- 保持延長: 保存義務を覆うserver保護をどう継続するか。owner=OSM maintainer/storage owner。判断期限=2026-10-30 JST（今回A3で受容、設定変更の承認ではない）、実際の必要操作は最早lock下限2026-11-06 07:13:42 JSTより前。延長を自動実行しない。期限後同policy再観察取得は既存README運用で、policy変更が必要と判明したときだけ新Phase A。
- ローカルBuild接続: 最小のbackend結果と明示出力集合は何か。owner=OSM maintainer/BuildSystem担当。実Buildの必要性と入力集合・保存先をownerが承認してから着手、program期限2026-12-26または置換revision。
- Cloud限定アクセス: どのplatform/roleのegress/grant/read/write/閲覧を先に必要とするか。owner=OSM maintainer/platform管理者。Cloud担当を使う作業より前、program期限2026-12-26または置換revision。
- 操作性の別問い（重複run抑止・一般エラー文言・inspect/prune）: 各々に利用上の必要性が生じたときPhase A。owner=OSM maintainer、program期限2026-12-26または置換revision。同残件見出しのため同梱しない。

## 6. Phase A承認とPhase B引渡し

2026-10-07 JST、ownerが「phasea3凍結でよい phseC/C′まで進めて」と明示承認した。§0〜5の範囲、最低条件M1–M4、限定reader probe、全7 offline suite/3 .NET build/Unity回帰除外、従来HANDOFF方式、保持ownerと2026-10-30の判断期限を凍結した。BとC/C′までを許可し、D・push・PR・mergeは未承認。

A3仕様snapshotは§0のpath/hashを正とする。担当ごとの所見は固定判定入力の外に保持し、レビュー記録commitで引き継ぐ。implementation headとレビュー記録headを分ける。仕様と責務の変更はない。

Phase B担当は新規sessionの`/root/phase_b_portability`、起動tool指定model `gpt-6-sol`。CはBと異なるmodelの新規session、C′はA/B/C未関与でB/C両者と異なるmodelの新規sessionを使う。同OpenAI/GPT系列という独立性の限界は保持する。

Phase B実装結果は未生成。

## 7. Phase C

未実施

## 8. Phase C′

未実施

## 9. Phase D

未実施
