# BuildSystem Artifact Storage — Build 件数保持の運用接続スライス

## 0. メタデータ

- type: `slice`
- status: `A`（A0・A1 初稿完了、A2 独立レビュー結果を記録済み。A3 は未実施で、本書はまだ凍結していない）
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

**F5. R2 側の設定は未確認。** `development/builds/v1/` に bucket の lock・lifecycle rule があるかを AI は管理画面で読まない（README の reset 節の方針）。DELETE が 403（`ObjectLockedByBucketPolicy` 等）を返す可能性があり、その場合は rule を変えずに削除保留として扱う。

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

### 未決事項（A1 で案を出し、A3 で決める）

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
  1. **収集:** 指定した 1 系列について、ローカル store の全 Build 記録を読み、成功・失敗/未完了・削除中・削除済みを分類した snapshot と、各成功 Build の利用中保護を集めて `Get-BuildRetentionDecision` に渡せる。系列に属するかを判定できない記録や保護が 1 件でもあれば、その系列は何も消さずに理由を返す。
  2. **保護:** Build に有限期限付きの利用中保護を付け外しでき（`build use`／`build release`）、有効な保護を持つ Build は N を超えても削除されず N に数えられる。保護だけで N を超える間は `excessCount` を表示して正常終了する。
  3. **削除:** 系列 lock の下で削除直前に snapshot と保護を取り直して policy を再評価し、なお削除候補の Build だけを、削除意図を記録 → R2 DELETE → GET が 404 `NoSuchKey` → 削除済み記録、の順で消す。404 を確認できない限り削除済みとしない。途中で止まったものは次回の実行で続きから確認する。
  4. **消さないこと:** 失敗・未完了 publish、保護中、保護不明、取得中、lock 競合、DELETE の 403/timeout/応答不明、catalog 不完全のいずれでも、該当 Build（不完全なら系列全体）を消さない。Content の key は従来どおり DELETE を拒否する。
  5. **実 R2:** 実系列の dry-run で現況（1 件、`within-limit`）を表示し、検証用系列で N+1 件を publish したうえで実行して、最古の 1 件だけが DELETE・404 確認・削除済み記録となり、再実行では何も送らないことを生結果で残す。判定 C の責任とし、実行前に owner の許可を得る（§5）。

- **受け入れ条件**（最低条件の観測可能な詳細）:

  | # | 条件 | 確認 |
  |---|---|---|
  | AC1 | `build cleanup --profile osm --config <build-config> --project <p> --target <t> --configuration <c> [--dry-run]` は 1 系列だけを対象にする。系列 token は `Get-BuildSeries` の規則で検査し、不正なら何も読まず `invalid-build-identity`。 | offline |
  | AC2 | catalog は `builds-v1/<repositoryId>/` 直下を列挙し、`<32hex>` directory、`<32hex>.reserved`、本スライスが作る `locks/` だけを既知とする。directory ごとに、valid な receipt があれば成功（`publishedAt` は receipt の値）、receipt が無く intent があれば未完了（intent の系列で分類）、どちらも無ければ系列不明の未完了とする。成功の判定は `Read-BuildReceipt` の検査を流用する。 | offline |
  | AC3 | 次のどれかがあれば、**指定系列を含む repo 全体を** `status=blocked` とし、DELETE も記録書込みもしない: receipt があるが検査に失敗（config 不一致を含む）、intent が読めず receipt もある、未知の entry、reparse・ACL 不一致、保護記録の破損、削除記録の破損。理由は `catalog-incomplete`／`catalog-config-mismatch`／`protection-unknown`／`delete-record-invalid` のいずれか。系列の所属が分からない成功記録は、その系列の成功かもしれないため（U6）。 | offline |
  | AC4 | 系列不明の未完了（receipt も読める intent も無い）は成功ではないので blocked にしない。結果の `held` に `publish-incomplete` として件数だけ出す。 | offline |
  | AC5 | 保護は `build use --build-id <id> --consumer <id> --until <UTC> --reason <text>` で付け、`build release --build-id <id> --protection <id>` で外す。`until` は現在より後で、現在から 90 日以内。`consumer`／`reason` は Evidence の use と同じ字種・長さ規則。記録は `<buildId>/protections/<protectionId>.use.json` と `.release.json` の追記だけで、既存記録を書き換えない。成功 receipt の無い Build、削除意図のある Build への use は拒否する（`build-not-retainable`）。 | offline |
  | AC6 | 保護が有効なのは `createdAt ≤ now < until` かつ対応する release が無いとき。release 済み・期限切れは無効。1 件でも記録が検査に失敗すれば AC3 の `protection-unknown`。時刻は注入した時計で判定し、文字列の時刻は UTC（offset 0）だけを受ける。 | offline |
  | AC7 | N は本スライスでは定数 10（`BuildCleanup` 内の 1 箇所。テストだけ注入可能）。CLI から変えられない（U4）。 | offline |
  | AC8 | `--dry-run` は lock を取らず、記録・network に触れずに、`retained`／`candidates`／`held`／`deleting`／`deleted` の件数と ID、`excessCount`、`reasonCode` を返す。実行時と同じ catalog と policy を使う。 | offline |
  | AC9 | 実行時は `builds-v1/<repositoryId>/locks/<project>~<target>~<configuration>.lock` を FileShare.None・非継承で開いた handle を系列 lock とし、取れなければ `cleanup-in-progress` で何もしない。`build use` も同じ lock を取る。lock は process 終了で解放され、永続の予約は作らない。 | offline（別 process を signal で同期） |
  | AC10 | lock 取得後に catalog と保護を**取り直して**policy を再評価し、その時点の候補だけを扱う。lock 前の dry-run 結果を使わない。 | offline |
  | AC11 | 候補ごとに、`<buildId>/access.lock` を FileShare.None で開けなければ（取得中）その Build を `in-transfer` で飛ばす。`build fetch` はローカルに同じ buildId の directory があれば、取得の開始から終了まで同 file を FileShare.ReadWrite で開いて保持する。directory が無い（別 PC の参照）場合は lock を取らずに従来どおり取得する（F2 の限界と同じ）。 | offline（別 process を signal で同期） |
  | AC12 | 削除の手順は候補ごとに直列で、古い順。`delete-intent.json`（key・packageSha256・意図時刻）を不変記録で書く → transport の DELETE → 同じ key の GET が `Test-ArtifactMissing` を満たす → `deleted.json`（意図の hash と 404 観測）を不変記録で書く。DELETE が 2xx でも GET が 404 で無ければ `delete-unconfirmed` で止め、削除済みとしない。 | offline |
  | AC13 | DELETE または GET が 403・timeout・redirect・応答不明・generation 変化のとき、その Build は `delete-pending` として残し、同じ run で再送しない。系列の残りの候補も処理しない（順序を保つ）。結果は `status=partial`、exit 2。 | offline |
  | AC14 | 削除意図があり削除済み記録が無い Build は、成功件数にも保護にも数えず（`deleting`）、次回の実行で policy の判定にかかわらず GET から確認を再開する。GET が 404 なら削除済み記録を書き、200 なら DELETE を 1 回送り直して AC12 と同じ確認をする。削除意図は取り消さない。 | offline |
  | AC15 | 削除済み記録のある Build は snapshot から除き（成功に数えない）、`deleted` に件数を出す。receipt・intent・削除記録は残し、ローカル記録は削除しない。 | offline |
  | AC16 | 1 回の実行で DELETE する候補は最大 20 件。残りは `remaining` に件数を出して次回へ回す。時間予算は 600 秒（transport の各呼出し deadline にも効かせる）。予算超過は `budget-exceeded`、その時点の Build は AC13 と同じ扱い。 | offline |
  | AC17 | transport の入口は `development/builds/v1/…/bundle.zip` の DELETE を、request に `BuildRetentionDelete=$true` がある場合だけ通す（U2）。その印を付けるのは `BuildCleanup` だけ。Content の DELETE と、印の無い Build DELETE は従来どおり拒否する。`build delete` という grammar は作らない。 | offline |
  | AC18 | 結果 JSON は非秘密の固定 field（`status`、`reasonCode`、`seriesId`、`keepCount`、`excessCount`、各分類の ID 配列と件数、`deleted`、`remaining`、Build ごとの `outcome`）だけを持つ。endpoint、ローカル path、例外本文、SDK 応答本文を出さない。exit は 0=完了（`within-limit`・`protected-over-limit` を含む）、1=blocked/失敗、2=partial。 | offline |
  | AC19 | `build publish`・`collect`・`inspect`・`fetch` の外から見える挙動は、AC11 の access lock 以外は変えない。既存 receipt の形式は変えない。 | offline（既存 suite） |
  | AC20 | 実 R2: 実系列の dry-run、検証用系列（§5）で N+1 件 publish → 実行 → 最古 1 件だけ DELETE・404・削除済み記録 → 再実行で DELETE/GET を送らない。 | 判定 C（owner 許可後） |

- **ここでは答えない問いと所有する後続スライス:**

  | 問い | 所有 |
  |---|---|
  | Content の実 DELETE、保持中 Player（ArtifactsRoot の `build-info.json`）からの Content 保護収集、確定記録との関係 | 新規「Content 件数保持の運用接続」（program §4 B に登録） |
  | ArtifactsRoot の `players/`・`content/` の件数清掃、`.staging` 回収、ArtifactsRoot の所在の登録 | 既存「ArtifactsRoot の件数清掃と `.staging` 回収」（program §4 B の項目を独立スライスとして登録） |
  | 系列の退役 | 「件数保持の運用接続」の残り（Content 側と同時に扱う） |
  | 自動起動（scheduler 接続） | 新規「件数清掃の自動起動」。Evidence の固定 runtime と同じ方式で足すかを着手時の A1 で決める |
  | 診断の改善（§1 対象外の一覧） | 既存「診断の改善」 |
  | 別 PC の publish を数える、R2 一覧、孤児回収 | 既存「Contentの容量上限」の孤児回収と、新規の R2 一覧の要否（program §4 B へ入力として登録） |
  | N を設定可能にするか | 自動起動スライスの A1 |

- **program §4 B に溜まった入力の仕分け（A1 案）:**

  | 入力（出典） | 本スライスで決めること | 後続へ送るもの |
  |---|---|---|
  | 保護の収集源の範囲（r7 レビュー） | Build の収集源は、この PC の `builds-v1` にある明示保護記録と取得中の access lock だけ。`players/` の外の配布済み Player や他 machine の Build は収集源に入らない。「収集できない」（記録の破損・読めない）は blocked、「存在しない」（記録が無い）は保護なし、と区別する（AC3・AC6） | 保持中 Player からの Content 保護収集と、DIST cache の再取得元である ArtifactsRoot の削除規則との関係 → Content 件数保持の運用接続 |
  | 保護が N を超えた場合（r7 レビュー、件数保持の運用接続） | Build は §2 の超過条項をそのまま適用（保護は外さず `excessCount` を表示、exit 0） | Content が同じ条項を引き継ぐかの確認 → Content 件数保持の運用接続 |
  | 件数保持がどの層に効くか（件数保持の運用接続） | Build の件数保持は **R2 の object にだけ効く**。ローカルの成功記録は消さず削除済みとして残し、ArtifactsRoot の退避先には触れない | ArtifactsRoot の件数清掃 → 同名スライス。DIST cache → 対象外のまま |
  | Content の実 DELETE（件数保持の運用接続） | 決めない。Content key の DELETE 拒否を維持する（AC17） | Content 件数保持の運用接続 |
  | 系列の退役（件数保持の運用接続） | 決めない | 件数保持の運用接続の残り |
  | ArtifactsRoot の件数清掃、`.staging` 回収（§4 B） | 決めない。触れないことをテストで固定する | ArtifactsRoot の件数清掃と `.staging` 回収 |
  | 失敗時の `publish` 表示、`existing` 結果の `objects`、照合結果の記録、content store の ACL 付け直し、240 字超の `unconfirmed`、config 二度読み、`stage-conflict` の敗者（診断の改善、PR #118） | 決めない。新しい `build cleanup` の結果だけは、最初から Build ごとの `outcome` を持たせる（AC18） | 診断の改善 |
  | 準備 helper 失敗による未記録・未送信 private 残置（PR #111） | 決めない。catalog は receipt も intent も無い directory を系列不明の未完了として数えるだけ（AC4） | 未完了uploadとstaging |
  | R2 側に上書き防止が無い・別 PC の同時公開（PR #118） | Build の削除も同じ前提で、別 PC の writer・cleanup との競合は防がない（同じ Build を別 PC から削除する運用は想定しない）。README の known limits に書く | Content 確定記録 |

- **判定定義:** GO = 最低条件 1〜5 を判定必須テストと AC20 の生結果で確認し、阻害 findings が無い。NO-GO = いずれかの最低条件が未達、または誤削除（候補でない Build、保護中、Content key への DELETE 送信）が 1 件でも観測された。CONDITIONAL ACCEPT は使わない（スパイクではない）。
- **停止規則:** 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- **A3 後の例外承認:** なし（A3 未実施）
- **本文へ転記した実装制約:**
  - `#nullable enable`・`record` 禁止は Unity 側の C# の規則で、本スライスは Unity C# を変えない。C# の transport・packaging も変えない。
  - テストで時間待機を使わない。並行は別 process を signal で同期し、時計・transport・読戻しは注入する。
  - 公開 reason は許可語だけ。例外本文・path・endpoint を出さない（既存の Build／Content の規則）。
  - 資格情報は既存の `Invoke-CredentialTransport` 経由だけで使い、鍵を引数・ログ・ファイルへ出さない。
  - 通常 writer に bucket 管理権限を足さず、403 を rule 変更で突破しない。
  - 既存の成功記録（`receipt.json`・`intent.json`）を書き換えない。追加する記録も不変（CreateNew + rename）で、既存名を置換しない。
- **未決事項:** U1〜U7 の案は §3〜§5 に書いた。A2 の指摘と合わせて A3 で決める。

## 3. 責務マップ

| ファイル | 責務（一文） | 変更理由 | 所有者・寿命 | 依存（入力 → 出力） | 種別 | 公開面 | テスト境界 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|---|---|
| `BuildCatalog.psm1`（新規） | `builds-v1/<repo>/` を読み、1 系列の policy 入力行と、削除中・削除済み・blocked 理由を作る | store の配置や記録の種類が変わったとき | 状態を持たない。読むのは BuildStore が所有する記録 | BuildStore（receipt 検査、directory 解決）、BuildProtection（保護の読取と有効判定）→ 行の配列 | 永続化の読取 + 分類 | 内部 module 関数 `Get-BuildSeriesSnapshot` | 注入 root の fixture で全分類・全 blocked 理由を offline に検証 | 0 → 約 150 |
| `BuildProtection.psm1`（新規） | Build の利用中保護記録の追記・読取・有効判定と、`build use`／`release` の手順 | 保護の規則（期限・字種・解除）が変わったとき | 記録は BuildStore の Build directory 配下。寿命は Build の記録と同じ（削除済みでも残る） | BuildStore（directory、lock、不変書込み）、ArtifactCommands → 記録 | 永続化 + 小さな純粋判定 + 入口 | `Invoke-BuildUse`、`Invoke-BuildRelease`、内部 `Get-BuildProtectionState` | 注入時計で期限境界、破損、release 済み、削除意図ありへの use 拒否 | 0 → 約 140 |
| `BuildCleanup.psm1`（新規） | 系列 lock の下で catalog → policy → 候補ごとの削除意図・DELETE・404 確認・削除済み記録を順序どおりに進め、結果を集計する | 削除の手順・予算・上限・結果の形が変わったとき | 状態は持たず、記録は BuildStore が所有。lock は実行中の process だけが持つ | BuildCatalog、BuildRetentionPolicy、BuildStore、ArtifactApplication（network・`Test-ArtifactMissing`）、Credentials → 結果 | orchestration | `Invoke-BuildCleanup` | 注入 transport・時計・fault hook で全失敗経路、別 process の lock 競合 | 0 → 約 230 |
| `BuildStore.psm1` | Build 成功記録の配置・不変書込み・検査（既存）に、削除記録の読書きと系列 lock・access lock の取得を足す | 記録の種類が増えるため | Build application（既存のまま） | 既存どおり | 永続化 | `Write-BuildImmutable` の許可名に `delete-intent.json`・`deleted.json`、`Enter-BuildSeriesLock`、`Enter-BuildAccessLock`、`Read-BuildDeleteState` を追加 | 既存 + 新記録の検査 | 135 → 約 205（+52%） |
| `BuildApplication.psm1` | publish／inspect／fetch／collect（既存）。fetch に access lock を足す | AC11 | 既存 | + BuildStore の access lock | orchestration | 変更なし | 既存 suite + fetch 中の lock | 240 → 約 250 |
| `BuildRetentionPolicy.psm1` | 変更しない | — | — | — | Policy | — | 既存 suite | 148 → 148 |
| `Transport/R2ArtifactTransport.psm1` | Build DELETE を印付きの request だけ通す | AC17 | — | — | adapter 入口 | 変更なし（request field が 1 つ増える） | 入口の拒否・許可の組合せ | 46 → 約 47 |
| `tools/artifacts.ps1` | `build cleanup`／`use`／`release` の grammar | 入口の追加 | — | → 上記 module | 入口 | CLI | grammar の拒否・必須引数 | 約 +20 |
| `tests/BuildCleanup.Tests.ps1`（新規）、`tests/BuildCleanupChild.ps1`（新規） | AC1〜AC19 の offline 検証 | — | テスト専用 | 注入 hook | テスト | — | — | 0 → 約 450、約 60 |
| `tests/BuildPublish.Tests.ps1` | Build DELETE 拒否 assert を「印の無い DELETE は拒否、印付きは入口を通る」に改める。`build delete` grammar 拒否は維持 | AC17 | — | — | テスト | — | — | 286 → 約 295 |
| `tools/Artifacts/README.md` | 新節「開発Buildの件数清掃（build cleanup / use / release）」、policy 節の「未接続」の記述更新、known limits | 公開面 | — | — | 文書 | — | docs-audit | 275 → 約 310 |

**行数・責務の警報と判断:**
- `BuildStore.psm1` は +52% で 50% 警報が発火する。**非分割案**: 追加は「Build directory 配下の記録とその排他」という同じ変更理由・同じ所有者・同じ ACL 規則・同じテスト方法に属し、別 module に出すと `Write-BuildImmutable` の許可名と ACL 検査を二箇所に持つことになる（同じ事実が二箇所にあると腐る）。A2 の観点に含める。
- `BuildProtection.psm1` は「記録の永続化」「有効判定（純粋）」「CLI 入口の手順」の 3 責務になりうる（3 責務の警報）。**非分割案**: 3 つとも「保護の規則」という 1 つの変更理由で同時に変わり、合計 140 行程度。有効判定は時計を引数で受ける純粋関数として module 内で分け、単体テストできるようにする。A2 で分割（`BuildProtectionPolicy` を別 file）を要否判断する。
- `BuildCleanup.psm1` は orchestration だけに留め、分類（catalog）と選別（policy）と記録（store）を持たない。230 行を超える見込みが出たら Phase A へ戻す。
- tools/Artifacts 全体: Build 系は Contract／Stage／Store／Application／RetentionPolicy の 5 file に 3 file が加わる。Evidence 系（Contract／Paths／StateStore／RetentionPolicy／Cleanup／Schedule／Reset／Application）と同じ「純粋 policy・store・orchestration を分ける」配置に揃える。共通化（Evidence と Build の清掃 engine の統合）はしない。所有者（task store と Build store）、寿命（task 終了 30 日と件数）、guard が違い、統合すると一方の変更が他方へ波及するため。

**DELETE の所有者:** R2 の Build object を消す主体は `BuildCleanup` だけ。transport の印（AC17）はその境界をコード上に残すためで、権限ではない（同一ユーザー内の信頼が前提）。ローカルの記録は消さない。ArtifactsRoot と DIST cache には触れない。

**U1 の案（保護の収集源）:** 明示した保護記録だけを収集源とする（AC5・AC6）。採らない案: (a) 保護源なし（全 `inUse=false`）は「利用中は消さない」契約を満たせない。(b) ArtifactsRoot の退避先の存在は全件保護になる（F4）。(c) fetch の履歴は「使い終わった」が分からず、期限の無い保護になる。(d) Evidence の task store への相乗りは所有者と寿命が違う。fetch 中だけは access lock（AC11）で守る。

**U3 の案（途中状態）:** Build directory 配下の不変記録 `delete-intent.json` → `deleted.json`。Evidence の deleteIntent・tombstone と同じ考え方を、Build store の不変記録の規則で表す。

## 4. 実装計画

- **変更対象:** §3 の表のとおり。新規 3 module、既存 4 file の小変更、テスト 2 新規・1 変更、README。
- **順序:**
  1. `BuildStore` に削除記録・lock を追加し、既存 suite が通ることを確認する。
  2. `BuildProtection`（記録・有効判定・use/release）とその offline テスト。
  3. `BuildCatalog` と分類・blocked のテスト。
  4. `BuildCleanup`（dry-run → 実行 → 途中状態の再開）と失敗経路のテスト。
  5. transport の入口（AC17）、`build fetch` の access lock、CLI grammar。
  6. README。`pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1`。
- **Phase B から Phase A へ差し戻す条件:** 計画外の永続記録・lock・公開 CLI が必要になった、`BuildRetentionPolicy` の契約変更が必要になった、catalog を repo 単位でなく系列単位に区切らないと性能・正しさが成り立たない、transport の C# 変更が必要になった、`BuildCleanup` が分類や記録の責務を持たないと書けない、既存の receipt 形式の変更が必要になった。
- **対象外を維持する方法:** Content の key・store・ArtifactsRoot・`transfers/` を参照するコードを `BuildCleanup`／`BuildCatalog` に置かない。テストで Content key への DELETE が 0 回であること、ArtifactsRoot fixture が不変であることを assert する。

## 5. テストとレビュー計画

- **単体テスト（offline、`tests/BuildCleanup.Tests.ps1`）:**
  - catalog: 成功／未完了／系列不明／他系列の混在、破損 receipt、config 不一致、未知 entry、reparse、ACL 不一致、削除意図あり、削除済み（AC2〜AC4、AC14、AC15）。
  - 保護: 期限境界（`until` ちょうど、`createdAt` 前）、release 済み、破損、90 日超、削除意図ありへの use、receipt の無い Build への use（AC5、AC6）。
  - 削除: dry-run が何も書かない、候補だけを古い順に削除、保護が N を超える場合の `excessCount` と非削除、DELETE 2xx でも GET 200 なら `delete-unconfirmed`、403／timeout／応答不明で `delete-pending` と後続の非処理、再実行で GET から再開、上限 20 件、予算超過（AC7〜AC8、AC10、AC12〜AC16、AC18）。
  - 競合: 別 process が系列 lock を保持中の `cleanup-in-progress`、`use` と `cleanup` の競合、fetch が access lock を保持中の `in-transfer`、lock 取得と同時に保護が付いた場合に再評価で外れること（AC9〜AC11）。
  - 境界: Content key の DELETE が入口で拒否され続けること、印の無い Build DELETE の拒否、Content store・ArtifactsRoot fixture の不変（AC17、§4）。
- **差し戻し中の起点 `-Filter`:** `BuildCleanup.Tests.ps1 -Case '*'`、`BuildRetention.Tests.ps1 -Case '*'`、`BuildPublish.Tests.ps1 -Case '*'`。C は凍結済み条件の確認に必要なら根拠を記録して変えてよい。
- **判定必須テスト（GO 候補 head）:** 3 つの .NET Release build（Probe・Packaging・Transport）、`tools/Artifacts/tests/*.Tests.ps1` の全 suite を `-Case '*'` で、Workflow `TaskLifecycle`、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、および AC20 の実 R2 生結果。
- **全 EditMode 回帰の適用除外:** 適用除外。理由: Unity の source・asmdef・Assets を変更しない（変更は `tools/` と文書だけ）。代替証拠: 全 Artifacts offline suite の結果 JSON（registered／selected／executed の件数一致、failed 0）と、diff に `unity/` が含まれないことの機械確認。
- **統合・Unity テスト:** なし（Unity を変えない）。
- **操作・実行時・目視条件の検証経路:**
  - AC20（実 R2）: 担当は判定 C。環境は owner の Windows ユーザー、既存の `osm` 資格情報と登録済み Evidence config・build-config。初期状態は F3 の store。操作: (1) 実系列 `samplegame/windows-x64-player/il2cpp-high` の `build cleanup --dry-run`（`within-limit`、1 件を期待）。(2) 検証用系列（project `retention-probe`、target `windows-x64-player`、configuration `probe`。非秘密の小さな合成 file 1〜2 個）へ 11 件を `build publish` し、`build cleanup` を実行。(3) 同じ実行をもう一度。観測: (2) で最古 1 件だけが `deleted`、`delete-intent.json`・`deleted.json` がある、その key の `build fetch` が `download-unconfirmed` になる、残り 10 件の fetch が hash 一致。(3) で DELETE・GET の送信 0（結果 JSON の Build ごとの `outcome`）。保存する証拠: 結果 JSON、記録 file の内容 hash、fetch 結果。evidence bundle に入れて C／C′ に渡す。
  - 検証用系列は退役の手段が無い（対象外）ため、R2 に約 10 件の小さな object が残る。A3 で残置を受け入れるか、AC20 の後に同じ CLI で N を下げる手段は作らない前提で、owner の判断を記録する。
- **未知の操作経路の疎通結果と確認地点:** Build key への実 DELETE と、DELETE 直後の GET が 404 `NoSuchKey` を返すことは、本 bucket・本資格情報では**未確認。初回確認は Phase C**（AC20）。理由: Phase A では実 R2 を操作しない（owner の制約）。Evidence の reset で `evidence/first-use/**` の DELETE→GET 404 は成立済みだが、`development/builds/v1/` に bucket rule があるかは未確認（F5）。不成立時: 403 なら rule を変えずに `delete-pending` が正しく出ることを確認したうえで、owner に設定の状態を確認し、問いを「Build DELETE が bucket 設定上可能か」に分けるかを A3 revision で決める。
- **人間の判断が必要な条件:** AC20 の実行許可（owner。実行直前に 1 行で許可を求める）と、検証用系列の残置の受け入れ（A3）。合否は結果 JSON と記録の機械検査で判断し、人間の目視判断は求めない。
- **機械検査:** `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、全 Artifacts PowerShell の parse。
- **H1/CURRENT 方式の提案（U7）:** **採らない（tracked HANDOFF で進める）**ことを提案する。理由: 直前の Build／Content スライス（PR #111・#115・#118）が tracked HANDOFF で完了しており、H1 の固定 5 step は `artifact-evidence-lifecycle` 用に承認された集合で、Build 清掃の suite を足すには承認仕様の改訂が別途要る。採る場合の利点（B exit の機械記録、Git への raw 追加の拒否）は、本スライスでは判定必須テストの生結果を evidence bundle に入れることで代替できる。A3 で owner が決める。
- **A0/A1 主担当・モデル・ベンダー:** Claude Opus 5.5／Anthropic（Claude Code desktop、本 session）。
- **A2 独立レビューごとの観点・担当・モデル・ベンダー（割当て案と実績）:**
  1. **アーキテクチャゲート**（`architecture-gates.md` の 7 項目、責務マップ、BuildStore の 50% 警報、BuildProtection の 3 責務警報、DELETE の所有者、Evidence との非共通化判断）: Claude Sonnet 5.5／Anthropic、新規 subagent session。
  2. **失敗経路・fail-closed・競合**（AC3・AC9〜AC17 の穴、誤削除が起きうる順序、途中状態の再開、403/応答不明、時刻・期限境界、検証経路 AC20 の実行可能性）: Claude Fable 5.1／Anthropic、新規 subagent session。
  3. **A0 だけを読む代替構成**（§1 だけと入力正本を読み、初稿を見ずに構成を出す）: Claude Haiku 5.5／Anthropic、新規 subagent session。
  - 3 件とも同じ入力版（下記 sha256）を読み、互いの指摘を見ない。A1 主担当と同じベンダーなので**独立性制約あり**（モデル系列は違う）。
- **A3 統合担当・モデル・採否:** 未実施。
- **C' 用に予約した担当・モデル・ベンダー:** Phase A で使っていない Grok 系（xAI。`cursor-agent` は Grok 系の明示指定でのみ使う）、または人間担当。B／C の担当が決まった後、B／C と異なることを確認して owner が確定する。Phase A では Grok 系を使わない。
- **独立性の強化条件を満たせない場合の理由:** A2 は 3 件とも Anthropic で、A1 と同じベンダー。Phase A で利用できる別ベンダーは C′ 用に予約したため。

### A2 独立レビュー結果（A3 で採否を決める）

（A2 実施後に記録する）

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
