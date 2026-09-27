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

1. ownerがaccount固有S3 endpointと`osm-artifacts`だけに限定したObject Read & Write tokenを管理する。profileが無ければownerが自身の対話端末から既存CLIへmasked初回登録する。owner確認済みの正常なactiveがあれば、ACLとrecordを既存storeで検証して再利用し、再入力・`set`・`--replace`は行わない。破損、危険なACL、想定外のprofile/metadata、登録失敗では内容を推測・自動修復・上書きせず止める。token/secret/API管理鍵をchat、引数、env、Git、Evidenceに渡さない。ownerはbucketのprivate設定を確認する。endpointは非秘密の`-Endpoint`引数として各probe processに渡し、HTTPSのR2 account hostnameに限定する。storeのv1 schema、既存credentialsコマンド、環境変数は変更しない。
2. Agentが同じWindowsユーザーで一意な`probe/unlocked/<run-id>/` keyに固定synthetic bytesをPUTする。生成時に転送物から独立して保持したSHA-256を記録し、**別のpwsh process**が同じprofileでGETしてbyte数、hash、markerを一致させる。正常なunsigned GETが同じ存在keyの内容を返さない。認証GET成功を先に確認し、DNS/TLS/proxy失敗、redirect、timeout、404を非公開性の成功と数えない。保護無しkeyは同じwriterが異なるbyteで同じkeyを上書きし、GETでhash変化を確認してからDELETEできることを確かめる。
3. ownerはaccount設定権限で`probe/locked/`に限る有限のBucket Lock ruleを作り、prefix/保持期間/lifecycleとの関係を非秘密で記録する。試験後の残存synthetic objectとruleの後片付け時期も記録する。writer tokenにはbucket設定権限がないことをownerの権限設定で確認する。同じwriterがrule適用後にそのprefixへ**新規synthetic object**をPUTし、GET/hash一致を確認してから、異なるbyteで同一keyの上書きと削除を試み、双方拒否を観測する。再GETで原hashを確認する。拒否原因を単なる接続不能・認証失敗・writer権限不足と混同しない。
4. 実鍵、署名URL、Authorization header、HTTP body/header、SDK例外本文を通常出力・ログ・Git・子process引数に残さない。dummy sentinelによる正常/失敗/timeout/cancelの漏洩検査を行う。通信診断は既定で無効にし、実通信の結果はallowlistで構成した非秘密のkey、期待/観測hash、byte数、HTTP status分類、lock ruleの非秘密設定、実行UTC、cleanup状態、base/headだけを出す。結果schema外の出力は証拠への保存を拒否する。checkoutの意図しない変更がないことを前後で確認する。

### A2再レビュー後の判定表案（A3未凍結）

| 対象 | 有限範囲と合格判定 |
| --- | --- |
| synthetic fixture / key | ASCII markerとrun-idを含む各fixtureは1〜1024 byte。1 runで生成するremote keyは`probe/unlocked/<run-id>/...`と`probe/locked/<run-id>/...`の各1個、合計2個まで。上書き用の異なるfixtureは同じkeyに使う。run-idとkeyは厳密な全体一致検査を通し、制御文字を拒否する。 |
| 通信 / 本文 / process | 各SDK操作はheaderと本文EOFを含め30秒以内。非同期SDK呼出しとstream読取へ同じdeadlineのcancelを渡す。通信は親が監督する子processに置き、子`pwsh`は45秒以内に終了確認する。超過時は当該子processを終了確認して親の判定へ戻る。run全体は5分以内に判定へ進む。byte上限は時間上限の代わりにしない。 |
| cleanup | 本処理の期限と別の30秒を各保護なしkeyの削除・確認に与える。期限超過、通信不能、応答不明は`unconfirmed`とし、`removed`にしない。lock対象は保持期限前に削除せず`retained-by-lock`とkey/rule/保持期限/清掃予定を記録する。 |
| 結果 | 外部出力は固定schemaの`result`、`phase`、`operation`、検証済み`key`、期待/観測hash・byte数、分類済みHTTP結果、非秘密の`generation`、lock rule設定、実行UTC、cleanup、base/headに限定する。各fieldの型・長さ・値域を検査し、余計なproperty、任意の例外文・SDK response・秘密を拒否する。入力前失敗は固定code/phaseのみとしkeyを出さない。`pass`はexit 0、`provider-capability-failure`は2、`environment-blocked`は3、`inconclusive`は4。cleanupが`unconfirmed`なら観測結果を残して`inconclusive`/exit 4にする。 |
| privacy | 認証GETで同じ存在keyのhash/byte数を確認した後、署名無しGETにR2由来の想定拒否を確認したときだけ成立。404、redirect、429/5xx、DNS/TLS/proxy、timeout、認証GETの認証失敗は拒否成功に数えない。HTTP status単独で原因を断定しない。 |
| lock | 同じ`generation`の通常writerが保護なしkeyを上書き・GET・削除できた陽性対照を先に成立させる。lock設定のowner記録、適用後の新規PUT/認証GET、上書きとDELETEそれぞれのR2由来の想定拒否、再GETで原hash一致が全て揃ったときだけ成立。403単独、認証失敗、404/429/5xx、redirect、DNS/TLS、timeoutをlock成功に数えない。試験中はactiveを変更せず、各processの検証済みrecordの非秘密`Generation`を結果に含めて同一writerを対応付ける。 |

`provider-capability-failure`は開始条件・陽性対照が成立し、必要能力の否定が再現できた場合だけとする。それ以外の拒否原因が曖昧な場合は`inconclusive`、設定・接続・権限の準備不足は`environment-blocked`として未実施条件を残す。offlineのcontrolled transportではheader後の本文停止、指定byte数を返してEOFしない、子process停止、cleanup停止を注入し、期限・終了確認・`unconfirmed`を検査する。

GOは1〜4の実測が揃い、通常writerとaccount設定者の権限分離も確認したときだけ。実R2未実行やowner設定未完了はGOにしない。試験結果は`pass`、`provider-capability-failure`、`environment-blocked`、`inconclusive`に分類する。開始条件と陽性対照が成立した後にprivacyまたはlockの必要能力が再現性をもって失敗した場合だけ、spikeの否定結果として`CONDITIONAL ACCEPT`で閉じ、R2を次段に進めずprogramの新Phase Aで経路を再選定する。token/endpoint/egress/lock ruleの準備不足は後二者として未実施条件と再試行責任者を残し、sliceも完了しない。このspikeは未実施のまま期限切れで完了扱いにしない。GOまたは上記の否定結果が得られたら終了し、CLI/Cloud/Buildまで範囲を伸ばさない。

ここでは答えない問いと所有先: 反復利用できる`publish/fetch`、安全なpackage/extraction、信頼済みledgerはprogram r4の「最小のArtifact CLI」。実EvidenceとC/C'のログ/画像閲覧は「Evidence first use」。Cursor/Codex Cloudごとの無人grantと能力は「Cloud」。大きいBuildとmultipart、Unity以外のbackend実証は「Build」。実鍵rotation・失効/復旧は「最小のArtifact CLI」までに行い、このprobe成功だけで長期運用を開始しない。

## 3. A1 — 責務と実装境界

- `tools/Artifacts/Credentials/CredentialStore.psm1`（現在239物理行、+50〜80見込み）: profileのDPAPI/ACL検査と1操作中の秘密使用だけを所有。既存moduleの**CLIではない内部向けexport**を一つ追加し、同期したtrusted transport callbackへ復号済みAccess Key ID/Secret Keyと非秘密`Generation`を同一process内で渡す。probe processごとに一度だけ呼び、child process/env/引数へ秘密を渡さない。callbackの結果は許可した非秘密schemaだけに絞り、例外/SDK response/資格情報objectを戻り値やstdoutへ通さない。失敗時は固定の非秘密errorに分類し、参照を操作終了時に解放する。Success streamの戻り値検査だけを秘密非記録の保証とせず、Warning/Verbose/Debug/Information/Host/Console、SDK loggingと例外の各出口を確認する。PowerShell module間で呼ぶにはexportが必要なので「非export」を安全境界とはしない。storeはR2、lock、hashを知らない。dummy sentinelで正常/失敗/timeout/cancel時の漏洩とファイル不変を確認する。同一Windowsユーザーからの隔離は主張しない。+50%未満、500行未満でも新しい秘密使用面として構造レビューする。
- `tools/Artifacts/Probe/RouteProof.ps1`（新規、約120〜170行）: 非秘密の`-Endpoint`、固定profile、mode/key/hashを厳密に検証し、fixtureと事前hash、別processのwrite/readの起動と期限、陽性対照/lock結果の判定、非秘密resultを所有する。子processにはendpoint、key、期待hashなど非秘密引数だけを渡す。S3 SDKや資格情報を知らず、fake transportで判定を単体テストする。
- `tools/Artifacts/Probe/R2RouteTransport.psm1`と同階層の小さな.NET project（新規、合計約180〜250行）: profile callback内のS3 client/HTTP request、PUT/GET/DELETE、署名無しrequest、deadlineを伝播するstream/応答破棄、分類済み非秘密resultを所有する。通信診断は無効にし、raw出力を収集後redactする方式を使わない。bucket設定変更やprocess起動はしない。SDK型をprobe外へ公開せず、通信自体は実R2のsynthetic integrationで確認する。PowerShellと.NETの分担は同一process呼出しと秘密非記録を保つ範囲でBが最小化できるが、新たなowner/依存/public APIが必要ならAへ戻す。
- `tools/Artifacts/tests/`（既存Credentials.Tests.ps1は390行、+40〜60、新規probe試験は約100〜160見込み）: 既存dummy/ACL回帰とcallbackの漏洩・失敗保全を確認。R2の成功をfake成功で代替しない。既存テストが500行に達しそうなら、新しいprobeの試験を別ファイルに置く。
- `tools/Artifacts/README.md`（現在約44行、+20〜40見込み）: 診断の一時的な使い方、owner設定、実装済み/未検証、残存locked objectの扱いを記載。成功を一般CLIの完成と表現しない。

このsliceのコードは`tools/Artifacts`内に限定し、Unity asmdef/API、`tools/DebugStudio`、BuildSystemを変更しない。CredentialStoreは鍵の寿命、診断はnetwork/判定、ownerはaccount設定を持つ。probeは後のCLIへそのまま昇格する前提にせず、必要な境界だけを次のPhase Aで選び直す。計画外の状態、永続schema、公開API、owner/依存の変更が必要ならBで独断せずAを再開する。

## 4. 実装・検証経路

Phase Bは既存storeからの限定的な同一process利用、限定R2診断、offlineのdummy/fake試験、READMEと`pwsh tools/contract-audit.ps1`を担当する。SDKは依存を固定し、秘密を扱う前にrestore/buildと別processからの型loadを確認する。Phase Bは実R2の合否を宣言しない。

Phase Cの差し戻し中は`Credentials.Tests.ps1`の新callback caseと新probe試験の失敗caseを起点に選ぶ。判定Cではそれぞれ空filterのoffline suite、対象限定の実端末masked入力、実R2のwrite/read/unsigned/unlocked delete/locked overwrite+delete、allowlist結果と原hash、`contract-audit`、`docs-audit`、固定diffの構造レビューを必須とする。offline suiteにはendpoint/SDK/key/hash/byte数/制御文字の不正入力と、Success以外のPowerShell stream、Host/Console、SDK logging、例外、timeout/cancelにdummy sentinelを注入した非記録検査を含める。通信・本文・子process・cleanup停止のcontrolled transport検査では有限終了と残存の分類を確認する。実鍵のraw出力を保存してからredactしない。実装変更はUnity側に一切及ばず、全EditMode回帰は適用除外とし、PowerShell/.NET offline回帰と実R2で代替する。Unityコード・packageへ変更が必要ならAへ戻す。

実行経路: ownerがCloudflare dashboardで非秘密endpointを取得しbucket限定tokenを発行、対話端末でmasked登録する。lockはownerが同dashboardで`probe/locked/`限定・短期のruleを設定し、通常writerとは異なる管理権限で保有する。Agentは同じWindowsユーザーの二つのpwsh processから限定診断を実行する。Cloudflare設定の証拠はtoken値を含まないownerの設定記録、通信証拠はallowlistの非秘密resultと保存前後のrepo statusとする。実鍵使用時のSDK/HTTP raw output、request/response、例外本文は保存しない。Cはcheckout/同期領域外の限定ACL evidence bundleに固定base/headとraw offline test結果、sanitized route result、固定diffを収録し、hash/場所/保持期限をHANDOFFに記す。Cの所見は別記録にする。C' blind bundleはA3 snapshot、所見のないB result、同じ固定diff、sanitized結果、C前の機械検査だけから別に生成し、path/hash/生成UTCを記録する。C'は別セッションでbundleを読み、Cの所見を受け取らない。lock済みobjectは期限前に削除せず、key、rule、保持期限、清掃予定とownerを非秘密台帳へ残す。spike終了時にtokenを残す場合は次sliceでの用途と失効担当をownerが明記し、経路を不採用にする場合はownerがremote失効を確認した後にlocal profileを削除する。local削除だけを失効と呼ばない。

A3前の疎通: `pwsh`、.NET 8でのAWSSDK.S3 restore/buildと別pwshでのload、非秘密JSONの別process読取は2026-09-27の前案で確認した。ownerが実鍵をmasked登録した際、既存`Artifacts`ディレクトリの所有者不一致で初回登録が失敗した。空ディレクトリをowner端末で非再帰削除し、新規ACLへ実行ユーザーを明示的に設定する修正後、登録と`status`が成功した。owner端末の限定CLIで`probe/prea3/9ace5edb614d4203a6d71d6a8069e498.txt`の48 byteをPUTし、別`pwsh` processが認証GETしてSHA-256 `13cbdab75bf6856a24088bf26d230a2cd58ad3d6f1faacc86615bdb949c4e255`とmarkerを照合、同じwriterでDELETEした。ownerが提示したsanitized出力は`roundtrip: pass; ...; cleanup=removed`であり、別の読み取り専用CLIで`probe/prea3 empty: true`を確認した。初回probe実装にあった非IDisposable応答へのDispose呼出しによる失敗表示は修正済み。これらはowner端末から提示された出力であり、Agentが実鍵を直接扱った記録ではない。旧`PreA3RoundTrip.ps1`は通信本文、子process、cleanupに全体期限がないため再実行用入口から退役した。過去の疎通記録は維持するが、旧コードをRoute proofへ流用しない。

ownerはCloudflare画面でtokenのR2 Bucket Item Read/Writeが`osm-artifacts`だけを対象とし、Public Development URLが無効、Custom Domainsが空であることを確認した。A3前の認証付き経路は成立したが、署名無しGET、lock設定と通常writerへの強制力、Cloud到達性は未確認。Cでの本検証案は§2のまま。DashboardのSettings→Bucket lock rulesでprefixを`probe/locked/`に限定し有限の保持期間を設定できることはCloudflare公式文書で確認した。owner権限と証拠受入方法は§5にA3確認案として記載した。

## 5. A2 / A3 と後続Phase

- A0/A1: 本sessionのCodex（実際のmodel variantは未確認）。目的からの独立A0代替案は、R2を第一候補としつつSDK Adapter、server検証付きrotation、複数provider抽象を最初の条件にしない点で一致。レビュー担当の実モデル割当は未確認。
- A2: 同一初稿SHA-256`9A85555C5FA37F24DE9DDADB66C94B195BF5313C0F19E18D07E08DA1E707D329`を、アーキテクチャと失敗/実行可能性の独立した担当がレビューした。両者のendpoint受渡し・callback・診断責務・raw実鍵出力の非保存・陽性対照・否定結果の終端・C' bundle分離の指摘を採用して§2〜4へ反映。programとsliceの別branch、A3前の実経路疎通の指摘も採用し、metadataとA3待機条件を修正した。モデル指定と実割当variantの一致は確認できないため多様性は未証明。未採用は「callbackをmoduleから非exportにする」案で、別moduleから呼ぶPowerShell関数はexportが必要なため。CLIに秘密取得コマンドは設けず、同一ユーザーが既にDPAPI復号可能な前提で、非記録の狭い内部APIとして扱う。
- A2再レビュー（対象head `9704a6d7eebc8349e46f5005667c525396e6d2ed`、静的レビュー）: 指摘A/Bを採用。旧preflightには操作/本文/子process/cleanupの全体期限がなく、入力検証前の失敗出力に`Key`を反射する経路がある。予備疎通は達成済みなので、旧entry pointと専用SDK projectを退役し、READMEの再実行手順を除去する。A/Bの失敗経路は新Route proofの§2判定表とoffline試験へ移し、旧コードを修正して再利用しない。指摘Cを採用。bool戻り値はSuccess streamしか制限しないため、別stream/Host/Console/SDK logging/例外へのsentinel試験を§3〜4に明記した。指摘Dを採用。既存activeの再利用と異常時fail closedを§2.1へ分け、同一writerの対応付けに既存の非秘密`Generation`を使う案とした。全面的な責務再設計、provider registry、Cloud/実payloadの前倒しは不採用（このsliceの問いに不要）。このレビューはA3承認ではない。
- A3未実施: ownerは方向を概ね了承し、凍結前にR2 tokenを所有者端末のCLIへ登録してCLIから実R2を利用できることを条件とした。§4の限定preflightでこの条件を達成し、ownerはR2 Bucket Item Read/Writeの対象が`osm-artifacts`のみ、Public Development URL無効、Custom DomainsなしとCloudflare画面で確認した。その後、ownerは再レビューのため凍結解除を指示した。A2統合案にあるアーキテクチャ・失敗経路の指摘は採否候補として§2〜4へ反映済みだが、再レビュー後にprogramとsliceを別々にA3判断する。`credentials` CLIはローカル保管のみ、A3前の使い捨てprobe CLIは認証付き経路の予備確認であり、§2の署名無しGET、保護なし陽性対照、Bucket Lockの上書き/削除拒否は未検証。
- A3で確認する操作・証拠受入案: ownerだけがCloudflare dashboardで`probe/locked/`限定の有限Bucket Lock ruleを設定し、rule名、prefix、保持期間、適用UTC、lifecycleとの関係、残存objectとruleの清掃予定を非秘密で記録する。通常writer tokenにBucket設定権限を追加しない。Agentは同一Windowsユーザーの別`pwsh` processを起動して§2のsynthetic probeとoffline試験を行い、非秘密のallowlist結果、固定base/head、前後のrepo status、raw test結果をcheckout/同期領域外の限定ACL bundleに保存する。Cはownerの設定記録を受理し、通信結果のhash・byte数・HTTP分類と陽性対照、lock時の再GETを突き合わせる。C'へは所見を含まない同一固定版のblind bundleを渡す。ownerがrule設定に到達できない場合はenvironment-blockedで止め、Agentが管理鍵を受け取って代行しない。
- C'担当: Phase B/Cと異なるmodelの新規session、または人間。Phase Aに未関与の候補を残す。判定Cの固定base/head、raw結果とblind bundleを使う。
- Phase B/C/C'/D: 未到達。各snapshot、実行結果、未確認、担当/モデル、採否を到達時に記録する。
