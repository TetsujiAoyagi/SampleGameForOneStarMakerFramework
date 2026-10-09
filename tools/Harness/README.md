# Local Harness H1 / H2c

## Unity test runner の機械結果

`pwsh tools/run-tests.ps1` は Unity EditMode テストの標準入口。`-Filter` を省略すると全 EditMode、指定すると限定実行になる。既存の `-Platform`、`-UnityRoot`、`-UnityExe`、`-WithGraphics` も使える。外部保存先は `-OutputRoot '<path with spaces>'` で指定し、省略時はリポジトリ直下の `TestResults` を使う。

```powershell
pwsh tools/run-tests.ps1 -OutputRoot 'C:\Test Evidence\Unity'
```

各 invocation は保存先の新しい GUID 子ディレクトリを所有し、`results.xml`、`unity.log`、`step.json` を置く。従来の `TestResults` 直下の XML glob は使えない。

保存後の marker は固定 prefix `UNITY_TEST_RESULT`、ASCII 空白 1 個、行末までの `step.json` 絶対 path で構成する。path 自体にも空白があり得るため、空白で分割せず prefix と空白 1 個を除いた行末全体を取得先として使う。出力先確保や step 保存自体に失敗した場合は exit 1 で marker が出ない。途中終了で step がない run は未完了であり、XML だけでは採用しない。

成功判定は marker ではなく step の `status=passed` と `exitCode=0` を読む。runner の終了コードも同じ合否を返す。完成した Passed XML があっても、`unity.log` に `error CS\d+` が大小文字を問わず部分一致すれば failed にする。

step v1 は今回の条件、Unity 実行ファイルと要求版、process の終了、XML の leaf 件数と結果、compile error、ログの hash を保存する。`cases[].reason` は XML の `reason/message` だけを写し、一般の失敗本文を表す欄ではない。`failure/message` を含む詳しい失敗内容は同じ invocation の生 `results.xml` を参照する。

`durationMs` は invocation 全体の単調時計の経過、`timing.processDurationMs` は process 待機、XML の時刻と duration は原値である。Unity 内 marker と実ロード assembly は `unknown` と記録する。結果の対象集合が期待集合を満たすかという gate 判定や H1 の CURRENT/receipt への接続は、この runner にはない。

## Unity 内観測（EditMode）

`-ObserveUnity` は明示した EditMode invocation だけに Unity 内の観測を要求する。`PlayMode` と組み合わせた場合は Unity を起動せず failed step v2 を保存する。既定の非観測実行は従来の step v1、marker、argv、logHash、終了判定を使う。

```powershell
pwsh tools/run-tests.ps1 -ObserveUnity -Filter 'OneStarMaker.Tests.Editor.TestObservation.ObservationStateTests'
```

観測実行は同じ GUID ディレクトリに `observation-progress.json` と追加専用の最終 `observation.json` を保存する。step v2 の `process.id` は runner が起動した process の PID、`observation` は両ファイルの絶対 path・SHA-256・機械判定を持つ。`logs` の順序は `results.xml`、`unity.log`、`observation-progress.json`、`observation.json` で、実際に読めたファイルだけを含む。`logHash` はこの順に各 raw bytes の大文字 SHA-256 を `|` で連結し、その UTF-8 の小文字 SHA-256 を取る。step 自体と private `reload-settings.json` は含めない。

`observation.json` の schema v1 は、invocation/project/PID、初回選択 leaf、started/finished callback、終端 leaf、domain と単調時計、実ロード assembly の FullName・Location・MVID と取得時の disk SHA-256 を記録する。選択 leaf の ID はその invocation 内だけで有効である。disk SHA-256 は取得時のファイル値で、ロード済みメモリ bytes の hash や import/準備時間は示さない。正常 reload では最初の選択を progress から復元し、domain ordinal と beforeReload/復帰を残す。

観測 schema は未知のキーと必須キーの欠落を拒否する。assembly は開始時、実行中の各 reload 復帰時、終了時に取得し、各取得点で必須 assembly が揃っていることを確認する。別の取得点の assembly を補って成立させることはできない。

観測の `complete` は記録の整合性と必要な callback が揃ったことを表し、テスト合格とは別に判定する。Failed のテストでも必要な記録が揃えば観測は complete になり、step は XML policy により failed になる。callback が欠けた未実行テストなどは観測も不成立となり、`terminal.failure` の拒否理由を step に残す。

観測 required の exit 0 は、従来の XML/log policy と、同じ invocation の sidecar/progress、PID、seal、callback/終端/XML の leaf 集合、assembly、時計、観測器 error log veto がすべて合格し、Unity process が exit 0 の場合に限る。RunFinished 時点は候補で、終了時 seal が欠ける・書込が失敗する・seal 後に callback が来る場合は exit 1 とする。正常 reload の限定検証には `OneStarMaker.Tests.Editor.TestObservation.ObservationReloadTests.RealDomainReloadRoundTrip` を、故意欠測の限定検証には `OneStarMaker.Tests.Editor.TestObservation.ObservationFaultFixture.IntentionalMissingCallback` をそれぞれ単独 `-Filter` で使う。故意欠測 fixture は通常の全件では正常テストとして走り、明示単独 filter の観測時だけ callback を一つ落とす。

reload fixture が Enter Play Mode 設定を変更する際は、変更前の値を同じ GUID ディレクトリの private `reload-settings.json` に保存し、fixture 終了時または Editor 終了時に復元する。復元失敗は観測失敗として残る。このファイルは設定復旧資料であり、test 結果の別正本ではない。

設定復旧資料が欠落・破損している、別 invocation に属する、または復元未完了の場合は seal を拒否する。強制終了や Editor crash、実行途中にディスクへ保存された ProjectSettings の自動復旧は保証しない。終了時の復元処理と前後の正常値確認だけでは、これらの復旧を証明できないため、調査時は private 設定復旧資料を保持する。

非観測実行でも reload fixture は Play Mode に入るが、Enter Play Mode 設定を変更しないため、実際の domain reload を保証しない。reload の証明には、上記の単独 filter と `-ObserveUnity` を使い、同じ invocation の reload 境界と復帰を確認する。

H1はA3で適用を明示したArtifacts/Harness作業だけを扱う。同じWindowsユーザー・同じマシンの別worktreeから、task IDでGit外の現行入力を選ぶ。Unity変更、live Route Proof、別マシン配布、証拠削除はこの版の対象外。

```powershell
pwsh tools/harness.ps1 status
pwsh tools/harness.ps1 current -Task h1-transport-identity
```

新規taskの `init` は人間が承認したA3 snapshotとownerを受け取る。既存taskのCURRENTが欠落したら `init` で上書きせず `restore` を使う。registrationが欠落・破損した場合は`restore`も使えないため、statusの診断に従ってtaskディレクトリと原本を確認し、手動で復旧を判断する。存在するCURRENTが壊れている場合も上書きせず停止する。statusにはtask IDと健全性を表示する。保存先はLocalApplicationDataの `OneStarMaker/Harness/<repo-id>/tasks/<task-id>`。端末喪失へのbackupはH3で扱う。

H1では `h1-transport-identity` と `artifact-evidence-lifecycle` のA3 snapshotをtask別に承認し、snapshot本文のSHA-256と構造値を実装内の承認値と照合する。別taskを始める場合はA3で仕様と承認値を追加する。CURRENTの未解決・blocker・次作業は `current -ExpectedRevision <n> -Unresolved ... -Blockers ... -NextAction ...` で更新し、仕様本文は更新できない。
`current` は凍結仕様と採用runの固定レコードについて、保存先・hash・実行時刻・読み込んだバイナリのpath/hashを表示する。別Agentはrun IDだけを手掛かりに過去RESULTを探索せず、その固定レコードを照合できる。

```powershell
pwsh tools/harness.ps1 init -Task h1-transport-identity -Owner OSM-maintainer -SpecFile <approved-snapshot>
pwsh tools/harness.ps1 restore -Task h1-transport-identity
```

実行は固定suiteだけ。初回は `-Difference 初回`、再実行は前回run IDと前回との差を指定する。質問と停止条件が空なら実行しない。`-ImplementationResult` は所見を含まないBの操作観察を固定runへ残す任意欄。`discovery` はB exit、`judgment` は判定Cの広いsuite。途中commitにtest gateを要求しない。dirtyな実行はtrialとして記録できるが、完了引渡しに使えない。

```powershell
pwsh tools/harness.ps1 run -Task h1-transport-identity -Stage discovery -Difference 初回 -Question '変更面の回帰が通るか' -StopWhen '必須caseが全て実行された'
pwsh tools/harness.ps1 adopt -Task h1-transport-identity -RunId <id> -Reason 'この候補のB確認に採用'
pwsh tools/harness.ps1 handoff -Task h1-transport-identity -To CDiscovery -RunId <id>
pwsh tools/harness.ps1 assist -Task h1-transport-identity -Reason '失敗runの調査を依頼'
```

判定Cには `run -Stage judgment` のrunを渡す。CBlindはCJudgmentが確定した同じ入力ID/hashを読み、別の固定receiptとレビュー参照を残す。assistは常にWIPでready=false。closeはownerがレビュー終了を明示し、closed.jsonの時刻を一度だけ固定してからCURRENT表示を更新する。H1は保持期限を記録するだけで削除しない。

他のconsumerや欠陥が参照するrunは `reference -Action Add` でowner/目的/有限期限を登録し、ownerが `-Action Release` または `-Action Extend` で扱う。期限切れの有効参照があればcloseを拒否する。close時にはtask自身の参照を終了し、他consumerの参照は残す。

```powershell
pwsh tools/harness.ps1 handoff -Task h1-transport-identity -To CJudgment -RunId <id>
pwsh tools/harness.ps1 handoff -Task h1-transport-identity -To CBlind -RunId <id>
```

H1適用taskの契約検査は `pwsh tools/contract-audit.ps1 -HarnessTask <id>`。task指定なしの検査は未移行作業用で、新規証拠追加の検査をしない。Gitの途中commitに生成証拠を追加して後で削除しても、適用taskでは拒否する。

## Evidence lifecycle のtask限定gate

`pwsh tools/harness.ps1 current -Task artifact-evidence-lifecycle` を実装入口にする。A3本文hash・固定base・変更scope・discovery/judgmentのstep集合は `ApprovedSpecifications.psm1` に登録し、init/current/run/handoffで再照合する。tracked HANDOFFからGit外specへ投影した削除だけを許し、新しい仕様・RESULT・rawをGitへ追加しない。旧H1とH2の集合やclose時計は変えない。

このtools専用taskの5 stepは `artifacts-evidence-local`、`workflow-local`、`harness-local`、`contract-audit`、`docs-audit`。Artifacts stepはProbe/Packaging/Transportの3 Release build、全PowerShell parse、Credentials/RouteProof/R2RouteTransport/ArtifactPackage/ArtifactTransfer/ArtifactRotation/ArtifactEvidence/EvidenceRetention/EvidenceCleanup/EvidenceSchedule/EvidenceResetの11 suiteを収録する。Workflowは全parseとTaskLifecycle suite、Harnessは全parseとHarness suite。suiteごとにregistered/selected/executedが同一の非空集合、failed 0であることを確認し、生sidecar/logと実ロードDLL/depsのpath/hashを固定する。通常auditは適用taskの証拠にならず、contractは `-HarnessTask artifact-evidence-lifecycle` を使う。

同じ5 stepでもdiscoveryはB exit、judgmentはCの最終head実行として区別し、discovery runをjudgmentへ流用しない。Harnessの成功はoffline gateの成功であり、R2/scheduler/resetの実観測とM1〜M5の成立判断はCが担当する。

このtaskのrunだけ、`-ObservationManifest <path> -ObservationSha256 <trusted64>` で所見を含まない非秘密原観測を接続できる。manifestはschemaVersion 1、HarnessのrepositoryId、taskId、固定base/head、files配列を持ち、各fileはmanifest親からの相対path、role、bytes、sha256を持つ。別のStorage repositoryIdは各原観測内に記録する。作成者は内容の非秘密性と所見の分離を確認する。所見/判断role、未知field、path逸脱/reparse/case衝突、hash/版不一致を拒否する。manifestは1 MiB、fileは4096件、単一256 MiB、総1 GiBまで。

原bytesをrunのprivate `payload/<run>/observations/` に追加専用でコピーし、manifest path/hashをrun/ImplementationResultへ固定する。引渡し時は全fileを再検査し、CURRENTは採用runのmanifest参照を表示する。CBlindはCJudgmentと同じ入力ID/hashと同じmanifest参照を使い、Cの所見を追加しない。同一hostの別worktree/sessionまでを扱う。
## H2c Unity pilot

旧 `h2c-unity-gate` と再開task `h2c-unity-gate-r2` の二つの明示entryを承認している。現在の実装入口はr2で、旧taskのCURRENT・specification・run・承認値を保持し、旧入口は読取のみとする。A3本文のSHA-256、完全base、`unity-pilot-gates-v1`、`external-current-v1`、固定profileと変更pathはtask別に `ApprovedSpecifications.psm1` にあり、init/current/run/handoffで再照合する。Git外の登録JSONを承認元にしない。H1の登録・run・非観測step v1はそのまま扱う。

現在の入口は `pwsh tools/harness.ps1 current -Task h2c-unity-gate-r2`。r2の新規initには凍結済みh2c-a3-v2本文と承認済みbaseを使い、旧taskを上書きしない。`discovery` はrunner/observation/adapter/Harnessのoffline suiteと両audit、固定filterの11件を要求する。`judgment` はoffline suiteと両audit、Artifacts suite、空filterの全EditModeを要求する。いずれも変更pathにかかわらず必須で、H1の変更path派生gateやUnity拒否を広げない。B限定runを判定Cに、C全件runを発見Cに流用しない。

Unity adapterは `tools/run-tests.ps1 -ObserveUnity -OutputRoot <task>/payload/<run>/unity` を一回呼び、限定profileのときだけ固定 `-Filter OneStarMaker.Tests.Editor.TestObservation.ObservationStateTests` を付ける。子processだけに `SAMPLEGAME_CONTENT__RUNTIMEMODE=addressables` を設定する。標準runnerのmarkerを唯一のstep取得先とし、stdout/stderrとstep v2、XML、Unity log、progress、最終observation、private復旧資料があればそのraw bytesを同runに保持する。失敗runも取得済みrawと理由を残すが、readyにはならない。固定timeoutによる正常進行中の中断はしない。

保存済みUnity stepの引渡しは `Assert-UnityPayload` でpath/hash、runnerとrawのargv/project/PID/版、XML leaf、観測callback/clock/assembly取得地点、固定集合を再検査する。`observedAssemblies` は原observationのpath/hash参照であり、後日のLibrary DLL変更・消失で取得時の原観測を無効にしない。全件性は空filter起動と同一invocationの完全観測/XML一致、固定11件包含で判定する。未知assemblyの静的catalog照合やロード済みメモリbytesのhashは扱わない。

初回adapter実機疎通はC担当が実行・記録する。B限定許可は旧taskとr2 taskの各承認済み `unity-pilot-gates-v1` `discovery` にだけ適用する。B限定の実行が同一head、clean、承認profileを満たした場合はB exit証拠として使い、同じ11件を形式だけで二重起動しない。最終GOには判定Cの別profileで全EditModeと故意欠測負例、blind同入力の再検査が必要。WindowsのUnity実行は最初からsandbox外の承認済み経路を使い、Editor所有者とproject lockを確認する。

## 観測ファイルの保存とテスト資源の寿命

`TestObservationWriter.AtomicWrite` は invocation が所有する同じflush済みtempを使う。保存先がなければMoveを一度だけ行う。既存保存先へのReplaceは、WindowsのIOExceptionでHRESULTが `0x80070020`、`0x80070021`、`0x80070497` の場合だけ再試行する。最大5 attempt、各retry前に20ms、最初のReplace開始から100ms未満で追加attemptを開始する。これは追加retryの開始期限であり、native I/O自体の終了時間の保証ではない。

tempまたは保存先が消えた場合、部分変更を示すエラー、未分類の例外、永続競合は失敗になる。delete-then-moveへの切替や成功推測はしない。publicationとtemp cleanupの両方が失敗した場合は両例外を保持する。Progress/SaveRecoveryの同期保存だけがこの処理を使い、Terminal/CreateRecoveryは追加専用のまま。元の保存障害の外部actorやHRESULTを、日本語メッセージだけから確定していない。

SampleGameのContent Directory統合fixtureは、HTTP/local installからsource disposeとserver worker終了までを一つのoperationとして登録してから開始する。TearDownはcancel、task終端の観測、delivery削除の順に進む。10秒のdeadlineで終端を確認できなければdeliveryの絶対pathを残して失敗にし、使用中の資源を強制解放・削除しない。取消要求だけを解放完了とみなさず、未消費のoperation faultとcleanup faultは保持する。独立したfixture/copy/空親のcleanupは引き続き試みる。

## Unity終了時の既知native stallと調査の停止規則

Unity 6000.6.0f1で、全EditModeの957件Passed・skip0・観測complete後にnative終了処理が停滞する事象が確認されている。各失敗runの二つのdumpはregister/stackが同じままCPU時間が増加し、停止位置 `Unity.dll+0x37d50c6` は一致PDBで `_mi_page_free_collect+0xc6` に解決した。呼出経路は `_mi_auto_process_done → mi_theap_collect → mi_theap_collect_ex → _mi_page_free_collect`。対象heapの必要bytesがなく、heap破損の起因、double-free、Unity/OSMどちらの関与も未確定である。

製品・fixtureの寿命、native連携、観測/runner、Editor/Package環境を比較した限定調査では原因を確定できなかった。同じ実装・Editor・設定で行った正式再取得は、957件Passedと必須offline/auditの成功に加え、Unity/runner/Harnessの保持process handleがsignalになり、実exit0で自然終了した。native stallへの修復変更はなく、非再現を修復の因果証明とは扱わない。元の失敗run、dump、固定仕様・承認値はGit外の正規recordと原証拠に保持する。

終了ログや `GetExitCodeProcess=0` だけでは終了完了と判断しない。この事象ではexitCode0でもprocess handleが未signalだった。正常終了の確認には保持handleのsignalと実終了コードを併用し、強制終了した取得は成功証拠にしない。

**同じ事象の次回再開では、最初の1回だけ限定調査を行い、それ以上の原因調査を続けない。** 既存証拠との差分と同一症状かを確認し、追加取得が必要なら仮説・変更条件・観測・終了条件を先に記録して最大1回に限定する。同じ症状が続けば既知リスクとして受け入れ、追加dump、filter二分探索、無変更の全件run反復、別session/taskへの持越しで調査回数を増やさない。明示的な新しいユーザー指示なしに調査を再開しない。

この停止規則は原因調査の予算であり、raw結果の書換えや自動ゲート緩和ではない。test失敗・欠測・compile errorなど別の不成立へ受容を広げず、未終了/強制終了はそのまま記録する。timeout延長、skip、Burst無効化、無差別cache削除を修復成功にしない。実行中はunity.log等だけを観察し、progress/sidecarを開かない。人間所有processを停止せず、過去のPID用停止スクリプトを再利用しない。
