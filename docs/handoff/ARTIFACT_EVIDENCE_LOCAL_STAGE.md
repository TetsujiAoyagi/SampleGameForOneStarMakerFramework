# Artifact Storage / Evidence のローカル固定と R2 昇格 HANDOFF

## 0. メタデータ

- type: `slice`
- status: `A`（A1 初稿。A2 独立レビューと A3 凍結は未実施。設計判断は凍結前）
- branch: `claude/evidence-local-stage`（PR の base は `develop`）
- implementation base commit: `6a51d2fefc9cf6d09d2a74a1ec1c42784e0e4524`（`origin/develop`、PR #127 マージ後）
- implementation head commit: 未固定
- risk: `high`（Evidence catalog の artifact state と cleanup の fail-closed 契約に触れる。加えて `osm-workflow` の Phase 境界・push・head 固定の常時規則を書き換える）
- owner: OSM maintainers
- created: 2026-10-16
- expires: 2026-11-16 または置換 revision
- harvest to: 操作と状態遷移を `tools/Artifacts/README.md`（Evidence の節と「エージェント操作の事前承認」）へ、Phase 規則の変更は `.agents/skills/osm-workflow/` の本文そのもの（本スライスの実装対象）へ。`BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §4 C（別host / Cloud）に「ローカル固定は完了、Cloud への配布経路は未着手のまま」を 1 行追記する。
- Phase A snapshot の取得案内・状態: 未保存（本 HANDOFF の commit を凍結対象版とする。A3 で内容 sha256 を引継ぎ票に書く）
- Phase B result snapshot / evidence bundle / C' blind bundle の取得案内・状態: 未生成（未到達 Phase）
- 引継ぎ票: 次の Phase は **A2（独立レビュー）**。入力は本 HANDOFF の commit（A1 初稿）。観点は §5 の A2 欄。A2 の担当は実装・ストア・資格情報に触れず、HANDOFF とコードの読取りだけを行う。A2 の後、owner と主担当が A3 で採否を決めて凍結する。引き渡さないもの: なし。

## 1. 目的と対象外

- 目的: 同一 PC で動く Agent 同士が、固定入力（差し戻し中の実装 snapshot、Phase A snapshot、判定 evidence bundle、C' blind bundle）を R2 も git push も使わずに内容 hash で固定して受け渡せるようにする。Cloud の読み手が決まった時だけ、同じ ZIP bytes を R2 へ昇格させ、packageSha256 を変えずに reference だけを R2 用に差し替えられるようにする。これを前提に、`osm-workflow` の push・commit・head 固定の規則を「公開点だけ」に書き換える。
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
- 対象外: Cloud・別 PC から R2 を fetch する経路（program §4 C）。`ready` artifact のローカル copy からの network 無し fetch。Harness の `external-current-v1` への全スライス移行。既存履歴の rebase。git 管理外のローカル運用メモ（手元層）の更新は owner が行う。Unity 側の変更なし。

## 2. 意思決定と受け入れ境界（A1 初稿。凍結前）

- このスライスが答える問い: 「同一 PC の Agent 間の固定入力を、R2 と push を使わずに内容 hash で固定して受け渡し、Cloud の読み手が決まった時だけ同じ hash のまま R2 へ昇格できるか。そしてそれを前提に push と commit の規則を公開点だけに絞っても、C / C' の head 固定と盲検の契約を保てるか」。
- A0 で owner が固定した前提: ツール変更とワークフロー規則変更は同じスライスで扱う（2026-10-16）。A2 が分割を提案する場合は理由を記録し、A3 で owner が決める。
- 方式:
  - **D1 `evidence stage`（ローカル固定）。** 既存 publish の「staging 登録 → snapshot → `CreateVerified` → `Extract` 再検証」までを実行し、R2 へ PUT しない。artifact を新 state **`staged`** で catalog に記録し、`key=$null`、`receipt=$null`、`packageSha256` / `manifestSha256` / `packageBytes` / `base` / `head` / `entries` は publish と同じ値を持つ。ZIP は staging operation 内に残し、`ownedCopies` に登録する。出力は `reference`（下記 D2 の local 形式）と `packageSha256`。network・資格情報・deployment の疎通を要しない。task guard と `Assert-EvidenceTransitionSafe` は従来どおり通す。
  - **D2 local reference。** `osm-evidence-local:` を prefix とし、payload は `schemaVersion=2`、`deploymentId`、`repositoryId`、`taskId`、`artifactId`、`base`、`head`、`packageBytes`、`manifestSha256` を持つ（`key` を持たない）。`Read-EvidenceReference` は両 prefix を受け、`kind`（`local` / `remote`）を返す。R2 形式 `osm-evidence-v2:` の payload・検査は変えない。
  - **D3 fetch の local 分岐。** local reference を受けた fetch は、catalog の artifact が `staged` または `ready` であり、identity（packageSha256・base・head・manifestSha256・packageBytes）が一致する場合に、staging operation 内の ZIP を新しい private operation へ copy し、R2 からの fetch と同じ `PackageIO.Extract` で期待 hash・manifest・全 entry を検証して `outputPath` を返す。network を一行も通らない。ZIP が無い・hash 不一致・identity 不一致は `identity-mismatch` / `local-payload-missing` で拒否し、R2 へ fallback しない。remote reference の fetch は従来どおり R2 から取得する。
  - **D4 昇格（promote）。** `evidence publish --profile osm --config <cfg> --artifact <artifactId>` は、`staged` の artifact について、staging operation 内の ZIP bytes の SHA-256 を catalog の `packageSha256` と照合してから、既存の PUT → 別 process GET → receipt 確定の経路に**同じ bytes** を流す。成功で `key` と `receipt` を設定し state を `ready` にする。packageSha256・manifestSha256・base・head は不変。再 snapshot・再 package はしない。出力は R2 形式の `reference` と同じ `packageSha256`。`staged` 以外の artifactId は `invalid-source` で拒否する（`ready` の再 PUT をしない）。PUT 応答不明は従来どおり `put-pending` で残し、`Resolve-EvidencePutGuarded` が解決する。
  - **D5 保持と清掃。** `staged` artifact は `ready` と同じ task 終了 + 30 日の保持規則に従う（`Get-EvidenceRetentionDecision` が `staged` を `ready` と同じ経路で判定する）。満了した `staged` の削除は、ローカルの ZIP と `ownedCopies` の除去と state `deleted` だけで、R2 への DELETE・GET を発行しない（`key` が無いため発行できない）。staging の 7 日清掃は `staged` artifact が参照する staging を `operation-pending` 相当として除外する（既存の「put-pending を参照する staging は除外」と同じ述語に `staged` を加える）。dry-run は catalog を変えない。
  - **D6 publish の既存経路は不変。** `--selection` を渡す publish は従来どおり snapshot から PUT まで一操作で行う。D1〜D5 は追加経路であり、既存 reference・receipt・cleanup の意味を変えない。
  - **D7 ワークフロー規則（`osm-workflow`）。** 以下を本スライスの実装対象として書き換える。
    1. `SKILL.md` 作業開始時 5 と `phases-and-handoff.md` 「Phase 境界の引継ぎ」: 照合対象を「リモートの head」から「ブランチの head」に変える。同一 PC の引継ぎは同じ worktree か、ローカルブランチから作った worktree で行う。origin との照合は Cloud・別 host の担当が割り当てられている時だけ。git は同じブランチを複数 worktree で checkout できないため、前 session の worktree を閉じるか同じ worktree を開く、と明記する。
    2. `SKILL.md` Phase B 出口と `phases-and-handoff.md` 「実行担当と判断担当」: 「push して終える」を「commit して終える。push は公開点の規則に従う」に変える。
    3. `SKILL.md` 「PR公開後の差し戻しとGit履歴」: 「push は配布であり保存ではない。リモートにしか居ない読者が居る時だけ push する」を原則として置き、公開点を (a) PR 作成（判定 C の GO 候補 head が固まり C' の巡が収束した時点）、(b) レビュー 1 巡ごとの修正 1 commit、(c) マージ前の最終（harvest と HANDOFF 削除）の 3 つに限る。例外は Cloud・別 host の担当がいる Phase 境界と、人間の明示指示。「判定 evidence を取った head は push して到達可能に保つ」は「push しないローカルの軽量 tag `evidence/<taskId>/<n>` で到達可能に保つ」に置き換える。push 前の点検に `pwsh tools/docs-audit.ps1` と機微 path の確認を加える。
    4. `review-evidence.md` 「対象を固定する」: 発見 C の差し戻し中は、implementation head commit に代えて `evidence stage` の packageSha256 で対象を固定してよい、と追記する。その snapshot には base からの `git diff` を `changes.patch` として、変更後 file と共に収録する。判定 C と C' の GO 候補は従来どおり commit で固定する。review-record commit の規則は不変。
    5. `review-evidence.md` 「保存先とリモートへの配布」: 対応済み環境の既定を `evidence stage` とし、R2 への昇格は「Cloud・別 host の読み手に渡す時」「端末喪失に備えて owner が指示した時」に限る。取得案内は local reference と期待 packageSha256 を持ち、昇格時は reference だけを差し替える（hash・base・head は書き直さない）。
    6. `phases-and-handoff.md` 「PR 状態ブロック」: 更新義務を「PR が存在する間」に限り、公開前は HANDOFF §0 を唯一の正本とする。「Cloud の担当」節に「Cloud は凍結入力を読む役（A2・C'）を既定とし、B の修正ループは例外。例外時のみ Phase 境界で push する」を加える。
    7. `handoff-template.md` §0: 取得案内の状態欄に `ローカル固定済み（未昇格）` を加える。
  - **D8 事前承認。** `tools/Artifacts/README.md` の「エージェント操作の事前承認」に `evidence stage` と local fetch を通常操作として加える。どちらも network・資格情報を使わない。昇格は既存 publish の承認範囲に含まれる。
- 却下した代替案:
  - 同一 PC でも R2 を経由する現状維持: 往復ごとに network と承認が要り、owner が不要と判断（2026-10-16）。
  - Storage を使わず patch を共有 folder に置く: catalog・保持・清掃・盲検分離の既存契約から外れ、昇格時に hash が変わる。
  - `staged` を staging の 7 日清掃に乗せる: 判定 C や C' の bundle を Cloud へ渡さない限り 7 日で消えてしまい、task 終了後 30 日の引渡し保証と矛盾する。A2 で再検討してよい（§2 未決事項）。
  - 昇格で再 snapshot する: `createdAt` で hash が変わり、取得案内の書き直しが要る。
  - `ready` artifact のローカル copy からの fetch: 価値はあるが本スライスの問いの外。後続への入力にする。
- 進める最低条件:
  - 不変条件（起きてはいけないこと）:
    - **I1** `evidence stage` と local fetch は、network・資格情報・deployment の疎通を一切行わない（偽 transport を注入した fixture で呼び出し 0 件を観測する）。
    - **I2** 昇格は staging 内の ZIP bytes をそのまま PUT し、再 snapshot・再 package をしない。昇格前後で `packageSha256`、`manifestSha256`、`base`、`head`、`packageBytes` が一致する。一致しなければ PUT せず `identity-mismatch` で止まる。
    - **I3** local fetch は identity 不一致・ZIP 欠落・hash 不一致で拒否し、R2 へ fallback しない。
    - **I4** `staged` artifact の清掃は task 終了 + 30 日の前に起きず、削除時に R2 へ DELETE・GET を発行しない。`staged` が参照する staging を 7 日清掃で消さない。
    - **I5** 既存の `--selection` publish、remote fetch、`ready` の cleanup・保持判定・reasonCode の意味を変えない（既存 suite が無変更で通る）。
    - **I6** `staged` 以外の artifactId への昇格、同じ artifactId の二重昇格を `invalid-source` で拒否する。
    - **I7** D7 の規則変更は、判定 C と C' の GO 候補が commit で固定されること、C' が所見を含まない入力だけを受けること、公開済み履歴を書き換えないことを変えない。
  - 成立条件（起きなければならないこと）:
    - **S1** 別 session が local reference と期待 packageSha256 だけから fetch し、全 entry 検証済みの copy で `changes.patch` と変更後 file を閲覧できる。
    - **S2** 昇格後、同じ packageSha256 で R2 形式 reference による fetch が成立し、別 process の GET で全 bytes が一致する。
    - **S3** `osm-workflow` の本文が D7 の 7 項目を含み、`docs-audit`・`contract-audit` が errors=0 で、「リモートの head」「push して終える」の旧文言が残らない。
- 受け入れ条件（検証例。I/S との紐付けを併記。offline fixture は隔離した temp store と偽 transport、実ストア・network 不使用。D7 は文書検査）:
  - **AC1**（S1, I1）: fixture の作業ツリーから `evidence stage` → 別 process で local fetch → 全 entry の SHA-256 が元 file と一致し、transport の呼び出し記録が 0 件。
  - **AC2**（I3）: catalog の `packageSha256` を書き換えた状態、ZIP を削除した状態、別 task の artifactId を指す reference、の 3 通りで local fetch が拒否され、transport 呼び出し 0 件。
  - **AC3**（S2, I2）: `staged` → 昇格 → remote fetch。昇格前後の identity 5 項目が一致し、PUT bytes の SHA-256 が `packageSha256` に一致する。ZIP を 1 byte 改変した状態では PUT 0 件で `identity-mismatch`。
  - **AC4**（I6）: `ready` の artifactId、`deleted` の artifactId、存在しない artifactId への昇格が `invalid-source`。昇格成功後の再昇格も `invalid-source` で PUT 0 件。
  - **AC5**（I4）: 時刻注入で task 終了 + 30 日の前日と当日を比較し、`staged` は前日 `retained`、当日 `expired` → 削除で ZIP と ownedCopies が消え、DELETE・GET 0 件、state `deleted`。`staged` が参照する staging は 7 日経過後も `operation-pending` 相当で残る。dry-run は catalog を変えない。
  - **AC6**（I5）: 既存の `ArtifactEvidence` / `EvidenceCleanup` / `EvidenceRetention` suite が無変更で failed 0。
  - **AC7**（I1 の失敗経路）: 昇格の PUT 応答不明を注入し、`put-pending` で残って `Resolve-EvidencePutGuarded` が GET で `ready` に解決する。stage 自身には PUT 経路が無いことを fixture で確認する。
  - **AC8**（S3, I7）: D7 の 7 項目が本文に入り、grep で旧文言（「リモートの head を照合」「push して終える」「push して到達可能に保つ」）が `.agents/skills/osm-workflow/` に残らない。判定 C・C' の commit 固定、C' の盲検、公開済み履歴の非書き換えの文言は残る。`docs-audit`・`contract-audit` errors=0。
  - **AC9**（D8）: README の事前承認節と Evidence の節に stage・local fetch・昇格の操作、state `staged`、local reference、保持規則が書かれ、旧記述と矛盾しない。
- ここでは答えない問いと所有する後続スライス:
  - Cloud・別 PC が R2 から fetch する経路と token の受け渡し → program §4 C。
  - `ready` artifact のローカル copy からの network 無し fetch → program §4 A の後続への入力。
  - 端末喪失に備えた未昇格 `staged` の backup → Harness H3。
  - 全スライスの Phase 記録を Git 外 CURRENT へ移す（HANDOFF を untracked にする） → 本スライスの結果を見て別 program で判断。
  - git 管理外のローカル運用メモ（手元層）の更新 → owner。
- 判定定義: GO = I1〜I7 を破る欠陥がなく、S1〜S3 が offline fixture と文書検査で成立。NO-GO = いずれかの不変条件の違反、または S の不成立。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし。
- 決定記録: なし。
- 本文へ転記した実装制約: `#nullable` と `record` の規則は Unity 側のみで本スライスには当たらない。PowerShell は `Set-StrictMode -Version Latest`。reasonCode は既存の小文字 kebab。path を CLI 出力に出さない。テストで `Start-Sleep` を使わず時刻を注入する。
- 未決事項（A2 への問い）: (1) `staged` の保持を task 終了 + 30 日にするか、staging と同じ 7 日にするか。(2) local reference に `deploymentId` を含めるか（R2 config に依存しない local 専用 reference にするか）。(3) ツール変更とワークフロー規則変更の分割。(4) 公開点 (a) を「判定 C の GO 候補が固まった時」にするか「C' 収束後」にするか。

## 3. 責務マップ

| ファイル | 責務 | 変更理由 | 現在行数 → 予想 | 備考 |
|---|---|---|---|---|
| `tools/Artifacts/EvidenceApplication.psm1` | publish / fetch の orchestration | publish を「準備（snapshot・package・検証）」と「送信（PUT・GET・receipt）」に分け、stage・local fetch・昇格を同じ部品で組む | 141 → 約 200（+40%） | 3 責務にならないよう、送信部を private 関数に抽出する。50% 未満だが 40% 超なので非分割理由を B 結果に書く |
| `tools/Artifacts/EvidenceContract.psm1` | config・selection・reference の契約 | local reference の読み書きと `kind` | 89 → 約 105 | R2 形式の検査は不変 |
| `tools/Artifacts/EvidenceRetentionPolicy.psm1` | 純粋な保持 policy | `staged` を `ready` と同じ経路で判定。staging 除外述語に `staged` 参照を加える | 60 → 約 66 | I/O なし。単体テスト可能 |
| `tools/Artifacts/EvidenceCleanup.psm1` | 経路検証と清掃 | `staged` の満了削除（ローカルのみ）、昇格の `put-pending` 解決は既存流用 | 335 → 約 355 | 500 行未満。削除 API の呼び分けは state で分岐 |
| `tools/artifacts.ps1` | CLI の引数解釈 | `evidence stage`、publish の `--artifact` | 175 → 約 185 | |
| `tools/Artifacts/README.md` | 公開仕様 | stage・local fetch・昇格・`staged`・事前承認 | 379 → 約 400 | |
| `.agents/skills/osm-workflow/SKILL.md` | Phase 規則 | D7-1〜3 | 105 → 約 112 | |
| `.agents/skills/osm-workflow/references/review-evidence.md` | レビュー証拠 | D7-4〜5 | 168 → 約 182 | |
| `.agents/skills/osm-workflow/references/phases-and-handoff.md` | Phase 境界 | D7-1、2、6 | 187 → 約 195 | |
| `.agents/skills/osm-workflow/references/handoff-template.md` | テンプレート | D7-7 | 146 → 約 148 | |
| `tools/Artifacts/tests/ArtifactEvidence.Tests.ps1`、`EvidenceRetention.Tests.ps1`、`EvidenceCleanup.Tests.ps1` | offline 検証 | AC1〜AC7 | 追加ケース | 既存の偽 transport と時刻注入を使う |

状態の所有者は task の catalog（task guard が直列化）。新しい state は `staged` の 1 つ。寿命は `ready` と同じ task 終了 + 30 日。新しい公開面は CLI の `evidence stage` と publish の `--artifact`、reference の `osm-evidence-local:` prefix。新しいファイル・asmdef・依存 edge は作らない。Unity 側の変更なし。

## 4. 実装計画

- 変更対象: §3 のファイル。
- 順序: (1) `EvidenceContract` の local reference（純粋、既存検査の無変更をテストで固定）→ (2) `EvidenceRetentionPolicy` の `staged`（純粋）→ (3) `EvidenceApplication` の準備／送信の分離（既存 publish の挙動不変を AC6 で固定）→ (4) `stage` → (5) local fetch → (6) 昇格 → (7) `EvidenceCleanup` の `staged` 満了削除 → (8) CLI → (9) README → (10) `osm-workflow` の D7 → (11) `docs-audit`・`contract-audit`。
- Phase B から Phase A へ差し戻す条件: catalog スキーマ（`Read-EvidenceTask` の `Assert-Fields`）の変更、`EvidenceSchedule` の変更、`PackageIO` の manifest 形式の変更、Workflow の終了意味の変更が必要になったとき。D7 の文言が凍結済みの他規則（C' の盲検、head 固定）と矛盾し、本文の読み替えで解消できないとき。
- 対象外を維持する方法: stage と local fetch の経路に `Invoke-ArtifactNetwork` の呼び出しを置かない（AC1・AC2 の呼び出し記録 0 件で確認）。昇格は既存送信部を呼ぶだけで、snapshot 生成の関数を呼ばない。

## 5. テストとレビュー計画

- 単体テスト: local reference の往復と拒否、`staged` の保持判定、staging 除外述語を純粋関数として `ArtifactEvidence.Tests.ps1` と `EvidenceRetention.Tests.ps1` に追加。
- 差し戻し中の起点 `-Filter`: `tools/Artifacts/tests/ArtifactEvidence.Tests.ps1`、`EvidenceRetention.Tests.ps1`、`EvidenceCleanup.Tests.ps1 -Case <新規ケース>`。C は凍結済み条件の確認に必要なら根拠を記録して変えてよい。
- 判定必須テスト（GO 候補 head）: `tools/Artifacts/tests/*.Tests.ps1` と `tools/Workflow/tests/*.Tests.ps1` の全体（`StoreContext.Tests.ps1` は非仮想化 host で）、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC8 の旧文言 grep。`EvidenceCleanup.Tests.ps1` は `rg` が PATH に要る。
- 全 EditMode 回帰の適用除外: Unity 側コードを変更しないため。代替証拠は上記 offline suite 全体と audit。
- 統合・Unity テスト: なし。
- 操作・実行時・目視条件の検証経路: 昇格の実 R2 往復 1 回を運用経路の確認として判定 C で行う（限定実 R2。隔離した task ID を使い、終了後に通常清掃へ乗せる）。それ以外は offline fixture で観測できる。
- 未知の操作経路の疎通結果: なし。
- 人間の判断が必要な条件: §2 未決事項 (1)〜(4) の A3 裁定。
- 高コスト証拠: なし。判定 C の照合 script: なし。
- 機械検査: `contract-audit`、`docs-audit`、`git diff --stat`、AC8 の grep。
- A0/A1 主担当・モデル・ベンダー: Claude（この session）。
- A2 独立レビュー（未実施。入力版は本 HANDOFF の commit 後の内容 sha256 を正とし commit hash を補助）: 高リスクのため、(a) アーキテクチャゲート担当 1 件以上（state 追加と準備／送信分離の配置）、(b) fail-closed の破り方（I1〜I6 を破る順序を各 1 つ挙げる）担当、(c) D7 の規則変更だけを読み、C' の盲検と head 固定の契約との整合を見る担当、を推奨。担当・モデルは A2 開始時に選ぶ。互いの指摘は見せない。
- A3 統合担当・モデル・採否: 未実施。
- C' 用に予約した担当・モデル・ベンダー: 未定。A2 で使い切らない。
- 独立性の強化条件を満たせない場合の理由: 未定。

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
