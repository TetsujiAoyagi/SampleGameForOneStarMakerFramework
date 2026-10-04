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

H1で承認済みなのは `h1-transport-identity` のA3 snapshotだけで、snapshot本文のSHA-256を実装内の承認値と照合する。別taskを始める場合はA3で仕様と承認値を追加する。CURRENTの未解決・blocker・次作業は `current -ExpectedRevision <n> -Unresolved ... -Blockers ... -NextAction ...` で更新し、仕様本文は更新できない。
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

## H2c Unity pilot

旧 `h2c-unity-gate` と再開task `h2c-unity-gate-r2` の二つの明示entryを承認している。現在の実装入口はr2で、旧taskのCURRENT・specification・run・承認値を保持し、旧入口は読取のみとする。A3本文のSHA-256、完全base、`unity-pilot-gates-v1`、`external-current-v1`、固定profileと変更pathはtask別に `ApprovedSpecifications.psm1` にあり、init/current/run/handoffで再照合する。Git外の登録JSONを承認元にしない。H1の登録・run・非観測step v1はそのまま扱う。

現在の入口は `pwsh tools/harness.ps1 current -Task h2c-unity-gate-r2`。r2の新規initには凍結済みh2c-a3-v2本文と承認済みbaseを使い、旧taskを上書きしない。`discovery` はrunner/observation/adapter/Harnessのoffline suiteと両audit、固定filterの11件を要求する。`judgment` はoffline suiteと両audit、Artifacts suite、空filterの全EditModeを要求する。いずれも変更pathにかかわらず必須で、H1の変更path派生gateやUnity拒否を広げない。B限定runを判定Cに、C全件runを発見Cに流用しない。

Unity adapterは `tools/run-tests.ps1 -ObserveUnity -OutputRoot <task>/payload/<run>/unity` を一回呼び、限定profileのときだけ固定 `-Filter OneStarMaker.Tests.Editor.TestObservation.ObservationStateTests` を付ける。子processだけに `SAMPLEGAME_CONTENT__RUNTIMEMODE=addressables` を設定する。標準runnerのmarkerを唯一のstep取得先とし、stdout/stderrとstep v2、XML、Unity log、progress、最終observation、private復旧資料があればそのraw bytesを同runに保持する。失敗runも取得済みrawと理由を残すが、readyにはならない。固定timeoutによる正常進行中の中断はしない。

保存済みUnity stepの引渡しは `Assert-UnityPayload` でpath/hash、runnerとrawのargv/project/PID/版、XML leaf、観測callback/clock/assembly取得地点、固定集合を再検査する。`observedAssemblies` は原observationのpath/hash参照であり、後日のLibrary DLL変更・消失で取得時の原観測を無効にしない。全件性は空filter起動と同一invocationの完全観測/XML一致、固定11件包含で判定する。未知assemblyの静的catalog照合やロード済みメモリbytesのhashは扱わない。

初回adapter実機疎通はC担当が実行・記録する。B限定許可は旧taskとr2 taskの各承認済み `unity-pilot-gates-v1` `discovery` にだけ適用する。B限定の実行が同一head、clean、承認profileを満たした場合はB exit証拠として使い、同じ11件を形式だけで二重起動しない。最終GOには判定Cの別profileで全EditModeと故意欠測負例、blind同入力の再検査が必要。WindowsのUnity実行は最初からsandbox外の承認済み経路を使い、Editor所有者とproject lockを確認する。
