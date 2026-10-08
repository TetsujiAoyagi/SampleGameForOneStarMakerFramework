# Workflow task の終了・再開

開発 task の活動状態は repositoryId + taskId ごとに Workflow が所有します。repositoryId は GitHub origin を小文字の github.com/owner/repo に正規化した SHA-256 なので、worktree を移しても同じ task に接続します。旧 Evidence の supplied repositoryId は reset の固定対象にだけ使い、新 task の identity に流用しません。

通常の開始、完了、打切り、再開は次の入口を使います。

~~~powershell
pwsh tools/workflow-task.ps1 start -Task example-task
pwsh tools/workflow-task.ps1 status -Task example-task
pwsh tools/workflow-task.ps1 end -Task example-task -Reason completed -ExpectedVersion 1 -Decision trusted-completion-record-id
pwsh tools/workflow-task.ps1 resume -Task example-task -ExpectedVersion 2 -Decision trusted-resume-record-id
pwsh tools/workflow-task.ps1 end -Task example-task -Reason cancelled -ExpectedVersion 3 -Decision trusted-cancellation-record-id
~~~

completed は人間が task 全体の完了を決めたとき、cancelled は打切りを決めたときに、workflow 担当が通常の最終処理として記録します。Phase D の完了処理も同じ入口です。C/C′ 合格、PR 作成、merge 検知、Agent 停止、pause、timeout、Harness close から終了を推測しません。Evidence 専用の追加承認を求めません。

Decision は非秘密の信頼済み message／decision-record ID です。同じ決定を再実行するときは ID、kind、Reason、ExpectedVersion を変えません。同じ Decision に別の内容を渡すと拒否します。初回の正常な最終処理で確定した UTC は event と完了 receipt に固定し、配送時刻や再試行時刻で更新しません。外部の信頼済み終了 record を取り込む内部 Workflow adapter は、原 record の終了 UTC と指示 UTC を区別して保存します。Storage が会話や PR を探して終了を推測することはありません。

状態は Windows Known Folder LocalApplicationData の OneStarMaker/Workflow/<repositoryId>/tasks/<taskId>/ にあります。private ACL、reparse 拒否、task 単位の FileShare.None 排他、generation CAS、CreateNew／flush／rename で state・immutable event・配送 outbox を保存します。終了指示の durable record だけが存在して完了 receipt がない場合、status は pending-finalization を表示します。次の担当は同じ Decision で最終処理をやり直し、元の event があれば version と UTC を保って receipt を回復します。終了未記録の task は active/end-not-recorded です。

保存領域の実体pathを解決するWindowsStorePathsは、read-only native handleのfilesystem情報だけを扱います。Workflow storeはtaskの状態・guard・ACLを所有し、Artifactsをimportしません。通常CLIと固定runtime jobは同じ既存Workflow/guardへ接続し、jobでは検証済みのprocess内接続をmodule再import後も維持します。欠落や実体相違を別の空storeへ切り替えて隠しません。

終了・再開の commit 前には共通 guard 内で Storage の未確定 DELETE を照合します。不明な元 process／子 process／remote 結果が残れば state を変更しません。commit 後は guard を解放して配送します。配送失敗は exit 2 と recorded/delivery-pending で返り、終了 event を取り消しません。登録済み Evidence config がない場合も配送待ちです。後の publish／fetch／inspect／cleanup と毎日／次ログオンの job が同じ outbox を配送します。

Evidence config は private OneStarMaker/Artifacts/deployments/<deploymentId>.config.json と、その bytes/hash を結ぶ <deploymentId>.json の deployment receipt で登録します。同じ canonical repositoryId を使うため、別 worktree の workflow コマンドも登録済み config を解決できます。

実際に再開する担当は、Evidence 保存や利用を開始する前に resume を完了させます。再開は終了時刻を取り消し、再終了で新しい UTC を記録します。既に削除された payload は再開しても復元されません。resume は payload-deleted と deletedArtifacts の一覧を返し、event の確定と配送待ちを区別します。端末に返る時刻の override はありません。

offline 検証は tools/Workflow/tests/TaskLifecycle.Tests.ps1 です。Artifacts／Harness と同じ registered／selected／executed 名集合と failed 結果を保存し、0 件を合格にしません。
