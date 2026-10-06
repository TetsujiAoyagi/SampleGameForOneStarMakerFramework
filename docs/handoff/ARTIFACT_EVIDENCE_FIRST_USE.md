# Artifact Storage — Evidence first use（A3凍結 r1）

## 0. メタデータと現在の到達点

- type: `slice`、status: `C/C′完了 — 各担当GO、D未実施`。
- branch: `codex/artifact-evidence-first-use-phase-a`。
- implementation base: `6d804ca637cf42fb876e602c8ccc0656cfbd255d`。2026-10-06のorigin/develop読取とlocal developが一致、開始時tracked差分なし。
- implementation head: `896eaea8a912248333704c80549d46ecf20c02c6`（15 code filesだけの単一固定commit）。B result SHA-256=`b900a3730d5f61d61b15249b25c79469369540b3d55941af6c2e66282d3e0466`。旧発見C head `042dd8bcd86b264dbb3a870ff59be4f260a0d67c` はlocal ref/固定rawで保持。judgment evidence / C′ blind bundleは同249-file版で固定・受信照合済み（§7）。旧headで補わない。HANDOFFのreview-record commitはimplementation headと区別する。
- risk: high（実Evidence初回、fail-closed保護契約の変更）。owner: OSM maintainer、主担当: Codex root。created: 2026-10-06 JST、integrated: 2026-10-07 JST、expires: 2026-11-05または置換revision。
- harvest to: `tools/Artifacts/README.md`、programの現在入口、必要な恒久契約だけ。D時に本slice HANDOFFを削除し、小ledgerをPR本文等へ引継ぐ。program本体は残す。
- A0/A1/A2原記録は手元のignored planning領域に保持。固定入力hashと全採否は独立A2欄に転記。本書が承認後の自己完結仕様候補。A3 snapshotの時刻/hashは人間承認後に固定し、この承認案のhashと混同しない。
- H1 / `external-current-v1` は今回採用しない。既存H1/H2c task/CURRENT/承認値は変更しない。従来の固定HANDOFFとGit外payload、小ledgerの規約を使う。Harness連携は後続H2d/H3。

PR #96はC/C′ GO・D完了、merge SHAは上記base、検証済みArtifact CLI headは `caed8bae55c033673b75532dc650f0a3a3cedc48`、旧Evidence baseは `fd7ebf932d230a522293dee72572cbdeeac1c8fa`。今回は前段を再開しない。新しい転送実装を旧85件/旧liveの合格で通さない。

## 1. 問い、対象外、GOの最低条件

問い: 既存形式のfindings-freeな実Evidenceを非公開領域に安全に固定し、同一Windowsユーザーの別agent sessionが信頼済み参照/hashから取得・全hash照合・必要rawログ/画像の実閲覧を行えるか。

- M1: 明示したE1/E2だけがsnapshot/実ZIP/取得entryに入り、全期待bytes/hash・対象base/headが一致する。秘密/C所見非混入の確認を送信前に行う。
- M2: private設定・全適用lock/lifecycle・writer scopeの前後観察と、同run scopeのsynthetic witnessの上書き/DELETE拒否・原hash維持、同writer unlocked対照が揃う。実Evidence keyへ二度目PUT/DELETEが無い。
- M3: publish候補と独立したledger確定・固定取得案内が成立。中断/不明/競合では確定/readyを偽らず、永続intentから残存keyと不明状態を復元できる。
- M4: 別新規sessionがpublic CLI fetchでE1/E2の全hash/entry/版を照合し、指定ログと実設定画像を実際に閲覧。人間の鍵再入力・ファイル運搬を要しない。
- M5: 新implementation headに対応する必須offline/live/rawを固定し、別担当Cとblind C′が同headのM1〜M4を判定する。

GOはM1〜M5全達・致命的反証なし。未達はNO-GO/未確認を区別し、CONDITIONAL ACCEPTは使わない。最低条件を満たしたらローカル限定first useを閉じる。転送元#96のC/C′再判定、Cloud対応、program全面採用の証明とはしない。

対象外: Cloud、実Build転送/実Build実行、H2d/H3、Unity test/終了stall再調査、token作成/rotation/TokenDelete/鍵再入力、旧Evidence/失敗raw/受信copyの移動や清掃、公開URL/domain、OneDrive、provider registry、inspect/prune、汎用Evidence schema。全体依存、asmdef、SceneState、Unity側C#、assetを変更しない。

## 2. 明示する最初の実Evidence

### E1: 既存findings-free bundleの選択済みコピー

source root: `C:\Users\void\AppData\Local\OneStarMaker\Artifacts\review-evidence\artifact-cli-final-r3-cec0e01b2bb6433e9cab782ddcc84df4`。

既存root manifest SHA-256: `b6e3dbf63c1bf925c36c966e64022bb6d1b14897f04aeb1af7af4082a2693ee4`。manifestは218 files・14,670,091 bytes、画像なし。Aでmanifest hashと次の13件のbytes/hashを実測一致確認済み。全218を今回再検証したとは記さない。

E1 evidence base=`fd7ebf932d230a522293dee72572cbdeeac1c8fa`、head=`caed8bae55c033673b75532dc650f0a3a3cedc48`。source path→payload pathは `source/<source path>`。次の13件だけを明示選択する（bytes / SHA-256）:

1. `final-offline/snapshots/A3.md` — 29410 / `31f78fdbeb01f0b82a24e3c6d4dda432f64ad83e3fd1890388f74009903faceb`
2. `final-offline/snapshots/B_RESULT.md` — 2115 / `a5a5f60e455c0852c12795d4338db4148368164b8c4297e4823a2fb8d6215d3c`
3. `final-offline/snapshots/implementation.diff` — 206304 / `e73495ccc6527639999aef6badaa1d72afd1714dfa1bb91ecc4ad84543cbe8cb`
4. `final-offline/snapshots/implementation.name-status` — 897 / `06e367fa4bbc7109842394b83e0849af6fbad021aaa4eac25009ca2575e27c13`
5. `final-offline/snapshots/implementation.stat` — 1335 / `1daa6b2d0bdd3b46bcbcbcdc905dc7f8928bea5737759ffa74b6a1f1fa8c93ca`
6. `final-offline/c-raw/ArtifactTransfer.stdout.log` — 331 / `1e34e44e63c18d707650856f8e2a5daf6ac9e745cdcbba4b80c5c9d9925538cc`
7. `final-offline/c-raw/ArtifactPackage.stdout.log` — 237 / `d9857c384c5b3ef45c4092eab5d0c91ee6ad591b2d0c74dcd2a16ec0037691ef`
8. `continuation/current-run/publish-operation/operation-observations.json` — 6366 / `b8b54e6331e933ba19d1227302b107056012c2749e022a7519b161e008649d51`
9. `continuation/current-run/publish-operation/protection-receipt.json` — 6371 / `1b1cf9d643c8f43c7cf9913ce19e203636bba70e5241da1918994c9f9d20e2c3`
10. `continuation/raw/fetch-reader/verification.json` — 2761 / `6fd72d986c5259aa4e799da5b9201288f88cba4c1942c6ba4a6949dbb89ed4dd`
11. `continuation/settings-provenance.json` — 3195 / `971c93075eb097755c60f3a5db4480ae15f9ade2f90601fefd74d1a33bdd950a`
12. `continuation/observations/bucket-before.json` — 1174 / `cd6a4fd59f533cee313460c67de895c409143e56bae0dcee7ff15061770ae200`
13. `continuation/observations/bucket-after.json` — 1131 / `03e5a12f96bd7383b46ac03dd4f480b0d997a796d307c65924bccf04f229c99a`

追加は既存root manifestの原bytesを `source/manifest.json`（上記期待hash）へコピーし、所見なし機械照合 `selection-receipt.json` を生成するだけ。計15 input files。receiptは選択13件の期待/実測hash/bytes、path mapping、source manifest hash、evidence base/head、取得時刻だけ。source manifestに載る非選択fileは参照情報であって転送済みとは扱わない。既存root manifestや原bundleを編集しない。

### E2: 今回の保護設定の一次画像観察

E1に画像が無いため、現在の設定観察を**別package**にする。E2 evidence baseは本slice implementation base、headはB完了の新implementation head。E1に現在の観察を混ぜない。

E2 inputは `settings-before.json`、`settings-overview.jpg`、`settings-rules.jpg`、`capture.json` の4件。JPEGはbrowser toolが返す原bytesを保存し、生成画像・合成・注釈・PNGへの変換はしない。overviewはbucket名/private URL/domain、rulesは対象lockとlifecycleの見える画面。captureは実装head、対象bucket/page、時刻、観察者/tool、2画像のbytes/hash、観察範囲のみ。accountメール/認証情報/他bucketを避けた画面領域を保存する。画像から見えないwriter権限や全ruleはJSON原記録で照合する。

Cが採取して4件の期待hashを送信前に固定する。settings-before.jsonは全適用ruleの明細（空prefix、親prefix、exact key、無効ruleを区別）、private設定、writer scope、lifecycle、観察時刻を持つ。C結論・A2所見は書かない。原記録からstrict configに必要な閉じた設定要約を生成し、要約が原記録と一致する機械記録を外側へ保存する。

### findings-freeと秘密の境界

明示一覧だけを使い再帰収集しない。任意private log、credentials/grant/.env、C所見/結論、A2所見、可変HANDOFF/CURRENT、PR本文をpayloadへ混ぜない。B_RESULT/一次観察へC所見を転載しない。送信前にAIが選択text/JSON/画像の内容を確認する。purpose宣言やgrep/hash一致だけで内容の秘密/C非混入を証明したとは扱わない。秘密や所見を見つけたら値を出さず停止、元証拠を編集しない。選択変更は新A revision。

内側の既存review evidence形式とbytesを保持。外側は既存manifest.json+payload/のpackage構造を維持し、purpose=`evidence`と30日retentionの閉じた許可を追加する。outer manifest hashとsource manifest hashを分ける。E1は前段全判定bundleの代替でも新headのテスト証拠でもない。

## 3. 実Evidenceを破壊しない保護フロー

実prefix **`evidence/first-use/`**、private bucket `osm-artifacts`、age lock **30日=2,592,000秒** 1 rule。既存 `probe/locked/` の1日ruleを変更しない。実prefixにDate/indefinite/重複rule、object expiration/transitionを認めない。lifecycleは既存multipart abort7日のみ。writerは同bucket限定Object Read & Write・管理権限なし。

repositoryIdは既存 `4d2d1728f1db77f50605a10da0bbf10fd31443c381c4f312658134fffb850b3b`。

- bundle: `evidence/first-use/<repositoryId64>/<evidenceHead40>/<runId32>/bundle.zip`
- synthetic witness: **同じrun directory** の `protection-witness.txt`
- unlocked対照: `probe/unlocked/<operationId32>/control.txt`

E1/E2ごとに新規run/keyを使う。初回は2publishだけ。失敗後の別run追加は記録した原因/修正/再試行範囲に根拠がある場合だけ。成功runを反復して増やさない。

順序と失敗境界:

1. private設定・全適用rule明細・writer・lifecycleとstrict configを照合。config原記録hash、UTC observedAt/validUntil（最大24h）、purpose/prefix/retention/identityが一致しない場合は通信前に拒否。
2. EvidenceApplicationがprivate operation内の明示mapped sourceをread lockでsnapshotし期待bytes/hashを照合、selection receiptを生成。Packagingが同operationの確認済みsnapshotからZIPをCreateNewで一度作り、実manifest/entriesと明示期待集合を再照合。以後は固定ZIPだけをread lockで渡す。
3. ネットワーク前に `intent.json` をCreateNew+flushで固定。operation/run、bundle/witness/controlの正確なkey、package/manifest hash、generation、config hash、producer/evidence各base/headを記録。保存失敗なら通信しない。
4. synthetic witnessだけにPUT→別process GET/hash→異なるbytes PUTのlock拒否→DELETEのlock拒否→別process GETで原bytes/hash維持。
5. 同writer/generationのunlocked対照をPUT→GET/hash→DELETE→404/NoSuchKey。単なるwriter削除権限不足をlock成立にしない。失敗なら実Evidence PUTへ進まない。
6. 実bundle keyの不存在（404/NoSuchKey）を観測。直前に `put-intent.json` をCreateNew+flushで永続化し、以後はmay-have-been-sent（送信されていなくても不明）とする。この境界の保存失敗なら送信しない。固定ZIPのhashを再照合し、**一回だけPUT**。結果不明なら同keyへ再送しない。
7. 実keyは別process **GETだけ** で全bytes/hash照合。witnessもGETで再確認。実keyにoverwrite/DELETE/cleanupを送らない。transportはEvidence bundle DELETEを拒否し、Protectionは破壊操作対象をwitness exact keyだけに限定する。
8. publishは非秘密candidateだけ返す。C/別のledger orchestrationが設定事後原記録を取得し、前後それぞれのhashを固定して**保護内容**を比較する（日時を含む全bytes一致を要求しない）。不一致ならledger確定不可。

lock成功tupleは同応答の403/forbidden/ObjectLockedByBucketPolicy または409/other/ObjectLockedByBucketPolicy、同generation、timeout/redirectなし、原witness bytes/hash維持。一般401/403、unknown、missing、SDK例外本文をlock成功へ変更しない。例外本文EOFを実測したとは扱わない。

同runのprefix policy実効性＋全rule/前後設定観察＋実key読戻しによる限定証拠。途中admin変更、悪意ある同Windowsユーザー、不存在GETとPUT間の競合を隔離する保証はない。新しいrandom128bit keyとlockを使い、conditional PUTや新管理APIは今回追加しない。[Bucket Locks](https://developers.cloudflare.com/r2/buckets/bucket-locks/)のprefix適用契約に基づく推論であり、実Evidenceへ破壊試行した証拠ではない。

## 4. 保持と残存物

server lockの保護下限はPUT-intentの通信前時刻+30日（`serverLockLowerBound`）。reference既存`retainUntil`もこの下限だけを表す。実server作成時刻の正確値や、Evidence削除期限と扱わない。

保存義務は**このfirst-use sliceをownerがDで閉じた時刻+30日以上、かつ参照継続中は保持**。ledgerには保存義務ruleを固定し、close前の`closedAt/retentionObligationUntil`はnull（未確定）にする。Dではownerの明示closeイベントからappend-onlyの別保持record（ledger ID/hash、closedAt、最短保存日、consumer参照、owner）をCreateNewで固定し、元ledgerを更新しない。close前や参照状況不明では清掃不可。

**30日server lockはclosure+30日/長期参照の全期間保護を保証しない。** 保存義務はlock expiry後も続く。自動削除なし。初回runの前からこの差をownerに提示し、OSM maintainerへ各keyとserverLockLowerBoundを引き渡す。期限後の継続運用/延長はArtifacts保持延長スライスの新Aへ送り、fresh config/profile移行もそこで定める。初期policy不変時のfetchだけを今回保証し、延長後のfetch成立を無条件には主張しない。この残存リスクを人間が受容しない場合は、本案を凍結せず期間/lock方式を新revisionで決める。

source原本、既存受信copy、69/146/218-file bundle、原失敗/旧NO-GO、旧locked/control objectsは全て保持。旧証拠の最短保持日は2026-11-04T14:31:01.6611325Z、参照中はさらに保持。新sender snapshot/intent/result/config/原観察/reader ready/receiptも保持。初回sliceでは実Evidenceとlocked witnessを削除しない。witnessは後続の明示清掃まで残る。

失敗はledger/outputPath=null、実keyとlocal path/PUT意図境界/保持下限/不明状態をresidueへ残す。強制終了ではfinallyに頼らずintentから復元し、自動再送/自動復旧コマンドは作らない。PUT前で止まったか、境界後may-have-been-sentかを区別する。新unlocked controlだけ、期限内・子process/pipe停止確認済みならDELETE→NoSuchKeyまで行える。停止不明なら競合cleanupせず保持。既存key/prefix/他task/credentialを清掃しない。将来のexpired・unreferenced対象key清掃はownerの明示承認とprune後続。

## 5. 独立ledger確定とreaderの信頼起点

publishはGit/HANDOFF/PR/最終ledgerを更新しない。exit0はcandidate生成まで、exit1はfailed/unconfirmed・candidate null。result schema v1を維持し、candidateにopaque reference、key、package bytes/SHA-256、outer manifest SHA-256、evidence base/head、producer implementation base/head、run、source manifest hash（E1）、selection receipt hash、protection receipt hash、serverLockLowerBoundを追加する。秘密/署名URL/SDK型を上位に出さない。

新しい公開入口は `pwsh tools/artifacts.ps1 evidence publish --profile osm --config <absolute> --selection <absolute> --base <evidence-base> --head <evidence-head>` と `evidence commit --candidate <absolute-result> --candidate-sha256 <trusted64> --settings-after <absolute> --settings-after-sha256 <trusted64>`。既存synthetic publish/fetch/credential grammarは維持。Evidence publishは内容確認済みの明示selection metadataを受け、任意rootのsynthetic宣言で実payloadを通さない。commitはローカルだけ。

EvidenceLedgerはcandidate/result/intent/receipt/設定前後原記録のhash、各identity、単一PUT/witness/control/entry集合を再照合して、Known Folder `OneStarMaker/Artifacts/evidence-ledgers/<ledgerId32>/ledger.json` に固定する。filesystemは新規 `EvidencePaths.psm1` が専用root、reparse拒否、owner/SYSTEM/Administrators限定ACL、CreateNew・flush・同directory atomic renameを所有。transfersだけを受ける既存path APIへ無理な例外を作らない。既存ledger置換を拒否。途中write/rename失敗はpendingでledger確定扱いしない。networkもcredentialsもledger moduleへ入れない。

**信頼起点の所有者:** publisherと別の引渡し担当（C/親orchestration）がledger確定後、reader開始前に `OneStarMaker/Artifacts/evidence-handoffs/<handoffId32>/reader-input.json` を同ACL/CreateNewで固定。案内hashを新規reader sessionの開始promptへ明示的に渡す。案内を読取時に可変ledgerから作り直さない。

案内にはE1/E2各ledger path/hash、reference/key、期待package SHA-256/bytes、outer manifest hash、source manifest hash、evidence base/head、producer head、config path/hash、保持rule/owner、開く相対pathだけを記録。C所見/疑念候補/可変HANDOFF/PRを含めない。readerは**案内bytes→prompt期待hash、ledger bytes→案内期待hash、全identity/hash→案内/ledger/reference一致**の順で照合してからfetch。差替え/不一致では期待hashを取り直して続行しない。期待package hashをdownloadやopaque referenceから補完しない。同Windowsユーザーからの悪意ある改変に耐える署名基盤は保証しない。

Gitへ大payload/生成manifest/rawを追加しない。小さいkey/hash/base/head/保持/取得手順のledgerだけを進行HANDOFF、D時はPR本文へ引継ぐ。blind reader/C′には独立案内を渡す。C/C′の判断はledger/payload/reader receiptの外に隔離する。

## 6. 別session fetch・閲覧と経路の状態

fresh configは最大24hの新しい読取観察を持ち、endpoint/bucket/repository/prefix/retentionというprofile identityを維持する。日時/原記録hash更新はidentityを変更しない。期限切れpublish configや前段設定をそのまま使わない。policy変更後のconfig migrationは§4の後続。鍵値/暗号文は転送しない。

新規reader agent sessionは同Windowsユーザー/マシンで開始、sender source/stagingを直接読まない。案内の照合後、各packageについて:

`pwsh tools/artifacts.ps1 fetch --profile osm --config <fresh-absolute-config> --reference <trusted-reference> --sha256 <ledger-expected-package-sha256>`

public fetchは公開CLIという意味であり匿名bucket公開ではない。exit0、全archive hash、outer manifest hash、対象版、全entry path/bytes/hash、期待entry集合、private新規readyの安全展開を確認。manifest hashは案内/ledger/opaque内の一致も確認し、不一致なら通信前に止める。source manifestのhashはreadyコピーから独立照合する。

E1 readyの `source/final-offline/c-raw/ArtifactTransfer.stdout.log` と `source/continuation/current-run/publish-operation/operation-observations.json` を実readし、件数/phase/status等の見えた事実を所見なしreader receiptへ保存。E2 readyのJPEG2枚を `view_image` 等の実local画像toolで開き、private設定/lock prefixと期間/lifecycleの見えた範囲を記録。hashだけを目視済みとしない。取得script/DLLは実行しない。

receiptはsession/model/tool、時刻、対象key/hash/head、読んだpath/hash、見えた事実、未確認だけ。C判定とは別に固定し、M4の一次観察としてC/C′へ渡す。

**Aで確認済み:** public credentials statusはactive `3068547bc9524f59b33c24ae9e3c4e9e`、stable、retiredなし（local-only）。既存manifest/13選択fileのhash照合。既存ログイン済みbrowserからosm-artifacts Settingsへ到達し、development URL無効/custom domainなし、probe_locked 1日1件、multipart abort7日1件をread-only AX観察。browser screenshot取得・表示まで成立。browserの原画像形式はJPEGでありA1のPNG案を修正した。設定/object/credential変更なし。

**未確認。初回確認はPhase C:** 新first-use rule（未作成）、画像原bytesのprivate保存→local画像tool、Evidence modeのwitness、実Evidence publish、別session public fetch→readyから画像閲覧。Bは保存/path/selection等のoffline支援を完成し、Cは**実Evidence送信前**に現在の設定画像をprivate保存しlocal toolで開く小さい疎通と新scopeのsynthetic witness/controlを確認する。任意の新capture支援が必要なら§7の依存境界内に限りBへ返す。できなければ未達で停止、反復的な人間ファイル運搬・鍵再入力を代替にしない。

設定のowner手順（A3承認済み、Phase Cで実行）: Cloudflare R2→`osm-artifacts`→Settings→Bucket Lock Rules→Addでprefix `evidence/first-use/`、Age30日、enabled、1件を追加。既存probe rule/URL/domain/lifecycleは変えない。通常writerは設定権限なしのまま、account ownerが設定操作を所有する。A3に明示された対象のbrowser操作はAIが代行可能、ログイン要求が出ればownerがログインだけを担当。現在はログイン済みなので、新login/token/key操作は依頼しない。設定前後全ruleをAIが記録し、影響が明示範囲と異なる場合は保存前に止める。

## Phase A設計: 責務配置・規模・検証

配置は全て `tools/Artifacts` と外部entry point。Game/Framework/Runtime/Editor/asmdef/Unity/Build依存は追加しない。state/resourceは実行Windowsユーザーが所有、転送operationはManual lifetime、immutable ledger/handoff/retention recordは別の保持寿命。資格情報は既存storeだけが所有し、Packaging/Evidence selection/Ledgerは鍵を見ない。

- `tools/artifacts.ps1` 現63行、+25〜40: grammar dispatch。Evidence publish/commitを呼び、結果を出すだけ。
- `ArtifactCommands.psm1` 現151行、+30〜50: strictJSON/referenceの既存codec。新規 `ArtifactProtectionPolicy.psm1` 120〜180: synthetic86400 / Evidence2592000の閉じたpurpose/prefix/key/config組と全rule照合、pure policy。policy/codecの変更理由とtest境界を分離、自由prefixや汎用provider選択を追加しない。
- `ArtifactApplication.psm1` 現431行、+30〜60以内: 共通fetch/固定package転送。`Invoke-ArtifactPublishPrepared`を**限定export**し、EvidenceApplicationが一方向にimportする（公開CLIから任意prepared pathを受けない）。operation ID/root、固定ZIPの正確path、期待bytes/hash/manifest/entry集合を受け、operation内/ACL/reparseとZIP全bytesを入口で再照合し、source再snapshotはしない。「内部」はtrusted orchestrationの境界であり非exportのPS scopeを意味しない。新規 `ArtifactProtection.psm1`180〜260: intent・witness/control・一回PUTの順序、bounded transport、candidate。Protection→policy/transport/credential callback、Application→Protection、逆importなし。500行警報時は分割理由をB結果に説明。
- 新規 `EvidenceApplication.psm1`160〜240: selection/input provenance/content確認receiptとmapped snapshotの所有者。`PackageIO.CreateVerified`をPowerShellから呼び、**同operation**のsnapshot/期待一覧からZIPを作り、Applicationへ固定ZIPを渡す。transfers内任意rootの公開入力を許可しない。選択I/Oとpure集合比較は同module内で注入可能な別関数。
- `Packaging/PackageIO.cs`現271行、+50〜90、`PackagePolicy.cs`現122行、+20〜40: existing schemaのpurpose/retentionとchecked snapshotのZIP/全entry集合照合。public synthetic Create/Extract互換。`CreateVerified`はC# assembly上では **public static** とする（C# internalをPowerShellから呼ぶreflection迂回はしない）。同operation root、snapshot path、固定ZIP path、expected path/bytes/hashを受け、root逸脱/reparseと実bytesを自身で検査する。CLI grammarには公開しない。PackagingはEvidence分類/所見判断/networkを知らない。
- 新規 `ArtifactAcl.psm1`約75〜110: 現行ArtifactPathsのACL作成/検査/設定原始操作だけをbehavior不変で移し、狭いexportを提供。ArtifactPaths/EvidencePaths→ArtifactAclの一方向、逆importなし。同じACL契約の二重実装を作らない。
- 新規 `EvidencePaths.psm1`130〜200: Known Folderのevidence-ledgers/evidence-handoffs/retention専用領域とACL/reparse/原子的file確定。root名は閉じたenum相当で任意pathを公開しない。既存 `ArtifactPaths.psm1`現136行はtransferだけを維持し、ACL原始操作の移動とimport以外は変えない。ACL mutatorを呼ぶ前に呼出元Pathsが専用rootと自分の新規childを照合する。既存共有親や既存異常ACLを修復しない。
- 新規 `EvidenceLedger.psm1`140〜240: candidate/provenanceの検証とcommit状態・immutable ledger/reader案内/close保持record。filesystemはEvidencePaths、network/packagingなし。ledgerはpublisherとは別の明示commit操作で確定。D用に `Write-EvidenceCloseRecord` を限定export（製品CLI commandは増やさない）。引数は両ledger ID/期待hash、ownerの明示close eventの固定JSON path/期待hash。eventにはowner、両ledger ID/hash、UTC closedAt、consumer参照state、ownerのclose指示を取得した参照を記録する。Ledgerがidentity/時刻/owner/参照を検証し、closedAt+30日を算出、Pathsが一度だけCreateNew/flush/rename。重複closeは拒否、partialwriteは未確定。Bはdummy eventで検査、実行はownerがD closeを明示した後だけ。
- `Transport/R2ArtifactTransport.psm1`現35行、+20〜35、`ArtifactReadback.ps1`現59行、+15〜25: Evidence bundle/witness exact keyとGET許可。`R2ArtifactTransport.cs`現158行、+15〜30: real bundle DELETEを防御し、S3 I/O/固定bucket/非秘密DTOだけ。SDK型を上位へ出さない。実keyPUT回数はProtectionで制御、transportは一般的なone-shot request。
- 新規 `tests/ArtifactEvidence.Tests.ps1`200〜350、既存Package144行/Transfer244行・Transport/Rotation/Credentialsの必要case。fake network記録から実keyPUT最大1/DELETE0、witnessだけ破壊することを確認。Ledger/Paths/Package policyはdummy bytes・隔離ACL root・注入filesystemでUnity/R2なしの単体検証。
- README current harvestはC/C′達成後。browser画像採取はPhase担当toolのorchestrationであり、製品CLIへbrowser/Unity依存を追加しない。新汎用Helpers/Managersは作らない。

発見/差し戻しの起点はEvidence suiteと影響Package/Transfer/Transport suite。原因と凍結条件を根拠にCが選択/追加できる。B完了引渡しで関連offlineを実行（H1適用とは扱わない）、live/Unity/実Buildなし。

判定Cの最終GO候補head必須: Artifacts全6既存suite（Credentials/RouteProof/R2RouteTransport/ArtifactPackage/ArtifactTransfer/ArtifactRotation）+ArtifactEvidence suite、3 .NET build、PowerShell parse、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、`git diff --check`、M1〜M4のlive。command/全case名・件数・failed・実head・DLL path/hash/MVID/依存identityとrawを固定。旧85件を新headの結果としない。

全EditMode適用除外: 変更は外部Artifacts CLIだけでUnity assembly/runner/Harness/assetへ波及なし。代替証拠は全Artifacts offline+3build+R2/別sessionの操作観察。Unity test/Build/終了調査は今回一切起動しない。

offline failure cases: strict期限/hash/identity/全rule/purpose組、余剰/欠落/改変entry、snapshot→ZIP→PUTの同bytes、witnessのtuple/原hash/control失敗、実key存在、intent保存失敗・intent後/send前/send後/response前/candidate前中断、PUT不明/GET不一致/post設定不一致、ledger partialwrite/rename/競合、ledger/案内単独差替え/identity不一致/期待hash再採取の拒否、fetch path/size/manifest/hash/entry、dummy secretsのstdout/stderr/receipt非露出。テストでTask.Delay/Thread.Sleepを使わない。

上限は既存のZIP256MiB、JSON1MiB、4096entry、単一256MiB/総1GiB/圧縮比100、operation10分/network120秒、retry0/redirect拒否を維持。child終了/pipe停止未確認なら競合回復通信しない。秘密値/HTTP本文/SDK例外文字列は出さない。

## Phase A独立A2と採否（人間承認済み）

共通固定A0 SHA-256=`05ab7283a20f7629bc2f6086be66738ca082b611a2b773aba6ece93122409a7e`、A1=`5a40a6b5c1007e692f1b65f2f4f9c62237025f09cebbc32d99e54f455341802b`。architectureとprotectionは同bytes。A0-only代替はA1を見ていない。3担当は新規forkなしcontext、互いの所見未閲覧。主担当のみ統合した。

モデル指定（dispatch metadata）: architecture=`gpt-6-sol`、protection=`gpt-6-astra`、A0-only=`gpt-6-luna`。agent内部のvariant実測は不可、同OpenAI/GPT系列という多様性制約を明記。rootのvariantも実測不可。protection所見初稿保存後、rootからread-only UI到達の観測補足だけを受領、他review所見は渡していない。PNGというrootの補足表記はJPEGへ訂正し、rawレビューを成功証拠として書換えない。

- ARC-1 High、採用: mapped snapshotからZIP/PUTへ渡すprepared境界を保護フロー/責務配置へ固定。公開任意transfers root許可は不採用。M1の同bytes検査を追加。
- ARC-2 Medium、採用: Ledgerの専用filesystem所有者をEvidencePathsへ分離。既存transfers専用APIの暗黙流用はしない。M3の部分write/rename/競合を検査。
- ARC-3 Medium、採用: serverLockLowerBoundとclose後のretentionObligationUntilを分離、owner D closeとappend-only保持recordを固定。永久server保護の主張を除いた。
- PRO-1 P1、採用: network前intentと実PUT前の不可逆intentを永続化。finallyだけに依存せず、中断keyとmay-have-been-sentを保持。M3 failure注入を追加、自動再送/復旧は追加しない。
- PRO-2 P2、採用: 別引渡し担当・immutable reader案内・prompt期待hash・reader最初のledger照合順序を§5へ固定。producer/受信直前の可変bytesから期待hash再採取は拒否。
- PRO-3 P2、保留（後続入力）、理由: 初期policy不変のfirst-use問いは阻害しない。延長後strict profile/config移行をArtifacts保持延長へ送る。期限とowner引渡し・server期間差の明記は今回採用し、将来fetch成立を誇張しない。
- protection補足、採用: all-rule原明細、空/親/個別prefix照合、前後内容比較、初回Cの画像保存/閲覧preflightを明記。新管理APIは追加しない。
- A0-onlyの明示入力/原source manifest保持/独立ledger/同scope witness/別session実閲覧/清掃分離、採用（ARC/PROとのduplicateは上記へ統合）。
- A0-onlyの実object S3 retention metadata/HeadObjectを必須にする案、不採用: R2 Bucket LockをS3 Object Lockと同一視しない。公式[S3互換表](https://developers.cloudflare.com/r2/api/s3/api/)でObject Lock機能は未実装。実objectの正確retainUntilを推測しない。全prefix規則＋witness実効性の限定証拠を採用。代替案でDELETE拒否が落ちた部分はM2を維持する。
- A1のPNG/複数画像合成、変更: browser toolの原JPEG2枚をE2へ収録。無関係な画像生成や新加工支援を追加しない。E1/E2の版分離を維持。
- Harness移行、provider追加、Cloud/Build/inspect/prune、未参照cleanup自動化、保留: 各後続が所有、現在の最低条件へ追加しない。

限定follow-upは統合前案hash `e20345e89b75967c14352c900c30f369afd358837fdcfa8b7675d9b3a7ae9066` を再確認した非盲検であり、独立A2件数へ加算しない。原報告は保持。architecture follow-up FU-1 High（PS export/C# public可視性）、FU-2 Medium（共通ACL供給）、FU-3 Medium（D close入口）は全て採用し、上の責務配置に具体化した。protection follow-upはPRO-1/2の設計上解消を確認し、finite30日案は人間の期間差リスク受容付きなら維持可能と評価した。無期限lockは期間差を減らす代替だが、reference/configのnullable期限表現とwitness無期限残存の新判断を伴うため現在案へ暗黙採用しない。人間が保存義務全期間のserver保護を求めた場合に限り新A revisionへ戻す。実装/liveの合格確認ではない。

原review hash: architecture `dbf6b69b786b64bf741e6736006502942210a7089d07a38793898b60705d9856`、protection `599b220f0f3734142c8be94aa7b14f3472826622253bf66104ad40a715d74c1d`、A0-only代替 `ae653cbad91881a87e831423eafb6afb5a7514f8b9d2858481c883458fa2ab92`。明示入力13件のA実照合JSON hash=`f310cd2ecd945624a9f4246afe869cf6aaa4ed802e8de25bedf96a1cd3fe905b`。これらはA計画/レビュー記録、C′共有payloadには入れない。

C′候補はPhase A未関与 `gpt-5.6-sol` を予約、B/Cとも異なるmodel・新規session・同headのblind bundleでのみ実施。主担当variantが未確定なのでB/C開始時に担当名を実績として確定し、違いを満たせなければ別担当/人間へ返す。同ベンダー系列という強化独立性制約は人間が採否を判断する。予約をC′実施済みと書かない。

follow-up原記録hashはarchitecture `3efdf4b60a320d49a56c87f2d33ca8b0eeaaac2ae26c737e5eb175a1dbae6d84`、protection `79f29a7d1476dbfeb8896e9bca1a307e22ef6613a7efb9175509ce7d76fd490a`。FU-1〜3の変更は主担当が言語/module可視性とentry契約に照合して反映。未関与の追加盲検レビューがこの最終本文を承認したという主張はしない。

**C/C′入力の準備:** A3承認後、採用した技術仕様（目的/条件、明示入力、責務、保護/保持/ledger/reader/test/停止契約）だけをfixed Phase A specificationへ抽出し、そのpath/id・生成UTC・hashを別manifestに固定する。本書のA2所見/採否や可変進行欄はblind入力へ収録しない。抽出で規範を変更/欠落させず、承認本文と対応を機械照合する。判定C前に同一new implementation base/headの完全diff/stat/name-status、findings-free B result、全必須offline/build/audit raw、settings前後原記録、intent/operation/保護/selection原記録、固定ledger/reader案内、別session fetchとログ/画像の一次閲覧receipt・受信画像を、新規private `review-evidence/evidence-first-use-judgment-<id32>/` の共通findings-free bundleへ固定し、全fileのbytes/hashと案内を記録する。C/C′はこの同bundleを使う。E1の15file選択packageを、この新headの完全judgment bundleと取り違えない。C結論は共通bundleの外に保存し、C′へC結論/疑念/A2報告/可変本書を渡さない。受け渡し先コピーで全hashと必須rawの読取を確認する。C′の同じテスト/live重複実行は要求しない。

## Phase A3: 人間に必要な判断と停止規則

A3承認対象は、E1の15file/E2の原JPEG観察4fileという最初の入力と版分離、30日age lockの実prefix追加、参照中の保存義務とserver期間差の限定受容、設定追加のowner/AI責務、独立ledger/reader信頼起点、H1移行なし、上記A2採否である。特に30日を超えるserver保護/延長後fetchは後続であり、無期限保護の条件に読み替えない。2026-10-07 JST、人間の「PhaseA3凍結でOK、PhaseC/C′まで進めて」で承認対象を凍結。BとC/C′を実施する。D close/mergeは未承認。

既存token/鍵の再入力・削除を求めない。現在のstable osmを使い、資格情報不備が判明しても自動set/remove/rotationで回避しない。具体的な問題と必要操作を整理して返す。

凍結後、新しいAPI/状態/責務/依存/fail-closed変更、秘密/所見混入、保護不成立、設定影響の拡大、最初の入力変更、画像経路の不成立で条件変更が必要ならAを新revisionで再開。境界内の支援不備/実装不具合はB適応。未知は小さいspikeで条件と停止地点を記録し、実Evidenceの再PUT/DELETE・Unitystall再調査はしない。

ここで答えない問いの所有先: program Cloud段=platformごとのegress/grant/read/write/閲覧、program Build段=実Build/multipart/容量費用、Harness H2d/H3=CURRENT/backup/別host、Artifacts保持延長=参照長期化時のlock方式/read policy migration、Artifacts操作性=inspect/prune。A3後の例外承認はなし。

## Phase B: 実装完了・発見Cへ提出

新規sessionのB担当 `gpt-6-sol` が凍結仕様を実装。15 code filesを `042dd8bcd86b264dbb3a870ff59be4f260a0d67c` へ固定。7 offline suitesは99 registered/selected/executed、failed0、3 .NET builds成功、PowerShell parse 26 files/errors0、contract/docs audit errors0/warnings0、diff check成功。findings-free B result SHA-256=`10e8444cd2992d49eb521a50df43b3eb9dc7945438b37b6138a9eb80b167794f`。Bではreal R2/設定/token/Unity/Build操作なし。B resultと原rawは手元に保持し、判定bundleへ固定コピーする。

rootおよびCは原JPEG保存→保存済みbytesの画像tool閲覧preflightを確認（設定/object変更なし）。Cのbrowser接続は利用不可のため、設定UI原観察はrootが代行し、判定C担当と原観察者を区別する。CはE1の明示13file/source manifestを全文内容確認・hash照合。B/Cのモデル相違を満たす。C′は未関与 `gpt-5.6-sol` の新規sessionを予約し、判定raw完備後のみ開始する。旧#96の完了をこのsliceの完了として扱わない。

## 7. Phase C

新規session `gpt-6-astra` が固定head `042dd8bcd86b264dbb3a870ff59be4f260a0d67c` の発見Cを実施、B差戻し5件。構造・契約・凍結した失敗経路を先行確認。判定一式/live未開始、GO判定なし。隔離所見 SHA-256=`89601150501901b7433a4754c88f056ae0444f6bfe8fba1d7b3e6c3325913986`。C所見は共通findings-free payloadの外へ保持。

- D1 P1、現在の問いを阻害、採用: 独立ledgerがtimeout lockとcontrol/bundle GET誤hashをhashchain整合後に受理するダミー再現。§3/§5のidentity・tuple・bytes/hash再照合違反。
- D2 P2、現在の問いを阻害、採用: 2020年のafter観察を受理するダミー再現。M2/§3/§6のfreshかつ事後観察違反。
- D3 P1、現在の問いを阻害、採用: expected1byteの入力から2048byteを全copyしてから拒否、JPEGもactual size前の全量読取。§7の上限/共有10分予算違反。
- D4 P2、現在の問いを阻害、採用: strict captureが2画像bytes/観察範囲を表現できない。§2のE2固定内容違反。
- D5 P2、現在の問いを阻害、採用: §7のintent永続化/中断、ledger partialwrite/rename/競合、ledger/案内identity単独差替え、dummy秘密非露出のoffline検証不足。

全5件を凍結済み責務・契約内のB適応へ戻した。A再開/受け入れ条件追加なし。旧head/rawは保持し、新しい固定headで発見Cを再確認する。C′は未開始。

B修正後の提出: `896eaea8a912248333704c80549d46ecf20c02c6`、7 offline suites 119 registered/selected/executed、failed0（Evidence34件）、3build/audits成功。ledgerの13観察再照合、fresh事後時刻、bounded snapshot/JPEG、capture bytes/scope、isolated障害testを同責務内へ実装。新rawを別領域に固定、旧99件を上書きしていない。旧headはlocal ref `refs/codex/evidence-first-use/discovery-042dd8b` にも保持。

修正後の発見Cは5件の解消と追加blockerなしを確認。判定用offlineを同headで実施し、7suite119/119、3build、entryを含むPowerShell26file parse、contract/docs/diffcheckを通過。最終DLLのhash/MVID/依存identityを固定し、build後のDLL使用5suite71/71も通過、DLL hash不変を確認。offline manifest SHA-256=`bf5f5c714eb83e5dfa7c02d5de6f6bbadc8eb646d415e75b222cfd33515441f5`。

rootが承認済みの `osm-artifacts` Settingsへ `evidence_first_use` / `evidence/first-use/` / Age30日 / Enabledを1件追加し、保存済み表を確認。既存probe1日・multipart abort7日・private URL/domain設定は維持。既存writerは同bucket限定Object Read & Write / Activeを再観察、token/鍵操作なし。原JPEG2枚を保存しroot/Cが保存bytesをlocal image toolで実閲覧。画像の省略prefix/abort日数はAXの全明細JSONで補完し、画像だけで全文確認したとは扱わない。

送信前のprivate input rootは `C:\Users\void\AppData\Local\OneStarMaker\Artifacts\transfers\f973c7b7f44d41939ae198e42d6ffb72`。fresh config SHA-256=`42e449b1b70d33abf3d3a534c87fd0065f3e7a933aaf4f071669d4bbfdf57236`、E1selection=`a12cdd8e2a73a98e9f65d151cd3eb0bbdafb546735cd1772354508fc236bc30c`、E2selection=`37d432934bbe092f76bbde09d6c9dd8d1ebe0746360d330634582b60419840cf`。private移管は原bytesの無変換copy、hash/bytes一致。E1全文内容確認と全固定hash、E2 text全文/原画像内容確認を完了。

送信承認前の到達点（履歴）: Cのpublishとrootの承認根拠付き再審査は、どちらもprocess起動前にautomatic approval reviewが拒否。理由は一般的なPhase C/C′継続承認だけでは、内部Evidenceの外部Cloudflare R2へのexact payload/destinationの個別承認を確認できないため。E1/E2明示入力を既存private `osm-artifacts/evidence/first-use/` へ各1回送信する限定承認を人間へ提示して停止した。代替経路での迂回なし。その時点ではlive witness、real publish、事後設定、ledger、別reader、共通判定bundle、C/C′最終判定は未確認、設定・新入力・旧証拠を保持した。

その後人間が「承認します で大丈夫？」と、提示した固定E1/E2→既存private `osm-artifacts/evidence/first-use/` 各1回の送信を明示承認。rootは承認受領を回答し、同じautomatic approval review経路で許可された実送信を再開した。先の拒否は実行前で、remote intent/objectを生成していない。

同headでE1の1publishはexit0。E2最初の起動はsourceがtransfers配下にあるため通信前・operation root未作成で拒否。凍結済みsource境界に沿って同一4input bytesを新private review-evidence子へコピーし、configのsettings pathとselection rootだけを対応固定。元入力/失敗rawを保持、共有親ACLは変更せずallowed SID/ownerを読取確認し新子だけprotected。実装変更/新head/受け入れ条件変更は不要。修正後E2の初回実送信はexit0。各run13観察はwitness異bytesPUT/DELETEとも409/other/ObjectLockedByBucketPolicy、timeout/redirectなし、原witness49byte/hash維持。同writerのunlocked対照はPUT/GET/hash/DELETE/404NoSuchKey。real bundleは各1PUTと別processGET/hashのみ、再PUT/DELETEなし。

rootがE1完了後22:15:25Z、E2完了後22:17:22ZにSettings/writerをreloadし、全private/lock/lifecycle/scopeの保護内容不変を原JSONに固定。独立した公開CLI commitは各exit0、2ledgerをCreateNew/atomic確定。owner=`OSM maintainer`、closedAt/retentionObligationUntilはnullのまま。D close+30日以上/参照中保持、server下限は削除期限ではない。

- E1 ledger: `C:\Users\void\AppData\Local\OneStarMaker\Artifacts\evidence-ledgers\8dd797d6e2f14258978185eef15dade2\ledger.json`、SHA-256=`3519e864aba787b9fb370fbf6a748adb410be5549ebd294d604bfcd5d2a20357`。key=`evidence/first-use/4d2d1728f1db77f50605a10da0bbf10fd31443c381c4f312658134fffb850b3b/caed8bae55c033673b75532dc650f0a3a3cedc48/64c42fc4d5d24fa580af7cc12d3a3c92/bundle.zip`。316952bytes、15entries、serverLockLowerBound=`2026-11-05T22:13:42.5023602+00:00`。
- E2 ledger: `C:\Users\void\AppData\Local\OneStarMaker\Artifacts\evidence-ledgers\767d073dec92416281d9b03f7e58f527\ledger.json`、SHA-256=`5d96b17b3cb0deddd0f59fa7c09ef8aa27c247f38d20ffe6e8d4e4f87a759977`。key=`evidence/first-use/4d2d1728f1db77f50605a10da0bbf10fd31443c381c4f312658134fffb850b3b/896eaea8a912248333704c80549d46ecf20c02c6/1495786d921949fca6e92edbef21010e/bundle.zip`。112718bytes、4entries、serverLockLowerBound=`2026-11-05T22:16:35.3325043+00:00`。

publisherとは別のrootがimmutable reader案内を生成: `C:\Users\void\AppData\Local\OneStarMaker\Artifacts\evidence-handoffs\d79fb66106844337b4c1a0f0c311d2e6\reader-input.json`、SHA-256=`32311d1cbb87c067f22e4e033c4c3577b0df866d8db86411651362fc22ea5f92`。生成=`2026-10-06T22:19:07Z`。fresh reader config SHA-256=`96b1540a1ea5a4d2b73622671361e1d44a273408dcae45aa21735f614e082023`、profile identity維持、E2事後22:17:22Zの観察から24h以内。新規forkなしreaderにprompt期待案内hashを渡した。sender source/stagingを直接読まず、案内→ledger→reference/config照合→public fetch2件→全entry/版→指定log/JPEGを実閲覧する。案内/configの期限切れ後は同policy identityの新原観察/configと新immutable案内を固定し、期待hashを新sessionへ明示する。旧案内を更新せず保持する。

E1期待package SHA-256=`49f64d158a7ac32a49a609260cc66e6a644d42d1398c272a0d62ce68ef67a5cd`、outer manifest SHA-256=`8c56671c485820eec4474eef18bec5a307b08a7b860e8e534369c637510c7d3d`。E2期待package SHA-256=`4d7810cfbd0415bde093b16f3e2ce556c86a20ef65da9a4571290ab919b30b06`、outer manifest SHA-256=`f847db6cea2b10acfe78aecba7f43c927bff95435380208ebc3766ee680905ee`。source manifest hashはE1のみ有効、E2 ledgerのzero64は非該当markerである。

新readerはprompt期待案内hash→ledger/config/referenceの照合後、public fetchを各1回・exit0で完了。archive全hash、outer manifest、対象base/head、全15+4entryのpath/bytes/hashと完全集合、E1 source manifest hash一致。E1指定2logを実readし、E2 fetched-readyの原JPEG2枚を `view_image(detail=original)` で実閲覧。Node読取のEPERMは保全し、画像toolで見えたpixelsを根拠とした。prefix全文/lifecycle日数は画像では省略されており見えたと扱わず、原設定JSONと区別。receipt SHA-256=`83363ea80f1fde8613fe70a4446c98f7ccbdc900a106cac74a09838aecc53841`、生成=`2026-10-06T22:23:52.9724265+00:00`、session=`/root/evidence_reader`、modelは親default継承・backend variant未実測。fetched script/DLL実行、sender原source/staging直接読取、C/A2所見読取なし。受信readyは `.../transfers/5a2bd180c4c04112bd1d2de71bc6ebb2/ready`（E1）、`.../transfers/c0e929c6b74a4b4eaddd9300cf447376/ready`（E2）を保持。

共通findings-free bundleを249filesで固定: `C:\Users\void\AppData\Local\OneStarMaker\Artifacts\review-evidence\evidence-first-use-judgment-04a488a71a464d1fb405d002b33745ac`、manifest SHA-256=`0c40482ceee7412f548e5be952323eee6c284ff3a32ad51b566ccefb485ad15b`、生成=`2026-10-06T22:26:01.7668057Z`。fixed A3/B/全implementation diff、必須offline/build/parse/audit/DLL identity、settings前後原観察、intent/保護/selection/candidate/ledger/実ZIP、固定案内、別reader raw/receipt/取得payloadを明示収録。C/A2所見、可変本書/CURRENT、秘密は収録しない。

別pwsh processで全249filesと原manifestを新private recipientへ無変換コピーし、受信全bytes/hash一致と規範/diff読取を確認。recipient=`C:\Users\void\AppData\Local\OneStarMaker\Artifacts\review-evidence\evidence-first-use-judgment-reader-62f39c71ebb34b49a434c29105752e27`、同manifest hash。copy receipt SHA-256=`77f721c0a93d55ef1c673392fbb14ae9db6333460f55a28f7db5ec4840ca7b2c`、生成=`2026-10-06T22:26:18.8110030+00:00`。C/C′のいずれの最終判定前にもこの同bundle/recipientを固定。C/C′の所見はpayload/ledger/reader receiptの外へ隔離し、C′に上記の発見C記録/可変本書を渡していない。C′の軽量availability probeは新gpt-5.6-sol sessionで成功し、そのsessionへblind固定入力だけを続けて渡した。

判定C: `gpt-6-astra` の新規sessionが同249-file recipient/実装headのM1〜M5を照合し、**C単独のGO**、未解消blockerなしと記録。全固定file/reader receipt/DLL identity/raw/2ledger/案内chain・public fetch/全19entry・指定raw閲覧を確認し、recipientコピーから指定2log全文と原JPEG2枚をC自身も実閲覧。最終出力SHA-256はMarkdown=`19828e661a2a61bdfc4e45104410561382728b7919f0487123b50cff468219a2`、JSON=`de0301938bd7c2885dfd5bbc6a539b00babd749e83e6bc5be084e58a10be027f`。出力は共通bundle外で保持。C′との統合判定を先取せず、C′結果非閲覧/非接触、再test/liveなし。

残存/未確認: 保存義務と有限server lockの期間差、管理者の途中設定変更、同Windowsユーザーの悪意ある改変・DPAPI分離限界、画像の省略範囲、別host/Cloud/Build/Unity/保持延長後policy移行。いずれも凍結範囲内の限定証拠と後続所有先を維持し、全期間server保護や全program採用を主張しない。

## 8. Phase C′

新規 `/root/evidence_cprime`、dispatch `gpt-5.6-sol` sessionが、上の同249-file recipient/manifest hash/固定base-headを使ってblind監査を完了し、**C′単独のGO**、M1〜M5 PASS、現在の問いを阻害する欠陥/常時契約違反なしと記録した。全249 entryのpath/bytes/hash、A3/B固定hash、完全diffの独立git生成との一致（130872bytes、SHA-256=`b5c0d69d8a3289c4fea4dc478ac8d70ca43adeeec84790788bd232e3b541eb3e`）、119/119と最終DLLの必須raw、2ledger/案内/public fetchのchainを確認。指定E1 logを読み、recipientの原JPEG2枚をoriginal detailでC′自身が実閲覧。画像の省略範囲を確認済みに読み替えず、所見は共通bundle外に保存した。

監査出力は `D:\repositories\unity\SampleGameForOneStarMakerFramework\docs\planning\harness\ARTIFACT_EVIDENCE_FIRST_USE_CPRIME_896eaea\C_PRIME_RESULT.md` と同folderの `C_PRIME_RESULT.json`。Markdown SHA-256=`cd20f5135b8ea7e119322af7b76e17f85504739308044eb8b3d936e86788cc30`、JSON SHA-256=`e7b2be9e7ff80352d1d7703fa04086c3600c1f3f106a2cebaaefaae8319c14c4`。rootがhashを再照合し、LastWriteTimeUtcは2026-10-06T22:33:27Z/22:33:28Zと観察。Gitへrawを追加せずownerの手元で保持する。取得対象の固定入力は§7のprivate recipientである。

B/C両方と異なるモデル、A未関与、新規session、blind入力という最低独立性条件を満たす。可変本書/CURRENT/C所見/A2報告を入力していない。軽量availability probeだけを先に実施し、固定判定証拠が揃ってから監査を開始した。backend variantは未実測。同OpenAI/GPT系列という強化独立性制約があり、vendor diversityは成立済みと扱わない。凍結済みの限定受容を維持し、人間のD判断へ引継ぐ。

C′が挙げた後続入力はArtifacts保持延長（owner D close+30日/長期参照と有限server lockの差）、画像の省略部分と構造観察の区別、別host/Cloud/Build/H2d/H3/同ユーザー悪意ある改変/policy移行、CreateNew/flush/ACL/hashが物理WORMではない点とrepair/pruneである。凍結済み境界を拡大しない。C′によるlive/資格情報/Unity/Buildの再実行や取得payload実行はなし。CとC′の統合・採否はDで行う。

## 9. Phase D

未到達。owner close/merge/harvest未実施。

2026-10-07 JST、人間が「PhaseD承認の前にPR出してほしい。オンラインのAgentにもチェックしてもらうので」と依頼。`develop` baseへのpush/PR公開だけを追加承認として受領。D close/merge/harvestは未承認のまま、公開READMEへのharvestもDで行う。オンラインAgent向けの追加レビューはPRの固定実装差分・凍結仕様・offline再現手順を入口にし、同一Windowsユーザー/マシンの既存reader取得をCloud成立と読み替えない。DPAPI資格情報とprivate原EvidenceをGitへ配布せず、オンラインで未取得のlive rawは未検証として扱う。

C/C′完了のreview-record更新後、`pwsh -NoProfile -File tools/docs-audit.ps1` は111 tracked md / errors0 / warnings1。warningは本書の§7/§8が両方埋まりharvest時期を促す検査3であり、D未実施のため本書を保持する。link/層違反なし。`git diff --check` 成功、`git diff --exit-code HEAD -- tools/artifacts.ps1 tools/Artifacts` 成功で判定後の実装差分なしを確認。レビュー結果だけの別commitはimplementation headを更新しない。生証拠/秘密/ignored planningはGitへ追加しない。上記人間依頼に基づくpush/PR公開の実績はPR本文へ記録し、D close/mergeは行わない。

A3固定specification: `C:\Users\void\AppData\Local\OneStarMaker\Artifacts\review-evidence\evidence-first-use-a3-7d774b8cbec9450eb2fd02f2c79f42f6\A3.md`、SHA-256=`ca360de172cea7766aed88152a14512f37306f33b32dc4fcfffd064ec5a58b95`。生成UTC=`2026-10-06T15:26:56Z`。同private rootに承認原bytes、freeze manifest、原文対応の機械照合を保持。新childはowner/SYSTEM/Administrators専用・継承停止ACL、既存共有親は変更していない。
