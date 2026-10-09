# 開発Build保持の選別policy

## 0. メタデータとA0入力固定

- type: `slice`
- status: `B` — A3 r2凍結済み。2026-10-10の人間指示「PhaseA3凍結としてPhaseC’まで進めて」により実装・C・C′へ進む。Phase Dは対象外。
- branch: `codex/artifact-build-retention-policy`
- planning base / implementation base commit: `2203dded470a3ac4e7a751f594f8f7110005576b`
- implementation head commit: Phase Bで確定する。
- risk: `normal`。内部の新規policy関数だけを設計し、削除・公開CLI・Unity APIには接続しない。
- owner: OSM maintainers / Artifact担当（本チャットのroot）
- created: 2026-10-10
- expires: 2026-11-10 または置換revision。完了時にharvestして削除する。
- harvest to: `tools/Artifacts/README.md` の実装済みpolicy・offline検証説明。
- task ID: `artifact-build-retention-policy`（この計画で予約）。`artifact-evidence-lifecycle` を流用しない。
- 担当worktree: `D:\repositories\unity\SampleGameForOneStarMakerFramework`
- 開始確認: `git worktree list`、`git branch --show-current`、`git status --short` を確認。開始branchはdevelop、追跡対象の差分と表示された未追跡変更はなし。生成物配下 `artifacts/route-proof-phase-c-8c1793e/live-run/` に一覧取得拒否警告あり。その領域の無変更・不存在までは証明せず、立ち入らない。
- `git fetch origin develop` 成功後のSHAを上記baseとし、そのcommitから専用branchを作成。既存branchをreset/stash/cleanしない。
- `D:\repositories\unity\OSM-verify` は開始時のworktree一覧に未掲載。他担当が用意する領域として扱い、存在確認・checkout・Unity・出力には触れない。積みPRのレビューは引き取らない。
- Phase A snapshot: A2入力の固定copyは `tools/Artifacts/artifacts/build-retention-planning/A1-r1.md`（Git外、生成UTC `2026-10-09T16:49:18.7375276Z`、SHA-256 `2ba55ad4180ed03749eae4c43d468319095ab364ba7b3533217b5e8d92d7e316`）。A2時の置き場所は `artifacts/artifact-build-retention-policy/A1-r1.md`、同bytes/hashで現在のignored領域へ移した。人間のA3承認後のsnapshotは未生成。
- A3凍結snapshot: `tools/Artifacts/artifacts/build-retention-planning/A3-r2-frozen.md`、生成UTC `2026-10-09T17:21:06.4901894Z`、SHA-256 `e3defbf34df92fbecda4d99ea697c2dbc0eee34d2a29f1797dfec5562c07be92`。本書の以後の進行記録と分け、仕様入力はこのcopyを使う。
- Phase B result / evidence bundle / C′ blind bundle のpath・時刻・hash: 未到達。
- Workflow start: 2026-10-09T17:21:13.0411927Z、当該task version=1、status=ok。C/C′完了をtask終了へ変換しない。

根拠は `AGENTS.md`、`osm-workflow` とそのphases-and-handoff/docs-policy/handoff-template/architecture-gates/review-evidence、`docs/README.md`、`BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` r6、`tools/Artifacts/README.md`。過去PR・旧証拠は再監査しない。PR #109のEvidence sliceは完了済みとし、30日保持・完了済みreset・#107実適用の撤回・旧reader非互換を維持する。

## 1. 目的と対象外

**問い:** 明示した1系列のBuild一覧snapshotと保持数Nから、成功publishと利用中保護を正しく扱い、保持対象・削除候補・保留理由を決定できるか。

初回依頼のA0/A1・独立A2・A3承認案提示は完了した。2026-10-10の後続指示により、提示済みr2の設計・採否・検証範囲を凍結し、純粋policyの実装、C、独立C′まで行う。実データのpublish/delete、Unity、PR確認側の作業は追加しない。

対象外はBuildの生成・実行・publish/fetch、catalog永続化、外部JSON schema、CLI接続、実DELETE、自動清掃、scheduler、利用状態の取得/更新、multipart・容量課金、Cloud/別host/provider/GUI、BuildSystem/Workflow/Harness改修、Evidence契約変更、R2設定・reset・鍵操作、無関係な整理。Nの既定値10はProgramの案のままとし、本policyに既定値・設定file・UIを作らない。

現況: `EvidenceRetentionPolicy.psm1`（37行）は`eligible/reasonCode`を返し、`EvidenceApplication.psm1`のinspectと`EvidenceCleanup.psm1`のdry-run/削除直前から呼ばれる。後者はguard内で再判定する。`EvidenceRetention.Tests.ps1`（32行、登録14case）は状態/時刻を入力して境界を確認する。Build用の件数policyと呼出箇所はまだない。Evidenceのtask終了時計やprotection形式をBuildへ持ち込まない。

## 2. 最低条件・入出力・受け入れ境界（A3承認案 r2）

### 進める最低条件

1. 成功publishだけをNに数え、失敗/未完了が既存成功を追い出さない。
2. 利用中成功BuildをN内の優先保持として扱い、削除可能な古い成功Buildから超過分を候補にする。保護だけの超過は破らず数値で返す。
3. 不明/不正を成功・削除可能と推測しない。順序・同点・不正Nが決定的であり、入力を変更しない。
4. policyがfile/R2/資格情報/Unity/実時計/永続storeへ触れず、非秘密のメモリfixtureだけで上記を独立検証できる。

### 最小の内部API案

`Get-BuildRetentionDecision -SeriesId <string> -Builds <object[]> -KeepCount <int|long>` を1関数だけexportする。PowerShellの暗黙型変換前に生値を検査するため、宣言上の強制castで文字列Nや文字列boolを許可しない。これはprocess内の呼出契約であり、JSON schema/codecではない。

- `SeriesId`: 空白だけではないstring。呼出側が選んだ1系列のopaque ID。大文字小文字を含めそのまま扱い、project/target/configuration/branchから生成・解釈しない。
- `Builds`: 非nullのmaterialized `object[]`（空配列可）。各要素は通常のデータpropertyだけを持つ`PSCustomObject`。呼出側が同一系列の一覧を確定して渡す。行にseriesを重複保持せず、policyは外部一覧の完全性や系列への所属を証明しない。実行中の入力同時変更・script property等の実行可能objectは契約外。
- 全行の必須fieldは `buildId`（空白だけでないstring、Ordinalで一意）、`publishState`（大小文字を区別した `succeeded | failed | incomplete`）。成功行だけ `publishedAt` と `inUse` を必須にする。余剰の通常データfieldは参照せず、外部schemaとして固定しない。
- `succeeded` は呼出側がpublish確定済みと判断した状態。policyは転送成功やreceiptを検証しない。成功行の `publishedAt` は成功確定の `DateTimeOffset` 値でOffset=0。file生成時刻でも受信時刻でもない。`failed/incomplete` はheldへ分類し、`publishedAt/inUse` を読まず、欠落/null/型不正/任意値でも成功集合の選別を止めない。
- 成功行の `inUse` はsnapshot時点の保護の結論で、実boolを要求する。期限・owner・lease・実時計をpolicyへ持ち込まない。不明ならbool falseへ変換せず、不明値を渡して判定を保留する。失敗/未完了の未知fieldを無視しても、それを成功や削除可能へ昇格させることはない。
- Nは実`Int32`または`Int64`の1以上。0、負数、null、string、bool、小数は拒否。上限はInt64の表現範囲であり、N個の領域確保はしない。0保持の運用は今回作らない。

返値は新規`PSCustomObject`。既存の`reasonCode`語彙形式を使い、真偽の`eligible`を実DELETE許可と誤解させないよう候補名を明記する。

- `status`: `evaluated | blocked`
- `reasonCode`: evaluatedなら `within-limit | candidates-found | protected-over-limit`、blockedなら下記の入力拒否理由。
- `retainedBuildIds`: 成功Buildの保持ID配列（0/1件でも配列）。
- `deleteCandidateBuildIds`: 成功Buildの削除候補ID配列（0/1件でも配列）。
- `heldBuilds`: `{ buildId, reasonCode }` の配列。既知の失敗は`publish-failed`、未完了は`publish-incomplete`。Nに数えず、削除候補にも入れない。これらの後始末は対象外。
- `excessCount`: evaluatedなら保持成功数がNを超える数（0以上のInt64）、blockedならnull（0と偽らない）。候補を除外した後に残る超過であり、削除実行済みを意味しない。

**選別に必要なsnapshotが不正なら全体保留**とする最小案。不正N、不正series、一覧型不正、行の不正/必須field欠落、未知publish状態、重複ID、不正成功時刻、成功行の不明inUseのどれかがあれば、status=blocked、3配列は空、excessCount=null。reasonCodeが一覧全体の保留理由であり、空配列を「保持不要」「削除済み」と解釈しない。1件ずつ部分判定する回復機構を作らず、次の確定snapshotを待つ。既知failed/incompleteの選別に不要なfieldは上記のとおり読まない。

複数不正時の優先順位は `invalid-keep-count` → `invalid-series-id` → `invalid-build-list` → `invalid-build-snapshot`。行内の詳細は最後の1codeへまとめ、入力位置や例外本文に依存した診断を返さない。同一の行集合なら入力配列を並べ替えても同じ結果になる。unknown状態を成功件数に読み替える余地をなくすため、blocked時は件数計算自体を確定しない。

### 選別と順序

1. 全入力を検査してから成功集合S、利用中成功集合P、利用中でない成功集合Uへ分ける。
2. 新しい順の全順序は `publishedAt.UtcTicks` 降順、同点は `buildId` の `StringComparer.Ordinal` 昇順。文化依存sort・入力順・実時計は使わない。大小文字の異なるIDは別IDとして扱う。
3. Pをすべて保持し、Uの新しい順に `max(0, N - |P|)` 件までを保持する。残りのUが候補。保持配列は上記の新しい順、候補配列はその逆順（時刻昇順・同点ID Ordinal降順）で返す。
4. `excessCount = max(0, |P| - N)`。excessCount>0なら`protected-over-limit`、それ以外で候補があれば`candidates-found`、なければ`within-limit`。保護超過時もUは候補になり得る。
5. heldBuildsはbuildIdのOrdinal昇順。入力配列・要素をin-place sort/書換えしない。出力はIDと新規recordだけで、入力object参照を返さない。

この選別は「最新N件に利用中を無条件追加」と異なる。Programの「利用中はN件には数える」を採用する。全候補は古い削除可能成功Buildから選ぶが、利用中を消して枠を空けない。

### 具体例と観測可能な受け入れ条件

以下のA/B/C/Dは時刻が新しい順、指定のない行は成功・非利用中。同じ系列`sample/windows/dev`を呼出側から渡す。各例で返却順、3集合の重複なし、成功行の保持/候補への全件割当を検査する。

- E1: N=2、A/B/C → retained=[A,B]、candidates=[C]、held=[]、excess=0。
- E2: N=2、A/B/C、Cだけ利用中 → retained=[A,C]、candidates=[B]、held=[]、excess=0。
- E3: E2に失敗F・未完了Gを追加 → 成功の結果はE2と同じ。held=[F:publish-failed,G:publish-incomplete]。F/GのpublishedAt/inUseが欠落・null・文字列・非bool・成功行より新しい任意時刻でも同じ結果。
- E4: N=2、A/B/Cすべて利用中、さらに古いDは非利用中 → retained=[A,B,C]、candidates=[D]、excess=1、protected-over-limit。利用中だけ3件の場合は同じ超過で候補=[]。
- E5: N=2、空 → すべて空・excess=0。Aだけ/ちょうどA,B/失敗Fだけも候補なし。成功がN未満ならすべて保持。
- E6: N=1、同じUTCのA/B → retained=[A]、candidates=[B]。N=1で同時刻A/B/Cならcandidates=[C,B]。大小文字や文化を変えてもOrdinal順を維持。
- E7: N=2、A/B/CのC.inUseがnull/文字列false、またはpublishStateがunknown → blocked、全配列空、excess=null、invalid-build-snapshot。未知行を無視して他行を削除候補にしない。
- E8: N=0/-1/2.5/文字列2/bool/null → blocked、invalid-keep-count。ID重複・ID欠落・成功時刻欠落/非UTC/文字列・行null・必須field欠落もblocked。Builds=nullはinvalid-build-list、空seriesはinvalid-series-id。複数不正時は上記優先順位。
- E9: N=Int64.MaxValue、A/B/C → 全保持。入力順の全置換・複数回呼出・異なるCurrentCultureで同一内容。同じsnapshotの全property/配列順を呼出前後で照合し、不正入力時も不変。出力配列/held行を変更しても入力へ伝播しない。

GOは最低条件1〜4を上記fixtureと構造レビューで確認し、現在の問いを阻害する未解決欠陥がないこと。NO-GOは未達または契約違反。A3未承認は実装開始不可であり、実装GOではない。スパイクのCONDITIONAL ACCEPTは使わない。

## 3. 責務マップと再利用

- 新規 `tools/Artifacts/BuildRetentionPolicy.psm1`: 0→約100〜170行。1系列の件数選別とその入力検証という一責務。上記1関数だけexport、補助処理は非export。同階層のEvidence policyと同じドメインだが、件数と期限は独立して変わるため別module。状態/資源の所有者は呼出側、policy内部は1呼出で消えるメモリだけ。PowerShell/.NET基底の値/collection以外の依存なし。file/network/時計/環境/store/Unityへの呼出、他module import、script scope可変状態はなし。新フォルダ、汎用engine、strategy階層、class/asmdefを作らない。
- 新規 `tools/Artifacts/tests/BuildRetention.Tests.ps1`: 0→約160〜240行。上記fixtureと集合/順序/不変性の検証一責務。`-Case`と`-ResultPath`、registered/selected/executed/failedの結果形式を既存に揃える。`EvidenceTestSupport.ps1`のAssert/Complete関数をdot-sourceして再利用可。ただしNew-EvidenceTestEnvironmentは呼ばず、test自体もBuild policy以外のproduction moduleをimportしない。実行後の非秘密結果file保存はtest runnerだけの責務であり、policyにI/Oを注入しない。0件・未知case・集合不一致は不合格。
- 既存 `tools/Artifacts/README.md`: 165行→約8〜15行追加。実装後に内部policyの呼び方/境界/offline commandを説明する。CLIが利用可能とは書かない。説明の所有者はArtifacts保守担当。恒久的な設計文書を別に作らない。
- 本HANDOFF: Phase Aの仕様・採否・検証計画を1本に保持。新しい台帳・進行管理toolは作らない。

EvidenceRetentionPolicy/既存呼出側/既存test/supportには変更しない。既存`Assert-Utc`は`ArtifactCommands`→`ArtifactPaths`依存を連れてくるためimport再利用しない。Buildでは型付きUTC値を受けてparse自体を省く。Evidenceで実績のある「snapshotを判定し、実行側が直前に再判定する」責務境界を再利用する。

新規module/testは0からの増加なので50%警報の構造判断対象。各々1責務・同じ依存/寿命/テスト境界に凝集し、500行未満見込み。行数を減らすために選別とI/Oを統合しない。3責務や500行超を要するならscopeを見直す。

## 4. 実装順序・停止規則・方式

将来のBでは、A3承認snapshotを受けてpolicyと非秘密fixtureを実装し、READMEを現況へ合わせる。production呼出側は追加せず、単体で引き渡す。

今回のA3方式案は**従来のtracked HANDOFFを正本**とし、H1/external-current-v1へ新taskを移行しない。Harness READMEではtask別固定仕様/承認値の追加が必要で、現在の新taskをそのまま実行できない。policy単体のためにHarnessのコード・固定profileを変更する必要はない。既に適用された他taskのgateは維持し、通常auditの緑をその代替にはしない。`artifact-evidence-lifecycle`のCURRENT/承認値を複写・上書きしない。

初回Phase Aでは文書確認・適用監査に限り、Workflow startやHarness init/current更新、Artifact保存/配送は行わなかった。A3凍結後のWorkflow startは通常taskの開始手順として当該task IDだけで行う。これはpolicyの実装範囲ではない。Harness移行、Artifact payloadの保存/削除・資格情報・scheduler設定変更は行わない。

外部I/O/新しい永続状態/横断管理機構/CLI接続/系列収集が必要になったら、その場で追加せず本案を縮小する。新状態・新依存・所有者/寿命/API・不明状態の扱いを変える必要がある場合はPhase Aの新revisionへ返す。scope内の実装詳細修正はB適応と区別する。最低条件を満たしたらpolicy sliceを閉じ、後続機能の未成立をblockerにしない。承認前の本セッションはA3案を提示して停止する。

developが進んでも自動pull/merge/rebaseしない。承認前等の区切りでfetchしたSHA差分と今回の入力契約への影響だけを照合する。他worktree・branch・Unity・共有storeは操作しない。

A3案提示前に `git fetch origin develop` を再実行し、origin/developがplanning baseと同じSHAで差分なしと確認した。branchのmerge/rebaseは行っていない。

## 5. 検証と独立レビュー計画

Phase Aで実行したのは文書確認、`git diff --check`、`pwsh tools/docs-audit.ps1`、`pwsh tools/contract-audit.ps1 -BaseRef 2203dded470a3ac4e7a751f594f8f7110005576b`。A3凍結後は以下のoffline検証をCで実行する。Unity Editor/test、Build、R2の実操作は対象外。

将来の検証は次のとおり。全て同PCの担当worktreeで、非秘密fixtureを使う。

- 差し戻し中の起点: `pwsh tools/Artifacts/tests/BuildRetention.Tests.ps1 -Case protected-counts,failed-incomplete,protected-over-limit,invalid-snapshot,invalid-n`。これらcase名にE2/E3/E4/E7/E8を対応させる。Cは違反する条件を記録して必要なcaseへ調整できる。時刻注入済みの値のみを使用し、Task.Delay/Thread.Sleepを使わない。
- 判定C必須（GO候補head）: 新規BuildRetention suiteの全case（E1〜E9の全境界を収録、`-Case '*'`）を`-ResultPath`付きで実行する。全Artifacts PowerShell parse、上記docs/contract/diff audit、完全diffでEvidence policy/support/呼出側とUnity/依存の無変更、policyのI/O不在を確認する。suiteはregistered=selected=executedの非空同集合、failed=0、exit=0を要求する。変更もproduction依存もないEvidenceRetention全14caseは今回の必須にせず、既存30日契約は無変更のdiffで確認する。既存適用taskのEvidence gateは変更しない。
- H1未移行のためBに新しいH1 B-exit gateを課さない。テスト実行・合否はCへ渡す。Bは既存の実装後contract auditと未実行記録を行う。
- 全EditModeの適用除外をA3で承認する案: Unityコード/アセット/asmdef/依存を変更せず、純粋PowerShellに閉じるため。代替証拠は新規policyの全offline suite、PowerShell parse、固定diffの構造レビューと機械audit。既存適用taskのUnity/H1 gateの省略を許可するものではない。Unity/build/transport/Workflow/Harness suiteは今回変更する依存経路がなく、判定必須に追加しない。
- 操作・目視条件: なし。未知の実機経路: なし。test入口のPowerShell 7と`-Case/-ResultPath`/結果集合形式は既存EvidenceRetentionと同じ経路を読んで確認済み。新規suiteは未実装であり疎通成功とはしない。Cが初回実行し、policy自身の単体実行がI/O整備を要するならB適応かA再開へ分類する。
- 証拠: A3固定snapshot、所見のないB result、base/headの完全diff、各offline生stdout/stderr/結果JSON、parse/audit結果をGit外のこのtask専用bundleに保持し、取得先・UTC・hashをこのHANDOFFに追記する従来方式。同PCのC/C′が固定copyを読めることを確認する。rawをGitへ追加せず、Storageへのuploadは必要条件にしない。C′へCの所見を渡さない。
- A0/A1主担当: root / Codex（OpenAI GPT-6系。実行環境から詳細モデルIDは未確認）。
- A2: 新規セッションの独立モデルに同じ固定A0/A1 r1を渡す。通常riskとして最低1件、アーキテクチャゲートと契約/小ささ/独立検証可能性を確認する。自己レビューで代替しない。
- A3: rootが採否を整理、人間が設計判断と受け入れ境界を承認して初めて凍結する。
- B/C/C′は新規subagent sessionを使い、dispatch指定モデルをB=`gpt-6-sol`、C=`gpt-5.6-sol`、C′=`gpt-6-astra`とする。CはBと異なるモデル、C′はB/Cと異なるモデルでblind入力を使う。全てOpenAIで別vendorの強化独立性はない。詳細実績は各Phaseで記録する。

## A2記録とA3承認案

独立A2は新規subagent `a2_build_policy` に固定A0/A1 r1だけを渡して実施した。tool指定モデルは `gpt-5.6-sol` / OpenAI、reasoning high。可変HANDOFF・他レビュー所見を渡さず、reviewerは固定hash/baseの一致を確認した。主担当の自己レビューではない。同じOpenAI vendorであり、別vendorによる独立性はない。reviewer自身は詳細モデル系列を確認できないと報告したため、ここで記録するモデル名はdispatch指定値でありbackendの独立検証結果ではない。通常riskの1件以上とarchitecture gateを満たすA2として扱い、C′実施済みにはしない。

- A2-1（P2/semantic、unique）: failed/incompleteの選別に不要なpublishedAt/inUseまで全体blockerにするとの指摘。**採用**。主担当の分類は「現在の問いに不要な入力制約」。旧案でも既存成功の削除候補化は起きないため、直接の誤削除違反ではないが、成功集合の判定を止める理由がない。成功行だけでこれらfieldを検証し、E3を拡充した。未知publishStateと成功行の未知保護は全体保留を維持する。
- A2-2（P2/architecture、unique）: 判定に使わないSeriesIdとinvalid-series-idを削除するとの指摘。**不採用**。今回の依頼は系列を「呼出側から与えられる識別子」として扱うことを明示するため、その最小string入力を維持する。生成/分解/永続化/行所属の照合は追加しない。これは入力形状の確認だけであり、系列の正しさを証明しないというpreconditionを維持する。安全性の証明を主張していないため、現契約違反ではなくAPI簡略化の選択肢と分類する。人間の判断点として残す。
- A2-3（P3/scope-test、unique）: 未変更・非依存のEvidenceRetention全14caseを新sliceの必須にするのは過剰との指摘。**採用**。現在の問いに不要な検証結合として、新suite全件とparse/audit/構造diffへ限定した。他taskの既に適用されたgateはそのままで、Evidence契約の無変更は固定diffで確認する。

後続改善候補の新規追加はなし。A2はN内保護、超過、Ordinal順、非変更の選別と1module/1suiteの配置、I/O・時計・store・Unityからの分離を妥当と評価。実装・test結果・未生成の証拠経路は未確認。

同reviewerがr2固定copy `tools/Artifacts/artifacts/build-retention-planning/A3-r2-review.md`（生成UTC `2026-10-09T16:54:19.3212615Z`、SHA-256 `a4c5f1255d9c0e8230c8462dfb30d424f240a791b61190f8a8a6fcb2184b5ad6`）を再確認し、A2-1/3の反映とA2-2不採用を妥当とした。採否反映PASS、現在の問いのblocker 0。これは設計レビューの結果であり、人間のA3承認・実装GOではない。

Phase Aの機械確認はdocs auditがerrors=0/warnings=0、contract auditがerrors=0/warnings=0（検査8はH1未移行なのでnot-applicable）、diff checkに違反なし。初回docs auditの未実施C/C′欄に対する形式警告は表記修正で解消した。contract auditが検査したUnity .csは608件だが、Unityのコンパイル・テストを実行した意味ではない。既知生成物ディレクトリの一覧取得拒否警告は残し、その中身は今回の確認対象に含めない。

人間が2026-10-10に凍結した判断点は、(1)利用中をN内に数える選別、(2)N>=1・UTC型値・Ordinal同点順、(3)不正snapshot全体保留、(4)process内APIと最小変更範囲、(5)tracked HANDOFF/H1未移行とoffline代替証拠。承認根拠は本チャットの「PhaseA3凍結としてPhaseC’まで進めて」。契約内容はr2承認案から変更しない。

## 6. Phase B 実装結果

未実施。implementation head・担当・result snapshot未生成。

## 7. Phase C

未実施

## 8. Phase C′

未実施

## 9. Phase D

未実施。PR作成・push・マージ・harvestなし。

## 次の接続先と後続へ送る問い

- Program Bの次スライス: Build publish/catalog側から1系列の確定snapshotを作り、このpolicyへ渡す。系列情報の収集・永続化、成功確定の証拠、利用中状態の取得はその所有者が設計する。
- Program Bの実行側: policy結果は候補であり実DELETEの許可証ではない。削除直前の最新状態確認・排他・再試行・部分失敗を実行側が所有する。今回その仕組みを作らない。
- Program Bの後続: Nの設定/既定値、失敗/未完了の後始末、容量・multipart。Program C/D: 別host/Cloud、BuildSystem/Harness接続。各着手時に個別Phase Aを行う。
