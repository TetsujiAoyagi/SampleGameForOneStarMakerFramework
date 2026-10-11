# Artifact Storage program r7: Content 種別の追加（Phase A）

## 0. メタデータ

- type: slice（文書 revision。program 文書 `BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` を r6 → r7 にする）
- status: B（A3 凍結済み rev4。2026-10-11、owner の指示）
- branch: `codex/artifact-storage-content-kind-phase-a` / PR base: `develop`
- implementation base commit: `4d5dcc379a31bd6d47713d1072e47bed020061ee`（`develop`、PR #116 を含む）
- implementation head commit: 未実装（Phase B で記録する。実装は文書改訂のみ。コードは変えない）
- risk: normal（保存契約の文書。ただし保存形式が後続の Player 構成と R2 の object 構成を拘束するため、A2 で保存形式の拘束を確認済み）
- owner: OSM maintainers
- created: 2026-10-11
- expires: 2026-11-10 または置換 revision。期限時は owner が更新・廃止を判断する
- harvest to: r7 の確定内容は `BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §2/§4 に入る（本書の目的そのもの）。実装済みの操作は後続スライスが `tools/Artifacts/README.md` へ反映する
- Phase A snapshot の取得案内・状態: 未保存（ローカルで生成。Artifact Storage への publish は本依頼の範囲外）
- Phase B result snapshot の取得案内・状態: 未生成（Phase B で生成）
- evidence bundle の取得案内・状態: 未生成（未到達）
- C' blind bundle の取得案内・状態: 未生成（未到達）

この slice は文書作成までで、artifacts の publish/fetch・資格情報・Unity・コードに触れない。

## 1. 目的と対象外

- 目的: 開発用 Artifact Storage の単一保存契約に、Player とは独立した Content Directory の保存種別を足し、その契約の骨格（単位・系列・置き場・保持・保存形式の制約）を固定する。
- 対象外: 下記「ここでは答えない問いと所有する後続スライス」の全項目。コード変更、`build collect` の拡張、R2 の key の確定、Unity の Player 構成変更、ArtifactsRoot の清掃実装。

### 現況（base `4d5dcc3` で確認）

- program §2 の保存種別は **開発Build** と **開発Evidence** の2つ。Content は種別として存在しない。§4 B は Content を「program r7 revision（Content種別を追加する Phase A）」へ送っている。
- PR #115 の `build collect` は Player 1 件を `<ArtifactsRoot>/players/<project>/<target>/<configuration>/<buildId>/` へ退避して既存 `build publish`（ZIP 1 個を 1 PUT）に渡す。Player は `content/` を内包し、検査は `content/` の全 file が `transport.json` の `files` と一致することを要求する。`build-info.json` に `contentSet`・`contentTarget`・`buildIdentity`・`transportManifestSha256` を持つ。#111 の選択集合の上限は 512 file・240 MiB・単一 256 MiB（ZIP も 256 MiB）で、R2 側の上限ではなく自前の上限。
- `ContentTransportPublisher` は Content を `transport.json` + `content/` の形で出す。manifest は `contentSet`・`revision`・`target`・`unityVersion`・`rootSchemaVersion`・`playerConfigSchemaVersion`・各 file の size/SHA-256 を持つ。`revision` は Build ごとに新しく作られる Build identity（時刻 + GUID）で、1 revision が不変単位。transport manifest v1 の `target` は固定値 `StandaloneWindows64-Player`（Architecture §13）。現在 transport を出すのは Player Build だけ（`SampleGamePlayerBuildCoordinator`）。
- 取り込み側（Unity）は file 単位で読む。`IContentArtifactSource.OpenReadAsync(relativePath)` を Local / HTTP が実装し、`ContentInstaller` は manifest の全 file を新しい staging へ取得・検証して `installed/<contentSet>/<revision>` へ公開する。同一 revision・同一 digest の丸ごとの再利用はあるが、別 revision の file を再利用する処理は無い。既存の HTTP の口は認証ヘッダーを付けず redirect も無効で、private な R2 を直接は読めない。取り込み元に local directory の revision（`transport.json` + `content/`）を使える（Architecture §20）。
- Player の content 参照は 2 通り既に存在する。(a) baked の `content:relativeDirectory`（SampleGame は `"content"`）が指すパッケージ内 directory。(b) `content:installedRevisionPath` と `content:manifestSha256` の pair による DIST managed revision root。(b) は `InstalledRevisionVerifier` が receipt・manifest digest・contentSet・revision・target・全 file を照合し、revision には Player の baked BuildIdentity を渡す（`PlayerContentConfiguration.cs:30`）。つまり Player が使える revision は、自分と同じ Build identity のもの 1 つに限られる。pair は Player の protected key に含まれず、片方だけの指定は拒否される。外部 revision root は `…/installed/<contentSet>/<revision>` の形で `receipt.json` を持つ必要がある（DIST cache の配置。target の階層は無い）。
- 「4 季節で約 880 file」は owner の推定で、base では実測していない。

## 2. 意思決定と受け入れ境界

### A0 の確定: r7 が要るか

**要る。** 次の 3 点は program §2 の単一契約を変えるため、後続スライスの A3 で個別に決めると契約が割れる。

1. 寿命が違う。Content は revision 単位の不変物で、Editor は任意の revision を取り込める。Build の `project/target/configuration` 系列にも Evidence の task 終了にも属さない。
2. 保持の単位が違う。系列は `contentSet/target`、削除単位は revision 丸ごと。§2 の保持契約への追加であり、README の運用だけでは決められない。
3. 保存形式は、Player/Content の分離方式（U1〜U3）のどれを後続が選んでも変わらないことを確認したうえで固定する。ZIP 1 個の Build 保存形式のままでは Player の 512 file 上限に縛られるため、Content の保存形式は Build と分ける必要がある（owner の指示）。

**独立種別か、Build 付随か。** 「`buildId` が同じ Content を Build の別 bundle として同じ系列・保持に置く」代替がありうる。owner は独立種別を選んだ。理由は、Editor が Build に結び付かない任意の revision を取り込む要件と、将来の Content 単独 Build を受けられること。A3 で採用した。

### このスライスが答える問い（1 つ）

> Content Directory を、Player とは独立した第三の保存種別として program §2 に足すとき、保存の単位・系列・置き場・保持と、保存形式の制約をどう固定するか。

「どう実装するか」「どの順で作るか」は含めない。答えは program 文書 r7 の本文と、後続スライスへの移送先の確定である。

### 前提（保存契約ではなく Unity の現状。r7 は変えない）

- Player は自分の Build 時の `contentSet` と BuildIdentity に結び付く。起動時の切り替えは行わない（Build 時の埋め込みリソースと整合しないため）。
- したがって現状、配布済み Player に Content だけの revision を差し替える運用はできない。この前提を変える案（Player と Content の互換を計算した「互換キー」で判定する）は、Content 分離の実装スライスの A1 で最初の選択肢にする。保存契約は、どちらの案でも変わらない。そのため保護は最低条件 2 のとおり「保持中 Player が受け入れうる revision の集合」と抽象化し、manifest の schema を固定しない（最低条件 1）。

### 進める最低条件

次スライス（Content 保存の最初の実装スライスの Phase A）を起こしてよいと判定できる条件は、r7 の本文が次をすべて固定していること。

1. **単位:** revision ごとに、`transport.json` + `content/` という配置を丸ごと不変単位とする。revision の上書きは行わない。manifest の schema は保存側にとって中身を見ないもので、検証は消費側が行う（互換キー等の将来の field 追加を縛らない）。差分 upload・content-addressed・GC を採らない旨を書く。
2. **系列と削除:**
   - 系列は `contentSet/target`。platform は target 値で表す。系列とパスに使う target の値は 1 つで、manifest の `contentTarget` から一意に写像できること（Build の系列 target は小文字の正規化値、`contentTarget` は別の値。具体的な対応表は実装スライス）。
   - `contentSet` は大文字・`_`・`.` を許すが、ローカル（NTFS と DIST の配置判定）は大文字小文字を区別しない。大文字小文字だけが違う `contentSet` や revision を別のものとして受け入れない。パス要素として安全な範囲に限る。具体規則は命名規約の後続。
   - 古い revision の削除は Build と同じ件数保持 policy を別系列として適用する（成功のみ N に数える、利用中保護、失敗は既存を追い出さない）。N の既定値は Build に揃える案で、確定は実装スライス。
   - **保持中の Player が受け入れうる Content revision は利用中として保護し、N に数える。** 現状の Unity 契約では、Player の `build-info.json` の `contentSet`・`contentTarget`・`buildIdentity`・`transportManifestSha256` で 1 件に決まる。保護状態を収集できないときは削除しない（fail-closed）。保護状態の収集は後続。
   - 件数保持が R2 とローカルのどちらの層に効くかは、層ごとの対応を件数保持の運用接続スライスで明示する（現状 ArtifactsRoot の退避先は件数清掃の対象外）。file 単位 upload が途中で止まったものを §2「未完了 upload と staging」に載せるかは実装スライスで決める。
3. **置き場:** ローカルは `<ArtifactsRoot>/content/<contentSet>/<target>/<revision>/`。ArtifactsRoot は Player と共用の任意のローカル folder。これは取り込み元（install source）であり、消費側の DIST cache（`installed/<contentSet>/<revision>`、known-good・lease・eviction を持つ）とは別の層である。削除する主体は層ごとに別で、同じ revision を二重に消さない。R2 にも revision 単位で全 file を上げる。
4. **全 platform で 1 形式:** 保存形式は platform で分けない。ただし transport manifest v1 の `target` が固定値である制約を、保存契約の前提として明記する。
5. **hd/sd の受け止め:** 端末性能別の違いが `contentSet` か representation で表せるか、系列 key の拡張で足りるかを、現況の根拠付きで結論する。暫定案は「第一候補は representation、revision を割る場合は contentSet に含める。系列 key は `contentSet/target` から変えない」。
6. **上限の扱い:** r7 は新しい上限を足さない。Content は ZIP 1 個にしないため、#111 の選択集合の上限（512 file・240 MiB）や ZIP の 256 MiB は Content の前提にしない。Content 固有の上限値と R2 のプラン変更の要否は、4 季節 Content の実測後に後続で決める。
7. **移送:** 下記「ここでは答えない問いと所有する後続スライス」の各項目に所有する後続スライス名を付ける。program §4 B 末尾の「後続の問い」一覧を r7 で置換する。
8. **用途識別と昇格しない原則:** 用途（通常開発 / 期間限定イベント / 次期大型アップデート等）は `contentSet` の名前に持たせる（例 `mainline`、`event-<名>`、`next-major`）。階層は足さず、manifest・検証・Player の baked 設定を変えない。系列と件数保持は用途ごとに独立する。**revision の昇格・改名は行わない。** 系列間の統合はソース側で行い、統合先の `contentSet` で Build し直す。
9. **可変の別名を持たない:** ストレージに latest 等の可変の別名を持たせない。消費側は revision と manifest の SHA-256 で固定する。どの系列を見るかの選択（workspace の git 管理外のローカル設定）と「最新」の解決は、Unity の DIST の外（取り込み元を選ぶ側）で行い、Unity には revision と digest を明示して渡す。新旧の順序は revision 名の大小ではなく保存側の確定記録で決める（Architecture §20 に従う）。確定記録は digest も持つ必要がある（後続）。選択の仕組みは後続。
10. **取得側が不足 file だけを取れる保存形式:** Content は ZIP にせず、revision の prefix 配下に file 単位の object として置く（`<prefix>/transport.json` と `<prefix>/content/<path>`）。manifest に載った各 file の SHA-256 で、どの取得者も不足 file だけを判定して取れる形にする。差分 upload は採らない（revision は全 file を丸ごと上げる）。再利用する箇所は「R2 から ArtifactsRoot」と「取り込み元から DIST cache」の 2 つで、それぞれの所有は後続に置く（Unity の `ContentInstaller` は名指ししない）。

受け入れ条件（上の観測可能な詳細。別の完了バーにしない）:

- program 文書の §0（revision を `r7`、`updated`）、§2（Content 節と前提）、§4 B の後続一覧が上記 1〜10 と矛盾なく更新されている。
- 同じ事実を 2 箇所に置かない（置き場・系列は §2 に 1 回だけ書き、他は参照）。
- `tools/Artifacts/README.md` は変えない（r7 は未実装の契約。実装済みの操作の説明に混ぜない）。`docs/README.md` の program 欄は必要なら 1 文だけ更新する。
- `pwsh tools/docs-audit.ps1` と `pwsh tools/contract-audit.ps1` が通る。

### ここでは答えない問いと所有する後続スライス

「新規」は program にまだ存在しない後続スライスで、r7 が名前と所有を与える。「既存」は program に実在するもの。

| 問い | 所有 |
|---|---|
| Unity 側の変更要否（Player の content 参照先）。候補は「未決事項の詳細」の U1〜U3。外部 revision を使う Player は BuildIdentity 一致を要求し、DIST cache の `installed/` 配置と receipt が必要 | 新規: Content 分離の実装スライス（着手時に A1 から。A1 でスパイクを先に切る可能性あり） |
| Player と Content の互換判定を、BuildIdentity 一致から計算した互換キーへ替える案（運用中アプリの Content のみ更新）。入力に Unity バージョン、root schema、Player のコード hash、埋め込みリソース hash 等を想定し、安全側（フルビルド要求が多め）から始める | 同上の A1 の最初の選択肢 |
| `build collect` の Player が `content/` を内包しない場合の検査・`build-info.json`・selection の変更 | 同上 |
| Content の退避・publish の CLI（collect の content 版）、R2 key、完了確定点（file を先、`transport.json` か完了マーカーを最後）、receipt、publish の順序（Content を先か Player を先か）、保存側の確定記録（新旧順序と manifest digest）、`contentSet`/`target` の値の対応表 | 新規: Content 保存の実装スライス |
| R2 から ArtifactsRoot への取得（private な R2 は Unity の HTTP の口では読めないため、認証を扱える Artifact CLI 側）と不足 file の再利用 | 新規: Content 保存の実装スライス（取得側の CLI） |
| 取り込み元から DIST cache への不足 file の再利用（read lease を取ったコピー。hardlink は容量計測と tombstone の前提を崩す）、cache の eviction・lease との関係、アプリ更新を越える cache の永続性、Unity バージョンと root schema の完全一致要求、HTTP の file ごとの deadline | 新規: Content 取り込みのスライス（Unity 変更を含む） |
| 統合フォルダからの取り込み方式（取得型／監視型）、複数ワークスペースの衝突規則、統合フォルダと ArtifactsRoot の同一性、共有時の削除主体、ローカル設定の形式と選択 UI、「最新」の解決 | 新規: 統合 Content フォルダのスライス |
| `contentSet` の命名規約（用途 × 構成 × 性能の合成名の扱い、既存値 `bs4-spring-full` を改名しないこと、ブランチや Build 設定から決める方法、大文字小文字とパス要素の安全規則）、hd/sd を 1 revision に同梱した場合の部分取得 | 新規: Content 保存の実装スライスの A1 |
| Content の上限値、実測（4 季節 Content の file 数とサイズ。880 file の推定の検証）、R2 のプラン変更の要否、R2 の操作回数の枠（file 単位 object による PUT 数）、R2 の途中失敗で残る object の孤児回収 | program §4 B の容量上限の固定（B の A3 の入力として既存）。実測は新規: Content 保存の実装スライスの A0 |
| ArtifactsRoot の件数清掃・`.staging` 回収、Content の実 DELETE と利用中状態の収集（保持中 Player との対応、保護が N を超えた場合の挙動）、系列の退役規則（イベント終了時に local・R2・統合フォルダ・DIST cache を片付ける） | 既存: 件数保持の運用接続スライス |
| transport manifest v1 の固定 `target` と DIST cache が target を持たない点の拡張（Windows 以外の platform） | r7 は前提として明記するにとどめ、扱う新規スライスは未定: Windows 以外の target を扱うスライス |
| Player の起動時 `contentSet` 切り替え | 行わない（対象外。必要になれば別途 owner が判断） |
| 診断の改善（`publish` 表示、240 字超、二度読み、`stage-conflict`） | 既存: 件数保持の運用接続スライスの入力（変更なし） |
| 開発環境セットアップ script（`OSM_ARTIFACTS_ROOT` 等） | 既存: セットアップスライス |
| 別 host / Cloud 配布 | 既存: program §4 C |

### 判定定義と停止規則

- GO: 最低条件 1〜10 を満たす r7 が、凍結済みの受け入れ条件どおりに program 文書へ反映され、docs-audit / contract-audit が通る文書 head。
- NO-GO: 保存形式が Player 分離の方式に依存して 1 つに決まらないと分かった場合。この場合は U2 のスパイクを先に切る問いへ分け、r7 は保留する。
- 停止規則: 最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。未達のまま終了しない。実装・検証の追加（実 R2 往復、Unity 実行）は求めない。
- A3 後の例外承認: なし
- 本文へ転記した実装制約: §4 の同名節
- 未決事項: §4 の「未決事項の詳細」

## 3. 責務マップ

| ファイル | 責務 | 現在行数 | 予想増分 | 備考 |
|---|---|---|---|---|
| `docs/handoff/BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` | program 全体の保存契約。r7 の本文はここ | 136 | +40〜55 | §2 に Content 節と前提を足し、§4 B の後続一覧を置換する。500 行未満・責務は変わらない |
| `docs/README.md` | program 一覧の状態文 | 77 | 0〜+1 行 | 状態が変わる場合だけ |
| 本書 | r7 の作業指示と A2/A3 の記録 | 約 200 | — | r7 のマージ後、Phase D で保存・引渡し確認のうえ削除（program に harvest 済み） |

新しい恒久文書は作らない。Content の契約は program §2 に置き、実装済みの操作は後続スライスが `tools/Artifacts/README.md` に置く。

## 4. 実装計画・未決事項の詳細・制約

### 実装計画

- 変更対象: 上表のとおり。コード・スクリプト・Unity アセットは変更しない。
- 順序: program 文書の r7 を書く → docs-audit / contract-audit → Phase C / C'。
- Phase B から Phase A へ差し戻す条件: Content の保存形式が Player 分離の方式（未決事項の詳細）に依存して 1 つに決まらないと分かった場合。
- 対象外を維持する方法: r7 の文章に実装手順・コマンド名・R2 key を書かない。書きたくなったら後続スライスへ移す。

### 未決事項の詳細

#### Unity 側の変更要否（Player の content 参照先）

問い自体は後続の所有だが、保存契約が拘束されるかを見るため候補を記録する。r7 は候補のどれも選ばない。

| 候補 | 内容 | Unity 変更 | 保存契約への影響 |
|---|---|---|---|
| U1 | Player は今のまま `content/` を内包。Content は同じ revision を別途保存（重複保存） | なし | Player publish が 512 file を超える問題は残る。分離の動機を満たさない |
| U2 | Player build が `content/` を出さず、起動側が既存の `content:installedRevisionPath` + `content:manifestSha256` で外部 revision root を指す | Player build の `content/` 省略。外部 revision を指す経路では baked `relativeDirectory` の空を拒否しない（空不可は外部を指さない経路のみ）ため、仮値が要るかは後続で確認 | 外部 revision root は DIST cache の `installed/` 配置と receipt を要する。保存側（`<ArtifactsRoot>/content/...`）はその取り込み元（最低条件 3）。r7 の単位 1 と整合する |
| U3 | 新しい config key で Content root を指す | 公開面（config スキーマ・protected key）の追加 | Phase A 再開級。r7 では選ばない |

Editor は既に Use For Next Play で明示した pair を選び、local directory の revision を取り込み元にできる（確認済み）。用途の切り替えは、ローカル設定で系列を指定して取り込むことで足りる見込み。Player は用途を切り替えられない（前提の節）。

U2 が成立するかは未確認。確認する点は、(a) Player build が `content/` なしで成立するか、(b) 外部 revision root を Player 起動時に渡す経路（app-config / 環境変数 / command line）が、directory Player の「生成 bootstrap Scene と runtime 名で latch」という起動契約と両立するか、(c) target の固定値が起動時の初期化処理（`AbstractApplicationInitializer`）と cache（`ContentCacheStore`）にある点（`InstalledRevisionVerifier` は target を引数で受ける）、(d) 外部 revision を使う Player が BuildIdentity 一致を要求する点。いずれも Unity 実行を伴うため、後続スライスの A1 で扱う。**未確認。確認は Content 分離の実装スライス。**

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
- 全 EditMode 回帰の適用除外: 適用外。理由は文書のみの変更で Unity 資産・コードを変えないため。代替証拠は上記 2 つの audit と、program 文書の参照整合の確認。
- 統合・Unity テスト: なし。
- 操作・実行時・目視条件の検証経路: なし（人間の操作・Unity 実行・実機観察を含む最低条件を置かない）。
- 未知の操作経路の疎通結果: なし。U2 の成立確認は実装スライスで行う旨を「未決事項の詳細」に記録済み。
- 人間の判断が必要な条件: なし。
- 機械検査: 上記 audit。
- A0/A1 主担当・モデル・ベンダー: Claude Sonnet 5.5（Anthropic）。
- A2 第 1 ラウンド（入力版 rev2、commit `3708889`）: ①契約の過不足と事実検証（Claude Opus）、②アーキテクチャゲート（Claude Opus）、③A0 のみからの代替構成（Claude Sonnet）。互いの指摘は見せていない。
- A2 第 2 ラウンド（入力版 rev3、第 1 ラウンドの結果を除いた版）: ①契約整合（Claude Opus）、②アーキテクチャゲート（Claude Opus）、③運用要件の充足（Claude Sonnet。当初 Fable を割り当てたが使用クレジット不足で実行できず、Sonnet に替えた）。
- A2 の独立性: 全員が同一ベンダー（Anthropic）で、A0/A1 主担当とも同系列。独立性の強化条件は満たしていない。
- A3 統合担当: Claude Sonnet 5.5 と owner（2026-10-11、凍結の指示）。
- A3 の採否:
  - 採用: 独立種別（Build 付随の代替は不採用）。Player は BuildIdentity と対（前提）と、保持中 Player が受け入れうる revision の保護（最低条件 2）。保護は集合として抽象化し、manifest の schema を固定しない（最低条件 1、前提）。
  - 採用: ArtifactsRoot は取り込み元、DIST cache は消費側の 2 層（最低条件 3）。
  - 採用: 可変の別名なし、新旧は保存側の確定記録、「最新」の解決は Unity の外（最低条件 9）。
  - 採用: file 単位 object と、取得側が各 file の SHA-256 で不足 file だけを取る前提。再利用箇所は 2 つで所有は後続、`ContentInstaller` は名指ししない（最低条件 10）。
  - 採用: target の値の一意な写像、`contentSet` の大文字小文字の原則、fail-closed、保護キーへ `contentTarget` を追加（最低条件 2）。
  - 採用: 昇格しない原則（最低条件 8）。上限は新しく足さない（最低条件 6）。
  - 採用: A0 の根拠 3 の言い換え、GO 定義の変更、移送表の「新規/既存」の明記と 880 file の実測の所有の一本化、事実の訂正。
  - 保留（後続へ）: 互換キー、Unity バージョン・root schema の束縛、DIST cache の target、命名規約、系列の退役、共有フォルダの削除主体、孤児回収、publish の順序、HTTP の認証と deadline。
  - 不採用: A2 の第 3 ラウンド。第 2 ラウンドの指摘は文言の修正で、設計判断を変えないため。
- C 用: Phase B と異なるモデル・新規セッション。C' 用: B、C と異なるモデル・新規セッション・blind audit bundle。利用可能なのは Anthropic のモデルのみで、B は Claude Sonnet、C は Claude Opus、C' は Claude Haiku を割り当てる。別系列・別ベンダーの予約は不可。
- 独立性の強化条件を満たせない場合の理由: 現環境で利用可能な AI が Anthropic のモデルに限られる（Fable は使用クレジット不足）。C' は独立性制約ありとして記録し、人間が採否を判断する。

## 6. Phase B 実装結果

未着手

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手
