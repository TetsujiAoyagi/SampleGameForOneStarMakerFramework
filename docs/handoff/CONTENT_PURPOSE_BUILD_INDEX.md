# 目的 build の索引（S2-3）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S2-3。S2 を S2-2 と S2-3 に分けた判断は削除済みの `CONTENT_OVERLAY_BUILD.md` の §2.5・§2.8 U2（`git show 25aa59e^:docs/handoff/CONTENT_OVERLAY_BUILD.md`）にある）
- status: `C`（発見 C 完了、阻害する指摘なし。次は判定 C）
- branch: `claude/content-purpose-build-index`（PR の base は `develop`）
- implementation base commit: `2c775ff0`（`origin/develop` の head、2026-11-16。§1 の現況はこの base。起動指示が挙げた `d945927` の後に PR #194 がワークフロー文書だけを変えて進んでいたので、新しい方を採った）
- implementation head commit: `400ff2cc9b6b1898f51acfc2ab68341a2b7ba152`
- A3 凍結記録（承認者・日付・対象 revision・承認の根拠）: 承認者 owner（root）、2026-11-16、Phase A revision 1（本記録を含む commit。SHA は起動指示と PR 状態ブロックで渡す）。根拠: A2 の 2 件と外部計画レビュー 3 件（2 巡の cursor[bot] と owner のアカウントの計画レビュー）の採否を §5.1 に記録し、2 巡目以降の指摘は文言の補いだけで新しい blocker・major が無いことを確かめたうえで、owner が会話で「対応するものはして凍結」と指示した
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
  - 次の Phase・役割: 判定 C。発見 C（§7）は阻害する指摘なしで、implementation head が GO 候補 head。
  - 次の作業: GO 候補 head で §5 の判定必須テスト（起点 filter、AC6・AC7 の `OneStarMaker.Tests.Editor.Build.PurposeBuildIndexIntegrationTests`、最終の全 EditMode 回帰）を実行し、XML のテスト名と件数で必要な集合を確かめ、判定 C と C' の bundle を同じ commit から作り、§7 に判定を記録する。空 filter の全 EditMode は起点 filter と AC6・AC7 の class を含むので、XML で収録を確かめられれば 1 プロセスにまとめてよい。
  - 読む範囲: 既定（Phase C は §0〜§8）。
  - 引き継ぐ commit: この票を含む commit（SHA は起動指示で渡す。PR 状態ブロックは下の owner の指示により更新しない）。implementation base は上の `2c775ff0`、implementation head は上の欄。
  - 入力の snapshot・bundle: なし（機械向けの期待値・照合 script は無い。§5）。
  - 必要な権限: Unity（sandbox 外の承認済み経路で `pwsh tools/run-tests.ps1`。AC6・AC7 は Content Directory の build を伴うので、他の Unity を止めて走らせる。§5）。
  - 停止規則: §2.6 と §4 の差し戻し条件。`RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` の Phase B と同時に走らせない。
  - 引き渡さないもの: なし。
  - owner の指示（2026-11-16、Phase C の進行）: 発見 C・判定 C・差し戻しの B の修正は、B と同じ系列のモデルが新しい session で担う。owner は独立性制約ありを受け入れ済みで、独立性のためにモデルを上げない。各担当は §7 に独立性制約ありと記録する。C' は §5 の予約どおり cursor-agent の Grok 系。
  - owner の指示（同上）: C' の完了まで push せず、PR 本文（状態ブロック）も更新しない。PR 状態ブロックの `引き継ぐ commit` は Phase B の出口の値のまま古くなるので、開始時の照合には使わず、起動指示の SHA と §0 の implementation head 欄で照合する。各 Phase の出口で PR 状態ブロックを更新する手順は、この期間は行わない。

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
  - 塞ぎ方: 読み口は bytes と期待 digest の両方を受け、上限 4 MiB と sha256 を照合してから raw の形を検査する。`JsonUtility` は入れ子の class の欄を省けないので、publisher は索引を出さない時も `"index":{"version":0,"entries":[]}` を書く。そこで「索引なし」は「`index` の欄が無い、または型の検査を通った `version` が 0」と定義する（§2.8 U8）。型の検査を通った知らない版（2 以上）は「未対応の索引」として「索引なし」と同じく理由を残して飛ばす（§2.8 U8）。型の検査に落ちた索引は版に関係なく形の失敗である（下の (c)）。
  - 判定の順（外部計画レビュー 1 巡目の指摘 2 で固定した）: (a) 上限 4 MiB を超える bytes と digest の不一致は検証の失敗。(b) `index` の欄が無ければ「索引なし」。(c) `index` があれば、版に関係なく install と同じ JSON の型の規則（`index` は object、`version` は整数、`entries` は object の配列、entry の各欄は文字列）で検査し、`version` の欄が無い・整数でない・32 bit の符号付き整数（DTO の `int`）の範囲を外れる場合も含めて外れれば形の失敗（raw の値で判定し、`JsonUtility` の `int` への変換の挙動に頼らない）（正規の publish は `JsonUtility` が `version` を必ず書くので、欄が欠けるのは正規でない bytes だけ）。(d) その後で版を見る。0 は「索引なし」（`entries` の要素の値は見ない）、2 以上は「未対応の索引」、それ以外の整数（負の値）は形の失敗、1 は値（kind の値、重複、空白だけの logical key・表現）を検査し、外れれば形の失敗。
- **I-3 選択は content を登録・走査しない。** 選択の入力は `transport.json` の bytes だけで、`content/` の file を開かず、Unity の Content Directory を登録しない。索引は publish の bytes の中にあり、Editor にだけある file ではない。
  - 経路: `BuildContentRoot` を読むために `ContentDirectorySession` を作る実装、`Library/BuildHistory` の `ContentLayout.json` や preflight（Editor の手元にだけある）を読む実装、installed cache の `content/` を歩く実装。
- **I-4 選ぶ build を暗黙に決めない。** 複数の候補が含むときは、呼び手が渡した順で最初の 1 つを選ぶ。revision・contentSet の文字列の大小や候補の列挙順で決めない。含まない候補・索引の無い候補・未対応の版の索引の候補は、理由を残して飛ばす。検証に落ちた候補（上限超過、digest の不一致、形の失敗）では選択を止める（§2.8 U5・U8）。候補は渡された順に 1 つずつ評価し、最初の「含む」か最初の検証の失敗のどちらか先に来た所で止まる。止まった後ろの候補は評価せず、理由は「未評価」とする（`ContentResolver` の「採用した時点で止まる」と揃える。外部計画レビュー 1 巡目の指摘 3）。候補が 1 つも含まなければ「見つからない」を返し、成功にしない。
  - 経路: 内部で sort する実装、`Dictionary` の列挙順、検証の失敗を「含まない」と同じに扱って黙って次の build を採る実装（program §3.1 の 10。§2.8 U5 で止めると決めた）。
- **I-5 既存の manifest の消費者を壊さない。** 索引を持つ manifest は、今の install（`ContentInstaller`・`ContentManifestValidation`）、installed の再検証（`InstalledRevisionVerifier`）、SampleGame の検証 Play・From published directory・BS4 の Player build、Artifacts の `content collect` / `fetch` が受け付ける。索引の無い旧 manifest も今までどおり install できる。`version`・`rootSchemaVersion` は変えない（§2.8 U2）。
  - 経路: 必須欄の追加、`version` の変更（`Validate` が 1 以外を拒む）、manifest の大きさの上限（4 MiB。C# と Artifacts の両方）、Artifacts の JSON の深さ（32）。`ContentManifestValidation` は install だけでなく、登録のたびの `InstalledRevisionVerifier.Verify` / `VerifyInsideLease` でも走るので、索引の版や kind の値をここで拒むと、索引の版を上げた時に古い runtime が manifest 全体を install・再検証できなくなる。
  - 塞ぎ方: install と再検証の検査は、`index` があれば JSON の型（object、`version` は整数、`entries` は object の配列で各欄は文字列）だけを見る。版と値の意味は選択の読み口だけが見る（§2.8 U8）。

**成立条件（起きなければならないこと）**

- **S-1** `Full` の目的 build を publish すると、`transport.json` に索引が入り、その entry の集合は同じ build の `BuildContentRoot` の entry の `(logical key, kind, 表現)` の集合と、生の値どうしで等しい（tag の無い root の空の表現は空のまま）。
- **S-2** 目的 2 つ以上の `transport.json` の bytes と期待 digest を渡すと、要求した Scene を含む build が呼び手の順で選ばれ、候補ごとの理由が返る。要求が表現を指定し、その表現が無く `Full` があれば、directory と同じく含むと答える。
- **S-3** 同じ公開 directory を Local と HTTP の供給元から install した `transport.json` は同じ bytes で、同じ索引と同じ選択の結果になる。

### 2.2 受け入れ条件

fixture は SampleGame を使わない合成（program D10）。実機の build は既存の `BuildContentDirectoryIntegrationTests`（SampleGame の test の assembly にある。SampleGame の型は使わず、internal は既存の friend 経由。置き場は §3）と同じ build の手順で、目的 2 つを作る: 目的 A（Scene `Entry`、Scene `Shared`、Representation の tag の無い Object `SharedPrefab`）、目的 B（Scene `Next`、Scene `Shared`、`Shared` は `Full` と `Whitebox` の 2 表現、`Whitebox` の表現だけを持つ Scene `WhiteboxOnly`）。

| ID | 検証する条件 | 内容 |
|---|---|---|
| AC1 | S-1・I-1 | 単体。publish が、coordinator が記録した entry から索引を書く。entry の表現は root の entry と同じ生の値（tag の無い root は空のまま）で、logical key、kind の文字列（`Object` < `Scene`）、生の表現の順に、それぞれ ordinal で並べる。上書きでない既存の build の report と directory は索引の追加の欄以外変わらない（既存の `BuildContentReportTests`・`BuildContentFullModeTests` が変更なしで通る）。**失敗経路:** 記録の印（outcome の直下に置く整数の欄。入れ子の object にしない。欄が無ければ `JsonUtility` の既定の 0 と読み、0 を「記録なし」とする）が 0 の outcome（旧 outcome と、coordinator を通さずに書いた test の outcome）からは、拒まずに `version` 0 の索引で publish し、その manifest は `ContentInstaller` で install でき、読み口が「索引なし」と答える（§2.8 U2・U8）。記録の印があって一覧が空の outcome を「索引なし」と取り違えない。同じ `(logical key, kind, 実効表現)` が 2 つある、または空白だけの表現がある記録からは publish を拒む（publisher が書いた bytes を読み口で読み直して検出する）。test は `OneStarMaker.Tests.Editor` の `Tests/Editor/Build` に置く（`OneStarMaker.Tests` は `OneStarMaker.Build.Content` を参照しない）。 |
| AC2 | I-1 | 単体。照合の関数が、表の entry と要求に対し、完全一致、表現を指定しない要求、`Whitebox` の要求で `Whitebox` があれば `Whitebox`、無ければ `Full`、`Full` も無ければ含まない、表現を指定しない要求は `Full` だけに当たり `Whitebox` だけの entry には含まないと答える、生の空の表現の entry は `Full` として当たる、Object も同じ fallback、kind の違いは含まない、を返す。`ContentDirectoryIndex` の `TryResolveScene` / `TryResolveObject` がこの関数を使う（既存の `ContentDirectoryIndexTests` が変更なしで通る） |
| AC3 | I-2 | 単体。読み口は bytes と期待 digest を受ける。**失敗経路:** digest が違えば読まない（検証の失敗）。索引の欄が無い manifest は「索引なし」で、`Shared` の要求に対し「含まない」とも「含む」とも答えない。判定の順は I-2 のとおりで、各段の境界を 1 つずつ試す: 上限 4 MiB を超える bytes は検証の失敗。`index` の欄が無い、または版 0 は「索引なし」、版 2 以上は「未対応の索引」（どちらも「含む」「含まない」と答えない）。`index` が object で `version` の欄が無い、`version` が `"1"`・`1.0`・`true`・`4294967297`（`int` の範囲外）、版 2 で `entries` の型が壊れている、版 0 で `entries` の型が壊れている、はどれも型の失敗（形の失敗）。版が負、版 1 での entry の欄の欠け・kind の未知の値・`(logical key, kind, 実効表現)` の重複・空白だけの logical key・空白だけの表現は形の失敗。`ContentManifestValidation.ValidateRequiredFields` と `Validate` は、索引を持つ manifest と持たない manifest の両方を今までどおり通す |
| AC4 | S-2・I-3・I-4 | 単体。`content/` の無い状態で、目的 A と B の `transport.json` の bytes だけを渡す。順 `[A, B]` で `Next` は B（A は「含まない」）、`Entry` は A、`Shared` は A。順 `[B, A]` で `Shared` は B。`WhiteboxOnly` を表現の指定なしで要求すると「見つからない」、`Whitebox` で要求すると B。`Missing` は「見つからない」で、両方の理由が「含まない」。未対応の版の索引を持つ候補を先頭に置くと「未対応の索引」の理由で飛ばす。旧 manifest（索引なし）を先頭に置くと、それを「索引なし」の理由で飛ばして次を選ぶ（§2.8 U5）。**失敗経路:** digest の違う候補、索引の形が壊れた候補、4 MiB を超える候補が途中にあると、後ろに含む候補があっても選択はそこで止まり、成功にせず「検証の失敗」の理由を返す（§2.8 U5）。順 `[A, digest の違う B]` で `Shared` を要求すると A を選んで成功し、B は「未評価」（I-4）。順 `[digest の違う A, B]` で `Shared` を要求すると A の「検証の失敗」で止まり、B は「未評価」（「含まない」「含む」と評価しない）。順 `[A, B]` で `Entry` を要求すると A を選び、B は「未評価」。選択の公開面は要求を `BuildContentKind` と文字列で受け（`ContentResolution` の `ContentKind` は参照しない）、入力は bytes と期待 digest だけの型とする。file system・Unity の Content Directory・`ContentDirectorySession` を使わないことは、C が新しい file の `using` と呼び出しを検査して確かめる |
| AC5 | I-5 | 単体と既存の test。索引を持つ manifest を `ContentInstaller` で install でき、`InstalledRevisionVerifier` が通す（既存の `ContentInstallerTests`・`ContentManifestValidationTests`・`ContentDeliveryFailureMatrixTests` が変更なしで通る）。SampleGame の `PublishedDirectoryReceiveTests` が通る。版が 2 以上の索引や未知の kind の値を持つ manifest も、型の規則を保つ限り install でき、再検証を通る（前方互換は型の規則を保つ版に限る。§2.8 U8）。**失敗経路:** `index` の JSON の型が壊れた manifest（`index` が object でない、`version` が整数でない、`entries` が配列でない、entry の欄が文字列でない）は、install の必須欄の検査で拒む |
| AC6 | S-1・I-1 | 実機（Unity の build を伴う EditMode test）。目的 A と B を製品の coordinator で `Full` build して publish し、各 `transport.json` の索引の entry の集合が、その `content/` を登録した `BuildContentRoot` の entry の集合と、生の値どうしで等しい（tag の無い `SharedPrefab` の空の表現を含む）。登録した `ContentDirectoryIndex` と索引の照合が、全 entry、`Missing`、`Shared` の `Whitebox`（B では `Whitebox`、A では `Full` への fallback）、`WhiteboxOnly` の表現の指定なしと `Whitebox`、`Next` を `Object` として要求した時（kind の違い）で同じ答えになる。登録と directory の索引は internal（`ContentDirectorySession`・`ContentDirectoryIndex`）を既存の friend 経由で使う |
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

`index` は `{ "version": 1, "entries": [ { "logicalKey": "...", "kind": "Scene" | "Object", "representation": "..." } ] }`。entry は ordinal 順、表現は root の entry と同じ生の値（tag の無い root は空）で、実効値（空は `Full`）への読み替えは読み口が行う。stable key・Unity の locator・path は入れない（選択に要らず、Unity の内部を配布の protocol にしない既存の方針）。entry の並べ順は logical key、kind の文字列、生の表現の順にそれぞれ ordinal。`index` の欄が無いか `version` が 0 なら「索引なし」、1 なら本書の形、2 以上は「未対応の索引」。どの版でも型の規則（I-2 の (c)）は同じで、型の規則を変える版は古い runtime の install を止める（§2.8 U8）。

**生成時点**

| 案 | 内容 | 推奨 |
|---|---|---|
| G1 | projection の root から entry を出す純粋な関数を adapter と coordinator が共有し、coordinator が一覧と記録の印を outcome（追加の欄）に書き、publisher が manifest に写す。`BuildContentResult` へは nullable の任意引数で足す（public の constructor を SampleGame の test が使う） | **採用**（I-1 の出所が 1 つ） |
| G2 | publisher が preflight の `selected` / `roots` / `requirements` から作る | kind が無く、root との一致を別計算に頼る。不採用 |
| G3 | publisher が生成した `BuildContentRoot` の asset を読む | asset は build の一時物で、publish の時点に残る保証が無い。Editor の API に依る。不採用 |

**選択規則（I-4）**

| 案 | 内容 | 推奨 |
|---|---|---|
| R1 | 純粋な関数 `Select(要求, 順のある候補の列)` を Distribution に置く。順に評価し、最初の「含む」か最初の検証の失敗で止まり、評価した候補の理由と、評価しなかった候補の「未評価」を返す。供給元（S4）が 1 要求に 1 候補を返すためにこれを呼ぶ | **推奨**。`ContentResolver` は供給元の順を、R1 は 1 つの供給元の中の目的 build の順を扱い、層が分かれる |
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

- **U8 「索引なし」の bytes 上の形と、索引の前方互換（A2 で出た。U2・U5 の決定の一部を変える）。** `JsonUtility` は入れ子の欄を省けないので、U2 の「索引なしで publish」は `"index":{"version":0,"entries":[]}` を書く。初稿の AC3 は版 0 を形の失敗とし、AC5 は壊れた索引を install で拒むとしていたので、そのままでは旧 outcome からの publish が install できなかった。担当の推奨: (1)「索引なし」を「欄が無い、または `version` 0」と定義する。(2) install と再検証は索引の JSON の型だけを見て、版と kind の値は見ない（索引の版を上げても古い runtime が manifest を install できる。`version` を据え置いた U2 の理由と揃える）。(3) 型の検査を通った知らない版（2 以上）は「未対応の索引」として「索引なし」と同じく飛ばす。U5 の「形の失敗で止める」は、型の失敗（どの版でも）と、版 1 の値の失敗に当てる（外部計画レビュー 2 巡目で文言を判定の順に揃えた）。決定: 推奨どおり（owner、2026-11-16）。owner の補足: 索引の無い旧 build との後方互換を残す理由は特に無いが、厳密に拒む必要も無い。そこで (1) の「索引なし」の扱いは推奨のまま残す。外部計画レビュー 1 巡目（指摘 2）を受けた補い（owner 採用、2026-11-16）: 判定の順を I-2 の (a)〜(d) に固定し、`index` の中の型の失敗は版に関係なく形の失敗とする。(2) の前方互換は、型の規則を保つ版に限る。

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
- C' 用に予約した担当: cursor-agent の Grok 系。ただし Draft PR の外部計画レビュー（cursor[bot]）も Grok 系だったので、Phase A に関与した系列になる。Google 系と Codex は使えないので、C' は Grok 系のまま独立性制約ありとして行う（risk `normal` なので強化条件は対象外）。
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
- 外部計画レビュー 1 巡目（2026-11-16、Draft PR の cursor[bot]、Grok 系。対象 commit `7a708e31`）。owner が 4 件とも採用した。A2 の観点の再レビューは行わない（受け入れ条件の観測を 1 つに固定する文の補いで、境界・依存・公開面は変えない。2 巡目の外部計画レビューで確かめる）:

  | 指摘 | 分類 | 反映 |
  |---|---|---|
  | 1 索引に書く表現が生の値と実効値の両方に読め、並べる key の kind の比べ方も無い。AC6 の fixture は tag の無い root を踏まない | 採用 | 生の値に統一（S-1・AC1・形式）。並べ順を固定。目的 A の `SharedPrefab` を tag 無しにした |
  | 2 U8 の境界（`version` の欠け・整数でない値、版 2・版 0 で `entries` の型の破損）で、飛ばすと止めるの両方が読める | 採用 | I-2 に判定の順 (a)〜(d)、AC3 に境界の試験、AC5 と U8 に前方互換の範囲 |
  | 3 `[含む, digest 不一致]` の結果が 3 通りに読める。4 MiB 超を検証の失敗に数えるかが無い | 採用 | I-4・R1 に「最初の含むか最初の検証の失敗で止まり、後ろは未評価」、AC4 に例と 4 MiB 超 |
  | 小さい点: 記録の印を入れ子にすると版 0 と同じ区別できない問題が起きる | 採用 | AC1 に「outcome の直下の整数、欄が無ければ 0」 |

- 外部計画レビュー 2 巡目（2026-11-16、対象 commit `80d29178`）。1 巡目の 4 件は閉じたと確認された。新しい blocker・major は無い。owner が下の 3 件を全て採用した。A2 の観点と外部計画レビューの再レビューは行わない（文言を判定の順に揃える補いで、設計と観測を変えない）:

  | 出所 | 指摘 | 分類 | 反映 |
  |---|---|---|---|
  | cursor[bot]（Grok 系）小さい点 1 | I-2 の定義文と U8 (3) が、型の壊れた版 0 を「索引なし」と読める。(d) に負の版が無い | 採用 | I-2 の定義文、(d)、U8 (3) |
  | cursor[bot] 小さい点 2 | 検証の失敗で止まった後ろの候補が、AC4 では未評価に固定されていない | 採用 | AC4 に `[digest の違う A, B]` と `[A, B]` の `Entry` の例 |
  | owner のアカウントの計画レビュー（軽微） | raw の整数の検査（`long.TryParse`）を通る `int` の範囲外の版を、`JsonUtility` の変換の挙動に任せない | 採用 | I-2 の (c)、AC3 に `4294967297` の例 |

- A3: 凍結（§0 の凍結記録）。

## 6. Phase B 実装結果

- 担当: Claude Code（2026-11-16、同一 PC）。開始時の照合: 渡された `c16df025` と HEAD が一致し clean、§0 の凍結記録・revision 1・次担当 B、PR 状態ブロックと一致。
- 実装（§4 の順）:
  1. 照合の規則を `ContentDirectoryIndex.cs` の internal `ContentEntryMatch`（実効表現 = 空なら `Full`、key の組み立て）と `ContentEntryTable<T>`（`TryAdd` は同じ `(logical key, 実効表現, kind)` で false、`TryResolve` は要求した表現 → 無ければ `Full`。表現の指定なしは `Full`）に出し、`ContentDirectoryIndex` を載せ替えた。照合の関数は `ContentDirectoryIndex.cs` に置いた（§3 で B が決めてよい点）。
  2. DTO: `ContentTransportManifest.index`（`ContentTransportIndex { version, entries }`、`ContentTransportIndexEntry { logicalKey, kind, representation }`）。検査: `ContentManifestValidation` に索引の型の規則を 1 つ（`ReadIndex` / `ReadIndexShape`）置き、`ValidateRequiredFields`（install・再検証）と読み口が共有する。`version` は raw の字面を `int.TryParse` で 32 bit の範囲まで見る（install も同じ規則）。entry の 3 欄は「あれば文字列」。読み口と選択: 新規 `Distribution/ContentPurposeIndex.cs`（名前は仮のまま採用）。public は `ContentPurposeCandidate`（bytes の複製と期待 digest）、`ContentPurposeRequest`（`BuildContentKind`・logical key・任意の表現）、`ContentPurposeIndex.Read` / `Contains` / `Select`、状態・失敗・判定の enum、`ContentPurposeSelection`。判定の順は I-2 の (a)〜(d)、選択は I-4 のとおり（最初の「含む」か最初の検証の失敗で止まり、後ろは `NotEvaluated`）。
  3. Editor: `UnityContentDirectoryAdapter.cs` に internal `BuildContentRootKeys.Describe(ProjectedContent)`（kind は path の `.unity`、表現は生の値）を置き、adapter の entry 作成と coordinator の記録の両方が使う。coordinator は成功時だけ `BuildContentResult` の任意引数 `indexEntries` に渡し、`WriteOutcome` が outcome の直下に `indexRecord`（整数、1 = 記録あり）と `indexEntries` を書く。publisher は記録 0 なら `{"version":0,"entries":[]}`、1 なら logical key・kind・生の表現の ordinal 順で版 1 を書き、書いた bytes を `ContentPurposeIndex.Read` で読み直して期待の状態でなければ公開せずに拒む。記録の値が 0・1 以外の outcome は拒む（outcome は coordinator だけが書く）。
  4. test: AC2・AC3 は `Tests/BuildContent/Distribution/ContentPurposeIndexTests.cs`、AC4 は同 `ContentPurposeSelectionTests.cs`、AC5 は同 `ContentPurposeIndexInstallTests.cs`、AC1 は `Tests/Editor/Build/ContentPurposeIndexPublishTests.cs`（§5 の「AC1 の新しい test の class」は `OneStarMaker.Tests.Editor.Build.ContentPurposeIndexPublishTests`）、AC6・AC7 は `SampleGame/Tests/Editor/Build/PurposeBuildIndexIntegrationTests.cs`（class の full name は `OneStarMaker.Tests.Editor.Build.PurposeBuildIndexIntegrationTests`。隣の既存の統合 test と同じ namespace）。
- AC6 の実装の選び方（B 適応）: 登録は既存の統合 test と同じく PlayMode（`SAMPLEGAME_CONTENT__RUNTIMEMODE=addressables`）に入ってから、internal の `UnityContentDirectoryBackend` で公開 directory の `content/` を登録し、`GetRoots` の root を `ContentDirectoryIndex.TryCreate` で索引にする（`ContentDirectorySession` は経由しない。session は内部の索引を公開しないため）。登録前に fixture の source を消す。
- 行数（base → 現在）: `ContentTransportManifest.cs` 150 → 174、`ContentManifestValidation.cs` 293 → 353、`ContentPurposeIndex.cs` 0 → 282、`ContentDirectoryIndex.cs` 130 → 159、`BuildContentCoordinator.cs` 208 → 215、`UnityContentDirectoryAdapter.cs` 238 → 261、`BuildContentReport.cs` 259 → 279、`ContentTransportPublisher.cs` 153 → 186。500 行と 50% 増に届く file は無い。
- §3 との差: `BuildContentResult` に public の読み取り property `IndexEntries` を足した（任意引数の値を outcome へ運ぶため）。preflight にも `ContentReportFile` の新しい欄が `0` と空で書かれる（同じ保存形を共有するため。AC1 の「索引の追加の欄以外変わらない」の範囲）。asmdef の `references`・friend・`tools/Artifacts`・`Runtime/ContentResolution/`・`SceneState` は変えていない。
- 新しい事実の分類: なし（上の AC6 の登録経路は B 適応、HANDOFF の範囲内）。
- 検証（Unity 6000.6.0f1、`pwsh tools/run-tests.ps1`。Unity テストの B での実行は、Skill の既定（H2 未適用の B は実行しない）と票が食い違ったので owner に確かめ、owner が 2026-11-16 に「B で走らせる」と回答した）:
  - Editor のコンパイル: 成功（`compilationFailed=false`。console の error 26 件は Burst の compiler server の起動失敗で、本変更と無関係）。
  - 1 回目: 起点 filter（§5）+ AC6・AC7 の test。178 件中 177 Passed、1 Failed（`ContentPurposeIndexTests.InstallValidation_AcceptsManifestsWithAndWithoutIndex`。test の要求の contentSet・revision が fixture と違う test 側の誤り）。AC6・AC7 の `PurposeBuildIndexIntegrationTests` は Passed（192 秒）。
  - test を直して 2 回目: 起点 filter（§5、AC6・AC7 を除く）。step `passed`・exit 0、XML `Passed` total 177・failed 0・skipped 0・inconclusive 0。AC6・AC7 は 2 回目で再実行していない（実装は 1 回目から変えていない）。
  - `pwsh tools/contract-audit.ps1`: errors 0・warnings 0。`pwsh tools/docs-audit.ps1`: errors 0、warning 1（既存の program HANDOFF の harvest の頃合い。本作業と無関係）。
- 未実行: 最終の全 EditMode 回帰（判定必須。判定 C が行う）。
- Editor が開いた時に `Assets/MobileDependencyResolver` の `.pdb.meta` 5 件を消したが、本作業と無関係なので戻し、commit に含めていない。

## 7. Phase C

### 発見 C（1 回目）

- 種別: 発見。対象は implementation base `2c775ff0` → head `400ff2cc`（固定 commit の完全 diff。21 file、+1,871 −33）。
- 開始時の照合: 起動指示の `027e7369` と HEAD が一致し clean。implementation head `400ff2cc` は HEAD の祖先で、その先の差分は本書だけ。PR #195 の timeline に Phase A 以降の新着は無い（最後は 2026-11-16 04:18 の owner のアカウントのレビューで、対象は `80d29178`）。状態ブロックは owner の指示（§0）で照合に使わない。
- 使用した evidence bundle の取得案内・状態: 未生成。発見 C は同一 PC の worktree で固定 commit の diff を直接読んだ（テスト未実行の発見レビュー）。判定 C と C' の bundle は判定 C が同じ commit から作る。
- 構造適合: §3 の責務マップと一致する。照合の規則は `ContentDirectoryIndex.cs` の internal `ContentEntryMatch`・`ContentEntryTable<T>` の 1 箇所で、`ContentDirectoryIndex` と `ContentPurposeIndex` の両方が使う（I-1 の塞ぎ方）。root から `(logical key, kind, 生の表現)` を出す関数は `BuildContentRootKeys.Describe` の 1 つで、adapter の entry 作成と coordinator の記録の両方が `projection.Roots` に当てる。索引の型の規則は `ContentManifestValidation.ReadIndexShape` の 1 つで、install・再検証と読み口が共有し、版と値の意味は読み口だけが見る（I-5・U8）。新しい `ContentPurposeIndex.cs` は `System`・`System.Collections.Generic`・`System.Text` だけを使い、file system・Unity の Content Directory・`ContentDirectorySession` を呼ばない（AC4 の C の検査）。asmdef の `references`・friend・`tools/Artifacts`・`Runtime/ContentResolution/`・`SceneState` に変更は無い（AC8）。`base..head` の `PLAY_CONTENT_RESOLUTION_PROGRAM.md` の 1 行は Phase A の commit の変更。行数: `ContentPurposeIndex.cs` は 282 行で §3 の見込み 200 を超えるが、public の結果の型（候補・要求・判定・選択の 4 class と 4 enum）を同じ file に置いたためで、責務は「読みと選択」の 1 つ。500 行・50% 増の警報に当たる file は無い。
- 失敗経路の照合: I-2 の判定の順 (a)〜(d) は `Read` の順と一致し、版 0 で `entries` の値を見ない・負の版は形の失敗・`int` の範囲は raw の字面で判定、を確かめた。I-4 は最初の「含む」か最初の検証の失敗で止まり後ろは `NotEvaluated`。publisher は記録 0 なら版 0、1 なら ordinal 順の版 1 を書き、読み直しの状態が期待と違えば staging のまま拒む（重複・空白だけの表現の test がある）。AC1〜AC7 の各失敗経路に対応する test を読んだ。
- 現在の問いを阻害する findings: なし。
- 阻害しない findings（記録のみ）:
  - minor（test の強さ）: `PurposeBuildIndexIntegrationTests.AssertMatchesRegisteredDirectory` の `SharedPrefab/Object/`（空の表現）の確認は「root に `SharedPrefab` があれば」の条件付きで、A の root から `SharedPrefab` が消えた場合は空振りする。ただし表現を `Full` に読み替える食い違い（I-1 の経路 (2)）は条件の中で検出され、`SharedPrefab` が消えるのは plan の候補を落として build が成功する場合だけで、本スライスの不変条件の外である。集合の等しさ（AC6 の主張）は無条件に検査されている。差し戻しの理由にしない。次に B がこの file を触る時に、A では無条件に検査する形へ直してよい。
- 後続スライスへ移送する findings: なし。
- 実行したテストコマンド: なし（発見 C は Unity を起動していない）。機械検査: `pwsh tools/contract-audit.ps1` は errors 0・warnings 0（checks 1〜9、検査 8 は対象外）。差分の追加行に `Task.Delay`・`Thread.Sleep`・`record`・`ZLogger` は無く、`UnityEditor` は SampleGame の Editor の test の assembly だけ。
- テスト結果: §6 の B の実行（起点 filter 177 件 Passed、AC6・AC7 は 1 回目で Passed）を読んだだけで、判定の入力にしない。
- 判定必須のうち未実行: 全て（起点 filter、AC6・AC7、最終の全 EditMode 回帰）。判定 C が GO 候補 head で実行する。
- 重い検証を発見段階で限定実行した場合の理由と範囲: なし。
- 未確認事項: なし。
- 担当・モデル: Claude Code（Anthropic。B と同じ系列で、新しい session）。独立性制約あり（owner が 2026-11-16 に受け入れ済み。§0）。

## 8. Phase C'

未着手

## 9. Phase D

未着手
