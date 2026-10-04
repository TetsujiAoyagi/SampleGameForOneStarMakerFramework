# BS3 / Burst 修復 — 凍結 A3 v1

## 0. メタデータ・承認

type: slice。risk: high（テスト内の非同期資源寿命）。owner: OSM maintainer。
branch: codex/bs3-burst-repair。implementation base: 7983e6884a01e01c335e92d9045dd2b34bea4d06。
created: 2026-10-04 Asia/Tokyo。expires: 2026-10-31。harvest: 既存 testing/build documentation。
通常の Unity 修復として tracked HANDOFF と Git 外 raw を使用。external-current-v1 と H2c限定B実行許可は採用しない。

ユーザーの2026-10-04依頼は、指定「1. 別スライスのBS3/Burst修復」に従う調査・必要最小限修復・独立C/C′と修復後のH2c再開を明示承認している。本A3はその範囲内のtest-only不具合修復を具体化する。新しい製品契約判断・範囲拡張は行わない。D/merge/push/PRは禁止。H2c本体の再開は固定引き継ぎを次担当へ渡す。

## 1. 入力・問い・対象外

原H2cの4786 runはBurst named pipe startup error、55ee runはTimeout。その後は両方でBS3 Cleanupのtransaction.lock IOException。異なる初発障害を同根本原因と断定しない。H2c CURRENT revision19、固定head7983e68、旧A3/spec/rawを変更しない。

既存fixtureはHTTP/local installへCancellationToken.Noneを渡し、UniTask.ToCoroutine内部のtask終了をTearDownが保持・確認せずdelivery directoryを消す。ContentInstallerは既存のtransaction using内でawaitし、HTTP sourceはtoken/response取消を処理する。製品コードを変えずcaller側の所有漏れを修復する。

問い: BS3中断時の所有資源を終了・解放でき、Burstを有効にした同fixtureと全EditMode成功を固定し、H2c再検証へ渡せるか。

対象外: H2c observer/runner/approvalの変更、Burst無効化、timeout延長、skip/LogAssertで失敗許容、無差別cache削除、package/Editor upgrade、Runtime公開API/asmdef/依存/所有契約の変更、本番asset再生成。将来のBurst多環境再現/恒久対策はbuild-system後続、H2c正式scope/base/spec調整はH2c再開A revisionが所有する。

## 2. 最低条件・停止

- M1: Burstの原障害、現環境、採用した有界復旧を分けて記録。Unity6000.6.0f1、現package、Burst有効の標準runnerで対象fixture成功。原障害の根因が確認できなければ未確定と明記。現環境で成功する場合は環境変更を捏造しない。
- M2: fixtureはHTTP install、local install、比較、source dispose、server response/worker終了を含むoperation全体と取消源を所有。先にfixtureへ登録してから開始し、同期例外もtaskへ捕捉。TearDownはcancel→全taskの終端観測→delivery削除の順。CTSはtask終端後にdispose。main threadで無期限同期joinしない。取消要求を解放完了とみなさない。
- M3: signal到達後のcancelとsource faultを別々に再現。同じ実Cleanup経路を呼び、ContentCacheStore.Inspect()でtransaction再取得、stream/source/workerの終了、final未公開・staging不在/空、fixture/copy/delivery/空親metaの後始末を確認。空でない親と無関係資源を維持する。実HTTP中断でresponse/server終了を一度確認する。通常成功は既存統合fixtureが担う。
- M4: 最終commitの全EditMode（空filter、全leaf Passed、skip0、failed0、compile errorなし）、contract/docs audit、独立C/C′。前後source clean。修復後の古いhead結果を新headの合格根拠にしない。

終了未確認時の停止上限は10秒。TearDownのdeadline CTS.CancelAfter(TimeSpan.FromSeconds(10))と取消登録で完了するsignalをTask.WhenAny等で観測する。Task.Delay/Thread.Sleepは使用しない。テストではdeadline tokenを明示cancelしてこの分岐を決定的に検証する。deadlineは成功や強制解放の代用にせず、delivery絶対pathを保持・報告してfailureにする。停止未確認の資源を使用中のままdispose/deleteしない。独立するfixture/copy/空親cleanupは引き続き試し、operation faultとcleanup例外を消さず集約する。通常経路で既に観測した同じfaultを二重に成功・失敗へ変換しない。

GOはM1〜M4の証拠完備、未達はNO-GO。条件付き成功なし。条件を満たして致命的反証がなければ終了。新しい有益な不確実性は完了条件へ追加しない。

## 3. 責務と変更範囲

全C#変更は unity/Assets/SampleGame/Tests/Editor/Build/ の既存Editor assembly内に限定し、各新規fileの.metaも作る。namespaceは既存fixtureと同じ。以下以外の製品コード変更が必要ならBを止めAへ返す。

- BuildContentDirectoryIntegrationTests.cs: 現475行。既存一つのbuild/load統合シナリオを維持しoperation所有とCleanupを接続。内部test seamを同じCleanupの検証用に限定許可。予想400〜530行。500行警報は既存シナリオを非分割とする理由をB結果に記録。環境変数/scene復元全体の再設計をしない。
- ContentInstallTestOperation.cs: 新規60〜140行。internalなCTS/task寿命のorchestrationのみ。AssetDatabase/path削除policyを持たせない。fixtureが持つcallback全体をTask化しterminalを観測。入力System.Threading/Tasks、test callback、製品公開面なし。
- LoopbackArtifactServer.cs: 既存nested serverを抽出、100〜180行程度。test-only HTTP listener/response/workerを所有するinfrastructure。停止要求とawaitable completionを提供し無期限同期joinを除く。停止中に起こる既知のlistener/stream例外と正常時のfaultを区別する。stall信号は中断再現の限定seamのみ。
- ContentInstallTestOperationTests.cs: 新規150〜350行程度。signal付きsourceと実ContentInstaller/ContentCacheStore、同じCleanup seam、実HTTP中断を検証。責務は中断したfixture寿命の回帰。行数増加50%警報について、別の変更理由を混ぜず配置を選んだ根拠をB結果に記録。
- docs/handoff/BS3_BURST_REPAIR.md: 通常sliceの指示・小さな証拠台帳。raw/完全diff/各Phase所見はignored planningとTestResultsに保全。H2c証拠をGitへ追加しない。

公開API、Runtime、asmdef、Scene/Prefab/asset YAMLは変更しない。Game→Framework依存を維持。Unity C#先頭#nullable enable、record禁止、偽nullに注意。テストTask.Delay/Thread.Sleep禁止。新汎用Helpers/Managersは作らない。

## 4. 検証経路

担当Cが既存Editor/project/lockを確認し、標準pwsh tools/run-tests.ps1を最初からsandbox外で実行する。Unity6000.6.0f1、EditMode、nographics、ObserveUnityを使い、process内SAMPLEGAME_CONTENT__RUNTIMEMODE=addressablesを設定・復元。H2c CURRENTへadoptしない。人間Editor・無関係process停止禁止。unity test/run禁止。正常進行は時間だけで中断しない。live progress/sidecar/hashは開かずunity.logで進行確認、hashとXMLは終了後に取得する。

起点filter: BuildContentDirectoryIntegrationTests / ContentInstallTestOperationTests。必要なら既存ContentInstallerTests / ContentHttpArtifactSourceTestsを同プロセスに含める。最終は空filter全EditMode一回、除外なし。M3のsignal cancel/fault、deadline超過保全、実HTTP中断の到達・後始末をXMLの実名と件数で確認する。実Unityの初回経路確認はCの限定run。BはUnity test/buildを実行しない。Editorコンパイル未確認なら明記。

Burst baselineは変更前C diagnosticが担当し原結果は修復headの成功根拠にしない。環境変更が必要なら根拠と対象を固定する。現在の標準起動で成功するなら環境変更なしと記録する。H2c全件を盲目的に再試行しない。

判定必須: 上記M3集合を包含した全EditMode、pwsh tools/contract-audit.ps1、pwsh tools/docs-audit.ps1。追加offline harness全suiteはこのtest-only修復では不要（H2c再開時に旧A3必須を新headで取り直す）。

## 5. A2採否・独立性・固定入力

A1 SHA256: 4f6d2adbef6da73a0c3afc7733e05840271c695de2b3c75b80a978b76097f89d。
A2 architecture=gpt-6.1-sol新規session、failure-path=gpt-6-luna新規session。同じA1を独立読取。他所見は共有せず、各原本をignored planningに保存。
採用: operation全体の所有、async server終了、deadline値/機構/未終了保全、同じCleanup seam、Inspectによるlock再取得、signal cancel/faultと実HTTP中断。A2のExitPlayMode先行という順序案は、install中断時は先にcancel/drainしてmain-thread continuationをdomain遷移前に回収する順序へ統合した。将来の環境/namespace/製品取消保証拡張は後続とし採用しない。

A主担当=root（Codex GPT-6、本sessionには細別名非提供）。B=gpt-6-sol新規、C=gpt-6.1-sol新規、C′=gpt-6-astra新規blindを予定し実績で確定。C′はA/B/Cへ未使用。H2c予約gpt-5.6-solは温存。同OpenAI/GPTのため別ベンダー強化条件未達の独立性制約を明記し、独立監査の最低条件（B/C別モデル・新規・blind）は守る。

C/C′は同base/headとA3、所見なしB snapshot、完全diff、判定必須raw、判定前機械検査を使用。C/C′結論は別ファイルに保持しDの突合はしない。C′へ可変HANDOFF、CURRENT、調整メモ、C所見・疑念候補を渡さない。証拠を同一hostの別worktreeから読取・hash検証し、再実行したと偽らない。保持期限2026-10-31。

新しい状態/API/依存/所有者/寿命/拒否契約を製品へ追加する必要、または計画外Runtime修復が判明すればPhase Aへ返す。それ以外の本範囲内の実装詳細修正はB適応として進める。

## 6. 所見なし実装結果・再検証への引渡し

status: ユーザー承認済みの別slice observation-progress-repairを依存として追加し、両修復を含む新最終headでM1〜M4を再検証する。旧凍結A3 v1は変更しない。別sliceのO1〜O4と許可範囲はOBSERVATION_PROGRESS_REPAIR.mdに固定。旧review記録はcodex/bs3-burst-repairの4c3b53cbd03cb34fb2448d006a548bbee3b5d451に保全。本書の前向き中立化は証拠削除/履歴改変ではない。

判定headは新sliceのmanifestで固定する。旧headの結果は新headへ流用しない。H2c本体CURRENT/A3は変更せず、再開担当へsource修復の合流と新A revision/base/specの調整、全必須再検証を引き継ぐ。D/merge/push/PRなし。
# BS3/Burst repair — 所見なし実装結果

Implementation base: 7983e6884a01e01c335e92d9045dd2b34bea4d06。Implementation headは同梱manifestの完全SHA。branch: codex/bs3-burst-repair。
B担当: gpt-6-sol 新規session。rootはこの最終snapshotを担当のB_RESULT/B_RESULT_R2と固定ソースから組み立てた。

変更はSampleGame/Tests/Editor/Buildの4つのC#（既存1、新規3）、新規.meta3、tracked HANDOFFのみ。Runtime、公開API、asmdef、package、Scene/Prefab/assetに変更なし。

BuildContentDirectoryIntegrationTestsはHTTP/local install、source dispose、server終了を含むcallback全体を保持する。fixtureがoperationを登録した後にStartを呼び、同期callback例外もtaskへ保持する。正常iteratorはtask終端後に結果を消費する。TearDownは取消し、全task終端を待ってからdeliveryを削除する。10秒deadlineで未終了ならdelivery絶対pathを残してfailureとし、独立したfixture/copy/空親cleanupを続ける。

ContentInstallTestOperationはCTSとtaskを所有する。deadlineの取消signalとTask.WhenAnyを使い、CTSは終端確認後にdisposeする。未消費faultと取消callback例外を保持し、既に本体が消費したfaultを重複報告しない。RunWithShutdownAsyncはinstall/sourceとserver終了の双方を試み、両方のfaultはAggregateExceptionで保持、単独faultは元stackを維持する。

LoopbackArtifactServerはlistener、active response、workerを所有し、StopAsyncがworker終端を待つ。同期無期限joinを除いた。実HTTP中断用の限定seamはheadersとbody1byteを送信してからsignalで待つ。

回帰集合は、信号後取消、観測済みsource fault、未観測source fault、実HTTP response中断、deadlineによるdelivery保持、callback開始前登録と同期fault、installとshutdownの二重fault。同じ実Cleanup経路を使い、transaction再取得、stream/source/response/worker終了、staging/final、物理path/meta、非所有folder保持を確認する。回帰fixture自身のTearDownも信号解放とcleanupを試す。

実測行数: integration490（旧475）、operation100、server120、regression356。async寿命とHTTP infrastructureを分離し、既存のbuild/load/source-isolationシナリオは一つに維持。regressionの予想から6行増は同じ寿命契約の二つの失敗経路による。

B確認: git diff --check exit0、pwsh tools/contract-audit.ps1 exit0（errors0/warnings0、非H1 taskのcheck8はnot-applicable）。Editorコンパイル確認、Unity test/build、docs auditはBで未実行。Bはcommit/push/PR/mergeを行わず、commitと証拠固定をrootへ渡した。Cの結論・指摘はこのsnapshotに含めない。
