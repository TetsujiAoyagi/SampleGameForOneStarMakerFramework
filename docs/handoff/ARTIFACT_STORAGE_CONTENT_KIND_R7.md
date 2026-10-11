# Artifact Storage program r7: Content 種別の追加（Phase A）

## 0. メタデータ

- type: slice（文書 revision。program 文書 `BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` を r6 → r7 にする Phase A）
- status: A（A1 改訂 rev3。A2 第 1 ラウンドの指摘と owner の方針を反映済み。A2 第 2 ラウンドの入力版。A3 未実施）
- branch: `codex/artifact-storage-content-kind-phase-a` / PR base: `develop`
- implementation base commit: `4d5dcc379a31bd6d47713d1072e47bed020061ee`（`develop`、PR #116 を含む）
- implementation head commit: 未実装（実装は文書改訂のみ。コードは変えない）
- risk: normal（保存契約の文書。ただし保存形式が後続の Player 構成と R2 の object 構成を拘束するため、A2 では保存形式の拘束を見る）
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

- 目的: 開発用 Artifact Storage の単一保存契約に、Player とは独立した Content Directory の保存種別を足し、その契約の骨格（単位・系列・置き場・保持・保存形式の制約）を固定する。
- 対象外: 下記「ここでは答えない問いと所有する後続スライス」の全項目。コード変更、`build collect` の拡張、R2 の key の確定、Unity の Player 構成変更、ArtifactsRoot の清掃実装。

### 現況（base `4d5dcc3` で確認）

- program §2 の保存種別は **開発Build** と **開発Evidence** の2つ。Content は種別として存在しない。§4 B は Content を「program r7 revision（Content種別を追加する Phase A）」へ送っている。
- PR #115 の `build collect` は Player 1 件を `<ArtifactsRoot>/players/<project>/<target>/<configuration>/<buildId>/` へ退避して既存 `build publish`（ZIP 1 個を 1 PUT）に渡す。Player は `content/` を内包し、検査は `content/` の全 file が `transport.json` の `files` と一致することを要求する。`build-info.json` に `contentSet`・`contentTarget`・`buildIdentity`・`transportManifestSha256` を持つ。#111 の上限は 512 file・240 MiB・単一 256 MiB で、R2 側の上限ではなく自前の上限。
- `ContentTransportPublisher` は Content を `transport.json` + `content/` の形で出す。manifest は `contentSet`・`revision`・`target`・各 file の size/SHA-256 を持つ。`revision` は Build ごとに新しく作られる Build identity（時刻 + GUID）で、1 revision が不変単位。transport manifest v1 の `target` は固定値 `StandaloneWindows64-Player`（Architecture §13）。現在 transport を出すのは Player Build だけ（`SampleGamePlayerBuildCoordinator`）。
- 取り込み側（Unity）は file 単位で読む。`IContentArtifactSource.OpenReadAsync(relativePath)` を Local / HTTP が実装し、`ContentInstaller` は manifest の全 file を新しい staging へ取得・検証して `installed/<contentSet>/<revision>` へ公開する。既存の install 済み revision の file を再利用する処理は確認できていない。取り込み元に local directory の revision（`transport.json` + `content/`）を使える（Architecture §20）。
- Player の content 参照は 2 通り既に存在する。(a) baked の `content:relativeDirectory`（SampleGame は `"content"`）が指すパッケージ内 directory。(b) `content:installedRevisionPath` と `content:manifestSha256` の pair による DIST managed revision root。(b) は `InstalledRevisionVerifier` が receipt・manifest digest・contentSet・revision・target・全 file を照合し、revision には Player の baked BuildIdentity を渡す（`PlayerContentConfiguration.cs:30`）。つまり Player が使える revision は、自分と同じ Build identity のもの 1 つに限られる。pair は Player の protected key に含まれず、片方だけの指定は拒否される。外部 revision root は `…/installed/<contentSet>/<revision>` の形で `receipt.json` を持つ必要がある（DIST cache の配置。target の階層は無い）。
- 「4 季節で約 880 file」は owner の推定で、base では実測していない。

## 2. 意思決定と受け入れ境界

### A0 の確定: r7 が要るか

**要る。** 次の 3 点は program §2 の単一契約を変えるため、後続スライスの A3 で個別に決めると契約が割れる。

1. 寿命が違う。Content は revision 単位の不変物で、Editor は任意の revision を取り込める。Build の `project/target/configuration` 系列にも Evidence の task 終了にも属さない。
2. 保持の単位が違う。系列は `contentSet/target`、削除単位は revision 丸ごと。§2 の保持契約への追加であり、README の運用だけでは決められない。
3. 保存形式が Player 分離と結び付く。ZIP 1 個の Build 保存形式のままでは Player の 512 file 上限に縛られるため、「Content の保管形式」と「Player/Content の分離」は同時に決める必要がある（owner の指示）。

**独立種別か、Build 付随か。** A2 第 1 ラウンドが「`buildId` が同じ Content を Build の別 bundle として同じ系列・保持に置く」代替を挙げた。owner は独立種別を選んだ。理由は、Editor が Build に結び付かない任意の revision を取り込む要件と、将来の Content 単独 Build を受けられること。A3 で確認する。

### このスライスが答える問い（1 つ）

> Content Directory を、Player とは独立した第三の保存種別として program §2 に足すとき、保存の単位・系列・置き場・保持と、保存形式の制約をどう固定するか。

「どう実装するか」「どの順で作るか」は含めない。答えは program 文書 r7 の本文と、後続スライスへの移送先の確定である。

### 前提（保存契約ではなく Unity の現状。r7 は変えない）

- Player は自分の Build 時の `contentSet` と BuildIdentity に結び付く。起動時の切り替えは行わない（Build 時の埋め込みリソースと整合しないため）。
- したがって現状、配布済み Player に Content だけの revision を差し替える運用はできない。この前提を変える案（Player と Content の互換を計算した「互換キー」で判定する）は、Content 分離の実装スライスの A1 で最初の選択肢にする。保存契約は、どちらの案でも変わらない。

### 進める最低条件

次スライス（Content 保存の最初の実装スライスの Phase A）を起こしてよいと判定できる条件は、r7 の本文が次をすべて固定していること。

1. **単位:** 既存 transport 形式（`transport.json` + `content/`）を revision ごとに丸ごと不変単位とする。revision の上書きは行わない。差分 upload・content-addressed・GC を採らない旨を書く。
2. **系列と削除:** 系列は `contentSet/target`。platform は target 値で表す。値の正規化は原則だけ決める（Build の系列 target は小文字の正規化値、Content の `contentTarget` は別の値、`contentSet` は大文字・`_`・`.` を許す）。具体的な対応表は実装スライス。古い revision の削除は Build と同じ件数保持 policy を別系列として適用する（成功のみ N に数える、利用中保護、失敗は既存を追い出さない）。N の既定値は Build に揃える案で、確定は実装スライス。**保持中の Player が指す Content revision（`build-info.json` の `contentSet`・`buildIdentity`・`transportManifestSha256`）は利用中として保護し、N に数える。** 保護状態の収集は後続。
3. **置き場:** ローカルは `<ArtifactsRoot>/content/<contentSet>/<target>/<revision>/`。ArtifactsRoot は Player と共用の任意のローカル folder。これは取り込み元（install source）であり、消費側の DIST cache（`installed/<contentSet>/<revision>`、known-good・lease・eviction を持つ）とは別の層である。削除する主体は層ごとに別で、同じ revision を二重に消さない。R2 にも revision 単位で全 file を上げる。
4. **全 platform で 1 形式:** 保存形式は platform で分けない。ただし transport manifest v1 の `target` が固定値である制約を、保存契約の前提として明記する。
5. **hd/sd の受け止め:** 端末性能別の違いが `contentSet` か representation で表せるか、系列 key の拡張で足りるかを、現況の根拠付きで結論する。暫定案は「第一候補は representation、revision を割る場合は contentSet に含める。系列 key は `contentSet/target` から変えない」。
6. **上限の扱い:** r7 は新しい上限を足さない。Content は ZIP 1 個にしないため、#111 の ZIP 由来の 512 file・240 MiB は Content の前提にしない。Content 固有の上限値と R2 のプラン変更の要否は、4 季節 Content の実測後に後続で決める。
7. **移送:** 下記「ここでは答えない問いと所有する後続スライス」の各項目に所有する後続スライス名を付ける。program §4 B 末尾の「後続の問い」一覧を r7 で置換する。
8. **用途識別:** 用途（通常開発 / 期間限定イベント / 次期大型アップデート等）は `contentSet` の名前に持たせる（例 `mainline`、`event-<名>`、`next-major`）。階層は足さず、manifest・検証・Player の baked 設定を変えない。系列と件数保持は用途ごとに独立する。
9. **可変の別名を持たない:** ストレージに latest 等の可変の別名を持たせない。消費側は revision と manifest の SHA-256 で固定する。どの系列を見るかの選択（workspace のローカル設定）と「最新」の解決は消費側の責務で、新旧の順序は revision 名の大小ではなく保存側の確定記録で決める（Architecture §20 の「revision 名の大小で新旧を判定しない」に従う）。選択の仕組みは後続。
10. **取得側が既存 file を再取得せずに済む保存形式:** Content は ZIP にせず、revision の prefix 配下に file 単位の object として置く。取得側が manifest の SHA-256 で不足 file だけを判定して取れる形にする。これは転送量を減らすための取得側の再利用の前提であり、差分 upload は採らない（revision は全 file を丸ごと上げる）。再利用の仕組み（`ContentInstaller` の拡張、cache との関係）は後続。

受け入れ条件（上の観測可能な詳細。別の完了バーにしない）:

- program 文書の §0（revision を `r7`、`updated`）、§2（Content 節）、§4 B の後続一覧が上記 1〜10 と前提の節に矛盾なく更新されている。
- 同じ事実を 2 箇所に置かない（置き場・系列は §2 に 1 回だけ書き、他は参照）。
- `tools/Artifacts/README.md` は変えない（r7 は未実装の契約。実装済みの操作の説明に混ぜない）。`docs/README.md` の program 欄は必要なら 1 文だけ更新する。
- `pwsh tools/docs-audit.ps1` と `pwsh tools/contract-audit.ps1` が通る。

### ここでは答えない問いと所有する後続スライス

| 問い | 所有 |
|---|---|
| Unity 側の変更要否（Player の content 参照先）。候補は「未決事項の詳細」の U1〜U3。外部 revision を使う Player は BuildIdentity 一致を要求し、DIST cache の `installed/` 配置と receipt が必要 | Content 分離の実装スライス（着手時に A1 から。A1 でスパイクを先に切る可能性あり） |
| Player と Content の互換判定を、BuildIdentity 一致から計算した互換キーへ替える案（運用中アプリの Content のみ更新） | 同上の A1 の最初の選択肢。入力に Unity バージョン、root schema、Player のコード hash、埋め込みリソース hash 等を想定。安全側（フルビルド要求が多め）から始める |
| `build collect` の Player が `content/` を内包しない場合の検査・`build-info.json`・selection の変更 | Content 分離の実装スライス |
| Content の退避・publish の CLI（collect の content 版）、R2 key、完了確定点（全 file 後の完了マーカー）、receipt、publish の順序（Content を先か Player を先か） | Content 保存の実装スライス |
| 取得側の既存 file 再利用（`ContentInstaller` の拡張、cache の eviction・lease との関係、アプリ更新を越える cache の永続性） | Content 取り込みのスライス（Unity 変更を含む） |
| 統合フォルダからの取り込み方式（取得型／監視型）、複数ワークスペースの衝突規則、ローカル設定の形式、選択 UI、「最新」の解決 | 統合 Content フォルダのスライス |
| 保存側の確定記録（新旧順序の根拠） | Content 保存の実装スライス |
| Unity バージョンと root schema への束縛（Unity 更新で既存 revision が取り込めなくなる） | Content 取り込みのスライス |
| DIST cache が target を持たない点と、保存側の target との写像 | Windows 以外の target を扱うスライス |
| `contentSet` の命名規約と既存値（`bs4-spring-full`）の扱い（用途 × 構成 × 性能の合成名になる懸念） | Content 保存の実装スライスの A1 |
| Content の上限値、R2 のプラン変更の要否、R2 の操作回数の枠（file 単位 object による PUT 数） | 4 季節 Content の実測を含む上限スライス（program §4 B の既存の後続の入力） |
| ArtifactsRoot の件数清掃・`.staging` 回収、Content の実 DELETE と利用中状態の収集（保持中 Player との対応を含む） | 件数保持の運用接続スライス（既存の後続） |
| transport manifest v1 の固定 `target` の拡張（Windows 以外の platform） | Windows 以外の target を扱うスライス（program §4 B の既存の後続） |
| Player の起動時 `contentSet` 切り替え | 行わない（対象外。必要になれば別途 owner が判断） |
| 診断の改善（`publish` 表示、240 字超、二度読み、`stage-conflict`） | 件数保持の運用接続スライスの入力（既存。変更なし） |
| 開発環境セットアップ script（`OSM_ARTIFACTS_ROOT` 等） | セットアップスライス（既存の後続） |
| 別 host / Cloud 配布 | program §4 C |

### 判定定義と停止規則

- GO: 最低条件 1〜10 を満たす r7 が A3 で凍結され、文書 PR が `develop` へ入る。
- NO-GO: 保存形式が Player 分離の方式に依存して 1 つに決まらないと分かった場合。この場合は U2 のスパイクを先に切る問いへ分け、r7 は保留する。
- 停止規則: 最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。未達のまま終了しない。実装・検証の追加（実 R2 往復、Unity 実行）は求めない。
- A3 後の例外承認: なし
- 本文へ転記した実装制約: §4 の同名節
- 未決事項: §4 の「未決事項の詳細」

## 3. 責務マップ

| ファイル | 責務 | 現在行数 | 予想増分 | 備考 |
|---|---|---|---|---|
| `docs/handoff/BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` | program 全体の保存契約。r7 の本文はここ | 136 | +35〜45 | §2 に Content 節と前提を足し、§4 B の後続一覧を置換する。500 行未満・責務は変わらない |
| `docs/README.md` | program 一覧の状態文 | 77 | 0〜+1 行 | 状態が変わる場合だけ |
| 本書 | r7 Phase A の作業指示 | 約 190 | — | r7 のマージ後、Phase D で保存・引渡し確認のうえ削除（program に harvest 済み） |

新しい恒久文書は作らない。Content の契約は program §2 に置き、実装済みの操作は後続スライスが `tools/Artifacts/README.md` に置く。

## 4. 実装計画・未決事項の詳細・制約

### 実装計画

- 変更対象: 上表のとおり。コード・スクリプト・Unity アセットは変更しない。
- 順序: A2 第 2 ラウンド → A3 で採否を記録 → program 文書の r7 を書く → docs-audit / contract-audit → PR。
- Phase B から Phase A へ差し戻す条件: Content の保存形式が Player 分離の方式に依存して 1 つに決まらないと分かった場合。
- 対象外を維持する方法: r7 の文章に実装手順・コマンド名・R2 key を書かない。書きたくなったら後続スライスへ移す。

### 未決事項の詳細

#### Unity 側の変更要否（Player の content 参照先）

問い自体は後続の所有だが、保存契約が拘束されるかを見るため候補を記録する。r7 は候補のどれも選ばない。

| 候補 | 内容 | Unity 変更 | 保存契約への影響 |
|---|---|---|---|
| U1 | Player は今のまま `content/` を内包。Content は同じ revision を別途保存（重複保存） | なし | Player publish が 512 file を超える問題は残る。分離の動機を満たさない |
| U2 | Player build が `content/` を出さず、起動側が既存の `content:installedRevisionPath` + `content:manifestSha256` で外部 revision root を指す | Player build の `content/` 省略。外部 revision を指す場合に baked `relativeDirectory` が空でよいかは未確定（空不可なのは外部を指さない経路のみとの指摘があり、後続で確認） | 外部 revision root は DIST cache の `installed/` 配置と receipt を要する。保存側（`<ArtifactsRoot>/content/...`）はその取り込み元（最低条件 3）。r7 の単位 1 と整合する |
| U3 | 新しい config key で Content root を指す | 公開面（config スキーマ・protected key）の追加 | Phase A 再開級。r7 では選ばない |

Editor は既に Use For Next Play で明示した pair を選び、local directory の revision を取り込み元にできる（確認済み）。用途の切り替えは、ローカル設定で系列を指定して取り込むことで足りる見込み。Player は用途を切り替えられない（前提の節）。

U2 が成立するかは未確認。確認する点は、(a) Player build が `content/` なしで成立するか、(b) 外部 revision root を Player 起動時に渡す経路（app-config / 環境変数 / command line）が、directory Player の「生成 bootstrap Scene と runtime 名で latch」という起動契約と両立するか、(c) `InstalledRevisionVerifier` が `target` を `StandaloneWindows64-Player` に固定している点、(d) 外部 revision を使う Player が BuildIdentity 一致を要求する点。いずれも Unity 実行を伴うため、後続スライスの A1 で扱う。**未確認。確認は Content 分離の実装スライス。**

#### hd/sd

`representation` は Content の候補ごとの属性（`BuildContentProjection`）で、1 候補につき 1 値、曖昧なら `InvalidRepresentation`。Player の config では起動時に 1 値に固定される。1 revision 内に複数 representation を同梱できる可能性が高いが、同梱の可否は未確認。最低条件 5 の暫定案を採る。

#### その他

- N の既定値（Build に揃えて 10）。確定は実装スライス。
- 880 file の推定は実測ではない。実装スライスの A0 で実測してから動機として引用する。
- file 単位 object にすると PUT 回数が file 数だけ増える（例 880 回）。R2 の操作回数の枠への影響は実測後に後続で見る。

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
- 未知の操作経路の疎通結果: なし。U2 の成立確認は実装スライスで行う旨を「未決事項の詳細」に記録済み。
- 人間の判断が必要な条件: なし。A3 の採否は owner が行う。
- 機械検査: 上記 audit。
- A0/A1 主担当・モデル・ベンダー: Claude Sonnet 5.5（Anthropic）。
- A2 第 1 ラウンド（入力版 rev2、commit `3708889`）: ①契約の過不足と事実検証、②アーキテクチャゲート、③A0 のみからの代替構成。モデルは Claude Opus 2 件、Claude Sonnet 1 件で、すべて同一ベンダー（Anthropic）。互いの指摘は見せていない。
- A2 第 1 ラウンドの結果と rev3 での扱い（owner の方針による。A3 で最終確認）:
  - Player は自分の BuildIdentity と同じ revision にしか結び付かない（高、2 件が独立に指摘）→ 前提の節と最低条件 2 の保護に反映。互換キーは後続の最初の選択肢。
  - 独立種別か Build 付随か（高）→ 独立種別を選択（A0 の節）。
  - 置き場が DIST cache と別層（中、2 件）→ 最低条件 3 に反映。
  - 最低条件 8〜10 が問いを超える（中）→ 9 を縮小、Player 固定は前提へ移動。
  - 「最新」の順序は §20 と緊張（中）→ 最低条件 9 に保存側の確定記録を明記。
  - 系列 key の値の規則（中）→ 最低条件 2 に原則を追加。
  - 受け入れ条件・GO が 1〜7 のまま、状態欄・行数の不一致、上限の書き方、移送先の食い違い → 修正。
  - 事実の訂正（Editor は local directory 可、`relativeDirectory` の空不可は外部を指さない経路のみ）→ 反映。
  - 代替構成レビュー（A0 のみ）は初稿とほぼ同じ構成に収束。完了マーカー、revisionId の衝突、参照中保護は後続の入力として採用。
  - owner の追加要件（運用中アプリの Content のみ更新、アプリ更新でも全 file の再取得を避ける）→ 最低条件 10 と互換キー案。
- A2 第 2 ラウンド: 入力版 rev3 で、第 1 ラウンドの指摘が解消しているかと、新たに入れた最低条件 8〜10・前提の節・移送表を、第 1 ラウンドの結果を渡さずに独立に確認する。
- A3 統合担当・採否: 未実施。
- C' 用に予約した担当: 未予約。
- 独立性の強化条件を満たせない場合の理由: 現時点で A2 が全員同一ベンダー（Anthropic）。C' は別系列を予約できていない。

## 6. Phase B 実装結果

未着手

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手
