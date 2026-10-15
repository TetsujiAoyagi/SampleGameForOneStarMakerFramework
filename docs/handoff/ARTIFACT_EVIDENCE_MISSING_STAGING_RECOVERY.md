# Artifact Storage / Evidence 欠落stagingの回復 HANDOFF

## 0. メタデータ

- type: `slice`
- status: `C`（Phase B 完了。次は発見 C。凍結内容は §2・§3・§5。設計判断を変えるときは Phase A を新しい revision として再開する）
- branch: `claude/evidence-missing-staging-recovery`（PR の base は `develop`）
- implementation base commit: `92a0573`（`origin/develop`、PR #125 マージ後。初稿の base は PR #126 マージの `a251201`、差分は workflow 文書のみで本スライスに影響しない）
- implementation head commit: 未固定
- risk: `high`（失敗時に処理を拒否する契約 fail-closed と、catalog の保持・削除の意味に触れる。[新しい事実の分類](../../.agents/skills/osm-workflow/references/phases-and-handoff.md#新しい事実の分類) の「A 再開」級の判断を含む）
- owner: OSM maintainers
- created: 2026-10-15
- expires: 2026-11-15 または置換 revision
- harvest to: 挙動と reasonCode を `tools/Artifacts/README.md`（自動清掃・通常操作の節）と `tools/Workflow/README.md`（end の出力）へ反映し、`BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §4 E の該当項目を消す。
- Phase A snapshot の取得案内・状態: 未保存（tracked HANDOFF の commit `37fb4d3` が凍結対象版。Artifact Storage への保存は行っていない）
- Phase B result snapshot / evidence bundle / C' blind bundle: 未生成（未到達 Phase）
- B 出口の引継ぎ票: 次の Phase は **発見 C**（検証経路が `対象なし`）。入力は implementation base `92a0573` と、B の実装 commit（push 後の head を PR 状態ブロックに固定）。結果は §6。判定 C・C'・照合 script は B が担っていない。実ストアは未接触。
- （旧）引継ぎ票: 次の Phase は **B（実装）**、役割は実装担当、作業は §4 の順序で §2 の受け入れ条件 AC1〜AC10 を満たす実装とテスト追加。入力は凍結した Phase A revision 2（凍結対象版の内容 sha256 `d46cebb972b668dc1c362d9977b7c01b2127e388eea986a4413b525ef77faa58`、LF 正規化した git blob 内容、commit `37fb4d3`）。凍結の記録はこの HANDOFF の次の commit。必要な権限: ローカルの PowerShell と git のみ（Unity、sandbox 外実行、資格情報、network は不要）。**実ストアの catalog・transfers・資格情報は読まず、編集もしない。** B の出口で `pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1` を実行し、§5 の起点 filter を通し、実装結果（§6）と PR の状態ブロックを更新して push する。判定 C の実行、C' の起動、判定 C の照合 script は担わない。停止規則: §4 の「Phase A へ差し戻す条件」に該当したら実装を止めて判断を返す。引き渡さないもの: なし（C' 用の blind bundle は未生成）。PR を作る前にブランチ名をスライスを表す名前へ改める。

## 1. 目的と対象外

- 目的: Evidence task の catalog に、未採用・operation なしで実体 directory だけが無い staging entry が残っても、publish・task 終了（end）・cleanup が `unconfirmed` / `workflow-unavailable` で止まらないようにする。欠落は黙殺せず、通常操作の出力に明示する。実在する staging のパス・ACL・reparse の異常は今まで通り拒否する。
- 現況（2026-10-14/15 の PR #126 Phase D 実測。実ストアは今回読んでいない）:
  - task `artifacts-virtualized-store` の catalog に ready 3 件と staging 9 件。最後の 1 件が `origin=storage-copy, adopted=false, operation=null` で、`transfers\<id>` が無い。
  - `evidence publish` は `unconfirmed`。`FailureHook` で `Resolve-ArtifactStoredPath`（[ArtifactPaths.psm1:46](../../tools/Artifacts/ArtifactPaths.psm1)、`-AllowMissing` 無しで存在しないと `Artifact path unavailable.`）が、`Assert-EvidenceTaskPaths`（[EvidenceCleanup.psm1:97](../../tools/Artifacts/EvidenceCleanup.psm1)）から `Assert-EvidenceTransitionSafe` 経由で呼ばれていると特定済み。
  - `evidence cleanup --dry-run` はその task を `unconfirmed` にする。`workflow-task end` は `Assert-EvidenceTransitionSafe`（[workflow-task.ps1:51](../../tools/workflow-task.ps1)）で同じ例外になり `workflow-unavailable`。task は `active/end-not-recorded` のまま。
- コード調査で分かった構造（実装前の事実）:
  1. 欠落を許すのは `$stage.adopted -and -not $stage.operation` の場合だけ（97 行）。未採用は存在を強制する。
  2. `Assert-EvidenceTaskPaths` が task ごとの先頭（cleanup 210 行、`Assert-EvidenceTransitionSafe` 170 行）で走るため、cleanup 本体の「directory が無ければ tree 削除を飛ばして entry だけ落とす」分岐（233-236 行）は、欠落 staging では到達しない。仮に preflight を外しても 231 行の `Resolve-ArtifactStoredPath`（`-AllowMissing` 無し）が先に投げ、`local-pending` で永久に残る。
  3. publish の source 候補ループ（[EvidenceApplication.psm1:42](../../tools/Artifacts/EvidenceApplication.psm1)）も同じ解決を未採用 staging に対して `-AllowMissing` 無しで行う。ここも直さないと publish は別経路で止まる。
  4. `Resolve-ArtifactStoredPath -AllowMissing` は、絶対・正規形・root 配下・各 component の形式・root の ACL・解決先 chain の reparse・存在する親の ACL を、存在しない場合でも検査する。root 自体が無い／ACL 不適合なら投げる。欠落の許容に必要な fail-closed の大半は既存部品で足りる。
  5. staging の保持規則（7 日、`Get-EvidenceStagingDecision`）は「採用済み・採用 pending・operation・deleteIntent・protection があれば除外」という述語を持つ。
- 欠落の原因は未確定（手動削除、AppData 仮想化で別側にある、過去 fetch の失敗残骸の後始末、など）。本スライスは原因を特定せず、原因によらず安全な方式にする。
- 対象外: §2 の「ここでは答えない問い」に列挙。実ストアの手修正、`end` の反復実行、既存 Evidence・資格情報・R2 の変更はしない。

## 2. 意思決定と受け入れ境界（revision 2。A3 で凍結済み）

- このスライスが答える問い: 「未採用・operation なし・実体なしの staging を、自分の残骸と証明できる範囲だけ、報告付きで通常操作から回復可能にしつつ、実パス・ACL・reparse の異常と、仮想化による取り違えを従来どおり拒否できるか」。
- 方式:
  - **D1 述語を2層にする（純粋、`EvidenceRetentionPolicy.psm1`）。**
    - `Test-EvidenceStagingUnheld($Stage)`: `origin=storage-copy`、未採用、`adoptionPending` 偽、`operation` null、`deleteIntent` null、protection 0。`Get-EvidenceStagingDecision` の除外判定はこの否定を使い、シグネチャ・reasonCode・既存テストを変えない。
    - `Test-EvidenceStagingDormant($Task,$Stage)`: Unheld であり、かつ (i) `artifactId` が null、または catalog に在る artifact を参照し、その state が `put-pending` でなく、`deleteIntent` も `operation` も持たない、(ii) どの artifact の ownedCopies の path も、この stage の path 配下にない（比較は区切り `\` で終端した前方一致、OrdinalIgnoreCase）。catalog に無い artifactId を参照する stage、property が欠ける stage は dormant にしない（例外を投げず偽を返す）。
  - **D2 経路状態を 1 関数で判定する（`EvidenceCleanup.psm1`、export 必須。publish の候補ループも使う）。** `Get-EvidenceStagingPathState($Task,$Stage)` は `present` / `missing` を返すか、例外で拒否する。
    - dormant でなければ、従来どおり `Resolve-ArtifactStoredPath` を `-AllowMissing` 無しで呼ぶ。
    - dormant なら `Resolve-ArtifactStoredPath -AllowMissing` で解決し、**解決済み path** に対して `GetDirectoryName(resolved) -ceq Get-ArtifactRoot` かつ leaf が `^[0-9a-f]{32}$` であることを確認する。
    - 「存在しない」と判定する前に、leaf 自体の属性（`FileInfo.Attributes`）を見る。エントリが在るのに `Exists` が偽になるもの（リンク先の無い junction / symlink）は欠落ではなく拒否する。
    - root 不在・root/親の ACL 不適合・reparse・形式不正は例外のまま（`-AllowMissing` の既存検査）。
  - **D3 欠落は報告する。** observation は `[Collections.Generic.List[object]]$Observations` の収集器で受け渡す。
    - 収集器は `Observations`（`{key; reasonCode='staging-missing'}` の List）と `Witnesses`（整数）を持つ可変オブジェクト 1 つとし、呼び出し側が **try の外で**生成する（失敗した publish の結果にも件数を出すため）。`Assert-EvidenceTaskPaths` は欠落 dormant を拒否せず収集器へ積み、**戻り値を持たない**（呼び出しは `$null=` で受ける。数値をパイプラインへ流すと、`workflow-task` の `@(Assert-EvidenceTransitionSafe ...)` が deleted artifact id として誤読する）。`Assert-EvidenceTransitionSafe` は任意の `-Residue` を受け、戻り値（deleted artifact id の配列）は変えない。
    - `key` は cleanup の cursor と同じ SHA-256 で、`Get-EvidenceStagingKey` に抽出して両方で使う。path は出力しない。
    - 出力: publish・fetch・use の結果と、`workflow-task` の end・resume の JSON に、トップレベルの整数 `stagingMissing` を **0 のときも常に**出す（失敗した publish にも出す。config が複数なら合算）。status・終了コード・`reasonCode` の既存意味は変えない（欠落だけでは 0、`recorded/delivery-pending` は従来どおり 2）。`workflow-task status` は preflight を通らないため対象外。
    - cleanup の item は `reasonCode='staging-missing'`。
  - **D4 満了後・証人あり・guard 内だけ entry を落とす。** cleanup（非 dry-run）は、欠落 dormant staging が既存の 7 日（`staging-expired`、境界は `-ge 604800秒`）に達し、かつ**証人数が 1 以上**のときだけ、task guard 内で欠落を再確認して catalog から entry を外す。
    - この分岐は**ファイル・directory の削除 API を呼ばない**。item は `reasonCode='staging-missing-dropped'`、件数は既存の `skipped`（`deleted` に数えない）。`EvidenceSchedule` に新しい counter は足さない。
    - **証人**は、この preflight で実在・ACL・identity を確認できた、同じ task の登録済み path である。数えるのは (a) artifact の ownedCopies のファイル、(b) `present` と判定した別の staging directory。`missing` の staging と adopted で欠落許容された staging は数えない。証人数 0 なら、満了後でも entry を残し `staging-missing` のまま報告する。目的は、root が実体の無い側を指すときに記録を失わないことである。限界（実装者が誤読しないよう明記）: ownedCopies を持つ ready artifact があれば、既存の preflight が先に `changed-copy` で拒否するため証人は自動的に 1 以上になり、条件は実質無条件になる。証人が示すのは「root がこの task のファイルのある側を指す」ことだけで、個別の欠落 directory が反対側にあることは否定しない（側が混在する状態は本スライスの外で、`Split Store Recovery` が所有）。証人 0 の task（deleted のみ、または staging だけ）では欠落 entry が残り続けるが、安全側として受け入れる。
    - cleanup の staging 項目は、欠落判定を `Get-EvidenceStagingDecision` より前に置く。非 dry-run・dry-run を問わず、欠落かつ満了前、または満了済みで証人 0 は `reasonCode='staging-missing'` で `skipped` に数える。満了済み・証人あり・非 dry-run は `staging-missing-dropped` を `skipped` に数える。dry-run は満了済みでも `staging-missing`。
    - dry-run、`Assert-EvidenceTransitionSafe`、Sync は、欠落 entry について catalog を書かない。再確認時に directory が現れていたら、従来の削除経路へ戻す。
    - 前提: 3 つの CLI は仮想化された process を store に触れる前に拒否する（PR #126）。D4 はこれに頼らず、証人条件を独立に置く。
- 却下した代替案:
  - catalog の手修正・`end` の再試行: 禁止（依頼）。
  - 検出即時の entry 除去: 仮想化で directory が別側にある場合、記録だけを失う。
  - `end` だけ preflight を飛ばす: 「Storage の曖昧さを解決してから Workflow が確定する」を破り、publish・cleanup の停止も残る。
  - 解決不能な dormant staging を全部許す: ACL・reparse 異常まで通る。
  - `adopted` の書き換え、`evidence repair` の新設、`evidence inspect` への項目追加（後続へ）。
  - 出力に path 配列を載せる案: README の「path を表示しない」規則に反する。
  - 仮想化 host では drop を禁止する案: 仮想化 host の cleanup で欠落 entry が永久に残る。証人条件で足りるため不採用。
- 進める最低条件:
  - 不変条件（起きてはいけないこと）:
    - **I1** transfers root が不在・ACL 不適合・reparse、staging path の形式が不正（root 外、root 直下の 32 hex でない、`..` 等）、または leaf が dangling な junction / symlink のとき、従来どおり拒否する（欠落として扱わない）。
    - **I2** dormant でない staging（採用済み・operation あり・`adoptionPending`・deleteIntent・protection・ownedCopies が配下にある・put-pending/deleteIntent artifact を参照・catalog に無い artifactId を参照）の解決失敗は従来どおり拒否する。
    - **I3** 欠落 entry の処理は catalog entry の除去だけで、ファイル・directory を一つも削除しない。他 entry、他 task、artifact の ownedCopies、transfers 内の未登録 directory は変化しない。
    - **I4** 7 日の満了前、または証人数 0 のとき、欠落 entry を catalog から捨てない。
    - **I5** 欠落を許容した通常操作（publish・fetch・use・end・resume・cleanup）は、その件数を出力に含める。黙って成功にしない。
    - **I6** artifact の ownedCopies 欠落（`changed-copy`）、operation 付き staging の欠落（`operation-pending`）、putIntent 不一致（`unresolved-put`）は本スライスで緩めない。
    - **I7** 読取経路（`Assert-EvidenceTransitionSafe`、Sync）と dry-run は、欠落 entry の処理で catalog を書き換えない（既存の `Resolve-*Guarded` による artifact 側の書込みは従来どおり）。
  - 成立条件:
    - **S1** 欠落 dormant staging を持つ task で、publish が `passed`、`workflow-task end` が成功し、cleanup（dry-run を含む）が `unconfirmed` ではなく `staging-missing` を返す。
    - **S2** 満了済み・証人あり・欠落 dormant staging は、cleanup が entry を外し（`staging-missing-dropped`）、artifact・他 staging・ファイル群は不変。
    - **S3** S1 の各操作の出力に `stagingMissing` の件数が現れる。
- 受け入れ条件（検証例。いずれも隔離した temp store の offline fixture、実ストア・network・Unity 不使用。拒否の検査は、拒否の原因が staging 経路であると分かるよう、artifact 0 件で欠落 dormant staging だけを持つ task でも行う）:
  - **AC1（S1, S3）** 既存の publish 済み fixture（ready artifact あり）に、未採用・operation なし・artifactId null の staging を `transfers\<新しい 32hex>` で登録し directory は作らない。publish が `passed` で `stagingMissing=1`、fetch・use も成功して `stagingMissing=1`、`-Observations` が 1 件。欠落が無い task は `stagingMissing=0`。不合格: `unconfirmed`、件数 0 または欠落。publish が途中で失敗した結果にも件数がある。
  - **AC2（S1）** 同 fixture で `Invoke-EvidenceCleanup -DryRun` が当該 item を `staging-missing` にし、`unconfirmed` を出さない。ready artifact の item は従来の reasonCode。
  - **AC3（S1）** `workflow-task` の end・resume 相当（`WorkflowCliFixture.ps1` の stub が観測を返す）で `stagingMissing` が出力され、transition が commit され、終了コードが 0。config が 2 つなら合算。
  - **AC4（S2, I3, I4, I7）** 満了の 1 秒前は entry を残し、満了ちょうど（`-ge`）で外れる。外れた後、`deleted=0`、`skipped` に数え、sentinel file を置いた別の `transfers\<32hex>` directory、他 task の catalog、artifact の catalog 部分が前後で同一（hash 一致）。dry-run は満了後でも catalog を変えない（hash 一致）。
  - **AC5（I1）** （leaf の属性検査は、`Assert-NoArtifactReparse` が既に dangling directory junction を捕捉することを実測済みの A2 所見のうえで、dangling な file symlink・directory symlink を防御的に塞ぐ二重化として残す。fixture で各種の挙動を実測して記録する）transfers root の不在、root の ACL 不適合、leaf が非 hex、root 外 path、root 直下でない path（`transfers\a\<32hex>`）、leaf が dangling junction、path 上の junction、の各 fixture で、欠落 entry の task の publish/cleanup が従来どおり拒否（`unconfirmed`）。
  - **AC6（I2, I6）** 欠落かつ operation あり、`adoptionPending`、adopted かつ operation あり、protection、deleteIntent、put-pending artifact の参照、catalog に無い artifactId の参照、ownedCopies が配下にある、の各 staging は従来どおり拒否。既存の `missing-owned-copy` の結果と、putIntent 不一致の `unresolved-put` は変わらない。
  - **AC7（I3 競合）** 欠落判定の後、drop の直前に directory が現れる fixture（既存の `FaultHook` 方式）で entry を外さず、従来の削除経路が走る。限界: guard が直列化するのは catalog の書き手だけで、drop の後に外部で現れた directory は未登録として残る（ファイルは消さないので I3 は守られる。StoreConsolidation の `unreferenced-transfer` が拾う前提）。
  - **AC8（I4 証人）** 満了済みの欠落 dormant staging だけを持ち、実在を確認できる登録済み path が一つもない task で、cleanup は entry を残し `staging-missing` を返す。証人が 1 件でも在れば外す。
  - **AC9（D1）** `Test-EvidenceStagingUnheld` と `Get-EvidenceStagingDecision` の判定、`Test-EvidenceStagingDormant` の各条件を表駆動で確認する。property が欠ける stage は偽を返し例外を出さない。既存の `staging-7-days`・`staging-adopted` は変更なしで通る。
  - **AC10（I7）** `Assert-EvidenceTransitionSafe`・Sync・dry-run の前後で、欠落を含む catalog.json の hash が一致する。
- 判定定義: AC1〜AC10 が offline suite で通り、`contract-audit` と `docs-audit` が通れば GO。いずれかの不変条件を破る再現があれば NO-GO。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- ここでは答えない問いと所有スライス（program §4 E へ記録済み。所有スライスは各着手時の A0 で確定）:
  - Evidence task catalog の 1 MiB 上限（600 file の bundle で 919 KB、次の fetch が `record-limit`）と、上限を超えた fetch が不完全な state を残す経路: `Artifact Storage / Evidence task catalog size`（未着手）。スキーマ・record 形式の設計判断を含むため別 A0。
  - 欠落の**原因**の特定と、別側に残った実 directory の後始末、反対側の store に古い catalog の複製がある場合に StoreConsolidation が落とした entry を復活させうる点: `Artifact Storage / Split Store Recovery`、`Codex側残置の後始末`（program §4 E）。
  - artifact の ownedCopies 欠落、operation 付き staging の欠落の回復: 上と同じ。I6 により現挙動を保つ。
  - `invalid-evidence-path` 等の診断を `unconfirmed` から分ける、`evidence inspect` の staging 表示、cleanup の `MaxObjects` を欠落 entry が dry-run 毎回消費する点: program §4 E 末尾の項目と、その所有スライス。
  - 実 task `artifacts-virtualized-store` への適用: 本スライスのマージ後、owner の指示で通常コマンド（`evidence cleanup`、`workflow-task end` を 1 回）を実行する運用。計画と実装では実ストアに触れない。
- A3 後の例外承認: なし
- 決定記録: DR-1（2026-10-15、owner 裁定、凍結条件の意味・合否を変えない読み方の確定）: AC6 の「ownedCopies が配下にある staging は従来どおり拒否」は、述語 `Test-EvidenceStagingDormant` の条件を AC9 の表駆動で検証し、拒否の結果を統合テスト（`staging-missing-refuse` の `owned copy under stage`）で確認することで充足とする。理由: 統合では原因を分離できない。配下に実在ファイルがあれば directory は欠落せず（欠落の対象外）、ファイルが無ければ preflight の ownedCopies 検査が先に `changed-copy` で拒否するため、述語の配下条件だけが拒否の原因になる構成が作れない（§2 D4 の限界と同じ構造）。適用時点: 判定 C の差し戻し 1 巡目後（head 362d51a の判定）。
- 本文へ転記した実装制約:
  - Unity 側コードは変更しない。PowerShell は既存の module 境界（EvidenceRetentionPolicy は純粋、I/O は EvidenceCleanup）を保つ。依存 edge は既存の Application→Cleanup→RetentionPolicy だけ。
  - 一時的な debug 用 hook を残さない。テスト用の観測は既存の `FaultHook` / `Set-EvidenceTestValue` 方式を使う。
  - 例外本文・実 path を出力に出さない（README の既存規則）。observation の位置は SHA-256 キーで示す。
- 未決事項: なし（初稿の 1〜3 は §5.1 の回答を採用。A3 で人間が確定する）。

## 3. 責務マップ

| ファイル | 責務 | 変更理由 | 現在行数 → 予想 | 備考 |
|---|---|---|---|---|
| `tools/Artifacts/EvidenceRetentionPolicy.psm1` | 純粋な保持 policy（時刻と snapshot だけで判定） | stage 単体の Unheld と Task を見る Dormant の述語 | 37 → 約 55 | I/O を持たない。`Get-EvidenceStagingDecision` は Unheld を使い、シグネチャを変えない |
| `tools/Artifacts/EvidenceCleanup.psm1` | staging/artifact の経路検証と清掃の orchestration | `Get-EvidenceStagingPathState`、`Get-EvidenceStagingKey`、観測と証人数の収集、満了後の entry 除去 | 285 → 約 330 | 500 行未満。独立した責務は増えない（既に経路検証と清掃を持つ）。末尾に `Export-ModuleMember` 後の関数定義があるが既存で今回は動かさない |
| `tools/Artifacts/EvidenceApplication.psm1` | publish/fetch/use の orchestration | 候補ループの欠落許容と `invalid-source`、結果の `stagingMissing` | 132 → 約 145 | |
| `tools/workflow-task.ps1` | task 終了/再開の CLI | end・resume の出力に `stagingMissing`（config 合算） | 70 → 約 76 | |
| `tools/Artifacts/README.md`、`tools/Workflow/README.md` | 公開仕様 | harvest | 各数行 | |
| `tools/Artifacts/tests/EvidenceCleanup.Tests.ps1`、`EvidenceRetention.Tests.ps1`、`ArtifactEvidence.Tests.ps1`、`tools/Workflow/tests/WorkflowCliFixture.ps1` | offline 検証 | AC1〜AC10 | 追加ケース | `$registered` の長い一覧は既存形式のまま追記 |

状態の所有者は task の catalog（task guard が直列化）。寿命は既存の staging 保持（7 日）。新しい公開面は `Test-EvidenceStagingUnheld`、`Test-EvidenceStagingDormant`、`Get-EvidenceStagingPathState`、`Get-EvidenceStagingKey` と、`Assert-EvidenceTransitionSafe` の任意引数 1 つだけ。新しいファイル・依存 edge・永続状態・catalog スキーマ変更は作らない。行数警報（500 行、3 責務、50% 増）は発火しない。

## 4. 実装計画

- 順序: (1) 述語2層と `Get-EvidenceStagingDecision` の共通化（挙動不変を AC9 で固定）→ (2) `Get-EvidenceStagingKey`、`Get-EvidenceStagingPathState`、`Assert-EvidenceTaskPaths`、`Assert-EvidenceTransitionSafe` → (3) publish 候補ループと publish・fetch・use の `stagingMissing` → (4) cleanup の欠落分岐と証人条件 → (5) workflow-task 出力 → (6) README。
- Phase B から Phase A へ差し戻す条件: catalog スキーマ（`Read-EvidenceTask` の `Assert-Fields`）の変更が必要、`EvidenceSchedule` の変更が必要、`Get-EvidenceStagingDecision` のシグネチャ変更が必要、欠落判定に root 以外の新しい所有者情報が要る、`end` の終了意味を変える必要が出たとき。
- 対象外を維持する方法: 欠落分岐に削除 API を呼ぶコードを置かない（AC4 の sentinel と hash で確認）。1 MiB 上限の変更を含めない。

## 4.1 Phase B 開始手順（実装担当はこの順で進める）

1. `AGENTS.md` と `.agents/skills/osm-workflow/SKILL.md`（Phase B の節と、PR #125 の「動作確認」の節）を読む。この HANDOFF は全文を読み、§2・§3・§4・§5 を正とする。書かれていない設計文書を読み込んで再設計しない。
2. 照合する: ブランチが `claude/evidence-missing-staging-recovery`、`origin/develop` を含む（implementation base は `92a0573`）、HANDOFF §0 の status が `B`。食い違えば実装せず停止して人間へ返す。
3. 読むコード: `tools/Artifacts/EvidenceRetentionPolicy.psm1`、`EvidenceCleanup.psm1`、`EvidenceApplication.psm1`、`ArtifactPaths.psm1`、`ArtifactAcl.psm1`、`tools/workflow-task.ps1`、既存テスト `tools/Artifacts/tests/EvidenceCleanup.Tests.ps1`・`EvidenceRetention.Tests.ps1`・`ArtifactEvidence.Tests.ps1`、`tools/Workflow/tests/WorkflowCliFixture.ps1`。
4. §4 の順序で実装する。各段階の後に、該当する既存テストと新規ケースを `-Case` / filter で実行する。`EvidenceCleanup.Tests.ps1` は `rg` が PATH に必要（無ければ cursor 同梱の `rg` を PATH へ足す）。PowerShell のテストは、Unity Editor を使わず `pwsh -NoProfile -File <test>` で直接実行する。
5. 実ストア（`%LOCALAPPDATA%\OneStarMaker\Artifacts\...`）、資格情報、network には触れない。テストは隔離した temp store だけを使う。Claude の Bash は LocalAppData が仮想化されるため、store に関わる実行は pwsh で行う。実 task `artifacts-virtualized-store` の catalog・staging は読まず、編集もしない。
6. 実装中に §4 の「Phase A へ差し戻す条件」（catalog スキーマ変更、`EvidenceSchedule` の変更、`Get-EvidenceStagingDecision` のシグネチャ変更、root 以外の所有者情報、`end` の終了意味の変更）に該当したら止めて人間へ返す。新しい事実は [新しい事実の分類](../../.agents/skills/osm-workflow/references/phases-and-handoff.md#新しい事実の分類) で先に分類する。
7. 出口: `pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1` を実行し、§6 に実装結果（HANDOFF との差、未実行、implementation head）を書き、引継ぎ票を次の担当（検証経路が `対象なし` のため発見 C）へ更新する。README（`tools/Artifacts/README.md`、`tools/Workflow/README.md`）の harvest は §0 の harvest 先に従い、B で行ってよい。push して終える。判定 C、C'、照合 script は担わない。PR を作る場合は base を `develop` にし、本文先頭に状態ブロックを置く。
8. 実装コミットは試行錯誤を積まず、公開前に整理する（SKILL の「PR公開後の差し戻しとGit履歴」）。

## 5. テストとレビュー計画

- 単体テスト: 述語は `EvidenceRetention.Tests.ps1` に既存の `staging-7-days` 等と並べて追加（純粋、I/O なし）。
- 差し戻し中の起点 `-Filter`: `tools/Artifacts/tests/EvidenceCleanup.Tests.ps1 -Case <新規ケース>`、`EvidenceRetention.Tests.ps1`、`ArtifactEvidence.Tests.ps1` の該当ケース。C は凍結済み条件の確認に必要なら根拠を記録して変えてよい。
- 判定必須テスト（GO 候補 head）: `tools/Artifacts/tests/*.Tests.ps1` と `tools/Workflow/tests/*.Tests.ps1` の全体（`StoreContext.Tests.ps1` は非仮想化 host で）、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。`EvidenceCleanup.Tests.ps1` は `rg` が PATH に要る。
- 全 EditMode 回帰の適用除外: Unity 側コードを変更しないため。代替証拠は上記 offline suite 全体と audit。
- 統合・Unity テスト: なし。
- 操作・実行時・目視条件の検証経路: `対象なし`（全条件を offline fixture で観測できる。PR #125 の「動作確認（Phase B の後）」は実行しない）。実環境（仮想化 host を含む）での確認は本スライスの受け入れ条件にしない。
- 未知の操作経路の疎通結果: なし。
- 人間の判断が必要な条件: なし。ただし実 task への適用（§2 最終項）は owner の指示を要する。
- 高コスト証拠: なし。判定 C の照合 script: なし。
- 機械検査: `contract-audit`、`docs-audit`、`git diff --stat`。
- A0/A1 主担当: Claude（この session）。独立性の要件はこの欄の対象外。
- A2 独立レビュー（実施済み。結果は §5.1。入力版は commit 後の内容 sha256 を正とし commit hash を補助）: 高リスクのため、(a) アーキテクチャゲート担当 1 件以上、(b) fail-closed の破り方（I1〜I6 を破る順序を各 1 つ挙げる）担当、(c) この HANDOFF の §1 だけを読んで代替構成を出す担当を推奨。担当・モデルは A2 開始時に選ぶ。互いの指摘は見せない。
- A3 統合担当・採否: 2026-10-15、主担当（Claude）と人間（owner）が revision 2 を採用して凍結した。§5.1 の #1〜#10 は採用または注記として §2・§3 に反映済み、#11（cleanup の `MaxObjects` を欠落 entry が消費する点）は保留として後続スライスへ移送する。fail-closed 担当の再確認（revision 2、commit `8464cd5`）の所見は `37fb4d3` で反映済み。凍結対象版は commit `37fb4d3`、内容 sha256 は引継ぎ票のとおり。
- C' 用に予約する担当: 未定。A2 で使い切らない。

## 5.1 A2 独立レビュー結果と A3 の採否

入力版は初稿（sha256 `4cb42b3c9c109a80157420c8232c0f8f9027102ed957d1fd68277bfded90720f`、commit `b8d02a2`）。3 件は互いの指摘を見ずに実施した。(c) は §1 だけを抜き出した入力から立案した。担当・モデルは使用実績で、固定割り当てではない。

| 担当 | モデル | 判定 |
|---|---|---|
| (a) アーキテクチャゲート | Opus 系 | 条件付き GO（所見 1 の解消が前提） |
| (b) fail-closed の破り方 | Sonnet 系 | NO-GO（blocker 2 件。修正後に再レビュー） |
| (c) A0 のみからの代替構成 | Fable 系 | 初稿とほぼ同じ構成に収束（判定なし） |

(c) は独立に、述語の切り出し、既存 `-AllowMissing` の利用、満了後に entry だけ除去、削除 API 不使用、operation 付き欠落は拒否のまま、という同じ骨格を選び、却下案（preflight 除外、検出即除去、`adopted` の書き換え）も一致した。

### 提案する採否（主担当案）

| # | 指摘（担当） | 分類 | 提案する反映 |
|---|---|---|---|
| 1 | dangling junction / symlink が「欠落」と判定される。`Assert-NoArtifactReparse` は `Exists` が真のときだけ属性を見る（b、I1 の blocker） | 採用 | D2 で、leaf が「存在しない」と判定する前に、leaf 自体の属性（`FileInfo.Attributes`、ReparsePoint）を見て、エントリが在るのに解決できないものは欠落ではなく拒否する。AC5 に追加 |
| 2 | 仮想化で root が空の別側へ解決され、全 dormant が欠落になり満了後に記録が落ちる（b、I3/I4 の blocker） | 採用（条件は A3 で確定） | D4 の drop は、同じ task の実在する登録済み path（ready artifact の ownedCopies、または別の存在する staging）を 1 件以上確認できる場合だけ行う（root が実体側を指す証人）。確認できなければ entry を残し `staging-missing` のまま報告する。CLI 入口の仮想化拒否（PR #126）を前提とするが、前提に頼らず証人条件を置く。限界として、反対側に古い catalog の複製がある場合は StoreConsolidation の衝突規則に従い、本スライスの外と §2 に注記する |
| 3 | fetch と use も `Assert-EvidenceTransitionSafe` を通り、D2 後は欠落を黙って許容する（a、I5 の blocker） | 採用 | 件数フィールドを publish・fetch・use・end・resume の全てに出す。AC1 に fetch と use を追加。`workflow-task status` は preflight を通らないため対象外にする（a の所見 2） |
| 4 | 出力名: `residue` の下ではなく、トップレベルの整数 `stagingMissing` を 0 のときも常に出す。config 間は合算、欠落だけなら終了コード 0・status `ok`・`reasonCode` 不使用。publish が failed でも出す（a、b） | 採用 | 未決 1 の回答。(c) の path 配列は、path を出さない README の規則に反するため不採用 |
| 5 | 述語を 2 層にする: stage 単体の `Test-EvidenceStagingUnheld($Stage)`（`Get-EvidenceStagingDecision` が使い、シグネチャ・既存テスト・reasonCode を保つ）と、artifact 参照を見る `Test-EvidenceStagingDormant($Task,$Stage)`。カタログに無い artifactId を参照する stage、property が欠ける stage は dormant にしない（例外ではなく偽）。ownedCopies のいずれかがその stage 配下なら dormant にしない（a、b） | 採用 | 未決 3 の回答。RetentionPolicy に置く |
| 6 | 「root 直下の 32 hex」は生の `stage.path` ではなく、解決済み path に対して `GetDirectoryName(resolved) -ceq Get-ArtifactRoot` と leaf の正規表現で判定する。`Get-EvidenceStagingPathState` は export 必須（a） | 採用 | D2 に反映 |
| 7 | observation の受け渡しは `[ref]` ではなく `[Collections.Generic.List[object]]$Observations` の収集器。キーは cleanup の cursor と同じ SHA-256 を `Get-EvidenceStagingKey` に抽出して共有する（a） | 採用 | D3 に反映 |
| 8 | 欠落かつ operation あり、`adoptionPending` あり、は拒否のまま。欠落 stage は publish の source になれず `invalid-source` を明示する。読取経路と dry-run は catalog を書かない（c） | 採用 | I2 と AC に反映。(c) の `evidence inspect` への項目追加は不採用（後続へ移送） |
| 9 | AC の精度: AC5 に root 不在・dangling junction を追加し、欠落 dormant だけを持つ task（artifact 0 件）で検査して staging 経路が拒否原因だと判別できるようにする。AC4 に満了境界（604800 秒の -1 秒 / 0 秒、判定は `-ge`）と dry-run が catalog を変えないこと、AC6 に `unresolved-put`、述語と `Get-EvidenceStagingDecision` の表駆動一致を追加（b、c） | 採用 | AC を拡張する（新しい完了バーではなく既存条件の検証例） |
| 10 | D4 の競合限界: 再確認から書込みまでに外部で directory が現れると、drop 後に未登録 directory が残る。ファイルは消さないので I3 は守られ、残骸は StoreConsolidation の `unreferenced-transfer` が拾う（a、b） | 注記 | §2 に一文で記載 |
| 11 | cleanup の `MaxObjects` を欠落 entry が dry-run 毎回消費する（b） | 保留 | 致命ではない。後続の入力 |

### 未決事項への回答（提案）

1. 出力面: トップレベル `stagingMissing`（整数、常時）。終了コードは従来規則を保つ（#4）。
2. drop の時機: 満了後に限る（3 担当一致）。即時案は却下のまま。加えて証人条件（#2）。
3. 述語の置き場: RetentionPolicy。2 層に分ける（#5）。

### A3 前に人間が決めること

- #2 の証人条件を「同じ task に実在する登録済み path が 1 件以上」とする案でよいか（代替: 仮想化 host では drop を禁止する）。
- 以上を反映した **revision 2** として §2 と §3 を書き直し、再度 A2 の差分レビュー（(b) の再確認）を行うか、そのまま凍結するか。(b) が NO-GO のため、再確認を推奨する。

## 6. Phase B 実装結果

§4 の順序（述語2層 → PathState・収集器 → publish/fetch/use → cleanup の欠落分岐 → workflow-task → README）で実装した。凍結済みの設計（§2）は変えていない。§4 の「Phase A へ差し戻す条件」には当たらなかった。実ストア・資格情報・network には触れていない（隔離 temp store の offline fixture のみ）。

HANDOFF との差（B 適応。設計判断ではない）:

- **AC10 の hash は `generation` を除いた内容 hash。** `Sync-EvidenceTaskGuarded` は既存の挙動として読むたびに catalog の `generation` を 1 進める（欠落 entry と無関係）。ファイル全体の hash 一致は構造上成立しないため、テストは `generation` 以外（staging entry・artifacts を含む）の hash を比較する。欠落 entry・artifacts は preflight・dry-run の前後で不変。
- **AC5 の実測:** リンク先の無い directory junction・directory symlink・file symlink は、いずれも `Resolve-ArtifactStoredPath -AllowMissing` の `Assert-NoArtifactReparse` が先に拒否する。`Get-EvidenceStagingPathState` の leaf 属性検査（`File.GetAttributes`。`FileInfo.Attributes` は .NET 8 で不在時に -1 を返し例外にならないため使えない）は、その二重化として残してある。
- **`workflow-task` の `stagingMissing` は start・end・resume に出す。** start も同じ preflight を通るため（status のみ除外）。
- 述語は `Test-EvidenceStagingUnheldStrict`（例外を投げる内部形）を追加し、`Get-EvidenceStagingDecision` は property 欠落時の `unknown-staging` を従来どおり返す。公開面は §3 の列挙どおり（Strict は未 export）。
- 競合テスト（AC7）のため、cleanup に既存の `FaultHook` 方式で `before-staging-drop` の点を 1 つ足した。

追加テスト: `EvidenceRetention.Tests.ps1`（`staging-unheld-table`、`staging-dormant-table`＝AC9）、`EvidenceCleanup.Tests.ps1`（`staging-missing-report`＝AC1/2/10・invalid-source・失敗 publish の件数、`-drop`＝AC4、`-no-witness`＝AC8、`-race`＝AC7、`-refuse`＝AC5/6。対照として許容される欠落を先に確認）、`TaskLifecycle.Tests.ps1`＋`WorkflowCliFixture.ps1`（AC3、config 2 つで合算 2）。AC6 の「ownedCopies が配下にある」は、述語単体を AC9 の表駆動で検証する。拒否経路は、配下にファイルの無い ownedCopy を持つ artifact と欠落 staging を併せた構成（`staging-missing-refuse` の `owned copy under stage`）で、cleanup・publish が `staging-missing` を返さず従来どおり拒否することを確認する（判定 C の差し戻し 1 巡目で追加。実在ファイルが配下にあると directory は欠落しない）。また `staging-missing-report` で、preflight の戻り値（削除済み artifact id の配列）に証人の増分が混ざらないことを確認する。

確認結果（head は push 後に確定）: `tools/Artifacts/tests/*.Tests.ps1` と `tools/Workflow/tests/*.Tests.ps1` の全体を実行し全て通過（EvidenceCleanup 40、EvidenceRetention 16、TaskLifecycle 23、StoreContext 22 ほか。`rg` は cursor 同梱を PATH に追加）。`R2RouteTransport.Tests.ps1` は Probe の build 出力が無い新しい worktree で最初に落ちたため、README の `dotnet build` で作り直して通過（7）。Packaging・Transport・Probe の build 出力は未追跡の生成物で commit しない。`pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1` は errors=0。Unity は変更しておらず未実行。

README の harvest は B で実施済み（`tools/Artifacts/README.md`、`tools/Workflow/README.md`、プログラム文書 §4 E の該当項目）。

## 7. Phase C / 8. Phase C' / 9. Phase D

未到達。Phase D の Evidence 保存・引渡しは [レビュー証拠](../../.agents/skills/osm-workflow/references/review-evidence.md#phase-d-の保存引渡し確認) に従う。
