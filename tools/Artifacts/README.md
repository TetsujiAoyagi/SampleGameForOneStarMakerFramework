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

`PreA3RoundTrip.ps1`は設計凍結前に実経路を確かめるための使い捨てprobeです。`probe/prea3/`の小さなsynthetic objectだけをPUTし、別`pwsh` processで認証GET・SHA-256照合してからDELETEします。実EvidenceやBuildは扱えず、通常のpublish/fetchやBucket Lockの判定にも使いません。SDK例外、鍵、署名付きURLを通常出力しません。実行前に固定バージョンのSDK依存をビルドします。

```powershell
dotnet build tools/Artifacts/Probe/Probe.Dependencies.csproj -c Release -o tools/Artifacts/Probe/artifacts/sdk -p:BaseIntermediateOutputPath=artifacts/obj/ -p:MSBuildProjectExtensionsPath=artifacts/obj/
pwsh -NoProfile -File tools/Artifacts/Probe/PreA3RoundTrip.ps1 -Endpoint https://<account-id>.r2.cloudflarestorage.com
pwsh -NoProfile -File tools/Artifacts/Probe/PreA3RoundTrip.ps1 -Endpoint https://<account-id>.r2.cloudflarestorage.com -Mode empty
```

endpointだけが非秘密の引数です。登録済みのDPAPI profileを同一processで復号して使い、鍵は子processの引数・環境変数へ渡しません。成功時のkey/hash/byte数と清掃結果は非秘密のprobe記録です。失敗時に`cleanup=unconfirmed`なら、対象prefixの残存を確認するまで清掃済みと扱いません。2026-09-27の所有者端末では往復と空prefixを確認しました。ownerはCloudflare画面でtokenの対象が`osm-artifacts`のみ、Public Development URL無効、Custom Domainsなしと確認しました。署名無し取得拒否とBucket Lockの実効性はRoute proofで別に実測します。

## 保守と検証

依存は `tools/artifacts.ps1` → `CredentialCommands.psm1` → `CredentialStore.psm1` → `CredentialPathAcl.psm1` の一方向です。launcherはCLI解析と終了コード、Commandsは入力と安全な表示、Storeはレコード検証・DPAPI・原子的置換・回復、PathAclは保存先と権限検査を担当します。公開コマンドで秘密を読み出す機能はありません。テストの保存先・障害注入はモジュール内部に限定します。

WindowsのPowerShell 7で次を実行します。資格情報テストは毎回生成するダミー値と隔離した保存先を使い、UnityやR2への接続は不要です。

```powershell
pwsh -NoProfile -File tools/Artifacts/tests/Credentials.Tests.ps1
pwsh -NoProfile -File tools/contract-audit.ps1
pwsh -NoProfile -File tools/docs-audit.ps1
```

DPAPIとACLの検証は所有Windowsユーザーの実行環境で行います。別のsandboxユーザーの失敗や成功を、所有ユーザーでの検証に読み替えません。入力判定を変更した場合は、上記の文字列回帰に加え、実PowerShellの解析とConsole付き非対話起動を照合し、入力前の有限時間での拒否と保存物の不変を確認します。通常のmasked入力も確認し、リダイレクト下のテストだけで対話入力まで検証済みと扱わないでください。
