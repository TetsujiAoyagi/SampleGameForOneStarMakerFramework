# Artifact Storage A — Evidenceの通常利用と期限清掃

## 0. メタデータと入口

- type: `slice`、status: `A2指摘統合 r4、A3判断待ち`。今回のPRはPhase Aのみ。以下は未実装の具体契約案。
- branch: `codex/artifact-evidence-retention-phase-a`。専用worktree。共有checkoutと他Agentのbranch/生成物は変更しない。
- planning / implementation base: `d1a2606f0e84dde4bfb795e88c21a58cd4a7ecb9`。2026-10-08 fetchしたorigin/develop、PR #108 merge済み。implementation head: 未到達。
- risk: high（寿命・並行削除・一回のリセット）。owner: OSM maintainer/storage owner。created: 2026-10-08。expires: 2026-11-08または置換revision。
- harvest to: 実装・切替後の通常CLI/保存契約は `tools/Artifacts/README.md`、workflowイベントは `tools/Workflow/README.md`、H1変更は `tools/Harness/README.md`、program状態は `docs/README.md`。実装スライスDで本書を削除する。Phase Aだけのmergeでは保持する。
- Phase A snapshot: `evidence-lifecycle-a1-r4`（Git外固定copy、生成 `2026-10-08T00:55:11.0277112Z`、SHA-256 `b2125db14162f143e74323d8af587b2edb5d1e733829ea409f06188cfed42ce0`）。A3 snapshotは未生成。Phase B result、evidence bundle、C′ blind bundleは未到達、値を推測しない。
- **H1提案:** A3でtask `artifact-evidence-lifecycle` に `external-current-v1` / `local-gates-v1` を明示採用する。承認前は本HANDOFFが入口。A3承認後、固定本文UTF-8 bytes/hash/baseをApprovedSpecificationsへ登録し、`pwsh tools/harness.ps1 current -Task artifact-evidence-lifecycle` を唯一の実装入口にする。今回init/CURRENT書込/承認値追加は行わない。
- H1 bootstrapはB最初の小commitで承認entryを追加しinit、以後仕様変更はA再開。完了引渡しのB exitはdiscovery、判定Cはjudgment、同一host別worktreeまで。過去RESULT探索/Gitへのraw・REVISION・Phase RESULT追加をしない。

## 1. A0 — 目的・現況・制約

**問い:** 明示した任意の非秘密file集合を一操作で保存し、別sessionでhash照合・ログ/画像閲覧でき、作業中は保持し、実際のtask終了から30日で自動清掃できるか。

program r6のスライスAを具体化する。PR #108は方針改訂のD完了、通常Evidence/清掃/リセット成立の証拠ではない。旧CLIは固定E1/E2、Age30日config、独立commit/witnessのまま。PR #106の `EvidenceReaderFixture.ps1` は13非秘密dummy + manifest/receiptを隔離生成し、実Packaging/偽transport/ledger/readerを通せる。これを通常fixtureへ再利用し、本番private E1依存を復活させない。

対象外: Build系列/最新N件(B)、別host/Cloud/別provider(C)、Harness全体とH2d/H3・同一実体の所有整理(D)、GUI/常駐broker、Unity source/Scene/asmdef/テスト、長期lock/WORM、旧payload救済、legacy読取/自動変換、旧最低保存義務、PR別移行台帳、#107保護追加実適用。公開synthetic GitHub Releaseの整理も含めない。

AGENTS常時契約: Game→Framework/Editor隔離/asmdef追加禁止、SceneState既存順序、ILogger公開面、Update順序/例外隔離、Unity C# record禁止/nullable/fake-null、テストTask.Delay/Thread.Sleep禁止を維持。本スライスはtoolsのみ。Unity native終了stall再調査なし。cursor-agentを使用する場合はGrok明示のみ。

R2は既存同一accountのprivate `osm-artifacts`、同WindowsユーザーのDPAPI osm鍵。秘密/暗号化store/header/署名URLをprompt・argv・env・log・Git・同期先へ出さない。資格情報の登録/rotation/失効は変更しない。通常writerにbucket管理権限を追加しない。同ユーザー/管理者の悪意ある改変まで保証しない。

明示file集合、read lock snapshot、同bytesのmanifest/ZIP、固有key、別に渡された期待hash、private新規展開、path/reparse/case衝突・有限量を維持。ZIP256MiB、JSON1MiB、entry4096、単一展開256MiB、総1GiB、圧縮比100を現Packagingから継続。内容を事前確認する担当はpublisher。purpose/hash/禁止名検査だけで秘密不存在とは言わない。fetch/inspectは内容を実行しない。

## 2. 最低条件と受け入れ境界

- **M1 通常利用:** 明示非秘密fileを一操作publish。内部PUT/別process読戻しhash/manifest全entry/永続record完了後のみreference + packageSha256を返す。別sessionでfetch・必要ログ/原画像閲覧。旧v1 reference/config/commit経路は有限時間でunsupported-schema、network前拒否。
- **M2 終了接続:** workflowのcompleted/cancelled/resumedを同じ決定から発行・自動配送。task終了意味・発行元は§3、Storageは品質判断をしない。再送で起算日不変、順序逆転/同版不一致を拒否、再開で削除資格取消、再終了から30日。
- **M3 清掃:** inspectとcleanupが同じ純粋policyを使い、期限ちょうどを含む30日境界/利用中/転送中/不明状態/対象外/部分失敗を検査。自動schedulerが無人で再配送と清掃を行う。GET/inspectだけで延長しない。未終了taskを年齢で消さない。
- **M4 一回の切替:** §6.1 exact対象と除外をlive再照合し、旧利用停止→対象限定設定/生成物整理→新契約開始。一回のreset実行は後続Phase C、今回は計画だけ。旧保持/互換/#107保護延長を完成条件にしない。
- **M5 運用文書:** READMEを実CLI・状態・制限へ置換。日常清掃にkeyごとの承認なし、原sourceとコピー別所有、利用中/秘密/無関係領域保護を説明。

GOはM1〜M5の固定版証拠が揃い、致命的反証なし。未達/不明はNO-GO。最低条件を満たしたら終了し後続B/C/Dの問いを足さない。A3後のblockerは凍結M/常時契約への違反を併記し、それ以外はprogramの所有スライスへ送る。例外承認: なし。A3判断未決は§10、実装開始は人間のA3後。

## 3. Workflowの終了・再開と配送

発行元は新 `tools/workflow-task.ps1` と `tools/Workflow/TaskLifecycle.psm1`。特定Agent製品のturn終了hookに依存しない。repo内の開発taskを `repositoryId + taskId` で登録し、同一taskを複数worktree/sessionから使える。taskIdは小文字英数字/hyphen1〜64字、repoIdは既存64hex。状態の寿命はtaskの全活動期間。taskの終了を決めたworkflow担当Agentが、人間の通常の完了/打切り指示（Phase Dの完了指示等）を処理する同じ手順でendを呼ぶ。Evidence専用Close承認は作らない。通常task全体の決定を記録する唯一入口とし、保存だけをしたtaskもこれを使う。

新公開入口（仕様案、現在は実行不可）:

```powershell
pwsh tools/workflow-task.ps1 start -Task <id>
pwsh tools/workflow-task.ps1 end -Task <id> -Reason completed -ExpectedVersion <n> -Decision <non-secret-decision-id>
pwsh tools/workflow-task.ps1 end -Task <id> -Reason cancelled -ExpectedVersion <n> -Decision <non-secret-decision-id>
pwsh tools/workflow-task.ps1 resume -Task <id> -ExpectedVersion <n> -Decision <non-secret-decision-id>
pwsh tools/workflow-task.ps1 status -Task <id>
```

completedは人間がtask全体の完了を決定した時、cancelledは人間がtaskの打切りを決定した時。完了の品質/Phase D承認自体は既存workflowが所有し、このCLIは再審査しない。Agent停止、pause、timeout、失敗、C/C′合格、PR作成/mergeの検知、Harness closeを終了に推測変換しない。endが未発行ならinspectは `active/end-not-recorded` を表示。終了の取りこぼしは同じDecisionを再適用して直す。resumeは同じtaskの作業を実際に再開する入口で、保存/利用開始より先に完了させる。新task作成で旧task状態を上書きしない。

Workflow storeはKnown Folder `OneStarMaker/Workflow/<repoId>/tasks/<taskId>/`、credentialを含まないprivate ACL/reparse拒否。task stateにimmutableイベント列/outboxを一つのatomic更新で記録（CreateNew temp/flush/rename、世代CAS、同task file lock）。state原本はworkflow所有。Storageは原本削除/書換をしない。

event `schemaVersion=1, repositoryId, taskId, eventId(32hex), version(正整数), previousVersion, kind=started|ended|resumed, occurredAt(UTC), endReason(completed|cancelled|null), decisionId`。occurredAtは発行側が決定を初めて永続化した一回のUTC、受信時刻で上書きしない。taskEndedAtはended.occurredAt、再開時null、deleteEligibleAtはtaskEndedAt+2592000秒（UTC、夏時間影響なし）。CLI引数で日時を偽装しない。時刻差替えはoffline module scopeだけ。

同Decision同payloadは既存event返却（version/時刻不変）。同Decision異payloadは拒否。versionは同task排他内で単調増加、previousVersionはversion-1。同version/id同内容は冪等、同version異内容はcorrupt/conflict。高い版が先着したらpending-gap、欠けたイベントをoutboxから順に配送するまで削除しない。低い版は既存bytes照合後staleとして保持時計を変更しない。endedの次はresumeのみ、activeの二重endは同Decisionの再送以外拒否。再終了は新Decision/new version。

commit後workflowコマンドはArtifacts syncを直接一度呼び、失敗は `recorded/delivery-pending` を表示しeventを取り消さない。Storageのapply成功ackを永続化後のみdelivered。publish/fetch/inspect/cleanupのprologueも同task outboxを再配送し、毎日のschedulerは登録済み全taskを再配送する。CLIは任意event JSONを信頼せず、Workflow storeの固定identity/版/hashから読み取る。Storage state/catalog欠落や配送不明ではそのtask非削除。ネットワークなしでendを記録できるが未配送を成功表示へ潰さない。

Storageコピーの登録はtask全体stateに結び、後でpublishされたcopyにも最新状態を適用する。ended taskへの新publishはresume要求、endとpublishが競合したらtask lock順に解決し、end後にreadyを追加しない。

### 終了決定と削除中断の確定境界（r2）

`end`はworkflowのtask完了/打切りの正規最終処理であり、別のEvidence承認操作ではない。workflow SkillのD完了・通常task完了/打切り手順を更新し、完了receiptを発行する同じ処理でDecisionとeventを同時永続化する。`resume`も通常の再開処理そのもので、Storage利用前に行う。人間指示の信頼済みmessage/decision-record ID、task instance、kind、決定UTCをDecisionとして固定する。再実行は同じID/UTC/内容を使う。初回は正規最終処理内で取得したUTCをDecision/eventへ一度だけ確定し、終了処理の実完了時刻とする。rawの人間指示時刻と終了処理完了時刻は別field、配信時刻を終了にしない。

CLI未呼出のままAgentが落ちた場合はtask終了済みと表示しない。workflow statusは完了指示recordと終了receiptの不足をpending-finalizationとして表示し、次sessionが同じ信頼済みDecisionから正規処理を再実行する。完了指示recordはworkflowの最終処理開始時にdurable保存し、記録自体をendとは扱わない。taskを既に終了済みと扱った外部既存recordからの回復はそのtrusted recordの終了UTCを取り込み、nowへ更新しない。取り込みはworkflow入力adapterだけ、StorageがPR/会話/closed.jsonを探索して推測しない。正規finalizationのdecision→event→receipt各crash地点と、record済み未配送をofflineで検査する。

`workflow-task.ps1`をTaskLifecycle/TaskEventStoreとStorage preflightのcomposition rootにする。resume前に同じguard内で `Assert-EvidenceTransitionSafe` がdeleteIntent/process/remote不明を確認し、未解決ならWorkflow version/eventを変更せず返す。必要なreconcile networkは同guard内のArtifacts側が実行し、結果確定後だけWorkflow transitionをcommitする。Workflow core/storeはArtifactsをimportしない。Storage public orchestrationはguardを取得し、guard取得済み内部APIは再取得しない。end/start/publish/useも破壊操作不明の間は変更しない。syncはcommit後guardを解放して呼ぶ。

## 4. Storage schema/APIと所有

新config schemaVersion=2: profile=`osm`, endpoint(既存検証済み), bucket=`osm-artifacts`, repositoryId, prefix=`development/evidence/v2/`, policy=`task-end-30d-v1`, deploymentId(32hex)。setupでprivate/public URL無効/custom-domainなし/適用lock・lifecycleが新prefixを削除しないことを確認し、その非秘密deployment記録に結ぶ。毎runの設定撮影/24h更新/witness試験は廃止。未知deployment/旧schemaは拒否。管理者の途中変更は保証外、DELETE拒否時はblockedを表示しruleを解除しない。

selection schemaVersion=2: purpose=`evidence`, taskId, root(absolute), files(明示relative配列)、base/head(40hex)。1〜4096file、再帰収集なし。各ファイルの非秘密内容とblind入力/所見分離はpublisherの責任。既存E1/E2定数・Capture専用selectorを置換。レビュー用bundle内の原source bytesは変換しない。

keyは `development/evidence/v2/<repositoryId>/<taskId>/<artifactId32>/bundle.zip`。成功artifactはimmutable、上書き禁止、転送前intent固定後1 PUT。PUT不明を同key再送せずreconcile GETで期待hash確認、absence/不明をreadyへ推測しない。成功したDecisionと別runの重複を自動抑止する大きなdedup機能は今回追加しない。

opaque referenceは `osm-evidence-v2:` とbase64url strict JSON。fields: schemaVersion=2, deploymentId, repositoryId, taskId, artifactId, key, base, head, packageBytes, manifestSha256。期待packageSha256をreferenceから補わない。旧 `osm-artifact-v1:` はunsupported-schema、旧retainUntil/serverLockLowerBound/OwnerCloseを期限に流用しない。outer package codecはEvidenceのv2 identity/taskを明示し、旧v1を通常fetchで受理しない。Packagingの安全なZIP/hash処理は再利用する。

artifact record: schemaVersion=2, artifactId/taskId/repositoryId/deploymentId/key/base/head/packageBytes/packageSha256/manifestSha256/entry集合/createdAt, state=`put-pending|ready|delete-pending|deleted|failed`, appliedEventVersion, taskEndedAt/endReason/deleteEligibleAt, protections[], ownedCopies[], deleteIntent。mutable stateは単一task transaction、immutable publish receiptは別に固定。copy記録はtool-owned absolute path/bytes/hash/origin=storage-copy、source pathは清掃対象へ登録しない。採用変更とstaging清掃判定は同task guard内で行い、採用pending中も非削除とする。deleted tombstoneを残しfetchはexpired、payload復元は保証しない。state bytes破損は非削除。

公開操作（案）:

```powershell
pwsh tools/artifacts.ps1 evidence publish --profile osm --config <v2> --selection <v2>
pwsh tools/artifacts.ps1 fetch --profile osm --config <v2> --reference <opaque-v2> --sha256 <trusted64>
pwsh tools/artifacts.ps1 evidence inspect --profile osm --config <v2> --task <id>
pwsh tools/artifacts.ps1 evidence use --profile osm --config <v2> --reference <v2> --consumer <id> --until <UTC> --reason <text>
pwsh tools/artifacts.ps1 evidence release --profile osm --config <v2> --reference <v2> --protection <id>
pwsh tools/artifacts.ps1 evidence cleanup --profile osm --config <v2>
pwsh tools/artifacts.ps1 evidence cleanup --profile osm --config <v2> --dry-run
pwsh tools/artifacts.ps1 evidence schedule install --profile osm --config <v2>
pwsh tools/artifacts.ps1 evidence schedule status --profile osm --config <v2>
```

publish exit0はPUT/readback/全hash/receipt/catalog atomic確定まで。reference/hashはJSON resultの並列fieldsで返す。内部検証で固定するreceiptを利用者が別commitしない。失敗/部分失敗/不明はexit1かつsafe reasonCode/residue、成功を偽らない。end/resumeのdelivery pendingとscheduler/cleanup部分失敗はexit2、ready/deleted未確定を区別。未知/旧CLI経路はexit1 unsupported-command/schema。資格情報exitの意味は変更しない。

fetchはtask排他で一時operation-useを取得し、削除と競合しないうちにcopyを作る。fetch/GETのみでは明示利用中protectionを延長しない。readyコピーもStorage所有・taskの同じ期限対象、調査/reviewで継続利用するときはuseで有限untilを明示しreleaseまたは満期で終了。古いPRリンクはpinにしない。use失敗時は保護済みと扱わずpayload利用を開始しない。利用者が自分で作ったsource/copyはStorageが消さない。

protectionsは明示owner/consumer/reason/createdAt/until/releasedAtを持つ有限保護。期限不正・状態不明ならskip/unknown-use。プロセス中のoperation-useは保持file handleとprocess identity/start時刻、転送intentを使い、timerだけで解除しない。process終了/pipe EOF未確認なら転送中として非削除。lease/heartbeat/周期的Owner再承認なし。

## 5. 競合・削除と一時物

純粋 `EvidenceRetentionPolicy` は注入UTC + taskの連続版state + artifact + protection + operationからeligibilityと理由を返す。readyかつended、now>=deleteEligibleAt、有効protectionなし、転送/未確定intentなし、新prefix/registry所属/owned-copyのみが条件。inspect/dry-run/実DELETE直前で同じ関数を呼ぶ。stale snapshotを削除許可にしない。

共通task guardはWorkflow storeが所有するFileShare.Noneの排他handle。workflowのcommit、Storage event適用/publish/fetch/use/release/削除最終判定はこのguard内で線形化。Storage→Workflow guard、Workflow CLIはguardを解放してsyncを呼ぶ（再入/逆順deadlock禁止）。network DELETE完了・確認GETまでguardを保持する。排他取得は最大30秒でbusyを返し、無期限に待たない。network一回120秒/run10分・retry/redirect禁止を継続。

削除前にevent outboxを連続版まで適用し、policy再評価、durable deleteIntent（key/record版/hash/process identity/時刻）を固定する。DELETE→404 NoSuchKeyの確認後tombstone/owned copiesを対象限定清掃、部分失敗はremote/local別stateで再実行。DELETE403/409/timeout/不明はblocked/delete-pending、管理設定は変更しない。

再開/useが先ならDELETEはskip。DELETEが先なら再開は終了を待ち、既削除artifactは `payload-deleted` と一覧返却（task再開で復元しない）。process crashでguardが解放されてもdeleteIntentは残る。reconcileで元process/子の終了とGET結果が確定するまでresume/use/publishはbusy/unresolved-delete、成功を返さない。未停止の子DELETEと復帰GETを競合させない。既削除の確認後resumeは新活動版を発行できるがそのpayloadは失効のまま。task単位版を単純に戻してpayload存続を主張しない。

一時物はcreatedAt+7日（604800秒）でtool-owned upload/staging/未採用失敗copyのみ清掃。転送中、deleteIntent不明、レビュー入力へ採用、明示useは除外。採用する失敗ログは通常publishでEvidenceへcopyし、その成功前に元stagingを消さない。R2 incomplete uploadのintent登録keyだけを清掃、registryなしobjectは日常cleanerが勝手に削除しない。Harness source/retainUntilはStorageの第二時計にしない。

## 6. 自動起動と責務マップ

Windows Task Schedulerの同一owner InteractiveToken/Limited task `OSM-Evidence-Cleanup-<repoId先頭12>`。毎日03:00 local + logon + StartWhenAvailable、重複はIgnoreNew。ログオンしていない間は実行されず次ログオンで追いつく、期限時刻は削除資格で即時削除SLAではない。password/親鍵を登録しない。ネット失敗はpendingを次回再実行、成功Evidenceを再publishしない。ジョブはoutbox再配送と同policy cleanupをそれぞれ独立した永続cursorで進める。1 run最大10分、再配送最大2分/100task、清掃へ残り8分/100objectを予約し、残件は次回へ。両段階の失敗task/itemはcursorを進めて次回の末尾へ回し、先頭障害や大量outboxが清掃を飢餓させない。busyはskip。非秘密lastRun/nextRun/deleted/skipped/blockedをstatusで確認。

installは判定対象の最終implementation headから作成したscript/module/DLL/deps/config manifestをKnown Folder `OneStarMaker/Artifacts/runtime/<implementationHead>/`へprivateコピーしてhash照合、worktreeの削除/dirty変更に依存させない。Windows scheduler actionは絶対pwsh path、-NoProfile/-NonInteractive/-Fileの固定entry、資格情報引数なし、background helperはHidden。既存同名taskのidentityが違えば上書きせず停止。runtime/config欠落/hash不一致なら何も消さずfailed。scheduler installは一回の運用設定、日常清掃の個別承認は増やさない。

以下はB変更案。数値はA1時の行数→予想増分/新規規模。Unity folder/namespace/asmdefへの依存は0、module内部の時計/transport/fault hookは非公開、標準CLIにテストroot/time overrideを出さない。

- `tools/workflow-task.ps1`（0→約100）、`tools/Workflow/TaskLifecycle.psm1`（0→約250）: task意味と状態遷移、workflow担当所有。CLI parsingと純粋transitionを分離、task全寿命、Artifacts transportに依存しない。Decision/versionの単体検査。
- `tools/Workflow/TaskEventStore.psm1`（0→約250）: private状態/outbox/CAS/guard永続化、task全寿命。TaskLifecycle→store、storeはArtifact/レビュー合否に依存なし。atomic失敗/並行processテスト。Workflow directoryは意味の所有境界。
- `tools/Artifacts/EvidenceApplication.psm1`（207→置換約250）: 通常publish orchestration、operation寿命。selection/Packaging/転送/recordにのみ依存、保護/期限policyを混ぜない。#106 fixtureで内部検証失敗確認。
- `tools/Artifacts/EvidenceContract.psm1`（0→約180）: strict config/selection/reference v2 codec。CLI/application入力境界、state/clock/networkなし。旧schema/identity/path拒否。
- `tools/Artifacts/EvidenceStateStore.psm1`（0→約300）: catalog/receipt/protection/intent/tombstoneとowned-copy、Storage所有、Workflow guardを利用。event取込みと連続版照合、atomic/faultテスト。原source/root清掃機能を持たない。
- `tools/Artifacts/EvidenceRetentionPolicy.psm1`（0→約160）: UTC/終了版/利用/状態から純粋削除資格。I/Oなし、policy所有、境界時刻と全順序の単体検査。
- `tools/Artifacts/EvidenceCleanup.psm1`（0→約250）: guard→sync→policy→DELETE/GET→tombstone/local copy、実行単位寿命。transport/store/policyに依存、R2管理設定に依存しない。故障注入と限定live。
- `tools/Artifacts/EvidenceSchedule.psm1`（0→約180）、`tools/Artifacts/EvidenceCleanupJob.ps1`（0→約60）: runtime固定/scheduler登録と起動I/O。利用者Windows session寿命、policy重複なし。fake scheduler+実一回起動検証。
- `tools/Artifacts/ArtifactApplication.psm1`（522→増分最大50、prepared transfer再利用）、`ArtifactCommands.psm1`（151→増分最大70）、`tools/artifacts.ps1`（74→増分約100）、`ArtifactPaths.psm1`（76→増分約60）、`EvidencePaths.psm1`（79→置換約100）: 共通network/package/private pathとdispatchのみ。既存522行警報は既存由来、新期限責務をこのファイルへ足さず上記へ分割。
- `tools/Artifacts/Packaging/PackagePolicy.cs` と `PackageIO.cs`、`Transport/R2ArtifactTransport.cs/.psm1`、`ArtifactReadback.ps1`（59→増分最大40）: codec v2/task identityと新prefixのexact operation allowlist、同bytes検証を再利用。SDK型/秘密はadapter内。互換legacy分岐は作らずcredentials/probeの必要経路だけ維持。各増分最大80、現在行数はPackagePolicy122、PackageIO335、R2ArtifactTransport.cs160/.psm136、ArtifactReadback59。
- `EvidenceLedger.psm1`（364→旧public reader/OwnerClose/独立commitを撤去、共通receipt処理を移して縮小）、`ArtifactProtection*.psm1`（172/98）: 通常Evidence依存を除去。synthetic probeに必要なlock検査は残す。参照0だけで削除せず旧方式の置換残骸と確認する。
- `tools/Harness/ApprovedSpecifications.psm1`（72→約20増）、`GatePolicy.psm1`（144→約15増）、`Adapters/LocalChecks.psm1`（276→約50増）: このtaskのA3固定entry/scope、Workflow/test stepだけ追加。`close`/Unity profileは変更しない。RecordStoreは下記の固定live attachmentだけ追加する。既存大規模adapterにlifecycle/cleanup logicを混ぜない。H1 raw保存/gate集合は再利用。
- `tools/Artifacts/tests/EvidenceReaderFixture.ps1`（70→約40増）とArtifactEvidence.Tests（460→最大100増、新規policy/競合suiteへ分離）、追加Lifecycle/Retention/Cleanup/Schedule suites、`tools/Workflow/tests/TaskLifecycle.Tests.ps1`: #106生成fixtureとsignal/fault/注入時計を再利用、各新suite約150〜300。suiteの登録/選択/実行集合を機械記録、0件成功禁止。
- `tools/Workflow/README.md`（0→約70）、Artifacts README/Harness README/docs README/programとworkflow Skill completion手順: 実装成立分だけharvest。Task endコマンドをtask完了処理の同じ入口へ記述、Harness closeへの暗黙接続を作らない。今回tracked変更はHANDOFFのみを基本とする。

- `tools/Artifacts/EvidenceReset.psm1`（0→約250）: 一回reset専用orchestration/strict manifest codec/hash/identity/対象と除外/receipt/残件再実行を所有。通常Cleanupの新prefix guardは拡張しない。管理rule変更を実装へ混ぜずCの管理UI手順へ。tool-owned pathとexact旧keyの限定DELETEはこのmoduleだけ、原sourceを受け付けない。manifest替え/foreign key/root/path/reparse/secret/source/use/partial再実行のoffline testを `EvidenceReset.Tests.ps1`（0→約220）へ置く。
500行/50%以上増/3責務の警報対象はA3で実測し分割理由を残す。新規moduleの50%以上増は新設由来、上記の変更理由/所有/I/O境界で分離する。過剰な汎用Manager/Helperは作らない。

## 6.1 一回のリセット — 対象と手順

**今回は計画のみ、下記を実行しない。** historical観測のexact key/ruleと、2026-10-08のlocal read-only存在確認を区別する。live R2の現存/解除可否/影響はA3凍結前の未確認。auto-reviewがCloudflareタブ読取を拒否したため限定bucket読取の回答待ち、未確認を不存在/確認済みにしない。

R2候補は同account/private bucket `osm-artifacts` の以下だけ。repoId `4d2d1728f1db77f50605a10da0bbf10fd31443c381c4f312658134fffb850b3b`。

- E1: `evidence/first-use/4d2d1728f1db77f50605a10da0bbf10fd31443c381c4f312658134fffb850b3b/caed8bae55c033673b75532dc650f0a3a3cedc48/64c42fc4d5d24fa580af7cc12d3a3c92/bundle.zip`。
- E2: `evidence/first-use/4d2d1728f1db77f50605a10da0bbf10fd31443c381c4f312658134fffb850b3b/896eaea8a912248333704c80549d46ecf20c02c6/1495786d921949fca6e92edbef21010e/bundle.zip`。
- 各E1/E2と同directoryの `protection-witness.txt`（bundleの兄弟key、#107追加rule対象外）。上記4keyは#104送信実測記録を根拠とする。
- #107 W `probe/unlocked/8b4830f6345d462f9db4ce07b47e5240/existing.txt`、L `probe/locked/8b4830f6345d462f9db4ce07b47e5240/existing.txt`。control.txtは当時削除成功、現在不存在を再確認し存在なら対象へ黙って追加しない。
- 追加2rule `retention_continuity_w_8b4830f6345d462f9db4ce07b47e5240` / `retention_continuity_l_8b4830f6345d462f9db4ce07b47e5240`。enabled Indefinite、各W/L key全文prefix。suffixにも一致するので現在の全matching keyを確認する。
- 旧Age30日 `evidence/first-use/` rule、旧Age1日 `probe/locked/` ruleは現在のrule ID/enabled/条件/全適用範囲を取得してexact IDを凍結するまで削除不可。empty-prefix multipart-abort7日lifecycleはEvidence期限を決めず、無関係処理も覆うので維持候補（変更しない）。他lock/lifecycle/ruleを丸ごと置換しない。
- 旧synthetic publish/RouteProofの残存keyはroot prefixだけで削除許可にしない。既存intent/ledger/run markerと同identityの現在一覧を照合したexact keyをreset manifestへ列挙し、関連ruleの影響に無関係keyがあればruleを解除せずA3へ返す。

local候補（Known Folder相対、絶対pathは実行時解決・再parse拒否）:

- `OneStarMaker/Artifacts/evidence-ledgers/8dd797d6e2f14258978185eef15dade2/ledger.json` と `767d073dec92416281d9b03f7e58f527/ledger.json`。
- `.../evidence-retention/8fe65074daf5379c4a311ec5166b21ff/retention.json`。
- `.../evidence-handoffs/d79fb66106844337b4c1a0f0c311d2e6/reader-input.json`。同rootの09494a099af44756b5a91f6444d1d4e8と808d4d7c54c74464aab8c16688ce5133 directoryも現存、内容所有を読取確認するまで除外。
- `.../transfers/5a2bd180c4c04112bd1d2de71bc6ebb2/ready` と `c0e929c6b74a4b4eaddd9300cf447376/ready`（fetch copy）。上記ledger/案内/record/readyの存在はread-onlyで確認済み。
- `.../review-evidence/evidence-first-use-judgment-04a488a71a464d1fb405d002b33745ac` と `evidence-first-use-judgment-reader-62f39c71ebb34b49a434c29105752e27`（directory存在確認済み）。終了したArtifacts task生成物として利用中でなければ対象候補。
- 他の旧raw/受取copy/#107 packetは生成metadataからexact pathと所有taskをread-onlyで解決しmanifestへ列挙、名前だけで再帰全削除しない。共有checkout内のartifacts/、他worktree/branchは今回のreset pathにしない。

**必ず除外:** credentialsと暗号文/temp/backup、利用者原source、Harness全root (`OneStarMaker/Harness`)、Workflow root、新v2/current A2/C/C′入力/runtime、現在利用中/転送中/利用状態不明、無関係bucket/rule/領域、他Agent所有物。`review-evidence/artifact-cli-final-r3-cec0e01b2bb6433e9cab782ddcc84df4` はE1の原sourceとして現コードが参照するため、単にArtifacts生成directoryであることを理由に消さない。source所有側の作業は今回しない。

reset manifest schemaVersion=1、purpose=`development-reset-v1`、repositoryId/deployment endpoint identity/bucket、observedAt/frozenAt、objects[]（exact key, expectedBytes, expectedHashまたはETag, observedExists, relatedRuleIds）、localTargets[]（Known Folder相対path, resolvedPath, ownerTask, kind, observedExists, entry集合/hash, source=false, inUse=false）、rules[]（exact id/enabled/prefix/condition/全matchingKeys, action=remove|preserve）、excludedKeys[]/excludedPaths[]/preservedRules[]。strict field/type/重複/path/root/prefix/reparse/秘密source/use拒否、上位hashは別に渡す。存在不明はeligibleにしない。raw originやknown pathはallowlist内でも利用中ならskipし、entry追加/identity/rule差分があればreset停止。runtime/newv2/原sourceのnested overlapはancestorも除外。manifestは一回操作範囲だけの記録で、旧hash移行/保全審査ではない。

管理UIのrule変更後はrule receipt（変更exact id/前後collection hash/無関係不変）をreset inputへ紐付ける。reset CLIにはrule変更APIを置かず、設定の管理権限をwriterへ渡さない。objects/localTargetsのreceiptはitem別operationId/status/reason/deletedAtと確認GET結果だけをappend、payloadや秘密なし。同manifest再実行は確定済み削除itemをskipし、未確定だけ再検査する。未知scopeを自動追加しない。
手順（A3はexact reset manifestを凍結、旧義務台帳/移行台帳ではなく操作allowlist）:

1. read-only一覧でendpoint/bucket/key,size,ETagまたは期待hash、rule ID/prefix/typeと全matching key、local resolved path/所有者/reparse/useを固定。秘密は収録しない。scope不明を削除候補へ繰り上げない。現在利用中の対象はskip/除外、旧PRリンクだけを利用中とは推測しない。
2. 実装・offline/限定sandbox-prefix検証後、Cが運用切替窓を開始。旧CLIのpublisher/reader/子processを終了・EOF確認し、旧入口disabledを記録。旧利用停止後にだけresetへ進む。active他Agent/taskがいる範囲は処理しない。
3. 認証済みCloudflare管理UIで対象ruleを一件ずつID/条件照合→Delete当該rule→Save→全collection差分確認。full-key2ruleと旧Age ruleの現在matching対象だけを扱う。管理UI操作はreset担当、通常鍵は使わない/管理APIを作らない。解除不明や除外keyへ影響するruleは触らず停止。
4. Bで追加する限定 `pwsh tools/artifacts.ps1 evidence reset --profile osm --manifest <frozen-reset-json> --manifest-sha256 <trusted64>` をCが一回実行。dry-runも提供。manifestのexact keyだけDELETE→GET404、local allowlistだけ `Remove-Item -LiteralPath <validated-target>` 相当を単一PowerShell経路で実行。再帰対象は絶対pathがallowlist内部と確認後、root/ancestor/reparseを拒否。bucket全erase/外部shell連結なし。
5. partial/inconclusiveはitem別remote/local結果を保存して未完を残す。再実行は同じmanifestの残件のみ、成功key再PUTなし、旧経路自動復帰/二重運用なし。credentials/source/Harness/除外markerの前後不変を確認する。
6. 新v2 deploymentの非公開・適用ruleなしを確認、scheduler runtime固定/install/status/startを実測して新CLIを開始。旧v1説明/selector/独立commit/OwnerClose通常経路を置換。旧payload/hash橋渡しや互換readerを作らない。

未知の追加対象があればA3 freezeを更新して範囲だけ再レビューする。keyごとの削除承認や旧保存価値審査を足さない。新清掃は実装/検証/切替成立後にArtifacts READMEの事前承認で回す。

## 6.2 実装順・検証経路

順序: A3本文/承認値固定→H1 bootstrap→Workflow store/transition + Storage純粋policy→v2 codec/publish/fetch/record→sync/protection/delete/reconcile→scheduler/runtime→offline B exit→発見C修正→C限定R2/対象限定resetと切替の原観測→README最終化→GO候補implementation head固定とruntime再固定→同headの判定必須offline/必要な限定運用再照合→M1〜M5証拠を揃えた判定C→同head同bundle blind C′→D。コードが変わったら最終head証拠を取り直し、記録だけのcommitと区別。

H1 discoveryは変更面のArtifacts全offline（既存7suite+追加Retention/Cleanup/Schedule）、Workflow lifecycle suite、Harness suite/両audit。judgmentは関連全Artifacts/Harness/Workflow suite+3 .NET build+全PowerShell parse+両audit。LocalChecksにstep `workflow-local` とsuite集合を追加し、このtaskのjudgmentだけ固定必須、他task profileは変えない。live観察は同headの所見なしImplementationResult/機械hash付private payloadへ接続、H1をlive test自動判定へ全改造しない。contractは `-HarnessTask artifact-evidence-lifecycle`、普通auditを適用taskの代用にしない。

### H1のtask限定scopeと固定step（r2）

このtaskに限りApprovedSpecificationsの構造値へ `scope` / `discoverySteps` / `judgmentSteps` を追加し、New/Assert-ApprovedSpecificationとinit/current/run/handoffで承認本文hashと同じ値を照合する。dispatchはexact taskId=`artifact-evidence-lifecycle`、既存H1/H2 entryの値/形式/集合を変更しない。

scope内の省略module名は以下すべてrepo相対full pathを意味する。scopeは `tools/Artifacts/**`（code/tests/READMEのみ、生成artifacts/bin/obj除外）、`tools/Workflow/**`（code/tests/READMEのみ）、`tools/artifacts.ps1`、`tools/workflow-task.ps1`、`tools/Harness/ApprovedSpecifications.psm1`、`tools/Harness/RecordStore.psm1`、`tools/harness.ps1`、`tools/Harness/GatePolicy.psm1`、`tools/Harness/Adapters/LocalChecks.psm1`、`tools/Harness/tests/Harness.Tests.ps1`、`tools/Harness/README.md`、`tools/contract-audit.ps1`、`tools/docs-audit.ps1`、`.agents/skills/osm-workflow/SKILL.md`、`.agents/skills/osm-workflow/references/phases-and-handoff.md`、`docs/README.md`、`docs/handoff/BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md`。本tracked HANDOFFはA3承認後Git外specへ投影して削除し、移行削除commitだけscopeへ明示許可する。新規tracked仕様/RESULT/rawの追加を禁止。Unity/Cloud/他taskへのscope展開を拒否。

bootstrap commitはApprovedSpecificationsにowner承認済みA3本文のhash/base/上記scope/stepだけを追加。未登録entryをcurrentで推測承認しない。init後の `current` が固定specを表示することを最初に確認、次commitから実装。承認済みfull baseを使い、fetchで動くdevelopへbaseを自動更新しない。baseline差分内の本HANDOFF削除だけを許可するため、bootstrapからGatePolicyの同task分岐/ApprovedSpec照合を含める。自己承認や他task承認値更新をしない。

`discoverySteps` / `judgmentSteps` はこのtools専用taskでは同じ5集合: `artifacts-evidence-local`, `workflow-local`, `harness-local`, `contract-audit`, `docs-audit`。B exitは関連面が全部Artifacts/Workflow/Harnessなので同集合、判定Cは同全集合をGO候補headで取り直す。これでもB/CのresponsibilityとinputKindを混同しない。

`artifacts-evidence-local`は3 .NET Release build（Probe/Packaging/Transport）→全Artifacts PowerShell parse→Credentials/RouteProof/R2RouteTransport/ArtifactPackage/ArtifactTransfer/ArtifactRotation/ArtifactEvidenceの7suite（v2置換に合わせlegacy成功caseを旧schema拒否caseへ更新）+ 新Retention/Cleanup/Schedule/Reset suites。`workflow-local`はWorkflow全parse + TaskLifecycle suite、`harness-local`はHarness全parse + Harness.Tests（このtask scope/step/transitionの追加case）。残り2stepは `contract-audit -HarnessTask artifact-evidence-lifecycle` とdocs-audit。全suiteのregistered/selected/executed名前集合が同一、各非0/failed0、build/parse成功/実DLL path/hash/depsをrawへ収録。既存 `artifacts-local` の1build/3suiteをこのstepの証拠へ流用しない。実装/テストをsmall filesへ分けても集合を減らさない。

live rawはCがrunのprivate payload内へ保存し、所見なし観察manifestのpath/hashをImplementationResultへ固定して引渡す。H1標準runはoffline gateのみ、Cは同base/headのlive manifest/全hash/必須操作の存在を照合して判断する。CURRENTの採用inputにlive manifest参照を含める最小欄だけ追加し、一般live runner/profileを作らない。blind入力には同じraw manifestだけ、Cの判断を入れない。live manifest公開登録はこのtaskに限る `tools/harness.ps1`（236→最大25増）のrun引数 `-ObservationManifest <path> -ObservationSha256 <trusted64>` をcomposition rootにし、scope/identity/hash照合済み非秘密原観測をrun private payloadへコピーして固定する。runの生観測欄として保持し、review findingsの登録欄には使わない。この接続用 `RecordStore.psm1`（371→最大30増）はprivate immutable attachment保存/参照のみでclose/retainUntilは変更しない。

差し戻し中の起点: ArtifactEvidenceのreader-hash/handoff-identity-substitution、追加suiteのevent-idempotent/event-gap/resume-stale-end/at-30-days/use-delete-race/delete-crash-reconcile/cleanup-partial/scheduler-runtime-missing（Bで登録するcase名）。Cは凍結Mに必要な根拠を残して選択を変えられる。B exitは関連suite全集合、狭いfilterを最終GOへ流用しない。

判定必須offline: #106 dummy再利用で通常v2 package・別process fetchを通す; end再送で日時不変; version逆転/欠番/同版異payload; 再開→古end→再終了; 終了無し100日非削除; 注入UTCで30日-1tick/ちょうど/+1tick; use満期/解除/不明; use/resume対削除の双方順序をbarrier/signalで再現; DELETE成功/timeout/403/子未終了/crash after-intent/after-remote/local-failure; scheduler欠測/重複/次ログオン追いつき/公平cursor; staging7日/採用/転送中; credentials/source/無関係marker/reparse/path非削除; unsupported旧schema network0; secret例外出力なし; package path/hash/zipbomb拒否。Task.Delay/Thread.Sleep/30日待機は禁止、clock・transport・scheduler注入とシグナルで検証。

実R2はCが所有Windowsユーザー/既存鍵で新prefix下の使い捨てtask一つ、非秘密txt+原PNGの1packageを一回publish、別fresh session public fetch + 全hash/版 + ログ読取/画像表示。実workflow endの原イベントとoutbox配送/ack/Storage適用を経た同taskにだけprivate module-scope clockを注入するC専用fixed runner（公開CLIに日時overrideを出さない）で同じproduction policy/DELETE adapterを呼び、実R2 DELETE/404 + localコピー清掃を確認。active/use/外scopeの対照marker非削除。30日実待機/旧E1/E2再送・継続取得なし。fixtureUTC/注入地点/固定runner hash/実装base-headと実observationsを残し、自然30日経過の実測とは主張しない。

自動起動はCが一回の限定scheduler登録、user手動反復なしでjob起動→sync/cleanup→lastRun/resultを確認。短いtest triggerはC専用登録で本番毎日03:00とは区別、ログオン条件と復帰起動はOS task query/実process結果で確認。終了後限定検証taskだけ解除、運用taskは切替手順で登録。勝手に既存同名taskを上書きしない。

新event/scheduler接続・reset一覧取得は未実装/未確認。初回実動確認はC、必要な支援はこの計画のCLI/store/限定runner/scheduler状態収録だけ。失敗なら基盤不備/観測不足/実装欠陥を区別、権限内で修正。終了の意味/状態/API/所有/allowlistが変わるならA再開。代行人間の反復操作を暗黙条件にしない。既存R2往復/#106経路の過去成功を新v2成功と読み替えない。

全EditMode/Unity Buildは適用外: toolsのみでUnity変更/依存がなく、上記offline + 限定R2/scheduler実測で問いを判定。Unity native stallの再調査なし。資格情報DPAPI/ACLはowner環境の実測、sandboxユーザーの結果をowner成立へ流用しない。

判定証拠/C′受渡しはGit外H1 fixed spec/run/input/receipt、同base/head、完全diff、3build/parse/offline集合、publish/fetch/open/delete/scheduler/resetの非秘密raw/原画像。snapshot/runner/binary/config版とhashをmanifestへ機械記録。C′にはC所見/可変HANDOFF/A2疑念を渡さず、実機原観測を同hostの別copyで全hash確認・必要file閲覧。別host配布成立を主張しない。

## 6.3 Phase B（未到達）

Phase B: 未着手、実装・リセット・scheduler登録・R2通信なし。implementation head/result snapshotは未生成。

## 7. Phase C

未着手

## 8. Phase C′

未実施

## 9. Phase D

未着手

## 10. A2・A3判断

A0/A1主担当: Codex GPT-6、OpenAI（runtime variant未実測）。独立A2は同固定r1（修正後r2/r3を同担当が再確認）、architecture/ownershipとfailure/運用経路に分け、互いの指摘を見せない。高risk代替担当にはA0のみ渡す。各採否は下記へ全件記録する。

A3判断項目: (1) M1〜M5/対象外、(2) Workflow task start/end/resume発行意味・一回日時・Decision/versionとdelivery outbox、(3) v2 schema/API/共有guardと削除不明時停止、(4) H1 CURRENT/bootstrap/scope/step集合、(5) scheduler毎日03:00/logon/固定runtimeと7日一時物、(6) exact reset allowlist/除外・全rule影響、(7) C限定時刻注入R2/scheduler検証とUnity適用除外、(8) A2採否/残存risk。

未決: live R2現存key/rule ID/全matching影響とlocal未知rawの所有・利用状態確認、A2指摘採否、人間A3。今回PRはこれを確認・判断できる計画の提出であり、承認済み/実装済み/reset開始可とはしない。

### A2採否と独立性

- A0-only代替: 新規subagent、起動指定 `gpt-6-astra/high`。主稿/他所見なし。workflow正本+atomic配送待ち、削除時正本照合、共通排他、日次/logon固定runtime、原source別所有を採用（M2〜M4を最小責務で満たす）。最新full-state通知だけで欠番を飛ばす案は採用せず、連続version適用/欠番非削除を選択（再終了の経緯を保持し、不明な版を清掃可にしない）。DELETE intentを不可逆な論理失効点とする案は採用せず、network完了までguard保持/不明時resume成功禁止を選択（物理削除と利用開始の意味を明示できる）。live exact一覧までreset凍結不可の指摘は採用。
- 構造A2: 新規subagent、起動指定 `gpt-6.1-sol/high`、fixed r1/r2/r3のみ。AR1 highのcrash後resume責務不成立を採用、CLI composition rootのevent commit前preflightへ修正。AR2 mediumのreset配置欠落を採用、EvidenceReset/strict manifestを追加。AR3 mediumのH1 scope/step不足を採用、task限定承認値/5step/3build/全parseを固定。AR4 runtime文言訂正を採用（運用A2の同指摘とduplicate）。r2のAR5 medium RecordStore scope漏れを採用、RecordStore/harness入口をexact scopeへ追加。r3のAR6 medium Harness test path不一致を採用、`tools/Harness/tests/Harness.Tests.ps1` へ修正。本人のscope行限定再確認で解消、architecture指摘残件0（r4全文reviewは未実施）。後続条件の追加なし。
- 運用A2: 新規subagent、起動指定 `gpt-6-sol/high`。FR1 highの終了発行漏れを採用、信頼済みDecision/pending-finalization/receiptと正規処理の結合を具体化。FR2 highのruntime版不定を採用、最終implementation head/deps hashへ固定。FR3 mediumのlive終了配送迂回を採用、実end/outbox/ack/Storage適用を必須化。FR4 mediumの配送による清掃飢餓を採用、2分/8分と独立fair cursorを追加。r2のFR5 high 判定Cより後のreset/READMEを採用、M4/M5と最終headの証拠を揃えてから判定C/C′へ修正。r3で全指摘解消、新規必須欠陥なし。
- 各reviewerは別session/inputを使用。全員の自己報告はGPT-6系/正確なruntime variant未実測（起動指定との差は実測していない）。異なる起動指定を使った同OpenAI系列のA2であり、異vendor/実runtimeモデル相違やC/C′独立監査を主張しない。運用担当のr1検索結果にmutable HANDOFF一致行が混入した制約あり、本人は本文を開かず固定hash入力に基づくと報告。r2/r3再確認は固定コピーのみ。主担当の修正方針説明を途中で運用担当へ渡した事実を記録し、blind C′とは扱わない。
- A2 fixed input: r1 `fcc9757f3548d8dd044b3e04927d73309987309ce90961b269400efe27266c23`、r2 `0a3a2fec9a4d9d95abf906b7ce47ab69716ca06183609016df2d7df854098784`、r3 `63151c4764849719e86b72f66d39387a9101e080979e92099a55fb2acc2ad22c`。生成/レビュー回答の固定copyと最終snapshotはowner管理の同一host・このtask専用Git外生成領域、取得ID/hash/時刻をPR本文へ引き継ぐ。payload/所見rawはGitへ追加しない。
- 主担当A3統合案: 上記全finding採用、保留はlive inventory/人間A3のみ。承認済みと記録しない。今回実装/R2送信/reset/登録/鍵変更はなし。文書検査は最終copyに対するdocs/contract/diffをPRへ記録。

### A3に渡す未確認境界

計画の終了接続/schema/API/競合/自動起動/H1/test方法はレビュー済み。現在のR2 key/rule一覧とlocal未知生成物の所有/useが揃うまでM4 exact reset manifestを凍結できない。これは旧保存義務の継続やkeyごとの承認ではなく、Artifacts READMEの一回reset開始条件である。今回のPRをA3承認またはreset開始許可へ読み替えない。限定bucketのlive読取が承認されたらその非秘密一覧だけを同Phase A revisionへ追記・影響部分を再確認し、ownerに全体A3判断を渡す。拒否を迂回せず、Cloud/Build/Harness全体改造を追加条件にしない。