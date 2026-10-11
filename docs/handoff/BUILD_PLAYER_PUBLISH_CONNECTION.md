# Build成果物の保存接続（Player Build → buildId退避 → selection自動生成 → publish）

この文書は Phase A の A0 planning packet（§A0）と、A3 で凍結した A1 r3（§0〜§9）を持つ単一スライスHANDOFFである。A2 の入力版と結果は §5 に記録する。凍結後の設計判断の変更は Phase A の新しい revision として扱う。

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
- status: `done`（Phase D 完了。owner が GO を採用）
- branch: `feat/build-player-publish-phase-a`
- implementation base commit: `32ec358`（凍結時点の `origin/develop` と同一）
- implementation head commit: `d35823c`
- risk: `normal`（Unity側は変更せず、tools/Artifacts に閉じる）
- owner: OSM maintainers
- created: 2026-10-11
- expires: 2026-11-30 または置換revision
- harvest to: 実装済みの操作を `tools/Artifacts/README.md`「開発Build 1件の保存と取得」へ、program状態を `BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` §4 B と `docs/README.md` へ反映し、スライス完了時に本書を削除する。
- Phase A snapshot の取得案内・状態: 確認済み（2026-10-11）。凍結版 HANDOFF（`f1cbf1e`）と A2 の生出力を Evidence publish し、別 process の fetch で全 file の hash 一致を確認した。取得案内は、メイン checkout の git 管理外の引渡し先（`TestResults/handoff/build-player-publish-connection/`）にある。A2 の所見を含むので、C' の blind 入力には使わない
- Phase B result snapshot の取得案内・状態: 確認済み（2026-10-11、別 process の fetch で全4 file の hash 一致）。引渡し先は Phase A と同じ git 管理外の場所
- evidence bundle の取得案内・状態: 確認済み（全37 file）。引渡し先は同上
- C' blind bundle の取得案内・状態: 確認済み（全42 file。監査担当の fetch でも一致）。引渡し先は同上で、所見を含む案内とは別 file

## 1. 目的と対象外

- 目的: Player Build の完了から既存 `build publish` までの接続を、人が JSON を手書きせず1コマンドで成立させる。最終形の Player 側の置き場所（`<ArtifactsRoot>/players/...`）と `build-info.json` を、このスライスで先に固定する。
- 対象外: §A0-6 のとおり。
- 現況: §A0-2・§A0-3 のとおり。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: Player Build の完了後、人が JSON を手書きせずに1コマンドで、buildId フォルダへの退避、selection の自動生成、既存の publish までつながるか（§A0-8 の問いを R2-4 に従って言い直した）。
- 進める最低条件:
  1. 新しい1コマンド `build collect` は、BS4 の配布形Player出力 `artifacts/bs4/players/<identity>/` と既存の build-config を入力に取る。`<ArtifactsRoot>/players/<project>/<target>/<configuration>/<buildId>/` への退避、`build-info.json` の生成、selection の生成、既存 `build publish` の呼出しまでを、人が JSON を書かずに行い、`reference` と独立した `packageSha256` を返す。
  2. 判定用の実在Player `20260924T202227427Z-b80d0ef69df841f4bde30f85a2b4cd6e`（266 file、§A0-3）で、1 を実R2に対して実行する。別processの `build fetch` で、全entry/hashが退避先と一致する。
  3. 失敗と再実行で壊れない。入力不正・設定不足・上限超過・publish失敗のいずれでも、元の `players/<identity>`、他の退避先、既存の成功receiptを変更しない。同じ入力で再実行したときは次のように振る舞う。
     - 退避が完了済みなら、退避を省く。
     - 成功receiptがあれば、同じ `reference`/`packageSha256` を返し、PUT しない。
     - 予約済みで receipt が無ければ `publish-unresolved` で止まり、PUT しない。
     - 未予約なら publish へ進む。
     - 同じ buildId の並行実行は、片方だけが退避に成功する。
- 受け入れ条件:
  - A1. `build-info.json` は非秘密の固定 field だけを持つ: `schemaVersion=1`、`project`、`target`、`configuration`、`buildId`、`buildIdentity`、`contentSet`、`contentTarget`、`transportManifestSha256`、`unityVersion`、`playerBuildGuid`。各値の出所は次のとおり。欠落・不一致は推定で埋めず拒否する。
    - `build-receipt.json` から次の対応で読む: `identity` → `buildIdentity`、`target` → `contentTarget`、`transportManifestSha256` → 同名、`playerBuildGuid` → 同名。receipt の `target` は系列の `target`（A6）とは別の値で、混同しない。`buildIdentity` は入力ディレクトリ名と一致すること。`transportPath` は transport.json の場所を知るためだけに読み、`build-info.json` へは写さない。
    - `contentSet`・`unityVersion` は、receipt の `transportPath` が指す実在の `transport.json` から読む。transport.json の bytes の SHA-256 は `transportManifestSha256` と一致し、`revision` は `buildIdentity`、`target` は `contentTarget` と一致すること。
    - 系列（`project`/`target`/`configuration`）は A6、`buildId` は A5 で決める。
    - ローカル絶対path、ユーザー名、時刻は含めない。
  - A2. 入力Playerの `content/` の file 集合と各 SHA-256 は、上記 transport.json の `files` と一致すること。これで「前提とする contentSet」を宣言だけでなく bytes で裏付ける。
  - A3. 退避先の file 集合は、入力の file 集合から `build-receipt.json` を除き、`build-info.json` を加えたものとする。退避は copy で行い、元は移動・変更しない。呼出しごとに固有名の staging を `<ArtifactsRoot>` 内に作り、全 file の size/SHA-256 が元と一致することを確認してから、同一 volume の rename で公開する。
    - 退避先が既に存在する場合: 中身がこの入力から作るものと完全に一致すれば完了済みとして扱う。そうでなければ拒否する。
    - 自分の staging 以外（他の呼出しの staging、他の退避先）には触れない。
  - A4. selection は退避先 root の外、`New-ArtifactOperation` で作る新規 private operation に置く。build-config は利用者が渡した既存のものを `Read-BuildConfig` で検証し、その bytes を同じ operation へ複写する。`files` は既存の `Get-BuildInventory` の結果をそのまま使う。selection の生成はこのスライスが担い、build-config の生成・登録は行わない。
  - A5. `buildId` は BS4 identity の末尾32桁 hex とする。identity が `yyyyMMddTHHmmssfffZ-<32hex>` 形式でなければ拒否する。
  - A6. 系列は receipt から固定の対応表で決める。`target=StandaloneWindows64-Player` は `windows-x64-player`、`backend=IL2CPP` かつ `stripping=High` は `il2cpp-high`、project は `samplegame` 固定。表に無い組合せは拒否する。値は既存の #111 検査（`Get-BuildSeries`）を通ること。
  - A7. `<ArtifactsRoot>` は明示が必須で、既定値は持たない（owner 決定 2026-10-11、案B）。`<ArtifactsRoot>` は退避した Player の置き場で、Storage の内部保存先（`%LOCALAPPDATA%\OneStarMaker\Artifacts`。資格情報・deployment・receipt・transfers）とは別物である。owner は D ドライブ等の、同期フォルダでない場所に置く想定。解決順は (1) `--artifacts-root`、(2) ユーザー環境変数 `OSM_ARTIFACTS_ROOT`。どちらも無ければ `artifacts-root-not-configured` を返し、何もしない。指定値は次をすべて満たすこと。満たさなければファイル操作の前に拒否する。
    - 絶対pathで、既存のディレクトリである。このコマンドは ArtifactsRoot 自体を作らない。
    - reparse point ではない。
    - 入力rootと親子関係にない。
    - #111 の `Assert-BuildSourceRoot` を通る（所有store・Workflow・`Get-ArtifactRoot` との親子拒否を、同じ関数で行う）。
  - A8. 入力が配布形でなければ拒否する。対象は、`build-receipt.json` が無い、`*_BackUpThisFolder_ButDontShipItWithYourGame` を含む、`artifacts/bs4/work/` 配下を指す場合。#111 の上限（512 file / 240 MiB / 単一 256 MiB / path 240字）の超過は、退避の前に検出して拒否する。
- ここでは答えない問いと所有する後続スライス:
  - Content の保管形式、`<ArtifactsRoot>/content/...`、R2 への revision upload、Content の件数保持、統合Contentフォルダとワークスペース間の取り込み（取得型／監視型） → **program r7 revision**（`BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` に Content 種別を追加する Phase A。本スライスの後に owner が起票）。
  - Player/Content 分離 → 同上。後送りの根拠は「現行Playerは `content/` を内包しないと起動しない」こと（§A0-2）。4季節で約880 file という推定は注記に留める。
  - `OSM_ARTIFACTS_ROOT` などを設定する開発環境セットアップ script（bat 等。他の設定もまとめる） → 別途のセットアップスライス（owner 指示 2026-10-11）。本スライスは設定値を読むだけで、書き込まない。
  - 件数保持の運用接続、実DELETE、`<ArtifactsRoot>` のローカル件数清掃 → program §4 B の後続スライス。
  - Unity Editor からの自動起動、BuildSystem/Harness 接続 → program §4 D。
  - 別host / Cloud 取得 → program §4 C。
  - #111 の上限引き上げ・multipart → program §4 B の後続。
  - Windows 以外の target 値、端末性能別の系列値 → program r7 で「系列keyが追加値を受け止められるか」だけ確認する。
- 判定定義: GO = 進める最低条件 1〜3 を全て満たす。NO-GO = いずれか未達、または常時契約違反。スパイクではないため CONDITIONAL ACCEPT は使わない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 本文へ転記した実装制約: §A0-5 のとおり。加えて、`tools/Artifacts` は Unity を import しない。Unity側（`unity/Assets/**`）は変更しない。#111 の selection・config・receipt・reference の形式と検査は変えない。
- 確定した論点（A3。A2 結果と採否は §5）:

| # | 論点 | 決定 | 理由 |
|---|---|---|---|
| U1 | H1/CURRENT 採否 | 従来の tracked HANDOFF | tools/Artifacts だけが対象で、#111 も従来方式で完了した |
| U2 | `<ArtifactsRoot>` | 既定値なし・明示必須（A7） | owner 決定（案B）。設定は別途のセットアップ script に任せる |
| U3 | copy / move | copy と全 hash 照合（A3） | 元の Player 出力は Storage が所有しない原sourceである。move は volume を跨ぐと非atomic |
| U4 | buildId | identity の32桁 hex（A5） | 再実行で同じ buildId になり、#111 の予約・receipt で冪等に扱える |
| U5 | 系列トークン | `samplegame` / `windows-x64-player` / `il2cpp-high`（A6） | 既存テストの例と一致する。R1・R2 の指摘による |
| U6 | `build-receipt.json` | 退避先から除き、必要な値を `build-info.json` へ移す（A1・A3） | ローカル絶対pathを含み、実行時には読まれない（runtime JSON は `SampleGame_Data/StreamingAssets/bs4-runtime.json`） |
| U7 | Build 完了の受け取り | Build 完了後に `--source <players/identity>` を明示して1コマンド | `--latest` は並行 worktree で取り違える。Unity からの起動は program D の範囲 |
| U8 | 実 Unity Build | 判定に含めない。既存Player出力の往復で判定する | Unity側は変更しないため、Build を再実行しても接続の判定に情報が増えない |

## 3. 責務マップ

ゲート分類: P = Policy、O = orchestration、I = I/O・永続化。

| file | 分類 | 責務 | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|---|---|
| `tools/Artifacts/BuildContract.psm1` | P | `build-info.json` の codec、receipt/transport からの値抽出と照合、系列対応表（A1・A2・A5・A6）、上限の事前検査（A8） | 保存契約の値の規則。#111 の selection 規則と同じ理由で変わる | 状態なし | 既存どおり | `New-BuildInfo` 等を内部 export | 文字列/JSON fixture で純粋に検証 | 113 → 約185 |
| `tools/Artifacts/BuildStage.psm1`（新規） | I | ArtifactsRoot の解決と検査（A7）、入力の配布形検査（A8）、staging→hash 照合→rename の退避、既存退避先の一致判定（A3） | ローカル退避の所有と失敗時後始末。publish の成功確定点とは寿命が違う | 退避先は `<ArtifactsRoot>` 所有で、このスライスでは削除しない。staging は呼出しごとに固有名 | `BuildContract`、`ArtifactPaths`（reparse・包含判定）。`BuildApplication` には依存しない。入力の file 一覧は Application から引数で受け取る | 内部関数 | 隔離した一時 ArtifactsRoot、hash 不一致・並行 rename を注入 | 0 → 約170 |
| `tools/Artifacts/BuildApplication.psm1` | O | `Invoke-BuildCollect`: 入力の `Get-BuildInventory` → stage → 退避先の `Get-BuildInventory` → operation 作成 → config 複写 → selection 生成 → inspect / 既存 publish の順に束ね、再実行時の分岐（最低条件3）を担う | 新しい入口の orchestration | 既存どおり | `BuildStage`、`BuildContract`、`ArtifactPaths`、既存 | `Invoke-BuildCollect` を追加 export | 注入 transport で offline | 189 → 約235 |
| `tools/artifacts.ps1` | O | `build collect` の引数解釈。`--profile`・`--config`・`--source` は必須、`--artifacts-root` は省略可（省略時は A7 の環境変数）。既存サブコマンドの完全一致の解釈は変えず、collect 用の分岐を足す | 入口追加 | 既存 | 既存 | CLI | 既存の CLI テスト形式 | 106 → 約120 |
| `tools/Artifacts/tests/BuildCollect.Tests.ps1`（新規） | — | §2 A1〜A8 と最低条件3の offline 検証 | 新規 | — | 隔離 store、偽Player fixture | — | — | 0 → 約300 |
| `tools/Artifacts/README.md` | — | `build collect` と `OSM_ARTIFACTS_ROOT` の説明 | 公開面の現況更新 | — | — | 文書 | docs-audit | 206 → 約225 |

- 行数警報: 500行・3責務の警報には当たらない。`BuildContract.psm1` は +64% で 50% 警報が発火する。分割しない。理由は、追加分が既存の selection/reference 規則と同じ「保存契約の値」の規則で、依存・寿命・テスト方法も同じだから。Policy をここに集めると、Stage を I/O だけに保てる。
- 退避を `BuildApplication` に入れない理由: staging・rename の所有と後始末を、publish の成功確定点（receipt rename）と同じ関数の失敗処理で混ぜないため。
- `BuildStore.psm1` は変更しない。#111 の予約・receipt の所有を、退避と混ぜない。
- build-config の寿命: 利用者が渡す原本は利用者の private operation にあり、collect は読むだけで変更・削除しない。collect は新規 operation へ bytes を複写し、その複写と selection は今回の publish の入力として #111 の operation と同じ扱いになる（このスライスで清掃しない）。
- 依存は Application → Stage → Contract の一方向にする。directory の走査（`Get-BuildInventory`）は I/O なので Policy の `BuildContract` へは移さず、`BuildApplication` に残す。Application が一覧を取って Stage へ渡す（再レビューの指摘による）。

## 4. 実装計画

- 変更対象: §3 の表。
- 順序: (1) Contract の build-info・照合・対応表、(2) Stage の ArtifactsRoot 検査と退避、(3) Application の collect と再実行分岐、(4) CLI、(5) offline テスト、(6) README。
- Phase B から Phase A へ差し戻す条件:
  - Unity側の変更が必要になった。
  - #111 の selection・config・receipt・reference の形式または検査を変える必要が出た。
  - `Assert-BuildSourceRoot` を通る ArtifactsRoot を定義できない。
  - transport.json と Player 内 `content/` の照合（A2）が実出力で成立しない。
  - Content を別に保存しないと最低条件2を満たせない。
- 対象外を維持する方法: Content を独立して保存・列挙するコードを書かない。retention policy を呼ばない。DELETE を発行しない。ArtifactsRoot と環境変数を書き込まない。

## 5. テストとレビュー計画

- 単体テスト: `BuildCollect.Tests.ps1`。偽Player fixture（非秘密の小さな file 群、`build-receipt.json`、transport の最小 JSON）、隔離した ArtifactsRoot と store、注入 transport を使う。ケースは次のとおり。
  - 正常系。
  - A7: ArtifactsRoot 未設定、相対path、不存在、reparse、所有store・Workflow・入力rootとの親子関係（両方向）。
  - A8: receipt 欠落、backup dir 混入、`work/` 指定、上限超過（file 数・総量・単一file・path 長）。
  - A1・A2: transport.json の hash 不一致、revision/target 不一致、`content/` と manifest の不一致。
  - A5・A6: identity 形式不正、対応表に無い組合せ。
  - A3: copy 中の改変注入による hash 不一致、退避先既存の一致・不一致。
  - 並行2 process の同一 buildId（シグナルで同期。待機に `Task.Delay` / `Thread.Sleep` を使わない）。
  - 最低条件3: receipt 有りで PUT 0 回、予約有り・receipt 無しで `publish-unresolved`・PUT 0 回、publish 失敗後に元と退避先が不変。
- 差し戻し中の起点 `-Filter`: `BuildCollect.Tests.ps1 -Case '*'` と `BuildPublish.Tests.ps1 -Case '*'`。
- 判定必須テスト: Artifacts の Build 系 offline suite（BuildCollect、BuildPublish、BuildRetention、ArtifactPackage、ArtifactTransfer）全件、Packaging/Transport の Release build、変更 PowerShell の構文確認、`contract-audit`・`docs-audit`、最低条件2の実R2往復。
- 全 EditMode 回帰の適用除外: Unity source・依存を変更しないため適用しない。代替証拠は上記 offline suite と実Player bytes の往復。
- 統合・Unity テスト: なし。
- 操作・実行時・目視条件の検証経路:
  - 最低条件2: 担当は Phase C（判定C）。
    - 環境: owner の Windows ユーザー、登録済みの `osm` 資格情報、既存の Evidence config・build-config、判定用に新規作成した空の ArtifactsRoot（`--artifacts-root` で渡す）。
    - 初期状態: 判定用 identity の `players/` と `transport/` が存在し、buildId `b80d0ef69df841f4bde30f85a2b4cd6e` の成功receipt・予約が無いこと。2026-10-11 時点で、#111 の成功記録は Codex アプリの MSIX 仮想化された LocalAppData 側の `builds-v1` にあり、中身は buildId `4a244e29…` だけだった（凍結 r3 の「このPCに存在しない」は誤りで、A3 後の事実訂正）。Storage の内部保存先は同日、本来の LocalAppData に一本化した。#111 は同じ Player を同じ系列・別の buildId `4a244e29ea66438c83f61d971ad66234` で保存していた（#111 の保持 worktree の selection で確認）。このため R2 key `…/b80d0ef69df841f4bde30f85a2b4cd6e/bundle.zip` は #111 では使われていない。
    - 操作: `build collect` を1回実行し、別の PowerShell process で `build fetch` を実行する。続けて同じ入力で `build collect` をもう1回実行する。
    - 観測: 返却値がある。退避先と fetch 展開先の全 entry/hash が一致する。元出力が前後で不変（全 hash）。2回目は同じ reference/hash を返し、PUT していない。
    - 証拠: 実行ログと hash 一覧（ローカルpathを除いた相対path）を evidence bundle に入れる。
  - #111 の publish/fetch 経路は、#111 の実R2往復で成立済み。新しく加わるのは collect だけで、R2 経路は変えない。
- 未知の操作経路の疎通結果: `build collect` は新しい操作で、実R2 での疎通は未確認。初回確認は Phase C（判定C）で、上記の最低条件2の手順で行う。理由: 実装前の操作であり、offline テストでは R2 を注入に置き換えるため。前提確認として、Phase C 開始時に `pwsh tools/artifacts.ps1 build inspect --profile osm --config <既存build-config> --build-id b80d0ef69df841f4bde30f85a2b4cd6e` を実行し、`receipt-unavailable` であることを確かめる。成功記録が見つかった場合、別 identity への切替はしない。最低条件2の前提が崩れたものとして判定を止め、人間に返す。
- 人間の判断が必要な条件: なし。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、`git diff --check`。
- A0/A1 主担当・モデル・ベンダー: Claude Code / Claude Opus 5.5 / Anthropic。
- A2 独立レビューごとの観点・担当・モデル・ベンダー: 入力版はこの節を含む commit。互いの指摘を見せない。(R1) アーキテクチャゲート＋最低条件・検証経路の実行可能性: 全文と関連コードを読む。cursor-agent / Grok 系 / xAI。(R2) A0 だけから代替構成を出す: §A0 のみを読む。Claude Code subagent / Claude Sonnet 5.5 / Anthropic。結果と採否は A3 でこの欄の下に記録する。
- A2 結果（入力版 `fb2d59f`、2026-10-11。両者とも互いの結果を見ていない）:
  - R1（Grok 系、xAI）: 「A3 凍結不可」。blocker 3 / major 4 / minor 1。主担当がコードで照合し、事実関係は概ね正しいと確認した。
  - R2（Claude Sonnet 5.5、A0 のみ）: 代替構成として、tools 側の1コマンド（stage → 既存 publish）、Unity 無改修を推奨。比較案（Unity 側 `PlayerBuildPublisher` で退避）は program D と混ざるため非推奨。A0 への指摘は major 2 / minor 2。事実認定の誤りは無し。未決 8 件のうち U1・U3・U4・U6・U7・U8 は A1 推奨と一致し、U2・U5 は異なる。
- A3 採否（2026-10-11。R2-U2 は owner が案Bに決定し、他の12件も owner が採用を決定した。§2・§3 に反映済み）:

| ID | 指摘 | 案 | 理由・反映先 |
|---|---|---|---|
| R1-B1 | A1 は build-info の全値を receipt / transport.json から読むとしているが、系列・buildId はどちらにも無い。`players/<identity>` に transport.json は無い | 採用 | 系列は U5 の固定入力、buildId は U4 で導出、contentSet・unityVersion は receipt の `transportPath` が指す実在 transport.json から読み、manifest hash を `transportManifestSha256` と照合する。§2 A1 を改稿 |
| R1-B2 | build-config は `evidenceConfigPath` 等を要し、Player 出力から生成できない。§3 に `ArtifactPaths` 依存と ArtifactsRoot 設定の所有者が無い | 採用 | 既存の build-config を collect の必須入力にし、private operation への複写だけを行う（R2 も同じ結論）。§3 に `ArtifactPaths` 依存と設定の所有者・寿命を追加 |
| R1-B3 | A4 は `builds-v1` との親子関係しか見ていないが、publish 側の `Assert-BuildSourceRoot` は `OneStarMaker\Artifacts`・`Workflow`・`Get-ArtifactRoot` も拒否する | 採用 | 退避の前に同じ関数で ArtifactsRoot と退避先を検査し、検査規則を二重化しない。A7 に反映 |
| R1-M1 | Collect に入力検証・codec・設定・selection 生成が同居している。全 file 列挙は既存の `Get-BuildInventory` と重複する | 採用（方針） | build-info codec は Contract、退避は Store 相当、orchestration は Application に置く。inventory は既存関数を export して再利用する。§3 を改稿 |
| R1-M2 | 最低条件2で09-24の1件に固定しているが、U4=(a) では同じ buildId が既に予約済みかもしれない。代わりに09-20を使うと file 数の条件が変わる | 採用 | 判定 identity を最低条件2に固定。再レビューの blocker を受け、別 identity への切替を削除した |
| R1-M3 | 退避が成功して publish が失敗した後、再実行すると「退避先既存」で拒否され、先へ進めない | 採用 | 同じ buildId の退避先が build-info と全 hash で一致すれば退避を省き、publish（`inspect`）から再開する。最低条件3に書く |
| R1-M4 | A7 にテストも検証経路も無い。`build collect` は新しい操作なのに、疎通確認が「なし」になっている | 採用 | 並行 rename の offline ケースを追加する。実R2 での collect は「未確認。初回確認は Phase C」と確認地点を書く |
| R1-m1 | ゲートの分類が無い。`Assert-BuildToken` は export されていない。トークンが既存テスト例と違う | 採用 | 分類欄を追加。トークンは既存テストに合わせて `windows-x64-player` / `il2cpp-high` とする（R2 も configuration は `il2cpp-high`）。U5 を改稿 |
| R2-1 | selection を退避先 root の中に置けないことを明示すべき | 採用 | A4 に「selection は root 外の private operation に置く」と反映 |
| R2-2 | 並行2回実行と同じ identity の再実行を最低条件に入れるべき | 採用（R1-M3 と統合） | 再実行は「再開」として定義し、並行実行は片方だけ成功する |
| R2-3 | 4季節で約880 file になる推定を、対象外を切る根拠に使っている | 採用 | 注記として残し、分離を後送りする根拠は「Player が content を内包しないと起動しない」ことに一本化する |
| R2-4 | 「人手なし」と「人が1コマンドを実行する」の文言が揃っていない | 採用 | 問いを「JSON を手書きせず1コマンドで」に揃える |
| R2-U2 | ArtifactsRoot は既定値を置かず、引数または環境変数で必須にする | 採用（owner 決定） | 明示必須にする。設定は別途のセットアップ script（bat 等）に任せ、後続スライスへ送る。A7 |
- A2 再レビュー（入力版 `bdd182d` = r2、2026-10-11、owner 指示）: cursor-agent / Grok 系 / xAI。owner はベンダー・モデルの変更を求めなかった。結果は「凍結不可」、blocker 1 / major 3 / minor 2。主担当がコードで照合し、全件採用して r3 に反映した。
  - blocker: 判定 identity が §2 では固定、§5 では別 identity への切替を許していて矛盾する → 切替を削除し、前提が崩れたら判定を止める。#111 が別の buildId を使っていたことも確認して記録した。
  - major: Stage → Application → Stage の循環 → Application が一覧を渡す形に固定。receipt の実 field 名（`identity`/`target`）との対応 → A1 に対応表。`build inspect` の必須引数が欠けている → §5 のコマンドを修正。
  - minor: 採否表の反映先のずれ → 修正。`--artifacts-root` の省略可否 → §3 CLI 行に明記。build-config の寿命 → §3 に追記。
- A3 統合担当・モデル・採否: 主担当 Claude Opus 5.5（Anthropic）が統合し、owner が13件の採用を決定。再レビュー6件は owner 指示（レビュー結果を反映して凍結）に従い全件反映。r3 を凍結版とする。
- C' 用に予約した担当・モデル・ベンダー: owner 決定（2026-10-11）。C' は人間担当、または B・C と異なるモデルの AI 担当とする。ベンダーの相違は求めない。どちらの場合も新規セッションと blind audit bundle は必須。
- 独立性の強化条件を満たせない場合の理由: 利用可能な AI 経路が Anthropic（Claude）と cursor-agent 経由の xAI（Grok 系のみ）の2系統で、Codex 系は利用不可。owner は、モデルが異なれば blind 監査として十分と判断し、強化条件（未関与ベンダー）を求めないと決めた（2026-10-11）。`独立性制約あり` として記録する。

## 6. Phase B 実装結果

- 担当・モデル: Claude Code / Claude Opus 5.5（Anthropic）。2026-10-11。
- implementation base / head: `32ec358` / `d35823c`。
- 変更（§3 の責務マップどおり。依存は Application → Stage → Contract の一方向）:

| file | 行数（凍結時 → 実績） | 内容 |
|---|---|---|
| `tools/Artifacts/BuildContract.psm1` | 113 → 201 | identity → buildId（A5）、系列対応表（A6）、receipt/transport.json の読取と照合（A1）、content/ と manifest の照合（A2）、build-info.json の固定順直列化（A1）、上限の事前検査（A8）。`Assert-BuildSourceRoot` を export |
| `tools/Artifacts/BuildStage.psm1`（新規） | 0 → 121 | ArtifactsRoot の解決と検査（A7）、配布形検査（A8）、staging → 読込hash照合 → 書込後の再読照合 → rename（A3）、既存退避先の全hash一致判定 |
| `tools/Artifacts/BuildApplication.psm1` | 189 → 260 | `Invoke-BuildCollect`。入力の一覧は既存 `Get-BuildInventory` で取り Stage へ渡す。再実行の分岐（最低条件3） |
| `tools/artifacts.ps1` | 106 → 109 | `build collect`。`--artifacts-root` だけ省略可 |
| `tools/Artifacts/tests/BuildCollect.Tests.ps1`（新規） | 0 → 300 | 9ケース（下記） |
| `tools/Artifacts/tests/BuildCollectChild.ps1`（新規） | 0 → 47 | 実CLIを別processで通す子。並行ケースは名前付き event で同期 |
| `tools/Artifacts/README.md` | 206 → 236 | `build collect` と `OSM_ARTIFACTS_ROOT` |

- 行数: `BuildContract.psm1` は予想（約185）を超えて +78%。追加分は全て §3 の P 分類（保存契約の値の規則）で、分割しない判断は §3 のとおり。
- B 適応（設計判断を増やさない実装詳細）:
  - 成功receiptが既にある場合、receipt の entries・系列が今回の退避先と一致することを確かめてから同じ参照を返す。一致しなければ `receipt-conflict` で止まり、PUT しない。最低条件3「同じ入力で再実行したとき」の前提を確かめる検査で、#111 の receipt 形式は変えない。
  - `OSM_ARTIFACTS_ROOT` は、起動した process が引き継いだ環境変数を読む。ユーザー環境変数の registry は読まず、書き込まない。
  - テスト補助として `BuildCollectChild.ps1` を追加した（§3 の表に無い）。既存の `BuildPublishChild.ps1` は selection 前提のため流用しなかった。
  - 上限のうち相対path 240字超は、既存 `Get-BuildInventory` の path 検査で退避前に拒否される（reasonCode は `unconfirmed`）。他の上限は `size-limit`。
- テストケース（`BuildCollect.Tests.ps1`）: `collect-roundtrip`（環境変数経由、build-info 11 field と値、退避先＝入力−receipt＋build-info、fetch 一致、PUT 1）、`rerun-and-resume`（予約前失敗後の再開、receipt 有りで PUT 0、予約有り receipt 無しで `publish-unresolved`・PUT 0、他の退避先・他の receipt・元が不変、DELETE 0）、`artifacts-root-reject`（未設定・相対・不存在・junction・入力との親子両方向・所有store）、`source-reject`（receipt 欠落・backup・work/・相対・file数・単一size・総量・path長）、`transport-and-content-reject`、`identity-and-series-reject`、`stage-integrity`（copy前の元改変、staging改変、他のstaging不変、既存退避先の不一致）、`concurrent-stage`（2 process を rename 直前で揃え、片方だけ成功・PUT 1）、`fresh-process-cli`（実CLI、再実行、未設定、grammar 拒否）。
- 実行した確認（B exit。implementation head `d35823c` の作業ツリー）:
  - `pwsh tools/Artifacts/tests/BuildCollect.Tests.ps1 -Case '*'` → 9 / failed 0。
  - `pwsh tools/Artifacts/tests/BuildPublish.Tests.ps1 -Case '*'` → 9 / failed 0。
  - 変更・新規 PowerShell 6 file の構文解析 → error 0。
  - `pwsh tools/contract-audit.ps1` → errors 0、`pwsh tools/docs-audit.ps1` → errors 0、`git diff --check` → 指摘なし。
  - 通信・資格情報なしで、判定用の実Player（`20260924T202227427Z-b80d…`）に対して Contract/Stage の検査関数だけを実行した。receipt・transport.json・content/ 220 file の照合が成立し、退避予定は 266 file・125.48 MiB で上限内だった。§4 の差し戻し条件「A2 が実出力で成立しない」には当たらない。退避・publish は実行していない。
- 未実行（Phase C の担当）: 判定必須の offline suite 全件（BuildRetention・ArtifactPackage・ArtifactTransfer）、Packaging/Transport の Release build、最低条件2の実R2往復と前提確認の `build inspect`。
- Unity Editor のコンパイル確認: 対象外（Unity側を変更していない）。

## 7. Phase C

- 担当・モデル: Claude Code subagent（新規セッション）/ Claude Sonnet 5.5 / Anthropic。Phase B と異なるモデル。2026-10-11〜12。
- 種別: 発見C → 判定C を同じ担当が続けて実施した。対象は base `32ec358` / implementation head `d35823c`。判定前に `git diff d35823c HEAD -- tools/` が空であることを確認した。
- 使用した入力: Phase A snapshot（凍結版 `f1cbf1e` の本書）、Phase B result snapshot（確認済み）、固定 diff。
- 構造適合: §3 の責務マップどおり。依存は Application → Stage → Contract の一方向で、Stage は BuildApplication に依存しない。BuildStore は変更なし。
- 現在の問いを阻害する指摘: なし。
- 後続スライスへ移送する指摘（いずれも semantic、違反根拠なし）:
  - collect が publish まで進んで失敗した場合も、出力の `publish` は `published` を示す（成否は `status`/`reasonCode` で判断する）。
  - 異常終了した process の `.staging` が残りうる。回収は ArtifactsRoot 清掃のスライスへ送る。
  - receipt の entries 照合（B 適応）は #111 の形式を変えていない。
- 実行したテスト・検査（判定必須。生出力は evidence bundle に収録）:
  - Packaging / Transport の Release build: exit 0。
  - offline suite（registered=selected=executed、failed 0）: BuildCollect 9、BuildPublish 9、BuildRetention 9、ArtifactPackage 9、ArtifactTransfer 20。
  - 変更 PowerShell 6 file の構文解析 error 0。`contract-audit` errors 0、`docs-audit` errors 0、`git diff --check` 指摘なし。
  - 最低条件2の実R2往復:
    - 前提確認の `build inspect` は `receipt-unavailable`。ArtifactsRoot は空だった。
    - `build collect`: `stage=created`、266 file。
    - 別 process の `build fetch`: passed。退避先と展開先の全 entry・hash が一致した。退避先は「元 − build-receipt.json + build-info.json」。
    - 元出力の全 hash は前後で不変。
  - 再実行（2回目）: 同じ reference/packageSha256、`publish=existing`。
    - 2回目の前後の store snapshot は、取得スクリプトの不具合で空になった（収録不備）。
    - このため同じ入力で3回目を実行した。前後で receipt/intent/予約の hash・mtime、builds-v1 の全 file、transfers 直下の operation 一覧が不変だった。新しい publish operation は作られていない。reference/hash は同一。
- 判定: GO（最低条件1〜3を満たし、常時契約違反なし）。
- 判定必須のうち未実行: なし。
- evidence bundle の取得案内・状態: 確認済み（publish 後、別 process の fetch で全37 file の hash 一致）。

## 8. Phase C'

- 担当・モデル: Claude Code subagent（新規セッション）/ Claude Haiku 5.5 / Anthropic。B（Opus 5.5）・C（Sonnet 5.5）と異なるモデル。予約どおりベンダーの相違は求めない（`独立性制約あり`、§5）。最初に起動した Claude Fable 5.1 は利用クレジット不足で開始前に終了したため、Haiku 5.5 に替えた。
- 入力: C' blind bundle（凍結 Phase A snapshot、所見を含まない Phase B result、完全 diff、判定必須の生出力）。担当が自分の process で fetch し、42 file 全ての hash 一致を確認した。現行 HANDOFF・PR・commit message・C の所見は読んでいない。
- 現在の問いを阻害する指摘:
  - コードの欠陥: なし。A1〜A8 を `d35823c` のコードと照合した。
  - 証拠の未充足（監査担当の判断）: 「再実行で PUT していない」は、`publish=existing` の戻り値経路からの推定にとどまり、R2 側の観測（PUT 件数・object 不変）が無い。2回目の store snapshot も欠けている。
- 後続スライスへ移送する指摘:
  - 失敗時も `publish=published` と表示される（C と重複）。
  - build-config を `Read-BuildConfig` で検証した後、`ReadAllBytes` で読み直して複写しており、二度読みの間の同一性は保証しない。
  - 並行実行の敗者は、退避先が一致しても `stage-conflict` で止まる（A3「一致なら完了済み」と最低条件3「片方だけ」の緊張。後者を優先した実装）。
  - path 長超過のテストは reasonCode を見ていない。
  - `BuildContract.psm1` は +78%。`Write-BuildOperationFile` を Application に置いた。
  - 異常終了時の `.staging` に掃除規則が無い。
  - evidence の生出力にローカル path が入る。
- 監査できなかった範囲: Unity 側（変更なし）、実R2の再実行、R2 object の metadata。
- 監査結果: **FAIL**（コードの阻害欠陥は無い。理由は上の証拠の未充足）。
- C' blind bundle の取得案内・状態: 確認済み（送信側の fetch で全42 file の hash 一致、監査担当の fetch でも一致）。
- C と C' の突き合わせと採否は Phase D（人間）で行う。突き合わせの材料:
  - 3回目の前後 snapshot（`25`/`27`）は blind bundle に収録されている。publish の PUT は `Invoke-BuildPublish` の新規 operation と intent の書込みを経てからしか発生しない。
  - R2 object の metadata を観測する CLI は、凍結時の検証経路に含まれていない。

## 9. Phase D

- 判断者: owner（人間）。記録: Claude Code / Claude Opus 5.5。2026-10-11。
- C と C' の突き合わせ: コードの阻害欠陥は、C・C'・PR レビュー（cursor[bot]、`d35823c` 対象）のいずれでも 0 件だった。C' の FAIL 理由は「再実行で PUT していない」に R2 側の観測が無いことだった。
- owner の観測（C' の不足を埋めるもの）:
  - R2 から `bundle.zip` を直接ダウンロードした。SHA-256 とサイズは判定C の期待値と一致した。
  - 展開後の比較で一時的に差分が出たが、原因は展開先のフルパスが 260 字を超えて 18 file が展開されなかったことだった。zip 自体には manifest の 266 file がすべて入っていた。
  - R2 の Last Modified は 2026-10-11 09:00:09 JST だった。1回目 collect の PUT→読戻し→receipt 確定（09:00:15.87）の範囲内で、2回目（09:00:48）・3回目（09:01:43）の後に更新は無い。
- 採否: owner が案A を選んだ。上記の観測で C' の不足は解消済みとして採用し、GO とする。C' の FAIL という記録は §8 にそのまま残す。
- 後続へ送る指摘（C・C'・PR レビューの和集合）: 失敗時の `publish` 表示、異常終了時の `.staging` 回収、build-config の二度読み、並行敗者の扱い、path 長超過の reasonCode、evidence 生出力のローカル path。program の §4 B へ移送した。
- Evidence の保存・引渡し: Phase A snapshot、Phase B result snapshot、判定C evidence bundle、C' blind bundle は確認済み（§0）。削除前の本書は final record として publish し、別 process の fetch で一致を確認する。
- task 終了・後始末: owner の完了指示を受けて、merge 後に `workflow-task.ps1 end -Reason completed` を記録する。
