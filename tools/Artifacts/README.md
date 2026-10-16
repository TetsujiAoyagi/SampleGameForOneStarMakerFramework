# Artifact Storage と資格情報管理

同一WindowsユーザーのPowerShell 7から、既存のDPAPI `osm`資格情報とprivate R2 bucket `osm-artifacts`を使います。通常Evidenceは明示した非秘密file集合をpublishし、別sessionで期待hashを照合してfetch・ログ/原画像を閲覧できます。taskの終了/再開は[Workflow](../Workflow/README.md)が所有し、Storageは終了から30日のコピー清掃を担当します。既存Build 1件も明示した全file集合から保存・取得でき、BS4の配布形Player出力は `build collect` の1コマンドで退避・保存できます。R2上のBuildは `build cleanup`、Contentは `content cleanup` で系列ごとに件数清掃でき、使い終わった系列は `build retire`／`content retire` で退役させてR2から消し切れます。同一PCの担当間の固定入力は `evidence stage` でR2を使わずに渡せます（[ローカル固定](#ローカル固定evidence-stage)）。別host/Cloudの配布と、ローカル固定したartifactのR2昇格は後続スライスです。

スライスAは[PR #109](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/109)で実装・限定運用検証を完了し、同じ実装headの正式C/C′はGOです。r6は同じ既存保存領域へOS jobを接続する修正で、保存領域の移動や資格情報の複製を行いません。固定仕様・実行集合・原観測の入口は `pwsh tools/harness.ps1 current -Task artifact-evidence-lifecycle`。一回の限定resetは完了済みです。既知のACL不適合copyは保護されたままで、OS jobのpartial結果と通常schedulerの正常statusは区別して記録しています。

## エージェント操作の事前承認

ownerは、設定した開発領域・明示入力・保存契約内の通常publish/fetch/清掃を事前承認しています。`evidence stage` と、`staged` artifactのfetch（local分岐）・use・release・満了清掃も同じ承認に含む通常操作です。どれもnetworkと資格情報を使いません。日常清掃は、対応機能の実装・検証と運用切替の成立後に行います。一回のresetはA3で固定した対象/除外と開始条件を照合して実行し、文書改訂だけで開始しません。通信や同一ユーザーの既存鍵利用だけを理由に再確認せず、keyごとの削除承認や旧保存価値の審査を追加しません。Agent製品・モデルによらず適用します。系列の退役（`build retire`／`content retire`）はこの通常操作に含めず、ownerが系列と指示IDを明示した範囲でだけ実行します（[系列の退役](#系列の退役build-retire--content-retire)）。

対象は同じaccountの検証済みendpoint/private bucketと新Evidence prefixです。公開development URL/custom domainを有効にせず、prefix分離を公開境界とは扱いません。非秘密内容、固定版、blind入力と所見の分離は送信担当が確認します。purpose宣言・禁止名検査・hash一致だけで秘密不存在を証明したとは扱いません。取得したscript/DLLを実行しません。

資格情報の新規登録・置換・rotation・失効/削除、別account/bucket、公開配布、別host/Cloudは通常操作の承認範囲外です。鍵や暗号化store、header、署名URLは会話・引数・環境変数・ログ・Git・同期先へ出しません。認証失敗を鍵の再入力・削除で自動修復しません。実行環境の承認UIには従い、拒否を別経路や設定緩和で迂回しません。

## 実行するprocessの条件（AppData仮想化）

`tools/artifacts.ps1`、`tools/workflow-task.ps1`、`tools/harness.ps1` は、引数の解釈とstore moduleのimportより前に、そのprocessのLocalApplicationDataへの書込みが仮想化されていないかを判定します。packaged appの書込み仮想化（例: Codexデスクトップ）の中では、新しいfileがpackage専用のLocalCacheへ置かれ、ストアが黙って二つに分かれるためです。

判定は呼出しごとに、`%LOCALAPPDATA%` の直下（ストアの外）へ一意な名前のdirectory `OneStarMaker.StoreProbe.<32桁の16進>` を、同じ名前が既にあれば失敗する方法で新しく作ります。その中に固有名のprobe fileを `CreateNew` と `DeleteOnClose` で開き、handleの実体pathを論理pathと比べます。handleを閉じるとOSがprobe fileを消し、最後に自分で作ったそのdirectoryだけを、空であれば削除します。package identityは使いません（identityの無い仮想化と、identityのある非仮想化の両方が観測されたため）。

毎回新しいdirectoryを作るのは、仮想化されたprocessの新しいfileの書込み先がpathごとに違うためです。実体側にだけあるdirectoryの中に作ったfileは実体側へ書かれるので、既にあるdirectory（以前の固定名 `OneStarMaker.StoreProbe\`、他の呼出しの残骸、ストア自体）の中でprobeすると、仮想化されたprocessでも「非仮想化」と誤ります。判定はそれらを開かず、作らず、消しません。

| 判定 | 結果 |
|---|---|
| 非仮想化 | 既存どおり実行する |
| 仮想化 | `{"status":"failed","reasonCode":"virtualized-store"}`、exit 1 |
| 判定不能（probe directoryの作成失敗・同名の既存、probe fileの作成・実体path照会・closeの失敗） | `{"status":"failed","reasonCode":"store-context-unknown"}`、exit 1 |

probe directoryの削除に失敗しても判定は変わりません。processの強制終了や削除の失敗で残るのは、空の `OneStarMaker.StoreProbe.<32桁の16進>` だけです。名前は二度と使われないので後の判定に影響せず、ストアの一部でもありません。判定は他の呼出しの残骸を清掃しません（名前の接頭辞でまとめて消すと、並行して動いている呼出しのdirectoryを消しうるため）。残骸を手で消す場合は、その名前に一致する空のdirectoryだけを、判定を使うCLIを動かしていない間に、非再帰で削除します。以前の固定名 `OneStarMaker.StoreProbe\` は判定に使われなくなりました。残っていても判定に影響しません。

```powershell
Get-ChildItem -LiteralPath ([Environment]::GetFolderPath('LocalApplicationData')) -Directory -Force |
  Where-Object Name -Match '\AOneStarMaker\.StoreProbe\.[0-9a-f]{32}\z' |
  ForEach-Object { try { [IO.Directory]::Delete($_.FullName, $false) } catch { } }
```

既知の未確認: packageの初回（package専用の `LocalCache\Local` がまだ無い状態）で新しいdirectoryがどちら側に作られるかは観測していません。

どちらの拒否でもストア配下には何も作らず、既存fileも開きません。仮想化されたhostではStorage・Workflow・Harnessを操作せず、非仮想化の担当（ownerの端末、仮想化されないエージェント環境等）へ固定入力のfetchや出口のpublishを依頼します。仮想化されたhostから使えるようにする経路は後続です。定期清掃jobはpackage外のTask Schedulerで動き、この判定の対象外です。

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

## ローカル固定（evidence stage）

同じPCで動く担当同士の固定入力（差し戻し中の実装snapshot、Phase A snapshot、判定evidence bundle、C′ blind bundle）を、R2もgit pushも使わずに内容hashで固定して渡す既定の経路です。network・資格情報・deploymentの疎通を使いません。

```powershell
pwsh tools/artifacts.ps1 evidence stage --profile osm --config <v2-config> --selection <v2-selection>
pwsh tools/artifacts.ps1 fetch --profile osm --config <v2-config> --reference <trusted-reference> --sha256 <trusted-package64>
pwsh tools/artifacts.ps1 evidence inspect --profile osm --config <v2-config> --task sample-task
```

- **stage:** publishと同じselectionを受け、同じsnapshot・manifest/ZIP/hashの生成とローカル再検証までを行い、PUTしません。artifactはstate `staged`（`origin: stage`）でcatalogに入り、ZIPはStorage所有のstagingに唯一の複製として残ります。返す `reference`（`osm-evidence-v2:`）と `packageSha256` はpublishと同じ形式で、keyはartifactIdから決まる値を未送信のまま持ちます。出力の `locality` は `local` です。
- **preflight:** networkを使いません。同じtaskに未解決のPUT（`put-pending`）または削除intentが残っていれば、解決を試みずに `unresolved-put` / `unresolved-delete` で拒否します。解決は通常のpublish・fetch・cleanupで行ってから再実行します。
- **fetch:** catalogのstateで取得元を選びます。`staged` ならstaging内のZIPを新しいprivate operationへcopyし、R2から取得した場合と同じ検証（期待packageSha256、manifest、全entry）をしてoutputPathを返します。ZIPの欠落は `changed-copy`、hash・identityの不一致は `identity-mismatch`、別taskのreferenceは `catalog-missing` で拒否し、R2へfallbackしません。`ready` は従来どおりR2から取得します。
- **inspect:** `staged` にもreferenceとpackageSha256を返し、`locality` を `local`（`staged`）/ `promoted`（`ready`）で示します。
- **保持・保護・清掃:** `staged` は `ready` と同じtask終了 + 30日で満了し、`use` / `release` の対象です。満了した `staged` の削除はDELETE・GET・削除intentを使いません。state `deleted` の永続化が削除の確定点で、その後にstaging内のcopyを消します。確定前に止まれば `staged` のまま（fileは無傷）、確定後に止まれば次回のcleanupが残ったcopyだけを消します。copyの欠落を削除の進捗として読むのは、`deleted` になったstage由来のartifactだけです。`staged` のままのartifactのcopy欠落は従来どおり `changed-copy` で止まります。`staged` が参照するstagingは7日清掃の対象外（`operation-pending`）です。
- **昇格は後続:** 同じartifactを同じbytesでR2へ置く経路は後続スライスです。Cloud・別hostの読み手には、従来のpublishで新しいartifactを作るかPRブランチで渡します。publishは別のartifactなのでreferenceとhashが変わり、その配布には新しい取得案内を作ります（localの取得案内を流用しません）。`staged` のstagingをselectionのrootにしてpublishすると、既存のsource規則どおり再snapshotした別artifactになり、元の `staged` は変わりません。端末を失うと未昇格の `staged` も失われます（backupは後続）。

取得案内は、reference、期待packageSha256、対象base / head、用途、所在task、保持条件、引渡し先、`locality` を持ちます。referenceとpackageSha256の実値は取得案内にだけ置き、tracked HANDOFFとPR本文には転記しません（[レビュー証拠](../../.agents/skills/osm-workflow/references/review-evidence.md#保存先とリモートへの配布)）。

### レビュー用の変更集合（changes.patch）

差し戻し中の発見Cに未commitの作業ツリーを渡すときは、base commitのSHA、完全な変更集合 `changes.patch`、変更後の全fileをstageします。送信側は実indexに触れず、一時index（`GIT_INDEX_FILE`）で作ります。

```powershell
$env:GIT_INDEX_FILE = '<private-temp>\review.index'
git read-tree <base-sha>
git add -A
git diff --cached --binary --no-color --no-renames --output=<input-root>\changes.patch <base-sha>
git diff --cached --name-only --no-renames --diff-filter=d <base-sha>
Remove-Item Env:GIT_INDEX_FILE
```

patchには追跡済み・未追跡・削除・バイナリの変更が入ります（未追跡は追加、削除は削除、バイナリは `--binary` で復元できる形）。変更集合の範囲はcommitと同じで、`.gitignore` に該当する未追跡fileは `git add -A` が加えないため含みません。レビューに必要な生成物は、ignore対象のままにせずselectionの別fileとして明示します。selectionのrootには `base.txt`（base SHA）、`changes.patch`、`files/<path>` を置きます。`files/` に収録するのは、最後の `git diff --cached --name-only --no-renames --diff-filter=d` が列挙するpatchの追加・変更対象の全fileです（`git read-tree` と `git add -A` は何も列挙しません）。削除はpatchだけに現れます。selectionの `base` はbase SHA、`head` はその時点のHEAD commitです。受け手はbase SHAのcheckoutへ `git apply --binary changes.patch` を適用した結果を `files/` と照合でき、送信元の可変な作業ツリーを読みません。判定CとC′の入力にはこの形を使わず、commitで固定します。

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

Storage所有のupload/staging/未採用失敗copyはcreatedAtから604800秒の7日清掃です。転送中、採用pending、レビュー入力採用、有限use、削除不明、未昇格の `staged` が参照するstagingは除外します。採用する失敗ログは通常publishが成功する前に元stagingを消しません。Storage copyはtaskの同じ30日期限で管理し、利用者/Harnessの原sourceや利用者が作ったcopy、資格情報、他task/無関係領域を清掃対象へ登録しません。

採用・operation・削除intent・利用中保護のいずれも無く、どのartifactからも参照されないstagingは、catalogに残ったまま実体directoryだけが無くても通常操作を止めません。publish・fetch・useの結果とcleanupの `items` に、欠落の件数を `stagingMissing`（常に整数、0を含む）または `reasonCode: staging-missing` で出します。pathは表示せず、成功を黙って返すこともありません。欠落が許されるのは、解決後のpathがtransfers root直下の32桁hex名で、rootと親のACL・reparse検査を通る場合だけです。実在するstagingのpath・ACL・reparse異常、リンク先のないjunction/symlink、採用済み・operation付き・進行中のartifact（put-pending・`staged`・削除intent・operation付き）や未知のartifactIdから参照されるstagingの欠落、artifactのownedCopies欠落（`deleted` になったstage由来artifactの残務を除く）、putIntent不一致は従来どおり拒否します。欠落directoryはpublishのsourceにできず `invalid-source` です。7日（604800秒）が過ぎ、かつ同じtaskの登録済みpathの実在を1件以上確認できた場合に限り、cleanup（dry-runを除く）がcatalogのentryだけを外し `staging-missing-dropped` を `skipped` に数えます。fileもdirectoryも削除せず、確認できるpathが無いtaskのentryは残ります。dry-run・読取経路はcatalogのentryを変えません。

exit 0は検証・確定完了、1は失敗/未確認、2は配送待ち・scheduler/cleanupの部分保留です。inspectのactive/end-not-recordedやcleanupのdeleted/skipped/blocked/remaining/itemsを確認し、未確認を成功へ置き換えません。例外本文やSDK応答本文は表示せず、非秘密reasonCode/residueを使います。資格情報の既存exit意味は変えません。

publish・stage・fetch・inspect・use・releaseは、`--config` の読取・検証の失敗（相対path・不在・JSON不正・field不一致・hex不正）を `invalid-evidence-config`、configに対応するdeployment receiptの不在・読取不能・不一致を `unknown-deployment`、publish・stageの `--selection` の読取失敗（相対path・不在・JSON不正・field不一致）を `invalid-selection` で返します。いずれもWorkflow guard・資格情報・networkに進まず（publishでは `residue.remote` が `pre-put`）、pathは表示しません。configとselectionは絶対pathで渡します。cleanup・schedule-install・schedule-statusのconfig失敗は従来どおりです。

## 自動清掃

```powershell
pwsh tools/artifacts.ps1 evidence schedule install --profile osm --config <v2-config>
pwsh tools/artifacts.ps1 evidence schedule status --profile osm --config <v2-config>
```

同じownerのTask Scheduler `OSM-Evidence-Cleanup-<repositoryId先頭12>` は毎日03:00 localとlogon、StartWhenAvailable、IgnoreNew、InteractiveToken/Limitedで動きます。password/親鍵は登録しません。未ログオン中は動かず次ログオンで追いつくため、期限は削除資格であり即時削除SLAではありません。

installは最終implementation headのscript/module/DLL/deps/configをKnown Folder `OneStarMaker/Artifacts/runtime/<head>/`へprivate copyします。runtime manifest v2は五つの既存専用領域（Workflow、Evidence、deployment、transfer、runtime）の実体path・volume/file identityとownerを固定します。WindowsのAppData仮想化で論理pathと実体が異なる場合も、新しい保存先へ移さず同じファイルを参照します。actionのpwsh/entry/runtime/作業directoryは実体の絶対pathで、可変worktreeに依存しません。

**ローカル固定の導入手順:** 共有storeへ `staged` を書き始める前に、`schedule status` で登録済みruntimeのheadを確認し、`staged` を読める新しいimplementation headで `schedule install` を行います。旧runtimeの日次jobは `staged`（およびstage由来の `deleted`）を含むcatalogを `corrupt-state` で読めず、そのtaskの清掃が止まるため、切替前に `evidence stage` を使いません。runtimeの収集機構は変えません（tracked moduleは既存の `git ls-files` 収集で入ります）。

**登録済みtaskの切替（identityが変わるとき）:** `install` は同名の登録済みtaskを修復・上書き・有効化せず、identityが一致すれば `unchanged`、違えば `scheduler-identity-mismatch` で止まります。identityは現在のpwshの実体path、`runtime/<head>/` の実体path、`runtime.json` のhashから毎回計算するため、(1) 新しいimplementation headで `install` する、(2) Store版pwshの更新で実体path（`WindowsApps\Microsoft.PowerShell_<版>_...\pwsh.exe`）が変わる、(3) configを変える、のどれでも登録済みtaskと食い違います。(2) では日次jobが消えた旧pathのpwshを起動できず `0x80070002` で失敗し、`schedule status` も `scheduler-identity-mismatch` になります（2026-10-16に7.6.6→7.6.7の更新で実測）。切替はownerが行います: 同じWindowsユーザーのpwshで `Unregister-ScheduledTask -TaskPath '\OneStarMaker\' -TaskName 'OSM-Evidence-Cleanup-<repositoryId先頭12>'` を実行して旧taskを削除し、新しいheadのworktreeから `schedule install` を実行し、`schedule status` が `passed` を返すことを確認します。CLIにtaskを削除する機能はなく、旧headの `runtime/<head>/` は残ります（削除しません）。

`schedule install` / `schedule status` の失敗のうち次の固定codeは、stdoutに `{"status":"failed","reasonCode":"<code>"}`（exit 1）で返します。`scheduler-identity-mismatch`（登録済みtaskまたはdescriptorが期待identityと違う。上の切替手順）、`scheduler-disabled`（taskが無効化されている。CLIは有効化しません）、`scheduler-not-installed`（installの直後にOSで登録を確認できない。statusでは従来どおり `pending`、exit 2）、`scheduler-query-failed`（Task Schedulerの照会が不在以外の理由で失敗）、`scheduler-runtime-missing`（3 projectのRelease buildが無い）、`runtime-source-uncommitted`（worktreeに未commitの変更がある、またはHEADを解決できない）、`runtime-identity-mismatch`（`runtime/<head>/` のmanifest・owner・configが現在の入力と違う）、`runtime-hash-mismatch`（runtimeのfileのhashがmanifestと違う）、`runtime-config-mismatch`（descriptorが指すruntimeのconfig hashが渡したconfigと違う）。これ以外の例外は従来どおりstderrの `Artifact operation failed.` だけで、endpoint・path・SID・例外本文は出しません。configの読取失敗の扱いも従来どおりです。

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

## 分裂したストアの合流（store plan / consolidate）

仮想化されたprocessが書いた記録（package側のcopy）を、非仮想化のprocessから実ストアの同じ相対位置へbytes同一で合流します。一回の管理操作で、plan → ownerがmanifestを承認 → dry-run → 実行 → 単位receipt → 公開後の確認、の順に進めます。package側のcopyは読取り専用handleで開くだけで、変更・削除しません。

```powershell
pwsh tools/artifacts.ps1 store plan --source-root <package側のOneStarMaker> --output <新しいmanifest path>
pwsh tools/artifacts.ps1 store consolidate --manifest <承認したmanifest> --manifest-sha256 <trusted64> --dry-run
pwsh tools/artifacts.ps1 store consolidate --manifest <承認したmanifest> --manifest-sha256 <trusted64>
```

- **対象:** `Workflow`、`Harness`、`Artifacts\evidence-v2`、`Artifacts\deployments`、記録が参照する `Artifacts\transfers` のoperationだけ。`credentials`、`builds-v1`、`contents-v1`、`runtime`、`RevisionLocks`、toolsが所有しないdirectoryは名前を列挙するだけで開かず、manifestの `excluded` に理由付きで載せます。
- **単位:** task単位（同じtaskのWorkflow・Evidence・Harness directoryと、その記録が参照するoperation）とdeployment単位（receiptとconfigの対）。同じoperationを参照するtaskは一つの単位に結合します。単位内はtransfers → Evidence → Harness → Workflowの順に公開します。Workflowのtask一覧（清掃・jobの起点）にはWorkflowのtask directoryが置かれた時点でtaskが現れるので、公開途中の単位が一覧から見えないのは、**新たに公開するWorkflowが1つで、単位のWorkflowが実ストアにまだ無い**場合に限ります。それ以外の単位は次の「保留」のとおり公開しません。
- **参照の検査:** 記録のJSON文字列のうち完全修飾pathは、LocalCacheのpathを含まず、論理ストアの下では対象roleの中を指し、指す先のfile・directoryそのものが同じ単位にあるか実ストアに既にある必要があります（単位やdirectoryがあるだけでは足りません。実ストアにだけある参照先は、private ACLに加えてbytesを読めることも要し、別writerが排他で開いている等で読めなければ成立としません。Evidence taskはdeploymentのreceiptとconfigの両方を要します）。満たさなければ `reference-missing` です。ストア外のpath（worktreeのsource等）は参照として扱いません。読み込める上限（16MB）を超えるJSONらしいfileは `source-unchecked`、名前が `.json` なのにJSONでないfileは `source-invalid` として、検査せずに公開せず保留します。
- **保留:** 実ストアにbytesの違う同じentry（分岐したWorkflow履歴、違うHarness CURRENT等）、参照欠落、private ACL規則（owner・継承無しの3主体FullControl）に合わない既存entry、reparse pointがあれば、勝者を選ばずその単位を保留します。公開途中にtask一覧から見えてしまう単位も保留します: 新たに公開するWorkflowが2つ以上（`multi-workflow-unit`。directoryは一つずつrenameで現れるため）、単位のWorkflowが実ストアに既にあり（同一を含む）Workflow以外に公開するitemがある（`workflow-already-visible`）。全itemが同一の単位と、新たに公開するのがWorkflow 1つだけの単位は保留しません。理由は独立に判定し、一つの単位が複数の理由を持ちます。保留した単位はfileを一つも公開せず、stagingも単位receiptも作らず、journalに保留の記録だけを残します。依存する単位は `dependency-held` になりますが、deployment単位は従来どおり先に公開します。実行時の再検査では、承認時に保留だった単位は理由が消えても公開せず（再planと再承認が要ります）、承認時に公開対象だった単位の構成が変われば `source-changed`、保留の条件を満たせば `target-changed` で止めます。保留・実行時の変更検出・別writerとの競合（`concurrent-writer`）が残るrunは `partial`（exit 2）です。
- **公開:** 実ストアの `Artifacts\consolidation\<runId>\` がrunを所有します（runIdは承認manifestのsha256から決まり、同じmanifestの再実行は同じrunを再開します）。lock、manifestの写し、journal、staging、単位receiptを置き、stagingへCreateNew → flush → SHA-256照合 → ACL設定の順に複写してから、directory単位の非上書きrenameで公開します。複写は開いたsourceのhandleの実体pathがsource上の位置そのものであることを確かめ、棚卸し後にjunction等へ差し替わっていれば単位を `reparse` で止めます。manifestの写しと単位receiptもstagingに書いてから非上書きrenameで置くので、書込み途中の記録は残りません。既存のreceiptは、このrunのこの単位の記録として読めた場合だけ完了とみなし、読めなければ書き換えずに `receipt-invalid` で止めます。既存entryは上書き・削除せず、消すのはrun自身の未公開stagingだけです。途中で止まっても再実行で完了へ収束します。
- **運用の前提:** run中は対象taskIdを対話CLIで操作しません。先にguardがtask directoryを作った場合、その単位は `concurrent-writer` で止まります。公開後は単位ごとに `workflow-task.ps1 status`、`evidence inspect`、`harness.ps1 current` を非仮想化のprocessで実行し、各domainの読取り検証を通すことを確認します。

plan・consolidateの失敗理由は `virtualized-store`、`store-context-unknown`、`invalid-source-root`、`invalid-output`、`manifest-hash-mismatch`、`manifest-invalid`、`consolidation-busy`、`run-conflict`、`store-path-unavailable`、`consolidation-failed` です。出力に実path・package名・SIDは含めません。

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
dotnet build tools/Artifacts/Probe/R2RouteTransport.csproj -c Release -o tools/Artifacts/Probe/artifacts/probe
pwsh -NoProfile -File tools/Artifacts/Probe/RouteProof.ps1 `
  -Endpoint https://<32-hex-account-id>.r2.cloudflarestorage.com `
  -ImplementationBase <40-hex-base-commit> `
  -ImplementationHead <40-hex-implementation-head-commit> `
  -LockRuleJson '{"Prefix":"probe/locked/","Enabled":true,"Kind":"Age","RetentionSeconds":86400,"RuleCount":1,"DateRules":0,"IndefiniteRules":0,"WriterCanConfigure":false,"LifecycleCompatible":true,"BeforeHash":"<64-hex>"}'
```

## 保守とoffline検証

### 開発Build 1件の保存と取得

Build 1件の保存は `build publish`、成功記録の再照会は `build inspect`、取得は `build fetch` を使います。既存Player出力の明示file集合を対象にし、Unityを起動せず保存します。`selection` は `schemaVersion=1`、`purpose=build`、絶対`root`、相対`files`配列、`project`、`target`、`configuration`、publish前に保存した32桁hexの`buildId`だけを持ちます。`files`はroot内の全fileと一致させます。Season別Player出力も、選んだ単一root内のfileを列挙して保存できます。外部Content Directoryは自動収集しません。

```powershell
pwsh tools/artifacts.ps1 build publish --profile osm --config <build-config> --selection <build-selection>
pwsh tools/artifacts.ps1 build inspect --profile osm --config <build-config> --build-id <selection内のbuildId>
pwsh tools/artifacts.ps1 build fetch --profile osm --config <build-config> --reference <osm-build-v1:...> --sha256 <独立した期待packageSha256>
```

`build-config` はprivate operation内に置く非秘密の8 field JSONです。`schemaVersion=1`、`purpose=build`、`profile=osm`、既存登録済みEvidence configの絶対`evidenceConfigPath`とそのbytesの`evidenceConfigSha256`、canonical `repositoryId`、`prefix=development/builds/v1/`、`policy=build-publish-v1`を指定します。登録済みdeploymentの整合を読取専用で確認し、endpoint/bucketと既存ユーザーの鍵を使います。Build configはEvidenceのtask終了・30日期限を継承しません。

Buildは最大512 file・選択総量240 MiB、単一fileとZIPは256 MiB、相対path240文字、manifest/selection/receipt各1 MiBです。選択集合と全read lockを確認してprivate snapshotを作り、Build manifestを含むZIPをローカル展開検証してから、固定keyへ1回PUTします。別PowerShell processによる同keyの全bytes・hash読戻しが成功した後、`receipt.json`のatomic renameを唯一の成功確定点とします。成功結果の`reference`と独立した`packageSha256`を保存してください。参照文字列だけから期待package hashを補いません。

成功正本は同一WindowsユーザーのLocalApplicationData `OneStarMaker/Artifacts/builds-v1/<repositoryId>/<buildId>/receipt.json`です。応答だけが失われた場合は、selectionに事前保存した同じbuildIdで`inspect`を呼び、同じreferenceとhashを確認します。同じIDの再publishはnetwork前に拒否されます。PUT応答不明、読戻し不成立、receipt確定前の失敗は成功ではなく、intentとlocal/remote残置を保持します。publishは自動再PUT・rollback DELETEを行いません（R2上の古いBuildの削除は後述の `build cleanup` だけが行います）。取得物は新規private operationへ展開し、実行しません。この記録は同PC・同ユーザー内の永続性であり、別hostのcatalog復元とR2上の全件一覧は後続の作業です。件数清掃で削除済み・削除中のBuildには、`inspect` が成功ではなく `payload-deleted`／`delete-pending` を返します。

系列はrepository内の`project/target/configuration`で識別し、各値は小文字英数字で始まる1〜64文字の小文字英数字・ハイフンです。branchを保存枠へ追加せず、keyは`development/builds/v1/<repositoryId>/<project>/<target>/<configuration>/<buildId>/bundle.zip`に固定します。Build生成元のrevisionが不明な場合、現在のHEADから推定して記録しません。

configとselectionは同じ新規private operationへ置きます。入力rootと所有storeの親子関係を拒否し、全source handleの読取lock、file集合の前後照合、snapshotの全bytes/hash検証で保存対象を固定します。ZIPの最終検証から送信まで同じ読取lockを保ちます。成功receiptには`inUse`を記録せず、未取得の利用状態をfalseと見なした保持policyの呼出しも行いません。

### BS4 Player出力の退避と保存（build collect）

`build collect` は、BS4のPlayer Build（`Tools/OSM/Content/Build BS4 Spring Player`）が作った配布形Player `artifacts/bs4/players/<identity>/` 1件を、selectionやbuild-configを手書きせずに退避・保存します。Build完了後に人が `--source` を明示して1回実行します。最新のidentityを自動選択しません。Unityを起動せず、Unity側からも自動起動しません。

```powershell
pwsh tools/artifacts.ps1 build collect --profile osm --config <build-config> --source <artifacts/bs4/players/<identity>の絶対path> [--artifacts-root <ArtifactsRoot>]
```

`<ArtifactsRoot>` は退避したPlayerの置き場で、Storageの内部保存先（LocalApplicationData `OneStarMaker/Artifacts`）とは別です。worktreeに縛られない任意のローカルfolderを、`--artifacts-root` かユーザー環境変数 `OSM_ARTIFACTS_ROOT`（起動したprocessが引き継いだ値）で指定します。既定値はありません。どちらも無ければ `artifacts-root-not-configured` を返し、何もしません。指定値は既存の絶対path directoryで、reparse pointではなく、入力Playerや所有store・Workflow・transfersと親子関係にないことを要求します。このコマンドはArtifactsRootと環境変数を作成・変更しません。同期folderでない場所を推奨します。

処理は次の順です。

1. 入力が配布形か検査します。`build-receipt.json` が無い、IL2CPP backup directoryを含む、`artifacts/bs4/work/` 配下を指す入力は拒否します。#111の上限（512 file・240 MiB・単一256 MiB・相対path 240字）を退避先に置く集合で先に検査します。
2. receiptの `transportPath` が指す `transport.json` のbytesを、receiptの `transportManifestSha256` と照合します。`revision`・`target` が同じBuildを指し、Player内 `content/` の全file・size・SHA-256がmanifestの `files` と一致することを要求します。
3. `buildId` はidentity（`yyyyMMddTHHmmssfffZ-<32hex>`）の末尾32桁hexです。系列は固定の対応表で決めます。`StandaloneWindows64-Player` は `windows-x64-player`、`IL2CPP`+`High` は `il2cpp-high`、projectは `samplegame` です。表に無い組合せは拒否します。
4. `<ArtifactsRoot>/players/<project>/<target>/<configuration>/<buildId>/` へcopyで退避します。元は変更しません。呼出しごとに固有名のstagingを同じ親directoryに作り、全fileのsize/SHA-256を元と照合してからrenameで公開します。退避先からは `build-receipt.json`（ローカル絶対pathを含む）を除き、`build-info.json` を加えます。既存の退避先は、この入力から作る内容と全hashで一致すれば完了済みとして再利用し、一致しなければ拒否します。同じbuildIdの並行実行は片方だけが公開でき、負けた側は `stage-conflict` で止まります。他の呼出しのstagingや他の退避先には触れません。
5. 新規private operationへbuild-configのbytesを複写し、退避先の全file一覧からselectionを生成して既存の `build publish` を呼びます。

`build-info.json` は非秘密の固定11 field（`schemaVersion=1`、`project`、`target`、`configuration`、`buildId`、`buildIdentity`、`contentSet`、`contentTarget`、`transportManifestSha256`、`unityVersion`、`playerBuildGuid`）だけを持ちます。`contentTarget` はContent/Unityのtarget名で、系列の `target` とは別の値です。ローカルpath・ユーザー名・時刻は含めません。

同じ入力での再実行は次のとおりです。退避済みなら退避を省きます。成功receiptがあれば同じ `reference`/`packageSha256` を返し、PUTしません（返る `publish` は `existing`）。件数清掃でR2から削除済み・削除中のBuildなら、既存の成功を返さず `payload-deleted`／`delete-pending` で止まり、再PUTもしません。予約だけが残りreceiptが無い場合は、PUT応答不明の可能性があるため `publish-unresolved` で止まり、再送しません。その場合は `build inspect` で状態を確認してください。未予約ならpublishへ進みます。退避先は件数清掃の対象外で、このコマンドは削除しません。

成功結果の `reference` と独立した `packageSha256` を保存し、取得は `build fetch` を使います。Content Directoryの独立した保存、Player/Contentの分離、ArtifactsRootの清掃、Windows以外のtargetは後続の作業です（R2上のBuildの件数清掃は `build cleanup`）。

非秘密の偽Player fixtureと注入transportによる全件offline検証は次のコマンドです。並行実行は別processをsignalで同期し、時間待機を使いません。

```powershell
pwsh tools/Artifacts/tests/BuildCollect.Tests.ps1 -Case '*' -ResultPath <result-json-path>
```

### 開発Content 1件の保存と取得（content collect / inspect / fetch）

Content出力（`transport.json` + `content/`、BS4の `artifacts/bs4/transport/<revision>/` など）1件を、ZIPにせずfile単位のobjectとして保存します。単位はrevisionごとで、保存後は丸ごと不変です（上書き・差分upload・content-addressed・GCはしません）。Playerとは独立した保存種別で、Build/Evidenceの契約や保存形式は変えません。Unityを起動せず、Unity側からも自動起動しません。

```powershell
pwsh tools/artifacts.ps1 content collect --profile osm --config <content-config> --source <Content出力の絶対path> [--artifacts-root <ArtifactsRoot>]
pwsh tools/artifacts.ps1 content inspect --profile osm --config <content-config> --content-set <contentSet> --target <windows-x64-player> --revision <revision>
pwsh tools/artifacts.ps1 content fetch --profile osm --config <content-config> --reference <osm-content-v1:...> --sha256 <独立した期待manifestSha256>
```

`content-config` は `build-config` と同じ形の非秘密8 field JSONです。`schemaVersion=1`、`purpose=content`、`profile=osm`、既存登録済みEvidence configの絶対`evidenceConfigPath`とそのbytesの`evidenceConfigSha256`、canonical `repositoryId`、`prefix=development/content/v1/`、`policy=content-publish-v1` を指定します。`build-config` は流用できません。`collect` と `fetch` はconfigのbytesを新規private operationへ複写して使い、`inspect` は渡されたconfigをprivate operation内で読みます。

`collect` の処理は次の順です。入力は `transport.json` と `content/` を直下に持つdirectory 1件で、最新の出力は自動選択しません。ArtifactsRootは `build collect` と同じ規則（`--artifacts-root` かユーザー環境変数 `OSM_ARTIFACTS_ROOT`、既定値なし）です。

1. `transport.json`（上限4 MiB）を読み、`contentSet`・`revision`・`target`・`files` を検査します。directory名はrevisionと一致させます。`target` は `StandaloneWindows64-Player` → `windows-x64-player` の1行の表だけで、表に無い値は `unsupported-series` です。`contentSet` は `[a-z0-9][a-z0-9-]{0,63}`、revisionは `yyyyMMddTHHmmssfffZ-<32hex>` に限ります。
2. manifestの `files` の各 `path` は `PackagePolicy` の規則（予約名・末尾のdot/space・保護名・`..`・空segmentの拒否、240字以下）に加え、文字を `[A-Za-z0-9._-]` と `/` に限り、`..` を含む名前は拒否します（transportのkey検査と揃えるため）。大文字小文字だけ違う重複、fileとdirectoryの衝突（`a` と `a/b`）、`size` が0のfileは拒否します。実fileの集合・size・SHA-256がmanifestと完全に一致する場合だけ進みます。
3. `<ArtifactsRoot>/content/<contentSet>/<target>/<revision>/` へcopyで退避します（`Invoke-BuildStage` の規則。元は変更しません）。既存の退避先は全hashが一致すれば再利用し、違えば `stage-conflict` で止まります。退避先は削除しません。
4. 退避先を新規private operationへsnapshotし、全fileをmanifestと照合してから使います。各PUTの直前にも送るfileのsizeとSHA-256を再照合し、ArtifactsRootのfileを直接送りません。
5. 公開の最初に、遠隔の `transport.json` を別PowerShell processのGETで照合します。存在してhashが違えば `revision-conflict` で何も送らず止まります。その後 `content/*` をmanifestの順に、各objectについて「子processのGETで照合 → 404なら再照合してPUT → 子processの読戻しで照合」を直列に行い、最後に `transport.json` を置きます。一致したobjectは再送せず、不一致（`object-conflict`）・403・timeout・応答不明（`object-unconfirmed`）はそのobjectを未確認として止まり、DELETEも上書きもしません。PUT応答が不明になったkeyは同じrunで再送しません。
6. 全objectの照合が通った後、ローカルの `receipt.json`（`contents-v1/<repositoryId>/<contentSet>/<target>/<revision>/`）のatomic renameだけを成功確定点とし、`reference`（`osm-content-v1:`、opaque）と、それとは別の `manifestSha256`（`transport.json` のbytesのSHA-256）を返します。失敗・応答不明・途中停止・予算超過では成功を返しません。

再実行は次の優先順位です。(1)退避先に違う内容があれば `stage-conflict`（退避先が期待と完全一致なら、同時公開でrenameに負けた側も既存として進みます）、(2)`receipt` があり同一入力なら送信せず同じ `reference` を返し（`publish=existing`）、(3)`intent.json`（最初の送信前に固定する決定的な送信計画）が今回と違えば `intent-conflict`（`receipt` があっても成功にしません）、(4)遠隔の `transport.json` のhashが違えば `revision-conflict`、(5)それ以外は上の経路で続行します。「同一入力」はintent.jsonのbytes一致で判定します（実装は(2)と(3)を同じ照合で先に評価します）。途中で止まったrevisionを同じ入力で再実行すると、照合が一致したobjectは再送せず、404のものだけPUTして完了します。ArtifactsRootを変えて呼んでも同じ判定です。件数清掃（`content cleanup`）でR2から削除済み・削除中のrevisionは、intentの照合の後・receiptの分岐の前に `payload-deleted`／`delete-pending` で止まり、既存の成功も再PUTも返しません。同じrevisionの同時公開は、`publish.lock`（`FileShare.None` で開いたhandle。保持区間はintentの照合からreceiptの再読まで）を取れた片方だけが進み、負けた側は `publish-in-progress` で止まります。永続の予約は持ちません。

`inspect` はローカルのreceiptだけを見て同じ `reference` と `manifestSha256` を返します（応答だけを失った場合の回復）。`receipt` が無い状態では成功を返しません。`inspect` はrevisionの `publish.lock` を取ってから読み、件数清掃の試行中などで取れなければ待たずに `publish-in-progress` を返します。receiptより先に削除の記録を見て、削除済みなら `payload-deleted`、削除中なら `delete-pending`、削除の記録が読めなければ `receipt-unavailable` を返し、referenceを返しません。`inspect` は既存の `publish.lock` だけを開き、無ければ作らずに `receipt-unavailable` を返します。

`fetch` は `reference` と、**独立に渡された**期待 `manifestSha256` の両方を要求します。referenceが含むhashは照合にだけ使い、期待hashの代わりにしません。新規private operationの `payload/` へ `transport.json` と全fileを取得し、取得した `transport.json` のhashが期待と違えばほかのfileを取らず、各fileのsizeとSHA-256、集合の完全一致（余剰・欠落の拒否）、path規則を検査します。取得物は実行せず、ArtifactsRootへは書きません。

上限は暫定の暴走防止ガードで、Content固有の容量上限の決定ではありません。object 1,024件（`transport.json` を含む）、宣言sizeの合計512 MiB、単一file 256 MiB、`transport.json` 4 MiB、R2 keyは512字以下です。宣言sizeは取得前に検査し、転送中の上限はtransportの256 MiBだけです。時間予算は公開が `300 + 8×N` 秒、取得が `300 + 2×N` 秒（Nはobject数。Spring 221 objectで公開2,068秒、取得742秒）で、予算はobjectごとのPUT・GET・読戻しのdeadlineにも効き、超えたら成功にしません。読戻しは1 objectごとに別processを起動するため、時間の大半は子processの起動です。

known limits: R2側には上書きを防ぐ仕組みが無く、「上書きしない」はクライアント側の照合だけで守ります。照合とPUTの間に別PCのwriterが書く競合は防ぎません（同じrevisionを別PCから同時に公開する運用は想定しません）。`inspect` はローカルの記録しか見ないので、件数清掃以外の理由で遠隔のobjectが後で失われても成功を返します。0 byteのfile、Windows以外のtarget、確定記録と「最新」の解決、R2からArtifactsRootへの配置と不足fileの再利用、R2上の孤児objectの回収、公開・取得で残るprivate operationの期限清掃は後続の作業です。`development/content/v1/` 配下のDELETEは、件数清掃が付ける印（`ContentRetentionDelete=$true`）付きのrequestだけをtransportの入口で通します。`collect`／`inspect`／`fetch` はR2 objectを削除しません（古いrevisionの削除は後述の `content cleanup` だけが行います）。

非秘密の合成fixture（実Contentの形を模した平坦な32hex名の `.cf`/`.resS`）と注入したtransport・readback・時計によるofflineの検証は次のコマンドです。並行実行は別processをsignalで同期し、時間待機を使いません。

```powershell
pwsh tools/Artifacts/tests/Content.Contract.Tests.ps1 -Case '*' -ResultPath <result-json-path>
pwsh tools/Artifacts/tests/Content.Publish.Tests.ps1 -Case '*' -ResultPath <result-json-path>
pwsh tools/Artifacts/tests/Content.Fetch.Tests.ps1 -Case '*' -ResultPath <result-json-path>
```

### 開発Buildの件数保持policy

`BuildRetentionPolicy.psm1` は内部の `Get-BuildRetentionDecision -SeriesId <string> -Builds <object[]> -KeepCount <int|long>` と、系列の退役用の `Get-SeriesRetirementDecision -SeriesId <string> -Builds <object[]>`（末尾で説明）を公開します。
呼出側が1系列の確定snapshotを渡し、policyは成功publishだけをNに数え、利用中の成功BuildをN内で優先保持します。
N=2、成功Buildが新しい順にA/B/CでCだけ利用中なら、A/Cを保持してBを削除候補にします。保護数がNを超えた場合も保護を維持し、`excessCount` に保護由来の超過件数を返します。
返値の `retainedBuildIds`、`deleteCandidateBuildIds`、`heldBuilds` は分類結果です。候補は実DELETEの許可ではありません。
削除を実装する側は、削除直前の新しいsnapshotと保護状態の確認・排他・再試行を所有します。系列の所属・一覧の完全性・成功確定・利用中状態の根拠は呼出側の責務です。
入力は正のInt32/Int64のN、空白でない系列ID、`object[]`の一覧です。各行は値だけを持つPSCustomObjectで、重複のない`buildId`と`succeeded`/`failed`/`incomplete`の`publishState`を要求します。成功行だけはUTC（offset 0）のDateTimeOffsetの`publishedAt`とboolの`inUse`も必要です。文字列からの型変換や未知状態の補完はしません。
不正な入力は `status=blocked` と理由を返し、3配列を空、`excessCount` をnullにします。
理由の優先順は`invalid-keep-count`→`invalid-series-id`→`invalid-build-list`→`invalid-build-snapshot`です。1行でも必要な状態が不正なら一覧全体を保留し、部分的な削除候補を出しません。
失敗・未完了publishは `heldBuilds` に入り、後始末はこのpolicyの対象外です。
保持は成功時刻の降順・同点はIDのOrdinal昇順、候補はその逆順、保留はIDのOrdinal昇順です。入力を変更せず、出力の書換えも入力へ伝播しません。
`Get-SeriesRetirementDecision` はNを使わず、保護中でない成功を全て候補（古い順）にし、保護中の成功を `reasonCode=protected`、失敗・未完了をこれまでどおりの理由で `heldBuilds` に入れます。行の検査・全順序・blockedの返し方は同じで、理由は `candidates-found`（候補あり）／`retired-held`（保留だけ）／`retired`（どちらも無い）です。`Get-BuildRetentionDecision` にN=0を渡して退役を表すことはしません（`invalid-keep-count` の入口検査は件数清掃の安全装置のまま）。
このpolicyはfile、R2、時計、store、Unityに触れません。実際の削除は次節の `build cleanup` と、末尾の「系列の退役」がこのpolicyを呼んで行います。
非秘密のメモリfixtureによる全件offline検証は次のコマンドです。

```powershell
pwsh tools/Artifacts/tests/BuildRetention.Tests.ps1 -Case '*' -ResultPath <result-json-path>
```

### 開発Buildの件数清掃（build cleanup / use / release）

`build cleanup` は、Build系列1つについて、このPCの成功記録と利用中保護を集め、上のpolicyでN件を超えた古いBuildのR2 objectだけを削除します。人またはAgentが明示実行するCLIで、自動起動はありません。

```powershell
pwsh tools/artifacts.ps1 build cleanup --profile osm --config <build-config> --project <project> --target <target> --configuration <configuration> [--dry-run]
pwsh tools/artifacts.ps1 build use --profile osm --config <build-config> --build-id <buildId> --consumer <id> --until <UTC> --reason <text>
pwsh tools/artifacts.ps1 build release --profile osm --config <build-config> --build-id <buildId> --protection <protectionId>
```

- **Nは10**です（コード内の定数で、CLIやbuild-configからは変えられません）。消したくないBuildは `build use`（最長90日）で守るか、Nを増やします（現状はコード変更）。保護中のBuildはNに数え、保護だけでNを超える間は保護を外さず `excessCount` を出して正常終了します。
- **系列はbranchや人で分かれません。** `project/target/configuration` が同じBuildは同じN件の枠を共有します。
- **数えるのはこのPCの成功記録だけです。** `builds-v1/<repositoryId>/` のreceiptを、今のbuild-configのhashに依存しない検査で読みます（configのbytesが変わっても過去の成功を数えます）。失敗・未完了のpublishは数えません。receiptの無いBuildは未完了として扱い、その `intent.json` は所属の判定にだけ使います（検査するのは所属5 field `buildId`・`seriesId`・`project`・`target`・`configuration` だけです。未完了は成功に数えず候補にもならないので、他のfieldの破損は消すBuildの選び方に影響しません）。指定系列の記録、または系列を判定できない記録が1件でも読めない、未知のentry・reparse・ACL不一致がある、保護・削除記録が読めない、`publishedAt` が未来（時計のずれ）のいずれかなら系列全体を `blocked` にし、何も消しません。他系列と確定できる記録の破損では止めず、`foreignInvalid` に件数を出します。
- **保護**は `<buildId>/protections/` の追記だけの記録です。`use` は系列lockの下で成功記録と削除状態を読み直してから書き、削除中・削除済み・receiptの無いBuildには付けません（`build-not-retainable`）。`release` は解除記録を足します。保護の判定は有限の `until` と明示のreleaseだけで、記録が読めなければ `protection-unknown` で止めます。
- **削除の手順**は、系列lockの下でsnapshotと保護を取り直してから、候補ごとに古い順で、削除意図の記録 → DELETE（1試行に1回）→ 送信観測の記録 → 確認GET → GETが404 `NoSuchKey` なら削除済み記録、の順です。DELETEの2xxだけでは削除済みにしません。記録は `<buildId>/deletion/<n>.(intent|sent|deleted|abandoned).json` の不変記録で、receipt等のローカル記録は消しません。DELETEが検証済み2xx以外（403・timeout・応答不明等）、確認GETが判定できない、記録の書込みに失敗、時間予算（600秒）超過のときは、その試行を未解決のまま残し、系列の残りも処理せず `partial`（exit 2）で終わります。次回の実行は未解決の試行を確認GETから先に確定させ、objectが残っていれば試行を取り下げて計算し直し、今も候補のときだけ新しい試行で削除します。1回の実行のDELETEは最大20回で、残りは `remaining` に出します。
- **このPCでの取得中**（`build fetch` が `<buildId>/access.lock` を共有で保持している間）のBuildは `in-transfer` で飛ばします。逆に削除試行中のBuildへの `fetch`／`inspect`／`collect` は待たずに `delete-pending` で失敗します。
- 結果JSONは `status`・`reasonCode`・`mode`（件数保持は `retention`、退役済みの系列では `retirement`。退役の詳細は[系列の退役](#系列の退役build-retire--content-retire)）・`seriesId`・`keepCount`・`excessCount`、`retained`／`candidates`／`held`／`deleting`／`deleted` の件数とID、`remaining`、`foreignInvalid`、Buildごとの `outcomes`、送信回数 `requests`（`delete`・`get`）だけで、endpoint・ローカルpath・例外本文を含みません。exitは0=完了（`within-limit`・`protected-over-limit` を含む）、1=blocked／失敗、2=partialです。`--dry-run` はlockを取らず、記録にもnetworkにも触れずに同じ分類を返します。
- 削除は書込み鍵のあるPCだけが行います。Cloud等の読み取り専用tokenでは行いません。削除したBuildは `build inspect` が `payload-deleted` を返すことで分かります。R2のDELETEが403を返す場合もbucketのruleは変更しません。

known limits: 別PC・Cloudでのpublishと利用（取得・保護）は観測しません。別PCの成功は数えないため、R2全体では系列がNを超えて残りえます（消さない方向の誤差です）。receiptを失ったobjectは候補にならず、回収は後続の作業です。「最新」は記録した `publishedAt` の順で、時計が巻き戻っていた間に確定したpublishの順序は保証しません。同じBuildを別PCから同時に削除する運用は想定しません。bucketのversioningが有効な場合、404の確認は現行版が無いことだけを示します。ArtifactsRootの `players/`・`content/` の件数清掃と `.staging` 回収、自動起動は後続の作業で、このコマンドはR2のBuild object以外を削除しません（Contentの件数清掃は次節の `content cleanup`、系列を丸ごと消すのは「系列の退役」の `build retire`。退役済みの系列ではこのコマンドも退役の選別で動きます）。

非秘密の合成receiptと注入した時計・transport・予算による全件offline検証は次のコマンドです。並行は別processをsignalで同期し、時間待機を使いません。

```powershell
pwsh tools/Artifacts/tests/BuildCleanup.Tests.ps1 -Case '*' -ResultPath <result-json-path>
```

### 開発Contentの件数清掃（content cleanup）

`content cleanup` は、Content系列（`contentSet/target`）1つについて、このPCの成功記録と保持中のPlayerからの保護を集め、`build cleanup` と同じpolicyでN件を超えた古いrevisionの全objectをR2から削除します。人またはAgentが明示実行するCLIで、自動起動はありません。

```powershell
pwsh tools/artifacts.ps1 content cleanup --profile osm --config <content-config> --content-set <contentSet> --target <windows-x64-player> [--artifacts-root <ArtifactsRoot>] [--dry-run]
```

- **Nは10**です（コード内の定数で、CLIやcontent-configからは変えられません）。保護中のrevisionはNに数え、保護だけでNを超える間は保護を外さず `excessCount` を出して正常終了します。件数保持が効くのはR2のobjectだけで、ローカルの記録は削除済みとして残ります。
- **数えるのはこのPCの成功記録だけです。** `contents-v1/<repositoryId>/<contentSet>/<target>/` のreceiptを読みます（content-configのhashには依存しません）。receiptの無いrevisionは未完了でNに数えません。未知のentry・reparse・ACL不一致・読めないreceipt、`publish.lock` の無い成功revision（cleanupは `publish.lock` を作りません）があれば `catalog-incomplete`、削除の記録が読めなければ `delete-record-invalid`、`publishedAt` が未来なら `clock-skew` で系列全体を `blocked` にし、何も消しません。「最新」は `publishedAt` の降順で、同時刻はrevisionのOrdinal昇順を新しい側とし、削除は最古から行います。
- **保護するのは保持中のPlayerが受け入れうるrevisionだけです。** `build collect` で退避したPlayerが受け入れるrevisionは、末尾32 hexがそのPlayerのbuildIdのものです。保持中のPlayerは、このPCの `builds-v1/<repositoryId>/` のBuild（directoryと `.reserved`。削除済みと読めたBuildを除き、状態を読めないBuildは保護に残します）と、ArtifactsRootの `players/<project>/<target>/<configuration>/` の退避済みPlayerとstagingです。名前だけで照合し、`build-info.json` の中身は読みません。失敗・未完了のBuildも保護に数えます（消さない側の過剰保護です）。保護源の列挙に失敗すれば `protection-unknown` で止めます。明示の利用中保護（use／release）はありません。
- **ArtifactsRootは必須です。** `--artifacts-root` かユーザー環境変数 `OSM_ARTIFACTS_ROOT`（`content collect` と同じ規則、既定値なし）で指定します。どちらも無ければ保護源を集められないので `artifacts-root-not-configured` で何も読みません。このコマンドはArtifactsRootを読むだけで、書き換えも削除もしません。
- **削除の手順**は、系列lock（`<target>/cleanup.lock`）の下でsnapshotと保護を取り直し、候補ごとに古い順で、revisionの `publish.lock` を取った後にもう一度取り直してなお候補の場合だけ始めます。1試行は、削除意図の記録 → `transport.json` のDELETE → 送信観測の記録 → `transport.json` の確認GET → GETが404 `NoSuchKey` なら確定点の記録（`committed`）→ 残りのobjectを順に「GET → 残っていればDELETE → GET」→ 全keyのGET 404で削除済み記録、の順です。DELETEの2xxだけでは削除済みにしません。`transport.json` を最初に消すのは、取得が `transport.json` から始まるためで、確定点より前に `transport.json` が残っていれば試行を取り下げてrevisionを無傷で戻し、確定点の後は候補・保護にかかわらず残りを消し切ります。記録は `<revision>/deletion/<n>.(intent|sent|committed|abandoned|deleted).json` の不変記録で、receipt・intent等のローカル記録は消しません。
- 応答が検証済み2xx・404でない、応答不明、記録の書込み失敗、時間予算（600秒）超過、DELETEの上限（1回の実行で2,048回）到達のときは、その試行を未解決のまま残し、系列の残りも処理せず `partial`（exit 2）で終わります。次回の実行は未解決の試行を先に、確定点があれば残りobjectのGETから、無ければ確認GETから続けます（GETが先なので、既に404のobjectへDELETEを送りません）。
- **このPCで `content collect`／`inspect` が `publish.lock` を保持している間**のrevisionは `revision-busy` で飛ばします。逆に削除試行中のrevisionへの `inspect` は待たずに `publish-in-progress` を返します。削除済み・削除中のrevisionには、`inspect` と同じsourceでの `collect` が `payload-deleted`／`delete-pending` を返し、成功も再PUTも返しません。
- 結果JSONは `status`・`reasonCode`・`mode`（件数保持は `retention`、退役済みの系列では `retirement`。退役の詳細は[系列の退役](#系列の退役build-retire--content-retire)）・`seriesId`・`keepCount`・`excessCount`、`retained`／`candidates`／`held`／`protected`／`deleting`／`deleted` の件数とrevision、`remaining`、revisionごとの `outcomes`、送信回数 `requests`（`delete`・`get`）だけで、endpoint・ローカルpath・例外本文を含みません。exitは0=完了（`within-limit`・`protected-over-limit` を含む）、1=blocked／失敗、2=partialです。`--dry-run` はlockを取らず、記録にもnetworkにも触れずに同じ分類を返します。
- transportは `development/content/v1/` のDELETEを、このコマンドが付ける印 `ContentRetentionDelete=$true`（bool）付きのrequestだけ通し、この印でBuild・Evidenceのkeyを消すことはできません。削除は書込み鍵のあるPCだけが行います。Cloud等の読み取り専用tokenでは行いません。R2のDELETEが403を返す場合もbucketのruleは変更しません。

known limits: 別PC・Cloudのpublish・Build・Playerは観測しません（別PCの成功は数えず、別PCで保持中のPlayerも保護しません）。`content fetch` 中のrevisionは保護しません（`fetch` はローカルの記録に触れず、削除と重なった取得は `transport.json` の404かhash不一致で失敗するだけで、壊れた取得物を成功として返しません）。同じidentityの `build collect` と `content cleanup` を同時に動かす競合は排除しません（試行の開始直前に見えた保護源だけを守ります）。receiptを失ったobjectは候補にならず、R2上の孤児objectの回収、ArtifactsRootの `content/` の件数清掃、自動起動、Nの系列ごとの設定は後続の作業です。このコマンドはR2のContent object以外を削除しません（系列を丸ごと消すのは次節の `content retire`。退役済みの系列ではこのコマンドも退役の選別で動きます）。

非秘密の合成receiptと注入した時計・transport・予算・上限による全件offline検証は次のコマンドです。並行は別processをsignalで同期し、時間待機を使いません。

```powershell
pwsh tools/Artifacts/tests/ContentCleanup.Tests.ps1 -Case '*' -ResultPath <result-json-path>
```

### 系列の退役（build retire / content retire）

`build retire`／`content retire` は、ownerが「この系列は終わり」と明示したBuild系列またはContent系列1つについて、退役を不変の記録として残し、このPCの成功記録にあるR2 objectを、保護中のものを除いて全て消します。件数清掃は最新N件を残すので、使い終わった系列（期間限定イベントの `contentSet`、検証用の系列）を消し切るにはこちらを使います。

```powershell
pwsh tools/artifacts.ps1 build retire --profile osm --config <build-config> --project <project> --target <target> --configuration <configuration> --decision <指示ID> [--dry-run]
pwsh tools/artifacts.ps1 content retire --profile osm --config <content-config> --content-set <contentSet> --target <windows-x64-player> --decision <指示ID> [--artifacts-root <ArtifactsRoot>] [--dry-run]
```

- **起点はownerの明示指示だけです。** イベント終了や最終publishからの経過で自動的に退役することはありません。`--decision` にはownerの指示を指す非秘密のID（Workflowの `Decision` と同じ書式。例 `chat:20261012T1230Z:series-retirement:retired`、`github:<owner>/<repo>/pull/<n>/<fragment>`。英数字で始まる128字以内の `[a-zA-Z0-9._:/-]`）を渡し、退役記録に残します。指示IDと系列tokenは何も読む前に検査します（`invalid-decision`／`invalid-build-identity`／`invalid-content-identity`／`unsupported-series`）。ArtifactsRootは `content cleanup` と同じ規則です。
- **事前承認の範囲外です。** 退役は系列単位の不可逆な判断なので、Agentはownerが系列と指示IDを明示した指示の範囲でだけ実行します。退役済みの系列への `build cleanup`／`content cleanup` と、同じ `retire` の再実行（続きの削除）は通常の清掃です。実行前に `--dry-run` で消える対象を確かめます。
- **取消はありません。** 退役記録は `retirements-v1/<repositoryId>/<build|content>/<系列tokenを~で結んだ名前>.json`（LocalApplicationDataのArtifacts配下、専用ACL）の不変記録で、書き換えも削除もしません。同じ系列への2回目以降の指示は、最初の記録の `retiredAt`・`decision` を保ちます。退役した系列へは以後保存できません（Contentは新しい `contentSet` 名で続けます。Buildの実系列を退役させると、その `project/target/configuration` へは保存できなくなります）。
- **退役は保護を外しません。** Buildは `build use` の有効な保護、Contentは保持中のPlayerが受け入れうるrevisionを残し、receiptの無い未完了のpublishも消しません。残りがあれば件数とIDを `held` に出して `retired-held`（exit 0）で終わり、保護が切れた後の再実行で残りを消して `retired` になります。このPCで取得中のBuild・`publish.lock` 保持中のrevisionは飛ばして `partial` にし、次の実行で消します。Buildの系列を判定できない未完了（予約だけのもの等）は消せないので、退役の完了判定には数えず、`held` にも混ぜず、結果の `unattributed.count` に件数だけ出します（`held` の件数と `retired` の表示が食い違いません）。
- **手順は件数清掃と同じです。** 系列lockの下で退役記録を読み、無ければ書いてから（記録が確定するまで何も送りません）catalogと保護を取り直し、保護中でない成功Build・revisionを全て候補にして古い順に、件数清掃と同じ削除試行（削除意図 → DELETE → 送信観測 → 確認GET → 404 `NoSuchKey` で削除済み記録。Contentは `transport.json` の確定点つき）で消します。Nは使いません。DELETEの上限（Build 20・Content 2,048）、時間予算（600秒）、403・応答不明で止まれば `partial`（exit 2）で、同じコマンドの再実行が続きから確定させます。完了後の再実行は何も送りません。
- **退役済みの系列では**、`build cleanup`／`content cleanup` も件数保持ではなく退役の選別で動きます。`build publish`（`build collect` 経由を含む）と `content collect` は送信・退避・storeへの書込みの前に、`build use` は保護記録を書く前に `series-retired` で止まり、同じ入力の既存成功（`existing`）も返しません。`inspect`／`fetch` の応答は変わらず、削除済みは `payload-deleted`、まだ消えていない保護中のBuild・revisionは取得できます。
- **退役記録を確認できないとき**（形・値・ACL・reparse・読取の失敗、別系列の内容）は `retirement-unknown` です。retire・cleanupは何も送らずに `blocked` で止まり（件数保持の選別へ戻りません）、publish・collect・useも止まります。`retirements-v1` が無いことは「どの系列も退役していない」を表します。
- 結果JSONは件数清掃と同じ固定fieldです（`mode` は件数保持が `retention`、退役と退役済みの系列が `retirement`）。退役の結果にはさらに `retirement`（`retiredAt`・`decision`）と、Buildだけ `unattributed`（`count`）を足し、`keepCount` はnullです。`status`／`reasonCode` は、全件削除済み `passed`／`retired`、保護中・未完了が残る `passed`／`retired-held`（exit 0）、未解決・残りあり `partial`（exit 2）、`blocked`／失敗（exit 1）です。`--dry-run` はlockを取らず、退役記録を書かず、記録にもnetworkにも触れずに、退役で行う選別を返します（退役記録がまだ無ければ `retirement` はnull）。
- **消すのはR2のobjectだけです。** receipt・intent・`deletion/`・保護記録は削除済みの記録として残し、ArtifactsRootの `players/`・`content/`、統合Contentフォルダ、DIST cacheには触れません（それぞれを所有する層の作業です）。

known limits: publishは系列lockを取らないので、退役記録の確定より前に始まった `build publish`／`collect`・`content collect` は止めません。成功すれば退役済み系列の成功記録になり、次の `retire`／`cleanup` で消えます（確定後に始まったものは止まります）。receiptの無いobject、別PCのpublish、receiptを失ったobjectは回収しません（R2の一覧は使いません）。退役記録はこのPCだけのもので、別PC・Cloudとは共有しません。退役の取消、ローカル記録の圧縮、退役した系列のArtifactsRoot・統合フォルダ・DIST cacheの片付けは後続の作業です。

非秘密の合成receiptと注入した時計・transport・予算・上限による全件offline検証は次のコマンドです。並行は別processをsignalで同期し、時間待機を使いません。

```powershell
pwsh tools/Artifacts/tests/SeriesRetirement.Tests.ps1 -Case '*' -ResultPath <result-json-path>
```

### Evidenceの責務とoffline検証

資格情報はCLI→CredentialCommands→CredentialStore→PathAcl/Recordの一方向です。EvidenceApplicationはpublish/stage/fetch/use orchestration（publishとstageは送信前までの準備を共用）、EvidenceContractはv2 codec/deployment、EvidencePathsはtask transaction/receipt、EvidenceRetentionPolicyは純粋判定、EvidenceCleanupは配送/削除/reconcile、EvidenceScheduleは固定runtime/OS adapter、EvidenceResetは一回の限定清掃を所有します。Workflow core/storeはArtifactsをimportせず、CLI composition rootが共通guardとpreflight/syncを結びます。合流の規則（単位・参照・保留の分類とmanifestの再照合）はStoreConsolidationPlanの純粋関数、棚卸し・staging・公開・receiptはStoreConsolidationが所有し、合流moduleはWorkflow/Evidenceのdomain moduleをimportしません。

仮想化の判定と入口は `tools/Workflow/tests/StoreContext.Tests.ps1`、合流の規則はファイルI/Oの無いメモリfixtureの `StoreConsolidationPlan.Tests.ps1`、合流のI/Oは隔離した二つのtemp storeの `StoreConsolidation.Tests.ps1` で検証します。後者は実moduleでpublishした記録を仮想化側へ移してから合流し、既存のWorkflow/Evidence readerで読めることも確かめます。どれも実ストアと実LocalApplicationDataのストアを読み書きしません（判定suiteのnative probe 1件だけが、実LocalApplicationDataの直下に自分のprobe directoryを作って消します。他のnative caseはLocalApplicationDataの代わりに渡す一時directoryの上で動きます）。

H1のこのtaskは3 .NET Release build、全Artifacts PowerShell parse、11 Artifact suite、Workflow TaskLifecycle、Harness、適用taskのcontract/docs auditを固定5 stepで収録します。registered/selected/executedは各非空同集合、failed 0、実DLL path/hash/MVID/depsと生結果を保存します。B exitはdiscovery、最終Cはjudgment、CBlindは同base/headと同じ固定入力です。Unity source/依存を変更しないためUnity test/buildはこのsliceに適用しません。

```powershell
dotnet build tools/Artifacts/Probe/R2RouteTransport.csproj -c Release -o tools/Artifacts/Probe/artifacts/probe
dotnet build tools/Artifacts/Packaging/ArtifactPackaging.csproj -c Release -o tools/Artifacts/Packaging/artifacts/package
dotnet build tools/Artifacts/Transport/R2ArtifactTransport.csproj -c Release -o tools/Artifacts/Transport/artifacts/transport
pwsh tools/harness.ps1 current -Task artifact-evidence-lifecycle
pwsh tools/harness.ps1 run -Task artifact-evidence-lifecycle -Stage discovery -Difference 初回 -Question '固定offline集合が通るか' -StopWhen '全必須集合を収録'
pwsh tools/contract-audit.ps1 -HarnessTask artifact-evidence-lifecycle
pwsh tools/docs-audit.ps1
```

PR #106の非秘密dummy fixtureをv2に再利用します。各testは隔離store、clock/transport/scheduler注入とbarrier/signalを使い、30日待機やTask.Delay/Thread.Sleep、秘密や実E1 source、実R2 DELETEを必要としません。DPAPI/ACLの成立はowner Windowsユーザーで確認し、別sandboxユーザーの結果を流用しません。synthetic診断と過去first-useの成功を新v2、scheduler、resetの成立証拠にしません。EvidenceCleanup suiteの `fresh-process-cleanup-contract` は `rg`（ripgrep）でsource一覧を取るため、全suiteを流す前にrgをPATHへ通します。無いとこのcaseだけが失敗します。
