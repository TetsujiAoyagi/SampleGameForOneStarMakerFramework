# Artifact Storage / Evidence 欠落stagingの回復 HANDOFF

## 0. メタデータ

- type: `slice`
- status: `A`（A0 / A1 初稿。A2 独立レビュー未実施、A3 未凍結。凍結前に実装へ進まない）
- branch: `claude/amazing-blackburn-f04cf2`（作業用の仮名。PR を作る前にスライスを表す名前へ改める）
- implementation base commit: `a251201`（`origin/develop`、PR #126 マージ）
- implementation head commit: 未固定
- risk: `high`（失敗時に処理を拒否する契約 fail-closed と、catalog の保持・削除の意味に触れる。[新しい事実の分類](../../.agents/skills/osm-workflow/references/phases-and-handoff.md#新しい事実の分類) の「A 再開」級の判断を含む）
- owner: OSM maintainers
- created: 2026-10-15
- expires: 2026-11-15 または置換 revision
- harvest to: 挙動と reasonCode を `tools/Artifacts/README.md`（自動清掃・通常操作の節）と `tools/Workflow/README.md`（end の出力）へ反映し、`BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §4 E の該当項目を消す。
- Phase A snapshot の取得案内・状態: 未生成
- Phase B result snapshot / evidence bundle / C' blind bundle: 未生成（未到達 Phase）
- 引継ぎ票: 次は A2（独立レビュー）。レビューは §5 の入力版だけを読む。必要な権限は読取りのみ。**実ストアの catalog・transfers・資格情報は読まず、編集もしない。** 停止規則は §2。

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

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 「未採用・operation なし・実体なしの staging を、自分の残骸と証明できる範囲だけ、報告付きで通常操作から回復可能にしつつ、実パス・ACL・reparse の異常を従来どおり拒否できるか」。
- 方式（A1 初稿）:
  - **D1 欠落 dormant staging を許容する。** 純粋述語 `Test-EvidenceStagingDormant($Task,$Stage)` を `EvidenceRetentionPolicy.psm1` に置く。真の条件は、`origin=storage-copy`、未採用、`adoptionPending` 偽、`operation` null、`deleteIntent` null、protection 0、かつ `artifactId` が null か、参照先 artifact が `put-pending` でも `deleteIntent` 持ちでもないこと（cleanup 225 行の既存除外と同じ）。`Get-EvidenceStagingDecision` も同じ述語を使い、同じ事実を 2 箇所に書かない。
  - **D2 経路状態の判定を 1 関数にする。** `Get-EvidenceStagingPathState` を `EvidenceCleanup.psm1` に置く。dormant でなければ従来どおり `Resolve-ArtifactStoredPath` を `-AllowMissing` 無しで呼ぶ。dormant なら leaf が transfers root 直下の 32 hex であることを確認し、`-AllowMissing` で解決する。解決結果が存在しなければ `missing`、存在すれば従来の検査へ。形式違反・root 不在・ACL 不適合・reparse は例外のまま。
  - **D3 欠落は報告し、即時には捨てない。** `Assert-EvidenceTaskPaths` は拒否しない代わりに欠落 observation を返す。`Assert-EvidenceTransitionSafe` は既存の戻り値（deleted artifact id の配列）を変えず、任意の out 引数に observation を積む。publish は `residue.stagingMissing`（件数）、`workflow-task` の end/resume/status 出力は `stagingMissing`（件数）、cleanup は item に `reasonCode='staging-missing'` を返す。
  - **D4 満了後に entry だけ落とす。** cleanup は欠落 dormant staging が既存の 7 日（`staging-expired`）に達したときだけ、task guard 内で再度欠落を確認し、catalog から entry を外す。この分岐は**ファイル・directory の削除 API を一切呼ばない**。item は `reasonCode='staging-missing-dropped'`。件数は既存の `skipped` に数え（削除していないため `deleted` に数えない）、新しい counter と EvidenceSchedule の変更は加えない。再確認時に directory が現れていたら、従来の削除経路（存在する staging の通常処理）へ戻す。
- 却下した代替案:
  - catalog の手修正・`end` の再試行: 禁止（依頼）。
  - preflight で欠落を見つけ次第 entry を即時に捨てる: 仮想化で directory が別側にある場合、記録だけを失い別側の残骸が追えなくなる。7 日の猶予は既存の保持規則そのもので、StoreConsolidation の `unreferenced-transfer` 保留とも矛盾しない。
  - `end` だけ Evidence preflight を飛ばす: 「Storage の曖昧さを解決してから Workflow が確定する」（workflow-task.ps1 コメント）を破り、publish・cleanup の停止も残る。
  - 欠落に限らず解決不能な dormant staging を全部許す: ACL・reparse 異常まで通り fail-closed を破る。
- 進める最低条件:
  - 不変条件（起きてはいけないこと）:
    - **I1** transfers root が不在・ACL 不適合・reparse、または staging path の形式が不正（root 外、leaf が 32 hex でない、`..` 等）のとき、従来どおり拒否する（欠落として扱わない）。
    - **I2** dormant でない staging（採用済み・operation あり・deleteIntent・protection・採用 pending・put-pending/deleteIntent artifact を参照）の解決失敗は従来どおり拒否する。
    - **I3** 欠落 entry の処理は catalog entry の除去だけで、ファイル・directory を一つも削除しない。他 entry、他 task、artifact の ownedCopies、transfers 内の未登録 directory は変化しない。
    - **I4** 7 日の満了前に欠落 entry を catalog から捨てない。
    - **I5** 欠落を許容した通常操作は、その事実（件数）を出力に含める。黙って成功にしない。
    - **I6** artifact の ownedCopies 欠落（`changed-copy`）、operation 付き staging の欠落（`operation-pending`）、putIntent 不一致は本スライスで緩めない。
  - 成立条件:
    - **S1** 欠落 dormant staging を持つ task で、publish が `passed`、`workflow-task end` が成功し、cleanup（dry-run を含む）が `unconfirmed` ではなく `staging-missing` を返す。
    - **S2** 7 日以上経過した欠落 dormant staging は、cleanup が entry を外し（`staging-missing-dropped`）、artifact・他 staging・ファイル群は不変。
    - **S3** S1 の出力に `stagingMissing` の件数が現れる。
- 受け入れ条件（検証例。いずれも隔離した temp store の offline fixture、実ストア・network・Unity 不使用）:
  - **AC1（S1, S3）** 既存の publish 済み fixture（ready artifact あり）に、未採用・operation なし・artifactId null の staging を `transfers\<新しい 32hex>` で登録し directory は作らない。`Invoke-EvidencePublish` が `passed`、`residue.stagingMissing=1`。`Assert-EvidenceTransitionSafe -Observations` が 1 件を返す。合格: 上記。不合格: `unconfirmed`、または件数 0。
  - **AC2（S1）** 同 fixture で `Invoke-EvidenceCleanup -DryRun` が当該 item を `staging-missing`（満了前）にし、`unconfirmed` を出さない。同 task の ready artifact の item も従来の reasonCode のまま出る。
  - **AC3（S1）** `workflow-task end` 相当（`tools/Workflow/tests/WorkflowCliFixture.ps1` の stub に観測を返させる）で `stagingMissing` が出力に出て、transition が commit される。
  - **AC4（S2, I3, I4）** 満了前の cleanup は entry を残す。`createdAt` を 7 日前にして cleanup すると entry が外れ、`deleted=0`、`skipped` に数える。sentinel file を置いた別の `transfers\<32hex>` directory と、他 task の catalog が cleanup 前後で同一（hash 一致）。
  - **AC5（I1）** root の ACL を壊す、leaf を非 hex にする、root 外 path にする、path 上に junction を置く、の各 fixture で、欠落 entry を持つ task の publish/cleanup が従来どおり拒否（`unconfirmed`）。欠落扱いにならない。
  - **AC6（I2, I6）** 欠落かつ operation あり、adopted=true かつ operation あり、protection あり、deleteIntent あり、put-pending artifact を参照、の各 staging は従来どおり拒否。ownedCopies の欠落（既存 `missing-owned-copy`）の結果は変わらない。
  - **AC7（I3 競合）** 欠落判定の後、drop の直前に directory が現れる fixture（`FaultHook` 相当）で entry を外さず、従来の削除経路が走る。
- 判定定義: AC1〜AC7 が offline suite で通り、`contract-audit` と `docs-audit` が通れば GO。いずれかの不変条件を破る再現があれば NO-GO。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- ここでは答えない問いと所有スライス（program §4 E へ記録済みの項目。所有スライスは各着手時の A0 で確定）:
  - Evidence task catalog の 1 MiB 上限（600 file の bundle で 919 KB、次の fetch が `record-limit`）と、上限を超えた fetch が不完全な state を残す経路: `Artifact Storage / Evidence task catalog size`（未着手）。スキーマ・record 形式の設計判断を含むため別 A0。本スライスの対象にしない。
  - 欠落の**原因**の特定（手動削除か仮想化か）と、Codex 側などに残った実 directory の後始末: `Artifact Storage / Split Store Recovery`、`Codex側残置の後始末`（program §4 E）。
  - artifact の ownedCopies 欠落、operation 付き staging の欠落の回復: 上と同じ program 項目。I6 により現挙動を保つ。
  - `invalid-evidence-path` 等の診断を `unconfirmed` から分ける: program §4 E 末尾の項目。
  - 実 task `artifacts-virtualized-store` への適用: 本スライスのマージ後、owner の指示で通常コマンド（`evidence cleanup`、`workflow-task end` を 1 回）を実行する運用。計画段階と実装では実ストアに触れない。
- A3 後の例外承認: なし
- 決定記録: なし
- 本文へ転記した実装制約:
  - Unity 側コードは変更しない。PowerShell は既存の module 境界（EvidenceRetentionPolicy は純粋、I/O は EvidenceCleanup）を保つ。
  - 一時的な debug 用 hook を残さない。テスト用の観測は既存の `FaultHook` / `Set-EvidenceTestValue` 方式を使う。
  - 例外本文・実 path を出力に出さない（README の既存規則）。observation には staging の位置を示す値として path ではなく、既存の cursor と同じ SHA-256 キーを使う。
- 未決事項（A2 で判断を求める）:
  1. D3 の出力面（`residue.stagingMissing` と workflow-task の `stagingMissing`）の名前と、end の終了コード（現案: 欠落だけでは 0、`recorded/delivery-pending` の既存規則は不変）。
  2. D4 の drop を満了後に限る案と、欠落かつ transfers root が健全なら即時に落とす案の比較（現案は前者。仮想化 host 下の誤判定が主な懸念）。
  3. `Test-EvidenceStagingDormant` を RetentionPolicy に置くか（現案）、Cleanup に置くか。

## 3. 責務マップ

| ファイル | 責務 | 変更理由 | 現在行数 → 予想 | 備考 |
|---|---|---|---|---|
| `tools/Artifacts/EvidenceRetentionPolicy.psm1` | 純粋な保持 policy（時刻と snapshot だけで判定） | 「dormant staging」の述語を一箇所に置く | 37 → 約 45 | I/O を持たない。単体テスト可能 |
| `tools/Artifacts/EvidenceCleanup.psm1` | staging/artifact の経路検証と清掃の orchestration | 欠落の判定関数、observation の返却、満了後の entry 除去 | 285 → 約 320 | 500 行未満。責務は増えない（既に経路検証と清掃を持つ）。末尾に `Export-ModuleMember` 後の関数定義があるが既存で、今回は動かさない |
| `tools/Artifacts/EvidenceApplication.psm1` | publish/fetch の orchestration | 候補ループの欠落許容と `residue.stagingMissing` | 132 → 約 140 | |
| `tools/workflow-task.ps1` | task 終了/再開の CLI | 出力に `stagingMissing` を足す | 70 → 約 74 | |
| `tools/Artifacts/README.md`、`tools/Workflow/README.md` | 公開仕様 | harvest | 各数行 | |
| `tools/Artifacts/tests/EvidenceCleanup.Tests.ps1` ほか | offline 検証 | AC1〜AC7 | 追加ケース | `$registered` の長い一覧は既存形式のまま追記 |

状態の所有者は task の catalog（task guard が直列化）。寿命は既存の staging 保持（7 日）。新しい公開 API は `Test-EvidenceStagingDormant`、`Get-EvidenceStagingPathState`（module 内部、必要なら export）と `Assert-EvidenceTransitionSafe` の任意引数 1 つだけ。新しいファイル・依存 edge・状態は作らない。行数警報（500 行、3 責務、50% 増）は発火しない。

## 4. 実装計画

- 順序: (1) 述語と `Get-EvidenceStagingDecision` の共通化（挙動不変をテストで固定）→ (2) `Get-EvidenceStagingPathState` と `Assert-EvidenceTaskPaths`、`Assert-EvidenceTransitionSafe` → (3) publish 候補ループ → (4) cleanup の欠落分岐 → (5) workflow-task 出力 → (6) README。
- Phase B から Phase A へ差し戻す条件: catalog スキーマ（`Read-EvidenceTask` の `Assert-Fields`）の変更が必要、`EvidenceSchedule` の変更が必要、欠落判定に root 以外の新しい所有者情報が要る、`end` の終了意味を変える必要が出たとき。
- 対象外を維持する方法: 欠落分岐に削除 API を呼ぶコードを置かない（AC4 の sentinel と hash で確認）。1 MiB 上限の変更を含めない。

## 5. テストとレビュー計画

- 単体テスト: 述語は `EvidenceRetention.Tests.ps1` に既存の `staging-7-days` 等と並べて追加（純粋、I/O なし）。
- 差し戻し中の起点 `-Filter`: `tools/Artifacts/tests/EvidenceCleanup.Tests.ps1 -Case <新規ケース>`、`EvidenceRetention.Tests.ps1`、`ArtifactEvidence.Tests.ps1` の該当ケース。C は凍結済み条件の確認に必要なら根拠を記録して変えてよい。
- 判定必須テスト（GO 候補 head）: `tools/Artifacts/tests/*.Tests.ps1` と `tools/Workflow/tests/*.Tests.ps1` の全体（`StoreContext.Tests.ps1` は非仮想化 host で）、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。`EvidenceCleanup.Tests.ps1` は `rg` が PATH に要る。
- 全 EditMode 回帰の適用除外: Unity 側コードを変更しないため。代替証拠は上記 offline suite 全体と audit。
- 統合・Unity テスト: なし。
- 操作・実行時・目視条件の検証経路: なし（全条件を offline fixture で観測できる）。実環境（仮想化 host を含む）での確認は本スライスの受け入れ条件にしない。
- 未知の操作経路の疎通結果: なし。
- 人間の判断が必要な条件: なし。ただし実 task への適用（§2 最終項）は owner の指示を要する。
- 高コスト証拠: なし。判定 C の照合 script: なし。
- 機械検査: `contract-audit`、`docs-audit`、`git diff --stat`。
- A0/A1 主担当: Claude（この session）。独立性の要件はこの欄の対象外。
- A2 独立レビュー（未実施、入力版は commit 後の内容 sha256 を正とし commit hash を補助）: 高リスクのため、(a) アーキテクチャゲート担当 1 件以上、(b) fail-closed の破り方（I1〜I6 を破る順序を各 1 つ挙げる）担当、(c) この HANDOFF の §1 だけを読んで代替構成を出す担当を推奨。担当・モデルは A2 開始時に選ぶ。互いの指摘は見せない。
- A3 統合担当・採否: 未実施。
- C' 用に予約する担当: 未定。A2 で使い切らない。

## 6. Phase B 実装結果

未到達。

## 7. Phase C / 8. Phase C' / 9. Phase D

未到達。Phase D の Evidence 保存・引渡しは [レビュー証拠](../../.agents/skills/osm-workflow/references/review-evidence.md#phase-d-の保存引渡し確認) に従う。
