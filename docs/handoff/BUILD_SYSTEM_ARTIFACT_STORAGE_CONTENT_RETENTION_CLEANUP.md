# BuildSystem Artifact Storage — Content 件数保持の運用接続スライス

## 0. メタデータ

- type: `slice`
- status: `A`（A2 1 巡目を反映した r2）
- branch: `codex/artifact-storage-content-retention-cleanup`
- implementation base commit: `7a75cac40fc9ba8a1dd51f0ab4fda07c1f9edc41`（`develop`。PR #122 を含む。A3 で凍結時の `develop` と照合し直す）
- implementation head commit: 未固定（Phase B で記録）
- risk: `high`（R2 の実 DELETE を初めて Content に開く。1 revision が複数 object で、途中で止まると revision が壊れる。公開 API・asmdef・Unity は変えない）
- owner: OSM maintainers
- created: 2026-10-12
- expires: 2026-11-09 または置換 revision
- harvest to: 操作と契約を `tools/Artifacts/README.md`（新節「開発Contentの件数清掃（content cleanup）」と Content 保存節の既知の限界の更新）、program の状態と後続への移送を `docs/handoff/BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §4 B へ。完了時に本書を削除する。
- Phase A snapshot の取得案内・状態: 未生成
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票: 次は A2（独立レビュー）。A2 担当は §0〜§5 の入力版（commit と sha256 を §5 に記録）だけを読み、互いの指摘を見ない。実 R2・資格情報・Unity には触れない。

入力の正本は program §2「開発Content」と §4 B（PR #118 の「決めたこと」「後続への入力」、PR #119 の「決めたこと」「後続への入力」）、§4 A 末尾の PR #121 の段落、`tools/Artifacts/README.md` の「開発Buildの件数清掃」と「開発Content 1件の保存と取得」、および現行コード。program で固定済みの契約は §1 に転記し、再議論しない。

## 1. 目的と対象外（A0 planning packet）

### 目的

program §2 が固定した Content の件数保持契約（Build と同じ policy を系列ごとに適用し、保持中の Player が受け入れうる revision を保護する）を、R2 から古い revision を実際に消せる手動の運用経路へ接続する。Build の件数清掃（PR #119）と同じく、消し損ねは許し、消し過ぎは許さない方向を検証で固定する。

### program で固定済みの契約（再議論しない）

- 単位は revision（`transport.json` + `content/`）。保存後は不変で上書きしない。R2 は revision の prefix 配下に file 単位の object（PR #118）。
- 系列は `contentSet/target`。件数保持は系列ごとに独立。
- 成功 publish だけを N に数える。失敗・未完了は数えず、既存の成功を追い出さない。N の既定値案は Build に揃えて 10。
- 保持中の Player が受け入れうる revision は利用中として保護し、N に数える。保護状態を収集できないときは削除しない。
- 削除の主体は層ごとに別（R2、ArtifactsRoot、DIST cache）。DIST cache は対象外。
- 通常 writer に bucket 管理権限を与えず、削除拒否を rule 解除で突破しない。最低保存日数・owner の Close 承認・key ごとの削除承認は設けない。
- 確定記録は追記だけの記録（Content 確定記録スライスが所有）。消費側は revision と manifest の SHA-256 で固定する。

### 現況（A0 で確認した事実。読取だけ）

**F1. Content DELETE は transport 入口で無条件に拒否されている。**
- `Transport/R2ArtifactTransport.psm1` の `Test-R2ArtifactRequestAllowed` は `development/content/v1/` の DELETE を印の有無にかかわらず拒否する。Build DELETE は `BuildRetentionDelete=$true`（bool）の印付き request だけを通す（PR #119）。C# 側の `Execute` は Content key の DELETE を通す。Content DELETE を開く変更は psm1 の入口だけで決まる。
- 既存テスト `tests/Content.Contract.Tests.ps1` が Content DELETE の拒否を固定している。

**F2. Content のローカル記録。**
- 成功正本は `LocalApplicationData/OneStarMaker/Artifacts/contents-v1/<repositoryId>/<contentSet>/<target>/<revision>/receipt.json`。**directory が系列の階層を持つ**（Build の `builds-v1` は buildId 単位で系列を持たない）。各 revision directory には `intent.json`（最初の送信前に固定する決定的な送信計画。全 object の `path`・`bytes`・`sha256` を持つ）と `publish.lock`（`FileShare.None` の排他 handle 用 file）がある。
- `Read-ContentReceipt` は config の hash を照合しない（receipt の `intentSha256` と intent の bytes、主要 field の一致だけ）。content-config の bytes が変わっても成功記録は読める。ただし intent は `configSha256` を含むので、config を変えて同じ revision を `content collect` すると `intent-conflict` になる（既存の挙動）。
- 削除・保護の記録は無い。`content inspect` は receipt だけを見て、遠隔の object が消えても成功を返す（README の既知の限界）。`content collect` は receipt があり同一入力なら送信せず `existing` を返す。

**F3. 手元の記録（2026-10-12、読取だけ）。**
- `contents-v1`: repositoryId 1、系列 2（`bs4-spring-full/windows-x64-player`・`spring-full/windows-x64-player`）、各 1 revision。いずれも receipt・intent・`publish.lock` がある。どの系列も N=10 に遠い。
- `builds-v1`: Build directory が多数（PR #119 の検証用系列 `retention-probe/windows-x64-player/probe-1`〜`probe-4` の 44 件と、実系列の 1 件）。
- `OSM_ARTIFACTS_ROOT` はユーザー・process とも未設定。ツールは ArtifactsRoot の所在を後から知る手段を持たない（PR #119 の F3 と同じ）。

**F4. Player と Content revision の対応。**
- `build collect` は Player の receipt の `identity`（`yyyyMMddTHHmmssfffZ-<32hex>`）を buildIdentity とし、**buildId はその末尾 32 hex**（`Get-BuildCollectBuildId`）。同じ Player の `transport.json` の `revision` は receipt の `identity` と一致することを要求している（`ConvertFrom-BuildTransportManifest`）。つまり **build collect で退避した Player が受け入れる Content revision は「buildId を末尾 32 hex に持つ revision」** である。実例: Build `b80d0ef6…` と Content revision `20260924T202227427Z-b80d0ef6…`（`bs4-spring-full`）。
- `build-info.json`（ArtifactsRoot の `players/<project>/<target>/<configuration>/<buildId>/`）は `buildIdentity`・`contentSet`・`contentTarget`・`transportManifestSha256` を持つ（固定 11 field）。R2 上の Build の bundle にも同じ file が入るが、ローカルの Build receipt・intent は各 file の path・size・sha256 だけを持ち、`build-info.json` の中身は持たない。
- 現在の Player は `content/` を内包して起動する（Content 分離は後続）。保護しない revision を消しても、退避済み・R2 上の Player 自体は壊れない。保護は program §2 の契約を守るためで、Content 分離後に効く。

**F5. 削除の既存部品。**
- `BuildCleanup.psm1`（205 行）の削除試行は「intent → DELETE → sent → 確認 GET → deleted／abandoned」の不変記録（`BuildStore` の `Write-BuildImmutable`、`deletion/<n>.*.json`）と、transport の観測を非秘密の固定 field へ写す `ConvertTo-BuildObservation`（非 export）からなる。記録の読取側の観測検査は `BuildStore` の `Assert-BuildObservation`。
- `EvidenceCleanup.psm1`・`EvidenceReset.psm1` も「DELETE → GET 404 → tombstone」を持つが、記録の形・guard・store が別（task store、reset manifest）。
- `BuildRetentionPolicy.psm1` の `Get-BuildRetentionDecision` は行の `buildId` を書式検査せず、空白でない一意の文字列として扱う。Content revision をそのまま行の ID に使える。
- `ArtifactApplication.psm1` の `Test-ArtifactSuccess`／`Test-ArtifactMissing`（GET 404 `NoSuchKey`）、`New-ArtifactRequest`（Stopwatch による deadline）は共通。

**F6. R2 側の状態。** `development/content/v1/` に bucket rule があるかは未確認（AI は管理画面を読まない）。versioning は PR #119 で owner が「無効（設定項目なし）」と確認した。R2 には PR #118 の Spring（219 file）等の Content と、PR #119 の Build 検証用系列 40 object が残る。

### 要求（依頼の要約）

Content 系列 1 つについて、このPCの成功記録と保持中 Player からの保護を集め、N 件を超えた古い revision の全 object だけを R2 から実 DELETE できること。保護不明・確認不能・失敗のときは消さないこと。あわせて program §4 B に溜まった入力（下表）を、このスライスで決めるものと後続へ送るものに分ける。

### 対象外（A0 で確定。所有先は §2）

- 系列の退役（R2 の `retention-probe` 系列 40 object を含む）
- ArtifactsRoot の `players/`・`content/` の件数清掃と `.staging` 回収
- 自動起動（scheduler 接続）と N の系列ごとの設定
- Content 確定記録そのもの（境界だけ決める）
- Evidence 清掃・reset の削除手順の書換え（共通化の対象から外す。§3）
- 明示の利用中保護（`content use`／`release`）。保護源は保持中 Player だけにする（U2）
- このPCでの取得中の保護（Content の `fetch` は local store に触れない。U6）
- R2 上の孤児 object の回収、R2 一覧、別 PC の記録
- DIST cache、Unity 側の変更、`BuildRetentionPolicy` の契約変更

### 常時契約のうち本スライスに効くもの

- テストで `Task.Delay`／`Thread.Sleep` を使わない（並行は signal 同期、時刻は注入）。
- 参照 0 を削除理由にしない（R2 object は消すが、コードの未使用 API は削らない）。
- Artifact Storage の通常操作は Artifacts README の事前承認に従う。本依頼では owner が実 R2 の操作を許可済み（2026-10-12 チャット）。
- Unity の実行・テスト・Build は Phase C の責任（本スライスは Unity を変えない）。
- PR の base は `develop`。

### 未決事項（A1 で推奨案を出し、A3 で owner が決める）

- U1. 「保持中」の Player を何とみなすか（R2 の Build、ArtifactsRoot の Player、両方）と、その照合方法。
- U2. Content に明示の利用中保護（use／release）を足すか。
- U3. 複数 object の revision をどの順で消し、途中で止まったときに何を再開・取下げの基準にするか。
- U4. 「DELETE → GET 404 → 記録」を共通 primitive にするか、どこまで共通にするか。
- U5. 保護が N を超えた場合（Build の超過条項を引き継ぐか）と、件数保持が効く層。
- U6. この PC での取得中（`content fetch`）を保護するか。
- U7. 削除済み・削除中の revision に対する `content inspect`／`collect` の応答。
- U8. 系列の退役と `retention-probe` 40 object の扱い。
- U9. receipt の無い Build の `intent.json` の検査範囲（PR #119 の C′ 指摘）。
- U10. Content 確定記録との境界。
- U11. 実 R2 DELETE の検証の系列・件数・残置。
- U12. tracked HANDOFF か H1/CURRENT か。

## 2. 意思決定と受け入れ境界

- **このスライスが答える問い:** Content 系列 1 つについて、この PC の成功記録と保持中 Player（U1）からの保護を集め、`Get-BuildRetentionDecision` の判定で N 件を超えた古い revision の全 object だけを R2 から実 DELETE でき、保護不明・確認不能・失敗のときは消さないか。
  - 採否（A0）: program §4 B が挙げた入力のうち、実 DELETE と保護の収集は問いの中核なので扱う。系列の退役は「どの系列を終わらせるか」の別の判断（ownerの指示が起点で、件数とは独立）で、件数清掃の正しさとは別に判定できるため後続へ分ける。ArtifactsRoot 清掃と自動起動は PR #119 で独立スライスとして登録済み。共通 primitive 化は A1 で範囲を決める（U4）。

- **進める最低条件**

  **不変条件（起きてはいけないこと）:**
  - I1. 手順 1 の直前（revision の `publish.lock` 取得後に取り直した時点）で保護源（U1）に見えている revision に対して、新しい削除試行を始めない。それより後に現れた保護源（同じ identity の `build collect` が同時に走る場合）は排除しない（AC17 の既知の限界）。
  - I2. 保護状態・成功記録・削除記録のどれかを確認できないとき、その系列で新しい削除試行を始めない（fail-closed）。
  - I3. 確定点（`transport.json` の GET 404 を記録した時点）より前の試行では、候補でない revision（N 内の保持、保護、失敗・未完了、他系列）の object へ DELETE を送らない。Build・Evidence の key には本スライスの経路から一切 DELETE を送らない。確定点の後の残り object の削除は、取得できなくなった revision を消し切る処理で、候補・保護の判定に依らない（確定点の後に保護源が現れても、その revision は既に `transport.json` が無く取得できない）。
  - I4. 全 object の GET 404 `NoSuchKey` を確認して `deleted.json` を確定するまで、revision を削除済みと記録・表示しない（`committed.json` だけの revision は `deleting`）。DELETE の 2xx だけで削除済みにしない。
  - I5. 削除済み・削除中の revision について、`content inspect`／`content collect` が成功（`passed`）を返さず、再 PUT もしない。
  - I6. ローカルの成功記録（receipt・intent）と ArtifactsRoot・DIST cache を消さない・書き換えない。

  **成立条件（起きなければならないこと）:**
  - S1. 1 系列の成功 revision が N を超え、超過分が保護されていないとき、最古から超過分の revision の全 object が DELETE され、GET 404 を確認して削除済み記録が残る。
  - S2. 保護だけで N を超える間は、保護を外さず `excessCount` を出して正常終了する。
  - S3. 途中で止まった削除は、次回の実行で手順表（下）の「再開」から確定し、同じ入力の再実行では送信しない。
  - S4. 実 R2 で、検証用系列の N+1 件から最古 1 件の全 object だけが消え、再実行で送信 0 になる（高コスト証拠。§5）。

- **受け入れ条件**（各条件の検証例。検証する条件を併記）:

  | # | 条件 | 検証する条件 | 確認 |
  |---|---|---|---|
  | AC1 | `content cleanup --profile osm --config <content-config> --content-set <s> --target <t> [--artifacts-root <root>] [--dry-run]` は 1 系列だけを対象にする。`--content-set`・`--target` は `Get-ContentSeries` の規則で何も読む前に検査し、不正なら `invalid-content-identity`／`unsupported-series`。ArtifactsRoot は `content collect` と同じ規則（`--artifacts-root` かユーザー環境変数 `OSM_ARTIFACTS_ROOT`、既定値なし）で、どちらも無ければ `artifacts-root-not-configured` で何も読まない（保護源を集められないため。I2） | I2・I3 | offline |
  | AC2 | catalog は `contents-v1/<repo>/<contentSet>/<target>/` 直下を列挙し、revision 書式の directory と、本スライスが作る `cleanup.lock` だけを既知とする。revision directory 内の既知 entry は `receipt.json`・`intent.json`・`publish.lock`・`deletion/`。receipt が検査を通れば成功（`publishedAt` は receipt の値）、receipt が無く intent があれば未完了（`publish-incomplete`）、どちらも無ければ未完了。未完了は N に数えない。未知の entry、reparse、ACL 不一致、列挙失敗、receipt の検査失敗（receipt があって読めない）があれば系列を `catalog-incomplete` で、成功 revision の `deletion/` の記録が読めない（形・連番・hash の結び付き・観測の検査失敗、ACL 不一致）ときは `delete-record-invalid` で blocked にし、新しい試行を始めない。系列 directory が無ければ空（`within-limit`） | I2・I3 | offline |
  | AC3 | 系列に `publishedAt` が現在より未来の成功記録があれば `clock-skew` で blocked にする。「最新」は記録した `publishedAt` の降順で、同時刻は revision の Ordinal **昇順を新しい側**とする（`Get-BuildRetentionDecision` の既存の全順序。削除候補はその逆順で、最古から）。 | I2・I3 | offline |
  | AC4 | 保護の収集（U1。`ContentProtection` が所有）: (a) `builds-v1/<repo>/` 直下の entry 名 `<32hex>`（directory）と `<32hex>.reserved` の buildId のうち、`Read-BuildDeletionState` が `deleted` と**読めた**もの以外（directory の ACL 不一致・削除記録の破損など状態を確認できない ID は保護に残す）、(b) ArtifactsRoot の `players/<project>/<target>/<configuration>/` 直下の entry 名のうち、`<32hex>` と `build collect` の staging 名 `.<32hex>.<32hex>.staging` の先頭の buildId（列挙は配置の所有者 `BuildStage` が公開する読取関数で行う）。revision の末尾 32 hex がこの集合に含まれれば保護（`inUse=$true`）。名前が見えた ID は状態にかかわらず保護に数え（消さない側）、32 hex を取り出せない名前は保護に寄与しない。(a) の root・repo、(b) の `players/` と各階層の**列挙そのもの**の失敗・reparse は `protection-unknown` で blocked。`builds-v1` の repo directory や ArtifactsRoot の `players/` が無いことは「保護なし」。失敗・未完了・予約だけの Build も保護に数えるのは意図した過剰保護（publish 途中の Player を守るため）。 | I1・I2 | offline |
  | AC5 | N は定数 10（`ContentCleanup` 内の 1 箇所。テストだけ注入可能）。CLI・content-config から変えない。保護だけで N を超える間は `excessCount` を出し exit 0（S2）。 | S1・S2 | offline |
  | AC6 | `--dry-run` は lock を取らず、記録・network に触れず、実行時と同じ catalog・保護収集・policy で `retained`／`candidates`／`held`／`deleting`／`deleted` の件数と ID、`excessCount`、`protected`（保護された revision の ID）、`reasonCode` を返す。 | I3 | offline |
  | AC7 | 実行時は系列 lock（`<target>/cleanup.lock` を `FileShare.None`・非継承で開いた handle）を取り、取れなければ `cleanup-in-progress` で何もしない。lock 取得後に catalog と保護を取り直して policy を再評価し、lock 前・dry-run の結果を使わない。各候補の試行では revision の `publish.lock` を取った後、手順 1 の直前に catalog と保護を取り直し、なお候補の場合だけ始める。 | I1・I2・I3 | offline（別 process を signal で同期） |
  | AC8 | 各試行は revision の `publish.lock` を `FileShare.None` で取ってから始め、試行の終了まで保持する。取れなければ（`content collect`・`inspect` が保持中）その revision を `revision-busy` で飛ばす（`partial`）。 | I5 | offline（別 process） |
  | AC9 | 削除は候補ごとに直列で、古い順。1 試行の記録・送信・判定点は下の「削除試行の状態遷移」表を**唯一の正**とする。 | S1・S3・I4 | offline |
  | AC10 | 次のどれかのとき、その revision の試行は未解決のまま残り（`delete-pending`）、同じ run で同じ key へ再送せず、系列の残りの候補も処理しない。結果は `partial`、exit 2: (a) DELETE の観測が `Test-ArtifactSuccess`（同じ key・generation の検証済み 2xx）を満たさない、(b) 確認 GET の観測が `Test-ArtifactMissing` も `Test-ArtifactSuccess` も満たさない、(c) intent 確定後の記録の書込み失敗、(d) 予算超過（`budget-exceeded`）。 | I4・S3 | offline（各分類を注入） |
  | AC11 | 未解決の試行がある revision（`committed.json` だけで `deleted.json` が無いものを含む）は、成功件数にも保護にも数えない（`deleting`）。次回の実行は、新しい候補より先に未解決の試行を表の「再開」から処理する。削除済み記録のある revision は snapshot から除き `deleted` に数える。取下げだけの revision は通常の成功行に戻る。 | S3・I3 | offline |
  | AC12 | 1 回の実行で送る DELETE は最大 2,048 回（全試行の合計。手順 1 の開始時と各 DELETE の送信直前に判定し、試行の途中で達したらその試行を未解決のまま止める）、時間予算は 600 秒（`New-ArtifactRequest` の Stopwatch で各呼出しの deadline に効かせ、表の予算判定点でも判定）。上限に達したら残りを `remaining` に出して `partial`。 | S3 | offline |
  | AC13 | transport 入口: `development/content/v1/…` の DELETE は request に `ContentRetentionDelete=$true`（bool）がある場合だけ許す。印を付けるのは `ContentCleanup` だけ。印の無い・bool でない Content DELETE、印付きの Build・Evidence key の DELETE は拒否する（Build の `BuildRetentionDelete` と Evidence の `ResetAuthorized` の扱いは変えない）。`Test-R2ArtifactRequestAllowed` の表で検証する。既存の Content DELETE 拒否 assert は残す。 | I3 | offline（純粋関数の表） |
  | AC14 | `content inspect` は revision の `publish.lock` を `FileShare.None` で取り、取れなければ `publish-in-progress`。取れたら**receipt の検査より先に** `deletion/` の記録だけで削除状態を判定し、`deleted.json` が確定していれば `payload-deleted`、未解決の試行（`committed.json` だけの場合を含む）があれば `delete-pending`、`deletion/` が読めなければ `receipt-unavailable` を返し、reference を返さない。`content collect` は既存の `publish.lock` 保持区間で、intent 照合の後・receipt 分岐の前に同じ判定をし、`existing` の成功も新しい PUT も返さない。 | I5 | offline |
  | AC15 | 結果 JSON は非秘密の固定 field（`status`、`reasonCode`、`seriesId`、`keepCount`、`excessCount`、各分類の ID 配列と件数、`protected`、`remaining`、revision ごとの `outcome`、送信回数 `requests`（`delete`・`get`））だけ。endpoint・ローカル path・例外本文を出さない。exit は 0=完了、1=blocked／失敗、2=partial。 | I3 | offline |
  | AC16 | 本スライスは `contents-v1` の receipt・intent・`publish.lock`、`builds-v1` 配下の全 file、ArtifactsRoot 配下の全 file を書き換えない・消さない（書くのは `deletion/` と `cleanup.lock`、確認 GET の取得先である新規 private operation だけ）。fixture の前後で、これらの全 file の一覧と SHA-256 が一致することで確かめる。 | I6 | offline |
  | AC17 | README に運用と既知の限界を書く: N は 10、保護は保持中 Player（U1）だけ、ArtifactsRoot が必須、別 PC・Cloud の Build/Player は観測しない、`content fetch` 中の revision は保護しない（取得が失敗するだけ）、build collect と content cleanup を同じ identity で同時に動かす競合は排除しない、削除は書込み鍵のある PC だけ。 | — | docs-audit と目視 |
  | AC18 | 実 R2（高コスト証拠。§5）: 実系列 2 つの dry-run（期待値は独立 script で再計算）、空を確かめた検証用系列で 11 revision を `content collect` → `content cleanup` → 最古 1 revision の全 object が DELETE・GET 404・削除済み記録（DELETE 2・GET 3）、`content inspect`／同じ source での `content collect` が `payload-deleted`、`content fetch` が失敗、残り 10 revision の fetch が passed → 再実行で `requests` が DELETE 0・GET 0。 | S4・I3・I4・I5 | 判定 C |

- **削除試行の状態遷移（AC9 の正）:** 記録は全て `<revision>/deletion/` 配下の不変記録（CreateNew の temp へ全量書込み → rename。rename が確定点）。n は 1 からの連番。観測 field は Build と同じ非秘密の組（`operation`・`key`・`generation`・`httpStatus`・`statusClass`・`s3Code`・`statusObserved`・`timedOut`・`redirected`・`observedAt`）。系列 lock と revision の `publish.lock` は手順 1（再開なら該当手順）から試行の終了まで保持する。**`transport.json` を最初に消し、その 404 を確認した時点（手順 5）を「確定点」とする。** 確定点より前なら revision は無傷で取下げできる。確定点の後は revision を取得できない（fetch は `transport.json` から始める）ので、候補かどうかにかかわらず残りの object を消し切る。

  | 手順 | 前提 | 行うこと | 書く記録 | 予算の判定 | 失敗・超過のとき |
  |---|---|---|---|---|---|
  | 1 開始 | lock 取得後に取り直した snapshot・保護で候補（AC7）、`publish.lock` を取得（AC8）、DELETE 上限に余裕 | 試行を開始 | `<n>.intent.json`（revision、`manifestSha256`、receipt の `intentSha256`、消す key の一覧（`transport.json` を先頭、続いて intent の entries の順）、`intentAt`） | rename 直前 | 書かない。何も送らない |
  | 2 DELETE manifest | 手順 1 の記録が確定 | `transport.json` へ DELETE を 1 回 | — | 送信直前 | 送らない。未解決（次回は再開） |
  | 3 送信観測 | 応答を受けた（成否を問わない） | 観測を残す | `<n>.sent.json` | rename 直前 | 書かない。未解決（AC10 (c)）。2xx でなければ書いた後で AC10 (a) |
  | 4 確認 GET manifest | 手順 3 まで、または再開 | `transport.json` を GET | — | 送信直前 | 送らない。未解決 |
  | 5 確定 | GET が `Test-ArtifactMissing` | 確定点を記録 | `<n>.committed.json`（intent・sent の hash または `deleteObservation: unknown`、GET の観測） | rename 直前 | 書かない。未解決（次回の GET で確定） |
  | 6 取下げ | GET が同じ key・generation の `Test-ArtifactSuccess`（`transport.json` が残っている）。それ以外は AC10 (b) | 試行を閉じる | `<n>.abandoned.json`（intent の hash、GET の観測） | rename 直前 | 書かない。未解決 |
  | 7 残り object | 手順 5 が確定 | intent の一覧の 2 件目以降を順に: GET → 404 なら済み、検証済み 2xx なら DELETE 1 回 → GET。各 key の最終 GET が `Test-ArtifactMissing` であること | — | 各送信直前 | 未解決のまま止める（AC10）。次回は手順 7 を最初から（GET が先なので済んだ key へ DELETE を送らない） |
  | 8 削除済み | 手順 7 で全 key が 404 | 試行を閉じる | `<n>.deleted.json`（committed の hash、key ごとの最終 GET の観測と、この run で DELETE を送った key の送信観測） | rename 直前 | 書かない。未解決（次回は手順 7 から） |
  | 再開 | 未解決の試行あり | committed があれば手順 7 から。無ければ手順 4 から | — | — | 判定できなければ AC10 |

  - 手順 6 の後、その revision を成功行に戻し、完全な snapshot で policy を計算し直す。今も候補なら新しい n で手順 1 から。通常の run で手順 4 が 2xx（DELETE 2xx の後も残る）なら手順 6 で閉じ、その run では同じ revision に新しい試行を始めず `delete-unconfirmed`。
  - 手順 7 で key が `Test-ArtifactSuccess` のまま DELETE 後も残る場合は AC10 (b) と同じく未解決で止める（確定点の後なので取下げない）。
  - 手順 7 の「済み」判定は GET 404 だけに依る。

- **ここでは答えない問いと所有する後続スライス:**

  | 問い | 所有 |
  |---|---|
  | 系列の退役（イベント終了時の local・R2・統合フォルダ・DIST cache の片付け）、R2 の Build 検証用系列 `retention-probe/*` 40 object と本スライスの Content 検証用系列の残置の回収 | 新規「系列の退役」（program §4 B に登録） |
  | ArtifactsRoot の `players/`・`content/` の件数清掃、`.staging` 回収、ArtifactsRoot の所在の登録 | 既存「ArtifactsRoot の件数清掃と `.staging` 回収」 |
  | 自動起動、N の系列ごとの設定 | 既存「件数清掃の自動起動」 |
  | Content の明示の利用中保護（use／release）、取得中の保護、別 PC・Cloud と共有する保護 | Content 取り込み（DIST cache の lease と一緒に）／program §4 C |
  | 確定記録の形式・書込み方式、revision 削除時の記録の扱い、単調な publish 順序、「最新」の解決 | Content 確定記録（境界は §3 U10） |
  | Evidence 清掃・reset の削除手順を共通部品へ寄せるか | 必要になった時に各 owner が判断（本スライスは寄せない。U4） |
  | R2 一覧・孤児回収・別 PC の publish を数える | Content の容量上限（孤児回収）と R2 一覧の要否 |
  | 診断の改善（PR #118・#119・#121 の入力） | 既存「診断の改善」 |

- **program §4 B に溜まった入力の仕分け（A1 案）:**

  | 入力 | 本スライスで決めること | 後続へ送るもの |
  |---|---|---|
  | Content の実 DELETE | AC9〜AC13 | — |
  | 保持中 Player からの保護、R2 の Build と ArtifactsRoot の Player のどちらを保持中とみなすか | 両方（U1、AC4） | 別 PC・Cloud の Player → §4 C |
  | 共通 primitive 化 | 観測の写像と記録の観測検査だけを共通 module へ（U4） | Evidence への適用 → 必要時 |
  | 保護が N を超えた場合、件数保持の層 | Build の超過条項をそのまま引き継ぐ。R2 の object にだけ効く（U5） | ArtifactsRoot → 同名スライス |
  | 系列の退役、`retention-probe` 40 object | 決めない（U8） | 新規「系列の退役」 |
  | receipt の無い Build の intent 検査（PR #119 C′） | 現状の検査（所属 5 field）を契約として README に明記し、コードは変えない（U9） | — |
  | 確定記録との関係 | 境界だけ（U10） | Content 確定記録 |

- **判定定義:** GO = 不変条件 I1〜I6 と成立条件 S1〜S4 を判定必須テストと AC18 の生結果（最終 head）で確認し、阻害 findings が無い。NO-GO = いずれかが未達、または誤削除（候補でない revision・保護中の revision・Build/Evidence key への DELETE 送信）か、削除済み revision を成功として返す照会が 1 件でも観測された。CONDITIONAL ACCEPT は使わない。
- **停止規則:** 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- **A3 後の例外承認:** なし
- **決定記録:** なし
- **本文へ転記した実装制約:**
  - Unity C# の規則（`#nullable enable`・`record` 禁止）は本スライスに効かない（Unity C# を変えない）。transport・packaging の C# も変えない。
  - テストで時間待機を使わない。並行は別 process を signal で同期し、時計・transport・予算・N は注入する。
  - 公開 reason は許可語だけ。例外本文・path・endpoint を出さない。
  - 資格情報は既存の `Invoke-CredentialTransport` 経由だけ。鍵を引数・ログ・ファイルへ出さない。
  - 通常 writer に bucket 管理権限を足さず、403 を rule 変更で突破しない。
  - 既存の receipt・intent を書き換えない。追加する記録は不変（CreateNew + rename）。
- **未決事項:** U1〜U12（§3 に推奨案）。

## 3. 責務マップ

| ファイル | 責務（一文） | 変更理由 | 所有者・寿命 | 依存（入力 → 出力） | 公開面 | テスト境界 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|---|
| `ArtifactDeletion.psm1`（新規） | transport の観測を非秘密の固定 field へ写す・戻す（観測時刻は引数）、記録の観測を `Test-ArtifactSuccess`／`Test-ArtifactMissing` と同じ条件で検査する（純粋） | 観測の形・判定条件が変わったとき | 状態を持たない。時計・network に触れない | ArtifactCommands だけ（ArtifactApplication を import しない。記録の検査は既存 `Assert-BuildObservation` と同じく条件を field に対して判定する） | `ConvertTo-ArtifactObservation`、`ConvertFrom-ArtifactObservation`、`Assert-ArtifactObservation` | 表検証 | 0 → 約 60 |
| `BuildCleanup.psm1`・`BuildStore.psm1` | 既存の観測写像（`ConvertTo/From-BuildObservation`）と記録の観測検査（`Assert-BuildObservation`）を `ArtifactDeletion` へ移し、呼出しに置き換える（挙動は変えない）。`Write-BuildImmutable` に記録の種別（`build`／`content`）を引数で受け、許可名の表を種別ごとに分ける。**Build の許可集合は変えない**（Build に `committed` は書けない） | 共通化・Content 記録の追加 | 既存 | + ArtifactDeletion（BuildStore が依存するのは純粋な ArtifactDeletion だけ） | 変更なし | 既存の Build 系 suite（Publish・Collect・Cleanup・Retention）が無変更で通り、Build 種別が `committed` を拒否する assert を足す | 205 → 約 180、316 → 約 310 |
| `ContentDeletionStore.psm1`（新規） | revision の `deletion/` 配下の削除試行の不変記録の書込み・読取（`Read-ContentDeletionState`：形・連番・hash の結び付き・観測の検査）と系列 lock（`Enter-ContentSeriesLock`） | 削除記録の種類・検査が変わったとき | Content application。ContentStore の既存記録（receipt・intent・`publish.lock`）とは変更理由が別 | ContentStore（`Get-ContentDirectory`）、BuildStore（`Write-BuildImmutable` の `content` 種別）、ArtifactDeletion → 状態 | 上記を export | 記録の各破損の検査 | 0 → 約 150 |
| `ContentStore.psm1` | 変更なし（既存の `Enter-ContentPublishLock` は既に非待機で `publish-in-progress` を投げる。cleanup はその失敗を `revision-busy` へ写すだけ） | — | — | — | — | — | 105 → 105 |
| `ContentCatalog.psm1`（新規） | 1 系列の revision を読み、policy 入力行・削除中・削除済み・blocked 理由を作る | catalog の規則が変わったとき | 状態を持たない。何も書かない | ContentStore、ContentDeletionStore、ArtifactAcl → snapshot | `Get-ContentSeriesSnapshot` | 注入 root の fixture で全分類・全 blocked 理由 | 0 → 約 120 |
| `ContentProtection.psm1`（新規） | 保護源（AC4 の (a)(b)）から保護される buildId の集合を作る | 保護源の規則が変わったとき（Build・ArtifactsRoot 側の変更） | 状態を持たない。何も書かない | BuildStore（`Get-BuildRoot`、`Read-BuildDeletionState`）、BuildStage（players 配置の読取関数）、ArtifactAcl → 集合または `protection-unknown` | `Get-ContentProtectedBuildIds` | 注入 root の fixture | 0 → 約 70 |
| `BuildStage.psm1` | `players/` の配置の所有者として、`<ArtifactsRoot>/players/<p>/<t>/<c>/` 直下の entry 名から buildId を取り出す読取関数を足す（書込みなし） | 配置知識を 1 箇所に保つ | 既存 | — | `Get-BuildStagedPlayerIds` を追加 | Collect suite + 新ケース | 144 → 約 165 |
| `ContentCleanup.psm1`（新規） | 系列 lock の下で catalog・保護 → policy → 候補ごとの試行（状態遷移表）を順に進め、結果を集計する | 削除の手順・予算・上限・結果の形 | 状態は持たず、記録は ContentDeletionStore | ContentCatalog、ContentProtection、BuildRetentionPolicy、ContentStore、ContentDeletionStore、ArtifactDeletion、ArtifactApplication（network・判定）、Credentials → 結果 | `Invoke-ContentCleanup` | 注入 transport・時計・fault hook で全失敗経路 | 0 → 約 250 |
| `ContentApplication.psm1` | inspect の `publish.lock` と削除状態判定、collect の削除状態判定（AC14） | AC14 | 既存 | + ContentStore | 変更なし（返す reason が増える） | Content suite + 新ケース | 163 → 約 185 |
| `Transport/R2ArtifactTransport.psm1` | Content DELETE を `ContentRetentionDelete=$true` の印付き request だけ通す | AC13 | — | — | 変更なし | 表検証 | 54 → 約 58 |
| `BuildStore.psm1` の `Write-BuildImmutable` | Content の `deletion/<n>.(intent\|sent\|committed\|abandoned\|deleted).json` を許可名に足す | 記録の追加 | 既存 | — | — | 既存 | 上記に含む |
| `tools/artifacts.ps1` | `content cleanup` の grammar | 入口 | — | → ContentCleanup | CLI | grammar 拒否・必須引数 | 約 +10 |
| `tests/ContentCleanup.Tests.ps1`（新規）、`tests/ContentCleanupChild.ps1`（新規） | AC1〜AC16 の offline 検証 | — | テスト専用 | 注入 hook | — | — | 0 → 約 500、約 60 |
| `tests/Content.Contract.Tests.ps1` | AC13 の表に Content 印付き行を足す（既存の拒否 assert は残す） | AC13 | — | — | — | — | 196 → 約 205 |
| `tools/Artifacts/README.md` | 新節と既知の限界（AC17）、U9 の明記 | 公開面 | — | — | — | docs-audit | 304 → 約 345 |

**行数・責務の警報と判断:**
- 削除記録は `ContentStore` に足さず `ContentDeletionStore` に分ける（A2 ARCH-2）。publish の記録と削除の記録は変更理由が別で、足すと ContentStore が +90% で 50% 警報に当たる。保護の収集は catalog から `ContentProtection` に分ける（A2 ARCH-4）。Build・ArtifactsRoot 側の変更が catalog に波及しないため。
- `Write-BuildImmutable` を Content の記録にも使う既存の構成（PR #118）を継続する。許可名は種別引数で分け、Build の許可集合は変えない（A2 ARCH-3）。
- `ContentCleanup` は run 全体（lock・予算・集計）と 1 試行（`Invoke-ContentDeleteAttempt`）を同じ module に置く。合計 300 行を超える見込みなら Phase A へ戻す（A2 ARCH-8）。
- `ContentCleanup.psm1` は orchestration だけにし、分類（catalog）・選別（policy）・記録（store）・観測（ArtifactDeletion）を持たない。300 行を超える見込みなら Phase A へ戻す。
- `BuildCleanup` と `ContentCleanup` の手順本体は共通化しない。Build は 1 object・取下げ可能、Content は複数 object・確定点の後は取下げ不能で、状態遷移が違う（U4）。

### 未決事項の推奨案（A3 で owner が決める）

- **U1（保持中の Player）推奨: R2 の Build と ArtifactsRoot の Player の両方を保持中とみなし、buildId と revision の末尾 32 hex で照合する（AC4）。** 理由: F4 のとおり build collect した Player が受け入れる revision は末尾 32 hex が buildId のものに一意に決まる。照合に `build-info.json` の中身を読まないので、破損・ACL で読めない file によって系列が止まらず、Build の receipt が読めなくても directory 名だけで保護できる（消さない側に倒れる）。contentSet を照合しないので、同じ identity を別 contentSet に持つ revision も保護する（過剰保護。identity は Build ごとに一意なので実害は無い）。採らない案: (a) R2 の Build だけ: R2 から消えた後も ArtifactsRoot に残る Player を守れない（ArtifactsRoot 清掃は後続）。(b) `build-info.json` の 4 field で厳密照合: 正確だが、読めない file・中身の破損で系列を止める理由が増え、末尾照合より守る範囲が狭い。(c) ArtifactsRoot を任意: 未設定時に保護源を集められず I2 に反する。
- **U2（明示の保護）推奨: 足さない。** 理由: program §2 の保護は「保持中の Player」で定義され、Build の use／release に当たる要求は program に無い。足すと記録・CLI・lock の範囲が倍になる。必要になれば Content 取り込み（DIST cache の lease）と一緒に設計する。
- **U3（削除順と途中停止）推奨: 状態遷移表のとおり `transport.json` を最初に消し、その 404 を確定点にする。** 理由: fetch は `transport.json` から始めるので、確定点の前は revision が無傷で取下げでき、後は取得不能で「候補でなくなったら止める」に意味が無い。content を先に消すと、取下げ時に壊れた revision が成功として残る。
- **U4（共通 primitive）推奨: 観測の写像と記録の観測検査だけを `ArtifactDeletion.psm1` へ移し、Build と Content が使う。手順本体と Evidence は寄せない。** 理由: 写像と検査は Build と Content で同一でなければならず（記録だけから判定を再評価する前提）、二重に持つと腐る。手順は状態遷移が違い（U3）、Evidence は guard・store・tombstone の形が別で、寄せると一方の変更が他方へ波及する。
- **U5（超過と層）推奨: Build の超過条項をそのまま引き継ぐ（保護は外さず `excessCount`、exit 0）。件数保持は R2 の object にだけ効き、ローカル記録は削除済みとして残す。** 理由: program §2 が「Buildと同じ件数保持policy」と定めている。ArtifactsRoot の `content/` は取り込み元で、削除主体が別の層。
- **U6（取得中）推奨: 保護しない。** 理由: `content fetch` は reference だけで動き local store に触れない。削除と重なっても取得が `transport.json` の 404 か hash 不一致で失敗するだけで、壊れた取得物を成功として返さない（既存の fetch の検査）。README の既知の限界に書く。
- **U7（照会）推奨: AC14。** Build の AC21 と同じ考え方を Content の `publish.lock` で実現する。
- **U8（退役）推奨: 後続の新規「系列の退役」へ送る。** 理由: 退役は「その系列を終える」という件数と独立の判断で、対象の決め方（イベント終了・ownerの指示）と層（local・R2・統合フォルダ・DIST cache）が広い。本スライスの検証用系列の残置もそこで回収する。
- **U9（receipt の無い Build の intent）推奨: コードは変えず、「receipt の無い Build は未完了として数えず、intent は所属の判定にだけ使う（所属 5 field の検査）」を README に契約として書く。** 理由: 未完了は成功に数えず候補にもならないので、他の field の破損は消す Build の選び方に影響しない（PR #119 の owner 判断）。Content の catalog は directory が系列を持つので intent を所属の判定に使わない。
- **U10（確定記録との境界）推奨: 本スライスは確定記録を作らず、読まず、書かない。** 削除の正本はローカルの `deletion/` 記録。確定記録スライスは、削除済み revision を「最新」の解決から除く方法（削除の追記か、削除記録の参照か）を決める。本スライスの順序は receipt の `publishedAt`（時計の巻戻りは `clock-skew` で止めるだけ）。
- **U11（実 R2）推奨: §5 のとおり、試行ごとに新しい検証用 contentSet `retention-probe-<n>` で 11 revision（各 2 object）を collect して実行する。残置は試行ごとに最大 22 object（DELETE 失敗時）、成功時 20 object。** 回収は「系列の退役」。
- **U12 推奨: tracked HANDOFF で進める（H1/CURRENT は採らない）。** 理由: 直前の Build／Content スライスと同じ。H1 の固定 step は `artifact-evidence-lifecycle` 用で、Content 清掃の suite を足すには承認仕様の改訂が要る。

## 4. 実装計画

- **変更対象:** §3 の表のとおり。
- **順序:**
  1. `ArtifactDeletion.psm1` を作り、`BuildCleanup`・`BuildStore` の観測写像・検査を移し、`Write-BuildImmutable` に種別引数を足す。既存の Build 系 suite（Publish・Collect・Cleanup・Retention）が無変更で通ることを確認する。
  2. `ContentDeletionStore` の削除記録・系列 lock。
  3. `ContentCatalog`（分類・blocked）、`BuildStage` の読取関数と `ContentProtection`（保護収集）とテスト。
  4. `ContentCleanup`（dry-run → 実行 → 再開・取下げ・確定点後の続行）と失敗経路のテスト。
  5. transport の印（AC13）、`ContentApplication` の inspect／collect（AC14）、CLI grammar。
  6. README。`pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1`。
- **Phase B から Phase A へ差し戻す条件:** 計画外の永続記録・lock・公開 CLI が必要、`BuildRetentionPolicy` の契約変更が必要、transport の C# 変更が必要、`ContentCleanup` が分類・記録・観測の責務を持たないと書けない、既存の receipt・intent の形式変更が必要、`ArtifactDeletion` への移動で BuildCleanup の既存テストの期待値を変える必要が出た。
- **対象外を維持する方法:** `ContentCleanup`／`ContentCatalog` に ArtifactsRoot・`builds-v1` への書込みを置かない。テストで Build・Evidence key への DELETE が 0 回、ArtifactsRoot・`builds-v1` fixture が不変であることを assert する。

## 5. テストとレビュー計画

- **単体テスト（offline、`tests/ContentCleanup.Tests.ps1`）:**
  - catalog: 成功・未完了・削除中・削除済み・取下げ済みの混在、未知 entry・reparse・ACL 不一致・読めない receipt で blocked、未来時刻で `clock-skew`、系列 directory 無しで `within-limit`（AC2・AC3・AC11）。
  - 保護: builds-v1 の directory・`.reserved`・削除済み Build・状態を読めない Build（保護に残る）、ArtifactsRoot の players（staging 名を含む。32 hex を取り出せない名前は無視）、`players/` 無し、builds-v1 の repo 無し、列挙失敗・reparse で `protection-unknown`、ArtifactsRoot 未設定で `artifacts-root-not-configured`、保護が N を超える `excessCount`（AC1・AC4・AC5）。
  - 削除: dry-run が何も書かない、候補だけを古い順（同時刻の向きを含む）、`deletion/` が読めない revision で `delete-record-invalid`、`committed.json` だけの revision が `deleting`・inspect で `delete-pending`、確定点の後に保護源が現れても残り object を消し切る、試行途中の DELETE 上限で未解決、状態遷移表の各手順の失敗・予算超過、確定点前の取下げと再計算、確定点後の再開（済んだ key に DELETE を送らない）、DELETE 2xx 後の残存、403／timeout／応答不明、DELETE 上限、記録だけからの判定の再評価、`requests` の回数（AC6〜AC12・AC15）。
  - 照会: 削除済み・削除中の revision への inspect／collect（AC14）。
  - 競合: 系列 lock 保持中の `cleanup-in-progress`、collect が `publish.lock` 保持中の `revision-busy`、cleanup 保持中の inspect の `publish-in-progress`（AC7・AC8・AC14）。
  - 境界: `Test-R2ArtifactRequestAllowed` の表（AC13）、fixture 不変（AC16）。
- **差し戻し中の起点 `-Filter`:** `ContentCleanup.Tests.ps1`、`Content.Contract.Tests.ps1`、`Content.Publish.Tests.ps1`、`Content.Fetch.Tests.ps1`、`BuildCleanup.Tests.ps1`（いずれも `-Case '*'`）。
- **判定必須テスト（GO 候補 head）:** 3 つの .NET Release build（Probe・Packaging・Transport）、`tools/Artifacts/tests/*.Tests.ps1` の全 suite を `-Case '*'`（`rg` を PATH に通してから 1 回）、Workflow `TaskLifecycle`、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、diff 中の `unity/` が 0 件の機械確認、AC18 の実 R2 生結果（最終 head）。
- **全 EditMode 回帰の適用除外:** 適用除外。理由: Unity の source・asmdef・Assets を変更しない。代替証拠: 全 Artifacts offline suite の結果 JSON と、diff に `unity/` が無いことの機械確認。
- **統合・Unity テスト:** なし。
- **操作・実行時・目視条件の検証経路（AC18）:** 担当は判定 C（実行担当が CLI を順に実行し、判定 C 担当が書いた独立照合 script を原記録に対して実行する）。最終 head で行う。環境は owner の Windows ユーザー、既存の `osm` 資格情報と登録済み Evidence config・content-config。ArtifactsRoot は git 管理外の新規の空 directory。操作:
  1. 実系列 `bs4-spring-full/windows-x64-player` と `spring-full/windows-x64-player` の `content cleanup --dry-run`（ArtifactsRoot は下記の検証用 root。期待値は判定 C の独立 script が直前の生の receipt・`builds-v1` の entry 名・ArtifactsRoot の entry 名から再計算し、本スライスの catalog 関数を使わない。F3 時点の予想は各 1 件で `within-limit`、`bs4-spring-full` の revision は builds-v1 の `b80d0ef6…` により `protected`。予想と違っても再計算した期待値と一致すれば合格）。
  2. 検証用 contentSet `retention-probe-<n>`（n は試行ごとに新しく 1 から）について、dry-run で成功 0・削除中 0・未完了 0 を原記録に残す（空でなければ collect も cleanup もせず中止し、新しい n にする）。その後、合成の Content 出力 11 件（各 `transport.json` + 1 file、revision は時刻順の `yyyyMMddTHHmmssfffZ-<32hex>`、末尾 32 hex は builds-v1・ArtifactsRoot に無い乱数）を `content collect`。
  3. `content cleanup` を実行。期待: 最古（AC3 の全順序）の 1 revision だけが `deleted`、`requests` が DELETE 2・GET 3（`transport.json` の DELETE・GET、content object の事前 GET・DELETE・GET。手順表から再計算した値と照合し、結果 JSON の回数だけで合否にしない）、`deletion/1.intent.json`・`sent.json`・`committed.json`・`deleted.json` がある。
  4. 削除した revision の `content inspect` と同じ source での `content collect` が `payload-deleted`、`content fetch` が失敗。残り 10 revision の `content fetch` が passed で、取得物の `transport.json` の SHA-256 が receipt の `manifestSha256` と一致。
  5. 同じ `content cleanup` をもう一度。期待: `requests` が DELETE 0・GET 0。
  - 保存する証拠（非秘密の原記録そのもの）: 実系列の dry-run 結果、検証用系列の実行前 dry-run、11 件の receipt・intent、`deletion/` 配下の全記録、cleanup の結果 JSON 2 回分、inspect・collect・fetch の結果と exit code、取得物の hash 照合出力、判定 C の独立照合 script の source と出力。endpoint とローカル path は収録前に除く（原本での照合は判定 C の script が行い、その出力を同じ bundle に入れる）。
  - DELETE が 403 なら S4 は未達で、rule は変えない。head が変わって実証をやり直すときは新しい `retention-probe-<n>` を使う。
- **未知の操作経路の疎通結果と確認地点:** Content key への実 DELETE と直後の GET 404 `NoSuchKey` は未確認。初回確認は Phase C（AC18）。理由: Phase A では実 R2 を操作しない。Build key の DELETE → GET 404 は PR #119 で成立済み、versioning 無効も確認済み。`development/content/v1/` の bucket rule は未確認。不成立時: 403 なら rule を変えずに `delete-pending` が出ることを確認し、owner に設定を確認してもらい、問いを分けるかを A3 revision で決める。
- **人間の判断が必要な条件:** なし（合否は結果 JSON と記録の機械照合）。実 R2 の操作は owner が許可済み（2026-10-12）。
- **高コスト証拠:** AC18（実 R2 の collect・DELETE・fetch）。手順は上記、独立した期待値は「11 件の receipt から policy の全順序で算出した最古 1 revision の 2 key だけが DELETE・GET 404、他の 20 key は fetch で hash 一致、再実行の送信 0」。順序は 判定 C（高コスト以外）→ 暫定 C′ → AC18 取得 → 最終証拠ラウンド（判定 C・最終 C′）。
- **判定 C の照合 script の方式:** 判定 C の offline 実行。判定 C 担当が script を書き、資格情報・通信に触れない環境で、AC18 の原記録の複製に対して実行する。原記録へ適用する前に、判定 C が作った合成 fixture（期待値を先に書く）で一度通す。
- **機械検査:** `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、全 Artifacts PowerShell の parse。
- **A0/A1 主担当・モデル・ベンダー:** Claude Opus 5.5／Anthropic（Claude Code desktop）。
- **A2 独立レビューごとの観点・担当・モデル・ベンダー、入力版:** 2 件とも入力版 commit `b0961a57d4a984be0524b9c622339a0d213bdf24` の本書（sha256 `50680ea7…8c9`）を読み、互いの指摘を見ていない。読取専用で、R2・資格情報・Unity に触れていない。
  1. **アーキテクチャゲート:** Claude Sonnet 5.5／Anthropic、新規 subagent session。blocker 0、major 4、minor 5。
  2. **失敗経路・fail-closed・競合・検証経路:** Grok 4.7 High（`grok-4.7-high`）／xAI、cursor-agent の新規 session（`--trust`、編集禁止を指示）。当初 Claude Fable 5.1 に割り当てたが、利用クレジット不足で起動できなかった。blocker 4、major 4、minor 1。
  - 独立性: ②は別ベンダー。①は A1 主担当と同じベンダー（モデル系列は違う）で独立性制約あり。

  | # | 出典 | 重大度 | 指摘（要約） | 採否（r2 で反映） |
  |---|---|---|---|---|
  | ARCH-1 | ① | major | ArtifactDeletion が ArtifactApplication に依存すると、BuildStore が network・資格情報を引き込む。写像は時計に依存している | 採用。ArtifactDeletion は ArtifactCommands だけに依存し、観測時刻は引数 |
  | ARCH-2 | ① | major | ContentStore の増分見積りが過小。削除記録の読取は Build と同型で大きい | 採用。`ContentDeletionStore` に分離 |
  | ARCH-3 | ① | major | `Write-BuildImmutable` の許可名に `committed` を足すと Build にも書ける | 採用。種別引数で許可表を分け、Build の集合は変えない |
  | ARCH-4 | ① | major | catalog と保護収集は変更理由が別。players の配置の所有者は BuildStage | 採用。`ContentProtection` に分離、players の列挙は BuildStage の読取関数 |
  | ARCH-5 | ① | minor | (a) の過剰保護が意図か不明 | 採用。AC4 に意図を明記 |
  | ARCH-6 | ① | minor | `publish.lock` の非待機取得は既存 | 採用。ContentStore は変更なしに訂正 |
  | ARCH-7 | ① | minor | 削減行数の見積り | 採用（見積り修正） |
  | ARCH-8 | ① | minor | 1 試行と run 全体の境界 | 採用。同 module・300 行超で Phase A |
  | ARCH-9 | ① | minor | 確認対象を BuildCleanup に限らない | 採用。Build 系 suite 全体 |
  | FAIL-1 | ② | blocker | build collect の staging 中（`.<H>.<guid>.staging`、`<H>.reserved` 作成前）は保護源に見えず、同 identity の revision を消しうる | 採用。staging 名も保護に数え、I1 を「手順 1 直前に見えた保護源」に明確化、残る窓は AC17 の既知の限界 |
  | FAIL-2 | ② | blocker | 確定点の後に保護源が現れても手順 7 が消し切る | 採用（I3 を明確化）。確定点の後は revision が取得不能で、消し切りは保護判定に依らないと I3 に明記 |
  | FAIL-3 | ② | blocker | 成功 revision の `deletion/` が読めないときの扱いが AC2 に無い | 採用。`delete-record-invalid` で blocked |
  | FAIL-4 | ② | major | 名前は見えたが状態を確認できない ID の扱いが曖昧 | 採用。保護に残す |
  | FAIL-5 | ② | blocker | 手順 7 の事前 GET により期待値は GET 3（AC18 の GET 2 と矛盾） | 採用。DELETE 2・GET 3、表から再計算して照合 |
  | FAIL-6 | ② | major | `committed.json` だけの状態の表示が曖昧 | 採用。`deleting`／`delete-pending` |
  | FAIL-7 | ② | major | AC16 の書込先と hash 対象が曖昧 | 採用。全 file の一覧と SHA-256 |
  | FAIL-8 | ② | major | 検証用系列が空でない場合の中止条件、実系列の期待値の独立性 | 採用。中止条件と独立 script での再計算 |
  | FAIL-9 | ② | minor | 同時刻の向き、試行途中の DELETE 上限 | 採用。AC3・AC12 に明記 |
- **A3 統合担当・モデル・採否:** 未実施。
- **C' 用に予約した担当・モデル・ベンダー:** GPT（Codex CLI）／OpenAI。Phase A・B・C に使わない。
- **独立性の強化条件を満たせない場合の理由:** —

## 6. Phase B 実装結果

（未着手）

## 7. Phase C

（未着手）

## 8. Phase C'

（未着手）

## 9. Phase D

（未着手）
