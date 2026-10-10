# Build成果物の保存接続（Player Build → buildId退避 → selection自動生成 → publish）

この文書は Phase A の A0 planning packet（§A0）と A1 初稿（§0〜§9）を持つ単一スライスHANDOFFである。A2 独立レビューの入力版は §5 に記録する。A3 で凍結するまで実装キューではない。

## A0. Planning packet（入力固定）

A2 の代替構成担当はこの節だけを読む。§0 以降の A1 初稿を読まずに構成を出せるよう、事実・要求・制約・未決をここで閉じる。

### A0-1. 入力の正本

- `docs/handoff/BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §2（単一の保存契約／開発Build）、§4 B・D、§5（責務・残す安全性）。policy revision `r6`。
- `tools/Artifacts/README.md` の「開発Build 1件の保存と取得」「開発Buildの件数保持policy」。
- [PR #110](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/110)（純粋な件数保持policy、運用未接続）、[PR #111](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/111)（既存Build 1件の検証付きpublish/inspect/fetch）。
- Content側の参照: `unity/Assets/OneStarMaker/Scripts/Editor/Build/Content/ContentTransportPublisher.cs`、`unity/Assets/Docs/Architecture/13-resource-system.md`「DIST transport と disk cache の境界」。
- planning base: `develop` `32ec358`。

### A0-2. 現況（2026-10-11 にコードと手元出力で確認）

**依頼文の前提との差。** 依頼文は「Player出力は `unity/Builds/ActiveVariant` の固定pathで毎回上書きされる（`VariantPlayerBuild.cs`）」としていた。現行コードでは次のとおりで、この前提は既に解消済みである。

- `VariantPlayerBuild.BuildActiveVariant`（menu `OneStarMaker/Build/Build Player (Active Variant)`）は退役済みで、警告を出して何もしない。`unity/Builds/ActiveVariant` は空のディレクトリである。内部の `UnityPlayerBuildBackend` は固定pathへ出力するが、menuからは到達しない。
- 現行のPlayer Build入口は `SampleGamePlayerBuildCoordinator.Build()`（menu `Tools/OSM/Content/Build BS4 Spring Player`、`unity/Assets/SampleGame/DependOnAll/Editor/Build/`）である。Content build → `ContentTransportPublisher.Publish` → Player build（IL2CPP / stripping High / `StandaloneWindows64` Player固定）→ `PlayerBuildReportVerifier` → `PlayerBuildPublisher.Publish` の順に1回で実行する。
- 出力は **repo内・gitignore済み** の `<repo>/artifacts/bs4/` 配下で、build identity（`yyyyMMddTHHmmssfffZ-<32hex>`）ごとに分かれる。
  - `work/<identity>/`: Unity生出力（IL2CPP backupを含む）。publish後も残る。
  - `transport/<identity>/`: Content transport（`transport.json` + `content/`）。
  - `players/<identity>/`: 配布形Player。Player本体 + `content/`（Content Directoryの複写）+ `build-receipt.json`。backupディレクトリは除外。staging→rename、既存identityは拒否して上書きしない。
- `build-receipt.json` は identity、target、`transportManifestSha256`、`playerBuildGuid`、backend、stripping、scenes、checkedRootGuids に加え、**ローカル絶対path**（`contentReportPath`、`transportPath`）を持つ。
- Player実行時設定（runtime JSON）は `content.relativeDirectory = "content"` で、Player隣接の `content/` を読む。つまり現行のPlayerは同じidentityのContentを内包して初めて起動できる。
- 保存側（#111）: `tools/artifacts.ps1 build publish|inspect|fetch` は実装済み。selection は `schemaVersion=1, purpose=build`、絶対`root`、root内全fileと一致する相対`files`、`project/target/configuration`（小文字英数字・ハイフン1〜64字）、事前保存した32桁hex `buildId` を持ち、**32桁hex名の private operation ディレクトリ直下**に置く必要がある。入力rootと所有store（LocalApplicationData `OneStarMaker/Artifacts/builds-v1/...`）の親子関係は拒否される。上限は最大512 file・選択総量240 MiB・単一fileとZIP各256 MiB・相対path240字。
- 件数保持（#110）: 純粋policy `Get-BuildRetentionDecision` のみ。系列一覧・inUse収集・実DELETEは未接続。

**痛み（現行に合わせて言い直し）。** Build自体は identity 単位で残るが、(1) publish には selection JSON と build-config を手で書く必要がある、(2) 保存単位がrepo内 worktree に縛られ、worktreeを捨てると消える、(3) R2 publish に渡すメタデータ（contentSet・buildIdentity）の置き場が無く、`build-receipt.json` はローカル絶対pathを含むため そのまま配布物に含めてよいか判断が要る。

### A0-3. 実測（新規Unity Buildなし）

手元の既存出力を測った。Unity Build は実行していない。最新は 2026-09-24 の identity `20260924T202227427Z-b80d…`（Unity 6000.6.0f1、現行 `ProjectVersion.txt` と同一。以後のBuildコード変更はテストasmdef分離のみ）。#111 の実R2往復に使った 266 file のBuildと同じものである。

| 対象 | file数 | 総量 | 最大file | 最長相対path |
|---|---:|---:|---:|---:|
| `players/<identity>`（Player + content + receipt） | 266 | 125.49 MiB | 46.91 MiB | 109字 |
| うち `content/` | 220 | 5.45 MiB | — | — |
| うち Player本体（content除く） | 46 | 約120.0 MiB | 46.91 MiB | — |
| `transport/<identity>`（transport.json + content） | 221 | 5.77 MiB | 0.98 MiB | 45字 |
| `work/<identity>`（参考: Unity生出力） | 907 | 1,421.40 MiB | 531.04 MiB | 130字 |
| うち IL2CPP backup | 862 | 1,301.37 MiB | — | — |

- **#111 上限への適合:** 配布形 `players/<identity>` は 266/512 file、125.49/240 MiB、最大file 46.91/256 MiB、path 109/240字で収まる。余裕は file 246件・約114 MiB。
- `work/` をそのまま選ぶと 907 file・1.4 GiB・単一531 MiB で全上限を超える。publish対象は配布形に限る必要がある。
- 2026-09-19〜20 の旧3件は 178 file / 124.3 MiB（content 132 file）。Spring 1季節のcontentが 132→220 file に増えた一方、Player本体はほぼ一定。
- **推定（未実測）:** 4季節を同じ密度で1つのPlayerに内包すると content だけで約 880 file となり、512 file 上限を超える。Player本体は約46 fileで変動が小さい。上限に先に当たるのは容量ではなくContent由来のfile数である。この推定はPlayerとContentを別扱いにする最終形を支持するが、このスライスの判断材料にはしない（§A0-6）。

### A0-4. 要求

**owner が想定する最終形（全体像。最初のスライスで全部はやらない）**

- Player と Content Directory を別扱いにする。
- Player: repo外の `<ArtifactsRoot>/players/<project>/<target>/<configuration>/<buildId>/` に退避する。R2 は既存の `bundle.zip`。`build-info.json` に前提とする contentSet と buildIdentity を記録する。
- Content Debug: 既存transport形式（`transport.json` + `content/<Unity構成>`）を `<ArtifactsRoot>/content/<contentSet>/<revision>/` に置く。
- Content: 既存transport形式を revision ごとにまるごと不変単位として `<ArtifactsRoot>/content/<contentSet>/<target>/<revision>/` に置き、R2にも revision 単位でまるごと上げる。差分upload・content-addressed・GC は採らない。古いrevisionの削除は Player と同じ件数保持policy（系列 = contentSet/target）で行う。
- 全プラットフォーム（Windows / Steam Deck / Xbox / PS5 / Switch / Mac / iOS / Android）で保存形式を1つにし、プラットフォームは系列の target 値として扱う。プラットフォーム固有の差分配信・パッケージ変換は program 対象外。
- 将来（program外）: リリース向けパッケージ変換で Content の file 名を鍵付き path hash（HMAC）へ変換する。保存側は Unity 構成のまま持つ。
- 将来（夢）: 端末性能別のファイル分け（hd/sd 等）を contentSet または representation の違いとして配る。保存の系列keyが追加の値として受け止められることだけを確認し、実装しない。

**owner の追加入力（2026-10-11、A1 初稿の後）**

- **R2 の容量は費用を払って拡大してよい。** 保存総量の費用は制約にしない。ただし #111 の 512 file / 240 MiB / 単一 256 MiB は R2 の容量ではなく、単発PUT・ローカル展開検証を前提にしたツール側の上限である。上限を上げる（multipart 等）のは #111 の契約変更で、このスライスでは扱わない（§A0-6）。
- **Content の置き場は任意のローカルフォルダにしたい。** 例: あるワークスペースでは VFX だけを実開発し、残りはホワイトボックスのまま。この場合も、各ワークスペースの Content Directory のビルド成果物は1つの統合フォルダへ集める。他のワークスペースはそこを見て、必要なものを自分の Content フォルダへ持ってくる。あるいは Content Directory 側が統合フォルダを見て、専用フォルダへ取り込む。Content の保管形式と同じく program r7 の要求として扱う。このスライスへの影響は次の2点に限る。
  - `<ArtifactsRoot>` は worktree・ワークスペースに縛られず、利用者が任意のローカルフォルダを指定できなければならない。
  - 同じ `<ArtifactsRoot>` を複数のワークスペースが同時に使いうる。退避は既存の書き込み先を上書きせず、並行した書き込みで壊れない必要がある。

**このスライスの要求:** Player Build の完了後、人が selection / build-config を書かずに、Player成果物を buildId 単位のフォルダへ退避し、selection を自動生成して既存の `build publish` へ渡し、参照と独立した期待packageSha256を得る。

### A0-5. 常時契約・既知の制約（本文転記）

- 依存は Game → Framework の一方向。asmdef 参照の追加は設計判断であり勝手に行わない。Editor コードを Runtime アセンブリに置かない。新規・編集する Unity側 `.cs` は先頭に `#nullable enable`。Unity側で `record` を使わない。
- Unity テスト/Build の実行・判定は Phase C。`unity test` / `unity run` を使わない。テストで `Task.Delay` / `Thread.Sleep` を使わない。
- Artifact Storage の通常 publish/fetch は事前承認済み（設定した開発領域・明示入力・保存契約内）。資格情報の新規登録・置換、別account/bucket、公開配布、別host/Cloud は承認範囲外。鍵・header・署名URLを引数・ログ・Git・同期先へ出さない。認証失敗を鍵の再入力・削除で自動修復しない。
- program §5: 明示file集合、照合したsnapshot、固有key、信頼した期待hash、新規private展開先、path/reparse/衝突検査を維持する。payload を Git に置かない。公開 development URL を有効にしない。
- #111 の成功確定点（別process読戻し後の receipt atomic rename）、同一buildIdの再publish拒否、PUT応答不明時に自動再PUTしない契約を変えない。
- Build config は Evidence の task終了・30日期限を継承しない。branch を保存枠に入れない。生成元 revision が不明なとき現在の HEAD から推定して記録しない。
- PR の base は `develop`。

### A0-6. 対象外

- Content の保管形式・配置（`<ArtifactsRoot>/content/...`）・R2 への revision 単位 upload・Content の件数保持。program §2 は「開発Build」と「開発Evidence」の2種だけを定義し、Contentという保存種別を持たない。program revision が必要なため後続の問いとして送る（§2 ここでは答えない問い）。
- Player と Content の分離。現行Playerは `content/` 内包が起動条件（A0-2）。分離は Content 保管形式と同時に決める。
- 件数保持の運用接続・実DELETE・`<ArtifactsRoot>` のローカル清掃（program §4 B の残り）。
- Unity Editor から外部CLIを呼ぶ自動起動、BuildSystem/Harness 接続（program §4 D）。
- Windows 以外の target、HMAC path hash、端末性能別配信。
- `artifacts/bs4/work/` の肥大（1 Build 約1.4 GiB）の清掃。
- #111 の上限（512 file / 240 MiB / 単一 256 MiB）の引き上げと multipart 転送。
- 統合 Content フォルダとワークスペース間の Content 取り込み（取得型／監視型）。

### A0-7. 未決事項（A1 で候補を出し A3 で決める）

1. H1 / CURRENT（`external-current-v1`）の採否。
2. `<ArtifactsRoot>` の既定値と上書き方法。
3. 退避を copy にするか move にするか。
4. buildId の決め方（BS4 identity の32桁hex部を使う／退避時に新規発行）。
5. 系列トークン（project / target / configuration）の値。
6. `build-receipt.json`（ローカル絶対pathを含む）を退避先・publish対象へ含めるか、`build-info.json` へ置き換えるか。
7. 「Build の完了」をどう受け取るか（Build完了後に人が1コマンドを実行する／Unity側の完了hookから起動する）。
8. 実Unity Build を判定に含めるか、既存Player出力の往復で足りるか。

### A0-8. このスライスが答える問い・次へ進む最低条件・後続へ送る問い

- **問い:** Player Build の完了から、buildId フォルダへの退避と selection の自動生成を経て、既存の publish まで人手なしでつながるか。
- **次スライスへ進む最低条件:** 実在する配布形Player出力1件について、人が JSON を書かずに1回の操作で退避・selection生成・publish が成功し、返った参照と期待hashで別processからfetch・全hash一致する。失敗時に既存の成功記録・元出力を壊さない。
- **後続へ送る問い:** Content保管形式（program revision）、Player/Content分離、件数保持の運用接続とローカル清掃、Unity側からの自動起動（D）、別host/Cloud（C）、全platformの target 値の網羅、端末性能別の系列値、統合Contentフォルダとワークスペース間の取り込み、#111 上限の引き上げ。

---

## 0. メタデータ

- type: `slice`
- status: `A`（A2完了。A3の人間決定待ち）
- branch: `feat/build-player-publish-phase-a`
- implementation base commit: `32ec358`（`develop`。A3で更新しうる）
- implementation head commit: 未到達
- risk: `normal`（候補。A3でUnity側hookを採るなら `high` へ見直す。§2 未決）
- owner: OSM maintainers
- created: 2026-10-11
- expires: 2026-11-30 または置換revision
- harvest to: 実装済みの操作を `tools/Artifacts/README.md`「開発Build 1件の保存と取得」へ、program状態を `BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §4 B と `docs/README.md` へ反映し、スライス完了時に本書を削除する。
- Phase A snapshot の取得案内・状態: 未生成（A3凍結時に生成）
- Phase B result snapshot の取得案内・状態: 未到達
- evidence bundle の取得案内・状態: 未到達
- C' blind bundle の取得案内・状態: 未到達

## 1. 目的と対象外

- 目的: Player Build の完了から既存 `build publish` までの接続を、人が JSON を書かずに1操作で成立させる。最終形の Player 側の置き場所（`<ArtifactsRoot>/players/...`）と `build-info.json` を、このスライスで先に固定する。
- 対象外: §A0-6 のとおり。
- 現況: §A0-2・§A0-3 のとおり。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: §A0-8 の問い。
- 進める最低条件:
  1. 新しい1コマンド（仮称 `build collect`。名称はA3）が、BS4 の配布形Player出力 `artifacts/bs4/players/<identity>/` を入力に、`<ArtifactsRoot>/players/<project>/<target>/<configuration>/<buildId>/` への退避、`build-info.json` の生成、selection・build-config の private operation への生成、既存 `build publish` の呼出しまでを人手なしで行い、`reference` と独立した `packageSha256` を返す。
  2. 2026-09-24 の実在Player（266 file）で 1 を実R2に対して実行し、別processの `build fetch` で全entry/hashが退避先と一致する。
  3. 失敗時（入力不正・退避先既存・上限超過・publish失敗）に、元の `players/<identity>`、既存の退避先、既存の成功receipt を変更しない。publish失敗後の再実行は #111 の `inspect` 経路で状態を確認でき、同じbuildIdで自動再PUTしない。
- 受け入れ条件:
  - A1. `build-info.json` は非秘密の固定fieldだけを持つ: `schemaVersion=1`、`project`、`target`、`configuration`、`buildId`、`buildIdentity`、`contentSet`、`contentTarget`（transport の target 値。例 `StandaloneWindows64-Player`）、`transportManifestSha256`、`unityVersion`。ローカル絶対path・時刻以外の環境情報を含めない。値は `build-receipt.json` と `transport.json` から読み、欠落時は推定せず拒否する。
  - A2. 退避先のfile集合 = 元の配布形Player のfile集合（§A0-7-6 の決定に従い `build-receipt.json` を除外または同梱）+ `build-info.json`。全fileの size/SHA-256 が元と一致することを退避完了前に確認し、staging→rename で公開する。退避先が既に存在すれば拒否する。
  - A3. selection の `files` は退避先 root 内の全fileと一致し、`root` は退避先、`buildId` は退避前に確定した値。selection と build-config は 32桁hex名の新規 private operation に置く。
  - A4. `<ArtifactsRoot>` が #111 の所有store（`OneStarMaker/Artifacts/builds-v1`）と親子関係にある設定、または入力rootと親子関係にある設定を、ファイル操作の前に拒否する。
  - A7. 同じ `<ArtifactsRoot>` を複数のワークスペース・processが同時に使っても、同じbuildIdの退避は一方だけが成功し、他方は既存として拒否される。staging は呼出しごとに固有名で、他の呼出しの staging・退避先に触れない。
  - A5. 入力が配布形でない（`build-receipt.json` 欠落、`*_BackUpThisFolder_ButDontShipItWithYourGame` を含む、`work/` を指す）場合は拒否する。#111 の上限超過は publish 前に検出して拒否する。
  - A6. 系列トークン・buildId は #111 の検査（`Assert-BuildToken` / `Assert-BuildId`）を通る値になる。
- ここでは答えない問いと所有する後続スライス:
  - Content の保管形式・`<ArtifactsRoot>/content/...`・R2 revision upload・Content件数保持、統合Contentフォルダとワークスペース間の取り込み（取得型／監視型） → **program r7 revision**（`BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` に Content 種別を追加する Phase A。本スライスの後に owner が起票）。
  - Player/Content 分離と、4季節内包時の 512 file 超過見込み → 同上（program r7）。
  - 件数保持の運用接続、実DELETE、`<ArtifactsRoot>` のローカル件数清掃 → program §4 B の後続スライス。
  - Unity Editor からの自動起動、BuildSystem/Harness 接続 → program §4 D。
  - 別host / Cloud 取得 → program §4 C。
  - Windows 以外の target 値、端末性能別の系列値 → program r7 で「系列keyが追加値を受け止められるか」だけ確認。
- 判定定義: GO = 進める最低条件 1〜3 を全て満たす。NO-GO = いずれか未達、または常時契約違反。スパイクではないため CONDITIONAL ACCEPT は使わない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし（未凍結）
- 本文へ転記した実装制約: §A0-5 のとおり。加えて、`tools/Artifacts` は Unity をimportせず、Unity側コードは `tools/` を参照しない（候補 a を採る場合、Unity側は無変更）。
- 未決事項（A3 で決める候補）:

| # | 論点 | 候補 | A1 の推奨と理由 |
|---|---|---|---|
| U1 | H1/CURRENT 採否 | (a) 従来tracked HANDOFF、(b) `external-current-v1` | **(a)**。対象はtools/Artifactsのみで、#111 も従来方式で完了した。(b) を採るなら A3 で本書を CURRENT へ移し、本書の §5 以降を固定仕様へ置き換える |
| U2 | `<ArtifactsRoot>` 既定値 | (a) `%LOCALAPPDATA%\OneStarMaker\BuildArtifacts`、(b) repo の親ディレクトリ直下 `osm-artifacts\`、(c) 既定なし（設定必須） | **(a) + 非秘密の設定fileで任意のローカルフォルダへ変更可能**。owner 追加入力（§A0-4）により、任意フォルダの指定と複数ワークスペースからの共有は必須。既定値はその上での初期値に過ぎない。(a) は repo外・同一ユーザー・worktree非依存で、#111 の store（`OneStarMaker\Artifacts`）と兄弟になり親子関係にならない。指定値は A4 の親子検査を通す。1 Build 約125 MiB なので、ローカル容量はローカル清掃（後続）が入るまで手動管理と明記する |
| U3 | copy / move | (a) copy + 全hash照合、(b) move | **(a)**。元の `players/<identity>` は Player Build 側の出力で、Storage が所有しない原sourceである（program §2 未完了upload節・§3）。move は volume を跨ぐと非atomic |
| U4 | buildId | (a) identity の32桁hex部、(b) 退避時に新規GUID | **(a)**。同じBuildの再実行が同じbuildIdになり、#111 の重複拒否と `inspect` で冪等に扱える。`build-info.json` に identity 全体も残す |
| U5 | 系列トークン | project=`samplegame`、target=`windows64` か `standalonewindows64-player`、configuration=`il2cpp-high` か `development` | target は platform 名だけを持つ `windows64` を推奨（Steam Deck 等を同じ軸に足せる）。transport の target 文字列は `build-info.json` の `contentTarget` に別に残す。configuration は現行BS4が単一構成のため `development` を推奨 |
| U6 | `build-receipt.json` | (a) 除外し `build-info.json` に必要値を移す、(b) 同梱 | **(a)**。ローカル絶対pathとユーザー名を含む。Player実行に不要（runtime JSON は `SampleGame_Data/StreamingAssets/bs4-runtime.json`、receipt は実行時に読まれない） |
| U7 | Build完了の受け取り | (a) Build完了後に `--source <players/identity>` を渡して1コマンド、(b) `--latest` で `artifacts/bs4/players` の最新を選ぶ、(c) Unity coordinator 末尾から起動 | **(a)**。(b) は並行worktreeで取り違える。(c) は Unity→外部CLIの依存で program D の範囲。(a) なら Unity側は無変更 |
| U8 | 実Unity Build を判定に含めるか | (a) 既存Player出力の往復で判定、(b) Phase C で新規BS4 Buildを1回行い続けて collect | **(a)**。Unity側無変更なら Build の再実行は接続の判定に情報を足さない。(b) を採るなら Phase C の Unity Build 予算として A3 に明記する |

## 3. 責務マップ

U1〜U8 を推奨どおり採った場合の初稿。

| file | 責務 | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|---|
| `tools/Artifacts/BuildCollect.psm1`（新規） | 配布形Player入力の検証、`build-info.json` 生成、退避（staging→hash照合→rename）、selection/build-config 生成 | 新しい責務（収集・退避）。publish とは失敗時の後始末と所有物が違う | 退避先は ArtifactsRoot 所有。staging はこの呼出し専用 | `BuildContract.psm1`（token/ID検査の再利用）のみ。R2・資格情報に依存しない | 内部関数 | fixture の偽Player dir で offline 単体検証可能 | 0 → 約150 |
| `tools/Artifacts/BuildApplication.psm1` | collect → 既存 publish の orchestration | 既存 publish 関数を private operation の selection で呼ぶ接続 | 既存どおり | `BuildCollect`、既存 | 既存 | 注入transportで offline | 189 → 約215 |
| `tools/artifacts.ps1` | `build collect` サブコマンドの引数解釈 | 入口追加 | 既存 | 既存 | CLI | 既存のCLIテスト形式 | 106 → 約120 |
| `tools/Artifacts/tests/BuildCollect.Tests.ps1`（新規） | §2 A1〜A6 と失敗経路の offline 検証 | 新規 | — | 隔離store、偽Player fixture | — | — | 0 → 約250 |
| `tools/Artifacts/README.md` | 操作の追記 | 公開面の現況更新 | — | — | 文書 | docs-audit | 206 → 約225 |

- 行数警報: 500行・3責務・50%増のいずれにも当たらない見込み。`BuildApplication.psm1` は +14%。`BuildCollect` を `BuildApplication` に入れない理由は、退避の staging/rename 所有と publish の成功確定点を同じ関数の失敗処理で混ぜないため。
- Unity側（`unity/Assets/**`）は無変更。U7 で (c) を採る場合はここに Unity側 file が加わり、asmdef 境界と risk を A3 で見直す。

## 4. 実装計画

- 変更対象: §3 の表。
- 順序: (1) `build-info.json` codec と入力検証、(2) 退避（staging→hash→rename）、(3) selection/build-config 生成、(4) publish 接続と CLI、(5) offline テスト、(6) README。
- Phase B から Phase A へ差し戻す条件: Unity側の変更が必要になった場合、#111 の selection/config/receipt 契約を変える必要が出た場合、`<ArtifactsRoot>` が所有storeと分離できない場合、Content を別保存しないと最低条件2が満たせない場合。
- 対象外を維持する方法: Content を独立して保存・列挙するコードを書かない。retention policy を呼ばない。DELETE を発行しない。

## 5. テストとレビュー計画

- 単体テスト: `BuildCollect.Tests.ps1`。偽Player fixture（非秘密の小さなfile群、`build-receipt.json` と transport の最小JSON）、隔離した ArtifactsRoot と store、注入transport。ケース: 正常系、receipt欠落、backup dir混入、`work/`指定、退避先既存、ArtifactsRoot と store の親子関係（両方向）、hash不一致（copy中の改変を注入）、上限超過（file数・総量）、publish失敗後の元/退避先不変、同一identityの再実行が #111 の重複拒否になること。
- 差し戻し中の起点 `-Filter`: `BuildCollect.Tests.ps1 -Case '*'` と `BuildPublish.Tests.ps1 -Case '*'`。
- 判定必須テスト: Artifacts の Build 系 offline suite（BuildCollect、BuildPublish、BuildRetention、ArtifactPackage、ArtifactTransfer）全件、Packaging/Transport の Release build、変更 PowerShell の構文確認、`contract-audit`・`docs-audit`、最低条件2の実R2往復。
- 全 EditMode 回帰の適用除外: Unity source・依存を変更しないため適用しない。代替証拠は上記 offline suite と実Player bytes の往復。U7=(c) を採る場合は除外を取り消す。
- 統合・Unity テスト: なし（U8=(a) の場合）。
- 操作・実行時・目視条件の検証経路:
  - 最低条件2: 担当は Phase C（判定C）。環境はowner Windowsユーザー・登録済み `osm` 資格情報・既存Evidence config。初期状態は `artifacts/bs4/players/20260924T202227427Z-b80d…` が存在し、退避先と同buildIdの成功receiptが存在しないこと。操作は `build collect` 1回 → 別 PowerShell process で `build fetch`。観測は返却値の存在、退避先と fetch 展開先の全entry/hash一致、元出力の不変（前後の全hash）。証拠は実行ログ・hash一覧（ローカルpathを除いた相対path）を evidence bundle に入れる。
  - 既存経路（#111 の publish/fetch）は #111 の実R2往復で成立済み。新規部分は collect のみで、R2 経路は変えない。
- 未知の操作経路の疎通結果: なし。入力の実在Player（266 file）と #111 の実R2経路は既知。同じbuildIdが既に #111 のpublishで成功記録を持っている可能性があり、その場合 U4=(a) では重複拒否になる。Phase C 開始前に `build inspect` で確認し、既存なら別identityの既存Player（09-20 の3件）を使う。この確認は最低条件2の前提確認であり、新規Buildを要しない。
- 人間の判断が必要な条件: なし。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、`git diff --check`。
- A0/A1 主担当・モデル・ベンダー: Claude Code / Claude Opus 5.5 / Anthropic。
- A2 独立レビューごとの観点・担当・モデル・ベンダー: 入力版はこの節を含む commit。互いの指摘を見せない。(R1) アーキテクチャゲート＋最低条件・検証経路の実行可能性: 全文と関連コードを読む。cursor-agent / Grok 系 / xAI。(R2) A0 だけから代替構成を出す: §A0 のみを読む。Claude Code subagent / Claude Sonnet 5.5 / Anthropic。結果と採否は A3 でこの欄の下に記録する。
- A2 結果（入力版 `fb2d59f`、2026-10-11。両者とも互いの結果を見ていない）:
  - R1（Grok 系、xAI）: 「A3 凍結不可」。blocker 3 / major 4 / minor 1。主担当がコードで照合し、事実関係は概ね正しいと確認した。
  - R2（Claude Sonnet 5.5、A0 のみ）: 代替構成として、tools 側の1コマンド（stage → 既存 publish）、Unity 無改修を推奨。比較案（Unity 側 `PlayerBuildPublisher` で退避）は program D と混ざるため非推奨。A0 への指摘は major 2 / minor 2。事実認定の誤りは無し。未決 8 件のうち U1・U3・U4・U6・U7・U8 は A1 推奨と一致し、U2・U5 は異なる。
- A3 採否案（主担当の案。人間の決定までは未確定）:

| ID | 指摘 | 案 | 理由・反映先 |
|---|---|---|---|
| R1-B1 | A1 は build-info の全値を receipt / transport.json から読むとしているが、系列・buildId はどちらにも無い。`players/<identity>` に transport.json は無い | 採用 | 系列は U5 の固定入力、buildId は U4 で導出、contentSet・unityVersion は receipt の `transportPath` が指す実在 transport.json から読み、manifest hash を `transportManifestSha256` と照合する。§2 A1 を改稿 |
| R1-B2 | build-config は `evidenceConfigPath` 等を要し、Player 出力から生成できない。§3 に `ArtifactPaths` 依存と ArtifactsRoot 設定の所有者が無い | 採用 | 既存の build-config を collect の必須入力にし、private operation への複写だけを行う（R2 も同じ結論）。§3 に `ArtifactPaths` 依存と設定の所有者・寿命を追加 |
| R1-B3 | A4 は `builds-v1` との親子関係しか見ていないが、publish 側の `Assert-BuildSourceRoot` は `OneStarMaker\Artifacts`・`Workflow`・`Get-ArtifactRoot` も拒否する | 採用 | 退避の前に同じ関数で ArtifactsRoot と退避先を検査し、検査規則を二重化しない。A4 を改稿 |
| R1-M1 | Collect に入力検証・codec・設定・selection 生成が同居している。全 file 列挙は既存の `Get-BuildInventory` と重複する | 採用（方針） | build-info codec は Contract、退避は Store 相当、orchestration は Application に置く。inventory は既存関数を export して再利用する。§3 を改稿 |
| R1-M2 | 最低条件2で09-24の1件に固定しているが、U4=(a) では同じ buildId が既に予約済みかもしれない。代わりに09-20を使うと file 数の条件が変わる | 採用 | Phase C の開始前に `inspect` で確認する。判定に使う identity は A3 で本文に固定する |
| R1-M3 | 退避が成功して publish が失敗した後、再実行すると「退避先既存」で拒否され、先へ進めない | 採用 | 同じ buildId の退避先が build-info と全 hash で一致すれば退避を省き、publish（`inspect`）から再開する。最低条件3に書く |
| R1-M4 | A7 にテストも検証経路も無い。`build collect` は新しい操作なのに、疎通確認が「なし」になっている | 採用 | 並行 rename の offline ケースを追加する。実R2 での collect は「未確認。初回確認は Phase C」と確認地点を書く |
| R1-m1 | ゲートの分類が無い。`Assert-BuildToken` は export されていない。トークンが既存テスト例と違う | 採用 | 分類欄を追加。トークンは既存テストに合わせて `windows-x64-player` / `il2cpp-high` とする（R2 も configuration は `il2cpp-high`）。U5 を改稿 |
| R2-1 | selection を退避先 root の中に置けないことを明示すべき | 採用 | A3 に「selection は root 外の private operation に置く」と追記 |
| R2-2 | 並行2回実行と同じ identity の再実行を最低条件に入れるべき | 採用（R1-M3 と統合） | 再実行は「再開」として定義し、並行実行は片方だけ成功する |
| R2-3 | 4季節で約880 file になる推定を、対象外を切る根拠に使っている | 採用 | 注記として残し、分離を後送りする根拠は「Player が content を内包しないと起動しない」ことに一本化する |
| R2-4 | 「人手なし」と「人が1コマンドを実行する」の文言が揃っていない | 採用 | 問いを「JSON を手書きせず1コマンドで」に揃える |
| R2-U2 | ArtifactsRoot は既定値を置かず、引数または環境変数で必須にする | **保留（人間判断）** | A1 推奨は LocalAppData 既定＋上書き。owner 入力の「任意フォルダ」はどちらでも満たせる |
- A3 統合担当・モデル・採否: 未実施。（上表の人間決定待ち）
- C' 用に予約した担当・モデル・ベンダー: owner 決定（2026-10-11）。C' は人間担当、または B・C と異なるモデルの AI 担当とする。ベンダーの相違は求めない。どちらの場合も新規セッションと blind audit bundle は必須。
- 独立性の強化条件を満たせない場合の理由: 利用可能な AI 経路が Anthropic（Claude）と cursor-agent 経由の xAI（Grok 系のみ）の2系統で、Codex 系は利用不可。owner は、モデルが異なれば blind 監査として十分と判断し、強化条件（未関与ベンダー）を求めないと決めた（2026-10-11）。`独立性制約あり` として記録する。

## 6. Phase B 実装結果

未着手

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手
