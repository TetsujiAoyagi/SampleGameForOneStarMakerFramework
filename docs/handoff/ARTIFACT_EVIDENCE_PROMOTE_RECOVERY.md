# Artifact Storage / Evidence 昇格の回復と送信済み staged の寿命 HANDOFF

## 0. メタデータ

- type: `slice`
- status: `A`（A3 統合済み revision 3。revision 2 への再レビューの局所修正を §5.3 に記録。owner の本文確認による凍結待ち。A2 の結果は §5.1、外部レビューと統合の差分は §5.2）
- branch: `claude/evidence-promote-recovery`（PR の base は `develop`）
- implementation base commit: `498d0da8f96ef324772d27596c464456f3fad370`（`origin/develop`、PR #130 マージ後。revision 2 の統合時に再確認し、変化なし。凍結時に確定する）
- implementation head commit: 未固定
- risk: `high`（catalog の state 遷移、PUT 応答不明・削除の fail-closed 契約、保持 policy と削除経路の振り分けを同時に変える。同じ key への 2 回目の PUT を許す）
- owner: OSM maintainers
- created: 2026-10-17
- expires: 2026-11-17 または置換 revision
- harvest to: 操作・回復規則・残存リスク・導入手順を `tools/Artifacts/README.md`（冒頭の事前承認、「R2への昇格（evidence promote）」「終了・再開・利用中と清掃」「自動清掃」）へ。`.agents/skills/osm-workflow/references/review-evidence.md` の「保存先とリモートへの配布」の `failed` の扱い（本スライスの実装対象）。`docs/handoff/BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §4 C に「回復と寿命は完了」と後続入力を記録する。
- Phase A snapshot の取得案内・状態: 未保存（凍結時に、凍結 commit と内容 sha256 をここに書く。Git 上の commit で固定し、同一 PC の担当は commit から読む）
- Phase B result snapshot / evidence bundle / C' blind bundle の取得案内・状態: 未生成（未到達 Phase）
- H1 / CURRENT: 採用しない（tracked HANDOFF。前スライスと同じ）
- 引継ぎ票: 次は **owner による revision 3 の本文確認と凍結**。入力は本 HANDOFF の §2〜§5（revision 3）と §5.2・§5.3 の差分一覧。owner が確認したら、主担当が凍結 commit と内容 sha256 を上の Phase A snapshot 欄に書き、引継ぎ票を Phase B 向けに書き換える。push はしない（公開点は `osm-workflow` の「公開点とGit履歴」）。引き渡さないもの: なし。
- （旧）引継ぎ票（A2 → A3。記録として残す）: 次は A3。入力は §5.1 と A1 初稿（commit `bd7ec634`）。
- （旧）引継ぎ票（A1 → A2。記録として残す）: 次は A2。入力は A1 初稿の commit `bd7ec634`（内容 sha256 は §5.1）。

## 1. 目的と対象外

- 目的: PR #130 で入った昇格（`evidence promote`）は、PUT 応答不明の後に回復 GET が 404 を返すと artifact を `failed` で凍結し、取得も再昇格も削除もできない（README の既知の制限）。本スライスは、`origin: stage` の昇格に限って、404 の後を `staged`（putIntent 保持）に戻し、同じ bytes の再昇格と local fetch を可能にする。同時に、送信を試みた可能性のある `staged`（putIntent を持つもの）を、ローカル削除ではなく remote DELETE と GET 404 の確認を経る削除経路に載せ、満了で片付くようにする。publish 由来の artifact の挙動は変えない。
- 現況（2026-10-17、`origin/develop` = `498d0da8`。関数名を正とし、行番号は補助）:
  - promote（[EvidenceApplication.psm1](../../tools/Artifacts/EvidenceApplication.psm1) `Invoke-EvidencePromote` / `Send-EvidenceStagedPackage`）は、preflight と Sync の後の state で分岐し、`staged`・`origin: stage`・putIntent と receipt が null・task が `active` の artifact だけに、1 回の呼び出しで最大 1 回 PUT する。書込み A（intent の参照・`put-pending`・readback staging・artifact の `operation`）→ PUT → 別 process の readback → receipt file → 書込み B（receipt・`ready`・`ownedCopies` 追記・自分の readback staging だけ adopted）。intent file が正規 path に既に在り catalog に未記録なら、field を照合して再利用する（`Confirm-EvidencePromoteIntent`）。putIntent を持つ `staged` は `invalid-source`。送信中の例外では `Complete-EvidencePromoteFailure` が disk の catalog を読み直し、readback の子が居なければ artifact と readback staging の `operation` を外す。
  - 回復（[EvidenceCleanup.psm1](../../tools/Artifacts/EvidenceCleanup.psm1) `Resolve-EvidencePutGuarded`）は origin を見ない。artifact の `operation` の停止を確認し、新しい recovery staging を登録して（`New-EvidenceRecoveryStage`、ここで catalog を書く）、artifact の `operation` をそれに置き換えて GET する。一致なら receipt を書く（既存 receipt は key・packageSha256・manifestSha256 の 3 つで照合して再利用）→ recovery staging の file を `ownedCopies` に追記 → recovery staging と**同じ artifactId の全 staging** を adopted・operation null にする → `ready`。404 なら recovery staging の operation だけを外し、artifact を `failed`（他の staging の operation は残る）。それ以外は `unresolved-put`（recovery staging と artifact の operation は残る）。
  - 回復を呼ぶのは network 付きの preflight（`Assert-EvidenceTransitionSafe`。task 内の全 artifact を走査する。publish・promote・use と、preflight 前の catalog で対象が `staged` でない fetch）と cleanup の各 artifact の処理で、対象は `put-pending` だけ。`failed` はどこからも回復されない。network 無しの preflight（stage と、呼び出し開始時に `staged` の artifact の fetch）は、task 内の `put-pending` と削除 intent を `unresolved-put` / `unresolved-delete` で拒否し、`failed` には触れない。cleanup は 1 つの artifact の回復が例外を投げると、その task の残りの処理を止めて task 全体を `unconfirmed` で blocked に数える（`Invoke-EvidenceCleanup` の task ごとの catch）。
  - 保持 policy（[EvidenceRetentionPolicy.psm1](../../tools/Artifacts/EvidenceRetentionPolicy.psm1) `Get-EvidenceRetentionDecision`）: `deleted` → `payload-deleted`、`ready` / `staged` 以外 → `unconfirmed-artifact`（`failed` を含む。削除しない）、putIntent も receipt も null の `origin: stage` 以外の `staged` → `unknown-state`、以降は期限・保護の判定。
  - 削除経路（`Invoke-EvidenceCleanup` の artifact の処理、330 行目付近）: 保持判定が `expired` なら、state が `staged` → ローカル削除（`Invoke-EvidenceLocalDeleteGuarded`、network 無し）、それ以外 → remote 削除（`Invoke-EvidenceDeleteGuarded`: deleteIntent・`delete-pending` を書く → FaultHook `after-intent` → DELETE → FaultHook `after-remote` → 確認用 staging を登録して GET → 404 `NoSuchKey` → `deleted` → `ownedCopies` の削除）。削除 intent の回復（`Resolve-EvidenceDeleteGuarded`。intent の process が停止済みのときだけ）は GET 404 で `deleted`、一致で `ready` に戻す。
  - catalog の読戻し（[EvidenceStateStore.psm1](../../tools/Artifacts/EvidenceStateStore.psm1) `Read-EvidenceTask`）は、`ready`・`delete-pending`（origin を問わない）と、origin が `stage` 以外の `deleted` に receipt を要求する（無ければ `receipt-missing`）。origin と state の組み合わせは他に制限しない。
  - 清掃は `staged` または `put-pending` の artifact を参照する staging を 7 日清掃から一律に除外する（`Invoke-EvidenceCleanup` の staging の処理、293 行目付近）。
  - fetch は preflight 前の catalog で対象が `staged` なら network 無しの preflight と local 分岐、それ以外は network 付き preflight と remote 分岐を選び、preflight 後に選び直さない。ローカル固有の reasonCode（`changed-copy`・`unresolved-put`・`unresolved-delete`）は local 分岐のときだけ返し、remote 分岐の失敗はそれ以外を `unconfirmed` にする。
  - `use` / `release` は `ready` と `staged` を受け付ける。inspect の `locality` は `staged` → `local`、`ready` → `promoted`。
  - 登録済みの日次 job の runtime は、`schedule install` を行った head に固定される（README「自動清掃」）。PR #128 より前の runtime は `origin: stage` を含む catalog を読めない。
  - offline テストの偽 transport（`EvidenceTestSupport.ps1`）は、既に object が在る key への PUT を `duplicate-put` で失敗させる。
- 前スライスからの入力（program §4 C「R2昇格の送信」、削除前の HANDOFF `docs/handoff/ARTIFACT_EVIDENCE_R2_PROMOTION.md`）:
  - revision 2（commit `8494c791`）の D3（同じ bytes の再送の許可と publish の再送禁止との区別）、D4 (i)(ii)、D5、D6、D8、D11、AC5・AC7・AC8・AC9・AC11・AC14・AC15、既存ケース `staged-unsent-required` の 1 行目の変更。A3 裁定 (1)（404 後の `staged` と再送）・(2)（削除後の遅延 PUT の受容）・(5)（実 R2 の前の `schedule install`）は再確認の対象。
  - 制約: (C1) 回復（404 → `staged`）と保持 policy・削除経路・清掃の振り分けを同じスライス・同じ commit に置く。(C2) `origin: stage` の `failed`（putIntent あり・receipt 無し）を `corrupt-state` にしない。(C3) 回復一致時の adopted の規則を変えるなら、publish 由来の staging を巻き込まない（origin で分けるか「今回の回復に使った staging だけ」に言い換える）。
  - 既知の残り（後続入力）: receipt 再利用の照合が 3 field に限られる点、回復一致時に promote の readback staging が未登録 file ごと adopted になる点、書込み A の後に止まり 404 の場合に promote の readback staging が `operation` を持ったまま残る点、送信部の共有（`EvidenceSend.psm1`）、`createdAt`・`process` の型検査。
- 対象外: publish 由来（`origin: publish` と origin の無い artifact）の回復・保持・削除・adopted・reasonCode。送信部の共有（`EvidenceSend.psm1`）。receipt 再利用の照合の拡張。catalog の組み合わせ検査の厳格化（revision 2 の D8 のうち、本スライスの shape を読むための緩和以外。§2 R7）。条件付き PUT など transport の変更。Cloud・別 PC からの取得経路と token（program §4 C）。`ready` の local copy からの network 無し fetch。`use` の network 無し preflight。端末喪失に備えた未昇格 `staged` の backup（Harness H3）。Unity 側の変更なし。

## 2. 意思決定と受け入れ境界（revision 3。A2 と外部レビュー 2 回の採用分を統合。owner の本文確認待ち）

- このスライスが答える問い: 「昇格由来（`origin: stage`）の `put-pending` と `failed` が、回復 GET の 404 の後に `staged`（putIntent 保持）へ戻って、local fetch と同じ bytes の再昇格ができ、送信を試みた可能性のある `staged` が remote DELETE と GET 404 の確認を経てだけ `deleted` になるか。publish 由来の挙動を変えず、無関係な artifact の操作を止めずに」。
- A0 で固定した前提: 範囲は昇格由来の回復と寿命だけ（program §4 C の後続スライス、2026-10-17）。前スライスの制約 C1〜C3 を守る。
- A3 の裁定（2026-10-17。owner は外部レビューを渡して「妥当だと思ったら受け入れて修正して」と指示し、主担当が §5.1・§5.2 の採否を判断して統合した。最終確認は凍結前の owner の本文確認）: (1) A2 の #1〜#9・#11・#12 を採用。(2) A2 #10 は「受容」ではなく、外部レビュー P1-1 の推奨どおり **`failed` の回復を対象 artifact 自身の promote・fetch と cleanup に限る**（R3）。(3) 未決事項 1（network 無しの preflight は `failed` を拒否しない）を採用。(4) 未決事項 2（実 R2 の前の `schedule install` は不要）を、読み取りだけの `schedule status` で登録済み runtime の互換を確かめることを開始条件にして採用。(5) 未決事項 3 は凍結前に隔離環境で疎通を確認した（§5）。AC12 が実行できない場合に S5 を縮める逃げ道は削除し、NO-GO と Phase A / B への差し戻しにする。(6) 未決事項 4（削除後の遅延 PUT）は README に保証外と明記して受容する。
- 方式:
  - **R1 回復 GET の 404 → `staged`（`origin: stage` のみ）。** `Resolve-EvidencePutGuarded` は、対象が `origin: stage` のとき、次の順で行う。
    1. **過去の operation の停止確認（GET と recovery staging の登録の前）:** artifact の `operation`（あれば）と、同じ artifactId の staging のうち `operation` を持つもの全てに `Assert-EvidenceOperationStopped` を行う。1 つでも通らなければ `operation-pending` で止まり、recovery staging を登録せず、GET もせず、catalog を書かない。
    2. 全て通った後で、今回の recovery staging を登録し（既存の `New-EvidenceRecoveryStage`）、artifact の `operation` をそれにして GET する（既存）。
    3. GET 404: 1 回の catalog 書込みで、state を `staged` に戻し、artifact の `operation`・今回の recovery staging の `operation`（今回の処理が終わったことが根拠）・手順 1 で停止を確認した過去の staging の `operation` を外す。`putIntent`・`ownedCopies`・`protections`・identity の全 field と、全 staging の `adopted` は変えない。
    - `origin: publish` と origin の無い artifact は既存のまま（404 → `failed`、停止確認は artifact の `operation` だけ）。
    - 戻した `staged` の staging は、artifact が `staged` の間は既存の除外で 7 日清掃に乗らない。artifact が `ready` または `deleted` になった後に、未採用のものが通常の 7 日清掃に乗る。
  - **R2 回復 GET 一致時の adopted（`origin: stage` のみ）。** R1 の手順 1・2 を同じく行い、一致なら既存どおり receipt を書いて（在れば既存の照合で再利用）recovery staging の file を `ownedCopies` に追記し `ready` にする。adopted にするのは今回の recovery staging だけ。手順 1 で停止を確認した過去の staging は `operation` を外すだけで `adopted` を変えない（stage の元の staging は既に adopted のまま、停止した promote の readback staging は未採用のまま 7 日清掃に乗る）。`origin: publish` は既存どおり同じ artifactId の全 staging を adopted にする（C3）。
  - **R3 `failed` の回復は対象 artifact 自身の操作と cleanup だけ（`origin: stage`・putIntent あり・receipt 無しのみ）。** この shape（PR #130 の runtime と、本スライスより前の日次 job が回復の 404 で作る）を「回復できる `failed`」と呼ぶ。
    - `Assert-EvidenceTransitionSafe` に省略可能な引数（回復を許す artifactId、1 つ）を足す。network 付きの preflight は、`put-pending` を既存どおり task 内の全てで回復し、回復できる `failed` はこの引数の artifact だけを `Resolve-EvidencePutGuarded` に渡す。他の `failed` には触れない（今と同じく不活性）。
    - 引数を渡すのは promote と fetch で、reference の artifactId を渡す。publish・use・stage・release・inspect は渡さない。network 無しの preflight は引数を見ず、`failed` に触れず、拒否もしない（今の stage・local fetch の成功を変えない）。
    - cleanup は、回復できる `failed` の artifact の処理で（dry-run を除く）`Resolve-EvidencePutGuarded` を呼ぶ。この回復が例外を投げた場合は、その artifact の item を `unconfirmed-artifact`（今の `failed` と同じ code）にする。回復は途中でメモリ上の state・receipt・`ownedCopies`・adopted を書き換えてから例外を投げうる（例: 一致の後の `ownedCopies` の列挙中）ので、例外の後はその task のメモリ上の状態を以降の catalog の書込みに使わない: その実行ではその task の残りの item を `remaining` に数えて次の task へ進む。cleanup の cursor は失敗した item を指して試行の前に永続化済みなので、次の実行はその次の item から始まり、解決しない `failed` は毎回最後に回る（他の artifact の item は毎回処理される）。この試行が disk に書いた operation は既存の `put-pending` の失敗と同じく残し、次の実行の停止確認に任せる。
    - 結果は R1・R2 と同じ（一致 → `ready`、404 → `staged`、それ以外 → 例外で state は `failed` のまま）。promote・fetch では、自分の対象の回復の失敗は既存の preflight の失敗と同じ扱い（promote は `unresolved-put`、remote 側の fetch は `unconfirmed`）。
    - 依存の変化: Application（promote・fetch）が Cleanup の preflight に artifactId を渡す。依存の向きは変わらない（Application → Cleanup）。
  - **R4 再昇格（同じ bytes の 2 回目の PUT）。** promote の送信条件を、**preflight と Sync の後の state** で、`staged`・`origin: stage`・receipt が null・task が `active`・identity と期待 hash が一致、かつ「putIntent が null」または「putIntent が在り、正規 path の intent file の SHA-256 が `putIntent.sha256` と一致し、`Confirm-EvidencePromoteIntent` の field 照合を通る」に広げる。1 回の promote 呼び出しの中で、preflight が自分の対象の `put-pending` / 回復できる `failed` を 404 で `staged` に戻し、そのまま送信へ進むことを含む。intent file は作り直さず、`createdAt`・`process` も前回の値のまま使う。照合に失敗したら `unresolved-put`、PUT 0 件。以降の書込み A・PUT・readback・書込み B は既存の `Send-EvidenceStagedPackage` のまま（1 回の呼び出しで最大 1 回の PUT、PUT 応答不明は `put-pending` のまま再送しない）。
    - 根拠（revision 2 の D3）: 送る bytes は catalog の packageSha256 と lock 下で照合した ZIP に固定され、R2 の PUT は object 全体の置換なので、先行 PUT が後から確定しても後続と同じ内容になる。保証するのは「その key の object は、存在するなら packageSha256 の bytes である」ことと「`ready` は別 process の readback 一致の後にしか書かない」ことで、「同じ key への PUT が通算 1 回」は保証しない。publish の「PUT 応答不明を同 key へ再送しない」は毎回別の snapshot を作る publish の契約で、変えない。
    - receipt file が正規 path に既に在る（前回の試行が receipt file を書いた後に止まり、その後 object が消えて 404 → `staged` に戻った）場合、再昇格は immutable な receipt の書込みで失敗し `put-pending` のまま残る。次の回復の GET 一致が既存 receipt を照合して再利用し `ready` に収束するので、本スライスでは直さない（前スライスの「優先度の低い入力」のまま）。
  - **R5 保持 policy（削除してよいか）。** `Get-EvidenceRetentionDecision` は、`staged`・`origin: stage`・putIntent あり・receipt 無しを、`unknown-state` にせず `ready` と同じ期限・保護の判定にする。receipt を持つ `staged`、publish 由来の `staged`、origin の無い `staged` は既存どおり `unknown-state`。保持 policy は「削除してよいか」だけを答え、「どの手段で削除するか」は持たない（R6）。
  - **R6 削除経路の振り分け（どの手段で削除するか）。** `EvidenceCleanup.psm1` に副作用の無い内部関数 `Get-EvidenceDeletionRoute` を置く: `local` = `staged`・`origin: stage`・putIntent 無し・receipt 無し。`remote` = `ready`、または `staged`・`origin: stage`・putIntent あり・receipt 無し。それ以外は `none`。cleanup の artifact の処理は、保持判定が `expired` のときこの関数で振り分け、`none` は削除しない（`unknown-state` として blocked に数える）。`Invoke-EvidenceLocalDeleteGuarded` も自分の中で経路が `local` であることを再確認し、違えば何も書かずに `unknown-state` を返す。
    - putIntent を持つ `staged` の remote 削除は既存の `Invoke-EvidenceDeleteGuarded`（deleteIntent・`delete-pending` → DELETE → GET 404 → `deleted` → `ownedCopies` の削除）。削除 intent の回復と照合（`Resolve-EvidenceDeleteGuarded`）で GET が一致した場合、receipt を持つ artifact は既存どおり `ready`、receipt を持たない artifact は `staged` に戻す。cleanup の artifact の処理は、今と同じく照合の直後に同じ呼び出しの中で保持判定をやり直すので、満了していれば同じ cleanup の中で新しい削除 intent による再 DELETE へ進む（制御フローは変えない）。
    - **確定の順序（`origin: stage` の remote 削除、実装変更）:** 現行の `Invoke-EvidenceDeleteGuarded` と `Resolve-EvidenceDeleteGuarded` は、GET 404 の後に `Remove-EvidenceOwnedCopies` で local copy を消してから `deleted` を catalog に書く。この間で止まると、disk は `delete-pending` のまま ZIP が無く、次の preflight が `changed-copy` で止まる。`origin: stage` の artifact（putIntent を持つ `staged` と、昇格済みの `ready` の両方）では、GET 404 の後に「deleteIntent 解除・`deleted`・operation 解除を catalog に書く → `Remove-EvidenceOwnedCopies` → 残った `ownedCopies` を catalog に書く」の順にする。`deleted` の後に止まった場合の残りの copy は、既存の仕組み（`Assert-EvidenceTaskPaths` が `origin: stage` の `deleted` の欠落 copy を許し、cleanup の `deleted` の処理が残りを消す）で片付く。`origin: publish` と origin の無い artifact は現行の順序のまま（I1。`deleted` の欠落 copy の許容が stage 由来だけなので、同じ変更は後続入力）。
    - 失敗した書込みの後: `Write-EvidenceTask` は書込みの前にメモリ上の generation を進めるので、書込みが失敗した後の同じ task への書込み（`Resolve-EvidenceDeleteGuarded` の `finally` を含む）は `state-conflict` で失敗しうる。`after-rename` の失敗なら disk は書けている。どちらの場合も満たすべきは「disk が `delete-pending` なら ZIP は残っている」「disk が `deleted` なら ZIP は残っていてもよく、次の cleanup が消す」の 2 つで、どちらの終点になったかは B が実測して §6 に記録し、テストはこの 2 つと次の cleanup での収束を assert する。
    - 残存リスク（受容。README に保証外と書く。revision 2 の A3 裁定 (2) の再確認）: 削除の後に、先行 PUT が遅れて確定すると、R2 に追跡外の object が残る。全ての PUT は task が `active` の間にだけ発行され、呼び出し側の期限（最大 600000 ms）で打ち切られ、remote 削除は task 終了 + 30 日以後にだけ起きるので、R2 側で 30 日以上確定が遅れる必要がある。有限回の追加確認ではこのリスクを消せないので、削除後の追加確認は置かない。
  - **R7 catalog の読戻し。** `Read-EvidenceTask` は、`origin: stage` の `delete-pending` に限り、receipt が null でも putIntent を持てば読む（R6 で receipt を持たない artifact が `delete-pending` を通る）。putIntent も receipt も無い `origin: stage` の `delete-pending` は既存どおり `receipt-missing`。他の組み合わせの検査は変えない（revision 2 の D8 の厳格化は後続。C2 により回復できる `failed` は読めるまま）。
  - **R8 fetch の取得元を preflight の後に決める。** preflight に network を使うかは、既存どおり preflight 前の catalog で決める（対象が `staged` なら network 無し）。取得元は preflight と Sync の後の state で決める: `staged` → local 分岐、`ready` → remote 分岐、それ以外 → 既存の拒否（`identity-mismatch` 等）。昇格由来の `put-pending` と自分の回復できる `failed` は、network 付き preflight の回復で `ready` か `staged` になり、その取得元で取得される。ローカル固有の reasonCode は、preflight 前の判定か最終の取得元のどちらかが local のときに返す。preflight が回復に失敗した場合（取得元が決まる前の失敗）は、preflight 前の判定に従う（network 付きなら `unconfirmed`。今と同じ）。失敗時に他方の取得元へ fallback しない。network 無しの保証は「呼び出し開始時に `staged` の artifact」に限る（既存の README の文言どおり）。
  - **R9 ワークフロー文書。** `review-evidence.md` の「保存先とリモートへの配布」の「昇格が `failed` で止まった場合は、そのartifactをremote用に使わず、従来の `evidence publish` で新しいartifactを作り、取得案内も新しく作る（referenceとhashは別物になるので、local stageの取得案内をremote用として流用しない）」を、「昇格が応答不明で止まった場合は、同じ artifact への次の fetch・promote か日次清掃が回復し、`ready` になればそのまま、`staged` に戻れば同じ `evidence promote` を再実行する。reference と hash は変わらない」に置き換える。`SKILL.md`・`phases-and-handoff.md`・`handoff-template.md` は変えない。
  - **R10 README と導入手順。** 「R2への昇格」の「対象と拒否」「PUT応答不明」「既知の制限」「fetch」を R1〜R4・R8 と残存リスク（R6）に置き換え、「終了・再開・利用中と清掃」に未送信の `staged`（ローカル削除）と putIntent を持つ `staged`（remote 削除）の清掃契約を分けて書く。冒頭の事前承認の文（`staged` の満了清掃は network と資格情報を使わない）を「putIntent の無い `staged` の満了清掃は network と資格情報を使わない。putIntent を持つ `staged` の満了清掃は remote DELETE と GET を使い、通常清掃と同じ承認範囲」に直す。「Evidence v2 の通常操作」の再送禁止は publish の契約であり、promote は R4 に従うと書く。回復できる `failed` の回復が解決できない間は、その artifact への fetch・promote が失敗し、cleanup がその item を `unconfirmed-artifact` で残す（他の artifact の操作は止めない）と書く。導入手順: 共有 store で本スライスの shape（putIntent を持つ `staged`、receipt の無い `delete-pending`）が生じる前に、`schedule status` で登録済み runtime の head を確認し、本スライスの head で `schedule install` を行う（登録済み task の切替は既存の手順）。
    - 旧 runtime（PR #128 以降で本スライスより前の head）と新しい shape の関係: putIntent を持つ `staged` は、旧 reader で読め、旧 policy で `unknown-state`（削除しない）、旧 cleanup はその staging を除外する。receipt の無い `delete-pending` は旧 reader で `receipt-missing` になり、旧 runtime ではその task 全体の清掃と操作が止まる（削除は起きない）。後者は新しい runtime の cleanup が putIntent を持つ `staged` を削除し始めて初めて生じる。どちらも fail-closed で、データを失わない。旧 runtime の日次 job は昇格由来の `put-pending` を 404 で `failed` にするが、それは R3 で回復できる。
    - 判定 C の実 R2 検証（AC12）は putIntent を持つ `staged` を経由するが、削除まで進めない（R6 の実削除は offline のみ）ので、旧 runtime の日次 job が同じ task を見ても削除・停止は起きない。したがって判定のために共有 store の登録済み runtime を切り替えない（revision 2 の A3 裁定 (5) を置き換える）。代わりに AC12 の開始条件として、読み取りだけの `schedule status` で登録済み runtime が `origin: stage` を読めることを確かめる（§5）。
- 却下した代替案:
  - 新 state `promote-pending` で 404 の後も保持し、再送を禁じる（前スライスの A2 (c)）: 404 の後は遅れた PUT が確定しない限り永久に `ready` に達せず、同じ reference のまま昇格する目的を満たせない。新 state は reader・保持・清掃・fetch・use の全分岐の網羅を要する（前スライスの A3 裁定 (1)）。
  - 404 の後に不在確認の時刻を記録してローカル削除を許す（旧 HANDOFF の D4'）: 遅れて確定した object が追跡外に残る（ADD-01）。
  - 回復一致時の「中の file が全て `ownedCopies` に入った staging だけを adopted」を origin を問わず入れる: publish 由来の staging（`readback-process.json` 等は `ownedCopies` 外）が adopted から外れ、7 日清掃で消えて task が `changed-copy` で止まる（C3）。
  - 回復できる `failed` を task 内の全ての network 付き preflight で回復する（A1 初稿の R3、A2 #10 の受容案）: その回復が別 bytes・通信エラーになると、同じ task の無関係な artifact の publish・use・remote fetch・promote が `unresolved-put` で止まる。今は `failed` が不活性で止まらないので、新しい故障の波及になる（外部レビュー P1-1）。
  - network 無しの preflight で回復できる `failed` を `unresolved-put` で拒否する（revision 2 の D4）: 今の stage・local fetch の成功を、同じ task の無関係な `failed` を理由に止める既存挙動の変更になる。
  - 削除前に putIntent の process の停止を確認する（前スライスの A2 #20）: `staged` へ戻す回復が既に operation の停止を確認している。前スライスで不採用で閉じた。
  - catalog の組み合わせ検査を全面的に厳格化する（revision 2 の D8）: 本スライスの問いに不要で、既存の実 catalog の shape を読めなくする危険を増やす。必要な緩和（R7）だけを行う。
  - AC12 を実行できない場合に S5 を「再昇格の実 R2 往復」に縮める（A1 初稿 §5）: 本スライスの中心は PUT 前停止 → 実 R2 の 404 → `staged` → 再昇格の経路で、それを外すと問いに答えない（外部レビュー P1-2）。
- 進める最低条件:
  - 不変条件（起きてはいけないこと）:
    - **I1** publish 由来（`origin: publish` と origin の無い artifact）の回復・保持判定・削除経路・adopted・reasonCode が変わる。既存テストのうち、publish 由来・origin の無い artifact を対象とする assertion が変わる。§2 AC4 の変更表に無い既存 assertion が変わる、または削除される。
    - **I2** putIntent を持つ artifact（PUT を試みた可能性があるもの）が、remote DELETE と GET 404（`NoSuchKey`）の確認を経ずに `deleted` になる。ローカル削除の経路を通る。`origin: stage` の artifact の remote 削除（通常の削除と削除 intent の回復）で、`deleted` が disk に永続化される前に `ownedCopies` の file が消える。
    - **I3** promote が、**preflight と Sync の後の state** で R4 の条件を満たさない artifact（`put-pending`、`failed`、receipt を持つもの、publish 由来、終了済み task、identity・期待 hash の不一致、putIntent と intent file の不一致）に PUT を発行する。1 回の呼び出しが 2 回以上 PUT を発行する。catalog の packageSha256・packageBytes と一致しない bytes を PUT する。再 snapshot・再 package が起きる。
    - **I4** 回復・再昇格の前後で、reference 文字列・artifactId・key・packageSha256・manifestSha256・base・head・packageBytes・entries・taskEndedAt・deleteEligibleAt・protections、または putIntent の参照と intent file の SHA-256 が変わる。
    - **I5** 別 process の readback で bytes・hash の一致を確認する前に、`ready` または receipt が catalog に記録される。
    - **I6** 404 で `staged` に戻った artifact が、昇格前の `ownedCopies` のいずれかを失う（確認済みの削除以外で）。`origin: stage` の回復一致で、今回の recovery staging 以外の staging が新たに adopted になる。今回の処理のもの以外で、`Assert-EvidenceOperationStopped` を通っていない `operation` が外される。過去の operation の停止を確認できないのに、recovery staging の登録・GET・catalog の書込みが起きる。
    - **I7** 保護中（有効な `use`）または `deleteEligibleAt` より前の artifact が、回復・再昇格・清掃で削除される。
    - **I8** 呼び出し開始時に `staged` の artifact への stage・fetch が network・資格情報を使う。fetch が local と remote の間で失敗時に fallback する。fetch が、自分の preflight で state が変わったことを理由に、取得できる artifact を拒否する。
    - **I9** PR #130 の runtime が作る catalog の shape（`origin: stage` の `staged`（putIntent 無し）・`put-pending`・`ready`・回復できる `failed`（staging の operation が残るもの・staging が清掃済みのものを含む）・`delete-pending`（receipt あり）・`deleted`）のどれかが、新しい reader で `corrupt-state` / `receipt-missing` / `receipt-conflict` になる。
    - **I10** 回復できる `failed` が回復できない（GET が別 bytes・通信エラー）ことを理由に、同じ task の別の artifact の publish・stage・fetch・use・release・promote・cleanup の結果が変わる。
  - 成立条件（起きなければならないこと）:
    - **S1** 昇格由来の `put-pending` が回復 GET 404 で `staged`（putIntent 保持）に戻り、続く local fetch が成功し、promote の再実行が同じ intent を再利用して PUT 1 回で `ready` になり、remote fetch が全 entry 一致で成功する。
    - **S2** 回復できる `failed` が、その artifact 自身への promote・fetch と cleanup で、GET 404 なら `staged`、GET 一致なら `ready` になる。
    - **S3** putIntent を持つ `staged` が満了で remote 削除の経路を通って `deleted` になり、その後も `Read-EvidenceTask` が通る。途中で止まっても、次の実行が `staged` か `deleted` に収束させる。putIntent の無い `staged` は既存どおりローカル削除される。
    - **S4** 昇格由来の `put-pending` / 回復できる `failed` の fetch が、preflight の回復の後の state に合った取得元で成功する。
    - **S5** 実 R2 で、PUT 前に止めた昇格の回復 GET が 404 を返して `staged` に戻り、local fetch・再昇格・別 process の remote fetch が全 entry 一致で成功する（判定必須。§5。縮めない）。
- 受け入れ条件（検証例。offline は隔離した temp store・偽 transport・時刻注入。実ストア・network 不使用。実 R2 は AC12 だけ。「停止」は FaultHook または `WriteHook` で例外を投げ、必要なら停止時点の catalog の bytes を戻して process の消失を再現し、`ProcessHook` で停止済みとして扱う既存の方法）:
  - **AC1**（S1, I3, I4, I6）: fixture の `staged` を FaultHook `promote-after-put-pending` で止める（`put-pending`、PUT 0 件）。fetch（preflight 前は `put-pending` なので network 付き）の回復で偽 GET 404 → `staged`。disk から読み直した catalog で、putIntent の参照と intent file の SHA-256 が停止前と同じ、I4 の全 field が stage 直後と同じ、`ownedCopies` が stage 直後の集合を全て含む、artifact と promote の readback staging と recovery staging の `operation` が null、readback staging と recovery staging の `adopted` が false。その fetch は local 分岐で成功する。続く 2 回目の fetch は network 無し（transport 呼び出し 0 件・資格情報の読取り 0 件）で成功する。promote の再実行は PUT 1 回で `ready`、intent file は作り直されず（SHA-256 不変）、receipt の `putIntentSha256` が同じ値。remote fetch が成功する。不合格例: 再昇格で intent file が新しく書かれる、2 回目の fetch が network 付き preflight を通る。
  - **AC2**（S1, I3）: (a) 偽 transport で PUT の応答を失わせる（object は置かない）→ `put-pending`、その呼び出しの PUT は 1 回。次の promote 1 回の呼び出しの中で、preflight の回復 GET 404 → `staged` → PUT 1 回 → `ready`（`complete`）。通算 PUT 2 回、各呼び出しで最大 1 回（不合格例: 応答を失った呼び出しの中で 2 回目の PUT、または 2 回目の呼び出しが `invalid-source`）。(b) 応答を失わせて object は置く → 次の promote の preflight の回復一致で `ready` → `already-promoted`、PUT 通算 1 回。
  - **AC3**（S2, I9, I10）: 回復できる `failed` を、PR #130 の実出力と同じ形で 3 variant 作る（AC1 の停止の後、旧 `Resolve-EvidencePutGuarded` と同じ書込みで state `failed`・recovery staging の operation 解除）。(i) promote の readback staging の `operation` が非 null で残る（PR #130 の実際の形。その process は停止済み）、(ii) readback staging と recovery staging が 7 日清掃で catalog から消えた形、(iii) 全 staging の `operation` が null。各 variant で: (a) その artifact への promote の preflight で偽 GET 404 → `staged` → 同じ呼び出しで PUT 1 回 → `ready`。(b) その artifact への fetch で GET 404 → `staged` → local 分岐で成功、GET 一致 → `ready`（receipt あり、recovery staging だけ adopted）→ remote 分岐で成功。(c) GET が別 bytes → promote は `unresolved-put`・fetch は `unconfirmed`、state は `failed` のまま、PUT 0 件。(d) cleanup が (a)(b)(c) と同じ 3 終点を作る。(c) ではその item が `unconfirmed-artifact` で、同じ task の別 artifact の item（満了した `ready` の remote 削除、満了した未送信 `staged` のローカル削除）は、その実行で失敗した item より前の順なら同じ実行で、後なら `remaining` に数えられて次の実行で処理される（2 回の実行の後に両方が `deleted`）。回復が一致の後の書込みで失敗した場合（最後の catalog 書込みに `WriteHook` の `before-rename` を注入）、disk の対象は `failed` のままで、その実行の後続の item の書込みがメモリ上の `ready`・receipt・adopted を disk に持ち込まない（不合格例: 後続の item の書込みで対象が `ready` として disk に現れる、または後続の item が `state-conflict` で毎回止まる）。(e) 同じ task の別 artifact への publish・stage・fetch（local / remote）・use・release・promote が、(c) の `failed` があっても成功し、`failed` は変わらない（transport 呼び出しは別 artifact の分だけ）。publish 由来の `failed`、putIntent 無しの `failed` は (a)〜(e) のどれでも変わらない。(i) で readback staging の process が稼働中なら、(a)(b) は `operation-pending` で止まり、recovery staging が登録されず、GET 0 件、catalog の generation 不変。
  - **AC4**（I1）: `origin: publish` の `put-pending` で偽 GET 404 → `failed`、GET 一致 → 同じ artifactId の全 staging が adopted（既存挙動）。origin の無い `staged` と publish 由来の `staged` は保持判定 `unknown-state`・削除経路 `none` で、DELETE・ローカル削除とも 0 件。既存の `tools/Artifacts/tests/*.Tests.ps1` と `tools/Workflow/tests/*.Tests.ps1` が failed 0。既存 assertion の変更は次の表の範囲だけで、表に無い既存 assertion の変更・削除は §4 の差し戻し条件（B が無言で追認しない）。各行は旧期待を消すだけでなく新期待を assert する。

    | file / ケース | 旧期待 | 新期待 | 理由 |
    |---|---|---|---|
    | `ArtifactEvidence.Tests.ps1` / `promote-refusals` の `(d) putIntent`（正規 path の有効な intent file と一致する sha を持つ `staged`） | `invalid-source`、PUT 0 | `passed`・`complete`、PUT 1、intent file の SHA-256 不変 | R4 |
    | 同 / `promote-refusals` の `(c)`（応答喪失・object 無しの後の再 promote） | `invalid-source`、state `failed` | 同じ呼び出しの preflight で 404 → `staged` → PUT 1 → `passed`・`complete`、state `ready`、通算 PUT 2 | R1・R4（AC2 (a)） |
    | 同 / `promote-lost-response` の後半（`failed promote`・`failed kept`・`failed policy`・`failed cleaned`） | 再 promote `invalid-source`、`failed` で保持、満了後も `unconfirmed-artifact` で非削除 | 再 promote が `passed`（protection・ZIP・昇格前の `ownedCopies` が残る、通算 PUT 2）、満了後の保持判定 `expired`、cleanup が remote 削除で `deleted` | R1・R4・R6 |
    | 同 / `promote-stop-endpoints` の `match` | 停止した promote の readback staging が adopted・operation null | readback staging は adopted=false・operation null、今回の recovery staging だけ adopted。満了後の cleanup の結果（artifact `deleted`）は不変で、readback staging は 7 日清掃で消える | R2 |
    | 同 / `promote-stop-endpoints` の `missing`（fetch・state・staging と満了後の cleanup） | fetch `identity-mismatch`、state `failed`、readback staging の operation が非 null、cleanup `unconfirmed-artifact`・ZIP 残存 | fetch は local 分岐で `passed`、state `staged`（putIntent 保持）、readback staging の operation null・adopted=false、cleanup は `expired` → remote 削除（DELETE 1・GET 404）で `deleted`・ZIP 削除 | R1・R6・R8 |
    | 同 / `promote-fetch-pending` の `pending missing` | `identity-mismatch`、state `failed` | `passed`（local 分岐）、state `staged` | R1・R8 |
    | 同 / `promote-atomic-failure` の `a-after` の再 promote | `invalid-source`、state `failed`、PUT 0 | `passed`・`complete`、state `ready`、PUT 1 | R1・R4 |
    | `EvidenceRetention.Tests.ps1` / `staged-unsent-required` の 1 行目（putIntent を持つ `staged`） | `unknown-state` | 期限ちょうどで `expired`（同じケースの残り 3 行は無変更） | R5 |

  - **AC5**（I6）: `origin: stage` の回復一致で、adopted に変わる staging は今回の recovery staging だけ。停止した promote の readback staging（中に `readback-process.json` 等の未登録 file）は adopted=false・operation null のまま残り、時刻注入で 7 日後の cleanup がそれを削除する（artifact は `ready`）。stage の元の staging は adopted のまま、その中の ZIP は `ownedCopies` に残る。不合格例: readback staging が adopted になり、artifact 削除後もディレクトリが残る。
  - **AC6**（S3, I2, I7）: 時刻注入。putIntent を持つ `staged` は `deleteEligibleAt` の 1 tick 前に `retained`、ちょうどで remote 削除（deleteIntent → DELETE → GET 404）され `deleted`、ZIP と `ownedCopies` が消え、その後も `Read-EvidenceTask` が通る（不合格例: DELETE 0 件で `deleted`）。putIntent も receipt も無い `staged` は既存どおりローカル削除（transport 呼び出し 0 件）。`Get-EvidenceDeletionRoute` は state × origin（`stage` / `publish` / 無し）× putIntent の有無 × receipt の有無の全組で表どおりの値を返す（単体テスト）。`Invoke-EvidenceLocalDeleteGuarded` に putIntent を持つ `staged` を直接渡すと何も書かず `unknown-state`。putIntent を持つ `staged` の削除の停止と再開（各停止の後、disk から新しく読み直した catalog を `Read-EvidenceTask` で読み、削除 intent の process を `ProcessHook` で停止済みとして次の cleanup を行う）:
    - (a) FaultHook `after-intent`（`delete-pending` の書込みの直後・DELETE の前）で停止 → disk は `delete-pending`（receipt 無し・putIntent あり）で読め、ZIP は残る → 次の cleanup の照合 GET:
      - 一致（object を置いた fixture）: 照合の時点で `staged` に戻り ZIP と putIntent が残る（照合直後の disk で観測する。偽 transport の呼び出しに合わせて disk を読み直す、または照合後に止める hook で観測する）。同じ cleanup の中で保持判定がやり直され、満了しているので新しい削除 intent で再 DELETE → GET 404 → `deleted` が最終結果になる（再 DELETE してよい。不合格例: 照合一致の時点で `deleted`、または DELETE を経ずに `deleted`）。
      - 404: `deleted`。
    - (b) FaultHook `after-remote`（DELETE 成功の後・確認 GET の前）で停止 → disk は `delete-pending`、ZIP は残る → 次の cleanup の照合 GET 404 → `deleted`、その後に ZIP が消える。
    - (c) 確認 GET 404 の後の `deleted` の catalog 書込みに `WriteHook` の `before-rename` と `after-rename` で失敗を注入する。通常の削除（`Invoke-EvidenceDeleteGuarded`）と削除 intent の回復（`Resolve-EvidenceDeleteGuarded`。`finally` の書込みを含む）の両方で行う（4 ケース）。各ケースの後、メモリ上の state は根拠にせず、新しく読み直した disk の catalog と ZIP の実在で「disk が `delete-pending` なら ZIP は残る」「disk が `deleted` なら ZIP は残っていてもよい」を確かめ、どちらの終点だったかを記録する。続く cleanup で `deleted` かつ ZIP 無しに収束し、`Read-EvidenceTask` が通る（不合格例: disk が `delete-pending` で ZIP が無く、次の preflight が `changed-copy`）。昇格済み `ready`（`origin: stage`）の remote 削除でも同じ順序（1 ケース以上）。
    - (d) DELETE を失敗させる → `delete-pending` で残り ZIP は残る → 次の cleanup で (a) と同じ 2 終点。
  - **AC7**（I7）: active な task で stage → `use`（until = 終了予定 + 30 日 + 1 時間）→ AC1 の停止と回復 404 → `staged` で protection が残る → 時刻 `T` の ended event を `workflow-task end` の経路で適用 → 時計を `T + 30 日` の直後（until より前）へ進めた cleanup が `in-use`・DELETE 0 件 → `release` の後、同じ時計で remote 削除に進む。不合格例: active のまま時計だけ進めて `active/end-not-recorded` を `in-use` と取り違える。
  - **AC8**（S4, I8）: 昇格由来の `put-pending` の fetch。偽 GET 一致 → `ready` → remote 分岐で成功。GET 404 → `staged` → local 分岐で成功（`identity-mismatch` を返さない）。GET が別 bytes → `status: failed`・`reasonCode: unconfirmed`・取得 0 件・state 不変（`put-pending`）で、local 分岐へ fallback しない（内部の `unresolved-put` を公開 code にしない）。自分の回復できる `failed` の fetch も同じ 3 通り（別 bytes では state `failed` のまま）。呼び出し開始時に `staged` の artifact の fetch と stage は、既存ケースどおり transport と資格情報の読取りが 0 件。
  - **AC9**（I9）: PR #130 の runtime が作る shape（I9 の列挙。回復できる `failed` は AC3 の 3 variant）を持つ catalog と、origin の無い既存 catalog を新しい `Read-EvidenceTask` が読む。`origin: stage` の `delete-pending` で putIntent も receipt も無いものは既存どおり `receipt-missing`。
  - **AC10**（I3）: 再昇格の拒否。putIntent を持つ `staged` で、(a) intent file の SHA-256 が `putIntent.sha256` と違う → `unresolved-put`、(b) intent file の key・packageSha256・artifactId・packageBytes・schemaVersion のどれかが違う（sha256 は合わせ直した fixture）→ `unresolved-put`、(c) intent file が無い → preflight の既存の検査の code（B が実測して §6 に記録し、判定 C はその値で照合する）。いずれも PUT 0 件。receipt を持つ `staged` は `invalid-source`、終了済み task の putIntent を持つ `staged` は `resume-required`、いずれも PUT 0 件。
  - **AC11**（I5）: 再昇格の経路で、偽 transport の PUT 呼び出しの中で disk から読み直した catalog が `put-pending`・putIntent あり（同じ SHA-256）であること、readback の子 process が一致を返すまで disk に receipt と `ready` が無いこと（前スライスの AC12 と同じ方法）。readback を失敗させると `put-pending` のまま receipt 無し。
  - **AC12**（S5、判定必須）: §5 の手順で実 R2 に 1 回。
  - **AC13**（R9, R10）: README・`review-evidence.md` に再昇格の操作、R1〜R4・R6・R8 の規則、R3 の回復の範囲と解決できない間の結果、publish 側の再送禁止との区別、未送信 `staged` と putIntent を持つ `staged` の清掃契約、残存リスク、導入手順が書かれ、次の旧文言が `tools/Artifacts/README.md` と `.agents/skills/osm-workflow/` に残らない: README 冒頭の事前承認の「`staged` artifactのfetch（local分岐）・use・release・満了清掃も同じ承認に含む通常操作です。どれもnetworkと資格情報を使いません」（putIntent の有無で分けた文に置き換える）、「回復で `failed` になった昇格」（対象と拒否）、「GETが404なら `failed` になります」（PUT応答不明）、「`failed` で止まった昇格は、そのartifactからR2経由の取得も再昇格もできません」と「回復は後続スライス」（既知の制限）、「`ready` か `failed` に解決してから判定します」（fetch）、`review-evidence.md` の「昇格が `failed` で止まった場合は、そのartifactをremote用に使わず」。`docs-audit`・`contract-audit` が errors=0。
- ここでは答えない問いと所有する後続スライス:
  - catalog の組み合わせ検査の厳格化（revision 2 の D8 の残り）→ 診断の改善の入力（所有スライス未定）。
  - receipt 再利用の照合の拡張（3 field → 全 identity）、前回の試行が receipt file を書いた後に object が消えた場合の再昇格の失敗（R4。回復で自己回復する）→ 送信まわりの整理の入力。
  - 送信部の共有（`EvidenceSend.psm1`）と intent の `createdAt`・`process` の型検査 → 同上。
  - publish 由来の回復一致時の一律 adopted と `ownedCopies` 置換、publish の 404 → `failed` の residue 回収 → 診断の改善 / Content の容量上限・孤児回収の入力。
  - publish 由来の remote 削除の確定順序（GET 404 の後に copy を消してから `deleted` を書く現行の順序と、publish 由来の `deleted` の欠落 copy を許さない `Assert-EvidenceTaskPaths`）→ 診断の改善の入力。
  - 削除後に遅れて確定した object の回収（task prefix の一覧等。R6 の残存リスク）→ Content の容量上限・孤児回収の入力。
  - `put-pending` の回復が解決できない間に task 全体の network 付き操作が止まる既存挙動（R3 は `failed` だけを限定した）→ 診断の改善の入力。
  - Cloud・別 PC が R2 から fetch する経路と token → program §4 C（`Artifact Storage / Evidence の Cloud 配布`）。
  - `ready` の local copy からの network 無し fetch → program §4 A の後続。`use` の network 無し preflight → program §4 C の入力。
  - 端末喪失に備えた未昇格 `staged` の backup → Harness H3。
- 判定定義: GO = I1〜I10 を破る欠陥がなく、S1〜S4 が offline fixture と文書検査で成立し、S5 の実 R2 検証 1 回が判定必須テストとして縮めずに成立し、判定 C と C' がともに阻害 0。NO-GO = いずれかの不変条件の違反、S の不成立、または AC12 を凍結した手順で実行できない場合（Phase A または B へ差し戻し、実行方法の変更は再計画・再凍結してから評価する）。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。合格条件を弱めて GO にしない。AC12 が 2 回の試行で `passed` に達しなかった場合は、その task を active のまま残し（putIntent を持つ `staged` は旧 runtime でも fail-closed）、生記録とともに判断担当へ返す。
- A3 後の例外承認: なし。
- 決定記録: なし。
- 本文へ転記した実装制約: `#nullable` と `record` の規則は Unity 側のみで本スライスには当たらない。PowerShell は `Set-StrictMode -Version Latest`。reasonCode は既存の小文字 kebab で、新しい code を足さない。CLI の grammar と出力の形を変えない。path・key・資格情報を CLI 出力とログに出さない。テストで `Start-Sleep` / `Task.Delay` / `Thread.Sleep` を使わず時刻と transport を注入する。catalog に新しい state・新しい field を足さない。transport・`EvidenceContract.psm1`・`EvidenceSchedule.psm1`・`EvidenceReset.psm1` を変えない。資格情報の root・暗号文・DPAPI の利用を変えない。実ストアの catalog・transfers・資格情報を offline テストで読み書きしない。publish の関数（`Invoke-EvidencePublish`・`Initialize-EvidencePackage`）を変えない。既存テストの assertion を AC4 の表の範囲外で変更・削除しない。偽 transport の `duplicate-put` の検査は残す（re-PUT は 404 の後だけなので当たらない。当たるなら差し戻し）。
- 未決事項: なし（A1 の 4 点と A2 #10 は A3 で裁定済み）。

## 3. 責務マップ

| ファイル | 責務 | 変更理由 | 現在行数 → 予想 | 備考 |
|---|---|---|---|---|
| `tools/Artifacts/EvidenceCleanup.psm1` | preflight、put / delete の回復、清掃 | R1・R2（`Resolve-EvidencePutGuarded` の origin 分岐と、GET 前の過去 operation の停止確認）、R3（`Assert-EvidenceTransitionSafe` の回復を許す artifactId、cleanup の `failed` の item 単位の回復）、R6（`Get-EvidenceDeletionRoute`、cleanup の振り分け、ローカル削除内の再確認、照合 GET 一致 → `staged`、`origin: stage` の remote 削除の確定順序を `Invoke-EvidenceDeleteGuarded` と `Resolve-EvidenceDeleteGuarded` の両方で変更）、R3 の cleanup の失敗後の扱い | 369 → 約 425 | 500 行未満。同居の理由: 回復と削除経路の選択は、どちらも task guard 下で catalog の state 遷移を所有する同じ所有者・同じ依存（StateStore・RetentionPolicy・transport）を持つ。経路の選択は副作用の無い内部関数で、回復のコードを変えずに単体テストと変更ができる。保持 policy（R5）は「削除してよいか」、経路（R6）は「どの手段で削除するか」を答え、別 file に置く。Application を import しない |
| `tools/Artifacts/EvidenceApplication.psm1` | publish / stage / promote / fetch / inspect / use の orchestration | R3（promote・fetch が preflight に artifactId を渡す）、R4（promote の送信条件と intent 再利用の照合）、R8（fetch の取得元と reasonCode） | 316 → 約 332 | publish・stage・inspect・use・release は変えない |
| `tools/Artifacts/EvidenceRetentionPolicy.psm1` | 純粋な保持 policy | R5 | 68 → 約 70 | 削除経路は持たない |
| `tools/Artifacts/EvidenceStateStore.psm1` | catalog の読戻し検査 | R7 の 1 条件 | 144 → 約 145 | 新 state・新 field なし |
| `tools/Artifacts/README.md` | 公開仕様 | R10 | 460 → 約 480 | |
| `.agents/skills/osm-workflow/references/review-evidence.md` | レビュー証拠の保存・配布 | R9 | 172 → 172 | 置換のみ |
| `tools/Artifacts/tests/ArtifactEvidence.Tests.ps1` | promote・fetch の offline 検証 | AC1〜AC3・AC7・AC8・AC10・AC11 と AC4 の表 | 499 → 約 620 | 500 行の警報を超える。ケースの列挙で、変更理由・依存（偽 transport・FaultHook・隔離 store）・テスト方法が既存の promote ケースと同じなので非分割 |
| `tools/Artifacts/tests/EvidenceCleanup.Tests.ps1` | 回復・清掃の offline 検証 | AC3 (d)・AC4・AC5・AC6・AC9 | 505 → 約 590 | 同上の理由で非分割 |
| `tools/Artifacts/tests/EvidenceRetention.Tests.ps1` | 保持 policy の単体テスト | R5、AC4 の表の 1 行 | 88 → 約 95 | |

状態の所有者は task の catalog（task guard が直列化）。寿命は既存の task 終了 + 30 日と staging の 7 日。新しい公開面は無い（CLI の grammar・reasonCode・出力の形は変えない）。module 間の新しい受け渡しは `Assert-EvidenceTransitionSafe` の省略可能な引数 1 つ（Application → Cleanup の既存の向き）。新しい file は無い。asmdef・Unity 側の変更なし。`EvidenceSchedule` の runtime 収集は変えない。

## 4. 実装計画

- 順序: (1) `EvidenceStateStore` の R7 と AC9 → (2) `EvidenceRetentionPolicy` の R5 と AC4 の表の retention の 1 行 → (3) `EvidenceCleanup` の `Get-EvidenceDeletionRoute` と R6 の振り分け・照合 GET・`origin: stage` の remote 削除の確定順序（両経路）→ (4) `Resolve-EvidencePutGuarded` の R1・R2 と R3（preflight の引数と cleanup の item 単位の回復）→ (5) promote の R3 の引数と R4 → (6) fetch の R3 の引数と R8 → (7) AC4 の表の既存 assertion の更新と新規ケース → (8) README（R10）と `review-evidence.md`（R9）→ (9) `contract-audit`・`docs-audit`・AC13 の grep。(2) と (3) は C1 により同じ commit に入れる（(2) だけでは putIntent を持つ `staged` がローカル削除に乗る）。(4) も同じ commit に入れる（(4) だけでは putIntent を持つ `staged` が `unknown-state` のまま削除不能になる）。
- 導入手順（R10。README に書く。B は書くだけで実行しない）: 本スライスの head で `schedule install`（既存の切替手順）。
- Phase B から Phase A へ差し戻す条件: 新しい state・field・reasonCode、CLI の grammar・出力の形の変更、transport の変更、publish の関数の変更、`EvidenceContract` / `EvidenceSchedule` / `EvidenceReset` の変更が必要になったとき。AC4 の表に無い既存 assertion を変更・削除しなければ通らないとき（B は無言で追認しない）。I9 の shape のどれかを読めなくしないと R7 を実装できないとき。`Resolve-EvidencePutGuarded` の origin 分岐で、publish 由来の経路の挙動を変えずに R1・R2 を実装できないとき。R3 を `Assert-EvidenceTransitionSafe` の引数 1 つで実装できないとき。R3 の cleanup の失敗後の扱い（その task の残りを `remaining`）を、cleanup の結果の形や cursor の意味を変えずに実装できないとき。`origin: stage` の確定順序の変更が publish 由来の削除の順序や `Assert-EvidenceTaskPaths` の欠落 copy の許容範囲の変更を要するとき。偽 transport の `duplicate-put` の検査を外さないとテストが書けないとき。
- 対象外を維持する方法: publish 由来の分岐は origin で先に分け、既存のコードを通す（AC4）。stage と、呼び出し開始時に `staged` の fetch の経路に `Invoke-ArtifactNetwork` と `Get-CredentialStatus` を置かない（AC8）。catalog 検査の厳格化をしない（R7 は緩和 1 件だけ）。

## 5. テストとレビュー計画

- 単体テスト: `Get-EvidenceRetentionDecision` の R5（`EvidenceRetention.Tests.ps1`）。`Get-EvidenceDeletionRoute` の全組と `Read-EvidenceTask` の R7（`EvidenceCleanup.Tests.ps1`）。回復・再昇格・削除の手順は偽 transport・FaultHook・`ProcessHook`（identity ごとに停止済み / 稼働中を返す形を含む）・`WriteHook`・時刻注入で検証する。停止の後の判定は常に disk から読み直した catalog で行う。
- 差し戻し中の起点 `-Filter`: `tools/Artifacts/tests/ArtifactEvidence.Tests.ps1 -Case <新規ケースと AC4 の表のケース>`、`EvidenceCleanup.Tests.ps1 -Case <新規ケース>`、`EvidenceRetention.Tests.ps1`。C は凍結済み条件の確認に必要なら根拠を記録して変えてよい。
- 判定必須テスト（GO 候補 head）: `tools/Artifacts/tests/*.Tests.ps1` と `tools/Workflow/tests/*.Tests.ps1` の全体（各 file を 1 process。`StoreContext.Tests.ps1` は非仮想化 host で。`EvidenceCleanup.Tests.ps1` は `rg` が PATH に要る）、3 project の Release build、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC13 の旧文言 grep、AC4 の既存テストの diff 確認（base..head の既存テスト file の変更・削除行が AC4 の表の行だけであること）、AC12 の実 R2 検証 1 回。
- 全 EditMode 回帰の適用除外: Unity 側のコードを変更しないため。代替証拠は上記 offline suite 全体と audit。
- 統合・Unity テスト: なし。
- 高コスト証拠: なし（AC12 は既存 promote・fetch と同じ endpoint・資格情報・事前承認の範囲で、3 file・通常清掃で可逆なので判定必須テストに含める。前スライスの AC10 と同じ扱い）。
- 操作・実行時・目視条件の検証経路: AC12。動作確認（Phase B の後）は `対象なし`（実 R2 は判定 C の実行担当が GO 候補 head で行い、B の head では行わない）。
- AC12 の手順（実 R2 検証 1 回）:
  - 担当: 判定 C の実行担当（判断担当と分ける場合は新しい実行担当 session）。環境: 非仮想化 host の sandbox 外 PowerShell 7（Claude の Bash は LocalAppData が仮想化されるので使わない）。GO 候補 head の detached worktree で 3 project を Release build。登録済みの日次 job の runtime は変えない（R10）。
  - 手順:
    - (0) 開始条件: `pwsh tools/artifacts.ps1 evidence schedule status --profile osm --config <cfg>`（読み取りだけ）の結果 JSON を記録する（返る field は `status`・`scheduler`・`job`・`runtime`・`manifestSha256`。`scheduler.lastRun` / `scheduler.lastResult` と `job.lastRun` / `job.status` を含む）。登録済み runtime の head は `runtime` の最後の directory 名（install 時の implementation head）で、PR #128 のマージ commit `d911e4b4b662e66fd3fcc54f6b2586c381fabf4a` がその祖先であることを `git merge-base --is-ancestor d911e4b4b662e66fd3fcc54f6b2586c381fabf4a <runtime head>` で確かめる（hash の大小や日時では判定しない）。`status` が `passed` でない、head を読めない、祖先でないのいずれかなら実 R2 を始めず、「AC12 未実行（GO 判定不可）」として判断担当へ返す。AC12 を省略して GO にしない（判定定義）。
    - (1) 新しい task id（`evidence-promote-recovery-rt-<n>`）で `pwsh tools/workflow-task.ps1 start`。
    - (2) 非秘密の固定 fixture（3 file、うち 1 つはバイナリ）を selection にして `evidence stage`。出力の reference と packageSha256 を記録。
    - (3) 1 つの pwsh process で次を行う（凍結前に隔離環境で同じ形の疎通を確認した。下記）: `Set-StrictMode -Version Latest`、`$ErrorActionPreference='Stop'`、`$WarningPreference='SilentlyContinue'`。`tools/Workflow/WindowsStoreContext.psm1` を import して `Get-OsmStoreContext` が `not-virtualized` であることを確かめ、違えば何もせずに止める。`tools/Artifacts/EvidenceApplication.psm1` を import し、`& (Get-Module EvidenceApplication) { $script:FaultHook = { param($p,$a) if($p -ceq 'promote-after-put-pending'){ throw 'ac12-stop' } } }` で停止点を入れ、`Invoke-EvidencePromote <config> <reference> <packageSha256>` を呼ぶ。stdout には結果を `ConvertTo-Json -Compress` した 1 行だけを出す。直後に catalog と intent file の写しを取る（写しはローカルの絶対 path を含むので、非公開の判定 evidence にだけ置き、CLI の公開出力・Git・PR 本文に載せない）。
    - (4) 別 process で CLI の `evidence fetch`（preflight 前は `put-pending` なので network 付き）。直前に `schedule status` を再度取り、`scheduler.lastRun` と `job.lastRun` を (0) と比べて、(3) と (4) の間に日次 job が走ったか（R1 と R3 のどちらを通ったか）を evidence に残す。
    - (5) `evidence inspect --task <task id>`。
    - (6) CLI の `evidence promote`（(2) の reference と packageSha256）。`passed` で終わらなければ同じコマンドを 1 回だけ再実行し、各回の結果を記録する。2 回目も `passed` でなければ止め、task を active のまま残して生記録とともに判断担当へ返す。
    - (7) `evidence inspect`。(8) 別 process で `evidence fetch`（remote 分岐）。(9) 同じ promote を再実行。
    - (10) 原記録（各結果 JSON、各時点の catalog・intent・receipt の写し、fixture の hash 一覧、(0)(4) の status）を非公開の判定 evidence に収録し、task を通常の最終手順で end する（R2 object は task 終了 + 30 日の通常清掃が消す。事前承認の範囲）。
  - 実装から独立した期待値: (3) が `status: failed`・`reasonCode: unconfirmed`・`residue.remote: pre-put`、catalog の対象が `put-pending` で putIntent あり。(4) が `passed`（回復 GET の 404 で `staged` に戻り local 分岐。R2 に object が無いことの観測。(3)〜(4) の間に日次 job が走っていれば R3 の経路で同じ結果）。(5) の対象が `staged`・`locality: local`、putIntent の SHA-256 が (3) と同じ。(6) は再実行しなければ `complete`、再実行した場合は最後の結果が `complete` か `already-promoted` のどちらか（どちらも合格。再実行の有無と各回の結果を原記録に残す）。reference 文字列が (2) と完全一致、packageSha256 が (2) と一致。(7) が `ready`・`locality: promoted`。(8) が `passed` で、展開した全 file の SHA-256 が fixture の hash 一覧と一致。(9) が `passed`・`already-promoted`。intent file の SHA-256 が (3)・(5)・(10) で同じで、receipt の `putIntentSha256` と一致し、receipt の readback の hash が packageSha256 と一致。catalog の artifactId・key・base・head・manifestSha256・packageBytes・entries が (2) 直後と (10) で一致し、(10) の `ownedCopies` が (2) 直後の `ownedCopies` を全て含む。
- 未知の操作経路の疎通結果: AC12 の手順 (3)（CLI を通さず module を直接呼び、`Get-Module` 経由で module 内の `$script:FaultHook` に注入して promote を止める）を、2026-10-17 に A3 の主担当が隔離環境で確認した（git 管理外の scratchpad の probe script。実ストア・実資格情報・network は不使用）: `New-EvidenceTestEnvironment` の隔離 temp store・偽 transport・偽の資格情報 status で stage した後、`& (Get-Module EvidenceApplication) {...}` で hook を入れて `Invoke-EvidencePromote` を直接呼ぶと、`Get-Module EvidenceApplication` は 1 個、hook は `promote-before-lock` → `promote-after-intent-file` → `promote-after-put-pending` の順に呼ばれ、結果は `failed`・`unconfirmed`・`pre-put`、disk の catalog は `put-pending`・putIntent あり・artifact の `operation` null、PUT 0 件。全ての TestRoot は temp 配下だった。同じ host の `Get-OsmStoreContext` は `not-virtualized`。観測した注意: module の import で未承認動詞の警告が出る（手順 (3) は `$WarningPreference` と結果 JSON 1 行だけの出力で対処。前スライスの実行担当が警告行で JSON 解析に失敗した経緯がある）。実 R2 での 404 の観測は判定 C の必須証拠のまま。AC12 を凍結した手順で実行できない場合は NO-GO とし、Phase A または B へ差し戻す（S5 を縮めない）。
- 人間の判断が必要な条件: なし。
- 判定 C の照合 script の方式: **判定 C の offline 実行**。判定 C の担当が、AC12 の原記録の複製に対して、資格情報・本番設定・network に届かない環境で照合 script を書いて実行する。適用前に合成 fixture で一度通す。凍結する合成 fixture と期待値: 合格 2 組（(6) を 1 回で `complete`、(6) を再実行して最後が `already-promoted`）と不合格 4 組（reference 文字列が 1 文字違う、intent file の SHA-256 が (3) と (10) で違う、(5) の state が `failed`、(8) の file hash が 1 件違う）。script は合格 2 組を合格、不合格 4 組をそれぞれ不合格と判定しなければならない。
- 機械検査: `contract-audit`、`docs-audit`、`git diff --stat`、AC13 の grep、AC4 の既存テストの diff 確認。
- A0/A1 主担当・モデル・ベンダー: Claude Opus 5.5（Anthropic、この session）。
- A2 独立レビュー: 実施済み（§5.1）。観点は (a) アーキテクチャゲート、(b) 不変条件 I1〜I9 を破る順序の探索と未決事項 1〜4。A0 だけからの代替構成は、同じ問いを前スライスの A2 (c) が出しており（新 state `promote-pending` で再送を禁じる案。§2 の却下理由）、再度は行わない。
- 外部レビュー: §5.1 までの版に対して Web 版 GPT（owner が依頼）。結果と対応は §5.2。独立 A2 には数えない（A2 の採否案を読んでいる）。
- A3 統合担当・モデル・採否: 主担当（Claude Opus 5.5）。owner の指示（2026-10-17、外部レビューを「妥当だと思ったら受け入れて修正して」）に基づき、§5.1・§5.2 の採否を主担当が判断して revision 2 に統合した。凍結は owner の本文確認後。
- C' 用に予約した担当・モデル・ベンダー: Grok 系（cursor-agent）。A2 と外部レビューでは使っていない。
- 独立性の強化条件を満たせない場合の理由: A2 の (b) は A1 主担当と同じ Claude 系（別 context の新規 session）。(a) と外部レビューは別ベンダー。

## 5.1 A2 独立レビュー結果と A3 の採否案

入力版は A1 初稿（commit `bd7ec63436a66c2a7c0e27e6d860d7f4d154a08e`、内容 sha256 `7d6c88bd946637e7b9888ea09feadd39e6f90e25aba66f1547ba3eb73fa1600b`、LF の git blob 内容）。2 件は 2026-10-17 に互いの指摘を見せずに実施した。どちらもこの PC 内の read-only 実行で、ファイル変更・テスト・network・資格情報・実ストアは使っていない。prompt には「計画は未実装で、現コードとの差は実装対象」と明記した。原記録（指示 file と結果）は A1 主担当 session の scratchpad（git 管理外）にある。担当・モデルは使用実績で、固定割り当てではない。

| 担当 | モデル | 判定 |
|---|---|---|
| (a) アーキテクチャゲート | Codex CLI（OpenAI、`gpt-6-astra`） | NO-GO（A-1 で §4 の差し戻しに初手で当たる。A3 の文言修正で解消） |
| (b) 不変条件を破る順序 | Claude Opus 系（新規 subagent context） | 条件付き GO（I2・I4・I5・I7・I9 を破る順序は構成できず。AC4 と R1 の文言修正が必要） |

主担当が実コードで確認した事実: `ArtifactEvidence.Tests.ps1` の PR #130 の promote ケースは、404 の後の `failed`・`invalid-source`・fetch の `identity-mismatch`・`unconfirmed-artifact`、回復一致時に停止した promote の readback staging が adopted になることを期待値として固定している（112・115〜116・175〜181・216〜222・230・240・289 行目付近）。いずれも本スライスが意図して変える `origin: stage` の挙動である。

### findings と主担当の採否案

| # | 指摘（担当） | 分類 | 提案する反映 |
|---|---|---|---|
| 1 | I1 第 2 文・AC4 の「既存テストの変更は `staged-unsent-required` の 1 行目だけ」は達成できない。PR #130 の promote ケースが旧挙動を固定しており、B は §4 の差し戻し条件に初手で当たる（a A-1 / b B-1） | 採用 | I1 第 2 文を「publish 由来・origin の無い artifact を対象とする既存ケースは無変更」に改める。AC4 に、変えてよい既存 assertion を file・ケース・旧期待 → 新期待で全件列挙する（上の 7 群と `staged-unsent-required` の 1 行目）。§4・§5 の diff 確認はこの列挙との一致で判定する |
| 2 | R1 を文字どおり実装すると、今回の recovery staging（自 process の operation）も停止確認にかかり、404 の回復が常に `operation-pending` で止まる（a A-2 / b B-2）。R2 の「他の staging は operation を外すだけ」に停止確認が無い（b B-11） | 採用 | R1・R2: 今回登録した recovery staging と artifact の `operation` は処理完了を根拠に外す（現行と同じ）。それ以外の同じ artifactId の staging の `operation` は、GET の前に `Assert-EvidenceOperationStopped` を通ったものだけ外す（通らなければ GET 前に `operation-pending`）。offline に「過去 process 停止済み / 稼働中」×「404 / 一致」のケースを足す（`ProcessHook` を identity ごとに返す形） |
| 3 | `EvidenceCleanup.psm1` に put 回復の origin 分岐と削除経路の選択を同居させる根拠が行数とテスト可能性だけ（a A-3） | 採用（同居は維持） | §3 に、両者が同じ所有者（task guard 下の catalog の状態遷移）と同じ依存を持ち、経路の選択は副作用の無い内部関数で回復と独立して変えられることを書く。保持 policy は「削除可能か」、経路は「どの手段で削除するか」と役割を明文化する |
| 4 | R4 と I3 をどの時点の state で判定するかが無い。1 回の promote の preflight が `put-pending` / `failed` を `staged` に戻し、同じ呼び出しで PUT する（b B-3） | 採用 | R4・I3 に「判定は preflight と Sync の後の state で行う（R8 と同じ原則）」と書く。AC2 に「`put-pending` への 1 回の promote で GET 404 → `staged` → PUT 1 回 → `ready`、呼び出し内の PUT は 1 回」を足す |
| 5 | AC3 で合成する `failed` の shape が PR #130 の実出力と違う（実際は readback staging の `operation` が非 null で残る。7 日清掃で staging が既に消えた shape もある）（b B-4） | 採用 | AC3 を 3 variant で回す: (i) readback staging の `operation` が非 null（停止済み）、(ii) staging 2 件が catalog から削除済み、(iii) 全 null |
| 6 | AC8 の別 bytes と AC10 の reasonCode が曖昧（remote 側は今 `unconfirmed`。`unresolved-put` に合わせると公開 code が変わる）（b B-5） | 採用 | AC8 の別 bytes は `status: failed`・`reasonCode: unconfirmed`・取得 0・state 不変と固定。AC10 は (a)(b) `unresolved-put`、(c) は既存 preflight の code を B の実測で固定し §6 に記録 |
| 7 | AC12 の「旧 runtime は無害」は登録済み job の head・稼働状況に依存するのに確認手順が無い（b B-6） | 採用 | AC12 の手順 (0) に読み取りだけの `schedule status`（head・lastRun・result）を記録し、head が `origin: stage` を読める（PR #128 以降）ことを開始条件にする。満たさなければ判断担当へ返す。(3)〜(4) の間の lastRun を記録し、R1 と R3 のどちらを通ったかを evidence に残す（合否は同じ） |
| 8 | AC12 (6) を再実行した場合の期待値が無い（b B-7） | 採用 | 再実行した場合は `complete` と `already-promoted` のどちらも合格とし、再実行の有無を原記録に残す。照合 script の合成 fixture にこの組を足す |
| 9 | README の旧文言の列挙が足りない（9 行目の「満了清掃も network を使わない」は R6 で偽になる、93・96・98 行目）（b B-8） | 採用 | AC13 の旧文言に 9・93・96・98 行目の該当句を加える。事前承認の文に「putIntent を持つ `staged` の満了清掃は remote DELETE を使い、通常清掃と同じ承認範囲」と書く |
| 10 | R3 で、`origin: stage` の `failed` の回復 GET が別 bytes・通信エラーになると、同じ task の publish・use・remote fetch が `unresolved-put` で止まるようになる（今は `failed` が不活性で成功）（b B-9） | 採用（受容して明記） | `put-pending` と同じ fail-closed として受容し、README の「PUT 応答不明」に書く。I1 を「publish 由来の artifact 自身の判定」と範囲を限定して書く。受容の可否は A3 で owner が決める（下の 2） |
| 11 | §1・R1・R6 の行番号が実コードとずれている（330・293 行目）（b B-10） | 採用 | 行番号を直す（関数名を主にする） |
| 12 | AC12 (3) の具体化不足: 同じ process での `Get-OsmStoreContext`（`not-virtualized` 以外なら停止）、`Set-StrictMode -Version Latest`・`$ErrorActionPreference='Stop'`、出力は結果 JSON だけ、catalog・intent の写しは絶対 path を含むので非公開の判定 evidence にだけ置く（b B-12） | 採用 | 手順 (3) に書く。(b) は実行可能と判断し、凍結前の疎通は不要とした。「未確認。初回確認は Phase C」と不成立時の縮小案は維持 |

指摘なしで確認された観点（(a)）: 依存方向（Application → Cleanup / StateStore / RetentionPolicy）、Unity・実ストアなしでの検証可能性、§1 の主要な関数名と現況、行数と test file の非分割根拠、publish 分岐の維持と catalog 緩和の限定。

### A3 で owner が決めること

1. **#1〜#9・#11・#12 の採用**（全て文言・受け入れ条件の修正で、方式は変えない）。主担当案は採用。
2. **#10 の受容。** `origin: stage` の `failed` の回復が解決できない間、同じ task の network 付き操作（publish・use・remote fetch・promote）が `unresolved-put` で止まる。主担当案は受容（`put-pending` と同じ fail-closed。止まった task は回復 GET が解決すれば戻る）。代替は「R3 の回復は cleanup と対象 artifact 自身への promote・fetch だけが行い、他の artifact の preflight では触れない」で、preflight の分岐が増える。
3. **§2 未決事項 1〜4。** (b) の意見: 1 は賛成、2 は #7 の確認を開始条件にすれば賛成（AC12 が 2 回の失敗で止まった場合は task を active のまま残し、本スライスの install の後に解決すると停止規則に書く）、3 は実行可能（#12）、4 は維持に賛成（削除後に task prefix の一覧で回収する方法は後続入力）。主担当案は (b) の意見どおり。

A3 の結果（2026-10-17）: 1 と 3 は主担当案で採用。2 は外部レビュー P1-1 により「受容」を取りやめ、`failed` の回復を対象 artifact 自身の操作と cleanup に限る方式に置き換えた（§5.2）。未決事項 3 は凍結前に隔離環境で疎通を確認した（§5）。統合した revision 2 は §2〜§5。

## 5.2 外部レビューと revision 2 への統合

**経緯（2026-10-17）。** owner は A2 の記録（§5.1 までの版、commit `50d72b13`）を Web 版 GPT に渡し、計画レビュー・A3 修正依頼（判定「条件付き GO。A3 で計画修正と owner 裁定を済ませるまで Phase B に進めない」）を受け取って、「妥当だと思ったら受け入れて修正して」と主担当に指示した。照合基準は `develop` = `498d0da8`（統合時に再確認し、変化なし）。原文は owner の手元（git 管理外）にある。レビューは維持すべき設計として、`origin: stage` だけの 404 → `staged`、同じ bytes の再 PUT と 1 呼び出し 1 PUT、putIntent を持つ `staged` の remote 削除と C1、fetch の取得元を preflight 後に決めること、新しい state・field・grammar・reasonCode・transport を足さないこと、Cleanup の内部関数の同居、削除後の遅延 PUT の受容を挙げた。

| ID | 指摘 | 採否 | 反映 |
|---|---|---|---|
| P1-1 | R3 / A2 #10: `failed` を task 全体の preflight で回復すると、その回復が解決しない間、同じ task の無関係な artifact の操作が止まる（新しい故障の波及）。回復対象を絞る方式を第一候補に再裁定する | 採用（A2 #10 の採否案を置き換え） | R3 を「回復できる `failed` は対象 artifact 自身の promote・fetch と cleanup だけが回復する」に変更。`Assert-EvidenceTransitionSafe` に回復を許す artifactId を 1 つ渡す。cleanup は item 単位で失敗を閉じ、task の残りを続ける。I10 と AC3 (d)(e) を追加。依存の変化を §3 に記載 |
| P1-2 | AC12 / S5: 実行できない場合に S5 を縮める逃げ道を削除し、NO-GO と差し戻しにする。凍結前に隔離環境で FaultHook の入口を確認する | 採用 | S5・判定定義・停止規則から縮小を削除し、実行不能は NO-GO（Phase A / B へ差し戻し）。§5 に隔離環境の疎通結果を記録（凍結前に実施） |
| P1-3 | R1 / R2 / A2 #2: 過去の operation の停止確認を recovery staging の登録と GET の前に行い、確認できなければ catalog を変えない順序を固定する | 採用 | R1 を手順 1〜3 に書き直し、R2 も同じ手順を使う。I6 に「停止を確認できないのに登録・GET・書込みが起きる」を追加。AC3 (i) に稼働中の場合（`operation-pending`、GET 0 件、generation 不変）を追加 |
| P2-1 | receipt 無し `delete-pending` の停止と再開（永続化境界での停止、別 process からの再開）を検証する | 採用 | AC6 に (a) `after-intent`、(b) `after-remote`、(c) `deleted` の書込み前、(d) DELETE 失敗の 4 つの停止と再開を追加。I2 に「`deleted` の永続化前に `ownedCopies` を消さない」を追加 |
| P2-2 | 旧期待を固定した既存 assertion の変更範囲を file・ケース・旧期待・新期待・理由で全件列挙する | 採用（A2 #1 と同じ） | AC4 に変更表。表の外の変更・削除は §4 の差し戻し条件。I1 を publish 由来・origin 無しの assertion の不変と表の範囲に限定 |
| P2-3 | AC12 の実行前提（`schedule status` の開始条件、旧 runtime との関係、`Get-OsmStoreContext`、strict mode、再実行時の期待値、非公開の写し）を本文へ転記する | 採用（A2 #7・#8・#12 と同じ） | §5 の AC12 の手順 (0)〜(10) と期待値・照合 script の合成 fixture に反映 |
| P2-4 | AC8 / AC10 の reasonCode と README の旧文言を曖昧にしない | 採用（A2 #6・#9 と同じ） | AC8 の別 bytes は `unconfirmed` と固定、AC10 の各 code を固定（(c) は B の実測）。AC13 の旧文言を 6 句に拡張し、README の清掃契約を putIntent の有無で分ける（R10） |

### revision 1（A1 初稿）から revision 2 への差分一覧

- **変更した意思決定:**
  - R1: 過去の operation の停止確認を GET と recovery staging の登録の前に置き、今回の処理の operation は完了を根拠に外す（A2 #2、P1-3）。
  - R2: R1 と同じ手順を使い、adopted にするのは今回の recovery staging だけ（変更なし）と、過去の staging の operation を外す根拠を明記（A2 #2・B-11）。
  - R3: `failed` の回復を task 全体から、対象 artifact 自身の promote・fetch と cleanup の item 単位に限定（P1-1。A2 #10 の受容案を却下）。
  - R4・I3: 判定は preflight と Sync の後の state で行い、1 回の promote の中で 404 → `staged` → PUT を含む（A2 #4）。
  - R10: 実 R2 の前の `schedule install` をやめ、読み取りだけの `schedule status` を開始条件にする（未決 2、A2 #7）。README の事前承認と清掃契約を putIntent の有無で分ける（A2 #9、P2-4）。
  - 判定定義・停止規則: AC12 が実行できない場合の S5 の縮小を削除し、NO-GO と差し戻しにする（P1-2）。
- **変更した不変条件・成立条件・受け入れ条件:** I1（範囲の限定と AC4 の表）、I2（`deleted` の永続化前に local copy を消さない）、I3（判定時点）、I6（GET 前の停止確認）、I9（`failed` の variant）、I10（新規。無関係な artifact へ波及しない）、S2（回復の範囲）、S3（停止からの収束）、S5（縮めない）。AC2（同じ呼び出しの中の回復と再送、object が在る場合）、AC3（3 variant、波及の確認、稼働中の process）、AC4（変更表）、AC6（停止と再開 4 種）、AC8（別 bytes の code）、AC10（code の固定）、AC12（手順 (0)(3)(4)(6) と期待値）、AC13（旧文言 6 句）。
- **残存リスク:** 削除の後に遅れて確定した先行 PUT の object が R2 に追跡外で残りうる（R6。README に保証外と明記して受容。回収は後続の孤児回収）。回復できる `failed` の回復が解決しない間、その artifact 自身の fetch・promote は失敗し続け、cleanup はその item を `unconfirmed-artifact` で残す（他の artifact は止めない）。`put-pending` の回復が解決しない間に task 全体の network 付き操作が止まる既存挙動は変えない（後続入力）。receipt file を書いた後に object が消えた場合の再昇格は失敗し、次の回復で自己回復する（R4）。旧 runtime が receipt の無い `delete-pending` を読めずその task の清掃が止まる（導入手順の `schedule install` で解消。fail-closed）。
- **Phase B に渡す実装境界:** 変更してよい file は §3 の表の 9 つだけ。publish の関数・transport・`EvidenceContract` / `EvidenceSchedule` / `EvidenceReset`・CLI の grammar と出力・reasonCode・catalog の state と field は変えない。module 間の新しい受け渡しは `Assert-EvidenceTransitionSafe` の省略可能な引数 1 つだけ。既存テストの変更は AC4 の表の範囲だけ。差し戻し条件は §4。

## 5.3 revision 2 への再レビューと revision 3 への局所修正

**経緯（2026-10-17）。** owner は revision 2（commit `4ba3652a`）を Web 版 GPT に再度渡し、「A3 に差し戻し（局所修正後、owner による凍結確認可）。設計全体のやり直しは不要」の結果を受け取って、主担当に改めての確認を依頼した。主担当は指摘 3 件を develop `498d0da8` のコードで確かめ（全て事実どおり）、自分でも C / C' で指摘されそうな箇所を見直して 1 件を加えた。方式（R1〜R5・R7〜R10）は変えていない。

| ID | 指摘 | 確認 | 反映 |
|---|---|---|---|
| RR-P1 | R6 の「`deleted` を書く前に `ownedCopies` を消さない順序は既存のまま」は誤り。`Invoke-EvidenceDeleteGuarded` も `Resolve-EvidenceDeleteGuarded` も、GET 404 の後に `Remove-EvidenceOwnedCopies` を `Write-EvidenceTask` より先に行う（対照の `Invoke-EvidenceLocalDeleteGuarded` は永続化が先）。その間で止まると disk は `delete-pending` で ZIP が無く、次の preflight が `changed-copy` になり、I2・AC6(c) を破る。`Resolve-EvidenceDeleteGuarded` の `finally` の書込みの扱いも固定する | 事実どおり（コードで確認。加えて、`Write-EvidenceTask` が書込み前にメモリ上の generation を進めるので、失敗の後の `finally` の書込みは `state-conflict` になりうることを確認） | R6 に確定の順序を新設（`origin: stage` の両経路で「`deleted` を書く → copy を消す → 残りを書く」。publish 由来は I1 により現行のまま、後続入力）。失敗した書込みの後の安全条件（disk が `delete-pending` なら ZIP は残る / `deleted` なら残ってもよい）を R6 と AC6(c) に書き、AC6(c) を 2 経路 × `before-rename` / `after-rename` の 4 ケース + 昇格済み `ready` に拡張。I2 を `origin: stage` の remote 削除に範囲を明示。§3・§4 に反映（変更 file は増やさない） |
| RR-P2a | AC6(a) の「次の cleanup で `staged` に戻る」は、照合一致の直後に同じ cleanup の中で保持判定をやり直して再 DELETE する現行の制御フローと合わない | 事実どおり（`Invoke-EvidenceCleanup` の artifact の処理で、削除 intent の回復の直後に保持判定と削除が続く） | 現行の制御フローを採る。AC6(a) を「照合一致の時点で `staged`・ZIP 保持 → 同じ cleanup で再 DELETE してよい → 最終は `deleted`」と途中と最終に分けて書き、R6 に制御フローを変えないことを明記 |
| RR-P2b | AC12 (0)(4) の `schedule status` の field 名（head・lastRun・result）が実際の戻り値と違う。runtime の head は Git の祖先関係で判定する | 事実どおり（`Get-EvidenceScheduleStatus` は `status`・`scheduler`・`job`・`runtime`・`manifestSha256` を返す。PR #128 のマージ commit は `d911e4b4`） | AC12 (0) を実際の field と `git merge-base --is-ancestor` に書き直し、不成立は「AC12 未実行（GO 判定不可）」。(4) を `scheduler.lastRun` / `job.lastRun` の比較に |
| RR-A | （主担当の再確認）R3 の cleanup の item 単位の回復は、回復が一致の後にメモリ上の state・receipt・`ownedCopies`・adopted を書き換えてから例外を投げうる。そのまま同じ task の残りの item を処理すると、後続の書込みがその未確定の状態を disk に持ち込む（例: `ready` だが recovery staging が未採用で、7 日清掃が `ownedCopies` の file を消す）。書込み失敗の後なら generation のずれで後続が毎回 `state-conflict` になる | 主担当がコードで確認（`Resolve-EvidencePutGuarded` の一致の経路は state を `ready` にしてから `ownedCopies` の列挙と最後の書込みを行う） | R3: 例外の後はその task のメモリ上の状態を使わず、その実行では残りの item を `remaining` に数えて次の task へ進む。試行の前に永続化した cursor により、次の実行は失敗した item の次から始まり、他の artifact は毎回処理される。AC3 (d) にこの順序と、一致の後の書込み失敗が後続の書込みで `ready` を持ち込まないことを追加。§4 の差し戻し条件に追加 |

主担当の再確認で、他に C / C' で問われそうな点として次を見た（変更なし）: R1 の過去の operation の停止確認は task guard の下なので、同じ artifact の fetch・promote と並行しない。AC12 の手順 (3) の後、`Complete-EvidencePromoteFailure` が readback の子の居ない promote の operation を外すので、(4) の停止確認は通る。回復できる `failed` の variant (ii)（staging が 7 日清掃で消えた形）は、`failed` の staging が清掃の除外に入らない現行の挙動で実際に生じる。putIntent を持つ `staged` の R2 上の object が無い場合の DELETE は R2 の通常応答（成功）で、続く GET 404 で `deleted` になる。

### revision 2 から revision 3 への差分一覧

- **変更した意思決定:** R6 に `origin: stage` の remote 削除の確定順序（実装変更、両経路）と、照合一致の後に同じ cleanup で再 DELETE する現行の制御フローの維持を追加。R3 の cleanup の失敗後の扱い（その task の残りを `remaining`、cursor の回転で他の artifact を処理）。
- **変更した不変条件・受け入れ条件:** I2（範囲を `origin: stage` の remote 削除に明示）。AC3 (d)（失敗の後の順序と、未確定の状態を持ち込まないこと）。AC6 (a)（途中と最終）・(c)（2 経路 × 2 注入点 + `ready`、disk の再読込みでの安全条件）。AC12 (0)(4)（実際の field と祖先関係）。
- **残存リスク（追加）:** publish 由来の remote 削除は、GET 404 と `deleted` の書込みの間で止まると次の preflight が `changed-copy` で止まる現行の挙動のまま（I1。後続入力）。解決しない回復できる `failed` がある task では、cleanup がその実行の残りの item を `remaining` に回すので結果が `pending`（`cleanup-partial`）になる。
- **Phase B に渡す実装境界:** 変わらない（変更してよい file は §3 の 9 つ）。`Invoke-EvidenceDeleteGuarded` と `Resolve-EvidenceDeleteGuarded` の `origin: stage` の分岐が実装対象に入ったことだけが増えた。

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
