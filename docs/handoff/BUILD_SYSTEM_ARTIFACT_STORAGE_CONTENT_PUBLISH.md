# BuildSystem Artifact Storage — Content 保存スライス

## 0. メタデータ

- type: `slice`
- status: `A3 凍結`（2026-10-11、owner 承認。次は Phase B。管理方式は tracked HANDOFF で、H1/CURRENT は採用しない。§5）
- branch: `codex/artifact-storage-content-publish`
- implementation base commit: `e949c6dc6b8b441d43c0bb1d14e72e486be514cf`（`develop`。PR #117 を含む）
- implementation head commit: `5aa9d740387e6e3b6b7aa63420061de8b854ac82`（Phase B の最終 head。初回実装は `603ede1`。本書の §6 更新 commit はその後）
- risk: `normal`（Artifacts の新規 module と、共有部品・allowlist への小さな変更。公開 API・asmdef・Unity は変えない。実 R2 に触れる操作を含む）
- owner: OSM maintainers
- created: 2026-10-11
- expires: 2026-11-08 または置換 revision
- harvest to: 操作と契約を `tools/Artifacts/README.md`、program の状態を `docs/handoff/BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §4 B へ。新規の後続スライス 2 件（Content 確定記録、Content 取得と再利用）を program §4 B に登録する。完了時に本書を削除する。
- Phase A snapshot の取得案内・状態: 生成済み（本書の §0〜§5 を A3 凍結 commit の版で固定したもの）・未保存。C／C′ への固定入力の引渡し時に、`review-evidence.md` に従い Evidence として publish し、取得案内を残す。
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成

入力の正本は program §2「開発Content」（r7）と §4 B の後続一覧。r7 で固定済みの契約は再議論しない（§1 に転記）。

## 1. 目的と対象外

- **目的:** r7 が固定した Content の保存契約のうち、「ローカルの Content 出力 1 件を file 単位の object として R2 へ保存し、別 process で取得して全 file の hash を照合する」最初の縦の経路を成立させる。
- **現況:**
  - Unity の `ContentTransportPublisher` は `transport.json` + `content/` を出力する。`transport.json` の bytes の SHA-256 が `manifestSha256` で、DIST の request pin、`build-info.json` の `transportManifestSha256` と同じ値になる。
  - `build collect`（PR #115）は Player 1 件を `<ArtifactsRoot>/players/...` へ退避し、`content/` を内包したまま ZIP 1 個で保存する（PUT 1 回）。Content を単独で保存・取得する経路は無い。
  - Artifact transport は PUT/GET/DELETE を 1 呼出し 1 object で行い、key を固定 allowlist（`Transport/R2ArtifactTransport.psm1`、`Transport/ArtifactReadback.ps1`）で絞る。Build 配下の DELETE は入口で拒否する。条件付き書込み（If-None-Match 等）は持たない。PUT と読戻しは 1 byte 未満の object を拒否する。
  - 読戻しは 1 object ごとに別 `pwsh` process を起動する（`ArtifactApplication.psm1` の `Invoke-ArtifactReadback`）。呼出しのたびに request と `.bin` を operation 直下に残し、`Protect-ArtifactTree` で operation 全体の ACL を設定し直す。
- **r7 で固定済みの契約（再議論しない）:** 単位は revision ごとの `transport.json` + `content/` で、丸ごと不変（上書きしない）。差分 upload・content-addressed・GC は採らない。保存形式は ZIP にせず file 単位の object。系列は `contentSet/target`。ローカルの置き場は `<ArtifactsRoot>/content/<contentSet>/<target>/<revision>/`（取り込み元。DIST cache とは別の層）。件数保持は Build と同じ policy を系列ごとに適用するが、実 DELETE は件数保持の運用接続スライスが所有する。ストレージに可変の別名を持たせない。新旧の順序は保存側の確定記録で決める。用途は contentSet の名前で表し、revision の昇格・改名はしない。Player は Build 時の contentSet と BuildIdentity に結び付く。
- **対象外（後続へ送る。§2 に所有先）:** Content 分離（Unity 変更・互換キー）、Content 取り込み（DIST cache への不足 file 再利用）、統合 Content フォルダ、`contentSet` 命名規約の確定、件数保持の実 DELETE と利用中状態の収集、R2 上の孤児 object の回収、Windows 以外の target、確定記録と「最新」の解決、R2 → ArtifactsRoot 配置と不足 file の再利用、0 byte の file への対応。
- **変えないもの:** Unity 側の C#・asmdef・テスト、`transport.json` の schema、Player/Build/Evidence の契約と保存形式、`build collect` と `build publish` の外から見える挙動、`BuildContract.psm1`、C# の transport・packaging。

### A0 実測（読取専用。Unity は起動していない）

対象は手元の Content 出力（`artifacts/bs4/transport/*`、`artifacts/bs2b/**/transport.json`）。

| 出力 | contentSet | files | 合計 | 最大 file | `transport.json` |
|---|---|---|---|---|---|
| 最新（`20260924T202227427Z-b80d…`） | `bs4-spring-full` | 220 | 5.45 MiB（5,716,575 B） | 1.0 MiB | 333,576 B |
| 以前の revision | `bs4-spring-full` / `spring-full` | 132 / 197 / 219 | 3.3 / 4.3 / 5.4 MiB | 0.5〜1.0 MiB | — |

- 内訳（最新）: `.cf` 179 件 4.29 MiB、`.resS` 39 件 1.12 MiB、`BuildManifestHash.txt` と `.json` 各 1。名前は内容 hash の平坦な 32hex + 拡張子（階層なし）。中央値 17 KiB、p90 21 KiB。相対 path は最長 37 字。0 byte の file は無い。
- `transport.json`（333,576 B、整形済み）のうち約 87%（290,142 B）は案内用 `sourceFiles`（1,182 件）、`files` は 43,128 B。
- **4 季節 Content は手元に無い。** `Tools/OSM/Content/Build All Seasons Full`（`SampleGameContentBuild.BuildAllFull`）は 4 季節を **1 revision・contentSet `all-full`** として出力する。手元の出力は Spring だけで、Unity の実行は Phase A では行わない。program の 880 file 推定（220 × 4）は未検証のまま残す。Spring 220 file には UI・SceneResourceMap 等の共通部分が含まれるので、`all-full` 1 revision は**約 881 object・約 22 MiB 以下**と見積もる。
- `transport.json` の大きさ: Spring の `sourceFiles` には Summer／Autumn／Winter の source が各 220 件すでに含まれる（SceneResourceMap の閉包が全季節を指すため）。`all-full` で増えるのは主に `files`（43 KB → 約 170 KB）で、`transport.json` は**約 0.4〜0.5 MB** と見積もる（未検証）。
- 既存の contentSet: `bs4-spring-full`、`spring-full`、`all-full`、`spring-whitebox`、`spring-full-whitebox`（`SampleGameContentBuild.cs` のメニュー）。

**A3 前の小さな疎通（owner が 2026-10-11 に資格情報の使用を許可して実施）**

- 方法: 既存の DPAPI `osm` と登録済み deployment の endpoint で、`probe/unlocked/<run>/` に乱数 17 KiB の object を 10 件 PUT→GET し、最後に自分が作った 10 key だけを DELETE した（全件 204）。呼出しは `Invoke-CredentialTransport` → `Invoke-R2ArtifactOperation`（1 操作ごとに DPAPI 復号を含む）。秘密と endpoint は出力していない。Content の prefix は使っていない。
- in-process（10 件）: PUT 中央値 202 ms（148〜302）、GET 中央値 144 ms（99〜401）、DELETE 中央値 103 ms。PUT 直後の GET は 10/10 で bytes と SHA-256 が一致した。未作成 key の GET は 404 `NoSuchKey`（`not-found`）として観測できた。
- 子 process 1 回の GET（module import・DPAPI・通信を含む）: 1.6〜1.8 s（5 回。初回だけ 2.4 s）。
- `Set-ArtifactAcl` の単価（offline、200 file）: 2.5 ms/file。1 operation を使い回すと `Protect-ArtifactTree` の総量は約 2N² 回で、Spring で約 4 分、`all-full` で約 65 分になる。
- 未確認: 最大 1.0 MiB の object の転送時間、数百件を連続で送ったときの throttling、`development/content/v1/` に bucket rule が及ぶか。判定 C の実 R2 往復で初めて確かめる。

**R2 操作回数・容量・時間の見積もり（1 revision あたり。§2 の公開経路による）**

| | Spring（実測した出力） | 4 季節 `all-full`（未検証・上限） | Player ZIP（現行 Build、参考） |
|---|---|---|---|
| object 数 | 221（content 220 + `transport.json`） | 約 881 | 1 |
| 公開時 PUT（初回） | 221 | 約 881 | 1 |
| 公開時 GET（事前照合 + 読戻し。初回） | 442 | 約 1,762 | 1 |
| 続行時 GET | 照合 221 + 未送信分の読戻し | 同左 | — |
| 取得時 GET | 221 | 約 881 | 1 |
| 保存量 / revision | 約 5.5 MiB | 約 22 MiB | ZIP |
| N=10 の保持量 / 系列 | 約 55 MiB | 約 220 MiB | — |
| 公開の所要時間（1 object 約 3.6 s） | 約 13 分（約 800 s） | 約 53 分（約 3,200 s） | — |
| 取得の所要時間（1 object 約 0.15 s） | 1 分未満 | 約 2〜3 分 | — |

- 回数よりも、object 数に比例する経過時間が先に効く。時間の大半は子 process の起動（約 1.7 s × 2 回/object）。
- R2 の料金・無料枠に対する比は、着手時の最新の料金表で owner が確認する。本書は料金の数値を固定しない。

## 2. 意思決定と受け入れ境界

- **このスライスが答える問い:** 既存の Content 出力 1 件（`transport.json` + `content/`）を、`<ArtifactsRoot>/content/<contentSet>/<target>/<revision>/` へ退避し、file 単位の object として R2 へ保存し、別 process で取得して全 file の SHA-256 を照合できるか。
  - 採否: owner 指定の候補をそのまま採る。これより小さく絞ると、「file 単位 object の保存が既存 transport と読戻しで成立するか」という中核の未知が検証されない。退避・保存・取得は同じ manifest と検証規則を共有し、それぞれの失敗は個別に判定できるので、束ねても独立に判定できなくなる未知は増えない。確定記録、取り込み、R2 → ArtifactsRoot 配置は別の未知なので含めない。

- **進める最低条件**（満たせば GO。判定は §5 の検証経路で行う）:
  1. **退避:** Content 出力 1 件を、元を変えずに ArtifactsRoot へ退避する。`transport.json` の bytes の SHA-256、manifest の `files`（path・size・sha256）、`content/` の実 file 集合が完全に一致する場合だけ公開する。不足・余剰・hash 不一致・path 逸脱・reparse・0 byte の file は拒否する。同じ入力の再実行は完了済みとして再利用し、同じ revision で内容が違うものは拒否する。
  2. **保存:** 退避した 1 件を private operation へ snapshot し、全 object を「子 process の GET で照合 → 404 なら PUT → 子 process の読戻しで照合」の 1 経路で R2 へ置く。`content/*` を先に、`transport.json` を最後に置く。全 object の照合が通った後でだけ、ローカルの receipt を唯一の成功確定点として確定し、`reference` と、それとは別の `manifestSha256` を返す。失敗・応答不明・途中停止・予算超過では成功を返さない。
  3. **取得:** 別 process が `reference` と**独立に渡された**期待 `manifestSha256` から、新規 private 展開先へ `transport.json` と全 file を取得し、全 file の size と SHA-256、集合の一致を検査する。不一致・欠落・余剰は拒否する。取得物は実行しない。
  4. **上書きしないことと続行:** 既存の object と内容が違うものは上書きせず、fail-closed で拒否する。途中で止まった revision を同じ入力で再実行すると、照合が一致した object は再送せず、404 のものだけ PUT して完了できる。
  5. **実 R2 の往復:** 手元の実 Content 出力 1 件（Spring、220 file + `transport.json`）で 1〜4 の正常系を実 R2 に通す。初回の PUT 数が 221、全 object の照合が一致し、別 process で取得した全 file の hash が manifest と一致し、§2 の時間予算内に終わることを生結果で残す。判定 C の責任とし、実行前に owner の session ごとの許可を得る（§5）。

- **受け入れ条件**（最低条件の観測可能な詳細。別の完了バーではない）:

  | # | 条件 | 確認 |
  |---|---|---|
  | AC1 | 系列の写像は `(contentSet, manifest.target)` → `content/<contentSet>/<target トークン>/<revision>/`。target は `StandaloneWindows64-Player` → `windows-x64-player` の 1 行の表だけ。表に無い target は `unsupported-series`。 | offline |
  | AC2 | `contentSet` は `\A[a-z0-9][a-z0-9-]{0,63}\z`、revision は `\A[0-9]{8}T[0-9]{9}Z-[0-9a-f]{32}\z`。これ以外は `invalid-content-identity`。入力 directory 名・manifest の `revision`・`contentSet`・`target` の一致を要求する。 | offline |
  | AC3 | R2 key は `development/content/v1/<repositoryId>/<contentSet>/<target トークン>/<revision>/transport.json` と `…/content/<path>`。任意の key を入力に取らず、検証済みの値から導く。key 全体は 512 字以下。 | offline |
  | AC4 | manifest の path は `PackagePolicy.Relative` の規則（予約名・末尾の dot/space・保護名・`..`・空 segment の拒否、240 字以下）を満たし、文字は `[A-Za-z0-9._-]` と `/` に限る。OrdinalIgnoreCase で重複する path、file と directory の衝突（`a` と `a/b`）を拒否する。`size` が 0 の entry は `unsupported-content`。退避・公開・取得のすべてで同じ規則を使う。 | offline |
  | AC5 | `transport.json` は上限 4 MiB の専用 reader で読み、重複 JSON key を `ArtifactCommands` と同じ方式で拒否する。既存の 1 MiB reader（`Read-BuildStageBytes`・`Read-ArtifactJson`）は使わない。 | offline |
  | AC6 | 公開は退避先から private operation へ snapshot して行う。snapshot は全 file を manifest と照合してから使い、各 PUT の直前に送る file の size と SHA-256 を再照合する。ArtifactsRoot の file を直接送らない。 | offline |
  | AC7 | 各 object は「子 process の GET で照合」から始める。一致なら送らない。404 `NoSuchKey` なら PUT し、子 process の読戻しで bytes と SHA-256 を照合する。それ以外（不一致、403、timeout、応答不明）は、その object を未確認として停止し、成功を返さず、DELETE も上書きもしない。同じ run の中で PUT の応答が不明になったら、その key を再 PUT せずに停止する（次の run の照合から再開する）。初回か続行かで経路を変えない。 | offline（注入した transport・readback で観測） |
  | AC8 | 公開の最初に遠隔の `transport.json` を子 process で照合する。存在して hash が違えば `revision-conflict` で停止し、何も送らない。順序は `content/*`（manifest の順）→ `transport.json`。`transport.json` は、同じ run で全 `content/*` の照合が通った後にだけ置く。 | offline |
  | AC9 | 成功確定点はローカル receipt の atomic rename だけ。receipt は intent の hash、manifest の hash、object 数、合計 bytes、`publishedAt`（確定時の UTC）を持つ。receipt が無い状態で `inspect` は成功を返さない。 | offline |
  | AC10 | intent は最初の送信前に固定し、決定的な値だけを持つ（content-config と Evidence config の hash、repositoryId、系列、revision、manifest hash、全 entry の path・size・sha256）。operation path や時刻は持たない。再実行時は既存 intent と今回の計画を照合し、違えば `intent-conflict`。intent と receipt はどちらも 1 MiB 以下に収まる（1,024 entry × 約 150 B の見積もり）。 | offline |
  | AC11 | 再実行の規則と優先順位: (1) 退避先に違う内容があれば `stage-conflict`、(2) ローカル receipt があり同一入力なら PUT せず同じ `reference` を返す（`existing`）、(3) intent があり違えば `intent-conflict`、(4) 遠隔の `transport.json` の hash が違えば `revision-conflict`、(5) それ以外は AC7 の経路で続行する。ArtifactsRoot を変えて呼んだ場合も (2)〜(4) で同じ判定になる。 | offline |
  | AC12 | 同じ revision の同時公開は片方だけが進む。lock は `contents-v1/…/<revision>/publish.lock` を FileShare.None・非継承で開いた handle で、保持区間は intent の照合から receipt の再読まで。process が終われば解放される。永続の予約は持たない。負けた側は `publish-in-progress`。 | offline（別 process を signal で同期。時間待機を使わない） |
  | AC13 | 時間予算: 公開は 300 + 8×N 秒（N は object 数。Spring で 2,068 s）、取得は 300 + 2×N 秒。全体予算は object ごとの PUT・GET・読戻しの deadline の上限にも効き、実行中の操作も予算で打ち切られる。予算を超えたら成功にしない。予算は注入した時計で検査できる。 | offline（時計を注入） |
  | AC14 | `fetch` は `reference`（`osm-content-v1:`、opaque な Base64url）と、別に渡された `manifestSha256` を要求する。reference が含む hash は照合にだけ使い、期待 hash の代わりにしない。取得した `transport.json` の hash が期待と違えば、ほかの file を取らない。 | offline |
  | AC15 | `fetch` は manifest の各 file を取得し、size と SHA-256 を検査する。集合は完全一致（余剰・欠落を拒否）、path は AC4 の規則。展開先は新規 private で、取得物を実行しない。 | offline |
  | AC16 | 上限（暫定ガード）: object 1,024 件、宣言 size の合計 512 MiB、単一 file 256 MiB（transport の上限）、`transport.json` 4 MiB。宣言 size は取得前に検査し、転送中の上限は transport の 256 MiB だけ（program §5 の「有限の展開量」との差は Content の容量上限へ送る）。上限はこのスライスの暴走防止で、Content 固有の容量上限の決定ではない。 | offline |
  | AC17 | 秘密（資格情報・署名・認証 header）と endpoint は結果・ログ・receipt・intent に出ない。公開 reason は許可語だけで、例外本文を表示しない。 | offline |
  | AC18 | `development/content/v1/` 配下の DELETE は transport の入口で拒否する。このスライスは R2 object を削除しない。 | offline |
  | AC19 | Build/Evidence の既存 offline suite（§5 の 14 本）が変わらず通る。 | 既存 suite |

- **R2 側の上書き防止について:** R2 側には上書きを防ぐ仕組みが無い。現行 transport は条件付き書込みを持たず、bucket rule が `development/content/v1/` に及ぶかも確認していない。「上書きしない」はクライアント側の照合（AC7・AC8）だけで守る。照合と PUT の間に別の writer が書く競合は、同じ PC の同時実行なら AC12 で防ぐが、別 PC の同時実行は防がない（同じ revision を別 PC から同時に公開する運用は想定しない。Content 確定記録で扱う）。

- **応答不明の PUT を再送しない既存規則との整合:** Build は応答不明の key を再送しない。object の状態が分からないまま上書きしうるためである。Content は次の 4 点により、この規則の根拠を保ったまま続行できる。(a) key ごとの bytes は manifest の hash で固定されている。(b) 送る直前に bytes を再照合する（AC6）。(c) 遅れて着いた前回の PUT は同じ bytes なので害が無い。(d) 404 は確定ではなく「送ってよい」手掛かりとしてだけ扱い、送った後は必ず別 process で読み戻す。同じ run の中では応答不明の key を再送しない（AC7）。

- **ここでは答えない問いと所有する後続スライス:**

  | 問い | 所有先 |
  |---|---|
  | Player が `content/` を内包しない場合の Unity 側の変更、互換キー案、`build collect` の検査・`build-info.json`・selection の変更、Build と Content の manifest 規則の統合 | **Content 分離**（program §4 B） |
  | R2 → ArtifactsRoot への配置と、その際の不足 file の再利用 | **Content 取得と再利用**（新規。program §4 B へ登録） |
  | ArtifactsRoot から DIST cache への不足 file の再利用、lease・eviction、Unity バージョンと root schema の束縛、HTTP の file ごとの deadline | **Content 取り込み** |
  | 取得型／監視型の統合フォルダ、複数ワークスペースの衝突、削除主体、設定と選択 UI、「最新」の解決の所有 | **統合 Content フォルダ** |
  | 新旧順序の追記だけの確定記録（形式、書込み方式、revision 削除時の扱い）、「最新」の解決の保存側の部分、別 PC からの同時公開の排他 | **Content 確定記録**（新規。R2 側に LIST または条件付き書込みが要る可能性がある別の未知。program §4 B へ登録） |
  | `contentSet` の命名規約（用途×構成×性能の合成名、`bs4-spring-full` を改名しないこと、ブランチや Build 設定からの決め方、hd/sd 同梱時の部分取得） | **`contentSet` 命名規約**（AC2 の文字種はそれまでの安全な部分集合にすぎない） |
  | Content 固有の上限値、R2 のプラン変更の要否、4 季節の実測、R2 上の孤児 object の回収、0 byte の file への対応、転送中の展開量の上限、読戻しの batch 化 | **Content の容量上限**（program §4 B） |
  | Content の実 DELETE、利用中状態の収集、保護が N を超えた場合、系列の退役、ArtifactsRoot の `.staging` の回収、件数保持がどの層に効くか、保護の収集源の範囲 | **件数保持の運用接続** |
  | 公開・取得で残る private operation（snapshot、読戻し物）の期限清掃 | **未完了 upload と staging**（program §2 の 7 日清掃。Build の operation と同じ扱い） |
  | Windows 以外の target、baked target と外部 revision 照合 target の分離 | Content 分離と、Windows 以外の target を扱うスライス（未定） |

- **判定定義:**
  - **GO:** 最低条件 1〜5 がすべて満たされ、現在の問いに致命的な反証がない。
  - **NO-GO:** 最低条件のどれかが未達、または次のいずれかが判明した場合。(a) 実 R2 の Spring 1 件の公開が AC13 の予算（2,068 s）内に終わらない。(b) 既存 transport で file 単位 object の照合（404 の観測、読戻しの一致）が成立しない。(c) 退避後の `content/` と manifest の整合を保存側が検査できない。
  - 4 季節（`all-full`）の公開は GO の条件にしない。スパイクではないので CONDITIONAL ACCEPT は使わない。
- **停止規則:** 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。確定記録、取り込み、R2 → ArtifactsRoot 配置、命名規約、容量上限、実 DELETE、operation の清掃を現スライスで検証し始めたら停止し、上表の所有先へ移送する。
- **A3 後の例外承認:** owner（2026-10-11、チャット）。理由: C′ が、最低条件 5 の初回公開を最終 head で行っていないことを阻害欠陥とした。同じ revision は不変で DELETE もしないため、最終 head で同じ出力を公開し直せない。置き換える既存条件: 最低条件 5 の対象出力を、最終 head では手元のもう 1 件の実 Spring 出力 `artifacts/bs2b/transport/20260924T141708718Z-8e61…`（contentSet `spring-full`、219 file + `transport.json`）とし、期待する初回 PUT 数を 220、時間予算を 300 + 8×220 = 2,060 s とする。検証予算: この出力での collect・別 process の fetch と全 file 照合・同じ collect の再実行の 1 回ずつ。あわせて、各 object の照合結果を operation に記録しない点は受け入れ条件の違反とせず、診断の改善として件数保持の運用接続の入力へ送る。
- **本文へ転記した実装制約**（AGENTS.md 等から）:
  - 依存は Game → Framework の一方向。asmdef 参照を追加しない（本スライスは Unity を変更しない）。
  - Unity 側の C# に触れない（`#nullable enable`・`record` の規則は適用外）。`tools/Artifacts` の C#（transport・packaging・probe）も変更しない。
  - テストで `Task.Delay`/`Thread.Sleep` を使わない。待機は signal または注入した時計で行う。
  - 秘密は会話・引数・環境変数・ログ・Git・同期先へ出さない。既存の DPAPI `osm` 資格情報と private bucket `osm-artifacts` を使い、新規登録・置換・失効はしない。認証失敗を鍵の再入力・削除で自動修復しない。公開 development URL／custom domain は有効にしない。
  - Artifact Storage の通常操作は、AGENTS.md と `tools/Artifacts/README.md` の事前承認の範囲内にある（`osm-artifacts` の `development/content/v1/`、既存 DPAPI `osm`、非秘密の明示 file 集合、DELETE なし）。そのうえで、owner は本スライスに限り、実 R2 の publish/fetch と資格情報へのアクセスの前に、何をするかを 1 行で示して session ごとに許可を得ることを指定した。これは常時契約より厳しい owner の指定で、矛盾ではない。
  - Unity の実行・テスト・Build は Phase C の責任。このスライスは Unity を使わない。
  - R2 object の DELETE、bucket/rule の変更、通常 writer への管理権限の追加はしない。取得物を実行しない。
  - config・snapshot・読戻し物は private operation（`Get-ArtifactRoot` 配下の 32hex directory と `operation.lock`）の中に置く。
  - 参照 0 を削除理由にしない。未使用 API は削除しない。
  - PR の base は `develop`。ブランチ名は `codex/` で始める。

### A1: r7 §4 B「後続のA1で扱う入力」と Content保存の項目の振り分け（A3 で確定）

**このスライスで決めた（実装する）もの**

| 項目 | 決定 |
|---|---|
| 完了確定点 | ローカル receipt の atomic rename だけ（Build と同じ）。R2 には別の完了マーカーを置かない。`transport.json` を最後に置くのは順序の保証にすぎず、取得側は `transport.json` の存在を完了の根拠にしない。取得側の完全性は、独立した期待 hash と各 file の hash で取得時に自分で証明する。 |
| receipt と intent | LocalApplicationData `OneStarMaker/Artifacts/contents-v1/<repositoryId>/<contentSet>/<target トークン>/<revision>/{intent.json,receipt.json,publish.lock}`（同一ユーザー・同一 PC）。書込みは `Write-BuildImmutable` の規則を再利用する。中身は AC9・AC10。 |
| publish の順序 | 遠隔 `transport.json` の照合 → `content/*`（manifest の順）→ `transport.json`。各 object は AC7 の経路を直列に行う。Player と Content のどちらを先に保存するかは決めない（独立した 2 コマンド。結び付きは `build-info.json` の `transportManifestSha256` と本スライスの `manifestSha256` が同じ値であることだけ）。順序の拘束は Content 分離が決める。 |
| 照合の置き場所 | 事前照合の GET も読戻しも、すべて子 process で行う。同じ process 内の GET で一致した object を検証済みにすると、最低条件 2 の「別 process で照合」と食い違うため。代わりに 1 object 約 3.6 s かかり、予算式（AC13）はこれを前提に余裕を持たせた（Spring で実測見積もりの約 2.6 倍）。 |
| contentSet/target の対応表 | target は `StandaloneWindows64-Player` → `windows-x64-player` の 1 行（Build の表と同じ値）。contentSet は AC2 の安全な部分集合で、既存の 5 値はすべて入る。 |
| R2 key | AC3。prefix `development/content/v1/` は新規で、Evidence・Build の prefix と分ける。 |
| 公開側の CLI | `content collect --profile osm --config <content-config> --source <Content 出力の絶対 path> [--artifacts-root <ArtifactsRoot>]`。退避と公開を 1 コマンドで行う（`build collect` と同じ形）。入力は `transport.json` と `content/` を直下に持つ directory 1 件。ArtifactsRoot は `--artifacts-root` か `OSM_ARTIFACTS_ROOT`（既定値なし、`Resolve-BuildArtifactsRoot` を再利用）。最新の出力を自動選択しない。 |
| 取得側の CLI | `content fetch --profile osm --config <content-config> --reference <osm-content-v1:...> --sha256 <独立した期待 manifestSha256>` → 新規 private 展開先（`transport.json` + `content/…`）。`content inspect --profile osm --config <content-config> --content-set <…> --target <トークン> --revision <…>` はローカル receipt から同じ `reference` と `manifestSha256` を返す（応答だけを失った場合の回復）。inspect はローカル receipt しか見ないので、遠隔の object が後で失われても `existing` を返す。これは既知の限界で、件数保持と確定記録の入力にする。 |
| content-config | `build-config` と同じ形の非秘密 8 field JSON を新設する。`schemaVersion=1`、`purpose=content`、`profile=osm`、既存登録済み Evidence config の絶対 `evidenceConfigPath` とその bytes の `evidenceConfigSha256`、canonical `repositoryId`、`prefix=development/content/v1/`、`policy=content-publish-v1`。`build-config` は流用しない。共有するのは `Read-EvidenceConfig` と `Assert-ArtifactOperationFile` だけで、8 field と許可値の検査は `ContentContract` に置く（Build と種別ごとの許可値は独立に変わるため、約 15 行の複製を許す）。`BuildContract` は変えない。CLI は Build と同じく、config を新規 private operation へ複写してから使う。 |
| 途中で止まった upload を「未完了 upload と staging」に載せるか | R2 に残る途中の object は載せない（削除せず、同じ revision の再実行で続行する対象として残す。期限清掃の対象にすると、続行中の revision を消しうるため）。R2 の孤児回収は Content の容量上限へ送る。ローカルに残る private operation（config の複写、snapshot、読戻し物）は「未完了 upload と staging」の清掃へ送る（Build の operation と同じ扱い）。ArtifactsRoot の `.staging` は件数保持の運用接続へ送る。intent は消さない。 |
| `contentSet` 命名規約との関係 | 本スライスは安全な部分集合の文法（AC2）だけを決める。命名規約の残り（合成名、改名しないこと、決め方、hd/sd）は後続。既存の 5 値はすべて部分集合に入るので、命名規約の確定前に保存を始めても改名は生じない。 |
| 0 byte の file | 退避前に `unsupported-content` で拒否する（現行 transport が 1 byte 未満の PUT・読戻しを拒否するため）。対応は Content の容量上限へ送る。 |
| r7 後続入力「新規 private 展開先と不足 file 再利用の両立」 | fetch は常に新規 private 展開先へ取得する。再利用元（既存の ArtifactsRoot の revision）を読むのは後続スライスで、取得 file を再 hash して使う形にする。本スライスは再利用しない。 |
| r7 後続入力「baked target と外部 revision 照合 target」 | 本スライスは manifest の `target` 1 値だけを扱う。分離は Content 分離と Windows 以外の target へ送る。 |

**後続へ送るもの**

| 項目 | 送り先 | 理由 |
|---|---|---|
| 確定記録（新旧順序、manifest digest、追記だけの記録）、revision 削除時の記録の扱い、別 PC からの同時公開 | Content 確定記録 | 追記の原子性には LIST または条件付き PUT が要り、現行 transport に無い。保存の成否とは独立に失敗しうる別の未知。本スライスの receipt は `publishedAt` と manifest hash を持つので、その入力になる。 |
| 保護の収集源の範囲、ArtifactsRoot の削除規則との関係、保護が N を超えた場合 | 件数保持の運用接続 | 実 DELETE を所有するスライスの責務。 |
| R2 → ArtifactsRoot 取得時の不足 file の再利用 | Content 取得と再利用 | 本スライスは全 file を取得する。保存形式は manifest の各 file に hash があるので、不足判定ができる形になっている。 |
| 容量上限、孤児回収、0 byte、転送中の展開量の上限、batch 読戻し | Content の容量上限 | §1 の実測と §2 の見積もりを入力にする。 |

## 3. 責務マップ

行数は現在値（`wc -l`）。既存 module は 1 行が長く密なため、行数より責務の数で見る。依存は **Content → Build の一般規則（退避の rename、immutable 書込み、ArtifactsRoot の解決）** の向きだけを許し、Build → Content の依存は作らない。Build 側の例外語（`stage-conflict`、`build-store-unavailable` 等）は `ContentApplication` が Content の許可語へ写す。

| file | 責務（変更理由） | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 行数 |
|---|---|---|---|---|---|---|
| `tools/Artifacts/ContentContract.psm1`（新規） | Content の値の規則: 系列・identity の文法、target 表、key 導出、`osm-content-v1:` reference の codec、manifest の 4 MiB reader と型・集合・path の検査（AC2〜AC5、AC16）、content-config の 8 field と許可値。値の規則は純粋関数で、config の検証と manifest の読取だけが読取 I/O。 | 状態を持たない | `ArtifactCommands`、`ArtifactPaths`、`EvidenceContract`（`Read-EvidenceConfig`）、`PackagePolicy` | module 関数 | fixture で文法・拒否ケースを単体検査 | 0 → 約 220 |
| `tools/Artifacts/ContentStage.psm1`（新規） | 退避先の決定と退避。copy → hash 照合 → rename の規則は `Invoke-BuildStage` を再利用する。 | 退避先 `<ArtifactsRoot>/content/...` を所有。削除しない。`.staging` は自分が作ったものだけ失敗時に消す（Build と同じ） | `BuildStage`、`ContentContract` | module 関数 | 冪等・`stage-conflict`・hash 不一致・0 byte 拒否 | 0 → 約 60 |
| `tools/Artifacts/BuildStage.psm1`（変更） | `Invoke-BuildStage` の `InfoBytes` を省略可能にする（`$null` なら info file を書かない）。`BuildApplication` の内部関数 `Get-BuildInventory` をここへ移して export する。既存の挙動は変えない。 | 既存 | 既存 | 既存 + `Get-BuildInventory` | `BuildCollect`・`BuildPublish` で不変を確認 | 121 → +約 25 |
| `tools/Artifacts/BuildApplication.psm1`（変更） | `Get-BuildInventory` の定義を削り、`BuildStage` から呼ぶだけにする。挙動は変えない。 | 既存 | 既存 | 不変 | 同上 | 260 → −約 15 |
| `tools/Artifacts/ContentStore.psm1`（新規） | ローカル `contents-v1` の root（Windows・LocalApplicationData・専用 ACL。`Get-BuildRoot` と同じ規則の別 root）、`publish.lock`（AC12）、intent の固定と照合（AC10）、receipt の確定と読取（AC9）。 | 永続（同一ユーザー・同一 PC）。lock は publish 1 process | `ArtifactAcl`、`ArtifactCommands`、`BuildStore`（`Write-BuildImmutable`）、`ContentContract` | module 関数。test 用の `TestRoot`・fault hook は非 export | 部分書込み・破損 receipt・intent 不一致・排他 | 0 → 約 150 |
| `tools/Artifacts/ContentTransfer.psm1`（新規） | object 単位の転送: AC7 の経路（子 process の照合 → PUT → 子 process の読戻し）、取得時の GET と hash 検査、予算（AC13）の所有。読戻しは 1 publish 1 operation の中に **object ごとの子 directory**（32hex 名 + `operation.lock`。既存の operation の前提を満たす）を作って行い、`Protect-ArtifactTree` の走査を object 単位に限る。削除はしない。予算は `ArtifactApplication` の module scope の `$script:Budget` に、同じ形の budget object（`Remaining`・`HardRemaining`）を設定・復元して効かせる。Evidence の関数には依存しない。 | 呼出し単位（状態は持たない。operation の所有者は ContentApplication） | `ArtifactApplication`（`Invoke-ArtifactNetwork`、`Invoke-ArtifactReadback`、`New-ArtifactRequest`、`New-ArtifactOperation`）、`ContentContract` | module 関数。transport・readback・時計の注入点は非 export | 順序・404・不一致・応答不明・予算超過を注入で観測 | 0 → 約 170 |
| `tools/Artifacts/ContentApplication.psm1`（新規） | `collect`／`inspect`／`fetch` の順序、private operation（config 複写・snapshot・展開先）の作成、AC11 の分岐と優先順位、receipt の確定、結果の許可語。 | orchestration。operation は作るだけで消さない | 上記 4 module、`ArtifactApplication`、`BuildStage`（`Resolve-BuildArtifactsRoot`） | `Invoke-ContentCollect/Inspect/Fetch` | 全経路の offline 検証 | 0 → 約 240 |
| `tools/artifacts.ps1`（変更） | 先頭で `content` を独立に分岐し、`collect`／`inspect`／`fetch` の引数文法を足す（`build` と同じ形）。 | CLI の composition root | 上記 | CLI | 文法の拒否ケース | 109 → +約 20 |
| `tools/Artifacts/Transport/R2ArtifactTransport.psm1`（変更） | key allowlist に AC3 の 2 形を追加し、その prefix の DELETE を拒否する。 | 既存 | 既存 | 既存 | AC3・AC18 | 45 → +約 3 |
| `tools/Artifacts/Transport/ArtifactReadback.ps1`（変更） | 読戻し子 process の key allowlist に AC3 の 2 形を追加する。 | 既存 | 既存 | 既存 | AC7 | 66 → +約 2 |
| `tools/Artifacts/tests/Content.*.Tests.ps1`（新規 3 本）と child | §5 の offline 検証 | テスト | 注入点 | `-Case '*' -ResultPath` | — | 0 → 合計約 800（1 本 300 行前後） |
| `tools/Artifacts/README.md`（変更） | Content の操作・上限・既知の限界の節 | 公開面 | — | — | `docs-audit` | 236 → +約 40 |
| `docs/handoff/BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md`（変更） | §4 B の Content 保存の状態と、新規後続 2 件の登録 | 公開面 | — | — | `docs-audit` | 160 → +約 3 |

**警報の判断**

- **500 行:** どの file も超えない見込み。最大は `ContentApplication.psm1`（約 240）。object 単位の処理を `ContentTransfer` に出したので、順序・分岐・確定だけが残る。
- **3 責務:** `ContentContract` は文法・codec・manifest・config の 4 つの変更理由を持つが、`BuildContract`（201 行）と同じ「種別の値の規則」の束として分割しない。どれも Content の値の規則で、別々の所有者・寿命を持たないため。`BuildApplication`（260 行、4 入口）には Content を足さない。
- **50% 以上の増加:** `BuildStage.psm1` は約 20%。`Get-BuildInventory` の移動は所有者の整理で、新しい責務ではない。`artifacts.ps1` は約 18%。引数文法の分離は本スライスでは行わず、次に種別を足すときに分離を求める警報として残す。
- **共有か複製か:** 退避（`Invoke-BuildStage`）と inventory 走査（`Get-BuildInventory`）は共有する。同じ安全規則の所有者を 2 つにしないため。config の 8 field 検査と manifest の規則は複製・並存させる。種別ごとに独立して変わり、共有すると `BuildContract` を変える必要があるため。Build と Content の manifest 規則の統合は Content 分離へ送る。第 3 の利用者が現れたら、中立な module への切り出しを求める警報とする。
- **operation の寿命:** 1 publish・1 fetch につき 1 operation を作り、削除しない（Build と同じ）。所有者は `ContentApplication`、清掃は「未完了 upload と staging」の後続。読戻しの子 directory は operation の中にあり、同じ寿命を持つ。

**テスト可能性:** 中核の判断（文法、key 導出、path・集合の検査、AC7 の分岐、AC11 の優先順位、順序、予算、lock）は、注入した transport・readback・時計と、隔離した store・transfers root・ArtifactsRoot だけで検査できる。注入点（`ContentStore` の `TestRoot`・fault hook、`ContentTransfer` の transport・readback・clock hook、`BuildStage` の既存 `StageHook`、`ArtifactPaths` の既存 test root）は export せず、CLI から差し替えさせない。実 R2 は判定 C だけが使う。

## 4. 実装計画

- **変更対象:** §3 の表。Unity、`unity/` 配下、Evidence の module、`BuildContract.psm1`、C# の project は変更しない。
- **順序:**
  1. `ContentContract` と `Content.Contract.Tests`。
  2. `BuildStage` の変更（`InfoBytes` の省略可能化、`Get-BuildInventory` の移動）、`BuildApplication` の追随、`ContentStage`。`BuildCollect`・`BuildPublish` を通す。
  3. `ContentStore`（root・lock・intent・receipt）。
  4. `ContentTransfer`（照合経路・子 directory の読戻し・予算）。
  5. `ContentApplication`、`artifacts.ps1`、transport・readback の allowlist。`Content.Publish.Tests`・`Content.Fetch.Tests`。
  6. README と program の更新、§5 の判定必須の通し（B 完了時に関連 offline suite を実行する）。
- **Phase B から Phase A へ差し戻す条件**（A 再開）:
  - 予算式（AC13）、照合の経路（AC7・AC8）、成功確定点（AC9）、lock（AC12）、operation の寿命の契約を変えないと最低条件を満たせないとき。
  - 計画外の公開 API、asmdef、所有者、寿命、削除主体、C# の変更が必要になったとき。
  - 既存の `Invoke-ArtifactReadback` が object ごとの子 directory で使えず、共有 module の契約変更が要るとき。
- **B 適応で足りるもの**（A を再開しない）: allowlist の正規表現の細部、結果の許可語の追加、注入点の具体的な形、テストの分け方、README の構成。
- **対象外を維持する方法:** 退避先・operation・R2 object を削除しない。R2 DELETE を allowlist で拒否する（AC18）。`latest` 等の別名を作らない。確定記録の LIST や条件付き書込みを実装しない。fetch は ArtifactsRoot へ書かない。

## 5. テストとレビュー計画

- **単体テスト**（offline。隔離した store、注入した transport・readback・時計、非秘密の合成 fixture）:
  - `Content.Contract.Tests.ps1`: AC1〜AC5、AC16（大文字小文字だけ違う入力、予約名、末尾 dot、file/directory の衝突、0 byte、path 逸脱、重複 key、余剰/欠落、hash 不一致、未知 target、上限、config の許可値）。
  - `Content.Publish.Tests.ps1`: 退避（最低条件 1、`stage-conflict`）、AC6〜AC13、AC17、AC18（公開順序、事前照合の一致/404/不一致、応答不明で同じ run 内は再送しない、続行、`revision-conflict`・`intent-conflict` の優先順位、同時実行を別 process で signal 同期、予算超過と実行中の打切り、秘密非露出、DELETE 拒否、子 directory での読戻し）。
  - `Content.Fetch.Tests.ps1`: AC14、AC15（期待 hash 不一致で他を取らない、欠落・余剰・改ざん、別 process での取得）。
  - 引数文法は `artifacts.ps1` の既存拒否ケースに倣う。
  - fixture は Spring の実 Content の**形**（平坦な 32hex 名、`.cf`/`.resS`、`BuildManifestHash.txt`）を模した小さな合成データ。実出力を fixture に入れない。
- **差し戻し中の起点 `-Filter`**（C が根拠付きで変更可。受け入れ条件の追加ではない）: Content の 3 suite、`BuildCollect.Tests.ps1`、`BuildPublish.Tests.ps1`。`pwsh tools/Artifacts/tests/<suite> -Case '*' -ResultPath <path>`。
- **判定必須テスト（GO 候補 head）:**
  1. .NET 3 build（`Probe/R2RouteTransport.csproj`、`Packaging/ArtifactPackaging.csproj`、`Transport/R2ArtifactTransport.csproj`。README の「Evidenceの責務とoffline検証」のコマンド）。
  2. 既存の Artifacts suite 14 本: `Credentials`、`RouteProof`、`R2RouteTransport`、`ArtifactPackage`、`ArtifactTransfer`、`ArtifactRotation`、`ArtifactEvidence`、`EvidenceRetention`、`EvidenceCleanup`、`EvidenceSchedule`、`EvidenceReset`、`BuildRetention`、`BuildPublish`、`BuildCollect`。
  3. 新規 Content suite 3 本。
  4. `tools/Workflow/tests/TaskLifecycle.Tests.ps1`、`tools/Harness/tests/Harness.Tests.ps1`。
  5. `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。
  6. 最低条件 5 の実 R2 往復（下表）。
  - 各 suite の registered／selected／executed が非空で一致し、failed 0 であることを生結果で残す。H1 を採用しないので、Harness の固定集合（`artifact-evidence-lifecycle`）は変更せず、判定 C が上記を直接実行する。
- **全 EditMode 回帰の適用除外:** あり。理由: Unity のソース・asmdef・Scene・Prefab・Addressables のいずれも変更しない。代替証拠: 上記の判定必須と、`git diff --stat <base>..<head>` に `unity/` 配下が含まれないことの確認。
- **統合・Unity テスト:** なし（Unity を起動しない）。
- **操作・実行時・目視条件の検証経路**

  | 条件 | 担当 | 環境・初期状態 | 操作 | 観測と合否 | 保存する証拠・受け渡し |
  |---|---|---|---|---|---|
  | 最低条件 1〜4（offline） | B（完了時に関連 suite）／C（判定） | 隔離した一時 store・transfers root・ArtifactsRoot と合成 fixture | §5 の suite | XML 上の実行テスト名と件数、failed 0 | 生結果。`review-evidence.md` に従い evidence bundle へ収録 |
  | 最低条件 5（実 R2 往復） | **判定 C。実行前に owner の session ごとの許可を得る** | 判定 head の Release build。手元の実出力 `artifacts/bs4/transport/20260924T202227427Z-b80d…`（220 file）。既存の DPAPI `osm`、登録済み Evidence config と repositoryId、判定 C が作る content-config。ArtifactsRoot は判定 C が用意する空の folder（同期 folder でない場所） | `content collect` → **別 process** で `content fetch`（独立に渡した `manifestSha256`）→ 取得物の全 file の size/hash を manifest と照合 | 初回の PUT 数 221（operation の観測記録と intent の entry 数で数える）、全 object の照合が一致、取得物の全 file が manifest と一致、所要時間が 2,068 s 以内、`status=passed` | collect と fetch の結果 JSON（`reference`・`manifestSha256`）、PUT・GET の観測数、所要時間、照合ログ。秘密・endpoint を含まない。evidence bundle へ収録し、C′ にも同じものを渡す |
  | 同じ入力の再実行 | 判定 C（上と同じ許可の範囲） | 上の公開の直後 | 同じ `content collect` を再実行 | `existing` が返り、PUT 0 | 結果 JSON |

- **未知の操作経路の疎通結果と確認地点:** A3 前に、既存の probe key で小さな疎通を行った（§1）。PUT・GET・404 の観測、PUT 直後の GET の一致、子 process の GET の時間は成立を確認した。**未確認**は次の 3 点で、**初回確認は Phase C**（判定 C、最低条件 5）。(1) 最大 1.0 MiB の object の転送時間。(2) 221 件を連続で送ったときの throttling の有無。(3) `development/content/v1/` に bucket rule が及ぶか。理由: 実際の Content prefix への書込みは、実装が無い Phase A では行えないため。不成立時の対応: 時間の不足（NO-GO (a)）や throttling は、予算式・照合経路の契約変更か読戻しの batch 化（Content の容量上限から前倒し）が要るので、owner に返して A を再開する。認証・権限の失敗は、資格情報を再入力・削除で自動修復せず owner へ返す。bucket rule が及んで上書きや PUT が拒否される場合は、観測結果を記録して owner へ返す。
- **人間の判断が必要な条件:** なし。実 R2 操作の許可は owner が与える（観察記録は不要。生結果で判定する）。
- **機械検査:** `pwsh tools/contract-audit.ps1`（Phase B 完了時と判定 C）、`pwsh tools/docs-audit.ps1`、.NET 3 build。

- **管理方式（H1/CURRENT）:** A1 では採用を提案したが、A3 では**採用しない**。owner が H1 の採否を明示しておらず、workflow は「CURRENT 管理方式の移行を明示していない作業に暗黙適用しない」と定めているため。本書（tracked HANDOFF）を正とし、Evidence の保存・引渡しは `review-evidence.md` に従う（H1 の採否とは独立）。Harness の固定集合は変更しない。

- **A0/A1 主担当・モデル・ベンダー:** 初稿は Claude Sonnet 5.5（`claude-sonnet-5-5`）／Anthropic（前 session）。A0 実測の再検証と A1 の改訂は Claude Opus 5.5（`claude-opus-5-5`）／Anthropic。
- **A2 独立レビューごとの観点・担当・モデル・ベンダー:** ① アーキテクチャゲート: Claude Opus 5.5／Anthropic（新規 session。当初割り当てた Fable 5.1 は利用クレジット不足で起動できず差し替えた。A1 改訂担当と同じモデルなので独立性制約あり）。② 失敗経路・安全契約・検証経路: Claude Haiku 5.5／Anthropic（新規 session）。どちらも同じ入力版（sha256 `4efb3ab7…ae8a`）を読み、互いの指摘を見ていない。③ A0 だけを読む代替構成案: risk `normal` のため実施していない。
- **A3 統合担当・モデル・採否:** Claude Opus 5.5／Anthropic が統合し、owner が 2026-10-11 に採否（下表）と凍結を承認した。
- **C' 用に予約した担当・モデル・ベンダー:** Phase A で使っていない Grok 系（xAI。`cursor-agent` は Grok 系の明示指定でのみ使う）または人間担当。B/C の担当が決まった後、B/C と異なることを確認して owner が確定する。
- **独立性の強化条件を満たせない場合の理由:** risk `normal` のため強化条件は必須でない。Phase A は Anthropic の 3 系列を使ったので、C′ を AI が担当する場合は Grok 系を使えば強化条件も満たせる。

### A2 独立レビュー結果と A3 の採否

アーキテクチャゲート（ARCH-1〜16）と失敗経路（FAIL-1〜23）の指摘を論点ごとにまとめた。blocker は ARCH-1、FAIL-1、FAIL-2 の 3 件で、いずれも解消した。

| # | 論点（指摘） | 採否 | 反映先と理由 |
|---|---|---|---|
| 1 | 判定必須の suite 集合が古く、AC19 を判定できない（ARCH-1 blocker、FAIL-16・23） | 採用 | §5 の判定必須に既存 14 suite と最低条件 5 を名前で列挙。H1 を採らないので Harness の集合は変えない。 |
| 2 | intent が無い経路で GET せずに PUT し、上書きしうる（FAIL-2 blocker、ARCH-8、FAIL-12） | 採用 | AC7・AC8。初回か続行かを問わず子 process の照合から始め、遠隔 `transport.json` を先に照合する。R2 側に上書き防止が無いことを §2 に明記。 |
| 3 | 実 R2 の許可が常時契約の事前承認と食い違う。未知経路を凍結前に確かめていない（FAIL-1 blocker、FAIL-10） | 一部採用 | 許可の手順は owner の明示指定として維持し、事前承認の範囲内であることと併せて §2 に記録。凍結前の小さな疎通は owner の許可を得て実施した（§1）。残る未確認 3 点は Phase C で確認する（§5）。 |
| 4 | 応答不明を再送しない規則との整合を論証していない（FAIL-3・20） | 採用 | §2 の論証と AC7。照合はすべて子 process（§2 A1 表）。 |
| 5 | 送る bytes の所有者が無い（ARCH-10、FAIL-4） | 採用 | AC6。private snapshot と PUT 直前の再照合。 |
| 6 | operation の寿命と読戻しの二乗コスト。object ごとの operation を B 適応に分類していた（ARCH-2、FAIL-5・6・9） | 採用 | §3 の `ContentTransfer` と警報判断。1 publish 1 operation、object ごとの子 directory、削除しない。清掃は「未完了 upload と staging」へ。疎通で二乗コスト（Spring 約 4 分、4 季節 約 65 分）を確認した。 |
| 7 | 全体予算が実行中の操作を止められず、NO-GO の閾値も未凍結（ARCH-4・12、FAIL-14） | 採用 | AC13 と判定定義。予算は `$script:Budget` の注入点で object ごとの deadline にも効かせる。式は疎通の結果から 300 + 8×N 秒に決めた（A2 時点の暫定 300 + 5×N では Spring の余裕が約 1.7 倍しかないため）。 |
| 8 | 0 byte の file を保存できない（ARCH-3） | 採用 | AC4。退避前に拒否し、対応は Content の容量上限へ送る。 |
| 9 | manifest の解析・path 規則・上限が既存部品と食い違う（ARCH-7、FAIL-7・8・13） | 採用 | AC4・AC5・AC16。`ContentContract` が規則を所有し、Build の規則と並存させる。転送中の展開量の上限は容量上限へ送る。 |
| 10 | config の検証を共有すると BuildContract が変わる（ARCH-5） | 採用 | §2 A1 表の content-config。共有は 2 関数だけ、8 field の検査は複製。 |
| 11 | inventory の走査が非公開（ARCH-6） | 採用 | §3。`Get-BuildInventory` を `BuildStage` へ移す。起点 filter に `BuildPublish` を加えた。 |
| 12 | intent を書き換えずに続行する規則が無い（ARCH-9、FAIL-19） | 採用 | AC10・AC11、§3 の `ContentStore`。 |
| 13 | 排他 lock が定義されていない（ARCH-11、FAIL-15） | 採用 | AC12。 |
| 14 | PUT 数の観測源、reference の hash、inspect の限界（FAIL-11・21・22） | 採用 | §5 の検証経路、AC14、§2 A1 表の取得側 CLI。 |
| 15 | 依存の向き、ContentContract の性質、reason の優先順位（ARCH-13・14・16） | 採用 | §3 冒頭と警報判断、AC11。 |
| 16 | A0 の事実誤り（ARCH-15、FAIL-17・18） | 採用 | §1 の内訳と contentSet の一覧、§3 の artifacts.ps1 の記述、§5 の suite 数を直した。 |

不採用の指摘は無い。A2 の総括: 両レビューとも、問いの絞り方、r7 契約の順守、module の分け方、`BuildStage` の共有の方向性を妥当と判断した。B 適応に回されていた続行・上書き防止・operation の寿命・時間予算・lock を、本凍結版ですべて契約として決めた。

## 6. Phase B 実装結果

- 実装: §3 の責務マップを §4 の順で実装した（1 commit）。
  - 新規 module: `ContentContract`（値の規則・reference codec・4 MiB manifest reader・content-config・intent/receipt の形）、`ContentStage`（退避先の決定、`Invoke-BuildStage` の再利用、snapshot、PUT 直前の再照合）、`ContentStore`（`contents-v1` root、`publish.lock`、intent の固定と照合、receipt の確定と読取）、`ContentTransfer`（AC7 の照合経路、object ごとの子 directory での読戻し、予算の設定と復元、取得）、`ContentApplication`（`collect`／`inspect`／`fetch` の順序と AC11 の優先順位）。
  - 変更: `BuildStage`（`InfoBytes` を省略可能にし、`Get-BuildInventory` を移して export）、`BuildApplication`（定義を削り呼ぶだけに）、`artifacts.ps1`（`content` grammar）、`R2ArtifactTransport.psm1`・`ArtifactReadback.ps1`（allowlist に Content の 2 形、`development/content/v1/` の DELETE 拒否）、`tools/Artifacts/README.md`（Content の節）、program §4 B の状態 1 語。
  - 新規 test: `Content.Contract.Tests`（9 case）、`Content.Publish.Tests`（12 case）、`Content.Fetch.Tests`（5 case）、共通 `ContentTestSupport.ps1`、子 `ContentChild.ps1`。Unity・C#・`BuildContract`・Evidence module は変更していない。
  - 計画外の公開 API、asmdef、所有者、寿命、削除主体、C# の変更、および §4 の差し戻し条件は発生しなかった。`Invoke-ArtifactReadback` は object ごとの子 directory（32hex 名 + `operation.lock`）で契約変更なしに使える（`Assert-ArtifactOperationFile` の前提を満たすことを offline test で確認）。
- HANDOFF との差（B 適応の記録。契約は変えていない）:
  1. manifest の path に `..` を含む名前（`x..y` など）を `invalid-content-manifest` で拒否する。C# の transport の key 検査（`SafeKey`）が key 全体の `..` を拒否するため、送信途中の失敗にせず検証側で先に弾く。AC4 の文字種の絞りの内側であり、実 Spring の名前（32hex + 拡張子）には影響しない。
  2. 公開の最初の遠隔 `transport.json` 照合（AC8）の結果を、最後の `transport.json` の「照合」にも使う（`match` なら再送せず、`missing` なら二重の GET をせず直接 PUT → 読戻し）。GET 数は §1 の見積もり（2N）と一致する（Spring 221 object で 442。回帰の観測は offline で `gets = 2N`）。
  3. `ContentTransfer` の注入点は時計だけを持ち、transport・readback は `ArtifactApplication` の既存 hook（`TransportHook`／`ReadbackHook`）を使う。非 export。予算は `Enter-ContentBudget`／`Exit-ContentBudget`（try/finally で対）で `ArtifactApplication` の `$script:Budget` を設定・復元する。
  4. 結果の許可語を追加した: `invalid-content-config`、`invalid-content-identity`、`unsupported-content`、`invalid-content-manifest`、`content-mismatch`、`snapshot-changed`、`object-conflict`、`object-unconfirmed`、`put-unconfirmed`、`readback-unconfirmed`、`budget-exceeded`、`content-store-unavailable`、`manifest-mismatch`、`download-unconfirmed` ほか（`Get-ContentReason` の一覧）。Build 側の語（`snapshot-conflict` 等）は写し直す。`inspect` の失敗は `receipt-unavailable` の 1 語。
  5. content-config の複写: `collect`／`fetch` は config の bytes を新規 private operation へ複写して使う（原本は任意の絶対 path でよい）。`inspect` は operation を作らないため、渡された config を private operation 内で読む（`Read-ContentConfig` は Build と同じ制約）。
  6. 結果 JSON に `objects`（`total`／`sent`／`verified`／`puts`／`gets`）と `residue.remote` を持たせた（判定 C の PUT 数・GET 数の観測用）。`gets` は子 process の GET 数で、取得（fetch）の GET は含まない。
  7. intent はファイルの bytes の一致で照合する（決定的な bytes なので同値）。遠隔照合（読取のみ）の後、最初の PUT の前に書く。そのため `revision-conflict` では intent を作らない。
  8. AC12 の lock は `FileShare.None`・非継承の handle。既存 lock file の ACL が欠けていれば（排他 open 後に）付け直す。
  9. `Content.Publish` の「同時公開」は、子 process が最初の PUT の直前（lock 保持中）で signal を送って待ち、親が同じ revision を公開して `publish-in-progress` を観測する形（時間待機なし）。
  10. 同じ revision の初回同時公開で、退避の rename に負けた側も lock へ進むようにした（AC12 の「負けた側は `publish-in-progress`」を満たすため）。`Invoke-ContentStage` で `Invoke-BuildStage` が `stage-conflict` を投げたとき、退避先を期待 entry と再照合し完全一致なら `existing` として lock へ進む（`BuildStage` は変更しない）。本当の不一致だけが `stage-conflict`。負けた側は `publish-in-progress`（または勝者の完了後なら `existing`）になる。`concurrent-stage` test（2 子 process を `StageHook` の `before-rename` で signal 同期）を追加。あわせて `contents-v1` 階層の作成を冪等（CreateDirectory のあと ACL が無ければ設定）にし、他 process が作成した直後の ACL 未設定を見ても失敗しないようにした。
  11. §3 に無かった依存辺を記録する。いずれも循環ではなく、Build→Content の逆向きも無い。`ContentTransfer`→`ContentStage`: snapshot の再照合（`Assert-ContentSnapshotFile`）の実体が `ContentStage` にあり、PUT 直前の再 hash の規則の所有者を 1 つにするため。`ContentTransfer`→`Credentials/CredentialStore`: run 開始時の資格情報 generation の固定（`Get-CredentialStatus`）を転送側が持つため（`BuildApplication` も同じ module を直接 import）。`ContentStore`→`Workflow/WindowsStorePaths`: `Get-BuildRoot` と同じ物理 path の解決（`Get-WindowsStorePathIdentity`）のため。便宜だけの辺は無く、削除はしていない。
  12. `ContentApplication` のコメントと README の再実行の優先順位を AC11 の番号順（1 stage-conflict、2 receipt があり同一入力なら existing、3 intent が違えば intent-conflict、4 revision-conflict、5 続行）に直し、「同一入力」は intent.json の bytes 一致で判定し、実装は (2) と (3) を同じ intent 照合で先に評価する（結果は番号順と同じ）ことを明記した。
  13. （B 適応の修正）AC13「予算を超えたら成功にしない」を成功確定点まで保つ。publish は予算を receipt の atomic rename の直前（`Write-ContentReceipt` の前）まで有効にし、超えていれば `budget-exceeded` で receipt を作らない。fetch は最後の保護（`Protect-ArtifactTree`）と集合検証の後、`passed` を返す直前に予算を確認する。Enter／Exit は try／finally で対のまま。注入時計の test を追加した（`budget` case の全転送後に時計を進める検査と、時計を戻した同一入力の再実行が再送なしの照合だけで完了する検査。`fetch-budget-and-errors` case の全 download 後に時計を進める検査）。`ContentTransfer` は `Assert-ContentBudget` を export しただけで、転送経路・transport・readback の file は変更していない。
- 未実行:
  - 実 R2 の往復（最低条件 5、判定 C の責任。owner の許可前なので R2・ネットワーク・資格情報には一切触れていない）。最大 1.0 MiB の転送時間、221 件連続の throttling、bucket rule が及ぶかは未確認のまま。
  - Unity の実行・テスト・Build（適用外）。`tools/Workflow/tests/TaskLifecycle.Tests.ps1`、`tools/Harness/tests/Harness.Tests.ps1`（本変更は触れていないため未実行）。.NET 3 build は出力が既存だったため再 build していない。
  - 既存 14 suite のうち `EvidenceCleanup` の `fresh-process-cleanup-contract` は、この実行環境の PowerShell に `rg`（ripgrep）が無く失敗した（本変更と無関係。他の 30 case は通過）。判定 C は `rg` のある環境で確認すること。
- 実行したテスト（Phase B 完了時、isolated な temp store・transfers・ArtifactsRoot と注入 hook のみ。各 `-Case '*'` の registered／selected／executed が一致し failed 0）: `Content.Contract` 9／9、`Content.Publish` 12／12、`Content.Fetch` 5／5、`BuildCollect` 9／9、`BuildPublish` 9／9、`BuildRetention` 9／9、`Credentials` 22／22、`RouteProof` 26、`R2RouteTransport` 7、`ArtifactPackage` 9／9、`ArtifactTransfer` 20／20、`ArtifactRotation` 11／11、`ArtifactEvidence` 13／13、`EvidenceRetention` 14／14、`EvidenceSchedule` 78／78、`EvidenceReset` 31／31、`EvidenceCleanup`（上記の 1 case を除く全て）。`pwsh tools/contract-audit.ps1`（errors 0）、`pwsh tools/docs-audit.ps1`（errors 0。warning 1 は §7／§8 が未記入の既存の通知）。
- implementation head commit: `5aa9d740387e6e3b6b7aa63420061de8b854ac82`
- Phase B 担当・モデル・ベンダー: Claude Sonnet 5.5（`claude-sonnet-5-5`）／Anthropic。新規 session。

## 7. Phase C

**判定: GO**（判定 C、最終 implementation head `5aa9d74`。最低条件 1〜5 をすべて満たし、現在の問いへの致命的な反証は無い）。

経緯: `9c8e703` で判定 C（GO）と C′（1 回目）を行った。C′ が予算の扱い（AC13）を阻害欠陥として指摘したため B へ差し戻し（C′ 起因の差し戻し 1 巡目）、`5aa9d74` で判定 C をやり直した。`9c8e703` の判定は無効とし、以下は `5aa9d74` の結果である。

- 種別: 発見 C（`603ede1`）→ B へ差し戻し 1 巡 → 発見 C の再レビュー（`603ede1..9c8e703`）→ 判定 C（`9c8e703`、C′ の指摘で無効化）→ C′ 起因の差し戻し 1 巡 → 差分レビュー（`9c8e703..5aa9d74`、阻害欠陥なし）→ 判定 C（`5aa9d74`）。
- 使用した evidence bundle の取得案内・状態: 判定用 bundle（diff・stat・name-status、Phase A snapshot、Phase B result、判定前の機械検査、判定必須の生結果、実 R2 往復の生結果）を生成済み。未保存（Artifact Storage への publish は Phase D で行う）。
- 構造適合: §3 の割当てと実際のメンバー・依存・行数は一致する（最大 `ContentApplication` 約 160 行、500 行・50% の警報は発火なし）。依存は Content → Build の一般規則の向きだけ。計画外の依存 3 辺は §6 の 11 に B 適応として記録済み（循環・逆向きなし）。`unity/` 配下の diff は空。
- 現在の問いを阻害する findings: 判定時点で残り 0 件。発見 C で 2 件（いずれも軽微）を B へ差し戻し、解消を確認した。(1) 同じ revision の初回同時公開で、退避の rename に負けた側が lock に達せず `stage-conflict` を返していた（AC12 違反）。(2) §3 に無い依存辺が §6 に未記録だった（§4 の「計画外の依存」規則）。
- 後続スライスへ移送する findings:
  - テストのコメント位置のずれ（`Content.Publish.Tests.ps1` の予算の block）→ 次に同じ file を触る作業で直す体裁の所見。
  - 予算が退避・snapshot・receipt の区間に掛からず、残りが 15 s 以下では reason が `budget-exceeded` でなく `object-unconfirmed` になる（成功は返さない）→ Content の容量上限。
  - 1 回の collect で各 file を約 7 回読む（上限 512 MiB 付近の I/O）→ Content の容量上限。
  - `ContentStore` は `Create` 時に既存 directory の ACL 不一致を付け直す（Build store は拒否）。intent・receipt・lock は個別に ACL を検査するので凍結条件の違反ではない → Build と方針をそろえる判断として件数保持の運用接続へ。
  - 再実行で `existing` を返す結果 JSON に `objects` 欄が無い（PUT 0 は転送経路に入らないことと所要 2 s で確認）→ 診断の改善（件数保持の運用接続の入力）。
  - Phase B result に発見 C の指摘 ID が転記されていた（review-evidence の分離規則）→ `2d9dbf9` で除去済み。C′ には除去後の版を渡した。
- 実行したテストコマンドと対象（判定必須、§5 のまま。`5aa9d74` で再実行。生結果は bundle に収録）:
  - 機械検査（判定の前に実行）: `pwsh tools/contract-audit.ps1` errors 0 / warnings 0、`pwsh tools/docs-audit.ps1` errors 0（warning 1 は §7/§8 の記入状態の既存通知）。
  - .NET Release build 3 本: exit 0、警告 0。
  - offline suite 19 本（`pwsh -NoProfile -File <suite> -Case '*' -ResultPath <path>`）: 全て registered = selected = executed（非空）、failed 0。`EvidenceCleanup` は `rg` が pwsh の PATH に無いため、インストール済みの VS Code 同梱 ripgrep の directory を PATH の先頭に足して実行した（何もダウンロードしていない）。
- テスト結果（件数）: Credentials 22、RouteProof 26、R2RouteTransport 7、ArtifactPackage 9、ArtifactTransfer 20、ArtifactRotation 11、ArtifactEvidence 13、EvidenceRetention 14、EvidenceCleanup 35、EvidenceSchedule 78、EvidenceReset 31、BuildRetention 9、BuildPublish 9、BuildCollect 9、Content.Contract 9、Content.Publish 12、Content.Fetch 5、TaskLifecycle 23、Harness 35（集合一致、並び順のみ異なる）。
- 最低条件 5（実 R2 往復。owner が 2026-10-11 にこの 4 操作を明示承認）:
  - 初回公開は `9c8e703` で行った。同じ revision は不変で、DELETE もしないため、最終 head で初回公開をやり直せない。`9c8e703..5aa9d74` のコード差分は、receipt と fetch の成功前の予算確認だけで、転送経路（`ContentTransfer` の処理、transport、readback）は変わっていない（export 1 語のみ）。そのため初回公開の観測を最終 head の証拠として引き継ぎ、取得と再実行は `5aa9d74` でやり直した。この扱いは Phase D で owner が確認する。
  - PUT 221 件は結果 JSON の `objects.puts` に加え、§5 の方法（operation の観測記録と intent の entry 数）でも確かめた。operation directory には 221 key それぞれに子 process の読戻しが 2 回ずつ（事前照合と PUT 後の読戻し）、計 442 件あり、AC7 では 2 回目の読戻しは PUT の後にしか起きない。`transport.json` は最初と最後。content の key は intent の 220 entry と一致し、`objectCount` は 221。
  - `content collect`（`9c8e703`）（実 Spring 出力 `20260924T202227427Z-b80d…`、空の ArtifactsRoot）: `status=passed`、`publish=published`、`objects.puts=221`、`gets=442`、所要 785 s（予算 2,068 s）。
  - 別 process の `content fetch`（source の `transport.json` から独立に計算した `manifestSha256` を渡す）: `9c8e703` で `status=passed`・30 s、`5aa9d74` で `status=passed`・29 s。どちらも取得物を独自に照合し、`transport.json` の bytes 一致、`content/` 220 file の集合一致、size/SHA-256 の不一致 0、source の bytes とも一致。
  - 最終 head での初回公開（owner の例外承認による置換。§2）: `artifacts/bs2b/transport/20260924T141708718Z-8e61…`（`spring-full`、219 file）を `5aa9d74` で公開し、`status=passed`、`objects.puts=220`、`gets=440`、所要 760 s（予算 2,060 s）。operation の記録は 220 key それぞれに子 process の読戻し 2 回（計 440）、content の key は intent の 219 entry と一致。別 process の `content fetch`（独立に計算した `manifestSha256`）は `passed`・27 s で、`transport.json` の bytes 一致、219 file の集合一致、不一致 0。同じ collect の再実行は `existing`・同じ reference・3 s。
  - 同じ `content collect` の再実行: `9c8e703` で `publish=existing`・同じ reference・2 s、`5aa9d74` で `publish=existing`・4 s（転送なし）。
  - 出力に endpoint・秘密が無いことを検査した（該当 0）。R2 の DELETE はしていない。
- 判定必須のうち未実行: なし。全 EditMode 回帰は §5 の適用除外（`unity/` 配下の diff 空）。
- 重い検証を発見段階で限定実行した場合の理由と範囲: なし。
- 未確認事項: 4 季節（`all-full`）の公開時間と throttling（GO の条件外。Content の容量上限へ）。`development/content/v1/` の bucket rule は、PUT がすべて成功したことから公開を妨げないことだけを確認した（rule の有無そのものは未確認）。
- 担当・モデル: 発見 C、差分レビュー、判定 C の offline 部分は Claude Opus 5.5（`claude-opus-5-5`）／Anthropic の新規 session。実 R2 往復は、その session が権限分類器に止められたため、統合担当の Claude Opus 5.5（同じモデル、owner の明示承認後）が実行した。いずれも Phase B（Sonnet 5.5）と異なるモデル。

## 8. Phase C'

**判定: FAIL（3 回目、`5aa9d74`）→ owner が阻害しないと判断（2026-10-11、チャット）。** 理由: 残る指摘は、予算の確認と receipt 確定（atomic rename）の間に残る窓で、確認してから確定する作りでは窓をゼロにできない。確定後に失敗を返すと AC9（receipt の rename だけが成功の確定点）と矛盾する。本番経路の窓はローカルの小さな receipt の書込みと rename の間だけである。この窓は残存リスクとして記録し、予算の対象区間の見直し（Content の容量上限）の入力とする。

**C′ 自身の結論:** 最低条件 5（例外承認後）の証拠は揃ったとしたうえで、予算の検査と receipt 確定の間に残る窓を AC13 違反とした。

- 担当方式: AI（新規 session、read-only sandbox、ネットワーク・資格情報なし）。
- 使用した blind audit bundle の取得案内・状態: 1 回目は `9c8e703`、2 回目は `5aa9d74` の bundle（Phase A snapshot、所見を含まない Phase B result、完全 diff、判定前の機械検査、判定必須の生結果、実 R2 往復の生結果、SHA256SUMS）。生成済み・未保存（publish は Phase D）。発見 C・判定 C の所見と結論は含めていない。
- 確認範囲・方法: SHA256SUMS の全件照合（1 回目 69 件、2 回目 80 件）、指示文書・snapshot・B result・diff・変更コード・機械検査・生結果の閲覧。テストの再実行はしていない。
- 1 回目（`9c8e703`）の阻害 findings と対応:
  - 予算（AC13）: publish は transfer 後に予算を外してから receipt を確定し、fetch も予算を外してから成功を返すので、期限切れでも成功しうる → B へ差し戻し（C′ 起因 1 巡目）、`5aa9d74` で修正。
  - 最低条件 5 の PUT 数を §5 の方法（operation の観測記録と intent の entry 数）で数え直せない → 再梱包で対応（operation directory の子 process 読戻し記録 442 件・221 key と intent を、endpoint を除いて収録）。
- 2 回目（`5aa9d74`）の阻害 findings:
  - 最低条件 5 と §5「GO 候補 head の判定必須」: 初回公開（PUT 221）は `9c8e703` で行ったもので、最終 head では fetch と `existing` の再実行しかしていない。review-evidence は、差分が変わった後の旧結果を新 head の合格証拠にしないと定めており、この例外は凍結されていない。
  - 最低条件 5 と §5「照合ログ」: 収録した operation の記録は key・期待値・子 process の終了状態だけで、各 object の照合結果（HTTP 結果、観測 bytes/hash）を持たないため、初回公開時の各 object の照合一致を記録から独立に再判定できない（実装は照合結果を operation に残さない）。
- 3 回目（`5aa9d74`、owner の例外承認と最終 head での `spring-full` の初回公開を収録した bundle）の阻害 findings:
  - AC13: 予算の検査は receipt 書込みの前に 1 回だけで、その後の `before-receipt` hook と `Write-BuildImmutable` の書込み・rename の間に期限を越えても成功を確定しうる（注入時計で再現可能な境界。今回の実行で発現したという所見ではない）。
  - 最低条件 5 は、最終 head での初回公開（PUT 220・GET 440・760 s、取得 219/219 一致、再実行 `existing`）で満たされたと判断した。
- 後続スライスへ移送する findings（3 回目）: 各 object の照合結果を operation に記録しない点（例外承認どおり件数保持の運用接続へ）、別 PC の同時 writer（Content 確定記録）。
- 後続スライスへ移送する findings（1・2 回目）: なし（残存リスクとして、別 PC の同時公開と R2 側の上書き防止は Content 確定記録、0 byte と 4 季節の容量は Content の容量上限、private operation の清掃は未完了 upload と staging）。
- 監査できなかった範囲: 実 R2 操作前の owner の許可（bundle に無い）。実 R2・資格情報・Unity。
- 独立性: Phase A（Anthropic 3 系列）、B（Sonnet 5.5）、C（Opus 5.5）のいずれにも関与していない系列・ベンダーで、強化条件も満たす。
- 発見 C / 判定 C 結論の事前閲覧・設計実装への関与: なし（bundle から除外。指示文で HANDOFF を読まないよう指定し、checkout の HANDOFF にも §7 は無い）。
- 担当・モデル: OpenAI GPT（codex CLI 0.159.2、`gpt-6-sol`）。1〜3 回目とも 1 回ずつ実行。

## 9. Phase D

未着手。`review-evidence.md` の「Phase D の保存・引渡し確認」に従う。

- C / C' の突合:
- マージ判断:
- 必要なEvidenceの保存・取得・必須内容の閲覧確認:
- 取得案内の引渡し確認:
- harvest:
- 削除確認:
- 通常のtask終了・後始末の状態と未完了事項:
