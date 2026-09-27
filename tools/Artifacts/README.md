# ローカル資格情報管理

Windowsの所有ユーザーが、PowerShell 7から固定プロファイル「osm」の資格情報を管理するためのツールです。`credentials` CLIはローカル段1（programのスライス0）です。2026-09-27に所有者端末で実R2鍵を登録し、後述の限定probeでsynthetic objectのR2往復を確認しました。一般のartifact転送CLIは未実装です。

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

サーバーでの検証を伴う鍵の切替えと旧トークンの失効確認は後続作業です。このツールのローカル操作成功だけで、実R2鍵が有効とは判断しません。

## A3前の限定R2疎通確認

2026-09-27に使い捨てprobeで所有者端末のsynthetic PUT、別`pwsh` processの認証GET・SHA-256照合、DELETEと空prefixを確認しました。このprobeは通信本文・子process・cleanupの有限期限を備えていないため退役し、再実行用スクリプトと専用SDK projectを削除しました。この記録は署名無し取得拒否やBucket Lockの実効性を証明しません。これらは進行中のRoute proofで別に実測します。

## Route proof（Phase B実装、実R2未実行）

`Probe/RouteProof.ps1` はこのslice専用の限定診断です。親processはendpointとレビュー対象の`-ImplementationBase` / `-ImplementationHead`（40桁小文字hex commit ID）を受け取り、観測へ固定値を記録します。作業ツリーのHEADやdevelopとのmerge-baseを実行時に推測しません。`probe/unlocked/<run-id>/` と `probe/locked/<run-id>/` の各1 key、1操作1子`pwsh` process、各操作30秒・子process45秒・run全体5分の期限を使います。各childもrun-id/keyとrevisionの形式を検証します。子processの引数に鍵を渡さず、`CredentialStore` の `Invoke-CredentialTransport` が同一process内の一回のcallbackへDPAPI復号値を限定して渡します。callbackから戻るのは閉じた非秘密transport観測だけです。親processは子と同じ単一JSON行をschema検証し、offline testsは同じ12操作主ループとJSON境界を通して成功完走・期限・結果照合・cleanup分類を検査します。

transportは`Probe/R2RouteTransport.csproj`の固定`AWSSDK.S3`依存を使います。認証PUT/GET/DELETEと、Authorizationおよび署名queryを付けないHTTP GETを分離し、本文は保存せず、EOF確認時だけ全体hash、期待長に達した場合だけ先頭hashを返します。8193 byte目に達しても、それ以前に得た期待長prefix hashは残し、EOF未確認の全体hashは作りません。期限中にreadが中断されても、到着済みprefix hashを保ち、全体hashとEOF確認は未確定にします。unsigned応答のS3 `Code`要素は短い安全なcode値だけを逐次抽出し、Messageや本文全体は保持しません。閉じた非秘密観測にunsigned requestのmethod、正規URI、Authorization/署名query/対象queryの有無を加え、RouteProofが意図したGETか照合します。401/`unauthorized`/`Unauthorized`、または403/`forbidden`/`AccessDenied`という同一応答内の組だけを拒否証拠として許可し、交差したstatus/codeは許可しません。HTTP 400/`InvalidArgument`はprivate拒否と判定せず`inconclusive`です。一度のprefix一致は内容露出として記録しますが、再現条件を満たすまではprovider capability failureと確定しません。locked overwrite/deleteの成功応答だけでは保持機能の失敗へ昇格せず、状態確認できない場合は`inconclusive`です。期限後に子processの停止を確認できなければ、競合する回復DELETEを送らずcleanupを未確認にします。SDK例外のMessage、HTTP本文、request/header、秘密は結果へ通しません。`artifacts/` は生成物でGit管理外です。

所有者がCloudflareで`probe/locked/`の全有効ruleを確認し、次のような秘密を含まないJSONを手元で用意してから実R2を実行します。`BeforeHash` と `AfterHash` は設定全体の記録hashで、試験前後に同一であることを示します。通常writer tokenへBucket設定権限を追加しないでください。

```powershell
dotnet restore tools/Artifacts/Probe/R2RouteTransport.csproj
dotnet build tools/Artifacts/Probe/R2RouteTransport.csproj -c Release -o tools/Artifacts/Probe/artifacts/route-transport --no-restore
pwsh -NoProfile -File tools/Artifacts/Probe/RouteProof.ps1 `
  -Endpoint https://<32-hex-account-id>.r2.cloudflarestorage.com `
  -ImplementationBase <40-hex-base-commit> `
  -ImplementationHead <40-hex-implementation-head-commit> `
  -LockRuleJson '{"Prefix":"probe/locked/","Enabled":true,"Kind":"Age","RetentionSeconds":900,"RuleCount":1,"DateRules":0,"IndefiniteRules":0,"WriterCanConfigure":false,"LifecycleCompatible":true,"BeforeHash":"<64-hex>","AfterHash":"<64-hex>"}'
```

Phase Bでは上記を実R2へ向けて実行せず、合否も宣言しません。実行時の出力は固定schemaの非秘密JSON Linesだけを保存し、`provider-capability-failure` は開始条件・陽性対照・再現性が揃った場合だけ意味を持ちます。lock対象は保持期限前に削除せず、`retained-by-lock` としてowner、rule、保持期限、清掃予定を別の非秘密台帳へ残します。`-Endpoint` は親だけが指定し、bucket、path、query、userinfo、port、別hostnameは受け付けません。

## 保守と検証

依存は `tools/artifacts.ps1` → `CredentialCommands.psm1` → `CredentialStore.psm1` → `CredentialPathAcl.psm1` の一方向です。launcherはCLI解析と終了コード、Commandsは入力と安全な表示、Storeはレコード検証・DPAPI・原子的置換・回復、PathAclは保存先と権限検査を担当します。公開コマンドで秘密を読み出す機能はありません。テストの保存先・障害注入はモジュール内部に限定します。

WindowsのPowerShell 7で次を実行します。資格情報テストは毎回生成するダミー値と隔離した保存先を使い、UnityやR2への接続は不要です。

```powershell
pwsh -NoProfile -File tools/Artifacts/tests/Credentials.Tests.ps1
pwsh -NoProfile -File tools/Artifacts/tests/RouteProof.Tests.ps1
dotnet build tools/Artifacts/Probe/R2RouteTransport.csproj -c Release -o tools/Artifacts/Probe/artifacts/route-transport --no-restore
pwsh -NoProfile -File tools/Artifacts/tests/R2RouteTransport.Tests.ps1
pwsh -NoProfile -File tools/contract-audit.ps1
pwsh -NoProfile -File tools/docs-audit.ps1
```

DPAPIとACLの検証は所有Windowsユーザーの実行環境で行います。別のsandboxユーザーの失敗や成功を、所有ユーザーでの検証に読み替えません。入力判定を変更した場合は、上記の文字列回帰に加え、実PowerShellの解析とConsole付き非対話起動を照合し、入力前の有限時間での拒否と保存物の不変を確認します。通常のmasked入力も確認し、リダイレクト下のテストだけで対話入力まで検証済みと扱わないでください。
