# Artifact Storage Route Proof — Phase A

## 0. メタデータ

- type: `slice`（R2候補のローカル必要条件を調べるspike）
- status: A2統合案・再レビュー待ち。A3凍結は解除済み。Phase B以降は未着手
- branch: `codex/artifact-storage-route-proof`（program草案とA3前疎通を記録した`93d2a1c`から分岐）
- candidate implementation base commit: `93d2a1c`（A3前疎通のbranch point。凍結解除は本review headに記録）
- implementation head commit: 未到達
- risk: `high`（初回の実鍵・実R2・bucket lock設定）
- owner: OSM保守担当。Cloudflare設定とtoken発行/失効はaccount owner
- created: 2026-09-27
- expires: 2026-12-26
- harvest to: 現行CLIの契約は`tools/Artifacts/README.md`、恒久的な保存/検証境界は適切な公開設計文書。probe固有の記録は削除
- Phase A snapshot: 再レビューと人間のA3採否後にcommit/hashで固定する。B result、C evidence、C' blind bundleは各Phaseで生成する

## 1. A0 — 目的、現況、対象外

目的は、R2を選定済みと扱う前に、既設の非公開`osm-artifacts`がローカルWindows Agentによる非公開・同一byteの受け渡しと、通常writerからのserver側保護という**必要条件**を満たすか実測すること。program r4とこのsliceの採否は再レビュー後にそれぞれA3で判断する。

段1で`credentials set/status/remove`、DPAPI CurrentUser保管、原子的なローカル置換を実装済み。2026-09-27にownerが実R2 S3鍵をmasked登録し、`status`で現行profileを確認した。store recordのendpointは未設定で、非秘密のaccount固有endpointを限定probeの引数に渡す。A3前のsynthetic PUT/別process GET/照合/DELETEはowner端末で成功した。ownerはtokenが`osm-artifacts`のみにR2 Bucket Item Read/Writeを持ち、Public Development URL無効、Custom Domainsなしと画面で確認した。署名無しGET、Bucket Lockの実効性、Cloud到達性は未確認。GitHub Releaseのsynthetic probeはlocal/Cursor Cloudで成功したが非公開Evidenceの検証ではなく、Codex Cloudは当時のegress 403で失敗した。

対象外は実Evidence・実Buildのupload、一般向け`publish/fetch`とsafe extraction、鍵rotation/retired管理、Cloudの無人grant・read/write・画像閲覧、複数providerの登録/切替、Unity/BuildSystem変更、継続運用用retention決定である。Cloudflareのlock管理権限をAgentへ渡さない。このsliceの結果だけでR2採用確定や実payload解禁を宣言しない。

このsliceが答える問い: **所有Windowsユーザーの二つの独立processが、既存DPAPI profileを用いてprivate R2上の小さなsynthetic objectを同一hashで往復し、同じ通常writerの上書き/削除が限定prefixのBucket Lockで拒否されるか。**

## 2. 進める最低条件と判定

1. ownerがaccount固有S3 endpointと`osm-artifacts`だけに限定したObject Read & Write tokenを管理する。profileが無ければownerが自身の対話端末から既存CLIへmasked初回登録する。owner確認済みの正常なactiveがあれば、ACLとrecordを既存storeで検証して再利用し、再入力・`set`・`--replace`は行わない。破損、危険なACL、想定外のprofile/metadata、登録失敗では内容を推測・自動修復・上書きせず止める。token/secret/API管理鍵をchat、引数、env、Git、Evidenceに渡さない。ownerはbucketのprivate設定を確認する。endpointは親だけが非秘密の`-Endpoint`引数として各probe processに渡し、`^https://[0-9a-f]{32}\.r2\.cloudflarestorage\.com$`に全体一致させる。path、query、userinfo、port、管轄別hostnameは受け付けない。既設bucketのAPACはlocation hintであり管轄endpointではなく、A3前疎通で使ったaccount endpointに固定する。bucketは引数にせず、既存storeが検証する`osm-artifacts`に固定する。storeのv1 schema、既存credentialsコマンド、環境変数は変更しない。
2. Agentが同じWindowsユーザーで一意な`probe/unlocked/<run-id>/` keyに固定synthetic bytesをPUTする。生成時に転送物から独立して保持したSHA-256を記録し、**別のpwsh process**が同じprofileで認証GETしてbyte数、hash、markerを一致させる。署名無しGETは同じ存在keyに対してAuthorizationと署名query無しで行い、期限内に本文をEOFまで読み切ったうえでS3 `Code=Unauthorized`（401）または`AccessDenied`（403）かつ本文hashがobjectと異なる場合だけ非公開性を認める。元objectと同じbyte列、またはそのbyte列に追加byteが続く応答は内容露出の観測とする。部分読取やtimeout、`SignatureDoesNotMatch`、404/`NoSuchKey`、redirect、429/5xx、DNS/TLS/proxy失敗、認証GETの認証失敗は成功に数えない。保護無しkeyは同じwriterが異なるbyteで同じkeyを上書きし、GETでhash変化を確認してからDELETEし、認証GETの`NoSuchKey`で削除確認する。
3. ownerはaccount設定権限で`probe/locked/`に限る有限のBucket Lock ruleを作り、全有効ruleのprefix、有効状態、保持条件（秒数/期限/Indefinite）、lifecycleとの関係を非秘密で記録する。試験後の残存synthetic objectとruleの後片付け時期も記録する。writer tokenにはbucket設定権限がないことをownerの権限設定で確認する。同じwriterがrule適用後にそのprefixへ**新規synthetic object**をPUTし、GET/hash一致を確認してから、異なるbyteで同一keyの上書きと削除を試みる。双方でS3 `Code=ObjectLockedByBucketPolicy`（403）を観測し、その後の認証GETで原hashが一致したときだけlockを認める。単なる403、接続不能、認証失敗、writer権限不足は成功に数えない。
4. 実鍵、署名URL、Authorization header、HTTP body/header、SDK例外本文を通常出力・ログ・Git・子process引数に残さない。dummy sentinelによる正常/失敗/timeout/cancelの漏洩検査を行う。通信診断は既定で無効にし、実通信の結果はallowlistで構成した非秘密のkey、期待/観測hash、byte数、HTTP status分類、S3 `Code`、lock ruleの非秘密設定、実行UTC、cleanup状態、base/headだけを出す。結果schema外の出力は証拠への保存を拒否する。checkoutの意図しない変更がないことを前後で確認する。

### 最低条件2〜4の判定詳細（A3未凍結）

| 対象 | 有限範囲と合格判定 |
| --- | --- |
| synthetic fixture / key | ASCII markerとrun-idを含む各fixtureは1〜1024 byte。1 runで生成するremote keyは`probe/unlocked/<run-id>/...`と`probe/locked/<run-id>/...`の各1個、合計2個まで。上書き用の異なるfixtureは同じkeyに使う。run-idとkeyは厳密な全体一致検査を通し、制御文字を拒否する。 |
| 通信 / 本文 / process | 成功経路は保護なしkeyのPUT→別process認証GET→署名無しGET→上書きPUT→認証GET→DELETE→削除確認GET、lock keyのPUT→認証GET→上書きPUT→DELETE→再GETの計12操作。1子processが1操作だけを行う。各操作はheaderと本文EOFを含め30秒以内で、非同期SDK呼出しとstream読取へ同じdeadlineのcancelを渡す。親は子`pwsh`を45秒以内に終了確認し、超過時は当該子processを終了確認する。run全体は5分の独立上限を持ち、残り時間を使い切ったら未了の子を終了確認して`inconclusive`へ進む。各操作が30秒以内でも全操作が5分内に完了する保証はない。byte上限は時間上限の代わりにしない。 |
| cleanup | 保護なしkeyの通常DELETEと削除確認GETは上記12操作とrun 5分に含め、ここでの成功だけを陽性対照に数える。本処理が途中で失敗してkeyが残り得る場合に限り、別の30秒で回復清掃のDELETE・確認を試みる。結果確定までの最大はこの場合5分30秒で、回復清掃は失敗したrunをGOへ変えない。期限超過、通信不能、応答不明は`unconfirmed`とし、`removed`にしない。lock対象は保持期限前に削除せず`retained-by-lock`とkey/rule/保持期限/清掃予定を記録し、追加通信はしない。 |
| 結果 | 外部出力は固定schemaの`result`、`phase`、`operation`、検証済み`key`、期待/観測hash・byte数、HTTP status分類とS3 `Code`、非秘密の`generation`、lock rule設定、実行UTC、cleanup、base/headに限定する。各fieldの型・長さ・値域を検査し、余計なproperty、任意の例外文・SDK response・秘密を拒否する。未知のS3 `Code`は`^[A-Za-z][A-Za-z0-9]{0,63}$`に全体一致した列挙名だけを`inconclusive`へ記録し、Messageを出さない。入力前失敗は固定code/phaseのみとしkeyを出さない。`pass`はexit 0、`provider-capability-failure`は2、`environment-blocked`は3、`inconclusive`は4。cleanupが`unconfirmed`なら観測結果を残して`inconclusive`/exit 4にする。 |
| privacy | 先に認証GETで同じ存在keyのhash/byte数を確認する。署名無しGETはAuthorization headerと`X-Amz-*`署名queryの無いrequestとし、S3 `Code`が`Unauthorized`（401）または`AccessDenied`（403）、かつ期限内に上限付きで本文をEOFまで読み切った全体のSHA-256がobjectと異なる場合だけ成立。本文は保存しない。途中で止まった読取の空hash・部分hashは合格に使わない。objectと同じbyte列が返る、またはそのbyte列に追加byteが続く応答はprivacy不成立の観測。拒否のerror XMLがobjectより長いだけでは内容露出と扱わない。上限超過・EOF未確認・`SignatureDoesNotMatch`・404/`NoSuchKey`・redirect・429/5xx・DNS/TLS/proxy・timeout・認証GETの認証失敗・許可リスト外codeは成功に数えない。 |
| lock | 同じ`generation`の通常writerが保護なしkeyを上書き・GET・削除できた陽性対照を先に成立させる。ownerが全有効ruleを記録し、試験時は`probe/locked/`の有限ruleがちょうど1件で、空prefix、他prefix、`probe/unlocked/`に掛かるrule、Indefiniteがないことを確認する。適用後の新規PUT/認証GET、上書きとDELETEの双方がS3 `Code=ObjectLockedByBucketPolicy`（403）、再GETで原hash一致が全て揃ったときだけ成立。403単独、認証失敗、404/429/5xx、redirect、DNS/TLS、timeout、許可リスト外codeをlock成功に数えない。試験中はactiveを変更せず、各processの検証済みrecordの非秘密`Generation`を結果に含めて同一writerを対応付ける。 |

`provider-capability-failure`は開始条件・陽性対照が成立し、必要能力の否定が再現できた場合だけとする。privacyの内容露出は新しいrun/keyで再現性を確認する。各processの`Generation`不一致、拒否原因不明、許可リスト外codeは`inconclusive`とし、rule記録や設定・接続・権限の準備不足は`environment-blocked`として未実施条件を残す。保護なしkeyの上書きが429/`TooManyRequests`なら1秒以上の書込み間隔を空けて1回だけ再試行し、再び429なら`inconclusive`とする。lock keyの上書きが1回成功しただけではproviderを否定せず、1秒以上の書込み間隔を取った1回の再試行でも`ObjectLockedByBucketPolicy`にならず、再GETで変更後hashが確認できた場合だけ否定結果候補とする。両再試行はrunの5分内に含め、無制限に繰り返さない。3xxは追跡せず、署名済みrequestを別hostへ送らない。offlineのcontrolled transportではheader後の本文停止、指定byte数を返してEOFしない、子process停止、回復清掃停止を注入し、期限・終了確認・`unconfirmed`を検査する。

S3 `Code`と同一keyの書込み制限は[R2 error codes](https://developers.cloudflare.com/r2/api/error-codes/)、ruleの重なりと適用範囲は[Bucket Locks](https://developers.cloudflare.com/r2/buckets/bucket-locks/)、lock設定APIの非互換は[S3 API compatibility](https://developers.cloudflare.com/r2/api/s3/api/)、APAC location hintと管轄endpointの違いは[Data location](https://developers.cloudflare.com/r2/reference/data-location/)を参照する。lock ruleの判定にS3の`GetObjectLockConfiguration`/`PutObjectLockConfiguration`を使わない。

GOは1〜4の実測が揃い、通常writerとaccount設定者の権限分離も確認したときだけ。実R2未実行やowner設定未完了はGOにしない。試験結果は`pass`、`provider-capability-failure`、`environment-blocked`、`inconclusive`に分類する。開始条件と陽性対照が成立した後にprivacyまたはlockの必要能力が再現性をもって失敗した場合だけ、spikeの否定結果として`CONDITIONAL ACCEPT`で閉じ、R2を次段に進めずprogramの新Phase Aで経路を再選定する。token/endpoint/egress/lock ruleの準備不足は後二者として未実施条件と再試行責任者を残し、sliceも完了しない。このspikeは未実施のまま期限切れで完了扱いにしない。GOまたは上記の否定結果が得られたら終了し、CLI/Cloud/Buildまで範囲を伸ばさない。

ここでは答えない問いと所有先: 反復利用できる`publish/fetch`、安全なpackage/extraction、信頼済みledgerはprogram r4の「最小のArtifact CLI」。実EvidenceとC/C'のログ/画像閲覧は「Evidence first use」。Cursor/Codex Cloudごとの無人grantと能力は「Cloud」。大きいBuildとmultipart、Unity以外のbackend実証は「Build」。実鍵rotation・失効/復旧は「最小のArtifact CLI」までに行い、このprobe成功だけで長期運用を開始しない。

## 3. A1 — 責務と実装境界

- `tools/Artifacts/Credentials/CredentialStore.psm1`（現在239物理行、+50〜80見込み）: profileのDPAPI/ACL検査と1操作中の秘密使用だけを所有。既存moduleの**CLIではない内部向けexport**を一つ追加し、同期したtrusted transport callbackを`&`で呼ぶ。dot-sourceせず、復号済みAccess Key ID/Secret Keyと非秘密`Generation`だけを引数に渡して秘密の寿命をstoreの1操作に閉じる。probe processごとに一度だけ呼び、child process/env/引数へ秘密を渡さない。callbackの戻り値は許可した非秘密schemaだけに絞り、例外/SDK response/資格情報objectを戻り値やstdoutへ通さない。失敗時は固定の非秘密errorに分類し、参照を操作終了時に解放する。Success streamの戻り値検査だけを秘密非記録の保証とせず、Warning/Verbose/Debug/Information/Host/Console、SDK loggingと例外の各出口を確認する。PowerShell module間で呼ぶにはexportが必要なので「非export」を安全境界とはしない。storeはR2、lock、hashを知らない。dummy sentinelで正常/失敗/timeout/cancel時の漏洩とファイル不変を確認する。同一Windowsユーザーからの隔離は主張しない。+50%未満、500行未満でも新しい秘密使用面として構造レビューする。
- `tools/Artifacts/Probe/RouteProof.ps1`（新規、約120〜170行）: 非秘密の`-Endpoint`、固定profile、mode/key/hashを厳密に検証し、fixtureと事前hash、1操作1子processの起動・期限・終了確認、陽性対照/lock結果の判定、非秘密resultと終了codeを所有する。子processにはendpoint、key、期待hashなど非秘密引数だけを渡す。S3 SDKや資格情報を知らず、fake transportで判定を単体テストする。
- `tools/Artifacts/Probe/R2RouteTransport.psm1`と同階層の小さな.NET project（新規、合計約180〜250行）: profile callback内のS3 client/HTTP request、PUT/GET/DELETE、署名無しrequest、deadlineを伝播するstream/応答破棄を所有する。返すのはoperation、HTTP status、S3 `Code`、byte数、本文hash、期限超過、redirectだけから成る閉じた非秘密の観測で、合否とexit codeは決めない。SDK例外からはstatusと`ErrorCode`だけを読み、Messageの文字列化・応答XML・Authorizationを結果へ通さない。署名無しGETの本文は有限byteで読んでhashし、本文を保存しない。通信診断は無効にし、raw出力を収集後redactする方式を使わない。bucket設定変更やprocess起動はしない。SDK型をprobe外へ公開せず、通信自体は実R2のsynthetic integrationで確認する。PowerShellと.NETの分担は同一process呼出しと秘密非記録を保つ範囲でBが最小化できるが、新たなowner/依存/public APIが必要ならAへ戻す。
- `tools/Artifacts/tests/`（既存Credentials.Tests.ps1は390行、+40〜60、新規probe試験は約100〜160見込み）: 既存dummy/ACL回帰とcallbackの漏洩・失敗保全を確認。route判定と停止注入は変更理由が異なるので、probe試験は最初から別ファイルに置く。行数500を分離理由にしない。R2の成功をfake成功で代替しない。
- `tools/Artifacts/README.md`（現在約44行、+20〜40見込み）: 診断の一時的な使い方、owner設定、実装済み/未検証、残存locked objectの扱いを記載。成功を一般CLIの完成と表現しない。

このsliceのコードは`tools/Artifacts`内に限定し、Unity asmdef/API、`tools/DebugStudio`、BuildSystemを変更しない。CredentialStoreは鍵の寿命、診断はnetwork/判定、ownerはaccount設定を持つ。probeは後のCLIへそのまま昇格する前提にせず、必要な境界だけを次のPhase Aで選び直す。計画外の状態、永続schema、公開API、owner/依存の変更が必要ならBで独断せずAを再開する。

## 4. 実装・検証経路

Phase Bは既存storeからの限定的な同一process利用、限定R2診断、offlineのdummy/fake試験、READMEと`pwsh tools/contract-audit.ps1`を担当する。SDKは依存を固定し、秘密を扱う前にrestore/buildと別processからの型loadを確認する。Phase Bは実R2の合否を宣言しない。

Phase Cの差し戻し中は`Credentials.Tests.ps1`の新callback caseと別ファイルの新probe試験の失敗caseを起点に選ぶ。判定Cではそれぞれ空filterのoffline suite、既存activeの所有者確認とstore検証（未登録時だけownerの対話端末でmasked初回入力）、実R2のwrite/read/unsigned/unlocked delete/locked overwrite+delete、allowlist結果と原hash、`contract-audit`、`docs-audit`、固定diffの構造レビューを必須とする。offline suiteにはendpoint/SDK/key/hash/byte数/制御文字の不正入力、操作別の`NoSuchKey`、`TooManyRequests`、`ObjectLockedByBucketPolicy`、異なる`Generation`とrule不足の分類、Success以外のPowerShell stream、Host/Console、SDK logging、例外Message、timeout/cancelにdummy sentinelを注入した非記録検査を含める。S3 `Code`は許可リスト値のままMessageだけにsentinelを置き、全出力にsentinelが出ないことを確認する。通信・本文・子process・cleanup停止のcontrolled transport検査は注入した時計と停止を通知するfake process/transportで有限終了と残存の分類を確認し、テストで`Task.Delay`/`Thread.Sleep`や30秒/45秒の実時間待機をしない。実鍵のraw出力を保存してからredactしない。実装変更はUnity側に一切及ばず、全EditMode回帰は適用除外とし、PowerShell/.NET offline回帰と実R2で代替する。Unityコード・packageへ変更が必要ならAへ戻す。

実行経路: ownerがCloudflare dashboardで非秘密endpointとbucket限定tokenの設定を確認する。未登録なら対話端末でmasked初回登録し、owner確認済みの正常activeなら再利用する。lockはownerが同dashboardで全有効ruleを確認し、`probe/locked/`限定・短期のruleを設定して通常writerとは異なる管理権限で保有する。Agentは同じWindowsユーザーの独立したpwsh processから限定診断を実行する。Cloudflare設定の証拠はtoken値を含まないownerの設定記録、通信証拠はallowlistの非秘密resultと保存前後のrepo statusとする。実鍵使用時のSDK/HTTP raw output、request/response、例外本文は保存しない。Cはcheckout/同期領域外の限定ACL evidence bundleに固定base/headとraw offline test結果、sanitized route result、固定diffを収録し、hash/場所/保持期限をHANDOFFに記す。Cの所見は別記録にする。C' blind bundleはA3 snapshot、所見のないB result、同じ固定diff、sanitized結果、C前の機械検査だけから別に生成し、path/hash/生成UTCを記録する。C'は別セッションでbundleを読み、Cの所見を受け取らない。lock済みobjectは期限前に削除せず、key、rule、保持期限、清掃予定とownerを非秘密台帳へ残す。spike終了時にtokenを残す場合は次sliceでの用途と失効担当をownerが明記し、経路を不採用にする場合はownerがremote失効を確認した後にlocal profileを削除する。local削除だけを失効と呼ばない。

A3前の疎通: `pwsh`、.NET 8でのAWSSDK.S3 restore/buildと別pwshでのload、非秘密JSONの別process読取は2026-09-27の前案で確認した。ownerが実鍵をmasked登録した際、既存`Artifacts`ディレクトリの所有者不一致で初回登録が失敗した。空ディレクトリをowner端末で非再帰削除し、新規ACLへ実行ユーザーを明示的に設定する修正後、登録と`status`が成功した。owner端末の限定CLIで`probe/prea3/9ace5edb614d4203a6d71d6a8069e498.txt`の48 byteをPUTし、別`pwsh` processが認証GETしてSHA-256 `13cbdab75bf6856a24088bf26d230a2cd58ad3d6f1faacc86615bdb949c4e255`とmarkerを照合、同じwriterでDELETEした。ownerが提示したsanitized出力は`roundtrip: pass; ...; cleanup=removed`であり、別の読み取り専用CLIで`probe/prea3 empty: true`を確認した。初回probe実装にあった非IDisposable応答へのDispose呼出しによる失敗表示は修正済み。これらはowner端末から提示された出力であり、Agentが実鍵を直接扱った記録ではない。旧`PreA3RoundTrip.ps1`は通信本文、子process、cleanupに全体期限がないため再実行用入口から退役した。過去の疎通記録は維持するが、旧コードをRoute proofへ流用しない。

ownerはCloudflare画面でtokenのR2 Bucket Item Read/Writeが`osm-artifacts`だけを対象とし、Public Development URLが無効、Custom Domainsが空であることを確認した。A3前の認証付き経路は成立したが、署名無しGET、owner accountでのlock rule保存と通常writerへの強制力、Cloud到達性は未確認。DashboardのSettings→Bucket lock rulesでprefixと有限保持を設定できることはCloudflare公式文書で確認したにとどまる。ownerによる全有効ruleの確認と限定rule保存はPhase Cで初回確認する。ownerが担当し、不成立なら`environment-blocked`として止め、Agentは管理鍵を受け取らない。Cでの本検証案は§2のまま。owner権限と証拠受入方法は§5にA3確認案として記載した。

## 5. A2 / A3 と後続Phase

- A0/A1: 本sessionのCodex（実際のmodel variantは未確認）。目的からの独立A0代替案は、R2を第一候補としつつSDK Adapter、server検証付きrotation、複数provider抽象を最初の条件にしない点で一致。レビュー担当の実モデル割当は未確認。
- A2: 同一初稿SHA-256`9A85555C5FA37F24DE9DDADB66C94B195BF5313C0F19E18D07E08DA1E707D329`を、アーキテクチャと失敗/実行可能性の独立した担当がレビューした。両者のendpoint受渡し・callback・診断責務・raw実鍵出力の非保存・陽性対照・否定結果の終端・C' bundle分離の指摘を採用して§2〜4へ反映。programとsliceの別branch、A3前の実経路疎通の指摘も採用し、metadataとA3待機条件を修正した。モデル指定と実割当variantの一致は確認できないため多様性は未証明。未採用は「callbackをmoduleから非exportにする」案で、別moduleから呼ぶPowerShell関数はexportが必要なため。CLIに秘密取得コマンドは設けず、同一ユーザーが既にDPAPI復号可能な前提で、非記録の狭い内部APIとして扱う。
- A2再レビュー（対象head `9704a6d7eebc8349e46f5005667c525396e6d2ed`、静的レビュー）: 指摘A/Bを採用。旧preflightには操作/本文/子process/cleanupの全体期限がなく、入力検証前の失敗出力に`Key`を反射する経路がある。予備疎通は達成済みなので、旧entry pointと専用SDK projectを退役し、READMEの再実行手順を除去する。A/Bの失敗経路は新Route proofの§2判定表とoffline試験へ移し、旧コードを修正して再利用しない。指摘Cを採用。bool戻り値はSuccess streamしか制限しないため、別stream/Host/Console/SDK logging/例外へのsentinel試験を§3〜4に明記した。指摘Dを採用。既存activeの再利用と異常時fail closedを§2.1へ分け、同一writerの対応付けに既存の非秘密`Generation`を使う案とした。全面的な責務再設計、provider registry、Cloud/実payloadの前倒しは不採用（このsliceの問いに不要）。このレビューはA3承認ではない。
- A2追加レビュー（対象head `9bdfc3e88ce86e43a0671007ffc52452284fb819`、[PR #80のGrok 4.7所見](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/80#issuecomment-5855225634)）: 旧A2採否を読んだレビューであり、盲検独立とは記録しない。拒否のS3 `Code`、12操作と親/子/run期限、曖昧な結果の分類、全有効ruleと同一`Generation`、transportの観測とprobeの判定の分離、`&` callback、別probe試験、owner設定のPhase C初回確認を採用し§2〜4へ反映。provider否定は再現性を要し、429や1度の上書き成功を即時否定にしない。probeとcredentials CLIのexit 2共通化は最小Artifact CLIへ送る。Cloudflare公開codeと実応答が異なる場合は列挙値だけを非秘密記録して`inconclusive`とし、許可リストの変更はPhase A改訂とする。A3凍結、Route proofのPhase B、実R2の署名無し/lock試験は未実施。
- A2追加レビュー追認（対象head `95a91a9898839b2d6b77bd46e993c656e0c63a30`、[同じGrok 4.7セッションの続き](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/80#issuecomment-5855320551)）: 新しい盲検レビューには数えない。番号付き最低条件2〜4と判定詳細の不一致、部分hashの誤合格、通常DELETEの二重期限、429の分類先、program旧r1の順序とCloudflare設定文を指摘された。条件本文に合格codeと本文読取条件を転記し、通常DELETE/確認は12操作の5分内、別30秒は途中失敗後の回復清掃だけに分けた。保護なし上書きの429再試行は1回で止める。program側は旧順序を歴史化し、公開設定維持とownerのlock rule追加を区別した。A3は未凍結。
- A3未実施: ownerは方向を概ね了承し、凍結前にR2 tokenを所有者端末のCLIへ登録してCLIから実R2を利用できることを条件とした。§4の限定preflightでこの条件を達成し、ownerはR2 Bucket Item Read/Writeの対象が`osm-artifacts`のみ、Public Development URL無効、Custom DomainsなしとCloudflare画面で確認した。その後、ownerは再レビューのため凍結解除を指示した。A2統合案にあるアーキテクチャ・失敗経路の指摘は採否候補として§2〜4へ反映済みだが、再レビュー後にprogramとsliceを別々にA3判断する。`credentials` CLIはローカル保管のみ、A3前の使い捨てprobe CLIは認証付き経路の予備確認であり、§2の署名無しGET、保護なし陽性対照、Bucket Lockの上書き/削除拒否は未検証。
- A3で確認する操作・証拠受入案: ownerだけがCloudflare dashboardで全有効ruleのprefix、有効状態、保持条件を確認し、`probe/locked/`限定の有限Bucket Lock ruleを設定する。rule名、適用UTC、lifecycleとの関係、残存objectとruleの清掃予定も非秘密で記録する。通常writer tokenにBucket設定権限を追加しない。Agentは同一Windowsユーザーの別`pwsh` processを起動して§2のsynthetic probeとoffline試験を行い、非秘密のallowlist結果、固定base/head、前後のrepo status、raw test結果をcheckout/同期領域外の限定ACL bundleに保存する。Cはownerの全rule設定記録を受理し、通信結果のhash・byte数・HTTP分類・S3 `Code`と陽性対照、lock時の再GETを突き合わせる。C'へは所見を含まない同一固定版のblind bundleを渡す。ownerがrule設定に到達できない場合はenvironment-blockedで止め、Agentが管理鍵を受け取って代行しない。
- C'担当: Phase B/Cと異なるmodelの新規session、または人間。Phase Aに未関与の候補を残す。判定Cの固定base/head、raw結果とblind bundleを使う。
- Phase B/C/C'/D: 未到達。各snapshot、実行結果、未確認、担当/モデル、採否を到達時に記録する。
