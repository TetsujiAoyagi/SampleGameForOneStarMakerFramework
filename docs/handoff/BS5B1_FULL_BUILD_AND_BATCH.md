# BS5b-1 `Full` の build・publish と batch

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `C' 1 巡目完了、判定 C の照合 script を修正済み（2 巡目待ち）`（2026-11-02。A3 凍結は同日）
- branch: `claude/bs5b-operation-routes`
- implementation base commit: `26f34046`（`develop`、PR #152 のマージ。§1.3 の現況は `6fe18810` で確認し、`26f34046` までの差分は workflow 文書だけ）
- implementation head commit: `36b966c6`（差し戻し 1 巡目。初回の Phase B は `3ceb19ab`）
- risk: `high`（全ての content build が通る `BuildContentCoordinator` の workspace の扱いを変えて削除を伴う。publish の受け付け条件を変え、BS4 の publish 経路にも効く）
- owner: 発注者 / BS5b-1 担当
- created: 2026-11-01（BS5b を 2026-11-02 に BS5b-1 と [BS5b-2](BS5B2_PURPOSE_PLAY_ROUTES.md) に分けた）
- expires: BS5b-1 の Phase D 完了時
- harvest to: Architecture §18 §4（「SampleGame の宣言と Editor build」「BuildSystem の後続」の build mode・batch）、[Harness README](../../tools/Harness/README.md#起動前の-path-予算検査) の「既知の予算外」、[BuildSystem program §7](BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md#7-buildsystem-の他の系列の所在) の BS5b-1 行
- workflow task: `bs5b1-full-build-batch`（2026-11-02 start）
- Phase A snapshot の取得案内・状態: ローカル固定済み（未昇格）。A3 凍結 snapshot（合成 fixture F1〜F5 と期待出力、生成 script。§5）を task `bs5b1-full-build-batch` に `evidence stage` で固定した。取得案内は判定 C と C' の起動指示で渡す（同じ PC では task の `evidence inspect` で引き直せる）。Phase B は読まない
- Phase B result snapshot の取得案内・状態: 未生成（B の結果は §6 と implementation head の commit で渡す。evidence bundle は発見 C が implementation head から作る）
- evidence bundle の取得案内・状態: ローカル固定済み（未昇格）。判定 C の判定 evidence bundle（implementation head `36b966c6` 固定。diff・機械検査・全 EditMode と VFX の XML・log・AC14 の生記録・照合 script と F1〜F5 の結果）を task `bs5b1-full-build-batch` に `evidence stage` で固定し、別 process の fetch で閲覧を確認した。取得案内は C' の起動指示で渡す。HC1 の原記録は未取得
- C' blind bundle の取得案内・状態: ローカル固定済み（未昇格）。C'（DR1 により GO 判定に使う。HC1 の原記録は含まない）の 2 巡目用 blind audit bundle（head `36b966c6` 固定、所見を含まない。1 巡目から、修正後の照合 script・F1〜F5 の出力・script の修正差分を加えた）を task `bs5b1-full-build-batch` に `evidence stage` で固定し、別 process の fetch で閲覧を確認した。取得案内は C' 担当へ渡す（worktree の外）
- 引継ぎ票: **C' 2 巡目の CP3 に発注者が裁定（§7 の DR2、2026-11-02）。次は判定 C（照合 script の (v) を DR2 に合わせて直す）。** 引き継ぐ commit はこの票を含む commit（SHA は起動指示で渡す）。GO 候補 head（implementation head）`36b966c6`、base `26f34046` は変わらない。作業: `check-hc1.ps1` の (v) の右辺を DR2 の集合にする（`resolve-selected.json` は Resolve の出力のままにし、script が bootstrap 2 件を足すか、別 file で渡すかは判定 C が選ぶ）。C' 2 巡目の後続入力にあった大小文字の区別（製品は `Ordinal`）は、合わせて直してよい。凍結した F1〜F5 の期待出力と一致することを確かめ、C' 用 bundle を作り直して stage する。C' 用の取得案内は worktree の外に r3 として置く。その後、C' 3 巡目（CP3 の解消を中心に。C' 用決定記録として DR2 を渡す）→ 判定 C の実行担当が HC1（§5）→ HC1 を加えた最終判定 C（DR1。最終 C' は行わない）。判定 C の結果は §7.3。発見 C・判定 C の所見と DR1 を C' へ渡さない。push と PR は未実施。

### PR 状態ブロック

PR を作る前は §0 を唯一の正本とする。

---

## 1. 目的と対象外

- **目的:** BS5a が作った宣言（`BuildPurposeDeclaration`）から、前回の build の残骸を含まない成果物を build して publish する経路を作り、Editor の外（CI・別 PC）から宣言名だけで起動できるようにする。publish の staging を path の予算に収める。
- **分割（2026-11-02）:** BS5b は、成果物を作る側（本スライス）と、宣言から Play する側（[BS5b-2](BS5B2_PURPOSE_PLAY_ROUTES.md)：Play As・検証 Play・From published directory）に分けた。BS5b-2 は本スライスの `Full` の build・publish と batch の結果 file の上に乗る。A2 は分割前の BS5b 全体で行い、AC・I・E・D の番号は分割前のまま残す（A2 の記録と照合できるように）。
- **対象外:**
  - BS5b-2 の範囲（Play As、検証 Play の 1 操作、Editor directory Play の初回 Scene、From published directory、Delivery 窓の宣言対応、共通 ledger）。
  - **d5b（S-4d5b）の実証。** d5b は BS5b を待たない（[S-4 program §6.3](S-4_FULL_SPEC_WORLD_AUTHORING.md)）。
  - **identity 後続:** 入力 identity、設定 snapshot、`content/index`、`priorRevisions`、build を skip する再利用。revision（`UtcNow + Guid`）の形式は変えない。所在は [BuildSystem program §7](BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md#7-buildsystem-の他の系列の所在)。
  - 経路の連鎖、object 単位の格納と差分更新、再 build の bytes の決定性、`outcome.json` の記録形式の整理（Architecture §18 §4「BuildSystem の後続」）。
  - R2・HTTP への publish（publish は local の published directory まで）、DIST の install と cache の契約の変更、CI 基盤への組み込み（Unity の起動の仕方は呼ぶ側の責務）。
  - InGame の Spring に入る Player の build 経路、BS4 Player の動作の変更。
- **現況:** §1.3（A0、`6fe18810` で確認）。

### 1.1 Phase A の判断基準（発注者。S-4 program §0 と BS5a の Phase A から転記）

- **主目的は BuildSystem。** Build 側の既知の課題（transport staging の path が予算の外）は「やらない」で片付けず、BuildSystem の作業の候補として扱う。本スライスは transport staging の path を範囲に含む。
- **残すコードは SampleGame に必要なものだけ。** 生成や authoring の道具は不要なら一時スクリプトで作り commit の前に消す。
- **checkout のルート。** BS4 Player を build するスライスは、ルートが 96 文字以下の checkout で作業する（transport staging の path の制約。本スライスが直すまで有効）。この worktree のルートは 31 文字。
- **実ユースケースで通す。** UC6（§1.5）が成り立つことを確かめる。

### 1.2 常時契約と program から転記した制約

- Game → Framework の一方向。Framework は season 名、Cell 座標文法、職種 role、`CellCompanionSet` を知らない。asmdef 参照を足さない（足す必要が出たら A へ返す）。
- Editor コードを Runtime アセンブリに置かない。Unity 側の `.cs` は `#nullable enable`、`record` と `init` accessor を使わない、破棄されうる `UnityEngine.Object` は `== null` で判定する、公開 API のログ抽象は `ILogger<T>`。
- path は `tools/path-budget.json` の予算に従う（repo 相対 150 文字。ルート + 150 ≤ 259）。
- `unity test` / `unity run` を使わない。Unity テストは `pwsh tools/run-tests.ps1`。Phase B は Editor 操作と compile 確認まで、Unity テスト・content build・Player build・batch の実行と判定は Phase C。Scene / asset YAML の手編集は禁止。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。

### 1.3 A0 の現況（`6fe18810`）

BS5a 以前の事実のうち BS5a が変えていないものは BS5a の A0（`git show b4ae3d78^:docs/handoff/BS5A_PURPOSE_SELECTION.md` §1.3）で確認済みで、ここでは本スライスの判断に要るものだけを再確認して転記する。

**宣言（BS5a 後）。** `SampleGameBuildPurposes` が 6 宣言（`all-full`、`spring-full`、`spring-whitebox`、`spring-full-whitebox`、`bs4-spring-full`、`spring-planner-whitebox`）を持ち、`SampleGameBuildPurposes.Find` が名前の ordinal 一致で宣言を返す（未知は `ArgumentException`、`SampleGameBuildPurposes.cs:39-41`）。宣言は `BuildContentRoot.Declaration` と transport manifest の `declaration`（名前・表現の主値・`RuntimeDefaults`。どちらも任意 field）に写る。

**build と publish の呼び手。** menu `Tools/OSM/Content/Build <contentSet>`（宣言ごと 5 件）は `SampleGameContentBuild.Build` → `BuildResult(declaration)` を呼び、Content Directory を `artifacts/bs2b/content/<identity>/` に作るだけで publish しない（`SampleGameContentBuild.cs:23-47`）。`ContentTransportPublisher.Publish(result, publishRoot)` の製品の呼び手は BS4 coordinator だけ（`SampleGamePlayerBuildCoordinator.cs:46`、publish 先 `artifacts/bs4/transport/<identity>`）。テストでは `BuildContentDirectoryIntegrationTests` が既定の request で 2 回 build し（2 回目で workspace の再利用を確かめる）、2 回目の result を publish する（同 file の 240-263 行付近）。`PlayerBuildIntegrationPolicyTests` も publish を呼ぶ。transport manifest の `revision` は build identity（`ContentTransportPublisher.cs:81`）。

**workspace と publish（最小性 (2) の保留）。** `BuildContentCoordinator` は Unity の出力先 workspace を `<root>/work/<target>/<contentSet>` で contentSet ごとに再利用し、成功後に workspace を丸ごと staging へ複写して `content/<identity>` へ移す（`BuildContentCoordinator.cs:57-59`、`CopyDirectory` は全 file `:137-145`）。adapter は `BuildContentOptions.None` で `BuildPipeline.BuildContentDirectory` を呼ぶ（`UnityContentDirectoryAdapter.cs:97`）。Unity が workspace の古い出力を消すかは未確認なので、前回の選択の file が今回の成果物と publish に混ざりうる。BS5a はこの最小性 (2)「publish の成果物に前回の workspace の残骸が混ざらない」を BS5b（publish の `Full` 必須）へ保留した。BS5a の高コスト証拠では build の前に workspace を手で消して回避した。

**transport staging の path。** `ContentTransportPublisher` は publish 先の sibling に `<final>.<guid32>.staging` を作る（`ContentTransportPublisher.cs:34`）。BS4 の publish 先 `artifacts/bs4/transport/<identity>`（identity は 52 文字。`yyyyMMddTHHmmssfffZ` + `-` + Guid の `N`、`BuildContentCoordinator.cs:53`）の staging の中の Unity の file は repo 相対 163 文字になり、ルート 97 文字以上の checkout で 260 文字を超えて `DirectoryNotFoundException` で失敗する（2026-10-27 に S-4d3 で観測、未修正。Harness README「既知の予算外」）。final 側の同じ file は 122 文字前後。coordinator 自身の staging（`<root>/staging/<identity>`）は final（`<root>/content/<identity>`）と同じ長さ。

**batch。** content build と publish を Editor の外から起動する入口は無い。`tools/unity-editor.cmd` は起動中の Editor の Pipeline server への `status` / `command` / `eval` / `pipeline list` だけを通し、`menu` を通さない。1 build は約 4.5 分。clean checkout（`Library/` 無し）からの batch build の所要時間は未測定。

### 1.4 参照者と経路（本スライスの行）

| 誰 | 必要なものをどう知るか | 持っているもの | BS5a 後（現況） | BS5b-1 後 |
|---|---|---|---|---|
| 別 PC / Cloud（build する側） | 宣言名 | clean checkout の source と Unity | menu | batch で `Full` build と publish、結果 file と終了コード |
| Editor（成果物の検証）・別 PC（受け取る側） | 宣言・結果 file | published directory | menu の `Build` と Delivery 窓の手入力 | 変えない（BS5b-2 が本スライスの publish と結果 file を使う） |
| Player | baked の宣言 | 内包 / installed | BS4 Player に付随 asset と宣言の既定値が内包される | 変えない（BS4 coordinator は `Full` で build する） |

### 1.5 ユースケース（本スライスの行）

| # | 誰が何をする | 今（BS5a 後） | BS5b-1 後 |
|---|---|---|---|
| UC6 | CI が clean checkout で目的 X を build し publish する | menu しか無い | batch 入口と終了コード。publish は `Full` の build だけ。結果 file（`last-result.json`）が UC7（BS5b-2）の期待値の出所になる |

UC2・UC4・UC7 は BS5b-2。UC1・UC3・UC5・UC8〜UC11 は BS5b で変えない（BS5a の A0 §1.5 のまま）。

---

## 2. 意思決定と受け入れ境界（A1）

- **このスライスが答える問い:** 宣言名 1 つで、Editor の外から、前回の build の残骸を含まない成果物を build して publish でき、失敗は終了コードで分かり、成功と取り違える結果を残さず、publish の staging が path の予算に収まるか。

### 2.0 証明の範囲

| 主張 | 何で示すか | 示さないこと |
|---|---|---|
| 最小性 (2): publish の成果物に前回の workspace の残骸が混ざらない | AC10・AC11（番兵 file）、HC1 | 今回の build の物理 file 単位の最小性、bytes の決定性 |
| batch で clean checkout から build と publish ができ、失敗が終了コードで分かる | AC13・AC14、HC1 | CI 基盤への組み込み、別 PC の実機 |
| transport staging が path の予算に収まる | AC12、HC1 (vi) | DIST cache の install 先の path |

### 2.1 不変条件（起きてはいけないこと）

- **I4（残骸を publish しない）:** publish される成果物（`transport.json` の `files` と published directory の `content/`）は、その build の Unity の出力だけからなる。publish は `Full` で build した結果だけを受け付け、それ以外は publish 先を作らずに失敗する。`Full` の削除は workspace の通常の directory の中だけに留め、リンク（junction・symbolic link 等の reparse point）の先を消さない。
- **I5（失敗を成功と見せない）:** batch 入口は、宣言名の誤り・選択の issue・Unity build の失敗・publish の失敗のどれでも 0 以外の終了コードで終わり、今回の成功の結果 file も、その実行が作った published directory も残さない（publish の後に結果 file の書き込みで失敗した場合を含む）。前回の結果 file を今回の結果と読める状態を残さない。成否の正は process の終了コードで、結果 file は終了コード 0 の時だけ今回の結果として読む（Unity が batch の method を呼ぶ前に終わる場合、たとえば project が別の Editor で開かれている・compile error では、結果 file に触れられないため。§2.4 D8）。
- **I6（既存の契約を変えない）:** build identity と transport manifest の schema・`files` 規則・DIST の install 配置、BS4 Player の baked config と smoke の段の順序、menu の `Build <contentSet>` の挙動（`Incremental`、publish しない）は変えない。
- **I7（Framework は語彙を知らない）:** Framework 側に足すもの（build mode、publish の拒否、staging）は season 名・Cell 座標文法・職種 role・`CellCompanionSet`・`SAMPLEGAME_` prefix を参照しない。asmdef 参照を変えない。

### 2.2 成立条件（起きなければならないこと）

- **E3（`Full` 必須）:** build に mode（`Incremental` / `Full`）があり、`Full` は Unity の build の前に workspace を空にする。mode は `outcome.json` に記録され、publish は `Full` を要求する。batch と BS4 coordinator は `Full` で build する（BS5b-2 の検証 Play の 1 操作も `Full` を使う）。menu の `Build <contentSet>` は従来どおり `Incremental`（publish しない）。
- **E4（batch）:** Unity の `-batchmode -executeMethod` で呼べる入口が、宣言名を引数に `Full` build と publish を行い、結果（identity・publish 先・manifest SHA-256・宣言・buildMode・終了コード）を固定 path の JSON に書き、終了コードで成否を返す。結果 file の欄は BS5b-2 の From published directory が期待値として読む（§2.4 D8）。
- **E6（staging の path）:** transport の publish が作る staging（`<final の親>/.staging/<guid32>`）の中の各 path は、同じ file の final の path より長くならない。ルート 109 文字（= 259 − 150）の checkout で BS4 の publish が成功する長さになる。

### 2.3 受け入れ条件（検証する条件を併記）

単体 = Unity に依存しない純 C# テスト（Editor asmdef の pure class を含む）。EditMode = AssetDatabase・合成 fixture を使うテスト。操作 = Phase C の実行。HC = 高コスト証拠（§5）。

| AC | 内容 | 条件 | 種別 |
|---|---|---|---|
| AC10 | `BuildContentRequest` に mode を足し、`Full` は adapter を呼ぶ前に workspace を空にする（workspace 自体は残す）。番兵 file を workspace に置いて `Full` で build（偽 adapter）→ 成果物 `content/<identity>` に番兵が無い。`Incremental` では従来どおり残る（偽 adapter で現状を固定）。引数なしの `SampleGameContentBuild.Build`（5 menu）は `Incremental` で build する。workspace が approved root の外を指す経路は既存の検査で拒否され、削除しない。負例: workspace の中に、別の directory（番兵 file を持つ）を指す junction を置いて `Full` で build → 何も消さずに build が失敗し、junction の先の番兵 file が残る（junction は `cmd /c mklink /J` で作る。Windows で管理者権限は要らない）。workspace 自身が junction の場合も同じ | I4・E3・I6 | 単体（偽 adapter と一時 root） |
| AC11 | `outcome.json` に `buildMode` が入り（後方互換の追加項目）、`ContentTransportPublisher.Publish` は `buildMode` が `Full` でない result（欄の無い旧 outcome を含む）を、publish 先と staging を作らずに `InvalidOperationException` で拒否する。BS4 coordinator は `Full` で build する。既存の `BuildContentDirectoryIntegrationTests` は、1 回目の build を `Full`、2 回目（workspace の再利用を確かめる）を `Incremental` にし、publish には 1 回目（`Full`）の result を渡す（build の回数は増やさない）。`PlayerBuildIntegrationPolicyTests` の publish も `Full` の result を渡す | I4・E3 | 単体 + EditMode（`BuildContentReport` の往復、既存 Integration の更新） |
| AC12 | publisher の staging を `<publishRoot の親>/.staging/<guid32>` に変え（GUID は切り詰めない。`.staging/` + 32 文字 = 41 文字は identity の 52 文字より短い）、staging 内の各 file の path 長が final の同じ file 以下になる。テストは識別子 52 文字・Unity の file 名は観測の 163 文字から staging 接尾辞 41 文字と identity 52 文字を引いた最長を想定した合成の directory で、ルートの長さ 109 の時に staging と final の全 path が 259 以下であることを計算で確かめる。staging は同じ volume の move で final になり、失敗時に staging だけを消す（既存の規則） | E6 | 単体（一時 directory） |
| AC13 | batch 入口 `SampleGameContentBatch.BuildAndPublish` は引数 `-osmPurpose <宣言名>` で `Full` build と publish を行い、`artifacts/bs2b/batch/last-result.json`（identity・publishPath・manifestSha256・declaration・buildMode・exitCode）を書いて `EditorApplication.Exit(0)`。終了コード: 0 成功、2 引数の誤り（宣言名の欠落・未知）、3 選択の issue（preflight の失敗）、4 Unity build の失敗、5 publish の失敗、1 その他の例外。開始時に前回の `last-result.json` を消し、結果は一時 file に書いてから move で確定する。0 以外では結果 file に `exitCode` と理由だけを書き、その実行が作った published directory だけを消す（publish の後の結果 file の確定で失敗した場合も消して 1 を返す）。他の published directory には触らない。負例（偽の段）: publish の後に結果の確定が失敗 → 終了コード 1、その実行の published directory が無い、前回の成功結果が残っていない | E4・I5 | 単体（段を注入）+ HC1 |
| AC14 | batch の失敗経路の実観測: 未知の宣言名で batch を起動すると終了コード 2 で、`artifacts/bs2b/batch/last-result.json` の `exitCode` が 2、新しい `transport/` の directory が増えない | I5 | 操作（Phase C。content build を伴わない） |
| AC18 | 最終の全 EditMode 回帰が通る。BS3 / BS4 の既存テスト（`ContentDirectorySessionTests`、`BuildContentDirectoryIntegrationTests`、`PlayerContentConfigurationTests`、`ContentDeliveryFailureMatrixTests`、`PlayerBuildIntegrationPolicyTests`）の結果を変えない（AC11 の更新を除く） | I6 | 回帰 |

### 2.4 A1 の設計

- **D6（build mode）:** Framework の `BuildContentRequest` に `BuildContentMode Mode`（既定 `Incremental`）を足し、coordinator は `Full` の時に adapter を呼ぶ前に workspace の中身を消す（`Child` で approved root の中と確かめた workspace だけ。workspace の外の file を消さない）。`BuildContentReport.WriteOutcome` が `buildMode` を書く。`ContentTransportPublisher.Publish` は outcome の `buildMode` を読み、`Full` 以外を拒否する。coordinator の workspace の再利用（`Incremental`）は menu の日常の build の速さのために残す。削除は、approved root（またはテスト root）から workspace までの各 segment と workspace の中の各 directory の属性を見て、reparse point が 1 つでもあれば何も消さずに失敗する（中を辿らない。字句上の包含の検査だけでは、リンクの先が root の外にある場合を防げない）。削除の失敗（reparse point、他 process の file lock）は Unity の build の前の失敗として result に記録し、build を始めない（batch の終了コード 4）。汎用の filesystem 防御は作らず、この削除経路に限る。
- **D7（staging）:** `ContentTransportPublisher` の staging を `Path.Combine(Path.GetDirectoryName(final), ".staging", Guid.NewGuid().ToString("N"))` に変える（切り詰めない）。同じ親の下なので move は同じ volume。`.staging` の残骸の回収は既存の失敗時の削除のまま（異常終了の残置は Storage program §6 の保留項目と同じ扱い）。
- **D8（batch 入口）:** `SampleGame/DependOnAll/Editor/Build/SampleGameContentBatch.cs` に `BuildAndPublish()`。引数は `Environment.GetCommandLineArgs()` から読み、宣言は `SampleGameBuildPurposes.Find` で引く（名前の直接入力は batch の引数だけ）。publish 先は `artifacts/bs2b/transport/<identity>`。結果 file は `artifacts/bs2b/batch/last-result.json`（coordinator と同じく project の親の `artifacts/bs2b` から解決する。開始時に消し、一時 file から move で確定する。同時に複数の batch を走らせることは想定しない）。成否の正は process の終了コードとする。Unity が method を呼ぶ前に終わる場合（project の lock、compile error）は結果 file に触れられず前回の file が残るので、呼ぶ側（CI）は終了コード 0 の時だけ結果 file を読む。BS5b-2 の From published directory は、結果 file の中の `exitCode` と、manifest との SHA-256・identity の照合で守られる（古い結果 file は古い実在の build を指すだけで、組み違いにはならない）。結果 file の欄（identity・publishPath・manifestSha256・declaration・buildMode・exitCode）は BS5b-2 が期待値として読むので、本スライスで固定する。その実行が作った published directory の所有はその実行にあり、成功結果の確定より前の失敗では batch が消す（AC13）。`-quit` に頼らず `EditorApplication.Exit(code)` で終える。pwsh の wrapper は作らない（Unity の起動の仕方は呼ぶ側の CI の責務。判定の起動コマンドは §5 に書く）。

### 2.5 ここでは答えない問いと所有する後続

| 問い | 所有 |
|---|---|
| 宣言から Play する経路（Play As、検証 Play、From published directory、Editor directory Play の初回 Scene） | [BS5b-2](BS5B2_PURPOSE_PLAY_ROUTES.md) |
| 入力 identity、設定 snapshot、`content/index`、`priorRevisions`、build を skip する再利用 | identity 後続（[BuildSystem program §7](BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md#7-buildsystem-の他の系列の所在)） |
| R2・HTTP への publish、別 PC の実機での受け取り、CI 基盤への組み込み | BuildSystem 後続（Storage program §6「取得経路と配布」と接続） |
| `.staging` と `work/` の残置の清掃 | Storage program §6「ローカルの残置の清掃」 |

- **判定定義:** 不変条件 I4〜I7 と成立条件 E3・E4・E6 を AC10〜AC14・AC18 と HC1 で確かめ、全て成立で GO。1 つでも不成立なら NO-GO。
- **停止規則:** 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。スパイクではないので CONDITIONAL ACCEPT は使わない。
- **A3 後の例外承認:** なし。
- **決定記録:** §7 の決定記録（DR1・DR2）。
- **本文へ転記した実装制約:** §1.2。
- **未決事項:** §2.6。

### 2.6 未決事項

- **U2（規模）:** 見積もりは製品コード約 200 行（Framework Editor 約 60、SampleGame の build と batch 約 140）、テスト 300〜400 行。変更理由は 2 つ（build mode と publish、batch）で、batch は `Full` と publish に依るので 1 スライスとする。
- **U4（`Incremental` の残し方）:** D6 で閉じた（menu は `Incremental` のまま、publish する経路だけ `Full`）。代案の全経路 `Full` は menu の build が毎回 Unity の full build になり遅くなるので不採用。

---

## 3. 責務マップ（A1）

| file | 責務 | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|---|
| `OneStarMaker/Scripts/Editor/Build/Content/BuildContentCoordinator.cs` | `Full` の workspace の削除 | `Full` | 1 build | adapter | public（request の欄） | 単体（AC10） | 147 → 170 |
| `.../BuildContentReport.cs` | `buildMode` の記録 | `Full` | 静的 | — | internal | 往復（AC11） | 142 → 150 |
| `.../ContentTransportPublisher.cs` | `Full` の要求、staging の path | `Full`・staging | 1 publish | outcome | public | 単体（AC11・AC12） | 118 → 135 |
| `SampleGame/DependOnAll/Editor/Build/SampleGameContentBuild.cs` | build mode の引数（menu は `Incremental`） | `Full` | 静的 | coordinator | public | 既存 + AC10 | 126 → 140 |
| `.../SampleGamePlayerBuildCoordinator.cs` | `Full` で build | `Full` 必須 | 1 build | 同上 | 既存 | 既存 | 151 → 153 |
| `.../SampleGameContentBatch.cs`（新規） | batch 入口、終了コード、結果 file の確定と、その実行の published directory の回収 | batch | 1 process | build・publish | `public static` | 単体（AC13） | 0 → 120 |

## 4. 実装計画（A1）

### 4.1 順序

1. Framework: build mode・`buildMode` の記録・publish の拒否・staging（AC10〜AC12 の単体を先に書く）。確認地点: 削除の対象が approved root の中、非 `Full` の publish の拒否、影響を受ける既存の呼び手とテストの列挙。
2. SampleGame: `SampleGameContentBuild` と BS4 coordinator に mode を通し、既存の Integration テストを AC11 のとおり更新する。
3. batch 入口（AC13）。確認地点: 結果 file と publish の失敗時の所有が明確、結果 file の欄が D8 のとおり。
4. compile 確認と `pwsh tools/contract-audit.ps1`。Unity テスト・content build・batch は実行しない（Phase C）。

### 4.2 Phase B から Phase A へ差し戻す条件

- asmdef 参照の追加が要る。
- `Full` の workspace の削除が approved root の外に及ぶ経路を断てない。
- BS4 Player の receipt の形、transport manifest の schema を変えないと条件を満たせない。

### 4.3 対象外を維持する方法

Play・bridge・初回 Scene のコードに触らない（BS5b-2）。identity・index の欄を足さない。R2・HTTP に publish しない。

## 5. テストとレビュー計画（A1）

- **単体:** AC10〜AC13（Editor asmdef の pure class と偽の段・偽 adapter）。
- **差し戻し中の起点 `-Filter`:** 新しいテスト class と、`ContentDirectorySessionTests`・`BuildContentDirectoryIntegrationTests`・`PlayerBuildIntegrationPolicyTests`・`PlayerContentConfigurationTests`・`ContentDeliveryFailureMatrixTests`。
- **判定必須テスト:** 最終の全 EditMode 回帰（空 filter）。PlayMode は対象外（Runtime を変えない）。content build を伴う `BuildContentDirectoryIntegrationTests` は全 EditMode に含まれる。
- **全 EditMode 回帰の適用除外:** なし。
- **操作・実行時・目視条件の検証経路:**
  - **AC14（batch の失敗経路）:** 担当は判定 C の実行担当。この worktree で Editor を閉じ、`<Unity.exe> -batchmode -nographics -projectPath unity -executeMethod SampleGame.DependOnAll.Editor.Build.SampleGameContentBatch.BuildAndPublish -osmPurpose no-such-purpose -logFile <log>` を起動。観測: process の終了コード、`artifacts/bs2b/batch/last-result.json`、起動前後の `artifacts/bs2b/transport/` の一覧。保存: log・結果 file・一覧を判定 evidence bundle へ。
  - 動作確認（Phase B の後）: 対象なし（Editor 操作の条件が無く、batch と content build は Phase B で実行しない）。引継ぎ票の次の担当は発見 C とする。
- **未知の操作経路の疎通結果と、必要な検証支援・確認地点:** clean checkout からの batch build の所要時間は未測定。初回確認は HC1 で Phase C（時間は判定に使わず記録する）。
- **人間の判断が必要な条件:** なし。
- **高コスト証拠:**
  - **HC1（clean checkout からの batch build と publish）:** 判定 C の実行担当が、GO 候補 head から新しい detached worktree をルート 109 文字以下（予算の上限の確認を兼ねて、可能なら 97 文字以上）の path に作り（`Library/` 無し）、Editor を開かずに AC14 と同じ形で `-osmPurpose spring-full` を起動する。観測: 終了コード、`last-result.json`、published directory の `transport.json`・`content/` の一覧、`outcome.json` の `buildMode`、`preflight.json` の selected、log、所要時間。期待値の導出規則: (i) 終了コード 0、`buildMode` は `Full`、`last-result.json` の `manifestSha256` が published directory の `transport.json` の SHA-256 と一致し、`identity` が `transport.json` の `revision` と一致し、`declaration` が `transport.json` の `declaration` の名前と一致する。(ii) `transport.json` の `declaration` は `SampleGameBuildPurposes.SpringFull` の欄（名前 `spring-full`、表現の主値 `Full`、`world:cellCompanionSet=Full`）。(iii) `transport.json` の `files` の path 集合が published directory の `content/` の実 file 集合と一致し、その全 file の SHA-256 が `files` と一致する。(iv) build の前に worktree の `artifacts/bs2b/work/<target>/spring-full/` に番兵 file `bs5b-sentinel.txt` を置き（新しい worktree なので directory を作る）、published directory に番兵が無い。(v) `preflight.json` の selected の stable key 集合が、同じ head の Editor で `SampleGameContentBuild.Resolve(SpringFull)` が返す集合と一致する（Resolve の出力は判定 C が同じ head で取る。BS5a の I4 で `840b6609` の policy と一致することは確かめ済み）。(vi) 最長の path（published directory と staging の中）が 259 文字以下。
  - 順序は review-evidence の「高コスト証拠と最終証拠ラウンド」に従う（判定 C → 暫定 C' → HC1 → 最終判定 C と最終 C'）。
- **判定 C の照合 script の方式:** **判定 C の offline 実行。** 照合するのは HC1 の原記録（`transport.json`・`content/` の一覧と hash・`outcome.json`・`preflight.json`・`last-result.json`）の file 検査だけで、通信も資格情報も要らない（publish は local）。判定 C の担当が script を書き、原記録の複製に対して、資格情報・本番設定・通信に届かない環境（sandbox 内の pwsh）で実行する。script は判定 evidence bundle に置く。原記録へ適用する前に通す合成 fixture は、A3 で次の 5 件と期待出力を作って Phase A snapshot に収録し凍結する: (F1) 合格（`files` と実 file が一致、番兵無し、`buildMode=Full`、宣言一致、結果 file と manifest の SHA-256・identity が一致）、(F2) 実 file に `files` に無い 1 file がある → 不合格、(F3) 番兵がある → 不合格、(F4) `outcome.json` の `buildMode` が欠落 → 不合格、(F5) `last-result.json` の `identity` が `transport.json` の `revision` と違う → 不合格。期待値の導出は上の (i)〜(vi) の規則で、機械向けの期待値 file と script 本体は git に入れない。
- **機械検査:** `pwsh tools/contract-audit.ps1`（`#nullable enable`、Editor コードの配置、path 予算等）。
- **A0/A1 主担当・モデル・ベンダー:** Claude Opus 5.5（Anthropic）、2026-11-01。
- **A2 独立レビューごとの観点・担当・モデル・ベンダー、入力版:** 入力版は A2 入力版 `bb671da6` の分割前の BS5b の HANDOFF（`docs/handoff/BS5B_OPERATION_ROUTES.md`、内容 sha256 `8ba57df617087d29229b4d059d94b605ad402e500481a83555a5ceffb0f315a6`）と関係ソース・文書の写し。Phase A snapshot に入れたものは無い。(R1) アーキテクチャゲート: Grok 4.7 high（xAI、cursor-agent）、判定「修正後に凍結可」（blocker 1・major 3・minor 7）。Codex（OpenAI）は利用上限で実行できなかった。(R2) A0 だけからの代替構成: Claude Opus（Anthropic、subagent。§1 の写しと関係ソースだけを読む）。外部レビュー（2026-11-02、発注者が受け取った。A2 の改訂版を対象）。分割後の外部レビュー（2026-11-02、分割後の 2 つの HANDOFF を対象。分割で生じた境界と差分の独立確認を兼ねる）。採否は下の「A2 の指摘と採否」。
- **A3 統合担当・モデル・採否:** Claude Opus 5.5（Anthropic）と発注者、2026-11-02。採否は下の表のとおり（#33 の分割と #32 の結果 file は発注者の判断）。発注者が凍結を承認した。
- **C' 用に予約した担当・モデル・ベンダー:** 当初は OpenAI（Codex、A2 では未関与）を予約した。利用上限で 2026-11-04 22:42 まで使えないため、C' の担当は Grok（xAI、cursor-agent）とする（2026-11-02、進行役の選択）。Grok は A2 の R1 に関与しているので**独立性制約あり**。
- **独立性の強化条件を満たせない場合の理由:** R2 は主担当と同じ系列（Anthropic）。A0 だけを読む制約で初稿へのアンカリングを断った。C' の Grok は A2 の R1 と同じ系列で、未関与の担当を確保できない（上の予約の変更）。

### A2 の指摘と採否（A3 統合の入力）

番号は分割前の BS5b の通し番号。本スライスに関わるものだけを置き、残りは [BS5b-2](BS5B2_PURPOSE_PLAY_ROUTES.md) の同じ節にある。#1〜#21 は A2 の R1・R2、#22〜#33 は外部レビュー（`develop` の `04445d52` で照合）、#34〜#41 は A3 の自己点検、#42 以降は分割後の外部レビュー（S-）と、その後の A3 の自己点検。

| # | 指摘（出所） | 採否 | 理由と反映先 |
|---|---|---|---|
| 6 | 動作確認に AC14（batch）が入っており、Phase B の禁止に反する（R1 major） | 採用 | AC14 は Phase C だけ。§5 動作確認は対象なし |
| 7 | identity は 51 文字ではなく 52 文字（R1 minor） | 採用 | §1.3・AC12 |
| 10 | menu の `Build` が `Incremental` であることを決める AC が無い（R1 minor） | 採用 | AC10 |
| 18 | publish・install の前に path 長を判定して I/O 前に止める（R2） | 不採用 | 現在の問いは staging を final 以下にすること（E6・AC12）。install 先の path は対象外（§2.0） |
| 21 | BS4 Player build をルート 97 文字以上で 1 回（R2） | 不採用 | staging の path は publisher の共通コードで、AC12 の計算と HC1（可能なら 97 文字以上のルート）で示す |
| 27 | batch の publish の後の結果 file の失敗と、前回の結果の誤読（外部 R6） | 採用 | 開始時に前回を消し、一時 file から確定。失敗時はその実行の published directory だけを消す。I5・AC13・D8 |
| 28 | `Full` 必須の publish で既存の Integration テストが衝突する（外部 R7） | 採用 | 1 回目 `Full`・2 回目 `Incremental`、publish は 1 回目。AC11・§4.1 |
| 29 | staging の GUID を 8 桁に切り詰めない（外部 R8） | 採用 | `.staging/<guid32>`。AC12・D7・E6 |
| 33 | Phase B の中の段階と確認地点（外部 §3） | 採用（2 スライスへの分割に置き換え） | 発注者の判断で BS5b を BS5b-1 と BS5b-2 に分けた。本スライスの中の確認地点は §4.1 |
| 40 | 分割で、batch の結果 file の欄が BS5b-2 との境界になる（A3 の自己点検） | 採用 | 欄を D8 で固定し、HC1 (i) と F1 で結果 file と manifest の一致を見る |
| 42 | `Full` の削除の境界を字句上の包含だけで済ませない。junction・symbolic link の先を消さない（分割後の外部 S-P1-A） | 採用 | 各 segment と中の directory の reparse point を見て、あれば何も消さずに失敗。I4・AC10・D6 |
| 43 | 結果 file の契約を最小の境界テストで固定する（分割後の外部 S-P1-C） | 採用（BS5b-1 の側） | 欄を D8 で固定、HC1 (i) に宣言名の一致、F5 に identity の不一致。読み手の側の境界テストは BS5b-2 の A3（BS5b-2 #48） |
| 44 | A2 は分割前の入力。分割で生じた境界と差分だけを独立に確認する（分割後の外部 S-P2-F） | 採用 | 分割後の外部レビューがその確認を兼ねる（§5 の A2 記録）。A2 は全面的にはやり直さない |
| 45 | Unity が batch の method を呼ぶ前に終わると、前回の結果 file が残り今回の結果と読めてしまう（A3 の自己点検） | 採用 | 成否の正は process の終了コード、結果 file は終了コード 0 の時だけ読む。I5・D8 |
| 46 | `Full` の削除が他 process の lock で失敗した時の扱いが無い（A3 の自己点検） | 採用 | Unity の build の前の失敗として記録し build を始めない。batch の終了コード 4。D6 |

## 6. Phase B 実装結果

- **実装（`3ceb19ab`）:**
  - Framework（`OneStarMaker.Build.Content`）: `BuildContentMode`（`Incremental` / `Full`）を足し、`BuildContentRequest` と `BuildContentResult` に `Mode`（既存の constructor は `Incremental`）。coordinator は `Full` の時、adapter を呼ぶ前に `ClearWorkspace` で workspace の中身を消す。approved root（またはテスト root）から workspace までの各 segment と workspace の中の全 directory を先に調べ、reparse point があれば何も消さずに `IOException`。これは既存の catch で `BuildFailure` になり、adapter は呼ばれない。検査を作成より先に行い、途中の segment がリンクなら `CreateDirectory` でリンク先に書かない。`BuildContentReport.WriteOutcome` が `buildMode` を書く（`ContentReportFile` の追加項目。preflight は同じ形なので空の欄が出る）。`ContentTransportPublisher.Publish` は outcome の file を読み、無い・identity 不一致・`buildMode` が `Full` でない（欄の無い旧 outcome を含む）なら、publish 先と staging を作る前に `InvalidOperationException`。staging は `StagingPathFor`（`<publishRoot の親>/.staging/<guid32>`）。
  - SampleGame（`SampleGame.DependOnAll.Editor`）: `SampleGameContentBuild.Build(declaration)`（5 menu）は `MenuBuildMode`（`Incremental`）。`BuildResult(declaration, mode, compose)` は従来どおり失敗を例外にし、新しい `BuildUnchecked` は coordinator の失敗を result のまま返す（宣言による選択の失敗は `PurposeSelectionException : InvalidOperationException`）。BS4 coordinator は `Full`。新規 `SampleGameContentBatch.BuildAndPublish`（§2.3 AC13 の引数・終了コード・結果 file。段は `Stages` で注入でき、既定は `BuildUnchecked(Full)`・`ContentTransportPublisher.Publish`・一時 file から move）。
  - テスト: 新規 `BuildContentFullModeTests`（AC10・AC11・AC12。偽 adapter、`TestResults/bs3-fixture` の下のテストごとの root、junction は `cmd /c mklink /J`）、新規 `SampleGameContentBatchTests`（AC13。偽の段）。`BuildContentDirectoryIntegrationTests` は AC11 のとおり 1 回目 `Full`・2 回目 `Incremental`、publish は 1 回目。
- **HANDOFF との差（全て B 適応。凍結条件の意味は変えていない）:**
  - `BuildContentResult` にも `Mode` を足した（outcome に書くため。§3 の表は request の欄だけを書いていた）。publisher は result の欄ではなく outcome の file を正とする。
  - 終了コード 3 と 4 を分けるため、`SampleGameContentBuild` に `BuildUnchecked` と `PurposeSelectionException` を足した（§3 の表に無い）。分類: 引数の誤り 2、選択の失敗（resolver が plan を作れない、または coordinator の preflight の issue）3、`BuildFailure`（Unity の build と、その前の workspace の削除）4、publish の例外 5、それ以外（materialize・bootstrap の失敗、結果の確定の失敗を含む）1。
  - AC12 のテストのため、publisher の staging の path を `internal static StagingPathFor` に切り出した。
  - `BuildContentDirectoryIntegrationTests` は publish に加え、`integration-copy-<identity>` の複写と install も 1 回目（`Full`）の result で行うようにした。copy の名前の identity を install した revision と照合する後段があり、publish した revision と揃える必要があるため。2 回目の result は workspace の再利用と report の確認にだけ使う。
  - `PlayerBuildIntegrationPolicyTests` は `ContentTransportPublisher.Publish` を呼んでいなかった（呼ぶのは `PlayerBuildPublisher`）。合成 result の helper を `Full` にしただけで、判定は変えていない。
  - AC10 の「引数なしの `Build` は `Incremental`」は、`Build(declaration)` が使う `MenuBuildMode` を単体で確かめる（実 SceneResourceMap の materialize を伴う menu の経路は偽 adapter に差し替えられないため）。
  - 失敗の結果 file は成功と別の形（`exitCode`・`reason` だけ）で書く。成功の欄（identity・publishPath 等）を持たない。
  - 規模（追加行）: 製品コードは見積もり約 200 行に対し約 340 行（Framework 約 110、SampleGame 約 230。batch が 196 行で、`Stages` と doc comment・削除の検査の理由のコメントが見積もりより多い）。テストは約 510 行（見積もり 300〜400）。変更理由は見積もりどおり 2 つ（build mode と publish、batch）。
- **未実行:** Unity テスト（新規 2 class を含む全て）、content build、batch（AC14・HC1）。Phase C で行う。junction の負例は Unity テストの中でだけ確かめる。
- **確認済み:** Unity Editor（6000.6.0f1）で compile（`OneStarMaker.Build.Content`・`OneStarMaker.Tests.Editor`・`SampleGame.DependOnAll.Editor`・`SampleGame.Tests.Editor` を含め error 0、変更した file に新しい warning なし、`EditorUtility.scriptCompilationFailed=False`）。`pwsh tools/contract-audit.ps1` は errors=0 warnings=0。Editor は B が起動し、正常終了させた。
- **作業中の観測（範囲外）:** Editor の初回 import が `unity/Assets/MobileDependencyResolver/` の `.pdb.meta` を 5 件消した（この worktree に `.pdb` が無いため）。commit せず `git checkout` で戻した。2026-11-01 に起動されたこの project の EditMode batch テストの Unity process が 2 つ（作業集合 10 MB 前後、lockfile 無し）残っていた。B のものではないので触っていない。
- **implementation head commit:** `3ceb19ab`
- **Phase B 担当・モデル・ベンダー:** Claude Opus 5.5（Anthropic）、2026-11-02

### 動作確認

対象なし（§5。Editor 操作の条件が無く、batch と content build は Phase B で実行しない）。

### 6.1 差し戻し 1 巡目（2026-11-02）

- **入力:** §7.1 の発見 C（F1、m1〜m3）。引き継いだ commit は `2fc38fb2`。
- **修正（`36b966c6`）:**
  - **F1:** `SampleGameContentBatch.Stages` に失敗の log の段 `LogError`（既定は `Debug.LogError`）を足し、`Run` の失敗経路の log（終了コードと理由、published directory の削除と失敗の結果 file の書き込みの例外）をその段へ出すようにした。テストの偽の段は log を記録し、各失敗経路で `exitCode=<code>` の log を、結果の確定が常に失敗する経路では commit の例外の log も確かめる。成功経路では error log が無いことを確かめる。テストで `LogAssert.Expect` を使わなかった理由: `SampleGame.Tests.Editor` の asmdef は `UnityEngine.TestRunner` を参照しておらず、asmdef 参照の追加は §1.2 で禁止されている。`BuildAndPublish` の最外の `Debug.LogException`（`Run` の外の想定外の例外）はテストが通らない経路なので変えていない。終了コード・結果 file・所有の規則は変えていない（B 適応）。
  - **m3（テスト追加）:** issue が空で成果物が無い result が終了コード 4 になることを `FailedBuildResultWithoutIssues_ReturnsFour` で固定した。
  - **m1（文書化、Phase D の harvest へ）:** approved root（`artifacts/bs2b`）自身が junction 等の reparse point だと、`Full` の build（batch・BS4 coordinator）は常に失敗する。D6 の「root から各 segment を見る」どおりの挙動で、凍結条件は変えない。運用上の制約として、harvest 先の Architecture §18 §4（build mode の記述）に書く。
  - **m2（見送り）:** 削除の途中の lock の失敗で workspace が途中まで消えて残ることは、コード内コメントに書いてあり（workspace は再利用の cache で、次の `Full` が空にし直す）、追加の対応はしない。
- **確認済み:** Unity Editor（6000.6.0f1）で compile（`EditorUtility.scriptCompilationFailed=False`、変更した 2 file に warning なし、新しい段とテストが読み込まれていることを reflection で確認）。`pwsh tools/contract-audit.ps1` は errors=0 warnings=0。Editor は B が起動し、正常終了させた。
- **未実行:** Unity テスト（F1 の修正の確認を含む）、content build、batch（AC14・HC1）。次の発見 C と判定 C で行う。
- **作業中の観測（範囲外）:** Editor の初回 import が `unity/Assets/MobileDependencyResolver/` の `.pdb.meta` 5 件を再び消した。`git checkout` で戻し、commit していない。2026-11-01 起動の EditMode batch テストの Unity process 2 つ（§6 の観測と同じ PID 39552・31952）がまだ残っている。B のものではないので触っていない。
- **担当・モデル・ベンダー:** Claude Opus 5.5（Anthropic）、2026-11-02

## 7. Phase C

### 7.1 発見 C（2026-11-02）

- **種別:** 発見 C（構造レビューと、新規テストの限定実行）。判定 C ではない。
- **使った evidence の取得案内と状態:** Phase A snapshot は使っていない（§0 のとおり発見 C では読まない）。evidence bundle は作っていない（未生成）。入力は implementation base `26f34046`..head `3ceb19ab` の差分と、この票を含む commit `b6e42182`。
- **構造適合:** 適合。§2 の I4〜I7 と E3・E4・E6、§3 の責務マップ、§6 の「HANDOFF との差」に照らして凍結条件・常時契約の違反は無い。`ClearWorkspace` は adapter と `CreateDirectory` の前に reparse point を検査し、`Full` の publish 拒否は outcome の `buildMode` を正として publish 先・staging の作成前に行う。staging は `.staging/<guid32>`。batch の終了コード分類と所有（結果 file・published directory）は D8 のとおり。asmdef 参照の変更なし、Framework に語彙の追加なし。
- **差し戻し指摘（B へ、1 件）:**
  - **F1: `SampleGameContentBatchTests` の失敗経路 10 件が失敗する。** 違反根拠: AC13（終了コード 2・3・4・5・1 の分岐と、publish 後の確定失敗の負例）が検証できていない。原因は `SampleGameContentBatch.Run` の `Fail` が `Debug.LogError` を出す（`SampleGameContentBatch.cs:73`）のに、テストが `LogAssert.Expect` していないこと（Unity Test Framework が Unhandled log message で失敗にする）。失敗したテスト: `Missing`・`NoValue`・`ValueIsOption`・`Unknown`、`SelectionException_ReturnsThree`、`FailedBuildResult_MapsIssueToExitCode`（`MissingClosure,3` と `BuildFailure,4`）、`PublishFailure_ReturnsFiveAndLeavesNoPublishedDirectory`、`CommitFailureAfterPublish_ReturnsOneAndRemovesOnlyThisRunsPublishedDirectory`、`CommitAlwaysFailing_StillReturnsOneAndLeavesNoPreviousResult`。製品の規則の誤りではなくテストの期待の欠落。直し方（テストで `LogAssert.Expect` するか、log の出し方を整理するか）は B の判断。
- **後続へ回す軽微な所見（差し戻し条件ではない）:**
  - m1: `ClearWorkspace` は approved root（またはテスト root）自身が reparse point でも拒否する。`artifacts/bs2b` を別 drive への junction にした環境では `Full`（batch・BS4）が常に失敗する。D6 の「root から各 segment」には沿う。運用上の制約として文書に残す価値がある（この worktree の `artifacts` は junction ではない）。
  - m2: 削除の段階で lock により失敗すると workspace が途中まで消えた状態で残る（コード内コメントに明記済み。workspace は再利用の cache）。
  - m3: issue が空で `IsSuccess=false` の場合は終了コード 4 になるが、テストで固定されていない。
- **実行したコマンドと結果:**
  - `pwsh tools/run-tests.ps1 -Filter "BuildContentFullModeTests;SampleGameContentBatchTests"`: total 22、passed 12、failed 10（上の F1）。`BuildContentFullModeTests`（AC10〜AC12、junction の負例を含む）は全て Passed。結果 XML は `TestResults/3f251e75-a254-44a7-bfc7-6bafbe3a016e/results.xml`（git 管理外）。process の終了コード 2。
  - Editor の import が `unity/Assets/MobileDependencyResolver/` の `.pdb.meta` 5 件を再び消した（Phase B と同じ現象で範囲外）。`git checkout` で戻し、commit していない。
- **未実行:** 全 EditMode 回帰、`BuildContentDirectoryIntegrationTests`（content build を伴う）、AC14、HC1、`pwsh tools/contract-audit.ps1`（B が errors=0 を確認済みで、発見 C では再実行していない）。F1 の修正後の判定 C で行う。
- **担当・モデル・ベンダー:** Claude Sonnet 5.5（Anthropic）、2026-11-02。Phase B（Claude Opus 5.5）とは別のモデル。

### 7.2 発見 C の再レビュー（1 巡目の後、2026-11-02）

- **種別:** 発見 C（差し戻し 1 巡目の再レビュー）。判定 C ではない。
- **使った evidence の取得案内と状態:** Phase A snapshot は使っていない。evidence bundle は未生成。入力は implementation head `3ceb19ab`..`36b966c6` の差分（`SampleGameContentBatch.cs` とそのテストだけ。Framework 側は無変更）と、引き継いだ commit `02966399`。
- **構造適合:** 適合。差分は `Stages` に失敗 log の段 `LogError`（既定は `Debug.LogError`）を足し、`Run` の失敗経路の log をその段へ通しただけで、終了コードの分類・所有・結果 file の欄は変わらない。asmdef 参照の変更なし、Framework に語彙の追加なし。
- **F1 の確認:** 解消。失敗経路のテストは log を記録して `exitCode=<code>` を確かめ、結果確定が常に失敗する経路では commit の例外の log も確かめる。成功経路は log が空であることを確かめる。
- **m1〜m3 の扱い:** 妥当。m3 は `FailedBuildResultWithoutIssues_ReturnsFour` で固定。m1 は Phase D の harvest（Architecture §18 §4）へ、m2 は見送り（コード内コメントで明記済み）。
- **差し戻し指摘:** なし。
- **実行したコマンドと結果:** `pwsh tools/run-tests.ps1 -Filter "BuildContentFullModeTests;SampleGameContentBatchTests"`: total 23、passed 23、failed 0、exitCode 0。結果は `TestResults/62c951d6-6b14-4854-b6d4-807e3c63505d/`（git 管理外）。Editor の import が `unity/Assets/MobileDependencyResolver/` の `.pdb.meta` 5 件を再び消したので `git checkout` で戻し、commit していない。
- **未実行:** 全 EditMode 回帰、`BuildContentDirectoryIntegrationTests`、AC14、HC1、`pwsh tools/contract-audit.ps1`（B が errors=0 を確認済みで、ここでは再実行していない）。判定 C の担当。
- **担当・モデル・ベンダー:** Claude Sonnet 5.5（Anthropic）、2026-11-02。

### 7.3 判定 C（2026-11-02。高コスト証拠 HC1 を除く）

- **対象:** implementation base `26f34046`、head（GO 候補）`36b966c6`。引き継いだ commit `d4640ac5`（HEAD と一致、dirty は判定から外した `docs/reviews/` のみ）。
- **使った evidence:** Phase A snapshot（取得案内は起動指示で受け、fetch して F1〜F5 を閲覧）。判定 evidence bundle はローカル固定済み（未昇格。§0）。
- **判定:** 阻害する指摘なし。I4〜I7・E3・E4・E6 に対し AC10〜AC13・AC18 と AC14 の証拠が揃った。HC1 は未取得（C' の後。§5・DR1）。
- **機械検査:** `pwsh tools/contract-audit.ps1` errors=0 warnings=0。
- **全 EditMode 回帰（空 filter、`-nographics`）:** total 1587、passed 1562、failed 0、inconclusive 25。ハーネスの status は failed（Inconclusive があるため）。25 件は全て `SampleGame.Tests.Rendering.Vfx` の描画系で、理由は `GraphicsDeviceType.Null`（`-nographics`）。`BuildContentFullModeTests`・`SampleGameContentBatchTests`・`BuildContentDirectoryIntegrationTests`・`PlayerBuildIntegrationPolicyTests` に該当する 37 件は全て Passed。
- **Inconclusive の解消:** `pwsh tools/run-tests.ps1 -WithGraphics -Filter "SampleGame.Tests.Rendering.Vfx"` で total 73、passed 73、failed 0、inconclusive 0。全 EditMode と合わせて必要な集合が全て Passed（このスライスは Rendering を変更していない）。
- **AC14:** `-osmPurpose no-such-purpose` の batch 起動で process の終了コード 2、`last-result.json` の `exitCode` は 2（reason は未知の宣言名）、`artifacts/bs2b/transport/` は起動の前後とも存在せず増えていない。
- **照合 script（offline、判定 C が作成）:** F1〜F5 を凍結した期待出力と照合し全件一致（F1 合格、F2 は (iii)、F3 は (iv)、F4・F5 は (i) で不合格）。原記録（HC1）への適用は未実施。script は判定 evidence bundle にあり git に入れていない。
- **範囲外の観測:** Unity の import が `unity/Assets/MobileDependencyResolver/` の `.pdb.meta` 5 件を消すので、各実行後に `git checkout` で戻し commit していない。2026-11-01 起動の Unity process 2 つ（PID 39552・31952）は残っており、触っていない。
- **担当・モデル・ベンダー:** Claude Sonnet 5.5（Anthropic）、2026-11-02。Phase B（Claude Opus 5.5）とは別のモデル。

### 7.4 判定 C の照合 script の修正（2026-11-02。C' 1 巡目の CP1・CP2 への対応）

- **対象:** `check-hc1.ps1`（判定 evidence bundle と C' 用 bundle にあり git に入れていない）。implementation head `36b966c6` と製品コード・テストは変えていない。
- **CP1:** (v) は `preflight.json` の `selected` の各要素（`StableKey|LogicalKey|PhysicalKey`）の先頭の欄（stable key）の集合を `resolve-selected.json` と比べる。`outcome.json` の `selected` は比べない（製品が空のまま書く。`BuildContentReport.cs`）。空の `selected` は不合格にする。
- **CP2:** (vi) は `longest-paths.json` を見ない。published directory の実 file の相対 path から、final（`<publishPath>/<rel>`）、staging（`<publishPath の親>/.staging/<guid32>/<rel>`）、実 path の長さを計算し、いずれかが 259 を超える、または staging が final より長ければ不合格にする。`publishPath` は `last-result.json` の欄。
- **確認:** 凍結した F1〜F5 を再実行し期待出力と全件一致（F1 合格、F2 は (iii)、F3 は (iv)、F4・F5 は (i)）。実形の `selected` と空の `outcome.selected` の F1 変形は合格、実際に 259 を超える file を足し一覧を古いままにした変形は (vi)、stable key 集合を誤った変形は (v) で不合格。変形は手元で作った記録で、生出力を C' 用 bundle に入れた。
- **担当・モデル・ベンダー:** Claude Sonnet 5.5（Anthropic）、2026-11-02。

### 決定記録

- **DR1（2026-11-02、発注者。進行役経由で受領）:** 人間（発注者）の判断で、HC1 の前に行う C'（いわゆる暫定 C'）を GO 判定に使う。
  - 適用時点: 判定 C 完了後（GO 候補 head `36b966c6`）。
  - 影響: §5 の順序「判定 C → 暫定 C' → HC1 → 最終判定 C と最終 C'」のうち、最終 C' を行わない。HC1 の取得と、HC1 を加えた最終判定 C は従来どおり行う。C' の後に head が変われば、「判定後の head 変更」に従って C' をやり直す。
  - 凍結した AC・不変条件・成立条件と判定定義（HC1 を含む）の意味・合否は変えない。
  - C' 担当には渡さない（C' 自身の指摘への裁定ではないため）。
- **DR2（2026-11-02、発注者。進行役経由で受領。C' 2 巡目の CP3 への裁定）:** §5 HC1 (v) の右辺「同じ head の Editor で `SampleGameContentBuild.Resolve(SpringFull)` が返す集合」を、「その集合に、`SampleGameContentBuild.BuildUnchecked` の `ComposeBootstrap` が足す bootstrap の 2 件（`samplegame:bootstrap:uicommon/Full`、`samplegame:bootstrap:scene-resource-map/Full`）を加えた集合」と読む。
  - 理由: `Resolve` は doc comment で「bootstrap の合成は含まない」と明記しており、bootstrap の合成は本スライスより前からある既存の経路で、本スライスの差分は変えていない。字義のままでは、正しい成功経路でも (v) が必ず不合格になる。(v) の目的（preflight の選択が宣言の選択と一致する）は、この読みで保たれる。
  - 位置づけ: 字義の合否を変える読みなので、規則上は Phase A の新しい revision にあたる。発注者の判断で、DR1 と同じく例外として決定記録で扱う。ほかの AC・不変条件・成立条件・判定定義は変えない。
  - 適用時点: C' 2 巡目の後（GO 候補 head `36b966c6`）。照合 script の (v) をこの読みに合わせて直し、凍結した F1〜F5 の期待出力との一致を確かめる。
  - C' 担当に渡す（C' 自身の指摘 CP3 への裁定。C' 用決定記録の版は DR2）。

## 8. Phase C'

### C'（1 巡目。DR1 により GO 判定に使う。HC1 の原記録は含まない）

- 種別: §7 の DR1 により GO 判定に使う C'（HC1 の前）。1 巡目は全体を初見で監査した。
- 入力: C' blind audit bundle（head `36b966c6` に固定。凍結した Phase A の §1〜§5、所見を除いた Phase B の結果、base..head の完全 diff（`docs/handoff/` を除く）、contract-audit と docs-audit、全 EditMode と VFX（graphics あり）の XML・step・log、AC14 の生記録、`check-hc1.ps1` と F1〜F5 の出力、head の source tree（`docs/handoff/` と git 履歴を除く））。状態はローカル固定済み（未昇格）。進行役が別 process で fetch して packageSha256 の一致を確かめ、発見 C・判定 C の所見と DR1 が含まれないことを確認した。指示書と報告の原本は worktree の外にある（担当者の `OneStarMaker/cprime/` の下）。発見 C・判定 C の所見と人間の裁定は渡していない。
- 担当・モデル: Grok 4.7（xAI、cursor-agent、`grok-4.7-high`）。2026-11-02。実行の進行役は Claude Opus 5.5。**独立性制約あり**: 予約していた OpenAI は利用上限で 2026-11-04 22:42 まで使えず、xAI は A2（R1）に関与している。B（Claude Opus 5.5）と C（Claude Sonnet 5.5）とは別のモデルなので、最低条件は満たす。
- **阻害する指摘（2 件。どちらも判定 C の照合 script `check-hc1.ps1` の欠陥で、implementation head の製品コードとテストには無い）:**
  - **CP1（§5 HC1 (v)）:** script は `outcome.json` と `preflight.json` の両方の `selected` を、`resolve-selected.json` と生の文字列で比べる。製品の `WriteOutcome` は `outcome.json` の `selected` を空のまま書き、`preflight.json` の要素は `StableKey|LogicalKey|PhysicalKey` である。このため、規則 (v)（`preflight.json` の selected の stable key 集合が `Resolve(SpringFull)` と一致する）を満たす正規の記録が不合格になる。進行役が script の (v) と `BuildContentReport.cs:108,142` を読んで確かめた。
  - **CP2（§5 HC1 (vi)）:** script は `longest-paths.json` に並べた文字列の長さだけを見る。published directory の実 file や staging の path からは導出しないので、実 path が 259 を超えても、短い一覧を渡せば合格になる。進行役が script の (vi) を読んで確かめた。
- **後続スライスの入力（C' の記述を要約）:** AC12 の合成 file 名が観測の分解より 1 文字短い（E6 の成否は変わらない。余裕は 20 文字以上）。AC10 の menu の `Incremental` は `MenuBuildMode` 定数の確認にとどまる。file lock・file の symbolic link・途中 segment だけの junction・結果確定の失敗後の削除失敗の経路にテストが無い。(iv) は事後の file だけを見るので、「置いてから消えた」と「最初から無い」を区別できない。`SampleGameContentBatch.cs` は 202 行（B 適応として記録済み）。
- **残存リスク（C' の記述）:** HC1 は未実施。全 EditMode の step は inconclusive 25（VFX、graphics なし）で status が failed になり、graphics ありの再実行は 73/73。AC14 の transport の一覧は、生の一覧ではなく「directory が無い」という注記である。
- **監査できなかった範囲（C' の記述）:** HC1 の生記録、F1〜F5 の fixture 本体（script は実行していない）、全 EditMode の log 全文。
- 次: CP1・CP2 は判定 C の照合 script の修正で解消する（implementation head は変わらない）。修正後の script と F1〜F5 の出力で、C' の 2 巡目（前巡の指摘の解消を中心に）を行い、その後に HC1 へ進む。

### C'（2 巡目。前巡の指摘の解消と照合 script の改訂を中心に）

- 入力: 2 巡目の C' blind audit bundle（head は 1 巡目と同じ `36b966c6`。1 巡目の内容に、改訂後の `check-hc1.ps1`、1 巡目の script、改訂差分、F1〜F5 と変種 3 件の出力を加えたもの）と、C' 自身の 1 巡目の報告。状態はローカル固定済み（未昇格）。進行役が別 process で fetch して packageSha256 の一致を確かめ、所見と DR1 が含まれないことを確認した。人間の裁定は無いので、C' 用決定記録は渡していない。
- 担当・モデル: 1 巡目と同じ（Grok 4.7、xAI、cursor-agent。独立性制約あり）。2026-11-02。
- 前巡の指摘: CP1・CP2 は解消（C' の判定）。F1〜F5 の出力は凍結した期待と一致する。
- **阻害する指摘（1 件）:**
  - **CP3（§5 HC1 (v)）:** 成功した `spring-full` の build では、`preflight.json` の selected の stable key 集合は、`Resolve(SpringFull)` の集合に bootstrap の 2 件（`samplegame:bootstrap:uicommon/Full`、`samplegame:bootstrap:scene-resource-map/Full`）を加えたものになる。そのため、凍結した (v) の字義（二つの集合が一致する）は、この head の正しい成功経路でも成り立たない。順序は次のとおり: `BuildUnchecked` が `Resolve` の plan を `ComposeBootstrap` で上書きする（`SampleGameContentBuild.cs:77-84`）。その plan で `WritePreflight` が書く。一方、`Resolve` は doc comment で「bootstrap の合成は含まない」と明記している（同 43-46）。進行役は head の該当箇所を読んで確かめた。bootstrap の合成は本スライスより前からある経路で、本スライスの差分は変えていない。欠陥は製品ではなく、凍結した導出規則 (v) の右辺の書き方にある。
- **後続スライスの入力（C' の記述を要約）:** script の (v) の比較は大小文字を区別しない（製品は `Ordinal`）。stable key に `|` が含まれる場合の分解は保証されない（現在のキーには無い）。
- **残存リスク（C' の記述）:** CP3 はソースから導いたもので、実 Editor で `Resolve(SpringFull)` と preflight を並べた記録は無い。(vi) は証拠の複製先の絶対 path も 259 以下を要求するので、複製先が深いと製品が正しくても不合格になりうる。
- 次: CP3 は凍結条件 (v) の読み方の問題なので、発注者の裁定を待つ（§0）。

## 9. Phase D

未到達。
