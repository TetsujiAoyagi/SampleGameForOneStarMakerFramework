# Local Harness H1

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
