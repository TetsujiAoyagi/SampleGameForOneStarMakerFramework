# Artifact Storage / Evidence 清掃 scheduler の identity 切替（README の切替手順と CLI の失敗 reasonCode）

`BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §4 A（Evidence の通常利用と期限清掃）の完了後に見つかった基盤不備 2 件を直す小スライス。PR #128 のローカル固定の導入手順（`schedule status` → 新 head で `schedule install`）が、登録済みの scheduled task を既存の `install` では直せないケースを書いていなかったこと、`tools/artifacts.ps1` が scheduler の既知の失敗理由を `Artifact operation failed.` に伏せていたことを扱う。

## 0. メタデータ

- type: `slice`
- status: `C`（発見 C 待ち。Phase B は 2026-10-16 に完了、結果は §6。動作確認は対象なし。Phase A revision 1 を 2026-10-16 に A3 で凍結。凍結内容は §2〜§5。設計判断を変えるときは Phase A を新しい revision として再開する）
- branch: `claude/serene-varahamihira-2b2442`（PR の base は `develop`）
- implementation base commit: `d911e4b4b662e66fd3fcc54f6b2586c381fabf4a`（`origin/develop`、PR #128 マージ後）
- implementation head commit: `911addb5eb11c019e333244d1d721a1014a6d85d`（B 完了 head。HANDOFF の記録 commit は含めない）
- risk: `low`（CLI の失敗表示の経路と README だけを変える。scheduler の登録・修復・削除の契約、runtime manifest、exit の意味、成功・pending の結果の形は変えない）
- owner: OSM maintainers
- created: 2026-10-16
- expires: 2026-11-16 または置換 revision
- harvest to: 操作と切替手順は `tools/Artifacts/README.md` の「自動清掃」（本スライスの実装対象そのもの）。`BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §4 A の後続入力の一覧から「schedule-install・schedule-status の失敗 reasonCode」を既定の扱いへ移し、config 失敗の reasonCode だけを残す。
- Phase A snapshot の取得案内・状態: 未保存（凍結版は本 HANDOFF の A3 凍結 commit。内容 sha256 は引継ぎ票）。
- Phase B result snapshot の取得案内・状態: 未生成。
- evidence bundle の取得案内・状態: 未生成。
- C' blind bundle の取得案内・状態: 未生成。
- 引継ぎ票: 次の Phase は **発見 C（欠陥発見レビュー）**、役割はレビュー担当（Phase B と異なるモデル、新規 session）。動作確認は対象なし（§5 の検証経路に操作・実行時・目視の条件が無い）。作業は implementation base `d911e4b4b662e66fd3fcc54f6b2586c381fabf4a` から B 完了 head `911addb5eb11c019e333244d1d721a1014a6d85d` までの差分を、§2〜§5（凍結 revision 1）と §6 の「HANDOFF との差」に照らして構造・契約・凍結済み失敗経路（I1〜I3、AC1〜AC6）の順に見ること。入力は commit で固定済みで、stage snapshot は使っていない。開始時に自分の worktree の HEAD（固定 SHA からの detached worktree でよい）をこの SHA と照合し、作業ツリーが dirty なら始めない。同一 PC ではローカルの SHA で照合する。必要な権限: ローカルの PowerShell 7（Store 版でよい）と git。判定 C の全 suite には 3 project の Release build と PATH 上の `rg` が要る。Unity・sandbox 外実行・資格情報・network は不要。実ストアの catalog・資格情報は読まず、`schedule-install` を実ストアに対して実行しない。起点 filter は §5。GO 判定、判定 C の実行、C' の起動は担わない。引き渡さないもの: なし。凍結した Phase A は revision 1（commit `c5077e3f`、内容 sha256 `958e5787f910b9bbbc76eca2fb7ce42a2c2b004755c96fb92b14e3d1e5615a5f`）。Workflow task は `evidence-schedule-cutover`（version 1、started）。

## 1. 目的と対象外

- 目的: 登録済みの Evidence 清掃 task の identity（pwsh の実体 path、runtime の head、config hash）が現在の環境と食い違ったとき、owner が CLI の出力と README だけから原因を特定し、既存の `install`（既存 task を修復しない設計）のまま復旧できるようにする。
- 対象外: `install` に既存 task の修復・上書き・削除・有効化を足すこと。`EvidenceSchedule.psm1` の契約・識別子・manifest の変更。`cleanup` / `schedule-*` の config 読取失敗（`invalid-evidence-config` / `unsupported-schema` / `unknown-deployment`）の reasonCode 化（program §4 A の後続入力のまま。結果の形と exit の意味を変える判断が要る）。`publish` 等の他コマンドの catch の変更。日次 job（`EvidenceCleanupJob.ps1`）の出力の変更。Task Scheduler の task を CLI から削除する機能。
- 現況:
  - `tools/Artifacts/EvidenceSchedule.psm1` の `Invoke-EvidenceScheduleInstall` は、同名の task が既に登録されていれば OS の export と期待 identity を `Assert-EvidenceSchedulerRegistration` で照合し、一致すれば `unchanged`、不一致なら `scheduler-identity-mismatch` を throw して何も書き換えない（コメント「neither public command enables or repairs an existing task」）。期待 identity（`Get-EvidenceScheduleIdentity`）は、現在の process の pwsh の実体 path、runtime `<root>/<head>/` の実体 path、`runtime.json` の hash から毎回計算する。
  - `Get-EvidenceScheduleStatus` は descriptor `<repositoryId>.schedule.json` の値を同じ期待 identity と照合し、不一致で `scheduler-identity-mismatch` を throw する。OS の task の照合はその後。
  - したがって identity が変わる事象は 3 つある。(a) 新しい implementation head で `install` する（arguments の runtime path と hash が変わる。既存 task と不一致）。(b) Store 版 pwsh の更新（実体 path `WindowsApps\Microsoft.PowerShell_<版>_x64_...\pwsh.exe` に版が入る。旧 path は消え、登録済み task の `execute` が無効になる）。(c) config の変更（status は `runtime-config-mismatch`、同じ head の install は `runtime-identity-mismatch`）。
  - 2026-10-16 の実事象: Store 版 pwsh が 7.6.6 → 7.6.7 に更新され、登録済み task の `execute` が消えた旧 path を指し、日次 job が `0x80070002` で失敗した。`schedule status` と `schedule install` は `scheduler-identity-mismatch` で止まったが、`tools/artifacts.ps1` は `Artifact operation failed.` だけを出した。owner が `Unregister-ScheduledTask` で旧 task を削除してから `develop` head で `install` をやり直して復旧した。
  - `tools/artifacts.ps1` の catch は `unsupported-command` だけを stdout の `{"status":"failed","reasonCode":"unsupported-command"}` にし、それ以外は stderr に `Artifact operation failed.` を出して exit 1 にする（例外本文・stack に秘密や path が混ざり得るため）。
  - `tools/Artifacts/README.md` の「自動清掃」は、「ローカル固定の導入手順」（`schedule status` → 新 head で `schedule install`）と「同名 task の identity 不一致、runtime/config 欠落/hash 不一致では上書き・削除をせず failed です」を書いているが、既存 task が別 identity のとき owner が何をすべきかを書いていない。
  - `EvidenceSchedule.Tests.ps1` は module 境界では `Reject` で throw を検査し、CLI 境界は `scheduler-missing-cli`（pending、exit 2）の 1 case だけ。失敗の表示を CLI 境界で検査する case は無い。

## 2. 意思決定と受け入れ境界（revision 1。2026-10-16 に A3 で凍結済み）

- このスライスが答える問い: 登録済み task の identity が現環境と食い違ったとき、owner が `schedule status` / `schedule install` の stdout と README だけで原因（identity 不一致、未登録、無効化、config 不一致、runtime 未 build）を特定し、既存の `install` の設計のまま復旧手順を実行できるか。
- 進める最低条件:
  - 不変条件:
    - I1: `schedule install` / `schedule status` は、既存の task を書き換え・削除・有効化しない（既存契約。変更しない）。
    - I2: CLI の失敗出力に endpoint、path、SID、例外本文、stack trace を含めない。allow-list（§3）に無い例外は従来どおり stderr の `Artifact operation failed.` だけ。
    - I3: exit の意味（0 / 1 / 2）と、成功・pending の結果の形を変えない。`schedule status` の `scheduler-not-installed`（`status: pending`、exit 2）はそのまま。
  - 成立条件:
    - S1: `evidence schedule install` / `evidence schedule status` が allow-list の reason を throw して失敗したとき、stdout に `{"status":"failed","reasonCode":"<code>"}` を 1 行出し exit 1。stderr に `Artifact operation failed.` を出さない。
    - S2: README の「自動清掃」に、既存 task が別 identity のときの切替手順がある。起きる条件（新 head での install、pwsh 更新、config 変更）、`install` が既存 task を直さないこと、owner が旧 task を削除してから `install` すること、削除の対象（task 名と folder）、表示される reasonCode の意味。
- 受け入れ条件（検証する条件を併記）:
  - AC1（S1、I2、I3）: `EvidenceSchedule.Tests.ps1` の新 case `scheduler-cli-identity-status`。install 後に descriptor の `execute` を別の値に書き換え、子 pwsh で `tools/artifacts.ps1 evidence schedule status` を実行する。exit 1、stdout が `{"status":"failed","reasonCode":"scheduler-identity-mismatch"}`、stderr に `Artifact operation failed.` が無い。
  - AC2（S1、I1）: 新 case `scheduler-cli-identity-install`。SchedulerHook が `scheduler-identity-mismatch` を throw する状態で子 pwsh から `schedule install` を実行する。exit 1、stdout が同じ形の JSON、install の呼び出しが 0 回。
  - AC3（I2、失敗経路）: 新 case `scheduler-cli-internal-hidden`。SchedulerHook が allow-list に無い文字列（偽の絶対 path を含む）を throw する状態で `schedule status` を実行する。exit 1、stdout に `reasonCode` が無く、stderr が `Artifact operation failed.`、stdout と stderr のどちらにもその path が無い。
  - AC4（I3）: 既存 case `scheduler-missing-cli` がそのまま通る（pending、exit 2）。
  - AC5（S2）: README の「自動清掃」に、§4 の文言要素を全て含む段落がある。`pwsh tools/docs-audit.ps1` がエラー 0。
  - AC6（I1、常時契約）: `EvidenceSchedule.psm1` に差分が無い（`git diff --stat` で確認）。`pwsh tools/contract-audit.ps1` がエラー 0。
- ここでは答えない問いと所有する後続スライス: config 読取失敗の reasonCode と exit の扱い（program §4 A の後続入力のまま）。`install` が旧 head の runtime directory を残すことの清掃（同上「診断の改善」）。CLI からの task 削除（owner 操作のまま。所有スライス未定）。
- 判定定義: GO は AC1〜AC6 が全て成立。NO-GO はいずれかの不成立。CONDITIONAL ACCEPT は定義しない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし。
- 決定記録: なし。
- 本文へ転記した実装制約:
  - allow-list は `tools/artifacts.ps1` の schedule 分岐でだけ設定する固定の文字列集合とし、`EvidenceSchedule.psm1` が throw する固定 code のうち README に意味を書くものに限る: `scheduler-identity-mismatch`、`scheduler-disabled`、`scheduler-not-installed`、`scheduler-query-failed`、`scheduler-runtime-missing`、`runtime-source-uncommitted`、`runtime-identity-mismatch`、`runtime-hash-mismatch`、`runtime-config-mismatch`。例外 message と allow-list の照合は `-cin`（大文字小文字を区別）で行い、部分一致・正規表現は使わない。
  - allow-list の変数は try の先頭（store context の判定の直後）で空に初期化し、schedule 分岐でだけ値を入れる。catch で参照できない状態を作らない。
  - 既存の `unsupported-command` の扱いは変えない。
  - 子 pwsh を使う test case は、既存 `scheduler-missing-cli` と同じく、module の TestRoot・SchedulerHook・BindingRootsHook・DeploymentRoot を子 process 内で設定してから `tools/artifacts.ps1` を呼ぶ。実ストア・資格情報・network に触れない。子 script の生成は 1 つの helper にまとめ、`scheduler-missing-cli` も同じ helper を使う（挙動は変えない）。
  - test の待機に `Thread.Sleep` / `Task.Delay` を使わない（常時契約）。
- 未決事項: なし。

## 3. 責務マップ

| file | 責務 | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 現在行数 / 予想増分 |
|---|---|---|---|---|---|---|---|
| `tools/artifacts.ps1` | Artifact CLI の grammar と結果の表示。失敗時に非秘密 reasonCode だけを出す | schedule 分岐の既知 reason を stdout の JSON で返す | OSM maintainers・恒久 | `EvidenceSchedule.psm1` の throw する固定 code | CLI の stdout/stderr/exit | `EvidenceSchedule.Tests.ps1` の子 pwsh case | 177 / +6 前後 |
| `tools/Artifacts/README.md` | Artifact Storage の公開面。操作・状態・制約 | 「自動清掃」に identity 切替の手順と reasonCode の意味を書く | OSM maintainers・恒久 | なし | 公開文書 | `docs-audit` | 415 / +6 前後 |
| `tools/Artifacts/tests/EvidenceSchedule.Tests.ps1` | scheduler module と CLI 境界の offline 検査 | 子 pwsh の helper 化と AC1〜AC3 の case | OSM maintainers・恒久 | `artifacts.ps1`、`EvidenceSchedule.psm1` | なし | 自身 | 約 480 / +45 前後 |

`EvidenceSchedule.psm1` は変更しない（新しい code を足さず、既存の throw をそのまま CLI で表示する）。500 行・3 責務・50% 増の警報に当たる file は無い。新しい中核ロジックは無く、単体テストは CLI 境界の子 pwsh case で足りる。

## 4. 実装計画

- 変更対象: §3 の 3 file。
- 順序:
  1. `pwsh tools/workflow-task.ps1 start -Task evidence-schedule-cutover` で task を開始する（Workflow の記録だけで、実ストアの Evidence には触れない）。
  2. `tools/artifacts.ps1`: try の先頭で `$publicFailures=@()` を置き、schedule 分岐（`$action -ceq 'schedule-install'` / `'schedule-status'` に入った後）で §2 の allow-list を代入する。catch で `$_.Exception.Message -cin $publicFailures` なら stdout に `{"status":"failed","reasonCode":"<message>"}` を出す（`ConvertTo-Json -Compress` で生成し、文字列を手で連結しない）。それ以外は従来どおり。理由をコメントに残す（既存 task を直さない設計のため、owner が原因を読めなければ復旧できないこと。allow-list 外を伏せる理由は既存コメントのまま）。
  3. `EvidenceSchedule.Tests.ps1`: 子 pwsh を起動する helper `Invoke-ScheduleCli`（引数: fixture、CLI の action、子 process 内で SchedulerHook に設定する scriptblock の文字列）を作り、`scheduler-missing-cli` をこの helper に載せ替える。AC1〜AC3 の case を `$cases` に登録して実装する。stdout と stderr は分けて取る（`2>` でファイルへ、または `Start-Process` の `-RedirectStandardError`）。
  4. README の「自動清掃」: 「ローカル固定の導入手順」の段落の直後に「登録済み task の切替」の段落を足す。文言要素: (1) identity が変わる 3 条件（新 head の install、pwsh 更新で実体 path が変わる、config 変更）、(2) `install` は既存 task を修復・上書きせず `scheduler-identity-mismatch` で止まること、日次 job は旧 path の pwsh を起動できず `0x80070002` で失敗すること、(3) 手順: owner が `Unregister-ScheduledTask -TaskPath '\OneStarMaker\' -TaskName 'OSM-Evidence-Cleanup-<repositoryId先頭12>'` で旧 task を削除し、同じ Windows ユーザーの pwsh から新 head で `schedule install` を実行し、`schedule status` で `passed` を確認する、(4) CLI から task を削除する機能は無いこと、旧 head の runtime directory は残ること、(5) `schedule install` / `schedule status` の失敗が stdout の `{"status":"failed","reasonCode":...}` で返る code の一覧と意味（§2 の 9 件）、それ以外は従来どおり `Artifact operation failed.` であること、config 読取失敗は従来どおりであること。
  5. 出口: `pwsh tools/Artifacts/tests/EvidenceSchedule.Tests.ps1`（全 case）、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1` を実行し、§6 に実装結果を書き、引継ぎ票を発見 C 向けに更新して commit する。
- Phase B から Phase A へ差し戻す条件: allow-list に §2 以外の code を足す必要が出た。`EvidenceSchedule.psm1` の変更が必要になった。CLI の結果の形または exit の意味を変える必要が出た。
- 対象外を維持する方法: `EvidenceSchedule.psm1` と他コマンドの分岐に触れない。`git diff --stat` を §6 に記録する。

## 5. テストとレビュー計画

- 単体テスト: `EvidenceSchedule.Tests.ps1` の `scheduler-cli-identity-status`、`scheduler-cli-identity-install`、`scheduler-cli-internal-hidden`（新規）、`scheduler-missing-cli`（既存）。
- 差し戻し中の起点 `-Filter`: `pwsh tools/Artifacts/tests/EvidenceSchedule.Tests.ps1 -Case scheduler-missing-cli,scheduler-cli-identity-status,scheduler-cli-identity-install,scheduler-cli-internal-hidden`。C は凍結済み条件の確認に必要なら根拠を記録して変えてよい。
- 判定必須テスト（GO 候補 head）: `EvidenceSchedule.Tests.ps1` の全 case、`tools/Artifacts/tests/*.Tests.ps1` の全体（`artifacts.ps1` の catch は全コマンドが通るため。3 project の Release build と PATH 上の `rg` が要る）、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、`tools/artifacts.ps1` と `EvidenceSchedule.Tests.ps1` の parse（`[Management.Automation.Language.Parser]::ParseFile`）。
- 全 EditMode 回帰の適用除外: Unity 側コードを変更しないため。代替証拠は上記 offline suite と audit。
- 統合・Unity テスト: なし。
- 操作・実行時・目視条件の検証経路: なし（CLI の表示は子 pwsh の offline case で観測できる。実 Task Scheduler・実 pwsh 更新の再現は求めない）。
- 未知の操作経路の疎通結果: なし。
- 人間の判断が必要な条件: なし。
- 高コスト証拠: なし。
- 判定 C の照合 script の方式: なし（結果は test の PASS/FAIL 行と audit の exit で読める）。
- 機械検査: `contract-audit`、`docs-audit`、`git diff --stat`（`EvidenceSchedule.psm1` に差分が無いこと）。
- A0/A1 主担当・モデル・ベンダー: Claude（この session）。
- A2 独立レビュー: 省略。規模が小さく（3 file、約 60 行）、依頼元 session（owner の代理）が「revision 1 で A3 まで進めてよい」と指示した。独立の目は発見 C と C' が初見で担う。
- A3 統合担当・モデル・採否: 主担当（Claude）。2026-10-16 に凍結。人間の裁定は依頼元の指示（README に切替手順を書く。CLI の reasonCode 公開は A1 で検討し採るなら実装とテストを足す）に従い、reasonCode 公開を採用した。
- C' 用に予約した担当・モデル・ベンダー: Grok 系（cursor-agent）または人間。Phase A では未使用。
- 独立性の強化条件を満たせない場合の理由: A2 を省いたため、Phase A の独立レビューは無い（`独立性制約あり`）。残存リスクは、allow-list の選定と README の文言が主担当の読みだけで決まっていること。発見 C と C' で見る。

## 6. Phase B 実装結果

- 実装: §4 の順序どおり。(1) `pwsh tools/workflow-task.ps1 start -Task evidence-schedule-cutover`（version 1、`started`）。(2) `tools/artifacts.ps1`: try の先頭で `$publicFailures=@()`、`schedule-install` / `schedule-status` の分岐に入った後（`EvidenceApplication.psm1` の import 直後）で §2 の 9 件を代入、catch で `-cin` 一致なら `ConvertTo-Json -Compress` の `{"status":"failed","reasonCode":...}` を stdout へ。理由をコメントに残した。(3) `EvidenceSchedule.Tests.ps1`: helper `Invoke-ScheduleCli`（子 script の生成、hook の埋込み、install 用の入力 JSON、stdout と stderr の分離、`json` = stdout の `{` で始まる行）。`scheduler-missing-cli` を helper に載せ替え（検査内容は同じ）、`scheduler-cli-identity-status` / `scheduler-cli-identity-install` / `scheduler-cli-internal-hidden` を追加。(4) README「自動清掃」に「登録済みtaskの切替」の段落と code 一覧の段落を追加。`git diff --stat`（base..head）: `tools/Artifacts/README.md` +4、`tools/Artifacts/tests/EvidenceSchedule.Tests.ps1` +63/−18、`tools/artifacts.ps1` +10/−1。`EvidenceSchedule.psm1` に差分なし。
- HANDOFF との差: (a) 子 pwsh の stdout/stderr 分離は `Start-Process` ではなく `& pwsh ... 2><file>` と `$LASTEXITCODE` で行った（§4 が許す 2 案の前者。`Start-Process -ArgumentList` は空白を含む path の引用が不確かなため）。(b) 子 pwsh の stderr には module import の警告（未承認の動詞）が混ざる。AC1・AC2 の「stderr に `Artifact operation failed.` が無い」は `-notmatch` で、stdout の JSON の一致は `{` で始まる行が 1 行だけでその行が完全一致、で検査した。(c) 新 case の有効性の確認として、`artifacts.ps1` を base の版へ一時的に戻して同じ 4 case を実行し、`scheduler-cli-identity-status` と `scheduler-cli-identity-install` が FAIL、`scheduler-cli-internal-hidden` と `scheduler-missing-cli` が PASS（旧挙動と同じ）になることを見た。その後 file を戻し、head の diff は上記のとおり。
- 未実行: `tools/Artifacts/tests/*.Tests.ps1` の全体（`EvidenceSchedule.Tests.ps1` 以外。3 project の Release build と `rg` がこの worktree に無い。判定 C で実行する）。実 Task Scheduler・実 pwsh 更新の再現（§5 で求めない）。
- B 出口の実行結果: `pwsh tools/Artifacts/tests/EvidenceSchedule.Tests.ps1`（全 case）: registered 81 / selected 81 / executed 81 / failed 0。`pwsh tools/contract-audit.ps1`: errors 0（検査1 の差分 0 file、検査8 not-applicable）。`pwsh tools/docs-audit.ps1`: errors 0、warnings 0。`tools/artifacts.ps1` と `EvidenceSchedule.Tests.ps1` の parse errors 0。
- implementation head commit: `911addb5eb11c019e333244d1d721a1014a6d85d`。
- Phase B 担当・モデル・ベンダー: Claude（Phase A と同じ session）。

### 動作確認

- 対象 head: `911addb5eb11c019e333244d1d721a1014a6d85d`。
- 条件ごとの実行担当・環境・実行したコマンド: 対象なし。
- 生の観測と保存先・状態: 対象なし。
- 検証経路の期待と違う観測と、B への差し戻し: なし。
- `未実施` の条件と理由・担当: 対象なし（§5 に操作・実行時・目視の条件が無い）。

## 7. Phase C

- 種別:
- 使用した evidence bundle の取得案内・状態:
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
- 使用した blind audit bundle の取得案内・状態:
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
- 必要な Evidence の保存・取得・必須内容の閲覧確認:
- 取得案内の引渡し確認:
- harvest:
- 削除確認:
- 通常の task 終了・後始末の状態と未完了事項:
