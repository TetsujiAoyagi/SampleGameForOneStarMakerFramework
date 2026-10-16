# Artifact Storage / Evidence の R2 昇格 HANDOFF

## 0. メタデータ

- type: `slice`
- status: `A`（A1 初稿。A2 独立レビュー待ち。設計判断は凍結前）
- branch: `claude/evidence-r2-promotion`（PR の base は `develop`）
- implementation base commit: `ec5288d73804cc23ec0edfe35370d16751e40157`（A1 時点の `origin/develop`、PR #129 マージ後。A3 で確定する）
- implementation head commit: 未固定
- risk: `high`（Evidence catalog の state 遷移と、PUT 応答不明・削除の fail-closed 契約に触れる。network を使う送信経路を増やす）
- owner: OSM maintainers
- created: 2026-10-17
- expires: 2026-11-17 または置換 revision
- harvest to: 操作・状態遷移・回復規則を `tools/Artifacts/README.md`（「ローカル固定」「Evidence v2 の通常操作」「終了・再開・利用中と清掃」「エージェント操作の事前承認」）へ。昇格の使い方は `.agents/skills/osm-workflow/references/review-evidence.md` の「保存先とリモートへの配布」（本スライスの実装対象）。`BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §4 C に「昇格は完了。Cloud・別 PC からの取得経路は未着手」を記録する。
- Phase A snapshot の取得案内・状態: 未保存（本 HANDOFF の commit を A2 の入力版とする。内容 sha256 は §5 の A2 欄に記録する）
- Phase B result snapshot / evidence bundle / C' blind bundle の取得案内・状態: 未生成（未到達 Phase）
- 引継ぎ票: 次は **A2（独立レビュー）**。入力は本 HANDOFF の A1 初稿の commit（§5 の A2 欄に SHA と内容 sha256）。観点は §5 の A2 欄。レビュー担当は read-only で、実ストア・資格情報・network・push を使わない。互いの指摘を見せない。A2 の後は主担当が §5.1 に結果と採否案をまとめ、A3 で owner が裁定・凍結する。push はしない（公開点は `osm-workflow` の「公開点とGit履歴」）。

## 1. 目的と対象外

- 目的: `evidence stage` でローカル固定した Evidence（state `staged`）を、**同じ artifact・同じ ZIP bytes のまま** R2 へ置けるようにする（昇格）。reference と packageSha256 を変えず、取得案内は `locality` だけを `local` から `promoted` に更新すれば済む。PUT 応答不明・回復 GET の 404・再送・成功確定・清掃・task 終了の各場面で、データを失わず、確認できない削除を削除済みにせず、R2 に孤児を残す経路を作らない回復契約を閉じる。
- 現況（2026-10-17、`origin/develop` = `ec5288d7`）:
  - `evidence stage`（PR #128）は publish と同じ準備（selection → guard → network 無し preflight → snapshot → `PackageIO.CreateVerified` → `Extract` 再検証）までを行い、artifact を state `staged`・`origin: stage`・`putIntent=null`・`receipt=null` で catalog に入れる。ZIP は staging operation 内の `ownedCopies` に唯一の複製として残る。`key` は `Get-EvidenceKey` が artifactId から導く値を未送信のまま持つので、reference（`osm-evidence-v2:`）は R2 に置いても変わらない（[EvidenceApplication.psm1](../../tools/Artifacts/EvidenceApplication.psm1) `Invoke-EvidenceStage`）。
  - publish の送信部（同ファイル `Invoke-EvidencePublish`）は、`intents/<artifactId>.json` を `Write-EvidenceAtomic -Immutable`（上書き不可）で書き → catalog に `putIntent` を記録 → package に read lock（`FileShare.Read`）を取って PUT → 別 process の readback GET で bytes・hash を照合 → `receipts/<artifactId>.json` を immutable で書き → state `ready`。PUT 応答不明は同 key へ再送せず `put-pending` のまま残す。transport（[R2ArtifactTransport.cs](../../tools/Artifacts/Transport/R2ArtifactTransport.cs)）は条件付き PUT（`If-None-Match`）を使っていない。
  - `put-pending` の回復（[EvidenceCleanup.psm1](../../tools/Artifacts/EvidenceCleanup.psm1) `Resolve-EvidencePutGuarded`）は、GET が一致すれば receipt を書いて `ready`、GET が 404 なら state を `failed` にする。`failed` は保持 policy で `unconfirmed-artifact` になり、清掃・DELETE されない。つまり既存 publish の「404 の後に先行 PUT が遅れて確定する」場合の remote object は、今も削除されない residue として残る（既存の受容。本スライスは publish 側を変えない）。
  - 読戻し検査（[EvidenceStateStore.psm1](../../tools/Artifacts/EvidenceStateStore.psm1) `Read-EvidenceTask`）は state を 6 値（`put-pending` / `ready` / `delete-pending` / `deleted` / `failed` / `staged`）、`origin` を `publish` / `stage` に限り、`ready` / `delete-pending` と origin が `stage` 以外の `deleted` に receipt を要求する。
  - 保持 policy（[EvidenceRetentionPolicy.psm1](../../tools/Artifacts/EvidenceRetentionPolicy.psm1)）は `staged` のうち `putIntent` も `receipt` も無いものだけを `ready` と同じ経路で判定し、`putIntent` を持つ `staged` は `unknown-state`（削除しない）にしている。清掃は `staged` をローカル削除（`Invoke-EvidenceLocalDeleteGuarded`）、`ready` を remote 削除（deleteIntent → DELETE → GET 404 `NoSuchKey` → `deleted`）に分ける。remote 削除の照合 GET が一致した場合は state を `ready` に戻す（`Resolve-EvidenceDeleteGuarded`）。
  - fetch は guard を取った後、preflight の前に catalog を読み、対象が `staged` なら network 無しの preflight と local 分岐、そうでなければ network 付き preflight と remote 分岐を選ぶ。選択は preflight 後に変わらない。
  - `use` / `release` は `ready` と `staged` を受け付ける。
  - 前スライスで後続へ送った入力（program §4 C）: 旧 HANDOFF revision 2 の D4（promote）/ D4'（回復）/ I8 / S4、追加レビュー ADD-01（R2 の GET 404 は先行 PUT の失敗確定ではなく、同 key 再送禁止の既存 publish 契約と衝突する）、前回所見 R2-02（昇格時の catalog 更新単位）、fetch の local / remote 選択が preflight 前に決まる点。全文は削除前の HANDOFF（`docs/handoff/ARTIFACT_EVIDENCE_LOCAL_STAGE.md`、commit `72a8b0c`、revision 2 は `16509b46^`）。
- 対象外: Cloud・別 PC が R2 から fetch する経路と token の受け渡し（2026-10-17 owner 裁定で本スライスから外した。program §4 C の後続）。既存 publish の `put-pending` 回復と `failed` の扱い。`ready` artifact のローカル copy からの network 無し fetch。条件付き PUT など transport の変更。`use` の network 無し preflight（program §4 C の入力のまま）。Unity 側の変更なし。

## 2. 意思決定と受け入れ境界（A1 初稿）

- このスライスが答える問い: 「`staged` の artifact を、同じ reference・同じ packageSha256・同じ bytes のまま R2 へ昇格でき、PUT 応答不明から先のどの停止点でも、payload を失わず、確認できない状態を成功・削除済みにせず、R2 上の object を catalog の追跡外に残さずに、昇格・取得・清掃へ収束できるか」。
- A0 で owner が固定した前提: 範囲は昇格だけ。Cloud・別 PC からの取得経路は後続（2026-10-17）。
- 方式:
  - **D1 `evidence promote`。** `pwsh tools/artifacts.ps1 evidence promote --profile osm --config <cfg> --reference <ref> --sha256 <期待 packageSha256>`。reference の taskId で task guard を取り、通常の（network を使う）preflight と Sync を行う。task が `active` でなければ `resume-required`（publish・stage と同じ）。preflight と Sync の**後**の state で分岐する。
    - `staged` かつ `origin: stage`: 昇格へ進む（D2）。
    - `ready` かつ `origin: stage` で identity（key・base・head・manifestSha256・packageBytes・packageSha256 = 期待 hash）が一致: 送信せず `status: passed`、`reasonCode: already-promoted`。前回の昇格の応答が失われても、同じコマンドの再実行で収束させるため。
    - それ以外（`origin: publish`、`put-pending` のまま、`failed`、`delete-pending`、`deleted`、catalog に無い）: `invalid-source`（`deleted` は既存どおり `expired`、無いものは `catalog-missing`）。identity 不一致は `identity-mismatch`。いずれも PUT 0 件。
  - **D2 送信（同じ bytes）。** (1) catalog の `ownedCopies` から packageSha256・packageBytes が一致する ZIP を選び、read lock（`FileShare.Read`）で開く。lock を保持したまま、その file の SHA-256 と長さを catalog の値と照合する。不一致なら PUT せず `identity-mismatch`。再 snapshot・再 package はしない。(2) putIntent: artifact が既に `putIntent` を持てば、その file の sha256 と key・packageSha256・artifactId を照合して再利用する。持たなければ `intents/<artifactId>.json` を確認し、既に在る（前回の昇格が catalog 記録の前に止まった）なら同じ照合で再利用、無ければ publish と同じ形で immutable に書く。照合に失敗したら `unresolved-put`。(3) readback 用の新しい staging operation を登録し（回復 GET と同じ `New-EvidenceRecoveryStage` の形）、**1 回の catalog 書込みで** `putIntent` の参照・state `put-pending`・`operation` を記録する（R2-02 の更新単位）。(4) lock を保持したまま同じ file を PUT し、別 process の readback GET で bytes・hash を照合する。(5) receipt を publish と同じ形（`putIntentSha256` を含む）で immutable に書き、**1 回の catalog 書込みで** `receipt`・state `ready`・`operation=null`・readback staging の採用を記録する。`origin: stage` は残す。出力は `reference`（入力と同じ文字列）、`packageSha256`、`locality: promoted`、`receipt`、`residue.remote`。PUT または readback が確認できなければ `put-pending` のまま `unconfirmed` を返す。
  - **D3 再送の許可（ADD-01 への答え）。** 昇格では、同じ key へ同じ bytes（catalog の packageSha256 と一致を確認した ZIP）を再度 PUT してよい。既存 publish の「同 key 再送禁止」は publish が毎回別の snapshot を作る経路の契約で、昇格には適用しない。根拠: 送る bytes は packageSha256 で固定され、R2 の PUT は object 全体の置換なので、先行 PUT が後から確定しても後続 PUT と同じ内容になる。保証するのは「その key の object は、存在するなら packageSha256 の bytes である」ことと「`ready` は別 process の readback 一致の後にしか書かない」ことで、「PUT が最大 1 回」は保証しない（旧 revision 2 の I8 を置き換える）。
  - **D4 昇格由来の `put-pending` の回復。** `Resolve-EvidencePutGuarded` は `origin: stage` の artifact について、GET が一致すれば既存どおり receipt を書いて `ready`。GET が 404 なら state を `staged` に戻し、`putIntent`・ZIP・`ownedCopies`・保護をそのまま残す（`failed` にしない）。GET が他の結果（別 bytes、403、timeout 等）なら既存どおり `unresolved-put` で止まる。`origin: publish` の挙動は変えない。戻した `staged` は local fetch でき（D6）、D1 の再実行で同じ putIntent を再利用して再送できる。404 は「今は無い」の観測であり、遅れて確定する先行 PUT はありうる。それは D3 で同じ bytes になり、D5 で必ず remote 削除の経路に乗るので、catalog の追跡外には残らない。
  - **D5 送信を試みた `staged` の清掃。** 保持 policy は `putIntent` を持つ `staged`（receipt 無し）を `unknown-state` にせず、`ready` と同じ保持・保護規則で判定する。削除の経路は純粋関数で決める: `putIntent` も `receipt` も無い `staged` だけがローカル削除（既存）。`putIntent` を持つ `staged` と `ready` は remote 削除（deleteIntent → DELETE → GET 404 `NoSuchKey` → `deleted`）。remote 削除の照合 GET が一致した場合、receipt を持つ artifact は既存どおり `ready` に、receipt を持たない artifact は `staged` に戻す（次回の清掃が新しい intent で再試行する）。R2 側の保証の限界: 全ての PUT は task が `active` の間にだけ発行され（D1）、remote 削除は task 終了 + 30 日以後にだけ起きるので、削除の後に遅れて確定する PUT は 30 日以上未完了の HTTP 要求を要する。これは受容する残存リスクとして README に書き、保証とは書かない。
  - **D6 fetch の取得元を preflight 後に決める。** fetch は preflight 前の catalog の読取りで「preflight に network を使うか」だけを決める（対象が `staged` なら network 無し。前スライスの I1 を維持）。取得元（local / remote）は preflight と Sync の**後**の state で決める: `staged` → local 分岐、`ready` → remote 分岐、それ以外 → 既存の拒否。これにより、`put-pending` の昇格中 artifact は network 付き preflight で `ready` または `staged` に解決された後、正しい取得元で取得される（古い選択による `identity-mismatch` を返さない）。`ready` になった昇格済み artifact は従来どおり R2 から取得する（ローカル copy からの取得は対象外）。network 無しの保証は「呼び出し開始時に `staged` の artifact」に限ると README に書く。
  - **D7 inspect。** `locality` は `staged` → `local`、`ready` → `promoted`、それ以外 → null（既存どおり）。`staged` で `putIntent` を持つものも `local`（remote に在るかは未確認）。
  - **D8 catalog の読戻し検査。** `origin: stage` の artifact に許す組み合わせを固定する。`staged`: receipt 無し（putIntent は有無どちらも可）。`put-pending`: putIntent 必須・receipt 無し。`ready`: receipt 必須。`delete-pending`: receipt 必須、または receipt 無しなら putIntent 必須。`deleted`: receipt 不要（既存）。`failed`: 許さない（`corrupt-state`。昇格はこの state を作らない）。`origin: publish` の検査は変えない。新しい state・新しい field は足さない。
  - **D9 ワークフロー文書。** `review-evidence.md` の「保存先とリモートへの配布」で、「昇格経路が後続スライスで成立するまで従来どおり publish」を、「Cloud・別 host の読み手、または端末喪失に備えて owner が指示した時は、同じ artifact を `evidence promote` で昇格する。reference と packageSha256 は変わらないので、取得案内は `locality` だけを更新する。Cloud・別 host からの取得経路は後続（program §4 C）で、成立するまでその読み手の作業は assist」に置き換える。`SKILL.md`・`phases-and-handoff.md`・`handoff-template.md` は変えない（状態「ローカル固定済み（未昇格）」と Cloud の assist 条件はそのまま正しい）。
  - **D10 README と事前承認。** `tools/Artifacts/README.md` の冒頭と「ローカル固定」の「昇格は後続」を D1〜D7 の操作・回復規則・残存リスクに置き換える。「エージェント操作の事前承認」に `evidence promote` を既存 publish と同じ承認範囲の通常操作として加える（同じ endpoint・bucket・prefix・既存資格情報。新しい資格情報・公開範囲は無い）。導入手順（§4）を書く。
- 却下した代替案:
  - 条件付き PUT（`If-None-Match: *`）で再送を禁止する: transport の変更と publish（I6）への波及が要り、R2 の S3 互換 API での挙動確認が未知の操作経路になる。D3 の「同じ bytes の再送は無害」で足りる。
  - 404 の後も `put-pending` のまま残す（既存 publish と同じ扱い）: local fetch・stage・cleanup が同じ task で `unresolved-put` により止まり続け、昇格を失敗させた artifact の唯一の複製を使えなくなる。
  - 404 の後に `failed` にする: staging が 7 日清掃の対象になり、唯一の複製を失う（旧 revision 2 の A2 #2）。
  - 404 の後に `remoteConfirmedAbsentAt` を記録してローカル削除を許す（旧 revision 2 の D4'・D5）: ADD-01 のとおり遅れて確定した object が追跡外に残る。
  - 昇格のたびに再 snapshot して新しい artifact を publish する: reference と hash が変わり、取得案内の書き直しが要る（それは既存 publish で今も可能）。
- 進める最低条件:
  - 不変条件（起きてはいけないこと）:
    - **I1** promote が、`staged`（`origin: stage`）以外の artifact、または identity・期待 hash が一致しない artifact に PUT を発行する。`ready` の再 promote が PUT を発行する。
    - **I2** promote が、catalog の packageSha256・packageBytes と一致しない bytes を PUT する。照合から PUT 完了までの間に ZIP が書き換えられる。再 snapshot・再 package が起きる。
    - **I3** 昇格または回復の前後で、reference 文字列・artifactId・key・packageSha256・manifestSha256・base・head・packageBytes・entries・taskEndedAt・deleteEligibleAt・protections のいずれかが変わる。
    - **I4** 別 process の readback で bytes・hash の一致を確認する前に、state `ready` または receipt が書かれる。
    - **I5** `putIntent` を持つ artifact（PUT を試みた可能性があるもの）が、remote DELETE と GET 404 の確認を経ずに `deleted` になる。昇格由来の artifact が `failed` になる。`deleted` になる前に ZIP・putIntent・receipt の参照が失われる。
    - **I6** 既存の publish・remote fetch・`origin: publish` の `put-pending` 回復・`ready` の清掃と保持判定・reasonCode の意味が変わる。呼び出し開始時に `staged` の artifact への stage・fetch が network・資格情報を使う。
    - **I7** fetch が、自分の preflight の中で state が変わったことを理由に、取得できる artifact を拒否する。local と remote の間で失敗時に fallback する。
    - **I8** 保護中（有効な `use`）または task 終了 + 30 日前の artifact が、昇格・回復・清掃のどれかで削除される。
  - 成立条件（起きなければならないこと）:
    - **S1** `staged` → promote → `passed`。返る reference と packageSha256 が stage の出力と同じで、inspect の `locality` が `promoted`。別 process の remote fetch が同じ reference と期待 hash で成功し、全 entry の hash が元 file と一致する。
    - **S2** PUT 応答不明 → `put-pending` → 回復の 2 終点: GET 一致なら `ready`。GET 404 なら `staged`（putIntent 保持）に戻り、local fetch が成立し、promote の再実行が同じ putIntent を再利用して `ready` に達する。
    - **S3** 清掃の 2 終点: 送信を試みていない `staged` は満了でローカル削除（既存）。putIntent を持つ `staged` は満了で remote 削除の経路を通って `deleted` になり、その後も `Read-EvidenceTask` が通る。
    - **S4** `ready` の再 promote と、応答を失った昇格の再実行が `already-promoted`（PUT 0 件）で収束する。
    - **S5** 実 R2 で、隔離した task の `staged` を 1 回昇格し、別 process の remote fetch で全 entry が一致する（高コスト証拠。§5）。
- 受け入れ条件（検証例。offline は隔離した temp store・偽 transport・時刻注入。実ストア・network 不使用。実 R2 は AC12 だけ）:
  - **AC1**（S1, I3, I4）: fixture の `staged` を promote。偽 transport が記録した PUT bytes の SHA-256 が packageSha256 と一致し、readback GET の後にだけ `ready` と receipt が書かれる（偽 transport の readback を失敗させると `put-pending` のまま receipt 無し）。昇格前後で I3 の全 field と reference 文字列が一致する。inspect の `locality` が `local` → `promoted`。別 process の fetch（remote 分岐）が成功する。
  - **AC2**（I1）: `ready`（`origin: publish`）、`put-pending`、`failed`、`delete-pending`、`deleted`、catalog に無い reference、期待 hash 不一致に promote → それぞれ `invalid-source` / `expired` / `catalog-missing` / `identity-mismatch`、PUT 0 件。
  - **AC3**（S4, I1）: 昇格済み（`origin: stage` の `ready`）への promote が `already-promoted`、PUT 0 件。合格例: 同じ期待 hash。不合格例: 別の期待 hash は `identity-mismatch`。
  - **AC4**（I2）: ZIP を 1 byte 改変した `staged` の promote は PUT 0 件で `identity-mismatch`。照合後・PUT 前に別 process が ZIP を書込みで開こうとすると共有違反で失敗する（lock の保持）。snapshot・package を作る関数が promote の経路から呼ばれない（呼び出し記録 0 件）。
  - **AC5**（S2, I5）: promote の PUT 応答不明を注入 → `put-pending`、putIntent 記録済み。回復で GET 一致 → `ready`（receipt あり）。別 fixture で GET 404 → `staged`、putIntent・ZIP・ownedCopies・protections が残り `failed` にならない。続けて local fetch が成功し（transport 呼び出し 0 件）、promote の再実行が既存の intent file を作り直さずに PUT 1 回で `ready` になる。`origin: publish` の `put-pending` は既存どおり 404 で `failed`。
  - **AC6**（S2, I5）: promote を「intent file を書いた後、catalog に記録する前」で停止 → artifact は `staged`・putIntent 無しのまま、local fetch 成立。再 promote は既存の intent file を照合して再利用する。intent file の内容が key・packageSha256・artifactId のどれかで食い違えば `unresolved-put` で PUT 0 件。
  - **AC7**（I7）: `put-pending`（`origin: stage`）の artifact を fetch。network 付き preflight で GET 一致 → `ready` に解決されて remote 分岐で成功。GET 404 → `staged` に解決されて local 分岐で成功（`identity-mismatch` を返さない）。取得失敗時に他方へ fallback しない（local の ZIP 欠落は `changed-copy`、remote の GET 失敗は既存の reasonCode）。
  - **AC8**（S3, I5, I8）: 時刻注入で task 終了 + 30 日の前日と当日。putIntent を持つ `staged` は前日 `retained`、当日 remote 削除（deleteIntent → DELETE → GET 404）で `deleted`、ZIP と ownedCopies が消え、その後も `Read-EvidenceTask` が通る。DELETE を失敗させると `delete-pending`（receipt 無し）で残り、ZIP は残る。照合 GET が一致すれば `staged` に戻り、ZIP と putIntent が残る。putIntent も receipt も無い `staged` は既存どおりローカル削除（DELETE・GET 0 件）。
  - **AC9**（I8）: `staged` に `use` → promote → 満了時刻を過ぎても `in-use`。回復で `staged` に戻った後も保護が残る。`release` 後に満了判定。
  - **AC10**（I6）: 既存の `ArtifactEvidence` / `EvidenceCleanup` / `EvidenceRetention` / `EvidenceSchedule` / `EvidenceReset` suite のケースが無変更で failed 0（publish の送信部を共有化しても挙動不変）。`origin` 無しの既存 catalog、`origin: stage` の既存 shape（putIntent 無しの `staged` / `deleted`）を読める。呼び出し開始時に `staged` の artifact への stage・fetch で transport と資格情報の読取りが 0 件（前スライスの AC を無変更で再実行）。
  - **AC11**（D8, I5）: `origin: stage` かつ `failed`、`put-pending` で putIntent 無し、`staged` で receipt あり、`delete-pending` で receipt も putIntent も無い catalog は `corrupt-state` で読まれず、その task の削除が起きない。
  - **AC12**（S5、高コスト証拠）: §5 の手順で実 R2 に 1 回。
  - **AC13**（D9, D10）: README・`review-evidence.md` に promote の操作・D3〜D6 の回復規則・残存リスク・導入手順・事前承認が書かれ、「昇格は後続」「昇格経路が後続スライスで成立するまで」の旧文言が `tools/Artifacts/README.md` と `.agents/skills/osm-workflow/` に残らない。`docs-audit`・`contract-audit` が errors=0。
- ここでは答えない問いと所有する後続スライス:
  - Cloud・別 PC が R2 から fetch する経路、読取り専用 token の登録・受け渡し → program §4 C（`Artifact Storage / Evidence の Cloud 配布`、新規）。
  - `ready` artifact のローカル copy からの network 無し fetch（昇格後も同じ PC の読み手が network 無しで取得する） → program §4 A の後続への入力。
  - `use` の network 無し preflight → program §4 C の入力のまま。
  - 既存 publish の 404 → `failed` で残る remote residue の回収 → Content の容量上限・孤児回収の入力。
  - 端末喪失に備えた未昇格 `staged` の定期 backup → Harness H3。
- 判定定義: GO = I1〜I8 を破る欠陥がなく、S1〜S4 が offline fixture と文書検査で成立し、S5 の実 R2 昇格 1 回が高コスト証拠として成立し、判定 C と最終 C' がともに阻害 0。NO-GO = いずれかの不変条件の違反、または S の不成立。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし。
- 決定記録: なし。
- 本文へ転記した実装制約: `#nullable` と `record` の規則は Unity 側のみで本スライスには当たらない。PowerShell は `Set-StrictMode -Version Latest`。reasonCode は既存の小文字 kebab（新規は `already-promoted` の 1 つ）。path・key・資格情報を CLI 出力とログに出さない。テストで `Start-Sleep` / `Task.Delay` / `Thread.Sleep` を使わず時刻と transport を注入する。catalog に新しい state・新しい field を足さない（D8 は既存 field の組み合わせの検査だけ）。資格情報の root・暗号文・DPAPI の利用を変えない。実ストアの catalog・transfers・資格情報を offline テストで読み書きしない。
- 未決事項（A2 で反証を求め、A3 で裁定する）:
  1. D3 の再送許可。既存 README の「PUT 応答不明を同 key 再送せず」を publish 側の契約に限ると明記して足りるか。
  2. D5 の残存リスク（削除後に遅れて確定する PUT）を受容とするか、remote 削除の後にもう 1 回の確認を置くか。
  3. D1 の `already-promoted` を `status: passed` にするか（主担当案）、別の status にするか。
  4. §3 の配置: publish と promote の送信部を共有する新 module を作るか（主担当案）、promote を `EvidenceApplication.psm1` に置き publish は触らないか。

## 3. 責務マップ

| ファイル | 責務 | 変更理由 | 現在行数 → 予想 | 備考 |
|---|---|---|---|---|
| `tools/Artifacts/EvidenceSend.psm1`（新規） | 1 artifact の送信: putIntent の作成・再利用、read lock 下の hash 照合と PUT、別 process readback、receipt 確定。publish と promote の共通部、および `Invoke-EvidencePromote` の orchestration | 送信と昇格は「同じ bytes を確実に 1 つの key に置き、確認後にだけ `ready` にする」という同じ変更理由を持つ | 0 → 約 120 | 依存: Application → Send → Cleanup / StateStore / ArtifactApplication の一方向。Cleanup は Send を import しない。回復（GET による解決）は Cleanup に残す |
| `tools/Artifacts/EvidenceApplication.psm1` | publish / stage / fetch / inspect / use の orchestration | publish の送信部を Send へ移す。fetch の取得元を preflight 後に決める（D6）。inspect の `locality`（D7） | 203 → 約 185 | 3 責務にならない。送信は持たない |
| `tools/Artifacts/EvidenceCleanup.psm1` | preflight、put / delete の回復、清掃 | `Resolve-EvidencePutGuarded` の `origin: stage` 分岐（404 → `staged`）、`Resolve-EvidenceDeleteGuarded` の receipt 無し一致 → `staged`、清掃の経路選択を D5 の純粋関数に委ねる | 369 → 約 390 | 500 行未満。既存の削除 API の呼び分けは route の値で分岐 |
| `tools/Artifacts/EvidenceRetentionPolicy.psm1` | 純粋な保持 policy | putIntent を持つ `staged` の判定、削除経路の純粋関数 `Get-EvidenceDeletionRoute`（`local` / `remote` / `none`） | 68 → 約 85 | I/O なし。単体テスト可能 |
| `tools/Artifacts/EvidenceStateStore.psm1` | catalog の読戻し検査 | D8 の origin × state × putIntent × receipt の組み合わせ | 144 → 約 152 | 新 state・新 field なし |
| `tools/artifacts.ps1` | CLI の引数解釈 | `evidence promote` の grammar と dispatch | 192 → 約 196 | |
| `tools/Artifacts/README.md` | 公開仕様 | D10 | 447 → 約 470 | |
| `.agents/skills/osm-workflow/references/review-evidence.md` | レビュー証拠の保存・配布 | D9 | 172 → 約 172 | 置換のみ |
| `tools/Artifacts/tests/ArtifactEvidence.Tests.ps1`、`EvidenceCleanup.Tests.ps1`、`EvidenceRetention.Tests.ps1`、`EvidenceTestSupport.ps1` | offline 検証 | AC1〜AC11 のケース追加。偽 transport の PUT bytes 記録、応答不明・404 の注入、停止点の FaultHook | 追加ケース | 既存の偽 transport・時刻注入・FaultHook を使う。`EvidenceCleanup.Tests.ps1` は 505 行で、テストは同じ変更理由の列挙なので非分割 |

状態の所有者は task の catalog（task guard が直列化）。寿命は既存の task 終了 + 30 日。新しい公開面は CLI の `evidence promote` と reasonCode `already-promoted`。新しいファイルは `EvidenceSend.psm1` の 1 つ。asmdef・Unity 側の変更なし。`EvidenceSchedule` の runtime 収集は変えない（tracked な psm1 は既存の `git ls-files` 収集で入る）。

## 4. 実装計画

- 順序: (1) `EvidenceStateStore` の D8（既存 shape の読戻し不変を AC10 で固定）→ (2) `EvidenceRetentionPolicy` の判定と `Get-EvidenceDeletionRoute`（純粋）→ (3) `EvidenceSend.psm1` へ publish の送信部を移し、publish をその呼び出しに置き換える（挙動不変を AC10 で固定）→ (4) `Invoke-EvidencePromote`（D1・D2）→ (5) `EvidenceCleanup` の D4・D5 → (6) fetch の D6 と inspect の D7 → (7) CLI → (8) README（導入手順を含む）と `review-evidence.md` → (9) `contract-audit`・`docs-audit`・AC13 の grep。
- 導入手順（README に書く。B は書くだけで実行しない）: 共有 store で promote を使い始める前に、`schedule status` で登録済み runtime の head を確認し、本スライスの implementation head で `schedule install` を行う。旧 runtime の日次 job は、`origin: stage` の `put-pending` を 404 で `failed` にし（本スライスの reader はこれを `corrupt-state` として読まない）、receipt 無しの `delete-pending` を `receipt-missing` で読めない。切替前に promote を使わない。
- 判定 C の実 R2 実行（AC12）はマージ前に共有 store で行うため、旧 runtime の日次 job（03:00 local と logon）と同じ task に触れない時間帯に、隔離した task で実行し、`passed` で終わらなければその場で本スライスの head の promote / fetch を再実行して `put-pending` を残さない。残った場合は日次 job の前に owner へ返す。
- Phase B から Phase A へ差し戻す条件: 新しい state・field、transport の変更、`PackageIO` の manifest 形式の変更、Workflow の終了意味の変更、`EvidenceSchedule` の変更が必要になったとき。publish の送信部を共有化すると既存 suite のケースを変えなければ通らないとき。D8 の組み合わせで既存の実 catalog の shape を読めなくなると判明したとき。
- 対象外を維持する方法: promote の経路に snapshot・package を作る関数（`Initialize-EvidencePackage`、`Copy-EvidenceSnapshot`、`PackageIO.CreateVerified`）の呼び出しを置かない（AC4 の呼び出し記録）。`staged` で始まる stage・fetch の経路に `Invoke-ArtifactNetwork` と `Get-CredentialStatus` を置かない（AC10）。Cloud・別 PC 向けの取得経路と token を作らない。`origin: publish` の分岐を変えない。

## 5. テストとレビュー計画

- 単体テスト: `Get-EvidenceRetentionDecision` と `Get-EvidenceDeletionRoute` の全組み合わせ（`EvidenceRetention.Tests.ps1`）、`Read-EvidenceTask` の D8（`ArtifactEvidence.Tests.ps1`）、putIntent 再利用の照合（Send の純粋部）。
- 差し戻し中の起点 `-Filter`: `tools/Artifacts/tests/ArtifactEvidence.Tests.ps1`、`EvidenceRetention.Tests.ps1`、`EvidenceCleanup.Tests.ps1 -Case <新規ケース>`。C は凍結済み条件の確認に必要なら根拠を記録して変えてよい。
- 判定必須テスト（GO 候補 head）: `tools/Artifacts/tests/*.Tests.ps1` と `tools/Workflow/tests/*.Tests.ps1` の全体（`StoreContext.Tests.ps1` は非仮想化 host で）、3 project の Release build、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC13 の旧文言 grep。`EvidenceCleanup.Tests.ps1` は `rg` が PATH に要る。高コスト証拠 AC12 は最終証拠ラウンドで加える。
- 全 EditMode 回帰の適用除外: Unity 側のコードを変更しないため。代替証拠は上記 offline suite 全体と audit。
- 統合・Unity テスト: なし。
- 操作・実行時・目視条件の検証経路: AC12 だけ（高コスト証拠）。動作確認（Phase B の後）では、B の出口 head で同じ手順を実 R2 に**実行しない**（高コスト証拠は動作確認の対象外）。動作確認の対象は `対象なし`。
- 未知の操作経路の疎通結果: なし。promote の PUT・readback GET・remote fetch は既存 publish・fetch と同じ transport・同じ key 形式・同じ資格情報を使う既知の経路。
- 人間の判断が必要な条件: なし。
- 高コスト証拠（AC12、実 R2 昇格 1 回）:
  - 担当: 判定 C の実行担当（判断担当と分ける場合は新しい実行担当 session）。環境: 非仮想化 host の sandbox 外 PowerShell 7（Claude の Bash は LocalAppData が仮想化されるので使わない）。GO 候補 head で 3 project を Release build。
  - 手順: (1) `pwsh tools/workflow-task.ps1 start -Task evidence-r2-promotion-rt-1`。(2) 非秘密の固定 fixture（3 file、うち 1 つはバイナリ）を selection にして `evidence stage`。出力の reference と packageSha256 を記録。(3) 別 process で `fetch`（local 分岐）。(4) `evidence promote --reference <stage の reference> --sha256 <stage の packageSha256>`。(5) `evidence inspect --task evidence-r2-promotion-rt-1`。(6) 別 process で `fetch`（remote 分岐）。(7) 同じ promote を再実行。(8) 原記録（各 CLI の結果 JSON、catalog・intent・receipt の写し、fixture の hash 一覧）を判定 evidence に収録し、task を通常の最終手順で end する（R2 object は task 終了 + 30 日の通常清掃が消す。事前承認の範囲）。
  - 実装から独立した期待値: (4) が `passed`、reference 文字列が (2) と完全一致、packageSha256 が (2) と一致。(5) の対象 artifact が `ready`・`locality: promoted`。(6) が `passed` で、展開した全 file の SHA-256 が fixture の hash 一覧と一致。receipt の `putIntentSha256` が intent file の SHA-256 と一致し、receipt の readback の hash が packageSha256 と一致。(7) が `passed`・`already-promoted`。catalog の artifactId・key・base・head・manifestSha256・packageBytes・entries が (2) 直後と (6) 後で一致。
- 判定 C の照合 script の方式: **判定 C の offline 実行**。判定 C の担当が、AC12 の原記録の複製に対して、資格情報・本番設定・network に届かない環境で照合 script を書いて実行する。適用前に、期待値どおりの合成 fixture（合格 1 組、reference 不一致・hash 不一致・`already-promoted` 欠落の不合格 3 組）で一度通す。合成 fixture と期待値は A3 で凍結する。
- 機械検査: `contract-audit`、`docs-audit`、`git diff --stat`、AC13 の grep。
- A0/A1 主担当・モデル・ベンダー: Claude（この session）。
- A2 独立レビュー（予定。互いの指摘を見せない。入力は A1 初稿の commit と内容 sha256。全て read-only、実ストア・資格情報・network・push 不使用。prompt には「計画は未実装で、現コードとの差は実装対象」と明記する）:
  - (a) アーキテクチャゲート（`architecture-gates.md`）: Codex CLI、GPT 系（OpenAI）。
  - (b) 不変条件 I1〜I8 を破る操作順序の構成（停止点・応答不明・404・遅延確定・清掃・終了・保護の組み合わせ）: Claude Opus 系（新規 session）。
  - (c) A0（§1 と §2 の問い・前提）だけからの代替構成: Claude Sonnet 系（新規 session、初稿の §2 方式以降を読まない）。
- A3 統合担当・モデル・採否: 主担当と owner。
- C' 用に予約した担当・モデル・ベンダー: Grok 系（cursor-agent）。A2 では使わない。
- 独立性の強化条件を満たせない場合の理由: (b)(c) は A1 主担当と同じ Claude 系。初稿への anchoring は (c) に初稿の方式を読ませないことで避ける。

## 5.1 A2 独立レビュー結果と A3 の採否案

未実施。

## 6. Phase B 実装結果

- 実装:
- HANDOFF との差:
- 未実行:
- implementation head commit:
- Phase B 担当・モデル・ベンダー:

### 動作確認

- 対象 head:
- 条件ごとの実行担当・環境・実行したコマンド:
- 生の観測と保存先・状態:
- 検証経路の期待と違う観測と、B への差し戻し:
- `未実施` の条件と理由・担当: 対象なし（§5）

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
