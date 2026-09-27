# Artifact Storage Route Proof — Phase A

## 0. メタデータ

- type: `slice`（R2候補のローカル必要条件を調べるspike）
- status: A2統合済み。所有者端末でtoken登録とA3前の実R2疎通を確認。token対象bucket・公開設定・権限をowner確認済み。sliceのA3 snapshotは専用branchで固定する。Phase B以降は未着手
- planning branch: `codex/artifact-storage-purpose-first`。programのA3採用後、専用`codex/artifact-storage-route-proof`へ移してsliceのA3 snapshotを固定する
- implementation base commit: `6537446b85bf33056dcdd5b08840940761823711`（`develop`）
- implementation head commit: 未到達
- risk: `high`（初回の実鍵・実R2・bucket lock設定）
- owner: OSM保守担当。Cloudflare設定とtoken発行/失効はaccount owner
- created: 2026-09-27
- expires: 2026-12-26
- harvest to: 現行CLIの契約は`tools/Artifacts/README.md`、恒久的な保存/検証境界は適切な公開設計文書。probe固有の記録は削除
- Phase A snapshot: A3採用版をcommit/hashで固定する。B result、C evidence、C' blind bundleは各Phaseで生成する

## 1. A0 — 目的、現況、対象外

目的は、R2を選定済みと扱う前に、既設の非公開`osm-artifacts`がローカルWindows Agentによる非公開・同一byteの受け渡しと、通常writerからのserver側保護という**必要条件**を満たすか実測すること。programのr4案と一緒にA3で採否を確認する。r4が不採用ならこのHANDOFFは凍結しない。

段1で`credentials set/status/remove`、DPAPI CurrentUser保管、原子的なローカル置換を実装済み。2026-09-27にownerが実R2 S3鍵をmasked登録し、`status`で現行profileを確認した。store recordのendpointは未設定で、非秘密のaccount固有endpointを限定probeの引数に渡す。A3前のsynthetic PUT/別process GET/照合/DELETEはowner端末で成功した。ownerはtokenが`osm-artifacts`のみにR2 Bucket Item Read/Writeを持ち、Public Development URL無効、Custom Domainsなしと画面で確認した。署名無しGET、Bucket Lockの実効性、Cloud到達性は未確認。GitHub Releaseのsynthetic probeはlocal/Cursor Cloudで成功したが非公開Evidenceの検証ではなく、Codex Cloudは当時のegress 403で失敗した。

対象外は実Evidence・実Buildのupload、一般向け`publish/fetch`とsafe extraction、鍵rotation/retired管理、Cloudの無人grant・read/write・画像閲覧、複数providerの登録/切替、Unity/BuildSystem変更、継続運用用retention決定である。Cloudflareのlock管理権限をAgentへ渡さない。このsliceの結果だけでR2採用確定や実payload解禁を宣言しない。

このsliceが答える問い: **所有Windowsユーザーの二つの独立processが、既存DPAPI profileを用いてprivate R2上の小さなsynthetic objectを同一hashで往復し、同じ通常writerの上書き/削除が限定prefixのBucket Lockで拒否されるか。**

## 2. 進める最低条件と判定

1. ownerがaccount固有S3 endpointと`osm-artifacts`だけに限定したObject Read & Write tokenを、自身の対話端末から既存CLIへmasked登録する。token/secret/API管理鍵をchat、引数、env、Git、Evidenceに渡さない。既存profileが有る、破損、ACL不安全、登録が失敗した場合は内容を推測・上書きせず止める。ownerはbucketのprivate設定を確認する。endpointは非秘密の`-Endpoint`引数として各probe processに渡し、HTTPSのR2 account hostnameに限定する。storeのv1 schema、既存credentialsコマンド、環境変数は変更しない。
2. Agentが同じWindowsユーザーで一意な`probe/unlocked/<run-id>/` keyに固定synthetic bytesをPUTする。生成時に転送物から独立して保持したSHA-256を記録し、**別のpwsh process**が同じprofileでGETしてbyte数、hash、markerを一致させる。正常なunsigned GETが同じ存在keyの内容を返さない。認証GET成功を先に確認し、DNS/TLS/proxy失敗、redirect、timeout、404を非公開性の成功と数えない。保護無しkeyは同じwriterが異なるbyteで同じkeyを上書きし、GETでhash変化を確認してからDELETEできることを確かめる。
3. ownerはaccount設定権限で`probe/locked/`に限る有限のBucket Lock ruleを作り、prefix/保持期間/lifecycleとの関係を非秘密で記録する。試験後の残存synthetic objectとruleの後片付け時期も記録する。writer tokenにはbucket設定権限がないことをownerの権限設定で確認する。同じwriterがrule適用後にそのprefixへ**新規synthetic object**をPUTし、GET/hash一致を確認してから、異なるbyteで同一keyの上書きと削除を試み、双方拒否を観測する。再GETで原hashを確認する。拒否原因を単なる接続不能・認証失敗・writer権限不足と混同しない。
4. 実鍵、署名URL、Authorization header、HTTP body/header、SDK例外本文を通常出力・ログ・Git・子process引数に残さない。dummy sentinelによる正常/失敗/timeout/cancelの漏洩検査を行う。通信診断は既定で無効にし、実通信の結果はallowlistで構成した非秘密のkey、期待/観測hash、byte数、HTTP status分類、lock ruleの非秘密設定、実行UTC、cleanup状態、base/headだけを出す。結果schema外の出力は証拠への保存を拒否する。checkoutの意図しない変更がないことを前後で確認する。

GOは1〜4の実測が揃い、通常writerとaccount設定者の権限分離も確認したときだけ。実R2未実行やowner設定未完了はGOにしない。試験結果は`pass`、`provider-capability-failure`、`environment-blocked`、`inconclusive`に分類する。開始条件と陽性対照が成立した後にprivacyまたはlockの必要能力が再現性をもって失敗した場合だけ、spikeの否定結果として`CONDITIONAL ACCEPT`で閉じ、R2を次段に進めずprogramの新Phase Aで経路を再選定する。token/endpoint/egress/lock ruleの準備不足は後二者として未実施条件と再試行責任者を残し、sliceも完了しない。このspikeは未実施のまま期限切れで完了扱いにしない。GOまたは上記の否定結果が得られたら終了し、CLI/Cloud/Buildまで範囲を伸ばさない。

ここでは答えない問いと所有先: 反復利用できる`publish/fetch`、安全なpackage/extraction、信頼済みledgerはprogram r4の「最小のArtifact CLI」。実EvidenceとC/C'のログ/画像閲覧は「Evidence first use」。Cursor/Codex Cloudごとの無人grantと能力は「Cloud」。大きいBuildとmultipart、Unity以外のbackend実証は「Build」。実鍵rotation・失効/復旧は「最小のArtifact CLI」までに行い、このprobe成功だけで長期運用を開始しない。

## 3. A1 — 責務と実装境界

- `tools/Artifacts/Credentials/CredentialStore.psm1`（現在239物理行、+50〜80見込み）: profileのDPAPI/ACL検査と1操作中の秘密使用だけを所有。既存moduleの**CLIではない内部向けexport**を一つ追加し、同期したtrusted callbackへ復号済みAccess Key ID/Secret Keyを同一process内で渡す。probe processごとに一度だけ呼び、child process/env/引数へ秘密を渡さない。callbackの結果は許可した非秘密schemaだけに絞り、例外/SDK response/資格情報objectを戻り値やstdoutへ通さない。失敗時は固定の非秘密errorに分類し、参照を操作終了時に解放する。PowerShell module間で呼ぶにはexportが必要なので「非export」を安全境界とはしない。storeはR2、lock、hashを知らない。dummy sentinelで正常/失敗時の漏洩とファイル不変を確認する。同一Windowsユーザーからの隔離は主張しない。+50%未満、500行未満でも新しい秘密使用面として構造レビューする。
- `tools/Artifacts/Probe/RouteProof.ps1`（新規、約120〜170行）: 非秘密の`-Endpoint`、固定profile、mode/key/hashを検証し、fixtureと事前hash、別processのwrite/readの起動、陽性対照/lock結果の判定、非秘密resultを所有する。子processにはendpoint、key、期待hashなど非秘密引数だけを渡す。S3 SDKや資格情報を知らず、fake transportで判定を単体テストする。
- `tools/Artifacts/Probe/R2RouteTransport.psm1`と同階層の小さな.NET project（新規、合計約180〜250行）: profile callback内のS3 client/HTTP request、PUT/GET/DELETE、署名無しrequest、stream/timeout/応答破棄、分類済み非秘密resultを所有する。bucket設定変更やprocess起動はしない。SDK型をprobe外へ公開せず、通信自体は実R2のsynthetic integrationで確認する。PowerShellと.NETの分担は同一process呼出しと秘密非記録を保つ範囲でBが最小化できるが、新たなowner/依存/public APIが必要ならAへ戻す。
- `tools/Artifacts/tests/`（既存Credentials.Tests.ps1は390行、+40〜60、新規probe試験は約100〜160見込み）: 既存dummy/ACL回帰とcallbackの漏洩・失敗保全を確認。R2の成功をfake成功で代替しない。既存テストが500行に達しそうなら、新しいprobeの試験を別ファイルに置く。
- `tools/Artifacts/README.md`（現在約44行、+20〜40見込み）: 診断の一時的な使い方、owner設定、実装済み/未検証、残存locked objectの扱いを記載。成功を一般CLIの完成と表現しない。

このsliceのコードは`tools/Artifacts`内に限定し、Unity asmdef/API、`tools/DebugStudio`、BuildSystemを変更しない。CredentialStoreは鍵の寿命、診断はnetwork/判定、ownerはaccount設定を持つ。probeは後のCLIへそのまま昇格する前提にせず、必要な境界だけを次のPhase Aで選び直す。計画外の状態、永続schema、公開API、owner/依存の変更が必要ならBで独断せずAを再開する。

## 4. 実装・検証経路

Phase Bは既存storeからの限定的な同一process利用、限定R2診断、offlineのdummy/fake試験、READMEと`pwsh tools/contract-audit.ps1`を担当する。SDKは依存を固定し、秘密を扱う前にrestore/buildと別processからの型loadを確認する。Phase Bは実R2の合否を宣言しない。

Phase Cの差し戻し中は`Credentials.Tests.ps1`の新callback caseと新probe試験の失敗caseを起点に選ぶ。判定Cではそれぞれ空filterのoffline suite、対象限定の実端末masked入力、実R2のwrite/read/unsigned/unlocked delete/locked overwrite+delete、raw結果と原hash、`contract-audit`、`docs-audit`、固定diffの構造レビューを必須とする。実装変更はUnity側に一切及ばず、全EditMode回帰は適用除外とし、PowerShell/.NET offline回帰と実R2で代替する。Unityコード・packageへ変更が必要ならAへ戻す。

実行経路: ownerがCloudflare dashboardで非秘密endpointを取得しbucket限定tokenを発行、対話端末でmasked登録する。lockはownerが同dashboardで`probe/locked/`限定・短期のruleを設定し、通常writerとは異なる管理権限で保有する。Agentは同じWindowsユーザーの二つのpwsh processから限定診断を実行する。Cloudflare設定の証拠はtoken値を含まないownerの設定記録、通信証拠はallowlistの非秘密resultと保存前後のrepo statusとする。実鍵使用時のSDK/HTTP raw output、request/response、例外本文は保存しない。Cはcheckout/同期領域外の限定ACL evidence bundleに固定base/headとraw offline test結果、sanitized route result、固定diffを収録し、hash/場所/保持期限をHANDOFFに記す。Cの所見は別記録にする。C' blind bundleはA3 snapshot、所見のないB result、同じ固定diff、sanitized結果、C前の機械検査だけから別に生成し、path/hash/生成UTCを記録する。C'は別セッションでbundleを読み、Cの所見を受け取らない。lock済みobjectは期限前に削除せず、key、rule、保持期限、清掃予定とownerを非秘密台帳へ残す。spike終了時にtokenを残す場合は次sliceでの用途と失効担当をownerが明記し、経路を不採用にする場合はownerがremote失効を確認した後にlocal profileを削除する。local削除だけを失効と呼ばない。

A3前の疎通: `pwsh`、.NET 8でのAWSSDK.S3 restore/buildと別pwshでのload、非秘密JSONの別process読取は2026-09-27の前案で確認した。ownerが実鍵をmasked登録した際、既存`Artifacts`ディレクトリの所有者不一致で初回登録が失敗した。空ディレクトリをowner端末で非再帰削除し、新規ACLへ実行ユーザーを明示的に設定する修正後、登録と`status`が成功した。owner端末の限定CLIで`probe/prea3/9ace5edb614d4203a6d71d6a8069e498.txt`の48 byteをPUTし、別`pwsh` processが認証GETしてSHA-256 `13cbdab75bf6856a24088bf26d230a2cd58ad3d6f1faacc86615bdb949c4e255`とmarkerを照合、同じwriterでDELETEした。ownerが提示したsanitized出力は`roundtrip: pass; ...; cleanup=removed`であり、別の読み取り専用CLIで`probe/prea3 empty: true`を確認した。初回probe実装にあった非IDisposable応答へのDispose呼出しによる失敗表示は修正済み。これらはowner端末から提示された出力であり、Agentが実鍵を直接扱った記録ではない。

ownerはCloudflare画面でtokenのR2 Bucket Item Read/Writeが`osm-artifacts`だけを対象とし、Public Development URLが無効、Custom Domainsが空であることを確認した。A3前の認証付き経路は成立したが、署名無しGET、lock設定と通常writerへの強制力、Cloud到達性は未確認。Cでの本検証は§2のまま。DashboardのSettings→Bucket lock rulesでprefixを`probe/locked/`に限定し有限の保持期間を設定できることはCloudflare公式文書で確認した。owner権限と証拠受入方法はsliceのA3で固定する。

## 5. A2 / A3 と後続Phase

- A0/A1: 本sessionのCodex（実際のmodel variantは未確認）。目的からの独立A0代替案は、R2を第一候補としつつSDK Adapter、server検証付きrotation、複数provider抽象を最初の条件にしない点で一致。レビュー担当の実モデル割当は未確認。
- A2: 同一初稿SHA-256`9A85555C5FA37F24DE9DDADB66C94B195BF5313C0F19E18D07E08DA1E707D329`を、アーキテクチャと失敗/実行可能性の独立した担当がレビューした。両者のendpoint受渡し・callback・診断責務・raw実鍵出力の非保存・陽性対照・否定結果の終端・C' bundle分離の指摘を採用して§2〜4へ反映。programとsliceの別branch、A3前の実経路疎通の指摘も採用し、metadataとA3待機条件を修正した。モデル指定と実割当variantの一致は確認できないため多様性は未証明。未採用は「callbackをmoduleから非exportにする」案で、別moduleから呼ぶPowerShell関数はexportが必要なため。CLIに秘密取得コマンドは設けず、同一ユーザーが既にDPAPI復号可能な前提で、非記録の狭い内部APIとして扱う。
- A3: ownerは方向を了承し、凍結前にR2 tokenを所有者端末のCLIへ登録してCLIから実R2を利用できることを条件とした。この条件は§4の限定preflightで達成した。`credentials` CLIは引き続きローカル保管のみ、実通信はA3前の使い捨てprobe CLIであり、汎用Artifact CLIの完成ではない。bucket限定権限・private設定のowner確認とlock ruleの設定責任・証拠受入方法を固定するまで、sliceのA3凍結とPhase B開始は宣言しない。program r4の判定とは別に記録する。
- C'担当: Phase B/Cと異なるmodelの新規session、または人間。Phase Aに未関与の候補を残す。判定Cの固定base/head、raw結果とblind bundleを使う。
- Phase B/C/C'/D: 未到達。各snapshot、実行結果、未確認、担当/モデル、採否を到達時に記録する。
