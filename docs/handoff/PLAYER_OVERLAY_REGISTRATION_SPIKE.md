# Windows の Player での上書きの登録の実測

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（スパイク。[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S4-0。S4 の A1 として S4 の行の分割も持つ。§2.4）
- status: `C'`（判定 C が 2026-11-23 に GO とした。スパイクの答えは「同じ」。次は C'（最終 C'）。§7）
- branch: `claude/player-overlay-registration-spike`（PR の base は `develop`）
- implementation base commit: `d926e3b6b40dafab086b293e6d60784e304ecf4c`（`develop` の head、2026-11-23。§1 の現況と A2 の固定 base は 1 つ前の `214806c1` で、2 つの差は program の S5-0 の行の 1 行だけである）
- implementation head commit: `b101b3820f7538f9e6e8779332d1405e32b316b5`
- A3 凍結記録: 承認者は root（owner）、2026-11-23、対象は revision 0（この記録を含む commit。外部計画レビューの 3 巡目が見た commit `2c24044d` の本書と program に、凍結の記録・引継ぎ票・3 巡目の記録・承認済みの語への置き換えだけを足した物で、目的・条件・責務マップは変えていない）。承認の根拠: 外部計画レビューが 3 巡目で阻害する指摘なしとして凍結へ進むことを勧め、owner が凍結と Phase B の開始を指示した。この指示で、凍結の時に承認するものとして本書が挙げていた 2 点（§1 の目的・利用者の文言、§2.4 の S4 の分割）も承認された
- risk: `high`（製品 code は変えない。結論が S4 の後続の行の形（実機へ編集を届ける方式）を決め、Player を build して起動する harness はこの project で初めてで、独立して失敗しうる未知が複数ある。§5）
- owner: root（依頼者）
- created: 2026-11-23
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: [CONTENT_DIRECTORY_BUILD_FACTS.md](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)（Player の実測の節を足し、上書きの 2 節と測り直しの節の「測っていないもの」の Player の語を実測の範囲に合わせて直し、「測り直し」に harness の commit と、Player の構成の変え方を書く）、[13 章](../../unity/Assets/Docs/Architecture/13-resource-system.md)「base と上書きの登録契約」の「範囲」と「測った設定」（Player は未測定、の語を実測の範囲に合わせて直す。Player に重ねることの禁止を解くかは S4-2 が決め、本スライスは解かない）と、同じ章が送り先を「S4」と書く 3 か所（「id による影」「測った設定」「まだ保証しないこと」）を分割後の行の名前に直すこと、program §5.3 と §8（本スライスの行、S4 の分割の確定）。実験 code は製品に残さず git 履歴に置く（S0-b・S2-0・S2-0b・S2-1 と同じ）
- 関連: [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md)（§8 の S4 の行と「S4 の A1 への入力（2026-11-23）」）、[CONTENT_DIRECTORY_BUILD_FACTS.md](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)（比べる相手の Editor の実測と harness の所在）
- Phase A snapshot の取得案内・状態: 該当なし（機械向けの期待値・照合 script は無い。§5。レビューの入力は本書だけ）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: ローカル固定済み（未昇格）。判定 C が implementation head で生成し、task `s4-0-player-overlay-registration-spike` に `evidence stage` した（判定用、21 file）。取得案内は進行役の共有メモ（git 管理外）の判定用の file にある。別の pwsh process の fetch で `passed`、README・(3) の XML・record の zip を読めた。受け取り側の別 session の fetch・閲覧は未確認
- C' blind bundle の取得案内・状態: ローカル固定済み（未昇格）。同じ head・同じ生結果から作り、同じ task に `evidence stage` した（19 file）。取得案内は進行役の共有メモに、判定用とは別の file で置いた。別の pwsh process の fetch で `passed`。受け取り側の fetch・閲覧の確認は C' の起動前に行う
- 引継ぎ票:
  - 次の Phase・役割: C'（独立監査、1 巡目）。判定 C の判定証拠が揃った implementation head（§0 の欄）を、C' blind bundle だけから監査する。§5 が予約した、Phase A〜C に関与していない系列の担当で行い（選ぶのは進行役。`cursor-agent` を使うなら Grok 系だけ）、使った担当・モデルと独立性を §8 に書く。高コスト証拠は無いので、この C' が最終 C' である。
  - 次の作業: C' blind bundle を fetch して閲覧を確かめてから監査し、結果を §8 に記録する（記録は進行役が C' の出力から書いてよい）。
  - 開始時の照合: C' は PR を読まない。bundle の README の base・head と、取得案内の base・head が一致すること。fetch が期待 packageSha256 で `passed` になること。食い違えば監査せずに止まる。
  - 読む範囲: C' は blind bundle だけ（README が目次）。本書を読まない。
  - 引き継ぐ commit: この票を含む commit（判定 C の記録 commit。SHA は起動指示と PR #209 の状態ブロックで渡す）。implementation head は欄の SHA のまま動かない。
  - 入力の snapshot・bundle: C' blind bundle（上の欄）。C' 用決定記録は無い（1 巡目）。Phase A snapshot と Phase B result snapshot は作っていない（blind bundle が凍結版 `ae1e1614` の本書全文と、Phase B の出口の commit `16328d4d` の §6 を収録する）。
  - 必要な権限: C' blind bundle の fetch（非仮想化の pwsh、sandbox 外、Artifacts の Release build 済みの checkout）。Unity の再実行は必須でない。push はしない。
  - 停止規則: C' の規則（osm-workflow の phase-c-prime.md）。C' の差し戻しは別に数えて上限 3 巡。
  - 独立性: Phase A〜C は全て Anthropic の系列（A2 と外部計画レビューの系列は §5.1）。C' は未関与のベンダーを優先し、残らない時は独立性の制約を §8 に記録する。
  - 引き渡さないもの: C' に、本書（特に §6 の差し戻しの小節、§7 の発見 C・判定 C の所見と結論）、判定用の evidence bundle とその取得案内、PR の本文と会話を渡さない。

## 1. 目的と対象外

- 目的: 取得した 1 つの目的 build（base）に、開発者が手元で build した上書き（自分が触った Scene と asset だけの Content Directory）を base → 上書きの順に登録した時、Windows の Player でも Editor の Play の実測と同じ答えになるか（上書きに入れた物は上書き側が読まれるか、上書きに入れていない物も含めて component に正しい class と値が付くか、同じ asset の実体の数は Editor の実測と同じか、読んだ物が登録の解除まで壊れないか）を、Unity 6000.6.9f1 の Windows の Player の実測だけで確かめる。その結果から、owner が「編集を実機へ届ける方式として、Player に上書きを重ねる形を進めるか、見直すか」を、供給元（Local / LAN）の設計に入る前に決められるようにする。
- 利用者: OSMFramework の開発者（S4 の後続の行で、実機へ編集を届ける経路と content の供給元を設計する owner と、それを実装する人）。本スライスに実行時の利用者の操作は無い。
- 対象外（所有する後続は §2.3）:
  - 上書きを製品の Player に載せること（起動が Player の構成で上書きの key を拒む `ContentOverlayStartup.Decide`、13 章「base と上書きの登録契約」の「範囲」の禁止、Player の base の検証、起動の設定）。本スライスは製品 code を変えず、13 章の禁止も解かない。owner の入力の「検証済みの base」は、本スライスでは製品の検証（publish → install → receipt の照合）を通した base ではなく、「build の時に棚卸しした directory と、Player が登録する directory が同じ bytes だと digest で照合した base」と読む。測るのは Unity の native の登録の答えで、製品の検証と登録の口を通した確認は S4-2 が持つ。
  - content directory から起動して操作を続けられる Player の構成。
  - 別の PC・別の `Library` で build した base。本スライスは 1 台の PC・1 つの `Library` で base・Player・上書きを build する。
  - Player の構成の組み合わせ（Mono、stripping の水準の違い、development build、窓を出した通常の起動、`-nographics` の起動）。測るのは、製品の BS4 の Player と同じ IL2CPP・managed stripping High の 1 つで、起動は `-batchmode`（窓は出さないが graphics の device は作る。`-nographics` は付けない）だけである。
  - Android・iOS。
  - 上書きが、Player に無い型を使う場合（Player を build した後に足した script、base が使わない Unity の組み込みの型と module）と、script の code・field を変えた場合。前者は Unity の公開文書が既に書く制約（現況）で、2 つの directory の登録の問いではない。
  - 登録の順が上書き → base の時（製品は順を base → 上書きに固定している。測らない）。
  - Editor の Play の測り直し。比べる相手は、6000.6.9f1 で測り直し済みの Editor の実測（現況）である。Player の答えが「同じ」でなかった時だけ、同じ runner を Editor の Play で走らせて差の出所を切り分ける（AC6）。
  - Local / LAN の供給元、Play の入口の prefetch、要求の時点の install。
  - 性能と所要（build と起動の所要は record に残るが、判断の基準にしない）。
- 現況（base `214806c1`）:
  - **owner が受けた入力（program §8「S4 の A1 への入力（2026-11-23）」の (2)）:** S4 の A1 は、Local / LAN の供給元の設計に入る前に、検証済みの base に上書きを重ねた Windows の Player で、Editor の実測と同じ答えになるかを確かめる gate を最初に置く。比べるのは、実測の事実の「同じ id で中身の違う file を持つ 2 つの directory の登録（上書き）」と「6000.6.9f1 での上書きと登録の測り直し」の、登録の順が base → 上書きの時の版と実体の数である。実験は製品の起動経路の外で行い、code を製品に残さない。答えが否定的なら結果を owner へ返し、実機へ編集を届ける方式を見直すかは owner が裁定する。
  - **比べる相手の Editor の実測（[実測の事実](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)。6000.6.0f1 で測り、2026-11-18 に 6000.6.9f1 で測り直して全て同じだった。Editor の Play、Domain Reload 無効、`-nographics` の batch。製品の起動は addressables の設定で成功した状態だった）:** 登録の順が base → 上書きの時、(a) 両方にある Scene（`SceneEdit`）と texture（`EditedTex`）は、base の root から引いた id でも上書きの root から引いた id でも上書きの版が出て、2 つの root の Scene の id と `LoadableObjectId` は等しく、両方の root から load した `EditedTex` の実体は 1 つだった。(b) base だけにある Scene、上書きの Scene、base の root から `Loadable` で load した prefab の component は、位置ごとに正しい class の完全名と値で、Missing は 0 だった。(c) base だけにある Scene の Renderer が直接参照する material の `mainTexture`（編集した texture）は base の版のままだった。(d) 実体の数は、両方の directory にある共通の texture・material・shader と編集した texture・material が 2 つずつ、base だけにある prefab と texture が 1 つずつだった。(e) Scene の unload と load し直しを繰り返しても保持している物は壊れず、Error も Exception も出ず、上書き → base の順で解除して登録は 0 件になった。
  - **製品の契約:** [13 章](../../unity/Assets/Docs/Architecture/13-resource-system.md)「base と上書きの登録契約」は「上書きは Editor の Play だけで使い、Player は 1 つの目的 build だけを載せる（Player は未測定）」と書く。起動は `ContentOverlayStartup.Decide`（`Scripts/Runtime/Bootstrap/ContentOverlayStartup.cs`）が、Player の構成か Editor の Play でない時に上書きの key を `InvalidConfiguration` で拒む。上書きを重ねられるのは検証済みの base（installed の pair）だけで、Player に同梱した directory には重ねられない。
  - **Unity の公開文書（6000.6 の manual「How code stripping affects content」「Unity linker marking rules reference」、2026-11-23 に確認）が言うこと:** Player と別に build した content は、Player から strip された型を使えない（AssetBundle・Addressables・content directory に共通）。content directory の build は使った C# の型と Unity の object の型を build report の directory の `ScriptsOnlyCache.yaml` に記録し、content を先に build してその directory を `BuildPlayerOptions.previousBuildReportDirectories` に渡すと、Player がその型（と、その型の module）を残す。managed stripping が Medium と High の時、asmdef の assembly の `MonoBehaviour` と `ScriptableObject` の派生型は root として残る。**言わないこと:** Player で 2 つの content directory を登録した時に、同じ id で中身の違う file のどちらを読むか。Player を build した後に作った content directory（上書き）の型の記録が、既にある Player に対してどう扱われるか。Editor の Play と Player で、登録中の directory の探索の順が同じか。
  - **製品の Player の build（SampleGame の BS4。`SampleGame/DependOnAll/Editor/Build/` の `SampleGamePlayerBuildCoordinator`・`UnityPlayerBuildAdapter`・`PlayerBuildProjectMutation`・`PlayerBuildReportVerifier`）:** content を build してから、`BuildPipeline.BuildPlayer` を StandaloneWindows64・IL2CPP・managed stripping High・`BuildOptions.DetailedBuildReport`・Scene は bootstrap の 1 つ・`previousBuildReportDirectories` は content の build report の directory・`extraScriptingDefines` は空で呼ぶ。build の間だけ、scripting backend・stripping の水準・Addressables の「Player と一緒に build する」設定を変え、終わりに戻す（project の今の値は Mono・stripping なし・Addressables は既定）。build report の packed assets で、content の root が Player に重複して入っていないことを確かめる。content は exe の横の `content` に写す。入口は menu だけで、Player を build して起動する test と script は無い。Player の build の所要の記録は無い。Player の証拠は全て 6000.6.0f1 のものである（program §8「Unity 6000.6.9f1 への更新から移送した入力」）。この PC の 6000.6.9f1 の Editor に、Windows の IL2CPP の Player の support の folder があることは確かめた（C++ の toolchain までは確かめていない）。
  - **製品の起動と、この project から build した Player（code を読んだ範囲。実行していない）:** SampleGame の `AppInitializer` は `[RuntimeInitializeOnLoadMethod]` の 3 つの段を持ち、この project から build した Player では必ず走る。動作確認の Player の経路（`UseRequiredPlayerFileConfiguration`）に入るのは、最初の Scene の名前が `UICommon` の時だけである。それ以外の Player で、設定に `content:runtimeMode` が無ければ、`BeforeSceneLoad` は例外になり、捕まえて log に出し、`ReleaseAll` で部分初期化を回収し、`AfterSceneLoad` は Error を 1 行（「BeforeSceneLoad が失敗したため AfterSceneLoad をスキップします」）出して何もしない（`Application.Quit` は呼ばない）。設定は、Addressables 経由の JSON（同期の load。Player に Addressables の data が無ければ失敗する）、`SAMPLEGAME_` で始まる環境変数、command line の `-` で始まる全ての引数、の順に重なる。`content:runtimeMode` が `addressables` で入ると、`BeforeSceneLoad` は成功し、製品の起動が常駐の service を作って非同期の起動へ進む。この repo の Harness と互換経路の手順は、環境変数 `SAMPLEGAME_CONTENT__RUNTIMEMODE` を使う。
  - **製品の native 層:** `UnityContentDirectoryBackend`（`Scripts/Runtime/BuildContent/`、133 行）は internal で、`InternalsVisibleTo` の相手は test の assembly と `OneStarMaker.Editor` だけである（どれも Player の compile に入らない）。backend が呼ぶのは Unity の公開 API だけである（`ContentLoadManager.RegisterContentDirectory`・`GetRootAssets<BuildContentRoot>`・`UnregisterContentDirectory`、`Loadable<T>.LoadAsync`・`Release`、`SceneManager.LoadSceneAsync(LoadableSceneId, …)`・`GetSceneByLoadableSceneId`・`UnloadSceneAsync`）。root の型 `BuildContentRoot` と `BuildContentEntry` は public で、`BuildIdentity`・`Entries`・entry の `LogicalKey`・`SceneId`・`Object`（`Loadable<Object>`）を読める。
  - **前例の harness（S2-1、commit `a304963b`、約 3,200 行。Editor の Play 用）:** fixture の生成（固定 GUID、v1 と v2）、2 回の build と棚卸し、前提 P1〜P3、round の観測（版、位置ごとの class と値、Missing、実体の数、壊れ方）、JSONL の record、Unity を使わない判定の写像を持つ。round の観測の本体（登録、load、`GetComponents`、`JsonUtility`、`GetPixels32`、`FindObjectsOfTypeAll`、`EntityId`）は runtime の API だけで書かれている。fixture・build・Play の往復・log の捕捉（`LogAssert`）は Editor と test framework に依る。probe の `MonoBehaviour` は、Player の compile に入るよう、test の制約の無い、参照を持たない asmdef に置いた。補助の判定 code は、対照の順の class を上書きの版が出る前提で読む既知の誤りを持つ（実測の事実「測り直し」）。
  - Unity 上では何も実行していない。
- UC ごとの利用者の入力: `なし`（開発時の宣言も実行時の操作も無い）。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 取得した 1 つの build（以下 base）に、手元で build した上書きを base → 上書きの順に登録した Windows の Player で、次の 4 つが Editor の Play の実測と同じ答えになるか。
  - **Q1 読まれる側:** base と上書きの両方にある Scene と asset を要求した時、上書き側の中身が読まれるか。
  - **Q2 class:** base にだけある Scene と prefab、上書きの Scene の component に、正しい class と値が付くか。
  - **Q3 実体の数:** 同じ asset の実体の数が、Editor の実測と同じか。
  - **Q4 寿命:** 上書きを解除しない限り、Scene の出入りと `Loadable` の保持・解放を繰り返しても、読んだ物が解除まで壊れないか。
- 成果の範囲: 結論は「この PC、Unity 6000.6.9f1、StandaloneWindows64 の Player（IL2CPP、managed stripping High、development build でない、`-batchmode` の起動で graphics の device あり）、loose file、合成 fixture、base・Player・上書きを同じ `Library` で build、Player は base の build report を渡して build し、上書きはその後に build、base は製品の検証を通さない directory、製品の起動は設定が無く失敗して常駐の service が無い状態」に限る。record の先頭に書き、別の PC の base・実データ・他の Player の構成へ広げない。比べる相手の Editor の実測は、`-nographics` で、製品の起動が成功した状態で取られているので、host（Editor / Player）、graphics の device の有無、製品の起動の状態の 3 つが一緒に変わる。観測は CPU 側の値（名前、class、値、読める texture の pixel、`EntityId`）で、GPU 側の資源は測らない。言えるのは「測った範囲でこう振る舞った」までで、「設計上そうなる」は言わない。
- どうすればこの問いに誤った答えを出せるか（C と C' はこれを探す）:
  - (a) 危険な条件が fixture に入っていない。script をまとめた file が base と上書きでたまたま同じ中身になる、編集したはずの Scene・asset の file が同じ bytes になる（前提 P1〜P3。満たさなければ未判定）。
  - (b) Editor で取った record を Player の物として読む（record の先頭を、round を実行した process 自身が書く。`Application.isEditor` が偽、platform、scripting backend、Unity の版、`Application.buildGUID`、process の id。launcher は、起動する Player の出力の棚卸しの digest を build の時の値と照合する）。
  - (c) Player が上書きを登録していない、または順が違う（`Register` を呼んだ順、handle が有効か、登録の一覧の件数、handle ごとの root の `BuildIdentity` を record に残し、round の定義と照合する）。
  - (d) Player の中の物が Content Directory 由来でない。fixture の asset が Player 本体（Scene、`Resources`）に入る（Player の Scene は runner だけを持つ Scene 1 つにし、Player の build report の packed assets に fixture の asset が無いことを確かめ、登録の前に fixture の名前の物が 0 件であることを記録する）。
  - (e) 古い build の出力を読む。前の実行の Player や directory が残っている、Player の出力の一部が別の build の物に替わっている（実行 id ごとの一時 root。launcher は起動の前に、2 つの directory と Player の出力の棚卸しの digest を第 1 process の record と照合し、runner の `Application.buildGUID` を build report の値と照合する）。
  - (f) 「null でない」だけを見て別の class が付いたのを見逃す。型の集合だけを比べて位置の入れ替わりを見逃す。Missing を数えない。解決できない script の component が、`null` として残らずに列から落ちて消える場合を見逃す（`GetComponents<Component>()` の並びを、fixture が付けた期待の列と、長さ・位置ごとの class の完全名・component ごとに違う値で照合し、`== null` を数え、script が無いことを言う Unity の log の行も数える）。
  - (g) 上書き版か base 版かを区別できない物で Q1 を答える（版ごとに違う名前の GameObject と、版ごとに違う pixel）。
  - (h) 期待値を、測る対象と同じ実行から採る（期待値は fixture の定義から採る。Editor の実測の数は実測の事実の文から採る。単独の基準は、その経路が Player で成り立つことの確認に使う）。
  - (i) Unity の Error・Warning を握りつぶす（Player の log の file を全て証拠に入れ、runner は開始の印より後の log を段に結びつけて record に残す）。登録の段や解除の段の Error・Exception を数えずに「同じ」「違うが成り立つ」と答える（X3 は round の全ての段を数え、1 件でもあれば「成り立つ」側に入れない）。製品の起動が出す log を round の log と混ぜる（round は製品の `AfterSceneLoad` の後に始める。前提 P0）。
  - (j) 1 回の観測で答える（各 round を 3 つの Player の process で行う。単独の基準で観測の行が違えば未判定、主の順で違えば、それ自体を Editor との違いとして答えに入れ、最も悪い process の答えを採る）。
  - (k) Player が落ちた・終わらなかった・load が失敗したことを、harness の失敗や未判定として捨てる。失敗を記録して終了行まで書いた round と、落ちて終了行の無い round とで、同じ失敗の答えが変わる。または逆に、harness の不備・上書きと無関係な失敗を Unity の答えとして読む（下の「答えにできる失敗」の条件。終了行の有無に依らない。単独の基準は、主の順と同じ操作の列を 1 つの directory で踏む）。
  - (q) 主の順でだけ起きた異常（中身がどの版でもない、参照をたどった数と列挙の数が合わない、process の間で観測が違う）を、数え方や harness の不成立として未判定にする（単独の基準で読み方と数え方が成り立っていれば、主の順の異常は答えに入れる）。
  - (l) 上書きだけが使う script の型が Player に無いことによる Missing と、2 つの directory の登録による class の誤りを区別しない（前提 P4 を record に残し、答えに原因を添える）。型を残す仕掛けを harness が足して、この答えを変える（§4 の禁止）。
  - (m) 前例の補助の判定 code の誤りを写す（判定の写像は、対照の順の語を除いて写す）。
  - (n) runner（登録と load を Unity の API で直に呼ぶ新しい code）の誤りによる差を、Player の挙動の差として読む（単独の基準で観測の読み方を確かめる。答えが「同じ」でない時は、同じ runner を Editor の Play で走らせ、Editor の実測を再現するかを見てから差を host に帰す。AC6）。
  - (o) runner の型と probe の型を同じ assembly に置き、Player の Scene が runner を使うことで assembly 全体が残り、「上書きだけが使う script の型が Player に残るか」（P4）の答えを harness が変える（probe は、content からしか使われない別の assembly に置く。§3）。
  - (p) 製品の起動が成功して動いている Player で測る。親の process の `SAMPLEGAME_CONTENT__RUNTIMEMODE` を Player が継承する等（launcher は `SAMPLEGAME_` で始まる環境変数を除いた環境で Player を起動する。前提 P0 が、製品の起動が止まったことを Player の log で確かめる）。
- 判定に使う語（実測の前に固定する）:
  - **base / 上書き / Player:** fixture の source を v1 の状態で build した directory が base。base の build の直後に、base の build report の directory を渡して build した実行 file とその data が Player。その後に source を v2 に変え、一部の root だけを build した directory が上書き。**主の順** は base → 上書きの登録である。
  - **round:** Player の process 1 つが行う、登録・観測・解除の 1 組。round の名前と登録の順は、launcher が process の環境変数で渡す（製品の設定の読み口が読むのは command line の `-` で始まる引数と `SAMPLEGAME_` で始まる環境変数なので、どちらにも当たらない名前の環境変数にする。command line に足すのは `-batchmode` と `-logFile` だけ）。1 つの process は 1 つの round だけを行うので、どの round も、その process で最初に content を読む。runner は、Scene の `Start` 以降（製品の `AfterSceneLoad` の後）に round を始め、始める時に開始の印を `Debug.Log` で 1 行出す。process の開始からその印までを「起動」の段と呼ぶ。
  - **record:** round ごとに、Player の process が自分で書く JSONL の file 1 つ（process ごとに別の file。行ごとに flush し、行は書いた時刻を持つ）。launcher は別の file に、起動（pid、開始時刻、渡した環境変数、除いた環境変数の名前）、終わり（終了 code、所要）、停止（上限の超過と、止めた時刻）だけを書く。launcher は観測の行を書かず、Player の log の file はそのまま証拠に入れる。
  - **操作と、その失敗:** runner が行う操作（`Register`、Scene の load、`Loadable` の load、Scene の unload、`UnloadUnusedAssets`、`Release`、`Unregister`）は、それぞれ開始の行と結果の行を record に書く。失敗は、例外、無効な handle、`null` か型の違う結果、load 済みにならない Scene、のどれかで、結果の行に、対象の logical key と id、呼び出し、失敗の種類、例外の型と文面を書く。操作が失敗したら、runner はその round の残りの観測を行わず、後始末（`finally` の Release・unload・解除）へ進み、打ち切った段を終了行に書く。runner の上限の timer が process を終える時は、その前に上限の超過の行を書く。
  - **危険な条件の前提（P1〜P3。S2-1 と同じ）:** 両 directory の出力の棚卸し（名前・size・sha256）と `ContentLayout.json` から、登録の前に確かめる。P1: MonoScript をまとめた file が両方にあり、id が同じで sha256 が違い、その file の layout の `SourceAssets` の一覧について、base の一覧に `ProbeBaseOnly` と `ProbeBoth` があって `ProbeOverlayOnly` が無く、上書きの一覧に `ProbeOverlayOnly` と `ProbeBoth` があって `ProbeBaseOnly` が無い（一覧の path は script の `.meta` の GUID と結びつけて照合する）。P2: `SceneEdit` と `EditedTex` の file が両方にあり、id が同じで sha256 が違う。P3: `SharedTex`・`SharedMat`・`SharedShader`・`EditedMat` の file が両方にあり、名前と sha256 が同じである。帰属は固定 GUID で引き、名前で対応づけない。
  - **製品の起動が止まっている前提（P0）:** (1) Player の log の file の「起動」の段に、製品の `AfterSceneLoad` の 1 行（「BeforeSceneLoad が失敗したため AfterSceneLoad をスキップします」）がある。(2) round の登録の前に、登録中の content directory が 0 件、load 済みの Scene が runner の Scene 1 つだけ、fixture の名前の物が 0 件である。runner は、その時点の root の GameObject（`DontDestroyOnLoad` の物を含む）の名前の一覧も record に残す（参考で、P0 の成否には使わない）。
  - **Player の型の前提（P4）:** runner が登録の前に、probe の 3 型（`ProbeBaseOnly`・`ProbeBoth`・`ProbeOverlayOnly`）のそれぞれが Player の assembly にあるかを、型の完全名と assembly 名の文字列から引いて record に残す。第 1 process は、Player の出力の scripting assembly の一覧に probe の assembly があるかも記録する。
  - **版:** `SceneEdit` は root の GameObject の名前（v1 は `Marker_v1`、v2 は `Marker_v2`）、`EditedTex` は pixel の sha256（v1 と v2 で違う単色）。load は成功したが中身が v1 でも v2 でもない時は「どの版でもない」と読む（単独の基準では期待と違う、主の順では Q1 の不成立）。
  - **正しい class:** 決めた GameObject の `GetComponents<Component>()` に `== null` が無く、probe でない物を除いた列が、fixture が付けた順の期待の列と、長さも位置ごとの中身も一致すること（`GetType().FullName` と、`JsonUtility.ToJson` で読む `_tag`・`_value`。値は全ての probe の component で互いに違える）。
  - **実体の数:** load の結果から参照をたどって集めた、相異なる `EntityId`（`EntityId.ToULong(GetEntityId())`）の個数。`Resources.FindObjectsOfTypeAll` の同名・同型の列挙と照合する。単独の基準で 2 つの集合が一致すること（数え方の成立）を先に確かめる。主の順で 2 つが違う時は、多い方の数を使い、違いを Editor との違いとして挙げる。物理メモリの量ではない。
  - **壊れた:** 保持している物について、`== null` になる、texture の pixel の sha256 が load 直後と違う、material の `mainTexture`・`shader` が `== null` か別の `EntityId` になる、Renderer の `sharedMaterial` が `== null` になる、component の列（class の完全名と値、Missing の位置）が**その物を load した直後の列から変わる**、のどれか。偽 null は `== null` で判定する。最初から Missing の位置は、Missing のままなら壊れたに数えない（Q2 が受ける）。
  - **生の値と判定の分担:** Player の runner は期待値を持たず、確認点ごとに生の値（名前、`EntityId`、class の列と json、pixel の sha256、material・shader の参照、登録の一覧）だけを record に書く。版・正しい class・実体の数の整合・壊れたの判定は、Unity を使わない判定の写像が record の行から決める。runner が要る key と名前は、第 1 process が一時 root に書く fixture の事実の file から読む。
- 各問の答え（round の観測から機械的に決める）:
  - **Q1:** 物ごとに、**強**（base の root の id から要求しても、上書きの root の id から要求しても v2）、**弱**（上書きの root の id からは v2、base の root の id からは v1）、**不成立**（それ以外。上書きの root の id から要求して v2 が出ない、base の root の id から要求した物がどの版でもない）。load の操作が失敗した時は、下の「答えにできる失敗」で読む。`SceneEdit` と `EditedTex` の両方に付け、弱い方を Q1 の答えとする。
  - **Q2:** **正しい**（`SceneKeep`、上書き版の `SceneEdit`、base の root から load した `SharedPrefab` の全 probe が正しい class）、**誤り**（どれかが Missing・別の class・値の不一致・列の長さの違い）。誤りの時は、当たった component と、その型の P4 の値を添える。
  - **Q3:** **一致**（AC4 の計数の時点と AC5 の (1) の計数の時点の両方で、`SharedMat`・`SharedTex`・`SharedShader`・`EditedMat`・`EditedTex` の実体がそれぞれ 2、`SharedPrefab` と `KeepTex` が 1。Editor の実測と同じ数）、**収まるが違う**（前の 5 つがそれぞれ 1 か 2 で、後の 2 つが 1 だが、Editor の数と違う物がある）、**超える**（どれかが上の範囲を超える）。参照をたどった数と列挙の数が違う物は、多い方の数で読み、「一致」にしない。load の操作が全て成功しているのに 0 の物がある時だけ、未判定とする（数えられていない）。load の操作が失敗した時は、下の「答えにできる失敗」で読む。
  - **Q4:** **無傷**（AC5 の全ての確認点で、保持している物が壊れておらず、終わりの解除の後に登録が 0 件）、**壊れた**。
  - **Editor の実測にある、他の 3 つの点（同じ / 違うで読む）:** **X1** AC3 で、2 つの root の `SceneEdit` の id が等しく、`EditedTex` の `LoadableObjectId` が等しく、両方の root から load した `EditedTex` の `EntityId` が等しい（実体 1。Editor と同じ）。**X2** AC4 で、`SceneKeep` の Renderer が直接参照する `EditedMat` の `mainTexture` の版が v1（Editor と同じ）。**X3** 主の順の 3 つの round の全ての段（開始の印から終了行まで。登録、観測の各段、AC5 の (6) の Release・unload・解除）に、Unity の Error・Exception の log が 0 件（Editor と同じ）。起動の段（開始の印より前。製品の起動が出す log）と、終了行より後の log は数えず、記録して添える。Warning は数えず、全件を添える。
- スパイクの答え（主の順の round だけから決める）:
  - **同じ:** 3 つの process の全てで、Q1 強、Q2 正しい、Q3 一致、Q4 無傷で、X1・X2・X3 が全て Editor と同じであり、process の間で観測の行が一致する。
  - **違うが成り立つ:** 3 つの process の全てで、操作の失敗が無く、X3 が 0 件で、Q2 正しい、Q4 無傷、Q1 が強か弱、Q3 が一致か「収まるが違う」であり、「同じ」に当たらない。Editor と違った点（Q1 の弱、Q3 の数、X1、X2、process の間の観測の違い）を全て挙げる。
  - **成り立たない:** 上のどちらでもない。Q1 の不成立、Q2 の誤り、Q3 が超える、Q4 の壊れた、round の段の Error・Exception（X3）、「答えにできる失敗」のどれかが、1 つの process にでもある時である。どの process のどの問・どの操作がどう外れたかと、その log を添える。
  - process ごとに答えが違う時は、最も悪い答え（同じ < 違うが成り立つ < 成り立たない）を採る。
  - **process の間の違いの扱い:** 主の順で process の間の観測が違った時は、答えがどれでも、答えに「process の間で観測が違う」の印を付け、違った観測ごとに 3 つの process の値を並べ（どの round の、どの root・どの物が、どう違ったか）、3 つの process で同じだった観測も挙げる。同じ条件の再実行で結果が変わることは、Editor の実測に無い事実で、Q1 の弱や実体の数の違いのような「契約に取り込める違い」とは別に扱う（下の後続の表）。
- 判定定義。スパイクの合否は「答えが証拠付きで出たか」で決め、答えの向きは合否に使わない（S2-0b・S2-1 と同じ）。
  - **process の間の一致:** 同じ round の 3 つの process で、観測の行（版、id が等しいか、class の列と値、数、確認点ごとの壊れた項目、操作の成否、段ごとの Error・Exception の件数）が一致すること。`EntityId` の値そのもの、時刻、pid は比べない。単独の基準では一致を求め（しなければ未判定）、主の順では、一致しないこと自体を答えに入れる（上の「スパイクの答え」）。
  - **単独の基準が成り立つ:** AC2 の 3 つの round の全ての process で、終了行があり、操作が全て成功し、round の段の Error・Exception が 0 件で、観測（版、class の列と値、pixel、数と列挙の照合、確認点ごとの壊れた項目）が fixture の定義と一致し、process の間で一致すること（下の「型の欠落」に当たる所を除く）。
  - **未判定（結論を出さない）:** base・Player・上書きのどれかを build できない。P1〜P3 のどれかが成り立たない。Player の build report の packed assets に、runner の Scene 以外の fixture の asset がある。launcher が、2 つの directory か Player の出力の棚卸しの digest を第 1 process の record と照合できない。round の record の先頭が Player の process の物でない（`Application.isEditor` が真、backend が IL2CPP でない、Unity の版・実行 id・head・`Application.buildGUID` のどれかが第 1 process の record と違う）。P0 が成り立たない。record の開始行が無い round がある。単独の基準が成り立たない（Player の 1 つの directory の経路、読み方、数え方のどれかが成り立っていない。何が外れたかを結論に書く）。答えが「同じ」でなく、AC6 の Editor の対照が Editor の実測を再現しない（差が runner にありうる）。`Register` を呼んだ順が record に無いか、round の定義と違う。成功した登録の handle の root の `BuildIdentity` が、その directory の build の物と一致しない。主の順の round で、操作の失敗も無いのに終了行が無く、下の「答えにできる失敗」の条件を満たさない。主の順の round の 1 つ目の登録（base）が失敗した（同じ操作が単独の基準では成功している）。主の順で、load の操作が全て成功しているのに実体の数が 0 の物がある。launcher が起動した Player の process が、実行の後に残っている。harness・fixture・環境の問題は B へ戻し、Unity の挙動が定義した語で読めない時は Phase A へ戻す。
  - **GO（答えが出た）:** 未判定に当たらず、record からスパイクの答え（同じ / 違うが成り立つ / 成り立たない）が一意に読める。Q1〜Q4 と X1〜X3 は、読めた物を全て書く。操作の失敗か record の切れ目の後で観測できなかった物は、答えを作らず、「観測できなかった（どの操作の後か）」と書く。
    - **終了行のある round:** 終了行までの観測と操作の成否は有効である。終了行の後の process の終わり方（終了 code、終了待ちの超過、launcher による停止）は、どの round でも、答えにも基準の成否にも使わず、記録して添えるだけにする。launcher は終了行を見た後、30 秒の猶予で process を止めてよい。
    - **答えにできる失敗:** 主の順の round で、次のどちらかが起きた時は、下の条件が全て成り立てば、「成り立たない（登録できない / load できない / unload・解除できない / 落ちた / 終わらない）」という答えであり、未判定ではない。**(A) 操作の失敗が record にある**（2 つ目の `Register` か、それより後の操作。runner が失敗を記録して終了行まで書いた時も、その後に落ちた時も同じに読む）。**(B) record が途中で切れている**（終了行が無く、最後の行が 2 つ目の `Register` の呼び出しの開始の行か、それより後である。process が落ちた、runner の上限か launcher の上限で終わった）。条件: (1) その record に、先頭、成り立った P0、P4、1 つ目の登録の成功の行がある。(2) 単独の基準が成り立っている。(3) (B) で「終わらない」と答えるのは、止める直前の 60 秒の間、record に行が増えていない時だけである（runner が書く上限の超過の行は数えない）（行が増え続けたまま上限に達した時は未判定で、B が上限を見直す）。失敗した操作の行（対象、呼び出し、失敗の種類）、その段の log、最後に書いた record の行、終了 code を添える。条件を満たさない時は未判定とする。
    - **型の欠落:** P4 で `ProbeOverlayOnly` が Player に無く、上書きだけの基準と主の順の round で、`ProbeOverlayOnly` の位置の component だけが Missing（か列から欠けている）で、他の位置が全て正しい時は、未判定にせず、Q2 は誤り（原因は、上書きだけが使う script の型が Player に残らなかったこと）と答える。Q4 は上の「壊れた」の定義（load 直後からの変化）で読むので、この Missing だけでは壊れたにならない。この時、上書きだけの基準の 3 つの process の全てに同じ文面で出る Error は、型の欠落に伴う log として、単独の基準の成否と X3 に数えず、答えに添える（主の順の round に出た同じ文面の物も同じ）。`ProbeBaseOnly` か `ProbeBoth` が Player に無い時は、base の型の記録を Player の build に渡せていないので未判定（harness の問題）とする。
  - CONDITIONAL ACCEPT と NO-GO は使わない。「成り立たない」は GO の一形（答えが出た）である。
  - **test の合否と判定は別である。** launcher の test が assert するのは harness の成立だけ（全 round の process を起動して終わりを見届けた、record と log を集めた、後始末をした）。答えの向きは assert しない。XML の failed 0 は「harness が最後まで動いた」ことだけを意味する。判定は判定 C が record の観測の行から読む。判定の写像が書く答えの行は補助である。
  - 結論は、実測した事実と、仕組みの推測を分けて書く。
- **後続への含意（判定ではない。結論に添えて owner へ渡す。当たった行を全て書く）:**

| 答え・観測 | 後続 |
|---|---|
| 同じ | S4-2（上書きを Windows の Player に載せて起動する）の A1 を、この範囲の実測を前提に始められる。13 章の禁止は S4-2 が、製品の登録の口と検証済みの base で確かめてから解く |
| 違うが成り立つ（process の間の観測は一致） | 同上。違った点を S4-2 の契約の入力にする |
| 主の順で process の間の観測が違う（答えが「違うが成り立つ」の時を含む。答えがどれでも） | S4-2 を自動では始めない。観測ごとの揺れ方（常に同じだった観測、揺れた root・物、Editor の対照の 3 回の結果）を添えて owner へ返し、S4-2 が何を前提にしてよいか・進め方を owner が裁定する |
| 成り立たない（外れたのが Q2 だけで、その原因が型の欠落だけ。Q2 以外は「同じ」か「違うが成り立つ」の条件を満たし、process の間の観測は一致） | 上書きの形そのものの反証ではない。S4-2 の A1 を始められ、S4-2 が、上書きが使う型を Player に残す方法（上書きの build の型の記録と Player の型の照合、開発用の Player の stripping）を決める |
| 成り立たない（それ以外。round の段の Error・Exception だけが外れた時を含む） | owner へ返す。実機へ編集を届ける方式（目的 build の作り直し、CI での作り直し等）を見直すかは owner が裁定する。S4-2 は裁定まで始めない |
| 単独の基準が成り立たない（未判定。base だけで、操作が失敗した・壊れた・Error が出た・process の間で違った） | 上書きに固有でない、Player の 1 つの directory の問題である。harness の誤りでないことを B が確かめた上で Phase A へ戻し、結果を owner へ返して S4-1 の入力にする（本スライスの問いに答えを出せるかは、その時に owner が裁定する） |
| P4 で `ProbeOverlayOnly` が Player にある | 「base の build report を渡して build した Player に、base が使う型と同じ assembly にある、base が使わない `MonoBehaviour` が残った」という、この構成での事実。別の assembly・組み込みの型へ広げない |

- 停止規則: 進める最低条件を満たし、問いに致命的な反証がなければ GO で終了する。最低条件未達（未判定）のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし

### 2.1 進める最低条件

**不変条件（起きてはいけないこと）**

- **I-1 危険な条件の無い fixture と、違う build の出力で答えない:** 答えは、P1〜P3 が登録の前の record で成り立ち、round が読んだ 2 つの directory と起動した Player が同じ実行の build の出力だと照合できた実行からだけ出す。
  - 崩しうる経路: (a) base と上書きの component の型の集合が結果的に同じになり、script をまとめた file が同じ bytes になる（probe の 3 型で差を作り、P1 で確かめる）。(b) v2 への書き換えが import されず、上書きに v1 の file が入る（P2）。(c) 共通物が他の asset と同じ file に入り、bytes が変わる（P3）。(d) layout を名前で引いて別の asset の file を見る（固定 GUID と local id）。(e) base と上書きの root を同じ asset の書き換えで作り、base の handle が上書きの root を返す（root は別の固定 GUID の asset にし、handle ごとの `BuildIdentity` を照合する）。(f) Player の build と上書きの build の順が逆になり、Player が上書きの型の記録を知った状態になる（順は base → Player → v2 → 上書きに固定し、Player の build に渡す directory は base の build report だけにする。順と渡した directory を record に残す）。(g) 前の実行の Player・directory を起動・登録する。Player の exe は同じで `GameAssembly.dll` や data だけが別の build の物に替わる（実行 id ごとの一時 root。Player の出力の棚卸しの digest と `Application.buildGUID` の照合。exe の sha256 は棚卸しの 1 行で、それだけを根拠にしない）。(h) 上書きの build が、Player の build の後の `Library` の状態で base の file を作り直す（上書きの棚卸しで P2・P3 を確かめる。base の directory は Player の build の前に棚卸しし、起動の前に digest を照合する）。(i) `Library` に前の実行の上書きの build の履歴があり（固定 GUID なので、2 回目以降は `ProbeOverlayOnly` の型の記録が残る）、Player の build がそれを読む（渡す directory を base の 1 つに限る。第 1 process は、build の前に `Library` の build の履歴に spike の名前の build があるかを記録する。読むかどうかは未確認で、P4 の答えに添える）。
  - 探索した境界: S2-0・S2-0b・S2-1 の harness と実測、製品の BS4 の Player の build の code。未確認: Player の build（IL2CPP）が、同じ `Library` の後続の content の build の出力を変えるか（P2・P3 と digest の照合で受ける）。Player の exe が build をまたいで同じ bytes になるか（digest の対象を出力全体にして受ける）。
- **I-2 Player の観測でない物、見分けられない観測で答えない:** 答えは、Player の process 自身が書いた record の生の値（版を区別できる物、class の完全名と値、整合した実体の数、保持したままの参照）と、Unity が書いた Player の log の file からだけ出す。launcher が書くのは process の起動と終わり方の行だけで、「答えにできる失敗」の条件にだけ使う。期待値は fixture の定義と Editor の実測の文から採る。
  - 崩しうる経路: (a) launcher（Editor の process）が観測を作る・補う（launcher は観測の行を書かず、record と log の file をそのまま集める）。(b) fixture の asset が Player 本体に入り、Content Directory の物でない実体が観測に混ざる（build report の packed assets の確認と P0）。(c) 製品の起動が成功して content や Scene を load する、service を常駐させる、log を出して round の log と混ざる（`SAMPLEGAME_` の環境変数を除いた起動、P0 の log の行、round を製品の `AfterSceneLoad` の後に始めること）。(d) runner が購読する前の log が record に無い（起動の段の一次証拠は Player の log の file にする）。(e) Missing の component が `GetComponent<T>` では見えない、列から落ちる（`GetComponents<Component>()` の `== null` と列の長さ）。(f) 参照を離した後の `== null` を壊れたと読む（確認点まで参照を保持する）。(g) `?.`・`is null` で偽 null を見落とす。(h) `-batchmode` の Player で texture の pixel か material が読めず、空の値どうしが一致して見える（単独の基準が fixture の定義の pixel の sha256 と一致することを先に確かめる。一致しなければ未判定）。(i) Player が落ちて record が途中で切れ、切れる前の行だけで肯定の答えを出す（終了行の無い round からは、「答えにできる失敗」の条件で否定の答えだけを出す）。(j) 止め損ねた前の process が、後の round の record に行を書く（record は process ごとに別の file）。(k) 期待値を Player 側と Editor 側の 2 か所に持ち、片方だけが変わる（期待値は Editor 側の 1 つの表だけに置き、runner は第 1 process が書いた fixture の事実の file から key と名前を読む）。
  - 探索した境界: S2-1 の数え方（`FindObjectsOfTypeAll` は Content Directory から載った物を見た。Editor）、製品の起動の code（`AbstractApplicationInitializer` の `BootstrapBeforeSceneLoad`・`BootstrapAfterSceneLoad`・`BuildConfig`・`ReadContentDirectoryConfiguration`、SampleGame の `AppInitializer`、設定の 3 つの読み口）、他の `[RuntimeInitializeOnLoadMethod]`（`Runtime/Telemetry` の 1 つだけで、観測の登録だけを行う）。未確認: package の中の `[RuntimeInitializeOnLoadMethod]`。IL2CPP の Player で、`FindObjectsOfTypeAll`・`EntityId`・`JsonUtility.ToJson`・`GetPixels32` が同じ観測を返すこと（単独の基準 AC2 と AC7 が確かめる）。製品の `AfterSceneLoad` の 1 行が、最初の Scene の `Start` より前に出ること（P0 の (1) は log の file で読むので、順が違っても確かめられる）。`DoNotBuildWithPlayer` の設定でも、`Library` に既にある Addressables の build が Player に入るか（AC1 が記録する）。
- **I-3 製品を変えない、残さない:** §3 の新規 file と本書・program 以外の変更が無く、実験は asset・設定・process を repo と PC に残さない。build の出力と record は一時 root だけに置き、Phase D の後始末で消す。
  - 崩しうる経路: (a) fixture の folder と `.meta` の残り。(b) Player の build のために変える設定（scripting backend、managed stripping の水準、Addressables の「Player と一緒に build する」設定）が戻らない。3 つとも追跡している file の値で、scope の間に保存が走ると disk に出る。戻す前に Unity が強制終了されると、変えた値が file に残る。scope の途中で `yield`・`await` を挟むと、domain reload や test の中断で `finally` が失われる（scope の開始から復元までを 1 つの同期の呼び出しの中に置き、復元の後に保存して、2 つの file を変える前の bytes に書き戻し、bytes が開始前と同じことを確かめる。Unity の process が終わった後の `git status` でも確かめる）。(c) Player の build が `Assets/` の下（`StreamingAssets`、`Resources`）、`ProjectSettings/` の下の新しい file、Addressables の出力に file を作る（process の前後の `git status`）。(d) 起動した Player の process が残る。launcher の Unity が round の途中で落ちる・強制終了されると、後始末が走らない（runner は自分の上限を持ち、main thread に依らない timer から process を終える。launcher は起動の直後に pid を record へ flush し、自分が起動した process だけを止める。他の process を名前で止めない。実行の後に、record の pid が全て終わっていることを確かめる）。(e) probe と runner の型が Phase D まで SampleGame の Player にも入る（runner は `[RuntimeInitializeOnLoadMethod]` を持たず、spike の Scene に置かれ、環境変数がある時だけ動く）。(f) 残りの回収が、自分の marker の無い folder を消す。(g) Player が user の folder（`LocalLow` の下の製品名の folder、crash dump）に file を作る（製品の Player と同じ場所で、範囲の外とする。Player の log は `-logFile` で一時 root へ出す。同じ製品名の Player が動いていないことを初期状態にする）。
  - 範囲: 守るのは git で追跡する file、fixture の folder、`Assets/OneStarMakerGenerated`、一時 root の外、Player の process。`Library` の cache と、Player が user の folder に作る file は対象にしない。

**成立条件（起きなければならないこと）**

- **E1 準備:** base・Player・上書きが build でき、P1〜P3 と、Player の build の設定（target、backend、stripping の水準、渡した build report の directory、Scene の一覧）と結果（build report の GUID、出力の棚卸し、packed assets の確認）が record にある。
- **E2 基準:** base だけ（読みと class と数、寿命）、上書きだけを登録した Player の round が、3 つの process ずつ record にあり、単独の基準が成り立つ（§2 の語）。
- **E3 答え:** 主の順の 3 つの round（読まれる側、class と実体の数、寿命）が、3 つの process ずつ record にあり（終了行まで、または「答えにできる失敗」の条件を満たす所まで）、スパイクの答えが一意に読める。

### 2.2 受け入れ条件

全て `[Explicit]` の EditMode test（§5）が作る record（JSONL）、Player の log の file、XML を根拠に判定する。Player の round の record は、その process が自分で書いた先頭（実行 id、head、`Application.isEditor`、platform、backend、Unity の版、`Application.buildGUID`、graphics の device の種類、process の id、渡された round の設定）と、round の開始行・終了行を持つ。runner は、開始の後に P0 の (2) と P4 を記録し、渡された順に `ContentLoadManager.RegisterContentDirectory` を呼び、呼び出しの開始と結果（`Register` を呼んだ順、handle が有効か、登録の一覧の件数、handle ごとの root の `BuildIdentity`）を記録してから、load を始める。開始の印より後の Unity の log（Error・Exception・Warning・Log）は、発生順のまま全件を段（登録、観測の各段、解除）に結びつけて record に残す。async の load・unload・`UnloadUnusedAssets` は完了を待つ。操作ごとに開始と結果の行を書き、失敗したら残りの観測を行わずに後始末へ進む（§2 の「操作と、その失敗」）。後始末は `finally` で行い、登録が残れば登録の一覧の handle から解除して記録する。test の待ち合わせは、process の終了の signal と frame の進行で行い、`Task.Delay`・`Thread.Sleep` を使わない。**反復:** 各 round を 3 つの Player の process で行う（起動は順に 1 つずつ）。

- **AC1（E1、I-1、I-2）準備と前提:** 第 1 process が次を順に行う。Active Build Target が StandaloneWindows64 であることを記録する（違えば何も作らずに止まる。未判定）。fixture（§3）を v1 で作って base を build し、base の出力を棚卸しする。runner だけを持つ Scene を作り、設定を変えて Player を build し、設定を戻す（設定は §3。変更から復元までは 1 つの同期の呼び出し）。source を v2 に変えて上書き（root は `SceneEdit` と `EditedTex` だけ）を build する。両方の directory と Player の出力（backup の folder を除く）の全 file の名前・size・sha256、`ContentLayout.json` の写し、Player の build の設定と結果、build report の GUID、scripting assembly の一覧、Player の出力に Addressables の data（`StreamingAssets/aa`）があるか、build の前の `Library` の build の履歴に spike の名前の build があるか、fixture の事実（runner が読む key と名前）を記録する。P1〜P3 の成否と、Player の build report の packed assets に runner の Scene 以外の fixture の asset が無いことを record に書く。fixture の folder を消して終わる（消したことを file system で確かめる）。順は base → Player → v2 → 上書きである。
  - 失敗経路の例: P1〜P3 のどれかが成り立たない、Player の build が失敗する、packed assets に `EditedTex` がある。どれも未判定で、第 2 process は round を起動しない。
- **AC2（E2、I-2）単独の基準:** 第 2 process の launcher が、第 1 process の同じ実行の出力であることを実行 id・head・3 つの棚卸しの digest で照合した後、次の 3 つの round を 3 つの process ずつ行う。(1) **base だけ:** `SceneKeep`・`SceneEdit`（v1）を load し、`SharedPrefab` と `EditedTex`（v1）を保持して、版・class・値・pixel と、7 つの物の実体の数（全て 1）と列挙の照合を記録する。(2) **base だけの寿命:** AC5 の (1)〜(6) と同じ列を、base の directory だけを登録して行う（`SceneEdit` は base の v1、`EditedTex` は base の root から。(6) の解除は base の 1 つ）。(3) **上書きだけ:** `SceneEdit`（v2）の版・class・値と `EditedTex`（v2）の pixel を記録する。3 つとも「単独の基準が成り立つ」（§2 の語）こと。成り立たなければ未判定で、何が外れたかを結論に書く。
- **AC3（E3、Q1、X1）読まれる側:** 主の順の round で、次を順に行う。base の root の `SceneEdit` の id と上書きの root の `SceneEdit` の id が等しいかを記録する。base の root の id から load して版を記録して unload し、上書きの root の id から load して版を記録して unload し、`UnloadUnusedAssets` する。`EditedTex` を base の root、上書きの root の順に `Loadable` から load して両方を保持し、pixel の版、`EntityId`、`LoadableObjectId` が等しいか、列挙との照合を記録する。
  - 合格例（強）: どちらの id からも `Marker_v2`。不合格ではない例（弱）: 上書きの id から `Marker_v2`、base の id から `Marker_v1`（答えは「違うが成り立つ」の側）。X1 の違いの例: 版はどちらも v2 だが、2 つの load の `EntityId` が違う（実体 2）。答えは「違うが成り立つ」の側。
- **AC4（E3、Q2・Q3、X2）class と実体の数:** 主の順の round で、`SceneKeep`（base の root の id）、`SceneEdit`（上書きの root の id）の順に additive に load して両方を載せたままにし、base の root から `SharedPrefab` を、上書きの root から `EditedTex` を load して保持した状態で、全 probe の class と値、Missing の数、`SharedMat`・`SharedTex`・`SharedShader`・`EditedMat`・`EditedTex`・`SharedPrefab`・`KeepTex` の実体の数、`SceneKeep` の Renderer が直接参照する `EditedMat` の `mainTexture` の pixel の版を記録する。
  - 失敗経路の例: `SceneKeep` か `SharedPrefab` の `ProbeBaseOnly` の位置の component が `== null`（Missing）、列から欠けている、または `FullName` が `ProbeOverlayOnly`・`ProbeBoth` になる。どれも Q2 は誤り。
- **AC5（E3、Q4）寿命:** 主の順の round で、次の列を順に行い、各段の後に、その時点で保持している物（Scene の probe と Renderer の material・texture、`Loadable` の物）の生の値を記録する。(1) 上書きの `SceneEdit` を先に、`SceneKeep` を後に load し（AC4 と逆の順）、base の root から `SharedPrefab`、上書きの root から `EditedTex` を load して保持し、AC4 と同じ 7 つの物の実体の数を記録する。(2) `SceneEdit` を unload し `UnloadUnusedAssets`。(3) `SceneEdit` を load し直し、版を記録する。(4) `SceneKeep` を unload し `UnloadUnusedAssets`。(5) `SceneKeep` を load し直す。(6) `Loadable` を Release し、両 Scene を unload し、上書き → base の順に解除して、登録の一覧の件数を記録する（0 件が Q4 の条件）。(6) の間の log は (1)〜(5) と分けて記録する（どちらも X3 が数える）。
  - 境界: 「解除まで」は (6) の解除の完了と終了行までを指す。終了行の後の process の終わり方は答えに使わない（記録して添える）。
  - 合格例: 終了行があり、その後 process が終わらず launcher が止めた。観測は有効で、終わり方は添えるだけ。答えにできる失敗の例 (A): 2 つの登録は成功し、(3) の `SceneEdit` の load が例外で失敗し、runner が失敗の行を書いて後始末をし、終了行まで書いた。単独の基準は成り立っている。答えは「成り立たない（load できない）」。答えにできる失敗の例 (B): record の最後の行が (3) の load の開始で、終了行が無く、単独の基準は成り立っている。答えは「成り立たない（落ちた）」。X3 の例: 観測の値は全て期待どおりだが、(6) の解除の段に Error が 1 件ある。答えは「成り立たない（解除の段の Error）」。未判定の例: record に 1 つ目の登録の成功の行が無い。
- **AC6（I-2。Editor の対照。条件付き）:** スパイクの答えが「同じ」でなかった時（「答えにできる失敗」と「型の欠落」を含む）だけ、同じ runner を、同じ 2 つの directory に対して Editor の Play（project の今の設定である Domain Reload 無効のまま。設定は record に残し、違えば未判定。その Unity の process の最初の Play、graphics あり、`SAMPLEGAME_` の環境変数を除いた状態）で、主の順の 3 つの round について 1 回ずつ走らせ、record に残す。主の順で process の間の観測が違った時は、3 回ずつ走らせる（どの回も別々の Unity の process）。Editor の対照が、走らせた全ての回で Editor の実測（現況の (a)〜(e)）を再現すれば、Player との差（process の間の違いを含む）は host の差として結論に書く。1 回でも再現しなければ未判定で、B へ戻す（runner の誤りを探す）。B が runner の誤りを見つけられない時は Phase A へ戻す（Editor の対照の条件は、比べる相手の実測と、製品の起動の状態と graphics の有無が違う）。答えが「同じ」の時は実行しない（Player の観測が、fixture の定義と Editor の実測の両方に一致しているので、runner の誤りがそれを偶然に作る道は考えにくい）。
  - 合格例: Player の Q3 が「収まるが違う」（共通の material が 1）で、Editor の対照は 2。差は host にある。不合格例: Editor の対照でも 1。Editor の実測（2）を再現しないので未判定。process の間の違いの例: Player で、base の root の id から要求した `SceneEdit` が 3 つの process で v2・v1・v2、上書きの root の id からは 3 つとも v2、他は全て期待どおり。答えは「違うが成り立つ」に「process の間で観測が違う」の印が付く。Editor の対照を 3 回ずつ走らせ、3 回とも v2 なら、揺れは Player に固有と結論に書く。後続は owner の裁定で、S4-2 は自動では始めない。
- **AC7（I-2）数え方の成立:** 単独の基準（AC2 の (1) と、(2) の (1) の計数の時点）で、参照をたどった `EntityId` の集合が列挙の集合と一致する。一致しなければ未判定（数え方が Player で成り立っていない）。主の順（AC3、AC4、AC5 の (1)）で 2 つが違う時は、未判定にせず、多い方の数で Q3 と X1 を読み、違いを挙げる。
- **AC8（I-2）Player の観測であること:** AC2〜AC5 の全ての round の record の先頭が、`Application.isEditor` が偽・backend が IL2CPP で、Unity の版・実行 id・head・`Application.buildGUID` が第 1 process の record と同じで（Unity の版は、project の `ProjectVersion.txt` の版として record に残す）、P0 が成り立ち、P4 の 3 つの値がある。launcher の file に、観測の行が無い。
  - 失敗経路の例: Player の log の起動の段に製品の `AfterSceneLoad` の 1 行が無い（製品の起動が成功して動いている）。登録の前に登録中の directory が 1 件以上ある。どちらも未判定。
- **AC9（I-3）製品の無変更と後始末:** base から head までの Phase B の commit が変える file が、本書と program を除いて、§3 の新規 file と `.meta` だけである。`pwsh tools/contract-audit.ps1` が合格する。次の 2 つの時点で確かめる。**(1) Unity の process の中:** 第 1 process の record に、変える前と復元の後の scripting backend・managed stripping の水準・Addressables の設定の値が同じで、`ProjectSettings/ProjectSettings.asset` と Addressables の設定の asset の bytes の hash が同じであること（scope は、値を戻して保存した後、2 つの file を変える前の bytes に書き戻す。Unity の再 serialize による無関係な差分を残さない）、fixture の folder が無いこと。**(2) 各 Unity の process が終わった後（強制終了した時を含む）:** Phase C の実行担当が取る `git status --porcelain` が実行の前と同じで、record にある Player の pid が全て終わっていること。marker の無い残りがあれば何も消さずに止まる。
  - 合格例・不合格例: (1) が合格でも、Unity の終了の後に `ProjectSettings.asset` に差分があれば不合格。その時は、実行担当がその file だけを `git restore` で戻して記録し、その実行は判定に使わない。
- **AC10（I-1・I-2）判定の写像:** Unity を使わない test が、(a)〜(q) のうち入力で表せる誤り（P の不成立、Editor の先頭を持つ record、`buildGUID` の不一致、順の違う登録、型の集合は同じで位置だけ入れ替わった入力、期待の列より短い component の列、process の間の観測の不一致、P0 の log の行の欠落、X1・X2・X3 のそれぞれが違う入力、終了行のある round の異常な終了 code、開始行の無い round、1 つ目の登録より前で切れた record、2 つ目の登録の後で切れた record、行が増え続けたまま上限で止めた record、単独の基準が成り立たない時の主の順の失敗、型の欠落の 2 つの場合と、その時の Q4 と、基準に共通の Error の扱い、主の順で load の操作が全て成功しているのに 0 の物がある入力、版がどちらでもない入力）を「同じ」にせず、定義どおりの答えか未判定にすることを確かめる。あわせて、否定の結果が未判定に落ちないことを確かめる: 登録の段にだけ Error がある入力、AC5 の (6) の段にだけ Error がある入力、観測の値は全て期待どおりで X3 だけが外れた入力（3 つとも「成り立たない」で、「違うが成り立つ」にならない）、主の順の load が失敗して終了行のある入力と、同じ失敗で終了行の無い入力（どちらも「成り立たない（load できない）」）、2 つ目の登録が無効な handle を返した入力、主の順の 1 つ目の登録が失敗した入力（未判定）、主の順で参照をたどった数と列挙の数が違う入力（未判定にならず、多い方で読む）、主の順で process の間の観測が違う入力（未判定にならず、最も悪い答え）、主の順で process の間の観測が違い、どの process も「成り立つ」側の範囲にある入力（答えは「違うが成り立つ」で、「process の間で観測が違う」の印と、違った観測ごとの 3 つの値が出る）、単独の基準で同じことが起きた入力（未判定）。

### 2.3 ここでは答えない問いと所有する後続

行の名前は §2.4 の分割による（program §8）。

| 問い | 所有 |
|---|---|
| content directory から起動して操作を続けられる Player の構成。Player の実機の非退行（S3-1 の配線、S3-2 の session・gate の層化）の 6000.6.9f1 での確認 | S4-1 |
| 上書きを製品の Player に載せること（13 章の禁止を解く条件、起動の設定、検証済みの base、製品の登録の口での確認、照合の失敗を base だけに縮退しないこと） | S4-2 |
| 上書きが Player に無い型を使う時の検出と扱い（Player を build した後に足した script、base が使わない組み込みの型と module）。script の code・field を変えた時 | S4-2 |
| S4-1 が決める開発用の Player の構成（backend、stripping、development build）が本スライスと違う時の、native の答えの測り直し | S4-2 の A1 の最初の gate（本スライスの harness を git 履歴から取り出し、その構成で通す） |
| 別の `Library` で build した base を載せた Player に、手元の上書きを重ねた時の答え | S4-2 の A1 の最初の gate（別の checkout で足りる。実際の base は必ず別の機械で作られる） |
| 別の PC で build した base と手元の上書きの組み合わせ（Editor）。上書きに依存として入った同じ id の物の読み先 | S4-3（A1 の最初の gate） |
| 窓を出した通常の起動での答え、実データ（SampleGame）での答え、GPU 側の資源の重複 | S4-2（SampleGame の Player に載せる時） |
| 片方の directory の file が disk から消えた時の読み先 | S4-3 |
| build の後に上書きの directory が書き換わった時の検出 | S4-2 |
| Scene を load した後に上書きを登録する順、実行の途中の上書きの作り直しと解除 | S4-4 |
| Android・iOS の Player | S4-5 |
| 同じ file を持つ 2 つの directory の登録（S2-0b）の Player での答え、URP の `Lit` の variant、Scene に置いた prefab instance・nested prefab・sprite atlas・audio を上書きに入れた時の Player での答え | S4-2 の A1 が要否を決める（Editor でも未測定の物を含む。program §8「S2-0b から移送した入力」「S2-1 から移送した入力」） |
| 登録の順が上書き → base の時の Player での答え | 答えが「成り立たない」で、owner が順の入れ替えを方式の候補にした時の後続 |

### 2.4 スライス分割の判断（S4 の分割）

**S4 を 6 つの行に分ける。本スライスは、その最初の S4-0 である。** 契機は 2 つある。(1) S4 の行は、後から送られた入力（program §8 の各「移送した入力」と「S4 の A1 への入力（2026-11-23）」）で、独立した変更理由を持つ問いの集まりになっている（実機の Player、供給元、登録の時点、他の target）。(2) owner の入力が、供給元の設計より前に Player での上書きの実測を置くよう求め、遊べる Player の構成をどの行が持つかを S4 の A1 に決めさせている。

| 行 | 問い（1 行） | risk | 順序と理由 |
|---|---|---|---|
| S4-0（本スライス） | base → 上書きの登録が、Windows の Player で Editor の実測と同じ答えになるか | high | 最初。答えが後続の形を決める。製品 code を変えない |
| S4-1 | content directory から起動して操作を続けられる Player の構成を、動作確認の Player の契約と protected config・fail-closed を弱めずに持てるか | high | S4-0 の Phase D の後（S4-0 の答えがどちらでも要る。上書きが成り立たなくても、作り直した content を実機で遊んで確かめる Player は要る。ただし Player に上書きの差込口を残すかは S4-0 の答えで変わるので、後に置く）。Player の起動を変えるので、Player の実機の非退行（元の S4 の gate）をここで確かめる |
| S4-2 | 編集する開発者が、検証済みの base を載せた遊べる Player に、自分の編集の上書きを重ねて起動できるか | high | S4-0 の答えが「同じ」か「違うが成り立つ」か「型の欠落だけが原因の、成り立たない」で、主の順の process の間の観測が一致しており、S4-1 が終わった後。それ以外の「成り立たない」の時と、process の間の観測が違った時は、owner の裁定の後に行を置き直す。A1 の最初の gate に、別の `Library` の base を載せた Player での答えと、S4-1 の構成が S4-0 と違う時の測り直しを置く |
| S4-3（元の名前 `LOCAL_LAN_PROVIDER` を引き継ぐ） | Local の installed cache / local の publish 済み / LAN の供給元を段階化し、Play の入口の prefetch と base の選び方を持てるか | normal | 実機の経路が決まった後。A1 の最初の gate に、別の PC で build した base の実測（Editor）を置く。元の S4 の gate（Local hit / miss、LAN timeout、tamper、cancel、immutable revision、cache lease、publish 成功だけで Play 可能と宣言しない）を持つ。Local と LAN、prefetch と base の選び方をさらに分けるかは、この行の A1 が決める |
| S4-4 | 要求の時点の install と登録（起動の順序を含む）を、登録の契約を弱めずに持てるか | high | S4-3 の後。13 章は load の後の登録を未測定のため禁止しているので、A1 の最初の gate にその実測を置く。登録の寿命に関わる未測定（Play の間の script の compile、reload と native の登録の残り）もここで持つ。後者は S4-3 に依らず、今ある Editor の上書きの Play に関わるので、owner が先に切り出してよい |
| S4-5 | 上書きを Android・iOS の Player に載せられるか | high | S4-2 の後で、着手の時期は owner が決める。target の固定（manifest・cache・起動）を解くことと、実機への転送を含む |

- **本スライスをさらに分けるか:** 分けない。Q1〜Q4 は同じ 2 つの directory と同じ Player で測れ、分けると build と harness を作り直す。独立して失敗しうる未知（test からの Player の build、製品の起動が邪魔をしないこと、IL2CPP の Player での観測の API）は、どれも単独の基準（AC2）より手前で分かり、§5 の「未知の操作経路」に不成立の時の扱いを分けて書いた。
- **Player の実機の非退行を本スライスに入れない理由:** 製品の動作確認の Player（BS4）を build して receipt を読む作業は、製品の経路の回帰の問いで、Unity の挙動を測る本スライスの問いと別である。本スライスの単独の基準（base だけを登録した Player の round）は、製品の session・gate を通らないので、その代わりにならない。
- **要求の時点の install と登録を行として残す理由:** program の S4 の行の定義にあり、落とす・延期するのは範囲の変更で owner の判断である。本書は行を残して順を後ろに置くだけにした。
- **入力の振り分け:** program §8 の各「移送した入力」が S4 へ送った項目の行き先は、program §8「S4-0 の A1 が振り分けた入力」に書いた。
- **program の変更箇所（2026-11-23 の凍結で owner が承認した範囲）:** §8 の S4 の行を見出しの行と S4-0〜S4-5 の 6 行に置き換えたこと、§8 に「S4-0 の A1 が振り分けた入力」の項を足したこと、§4.2 の表の所有の欄（4 行）とその前の 1 文、§10 の U7 の所有の語。行の定義から落とした項目は無い。

### 2.5 未決事項

なし。§1 の目的・利用者の文言と、§2.4 の S4 の分割は、2026-11-23 の凍結で owner が承認した（§0 の A3 凍結記録）。

**担当が決めた点（覆す時だけ改める）:**

- **Player の構成は IL2CPP・managed stripping High の 1 つ。** 今ある製品の Player（BS4）の構成で、content と Player の型の食い違いが最も出やすい側である。他の構成は、開発用の Player の構成を決める S4-1 と、その構成で測り直す S4-2 が持つ。
- **起動は `-batchmode` で、`-nographics` を付けない。** 実際に使う Player は graphics の device を持つので、そちらに合わせる（窓は出ない）。比べる相手の Editor の実測は `-nographics` で、観測は CPU 側の値だけなので、device の有無で答えが変わる道は考えにくいが、変数が一緒に変わることは範囲に明記する（§2 の成果の範囲）。
- **登録と load は Unity の公開 API を runner が直接呼ぶ。** 製品の wrapper（internal）は Player の assembly から見えず、見えるようにするには製品の変更が要る。wrapper が足すのは型の検査と例外の変換だけで、native の呼び出しは同じである（現況）。
- **Editor の対照は、答えが「同じ」でない時だけ。** 相手は 2026-11-18 に 6000.6.9f1 で測り直し済みで、製品 code はその後 native 層を変えていない。Player の観測が fixture の定義と Editor の実測の両方に一致した時は、同じ runner を Editor で走らせても答えは変わらない。差が出た時は、差が host にあるのか runner にあるのかを分ける必要があるので、同じ runner を Editor の Play で走らせる（AC6）。runner の設定を環境変数で渡すので、Editor でも同じ runner がそのまま動く。主の順で process の間の観測が違った時だけは、Editor の対照も 3 回ずつ走らせ、揺れが Player に固有かを見る。
- **登録の順が上書き → base の対照は測らない。** owner の入力と目的が、比べる相手を主の順に限っている。
- **base だけの寿命の round を、単独の基準に無条件で入れる。** 主の順の寿命の失敗を上書きのせいと読む前に、同じ操作の列が 1 つの directory で成り立つことが要る。Player の起動 3 回で済む。
- **Player の build を高コスト証拠に分類しない。** 外部への作用が無く、この PC で何度でも作り直せる。分類すると、判定の入力が判定 C の前に 1 つも無い形になる（§5）。
- **第 1 process を `-executeMethod` から起こす代替は持たない。** `run-tests.ps1` は test の実行しか組み立てず、代替にすると XML のゲートが無くなる。test から Player を build できない時は Phase A へ戻す（§5 の u1）。
- **risk は high。** program の S4 の行は normal だが、本スライスは S2-1 と同じく結論が後続の形を決め、初めての経路を含む。

## 3. 責務マップ

新規 file は、`unity/Assets/OneStarMaker/Tests/Editor/Spikes/PlayerOverlay/`（既存の `OneStarMaker.Tests.Editor` asmdef。参照は足さない）と、Player の compile に入る新しい asmdef 2 つ（probe だけの `unity/Assets/OneStarMaker/Tests/Spikes/PlayerOverlayProbes/` と、runner の `unity/Assets/OneStarMaker/Tests/Spikes/PlayerOverlayRunner/`）である。製品の file は変えない。全て Phase D で削除する。新規の `.cs` は全て先頭に `#nullable enable` を置き、`record` 型を使わない（常時契約）。S2-1 の harness（commit `a304963b`）から、fixture の固定 GUID・marker、棚卸し、前提 P1〜P3、record、round の観測、判定の写像を写して直す。

| file | 責務 | 予想行数 | テスト境界と配置理由 |
|---|---|---:|---|
| `Tests/Editor/Spikes/PlayerOverlay/PlayerOverlayExpected.cs` | fixture の定義の表（asset の名前、logical key、probe の期待の列と値、版ごとの名前と pixel）と、Editor の実測の数。fixture の生成、fixture の事実の file、判定の写像が、この 1 つの表を読む | 160 | 宣言的な表。期待値の出所を 1 か所にする（I-2 の (k)） |
| `…/PlayerOverlayVerdict.cs` | 事実（棚卸し、layout の帰属、build の結果）から P1〜P3 と準備の成否を決め、record の行と Player の log から、P0、版、正しい class、実体の数の整合、壊れた、Q1〜Q4、X1〜X3、process の間の一致、「答えにできる失敗」、「型の欠落」、スパイクの答え、未判定の理由、Editor の対照の再現を決める。入力は文字列・数値・真偽だけ | 490 | Unity なしで単体テストできる pure な写像。S2-1 の `OverlayVerdict` を写し、対照の順の語（既知の誤りのある読み）を除き、Player の語を足す。500 行に近いが、1 つの写像の表なので非分割とする |
| `…/PlayerOverlayVerdictTests.cs` | 上の写像だけの EditMode test（AC10） | 450 | 通常の EditMode 回帰に入る |
| `…/PlayerOverlayInventory.cs` | directory の棚卸し（名前・size・sha256）と digest、`ContentLayout.json` からの帰属の読み取り、fixture の事実の file の読み書き。Unity の build に依らない file の I/O | 220 | 第 1 process（作る）と launcher・Editor の対照（照合する）が使う。build の列と変更理由が別なので分ける |
| `…/PlayerOverlayFixture.cs` | fixture の生成（固定 GUID、v1 と v2）、runner だけを持つ Scene の生成、所有の marker、残りの回収と削除 | 330 | Unity / `AssetDatabase` の I/O |
| `…/PlayerOverlayProjectScope.cs` | Player の build の間だけ変える 3 つの設定（scripting backend、managed stripping の水準、Addressables の「Player と一緒に build する」設定）の記録・変更・復元と、復元の後の保存、2 つの設定の file を変える前の bytes へ書き戻すこと、bytes の hash の照合。`IDisposable`。Addressables の設定は、reflection（既存の test と同じ）か設定の asset の `SerializedObject` で読み書きする（test の asmdef は Addressables の Editor の assembly を参照していない） | 140 | Editor の設定の I/O。何を戻すかの一覧が変更理由。開始から復元までを、呼び手が `yield`・`await` を挟まない 1 つの同期の呼び出しの中で使う |
| `…/PlayerOverlayBuilds.cs` | `[Explicit]` の `FirstProcess`（coroutine にしない `[Test]`）。base の build、Player の build、上書きの build、build report の読み取り（GUID、packed assets、scripting assembly の一覧）、事実の record、fixture の削除 | 420 | Unity の build の I/O。P1〜P3 と準備の成否は `PlayerOverlayVerdict` が事実から決める |
| `…/PlayerOverlayLaunch.cs` | `[Explicit]` の `SecondProcess`。第 1 process の出力の照合、round ごとの Player の process の起動（`SAMPLEGAME_` を除いた環境と渡す環境変数、log の file）、pid の記録、終わりの待ち合わせと上限、自分が起動した process の停止、record と log の収集、判定の写像の呼び出し | 360 | process の orchestration。観測は書かない。判断は `PlayerOverlayVerdict` に置く |
| `…/PlayerOverlayEditorControl.cs` | 条件付きの `[Explicit]` の `[UnityTest]`（AC6。round ごとに 1 つ）。第 1 process の出力の照合、runner の Scene の一時生成、環境変数の設定と `SAMPLEGAME_` の退避、Editor の Play への出入り、runner の終了行の待ち合わせ、後始末 | 200 | Play を伴う orchestration。観測は runner が書く。launcher と分けるのは、Play の往復と process の起動で、失敗の仕方と後始末が違うため |
| `Tests/Spikes/PlayerOverlayProbes/ProbeBaseOnly.cs`・`ProbeOverlayOnly.cs`・`ProbeBoth.cs` | `_tag`（string）と `_value`（int）だけを持つ空の `MonoBehaviour`（先頭に `#nullable enable`） | 各 15 | Content Directory の component が指す script は、Player の compile に入る assembly に要る（S2-1 の事実） |
| `Tests/Spikes/PlayerOverlayProbes/OneStarMaker.Spikes.PlayerOverlayProbes.asmdef` | probe の 3 型だけを持つ assembly。`references` は空、`defineConstraints` は空、`includePlatforms` と `excludePlatforms` は空、`autoReferenced` は false。どの assembly もこれを参照しない（S2-1 の probe の asmdef と同じ形） | — | runner と別の assembly にする。Player の Scene は runner を使うので、同じ assembly に置くと「Scene が使う型のある assembly」として probe の型が残り、content からしか使われない script の型が Player に残るか（P4）を測れない |
| `Tests/Spikes/PlayerOverlayRunner/PlayerOverlayRunner.cs` | spike の Scene に置く `MonoBehaviour`。環境変数（round の名前、登録の順、2 つの directory の path、fixture の事実の file の path、record の path、実行 id、head、上限の時間）を読み、`Start` 以降に開始の印を出して round を始め、先頭・P0 の (2)・P4 を書き、登録し、名前の round を行い、解除し、終了行を書いて終わる（Player では `Application.Quit`）。操作ごとに開始と結果の行を書き、失敗したら残りの観測を行わずに後始末へ進む。自分の上限を持ち、main thread に依らない timer から、上限の超過の行を書いて process を終える（Player だけ）。環境変数が無ければ何もしない。`[RuntimeInitializeOnLoadMethod]` を持たない。probe の型は文字列と `Component` だけで扱い、参照しない | 400 | Player と Editor の Play の中の orchestration。観測の読み方は下の file に置く |
| `…/PlayerOverlayObservations.cs` | 生の値の読み取り（Scene の root の名前、component の列と json、pixel の sha256、参照をたどった `EntityId` と列挙、material・shader の参照）。期待値を持たず、判定をしない | 240 | runtime の API だけ。S2-1 の round の本体から、読み取りの部分を写す |
| `…/PlayerOverlayRecord.cs` | JSONL の書き出し（行ごとに flush、行に時刻）と、`Application.logMessageReceivedThreaded` の捕捉と段の付与 | 130 | file の I/O。落ちた時に書いた行までが残るよう、行ごとに閉じる |
| `…/OneStarMaker.Spikes.PlayerOverlayRunner.asmdef` | runner・観測・record の 3 file を持つ assembly。`references` は `OneStarMaker.Runtime`（root の型 `BuildContentRoot` のため）と `UniTask`（製品の native 層と同じ形で load を await するため）だけ。`defineConstraints` は空、`includePlatforms` と `excludePlatforms` は空、`autoReferenced` は false。どの assembly もこれを参照しない。新しい folder の `.meta` も AC9 の「§3 の新規 file と `.meta`」に入る | — | test の制約を持たないので Player の compile に入る。参照は Framework の test の領域から Framework の Runtime への向きで、Game → Framework の向きと循環に辺を足さない。Phase D まで、SampleGame の Player にも 2 つの assembly の型が入る（どの Scene も使わず、runner は環境変数が無ければ動かない）。一時の物なので `unity/Assets/README.md` の依存図には載せない（S2-1 と同じ） |

合計は約 3,600 行（S2-1 は約 3,200 行）。`LogAssert`、round の並びを変えた 5 つの test、対照の順の round は写さない（Player の process は round ごとに新しい）。Editor の Play の往復は、条件付きの対照（AC6）の 1 file にだけ持つ。

**fixture（`Assets/OsmSpikePov`、SampleGame を使わない。中身は S2-1 と同じ）:**

- 共通物: `SharedShader`（variant の無い自作 shader。`Lit` を使わない）、`SharedTex`、`SharedMat`（`SharedShader` と `SharedTex`）、`SharedPrefab`（`SharedMat` の Renderer と、`ProbeBaseOnly`・`ProbeBoth`。base の root だけが持つ）。
- 編集する物: `EditedTex`（読める小さな texture。v1 と v2 で違う単色。GUID は同じ）、`EditedMat`（`SharedShader` と `EditedTex`）。
- base だけの物: `KeepTex`、`KeepMat`、Scene `SceneKeep`（Renderer が `SharedMat`・`EditedMat`・`KeepMat` を直接参照し、GameObject が `ProbeBaseOnly` と `ProbeBoth` を持つ）。
- 両方にある Scene: `SceneEdit`。v1 は `Marker_v1` に `ProbeBaseOnly` と `ProbeBoth`、v2 は `Marker_v2` に `ProbeOverlayOnly` と `ProbeBoth`。どちらも Renderer が `SharedMat` と `EditedMat` を直接参照する。probe の `_tag`・`_value` は全ての component で互いに違う値にする。
- root は製品の `BuildContentRoot`（無改変）。base の root は `SceneKeep`・`SceneEdit`・`SharedPrefab`・`EditedTex`、上書きの root は `SceneEdit`・`EditedTex`。2 つの root は別の固定 GUID を持つ別の asset にする。Scene に prefab instance を置かない。collider などの、fixture が意図しない組み込みの component を持たせない（project は engine の code の stripping が有効で、base が使わない組み込みの型は Player に残らない）。
- runner の Scene: `PovBoot`（名前は `UICommon` にしない。GameObject 1 つに runner だけ）。fixture の asset を参照しない。Player の build に入れる Scene はこれ 1 つで、Editor の対照（AC6）でも同じ形の Scene を一時に作って開く。

**build の設定:**

- content（base と上書き）: `BuildPipeline.BuildContentDirectory`、`compression` は `Uncompressed`、`options` は `BuildContentOptions.None`、`extraScriptingDefines` は空（製品の `UnityContentDirectoryAdapter` と S2-1 と同じ）。
- Player: `BuildPipeline.BuildPlayer`、target は StandaloneWindows64、subtarget は Player、`options` は `BuildOptions.DetailedBuildReport`、Scene は `PovBoot` だけ、`previousBuildReportDirectories` は base の build report の directory だけ、`extraScriptingDefines` は空。build の間、scripting backend は IL2CPP、managed stripping は High、Addressables は Player と一緒に build しない（製品の `UnityPlayerBuildAdapter`・`PlayerBuildProjectMutation` と同じ値。製品の code は呼ばない）。出力は一時 root の下。実行 id と folder の名前は短くし、build の前に出力の path の長さの見積もりを記録する（IL2CPP の backup の folder が深い）。

**資源の所有と寿命:**

| 資源 | 作る・解放する所有者 | 寿命 | 失敗時 |
|---|---|---|---|
| fixture の folder と marker | `PlayerOverlayFixture`（第 1 process） | 第 1 process の中 | `finally` で削除。次の開始時に marker のある残りだけ回収 |
| 変えた 3 つの設定 | `PlayerOverlayProjectScope` | Player の build の間（1 つの同期の呼び出し） | `Dispose` で戻して保存し、file の hash を照合する。戻せなければ test を失敗にして何が残ったかを record に書く。Unity が途中で終わった時は、実行担当が `git status` で検出して 2 つの file を `git restore` で戻す（AC9） |
| build の出力（2 つの directory、Player）、fixture の事実の file、record、log | `PlayerOverlayBuilds`（作る）、`PlayerOverlayLaunch`・`PlayerOverlayEditorControl`（読む・足す） | 実行 id ごとの一時 root（`TestResults/pov/<run id>/`、git 管理外）。evidence の保存を確かめた後、Phase D の後始末で消す | 他の実行の root に触れない |
| Player の process | `PlayerOverlayLaunch`（起動と停止）、`PlayerOverlayRunner`（自分の上限） | 1 つの round | 上限の時間の後、launcher が自分の起動した process だけを止めて記録する。launcher が先に死んだ時は、runner の timer が process を終える |
| Editor の対照の Scene と環境変数 | `PlayerOverlayEditorControl` | 1 つの test | `finally` で Play を抜け、Scene を消し、環境変数を戻す |
| 登録と load の token・Scene | `PlayerOverlayRunner` | 1 つの round（Player では 1 つの process） | `finally` で Release・unload・解除。Player で残れば process の終了で消える（記録する）。Editor で残れば、対照の test が登録の一覧の handle から解除する |
| probe・runner の script と 2 つの asmdef | 本スライスの branch | Phase D まで | harvest の commit で削除 |

## 4. 実装計画

- 順序: (1) 期待の表と pure の 2 file。(2) probe・runner・観測・record（Player 側）。(3) 棚卸し、fixture、設定の scope、build。(4) launcher と Editor の対照。(5) 実行は Phase C。
- Phase B から Phase A へ差し戻す条件: 製品 code の変更か、§3 の asmdef 以外の asmdef の追加・変更、または §3 の 2 つの asmdef への、§3 に書いた以外の参照・define 制約の追加が要る。EditMode の test から Player を build できず、別の入口が要る。製品の起動が spike の Player で round を妨げ（P0 が成り立たない、process を終わらせる）、製品を変えずに避けられない。P1〜P3 を満たす fixture が §3 の形で作れない。IL2CPP・High の Player で、単独の基準が §2 の語で観測できない（`FindObjectsOfTypeAll`・`EntityId`・`JsonUtility`・`GetPixels32` のどれかが使えない）。probe の型を Player に残す仕掛けが要る（下の禁止）。
- **禁止（答えを変えるので、B 適応にしない）:** probe の 3 型を Player に残す仕掛け（`[Preserve]`、`link.xml`、runner・観測からの型の静的参照、`typeof`、`GetComponent<T>` の型引数）を足さない。runner と観測に期待値を持たせない。launcher に観測の行を書かせない。
- B 適応の範囲: 環境変数の名前（`SAMPLEGAME_` で始めない）、record の項目名、fixture の asset の細部（大きさ、色、GameObject の構成）、component の付け方、S2-1 の harness から写す範囲、1 起動あたりの上限の時間（既定は 180 秒）と終了行の後の猶予（既定は 30 秒）と「終わらない」を読む無音の時間（既定は 60 秒。どれも、変えたら record に残す）、test の `[Timeout]` の値、runner の asmdef の `UniTask` の参照を使わずに外すこと、Addressables の設定の読み書きの手段（reflection か `SerializedObject`）、Player の出力の scripting assembly の一覧と build report の読み方、runner の Scene 自身が生成する IL2CPP の型のための保持（runner の assembly に限る）。
- 対象外を維持する方法: 製品 file を変えない（AC9）。build の出力と record は `TestResults/` 下の一時 root に置く。file の名前と hash の一覧は機械向けのデータなので git に入れず、evidence bundle に置く。

## 5. テストとレビュー計画

- 単体テスト: `PlayerOverlayVerdictTests`。
- 差し戻し中の起点 `-Filter`: `OneStarMaker.Tests.Editor.Spikes.PlayerOverlay.PlayerOverlayVerdictTests`。
- 判定必須テスト（GO 候補 head）: 次の 3 つを順に、それぞれ別の Unity process で実行する。(1) `pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.Spikes.PlayerOverlay.PlayerOverlayVerdictTests`。(2) `-Filter OneStarMaker.Tests.Editor.Spikes.PlayerOverlay.PlayerOverlayBuilds.FirstProcess`。(3) `-Filter OneStarMaker.Tests.Editor.Spikes.PlayerOverlay.PlayerOverlayLaunch.SecondProcess`（(2) の出力を読み、base だけ・base だけの寿命・上書きだけ・読まれる側・class と実体の数・寿命の 6 つの round を 3 つの process ずつ、計 18 回 Player を起動する）。**条件付き:** (3) の record から読んだスパイクの答えが「同じ」でなかった時だけ、(4) `pwsh tools/run-tests.ps1 -WithGraphics -Filter OneStarMaker.Tests.Editor.Spikes.PlayerOverlay.PlayerOverlayEditorControl.<round>` を、主の順の 3 つの round について 1 つずつ、それぞれ別の Unity process で実行する（主の順で process の間の観測が違った時は、round ごとに 3 回。Enter Play Mode の設定は project の今の値（Domain Reload 無効）のままで、変えない）。(1)〜(3) の各 XML の total が 1 以上で failed 0、root result `Passed`、(3) の launcher の file に全 round の process の起動と終わり方がある。ここまでは harness が最後まで動いたことの確認で、その後に判定 C が §2 の定義を record に当てて答えを読む。**Unity の process の自然終了をゲートにしない**（XML の完成後に Unity が終わらない既知の終了 stall は、[Harness README](../../tools/Harness/README.md#unity終了時の既知native-stallと調査の停止規則) の規則に従い、XML と record で判定する。第 1 process を強制終了した時は、AC9 の (2) を必ず確かめる）。
- 全 EditMode 回帰の適用除外: **適用除外する。** 理由: 製品 code を変えず（AC9）、build と Player の起動を行う `[Explicit]` の実験 test は通常の回帰に混ぜない。足すのは、どこからも参照されない asmdef 2 つと、その中の、環境変数が無ければ動かない型だけである。代替証拠: AC9、Unity のコンパイル確認、`contract-audit`、(1) の単体テスト。
- 操作・実行時・目視条件の検証経路: 全て上の 3 コマンド（と条件付きの (4)）で閉じる（Unity の batch と、その test が起動する Player で、Phase C の実行担当が sandbox 外の承認済み経路で実行する）。初期状態は Editor を閉じ、Active Build Target が StandaloneWindows64、作業ツリーが clean、他の Unity が動いておらず、同じ製品名の Player（前の実行の物を含む。user の folder を共有する）が動いていない。事前に `git status --porcelain` を取る。観測は record・launcher の file・Player の log・XML・Unity の log。事後に、各 Unity の process が終わってから `git status --porcelain` を取り、launcher の file にある pid が全て終わっていることを CIM で確かめる（AC9 の (2)）。目視は無い。証拠は record・launcher の file・Player の log・XML・各 build の `ContentLayout.json` の写し・Player の build の結果を evidence bundle に入れる。動作確認は `対象なし`（offline のテストで閉じない条件は全て Unity の batch と Build で、Phase C が担当する）。
- 未知の操作経路の疎通: 次の 5 つは未確認で、**初回確認は Phase C**（理由: Unity の test と Build の実行は Phase C の責任。担当: Phase C の実行担当）。Phase A では code を読むことと、この PC に IL2CPP の Player の support があることだけを確かめた。
  - (u1) EditMode の test から `BuildPipeline.BuildPlayer`（IL2CPP）を `run-tests.ps1` の batch で完走できるか（製品の Player の build は menu からで、test からの前例は無い。test から外部の process を起動する前例はある）。確認地点: (2) の record の Player の build の結果。不成立時: C++ の toolchain の欠落など環境の不備は B が原因を記録し、担当の権限で直せない物は owner へ返す。test の中から build できないことが原因なら Phase A へ戻す。
  - (u2) 製品の起動が spike の Player で round を妨げないか（code の読みでは、`content:runtimeMode` の無い Player は `BeforeSceneLoad` の失敗を log に出して何もしない。Addressables 経由の設定の同期 load が、data の無い Player で失敗して先へ進むことは実行で確かめる）。確認地点: (3) の最初の round の P0 と Player の log の起動の段。不成立時: Phase A へ戻す（製品を変えずに隔てる方法（別の最小の project で Player を作る等）を比べる）。
  - (u3) probe の asmdef の `MonoBehaviour` が IL2CPP・High の Player に残り、Content Directory の component がその型に解決されるか。確認地点: P4 と、base だけの round の class。不成立時: `ProbeBaseOnly` か `ProbeBoth` が無ければ未判定で B へ（base の build report の渡し方を直す。型を残す仕掛けは足さない）。`ProbeOverlayOnly` だけが無ければ答えの一部（§2 の「型の欠落」）。
  - (u4) `-batchmode` の IL2CPP の Player（Phase C の実行担当の環境で graphics の device を作れること）で、読める texture の pixel、material、`FindObjectsOfTypeAll`、`EntityId`、`JsonUtility.ToJson` が観測に使えるか。確認地点: record の先頭の graphics の device の種類と、単独の基準（AC2 の (1) の版・class・pixel・数と列挙の照合）と AC7。不成立時: device を作れない環境なら、`-nographics` を足して観測できるかを見て、Phase A へ戻して範囲の語を改める。API が使えないなら Phase A へ戻す。
  - (u5) Player の build が、同じ `Library` の後続の content の build と、戻すべき設定に残す影響。確認地点: 上書きの棚卸しの P2・P3、AC9 の 2 つの時点。不成立時: 設定の残りは B が復元を直す。content の出力が変わるなら Phase A へ戻す。
- 人間の判断が必要な条件: なし（結論を受けた方式の採否は Phase D で owner）。
- 高コスト証拠: なし。Player の build は外部への作用が無く、この PC で繰り返せる（所要は未記録で、初回の実行で record に残る）。build は content 2 回と Player 1 回、Player の起動は 18 回、Unity の起動は 3 回（条件付きの Editor の対照で 3 回、process の間の観測が違った時は 9 回増える）。
- 判定 C の照合 script の方式: なし。判定 C は record の行を AC ごとに読む。`PlayerOverlayVerdict` の出力は補助である。
- 機械検査: `contract-audit`、`docs-audit`。
- A0/A1 主担当: 本 session（Claude Fable 5.2、Anthropic）。A0 は program §8 の S4 の行と「S4 の A1 への入力（2026-11-23）」、各「移送した入力」の S4 の項目、実測の事実、13 章を入力とした。
- C' 用に予約した担当: Phase A・B・C に関与していないベンダー（`cursor-agent` を使う場合は AGENTS.md の契約どおり Grok 系だけ）。A2 では Anthropic の系列だけを使った。
- 独立性の強化条件を満たせない場合の理由: 凍結前の外部計画レビューが使った系列は、Phase A に関与した系列として §5.1 に記録する。C' に同じ系列しか残らない時は、独立性の制約を §8 に記録する。C' の充足は起こす時に確かめて記録する。

### 5.1 A2 の独立レビューと A3 の採否

**A2 の独立レビュー（実施 2026-11-23。互いの指摘を見せていない。全て Anthropic の系列で、別ベンダーは外部計画レビューと C' 用に残した）:**

- (1) **目的レビュー:** 本 session が起動した新しい subagent（Claude Opus、Anthropic）。リポジトリを読まず、ツールを使わずに回答した。入力は §1 の目的と利用者の欄の原文だけで、sha256 は `13eb5d330cdf26b0abb02e106aaeefca7a9726b80b6cc1bb06046bc84ab694ad`（目的欄の本文、改行、利用者欄の本文。UTF-8、LF、末尾改行なし）。問いは「この目的のために何を保証すべきか」「何を作らないべきか」の 2 つだけ。
- (2) **A0 だけからの代替構成:** (1) と同じ subagent が、目的レビューの回答を記録した後に続けた。入力は A0 の packet（program の S4 の行と「S4 の A1 への入力」の原文、読んでよい file の一覧、Unity の公開文書から確かめた事実。sha256 `8bcb001fae89f4effd081382736c7ee2db6558833f682fba9f22173926eea4b7`）と、固定 base `214806c194716f3a87159fa519d2d7537b5d844f` の clean な detached checkout。本書は読んでいない。
- (3) **アーキテクチャゲート兼、各不変条件を破る順序・誤った答えの道・受け入れ条件の曖昧さ・検証経路の実行可能性・S4 の分割:** 本 session が起動した別の新しい subagent（Claude Fable 5.2、Anthropic。A1 の主担当と同じモデルの別 session）。入力は A2 入力版の本書（内容 sha256 `f97aeba6dd41b93a8e588c19d4bea9067fcddb159dca3798aeef220386294742`）、変更後の program の写し（sha256 `18152b18539e7a5ea7d11d65b539478783e3e6a6f56732c2757190451a764935`）、(2) と同じ固定 base の checkout。枠は「§3・§4 の計画した code は実装済みと仮定し、現在の code は既存の前提の確認にだけ使う」に固定した。

**A3 の採否（統合担当: 本 session。目的と利用者の欄は A1 の後に変えていない。目的レビューのやり直しは無い）:**

(1) 目的レビュー。保証すべき 9 点（版を見分けられる形、base だけの物の class、実体の数、解除までの健全性、条件の固定と記録、比べる相手を同じ題材と観点で持つこと、Player の実測だけを根拠にすること、観点ごとの一致・不一致を示すこと、否定の結果も成果にすること）は、初稿に全てあった。作らないべき 9 点のうち、初稿と食い違ったのは 1 点である。

| 指摘 | 採否 | 理由と反映 |
|---|---|---|
| base → 上書き以外の順序の検証をしない | 採用 | 初稿の「主の順の版が違った時だけ、上書き → base の対照を測る」を落とした（§1 の対象外、§2.5）。答えが否定的な時は owner へ返す |
| 性能や load の時間を方式の判断基準にしない | 採用（明記） | 所要は record に残るが基準にしない、と §1 の対象外に書いた |
| 他の 7 点（供給元、届ける経路、UI、差分の自動選択、base の取得、その場での修正、製品向けの抽象） | 初稿のとおり | どれも初稿が作っていない |

(2) A0 だけからの代替構成。推奨案（同じ project、使い捨ての Player、1 round を 1 process、Unity の公開 API を直に呼ぶ runner、base → Player → 上書きの build の順、IL2CPP・High）は初稿と同じ構成だった。違った点の採否:

| 指摘 | 採否 | 理由と反映 |
|---|---|---|
| runner の設定を環境変数で渡す（command line は製品の設定の読み口も読む） | 採用 | §2 の round の語、§3。Editor でも同じ runner が動く形になった |
| probe の asmdef を前例と同じ参照なしの形で残し、runner は別の asmdef にする | 採用 | §3。同じ assembly に置くと、Player の Scene が runner を使うことで probe の型が残り、P4 の答えを harness が変える（(3) の M-8 も同じ指摘） |
| 解決できない script の component は、`null` で残らず列から消えることがありうる | 採用 | 正しい class の定義に列の長さを足し、script が無いことを言う log の行も数える（§2 の (f)） |
| 同じ runner を Editor の Play でも走らせ、差を host の差だけに絞る | 一部採用 | 答えが「同じ」でない時だけ走らせる（AC6）。Player の観測が fixture の定義と Editor の実測の両方に一致した時は、走らせても答えが変わらない |
| `-nographics` を付けずに起動し、graphics の device を記録する | 採用 | §1 の対象外、§2 の成果の範囲、§2.5 |
| Player の出力の scripting assembly の一覧に probe の assembly があるかを記録する | 採用 | P4、AC1 |
| 製品の起動を、`SAMPLEGAME_CONTENT__RUNTIMEMODE=directory` を置いて確実に失敗させる | 不採用 | 設定が無くても同じ箇所で失敗する（code の読み）。代わりに、`SAMPLEGAME_` の環境変数を除いた環境で起動し、失敗したことを P0 で確かめる |
| 対照の順の round を無条件で入れる | 不採用 | (1) の目的レビューと owner の入力が、主の順に限っている |
| Player に入らない型を使う component を別の上書きに置く負の対照 | 不採用 | 期待の列との長さ・位置ごとの照合が、Missing と欠けの両方を拾う |
| 実体の数の判定に、参照をたどった `EntityId` の集合を使う（列挙は host で違いうる） | 初稿のとおり | 数は参照をたどった集合で数え、列挙との一致は数え方の成立の条件にしている（AC7）。Player には source の asset が無い |
| 「検証済みの base」を digest の一致として読み替えることを明記する | 採用 | §1 の対象外の 1 つ目 |
| S4 の割り方: Local / LAN / hook・prefetch・base の選び方を別の行にし、要求の時点の install は Local の行に入れる。Android・iOS の行は置かない | 不採用 | 要求の時点の install は、13 章の「load の後の登録の禁止」と当たるので、実測の gate を持つ別の行にした。供給元の中の割り方は、S4-3 の A1 が決めると行に書いた。Android・iOS は、S3-4b が S4 へ送った入力なので、所有する行を残した |
| S4-1 の A1 は S4-0 の後に始める方が手戻りが無い（上書きの差込口を残すかが答えで変わる） | 採用 | §2.4、program の S4-1 の行 |

(3) アーキテクチャゲート兼反証。blocker 2 件、major 13 件、minor 12 件。

| ID | 指摘 | 採否 | 理由と反映 |
|---|---|---|---|
| BL-1 | 「同じ」の定義が、Editor の実測のうち 3 点（両方の root から load した `EditedTex` の実体が 1、base だけの Scene の直接参照が v1、Error・Exception が 0）を含まない | 採用 | X1〜X3 を足し、「同じ」の条件に入れた。現況の (a) に実体 1 を足した。AC10 に入力を足した |
| BL-2 | 「答えにできる失敗」が失敗の時点を条件にしていない。終了行の後の終わり方、1 つ目の登録より前の失敗、開始行の無い round の扱いが無い | 採用 | 4 つの条件、終了行のある round の扱い、開始行の無い round を未判定とすることを §2 に書き、AC5 に例を足した |
| M-1 | runner が購読する前の log は record に無い。製品の `AfterSceneLoad` の Error が round の段に入る | 採用 | round は `Start` 以降に始め、開始の印を出す。起動の段の一次証拠は Player の log の file にした |
| M-2 | P0 は、製品の起動が成功して動いている状態を検出しない（親の process の `SAMPLEGAME_CONTENT__RUNTIMEMODE` の継承） | 採用 | `SAMPLEGAME_` を除いた環境での起動、P0 の (1)（製品が止まった 1 行）、Scene と root の GameObject の記録。現況に設定の読み口を足した。root の GameObject は Unity や package が作る物がありうるので、一覧は参考とし、成否には使わない |
| M-3 | exe の sha256 と、launcher が渡した実行 id の書き戻しでは、起動した Player が同じ build の物だと言えない | 採用 | Player の出力全体の棚卸しの digest と、`Application.buildGUID` と build report の GUID の照合にした |
| M-4 | 変えた設定が disk に残る順序が 2 つあり、AC9 の「前後」の時点が決まっていない | 採用（一部を簡単にした） | scope を 1 つの同期の呼び出しにし、復元の後に保存して file の hash を照合する。AC9 を 2 つの時点に分けた。一時 root に復旧の記録を書く案は採らず、Unity が途中で終わった時は実行担当が `git status` で検出して 2 つの file を `git restore` で戻す（追跡している file なので、それで足りる） |
| M-5 | launcher の process が死ぬと Player の process が残る | 採用 | runner が自分の上限を持つ。launcher は pid をすぐ記録する。事後に pid の終了を確かめる |
| M-6 | 単独の基準が、主の順の操作の列（2 Scene の同時保持、unload と load し直し、計数）を踏まない | 採用（無条件にした） | base だけの寿命の round を単独の基準に足し、base だけの round に計数を足した。条件付きにしないのは、Player の起動 3 回で済み、判定の条件が簡単になるため |
| M-7 | 型の欠落の時に Q4 も「壊れた」になる。B が型を残す仕掛けを足せる | 採用 | 「壊れた」の class を load 直後からの変化で読む。型を残す仕掛けを §4 で禁止した |
| M-8 | probe と runner を同じ asmdef に置くと P4 が交絡する | 採用 | (2) と同じ |
| M-9 | 「壊れた」の判定と期待値が Player 側に入り、同じ事実が 2 つの assembly に分かれる | 採用 | runner は生の値だけを書き、判定は pure の写像が行う。期待の表を 1 つの file にし、runner は fixture の事実の file から key と名前を読む |
| M-10 | 設定の scope が Addressables の設定を触る手段が決まっていない（test の asmdef は参照を持たない） | 採用 | reflection か `SerializedObject` と §3 に書いた（どちらかは B 適応） |
| M-11 | Player 本体に何が入ったかを build report で確かめていない | 採用 | packed assets の確認と、Addressables の data の有無の記録を AC1 に足した |
| M-12 | 別の PC の base × Player × 上書きの組み合わせに所有が無い | 採用 | S4-2 の A1 の最初の gate に、別の `Library` の base を載せた Player での答えを置いた（§2.3、§2.4、program） |
| M-13 | S4-1 の開発用の Player の構成が本スライスと違う時、native の答えの測り直しに所有が無い | 採用 | S4-2 の A1 の最初の gate に置いた。harvest の「測り直し」に構成の変え方を残す |
| m-1 | AC3 の操作の順が決まっていない | 採用 | 前例と同じ順を AC3 に書いた |
| m-2 | 対照の条件の文が AC6 と §5 で違う | 該当なし | 対照の順を落とし、AC6 を Editor の対照に置き換えた。条件は AC6 と §5 で同じ文にした |
| m-3 | AC9 は、文字どおりには本書を変える commit で不合格になる | 採用 | 本書と program を除いた |
| m-4 | process の間の一致が「答え」の一致で定義されている | 採用 | 観測の行の一致にした |
| m-5 | Q1・Q3 に定義の無い組み合わせがある | 採用 | 未判定と書いた |
| m-6 | 「Player の record だけから答える」と、launcher が見た終わり方に依る「答えにできる失敗」が合わない | 採用 | launcher が書いてよい行の種類を定義し、I-2 に書いた。record は process ごとに別の file |
| m-7 | 一時 root を消す所有、user の folder、`ProjectSettings/` の新しい file、path の長さ、初期状態の理由 | 採用 | I-3、資源の表、§3、§5 |
| m-8 | `-executeMethod` の代替に、コマンドとゲートが無い | 採用（代替をやめた） | 代替を B 適応から外し、不成立時は Phase A へ戻す |
| m-9 | `PlayerOverlayBuilds.cs` が 500 行を超える。P1〜P3 をどちらが判定するかが読めない | 採用 | 棚卸しを別の file に出し、P1〜P3 は pure の写像が事実から決めると書いた |
| m-10 | 成果の範囲に足す条件（検証を通さない base、製品の起動の状態、`Library` の履歴） | 採用 | §2 の成果の範囲、I-1 の (i) |
| m-11 | 振り分けの細部（S2-0b の結論と対照の順は本スライスが測らない、類似事例の表の「S4」、13 章の「S4」、S4-2 の開始条件、S4-4 の混在、program の変更箇所の列挙） | 採用 | §0 の harvest、§2.3、§2.4、program の S4-2・S4-4 の行と振り分けの項 |
| m-12 | `InternalsVisibleTo` の相手に `OneStarMaker.Editor` もある | 採用 | 現況を直した |

**影響する A2 観点の再レビューの要否:** A2 の指摘の反映は、判定の定義と責務マップを広く変えた（X1〜X3、答えにできる失敗の条件、生の値と判定の分担、asmdef を 2 つにしたこと、Editor の対照）。その統合版は A2 の担当に戻さず、Draft PR の外部計画レビューが最初に見た。下の 1 巡目・2 巡目の反映と凍結前の見直しは、判定の定義と後続の開始条件だけを変え、責務マップ・asmdef・build の順・S4 の行の割り方は変えていない。A2 の担当には戻さず、外部計画レビューの次の巡が、直した定義を見る。

**外部計画レビュー（Draft PR #209）:**

1 巡目（2026-11-23。PR の総評コメント 1 件。対象は commit `3fd78a25` の本書。レビュー担当のモデル系列は、コメントに書かれていない）。方向（製品の登録の口を通さず、Unity の native の挙動だけを Player で測ること、「成り立たない」も証拠付きなら GO とすること、S4-1 と S4-2 を分けること）は支持で、S4 の分割を変える理由は無いとした。major 2 件は、どちらも「測ろうとしている失敗が、正しい答えに写らない」指摘である。

| 指摘 | 採否 | 理由と反映 |
|---|---|---|
| 1. Error・Exception があっても「同じ」「違うが成り立つ」になれる。X3 が数えるのは観測の段だけで、登録の段と AC5 の (6) の段は数えない。「違うが成り立つ」は X3 の不一致を許す | 採用 | X3 を round の全ての段（開始の印から終了行まで）に広げ、「同じ」と「違うが成り立つ」の両方の条件にした。1 件でもあれば「成り立たない」で、owner へ返す側に入る。「無害な既知の log」の一覧は持たない。代わりに、同じ操作を 1 つの directory で踏む単独の基準が Error 0 件であることを求め（外れれば未判定）、主の順にだけ出た Error を 2 つの directory の登録に帰せる形にした。製品の起動の log（開始の印より前）と、終了行より後の log は、今までどおり数えない。型の欠落の時に上書きだけの基準にも共通に出る Error だけは、型の欠落に伴う log として数えない。AC10 に 3 つの入力を足した |
| 2. 主の順で load が失敗し、runner が失敗を記録して終了行まで書いた時、Q3 の 0 が未判定になり、「成り立たない」へ進めない。終了行の有無で答えが変わる | 採用 | 操作ごとの開始と結果の行と、失敗の定義（§2 の「操作と、その失敗」）を足した。「答えにできる失敗」を、(A) 操作の失敗が record にある、(B) record が途中で切れている、の 2 つにし、終了行の有無に依らず同じ条件で読む。Q3 の 0 を未判定にするのは、load の操作が全て成功している時だけにした。AC5 に例を、AC10 に入力を足した |

2 巡目（2026-11-23。PR の総評コメント 1 件。対象は commit `31c95977` の本書。レビュー担当のモデル系列は、コメントに書かれていない）。1 巡目の major 2 件は解消したと判断し、「主の順でだけ起きる異常を捨てずに答えにする」方針も支持した。その上で major 1 件。S4 の分割と他の判定の修正に、新しい阻害する指摘は無い。

| 指摘 | 採否 | 理由と反映 |
|---|---|---|
| process の間の不一致を「違うが成り立つ」として、S4-2 に無条件で送れる。同じ条件の再実行で base の root の解決が揺れることは、Q1 の弱を許すかという契約の選択とは違い、S4-2 が何を前提にしてよいかを決める必要がある | 採用 | 答えの 3 つの分類は増やさず、主の順で process の間の観測が違った時は、答えに印を付け、違った観測ごとに 3 つの process の値を並べて添える（§2「process の間の違いの扱い」）。後続の表で別の行にし、S4-2 を自動では始めず、owner が進め方を裁定する。S4-2 の開始条件（§2.4、program の S4-0・S4-2 の行）に「process の間の観測が一致」を足した。この時だけ、Editor の対照を round ごとに 3 回走らせ、揺れが Player に固有かを切り分ける（AC6）。AC10 に入力を足した |

3 巡目（2026-11-23。PR の総評コメント 1 件。対象は commit `2c24044d` の本書と program。レビュー担当のモデル系列は、コメントに書かれていない）。2 巡目の major 1 件は解消したと判断し、新しい阻害する指摘は無く、凍結へ進めてよいとした。確かめた点として、判定と後続の判断の分離、型の欠落の例外にも同じゲートがあること、Editor の対照の検証経路、§2 の後続の表・§2.4・program §8・AC10・§5.1 の一致を挙げた。直す所は無い。あわせて、計画のレビューなので、IL2CPP の Player の build、process の実行、型の保持、Editor の対照（u1〜u5 を含む）の成立を承認したのではなく、実測と evidence の成否は Phase C で判断する、と明記している。

**A3 の凍結（2026-11-23）:** owner（root）が凍結と Phase B の開始を指示し、revision 0 として凍結した（§0 の A3 凍結記録）。目的と利用者の欄は A1 の後に変えておらず、目的レビューのやり直しは無い。A2 と外部計画レビューの後に A2 の担当へ戻していない変更（判定の定義と後続の開始条件）は、外部計画レビューの 2 巡目と 3 巡目が見た。

**凍結前の見直し（2026-11-23。owner の指示: 挙動を測るという目的への難点と、Phase C で指摘されそうな点を見直す）:** 1 巡目の 2 件と同じ型（主の順でだけ起きた実際の挙動が、未判定か「成り立つ」側へ落ちる）と、凍結した文をそのまま読むと必ず外れる・読み手で割れる条件を、本 session が洗い直した。

| 見直した点 | 直し方 |
|---|---|
| 主の順で process の間の観測が違うと未判定だった。2 つの directory の時だけ不安定、という挙動を捨てる | 主の順では、違うこと自体を Editor との違いとして答えに入れ、最も悪い process の答えを採る。単独の基準で違う時は未判定のまま |
| 主の順で、参照をたどった数と列挙の数が合わないと未判定だった。参照から届かない余分な実体、という挙動を捨てる | 数え方の成立は単独の基準で確かめる（AC7）。主の順で合わない時は、多い方の数で読み、違いを挙げる |
| 主の順で、load は成功したが中身が v1 でも v2 でもない時が未判定だった | Q1 の不成立と読む。Q1 から未判定の組み合わせを無くした |
| 単独の基準が「期待と一致」としか書かれていなかった | 「単独の基準が成り立つ」を語として定義した（終了行、操作の成功、Error 0 件、観測の一致、process の間の一致） |
| 操作が失敗した後、runner がどう振る舞うかが書かれていなかった | 残りの観測を行わずに後始末へ進み、終了行に打ち切った段を書く。観測できなかった問は、答えを作らず、そう書く |
| runner の上限の timer が書く行が、「止める直前の 60 秒に行が増えていない」の条件を自分で破る | 上限の超過の行は数えない、と書いた |
| 「短い猶予」「Unity の版が 6000.6.9f1」のように、読み手で割れる・Editor の更新で必ず外れる語 | 猶予は 30 秒、版は第 1 process の record（project の版）と同じ、にした |
| 設定を戻して保存した時、Unity の再 serialize で、変えていない行に差分が出ると、AC9 の hash の一致が外れる | scope が、2 つの file を変える前の bytes に書き戻す |
| Editor の対照が「Domain Reload を無効にした状態で」と書かれ、追跡している設定を実行担当が変える読みができた | project の今の値（無効）のままで変えない、と書いた。違えば未判定 |
| Editor の対照が Editor の実測を再現しない時、B へ戻すとしか書かれていなかった（条件の違いが原因なら B は直せない） | B が runner の誤りを見つけられない時は Phase A へ戻す |
| 単独の基準が成り立たない時の出口が無かった（未判定のまま終われない） | harness の誤りでないことを B が確かめた上で Phase A へ戻し、owner が裁定する |
| base だけの寿命の round の中身が「AC5 と同じ列」としか書かれていなかった | 何を base から読むかを AC2 に書いた |
| test の待ち合わせが `Task.Delay`・`Thread.Sleep` の禁止（常時契約）に当たらないことが読めなかった | process の終了の signal と frame の進行で待つ、と書いた |
| 新規の `.cs` の `#nullable enable` が probe の行にしか書かれていなかった | §3 の前文に、全ての新規 file について書いた |
| Active Build Target が初期状態にあるだけで、誰が確かめるかが無かった | 第 1 process が記録し、違えば止まる（AC1） |

## 6. Phase B 実装結果

2026-11-23。§3 の新規 file だけを §4 の順序で作った（製品の file、§3 以外の asmdef・参照・define 制約の変更は無い）。S2-1 の harness は `git show a304963b:<path>` で読んで写した。

**作った file（行数）:** `Tests/Editor/Spikes/PlayerOverlay/` に `PlayerOverlayExpected.cs`（131）、`PlayerOverlayVerdict.cs`（617）、`PlayerOverlayVerdictTests.cs`（496）、`PlayerOverlayInventory.cs`（236）、`PlayerOverlayFixture.cs`（314）、`PlayerOverlayProjectScope.cs`（101）、`PlayerOverlayBuilds.cs`（486）、`PlayerOverlayLaunch.cs`（182）、`PlayerOverlayEditorControl.cs`（115）。`Tests/Spikes/PlayerOverlayProbes/` に probe の 3 型（各 20）と asmdef（参照・define 制約・platform の指定なし、`autoReferenced` false）。`Tests/Spikes/PlayerOverlayRunner/` に `PlayerOverlayRunner.cs`（507）、`PlayerOverlayObservations.cs`（120）、`PlayerOverlayRecord.cs`（106）と asmdef（参照は `OneStarMaker.Runtime` と `UniTask` だけ、`autoReferenced` false）。全て先頭に `#nullable enable`、`record` 型なし。合計 3,471 行（計画は約 3,600 行）。

**計画の行数を超えた file（非分割のまま）:** `PlayerOverlayVerdict.cs` は 617 行（計画 490）、`PlayerOverlayRunner.cs` は 507 行（計画 400）。前者は 1 つの判定の写像の表で、§3 の非分割の判断の理由（1 つの写像）がそのまま当たる。後者は round の手順と操作の開始・結果の行を 1 つの `MonoBehaviour` に持ち、観測の読み取りと record の書き出しは §3 どおり別の file にある。責務の数は計画と同じなので分けていない。

**B 適応と判断の記録（§4 の B 適応の範囲として行った。発見 C が範囲に入るかを確かめる）:**

- **環境変数の名前:** `OSM_POV_ROUND`・`OSM_POV_LABEL`・`OSM_POV_ORDER`・`OSM_POV_DIR_BASE`・`OSM_POV_DIR_OVERLAY`・`OSM_POV_FACTS`・`OSM_POV_RECORD`・`OSM_POV_RUN`・`OSM_POV_HEAD`・`OSM_POV_LIMIT`（Editor の対照だけ `OSM_POV_DOMAIN_RELOAD_DISABLED`）。登録の順は launcher が `PlayerOverlayVerdict.RegisterOrder` から渡し、runner は渡された順に登録する。
- **record の形:** runner は行の種類ごとに名前の付いた欄を JSON で書き（`header`・`round-start`・`p0`・`p4`・`op-start`・`op-result`・`ids`・`scene`・`texture`・`components`・`count`・`keep-edited`・`held`・`directories`・`log`・`limit-exceeded`・`end`）、Editor 側は全ての欄の和の型 `PovLine`（`PlayerOverlayInventory.cs`）に `JsonUtility` で読む。欄の名前が 2 つの assembly に分かれる（期待値ではなく行の形）。runner が読む fixture の事実の file の型も同じく 2 か所にある。「壊れた」の比べる相手（load 直後の列）は、runner が保持を始めた直後に stage `loaded` の `held` 行として書き、判定の写像が確認点の行と比べる。
- **上限:** runner の上限 180 秒、終了行の後の猶予 30 秒、「終わらない」を読む無音 60 秒（既定どおり）。launcher の上限は runner の上限 + 60 秒で、launcher の file の `second-start` 行に残る。launcher は frame を進めながら約 1 秒ごとに record の終了行を見て、止めた後は `Process.WaitForExit(10000)` で終了の signal を待つ。
- **一時 root の名前:** `TestResults/pov/<MMddHHmmss>/`、出力の folder は `base`・`ovl`・`ply`、Player は `Pov.exe`（IL2CPP の backup の folder の path を短くするため）。第 1 process の `player-build-start` 行に exe の path の長さと、最も深い path の見積もりを残す。
- **`previousBuildReportDirectories` に渡す directory:** base の build の `Library/BuildHistory/<session>`（`ContentLayout.json`・`ScriptsOnlyCache.yaml`・`.buildreport` がある folder）を渡した。§3 の「base の build report の directory」と §1 の公開文書の事実（型は build report の directory の `ScriptsOnlyCache.yaml` に記録される）に合わせた。**製品の `UnityContentDirectoryAdapter` は content の出力 folder を渡しており、ここは製品と違う。** P4 で `ProbeBaseOnly` か `ProbeBoth` が Player に無ければ、§5 の u3 のとおり B が渡し方を直す（型を残す仕掛けは足さない）。第 1 process の `inventory` 行に、渡した directory と `ScriptsOnlyCache.yaml` の有無を残す。
- **Addressables の設定の読み書き:** reflection（`AddressableAssetSettingsDefaultObject.Settings` の `BuildAddressablesWithPlayerBuild`）。復元の後に `AssetDatabase.SaveAssets` し、`ProjectSettings.asset` と Addressables の設定の asset を変える前の bytes に書き戻し、後者は import し直す。
- **scripting assembly の一覧:** Player の `Pov_Data/ScriptingAssemblies.json` の名前、無ければ build report の managed の file。
- **Error として数える log の種類:** `Error`・`Exception` に `Assert` を含めた（Unity の Error 水準の log。含めると「同じ」「違うが成り立つ」に入りにくくなる側で、答えを甘くしない）。
- **「script が無い」log の行:** round の段の log のうち、文面に `script` と `missing` を含む行を数えて答えに添える（判定には使わない。§2 の (f) の記録）。
- **Editor の対照の Scene:** 保存しない一時の Scene に runner の GameObject を作って Play に入る（file を作らない）。Domain Reload が有効、または 2 つの directory が第 1 process の出力と違う時は、Play に入らずに test を失敗にする（設定は変えない）。
- **runner の保持:** load は成功したが期待の root（`Keeper`・`Marker_*`）が Scene に無い時も保持の対象に入れ、確認点で `object.null`（壊れた）として書く。
- **probe と runner の `.meta`:** probe の 3 型と runner の script の GUID を固定値で書いた（fixture が MonoScript を GUID から引き、P1 は layout の一覧の path を GUID と結びつけて照合する）。他の `.meta` は Editor が生成した。
- **補助の判定の file:** 第 2 process は `verdict-second.json`、Editor の対照は `verdict-<label>.json` を run の root に書く（launcher の file には `verdict` の行として file 名と結論だけを書き、観測の行は書かない）。答えが「同じ」でない時、Editor の対照の record が揃うまで補助の判定は未判定（理由に `AC6`）と出る。

**未実行（Phase C の担当）:** `PlayerOverlayVerdictTests` を含む Unity の test を一つも実行していない。content と Player の build、Player の起動、Editor の対照も実行していない。§5 の u1〜u5 は全て未確認のまま。判定の写像の test が期待どおり通るかも未確認である。

**確認したこと:** Unity 6000.6.9f1 の Editor でコンパイルが成功した（`compilationFailed` false。eval で、probe の 3 型・runner・判定の写像・launcher・Editor の対照・test の型が読み込まれ、probe と runner の固定 GUID が script の path に解決することを確かめた）。Editor は自分で起動し、未保存の Scene が無いことを確かめてから終了した。終了の後の `git status --porcelain` は新規 file だけで、`ProjectSettings/` に差分は無い。`pwsh tools/contract-audit.ps1` は合格（errors 0、warnings 0、検査 9 の path の長さを含む）。

### 差し戻し 1 巡目（2026-11-23）

発見 C（1 回目、§7）の D1〜D3 を直した。変えた file は `PlayerOverlayVerdict.cs` と `PlayerOverlayVerdictTests.cs` だけで、§3 の責務・asmdef・製品の file と §2 の語は変えていない（§4 の B 適応の範囲。Phase A へ差し戻す条件には当たらない）。

- **D1（X1 を多い方の数で読む）:** 主の順の `read` の round で、X1 の「実体 1」の条件に、`EditedTex` の計数（参照をたどった数と列挙の数の多い方）が 1 であることを足した（AC7 の「主の順で 2 つが違う時は、多い方の数で Q3 と X1 を読む」）。X1 の答えの行に数を添える。その計数の行が無い時は、他の読みの観測が無い時と同じく未判定（`read observations are missing`）にした（AC3 が列挙との照合の記録を求め、runner は必ず書く）。Editor の対照（AC6）の `ControlProblems` は同じ `ObserveMain` を通るので、集合の不一致だけは外れにしない今の扱いのまま、多い方の数で実体 2 になれば「X1 differs」として再現の外れになる。これは同じ関数の既存の注記「数の違いと他の違いは外れ」と同じ読みで、矛盾しない（Editor の実測は実体 1）。test: `ReachedAndEnumeratedDifferForEditedTex_ReadsX1WithTheLarger`（reached `[t]`、enumerated `[t, u]` → X1 は differs、答えは「違うが成り立つ」）、`ControlWithEditedTexEnumeratedTwice_DoesNotReproduce`（対照で同じ入力 → AC6 の未判定）。
- **D2（harness の例外で打ち切った round）:** `FailureAnswer` の先頭で、`harness-exception` 行がある round、または終了行の `abortedAt` が `harness:` で始まる round を、操作の失敗の行や record の切れ目があっても未判定（`aborted by a harness exception`）にした（§2 の (k)）。runner は操作の失敗を `OperationFailed` で別に捕まえるので、`harness-exception` 行がある round では、round の定義の操作の失敗は記録されていない（後に続く失敗は後始末の物）。`Judge` の「操作の失敗の無い打ち切り」の理由は、同じ round に既に理由がある時は重ねて足さない。test: `HarnessExceptionThenCleanupFailure_IsUndecided`（2 つの登録の成功の後に harness の例外と `unregister` の失敗。終了行のある入力と無い入力の両方で未判定、「cannot unload or unregister」の答えが出ない）。
- **D3（1 つ目の登録より前で切れた record）:** test `RecordCutBeforeFirstRegister_IsUndecided` を足した（1 つ目の登録の `op-start` より前で切り、終了行なし → 未判定）。code は変えていない（既存の `registers.Count < 2` の枝）。

**確認したこと:** Unity 6000.6.9f1 の Editor でコンパイルが成功した（自分で起動し、eval で `scriptCompilationFailed` false、足した 4 つの test の method が読み込まれていることを確かめた。未保存の Scene が無いことを確かめて終了した）。終了後の `git status --porcelain` は上の 2 file だけ。`pwsh tools/contract-audit.ps1` は合格（errors 0、warnings 0、検査 1〜9）。

**未実行:** Unity の test は実行していない（足した test を含め、起点 filter の合否は発見 C が確かめる）。build・Player の起動・Editor の対照も未実行で、§5 の u1〜u5 は未確認のまま。

### 動作確認

`対象なし`（§5）。

## 7. Phase C

### 発見 C（1 回目、2026-11-23）

- 種別: 発見。GO 判定はしない。C' は起動しない。
- 対象: implementation base `d926e3b6b40dafab086b293e6d60784e304ecf4c`、implementation head `16328d4d09e80ef159df37310ee41788334cd33d`（commit で固定。完全 diff は `git diff d926e3b6 16328d4d`、41 file、+4,102 / −7）。
- 使用した evidence bundle の取得案内・状態: 未生成（発見 C は固定した commit の diff と機械検査だけを入力にした。判定 C と C' の bundle は GO 候補 head で作る）。
- 構造適合: 変更 file の集合は §3 の新規 file・folder の `.meta`・本書・program だけで、製品の file と §3 以外の asmdef の変更は無い（AC9 の file 集合）。2 つの asmdef の参照・define 制約・platform・`autoReferenced` は §3 どおり。probe は runner と別の assembly にあり、runner・観測は probe の型を `typeof`・型引数・静的参照で使わず（P4 は facts の file の文字列から `Type.GetType`）、`[Preserve]`・`link.xml`・`[RuntimeInitializeOnLoadMethod]` は無い（§4 の禁止）。runner と観測は期待値を持たず、launcher は観測の行を書かない（launcher の file の `verdict` 行は補助の判定の file 名と結論だけ）。設定の scope は 1 つの同期の呼び出し（`BuildPlayer` の中の try/finally）で開閉し、復元後に保存して 2 つの file を元の bytes に書き戻して hash を記録する。build の順は base → Player → v2 → 上書き。偽 null・`Task.Delay`・`Thread.Sleep`・`record`・`#nullable enable` の違反は無い（`??`・`?.` は全て UnityEngine.Object でない値）。行数警報: `PlayerOverlayVerdict.cs`（617）は非分割妥当（1 つの判定の写像の表。§3 の判断のとおり）、`PlayerOverlayRunner.cs`（507）は非分割妥当（round の手順と操作の開始・結果の行を 1 つの orchestration に持ち、読み取りと record は §3 どおり別 file。分けると 1 つの round の手順が file をまたぐ）。構造劣化は無い。
- §6 の B 適応の記録: 全項を §4 の B 適応の範囲と照らした。範囲内である（環境変数・record の欄・上限の既定値・一時 root の名前・設定の読み書きの手段・scripting assembly の読み方は §4 が列挙する項目、`previousBuildReportDirectories` に BuildHistory の directory を渡すことは §3 の「base の build report の directory」の実装で、不成立時は §5 の u3 が B の修正先と決めている、Error に `Assert` を含めるのは答えを甘くしない側）。
- 現在の問いを阻害する findings（3 件。全て B 適応で直せ、契約・責務配置・asmdef を変えない。Phase A は再開しない）:
  - **D1（AC7・AC10。severity medium、semantic）:** 判定の写像の X1 が、主の順で参照をたどった数と列挙の数が違う時に多い方の数で読まれていない。`PlayerOverlayVerdict.cs:489` は X1 を `sceneIdsEqual`・`loadableIdsEqual`・2 つの texture の行の `entityId` の一致だけで決め、`read` の計数（`EditedTex` の reached と enumerated）を見ない。順序: read の round で reached が `[t]`、enumerated が `[t, u]`（実体 2）→ X1 の答えの行は「same」、違いとして「reached and enumerated differ」だけが足される。AC7 は「主の順で 2 つが違う時は、多い方の数で Q3 と X1 を読み」、X1 の定義は「実体 1。Editor と同じ」なので、この入力の X1 は「differs」でなければならない（スパイクの答えはどちらでも「違うが成り立つ」で変わらないが、X1 の行が定義と違う）。AC10 の「主の順で参照をたどった数と列挙の数が違う入力（多い方で読む）」の test は Q3（class の `SharedShader`）だけで、X1 の経路が無い。直し方: X1 の条件に `read` の `EditedTex` の計数の多い方が 1 であることを足し、その入力の test を足す（AC6 の Editor の対照の `ControlProblems` も同じ X1 を読むので、対照が「reached and enumerated」の違いだけで外れにならない今の扱いと矛盾しないか確かめる）。
  - **D2（§2 の誤った答えの道 (k)。severity medium、semantic）:** harness の例外で打ち切られた主の順の round で、後始末の操作が失敗すると、その失敗が「成り立たない（cannot unload or unregister）」の答えになる。順序: read の round で 2 つの登録が成功 → 観測の途中で harness の例外（例: `Entry` の `InvalidOperationException`）→ runner は `harness-exception` 行と `abortedAt` が `harness: …` の終了行を書く → `CleanupAsync` の Release か Unregister が失敗して `op-result` の `ok` が false → `FailureAnswer`（`PlayerOverlayVerdict.cs:429`）は最初の失敗の行を答えにし、`abortedAt` の harness を見ない。(k) は「harness の不備・上書きと無関係な失敗を Unity の答えとして読む」ことを禁じ、未判定の定義は「harness・fixture・環境の問題は B へ戻し」と書く。後始末の失敗は、round の定義の操作の列が harness の例外で途切れた後の物で、(A) の「操作の失敗」の読みに入れると (k) を開く。直し方: `harness-exception` 行がある round、または終了行の `abortedAt` が harness の round は、操作の失敗の行があっても未判定（harness の問題）にし、その入力の test を足す。
  - **D3（AC10。severity low、obvious）:** AC10 が列挙する「1 つ目の登録より前で切れた record」の test が無い。`RecordCutBeforeSecondRegister_IsUndecided`（`PlayerOverlayVerdictTests.cs:205`）は 1 つ目の登録の結果の行の直後で切っており、AC10 の別の入力（1 つ目の登録より前）を踏まない。今の code では同じ枝（`registers.Count < 2`）で未判定になるが、AC10 は入力ごとの確認を求める。直し方: 1 つ目の登録の `op-start` より前で切った入力（終了行なし）が未判定になる test を足す。
- 阻害しない記録（判定 C への申し送り。修正を求めない）: 判定の写像は、操作の失敗か切れ目のある主の順の round で、失敗の前に読めた Q1〜Q4・X1・X2 を答えの行に書かない（`ObserveMain` を呼ばない）。§2 の GO は「読めた物を全て書く」なので、判定 C は record から読めた所まで書く（写像の出力は補助）。
- 後続スライスへ移送する findings: なし。
- 実行したテストコマンドと `-Filter`、対象を選んだ理由: `pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.Spikes.PlayerOverlay.PlayerOverlayVerdictTests`（§5 の起点 filter。Phase B が Unity の test を一度も実行していないので、差し戻しの前に写像の test が今の head で通るかを確かめ、B の修正の基準にする）。Editor が閉じ、Unity・Pov の process が無いことを CIM で確かめ、sandbox 外で起動した。
- テスト結果: step `status=passed`、`exitCode=0`（Unity は自然終了）。XML の root `Passed`、total 44、passed 44、failed 0、skipped 0（`PlayerOverlayVerdictTests` の全 case。`TypeMissingOnly_DoesNotHoldWithTheCause` の 2 つの TestCase を含む）。実行の前後の `git status --porcelain` は空のまま。
- 判定必須のうち未実行: (2) `FirstProcess`、(3) `SecondProcess`、条件付きの (4) Editor の対照。差し戻しが決まったので実行しない（発見 C の規則）。§5 の u1〜u5 は全て未確認のまま。
- 重い検証を発見段階で限定実行した場合の理由と範囲: なし（content・Player の build と Player の起動は実行していない）。
- 機械検査: `pwsh tools/contract-audit.ps1`（errors 0、warnings 0、検査 1〜9）。
- 未確認事項: Player 上の挙動の全て（u1〜u5）。`Application.buildGUID` と build report の `summary.guid` が正規化の後に一致する形か（ValidatePlayer の照合。外れれば未判定で B へ戻る経路）。
- 巡の数: B と発見 C の差し戻しの 1 巡目を始める（上限 4 巡）。
- 担当・モデル: 判断担当と実行担当は同じ session（Claude Opus 5.5、Anthropic）。Phase B と同じモデルで、**独立性制約あり（owner 受容。B と別のモデルにするために高コストのモデルへ上げない方針）**。新規 session で、B の会話は読んでいない。

### 発見 C（2 回目、2026-11-23）

- 種別: 発見。GO 判定はしない。C' は起動しない。
- 対象: implementation base `d926e3b6b40dafab086b293e6d60784e304ecf4c`、implementation head `b101b3820f7538f9e6e8779332d1405e32b316b5`（commit で固定。完全 diff は `git diff d926e3b6 b101b382`。1 回目の対象 `16328d4d` からの差は `PlayerOverlayVerdict.cs`（+13 / −6）・`PlayerOverlayVerdictTests.cs`（+53）と本書だけ）。
- 使用した evidence bundle の取得案内・状態: 未生成（固定した commit の diff と機械検査、起点 filter の結果だけを入力にした）。
- 構造適合: 1 回目からの差は判定の写像とその test の 2 file で、§3 の責務・asmdef・製品の file・§2 の語は変わらない。1 回目の構造の確認（変更 file の集合、2 つの asmdef、§4 の禁止、偽 null・`Task.Delay`・`Thread.Sleep`・`record`・`#nullable enable`）はこの差に当たる箇所が無く、そのまま有効である。`PlayerOverlayVerdict.cs` は 624 行で、1 回目の非分割の判断（1 つの判定の写像の表）が当たる。
- D1〜D3 の確認（全て B 適応の範囲で直っている。§4 の Phase A へ差し戻す条件に当たらない）:
  - **D1:** read の round の X1 が、`EditedTex` の計数の多い方（`Count` は reached（`null` を除く）と enumerated の集合の大きい方の数）が 1 であることを条件に持つ。runner は read の round で 2 つの load の後に必ずこの `count` 行を書く（`PlayerOverlayRunner.cs` の `ReadAsync`）ので、行が無い時の未判定は (q) を開かない（load の失敗は `FailureAnswer` が先に受ける）。Editor の対照は同じ `ObserveMain` を通り、「reached and enumerated」の違いだけを外れから除き、「X1 differs」は外れに残るので、多い方で実体 2 なら再現しない。AC7 の「多い方の数で Q3 と X1 を読む」と一致する。
  - **D2:** `FailureAnswer` の先頭で、`harness-exception` 行がある round と、終了行の `abortedAt` が `harness:` で始まる round を未判定にする。runner は `OperationFailed` 以外の例外だけを `harness:` と `harness-exception` 行にし（`PlayerOverlayRunner.cs` の round の `catch`）、`abortedAt` の既定値は空文字（`PovLine`）なので `StartsWith` は null で落ちない。後始末の失敗も record の切れ目も、この枝で未判定になり、(k) の後半（harness の不備を Unity の答えに読む）を閉じる。`Judge` の「操作の失敗の無い打ち切り」の理由を重ねない変更は、理由の数で round を外す既存の流れを変えない。
  - **D3:** `RecordCutBeforeFirstRegister_IsUndecided` が 1 つ目の登録の `op-start` から後を落とした入力を未判定と確かめる。
- 現在の問いを阻害する findings: なし。
- 阻害しない記録（判定 C への申し送り。修正を求めない）: 1 回目の申し送り（操作の失敗か切れ目のある主の順の round では、写像が失敗の前に読めた Q1〜Q4・X1・X2 を書かないので、判定 C は record から読めた所まで書く）はそのまま有効。D2 で、harness の例外で打ち切った round は主の順でも未判定になるので、判定 C は該当の round があれば (k) の前半（Unity の失敗を harness の失敗として捨てる）に当たらないかを、`harness-exception` 行の例外の型と文面で確かめる（runner が Unity の API の失敗を `OperationFailed` に包まずに投げた場合は harness の不備で、B へ戻す）。
- 後続スライスへ移送する findings: なし。
- 実行したテストコマンドと `-Filter`、対象を選んだ理由: `pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.Spikes.PlayerOverlay.PlayerOverlayVerdictTests`（§5 の起点 filter。修正は判定の写像とその test だけなので、この filter で修正箇所と影響する既存の経路を覆う）。実行の前に Unity Editor と Pov の process が無いことを CIM で確かめ、sandbox 外で起動した。
- テスト結果: step `status=passed`、`exitCode=0`。XML の root `Passed`、total 48、passed 48、failed 0、skipped 0（1 回目の 44 に、足した 4 つの test が加わり全て Passed）。実行の前後の `git status --porcelain` は空のまま、実行の後に Unity・Pov の process は無い。
- 判定必須のうち未実行: (2) `FirstProcess`、(3) `SecondProcess`、条件付きの (4)。阻害する findings が無く、implementation head が GO 候補になったので、判定必須の一式は判定 C が GO 候補 head で実行する（発見 C で実行すると、判定 C が同じ head で取り直す重い検証を二重にするため）。§5 の u1〜u5 は全て未確認のまま判定 C へ渡す。
- 重い検証を発見段階で限定実行した場合の理由と範囲: なし。
- 機械検査: `pwsh tools/contract-audit.ps1`（errors 0、warnings 0、検査 1〜9）。
- 未確認事項: Player 上の挙動の全て（u1〜u5）。`Application.buildGUID` と build report の `summary.guid` の照合の形（1 回目の記録のとおり）。
- 巡の数: 差し戻し 1 巡目が収束した（上限 4 巡）。
- 担当・モデル: 判断担当と実行担当は同じ session（Claude Opus 5.5、Anthropic）。Phase B と同じモデルで、**独立性制約あり（owner 受容）**。新規 session で、B と発見 C の 1 回目の会話は読んでいない。

### 判定 C（2026-11-23）

- 種別: 判定 C。結論は **GO**。スパイクの答えは **「同じ」**（主の順の 3 つの round の 3 つの process の全てで Q1 強・Q2 正しい・Q3 一致・Q4 無傷、X1・X2・X3 が Editor と同じ、process の間の観測が一致）。未判定の条件（§2）に当たる観測は無い。高コスト証拠は無いので、次の C' が最終 C' である。
- 対象: implementation base `d926e3b6b40dafab086b293e6d60784e304ecf4c`、implementation head `b101b3820f7538f9e6e8779332d1405e32b316b5`。実行した checkout の HEAD は引き継いだ `2ecd8fffeb9b7a185493bd38e614eec80d25d754` で、head との差は本書だけ（`git diff --stat` で確認）。record の `head` 欄は `2ecd8fff` で、第 1 process と 18 個の Player の record で同じ（AC8 の照合は第 1 process の record との一致）。
- evidence bundle: 判定用と C' blind bundle を同じ head・同じ生結果から作った（§0 の欄）。判定用は 21 file（目次の README、完全 diff・stat・name-status、凍結版 `ae1e1614` と引継ぎの `2ecd8fff` の本書、head の変更 file の zip、`contract-audit`、(1)〜(3) の XML・`step.json`・Unity の log、一時 root `TestResults/pov/1123003048/` の record・Player の log・layout の写し・facts・launcher の file・補助の判定の zip、executor-note、判定 C の読み取り script とその出力）。C' blind bundle は 19 file（目次の README、凍結版、Phase B の出口 `16328d4d` の §6、本書を除いた diff と変更 file の zip、`contract-audit`、(1)〜(3) の生結果、同じ zip、executor-note）。build の出力（`base`・`ovl`・`ply`）は入れていない（棚卸しの名前・size・sha256 は第 1 process の record にある）。
- 構造適合: 発見 C の 2 回目と同じ diff（41 file。本書・program・§3 の新規 file と `.meta`）を自分でも照合した。製品の file と §3 以外の asmdef の変更は無い。probe の asmdef は参照・define 制約・platform の指定なし・`autoReferenced` false、runner の asmdef は参照が `OneStarMaker.Runtime` と `UniTask` だけ。spike の `.cs` の全てが先頭に `#nullable enable` を持ち、`[Preserve]`・`link.xml`・probe の型の `typeof`・型引数・`[RuntimeInitializeOnLoadMethod]`・`Task.Delay`・`Thread.Sleep` はコメントの中にしか無い（§4 の禁止）。`pwsh tools/contract-audit.ps1` は errors 0、warnings 0、検査 1〜9。
- 実行した判定必須テスト（§5。それぞれ別の Unity process、sandbox 外、初期状態は Editor・`Unity.exe`・`Pov.exe` が無く作業ツリー clean。各 process の後の `git status --porcelain` は全て空）:
  - (1) `pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.Spikes.PlayerOverlay.PlayerOverlayVerdictTests`: XML の root `Passed`、total 48、passed 48、failed 0。`step.json` `passed`・exit 0、自然終了。
  - (2) `-Filter …PlayerOverlayBuilds.FirstProcess`: XML の root `Passed`、total 1、passed 1、failed 0（test の所要 453.7 秒。Player の build は 343.6 秒）。**`step.json` は `status=failed`**（`failure` は「C# コンパイルエラーを検出しました」）。原因は test の後に Unity の中で走った Project Auditor package の post-build 監査（`ProjectAuditor:DelayedPostBuildAudit`）で、`Library/BeeAssemblyBuilder` が assembly ごとに `-langversion:9.0` で compile し直して `SampleGame.InGame`・`SampleGame.OutGame` で `error CS8773` を出し、`run-tests.ps1` がその行を拾った。spike の Player の build の結果は `Succeeded`・`totalErrors` 0 で、監査は XML の end-time（00:38:21Z）の後に始まり、XML の書き出しを 00:44:57Z まで遅らせた。§5 のゲートは XML（total 1 以上、failed 0、root `Passed`）なので、判定に影響しない。**終了 stall:** XML の完成後 5 分を過ぎても Unity（PID 41180）が残り、thread 1 本で UserModeTime が 15 秒に約 14.9 秒増え続けた（Harness README の既知の native stall と同じ形）。00:50:32Z に `Invoke-CimMethod -MethodName Terminate` で止め（ReturnValue 0）、CIM で消えたことを確かめた。数値は executor-note。強制終了の後も `git status --porcelain` は空（AC9 の (2)）。
  - (3) `-Filter …PlayerOverlayLaunch.SecondProcess`: XML の root `Passed`、total 1、passed 1、failed 0。`step.json` `passed`・exit 0、自然終了。18 回の Player の起動は全て自分で終了（exit 0、各 2.7〜3.1 秒、launcher の停止なし）。launcher の file の 18 個の pid は、実行の後に CIM で全て存在しない。
  - (4) Editor の対照: 答えが「同じ」なので実行しない（AC6）。
- 読み方: §5 のとおり、判定 C が record の行を AC ごとに読んだ（照合 script の方式は「なし」）。読むために offline の読み取り script を書き、record の複製に当てた（判定用 bundle に script と出力を入れた。期待の列・値・pixel の sha256 は `PlayerOverlayExpected.cs` の fixture の定義から写し、pixel の sha256 は定義の色から計算した）。harness の補助の判定 `verdict-second.json`（`Outcome` GO、`Spike` same、`Reasons` 空）は、読み終えた後に突き合わせ、全ての項目で一致した。

**受け入れ条件:**

| AC | 結果 | 根拠（record の行） |
|---|---|---|
| AC1 | 合格 | 第 1 process の record: Active Build Target `StandaloneWindows64`、build の前の `Library` の履歴に spike の build なし。順は base（`Succeeded`）→ Player（`Succeeded`、IL2CPP、stripping High、Addressables `DoNotBuildWithPlayer`、Scene は `PovBoot` だけ、`previousBuildReportDirectories` は base の BuildHistory の 1 つ、`extraScriptingDefines` 空、build report の GUID `ff151441…`）→ v2 → 上書き（`Succeeded`）。棚卸しの digest 3 つ、layout の写し 2 つ、scripting assembly に probe の assembly あり、Player に `StreamingAssets/aa` なし、packed assets に fixture の asset なし（`packedFixtureAssets` 空）。P1: MonoScript の file は両方で id `a2a42d71…cfid`、sha256 が違い（`4a747de3…` / `2920274a…`）、base の `SourceAssets` は `ProbeBaseOnly`・`ProbeBoth`、上書きは `ProbeOverlayOnly`・`ProbeBoth`。P2: `SceneEdit`（`a6ab1e14…` / `ded39a4f…`）と `EditedTex`（`3978ff8d…` / `8917596b…`）は id が同じで sha256 が違う。P3: `SharedTex`・`SharedMat`・`SharedShader`・`EditedMat` は名前と sha256 が同じ。`preconditions` は `hold` true。fixture の folder は消えた（`fixtureFolderExists` false） |
| AC2 | 合格 | base-alone・base-lifetime・overlay-alone の 9 process の全てで、終了行（`completed` true、`abortedAt` 空）、操作は全て成功、round の段の log は 0 行。base-alone: `SceneKeep`・`SceneEdit` は `Marker_v1`、`EditedTex` の pixel は v1、`SceneKeep`・`SceneEdit`・`SharedPrefab` の component の列は期待の列と長さ・位置・class・`_tag`・`_value` が一致し `null` なし、7 つの物の数は全て 1。base-lifetime: (1) の数は全て 1、(3) の `SceneEdit` は `Marker_v1`、保持した物は全ての確認点で load 直後の行と同じ、(6) の後の登録は 0 件。overlay-alone: `SceneEdit` は `Marker_v2` と v2 の probe の列、`EditedTex` の pixel は v2。3 つの process の観測は一致（正規化した record の差は `FindObjectsOfTypeAll` の列挙の順だけ） |
| AC3 | 合格（Q1 強、X1 同じ） | read の 3 process: 2 つの root の `SceneEdit` の id が等しく（`1c8f9e65…`）、`EditedTex` の `LoadableObjectId` が等しい。base の root の id から load した `SceneEdit` も上書きの root の id から load した物も `Marker_v2`。`EditedTex` は 2 つの root から load して同じ `EntityId`、pixel は v2、参照をたどった集合と列挙の集合はどちらも 1 個 |
| AC4 | 合格（Q2 正しい、Q3 一致、X2 同じ） | class の 3 process: `SceneKeep`（`ProbeBaseOnly` 101・`ProbeBoth` 102）、上書き版の `SceneEdit`（`ProbeOverlayOnly` 301・`ProbeBoth` 302）、base の root の `SharedPrefab`（`ProbeBaseOnly` 401・`ProbeBoth` 402）の列が期待どおりで Missing 0。数は `SharedMat`・`SharedTex`・`SharedShader`・`EditedMat`・`EditedTex` が 2、`SharedPrefab`・`KeepTex` が 1 で、参照をたどった集合と列挙の集合は全て一致。`SceneKeep` の Renderer が直接参照する `EditedMat` の `mainTexture` は v1 |
| AC5 | 合格（Q4 無傷） | lifetime の 3 process: (1) の数は AC4 と同じ（2・2・2・2・2・1・1、集合は一致）。(1)〜(5) の全ての確認点で、保持した物（`SceneEdit`・`SceneKeep`・`SharedPrefab`・`EditedTex`。load し直した Scene はその load 直後の行と比べる）の物の id・material・texture の id と pixel・component の列が load 直後の行と同じ。(3) で load し直した `SceneEdit` は `Marker_v2`。(6) の Release・unload・上書き → base の解除は成功し、登録は 0 件。(6) の段の log も 0 行 |
| AC6 | 対象外 | 答えが「同じ」 |
| AC7 | 合格 | base-alone の計数と base-lifetime の (1) の計数で、参照をたどった `EntityId` の集合と列挙の集合が全ての物で一致 |
| AC8 | 合格 | 18 個の record の先頭が `isEditor` false、`platform` `WindowsPlayer`、`backend` `IL2CPP`、`unityVersion` 6000.6.9f1（`ProjectVersion.txt` と同じ）、実行 id `1123003048`・head・`buildGuid`（`ff151441…`）が第 1 process の record と同じ、pid が launcher の起動の行と同じ、`graphics` は `Direct3D11`。P0: 18 個の Player の log の起動の段に製品の `AfterSceneLoad` の 1 行があり、record の `p0` は登録中の directory 0、Scene は `PovBoot` だけ、fixture の名前の物 0。P4 の 3 つの値がある。launcher の file の行の種類は `second-start`・`launch`・`exit`・`pids-after`・`verdict` だけで、観測の行は無い。第 2 process は第 1 process の出力を実行 id・head・3 つの digest で照合した（`inputMatched` true） |
| AC9 | 合格 | 変更 file は本書・program・§3 の新規 file と `.meta` だけ。`contract-audit` 合格。(1) 第 1 process の record: 変える前と復元の後の値が同じ（Mono2x・stripping Disabled・`PreferencesValue`）、`ProjectSettings.asset`（`b52d2d16…`）と Addressables の設定の asset（`f065d1d0…`）の hash が前後で同じ、fixture の folder と `Assets/OneStarMakerGenerated` が無い。(2) 3 つの Unity process の後（(2) は強制終了の後）の `git status --porcelain` は全て空、Player の pid は全て終了 |
| AC10 | 合格 | (1) の 48 件が全て Passed（発見 C の 2 回目が同じ test の集合を AC10 の入力と照合済み） |

**§5 の未知の操作経路（初回確認）:**

- (u1) 成立。EditMode の test から `BuildPipeline.BuildPlayer`（IL2CPP）を `run-tests.ps1` の batch で完走した（343.6 秒）。
- (u2) 成立。製品の起動は 18 個とも `BeforeSceneLoad` で止まり、`AfterSceneLoad` を飛ばした。止まった所は code の読み（`content:runtimeMode` が無い）より手前で、設定の JSON の Addressables 経由の同期 load だった（Player に Addressables の data が無く、`RuntimeData is null`・`Unable to load runtime data` の Error の後、`ResourceManager+CompletedOperation<TextAsset>` の既定の constructor が無いことによる `MissingMethodException`）。起動の段の log は 18 個とも同じ 5 件（`LogError` 4、`LogException` 1。開始の印より前なので数えない）。
- (u3) 成立。P4 で probe の 3 型が全て Player の assembly にあり、base だけの round の class は正しい。
- (u4) 成立。graphics の device は `Direct3D11`。単独の基準の版・class・pixel・数と列挙の照合が fixture の定義と一致した。
- (u5) 成立。上書きの棚卸しで P2・P3 が成り立ち、AC9 の 2 つの時点で設定の残りが無い。

**答えと観測（実測した事実）:**

- Q1 強、Q2 正しい、Q3 一致（AC4 と AC5 の (1) の両方で Editor の実測と同じ数）、Q4 無傷。X1・X2 は Editor と同じ。X3 は主の順の 9 process の全ての段で Unity の Error・Exception が 0 件（record の log の行は 0 行。Player の log の開始の印より後にも Error・Exception の stack を持つ行は無い）。
- process の間の一致: 6 つの round の全てで、3 つの process の観測の行が一致した。正規化した record（時刻・pid・連番を除き、`EntityId` を初出の順の記号に置き換えた物）の差は、class の round の `EditedMat` と lifetime の round の `SharedTex` の `enumerated`（`FindObjectsOfTypeAll` の列挙）の並びの順だけで、集合と数は同じ。
- P4: `ProbeOverlayOnly` も Player にあった（`OneStarMaker.Spikes.PlayerOverlayProbes`）。§2 の後続の表の最後の行の事実（base の build report を渡して build した Player に、base が使う型と同じ assembly にある、base が使わない `MonoBehaviour` が残った）で、別の assembly・組み込みの型へ広げない。
- 添えるもの（判定に使わない）: (a) Player の log 18 個のうち 5 個（base-alone#1、class#1、overlay-alone#1・#3、read#2）で、開始の印より後、終了処理の `[Physics::Module] Cleanup current backend.` の直前の行に `Curl error 42: Callback aborted` の 1 行がある。stack trace を持たない native の行で、runner の log の捕捉（`logMessageReceivedThreaded`）には届いておらず（record の log の行は 0）、base だけの基準の round と主の順の round の両方に出ている。X3 の数える対象（Unity の Error・Exception の log）に当たらないと読んだ。(b) Warning は record に 0 件。
- 範囲: 結論は §2 の「成果の範囲」の条件（この PC、6000.6.9f1、StandaloneWindows64、IL2CPP・High、development build でない、`-batchmode`・`Direct3D11`、loose file、合成 fixture、同じ `Library`、base の build report を渡して Player を build、上書きはその後、製品の検証を通さない base、製品の起動は失敗して常駐の service なし）に限る。仕組みの推測（なぜ base の root の id からも上書き版が出るか等）はしない。
- 後続への含意（§2 の表で当たる行）: 「同じ」→ S4-2 の A1 を、この範囲の実測を前提に始められる。13 章の禁止は S4-2 が製品の登録の口と検証済みの base で確かめてから解く。「P4 で `ProbeOverlayOnly` が Player にある」→ 上の事実。S4-2 の開始条件（§2.4）のうち、S4-0 の側（答えと process の間の一致）は満たした。

**指摘:**

- 現在の問いを阻害する findings: なし。
- 後続スライスの入力（現スライスを拡張しない。凍結済み条件・常時契約への違反根拠は無い）:
  - **J1（S4-1 の入力）:** Addressables の data の無い IL2CPP・stripping High の Player で、製品の起動の設定の読み込み（`JsonFileConfigProvider` → `AddressableBackend.LoadAssetSync`）が、data の欠落の失敗を返す途中で `ResourceManager+CompletedOperation<TextAsset>` の既定の constructor が strip されていて `MissingMethodException` になった。本スライスでは P0 の側の事実だが、S4-1（content directory から起動して操作を続けられる Player の構成）が Addressables の無い Player の起動を設計する時に、strip による失敗の形として当たりうる。
  - **J2（harness の運用の入力。置き場所は進行役・owner が決める）:** test の中で Player を build すると、Project Auditor package の post-build 監査が同じ Unity の中で走り、XML の書き出しを約 6 分遅らせ、`run-tests.ps1` の `step.json` を `failed`（compile error の検出）にする。製品の BS4 の Player の build を test から行う後続（S4-2 の gate の測り直し等）でも同じ形になる見込みで、ゲートを XML で書くか、監査の扱いを決める必要がある。
- 巡の数: 判定 C で B へ戻していない（B と発見 C の差し戻しは 1 巡で収束したまま）。
- 担当・モデル: 判断担当と実行担当は同じ session（Claude Opus 5.5、Anthropic）。Phase B と同じ系列で、**独立性制約あり（owner 受容）**。新規 session で、B と発見 C の会話は読んでいない。

## 8. Phase C'

未着手

## 9. Phase D

未着手
