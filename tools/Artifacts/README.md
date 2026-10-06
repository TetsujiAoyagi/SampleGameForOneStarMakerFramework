# ローカル資格情報管理

Windowsの所有ユーザーが、PowerShell 7から固定プロファイル「osm」の資格情報、synthetic artifact、固定E1/E2のEvidence first useを扱うツールです。所有者端末で実R2鍵の登録、syntheticな`publish` / 別sessionの`fetch` / `rotate` / `confirm-revocation`と、実Evidenceへの破壊試行をしない保護送信・独立ledger確定・別session取得とログ/原画像閲覧を検証済みです。Evidenceの対象は下記の固定2packageに限ります。任意Evidence、実Build転送、Cloud、別ホストへの配布は未対応です。

## エージェント操作の事前承認

所有者は、この節の範囲にあるArtifact Storage操作を事前承認する。使用するエージェント製品・モデル・セッションによらず適用し、通常のリポジトリ文書を正本とする。操作ごとに「外部通信する」「既存鍵を使う」という理由だけで所有者へ再確認を求めない。この承認は作業の許可であり、未対応機能やPhase Aの受け入れ境界を拡大せず、各製品のsandbox・自動審査・承認UIを解除する設定でもない。

### 都度確認なしで進める範囲

この事前承認の実Evidence保存対象は、下記の固定E1/E2の入力・取得物と、本READMEで定義する設定原観察・intent・receipt・result・ledger・取得案内・保持記録に限る。完了済みE1/E2の再送は含めず、新規file集合は対象と保存先を所有者が明示承認するまで範囲外とする。synthetic `publish`は非秘密の使い捨てfixtureだけに使い、任意のログ・実Evidence・実Buildを`purpose="synthetic"`の宣言や明示一覧だけで送信してはならない。

- 所有者の同一Windowsユーザー環境で、既存の`osm`プロファイルと検証済みCLIを使う。接続先は信頼済み台帳・取得案内・設定原観察で同一identityを確認した既存R2 endpoint、private bucket `osm-artifacts`に限る。ホスト名の形式が合うだけで、別accountを許可済みにしない。
- 承認済み作業の固定入力を、明示されたfile一覧・base/head・bytes/hash・保存目的・保持方針に従って新規private operationへ保存し、対応する小台帳と取得案内を確定する。既存証拠・台帳・案内・失敗記録を上書きしない。
- 承認済み選択範囲内で内容の非秘密性と盲検入力の分離を確認したEvidenceを、対応する保護送信経路で固有keyへ1回送信し、別processのGETとhash照合、事後設定観察、独立ledger確定まで行う。対象と保存先が承認済み範囲に収まる場合は、fileごとの追加送信承認を求めない。現実装の実Evidenceは下記E1/E2だけで、完了済みrunを承認設定の試験のため再送しない。
- 信頼済み案内・台帳から別に渡された期待hashで`fetch`し、新規private readyの全entry・版・hashを検証して、必要なログと原画像を閲覧する。観察期限後は同policy identityの設定を再観察し、新しいconfig・案内・期待hashを固定してよい。設定自体の変更と、取得したscript/DLLの実行は含まない。
- 上記送信に付随する、承認済みCLIの同run synthetic witnessとunlocked対照の限定PUT/GET/DELETEを行う。実Evidenceや既存locked witnessを破壊試行・清掃の対象にせず、intent境界後の同key再PUT、失敗原因未記録の新run、成功runの反復を行わない。

### 秘密の保護と個別承認の境界

既存鍵はCLIの資格情報storeとtransport内部だけで使用し、値をエージェントの会話、引数、環境変数、ログ、Git、証拠package、同期先へ取り出さない。暗号化レコードも配布せず、既存DPAPI/ACLを保持する。認証が失敗した場合は非秘密の結果を記録して止め、鍵の再入力やTokenDeleteを通常操作の前提にしない。

新規鍵の登録・置換・rotation・失効・削除、bucket/account/prefixや送信対象の承認範囲拡大、公開URL/domain・lock/lifecycle・権限・保持方針の変更、実Evidenceの上書き・削除・prune、別host/Cloud/実Buildへの展開は、この事前承認に含めない。必要な場合は対象・変更・影響を具体化して所有者の個別承認を受け、実装・検証が未対応なら先に該当Phase Aを行う。保持期限だけで削除を許可しない。

実行環境が承認を要求したら、その環境の承認経路を使用し、この節と対象入力・保存先・副作用・秘密非露出の根拠を示す。それでも人間の承認が残る場合は、その操作だけを待つ。拒否された操作を別コマンドや設定緩和で迂回せず、理由を報告して、影響しない作業を続ける。文書の事前承認を「すべての実行環境で承認画面が出ない」という保証として扱わない。

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

この`publish`経路は送信したobject自身にlockの破壊試行を行うため、synthetic-onlyです。実Evidenceには下記の独立した`evidence publish`経路を使います。Cloud/Build連携、H2d/H3は未対応です。R2の管理者が途中でpolicyを変更する脅威までは保証しません。

検証済み実装は `caed8bae55c033673b75532dc650f0a3a3cedc48` です。85件のoffline testと3 buildに加え、実R2で設定の前後一致、synthetic publish、別session fetchと全entryのhash照合・安全展開、locked objectの上書き/DELETE拒否、新鍵の陽性対照を挟んだ旧鍵の401/`unauthorized`/`Unauthorized`拒否とretired清掃を確認しました。候補検証・切替・owner失効は旧実装 `4e83b7e7a4116fcf45c996b110b89515211da4b4` での観測です。その経路のsource不変性と、Git revision metadataだけを揃えた旧runtimeの完全再現・通常の最終runtimeへの復元を照合して接続し、ownerのtoken削除は反復していません。同じ固定証拠を使ったCと独立blind C′はGOで、ownerのPhase D判断も完了しました。C′は別モデル・新規sessionですが、同じOpenAI/GPT系列という独立性の制約があります。この合格はsynthetic限定であり、実payload利用の承認へ広げません。

## Evidence first use（固定E1/E2・同一Windowsユーザー）

`evidence publish`は任意ログを送る汎用コマンドではありません。E1はPR #96のfindings-freeな既存固定Evidenceの選択済み13file、原source manifest、selection receiptの15entryです。source rootと各bytes/hash、evidence base=`fd7ebf932d230a522293dee72572cbdeeac1c8fa`、head=`caed8bae55c033673b75532dc650f0a3a3cedc48`をコードで固定しています。E2は保護設定の`settings-before.json`、`settings-overview.jpg`、`settings-rules.jpg`、`capture.json`の4entryです。原JPEGを変換せず、captureに2画像のbytes/hash・観察範囲・版を記録します。E2のevidence headとcapture/selectionのproducer headは実行時のcheckout HEADに一致する必要があります。review-recordやharvest後のHEADから旧E2を再送しないでください。

既存review-evidenceの内側bytesと外側manifest/ZIP形式を維持し、明示一覧だけをread lockでbounded snapshotします。同じsnapshotからpackageを一度作り、期待entry集合・版・全bytes/hashを照合します。credential/grant、scope外、transfers、reparse、上限外のsourceは拒否します。送信担当は選択したtext/JSON/原画像の内容を事前に確認し、秘密やC/A2所見を混ぜません。purpose宣言やhash一致だけで非秘密性を証明したとは扱いません。

```powershell
pwsh tools/artifacts.ps1 evidence publish --profile osm --config <absolute-config.json> --selection <absolute-selection.json> --base <evidence-base40> --head <evidence-head40>
pwsh tools/artifacts.ps1 evidence commit --candidate <absolute-result.json> --candidate-sha256 <trusted64> --settings-after <absolute-settings-after.json> --settings-after-sha256 <trusted64>
pwsh tools/artifacts.ps1 fetch --profile osm --config <fresh-absolute-config.json> --reference <trusted-opaque-reference> --sha256 <trusted-package64>
```

strict configはpurpose=`evidence`、bucket=`osm-artifacts`、prefix=`evidence/first-use/`、retentionSeconds=`2592000`、最大24hの原設定観察と固定identityを持ちます。全適用lockは当該prefixのAge30日1rule、Date/indefinite/重複なし、lifecycleは既存empty-prefix multipart abort7日のみ。公開development URL無効、custom domainなし、同bucket限定writer/管理権限なしを確認し、既存probe1日ruleとstable資格情報を変更しません。

実bundle keyは`evidence/first-use/<repositoryId64>/<evidenceHead40>/<runId32>/bundle.zip`です。同run directoryのsynthetic `protection-witness.txt`にだけ異bytes PUT/DELETEを試し、正確な403/forbidden/ObjectLockedByBucketPolicyまたは409/other/ObjectLockedByBucketPolicyと原bytes/hash維持を要求します。同writerのunlocked対照をPUT/GET/hash/DELETE/404NoSuchKeyまで確認し、writer権限不足をlock成功にしません。実bundleは不存在確認→durable put-intent→1回のPUT→別process GET/hashだけで、Evidence bundle DELETEをtransportも拒否します。

ネットワーク前の`intent.json`とPUT直前の`put-intent.json`をCreateNew/flushで固定します。後者の境界後は送信されていなくてもmay-have-been-sentとして同keyへ再送しません。失敗・中断・不明ではledger/readyを偽らず、key/local path/保持下限をresidue/intentに残します。失敗原因と修正範囲を記録せず新runへ逃げず、成功runを反復して増やさない運用です。別runの重複publish全体を禁止する仕組みはなく、追加は技術的には可能です。自動再送/自動復旧/清掃はありません。

publishのexit0はcandidate生成までです。`evidence commit`は通信せずcandidate/result/intent/receipt/設定原記録のhash・identity、13観察のtuple/bytes/hash、receipt完了後かつ24h以内の事後設定と保護内容不変を独立再照合し、`OneStarMaker/Artifacts/evidence-ledgers/<id>/ledger.json`をCreateNew・同directory atomic renameで確定します。pending write/renameや競合は確定済みにしません。network/資格情報はledgerへ入れません。CLIの外側catchはEvidenceでも非秘密の`Credential operation failed.`であり、詳細は非秘密result/residueから確認します。

publisherと別の引渡し担当が`EvidenceLedger.psm1`の`Write-EvidenceReaderInput`で2ledgerとfresh configを固定案内へ結び、案内SHA-256を新規reader開始promptへ渡します。readerは`Read-EvidenceReaderInput`で案内bytes→prompt期待hash、ledger/config/reference/entry集合を照合してから公開CLIの`fetch`を実行します。「公開CLI」は匿名bucket公開ではありません。archive/outer manifest/版/全entryを検証したprivate新規readyから必要ログと原画像を実際に閲覧し、script/DLLを実行しません。C所見・可変HANDOFF/PR・sender stagingを案内へ混ぜません。24h観察期限後は同policy identityのfresh原観察/configと新immutable案内/期待hashを固定し、旧案内を更新しません。policy変更後のfetchは後続で扱います。

serverLockLowerBoundとreferenceのretainUntilはPUT-intent通信前時刻+30日という保護下限で、削除期限ではありません。保存義務はowner D close後30日以上かつ参照中です。元ledgerのclose欄はnullを保ち、Dの明示owner closeイベントから`Write-EvidenceCloseRecord`が別の`evidence-retention/<id>/retention.json`をappend-onlyで固定します。有限server lockはclose後30日/長期参照の全期間を保証しません。期限後も自動削除せず、延長・policy移行は後続、清掃はexpiredかつunreferencedと確認した正確なkeyへのowner明示承認が必要です。原source/受信copy/旧失敗/snapshot/intent/ledger/案内/原観察、実bundleとlocked witnessを保持します。

判定済みimplementation headは`896eaea8a912248333704c80549d46ecf20c02c6`です。7 offline suites119/119、3build、PowerShell26file parse、contract/docs/diff検査、最終DLLのhash/MVID/依存とbuild後71件再確認に加え、実R2の各13観察、E1/E2各1送信・独立ledger、別session fetch・全19entry/版/hash・指定ログ/原JPEG閲覧を確認しました。Cとblind C′は同249-file固定入力で各GO、同OpenAI/GPT系列という独立性制約があります。ownerのD closeは記録済みで、小台帳・保持記録は[PR #104](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/104)へ引き継ぎます。ローカルWindows同一ユーザーに限るfirst useで、Cloud/Build/別hostの成立や無期限保護ではありません。

原画像の対象prefix全文/lifecycle日数は省略され、全設定の構造観察JSONとlive witnessで補完しています。画像だけで全設定やwriter権限を確認したとは扱いません。CreateNew/flush/ACL/hashは物理WORMではなく、悪意ある同ユーザーや管理者の途中rule変更の防御は保証しません。

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

Evidence 34件のうち`reader-hash`と`handoff-identity-substitution`は偽transport/隔離出力を使いますが、入力には製品に固定したprivate実E1 source treeを読みます。所有者端末では119件を確認済みですが、Git treeだけではこの2件を再現できず、sourceが無い環境ではfail-closedで失敗します。原Evidenceをコピー配布・改変したり実R2へ再送して補完しないでください。テスト入力のdummy fixture化は後続のoffline可搬性改善です。

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
pwsh -NoProfile -File tools/Artifacts/tests/ArtifactEvidence.Tests.ps1
pwsh -NoProfile -File tools/contract-audit.ps1
pwsh -NoProfile -File tools/docs-audit.ps1
```

DPAPIとACLの検証は所有Windowsユーザーの実行環境で行います。別のsandboxユーザーの失敗や成功を、所有ユーザーでの検証に読み替えません。入力判定を変更した場合は、上記の文字列回帰に加え、実PowerShellの解析とConsole付き非対話起動を照合し、入力前の有限時間での拒否と保存物の不変を確認します。通常のmasked入力も確認し、リダイレクト下のテストだけで対話入力まで検証済みと扱わないでください。
