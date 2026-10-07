# Artifact Evidence offlineテスト可搬性

## 0. メタデータ

- type: slice
- status: C/C′完了 — A3凍結済み、B完了、判定C GO・blind C′ GOを個別記録。D未実施・未承認。
- revision: portability-a3-frozen-r2
- branch: `codex/artifact-offline-portability-phase-a`
- implementation base commit: `c0f7d6e57ba1fb918863403f6b0077b4ea4c3509`（2026-10-07 JST、origin/developをls-remoteで照合）
- implementation head commit: `19720ed7c6065ffcc55ee2643f811462025a940d`。計画/承認/結果追記commitと区別する。
- risk: normal（tests限定。本番selection/protectionの変更が必要ならAへ戻す）
- owner: OSM maintainer。Phase A主担当はこのCodex session。
- created: 2026-10-07
- expires: 2026-10-21または置換revision。programの残件期限2026-12-26とは別。
- harvest to: `tools/Artifacts/README.md` のoffline再現条件。D完了時に本HANDOFFを削除する。
- Phase A snapshot: `C:/Users/void/AppData/Local/OneStarMaker/Artifacts/transfers/907102fae5704867b0508d8b9b8f78e1/A3.md`。source commit `72de8cc889df40da891409c00d9033467bead3b2` の§0〜5、generated at `2026-10-07T00:32:50.6248551Z`、SHA-256 `46f01c8aba7be6cbb34c5f96458ccaa066ab85f46098dd1ceebfb360bd17a356`。A2初稿と承認仕様は区別する。
- Phase B result snapshot / evidence bundle / C′ blind bundle: §6/7/8のpath・時刻・hashを正とする。C/C′は同105-file固定bundleを使用。
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

専用worktreeは`C:/Users/void/.codex/worktrees/artifact-offline-phase-a-8865/SampleGameForOneStarMakerFramework`。固定baseから作成し、専用branchへ切替。開始時差分なし。Phase A時点の書込みは本HANDOFFとprivate/ignoredのA2原記録だけで、実装・設定・Evidence送信・実Buildは行っていない。A3承認後の実装/検証は§6〜8に記録する。完了HANDOFFを復活させない。

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

## 4. A3で凍結した実装と検証の計画（実績は§6〜8）

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

## 6. 独立A2の実績、採否とA3凍結

### 共通の固定レビュー入力

A0/A1 r1はcommit `4e61bf53c962bfc3ed032acce6607ad3d2fee86e`、本HANDOFFのGit blob原bytes 21921、SHA-256 `4a4fbc79ec2b5f161451334c356b3a5489a628d834bb941716d2260960f942f1`。両担当とも`git show`の固定版とbase `c0f7d6e…`の同文書/codeを読み、bytes/hash一致を本人が確認した。互いの所見は渡していない。主担当の自己レビューはA2に数えない。

- architecture: `/root/a2_architecture`、起動tool指定model `gpt-6-astra`、新規subagent・A0/A1非関与。architecture-gatesに従い責務/フォルダ/依存/所有者/寿命/テスト境界を確認し、prepared→commit/reader/closeに実装上の余地あり、新公開API/Unity依存不要と評価。findingは下記1件。本人申告はGPT-6系でvariantは未確認（model指定は起動tool引数、実variantの独立な実測ではない）。
- 検証/運用: `/root/a2_verification`、起動tool指定model `gpt-6-sol`、新規subagent・A0/A1非関与。M1–M4、case selector/全suite、prepared/ledgerの接続、Windows限定、三期限、既存fetch再利用、承認境界を確認。承認案を阻害するfindingなし。本人申告はGPT-6系でvariantは未確認。

実際に二担当が独立にレビューした結果である。別variantを指定したが、実行variantの一致/異vendor多様性は確認済みと主張しない。同OpenAI/GPT系列という独立性制約を人間に提示する。担当のモデル多様性が必要と判断された場合は、この入力に未関与の別モデル/人間の追加A2を依頼し、実施前に完了扱いしない。

受領テキストのローカル保存copy（Markdown装飾を正規化、tool実行rawではない）は本worktreeの`tools/Artifacts/artifacts/phase-a-offline-portability/a2-4e61bf53/`。CreateNewで新規保存、Git/R2へ送信していない。architecture `architecture-received.txt` SHA-256 `d925b775f7481e179b325eb9bd9857cddc26ddda3a665b0d03d9fd332b98166e`、verification `verification-received.txt` SHA-256 `eb66866d0f50eb2fd6b4ddfd710f32f024147d9690a91f649a0b7555ed7b918a`。この小台帳から受領copyを照合できる。

### 指摘の採否案

- **A2-ARCH-01 / P2 / semantic / unique:** M3（r1行80）がE1定数overrideを禁じ、§4（r1行133）がsentinel差替えを要求して矛盾。現在の承認案を阻害する計画上の欠陥。**採用案:** 本番sourceの定数/selector制約を変えないことをM3へ明記した上で、reader 2件だけの隔離processでE1Root sentinelを使う限定例外を具体化。`-MissingE1SourceProbe`はその2件の完全一致選択だけを許可し、本番selectorのnegative/全suiteは通常processで行う。E1Fixed/ManifestHash/validatorは一切overrideしない。採用理由は不在経路の検証と本番入口の非緩和を混同しないため。今回のowner承認で採用。
- 検証/運用A2: findingなし。runtime成功との主張へ拡張せず、未実行項目をA3未確認として残す。
- 不採用案: なし。保留finding: なし。候補比較で後続へ移した問いはA2 findingとして創作しない。


A2-ARCH-01の限定追認: 同architecture担当が修正版commit ceef01b76ffca85022c2ff4e444c9022925c9b13（本書Git blob SHA-256 a16f6881c56b803374176442c6360271735dcd9884336bdc0a33fdc540324743）の§0〜5だけを確認し、M3/runner/通常processとprobeの分離が一致し、計画本文上は解消したと回答した。他A2情報のある§6は読んでいない。新しい独立A2や実行確認に数えない。受領copyは同rootのarchitecture-recheck-received.txt、SHA-256 17b7411aaa2e8caf9cd8d383ea992ed03ada683ce4da1da0c553b25591fc5896。今回のowner承認で採用。

### 担当選定と独立性

A0/A1/A3統合主担当はCodex（OpenAI/GPT-6、variant未実測）。B/CはPhase開始時に選び、CはBと異なるmodel、新規session。C′はA/B/Cに未関与の新規担当を予約する。現在利用可能な候補は今回使っていない`gpt-5.6-sol`または未関与の人間で、将来の固定割当ではない。AI C′は実施時にB/C両者と異なるmodelが確認できることを最低条件とする。確認できなければ独立監査未実施として返し、人間へ確認を求める。人間担当もblind入力に対する本人の範囲/所見/判定を必要とし、AIが先にGOを記録しない。

同GPT系列だけになる場合は強化独立性の制約と残存リスクを記録して人間が受容する。A2二担当をそのままC′へ流用しない。C′に残す別系列/vendor候補を今回消費していない。この段落はA3案時点の担当選定。今回の実担当・確認結果は§7/8に記録する。

### 人間へ提示したA3判断（下記の明示承認で採用）

1. 推奨スライスはoffline可搬性2件のみ。M1–M4、test fixture/prepared境界、negative追加、tests内限定sentinel probeを承認するか。
2. 同じ最終headで全7 offline suites（既存119+追加1、想定120）・3 .NET builds・parse/audit/production無差分を判定必須とし、全EditMode回帰を理由付きで除外するか。不在hostの実測と限定sentinel証拠を区別した上で、後者を今回の不在経路証拠として受け入れるか。
3. 従来tracked HANDOFF方式を使い、H1/CURRENT移行を同梱しないか。同OpenAI/GPT系列かつ実variant未確認という今回のA2制約を受容するか、未関与の追加A2を必要とするか。
4. 保持延長の判断ownerをOSM maintainer/storage owner、判断期限案を2026-10-30 JSTとするか。延長方法/設定変更の承認は別Phase Aへ送り、今回承認へ含めない。
5. C′は未関与担当、B/Cとmodel相違確認、新session、同判定rawのblind bundleという条件で選ぶか。通常Artifact操作の事前承認はこの設計承認/Phase移行の代わりではない。

A3時点で未確認だったfixture/probe/selector、fresh checkoutのrestore/Windows ACL/DPAPI、3 builds/全120件、固定証拠の実受渡しは§7/8の実績で確認した。live設定の現在値とserver保護延長方式は未確認の後続の問いで、このoffline sliceのGO条件へ足さない。

A3は下記owner承認で凍結済み。BからC/C′まで進める。D・マージは未承認。文書監査/契約監査成功をoffline判定suite成功に読み替えない。

### A3の人間承認記録

2026-10-07 JST、このchatのownerが「phasea3凍結でよい phseC/C′まで進めて」と明示承認した。承認対象は計画commit `a18cf5d4dd257fc1cd528cacb07444f4b9362647`（Git blob原bytes SHA-256 `3958ac7cf751e766b271e19a28a1a057a08aa6dda0ed14e20d40d276c4767951`）のA3案。上記1〜5の採否案を受容し、A2-ARCH-01採用、限定reader probe、全7suite/3 .NET build/Unity回帰除外、従来HANDOFF方式、同GPT系列/variant未実測のA2制約、保持ownerと判断期限を凍結した。未関与C′の選定条件は維持する。

Phase BとC/C′を許可。実Evidence/設定/保持変更/Cloud/実Buildは範囲外のまま。D・push・PR・mergeは今回の承認へ含めない。A3凍結仕様の変更が必要なら新revisionへ戻す。旧「提案・未承認」の記述はA2/A3提案時の履歴であり、この明示承認記録が現在の状態である。

### Phase B実績と固定入力

B担当は新規sessionの`/root/phase_b_portability`、起動tool model `gpt-6-sol`（実variant未実測）。tests 2fileをcommit `850bc875c7c37468a7144456cdf059b78d9963b5`に固定。runnerは377→460行で、assertion/fake transport/隔離rootとhookの既存責務にscalar `-Case`と限定probe/結果集計を追加した。70行のfixtureはdummy provenance/package生成だけを所有する。13+manifest+receiptの15entryを実Packaging/prepared transfer/commit/reader/closeへ通す。既存34caseを保持し、dummy E1 selector拒否1件を追加。production code/API/policy/credentials/retention無差分。新fixtureの生成責務をrunner assertionから分け、500行未満のrunnerを行数だけで再分割しない。

主担当がREADMEの再現条件を更新し、整理前implementation headを`527a46c7f6dd323698ee5be080704781c7532e7e`に固定。その後ownerのPR公開指示に従い、未公開履歴を同一Git treeの`19720ed7c6065ffcc55ee2643f811462025a940d`へまとめた。最終implementation headは`19720ed7…`。これはC/C′共通の実装判定headで、以下の結果追記commitはimplementation headを更新しない。Bで両ps1 ASTエラー0、contract audit 608file errors/warnings0、diffcheck成功。Bでは3 .NET build/offline実行を行わず、Cの初回確認へ引き渡した。

B結果snapshotは`C:/Users/void/AppData/Local/OneStarMaker/Artifacts/transfers/818b4aadcddd49ae9eff9e69c5271f73/B_RESULT.md`、generated `2026-10-07T03:21:02.3142633+00:00`、SHA-256 `f2aeac7b1a9aa94b15cb42eda147c5804c549a526ecb5c31bf4b082fbde1f0d9`。所見なしで実装責務・変更範囲・B未実行を記録する。A2採否は判定前に管理recordへ隔離し、C′完了後この結果追記で元記録から復元した。A3仕様snapshotと完全diffの判定入力にはA2/C所見を入れていない。

## 7. Phase C — 公開前の固定head再判定

**判定C: GO**。新規session `/root/pr_c_portability`、tool指定model `gpt-6-astra`。Bの`gpt-6-sol`と異なるmodel、新規固定入力から構造→凍結条件→生結果を評価。実runtime variantは独立実測していない。同OpenAI/GPT系列の制約は残す。

ownerの「pr出して欲しい」でpush/PR公開を許可。未公開の9commitを実装1commit+本結果記録1commitへ整理するため、旧実装treeとbyte同一（Git tree `d5eb0a67f30f4acc61029fff12cdf000e6f819f3`）の新headを作成した。base=`c0f7d6e57ba1fb918863403f6b0077b4ea4c3509`、判定head=`19720ed7c6065ffcc55ee2643f811462025a940d`。旧527a46c7のrun/C/C′を新headへ流用せず、全判定必須と新規C/C′を取り直した。旧commit/実績はローカルbranch `codex/artifact-offline-portability-before-publish`とprivate保存先に保持し、公開履歴へ中間commitを積まない。

### 共通固定入力と受渡し

判定C/C′開始前に固定したbundle rootは`C:/Users/void/AppData/Local/OneStarMaker/Artifacts/transfers/62fb77e8a6424c2eae989e5a70e16018`。generated `2026-10-07T12:26:05.4299141+09:00`、`manifest.json` SHA-256 `d0d0f1161bf020d709e6444272a012bb413dc924ae5bda89207d385f103dbb02`。82payload+manifest=83入力file。A3/B snapshot、同base/headの完全diff/stat/name-status、3build/全suite/probeの全stdout/stderr/exit・command/time/cwd/head/case JSON、判定前機械検査、DLL情報だけを収録。Cの所見は別rootに隔離し、C′へ渡していない。

受取copyは`C:/Users/void/AppData/Local/OneStarMaker/Artifacts/transfers/46af9ae9764a423c9c763064bcbfa6cc`。別PowerShell processで期待manifest hash→全82 entryのpath/bytes/SHA-256→必須56file可読を確認、完了`2026-10-07T03:27:07.3660130Z`。receiptは原実行root `C:/Users/void/AppData/Local/OneStarMaker/Artifacts/transfers/e6dcc3bebc554e1db12559842b5d75bd/transfer-verification.json`。原root/受取copy/旧証拠は参照中保持し、D close後30日以上の保存義務を満たすまで清掃しない。新集合のR2/Git payload送信は行っていない。取得は同Windowsユーザーの正規承認経路に限り、別host/Cloudの可読性は保証しない。

### 必須検証と条件評価

新fresh worktree `C:/Users/void/.codex/worktrees/artifact-portability-pr-verify/SampleGameForOneStarMakerFramework`、detached19720ed7、開始時clean・Artifacts DLL0から実行。正確な引数/日時/exit/cwd/headはbundleの各`*.execution.json`。コマンドは§4の3 .NET build、全7suite（各新private ResultPath）、contract/docs/diffcheckと別process reader完全一致probeを用いた。

- 3 .NET build: 各exit0、warning/error0。Git入力からProbe/Packaging/Transportをrestore/buildし、旧DLLはコピーしていない。
- 全7suite: Credentials22、RouteProof26、R2RouteTransport7、ArtifactPackage9、ArtifactTransfer10、ArtifactRotation11、ArtifactEvidence35。計120/120、failed0、skipなし。baseの119件名を保持し追加は`dummy-e1-selection-rejected`1件のみ、registered/selected/executedの名前と件数一致。
- Evidence全35件は通常process、本番定数差替えなし。別process `reader-hash,handoff-identity-substitution -MissingE1SourceProbe`は2/2、failed0。sentinel前後不存在/E1Root復元/fresh importのroot/fixed/manifest不変を確認。
- Artifacts26 PS file ASTエラー0。contract608file errors/warnings0・check8 not-applicable（H1未適用）、docs111file errors/warnings0、diffcheck0、許可4file外production code無差分、final status clean。
- 7 DLLのpath/hash/MVID/referencesと3deps、主要3DLL実ロードの前後hash/MVID一致を記録。raw audit stdoutはCP932と区別し、JSON metadataはUTF-8。
- 補助collectorのpath除外による0file収集と誤った件数guardを実際の26file列挙で補正。受取copyの必須一覧連結ミスは同copyへの明示List確認で補正。原記録を実行rootに保持し、製品suite/buildは反復せず、必要な証拠収録だけ修正した。

責務マップに適合。runner460行は選択/assertion/fake transport/process隔離を所有し、70行fixtureはdummy provenance/package生成だけを所有。既存Packaging/prepared/ledger/reader/closeへ依存し、本番policyロジックを複製していない。新公開API/状態/所有者/寿命/asmdef/Unity/Game→Framework依存なし。500行未満のrunnerと凝集したfixtureを行数だけで追加分割しない。

M1–M4すべてPASS。2 reader caseはprivate E1なしの生成dummyを使い、実prepared→commit/reader/closeにより15/4entry、版/hash/entrySet/openPathsを照合。誤prompt hash、正しい案内hashでのidentity差替え、duplicate closeを拒否。本番dummy E1 selector negativeはfailed/no ledger/network0。現在の問いを阻害するfindingなし、新規後続findingなし、判定必須未実行なし。

判定C recordはblind外の`C:/Users/void/AppData/Local/OneStarMaker/Artifacts/transfers/7522858d606e441fbd9f4940f06275d1/C_RESULT.json`、SHA-256 `21e94c1f893ba18eed0bb23f5258a9e6af699a6563fb1adb230be2fd72909e6b`、completed `2026-10-07T03:28:25.0771305Z`。C′の所見・結論を読まずに本人がGOを判定した。

## 8. Phase C′ — 公開前のblind独立監査

**C′判定: GO**。新規未関与session `/root/pr_cprime_portability`、tool指定model `gpt-5.6-sol`。B=`gpt-6-sol`/C=`gpt-6-astra`と異なるmodel ID、新規session、同§7のblind bundleでAI最低条件を満たした。C所見/結論、過去history/旧証拠、可変HANDOFFは読んでいない。同OpenAI/GPT系列/runtime variant未実測の強化独立性制約は残す。

当初の新規persistent Artifact operationへの83-file copy/ACL/receipt作成はauto-reviewに拒否された。「新規file集合/保存拡大はREADMEの事前承認外でowner明示承認が必要」という実行環境の判断に従い、複製を実行せず迂回していない。代替として既存C受取copyを読み取り専用で使う操作が正規require_escalatedで承認された。

本人の別PowerShell processで、既存`46af9ae…`の期待manifest hash一致と82payload全path/bytes/SHA-256/可読性、合計353,570bytesを確認。物理84fileは82payload+manifest+管理用operation.lock。新規directory/file/operation/receipt/ACL変更はなく、新規receipt path/hashはない。受渡コピーの独立process照合を満たし、拒否された保存拡大を別保存先で実行したものではない。

本人がA3/B/full diffと新headの必須raw・関連sourceを監査し、M1–M4/構造/凍結失敗経路をPASS、120/120・probe2/2・3build・26parse/audits/production無差分を確認。現在の問いを阻害する欠陥なし、新規後続入力なし。監査結果はこのchatの本人受領reportを本欄へ記録し、個別private所見fileを新規保存していない。主担当の自己レビューはC′に数えない。

未確認/残存リスク: C′はsuite/buildを再実行せず、hash検証済rawを監査した（凍結条件では再実行必須でない）。source不在は同owner hostの限定sentinel代替で、別host/別Windowsユーザー不在の実測ではない。実R2/実credentials/既存E1/E2/Unity/実Build/Cloudは対象外・未操作。dummyの成功を実送信/取得/原画像閲覧/無期限server保護へ広げない。Unity全EditModeはA3の明示適用除外、代替offline/build/parse/production無差分で評価した。

## 9. Phase DとPR公開

ownerの今回の指示でpush/PR公開を許可。C/C′本人の同head判定を個別記録したが、Dの突合・merge判断は未実施・未承認。program完了harvest/HANDOFF削除はD時に行うため今回は保持する。本結果記録commitはimplementation head `19720ed7c6065ffcc55ee2643f811462025a940d`を更新しない。公開履歴は実装と結果記録の2単位とし、公開headと判定headをPR本文で区別する。

結果追記後の監査はcontract errors/warnings0、docs errors0/warnings1、diffcheck成功。docsの警告はC/C′欄完了に対するharvest待ち通知。D未実施を理由に保持し、判定前bundleのdocs warnings0と区別する。保持延長のowner/判断期限2026-10-30、Build/Cloud/操作性の別着手条件は§5のまま。既存append-only retention record、元ledger/案内/原画像/失敗記録/locked witness/実資格情報/保持設定は変更していない。
