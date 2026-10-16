# Artifact Storage / Evidence の R2 昇格（送信）HANDOFF

## 0. メタデータ

- type: `slice`
- status: `B`（Phase A revision 5 を 2026-10-17 に owner の本文確認で凍結。凍結内容は §2〜§5。設計判断を変えるときは Phase A を新しい revision として再開する。revision 4 以前の経緯は §5.1〜§5.4）
- branch: `claude/evidence-r2-promotion`（PR の base は `develop`）
- implementation base commit: `ec5288d73804cc23ec0edfe35370d16751e40157`（`origin/develop`、PR #129 マージ後。凍結時に確定）
- implementation head commit: 未固定
- risk: `high`（network を使う送信経路を Evidence に増やす。catalog の state 遷移と PUT 応答不明の fail-closed 契約に接続する。revision 3 で、catalog 検査・保持 policy・清掃・回復のコードには触れなくなった）
- owner: OSM maintainers
- created: 2026-10-17
- expires: 2026-11-17 または置換 revision
- harvest to: 操作と既知の制限を `tools/Artifacts/README.md`（「ローカル固定」「Evidence v2 の通常操作」「エージェント操作の事前承認」）へ。昇格の使い方は `.agents/skills/osm-workflow/references/review-evidence.md` の「保存先とリモートへの配布」（本スライスの実装対象）。`BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §4 C に「送信は完了。回復と寿命は後続スライス、Cloud・別 PC からの取得経路は未着手」と、§2 の後続スライスの入力を記録する。
- Phase A snapshot の取得案内・状態: 凍結対象版は commit `e9d958b58886f650014592d7cd36313b7be17cc4` の本 HANDOFF（内容 sha256 `20e368b670d081ccb6bdd1950ec3c11fd4103a6ffd7ce610cff30146ee02a97b`、LF 正規化した git blob 内容）。Git 上の commit で固定し、Artifact Storage には未保存（同一 PC の担当は commit から読む）
- Phase B result snapshot / evidence bundle / C' blind bundle の取得案内・状態: 未生成（未到達 Phase）
- 引継ぎ票: 次は **Phase B（実装）**、役割は実装担当。作業は §4 の順序で、§2 の D1〜D8 と受け入れ条件 AC1〜AC13 を満たす実装・テスト追加・文書変更（AC10 の実 R2 は判定 C の担当で、B は行わない）。入力は凍結した Phase A revision 5（commit `e9d958b5`、内容 sha256 `20e368b670d081ccb6bdd1950ec3c11fd4103a6ffd7ce610cff30146ee02a97b`）。凍結の記録はこの HANDOFF の次の commit で、§0 以外の本文は凍結対象版と同じ。開始時に、自分の worktree の HEAD がこの凍結記録の commit 以降で、作業ツリーが dirty でないこと、`git diff e9d958b5 HEAD -- docs/handoff/ARTIFACT_EVIDENCE_R2_PROMOTION.md` の差が §0 だけであることを照合し、食い違えば始めない。origin とは照合しない（同一 PC、未 push）。必要な権限: ローカルの PowerShell 7 と git、3 project の Release build（`dotnet build`）、`EvidenceCleanup.Tests.ps1` 用に PATH 上の `rg`。Unity・sandbox 外実行・資格情報・network は不要。**実ストアの catalog・transfers・資格情報を読まず、編集もしない。R2 にも触れない。`schedule install` も行わない。** B の出口で `pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1` を実行し、§5 の起点 filter、AC9 の diff 確認、AC11 の grep を通し、実装結果（§6）を書き、引継ぎ票を次の担当（動作確認は `対象なし` なので発見 C）向けに更新して commit する。push はしない。判定 C の実行、C' の起動、判定 C の照合 script は担わない。停止規則: §4 の「Phase B から Phase A へ差し戻す条件」に該当したら実装を止めて判断を返す。新しい事実は `phases-and-handoff.md` の「新しい事実の分類」で分類する。引き渡さないもの: なし（C' 用の blind bundle は未生成）。
- （旧）引継ぎ票（revision 5 の確認。記録として残す）: 次は owner による revision 5 の本文確認と凍結。owner は 2026-10-17 に revision 5 を確認し、凍結を指示した。
- （旧）引継ぎ票（revision 4。記録として残す）: 次は owner による revision 4 の本文確認と凍結（commit `f9b2eba7`）。owner は凍結の前に追加レビュー（Codex）を依頼し、その指摘を revision 5 に反映した（§5.4）。
- （旧）引継ぎ票（revision 3。記録として残す）: 次は owner による revision 3 の本文確認と凍結（commit `49bf9b11`）。owner は凍結の前に外部レビュー（Web 版 GPT）を依頼し、その指摘を revision 4 に反映した（§5.3）。
- （旧）引継ぎ票（revision 2。記録として残す）: 次は owner による revision 2 の本文確認と凍結（commit `8494c791`）。owner は凍結せず、実装単位が大きいとして 2 スライスへの分割を指示した（§5.2）。
- （旧）引継ぎ票（A2 → A3。記録として残す）: 次は A3（統合と凍結）。入力は §5.1 と A1 初稿（commit `d9b5c3e5`）。owner は 2026-10-17 に 5 点を主担当案で裁定した。
- （旧）引継ぎ票（A1 → A2。記録として残す）: 次は A2（独立レビュー）。入力は A1 初稿の commit `d9b5c3e5`（内容 sha256 `e5a7060cb698c9a1d64e3a9ae4527191bff3cd2570c329d6337600d349929760`）。観点は §5 の A2 欄。

## 1. 目的と対象外

- 目的: `evidence stage` でローカル固定した Evidence（state `staged`）を、**同じ artifact・同じ ZIP bytes のまま** R2 へ置けるようにする（昇格の送信）。reference と packageSha256 を変えず、取得案内は `locality` だけを `local` から `promoted` に更新すれば済む。PUT 応答不明は既存の `put-pending` 回復へそのまま接続し、本スライスでは既存の回復・保持・清掃のコードを変えない。
- 現況（2026-10-17、`origin/develop` = `ec5288d7`）:
  - `evidence stage`（PR #128）は publish と同じ準備（selection → guard → network 無し preflight → snapshot → `PackageIO.CreateVerified` → `Extract` 再検証）までを行い、artifact を state `staged`・`origin: stage`・`putIntent=null`・`receipt=null` で catalog に入れる。ZIP は staging operation 内の `ownedCopies` に唯一の複製として残る。`key` は `Get-EvidenceKey` が artifactId から導く値を未送信のまま持つので、reference（`osm-evidence-v2:`）は R2 に置いても変わらない（[EvidenceApplication.psm1](../../tools/Artifacts/EvidenceApplication.psm1) `Invoke-EvidenceStage`）。
  - publish の送信部（同ファイル `Invoke-EvidencePublish`）は、`intents/<artifactId>.json` を `Write-EvidenceAtomic -Immutable`（上書き不可）で書き → catalog に `putIntent` を記録 → package に read lock（`FileShare.Read`）を取って PUT → 別 process の readback GET で bytes・hash を照合 → `receipts/<artifactId>.json` を immutable で書き → state `ready`。PUT 応答不明は同 key へ再送せず `put-pending` のまま残す。停止点の注入は `$script:FaultHook`（`after-put-intent`、`after-receipt`）。transport（[R2ArtifactTransport.cs](../../tools/Artifacts/Transport/R2ArtifactTransport.cs)）は条件付き PUT を使っていない。
  - `put-pending` の回復（[EvidenceCleanup.psm1](../../tools/Artifacts/EvidenceCleanup.psm1) `Resolve-EvidencePutGuarded`）は origin を見ない。GET が一致すれば、既存の receipt があれば照合して再利用し（無ければ書き）、`ownedCopies` に回復 GET の file を追記して `ready`。GET が 404 なら `failed`。`failed` は保持 policy で `unconfirmed-artifact` になり、清掃・DELETE されない（既存 publish でも、404 の後に先行 PUT が遅れて確定した remote object は削除されない residue として残る。既存の受容）。
  - 読戻し検査（[EvidenceStateStore.psm1](../../tools/Artifacts/EvidenceStateStore.psm1) `Read-EvidenceTask`）は state を 6 値、`origin` を `publish` / `stage` に限る。origin と state の組み合わせ自体は制限していない。receipt は `ready` と `delete-pending`（origin を問わない）、および origin が `stage` 以外の `deleted` に要求する。したがって `origin: stage` の `put-pending`（putIntent あり）・`ready`（receipt あり）・`failed`（putIntent あり）は、今の reader で読める。
  - 保持 policy（[EvidenceRetentionPolicy.psm1](../../tools/Artifacts/EvidenceRetentionPolicy.psm1)）は、`origin: stage` で putIntent も receipt も null の `staged` を `ready` と同じ経路で判定し、それ以外の `staged` は全て `unknown-state`（削除しない）。清掃は `staged` をローカル削除、`ready` を remote 削除（deleteIntent → DELETE → GET 404 `NoSuchKey` → `deleted`）に分ける。
  - 清掃は、`staged` または `put-pending` の artifact を参照する staging を、7 日清掃から一律に除外する（[EvidenceCleanup.psm1:293](../../tools/Artifacts/EvidenceCleanup.psm1)）。
  - fetch は preflight 前の catalog の読取りで、対象が `staged` なら network 無しの preflight と local 分岐、そうでなければ network 付き preflight と remote 分岐を選ぶ。`put-pending` の artifact は network 付き preflight で回復され、`ready` になれば remote 分岐で取得でき、`failed` になれば `identity-mismatch` で拒否される。inspect は `staged` → `local`、`ready` → `promoted` を既に返す（同ファイル `Invoke-EvidenceInspect`）。
  - `use` / `release` は `ready` と `staged` を受け付ける。
  - 前スライスで後続へ送った入力（program §4 C）: 旧 HANDOFF revision 2 の D4 / D4' / I8 / S4、追加レビュー ADD-01（R2 の GET 404 は先行 PUT の失敗確定ではなく、同 key 再送禁止の既存 publish 契約と衝突する）、前回所見 R2-02（昇格時の catalog 更新単位）。全文は削除前の HANDOFF（`docs/handoff/ARTIFACT_EVIDENCE_LOCAL_STAGE.md`、commit `72a8b0c`、revision 2 は `16509b46^`）。本スライスは ADD-01 に「再送しない」で答え（§2 D3）、R2-02 に 2 回の catalog 書込みで答える（D2）。
- 対象外: 回復 GET の 404 の後に `staged` へ戻す回復・再送、送信を試みた `staged` の寿命と remote 削除、fetch の取得元の決め直し、catalog の組み合わせ検査、runtime の切替（以上は後続スライス。§2）。publish と promote の送信部の共有（`EvidenceSend.psm1`）。Cloud・別 PC が R2 から fetch する経路と token の受け渡し（program §4 C）。`ready` artifact のローカル copy からの network 無し fetch。条件付き PUT など transport の変更。Unity 側の変更なし。

## 2. 意思決定と受け入れ境界（revision 5。owner の本文確認待ち）

- このスライスが答える問い: 「`staged` の artifact を、同じ reference・同じ packageSha256・同じ bytes のまま R2 へ昇格でき、PUT 応答不明を既存の `put-pending` 回復（一致 → `ready`、404 → `failed` で凍結・非削除）へ接続することで、既存の回復・保持・清掃のコードを変えずに、送った可能性のある bytes を確認なしに消さない状態へ閉じられるか」。
- A0 で owner が固定した前提: 範囲は昇格だけ。Cloud・別 PC からの取得経路は後続（2026-10-17）。実装単位を 2 スライスに分け、本スライスは送信だけ（2026-10-17）。
- revision 2 の A3 裁定のうち本スライスに残るもの: (3) `already-promoted` は `status: passed`、初回の成功と同じ出力の形で、終了済み task でも返す。(4) のうち「promote は `EvidenceApplication.psm1`」。裁定 (1)（404 後の `staged` と再送）・(2)（削除後の遅延 PUT の受容）・(5)（AC12 前の `schedule install`）は後続スライスへ移った。裁定 (4) のうち `EvidenceSend.psm1` は作らない（§5.2）。
- 方式:
  - **D1 `evidence promote`。** `pwsh tools/artifacts.ps1 evidence promote --profile osm --config <cfg> --reference <ref> --sha256 <期待 packageSha256>`。順序は次のとおり。
    1. config と reference を読み、reference の taskId で task guard を取る。
    2. 通常の（network を使う）preflight と Sync を行う。同じ task の `put-pending` は既存の回復で `ready` または `failed` に解決される。解決できなければ既存どおり `unresolved-put` / `unresolved-delete`。
    3. catalog に reference の artifactId が無ければ `catalog-missing`、`deleted` なら `expired`。
    - 手順 1〜3 の既存の検査が先に拒否した場合は、その既存の reasonCode をそのまま返し、`identity-mismatch` へ変換しない。具体的には、reference の文字列・形式・key（taskId と artifactId から導く値との不一致を含む）の不正は `Read-EvidenceReference` の `unsupported-schema` / `invalid-reference`、登録済み `ownedCopies`（ZIP を含む）の欠落・長さ・hash の不一致は preflight の `changed-copy`。いずれも PUT 0 件。
    4. **全分岐の前に** identity を照合する: key・base・head・manifestSha256・packageBytes が reference と、packageSha256 が期待 hash と一致すること。不一致は `identity-mismatch`、PUT 0 件。
    5. `ready` かつ `origin: stage`: 送信せず `status: passed`、`reasonCode: already-promoted`。出力は初回の成功と同じ形（`reference`・`packageSha256`・`locality: promoted`・`receipt`・`residue.remote: ready`）。PUT も state 変更も無いので、task の状態（`active` / `ended`）を問わない。応答を失った昇格の再実行も、手順 2 の回復で `ready` になればここで収束する。
    6. `staged` かつ `origin: stage` かつ putIntent が null かつ receipt が null: task が `active` でなければ `resume-required`（publish・stage と同じ）。`active` なら D2 へ進む。
    7. それ以外（`origin: publish` の全ての state、origin の無い artifact、`origin: stage` の `failed`、putIntent または receipt を持つ `staged`）: `invalid-source`、PUT 0 件。`origin: stage` の `failed` の回復は後続スライス。
  - **D2 送信（同じ bytes）。** catalog の書込みは 2 回（R2-02 の更新単位）。
    1. catalog の `ownedCopies` から packageSha256・packageBytes が一致する ZIP を選び、read lock（`FileShare.Read`）で開く。lock を保持したまま、その file の SHA-256 と長さを catalog の値と照合する。不一致なら PUT せず `identity-mismatch`。再 snapshot・再 package はしない（snapshot・package を作る関数を呼ばない）。
    2. putIntent: 再利用を許すのは、catalog にまだ `putIntent` が無い `staged` だけ。正規の path（`<task directory>/intents/<artifactId>.json`）に file が既に在る（前回の promote が書込み A の前に止まった）なら、JSON として読め、publish の intent と同じ field 集合（`schemaVersion`・`artifactId`・`key`・`packageBytes`・`packageSha256`・`createdAt`・`process`）を持ち、`schemaVersion` が 2、`artifactId`・`key`・`packageBytes`・`packageSha256` が catalog の artifact と一致する場合に限り再利用し、その file の SHA-256 を `putIntent.sha256` にする（`createdAt`・`process` は前回の値のまま使う）。どれかが欠ける・食い違う・読めない場合は `unresolved-put`、PUT 0 件。無ければ publish と同じ形で immutable に書く。immutable の書込みが rename 前に失敗した場合は intent file が無いままで、再実行は新しく書く（`.pending` の一時 file は未登録の residue として残りうる。既存 publish と同じ）。ここで止まっても artifact は `staged`・putIntent 無しのままで、何も送っていない。
    3. **書込み A:** 先に `New-ArtifactOperation` で readback 用の新しいディレクトリを作る（ここで止まれば、catalog に無い空のディレクトリが残るだけ。既存 publish と同じ）。次に 1 回の catalog 書込みで、そのディレクトリを指す staging（`artifactId` 付き、`adopted=false`、`operation` 付き）の登録・`putIntent` の参照・state `put-pending`・artifact の `operation` を記録する。staging と artifact の `operation` は同じ値（この process の identity、`childMarker` は `<ディレクトリ>/readback-process.json`、`path` はそのディレクトリ）で、既存の `Assert-EvidenceOperationStopped` がそのまま判定できる形にする。既存の `New-EvidenceRecoveryStage` は自分で catalog を書くので、そのままは使わない。
    4. lock を保持したまま同じ file を PUT し、別 process の readback（`Invoke-ArtifactReadback`）で bytes・hash を照合する。同じ process の GET の一致では `ready` にしない。
    5. receipt を publish と同じ形（`putIntentSha256` を含む）で immutable に書く。
    6. **書込み B:** 1 回の catalog 書込みで、`receipt`・state `ready`・`operation=null` を記録する。`ownedCopies` は**追記のみ**で、昇格前の集合を全て残し、readback staging の file を加える。adopted にするのは、この promote が書込み A で登録した readback staging だけ。`origin: stage` は残す。
    - 出力は `reference`（入力と同じ文字列）、`packageSha256`、`locality: promoted`、`receipt`、`residue.remote`。PUT または readback が確認できなければ `put-pending` のまま `status: failed`・`reasonCode: unconfirmed` を返す。
    - 例外で失敗した場合の後始末（promote 専用。publish の `Clear-EvidencePackageContext` は使わず、変えない）: task guard を保持したまま、disk の catalog を `Read-EvidenceTask` で**読み直し**、永続化済みの状態を正とする。メモリ上の object（未確定の state・receipt・`ownedCopies`・adopted・増えた generation）は捨て、書き戻さない。読み直した state が `staged`（書込み A が未確定）なら何もしない。`put-pending`（A 確定・B 未確定）なら、readback の子 process の marker が無いか、子が終了して pipe を閉じたと読める場合だけ、読み直した artifact と、書込み A で登録した staging の `operation` を外して書く（それ以外は残し、次の preflight の `Assert-EvidenceOperationStopped` に任せる）。`ready`（B 確定済み）なら何もしない。返す結果は読み直した state に合わせる（`ready` なら `status: passed`、`reasonCode: complete`）。読み直しができなければ何も書かず `unconfirmed` を返す。process ごと止まった場合は `operation` が残り、同じ判定で次の preflight が処理する。
    - 停止点の注入: `$script:FaultHook` に promote 用の名前 `promote-before-lock`（D1 の分岐の後・手順 1 の lock の前）、`promote-after-intent-file`（手順 2 の後・書込み A の前）、`promote-after-put-pending`（書込み A の後・PUT の前）、`promote-after-receipt-file`（手順 5 の後・書込み B の前）を加える。publish の `after-put-intent` / `after-receipt` は変えない。
    - 送信の手順は `Invoke-EvidencePromote` の中に独立した内部関数として書き、publish とは共有しない（後続で共有するときに機械的に抽出できる形にする）。
  - **D3 再送しない（ADD-01 への答え）。** promote が PUT を発行するのは、putIntent を catalog に記録していない `staged` からの 1 回だけ。`put-pending` のまま、または回復で `failed` になった artifact には PUT しない（D1 手順 7）。既存 publish の「PUT 応答不明を同 key へ再送しない」契約は promote にもそのまま成り立つ。404 の後の再送と回復は後続スライスで扱う。
  - **D4 既存の回復への接続。** promote 由来の `put-pending` は、既存の `Resolve-EvidencePutGuarded` が publish 由来と同じに扱う。GET 一致 → `ready`（既存の receipt があれば照合して再利用）、404 → `failed`、その他 → `unresolved-put`。本スライスは回復のコードを変えない。`origin: stage` の `failed` は、保持 policy で `unconfirmed-artifact`（凍結・非削除）になり、ZIP と putIntent を保持したまま残る。この状態からの取得・再昇格はできない（README の既知の制限。後続スライスで回復する）。また、promote が書込み A の後に止まり、回復 GET が一致した場合、既存の回復は同じ artifactId の staging を全て adopted にするので、promote の readback staging は中の未登録 file（readback の marker 等）ごと残る。publish の回復と同じ既存挙動で、payload ではないローカル residue として受容し、後続スライスで扱う（§2 の後続スライスの制約）。回復が一致した場合も `ownedCopies` は既存どおり追記なので、昇格前の集合は残る。既存の回復が既存の receipt を再利用するときの照合は key・packageSha256・manifestSha256 の 3 つで、新しく書く receipt の全 identity（taskId・artifactId・base・head・packageBytes 等）には及ばない。本スライスで promote が書く receipt は同じ artifact の固定 path に immutable で書くので実害の経路は見つかっていないが、既存回復を変えない前提の既知リスクとして README には書かず本 HANDOFF に記録し、後続スライスの入力にする。
  - **D5 到達する state と寿命（既存のまま）。** 本スライスで promote が作る state は `staged`（putIntent 無し）・`put-pending`（putIntent あり）・`ready`（receipt あり）・`failed`（putIntent あり、回復の 404 から）の 4 つで、全て今の reader・保持 policy・清掃が扱う。PUT は必ず書込み A（putIntent の記録）の後に発行されるので、送った可能性のある bytes を持つ artifact は putIntent を持ち、ローカル削除の経路（putIntent も receipt も無い `staged` だけ）に乗らない。`ready` は既存の remote 削除、`failed` は非削除。書込み A で登録した readback staging は、artifact が `put-pending` の間は 7 日清掃から除外され（既存）、`ready` で adopted、`failed` の後は既存の 7 日清掃に乗る。
  - **D6 inspect と fetch。** コードを変えない。inspect は既に `staged` → `local`、`ready` → `promoted` を返す。昇格済み（`ready`）は既存の remote 分岐で取得する（ローカル copy からの取得は対象外）。昇格中（`put-pending`）の artifact の fetch は、network 付き preflight の回復で `ready` になれば取得でき、`failed` になれば `identity-mismatch` で拒否される（publish と同じ既存挙動）。
  - **D7 ワークフロー文書。** `review-evidence.md` の「保存先とリモートへの配布」で、「昇格経路が後続スライスで成立するまで従来どおり publish」を、「Cloud・別 host の読み手、または端末喪失に備えて owner が指示した時は、同じ artifact を `evidence promote` で昇格する。reference と packageSha256 は変わらないので、取得案内は `locality` だけを更新する。昇格が `failed` で止まった場合は、その artifact を remote 用に使わず、従来の publish で新しい artifact を作る（取得案内も新しく作る）。Cloud・別 host からの取得経路は後続（program §4 C）で、成立するまでその読み手の作業は assist」に置き換える。`SKILL.md`・`phases-and-handoff.md`・`handoff-template.md` は変えない。
  - **D8 README と事前承認。** `tools/Artifacts/README.md` の冒頭と「ローカル固定」の「昇格は後続」を、promote の操作（D1・D2）、出力、`already-promoted`、PUT 応答不明の扱い（D3・D4）、既知の制限（`failed` で止まった昇格は取得・再昇格できない。回復は後続スライス）に置き換える。「エージェント操作の事前承認」に `evidence promote` を既存 publish と同じ承認範囲の通常操作として加える（同じ endpoint・bucket・prefix・既存資格情報。新しい資格情報・公開範囲は無い）。runtime の切替は不要（D5 のとおり、promote が作る shape は今の runtime の日次 job が読み、同じ回復を行う）。
- 却下した代替案:
  - revision 2 の全体（送信・回復・寿命・catalog 検査・runtime 切替）を 1 スライスで行う: 実装単位が大きく、C / C' の指摘と修正が膨らむ（2026-10-17 owner）。§5.2。
  - 回復（404 → `staged`）だけを先に入れ、保持・削除経路を後にする: 送信を試みた `staged` がローカル削除の経路に乗るか、永久に `unknown-state` で削除不能になる。回復と寿命は同じスライスに置く（後続スライス）。
  - 後続スライスで `origin: stage` の `failed` を `corrupt-state` にする: 本スライスの runtime 自身が回復の 404 でこの shape を正当に作るので、その task の catalog 全体が読めなくなる（§5.2 Fable の指摘）。後続スライスへの制約として §2 に書く。
  - 送信部を `EvidenceSend.psm1` に共有する: publish に触れ、既存の publish の挙動が丸ごとレビュー対象になる。後続の入力。
  - 条件付き PUT（`If-None-Match: *`）: transport の変更と publish への波及。
  - 昇格のたびに再 snapshot して新しい artifact を publish する: reference と hash が変わる（それは既存 publish で今も可能）。
- 進める最低条件:
  - 不変条件（起きてはいけないこと）:
    - **I1** promote が、D1 手順 6 の条件（`staged`・`origin: stage`・putIntent と receipt が null・identity と期待 hash が一致・task が `active`）を満たさない artifact に PUT を発行する。1 回の promote が 2 回以上 PUT を発行する。
    - **I2** promote が、catalog の packageSha256・packageBytes と一致しない bytes を PUT する。照合から PUT 完了までの間に ZIP が書き換えられる。再 snapshot・再 package が起きる。
    - **I3** 昇格の前後（回復を含む）で、reference 文字列・artifactId・key・packageSha256・manifestSha256・base・head・packageBytes・entries・taskEndedAt・deleteEligibleAt・protections のいずれかが変わる。
    - **I4** 別 process の readback で bytes・hash の一致を確認する前に、promote が state `ready` または receipt を書く。
    - **I5** disk 上の catalog が putIntent と `put-pending` を記録する前に PUT が発行される。disk 上の catalog に receipt と `ready` が、readback の一致より前に記録される。どの書込みの途中で止まっても、次に新しく読み直した catalog が `Read-EvidenceTask` で読めない、または既存の回復で `put-pending` から `ready` / `failed` へ進めない。正規 path 以外の intent file、または field の欠落・不一致がある intent file を使って PUT する。昇格前の `ownedCopies` のいずれかが、昇格の後に `ownedCopies` から外れる。この promote が登録していない staging を promote が adopted にする。
    - **I6** 既存の publish・stage・fetch・inspect・use・release・cleanup・回復・保持判定・catalog 検査の挙動と reasonCode の意味が変わる（既存テストを 1 件も変えずに通る）。
    - **I7** 保護中（有効な `use`）または task 終了 + 30 日前の artifact が、昇格の経路で削除される。
  - 成立条件（起きなければならないこと）:
    - **S1** `staged` → promote → `passed`。返る reference と packageSha256 が stage の出力と同じで、inspect の `locality` が `promoted`。別 process の remote fetch が同じ reference と期待 hash で成功し、全 entry の hash が元 file と一致する。
    - **S2** PUT 応答不明 → `put-pending` → 既存の回復の 2 終点: GET 一致なら `ready`（その後の promote は `already-promoted`）。404 なら `failed` で、ZIP・putIntent・保護が残り、満了後も削除されない。
    - **S3** `ready` の再 promote が、task の状態を問わず `already-promoted`（PUT 0 件）で収束する。
    - **S4** intent file を書いた後・書込み A の前に止まった promote は、再実行で既存の intent file を再利用して PUT 1 回で `ready` になる。
    - **S5** 実 R2 で、隔離した task の `staged` を 1 回昇格し、別 process の remote fetch で全 entry が一致する（判定必須。§5）。
- 受け入れ条件（検証例。offline は隔離した temp store・偽 transport・時刻注入。実ストア・network 不使用。実 R2 は AC10 だけ）:
  - **AC1**（S1, I3, I4, I5）: fixture の `staged` を promote。偽 transport が記録した PUT bytes の SHA-256 が packageSha256 と一致し、PUT は 1 回。`ready` と receipt は別 process の readback の後にだけ書かれる（合格例: readback child が一致を返した後に `ready`。不合格例: 同じ process の GET の一致だけで `ready`。偽の readback を失敗させると `put-pending` のまま receipt 無し）。昇格前後で I3 の全 field と reference 文字列が一致する。昇格後の `ownedCopies` が昇格前の集合を全て含む。adopted に変わった staging は書込み A で登録した 1 つだけ。inspect の `locality` が `local` → `promoted`。別 process の fetch（remote 分岐）が成功する。
  - **AC2**（I1）: 次の組で promote し、全て PUT 0 件。(a) `origin: publish` の `ready` → `invalid-source`。(b) `origin: publish` の `put-pending` で偽 GET が 404 → 回復で `failed` → `invalid-source`。同じく GET 一致 → `ready` → `invalid-source`。GET が別 bytes → `unresolved-put`。(c) `origin: stage` の `failed` → `invalid-source`。(d) putIntent を持つ `staged`、receipt を持つ `staged` → `invalid-source`。(e) `delete-pending` で偽 GET 404 → `deleted` → `expired`。GET が別 bytes → `unresolved-delete`。(f) `deleted` → `expired`。(g) catalog に無い artifactId → `catalog-missing`。(h) reference として形式が正しく key も導出どおりで、catalog の artifact と base・head・manifestSha256・packageBytes のどれかだけが違うもの、および期待 hash の不一致 → `identity-mismatch`。(i) reference の key だけを改変（taskId・artifactId から導く値と不一致）→ `invalid-reference`。全て CLI の入口から与え、preflight を stub しない。
  - **AC3**（S3, I1）: 昇格済み（`origin: stage` の `ready`）への promote が `already-promoted`、PUT 0 件、出力の形（reference・packageSha256・`locality: promoted`・receipt・`residue.remote: ready`）が初回の成功と同じ。task が `active` の場合も `ended` の場合も同じ結果（不合格例: `ended` で `resume-required`）。期待 hash が違えば `identity-mismatch`。`ended` の task の `staged` への promote は `resume-required`、PUT 0 件。
  - **AC4**（I2）: promote の前に ZIP を 1 byte 改変した `staged` の promote は、preflight の登録済み copy の検査で `changed-copy`、PUT 0 件（CLI の入口から。preflight を stub しない）。lock 下の照合は、FaultHook `promote-before-lock`（preflight と D1 の分岐を通過した後・ZIP の lock を取る前）で ZIP を 1 byte 改変して試し、`identity-mismatch`・PUT 0 件。照合後・PUT 前に別 process が ZIP を書込みで開こうとすると共有違反で失敗する（lock の保持）。snapshot・package を作る関数（`Initialize-EvidencePackage`、`Copy-EvidenceSnapshot`、`PackageIO.CreateVerified`）が promote の経路から呼ばれない（呼び出し記録 0 件）。
  - **AC5**（S2, I1, I5）: promote の PUT 応答不明を注入 → `put-pending`、putIntent 記録済み、PUT は 1 回（再送しない）。続く network 付き preflight（別の fetch または promote）の回復で、偽 GET 一致 → `ready`（receipt あり、`ownedCopies` は昇格前を含む）→ 再 promote は `already-promoted`。別 fixture で GET 404 → `failed`、ZIP・putIntent・ownedCopies・protections が残る。この `failed` への promote は `invalid-source`、PUT 0 件（通算 PUT 1 回）。時刻注入で task 終了 + 30 日を過ぎても保持判定は `unconfirmed-artifact` で削除されない。
  - **AC6**（S4, S2, I5）: FaultHook で停止を注入する。`promote-after-intent-file` で停止 → artifact は `staged`・putIntent 無しのまま、local fetch 成立、PUT 0 件。再 promote は既存の intent file を照合して再利用し（intent file の sha256 不変）、PUT 1 回で `ready`。intent file の内容が key・packageSha256・artifactId のどれかで食い違えば `unresolved-put` で PUT 0 件。`promote-after-put-pending` で停止 → `put-pending`、PUT 0 件 → 回復で偽 GET 404 → `failed`（AC5 と同じ終点）。`promote-after-receipt-file` で停止 → `put-pending`、receipt file はあるが catalog に receipt 無し（receipt を書いただけでは `ready` にならない）→ 回復で GET 一致 → 既存の receipt を照合して再利用して `ready` → 再 promote は `already-promoted`。intent の不一致は field ごとに 1 ケース（`schemaVersion`・`artifactId`・`key`・`packageBytes`・`packageSha256` の各改変、field の欠落、JSON として読めない）で、全て `unresolved-put`・PUT 0 件。各停止の後は、promote の process 内の object ではなく、**disk から新しく読み直した catalog** で判定する。テストは `Test-EvidenceProcessStopped` の既存の注入点（`ProcessHook`）で promote の process を停止済みとして扱う。`promote-after-put-pending` の停止の後、回復の 3 終点（GET 一致・404・別 bytes）それぞれで、readback staging の `operation` と `adopted`、artifact の `operation`、`ownedCopies`（昇格前を含む）、最終の state と reasonCode を観測し、続けて (a) 同じ task の stage・fetch・use が `operation-pending` や `changed-copy` で不当に止まらないこと（別 bytes は既存どおり `unresolved-put`）、(b) 時刻注入で task 終了 + 30 日の後に cleanup の dry-run と実行を行い、`ready` は既存 remote 削除、`failed` は非削除（`unconfirmed-artifact`）、`staged`（putIntent 無し）は既存ローカル削除になり、ZIP を含む payload が他の経路で消えないことを確かめる。
  - **AC7**（I6）: 昇格中（`put-pending`、`origin: stage`）の artifact を fetch。回復で偽 GET 一致 → `ready` に解決されて remote 分岐で成功。404 → `failed` → `identity-mismatch`（publish と同じ既存挙動）。
  - **AC8**（I7）: 時刻 `T` を注入して固定し、既存の event 適用経路を使う（catalog の期限を手で書かない）。(1) active な task で stage。(2) 終了予定の `T` を基準に `until = T + 30 日 + 1 時間` で `use`。(3) 同じ active な task で promote し、`ready` の artifact に protection が残ることを確認。(4) 時刻 `T` の通常の ended event を `workflow-task end` の経路で適用し、`deleteEligibleAt = T + 30 日` を確認。(5) 時計を `T + 30 日` の直後（until より前）へ進め、cleanup が `in-use`・DELETE 0 件。(6) `release` の後、同じ時計で cleanup が既存の remote 削除経路に進む。不合格例: until を満了より前に置いて保護なしで通る、または active のまま時計だけ進めて `active/end-not-recorded` を `in-use` と取り違える。
  - **AC9**（I6）: 既存の `tools/Artifacts/tests/*.Tests.ps1` と `tools/Workflow/tests/*.Tests.ps1` のケースを 1 件も変えずに failed 0。`EvidenceCleanup.psm1`・`EvidenceStateStore.psm1`・`EvidenceRetentionPolicy.psm1`・`EvidenceContract.psm1` に diff が無い。
  - **AC10**（S5、判定必須）: §5 の手順で実 R2 に 1 回。
  - **AC12**（I5）: 偽 transport の PUT 呼び出しの中で、disk 上の catalog を `Read-EvidenceTask` で新しく読み直し、対象 artifact が `put-pending`・`putIntent` あり、その intent file が正規 path に在り `putIntent.sha256` と一致することを assert する。readback の子 process が一致を返すまで、disk 上の catalog に receipt と `ready` が無いことを assert する。
  - **AC13**（I5）: `Write-EvidenceAtomic` の既存の `WriteHook` で、書込み A と書込み B の `before-rename` と `after-rename` に失敗を注入する（4 ケース）、および intent file の `before-rename` に失敗を注入する。hook は対象の catalog 書込みに一度だけ発火させ、promote の catch と finally が終わった後に別の読取りで状態を確かめる。各ケースの後、disk から新しく読み直した catalog が `Read-EvidenceTask` で読め、書込み A の `before-rename` なら `staged`・putIntent 無し・PUT 0 件、`after-rename` なら `put-pending`・PUT 0 件で既存の回復へ進める、書込み B の `before-rename` なら `put-pending`・receipt file ありで回復の GET 一致から `ready`、`after-rename` なら `ready` のまま（後始末で `put-pending` に巻き戻らない）で、promote の結果は `passed`、続く再 promote は PUT 0 件で `already-promoted`。intent の `before-rename` なら intent file が無く `staged`・PUT 0 件で、再 promote が成功する。
  - **AC11**（D7, D8）: README・`review-evidence.md` に promote の操作、出力、`already-promoted`、PUT 応答不明の扱い、既知の制限（`failed` で止まった昇格）、事前承認が書かれ、「昇格は後続」「昇格経路が後続スライスで成立するまで」の旧文言が `tools/Artifacts/README.md` と `.agents/skills/osm-workflow/` に残らない。`docs-audit`・`contract-audit` が errors=0。
- ここでは答えない問いと所有する後続スライス:
  - **後続スライス `Artifact Storage / Evidence 昇格の回復と送信済み staged の寿命`（新規。本スライスのマージ後に A1 から。入力は revision 2 の該当部分と §5.1・§5.2）:** 問いは「昇格由来の `put-pending` / `failed` が、回復 GET の 404 の後に `staged` へ戻って同じ bytes で再送・local fetch でき、送信を試みた `staged` が remote DELETE と GET 404 を経てだけ `deleted` になるか」。入力:
    - revision 2 の D3（同じ bytes の再送の許可と、publish 側の再送禁止との区別）、D4 (i)(ii)（`origin: stage` の `put-pending` と `failed` の 404 → `staged`、停止済み staging の operation 解除）、D5（保持 policy、`Get-EvidenceDeletionRoute` を Cleanup に、origin の確認、照合 GET 一致 → `staged`、削除後の遅延 PUT の受容）、D6（fetch の取得元を preflight 後に決め、reasonCode も最終の取得元で出す）、D8（catalog の組み合わせ検査）、D11（runtime 切替）、AC5・AC7・AC8・AC9・AC11・AC14・AC15 と、既存ケース `staged-unsent-required` の 1 行目の変更。revision 2 の A3 裁定 (1)(2)(5) は再確認の対象。
    - 制約: 回復（404 → `staged`）と保持 policy・削除経路・清掃の振り分けを同じスライス・同じ commit に置く（片方だけでは送信を試みた `staged` がローカル削除に乗るか削除不能になる）。D8 で `origin: stage` の `failed`（putIntent あり・receipt 無し）を `corrupt-state` にしない（本スライスの runtime が正当に作る shape）。回復 GET 一致時に「中の file が全て `ownedCopies` に入った staging だけを adopted にする」規則を既存の `Resolve-EvidencePutGuarded` へそのまま入れると、publish 由来の staging（`readback-process.json` 等は `ownedCopies` 外）が adopted から外れて 7 日清掃で消え、task が `changed-copy` で止まる。origin で分けるか、「今回の回復に使った staging だけ」に言い換える。
    - 不採用で閉じたもの: remote 削除前の process 停止確認（A2 #20。`staged` に戻る前の回復が既に停止を確認している）。
    - 優先度の低い入力: 前回の試行が receipt を書いた後に object が外部で消えた場合の receipt 再利用（A2 B-10。再 promote は失敗するが、次の回復が既存 receipt を再利用して自己回復する）。
    - 既存の回復が既存の receipt を再利用するときの照合が key・packageSha256・manifestSha256 の 3 つに限られる点（§5.3 R2-04）、回復一致時に promote の readback staging が未登録 file ごと adopted になる点（§5.3 R2-01・R2-03）。
  - 送信部の共有（`EvidenceSend.psm1`、A2 (a) の依存方向の提案）→ 送信まわりを整理するときの入力（所有スライスは未定。promote の送信手順は独立した内部関数にしてある）。
  - Cloud・別 PC が R2 から fetch する経路、読取り専用 token の登録・受け渡し → program §4 C（`Artifact Storage / Evidence の Cloud 配布`、新規）。
  - `ready` artifact のローカル copy からの network 無し fetch → program §4 A の後続への入力。
  - `use` の network 無し preflight → program §4 C の入力のまま。
  - 既存 publish の 404 → `failed` で残る remote residue の回収 → Content の容量上限・孤児回収の入力。
  - 端末喪失に備えた未昇格 `staged` の定期 backup → Harness H3。
- 判定定義: GO = I1〜I7 を破る欠陥がなく、S1〜S4 が offline fixture と文書検査で成立し、S5 の実 R2 昇格 1 回が判定必須テストとして成立し、判定 C と C' がともに阻害 0。NO-GO = いずれかの不変条件の違反、または S の不成立。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし。
- 決定記録: なし。
- 本文へ転記した実装制約: `#nullable` と `record` の規則は Unity 側のみで本スライスには当たらない。PowerShell は `Set-StrictMode -Version Latest`。reasonCode は既存の小文字 kebab（新規は `already-promoted` の 1 つ）。path・key・資格情報を CLI 出力とログに出さない。テストで `Start-Sleep` / `Task.Delay` / `Thread.Sleep` を使わず時刻と transport を注入する。catalog に新しい state・新しい field を足さない。`EvidenceCleanup.psm1`・`EvidenceStateStore.psm1`・`EvidenceRetentionPolicy.psm1`・`EvidenceContract.psm1` と transport を変えない。資格情報の root・暗号文・DPAPI の利用を変えない。実ストアの catalog・transfers・資格情報を offline テストで読み書きしない。
- 未決事項: なし。

## 3. 責務マップ

| ファイル | 責務 | 変更理由 | 現在行数 → 予想 | 備考 |
|---|---|---|---|---|
| `tools/Artifacts/EvidenceApplication.psm1` | publish / stage / promote / fetch / inspect / use の orchestration | `Invoke-EvidencePromote`（D1）と、その送信手順の内部関数（D2） | 203 → 約 285（+40%） | 50% 未満で非分割。送信手順は内部関数として独立させ、後続で共有するときに抽出できる形にする。publish・stage・fetch・inspect・use・release の既存関数は変えない |
| `tools/artifacts.ps1` | CLI の引数解釈 | `evidence promote` の grammar と dispatch | 192 → 約 196 | |
| `tools/Artifacts/README.md` | 公開仕様 | D8 | 447 → 約 465 | |
| `.agents/skills/osm-workflow/references/review-evidence.md` | レビュー証拠の保存・配布 | D7 | 172 → 約 172 | 置換のみ |
| `tools/Artifacts/tests/ArtifactEvidence.Tests.ps1`、`EvidenceTestSupport.ps1` | offline 検証 | AC1〜AC8 のケース追加。偽 transport の PUT bytes 記録、応答不明・404・別 bytes の注入、promote 用 FaultHook | 追加ケースのみ | 既存ケースは変えない（AC9）。既存の偽 transport・時刻注入・FaultHook を使う |

状態の所有者は task の catalog（task guard が直列化）。寿命は既存の task 終了 + 30 日。新しい公開面は CLI の `evidence promote` と reasonCode `already-promoted`。新しいファイルは無い。asmdef・Unity 側の変更なし。`EvidenceSchedule` の runtime 収集は変えない。

## 4. 実装計画

- 順序: (1) `Invoke-EvidencePromote` の D1（分岐と identity 照合。偽 transport で PUT 0 件の組を先に固める）→ (2) D2 の送信手順と 2 回の catalog 書込み・FaultHook → (3) CLI → (4) README（D8）と `review-evidence.md`（D7）→ (5) `contract-audit`・`docs-audit`・AC11 の grep・AC9 の diff 確認。
- Phase B から Phase A へ差し戻す条件: `EvidenceCleanup.psm1`・`EvidenceStateStore.psm1`・`EvidenceRetentionPolicy.psm1`・`EvidenceContract.psm1`・transport・既存テストのどれかを変えないと D1〜D6 を満たせないとき。新しい state・field が必要になったとき。promote が作る shape を今の reader・回復・清掃が扱えないと判明したとき。
- 対象外を維持する方法: promote の経路に snapshot・package を作る関数の呼び出しを置かない（AC4）。AC9 の diff 確認で回復・保持・清掃・catalog 検査のコードに触れていないことを確かめる。Cloud・別 PC 向けの取得経路と token を作らない。

## 5. テストとレビュー計画

- 単体テスト: promote の分岐（D1）を偽 transport と隔離 store で全組（AC2・AC3）。intent file の照合（AC6）。送信の手順は偽 transport と promote 用 FaultHook（AC6）、既存の `WriteHook`（AC13）、PUT 時の disk 上の catalog の読み直し（AC12）で検証する。停止の後の判定は常に disk から読み直した catalog で行う。
- 差し戻し中の起点 `-Filter`: `tools/Artifacts/tests/ArtifactEvidence.Tests.ps1`（新規ケース）。C は凍結済み条件の確認に必要なら根拠を記録して変えてよい。
- 判定必須テスト（GO 候補 head）: `tools/Artifacts/tests/*.Tests.ps1` と `tools/Workflow/tests/*.Tests.ps1` の全体（`StoreContext.Tests.ps1` は非仮想化 host で）、3 project の Release build、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC11 の旧文言 grep、AC9 の diff 確認、AC10 の実 R2 昇格 1 回。`EvidenceCleanup.Tests.ps1` は `rg` が PATH に要る。
- 全 EditMode 回帰の適用除外: Unity 側のコードを変更しないため。代替証拠は上記 offline suite 全体と audit。
- 統合・Unity テスト: なし。
- 高コスト証拠: なし（実 R2 昇格 1 回は、既存 publish と同じ endpoint・資格情報・事前承認の範囲で、3 file・通常清掃で可逆なので、判定必須テストに含める。暫定 C' と最終 C' には分けない）。
- 操作・実行時・目視条件の検証経路: AC10。動作確認（Phase B の後）は `対象なし`（実 R2 は判定 C の実行担当が GO 候補 head で行い、B の head では行わない）。
- 未知の操作経路の疎通結果: なし。promote の PUT・readback GET・remote fetch は既存 publish・fetch と同じ transport・同じ key 形式・同じ資格情報を使う既知の経路。runtime の切替は不要（D8）。
- 人間の判断が必要な条件: なし。
- AC10 の手順（実 R2 昇格 1 回）:
  - 担当: 判定 C の実行担当（判断担当と分ける場合は新しい実行担当 session）。環境: 非仮想化 host の sandbox 外 PowerShell 7（Claude の Bash は LocalAppData が仮想化されるので使わない）。GO 候補 head で 3 project を Release build。
  - 手順: (1) 実行ごとに新しい task id（`evidence-r2-promotion-rt-<n>`）で `pwsh tools/workflow-task.ps1 start`。(2) 非秘密の固定 fixture（3 file、うち 1 つはバイナリ）を selection にして `evidence stage`。出力の reference と packageSha256 を記録。(3) 別 process で `fetch`（local 分岐）。(4) `evidence promote --reference <stage の reference> --sha256 <stage の packageSha256>`。`passed` で終わらなければ同じコマンドを 1 回だけ再実行し（既存の回復で `ready` になれば `already-promoted`）、それでも `passed` でなければ止めて生記録とともに判断担当へ返す。(5) `evidence inspect --task <task id>`。(6) 別 process で `fetch`（remote 分岐）。(7) 同じ promote を再実行。(8) 原記録（各 CLI の結果 JSON、catalog・intent・receipt の写し、fixture の hash 一覧）を判定 evidence に収録し、task を通常の最終手順で end する（R2 object は task 終了 + 30 日の通常清掃が消す。事前承認の範囲）。
  - 実装から独立した期待値: (4) が `passed`、reference 文字列が (2) と完全一致、packageSha256 が (2) と一致。(5) の対象 artifact が `ready`・`locality: promoted`。(6) が `passed` で、展開した全 file の SHA-256 が fixture の hash 一覧と一致。receipt の `putIntentSha256` が intent file の SHA-256 と一致し、receipt の readback の hash が packageSha256 と一致。(7) が `passed`・`already-promoted`。catalog の artifactId・key・base・head・manifestSha256・packageBytes・entries が (2) 直後と (6) 後で一致し、(6) 後の `ownedCopies` が (2) 直後の `ownedCopies` を全て含む。
- 判定 C の照合 script の方式: **判定 C の offline 実行**。判定 C の担当が、AC10 の原記録の複製に対して、資格情報・本番設定・network に届かない環境で照合 script を書いて実行する。適用前に合成 fixture で一度通す。凍結する合成 fixture と期待値: 合格 1 組（上の期待値を全て満たす）と不合格 4 組（reference 文字列が 1 文字違う、(6) の file hash が 1 件違う、(7) が `already-promoted` でない、昇格後の `ownedCopies` が昇格前の 1 件を欠く）。script は合格組を合格、不合格 4 組をそれぞれ不合格と判定しなければならない。
- 機械検査: `contract-audit`、`docs-audit`、`git diff --stat`、AC11 の grep、AC9 の diff 確認。
- 外部レビュー: revision 3 に対して Web 版 GPT（owner が依頼）。結果と対応は §5.3。独立 A2 には数えない。
- A0/A1 主担当・モデル・ベンダー: Claude（この session）。
- A2 独立レビュー: revision 1 に対して実施済み（§5.1）。revision 3 は revision 2 の部分集合と分割の相談（§5.2）から作った。
- A3 統合担当・モデル・採否: 主担当（Claude）と owner。revision 2 の裁定と分割の経緯は §5.1・§5.2。
- C' 用に予約した担当・モデル・ベンダー: Grok 系（cursor-agent）。A2 と分割の相談では使っていない。
- 独立性の強化条件を満たせない場合の理由: A2 の (b)(c) と分割の相談（Fable 系）は A1 主担当と同じ Claude 系。

## 5.1 A2 独立レビュー結果と A3 の採否案（revision 1・2 の記録）

本節は A1 初稿（revision 1）への A2 と、revision 2 への A3 裁定の記録で、現在有効な契約は §2〜§5（revision 3）である。採用分の多くは §5.2 の分割で後続スライスの入力へ移った。

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

## 5.2 revision 2 から revision 3 への分割

**経緯（2026-10-17）。** owner は revision 2（commit `8494c791`）を確認し、「R2 昇格のターゲットとしては妥当だが実装内容が膨らんでいる。大きいと Phase C / C' で指摘と修正が膨らむ」として凍結せず、適切な実装単位への分割を指示した。主担当は先に、A2 の採用分のうち 4 点を後続へ移送し、1 点（#20）を重複として不採用、実 R2 往復を高コスト証拠から判定必須テストへ下げる案を出した。owner の指示で、その案と revision 2 を Fable 系（Claude Fable 5.1、新規 read-only session、入力は revision 2 の commit `8494c791` とワークフロー文書・関連コード）に分割の相談として渡した。原記録は A1 主担当 session の記録にある。これは独立レビューに数えない（主担当の案を読んだ相談）。

**Fable の提案（要点）と主担当の確認。**

- 境界は「送信」と「回復・寿命」の 2 スライス。根拠: 送信を試みた可能性があり、かつローカル削除の対象と区別が要る `staged` + putIntent という組み合わせを作るのは、revision 2 の D4 の「404 → `staged`」だけで、promote 自体は書込み A で putIntent と `put-pending` を同時に書くので作らない。promote を既存の回復（一致 → `ready`、404 → `failed` で凍結）にそのまま接続すれば、送信スライスは単独で fail-closed に閉じる。主担当は、`Resolve-EvidencePutGuarded` が origin を見ずに 404 で `failed` にすること（[EvidenceCleanup.psm1:352](../../tools/Artifacts/EvidenceCleanup.psm1)）、今の reader が origin と state の組み合わせを制限しないこと、inspect が既に `locality` を返すこと（[EvidenceApplication.psm1:177](../../tools/Artifacts/EvidenceApplication.psm1)）を確認した。
- 送信スライスは runtime の切替が不要。promote が作る shape を今の reader・日次 job が全て読み、同じ回復を行う。revision 2 の D11 と A3 裁定 (5) は後続スライスだけの話になる。
- 主担当案の「旧 runtime の `failed` の回復（D4 (ii)）を移送し、D8 でこの `failed` を `corrupt-state` にする」は危険。送信スライスの runtime 自身が回復の 404 で `origin: stage` の `failed`（putIntent あり）を正当に作るので、後続でそれを `corrupt-state` にすると task 全体が止まる。→ 採用。後続スライスへの制約として §2 に書いた。
- 実 R2 往復は判定必須テストへ下げてよい（同じ endpoint・鍵・事前承認、3 file、通常清掃で可逆、runtime 切替なし）。条件: 実行ごとに task id を変える、再実行は 1 回まで、独立期待値と照合 script の方式は残す。→ 採用（§5）。
- 送信部の共有（`EvidenceSend.psm1`）の移送に賛成。promote の送信手順は独立した内部関数にしておく。→ 採用（D2）。
- receipt の再利用（B-10）の移送に賛成（既存の回復が receipt を再利用して自己回復する）。#20 の不採用に賛成（重複）。→ 採用。
- D6（fetch の取得元の決め直し）は、404 → `staged` を作る後続スライスに入れるのがよい。送信スライスでは昇格中の fetch は回復で `ready` または `failed` に解決され、既存挙動で閉じる。→ 採用。
- revision 2 の D2(3) の記述「書込み 1 で止まれば未採用 staging が 7 日清掃に乗る」は誤り（artifact が `staged` の間、その artifactId の staging は清掃が一律に除外する。[EvidenceCleanup.psm1:293](../../tools/Artifacts/EvidenceCleanup.psm1)）。→ revision 3 は readback staging の登録を書込み A に含め、中間状態を無くした。
- 後続スライスの罠: 「中の file が全て `ownedCopies` に入った staging だけを adopted にする」規則を既存の回復へそのまま入れると、publish 由来の staging が adopted から外れて 7 日清掃で消え、task が `changed-copy` で止まる。→ 後続スライスへの制約として §2 に書いた。
- 避けるべき切り方: 回復（404 → `staged`）と保持・削除経路・清掃の振り分けを別スライスにする、保持を変えずに 404 → `staged` だけ入れる、D8 を先に厳しくする、送信部の共有と promote を同じスライスに入れる。→ revision 3 と後続スライスの境界はこれに従う。

**revision 2 から後続スライスへ移したもの:** D3 の再送許可、D4 (i)(ii)、D5、D6、D8、D11、AC5・AC7・AC8・AC9・AC11・AC14・AC15 の revision 2 版、既存ケース `staged-unsent-required` の変更、A3 裁定 (1)(2)(5)。本スライスに残したもの: D1（終了済み task の `already-promoted` を含む）、D2（書込みは 2 回に整理）、D7（inspect、コード変更なし）、D9・D10 の一部（本スライスの D7・D8）、A2 の #6・#7・#12・#16 と #2 の前半（`ownedCopies` 追記のみ、adopted は自分の staging だけ）。

## 5.3 revision 3 への外部レビューと revision 4 への反映

2026-10-17、owner が revision 3（commit `49bf9b11`）を Web 版 GPT に依頼し、結果（「条件付き GO。Phase B 着手前に High の解消が必要」、R2-01〜R2-06）を主担当へ渡した。レビュー担当は §5.1・§5.2 を含む revision 3 全体を読んでいるので、独立 A2 には数えない。原記録は owner の手元（git 管理外）。主担当は全件を実コードで照合し、全て根拠ありと判断した。いずれも既存モジュール（cleanup・回復・保持・catalog 検査・contract・transport・publish）の変更を要さず、手順の明記とテストで閉じるので、Phase A への差し戻し（範囲の変更）にはしない。

| # | 指摘（重さ） | 分類 | 主担当が確認した事実 | revision 4 の反映 |
|---|---|---|---|---|
| R2-01 | 書込み A の staging・operation と既存回復の整合（High） | 根拠あり。手順の明記とテストで閉じる | `Assert-EvidenceOperationStopped` は operation のディレクトリの実在、process の停止、`readback-process.json` の子 marker で判定する。回復の一致は同じ artifactId の staging を全て adopted・operation 解除にし、404 は回復用 staging の operation だけを外す（promote の staging の operation は、artifact が `failed` になった後に清掃が停止を確かめて外す） | D2 の書込み A を「ディレクトリ作成 → 1 回の catalog 書込み」と operation の値まで明記。書込み A 後の例外時の後始末を publish と同じ規則に。AC6 に回復 3 終点の観測と、その後の stage・fetch・use・cleanup の確認 |
| R2-02 | intent 再利用の照合が狭い（High） | 根拠あり | publish の intent は `schemaVersion`・`artifactId`・`key`・`packageBytes`・`packageSha256`・`createdAt`・`process` を持つ | D2 手順 2 を、正規 path・field 集合・`schemaVersion`・4 field の一致・catalog に putIntent の無い `staged` だけ、に広げた。AC6 に field ごとの拒否ケース、I5 に追記 |
| R2-03 | 回復成功時の `ownedCopies`・adopted と清掃（Medium） | 根拠あり。テストで確認 | 回復の一致は `ownedCopies` に追記する（昇格前の集合は残る） | AC6 に回復後の cleanup dry-run と実行の確認。residue の内容・理由・後続での扱いを D4 と §2 の後続入力に記録 |
| R2-04 | receipt 書込み後・書込み B 失敗の収束（Medium） | 根拠あり。テストと既知リスクの記録 | 回復の receipt 再利用は key・packageSha256・manifestSha256 の 3 つで照合する | AC6・AC13 に「receipt を書いただけでは `ready` にならない」「書込み B の失敗から回復の一致で `ready`」。照合の狭さは既知リスクとして D4 と後続入力に記録 |
| R2-05 | PUT 前の永続化順序を観測可能に（Medium） | 根拠あり | 既存の偽 transport は呼び出しの中で任意の検査ができる | AC12（PUT の中で disk 上の catalog を読み直す、readback 前に receipt・`ready` が無い） |
| R2-06 | 書込み A・B 自体の原子的失敗（Medium） | 根拠あり | `Write-EvidenceAtomic` に `WriteHook`（`before-write`・`before-rename`・`after-rename`）があり、既存テスト `receipt-atomic-failure` が使っている | AC13（書込み A・B の rename 前後と intent の rename 前、5 ケース）。停止後の判定は disk から読み直した catalog で行うと §5 に明記 |

既存の A2（§5.1）との重複・相違: R2-01 は A2 B-9（書込み回数と operation 解除）と、R2-03 は B-2（`ownedCopies` 置換と一律 adopted）と、R2-04 は B-10（receipt 再利用）と同じ箇所を見ている。A2 はそれらを方式の変更（後続スライスへ移した回復）で答えたのに対し、本レビューは revision 3 の範囲のまま、既存回復への接続をテストで証明することを求めた。R2-02・R2-05・R2-06 は A2 に無い新しい指摘で、全てテストと手順の明記で閉じた。

## 5.4 revision 4 への追加レビューと revision 5 への反映

2026-10-17、owner が revision 4（commit `f9b2eba7`）を Codex（GPT 系）に依頼し、結果（「方針は妥当、現状のままの凍結は勧めない」、R4-01〜R4-03）を主担当へ渡した。レビュー担当は §5.1〜§5.3 を読んでいるので、独立 A2 にも C' にも数えない。原文は owner の手元（git 管理外）。主担当は全件を実コードで照合し、全て根拠ありと判断した。いずれも既存モジュールの変更やスライスの拡大を要さない。

| # | 指摘（重さ） | 分類 | 主担当が確認した事実 | revision 5 の反映 |
|---|---|---|---|---|
| R4-01 | 書込み B の確定後の例外でも `put-pending` に揃える記述が AC13 と矛盾（P1） | 根拠あり | `Write-EvidenceAtomic` は Move の後に `after-rename` を呼ぶ。`Write-EvidenceTask` は永続化の前にメモリの generation を増やす。`Clear-EvidencePackageContext` はメモリの object を書き戻す | D2 の後始末を「guard 下で disk を読み直し、永続化済みの state を正とする。メモリの未確定の変更は書き戻さない」に置き換えた。AC13 に hook の一度だけの発火、catch・finally 後の別読取り、B 確定後の `already-promoted` を追加 |
| R4-02 | 前段で拒否される入力まで `identity-mismatch` を期待している（P2） | 根拠あり。既存の reasonCode をそのまま返す方を選んだ | preflight の `Assert-EvidenceOwnedCopy` は ZIP の hash 不一致を `changed-copy` にする。`Assert-EvidenceReference` は導出と違う key を拒否する | D1 に「前段の既存 reasonCode を変換しない」を明記。AC2 を (h)(i) に分け、AC4 を事前改変（`changed-copy`）と lock 前の改変（FaultHook `promote-before-lock`、`identity-mismatch`）に分けた。どちらも CLI の入口から、preflight を stub しない |
| R4-03 | AC8 は active と ended の境界が抜けていて実行順序が成立しない（P2） | 根拠あり | `Apply-EvidenceEvent` は ended で初めて `deleteEligibleAt` を設定する。保持 policy は active を保護の評価より前に `active/end-not-recorded` にする | AC8 を、時刻 `T` 固定・active で stage → use → promote → ended event の適用 → 期限直後の `in-use` → release 後の remote 削除、の順に書き直した |

既存の指摘との関係: R4-01 は R2-06（書込み A・B の原子的失敗）を受けて revision 4 で足した後始末の記述そのものの矛盾で、revision 4 の反映漏れにあたる。R4-02・R4-03 は AC の実行可能性の指摘で、A2・R2 には無かった。レビュー担当は、404 → `failed`、回復一致時の residue、送信部の非共有、AC10 の分類を指摘にしないと明記している。

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
