# Artifact Storage / Evidence のローカル固定 HANDOFF（R2 昇格は後続スライス）

## 0. メタデータ

- type: `slice`
- status: `A`（revision 3。追加レビュー ADD-01（§5.2）を受け、R2 への昇格を後続スライスへ移送し、ローカル固定と D6 のワークフロー規則に絞った。revision 2 の記録は §5.1・§5.2 に残す。凍結は owner の本文確認後。設計判断は凍結前）
- branch: `claude/evidence-local-stage`（PR の base は `develop`）
- implementation base commit: `6a51d2fefc9cf6d09d2a74a1ec1c42784e0e4524`（`origin/develop`、PR #127 マージ後）
- implementation head commit: 未固定
- risk: `high`（Evidence catalog の artifact state と cleanup の fail-closed 契約に触れる。加えて `osm-workflow` の Phase 境界・push・head 固定の常時規則を書き換える。revision 3 で network 経路と `put-pending` の回復には触れなくなった）
- owner: OSM maintainers
- created: 2026-10-16
- expires: 2026-11-16 または置換 revision
- harvest to: 操作と状態遷移を `tools/Artifacts/README.md`（Evidence の節と「エージェント操作の事前承認」）へ、Phase 規則の変更は `.agents/skills/osm-workflow/` の本文そのもの（本スライスの実装対象）へ。`BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §4 C（別host / Cloud）に「ローカル固定は完了。同じ artifact の R2 昇格と回復契約は後続スライス `Evidence 昇格と Cloud 配布` の入力（§5.2）」を追記する。
- Phase A snapshot の取得案内・状態: 未保存（本 HANDOFF の commit を凍結対象版とする。A3 で内容 sha256 を引継ぎ票に書く）
- Phase B result snapshot / evidence bundle / C' blind bundle の取得案内・状態: 未生成（未到達 Phase）
- 引継ぎ票: 次は **owner による revision 3 の確認と凍結**。入力は本 HANDOFF の §2〜§5（revision 3）。owner が確認したら、主担当が凍結 commit と内容 sha256 をここに書き、引継ぎ票を Phase B 向けに書き換える。push はしない（公開点は §2 D6-3）。引き渡さないもの: なし。
- （旧）引継ぎ票（revision 2。記録として残す）: 次は owner による revision 2 の確認と凍結。revision 2 は commit `eee62d78`（内容 sha256 `28a35753b88eec4b8b5980412bf48aba07fc27fc569e175f797a76680999b13f`）。追加レビュー ADD-01 を受けて凍結せず、revision 3 へ切り直した。
- （旧）引継ぎ票（A2 → A3。記録として残す）: 次の Phase は A3（統合と凍結、owner）。入力は §5.1 の A2 結果と主担当の採否案。owner が 6 点を裁定し、主担当が revision 2 を統合する。
- （旧）引継ぎ票（A1 → A2。記録として残す）: 次の Phase は A2（独立レビュー）。入力は本 HANDOFF の commit `f0a64288`（A1 初稿、内容 sha256 `52c94278500ab3188ec8f853fe5c9fb97c3d16b4df86278d8978c89660a19eb4`）。観点は §5 の A2 欄。

## 1. 目的と対象外

- 目的: 同一 PC で動く Agent 同士が、固定入力（差し戻し中の実装 snapshot、Phase A snapshot、判定 evidence bundle、C' blind bundle）を R2 も git push も使わずに内容 hash で固定して受け渡せるようにする。reference は従来形式のまま決定的に導き、後続スライスで同じ artifact を R2 へ昇格しても取得案内を書き直さなくてよい設計にする（昇格そのものは後続。§5.2）。これを前提に、`osm-workflow` の push・commit・head 固定の規則を「公開点だけ」に書き換える。
- 現況（2026-10-16 の実測）:
  - PR #126 の作業ブランチは 52 commit に対して約 36 回 push され、PR 作成はマージの 14 分前だった。PR #127 も PR ブランチへの push は 1 回で、残りは Phase 境界と HANDOFF 記録 commit ごとの push である。cursor[bot] 以外の担当（Claude デスクトップ、Codex、codex CLI、cursor-agent）は全て同じ PC で動いており、origin を経由する必要がない。
  - push を強制している規則は、`osm-workflow/SKILL.md` の「前の Phase から引き継ぐときは…リモートの head を照合」「B の session は…push して終える」「判定 evidence を取った head は…push して到達可能に保つ」と、`phases-and-handoff.md` の「Phase 境界の引継ぎ」「Cloud の担当」である。
  - `evidence publish`（[EvidenceApplication.psm1](../../tools/Artifacts/EvidenceApplication.psm1) の `Invoke-EvidencePublish`）は、staging を catalog に登録 → read lock で snapshot → `PackageIO.CreateVerified` で ZIP と manifest と packageSha256 を生成 → `PackageIO.Extract` でローカル再検証、まで network と資格情報を使わない。その後に初めて R2 へ PUT し、別 process の GET で照合して receipt を確定する。
  - selection の `base` / `head` は 40hex の形式検査だけで、git の commit とは照合しない（[EvidenceContract.psm1](../../tools/Artifacts/EvidenceContract.psm1) の `Assert-EvidenceReference`、[PackageIO.cs](../../tools/Artifacts/Packaging/PackageIO.cs) の manifest 照合）。未 commit の作業ツリーも file 集合として固定できる。
  - manifest には `createdAt` が入るため、同じ file 集合を再 snapshot すると packageSha256 は変わる。昇格で hash を保つには ZIP bytes の再利用が必要である。
  - artifact の state は `put-pending` / `ready` / `failed` / `delete-pending` / `deleted`。`Get-EvidenceRetentionDecision` は `ready` 以外を `unconfirmed-artifact` として削除対象にしない。
  - reference は `osm-evidence-v2:` の opaque 文字列で、R2 の `key` を含む。fetch は R2 からの GET を前提にしている。
  - Cloud から R2 の Evidence を取得する経路は未成立（program §4 C）。Cloud に渡せるのは PR 上のブランチだけで、その配布は push に依存する。
  - Cloud 環境には通常 Unity が無い（2026-10-16 owner）。Cloud が Phase B の修正ループに入ることは例外として扱う。
- 対象外: 同じ artifact を同じ bytes で R2 へ昇格する経路と、その PUT 応答不明・GET 404・再送・清掃の回復契約（後続スライス `Evidence 昇格と Cloud 配布`。§5.2）。Cloud・別 PC から R2 を fetch する経路（program §4 C）。`ready` artifact のローカル copy からの network 無し fetch。Harness の `external-current-v1` への全スライス移行。既存履歴の rebase。git 管理外のローカル運用メモ（手元層）の更新は owner が行う。Unity 側の変更なし。

## 2. 意思決定と受け入れ境界（revision 3。昇格を後続スライスへ移送し、ローカル固定と D6 のワークフロー規則に絞った。owner の凍結確認待ち）

- このスライスが答える問い: 「同一 PC の Agent 間の固定入力を、R2 と git push を使わずに内容 hash で固定して受け渡し、それを前提に push と commit の規則を公開点だけに絞っても、C / C' の head 固定と盲検の契約を保てるか」。
- A0 で owner が固定した前提: ツール変更とワークフロー規則変更は同じスライスで扱う（2026-10-16）。R2 への昇格は、Cloud から R2 を取得する経路（program §4 C）が未成立で今は消費者が居ないため、追加レビュー ADD-01（§5.2）を受けて後続スライスへ移送した（2026-10-16 owner）。
- 方式:
  - **D1 `evidence stage`（ローカル固定）。** `pwsh tools/artifacts.ps1 evidence stage --profile osm --config <cfg> --selection <sel>` は、既存 publish の「staging 登録 → read lock で snapshot → `PackageIO.CreateVerified` → `PackageIO.Extract` 再検証」までを実行し、R2 へ PUT しない。artifact を新 state **`staged`** で catalog に記録する。`key` は `Get-EvidenceKey` が導く決定的な値を未送信のまま持つ（`$null` にしない。`Read-EvidenceTask` の key 照合を通すため）。`receipt=$null`、`putIntent=$null`、`origin='stage'`（任意 field。無い artifact は `publish` と読む）。`packageSha256` / `manifestSha256` / `packageBytes` / `base` / `head` / `entries` は publish と同じ値。ZIP は staging operation 内に残し `ownedCopies` に登録する。出力は従来形式の `reference`（`osm-evidence-v2:`）と `packageSha256`。network・資格情報・deployment の疎通を要しない。task guard は従来どおり通すが、preflight は **network を使わない形**（`Assert-EvidenceTransitionSafe -NoNetwork`）で呼び、未解決の `put-pending` / `deleteIntent` が task に残っていれば解決を試みず `unresolved-put` / `unresolved-delete` で拒否する。
  - **D2 reference は従来形式のまま。** 新しい prefix や codec を作らない。`key` は artifactId から決定的なので、`staged` が返す `osm-evidence-v2:` の文字列は、後続スライスで同じ artifact を R2 へ昇格しても変わらない。`inspect` は `staged` にも reference と packageSha256 を返し、`locality`（`local` / `promoted`）を出力する。`EvidenceContract.psm1` は変更しない。
  - **D3 fetch の local 分岐。** fetch は reference から catalog の artifact を解決し、state が `staged` なら staging operation 内の ZIP を新しい private operation へ copy し、R2 からの fetch と同じ `PackageIO.Extract` で期待 hash・manifest・全 entry を検証して `outputPath` を返す。network を一行も通らず、preflight は D1 と同じ network 無しの形。state が `ready` なら従来どおり R2 から取得する。ZIP の欠落は既存 preflight の `changed-copy`、hash・identity の不一致は `identity-mismatch`、別 task の reference は `catalog-missing` で拒否し、R2 へ fallback しない。取得した copy は従来どおり staging として登録する。
  - **D4 保持・保護・清掃。** `staged` は `ready` と同じ task 終了 + 30 日の保持規則に従う（`Get-EvidenceRetentionDecision` が `staged` を `ready` と同じ経路で判定する）。`use` / `release` は `staged` も対象にする。満了した `staged` の削除は、ローカルの ZIP と `ownedCopies` の除去と state `deleted` だけを行い、R2 への DELETE・GET を発行せず、`deleteIntent` も使わず、途中で止まっても同じ手順の再実行で完了する。`Read-EvidenceTask` は `origin='stage'` の `deleted` に receipt を要求しない。staging の 7 日清掃は `staged` artifact が参照する staging を `operation-pending` 相当として除外する（既存の「`put-pending` を参照する staging は除外」の述語に `staged` を加える）。dry-run と読取経路は `staged` の field を変えない（`Sync` による generation の増加は従来どおり）。本スライスでは `staged` が `put-pending` に遷移する経路を作らないので、`origin='stage'` かつ putIntent ありの artifact は存在しない。見つかれば `unknown-state` で削除しない。
  - **D5 publish の既存経路は不変。** `--selection` を渡す publish は従来どおり snapshot から PUT まで一操作で行い、`origin='publish'`。`staged` を source にした publish は、既存の source 候補ループどおり再 snapshot して**別の artifact**を作る（hash と reference は変わる）。昇格経路（同じ artifact を同じ bytes で R2 へ）は後続スライスの所有で、本スライスは `put-pending` の回復・再送・清掃に触れない。
  - **D6 ワークフロー規則（`osm-workflow`）。** 以下を本スライスの実装対象として書き換える。
    1. **引継ぎの照合（`SKILL.md` 作業開始時 5、`phases-and-handoff.md` 「Phase 境界の引継ぎ」）。** 引継ぎ票に commit SHA と、stage を使う場合は取得案内の所在を書く（reference と packageSha256 の実値は取得案内に置き、tracked HANDOFF と PR 本文には転記しない）。受け手は自分の worktree の HEAD を SHA と照合し、作業ツリーが dirty なら引き継がない。同一 PC ではローカルの SHA で照合し、origin との照合は Cloud・別 host の担当が割り当てられている時だけ。レビュー用 worktree は固定 SHA からの detached worktree でよく、同じブランチを複数 worktree で checkout する必要はない。
    2. **B の出口（`SKILL.md` Phase B、`phases-and-handoff.md` 「実行担当と判断担当」）。** 「push して終える」「引継ぎ票と push」を「commit し、引継ぎ票に SHA と取得案内の所在を書いて終える。push は公開点の規則に従う」に変える。
    3. **公開点（`SKILL.md` 「PR公開後の差し戻しとGit履歴」）。** 原則「push は配布であり保存ではない。リモートにしか居ない読者が居る時だけ push する」。公開点は (a) PR 作成、(b) レビュー依頼（`@cursoragent` のコメントを含む）と、レビュー 1 巡ごとの修正 1 commit、(c) マージ前の最終（harvest と HANDOFF 削除）。例外は Cloud・別 host の担当がいる Phase 境界と、人間の明示指示。順序を「B ↔ 発見 C → 未公開の試行錯誤 commit の整理 → GO 候補 implementation head の固定 → 判定 C → C' → PR 作成・push」と明記し、履歴整理は判定 C の前に済ませる。「判定 evidence を取った head は push して到達可能に保つ」は「push しないローカルの軽量 tag `evidence/<taskId>/<n>` で到達可能に保ち、対象 SHA を引継ぎ票に書く」に置き換える。tag は旧 head の到達可能性だけを担い、整理後の別 head に旧判定を移す根拠にならない。push 前の点検に `pwsh tools/docs-audit.ps1` と機微 path の確認を加える。
    4. **固定の対象（`review-evidence.md` 「対象を固定する」、`SKILL.md` Phase C 入口、`phases-and-handoff.md` 「Phase の責任」の 3 箇所に同じ文言）。** 「発見 C の差し戻し入力に限り、完全な変更集合を収録した stage snapshot（reference と packageSha256）で固定できる。判定 C と C' は commit 固定を必須とし、C / C' 用 bundle は同一 commit から生成する。snapshot を使うレビューでは、送信元の可変な作業ツリーをレビュー入力にしない」。stage snapshot の収録規則: base commit の SHA、追跡済み・未追跡・削除・バイナリを含む完全な変更集合を表す `changes.patch`、変更後の全 file。review-record commit の規則は不変。
    5. **保存先（`review-evidence.md` 「保存先とリモートへの配布」）。** 対応済み環境の同一 PC 内の既定を `evidence stage` とする。Cloud・別 host の読み手に渡す必要がある時は、昇格経路が後続スライスで成立するまで従来どおり、従来の publish（新しい artifact）で R2 へ置くか、PR ブランチで渡す。取得案内は reference、期待 packageSha256、対象 base / head、用途、所在 task、保持条件、引渡し先、`locality` を持ち、公開文書には所在だけを書く。Phase D の取得・閲覧確認は local でも同じ。
    6. **PR 状態ブロックと Cloud（`phases-and-handoff.md`）。** 状態ブロックの更新義務は「PR が存在する間」に限り、公開前は HANDOFF §0 を唯一の正本とする。「Cloud の担当」節: Cloud は凍結入力を読む役（A2・C'）を既定とし、B の修正ループは例外。Cloud の担当が割り当てられた Phase 境界では PR 作成・状態ブロック更新・対象 SHA の push を必須手順とする。**PR の有無にかかわらず、必要な固定 Evidence を取得・検証・閲覧できない Cloud 作業は assist** であり、PR 作成や push だけで正規の発見 C・判定 C・C' に昇格させない（現行の能力条件を残す）。
    7. **テンプレート（`handoff-template.md` §0）。** 取得案内の状態欄に `ローカル固定済み（未昇格）` を加え、引継ぎ票の項目に「commit SHA、stage を使う場合は取得案内の所在」を加える。
  - **D7 事前承認と導入。** `tools/Artifacts/README.md` の「エージェント操作の事前承認」に `evidence stage` と fetch の local 分岐を通常操作として加える（どちらも network・資格情報を使わない）。導入手順は §4。
- 却下した代替案:
  - 同一 PC でも R2 を経由する現状維持: 往復ごとに network と承認が要り、owner が不要と判断（2026-10-16）。
  - Storage を使わず patch を共有 folder に置く: catalog・保持・清掃・盲検分離の既存契約から外れる。
  - `staged` を staging の 7 日清掃に乗せる: active な task の最中に唯一の複製が消える（A2 (a)(b)(d) 一致で却下）。
  - local 専用の reference prefix（A1 初稿の D2）: key が決定的なので不要。codec の追加と `deploymentId` の論点を生むだけ（A2 #5 で (d) 案に置換）。
  - `staged` を state にせず別の local-stage record にする（A2 (a) 代替案）: 保持・保護・削除の所有権が二重になる。読戻し検査を直せば state 追加で足りる。
  - 同じスライスで昇格（同じ artifact を同じ bytes で R2 へ）まで実装する（revision 2 の D4 / D4'）: PUT 応答不明後の GET 404 は先行 PUT の失敗確定ではなく、同 key 再送を禁じる既存の publish 契約と衝突する。消費者となる Cloud 取得経路も未成立。後続スライスへ移送（§5.2）。
  - `ready` artifact のローカル copy からの fetch: 価値はあるが本スライスの問いの外。後続への入力にする。
- 進める最低条件:
  - 不変条件（起きてはいけないこと）:
    - **I1** `evidence stage` と fetch の local 分岐は、network・資格情報・deployment の疎通を一切行わない。preflight も network を使わず、未解決の put / delete があれば解決を試みず拒否する（偽 transport と資格情報読取りの記録が 0 件）。
    - **I2** fetch の local 分岐は identity 不一致・ZIP 欠落・hash 不一致で拒否し、R2 へ fallback しない。
    - **I3** `staged` の清掃は task 終了 + 30 日の前に起きず、削除時に R2 へ DELETE・GET を発行しない。`staged` が参照する staging を 7 日清掃で消さない。
    - **I4** 既存の `--selection` publish、remote fetch、`ready` / `put-pending` / `failed` / `delete-pending` / `deleted` の cleanup・保持判定・回復・reasonCode の意味を変えない（既存 suite が無変更で通る。`origin` 無しの既存 catalog を読める）。
    - **I5** `staged` を `put-pending` へ遷移させる経路を作らない。同 key への PUT は本スライスで一度も発生しない。
    - **I6** D6 の規則変更は、判定 C と C' の GO 候補が commit で固定されること、C / C' 用 bundle が同一 commit から生成されること、C' が所見を含まない入力だけを受けること、公開済み履歴を書き換えないことを変えない。3 文書の固定規則は同じ意味で書かれる。
    - **I7** `staged` に付けた `use` 保護は保護中は満了でも削除されない。tracked HANDOFF と PR 本文に reference・packageSha256 の実値を転記しない。
  - 成立条件（起きなければならないこと）:
    - **S1** 別 session が取得案内の reference と期待 packageSha256 だけから local fetch し、全 entry 検証済みの copy で base SHA・`changes.patch`・変更後 file を閲覧できる。
    - **S2** 満了した未送信 `staged` が、network を使わずに `deleted` へ進み、途中停止後の再実行でも完了し、その後も catalog を読める。
    - **S3** `osm-workflow` の本文が D6 の 7 項目を含み、固定規則が 3 文書で同じ意味になり、`docs-audit`・`contract-audit` が errors=0 で、旧文言が残らない。
- 受け入れ条件（検証例。I/S との紐付けを併記。offline fixture は隔離した temp store と偽 transport、実ストア・network 不使用。D6 は文書検査。実 R2 は使わない）:
  - **AC1**（S1, I1）: fixture の作業ツリーから `evidence stage` → 別 process で fetch → 全 entry の SHA-256 が元 file と一致し、transport の呼び出しと資格情報の読取りが 0 件。
  - **AC2**（I2）: catalog の `packageSha256` を書き換えた状態は `identity-mismatch`、ZIP を削除した状態は `changed-copy`、別 task の reference は `catalog-missing` で fetch が拒否され、transport 呼び出し 0 件。
  - **AC3**（I1）: `put-pending` を持つ task と `deleteIntent` を持つ task で stage と local fetch を呼び、`unresolved-put` / `unresolved-delete` で拒否され、transport と資格情報の読取りが 0 件。
  - **AC4**（I3, S2）: 時刻注入で task 終了 + 30 日の前日と当日を比較し、未送信の `staged` は前日 `retained`、当日 `expired` → 削除で ZIP と ownedCopies が消え、DELETE・GET 0 件、state `deleted`、その後も `Read-EvidenceTask` が通る。`staged` が参照する staging は 7 日経過後も `operation-pending` 相当で残る。dry-run は `staged` の field を変えない。
  - **AC5**（S2）: `staged` の満了削除を 1 file 削除後・catalog 更新前で中断してから再実行し、DELETE・GET 0 件で `deleted` になり、その後も `Read-EvidenceTask` が通る。正常な `staged` の ZIP 欠落は従来どおり `changed-copy` で拒否されたまま（削除途中の復旧で欠損拒否を一律に緩めない）。
  - **AC6**（I4）: 既存の `ArtifactEvidence` / `EvidenceCleanup` / `EvidenceRetention` suite が無変更で failed 0。`origin` 無しの既存 catalog を読める。`origin='publish'` の `put-pending` の回復は従来どおり。
  - **AC7**（I5）: `staged` を source にした publish は別 artifactId の `origin='publish'` を作り、元の `staged` は不変。`staged` 自身への PUT 経路が無いことを fixture の transport 記録で確認する。
  - **AC8**（S3, I6）: D6 の 7 項目が本文に入り、grep で旧文言（「リモートの head を照合」「push して終える」「引継ぎ票と push」「push して到達可能に保つ」）が `.agents/skills/osm-workflow/` に残らない。固定規則の文言が 3 文書で一致し、判定 C・C' の commit 固定、同一 commit からの bundle 生成、C' の盲検、公開済み履歴の非書き換えの文言が残る。`docs-audit`・`contract-audit` errors=0。
  - **AC9**（D7）: README の事前承認節と Evidence の節に stage・fetch の local 分岐の操作、state `staged`、`locality`、保持・保護規則、導入手順、「昇格は後続」が書かれ、旧記述と矛盾しない。
  - **AC10**（I7）: `staged` に `use` → 保護付きで満了時刻を過ぎても `in-use`。`release` 後に `expired`。HANDOFF テンプレートと D6-1/5/7 の文言が実値の転記を禁じている。
  - **AC11**（S1）: 追加・変更・削除・未追跡・バイナリを含む fixture で stage し、base SHA に `changes.patch` を適用した結果の file 集合が、変更対象・削除対象・未変更対象を区別して収録 file 集合と一致する。収録し忘れを fixture で検出できる。
- ここでは答えない問いと所有する後続スライス:
  - 同じ artifact を同じ bytes で R2 へ昇格する経路と、PUT 応答不明・GET 404・再送・清掃の回復契約 → 後続スライス `Artifact Storage / Evidence 昇格と Cloud 配布`（program §4 C と組で A1 から行う。revision 2 の D4 / D4' / I8 / S4 / AC3・4・7・11・12 と §5.2 の ADD-01 を入力にする）。
  - Cloud・別 PC が R2 から fetch する経路と token の受け渡し → program §4 C。
  - `ready` artifact のローカル copy からの network 無し fetch → program §4 A の後続への入力。
  - 端末喪失に備えた未昇格 `staged` の backup → Harness H3。
  - 全スライスの Phase 記録を Git 外 CURRENT へ移す（HANDOFF を untracked にする） → 本スライスの結果を見て別 program で判断。
  - git 管理外のローカル運用メモ（手元層）の更新 → owner。
- 判定定義: GO = I1〜I7 を破る欠陥がなく、S1〜S3 が offline fixture と文書検査で成立。NO-GO = いずれかの不変条件の違反、または S の不成立。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし。
- 決定記録: なし。
- 本文へ転記した実装制約: `#nullable` と `record` の規則は Unity 側のみで本スライスには当たらない。PowerShell は `Set-StrictMode -Version Latest`。reasonCode は既存の小文字 kebab。path を CLI 出力に出さない。テストで `Start-Sleep` を使わず時刻を注入する。catalog の新 field `origin` は任意とし、無い artifact は従来どおり読む。transport（`Transport/R2ArtifactTransport.*`）は変更しない。
- 未決事項: なし。

## 3. 責務マップ

| ファイル | 責務 | 変更理由 | 現在行数 → 予想 | 備考 |
|---|---|---|---|---|
| `tools/Artifacts/EvidenceApplication.psm1` | publish / fetch / inspect / use の orchestration | stage（既存 publish の準備部を private 関数に抽出して共用）、fetch の local 分岐、inspect の `locality`、use の `staged` 受付 | 141 → 約 185（+30%） | 送信部は動かさない。50% 未満 |
| `tools/Artifacts/EvidenceStateStore.psm1` | catalog の読戻し検査 | 許可 state に `staged`、任意 field `origin`、`origin='stage'` の `deleted` の receipt 条件 | 136 → 約 144 | schema の変更はこの 3 点に限る |
| `tools/Artifacts/EvidenceRetentionPolicy.psm1` | 純粋な保持 policy | `staged` を `ready` と同じ経路で判定。staging 除外述語に `staged` 参照を加える | 60 → 約 68 | I/O なし。単体テスト可能 |
| `tools/Artifacts/EvidenceCleanup.psm1` | 経路検証と清掃 | `-NoNetwork` preflight、`staged` の満了削除（ローカルのみ、再実行可） | 335 → 約 360 | 500 行未満。`Resolve-EvidencePutGuarded` は変更しない |
| `tools/Artifacts/EvidenceContract.psm1` | 契約 | 変更なし | 89 | reference 形式を維持 |
| `tools/artifacts.ps1` | CLI の引数解釈 | `evidence stage`、inspect の出力 | 175 → 約 183 | |
| `tools/Artifacts/README.md` | 公開仕様 | stage・fetch の local 分岐・`staged`・`locality`・保持・導入手順・事前承認・「昇格は後続」 | 379 → 約 400 | |
| `.agents/skills/osm-workflow/SKILL.md` | Phase 規則 | D6-1〜4 | 105 → 約 114 | |
| `.agents/skills/osm-workflow/references/review-evidence.md` | レビュー証拠 | D6-4〜5 | 168 → 約 182 | |
| `.agents/skills/osm-workflow/references/phases-and-handoff.md` | Phase 境界 | D6-1、2、4、6 | 187 → 約 198 | |
| `.agents/skills/osm-workflow/references/handoff-template.md` | テンプレート | D6-7 | 146 → 約 149 | |
| `tools/Artifacts/tests/ArtifactEvidence.Tests.ps1`、`EvidenceRetention.Tests.ps1`、`EvidenceCleanup.Tests.ps1`、`EvidenceTestSupport.ps1` | offline 検証 | AC1〜AC7、AC10、AC11。fixture 支援: 偽 transport の呼び出し記録、資格情報読取りの記録、隔離 store の ZIP 欠落・改変注入、別 process の fetch、削除途中停止の注入 | 追加ケース | 既存の偽 transport と時刻注入を使う |

状態の所有者は task の catalog（task guard が直列化）。新しい state は `staged` の 1 つ、新しい任意 field は `origin` の 1 つ。寿命は `ready` と同じ task 終了 + 30 日。新しい公開面は CLI の `evidence stage` と inspect の `locality`。新しいファイル・asmdef・依存 edge は作らない。transport・Unity 側の変更なし。

## 4. 実装計画

- 変更対象: §3 のファイル。
- 順序: (1) `EvidenceStateStore` の `staged`・`origin`・receipt 条件（既存 catalog の読戻し不変を AC6 で固定）→ (2) `EvidenceRetentionPolicy` の `staged`（純粋）→ (3) `EvidenceCleanup` の `-NoNetwork` preflight → (4) `EvidenceApplication` の準備部抽出と `stage`（既存 publish の挙動不変を AC6 で固定）→ (5) fetch の local 分岐と inspect の `locality` → (6) `staged` の満了削除と use / release → (7) CLI → (8) README（導入手順を含む）→ (9) `osm-workflow` の D6（3 文書同義）→ (10) `docs-audit`・`contract-audit`・AC8 の grep。
- 導入手順（README に書く。B は手順を書くだけで実行しない）: 共有 store へ `staged` を書き始める前に、`schedule-status` で登録済み runtime の head を確認し、新しい implementation head で `schedule-install` を行う。旧 runtime の日次 job は `staged` を含む catalog を `corrupt-state` で読めず止まるため、切替前に `staged` を作らない。`EvidenceSchedule` の収集機構は変えない（tracked な psm1 は既存の `git ls-files` 収集で拾う）。
- Phase B から Phase A へ差し戻す条件: §3 に書いた 3 点を超える catalog schema の変更、`EvidenceSchedule`・transport・`Resolve-EvidencePutGuarded` の変更、`PackageIO` の manifest 形式の変更、Workflow の終了意味の変更が必要になったとき。D6 の文言が凍結済みの他規則（C' の盲検、head 固定）と矛盾し、本文の読み替えで解消できないとき。
- 対象外を維持する方法: stage と fetch の local 分岐の経路に `Invoke-ArtifactNetwork` と `Get-CredentialStatus` の呼び出しを置かない（AC1・AC2・AC3 の記録 0 件で確認）。`staged` から `put-pending` への遷移を書くコードを置かない（AC7）。Cloud からの fetch 経路と昇格経路は作らない。

## 5. テストとレビュー計画

- 単体テスト: `staged` の保持判定と staging 除外述語（`EvidenceRetention.Tests.ps1`）、`Read-EvidenceTask` の `staged` と receipt 条件（`ArtifactEvidence.Tests.ps1`）。
- 差し戻し中の起点 `-Filter`: `tools/Artifacts/tests/ArtifactEvidence.Tests.ps1`、`EvidenceRetention.Tests.ps1`、`EvidenceCleanup.Tests.ps1 -Case <新規ケース>`。C は凍結済み条件の確認に必要なら根拠を記録して変えてよい。
- 判定必須テスト（GO 候補 head）: `tools/Artifacts/tests/*.Tests.ps1` と `tools/Workflow/tests/*.Tests.ps1` の全体（`StoreContext.Tests.ps1` は非仮想化 host で）、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC8 の旧文言 grep。`EvidenceCleanup.Tests.ps1` は `rg` が PATH に要る。実 R2 の往復は本スライスでは不要（network 経路を変えない）。
- 全 EditMode 回帰の適用除外: Unity 側コードを変更しないため。代替証拠は上記 offline suite 全体と audit。
- 統合・Unity テスト: なし。
- 操作・実行時・目視条件の検証経路: なし（全条件を offline fixture と文書検査で観測できる）。
- 未知の操作経路の疎通結果: なし。
- 人間の判断が必要な条件: なし（A3 の裁定は §5.1・§5.2）。
- 高コスト証拠: なし。判定 C の照合 script: なし。
- 機械検査: `contract-audit`、`docs-audit`、`git diff --stat`、AC8 の grep。
- A0/A1 主担当・モデル・ベンダー: Claude（この session）。
- A2 独立レビュー: 実施済み。担当・モデル・入力版・結果は §5.1。revision 2 に対する追加レビューと切り直しの経緯は §5.2。
- A3 統合担当・モデル・採否: 主担当（Claude）と owner。2026-10-16 に owner が §5.1 の 6 点を主担当案で裁定して revision 2 を統合し、同日、追加レビュー ADD-01 を受けて昇格を後続へ移送した revision 3 を切り直した。凍結は owner の確認後（凍結 commit と内容 sha256 は §0 引継ぎ票）。
- C' 用に予約した担当・モデル・ベンダー: Grok 系（cursor-agent）。A2 では未使用。
- 独立性の強化条件を満たせない場合の理由: A2 の (b)(d) は A1 主担当と同じ Claude 系。初稿への anchoring は (d) に初稿を読ませないことで避けた。(e)(f) は所見込みの版を読んだ追加レビューで、独立レビューに数えない。

## 5.1 A2 独立レビュー結果と A3 の採否案（revision 2 の記録）

本節は A1 初稿 → revision 2 の経緯の記録で、現在有効な契約は §2〜§5（revision 3）である。revision 2 で採用した #1〜#23 のうち、昇格に関わる #2・#4・#10・#18・#23 と A3 裁定 1・3・6 は、§5.2 の切り直しで後続スライス `Evidence 昇格と Cloud 配布` の入力へ移送した。それ以外は revision 3 にそのまま残っている。

入力版は A1 初稿（内容 sha256 `52c94278500ab3188ec8f853fe5c9fb97c3d16b4df86278d8978c89660a19eb4`、commit `f0a64288`）。4 件は互いの指摘を見ずに実施した。(d) は §1 だけを抜き出した入力から立案し、初稿本文を読んでいない。全件このPC内の読取り専用 session で、push・ストア・資格情報は使っていない。原記録は git 管理外の `TestResults/a2-local-stage/`（prompt と result）。担当・モデルは使用実績で、固定割り当てではない。

| 担当 | モデル | 判定 |
|---|---|---|
| (a) アーキテクチャゲート | Codex CLI、GPT 系（OpenAI） | NO-GO（§3 に `EvidenceStateStore.psm1` が無く、`staged` の読戻し・保持・昇格が既存契約と噛み合わない） |
| (b) fail-closed の破り方 | Claude Opus 系 | 条件付き GO（I1・I4 を破る順序を構成。D1/D4/D5 の追記と StateStore 追加で塞げる） |
| (c) D7 ワークフロー規則の整合 | Codex CLI、GPT 系（別 session） | NO-GO（差し戻し中の snapshot 固定と worktree 間の HEAD 照合が既存契約と衝突。D7 の分割は不要） |
| (d) A0 のみからの代替構成 | Claude Sonnet 系 | 判定なし。「準備／送信の分離、ZIP bytes 再利用、task 終了 + 30 日の保持」で初稿と収束。reference は不変にできると指摘 |
| (e) 採用案を反映しても残る問題（追加。入力は §5.1 込みの commit `68865dea`。(a)〜(d) の所見を読んだうえでのレビューで、独立レビューとは数えない） | Web 版 GPT（OpenAI、owner が手動で依頼） | 方向性に賛成、このままの A3 凍結は非推奨（P1 3 件: `use` が `staged` を拒否、昇格失敗後の回復が閉じない、snapshot 例外の反映先不足） |

主担当が blocker 候補の根拠を実コードで確認した事実: `Read-EvidenceTask` は artifact の `key` を `Get-EvidenceKey` と一致させ、state を既存 5 値に限り、`deleted` / `delete-pending` に receipt を要求する。`Assert-EvidenceTransitionSafe` は `put-pending` / `deleteIntent` を `Resolve-EvidencePutGuarded` / `Resolve-EvidenceDeleteGuarded` で解決し、そこで `Get-CredentialStatus` と GET が走る。cleanup の staging 除外は `put-pending` と `deleteIntent` だけで `failed` を含まない。publish の read lock は `CreateVerified` の後に PUT 直前で開く。

### findings と主担当の採否案

| # | 指摘（担当） | 分類 | 提案する反映 |
|---|---|---|---|
| 1 | D1 の `key=$null` と state `staged` は `Read-EvidenceTask` で `corrupt-state`。StateStore が §3 に無い（a-1、b-I4b） | 採用 | D1: `key` は `Get-EvidenceKey` で導く予約値を常に持たせる（未送信でも値は決定的）。StateStore の許可 state に `staged` を加える。§3 に `EvidenceStateStore.psm1`（136 → 約 145）を追加。§4 の差し戻し条件を「本 revision で許可した state 追加と receipt 条件の変更を超える schema 変更」に直す |
| 2 | 昇格の回復契約: 昇格由来の `put-pending` が `failed` になると staging が 7 日で消え、task 全体が `changed-copy` で止まる（a-2、b-I4a） | 採用 | D4: putIntent と `operation` を state 変更より前に書く。昇格由来の `put-pending` は GET missing で `failed` にせず `staged` に戻す。staging 除外述語に `staged` 参照を加える（D5 に既記載、`failed` 由来の経路は昇格で発生させない）。AC11 を追加 |
| 3 | stage / local fetch が通る preflight が未解決 put / delete を network で解決し I1 を破る（b-I1） | 採用 | D1・D3: network 無しの preflight を使い、未解決 put / delete があれば `unresolved-put` / `unresolved-delete` で拒否する。AC10 を追加 |
| 4 | `staged` の満了削除で receipt 無しの `deleted` を書くと `receipt-missing`（b-I4b） | 採用（方式は A3） | D5: 案 (i) `putIntent` が無い（未送信）artifact の `deleted` は receipt 不要とする StateStore の分岐。案 (ii) 別の終端 state を足す。主担当案は (i)。AC12 を追加 |
| 5 | reference: `key` は artifactId から決定的なので、昇格前後で `osm-evidence-v2:` 文字列そのものを不変にでき、local prefix と codec 追加は不要（d）。a-6 は逆に local codec の設計不足を指摘 | 採用（d 案で置換） | D2 を「新 prefix を作らない。fetch は catalog の state で local / remote を選ぶ。`inspect` に `locality` を出す。未昇格 reference の remote fetch は `not-promoted`」に置換。`EvidenceContract.psm1` は変更なしになり、I6 の「`--artifact` だけでは taskId が決まらない」（b-I6）も reference 入力で解消。昇格は `--reference` と `--sha256` を受ける |
| 6 | 差し戻し中の snapshot 固定は「可変な作業ツリーを正本にしない」と衝突。worktree 間で HEAD・index が別で、「ブランチの head」照合は dirty を見逃す（c-1、c-2） | 採用 | D7-1: 引継ぎ票に commit SHA と、stage を使う場合は packageSha256 を書く。受け手は自分の worktree の HEAD を SHA と照合し、dirty なら引き継がない。D7-4: 発見 C の入力は stage snapshot（base commit + `changes.patch` + 変更後 file）だけで、作業ツリーを読まない。判定 C / C' は commit 固定。C / C' 用 bundle は同一 commit から生成、を明記 |
| 7 | Cloud が早期に割り当てられた場合、公開点まで固定入力を読めない（c-3） | 採用 | D7-6: Cloud 引継ぎでは PR 作成・状態ブロック更新・対象 SHA の push を Phase 境界の必須手順にし、PR 前の Cloud 作業は assist に限る |
| 8 | local の取得案内に必須情報と引渡し確認条件が無い（c-4） | 採用 | D7-5・7: 取得案内に reference、期待 packageSha256、base / head、用途、所在 task、保持条件、引渡し先、`locality` を要求。Phase D の取得・閲覧確認は local でも同じ |
| 9 | 公開点にレビュー依頼（`@cursoragent` のコメント）を含める（d）。B 出口の「引継ぎ票と push」など残る参照の統一、evidence tag の命名・記録（c-5） | 採用 | D7-3: 公開点 (a) PR 作成、(b) レビュー依頼と 1 巡ごとの修正 1 commit、(c) マージ前の最終。tag は `evidence/<taskId>/<n>` で、対象 SHA を引継ぎ票に書く。AC8 の grep 対象に「引継ぎ票と push」を加える |
| 10 | 照合と PUT の間の TOCTOU（b-I2） | 採用 | D4: hash 照合は PUT と同じ read lock の handle で行う |
| 11 | AC2 の reasonCode は `changed-copy` が先に出る。AC5 の「catalog を変えない」は Sync で generation が増える（b-I3、b-I5） | 採用 | AC2: ZIP 欠落は `changed-copy`。AC5: 「`staged` の field が不変」に書き直す |
| 12 | fixture 支援の記載不足: 別 process、ZIP 欠落 / 改変注入、送信 bytes の記録（a-5） | 採用 | §5 単体テスト欄に、偽 transport の送信 bytes 記録と隔離 store の破損注入を明記 |
| 13 | `EvidenceApplication.psm1` の見積り過少、3 責務化の懸念（a-4） | 採用（判断は A3） | 141 → 約 230（+60%）に改める。送信部（PUT・GET・receipt・昇格）を `EvidencePromotion.psm1` に分けるか、同 file に留めるかを A3 で決める。主担当案は分ける |
| 14 | `staged` を state にせず別の local-stage record にする（a 代替案） | 不採用 | (b)(d) は state 追加で既存の保持・保護・削除を流用でき、record 分離は所有権と保持の二重定義を生む。#1 で読戻し検査を直せば state 追加で足りる |
| 15 | 保持は task 終了 + 30 日（a・b・d 一致）。7 日では active な task の最中に唯一の複製が消える | 採用 | §2 未決事項 (1) を解消。D5 のまま |
| 16 | D7 の分割は不要（c）。公開点と固定入力・引継ぎの契約が相互依存 | 採用 | §2 未決事項 (3) を解消。同じスライスで扱う |

(e) の指摘と採否案。主担当が実コードで確認した事実: `Invoke-EvidenceUse` は state が `ready` でない artifact を `payload-deleted` で拒否する。`Resolve-EvidencePutGuarded` は GET missing で state と operation を変えるが `putIntent` を消さない。putIntent は `intents/<artifactId>.json` の固定 path に `Write-EvidenceAtomic -Immutable`（`File.Move` の overwrite 無し）で書くため、同じ artifactId の再送信で intent を作り直すと衝突する。`SKILL.md` の Phase C 入口は implementation base / head commit の固定を要求している。

| # | 指摘（担当） | 分類 | 提案する反映 |
|---|---|---|---|
| 17 | `staged` の copy を `use` で保護できず、README の「use 失敗時は利用を開始しない」契約により取得後の通常手順が止まる（e-1、P1） | 採用 | D5: `use` / `release` の対象に `staged` を加え、保護・期限切れ・保持判定の接続を変えない。昇格しても保護が失われないことを AC13 として追加 |
| 18 | 昇格失敗から `staged` へ戻した後の再昇格と清掃が閉じない。putIntent が残るため #4 (i) の「putIntent 無し」条件に入らず、再昇格は不変 intent と衝突する。一括 publish 由来と昇格由来の `put-pending` の判別も未固定（e-2、P1） | 採用 | D4: 昇格は既存の putIntent（同じ key・packageSha256・artifactId）が在れば再利用し、作り直さない。回復で `staged` へ戻す際は putIntent を保持する。#4 の削除条件を「putIntent の有無」ではなく「receipt 無し、かつ GET 404 の確認記録または未送信」に改める。昇格由来か publish 由来かは artifact に `origin`（`publish` / `stage`）を持たせて判別する。受け入れ条件を「昇格失敗 → 不在確認 → staged へ回復 → 同じ bytes で再昇格成功」と「→ 保持満了後にローカル清掃成功」の 2 終点まで伸ばす（AC11・AC12 を改訂） |
| 19 | snapshot 固定の例外を `review-evidence.md` だけで書くと、`SKILL.md` Phase C 入口と `phases-and-handoff.md` の Phase 責任（commit 固定）と矛盾が残る。`git diff` + 変更後 file では未追跡・削除・バイナリの扱いが曖昧で、AC1 は収録し忘れを検出できない（e-3、P1） | 採用 | D7-4 の反映先に `SKILL.md` Phase C 入口と `phases-and-handoff.md` Phase の責任を加え、3 文書で同じ文言にする。stage snapshot の収録規則を「追跡済み・未追跡・削除・バイナリを含む完全な変更集合」と定義し、`changes.patch` と収録 file 集合が同じ実装を表すことを追加・変更・削除を含む fixture で確認する AC14 を追加。AC8 の確認対象に入力区分の無矛盾を加える |
| 20 | 未公開 commit の整理時点を「公開前」に置くと、判定 C 後に head が変わって再判定が増える（e-4、P2） | 採用 | D7-3: 順序を「B ↔ 発見 C → 試行錯誤 commit の整理 → GO 候補 head の固定 → 判定 C → C' → PR 作成・push」と明記。evidence tag は旧 head の到達可能性だけを担い、整理後の head に旧判定を移さないと明記。D7-1 に「レビュー用 worktree は固定 SHA からの detached worktree でよい」を追記 |
| 21 | 旧 runtime の日次 job は `staged` を含む catalog を読めず止まる。導入手順に runtime の版確認・切替が無い（e-5、P2） | 採用 | §4 に導入手順を追加: 共有 store へ `staged` を書き始める前に、登録済み runtime の head を確認し、新 head で `schedule-install` を行う。`EvidenceSchedule` の収集機構は変えない（tracked な psm1 は既存収集で拾う） |
| 22 | 「PR 前の Cloud は assist」は、PR 後なら正規 C' ができると読める。Cloud の Evidence 取得は未成立（e-6、P2） | 採用 | D7-6 を「PR の有無にかかわらず、固定 Evidence を取得・検証・閲覧できない Cloud 作業は assist。PR 作成や push だけで正規の C / C' に昇格させない」に改める。現行 Cloud 節の能力条件を残す |
| 23 | §5 の実 R2 往復 1 回が GO 必須か補助確認かが §2 判定定義と揃っていない（e 検証計画） | 採用（判断は A3） | 主担当案: 昇格の実 R2 往復 1 回を判定必須に含める（S2 の実経路確認）。offline fixture だけでは transport の実装差を観測できないため |

### A3 の裁定と統合

2026-10-16、owner は下記 6 点を主担当案のとおり裁定し、主担当が #1〜#23 の採用分を §2〜§5 へ revision 2 として統合した（#14 は不採用、#15・#16 は未決事項の解消）。凍結は owner が revision 2 本文を確認した後に行う。以下は裁定前の問いと主担当案を記録として残す。

### A3 で owner が決めること（裁定済み）

1. #4 と #18 の方式: 未送信・receipt 無しの `staged` の削除条件。主担当案は「既存 `deleted` を使い、条件は receipt 無しかつ（未送信、または GET 404 の確認記録あり）」。putIntent は再昇格のために保持し、削除条件に使わない。
2. #5 の採用: local prefix を廃止し、既存 reference を昇格前後で不変にする。`EvidenceContract.psm1` は変更なし。fetch は `staged` なら local、`ready` なら従来の remote。(e) も賛成。
3. #13 の配置: 送信部と昇格を `EvidencePromotion.psm1` へ分ける。(e) も賛成。
4. 公開点 (a) の時点: 主担当案は C' 収束後。履歴整理は判定 C 前に済ませる（#20）。(e) も同意見。
5. §2 未決事項 (2) local reference の `deploymentId`: #5 を採用すれば問いが消える。
6. #23: 昇格の実 R2 往復 1 回を判定必須に含めるか。主担当案は含める。

## 5.2 revision 2 への追加レビューと revision 3 への切り直し

2026-10-16、owner が Web 版 GPT（OpenAI）に revision 2（commit `eee62d78`、内容 sha256 `28a35753b88eec4b8b5980412bf48aba07fc27fc569e175f797a76680999b13f`）の追加レビューを依頼し、所見 (f) が返った。原記録は git 管理外の `TestResults/a2-local-stage/`。独立レビューには数えない。

**ADD-01（P1、設計保証の根拠不足）:** revision 2 の D4'「PUT 応答不明 → 回復 GET が 404 → `staged` へ戻し、同じ putIntent で再昇格」は、R2 の一貫性仕様（GET / HEAD の 404 は読取り時点の不在を示すだけで、結果不明の先行 PUT が後から確定しうる）のもとで I8「PUT は最大 1 回」を保証できない。再送しない場合も、不在観測の後に先行 PUT が確定すると、D5 の満了清掃がローカルだけを消して catalog を終端へ進め、remote object が残る。主担当が確認した事実: 既存の publish 契約（`tools/Artifacts/README.md`）は「PUT 応答不明を同 key 再送せず、照合できるまで未確認として residue を残す」と公開しており、D4' はこの契約を昇格経路だけで破っていた。固定 base の transport は条件付き PUT を使っていない。

**主担当の判断と owner の裁定（2026-10-16）:** ADD-01 は正しい。解消には条件付き PUT（transport の変更と I5 への波及）か、再送禁止と清掃前の remote 確認（network を使う清掃と「安全に停止」の受容）が要り、どちらも契約と責務を広げる。一方、昇格の消費者である Cloud からの R2 取得経路（program §4 C）は未成立で、今日の Cloud の読み手は PR ブランチを読んでいる。よって owner は「昇格を後続スライスへ移し、ローカル固定と D6 のワークフロー規則だけの revision 3 に切り直す。revision 2 の記録は残す」と裁定した。

**revision 3 で後続へ移送したもの:** revision 2 の D4（promote）、D4'（回復）、I8、S4、AC3・AC4・AC7・AC11・AC12、`EvidencePromotion.psm1` の新設、`remoteConfirmedAbsentAt` field、A3 裁定 1（削除条件）・3（Promotion 分離）・6（実 R2 往復）。これらと ADD-01 の「再送・成功確定・清掃・終了」の 4 論点、前回所見 R2-02・R2-03 を、後続スライス `Artifact Storage / Evidence 昇格と Cloud 配布`（program §4 C と組で A1 から行う）の入力にする。

**revision 3 に残したもの:** D1〜D3・D5（stage、reference 不変、fetch の local 分岐、publish 不変）、`staged` のローカル保持・保護・清掃（D4）、network 無しの preflight、D6 の 7 項目（3 文書同義、判定 C 前の履歴整理、公開点、Cloud の assist 条件、取得案内の実値非転記）、導入手順。前回所見 R2-01（実値の非転記）と (e) の #17・#19〜#22 はここに含む。

**整合の確認（主担当）:** revision 3 では `staged` から `put-pending` への遷移が無いため、`origin='stage'` の artifact は putIntent を持たず、ADD-01 の順序は構成できない（I5）。既存 publish の `put-pending` 回復は変更しない（I4）。(f) が求めた「統合後の反証レビュー 1 巡」は、`put-pending` を含まない状態遷移（未送信 `staged` → use / release → 満了 → ローカル削除、途中停止と再実行）に対して、凍結前に新規 session で 1 回行う。担当は A2 と同じく Grok 系を予約したまま選ぶ。

## 6. Phase B 実装結果

- 実装:
- HANDOFF との差:
- 未実行:
- implementation head commit:
- Phase B 担当・モデル・ベンダー:

## 7. Phase C

- 種別: 発見 / 判定
- 使用したevidence bundle の取得案内・状態:
- 構造適合:
- 現在の問いを阻害する findings:
- 後続スライスへ移送する findings:
- 実行したテストコマンドと `-Filter`、対象を選んだ理由:
- テスト結果:
- 判定必須のうち未実行:
- 未確認事項:
- 担当・モデル:

## 8. Phase C'

- 担当方式:
- 種別と巡:
- 受け取った決定記録の版:
- 使用したblind audit bundle の取得案内・状態:
- 確認範囲・方法:
- 判定:
- 現在の問いを阻害する findings:
- 後続スライスへ移送する findings:
- 残存リスク:
- 監査できなかった範囲:
- 独立性:
- 担当・モデル:

## 9. Phase D

- C / C' の突合:
- マージ判断:
- 必要なEvidenceの保存・取得・必須内容の閲覧確認:
- 取得案内の引渡し確認:
- harvest:
- 削除確認:
- 通常のtask終了・後始末の状態と未完了事項:
