# Artifact Storage / Evidence 昇格の回復と送信済み staged の寿命 HANDOFF

## 0. メタデータ

- type: `slice`
- status: `A`（A1 初稿。A2 の入力版）
- branch: `claude/evidence-promote-recovery`（PR の base は `develop`）
- implementation base commit: `498d0da8f96ef324772d27596c464456f3fad370`（A1 時点の `origin/develop`、PR #130 マージ後。A3 で確定する）
- implementation head commit: 未固定
- risk: `high`（catalog の state 遷移、PUT 応答不明・削除の fail-closed 契約、保持 policy と削除経路の振り分けを同時に変える。同じ key への 2 回目の PUT を許す）
- owner: OSM maintainers
- created: 2026-10-17
- expires: 2026-11-17 または置換 revision
- harvest to: 操作・回復規則・残存リスク・導入手順を `tools/Artifacts/README.md`（「R2への昇格（evidence promote）」「終了・再開・利用中と清掃」「自動清掃」）へ。`.agents/skills/osm-workflow/references/review-evidence.md` の「保存先とリモートへの配布」の `failed` の扱い（本スライスの実装対象）。`docs/handoff/BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §4 C に「回復と寿命は完了」と後続入力を記録する。
- Phase A snapshot の取得案内・状態: 未保存（本 HANDOFF の commit を A2 の入力版とする。内容 sha256 は §5 の A2 欄に記録する）
- Phase B result snapshot / evidence bundle / C' blind bundle の取得案内・状態: 未生成（未到達 Phase）
- H1 / CURRENT: 採用しない（tracked HANDOFF。前スライスと同じ）
- 引継ぎ票: 次は **A2（独立レビュー）**。入力は本 HANDOFF の A1 初稿の commit（§5 の A2 欄に SHA と内容 sha256）。観点は §5 の A2 欄。各レビューは互いの指摘を見ない。push はしない。引き渡さないもの: なし。

## 1. 目的と対象外

- 目的: PR #130 で入った昇格（`evidence promote`）は、PUT 応答不明の後に回復 GET が 404 を返すと artifact を `failed` で凍結し、取得も再昇格も削除もできない（README の既知の制限）。本スライスは、`origin: stage` の昇格に限って、404 の後を `staged`（putIntent 保持）に戻し、同じ bytes の再昇格と local fetch を可能にする。同時に、送信を試みた可能性のある `staged`（putIntent を持つもの）を、ローカル削除ではなく remote DELETE と GET 404 の確認を経る削除経路に載せ、満了で片付くようにする。publish 由来の artifact の挙動は変えない。
- 現況（2026-10-17、`origin/develop` = `498d0da8`）:
  - promote（[EvidenceApplication.psm1](../../tools/Artifacts/EvidenceApplication.psm1) `Invoke-EvidencePromote` / `Send-EvidenceStagedPackage`）は、`staged`・`origin: stage`・putIntent と receipt が null・task が `active` の artifact だけに、1 回の呼び出しで最大 1 回 PUT する。書込み A（intent の参照・`put-pending`・readback staging・artifact の `operation`）→ PUT → 別 process の readback → receipt file → 書込み B（receipt・`ready`・`ownedCopies` 追記・自分の readback staging だけ adopted）。intent file が正規 path に既に在り catalog に未記録なら、field を照合して再利用する（`Confirm-EvidencePromoteIntent`）。putIntent を持つ `staged` は `invalid-source`。
  - 回復（[EvidenceCleanup.psm1](../../tools/Artifacts/EvidenceCleanup.psm1) `Resolve-EvidencePutGuarded`）は origin を見ない。新しい recovery staging を登録して GET する。一致なら receipt を書く（既存 receipt は key・packageSha256・manifestSha256 の 3 つで照合して再利用）→ recovery staging の file を `ownedCopies` に追記 → recovery staging と**同じ artifactId の全 staging** を adopted・operation null にする → `ready`。404 なら recovery staging の operation だけを外し、artifact を `failed`。それ以外は `unresolved-put`。
  - 回復を呼ぶのは network 付きの preflight（`Assert-EvidenceTransitionSafe`。publish・promote・use と、preflight 前の catalog で対象が `staged` でない fetch）と cleanup の各 artifact の処理で、対象は `put-pending` だけ。`failed` はどこからも回復されない。network 無しの preflight（stage と、呼び出し開始時に `staged` の artifact の fetch）は、task 内の `put-pending` と削除 intent を `unresolved-put` / `unresolved-delete` で拒否し、`failed` には触れない。
  - 保持 policy（[EvidenceRetentionPolicy.psm1](../../tools/Artifacts/EvidenceRetentionPolicy.psm1)）: `deleted` → `payload-deleted`、`ready` / `staged` 以外 → `unconfirmed-artifact`（`failed` を含む。削除しない）、putIntent も receipt も null の `origin: stage` 以外の `staged` → `unknown-state`、以降は期限・保護の判定。
  - 削除経路（`Invoke-EvidenceCleanup` の 332 行目）: 保持判定が `expired` なら、state が `staged` → ローカル削除（`Invoke-EvidenceLocalDeleteGuarded`、network 無し）、それ以外 → remote 削除（`Invoke-EvidenceDeleteGuarded`: deleteIntent・`delete-pending` → DELETE → GET 404 `NoSuchKey` → `deleted`）。remote 削除の照合 GET（`Resolve-EvidenceDeleteGuarded`）が一致すれば state を `ready` に戻す。
  - catalog の読戻し（[EvidenceStateStore.psm1](../../tools/Artifacts/EvidenceStateStore.psm1) `Read-EvidenceTask`）は、`ready`・`delete-pending`（origin を問わない）と、origin が `stage` 以外の `deleted` に receipt を要求する（無ければ `receipt-missing`）。origin と state の組み合わせは他に制限しない。
  - 清掃は `staged` または `put-pending` の artifact を参照する staging を 7 日清掃から一律に除外する（`Invoke-EvidenceCleanup` の 295 行目）。
  - fetch は preflight 前の catalog で対象が `staged` なら network 無しの preflight と local 分岐、それ以外は network 付き preflight と remote 分岐を選び、preflight 後に選び直さない。ローカル固有の reasonCode（`changed-copy`・`unresolved-put`・`unresolved-delete`）は local 分岐のときだけ返す。
  - `use` / `release` は `ready` と `staged` を受け付ける。inspect の `locality` は `staged` → `local`、`ready` → `promoted`。
  - 登録済みの日次 job の runtime は、`schedule install` を行った head に固定される（README「自動清掃」）。
- 前スライスからの入力（program §4 C「R2昇格の送信」、削除前の HANDOFF `docs/handoff/ARTIFACT_EVIDENCE_R2_PROMOTION.md`）:
  - revision 2（commit `8494c791`）の D3（同じ bytes の再送の許可と publish の再送禁止との区別）、D4 (i)(ii)、D5、D6、D8、D11、AC5・AC7・AC8・AC9・AC11・AC14・AC15、既存ケース `staged-unsent-required` の 1 行目の変更。A3 裁定 (1)（404 後の `staged` と再送）・(2)（削除後の遅延 PUT の受容）・(5)（実 R2 の前の `schedule install`）は再確認の対象。
  - 制約: (C1) 回復（404 → `staged`）と保持 policy・削除経路・清掃の振り分けを同じスライス・同じ commit に置く。(C2) `origin: stage` の `failed`（putIntent あり・receipt 無し）を `corrupt-state` にしない。(C3) 回復一致時の adopted の規則を変えるなら、publish 由来の staging を巻き込まない（origin で分けるか「今回の回復に使った staging だけ」に言い換える）。
  - 既知の残り（後続入力）: receipt 再利用の照合が 3 field に限られる点、回復一致時に promote の readback staging が未登録 file ごと adopted になる点、書込み A の後に止まり 404 の場合に promote の readback staging が `operation` を持ったまま残る点、送信部の共有（`EvidenceSend.psm1`）、`createdAt`・`process` の型検査。
- 対象外: publish 由来（`origin: publish` と origin の無い artifact）の回復・保持・削除・reasonCode。送信部の共有（`EvidenceSend.psm1`）。receipt 再利用の照合の拡張。catalog の組み合わせ検査の厳格化（revision 2 の D8 のうち、本スライスの shape を読むための緩和以外。§2 R7）。条件付き PUT など transport の変更。Cloud・別 PC からの取得経路と token（program §4 C）。`ready` の local copy からの network 無し fetch。`use` の network 無し preflight。端末喪失に備えた未昇格 `staged` の backup（Harness H3）。Unity 側の変更なし。

## 2. 意思決定と受け入れ境界（A1 初稿）

- このスライスが答える問い: 「昇格由来（`origin: stage`）の `put-pending` と `failed` が、回復 GET の 404 の後に `staged`（putIntent 保持）へ戻って、local fetch と同じ bytes の再昇格ができ、送信を試みた可能性のある `staged` が remote DELETE と GET 404 の確認を経てだけ `deleted` になるか。publish 由来の挙動を変えずに」。
- A0 で固定した前提: 範囲は昇格由来の回復と寿命だけ（program §4 C の後続スライス、2026-10-17）。前スライスの制約 C1〜C3 を守る。
- 方式:
  - **R1 回復 GET の 404 → `staged`（`origin: stage` のみ）。** `Resolve-EvidencePutGuarded` は、対象が `origin: stage` のとき、GET 404 で state を `staged` に戻す。`putIntent`・`ownedCopies`・`protections` と identity の全 field はそのまま残す。artifact の `operation` を外す。同じ artifactId の staging のうち `operation` を持つものは、`Assert-EvidenceOperationStopped` を通ったものだけ `operation` を外し（通らなければ `operation-pending` で止まり、何も書かない）、`adopted` は変えない。`origin: publish` は既存どおり 404 → `failed`。
    - 戻した `staged` の staging は、artifact が `staged` の間は既存の除外（295 行目）で 7 日清掃に乗らない。artifact が `ready` または `deleted` になった後に、未採用のものが通常の 7 日清掃に乗る。
  - **R2 回復 GET 一致時の adopted（`origin: stage` のみ）。** `origin: stage` のとき、adopted にするのは今回の回復で登録した recovery staging（中の file を全て `ownedCopies` に追記したもの）だけにする。同じ artifactId の他の staging は、`operation` を外すだけで `adopted` を変えない（stage の元の staging は既に adopted のまま、停止した promote の readback staging は未採用のまま 7 日清掃に乗る）。`origin: publish` は既存どおり同じ artifactId の全 staging を adopted にする（C3）。receipt の書込み・再利用は既存のまま。
  - **R3 `failed` の回復（`origin: stage`・putIntent あり・receipt 無しのみ）。** network 付きの preflight と cleanup は、`put-pending` に加えてこの `failed` も `Resolve-EvidencePutGuarded` に渡す（PR #130 の runtime と、本スライスより前の日次 job が回復の 404 で作る shape）。結果は R1・R2 と同じ（一致 → `ready`、404 → `staged`、それ以外 → `unresolved-put` で state を変えない）。それ以外の `failed`（publish 由来、putIntent 無し、receipt あり）は既存どおり触れない。
    - network 無しの preflight はこの `failed` に触れず、拒否もしない（今の stage・local fetch は同じ task に `failed` があっても成功しており、その挙動を変えない）。revision 2 の D4 は「`unresolved-put` で拒否」だったが、`failed` は凍結・非削除で他の artifact に影響しないので、拒否する理由が無い。
  - **R4 再昇格（同じ bytes の 2 回目の PUT）。** promote の送信条件（D1 手順 6）を、`staged`・`origin: stage`・receipt が null・task が `active`・identity と期待 hash が一致、かつ「putIntent が null」または「putIntent が在り、正規 path の intent file の SHA-256 が `putIntent.sha256` と一致し、`Confirm-EvidencePromoteIntent` の field 照合を通る」に広げる。intent file は作り直さず、`createdAt`・`process` も前回の値のまま使う。照合に失敗したら `unresolved-put`、PUT 0 件。以降の書込み A・PUT・readback・書込み B は既存の `Send-EvidenceStagedPackage` のまま（1 回の呼び出しで最大 1 回の PUT、PUT 応答不明は `put-pending` のまま再送しない）。
    - 根拠（revision 2 の D3）: 送る bytes は catalog の packageSha256 と lock 下で照合した ZIP に固定され、R2 の PUT は object 全体の置換なので、先行 PUT が後から確定しても後続と同じ内容になる。保証するのは「その key の object は、存在するなら packageSha256 の bytes である」ことと「`ready` は別 process の readback 一致の後にしか書かない」ことで、「同じ key への PUT が通算 1 回」は保証しない。publish の「PUT 応答不明を同 key へ再送しない」は毎回別の snapshot を作る publish の契約で、変えない。
    - receipt file が正規 path に既に在る（前回の試行が receipt file を書いた後に止まり、その後 object が消えて 404 → `staged` に戻った）場合、再昇格は immutable な receipt の書込みで失敗し `put-pending` のまま残る。次の回復の GET 一致が既存 receipt を照合して再利用し `ready` に収束するので、本スライスでは直さない（前スライスの「優先度の低い入力」のまま）。
  - **R5 保持 policy。** `Get-EvidenceRetentionDecision` は、`staged`・`origin: stage`・putIntent あり・receipt 無しを、`unknown-state` にせず `ready` と同じ期限・保護の判定にする。receipt を持つ `staged`、publish 由来の `staged`、origin の無い `staged` は既存どおり `unknown-state`。削除経路は持たない（R6）。
  - **R6 削除経路の振り分け。** `EvidenceCleanup.psm1` に副作用の無い関数 `Get-EvidenceDeletionRoute` を置く: `local` = `staged`・`origin: stage`・putIntent 無し・receipt 無し。`remote` = `ready`、または `staged`・`origin: stage`・putIntent あり・receipt 無し。それ以外は `none`。cleanup の 332 行目はこの関数で振り分け、`none` は削除しない（`unknown-state` として blocked に数える）。`Invoke-EvidenceLocalDeleteGuarded` も自分の中で経路が `local` であることを再確認し、違えば何も書かずに `unknown-state` を返す。
    - putIntent を持つ `staged` の remote 削除は既存の `Invoke-EvidenceDeleteGuarded`（deleteIntent・`delete-pending` → DELETE → GET 404 → `deleted` → `ownedCopies` の削除）。照合 GET（`Resolve-EvidenceDeleteGuarded`）が一致した場合、receipt を持つ artifact は既存どおり `ready`、receipt を持たない artifact は `staged` に戻す（次回の清掃が保持判定からやり直す）。
    - 残存リスク（受容。README に書き、保証とは書かない。revision 2 の A3 裁定 (2) の再確認）: 削除の後に、先行 PUT が遅れて確定すると、R2 に追跡外の object が残る。全ての PUT は task が `active` の間にだけ発行され、remote 削除は task 終了 + 30 日以後にだけ起きるので、30 日以上未完了の HTTP 要求を要する。有限回の追加確認ではこのリスクを消せないので、削除後の追加確認は置かない。
  - **R7 catalog の読戻し。** `Read-EvidenceTask` は、`origin: stage` の `delete-pending` に限り、receipt が null でも putIntent を持てば読む（R6 で receipt を持たない artifact が `delete-pending` を通る）。putIntent も receipt も無い `origin: stage` の `delete-pending` は既存どおり `receipt-missing`。他の組み合わせの検査は変えない（revision 2 の D8 の厳格化は後続。C2 により `origin: stage` の `failed` は読めるまま）。
  - **R8 fetch の取得元を preflight の後に決める。** preflight に network を使うかは、既存どおり preflight 前の catalog で決める（対象が `staged` なら network 無し）。取得元は preflight と Sync の後の state で決める: `staged` → local 分岐、`ready` → remote 分岐、それ以外 → 既存の拒否（`identity-mismatch` 等）。昇格由来の `put-pending` / `failed` は network 付き preflight の回復で `ready` か `staged` になり、その取得元で取得される。ローカル固有の reasonCode は、preflight 前の判定か最終の取得元のどちらかが local のときに返す。失敗時に他方の取得元へ fallback しない。network 無しの保証は「呼び出し開始時に `staged` の artifact」に限る（既存の README の文言どおり）。
  - **R9 ワークフロー文書。** `review-evidence.md` の「保存先とリモートへの配布」の「昇格が `failed` で止まった場合は、その artifact を remote 用に使わず、従来の `evidence publish` で新しい artifact を作り」を、「昇格が応答不明で止まった場合は、次の network を使う操作（fetch・promote・日次清掃）が回復し、`ready` になればそのまま、`staged` に戻れば同じ `evidence promote` を再実行する。同じ reference と hash のまま使える」に置き換える。`SKILL.md`・`phases-and-handoff.md`・`handoff-template.md` は変えない。
  - **R10 README と導入手順。** 「R2への昇格」の「PUT 応答不明」「既知の制限」を R1〜R4・R8 と残存リスク（R6）に置き換え、「終了・再開・利用中と清掃」に putIntent を持つ `staged` の削除経路（R5・R6）を書く。「Evidence v2 の通常操作」の再送禁止は publish の契約であり、promote は R4 に従うと書く。導入手順: 共有 store で本スライスの shape（putIntent を持つ `staged`、receipt の無い `delete-pending`）が生じる前に、`schedule status` で登録済み runtime の head を確認し、本スライスの head で `schedule install` を行う（登録済み task の切替は既存の手順）。
    - 旧 runtime（本スライスより前の head）と新しい shape の関係: putIntent を持つ `staged` は、旧 reader で読め、旧 policy で `unknown-state`（削除しない）、旧 cleanup はその staging を除外する。receipt の無い `delete-pending` は旧 reader で `receipt-missing` になり、旧 runtime ではその task 全体の清掃と操作が止まる（削除は起きない）。後者は新しい runtime の cleanup が putIntent を持つ `staged` を削除し始めて初めて生じる。どちらも fail-closed で、データを失わない。
    - 判定 C の実 R2 検証（AC12）は putIntent を持つ `staged` を経由するが、削除まで進めない（R6 の実削除は offline のみ）ので、旧 runtime の日次 job が同じ task を見ても削除・停止は起きない。したがって revision 2 の A3 裁定 (5)（AC12 の直前の `schedule install`）は不要とする（共有 store の登録済み runtime を判定のために変えない）。
- 却下した代替案:
  - 新 state `promote-pending` で 404 の後も保持し、再送を禁じる（前スライスの A2 (c)）: 404 の後は遅れた PUT が確定しない限り永久に `ready` に達せず、同じ reference のまま昇格する目的を満たせない。新 state は reader・保持・清掃・fetch・use の全分岐の網羅を要する（前スライスの A3 裁定 (1)）。
  - 404 の後に不在確認の時刻を記録してローカル削除を許す（旧 HANDOFF の D4'）: 遅れて確定した object が追跡外に残る（ADD-01）。
  - 回復一致時の「中の file が全て `ownedCopies` に入った staging だけを adopted」を origin を問わず入れる: publish 由来の staging（`readback-process.json` 等は `ownedCopies` 外）が adopted から外れ、7 日清掃で消えて task が `changed-copy` で止まる（C3）。
  - network 無しの preflight で `origin: stage` の `failed` を `unresolved-put` で拒否する（revision 2 の D4）: 今の stage・local fetch の成功を、同じ task の無関係な `failed` を理由に止める既存挙動の変更になる。R3。
  - 削除前に putIntent の process の停止を確認する（前スライスの A2 #20）: `staged` へ戻す回復が既に artifact の `operation` の停止を確認している。前スライスで不採用で閉じた。
  - catalog の組み合わせ検査を全面的に厳格化する（revision 2 の D8）: 本スライスの問いに不要で、既存の実 catalog の shape を読めなくする危険を増やす。必要な緩和（R7）だけを行う。
- 進める最低条件:
  - 不変条件（起きてはいけないこと）:
    - **I1** publish 由来（`origin: publish` と origin の無い artifact）の回復・保持判定・削除経路・adopted・reasonCode が変わる。既存テストのうち、`EvidenceRetention.Tests.ps1` のケース `staged-unsent-required` の 1 行目（putIntent を持つ `staged` は `unknown-state`）以外が変わる。
    - **I2** putIntent を持つ artifact（PUT を試みた可能性があるもの）が、remote DELETE と GET 404（`NoSuchKey`）の確認を経ずに `deleted` になる。ローカル削除の経路を通る。
    - **I3** promote が、R4 の条件を満たさない artifact（`put-pending`、`failed`、receipt を持つもの、publish 由来、終了済み task、identity・期待 hash の不一致、putIntent と intent file の不一致）に PUT を発行する。1 回の呼び出しが 2 回以上 PUT を発行する。catalog の packageSha256・packageBytes と一致しない bytes を PUT する。再 snapshot・再 package が起きる。
    - **I4** 回復・再昇格の前後で、reference 文字列・artifactId・key・packageSha256・manifestSha256・base・head・packageBytes・entries・taskEndedAt・deleteEligibleAt・protections、または putIntent の参照と intent file の SHA-256 が変わる。
    - **I5** 別 process の readback で bytes・hash の一致を確認する前に、`ready` または receipt が catalog に記録される。
    - **I6** 404 で `staged` に戻った artifact が、昇格前の `ownedCopies` のいずれかを失う（確認済みの削除以外で）。`origin: stage` の回復一致で、中の file が `ownedCopies` に無い staging が新たに adopted になる。停止を確認していない process の `operation` が外される。
    - **I7** 保護中（有効な `use`）または `deleteEligibleAt` より前の artifact が、回復・再昇格・清掃で削除される。
    - **I8** 呼び出し開始時に `staged` の artifact への stage・fetch が network・資格情報を使う。fetch が local と remote の間で失敗時に fallback する。fetch が、自分の preflight で state が変わったことを理由に、取得できる artifact を拒否する。
    - **I9** PR #130 の runtime が作る catalog の shape（`origin: stage` の `staged`（putIntent 無し）・`put-pending`・`ready`・`failed`（putIntent あり）・`delete-pending`（receipt あり）・`deleted`）のどれかが、新しい reader で `corrupt-state` / `receipt-missing` / `receipt-conflict` になる。
  - 成立条件（起きなければならないこと）:
    - **S1** 昇格由来の `put-pending` が回復 GET 404 で `staged`（putIntent 保持）に戻り、続く local fetch が成功し、promote の再実行が同じ intent を再利用して PUT 1 回で `ready` になり、remote fetch が全 entry 一致で成功する。
    - **S2** 昇格由来の `failed`（putIntent あり・receipt 無し）が、network 付きの preflight または cleanup で、GET 404 なら `staged`、GET 一致なら `ready` になる。
    - **S3** putIntent を持つ `staged` が満了で remote 削除の経路を通って `deleted` になり、その後も `Read-EvidenceTask` が通る。putIntent の無い `staged` は既存どおりローカル削除される。
    - **S4** 昇格由来の `put-pending` / `failed` の fetch が、preflight の回復の後の state に合った取得元で成功する。
    - **S5** 実 R2 で、PUT 前に止めた昇格の回復 GET が 404 を返して `staged` に戻り、再昇格と別 process の remote fetch が全 entry 一致で成功する（判定必須。§5）。
- 受け入れ条件（検証例。offline は隔離した temp store・偽 transport・時刻注入。実ストア・network 不使用。実 R2 は AC12 だけ）:
  - **AC1**（S1, I3, I4, I6）: fixture の `staged` を FaultHook `promote-after-put-pending` で止める（`put-pending`、PUT 0 件）。fetch（preflight 前は `put-pending` なので network 付き）の回復で偽 GET 404 → `staged`。disk から読み直した catalog で、putIntent の参照と intent file の SHA-256 が停止前と同じ、I4 の全 field が stage 直後と同じ、`ownedCopies` が stage 直後の集合を全て含む、artifact と promote の readback staging の `operation` が null、readback staging の `adopted` が false。その fetch は local 分岐で成功する。続く 2 回目の fetch は network 無し（transport 呼び出し 0 件・資格情報の読取り 0 件）で成功する。promote の再実行は PUT 1 回で `ready`、intent file は作り直されず（SHA-256 不変）、receipt の `putIntentSha256` が同じ値。remote fetch が成功する。不合格例: 再昇格で intent file が新しく書かれる、2 回目の fetch が network 付き preflight を通る。
  - **AC2**（S1, I3）: 偽 transport で PUT の応答を失わせる → `put-pending`、その呼び出しの PUT は 1 回。回復の偽 GET 404 → `staged` → 再 promote の PUT 1 回で `ready`。通算 PUT 2 回、各呼び出しで最大 1 回（不合格例: 応答を失った呼び出しの中で 2 回目の PUT）。
  - **AC3**（S2, I9）: PR #130 の runtime が作る `failed` の shape（`origin: stage`・putIntent あり・receipt 無し・artifact と staging の `operation` null）を、AC1 の停止の後に `Resolve-EvidencePutGuarded` の旧挙動と同じ書込み（state `failed`、recovery staging の operation 解除）で作る。(a) network 付き preflight（promote・use・fetch）で偽 GET 404 → `staged`。(b) GET 一致 → `ready`（receipt あり、`ownedCopies` に recovery staging の file が追記）。(c) GET が別 bytes → `unresolved-put`、state は `failed` のまま。(d) cleanup が同じ 3 終点を作る。(e) 同じ task の別 artifact の stage と local fetch が、この `failed` があっても成功し、`failed` は変わらない（transport 呼び出し 0 件）。publish 由来の `failed`、putIntent 無しの `failed` は (a)〜(d) のどれでも変わらない。
  - **AC4**（I1）: `origin: publish` の `put-pending` で偽 GET 404 → `failed`、GET 一致 → 同じ artifactId の全 staging が adopted（既存挙動）。origin の無い `staged` と publish 由来の `staged` は保持判定 `unknown-state`・削除経路 `none` で、DELETE・ローカル削除とも 0 件。既存の `tools/Artifacts/tests/*.Tests.ps1` と `tools/Workflow/tests/*.Tests.ps1` が failed 0 で、既存テスト file の変更は `staged-unsent-required` の 1 行目の期待（`unknown-state` → `retained` / `expired` 系の判定）だけ。同じケースの残り 3 行は無変更。
  - **AC5**（I6）: `origin: stage` の回復一致で、adopted に変わる staging は今回の recovery staging だけ。停止した promote の readback staging（中に `readback-process.json` 等の未登録 file）は adopted=false・operation null のまま残り、時刻注入で 7 日後の cleanup がそれを削除する（artifact は `ready`）。stage の元の staging は adopted のまま、その中の ZIP は `ownedCopies` に残る。不合格例: readback staging が adopted になり、artifact 削除後もディレクトリが残る。
  - **AC6**（S3, I2, I7）: 時刻注入。putIntent を持つ `staged` は `deleteEligibleAt` の 1 tick 前に `retained`、ちょうどで remote 削除（deleteIntent → DELETE → GET 404）され `deleted`、ZIP と `ownedCopies` が消え、その後も `Read-EvidenceTask` が通る（不合格例: DELETE 0 件で `deleted`）。DELETE を失敗させると `delete-pending`（receipt 無し・putIntent あり）で残り、`Read-EvidenceTask` が通り、ZIP は残る。続く照合 GET が一致すれば `staged` に戻り、ZIP と putIntent が残る。照合 GET 404 なら `deleted`。putIntent も receipt も無い `staged` は既存どおりローカル削除（transport 呼び出し 0 件）。`Get-EvidenceDeletionRoute` は state × origin（`stage` / `publish` / 無し）× putIntent の有無 × receipt の有無の全組で表どおりの値を返す（単体テスト）。`Invoke-EvidenceLocalDeleteGuarded` に putIntent を持つ `staged` を直接渡すと何も書かず `unknown-state`。
  - **AC7**（I7）: active な task で stage → `use`（until = 終了予定 + 30 日 + 1 時間）→ AC1 の停止と回復 404 → `staged` で protection が残る → 時刻 `T` の ended event を `workflow-task end` の経路で適用 → 時計を `T + 30 日` の直後（until より前）へ進めた cleanup が `in-use`・DELETE 0 件 → `release` の後、同じ時計で remote 削除に進む。不合格例: active のまま時計だけ進めて `active/end-not-recorded` を `in-use` と取り違える。
  - **AC8**（S4, I8）: 昇格由来の `put-pending` の fetch。偽 GET 一致 → `ready` → remote 分岐で成功。GET 404 → `staged` → local 分岐で成功（`identity-mismatch` を返さない）。GET が別 bytes → 既存の `unresolved-put` 系の拒否で、local 分岐へ fallback しない。昇格由来の `failed` の fetch も同じ 3 通り。呼び出し開始時に `staged` の artifact の fetch と stage は、既存ケースどおり transport と資格情報の読取りが 0 件。
  - **AC9**（I9）: PR #130 の runtime が作る 6 shape（I9 の列挙。`delete-pending` は receipt ありの既存 shape）を持つ catalog と、origin の無い既存 catalog を新しい `Read-EvidenceTask` が読む。`origin: stage` の `delete-pending` で putIntent も receipt も無いものは既存どおり `receipt-missing`。
  - **AC10**（I3）: 再昇格の拒否。putIntent を持つ `staged` で、(a) intent file の SHA-256 が `putIntent.sha256` と違う、(b) intent file の key・packageSha256・artifactId・packageBytes・schemaVersion のどれかが違う、(c) intent file が無い、のそれぞれで promote は PUT 0 件（preflight の既存検査が先に拒否する場合はその既存の reasonCode、そうでなければ `unresolved-put`）。receipt を持つ `staged`、終了済み task の putIntent を持つ `staged`（`resume-required`）も PUT 0 件。
  - **AC11**（I5）: 再昇格の経路で、偽 transport の PUT 呼び出しの中で disk から読み直した catalog が `put-pending`・putIntent あり（同じ SHA-256）であること、readback の子 process が一致を返すまで disk に receipt と `ready` が無いこと（前スライスの AC12 と同じ方法）。readback を失敗させると `put-pending` のまま receipt 無し。
  - **AC12**（S5、判定必須）: §5 の手順で実 R2 に 1 回。
  - **AC13**（R9, R10）: README・`review-evidence.md` に再昇格の操作、R1〜R4・R6・R8 の規則、publish 側の再送禁止との区別、残存リスク、導入手順が書かれ、旧文言（README の「`failed` で止まった昇格は、そのartifactからR2経由の取得も再昇格もできません」と「回復は後続スライス」、`review-evidence.md` の「昇格が `failed` で止まった場合は、そのartifactをremote用に使わず」）が `tools/Artifacts/README.md` と `.agents/skills/osm-workflow/` に残らない。`docs-audit`・`contract-audit` が errors=0。
- ここでは答えない問いと所有する後続スライス:
  - catalog の組み合わせ検査の厳格化（revision 2 の D8 の残り）→ 診断の改善の入力（所有スライス未定）。
  - receipt 再利用の照合の拡張（3 field → 全 identity）、前回の試行が receipt file を書いた後に object が消えた場合の再昇格の失敗（R4。回復で自己回復する）→ 送信まわりの整理の入力。
  - 送信部の共有（`EvidenceSend.psm1`）と intent の `createdAt`・`process` の型検査 → 同上。
  - publish 由来の回復一致時の一律 adopted と `ownedCopies` 置換、publish の 404 → `failed` の residue 回収 → 診断の改善 / Content の容量上限・孤児回収の入力。
  - Cloud・別 PC が R2 から fetch する経路と token → program §4 C（`Artifact Storage / Evidence の Cloud 配布`）。
  - `ready` の local copy からの network 無し fetch → program §4 A の後続。`use` の network 無し preflight → program §4 C の入力。
  - 端末喪失に備えた未昇格 `staged` の backup → Harness H3。
- 判定定義: GO = I1〜I9 を破る欠陥がなく、S1〜S4 が offline fixture と文書検査で成立し、S5 の実 R2 検証 1 回が判定必須テストとして成立し、判定 C と C' がともに阻害 0。NO-GO = いずれかの不変条件の違反、または S の不成立。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし。
- 決定記録: なし。
- 本文へ転記した実装制約: `#nullable` と `record` の規則は Unity 側のみで本スライスには当たらない。PowerShell は `Set-StrictMode -Version Latest`。reasonCode は既存の小文字 kebab で、新しい code を足さない。path・key・資格情報を CLI 出力とログに出さない。テストで `Start-Sleep` / `Task.Delay` / `Thread.Sleep` を使わず時刻と transport を注入する。catalog に新しい state・新しい field を足さない。transport・`EvidenceContract.psm1`・`EvidenceSchedule.psm1`・`EvidenceReset.psm1` を変えない。資格情報の root・暗号文・DPAPI の利用を変えない。実ストアの catalog・transfers・資格情報を offline テストで読み書きしない。publish の関数（`Invoke-EvidencePublish`・`Initialize-EvidencePackage`）を変えない。
- 未決事項（A2・A3 で確かめる）:
  1. R3 で network 無しの preflight が `origin: stage` の `failed` を拒否しない判断（revision 2 の D4 からの変更）。
  2. R10 で revision 2 の A3 裁定 (5)（実 R2 の前の `schedule install`）を不要とする判断。
  3. AC12 の手順 (3) の、CLI ではなく module を直接呼んで FaultHook で止める実行方法の実行可能性（§5 の未知の操作経路）。
  4. R6 の残存リスク（削除後の遅延 PUT）の受容を、revision 2 の A3 裁定 (2) のまま維持してよいか。

## 3. 責務マップ

| ファイル | 責務 | 変更理由 | 現在行数 → 予想 | 備考 |
|---|---|---|---|---|
| `tools/Artifacts/EvidenceCleanup.psm1` | preflight、put / delete の回復、清掃 | R1・R2（`Resolve-EvidencePutGuarded` の origin 分岐）、R3（`Assert-EvidenceTransitionSafe` と cleanup が `origin: stage` の `failed` を回復へ渡す）、R6（`Get-EvidenceDeletionRoute`、cleanup の振り分け、ローカル削除内の再確認、照合 GET 一致 → `staged`） | 369 → 約 405 | 500 行未満。経路の選択は副作用の無い関数として単体テストする。Application を import しない |
| `tools/Artifacts/EvidenceApplication.psm1` | publish / stage / promote / fetch / inspect / use の orchestration | R4（promote の送信条件と intent 再利用の照合）、R8（fetch の取得元） | 316 → 約 330 | publish・stage・inspect・use・release は変えない |
| `tools/Artifacts/EvidenceRetentionPolicy.psm1` | 純粋な保持 policy | R5 | 68 → 約 70 | 削除経路は持たない |
| `tools/Artifacts/EvidenceStateStore.psm1` | catalog の読戻し検査 | R7 の 1 条件 | 144 → 約 145 | 新 state・新 field なし |
| `tools/Artifacts/README.md` | 公開仕様 | R10 | 460 → 約 475 | |
| `.agents/skills/osm-workflow/references/review-evidence.md` | レビュー証拠の保存・配布 | R9 | 172 → 172 | 置換のみ |
| `tools/Artifacts/tests/ArtifactEvidence.Tests.ps1` | promote・fetch の offline 検証 | AC1〜AC3・AC7・AC8・AC10・AC11 | 499 → 約 600 | 500 行の警報を超える。ケースの列挙で、変更理由・依存（偽 transport・FaultHook・隔離 store）・テスト方法が既存の promote ケースと同じなので非分割 |
| `tools/Artifacts/tests/EvidenceCleanup.Tests.ps1` | 回復・清掃の offline 検証 | AC3 (d)・AC4・AC5・AC6・AC9 | 505 → 約 570 | 同上の理由で非分割 |
| `tools/Artifacts/tests/EvidenceRetention.Tests.ps1` | 保持 policy の単体テスト | R5、`staged-unsent-required` の 1 行目 | 88 → 約 95 | |

状態の所有者は task の catalog（task guard が直列化）。寿命は既存の task 終了 + 30 日と staging の 7 日。新しい公開面は無い（CLI の grammar・reasonCode・出力の形は変えない）。新しい file は無い。asmdef・Unity 側の変更なし。`EvidenceSchedule` の runtime 収集は変えない。

## 4. 実装計画

- 順序: (1) `EvidenceStateStore` の R7 と AC9 → (2) `EvidenceRetentionPolicy` の R5 と `staged-unsent-required` の 1 行目 → (3) `EvidenceCleanup` の `Get-EvidenceDeletionRoute` と R6 の振り分け・照合 GET → (4) `Resolve-EvidencePutGuarded` の R1・R2 と R3 の呼び出し側 → (5) promote の R4 → (6) fetch の R8 → (7) README（R10）と `review-evidence.md`（R9）→ (8) `contract-audit`・`docs-audit`・AC13 の grep。(2) と (3) は C1 により同じ commit に入れる（(2) だけでは putIntent を持つ `staged` がローカル削除に乗る）。(4) も同じ commit に入れる（(4) だけでは putIntent を持つ `staged` が `unknown-state` のまま削除不能になる）。
- 導入手順（R10。README に書く。B は書くだけで実行しない）: 本スライスの head で `schedule install`（既存の切替手順）。
- Phase B から Phase A へ差し戻す条件: 新しい state・field・reasonCode、transport の変更、publish の関数の変更、`EvidenceContract` / `EvidenceSchedule` / `EvidenceReset` の変更が必要になったとき。AC4 で列挙した 1 行以外の既存テストを変えなければ通らないとき。I9 の shape のどれかを読めなくしないと R7 を実装できないとき。`Resolve-EvidencePutGuarded` の origin 分岐で、publish 由来の経路の挙動を変えずに R1・R2 を実装できないとき。
- 対象外を維持する方法: publish 由来の分岐は origin で先に分け、既存のコードを通す（AC4）。stage と、呼び出し開始時に `staged` の fetch の経路に `Invoke-ArtifactNetwork` と `Get-CredentialStatus` を置かない（AC8）。catalog 検査の厳格化をしない（R7 は緩和 1 件だけ）。

## 5. テストとレビュー計画

- 単体テスト: `Get-EvidenceRetentionDecision` の R5（`EvidenceRetention.Tests.ps1`）。`Get-EvidenceDeletionRoute` の全組と `Read-EvidenceTask` の R7（`EvidenceCleanup.Tests.ps1`）。回復・再昇格・削除の手順は偽 transport・FaultHook・`ProcessHook`・`WriteHook`・時刻注入で検証する。停止の後の判定は常に disk から読み直した catalog で行う。
- 差し戻し中の起点 `-Filter`: `tools/Artifacts/tests/ArtifactEvidence.Tests.ps1 -Case <新規ケース>`、`EvidenceCleanup.Tests.ps1 -Case <新規ケース>`、`EvidenceRetention.Tests.ps1`。C は凍結済み条件の確認に必要なら根拠を記録して変えてよい。
- 判定必須テスト（GO 候補 head）: `tools/Artifacts/tests/*.Tests.ps1` と `tools/Workflow/tests/*.Tests.ps1` の全体（各 file を 1 process。`StoreContext.Tests.ps1` は非仮想化 host で。`EvidenceCleanup.Tests.ps1` は `rg` が PATH に要る）、3 project の Release build、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC13 の旧文言 grep、AC4 の既存テストの diff 確認（削除・変更行が `staged-unsent-required` の 1 行目だけ）、AC12 の実 R2 検証 1 回。
- 全 EditMode 回帰の適用除外: Unity 側のコードを変更しないため。代替証拠は上記 offline suite 全体と audit。
- 統合・Unity テスト: なし。
- 高コスト証拠: なし（AC12 は既存 promote・fetch と同じ endpoint・資格情報・事前承認の範囲で、3 file・通常清掃で可逆なので判定必須テストに含める。前スライスの AC10 と同じ扱い）。
- 操作・実行時・目視条件の検証経路: AC12。動作確認（Phase B の後）は `対象なし`（実 R2 は判定 C の実行担当が GO 候補 head で行い、B の head では行わない）。
- AC12 の手順（実 R2 検証 1 回）:
  - 担当: 判定 C の実行担当（判断担当と分ける場合は新しい実行担当 session）。環境: 非仮想化 host の sandbox 外 PowerShell 7（Claude の Bash は LocalAppData が仮想化されるので使わない）。GO 候補 head の detached worktree で 3 project を Release build。登録済みの日次 job の runtime は変えない（R10）。
  - 手順: (1) 新しい task id（`evidence-promote-recovery-rt-<n>`）で `pwsh tools/workflow-task.ps1 start`。(2) 非秘密の固定 fixture（3 file、うち 1 つはバイナリ）を selection にして `evidence stage`。出力の reference と packageSha256 を記録。(3) 1 つの pwsh process で、`tools/artifacts.ps1` が import するのと同じ module を import し、`EvidenceApplication` module の `$script:FaultHook` に「名前が `promote-after-put-pending` なら throw」する scriptblock を入れて、`Invoke-EvidencePromote <config> <reference> <packageSha256>` を呼ぶ。結果 JSON と、直後の catalog・intent file の写しを記録する（期待: `status: failed`、`residue.remote: pre-put`、catalog は `put-pending`）。(4) 別 process で CLI の `evidence fetch`（preflight 前は `put-pending` なので network 付き）。(5) `evidence inspect --task <task id>`。(6) CLI の `evidence promote`（(2) の reference と packageSha256）。`passed` で終わらなければ同じコマンドを 1 回だけ再実行し、それでも `passed` でなければ止めて生記録とともに判断担当へ返す。(7) `evidence inspect`。(8) 別 process で `evidence fetch`（remote 分岐）。(9) 同じ promote を再実行。(10) 原記録（各結果 JSON、各時点の catalog・intent・receipt の写し、fixture の hash 一覧）を判定 evidence に収録し、task を通常の最終手順で end する（R2 object は task 終了 + 30 日の通常清掃が消す。事前承認の範囲）。
  - 実装から独立した期待値: (3) が `failed`・`pre-put`、catalog の対象が `put-pending` で putIntent あり。(4) が `passed`（回復 GET の 404 で `staged` に戻り local 分岐。R2 に object が無いことの観測）。(5) の対象が `staged`・`locality: local`、putIntent の SHA-256 が (3) と同じ。(6) が `passed`・`complete`、reference 文字列が (2) と完全一致、packageSha256 が (2) と一致。(7) が `ready`・`locality: promoted`。(8) が `passed` で、展開した全 file の SHA-256 が fixture の hash 一覧と一致。(9) が `passed`・`already-promoted`。intent file の SHA-256 が (3)・(5)・(10) で同じで、receipt の `putIntentSha256` と一致し、receipt の readback の hash が packageSha256 と一致。catalog の artifactId・key・base・head・manifestSha256・packageBytes・entries が (2) 直後と (10) で一致し、(10) の `ownedCopies` が (2) 直後の `ownedCopies` を全て含む。
- 未知の操作経路の疎通結果: AC12 の手順 (3)（CLI を通さず module を直接呼び、module 内の `$script:FaultHook` に注入して実ストア・実資格情報で promote を止める）は、offline テストと同じ注入点だが実ストアでは未実行。CLI の入口が行う検査（AppData 仮想化の判定等）を通らないので、非仮想化 host で実行する前提を手順に書いた。**未確認。初回確認は Phase C の実行担当**（A3 で、凍結前に小さく確かめるかを決める）。不成立時の対応案: (3) を「偽 transport で PUT を失敗させる」に置き換えることはできない（実 R2 の 404 の観測が目的）ので、(3)〜(5) を外し、R1 の実 R2 観測を offline の AC1 に委ねて S5 を「再昇格の経路の実 R2 往復」に縮める。どちらにするかは A3 で凍結する。
- 人間の判断が必要な条件: なし。
- 判定 C の照合 script の方式: **判定 C の offline 実行**。判定 C の担当が、AC12 の原記録の複製に対して、資格情報・本番設定・network に届かない環境で照合 script を書いて実行する。適用前に合成 fixture で一度通す。凍結する合成 fixture と期待値: 合格 1 組（上の期待値を全て満たす）と不合格 4 組（reference 文字列が 1 文字違う、intent file の SHA-256 が (3) と (10) で違う、(5) の state が `failed`、(8) の file hash が 1 件違う）。script は合格組を合格、不合格 4 組をそれぞれ不合格と判定しなければならない。
- 機械検査: `contract-audit`、`docs-audit`、`git diff --stat`、AC13 の grep、AC4 の既存テストの diff 確認。
- A0/A1 主担当・モデル・ベンダー: Claude Opus 5.5（Anthropic、この session）。
- A2 独立レビュー: 未実施。予定する観点: (a) アーキテクチャゲート（`architecture-gates.md`。責務・依存・テスト境界、`EvidenceCleanup.psm1` に回復の origin 分岐と削除経路の選択を同居させる判断）。(b) 不変条件 I1〜I9 を破る順序の探索（停止点・回復・清掃・旧 runtime の日次 job との組み合わせ）と、§2 末尾の未決事項 1〜4。A0 だけからの代替構成は、同じ問いを前スライスの A2 (c) が出しており（新 state `promote-pending` で再送を禁じる案。§2 の却下理由）、再度は行わない。
- A3 統合担当・モデル・採否: 未実施。
- C' 用に予約した担当・モデル・ベンダー: Grok 系（cursor-agent）。A2 では使わない。
- 独立性の強化条件を満たせない場合の理由: 未確定（A2 の担当の決定後に書く）。

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
