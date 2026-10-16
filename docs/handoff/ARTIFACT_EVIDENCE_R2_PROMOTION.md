# Artifact Storage / Evidence の R2 昇格 HANDOFF

## 0. メタデータ

- type: `slice`
- status: `A`（A3 統合済み。owner が 2026-10-17 に §5.1 末尾の 5 点を主担当案で裁定し、主担当が §2〜§5 を revision 2 に統合した。凍結は owner の本文確認後）
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
- 引継ぎ票: 次は **owner による revision 2 の本文確認と凍結**。入力は本 HANDOFF の §2〜§5（revision 2）と §5.1。owner が確認したら、主担当が凍結 commit と内容 sha256 をここに書き、引継ぎ票を Phase B 向けに書き換える。push はしない（公開点は `osm-workflow` の「公開点とGit履歴」）。引き渡さないもの: なし。
- （旧）引継ぎ票（A2 → A3。記録として残す）: 次は A3（統合と凍結）。入力は §5.1 と A1 初稿（commit `d9b5c3e5`）。owner は 2026-10-17 に 5 点を主担当案で裁定した。
- （旧）引継ぎ票（A1 → A2。記録として残す）: 次は A2（独立レビュー）。入力は A1 初稿の commit `d9b5c3e5`（内容 sha256 `e5a7060cb698c9a1d64e3a9ae4527191bff3cd2570c329d6337600d349929760`）。観点は §5 の A2 欄。

## 1. 目的と対象外

- 目的: `evidence stage` でローカル固定した Evidence（state `staged`）を、**同じ artifact・同じ ZIP bytes のまま** R2 へ置けるようにする（昇格）。reference と packageSha256 を変えず、取得案内は `locality` だけを `local` から `promoted` に更新すれば済む。PUT 応答不明・回復 GET の 404・再送・成功確定・清掃・task 終了の各場面で、データを失わず、確認できない削除を削除済みにせず、R2 に孤児を残す経路を作らない回復契約を閉じる。
- 現況（2026-10-17、`origin/develop` = `ec5288d7`）:
  - `evidence stage`（PR #128）は publish と同じ準備（selection → guard → network 無し preflight → snapshot → `PackageIO.CreateVerified` → `Extract` 再検証）までを行い、artifact を state `staged`・`origin: stage`・`putIntent=null`・`receipt=null` で catalog に入れる。ZIP は staging operation 内の `ownedCopies` に唯一の複製として残る。`key` は `Get-EvidenceKey` が artifactId から導く値を未送信のまま持つので、reference（`osm-evidence-v2:`）は R2 に置いても変わらない（[EvidenceApplication.psm1](../../tools/Artifacts/EvidenceApplication.psm1) `Invoke-EvidenceStage`）。
  - publish の送信部（同ファイル `Invoke-EvidencePublish`）は、`intents/<artifactId>.json` を `Write-EvidenceAtomic -Immutable`（上書き不可）で書き → catalog に `putIntent` を記録 → package に read lock（`FileShare.Read`）を取って PUT → 別 process の readback GET で bytes・hash を照合 → `receipts/<artifactId>.json` を immutable で書き → state `ready`。PUT 応答不明は同 key へ再送せず `put-pending` のまま残す。transport（[R2ArtifactTransport.cs](../../tools/Artifacts/Transport/R2ArtifactTransport.cs)）は条件付き PUT（`If-None-Match`）を使っていない。
  - `put-pending` の回復（[EvidenceCleanup.psm1](../../tools/Artifacts/EvidenceCleanup.psm1) `Resolve-EvidencePutGuarded`）は、GET が一致すれば receipt を書いて `ready`、GET が 404 なら state を `failed` にする。`failed` は保持 policy で `unconfirmed-artifact` になり、清掃・DELETE されない。つまり既存 publish の「404 の後に先行 PUT が遅れて確定する」場合の remote object は、今も削除されない residue として残る（既存の受容。本スライスは publish 側を変えない）。
  - 読戻し検査（[EvidenceStateStore.psm1](../../tools/Artifacts/EvidenceStateStore.psm1) `Read-EvidenceTask`）は state を 6 値（`put-pending` / `ready` / `delete-pending` / `deleted` / `failed` / `staged`）、`origin` を `publish` / `stage` に限る。origin と state の組み合わせ自体は制限していない。receipt は、`ready` と `delete-pending`（origin を問わない）、および origin が `stage` 以外の `deleted` に要求する。
  - 保持 policy（[EvidenceRetentionPolicy.psm1](../../tools/Artifacts/EvidenceRetentionPolicy.psm1)）は、`staged` を次のように判定する。`origin: stage` で、`putIntent` が null かつ `receipt` が null のものは `ready` と同じ経路。それ以外の `staged`（putIntent を持つ、receipt を持つ、publish 由来、origin 無し）は全て `unknown-state`（削除しない）。清掃は `staged` をローカル削除（`Invoke-EvidenceLocalDeleteGuarded`）、`ready` を remote 削除（deleteIntent → DELETE → GET 404 `NoSuchKey` → `deleted`）に分ける。remote 削除の照合 GET が一致した場合は state を `ready` に戻す（`Resolve-EvidenceDeleteGuarded`）。
  - fetch は guard を取った後、preflight の前に catalog を読み、対象が `staged` なら network 無しの preflight と local 分岐、そうでなければ network 付き preflight と remote 分岐を選ぶ。選択は preflight 後に変わらない。
  - `use` / `release` は `ready` と `staged` を受け付ける。
  - 前スライスで後続へ送った入力（program §4 C）: 旧 HANDOFF revision 2 の D4（promote）/ D4'（回復）/ I8 / S4、追加レビュー ADD-01（R2 の GET 404 は先行 PUT の失敗確定ではなく、同 key 再送禁止の既存 publish 契約と衝突する）、前回所見 R2-02（昇格時の catalog 更新単位）、fetch の local / remote 選択が preflight 前に決まる点。全文は削除前の HANDOFF（`docs/handoff/ARTIFACT_EVIDENCE_LOCAL_STAGE.md`、commit `72a8b0c`、revision 2 は `16509b46^`）。
- 対象外: Cloud・別 PC が R2 から fetch する経路と token の受け渡し（2026-10-17 owner 裁定で本スライスから外した。program §4 C の後続）。既存 publish の `put-pending` 回復と `failed` の扱い。`ready` artifact のローカル copy からの network 無し fetch。条件付き PUT など transport の変更。`use` の network 無し preflight（program §4 C の入力のまま）。Unity 側の変更なし。

## 2. 意思決定と受け入れ境界（revision 2。A2 の採用分と A3 の 5 点の裁定を統合。owner の本文確認待ち）

- このスライスが答える問い: 「`staged` の artifact を、同じ reference・同じ packageSha256・同じ bytes のまま R2 へ昇格でき、PUT 応答不明から先のどの停止点でも、payload を失わず、確認できない状態を成功・削除済みにせず、R2 上の object を catalog の追跡外に残さずに、昇格・取得・清掃へ収束できるか」。
- A0 で owner が固定した前提: 範囲は昇格だけ。Cloud・別 PC からの取得経路は後続（2026-10-17）。
- A3 で owner が裁定したこと（2026-10-17、§5.1 末尾の 5 点を全て主担当案で採用）: (1) 回復 GET の 404 の後は `staged` に戻し、同じ bytes の再送を許す。putIntent を持つ artifact は remote 削除だけ。(2) 削除の後に遅れて確定する PUT は受容する残存リスクとして README に書く。(3) `already-promoted` は `status: passed`、初回の成功と同じ出力の形で、終了済み task でも返す。(4) `EvidenceSend.psm1` は送信プリミティブだけ、promote は `EvidenceApplication.psm1`、削除経路の選択は `EvidenceCleanup.psm1`。(5) 判定 C の実行担当が AC12 の直前に GO 候補 head で `schedule install` を行うことを承認する（D11）。
- 方式:
  - **D1 `evidence promote`。** `pwsh tools/artifacts.ps1 evidence promote --profile osm --config <cfg> --reference <ref> --sha256 <期待 packageSha256>`。順序は次のとおり。
    1. config と reference を読み、reference の taskId で task guard を取る。
    2. 通常の（network を使う）preflight と Sync を行う。ここで同じ task の `put-pending` と、D8 で読める昇格由来の `failed` は D4 の回復で解決される。解決できなければ既存どおり `unresolved-put` / `unresolved-delete`。
    3. catalog に reference の artifactId が無ければ `catalog-missing`、`deleted` なら `expired`。
    4. **全分岐の前に** identity を照合する: key・base・head・manifestSha256・packageBytes が reference と、packageSha256 が期待 hash と一致すること。不一致は `identity-mismatch`、PUT 0 件。
    5. `ready` かつ `origin: stage`: 送信せず `status: passed`、`reasonCode: already-promoted`。出力は初回の成功と同じ形（`reference`・`packageSha256`・`locality: promoted`・`receipt`・`residue.remote: ready`）。PUT も state 変更も無いので、task の状態（`active` / `ended`）を問わない。応答を失った昇格の再実行もここで収束する。
    6. `staged` かつ `origin: stage`: task が `active` でなければ `resume-required`（publish・stage と同じ）。`active` なら D2 へ進む。
    7. それ以外（`origin: publish` の全ての state、`origin` の無い artifact、手順 2 の後も残る `delete-pending` 等）: `invalid-source`、PUT 0 件。
  - **D2 送信（同じ bytes）。** catalog の書込みは次の 3 回で、間のどこで止まっても安全な状態になる。
    1. catalog の `ownedCopies` から packageSha256・packageBytes が一致する ZIP を選び、read lock（`FileShare.Read`）で開く。lock を保持したまま、その file の SHA-256 と長さを catalog の値と照合する。不一致なら PUT せず `identity-mismatch`。再 snapshot・再 package はしない（snapshot・package を作る関数を呼ばない）。
    2. putIntent: artifact が既に `putIntent` を持てば、その file の sha256 と、内容の key・packageSha256・artifactId を照合して再利用する。持たなければ `intents/<artifactId>.json` を見て、既に在る（前回の昇格が catalog 記録の前に止まった）なら同じ照合で再利用し、無ければ publish と同じ形で immutable に書く。照合に失敗したら `unresolved-put`、PUT 0 件。
    3. **書込み 1:** readback 用の新しい staging operation を登録する（回復 GET と同じ `New-EvidenceRecoveryStage` の形）。artifact は `staged` のまま。ここで止まれば、artifact は無傷で、未採用の staging が 7 日清掃に乗る。
    4. **書込み 2:** 1 回の catalog 書込みで、`putIntent` の参照・state `put-pending`・artifact の `operation` を記録する。
    5. lock を保持したまま同じ file を PUT し、別 process の readback（`Invoke-ArtifactReadback`）で bytes・hash を照合する。同じ process の GET の一致では `ready` にしない。
    6. receipt: `receipts/<artifactId>.json` が既に在れば、回復と同じ照合（key・packageSha256・manifestSha256）で再利用する。無ければ publish と同じ形（`putIntentSha256` を含む）で immutable に書く。
    7. **書込み 3:** 1 回の catalog 書込みで、`receipt`・state `ready`・`operation=null` を記録する。`ownedCopies` は**追記のみ**で、昇格前の集合を全て残し、readback staging の file を加える。adopted にする staging は、中の file が全て `ownedCopies` に入ったものに限る。`origin: stage` は残す。
    - 出力は `reference`（入力と同じ文字列）、`packageSha256`、`locality: promoted`、`receipt`、`residue.remote`。PUT または readback が確認できなければ `put-pending` のまま `status: failed`・`reasonCode: unconfirmed` を返す。
    - 停止点の注入: Send の FaultHook `after-put-intent-file`（intent file の後・書込み 2 の前）、`after-put-pending`（書込み 2 の後・PUT の前）、`after-receipt-file`（receipt file の後・書込み 3 の前）。
  - **D3 再送の許可（ADD-01 への答え）。** 昇格では、同じ key へ同じ bytes（catalog の packageSha256 と一致を確認した ZIP）を再度 PUT してよい。既存 publish の「PUT 応答不明を同 key 再送しない」は、publish が毎回別の snapshot を作る経路の契約で、昇格には適用しない（README に publish 側の契約と明記し、既存テスト `one-put-lost-response` も publish にだけ適用する）。根拠: 送る bytes は packageSha256 で固定され、R2 の PUT は object 全体の置換なので、先行 PUT が後から確定しても後続 PUT と同じ内容になる。保証するのは「その key の object は、存在するなら packageSha256 の bytes である」ことと「`ready` は別 process の readback 一致の後にしか書かない」ことで、「PUT が最大 1 回」は保証しない。promote は `put-pending` のまま PUT しない（必ず D1 手順 2 の回復で `staged` か `ready` に確定させてから送る）。
  - **D4 昇格由来の未確定の回復。** `Resolve-EvidencePutGuarded` は、`origin: stage` で (i) `put-pending`、または (ii) putIntent あり・receipt 無しの `failed`（本スライスより前の runtime が回復 GET の 404 で付けた state。D8）について:
    - GET が一致: 既存どおり receipt を書いて（在れば D2-6 と同じ照合で再利用して）`ready`。`ownedCopies` は追記のみ、adopted にする staging は中の file が全て `ownedCopies` に入ったものに限る（同じ artifactId の staging を一律に adopted にしない）。
    - GET が 404: state を `staged` に戻し、`putIntent`・ZIP・`ownedCopies`・`protections` をそのまま残す。同じ artifact の停止済み staging の `operation` を解除する（そのままだと清掃が `staged` の staging を飛ばすので解除されず、ディレクトリを失うと task が止まる）。
    - GET がそれ以外（別 bytes、403、timeout 等）: 既存どおり `unresolved-put` で止まる。
    - `origin: publish` の挙動は変えない（404 → `failed`）。
    - 戻した `staged` は local fetch でき（D6）、promote の再実行で同じ putIntent を再利用して再送できる（D3）。404 は「今は無い」の観測であり、遅れて確定する先行 PUT はありうる。それは D3 により同じ bytes で、D5 により必ず remote 削除の経路に乗るので、catalog の追跡外には残らない。
    - network 無しの preflight（`-NoNetwork`）は、(i)(ii) のどちらも既存の `put-pending` と同じく `unresolved-put` で拒否する。
  - **D5 送信を試みた `staged` の清掃。**
    - 保持 policy（`Get-EvidenceRetentionDecision`）は、`origin: stage`・putIntent あり・receipt 無しの `staged` を、`unknown-state` にせず `ready` と同じ保持・保護規則で判定する。receipt を持つ `staged`、publish 由来の `staged`、origin の無い `staged` は既存どおり `unknown-state`。
    - 削除経路は `EvidenceCleanup.psm1` の副作用の無い関数 `Get-EvidenceDeletionRoute` で決める: `local` = state `staged`・`origin: stage`・putIntent 無し・receipt 無しの全て。`remote` = `ready`、または state `staged`・`origin: stage`・putIntent あり・receipt 無し。それ以外は `none`（削除しない）。`Invoke-EvidenceLocalDeleteGuarded` も、自分の中で経路が `local` であることを再確認する。
    - putIntent を持つ artifact の remote 削除は、putIntent の `process` が停止済み（`Test-EvidenceProcessStopped`）であることも条件にする。満たさなければ `operation-pending` で残す。
    - remote 削除は既存の deleteIntent → DELETE → GET 404 `NoSuchKey` → `deleted`。照合 GET が一致した場合、receipt を持つ artifact は既存どおり `ready` に、receipt を持たない artifact は `staged` に戻す（次回の清掃が新しい intent で再試行する）。
    - R2 側の保証の限界（受容する残存リスク。README に書き、保証とは書かない）: 全ての PUT は task が `active` の間にだけ発行され（D1）、remote 削除は task 終了 + 30 日以後にだけ起きるので、削除の後に遅れて確定する PUT は 30 日以上未完了の HTTP 要求を要する。有限回の追加確認ではこのリスクを消せないので、削除後の追加確認は置かない。
  - **D6 fetch の取得元を preflight 後に決める。** fetch は preflight 前の catalog の読取りで「preflight に network を使うか」だけを決める（対象が `staged` なら network 無し。前スライスの I1 を維持）。取得元（local / remote）は preflight と Sync の**後**の state で決める: `staged` → local 分岐、`ready` → remote 分岐、それ以外 → 既存の拒否。昇格中（`put-pending`）や昇格由来の `failed` の artifact は network 付き preflight で `ready` または `staged` に解決された後、正しい取得元で取得される（古い選択による `identity-mismatch` を返さない）。ローカル固有の reasonCode（`changed-copy`・`unresolved-put`・`unresolved-delete`）の出し分けも、最終的な取得元で行う。`ready` になった昇格済み artifact は従来どおり R2 から取得する（ローカル copy からの取得は対象外）。失敗時に他方の取得元へ fallback しない。network 無しの保証は「呼び出し開始時に `staged` の artifact」に限ると README に書く。
  - **D7 inspect。** `locality` は `staged` → `local`、`ready` → `promoted`、それ以外 → null（既存どおり）。putIntent を持つ `staged` も `local`（remote に在るかは未確認）。
  - **D8 catalog の読戻し検査。** `origin: stage` の artifact に許す組み合わせを固定する。`staged`: receipt 無し（putIntent は有無どちらも可）。`put-pending`: putIntent 必須・receipt 無し。`ready`: receipt 必須。`delete-pending`: receipt 必須、または receipt 無しなら putIntent 必須。`deleted`: receipt 不要（既存）。`failed`: putIntent 必須・receipt 無しの場合だけ読む（D4 (ii)。本スライスのコードはこの state を作らない。保持 policy は既存どおり `unconfirmed-artifact` で削除しない）。それ以外の組み合わせは `corrupt-state`。`origin: publish` の検査は変えない。新しい state・新しい field は足さない。
  - **D9 ワークフロー文書。** `review-evidence.md` の「保存先とリモートへの配布」で、「昇格経路が後続スライスで成立するまで従来どおり publish」を、「Cloud・別 host の読み手、または端末喪失に備えて owner が指示した時は、同じ artifact を `evidence promote` で昇格する。reference と packageSha256 は変わらないので、取得案内は `locality` だけを更新する。Cloud・別 host からの取得経路は後続（program §4 C）で、成立するまでその読み手の作業は assist」に置き換える。`SKILL.md`・`phases-and-handoff.md`・`handoff-template.md` は変えない（状態「ローカル固定済み（未昇格）」と Cloud の assist 条件はそのまま正しい）。
  - **D10 README と事前承認。** `tools/Artifacts/README.md` の冒頭と「ローカル固定」の「昇格は後続」を D1〜D8 の操作・回復規則・残存リスクに置き換える。「Evidence v2 の通常操作」の「PUT応答不明を同key再送せず」は publish の契約であり、昇格は D3 に従うと書く。「エージェント操作の事前承認」に `evidence promote` を既存 publish と同じ承認範囲の通常操作として加える（同じ endpoint・bucket・prefix・既存資格情報。新しい資格情報・公開範囲は無い）。導入手順（D11）を書く。
  - **D11 runtime の切替。** 共有 store で promote を使い始める前に、登録済み runtime を本スライスの head へ切り替える（`schedule status` で head を確認し、`schedule install`）。旧 runtime の日次 job は、昇格由来の `put-pending` を 404 で `failed` にし（新しい reader は D8 で読み、D4 で回復する）、receipt 無しの `delete-pending` を `receipt-missing` で読めない。判定 C の AC12 はマージ前に共有 store で行うので、**判定 C の実行担当が AC12 の直前に GO 候補 head で `schedule install` を行う**（A3 裁定 5 の owner 承認、2026-10-17）。マージ後に最終 head が GO 候補 head と違えば、通常の導入手順で入れ直す。
- 却下した代替案:
  - 新 state `promote-pending` で 404 の後も保持し、再送を禁じる（A2 (c)）: 404 の後は遅れた PUT が確定しない限り永久に `ready` に達せず、reference と hash を保ったまま昇格する目的を満たせない。新 state は D8・保持・清掃・fetch・use の全分岐の網羅を要する（A3 裁定 1）。
  - 条件付き PUT（`If-None-Match: *`）で再送を禁止する: transport の変更と publish（I6）への波及が要り、R2 の S3 互換 API での挙動確認が未知の操作経路になる。D3 の「同じ bytes の再送は無害」で足りる。
  - 404 の後も `put-pending` のまま残す（既存 publish と同じ扱い）: local fetch・stage・cleanup が同じ task で `unresolved-put` により止まり続け、昇格に失敗した artifact の唯一の複製を使えなくなる。
  - 404 の後に `failed` にする: `failed` は終点で、どの経路からも回復できず、遅れて確定した object も削除されない（元の staging は adopted なので 7 日清掃では消えない。A2 (b) B-11 で初稿の理由を訂正）。
  - 404 の後に不在確認の時刻を記録してローカル削除を許す（前スライスの revision 2 の D4'・D5）: ADD-01 のとおり遅れて確定した object が追跡外に残る。
  - `put-pending` の artifact の fetch を network 無しで local ZIP から行う（A2 (c)）: 前スライスの「未解決の put があれば network 無しの preflight は拒否」を変える（I6）。D6 で取得は成立する。
  - 昇格のたびに再 snapshot して新しい artifact を publish する: reference と hash が変わり、取得案内の書き直しが要る（それは既存 publish で今も可能）。
- 進める最低条件:
  - 不変条件（起きてはいけないこと）:
    - **I1** promote が、`staged`（`origin: stage`）以外の artifact、または identity・期待 hash が一致しない artifact に PUT を発行する。`ready` の再 promote が PUT を発行する。`put-pending` のままの artifact に PUT を発行する。
    - **I2** promote が、catalog の packageSha256・packageBytes と一致しない bytes を PUT する。照合から PUT 完了までの間に ZIP が書き換えられる。再 snapshot・再 package が起きる。
    - **I3** 昇格または回復の前後で、reference 文字列・artifactId・key・packageSha256・manifestSha256・base・head・packageBytes・entries・taskEndedAt・deleteEligibleAt・protections のいずれかが変わる。
    - **I4** 別 process の readback で bytes・hash の一致を確認する前に、promote が state `ready` または receipt を書く。
    - **I5** putIntent を持つ artifact（PUT を試みた可能性があるもの）が、remote DELETE と GET 404 の確認を経ずに `deleted` になる。本スライスのコードが昇格由来の artifact を `failed` にする。昇格由来の `failed` が回復できない終点になる。`deleted` になる前に、昇格前の `ownedCopies`・putIntent・receipt の参照が失われる。追跡外の file を持つ staging が adopted になる。
    - **I6** 既存の publish・remote fetch・`origin: publish` の `put-pending` 回復・`ready` の清掃と保持判定・reasonCode の意味が変わる。呼び出し開始時に `staged` の artifact への stage・fetch が network・資格情報を使う。publish 由来・origin 欠落の `staged` がローカル削除される。
    - **I7** fetch が、自分の preflight の中で state が変わったことを理由に、取得できる artifact を拒否する、または最終的な取得元と違う reasonCode を返す。local と remote の間で失敗時に fallback する。
    - **I8** 保護中（有効な `use`）または task 終了 + 30 日前の artifact が、昇格・回復・清掃のどれかで削除される。
  - 成立条件（起きなければならないこと）:
    - **S1** `staged` → promote → `passed`。返る reference と packageSha256 が stage の出力と同じで、inspect の `locality` が `promoted`。別 process の remote fetch が同じ reference と期待 hash で成功し、全 entry の hash が元 file と一致する。
    - **S2** PUT 応答不明 → `put-pending` → 回復の 2 終点: GET 一致なら `ready`。GET 404 なら `staged`（putIntent 保持）に戻り、local fetch が成立し、promote の再実行が同じ putIntent を再利用して `ready` に達する。旧 runtime が付けた昇格由来の `failed` も同じ 2 終点に回復する。
    - **S3** 清掃の 2 終点: 送信を試みていない `staged` は満了でローカル削除（既存）。putIntent を持つ `staged` は満了で remote 削除の経路を通って `deleted` になり、その後も `Read-EvidenceTask` が通る。
    - **S4** `ready` の再 promote と、応答を失った昇格の再実行が、task の状態を問わず `already-promoted`（PUT 0 件）で収束する。
    - **S5** 実 R2 で、隔離した task の `staged` を 1 回昇格し、別 process の remote fetch で全 entry が一致する（高コスト証拠。§5）。
- 受け入れ条件（検証例。offline は隔離した temp store・偽 transport・時刻注入。実ストア・network 不使用。実 R2 は AC12 だけ）:
  - **AC1**（S1, I3, I4, I5）: fixture の `staged` を promote。偽 transport が記録した PUT bytes の SHA-256 が packageSha256 と一致する。`ready` と receipt は別 process の readback の後にだけ書かれる（合格例: readback child が一致を返した後に `ready`。不合格例: 同じ process の GET の一致だけで `ready`。偽の readback を失敗させると `put-pending` のまま receipt 無し）。昇格前後で I3 の全 field と reference 文字列が一致する。昇格後の `ownedCopies` が昇格前の集合を全て含む。inspect の `locality` が `local` → `promoted`。別 process の fetch（remote 分岐）が成功する。
  - **AC2**（I1）: 次の組で promote し、全て PUT 0 件。(a) `origin: publish` の `ready` → `invalid-source`。(b) `origin: publish` の `put-pending` で偽 GET が 404 → 回復で `failed` → `invalid-source`。同じく GET 一致 → `ready` → `invalid-source`。GET が別 bytes → `unresolved-put`。(c) `origin: publish` の `failed` → `invalid-source`。(d) `delete-pending` で偽 GET 404 → `deleted` → `expired`。GET が別 bytes → `unresolved-delete`。(e) `deleted` → `expired`。(f) catalog に無い artifactId → `catalog-missing`。(g) 期待 hash 不一致、reference の base・head・key・manifestSha256・packageBytes の改変 → `identity-mismatch`。`origin: stage` の `put-pending` は AC5・AC7 で扱う。
  - **AC3**（S4, I1）: 昇格済み（`origin: stage` の `ready`）への promote が `already-promoted`、PUT 0 件、出力の形（reference・packageSha256・`locality: promoted`・receipt・`residue.remote: ready`）が初回の成功と同じ。task が `active` の場合も `ended` の場合も同じ結果（不合格例: `ended` で `resume-required`）。期待 hash が違えば `identity-mismatch`。`ended` の task の `staged` への promote は `resume-required`、PUT 0 件。
  - **AC4**（I2）: ZIP を 1 byte 改変した `staged` の promote は PUT 0 件で `identity-mismatch`。照合後・PUT 前に別 process が ZIP を書込みで開こうとすると共有違反で失敗する（lock の保持）。snapshot・package を作る関数（`Initialize-EvidencePackage`、`Copy-EvidenceSnapshot`、`PackageIO.CreateVerified`）が promote の経路から呼ばれない（呼び出し記録 0 件）。
  - **AC5**（S2, I5）: promote の PUT 応答不明を注入 → `put-pending`、putIntent 記録済み。回復で GET 一致 → `ready`（receipt あり、`ownedCopies` は昇格前を含む）。別 fixture で GET 404 → `staged`、putIntent・ZIP・ownedCopies・protections が残り `failed` にならず、前回の readback staging の `operation` が解除される。続けて local fetch が成功し（transport 呼び出し 0 件）、promote の再実行が既存の intent file を作り直さずに（intent file の sha256 不変）PUT して `ready` になる。PUT は通算 2 回（応答を失った 1 回と再実行の 1 回。不合格例: intent が作り直されて sha256 が変わる）。`origin: publish` の `put-pending` は既存どおり 404 で `failed`。
  - **AC6**（S2, I5）: FaultHook で停止を注入する。`after-put-intent-file` で停止 → artifact は `staged`・putIntent 無しのまま、local fetch 成立、再 promote は既存の intent file を照合して再利用する。intent file の内容が key・packageSha256・artifactId のどれかで食い違えば `unresolved-put` で PUT 0 件。`after-put-pending` で停止 → 次の preflight の回復で GET 404 → `staged`。`after-receipt-file` で停止 → 次の preflight の回復で GET 一致 → 既存の receipt を再利用して `ready`。同じ停止の後に object が外部で消えて GET 404 → `staged` → 再 promote が既存の receipt を照合して再利用し、immutable な書込みで失敗しない。
  - **AC7**（I7）: `put-pending`（`origin: stage`）の artifact を fetch。network 付き preflight で GET 一致 → `ready` に解決されて remote 分岐で成功。GET 404 → `staged` に解決されて local 分岐で成功（`identity-mismatch` を返さない）。後者で ZIP を欠落させると `changed-copy`（不合格例: `unconfirmed`）。remote の GET 失敗は既存の reasonCode。どちらも他方へ fallback しない。
  - **AC8**（S3, I5, I8）: 時刻注入。putIntent を持つ `staged` は `deleteEligibleAt` の 1 tick 前に `retained`、`deleteEligibleAt` ちょうどで remote 削除（deleteIntent → DELETE → GET 404）され `deleted`、ZIP と ownedCopies が消え、その後も `Read-EvidenceTask` が通る（不合格例: 同じ日付の 00:00 で削除）。DELETE を失敗させると `delete-pending`（receipt 無し）で残り、ZIP は残る。続く照合 GET が一致すれば `staged` に戻り、ZIP と putIntent が残る。putIntent の process が停止していなければ `operation-pending` で削除しない。putIntent も receipt も無い `staged` は既存どおりローカル削除（DELETE・GET 0 件）。`origin: publish` の `staged` と origin 欠落の `staged` は `unknown-state` で削除されず、`Get-EvidenceDeletionRoute` は `none`。
  - **AC9**（I8）: `staged` に `use`（until = deleteEligibleAt + 1 時間）→ promote → 満了直後も `in-use`。回復で `staged` に戻った後も保護が残る。`release` 後に満了判定。不合格例: until を満了前に置いてしまい、保護なしで通る。
  - **AC10**（I6）: 既存の `ArtifactEvidence` / `EvidenceCleanup` / `EvidenceRetention` / `EvidenceSchedule` / `EvidenceReset` suite のケースが failed 0。意図して変える既存の期待は `EvidenceRetention.Tests.ps1` のケース `staged-unsent-required` の 1 行目（putIntent を持つ `staged` は `unknown-state`）だけで、`ready` と同じ判定に改める。同じケースの残り 3 行（receipt を持つ `staged`、publish 由来の `staged`、origin 無しの `staged` は `unknown-state`）と他の全ケースは無変更で通る。`origin` 無しの既存 catalog、`origin: stage` の既存 shape（putIntent 無しの `staged` / `deleted`）を読める。呼び出し開始時に `staged` の artifact への stage・fetch で transport と資格情報の読取りが 0 件（前スライスのケースを無変更で再実行）。publish の送信部を共有化しても publish の既存ケースが無変更で通る。
  - **AC11**（D8, I5）: `origin: stage` で、`failed` かつ putIntent 無し、`failed` かつ receipt あり、`put-pending` かつ putIntent 無し、`staged` かつ receipt あり、`delete-pending` かつ receipt も putIntent も無い catalog は `corrupt-state` で読まれず、その task の削除が起きず、その task への promote も PUT 0 件で失敗する。`origin: stage`・putIntent あり・receipt 無しの `failed` は読め、保持判定は `unconfirmed-artifact`。
  - **AC12**（S5、高コスト証拠）: §5 の手順で実 R2 に 1 回。
  - **AC13**（D9, D10）: README・`review-evidence.md` に promote の操作、D3〜D6 の回復規則、publish 側の再送禁止との区別、残存リスク、導入手順、事前承認が書かれ、「昇格は後続」「昇格経路が後続スライスで成立するまで」の旧文言が `tools/Artifacts/README.md` と `.agents/skills/osm-workflow/` に残らない。`docs-audit`・`contract-audit` が errors=0。
  - **AC14**（S2, I5）: 旧 runtime の回復を模した catalog（`origin: stage`・putIntent あり・receipt 無しの `failed`）で、network 付き preflight の回復が、偽 GET 一致なら `ready`、404 なら `staged` にする。network 無しの preflight はこの artifact を `unresolved-put` で拒否する。
  - **AC15**（I5）: 回復 GET の一致で、同じ artifactId を持つ staging のうち、中の file が `ownedCopies` に入っていないもの（停止した前回の promote の readback staging）は adopted にならず、7 日後に 7 日清掃で消える。
- ここでは答えない問いと所有する後続スライス:
  - Cloud・別 PC が R2 から fetch する経路、読取り専用 token の登録・受け渡し → program §4 C（`Artifact Storage / Evidence の Cloud 配布`、新規）。
  - `ready` artifact のローカル copy からの network 無し fetch（昇格後も同じ PC の読み手が network 無しで取得する） → program §4 A の後続への入力。
  - `use` の network 無し preflight → program §4 C の入力のまま。
  - 既存 publish の 404 → `failed` で残る remote residue の回収 → Content の容量上限・孤児回収の入力。
  - publish の `ownedCopies` 置換と、回復 GET 一致時に同じ artifactId の staging を一律に adopted にする既存挙動（publish 由来） → 診断の改善の入力（本スライスは昇格由来の経路だけを直す）。
  - 端末喪失に備えた未昇格 `staged` の定期 backup → Harness H3。
- 判定定義: GO = I1〜I8 を破る欠陥がなく、S1〜S4 が offline fixture と文書検査で成立し、S5 の実 R2 昇格 1 回が高コスト証拠として成立し、判定 C と最終 C' がともに阻害 0。NO-GO = いずれかの不変条件の違反、または S の不成立。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし。
- 決定記録: なし。
- 本文へ転記した実装制約: `#nullable` と `record` の規則は Unity 側のみで本スライスには当たらない。PowerShell は `Set-StrictMode -Version Latest`。reasonCode は既存の小文字 kebab（新規は `already-promoted` の 1 つ）。path・key・資格情報を CLI 出力とログに出さない。テストで `Start-Sleep` / `Task.Delay` / `Thread.Sleep` を使わず時刻と transport を注入する。catalog に新しい state・新しい field を足さない（D8 は既存 field の組み合わせの検査だけ）。資格情報の root・暗号文・DPAPI の利用を変えない。実ストアの catalog・transfers・資格情報を offline テストで読み書きしない。
- 未決事項: なし（A1 の 4 点は A3 で裁定済み）。

## 3. 責務マップ

| ファイル | 責務 | 変更理由 | 現在行数 → 予想 | 備考 |
|---|---|---|---|---|
| `tools/Artifacts/EvidenceSend.psm1`（新規） | 送信プリミティブ: putIntent の照合・作成、read lock 下の hash 照合と PUT、別 process readback、receipt の照合・作成 | 「同じ bytes を 1 つの key に置き、確認後にだけ receipt を作る」手順は publish と promote で同じ理由で変わる | 0 → 約 90 | catalog・state 遷移・回復を持たない。依存は `ArtifactApplication`・`EvidencePaths` より下だけ（intent に書く process identity は呼び出し側が渡す）。FaultHook の注入点（D2）を持つ |
| `tools/Artifacts/EvidenceApplication.psm1` | publish / stage / promote / fetch / inspect / use の orchestration | `Invoke-EvidencePromote`（D1・D2 の catalog 書込み）、publish の送信を Send の呼び出しに置換、fetch の D6、inspect の D7 | 203 → 約 240（+18%） | 依存は Application → Send / Cleanup / StateStore / RetentionPolicy の一方向。送信の手順は持たない |
| `tools/Artifacts/EvidenceCleanup.psm1` | preflight、put / delete の回復、清掃 | D4（`origin: stage` の回復、adopted の限定、operation 解除）、D5（`Get-EvidenceDeletionRoute`、process 停止の条件、照合一致 → `staged`、ローカル削除内の経路再確認）、`-NoNetwork` の D4 (ii) | 369 → 約 405 | 500 行未満。Send を import しない。経路の選択は副作用の無い関数として単体テストする |
| `tools/Artifacts/EvidenceRetentionPolicy.psm1` | 純粋な保持 policy（期限・保護の適格性） | putIntent を持つ `origin: stage` の `staged` の判定 | 68 → 約 72 | 削除経路は持たない |
| `tools/Artifacts/EvidenceStateStore.psm1` | catalog の読戻し検査 | D8 の origin × state × putIntent × receipt の組み合わせ | 144 → 約 156 | 新 state・新 field なし |
| `tools/artifacts.ps1` | CLI の引数解釈 | `evidence promote` の grammar と dispatch | 192 → 約 196 | |
| `tools/Artifacts/README.md` | 公開仕様 | D10・D11 | 447 → 約 475 | |
| `.agents/skills/osm-workflow/references/review-evidence.md` | レビュー証拠の保存・配布 | D9 | 172 → 約 172 | 置換のみ |
| `tools/Artifacts/tests/ArtifactEvidence.Tests.ps1`、`EvidenceCleanup.Tests.ps1`、`EvidenceRetention.Tests.ps1`、`EvidenceTestSupport.ps1` | offline 検証 | AC1〜AC11・AC14・AC15。偽 transport の PUT bytes 記録、応答不明・404・別 bytes の注入、Send の FaultHook | 追加ケース | 既存の偽 transport・時刻注入・FaultHook を使う。`EvidenceCleanup.Tests.ps1`（505 行）はケースの列挙で変更理由が同じなので非分割 |

状態の所有者は task の catalog（task guard が直列化）。寿命は既存の task 終了 + 30 日。新しい公開面は CLI の `evidence promote` と reasonCode `already-promoted`。新しいファイルは `EvidenceSend.psm1` の 1 つ。asmdef・Unity 側の変更なし。`EvidenceSchedule` の runtime 収集は変えない（tracked な psm1 は既存の `git ls-files` 収集で入る）。

## 4. 実装計画

- 順序: (1) `EvidenceStateStore` の D8（既存 shape の読戻し不変を AC10 で固定）→ (2) `EvidenceRetentionPolicy` の判定 → (3) `EvidenceSend.psm1` へ publish の送信手順を移し、publish をその呼び出しに置き換える（publish の `ownedCopies` 置換を含む既存挙動は変えない。AC10 で固定）→ (4) `EvidenceCleanup` の D4・D5 と `Get-EvidenceDeletionRoute` → (5) `Invoke-EvidencePromote`（D1・D2）→ (6) fetch の D6 と inspect の D7 → (7) CLI → (8) README（D10・D11）と `review-evidence.md`（D9）→ (9) `contract-audit`・`docs-audit`・AC13 の grep。
- 導入手順（D11。README に書く。B は書くだけで実行しない）: 共有 store で promote を使い始める前に、`schedule status` で登録済み runtime の head を確認し、本スライスの head で `schedule install` を行う。
- Phase B から Phase A へ差し戻す条件: 新しい state・field、transport の変更、`PackageIO` の manifest 形式の変更、Workflow の終了意味の変更、`EvidenceSchedule` の変更が必要になったとき。publish の送信手順を共有化すると、AC10 で列挙した 1 行以外の既存ケースを変えなければ通らないとき。D8 の組み合わせで既存の実 catalog の shape を読めなくなると判明したとき。Send に catalog・state 遷移を持たせないと D2 を実装できないとき。
- 対象外を維持する方法: promote の経路に snapshot・package を作る関数の呼び出しを置かない（AC4）。`staged` で始まる stage・fetch の経路に `Invoke-ArtifactNetwork` と `Get-CredentialStatus` を置かない（AC10）。Cloud・別 PC 向けの取得経路と token を作らない。`origin: publish` の分岐を変えない（AC2・AC10）。

## 5. テストとレビュー計画

- 単体テスト: `Get-EvidenceRetentionDecision` の判定（`EvidenceRetention.Tests.ps1`）、`Get-EvidenceDeletionRoute` の全組み合わせ（state × origin × putIntent × receipt）と `Read-EvidenceTask` の D8（`ArtifactEvidence.Tests.ps1` / `EvidenceCleanup.Tests.ps1`）、Send の intent・receipt の照合（偽 transport を使わない純粋部を直接）。送信の手順は偽 transport と Send の FaultHook で検証する。
- 差し戻し中の起点 `-Filter`: `tools/Artifacts/tests/ArtifactEvidence.Tests.ps1`、`EvidenceRetention.Tests.ps1`、`EvidenceCleanup.Tests.ps1 -Case <新規ケース>`。C は凍結済み条件の確認に必要なら根拠を記録して変えてよい。
- 判定必須テスト（GO 候補 head）: `tools/Artifacts/tests/*.Tests.ps1` と `tools/Workflow/tests/*.Tests.ps1` の全体（`StoreContext.Tests.ps1` は非仮想化 host で）、3 project の Release build、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC13 の旧文言 grep。`EvidenceCleanup.Tests.ps1` は `rg` が PATH に要る。高コスト証拠 AC12 は最終証拠ラウンドで加える。
- 全 EditMode 回帰の適用除外: Unity 側のコードを変更しないため。代替証拠は上記 offline suite 全体と audit。
- 統合・Unity テスト: なし。
- 操作・実行時・目視条件の検証経路: AC12 だけ（高コスト証拠）。高コスト証拠は動作確認の対象外なので、動作確認は `対象なし`。
- 未知の操作経路の疎通結果: なし。promote の PUT・readback GET・remote fetch は既存 publish・fetch と同じ transport・同じ key 形式・同じ資格情報を使う既知の経路。`schedule install` も既存の導入手順。
- 人間の判断が必要な条件: なし。
- 高コスト証拠（AC12、実 R2 昇格 1 回）:
  - 時点: 判定 C → 暫定 C' の阻害 0 の後、同じ head で 1 回（`review-evidence.md` の「高コスト証拠と最終証拠ラウンド」）。
  - 担当: 判定 C の実行担当（判断担当と分ける場合は新しい実行担当 session）。環境: 非仮想化 host の sandbox 外 PowerShell 7（Claude の Bash は LocalAppData が仮想化されるので使わない）。GO 候補 head で 3 project を Release build。
  - 手順: (0) `evidence schedule status` で登録済み runtime の head を記録し、GO 候補 head で `evidence schedule install` を行い、再度 `schedule status` を記録する（D11、owner 承認済み）。(1) `pwsh tools/workflow-task.ps1 start -Task evidence-r2-promotion-rt-1`。(2) 非秘密の固定 fixture（3 file、うち 1 つはバイナリ）を selection にして `evidence stage`。出力の reference と packageSha256 を記録。(3) 別 process で `fetch`（local 分岐）。(4) `evidence promote --reference <stage の reference> --sha256 <stage の packageSha256>`。`passed` で終わらなければ同じコマンドを 1 回だけ再実行し、それでも終わらなければ止めて生記録とともに判断担当へ返す。(5) `evidence inspect --task evidence-r2-promotion-rt-1`。(6) 別 process で `fetch`（remote 分岐）。(7) 同じ promote を再実行。(8) 原記録（各 CLI の結果 JSON、catalog・intent・receipt の写し、fixture の hash 一覧、手順 0 の status 2 回）を判定 evidence に収録し、task を通常の最終手順で end する（R2 object は task 終了 + 30 日の通常清掃が消す。事前承認の範囲）。
  - 実装から独立した期待値: (4) が `passed`、reference 文字列が (2) と完全一致、packageSha256 が (2) と一致。(5) の対象 artifact が `ready`・`locality: promoted`。(6) が `passed` で、展開した全 file の SHA-256 が fixture の hash 一覧と一致。receipt の `putIntentSha256` が intent file の SHA-256 と一致し、receipt の readback の hash が packageSha256 と一致。(7) が `passed`・`already-promoted`。catalog の artifactId・key・base・head・manifestSha256・packageBytes・entries が (2) 直後と (6) 後で一致し、(6) 後の `ownedCopies` が (2) 直後の `ownedCopies` を全て含む。手順 0 の 2 回目の status の head が GO 候補 head。
- 判定 C の照合 script の方式: **判定 C の offline 実行**。判定 C の担当が、AC12 の原記録の複製に対して、資格情報・本番設定・network に届かない環境で照合 script を書いて実行する。適用前に合成 fixture で一度通す。凍結する合成 fixture と期待値: 合格 1 組（上の期待値を全て満たす）と不合格 4 組（reference 文字列が 1 文字違う、(6) の file hash が 1 件違う、(7) が `already-promoted` でない、昇格後の `ownedCopies` が昇格前の 1 件を欠く）。script は合格組を合格、不合格 4 組をそれぞれ不合格と判定しなければならない。
- 機械検査: `contract-audit`、`docs-audit`、`git diff --stat`、AC13 の grep。
- A0/A1 主担当・モデル・ベンダー: Claude（この session）。
- A2 独立レビュー: 実施済み。担当・モデル・入力版・結果は §5.1。
- A3 統合担当・モデル・採否: 主担当（Claude）と owner。2026-10-17 に owner が §5.1 末尾の 5 点を主担当案で裁定し、主担当が §5.1 の採用分（#1〜#16、#19、#20）を revision 2 として §2〜§5 に統合した（#17・#18・#21 は不採用）。凍結は owner の本文確認後。
- C' 用に予約した担当・モデル・ベンダー: Grok 系（cursor-agent）。A2 では使っていない。
- 独立性の強化条件を満たせない場合の理由: A2 の (b)(c) は A1 主担当と同じ Claude 系。初稿への anchoring は (c) に初稿の方式を読ませないことで避けた。

## 5.1 A2 独立レビュー結果と A3 の採否案

入力版は A1 初稿（commit `d9b5c3e5`、内容 sha256 `e5a7060cb698c9a1d64e3a9ae4527191bff3cd2570c329d6337600d349929760`。(c) は §1 と §2 の問い・前提だけを抜き出した写しで、方式以降を読んでいない）。3 件は 2026-10-17 に互いの指摘を見せずに実施した。全件この PC 内の read-only session で、実ストア・資格情報・network・push は使っていない。prompt には「計画は未実装で、現コードとの差は実装対象」と明記した。原記録は git 管理外の `TestResults/a2-r2-promotion/`（入力の写し 2 種と、(a) の prompt・結果・log）。(b)(c) の prompt と結果の本文は A1 主担当 session の記録にある。担当・モデルは使用実績で、固定割り当てではない。

| 担当 | モデル | 判定 |
|---|---|---|
| (a) アーキテクチャゲート | Codex CLI、GPT 系（OpenAI） | 条件付き GO（送信と昇格の orchestration を同じ module に置く依存方向、送信の単体テスト境界） |
| (b) 不変条件を破る順序 | Claude Opus 系（新規 session） | 条件付き GO（I5 を破る順序 2 件、AC2・AC10 の欠陥 2 件。新しい state・field は不要） |
| (c) A0 のみからの代替構成 | Claude Sonnet 系（新規 session） | 判定なし。新 state `promote-pending` で 404 後も保持し、再送を禁じる案 |

主担当が実コードで確認した事実: publish は receipt 確定時に `ownedCopies=@()` にしてから operation 配下を列挙し直す（[EvidenceApplication.psm1:103](../../tools/Artifacts/EvidenceApplication.psm1)）。回復 GET が一致すると同じ artifactId の staging を全て adopted にする（[EvidenceCleanup.psm1:367](../../tools/Artifacts/EvidenceCleanup.psm1)）。既存ケース `staged-unsent-required` は putIntent を持つ `staged` を `unknown-state` に固定している（[EvidenceRetention.Tests.ps1:69](../../tools/Artifacts/tests/EvidenceRetention.Tests.ps1)）。fetch のローカル固有 reasonCode は preflight 前の `$local` で出し分けている（[EvidenceApplication.psm1:168](../../tools/Artifacts/EvidenceApplication.psm1)）。清掃は `staged` を参照する staging の operation を解除しない（[EvidenceCleanup.psm1:293](../../tools/Artifacts/EvidenceCleanup.psm1)）。

### findings と主担当の採否案

| # | 指摘（担当） | 分類 | 提案する反映 |
|---|---|---|---|
| 1 | 旧 runtime の日次 job が昇格由来の `put-pending` を 404 で `failed` にすると、D8 でその task が `corrupt-state` になり、遅れて確定した object も回収されない。§4 の「切替前に promote を使わない」と AC12 の「マージ前に共有 store で実行」が矛盾（b B-1） | 採用 | D8: `origin: stage` の `failed` を「putIntent あり・receipt 無し」に限って読む（保持判定は `unconfirmed-artifact` のまま）。D4: この `failed` も回復対象にし、GET 一致 → `ready`、404 → `staged`。§4・§5: AC12 の前に GO 候補 head で `schedule install` を行う（A3 裁定 5）。旧 worktree の CLI も同じ順序を作れるので、D8 の受け入れで閉じる |
| 2 | 共有した送信部が publish と同じく `ownedCopies` を置き換えると、元の ZIP・snapshot が追跡外に残り、満了削除後もローカルに永久に残る。回復一致時の「同じ artifactId の staging を全て adopted」も、停止した前回の readback staging を追跡外のまま残す（b B-2） | 採用 | D2(5)・D4: 昇格の `ownedCopies` は追記のみ（昇格前の集合 ⊆ 昇格後）。adopted にする staging は中の file が全て `ownedCopies` に入ったものに限り、それ以外は未採用のまま 7 日清掃に任せる。AC1 に包含の検査を追加。publish の既存挙動は変えない |
| 3 | D1 は preflight 後の state で分岐するので、AC2 の `put-pending`・`delete-pending` は分岐の時点で存在しない。`origin: stage` の `put-pending` は 404 で `staged` に戻って PUT される（AC2 の「PUT 0 件」と逆）（b B-3） | 採用 | AC2 を、origin・偽 transport の応答・期待 reasonCode（`unresolved-put` / `unresolved-delete` を含む）の組で書き直す。`origin: stage` の `put-pending` は AC2 から外し AC5・AC7 で扱う。D1 から到達しない「`put-pending` のまま」の分岐を消す |
| 4 | D5 は既存ケース `staged-unsent-required` の 1 行目（putIntent を持つ `staged` は `unknown-state`）を必ず壊すので、AC10 の「無変更で failed 0」は成立しない（b B-4） | 採用 | AC10: 意図して変える既存ケースは :69 の 1 行だけと列挙し、:70〜72（receipt を持つ `staged`、publish 由来の `staged`、origin 無しの `staged` は `unknown-state`）は無変更で維持する。それ以外の既存ケースの変更は §4 の差し戻し条件 |
| 5 | D5 のローカル削除経路が origin を条件に含めず、publish 由来・origin 欠落の `staged` がローカル削除されうる（b B-5） | 採用 | ローカル削除の経路は `origin: stage`・putIntent 無し・receipt 無しの全てを要する。`Invoke-EvidenceLocalDeleteGuarded` の中でも経路が `local` であることを再確認する |
| 6 | D1 の `staged` 分岐で reference の identity 照合が明示されていない（b B-6） | 採用 | D1: 分岐の前に fetch と同じ identity 照合（key・base・head・manifestSha256・packageBytes・期待 hash）を全分岐に適用。AC2 に reference 改変ケース |
| 7 | 応答を失った後に task を end すると、再実行は `resume-required` で `already-promoted` に収束しない（b B-7） | 採用（方式は A3） | A3 裁定 3 |
| 8 | D6 で `put-pending` → `staged` → local 分岐に入った場合、reasonCode の出し分けが preflight 前の判定のままで、ZIP 欠落が `unconfirmed` になる（b B-8） | 採用 | D6: reasonCode の出し分けは最終的な取得元で行う |
| 9 | D2(3) の「1 回の catalog 書込み」は `New-EvidenceRecoveryStage` の書込みを含めると 2 回。404 で `staged` に戻った後、前回の readback staging の operation が解除されず、そのディレクトリを失うと task が止まる（b B-9） | 採用 | D2(3): 書込み順序を「readback staging の登録 → putIntent・`put-pending`・operation」と事実どおりに書く（間の状態は `staged` のままで安全）。D4: 404 経路で同じ artifact の停止済み staging の operation も解除する |
| 10 | 前回の試行が receipt を書いた後に止まり、object が消えて 404 → `staged` に戻ると、再 promote が immutable な receipt の書込みで失敗する（b B-10） | 採用 | D2(5): 既存の receipt は回復と同じ照合（key・packageSha256・manifestSha256）で再利用する |
| 11 | 却下理由「404 後に `failed` にすると 7 日清掃で唯一の複製を失う」は不正確。元の staging は adopted で保護される。本当の欠点は `failed` が終点で回復できないこと（b B-11） | 採用 | §2 の却下理由を直す |
| 12 | AC1・AC5・AC8・AC9 の読み違えやすい箇所（readback は別 process、PUT 回数は合計 2 回、満了の境界は `deleteEligibleAt` ちょうど、`use` の until は満了より後）（b） | 採用 | 各 AC に合格例・不合格例を付ける |
| 13 | 送信 module に promote の orchestration と回復・状態遷移まで持たせると、依存が Send → Cleanup / StateStore になり責務境界が崩れる（a A-1） | 採用 | §3: `EvidenceSend.psm1` は送信プリミティブ（intent の照合・作成、lock 下の照合と PUT、readback、receipt の照合・作成）だけ。`Invoke-EvidencePromote` は `EvidenceApplication.psm1` に置き、Application が Send・Cleanup・StateStore を組み合わせる。Send は ArtifactApplication・EvidencePaths より下だけに依存する |
| 14 | 削除経路の選択を「純粋な保持 policy」に置くと責務名とずれる（a A-4） | 採用 | `Get-EvidenceDeletionRoute` は Cleanup 側の副作用なしの関数とし、単体テストする。保持 policy は期限・保護の適格性だけ |
| 15 | promote の呼び出し経路で D8 の不正形状が `corrupt-state` になり PUT 0 件であることを確認する（a A-5） | 採用 | AC11 に promote の経路のケースを追加 |
| 16 | 送信の単体テスト境界と停止点の注入点（AC6 の「intent 後・catalog 前」）が未記載（a A-6） | 採用 | §5: Send の純粋部（intent・receipt の照合）は直接テスト。送信の停止点は Send の FaultHook（`after-put-intent-file`、`after-put-pending`、`after-receipt-file`）で注入 |
| 17 | §1 の保持 policy と読戻し検査の現況記述が現コードと違う（a A-2、A-3） | 不採用（読み違い） | §1 は「putIntent も receipt も無いものだけを判定し、putIntent を持つ `staged` は `unknown-state`」「`ready` / `delete-pending` と、origin が `stage` 以外の `deleted` に receipt を要求」と書いており、指摘の内容と一致している。誤読を避けるため、統合時に条件を箇条に分けて書き直す |
| 18 | 新 state `promote-pending` を足し、404 の後も保持して再送を禁じる。復旧は新 artifact を stage し直す（c） | 不採用（A3 裁定 1） | 再送を禁じると、404 の後は遅れた PUT が確定しない限り永久に `ready` に達せず、reference と hash を保ったまま昇格する目的が満たせない。新 state は D8・保持・清掃・fetch・use の全分岐に網羅を要し、(c) 自身もその漏れを主要リスクに挙げている。(b) は同じ bytes の再送を受容可と判断した |
| 19 | 送信を試みた artifact は remote 削除（DELETE → GET 404）を経るまでローカル複製を消さない。照合 GET 一致時は元の state に戻す（c） | 採用（D5 と一致） | 初稿 D5 と同じ結論。独立に収束した |
| 20 | 送信を試みた artifact の remote 削除の前に、PUT を実行した process の停止を確認する（c） | 採用 | D5: remote 削除の適格性に、putIntent の process が停止済みであることを加える（`Test-EvidenceProcessStopped`）。`put-pending` の解決を経て `staged` に戻った時点で満たされるはずの条件を、削除の直前にも確かめる |
| 21 | `put-pending` / 昇格中の artifact の fetch は local ZIP を network 無しで使う（c） | 不採用 | 前スライスの「未解決の put があれば network 無しの preflight は拒否」（`unresolved-put`）を昇格由来だけ緩めることになり、I6 の既存契約を変える。D6（network 付き preflight で解決してから取得元を決める）で取得は成立する |

### A3 で owner が決めること

1. **404 の後の扱い（#18）。** 主担当案は初稿どおり「`staged` に戻し、同じ bytes の再送を許す。putIntent を持つ artifact は remote 削除だけ」。代替は (c) の「新 state で保持し、再送を禁じる」。(b) は主担当案を支持。
2. **D5 の残存リスク（未決事項 2）。** 削除の後に遅れて確定する PUT を、30 日以上未完了の HTTP 要求を要する受容リスクとして README に書く。(b) は受容に賛成（有限回の追加確認ではリスクを消せない）。主担当案は受容。
3. **`already-promoted`（未決事項 3、#7）。** 主担当案: `status: passed`、初回の成功と同じ出力の形。`ready` かつ identity 一致の判定は PUT も state 変更も無いので、task の `active` 確認より前に置き、終了済み task でも `already-promoted` を返す。昇格（PUT）が要る分岐だけ `resume-required`。
4. **配置（未決事項 4、#13・#14）。** 主担当案は (a) の案: `EvidenceSend.psm1` は送信プリミティブだけ、promote は Application、経路の選択は Cleanup。
5. **AC12 の前の runtime 切替（#1）。** 主担当案: 判定 C の実行担当が、AC12 の直前に GO 候補 head で `schedule install` を行う（日次 job が昇格由来の state を新しい reader で扱うようにする）。これは共有 store の登録済み runtime を変える操作なので、A3 で owner の明示承認を受ける。承認しない場合は、AC12 を旧 runtime の job が走らない時間帯に行い、`passed` で終わらなければその場で解決する（#1 の D8・D4 修正で、旧 job が `failed` にしても新 head で回復できる）。

2026-10-17、owner は 5 点を全て主担当案で裁定した（5 は AC12 の直前の `schedule install` の明示承認を含む）。主担当が採用分を §2〜§5 に revision 2 として統合した。凍結は owner の本文確認後。

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
