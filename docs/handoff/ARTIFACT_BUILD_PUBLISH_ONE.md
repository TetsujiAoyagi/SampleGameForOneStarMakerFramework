# 既存Build 1件のpublish・読戻し検証・成功記録

## 0. メタデータと承認状態

- type: slice
- status: B（A3 r2を人間承認により凍結。B着手、C/C′/D未着手）
- task: `artifact-build-publish-one`（今回新規。Workflow active / version 1）
- branch: `codex/artifact-build-publish-one-phase-a`
- implementation base commit: `f5c67596a264baca48e49771acf54b34076d9c22`
- implementation head commit: 未到達。今回の変更は計画文書のみ。
- risk: normal。単一Buildの新規保存。削除・共有状態更新・Unity変更を含まない。ただし成功確定と不明応答は重点レビューする。
- owner: root / OSM maintainers
- created: 2026-10-10
- expires: 2026-11-10 または次revision
- harvest to: 実装成立時の操作・限界を tools/Artifacts/README.md、program Bの現在地を docs/handoff/BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md
- A0/A1版: r2。A2初回は同一r1を使用し、修正確認も同一r2を使用する。
- A3承認記録: user-20261010-build-publish-a3。人間の最新承認を受理。設計r2を凍結し、B/C/C′とdevelop向けPR作成を許可。mergeは含めない。固定snapshotは tools/Artifacts/artifacts/build-publish-one-planning/A3-frozen-r2.md、hash/時刻は freeze-manifest.json。

この変更で初めて、既存のPlayer出力1件をUnityを起動せずに保存し、返された参照と期待hashを使って別processで取り出せる。単なるpolicy fixture接続ではなく、snapshotから検証済み成功receiptまでの一操作を作る。Buildの件数清掃が完成したとは扱わない。

## 1. A0: 現況・根拠・制約

### baseと並行作業

2026-10-10 JST、origin/developをfetchし、GitHub refs/heads/developも照合した。上記baseは依頼時参考SHAと一致。PR #109はmerge 2203dded、#110はmerge f5c67596でclosed/merged。両PRの最新本文とレビュー/会話を確認した。#109のscheduler修正・正式C/C′完了は既存経路の事実であり、今回の成功根拠へ転用しない。#110は純粋選別と9caseの確認で、成功・所属・一覧完全性・利用状態はcaller責任という境界を維持する。

開始時open PRは #94、#95、#98〜#103。変更file一覧との照合では今回予定のArtifacts fileに重複なし。#98〜#103はdocs/README.mdを含むため、今回Aでは同fileを更新せず、この専用HANDOFFだけをtracked変更する。他PR全体のレビューや修正はしない。提出直前にdevelop差分を再確認し、無関係変更を取り込まない。

既存checkoutはdevelopのまま。専用managed worktreeを上記SHAから作成。task storeはworktree単位ではなくrepositoryId + taskIdで共有される。今回だけのtaskを開始し、完了taskをresumeしていない。承認待ちはtask終了ではなくactiveを維持する。

### 再利用できるものとEvidence固有のもの

- `tools/artifacts.ps1` (81行): publishはevidence限定。Build CLIは存在しない。
- `ArtifactApplication.psm1` (374行): deadline、credential generation固定、PUT/GET、別pwshの読戻しとexit/pipe EOF確認は再利用できる。Evidenceのruntime binding分岐はBuildから有効にしない。通常BuildでEvidenceのBudget/stateを設定しない。
- `ArtifactPaths.psm1` / `ArtifactAcl.psm1`: private GUID operation、path/reparse/ACL検証は再利用できる。既存Evidence catalogへBuildのcopyを登録しない。
- `Packaging/PackageIO.cs` (340行) / `PackagePolicy.cs` (127行): 有限ZIP・hash・明示entry・安全な展開は再利用できる。ただしCreateVerifiedはRequireEvidenceTaskとpurpose=evidence/schema=2を固定、Extractもevidence/synthetic前提。Buildを偽Evidence taskとして通さず、限定Build codecが必要。
- `Transport/R2ArtifactTransport.psm1` (41行) と `Transport/ArtifactReadback.ps1` (66行): 現在のprefix allowlistはBuildを拒否する。Build専用keyのPUT/GETのみ追加する。C# transportは有限転送/再試行0を既に持つ。
- `EvidenceApplication.psm1` (129行): Workflow task guard・active/resume・stage adoption・task state・receiptとready状態の二段更新はEvidence固有。丸ごと一般化・複写しない。現在の読戻しは別processのGET全bytes/hashと事前のpackage全検査を組み合わせている。
- `EvidenceContract.psm1` / `EvidencePaths.psm1` / `EvidenceStateStore.psm1`: deployment登録、task終了event、30日、use、cleanupの状態正本。Buildのowner/寿命をここへ足さない。
- `BuildRetentionPolicy.psm1` (148行) と `tests/BuildRetention.Tests.ps1` (222行): 純粋policyだけ。production caller、Build成功receipt、Build catalog、Build fetch入口はいずれも存在しない。
- SampleGameの`PlayerBuildPublisher`が出す`build-receipt.json`はローカルPlayer生成の証拠であり、R2 publish成功receiptではない。これを保存成功に読み替えない。

参照した契約はAGENTS、osm-workflow全文、phases-and-handoff、docs-policy、architecture-gates、handoff-template、review-evidence、docs/README、program、Artifacts/Workflow README、Harnessの現行記録契約。完了HANDOFFと過去私的記録を入口にしていない。

### 既存実Buildと対象サイズ

検証候補は主checkoutの
`artifacts/bs4/players/20260924T202227427Z-b80d0ef69df841f4bde30f85a2b4cd6e`。
266 files / 131,584,119 bytes（約125.5 MiB）、最大file GameAssembly.dll 49,191,936 bytes、reparse 0をread-onlyで観測。SampleGame.exe、UnityPlayer.dll、GameAssembly.dll、D3D12、SampleGame_Data、content、build-receipt.jsonを含む。unity/Builds/ActiveVariantにはfileがなく、こちらを候補にはしない。bs4の他3出力とbs2bの既存出力も存在するが、今回のlive入力は上記1件に限定する。

生成receiptのtarget=StandaloneWindows64-Player、backend=IL2CPP、stripping=Highを確認。系列metadata案はproject=`samplegame`、target=`windows-x64-player`、configuration=`il2cpp-high`。configurationはこの保存操作で明示する分類名であり、未知の開発/リリースflagやソースcommitを捏造しない。build-receiptはpayloadに含めるがStorageがUnity schemaとして解釈しない。

全fileをread-onlyでhash取得した。最長相対path109字、case衝突0、inventory JSON 49,754 bytes。Git外のbuild-inventory.jsonへ266件を保存（SHA-256 d75be5cb97d08b15d588413941a75ec52e5887f3748c3035d13b6362e37b002e）。実Buildのsnapshot/publish/fetchは未実施。動作可能性・起動成功はこのスライスで証明しない。候補が後で消失/変更したらその事実を記録し、同一入力の成功とは扱わない。dummyテストだけではGOにしない。

### 後のR2検証の成立条件

既存DPAPI profile osmはstatusでstable、retiredなし。これはlocal-onlyで接続成功ではない。現在の登録deploymentはd8eb82b00ed744bd84ccfa2bee0c0ffa、private bucket osm-artifacts、Evidence prefix development/evidence/v2/。canonical repositoryIdは16663c274d3c4478e5dcf3de044f82a356c1de1e4d881b0fa2ae9b59855eac0c。登録configとreceiptの整合を既存readerで確認した。既存baseline ID=settings-before-2026-10-06、owner observation ID=owner-unchanged-config-2026-10-08を参照できる。

既存事前承認は同account/検証済みendpoint/private bucket/新Evidence prefixの通常操作。本案の`development/builds/v1/`は追加でA3に固定する範囲。Evidence configを編集・上書きしない。後のBuild configは厳密な8 fieldだけを持つ非秘密JSONとする: `schemaVersion=1`、`purpose=build`、`profile=osm`、`evidenceConfigPath`（既存登録configの絶対path）、`evidenceConfigSha256`（登録config bytesの64hex）、`repositoryId`（上記canonical値）、`prefix=development/builds/v1/`、`policy=build-publish-v1`。BuildContractは既存Read-EvidenceConfigをread-onlyで呼び、登録receipt/config hash・profile・repositoryIdを照合し、endpoint/bucketはそこからだけ解決する。Build configのpurpose/policyはEvidenceの30日契約を継承しない。EvidenceContractへの依存は既存deployment設定の読取だけで、登録・task・event・cleanupには触れない。endpoint/bucketを任意指定する汎用provider設定は作らない。既存鍵の複製・再入力・削除や権限変更を前提にしない。

新prefixに対する実権限・現設定との適合は未通信のため未確認。A3で同既存鍵による限定PUT/GETと当該Build範囲を承認対象にする。実装後Cが最終headで最初のPUT/GETを行い、拒否なら未達のまま止める。設定/鍵変更や別prefixへの逃避をしない。新ruleの設定や管理権限は要求しない。非秘密性は名前・hashだけで断定せず、Cの送信前に候補の内容種別と設定fileを確認する。

### 初回Cのconfig/selection生成と受渡し（未実施）

C担当が最終clean headの既存 `New-ArtifactOperation` でprivate GUID operationを1つ確保し、その絶対pathをCの証拠rootに記録する。既存の登録config pathは `C:/Users/void/AppData/Local/Packages/OpenAI.Codex_2p2nqsd0c76g0/LocalCache/Local/OneStarMaker/Artifacts/deployments/d8eb82b00ed744bd84ccfa2bee0c0ffa.config.json`、期待SHA-256は `fbc60c8ac6e6553fe8fd2861690e8ecaf4e8d9589caf08d6ef0b6600ae79c922`。この既存pathをread-only検証し、上記8 fieldで `<operation>/build-config.json` をCreateNew/UTF-8 no BOM/Flush(true)で作る。既存ArtifactAclのSet/Assert-ArtifactAclを新fileだけに適用し、reparseを拒否する。既存deployment/資格情報は複写・変更しない。GUID部分は操作前に生成した値を記録して固定し、後でglob探索しない。

同じC担当が `[Guid]::NewGuid().ToString('N')` を一度生成して後述buildIdとして `<operation>/build-selection.json` に保存し、候補rootの全fileを明示リスト化する。両JSONのhash、候補root全entry/hashと内容種別の確認、A3承認recordの識別子を送信前preflight記録に結ぶ。probeTokenはBS4生成コードで作った非秘密fixture識別子であり認証鍵ではない。UnityServices設定はpackage/version/initializer等を確認済みだが、バイナリ全体の秘密不存在の証明とはしない。最終送信前に担当Cが固定選択の非秘密性を判断し、未確認なら送信しない。

BuildContractはconfig fileと親operationのprivate ACL/reparseも検査し、読取後の固定bytes/hashをpublish中保持する。Build config hashと既存deployment config hashをintent/receiptに含める。config/selectionの準備はこのC用の既存file操作で足り、新しいconfig writer CLIやregistration storeを作らない。読み取り不能/hash不一致/所有不一致は未達で止め、既存rootのACL修復や設定再登録をしない。operation内の非秘密原記録をC/C′ bundleへ収録し、同hostのレビュー担当がmanifest hashと全file hashを検査して取得する。root所有者は今回のC担当、task完了判断まで原本を保持し、承認なしに削除しない。

## 2. A1: 最小契約案

### 入力・系列・入口

主入口案:
`pwsh tools/artifacts.ps1 build publish --profile osm --config <build-config> --selection <selection>`。
selectionは厳密なschemaVersion=1、purpose=build、absolute root、明示relative files配列、project、target、configuration、buildIdのみ。filesは呼出側が1つのBuild rootから列挙し、CLIは黙って対象を増やさない。空集合、未知field、重複/case衝突、path逸脱/reparse、資格情報/所有store/別task領域を拒否。root外のfile、Unity設定やLibraryの推測収集は禁止する。

project/target/configurationは各1〜64字のlowercase ASCII token（先頭英数字、以降英数字/hyphen）。系列はrepositoryId内で`project/target/configuration`のOrdinal一致。branchを系列に含めない。buildIdは呼出側がpublish前に生成しselectionへ永続保存する32hex GUIDで、元のBuild生成identityとは別。成功応答からIDを知る設計にしない。同一repository内のbuildId再利用は、BuildStoreが所有するCreateNewのID予約markerでnetwork前に拒否する。既存IDへpublishを再実行してもPUTしない（already-exists）。intentだけなら未完了、有効receiptなら別入口inspectで同じ成功を確認する。破損/不明recordを上書きしない。新IDでの再publishは明示した別操作であり自動回復ではない。source commit不明を現在のHEADで補わない。

上限は既存のZIP/転送256 MiB、manifest/selection/receipt各1 MiB、entry4096、単一file256 MiB、総展開1 GiB、圧縮比100、relative path240字を維持。Build snapshotの選択総量は今回追加の安全上限240 MiB、file数は512に限定する。NoCompressionのZIP overheadも最終256 MiBで再検査。約125.5 MiBの候補を包める。全体10分、各network/readback120秒、子終了確認15秒の現行予算を維持。超過はPUT前拒否または未完了。multipart、上限拡大、時間無制限化はしない。

### 同一snapshot

選択とmetadataを最初に固定し、全選択fileのread handle（FileShare.Read）を確保してからcopy/hashする。512 file上限でhandle数を有限にする。選択rootのfile集合はcopy前後で照合し、明示集合との差や書込競合は拒否する。snapshot完成後は原sourceを再読してpackageに混ぜない。snapshot、manifest、ZIP、receiptのentry集合/bytes/hashとmetadataを一致させる。これは明示fileの保存時snapshotであり、Build生成時の原子性やソースrevisionは保証しない。

### publishと成功の唯一の確定点

1. 検証済みBuild config/selection、入力buildIdの予約、系列、entry snapshotをprivate operationに確保する。
2. Build schemaのmanifest/ZIPを作り、既存安全展開を使ってmanifestと全entryをローカル検査する。ZIP/hash確定後は送信完了までread lockを維持する。
3. remote keyは厳密に`development/builds/v1/<repositoryId>/<project>/<target>/<configuration>/<buildId>/bundle.zip`。呼出側から任意keyを受けない。送信前にこのkey、metadata、package/manifest hash、bytes、operation pathをimmutable intentへflush/renameする。
4. 1回PUTし、成功応答後、別pwsh processで同keyをGETし、EOF・bytes・外側SHA256・子exit/pipe EOFを照合する。ローカルで検証済みZIPと完全同一bytesのhash照合をもって読戻しのmanifest/entry同一性を結ぶ。GET側で未検査のmanifestから期待hashを補わない。
5. 成功receiptを同directoryのtempへ書き、Flush(true)・ACL検査後、存在しない最終名へatomic renameする。これが唯一の成功commit点。receiptを再読・厳密検証してからstatus=passed / exit0、reference、独立fieldのpackageSha256、buildId、receiptPathを返す。

成功記録はprivate Known Folder LocalApplicationData配下の`OneStarMaker/Artifacts/builds-v1/<repositoryId>/<buildId>/receipt.json`だけ。intent/失敗診断は同buildIdに属するが成功正本ではない。共通root全体のACLを直さず、今回の新領域だけをprivateに作る。ownerはArtifact Build application、寿命は後続のBuild保持/清掃が引き取るまで保持。Workflow task end/30日/schedulerへ登録しない。Build stagingもEvidence清掃に登録しない。

receiptはschemaVersion=1、purpose=build、buildId、repositoryId、project/target/configuration/seriesId、key、configSha256、evidenceConfigSha256、packageBytes/packageSha256/manifestSha256、entry path/bytes/hash、intent hash、限定readback観測、publishState=succeeded、publishedAt UTCを持つ。publishedAtは成功commit用に一度固定したUTCで、PUT時刻や元Build生成時刻ではない。正本はreceiptであり、別のcatalog/index/ready flagを成功条件に足さない。R2へ第二のreceiptを同期しない。同PC/同userでの永続性であり、端末喪失からのcatalog復元は保証しない。

referenceはBuild専用opaque prefix `osm-build-v1:`。repository/系列/buildId/key/manifest hash/bytesを厳密検査する。期待package hashはreferenceから自動補完せず、publish結果の独立fieldまたは信頼されたreceiptから別に渡す。

返却例の利用入口:
`pwsh tools/artifacts.ps1 build fetch --profile osm --config <build-config> --reference <ref> --sha256 <trusted64>`。
新規private operationへGETし、外hash・Build metadata・manifest・全entryを検査してoutputPathを返す。既存sourceへ上書きせず、DLL/EXEを実行しない。Build利用中APIは付けず、今回Build削除経路が存在しないことを境界にする。

単一記録の再照会入口:
`pwsh tools/artifacts.ps1 build inspect --profile osm --config <build-config> --build-id <id>`。
呼出前にselectionへ保存した指定IDだけを読み、receiptが厳密に成立するとき同じreference/hashを返す。全系列一覧ではない。receipt不存在・破損・intentのみを成功扱いしない。このread-only入口は応答喪失後に同じ成功を見つけるために必要で、再PUTや再成功commitを行わない。

### 失敗と曖昧さ

- 入力/サイズ/snapshot/intent失敗: PUTなし、成功receiptなし。安全なreasonCodeだけ返す。
- PUTのtimeout/応答不明: 同key再送・自動再試行をしない。remote存在不明のintent/residueを保持し成功を返さない。今回はreconcile機能を追加しない。
- PUT成功後のGET失敗/hash不一致/子未終了: 成功receiptなし。remoteは未検証、local/remote residueを残す。取消要求だけで資源解放済みにしない。
- receiptのwrite/flush/rename前失敗: 検証済みobjectがあっても未完了で成功に数えない。旧成功receiptは別buildIdで不変。
- commit後に応答だけ喪失: receiptが有効なら成功は既に永続化済み。失敗recordを上書きせず、inspectで同一ID・時刻・hashを返す。rename結果不明は成功と断定せずcommit-unconfirmedを返し、次processのinspectで正本を検査する。
- 実DELETE・rollback削除・置換は全経路で呼ばない。失敗残置はこのsliceの既知限界。自動清掃の実装前に勝手に7日や30日で消さない。

#110へ将来渡せる意味は、receiptのbuildId→buildId、succeeded→publishState、publishedAt（厳密UTC DateTimeOffsetへparse）、系列tuple→SeriesIdまで。`inUse`は本sliceでは記録しない。存在しない値をfalse補完しない。全系列一覧/利用状態が揃わないためpolicyをproductionから呼ばず、「接続済み」と主張しない。

## 3. 最低受け入れ条件と検証経路

- M1: 明示した実Build1件をUnityなしで入力し、同一snapshotのpackageを1回PUT→別process GET→成功receipt確定まで一操作で完了できる。
- M2: 返却referenceと別渡し期待hashでfresh processのbuild fetchを行い、全entryのbytes/hashとmetadataが元snapshotと一致する。取得物の実行は不要。
- M3: 次の失敗集合で成功誤認/既存成功の削除・置換がない。source/Evidence/credentials/他taskの変更もない。

必要なoffline集合（BuildPublish suiteの登録case名案。これはfixture成功を実Build成功へ読み替えるものではない）:

1. `snapshot-roundtrip`: metadata/全entry/receipt/refの対応、sourceをsnapshot後変更してもarchiveは変わらない。
2. `input-and-size-reject`: 空/重複/case/逸脱/reparse/不明schema/非正規系列/512・240MiB超をnetwork前拒否。限界は小さい注入上限で検査し実大容量を何度も生成しない。
3. `snapshot-conflict`: read-lock競合・選択集合変化・snapshot改変を拒否、PUTなし。
4. `put-unconfirmed`: 送信後応答消失を注入しPUT回数1、receiptなし、residueあり。
5. `readback-failure`: GET error/hash mismatch/子exitまたはpipe未確認を各注入し成功なし。
6. `receipt-commit-failure`: write/flush/before-rename失敗は成功なし。after-rename/成功JSON全喪失は呼出前のselectionだけを使うfresh inspectで同じ1件を解決しpublishedAt/ref/hash不変、PUT回数1を検査する。test内部で生成IDを横渡ししない。同じIDの再publish/同時publishはnetwork前拒否も確認する。
7. `fetch-integrity`: 独立期待hash必須、manifest/entry tamper、Build referenceをEvidenceへ/逆方向へ渡す混同、範囲外keyは拒否。
8. `existing-success-preserved`: 同系列に既存成功を置き別ID失敗を注入。旧receipt/package不変、DELETE呼出0、Evidence/task/資格情報のwrite0。
9. `fresh-process-entrypoints`: 実CLI grammar・store読取とfresh-process reader経路を通すoffline seam。親processのmock成功値だけで子経路成立としない。

C担当はWindows同user・最終clean implementation headで、上記候補rootの明示fileリストと送信前非秘密確認を固定し、M1を1件、M2を1回行う。前後source全hash、snapshot inventory、選択JSON/hash、CLI JSON/exit、intent/receipt/hash、transport/readbackの限定観測、fresh fetch全entry照合をGit外evidenceへ保存。DLL/EXE起動、自然期限待機、DELETE、公開URL再撮影は不要。新prefix疎通は未確認・初回C。失敗なら理由と残置を記録しNO-GO、fixtureへ差し替えてGOにしない。

## 4. 責務配置・変更予定file・規模

Aでtracked変更するのは本HANDOFFのみ。以下は承認後Bの案であり未実装。

- `tools/artifacts.ps1`: 81行→+25〜40。CLI grammar/dispatchのみ。Build publish/fetch/inspectを明示分岐しEvidence switchを流用しない。
- 新 `tools/Artifacts/BuildContract.psm1`: 150〜220行。selection/config/reference/receiptのstrict codecと系列/key生成。値の検証と、既存登録deployment/configのread-only照合。codec純粋関数は時計/通信/Unityに依存せず、設定読取は既存Read-EvidenceConfig/ArtifactAclへ限定する。public CLIから呼ぶ内部module、単体テスト可能。
- 新 `tools/Artifacts/BuildStore.psm1`: 120〜180行。今回のprivate directory、CreateNewによるID予約、immutable intent/receipt、指定ID読取。owner/lifetimeはBuild、ArtifactAclとWindowsStorePathsだけを使う。EvidencePathsのtask-root付きatomic writerはimportしない。再利用できるflush/rename手法を小さく実装し、汎用storeを抽出しない。
- 新 `tools/Artifacts/BuildApplication.psm1`: 200〜280行。snapshot/梱包/PUT/readback/commit/fetchの順序。operation資源はinvocation所有、残置はBuild intent所有。clock/transport/store failureを内部注入してoffline検証する。Unity/Workflow/EvidenceApplication/retention policyへ依存しない。
- 新 `tools/Artifacts/Packaging/BuildPackage.cs`: 100〜160行。Build manifestのmetadata規約。既存PackagePolicyの安全制約とPackageIOのbounded ZIP mechanicsに依存し、Evidence taskを要求しない。
- `Packaging/PackageIO.cs`: 340行→+40〜90。既存verified snapshot/ZIP writer/extractorの限定共有部をinternalへ切り出し、Build codecを通せる最小入口を置く。既存Evidence/synthetic公開signatureとschemaを維持する。新しい任意purpose pluginや汎用registryを作らない。形式の意味はBuildPackage、byte I/OはPackageIOに分ける。
- `Packaging/PackagePolicy.cs`: 127行→0〜15。必要ならBuild identity検証への内部補助のみ。既存上限/安全pathを緩めない。
- `Transport/R2ArtifactTransport.psm1`: 41行→+5〜15、`Transport/ArtifactReadback.ps1`: 66行→+3〜8。Buildのexact keyをPUT/GET allowlistへ追加。Build DELETEは明示拒否。transport C#の通信方式、bucket、SDK依存は変更しない。
- 新 `tests/BuildPublish.Tests.ps1` と必要な非秘密child fixture: 300〜450行程度。上記9集合。既存 `tests/ArtifactPackage.Tests.ps1` / `ArtifactTransfer.Tests.ps1` (必要な各+20〜50行)には共有codeとBuild key境界の回帰だけ追加。
- `tools/Artifacts/README.md`: 現行操作として成立後に入口/成功点/上限/未対応を+30〜50行。program Bは進捗2〜3文だけharvestする。

500行・3責務・50%増は構造説明のtrigger。新moduleは責務ごとに分け、PackageIOが500行超またはcodec/永続化を抱えるならAへ戻す。短いtransport wrapperは50%増より変更理由を評価する。snapshotだけの汎用frameworkを先行追加せず、BuildApplicationの局所処理に留める。上記範囲で再利用できずEvidence全体の一般化が必要なら、阻害する関数とさらに小さいAPI境界をAへ返す。

## 5. 記録方式・B/Cの検証分担

本sliceは従来tracked HANDOFF方式を明示採用する。H1/external-current-v1は未適用とし、CURRENT initやApprovedSpecificationsへの追加を行わない。理由は現H1がtask別固定profile/承認hash/adapter登録を要し、この単一publishへ適用するにはHarness変更と広いsuiteが増えるため。#110同様、未移行を認める現行規約の範囲で処理する。完了済みEvidence taskのCURRENTを今回の入口に使わない。

従ってB完了にH1 B exit済みとは書かない。Bの局所確認は変更PowerShellのparse、Packaging/Transportの.NETコンパイル、docs/contract/diff auditまで。offline suiteの実行/合否と実R2は新規C sessionの責任。H1を採用したい場合は、承認前にこの節と必要scopeを改版し、暗黙移行しない。

- 発見C/差し戻し中: BuildPublishの関係する上記caseを `-Case <name,...>` で限定、必要なら共有ArtifactPackage/Transferの既存suite。0件は不成立。
- 判定C: clean最終headでBuildPublish全9集合、ArtifactPackage全件、ArtifactTransfer全件、ArtifactEvidence全件、BuildRetention全9件、変更PowerShell parse、Packaging/Transport Release build、contract/docs/diff audit。共有Packaging/Transport/Evidence CLIの回帰を証明し、policy意味が変わらないことを確認する。
- Credentials/Rotation、RouteProof、EvidenceCleanup/Schedule/Reset全suite、Workflow/Harness全suiteは無変更で既存の固定runtimeにも触れないため判定必須へ複製しない。新しい依存/変更が必要になった場合だけAへ戻し範囲を評価。
- Unity source/asmdef/asset/Editor/BuildSystemを変更しないため最終全EditModeは適用除外。代替は上記.NET/PowerShell回帰と実Build bytesのM1/M2往復。Unity未起動を明記する。
- 各suiteのregistered/selected/executed非空同集合・failed0、DLL path/hash/MVID/deps、base/headを収録。関係するsuiteがProbe DLLをロードする場合は既存Probeをコンパイルして依存を成立させるがRouteProof実通信はしない。
- Git外の今回専用記録directoryにA2原文/固定A1、B結果、完全diff、C生結果を保存。同host新規sessionへpathとhashで渡し、実際に読めることを確認。C′には凍結Aと所見なしBと同head生証拠だけを渡し、C所見を隔離する。
- Phase Aで検査するのは文書/auditだけ。Build作成、.NET実装build、テストsuite、R2 PUT/GET/DELETE、設定変更、mergeは今回未実行。

## 6. 対象外・停止規則・後続へ渡す問い

対象外は実DELETE、N件清掃、自動起動/scheduler、inUse API/lease/heartbeat、取得物の起動、GUI、全系列検索一覧、Cloud/別host/provider/リリース配布、Unity/BuildSystem/Harness刷新、Evidence30日/終了再開意味の変更、旧reset/legacy/#107復活、Unity native終了調査、新レビュー制度/daemon/job基盤。

M1〜M3が成立して初めて後続候補へ進める。実Build未検証、receipt確定不能、新prefix疎通不能、共有Evidenceの契約変更が必要ならNO-GOまたはA改版。未確認を成功に読み替えない。今回Phase AではA3承認案提出で止まり、承認前凍結・B着手をしない。

program B後続の問いは、完全な系列snapshot収集、利用状態の根拠、#110 policyへの運用接続、削除直前の排他/再確認、失敗残置の有限清掃。program Cは別hostのreceipt/信頼hash受渡し、program DはBuildSystem/Harness接続。今回は詳細設計しない。

## A2指摘の採否と独立性

主担当はroot（GPT-6系 / OpenAI）。A2は新規の履歴非継承sessionを2件起動し、r1固定copy（SHA-256 ad8bc3a6a3942eb8370358987123e7c0b863b084373e6ec8f7360472295d5449）を両者が照合した。architectureはgpt-6-astra指定、failure-pathsはgpt-5.6-sol指定。同じOpenAI vendorという制約はあるが、主担当の会話・互いの所見を渡していない。C′は未実施で、A未関与モデル（または人間）を後のPhase開始時に選ぶ。今ここで監査済みとはしない。

- A2-ARCH-F1 [P2/semantic、現slice阻害]: 成功応答喪失後にinspect用buildIdを取得不能。**採用**。呼出側がselectionへ事前永続化するIDとCreateNew予約、既存ID再publish拒否、公開fresh inspectによる回復へ修正。新たなprepare API/検索catalogは不採用（追加入口が不要）。
- A2-FAIL-F1 [High/semantic、現slice阻害]: 同じ応答喪失問題。ARCH-F1との**duplicateとして採用**。内部IDをテストから横渡ししないcaseを固定。
- A2-FAIL-F2 [Medium/semantic、現slice阻害]: Build configの生成/保存/受渡し経路未固定。**採用**。§1の厳密schema・C担当・具体path/hash・既存private operation・CreateNew/flush/ACL・receipt/証拠への結合で解消。新設定store/登録APIは不採用（既存非秘密設定の読取とC用file操作で足りる）。
- 両者が挙げた完全系列一覧/inUse/cleanup/別host復旧は**後続へ移送**。M1〜M3の成立に追加しない。未解決のA2阻害は0件。これを実装GOや人間承認へ読み替えない。

両者へ同じr2（SHA-256 4240c246b109aed619a1cc360c55c5cc15fe0989f0766de1a9c80900a1eb0254）を渡し、自身の指摘だけを修正確認した。互いのraw/採否台帳は渡していない。architectureはF1解消・新阻害なし、failure-pathsは2件解消・新阻害なし、A3統合へ送付可と回答。どちらも実装/test/R2/Unity未実施、A3承認・凍結を代用しないと明記した。

原入力・原文・改訂確認はこの専用worktreeの `tools/Artifacts/artifacts/build-publish-one-planning/` に置く。同hostの両reviewerが固定copyを実読しhash一致を確認した。architecture reportは担当の返答をrootが保存（担当のfile書込はfilesystem権限制約、auto-review拒否ではない）。生成証拠をGitへ追加しない。本書は小さな採否台帳として取得先を残す。生成時刻/bytes/hashは同directoryの `manifest.json` に記録する。

## A3承認案と現在地

人間へ承認を求める単位は本書r2のM1〜M3、責務/変更file、上限、単一receipt正本、呼出側既知ID、従来HANDOFF方式（H1/CURRENT未適用）、B/C分担、および後のC限定実検証である。新Build prefixと同user既存鍵による1件PUT/読戻し/fresh fetchを追加範囲とし、実DELETE・鍵/既存設定変更・実Build作成は含めない。

後の限定live検証は、未確認の新prefix権限と非秘密入力確認がC開始時に成立しなければNO-GO。既存Evidence用の事前承認をBuildへ黙って拡張しない。人間承認前は本書を凍結済みsnapshotやBの実装開始許可として扱わない。

提出直前にorigin/developを再fetchした結果は `f5c67596a264baca48e49771acf54b34076d9c22` のままで、固定baseとの差分0。取込/再計画は不要。今回のtracked変更はこのHANDOFF 1件だけ。後の共有file変更予定は§4と本チャットの作業報告で明示済み。PR作成・push・mergeや他PRへのコメント送信はしていない。

今回実行したのはread-only調査、専用worktree/branch・新規task開始、計画/レビュー記録保存、文書/契約検査だけ。承認待ちで停止し、Workflowはactiveのまま。B/C/C′/D、Unity/.NET build、offline suite、R2通信・DELETE、設定変更は未実施。通常docs/contract auditは本未移行taskの文書検査であり、H1適用の証拠ではない。

A3人間承認: **2026-10-10受理**（user-20261010-build-publish-a3）。凍結: **r2凍結済み**。上記の承認待ち記述はA提出時点の履歴として残す。本承認記録が現在地を上書きする。新規Phase B sessionへ固定snapshotを渡し、C/C′完了後にPRを作成する。Season別Playerフォルダは明示root単位で保存できるが、Content Directory依存関係・外部content自動収集・系列分類の拡張はprogram D以降へ送る。

## Phase B 結果

Build専用の`BuildContract`、`BuildStore`、`BuildApplication`、`BuildPackage`を追加し、CLIのpublish/fetch/inspect、既存ZIP I/OとTransportのBuild限定入口、9群のofflineテストコード、Artifacts操作文書を実装した。H1/CURRENTは適用しない。PowerShell parse、Packaging/Transport .NET Release build、docs/contract/diff auditをBで確認し、offline suite・実R2・Unityは新規Phase Cへ渡す。固定実装head、実行commandと詳細はGit外の`tools/Artifacts/artifacts/build-publish-one-planning/B-result.md`を正とする。
