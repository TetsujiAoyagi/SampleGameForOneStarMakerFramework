# Artifact Storage program r7: Content 種別の追加（Phase A）

## 0. メタデータ

- type: slice（文書 revision。program 文書 `BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` を r6 → r7 にする Phase A）
- status: A（A1 改訂 rev2。owner との相談 3 点を反映済み。A2 実施中、A3 未実施）
- branch: `codex/artifact-storage-content-kind-phase-a` / PR base: `develop`
- implementation base commit: `4d5dcc379a31bd6d47713d1072e47bed020061ee`（`develop`、PR #116 を含む）
- implementation head commit: 未実装（実装は文書改訂のみ。コードは変えない）
- risk: normal（保存契約の文書。ただし後続が Unity の Player 構成と R2 の保存形式を拘束するため、A2 では保存形式の拘束を見る）
- owner: OSM maintainers
- created: 2026-10-11
- expires: 2026-11-10 または置換 revision。期限時は owner が更新・廃止を判断する
- harvest to: r7 の確定内容は `BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §2/§4 に入る（本書の目的そのもの）。実装済みの操作は後続スライスが `tools/Artifacts/README.md` へ反映する
- Phase A snapshot の取得案内・状態: 未生成
- Phase B result snapshot の取得案内・状態: 未生成（未到達）
- evidence bundle の取得案内・状態: 未生成（未到達）
- C' blind bundle の取得案内・状態: 未生成（未到達）

この Phase A は文書作成までで、artifacts の publish/fetch・資格情報・Unity・コードに触れない。

## 1. 目的と対象外

- 目的: 開発用 Artifact Storage の単一保存契約に、Player とは独立した Content Directory の保存種別を足す必要があるかを確定し、足すなら契約の骨格を固定する。
- 対象外: 下記「ここでは答えない問いと所有する後続スライス」の全項目。コード変更、`build collect` の拡張、R2 の key 設計、Unity の Player 構成変更、ArtifactsRoot の清掃実装。

### 現況（base `4d5dcc3` で確認）

- program §2 の保存種別は **開発Build** と **開発Evidence** の2つ。Content は種別として存在しない。§4 B は Content を「program r7 revision（Content種別を追加する Phase A）」へ送っている。
- PR #115 の `build collect` は Player 1 件を `<ArtifactsRoot>/players/<project>/<target>/<configuration>/<buildId>/` へ退避して既存 `build publish` に渡す。Player は `content/` を内包し、検査は `content/` の全 file が `transport.json` の `files` と一致することを要求する。`build-info.json` に `contentSet`・`contentTarget`・`buildIdentity`・`transportManifestSha256` を持つ。#111 の上限は 512 file・240 MiB・単一 256 MiB。README は「Content Directory の独立した保存、Player/Contentの分離」を後続と明記している。
- `ContentTransportPublisher` は Content を `transport.json` + `content/` の形で出す。manifest は `contentSet`・`revision`・`target`・各 file の size/SHA-256 を持つ。`revision` は Build identity で、1 revision が不変単位。transport manifest v1 の `target` は固定値 `StandaloneWindows64-Player`（Architecture §13）。
- Player の content 参照は 2 通り既に存在する。(a) baked の `content:relativeDirectory`（SampleGame は `"content"`）が指すパッケージ内 directory。(b) `content:installedRevisionPath` と `content:manifestSha256` の pair による DIST managed revision root（`InstalledRevisionVerifier` が receipt・manifest digest・contentSet・revision・target・全 file を照合）。(b) は Architecture §04 / §20 に記載があり、Player の protected-key 一覧に含まれない。ただし baked の `relativeDirectory` は空でない値が必須で、不一致は起動失敗になる（`PlayerContentConfiguration`）。
- 「4 季節で約 880 file、#111 の 512 file 上限を超える」は owner の推定で、base では実測していない。

## 2. 意思決定と受け入れ境界

### A0 の確定: r7 が要るか

**要る。** 理由は次の 3 点で、いずれも program §2 の単一契約を変えるため、後続スライスの A3 で個別に決めると契約が割れる。

1. 寿命が違う。Content は revision 単位の不変物で、複数の Player が同じ revision を参照できる。Build の `project/target/configuration` 系列にも Evidence の task 終了にも属さない。
2. 保持の単位が違う。系列は `contentSet/target`、削除単位は revision 丸ごとで、Build と同じ「最新 N 件・利用中保護」を別系列として適用する。これは §2 の保持契約への追加であり、README の運用だけでは決められない。
3. 保存形式が Player 分離と結び付く。分離しないままだと Player publish が 512 file を超えうるため、「Content の保管形式」と「Player/Content の分離」は同時に決める必要がある（owner の指示）。

### このスライスが答える問い（1 つ）

> Content Directory を、Player とは独立した第三の保存種別として program §2 に足すとき、保存の単位・系列・置き場・保持の契約をどう固定するか。

「どう実装するか」「どの順で作るか」は含めない。答えは program 文書 r7 の本文と、後続スライスへの移送先の確定である。

### 進める最低条件

次スライス（Content 保存の最初の実装スライスの Phase A）を起こしてよいと判定できる条件は、r7 の本文が次をすべて固定していること。

1. **単位:** 既存 transport 形式（`transport.json` + `content/`）を revision ごとに丸ごと不変単位とする。差分 upload・content-addressed・GC を採らない旨を書く。
2. **系列と削除:** 系列は `contentSet/target`。platform は target 値で表す。古い revision の削除は Build と同じ件数保持 policy（成功のみ N に数える・利用中保護・失敗は既存を追い出さない）を別系列として適用する。N の既定値は Build に揃える案とし、確定は実装スライスの A3。
3. **置き場:** ローカルは `<ArtifactsRoot>/content/<contentSet>/<target>/<revision>/`。ArtifactsRoot は Player と共用の任意のローカル folder。R2 にも revision 単位で丸ごと上げる。
4. **全 platform で 1 形式:** 保存形式は platform で分けない。ただし transport manifest v1 の `target` が固定値である制約（上記現況）を、保存契約の前提として明記する。
5. **hd/sd の受け止め:** 端末性能別の違いが `contentSet` か representation で表せるか、系列 key の拡張で足りるかを、現況の根拠付きで結論する（「系列 key の拡張性だけを確認」）。
6. **上限の扱い:** 512 file を超える Content の保存は本 r7 では解かず、program §4 B の後続（上限引き上げ・multipart）へ送ることを明記する。Player を内包したまま 880 file（owner の推定）を保存できない事実を、分離の動機として書く。上限は契約へ焼き込まない設定値とし、R2 のプラン変更の要否は 4 季節 Content の実測後に後続で決める（上限は R2 側ではなく #111 の自前の上限）。
7. **移送:** 下記「ここでは答えない問いと所有する後続スライス」の各項目に所有する後続スライス名を付ける。program §4 B 末尾の「後続の問い」一覧を r7 で置換する。
8. **用途識別:** 用途（通常開発 / 期間限定イベント / 次期大型アップデート等）は `contentSet` の名前に持たせる（例 `mainline`、`event-<名>`、`next-major`）。階層は足さず、manifest・検証・Player の baked 設定を変えない。系列と件数保持は用途ごとに独立し、イベント終了後はその系列だけ片付けられる。
9. **消費側の指定:** Editor も Player も、取り込み済み revision root を `contentSet/target/revision` と manifest の SHA-256 で指定する（既存の `installedRevisionPath` + `manifestSha256` の組と同じ）。どの系列を見るかは workspace のローカル設定（git 管理外）で持ち、取り込み時にその系列の最新を revision と digest に解決して固定する。ストレージに可変の別名（latest 等）は持たせない。
10. **Player の固定:** Player は Build 時の `contentSet` に固定する。起動時の config / 引数による切り替えは対象外（Build 時の埋め込みリソースと整合しないため。既存の protected-key 検証と矛盾しない）。

受け入れ条件（上の観測可能な詳細。別の完了バーにしない）:

- program 文書の §0（revision を `r7`、`updated`）、§2（Content 節）、§4 B の後続一覧が上記 1〜7 と矛盾なく更新されている。
- 同じ事実を 2 箇所に置かない（置き場・系列は §2 に 1 回だけ書き、他は参照）。
- `tools/Artifacts/README.md` は変えない（r7 は未実装の契約。実装済みの操作の説明に混ぜない）。`docs/README.md` の program 欄は必要なら 1 文だけ更新する。
- `pwsh tools/docs-audit.ps1` と `pwsh tools/contract-audit.ps1` が通る。

### ここでは答えない問いと所有する後続スライス

| 問い | 所有 |
|---|---|
| Unity 側の変更要否（Player の content 参照先）。候補は 「未決事項の詳細」の U1〜U3 | Content 分離の実装スライス（着手時に A1 から。A1 でスパイクを先に切る可能性あり） |
| `build collect` の Player が `content/` を内包しない場合の検査・`build-info.json`・selection の変更 | 同上 |
| Content の退避・publish の CLI（collect の content 版）、R2 key、完了確定点、receipt | Content 保存の実装スライス |
| 統合フォルダからの取り込み方式（取得型／監視型）と複数ワークスペースの衝突規則 | 統合 Content フォルダのスライス |
| 512 file / 240 MiB 上限の引き上げ・multipart | program §4 B の上限スライス（既存の後続） |
| ArtifactsRoot の件数清掃・`.staging` 回収、Content の実 DELETE と利用中状態の収集 | 件数保持の運用接続スライス（既存の後続） |
| transport manifest v1 の固定 `target` の拡張（Windows 以外の platform） | Windows 以外の target を扱うスライス（program §4 B の既存の後続） |
| Player の起動時 `contentSet` 切り替え | 行わない（対象外。必要になれば別途 owner が判断） |
| ローカル設定の形式、取り込み UI、Editor の取り込み経路が local directory の revision を受けられるか（未確認） | Content 取り込みのスライス |
| 診断の改善（`publish` 表示、240 字超、二度読み、`stage-conflict`） | 診断改善の既存の後続（変更なし） |
| 開発環境セットアップ script（`OSM_ARTIFACTS_ROOT` 等） | セットアップスライス（既存の後続） |
| 別 host / Cloud 配布 | program §4 C |

### 判定定義と停止規則

- GO: 最低条件 1〜7 を満たす r7 が A3 で凍結され、文書 PR が `develop` へ入る。
- NO-GO: 保存形式が Player 分離の方式に依存して 1 つに決まらないと分かった場合。この場合は U2 のスパイクを先に切る問いへ分け、r7 は保留する。
- 停止規則: 最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。未達のまま終了しない。実装・検証の追加（実 R2 往復、Unity 実行）は求めない。
- A3 後の例外承認: なし
- 本文へ転記した実装制約: §4 の同名節
- 未決事項: §4 の「未決事項の詳細」

## 3. 責務マップ

| ファイル | 責務 | 現在行数 | 予想増分 | 備考 |
|---|---|---|---|---|
| `docs/handoff/BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` | program 全体の保存契約。r7 の本文はここ | 136 | +25〜35 | §2 に Content 節を足し、§4 B の後続一覧を置換する。500 行未満・責務は変わらない |
| `docs/README.md` | program 一覧の状態文 | 76 | 0〜+1 行 | 状態が変わる場合だけ |
| 本書 | r7 Phase A の作業指示 | 約 150 | — | r7 のマージ後、Phase D で保存・引渡し確認のうえ削除（program に harvest 済み） |

新しい恒久文書は作らない。Content の契約は program §2 に置き、実装済みの操作は後続スライスが `tools/Artifacts/README.md` に置く。

## 4. 実装計画・未決事項の詳細・制約

### 実装計画

- 変更対象: 上表のとおり。コード・スクリプト・Unity アセットは変更しない。
- 順序: A2（owner の依頼後）→ A3 で採否を記録 → program 文書の r7 を書く → docs-audit / contract-audit → PR。
- Phase B から Phase A へ差し戻す条件: Content の保存形式が Player 分離の方式（未決事項の詳細）に依存して 1 つに決まらないと分かった場合。
- 対象外を維持する方法: r7 の文章に実装手順・コマンド名・R2 key を書かない。書きたくなったら後続スライスへ移す。

### 未決事項の詳細（A1 の候補）

#### Unity 側の変更要否（Player の content 参照先）

問い自体は後続の所有だが、保存契約が拘束されるかを見るため候補を記録する。r7 は候補のどれも選ばない。

| 候補 | 内容 | Unity 変更 | 保存契約への影響 |
|---|---|---|---|
| U1 | Player は今のまま `content/` を内包。Content は同じ revision を別途保存（重複保存） | なし | Player publish が 512 file を超える問題は残る。分離の動機を満たさない |
| U2 | Player build が `content/` を出さず、起動側が既存の `content:installedRevisionPath` + `content:manifestSha256` で外部 revision root を指す | Player build の `content/` 省略、baked `relativeDirectory` の扱い（空不可のため仮値が要る）。参照 key は変更不要 | revision root が `transport.json` + `content/` の丸ごと形であることが前提。r7 の単位 1 と整合する |
| U3 | 新しい config key で Content root を指す | 公開面（config スキーマ・protected key）の追加 | Phase A 再開級。r7 では選ばない |

Editor 側は既に Use For Next Play で明示した pair を選ぶため、用途（contentSet）の切り替えはローカル設定の指定で足りる見込み。Editor の検証が local directory の revision を取り込み元として受けるかは未読・未確認。Player は baked の `contentSet` と外部 revision の `contentSet` の一致を検証するため、用途を切り替えられない（最低条件 10 の前提）。

U2 が成立するかは未確認。確認する点は、(a) Player build が `content/` なしで成立するか、(b) 外部 revision root を Player 起動時に渡す経路（app-config / 環境変数 / command line）が、directory Player の「生成 bootstrap Scene と runtime 名で latch」という起動契約と両立するか、(c) `InstalledRevisionVerifier` が `target` を `StandaloneWindows64-Player` に固定している点。いずれも Unity 実行を伴うため、Phase C 担当のスパイクに回すか、後続スライスの A1 で扱う。**未確認。確認は Content 分離の実装スライス。**

#### hd/sd

現況の根拠: `representation` は Content の候補ごとの属性（`BuildContentProjection`）で、Player の config では起動時に 1 値に固定される。したがって 1 revision 内に複数 representation を同梱できる可能性が高いが、同梱の可否と Player build が 1 値だけを持つ方式は未確認。A1 の暫定案は「性能別は representation を第一候補とし、revision を割る場合は contentSet に含める。系列 key は `contentSet/target` から変えない」。A2 で反証を求める。

#### その他

- N の既定値（Build に揃えて 10）。確定は実装スライス。
- 統合フォルダは取得型／監視型の別を r7 では決めない。決めると R2 側の契約まで拘束するため。
- 880 file の推定は実測ではない。実装スライスの A0 で実測してから動機として引用する。

### 本文へ転記した実装制約

- 依存は Game → Framework の一方向。`SceneState`・公開 API・asmdef は本スライスで触らない。
- Artifact Storage の通常操作は事前承認の範囲内。本スライスは publish/fetch・資格情報を実行しない。
- PR の base は `develop`。
- 同じ事実を 2 箇所に置かない。program 文書と README の重複を作らない。
- 常時契約により、保存種別の追加は旧データとの互換や移行台帳を要求しない（program §1）。

## 5. テストとレビュー計画

- 単体テスト: なし（コード変更なし）。
- 差し戻し中の起点 `-Filter`: なし。
- 判定必須テスト（GO 候補 head）: `pwsh tools/docs-audit.ps1`、`pwsh tools/contract-audit.ps1`。
- 全 EditMode 回帰の適用除外: 適用外。理由は文書のみの変更で Unity 資産・コードを変えないため。代替証拠は上記 2 つの audit と、program 文書の参照整合の目視。
- 統合・Unity テスト: なし。
- 操作・実行時・目視条件の検証経路: なし（人間の操作・Unity 実行・実機観察を含む最低条件を置かない）。
- 未知の操作経路の疎通結果: なし。U2 の成立確認は実装スライスで行う旨を 「未決事項の詳細」の Unity 節に記録済み。
- 人間の判断が必要な条件: なし。A3 の採否と、取得型／監視型を後続へ送る判断は owner が行う。
- 機械検査: 上記 audit。
- A0/A1 主担当・モデル・ベンダー: Claude Sonnet 5.5（Anthropic）。
- A2 独立レビュー: 未起動。owner の依頼後に、観点（①Content 契約の過不足と program 内の矛盾、②アーキテクチャゲート: Player/Content 分離が Unity の起動契約・寿命を拘束しないか、③A0 のみから代替構成を出す）を割り当てる。
- A3 統合担当・採否: 未実施。
- C' 用に予約した担当: 未予約。
- 独立性の強化条件を満たせない場合の理由: 未評価。

## 6. Phase B 実装結果

未着手

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手
