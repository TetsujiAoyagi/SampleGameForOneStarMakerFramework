# Artifact Storage: LocalCache 側 copy の扱い（合流の価値の判断と削除）

packaged app（Codex）が旧 root の `Artifacts` / `Workflow` / `Harness` として書き、書込み仮想化で LocalCache 側に入った記録（以下 **copy**）を、合流させずに全部削除して終えるスライス。`BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md`（以下 program）§6「LocalCache側copyの扱い（合流の価値の判断と削除）」を受け持つ。前提は program §4 F（store root の移設と実ストアの移行。PR #139・#140、2026-10-19 に完了）。

owner の決定（2026-10-20）: **copy は全部削除する。** 過去の遺物のために厳密さを足して重くしない。合流の価値の判断（program §6 の問いの前半）は「価値なし」で決着とし、copy の中身の分類・R2 の key の追跡・網羅性の証明はしない。

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: E1〜E4 完了（copy root は削除済み。§6）。次は §4 の E5（公開文書の書換えと文書だけの PR）。Phase A revision 1 は 2026-10-20 に凍結（凍結版 = message に `A3 freeze` を含む commit `2a08883a`）
- branch: `claude/localcache-copy-disposition`（`develop` = `cbcdb22c` から切った。PR base は `develop`）
- implementation base commit: `cbcdb22cb7e47671ebd01927d1ac92fa7b9425c8`（PR #140 の merge 後の `develop`）
- implementation head commit: 該当なし（コードの変更は無い。§4 の script は本書に埋め込み、repo に置かない）
- risk: `normal`（再帰削除という操作は不可逆で危険だが、対象は開発用の残置で owner が全部捨てると決めたので、残置の中身の価値は問わない。その代わり、操作の危険は削除対象の固定・非仮想化の確認・link の拒否・失敗時の固定 code で抑え、Phase B〜C' を使わない一回限りの軽い手順を owner の例外として選んだ。§5）
- owner: OSM maintainers（凍結、削除の直前の承認、文書の PR の merge）
- created: 2026-10-19
- expires: 2026-12-19 または置換 revision
- harvest to: `tools/Artifacts/README.md`（「実行するprocessの条件（store rootと移行済みの印）」の末段、「分裂したストアの合流」）、`tools/Harness/README.md`（H1・H2c の固定記録の入口と「注記（合流前）」）、`docs/README.md`（program 表と「注記（合流前）」）、program §4 A・E・F の注記と §6 の本項目
- Phase A snapshot の取得案内・状態: Git 上の本文で完結（A0 = commit `31263e9e`、A2 入力版 = commit `ce258f34`、凍結版 = message に `A3 freeze` を含む commit。内容 sha256 はその commit の blob から求められるので転記しない）
- Phase B result snapshot / evidence bundle / C' blind bundle の取得案内・状態: 該当なし（§5。実行の記録は §6 に本文として残す）
- 引継ぎ票（凍結後の状態として書く）:
  - 次の作業: §4 の E5（E1〜E4 は完了。§6）。
  - 引き継ぐ commit SHA: 実行の記録の commit（branch の先頭。凍結版は `2a08883a`）。作業ツリーが dirty なら引き継がない。
  - 実行担当: 非仮想化の pwsh を使える session（Claude Code の PowerShell tool で可）。Claude Code の Bash では行わない（LocalAppData への書込みが仮想化されるため）。
  - 必要な権限: copy root の削除（E3 の直前の owner の承認だけ）。
  - 停止規則: §2 の「止まる条件」のどれかに当たれば、調べ直さず、そのまま owner に返す。
  - 引き渡さないもの: copy・旧 root・新 root の実 path、package 名を含む path、資格情報。

## 1. 目的と対象外

- 目的: copy root（Codex の package の `LocalCache\Local\OneStarMaker`）を丸ごと削除し、packaged app 内の process が copy のために `legacy-store-present` で止まる条件を無くす。あわせて、公開文書の「注記（合流前）」と、copy にしか無い Harness の記録を「固定記録の入口」とする記述を、削除後の現況に直す。
- 対象外:
  - copy の合流、copy の中身の分類・保存、copy の記録が追跡していた R2 の object の列挙・削除（孤児として残ることは program §6「Contentの容量上限と孤児回収」に一文で書くだけ）。
  - 旧 root の tombstone と `RevisionLocks`、新 root、他の package、copy root の兄弟（同じ package の `LocalCache\Local` の他の entry）。どれも消さない・書き換えない。
  - resolver、3 CLI、印、store の module のコード。repo の `tools/` と `unity/` は変えない。
  - 資格情報の登録・置換・rotation・失効。copy の中の資格情報の複写の中身は開かない（directory ごと消す）。
  - packaged app（Codex）自身の設定・サンドボックス。
- 現況（A0 で固定した観測。日付は 2026-10-19、別記のものを除く）:
  - **実ストア:** 移設済み。新 root（UserProfile 直下の `OneStarMaker`）に移行済みの印があり、旧 root（LocalApplicationData 直下の `OneStarMaker`）の所有 role 3 つは tombstone（size 0 の通常 file）。旧 root には ほかに `RevisionLocks`（directory）だけがある。本 session（Claude Code の PowerShell tool。非仮想化として扱う）の列挙で、旧 root の `Artifacts` / `Workflow` / `Harness` は `dir=False`、`RevisionLocks` は `dir=True`。
  - **copy の所在:** 本 session から `LocalApplicationData\Packages\*\LocalCache\Local\OneStarMaker` を列挙すると、copy があるのは Codex の package 1 つだけ（Claude の package には無い）。最上位の entry と、reparse point をたどらない再帰列挙の file 数・合計 size は次のとおり（内容は開いていない。更新時刻は各 directory 自身の LastWriteTime）:

    | entry | 種別 | file 数 | 合計 size | 更新時刻 |
    |---|---|---|---|---|
    | `Artifacts` | 所有 role | 4,999 | 約 1.15 GB | 2026-10-10 |
    | `Workflow` | 所有 role | 37 | 約 29 KB | 2026-10-08 |
    | `Harness` | 所有 role | 2,385 | 約 199 MB | 2026-09-29 |
    | `WorktreeRecords` | 非所有 | 5,220 | 約 213 MB | 2026-10-10 |
    | `H2a Review` / `H2b Review` | 非所有 | 223 / 825 | 約 51 MB / 約 98 MB | 2026-10-01 |
    | `RevisionLocks` | 非所有（Unity Runtime） | 1,346 | 0 | 2026-09-20 |
    | `.virtualization-probe-20261012-<hex>` | 非所有（過去のスパイク） | 0 | 0 | 2026-10-12 |

  - **copy の `Artifacts` の下:** `builds-v1`、`credentials`、`deployments`、`evidence-handoffs`、`evidence-ledgers`、`evidence-retention`、`evidence-v2`、`resets`、`review-evidence`、`runtime`、`transfers`。`evidence-v2` には repositoryId 1 つの `tasks` がある（最終更新 2026-10-12）。
  - **copy の `Workflow` の下:** repositoryId が 2 つ。本 repo の側に task 17 件（`artifact-build-publish-one`、`artifact-build-retention-policy`、`artifact-evidence-lifecycle`、`debugstudio-player-ui-route`、`debugstudio-property-write`、`evidence-c-<16 hex>` 9 件、`integration-pr94-completion-20261010`、`integration-pr95-repair-20261010`、`integration-pr97-docs-20261010`。更新 2026-10-08〜10-12）。もう一方は 64 桁が全て `a` の repositoryId で、`guard-bound-identity-mismatch`、`guard-cross-process`、`guard-identity` の 3 件（2026-10-08。テストの fixture が実ストアの位置へ書いたものに見えるが、未確認）。
  - **copy の `Harness` の下:** task 4 件（`h1-transport-identity` 2026-09-30、`h2c-unity-gate` 2026-10-04、`h2c-unity-gate-r2` 2026-10-04、`artifact-evidence-lifecycle` 2026-10-10）。実ストアの Harness は空（PR #140 の smoke で `harness.ps1 status` が `taskはありません`）。
  - **2026-10-17 の読取のみの `store plan`（1 回）:** 単位 20（`publish` 4・`skip` 0・`hold` 16）、`excluded` 90（`unreferenced-transfer` 77、`not-owned` 8、`out-of-scope-role` 2、`credentials` 1、`revision-locks` 1、`unrecognized` 1）。Harness の 4 task は `h2c-unity-gate-r2` が `publish`、`h1-transport-identity` と `h2c-unity-gate` が `hold`（`reference-missing`）、`artifact-evidence-lifecycle` が `hold`（`dependency-held`、`multi-workflow-unit`、`reference-localcache`、`reference-missing`、`reference-out-of-role`）。deployment 単位 1 件が `conflict` で保留され、Evidence を持つ task 単位の多くがそれへの `dependency-held`。`unrecognized` 1 件は `retirements-v1`。この manifest は `logicalRoot` が旧 root で `legacyRoots` を持たないので、現在の CLI では `manifest-invalid` になり、合流に進むなら再 plan が要る（program §4 F の移設スライスの記録）。`store consolidate` は dry-run も含めて一度も実行していない。
  - **公開文書の依存:** `tools/Harness/README.md` は H1 の入口として `harness.ps1 current -Task h1-transport-identity`、H2c の固定記録の入口として `harness.ps1 current -Task h2c-unity-gate-r2` を挙げ（`AGENTS.md` は H2c の固定記録の入口を同 README に委ねている）、`tools/Artifacts/README.md` はスライス A の入口として `harness.ps1 current -Task artifact-evidence-lifecycle` を挙げる。4 件とも copy 側にだけあり、非仮想化 process からは読めないことを「注記（合流前、2026-10-17の `store plan`）」として `docs/README.md` に 1 か所、`tools/Harness/README.md` に 4 か所、`tools/Artifacts/README.md` に 2 か所、program に 3 か所書いている。どちらに決めてもこれらの記述は書き換えが要る。
  - **Harness CURRENT の新 root での読み戻しは未実証:** PR #140 の smoke 項目 4 は owner 承認の縮退（空状態の `status`）で、`harness.ps1 current` が実際の記録を新 root で読めることは示していない。承認済み snapshot を Git から得られない見込みが高い（PR #140 の進行役が git 履歴の全 `.md` blob を 4 つの承認値と照合して一致 0 件）。
  - **packaged app 側:** copy の `Artifacts` / `Workflow` / `Harness` が見える間は、Codex 内の process は `legacy-store-present` で止まる（README「実行するprocessの条件」）。tombstone（実体側の通常 file）と copy の directory が同じ名前で重なったときの仮想化された view の見え方は未観測（PR #140 の smoke 項目 7 は未実施）。
  - **清掃が届かない:** Storage の 30 日清掃と定期清掃 job は実ストア（新 root）だけを見る。copy の Evidence・Build・transfers の記録は、どの清掃からも届かない。
  - **保存方針（program §2「開発Evidence」）:** 恒久的な設計判断・制約・検証要約は公開文書へ残し、生ログ・画像・bundle・派生 copy・witness に長期監査保存を課さない。削除後の参照は期限切れと分かればよく、payload の復元は保証しない。
  - **削除の確認モードの試行（2026-10-20）:** §4 の script を、消さない確認モードで実 copy に対して 1 回実行した（非仮想化 pwsh）。exit 0、約 1.5 秒。最上位 entry 8 つ、file 15,035・directory 2,051・合計 1,714,521,154 bytes（A0 の表の file 数の合計と一致）、reparse point は無し。新 root の直下は `Artifacts`・印・`Workflow`、旧 root の直下は tombstone 3 つ（size 0）と `RevisionLocks`。出力に絶対 path と package 名は無かった。

## 2. 意思決定と受け入れ境界

- **このスライスが答える問い:** copy root を一回の削除で丸ごと消したとき、copy root 以外（新 root、旧 root の tombstone と `RevisionLocks`、copy root の兄弟、他の package）を変えずに、copy root が無い状態に到達できるか。
- **不変条件（起きてはいけないこと）:**
  - **I1 copy root の外を消さない・変えない:** 削除の前後で、新 root（印の bytes、直下の entry の名前と種別）と旧 root（直下の entry の名前・種別・size）が同じ。copy root の兄弟と他の package に触れない。
  - **I2 link の先に届かない:** copy root の中、または copy root とその祖先に reparse point（junction・symbolic link）があれば、何も消さずに止まる。
  - **I5 別の copy を消さない:** `Packages` の配下で copy root の形を持つ directory がちょうど 1 つで、それが `-CopyRoot` と一致する場合だけ進む（2 つ以上なら `multiple-copies`、違えば `copy-root-mismatch`）。`-Delete` には E2 が出した `source`（正規化した copy root の path の SHA-256。package 名と path は出さない）と同じ値を渡す（違えば `source-mismatch`）。実行の入口は Known Folder の値だけを使い、root を上書きする引数を持たない（R5-1）。
  - **I6 仮想化された view で動かない:** `tools/Workflow/WindowsStoreContext.psm1` の `Get-OsmStoreContext` が `not-virtualized` を返さなければ、確認も削除も `absent` の判定もしない（`store-context-not-verified`。R5-2）。
  - **I3 承認の前に消さない:** E3 の直前に owner が「消してよい」と言うまで、`-Delete` を付けて実行しない。
  - **I4 秘密と実 path を出さない:** 出力は件数・最上位 entry の名前・印の hash・`source` だけで、絶対 path・package 名・SID を含まない。失敗も全て捕らえ、stdout に固定の `reasonCode` だけを出して exit 1（.NET・PowerShell の例外の文には path が入りうるため。R5-3）。資格情報の複写の中身を開かない。
- **成立条件（起きなければならないこと）:**
  - **S1** `-Delete` の実行が `deleted` を返す（copy root が無く、新 root・旧 root の直下が削除の前と同じ）。
  - **S2** 実行担当とは別の確認として、E4 で同じ script（`-Delete` なし）を流し、非仮想化の判定を通った上で `absent` が返ることを見る（`Test-Path` の false だけを証明にしない。R5-2）。
  - **S3** 公開文書の「注記（合流前）」と「固定記録の入口」の記述が、削除後の現況に合う（§4 の E5。文書だけの PR）。文書には「物理的な copy を削除した」と書き、「Codex から CLI が動くことを実証した」とは書かない（R5 補足）。
- **受け入れ条件（検証の例）:**
  - **AC-1（I1・S1）:** temp の fixture（§5）で、`-Delete` が `deleted` を返し、copy root の兄弟の file・新 root の印・旧 root の tombstone が残る。読取り専用の file も消える。
  - **AC-2（I2、失敗経路）:** fixture の copy root の中に junction を置くと `reparse-present` で止まり、copy root も link の先も残る。
  - **AC-3（I1、失敗経路）:** copy root の形に合わない path（旧 root）を渡すと `copy-root-shape` で止まり、何も消さない。
  - **AC-6（I5、失敗経路）:** copy が 1 つの fixture で別の package の同じ形の path を渡すと `copy-root-mismatch`、copy が 2 つの fixture では `multiple-copies`、`-Delete` に違う `source` を渡すと `source-mismatch` で止まり、何も消さない。
  - **AC-7（I6、失敗経路）:** context が `virtualized` / `unknown` なら `store-context-not-verified` で止まり、何も消さない。
  - **AC-8（I4、失敗経路）:** 削除中に file を排他で開いたままにすると `removal-failed` で止まり、全ての case の出力に fixture の path と package 名が出ない。
  - **AC-4（再実行）:** 消した後にもう一度実行すると `absent` を返す。
  - **AC-5（実操作）:** E1〜E4 を一回行い、E2・E3・E4 の出力が S1・S2 を満たす。
- **止まる条件:** `status` が `failed`（`store-context-not-verified`、`copy-root-shape`、`multiple-copies`、`copy-root-mismatch`、`source-mismatch`、`reparse-present`、`removal-failed`）、`still-present`、`roots-changed`、E3 で予期しない `absent`。どれも調べ直さず、出力をそのまま owner に返す。`removal-failed` で copy が一部だけ消えた場合も再実行せず owner に返し、続けるかは owner が決める（続けるなら E2 からやり直す）。`roots-changed` は、削除中に別の writer（定期清掃 job 等）が新 root か旧 root の直下を変えた可能性も含むので、owner が出力を見て判断する。
- **判定:** GO = I1〜I6 に違反が無く S1〜S3 が成立。それ以外は NO-GO で owner に返す（R6）。E4 の `absent` は E3 の `deleted` と組で判定し、単独で削除の成功の証明にしない（`Directory.Exists` は権限や I/O の異常でも false を返しうるため）。
- **ここでは答えない問いと所有する後続:**
  - copy の記録が追跡していた R2 の object（孤児として残る）: program §6「Contentの容量上限と孤児回収」。本スライスは「copy 由来の追跡できない object が残っている」と一文書くだけで、件数も数えない。
  - 旧 root の tombstone と親 directory の後始末、旧 root 別名の廃止: program §6「分裂したストア」の既存の入力。
  - packaged app 内での実際の結果（印の読取り、サンドボックスの条件）: Codex を次に Phase 担当にするときの最初の作業で確かめる（各自の運用メモ）。
- **決めたこと（owner）:**
  - 全部削除する。合流しない。copy の中身の分類・R2 の key の一覧・網羅性の証明はしない（2026-10-20）。
  - tombstone は残す（README の「消さないでください」を正とする。program §6 の本項目の「旧rootに残るtombstone」は削除の対象から外し、Phase D で記述を直す）。
  - 公開文書の書換え（E5）: 「注記（合流前）」計 10 か所（`docs/README.md` 1、`tools/Harness/README.md` 4、`tools/Artifacts/README.md` 2、program 3）を削除後の現況に直す。`harness.ps1 current -Task <copy にしか無かった 4 task>` を「固定記録の入口」とする記述は、「CURRENT は copy と一緒に削除した。検証の要約は PR #93（H2c）、PR #109（スライス A）等にある」に直す。H2c の 2 task id は承認済み snapshot が無いため再 init できないことも書く。`AGENTS.md` の「完了済みH2cの実例と固定記録の入口は `tools/Harness/README.md` を正とする」は README のこの記述で満たし、`AGENTS.md` は変えない。`ApprovedSpecifications.psm1` の承認 entry とそれを使うテスト・分岐は残す（参照 0 を削除理由にしない）。
  - 重い検証の手順（一回限りの program、Phase B〜C' の evidence bundle、照合 script）は使わない（§5）。
- **本文へ転記した実装制約:**
  - LocalAppData の下を消す・確かめる操作は pwsh で行い、Claude Code の Bash では行わない。
  - Artifact Storage の通常操作の事前承認に copy の削除は含まれない。E3 の直前の owner の承認で 1 回だけ行う。実行環境の承認・拒否を迂回しない。
  - 出力・tracked 文書・PR に実 path・package 名を含む path・SID を書かない。
  - PR の base は `develop`。

## 3. 責務マップ

該当なし。repo の file は増やさず、`tools/` と `unity/` を変えない。変更は本書と公開文書（E5）だけ。削除の script は §4 に埋め込み、実行担当が scratch に書き出して使う。

## 4. 実行の手順

| 手順 | 担当 | 内容 | 止まる条件 |
|---|---|---|---|
| E1 静止 | owner | Codex のアプリを終了し、「Codex を終了した」と伝える | — |
| E2 確認 | 実行担当 | 凍結版の commit を checkout した worktree で、下の script を本文から scratch に書き出し、その file の SHA-256 を記録する。`pwsh -NoProfile -File <script> -RepositoryRoot <worktree> -CopyRoot <copy root>`（`-Delete` なし）を実行し、出力（`source`、件数、最上位 entry、新旧 root の直下）を owner に見せる | `checked` 以外 |
| E3 削除 | owner → 実行担当 | owner が E2 の出力を見て「消してよい」と言ったら、実行の直前に、scratch の script の SHA-256 が E2 で記録した値と同じで、worktree の HEAD が凍結版の commit のままであることを確かめてから、同じ引数に `-Delete -ExpectedSourceSha256 <E2 の source>` を付けて 1 回実行する（R6） | script の hash か HEAD が違う、または `deleted` 以外。再実行しない |
| E4 確認 | 実行担当 | 同じ script を `-Delete` なしでもう一度実行し、`absent` が返ることを見る（非仮想化の判定を通った上での不在）。script の SHA-256、worktree の HEAD、E2・E3・E4 の出力を §6 に書く（絶対 path は書かない） | `absent` 以外なら owner に返す |
| E5 文書 | 実行担当 | §2「決めたこと」の公開文書の書換えと、program §6・§4 の更新（本項目の完了、tombstone の記述、孤児回収の項目への一文）を行い、`pwsh tools/docs-audit.ps1` と `pwsh tools/contract-audit.ps1` を通して、文書だけの PR にする | audit の error |

script（root を上書きする引数は無い。§5 の fixture の確認は、`# --- removal core` から `# --- end of removal core ---` までをそのまま読み込み、temp の root を渡して呼ぶ。`Get-OsmStoreContext` はそのつど LocalApplicationData の直下に一意名の probe directory を作って消す。README のとおり、失敗時は空のまま残りうる）:

```powershell
param([Parameter(Mandatory)][string]$RepositoryRoot,[Parameter(Mandatory)][string]$CopyRoot,[switch]$Delete,[string]$ExpectedSourceSha256='')
# --- removal core (the fixture tests load exactly this part) ---
function Invoke-CopyRemoval([string]$LocalAppData,[string]$UserProfile,[string]$StoreContext,[string]$CopyRoot,[bool]$Delete,[string]$ExpectedSourceSha256){
  $ErrorActionPreference='Stop'
  # A virtualized view merges the copy with the real LocalAppData, so nothing here is trusted without this.
  if($StoreContext -cne 'not-virtualized'){throw 'store-context-not-verified'}
  # Only the copy root itself: <LocalApplicationData>\Packages\<one package>\LocalCache\Local\OneStarMaker.
  $copy=[IO.Path]::GetFullPath($CopyRoot).TrimEnd('\');$parts=[IO.Path]::GetRelativePath($LocalAppData,$copy).Split('\')
  if($parts.Count -ne 5 -or $parts[0] -ne 'Packages' -or $parts[1] -in @('','.','..') -or $parts[2] -ne 'LocalCache' -or $parts[3] -ne 'Local' -or $parts[4] -ne 'OneStarMaker'){throw 'copy-root-shape'}
  # The copy must be the only one of its shape, so that a path naming another package is refused rather than removed.
  $packages=[IO.Path]::Combine($LocalAppData,'Packages')
  $candidates=@([IO.Directory]::EnumerateDirectories($packages)|ForEach-Object{[IO.Path]::Combine($_,'LocalCache','Local','OneStarMaker')}|Where-Object{[IO.Directory]::Exists($_)})
  if($candidates.Count -gt 1){throw 'multiple-copies'}
  if($candidates.Count -eq 0){return [ordered]@{status='absent'}}
  if(-not [string]::Equals($candidates[0].TrimEnd('\'),$copy,[StringComparison]::OrdinalIgnoreCase)){throw 'copy-root-mismatch'}
  # Identifies the copy without naming the package: E3 must present the value E2 printed.
  $source=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($copy.ToLowerInvariant()))).ToLowerInvariant()
  if($Delete -and $ExpectedSourceSha256 -cne $source){throw 'source-mismatch'}
  for($d=[IO.DirectoryInfo]::new($copy);$null -ne $d;$d=$d.Parent){if($d.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)){throw 'reparse-present'}}
  $newRoot=[IO.Path]::Combine($UserProfile,'OneStarMaker');$oldRoot=[IO.Path]::Combine($LocalAppData,'OneStarMaker')
  function Get-Top([string]$Path){if(-not [IO.Directory]::Exists($Path)){return @()};@([IO.DirectoryInfo]::new($Path).EnumerateFileSystemInfos()|Sort-Object Name|ForEach-Object{"$($_.Name)|$(if($_.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)){'reparse'}elseif($_ -is [IO.DirectoryInfo]){'directory'}else{'file:'+$_.Length})"})}
  function Get-Roots{$m=[IO.Path]::Combine($newRoot,'store-root.json');[ordered]@{marker=$(if([IO.File]::Exists($m)){(Get-FileHash -LiteralPath $m -Algorithm SHA256).Hash.ToLowerInvariant()});newRoot=@(Get-Top $newRoot);oldRoot=@(Get-Top $oldRoot)}}
  # Walk without following links and refuse any reparse point, so that nothing outside the copy can be reached.
  $files=0;$bytes=[long]0;$dirs=0;$readOnly=[Collections.Generic.List[IO.FileInfo]]::new();$stack=[Collections.Generic.Stack[string]]::new();$stack.Push($copy)
  while($stack.Count){foreach($i in [IO.DirectoryInfo]::new($stack.Pop()).EnumerateFileSystemInfos()){
    if($i.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)){throw 'reparse-present'}
    if($i -is [IO.DirectoryInfo]){$dirs++;$stack.Push($i.FullName)}else{$files++;$bytes+=$i.Length;if($i.Attributes.HasFlag([IO.FileAttributes]::ReadOnly)){$readOnly.Add($i)}}}}
  $before=Get-Roots;$top=@(Get-Top $copy)
  if(-not $Delete){return [ordered]@{status='checked';source=$source;top=$top;files=$files;directories=$dirs;bytes=$bytes;roots=$before}}
  foreach($f in $readOnly){$f.Attributes=$f.Attributes -band (-bnot [IO.FileAttributes]::ReadOnly)}
  [IO.Directory]::Delete($copy,$true)
  $after=Get-Roots
  $same=(($before|ConvertTo-Json -Compress -Depth 5) -ceq ($after|ConvertTo-Json -Compress -Depth 5))
  $status=if([IO.Directory]::Exists($copy)){'still-present'}elseif(-not $same){'roots-changed'}else{'deleted'}
  [ordered]@{status=$status;source=$source;top=$top;files=$files;directories=$dirs;bytes=$bytes;roots=$after}
}
# Every failure becomes a fixed code: .NET and PowerShell messages can carry the full path and the package name.
function Invoke-CopyRemovalSafely([string]$LocalAppData,[string]$UserProfile,[string]$StoreContext,[string]$CopyRoot,[bool]$Delete,[string]$ExpectedSourceSha256){
  try{$r=Invoke-CopyRemoval $LocalAppData $UserProfile $StoreContext $CopyRoot $Delete $ExpectedSourceSha256}
  catch{$m=[string]$_.Exception.Message;$code=if($m -cin @('store-context-not-verified','copy-root-shape','multiple-copies','copy-root-mismatch','source-mismatch','reparse-present')){$m}else{'removal-failed'};$r=[ordered]@{status='failed';reasonCode=$code}}
  $r
}
# --- end of removal core ---
$ErrorActionPreference='Stop'
try{
  Import-Module ([IO.Path]::Combine($RepositoryRoot,'tools','Workflow','WindowsStoreContext.psm1')) -Force
  $context=Get-OsmStoreContext
}catch{$context='unknown'}
$result=Invoke-CopyRemovalSafely ([Environment]::GetFolderPath('LocalApplicationData')) ([Environment]::GetFolderPath('UserProfile')) $context $CopyRoot $Delete.IsPresent $ExpectedSourceSha256
$result|ConvertTo-Json -Compress -Depth 5
if($result.status -notin @('checked','deleted','absent')){exit 1}
```

## 5. テストとレビュー計画

- 単体の確認（2026-10-20 に R5 の反映後の script で実施済み。temp の fixture、非仮想化 pwsh、11 case）: 確認モード → `checked`。違う `source` → `source-mismatch`（残る）。`-Delete` → `deleted`（copy root が消え、兄弟の file・新 root の印・旧 root の tombstone が残る。読取り専用の file も消えた）。もう一度 → `absent`。別の package の path → `copy-root-mismatch`、copy が 2 つ → `multiple-copies`（どちらも残る）。context が `virtualized` / `unknown` → `store-context-not-verified`。copy root の中に junction → `reparse-present`（copy root も link の先も残る）。使用中の file → `removal-failed`。旧 root を渡す → `copy-root-shape`。どの出力にも fixture の path と package 名は出なかった。
- 実 copy での予行（2026-10-20。入口を通した起動、`-Delete` なし）: 非仮想化の判定を通り、`checked`、file 15,035・directory 2,051・1,714,521,154 bytes、約 2 秒。`-Delete` に違う `source` を渡した起動は `source-mismatch` で拒否され、copy は残った。出力に絶対 path と package 名は無かった。
- 実操作: E1〜E4（AC-5）。
- 機械検査: E5 の PR で `pwsh tools/docs-audit.ps1` と `pwsh tools/contract-audit.ps1`。
- 全 EditMode 回帰・Unity テスト: 適用しない（`unity/` も `tools/` も変えない）。
- Phase B〜C' の重い手順と通常の task の開始・終了（`workflow-task.ps1`）を使わない理由（owner の今回限りの例外。一般化しない。R5 補足）: コードの変更が無く、対象は owner が全部捨てると決めた開発用の残置で、守るのは「copy root の外を消さない」ことだけ。それは script の形の検査・reparse の拒否・前後の直下の比較と、fixture の確認で足りる。evidence bundle・照合 script・C' の盲検監査は作らない。計画のレビューは owner が依頼する外部レビューで行い、E5 の文書の PR は通常の PR レビューに掛ける。
- Web 版レビュー R5（2026-10-20。方針転換後の版が対象。判定「方針は支持。凍結・実削除の前に 3 点を局所修正」）: 3 点とも採用した。R5-1 削除対象の取り違え（同じ形の別 package、テスト用の root 上書き引数）→ I5、`source` の一致、入口から上書き引数を撤去、AC-6。R5-2 非仮想化の未確認 → I6、E4 も script で `absent` を確かめる、AC-7。R5-3 失敗時の出力の path → I4、全ての失敗を固定 code に、AC-8。補足（risk の書き方、文書に「Codex で実証済み」と書かない、手順の例外を今回限りと明記）も反映した。
- Web 版レビュー R6（2026-10-20。判定「A3 凍結を支持。凍結の前に 1 行の契約の整合を推奨」）: GO の定義を I1〜I6 に直した。実行時の注意として、E3 の直前に script の hash と worktree の HEAD を照合すること、E4 の `absent` を E3 の `deleted` と組で判定することを本文に足した。probe directory は削除の対象を広げる根拠にしない（§4 の注記のとおり）。
- 進行役の見直し（C / C' の視点。2026-10-20）: (1) 実行担当が本文から書き出した script が凍結版と同じであることを示せない → E2 で file の SHA-256 と worktree の HEAD を記録する。(2) 一部だけ消えた後の扱いが無い → 再実行せず owner に返し、続けるなら E2 から。(3) E3 で予期しない `absent` → 止まる条件に足した。(4) 記録の置き場 → §6 に本文で残し、本書を Phase D で削除した後は E5 の PR の本文に件数と結果を要約する。
- 経緯（詳細は Git 履歴）: A1（commit `ce258f34`）では一回限りの program・R2 の key の分類・網羅性・観測 script・判定 C の照合 script を持つ計画だった。A2 の 3 件、統合後レビュー、Web 版レビュー R3・R4 を経て厳密さを足すほど重くなり、R4 で照合 script の実行環境が規約を満たせない（同じ PC では隔離できない）ことが残った。owner は 2026-10-20 に「過去の遺物の為の厳密性のために重くするのは否定したい」として全部削除を決め、本書をこの形に書き直した。

## 6. 実行の記録

実行担当: 本 session（Claude Code の PowerShell tool、非仮想化 pwsh）。凍結版の HEAD `2a08883ad2ab283520818e45123832da5ced8ca6`、作業ツリーは clean。script は凍結版の本書から pwsh で scratch に書き出し、SHA-256 は `1e292ce75404eef74aedb4f9deb4feef221be0fde5043770a043ca8c86090438`（5,287 bytes）。copy root の兄弟は 2 件で、前後とも残った。

- **E1（owner、2026-10-20）:** 「Codex を終了した」。E2・E3 の直前に Codex の process が 0 件であることも確かめた。
- **E2（2026-10-19 16:58 UTC、`-Delete` なし）:** exit 0。`{"status":"checked","source":"6cf57e314851a9dc490709874a21e49ff97081f663e7746cc38a8066333bd5e5","top":[".virtualization-probe-20261012-98d039acb197|directory","Artifacts|directory","H2a Review|directory","H2b Review|directory","Harness|directory","RevisionLocks|directory","Workflow|directory","WorktreeRecords|directory"],"files":15035,"directories":2051,"bytes":1714521154,"roots":{"marker":"54b6caeb97abff6c9b32a35d12a41c9ffa4a0765e8ed07ac746e3ea7e562c3f2","newRoot":["Artifacts|directory","store-root.json|file:150","Workflow|directory"],"oldRoot":["Artifacts|file:0","Harness|file:0","RevisionLocks|directory","Workflow|file:0"]}}`
- **owner の承認（2026-10-20）:** 「E2 の出力を確認した。消してよい。E3 を実行して。」
- **E3（2026-10-19 17:02 UTC）:** 直前の照合は script の SHA-256 一致・HEAD 一致・作業ツリー clean・Codex の process 0 件。`-Delete -ExpectedSourceSha256 6cf57e31…d5e5` で 1 回実行。exit 0、約 6.4 秒。`{"status":"deleted", …}`（`source`・`top`・件数は E2 と同じ。`roots` は E2 と同じで、新 root の印の hash・直下、旧 root の tombstone 3 つ（size 0）と `RevisionLocks` が変わらなかった）。
- **E4（2026-10-19 17:03 UTC、`-Delete` なし）:** exit 0。`{"status":"absent"}`（非仮想化の判定を通った上で、copy root の形を持つ directory が `Packages` の配下に 1 つも無い）。E3 の `deleted` と組で S1・S2 が成立した。
- 判定: I1〜I6 に違反なし、S1・S2 が成立。S3（E5 の公開文書）は未着手。
- 実行時刻は UTC（日本時間では 2026-10-20 の 01:58〜02:03）。

## 7. Phase C

なし

## 8. Phase C'

なし

## 9. Phase D

未着手（E5 の PR の merge の前に、§6 の要約を PR の本文に写し、harvest の後に本書を削除する）
