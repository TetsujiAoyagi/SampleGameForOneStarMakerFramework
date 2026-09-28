# Artifact Storage Route Proof — Phase B 結果（703651e）

## 固定対象

- Phase A 凍結 snapshot: `artifacts/route-proof-phase-c-2b8aa21/blind-audit/snapshots/phase-a.md`
- Phase A snapshot SHA-256: `5DDB850ECC9F05DFFE666CAE752BF6C7AA4F8A5620883CA5203FB47D3AD0353F`
- implementation base: `93d2a1c436361ef6ee702096a61087cde55319b4`
- implementation head: `703651e30416a0fc429e9d9e98c828f3501e5cfc`
- 対象: `tools/Artifacts` のprobe・credential store・offline suite。Unity Runtime/Editor、Scene、Prefab、Addressables、BuildSystemには変更なし。

## Phase Bで確定した変更

- 認証付きS3通信はAWS SDKのredirect処理へ3xx応答を渡す前にHTTP層で遮断する。redirectを検出した場合は固定の`redirect`分類を返し、Location先へ署名付き要求を送らない。SDKの自動redirectとエラー再試行を無効にした。
- endpoint、RunId、key、hash、revision、Generation、S3 Codeの検査を文字列全体一致に統一した。主ループでも生成した2つのkeyを通信・記録より前に再検査する。
- 通信前に既存credential profileを復号して対象profile/bucket/endpoint/Generationを確認する。不在・不整合・読取不能の場合は通信を始めず、`environment-blocked`としてkeyを出さずに停止する。通常の通信失敗は既存のinconclusive経路に残る。
- offline suiteはredirect拒否handlerとSDK設定の接続、末尾改行を含む入力の拒否、environment-blocked時のkey非出力を検査する。
- 新規・更新したUnity側C#には該当なし。新しい日本語コメントは通信境界、入力検証、事前条件、観測保持の理由を説明する。

## 同一headでの検証

| 検証 | 結果 |
| --- | --- |
| `dotnet build tools/Artifacts/Probe/R2RouteTransport.csproj -c Release -o tools/Artifacts/Probe/artifacts/route-transport --no-restore` | 成功、警告0、エラー0 |
| `pwsh -NoProfile -File tools/Artifacts/tests/R2RouteTransport.Tests.ps1` | 成功、5件 |
| `pwsh -NoProfile -File tools/Artifacts/tests/RouteProof.Tests.ps1` | 成功、23件 |
| `pwsh -NoProfile -File tools/Artifacts/tests/Credentials.Tests.ps1` | ACL操作可能な実行権限で成功、22件 |
| `pwsh tools/contract-audit.ps1` | 成功 |
| `pwsh tools/docs-audit.ps1` | 成功 |
| `git diff --check 93d2a1c436361ef6ee702096a61087cde55319b4..703651e30416a0fc429e9d9e98c828f3501e5cfc -- tools/Artifacts` | 成功 |

Unity側に変更がないため、凍結A3の対象外規則に従いEditMode/Buildは検証対象外。誤って全EditMode実行scriptを開始したが、テスト開始前に中断した。Unity processは終了しており、repo内のUnity資産に差分はない。生成されたUnityLockfileとログは証拠の状態を保つため削除していない。

## 実行DLLとソースのSHA-256

| ファイル | SHA-256 |
| --- | --- |
| `tools/Artifacts/Probe/RouteProof.ps1` | `11CBB7E0AD5524945E5193D659716F05365E8B3084A3F8CB2479BAB628C111C2` |
| `tools/Artifacts/Probe/R2RouteTransport.cs` | `E30D097C6D6C387D45F67917837E17BAE3BF05110F4FD1D6746B72190EFD9158` |
| `tools/Artifacts/Probe/R2RouteTransport.psm1` | `F61006E34C55FF26D77EF29A5A494B277ECC41336D4D9560218E0F4DBD9F8F9B` |
| `tools/Artifacts/tests/RouteProof.Tests.ps1` | `DE3728FEC3D28D8A518F7F57A6828DB771F1B49E39CD697A082945A7D2292407` |
| `tools/Artifacts/tests/R2RouteTransport.Tests.ps1` | `10ACF92194448EA198A850CDBD94DDCBB4F72DAD7BFEFF745867565FE1B44EF6` |
| `tools/Artifacts/tests/Credentials.Tests.ps1` | `9E00307C95CA0D22276C7D96759730918C5420BE03288A9716E1A7492B3C1F27` |
| 実行DLL `tools/Artifacts/Probe/artifacts/route-transport/R2RouteTransport.dll` | `15376967E9431194583B48E0DCFB4F36F615959F002B1B616E7B7720316A787E` |

## 判定境界

このPhase B結果は実R2のGO判定ではない。実endpoint、実通信一次観測、およびownerによるlock rule設定前後の独立記録は含まない。credential profileの有無だけを確認する非通信preflightは行ったが、今回のPhase Bではsigned/unsigned R2要求を送っていない。
