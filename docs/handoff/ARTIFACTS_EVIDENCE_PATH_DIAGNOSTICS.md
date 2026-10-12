# Evidence CLI の path 診断

## 0. メタデータ

- type: `slice`
- status: `A`（A0 と A1 初稿まで。A2 は未依頼）
- branch: `codex/artifacts-cli-path-diagnostics`
- implementation base commit: `7b5b3002308eaeb330b4e549df941169ad06be36`（`develop`。PR #120 のマージを含む。A3 で凍結時の `develop` と照合し直す）
- implementation head commit:
- risk: `normal`（公開 API・asmdef・Unity は変えない。保存・削除の判定は変えず、失敗時の reasonCode だけを変える。fail-closed の向きは保つ）
- owner: OSM maintainers
- created: 2026-10-12
- expires: 2026-11-09 または置換 revision
- harvest to: `tools/Artifacts/README.md`（Evidence v2 の通常操作と exit／reasonCode の段落）。完了時に本書を削除する。
- Phase A snapshot の取得案内・状態: 未生成（A3 凍結時に作る）
- Phase B result snapshot の取得案内・状態:
- evidence bundle の取得案内・状態:
- C' blind bundle の取得案内・状態:
- 引継ぎ票: 次は Phase A2（独立レビュー 1 件の依頼）。その後 A3 で owner が U1〜U4 を決めて凍結し、push と PR 作成（base `develop`）を行う。A2 は読取専用。資格情報・Artifact Storage・Unity は不要。停止規則: A2 の担当は本書と常時契約・`osm-workflow` だけを読み、ファイルを変更しない。引き渡さないもの: なし（Phase A のため所見の隔離は未発生）

管理方式は tracked HANDOFF を推奨する（U3）。本スライスは PR #120 で改訂した `osm-workflow` の規則で進める初めてのスライスで、§2 は不変条件と成立条件を先に書く形式に従う。

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

### 未決事項（A1 で案を出し、A3 で決める）

- **U1 相対 path:** (a) 受け付けない（推奨）。(b) CLI 入口で `GetFullPath` によって絶対 path に解決してから渡す。b はカレントディレクトリへの依存を持ち込み、F5 の誤読の経路を開く。Build／Content とも揃わなくなる。
- **U2 対象コマンド:** (a) publish・fetch・inspect・use・release（`EvidenceApplication.psm1`）だけ。(b) a に cleanup・schedule-install・schedule-status を加える（推奨。同じ `--config` を受けるので、利用者から見て揃う）。
- **U3 管理方式:** tracked HANDOFF（推奨）か、H1 の `artifact-evidence-lifecycle` の固定 offline 集合に載せるか。後者は B exit の offline 実行が必須になり、本スライスの規模に対して重い。
- **U4 deployment receipt:** config 本体が読めても、対応する `deployments/<deploymentId>.json` が無い・読めない場合は、現状 `unconfirmed` になる。`unknown-deployment`（既存 code）に写すか。推奨は写す（同じ読取経路で、既存語彙で足りる）。

## 2. 意思決定と受け入れ境界

### 次へ進む最低条件

- 不変条件:
  - **I1:** reasonCode 以外の新しい出力を増やさない。path・例外本文・秘密・file の中身は、stdout・stderr・結果 JSON のどこにも出ない。
  - **I2:** config または selection の検証に失敗した実行は、Workflow guard の取得・資格情報の読取・ネットワーク要求のいずれにも進まない（`residue.remote` は `pre-put` のまま）。
  - **I3:** 受け付ける path の集合を変えない（U1 が a の場合）。fully qualified でない・存在しない・reparse 等の拒否条件はそのまま。
  - **I4:** 既存の reasonCode と exit code の意味を変えない。他の失敗（転送失敗、readback 失敗、未確定 PUT 等）は従来どおりの code。
- 成立条件:
  - **S1:** U2 で選んだ各コマンドで、`--config` が相対 path・不在・JSON 不正・schema 不一致のとき、reasonCode が `invalid-evidence-config`（schema 不一致は既存の `unsupported-schema` を維持）になる。
  - **S2:** `evidence publish` で、`--selection` が相対 path・不在・JSON 不正のとき、reasonCode が `invalid-selection` になる。
  - **S3:** U4 を採る場合、deployment receipt が不在・不一致のとき `unknown-deployment` になる。
  - **S4:** `tools/Artifacts/README.md` に追加した reasonCode と意味が 1 箇所に書かれている。
- 受け入れ条件（検証例。どの条件を確かめるかを併記）:
  - **AC1（S1・I2）:** 相対 path の `--config` で publish → `failed`／`invalid-evidence-config`。注入した transport が 1 回も呼ばれず、Workflow guard の取得も無い。
  - **AC2（S1）:** fetch・inspect・use・release（U2b なら cleanup・schedule-status も）で同じ入力 → `invalid-evidence-config`。
  - **AC3（S2・I2）:** 相対 path・不在・JSON 不正の `--selection` で publish → `invalid-selection`、transport 呼出し 0。
  - **AC4（I1）:** AC1〜AC3 の stdout・stderr・結果 JSON に、渡した path の文字列と fixture の dummy 秘密が含まれない。
  - **AC5（I1・F1 の再現）:** CLI（`pwsh tools/artifacts.ps1 evidence publish ...`）を子 process で起動し、相対 `--selection` で stdout の JSON が `invalid-selection` を返す。exit は 1。
  - **AC6（I4）:** 既存の `ArtifactEvidence.Tests.ps1` の全 case（`unsupported-schema`、`selection-path`、`one-put-lost-response` 等）が従来どおり通る。
  - 合格例・不合格例（論点 3 の曖昧な AC 用）: AC1 で `residue.remote` が `pre-put` かつ reasonCode が `invalid-evidence-config` なら合格。reasonCode が正しくても transport が 1 回でも呼ばれていれば不合格（I2 違反）。
- ここでは答えない問いと所有する後続スライス:
  - 相対 path の受け付け（U1b を採らない場合）: 所有なし。必要が生じたら新しいスライス。
  - 共通の `Read-ArtifactJson` に原因別の例外を持たせるか: 所有なし（他の利用者に影響するため本スライスでは触れない）。
- 判定定義: 上記の不変条件・成立条件を満たせば GO。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。
- A3 後の例外承認: なし
- 本文へ転記した実装制約:
  - 例外文字列を reasonCode にそのまま流さない。既知の固定語彙を whitelist で写す既存の形を保つ。
  - `Read-ArtifactJson` の挙動は変えない。Evidence 側で「config の読取に失敗した」ことを固定語彙に変換する。
  - テストは既存の `EvidenceTestSupport.ps1` の隔離 store と transport 注入を使う。実 R2・実資格情報・待機を使わない。

## 3. 責務マップ

| file | 責務（本スライスで担う変更） | 現在行数 | 予想増分 |
|---|---|---|---|
| `tools/Artifacts/EvidenceContract.psm1` | `Read-EvidenceConfig` が config・deployment receipt の読取失敗を `invalid-evidence-config`／`unknown-deployment` に変換する | 81 | +3〜5 |
| `tools/Artifacts/EvidenceApplication.psm1` | publish の selection 読取失敗を `invalid-selection` に変換。各 catch の whitelist に `invalid-evidence-config` を追加 | 129 | +3〜6 |
| `tools/Artifacts/EvidenceCleanup.psm1`・`EvidenceSchedule.psm1`（U2b の場合） | 同じ code を結果に写す | 285・422 | +1〜3 ずつ |
| `tools/Artifacts/tests/ArtifactEvidence.Tests.ps1` | AC1〜AC5 の case を追加 | 60 | +10〜20 |
| `tools/Artifacts/README.md` | reasonCode の追記（S4） | 302 | +2〜4 |

どれも 500 行・50% の警報に届かない。新しい file・依存・公開関数は作らない。変換は config を所有する `EvidenceContract` に置き、Application は既存の whitelist の形で写すだけにする（Build／Content と同じ配置）。

## 4. 実装計画

- 順序: EvidenceContract → EvidenceApplication →（U2b なら）Cleanup・Schedule → tests → README。
- Phase B から Phase A へ差し戻す条件: 変換のために `Read-ArtifactJson` や CLI 入口の例外方針を変える必要が出た、I2 を満たすには処理順の変更（guard 取得前の検証の追加等）が要ると分かった。
- 対象外を維持する方法: Build・Content・Credentials・Workflow・Harness の file に触れない。

## 5. テストとレビュー計画

- 単体テスト: `ArtifactEvidence.Tests.ps1` に `config-invalid-path`、`config-invalid-path-other-commands`、`selection-invalid-path`、`invalid-path-no-leak`、`cli-relative-selection` を追加する（名前は B で調整可）。
- 差し戻し中の起点 `-Filter`: `pwsh tools/Artifacts/tests/ArtifactEvidence.Tests.ps1 -Case <追加した case>`。
- 判定必須テスト: 3 project の Release build、`ArtifactEvidence.Tests.ps1` の全 case、U2b なら `EvidenceCleanup.Tests.ps1`・`EvidenceSchedule.Tests.ps1` の全 case、`pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。
- 全 EditMode 回帰の適用除外: 適用除外。理由: `unity/` を変更しない。代替証拠: diff に `unity/` が含まれないことの機械確認。
- 高コスト証拠: なし（実 R2・資格情報・人間観察を使わない）。最終証拠ラウンドは適用しない。
- 照合 script: なし（判定は各 test の registered／executed／failed で行う）。
- 操作・実行時・目視条件の検証経路: なし（offline test で閉じる）。
- 未知の操作経路: なし。
- 人間の判断が必要な条件: U1〜U4 の採否（owner、A3）。
- 機械検査: contract-audit、docs-audit、diff の対象 file の確認。
- A0/A1 主担当・モデル・ベンダー: Claude Opus 5.5／Anthropic（Claude Code desktop）。
- A2 独立レビュー: 未依頼。risk `normal` なので 1 件（アーキテクチャゲート＋秘密の非露出）を推奨。担当は A2 の時点で選び、C′ 用の系列を残す。
- A3 統合担当・モデル・採否:
- C' 用に予約した担当・モデル・ベンダー:
- 独立性の強化条件を満たせない場合の理由:

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
