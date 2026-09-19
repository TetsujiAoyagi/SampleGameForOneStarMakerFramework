# DIST — 配信・local cache・開発 workflow

## 0. メタデータ

- type: slice
- status: B（Phase A r4 凍結済み、発見CからB適応へ差し戻し）
- branch: `codex/dist-content-delivery`
- implementation base commit: `3c6769ad3af53c3cbe050894b7040b4eaa268e8d`
- implementation head commit: 未到達
- risk: high（永続化、別 process 排他、物理削除、Player 起動）
- owner: DIST 主担当 / Codex
- created: 2026-09-19
- expires: DIST Phase D。2026-10-19 に未完なら再確認
- harvest to: Architecture `04-app-startup.md`、`13-resource-system.md`、`18-asset-description.md`、`20-variant-checkout-workflow.md`
- Phase A snapshot: `artifacts/bs2b/dist-evidence/a3-r4/phase-a.md`（local evidence、PRへ同梱しない）
- Phase A snapshot generated at: `2026-09-19T14:33:19.5428001Z`
- Phase A snapshot SHA256: `f3bd5e4086f44c336410f85f0cd43263f64dba3b90372a0684a8141adc20ed57`
- Phase B result / evidence / C' blind bundle: 未到達

## 1. A0 — 目的・現況・制約

問い: remote/LAN の成果物を検証済み local directory として導入し、既存の利用中 revision を壊さず代表 Scene/Object をロードできるか。

PR #66 は 2026-09-19T10:15:43Z に develop へ merge 済み。merge commit は `b23ccc4c279f8605c4d953cb9d0015d7e20ad308`。開始時 HEAD と fetch 後 origin/develop は上記 base と一致し working tree は clean。base は workflow 指針を更新した PR #67 も含む。削除済み BS4 HANDOFF を復元しない。未追跡 PRE と既存検証ログは編集・追跡追加しない。

既存の成立契約:

- BS2b は選択済み directory 一式を identity ごとに公開し、preflight に content set / target / roots / dependency closure を記録。Unity 内部 manifest / BuildReport は opaque な build 成果物であり外部 protocol ではない。
- BS3 の ContentDirectorySession が登録、native operation、root、token、unregister と取消後 drain を所有する。AssetManagement は owner/cache を所有し、cache と処理中 operation も revision 利用に含む。利用中に登録 lease を解放しない。
- ContentRevisionGate は同一 process 内の identity+target または canonical path の衝突を防ぐ。現時点で別 process の排他はない。session は一 process 一つ。
- BS4 は Windows x64 / StandaloneWindows64-Player / IL2CPP / High、Unity 6000.6.0f1。Player は対応 content identity と bootstrap / graph metadata / first Scene / representation を持つ。任意 revision との互換性は証明していない。
- 既定 Addressables、通常 Play 停止時の同期 shutdown は残す。明示 close の drain を通常 Play 停止へ拡張しない。

本文へ転記した常時制約: Game → Framework の依存方向、既存 asmdef の無断 edge 追加禁止、IAssetManagement と AssetOwner、14 SceneState の順序と所有者、公開 ILogger<T>、UpdateSystemRuntime 順序・例外分離を維持。Unity C# は先頭 #nullable enable、record 禁止、破棄可能 Unity Object の偽 null に注意。テストで Task.Delay/Thread.Sleep 禁止。Unity Scene/asset は Editor 経由。B はコンパイル確認まで、Build/テストは C。標準 tools/run-tests.ps1 を Windows では sandbox 外で実行。参照 0 は削除理由にしない。

対象外: 実運用への公開、認証情報/課金、Git/SVN 自動 checkout、未取得 source の編集、asset 単位 HTTP 遅延 fetch、delta、CDN、汎用互換 Player 更新、実行中表現切替、Addressables 廃止。

## 2. A1 — 最低条件と契約案

### 最低条件・受け入れ条件

M1: HTTP（検証用 loopback endpoint）と local/LAN directory から manifest 指定の一式を取得し、size/SHA-256 検証後だけ install を公開する。実 Unity 登録から代表 Scene と Prefab の load/release/close まで確認する。

M2: 別 revision の更新が中断・欠損・hash 不一致・target/revision 不一致で失敗しても known-good の bytes と起動経路を変えない。再試行が成功する。install 前の staging は登録不可、同じ revision の内容差し替えは拒否。

M3: disk budget は既存 known-good と使用中 revision を強制削除しない。候補選定と削除結果を説明可能にし、容量不足を明示。BS3 gate 経由で owner/cache/native drain 中の削除、削除中の再登録を拒否。物理削除失敗後も finally で許可 lease を解放する。別 process の利用・削除競合を実 process で検証する。

M4: local/remote の明示選択と expected revision を入口にし、鮮度は requested revision と install revision の一致で判定する。source path の欠損・変更案内は実行判定と分離する。Editor Play を取得済み directory へ明示接続する。source 未取得の状態で対応 BS4 Player を起動できる。

M5: 最終 head の全 EditMode、HTTP/install/PlayMode、Windows x64 IL2CPP High 実 Player の bootstrap→登録→論理初回 Scene→代表 Prefab→明示 close が成功する。未実行、必須テスト skip、古い head の証拠は GO ではない。

GO は M1〜M5 と常時契約の充足。必須経路未実行なら NO-GO。CONDITIONAL ACCEPT は用いない。新しい public API/owner/state/依存/失敗契約が必要なら A revision。最低条件への違反でない新しい問いは後続へ送る。通常の技術判断・A2 採否・A3 凍結は program のユーザー委任に従い主担当が理由付きで行う。Phase D merge はユーザーの明示指示まで行わない。

### Transport と互換性

OSM transport manifest v1 は UTF-8 JSON。version=1、contentSet、revision（既存 BuildIdentity と同値）、target=`StandaloneWindows64-Player`、unityVersion=`6000.6.0f1`、rootSchemaVersion、files[{path,size,sha256}]、sourceFiles[{path,sha256}] を持つ。sourceFiles は build 時に採取した選択依存閉包の案内用で、取得先でも起動でも source を再走査しない。content file 一式を opaque に配信し Unity 内部 manifest を解釈しない。

manifest bytes 自体の SHA-256 を request で pin する。expected contentSet/revision/target/Unity/root schema と完全一致した manifest だけを受理する。SHA-256 は bytes 同一性であり配信元の真正性を自動保証しない。v1 は明示指定された信頼する manifest digest と HTTP(S)/LAN を使用する。署名・鍵運用は後続配信運用 slice。

path は `/` 区切り相対 path のみ。空、`.`/`..`、absolute、backslash、drive/ADS、URL escape、case-insensitive duplicate、file/directory prefix collision、Windows reserved name、末尾 dot/space、reparse point は拒否。size は非負 long、hash は lowercase 64 hex。数・manifest bytes・合計 size を上限検査し overflow を拒否する。未知 version は fail closed。未知 optional field を許すが必須 field 欠損を default で通さない。

### Install、cache、削除

cache root は caller が明示する local filesystem root。revision の immutable directory と、staging、制御 metadata を分ける。同じ volume の staging→final directory rename を commit point とする。OSM receipt と transport manifest を staging に置き、全 file を検証してから rename する。登録対象は receipt が指す content subdirectory のみ。既存 final は上書きせず完全再検証した同一 manifest のみ再利用する。

取消・接続断・404・hash 不一致は失敗として返し partial を active にしない。v1 は retry 時に一式を fresh staging へ取り直す（Range/resume は後続）。自動無限 retry はしない。cleanup 失敗は元の失敗を保持して残留 staging を報告し、次回明示 cleanup の候補にする。

known-good は検証済み install の明示 pin とし、現 session の利用状態とは別概念。利用中かの独自台帳を作らない。少なくとも選択中の既存 known-good を新規 install 完了まで保持する。disk budget は file bytes+staging 予約を含めて admission 判定し、known-good と今回 request を除く古い install を決定的順序で候補にする。削除許可が得られない/物理削除に失敗した候補は飛ばし、満たせなければ不足 bytes と理由を返す。

別 process 排他は ContentRevisionGate に追加する OS lease adapter を同一の Reserve/TryAcquireDelete 経由で使用する。登録側は session の既存 reservation と同じ寿命、削除側は物理削除完了/失敗まで保持する。identity+target と canonical path の双方を保護する。lock の待ち合わせはしない。OS 資源は process crash で解放され、永続 lock marker を利用中の根拠にしない。具体的な shared/exclusive primitive と supported filesystem は調査と A2 で確定する。

物理削除は install root 境界検証→delete lease→receipt 再検証→登録不可の tombstone へ rename→物理削除→finally lease 解放。失敗した tombstone は再登録不可で次回 cleanup 可能。削除候補を利用可否判定として使わない。

### 開発 workflow と Player

local は手元の published directory からも同じ manifest/hash/install 検証を通す。remote は manifest の revision を明示取得する。offline installed は既知 digest の検証済み revision を明示選択する。remote 最新を取得したと称さず、freshness は Exact / DifferentRevision / UnknownRemote として表示。更新通知・latest channel は後続。

Editor 入口は Play 開始前に install と設定を完了する。既存 directory mode に verified path/identity/representation を渡し、通常 Play の bootstrap/graph は既存 source を使う。source なしの complete Editor bootstrap 置換は RET。source 欠損時の起動保証は source 非依存の BS4 Player 経路で担保する。sourceFiles の依存閉包を path と hash で診断して Missing/Changed/Complete を案内し、Player 起動を止めない。

BS4 Player は baked identity/target/representation/firstScene を維持。DIST の install path の選択口だけを追加する案で、manifest revision が baked identity と一致しない場合は Unity 登録前に拒否。既存 package-relative content の既定経路は維持。Player の config を任意新 revision 向けに書き換える launcher にしない。取得は Player 起動前に完了する。

### 後続所有者

- RET（BUILD_SYSTEM_REBUILD_PROGRAM）: 既定 Addressables、旧 profile/whitelist/hybrid/catalog/checkout report、通常 Editor bootstrap の source 依存、通常 Play 停止の完全 drain、旧 menu と serialized 参照の移行。
- 配信運用拡張（同 program 後続入力）: signing/authentication、latest channel、CDN、delta/resume、他 OS/target/filesystem、互換 Player の revision 横断更新。
- 非 Scene 実需要 slice: Mesh 等の型/サブアセット一般化。

## 3. 責務マップ（A2 で確定）

新規 Runtime/BuildContent/Distribution は配信 domain の境界として置き、既存 OneStarMaker.Runtime asmdef を使う。新 asmdef edge は不要。transport DTO/validation（約220行）、manifest codec/hash/path/file I/O（約220行）、HTTP/local source adapter（約140行）、installer（約250行）、cache policy/削除（約220行）を分ける。状態は一 install request と cache store が所有、Unity Object/AssetDatabase 依存なし。公開面は request/result/source/store の必要最小限。DTO の mutable serialization と validated immutable snapshot を分ける。

ContentRevisionGate（現約116行、+50想定）は既存排他の orchestration、別ファイル RevisionProcessLease（新約130行）は OS 資源だけを所有。session reservation の寿命は変更しない。fake lease と実 process を双方検証。50%以上増加は別 process I/O を分離する理由とし gate へ file I/O を混在させない。

PlayerContentConfiguration（現約47行、+35想定）は installed override の互換性検査だけ追加。bootstrap（既存約1000行、+10以内）は呼び出し接続だけで配信責務を増やさない。Player baked config と env/CLI の優先順位を調査して凍結する。

Editor/Build/Content の transport publisher（新約170行）は build report と file tree から manifest を生成する Editor I/O。SampleGame/DependOnAll/Editor/Build の developer workflow（新約220行）は project 固有 menu/入力と Play 設定を所有。source 診断（新約100行）は file probe を注入可能にし Runtime の登録と分離。既存 BS4 coordinator は新 Player の build/validation 入口のみを必要時に追加。

テストは Tests/BuildContent/Distribution、Tests/Editor/Build に分ける。pure validator/cache policy と fake transport/delete failure は単体、実 HTTP/filesystem/process は統合、Unity Scene/Object は既存 fixture を拡張または専用 fixture、実 Player は独立 harness。実装前にファイル名・公開署名・規模の最終一覧を固定する。

## 4. 実装・検証計画

B は frozen snapshot のみを入力に新規セッションへ渡す。新しい契約判断は A revision。日本語コメントに所有者/寿命/失敗境界/理由を残す。B ではコンパイル確認と contract-audit、Build/テストは未実行と記録する。

発見 C は B と異なるモデルで固定 base/head の完全 diff と snapshot を構造レビューし、blocker をまとめる。差し戻し中の起点 filter は Distribution / ContentRevisionGate / ContentDirectorySession / PlayerContentConfiguration。判定 C は blocker 解消後の最終 head で全 EditMode（空 filter、適用除外なし）、graphics 付き実 HTTP→install→PlayMode Scene/Prefab→close、別 process lock、Windows x64 IL2CPP High の新規 Player build と source 無しの installed content 起動、更新中断と旧 revision 再起動を必須とする。fixture 生成・build は C の責任。NUnit Ignore を必須成功に数えない。

証拠は新規の ignored `artifacts/bs2b/dist-evidence/<id>/` に snapshot、完全 diff、manifest、SHA256、生ログ、XML、コマンド、Player receipt を固定する。既存ログへ書き込まない。標準 test runner の出力を新規 evidence へコピーして固定し、runner の既存ログを編集しない。入力 manifest に生成時刻/hash/base/head を記録。判定 C と C' は同じ head、C' は A snapshot/B result/raw evidence のみを読み C 所見を隔離する。証拠は PR の恒久追跡対象にしない。

担当計画: A 主担当 GPT-6 Astra、A0 代替検討 GPT-5.6 Sol（初稿なし）、A2 architecture GPT-5.6 Terra、failure/protocol GPT-5.6 Luna。B=GPT-5.6 Sol、C=GPT-6 Astra、C'=GPT-5.5 を各fresh sessionで実行する。C'はA/B/C未関与。同一ベンダーに限られる強化独立性の制約を明記する。

### A1 技術判断の具体化（レビュー入力 r1）

1. 対応範囲は同一 Windows user / local NTFS cache。LAN は取得元のみで install root にはしない。ContentRevisionGate の別 process lease は固定 user-local lock directory に SHA256(length-prefixed identity,target) と SHA256(canonical path) の2ファイルを作り、registration は FileAccess.Read/FileShare.Read、delete は FileAccess.ReadWrite/FileShare.None で非待機取得する。全 lease は同じ ordinal 順で取り、途中失敗は既取得分を dispose。空 lock file 自体は削除しない。初回 CreateNew 競合は既存ファイルを再 open、I/O failure は fail closed。Windows user が同じ全 Editor/Player に共通の `%LOCALAPPDATA%/OneStarMaker/RevisionLocks/v1` を用いる。複数 process の共有登録を許し、delete 中は新規登録不可。lock I/O 不可は `RevisionLockUnavailable` を新 failure code として返す。path alias は既存正規化に加え managed install の全 ancestor reparse point を拒否。別ユーザー・外部ツールによる lock/成果物変更は保証外。

2. cache root 全体の install/catalog/budget transaction は root/control の独立 exclusive FileStream lease 一つで直列化（busy は待たず構造化失敗）。session はこの lease を取らない。lock 順は cache transaction→ContentRevisionGate、逆順を作らない。既存 installed 検証と取得では registration 相当の一時 read lease が必要なので gate に `AcquireRead` を追加する（session の一 process 一登録制約とは分離、削除との唯一の排他を再利用）。TryAcquireDelete は in-process read leases と OS leases の両方を照合。read lease は検証/コピー終了時に解放し、登録は session が別途 reservation を取得後に managed receipt/hash を再検証してから native 登録する。これにより install結果→登録間の削除競合は fail closed となる。

3. installed tree は `installed/<contentSet>/<revision>/content/` と sibling `transport.json`、`receipt.json`。target は v1 固定だが manifest で照合。receipt は version/digest/contentSet/revision/target/installedUtc を持つ。制御 metadata は `control/known-good.json`、一 contentSet につき一 revision pin。昇格は実 register/load 成功を確認した caller の明示 `MarkKnownGood` のみ。pin 更新は同 volume temporary file→File.Replace（初回 Move）とし、失敗時旧 pin を残す。列挙は directory receipt が正本で中央 active 台帳を持たない。破損 receipt は自動削除候補にせず診断。staging/tombstone は専用領域の自己所有 marker を検証して cleanup。budget は installed、staging、tombstone の実 file size を数える（lock/小さい control metadata は除外）、request の全 size を予約し、候補は pin/request を除く installedUtc→key ordinal。known-good 不在の初回 install は許すが、インストール成功だけで known-good と呼ばない。pin の読み込み不正は削除を停止する。

4. manifest 上限は 4 MiB、100000 files、合計 size は request の positive disk budget 以下、sourceFiles も100000件。revision は BuildIdentity の safe ASCII segment、contentSet も同様（各128文字以下）。transport compatibility は version=1 / product=`OneStarMaker` / unityVersion=`6000.6.0f1` / rootSchemaVersion=2 / playerConfigSchemaVersion=1 を完全一致。v1 は新規 publisher のみを対象にし旧 root v1 の自動変換を持たない。HTTP は HttpClient streaming、finite timeout（request cancel と別に各 response 60秒）、redirect を許さず同一 base URI 下の escaped path を取得、非2xx/長さ超過を失敗。retry は caller が明示再実行する。

5. Player の base required JSON だけから baked content identity/target/representation/firstScene を読み、env/CLI と合成後の同キーが変わっていれば起動拒否する。追加キー `content:installedRevisionPath` と `content:manifestSha256` の pair で任意 absolute cache revision root を指定できる。pin digest、receipt、manifest、全 files、baked identity を照合して content subdir に解決する。pair 片欠けは拒否。既存 relativeDirectory は override 無しで維持。Runtime に network 起動を追加しない。Editor も同じ pair を最優先の明示経路として検証し、既存 directoryPath の直接指定は legacy として残す。managed install を直接指定しても session が receipt を検証する。既存 package directory を新たなDIST installと称さない。

6. Editor workflow は Framework Editor の `ContentDeliveryWindow`（Tools/OSM/Content/Delivery）で local published path / HTTP base URL / installed offline を選び、manifest digest、contentSet、revision、cache root、disk budget、representation を明示入力。UserSettings の専用 JSON に保存し shared app-config を編集しない。Prepare が install/validate と source診断を実行、Use For Next Play が process env の SAMPLEGAME_CONTENT__* を設定する SampleGame adapter を呼ぶ。Reset は自分の設定値のみ復元し、Play中の変更を拒否。Framework window から Game への依存を作らないため window 自体を SampleGame/DependOnAll/Editor/Build に置き、配信 core/source診断は Framework が所有。Domain Reload 後も設定を再評価し、source不足でも取得済み directory 入力を渡す。Play の通常 bootstrap source 欠損は明確に診断し、source無し保証経路として対応Player起動手順を案内する。

7. 最終ファイル/API map: Runtime/BuildContent/Distribution の `ContentTransportManifest.cs`（DTO+validated immutable model/要求/result、0→230）、`ContentManifestValidation.cs`（pure validation、0→230）、`ContentDeliveryFiles.cs`（path/hash/codec/receipt検証、0→220）、`ContentArtifactSource.cs`（public IDisposable source + Local/HTTP adapters、0→160）、`ContentInstaller.cs`（public InstallAsync(request,source,ct)、0→240）、`ContentCacheStore.cs`（public Inspect/MarkKnownGood/Evict、transactionと永続化、0→250）、`ContentEvictionPolicy.cs`（pure候補選定、0→100）、`ContentSourceAdvisor.cs`（source closure hash診断、0→100）。Runtime/BuildContent の `RevisionProcessLease.cs`（0→150）、既存 gate（+80）、session（+10）/PlayerContentConfiguration（+60）/bootstrap（+25）。Editor/Build/Content の `ContentTransportPublisher.cs`（成功BuildContentResult+preflight→別publish先 manifest/files、0→180）。SampleGame Editor Build の `ContentDeliveryWindow.cs`（UI/設定、0→230）と `ContentDeliveryPlayBridge.cs`（env寿命、0→120）。公開型は producer/consumer の assembly 境界に必要な DTO/source/install/store/advisor/publisher のみ、validation/policy/file/lockはinternal。既存 asmdef edge 追加なし。Player configuration と gate の50%増は新責務を別ファイルへ分離し、既存型には照合と接続だけ残す。署名の細部、private helper、test seam delegate は上記責務/所有者/公開面を変えない範囲で B 適応可。

8. 判定 helper は `tools/ContentDeliveryProbe/` の小さい .NET console（Runtime の pure gate/process lease source を link、Unity例外型は必要最小 shim、productionアルゴリズム複製禁止）を使用可能。OS lease の共有read/排他delete/crash解放を信号pipe/stdinで制御してテストする。Unity内の同等実Player gate成立を実Player保持中の削除拒否でも証明する。既存 BS4 smoke に signal-file ベースの任意 hold フック（receipt段階到達後、明示終了信号までresource保持、有限timeout）を追加してよい。sleepによる競合テストは作らない。

技術資料: [FileShare](https://learn.microsoft.com/en-us/dotnet/api/system.io.fileshare)、[Directory.Move](https://learn.microsoft.com/en-us/dotnet/api/system.io.directory.move)。共有readと排他open、同volume/既存destination拒否を設計入力とする。Unity Mono/IL2CPP上の成立は最終実証で判定し、.NET文書だけで実証済みとはしない。rename atomicity は可視性の境界であり、電源断に対するdurable transactionまで保証しない。

### A2 architecture 採用反映 r2（上記の暫定記述より本節を優先）

- Config: 新規 internal `PlayerBakedContentConfiguration.cs`（0→80行、Runtime/BuildContent、OneStarMaker.Runtime）は required JSON だけから protected keys（schemaVersion/runtimeMode/buildIdentity/target/representation/firstScene/relativeDirectory/probeToken）をコピーし immutable にする。Bootstrap.BuildConfig が一回 required provider を読み、その辞書を baked snapshot と合成済 AppConfig の両方の元にする。再度ファイルを読み直さない。snapshot は initializer 所有、起動〜ReleaseAll、ReleaseAll で破棄。`PlayerContentConfiguration.Resolve(baked, merged, installRoot,target)` は全 protected key の ordinal 一致と override pair を検査。既存 Read は既存単体テスト互換のため retained overload として内部既定経路を検証できるが、production Player は必ず Resolve を使う。receipt/hash は configuration 型へ実装しない。
- 登録 authority: 新規 internal `VerifiedInstalledRevision` は `InstalledRevisionVerifier.cs`（0→180行、Distribution）のみが生成。receipt/manifest/full files を検証し immutable な revisionRoot/contentPath/identity/target/digest を持つ。`RegisterVerified(VerifiedInstalledRevision)` は private 共通 register に接続し、session reservation 取得後に同 verifier を再実行して native Register へ進む。legacy `Register(path,identity,target)` は既存の非 managed directory 用に維持し、managed layout (`content` の親に receipt または transport がある) は verifier へ振り分け、一方だけ欠損も拒否する。Bootstrap の private configuration に optional VerifiedInstalledRevision を持たせて二経路を明示。session に transport/file I/O を実装せず検証呼出だけを置く。verifier は file I/O/manifest policy にのみ依存、cache store/catalog/Unity native には依存しない。中途削除からの再登録は native 前に失敗する。
- Lock ledger: InstallAsync は request 全体（admission→eviction→download→verification→rename→result）で cache transaction を保持する。v1 は同root同時downloadを直列化する可用性コストを受容し、durable reservation 台帳を追加しない。Inspect は read-only snapshot と明示し結果は削除許可ではない。MarkKnownGood/Evict/cleanup はそれぞれ operation全体を root transaction で囲む。verifier は gate read lease を取得し cache transaction は取得しない。session は gate Reserve→verifierの lease内再検証→native Register、逆向き cache lock を取らない。installの既存final再利用は cache transaction→gate read→verification→read解放。tombstone cleanup は cache transaction→元identity/path delete lease→marker照合→削除。非待機 lease 不可は structured Busy、OS I/O errorはLockUnavailable。downloadが取消/timeout/例外でも transaction/read/source streamをfinallyで全解放。

責務・owner・依存・単体境界の詳細:

- ContentTransportManifest: serialization DTO と immutable request/result/validated値。状態は caller input の defensive copy、OS資源なし。依存はSystem collectionsのみ。公開は assembly間受渡し型、DTO検証は validatorを経由。純粋値/変異隔離テスト。
- ContentManifestValidation: protocol/version/compatibility/path集合/sizeのpolicy。状態・資源なし、manifest値→validated値、IOなし、internal。Unity無しのpure case matrix。
- ContentDeliveryFiles: JSON codec/hash/path confinement/reparse/file列挙。streamは各メソッドのusing内だけ所有。System.IO/CryptoとJsonUtilityのcodec adapterのみ、internal。parserとfile probeを分離しvalidator/policyからUnity依存を隔離。ファイルサイズ/部分IO faultはfixtureと注入delegateで検証。
- ContentArtifactSource: public IDisposable sourceをcallerが所有し、複数requestで再利用可。`OpenReadAsync(relativePath,ct)` が返したStreamはinstallerがrequest内でdispose。HTTP adapterがHttpClientを所有しsource.Disposeで廃棄、返すstream wrapperがresponseを所有しstream.Disposeでresponseも廃棄。source.Disposeをinstallerから呼ばない。local adapterもcaller-owned、各open streamは返却先所有。通信のみ、Unity/manifest policy/cache依存なし。fake sourceで取消/short/oversize/throwを検証。
- ContentInstaller: public async install orchestration。caller-owned store/sourceを借り、request/transaction/staging/一時read lease/返却streamをoperation内だけ所有。成功resultへleaseを逃さない。依存はvalidator/source/store/verifier、Unity native/Editorなし。source/final move fault注入で旧revision不変を検証。
- ContentCacheStore: caller-owned cache root handle（永続open handleは持たない）。transaction単位でlock streamを所有。receipt列挙/control metadata/pin atomic update/削除実行とrecoveryを担い、policyはContentEvictionPolicyへ委譲。I/O fault delegateはconstructor内部port、公開Inspector/Evict/MarkKnownGoodはstructured result。Unity nativeとsource非依存。Temp NTFS fixtureとdelete/rename failure注入。
- ContentEvictionPolicy: receipt snapshot/budget/request/pin→ordered candidate、pure/internal、状態なし。busy判断をしない。保護対象・同時予約の計算・安定順テスト。
- ContentSourceAdvisor: manifest sourceFiles + projectRoot + file probe→Missing/Changed/Completeとpath一覧。callerの診断一回、永続状態なし。sourceを変更せずAssetDatabaseを使用しない。公開reportはEditorから参照。fake filesystemで閉包判定。起動・installへ依存しない。
- RevisionProcessLease: internal OS FileStream acquisition/disposeのみ、寿命はgateから返るlease。System.IO/CryptoのみでUnity無し。key導出/両lock取得順/取得途中失敗回収をpure/OS helperで検証。
- ContentRevisionGate: static in-process conflictとprocess port orchestration、read/reservation/delete token所有。session登録のsingle-owner制約を維持。Readはtemporary検証用途で複数可。public AcquireRead/TryAcquireDelete、Reserve internal。token final disposeまで利用保護、IDisposable再dispose安全。OS障害はfail closed。fake portと既存session testsがテスト境界。
- ContentDirectorySession: native登録/root/token/drainの既存owner。verified入力の検証呼出をreservation内へ追加するだけ。native backend portとverifierで失敗順序を検証、独立した配信stateを持たない。
- InstalledRevisionVerifier/VerifiedInstalledRevision: receiptとmanifestとbytes一致を保証するinternal authority。gate read leaseを検証中だけ所有。file/validation依存、Unity/Editor/cache policy非依存。cache transaction非取得。結果はowner tokenではなく再検証必須snapshot。
- PlayerBakedContentConfiguration/PlayerContentConfiguration: 前記immutable起動値と照合policy。IOはverifierへ委譲、protected config比較はpure、所有者はinitializerの一起動。internal。
- AbstractApplicationInitializer: baked snapshotの保持・構成結果に応じたRegister/RegisterVerifiedの選択のみ。既存App寿命。既存1000行超は既存負債として明記、新配信policyを入れず+35以内の配線に限る。
- ContentTransportPublisher: public Editor producer。成功result/preflight/出力directory→manifestとopaque files。copy/hash/source閉包採取は一publishのstaging所有。既存report schemaのみを理解、Unity内部manifestは解析しない。Runtime DTOとSystem.IO/JsonUtility、Editor assembly内。fake成功report+file treeでテスト、本物buildはC。
- ContentDeliveryWindow: SampleGame Editor表示/入力保存だけ。window寿命にsource/cancellationを所有、closeでcancel+dispose。Installerの結果と診断を表示、runtimeUnityには依存しない。UserSettings専用ファイルのみ変更。UI parsing/設定serializationを単体、代表操作をEditorで確認。
- ContentDeliveryPlayBridge: SampleGame Editor composition、explicit user設定→env pair/identity/representation一回適用。previous値と自分が書いた値を保存し、自分の値のままのキーのみResetで復元。Play中の適用拒否、domain reloadでUserSettingsを読むが既存値を無条件上書きしない。process envが寿命、AppConfigの規則に従う。fake envで適用/取消/他者変更保全を検証。
- PlayerContentSmoke hold hook: 既存代表Prefabの検証後〜destroy前だけの任意診断。signal rootは明示指定、ready fileを書き、release signalまでPlayerLoop Yieldとcancel/finite deadlineで待つ。handle/instance所有は既存finallyから動かさない。通常未指定時は既存挙動。source-linked probeはOS primitiveの証拠に限定し実Player holdでproduction runtimeとの接続を確認。

上記全新規Runtime型は既存 OneStarMaker.Runtime/BuildContent namespace subtree、Editor publisherは既存 OneStarMaker.Build.Content、UI/bridgeは既存 SampleGame.DependOnAll.Editorに置く。理由はFramework/Game、Runtime/Editor、protocol/file I/O/policy、owner/lifetimeの違い。Unity無し単体可能なcoreとcodec adapterを混ぜない。新testは現行Tests/Tests.Editorのfriend accessを使用、asmdef edgeは追加しない。小さく見せるための汎用Helper/Managerは作らない。

Protocol補足: content subtree の実 file 集合は manifest files と完全一致を検証し未記載fileも拒否。sourceFiles は案内用の project-relative `Assets/` または `Packages/` pathとhash（取得時に存在する.metaも含む）であり、source path は転送先path/URLに使用しない。publisherは成果物とpreflightのidentity/targetを照合し、outputをsource配下へ作らない。manifestはcanonical field/file順、UTF-8 no BOMで生成するが consumerはcanonical再serializationをhashせず受信したraw bytesをrequest digestと照合する。未知 optional field を含んでもraw bytes同一性を維持する。

公開失敗面は `ContentDeliveryException` + `ContentDeliveryFailureCode`（InvalidManifest/IntegrityMismatch/IdentityMismatch/TargetMismatch/CompatibilityMismatch/MissingFile/TransportFailure/Busy/LockUnavailable/BudgetUnsatisfied/InstallConflict/IoFailure）。caller取消は標準OperationCanceledException、timeoutはTransportFailureに原因保持。失敗のinner exceptionとcleanup診断を保持し成功resultを返さない。削除結果は Deleted/DeferredBusy/DeleteFailed/Protected と対象key/reason、budget不足はrequired/available bytesを持つ。成功resultはrevisionRoot/contentPath/manifest digest/identity/contentSet/target/AlreadyInstalledのみ。freshnessは期待revision照合結果を表示し、revision文字列の大小で新旧を推測しない。

### A2 r2 最終補正

- AcquireRead は **internal**。Distribution/session は同一 Runtime assembly のため新規 public read API は不要。前記public表記を訂正し、既存 public TryAcquireDelete/ContentDeletionLeaseのみ維持する。
- Play bridge の apply set は `SAMPLEGAME_CONTENT__RUNTIMEMODE=directory`、`SAMPLEGAME_CONTENT__INSTALLEDREVISIONPATH`、`SAMPLEGAME_CONTENT__MANIFESTSHA256`、`SAMPLEGAME_CONTENT__BUILDIDENTITY`、`SAMPLEGAME_CONTENT__REPRESENTATION` の5キー。各キーのprevious/installed値を記録し自己値が残るものだけResetで復元。directory mode内でmanaged pairをlegacy directoryPathより先に解決する。未知modeと片欠けpairを黙ってAddressablesへ戻さない。

## 5. A2 / A3

### 最終補正 r3

- RegisterVerified の Reserve は **同一process予約とOS shared read leaseの両方**を返し、receipt再検証→native Register→全利用→drain→unregister成功まで連続保持する。事前verificationの一時read解放とReserveの間にdeleteが成功した場合は、Reserve後の再検証が失敗してnative Registerへ進まない。利用前の削除を許すことと利用中の削除を許すことを混同しない。handoff gapでdeleteを挿入したtestを必須にする。
- Player baked protected keysへ `contentSet` を追加。SampleGamePlayerBuildCoordinator の RuntimeJson は `bs4-spring-full` を記録し、installed overrideはこのbaked contentSetとmanifestの完全一致を要求する。contentSet無しの既存package-relative pathの挙動は維持するが、installed overrideは明確に拒否して対応configを持つPlayerの再buildを案内する。Player configuration pure testsでwrong-contentSet拒否を検証し、判定Playerも同fieldを持つconfigでbuildする。Editor bridgeはinstall result由来のcontentSetをmanifestと照合できるため、Playerと異なりbaked configは必要ない。
- tombstone markerはversion/cache root/元revisionRoot/元contentPath/contentSet/revision/target/digestを保存する。cleanupはmarkerの自己所有とroot包含を検証し、cache transaction→**元**identity/target/contentPath delete lease→物理削除→finally解放。tombstoneのpathをnative登録に渡さない。rename前marker作成失敗は元installを変更せず終了。cleanup retryもM3失敗検証に含む。

### 採否 ledger

- A0 alternative / requested gpt-5.6-sol / fresh context、A1未読: 責務4分離、revision=BuildIdentity、共有read/排他delete、互換Player限定、成功後known-good昇格を採用。物理削除失敗時にinstalledのまま保持する案はpartial再登録防止のためtombstone方式へ変更。category semantic、独自、accepted/一部代替採用。
- A2 architecture r1 / requested gpt-5.6-terra / fresh context: owner/lifetime不足、baked/merged分離、verified登録authority、cache transaction ledgerの4 blockerを採用しr2で解消。category semantic、独自。source-link probe単独をproduction証拠としない指摘も採用（実Player必須を維持）。
- A2 architecture r2 / 同担当: 上記4 blocker解消を確認。Editor mode apply setとAcquireRead公開範囲の2 blockerを採用し最終補正へ反映。category semantic、独自、accepted。追加の範囲拡張なし。
- A2 failure/protocol r1 / requested gpt-5.6-luna / fresh context、architecture所見未入力: OS lease gap指摘はReserveがOS leaseも含む既定設計の読取り相違として欠陥判定は不採用、連続保持とgap再検証testの説明明確化は採用。category semantic、duplicate、rejected-as-defect。contentSet照合不足はM4のblockerとして採用しr3でbaked fieldを追加。category semantic、独自、accepted。tombstone retryの元path指摘は既定M3の詳細として採用しmarkerを明記。
- モデル名はspawn tool指定値を記録。A0担当のGPT-6 Astra自己報告はpersonaからの推測と本人が訂正済み。実ランタイムの別metadataは未確認。C'は未関与 gpt-5.5 を予約。同一OpenAI系列/ベンダーのため強化独立性には制約があり、B/C/C'別モデル・fresh context・blind入力の最低条件は維持する。
- A3統合: 主担当 GPT-6 Astra。programのユーザー委任に基づき上記を採用、目的/最低条件の追加はなし。r3を凍結してBへ渡す。新状態/API/owner/依存/失敗契約の変更が必要ならA revision。人間の目的・範囲判断を要する未決事項なし。Editor既存busyプロセス終了の権限確認だけ別途pending。

## 6. Phase B 実装結果

### Phase A revision 4（公開consumer API、凍結）

Editor offline/source診断からinternal verifier/codecへ依存しない公開境界が必要なためAを再開した。M1〜M5・owner/state/asmdef・永続schema・判定必須は変更しない。

- `ContentCacheStore.ValidateInstalled(string revisionRoot, string manifestSha256, string contentSet, string revision, string target) -> ContentInstallResult` を追加。storeのcache root内の正規installed/set/revisionに限定し、caller期待値をgate read lease内で完全検証してAlreadyInstalled=trueを返す。通信/mutation/pin昇格なし。leaseは返却前に解放、register時再検証を維持。
- `ContentSourceAdvisor.InspectInstalled(ContentInstallResult installed, string projectRoot) -> ContentSourceAdvice` を追加。固定resultのidentity/target/digestでread lease内再検証してsourceFilesを読み、既存Inspectへ委譲。string rootだけを取る公開overloadは禁止。sourceはprojectRoot内Assets/Packages相対pathに限定して読み取り、変更しない。
- 両公開境界は既定ContentDeliveryExceptionへ正規化。lock競合=Busy、lock I/O=LockUnavailable、receipt/manifest/sourceFiles不正=InvalidManifest/IntegrityMismatch、診断file I/O=IoFailure。Missing/Changedは正常advice。source診断側のI/O失敗も取得済みresultを破棄せず表示し、Player起動の拒否条件へ転用しない。installed metadata自体の検証失敗とは分離する。
- A2: gpt-5.6-terra（既存A担当へ固定r3+API差分のみ）。public failure分類を明記する1 blockerを採用。他のowner/lease/公開範囲は妥当と確認。元提案SHA256 `9ab10b1575708b9438111ba3da36080a77b6932bafeb88719a653be6953d9fcb`。A3: 主担当がprogram委任内で採用・凍結。内部へのfriend追加/Editor側独自検証複製案は不採用。
- tests: wrong cache root/digest/set、欠損metadata、read中delete拒否、sourceMissing/Changed、返却後delete→register拒否。既存条件の確認で新完了バーなし。

初回実装 head `38b8baaef87effa926884b8d5d62e19f19ee0811`。担当gpt-5.6-sol（新規context）。transport/install/cache/gate、baked config、Editor UI/bridge/publisher、process probe、testsを追加。contract-audit/diff-check PASS。Unity compile/必須実証は未実行。B result snapshotは `artifacts/bs2b/dist-evidence/discovery-38b8baa/phase-b-result.md`、hashは同bundle manifestに固定。probeのdotnet build --no-restoreはNETSDK1004でcompile前に終了、成功証拠に含めない。

既存Editor PID34948が作業開始前から長時間busyのため、未保存状態不明のまま強制終了していない。検証用worktreeを `artifacts/bs2b/dist-validation` に作成（初回はWindows長いpathで失敗し、今回限りcore.longpaths=trueで再作成）。同worktreeのEditorでcompileを確認する。既存PRE/検証ログを編集しない。

修正Bでは主担当gpt-6-astraもpublisherの整形、実HTTP/local installを既存Scene/Object統合fixtureへ接続、Player buildからtransport publishへの接続を実装した。このため以後の固定headの発見/判定Cは新規contextのgpt-5.6-terraへ移し、Bの両担当モデルとの相違を維持する。既存gpt-6-astraのCは初回head38の発見と検証入口調査までの記録とし、最終判定へ流用しない。C'は未関与gpt-5.5を維持する。

## 7. Phase C

発見Cのみ実施。担当gpt-6-astra（Bと異なる、新規context）。base `3c6769ad3af53c3cbe050894b7040b4eaa268e8d` / head `38b8baaef87effa926884b8d5d62e19f19ee0811`。
入力bundle `artifacts/bs2b/dist-evidence/discovery-38b8baa`、manifest SHA256 `e85298b1ed5e20446aa1fa8d5ab33355868e2497b98462728d3de36c558e04f1`。frozen A/B result/完全diff/stat/name-status/機械監査を固定。全入力hash一致をreviewerが確認。
所見 `c-findings.md` SHA256 `4b1ab100215bea9ebd336f92315a2dd0c5e5bd67e0c3fa20592fbd447289d45b`。構造のassembly方向/基本責務配置は概ね適合、失敗境界と実証接続が未充足。

C01〜C14は全件frozen条件に根拠を持つ現slice欠陥として採用しB適応へ差戻し（修正待ち）: identity segment/path境界、staging/tombstone登録authority、ancestor reparse、receipt意味/配置と削除直前検証、known-good pin検証、HTTP body timeout/取消、Player mode保護、consumer互換性定数、budget metadata/overflow/候補理由、staging回復、Editor reload所有情報、source診断/offline検証、必須JSON field、公開例外分類。
検証入口照合でM1の実HTTP/install→実load未接続と、M4のPlayer hold取消がinstantiate成功記録前に例外を返しclone破棄を飛ばす欠陥を追加採用した。前者は既存統合fixtureを接続、後者はbehavior確認後〜destroy前へ移すB適応とする。
後続へ送る新要求なし。判定必須はすべて未実行。発見段階で重い検証未実行、GO/C'なし。修正収束後の新headで判定Cを行う。

## 8. Phase C'

未実施。blind bundle 未生成。

## 9. Phase D

ユーザーのマージ判断待ちとなるのは C/C' 完了後。現時点で未到達、merge 禁止。
