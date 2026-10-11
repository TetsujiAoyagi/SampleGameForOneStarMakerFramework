# BuildSystem Artifact Storage — Build 件数保持の運用接続スライス

## 0. メタデータ

- type: `slice`
- status: `A`（A1 r3。A2 の 1 巡目 3 件、owner との協議、2 巡目（擬似 C′）の指摘を反映した。A3 は未実施で、本書はまだ凍結していない）
- branch: `codex/artifact-storage-build-retention-cleanup`
- implementation base commit: `cf7b77fefb4e84bbd8464732a3c3bccca10f0813`（`develop`。PR #118 を含む。A3 で凍結時の `develop` と照合し直す）
- implementation head commit: 未到達（Phase B で記録）
- risk: `high`（R2 の実 DELETE を初めて Build に開く。削除の可否・fail-closed・並行操作との競合を扱う。公開 API・asmdef・Unity は変えない）
- owner: OSM maintainers
- created: 2026-10-11
- expires: 2026-11-08 または置換 revision
- harvest to: 操作と契約を `tools/Artifacts/README.md`（「開発Buildの件数保持policy」節の拡張と新節）、program の状態と後続スライスへの移送を `docs/handoff/BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §4 B へ。完了時に本書を削除する。
- Phase A snapshot の取得案内・状態: 未生成（A3 凍結 commit の §0〜§5 を固定して生成する。C／C′ への固定入力の引渡し時に `review-evidence.md` に従い publish し、取得案内を残す）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成

入力の正本は program §2（開発Build・開発Content の保持、未完了uploadとstaging）と §4 B（件数保持の運用接続、診断の改善、ArtifactsRoot の件数清掃と `.staging` 回収、r7 レビュー由来の入力、PR #118 の後続への入力）、`tools/Artifacts/README.md` の Build／Content／件数保持 policy の各節、および現行コード。program で固定済みの契約は §1 に転記し、再議論しない。

## 1. 目的と対象外（A0 planning packet）

### 目的

program §2 が固定した Build の件数保持契約を、純粋 policy（PR #110）だけの状態から「実際に R2 から古い Build を消せる」運用経路へ接続する。最初の接続は Build 系列 1 つの手動実行に限り、削除の誤りが起きない方向（消し損ねは許し、消し過ぎは許さない）を検証で固定する。

### program で固定済みの契約（再議論しない）

- 成功 publish だけを系列ごとの最新 N 件に数える。N の既定値案は 10。失敗・未完了 publish は数えず、既存の成功を追い出さない。
- 利用中のものは削除しないが N には数える。保護だけで N を超える間は、超過を表示して許容する。
- 保護状態を収集できないときは削除しない（fail-closed）。
- 削除の主体は層ごとに別（R2、ArtifactsRoot、DIST cache）。DIST cache は対象外。
- 最低保存日数や owner の Close 承認は設けない。key ごとの削除承認も求めない。
- 通常 writer に bucket 管理権限を与えず、削除拒否を rule 解除で自動突破しない（program §2 日常操作）。
- Build の系列は `repositoryId/project/target/configuration`。branch ごとの枠は作らない。

### 現況（A0 で確認した事実。読取だけ）

**F1. R2 の object 一覧を取る経路は無い。**
- `Transport/R2ArtifactTransport.cs` の操作は `put`／`get`／`delete` の 3 つだけ（`Execute` と `ExecuteReset`）。`Transport/R2ArtifactTransport.psm1` も `Operation` を同じ 3 値に限る。`tools/` 全体に `ListObjects` 等の一覧呼出しは無い。
- `R2ArtifactTransport.psm1` の入口は `development/builds/v1/` と `development/content/v1/` の DELETE を拒否する（psm1 の条件式）。C# 側の `Execute` は `evidence/first-use/**/bundle.zip` 以外の DELETE を通すので、Build DELETE を開く変更は psm1 の入口だけで決まる。
- 既存 offline テストがこの拒否を固定している: `tests/BuildPublish.Tests.ps1` の「Build DELETE が入口で拒否される」assert（228〜230 行付近）と「`build delete` grammar を拒否する」assert（270 行付近）、`tests/Content.Contract.Tests.ps1` の Content DELETE 拒否（182〜186 行付近）。

**F2. 系列の全件一覧をローカル receipt だけで作る場合の限界。**
- Build の成功正本は `LocalApplicationData/OneStarMaker/Artifacts/builds-v1/<repositoryId>/<buildId>/receipt.json`。directory は **buildId 単位で、系列の階層を持たない**。系列の所属は各 receipt の `seriesId`／`project`／`target`／`configuration` を読んで初めて分かる。
- 同じ repo 直下に `<buildId>.reserved`（CreateNew の予約 marker）と、`<buildId>/reserved.lock`・`intent.json` がある。intent だけで receipt が無いものは成功ではない（送信不明を含む未完了）。
- `Read-BuildReceipt` は receipt の `configSha256` が**今回渡された build-config の bytes の hash と一致すること**を要求する。別の bytes の config で読むと、正しい成功記録も `receipt-invalid` になる。
- 別 PC の publish はこの PC の catalog に現れない。数えないので、R2 全体では系列が N を超えて残りうる（N は「この PC の catalog 上の成功」に効く）。消さない方向の誤差であり、誤削除は起こさない。
- receipt を失った object（store の消失、別 PC、旧試験物）は catalog に現れず、永久に削除候補にならない。回収は R2 上の孤児回収（後続）であり、本スライスは回収しない。
- store 自体が空（新しい PC 等）なら全系列が `within-limit` になり、何も消さない。

**F3. 手元の receipt 件数（2026-10-11、読取だけ）。**

| store | repositoryId 数 | receipt | 系列ごとの内訳 | その他 |
|---|---|---|---|---|
| `builds-v1` | 1 | 1 | `samplegame/windows-x64-player/il2cpp-high`: 1 件（buildId `b80d0ef6…`） | intent 1、`.reserved` 1、`reserved.lock` 1。receipt の無い intent は 0 |
| `contents-v1` | 1 | 2 | `bs4-spring-full/windows-x64-player`: 1 件、`spring-full/windows-x64-player`: 1 件 | intent 2、`publish.lock` 2。receipt の無い intent は 0 |

- どの系列も N=10 に遠く、実系列で実 DELETE が起きる状態ではない。実 DELETE の実観測には、件数を作れる検証用系列が要る（§5）。
- Build `b80d0ef6…` の buildId は Content revision `20260924T202227427Z-b80d0ef6…` の末尾と同じで、同じ BS4 Spring Build 由来である（Content 保護の手がかりの実例）。
- `transfers/` に private operation が 29 件残っている（公開・取得の残置。清掃は「未完了uploadとstaging」の後続）。
- ユーザー環境変数・process 環境変数の `OSM_ARTIFACTS_ROOT` はどちらも未設定。過去の collect は `--artifacts-root` を明示しており、**ツールは ArtifactsRoot の所在を後から知る手段を持たない**。本 A0 では ArtifactsRoot の中身を探索していない。

**F4. 「利用中」を判定する手がかりの所在。**
- **Build: 現在どこにも無い。** receipt は `inUse` を記録しない（README「開発Build 1件の保存と取得」と `BuildApplication.psm1` のコメント）。Build に use／release の入口は無い。`build fetch` は新規 private operation へ展開するだけで、どの Build を誰が使っているかを残さない。
- **ArtifactsRoot の `players/<…>/<buildId>/` は利用中の手がかりにならない。** `build collect` した Build は全て退避先を持つ（取り込み元であり利用の証拠ではない）ので、これを保護にすると全件が保護になる。
- **Evidence の利用中保護との違い:** Evidence は task state の artifact ごとに `protections`（`owner`／`consumer`／`reason`／`createdAt`／有限の `until`／`releasedAt`）を持ち、`evidence use`／`release` で付け外しする。fetch 中は一時的な operation-use を取る。`EvidenceRetentionPolicy` が `in-use`／`unknown-use` を判定し、不明は削除しない。これは task の終了・30 日期限と同じ store・guard に載っており、task を持たない Build にはそのまま使えない。考え方（有限 until、明示 release、不明は非削除、GET だけでは延長しない）は流用できる。
- **Content:** program §2 は「保持中の Player が受け入れうる Content revision」を保護とし、手がかりを `build collect` が退避先に書く `build-info.json` の `contentSet`・`contentTarget`・`buildIdentity`・`transportManifestSha256` と定めている。つまり Content の保護は **Build の保持結果（どの Player が残るか）と ArtifactsRoot の所在**に依存する。

**F5. R2 側の設定は未確認。** `development/builds/v1/` に bucket の lock・lifecycle rule があるかを AI は管理画面で読まない（README の reset 節の方針）。DELETE が 403（`ObjectLockedByBucketPolicy` 等）を返す可能性があり、その場合は rule を変えずに削除保留として扱う。bucket の versioning の有無も未確認で、有効なら GET 404 は「現行版が無い」ことだけを示し、非現行版の不在は示さない（A2 代替構成の指摘）。

**F6. 既存部品。** `BuildRetentionPolicy.psm1`（148 行）は `Get-BuildRetentionDecision -SeriesId -Builds -KeepCount` だけを公開し、file・R2・時計・store に触れない。`ArtifactApplication.psm1` に `Test-ArtifactMissing`（GET 404 `NoSuchKey` の判定）があり、Evidence 清掃と reset が「DELETE → GET 404 → tombstone」の確認に使っている。

### 要求（依頼の要約）

Build 系列 1 つについて、ローカル receipt から成功 publish の一覧と利用中保護を集め、`BuildRetentionPolicy` の判定で N 件を超えた古い Build だけを R2 から実 DELETE できること。失敗・保護不明・競合のときは何も消さないこと。

### 対象外（A0 で確定。所有先は §2）

- Content の実 DELETE（保護の収集源が Build の保持結果と ArtifactsRoot に依存するため、Build 側を先に成立させる）
- ArtifactsRoot の件数清掃（`players/`・`content/`）と異常終了で残る `.staging` の回収（層と削除主体が別。所在をツールが知らない＝F3）
- 系列の退役（イベント終了時の local・R2・統合フォルダ・DIST cache の片付け）
- 自動起動（Task Scheduler 等への接続）。本スライスは人または Agent が明示実行する CLI だけ
- 診断の改善（失敗時の `publish` 表示、`existing` 結果の `objects`、照合結果の記録、content store の ACL 付け直し、相対 path 240 字超の `unconfirmed`、build-config の二度読み、`stage-conflict` の敗者）
- R2 上の孤児 object の回収、別 PC の catalog 復元、R2 一覧の追加
- DIST cache（常に対象外）
- Unity 側の変更、`BuildRetentionPolicy` の契約変更

### 常時契約のうち本スライスに効くもの

- テストで `Task.Delay`／`Thread.Sleep` を使わない（並行は signal 同期、時刻は注入）。
- 参照 0 を削除理由にしない（本スライスは R2 object を削除するが、コードの未使用 API は削らない）。
- Artifact Storage の通常操作は Artifacts README の事前承認に従う。ただし本依頼では、実 R2 操作と資格情報アクセスは必要と分かった時点で owner に 1 行で許可を求める（Phase A では実行しない）。
- Unity の実行・テスト・Build は Phase C の責任（本スライスは Unity を変えない）。
- PR の base は `develop`。ブランチは `codex/` で始める。

### 未決事項（A1 で案を出し、A3 で決める。r2 時点の状態は §5 の A2 表）

- U1. Build の利用中保護の収集源を何にするか（F4 のとおり現在は無い）。
- U2. Build DELETE を transport 入口でどう開くか（無条件に開くか、呼出し元を限定する印を要求するか）。
- U3. 削除の途中状態（DELETE 送信後・404 確認前）をどこに記録し、次回どう扱うか。
- U4. N をどこで決めるか（定数・設定・CLI 引数）。
- U5. 実 DELETE の実観測を、どの系列で、誰が、どの許可で行うか。
- U6. catalog が読めない記録（破損 receipt、config 不一致、未知の entry）を見つけたときの停止範囲。
- U7. tracked HANDOFF のまま進めるか、H1/CURRENT を採るか。

## 2. 意思決定と受け入れ境界

- **このスライスが答える問い:** Build 系列 1 つについて、ローカルの receipt から成功 publish の一覧と利用中の保護を集め、`BuildRetentionPolicy` の判定で N 件を超えた古い Build だけを R2 から実 DELETE でき、失敗・保護不明・競合のときは何も消さないか。
  - 採否: owner 指定の候補をそのまま採る。Content の実 DELETE は保護の収集源が「Build の保持結果」と「ArtifactsRoot の所在」に依存し（F4）、Build 側が成立する前に束ねると、Content 側の失敗が Build の削除経路の欠陥か、保護収集の欠陥かを分けて判定できない。ArtifactsRoot 清掃は削除主体が別の層で、所在をツールが知らない（F3）未知を抱える。自動起動は手動経路の正しさが前提。いずれも後続へ分ける。

- **進める最低条件**（満たせば GO。判定は §5 の検証経路）:
  1. **収集:** 指定した 1 系列について、この PC の store の Build 記録を読み、成功・失敗/未完了・削除中・削除済みを分類した snapshot と、各成功 Build の利用中保護を集めて `Get-BuildRetentionDecision` に渡せる。指定系列に属する記録、または系列を判定できない記録・保護が 1 件でも読めなければ、その系列は何も消さずに理由を返す。他系列の記録の破損では止めない。build-config の bytes が変わっても、正しい成功記録は数えられる。
  2. **保護:** Build に有限期限付きの利用中保護を付け外しでき（`build use`／`build release`）、有効な保護を持つ Build は N を超えても削除されず N に数えられる。保護だけで N を超える間は `excessCount` を表示して正常終了する。時計が巻き戻っても、付けた保護は無効にならない。
  3. **削除:** 系列 lock の下で削除直前に snapshot と保護を取り直して policy を再評価し、なお削除候補の Build だけを、削除意図を記録 → R2 DELETE → GET が 404 `NoSuchKey` → 削除済み記録、の順で消す。404 を確認できない限り削除済みとしない。途中で止まったものは次回、**その時点でも候補である場合だけ**削除を続け、候補でなくなっていれば削除意図を取り下げて通常の計数へ戻す。
  4. **消さないこと:** 失敗・未完了 publish、保護中、保護不明、この PC での取得中、lock 競合、DELETE の 403/timeout/応答不明、catalog 不完全、時計のずれ（未来時刻の記録）のいずれでも、該当 Build（不完全・ずれなら系列全体）を消さない。Content の key は従来どおり DELETE を拒否する。
  5. **削除後の照会:** 削除済み・削除中の Build について、`build inspect` と `build collect` は成功（`passed`）を返さず `payload-deleted`／`delete-pending` を返す。引き継ぐ側が消えた reference を保存済みと誤認しない。
  6. **実 R2:** 実系列の dry-run で当日の snapshot どおりの分類を表示し、検証用系列で N+1 件を publish したうえで実行して、最古の 1 件だけが DELETE・404 確認・削除済み記録となり、再実行では DELETE・GET を送らないことを、通信回数を含む生結果で残す。判定 C の責任とし、実行前に owner の許可を得る（§5）。DELETE が 403 なら未達。

- **受け入れ条件**（最低条件の観測可能な詳細）:

  | # | 条件 | 確認 |
  |---|---|---|
  | AC1 | `build cleanup --profile osm --config <build-config> --project <p> --target <t> --configuration <c> [--dry-run]` は 1 系列だけを対象にする。系列 token は `Get-BuildSeries` の規則で検査し、不正なら何も読まず `invalid-build-identity`。 | offline |
  | AC2 | catalog は `builds-v1/<repositoryId>/` 直下を列挙し、`<32hex>` directory、`<32hex>.reserved`、本スライスが作る `locks/` だけを既知とする。directory ごとに、receipt が成功記録として検査を通れば成功（`publishedAt` は receipt の値）、receipt が無く intent が読めれば未完了（intent の系列で分類）、どちらも無ければ系列不明の未完了とする。 | offline |
  | AC3 | 成功記録の検査は **今の build-config の hash に依存しない**。BuildStore に、既存 `Read-BuildReceipt` の**全検査から `configSha256` と `evidenceConfigSha256` を今の config と照合する 2 項目だけを除いた**読取を足す（両 field が 64 hex であることは検査する）。repositoryId は呼出しの config から取る。検査の本体は 1 箇所に置き、既存 reader はそれに config 照合を足したものにする（二重実装にしない）。`inspect`／`fetch` の既存の判定は変えない。破損 field ごと（entries の型・重複・size、hash 形式、readback の HTTP・終了状態、intent の不一致）に非計数・blocked をテストする。 | offline |
  | AC4 | 次のどれかがあれば、指定系列を `status=blocked` とし、DELETE も記録書込みもしない: (a) 指定系列に属する記録（receipt・intent・保護・削除記録）が検査に失敗、(b) receipt があるが系列を読めない記録、(c) 未知の entry、repo 直下の reparse・ACL 不一致・列挙失敗。理由は `catalog-incomplete`／`protection-unknown`／`delete-record-invalid` のいずれか。**他系列に属すると読める記録の破損では止めない**（件数だけ `foreignInvalid` に出す）。 | offline |
  | AC5 | 系列不明の未完了（receipt も読める intent も無い）は成功ではないので blocked にしない。結果の `held` に `publish-incomplete` として件数だけ出す。 | offline |
  | AC6 | 指定系列に `publishedAt` が現在より未来の成功記録があれば、系列を `clock-skew` で blocked にする。「最新」は記録した `publishedAt` の順と定義し、時計が巻き戻っていた間に確定した publish の順序は保証しない（README の known limits）。 | offline |
  | AC7 | 保護は `build use --profile osm --config <build-config> --build-id <id> --consumer <id> --until <UTC> --reason <text>` で付け、`build release --profile osm --config <build-config> --build-id <id> --protection <id>` で外す。repositoryId は config から取る。use は、成功記録を読んで系列を決め、**その系列 lock を取った後に成功記録と削除状態を読み直し**、保護記録の rename まで lock を保持する（lock 前の判定で書かない）。記録の形は Evidence の保護に揃える（`id`・`owner`（SID）・`consumer`・`reason`・`createdAt`・`until`、解除は `releasedAt`）。`until` は現在より後で、現在から 90 日以内。`consumer`／`reason` は Evidence の use と同じ字種・長さ規則。記録は `<buildId>/protections/<protectionId>.use.json` と `.release.json` の追記だけで、既存記録を書き換えない。成功 receipt の無い Build、削除意図のある Build、削除済みの Build への use は拒否する（`build-not-retainable`）。 | offline |
  | AC8 | 保護の有効判定（注入した時計 `now`）: release 記録があれば無効。無ければ、`now < until` なら有効。`createdAt > now`（時計の巻戻り）も有効として扱う。読取時に `createdAt < until`、`until − createdAt ≤ 90 日`、`releasedAt ≥ createdAt`、UTC（offset 0）を検査し、満たさない記録は `protection-unknown`。 | offline |
  | AC9 | N は本スライスでは定数 10（`BuildCleanup` 内の 1 箇所。テストだけ注入可能）。CLI・build-config から変えられない。 | offline |
  | AC10 | `--dry-run` は lock を取らず、記録・network に触れずに、`retained`／`candidates`／`held`／`deleting`／`deleted` の件数と ID、`excessCount`、`reasonCode` を返す。実行時と同じ catalog と policy を使う。 | offline |
  | AC11 | 実行時は `builds-v1/<repositoryId>/locks/<project>~<target>~<configuration>.lock` を FileShare.None・非継承で開いた handle を系列 lock とし、取れなければ `cleanup-in-progress` で何もしない。`build use` も同じ lock を取る。lock は process 終了で解放され、永続の予約は作らない。 | offline（別 process を signal で同期） |
  | AC12 | lock 取得後に catalog と保護を**取り直して**policy を再評価し、その時点の候補だけを扱う。lock 前の dry-run 結果を使わない。 | offline |
  | AC13 | `<buildId>/access.lock` は BuildStore の 1 関数が「無ければ CreateNew、あれば Open」で扱う（既存 Build にも後から作れる。作成時の `reserved.lock` は予約の意味なので流用しない）。`build fetch` はこの PC に同じ buildId の directory があれば、取得の開始から終了まで FileShare.ReadWrite で保持する。cleanup は FileShare.None で開けなければその Build を `in-transfer` で飛ばし、開けたら削除意図の書込みから削除済み記録（または取下げ）まで保持する。**保護の範囲はこの PC の取得だけ**で、別 PC・Cloud の取得は観測しない（README の known limits）。 | offline（別 process を signal で同期） |
  | AC14 | 削除は候補ごとに直列で、古い順。削除の試行は `<buildId>/deletion/<n>.intent.json`（n は 1 からの連番。key・packageSha256・意図時刻）を不変記録で書く → transport の DELETE → 同じ key の GET が `Test-ArtifactMissing` を満たす → `<n>.deleted.json` を不変記録で書く。`deleted.json` は意図の hash と、DELETE と確認 GET の非秘密の観測（operation・key・generation・httpStatus・statusClass・s3Code・timedOut・redirected・observedAt。endpoint・本文は含めない）を持ち、C′ が記録だけから「この key を消して 404 を見た」を再判定できるようにする。DELETE が 2xx でも GET が 404 で無ければ `delete-unconfirmed` で止め、削除済みとしない。 | offline |
  | AC15 | DELETE または GET が 403・timeout・redirect・応答不明・generation 変化のとき、その Build は `delete-pending` として残し、同じ run で再送しない。系列の残りの候補も処理しない（順序を保つ）。結果は `status=partial`、exit 2。 | offline |
  | AC16 | 未解決の試行（intent があり、deleted も abandoned も無い）がある Build は、成功件数にも保護にも数えず（`deleting`）、次回の実行で最初に GET で確かめる。GET が 404 なら `<n>.deleted.json` を書く。GET が 200 なら、**その Build を成功行として戻し**（保護は取り直す）、系列の他の成功行と合わせた完全な snapshot で policy を計算し直し、access lock を取れて**今も候補の場合だけ** DELETE を送り直して AC14 と同じ確認をする。候補でなければ `<n>.abandoned.json` を書いて試行を取り下げ、その Build を通常の計数へ戻す。GET が判定できなければ `delete-pending`。 | offline |
  | AC17 | 削除済み記録のある Build は snapshot から除き（成功に数えない）、`deleted` に件数を出す。receipt・intent・削除記録は残し、ローカル記録は削除しない。 | offline |
  | AC18 | 1 回の実行で送る DELETE は、送り直しを含めて最大 20 回。残りは `remaining` に件数を出して次回へ回す。時間予算は 600 秒で、`New-ArtifactRequest` の Timer（Stopwatch）で transport の各呼出し deadline、記録の書込み、確認 GET の全てに効かせる（Evidence 名の予算には依存しない）。期限は、各 DELETE の送信前、削除意図・削除済み・取下げの各記録の rename 直前、結果の確定時に判定する。超過したらその記録を書かず `budget-exceeded`（GET 404 を見た後でも削除済み記録を書かない。次回の実行が GET から確かめて記録する）。その時点の Build は AC15 と同じ扱いで、exit 2。境界ごとに fault hook のテストを置く。 | offline |
  | AC19 | transport の入口の許可判定を純粋関数（例 `Test-R2ArtifactRequestAllowed`）に切り出して export する。`development/builds/v1/…/bundle.zip` の DELETE は request に `BuildRetentionDelete=$true`（bool）がある場合だけ許す。その印を付けるのは `BuildCleanup` だけ。Content の DELETE、印の無い・bool でない印の Build DELETE は従来どおり拒否する。`build delete` という grammar は作らない。既存の `BuildPublish.Tests.ps1` の拒否 assert は変えずに残す。 | offline（純粋関数の表検証） |
  | AC20 | 結果 JSON は非秘密の固定 field（`status`、`reasonCode`、`seriesId`、`keepCount`、`excessCount`、各分類の ID 配列と件数、`deleted`、`remaining`、`foreignInvalid`、Build ごとの `outcome`、操作ごとの送信回数 `requests`（`delete`・`get`））だけを持つ。endpoint、ローカル path、例外本文、SDK 応答本文を出さない。exit は 0=完了（`within-limit`・`protected-over-limit` を含む）、1=blocked/失敗、2=partial。 | offline |
  | AC21 | `build inspect`／`build collect` は、削除状態の確認から結果を返すまで `<buildId>/access.lock` を FileShare.ReadWrite で保持する（cleanup は削除意図の書込み前に FileShare.None で開くので、確認と結果の間に削除意図は確定しない。返す結果はその時点の状態）。`build inspect` は、削除済みなら `status=failed`・`reasonCode=payload-deleted`、未解決の試行があれば `delete-pending` を返し、reference を返さない。`build collect` は既存 receipt の分岐で同じ判定をし、`existing` の成功を返さない（再 PUT もしない）。`publish`・`fetch` は AC13 の access lock 以外の挙動を変えない。既存 receipt の形式は変えない。 | offline |
  | AC22 | README に運用を書く: N は 10 で、消したくない Build は `build use`（最長 90 日）で守るか N を増やす（現状はコード変更）。系列は branch や人で分かれず枠を共有する。削除は書込み鍵のある PC だけが行い、Cloud 等の読み取り専用 token では行わない。別 PC・Cloud の利用と時計巻戻り中の順序は観測・保証しない。削除済みは `payload-deleted` で分かる。 | docs-audit と目視 |
  | AC23 | 実 R2: 実系列の dry-run（期待値は当日の catalog snapshot から決める）、空であることを確かめた検証用系列（§5）で N+1 件 publish → 実行 → 最古 1 件だけ DELETE・404・削除済み記録 → `build inspect` が `payload-deleted` → 再実行で `requests` が DELETE 0・GET 0。 | 判定 C（owner 許可後） |

- **ここでは答えない問いと所有する後続スライス:**

  | 問い | 所有 |
  |---|---|
  | Content の実 DELETE、保持中 Player（ArtifactsRoot の `build-info.json`）からの Content 保護収集、R2 の Build と ArtifactsRoot の Player のどちらを「保持中」とみなすか、確定記録との関係 | 新規「Content 件数保持の運用接続」（program §4 B に登録） |
  | ArtifactsRoot の `players/`・`content/` の件数清掃、`.staging` 回収、ArtifactsRoot の所在の登録 | 既存「ArtifactsRoot の件数清掃と `.staging` 回収」（program §4 B の項目を独立スライスとして登録） |
  | 系列の退役 | 「件数保持の運用接続」の残り（Content 側と同時に扱う） |
  | 自動起動（scheduler 接続）、N を系列ごとに設定可能にするか | 新規「件数清掃の自動起動」。Evidence の固定 runtime と同じ方式で足すかを着手時の A1 で決める |
  | 別 PC・Cloud と共有する保護（R2 上の保護記録）、Evidence が参照する Build の保護、Cloud に削除権を渡さないこと | program §4 C（別 host／Cloud）への入力 |
  | 単調な publish 順序の記録（時計巻戻りへの耐性） | Content 確定記録への入力 |
  | 「DELETE → GET 404 → 記録」の共通 primitive 化（Evidence 清掃・reset・本スライス・将来の Content） | Content 件数保持の運用接続の A1 への入力 |
  | 長期保存（リリース保存） | program が別責務とした未作成の保存。今回作らない |
  | 診断の改善（§1 対象外の一覧） | 既存「診断の改善」 |
  | 別 PC の publish を数える、R2 一覧、孤児回収（store を失った PC の Build を含む） | 既存「Contentの容量上限」の孤児回収と、新規の R2 一覧の要否（program §4 B へ入力として登録） |

- **program §4 B に溜まった入力の仕分け（A1 案）:**

  | 入力（出典） | 本スライスで決めること | 後続へ送るもの |
  |---|---|---|
  | 保護の収集源の範囲（r7 レビュー） | Build の収集源は、この PC の `builds-v1` にある明示保護記録と取得中の access lock だけ。`players/` の外の配布済み Player や他 machine の Build は収集源に入らない。「収集できない」（記録の破損・読めない）は blocked、「存在しない」（記録が無い）は保護なし、と区別する（AC4・AC8）。別 PC・Cloud と共有する保護は §4 C へ（owner 合意） | 保持中 Player からの Content 保護収集と、DIST cache の再取得元である ArtifactsRoot の削除規則との関係 → Content 件数保持の運用接続 |
  | 保護が N を超えた場合（r7 レビュー、件数保持の運用接続） | Build は §2 の超過条項をそのまま適用（保護は外さず `excessCount` を表示、exit 0） | Content が同じ条項を引き継ぐかの確認 → Content 件数保持の運用接続 |
  | 件数保持がどの層に効くか（件数保持の運用接続） | Build の件数保持は **R2 の object にだけ効く**。ローカルの成功記録は消さず削除済みとして残し、ArtifactsRoot の退避先には触れない | ArtifactsRoot の件数清掃 → 同名スライス。DIST cache → 対象外のまま |
  | Content の実 DELETE（件数保持の運用接続） | 決めない。Content key の DELETE 拒否を維持する（AC19） | Content 件数保持の運用接続 |
  | 系列の退役（件数保持の運用接続） | 決めない | 件数保持の運用接続の残り |
  | ArtifactsRoot の件数清掃、`.staging` 回収（§4 B） | 決めない。触れないことをテストで固定する | ArtifactsRoot の件数清掃と `.staging` 回収 |
  | 失敗時の `publish` 表示、`existing` 結果の `objects`、照合結果の記録、content store の ACL 付け直し、240 字超の `unconfirmed`、config 二度読み、`stage-conflict` の敗者（診断の改善、PR #118） | 決めない。新しい `build cleanup` の結果だけは、最初から Build ごとの `outcome` と送信回数 `requests` を持たせる（AC20） | 診断の改善 |
  | 準備 helper 失敗による未記録・未送信 private 残置（PR #111） | 決めない。catalog は receipt も intent も無い directory を系列不明の未完了として数えるだけ（AC5） | 未完了uploadとstaging |
  | R2 側に上書き防止が無い・別 PC の同時公開（PR #118） | Build の削除も同じ前提で、別 PC の writer・cleanup との競合は防がない（同じ Build を別 PC から削除する運用は想定しない）。README の known limits に書く | Content 確定記録 |

- **判定定義:** GO = 最低条件 1〜6 を判定必須テストと AC23 の生結果（最終 head）で確認し、阻害 findings が無い。NO-GO = いずれかの最低条件が未達、または誤削除（候補でない Build、保護中、Content key への DELETE 送信）か、削除済み Build を成功として返す照会が 1 件でも観測された。CONDITIONAL ACCEPT は使わない（スパイクではない）。
- **停止規則:** 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- **A3 後の例外承認:** なし（A3 未実施）
- **本文へ転記した実装制約:**
  - `#nullable enable`・`record` 禁止は Unity 側の C# の規則で、本スライスは Unity C# を変えない。C# の transport・packaging も変えない。
  - テストで時間待機を使わない。並行は別 process を signal で同期し、時計・transport・読戻しは注入する。
  - 公開 reason は許可語だけ。例外本文・path・endpoint を出さない（既存の Build／Content の規則）。
  - 資格情報は既存の `Invoke-CredentialTransport` 経由だけで使い、鍵を引数・ログ・ファイルへ出さない。
  - 通常 writer に bucket 管理権限を足さず、403 を rule 変更で突破しない。
  - 既存の成功記録（`receipt.json`・`intent.json`）を書き換えない。追加する記録も不変（CreateNew + rename）で、既存名を置換しない。
- **未決事項:** U1〜U7 は r2 で案を反映済み（U1・U3 は §3、U2 は AC19、U4 は AC9、U5 は §5、U6 は AC4、U7 は §5）。A3 で凍結する。

## 3. 責務マップ

| ファイル | 責務（一文） | 変更理由 | 所有者・寿命 | 依存（入力 → 出力） | 種別 | 公開面 | テスト境界 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|---|---|
| `BuildCatalog.psm1`（新規） | `builds-v1/<repo>/` を読み、1 系列の policy 入力行と、削除中・削除済み・blocked 理由・他系列の破損件数を作る | store の配置や記録の種類が変わったとき | 状態を持たない。読むのは BuildStore が所有する記録 | BuildStore（config 非依存の成功記録の読取、削除記録・保護記録の読取）、BuildProtection（有効判定）→ 行の配列 | 永続化の読取 + 分類 | 内部 `Get-BuildSeriesSnapshot` | 注入 root の fixture で全分類・全 blocked 理由を offline に検証 | 0 → 約 170 |
| `BuildProtection.psm1`（新規） | 保護記録の形の検査と、注入した時計による有効判定（純粋） | 保護の規則（期限・字種・解除・時計ずれ）が変わったとき | 状態を持たない | ArtifactCommands（`Assert-Utc` 等）→ 判定 | Policy（純粋） | export `Test-BuildProtectionRecord`、`Get-BuildProtectionState` | 表で期限境界、巻戻り、90 日、release、破損 | 0 → 約 80 |
| `BuildCleanup.psm1`（新規） | 系列 lock の下で catalog → policy → 候補ごとの試行（intent・DELETE・404 確認・deleted／abandoned）を順序どおりに進め、結果を集計する | 削除の手順・予算・上限・結果の形が変わったとき | 状態は持たず、記録は BuildStore が所有。lock は実行中の process だけが持つ | BuildCatalog、BuildRetentionPolicy、BuildStore、ArtifactApplication（network・`Test-ArtifactMissing`・`New-ArtifactRequest`）、Credentials → 結果 | orchestration | `Invoke-BuildCleanup` | 注入 transport・時計・fault hook で全失敗経路、別 process の lock 競合 | 0 → 約 250 |
| `BuildStore.psm1` | Build directory 配下の記録の配置・不変書込み・検査・排他（既存）に、config 非依存の成功記録の読取、削除試行と保護の記録、系列 lock・access lock を足す | 記録の種類が増えるため | Build application（既存のまま） | 既存どおり | 永続化 | `Write-BuildImmutable` の許可を「固定名 + `deletion/<n>.(intent\|deleted\|abandoned).json` + `protections/<32hex>.(use\|release).json`」へ。`Read-BuildSuccessRecord`、`Read-BuildDeletionState`、`Read-BuildProtectionRecords`、`Enter-BuildSeriesLock`、`Enter-BuildAccessLock` を追加 | 既存 + 新記録の検査 | 135 → 約 230（+70%） |
| `BuildApplication.psm1` | publish／inspect／fetch／collect（既存）に、`use`／`release` の手順、inspect／collect の削除状態の判定、fetch の access lock を足す | AC7・AC13・AC21 | 既存 | + BuildStore、BuildProtection | orchestration | `Invoke-BuildUse`、`Invoke-BuildRelease` を追加 | 既存 suite + 新ケース | 240 → 約 310（+29%） |
| `BuildRetentionPolicy.psm1` | 変更しない | — | — | — | Policy | — | 既存 suite | 148 → 148 |
| `Transport/R2ArtifactTransport.psm1` | 入口の許可判定を純粋関数に切り出し、Build DELETE を印付きの request だけ通す | AC19 | — | — | adapter 入口 | export `Test-R2ArtifactRequestAllowed` を追加 | 純粋関数の表検証 | 46 → 約 65 |
| `tools/artifacts.ps1` | `build cleanup`／`use`／`release` の grammar | 入口の追加 | — | → 上記 module | 入口 | CLI | grammar の拒否・必須引数 | 約 +20 |
| `tests/BuildCleanup.Tests.ps1`（新規）、`tests/BuildCleanupChild.ps1`（新規） | AC1〜AC22 の offline 検証 | — | テスト専用 | 注入 hook | テスト | — | — | 0 → 約 500、約 60 |
| `tests/BuildPublish.Tests.ps1` | 変更しない（印の無い Build DELETE の拒否 assert はそのまま成立する） | — | — | — | テスト | — | — | 286 → 286 |
| `tools/Artifacts/README.md` | 新節「開発Buildの件数清掃（build cleanup / use / release）」、policy 節の「未接続」の記述更新、inspect の `payload-deleted`、運用と known limits（AC22） | 公開面 | — | — | 文書 | — | docs-audit | 275 → 約 320 |

**行数・責務の警報と判断:**
- `BuildStore.psm1` は +70% で 50% 警報が発火する。**非分割**: 追加は全て「Build directory 配下の記録とその排他」で、同じ変更理由・同じ所有者・同じ ACL 規則・同じテスト方法に属する。別 module に出すと `Write-BuildImmutable` の許可と ACL 検査を二箇所に持つことになる（A2 ARCH-2 の指摘どおり、二箇所に持つと腐る）。230 行は 500 行警報の半分以下。
- `BuildApplication.psm1` は +29%、約 310 行で警報は発火しない。use／release は既存の入口手順（publish／collect）と同じ層に置き、BuildProtection を純粋判定だけに保つ（A2 ARCH-3）。
- `BuildCleanup.psm1` は orchestration だけに留め、分類（catalog）と選別（policy）と記録（store）と保護判定（protection）を持たない。300 行を超える見込みが出たら Phase A へ戻す。
- tools/Artifacts 全体: Build 系は Contract／Stage／Store／Application／RetentionPolicy の 5 file に 3 file が加わる。Evidence 系と同じ「純粋 policy・store・orchestration を分ける」配置に揃える。Evidence と Build の清掃 engine は共通化しない。所有者（task store と Build store）、寿命（task 終了 30 日と件数）、guard が違い、統合すると一方の変更が他方へ波及するため（A2 で妥当と確認）。「DELETE → GET 404 → 記録」の小さな手順だけが複製になり、共通化は後続の入力とする。

**DELETE の所有者:** R2 の Build object を消す主体は `BuildCleanup` だけ。transport の印（AC19）はその境界をコード上に残すためで、権限ではない（同一ユーザー内の信頼が前提）。ローカルの記録は消さない。ArtifactsRoot と DIST cache には触れない。

**保護の収集源（U1）:** この PC の明示した保護記録だけ（AC7・AC8）と、この PC での取得中の access lock（AC13）。採らない案: (a) 保護源なし（全 `inUse=false`）は「利用中は消さない」契約を満たせない。(b) ArtifactsRoot の退避先の存在は全件保護になる（F4）。(c) fetch の履歴は「使い終わった」が分からず、期限の無い保護になる。(d) Evidence の task store への相乗りは所有者と寿命が違う。(e) R2 上の共有保護は別 PC・Cloud の経路と一緒に §4 C で設計する（owner 合意、2026-10-11）。

**途中状態（U3）:** Build directory 配下の `deletion/<n>.*.json` の不変記録。試行を連番にするのは、取下げ（abandoned）後に再び候補になった場合も、既存記録を置換せずに新しい試行を書けるようにするため。

## 4. 実装計画

- **変更対象:** §3 の表のとおり。新規 3 module、既存 4 file の小変更、テスト 2 新規・1 変更、README。
- **順序:**
  1. `BuildStore` に config 非依存の成功記録の読取、削除試行・保護の記録、lock を追加し、既存 suite が通ることを確認する。
  2. `BuildProtection`（純粋な検査と有効判定）とその表テスト。
  3. `BuildCatalog` と分類・blocked・他系列破損のテスト。
  4. `BuildCleanup`（dry-run → 実行 → 試行の再開・取下げ）と失敗経路のテスト。
  5. transport の許可判定の純粋関数（AC19）、`BuildApplication` の use／release・inspect／collect の削除状態・fetch の access lock、CLI grammar。
  6. README。`pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1`。
- **Phase B から Phase A へ差し戻す条件:** 計画外の永続記録・lock・公開 CLI が必要になった、`BuildRetentionPolicy` の契約変更が必要になった、catalog を repo 単位でなく系列単位に区切らないと性能・正しさが成り立たない、transport の C# 変更が必要になった、`BuildCleanup` が分類や記録の責務を持たないと書けない、既存の receipt 形式の変更が必要になった、config 非依存の成功記録の検査が AC3 の項目だけでは成り立たない。
- **対象外を維持する方法:** Content の key・store・ArtifactsRoot・`transfers/` を参照するコードを `BuildCleanup`／`BuildCatalog` に置かない。テストで Content key への DELETE が 0 回であること、ArtifactsRoot fixture が不変であることを assert する。

## 5. テストとレビュー計画

- **単体テスト（offline、`tests/BuildCleanup.Tests.ps1`）:**
  - catalog: 成功／未完了／系列不明／他系列の混在、指定系列の破損 receipt、**config bytes を変えた後も旧成功を数える**、他系列の破損では止めず `foreignInvalid`、未知 entry、reparse、ACL 不一致、未来時刻の `publishedAt` で `clock-skew`、削除中・削除済み・取下げ済み（AC2〜AC6、AC16、AC17）。
  - 保護: 期限境界（`until` ちょうど）、**時計の巻戻り（`createdAt > now`）でも有効**、release 済み、`createdAt ≥ until`・90 日超・`releasedAt < createdAt` の破損、削除意図あり・削除済み・receipt の無い Build への use（AC7、AC8）。
  - 削除: dry-run が何も書かない、候補だけを古い順に削除、保護が N を超える場合の `excessCount` と非削除、DELETE 2xx でも GET 200 なら `delete-unconfirmed`、403／timeout／応答不明で `delete-pending` と後続の非処理、再開時の GET 404 で削除済み、**再開時の GET 200 で今も候補なら再送、候補でなければ（保護の期限切れで別 Build が押し出された等）取下げて計数へ戻す**、送り直しを含む DELETE 上限 20 回、予算超過、`requests` の回数（AC9〜AC10、AC12、AC14〜AC18、AC20）。
  - 照会: 削除済み・削除中の Build への `inspect`／`collect` が `payload-deleted`／`delete-pending` で reference を返さず、PUT もしない（AC21）。
  - 競合: 別 process が系列 lock を保持中の `cleanup-in-progress`、`use` と `cleanup` の競合、fetch が access lock を保持中の `in-transfer`、cleanup が access lock を保持中に fetch が待たずに失敗すること、lock 取得と同時に保護が付いた場合に再評価で外れること（AC11〜AC13）。
  - 境界: `Test-R2ArtifactRequestAllowed` の表（印あり・なし・非 bool、Build・Content・Evidence の key）、Content store・ArtifactsRoot fixture の不変（AC19、§4）。
- **差し戻し中の起点 `-Filter`:** `BuildCleanup.Tests.ps1 -Case '*'`、`BuildRetention.Tests.ps1 -Case '*'`、`BuildPublish.Tests.ps1 -Case '*'`、`BuildCollect.Tests.ps1 -Case '*'`。C は凍結済み条件の確認に必要なら根拠を記録して変えてよい。
- **判定必須テスト（GO 候補 head）:** 3 つの .NET Release build（Probe・Packaging・Transport）、`tools/Artifacts/tests/*.Tests.ps1` の全 suite を `-Case '*'` で、Workflow `TaskLifecycle`、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、および AC23 の実 R2 生結果（最終 head）。
- **全 EditMode 回帰の適用除外:** 適用除外。理由: Unity の source・asmdef・Assets を変更しない（変更は `tools/` と文書だけ）。代替証拠: 全 Artifacts offline suite の結果 JSON（registered／selected／executed の件数一致、failed 0）と、diff に `unity/` が含まれないことの機械確認。
- **統合・Unity テスト:** なし（Unity を変えない）。
- **操作・実行時・目視条件の検証経路:**
  - AC23（実 R2）: 担当は判定 C。最終 head で行う（古い head の結果を流用しない）。環境は owner の Windows ユーザー、既存の `osm` 資格情報と登録済み Evidence config・build-config。初期状態は当日の store。操作: (1) 実系列 `samplegame/windows-x64-player/il2cpp-high` の `build cleanup --dry-run`（期待値は直前に読んだ catalog の件数から決める。F3 時点では 1 件で `within-limit`）。(2) 検証用系列（project `retention-probe`、target `windows-x64-player`、configuration は**試行ごとに新しい** `probe-<n>`（n は 1 から）。非秘密の小さな合成 file 1〜2 個）について、まず dry-run で系列が空（成功 0）であることを原記録に残し、11 件を `build publish` して `build cleanup` を実行。期待する削除 ID は、11 件の receipt から既存 policy の全順序（`publishedAt` 降順、同時刻は buildId の Ordinal 昇順）で算出した最古の 1 件。(3) 同じ実行をもう一度。観測: (2) で最古 1 件だけが `deleted`、`deletion/1.intent.json`・`1.deleted.json` がある、その Build の `build inspect` が `payload-deleted`、`build fetch` が `download-unconfirmed`、残り 10 件の fetch が hash 一致、`requests` が DELETE 1・GET 1。(3) で `requests` が DELETE 0・GET 0。(4) owner に bucket の versioning の有無を 1 回、外部テキストで確認してもらう（AI は管理画面を読まない）。保存する証拠（非秘密の原記録そのもの。hash だけにしない）: 実行前の dry-run 結果、11 件の `receipt.json`・`intent.json`、`deletion/` 配下の全記録、cleanup の結果 JSON 2 回分、inspect の結果、残り 10 件の fetch 結果と、取得した bundle の SHA-256 を receipt の `packageSha256` と照合した機械検査の出力、owner の確認テキスト。endpoint とローカル path は収録前に除く。evidence bundle に入れて C／C′ に渡す。
  - 検証用系列は退役の手段が無い（対象外）ため、R2 に小さな object が最大 11 件（DELETE が 403 の場合）残る。owner が受け入れた（2026-10-11）。DELETE が 403 なら最低条件 6 は未達で、rule は変えない。最終 head が変わって実証をやり直すときは新しい `probe-<n>` を使い、試行ごとに最大 11 件が増える。やり直しは実行前の許可依頼で残置の累計を示す。
- **未知の操作経路の疎通結果と確認地点:** Build key への実 DELETE と、DELETE 直後の GET が 404 `NoSuchKey` を返すことは、本 bucket・本資格情報では**未確認。初回確認は Phase C**（AC23）。理由: Phase A では実 R2 を操作しない（owner の制約）。Evidence の reset で `evidence/first-use/**` の DELETE→GET 404 は成立済みだが、`development/builds/v1/` に bucket rule があるかは未確認（F5）。不成立時: 403 なら rule を変えずに `delete-pending` が正しく出ることを確認したうえで、owner に設定の状態を確認し、問いを「Build DELETE が bucket 設定上可能か」に分けるかを A3 revision で決める。
- **人間の判断が必要な条件:** AC23 の実行許可（owner。実行直前に 1 行で許可を求める）（残置の受け入れは合意済み）と、versioning の有無の確認テキスト。合否は結果 JSON と記録の機械検査で判断し、人間の目視判断は求めない。
- **機械検査:** `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、全 Artifacts PowerShell の parse。
- **H1/CURRENT 方式（U7）:** **採らない（tracked HANDOFF で進める）**。owner 合意（2026-10-11）。理由: 直前の Build／Content スライス（PR #111・#115・#118）が tracked HANDOFF で完了しており、H1 の固定 5 step は `artifact-evidence-lifecycle` 用に承認された集合で、Build 清掃の suite を足すには承認仕様の改訂が別途要る。採る場合の利点（B exit の機械記録、Git への raw 追加の拒否）は、本スライスでは判定必須テストの生結果を evidence bundle に入れることで代替できる。A3 で owner が決める。
- **A0/A1 主担当・モデル・ベンダー:** Claude Opus 5.5／Anthropic（Claude Code desktop、本 session）。
- **A2 独立レビューごとの観点・担当・モデル・ベンダー（実績）:** 3 件とも入力版 commit `c28260b3ad311e0ed5951f023992ff02d794d33d` の本書（sha256 `832d4246…a267`）を読み、互いの指摘を見ていない。いずれも読取専用で、R2・資格情報・Unity には触れていない。
  1. **アーキテクチャゲート**（`architecture-gates.md` の 7 項目、責務マップ、BuildStore の 50% 警報、BuildProtection の 3 責務警報、DELETE の所有者、Evidence との非共通化）: Claude Sonnet 5.5／Anthropic、新規 subagent session。
  2. **失敗経路・fail-closed・競合・検証経路:** GPT（Codex CLI の既定 `gpt-6-sol`、reasoning high）／OpenAI、`codex exec --sandbox read-only --ephemeral` の新規 session。当初は Claude Fable 5.1 に割り当てたが、利用クレジット不足で起動できなかった。代わりに Opus 5.5 で起動したが、A1 主担当と同じモデルだったため、結果を出す前に止めて GPT へ替えた。
  3. **A0 だけを読む代替構成:** Claude Haiku 5.5／Anthropic、新規 subagent session。入力は本書 §0〜§1 だけを切り出した版（sha256 `52b95a8b…e716`）。本書と他のレビューを読んでいないと申告した。
  - 独立性: ②は別ベンダー。①③は A1 主担当と同じベンダー（モデル系列は違う）なので**独立性制約あり**。
- **A3 統合担当・モデル・採否:** 未実施。下の「主担当の提案」は A3 の入力で、凍結していない。
- **C' 用に予約した担当・モデル・ベンダー:** Phase A で使っていない Grok 系（xAI。`cursor-agent` は Grok 系の明示指定でのみ使う）、または人間担当。B／C の担当が決まった後、B／C と異なることを確認して owner が確定する。
- **独立性の強化条件を満たせない場合の理由:** C′ の強化条件（A〜C に未関与の系列・ベンダー）は、Grok を Phase A で使わずに予約したことで満たせる見込み。B／C に Grok を使う場合は満たせなくなるので、そのときは人間担当の C′ を検討する。

### A2 独立レビュー結果と r2 への反映

表の AC 番号は入力版（`c28260b`）の番号。r2 で採用分を本文へ反映し、AC を振り直した。owner は 2026-10-11 のチャットで #4・#7・#15・#16・#19 と U7 の提案を受け入れ、#2 は「共有の保護は §4 C で扱い、本スライスでは限界として受け入れる（必要なら N を増やす）」と決めた。A3 の正式な凍結はまだ。

総括: blocker は 4 件で、すべて失敗経路レビュー（GPT）から出た。いずれも「誤削除が起きうる順序」を具体的に示している。アーキテクチャゲートは blocker 0 で、新規 3 module の分け方・依存方向・DELETE の所有者・Evidence と共通化しない判断を妥当とした。A0 だけの代替構成は、新規 module・lock・意図→DELETE→404→tombstone の順序で初稿とほぼ同じ構成に独立に到達した。違いは N の置き場、保護の付け外し CLI の有無、停止範囲の 3 点。

| # | 出典 | 重大度 | 指摘（要約） | 主担当の提案（A3 で確定） |
|---|---|---|---|---|
| 1 | FAIL-1 | blocker | AC14 の「次回は policy にかかわらず確認を再開し、GET 200 なら DELETE を再送」は、前回 403 で止まった後に保護が期限切れになる等で候補から外れた Build を消しうる | **採用。** 再開時の GET 404 は削除済みとして記録する。GET 200 なら、最新 snapshot・保護・access lock・policy で今も候補の場合だけ再送する。候補でなければ `delete-abandoned.json` を追記して意図を解消し、通常の計数に戻す（Evidence の resolve と同じ考え方）。AC14 を書き換える |
| 2 | FAIL-2 | blocker | 別 PC の fetch は access lock を取らないので、「取得中は消さない」を満たせない | **採用（範囲を限定して明記）。** 最低条件 4 の「取得中」と保護の収集源を「この PC の記録」に限ると明記する。別 PC の利用は観測できない限界として README の known limits に書き、owner が A3 で受け入れるかを決める。共有の調停は後続（別 host／Cloud） |
| 3 | FAIL-3 | blocker | use 直後に時計が巻き戻ると `now < createdAt` となり、AC6 では保護が無効になる | **採用。** `createdAt > now` は `protection-unknown`（非削除）。読取時に `createdAt < until`、`until − createdAt ≤ 90 日`、`releasedAt ≥ createdAt` を検査する |
| 4 | FAIL-4 | blocker | `publishedAt` は確定時の壁時計なので、時計が巻き戻った間の publish が「最古」に見え、実際の最新が消えうる | **採用（検出して止める）。** 系列内に `publishedAt > now` の receipt があれば系列を blocked（`clock-skew`）。「最新」は記録時刻の順と定義し、巻戻り中に確定した publish の順序は保証しない限界を README に書く。単調な順序記録は後続（Content 確定記録の入力） |
| 5 | FAIL-5／ARCH-6／代替 U6 | major | build-config の bytes が変わると、`Read-BuildReceipt` の config hash 照合で旧 receipt が全て `receipt-invalid` になり、repo 全体が恒久的に blocked。止める範囲も広すぎる | **採用。** catalog は config の hash に依存しない検査（repositoryId・key・seriesId の整合、intent hash、readback）で成功を判定する。止める範囲は「指定系列に属する記録」と「系列を判定できない記録」に絞る。他系列の破損では止めない |
| 6 | FAIL-6 | minor | 再開分の再送を含めると DELETE が 21 回になりうる。上限の数え方が曖昧 | **採用。** 上限は再送を含む DELETE 試行の総数 20。予算は記録の書込みと確認 GET にも効かせる |
| 7 | FAIL-7 | major | 403 なら 11 件が残り、実証できない。再実行の「送信 0」を結果 JSON だけでは実通信の証拠にできない。実系列の件数も Phase C 時点で変わりうる | **採用。** 最大 11 件の残置を A3 の承認対象にする。非秘密の通信試行回数を結果に含めて証拠にする。実系列 dry-run の期待値は当日の snapshot から決める。403 なら最低条件 5 は未達 |
| 8 | ARCH-1 | major | transport 入口の許可判定が実行と同じ式・同じ catch にあり、「印付きは通る」を offline で検証できない | **採用。** 許可判定を純粋関数（例 `Test-R2ArtifactRequestAllowed`）に切り出して export し、表で検証する |
| 9 | ARCH-2 | major | 保護記録 `protections/<id>.use.json` は `Write-BuildImmutable` の固定名に合わず、書込み経路が責務マップと食い違う | **採用。** `Write-BuildImmutable` の許可を「固定名＋`protections/<32hex>.(use\|release).json`」へ広げると §3 に明記し、BuildStore の予想行数を見直す |
| 10 | ARCH-3 | minor | BuildProtection の入口 2 関数（use／release）は orchestration で、読取側の catalog も同じ module を import する | **採用。** `Invoke-BuildUse`／`Invoke-BuildRelease` は BuildApplication に置き、BuildProtection は記録の読書きと有効判定（純粋、export）だけにする |
| 11 | ARCH-4 | minor | `access.lock` を作る主体、既存 Build（`b80d0ef6…`）での扱い、cleanup 側の保持区間が未定義 | **採用。** BuildStore に「無ければ CreateNew、あれば Open」の 1 関数を置く。cleanup は削除意図の書込みから削除済み記録まで保持する |
| 12 | ARCH-5 | minor | 600 秒予算の実現手段が Evidence 名の `$script:Budget` しか無く、乗ると Evidence へ依存する | **採用。** `New-ArtifactRequest` の Stopwatch（Timer）だけで締める |
| 13 | ARCH-7 | minor | 「DELETE → GET 404 → 記録」が Evidence 清掃・reset・本スライスで 3 回目 | **保留 → 後続。** 本スライスでは複製し、共通 primitive 化は Content 件数保持の運用接続の A1 へ入力として送る |
| 14 | ARCH-8 | minor | 既存の Build DELETE 拒否 assert は、印の無い DELETE なので AC17 でもそのまま成立する。改める必要は無い | **採用。** `BuildPublish.Tests.ps1` は変更しない。陽性側は #8 の純粋関数で検証する |
| 15 | 代替 U2 | — | 印ではなく専用の操作名（`retention-delete`）と C# 側の専用入口にする | **不採用案。** C# 変更が増え、印は権限ではなく境界の表明であることは同じ。#8 で検証可能性は満たせる。A3 で確認 |
| 16 | 代替 U4 | — | N は CLI `--keep`（既定 10）。build-config に入れると config hash に結び付いて receipt が読めなくなる | **不採用案（本スライス）。** 誤った小さい値による一括削除の窓を開けない。build-config に入れない点は同意。設定化は自動起動スライスへ |
| 17 | 代替 U1 | — | 保護は Evidence と同形の記録にし、use／release CLI は後続 | **一部採用。** 記録の形（owner・consumer・reason・createdAt・有限 until・releasedAt）を Evidence に揃える。CLI は本スライスに残す（最低条件 2 に保護を付ける手段が要る） |
| 18 | 代替 §5 | — | bucket の versioning があると GET 404 は非現行版の不在を示さない | **採用（記録）。** F5 に追記し、AC20 で owner に versioning の有無を 1 回観察してもらう。404 確認は現行版の不在の確認と明記する |
| 19 | 代替 §4 | — | 系列 lock ではなく repo lock で直列化する | **保留。** 系列ごとに独立に進められる利点を優先して系列 lock を提案。A3 で確認 |

「妥当」とされた主な点（重複を除く）: 未完了を成功に数えない／読めない保護で止める方向、系列 lock 後の再収集、DELETE 後の厳密な 404 確認、不変記録による再開性、Content DELETE 拒否の維持、層ごとの清掃分離、公開面の最小性（`build delete` を作らない、N 定数）、Content・ArtifactsRoot に触れないことをテストで固定する計画。

**r2 で A2 の外から足したもの（owner との協議、2026-10-11）:** 削除後の照会（最低条件 5・AC21。消した Build を `inspect`／`collect` が成功と返さない）、運用の記述（AC22。消したくない Build は保護か N の増加、系列の枠は branch・人で共有、削除は書込み鍵のある PC だけ）、後続への入力（N の系列ごとの設定、共有の保護、Evidence が参照する Build の保護、Cloud に削除権を渡さない、リリース保存、store を失った PC の孤児）。

### A2 2 巡目（擬似 C′）の結果と r3 への反映

- 担当: GPT（Codex CLI の既定 `gpt-6-sol`）／OpenAI、新規の read-only session。1 巡目の失敗経路レビューと同じモデル系列だが別 session。入力版は commit `dc52ed939526f255276c35e73d4801e3fc31c272`（sha256 `24ee9ed4…9b4a`）。過去の C′ 指摘の 5 つの型（判定し直せない、確認から確定までの窓、最終 head 以外の証拠、凍結されていない例外、条件の曖昧さ）をチェックリストとして渡した。
- 結果: blocker 7、major 2、minor 1。全件採用して r3 の本文へ反映した。新しい blocker が出たため、指摘は出尽くしていない。r3 に対して 3 巡目を行う。

| ID | 型 | 重大度 | 指摘（要約） | r3 の反映 |
|---|---|---|---|---|
| PC-1 | 判定し直せない | blocker | 結果 JSON と記録の hash だけでは、削除した key・404 の観測・残り 10 件の照合を C′ が再判定できない | `deleted.json` に DELETE と GET の非秘密の観測を持たせ（AC14）、§5 の証拠を原記録そのものと照合の機械検査に変えた |
| PC-2 | 窓 | blocker | use が lock の前に削除状態を確認すると、古い判定で保護を付けうる | AC7: lock 取得後に読み直し、rename まで保持 |
| PC-3 | 窓 | blocker | inspect／collect の確認と結果の間に削除意図が確定しうる | AC21: 確認から結果まで access lock を共有で保持 |
| PC-4 | 窓 | major | 予算超過後に削除済み記録を確定しうる | AC18: 各 rename 直前と確定時に判定。超過なら書かずに次回 GET から |
| PC-5 | 例外 | blocker | config 非依存の検査が AC3 の列挙だけだと、既存 reader の検査が抜ける | AC3: 既存の全検査から config 照合 2 項目だけを除く。検査本体は 1 箇所 |
| PC-6 | 曖昧さ | blocker | 保留中の Build を成功行から外したままでは、GET 200 後に「今も候補」にならず再送できない | AC16: 保留 Build を成功行に戻した完全な snapshot で再計算 |
| PC-7 | 曖昧さ | blocker | 実証をやり直すと検証用系列の初期件数が変わり、期待が崩れる | §5・AC23: 試行ごとに新しい `probe-<n>`、空であることを原記録に残す |
| PC-8 | 曖昧さ | major | use／release に config が無く repo を決められない | AC7: `--config` を要求 |
| PC-9 | 曖昧さ | minor | 同時刻の 11 件では「最古」が時刻だけで決まらない | §5: 期待 ID は既存 policy の全順序から算出 |

## 6. Phase B 実装結果

- 実装:
- HANDOFF との差:
- 未実行:
- implementation head commit:
- Phase B 担当・モデル・ベンダー:

## 7. Phase C

- 種別:
- 使用したevidence bundle の取得案内・状態:
- 構造適合:
- 現在の問いを阻害する findings:
- 後続スライスへ移送する findings:
- 実行したテストコマンドと `-Filter`、対象を選んだ理由:
- テスト結果:
- 判定必須のうち未実行:
- 重い検証を発見段階で限定実行した場合の理由と範囲:
- 未確認事項:
- 担当・モデル:

## 8. Phase C'

- 担当方式:
- 使用したblind audit bundle の取得案内・状態:
- 確認範囲・方法:
- 判定:
- 現在の問いを阻害する findings:
- 後続スライスへ移送する findings:
- 残存リスク:
- 監査できなかった範囲:
- 独立性:
- 発見 C / 判定 C 結論の事前閲覧・設計実装への関与:
- 担当・モデル:

## 9. Phase D

- C / C' の突合:
- マージ判断:
- 必要なEvidenceの保存・取得・必須内容の閲覧確認:
- 取得案内の引渡し確認:
- harvest:
- 削除確認:
- 通常のtask終了・後始末の状態と未完了事項:
