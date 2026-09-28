# Artifact Storage Route Proof — Phase B 結果（da4e405）

## 固定対象

- Phase A 凍結 snapshot SHA-256: `5DDB850ECC9F05DFFE666CAE752BF6C7AA4F8A5620883CA5203FB47D3AD0353F`
- implementation base: `93d2a1c436361ef6ee702096a61087cde55319b4`
- implementation head: `da4e405a0c5019a2f0edd857e1a2c11b61432c34`
- 対象: `tools/Artifacts` のprobe・credential store・offline suite。Unity Runtime/Editor、Scene、Prefab、Addressables、BuildSystemには変更なし。

## Phase Bで確定した変更

- 認証付きS3通信は、SDKのredirect pipelineへ3xx応答を渡す前にHTTP層で遮断する。Location先へ署名付き要求を送らず、固定の`redirect`分類を返す。SDKの自動redirectとエラー再試行を無効にした。
- credential v1は`Endpoint=null`を維持する。通信先endpointは検証済みprobe引数から別に渡し、profile、bucket、generationを通信前に検査する。profile不在・不整合・読取不能なら通信を開始せず、keyを含めない`environment-blocked`結果を返す。
- endpoint、RunId、key、hash、revision、Generation、S3 Codeの検査を文字列全体一致に統一した。主ループでも親が生成したkeyを通信・記録前に再検査する。
- unsigned body readはstreamへ渡すbuffer長を上限＋1 byte以内にし、実際の消費量と`ByteCount`が一致する。上限到達後のprefix hashは保持し、EOF未確認の全体hashは作らない。
- child credential callbackは`GetNewClosure()`がscript-local functionを引けない問題を避けるため、budget計算関数とtransport invokerのscriptblockをclosureへ明示的に束縛する。offline testはproduction callback factoryをdummy資格情報とstub transportで実行し、通常経路の残り期限伝達と期限切れ時のtransport非呼出しを検査する。
- offline suiteはSDK設定とredirect guardの接続、307応答の拒否、body上限でのstream実読量、改行付き入力の拒否、credential v1のnull endpoint受理、preflight停止時のkey非出力を検査する。
- 変更箇所には日本語コメントを追加し、通信境界、検証順、本文上限、secret-safeな結果化の理由を説明する。

## 同一headでの検証

| 検証 | 結果 |
| --- | --- |
| `dotnet build tools/Artifacts/Probe/R2RouteTransport.csproj -c Release -o tools/Artifacts/Probe/artifacts/route-transport --no-restore` | 成功、警告0、エラー0 |
| `pwsh -NoProfile -File tools/Artifacts/Tests/R2RouteTransport.Tests.ps1` | 成功、7件 |
| `pwsh -NoProfile -File tools/Artifacts/Tests/RouteProof.Tests.ps1` | 成功、24件 |
| `pwsh -NoProfile -File tools/Artifacts/Tests/Credentials.Tests.ps1` | ACL操作可能な実行権限で成功、22件 |
| `pwsh tools/contract-audit.ps1` | 成功 |
| `pwsh tools/docs-audit.ps1` | 成功 |
| `git diff --check 93d2a1c436361ef6ee702096a61087cde55319b4 da4e405a0c5019a2f0edd857e1a2c11b61432c34 -- tools/Artifacts` | 成功 |

Unity側に変更がないため、凍結A3の対象外規則に従いEditMode/Buildは検証対象外。全EditMode実行scriptを過去に誤起動したが、Unityテスト開始前に中断した。Unity processは終了しており、Unity資産に差分はない。生成されたUnityLockfileとログは削除していない。

## ソースと実行DLLのSHA-256

| ファイル | SHA-256 |
| --- | --- |
| `tools/Artifacts/Probe/RouteProof.ps1` | `C72768E06E690083D4877B8FF8BB7685A2C468E922822CA7030F11712E25E5AB` |
| `tools/Artifacts/Probe/R2RouteTransport.cs` | `F9089FA32439FAFDEBD74F225328E575907E37EA028BA6926CB3629E75B08C8D` |
| `tools/Artifacts/Probe/R2RouteTransport.psm1` | `F61006E34C55FF26D77EF29A5A494B277ECC41336D4D9560218E0F4DBD9F8F9B` |
| `tools/Artifacts/Probe/R2RouteTransport.csproj` | `E075E59D0659F3D7EB2549224D7D79C5BD79C937FBCE8160C61360BBFB3C3713` |
| `tools/Artifacts/Credentials/CredentialStore.psm1` | `BFC439187FCDCDC79090F5379AA58F7CB63465C2175AF66E4E681825EB6DF694` |
| `tools/Artifacts/Credentials/CredentialPathAcl.psm1` | `B0A975A3EAB1EB371859A1E22D8ED191EFC638DAF996C0B9E9657A91D6B1BA2B` |
| `tools/Artifacts/tests/RouteProof.Tests.ps1` | `5F74ABAFC12A38FA639E6E4A801307DC622DF1837F5706898C575EA9F2AEFC37` |
| `tools/Artifacts/tests/R2RouteTransport.Tests.ps1` | `C5E858A9F372FDE703154821F5F012B1F4F97C888B762A19E399F51B1146B2FC` |
| `tools/Artifacts/tests/Credentials.Tests.ps1` | `9E00307C95CA0D22276C7D96759730918C5420BE03288A9716E1A7492B3C1F27` |
| 実行DLL `tools/Artifacts/Probe/artifacts/route-transport/R2RouteTransport.dll` | `274F47FA6835371ADEB02DBC48286200B5931CA97011BFC3E2298BBB702CC1CD` |

## 判定境界

このPhase B結果は実R2のGO判定ではない。実endpoint、実通信一次観測、およびownerによるlock rule設定前後の独立記録は含まない。実R2要求は送っていない。
