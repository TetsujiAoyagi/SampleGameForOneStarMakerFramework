# Artifact Storage と資格情報管理

同一WindowsユーザーのPowerShell 7から、既存のDPAPI `osm`資格情報とprivate R2 bucket `osm-artifacts`を使います。通常Evidenceは明示した非秘密file集合をpublishし、別sessionで期待hashを照合してfetch・ログ/原画像を閲覧できます。taskの終了/再開は[Workflow](../Workflow/README.md)が所有し、Storageは終了から30日のコピー清掃を担当します。Build系列・別host/Cloudの保存と配布は後続スライスです。

スライスAは[PR #109](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/109)で実装・限定運用検証を完了し、同じ実装headの正式C/C′はGOです。r6は同じ既存保存領域へOS jobを接続する修正で、保存領域の移動や資格情報の複製を行いません。固定仕様・実行集合・原観測の入口は `pwsh tools/harness.ps1 current -Task artifact-evidence-lifecycle`。一回の限定resetは完了済みです。既知のACL不適合copyは保護されたままで、OS jobのpartial結果と通常schedulerの正常statusは区別して記録しています。

## エージェント操作の事前承認

ownerは、設定した開発領域・明示入力・保存契約内の通常publish/fetch/清掃を事前承認しています。日常清掃は、対応機能の実装・検証と運用切替の成立後に行います。一回のresetはA3で固定した対象/除外と開始条件を照合して実行し、文書改訂だけで開始しません。通信や同一ユーザーの既存鍵利用だけを理由に再確認せず、keyごとの削除承認や旧保存価値の審査を追加しません。Agent製品・モデルによらず適用します。

対象は同じaccountの検証済みendpoint/private bucketと新Evidence prefixです。公開development URL/custom domainを有効にせず、prefix分離を公開境界とは扱いません。非秘密内容、固定版、blind入力と所見の分離は送信担当が確認します。purpose宣言・禁止名検査・hash一致だけで秘密不存在を証明したとは扱いません。取得したscript/DLLを実行しません。

資格情報の新規登録・置換・rotation・失効/削除、別account/bucket、公開配布、別host/Cloudは通常操作の承認範囲外です。鍵や暗号化store、header、署名URLは会話・引数・環境変数・ログ・Git・同期先へ出しません。認証失敗を鍵の再入力・削除で自動修復しません。実行環境の承認UIには従い、拒否を別経路や設定緩和で迂回しません。

## Evidence v2 の通常操作

3 projectのRelease buildを用意します。通常Evidenceの使用開始前に `workflow-task.ps1 start`、終了済みtaskなら `resume` を完了します。新configはschemaVersion 2、profile、endpoint、bucket、repositoryId、prefix、policy、deploymentIdの厳密なJSONです。

```json
{"schemaVersion":2,"profile":"osm","endpoint":"https://<32hex>.r2.cloudflarestorage.com","bucket":"osm-artifacts","repositoryId":"<64hex>","prefix":"development/evidence/v2/","policy":"task-end-30d-v1","deploymentId":"<32hex>"}
```

repositoryIdはworkflowと同じrepository identity、deploymentIdは切替時に固定したidentityです。private deployment registryでconfig hash、既存設定baseline、ownerの非秘密観察recordを結びます。未知deploymentや旧configはnetwork前に拒否します。変更しないprivate設定・writer scope・公開URL無効等は既存baselineとownerの不変申告を継承し、毎runの再撮影・24時間更新・witness破壊試験は要求しません。通常writerに管理権限を追加せず、管理者の途中変更までは保証しません。

selectionはschemaVersion 2、purpose `evidence`、taskId、absolute root、明示relative files、40hex base/headを持ちます。taskIdは小文字英数字/hyphenの1〜64字です。1〜4096fileを明示し、再帰収集しません。

```json
{"schemaVersion":2,"purpose":"evidence","taskId":"sample-task","root":"<absolute-source-root>","files":["result.log","original.png"],"base":"<40hex>","head":"<40hex>"}
```

```powershell
pwsh tools/workflow-task.ps1 start -Task sample-task
pwsh tools/artifacts.ps1 evidence publish --profile osm --config <v2-config> --selection <v2-selection>
pwsh tools/artifacts.ps1 fetch --profile osm --config <v2-config> --reference <trusted-reference> --sha256 <trusted-package64>
pwsh tools/artifacts.ps1 evidence inspect --profile osm --config <v2-config> --task sample-task
```

publishはsourceをread lockでsnapshotし、同bytesからmanifest/ZIP/hashを作ります。固有key `development/evidence/v2/<repositoryId>/<taskId>/<artifactId>/bundle.zip` のintentを固定して1回PUT、別processでGET・外hash/manifest/全entry・版を照合し、永続receipt/catalogを確定した後だけ、並列fieldのreferenceとpackageSha256を返します。別の手動commitは不要です。PUT応答不明を同key再送せず、照合できるまで未確認としてresidueを残します。

referenceはopaque `osm-evidence-v2:` です。fetchは別に渡された期待packageSha256を照合し、manifest/全entryを検証したprivate新規copyのoutputPathを返します。referenceから期待package hashを補いません。相対path逸脱/reparse/重複/case衝突を拒否し、ZIP 256 MiB、JSON 1 MiB、entry 4096、単一展開256 MiB、総展開1 GiB、圧縮比100を上限とします。

旧v1 reference/configと旧E1/E2、synthetic publish、独立Evidence commit/OwnerCloseの通常入口は非対応です。旧payloadの救済・自動変換・継続取得を行いません。内部のsynthetic transportは既存資格情報rotation/診断/offline検証に限ります。

## 終了・再開・利用中と清掃

workflow担当は、人間がtask全体の完了/打切りを決定した通常最終処理で、同じ信頼済みDecisionからendを記録します。Evidence専用のClose承認は作りません。Agent停止・pause・失敗・timeout・C/C′合格・PR検知・Harness closeから終了を推測しません。

```powershell
pwsh tools/workflow-task.ps1 status -Task sample-task
pwsh tools/workflow-task.ps1 end -Task sample-task -Reason completed -ExpectedVersion <n> -Decision <trusted-decision-id>
pwsh tools/workflow-task.ps1 resume -Task sample-task -ExpectedVersion <n> -Decision <trusted-decision-id>
pwsh tools/artifacts.ps1 evidence use --profile osm --config <v2-config> --reference <v2-reference> --consumer <id> --until <UTC> --reason <non-secret-text>
pwsh tools/artifacts.ps1 evidence release --profile osm --config <v2-config> --reference <v2-reference> --protection <id>
pwsh tools/artifacts.ps1 evidence cleanup --profile osm --config <v2-config> --dry-run
pwsh tools/artifacts.ps1 evidence cleanup --profile osm --config <v2-config>
```

Workflowはtask state/event/outboxを一つのatomic更新で記録し、commit後にStorage syncを一度呼びます。失敗は `recorded/delivery-pending` で、endを取り消しません。publish/fetch/inspect/cleanupの入口と日次jobもoutboxを再配送します。同Decision再送は初回の版/UTCを保ち、欠番・同版不一致・不明なStorage状態では削除しません。終了の取りこぼしは同じDecisionで通常最終処理を再開し、未完をpending-finalizationとして扱います。

taskEndedAtは実終了処理で一度固定したUTC、deleteEligibleAtはそこから2592000秒です。未終了taskはupload後100日でも年齢だけで消しません。GET/inspect・同event再送・古いPRリンクは延長になりません。resumeは利用前に行い、削除資格を取り消します。次のendから新たな30日です。削除済みcopyはresumeで復元せず、payload-deletedを返します。

fetch中は一時operation-useを取得します。取得後も調査/reviewで使うcopyは有限untilをuseで明示し、releaseか満期で終えます。use失敗を保護成功と扱わず、利用を開始しません。破損/不明なprotection、転送中、process/pipe EOF未確認、削除/PUT intent不明は非削除です。lease/heartbeat/周期的owner承認は追加しません。

inspect/dry-run/DELETE直前は同じ純粋policyを使います。task guard内でeventを連続版まで適用して再判定し、DELETE後の404 NoSuchKey確認でtombstoneを残します。remote/local部分失敗は別stateと残件を返し、次回は未確定だけを再検査します。403/409/timeoutはblocked/delete-pendingとして管理設定を変更しません。削除不明の間はresume/use/publishを成功にしません。

Storage所有のupload/staging/未採用失敗copyはcreatedAtから604800秒の7日清掃です。転送中、採用pending、レビュー入力採用、有限use、削除不明は除外します。採用する失敗ログは通常publishが成功する前に元stagingを消しません。Storage copyはtaskの同じ30日期限で管理し、利用者/Harnessの原sourceや利用者が作ったcopy、資格情報、他task/無関係領域を清掃対象へ登録しません。

exit 0は検証・確定完了、1は失敗/未確認、2は配送待ち・scheduler/cleanupの部分保留です。inspectのactive/end-not-recordedやcleanupのdeleted/skipped/blocked/remaining/itemsを確認し、未確認を成功へ置き換えません。例外本文やSDK応答本文は表示せず、非秘密reasonCode/residueを使います。資格情報の既存exit意味は変えません。

## 自動清掃

```powershell
pwsh tools/artifacts.ps1 evidence schedule install --profile osm --config <v2-config>
pwsh tools/artifacts.ps1 evidence schedule status --profile osm --config <v2-config>
```

同じownerのTask Scheduler `OSM-Evidence-Cleanup-<repositoryId先頭12>` は毎日03:00 localとlogon、StartWhenAvailable、IgnoreNew、InteractiveToken/Limitedで動きます。password/親鍵は登録しません。未ログオン中は動かず次ログオンで追いつくため、期限は削除資格であり即時削除SLAではありません。

installは最終implementation headのscript/module/DLL/deps/configをKnown Folder `OneStarMaker/Artifacts/runtime/<head>/`へprivate copyします。runtime manifest v2は五つの既存専用領域（Workflow、Evidence、deployment、transfer、runtime）の実体path・volume/file identityとownerを固定します。WindowsのAppData仮想化で論理pathと実体が異なる場合も、新しい保存先へ移さず同じファイルを参照します。actionのpwsh/entry/runtime/作業directoryは実体の絶対pathで、可変worktreeに依存しません。

jobはruntime全bytes/hashとowner、五領域の実体/ACL/reparseを検査してからconfig・guard・catalog・通信を使います。固定領域が欠落・相違する場合は空storeを作ったり別領域へ戻ったりせず停止します。同v2内の既存receipt/intent/転送marker/copyの絶対参照は、固定role内の同実体だけをI/O時に解決し、原recordを一括書換えません。既存資格情報のroot・暗号文・DPAPI利用は変更しません。同名taskのidentity不一致、runtime/config欠落/hash不一致では上書き・削除をせずfailedです。

一回のjobは10分以内、配送2分/100task、清掃8分/100objectを予約します。両段階の独立永続cursorは失敗項目も進め、残件を次回へ回して先頭障害による飢餓を避けます。busyはskip、network不明は次回へ保留し、成功Evidenceを再publishしません。statusはlastRun/nextRun/resultを非秘密情報で返します。

## 一回の旧開発環境reset

A3のfixed reset manifestにexact key/path/rule、hash、対象所有・利用状態、除外を固定してから旧利用を停止します。E1/E2・witness・試験W/L・旧owned生成物は固定一覧内だけが候補です。名前/prefixだけで再帰全削除せず、未知対象を自動追加しません。credentials、新v2/runtime、利用者/Harness原source、無関係bucket/rule、他worktreeは除外します。

設定baselineは既存recordとownerの「変える以外は以前と同じ」という申告を継承します。管理画面/APIをAIが読んだり操作したりせず、C担当がexact rule ID/prefix/条件/除外を短い手順へまとめ、owner本人へ変更を依頼します。ownerの変更結果とその他設定不変のテキストを独立verification receiptへ結び、凍結manifest自体を書き換えません。スクショや全collection再hash、変更しない設定の再証明は要求しません。

```powershell
pwsh tools/artifacts.ps1 evidence reset --profile osm --manifest <frozen-reset-json> --manifest-sha256 <trusted64> --owner-verification <owner-text-receipt> --owner-verification-sha256 <trusted64> --dry-run
pwsh tools/artifacts.ps1 evidence reset --profile osm --manifest <frozen-reset-json> --manifest-sha256 <trusted64> --owner-verification <owner-text-receipt> --owner-verification-sha256 <trusted64>
```

exact対象のDELETE→GET404と、validated owned local pathの清掃結果をitem別receiptへ残します。確定削除済みの対象は再実行でskipし、残件だけを再照合します。無条件bucket全erase、rule自動解除、通常writerへの管理権限追加は行いません。対象/除外影響不明や利用中は止め、scope追加が必要ならA3へ返します。

C担当は内部DELETE/404・local非存在と別に、削除対象key/ruleの一覧をownerへ一括提示し「消えたか」の外部テキスト観察を取得します。実行前の計画を消去完了と記録せず、partial/inconclusiveを残します。過去PRの保持日・参照状態を撤回済み旧保存義務として復活させません。

## 資格情報の操作

```powershell
pwsh tools/artifacts.ps1 credentials set --profile osm
pwsh tools/artifacts.ps1 credentials status --profile osm
pwsh tools/artifacts.ps1 credentials set --profile osm --replace
pwsh tools/artifacts.ps1 credentials remove --profile osm
```

登録と置換は対話端末で行います。Access Key IDとSecret Access Keyは画面に表示せず入力を受け付けます。リダイレクトされた入出力と `pwsh -NonInteractive`（`-noni`）での起動は、入力を求める前に拒否します。鍵をコマンド引数、リダイレクトした標準入力、ファイル、環境変数から渡すことはできません。通常の登録は既存プロファイルを拒否し、置換には既存プロファイルが必要です。バケットはosm-artifactsに固定し、接続先とトークンの参照情報は未設定のままです。

非対話指定の判定は [PowerShell 7.6.5 のホスト解析](https://github.com/PowerShell/PowerShell/blob/v7.6.5/src/Microsoft.PowerShell.ConsoleHost/host/msh/CommandLineParameterParser.cs)の `GetSwitchKey` / `MatchSwitch` と [IsDash](https://github.com/PowerShell/PowerShell/blob/v7.6.5/src/System.Management.Automation/engine/parser/CharTraits.cs) に合わせています。前後の空白、`noni` から完全形までの略語、大文字小文字、単一slash、ASCIIハイフン・en dash・em dash・horizontal barの単一または同じ文字の二重接頭辞（`--noni` など）を扱います。引数全体を調べ、後続の `-Interactive` で打ち消された指定も安全側で拒否します。PowerShellを更新するときは、この解析規則との一致を再確認してください。

保存先はWindowsのLocalApplicationData Known Folder配下の OneStarMaker/Artifacts/credentials です。保存レコード全体をDPAPIのCurrentUserで暗号化し、資格情報専用のフォルダとファイルはACLの継承を切って、所有ユーザー・SYSTEM・Administratorsだけに権限を与えます。既存の共有OneStarMakerフォルダや、その中の別機能のデータ・権限は変更しません。暗号化ファイルもcheckoutや同期フォルダへコピーしないでください。

保存先の祖先にreparse pointがある場合や、専用領域のACLが安全でない場合は操作を拒否し、平文保存へ切り替えません。共有OneStarMakerが未作成の場合は、その新規作成時にも制限ACLを設定します。既存共有親の扱いとは異なります。

新規に作る専用ディレクトリとファイルは、ACLだけでなく所有者も実行ユーザーへ明示します。管理者権限の端末などでWindowsの既定所有者がAdministratorsになる場合でも、直後の安全検査と一致させるためです。既存の所有者不一致ディレクトリは自動修復しません。

DPAPIはWindowsユーザーに結びつけて保存データを保護しますが、同じユーザー権限で動くAgentや別のプログラムからは復号できます。同ユーザーのAgentを隔離する仕組みではありません。今回、別Windowsユーザーによる復号拒否の実測は未確認です。他のPCへのファイルコピーを資格情報の移行手段にせず、その端末専用の鍵を用意する運用とします。

状態表示はレコードを復号・検証し、安全なローカル情報だけを返します。鍵の値やR2接続の成功、接続確認日時は表示しません。終了コードは、0がローカル操作成功、2が置換確定済み・一時ファイルの清掃保留、1が失敗です。

置換は候補を暗号化して再検証してから、同一フォルダ内で原子的に切り替えます。切替え前の失敗では旧レコードを保持し、切替え後の清掃失敗を「置換されなかった」とは扱いません。現行レコードが欠落・破損しているときは失敗し、バックアップを自動復元しません。候補やバックアップを手動で現行ファイルへ改名せず、保存先と権限の状態を確認してください。

削除は対象プロファイルの現行ファイルと、厳密な命名規則に合う所有一時ファイル・バックアップだけを対象とします。すでに削除済みでも成功します。ローカル削除によってCloudflareのトークンは失効しません。サーバー側の失効は所有者が別途行います。

安全でないACLのレコードは削除も拒否されるため、暗号文が残る場合があります。失敗を削除済みと扱わず、所有者が保存先と権限を確認してください。ディレクトリ全体や他機能のデータを清掃対象にしないでください。

`credentials rotate` は新しい鍵を対話端末でmasked入力し、使い捨てsynthetic keyへのPUT/GET/hash/DELETE/不存在を検証してから、新世代をactive、旧世代をretiredに原子的に切り替えます。切替え後は失効待ちでexit 2を返します。所有者が管理画面で対象の旧tokenだけを失効した後、非秘密の観察記録を使って`confirm-revocation`を実行します。旧鍵の401/403と安全な拒否code（`InvalidAccessKeyId`、`InvalidToken`、`AccessDenied`）、または同じ応答の正確な401/`unauthorized`/`Unauthorized`と、新鍵の前後の陽性対照が揃って初めてretiredを清掃します。pending中は通常のset/removeと次のrotateを拒否します。失効前のcrashではpendingを保ち、壊れたactiveをbackupから自動復元しません。旧鍵の失効や、新鍵を使えない場合の再発行・masked再登録は所有者が管理画面と本CLIで明示的に行います。ローカルのstatusやset成功をR2接続成功とは扱いません。

## Synthetic Route proof

`Probe/RouteProof.ps1` はsynthetic専用の限定診断です。親processはendpointとレビュー対象の`-ImplementationBase` / `-ImplementationHead`（40桁小文字hex commit ID）を受け取り、観測へ固定値を記録します。作業ツリーのHEADやdevelopとのmerge-baseを実行時に推測しません。`probe/unlocked/<run-id>/` と `probe/locked/<run-id>/` の各1 key、1操作1子`pwsh` process、各操作30秒・子process45秒・run全体5分の期限を使います。各childはrun-id/keyとrevisionに加え、PUT fixtureの長さ・hash・run marker・original/changed識別を検証し、PUT以外のpayloadを拒否します。子processの引数に鍵を渡さず、`CredentialStore` の `Invoke-CredentialTransport` が同一process内の一回のcallbackへDPAPI復号値を限定して渡します。callbackから戻るのは閉じた非秘密transport観測だけです。親processは子と同じ単一JSON行をschema検証し、stdoutとstderrは子45秒の共有残予算で順に回収します。期限超過後はprocess終了と両pipeのEOFを確認できるまで回復cleanupを送りません。offline testsは同じ12操作主ループとJSON境界を通して成功完走・期限・結果照合・cleanup分類を検査します。

transportは`Probe/R2RouteTransport.csproj`の固定`AWSSDK.S3`依存を使います。認証PUT/GET/DELETEと、Authorizationおよび署名queryを付けないHTTP GETを分離し、本文は保存せず、EOF確認時だけ全体hash、期待長に達した場合だけ先頭hashを返します。8193 byte目に達しても、それ以前に得た期待長prefix hashは残し、EOF未確認の全体hashは作りません。期限中の取消しやEOF前のI/O切断でも、到着済みprefix hashを保ち、全体hashとEOF確認は未確定にします。unsigned応答のS3 `Code`要素は短い安全なcode値だけを逐次抽出し、Messageや本文全体は保持しません。閉じた非秘密観測にunsigned requestのmethod、正規URI、Authorization/署名query/対象queryの有無を加え、RouteProofが意図したGETか照合します。401/`unauthorized`/`Unauthorized`、403/`forbidden`/`AccessDenied`、またはR2で観測した400/`other`/`InvalidArgument`という同一応答内の組を拒否証拠として許可し、交差したstatus/codeは許可しません。400は実際のstatusのまま記録し、正規GET・EOF・非露出hashの条件も同じく要求します。一度のprefix一致は内容露出として記録しますが、再現条件を満たすまではprovider capability failureと確定しません。Bucket Lock拒否は403/`forbidden`/`ObjectLockedByBucketPolicy`または実測した409/`other`/`ObjectLockedByBucketPolicy`の同一応答tupleだけを許可し、EOF・期限・redirect・本文上限と同一writerの条件を維持します。上書きとDELETEの双方の拒否後、最終の認証GETで元のbyte数・hashが一致して初めてprobeは完走します。locked overwrite/deleteの成功応答だけでは保持機能の失敗へ昇格せず、状態確認できない場合は`inconclusive`です。期限後に子processの停止を確認できなければ、競合する回復DELETEを送らずcleanupを未確認にします。SDK例外のMessage、HTTP本文、request/header、秘密は結果へ通しません。`artifacts/` は生成物でGit管理外です。

実行前にownerが確認した`probe/locked/`の全有効ruleの非秘密記録を使い、次の10 fieldの秘密を含まない事前JSONを用意します。`BeforeHash`は実行前の原記録のSHA-256です。事後値は入力せず、probe出力の`lockRule.afterHash`は`null`です。実際のAfter設定は実行後に独立取得した原記録で照合します。通常writer tokenへBucket設定権限を追加しないでください。

```powershell
dotnet restore tools/Artifacts/Probe/R2RouteTransport.csproj
dotnet build tools/Artifacts/Probe/R2RouteTransport.csproj -c Release -o tools/Artifacts/Probe/artifacts/route-transport
pwsh -NoProfile -File tools/Artifacts/Probe/RouteProof.ps1 `
  -Endpoint https://<32-hex-account-id>.r2.cloudflarestorage.com `
  -ImplementationBase <40-hex-base-commit> `
  -ImplementationHead <40-hex-implementation-head-commit> `
  -LockRuleJson '{"Prefix":"probe/locked/","Enabled":true,"Kind":"Age","RetentionSeconds":86400,"RuleCount":1,"DateRules":0,"IndefiniteRules":0,"WriterCanConfigure":false,"LifecycleCompatible":true,"BeforeHash":"<64-hex>"}'
```

## 保守とoffline検証

資格情報はCLI→CredentialCommands→CredentialStore→PathAcl/Recordの一方向です。EvidenceApplicationはpublish/fetch/use orchestration、EvidenceContractはv2 codec/deployment、EvidencePathsはtask transaction/receipt、EvidenceRetentionPolicyは純粋判定、EvidenceCleanupは配送/削除/reconcile、EvidenceScheduleは固定runtime/OS adapter、EvidenceResetは一回の限定清掃を所有します。Workflow core/storeはArtifactsをimportせず、CLI composition rootが共通guardとpreflight/syncを結びます。

H1のこのtaskは3 .NET Release build、全Artifacts PowerShell parse、11 Artifact suite、Workflow TaskLifecycle、Harness、適用taskのcontract/docs auditを固定5 stepで収録します。registered/selected/executedは各非空同集合、failed 0、実DLL path/hash/MVID/depsと生結果を保存します。B exitはdiscovery、最終Cはjudgment、CBlindは同base/headと同じ固定入力です。Unity source/依存を変更しないためUnity test/buildはこのsliceに適用しません。

```powershell
dotnet build tools/Artifacts/Probe/R2RouteTransport.csproj -c Release -o tools/Artifacts/Probe/artifacts/route-transport
dotnet build tools/Artifacts/Packaging/ArtifactPackaging.csproj -c Release -o tools/Artifacts/Packaging/artifacts/package
dotnet build tools/Artifacts/Transport/R2ArtifactTransport.csproj -c Release -o tools/Artifacts/Transport/artifacts/transport
pwsh tools/harness.ps1 current -Task artifact-evidence-lifecycle
pwsh tools/harness.ps1 run -Task artifact-evidence-lifecycle -Stage discovery -Difference 初回 -Question '固定offline集合が通るか' -StopWhen '全必須集合を収録'
pwsh tools/contract-audit.ps1 -HarnessTask artifact-evidence-lifecycle
pwsh tools/docs-audit.ps1
```

PR #106の非秘密dummy fixtureをv2に再利用します。各testは隔離store、clock/transport/scheduler注入とbarrier/signalを使い、30日待機やTask.Delay/Thread.Sleep、秘密や実E1 source、実R2 DELETEを必要としません。DPAPI/ACLの成立はowner Windowsユーザーで確認し、別sandboxユーザーの結果を流用しません。synthetic診断と過去first-useの成功を新v2、scheduler、resetの成立証拠にしません。
