# S-4d5b Planner 実証と S-4 の完了

## 0. メタデータ

- type: `slice`
- status: `B`（Phase B 完了。A3 凍結版は 2026-11-03 に発注者が §4.2 の合意を含めて凍結を承認した）
- branch: `claude/s-4d5b-planner-proof`
- implementation base commit: `fb97da0d`（`develop`、BS5b-2 のマージ後）
- implementation head commit: この票を含む commit
- risk: `normal`（公開 API・asmdef・所有者・寿命・`SceneState`・Addressables の設定を変えない。製品コードの変更は見込まない）
- owner: 発注者
- created: 2026-11-03
- expires: 本スライスの Phase D 完了時
- harvest to: Architecture §05（Scene）、§18（Variant / BuildSystem）、§24（Rendering）、§27（Folder）、Streaming 現状仕様（`docs/streaming/STREAMING_CURRENT_SPEC.md`）
- Phase A snapshot の取得案内・状態: 未生成（発注者の指示により本スライスでは Artifact Storage を使わない。A2 の入力版は §5.1 の内容 sha256 で固定した作業ツリーの版）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票: 次は発見 C（レビュー担当）。動作確認は `対象なし`（§5）。対象は implementation base から implementation head までの文書の差分で、§4 の harvest 表の各行と移り先（§6 の対応表）、program へのリンクの向け直し、対象外の維持（`git diff --stat <base> <head>` が `docs/` と `unity/Assets/Docs/` の `.md` だけ）を照合する。program（`docs/handoff/S-4_FULL_SPEC_WORLD_AUTHORING.md`）と本 HANDOFF は Phase D まで削除しない。発見 C は GO 判定と C' の起動をしない。判定 C は §5 の検証経路（AC1〜AC7 の検証 Play）と AC8 を GO 候補 head で実行する。必要な権限: 発見 C は文書の読みと git だけ（Unity・資格情報・sandbox 外実行は不要）。判定 C の実行担当は Unity Editor の起動・操作（`tools/unity-editor.cmd`）。snapshot・bundle は使わない（発注者の指示で Artifact Storage を使わない）。停止規則は §2 と §4（§4.2 を含む）。引き渡さないもの: C' には §5.1 の A2 記録と C の所見を渡さない。

## 1. 目的と対象外

- 目的: Planner（プランナー）が、Environment・Cell Lighting・Cell VFX を読まずに、Content Directory の Whitebox だけで Spring の local Event と major Event を Play で動かし、その完了をもって S-4 program を Architecture 文書へ畳んで閉じる。
- 利用者: Planner。Editor で宣言を 1 つ選んで Play し、Whitebox の World で Event の trigger を踏んで動作を確かめる。
- 対象外:
  - 製品コード・Framework・公開 API・asmdef の変更（必要になったら §4.2 で停止）。
  - Play As（Addressables mode）での Event の確認。BS5b-2 の AC17 (a) で config までを見ており、本スライスの問いは Content Directory の上で動くかである。
  - BS4 Player（smoke の後に `Application.Quit(0)` し Spring に入らない。Architecture §24 §16.7 の S-9）。
  - Architecture §24 §16.7 と §18 §4「BuildSystem の後続」に送られた事項の解消（S-5 の Tunnel 遷移、identity 後続、Framework の Add 取り残し等）。
  - 新しい自動テストの追加（§2 の D2）。
- 現況（`fb97da0d`）:
  - **Event の実装は完了している。** `Spring_Events_4_2` に local VFX の trigger（`LocalVfxEventTrigger`、付随 asset `Spring_Events_4_2:Burst`）と major Event の開始 trigger（`MajorEventStartTrigger`）がある。`Spring_Event_MultiCellProof` は Season_Spring の `OnDemand` の子で、完了の trigger（`MajorEventCompletionTrigger`）と VFX の起動位置（`MajorEventVfxAnchor`、付随 asset `Spring_Event_MultiCellProof:Burst`）を持つ。Planner の companion set で Environment を載せずに Event が起動・完了することは d5a の AC10（`SeasonMajorEventPlayTests`、Addressables mode と複製 Map）で確かめた。Content Directory の上ではまだ誰も動かしていない。
  - **宣言 `spring-planner-whitebox`。** `SampleGameBuildPurposes.SpringPlannerWhitebox` は roots `InGameScene` / `OutGameScene`、Season `Spring`、Role は `world:cellCompanionSet=Planner` から Events だけ、表現 `First [Whitebox, Full]`。Environment・Cell Lighting・Cell VFX を含まない 70 件。Season の `NecessaryAlways` の子（`Spring_Lighting`・`Spring_AtmosphereVFX`）は Role の filter の対象外なので含まれ、Play でも載る。付随 asset は所有 node（`Spring_Events_4_2`・`Spring_Event_MultiCellProof`）が到達するので `Object` entry として入る（Architecture §18 §4「付随 asset」）。
  - **検証 Play（BS5b-2）。** `Tools/OSM/Content/Verify Play/spring-planner-whitebox`（`VerifyPlayLauncher`）が `Full` build → publish → manifest の宣言の照合 → install → bridge（content の key、宣言の `RuntimeDefaults`、表現は主値 `Whitebox`、`content:firstScene=Title`）→ `EnterPlaymode` を 1 操作で行う。開始前に、開いている Scene が SceneResourceMap の名前・`UICommon` なら拒否する。build は `spring-full` で約 4.5 分だった。directory mode では Addressables を封鎖し、起動ログに封じた 1 行を出す（Architecture §04 §4.7）。
  - **Title から Spring まで。** directory の初回 Scene は `Title`。`TitleView` の Start ボタン（`OnStartRequested`）で `TitleScene` が Home へ切り替え、`HomeScene.OnStabledImpl` が自動で `SceneFlow.EnterInGame` を呼び `InGameSession` に入る。`InGameSession` は Spring `(0,4)` を初期 Ensure で載せ、`PlayerScene` は `WaitUntilWorldReady` の後に `PlayerWorldReadySequence`（Teleport → Focus 登録 → 入力 ON）を行い、`Player ready at Cell stream spawn …` をログに出す。`FlyController.Teleport(Vector3, Vector3)` は public。Cell の Events は companion driver（`SessionCellCompanionLoadDriver`）が resident の Cell の子から載せる。
  - **既知の観測（Architecture §18 §4「検証の残り」）。** directory mode の Editor Play で flight rig が spawn に置かれず原点に留まる観測がある。切り分けは未着手（所有スライス未定）。本スライスの経路はここを通る（§5「未知の操作経路」）。
  - **S-4 program の残り。** `docs/handoff/S-4_FULL_SPEC_WORLD_AUTHORING.md` は d5b の Phase D で harvest して削除すると決まっている（program §12）。program へのリンクは `docs/README.md`（1 箇所）と `docs/handoff/SEASON_WORLD_DESIGN.md`（3 箇所）にある（`artifacts/` 配下の過去の diff は記録なので変えない）。
- UC ごとの利用者の入力:

| UC | 利用者が選ぶ・入れるもの（誰がいつ） | 機械が決めるもの（誰がいつ） | 目的からの正当化 |
|---|---|---|---|
| UC1 Planner Play を始める | 宣言 `spring-planner-whitebox` を menu で選ぶ（Planner、実行時の操作） | content build・publish・install、mode（directory）、表現 `Whitebox`、`world:cellCompanionSet=Planner`、初回 Scene `Title`（宣言と App の既定値から自動導出。宣言自体は開発時に BS5a で済み） | 目的の「Planner が Whitebox だけで Play する」は、何を読むかを Planner が 1 つの選択で指定することそのもの。宣言を選ばせずに既定にすると、Full を読む通常 Play と区別できない |
| UC2 World に入る | Title の Start を押す（Planner、実行時の操作） | Home から InGame への遷移、Spring `(0,4)` の Ensure、Player の配置（既存の自動） | ゲームの既存の入口。d5b で増やした入力ではない |
| UC3 Event を確かめる | Cell (4,2) まで飛び、trigger を踏む（Planner、実行時の操作） | Cell・Events の streaming と companion のロード、VFX の起動、major Event の Add / 回収（既存の自動） | 目的の「Event を Play で動かす」の操作そのもの。判定 C では同じ操作を `eval` の Teleport で代える（§5） |

  開発時に Planner が書く宣言・設定は本スライスで増やさない。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: Planner が宣言 `spring-planner-whitebox` を 1 つ選んで Play したとき、Content Directory の中身だけで、Environment・Cell Lighting・Cell VFX を 1 つも載せずに、Whitebox の Cell (4,2) で local Event と major Event が起動し、回収されるか。
- 進める最低条件:
  - 不変条件（起きてはいけないこと）:
    - **I1（Planner が読まないものを読まない）:** Play の開始から終了まで、Environment・Cell Lighting・Cell VFX の Scene（`Spring_Environment_*`・`Spring_Lighting_{x}_{y}`・`Spring_VFX_{x}_{y}`）が 1 つも Unity の loaded scene に現れない。
      - 主張を崩しうる既存コードの経路: (a) bridge が `world:cellCompanionSet` を置かない・既定 `Full` が勝つ（`PurposeLaunchSettings` の key 写し、app-config の層の順序、コマンドラインの同じ key）。(b) Editor で開いている source の Scene が名前で再利用される（`RegisterAlreadyLoadedScenes`、`PerformUnitySceneLoad` の `GetSceneByName`。検証 Play の開始前検査が塞ぐ）。(c) directory に無い Scene を Addressables / source から読む（directory mode の封鎖が塞ぐ）。(d) Season の `NecessaryAlways` の子の誤分類で Cell の companion が Season 経由で載る（`GameSceneFactory` の分類）。
      - 探索した境界: 上の経路は BS5a / BS5b-2 の HANDOFF と Architecture §04 §4.6〜§4.8・§18 §4 の記述と、`SeasonSceneTagPolicy`・`SampleGameBuildPurposes`・`SessionCellCompanionLoadDriver` の読みまで。companion driver の内部の分岐は d5a までのテストに依る。
      - 未確認: directory mode で companion driver が Planner の set を受け取る経路を実 Play で通したことは無い（d5a の AC10 は test の factory に Planner を直接渡した）。
    - **I2（Content Directory の外から読まない）:** Play の間、Addressables・source から content を読まない。起動ログに封鎖の 1 行があり、`ContentDeclarationMismatch` の Warning が無く、`SealedAssetBackend` の `InvalidOperationException` を含む Error が無い。
      - 経路: (a) 付随 asset `.vfx` を Addressables mode の読み口で引く（directory mode では `LoadContentAssetAsync` に行く。§18 §4）。(b) 開いている source の Scene の再利用（I1 (b) と同じ）。(c) Delivery 窓の手で戻す ledger が残っていて別の directory を読む（検証 Play が拒否する）。
      - 探索した境界: 同上。未確認: 付随 asset を実 Scene から directory で読むのは BS5a の fixture（`AttachedAssetDirectoryIntegrationTests`）だけで、本番の `Spring_Events_4_2:Burst`・`Spring_Event_MultiCellProof:Burst` を directory から読んだことは無い。
  - 成立条件（起きなければならないこと）:
    - **E1（1 操作で Planner の Whitebox Play に入る）:** untitled の空 Scene から検証 Play `spring-planner-whitebox` の 1 操作で、build・publish・install が終わり、起動ログの config に `world:cellCompanionSet=Planner` と表現 `Whitebox` が出て、`Title` が Stable になる。
    - **E2（Spring に入る）:** Title の Start から Home を経て `InGameSession` に入り、Spring `(0,4)` が Stable になり、`Player ready at Cell stream spawn` が出る。
    - **E3（Whitebox の Cell と Events）:** rig を Cell (4,2) へ移すと、`Spring_Cell_4_2` が Whitebox の payload で載り（directory に Whitebox の entry しか無いことで確かめる）、`Spring_Events_4_2` が companion driver によって載る。
    - **E4（local Event）:** local VFX の trigger に rig が入ると、`Spring_Events_4_2:Burst` の source を持つ `VisualEffect` が 1 つ新しく現れて有効になる。rig を `(0,4)` へ戻して Cell (4,2) が unload された後、その個体は破棄されている。rig の退出で `Exit` が消え切りの Stop をかけるので、破棄が退出と Cell の unload のどちらによるかは区別しない（unload による回収は d4b の `LocalVfxEventPlayTests` が持つ。本スライスは「directory の上でも個体が残らない」までを見る）。
    - **E5（major Event）:** 開始 trigger に rig が入ると `Spring_Event_MultiCellProof` が Add されて Stable になり、Event Scene の 2 つの `MajorEventVfxAnchor` に対応する `Spring_Event_MultiCellProof:Burst` の source の `VisualEffect` が 2 つ新しく現れて有効になる。完了 trigger に rig が入ると Event の Scene が unload され、その 2 つの個体は破棄されている。
    - **E6（S-4 の完了）:** program の S-4d 固有の契約が §4 の harvest 表どおり Architecture 文書と Streaming 現状仕様にあり、program へのリンクが残らず、`pwsh tools/docs-audit.ps1` が通る。program と本 HANDOFF の削除は Phase D。
- 受け入れ条件:

| AC | 内容 | 条件 | 証拠 |
|---|---|---|---|
| AC1 | 検証 Play `spring-planner-whitebox` を untitled の空 Scene（Delivery 窓の ledger 無し、Variant profile 未選択）から 1 回実行し、`artifacts/bs2b/transport/<identity>` が出現し、Play mode に入り、`Title` が Stable。起動ログに封鎖の 1 行がある。Play 中の Editor process の環境変数が `SAMPLEGAME_CONTENT__REPRESENTATION=Whitebox` と `SAMPLEGAME_WORLD__CELLCOMPANIONSET=Planner`、`transport.json` の `declaration` の名前が `spring-planner-whitebox`（directory mode は `assets:sceneVariant` を置かず、ログにも config の key は出ない。A2 R3） | E1・I2 | 操作（判定 C）、Editor.log の区間、`eval` の環境変数 |
| AC2 | AC1 の Play で Title の Start を発火し、`InGameSession` と Spring `(0,4)` が Stable、`Player ready at Cell stream spawn` が出る。rig の `Rigidbody.position` が `WorldCellCatalog.SpawnPosition()` から 1 m 以内（ログはカタログの座標を出すだけなので位置は別に読む。原点に留まる既知の観測をここで確かめる）| E2 | 操作、ログと `eval` の位置 |
| AC3 | この検証 Play の build が出した `preflight.json` の `requirements[]` で `Spring_Cell_4_2` が representation `Whitebox` の 1 件だけで、`Full` の候補が `excluded` に `RepresentationNotPreferred` で入っていることを記録する。これは build の**選択**が Whitebox だけだったことの証拠で、同じ identity の `outcome.json` の成功、その directory の install、起動ログの identity（AC1）と組み合わせて「Play が読んだ directory に Full の `Spring_Cell_4_2` は無い」とする（bundle からロードした Scene の表現の観測には頼らない。A2 R4）。rig を Cell (4,2) の中心の上空（trigger の外）へ動かし、`Spring_Cell_4_2` と `Spring_Events_4_2` が loaded になる（上限 60 秒） | E3 | Play 前の content root の記録、`sceneLoaded` の記録 |
| AC4 | trigger に入れる直前に `VisualEffect` の baseline（§5 の `AllVfx()` の識別子の集合）を取る。rig を local VFX の trigger の外側に置き、collider の中心へ入れて FixedUpdate を待つ。baseline に無い `VisualEffect` のうち、`enabled` で `visualEffectAsset` が `Spring_Events_4_2:Burst` の asset と同名のものが 1 つ現れる（上限 10 秒。実体は `DontDestroyOnLoad` にあるので所属 Scene では絞らない。A2 R6）。その識別子を記録し、rig を `(0,4)` の spawn へ戻し、`Spring_Cell_4_2` の unload が記録された後の次のフレーム以降に、その識別子が `AllVfx()` から消える（破棄済み）（上限 15 秒。消え切り待ちの timeout は 10 秒）。**合格例:** 新しい個体 1 → 破棄済み。**不合格例:** 新しい個体 1 → `enabled` が偽になっただけで識別子が `AllVfx()` に残る、または新しい個体が 2 | E4 | 操作、`eval` の識別子の記録 |
| AC5 | rig を Cell (4,2) へ戻し、Events が載った後、AC4 と同じ baseline と入り方で major Event の開始 trigger に入れる。`Spring_Event_MultiCellProof` が loaded になり（上限 30 秒）、baseline に無い有効な `VisualEffect` で `Spring_Event_MultiCellProof:Burst` の asset と同名のものが 2 つ（Event Scene の anchor は 2 つで、`SeasonMajorEventPlayTests` と同じ数。どの anchor から出たかは見ない。この VFX は `SeasonMajorEventScene.OnStabledImpl` で起動するので、現れたことが Event の Stable の証拠を兼ねる）。完了 trigger に同じ入り方で入れ、`Spring_Event_MultiCellProof` の unload が記録された後の次のフレーム以降に、その 2 つの識別子が `AllVfx()` から消える（上限 30 秒） | E5 | 操作、`eval` の識別子の記録 |
| AC6 | I1 を 2 つの証拠の組で見る。(a) **content:** この build の `preflight.json` の `requirements[]` に `Spring_Environment_`・`Spring_Lighting_{x}_{y}`・`Spring_VFX_{x}_{y}` の logical key が 0 件（directory に入っておらず、封鎖の下では他から読めない）。(b) **実行:** Play に入った後、Title の Start を発火する**前**に、`eval` で `SceneManager.sceneLoaded` / `sceneUnloaded` に、名前を `[d5b-scene] loaded|unloaded <name>` の 1 行で Console へ出す handler を登録し、登録した時点の loaded scene も同じ形で 1 回出す（標本の間に載って消える Scene を落とさない。A2 R1）。登録より前の区間（Play の開始から Title の Stable まで）は `UICommon`・OutGame・Title の bootstrap だけで（Home へ進むのは Start の後） World の Scene をロードしないので、その区間は (a) と AC7 (a) で覆う。登録から Play の停止までの `[d5b-scene] loaded` に上の名前が 0 件（`Spring_Lighting`・`Spring_AtmosphereVFX` は Season の子で、あってよい）。**監視が動いていた証拠を必須にする:** 登録の完了の 1 行と、同じ handler が `Spring_Cell_4_2` と `Spring_Events_4_2` の `loaded` と、rig を `(0,4)` へ戻した後の `Spring_Cell_4_2` の `unloaded` を拾った行が区間にあること。無ければ I1 を PASS にせず `観測不足` とする。**失敗経路:** 照合（名前の一覧に対する検査）に `Spring_Environment_4_2` を含む一覧を与えて検出することを確かめる（検証 Play を走らせる必要はない）| I1 | `preflight.json`、操作、Editor.log の区間 |
| AC7 | AC1〜AC5 の Play の区間で、(a) 検証 Play を始める直前に記録した Editor の loaded scene が untitled の空 Scene 1 つだけ（開いたままの source の Scene は、SceneResourceMap に無い名前なら開始前検査を通り、Play の間 source のまま残る。A2 R2。bundle からロードした Scene の `Scene.path` の値には頼らない）、(b) Console に `ContentDeclarationMismatch` の Warning、`InvalidOperationException`（封鎖による拒否）、付随 asset のロード失敗の Error（major は `Major event VFX source failed`・`Major event VFX source loaded nothing`、local は `Local VFX event trigger failed to load its source`・`Local VFX event trigger loaded no source`）、その他の App の Error が 0。`eval` の 5 秒 timeout による `Failed to handle /api/exec request` の行は App の Error と区別して数える（BS5b-2 の読み）。**失敗経路:** 照合に、untitled 以外の Scene を含む開始前の一覧と、封鎖の拒否の行・付随 asset のロード失敗の行を与えて検出することを確かめる | I2 | 操作、Editor.log の区間 |
| AC8 | §4 の harvest 表の各行が移り先にあり、`git grep S-4_FULL_SPEC_WORLD_AUTHORING -- docs unity tools AGENTS.md` が program 自身と本 HANDOFF の外で 0 件（Phase D の削除の後は program 自身も無い）、`pwsh tools/docs-audit.ps1` が成功。Phase B の implementation head と、Phase D の削除・リンク変更の後の最終 head の両方で確かめる | E6 | 文書の差分、コマンド出力 |

- ここでは答えない問いと所有する後続スライス:
  - flight rig が directory mode で spawn に置かれない観測の原因と修正: AC2 で再現した場合、§4.2 の停止に従う。原因の切り分けと修正は所有スライス未定（Architecture §18 §4「検証の残り」）。
  - 実機（BS4 Player）で Spring に入って Event を見る経路: S-9（§24 §16.7）。
  - Planner Play の自動回帰（Content Directory の build を含む Play テスト）: 所有スライス未定。要るようになった時に Phase A で決める。
  - Architecture §24 §16.7・§18 §4 の送り事項: それぞれの記載先のまま。
  - S-5 の Tunnel 遷移と Season 排他 controller の公開面: S-5。
- スライス分割の判断: 契機あり（Planner の実証と program の harvest は変更理由が違う）。**分割しない。** harvest は program §12 が d5b の Phase D に置いた作業で、実証の結果（Planner が動いたこと）を持って program を閉じるので、別スライスにすると「S-4 の完了」の判定が 2 つに割れる。harvest の文書量は §4 の表で見積もり、実装の量は 0 なので、1 スライスに収まる。
- 判定定義: GO は AC1〜AC8 が全て成り立つこと。NO-GO はいずれかが成り立たないこと。CONDITIONAL ACCEPT は使わない。NO-GO で閉じる場合の扱いは §4.2 の (b) に限る。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし
- 本文へ転記した実装制約:
  - **Phase A の判断基準（発注者、2026-10-27。program §0 から転記）。** 主目的は BuildSystem で、VFX（S-4d）はそのつなぎ込みの題材である。各案で、スライスの成果物が content build・Player build の経路に乗るかと、Build 側の既知の課題を踏むかを書く。Build 側の課題は「やらない」で片付けず、BuildSystem の作業の候補として扱う。残すコードは SampleGame に必要なものだけ。生成や authoring の道具は一時スクリプトで作り commit の前に消す案を既定にする。Player を build するスライスは checkout のルートを 96 文字以下にする（本スライスは Player を build しない）。
  - **program §6.2（転記）。** Planner Play は `content:representation=Whitebox` と `world:cellCompanionSet=Planner` を使い、実証対象の Events / VFX を含めた revision で、Environment をロードしない状態の local / major event 両方を実証する。
  - **program §3.2（転記）。** `world:cellCompanionSet` の `Planner` は Events のみ。不明な値は起動を失敗させる。companion の role 判定は SampleGame に閉じ、Framework は Environment / Lighting / VFX / Events を知らない。
  - **program §2.2（転記）。** VFX Scene にゲーム進行の trigger を置かない。Events Scene が VFX の開始・停止を要求する。Scene を跨ぐ `GameObject` の直接参照は禁止。
  - `unity test` / `unity run`、Scene / asset YAML の手編集は禁止。Unity の操作は `tools/unity-editor.cmd`（`osm-unity-editor` Skill）。
  - 常時契約: `#nullable enable`、Unity の偽 null、`record` 禁止、テストで `Task.Delay` / `Thread.Sleep` を使わない（本スライスはコードを足さない見込み）。
- 未決事項: なし（A2 で出たものは §5.1）。凍結の承認時に発注者が合意するのは §4.2（AC2 が落ちた場合の診断の続行、製品の欠陥なら停止して (a) / (b) を裁定、(b) の閉じ方）だけ。

### 2.1 Phase A の設計判断

- **D1（実証の経路は検証 Play）。** program §6.3 の d5b の行は「今日と同じ手順の source の Editor Play で行い、Play As（BS5b）を待たない」とした。この一文は BS5b が無い時点の迂回で、BS5b-2 が完了した今は、宣言 1 つから Content Directory を作って Play する検証 Play が「Content Directory の Whitebox の上で」を最も直接に満たす。主目的の BuildSystem の成果（BS5a の宣言と付随 asset、BS5b-1 の `Full` build と publish、BS5b-2 の検証 Play）をそのまま受け入れの経路に使える。Play As は Addressables mode なので問いに答えない。Delivery 窓の手動 publish・install は宣言との照合を通らないので使わない。
- **D2（製品コードと自動テストを足さない）。** Event の振る舞いと Planner の companion set は d4b / d5a のテストで固定済みで、本スライスで新しく確かめるのは「Content Directory の中身だけで同じものが動くか」である。これを自動テストにすると、Content Directory の build（数分）と Title から Spring までの遷移を Play テストに持ち込むことになり、問いに対して重い。判定 C の操作で 1 回確かめる。Planner Play の自動回帰は後続へ送る。`eval` の手順は HANDOFF の本文に書き、使い捨てにする（判断基準の「残すコードは SampleGame に必要なものだけ」）。
- **D3（観測は Unity の loaded scene と `VisualEffect` の一覧とログ）。** SceneDirector の内部状態を `eval` で読まず、Unity の `SceneManager` の loaded scene（名前と path）と、`Object.FindObjectsByType<VisualEffect>` の有効なもの（`visualEffectAsset` の名前）と、Editor.log の区間を観測にする。Scene が SceneDirector に載っていても Unity の Scene が無いことは無いので、I1 の「現れない」はこれで観測できる。
- **D4（harvest は Phase B で書き、削除は Phase D）。** 実装が無いので、Phase B は §4 の harvest 表どおりに Architecture 文書と Streaming 現状仕様を更新し、program へのリンクを向け直す（program 本体と本 HANDOFF は残す）。C / C' は harvest の差分もレビューする。Phase D は実証の結果を §05 §5.13 等へ 1〜2 文で足し、program と本 HANDOFF を削除する。

## 3. 責務マップ

製品コード・テストコードの変更は無い。変更するのは文書だけ。

| ファイル | 責務 | 変更理由 | 現在行数 | 予想増分 |
|---|---|---|---|---|
| `unity/Assets/Docs/Architecture/05-scene.md` | Scene の分類と寿命の現況 | §2.1 SceneGraph・§2.3 Event の粒度・§2.2 職種の所有物（未収録の分）の移設 | 確認は B | +20〜40 |
| `unity/Assets/Docs/Architecture/24-rendering-system.md` | VFX 抽象と SampleGame backend の現況 | §2.4 VFX の粒度と寿命、§6.1 の PC 必須・Mobile 安全停止・ParticleSystem fallback の延期、§6.4 の未収録分の移設。§16.7 の「d1〜d5a では答えず」を「S-4d では答えず」に直す | 確認は B | +10〜25 |
| `unity/Assets/Docs/Architecture/27-folder-structure.md` | 物理フォルダ | §2.5 の未収録分（Events/ の major Event）の確認と移設、§3.4 の Role ごとに開く Scene の組の移設 | 確認は B | 0〜10 |
| `unity/Assets/Docs/Architecture/18-asset-description.md` | Variant と BuildSystem | §3.1 の「Editor Play 向け publish 入口は未整備」等、BS5b-2 で古くなった program の記述を移さない確認。必要なら 1 行 | 確認は B | 0〜5 |
| `docs/streaming/STREAMING_CURRENT_SPEC.md` | Streaming 現状 | S-4 の Scene 件数（658）と Season の子の構成の確認と移設 | 確認は B | 0〜10 |
| `docs/handoff/BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` | BuildSystem program | §0 の判断基準の主旨が無ければ 1〜3 文 | 確認は B | 0〜3 |
| `docs/README.md` | 文書の索引 | program の行を削除予定（Phase D）に更新し、移り先を示す | 確認は B | ±3 |
| `docs/handoff/SEASON_WORLD_DESIGN.md` | 世界構図の program | program へのリンク 3 箇所を移り先へ向け直す | 確認は B | ±3 |

現在行数（`fb97da0d`、`wc -l`）: `05-scene.md` 717、`24-rendering-system.md` 552、`27-folder-structure.md` 260、`18-asset-description.md` 352、`STREAMING_CURRENT_SPEC.md` 119、`BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` 242、`docs/README.md` 77、`SEASON_WORLD_DESIGN.md` 343。

行数警報: `05-scene.md` と `24-rendering-system.md` は既に 500 行を超えている。**非分割。** どちらも責務ごとの正本（Scene の分類と寿命、VFX の現況）で、本スライスはその責務の内側へ program の契約を移すだけで、新しい変更理由を持ち込まない。増分は各 40 行以下で 50% の警報にも当たらない。文書の分割は本スライスの問いに無関係なので行わない。

## 4. 実装計画

- 変更対象: §3 の文書だけ。harvest 表:

| program の節 | 移り先 | 扱い |
|---|---|---|
| §1 確定事項（四季・216 Cell・Spring 起動・Variant・Lighting の分割・VFX Graph の PC 必須 / Mobile 安全停止） | §05 §5.13、§24 §16.6 | 既に収録のものは移さず、無いものだけ 1 行ずつ |
| §0 Phase A の判断基準（主目的は BuildSystem、残すコードは SampleGame に要るものだけ、checkout のルート） | `docs/handoff/BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md` | S-4 に限らない発注者の方針なので、BuildSystem program に同じ主旨が無ければ 1〜3 文で足す。S-4 の着手手順（§0 の 1〜6）は移さない |
| §2.1 SceneGraph（四季の木）、§2.2 職種の所有物 | §05 §5.13 | §05 の既存の図（Main / OutGame / InGame の例）は四季の木ではないので、四季の木と職種ごとの所有（Cell は地形・衝突・距離の境界、Environment は set dressing、VFX Scene に進行の trigger を置かない、Scene を跨ぐ `GameObject` の参照の禁止）を移す（A2 R7） |
| §2.3 Event Scene の粒度 | §05 §5.13 | 規則（4 条件）を移す |
| §2.4 VFX の粒度と寿命 | §24 §16 | 規則を移す |
| §2.5 物理フォルダ | §27 | `Events/` の major Event の例が無ければ足す |
| §3.4 World Workspace の Role ごとに開く Scene の組 | §27 §6 | 無いので移す（Planner は Whitebox Cell と任意の Events。`WorldWorkspaceSelection` が正。A2 R8） |
| §3 の他（S-4a の要約） | 既に §04 §4.8 / §05 / §18 / §20 / §21 / §27 | 移さない。§3.1 の「Editor Play 向け publish 入口は未整備」は BS5b-2 で古いので移さない |
| §4・§5（S-4b・S-4c の記録） | 既に harvest 済み | 移さない |
| §6.1 VFX Graph の版・PC 必須・ParticleSystem fallback の延期 | §24 §16.6 | 無いものだけ。ParticleSystem fallback の延期（要否・契約・実装は後続の独立スライス、担当未定）は §24 に無いので必ず移す（§16.6 の能力判定を既収録と扱わない） |
| §6.2 実証 Scene と 658 件 | §05 §5.13 または Streaming 現状仕様 | 件数と 6 件の名前 |
| §6.3 分割表 | 移さない | 経緯。PR と git 履歴で辿れる |
| §6.4 VFX の責務配置 | §24 §16 | 既に収録。無いものだけ |
| §10 公開 API と依存 | §24 §16.1 | 既に収録の確認だけ |
| §11 検証方針、§12 境界 | 移さない | 完了済みスライスの手順 |

- 順序: (1) Phase B が §4 の harvest を書き、`pwsh tools/docs-audit.ps1` と `pwsh tools/contract-audit.ps1` を通し、commit する（implementation head）。(2) 判定 C が §5 の検証経路を 1 回実行し、harvest の差分を照合する。(3) C'。(4) Phase D が実証の結果を 1〜2 文足し、program と本 HANDOFF を削除する。
- Phase B から Phase A へ差し戻す条件: 製品コード・テスト・公開 API・asmdef・Scene / Prefab / Addressables の変更が必要になった場合。harvest で移り先の文書の既存の契約と program の記述が矛盾した場合（どちらを正とするかは Phase A の判断）。
- **§4.2 判定 C で経路が成り立たない場合（A3 で合意する）。**
  - **分類:** AC1〜AC5 のどこかで期待と違えば、まず `phases-and-handoff.md` の「新しい事実の分類」に従い「基盤不備（`eval` の手順の誤り）」「観測不足」「製品の欠陥」を区別して記録する。手順の誤りは判定 C の担当が直して再実行してよい（1 回まで）。
  - **AC2 が落ちた場合の診断の続行:** AC2 の失敗（例: rig が原点に留まる既知の観測）は FAIL として記録し、GO にしない。ただし `InGameSession` と Spring `(0,4)` が Stable で、rig の `Rigidbody` を上の動かし方で置き直せるなら、置き直した事実を記録したうえで AC3〜AC7 を**診断として**続け、各 AC を `PASS` / `FAIL` / `NOT RUN` で分けて記録する。1 回の検証 Play から、Whitebox と Event の可否の証拠を取りこぼさないためで、例外の GO ではない。置き直せない場合（World が Stable にならない等）は残りを `NOT RUN` として停止する。
  - **製品の欠陥だった場合:** 本スライスで直さずに停止し、発注者に (a) 修正を入れる Phase A の新しい revision、(b) 欠陥を後続スライスへ送り、d5b を NO-GO で閉じる、のどちらかを裁定してもらう。
  - **(b) の閉じ方:** 判定 C の結論は **NO-GO（Planner 実証未達）**と記録し、「S-4 の目的を達成した完了」とは書かない。program の削除は、Phase D で次の 4 点を引き渡し先の文書（BuildSystem program の該当系列、または Architecture §18 §4「BuildSystem の後続」）に書いた後に限る: 引き取る後続（文書名。担当が未定なら「未定」と書き発注者が承認する）、未達の AC、観測した欠陥と一次証拠の所在、再検証の条件（同じ AC1〜AC7 をどの head で走らせるか）。§05 §5.13 には「Content Directory の Planner Play は未実証」と 1 文書く。引き渡し先を書けない、または発注者が未達のまま閉じることを承認しない場合は program を削除しない。
- 対象外を維持する方法: Phase B の差分が `docs/` と `unity/Assets/Docs/` だけであることを C が `git diff --stat` で確かめる。
- **BuildSystem の既知の課題との関係（判断基準による記録）。** 本スライスは content build（BS5b-1 の `Full`）と検証 Play（BS5b-2）の経路に乗る。BS4 Player の課題（Spring に入らない）は踏まない（Player を build しない）。transport staging の path 予算は、検証 Play の publish 先が BS5b-2 で使った `artifacts/bs2b/transport/<identity>` で、この worktree のルート（`D:\repositories\unity\osm-s4d5`、29 文字）で BS5b-2 と同じ。directory mode で rig が原点に留まる観測（Build 側の課題）は AC2 で踏む可能性があり、踏んだら §4.2 に従い BuildSystem の作業の候補として送る。

## 5. テストとレビュー計画

- 単体テスト: なし（製品コードを足さない）。
- 差し戻し中の起点 `-Filter`: なし（文書だけの変更）。
- 判定必須テスト: Unity テストは無し。`pwsh tools/docs-audit.ps1`、`pwsh tools/contract-audit.ps1`、§5 の検証経路（AC1〜AC7）、AC8 の grep。
- 全 EditMode 回帰の適用除外: 適用除外する。理由: implementation の差分は文書だけで、Unity が読む C#・asset・設定を変えない。代替証拠: `git diff --stat <base> <head>` が `docs/` と `unity/Assets/Docs/` の `.md` だけであること、`contract-audit` の成功。AC1〜AC7 の検証 Play は `fb97da0d` の製品をそのまま動かす。
- 統合・Unity テスト: なし。
- 操作・実行時・目視条件の検証経路（AC1〜AC7。担当は判定 C の実行担当。動作確認は `対象なし`: 全ての条件が content build を含む検証 Play の上にあり、Build を動作確認で実行しないため）:
  - **環境・初期状態:** GO 候補 head の worktree（ルート 96 文字以下）。Editor を `tools/unity-editor.cmd` で起動し、untitled の空 Scene だけを開く。Delivery 窓の ledger 無し、Variant profile 未選択、Addressables の Play Mode は `BuildScriptFastMode` 以外。Editor.log の現在の末尾の行番号を区間の開始として記録する。
  - **観測の関数（`eval` で毎回同じ式を使う）:** `Loaded()` = `SceneManager.sceneCount` の各 Scene の `name` の一覧。`AllVfx()` = `Object.FindObjectsByType<UnityEngine.VFX.VisualEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None)` の各個体の識別子（`EntityId.ToULong(GetEntityId())`。`GetInstanceID` は obsolete-as-error）・`enabled`・`visualEffectAsset?.name`。baseline と破棄の判定に使う。`EnabledVfx()` = `AllVfx()` のうち `enabled` のもの。起動の判定に使う。実体は `DontDestroyOnLoad` にあるので所属 Scene では絞らない。`Rig()` = `FlyController` の go の `Rigidbody.position`。
  - **個体の追い方（AC4・AC5）:** `eval` の呼び出しをまたいで Unity の参照は持てないので、識別子で追う。起動の判定で baseline に無い識別子を記録し、以後の `eval` で `AllVfx()` にその識別子が 1 つも無くなったことを破棄とする（無効化だけなら `Include` の列挙に残る）。識別子・時刻・`enabled` を毎回生ログに残す。最初の 1 回は、識別子で同じ個体を 2 回続けて引けることを確かめてから使う。引けなければ AC4・AC5 は `観測不足`。
  - **標本の間隔:** local の VFX は、rig が trigger の中にいる間は handle が生きていて個体が残る（`Exit` が来るまで Stop しない）ので、起動を 1 秒ごとの標本で取り逃がさない。期待の asset 名は `Spring_Events_4_2` と `Spring_Event_MultiCellProof` の `AttachedAssetDescription` の `Burst` が指す asset を Edit Mode で読んで、Play の前に記録する。
  - **AC1:** 開始前の `Loaded()` を記録する（AC7 (a)）。`VerifyPlayLauncher` と `SampleGameBuildPurposes` は `internal` / Editor asmdef の型なので、`eval` から reflection で `VerifyPlayLauncher.VerifyAsync(SampleGameBuildPurposes.SpringPlannerWhitebox, interactive: false)` を呼ぶ（dialog の無い入口）。build は数分かかり `eval` は timeout するが処理は続く。`artifacts/bs2b/transport/` に新しい identity が出て `EditorApplication.isPlaying` が真になるのを待つ。Play に入ったら直ちに AC6 の handler を登録する。`Title` が loaded になるのを待ち、Editor.log の区間で封鎖の 1 行を探し、`Environment.GetEnvironmentVariable` で 2 つの環境変数を読む。この build の report（`artifacts/bs2b/reports/<identity>/preflight.json`。identity は transport と同じ形式の名前で、違えば build のログが出す report の path を使う）を保存し、AC3 と AC6 (a) の照合に使う。
  - **AC2:** `eval` で Play 中の `TitleView` を `Object.FindFirstObjectByType` で得て、`OnStartRequested` の delegate を reflection で呼ぶ（Start ボタンの click と同じ handler）。`InGameSession`・`Spring_Cell_0_4` が loaded になり、Editor.log に `Player ready at Cell stream spawn` が出るのを待つ（上限 120 秒）。`Rig()` と `WorldCellCatalog.SpawnPosition()` の距離を記録する。
  - **rig の動かし方（AC3〜AC5）:** `FlyController.Teleport` は `transform.position` を置き速度を 0 にするだけで、Rigidbody の位置を直接は置かない（`AutoSyncTransforms` は無効で、物理への反映は次の simulation の前の同期に任せる）。trigger の進入がこれで起きるかは確かめていないので、判定には使わない。大きな移動は `Rigidbody.position` と `transform.position` を目的地に置き、速度を 0 にして FixedUpdate を 1 回待つ。trigger へは、collider の外側の点に同じやり方で置いた後、`Rigidbody.MovePosition(collider.bounds.center)` を呼び FixedUpdate を 2 回以上待つ（既存の `LocalVfxEventPlayTests` と同じ入り方。ただし実機の rig は非 kinematic で、`FlyController.FixedUpdate` が毎回 `linearVelocity` を書くので、テストの probe と同じ動きとは仮定しない）。キー入力は送らない（入力が無ければ `FixedUpdate` は速度を書き換えるだけで rig は動かない）。各移動の後に `Rig()` が目的地から 1 m 以内であることを記録する。待ちは `eval` を繰り返して観測するだけで、コードに sleep を入れない。Cell (4,2) の中心は `Spring_Cell_4_2` の `SceneResource.Volume` の中心（SceneResourceMap から Edit Mode で読んで Play の前に記録する）の上空。trigger の位置は Play 中に `LocalVfxEventTrigger` / `MajorEventStartTrigger` / `MajorEventCompletionTrigger` の `Collider.bounds` を読む。`(0,4)` へ戻すと、Cell (4,2) は `UnloadRadius`（550 m）の外（中心間約 1118 m）になり unload される。
  - **AC3〜AC5:** 各手順の後に `Loaded()`・`Vfx()`・`Rig()` を記録し、AC の上限まで 1 秒ごとに取り直す。rig の位置が目的地から外れていたら（物理で戻された等）、その観測を記録して止まる（§4.2 の分類へ）。
  - **AC6・AC7:** Play を止めた後、区間の Editor.log を保存し、`[d5b-scene]` の行の名前の照合と AC7 の文字列の数え上げを行う。照合は使い捨ての式 1 つにし、本番の入力・負例（`Spring_Environment_4_2` を含む一覧、封鎖の拒否の行、付随 asset のロード失敗の行、untitled 以外を含む開始前の一覧）・許可の正例（`Spring_Lighting` と `Spring_AtmosphereVFX` だけを含む一覧）を同じ式に与え、式・入力・出力を 1 つの記録に残す。
  - **Stable の読み:** `Stable` と書く主張は、既存のログか振る舞いで確かめるものに限る。`Title` は起動ログの `stable=True`、World は `Player ready at Cell stream spawn`（`WaitUntilWorldReady` の後に出る）、major Event は AC5 の VFX の出現。それ以外の Scene は Unity の `loaded` の観測と書き、Stable とは書かない。
  - **1 回の試行への結びつけ:** 記録の先頭に、build identity、GO 候補 head、publish の path、`transport.json` の SHA-256、Editor.log の区間（開始と終了の行番号）、時刻を書く。`preflight.json`・`outcome.json`（どちらも `artifacts/bs2b/reports/<identity>/`）・`transport.json`・install のログ・起動ログの `identity=` が同じ identity であることを照合する。合わなければ全ての AC を `観測不足` とする。
  - **保存する証拠:** Editor.log の区間、開始前の `Loaded()`、`Vfx()`・`Rig()` の時刻つきの記録、環境変数の値、`preflight.json`、AC4・AC5 の baseline と新しい個体の識別子の記録、`artifacts/bs2b/transport/<identity>/transport.json` の宣言の欄。実行した `eval` の式もそのまま残す。保存先は判定 C の担当が HANDOFF §7 に書く（Artifact Storage は使わない。発注者の指示）。**C' への渡し方:** 判定 C の所見・合否の表を除いた生の証拠だけ（A3 凍結版、Phase B の差分、`eval` の式、Editor.log の区間、`preflight.json` / `outcome.json` / `transport.json`、identity の照合、照合の式と入出力、各 file の SHA-256、取れなかった観測の一覧）を渡し、C' は AC ごとに生の証拠から判定し直して記録する。
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点: **未確認。初回確認は Phase C（判定 C の実行担当、AC1 の実行の時点）。** 理由: 経路の全ての手順が content build を含む検証 Play の上にあり、Phase A では Build を行わない。未知は (1) `TitleView.OnStartRequested` を reflection で呼んで Home → InGame へ進むか（BS5a の AC13 で Start の event を手で発火して進めた前例がある）、(2) directory mode で rig が spawn に置かれ Focus が登録されるか（原点に留まる既知の観測）、(3) `Rigidbody.MovePosition` で入れた rig が trigger の `OnTriggerEnter` を起こすか（`Teleport` では起きないことはコードの読みで分かったので使わない。A2 R5）、の 3 つ。不成立のときの対応は §4.2（A3 で合意）。
- 人間の判断が必要な条件: なし（見た目の判断を求めない。VFX の描画の見た目は d3 で確認済み）。
- 高コスト証拠: なし（検証 Play の content build は数分で、取り直しできる）。
- 判定 C の照合 script の方式: なし（照合は名前の一覧と Editor.log の文字列の数え上げで、判定 C がその場で行う）。
- 機械検査: `pwsh tools/docs-audit.ps1`、`pwsh tools/contract-audit.ps1`。
- A0/A1 主担当・モデル・ベンダー: §5.1 に実績を書く。
- A2 独立レビュー: §5.1。
- A3 統合担当・モデル・採否: §5.1。
- C' 用に予約した担当・モデル・ベンダー: §5.1。
- 独立性の強化条件を満たせない場合の理由: risk `normal` なので強化条件は必須ではない。

### 5.1 Phase A のレビューの記録

- **A0/A1 主担当:** Claude Code（Anthropic）。
- **A2 入力版:** 未 commit の版（発注者の指示で Phase A の commit を切っていない）。HANDOFF の内容 sha256 `e30f920bd6cc155e5072e9d84ab55548449863c5069b7b1eb2360beba453bb9e`。Artifact Storage の snapshot は使っていない（発注者の指示）。
- **A2 (1) 目的レビュー:** Anthropic の別 session（目的・利用者の 2 欄の原文だけを渡し、repository を読ませない）。入力は 2 欄の原文、sha256 `1f33e0c778b7e416e952a071639377702f7f9a34ddada4dae85513ddd7845561`。
- **A2 (2) アーキテクチャゲートと不変条件の破り方・検証経路の実行可能性・harvest 表:** xAI（Grok、cursor-agent）。入力は上の HANDOFF と repository（`fb97da0d`）。計画の枠（§3・§4 は実装済みと仮定）を指示に固定した。OpenAI（Codex）で始めたが利用上限で応答が無く、Grok に替えた。
- **A3 統合:** 主担当が分類した。発注者が追加レビュー 2 回を依頼し、その採否を反映した第 3 版を 2026-11-03 に凍結承認した（§4.2 の合意を含む）。

**A3 の採否**

| # | 出所 | 指摘 | 採否 | 理由と反映 |
|---|---|---|---|---|
| R1 | (2) I1 | 5 秒ごとの標本の間に Environment が載って消えると AC6 が見落とす | 採用 | AC6 を `sceneLoaded` / `sceneUnloaded` の全件のログに替えた |
| R2 | (2) I2 | SceneResourceMap に無い名前の source の Scene を開いたまま検証 Play すると、開始前検査を通り source のまま残る | 採用 | AC7 (a) に開始前の loaded scene が untitled 1 つだけであることを足した（Grok の案「Play 中の Scene の path が空」は、bundle の Scene の `path` の値に依るので採らない） |
| R3 | (2) 経路 | AC1 の `assets:sceneVariant=Whitebox` はログに出ず directory mode では置かれない | 採用 | 環境変数 `SAMPLEGAME_CONTENT__REPRESENTATION` / `SAMPLEGAME_WORLD__CELLCOMPANIONSET` と `transport.json` の宣言を読む形に直した |
| R4 | (2) 経路 | bundle からロードした Scene の `Scene.path` は空で Whitebox を観測できず、Full fallback と区別できない | 採用 | AC3 を「directory の content root に `Spring_Cell_4_2` の Whitebox の entry しか無い」の記録に替えた |
| R5 | (2) 経路 | `FlyController.Teleport` は `transform` だけを動かし、`AutoSyncTransforms` 無効で物理に戻され trigger に入らない | 一部採用 | 「物理に戻される」は確かめていない（Unity は simulation の前に transform を同期する）ので本文で言い切らない。trigger の判定に `Teleport` を使わず、`Rigidbody.position` と `MovePosition` + FixedUpdate の待ちに替えた（既存の Play テストと同じ入り方） |
| R6 | (2) 経路 | VFX の実体は `DontDestroyOnLoad` で、所属 Scene で絞ると 0 件と読む | 採用 | `Vfx()` を所属 Scene で絞らない形にし、AC4 に明記した |
| R7 | (2) harvest | §05 の既存の図は四季の木ではなく、四季の木と §2.2 の職種の所有が移らない | 採用 | harvest 表の行を「移す」に直した |
| R8 | (2) harvest | §3.4 の Role ごとに開く Scene の組が §27 に無い | 採用 | harvest 表に行を足した |
| R9 | (2) harvest | program §0 の判断基準が削除で消える | 採用 | BuildSystem program に主旨が無ければ足す行を足した |
| R10 | (2) harvest | ParticleSystem fallback の延期が §24 に無い | 採用 | harvest 表の §6.1 の行に明記した |
| R11 | (2) ゲート | 製品型を足さない選択はフォルダ・依存・所有者・寿命に触れず、常時契約に反しない。問いは観測の修正（R3〜R6）で判定できる | 記録 | 分割は不要との結論も同じ |
| R12 | (1) | 再度 Play しても同じ結果になる（再現性） | 不採用 | 問いは Content Directory の上で動くかで、2 回目の Play が別の経路を通る根拠が無い。検証 Play の build を 2 回にする費用に見合わない |
| R13 | (1) | Play 中の警告も 0 | 不採用 | BS5b-2 で問いと無関係の Warning（audio listener の重複）が出た。問いに関わる Warning（宣言の不一致）だけを AC7 で数える |
| R14 | (1) | Play 終了後に状態・生成物が残らず次の Play に影響しない | 不採用（既存で保証） | 環境と ledger の復元は BS5b-2 の契約。VFX の回収は AC4・AC5 で見る |
| R15 | (1) | 二重発火しない、1 つの Event の失敗が他を止めない | 採用（既存の AC で足りる） | AC4・AC5 は有効な `VisualEffect` が「1 つ」であることを見る。例外の隔離は d1〜d4b の契約で、AC7 の Error 0 で見る |
| R16 | (1) | 閉じた根拠（Play の結果）を文書で辿れ、未了は明記する | 採用（既存） | Phase D で結果を §05 §5.13 等に 1〜2 文足す（D4）。§4.2 (b) の場合は「未実証」と書く |
| R17 | (1) | 作らないもの（追加入力、専用の編集ツール、独自のテスト機構、新しい抽象層、Spring 以外の追加） | 一致 | D2 と対象外の欄と同じ。初稿は宣言 1 つの選択の他に入力を増やしていない |

**A3 統合版への追加レビュー（発注者が依頼した外部レビュー、2026-11-03）と主担当の見直し**

入力は A3 統合版（sha256 `ebf0c3188fba228c946f306bdd1ea2b75d0e4746d8d2521105fee11703e8aa7b`、未 commit の版）。主担当は各指摘を `fb97da0d` のコードで確かめてから採否を決めた。

| # | 出所 | 指摘 | 採否 | 理由と反映 |
|---|---|---|---|---|
| H1 | 外部 P1-1 | major Event の VFX は anchor ごとに起動し、Event Scene の anchor は 2 つ。AC5 の「1 つ」は正しい挙動を不合格にする | 採用 | `Spring_Event_MultiCellProof.unity` に `MajorEventVfxAnchor` が 2 つあることを確かめた。E5・AC5 を 2 つに直した |
| H2 | 外部 P2-1 | 「有効な `VisualEffect` が 0」は無効化と破棄を区別できない | 採用 | baseline の差で新しい個体の参照を持ち、unload の後に `== null` になることを見る形に直した（backend は `Object.Destroy(go)` で破棄する） |
| H3 | 外部 P2-2 | §4.2 (b) の NO-GO と program の削除が「完了」と区別できず、引き取り先が無い | 採用 | (b) を NO-GO と記録し、引き渡しの 4 点を書けて発注者が承認した場合だけ program を削除する形にした |
| H4 | 外部 P2-3 | AC2 で止まると中心の AC3〜AC5 が未測定で終わる | 採用 | AC2 の失敗は FAIL のまま、置き直せるなら AC3〜AC7 を診断として続け `PASS` / `FAIL` / `NOT RUN` で記録する |
| H5 | 外部 P2-4 | handler を Play の後で登録するので、登録前の load / unload を取り逃がす | 採用 | 登録を Title の Start の前に固定し、登録前の区間を `preflight.json` の選択（禁止の Scene が directory に無い）と AC7 (a) で覆う形にした |
| H6 | 外部 P3-1 | 現在行数が空欄、500 行を超える文書の非分割理由が無い | 採用 | §3 に行数と非分割の理由を書いた |
| H7 | 外部 P3-2 | AC7 に local の付随 asset のロード失敗の文言が無い | 採用 | `LocalVfxEventSession` の 2 つの文言を足した |
| H8 | 外部 P3-3 | Phase D の削除の後にも docs-audit を走らせる | 採用 | AC8 に最終 head を足した |
| S1 | 主担当 | local の trigger から出ると `Exit` が消え切りの Stop をかけるので、AC4 の「Cell の unload の後に消える」は unload による回収の証拠にならない | 採用 | E4 を「directory の上でも個体が残らない」までにし、退出と unload のどちらで消えたかは区別しないと明記した（unload による回収は d4b の Play テストの範囲） |
| S2 | 主担当 | A2 の R5 で採った「`Teleport` は物理に戻され trigger に入らない」は確かめていない | 訂正 | 本文の言い切りを外した。判定は `MovePosition` の入り方で行い、各移動の後に rig の位置を記録する |
| S3 | 主担当 | AC3 の「content root を読む」は読む file と方法が決まっていない | 採用 | build の report の `preflight.json` の `requirements[]` / `excluded` を読む形にした（人が読める記録で、AC6 (a) と共用） |

**A3 統合版の第 2 版への最終レビュー（発注者が依頼、2026-11-03）**

入力は第 2 版（sha256 `61b798af688e1ec705b3e9725cd7b797eee823fb2401c92622cbb302a7780fe6`、未 commit の版）。発注者の指示でこれを最後のレビューとし、採否は「指摘が不当でないか」と「計画を不当に大きくしないか」で決めた。採用は全て観測・記録の書き方の変更で、製品コード・自動テスト・常設の道具・AC の追加は無い。

| # | 指摘 | 採否 | 理由と反映 |
|---|---|---|---|
| F1 | AC6 の 0 件は監視が動いていなくても出る | 採用 | 同じ handler が `Spring_Cell_4_2`・`Spring_Events_4_2` の loaded と Cell の unloaded を拾ったことを必須にし、無ければ `観測不足` |
| F2 | `eval` をまたいで同じ個体を追う方法が無い。baseline と起動判定の関数が混ざっている | 採用 | `AllVfx()`（Include、識別子つき）と `EnabledVfx()` を分け、識別子が列挙から消えたことを破棄とした。識別子の再取得を最初に 1 回確かめる |
| F3 | 別の試行の preflight と今回のログを混ぜうる | 採用 | 記録の先頭に identity 等を置き、5 つの記録の identity の一致を照合する。起動ログの `identity=` を使う |
| F4 | Unity の loaded と SceneDirector の Stable の混同 | 採用 | Stable は既存のログ・振る舞いで確かめるものに限り、他は loaded と書く |
| F5 | preflight は選択の証拠で、directory の実体の直接の観測ではない | 採用（文言） | AC3 の主張を「選択」と、outcome・install・起動 identity との組み合わせに正した。entry を直接読む道具は作らない |
| F6 | AC5 の「anchor に 1 つずつ」は同名 2 件では示せない | 主張を弱めて対応 | 位置の照合は問いに要らないので足さず、「2 件、どの anchor かは見ない」と書いた |
| F7 | 短命な VFX を 1 秒の標本で取り逃がしうる | 不採用（根拠を記録） | rig が trigger の中にいる間は handle が生き、個体が残る（`Exit` まで Stop しない）。毎フレームの監視は不要 |
| F8 | 負例を同じ照合の式で評価した記録が要る。`Spring_Lighting` の許可の正例 | 採用 | 式・入力・出力を 1 つの記録に残し、許可の正例を足した |
| F9 | C' に所見を除いた生の証拠を渡し、AC ごとに判定し直させる | 採用 | 渡す一覧を §5 に書いた |

- **C' 用に予約した担当:** risk `normal` なので強化条件は必須ではない。Phase B は文書だけで、C' は B / C と異なる系列を開始時に選ぶ（OpenAI は 2026-11-04 22:42 以降に利用可能。A2 で使った Grok を使う場合は `独立性制約あり` と記録する）。

## 6. Phase B 実装結果

- 担当: Claude Code（Anthropic）。
- 変更は文書だけ（本 HANDOFF を除き 7 file、+72 / −11）。製品コード・テスト・Scene・asset・設定・asmdef は変えていない。`pwsh tools/docs-audit.ps1`（errors 0・warnings 0）と `pwsh tools/contract-audit.ps1`（errors 0・warnings 0）が成功。Editor のコンパイル確認は対象なし（C# の変更が無い）。
- AC8 の grep（`git grep S-4_FULL_SPEC_WORLD_AUTHORING -- docs unity tools AGENTS.md`）は本 HANDOFF の 3 行だけ（program 自身は自分の名前を含まない）。
- harvest の対応（§4 の表の行 → 移り先）:

| program の節 | 移り先 | Phase B の扱い |
|---|---|---|
| §1 確定事項 | §05 §5.13 の「四季の SceneGraph（S-4）」、§24 §16.6 | 無かったもの（同じ AABB の共有、Environment は全 Cell・他の職種は必要な Cell だけ、職種の子は距離の候補にしない、PC 必須・Mobile の安全停止の製品判断）を足した。216 Cell・Spring 起動と spawn・Variant・Lighting の分割と代表 bake・生成器の撤去は §21・§24 §9.1・§04 §4.8・Streaming 現状仕様に既にあるので移していない |
| §0 Phase A の判断基準 | BuildSystem program §7 の冒頭 | 主旨が無かったので 3 文で足した。checkout のルートの長さは数値を書かず path 予算（`tools/path-budget.json`、Harness README）を指した |
| §2.1 SceneGraph、§2.2 職種の所有物 | §05 §5.13 | 四季の木（Tunnel は S-5 と書いた）と職種ごとの所有、VFX Scene に進行の trigger を置かない、Scene を跨ぐ参照の禁止を移した |
| §2.3 Event Scene の粒度 | §05 §5.13 | 4 条件を移した |
| §2.4 VFX の粒度と寿命 | §24 §16.6 | 「VFX の粒度と寿命」の項として移した |
| §2.5 物理フォルダ | §27 §3.3・§3.4 | フォルダ例に `Spring_AtmosphereVFX/`・Cell (4,2) の職種 Scene・`Events/` の major Event を足し、§3.4 の対応に `Spring_Event_{slug}` を足した。Full / Whitebox の同名の規則も §3.3 へ移した |
| §3.4 Role ごとに開く Scene の組 | §27 §6 | 表として移した（`WorldWorkspaceSelection` を読んで一致を確かめた） |
| §3 の他、§3.1 の「Editor Play 向け publish 入口は未整備」 | 移さない | §18 にこの記述が無いことを確かめ、§18 は変えていない |
| §6.1 VFX Graph | §24 §16.6 | 版と asmdef は既収録。PC 必須・Mobile の安全停止の製品判断と ParticleSystem fallback の延期を能力判定の項に足した |
| §6.2 実証 Scene と 658 件 | Streaming 現状仕様の表 | 6 件の名前と 658 件を 1 行で足した（`Seasons/` 配下の `.unity` が 658 件であることを数えて確かめた） |
| §6.4 VFX の責務配置 | §24 §16 の冒頭 | 既収録。無かった理由（CameraSystem との違い、Framework への移設は後で判断）を 1 文で足した |
| §10 公開 API と依存 | §24 §16.1・§16.5 | 既収録を確かめただけ |
| §6.3・§11・§12・§4・§5 | 移さない | 表どおり |

- §24 §16.7 の「d1〜d5a では答えず」を「S-4d では答えず」に直した。
- program へのリンク: `docs/README.md` の program の行をファイル名なしの「S-4 program」にして、残りは d5b、Phase D で削除、移り先を書いた。`SEASON_WORLD_DESIGN.md` の 3 箇所は Streaming 現状仕様と Architecture へ向け直し、S-4 の行の状態（d2〜d5 未着手）を現況に直した。
- 新しい事実（分類: 後続スライス。凍結条件の違反ではなく、本スライスの対象 file の外なので変えていない）:
  - Architecture §20 の冒頭・図・本文の 3 箇所が「Editor Play 向け publish 入口は未整備」のままで、BS5b-2 の検証 Play・From published directory と食い違う。
  - Architecture §21 の「上図が現況」の段落が Scene payload を 652 と書き、S-4c〜d の 6 件（Streaming 現状仕様の 658 件）を含まない。

### 動作確認

`対象なし`（§5。全ての操作・実行時の条件が content build を含む検証 Play の上にあり、判定 C で実行する）。

## 7. Phase C

（未着手）

## 8. Phase C'

（未着手）

## 9. Phase D

（未着手）
