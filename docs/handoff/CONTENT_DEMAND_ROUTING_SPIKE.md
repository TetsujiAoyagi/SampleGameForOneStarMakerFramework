# S0-b Editor snapshot と built directory の混成の技術実証（CONTENT_DEMAND_ROUTING_SPIKE）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。

- type: `slice`（スパイク）
- status: `C`（発見 C の 2 巡目待ち、2026-11-08。発見 C の 1 回目の差し戻し（§7.1 の C-01〜C-06）を B が修正した（§6.7）。B と発見 C の往復の 1 巡目。前の B 完了の head は `9a606dac`。A2 入力版は commit `e5b3ca50`、A3 凍結版は commit `3dd43790`）
- branch: `claude/play-content-s0b-spike`（PR の base は `develop`）
- implementation base commit: `e0d020d7`（`develop` の head、2026-11-08。A0 の実測 base `e022934` から `unity/` 以下の差分は無い）
- implementation head commit: `c45a37b8`
- risk: `high`（Editor Play の Scene 所有・寿命、Content Directory の native 登録、Enter Play Mode の設定に跨る初回実機の実証）
- owner: root（依頼者）
- created: 2026-11-08
- expires: 2026-12-31、または program [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md) の改訂でこのスライスが置き換わった時点
- harvest to: program §5.2・§7 D4・§10 U1 (2)・U2・U3（結論と採れない方式）、`unity/Assets/Docs/Architecture/20-variant-checkout-workflow.md` §1（旧 Hybrid Play の回顧の要約）。恒久の公開面へは program の完了時に program 経由で移す。実験 code は harvest しない
- 関連: program §2.4・§3・§5.2・§6・§8 S0-b 行・§9・§10（**本スライスの A0**。§0 の停止規則を本書 §2 に転記した）、[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)（所有境界の正本）、[RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md](RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md)（Phase B を同時に走らせない）
- Phase A snapshot の取得案内・状態: 該当なし（機械向けの期待値・script・fixture を持たない。§5「判定 C の照合 script の方式」。C / C' が読む凍結版は A3 凍結版 commit の本書）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: 発見 C（2 巡目。B と発見 C の往復の 1 巡目の再レビュー）。動作確認は対象なし（§5 の操作・実行時の条件は全て Unity テストと Content Directory build を伴い、Phase C の実行担当が持つ）。
  - 次の作業: §6.7 の修正が §7.1 の C-01〜C-06 を解消したかを、implementation head の差分（前の head `9a606dac` から）で確かめる。2 巡目は前巡の指摘の解消と修正差分を中心に見る。C-01 の record の順序と C-02 の DR 有効の設定で round が 2 件以上進むことは、Unity テストの実行で初めて観測できる（B は未実行）。差し戻し中に限定実行するなら §5 の起点 filter で理由と範囲を記録する。
  - 引き継ぐ commit: この票を含む commit（SHA は起動指示で渡す）。implementation head は §0 の欄の値で、この commit との差分は本書だけ。
  - 必要な権限: 差し戻しの判断だけなら無し。Unity テストを限定実行するなら Unity（Editor が閉じていること）と sandbox 外の承認済み経路（SKILL の Phase C）。
  - 停止規則: §2 の停止規則、§4「Phase B から Phase A へ差し戻す条件」、§5 に従う。`RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` の Phase B と同時に走らせない。製品 code・SampleGame・asmdef を変えない。B と発見 C の往復は 4 巡まで（SKILL。この修正で 1 巡を使った）。
  - 引き渡さないもの: §7 の発見 C の所見を Phase B result・C' 用の入力へ転載しない（C' 完了まで隔離）。

### 実測の方法

- 名前付き参照と行数は `grep` と `wc -l`（base `e0d020d7`）。program §2 の事実のうち本スライスに効く物（§1「現況」）は読み直して確認した。
- 動作の確認は行っていない。Unity 上の挙動は全て Phase B のコンパイル確認と Phase C の実行で初めて観測する。
- `tools/docs-audit.ps1` は A2 入力版と A3 凍結版の commit 前に実行した（errors 0）。

## 1. 目的と対象外

- 目的: Editor で開いている Scene を Play 前に退避した複製（snapshot）と、built Content Directory の Scene を、同じ Editor Play の session で扱ったとき、ロード・unload・cancel・再試行・Play 停止・Enter Play Mode の設定のどれでも、各 Scene の由来と所有者が一意に保たれ、開いている元の Scene が壊れないかを、SampleGame を使わない小さな fixture の実 Editor 上の実行の証拠で判定する。複製の機構の候補 2 つ（(i) 複製を Addressables の封じより前に既ロード Scene として渡す、(ii) 複製から一時 Content Directory を build して登録する）のそれぞれについて成立・限定付きで成立・不成立を決め、どちらも成立も限定付きの成立もしなければ program の決定 D4 の修正を owner に返す。製品 code は変えない。
- 利用者: (1) program の owner。結果を見て D4（Editor snapshot tier）を維持するか修正するかを決める。(2) 後続スライス（S0-a の directory 基数、S1、S2、S3、S0-c）の A1 担当。結果を機構の選択と、Editor Play の設定に課す制約の入力に使う。program の利用者 (a) 編集する開発者は最終の受益者で、本スライスでは何も操作しない。
- 目的の改訂記録（A3、2026-11-08）: 目的レビュー（A2）の指摘を受けて 2 か所を改めた。元の文言「実機の証拠で判定する」→「実 Editor 上の実行の証拠で判定する」（Player 端末と誤読されないため。P-02）、「どちらも成立しなければ」→「どちらも成立も限定付きの成立もしなければ」（判定定義と揃えるため。P-01）。owner（root）が 2026-11-08 に採用を承認し、未関与の担当で目的レビューを 1 回やり直した（§5）。利用者の欄は変えていない。
- 対象外:
  - 製品 code（`unity/Assets/OneStarMaker/Scripts/`、`unity/Assets/SampleGame/`）の変更。resolver・`ContentRequest`・LoadRecord の製品型（S0-a、S3）。`InstallContentDirectory` の 2 件目拒否の変更（program §10 U1 (2)。S0-b の結論の直後に切るスライス）。
  - Play ボタンの割り込み、`ExitingEditMode` の transaction、通知、RunPurpose（S0-c）。本スライスは snapshot を作る契機を実験 harness の明示呼び出しに置き、Play 入口の設計をしない。
  - 互換判定（S1）、package の索引と異なる build の合成（S2）、LAN・cache の供給元（S4）。directory は本スライスの fixture から build した 1 revision だけを使う。
  - 参照先 asset（prefab / material / ScriptableObject）の overlay。I11 の対象は開いている Scene 資産に限る（program §6 I11、R2 M-05）。参照先の変更は「snapshot の hash が参照先の一致を保証しない」ことの観測だけに使う（G22）。
  - SampleGame の Scene、SceneResourceMap、宣言（program §8「SampleGame を使わない独立した 2 Scene fixture」）。
  - Player での挙動（snapshot tier は Editor 専用。program §4.1）。
  - 性能の合格判定。(ii) の build 時間は比較の材料として計測するだけで、閾値を置かない。
- 現況（base `e0d020d7`。本スライスの主張を崩しうる既存の経路を含む）:
  - **起動は Editor で開いている Scene を名前で取り込む。** `AbstractApplicationInitializer.RegisterAlreadyLoadedScenes`（`unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs` 640 行目）は `SceneManager` のロード済み Scene のうち `SceneResourceMap` に名前がある物を `SceneDirector.AddScene(scene.name)` で登録する。`SceneDirector.PerformUnitySceneLoad`（`SceneSystem/SceneDirector.Loading.cs` 629 行目）は `SceneManager.GetSceneByName` で同名のロード済み Scene があれば再ロードせず `BackendSceneLoaded=false` で返し、unload は `SceneManager.UnloadSceneAsync` に回る。**由来（Editor か directory か）の区別は無く、名前だけが鍵である。** directory mode でもこの経路は通る（呼び出しは `InstallConfiguredContentDirectoryAsync` の後、398〜402 行目）。
  - **Content Directory は 1 件だけ。** `AssetManagement.InstallContentDirectory`（`AssetManagement/AssetManagement.ContentDirectory.cs` 20〜25 行目）は 2 件目を `InvalidOperationException` で拒否する。`internal` で、テスト asmdef は friend access で触れる。Play 停止は `CompleteContentDirectoryPlayStop`（同 52〜58 行目）が `Application.quitting` の同期 `ReleaseAll` から呼ばれ、native の unload を待たない（待つと死鎖しうる、とコメントにある）。`UnityContentDirectoryBackend` は `ContentLoadManager.RegisterContentDirectory` / `UnregisterContentDirectory` を 1 handle で包む（`BuildContent/UnityContentDirectoryBackend.cs` 31〜33 行目）。
  - **Addressables の封じ。** directory と決まった直後に `SealAddressables` が Addressables の取得経路を 1 回だけ封じる（program §2.2）。封じの後に AssetDatabase / Addressables で複製を読む案は比較に入れない（program §5.2 (i)、§10 U2）。
  - **Enter Play Mode の設定。** `unity/ProjectSettings/EditorSettings.asset` は `m_EnterPlayModeOptionsEnabled: 1`、`m_EnterPlayModeOptions: 1`（Domain Reload を無効、Scene Reload は有効）。**このリポジトリの既定は Domain Reload 無効**で、static の状態が Play を跨いで残る。`TestObservationBootstrap`（`Scripts/Editor/TestObservation/TestObservationBootstrap.cs` 131〜194 行目）が設定を退避・変更・復元する前例を持つ。
  - **現行の保護。** `VerifyPlayLauncher.CheckBeforeStart` / `FindReusableScenes`（`SampleGame/DependOnAll/Editor/Build/VerifyPlayLauncher.cs` 246〜278 行目）は、起動が名前で再利用しうる Scene が開いていれば拒否するか、保存を確認して空 Scene に替える。混成の衝突を「開いている Scene を閉じる」で避けている（program §2.4）。
  - **Build の fixture 経路。** `BuildContentRequest`（`Scripts/Editor/Build/Content/BuildContentReport.cs` 25 行目〜）は `declaration` を null にした「合成だけの build（fixture）」を受け付ける。publisher（`ContentTransportPublisher`）は `Full` build だけを受け付けるが、本スライスは publish を使わず build 済み directory を直接登録する。Editor Play で built content を読むには type tree 付きで build する必要がある（program §2.5、Unity 6.6 文書）。現 adapter の type tree 設定は未確認（§2 不変条件 N3 の未確認の箇所）。
  - **旧 Hybrid Play。** §2.1 に回顧を置く。
- UC ごとの利用者の入力: `なし`。本スライスに利用者の操作は無い。実験の操作（fixture の生成、snapshot の作成、Play の開始と停止、設定の切替）は Phase B が書く harness と Phase C の実行担当が行う検証操作で、利用者の入力ではない。実証された機構を利用者の入力に結び付ける設計は S0-c が持つ。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: **Editor で開いている Scene の Play 前の複製（snapshot）と built Content Directory の Scene を、同一 Editor Play session で、由来と所有者を一意に保ち、元の Scene を壊さずに扱える機構は、(i) / (ii) のどちらで成立するか（どちらも成立しないか）。**
- 用語（program §3.1 の「混ぜてはいけないもの」に従う）:
  - **snapshot:** Play 前に、開いている Scene の現在の内容（Dirty を含む）を別の asset として書き出した複製。`Assets/` に source file があるだけの Scene は snapshot ではない（program §3.1 の 1）。
  - **元の Scene:** 利用者が Editor で開いている Scene。I11 の対象は、その Scene 資産のシリアライズ内容（disk 上の `.unity` の bytes）と Editor 上の Dirty の状態、未保存の内容。
  - **論理 Scene identity:** 要求に使う名前（fixture では `Entry`、`Next`）。複製は別の path / GUID を持ちうるので、物理 asset とは別に扱う。
  - **由来:** 1 回のロードの実体が snapshot、directory、Unity 標準の Play が開いた Scene のどれかを示す値。
  - **由来マーカー:** fixture の各 Scene に置く実験用 component の値。snapshot を作る時に snapshot 側へ、directory を build する時に build 側へ、それぞれ別の値を書く。名前・path は由来の証拠にせず、実行中の Scene の由来はこの値で確かめる（A2 代替構成）。
  - **所有:** 3 つの層に分けて record で観測する（R4 F-03）。(a) **論理 Scene の所有:** SceneDirector の SceneBase の lifecycle を、どの論理 identity の Scene が進めているか。(b) **物理 Scene の unload 責任:** その Unity の Scene を誰が unload するか（AssetManagement 経由の backend unload か `SceneManager.UnloadSceneAsync` か）。(c) **AssetManagement の所有:** backend の token と scene 台帳の entry。現行の `PerformUnitySceneLoad` は既ロード Scene を見つけると `BackendSceneLoaded=false` を返して AssetManagement を通さず、unload は `SceneManager.UnloadSceneAsync` に回る（`SceneDirector.Loading.cs` 629 行目〜、`SceneDirector.Unloading.cs` 451 行目〜）。
  - **所有の成立水準（判定の前提）:** directory 由来の Scene は (a)(b)(c) が全てちょうど 1 つ。既ロードの snapshot（機構 (i)）は (a)(b) がちょうど 1 つで足り、(c) は必須にしない（現行の既ロード経路が (c) を持たないため。(c) の有無は record に残し、製品で必要かは S3 が決める）。どの由来でも、二重の unload、誰も unload しない残留、元の Scene への誤った unload は FAIL。
  - **同一 Editor Play session:** 1 回の Play の開始から停止まで。snapshot の Scene と directory の Scene が**同時に loaded な時間を必ず含む**（E1）。順にロードするだけの観測で同時の場合を代用しない。
  - **封じ:** directory mode の起動で、config の確定直後に `SealAddressables` が Addressables の取得経路を閉じること（program §2.2）。
  - **cell:** 判定の最小単位。機構（(i) / (ii)）× Enter Play Mode の 4 通り × シナリオ（受け入れ条件の各行）。値は PASS / FAIL / UNTESTED / N/A。
  - **実験の record:** harness が 1 回のロードごとに残す観測（論理 identity、由来、由来マーカーの値、物理 path / GUID、Dirty、snapshot の bytes の SHA-256、所有者、unload の経路、時刻順）と、run ごとの Unity version・Enter Play Mode の設定。S0-a の LoadRecord の製品型ではない。
- 進める最低条件:
  - 不変条件（起きてはいけないこと）:
    - **N1（I11）元の Scene は Play の前後で変わらない。** Play 停止後、元の Scene の disk 上の bytes と mtime、Dirty の状態、未保存の内容（Hierarchy の GameObject 名・数と Inspector の値の固定集合）、path / GUID、Editor の Scene setup（開いている Scene の集合と順序、active Scene）が Play 前と同じ。正常な停止だけでなく、取消・例外の途中での停止の後も同じ。Play 中の unload・変更は snapshot にだけ及ぶ。Undo の履歴は対象外。
      - 主張を崩しうる既存の経路: (a) `RegisterAlreadyLoadedScenes` と `PerformUnitySceneLoad` が名前で元の Scene の Play 中の実体を取り込み、`SceneManager.UnloadSceneAsync` で unload する経路。(b) Unity の Play 終了時の Scene 復元（Scene Reload の有無で挙動が変わりうる）。(c) `EditorSceneManager.SaveScene(scene, path, saveAsCopy: true)` が元の Scene の Dirty や path を変える可能性。(d) 複製の作成が AssetDatabase の import・refresh を起こし、元の Scene の再読込を促す可能性。(e) `VerifyPlayLauncher` 型の「空 Scene に替える」保護が元の構成を戻さない経路（本スライスの harness はこれを使わない）。(f) harness が通常の `SaveScene`（copy でない保存）を呼び、Dirty が消え disk が変わる経路。(g) Play 前に snapshot を Editor で開き、Play 終了時に Unity が「開いていた setup」として snapshot 側を復元する経路。(h) Editor の自動保存。(i) snapshot の作成が `.meta` まで写して GUID が元と重複する経路（N2 にも効く）。
      - 探索した境界: 上記の Runtime / SampleGame Editor の名前付き経路と `EditorSettings.asset`。
      - 未確認の箇所: Unity が Play 前に開いている Scene を退避・復元する内部実装（Temp の backup の扱い）、Scene Reload 無効時の復元、prefab override と新規未保存 Scene（path が空）の扱い。全て実験 (1)(2)(5) で観測する。
      - 検出器の妥当性: N1 の比較が壊れを検出できることを、負の対照（AC0）で先に示す。示せない検出器の PASS は数えない（A2 代替構成の反例「Unity 標準の隔離に手柄を取られる」への対策）。
    - **N2（I4、G21、G12）1 つの論理 Scene identity は同時に 1 つの実体だけが実行され、その実体の所有（用語の (a)(b)(c)）が「所有の成立水準」を満たす。** 同 identity の snapshot と directory の Scene がある時、実行されるのは snapshot の 1 個だけ。Unity 標準の Play が開いた元の Scene の実体と snapshot が同時に存在しない。
      - 主張を崩しうる既存の経路: (a) `PerformUnitySceneLoad` の `GetSceneByName`（複製の file 名が元と同じなら名前が衝突し、元と複製のどちらを返すかが Scene の並び順に依存する。違う名前にすると論理 identity から外れる）。(b) `RegisterAlreadyLoadedScenes` が由来を見ずに登録する経路。(c) Unity の `ContentLoadManager` の横断探索は後から登録した directory が先になり、同じ asset を別 directory に入れると重複ロードされうる（program §2.4）。(d) `AssetManagement` の scene 台帳の identity が名前だけである可能性。(e) Domain Reload 無効で前回の Play の static 状態（台帳・登録）が残る経路。
      - 探索した境界: SceneDirector の Loading / Unloading、AssetManagement の ContentDirectory 部分、UnityContentDirectoryBackend。
      - 未確認の箇所: `AssetManagement` の scene 台帳の key の詳細、Unity の `Scene.name` が同名 2 Scene をどう扱うか、`ContentLoadManager` の同名 Scene の探索順。実験 (3) で観測する。
    - **N3（I10、I2）Addressables の封じ、protected config、起動時の fail-closed を、snapshot のために弱めない。** snapshot を封じの後に AssetDatabase / Addressables で読まない。snapshot の所在を `content:` の予約 key に入れない（program §10 U3）。built directory の Scene は、harness が build した directory の登録経路（`ContentDirectorySession`）を通ってだけロードされる。
      - 主張を崩しうる既存の経路: (a) `EditorSceneManager.LoadSceneInPlayMode` 等の AssetDatabase 経由の Play 中ロード。(b) `content:runtimeMode` から `probeToken` までの protected 照合。(c) Editor Play で type tree の無い directory を読もうとして失敗し、harness が黙って source に戻る経路。
      - 探索した境界: program §2.2・§5.2・§10 U3、BuildContentRequest。
      - 未確認の箇所: 現 `UnityContentDirectoryAdapter` の type tree の設定、`SealAddressables` の後に `EditorSceneManager` の API がどこまで封じに掛かるか。
    - **N4（I9、G16）Play 停止・Play 再入・Domain Reload 無効の後に、前回の Play の物が残らない。** 停止後に、登録済みの Content Directory、harness の routing の状態、snapshot の一時 asset と一時 directory（AssetDatabase 上の登録と disk の両方）、実験の ledger が残らないか、残る物が次の Play の前に検出・回収される。Build Settings の Scene 一覧と Addressables の設定に snapshot が混入しない。同じ fixture で再び Play に入れ、前回の snapshot を拾わない。
      - 主張を崩しうる既存の経路: (a) `CompleteContentDirectoryPlayStop` が native の unload を待たない同期経路。(b) Domain Reload 無効で static field が残る（本リポジトリの既定）。`ContentRevisionGate` の process 単位の登録（static な `_registration`）と `ContentDirectorySession` の static な rollback もこれに当たる。(c) Editor の crash や Play の異常終了で一時 asset が残る。(d) 複数の Editor instance が同じ一時 path を使う。`RevisionProcessLease` は OS の lease で process を跨ぐので、実験の build identity が他の Editor と重なると干渉しうる（実験の identity は一時 root ごとに一意にする）。
      - 探索した境界: AssetManagement の Play 停止経路、EditorSettings.asset、TestObservationBootstrap の設定退避の前例。
      - 未確認の箇所: Unity の `UnregisterContentDirectory` が native load 中にどう振る舞うか、crash 後の残留の検出。実験 (5)(6) で観測する。
    - **N5（G15、G14）失敗・取消を成功と記録しない。由来の無い成功を記録しない。** native load 中の取消や Scene unload の後に遅れて完了した load の token は回収され、unregister / delete が早まらない。要求していない Scene を load・所有しない（G23）。実験の record の各成功は由来を持つ。
      - 主張を崩しうる既存の経路: (a) `AssetManagement.CloseContentDirectoryAsync` の受付停止と terminal 待ちの順序。(b) additive に開いている未要求の Scene を `RegisterAlreadyLoadedScenes` が登録する経路。
      - 探索した境界: AssetManagement.ContentDirectory.cs。
      - 取消の意味（A2 AG-05 で確定）: 機構 (i) の snapshot は Play 前に既ロードなので、snapshot の load の取消は N/A とする。(i) の N5 は、同じ Play の中の directory 側（`Next`）の取消と、snapshot の Play 中の unload → 再要求で見る。機構 (ii) は snapshot 側も directory の load なので、`Entry`（一時 directory）と `Next` の両方で取消を見る。どちらの機構も、取消は `RoutingBackend` の gate で native load の完了の通知を止めた状態で行い（早すぎる取消で遅延完了の経路を通らない誤った PASS を防ぐ）、未回収の token の数を backend の計数で観測して 0 になることを assert する。
    - **N6 実験は製品と SampleGame に依存を足さず、製品に残らない。** 実験 code と fixture は §3 の 1 folder に閉じ、asmdef 参照を追加しない。`SampleGame` の型・asset を参照しない。マージ前にこの folder を消す。
      - 主張を崩しうる既存の経路: テスト asmdef の既存参照に SampleGame が含まれる場合、harness が誤って使える。実装担当が製品 code に小さな hook を足したくなる経路。
      - 探索した境界: `OneStarMaker.Tests.Editor.asmdef` の位置。
      - 確認済み（A3、base `e0d020d7`）: `OneStarMaker.Tests.Editor.asmdef` の references は `OneStarMaker.Build.Materialization`、`OneStarMaker.Build.Content`、`OneStarMaker.Build.Selection`、`OneStarMaker.Editor`、`OneStarMaker.Runtime`、`Unity.Addressables`、`Unity.ResourceManager`、`UniTask`、`UniTask.Addressables` で、SampleGame を含まない。`Runtime/AssemblyInfo.cs` 6 行目が `OneStarMaker.Tests.Editor` に internal を開く。Phase B は着手時の head で同じ確認を §4 の手順 0 として繰り返す。
  - 成立条件（起きなければならないこと）:
    - **E1** fixture の `Entry`（snapshot）と `Next`（built directory）を同じ Play でロードし、両方が同時に loaded な状態を経て、実験の record に両方の由来（由来マーカーの値で確かめた物）・所有者・unload の経路が残る。`Next` を unload して再びロードできる（再試行）。機構 (i)・(ii) のそれぞれで行う。
    - **E2** 機構 (i)・(ii) のそれぞれに、§2「判定定義」の 3 値（成立 / 限定付きで成立 / 不成立）を、N1〜N5 と実験 (1)〜(6) の観測から付ける。各判定に一次証拠（実験の record と test の XML）を対応づける。
    - **E3** 比較の基準として、Unity 標準の Play（snapshot を作らない）で元の Scene がどう隔離されるかと、`RegisterAlreadyLoadedScenes` 相当の名前の再利用が何を取り込むかを、同じ fixture で観測して残す（実験 (1)）。owner の複製の要求は撤回しない。基準は「複製が何を追加で守るか」を示すために使う。
    - **E4** Enter Play Mode の 4 通り（Domain Reload 有効 / 無効 × Scene Reload 有効 / 無効）で、各機構の判定がどう変わるかを残す。限定付きで成立なら、成立する設定の範囲を書く。
    - **E5** 旧 Hybrid Play の廃止理由の回顧が §2.1 にあり（A1 で書いた）、各機構の判定に §2.1 の失敗の型 H1〜H4 を持つかが添えられる。
  - 不成立の結論を owner に返す時は、どの不変条件がどの cell で、どの順序で破れたか、D4 のどの前提（「複製を作る」と「元を壊さない」と「1 実体」のどれ）が崩れたかを添える（A2 目的レビュー）。
  - E2・E4・E5 の判定と判定表は**判定 C が §7 に書く**。B の head では実験が未実行なので、B に結果の文書を書かせない。B が満たすのは、判定に必要な観測を record に出す test と record の形（AC15）までである。
- 受け入れ条件（各条件の検証例。実験の番号は program §8 S0-b 行の (1)〜(6)）:

| ID | 検証する条件 | 実験 | 観測と合格 |
|---|---|---|---|
| AC0 | N1 の検出器の妥当性 | (1)(2) | 負の対照。harness が意図的に元の Scene を壊す操作（Play 中に元の Scene の値を書き換えて保存する、Dirty を消す）を 1 回ずつ行い、N1 の比較がそれぞれを FAIL として検出する。検出できない項目があれば、その項目の N1 の PASS は全 cell で数えない |
| AC1 | E3、N1 | (1) | snapshot を作らずに `Entry`（Dirty）を開いたまま Play に入り、Play 中に `Entry` の GameObject を 1 つ消して停止する。停止後の `Entry` の disk bytes の SHA-256、Dirty、未保存の値の固定集合を Play 前と比べた結果と、名前の再利用で登録された Scene の一覧が record にある。**合否を問わない基準の観測**（Unity 標準が守る範囲を示す） |
| AC2 | N1（失敗経路） | (2) | 各機構で、`Entry` を Dirty・新規未保存（path が空）・additive に 2 つ・prefab override 付き・参照先 prefab を別途変更、の 5 状態にして snapshot を作り、Play 中に snapshot 側の GameObject を消し、Scene を unload して停止する。N1 の観測項目を **(t0) snapshot 作成前、(t1) snapshot 作成後・Play 前、(t2) Play 停止後** の 3 時点で採り、t0 と t2 が同じ。t0 と t1 の差は snapshot の作成の副作用として別に記録する（測定の操作と機構の失敗を区別する）。`saveAsCopy` 単独の副作用を、Play に入らない対照として同じ 5 状態で採る。1 項目でも t0 と t2 が違えばその cell は FAIL。**拒否を許す状態は新規未保存（path が空）だけ**とし、拒否は「明示の診断を record に残し、元の Scene が t0 のまま、他の状態の試験を続ける」の 3 つを満たす時だけ N/A とする（黙って元を使うのは FAIL） |
| AC3 | N2（失敗経路）、G21 | (3) | 衝突用の Scene `Dup` を Editor で開き、同じ identity・同じ名前の `Dup` を fixture の built directory にも入れて、snapshot を作って `Dup` を要求する。実行中の `Dup` の実体が 1 個だけで、その由来マーカーが snapshot の値、`SceneManager` 上で同名の Scene が 2 つ以上 loaded にならない。元の Scene の Play 中の実体と snapshot が同時に loaded なら、または由来マーカーが build 側の値なら FAIL |
| AC4 | N2、G12 | (3) | snapshot の物理 path / GUID が元と違う場合と同じ場合（機構が許すなら）のそれぞれで、record の論理 identity が `Entry` のまま、物理 path / GUID の写像が record に残る。名前の再利用経路（`GetSceneByName`）が元と snapshot のどちらを返したかを観測する |
| AC5 | N5、G23 | (4) | `Entry` と `Other` を additive で開き、`Entry` だけ snapshot の対象にして Play に入る。`Other` は SceneManager 上で loaded でも、明示に要求するまで SceneBase の lifecycle に参加せず（所有の (a) が無い）、要求した時点で初めて参加する。`Other` が fixture の `SceneResourceMap` に**ある場合と無い場合**の両方で観測し、`RegisterAlreadyLoadedScenes` 相当の処理が `Other` を拾うかを対照として record に残す。要求前の自動参加は FAIL（現行経路の挙動として拾われた場合も FAIL として記録し、拾われる条件を §7 に書く）。Unity の `Awake` / `Start` の抑止は求めない。所有の (c) の有無だけで代用しない |
| AC6 | N5（失敗経路）、G15 | (4) | `RoutingBackend` の gate で `Next` の native load の完了の通知を止めた状態で取消し、続けて unload し、gate を開ける。遅れて完了した token が回収され（未回収 token の計数が 0）、unregister が token の回収より先に起きない。record に取消が「成功」として残らない。機構 (ii) では `Entry`（一時 directory）でも同じことを行う。機構 (i) の snapshot の load の取消は N/A（N5 の取消の意味）。gate は harness の偽の遅延で、遅延の間も native の load は進む。record に gate の区間と native の完了時刻を分けて残し、gate だけの観測で「実 native の取消が安全」と判定しない |
| AC7 | N4、G14 | (4) | (a) `Next` を要求せずに Play を停止する。`Next` が load されず、directory の登録が解除される。(b) gate で `Next` の load の完了を止めた状態で Play を停止する。遅れて完了した token が回収され、登録の解除が回収より先に起きず、次の Play に残らない |
| AC8 | N4（失敗経路）、G16、E4 | (5) | Enter Play Mode の 4 通りそれぞれで、Play → 停止 → 再 Play を 2 回繰り返し、**再 Play の前に毎回 `Entry` の内容と由来マーカーの値を変えて snapshot を作り直す**。各開始時に、前回の directory 登録・routing の状態・snapshot の一時 asset（AssetDatabase と disk）・ledger が残っていない（または検出・回収した記録がある）。Build Settings と Addressables の設定に snapshot が無い。実行中の由来マーカーが今回の値。Domain Reload 無効で前回の static 状態や前回の snapshot を取り込んだら FAIL |
| AC9 | N4（失敗経路） | (6) | (a) snapshot の作成と一時 directory の build を、harness の注入点で例外を投げて途中で失敗させる。どちらも一時 asset・一時 directory が残らず、Play は snapshot 無しで黙って進まない（fail-closed）。(b) crash の残骸の模擬として、死んだ process id の一時 root と偽の snapshot を予め置いてから開始する。開始時に検出・回収され、拾われない。**実際の Editor の異常終了は本スライスで再現しない**ので、その cell は UNTESTED と記録し、PASS に数えない |
| AC10 | N4 | (6) | 同じ project を 2 つの Editor process が開いた状況を模して、一時 path の衝突を起こす（同じ一時 root を 2 つの harness が予約する）。後から予約した側が拒否され、先の側の一時 asset を消さない。**実 process 2 つでの観測は限定**（§5「未知の操作経路」） |
| AC11 | N3（失敗経路） | (2)(3) | 機構 (i) で snapshot を渡す時点が封じより前であることを、封じの時刻と snapshot の load 完了の時刻の順で record に残す。封じの後に AssetDatabase / Addressables で Scene を読んだ load が 1 件でもあれば、その cell は FAIL（N3 の FAIL は限定付きの成立も妨げる。§2 判定定義）。機構 (ii) で、type tree の無い directory を Editor Play で登録した場合に harness が source へ戻らず失敗を記録する |
| AC12 | N3 | — | 実験 code の grep で、snapshot の所在を `content:` で始まる config key に書いていない |
| AC13 | N6 | — | `git diff --stat <base> <head>` に §3 の folder と `docs/handoff/` 以外の file が無い。asmdef の差分が無い。実験 code に `SampleGame` 名前空間・asset path の参照が無い（grep） |
| AC14 | E1 | (2) | 機構 (i)・(ii) のそれぞれで `Entry`（snapshot）→ `Next`（directory）→ `Next` を unload → `Next` を再ロード、が record に由来付きで残る |
| AC15 | E2、E4、E5 | 全て | 判定必須テストの 1 回の実行で、§2「判定定義」の cell の行列の各 cell について、PASS / FAIL / UNTESTED / N/A を決める観測（N1〜N5 のどれを破ったか、由来マーカー、時刻順）が record に揃い、判定 C が §7 に判定表と各 cell の一次証拠への対応を書ける。観測が欠けた cell は UNTESTED として残り、PASS に数えない（判定 C が書く。B は書かない） |
| AC16 | 比較の材料（合否なし） | (2) | 機構 (ii) の一時 directory の build 時間（snapshot 1 件と 2 件）、機構 (i) の snapshot 作成時間を record に残す。G22 の説明（snapshot の hash は当該 Scene 資産の出所追跡で、参照先 asset の一致を保証しない）を、参照先 prefab を変えた AC2 の観測に付けて判定 C が §7 に書く |

  - AC2 の合格例: Dirty の `Entry` で snapshot を作って Play し、停止後に `Entry` が Dirty のまま、未保存の値の固定集合が Play 前と同じ、disk bytes の SHA-256 も同じ。不合格例: 停止後に `Entry` の Dirty が消えている、または snapshot の作成で disk bytes が保存されて変わっている（Dirty の内容を利用者の確認なしに保存したことになる）。
  - AC3 の合格例: `SceneManager.sceneCount` の中で loaded な `Entry` が 1 個で、その path が snapshot の path（(i)）か、directory の Scene（(ii) の一時 directory 由来）。不合格例: 元の `Entry`（Unity の Play が開いた物）と snapshot の両方が loaded。
  - AC8 の境界: 「前回の物が残っていない」は 2 回目の Play の開始時（harness の最初の処理）に判定する。停止の直後ではない。停止から 2 回目の開始までに harness が回収すれば合格で、その回収を record に残す。
  - 反復: native の timing が絡む cell（AC6、AC7、AC8）は同じ条件で 3 回行い、1 回でも FAIL があれば FAIL とする（1 回きりの偶然の PASS を防ぐ）。
- ここでは答えない問いと所有する後続スライス:
  - 要求・候補・LoadRecord の製品の契約、resolver を AssetManagement の必須依存にしないこと → S0-a `GENERIC_CONTENT_REQUEST_CONTRACT`。
  - `InstallContentDirectory` の 2 件目拒否を製品で変えるか → program §10 U1 (2)。(ii) が勝った時に S0-b の直後に切るスライス（名前は program の改訂で決める）。
  - Play 入口、`ExitingEditMode` の transaction、snapshot を作る契機と通知 → S0-c `EDITOR_PLAY_ENTRY_PROOF`。
  - 異なる build の合成、共有依存の重複の採否（G11、G24） → S2 `CONTENT_PACKAGE_DISCOVERY`。本スライスの (ii) は「fixture の 1 revision の directory + snapshot の一時 directory」の 2 件の登録に限る。
  - 互換判定（G08） → S1 `CONTENT_COMPATIBILITY_SELECTION`。
  - SceneDirector 経由と直接の `IAssetManagement` 経由の製品の接続 → S3 `RUNTIME_PROVIDER_INTEGRATION`。
  - 参照先 asset の overlay（snapshot tier を Scene 以外へ広げるか） → program の owner（R2 M-05。本スライスは対象外と記録するだけ）。
- スライス分割の判断: 契機は「高リスクの初回実機スライスで独立して失敗しうる未知が複数ある」。未知は (a) Unity 標準の Play の隔離（基準）、(b) 機構 (i)、(c) 機構 (ii)（一時 build、2 件の directory 登録、type tree）、(d) Enter Play Mode の 4 通り、(e) cleanup と複数 instance。**分割しない。** 理由: このスライスの問いは (i) と (ii) の比較で、同じ fixture・同じ record・同じ設定の行列で観測しないと比較にならない。A2 の代替構成は Unity 層（OSM を通さない）と OSM 層への分割（S0-b1 / S0-b2）を推奨したが、本スライスは Runtime を変更せず（N6）、両層とも同じ test run で観測できるので、スライスは分けずに **Phase B の実装順を Unity 層 → OSM 層の 2 段にする**（§4）。Unity 層の段で両機構が N1 か N2 を破ることが静的に明らかになった場合（API が無い等）は、OSM 層を書かずに Phase B を終えてよい（その機構の OSM 層の cell は UNTESTED ではなく、Unity 層の FAIL を根拠に判定 C が判定する）。代わりに、各未知の成立を別々に判定できるよう、判定を機構ごと × 設定ごとのセルに分け（E2、E4、AC15）、1 つの未知の失敗が他のセルの証拠を無効にしない構成にした。(b) と (c) のどちらかが harness の段階で作れない場合は、作れた方だけで判定し、作れなかった方を「不成立（理由: 実装不能）」とせず「未判定」として §2 の停止規則に従う。
- 判定定義（スパイク）:
  - cell の行列（判定対象）: 機構 (i)・(ii) × Enter Play Mode の 4 通り × シナリオ（AC2〜AC11、AC14。AC9 は (a) と (b) の模擬、AC10 は in-process の模擬）。AC0 は全 cell の前提、AC1 は基準で判定の対象外。N/A は定義済みのもの（AC2 の新規未保存の拒否、AC6 の (i) の snapshot の取消）だけで、他を N/A にしない。
  - **範囲外の実環境検証（判定対象の cell にしない）:** 実際の Editor の異常終了と、実 process 2 つの衝突。本スライスでは行わず、判定 C が §7 に「未証明の引継ぎ」として記録して後続（S0-c、S3）へ送る。判定を妨げない（R4 F-01）。
  - cell の値の区別: **FAIL** は機構の負の実測（不変条件を破る観測がある）。**UNTESTED** は測定の欠測（harness が動かない、観測が record に無い）で、機構の失敗と偽らない。原因（harness の不足 / 現行製品の制限による拒否 / 技術的に実行不能）を record に分けて残す。
  - 機構ごとの判定（各機構に 1 つ。次の順で決め、重複しない）: S を「その設定の全 cell が PASS か定義済み N/A」である Enter Play Mode の設定の集合とする。
    1. S が 4 通り全てなら **成立**。
    2. そうでなく、S が空でなく、S の外の設定を Editor adapter が機械的に検出・拒否できる根拠（設定値を読む API）があれば **限定付きで成立**。限定は「S の設定に限る」で、シナリオを外す限定は認めない。既定の設定（Domain Reload 無効・Scene Reload 有効）が S の外なら、project の既定の変更が要ることを限定に書く。
    3. S が空で、全ての設定に FAIL の cell が 1 つ以上あれば **不成立**。破った順序（入力・状態・操作の列）を書く。
    4. それ以外（S が空で、FAIL が無く UNTESTED だけで S から外れた設定がある）は **未判定**。
  - 判定の意味: 成立・限定付きで成立は「機構が技術的な採用候補として残った」ことで、製品に統合できる証明ではない（R4 F-01）。
  - 機構 (ii) の 2 段の証明（R4 F-02）: 現行製品は 2 件目の directory を 2 か所で拒否する。`AssetManagement.InstallContentDirectory`（2 件目の install）と、`ContentRevisionGate.Reserve`（`Runtime/BuildContent/ContentRevisionGate.cs` 78〜98 行目。process 単位の static な登録で、path・revision が違っても 2 件目を拒否し、`ContentDirectorySession.RegisterCore` が必ず通る）。したがって既存の `ContentDirectorySession` を 2 つ持つ構成は作れない。
    - **Unity native 層:** `RoutingBackend` は既存 session を使わず、2 つの native directory（`UnityContentDirectoryBackend` 相当の `ContentLoadManager.RegisterContentDirectory`）を直接持つ。複数登録・同時の Scene load・unregister をここで観測する。この層の成功は、session が持つ revision の lease・rollback・token の安全性を証明しない。
    - **OSM 層の制約の観測:** 既存 `ContentDirectorySession.Register` で 2 件目を登録し、`ContentRevisionGate` が拒否することを record に残す。これは現行製品の制約の証拠で、Unity の機構の不成立と同一視しない（cell の値には使わない）。
    - (ii) の判定は native 層の観測から付け、必要な製品変更の候補（AssetManagement の install 数、`ContentRevisionGate` の単一登録、revision の lease、close の順序、token の所有）を判定 C が §7 に別々に列挙する。本スライスでは製品に実装しない。
  - スライスの判定:
    - **GO:** 両機構に「未判定」以外の判定が付き、少なくとも一方が「成立」（技術的な採用候補が残った。製品統合の証明ではない）。AC0、AC1、AC12、AC13、AC15、AC16 が揃う。
    - **CONDITIONAL ACCEPT:** 両機構に「未判定」以外の判定が付き、最良が「限定付きで成立」。限定の範囲を後続（S0-c の Play 入口の検査、S3）が受け入れ条件として引き取れる形で判定 C が §7 に書く。
    - **否定の結論:** 両機構が「不成立」で、証拠が揃っている（AC0 を含む）。スライスは正常に終了し、program の D4 の修正を owner に返す（program §8 S0-b の gate）。GO でも NO-GO でもない第 3 の正常終了で、program の後続（S1 以降）は owner の D4 の判断まで Phase B を始めない。
    - **NO-GO:** どちらかの機構が「未判定」のまま、または AC12・AC13（製品への混入・依存の追加）が不合格。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ、上の判定（GO / CONDITIONAL ACCEPT / 否定の結論）で終了する。最低条件未達のまま終了しない。program §0 の停止規則を転記する: **S0-b の結論が出るまで、Runtime の新 API、配布粒度、複数 directory の合成を凍結しない。S1 以降は S0-b の Phase D より前に Phase B を始めない。本スライスの Phase B を `RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` の Phase B と同時に走らせない。** program §8 の凍結条件を転記する: **実験 (1)〜(6) が成功するまで (i) / (ii) と既ロード Scene の取り込み方を確定せず、製品 code への統合を始めない。**
- A3 後の例外承認: なし
- 決定記録: なし
- 本文へ転記した実装制約:
  - AGENTS.md の常時契約: Game → Framework の一方向、asmdef 参照の追加は設計判断なので勝手に行わない。アセットは `IAssetManagement` 経由で `AssetOwner` を宣言（harness が `AssetManagement` を使う所）。`SceneState` 14 値を変えない。公開 API のログは `ILogger<T>`（実験 code は公開 API を持たない）。Editor コードを Runtime アセンブリに置かない。Unity 側の C# で `record` を使わない。新規 `.cs` の先頭に `#nullable enable`。repo 相対 path は `tools/path-budget.json` の 150 文字以内。破棄されうる `UnityEngine.Object` は `== null` / `!= null` で判定（`?.` / `??` / `is null` を使わない）。テストで `Task.Delay` / `Thread.Sleep` を使わない（Play の待ちは `EnterPlayMode` / `ExitPlayMode` の yield と signal で待つ）。`unity test` / `unity run` を使わず、Unity テストは `pwsh tools/run-tests.ps1`。参照 0 を削除理由にしない（本スライスの消去対象は自分が足した実験 folder だけ）。PR の base は `develop`。
  - Phase B の範囲: 対象を限定した Editor 操作とコンパイル確認まで。Unity テストと Content Directory build の実行・判定は Phase C。
  - 本スライスの制約: 製品 code・SampleGame・asmdef を変えない（N6）。snapshot の所在を `content:` の key に置かない（N3）。fixture は SampleGame の Scene graph の名前を変えた物にしない（program §9）。実験の一時 asset と一時 directory は process id を含む 1 つの一時 root の下に置き、harness の終了と次回の開始で回収する。
- 未決事項:
  - **U-b1** Play の初期 Scene の隔離（R4 R-01。機構 (i)・(ii) に共通の前提）。Unity が元の Scene を Play の初期 Scene としてロードしたまま snapshot を足すと、どちらの機構でも同じ identity の実体が二重になりうる。両機構とも、元の Scene を Play の初期 Scene にしない初期状態の制御を持ち、その制御の後も元の Scene の Editor setup が N1 を満たすことを確かめる。候補は `EditorSceneManager.playModeStartScene` を空の起点 Scene にする形と、Play 前に Editor の Scene 構成を差し替えて Play 後に戻す形（後者は N1 と衝突しやすい）。API の選択は Phase B のコンパイル確認で決めてよい（B 適応）。どの手段も無いことが分かったら、その事実を証拠に両機構の該当 cell を FAIL（技術的に実行不能）とし、UNTESTED にしない。
  - **U-b4** 封じの時点（A3 で追加）。harness は `AbstractApplicationInitializer` を通さないので、OSM 層の各 Play で `AssetManagement.SealAddressables` を起動の config 確定に相当する時点（最初の Scene の要求より前）で harness 自身が呼ぶ。呼ばないと AC11 の「封じより前」が空の主張になる。呼んだ時刻を Enter Play Mode の設定ごとに record に残し、Domain Reload の有無で封じの時点が動かないことを確かめる。
  - **U-b2** 実験 (4) の「SceneDirector の既登録」をどこまで実物で見るか。harness は `AbstractApplicationInitializer` を使わない（SampleGame の派生を要するため）。`RegisterAlreadyLoadedScenes` の処理（名前で `AddScene`）を harness が同じ順序で再現する形を初稿とし、実物との差（protected config、Bootstrap の段の順序）を §6 に書く。SceneDirector を fixture の `SceneResourceMap` と既存の test double（`Tests/Scene/TestDoubles/TestableSceneDirector.cs`）で構成できるかは Phase B の最初に確認する。構成できなければ、所有の (a) と AC5 の SceneBase の非参加は UNTESTED（測定の欠測）になり、その機構は未判定に寄る。harness の再現は bootstrap 全体の証明ではないので、実物との差（protected config、Bootstrap の段の順序、封じの時点）を判定 C が §7 に書き、S3 の実接続の test へ引き継ぐ（R4 F-04）。
  - **U-b3** 実験の test を通常の全 EditMode 回帰から外す方法（§5）。

### 2.1 旧 Hybrid Play の回顧（E5）

本節は A1 で code と履歴を読んで書いた。動作は確認していない。

- **仕組み:** `VariantHybridPlayModeScript`（`unity/Assets/OneStarMaker/Scripts/Editor/Build/Variants/VariantHybridPlayModeScript.cs`、現在は案内だけの残ファイル）は Addressables の Fast Mode DataBuilder の拡張だった。Play 前に、手元に checkout 済みかつ依存閉包が完結する asset だけを AssetDatabase 直読みの local catalog に載せ、他を共有の Addressables 設定から一時的に除外し、起動時に追加ロードする remote catalog で解決させた。一時変更は `AddressablesGroupSnapshot` で記録して build 後に復元し、crash 時の復元を次回の冒頭で行った。revision の一致は `build-info.json` の比較と `WarnOnRevisionMismatchAsync` の警告だった（§20 §5）。
- **切断（2026-09-20）の直接の理由:** RET スライス（`a77cbaf4` 他、PR #69）で、旧 Addressables build・開発 workflow が Content Directory と DIST Delivery に置換されたため、通常経路から切断した。Hybrid Play 自体の不具合が廃止の直接の理由として記録された箇所は、A1 が読んだ範囲（§20、RET の Phase A commit `34c7e373` の差分）には無い。
- **code から読める失敗の型（新しい機構と比べる観点）:**
  - H1 **共有設定の一時改変。** Play のために共有の Addressables 設定を書き換え、復元に頼った。crash で残る。→ 新しい機構は共有設定を書き換えない（N4 の一時 asset の回収と、N6 の製品への非混入で見る）。
  - H2 **閉包の判定に頼った混在。** 「閉包が完結するか」の判定が誤ると、AssetDatabase 直読みと remote の 2 実体が同じ asset に生じうる。→ N2（同一 identity の 1 実体）。本スライスは Scene 資産だけを snapshot にし、参照先 asset は混在させない（対象外）ので、H2 の asset 単位の混在は (i)(ii) とも構造上持たない。ただし (ii) の一時 directory が参照先 asset を自分の directory に複製して持つ場合、fixture の directory と同じ asset が 2 つの directory に入り重複ロードされうる（program §2.4）。AC16 の record で観測し、判定 C が §7 に書く。
  - H3 **revision の不一致を警告で済ませた。** fail-closed ではなかった。→ N3（fail-closed を弱めない）、N5。
  - H4 **2 つの catalog の探索順。** local と remote の解決順が設定に依存した。→ (ii) の Unity の後勝ち探索（N2 の経路 (c)）。
- **Unreal の Cooked Editor との比較（program §2.5、要約で確度は低い）:** 編集する物だけ cook 済みをやめて source に戻す形は (i) に近い。同じ asset が source と cook 済みの 2 実体になる問題は H2 と同じ型で、本スライスは Scene 資産に限ることでこれを避ける。

## 3. 責務マップ

製品 code の変更は無い。全て 1 つの実験 folder に置き、マージ前に消す（N6、AC13）。置き場は `unity/Assets/OneStarMaker/Tests/Editor/Spikes/ContentDemandRouting/`（repo 相対 67 文字。最長の file でも 150 文字の予算内）。asmdef は既存の `OneStarMaker.Tests.Editor` を使い、新しい asmdef も参照も足さない。理由: 実験は `UnityEditor`（`EditorSceneManager`、`EnterPlayMode`）と Runtime の `internal`（`InstallContentDirectory`、`IContentDirectoryBackend`）の両方に触れる必要があり、両方に届く既存の場所がこの asmdef である。SampleGame を参照していないことは Phase B の最初に確認する（N6 の未確認）。

| file（仮称） | 責務 | 所有者・寿命 | 依存 | テスト境界 | 予想行数 |
|---|---|---|---|---|---|
| `FixtureFactory.cs` | `Entry` / `Next` / `Other` / `Dup` の 4 Scene（`Dup` は同名衝突用で、Editor と fixture の directory の両方に置く）、共有 prefab 1 つ、ScriptableObject 1 つ、fixture 用 `SceneResourceMap` を一時 root に生成し、後で消す | 各 test の SetUp / TearDown | `UnityEditor`（AssetDatabase、EditorSceneManager、PrefabUtility） | 生成物の存在と内容を test 自身が確認 | 150 |
| `SnapshotWriter.cs` | 開いている Scene の snapshot を一時 root に書き出し、元の Scene の disk bytes の SHA-256・Dirty・未保存の値の固定集合を前後で採る | 1 回の実験 | `EditorSceneManager.SaveScene(saveAsCopy)`、`System.Security.Cryptography` | AC1・AC2 の観測 | 150 |
| `MechanismAlreadyLoaded.cs` | 機構 (i): snapshot を封じより前に既ロード Scene として Play に渡す | 1 回の Play | `SnapshotWriter`、`EditorSceneManager`、AssetManagement（internal） | AC3・AC4・AC11・AC14 | 150 |
| `MechanismTempDirectory.cs` | 機構 (ii): snapshot から一時 Content Directory を build し（`BuildContentCoordinator`、`declaration` null、type tree 付き）、登録する | 1 回の Play と一時 directory | `OneStarMaker.Build.Content`、`ContentDirectorySession` | AC3・AC6・AC11・AC14・AC16 | 200 |
| `OriginMarker.cs` | 由来マーカーの component（値 1 つ）。snapshot 作成時と build 時に別の値を書く | fixture の寿命 | なし | AC3・AC8 | 30 |
| `RoutingBackend.cs` | 実験専用の `IContentDirectoryBackend`。`AssetManagement.InstallContentDirectory` へ**1 回だけ** install され、内部に 2 つの native directory（`IContentNativeDirectory`。snapshot の一時 directory と fixture の directory）を直接持つ。既存の `ContentDirectorySession` は使わない（`ContentRevisionGate` が 2 件目を拒否するため。§2 判定定義の 2 段の証明）。所有する責務は、2 つの native 登録の順序と解除、論理 identity ごとの解決（同 identity は snapshot を優先し、Unity の後勝ち探索に委ねない）、load の token の計数、取消の gate、停止時の drain と unregister、一方の unregister が失敗しても他方を解除する回収。session の lease・rollback・token の安全性と、製品の 2 件 install の可能性は証明しない | 1 回の Play。停止で両 native 登録を解除する | `IContentDirectoryBackend`・`IContentNativeDirectory`（internal） | AC3・AC6・AC7・AC8 | 220 |
| `ExperimentRecord.cs` | 実験の record（§2 用語）を時刻順に持ち、JSON で test の出力 folder に書く | 1 回の test | なし | record 自身の書き出しを test が確認 | 120 |
| `TempRootLease.cs` | process id を含む一時 root の予約・回収・衝突の拒否 | test run。次回の開始で残留を回収 | `System.IO` | AC8・AC9・AC10 | 100 |
| `EnterPlayModeMatrix.cs` | Enter Play Mode の 4 通りを退避・設定・復元する（`TestObservationBootstrap` の前例に倣う。前例の code は呼ばず写さない） | test 1 件 | `EditorSettings` | AC8 | 80 |
| `ContentDemandRoutingExperiments.cs` | 実験 (1)〜(6) の `[UnityTest]`。各 test が record を書き、N1〜N5 の破れを assert する | test run | 上記全て | AC1〜AC11・AC14・AC16 | 400 |
| `docs/handoff/CONTENT_DEMAND_ROUTING_SPIKE.md` §6 / §7 | §6 は B の実装結果と U-b1〜U-b4 の確認。§7 は判定 C の判定表（AC15） | §6 は B、§7 は判定 C | — | — | — |

- 行数の警報: `ContentDemandRoutingExperiments.cs` は 400 行の見込みで 500 行に近い。実験ごとに file を分けると共通の setup が散るので、500 行を超えたら実験 (1)(2) / (3)(4) / (5)(6) の 3 file に分ける（B 適応）。
- 配置の理由: 実験は製品に残さないので、製品の folder・namespace・asmdef の規則に合わせる必要は無いが、製品 code と混ざらないことを path で示すため `Spikes/` の下に閉じる。`RoutingBackend` は将来の resolver の原型に見えるが、**製品の型の候補にしない**（S0-a の範囲。層の先取りをしない）。

## 4. 実装計画

- 変更対象: §3 の folder と本書だけ。
- 順序:
  0. 着手時の head で `OneStarMaker.Tests.Editor.asmdef` の references を読み、SampleGame が無いことを確かめて §6 に記録する。あれば実装・コンパイルへ進まず Phase A へ差し戻す（A2 AG-06）。
  1. `InstallContentDirectory` と `IContentDirectoryBackend` に friend access で届くこと、`EditorSceneManager.playModeStartScene` と `saveAsCopy` が使えることをコンパイルで確かめる（U-b1・U-b2・U-b4 の確認地点）。
  2. **Unity 層（OSM を通さない）:** `FixtureFactory`、`OriginMarker`、`TempRootLease`、`ExperimentRecord`、`SnapshotWriter`、`EnterPlayModeMatrix` と、AC0・AC1・AC2・AC3 の Unity 層の部分（`ContentLoadManager` への直接の登録と同名 Scene の共存）。
  3. **OSM 層:** 機構 (i)、機構 (ii)、`RoutingBackend` と、AssetManagement の owner 台帳・封じ（U-b4）・SceneDirector の既登録を通す AC4〜AC11・AC14。あわせて、既存 `ContentDirectorySession` で 2 件目を登録して `ContentRevisionGate` に拒否される観測（現行製品の制約の証拠）を 1 件置く。
  4. Editor のコンパイル確認と `pwsh tools/contract-audit.ps1`。Unity テストと Content Directory build は実行しない（Phase C）。
- Phase B から Phase A へ差し戻す条件: 製品 code・SampleGame・asmdef の変更が要る。`OneStarMaker.Tests.Editor` が SampleGame を参照している。機構 (i)・(ii) の両方で、実験を書くために本書の対象外（Play 入口の transaction、resolver の製品型）を作る必要が出た。N1〜N6 の主張の文言を変えないと実験が書けない。
- B 適応で扱ってよいもの: U-b1 の手段の選択、file の分割、record の field 名、fixture の細部。根拠を §6 に書く。
- 対象外を維持する方法: AC13 の `git diff --stat` と grep。マージ前の Phase D で実験 folder を消し、結論を program へ harvest する。

## 5. テストとレビュー計画

- 単体テスト: なし。実験は全て Unity 上の観測で、中核ロジックを製品に足さない。`ExperimentRecord` と `TempRootLease` の自己確認は実験 test の中で行う。
- 差し戻し中の起点 `-Filter`: `OneStarMaker.Tests.Editor.Spikes.ContentDemandRouting`（namespace filter。全件性は XML の test 名で確認する）。
- 判定必須テスト: 上の filter の全 test（実験 (1)〜(6)、Enter Play Mode の 4 通り）。XML に実行 test 名と件数が収録されていること。
- 全 EditMode 回帰の適用除外: **適用除外する。** 理由: 製品 code を変えない（AC13 で確認）ので製品の回帰の対象が無い。実験 test は Enter Play Mode の設定を切り替え、Content Directory を build するので、通常の全件回帰に混ぜると他の test の前提を乱しうる。代替証拠: AC13（製品 file と asmdef の差分が無い）と、実験 test が `EnterPlayModeMatrix` で設定を復元したことの record。U-b3: 実験 test を通常の全件回帰から外す手段（`[Explicit]` か category か）は、`run-tests.ps1` が `-testFilter` だけを渡す（`tools/run-tests.ps1` 246〜247 行目）ことを前提に Phase B が選ぶ。`[Explicit]` は名前の明示指定で実行されるかを Phase C の最初の実行で確認する（未確認）。
- 統合・Unity テスト: 判定必須テストと同じ。Windows では最初の実行から sandbox 外の承認済み経路で `pwsh tools/run-tests.ps1 -Filter <上の filter>` を実行する。
- 操作・実行時・目視条件の検証経路:
  - 担当: Phase C の実行担当（Unity テストと Content Directory build を伴うので動作確認・Phase B では実行しない）。
  - 環境・初期状態: このリポジトリの checkout、Unity `6000.6.0f1`、Unity Editor が閉じている、`unity/Temp/UnityLockfile` が無い。実験 test が fixture を一時 root に生成するので、事前の Scene 操作は要らない。
  - 操作: `pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.Spikes.ContentDemandRouting`。
  - 観測と合否: test の XML（実行 test 名・件数・failed 0）と、各 test が書く実験の record の JSON。**N1〜N5 の破れは test の失敗にしない。** cell の FAIL として record に残し、次の cell へ進む（1 つの cell の FAIL で他の cell の観測を止めないため）。test の assert は harness 自身の健全さ（record を書けた、設定を復元した、一時 root を回収した、AC0 の検出器が壊れを検出した）に限り、XML の failed 0 は「行列を最後まで観測できた」ことを意味する。cell の値と機構ごとの判定は、判断担当が record から §2 の判定定義に照らして決める。
  - 1 回の実行の構成（A2 AG-04）: 1 つの test が 1 つの Enter Play Mode の設定を受け持つ（4 test）。各 test は開始時に設定を退避して変え、全シナリオの Play の往復（AC8 の 2 回の再 Play、反復 3 回を含む）を順に行い、TearDown で設定を復元する。設定の変更は `EnterPlayMode` の yield より前に行い、Play の途中では変えない。test の順序に依存しないよう、各 test は自分の一時 root だけを使い、開始時に前の test の残骸を検出・回収して record に残す。AC0・AC1 は Play の設定に依らない別 test にする。
  - 対象版・保存証拠: GO 候補 head の XML、record の JSON、Unity のログを evidence bundle に収録し、[レビュー証拠](../../.agents/skills/osm-workflow/references/review-evidence.md#保存先とリモートへの配布) に従って Artifact Storage へ置く。C / C' は bundle から読む。
  - 画像は使わない。全ての観測は値（SHA-256、Dirty、Scene の一覧、時刻順）で取る。
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点:
  - batch mode の EditMode test から `EnterPlayMode` / `ExitPlayMode` で Play を往復し、Dirty な Scene を持ったまま Play に入れるか。Test Runner の Play が Play ボタンの Play と Scene の退避・復元や Enter Play Mode の扱いで違うか（A2 代替構成）: **未確認。初回確認は Phase C。** 理由: Unity テストの実行は Phase C の責任。担当: Phase C の実行担当。確認地点: 判定必須テストの最初の実行。不成立時の対応: batch mode で Dirty の Scene を保てない（test runner が Scene を保存・置換する）なら、test runner が Play 前に Scene を差し替える挙動そのものを AC1 の基準の観測として record に残し、実験を Editor を起動した対話的な実行（Unity CLI で Editor を起動し、harness の menu を 1 回呼ぶ）へ移す。**この切替は A3 で承認済みとし、Phase C の実行担当が実行時に判断してよい**（R4 R-02）。切替後の証拠は次の項目で batch と同等とみなす: 元の Scene の Dirty と N1 の観測項目の t0 / t1 / t2、Enter Play Mode の設定の退避と復元、Play の開始前後と停止後の Scene setup、由来マーカーと record、Unity version。最初に小さな smoke（Play の往復 1 回、`EnterPlayMode` の `expectDomainReload` の扱い、Domain Reload 後の test の継続と record の保持）を Phase C が実行してから行列に入る。
  - 実 process 2 つでの一時 path の衝突（AC10）: batch の test 1 process では実 process 2 つを作れない。**限定:** `TempRootLease` の予約が別 process id の残留 lock を拒否・回収することを、偽の lock file で in-process に観測する。実 process 2 つの観測は本スライスの合格に含めず、判定 C が §7 に未観測と記録する（S0-c の Play 入口が複数 instance を扱う時の入力）。
  - Content Directory の build を EditMode test の中から呼べるか（type tree 付き、`declaration` null）: **未確認。初回確認は Phase C。** 不成立時は、test の外で build する前処理を `run-tests.ps1` の起動前に置く手段は無い（標準の入口を変えない）ので、機構 (ii) を「未判定」として §2 の判定定義に従う。
- 人間の判断が必要な条件: なし（全て値の観測）。
- 高コスト証拠: なし。fixture の Content Directory build は Scene 数件で、不可逆でも高コストでもない。
- 判定 C の照合 script の方式: なし。合否の機械判定は test の assert と XML で閉じ、record の JSON は判断担当が §2 の判定定義に照らして読む。機械向けの期待値 file を持たない。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC12・AC13 の grep と `git diff --stat`。
- A0/A1 主担当: A0 は program（別の計画ブランチ）。A1 は本書の作成担当（Anthropic）。
- A2 独立レビューごとの観点・担当・入力版（実績、2026-11-08）:
  - 目的レビュー: §1 の目的・利用者の原文だけを渡した（入力 sha256 `420ea56538897de651652e1477f049ac1bf37c863600cd6b1b2e02192411ca9d`）。担当は本書の作成に関与していない新しい subagent（Anthropic）。file を読まずに回答した。
  - A0 だけからの代替構成（高リスク）: 目的レビューの回答を記録した後に、同じ担当へ program（A0）だけを渡して続けた。本書を読んでいないと担当が申告した。
  - アーキテクチャゲート: 担当は OpenAI（Codex CLI、read-only sandbox）。入力は A2 入力版 commit `e5b3ca50` の本書（内容 sha256 `6110db8d3c4b3df91998caf5699a6d1b20294a9f2348e8dd9a7eabeeb04ee89f`）、program、architecture-gates.md、AGENTS.md、関係 source の写し。担当は出力 file を書けず（sandbox が書き込みを拒否）、本文を log から回収した。
  - 目的レビューのやり直し（A3）: 改訂後の 2 欄の原文だけを、初回の目的レビュー・代替構成・初稿のどれにも関与していない新しい subagent（Anthropic）に渡した（入力 sha256 `877d855e71663ae6c609b992051d12306390e19c2209fdbe500336fa2ecac5d1`）。結果と採否は A3 採否の表の末尾。
  - Phase A snapshot: 使っていない（A2 の 3 件とも A2 入力版を直接読んだ。改訂版は未 commit で、まだ外部へ渡していない）。
- A3 統合担当・採否（統合担当は A1 の作成担当。2026-11-08 に owner の承認で凍結）:

| 指摘 | 出所 | 採否 | 反映先・理由 |
|---|---|---|---|
| AG-01 2 directory の所有モデルが単一 install と衝突 | アーキテクチャゲート | 採用（R4 F-02 で反映を改めた） | 当初は「1 回 install、内部に 2 session」としたが `ContentRevisionGate` に当たるので、R4 F-02 の native 直結に置き換えた。§2 判定定義に「製品の 2 件 install の証明ではない」と (ii) の判定の意味を追加 |
| AG-02 snapshot 作成の副作用と機構の失敗を区別できない | アーキテクチャゲート | 採用 | AC2 を t0 / t1 / t2 の 3 時点の観測にし、`saveAsCopy` 単独の対照と、拒否を許す状態（新規未保存だけ）と拒否の 3 条件を追加 |
| AG-03 3 値判定の規則が実行可能でない | アーキテクチャゲート、目的レビュー（曖昧さ 1・2） | 採用 | §2 に cell の定義と PASS / FAIL / UNTESTED / N/A、機構ごとの判定の規則を書き直し。UNTESTED と欠測を PASS に数えない |
| AG-04 1 回の実行の構成と crash の再現が未定義 | アーキテクチャゲート | 採用 | §5 に 1 回の実行の構成を追加。AC9 を例外の注入と残骸の模擬に分け、実 crash は UNTESTED として PASS に数えない |
| AG-05 機構ごとの取消の意味と token 回収の証明 | アーキテクチャゲート | 採用 | N5 に取消の意味を確定。AC6 を gate と未回収 token の計数にした |
| AG-06 asmdef の確認の時点が曖昧 | アーキテクチャゲート | 採用 | A3 で確認済みと N6 に記録し、§4 手順 0 で B が繰り返す |
| 6 軸を候補ごとに埋め、未確認を明示 | 目的レビュー | 採用 | cell の行列と UNTESTED（AG-03 と同じ反映） |
| 由来を表示名でなく証拠で区別、同名を含める | 目的レビュー、代替構成 | 採用 | 用語「由来マーカー」、`Dup` fixture、AC3 |
| 所有者の意味 | 目的レビュー | 採用 | 用語「所有者」 |
| 同一 session は同時か順か | 目的レビュー | 採用 | 用語「同一 Editor Play session」と E1 に同時に loaded な状態を必須化 |
| 元 Scene の比較項目（GUID・参照・取消/例外途中の停止） | 目的レビュー、代替構成 | 採用（Undo 履歴は除外） | N1 の主張を拡張（mtime、path / GUID、Scene setup、途中停止）。Undo は利用者の Scene 資産ではないので除外 |
| 「封じ」が未定義 | 目的レビュー | 採用 | 用語「封じ」 |
| 再現できる証拠（Unity 版・設定） | 目的レビュー | 採用 | 用語「実験の record」に run ごとの Unity version と設定 |
| 不成立時の返し方 | 目的レビュー | 採用 | §2 成立条件の後に、owner へ返す内容を追加 |
| 作らないもの（SampleGame、製品の先取り、第 3 の候補、UI、品質化、Player、常設 suite） | 目的レビュー | 採用（既に対象外と N6 にある） | 第 3 の候補は問いが (i)(ii) に限られるので追記不要と判断 |
| 負の対照で検出器を先に示す | 代替構成 | 採用 | AC0 を追加し、GO と否定の結論の前提に入れた |
| timing の cell を反復する | 代替構成 | 採用 | 反復 3 回、1 回でも FAIL なら FAIL |
| 再入ごとに内容を変えて古い snapshot の拾いを見る | 代替構成 | 採用 | AC8 |
| 残骸を AssetDatabase・Build Settings・Addressables まで見る、`.meta` の GUID 重複 | 代替構成 | 採用 | N4、N1 の経路 (i) |
| 限定は設定またはシナリオの制限で認める | 代替構成 | 一部採用 | シナリオを外す限定は目的の「どれでも」に反するので認めない。設定の限定は機械的に検出できる時だけ認める |
| 製品の規則の迂回は成立にしない | 代替構成 | 一部採用 | (ii) は迂回なしに試せない。成立を禁じると (ii) の判定が常に限定になり比較にならないので、「必要な製品変更を列挙した上での技術的な成立」と定義した |
| 専用 asmdef 2 つ | 代替構成 | 不採用 | asmdef の追加は設計判断で、既存 `OneStarMaker.Tests.Editor` が Editor と Runtime internal の両方に届く（N6 で確認済み） |
| Play を Editor script の `EnterPlaymode` で行い Test Runner に頼らない | 代替構成 | 保留（Phase C の初回確認に委ねる） | 標準の入口は `run-tests.ps1`。Test Runner と Play ボタンの差は §5 の未知の経路で扱い、不成立なら対話的な実行へ移す既存の代替経路を使う |
| S0-b1 / S0-b2 への分割 | 代替構成 | 不採用（実装順で代替） | §2 スライス分割の判断。Runtime を変更しないので、同時実行の停止規則を分割で避ける利益が無い。Unity 層 → OSM 層の実装順と早期終了で同じ利益を得る |
| R4 F-01 判定が予定試験を完走しても GO に届かない、規則が競合 | 凍結前レビュー R4 | 採用 | §2 判定定義を、範囲外の実環境検証の分離、FAIL と UNTESTED の区別、設定集合 S による決定順序に書き直し。GO を「技術的な採用候補」と定義 |
| R4 F-02 (ii) の 2 session 構成は `ContentRevisionGate` で成立しない | 凍結前レビュー R4 | 採用（code で確認） | `ContentRevisionGate.Reserve` が process 単位で 2 件目を拒否することを確認した。AG-01 の反映は不十分だった。(ii) を native 層と OSM 層の制約の観測の 2 段にし、§3 `RoutingBackend` を native 直結に変更 |
| R4 F-03 SceneDirector の所有と AssetManagement の台帳の所有の混同 | 凍結前レビュー R4 | 採用 | 用語「所有」を (a)(b)(c) に分け、「所有の成立水準」を判定の前提として明記 |
| R4 F-04 未要求 Scene の SceneBase lifecycle の非開始 | 凍結前レビュー R4 | 採用（根拠の文言は補正） | AC5 を強化。program G23 の原文は「scene owner を取らない」で lifecycle とは書いていないが、SceneDirector における owner は SceneBase の lifecycle への参加なので、そう読む。harness の再現と bootstrap の差を S3 へ引き継ぐ |
| R4 R-01 初期 Scene の隔離は両機構に共通 | 凍結前レビュー R4 | 採用 | U-b1 を両機構の前提に書き直し |
| R4 R-02 実行経路の代替を先に承認 | 凍結前レビュー R4 | 採用 | §5 で切替を承認済みとし、証拠の同等性の項目と最初の smoke を指定 |
| R4 R-03 gate だけで実 native の安全を判定しない | 凍結前レビュー R4 | 採用 | AC6 に gate と native の区間の区別 |
| 封じを harness が呼ばないと AC11 が空になる | A3 統合担当（R4 を受けた再点検） | 採用 | U-b4 |
| `ContentRevisionGate` の static と `RevisionProcessLease` の process 跨ぎ | A3 統合担当（同上） | 採用 | N4 の経路 (b)(d) |
| P-01 目的の「どちらも成立しなければ」に限定付きの成立が含まれるか | 目的レビュー | **採用（owner 裁定、2026-11-08）** | 改訂案: 「どちらも成立も限定付きの成立もしなければ」。本文の判定定義は既にこの読み（CONDITIONAL ACCEPT）で書いた。目的の文言の変更は owner が裁定し、変えたら目的レビューをやり直す |
| P-02 目的の「実機の証拠」が Player 端末と誤読されうる | 目的レビュー | **採用（owner 裁定、2026-11-08）** | 改訂案: 「実 Editor 上の実行の証拠」 |
| 目的レビューのやり直し: cancel の時点（Play 停止と競合）が未定 | 目的レビュー（A3 やり直し） | 採用 | AC7 に (b) gate で止めた load 中の Play 停止を追加 |
| 目的レビューのやり直し: 封じの時点が Enter Play Mode の設定で揺れうる | 目的レビュー（A3 やり直し） | 採用 | U-b4 で封じの時刻を設定ごとに記録し、動かないことを確かめる |
| 目的レビューのやり直し: 3 値の境界、由来・所有者・一意・壊れないの定義、同時か順か、4 通りの範囲、限定の具体化、再現性、優劣比較をしない、D4 自体を直さない | 目的レビュー（A3 やり直し） | 反映済み（変更なし） | §2 の用語・判定定義・AC で既に定義済み。目的の文言の再改訂は不要と判断し、やり直しは 1 回で終える |
- C' 用に予約した担当: xAI（cursor-agent の Grok 系）。Phase A では使わない。
- 独立性の強化条件を満たせない場合の理由: 発見 C（1 回目）は B と同じモデル系列（Anthropic）で行った。「Phase C は Phase B と異なるモデルで行う」を満たさない。owner（root）が 2026-11-08 に `独立性制約あり` として受け入れ、続行を指示した（§7）。判定 C の担当は未定。

## 6. Phase B 実装結果

B は実験を実行していないので、判定表と判定を書かない（AC15、§2 の E2〜E5 の注）。以下は実装、HANDOFF との差、未実行と、U-b1〜U-b4 の確認結果である（2026-11-08、base `e0d020d7`、引き継いだ commit `3dd43790`）。

### 6.1 手順 0・1 の確認

- 手順 0: 着手時の head（`3dd43790`）で `OneStarMaker.Tests.Editor.asmdef` の references を読んだ。A3 の記録（N6）と同じ 9 件で、SampleGame を含まない。差し戻し条件に当たらない。
- 手順 1: `AssetManagement.InstallContentDirectory`・`SealAddressables`・`CompleteContentDirectoryPlayStop`、`IContentDirectoryBackend`・`IContentNativeDirectory`・`ContentDirectoryIndex`・`UnityContentDirectoryBackend`、`ContentDirectorySession.Register`、`UnityContentDirectoryAdapter`・`ProjectedContent`・`BuildContentProjectionResult`（Build.Content の internal）、`EditorSceneManager.playModeStartScene`・`SaveScene(saveAsCopy)`・`LoadSceneAsyncInPlayMode` に、この asmdef から届くことをコンパイルで確かめた。

### 6.2 実装

全て `unity/Assets/OneStarMaker/Tests/Editor/Spikes/ContentDemandRouting/` の 21 file（各 `.meta` と、上位 `Spikes` folder の `.meta`）。製品 code・SampleGame・asmdef の差分は無い。

| file | 責務 | §3 との対応 |
|---|---|---|
| `ContentDemandRoutingExperiments.cs` | `[UnityTest]` 5 件。`Baseline_DetectorAndControls`（設定を変えない基準: 製品の 2 件目の登録の拒否、AC2 の saveAsCopy 単独の対照、AC0 の負の対照 2 件、AC1、AC5 の隔離なしの対照）と、Enter Play Mode の 4 通りを 1 件ずつ受け持つ `Matrix_*` 4 件。assert は harness の健全さだけ（record、設定の復元、一時 root の回収、行列の完走、harness 自身の失敗 0、AC0 の検出） | 同じ |
| `Scenarios.cs` | シナリオ名（record の `scenario`）と round の並び。行列は機構ごとに AC10・AC9 (a)・AC9 (b)・main（AC3/AC4/AC11/AC14 と AC2 の Dirty）・AC2 の残り 4 状態・AC5 の 2 通り・AC6/AC7 (a)/AC7 (b) を各 3 回・AC8（Play を 3 回続ける組を 3 回） | 新規（B 適応: file の分割） |
| `RoundRunner.cs` / `.Editor.cs` / `.WithoutPlay.cs` | round を Edit の準備（前の round の残りの検出 N4、Scene setup、t0、snapshot、(ii) の build、t1、Play の初期 Scene）→ Play → Edit の検証（停止の経路、AC7 (b) の gate の解放、AC0 の改ざん、t2、後始末）の順に進める。Play を伴わない観測（AC2 の対照、AC9 (a)、AC10） | 新規（B 適応。`ContentDemandRoutingExperiments.cs` の見込みを分けた。1 file 653 行になったので partial で 3 つに分けた） |
| `RoundPlay.cs` | Play 中の各シナリオの操作と観測。各操作の例外は `play.step.failed` として record に移し、Play を最後まで進める | 新規（同上） |
| `HarnessState.cs` | test 1 件の状態の file（round の位置、round の途中結果、前の round の痕跡、健全さの観測）。場所は SessionState | 新規（B 適応: Domain Reload を跨ぐため） |
| `RoutingBackend.cs` | 実験専用の `IContentDirectoryBackend`。§3 の責務どおり（native directory を直接持つ、先に足した snapshot を優先して解決、token の計数、gate、drain、片方の失敗で止めない unregister） | 同じ |
| `DirectorHarness.cs` | 製品の `SceneDirector`（internal constructor、`useContentDirectory`）と `AssetManagement` を fixture の `SceneResourceMap` と `RoutingBackend` で組む。`RegisterAlreadyLoadedScenes` の再現、封じ（U-b4）、所有 (a)(c) の観測、製品と同じ順序の Play 停止（`ReleaseAll` → `CompleteContentDirectoryPlayStop`、`Application.quitting` から） | 新規（B 適応。§3 では機構 file に含めていた OSM 層の構成を、両機構と基準で共有するため分けた） |
| `MechanismAlreadyLoaded.cs` / `MechanismTempDirectory.cs` | 機構 (i) の Play の構成（封じより前に snapshot を `LoadSceneAsyncInPlayMode` で additive に読む → fixture の directory 1 件 → 封じ → 既ロードの登録）、機構 (ii) の Edit の build と Play の構成（snapshot の directory → fixture の directory の順に 2 件登録） | 同じ |
| `FixtureFactory.cs` | `Boot`・`Entry`・`Other`・`Dup` と build 用の写し `Next`・`Other`・`Dup`、共有の prefab と material を一時 root に作る。directory の build | 同じ |
| `SnapshotWriter.cs` | snapshot の作成（`saveAsCopy` → マーカーの書き換え → import）、失敗時の削除、AC9 (a) の注入点 | 同じ |
| `SceneStateProbe.cs` | N1 の観測項目（disk の SHA-256・mtime、Dirty、loaded、path / GUID、GameObject ごとの名前・active・位置・scale・prefab override の数・component の型）と Editor の Scene setup、比較 | 新規（B 適応: §3 では `SnapshotWriter` に含めていた） |
| `OriginMarker.cs` | 由来マーカー | 同じ（形は下の B 適応 1） |
| `TempRootLease.cs` / `EnterPlayModeMatrix.cs` / `ExperimentRecord.cs` | 一時 root の予約・回収・衝突の拒否、Enter Play Mode の退避・設定・復元、record（JSON Lines） | 同じ |
| `PlayEnvironment.cs` | Play の初期 Scene（U-b1）、project の Game の bootstrap の静穏化、Unity のエラーのログの record への転記 | 新規（B 適応 7） |
| `PlayObserver.cs` | Play 中の Scene の列挙（名前・path・handle・マーカー）、同名の Scene の数と `GetSceneByName` の返す実体、load / unload の通知 | 新規 |
| `GateConstraint.cs` | 現行製品の制約の観測（`ContentDirectorySession.Register` の 2 件目が `ContentRevisionGate` に拒否される、`InstallContentDirectory` の 2 件目が拒否される）。cell の値に使わない | §4 手順 3 の「1 件置く」 |

record は `TestResults/ContentDemandRouting/<run>/<test>.jsonl`（1 行 1 観測。`seq`・`utc`・`frame`・`playing`・`kind` と cell の識別子 `mechanism`・`epm`・`scenario`・`iteration`・`play`）。判断担当が読む主な `kind`: `n1.t0` / `n1.t1` / `n1.t2`（差の項目名）、`snapshot.created`（論理 identity と物理 path / GUID の写像、作成時間）、`tempDirectory.built`（build 時間、snapshot の件数）、`assets.sealed` と `snapshot.playLoad.*`（AC11 の順序）、`observe`（同名の Scene の数・マーカー・`GetSceneByName`・所有 (a)(c)・routing の状態）、`routing.*`（native の完了と gate の区間を別の行、token、unregister とその理由）、`unity.sceneUnloaded`（(b) の判定に routing の unload の行と組で使う）、`n4.roundStart`、`ac6.*`、`ac7b.*`、`ac8.marker`、`ac9a.result`、`ac9b.*`、`ac10.leaseConflict`、`ac0.detector`、`osm.secondRegistration`、`play.step.failed`、`unity.log`、`round.untested`。

### 6.3 HANDOFF との差（B 適応、根拠つき）

1. **由来マーカーは component ではなく root GameObject の名前**（`OriginMarker|<値>`）。この asmdef は Editor 専用で、Editor の MonoBehaviour は Scene の GameObject に付けられず directory にも入らない。asmdef は足せない（N6）。snapshot 側の値は、snapshot を Editor で開かず file の該当行を書き換えて import する（開くと Scene setup が変わり N1 の経路 (g) を自分で起こすため）。名前・path を由来の証拠にしない方針（§2 用語）は保つ。マーカーは fixture の作成時と snapshot の作成時に harness が書く値で、Scene の名前・path とは別。
2. **fixture の ScriptableObject は material で代えた。** Editor asmdef の ScriptableObject 型は build に入らない。G22 の観測は共有 prefab の変更（`ac2-ref-prefab-changed`）で行う。
3. **directory の build は `BuildContentCoordinator` ではなく製品の `UnityContentDirectoryAdapter` を直接呼ぶ。** coordinator は BuildPlan と materialization snapshot（選択の経路）を要し、出力先も `artifacts/bs2b` と `TestResults/bs3-fixture` に限る。adapter は internal で届き、圧縮と `BuildContentOptions` は製品と同じ物が使われる（type tree の扱いは製品のまま。Editor Play で読めるかは C の観測）。active build target が StandaloneWindows64 Player でなければ adapter が拒否し、directory を要する round は `round.untested`（cause `fixture-directory`）になる。
4. **SceneDirector は reflection で組み・呼ぶ。** この asmdef は Foundation と R3 を参照せず、`AddScene` / `UnloadScene` の既定引数（Foundation の `TelemetryLevel`）に直接束縛できない。`TestableSceneDirector` は別の test asmdef（`OneStarMaker.Tests`）にあり使えない。製品の `SceneDirector` を internal constructor で組み、`ISceneQuery` と internal の `Lifecycle` で (a) を、AssetManagement の private な registry で (c) を読む。製品の型の private field（`SceneResource._sceneAssetDescription`、`SceneResourceMap._sceneResources`、`SceneDirector._currentScenes`、`AssetManagement._registry`）を reflection で読み書きする。製品の field 名が変わると harness が動かない（その場合は `play.step.failed` / `round.prepareFailed`）。
5. **file の分割**（6.2 の表）。
6. **一時 root は `unity/Assets/OsmSpikeCdr/<p{pid}-{8hex}>/`**（§3 の folder の外。git 管理外で、test の終了と次回の開始で回収する）。snapshot は import された asset である必要があり、`Editor` folder の下の asset は build に入らないので §3 の folder の下に置けない。build した directory と lock file は同じ root の `disk~`（AssetDatabase の対象外）に置く。adapter は自分の一時 asset を `Assets/OneStarMakerGenerated/BS2b/<identity>` に作って消す（`n4.roundStart` の `adapterGeneratedFolderLeft` で残りを見る）。
7. **project の Game の bootstrap が Play ごとに起動する。** この project では Play に入ると Game の initializer が必ず走り、content mode の config が無いと fail-closed のエラーで止まる。harness はそれを使わない（U-b2）ので、`ObservationReloadTests` の前例に倣い Process の環境変数で addressables mode を与える（Game の名前は書かず、`AbstractApplicationInitializer` の派生から prefix を発見する）。Game の bootstrap は同じ Play の中で自分の AssetManagement と SceneDirector を持って動き、`RegisterAlreadyLoadedScenes` も走らせる（fixture の Scene 名は Game の map に無いので取り込まれない見込み。C で `unity.log` と `observe` で確かめる）。実験の観測はこの干渉を含む。
8. **N1〜N5 の破れを test の失敗にしないため、`LogAssert.ignoreFailingMessages = true` にし、Unity のエラー・例外のログは `unity.log` として record に移す。**
9. **AC6 の取消は SceneDirector ではなく `IAssetManagement.LoadContentSceneAsync` に直接掛ける。** SceneDirector の取消は PreLoad の窓に限られ、Unity Scene の load は `CancellationToken.None` で走るため届かない（S3 の「SceneDirector 経由と直接経由」の入力）。
10. **`RoutingBackend.CompletePlayStop` は、load の terminal を待つ operation があれば unregister をその drain の後へ遅らせる。** 製品の session は待たずに解除する。AC7 (b) の「登録の解除が回収より先に起きない」を backend の責務として持たせた。遅らせた解除が起きなければ次の round の開始時に `n4.roundStart` の `unclosedRouting` として検出・回収する。Domain Reload 有効の設定でも、検出は Play に入る前（Edit の準備）に行うので static の一覧で見える。
11. **新規未保存の Scene は snapshot を拒否する**（`snapshot.refused`、AC2 が許す唯一の拒否。元を黙って使わない）。
12. **AC4 の「path / GUID が元と同じ場合」**: `saveAsCopy` は常に新しい GUID を割り当てるので、この機構では作れない。`snapshot.created` の `guidDiffers` に残す。
13. **AC9 (a) の機構 (i) は snapshot の作成の失敗だけ**（一時 directory の build が無い）。(ii) は snapshot の失敗（`ac9a-fail-snapshot`）と build の失敗（`ac9a-fail-build`）の両方。失敗した round は作った snapshot と directory を全て消し、Play に入らない。
14. **Domain Reload を跨ぐ再開。** round の位置は state file、phase は SessionState に置き、yield の後は file から読み直す。`EnterPlayMode` / `ExitPlayMode` は test method 自身の enumerator から yield する（1 巡目の修正の C-02。UTF は test method の enumerator の位置だけを戻し、入れ子の enumerator の位置と局所変数を戻さない）。
15. **AC0 の改ざんは Play の停止後・t2 の前に行う**（1 巡目の修正の C-06）。AC0 の文言は「Play 中に元の Scene の値を書き換えて保存する」だが、Play 中は Editor の Scene を保存できない。検出器の妥当性（N1 の比較が disk の書き換えと Dirty の消失を検出するか）は改ざんの時点に依らず、t2 の比較の前に壊れていれば確かめられる。

### 6.4 U-b1〜U-b4 の確認結果

- **U-b1（Play の初期 Scene の隔離）:** `EditorSceneManager.playModeStartScene` を fixture の空の `Boot` にする形を採った（コンパイル確認済み、挙動は未観測）。両機構の全 round と AC0 の round で使い、基準（AC1、AC5 の隔離なしの対照）だけ外す。退避した元の値は SessionState に置き、test の終了で戻して読み直す。Play 前に Editor の Scene 構成を差し替える形は使わない。
- **U-b2（SceneDirector の既登録をどこまで実物で見るか）:** 製品の `SceneDirector` を internal constructor で組めることはコンパイルで確かめた（B 適応 4）。`AbstractApplicationInitializer` は使わず、`RegisterAlreadyLoadedScenes` を同じ順序で再現する。実物との差: (1) protected config（`content:runtimeMode`〜`probeToken`）を通らない。(2) Bootstrap の段の順序（BeforeSceneLoad の封じ → AfterSceneLoad の登録）ではなく、Play に入った後に harness が「（(i) は snapshot の読み込み →）directory の登録 → 封じ → 既ロードの登録」の順で行う。(3) 封じは harness が自分の AssetManagement に呼ぶ（U-b4）。(4) 同じ Play で Game の bootstrap が別に動く（B 適応 7）。(5) UICommon は panel の無い空の component。判定 C はこれらを §7 に書き S3 へ引き継ぐ。
- **U-b3（通常の全件回帰から外す方法）:** test class に `[Explicit]` を付けた。`run-tests.ps1 -Filter OneStarMaker.Tests.Editor.Spikes.ContentDemandRouting` で選ばれるかは未確認（C の最初の実行で確かめる。0 件なら完全名の列挙を filter に使う）。
- **U-b4（封じの時点）:** `DirectorHarness.Seal()` を、routing の install の後・最初の Scene の要求より前に呼び、`assets.sealed`（seq・frame、封じの前の Addressables の呼び出し回数）を record に残す。機構 (i) では snapshot の `snapshot.playLoad.completed` が必ずその前の行になる。Domain Reload の有無で順序が動かないかは各設定の record で比べる。

### 6.5 機械検査と Editor 操作

- Editor のコンパイル確認: Unity `6000.6.0f1`、2026-11-08。最初の起動で 1 件のコンパイルエラー（`AsyncOperation` を UniTask で直接 await できない）が出て Editor が Safe Mode の確認で止まった。自分で起動した Editor だけを終了し、frame ごとの完了確認に直して起動し直し、`OneStarMaker.Tests.Editor.dll` がエラー・警告なしで再生成されたことを `unity/Logs/Editor.log` で確かめた後、Editor を正常終了した。その後の変更はコメントの言い換え 2 か所だけ。
- `pwsh tools/contract-audit.ps1`: errors 0、warnings 0（検査 1〜9）。
- AC12: 実験 code に `"content:` の文字列は無い（grep）。AC13: 実験 code に `SampleGame` の文字列は無い（grep）。asmdef の差分は無い。最長の repo 相対 path は 106 文字（予算 150）。

### 6.6 未実行（全て Phase C）

- Unity テスト、Content Directory の build、Play の往復を一度も実行していない。record は 1 行も生成していない。
- C の最初の実行で確かめること（§5 の未知の経路を含む）: (1) `[Explicit]` の test が namespace の filter で選ばれるか。(2) batch の EditMode test で `EnterPlayMode` / `ExitPlayMode` が往復でき、Domain Reload の後に state file から続きを再開できるか（再開できなければ §5 の承認済みの対話的な実行へ移す）。(3) test の中から adapter の build が通り、Editor Play で directory の Scene を読めるか（type tree）。(4) Game の bootstrap が静かに起動し、実験の Scene を取り込まないか。(5) 行列の所要時間。1 件の `Matrix_*` で Play の往復が 52 回、(ii) の build が 27 回あり、4 件で数時間になりうる（test の timeout は 4 時間）。
- 実際の Editor の異常終了と実 process 2 つの衝突は本スライスの範囲外（§2）。record では AC9 (b) と AC10 を模擬と明記している。

### 6.7 発見 C の差し戻しの修正（1 巡目、2026-11-08）

引き継いだ commit `cff662b1`（その時点の implementation head `9a606dac`）。修正の implementation head は `c45a37b8`（§0 の欄）。§7.1 の C-01〜C-06 を全て扱った（C-07 は owner の裁定どおり何もしない）。変更は §3 の folder の中だけで、製品 code・SampleGame・asmdef の差分は無い。

| finding | 修正 |
|---|---|
| C-01 AC7 (b) で `RoutingBackend` 自身が FAIL を作る | `LoadSceneAsync` が native の完了（gate の解放）の後に `!_accepting`（Play の停止・close の後）を見て、token を発行せず、Scene を unload して回収し、`ContentDirectoryException`（`DirectoryNotRegistered`）を投げる。回収はその operation の計数を持ったまま行うので、遅らせた unregister は回収の後に起きる。record は `routing.load.completedAfterStop` → `routing.abandoned.recovered` → `routing.unregistered`（reason `deferred-after-drain`）の順になる見込み。Play の停止で Unity が既に Scene を unload していれば native の unload を呼ばず `how=already-unloaded-by-unity` で回収済みに数える（製品の `ReleaseSceneTokenAfterPlayStop` と同じく、Unity が片付けた後は token だけを返す扱い） |
| C-02 DR 有効の `Matrix_*` が最初の reload の後に進まない | 入れ子の `RoundRunner.Run` を廃し、各 test method が `RoundRunner.Start` の後に `for (step = Next(); step != Done; step = Next()) yield return Instruction(step);` を持つ形にした。`Instruction` は `EnterPlayMode` / `ExitPlayMode` の instruction そのものを返すので、Play の出入りは test method 自身の enumerator から yield される。reload の後は yield の次の `Next()` から再開し、SessionState の phase（`entering` / `playing` / `played` / `exiting` / `verifying`）から続きを決める。Play 中の観測と Edit の検証は reload を伴わないので入れ子のままにした。歩が途中で失われた場合（phase が `playing` / `verifying` のまま `Next` に戻った等）は `harness.stepError` と harness の失敗として残し、偽の PASS にしない |
| C-03 AC5 の前提が行列で成り立たない | 基準 test に隔離なし・`Other` が map に無い round（`ac5-control-no-isolation-not-in-map`）を足した。`Baseline_DetectorAndControls` の round は 6 → 7。AC5 の各 round に前提の行 `ac5.premise`（隔離の有無、map の有無、要求前に Play 中で loaded な `Other` の数）を残す。数が 0 なら「要求前に参加しない」は自明な PASS で、判定担当はこの行で区別する |
| C-04 record の `seq` が重複しうる | `ExperimentRecord.Open` が同じ domain の中で path ごとに 1 つの instance を共有する。Domain Reload で共有が消えた後は、最初に開いた時に既存の行数から再開する（従来どおり） |
| C-05 回収の失敗を回収済みに数える | 回収の成功と失敗を別の計数にした（`abandonedRecoveryFailed`）。失敗は `unrecoveredAbandoned` に残る。AC6 の drain の待ちは計数ではなく operation の終端（`PendingOperations == 0`）だけを見て、両方の数を `ac6.afterDrain` の `routing` に残す |
| C-06 AC0 の改ざんの時点 | code は変えない。§6.3 の 15 に B 適応として理由を書いた |

- Editor のコンパイル確認: Unity `6000.6.0f1`、2026-11-08。閉じた状態からこの worktree の project を起動し、`OneStarMaker.Tests.Editor.dll` が再生成され、`unity/Logs/Editor.log` に `error CS` が 0 件、実験 folder の警告が 0 件であることを確かめた。Editor は自分で起動した物だけを main window から正常終了した（Scene・asset の編集はしていない）。初回 import が vendor の `MobileDependencyResolver` の `.pdb.meta` 5 件を消した（この worktree に `.pdb` が無いため）ので、作業範囲外として `git restore` で戻した。
- `pwsh tools/contract-audit.ps1`: errors 0、warnings 0（検査 1〜9）。
- AC12・AC13: 実験 code に `"content:` と `SampleGame` の文字列は無い（grep）。asmdef の差分は無い。最大の file は `RoutingBackend.cs` の 472 行（500 行の警報に届かない）。
- 未実行（Phase C）: Unity テスト、Content Directory の build、Play の往復。C-01 の record の順序と、C-02 の DR 有効の設定で round が 2 件以上進むこと（§7.1 の smoke）は C の最初の実行で確かめる。§6.6 の (1)〜(5) も残る。

## 7. Phase C

### 7.1 発見 C（1 回目、2026-11-08）

- 種別: 発見（GO 判定はしない。C' を起動しない）。
- 対象: implementation base `e0d020d7`、implementation head `9a606dac`（B の出口の commit。欄の句を照合し、HEAD と親の差分に `docs/handoff/` 以外の file があることを確かめた）。固定 SHA からの detached worktree で読んだ。作業ツリーは clean。PR は無い。
- 使用した evidence bundle の取得案内・状態: 該当なし（テスト未実行の発見レビュー。commit 固定の diff と機械的な grep だけを入力にした）。
- 担当・モデル: Anthropic（Claude Code）。**`独立性制約あり`:** B と同じモデル系列で、「Phase C は Phase B と異なるモデルで行う」を満たさない。owner（root）が 2026-11-08 にこの制約を受け入れて続行を指示した。判定 C はこの制約を引き継がず、改めて担当を選ぶ。
- 構造適合: 変更は §3 の folder・上位 `Spikes.meta`・`docs/handoff/` だけ（AC13。`git diff --name-only e0d020d7 9a606dac`）。asmdef の差分なし。実験 code に `"content:` と `SampleGame` の文字列なし（AC12・AC13 の grep）。file の分割と責務は §6.2 の表と一致する。最大は `RoutingBackend.cs` の 431 行で、500 行の警報に届かない。
- 現在の問いを阻害する findings:
  - **C-01 AC7 (b) で `RoutingBackend` 自身が FAIL を作る。** 違反: AC7 (b)「遅れて完了した token が回収され、登録の解除が回収より先に起きず」、§3 `RoutingBackend` の責務「停止時の drain と unregister」。経路: `StopUnderGateAsync` が gate を閉じたまま Play を止める → `CompletePlayStop` は pending があるので解除を遅らせる（`RoutingBackend.cs` 213〜221 行目）→ Verify が gate を開ける → `LoadSceneAsync` は `_accepting=false` を見ずに live token を発行して `RoutedScene` を返す（同 154〜157 行目）→ 製品の `AssetManagement.LoadContentSceneAsync` が `ReleaseAll` の後の台帳へそれを足し、誰も返さない → `CompleteOperation` の遅延 `UnregisterAll`（同 314 行目）が `liveTokens=1` のまま登録を外す。両機構・4 設定の全てで同じ順序になるので、AC7 (b) の cell が全て FAIL になり、harness の欠陥が機構の不成立に見える。修正の方向（B 適応）: 停止の後（`!_accepting`）に完了した load は token を発行せず、abandoned として回収の経路へ回す（製品 `ContentDirectorySession.LoadSceneAsync` の取消後の unload の扱いが前例）。修正後は、AC7 (b) の record で token の回収の行が unregister の行より前に来ることを C の実行で確かめる。
  - **C-02 Domain Reload 有効の 2 件の `Matrix_*` が標準の経路で最後まで進まない（source から予測）。** 違反: AC15（判定必須テストの 1 回の実行で全 cell の観測が揃う）、E4。根拠: UTF 1.8.0 の `EditModeRunner.PrepareForDomainReload` は test method 自身の enumerator（`TestEnumerator.Enumerator`）の `<>1__state` だけを退避し、reload 後は `EditorEnumeratorTestWorkItem` がその enumerator にだけ位置を戻す。入れ子の enumerator（`EnumeratorHelper` の static な stack）は戻らない。実験は `EnterPlayMode` を test method ではなく入れ子の `RoundRunner.Run` の中で yield するので、最初の reload の後、test method は `yield return RoundRunner.Run(...)` の次から再開し、`RoundRunner.Finish()` を round 0 件で呼ぶ。`AssertHealthy` の「行列を最後まで進められなかった」で失敗するので偽の PASS にはならないが、DR 有効の 2 設定の cell が全て UNTESTED になり、両機構の判定が良くても限定付きで成立、悪ければ未判定に寄る。§5 の未知の経路 (2) に当たる。修正の方向（B 適応）: `EnterPlayMode` / `ExitPlayMode` を test method の本体から直接 yield し、round の進行は従来どおり state file から読む形にする。§5 の承認済みの対話的な実行への切替もあるが、未実装なので B の修正を先にする。修正後の最初の実行で、DR 有効の設定で round が 2 件以上進むことを smoke として確かめる。
- 現在の問いを阻害しないが直すべき findings（同じ修正で扱ってよい）:
  - **C-03 AC5 の前提が行列で成り立たない。** U-b1 で Play の起点が `Boot` なので、Editor で additive に開いた `Other` は Play に載らず、行列の `ac5-other-in-map` / `ac5-other-not-in-map` は「要求前に参加しない」が自明に満たされる（2 つの違いも観測できない）。AC5 の「map にある場合と無い場合の両方で、`RegisterAlreadyLoadedScenes` 相当が `Other` を拾うかを対照として残す」のうち、loaded かつ map に無い対照が基準 test に無い。修正の方向: 基準に隔離なし・map に無い `Other` の round を足し、行列の round には Play 中の `Other` の loaded 数を前提の行として残す（判定担当が自明な PASS を区別できるように）。
  - **C-04 record の `seq` が重複しうる。** `ExperimentRecord` は instance ごとに `seq` を持ち、Play 中に作った instance（`RoutingBackend`・`DirectorHarness` が持つ）と Verify で開き直した instance が同じ file へ交互に書く経路（`verify-fallback`、AC7 (b) の gate の解放、次の round の残りの回収）で `seq` が重複・逆転する。file の行順は正しい。U-b4 は `seq` で順序を読むとしているので、path ごとに 1 つの instance を共有するか、書くたびに行数から採る。
  - **C-05 abandoned の回収が失敗しても回収済みに数える。** `DrainAbandoned` は `finally` で `_abandonedRecovered` を増やす（`RoutingBackend.cs` 300 行目）ので、AC6 の「未回収 token の計数が 0」が回収の失敗を隠す。失敗は別の計数にする。
  - **C-06 AC0 の改ざんの時点が HANDOFF と違う。** AC0 は「Play 中に元の Scene の値を書き換えて保存する」だが、実装は Play の停止後・t2 の前に改ざんする（Play 中は Scene を保存できない）。検出器の妥当性の確認としては妥当なので、§6.3 に B 適応として理由を書く。
- owner の裁定を待つ事項:
  - **C-07** `PlayEnvironment.FindGamePrefix` は Game の initializer の型を reflection で発見し、その method を呼ぶ（§6.3 の 7）。AC13 の grep は通るが、N6「`SampleGame` の型・asset を参照しない」に実行時の参照として当たるかは文言だけでは決まらない。
    - **裁定（owner、2026-11-08）: 許す。見直しは後続へ積む。** 許す範囲は、compile 時の依存を足さず、Game の bootstrap を静かに起動させる目的（環境変数の接頭辞の取得）にだけ使うこと。B はこの用途を広げない。判定 C は Game の bootstrap との干渉（§6.3 の 7、§6.6 (4)）を §7 に書く。見直しは下の「後続スライスへ移送する findings」の C-07 に移した。
- 確認して問題が無かった事項: fixture の Scene 名（`Boot`・`Entry`・`Next`・`Other`・`Dup`）は SampleGame の Scene 名と重ならない（Game の bootstrap が名前で取り込む見込みは無い。C の実行で `observe` で確かめる §6.6 (4) は残る）。`UnityContentDirectoryBackend` は static な状態を持たず、instance 2 つで 2 件の native 登録を持てる。機構 (i) が前提にする `PerformUnitySceneLoad` の順序（`GetSceneByName` が先で、directory mode でも同じ）は製品 code と一致する。
- 後続スライスへ移送する findings:
  - **C-07（見直し）** Editor Play の実験・検証が、project の Game の bootstrap を止められず、Game の型を reflection で呼んで静かに起動させている構造。Game を起動させずに Editor Play の検証を行う手段（Play の目的ごとの起動の抑止など）を Framework 側に持つかを見直す。移送先: S0-c `EDITOR_PLAY_ENTRY_PROOF`（Play 入口と RunPurpose を持つ）。Phase D で program §10 へ harvest する時に移送先へ渡す。本スライスでは追加実装しない。
- 実行したテストコマンドと `-Filter`: なし。発見の段階で Unity テストを実行していない（C-01・C-02 は code と UTF の source から確かめられ、明確な差し戻しが決まったので、最終判定用の検証を実行しない）。
- テスト結果: なし。
- 判定必須のうち未実行: 判定必須テストの全て（§5 の filter の 5 test）。
- 未確認事項: §6.6 の (1)〜(5)（`[Explicit]` が namespace の filter で選ばれるか、batch での Play の往復、test の中からの adapter の build と type tree、Game の bootstrap の静穏、所要時間）。C-02 の予測の実機確認。

## 8. Phase C'

未着手。

## 9. Phase D

未着手。マージ前に `unity/Assets/OneStarMaker/Tests/Editor/Spikes/ContentDemandRouting/` を消し、結論を program §5.2・§7 D4・§10 へ harvest する。
