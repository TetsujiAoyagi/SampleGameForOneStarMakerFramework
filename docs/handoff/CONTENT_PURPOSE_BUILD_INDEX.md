# 目的 build の索引（S2-3）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S2-3。S2 を S2-2 と S2-3 に分けた判断は削除済みの `CONTENT_OVERLAY_BUILD.md` の §2.5・§2.8 U2（`git show 25aa59e^:docs/handoff/CONTENT_OVERLAY_BUILD.md`）にある）
- status: `A`（A3 統合案。A2 の 2 件を §5.1 で分類し、本文へ反映した。owner が 2026-11-16 に分類と §2.8 U8 を推奨どおりと裁定した。Draft PR の外部計画レビューが未了で、未凍結）
- branch: `claude/content-purpose-build-index`（PR の base は `develop`）
- implementation base commit: `2c775ff0`（`origin/develop` の head、2026-11-16。§1 の現況はこの base。起動指示が挙げた `d945927` の後に PR #194 がワークフロー文書だけを変えて進んでいたので、新しい方を採った）
- implementation head commit: `未固定`
- A3 凍結記録（承認者・日付・対象 revision・承認の根拠）: `未凍結`
- risk: `normal`（program の行の値。public な manifest の DTO と新しい public 型を足すが、owner が `normal` のままと決めた。§2.8 U3）
- owner: root（依頼者）
- created: 2026-11-16
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: [18-asset-description.md](../../unity/Assets/Docs/Architecture/18-asset-description.md) §4「配信用 transport の公開境界」（索引の欄と生成時点）、[13-resource-system.md](../../unity/Assets/Docs/Architecture/13-resource-system.md)「DIST transport と disk cache の境界」（索引の検証と選択の規則）、program §5.3・§8・§10 U6
- 関連: [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md)（A0。§5.3「当面の方針」、§6、§8 の S2・S2-3、§10 U6）、[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)（所有境界の正本。§2.1 の分類で、索引の生成は BuildSystem 本体、読みと選択は Distribution に置く）。Phase B は `Runtime/BuildContent/` を触る（§3）ので、`RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` の Phase B と同時に走らせない（program §0 の停止規則。あちらは 2026-11-16 時点で A0 だけ）
- Phase A snapshot の取得案内・状態: 未生成（機械向けの期待値・照合 script は無い。§5）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: Phase A（A3）。
  - 次の作業: Draft PR の外部計画レビューを受け、owner が指摘を採否する。裁定と対象 revision・次担当 B を §0 の凍結記録に書いてから凍結版を commit する。
  - 読む範囲: 既定（Phase A は全節）。
  - 引き継ぐ commit: A3 統合案の commit（SHA は起動指示で渡す）。base は上の `2c775ff0`。
  - 必要な権限: なし（文書のみ）。
  - 停止規則: 文書だけ。同じ session で Phase B に入らない。asmdef の参照の追加は提案に留める（本書の案は追加を要しない。§3）。
  - 引き渡さないもの: なし。

## 1. 目的と対象外

- 目的: 供給元から得られる目的 build（1 つの宣言から作った正規の Content Directory）が複数あるときに、要求した Scene（と asset）を含む目的 build を、その build の Unity の content を登録・走査せずに、BuildSystem が publish で出した索引だけで選べるようにする。選べないときは、候補ごとの理由が分かる。
- 利用者: 供給元（Local・LAN）を実装する人と、Play の入口などで「どの目的 build を取るか」を決める host の code を書く人。索引は publish が自動で出すので、目的 build を作る人と編集する開発者に新しい入力は無い。
- 目的・利用者の文言の経緯: 上の 2 欄は program §8 の S2-3 の行と §10 U6 から本書で初めて書いた。A2 の前に owner（root）が 2026-11-16 に承認した（§2.8 U1）。承認の前に、目的名で目的 build を取る運用では索引が要らないこと、索引が効くのは「決めた目的 build が要求した Scene を含むかの確認」と「目的名を持たない要求からの選択」の 2 つの場面であることを owner と確かめ、「確認」だけに縮める案も示した。owner は両方の場面がありうるとして、文言を変えずに進めると決めた。
- 対象外:
  - 供給元が自分の持つ目的 build の一覧と、それぞれの manifest の期待 digest を得る方法（Local の installed cache、Artifacts の R2、LAN の公開 directory の列挙）。S4。
  - `IContentProvider` の製品実装と `ContentResolver` への接続、manifest から候補の事実（engine・schema）を作る adapter。S3・S4（program §8 の S1 から移送済みの項目）。
  - 同じ目的の複数 revision から 1 つを選ぶ規則（最新・known-good）。S4。revision 文字列の大小を鮮度にしない既存の規則（18 章）は変えない。
  - 目的 build 同士を同時に登録すること（program §5.3 で主にしない）。目的 build より細かい配布単位（Stage II の Scene package）。
  - 上書き（S2-2）の base を選ぶ接続、Play の入口での利用、menu と表示。S3・S5。
  - Artifacts（`tools/Artifacts`）の変更。Player。
- 現況（base `2c775ff0`。実測は `grep`・code の読み・手元の既存の publish 出力の集計で、Unity は動かしていない）:
  - **publish された bytes に Scene の一覧は無い。** `ContentTransportManifest`（`Runtime/BuildContent/Distribution/ContentTransportManifest.cs`、150 行）の一覧は `files`（`content/` の下の不透明な file の path・size・sha256）と `sourceFiles`（checkout の案内用の source の path・sha256）だけで、logical key・root・kind・表現は無い。`declaration`（名前・主表現・runtime 既定値）は任意で、検証されない。
  - **directory が何を含むかは、Unity に登録して初めて読める。** `BuildContentRoot`（entry ごとに logical key・stable key・表現・kind と Unity の locator）は content の `.cf` の中にある。`ContentDirectorySession.RegisterCore` が `ContentLoadManager.RegisterContentDirectory` → `GetRootAssets<BuildContentRoot>` で 1 つの root を得て、`ContentDirectoryIndex.TryCreate`（`Runtime/BuildContent/ContentDirectoryIndex.cs`、130 行）が索引にする。directory の中の JSON は読まない。U6 の「Runtime が成果物を走査して作らない」は、この登録を選択の前に行わないことを指す。
  - **directory の索引の規則。** key は `(logical key, 実効表現, kind)`。空の表現は `Full`（schema v1 の旧 entry）。Scene と Object の両方が、要求した表現が無いときだけ `Full` の entry へ fallback する（84〜101 行）。同じ key の entry が 2 つあれば `EntryAmbiguous` で登録を拒む。
  - **root と logical key が publish の外に出るのは preflight だけ。** `reports/<identity>/preflight.json`（`Editor/Build/Content/BuildContentReport.cs`）の `selected[]` は `stableKey|logicalKey|physicalKey`、`roots[]` は `stableKey|path|representation`、`requirements[]` は `{logicalKey, representation, path[]}`。kind はどこにも無い。preflight は publish されない。
  - **publish の出力は `transport.json` と `content/` の 2 つ。** `ContentTransportPublisher.Publish`（`Editor/Build/Content/ContentTransportPublisher.cs`、153 行）は `Full` の build だけを受け、上書きと絞った build を拒む。`rootSchemaVersion` は 2 固定、`version` は 1。
  - **Local と LAN は同じ bytes を読む。** `IContentArtifactSource` は `OpenReadAsync(relativePath)` だけを持ち、`LocalContentArtifactSource` と `HttpContentArtifactSource` は同じ公開 directory の `transport.json` と `content/<path>` を返す。一覧・存在確認の member は無い。install（`ContentInstaller`）は `transport.json` を最初に上限 4 MiB で読み、`installed/<contentSet>/<revision>/` に `transport.json` をそのまま残す。
  - **manifest の検証は欄の有無を見分けられる。** `ContentManifestValidation.ValidateRequiredFields` は raw JSON を `JsonShapeReader` で読んで必須欄を検査し、未知の欄は無視する（重複 key、深さ 64 超、末尾の余りは拒む）。`JsonUtility` の既定値だけでは、欄が無いことと空の配列を区別できない。
  - **Artifacts は `transport.json` と `content/*` だけを運ぶ。** `content collect` は直下に `transport.json` と `content/` を持つ directory を入力にし、R2 の key は `transport.json` か `content/<path>`（`ContentContract.psm1`）。manifest の読みは `ConvertFrom-Json -Depth 32` で `contentSet`・`revision`・`target`・`files` だけを見て、未知の欄で止まらない（code を読んだだけで、実行していない）。
  - **供給元に目的 build の一覧は無い。** Distribution に catalog・channel・「最新」の pointer は無い。installed cache の `control/known-good.json` は contentSet ごとに 1 件の pin。Artifacts の「最新」の解決は後続の作業（Artifacts README）。SampleGame の `PlayEntryCandidates.Select` は installed cache を歩いて、contentSet が宣言の名前と等しい最新の install を選ぶ（contentSet = 宣言の名前は SampleGame の慣習で、Framework は強制しない）。
  - **製品の `IContentProvider` は無い。** `ContentResolver` は provider を policy の順に当て、採用した時点で止まる。1 つの provider は 1 要求に候補を 1 つ返す。
  - **表現の無い root がある。** projection は Representation の tag が無い root を空の表現で通す（`Editor/Build/Content/BuildContentProjection.cs` 148〜152 行）。同じ logical key・kind で、tag の無い root と `Full` の tag の root が両方あると、build は成功し、登録だけが `EntryAmbiguous` になる（A2 のアーキテクチャゲートの指摘 7）。
  - **bootstrap の entry は plan に入ってから build される。** SampleGame の `ComposeBootstrap` は build の要求より前に plan と snapshot へ UICommon と SceneResourceMap を足すので、projection の root と `BuildContentRoot` の entry は一致する。
  - **実データの大きさ。** 手元の既存の publish 出力（git 管理外）で、`spring-full` は root 127 件・`transport.json` 約 348 KB（`files` 253 件、`sourceFiles` 1,216 件）、`spring-planner-whitebox` は root 70 件・約 307 KB。entry 1 件を 100 bytes 前後と見ると、索引は 13 KB 程度で、上限 4 MiB に対して小さい。
- UC ごとの利用者の入力:

  **UC-1 目的 build を publish する**（呼び手は host の Editor code か batch。既存の操作）

  | 入力 | 誰がいつ決めるか | 目的からの正当化 |
  |---|---|---|
  | （新しい入力なし） | — | 索引は build が root に書いた entry から publish が自動で作る。目的の「BuildSystem が publish で出した索引」を、利用者の作業を増やさずに満たす |

  機械が決めるもの: 索引の entry（logical key・kind・実効表現）、索引の版。

  **UC-2 要求に合う目的 build を選ぶ**（呼び手は供給元の code か host の code。開発者から見れば Play の前か要求の時）

  | 入力 | 誰がいつ決めるか | 目的からの正当化 |
  |---|---|---|
  | 要求（logical key・kind・任意の表現） | 呼び手が実行時に渡す（既存の `ContentRequest` と同じ欄） | 目的の「要求した Scene」そのもの |
  | 候補の目的 build の列（各 build の `transport.json` の bytes と、別に得た期待 digest） | 供給元が実行時に用意する（得方は S4） | 選ぶ対象。digest は未検証の bytes を使わないため（program §6 I2） |
  | 候補の順 | host が開発時に宣言する policy（例: 今の RunPurpose を先に）。§2.8 U4 | 複数の build が含むときに、選ぶ build を暗黙に決めない（program §6 I5） |

  機械が決めるもの: 各候補の判定（含む・含まない・索引が無い・検証に落ちた）と理由、選んだ build。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: **要求した Scene を含む目的 build を、BuildSystem が正規 directory と一緒に出す索引で選べるか。**（program §8 の S2-3 の行と同じ文。U6 の制約: Runtime が成果物を走査して作らない、Editor にしか無い sidecar にしない、Local と LAN が同じ bytes を見る）
- 誤って YES を出す道（各 AC はこれを塞ぐ形にする）:
  - (a) 索引を plan・preflight の `requirements` から作る。build の root と違う集合（合成の entry、表現の違い）を「含む」と答えうる。→ 索引の entry と、同じ directory を登録した `BuildContentRoot` の entry を実機で突き合わせる（AC6）。
  - (b) 索引の照合と directory の照合の規則がずれる（表現の `Full` fallback、空の表現）。→ 規則を 1 箇所で持ち、実機で全 entry と miss と fallback の答えが一致することを見る（AC2・AC6）。
  - (c) 索引の無い旧 manifest を「何も含まない」か「全部含む」と読む。どちらでも、fixture が新しい manifest だけなら通ってしまう。→ 旧 manifest を候補に混ぜた単体（AC3）。
  - (d) 選択の test が content の file が揃った directory で動き、実は directory を登録して答えている。→ `transport.json` の bytes だけを渡し、`content/` の無い状態で選ぶ（AC4）。
  - (e) 候補が 1 つの fixture。順の規則も、含まない build を飛ばすことも試されない。→ 共通の Scene と固有の Scene を持つ目的 2 つ以上（AC4）。
  - (f) Local だけで確かめ、LAN の bytes が同じであることは主張だけ。→ 同じ公開 directory から Local と HTTP で install した `transport.json` の bytes と索引が同じ（AC7）。

### 2.1 進める最低条件

**不変条件（起きてはいけないこと）**

- **I-1 索引は directory と食い違わない。** publish した目的 build の索引が要求 `(logical key, kind, 表現)` を「含む」と答えることと、その `content/` を登録した `ContentDirectoryIndex` がその要求を解決できることは、全ての要求で一致する（表現の `Full` fallback を含む）。
  - 主張を崩しうる経路: (1) 索引の出所。preflight の `selected` / `roots` / `requirements` は kind を持たず、`roots` は logical key を持たない。plan から作ると adapter が root に書く entry と別の計算になる。(2) 表現の実効値。adapter が空の表現を書いた entry（schema v1 の名残）、`ContentDirectoryIndex.EffectiveRepresentation` の `Full` への読み替え。(3) fallback。Scene と Object の両方が `Full` へ fallback する（Object も fallback することはコメントにだけ理由がある）。(4) `Incremental` の workspace の残り（前回の選択の file）。publish は `Full` だけを受けるので塞がっている。(5) 上書き・絞った build。publish が拒むので索引を持つ manifest にならない。(6) 同じ key の entry の重複。directory は `EntryAmbiguous` で登録を拒むが、索引が重複を許すと「含む」と答えうる。
  - 塞ぎ方: projection の root から `(logical key, kind, 表現)` を出す純粋な関数を `Editor/Build/Content` に 1 つ置き、adapter（`BuildContentRoot` の entry を作る所）と coordinator（outcome に一覧を書く所）の両方がそれを使う。`IContentDirectoryAdapter` と `ContentDirectoryBuild` の形は変えない（既存の fake が変更なしで通る）。索引の表現は root の entry と同じ生の値で書き、実効表現への読み替えと fallback は Runtime の読み口だけが行う。その規則は `ContentDirectoryIndex` と共有する 1 つの internal な関数に置く（Editor の publisher は Runtime の internal を呼べない。Runtime の friend に `OneStarMaker.Build.Content` は無い）。publisher は書いた manifest の bytes を public の読み口で読み直し、重複・空白だけの表現などの形の失敗があれば publish を拒む（規則を Editor に 2 つ目として書かない）。
  - 探索した境界: `Editor/Build/Content` の coordinator・adapter・projection・report・publisher、`Runtime/BuildContent` の root・index・session、SampleGame の `ComposeBootstrap`。§1 の「未確認」だった、表現が空の entry の経路は今もある（tag の無い root）。
- **I-2 検証していない索引を選択に使わない。** 選択は、期待 digest と一致した `transport.json` の bytes から読んだ索引だけを使う。索引の欄が無い manifest は「索引なし」で、「含まない」「含む」のどちらにも読まない。形の壊れた索引は候補の検証の失敗で、別の解釈に落とさない。
  - 経路: `JsonUtility` の既定値（欄が無いと空の配列になる）、digest を照合せずに bytes を読む呼び手、`ContentManifestValidation.Validate` を通らない読み口（`declaration` が検証されない前例）、kind の文字列の未知の値。
  - 塞ぎ方: 読み口は bytes と期待 digest の両方を受け、上限 4 MiB と sha256 を照合してから raw の形を検査する。`JsonUtility` は入れ子の class の欄を省けないので、publisher は索引を出さない時も `"index":{"version":0,"entries":[]}` を書く。そこで「索引なし」は「`index` の欄が無い、または `version` が 0」と定義する（§2.8 U8）。読み口が知らない版（2 以上）は「未対応の索引」として「索引なし」と同じく理由を残して飛ばす（§2.8 U8）。
- **I-3 選択は content を登録・走査しない。** 選択の入力は `transport.json` の bytes だけで、`content/` の file を開かず、Unity の Content Directory を登録しない。索引は publish の bytes の中にあり、Editor にだけある file ではない。
  - 経路: `BuildContentRoot` を読むために `ContentDirectorySession` を作る実装、`Library/BuildHistory` の `ContentLayout.json` や preflight（Editor の手元にだけある）を読む実装、installed cache の `content/` を歩く実装。
- **I-4 選ぶ build を暗黙に決めない。** 複数の候補が含むときは、呼び手が渡した順で最初の 1 つを選ぶ。revision・contentSet の文字列の大小や候補の列挙順で決めない。含まない候補・索引の無い候補・未対応の版の索引の候補は、理由を残して飛ばす。検証に落ちた候補（digest の不一致、知っている版の索引の形の失敗）では選択を止める（§2.8 U5・U8）。候補が 1 つも含まなければ「見つからない」を返し、成功にしない。
  - 経路: 内部で sort する実装、`Dictionary` の列挙順、検証の失敗を「含まない」と同じに扱って黙って次の build を採る実装（program §3.1 の 10。§2.8 U5 で止めると決めた）。
- **I-5 既存の manifest の消費者を壊さない。** 索引を持つ manifest は、今の install（`ContentInstaller`・`ContentManifestValidation`）、installed の再検証（`InstalledRevisionVerifier`）、SampleGame の検証 Play・From published directory・BS4 の Player build、Artifacts の `content collect` / `fetch` が受け付ける。索引の無い旧 manifest も今までどおり install できる。`version`・`rootSchemaVersion` は変えない（§2.8 U2）。
  - 経路: 必須欄の追加、`version` の変更（`Validate` が 1 以外を拒む）、manifest の大きさの上限（4 MiB。C# と Artifacts の両方）、Artifacts の JSON の深さ（32）。`ContentManifestValidation` は install だけでなく、登録のたびの `InstalledRevisionVerifier.Verify` / `VerifyInsideLease` でも走るので、索引の版や kind の値をここで拒むと、索引の版を上げた時に古い runtime が manifest 全体を install・再検証できなくなる。
  - 塞ぎ方: install と再検証の検査は、`index` があれば JSON の型（object、`version` は整数、`entries` は object の配列で各欄は文字列）だけを見る。版と値の意味は選択の読み口だけが見る（§2.8 U8）。

**成立条件（起きなければならないこと）**

- **S-1** `Full` の目的 build を publish すると、`transport.json` に索引が入り、その entry の集合は同じ build の `BuildContentRoot` の entry の `(logical key, kind, 実効表現)` の集合と等しい。
- **S-2** 目的 2 つ以上の `transport.json` の bytes と期待 digest を渡すと、要求した Scene を含む build が呼び手の順で選ばれ、候補ごとの理由が返る。要求が表現を指定し、その表現が無く `Full` があれば、directory と同じく含むと答える。
- **S-3** 同じ公開 directory を Local と HTTP の供給元から install した `transport.json` は同じ bytes で、同じ索引と同じ選択の結果になる。

### 2.2 受け入れ条件

fixture は SampleGame を使わない合成（program D10）。実機の build は既存の `BuildContentDirectoryIntegrationTests`（SampleGame の test の assembly にある。SampleGame の型は使わず、internal は既存の friend 経由。置き場は §3）と同じ build の手順で、目的 2 つを作る: 目的 A（Scene `Entry`、Scene `Shared`、Object `SharedPrefab`）、目的 B（Scene `Next`、Scene `Shared`、`Shared` は `Full` と `Whitebox` の 2 表現、`Whitebox` の表現だけを持つ Scene `WhiteboxOnly`）。

| ID | 検証する条件 | 内容 |
|---|---|---|
| AC1 | S-1・I-1 | 単体。publish が、coordinator が記録した entry から索引を書く。entry は `(logical key, kind, 実効表現)` の ordinal 順で、空の表現は `Full`。上書きでない既存の build の report と directory は索引の追加の欄以外変わらない（既存の `BuildContentReportTests`・`BuildContentFullModeTests` が変更なしで通る）。**失敗経路:** 記録の印（outcome の記録の版の整数）が無い outcome（旧 outcome と、coordinator を通さずに書いた test の outcome）からは、拒まずに `version` 0 の索引で publish し、その manifest は `ContentInstaller` で install でき、読み口が「索引なし」と答える（§2.8 U2・U8）。記録の印があって一覧が空の outcome を「索引なし」と取り違えない。同じ `(logical key, kind, 実効表現)` が 2 つある、または空白だけの表現がある記録からは publish を拒む（publisher が書いた bytes を読み口で読み直して検出する）。test は `OneStarMaker.Tests.Editor` の `Tests/Editor/Build` に置く（`OneStarMaker.Tests` は `OneStarMaker.Build.Content` を参照しない）。 |
| AC2 | I-1 | 単体。照合の関数が、表の entry と要求に対し、完全一致、表現を指定しない要求、`Whitebox` の要求で `Whitebox` があれば `Whitebox`、無ければ `Full`、`Full` も無ければ含まない、表現を指定しない要求は `Full` だけに当たり `Whitebox` だけの entry には含まないと答える、生の空の表現の entry は `Full` として当たる、Object も同じ fallback、kind の違いは含まない、を返す。`ContentDirectoryIndex` の `TryResolveScene` / `TryResolveObject` がこの関数を使う（既存の `ContentDirectoryIndexTests` が変更なしで通る） |
| AC3 | I-2 | 単体。読み口は bytes と期待 digest を受ける。**失敗経路:** digest が違えば読まない（検証の失敗）。索引の欄が無い manifest は「索引なし」で、`Shared` の要求に対し「含まない」とも「含む」とも答えない。版が 0 は「索引なし」、2 以上は「未対応の索引」（どちらも「含む」「含まない」と答えない）。版が負、entry の欄の欠け、版 1 での kind の未知の値、`(logical key, kind, 実効表現)` の重複、空白だけの logical key、空白だけの表現は形の失敗。上限 4 MiB を超える bytes は読まない。`ContentManifestValidation.ValidateRequiredFields` と `Validate` は、索引を持つ manifest と持たない manifest の両方を今までどおり通す |
| AC4 | S-2・I-3・I-4 | 単体。`content/` の無い状態で、目的 A と B の `transport.json` の bytes だけを渡す。順 `[A, B]` で `Next` は B（A は「含まない」）、`Entry` は A、`Shared` は A。順 `[B, A]` で `Shared` は B。`WhiteboxOnly` を表現の指定なしで要求すると「見つからない」、`Whitebox` で要求すると B。`Missing` は「見つからない」で、両方の理由が「含まない」。未対応の版の索引を持つ候補を先頭に置くと「未対応の索引」の理由で飛ばす。旧 manifest（索引なし）を先頭に置くと、それを「索引なし」の理由で飛ばして次を選ぶ（§2.8 U5）。**失敗経路:** digest の違う候補や索引の形が壊れた候補が途中にあると、後ろに含む候補があっても選択はそこで止まり、成功にせず「検証の失敗」の理由を返す（§2.8 U5）。選択の公開面は要求を `BuildContentKind` と文字列で受け（`ContentResolution` の `ContentKind` は参照しない）、入力は bytes と期待 digest だけの型とする。file system・Unity の Content Directory・`ContentDirectorySession` を使わないことは、C が新しい file の `using` と呼び出しを検査して確かめる |
| AC5 | I-5 | 単体と既存の test。索引を持つ manifest を `ContentInstaller` で install でき、`InstalledRevisionVerifier` が通す（既存の `ContentInstallerTests`・`ContentManifestValidationTests`・`ContentDeliveryFailureMatrixTests` が変更なしで通る）。SampleGame の `PublishedDirectoryReceiveTests` が通る。版が 2 以上の索引や未知の kind の値を持つ manifest も install でき、再検証を通る（前方互換、§2.8 U8）。**失敗経路:** `index` の JSON の型が壊れた manifest（`index` が object でない、`version` が整数でない、`entries` が配列でない、entry の欄が文字列でない）は、install の必須欄の検査で拒む |
| AC6 | S-1・I-1 | 実機（Unity の build を伴う EditMode test）。目的 A と B を製品の coordinator で `Full` build して publish し、各 `transport.json` の索引の entry の集合が、その `content/` を登録した `BuildContentRoot` の entry の集合と等しい。登録した `ContentDirectoryIndex` と索引の照合が、全 entry、`Missing`、`Shared` の `Whitebox`（B では `Whitebox`、A では `Full` への fallback）、`WhiteboxOnly` の表現の指定なしと `Whitebox`、`Next` を `Object` として要求した時（kind の違い）で同じ答えになる。登録と directory の索引は internal（`ContentDirectorySession`・`ContentDirectoryIndex`）を既存の friend 経由で使う |
| AC7 | S-3・I-3 | 実機。AC6 で publish した B の directory を、`LocalContentArtifactSource` と既存の loopback の HTTP の供給元から別々の cache root に install し、2 つの installed `transport.json` の bytes と公開 directory の bytes が同じで、AC6 の要求の全てについて、公開 directory の bytes で選んだ結果と同じ選択の結果になる |
| AC8 | 範囲 | 差分が §3 の file と、その `.meta` だけで、asmdef の `references`、`AssemblyInfo.cs` の friend、`tools/Artifacts`、`SceneState` に変更が無い。実機の test は既存の build を伴う test と同じ扱い（§5） |

### 2.3 ここでは答えない問いと所有する後続

| 問い | 所有 |
|---|---|
| 供給元が持つ目的 build の一覧と、各 manifest の期待 digest を得る方法（installed cache、Artifacts R2、LAN）。R2 から取った `transport.json` の索引が同じ bytes であることの実測 | S4 |
| 選択を `IContentProvider` として `ContentResolver` に載せること、manifest から候補の事実を作る adapter | S3・S4 |
| 同じ目的の複数 revision から 1 つを選ぶ規則（最新・known-good・pin） | S4 |
| 目的をまたぐ catalog（全目的の索引を 1 file にまとめる）が要るか | S4 の A1（本書の §2.4 の比較の P4） |
| Play の入口で索引を使って目的 build を提案・検査すること、表示 | S5 |
| 上書き（S2-2）の base の選択に索引を使う接続 | S3 |
| SampleGame の実データでの索引の大きさの実測 | S5（G20 と一緒に） |
| Player で索引を使うこと | 持たない（program U10） |

program が「S2」へ送った入力のうち、本書が引き取るのは §10 U6（索引の置き場）だけである。他は S2-2 の HANDOFF §2.4 が引き取り済み。

### 2.4 代替案の比較（置き場・形式・生成時点・選択規則）

**置き場**

| 案 | 内容 | U6 の制約 | 費用と危険 | 推奨 |
|---|---|---|---|---|
| P1 | `transport.json` に任意の欄 `index` を足す | 満たす。manifest の digest が索引を覆う。Local・LAN・Artifacts・installed cache が既に同じ bytes を運ぶ | `ContentTransportManifest` の DTO の追加、raw の形の検査。manifest が entry の分だけ大きくなる（実データで 13 KB 程度） | **推奨** |
| P2 | 公開 directory の直下に別 file（例: `index.json`）を置き、manifest にその sha256 を書く | 満たせるが、運ぶ経路が増える | Artifacts の `content collect` は `transport.json` と `content/` だけを運ぶので、Artifacts と installer と verifier の変更が要る。file が 2 つになり、片方だけ取れた状態の扱いが要る | 不採用寄り |
| P3 | `content/` の中（`BuildContentRoot` か Unity が読まない file）に置く | `BuildContentRoot` は登録しないと読めないので「走査しない」に反する。Unity の directory に OSM の file を混ぜると、Unity が無視するかが未確認 | — | 不採用 |
| P4 | 目的をまたぐ catalog を 1 つ出す（CI の 1 回の全目的 build の後に集約） | 満たせるが、集約の段と新しい保存種別が要る | 「目的ごとに 1 本で build する」（program §5.3）と別の段。Artifacts に catalog の保存種別が無い。P1 の上に後から作れる | 後続（S4 の A1） |

**形式（P1 の場合）**

`index` は `{ "version": 1, "entries": [ { "logicalKey": "...", "kind": "Scene" | "Object", "representation": "..." } ] }`。entry は ordinal 順、表現は root の entry と同じ生の値（tag の無い root は空）で、実効値（空は `Full`）への読み替えは読み口が行う。stable key・Unity の locator・path は入れない（選択に要らず、Unity の内部を配布の protocol にしない既存の方針）。`version` の欄が無いか 0 なら「索引なし」、1 なら本書の形、2 以上は「未対応の索引」（§2.8 U8）。

**生成時点**

| 案 | 内容 | 推奨 |
|---|---|---|
| G1 | projection の root から entry を出す純粋な関数を adapter と coordinator が共有し、coordinator が一覧と記録の印を outcome（追加の欄）に書き、publisher が manifest に写す。`BuildContentResult` へは nullable の任意引数で足す（public の constructor を SampleGame の test が使う） | **採用**（I-1 の出所が 1 つ） |
| G2 | publisher が preflight の `selected` / `roots` / `requirements` から作る | kind が無く、root との一致を別計算に頼る。不採用 |
| G3 | publisher が生成した `BuildContentRoot` の asset を読む | asset は build の一時物で、publish の時点に残る保証が無い。Editor の API に依る。不採用 |

**選択規則（I-4）**

| 案 | 内容 | 推奨 |
|---|---|---|
| R1 | 純粋な関数 `Select(要求, 順のある候補の列)` を Distribution に置く。最初に「含む」と答えた候補を選び、全候補の理由を返す。供給元（S4）が 1 要求に 1 候補を返すためにこれを呼ぶ | **推奨**。`ContentResolver` は供給元の順を、R1 は 1 つの供給元の中の目的 build の順を扱い、層が分かれる |
| R2 | 目的 build ごとに provider を作り、`ContentResolver` の順で選ぶ | 供給元の順と目的の順が 1 列に混ざり、provider の数が build の数だけ増える。provider の製品実装は S3・S4 なので、本書では test の provider でしか示せない |
| R3 | 含む候補がちょうど 1 つの時だけ選び、2 つ以上は曖昧として拒む | 共通の Scene（`Title` など）は全目的に入るので、ほぼ常に曖昧になる |

### 2.5 スライス分割の判断

- 契機: 索引を出す側（BuildSystem の publish）と読んで選ぶ側（Distribution）は、変更理由と触る file が別である。
- 判断: **分割しない。** 問いは「出した索引で選べるか」で、片方だけでは答えられない。どちらも純粋な処理と既存の build の test で閉じ、独立に失敗しうる実機の未知は「索引と directory の一致」（AC6）だけである。

### 2.6 判定定義と停止規則

- 判定定義:
  - **GO:** AC1〜AC8 が全て成り立ち、判定必須テスト（§5）が通る。
  - **NO-GO（owner へ返す）:** AC6 で、索引の entry と登録した root の entry が食い違い、原因が生成の出所（G1）では塞げない（例: Unity が build の時に entry を変える）。どの entry がどう違ったかを記録して返す。
  - それ以外の不成立は通常の差し戻しで B へ戻す。
  - スパイクではないので CONDITIONAL ACCEPT は定義しない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし

### 2.7 本文へ転記した実装制約

- 依存は Game → Framework の一方向。asmdef の `references` と `InternalsVisibleTo` を足さない（足す必要が出たら Phase A へ戻す）。
- 新規・編集する Unity 側の `.cs` は先頭に `#nullable enable`。`record` を使わない。Editor の code を Runtime の assembly に置かない。
- 公開 API のログは `ILogger<T>`（索引の処理はログを出さない見込み）。
- 破棄されうる `UnityEngine.Object` は `== null` / `!= null` で判定する。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。
- 参照 0 を削除理由にしない。
- repo 相対 path は `tools/path-budget.json` の予算に収める。
- coordinator の出力先は `artifacts/bs2b` か `TestResults/bs3-fixture` の下だけ。build の target は StandaloneWindows64 の Player 固定。
- `transport.json` の `version`・`rootSchemaVersion` と既存の必須欄を変えない（§2.8 U2 で owner が別に決めた場合を除く）。
- 機械向けの記録を git に入れない。

### 2.8 owner の判断を求める点（未決事項）

担当の推奨を付ける。owner（root）が 2026-11-16 に、U1〜U7 の全てを担当の推奨どおりと回答した（各項の「決定」）。

- **U1 目的と利用者の文言。** §1 の 2 欄を承認するか。A2 の目的レビューはこの 2 欄の本文だけを見る。 決定: 承認（経緯は §1）。
- **U2 manifest の版。** 推奨: `version` と `rootSchemaVersion` を変えず、`index` を任意の欄として足す（`declaration` と同じ前例。`Validate` が `version` 1 以外を拒むので、上げると既存の消費者が全て止まる）。旧 outcome（記録の無い build の結果）の publish は、推奨: 拒まずに索引なしで出す（既存の batch・BS4 の経路を止めない）。代わりに「索引の無い manifest を publish しない」を選ぶなら AC1 の失敗経路を拒否に固定する。 決定: 推奨どおり。AC1 の失敗経路は「旧 outcome からは索引なしで publish する」に固定する。
- **U3 risk。** program の値は `normal`。public な DTO の欄と新しい public 型（索引の読み口と選択の関数）を足すので、phases の「公開 API」の例に当たる。推奨: `normal` のまま（追加だけで、所有者・寿命・asmdef・`SceneState` を変えない）。`high` にするなら A2 に A0 だけからの代替構成のレビューを足す。 決定: `normal`。A0 だけからの代替構成のレビューは行わない。
- **U4 候補の順の出所。** 選択の関数は順を受けるだけにし、順を決めるのは呼び手（host の policy）とする。推奨: そのまま。SampleGame の具体（今の RunPurpose を先に、残りは何順か）は S5 で決める。 決定: 推奨どおり。
- **U5 索引なし・検証の失敗の候補の扱い。** 推奨: 「索引なし」は理由を残して飛ばす（旧 build が混ざっても、索引を持つ build で選べる）。digest の不一致と形の失敗は、整合性の失敗として選択を止める（program §4.1 の「hash 不正は記録し、同じ expected digest の正規コピーで修復できる時だけ進む」と、§3.1 の 10 の「非互換 revision への暗黙 fallback をしない」に合わせる）。止めずに飛ばす方を選ぶなら AC4 をそちらに固定する。 決定: 推奨どおり。AC4 の digest の違う候補は「検証の失敗」の理由で選択を止める形に固定する。
- **U6 選択の関数の置き場と公開範囲。** 推奨: R1 を `Runtime/BuildContent/Distribution/` に public で置く（S4 の供給元が呼ぶ。Distribution は BuildSystem 本体の分類で、resolver の core を参照しない）。照合の関数は `ContentDirectoryIndex` と共有する internal にする。 決定: 推奨どおり。
- **U7 実機の test の置き場。** 推奨: 既存の `SampleGame/Tests/Editor/Build/BuildContentDirectoryIntegrationTests.cs` の隣に、SampleGame の型を使わない test を足す（登録と directory の索引は既存の friend 経由。A2 の指摘 8 で「public だけ」の文言を直した）（build と loopback の HTTP の道具がそこにある）。Framework の test の assembly へ移すのは、道具ごと動かす別の作業。 決定: 推奨どおり。

- **U8 「索引なし」の bytes 上の形と、索引の前方互換（A2 で出た。U2・U5 の決定の一部を変える）。** `JsonUtility` は入れ子の欄を省けないので、U2 の「索引なしで publish」は `"index":{"version":0,"entries":[]}` を書く。初稿の AC3 は版 0 を形の失敗とし、AC5 は壊れた索引を install で拒むとしていたので、そのままでは旧 outcome からの publish が install できなかった。担当の推奨: (1)「索引なし」を「欄が無い、または `version` 0」と定義する。(2) install と再検証は索引の JSON の型だけを見て、版と kind の値は見ない（索引の版を上げても古い runtime が manifest を install できる。`version` を据え置いた U2 の理由と揃える）。(3) 読み口が知らない版（2 以上）は「未対応の索引」として「索引なし」と同じく飛ばす。U5 の「形の失敗で止める」は、知っている版（1）の索引にだけ当てる。決定: 推奨どおり（owner、2026-11-16）。owner の補足: 索引の無い旧 build との後方互換を残す理由は特に無いが、厳密に拒む必要も無い。そこで (1) の「索引なし」の扱いは推奨のまま残す。

## 3. 責務マップ

行数は base `2c775ff0` の `wc -l` 相当。増分は見込み。

| file | 責務 | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|---|
| `Runtime/BuildContent/Distribution/ContentTransportManifest.cs` | manifest の DTO | `index` の DTO を足す | 値 | なし | public の欄の追加 | AC3・AC5 | 150 → 175 |
| `Runtime/BuildContent/Distribution/ContentManifestValidation.cs` | manifest の raw の形と意味の検査 | 索引の raw の形の検査（あれば） | static | なし | internal | AC3・AC5 | 293 → 340 |
| `Runtime/BuildContent/Distribution/ContentPurposeIndex.cs`（新規、名前は仮） | bytes と期待 digest から索引を読み、照合し、順のある候補から選んで理由を返す | 新しい責務 | 値、呼び出しごと | 上の 2 つ、照合の関数 | public（U6） | AC3・AC4 | 0 → 200 |
| `Runtime/BuildContent/ContentDirectoryIndex.cs` | 登録した directory の索引 | 実効表現と fallback の照合を共有の関数へ出す | static | なし | internal のまま | AC2・既存の `ContentDirectoryIndexTests` | 130 → 140 |
| `Editor/Build/Content/BuildContentCoordinator.cs` / `UnityContentDirectoryAdapter.cs` / `BuildContentReport.cs` / `BuildContentResult`（定義の file） | build と report | projection の root から entry を出す純粋な関数（置き場は adapter の隣の internal static。名前は B が決める）を adapter と coordinator が使い、coordinator が一覧と記録の印を outcome へ書く。result へは nullable の任意引数 | 既存 | 既存 | internal の report の欄、public の constructor の任意引数 | AC1 | 208 / 238 / 259 → 各 +10〜25、新しい関数 +30 |
| `Editor/Build/Content/ContentTransportPublisher.cs` | publish | outcome の entry を manifest の `index` へ写し、書いた bytes を public の読み口で読み直して形の失敗なら拒む | 既存 | 既存 | 変えない | AC1・AC6 | 153 → 185 |
| test（AC2〜AC5 の単体は `OneStarMaker/Tests/BuildContent/Distribution/`、AC1 は `OneStarMaker/Tests/Editor/Build/`、実機は `SampleGame/Tests/Editor/Build/`） | — | — | — | — | — | AC1〜AC7 | 新規 |

- 行数の警報: どの file も 500 行と 50% 増に届かない見込み。新しい file は 1 つの責務（索引の読みと選択）。照合の関数が `ContentDirectoryIndex` と新しい file のどちらに属するかは B が決めてよいが、2 箇所に同じ規則を書かない。
- 配置の理由: 索引の生成は BuildSystem 本体（`Editor/Build/Content`）、読みと選択は配布の層（Distribution）で、[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md) §2.1 の分類に合う。resolver の core（`Runtime/ContentResolution/`）は参照しない。asmdef は全て既存の `OneStarMaker.Runtime` と `OneStarMaker.Build.Content` の中で、参照を足さない。

## 4. 実装計画

- 変更対象: §3 の表。
- 順序: (1) 照合の関数を出し、`ContentDirectoryIndex` を載せ替える（既存 test で非退行）。(2) 索引の DTO・検査・読み口・選択の単体。(3) coordinator の記録と publisher の書き込み。(4) 実機の test。
- Phase B から Phase A へ差し戻す条件: asmdef の参照か friend が要る。`version` を上げないと既存の消費者が索引を持つ manifest を拒む。adapter に渡す entry と同じ計算から一覧を出せない（G1 が成り立たない）。
- 対象外を維持する方法: `tools/Artifacts` と `ContentResolution` を変えない。供給元の一覧と revision の選択の型を作らない。

## 5. テストとレビュー計画

- 単体テスト: AC1〜AC5（Unity の build を伴わない EditMode。選択と読み口は Unity の API を使わない）。
- 差し戻し中の起点 `-Filter`（C が根拠付きで変更可。受け入れ条件の追加ではない）: `OneStarMaker.Tests.BuildContent`、`OneStarMaker.Tests.Editor.Build.BuildContentFullModeTests`、`OneStarMaker.Tests.Editor.Build.BuildContentReportTests`、AC1 の新しい test の class（`OneStarMaker.Tests.Editor.Build` の下。B が名前を決め、§6 に記録する）、`SampleGame.Tests.Editor.Build.PublishedDirectoryReceiveTests`。
- 判定必須テスト（GO 候補 head）: 上の起点 filter、AC6・AC7 の実機の test、最終の全 EditMode 回帰（空 filter。合否は「XML の failed 0 と root result `Passed`」で読む。`-nographics` の VFX 25 件の Inconclusive で `step.json` が `failed` になるため）。
- 全 EditMode 回帰の適用除外: なし。
- 統合・Unity テスト: AC6・AC7 は Content Directory の build を伴う。他の Unity が同時に動くと `BuildContentDirectoryIntegrationTests` が失敗した既知の事例がある（program §8）ので、判定 C は他の Unity を止めて走らせる。build を伴う batch の後の Unity の終了 stall は既知（`tools/Harness/README.md`）で、XML の完成で判定する。
- 操作・実行時・目視条件の検証経路: 対象なし（全て offline のテストで閉じる）。
- 未知の操作経路の疎通結果: なし。
- 人間の判断が必要な条件: なし。
- 高コスト証拠: なし。
- 判定 C の照合 script の方式: なし（XML のテスト名と件数で読む）。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。
- A0/A1 主担当: AI（Claude Code）。
- C' 用に予約した担当: cursor-agent の Grok 系（A2 は使っていない）。
- 独立性の強化条件を満たせない場合の理由: risk が `normal` なら強化条件は対象外。

### 5.1 A2 の独立レビューと A3 の採否

- A2（2026-11-16、同一 PC）。入力版は固定 base `2c775ff0` の clean detached checkout と HANDOFF copy（sha256 `ed2055c9d215b7a680c5f755232c97de41cabf5bb2afe124ce155a035b90749c`。§2.8 に U1〜U7 の決定を書いた版）。
  - アーキテクチャゲート: 担当 Claude Code の subagent（Anthropic、主担当と同じ系列。risk `normal` なので独立性の強化条件は対象外）。照合: HEAD・clean・hash は一致。結論は blocker 1、major 4、minor 5。
  - 目的レビュー: 担当 Claude Code の subagent（Anthropic、目的と利用者の原文だけを渡した別の担当）。入力の sha256 `ecdfac84e869fc19223bbdf2dc7672566a9027efe8a4b6f29f91440deac53c0b`。
- A3 の分類（担当の案を owner が 2026-11-16 に承認。目的・利用者の 2 欄は変えていないので目的レビューのやり直しは無い）:

  | 出所 | 指摘 | 分類 | 反映 |
  |---|---|---|---|
  | ゲート 1（blocker） | `JsonUtility` は入れ子の欄を省けず、「索引なし」が版 0 の索引になり、初稿の AC3・AC5 と合わせると旧 outcome からの publish が install できない | 採用 | I-2、形式、AC1・AC3、§2.8 U8 |
  | ゲート 2（major） | 版と kind の値を install・再検証で拒むと、索引の版を上げた時に古い runtime が止まる | 採用（U8 の裁定に依る） | I-5、AC3・AC5、§2.8 U8 |
  | ゲート 3（major） | 実効表現の規則を Editor の publisher と internal で共有できない | 採用 | I-1 の塞ぎ方（生の値を書き、読み口が直す。publisher は public の読み口で読み直す）、形式、AC1 |
  | ゲート 4（major） | G1 の「同じ計算」の共有の経路と、記録なしを空の索引にする取り違え | 採用 | I-1 の塞ぎ方、G1、AC1、§3 |
  | ゲート 5（major） | 「表現の指定なし」の意味が固定されていない | 採用 | fixture（`WhiteboxOnly`）、AC2・AC4・AC6 |
  | ゲート 6（minor） | AC1 の test の置き場と filter | 採用 | AC1、§3、§5 |
  | ゲート 7（minor） | 空白だけの表現・重複を publish で拒む規則が無い。「未確認」の答えは「ある」 | 採用 | §1 の現況、I-1、AC1・AC3 |
  | ゲート 8（minor） | AC6 は public だけでは書けない | 採用 | §2.2、AC6、U7 |
  | ゲート 9（minor） | 選択の要求の型、4 MiB の上限、AC4 の示し方 | 採用 | AC3・AC4 |
  | ゲート 10（minor） | AC7 の比較の相手 | 採用 | AC7 |
  | 目的 | 索引だけで選べる、build と一致する、供給元に依存しない、副作用が無い | 初稿にある | I-1・I-3、S-3 |
  | 目的 | 索引と build の取り違えを検出できる（識別子を持つ） | 初稿で満たす | 索引は `transport.json` の中にあり、manifest の digest と `contentSet`・`revision` に覆われる（P1）。別 file にしない |
  | 目的 | 欠落や不正を理由付きで返し、理由の種類を分ける | 採用（既にある分類に「未対応の索引」を足した） | I-2・I-4、AC3・AC4 |
  | 目的 | 索引の形式に版があり、合わない時は理由になる | 採用 | 形式、U8 |
  | 目的 | 作らない: 利用者の入力、宣言の新しい項目、走査で補う経路、取得・登録、供給元の探索、Scene と asset 以外の検索、目的をまたぐ索引、UI | 初稿と一致 | §1 の対象外、§2.3 |
  | 目的 | 作らない: 複数が満たす時の優先順位・score・自動選択の方針 | 初稿と一致 | 選択の関数は呼び手が渡した順を当てるだけで、順や score を持たない（U4） |

- A2 の再レビューの要否: 行わない。設計の変更はゲートの指摘をそのまま塞ぐもので、新しい境界・依存・公開面は「public の読み口を publisher が呼ぶ」と「`BuildContentResult` の任意引数」だけである。Draft PR の外部計画レビューで確かめる。
- A3: 統合案。A2 の分類と U8 は owner の裁定済み。外部計画レビューは未了。

## 6. Phase B 実装結果

未着手

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手
