# Evidence CLI の path 診断

## 0. メタデータ

- type: `slice`
- status: `B`（Phase A revision 1 を A3 で凍結済み）
- branch: `codex/artifacts-cli-path-diagnostics`
- implementation base commit: `7b5b3002308eaeb330b4e549df941169ad06be36`（`develop`。PR #120 のマージを含む。A3 凍結時に `origin/develop` と一致を確認済み）
- implementation head commit:
- risk: `normal`（公開 API・asmdef・Unity は変えない。保存・削除の判定は変えず、失敗時の reasonCode だけを変える。fail-closed の向きは保つ）
- owner: OSM maintainers
- created: 2026-10-12
- expires: 2026-11-09 または置換 revision
- harvest to: `tools/Artifacts/README.md`（Evidence v2 の通常操作と exit／reasonCode の段落）。完了時に本書を削除する。
- Phase A snapshot の取得案内・状態: revision 1 は本書の凍結 commit。判定用 evidence bundle に同梱して保存する（取得案内は bundle と共通。現時点は未保存）
- Phase B result snapshot の取得案内・状態:
- evidence bundle の取得案内・状態:
- C' blind bundle の取得案内・状態:
- 引継ぎ票: 次は Phase B（実装）。Phase A revision 1、implementation base は上記。§2〜§5 の範囲だけを実装し、offline の起点 test・contract-audit・docs-audit を実行して §6 を書き、push する。必要な権限: なし（資格情報・Artifact Storage・Unity・sandbox 外は不要）。停止規則: §4 の差し戻し条件に当たれば止めて Phase A に戻す。引き渡さないもの: なし

管理方式は tracked HANDOFF（U3、A3 で決定）。本スライスは PR #120 で改訂した `osm-workflow` の規則で進める初めてのスライスで、§2 は不変条件と成立条件を先に書く形式に従う。

## 1. 目的と対象外（A0 planning packet）

### 目的

`tools/artifacts.ps1 evidence ...` に使えない `--config`／`--selection` を渡したとき、結果 JSON だけで原因の種類が分かるようにする。秘密・path・例外本文は出さない。

### このスライスが答える問い

Evidence 系コマンドが、使えない config／selection の指定を、ネットワークにも資格情報にも触れずに、原因の種類を示す reasonCode で拒否できるか。

### 現況（2026-10-12 に観測した事実）

- **F1:** PR #120 の作業で `evidence publish` に相対 path の `--selection` を渡したところ、`{"status":"failed","reasonCode":"unconfirmed","residue":{"remote":"pre-put"}}` だけが返った。原因の特定に、モジュールスコープの `FailureHook` を差し込む診断 script が必要だった（約 15 分）。
- **F2:** 原因は `ArtifactCommands.psm1` の `Read-ArtifactJson` で、path が fully qualified でない、file が無い、1 byte 未満、1 MiB 超、reparse、UTF-8・JSON 不正、重複 key のいずれでも `'Artifact JSON unavailable.'` を throw する。`Invoke-EvidencePublish` の catch は既知の code（`unsupported-schema`、`unknown-deployment`、`resume-required`、`invalid-selection`、`invalid-source` 等）だけを reasonCode に写し、それ以外は既定の `unconfirmed` のまま返す。fetch・inspect・use・release も同じ形で、`Read-EvidenceConfig` の失敗は `unconfirmed` になる。
- **F3:** Build と Content は同じ状況を区別している。`Read-BuildConfig` は相対・非正規 path を `invalid-build-config`、`Read-ContentConfig` は `invalid-content-config` で拒否し、各 Application の catch がそれを reasonCode に写す。Evidence だけが揃っていない。
- **F4:** CLI 入口（`artifacts.ps1`）は引数の文法違反だけを `unsupported-command` として返し、それ以外の例外は「例外文字列に秘密や path が混ざり得る」ため一般診断にする方針である。reasonCode は非秘密の固定語彙なので、この方針と両立する。
- **F5:** 相対 path を受け付けないこと自体は、Build／Content も同じ。カレントディレクトリが違う別のエージェント（codex、subagent 等）から呼ぶ運用では、絶対 path を必須にするほうが別 file の誤読が起きにくい。

### 要求

- Evidence 系コマンドで、config の読取・検証の失敗と、selection の読取の失敗を、それぞれ固定の reasonCode で返す。
- 既存の reasonCode・exit code・保存／削除の判定は変えない。

### 対象外

- 相対 path の受け付け（U1。推奨は対象外のまま）。
- Build／Content の reasonCode（既に区別している）。
- 例外本文・path の表示、詳細な診断モード（`FailureHook` の公開等）。
- `Read-ArtifactJson` の共通の振る舞い（Build／Content・Workflow 等も使う）。
- Credential 系コマンド。

### 常時契約のうち本スライスに効くもの

- Artifact Storage の秘密の非露出。鍵・store・header・path を会話・引数・ログ・出力に出さない（Artifacts README）。
- Unity テスト・Build は無関係（`unity/` を変えない）。
- テストで `Task.Delay`／`Thread.Sleep` を使わない。
- PR の base は `develop`。

### 未決事項（A3 で決定済み。決定は §5 の A3 欄）

- **U1 相対 path:** (a) 受け付けない（推奨）。(b) CLI 入口で `GetFullPath` によって絶対 path に解決してから渡す。b はカレントディレクトリへの依存を持ち込み、F5 の誤読の経路を開く。Build／Content とも揃わなくなる。
- **U2 対象コマンド:** (a) publish・fetch・inspect・use・release（`EvidenceApplication.psm1`）だけ。(b) a に cleanup・schedule-install・schedule-status を加える（推奨。同じ `--config` を受けるので、利用者から見て揃う）。
- **U3 管理方式:** tracked HANDOFF（推奨）か、H1 の `artifact-evidence-lifecycle` の固定 offline 集合に載せるか。後者は B exit の offline 実行が必須になり、本スライスの規模に対して重い。
- **U4 deployment receipt:** config 本体が読めても、対応する `deployments/<deploymentId>.json` が無い・読めない場合は、現状 `unconfirmed` になる。`unknown-deployment`（既存 code）に写すか。推奨は写す（同じ読取経路で、既存語彙で足りる）。

## 2. 意思決定と受け入れ境界

### 次へ進む最低条件

- 不変条件:
  - **I1:** reasonCode 以外の新しい出力を増やさない。path・例外本文・秘密・file の中身は、stdout・stderr・結果 JSON のどこにも出ない。
  - **I2:** config の読取・検証、または selection の読取（file・JSON・field 集合の確認。publish で guard 取得より前に行う部分）に失敗した実行は、Workflow guard の取得・資格情報の読取・ネットワーク要求のいずれにも進まない（`residue.remote` は `pre-put` のまま）。guard 取得後の `Assert-EvidenceSelection`（root・files の検証）は従来どおりで、本条件の対象外とする。
  - **I3:** 受け付ける path の集合を変えない（U1a）。fully qualified でない・存在しない・reparse 等の拒否条件はそのまま。
  - **I4:** 既存の reasonCode と exit code の意味を変えない。他の失敗（転送失敗、readback 失敗、未確定 PUT 等）は従来どおりの code。`Read-EvidenceConfig` を呼ぶ Evidence 以外の入口（Build・Content・Workflow・cleanup・schedule・sync）の結果も変えない（A2 指摘 5）。
- 成立条件:
  - **S1:** publish・fetch・inspect・use・release（U2a）で、`--config` の読取または検証に失敗したとき（相対 path・不在・JSON 不正・field 集合不一致・Hex 不正）、reasonCode が `invalid-evidence-config` になる。`Read-EvidenceConfig` が現在 `unsupported-schema` を投げる条件（schemaVersion・profile・endpoint 等の値不一致）は `unsupported-schema` のまま。
  - **S2:** `evidence publish` で、`--selection` が相対 path・不在・JSON 不正・field 集合不一致のとき、reasonCode が `invalid-selection` になる。taskId の書式違反（`invalid-task`）は本スライスで変えない。
  - **S3:** U4 を採ったので、config 本体が有効で、対応する deployment receipt が不在・読取不能・field 集合不一致のとき、上記 5 コマンドの reasonCode が `unknown-deployment` になる（既存の値不一致の `unknown-deployment` と同じ code）。
  - **S4:** `tools/Artifacts/README.md` に追加した reasonCode と意味が 1 箇所に書かれている。
- 受け入れ条件（検証例。どの条件を確かめるかを併記）:
  - **AC1（S1・I2）:** 相対 path の `--config` で publish → `failed`／`invalid-evidence-config`。注入した transport が 1 回も呼ばれず、Workflow guard の取得も無い。
  - **AC2（S1・S3）:** fetch・inspect・use・release で相対 path の `--config` → `invalid-evidence-config`。publish で不在・JSON 不正・field 不足の config → `invalid-evidence-config`。receipt を消した有効 config で publish → `unknown-deployment`。いずれも transport 呼出し 0。
  - **AC3（S2・I2）:** 相対 path・不在・JSON 不正・field 不足の `--selection` で publish → `invalid-selection`、transport 呼出し 0、Workflow の task 記録が作られない。
  - **AC4（I1）:** AC1〜AC3 の stdout・stderr・結果 JSON に、渡した path の文字列と fixture の dummy 秘密が含まれない。
  - **AC5（I1・F1 の再現）:** 子 process（既存の `EvidenceTestChild.ps1` に publish の action を足し、隔離 store を設定したうえで `artifacts.ps1 evidence publish` を同じ process 内で呼ぶ）で相対 `--selection` を渡し、stdout の JSON が `invalid-selection`、exit が 1、stderr が空。実 `%LOCALAPPDATA%` には触れない（A2 指摘 2）。
  - **AC6（I4）:** 既存の `ArtifactEvidence.Tests.ps1` の全 case（`unsupported-schema`、`selection-path`、`one-put-lost-response` 等）と、`Read-EvidenceConfig` を呼ぶ入口の既存 suite（§5 の判定必須）が従来どおり通る。
  - 合格例・不合格例（論点 3 の曖昧な AC 用）: AC1 で `residue.remote` が `pre-put` かつ reasonCode が `invalid-evidence-config` なら合格。reasonCode が正しくても transport が 1 回でも呼ばれていれば不合格（I2 違反）。
- ここでは答えない問いと所有する後続スライス:
  - 相対 path の受け付け（U1b は不採用）: 所有なし。必要が生じたら新しいスライス。
  - cleanup・schedule-install・schedule-status の config 失敗の reasonCode（U2b は不採用。結果の形と exit の意味を変える設計判断が要る）: 所有なし。
  - `invalid-task` 等、whitelist に無い他の既知 code の公開: 所有なし。
  - 共通の `Read-ArtifactJson` に原因別の例外を持たせるか: 所有なし（他の利用者に影響するため本スライスでは触れない）。
- 判定定義: 上記の不変条件・成立条件を満たせば GO。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。
- A3 後の例外承認: なし
- 決定記録: なし
- 本文へ転記した実装制約:
  - 例外文字列を reasonCode にそのまま流さない。既知の固定語彙を whitelist で写す既存の形を保つ。
  - `Read-ArtifactJson` の挙動は変えない。Evidence 側で「config の読取に失敗した」ことを固定語彙に変換する。
  - `Read-EvidenceConfig` の変換は、config 本体の読取・検証と receipt の読取をそれぞれ別の try で囲む。既存の `unsupported-schema`・`unknown-deployment` はそのまま通し、それ以外を新しい code にする。`Get-EvidenceDeploymentRoot` の失敗（`deployment-root-unavailable`）は変換しない。
  - selection の読取と field 集合の確認（publish の guard 取得前）だけを try で囲み、`invalid-selection` に変換する。処理順は変えない。
  - inspect・release は現在 whitelist を持たないので、`invalid-evidence-config`・`unknown-deployment`（S1・S3 の対象）だけを写す分岐を足す。それ以外は `unconfirmed` のまま。
  - テストは既存の `EvidenceTestSupport.ps1` の隔離 store と transport 注入を使う。実 R2・実資格情報・待機を使わない。

## 3. 責務マップ

| file | 責務（本スライスで担う変更） | 現在行数 | 予想増分 |
|---|---|---|---|
| `tools/Artifacts/EvidenceContract.psm1` | `Read-EvidenceConfig` が config・deployment receipt の読取失敗を `invalid-evidence-config`／`unknown-deployment` に変換する | 81 | +4〜8 |
| `tools/Artifacts/EvidenceApplication.psm1` | publish の selection 読取失敗を `invalid-selection` に変換。publish・fetch・use の whitelist に code を追加し、inspect・release に写す分岐を足す（形が 4 通りあるため、A2 指摘 3） | 129 | +8〜12 |
| `tools/Artifacts/tests/ArtifactEvidence.Tests.ps1` | AC1〜AC5 の case を追加 | 60 | +10〜20 |
| `tools/Artifacts/tests/EvidenceTestChild.ps1` | AC5 用に publish の action を追加 | 約 30 | +1〜2 |
| `tools/Artifacts/README.md` | reasonCode の追記（S4） | 302 | +2〜4 |

どれも 500 行・50% の警報に届かない。新しい file・依存・公開関数は作らない。変換は config を所有する `EvidenceContract` に置き、Application は既存の whitelist の形で写すだけにする（Build／Content と同じ配置）。

`Read-EvidenceConfig` の他の呼出し元への波及（A2 指摘 5、A3 で確認）: Build（publish・use・release）の whitelist は新しい code を含まないので `unconfirmed` のまま。Content は catch で `invalid-content-config` に包むので不変。`Invoke-EvidenceSync`・`Get-EvidenceConfigurations` は全例外を固定 code に包むので不変。schedule は CLI の一般診断のまま。`workflow-task.ps1` は小文字の例外 message を reasonCode に写すが、直前の `Get-EvidenceConfigurations` が同じ config を読んで失敗を `unknown-deployment` に包むため、2 回の読取の間に config が壊れる競合でしか差は出ない。この競合で出る code も非秘密の固定語彙なので I1 には反しない。

## 4. 実装計画

- 順序: EvidenceContract → EvidenceApplication → tests → README。
- Phase B から Phase A へ差し戻す条件: 変換のために `Read-ArtifactJson` や CLI 入口の例外方針を変える必要が出た、I2 を満たすには処理順の変更（guard 取得前の検証の追加等）が要ると分かった。
- 対象外を維持する方法: Build・Content・Credentials・Workflow・Harness・EvidenceCleanup・EvidenceSchedule の file に触れない。

## 5. テストとレビュー計画

- 単体テスト: `ArtifactEvidence.Tests.ps1` に `config-invalid-path`、`config-invalid-path-other-commands`、`deployment-receipt-missing`、`selection-invalid-path`、`invalid-path-no-leak`、`cli-relative-selection` を追加する（名前は B で調整可）。
- 差し戻し中の起点 `-Filter`: `pwsh tools/Artifacts/tests/ArtifactEvidence.Tests.ps1 -Case <追加した case>`。
- 判定必須テスト: 3 project の Release build（README「Evidenceの責務とoffline検証」の 3 コマンド）、`tools/Artifacts/tests/*.Tests.ps1` の全 suite を各 1 回（`Read-EvidenceConfig` を Build・Content・cleanup・schedule・reset が共有するため。AC6）、`tools/Workflow/tests/TaskLifecycle.Tests.ps1`、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。各 suite の registered／executed／failed を記録する。
- 全 EditMode 回帰の適用除外: 適用除外。理由: `unity/` を変更しない。代替証拠: diff に `unity/` が含まれないことの機械確認。
- 高コスト証拠: なし（実 R2・資格情報・人間観察を使わない）。最終証拠ラウンドは適用しない。
- 照合 script: なし（判定は各 test の registered／executed／failed で行う）。
- 操作・実行時・目視条件の検証経路: なし（offline test で閉じる）。
- 未知の操作経路: なし。
- 人間の判断が必要な条件: なし（U1〜U4 は A3 で owner が決定済み）。
- 機械検査: contract-audit、docs-audit、diff の対象 file の確認。
- A0/A1 主担当・モデル・ベンダー: Claude Opus 5.5／Anthropic（Claude Code desktop）。
- A2 独立レビュー: 1 件。観点はアーキテクチャゲートと不変条件の破れ。担当は Claude Sonnet 5.5（Anthropic、Claude Code の subagent、新しい context）。入力版は sha256 `1f6ab94158d3be52280c20b079fcc1ea4d87da8a66ea5e7119df247f426eaef0`（commit `2a7ddb8`）。
- A3 統合担当・モデル・採否: Claude Opus 5.5（主担当）と owner。owner の決定は U1a・U2a・U3（tracked HANDOFF）・U4 採用。A2 指摘の採否:
  1. high（U2b では cleanup／schedule の結果の形と exit が変わる）: 採用 → U2a に変更。
  2. high（AC5 の実 CLI 子 process は実 `%LOCALAPPDATA%` が要る）: 採用 → AC5 を、隔離 store を設定する子 process 経由に変更。
  3. med（Application の catch が 4 通りの形をしている）: 採用 → 実装制約と責務マップの増分を修正。
  4. med（I2 の字義と guard 後の `Assert-EvidenceSelection` の順序が合わない）: 採用 → I2・S2 の範囲を guard 前の読取と field 集合に限定。処理順は変えない。
  5. med（`Read-EvidenceConfig` の他の呼出し元への波及）: 採用 → I4 に明記し、§3 に波及の確認を書き、判定必須を全 Artifacts suite と Workflow に広げる。
  6. med（S1 の schema 不一致の範囲が曖昧）: 採用 → S1 で範囲を固定。receipt は別の try にする。
  7. low（行数見積りが過小）: 採用 → §3 を修正。
- C' 用に予約した担当・モデル・ベンダー: Phase B は Claude Opus 5.5。判定 C は Claude Fable 5.1、C' はこの 2 つと異なる Claude Sonnet 5.5 の新しい subagent context とする（risk `normal` のため強化条件は不要）。
- 独立性の強化条件を満たせない場合の理由: 対象外（risk `normal`）。C' は A2 と同じ系列を使うが、A2 は設計の入力版だけを見ており、C の所見は見ない。

## 6. Phase B 実装結果

- 実装:
- HANDOFF との差:
- 未実行:
- implementation head commit:
- Phase B 担当・モデル・ベンダー:

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
