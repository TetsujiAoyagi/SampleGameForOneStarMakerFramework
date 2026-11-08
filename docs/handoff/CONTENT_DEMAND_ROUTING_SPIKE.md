# S0-b Editor snapshot と built directory の混成の技術実証（CONTENT_DEMAND_ROUTING_SPIKE）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。

- type: `slice`（スパイク）
- status: `A`（A1 初稿）
- branch: `claude/play-content-s0b-spike`（PR の base は `develop`）
- implementation base commit: `e0d020d7`（`develop` の head、2026-11-08。A0 の実測 base `e022934` から `unity/` 以下の差分は無い）
- implementation head commit: `未固定`
- risk: `high`（Editor Play の Scene 所有・寿命、Content Directory の native 登録、Enter Play Mode の設定に跨る初回実機の実証）
- owner: root（依頼者）
- created: 2026-11-08
- expires: 2026-12-31、または program [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md) の改訂でこのスライスが置き換わった時点
- harvest to: program §5.2・§7 D4・§10 U1 (2)・U2・U3（結論と採れない方式）、`unity/Assets/Docs/Architecture/20-variant-checkout-workflow.md` §1（旧 Hybrid Play の回顧の要約）。恒久の公開面へは program の完了時に program 経由で移す。実験 code は harvest しない
- 関連: program §2.4・§3・§5.2・§6・§8 S0-b 行・§9・§10（**本スライスの A0**。§0 の停止規則を本書 §2 に転記した）、[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)（所有境界の正本）、[RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md](RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md)（Phase B を同時に走らせない）
- Phase A snapshot の取得案内・状態: 未生成（A2 の入力は A2 入力版 commit の本書だけで、機械向けの期待値・script を持たない。§5「判定 C の照合 script の方式」）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: Phase A の A2（独立レビュー）。
  - 次の作業: 本書の A2 入力版を、§5 の A2 計画に従って目的レビュー、アーキテクチャゲート、A0 だけからの代替構成の 3 件に独立に渡す。互いの指摘を見せない。
  - 引き継ぐ commit: A2 入力版の commit（tree の外で渡す）。
  - 必要な権限: なし（文書のみ）。
  - 停止規則: §2 の停止規則に従う。A3 凍結まで Phase B を始めない。
  - 引き渡さないもの: 目的レビューに §1 の目的・利用者の原文以外を渡さない。

### 実測の方法

- 名前付き参照と行数は `grep` と `wc -l`（base `e0d020d7`）。program §2 の事実のうち本スライスに効く物（§1「現況」）は読み直して確認した。
- 動作の確認は行っていない。Unity 上の挙動は全て Phase B のコンパイル確認と Phase C の実行で初めて観測する。
- `tools/docs-audit.ps1` は A2 入力版の commit 前に実行する。

## 1. 目的と対象外

- 目的: Editor で開いている Scene を Play 前に退避した複製（snapshot）と、built Content Directory の Scene を、同じ Editor Play の session で扱ったとき、ロード・unload・cancel・再試行・Play 停止・Enter Play Mode の設定のどれでも、各 Scene の由来と所有者が一意に保たれ、開いている元の Scene が壊れないかを、SampleGame を使わない小さな fixture の実機の証拠で判定する。複製の機構の候補 2 つ（(i) 複製を Addressables の封じより前に既ロード Scene として渡す、(ii) 複製から一時 Content Directory を build して登録する）のそれぞれについて成立・限定付きで成立・不成立を決め、どちらも成立しなければ program の決定 D4 の修正を owner に返す。製品 code は変えない。
- 利用者: (1) program の owner。結果を見て D4（Editor snapshot tier）を維持するか修正するかを決める。(2) 後続スライス（S0-a の directory 基数、S1、S2、S3、S0-c）の A1 担当。結果を機構の選択と、Editor Play の設定に課す制約の入力に使う。program の利用者 (a) 編集する開発者は最終の受益者で、本スライスでは何も操作しない。
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
  - **実験の record:** harness が 1 回のロードごとに残す観測（論理 identity、由来、物理 path / GUID、Dirty、snapshot の bytes の SHA-256、所有者、unload の経路、時刻順）。S0-a の LoadRecord の製品型ではない。
- 進める最低条件:
  - 不変条件（起きてはいけないこと）:
    - **N1（I11）元の Scene は Play の前後で変わらない。** Play 停止後、元の Scene の disk 上の bytes、Dirty の状態、未保存の内容（Hierarchy の GameObject 名・数と Inspector の値の固定集合）が Play 前と同じ。Play 中の unload・変更は snapshot にだけ及ぶ。
      - 主張を崩しうる既存の経路: (a) `RegisterAlreadyLoadedScenes` と `PerformUnitySceneLoad` が名前で元の Scene の Play 中の実体を取り込み、`SceneManager.UnloadSceneAsync` で unload する経路。(b) Unity の Play 終了時の Scene 復元（Scene Reload の有無で挙動が変わりうる）。(c) `EditorSceneManager.SaveScene(scene, path, saveAsCopy: true)` が元の Scene の Dirty や path を変える可能性。(d) 複製の作成が AssetDatabase の import・refresh を起こし、元の Scene の再読込を促す可能性。(e) `VerifyPlayLauncher` 型の「空 Scene に替える」保護が元の構成を戻さない経路（本スライスの harness はこれを使わない）。
      - 探索した境界: 上記の Runtime / SampleGame Editor の名前付き経路と `EditorSettings.asset`。
      - 未確認の箇所: Unity が Play 前に開いている Scene を退避・復元する内部実装（Temp の backup の扱い）、Scene Reload 無効時の復元、prefab override と新規未保存 Scene（path が空）の扱い。全て実験 (1)(2)(5) で観測する。
    - **N2（I4、G21、G12）1 つの論理 Scene identity は同時に 1 つの実体だけが実行され、その実体の所有者と unload 経路が由来から一意に決まる。** 同 identity の snapshot と directory の Scene がある時、実行されるのは snapshot の 1 個だけ。Unity 標準の Play が開いた元の Scene の実体と snapshot が同時に存在しない。
      - 主張を崩しうる既存の経路: (a) `PerformUnitySceneLoad` の `GetSceneByName`（複製の file 名が元と同じなら名前が衝突し、元と複製のどちらを返すかが Scene の並び順に依存する。違う名前にすると論理 identity から外れる）。(b) `RegisterAlreadyLoadedScenes` が由来を見ずに登録する経路。(c) Unity の `ContentLoadManager` の横断探索は後から登録した directory が先になり、同じ asset を別 directory に入れると重複ロードされうる（program §2.4）。(d) `AssetManagement` の scene 台帳の identity が名前だけである可能性。(e) Domain Reload 無効で前回の Play の static 状態（台帳・登録）が残る経路。
      - 探索した境界: SceneDirector の Loading / Unloading、AssetManagement の ContentDirectory 部分、UnityContentDirectoryBackend。
      - 未確認の箇所: `AssetManagement` の scene 台帳の key の詳細、Unity の `Scene.name` が同名 2 Scene をどう扱うか、`ContentLoadManager` の同名 Scene の探索順。実験 (3) で観測する。
    - **N3（I10、I2）Addressables の封じ、protected config、起動時の fail-closed を、snapshot のために弱めない。** snapshot を封じの後に AssetDatabase / Addressables で読まない。snapshot の所在を `content:` の予約 key に入れない（program §10 U3）。built directory の Scene は、harness が build した directory の登録経路（`ContentDirectorySession`）を通ってだけロードされる。
      - 主張を崩しうる既存の経路: (a) `EditorSceneManager.LoadSceneInPlayMode` 等の AssetDatabase 経由の Play 中ロード。(b) `content:runtimeMode` から `probeToken` までの protected 照合。(c) Editor Play で type tree の無い directory を読もうとして失敗し、harness が黙って source に戻る経路。
      - 探索した境界: program §2.2・§5.2・§10 U3、BuildContentRequest。
      - 未確認の箇所: 現 `UnityContentDirectoryAdapter` の type tree の設定、`SealAddressables` の後に `EditorSceneManager` の API がどこまで封じに掛かるか。
    - **N4（I9、G16）Play 停止・Play 再入・Domain Reload 無効の後に、前回の Play の物が残らない。** 停止後に、登録済みの Content Directory、harness の routing の状態、snapshot の一時 asset と一時 directory、実験の ledger が残らないか、残る物が次の Play の前に検出・回収される。同じ fixture で再び Play に入れる。
      - 主張を崩しうる既存の経路: (a) `CompleteContentDirectoryPlayStop` が native の unload を待たない同期経路。(b) Domain Reload 無効で static field が残る（本リポジトリの既定）。(c) Editor の crash や Play の異常終了で一時 asset が残る。(d) 複数の Editor instance が同じ一時 path を使う。
      - 探索した境界: AssetManagement の Play 停止経路、EditorSettings.asset、TestObservationBootstrap の設定退避の前例。
      - 未確認の箇所: Unity の `UnregisterContentDirectory` が native load 中にどう振る舞うか、crash 後の残留の検出。実験 (5)(6) で観測する。
    - **N5（G15、G14）失敗・取消を成功と記録しない。由来の無い成功を記録しない。** native load 中の取消や Scene unload の後に遅れて完了した load の token は回収され、unregister / delete が早まらない。要求していない Scene を load・所有しない（G23）。実験の record の各成功は由来を持つ。
      - 主張を崩しうる既存の経路: (a) `AssetManagement.CloseContentDirectoryAsync` の受付停止と terminal 待ちの順序。(b) additive に開いている未要求の Scene を `RegisterAlreadyLoadedScenes` が登録する経路。
      - 探索した境界: AssetManagement.ContentDirectory.cs。
      - 未確認の箇所: snapshot 側の取消の意味（(i) は Play 前に既ロードなので取消の対象が無い可能性）。実験 (4) で観測する。
    - **N6 実験は製品と SampleGame に依存を足さず、製品に残らない。** 実験 code と fixture は §3 の 1 folder に閉じ、asmdef 参照を追加しない。`SampleGame` の型・asset を参照しない。マージ前にこの folder を消す。
      - 主張を崩しうる既存の経路: テスト asmdef の既存参照に SampleGame が含まれる場合、harness が誤って使える。実装担当が製品 code に小さな hook を足したくなる経路。
      - 探索した境界: `OneStarMaker.Tests.Editor.asmdef` の位置。
      - 未確認の箇所: 同 asmdef の参照一覧（Phase B が最初に確認し、SampleGame を参照していれば §4 の差し戻し条件）。
  - 成立条件（起きなければならないこと）:
    - **E1** fixture の `Entry`（snapshot）と `Next`（built directory）を同じ Play で順にロードし、実験の record に両方の由来・所有者・unload の経路が残る。`Next` を unload して再びロードできる（再試行）。機構 (i)・(ii) のそれぞれで行う。
    - **E2** 機構 (i)・(ii) のそれぞれに、§2「判定定義」の 3 値（成立 / 限定付きで成立 / 不成立）を、N1〜N5 と実験 (1)〜(6) の観測から付ける。各判定に一次証拠（実験の record と test の XML）を対応づける。
    - **E3** 比較の基準として、Unity 標準の Play（snapshot を作らない）で元の Scene がどう隔離されるかと、`RegisterAlreadyLoadedScenes` 相当の名前の再利用が何を取り込むかを、同じ fixture で観測して残す（実験 (1)）。owner の複製の要求は撤回しない。基準は「複製が何を追加で守るか」を示すために使う。
    - **E4** Enter Play Mode の 4 通り（Domain Reload 有効 / 無効 × Scene Reload 有効 / 無効）で、各機構の判定がどう変わるかを残す。限定付きで成立なら、成立する設定の範囲を書く。
    - **E5** 旧 Hybrid Play の廃止理由の回顧が §2.1 にあり（A1 で書いた）、各機構の判定に §2.1 の失敗の型 H1〜H4 を持つかが添えられる。
  - E2・E4・E5 の判定と判定表は**判定 C が §7 に書く**。B の head では実験が未実行なので、B に結果の文書を書かせない。B が満たすのは、判定に必要な観測を record に出す test と record の形（AC15）までである。
- 受け入れ条件（各条件の検証例。実験の番号は program §8 S0-b 行の (1)〜(6)）:

| ID | 検証する条件 | 実験 | 観測と合格 |
|---|---|---|---|
| AC1 | E3、N1 | (1) | snapshot を作らずに `Entry`（Dirty）を開いたまま Play に入り、Play 中に `Entry` の GameObject を 1 つ消して停止する。停止後の `Entry` の disk bytes の SHA-256、Dirty、未保存の値の固定集合を Play 前と比べた結果と、名前の再利用で登録された Scene の一覧が record にある。**合否を問わない基準の観測**（Unity 標準が守る範囲を示す） |
| AC2 | N1（失敗経路） | (2) | 各機構で、`Entry` を Dirty・新規未保存（path が空）・additive に 2 つ・prefab override 付き・参照先 prefab を別途変更、の 5 状態にして snapshot を作り、Play 中に snapshot 側の GameObject を消し、Scene を unload して停止する。停止後、元の Scene の disk bytes、Dirty、未保存の値の固定集合が Play 前と同じ。1 つでも違えばその機構は N1 不成立。新規未保存の Scene で snapshot を作れない機構は、その状態について「対象外として拒否した」と record に残れば N1 は保たれる（拒否は不成立ではない。黙って元を使うのは不成立） |
| AC3 | N2（失敗経路）、G21 | (3) | `Entry` の snapshot と、同じ identity の `Entry` を含む built directory を同時に用意して `Entry` を要求する。実行中の `Entry` の実体が 1 個だけで、その由来が snapshot、`SceneManager` 上で同名の Scene が 2 つ以上 loaded にならない。元の Scene の Play 中の実体と snapshot が同時に loaded なら不成立 |
| AC4 | N2、G12 | (3) | snapshot の物理 path / GUID が元と違う場合と同じ場合（機構が許すなら）のそれぞれで、record の論理 identity が `Entry` のまま、物理 path / GUID の写像が record に残る。名前の再利用経路（`GetSceneByName`）が元と snapshot のどちらを返したかを観測する |
| AC5 | N5、G23 | (4) | `Entry` と `Other` を additive で開き、`Entry` だけ snapshot の対象にして Play に入る。`Other` に所有者が付かない、または既登録の例外として record に理由付きで残る |
| AC6 | N5（失敗経路）、G15 | (4) | built directory の `Next` の load を開始した直後に取消し、続けて unload する。遅れて完了した token が回収され、unregister が token の回収より先に起きない。record に取消が「成功」として残らない |
| AC7 | N4、G14 | (4) | `Next` を要求せずに Play を停止する。`Next` が load されず、directory の登録が解除される |
| AC8 | N4（失敗経路）、G16、E4 | (5) | Enter Play Mode の 4 通りそれぞれで、Play → 停止 → 同じ fixture で再 Play を 2 回繰り返す。2 回目の開始時に、前回の directory 登録・routing の状態・snapshot の一時 asset・ledger が残っていない（または検出・回収した記録がある）。Domain Reload 無効で前回の static 状態を取り込んだら不成立 |
| AC9 | N4（失敗経路） | (6) | snapshot の作成を途中で失敗させる（書き込み先を読み取り専用にする等）と、一時 directory の build を途中で失敗させる。どちらも一時 asset・一時 directory が残らないか、次の開始時に回収され、Play は snapshot 無しで黙って進まない（fail-closed） |
| AC10 | N4 | (6) | 同じ project を 2 つの Editor process が開いた状況を模して、一時 path の衝突を起こす（同じ一時 root を 2 つの harness が予約する）。後から予約した側が拒否され、先の側の一時 asset を消さない。**実 process 2 つでの観測は限定**（§5「未知の操作経路」） |
| AC11 | N3（失敗経路） | (2)(3) | 機構 (i) で snapshot を渡す時点が封じより前であることを、封じの時刻と snapshot の load 完了の時刻の順で record に残す。封じの後に AssetDatabase / Addressables で Scene を読んだ load が 1 件でもあれば、その機構は不成立。機構 (ii) で、type tree の無い directory を Editor Play で登録した場合に harness が source へ戻らず失敗を記録する |
| AC12 | N3 | — | 実験 code の grep で、snapshot の所在を `content:` で始まる config key に書いていない |
| AC13 | N6 | — | `git diff --stat <base> <head>` に §3 の folder と `docs/handoff/` 以外の file が無い。asmdef の差分が無い。実験 code に `SampleGame` 名前空間・asset path の参照が無い（grep） |
| AC14 | E1 | (2) | 機構 (i)・(ii) のそれぞれで `Entry`（snapshot）→ `Next`（directory）→ `Next` を unload → `Next` を再ロード、が record に由来付きで残る |
| AC15 | E2、E4、E5 | 全て | 判定必須テストの 1 回の実行で、機構ごと × Enter Play Mode の 4 通りの各セルについて、N1〜N5 のどれを破ったか・破らなかったかを決める観測が record に揃い、判定 C が §7 に判定表と各セルの一次証拠への対応を書ける。観測が欠けたセルは「未判定」として判定表に残る（判定 C が書く。B は書かない） |
| AC16 | 比較の材料（合否なし） | (2) | 機構 (ii) の一時 directory の build 時間（snapshot 1 件と 2 件）、機構 (i) の snapshot 作成時間を record に残す。G22 の説明（snapshot の hash は当該 Scene 資産の出所追跡で、参照先 asset の一致を保証しない）を、参照先 prefab を変えた AC2 の観測に付けて判定 C が §7 に書く |

  - AC2 の合格例: Dirty の `Entry` で snapshot を作って Play し、停止後に `Entry` が Dirty のまま、未保存の値の固定集合が Play 前と同じ、disk bytes の SHA-256 も同じ。不合格例: 停止後に `Entry` の Dirty が消えている、または snapshot の作成で disk bytes が保存されて変わっている（Dirty の内容を利用者の確認なしに保存したことになる）。
  - AC3 の合格例: `SceneManager.sceneCount` の中で loaded な `Entry` が 1 個で、その path が snapshot の path（(i)）か、directory の Scene（(ii) の一時 directory 由来）。不合格例: 元の `Entry`（Unity の Play が開いた物）と snapshot の両方が loaded。
  - AC8 の境界: 「前回の物が残っていない」は 2 回目の Play の開始時（harness の最初の処理）に判定する。停止の直後ではない。停止から 2 回目の開始までに harness が回収すれば合格で、その回収を record に残す。
- ここでは答えない問いと所有する後続スライス:
  - 要求・候補・LoadRecord の製品の契約、resolver を AssetManagement の必須依存にしないこと → S0-a `GENERIC_CONTENT_REQUEST_CONTRACT`。
  - `InstallContentDirectory` の 2 件目拒否を製品で変えるか → program §10 U1 (2)。(ii) が勝った時に S0-b の直後に切るスライス（名前は program の改訂で決める）。
  - Play 入口、`ExitingEditMode` の transaction、snapshot を作る契機と通知 → S0-c `EDITOR_PLAY_ENTRY_PROOF`。
  - 異なる build の合成、共有依存の重複の採否（G11、G24） → S2 `CONTENT_PACKAGE_DISCOVERY`。本スライスの (ii) は「fixture の 1 revision の directory + snapshot の一時 directory」の 2 件の登録に限る。
  - 互換判定（G08） → S1 `CONTENT_COMPATIBILITY_SELECTION`。
  - SceneDirector 経由と直接の `IAssetManagement` 経由の製品の接続 → S3 `RUNTIME_PROVIDER_INTEGRATION`。
  - 参照先 asset の overlay（snapshot tier を Scene 以外へ広げるか） → program の owner（R2 M-05。本スライスは対象外と記録するだけ）。
- スライス分割の判断: 契機は「高リスクの初回実機スライスで独立して失敗しうる未知が複数ある」。未知は (a) Unity 標準の Play の隔離（基準）、(b) 機構 (i)、(c) 機構 (ii)（一時 build、2 件の directory 登録、type tree）、(d) Enter Play Mode の 4 通り、(e) cleanup と複数 instance。**分割しない。** 理由: このスライスの問いは (i) と (ii) の比較で、同じ fixture・同じ record・同じ設定の行列で観測しないと比較にならない。代わりに、各未知の成立を別々に判定できるよう、判定を機構ごと × 設定ごとのセルに分け（E2、E4、AC15）、1 つの未知の失敗が他のセルの証拠を無効にしない構成にした。(b) と (c) のどちらかが harness の段階で作れない場合は、作れた方だけで判定し、作れなかった方を「不成立（理由: 実装不能）」とせず「未判定」として §2 の停止規則に従う。
- 判定定義（スパイク）:
  - 機構ごとの判定（各機構に 1 つ）:
    - **成立:** Enter Play Mode の 4 通り全てで N1〜N5 を破る観測が無く、AC2〜AC11 と AC14 が合格。
    - **限定付きで成立:** 一部の設定でだけ N1〜N5 を破る観測が無い。成立する設定の範囲と、その範囲外を Editor adapter が検出・拒否できる根拠（設定値を読める API の存在等）を書く。
    - **不成立:** 全ての設定で N1〜N5 のどれかを破る観測がある。破った順序（入力・状態・操作の列）を書く。
    - **未判定:** harness が作れない、または証拠が揃わない。
  - スライスの判定:
    - **GO:** 両機構に「未判定」以外の判定が付き、少なくとも一方が「成立」。AC1、AC12、AC13、AC15、AC16 が揃う。
    - **CONDITIONAL ACCEPT:** 両機構に「未判定」以外の判定が付き、最良が「限定付きで成立」。限定の範囲を後続（S0-c の Play 入口の検査、S3）が受け入れ条件として引き取れる形で判定 C が §7 に書く。
    - **否定の結論:** 両機構が「不成立」で、証拠が揃っている。スライスは正常に終了し、program の D4 の修正を owner に返す（program §8 S0-b の gate）。GO でも NO-GO でもない第 3 の正常終了で、program の後続（S1 以降）は owner の D4 の判断まで Phase B を始めない。
    - **NO-GO:** どちらかの機構が「未判定」のまま、または AC12・AC13（製品への混入・依存の追加）が不合格。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ、上の判定（GO / CONDITIONAL ACCEPT / 否定の結論）で終了する。最低条件未達のまま終了しない。program §0 の停止規則を転記する: **S0-b の結論が出るまで、Runtime の新 API、配布粒度、複数 directory の合成を凍結しない。S1 以降は S0-b の Phase D より前に Phase B を始めない。本スライスの Phase B を `RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` の Phase B と同時に走らせない。** program §8 の凍結条件を転記する: **実験 (1)〜(6) が成功するまで (i) / (ii) と既ロード Scene の取り込み方を確定せず、製品 code への統合を始めない。**
- A3 後の例外承認: なし
- 決定記録: なし
- 本文へ転記した実装制約:
  - AGENTS.md の常時契約: Game → Framework の一方向、asmdef 参照の追加は設計判断なので勝手に行わない。アセットは `IAssetManagement` 経由で `AssetOwner` を宣言（harness が `AssetManagement` を使う所）。`SceneState` 14 値を変えない。公開 API のログは `ILogger<T>`（実験 code は公開 API を持たない）。Editor コードを Runtime アセンブリに置かない。Unity 側の C# で `record` を使わない。新規 `.cs` の先頭に `#nullable enable`。repo 相対 path は `tools/path-budget.json` の 150 文字以内。破棄されうる `UnityEngine.Object` は `== null` / `!= null` で判定（`?.` / `??` / `is null` を使わない）。テストで `Task.Delay` / `Thread.Sleep` を使わない（Play の待ちは `EnterPlayMode` / `ExitPlayMode` の yield と signal で待つ）。`unity test` / `unity run` を使わず、Unity テストは `pwsh tools/run-tests.ps1`。参照 0 を削除理由にしない（本スライスの消去対象は自分が足した実験 folder だけ）。PR の base は `develop`。
  - Phase B の範囲: 対象を限定した Editor 操作とコンパイル確認まで。Unity テストと Content Directory build の実行・判定は Phase C。
  - 本スライスの制約: 製品 code・SampleGame・asmdef を変えない（N6）。snapshot の所在を `content:` の key に置かない（N3）。fixture は SampleGame の Scene graph の名前を変えた物にしない（program §9）。実験の一時 asset と一時 directory は process id を含む 1 つの一時 root の下に置き、harness の終了と次回の開始で回収する。
- 未決事項:
  - **U-b1** 機構 (i) で、Unity の Play が開く元の Scene の代わりに snapshot を Play の最初の Scene 群にする手段。候補は `EditorSceneManager.playModeStartScene` を空の起点 Scene にし、snapshot を封じの前に additive でロードする形と、Play 前に Editor の Scene 構成を snapshot に差し替えて Play 後に元へ戻す形。後者は「元の Scene に影響を与えない」（I11）と衝突しやすい。どちらを実験するかは Phase B の最初のコンパイル確認で API の可否を見て決めてよい（B 適応）。両方不可なら (i) は「未判定」ではなく、手段が無いことを証拠にして「不成立」とする。
  - **U-b2** 実験 (4) の「SceneDirector の既登録」をどこまで実物で見るか。harness は `AbstractApplicationInitializer` を使わない（SampleGame の派生を要するため）。`RegisterAlreadyLoadedScenes` の処理（名前で `AddScene`）を harness が同じ順序で再現する形を初稿とし、実物との差（protected config、Bootstrap の段の順序）を §6 に書く。SceneDirector を fixture の `SceneResourceMap` と既存の test double（`Tests/Scene/TestDoubles/TestableSceneDirector.cs`）で構成できるかは Phase B の最初に確認する。構成できなければ AssetManagement の直接の経路だけで判定し、SceneDirector の既登録は未確認と記録する（A2 で読み違いを確認したい点）。
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
| `FixtureFactory.cs` | `Entry` / `Next` / `Other` の 3 Scene、共有 prefab 1 つ、ScriptableObject 1 つ、fixture 用 `SceneResourceMap` を一時 root に生成し、後で消す | 各 test の SetUp / TearDown | `UnityEditor`（AssetDatabase、EditorSceneManager、PrefabUtility） | 生成物の存在と内容を test 自身が確認 | 150 |
| `SnapshotWriter.cs` | 開いている Scene の snapshot を一時 root に書き出し、元の Scene の disk bytes の SHA-256・Dirty・未保存の値の固定集合を前後で採る | 1 回の実験 | `EditorSceneManager.SaveScene(saveAsCopy)`、`System.Security.Cryptography` | AC1・AC2 の観測 | 150 |
| `MechanismAlreadyLoaded.cs` | 機構 (i): snapshot を封じより前に既ロード Scene として Play に渡す | 1 回の Play | `SnapshotWriter`、`EditorSceneManager`、AssetManagement（internal） | AC3・AC4・AC11・AC14 | 150 |
| `MechanismTempDirectory.cs` | 機構 (ii): snapshot から一時 Content Directory を build し（`BuildContentCoordinator`、`declaration` null、type tree 付き）、登録する | 1 回の Play と一時 directory | `OneStarMaker.Build.Content`、`ContentDirectorySession` | AC3・AC6・AC11・AC14・AC16 | 200 |
| `RoutingBackend.cs` | 実験専用の `IContentDirectoryBackend`。論理 identity ごとに snapshot の directory と fixture の directory へ振り分け、同 identity は snapshot を優先する。`InstallContentDirectory` の 1 件制限を製品で変えずに 2 件の登録を試すため | 1 回の Play。停止で両 session を閉じる | `IContentDirectoryBackend`（internal） | AC3・AC6・AC7・AC8 | 180 |
| `ExperimentRecord.cs` | 実験の record（§2 用語）を時刻順に持ち、JSON で test の出力 folder に書く | 1 回の test | なし | record 自身の書き出しを test が確認 | 120 |
| `TempRootLease.cs` | process id を含む一時 root の予約・回収・衝突の拒否 | test run。次回の開始で残留を回収 | `System.IO` | AC8・AC9・AC10 | 100 |
| `EnterPlayModeMatrix.cs` | Enter Play Mode の 4 通りを退避・設定・復元する（`TestObservationBootstrap` の前例に倣う。前例の code は呼ばず写さない） | test 1 件 | `EditorSettings` | AC8 | 80 |
| `ContentDemandRoutingExperiments.cs` | 実験 (1)〜(6) の `[UnityTest]`。各 test が record を書き、N1〜N5 の破れを assert する | test run | 上記全て | AC1〜AC11・AC14・AC16 | 400 |
| `docs/handoff/CONTENT_DEMAND_ROUTING_SPIKE.md` §6 / §7 | §6 は B の実装結果と U-b1〜U-b3 の確認。§7 は判定 C の判定表（AC15） | §6 は B、§7 は判定 C | — | — | — |

- 行数の警報: `ContentDemandRoutingExperiments.cs` は 400 行の見込みで 500 行に近い。実験ごとに file を分けると共通の setup が散るので、500 行を超えたら実験 (1)(2) / (3)(4) / (5)(6) の 3 file に分ける（B 適応）。
- 配置の理由: 実験は製品に残さないので、製品の folder・namespace・asmdef の規則に合わせる必要は無いが、製品 code と混ざらないことを path で示すため `Spikes/` の下に閉じる。`RoutingBackend` は将来の resolver の原型に見えるが、**製品の型の候補にしない**（S0-a の範囲。層の先取りをしない）。

## 4. 実装計画

- 変更対象: §3 の folder と本書だけ。
- 順序:
  1. `OneStarMaker.Tests.Editor.asmdef` の参照に SampleGame が無いこと、`InstallContentDirectory` と `IContentDirectoryBackend` に friend access で届くこと、`EditorSceneManager.playModeStartScene` と `saveAsCopy` が使えることをコンパイルで確かめる（N6 と U-b1・U-b2 の確認地点）。
  2. `FixtureFactory`、`TempRootLease`、`ExperimentRecord`、`SnapshotWriter`。
  3. 機構 (i)、機構 (ii)、`RoutingBackend`。
  4. 実験 (1)〜(6) の test と `EnterPlayModeMatrix`。
  5. Editor のコンパイル確認と `pwsh tools/contract-audit.ps1`。Unity テストと Content Directory build は実行しない（Phase C）。
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
  - 観測と合否: test の XML（実行 test 名・件数・failed 0）と、各 test が書く実験の record の JSON。N1〜N5 の破れは test の assert で落ちる。機構ごとの判定（成立・限定付き・不成立）は、record から判断担当が §2 の判定定義に照らして決める（test は「不成立」を失敗として落とすのではなく、record に残して次の設定へ進む書き方にする。1 つのセルの失敗で他のセルの観測を止めないため）。合否の機械判定は test の assert（record の書き出し、設定の復元、製品の不変条件の破れの検出）に限る。
  - 対象版・保存証拠: GO 候補 head の XML、record の JSON、Unity のログを evidence bundle に収録し、[レビュー証拠](../../.agents/skills/osm-workflow/references/review-evidence.md#保存先とリモートへの配布) に従って Artifact Storage へ置く。C / C' は bundle から読む。
  - 画像は使わない。全ての観測は値（SHA-256、Dirty、Scene の一覧、時刻順）で取る。
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点:
  - batch mode の EditMode test から `EnterPlayMode` / `ExitPlayMode` で Play を往復し、Dirty な Scene を持ったまま Play に入れるか: **未確認。初回確認は Phase C。** 理由: Unity テストの実行は Phase C の責任。担当: Phase C の実行担当。確認地点: 判定必須テストの最初の実行。不成立時の対応: batch mode で Dirty の Scene を保てない（test runner が Scene を保存・置換する）なら、test runner が Play 前に Scene を差し替える挙動そのものを AC1 の基準の観測として record に残し、実験を Editor を起動した対話的な実行（Unity CLI で Editor を起動し、harness の menu を 1 回呼ぶ）へ移す。この移行は検証経路の変更なので A3 で合意する（A2 の論点）。
  - 実 process 2 つでの一時 path の衝突（AC10）: batch の test 1 process では実 process 2 つを作れない。**限定:** `TempRootLease` の予約が別 process id の残留 lock を拒否・回収することを、偽の lock file で in-process に観測する。実 process 2 つの観測は本スライスの合格に含めず、判定 C が §7 に未観測と記録する（S0-c の Play 入口が複数 instance を扱う時の入力）。
  - Content Directory の build を EditMode test の中から呼べるか（type tree 付き、`declaration` null）: **未確認。初回確認は Phase C。** 不成立時は、test の外で build する前処理を `run-tests.ps1` の起動前に置く手段は無い（標準の入口を変えない）ので、機構 (ii) を「未判定」として §2 の判定定義に従う。
- 人間の判断が必要な条件: なし（全て値の観測）。
- 高コスト証拠: なし。fixture の Content Directory build は Scene 数件で、不可逆でも高コストでもない。
- 判定 C の照合 script の方式: なし。合否の機械判定は test の assert と XML で閉じ、record の JSON は判断担当が §2 の判定定義に照らして読む。機械向けの期待値 file を持たない。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC12・AC13 の grep と `git diff --stat`。
- A0/A1 主担当: A0 は program（別の計画ブランチ）。A1 は本書の作成担当（Anthropic）。
- A2 独立レビューごとの観点・担当・入力版（予定。実績は A2 で記録する）:
  - 目的レビュー: §1 の目的・利用者の原文だけを渡す。担当は本書の作成に関与していない新しい session（Anthropic、初稿・A0 を読まない）。
  - A0 だけからの代替構成（高リスク）: 目的レビューの回答を記録した後に、同じ担当へ program（A0）だけを渡して続ける。本書を読ませない。
  - アーキテクチャゲート: [アーキテクチャゲート](../../.agents/skills/osm-workflow/references/architecture-gates.md) に従い、本書と関係 source を読む。担当は OpenAI（Codex CLI）。
  - 目的レビューのやり直し用の未関与の担当: Anthropic の新しい session を 1 件残す。
- A3 統合担当・採否: 未実施。
- C' 用に予約した担当: xAI（cursor-agent の Grok 系）。Phase A では使わない。
- 独立性の強化条件を満たせない場合の理由: 未定（B・C の担当が決まった時に記録する）。

## 6. Phase B 実装結果

未着手。B は実験を実行しないので、判定表と判定を書かない（AC15、§2 の E2〜E5 の注）。B が書くのは実装、HANDOFF との差、未実行（Unity テストと build の全て）と、U-b1・U-b2・U-b3 の確認結果である。

## 7. Phase C

未着手。

## 8. Phase C'

未着手。

## 9. Phase D

未着手。マージ前に `unity/Assets/OneStarMaker/Tests/Editor/Spikes/ContentDemandRouting/` を消し、結論を program §5.2・§7 D4・§10 へ harvest する。
