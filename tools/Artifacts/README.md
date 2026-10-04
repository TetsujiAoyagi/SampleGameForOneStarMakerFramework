# ローカル資格情報管理

Windowsの所有ユーザーが、PowerShell 7から固定プロファイル「osm」の資格情報とsynthetic artifactを扱うツールです。2026-09-27に所有者端末で実R2鍵を登録し、後述の限定probeでsynthetic objectのR2往復を確認しました。`publish` / `fetch` / `rotate` / `confirm-revocation` の実R2運用は未確認です。

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

`credentials rotate` は新しい鍵を対話端末でmasked入力し、使い捨てsynthetic keyへのPUT/GET/hash/DELETE/不存在を検証してから、新世代をactive、旧世代をretiredに原子的に切り替えます。切替え後は失効待ちでexit 2を返します。所有者が管理画面で対象の旧tokenだけを失効した後、非秘密の観察記録を使って`confirm-revocation`を実行します。旧鍵の401/403と安全な拒否code、新鍵の前後の陽性対照が揃って初めてretiredを清掃します。pending中は通常のset/removeと次のrotateを拒否します。失効前のcrashではpendingを保ち、壊れたactiveをbackupから自動復元しません。旧鍵の失効や、新鍵を使えない場合の再発行・masked再登録は所有者が管理画面と本CLIで明示的に行います。ローカルのstatusやset成功をR2接続成功とは扱いません。

## 最小Artifact CLI（synthetic限定）

```powershell
dotnet build tools/Artifacts/Packaging/ArtifactPackaging.csproj -c Release -o tools/Artifacts/Packaging/artifacts/package
dotnet build tools/Artifacts/Transport/R2ArtifactTransport.csproj -c Release -o tools/Artifacts/Transport/artifacts/transport

pwsh tools/artifacts.ps1 publish --profile osm --config <absolute-config.json> --input-list <absolute-input-list.json> --base <40hex> --head <40hex>
pwsh tools/artifacts.ps1 fetch --profile osm --config <absolute-config.json> --reference <opaque> --sha256 <64hex>
pwsh tools/artifacts.ps1 credentials rotate --profile osm --config <absolute-config.json>
pwsh tools/artifacts.ps1 credentials confirm-revocation --profile osm --config <absolute-config.json> --generation <retired32hex> --evidence <absolute-revocation.json>
```

`input-list` は `{"schemaVersion":1,"purpose":"synthetic","root":"<absolute>","files":["relative/file.txt"]}` です。1〜4096件のファイルだけを明示選択し、再帰収集しません。`purpose` はcallerの宣言であり、秘密検査ではありません。任意のログや実Evidenceをこの段階のCLIへ載せないでください。sourceをread lockで隔離snapshotし、その同じbytesからmanifest・ZIP・送信hashを作ります。相対path逸脱、reparse、秘密領域、重複、case衝突、上限違反を拒否します。

`config` は `schemaVersion=1`, `profile="osm"`, `endpoint="https://<32hex>.r2.cloudflarestorage.com"`, `bucket="osm-artifacts"`, `repositoryId=<64hex>`, `prefix="probe/locked/"`, `retentionSeconds=86400`, `observedAt`, `validUntil`, `settingsEvidencePath`, `settingsEvidenceSha256` の厳密なJSONです。`observedAt` と `validUntil` はUTC round-tripで、24時間以内の有効区間に現在時刻が含まれる必要があります。設定原記録は、同じendpoint/bucket/prefix/retentionと、公開development URL無効、custom domain 0、writerに設定権限なし、bucket scopeのwriter、lock有効、age rule 1・date/indefinite rule 0、lifecycle compatibleを記録します。未確認の値は成功にしません。設定はowner/AIの観察であり、admin APIによる常時保証ではありません。

`publish` は`probe/locked/<repositoryId>/<head>/<runId>/bundle.zip`にだけ保存します。別`pwsh` processでの認証GETと全byte/hash、同じsynthetic packageへの異なるbytes PUTとDELETEのlock拒否、再GETでの原byte/hash、`probe/unlocked/<runId>/control.txt`のwriter陽性対照を満たした場合だけledger候補を返します。結果・保護receipt・`operation-observations.json`はWindows Known Folder LocalApplicationDataの`OneStarMaker/Artifacts/transfers/<operation-id>/`に保存し、Gitへ自動追加しません。原観測は途中失敗でも保存し、status/code/byte/hash/世代と固定identityを含みます。lock拒否を検査したobjectは保持し、清掃目的のDELETEは行いません。失敗時のkeyとlocal pathは非秘密のresidueに残し、未確認を成功へ変更しません。1操作は最大10分、通信一回は最大120秒で、SDK retryとredirectを無効にします。

`fetch` はconfigとopaque referenceのprofile/keyを照合し、**callerが別に渡した**期待package SHA-256とdownload全体を照合します。さらにmanifestと全entryのhash/path/byteを検証してから、新規private operation配下の`ready`へ切り替えます。既存stagingへ展開せず、取得物を実行しません。ZIPは最大256 MiB、JSONは1 MiB、entryは4096、単一展開は256 MiB、総展開は1 GiB、圧縮比は100までです。引数や出力に資格情報を含めません。

転送のexitは0が検証完了、1が失敗・未確認です。rotationの切替え後/失効待ちは2であり、失効確認まで完了扱いしません。`confirm-revocation`成功は0、清掃保留は2、失敗・未確認は1です。既存credentialsのexit 0/1/2の意味は維持します。新commandの結果v1は`schemaVersion/operationId/operation/status/reasonCode/verification/ledger/outputPath/residue`を持つJSONです。失敗時のledgerとoutputPathはnullで、例外本文、SDK応答本文、秘密は表示しません。referenceは上位へopaqueな文字列として渡し、そこから期待package hashを補いません。

同じ実物にlockの破壊試行を行うため、現在の経路はsynthetic-onlyです。実Evidence用prefix、Cloud/Build連携、H2d/H3は未対応です。R2の管理者が途中でpolicyを変更する脅威までは保証しません。実R2のpublish/fetch、設定前後の原記録、token切替・失効の成立は判定Cで別に観察します。

## A3前の限定R2疎通確認

2026-09-27に使い捨てprobeで所有者端末のsynthetic PUT、別`pwsh` processの認証GET・SHA-256照合、DELETEと空prefixを確認しました。このprobeは通信本文・子process・cleanupの有限期限を備えていないため退役し、再実行用スクリプトと専用SDK projectを削除しました。この事前記録単独では署名無し取得拒否やBucket Lockの実効性を証明しません。後述のRoute proofで別に実測しました。

## Route proof（限定slice GO）

`Probe/RouteProof.ps1` はこのslice専用の限定診断です。親processはendpointとレビュー対象の`-ImplementationBase` / `-ImplementationHead`（40桁小文字hex commit ID）を受け取り、観測へ固定値を記録します。作業ツリーのHEADやdevelopとのmerge-baseを実行時に推測しません。`probe/unlocked/<run-id>/` と `probe/locked/<run-id>/` の各1 key、1操作1子`pwsh` process、各操作30秒・子process45秒・run全体5分の期限を使います。各childはrun-id/keyとrevisionに加え、PUT fixtureの長さ・hash・run marker・original/changed識別を検証し、PUT以外のpayloadを拒否します。子processの引数に鍵を渡さず、`CredentialStore` の `Invoke-CredentialTransport` が同一process内の一回のcallbackへDPAPI復号値を限定して渡します。callbackから戻るのは閉じた非秘密transport観測だけです。親processは子と同じ単一JSON行をschema検証し、stdoutとstderrは子45秒の共有残予算で順に回収します。期限超過後はprocess終了と両pipeのEOFを確認できるまで回復cleanupを送りません。offline testsは同じ12操作主ループとJSON境界を通して成功完走・期限・結果照合・cleanup分類を検査します。

transportは`Probe/R2RouteTransport.csproj`の固定`AWSSDK.S3`依存を使います。認証PUT/GET/DELETEと、Authorizationおよび署名queryを付けないHTTP GETを分離し、本文は保存せず、EOF確認時だけ全体hash、期待長に達した場合だけ先頭hashを返します。8193 byte目に達しても、それ以前に得た期待長prefix hashは残し、EOF未確認の全体hashは作りません。期限中の取消しやEOF前のI/O切断でも、到着済みprefix hashを保ち、全体hashとEOF確認は未確定にします。unsigned応答のS3 `Code`要素は短い安全なcode値だけを逐次抽出し、Messageや本文全体は保持しません。閉じた非秘密観測にunsigned requestのmethod、正規URI、Authorization/署名query/対象queryの有無を加え、RouteProofが意図したGETか照合します。401/`unauthorized`/`Unauthorized`、403/`forbidden`/`AccessDenied`、またはR2で観測した400/`other`/`InvalidArgument`という同一応答内の組を拒否証拠として許可し、交差したstatus/codeは許可しません。400は実際のstatusのまま記録し、正規GET・EOF・非露出hashの条件も同じく要求します。一度のprefix一致は内容露出として記録しますが、再現条件を満たすまではprovider capability failureと確定しません。Bucket Lock拒否は403/`forbidden`/`ObjectLockedByBucketPolicy`または実測した409/`other`/`ObjectLockedByBucketPolicy`の同一応答tupleだけを許可し、EOF・期限・redirect・本文上限と同一writerの条件を維持します。上書きとDELETEの双方の拒否後、最終の認証GETで元のbyte数・hashが一致して初めてprobeは完走します。locked overwrite/deleteの成功応答だけでは保持機能の失敗へ昇格せず、状態確認できない場合は`inconclusive`です。期限後に子processの停止を確認できなければ、競合する回復DELETEを送らずcleanupを未確認にします。SDK例外のMessage、HTTP本文、request/header、秘密は結果へ通しません。`artifacts/` は生成物でGit管理外です。

実行前にCloudflareで`probe/locked/`の全有効ruleを確認し、次の10 fieldの秘密を含まない事前JSONを用意します。`BeforeHash`は実行前の原記録のSHA-256です。事後値は入力せず、probe出力の`lockRule.afterHash`は`null`です。実際のAfter設定は実行後に独立取得した原記録で照合します。通常writer tokenへBucket設定権限を追加しないでください。

```powershell
dotnet restore tools/Artifacts/Probe/R2RouteTransport.csproj
dotnet build tools/Artifacts/Probe/R2RouteTransport.csproj -c Release -o tools/Artifacts/Probe/artifacts/route-transport --no-restore
pwsh -NoProfile -File tools/Artifacts/Probe/RouteProof.ps1 `
  -Endpoint https://<32-hex-account-id>.r2.cloudflarestorage.com `
  -ImplementationBase <40-hex-base-commit> `
  -ImplementationHead <40-hex-implementation-head-commit> `
  -LockRuleJson '{"Prefix":"probe/locked/","Enabled":true,"Kind":"Age","RetentionSeconds":86400,"RuleCount":1,"DateRules":0,"IndefiniteRules":0,"WriterCanConfigure":false,"LifecycleCompatible":true,"BeforeHash":"<64-hex>"}'
```

固定実装head `9c46b5837796de23465f6d9c059bb326c2f06858` の2026-09-29 JSTの1 runは、別processの認証GET、同じ存在keyの正規unsigned GET、unlockedの上書き・削除・NoSuchKey、lockedの上書き・DELETE拒否、最後の原57 byte/hash GETまで12操作を完走しました。unsigned GETは400/`other`/`InvalidArgument`とEOF・非露出hashの組であり、object byteを返さなかったという限定観測です。400の原因や認証拒否という因果は未特定です。locked上書きとDELETEはいずれも409/`other`/`ObjectLockedByBucketPolicy`で、最終GETは原hashに一致しました。前後のprivate設定、全有効rule、writer権限、lifecycleは同一で、別モデルの独立監査もblockerなしでした。これはRoute proof sliceのGOであり、汎用Artifact CLI、実Evidence/Buildのupload、Cloud経路、R2の全面採用は後続の別判定です。

SDK例外から作る認証操作の観測は、statusと安全なS3 Codeを返しますが、例外応答本文のEOF・上限・取消しを独立に実測した証拠ではありません。今回の限定live観測を異常系全般の保証に広げないでください。実行時の出力は固定schemaの非秘密JSON Linesだけを保存し、`provider-capability-failure` は開始条件・陽性対照・再現性が揃った場合だけ意味を持ちます。lock対象は保持期限前に削除せず、rule、保持期限、清掃予定を別の非秘密台帳へ残します。`-Endpoint` は親だけが指定し、bucket、path、query、userinfo、port、別hostnameは受け付けません。

## 保守と検証

既存credentialsの依存は `tools/artifacts.ps1` → `CredentialCommands.psm1` → `CredentialStore.psm1` → `CredentialPathAcl.psm1` / `CredentialRecord.psm1` の一方向です。新規転送はCLI→ArtifactCommands/ArtifactApplication→Packaging/Transport/Credentialsに分け、ArtifactPathsが別のprivate operation rootを管理します。StoreはDPAPI保存・原子的置換・世代競合、Recordはv1/v2 codec、Rotationはserver検証の順序を担当します。公開コマンドで秘密を読み出す機能はありません。テストの保存先・障害注入はモジュール内部に限定します。

WindowsのPowerShell 7で次を実行します。資格情報テストは毎回生成するダミー値と隔離した保存先を使い、UnityやR2への接続は不要です。

```powershell
pwsh -NoProfile -File tools/Artifacts/tests/Credentials.Tests.ps1
pwsh -NoProfile -File tools/Artifacts/tests/RouteProof.Tests.ps1
dotnet build tools/Artifacts/Probe/R2RouteTransport.csproj -c Release -o tools/Artifacts/Probe/artifacts/route-transport --no-restore
pwsh -NoProfile -File tools/Artifacts/tests/R2RouteTransport.Tests.ps1
dotnet build tools/Artifacts/Packaging/ArtifactPackaging.csproj -c Release -o tools/Artifacts/Packaging/artifacts/package
dotnet build tools/Artifacts/Transport/R2ArtifactTransport.csproj -c Release -o tools/Artifacts/Transport/artifacts/transport
pwsh -NoProfile -File tools/Artifacts/tests/ArtifactPackage.Tests.ps1
pwsh -NoProfile -File tools/Artifacts/tests/ArtifactTransfer.Tests.ps1
pwsh -NoProfile -File tools/Artifacts/tests/ArtifactRotation.Tests.ps1
pwsh -NoProfile -File tools/contract-audit.ps1
pwsh -NoProfile -File tools/docs-audit.ps1
```

DPAPIとACLの検証は所有Windowsユーザーの実行環境で行います。別のsandboxユーザーの失敗や成功を、所有ユーザーでの検証に読み替えません。入力判定を変更した場合は、上記の文字列回帰に加え、実PowerShellの解析とConsole付き非対話起動を照合し、入力前の有限時間での拒否と保存物の不変を確認します。通常のmasked入力も確認し、リダイレクト下のテストだけで対話入力まで検証済みと扱わないでください。
