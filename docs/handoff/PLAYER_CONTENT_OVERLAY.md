# 遊べる Windows の Player に手元の上書きを重ねて起動する

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S4-2）
- status: `B`（Phase A は revision 0 で 2026-11-24 に凍結した。Phase B は 2026-11-24 に終えた。発見 C の 1 巡目が阻害の指摘 3 件で B へ戻した（§7）。B は 2026-11-25 に DC1〜DC3 を直した（§6.5）。次は発見 C の 2 巡目。B と発見 C の往復の 1 巡目を終えた（上限 4 巡））
- branch: `claude/player-content-overlay`（PR の base は `develop`）
- implementation base commit: `1fd200b5ba835c833035d153579216e14cb823af`（`develop` の head、2026-11-24、S4-1a の merge の後。§1 の現況と A2 の固定 base もこれ）
- implementation head commit: `6f58156bd5530794953f03838b3e57cb023d3c3b`
- A3 凍結記録: 承認者は owner（root）、2026-11-24、対象は revision 0（この記録を含む commit の本書）。承認の根拠: S4-1a の後の `develop` への載せ替え、G0 (a)(b)(d) と u8 の実測、取り直した A2 の 2 件、外部計画レビュー 6・7 巡目の採否の反映の後、8 巡目の review（HEAD `b7a61c50`）が新規の blocker なしで凍結を推奨し、owner が同日に凍結を裁定した。凍結は計画としての承認で、実装の GO や実機の確認済みを意味しない。
- risk: `high`（Framework の起動（`AbstractApplicationInitializer`、`ContentOverlayStartup`）の Player の分岐と、Player が受け付ける config を変える。上書きの登録を Player で初めて製品の口から行う。独立して失敗しうる未知が複数ある（§5 の u1〜u6・u9〜u12））
- owner: root（依頼者）
- created: 2026-11-23
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: [04 章](../../unity/Assets/Docs/Architecture/04-app-startup.md) §4.6「起動の構成」（map の log の `hash=`）と §4.7「上書きの起動の設定」（Player の分岐と digest の key）、[13 章](../../unity/Assets/Docs/Architecture/13-resource-system.md)「base と上書きの登録契約」の「範囲」（Player の禁止を解いた範囲）、[18 章](../../unity/Assets/Docs/Architecture/18-asset-description.md)「宣言からの Play 経路」（Player の入口）、[実測の事実](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)（G0 と u8 の実測）、repo の README の「試す」、[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S4-2 の行と §2.3 の後続へ送る入力
- 関連: [program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S4-2 の行、「S4-0 の A1 が振り分けた入力」「S4-0 から移送した入力」「S4-1 から移送した入力」「S4-1a から移送した入力」の S4-2 の項目、§5.3「上書きの方式を選ぶ理由と見直しの条件」「Windows の Player での上書きの実測」
- Phase A snapshot の取得案内・状態: 該当なし（機械向けの期待値・照合 script は無い。§5。レビューの入力は本書と program だけ）。G0 の harness の差分と record は Phase A の gate の証拠で、状態は §2.5 の 10
- Phase B result snapshot の取得案内・状態: 該当なし（B は Unity の test を実行していない。結果は §6 と implementation head の差分だけ）
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: 発見 C（2 巡目）。動作確認の対象は無い（差し戻しの修正は test 2 件と build の戻しの保存で、利用者の操作の経路を変えない）。
  - 次の作業: §6.5 の修正が §7 の DC1〜DC3 を解いたかを確かめ、1 巡目で未確認のまま残った事項（§7 の「未確認事項」）へ進む。DC3 は X1（build を返した同じ process の比較で差分 0 件）で確かめる。
  - 読む範囲: 既定のとおり（§0〜§8）。§6.5 と §7 の発見 C（1 巡目）を先に読む。
  - 引き継ぐ commit: この票を含む commit（SHA は起動指示と PR の状態ブロックで渡す）。implementation head は欄の SHA で、そこから HEAD までの差は本書だけ。
  - 必要な権限: Unity の batch test と Player の build・起動（sandbox 外）、自分で起動した Unity と Player だけを止める。push は公開点の規則に従う。
  - 停止規則: §5 の発見 C の手順と u10 の不成立時の扱い、§2.1 の D11 の除外の規則に従う。
  - 引き渡さないもの: なし（C' へは発見 C・判定 C の所見を渡さない）。
## 1. 目的と対象外

- 目的: 編集する開発者が、検証済みの base（遊べる Player と同じ build の、検証済みの installed revision）を載せた Windows の遊べる Player に、手元の編集から作った上書きを 1 つ重ねて起動し、編集した Scene と asset を Player の上で確かめられるようにする。重ねられない時（上書きが base と合わない、上書きが build の後に書き換わった）は、base だけの起動に縮退せず、Player を起動する前か起動の失敗として理由を示す。Player は build した時の script で動き、script の code か field の違いや、上書きが Player に無い型を使うことでは止めない（その部分は Unity の扱いのとおり欠けて動く）。動作確認の Player（BS4）の receipt と exit code の契約、Player の protected config、起動の fail-closed は弱めない。
  - 目的の改訂（A3、2026-11-24）: 元の文言は「重ねられない時（上書きが base と合わない、上書きが build の後に書き換わった、上書きが Player に無い型を使う、script の code か field が Player を build した時と違う）は、base だけの起動に縮退せず、Player を起動する前か起動の失敗として理由を示す。」。owner（root）が同日に「build の時の script で動けばよい」と裁定した。理由は、Player の code は build の時点のままで、編集した本人がそれを知っており、field の違いは type tree で穏やかに読まれ、実際に困るのは Player に無い型を使う場合だけであること。改訂の後に目的レビューをやり直した（§5.1）。2 回目の改訂（A3、2026-11-24）: 「上書きが Player に無い型を使う」を止める理由の列から外した。owner（root）が同日に「型は止めも知らせもしない。component が無いだけなら動いていい」と裁定した。目的レビューのやり直しは 1 回を使い切ったので、規則どおり owner が文言を裁定し、A3 の採否に残した（§5.1）。
- 利用者: Player で自分の編集を確かめたい編集する開発者（担当外の source は手元に無くてよい）。Player と base は、自分か CI が遊べる Player の build の menu で作ったものを使う。上書きを作って Player を起動するのは Editor の menu の 1 つの操作で、遊ぶのは起動した Player の窓の中の操作である。
- 対象外（所有する後続は §2.3）:
  - 上書きを 2 つ以上重ねること、base を Player と別の build にすること（Player の build identity と違う base）。
  - 動作確認の Player（`smoke`）に上書きを重ねること（今のまま拒む）。同梱の directory（installed の pair なし）に重ねること。
  - Player での要求時解決の配線と record（`WireContentResolution` は Player の構成では呼ばないまま。理由は §2.1 の D5）。
  - script の差を吸収する機構（field の migration、型の代替、missing script の stub）と、script の差を上書きで Player へ届ける機構。script の code・field の違いでは止めず、Player は build した時の script で動く（owner の決定、2026-11-24）。
  - 不一致を警告に格下げして続ける・照合を飛ばす switch。base だけで起動する fallback。
  - Player の中で上書きを読み直すこと（hot reload）、Player の中で値を書き換えること。
  - Player や base を自動で作り直すこと。CI の build・取得の手順と「自分の base」を選ぶ規則（S4-3）。
  - Android・iOS（S4-5）。Mono、他の stripping の水準、development build の Player。
  - 上書きの登録の順・個数・時点・解除の契約そのもの（13 章のまま。Player でも同じ口を使う）。
- 現況（base `1fd200b5`。S4-1a の後）:
  - **上書きの起動の判定（`Runtime/Bootstrap/ContentOverlayStartup.cs:67-85`）:** key `editor:contentOverlayPath`・`editor:contentOverlayIdentity` が 1 つでもあれば、Player の構成か Editor の Play でない時は `Rejected("Content overlay is only available in Editor Play Mode.")`。Editor では mode が `directory`（か無し）で installed の pair がある時だけ `Overlay`。`ReadContentDirectoryConfiguration`（`AbstractApplicationInitializer.cs:1202-1273`）は `BeforeSceneLoad`（`:274`。baked の config は直前の `BuildConfig` が読み終えている）で呼ばれ、Decide を Player の分岐より前に呼び（`:1210-1216`）、`Rejected` を `InvalidConfiguration` の例外にする。Player の分岐（`:1240-1249`）は `PlayerContentConfiguration.Resolve` の結果から `ContentDirectoryConfiguration` を作り、overlay を渡さない（`:1248`）。
  - **Player の installed の照合（`Runtime/BuildContent/PlayerContentConfiguration.cs:22-34`）:** baked の 10 の protected key を重ねた後の値と ordinal に比べ、installed の pair があれば `InstalledRevisionVerifier.Verify(root, digest, baked.ContentSet, baked.BuildIdentity, target)`。照合は receipt の revision が baked の build identity と同じことを求めるので、Player に載せられる base は Player と同じ build の installed revision だけである。pair が無ければ exe の横の同梱 directory（検証なし）。
  - **起動の順（S4-1a）:** `BootstrapSourcePolicy.Plan`（`Runtime/Bootstrap/BootstrapSourcePolicy.cs:99-117`）が、検証済みの baked の種類から登録の位置・UICommon の規則・map の読み口を決める。遊べる Player は `AfterSceneLoad` の最初に content を登録し（`AbstractApplicationInitializer.cs:359-363`）、続けて UICommon（`:365-375`）と SceneResourceMap（`:382-391`）を登録した content から読む。動作確認の Player（`smoke`）は今の写しの順（開始 Scene の UICommon を借用、map は Player の写し、登録はサービスの開始の後 `:417-421`）のまま。起動の log は 3 行: `[AppInit] Content directory registered: identity=… from=installed|directory`（`:863`）、`[AppInit] Bootstrap UICommon: source=…`（`:375`）、`[AppInit] Bootstrap SceneResourceMap: source=…`（`:391`）。Player の構成の latch は開始 Scene の名前（`UICommon` か `PlayablePlayerStart`）。
  - **登録（`AbstractApplicationInitializer.cs:829-865`）:** `InstallConfiguredContentDirectoryAsync` は configuration の `Overlay` があれば `ContentDirectorySession.RegisterVerifiedWithOverlay(verified, overlay.Path, overlay.Identity)`（base → 上書き、上書きの root の記録と base の 5 欄の照合、失敗は例外で base だけの session を返さない）を呼び、成功で `[AppInit] Content overlay registered: identity=… baseRevision=… path=…` を出す。登録は上書きの中身の hash を見ない（04 章 §4.7）。遊べる Player ではこの method が UICommon より前に走るので、登録の失敗は UICommon と map を読む前に起きる。
  - **Player の Scene の load:** Player の構成では要求時解決を配線しない（`:427-433`）ので、`SceneDirector` は `IAssetManagement.LoadContentSceneAsync`（content 入口）で Scene を読む。13 章は、層の選び方と id による影を loader と content 入口の両方が守ると書く。
  - **遊べる Player の build（`SampleGame/DependOnAll/Editor/Build/SampleGamePlayerBuildCoordinator.cs:72-165`）:** `spring-full` を Full で build し、`artifacts/playable/transport/<identity>/` へ publish、`PlayerBuildProjectMutation`（`Assets/Resources/BS4/<identity>/` の所有の印、`Assets/StreamingAssets/bs4-runtime.json`、IL2CPP と managed stripping High、戻し）の中で `BuildPlayableFromStartScene`（`:126-165`）が commit した空の開始 Scene（`PlayablePlayerStart.unity`。root が 0 個でなければ build しない）1 つだけを Scene にして build し、`PlayerBuildPublisher.Publish`（`playerWork` の中身と content と `build-receipt.json` を staging に揃えて `players/<identity>/` へ移す）で置く。bootstrap（UICommon の Scene・map・`SceneResource`）は写さない。Player と transport は同じ build identity を持ち、`build-receipt.json` は `identity` と `transportManifestSha256` を持つ。identity は `yyyyMMddTHHmmssfffZ-<guid>`（`BuildContentCoordinator.cs:57`）で、ordinal の順が build の時刻の順になる。
  - **installed の候補の検証（`PlayEntryCandidates.cs:33-104`）:** Editor の Play の入口は、cache（`VerifyPlayLauncher.ResolveCacheRoot(ContentDeliveryWindow.LoadSettings().cacheRoot)`）の `ContentCacheStore.Inspect()` の entry を宣言と target で絞り、`ValidateInstalled` と宣言の照合（`VerifyPlayLauncher.CompareDeclaration`）を通った `ContentInstallResult` を返す。
  - **Delivery の「次に使う」（`ContentDeliveryPlayBridge.cs`）:** ledger は `SessionState`（`OSM.ContentDelivery.PlayBridge.v1`）で Unity を閉じると消える。手で戻す ledger があると Editor の Play の入口は「利用者の明示の設定」として扱う（`PlayEntryDecision.cs:349-350` の `HasExplicitSetting`）。
  - **Editor の上書きの入口（`PlayEntryOverlay.cs`、`SampleGameContentBuild.cs:121-186`）:** `OverlayPorts(declaration, baseInstall, suppliedSceneKeys, now)` が準備・計画（`ContentOverlayPlanner.Plan`）・認め・再利用か build の port を作り、`PlayEntryOverlay.Run` が順に呼ぶ。上書きの contentSet は `<宣言>-local-edits`（`:111`）、出力の root は `artifacts/bs2b`（`:113`。Framework の `BuildContentCoordinator` が許す root は `artifacts/bs2b` と test 用の 1 つだけ。`BuildContentCoordinator.cs:47-56`）。上書きの build は毎回新しい identity の directory（`<root>/content/<identity>/`）に出て、既にある directory を書き換えも消しもしない。未保存の Scene と asset は、入口が `EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo` と「Save and continue / Cancel」の確認（`PlayEntryInterceptor.cs:41-51,104-109`）に通す。
  - **上書きの完全性:** 上書きは publish されず（`ContentTransportPublisher.Publish` が拒む）、file の一覧と hash を持つ manifest が無い。build の後に上書きの directory が書き換わっても Editor も Player も見分けない。
  - **判定の test の形（`SampleGame/Tests/Editor/Build/PlayerKindEndToEndTests.cs`）:** 同期の `[Test]` の中で Player を起動し、`PlayerRun.WaitForLine`（`:374-388`）が Player の log を読み直して待つ。待つ間 main thread は test が持つので、`EditorApplication.update` は回らない（Unity の一般の挙動からの見込みで、この project では測っていない）。
  - **game の log と終わり方:** S4-1 のとおり（game の log は exe の横の `OneStarMakerLocal/Log/`、遊べる Player の起動の失敗は receipt を書かずに Player の log の stage と例外と exit 1、窓を閉じると exit 0）。
  - **G0 の結果（(a)(b) は 2026-11-23、Phase A の実行担当 = 前の session。Unity 6000.6.9f1）:**
    - (a) **別の `Library` の base: 答えは「同じ」。** S4-0 の harness（commit `b101b382`）を 2 つの新しい worktree X・Y（それぞれの `Library`）に取り出した。X で S4-0 の第 1 process をそのまま走らせて base と Player（IL2CPP・High、development build でない）を build し、run の root を Y へ写した。Y では harness に足した `[Explicit]` の test 1 つ（`PlayerOverlayBuilds.OverlayFromOtherLibrary`。commit しない変更で、差分の sha256 は `9c731bb1b446b3185269d13a43a2103315decc35f1ce5f7d4f68e305b018ed82`）が、Y の `Library` で fixture を v1 → v2 にして上書きだけを build し直し、X の上書きを退かして置き換え、帰属と digest を取り直した。その後 Y で S4-0 の第 2 process を走らせた。前提は全て成り立った。2 つの `Library` の間で script の file と編集した 2 物の id は等しく bytes は違い、共通の texture・material・shader の file は名前と bytes まで同じだった。6 種の round を 3 回ずつ、18 の Player の process は全て自分で exit 0 で終わり、残った process は無い。補助の判定は GO・「同じ」で、record の観測は S4-0 と同じだった（両方の root の id から v2、class は正しく Missing の行は 0、実体の数は共通物 2 つずつ・base だけの物 1 つずつ、寿命は壊れない、2 つの root の Scene の id と `LoadableObjectId` が等しい）。X の第 1 process の Unity は XML（`Passed`、total 1）を書いた後に終わらず（既知の終了 stall。Package Manager の停止と memory leak の報告の後で止まった）、その session が自分の起動した process だけを CIM で止めた（`run-tests.ps1` は exit 0）。遊べる Player も `link.xml` を足さず harness の Player と同じ構成なので、この答えをそのまま使える（§2.5 の (6)）。
    - (b) **型の集合の材料: ある。** content の build ごとに Unity が `Library/BuildHistory/<build>/ScriptsOnlyCache.yaml` を書き、`usedScripts`（assembly ごとの script の class の完全名）と `nativeClasses`（engine の class ID の int32 little-endian の 16 進の列）を持つ。Unity を開かずに text で読める。X の base は `ProbeBaseOnly`・`ProbeBoth`、上書きは `ProbeBoth`・`ProbeOverlayOnly` で、base に無い script の class を class の粒度で見分けられた。Player の build 自身の保持の集合は binary の `.buildreport`（`strippingInfo`）にあり、読んでいない。実データの `spring-full` の base の `nativeClasses` は 37 個で、`AudioSource`（82）・`ParticleSystem`（198）・`SpriteRenderer`（212）を含まない。
    - 証拠: X・Y の worktree の `TestResults/pov/1123141440/`（record、Player の log、`verdict-second.json`、`launch-entries.json`）と `run-tests.ps1` の XML。ローカル固定（未保存）で、Artifact Storage へは送っていない。
    - (d) **script の field の差: 止まらず、Error も出ない。**（2026-11-24、本 session。Unity 6000.6.9f1）(a) の Y で `ProbeBoth`（base と上書きの両方が使う script。field は `_tag`（string）と `_value`（int））を 4 通りに変え、Y の `Library` で上書きだけを build し直して、X の Player（build の時の script のまま）で S4-0 の第 2 process を通した（harness の fixture に、変えた field の形へ値を書く 8 行の差分を足した）。4 通りとも、18 の Player の process は全て自分で exit 0 で終わり、残った process は無く、Missing の component は 0、Player の log の Error・Exception を含む行の数は (a) の時と同じだった（増減したのは終了の時の `Curl error 42: Callback aborted` の行だけで、(a) の log にも出る）。Player が上書きの版の `ProbeBoth` から読んだ値: field を足す（`int _added`）→ `_tag` と `_value` は上書きの値のままで、足した field は無視された。field を消す（`_value`）→ `_tag` は上書きの値、`_value` は 0。型を変える（`_value` を string に、float に）→ どちらも `_tag` は上書きの値、`_value` は 0（float の 302.5 も 0 で、変換はされない）。足した variant の上書きの file に `_added` の名前が入っていること（type tree が変わったこと）を確かめた。Unity の終了の stall は起きなかった。範囲は `MonoBehaviour` の単純な field の 1 つの class、1 台の PC の 1 run で、入れ子の `[Serializable]` の型、配列、`[SerializeReference]`、field の改名は測っていない。
    - 証拠（追加）: Y の worktree の `TestResults/pov-g0d/`（variant ごとの `ProbeBoth.cs`、record、Player の log、XML、実行した driver、u8 の 2 つの Player の log と game の log）と `TestResults/pov-g0a-evidence/`（(a) の record の写し）。(a) と (d) の harness の差分は `b101b382` に当てる 1 つの patch にした（`harness-g0a-g0d.patch`、sha256 `c396519a9c4a2870acf5a4547082b093e770b40b70e0f4f4cb23efd83736e79b`。(a) の `[Explicit]` の test と (d) の fixture の 8 行）。ローカル固定（未保存）。
  - **u8 の結果（2026-11-24、本 session。S4-1a の判定が作った遊べる Player（identity `20261124T010140781Z-…`）を一時 folder へ写して測った）:** 同じ folder の exe を、`-logFile` だけを変えて続けて 2 つ起動した（installed の pair も上書きの key も無い既定の起動）。2 つ目も 3.6 秒で初回 Scene の行（`[AppInit] Player content first scene:`）まで進み、Player の log に Error・Exception の行は無く、窓を閉じる要求でどちらも exit 0 で終わった。game の log は 2 つの process が同じ 1 file（`OneStarMakerLocal/Log/<日付>_000.log`）へ書き、132 行のうち 2 行が JSON として読めない行（行の混ざり）になった。範囲は 1 台の PC の 1 回で、上書きつきの 2 つの起動は測っていない（AC13 が測る）。
- UC ごとの利用者の入力:

| UC | 利用者が選ぶ・入れるもの | 機械が決めるもの | 誰がいつ決めるか | 目的からの正当化 |
|---|---|---|---|---|
| UC1 遊べる Player と base を用意する | menu `Tools/OSM/Content/Build Spring Playable Player`（既存）を選ぶ（CI の Player を使う時は、Player と transport の folder を手元の `artifacts/playable/` の同じ path に置く。取得は S4-3）。その transport を Delivery の窓で install する（既存の操作。Player ごとに 1 回） | Player の構成、`player-compat.json`（D11）、transport | 開発者か CI の実行時の操作。他は自動導出 | 目的の「自分か CI が build の menu で作ったもの」「検証済みの installed revision」。install は base を検証済みにする既存の唯一の口である。Delivery の「次に使う」は選ばせない（D6 の (2)。Unity を開くたびの選び直しと、Editor の Play の入口の挙動の変化を避ける）。install を入口が代わりに行うことは、取得と一緒に S4-3 へ送る（§2.3） |
| UC2 上書きを重ねて Player を起動する | menu `Tools/OSM/Content/Play Playable Player With Local Edits`（新規、1 つ）を選ぶ。未保存の Scene・asset があれば保存を認め、作り直せない root があれば認める（どちらも既存の Editor の入口と同じ確認） | Player（`artifacts/playable/players/` の最新の identity）、base（その Player の identity と transport の digest に合う install）、編集と上書きの root（計画）、上書きの再利用か build、Player の入力の指紋の照合、上書きの digest、起動の環境変数 | 選ぶのは実行時の操作。他は自動導出 | 目的の「上書きを作って Player を起動するのは Editor の menu の 1 つの操作」。Player・base・編集・hash を手で入れさせない（目的レビューの指摘）。保存の確認は上書きが disk の file から作られるため、作り直せない root の認めは黙って通さないため |
| UC3 遊ぶ・閉じる | 窓の中の操作、窓を閉じる | なし | 実行時の操作 | 目的の「Player の上で確かめる」 |

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 編集する開発者が、検証済みの base を載せた遊べる Player に、自分の編集の上書きを重ねて起動でき、照合の失敗を base だけの起動に縮退せずに理由を示せるか（program §8 の S4-2 の行）。

### 2.1 進める最低条件

**前提のスライス:** S4-1a（遊べる Player が content を登録してから UICommon と SceneResourceMap を content から読む起動の順）は 2026-11-24 に Phase D を終えた（PR #213）。このスライスはその後の `develop` に載る。Player も UICommon と map を content から読むので、それらの編集も上書きで Player に届く。

**線（owner の決定、2026-11-24）:** 止めるのは「入れた物が信用できない」時（上書きの digest、base の記録、key の欠け、Player に焼き込む入力の違い）だけである。「入れた物の一部が Player に無い・古い」（script の差、型の欠け）は止めず、知らせもしない。

**設計の要点（§3・§4 の根拠。D7 は欠番）:**

- **D1 base と Player:** base は遊べる Player と同じ build identity の installed revision だけ（今の照合のまま）。Player と base は同じ coordinator の 1 回の build が作る。別に build した base を Player に載せる形は作らない。「別の `Library` の base」は、Player と base を別の checkout（別の `Library`）で build し、上書きを手元で build する形で確かめる（G0 (a) と AC11）。
- **D2 Runtime の判定:** `ContentOverlayStartup.Decide` に Player の種類（検証済みの baked の `PlayerKind`。重ねた後の config の値は使わない。`BeforeSceneLoad` の Decide の時点で baked は読み終えている）と digest の key を渡し、Player の構成で種類が `playable`、installed の pair があり、mode が `directory` の時だけ `Overlay` にする。上書きの 3 つの key（path・identity・digest）の**どれか 1 つでも**あれば判定に入る（digest だけの起動を `None` にしない）。それ以外は `Rejected` で、reason は次のとおり分ける（今の Decide は Player の構成なら pair より前に「Editor Play Mode」の文を返すので、順を変える）: pair の無い `playable` の Player は、今の Editor の分岐と同じ文 `Content overlay can only be layered on a verified installed revision (content:installedRevisionPath and content:manifestSha256).`。`smoke` の Player と、Editor の Play でも Player の構成でもない起動は、今の文 `Content overlay is only available in Editor Play Mode.` のまま。Player の分岐は `Overlay` を `ContentDirectoryConfiguration` に渡し、登録は Editor と同じ `RegisterVerifiedWithOverlay` を使う。登録の位置は S4-1a の `BootstrapSourcePolicy.Plan` が決めた位置（遊べる Player は `AfterSceneLoad` の最初）のままで、このスライスは位置を変えない。key は `content:` の外のままなので protected の照合（`PlayerContentConfiguration.Resolve`）は変えない。`ContentOverlayPlayConfig` の remarks（Player の構成では起動の失敗）を直す。
- **D3 上書きの完全性（Player が正本）:** 上書きの content directory の全 file の相対 path と SHA-256 から 1 つの digest を作る internal の関数（`ContentOverlayDigest`、Runtime）を足し、key `editor:contentOverlayDigest` を Player の構成では必須にする。起動は登録の直前（`InstallConfiguredContentDirectoryAsync` の中、`RegisterVerifiedWithOverlay` の前）に digest を取り直して比べ、違えば起動の失敗にする（遊べる Player では UICommon より前）。入口は build の時の digest を `overlay-record.json`（D8）に残し、再利用の時と起動の直前に取り直して比べる。Editor の Play では key を任意のままにし、key があれば照合する（無視しない）。digest の規則: 上書きの content の directory の直下から再帰した全 file を、相対 path を `/` 区切りにして ordinal（大小文字を区別）で並べ、各 file について `<相対 path>\n<SHA-256 の小文字の 16 進>\n` を UTF-8 で連ねた bytes の SHA-256（小文字の 16 進）。file の hash は Runtime の既存の `ContentDeliveryFiles.Sha256(path)` を使う。digest を取る path は、登録が使うのと同じ正規化した path（`ContentRevisionGate.TryNormalize`）にし、path が不正な時は今の登録と同じ失敗にする。空の folder は入れない。content の directory の外の file は入れない。上書きの root 自体か directory の中に reparse point（symlink・junction）があれば辿らずに失敗にする。合格例: 同じ bytes の file の組は同じ digest。不合格例: file を 1 つ足す・消す・1 byte 変える・名前の大小文字を変えると違う digest になる。
- **D4 script と型は照合しない（owner の決定、2026-11-24）:** Player は build した時の script で動く。script の code・field の違いでも、Player に無い型（新しい script、base が使わない既存の class や組み込みの型、`[SerializeReference]` の型）でも止めず、知らせもしない。Unity はその component か参照だけを欠いて続ける。content は type tree つきで build される（root の JSON の `BuiltWithTypeTrees: true`）。field の差の読まれ方は G0 (d) で測った（§1 の現況）。型を見たい時の手段は Editor の専用の debug menu にして後続へ送る（§2.3）。Runtime は script と型の照合をしない。
- **D5 要求時解決:** Player では配線しない。content 入口が層と影を守る（13 章）ので、上書きの版を読むのに loader は要らない。record は付かず、由来は登録の 1 行の log で示す。配線なしで層が効くことは AC1 で確かめる（u5）。
- **D6 入口:** SampleGame の Editor の menu 1 つ（UC2）。順は次のとおり。
  - (1) **Player を決める。** `artifacts/playable/players/*/build-receipt.json` を読み、`identity` が ordinal で最大の Player を選ぶ（identity は build の UTC の時刻で始まる）。選んだ identity を Console に出す。folder が無い・receipt が読めない・選んだ Player に `player-compat.json` が無い（このスライスより前の build）時は `NoPlayablePlayer` で止め、理由と「遊べる Player を build する」案内を出す。古い Player へ戻らない。合格例: Player が 2 つあれば新しい identity の方。不合格例: 新しい方に compat が無い時に古い方を使う。
  - (2) **base を決める。** Editor の Play の入口と同じ cache から、宣言（`spring-full`）と target が合い、revision が Player の identity と同じで、manifest の digest が Player の `build-receipt.json` の `transportManifestSha256` と同じ install を取り、`PlayEntryCandidates` の既存の検証（`ValidateInstalled` と宣言の照合）を通して `ContentInstallResult` にする（新しい口は `ContentInstallResult` だけを返す。Editor の Play の値（`DirectoryPlay`、`firstScene`）は作らず、検証の中身は既存の code と共有して複製しない）。無ければ `BaseNotInstalled` で止め、「`artifacts/playable/transport/<identity>` を Delivery の窓で install する」案内を出す。検証に落ちたら `BaseInvalid` で止めて理由を出す。Delivery の ledger と Editor の process の環境の `content:` の値は読まず、書かない（Editor の Play の入口の状態を変えない）。最新の install や別の revision へ縮退しない（Player が指す 1 つだけを使う）。
  - (3) 未保存の Scene と asset を確認に通す（Cancel なら `Cancelled` で止める）。Scene は `EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo`。asset は、Editor の入口の検出（`PlayEntryInterceptor.EnumerateUnsavedAssets`。可視性を internal にするだけで中身は変えない）で一覧を取り、この入口の「Save and continue / Cancel」の dialog を出す。Editor の入口の先置きの応答の `SessionState` の key（`OSM.PlayEntry.SaveAssetsResponse.v1`）は読まない（2 つの入口が応答を取り合わない。test は port を差し替える）。未保存の物が 0 件なら確認を呼ばない（batchmode の `EditorUtility.DisplayDialog` は窓を出さずに false を返すので、判定の E2E の初期状態は `Assets/` の Dirty が 0 件であること。patch は Unity の起動の前に disk へ当てる）。
  - (4) 保存が終わった後の disk で、D11 の Player の入力の指紋を照合する（上書きの準備（`Prepare`）より前。保存より前に照合すると、Inspector で変えて未保存の render pipeline の asset のような Player の入力の編集が、照合を通った後の保存で disk に書かれてすり抜ける）。通れば、既存の `PlayEntryOverlay.Run` の段（準備・計画・認め・再利用か build）を、`suppliedSceneKeys` を空にして呼ぶ（Player には snapshot の供給元が無いので、開いている Scene の編集も上書きに入れる）。計画・build の例外は `EntryFailed` で止めて message を出す。
  - (5) D3 の digest を起動の直前に取り直す。取り直しの直後・環境を置いて process を作る前に、何もしない port（`BeforeProcessStart`）を 1 つ呼ぶ（製品では空。AC18 (b) の test だけが、ここで上書きの 1 file を書き換える。AC4 (a) の差し込みは取り直しの前で、別の位置である）。
  - (6) 子 process の環境から継承した `SAMPLEGAME_*` を全て消してから、次の key だけを置く: 常に installed の pair の 2 key（`content:installedRevisionPath`・`content:manifestSha256`）。上書きがある時だけ、加えて上書きの path・identity・digest の 3 key。protected の 10 値、`PurposeLaunchSettings.DirectoryPlay` の `content:firstScene` と runtime の既定値、Editor の snapshot の印は置かない。消すのは子 process の環境だけである。`-logFile` を入口の work（`artifacts/playable/overlays/logs/`）の下に渡して Player の process を窓つきで起動する。
  - (7) 起動の後の観測（D10）を始め、menu はすぐ戻る。
  - 編集が無い（`NotRequired`）時は上書きの key を置かずに base だけで起動し、Console に「上書きなし（content の編集なし）」を 1 行出す（照合の失敗の縮退ではない）。作り直せない root だけの時（`UnbuildableOnly`）も同じで、Editor の入口と同じ通知を出す。
- **D8 上書きの置き場と再利用:** 上書きの build の出力は Framework の `BuildContentCoordinator` が許す root（`artifacts/bs2b`）の下に置き、Editor の入口とは contentSet だけで分ける（`<宣言>-player-local-edits`。`OverlayPorts` に contentSet を引数で渡せるようにし、Editor の入口は今の値を渡すので挙動を変えない。Framework の許可 root は変えない）。再利用の記録は contentSet ごとなので、2 つの入口が同じ記録を取り合わない。上書きの build は毎回新しい identity の directory に出て、既にある directory を書き換えも消しもしないので、動いている Player が使う directory は次の build で変わらない。入口も古い上書きを消さない（溜まることは §2.3）。入口の記録は `artifacts/playable/overlays/` に置く: 上書きの identity ごとの `overlay-record.json`（build の時の digest。Editor の再起動をまたいで残る）と、D10 の `last-attempt.json`。`PlayEntryOverlay.Run` に渡す `AcknowledgeAndReuse` の port を包み、既存の再利用の照合が通っても、記録の digest と今の digest が違えば null を返して既存の `AcknowledgeAndBuild` に進ませる（`Run` の後では build の段に戻れないので、この位置に固定する）。`overlay-record.json` が無い・読めない時も null を返す（build し直す）。記録は、`Run` が `Built` を返した後に入口が書く。
- **D9 同じ Player の folder の 2 つの process:** 前の Player を開いたまま入口を実行すると、新しい identity の上書きで 2 つ目の Player を同じ folder の exe から起動する。入口は止めない。2 つ目が起動できることは u8 で測った（§1 の現況）。2 つの process は game の log を同じ 1 file に書き、行が混ざる（既知の制約として README の「試す」に書く。Player の log は `-logFile` で process ごとに分かれる）。
- **D10 入口の結果の記録と起動の後の観測:** 入口は 1 回ごとに `artifacts/playable/overlays/last-attempt.json` を書き、AC は B が決める文言ではなくこの欄で合否を読む。
  - `outcome`（enum の名前）は入口が Player の process を作るか止めるかを決めた時点の結果で、値は `Launched`（上書きつきで process を作った）、`LaunchedWithoutOverlay`（content の編集なし。`UnbuildableOnly` を含む）、`NoPlayablePlayer`、`BaseNotInstalled`、`BaseInvalid`、`PlayerInputDiffers`（D11）、`OverlayChangedAfterBuild`（D3）、`Cancelled`、`EntryFailed`。止めた時は理由を `detail` に書き、Console と dialog に出す。
  - `startup` は process を作らなかった時 `NotStarted`、作った時 `Pending` で始め、次のどれかに 1 回だけ書き換える: Player の log に初回 Scene の行（`[AppInit] Player content first scene:`）が出たら `Observed`（上書きつきの起動で、その前に上書きの登録の行（`RegisteredLogMarker`）が無ければ `Failed`）、初回 Scene の行の前に process が終わったら `Failed`、起動から 120 秒を超えたら `TimedOut`。`Failed` の時は Player の log のうち `Exception` を含む最初の行を `startupReason` に写し、Console と dialog に出す。`outcome` は書き換えない。
  - **観測の 1 回分は関数にする**（`PlayerOverlayStartupObserver.Step`。入力は記録の中身・Player の log の text・process が終わったか・経過の秒、出力は新しい `startup` と理由。Unity を呼ばない）。製品では `EditorApplication.update` が約 1 秒ごとに呼び、判定の test は自分の待ちの loop の中から同じ口を呼ぶ（同期の test の間は `EditorApplication.update` が回らないため。§1 の現況）。
  - **観測の途中の domain reload と Editor の終了:** `last-attempt.json` に pid・process の開始時刻・Player の log の path・起動の時刻を書いておく。domain reload の後と Editor の起動の時（`[InitializeOnLoad]`）に、`startup` が `Pending` の記録があれば読み直して観測を続ける。その pid と開始時刻の process がもう無ければ、Player の log だけで決める（初回 Scene の行があれば `Observed`、無ければ `Failed`）。次の入口の実行は記録を上書きする。
  - 製品の観測（update と起動時の読み直し）は、Player の log を自分で開いて読む。開き方は、起動中の `-logFile` を読める共有の指定（`FileShare.ReadWrite | FileShare.Delete`。今の判定の test の `ReadLog` と同じ）にする。
  - 計画が上書きに入れた root の一覧（logical key）と、再利用の段の結果（`Reused` / `Built`）、選んだ Player の identity、子 process の環境に置いた `SAMPLEGAME_*` の名前の一覧（入口が実際に組み立てた環境から取る）も書く。root の一覧は、既存の計画の要約（`PlayEntryOverlayPlanSummary`。今は件数だけを持つ）に logical key の列を足して取る（Editor の入口と共有する型に欄を足すだけで、Editor の入口の挙動は変えない）。
  - `last-attempt.json` は入口・観測・判定の test が書くので、別名に書いてから置き換える（既存の `ContentOverlayReuse` の記録と同じ書き方）。記録は 1 つなので、前の Player の観測の途中で入口をもう一度実行すると、前の記録は新しい記録で置き換わる（前の Player の観測はそこで終わる）。
  - 初回 Scene の行の先頭は、Runtime の internal の定数にして `ContentOverlayPlayConfig` が public で公開し直す（`PlayerFirstSceneLogMarker`。04 章の「host は定数を使い、文字列を複写しない」に合わせる）。
  - 入口は作った process の handle を port の戻り値で呼び手に返す（判定の test が exit code を読み、閉じる要求を送るため。製品の観測は domain reload で handle を失うので、pid と開始時刻で生死だけを見る）。
  - Runtime の新しい reason は文言を固定する: digest の不一致 `Content overlay digest does not match the overlay directory.`、digest の key の欠け `Content overlay on a Player needs editor:contentOverlayDigest.`。
  - `PlayerContentStages`（動作確認の Player の receipt 専用の段の列）は `startup` の観測に使わない。
- **D11 Player に焼き込む content 以外の入力（owner の決定で止める）:** 遊べる Player の build が、project の mutation（`PlayerBuildProjectMutation`）を始める前に Player の入力の指紋を取り、`player-compat.json` として、Unity の Player の build が返った後・`PlayerBuildPublisher.Publish` を呼ぶ前に `playerWork`（`artifacts/playable/work/<identity>`）へ書く（Publish が Player と一緒に原子的に置く。Publisher は変えない。`smoke` では書かない）。file を集める口は 1 つ（`PlayerInputFiles`。Unity の I/O を持つ薄い file）にし、build の coordinator と入口の両方がそれを呼ぶ（集め方が 2 つに分かれると指紋が食い違う）。指紋は次の file の相対 path と SHA-256 の列の digest（D3 と同じ書式。空の folder は入れない）:
  - `ProjectSettings/` の下（folder も再帰する）の file のうち、Player に入らない物（`EditorSettings`、`EditorBuildSettings`、`PackageManagerSettings`、`VersionControlSettings`、`PresetManager` 等。一覧は B が Unity の版の `ProjectSettings/` から決めて §6 に書き、「Player に入らない」と言えない物は入れる側に倒す）を除いた物。`ProjectVersion.txt` は入れる。checkout をまたぐ AC1 か、同じ checkout の AC17 で不一致が出た file は、Player に入らないと言える物（Editor が生成する設定など）だけを除外に足す（B 適応）。言えない物で不一致が出たら Phase A へ戻す。
  - `Packages/manifest.json` と `Packages/packages-lock.json`（package の版。package の中の file は入れない。`Library/PackageCache` の path は checkout ごとに違うため）。
  - render pipeline の asset: `GraphicsSettings` の既定の render pipeline（`m_CustomRenderPipeline`。今は `Assets/Settings/PC_RPAsset.asset`）、`QualitySettings` の全ての level の render pipeline（今は PC の `PC_RPAsset.asset` と Mobile の `Assets/Settings/Mobile_RPAsset.asset`。Standalone を除外する level も入れる側に倒す）、`GraphicsSettings` の render pipeline ごとの global settings の表（`m_RenderPipelineGlobalSettingsMap`。今は `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`）の 3 つを起点にし、それらの依存の閉包（`AssetDatabase.GetDependencies`）のうち `Assets/` の下の file と、各 file の隣の `.meta`（import の設定）。起点を取る API は B が選ぶが、参照を名前で探さない（表は guid で指す）。
  - `Assets/` の下の `Resources` という名前の folder の中の file（mutation が書く `Resources/BS4/` を除く）、`Assets/StreamingAssets/` の file（mutation が書く `bs4-runtime.json` を除く）。
  - 遊べる Player の開始 Scene（`Assets/SampleGame/DependOnAll/PlayerStart/PlayablePlayerStart.unity` とその `.meta`）。
  - 入口は違えば違う file と「遊べる Player を build し直す。Unity が書き換えただけの file（新しい checkout の最初の起動の URP の global settings など）なら元へ戻す」案内で止める（`PlayerInputDiffers`）。設定を変えて上書きを作り、古い設定の exe で遊ぶことになるので、止める方が親切、が owner の判断である。
- **D12 `link.xml` は足さない:** 遊べる Player の構成は S4-0・S4-1・S4-1a・G0 と同じ IL2CPP・High のまま。G0 (a) の答えを製品の構成の答えとして使う。
- **D13 読んだ map を log で見分ける:** 起動の log の map の行に、読んだ map の `GenerateHash` を足す（`[AppInit] Bootstrap SceneResourceMap: source=<値> hash=<GenerateHash>`。3 つの読み口のどれでも出す。行の先頭と `source=` の値は変えない）。上書きの map が読まれたことを Player の log だけで判定するための 1 行で、Runtime の判断は増やさない。

**不変条件（起きてはいけないこと）:**

- **I1 上書きの指定がある起動を base だけで続けない。** 上書きの key が 1 つでもある Player の起動は、上書きを全部重ねて動くか、`BeforeSceneLoad`・`AfterSceneLoad` の起動の失敗（Player の log に理由、exit 1、Title を出さない）かのどちらかになる。上書きの一部が Player に無い型で欠けることは D4 の決定で受け入れ、I1 の違反に数えない。
  - 主張を崩しうる経路: `ContentOverlayStartup.Decide` の分岐、`ReadContentDirectoryConfiguration` の Player の分岐が overlay を落とす経路（今の `:1248`）、`InstallConfiguredContentDirectoryAsync` が Overlay の null で base だけを登録する経路、`RegisterVerifiedWithOverlay` の巻き戻しの後に起動が続く経路（例外を握る `catch`）、SampleGame の失敗の override（`AppInitializer.OnPlayerContentStartupFailedAsync`）が遊べる Player を終わらせない経路、digest の key だけ欠けた起動、digest の key だけがある起動（Decide の最初の `None`）、Editor の環境変数の継承で古い key が Player に渡る経路（D6 の (6) で塞ぐ）、`ProjectSettings` などの Player の入力だけの編集が「content の編集なし」で base だけの起動になる経路（D11 で塞ぐ）、`BootstrapSourcePolicy.Plan` が登録を後ろへ送る種類（`smoke`）で上書きが通る経路（D2 で `playable` だけに限る）。
  - 探索した境界: 上の file と `ContentDirectorySession.RegisterVerifiedWithOverlay` の入口まで。`ContentDirectoryLayers` の中は S3-2 の検証に依る。このスライスの失敗（Decide の拒否、digest の不一致、base の記録の不一致）は全て UICommon と map を読む前に起きるので、S4-1a が既知の境界とした「UICommon と map を読んだ後の失敗の後始末」（S4-1a から移送した (5)）には当たらない。
  - 未確認: IL2CPP・High で上書きの登録の経路の型が strip されて途中で例外の形が変わること（u2）。
- **I2 動作確認の Player の契約を変えない。** `smoke` の Player は上書きの key があれば今と同じく `before-scene-load` の失敗の receipt と exit 1、key が無ければ今と同じ receipt（10 段）と exit 0。
  - 経路: Decide に足す種類の引数が `smoke` を `Overlay` にする経路、coordinator の `player-compat.json` の書き込みが `smoke` の出力と `build-receipt.json` を変える経路、D13 の log の変更が receipt を変える経路（receipt は log を読まない）。
  - 探索した境界: `AbstractApplicationInitializer` の `BeforeSceneLoad` の順と、S4-1a の latch（開始 Scene の名前）と `BootstrapSourcePolicy`。
- **I3 protected config を弱めない。** 上書きの key・digest の key は baked の 10 の値を変えられず、installed の pair の照合（同じ build identity）を迂回できない。
  - 経路: key が `content:` の名前空間に入る経路（大小文字・`.` と `:` の同一視）、Decide が Resolve より前に `Overlay` を返して Resolve を飛ばす経路、上書きの path に installed の base の path を渡す経路（gate の層の重複の拒否）。
- **I4 照合を通らない上書きを Player に読ませない。** 上書きの directory の書き換え（digest の不一致）と上書きの key の欠け・空は、native の登録より前に止まる。base の記録の不一致は、今の登録契約（13 章）のとおり、native の登録の後・load を受け付ける前に上書き → base の順に巻き戻して起動の失敗にする（このスライスは層の中の順を変えない）。どの場合も、上書きの中身は 1 つも load されない。
  - 経路: digest を取る時点と native の登録の間の書き換え（窓は残る。受け入れる。§2.3）、digest が content の folder の外の file・空の folder・大小文字の違う名前をどう数えるか、symlink・reparse point。
- **I5:** 欠番（型の照合の不変条件だった。D4）。
- **I6 入口は base・installed cache・Player の出力・source・既存の上書きの directory・Delivery の ledger を書き換えない。** 書くのは新しい上書きの artifacts と入口の記録の下だけ。installed cache は revision の tree を対象にし、cache が自分で作る制御の file（`control/` の lock）は除く。観測の時点は、入口が Player の process を作る直前まで（process を作らずに止まる時は入口が戻る時点まで）とし、Player が自分で書く `OneStarMakerLocal/` は除く。未保存の Scene・asset の保存は利用者が確認で認めた時だけ行う。
- **I7 遊べる Player の既定の起動を変えない。** installed の pair も上書きの key も無い遊べる Player の起動は、S4-1a の後の起動と同じく同梱の directory から Title まで進み、窓を閉じて exit 0 で終わる（`player-compat.json` を加えた Player で）。
  - 経路: Decide の変更が key の無い起動を `Rejected` にする経路、`player-compat.json` の書き込みが Player の出力を変える経路。

**成立条件（起きなければならないこと）:**

- **E1** 別の checkout（別の `Library`）で build した遊べる Player と base（installed）に、手元の Editor から入口で上書きを重ねて起動すると、上書きの登録の 1 行が出て、編集した Scene と asset（Title の Scene、UICommon、SceneResourceMap を含む）が上書きの版で読まれ、Title から操作を続けられ、窓を閉じて exit 0 で終わる。
- **E2** I4 の各拒否と入口の各停止（`NoPlayablePlayer`・`BaseNotInstalled`・`BaseInvalid`・`PlayerInputDiffers`・`OverlayChangedAfterBuild`）で、利用者は理由（どの照合か、違う欄・file）を、入口なら Console と dialog で、Player なら Player の log の 1 行で読める。

### 2.2 受け入れ条件

全ての Player の起動は窓を出した通常の起動（`-batchmode` なし）。制限の 120 秒は、起動から初回 Scene の行（成功の AC）か process の終了（失敗の AC）までの時間で、D10 の `TimedOut` と同じ値である。その後の待ち（AC1 の「スタート」から Home までの 180 秒、閉じる要求から終了までの 120 秒）は別に数える。合格例: 初回 Scene の行が 100 秒で出て、Home まで更に 150 秒かかる AC1。不合格例: 初回 Scene の行が 150 秒で出る起動。判定は Player の log（`-logFile`）・game の log・exit code・入口の `last-attempt.json`（`outcome` と `startup`）と、AC1・AC12 だけ画像で行う。「Player の log の Error 以上の行が 0」は、`Shader 'Hidden/Universal Render Pipeline/` で始まり `is not supported or has been stripped from the build` を含む行（S4-1 から移送した (8)）だけを除いて数える。`未登録のシーンをスキップ:` の行は Error ではない（S4-1a から移送した (6)）。**game の log は case ごとに分ける:** 入口は同じ `players/<identity>/` の exe を何度も起動し、game の log はその folder の `OneStarMakerLocal/` に溜まるので、判定の test は Player を起動する前に `OneStarMakerLocal/` が無いことを確かめ、Player が終わった後に case の証拠の folder へ移す（S4-1a の test と同じ規則。動かすのは test で、入口ではない）。game の log の行の有無（Title の `Loaded.` が無い、Error が 0）は、その case の起動が書いた file だけで読む。AC13 だけは例外で、game の log を合否に使わない。

| AC | 内容 | 検証する条件 |
|---|---|---|
| AC1 | 成功: 初期状態は、checkout X で build した遊べる Player の folder（`players/<id>`）と transport を checkout Y の `artifacts/playable/` に写し（CI からの取得の代わり）、Y で transport を install した状態。Y の Unity を起動する前に、Title の Scene が読む UI の label の文字列（content の root の閉包にある asset）を token に変え、加えて Title の Scene の file そのものに保存された値で画面に出る物（具体の対象は B が Title の Scene の構成から選び、patch に固定する）を第 2 の token に変えておき、入口を実行する。`outcome` が `Launched`、`startup` が `Observed`、`last-attempt.json` の上書きの root に Title の Scene の root がある。Player の log に、`Content directory registered:` の行（`from=installed`、identity が Player の identity）と `RegisteredLogMarker` の行（identity が上書きの identity、baseRevision が Player の identity）、`Bootstrap UICommon: source=content`、`Bootstrap SceneResourceMap: source=content`。game の log に Title の `Loaded.`。判定の test が Title の窓の画像を file に取る（u6）。続けて判定の実行担当が「スタート」を 1 回押し（S4-1a の AC6 と同じ手段。画面の取り込みと OS の mouse の入力）、game の log に `OnStabledImpl HomeScene`。2 つの token が画像に見えるかは判定の担当が画像で判定する。game の log の Error 以上の行が 0（`<unparsed>` の行も 0）。Player の log の Error 以上の行が 0。窓を閉じて exit 0。 | E1、I1、D5 |
| AC2 | `smoke` の Player に上書きの 3 key を環境変数で渡して起動 → receipt の `succeeded=false`・stage `before-scene-load`・exit 1。key なしの起動は S4-1a の後と同じ receipt（10 段）と exit 0。加えて record に、`smoke` の Player の出力に `player-compat.json` が無いこと、`smoke` と遊べる Player の build の後で mutation の root（`Assets/Resources/BS4/<identity>/`）が残っていないこと。 | I2 |
| AC3 | 遊べる Player に、記録が違う base を指す上書きを渡す。作り方: 判定の test が、同じ checkout で `spring-full` をもう 1 回 build・publish・install した別の identity の base に対して、上書きの port を直に呼んで上書きを作り、その上書きの正しい digest と Player の base の pair と一緒に環境変数で Player へ渡す。照合の順は digest → base の記録なので、digest は通って base の記録で落ちる → Player の log に今の message `The overlay base record does not match the registered base` と違う欄の名前、exit 1、game の log に Title の `Loaded.` が無く、Player の log に `Bootstrap UICommon:` の行が無い（UICommon より前に落ちる）。 | I1、I4、E2 |
| AC4 | (a) 入口の port で、build の時の digest を記録した後・起動の直前の取り直しの前に上書きの 1 file を書き換えて差し込む単体テスト → `outcome` が `OverlayChangedAfterBuild`、process を作らない（取り直しの後の書き換えは AC18 (b) で、Player が止める）。(b) 入口が作った上書きの 1 file を書き換え、古い digest のまま環境変数で直に起動 → Player の log に D10 の digest の不一致の reason、exit 1、Title が無い。(c) digest の key を欠いた起動 → Player の log に D10 の「digest が要る」の reason、exit 1。(d) digest の key だけの起動（遊べる Player）→ Player の log に今の reason `Content overlay needs both`（Decide の key の欠け）を含む行、exit 1。(e) 再利用の正と負の対照を、同じ編集・同じ base・同じ Player のまま 1 つの列で見る（他の case の patch や壊した上書きに依らない）: (i) 入口を実行 → `Built`。(ii) Player を閉じ、編集を変えずに別の Unity process で入口を実行 → 再利用の段の結果が `Reused`、上書きの identity と digest が (i) と同じ、`startup` が `Observed`。(iii) その上書きの content に file を 1 つ足してから、同じ編集で入口を実行 → `Built`、identity が (i) と違う、`outcome` が `Launched`、`startup` が `Observed`（D8。常に build し直す実装は (ii) で落ちる）。 | I1、I4、E2 |
| AC5 | script の差では止めない（D4）: Title の閉包の prefab が使う既存の SampleGame の script に field を 1 つ足して値を入れ、入口を実行 → `outcome` が `Launched`、`startup` が `Observed`、Player は build の時の script で動き、game の log の Error 以上の行が 0、Player の log の Error 以上の行が 0（field の値は反映されない）。このスライスが判定で保証するのは field を足す場合だけである。field を消す・型を変える場合は Phase A の実測（G0 (d)）の記録で、C・C' の判定の根拠にしない。 | D4 |
| AC6 | 欠番（型の照合の AC だった。D4）。 | — |
| AC7 | 遊べる Player を installed の pair なし（同梱の directory）で、上書きの 3 key つきで起動 → Player の log に今の reason `Content overlay can only be layered on a verified installed revision` を含む行、exit 1。 | I1、I3 |
| AC8 | (a) 編集の無い Y で入口を実行 → `outcome` が `LaunchedWithoutOverlay`、`startup` が `Observed`、上書きの key なしで起動し、Console に「上書きなし（content の編集なし）」の 1 行、Player の log に `Content directory registered:` の行（`from=installed`）と `Bootstrap UICommon: source=content`・`Bootstrap SceneResourceMap: source=content`、上書きの登録の行が無く、game の log に Title の `Loaded.`、窓を閉じて exit 0（installed の pair からの成功の起動の初めての確認。u9）。(b) Editor の process の環境に、古い上書きの 3 key と、Editor の Play が残しうる `content:runtimeMode`・`content:firstScene`・`content:playerKind` の環境変数を置いた状態で (a) → 結果は (a) と同じで、`last-attempt.json` の子 process の環境の `SAMPLEGAME_*` の名前が installed の pair の 2 つだけ（test が自分で環境を掃除して Player を直に起動する形にしない。入口が組み立てた環境で起動する）。(c) 作り直せない root だけの計画（`UnbuildableOnly`。判定の test が Y の source から root の file を退かして作る）→ Editor の入口と同じ通知を出し、(a) と同じく起動する。(d) Y で transport を install する前に入口を実行 → `outcome` が `BaseNotInstalled`、`startup` が `NotStarted`、`detail` に install の案内、Player の process を作らない。(e) (a) の前後で Delivery の ledger（`SessionState` の key）と Editor の process の環境の `SAMPLEGAME_*` が変わらない。 | E1（縮退との区別）、I1、I6、E2 |
| AC9 | 単体テスト: `ContentOverlayStartup.Decide` の表（Player の種類 × pair × mode × 3 key の有無。digest だけを Player と Editor の両方で含む）、`ContentOverlayDigest`（順・大小文字・空 folder・content の外の file・reparse point）、D11 の Player の入力の指紋の集合と比較（`PlayerCompatibility`。開始 Scene・package の 2 file・`Resources/BS4/` の除外を含む）、`player-compat.json` の読み書き、D6 の (1)(2) の Player と base の決定（Player なし、compat なし、2 つの Player、install なし、digest の不一致、宣言の不一致）、D10 の `startup` の遷移（`Step`: `Observed`・`Failed`・`TimedOut`、上書きつきで登録の行が無い、process が無い時の読み直し）、D8 の再利用の包み（記録があり digest が同じ時は既存の再利用の結果をそのまま返す、記録が無い・壊れている・digest が違う時に null）、入口の port の test（指紋の照合の前に保存が走る順: 保存の port が指紋に入る file を書き換える → `PlayerInputDiffers` で、`Prepare` と process の起動は 0 回）。加えて `PlayerInputFiles` の EditMode の test: この project で集めた集合に `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`、`Assets/Settings/PC_RPAsset.asset`、`Assets/Settings/Mobile_RPAsset.asset`、それぞれの `.meta`、開始 Scene、`ProjectSettings/ProjectVersion.txt`、package の 2 file が入っている（build と入口が同じ口を使うので、集め方の抜けは AC17 では見えない）。 | I1〜I4、D6、D8、D10、D11 |
| AC10 | AC1・AC8 (a)・AC8 (d) の case で測る。入口の実行の開始から Player の process を作る直前まで（AC8 (d) は入口が戻る時点まで）に、base の installed の revision の tree、`artifacts/playable/players/<identity>/`（`OneStarMakerLocal/` を除く）、既にある上書きの directory、`git status --porcelain`（新しい上書きの artifacts と入口の記録、S4-1 から移送した (6) の空の folder を除く）が変わらない。 | I6 |
| AC11 | AC1 の X と Y が別の `Library` を持つことを record に残す（両方の `Library` の path と、X の build の Unity の版と Y の上書きの build の Unity の版）。 | E1（別の `Library` の base） |
| AC12 | UICommon の編集が Player で効く: UICommon の Scene が読む UXML（`OneStarMaker/Scenes/UISystem/UISystemDocument.uxml`。UICommon の root の閉包にある）に、画面に見える token の label を足して入口を実行 → `outcome` が `Launched`、`startup` が `Observed`、`last-attempt.json` の上書きの root に UICommon の root があり、Title の画像に token が見える（判定の担当が画像で判定する）。対照: AC1 の画像にはこの token が無い。 | E1、I1（S4-1a の効き） |
| AC13 | 入口で上書きつきの遊べる Player A を起動し、A の `startup` が `Observed` になった後、閉じずに上書きに入る asset を編集してもう一度入口を実行 → `outcome` が `Launched`、`startup` が `Observed` で、新しい identity の上書きで Player B が起動する（B の Player の log に B の上書きの identity の登録の行）。A の上書きの directory の file の一覧と hash は A の起動の時から変わらない。A と B を閉じてどちらも exit 0。この AC は game の log を合否に使わない（2 つの process が同じ file に書き、行が混ざる。§1 の u8）。 | I6、D8、D9 |
| AC14 | S4-1a の後の `PlayerKindEndToEndTests.LaunchPlayable`（installed の pair も上書きの key も無い既定の起動）を、このスライスの遊べる Player（compat を含む）で通す: Title の `Loaded.` の後に `WM_CLOSE` で exit 0。InGame までは見ない（S4-1 の自動の範囲と同じ）。 | I7 |
| AC15 | Y の `ProjectSettings/TagManager.asset` に tag を 1 つ足して（content の閉包には無い）入口を実行 → `outcome` が `PlayerInputDiffers`、`detail` がその file を示し、Player の process を作らない。 | I1、E2、D11 |
| AC16 | SceneResourceMap の編集が Player で効く: map の asset の `_generateHash` の値を token に変えて入口を実行 → `outcome` が `Launched`、`startup` が `Observed`、`last-attempt.json` の上書きの root に map の root があり、Player の log に `Bootstrap SceneResourceMap: source=content hash=<token>`。対照: AC1 と AC8 (a) の同じ行の `hash=` は token ではない（base の map の値）。上書きの map から `SceneResource` への参照の先がどちらの層かは見ない（§2.3）。 | E1、I1（S4-1a の効き）、D13 |
| AC17 | 自分で build した checkout でそのまま入口を使える: X で `BuildPlayable` が返った直後（同じ Unity process）と、その後の新しい Unity process の 2 つの時点で、`PlayerInputFiles` で集めた X の手元の指紋と、X の Player の `player-compat.json` を比べる → どちらの時点も差分が 0 件（build の mutation と戻しが、入口を `PlayerInputDiffers` で止めない）。 | D11、E1（UC1 から UC2 へ） |
| AC18 | 製品の観測の経路が動く: test が `Step` も「1 回観測する」口も呼ばず、Editor の main loop（`EditorApplication.update`）に観測を進めさせる case（frame を譲る `[UnityTest]`。待つ間、test は `last-attempt.json` を外から読むだけ）。driver は AC1 と同じ patch を当てる（上書きつきの起動にする。上書きは AC1 の物の再利用でよい）。(a) 成功の起動 → `startup` が `Pending` から `Observed` に変わる。(b) (a) の Player を閉じた後にもう一度入口を呼び、D6 の (5) の `BeforeProcessStart` の port で、その起動が使う上書きの directory の 1 file を書き換える（入口の取り直しは通っているので入口は止めず、process を作る）→ Player が digest の不一致で exit 1 で終わり、`startup` が `Failed`、`startupReason` に D10 の digest の不一致の reason、Editor の Console（Unity の log）に同じ理由の Error の行。domain reload をまたぐ観測の読み直しは、この AC では測らない（§2.3）。(b) が書き換えた上書きの directory は、後の case では D8 の記録との不一致で再利用されない。 | D10、E2、I1、I4 |

### 2.3 ここでは答えない問いと所有する後続

- 上書きが使う型のうち Player に無い物を一覧で見る手段: Editor の専用の debug menu。起動は止めない。owner の仮の置き場は S5-1（`ScriptsOnlyCache.yaml` の読みはこの menu の中に閉じる。G0 (b) が材料を確かめた）。上書きで試せる型の範囲を広げること（`link.xml`、Player の `strippingInfo`）も同じ所へ。
- 別の PC で build した base（S4-3）。base の選び方（「自分の base」、同じ目的の複数 revision、複数の Player から選ぶ UI）と CI からの取得、取得した transport の install を入口が代わりに行うこと（S4-3）。
- D11 の指紋は bytes の SHA-256 で、`.gitattributes` は `ProjectSettings/*.asset` などの改行を固定していないので、`core.autocrlf` の違う checkout（CI）で build した Player とは不一致になりうる（同じ PC の checkout では出ない）。改行の正規化は S4-3（CI の Player を取る時）へ送る。
- CI の Player より新しい commit を pull した時に、他人の script の変更が Player に無いことの案内: 作らない（D4 の owner の決定「案内もしない」）。
- 上書きの map から `SceneResource` への参照が上書きの側の `SceneResource` を指すことの観測: このスライスでは見ない（S4-1a から移送した (1) の後半）。理由は 2 つ。同じ仕組み（上書きの root から、その閉包の同じ id の物への参照が上書きの版になる）は、S4-0 と G0 (a) が同じ構成の Player で測り（「両方の root の id から v2」）、製品の口では AC1 の第 1 の token（Title の root の閉包の asset）が測る。`SceneResource` の値で遊びの log に出る物が無く、観測には Runtime の log か大きな patch が要る。map の構成を変える編集（Scene の追加）で食い違いが出たら、所有の無い入力として program §10 の U11 に足す。
- 観測の途中の domain reload と Editor の再起動をまたぐ読み直し（D10）を実際の reload で確かめること: このスライスでは測らない。読み直しの規則は AC9 の単体テスト（process が無い時、記録が `Pending` の時）で守り、製品の update の経路は AC18 が測る。reload をまたぐ実機の確認は、所有の無い入力として program §10 の U11 に足す。
- 保存の確認の検出に掛からない未保存の変更（`ProjectSettings` の、まだ disk に書かれていない変更など。既存の `EnumerateUnsavedAssets` の既知の限界）は、指紋の照合も見ない: このスライスでは塞がない。所有の無い入力として program §10 の U11 に足す。
- 古い上書きの directory が `artifacts/bs2b` に溜まること: 消す規則は作らない。判定の driver も消さない（判定の checkout X・Y ごと捨てる）。所有の無い入力として program §10 の U11 に足す。
- Player での要求時解決の配線と record（Player の観測を record で揃えるか）: 所有の無い入力として program §10 の U11 に足す。
- 2 つの Player を同時に動かした時の game の log の行の混ざり: 直さない（既知の制約として README の「試す」に書く）。process ごとに file を分けるかは U11 に足す。
- digest の時点から native の登録までの間の書き換え（I4 の窓）: 開発用の Player では受け入れる。閉じる必要が出たら S4-3 の cache の lease と一緒に扱う。
- 上書きの登録の順・個数・時点の契約を Player で変えること、上書きを 2 つ以上: 13 章のまま。
- script の差を吸収する（Player を作り直さずに script の変更を確かめる）: 対象外。
- S2-0b の「同じ file を持つ 2 つの directory」の結論を Player で測ること: 測らない。上書きの契約は id の違う物の 2 実体と同じ id の物の影で決まり、S4-0 が Player で同じ答えを得た。同じ bytes の 2 実体は開発の PC の重複として受け入れ済み（program §5.3 の当面の方針）。
- S4-1 から移送した (3)（設定を Addressables から読む Player の strip）: 遊べる Player は設定を Addressables から読まないので当たらない。S4-1 から移送した (6)〜(8) と S4-1a から移送した (6)〜(8): U11 のまま。判定の担当への注記として §5 に書く。
- Android・iOS（S4-5）。

### 2.4 スライス分割の判断

契機: 高リスクの初回実機スライスで、独立して失敗しうる未知がある（製品の口の Player での成立 u1〜u6・u9〜u12）。

判断: **分けない。** 起動の順は S4-1a に分けて済んだ（2026-11-24、owner の決定。PR #213）。残りは 4 つ（Runtime の口と digest、Editor の入口、Player の入力の指紋、判定の test と driver）で、program の S4-2 の行の問いに 1 つずつ要り、どれを外しても問いに答えられない（外部計画レビュー 2 巡目・4 巡目・レビュー入力の同意）。G0 (a)(b)(d) と u8 は凍結の前に測り終えた。残る u は全て判定の case で確かめられ、発見 C の最初の限定実行（§5）が、上書きの前の経路（installed の pair からの起動）と上書きの経路を分けて先に通す。

### 2.5 A3 の決定

答えが決まる点は担当が決めた（覆す時だけ改める）。owner の決定は日付つきで書いた。owner は 2026-11-24 に凍結を裁定した（§0）。

1. **G0:** (a)「同じ」、(b) 材料あり、(d) は §1 の現況のとおり。
2. **digest:** Player では必須、Editor の Play では key があれば照合する（Editor の入口と record を変えない）。
3. **照合の場所:** 入口と Runtime の両方で「入れた物が信用できるか」だけを見る。script と型は照合しない（D4、owner の決定 2026-11-24）。Runtime を重くしない（owner の決定 2026-11-24）。
4. **Framework の public の面:** `ContentOverlayPlayConfig` に `OverlayDigestKey`、digest の static method、初回 Scene の行の marker（`PlayerFirstSceneLogMarker`）を足す（S3-4b と同じ形）。Runtime の log の map の行に `hash=` を足す（D13）。
5. **digest から登録までの窓:** 開発用の Player では受け入れる（§2.3）。
6. **`link.xml` を足さない**（D12）。
7. **base の決め方:** Delivery の ledger をやめ、Player の identity から決める（D6 の (1)(2)。レビュー入力の 3.1）。外部 1 巡目の X1 が ledger を勧めた理由（最新の install へ黙って縮退しない）は、Player が指す 1 つだけを使う形で満たす。前の版の D7（ledger の読み口）、`NoBaseSelected`・`BaseMismatch`、`ContentDeliveryPlayBridge` の変更は外した。
8. **AC1 の「スタート」の押下:** 判定の実行担当が S4-1a の AC6 と同じ手段で行う。owner の作業にしない（前の版の owner の確認 (B) は要らなくなった）。高コスト証拠は無い。
9. **`ProjectSettings`・`Resources`・`StreamingAssets` の編集:** 止める（D11、owner の決定 2026-11-24）。開始 Scene と package の版も指紋に入れる。
10. **G0 の再現性（外部 2 巡目の R4）:** spike の branch は作らない。G0 (a)(d) の harness の差分（`b101b382` に当てる patch）と record を 1 つの Evidence として Artifact Storage に置く（機械向けのデータを git に入れない。前の版の owner の確認 (C) の push は要らなくなった）。状態: ローカル固定（未保存）。patch と record は Y の worktree に揃えた（§1 の現況）。保存（stage と昇格）は、資格情報の利用の許可を得てから担当が行う。保存は凍結の条件にしない。G0 は Phase A の gate の記録で、C・C' の入力ではない。C・C' が判定する script の差は AC5（field を足す）だけで、field を消す・型を変える場合の G0 (d) の結果は判定の根拠にしない（外部 6 巡目の Z4）。
11. **map から `SceneResource` への参照:** 見ない範囲にした（§2.3）。
12. **2 つの Player の同時の起動:** 止めない（D9）。game の log の混ざりは既知の制約にした。

## 3. 責務マップ

| file | 責務（変更理由） | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 現在行数 → 予想 |
|---|---|---|---|---|---|---|
| `OneStarMaker/Scripts/Runtime/Bootstrap/ContentOverlayStartup.cs` | 上書きを重ねるかの判定（Player の種類の引数と digest の key を足す。決定に digest を持たせる） | 状態なし | なし | internal | `ContentOverlayStartupTests`（表） | 91 → 130 |
| `OneStarMaker/Scripts/Runtime/BuildContent/ContentOverlayDigest.cs`（新規） | 上書きの directory の file の一覧と SHA-256 から digest を作る（算法の正本） | 状態なし | `System.IO`、`System.Security.Cryptography` | internal（Editor へは `ContentOverlayPlayConfig` の public の口から） | `ContentOverlayDigestTests`（一時 folder） | 0 → 70 |
| `OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs` | 起動の配線（Player の分岐で overlay を渡す、登録の前に digest を比べる、map の log に `hash=`、初回 Scene の行の先頭を internal の定数にする） | App | 上の 2 つ | 変えない | 既存の起動のテストと AC1〜AC4・AC7・AC16 | 1457 → 1482。500 行を超えるが、起動の orchestration の 1 責務で、足すのは分岐の配線と log の 1 欄だけ（新しい判断は上の 2 file に置く）。登録の位置（`BootstrapSourcePolicy`）は変えない。非分割 |
| `OneStarMaker/Scripts/Editor/ContentOverlay/ContentOverlayPlayConfig.cs` | key・算法・marker の public の写し、remarks の更新 | 状態なし | Runtime | public（`OverlayDigestKey`、`ComputeOverlayDigest`、`PlayerFirstSceneLogMarker`） | AC9 | 26 → 46 |
| `SampleGame/DependOnAll/Editor/Build/PlayerCompatibility.cs`（新規） | D11 の Player の入力の指紋の集合の規則（除外・並べ方）と記録の形と、手元との比較（policy。file の一覧は引数で受ける） | 状態なし | なし（文字列の列だけ） | internal | `PlayerCompatibilityTests` | 0 → 90 |
| `SampleGame/DependOnAll/Editor/Build/PlayerInputFiles.cs`（新規） | D11 の file を集める唯一の口（`ProjectSettings/` の列挙、render pipeline の asset の依存の閉包、`Resources`・`StreamingAssets`、開始 Scene、package の 2 file と各 file の hash）。規則は `PlayerCompatibility` に置き、ここは Unity と disk の I/O だけ | 状態なし | `PlayerCompatibility`、`AssetDatabase` | internal | AC17 と判定の build（単体テストは規則の側） | 0 → 70 |
| `SampleGame/DependOnAll/Editor/Build/PlayEntryOverlay.cs` | 計画の要約（`PlayEntryOverlayPlanSummary`）と結果に、上書きに入れた root の logical key の列を足す（D10。Editor の入口の挙動は変えない） | 既存 | 既存 | internal | 既存のテスト | 231 → 240 |
| `SampleGame/DependOnAll/Editor/Build/PlayEntryInterceptor.cs` | `EnumerateUnsavedAssets` の可視性を internal にする（D6 の (3)。中身と Editor の入口の挙動は変えない） | 既存 | 既存 | internal | 既存のテスト | 行数は変わらない |
| `SampleGame/DependOnAll/Editor/Build/SampleGamePlayerBuildCoordinator.cs` | 遊べる Player の build（mutation の前に `PlayerInputFiles` で指紋を取り、`playerWork` の中で publish の前に `player-compat.json` を書く段を足す。`smoke` には書かない） | Editor の操作 | `PlayerCompatibility`、`PlayerInputFiles` | menu は変えない | AC9、判定の build | 337 → 375 |
| `SampleGame/DependOnAll/Editor/Build/PlayerBuildPublisher.cs` | 変えない（staging の中身を原子的に置く。compat は `playerWork` に入れてから呼ぶ） | — | — | — | — | 変更なし |
| `SampleGame/DependOnAll/Editor/Build/PlayEntryCandidates.cs` | installed cache から revision を選んで検証する（既存）。revision と manifest の digest を指定して 1 件を検証する口を足す（既存の `TryValidate` を使う。D6 の (2)） | 状態なし | 既存 | internal | 既存のテストと AC9 | 106 → 130 |
| `SampleGame/DependOnAll/Editor/Build/SampleGameContentBuild.cs` | `OverlayPorts` が上書きの contentSet を引数で受け（D8。Editor の入口は今の値を渡す）、計画の要約に root の logical key の列を入れる | 既存 | 既存 | internal | 既存のテストと AC4 (e) | 234 → 248 |
| `SampleGame/DependOnAll/Editor/Build/PlayablePlayerLocator.cs`（新規） | D6 の (1): `players/*/build-receipt.json` の一覧から Player を 1 つ決める規則（最大の identity、compat の有無。policy は receipt の値の列だけで決め、file の読みは薄い口に分ける） | 状態なし | なし | internal | AC9（Player なし・compat なし・2 つ） | 0 → 70 |
| `SampleGame/DependOnAll/Editor/Build/PlayerOverlayAttemptRecord.cs`（新規） | 入口の 2 つの記録（`overlay-record.json`・`last-attempt.json`）の形と読み書き、`outcome` と `startup` の enum | 記録の file（`artifacts/playable/overlays/`） | なし | internal | AC9 | 0 → 90 |
| `SampleGame/DependOnAll/Editor/Build/PlayerOverlayStartupObserver.cs`（新規） | D10 の観測: 1 回分の関数 `Step`（Unity を呼ばない）と、それを `EditorApplication.update` と `[InitializeOnLoad]` から呼ぶ薄い pump（process の生死と log の読み） | Editor の domain。状態は `last-attempt.json` だけ | 上の記録 | internal（`Step` と「1 回観測する」口を test が呼ぶ） | AC9（遷移の表）、判定の test の待ち | 0 → 120 |
| `SampleGame/DependOnAll/Editor/Build/PlayerOverlayLaunch.cs`（新規） | 入口の orchestration（D6 の (1)〜(7) の順と停止、D8 の再利用の包み、環境変数の組み立て、process の起動、test 用の空の port `BeforeProcessStart`）。規則は上の 4 file と既存の port に置き、ここは順だけを持つ | Editor の操作 1 回 | `PlayablePlayerLocator`、`PlayEntryCandidates`、`PlayerCompatibility`、`PlayerInputFiles`、`PlayEntryOverlay`、`SampleGameContentBuild`、`ContentOverlayPlayConfig`、記録、observer | menu 1 つ | port を差し替えた単体テスト（起動しない。AC4 (a)） | 0 → 200 |
| `tools/s42-player-overlay.ps1`（新規、判定の driver） | 判定の process の列（§5）: X の build、Y への写し、case ごとの編集の適用・`run-tests.ps1 -Filter <case>$`・戻し・`git status` の確認 | 判定の 1 回 | `run-tests.ps1`、git | なし（Phase C の実行担当が使う） | dry run（編集の適用と戻しだけ） | 0 → 200。Phase D で残すかを決める |
| `SampleGame/Tests/Editor/Build/PlayerOverlayEdits/*.patch`（新規、判定の入力） | AC1・AC5・AC12・AC13・AC15・AC16 の編集を Unity の外で当てる patch（Title の label の token と Title の Scene の値の token、`.cs` の field、UICommon の UXML の label、map の `_generateHash`、`ProjectSettings/TagManager.asset` の tag）。file 名は path の予算（150 字）に収める | 恒久の fixture（実装から導ける） | なし | なし | driver の dry run | 新規 7 file |
| `SampleGame/Tests/Editor/Build/PlayerProcessRun.cs`（新規、test の補助） | 起動した Player の 1 回の観測（log を読み直して待つ、exit を待つ、`WM_CLOSE` を送る、game の log を読む）。今は `PlayerKindEndToEndTests` の private の入れ子 class（`PlayerRun` と game の log の読み）で、2 つの判定の test が使えるように file へ出す（移すだけで中身は変えない。process は呼び手が渡す） | test | なし | test | 2 つの判定の test | 0 → 190（`PlayerKindEndToEndTests.cs` は 579 → 400） |
| `SampleGame/Tests/Editor/Build/PlayerOverlayEndToEndTests.cs`（新規、`[Explicit]`、namespace `SampleGame.Tests.Editor.Build`） | 判定の Player の build・install・入口・起動・観測（同期の `[Test]` で、入口が返した process を `PlayerProcessRun` に渡し、待ちの中で observer の「1 回観測する」口を呼ぶ。AC18 だけは frame を譲る `[UnityTest]` で、observer を呼ばない） | test | 上の全て | test | — | 0 → 480（判定の case の列。1 責務の宣言的な列で非分割） |

単体テスト（`PlayerCompatibilityTests`、`PlayablePlayerLocatorTests`、`PlayerOverlayStartupObserverTests`、入口の port の test）は `SampleGame/Tests/Editor/` に置く（`OneStarMaker.Tests.Editor` は SampleGame を参照しない）。`ContentOverlayStartupTests` と `ContentOverlayDigestTests` は Framework の `OneStarMaker.Tests.Editor`（今の `ContentOverlayStartupTests` と同じ assembly）に置く。

namespace・asmdef は既存の file と同じ（Runtime は `OneStarMaker.Runtime`／`.BuildContent`、SampleGame の Editor は既存の Editor の asmdef）。新しい asmdef 参照は足さない。`ContentDeliveryPlayBridge` は変えない。

## 4. 実装計画

- 順序: (1) `ContentOverlayDigest` とテスト。(2) Decide の表の変更とテスト、`ContentOverlayPlayConfig`。(3) 起動の配線と map の log。(4) `PlayerCompatibility` とテスト、`PlayerInputFiles`、coordinator の `player-compat.json`。(5) `PlayablePlayerLocator`・`PlayEntryCandidates` の口・記録・observer とテスト、`PlayEntryOverlay` の要約の欄。(6) 入口。(7) `PlayerProcessRun` の切り出し、判定の test・driver・patch。
- Phase B から Phase A へ差し戻す条件: `RegisterVerifiedWithOverlay` を Player で通すのに session・gate の層か登録の位置（`BootstrapSourcePolicy`）の変更が要る、入口が Editor の Play の入口（`PlayEntryInterceptor`）か Delivery の ledger の状態を変えないと動かない、新しい asmdef 参照が要る、D13 の log の欄が既存の判定（S4-1a の test・04 章）を壊す。
- B 適応の例: IL2CPP・High で製品の登録の経路の型が欠けた時に足す `link.xml` の型（`SampleGame/DependOnAll/link.xml` を変える時は動作確認の Player の build にも効くことを結果に記録する）。上書きの受け入れ範囲を広げる目的の `link.xml` は B 適応にしない。AC1・AC12 の token の対象を、同じ root の閉包の別の見える物に変えること。D11 の `ProjectSettings` の除外の一覧。
- 対象外を維持する方法: `smoke` の分岐、Editor の Play の Decide の結果、`PlayerContentConfiguration.Resolve`、`BuildReceipt` の欄、`WireContentResolution` の条件、`BootstrapSourcePolicy`、`ContentDeliveryPlayBridge` を変えない。

## 5. テストとレビュー計画

- **G0（A1 の最初の gate。凍結の前に実施済み。結果は §1 の現況）:** 担当は Phase A の実行担当の session（Unity の batch と Player の起動）。(a) 別の `Library` の base と (b) 型の集合の材料は 2026-11-23、(d) script の field の差は 2026-11-24。手順: S4-0 の harness（commit `b101b382` の `Tests/Editor/Spikes/PlayerOverlay/` と probe・runner の asmdef）を 2 つの worktree（X・Y、それぞれの `Library`）に取り出し、base と Player を X で、上書きを Y で build して S4-0 の第 2 process（6 種の round を 3 回ずつ）を通す。(d) は Y で `ProbeBoth` の field を 4 通り（足す、消す、型を string に、型を float に）に変えて上書きだけを build し直し、X の Player で同じ第 2 process を通して、Player の log の Error・Exception の行、exit code、Player が読んだ probe の値を見た。
- 単体テスト: `ContentOverlayStartupTests`、`ContentOverlayDigestTests`、`PlayerCompatibilityTests`、`PlayablePlayerLocatorTests`、`PlayerOverlayStartupObserverTests`、入口の port を差し替えたテスト、既存の `PlayerContentConfigurationTests`・`PlayEntryCandidates` のテスト。
- 差し戻し中の起点 `-Filter`: `OneStarMaker.Tests.Editor.ContentOverlay.ContentOverlayStartupTests;OneStarMaker.Tests.BuildContent.PlayerContentConfigurationTests` と新しい test class（名前は B が決め、§6 に書く）。
- **判定の PASS と製品の経路の成功を分ける（外部 6 巡目）:** 判定の test が、製品の code が自分で行う事を代わりに用意すると、AC が通っても利用者の menu の経路は確かめられない。このスライスで test が代わりに用意しうる物と、製品の口が実際に動いた証拠を持つ AC は次のとおり。観測の駆動と Player の log の読み（test の待ちの loop と test の `ReadLog`）→ AC18（製品の update と製品の読み）。子 process の環境（test が掃除して直に起動）→ AC8 (b)（入口が組み立てた環境の記録）。指紋の file の集合（build と入口が同じ口）→ AC9 の `PlayerInputFiles` の test。同じ checkout での build の後の指紋 → AC17。B と C は、新しい case を足す時も「test が製品の口を迂回していないか」を同じ見方で確かめる。
- **発見 C の最初の限定実行:** Unity の test と Build は Phase C の責任なので、B は Unity を動かさずに全部を実装して渡す。発見 C は構造の ledger の前に、driver の先頭（X の `BuildPlayable`、Y への写し、AC8 (d)、install、AC8 (a)、AC1）を先に通す。AC8 (a) が落ちれば上書きの前の問題（installed の pair からの起動。S4-1a が成功を測っていない経路）、AC8 (a) が通って AC1 が落ちれば上書きの経路の問題として B へ戻す。
- 判定必須テスト（GO 候補 head）: 起点 filter、S4-1a の後の `PlayerKindEndToEndTests.BuildPlayable`・`LaunchPlayable`（AC14）・`BuildSmoke`・`LaunchSmoke`（AC2 の key なしの起動）、`PlayerOverlayEndToEndTests` の case（X の AC17 と AC2、Y の install と AC1・AC3〜AC5・AC7・AC8・AC10〜AC13・AC15・AC16・AC18）を別の Unity process で、最終の全 EditMode 回帰（`pwsh tools/run-tests.ps1`）。全 EditMode の合否の規則: Failed が 0。Inconclusive は、S4-1a の判定 C（run `j1`）の全 EditMode の XML で Inconclusive だった test の名前の集合（VFX の 25 件）に含まれる物だけを受け入れる（集合の外の Inconclusive は不合格。`run-tests.ps1` は Inconclusive で exit 1 になるので、合否は XML で読む）。staging の `Directory.Move` の access denied で落ちた test は、その test だけを 1 回取り直し、通れば受け入れて record に書く（program の S5-0、owner の既知の受容）。Unity の終了の stall は [Harness README の停止規則](../../tools/Harness/README.md#unity終了時の既知native-stallと調査の停止規則) に従い、XML が書かれた後に自分の起動した process だけを止めて note を残す。
- 全 EditMode 回帰の適用除外: なし。
- 操作・実行時・目視条件の検証経路: AC1〜AC5・AC7・AC8・AC10〜AC18 は上の判定の test が閉じる。担当は Phase C の実行担当。初期状態は Editor を閉じ、Active Build Target が StandaloneWindows64、X と Y が clean、他の Unity と `SampleGame.exe` が動いていない。観測は record・Player の log・game の log・receipt・XML・`last-attempt.json`・AC1 と AC12 の画像。事後に `git status --porcelain` と record の pid の終了を CIM で確かめる。証拠は `TestResults/s42/<run id>/`（Player の本体を除く）と XML を evidence bundle に入れる。画像の token は C と C' が画像で読む。動作確認（Phase B の後）: `対象なし`（offline のテストで閉じない条件は全て Player の build を要し、Phase C が担当する）。
- 未知の操作経路の疎通: 次は未確認で、**初回確認は Phase C**（Unity の test と Build は Phase C の責任）。u7・u8 は凍結の前に測り終えた（§1 の現況）。
  - (u1) 判定の test から 2 つの checkout をまたいで build・install・起動できるか（X の build は X の Unity process、Y の install と起動は Y の Unity process。driver が X の出力を Y の同じ path へ写す）。不成立時: 環境の不備は実行担当が記録して owner へ返す。test の形で解けなければ Phase A へ戻す。
  - (u2) IL2CPP・High の Player で `RegisterVerifiedWithOverlay` と digest の経路が strip で失敗しないか。不成立時: SampleGame・Framework の型の `link.xml` は B 適応、それ以外は Phase A。
  - (u3) Title の UI の label、Title の Scene の値、UICommon の UXML の label を変えた上書きが画像で見えるか。不成立時: 編集の対象を同じ root の閉包の別の見える物に変えるのは B 適応。
  - (u4) 入口が Player の process を窓つきで起動し、判定の test が入口の返した handle から exit code を読み、閉じられるか（S4-1 の `WM_CLOSE` の形）。不成立時: test の補助の変更は B 適応。
  - (u5) 要求時解決を配線しない Player で、logical key の層の選択と id による影が content 入口で効くか（13 章の規則。確認地点 AC1）。不成立時: Phase A へ戻す（配線するかは設計判断）。
  - (u6) 判定の test が Player の窓の画像を file に取れるか（画面の DC から窓の矩形を写す。S4-1a で shell からの画面の取り込みが成り立った手段と同じ）。不成立時: test の支援を足すのは B 適応、判定の実行担当が shell から取る形に替えるのも B 適応（owner が撮る形にはしない）。
  - (u9) installed の pair を渡した遊べる Player の成功の起動（S4-1a から移送した (2)。確認地点 AC8 (a)）。不成立時: 上書きの前の問題として発見 C の最初の限定実行で分かる。原因が S4-1a の起動の順にあれば Phase A へ戻す。
  - (u10) Player の build の mutation（`PlayerSettings` の IL2CPP・High、Addressables の設定）と戻しの後で、disk の `ProjectSettings/` が build の前と同じ bytes か（確認地点 AC17）。不成立時: mutation が変えて戻す設定を指紋の比較から外す規則を `PlayerCompatibility` に足すのは Phase A（止める範囲が変わる）。
  - (u11) 動いている Player（Unity）が上書きの directory へ file を書かないか（確認地点 AC13 の「A の directory が変わらない」。G0 の record では書かなかった）。不成立時: Phase A へ戻す（digest と再利用の前提が変わる）。
  - (u12) batch の EditMode の `[UnityTest]` が frame を譲る間に、製品の `EditorApplication.update` の観測が進むか（確認地点 AC18。S4-0 の第 2 process は同じ形の `[UnityTest]` で frame を進めた）。不成立時: 開いた Editor で入口だけを呼び、記録を外から読む形に替えるのは B 適応。製品の update の経路を測らずに AC18 を外すことはしない。
  - **判定の process の列（driver `tools/s42-player-overlay.ps1` が実行し、Phase C の実行担当はこの driver を呼ぶ）:** (1) X（新しい worktree）で、環境変数 `S41_RUN_ID` を 1 つの値に固定して（既存の `PlayerKindEndToEndTests` は、この値の `TestResults/s41/<id>/` に Player の写しと record を置き、無ければ失敗する）、`run-tests.ps1 -Filter <BuildPlayable の case>$`（AC17 の 1 つ目の時点を record に取る）、別の Unity process で AC17 の 2 つ目の時点の case、`LaunchPlayable$`（AC14）、`BuildSmoke$` と AC2 の case。AC2・AC14・AC17 は X で走らせる（既存の test は、同じ checkout の同じ run の写しから Player を起動する）。(2) X の `artifacts/playable/players/<id>/` と `transport/<id>/` を Y の同じ path へ写す。(3) driver は run ごとに、repo の外に固有の cache の folder を作り（path の予算に収まる短い path）、Y の `UserSettings/OSMContentDelivery.json` の `cacheRoot` にその path を書く（設定が空だと、入口と install は同じ Windows の user の既定の cache `%LOCALAPPDATA%/OSM/ContentCache` を共有し、別の run や他の作業の install と混ざる。この file は git が無視する）。install と入口が同じ path を使ったことを record に残し、終わりにその run の cache だけを消す（既定の cache には触らない）。続けて Y で AC8 (d)（install の前）。(4) Y で X の transport を install する case（install は disk に残る）。(5) case ごとに、driver が `PlayerOverlayEdits/` の patch を `git apply` し、`run-tests.ps1 -Filter <case>$` を別の Unity process で走らせ、XML の total を確かめ（`-Filter` は正規表現の部分一致なので末尾に `$` を付ける）、`git checkout`・`git clean -fd`（`-x` は付けない。`.gitignore` が無視する `artifacts/` の Player・上書き・記録を消さないため）で戻し、`git status --porcelain` が case の前と同じことを確かめる（違えば以後の case を止める）。上書きの artifacts と 2 つの記録は case の間で消さない（AC4 (e)・AC13 が前の結果を使う）。新しい worktree の最初の Unity の起動が書き換える `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`（S4-1 から移送した (7)。D11 の指紋に当たる）は、X と Y のどちらも最初の起動の後に `git checkout` で戻してから (1) と (3) に入る。Y の順は AC8 (d) → install → AC8 (a)(e) → AC1 → AC18 → AC4 → AC13 → AC12・AC16 → AC15 → AC5 → AC3 → AC7 → AC8 (b)(c) で、AC5（`.cs` の変更で再 compile する）を後ろに置く。AC10 は AC1・AC8 (a)・AC8 (d) の case の中で測る。AC12 と AC16 は 2 つの patch を当てた 1 回の起動で両方を読んでよい（合否は別々に書く）。AC13 は 1 つの case の中で Player A を起動し、test が patch を当てて（Unity の外の file の書き換えと `AssetDatabase.Refresh`）入口をもう一度呼び、最後に A と B を閉じる。AC1 の「スタート」は、case が Title の画像を file に取った後、game の log の `OnStabledImpl HomeScene` を 180 秒待つ間に、実行担当が画像で位置を読んで 1 回押す。
  - **判定の実装への注記（外部 8 巡目。AC は変えない）:** AC4 (e) の 3 つの段と AC18 (a)(b) は、他の case が書き換えた上書きに依らず、同じ編集のまま process ごとに走らせ、各段の `last-attempt.json` と Player の log を、次の段が上書きする前に case の証拠の folder へ写す。AC18 (b) で期待する Editor の Error の行は、`[UnityTest]` の「予期しない log」の失敗と区別し（期待する log として宣言する）、その行自体は生の証拠に残す。
  - 編集を伴う AC（AC1・AC5・AC12・AC15・AC16。AC13 は case の中で当てる）は、Y の Unity process を起動する前に編集し、別の process で入口を実行する（実行中の test の中で `.cs` を足すと compile と domain reload で test が切れる）。
  - **判定の担当への注記（移送された入力）:** Project Auditor の post-build 監査で Player の build の `step.json` が `failed` になりうる（S4-0 の J2、S4-1a でも同じ形）ので、合否は XML と record で読む。record の `PlayableBuildObservation.MapSceneResourceCount` は重複を除く前の件数である（S4-1a から移送した (6)）。Editor の検証 Play を判定に使うなら、開発者のローカル設定の Variant profile の選択を空にしてから行い、後で戻す（同 (7)）。このスライスは Editor の上書きの Play の起動の判定（Decide の Editor の分岐）に digest の任意の照合を足すので、Editor の上書きの Play を 1 回取り直す（同 (8)。起点は S3-4b の判定の case）。
- 人間の判断が必要な条件: なし（AC1・AC12 の画像の判定は判定の担当と C' が画像で行う）。
- 高コスト証拠: なし（全ての AC は Player の build が外部への作用を持たずこの PC で繰り返せるので、GO 候補 head ごとに取る。X の新しい worktree の import は約 11 分）。
- 判定 C の照合 script の方式: なし。判定 C は record と log と画像を AC ごとに読む。
- 機械検査: `contract-audit`、`docs-audit`。
- A0/A1 主担当: revision 0 の初稿から外部 4 巡目までは前の session（Claude Opus 5.5、Anthropic）。2026-11-24 の載せ替えと改訂は本 session（Claude Fable 5.2、Anthropic）。A0 は program §8 の S4-2 の行と S4-0・S4-1・S4-1a から移送した S4-2 の項目、§5.3、04・13・18 章、実測の事実の Player の節、レビュー入力（§5.1）を入力とした。
- C' 用に予約した担当: 最低条件（B・C の両方と異なるモデル、新規 session、blind audit bundle）を満たす担当を、起こす時に選ぶ。
- 独立性の強化条件を満たせない場合の理由: Phase A の計画レビューに Anthropic・SpaceXAI（Grok）・OpenAI（ChatGPT。PR の review）の 3 つの系列が関与し、この環境に他の系列の経路は無いので、Phase A に関与していない系列は残っていない。C' は `独立性制約あり` として、選んだ系列と残存リスクを §8 に記録し、owner が採否を判断する。

### 5.1 A2 の独立レビューと A3 の採否

**経緯（revision 0 の前半、2026-11-23〜24。採否の全表は commit `64bed397` の本書 §5.1 にある）:**

- A2 の独立レビュー（2026-11-23。互いの指摘を見せていない。全て Anthropic の系列）: (1) 目的レビュー（新しい subagent、Claude Opus 5.5。入力は目的と利用者の欄の原文だけで、sha256 `79de02b9e4396881ff5087adf8af8c76e56c9af6eb6dad121396654c4ebf1ede`）。(2) A0 だけからの代替構成（(1) と同じ subagent が回答の記録の後に続けた。packet の sha256 `3c9d33ecd4507be1350202e48c60fcdb8fa73081b7dbd8781fb5954138157e2f`、固定 base `116b0091`）。(3) アーキテクチャゲート兼反証（別の subagent、Claude Opus 5.5。A2 入力版の sha256 `416b02828ea53e5213fc3c558d27b67e14764d43d5d899298181ae19099215ba`、同じ base。blocker 2 件、major 8 件、minor 9 件）。
- 外部計画レビュー 1〜4 巡目（1 巡目は Grok 4.7（SpaceXAI）。2〜4 巡目は owner の account からの review と Claude Code の計画レビュー）: X1〜X8、R1〜R12、S1〜S5、T1〜T8。owner の account からの review のうち 3 本と、5 本目・6 巡目の review は ChatGPT（OpenAI の系列）が書いた（6 巡目の review の申告。前の版はこの系列を記録していなかった）。
- owner の決定（2026-11-24、root）: 型と script を照合しない、型を見る debug menu は後続、`ProjectSettings`・`Resources`・`StreamingAssets` の編集は止める、map と UICommon の編集は届ける（S4-1a を置く）。目的の欄を 2 回改めた（§1）。1 回目の後に目的レビューをやり直し（新しい subagent、入力の sha256 `7f8ff8f581d25e9655ba281229531c842ee94520a83b97e385ccbf1d7a180519`。新しい指摘は無い）、2 回目は規則どおり owner が文言を裁定した。

**前半の採否のうち、今の設計に残っている物:**

| 元の指摘 | 今の反映 |
|---|---|
| 目的レビュー: 上書きの改ざんは Player が起動時に照合する、理由が利用者に届く、重ねた結果が見える、base と Player を書き換えない、Player・base・hash を手で入れさせない | D3、D10・E2、AC1・AC12・AC16、I6、UC2 |
| A0 だけからの代替構成: `suppliedSceneKeys` を空にする、Player の `build-receipt.json` の `transportManifestSha256` と install を突き合わせる、Player の log から理由の行を拾う、配線なしで層が効くかを未知にする、上書きの置き場の path の長さ | D6 の (4)、D6 の (2)、D10、u5、§3（path の予算） |
| A2 (3): B-1（判定の test の置き場）、M-1（環境変数の継承）、M-2（digest だけの起動）、M-4（game の log と AC10 の時点）、M-5（AC4 (a)(e)）、M-6（入口が Player を見つける規則）、M-7（編集は process の前、指紋は準備の前）、M-8（compat は publish の前）、m-1〜m-7 | §3、D6 の (6)・AC8 (b)、D2・AC4 (d)、I6・AC10、AC4、D6 の (1)、§5・D6 の (4)、D11、D2・D3・AC1・AC3・AC8 (c) |
| 外部 1 巡目: X2（AC は今の message で読む）、X6（compat を書く場所）、X7（`smoke` への漏れ）、X8（digest の規則と例） | AC3・AC4・AC7、D11、AC2、D3 |
| 外部 2 巡目: R2（再利用の判定の前に digest を比べる）、R5（`link.xml` を足さない）、R6（既定の起動の非退行）、R7（Player の入力だけの編集）、R8（置き場と contentSet）、R10（driver）、R12（全 EditMode の合否の規則、reason の固定） | D8、D12、I7・AC14、D11・AC15、D8、§5、§5・D10 |
| 外部 3 巡目: S2（文言の整合）、S4（Scene の差の token）、S5（撮影は test） | 全体、AC1、AC1・u6 |
| 外部 4 巡目: T1（Framework の許可 root）、T2（既存の directory を変えない）、T5（u7 を凍結の前に測る）、T6（URP の asset と `ProjectSettings` の除外）、T7（`outcome` と `startup` を分ける） | D8、D9・AC13、G0 (d)、D11、D10 |

**この改訂（2026-11-24）で置き換えた採否:** X1・R1・S1（Delivery の ledger を base の正本にする、その読み口、session の間だけの選択）は D6 の (1)(2)（Player の identity から決める）に置き換えた。X4・R11（owner の「スタート」の押下と高コスト証拠）は判定の実行担当の操作に置き換えた（§2.5 の 8）。R4（spike の branch の push）は Evidence の保存に置き換えた（§2.5 の 10）。B-2（Player が登録の前に読む root の編集を止める）は S4-1a が起動の順を変えて無くし、AC12・AC16 が効きを見る。

**レビュー入力（2026-11-24。この branch の commit `ca64752c` の `docs/handoff/PLAYER_CONTENT_OVERLAY_REVIEW_INPUT.md`。PR #212 の計画レビューを 2 巡行った session（Claude Code、Anthropic）が書いた。入力は HANDOFF `64bed39` と `develop` `1fd200b`。採否をここへ写したので file は消した）と、PR の 5 本目の review（2026-11-23、HEAD `64bed397`、2 スレッド。P1 の 2 件はレビュー入力の 3.3 と 3.2 と同じ）の採否:**

| ID | 指摘 | 採否 | 理由と反映 |
|---|---|---|---|
| L1（3.1） | base を ledger ではなく Player の identity から決める | 採用 | ledger が `SessionState` であること、手で戻す ledger が Editor の Play の入口の「明示の設定」になることを code で確かめた（§1 の現況）。D6 の (1)(2)、D7 を欠番、UC1、AC8 (d)(e)、I6、§3（`ContentDeliveryPlayBridge` を変えない） |
| L2（3.2、PR の 2 件目） | AC12 は「上書きに入った」ではなく「Player で効いた」を見る。map と UICommon を分け、B に選ばせない | 採用 | UICommon は AC12（UXML の token を画像で読む）。map は AC16 と D13（log の `hash=`）。map から `SceneResource` への参照は見ない範囲に決めた（§2.3 に理由） |
| L3（3.3、PR の 1 件目） | 計画を S4-1a の後の `develop` に載せ替え、起動の配線を再照合する。Runtime の A2 を取り直す | 採用 | base を `1fd200b5` にし、§1 の現況、D2・D3・I1・I2、§3 の行数、AC2・AC3・AC14、Inconclusive の基準の run（`j1`）を今の code に合わせた。A2 は全体を取り直した（下） |
| L4（3.4） | S4-1a から送られた入力の置き場 | 採用 | (1) AC12・AC16・§2.3、(2) u9・AC8 (a)・§5 の最初の限定実行、(3) D11、(4) D10、(5) I1 の探索した境界、(6)〜(8) §5 の判定の担当への注記 |
| L5（3.5） | type tree の前提（u7）を凍結の前に測る | 採用（実施した） | G0 (d)（§1 の現況） |
| L6（3.6） | `startup` の観測を batch の test から進められる形にし、domain reload の扱いを決める | 採用 | D10（関数 `Step`、記録の読み直し）、§3 の observer。判定の test が同期の `[Test]` で待つことは code で確かめた |
| L7（§4） | AC5 の期待値を u7 の結果から書く | 採用 | AC5 と G0 (d) |
| L8（§4） | AC13 は u8 のまま。不成立の時の読みを先に決める | 採用（実測で閉じた） | u8 を測った（§1 の現況）。D9、AC13 は game の log を合否に使わない |
| L9（§4） | 新しい checkout の URP の書き換えで、触っていない file が `PlayerInputDiffers` になる | 採用 | D11 の案内の文と README の「試す」 |
| L10（§4） | 依存の閉包に package の file が入る | 採用 | D11（閉包は `Assets/` の下だけ、package は 2 つの file で見る） |
| L11（§4） | AC1 の人間の段を、S4-1a と同じ AI の操作にするかを書く | 採用 | §2.5 の 8 |
| L12（§4） | 判定の driver が古い上書きを消すかを決める | 採用（消さない） | §2.3 |
| L13（§5） | Runtime の配線が済んだ時点で、環境変数で直に起動する 2 つの case を先に通す | 一部採用 | B は Unity を動かさないので、発見 C の最初の限定実行にした（§5）。直に起動する専用の case は足さず、入口を通る AC8 (a) と AC1 を先頭に置いて、上書きの前と上書きの経路を分ける |
| L14（§6） | 後続へ送る物 | 採用 | §2.3 |

目的と利用者の欄は変えていないので、目的レビューはやり直していない。

**A2 の独立レビュー（2026-11-24 の取り直し。互いの指摘を見せていない。入力版は、固定 base `1fd200b5ba835c833035d153579216e14cb823af` の clean な detached checkout と、本書の固定 copy（sha256 `e7a509ff589cc457732cb293046cf92b02932c15fca390b7c817cd5eabc89791`）。枠は「§3・§4 の計画した code は実装済みと仮定し、現在の code は既存の前提の確認にだけ使う」に固定した）:**

(4) アーキテクチャゲート: 本 session が起動した新しい subagent（Claude Opus の系列、Anthropic。A1 の改訂の担当とは別のモデル）。HEAD・clean・copy の hash を照合してから読んだ。blocker 0 件、major 6 件、minor 8 件。Runtime の配線（D2・D3・D13、I1〜I4・I7）は S4-1a の後の code と合うと確かめ、指摘は SampleGame の Editor の側の責務マップの抜けに集まった。

| ID | 重さ | 指摘 | 採否 | 理由と反映 |
|---|---|---|---|---|
| A-1 | major | `last-attempt.json` の root の一覧を取る口が無い（計画の要約は件数だけ） | 採用 | code で確かめた。D10、§3（`PlayEntryOverlay`・`SampleGameContentBuild` の要約に logical key の列） |
| A-2 | major | 保存の確認の一部は `PlayEntryInterceptor` の private で、先置きの応答の key を共有すると 2 つの入口が取り合う | 採用 | code で確かめた。D6 の (3)（検出は可視性だけを変えて共有し、dialog と応答はこの入口の物にする）、§3 |
| A-3 | major | 指紋の file を集める Unity の I/O の置き場が無く、build と入口で 2 つに分かれる危険 | 採用 | D11、§3（`PlayerInputFiles` を唯一の口にする） |
| A-4 | major | 同じ checkout で build した直後に指紋が一致することを見る AC が無い（mutation が `PlayerSettings` を変えて戻す） | 採用 | AC17、u10、§5 の driver の (1) |
| A-5 | major | 観測が依存する初回 Scene の行の文字列が public の定数でない（04 章の規約） | 採用 | D10、§2.5 の 4、§3（`PlayerFirstSceneLogMarker`） |
| A-6 | major | 入口が作る process の handle を test へ渡す形と、`PlayerRun`（private の入れ子）を写すか共有するかが未設計 | 採用 | D10（port の戻り値）、§3（`PlayerProcessRun` へ切り出す）、u4 |
| A-7 | minor | digest を取る path の正規化と、root 自体の reparse point | 採用 | D3 |
| A-8 | minor | digest の各行の書式と、既存の SHA-256 の関数 | 採用 | D3 |
| A-9 | minor | `overlay-record.json` が無い時の返り値と、書く者 | 採用 | D8、AC9 |
| A-10 | minor | `last-attempt.json` の原子的な置き換え。AC13 で 2 回目が A の記録を上書きする | 採用 | D10、AC13（A が `Observed` になった後に 2 回目） |
| A-11 | minor | `ProjectSettings/` の再帰と、Editor が生成する file の扱い | 採用 | D11（再帰する。不一致が出た file の扱い） |
| A-12 | minor | 動いている Player が上書きの directory へ書かないことが未知の列に無い | 採用 | u11（確認地点 AC13） |
| A-13 | minor | `ContentOverlayDigestTests` を置く assembly | 採用 | §3 の末尾 |
| A-14 | minor | `PlayEntryCandidates` の既存の検証は Editor の Play の値も作る | 採用 | D6 の (2)（`ContentInstallResult` だけを返す） |

確認だけ（問題なし）: Decide の時点で baked の種類を読める、登録の直前の照合が全ての登録の経路（遊べる Player の `:362`、動作確認の Player と Editor の `:420`）を覆う、Player の分岐は `Resolve` の後に構成を返すので protected の照合を飛ばさない、map の log の読み手は先頭の一致で読むので `hash=` で壊れない、§1 の現況の行番号と中身は全て code と合う、新しい asmdef の参照は要らない、再利用の記録は contentSet ごとに分かれる、非分割の判断は妥当。確かめていない範囲: Unity の挙動（build が `ProjectSettings.asset` を disk に保存するか = u10、Mono で handle の無い process の exit code、Player が上書きの directory へ書くか = u11）、G0 の証拠の実物、13・18 章。

(5) 各不変条件を破る順序・受け入れ条件の曖昧さ・検証経路の実行可能性: `cursor-agent` の Grok（`grok-4.7-high`、SpaceXAI。Anthropic とは別のベンダー）。入力は同じ固定 copy と、固定 base から取り出した関係する code と 04・13 章の写し（78 file）。凍結を止める指摘 1 件、major 3 件、minor 1 件。I1・I2・I3・I7 は「破れない」と返した（I1 は Decide・登録の位置・失敗の通知・環境変数の掃除、I2 は種類と build の枝、I3 は `Resolve` と gate の重複の拒否、I7 は Decide の最初の `None`）。

| ID | 重さ | 指摘 | 採否 | 理由と反映 |
|---|---|---|---|---|
| DRV | blocker | driver の (1) と AC14 は、既存の `BuildPlayable`・`LaunchPlayable` が要る `S41_RUN_ID` を置かず、Y には run の写しが無いので、書かれた順では実行できない | 採用 | code で確かめた（`PlayerKindEndToEndTests.cs` の `RunDirectory`・`Launch`）。§5 の driver の (1): `S41_RUN_ID` を固定し、AC2・AC14・AC17 を X で走らせる |
| I4 | major | base の記録の不一致は、上書きを native に登録した後に照合して巻き戻す（13 章）ので、「native の登録より前に止まる」は成り立たない | 採用 | 13 章と合わせた。I4 の文を「読ませない」にし、digest と key は登録の前、base の記録は登録の後・load の前の巻き戻しと書き分けた |
| AC-T | major | 制限の 120 秒がどこまでの時間か、AC1 の 180 秒と衝突する | 採用 | §2.2 の冒頭に範囲と合格例・不合格例 |
| AC-E | major | AC10 の終わりの時点が、process を作らない停止と case の後の `git status` で二通りに読める。§5 の順に AC10 が無い | 採用 | I6、AC10（測る case と時点）、§5 の順 |
| I6 | minor | 入口の base の検証が cache の lock の file（`control/transaction.lock`）を作りうる | 採用 | I6・AC10 の対象を revision の tree にし、cache の制御の file を除いた |

**A3 の統合（2026-11-24。統合担当: 本 session）:** 取り直した A2 の 19 件（(4) の 14 件、(5) の 5 件）は全て採用した。どれも凍結済みの条件を変える物ではなく（この時点では凍結の前）、責務マップの抜け、文の書き分け、検証経路の実行可能性の補正である。目的と利用者の欄は変えていない。採用による設計の追加は、public の marker 1 つ（A-5）、file 2 つ（`PlayerInputFiles`、test の補助 `PlayerProcessRun`）、AC17 と u10・u11 で、Runtime の判断と依存の向きは変えていないので、A2 の再々レビューは行わない。

**外部計画レビュー 6 巡目（2026-11-24、入力は HEAD `f3e46fb6` の HANDOFF と base `1fd200b5` の code。PR の `@cursoragent` の review（Grok 4.7、SpaceXAI。Y1〜Y5）と、owner の account からの review 1 本・5 スレッド（ChatGPT、OpenAI。Z1〜Z5。「まだ凍結しない方がよい」。軸は、test の PASS と、開発者が実際に使う製品の経路の成功を混同しないこと））:**

| ID | 指摘 | 採否 | 理由と反映 |
|---|---|---|---|
| Y1 | URP の global settings は `GraphicsSettings` の参照を辿っても出ず、指紋から抜ける。build と入口が同じ関数なので AC17 では見えない | 一部採用 | 事実は違った（`GraphicsSettings.asset` の `m_RenderPipelineGlobalSettingsMap` が guid でこの asset を指す。名前では出ない）。集め方の抜けが AC17 で隠れる、という指摘は正しいので、D11 に起点の 3 つと `.meta` を書き、AC9 に集合の test を足した |
| Y2 | 製品の観測が Player の log を開く共有の指定が決まっていない。判定の test は自分で読んだ text を渡すので AC が通る | 採用 | D10（開き方を固定）、AC18（製品の読みで観測が進む） |
| Y3 | pair の無い遊べる Player の reason が D2 の「今のまま」と AC7 で食い違う | 採用 | code で確かめた（`ContentOverlayStartup.cs:74-75` が pair より前）。D2 に reason の書き分け |
| Y4 | driver の `git clean -x` が `artifacts/` を消す | 採用 | §5 の driver の (5)（`-fd` まで） |
| Y5 | batchmode の確認の dialog は false を返し、未保存があると E2E が `Cancelled` で止まる | 採用 | D6 の (3)（0 件なら呼ばない、E2E の初期状態） |
| Z1 | `Step` を test が直に呼ぶだけでは、製品の `EditorApplication.update` の経路が一度も動かなくても AC が通る | 採用 | AC18、u12、§3、§5 の「PASS と製品の経路を分ける」 |
| Z2 | 同じ Player の folder を複数の case が使うので、game の log を case ごとに分ける規則が要る（無いと「Title が無い」が偽の FAIL、Error 0 が履歴と混ざる） | 採用 | §2.2 の冒頭（起動の前に無いことを確かめ、終わった後に移す） |
| Z3 | AC8 (b) の 3 key だけでは、`SAMPLEGAME_*` を全て消すことを確かめられない | 採用 | AC8 (b)（`runtimeMode`・`firstScene`・`playerKind` も置き、入口が組み立てた環境の記録で読む）、D10（環境の名前の一覧を記録） |
| Z4 | G0 (d) の証拠を C' に渡さないなら、その実測にしか依らない主張を分ける | 採用（範囲を狭めた） | AC5 と §2.5 の 10（判定が保証するのは field を足す場合だけ。消す・型を変えるは Phase A の実測の記録）。証拠の保存は条件にしない |
| Z5 | OpenAI の系列は Phase A の計画レビューに関与しており、「未関与」として C' に予約できない | 採用 | §5 の C' の欄（最低条件で選び、`独立性制約あり` と記録する）、上の経緯の系列の記録 |

A2 の観点の再レビュー: 行わない。足したのは AC18 と u12、AC8 (b)・AC9 の観測、D2 の reason の文、D10・D11 の規則の明記、判定の手順で、責務マップの file・依存・公開面・不変条件は変えていない。目的と利用者の欄も変えていない。

**外部計画レビュー 7 巡目（2026-11-24、入力は HEAD `bc1f0319` の HANDOFF と base `1fd200b5` の code。PR の `@cursoragent` の review（Grok 4.7、SpaceXAI。W1・W2）と、owner の account からの review 1 本・3 スレッド（ChatGPT、OpenAI。V1〜V3。「W1 と P1 が閉じれば凍結して B に進める方向でよい」））:**

| ID | 指摘 | 採否 | 理由と反映 |
|---|---|---|---|
| W1 | AC18 (b) の差し込み口（digest の取り直しの後・process を作る前）が D6 と §3 に無く、AC4 (a) の口と同じにすると入口が先に止まる。壊す上書きの出どころも未定 | 採用 | D6 の (5)（空の port `BeforeProcessStart`）、§3 の入口の行、AC18（AC1 と同じ patch、(b) は (a) の後の起動の上書きを対象）、AC4 (a) の位置の書き分け |
| W2 | D11 の「各 level」は Mobile の render pipeline の asset も指すが、AC9 の集合の test に無い | 採用（全ての level を入れる） | code で確かめた（Mobile は Standalone を除外する level）。除外の規則を足すより入れる側に倒す。D11、AC9 |
| V1 | 指紋の照合が未保存の asset の保存より前なので、Inspector で変えた Player の入力（render pipeline の asset など）が、照合の後の保存で disk に書かれてすり抜ける | 採用 | D6 の (3)(4) の順を入れ替えた（保存 → 照合。照合は 1 回）。AC9 に port の test（保存が指紋の file を変える → `PlayerInputDiffers`、`Prepare` と起動は 0 回）。検出に掛からない未保存の変更は §2.3 |
| V2 | AC4 (e) と AC9 は、常に build し直す実装でも通る。再利用の正の対照が要る | 採用 | AC4 (e) を (i) `Built` → (ii) `Reused`（同じ identity と digest）→ (iii) file を足して `Built` の列にした。AC9 に「記録があり digest が同じなら再利用の結果を返す」 |
| V3 | X・Y が clean でも、install の cache は同じ user の既定の場所を共有する | 採用 | code で確かめた（`VerifyPlayLauncher.ResolveCacheRoot` の既定）。§5 の driver の (3)（run ごとの cache を Y の設定に書き、record に残し、その run の分だけ消す） |

A2 の観点の再レビュー: 行わない。D6 の順の入れ替えと空の port は入口の file の中の順で、責務マップの file・依存・公開面・不変条件は変えていない。他は AC の観測と判定の手順である。目的と利用者の欄も変えていない。

**外部計画レビュー 8 巡目と凍結（2026-11-24、入力は HEAD `b7a61c50` の HANDOFF と base `1fd200b5` の code。owner の account からの review 1 本（ChatGPT、OpenAI））:** 新規の blocker なし、凍結を推奨。7 巡目の V1〜V3・W1・W2 の反映を認めた。Phase B・C への非阻害の注意（AC4 (e) と AC18 の段ごとの証拠の退避、AC18 (b) の期待する Error の扱い）は、AC を変えない判定の実装の注記として §5 に写した（採用）。残る未知は §5 の u1〜u6・u9〜u12 と発見 C の最初の限定実行で扱う。owner（root）は同日、この版の凍結を裁定した（§0 の A3 凍結記録）。

## 6. Phase B 実装結果

担当: Phase B の session（2026-11-24）。引き継いだ commit `50541d25` を worktree の HEAD と照合し（一致、clean、§0 の凍結記録・revision 0・票の次担当 B を確認）、§3 の責務マップと §4 の順で実装した。設計判断は増やしていない（下の「B 適応」以外）。

### 6.1 変更した file（§3 との対応）

| §3 の行 | 実装 |
|---|---|
| `ContentOverlayDigest.cs`（新規） | D3 の規則。root と中の reparse point は辿らずに `ContentDirectoryException`。file の hash は `ContentDeliveryFiles.Sha256` |
| `ContentOverlayStartup.cs` | `Decide` に digest の key と baked の `PlayerContentKind` を足した。3 key のどれか 1 つで判定に入る。`smoke` と Player でも Editor の Play でもない起動は pair より前に今の文、pair の無い `playable` は Editor と同じ installed の文、`playable` で digest が無い・空は `Content overlay on a Player needs editor:contentOverlayDigest.`。決定に `Digest` を持たせた（Editor の Play は key がある時だけ値、無ければ null）。定数 `OverlayDigestKey`・`DigestMismatchReason`・`PlayerFirstSceneLogMarker` を足した |
| `AbstractApplicationInitializer.cs` | Decide に baked の種類（`ReadBakedPlayerKind()`）と digest の key を渡す。Player の分岐で `Overlay` を `ContentDirectoryConfiguration` に渡す。`InstallConfiguredContentDirectoryAsync` の中、`RegisterVerifiedWithOverlay` の前に `VerifyOverlayDigest`（`ContentRevisionGate.TryNormalize` と存在の確認を登録と同じ文で行い、digest が違えば `DigestMismatchReason`）。map の log に ` hash=<GenerateHash>`。初回 Scene の行の先頭を定数にした。登録の位置（`BootstrapSourcePolicy`）は変えていない |
| `ContentOverlayPlayConfig.cs` | `OverlayDigestKey`、`PlayerFirstSceneLogMarker`、`ComputeOverlayDigest(string)`（絶対 path を要求）を public で足し、remarks を Player の渡し方に直した |
| `PlayerCompatibility.cs`（新規） | D11 の集合の除外の規則、指紋（D3 と同じ書式）、`player-compat.json` の形と読み書き（schema・digest の自己照合）、比較（`<path> (changed|added|removed)`） |
| `PlayerInputFiles.cs`（新規） | 集める唯一の口。`ProjectSettings/` の再帰、package の 2 file、render pipeline の起点 3 つ（`GraphicsSettings.defaultRenderPipeline`、`QualitySettings.GetRenderPipelineAssetAt` の全 level、`GraphicsSettings.GetGraphicsSettings()` の `m_RenderPipelineGlobalSettingsMap` の参照を SerializedObject で辿る）の `AssetDatabase.GetDependencies` の閉包の `Assets/` の file と `.meta`、`Assets/` の下の全ての `Resources` folder、`Assets/StreamingAssets/`、開始 Scene と `.meta`。起点に挙げた file が無ければ例外 |
| `SampleGamePlayerBuildCoordinator.cs` | 遊べる Player だけ、mutation の前に指紋を取り、Unity の Player の build が返った後・`Publish` の前に `playerWork` へ `player-compat.json` を書く。`smoke` は書かない。Publisher は変えていない |
| `PlayablePlayerLocator.cs`（新規） | D6 の (1)。規則（`Choose`）は folder の値の列だけで決め、読み（`Scan`）を分けた。最新の folder が使えない（receipt が読めない、identity が folder 名と違う、digest が無い、compat が無い）時は古い方へ戻らず止める |
| `PlayEntryCandidates.cs` | 既存の検証を `Validate` に切り出して Editor の Play の候補と共有し、`ResolvePlayerBase`（宣言・target・revision = Player の identity・manifest の digest = `transportManifestSha256` の 1 件だけ）を足した。結果は `PlayerBaseResolution`（install、未 install、検証の失敗） |
| `PlayerOverlayAttemptRecord.cs`（新規） | `PlayerOverlayOutcome`・`PlayerOverlayStartup` の enum、`last-attempt.json`（`PlayerOverlayAttempt`）と `<identity>/overlay-record.json`（`PlayerOverlayBuildRecord`）の形、別名に書いて `File.Replace` で置き換える読み書き |
| `PlayerOverlayStartupObserver.cs`（新規） | `Step`（Unity を呼ばない 1 回分）、`ObserveOnce`（記録・Player の log・pid と開始時刻から 1 回観測して書き換え、`Failed`・`TimedOut` は Console の Error と dialog）、`[InitializeOnLoad]` と `Begin` の update の pump（約 1 秒ごと）。log は `FileShare.ReadWrite | FileShare.Delete` で開く。入口がもう一度実行されて記録が置き換わっていたら、前の観測の結果で上書きしない |
| `PlayEntryOverlay.cs`、`SampleGameContentBuild.cs` | 計画の要約と結果に `OverlayRootKeys`（任意の引数。既存の呼び手は変えずに済む）。`OverlayPorts` に contentSet の引数、`PlayerOverlayContentSetOf`（`<宣言>-player-local-edits`）。Editor の Play の入口は今の `OverlayContentSetOf` を渡す |
| `PlayEntryInterceptor.cs` | `EnumerateUnsavedAssets` を internal にし、`OverlayPorts` に今の contentSet を渡すだけ |
| `PlayerOverlayLaunch.cs`（新規） | menu `Tools/OSM/Content/Play Playable Player With Local Edits`。D6 の (1)〜(7) の順、再利用の包み（`VerifiedReuse`）、`BuildEnvironment`（継承の `SAMPLEGAME_*` を全て消して pair と上書きの 3 key だけ）、`BeforeProcessStart`、process の handle を結果で返す。`Built` の後に入口が `overlay-record.json` を書く。content の編集が無い時は Console に `上書きなし（content の編集なし）` の 1 行 |
| `tools/s42-player-overlay.ps1`（新規） | §5 の driver。X の 6 段、X から Y への写しと run ごとの cache（repo の外の `<drive>\s42c\<RunId>`、Y の `UserSettings/OSMContentDelivery.json` の `cacheRoot`）、Y の 17 段、cache の後始末。各段で URP の global settings を戻し、patch の適用、`run-tests.ps1 -Filter <case>$`、XML の total = 1 の確認、`git checkout`・`git clean -fd`、`git status --porcelain` の前後の一致（違えば止める）。`-DryRun` は Unity を起動しない |
| `PlayerOverlayEdits/*.patch`（新規、5 file） | `ac01-title`、`ac05-field`、`ac12-uicommon`、`ac15-tag`、`ac16-map`。§3 の「7 file」のうち AC13 は `ac12-uicommon` を case の中で当てる（AC3 も同じ）ので、5 file で足りた |
| `PlayerProcessRun.cs`（新規） | `PlayerKindEndToEndTests` の `PlayerRun` と game の log の読みを移した。観測の時刻と結果はこの object に持ち、呼び手が自分の record に写す（`PlayerKindEndToEndTests` は薄い包みで写す。挙動は変えていない）。刻みごとの任意の callback と `WaitUntil` を足した。`PlayerKindEndToEndTests.BuildAndCopy` は internal にし、結果を返す |
| `PlayerOverlayEndToEndTests.cs`（新規） | X1〜X3、Y01〜Y17（下の表）。入口は製品の port で呼び、差し替えは AC10 の時点を取る起動の包みと AC18 (b) の `BeforeProcessStart` と、batch で dialog を出さない `Stop` だけ |

単体テスト: Framework に `ContentOverlayStartupTests`（表を書き直した）と `ContentOverlayDigestTests`（新規）。SampleGame に `PlayerCompatibilityTests`、`PlayablePlayerLocatorTests`、`PlayerOverlayStartupObserverTests`、`PlayerOverlayLaunchTests`（入口の port、再利用の包み、記録の読み書き、AC4 (a)）、`PlayerInputFilesTests`（この project の集合）、`PlayEntryCandidatesTests` に base の決定の 4 件。

### 6.2 判定の case と受け入れ条件

| case（driver の名前） | AC |
|---|---|
| X1 `X1_BuildPlayableAndCompareInputs` | AC17 の 1 つ目、AC11 の X の側（run の写しは AC14 が使う） |
| X2 `X2_CompareInputsInANewProcess` | AC17 の 2 つ目 |
| X-LaunchPlayable（既存 `PlayerKindEndToEndTests.LaunchPlayable`） | AC14 |
| X-BuildSmoke・X-LaunchSmoke（既存） | AC2 の key なしの起動 |
| X3 `X3_SmokeRejectsOverlayKeys` | AC2 の上書きの 3 key、compat の不在、mutation の root の残り |
| Y01〜Y03 | AC8 (d)・AC10、install、AC8 (a)(e)・AC10 |
| Y04 `Y04_TitleEdits` | AC1・AC10・AC11 |
| Y05 `Y05_ProductObservation`（`[UnityTest]`） | AC18 (a)(b) |
| Y06〜Y08 | AC4 (e) の (i)〜(iii)（別の Unity process） |
| Y09 `Y09_DirectDigestRejections` | AC4 (b)(c)(d)（(b) の書き換えは起動の後に元の bytes へ戻す） |
| Y10 `Y10_SecondPlayerWhileFirstRuns` | AC13 |
| Y11 `Y11_UiCommonAndMapEdits` | AC12・AC16 |
| Y12・Y13 | AC15、AC5 |
| Y14 `Y14_OverlayForAnotherBase` | AC3（別の base は `artifacts/playable/ac3-transport/<identity>` に publish） |
| Y15・Y16・Y17 | AC7、AC8 (b)、AC8 (c) |

AC4 (a) と AC9 は単体テスト。

### 6.3 B 適応と新しい事実

- **D11 の `ProjectSettings` の除外の一覧（§4 の B 適応の例）:** `EditorSettings.asset`、`EditorBuildSettings.asset`、`PackageManagerSettings.asset`、`VersionControlSettings.asset`、`PresetManager.asset`（以上は §2.1 の例）、`ProjectAuditorSettings.asset`（Project Auditor の Editor の設定）、`SceneTemplateSettings.json`（Scene template の Editor の設定）、`CsprojModifierSettings.json`（IDE の project の生成の設定）。`ShaderGraphSettings.asset`・`UnityConnectSettings.asset`・`URPProjectSettings.asset`・`MemorySettings.asset`・`XRSettings.asset`・`MultiplayerManager.asset` は「入らない」と言えないので入れた。加えて、mutation が一時に書いて消す `Assets/Resources/BS4/` の下と `Assets/Resources/BS4.meta`、`Assets/StreamingAssets/bs4-runtime.json` とその `.meta` を除いた（§2.1 の除外の明記どおり。`BS4.meta` と `.meta` は mutation の後に残りうる親 folder・import の file で、同じ理由の除外）。
- **AC1 の第 2 の token の対象（u3 の B 適応）:** Title の Scene の file が持つ値は view の 2 つの参照（`_visualTreeAsset`・`_backgroundDefinition`）だけで、文字列や色を持たない。そこで `ac01-title.patch` は、Scene の `_visualTreeAsset` の参照を新しい UXML（`TitleS42Token.uxml`。Title の UXML を template として包み、`S42-AC1-SCENE-TOKEN` の label を足す）に替える。第 1 の token は Title の UXML の button の文字列（`S42-AC1-UXML-TOKEN`）で、包む側からも表示される。
- **AC5 の script（同じ B 適応の範囲）:** Title の root の閉包に prefab は無く、Title の Scene の view（`TitleView`）が閉包の SampleGame の script である。`ac05-field.patch` はこれに `int _s42AddedField` を足し、Title の Scene の file に値 42 を入れる。
- **AC8 (c) の作り方（未確認）:** `Y17_UnbuildableOnly` は map の root のうち Title・UICommon 以外の Scene の file と `.meta` を Assets の外へ退かして入口を実行する。source の欠けだけで計画が `UnbuildableOnly` になるかは、既存の計画の test（`OverlayFromLocalEditsIntegrationTests` の R4 は別の編集と組で `SourceAbsent` を作る）から確かめきれていない。`NotRequired` になれば AC8 (c) の初期状態が作れていないので、判定の担当は B へ戻す（退かす物を変えるのは B 適応）。

### 6.4 確認したこと・未実行

- `pwsh tools/contract-audit.ps1`: `errors=0 warnings=0`（検査 1〜9。path の予算を含む）。
- patch と driver: 新しい checkout では `.patch` が CRLF で取り出され、`eol=lf` の Unity の YAML に当たらなかった。`.gitattributes` に patch の folder だけ `text eol=lf` の 1 行を足した（他の file の取り出しは変えない）。その後、implementation head から作った新しい worktree 2 つ（X・Y）で `tools/s42-player-overlay.ps1 -DryRun` を流し、全 23 段で patch の適用・戻し・`git status --porcelain` の前後の一致が通った（Unity は起動しない）。AC13・AC3 が case の中で当てる `ac12-uicommon.patch` は、`ac01-title.patch` と別の file だけを変える。
- Editor での compile の確認: この worktree の Editor（Unity 6000.6.9f1、新しい worktree の初回の import）で、全ての assembly が compile した（`Tundra build success`、`error CS` 0 件）。最初の compile で test の 2 件の型の誤りが出たので直してから取り直した。Editor は自分で起動した物だけを閉じた。閉じた後に `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` が書き換わっていた（S4-1 から移送した (7) と同じ現象）ので `git checkout` で戻し、commit に入れていない。driver は各段の Unity の起動の前にこの file を戻す。
- 未実行（Phase C の責任）: Unity の EditMode の test（単体テストを含む全て）、Player の build・起動、判定の case、driver の本番の実行（`-DryRun` だけを流した）。u1〜u6・u9〜u12 は全て未確認のまま。

### 6.5 発見 C（1 巡目）の差し戻しの修正

担当: Phase B の session（2026-11-25）。引き継いだ commit `23517f3c` を worktree の HEAD と照合し（一致、clean、implementation head `ae49bebf` から HEAD までの差は本書だけ、PR の状態ブロックの引き継ぐ commit と implementation base / head も一致）、§7 の DC1〜DC3 を直した。3 件とも B 適応で、指紋の範囲・契約・責務配置は変えていない。

- **DC1:** `PlayerProcessRun.ReadGameLog` が game の log を `FileShare.ReadWrite | FileShare.Delete` で開く（Player の log の `ReadLog` と同じ指定）。`GameLogHas` と case の後の読みは同じ口を通る。
- **DC2:** `PlayerOverlayLaunchTests` に `[SetUp]` を足し、test ごとに呼び出しの列・書いた記録・起動の要求を空にする。製品の `PlayerOverlayLaunch` は変えていない。
- **DC3:** `PlayerBuildProjectMutation.Dispose` が値を戻した後、`ProjectSettings/ProjectSettings.asset` の object に dirty を付けて `AssetDatabase.SaveAssets()` を呼び、戻したメモリの値を disk に書く（§7 の観測で、Editor の終了の時の保存が元の bytes を書いたのと同じ内容）。build の前処理が足して後処理が外す preloaded asset と、Addressables の設定の戻しも同じ保存で disk に入る。指紋の比較から外す規則は足していない（u10 の Phase A の条件に当たらない）。
- 確認: `pwsh tools/contract-audit.ps1` は `errors=0 warnings=0`。この worktree の Editor（Unity 6000.6.9f1）を開いて compile を待ち、`EditorUtility.scriptCompilationFailed` が false、読み込まれた assembly に 3 件の新しい method（`SaveRestoredSettings`、`ResetRecordedCalls`、`ReadSharedLines`）があることを確かめた。Editor は自分で開いた物だけを閉じ、閉じた後の作業ツリーに URP の global settings の書き換えは無かった。
- 未実行（Phase C の責任）: Unity の test（DC2 の 4 件を含む）、X1 での DC3 の確認、Y03・Y04 以降の判定の case。

## 7. Phase C

### 発見 C（1 巡目）

- 種別: 発見。GO 判定はしない。C' も起動しない。
- 対象: implementation base `1fd200b5`、head `ae49bebf`（引き継ぐ commit `ba6fa74d`。作業ツリーは clean で HEAD と一致、`ae49bebf` からの差は本書だけ、PR #212 の状態ブロックの引き継ぐ commit も同じ）。
- evidence の状態: 取得案内は無い（bundle・snapshot は未生成。commit で固定した head の差分を直接読んだ）。限定実行の生の証拠は git 管理外（X・Y の worktree の `TestResults/` と進行役の作業 directory）に残り、判定の証拠には使わない。
- 機械検査: `contract-audit` errors 0・warnings 0。
- 構造適合: §3 の責務マップのとおり。Runtime に判定と digest、SampleGame の Editor に Player の決定・指紋・入口、test の補助は test の側。新しい asmdef 参照・公開 API の逸脱は無い。`PlayerOverlayEndToEndTests.cs` は 955 行で予想（480）と 500 行の警報を超えるが、判定の case の列という 1 責務で、process の操作は `PlayerProcessRun`、製品の経路は `PlayerOverlayLaunch` に分かれているので分割は求めない。
- 限定実行（§5 の「発見 C の最初の限定実行」。Unity 6000.6.9f1、sandbox 外、新しい worktree X・Y を `ba6fa74d` から作成、run `dc1`）:
  - 最初の import を兼ねて、X で起点 filter と新しい単体テストの class（`ContentOverlayStartupTests`、`ContentOverlayDigestTests`、`PlayerContentConfigurationTests`、`PlayerCompatibilityTests`、`PlayablePlayerLocatorTests`、`PlayerOverlayStartupObserverTests`、`PlayerOverlayLaunchTests`、`PlayerInputFilesTests`、`PlayEntryCandidatesTests`）: 116 件、Passed 112、Failed 4（全て `PlayerOverlayLaunchTests`。DC2）。Y は `PlayerInputFilesTests` 1 件 Passed。どちらも最初の起動の後に URP の global settings を戻した。
  - driver の X1・写し・Y01〜Y04 と X2: X1 Passed（XML。record は DC3）、写し（identity `20261124T130352253Z-5ff033e6de0d4875bb11c309e52bfb89`）、Y01（AC8 (d)）Passed、Y02（install）Passed、Y03（AC8 (a)）Failed、Y04（AC1）Failed（どちらも DC1）、X2 Passed（差分 0 件。driver が case の後に checkout を戻した後の新しい process）。全段で `git status --porcelain` は前後で一致。X1 の後に Unity の終了 stall（XML の 5 分後に 1 thread で CPU 時間が増加）が出て、その PID だけを CIM の `Terminate` で止めた（Harness README の既知の事象）。
  - 製品の経路の観測（判定ではない）: Y03 は `outcome` `LaunchedWithoutOverlay`・`startup` `Observed`、Player の log に `Content directory registered: … from=installed` → `Bootstrap UICommon: source=content` → `Bootstrap SceneResourceMap: source=content hash=…` → 初回 Scene `stable=True`。Y04 は `Launched`・`Observed`・上書きの root `Title`、`Content overlay registered: identity=<上書き> baseRevision=<Player の identity>` → installed の登録 → UICommon・map とも content → 初回 Scene `stable=True`。上書きの前の経路と上書きの経路は、test が落ちた点（game の log の読み）の前まで動いている。
  - DC3 の観測（Phase C の限定実行。X1 を driver を通さずに 2 回、run `dc1b`・`dc1c`）: build を返した同じ process の比較で 2 回とも `ProjectSettings/ProjectSettings.asset (changed)`。2 回目は X1 の間、disk の file を 0.5 秒ごとに読んで版を写した: build の途中で disk が mutation の値（Standalone の scripting backend `1`、stripping `3`、`preloadedAssets` に `Assets/InputSystem_Actions.inputactions`）に書かれ、Unity の終了まで変わらず、終了時に元の bytes へ戻った。Player の `player-compat.json` の hash は commit の file と同じ。
- 現在の問いを阻害する指摘:
  - **DC1（B 適応）:** 判定の test が、動いている Player の game の log を共有の指定なしで開き（`PlayerProcessRun.cs:154` の `File.ReadLines`、`PlayerOverlayEndToEndTests.cs:726–727` の `GameLogHas` から待ちの中で呼ぶ）、`IOException: Sharing violation` で落ちる。Y03・Y04 の両方で、Title の画像・「スタート」・閉じる段の前に止まる。違反: AC8 (a)・AC1（判定の case が条件を読めない）。
  - **DC2（B 適応）:** `PlayerOverlayLaunchTests` の `_calls`（`PlayerOverlayLaunchTests.cs:24`）が fixture の field で、test ごとに消されないので、前の test の呼び出しの列が後の test の順の assert に混ざり、`NoEdits_LaunchesWithoutOverlayKeys`、`SavingChangesAPlayerInput_StopsAsPlayerInputDiffers_BeforePrepareAndLaunch`、`OverlayChangedBetweenTheRecordAndTheRecheck_StopsWithoutAProcess`、`Reused_LaunchesWithoutBuilding_AndRecordsReused` が落ちる。製品の `PlayerOverlayLaunch.Run` の順の違反を示す証拠は無い。違反: AC9・AC4 (a)（単体テストが条件を証明しない）。
  - **DC3（B 適応）:** 遊べる Player の build の mutation（`PlayerBuildProjectMutation`）が設定した値が、build の途中の `AssetDatabase.SaveAssets()`（`CopyGraphClosure`）で `ProjectSettings.asset` に保存され、`Dispose` の戻しはメモリの上だけで保存しないので、build を返した同じ Editor の disk の指紋が Player の `player-compat.json` と違う。違反: AC17 の 1 つ目の時点（差分 0 件）と u10（build の後の `ProjectSettings/` が build の前と同じ bytes）。利用者が build した Editor のまま入口を使うと `PlayerInputDiffers` で止まる。修正先: 戻しを disk に反映させる修正は指紋の範囲を変えないので B 適応（D11「言えない物で不一致が出たら Phase A へ戻す」、u10「指紋の比較から外す規則を `PlayerCompatibility` に足すのは Phase A」は、除外を足す場合の規則）。
- 後続へ移送する指摘（違反根拠なし）: なし。
- 未確認事項: AC1 の画像と「スタート」の後の Home（u6 を含む）、AC8 (a) の閉じる段と exit code、u4、AC8 (c)（§6.3）、driver の Y05 以降と X の残りの段、全 EditMode 回帰。限定実行の run `dc1`・`dc1b`・`dc1c` は判定の証拠に使わない。
- 担当・モデル・ベンダー: レビュー（構造・契約・指摘の分類）は Codex CLI（`codex exec`、読み取り専用の sandbox）、`gpt-6-luna`、effort medium、OpenAI。新しい session で 1 回、追加の証拠で同じ session を 1 回再開した。Unity の実行・証拠の梱包・DC3 の観測・この欄の転記は進行役（Claude Code の desktop の session、Claude Opus 5.5、Anthropic。Phase B の session とは別）が行った。
- 独立性: レビューは Phase B（Claude Opus 5.5、Anthropic）と違うモデル・ベンダー。実行担当の進行役は Phase B と同じモデルで、凍結した手順と限定実行だけを行い、合否は書いていない。

## 8. Phase C'

（未着手）

## 9. Phase D

（未着手）
