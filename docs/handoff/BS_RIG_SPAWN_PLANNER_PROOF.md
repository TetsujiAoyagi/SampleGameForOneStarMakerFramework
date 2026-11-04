# directory mode の flight rig の spawn と Planner 実証

## 0. メタデータ

- type: `slice`
- status: `動作確認`（Phase B は完了。1 回目の差 2 件は決定記録 D1 の手順の再実行で解消。発見 C 待ち）
- branch: `claude/bs-rig-spawn-planner-proof`
- implementation base commit: `cea28ee4`（`develop`）
- implementation head commit: `a79530b4302fdb3728220551cee5642de4662414`
- risk: `normal`（SampleGame の `FlyController`・`PlayerScene` のログの 1 行・EditMode テスト 1 件だけを変える。Framework・公開 API・asmdef・所有者・寿命・`SceneState`・Addressables の設定を変えない）
- owner: 発注者
- created: 2026-11-04
- expires: 本スライスの Phase D 完了時
- harvest to: Architecture §18 §4「BuildSystem の後続」（S-4d5b の行・BS5b-2 の「未調査の観測」と「テストの隙間」・「検証の残り」）、BuildSystem Artifact Storage Program §7 の該当行、Architecture §05 §5.13（Planner Play の実証の記述）、Architecture §12 §3.2「ギャップ」（ログの formatter の欠落の行。U7）
- Phase A snapshot の取得案内・状態: 使わない（発注者の裁定 U4。Phase A の版は内容 sha256 で固定し、Phase A の commit は A3 凍結版だけにする）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - **更新（発見 C の RED 確認、起動 SHA `03d7cd59…`。2026-11-04）:** AC1 の修正前の失敗を確かめた（§6「発見 C」。(ii) の assertion で Failed、setup の失敗ではない）。Phase A に戻す理由は無い。次: 発見 C の残り（構造・契約のレビュー）の要否を発注者が決め、問題が無ければ判定 C 用の GO 候補 head の固定（履歴の整理があれば先に）へ進む。以下は更新前の記述。
  - **更新（動作確認の再実行、起動 SHA `b04f6471836e7c5a6c359041c6981fc7c7b9b9c5`。2026-11-04）:** 決定記録 D1 の手順で動作確認を 1 回やり直した（§6「再実行」）。差は無い（`handler-registered` が `loaded Title` より前、`(initial)` に Title なし、`IsSceneStable("InGameSession")` の eval の疎通、位置の 2 点を取得）。次: 発見 C（§5 のとおり、AC1 のテストだけの commit `40fcdb34` を detached worktree で `-Filter` 1 回走らせて RED を確かめる）。以下は更新前の記述。
  - **更新（差の扱いの検討、起動 SHA `07193672eab7e404e234f1835571cd447feea7ef`。2026-11-04）:** 差 1 は「新しい事実の分類」の A 再開にも B 適応にも当たらず、検証経路の基盤不備（hook を掛ける時点の手順）として扱う（osm-workflow の phases-and-handoff の「検証経路の成立」の「権限内で解消できる基盤不備」。根拠は §2 の決定記録 D1）。製品コードは変えず、implementation head も変わらないので B へは戻さない。差 2 は同じ再実行で取り直す。次: 動作確認の実行担当が、§5 の直した手順（D1）で implementation head の動作確認を 1 回やり直し、§6「動作確認」の「再実行」に書く。直した手順でも `handler-registered` が `loaded Title` より後なら、その時点で Phase A に戻す。差が無ければ票を発見 C へ更新する。開始の照合・権限・停止規則・引き渡さないものは下の票のまま（「implementation head から HEAD までの差分が HANDOFF だけ」も同じ）。以下は更新前の記述。
  - **更新（動作確認の実行担当、起動 SHA `20753a501691eae13d1ae5764d9d8ce43f0bd1b3`）:** 動作確認を 1 回実施した（§6「動作確認」）。差が 2 件あり、発見 C へは進めない。次: 差 1（`handler-registered` が Title の load より後）の扱いを Phase A（判定 C の `W_all` の起点と hook を掛ける手順）で決める。差 2 は動作確認の手順（`Player ready` の直後の位置の取得）の実行上の取りこぼしで、観測の仕組みは疎通している。以下の「次」は更新前の記述。
  - 次: 動作確認（実行担当は Phase B 以外の担当。§5「操作・実行時・目視条件の検証経路」の「動作確認」の手順だけを、implementation head で各 1 回行う）。疎通させるもの: E1 の `IsSceneStable("InGameSession")` の eval、`[d5b-scene] handler-registered` が `[d5b-scene] loaded Title` より前に出ること（届かなければ Phase A に戻す）。観測と期待との差を §6「動作確認」に書き、差があれば B へ戻す。無ければ票を発見 C へ更新する。発見 C は §5 のとおり、AC1 のテストだけの commit（§6 の SHA）を detached worktree で `-Filter` 1 回走らせて RED を確かめる。
  - 開始の照合: 起動指示で渡された SHA（この票を含む記録 commit）と HEAD が一致し、作業ツリーに `artifacts/`・`docs/reviews/` 以外の差分が無い。implementation head 欄の SHA が HEAD の祖先で、implementation head から HEAD までの差分が `docs/handoff/BS_RIG_SPAWN_PLANNER_PROOF.md` だけ。
  - 権限: ローカルの Unity Editor の起動と eval による操作（`tools/unity-editor.cmd`）。Unity テストと Build は走らせない（動作確認の対象外）。資格情報・実 R2 は使わない。
  - 停止規則: 想定外の観測は原因調査や再実行で埋めず、そのまま記録して B へ戻す（ledger の拒否の 1 回のやり直しは §5 のとおり）。凍結した受け入れ条件を増減しない。
  - 引き渡さないもの: A2 の各レビューの所見の写しを C' へ渡さない（§5.1 は HANDOFF の一部として読まれる）。`artifacts/` を commit に混ぜない（U6）。動作確認の観測に GO / NO-GO と合否を書かない。

## 1. 目的と対象外

- 目的: Content Directory（directory mode）の上の Planner Play（検証 Play `spring-planner-whitebox`）で、Title の Start の後に flight rig が spawn に置かれて Planner の World が使える状態になることを、S-4d5b が NO-GO で閉じた条件をそのまま満たして実証する。あわせて、From published directory の Play の後に既定 cache root の `installed/<contentSet>` が空になる観測が DIST の install の寿命として正しいかを確かめる（S-5 の W-6「検証済み installed revision だけを登録」の前提）。
- 利用者: SampleGame の開発者（Editor で検証 Play・From published directory を使う人）。
- 対象外:
  - Addressables の設定・Content Directory の build と publish の仕組み・DIST の install と eviction の製品コード。
  - Framework のログ基盤（`AppLoggerFactory` の JSON formatter が `Vector3` などの値を持つ行を黙って落とす件。§2 の後続へ送る）。
  - §18 §4 の BS5b-2 の「テストの隙間」「局所の整理」のうち、`EmptyCacheRoot_InstallsUnderTheDefaultRoot` 以外の項目。
  - `There are 2 audio listeners in the scene` の Warning、clean worktree の batch の 1 回目の CoreCLR のメモリ確保失敗（`0x8007000E`）。
  - §18 §4 の「未着手の問い」「入力 identity」「CI 組み込み」の各項目。Player（BS4）での同じ Play。
- 現況（A0 と A1 の観測。一次証拠は `artifacts/bs-rig-spawn-a1/`（git 管理外、`SHA256SUMS.txt` 付き）と S-4d5b の `artifacts/s-4d5b-phase-c-verdict/`）:
  - **rig が原点に留まる原因（A1 の観測と Unity の公式の説明。内部の機構は断定しない）:** `FlyController.Teleport` は `transform.position` だけを書き、`Rigidbody`（`interpolation = Interpolate`）の位置を書かない。Unity の公式の説明では、補間が有効な Rigidbody の Transform は physics が制御し、`Rigidbody.position` の変更は次の simulation の後に Transform へ反映される。A1 の観測（`hook.cs` が `EditorApplication.update` ごとに、位置が変わったときだけ記録。`ft` は `Time.frameCount`）では、Teleport の `ft=4810` に transform が `(125,28,1125)`・Rigidbody が `(0,0,0)`、続く `ft=4811` で transform も `(0,0,0)` に戻った（directory の Play は `ft=964`→`965`）。「次のフレーム」と言う根拠は `ft` の連続だけである。距離 Driver の Focus は `FlyController.Position`（`transform.position`）を読み、最初の Tick は `RegisterFlight` の後の `UniTask.WaitUntil` の明けるフレーム（Teleport の次のフレームの Update）で起きるので、原点で Tick して `Spring_Cell_0_4` を unload し `Spring_Cell_0_0` を載せる。
    - **説明の及ばない点:** Addressables の Play の `ft=4810`→`4811` は rt で 67 ms あり（`fixedDeltaTime` は記録していないが、既定の 0.02 s なら fixed step が数回走った区間）、simulate の前に Transform の変更が Rigidbody へ同期されるという通常の理解では戻りを説明できない。Play 中の `Physics.autoSyncTransforms` は `false`（`eval-returns.txt`）だが、これは query の前の同期の設定で、戻りの原因とは言えない。したがって「なぜ transform の書き込みが Rigidbody に入らないか」は未解明で、修正（`Rigidbody.position` も書く）が全てのフレームで効くことも推論では保証しない。AC1 の毎フレームの実測で決める（§2 AC1、§5）。
    - **回転（未観測）:** `ApplyLook` も補間つきの Rigidbody の `transform.rotation` を直接書く。製品の Teleport は `Vector3.forward`（yaw 0）を渡し、マウスの操作中は `ApplyLook` が毎 Update に絶対値で書き直すので実害は見込まない。A1 は rotation を記録しておらず、本スライスも観測しない（S-4d5b の未達の条件に回転は無い。症状が出たら別件として §18 §4 に送る。S14 の裁定）。
  - **directory mode と Addressables mode の比較（A1）:** 同じ Editor・同じ head（`cea28ee4`。`fb97da0d` から `unity/` の差は文書だけ）で、Addressables mode（Play As `spring-planner-whitebox`、Title を開いて Play）と directory mode（S-4d5b の install 済み revision を検証 Play の bridge と同じ値で渡し、空 Scene から Play）を各 1 回、どちらも Title の `start-button` へ `NavigationSubmitEvent` を送って Start した。**両 mode で同じ現象**（Teleport の次のフレームで原点に戻り、`Spring_Cell_0_0` が載る）。§18 の「directory mode で原点に留まる」は mode に依らない。
  - **Start の経路の差（A1）:** UI の `start-button` の submit と、S-4d5b の reflection（`OnStartRequested` の delegate を直接呼ぶ）は、どちらも `OnStartRequested` を通る（`TitleView.OnRootCreated` が `start-button` の `clicked` に `OnStartRequested?.Invoke()` を結ぶ。`TitleView.cs` 67 行）。症状（rig が原点に留まり `Spring_Cell_0_0` が載る）も同じだった。A1 は reflection の経路を再実行していない。reflection の結果は S-4d5b の 1 回だけである。
  - **`Player ready at Cell stream spawn` が出ない原因（A1 で特定）:** App のログ（`unity/OneStarMakerLocal/Log/*.log`、JSON の rolling file）では `Flight registered` の行は出るが、`Player ready` の行はA1 時点の全てのログ（24 file。file ごとの件数は `app-log-line-counts.txt`）で 1 度も出ていない。`PlayerScene` はこの行の値に `Vector3` を渡し、`AppLoggerFactory` の JSON formatter（`IncludeProperties.All`）が値を `System.Text.Json` で書こうとして `Vector3.normalized` の循環で `JsonException` になり、行ごと落ちる。A1 の eval で、同じ設定の ZLogger に `before`・`Player ready … {v}`・`after` を書くと `before` と `after` だけが残り、内部エラーが `JsonException: A possible object cycle was detected` だった（eval の戻り値は `eval-returns.txt`）。`InternalErrorLogger` を設定していないので、本番では何も出ない。S-4d5b の「`Player ready` が約 3 分出なかった」はこの欠落であり、bootstrap が止まっていたのではない（`Flight registered` と入力 ON は起きている）。製品コードで `ZLog*` に `Vector3` の位置の値を渡しているのはこの 1 箇所だけと A1 で grep したが、grep のコマンドは残っていない。B が `SampleGame` と `OneStarMaker` の非テストの `.cs` を `ZLog` で grep し直し、ログの値に `Vector3`・`Quaternion`・`Color`・`Bounds` 等の Unity の値型を渡す箇所の一覧をコマンドとともに §6 に残す（1 箇所以外が在っても本スライスでは直さず、ログの後続（U7）へ渡す）。この formatter の欠落が在る間は、App のログの `Error` が 0 件でも「アプリ内で Error が起きなかった」とは言えない（§2 AC4 は「観測できた App の JSON の行の `Error` が 0 件」とだけ主張する）。
  - **`installed/<contentSet>` が空になる観測（A1。事実・最有力の仮説・不明を分ける）:**
    - **事実:** 既定 cache root（`%LOCALAPPDATA%\OSM\ContentCache`）の子の `control`・`installed`・`installed\spring-full`・`staging`・`control\transaction.lock` の作成時刻と更新時刻が全て 2026-11-03 11:58:00 で、中身は空の `installed\spring-full` だけだった（`default-cache-root-listing.txt`。この一覧は子だけで、root 自身の行は無い。root の作成時刻は証拠に無い）。同じ日の EditMode 全件回帰（`TestResults/86277a4f-…/results.xml`、02:42:41Z〜02:58:26Z。写しは `artifacts/bs-rig-spawn-a1/s-4d5b-day-editmode-results.xml`）で `PublishedDirectoryReceiveTests.EmptyCacheRoot_InstallsUnderTheDefaultRoot` が `start-time="2026-11-03 02:58:00Z"`（11:58:00 JST）に走っている。このテストは既定 root に `spring-full/<テスト用 revision>` を install し、teardown で自分の revision だけを消すので、空の `spring-full` が残る（コードで確認）。S-4d5b の検証 Play が使った Delivery 窓の cache root（`D:\repositories\unity\OSM-evidence\bs5a-purpose-selection\ac13-cache`）には、その Play が使った `installed/spring-planner-whitebox/20261103T091410930Z-…` と `bs4-spring-full` の revision が残っている。
    - **最有力の仮説:** BS5b-2 の「空」の観測は、テストが既定 root に残した跡を From published directory の結果と読み違えたもの。
    - **不明（事後に確定できない）:** BS5b-2 の観測の手順と、そのとき見た root の絶対 path は残っていない。時刻の一致は仮説と整合するが、元の観測の因果を証明しない。「既定 root に他の書き手が居ない」も、I2 の探索が終わるまでは根拠に使わない（I2 の探索境界）。したがって E3 は「元の観測の原因を証明した」とは書かず、新しい観測（AC5）で「今の head で install の寿命の欠陥は再現しない」と「テストが既定 root を汚す経路を断った」を示して閉じる。
  - **DIST の install の寿命（正本は Architecture §13「DIST transport と disk cache の境界」。A1 はコードと突き合わせた）:** install した revision は `ContentCacheStore.MakeRoom` だけが消す。呼ぶのは次の install の容量確保（`EnsureCapacityUnsafe`）と明示の `Evict` で、budget を超えた分だけを、known-good の pin と要求中の revision を除いて古い順に tombstone へ移してから消す。Play の終了・ledger の Reset・窓を閉じることは revision を消さない。つまり「DIST は revision を消さない」ではなく、**容量確保と明示の Evict という契約上の経路でだけ消える**。W-6 の前提（検証済み installed revision が残る）とは、この条件つきで矛盾しない（W-6 が容量確保による削除をどう扱うかは S-5 の問い）。今回の当該 revision（S-4d5b の `spring-planner-whitebox`）が Play の後も残っていたことは、上の事実とは別の個別の観測として扱う。EditMode テストが既定 root に install すると、容量確保で開発者の revision を消しうる（今の budget の既定値で起きたかは未確認。起きた痕跡（tombstone）は無い）。
- UC ごとの利用者の入力:

| UC | 利用者が選ぶ・入れるもの | 機械が決めるもの | 誰がいつ決めるか | 目的からの正当化 |
| --- | --- | --- | --- | --- |
| UC1 検証 Play | menu で宣言 `spring-planner-whitebox` を選ぶ | mode・表現・runtime 既定値・初回 Scene・cache root（窓の設定、空なら既定）・build・publish・install・bridge | 選択は実行時の操作。他は宣言（開発時）と自動導出 | 実証する Play を 1 つに決める唯一の入力。既存の経路で、このスライスは増やさない |
| UC2 Start | Title の Start を押す | rig の spawn への配置・Focus の登録・入力 ON | 押下は実行時の操作。配置は自動 | 「Start の後に spawn に置かれる」を確かめる操作そのもの |
| UC3 EditMode テストの実行 | なし（`run-tests.ps1` を走らせるだけ） | テストの cache root（一時 directory） | 自動 | 開発者の実 cache root を観測の対象として信頼できるようにする。利用者の入力は増やさない |

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: directory mode の検証 Play `spring-planner-whitebox` で、Title の Start（通常の操作）の後に rig が spawn に置かれ、S-4d5b の未達の条件と再検証の条件が同じ 1 回の Play で揃うか。あわせて、`installed/<contentSet>` が空になる観測は DIST の install の寿命の欠陥か。
- 進める最低条件:
  - 不変条件:
    - **I1（Start の後、spawn に置かれた rig が維持され、原点の側の Cell への切り替えが観測されない）:** Start から `Player ready at Cell stream spawn` の後、診断で rig を動かすまでの間。最初の Tick は Teleport の次のフレームの Update で Focus を読むので、Teleport の後のフレームで transform が原点や中間の位置に戻ることが A1 の症状の起点である（AC1 は終点だけでなく毎フレームを見る）。
      - **受け入れる主張（観測できる形。S-4d5b の未達の条件の境界に揃える）:** 判定は (a) AC1 のテストの窓で毎フレーム位置が保たれる、(b) 検証 Play で `Spring_Cell_0_4` の unload が `W_spawn`（§5）で 0 件、`Spring_Cell_0_0` の load が `W_all` で 0 件、(c) AC3 の位置の eval の 2 点（`Player ready` の直後と、その 0.5 s 以上後）で rig が spawn から 1 m 以内、で行う。A1 と同じ持続的な原点への戻りなら通常は `0_4` の unload と `0_0` の load として表れるので、これらを再発の検知の指標にする（`WorldStreamingController` は in-flight の上限や優先順にも依るので、一瞬だけ外れた Focus が必ず完了のログを残すとは言わない。F4-2）。**「全ての Tick の入力を観測した」とは書かない。** 毎フレームの位置の hook と `_lastTickFocus` の reflection は使わない（`EditorApplication.update` は player loop と 1 対 1 に結び付かず、証明にならないうえ、観測の仕組みの検証が主になる。V3-1・S14 の裁定）。全 Tick の厳密な証明は本スライスの目的の外とする。観測できる語では、検証 Play の 1 回で `Spring_Cell_0_4` が Stable になった後に unload されず、`Spring_Cell_0_0` が載らない（spawn `(125,28,1125)` から `(0,0)` の Cell は load 距離 375 m の外）。
      - 主張を崩しうる既存コードの経路: (a) `FlyController.Teleport` が transform だけを書き、補間が戻す（A1 で確認した原因）。(b) `FixedUpdate` が入力 OFF の間に `linearVelocity` を 0 にするだけで位置を書かない（位置は動かさないので崩さない）。(c) `IFlightReadModel.Position` が `transform.position` を読む（Teleport が Rigidbody を書けば transform と一致する）。(d) `PlayerWorldReadySequence.Run` の順序（Teleport → RegisterFlight → 入力 ON。Focus は RegisterFlight より前に無い）。(e) F1〜F4 の debug teleport が同じ `Teleport` を通る（直す側に入る）。(f) `PlayerScene.unity` の rig の初期位置が原点（Teleport が効けば無関係）。(g) Cinemachine の Follow は rig の transform を読むだけで書かない。
      - 探索した境界: `SampleGame/InGame/InGameSession/PlayerScene/`・`Streaming/Runtime/{Distance,PlayerReady,Season}/`・`InGameSessionScene` の Focus と Flight の入口。`PlayerScene.unity` の rig（`PlayerFlightRig`）の component は `Transform`・`Rigidbody`・`CapsuleCollider`・`FlyController`・`PlayerRigBindings` だけ（A1 の eval で列挙）。`SampleGame/InGame` の非テストの `.cs` で `Rigidbody` を扱うのは `FlyController` と、進入者の Rigidbody を読むだけの trigger 3 種。未確認: Cinemachine・CameraSystem の内部が rig の transform を書かないこと（読むだけと理解しているが、package のコードは読んでいない）。
    - **I2（テストは開発者の実 cache root を変えない）:** EditMode 全件回帰の前後で、既定 cache root（`%LOCALAPPDATA%\OSM\ContentCache`）の file と directory の一覧（path・作成時刻・更新時刻）が変わらない。root が無ければ作られない。
      - 主張を崩しうる経路: (a) `EmptyCacheRoot_InstallsUnderTheDefaultRoot` が既定 root に install する（A1 で確認した原因）。(b) install の容量確保が既定 root の他の revision を消す。(c) 他のテストが `ResolveCacheRoot("")` の結果へ書く（`PurposeLaunchSettingsTests` は値の比較だけで書かない）。(d) `ContentDeliveryWindow.LoadSettings()` の窓の設定を読むテスト（`cacheRoot` が空なら既定 root）。
      - 探索した境界: `SampleGame/Tests` と `OneStarMaker/Tests` の `ResolveCacheRoot`・`LocalApplicationData`・`cacheRoot = ""`・`LoadSettings` の grep。未確認: `tools/` の .NET テストと `OneStarMaker/Tests` の全体（B が `ResolveCacheRoot`・`LocalApplicationData`・`LoadSettings`・`AcquireTransaction`・`ContentCacheStore(` を grep し、コマンドと結果を §6 に残す。既定 root に install するか transaction を取るテストが在れば、同じく一時 root へ寄せる）。この探索が終わるまで「既定 root に他の書き手が居ない」は主張しない（§1・E3 の根拠に使わない）。
    - **I3（判定の Play は選択外の content を読まない）:** 再検証の条件のうち「Environment と Cell 単位の Lighting・VFX が選択にもロードにも 0 件」「`ContentDeclarationMismatch` と App の Error が 0 件」（§18 §4 の原文）。このスライスは content の選択とロードを変えないので、崩しうる経路は既存のもの（Planner の companion set の選択、directory mode の封鎖）で、S-4d5b の判定 C で同じ検査が通っている。
  - 成立条件:
    - **E1（未達の条件、§18 §4 の原文どおり）:** Title の Start の後に `InGameSession` と Spring `(0,4)` が Stable になり、`Player ready at Cell stream spawn` が出て、rig の `Rigidbody.position` が `WorldCellCatalog.SpawnPosition()` から 1 m 以内にあること。
      - **各項目の観測元（A3 で確かめた。どれも無ければ計数不能）:** Spring `(0,4)` の Stable は App のログの `OnStabledImpl Spring_Cell_0_4`（`DemoCellScene` が出す。A1 のログに在る）。`Season_Spring` は `OnStabledImpl SeasonScene Season_Spring`。**`InGameSession` の Stable はどのログにも出ない**（App のログには `Create InGameSession` と `[InGameSession] SessionSeasonController created` だけ）ので、`Player ready` の行の後に eval で public の `ISceneQuery.IsSceneStable("InGameSession")`（identity は `SceneIds.InGameSession` の綴り。`SceneDirector` は `Lifecycle.State == Stable` かつ unload 未開始で true）を呼んで記録する（`SceneBase.Lifecycle` は internal なので読まない。`SceneDirector` への到達経路は B の後の動作確認で疎通させて凍結する。届かなければ Phase A に戻す）。`Player ready` は `WaitUntilWorldReady`（Spring と `(0,4)` の初期の要求の成功）の後に書かれるが、それだけで `InGameSession` の Stable の代わりにしない。
    - **E2（再検証の条件、同じ行の原文どおり）:** 後続の修正を含む head で、untitled の空 Scene から検証 Play `spring-planner-whitebox` を 1 回行い、E1 に加え、`preflight.json` で `Spring_Cell_4_2` が Whitebox の 1 件だけ、local trigger と major Event の VFX の出現と回収、Environment と Cell 単位の Lighting・VFX が選択にもロードにも 0 件、`ContentDeclarationMismatch` と App の Error が 0 件であることを、同じ 1 回の Play で確かめる。
    - **E3（install の寿命の判定）:** 次の 4 点が揃えば、BS5b-2 の「未調査の観測」の該当項目と「テストの隙間」の `EmptyCacheRoot_InstallsUnderTheDefaultRoot` の項目を閉じる。(a) 元の観測は絶対 path が残っておらず事後に因果を確定できないことを、§1 の「事実・最有力の仮説・不明」の形で §18 §4 に書く（「原因を証明した」とは書かない）。(b) 判定 C の検証 Play で実際に install した revision root を、`VerifyPlayLauncher` が Editor.log に出す `[VerifyPlay] installed <revision root> (cache root <root>)` の行から取り（`transport.json`・`outcome.json` から path を作らない）、Play を止めた後にその revision root が `receipt.json` とともに残っていることを pwsh の列挙で確かめる（今の head で寿命の欠陥が再現しないことの証拠。主張は「検証 Play が install と報告した revision root が停止後も在った」に限り、全ての revision の寿命や、root と Play の内容の暗号学的な一致は主張しない）。`installed` の行が 0 件か 2 件以上なら計数不能（NO-GO）。(c) 寿命は §13 の正本（容量確保と明示の Evict でだけ消える）を参照し、新しい契約を書かない。W-6 とは「この条件つきで矛盾しない」と書く。(d) AC5（テストが既定 root を変えない）。
  - E1・E2・E3 の成否は判定表に別々に記録する。片方の GO で他方の未達を隠さない（スライス分割の判断を参照）。
- 受け入れ条件:

| ID | 内容 | 条件 | 証拠 |
| --- | --- | --- | --- |
| AC1 | `Interpolate` の Rigidbody で `FlyController.Teleport` を呼んだ後、2 つの時点で判定する。(i) 呼んだ直後（同じフレーム）に `IFlightReadModel.Position`（Focus が読む値）が目標から 1e-3 以内。(ii) 呼んだ次のフレームから観測窓の終わりまで、**毎フレームの Update の後**（coroutine の `yield return null` の明け）に `transform.position` と `Rigidbody.position` の両方が目標から 1e-3 以内（1 フレームでも外れたら失敗。終点だけを見ない）。窓・入力・RED は表の下の「AC1 の詳細」 | I1・E1 | EditMode テスト（Play mode に入る `[UnityTest]`、§5 の fixture） |
| AC2 | `Player ready at Cell stream spawn` の行が App のログ（JSON の rolling file）に出て、文言と 3 成分の値を含む。ログの値は `WorldCellCatalog.SpawnPosition()` であって rig の位置ではないので、AC2 を 1 m の条件の証拠にしない（1 m は AC3 の eval の `Rigidbody.position` で数える） | E1 | 動作確認と判定 C の App のログ |
| AC3 | 検証 Play の 1 回で E1 の全項目。`Spring_Cell_0_4` が Stable の後に unload されず、`Spring_Cell_0_0` が載らない。`Player ready` の行が出た後に eval で `Rigidbody.position` と spawn の距離、入力 ON、Scene の一覧を取る（`Player ready` は入力 ON の後に書かれるので、行が出れば World を操作できる状態に在る）。位置（`Rigidbody.position` と `transform.position`）は `Player ready` の直後と、その 0.5 s 以上後（設定上の Tick の周期 0.2 s より十分長い。実際の Tick の回数は数えない。F4-5）の 2 点で取り、両方が spawn から 1 m 以内（I1 の (c)）。Start の後 3 分で `Player ready` が出なければ失敗（失敗経路: 修正が効かなければ A1 と同じ unload と `(0,0)` の load がログに出る） | I1・E1 | Editor.log の scene の load / unload 行、App のログ、eval の rig の位置 |
| AC4 | 同じ Play で E2 の残り（preflight の `Spring_Cell_4_2` が Whitebox の 1 件、local trigger と major Event の VFX の出現と回収、Environment・Cell 単位の Lighting・VFX が選択とロードに 0 件、Mismatch と App の Error が 0 件）。各項目の合否の定義は表の下の「AC4 の詳細」。content が Content Directory から読まれたことを、封鎖の行（`Addressables backend sealed`）と初回 Scene の行（`Editor content directory first scene: … stable=True`）が各 1 件・封鎖の拒否が 0 件で確かめる。App の `Error` の条件は「判定の窓（§5 の照合 script）で観測できた App の JSON の行の `Error` 以上が 0 件」とだけ主張し、formatter の欠落（§1）が在るので「アプリ内で Error が起きなかった」とは書かない。補助として同じ窓の Editor.log の `Error`・`Exception` の行も数えて記録する | I3・E2 | `preflight.json`、Editor.log、App のログ、照合 script の出力 |
| AC5 | `EmptyCacheRoot_InstallsUnderTheDefaultRoot` が既定 root に install しない（解決の値の比較だけにし、名前を実態に合わせて直す）。保証の範囲は「既定 path の解決（このテストと `PurposeLaunchSettingsTests`）」と「任意に指定した root への install（Framework の `ContentInstallerTests` 等の一時 root）」の組み合わせに縮み、**既定 root への実 install はどのテストも確かめなくなる**ことを §18 §4 に書く。EditMode 全件回帰の直前と直後で既定 root の一覧（root 自身を含む全ての file と directory の path・作成時刻・更新時刻）が変わらない。一覧は同じ pwsh の列挙で取り、列挙した絶対 path を記録する。Unity の `ResolveCacheRoot("")` とは照合しないので、root の同一性を完全に証明したとは書かず、既定 root への既知の書き込み経路を消したことと合わせた範囲に限る。root が無い場合は「前後とも無い」を合格とし、在る場合と区別して記録する。一覧の間には全件回帰だけを置き、検証 Play は一覧の外で行う（失敗経路: 修正前は root が無ければ root と `installed\spring-full` が作られ、在れば `control\transaction.lock` と `installed\spring-full` の更新時刻が変わる） | I2・E3 | 判定 C の全件回帰の前後の一覧 |
| AC6 | §18 §4 の S-4d5b の行・「検証の残り」・BS5b-2 の 2 項目、program §7 の行、§05 §5.13、§12 §3.2 のギャップの行（U7）が本スライスの結果で更新され（寿命は §13 を参照し、E3 の (a) の形で書く。ログの欠落は「欠落する機構」と「S-4d5b の Play で出なかった事実」を分けて書く。AC5 の保証の縮みを書く）、`pwsh tools/docs-audit.ps1` が通る | E3 | 文書の差分、コマンド出力 |

- **AC1 の詳細:**
  - **呼ぶ時点と窓（A3 で固定）:** `yield return new WaitForFixedUpdate()` の明けに Teleport を呼び、その後 60 フレームを窓とする（`fixedDeltaTime`・`captureDeltaTime` は変えない）。A1 では Addressables mode と directory mode の両方で、Teleport の次のフレームに原点へ戻った（フレーム長 67 ms の場合も）。fixed step の位相を作り分けて数える仕組みは置かない（S14 の裁定。RED が出なければその時点で Phase A に戻すので、位相の偶然で通る危険はそこで捕まえる）。
  - **入力:** 製品の順序（`PlayerScene.OnLoadedImpl` が先に入力 OFF、`PlayerWorldReadySequence` が Teleport の後に ON）のうち、位置の保持は入力に依らないので、テストは `InputEnabled = false` のまま行う（`SampleGame.Tests` は Input System を参照しておらず、外部のキーボード入力を検査できないため。入力 ON の後の保持は AC3 の Play で確かめる）。
  - **回転:** 確かめない（§1 の回転）。
  - **RED:** 修正前の実装では (ii) の assertion で失敗する（setup・起動の失敗は RED に数えない）。修正前の 1 回は発見 C が、テストだけを足した B の commit に対して `run-tests.ps1` の filter で確かめる（§4 の順序）。RED が出なければ Phase A に戻す。
- **AC4 の詳細（S-4d5b の判定 C の観測を合否の定義に直した。S-4d5b の照合 script 自体は bundle に残っていないので、規則をここに書く）:**
  - **選択（`preflight.json`）:** requirements の `Spring_Cell_4_2` が Whitebox の 1 件だけで、excluded に `Spring_Cell_4_2` の Full（`RepresentationNotPreferred`）が在る。requirements と selected に `Spring_Environment_{x}_{y}`・`Spring_Lighting_{x}_{y}`・`Spring_VFX_{x}_{y}`（Cell 単位）が 0 件。Season 単位の `Spring_Lighting`・`Spring_AtmosphereVFX` は許す。名前の規則は B が `SceneResourceMap` の実名で確かめ、正規表現として §6 に書く（照合 script はその正規表現を使う）。
  - **ロード（Editor.log）:** scene の load / unload の行は製品のログには無く、S-4d5b の `eval/ac6reg.cs`（`SceneManager.sceneLoaded`/`sceneUnloaded` に `[d5b-scene] loaded|unloaded <name>` を出す）が出していた。判定 C は同じ hook を Play に入る直前に掛け（掛ける時点は決定記録 D1）、`[d5b-scene] handler-registered` の行を正の証拠に加える。handler は登録より前の load を記録しないので、主張は「`handler-registered` の後の load が 0 件」に限る。登録が初回 Scene（Title）の load より前に入ることを B の後の動作確認で確かめる（`handler-registered` が `[d5b-scene] loaded Title` より前、かつ `(initial)` の一覧に Title が無い）。判定 C でこの順序が崩れていれば、登録前の区間は未観測として NO-GO。`W_all`（§5）で上の Cell 単位の名前の load が 0 件。I1 の `Spring_Cell_0_0` の load は、この行と App のログの `OnStabledImpl Spring_Cell_0_0` の両方で 0 件。
  - **local trigger の VFX:** rig を `Spring_Cell_4_2` の上に移し（`mv_above42.cs`）、`Spring_Cell_4_2` と `Spring_Events_4_2` の load を見た後、`mp_local.cs` の `MovePosition` で `LocalVfxTrigger_Burst` へ入れる。合格は、入れてから 30 秒以内に `obs.cs` の VFX の一覧に、入れる前の一覧に無かった enabled の `Spring_Events_4_2_Burst` が 1 件以上現れ、rig を spawn へ戻してから 30 秒以内にその entity id が一覧から消える（回収）。
  - **major Event の VFX:** `mp_start.cs` で開始の trigger へ入れ、30 秒以内に `Spring_Event_MultiCellProof` の load と enabled の `Spring_Event_MultiCellProof_Burst` が 1 件以上現れ、`mp_done.cs` で完了の trigger へ入れてから 30 秒以内に `Spring_Event_MultiCellProof` の unload とその entity id の消滅が見える。
  - 窓は §5 の `W_all`・`W_spawn` と、各 trigger の marker から 30 秒。移動に伴う源流の Cell の unload と `4_2` の再ロード・再 unload の揺れ（S-4d5b で 1 回）は不合格に数えないが、Cell 単位の Environment・Lighting・VFX の load は診断の移動の後も `W_all` で数える。
- 合格例・不合格例（AC3 の時点）: 合格は、Start の後に `Spring_Cell_0_4` が loaded のまま、周辺の Cell（`0_3`・`0_5` 等）が載り、rig が spawn に居る。不合格は、`0_4` が loaded の直後に unloaded になる、`0_0` が載る、rig が spawn から 1 m を超える、のどれか 1 つ。Start より前（Title の間）の load / unload は数えない。診断のために判定の後で rig を動かした後の load / unload は I1 の対象外（E2 の VFX の確認の手順）。
- ここでは答えない問いと所有する後続スライス:
  - Framework のログ基盤が値の書けない行を黙って落とす件（`AppLoggerFactory` の JSON formatter に Unity の値型の converter が無い、`InternalErrorLogger` が未設定）→ Foundation の Logging（U7）。本スライスは直さず、Phase D で Architecture §12 §3.2 の「ギャップ」の表に行を足すだけにする（担当のスライスは未定）。
  - W-6 の「検証済み installed revision だけを登録」の実装 → S-5（`docs/handoff/SEASON_WORLD_DESIGN.md` の W-6）。
  - 2 audio listeners の Warning、CoreCLR の `0x8007000E`、BS5b-2 の他の「テストの隙間」「局所の整理」→ §18 §4 に残す。
  - Player での Planner Play → BuildSystem program §7（所有未定）。
- スライス分割の判断: 契機あり（独立した変更理由が 3 つ混ざる: rig の配置、ログの 1 行、テストの cache root）。**分割しない。** rig の配置とログの行は同じ E1 を満たすのに両方要り、片方だけでは判定できない。テストの修正は、owner が A0 でこのスライスに置いた問い（install の寿命）の答えそのもので、テストだけの変更（製品の寿命の契約は変えない）。3 つとも数十行の規模で、判定の Play は 1 回にまとまる。
- 判定定義: GO は進める最低条件（I1〜I3、E1〜E3）が全て揃い、現在の問いに致命的な反証が無い。NO-GO はどれかが欠ける、不明、または計数できない（必須の正の証拠の欠落・ログの解析不能・複数の run の混在を含む）。スパイクではないので CONDITIONAL ACCEPT は使わない。E1・E2・E3 の成否は別々に記録する。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。未達・不明・計数不能なら、証拠を保存したうえで NO-GO として終了し、発見を Phase A に戻す（未達のまま GO にしない。無理に再試行を重ねて GO に寄せない）。
- A3 後の例外承認: なし
- 決定記録:
  - **D1（2026-11-04、発注者が裁定。適用は implementation head `a79530b4` の動作確認の再実行から。凍結条件の意味・合否は変えない）:** scene の load / unload の hook を掛ける時点を「Play に入った直後」から「Play に入る直前」に移す。
    - 契機: 1 回目の動作確認（§6）で、Play に入ってから eval で掛けると約 14 秒遅れ、`handler-registered` が `loaded Title` より後になった。製品コードに起因しない。
    - 分類: 「新しい事実の分類」の A 再開（状態・依存・所有者・寿命・公開 API・fail-closed の変更、または責務配置で満たせない）にも B 適応（B が直す実装）にも当たらない。検証経路の観測の手順の基盤不備で、権限内で直す。§5 の「届かなければ Phase A に戻す」は、直した手順でも登録が Title の load より前に入らない場合に適用する。
    - 方式: `EnterPlaymode` より前の eval で `EditorApplication.playModeStateChanged` に handler を掛け、`ExitingEditMode` で `sceneLoaded`・`sceneUnloaded` を登録し、`(initial)` の列挙と `handler-registered utc=… ft=…` を出す（`playModeStateChanged` の handler は 1 回で外す）。`ProjectSettings/EditorSettings.asset` は `m_EnterPlayModeOptions: 1`（Play に入るときの domain reload が無効）なので、Edit mode で登録した handler は Play に残る。判定 C の `VerifyAsync` の build の間の Edit mode の scene 操作を拾わないよう、eval の中で直接登録せず `ExitingEditMode` で登録する。製品コード・asmdef・`Assets/` の script は足さない（`InitializeOnLoad`・`RuntimeInitializeOnLoadMethod` の script や EditorPrefs は、`Assets/` へのコードの追加か製品側の読み手が要るので使わない）。
    - 変わらないもの: AC4 の詳細の主張（`handler-registered` の後の load が 0 件、登録が `loaded Title` より前かつ `(initial)` に Title が無い、崩れれば NO-GO）、`W_all`・`W_spawn` の定義、正の証拠の列挙。`handler-registered` の `utc=` は Play の開始より前になり、App のログの全区間が `W_all` に入る。`ft=` は Edit mode の `Time.frameCount` になる。
    - 失敗の見え方: domain reload が起きて handler が消えると、`handler-registered` の後に `loaded Title` が出ないので、正の証拠の欠落として計数不能になる（黙って通らない）。手順で `handler-registered` の後の `loaded Title` の存在を確かめ、実行時の `EditorSettings.enterPlayModeOptions` の値を eval で読んで記録する。
    - 採らなかった案: Editor.log の既存の行（`Editor content directory first scene: … stable=True`）や App のログの `OnStabledImpl`、`(initial)` の列挙で登録前の区間を補う案は、凍結済みの「登録前の区間は未観測として NO-GO」の意味を変えるので、決定記録ではなく Phase A の revision になる。
- 本文へ転記した実装制約:
  - 破棄されうる `UnityEngine.Object` に `?.` / `??` / `is null` を使わない（`_body` の判定は `!= null`）。
  - 新規・編集する `.cs` の先頭に `#nullable enable`。`record` を使わない。
  - テストで `Task.Delay` / `Thread.Sleep` を使わない。待機は `yield return new WaitForFixedUpdate()` と `yield return null` のフレームで行う。
  - Update は `UpdateSystemRuntime` に登録する（このスライスは新しい Update を足さない。`FlyController` の既存の MonoBehaviour の Update は変えない）。
  - asmdef 参照を足さない。足す必要が出たら Phase A に戻す。
  - 公開 API のログ抽象は `ILogger<T>`。`ZLogger*` を公開面へ出さない。
- 未決事項:
  - **U1（A0、裁定済み扱い）:** install の観測は (a) テストの既定 root への書き込みと分かったので、このスライスでテストを直す。製品は直さない。発注者は A1 への進行を指示した（2026-11-04）。推奨どおりの裁定と読んでいるので、違えば A3 で直す。
  - **U2・U3（A0、同上）:** 判定の Start は UI の経路（`start-button` への submit）で行い、reflection は比較にだけ使った。A1 の観測は両 mode を各 1 回取った。
  - **U4（Phase A の版の保存先、2026-11-04 発注者が裁定）:** Phase A snapshot（Artifact Storage）を使わない。A2 の各入力は未 commit の版を内容 sha256 で固定し（§5.1）、Phase A の commit は A3 凍結版の 1 点だけにする（A2 入力版の commit は作らない。S-4d5b と同じ扱い）。判定 C の照合 script は、判定 C の offline 実行の方式なので判定 C の evidence bundle に置き、git に入れない。
  - **U5（AC7、2026-11-04 発注者が裁定）:** 外す。Addressables mode の確認は受け入れ条件にも動作確認にも置かない（A1 の比較の観測は §1 の現況に残す）。
  - **U6（A1 の一次証拠の保存）:** `artifacts/bs-rig-spawn-a1/` は git 管理外で `SHA256SUMS.txt` を持つが、`.gitignore` に入っておらず `git status` に untracked として出る（S-4d5b の `artifacts/s-4d5b-phase-c-verdict/` と同じ）。commit に混ぜない。扱いは保留（発注者）。保留の間、C・C' は A1 と S-4d5b の一次証拠を別の PC から参照できない前提で扱う（§5 の evidence bundle に必要な写しを入れる）。
  - **U7（ログの後続の所有先、2026-11-04 発注者が裁定）:** Foundation の Logging が持つ。`AppLoggerFactory` は `OneStarMaker/Scripts/Foundation/Logging/` に在り、Architecture §12 の施行表 T2 が Foundation の所有としている（§03・§10 は使い方の規約）。§18 §4 は BuildSystem の後続の置き場なので置かない。Phase D で §12 §3.2 の「ギャップ」の表に 1 行を足す: 領域は Logging、ギャップは「JSON formatter（`IncludeProperties.All`）が Unity の値型（`Vector3` 等）を持つ行を黙って落とし、`InternalErrorLogger` が未設定のため落ちたことが外から見えない（本スライスの A1 の再現。S-4d5b の `Player ready` の欠落の原因）」、配置先は Foundation。担当のスライスは未定と書く。

## 3. 責務マップ

| file | 責務 | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 配置理由 | 現在行数 → 予想 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `SampleGame/InGame/InGameSession/PlayerScene/FlyController.cs` | 飛行 rig の入力と移動、瞬間移動 | `Teleport` が Rigidbody の位置を書かない | rig の GameObject（`PlayerScene.unity`、Scene の寿命） | UnityEngine・InputSystem（既存） | `Teleport` の signature は変えない | AC1 の `[UnityTest]` | 既存の file。瞬間移動の実装は rig の component が持つ | 234 → 約 240 |
| `SampleGame/InGame/InGameSession/PlayerScene/PlayerScene.cs` | rig の参照解決と bootstrap | `Player ready` の行が JSON のログで落ちる | 既存 | 既存 | なし | AC2（ログの観測） | 既存の 1 行 | 203 → 約 205 |
| `SampleGame/Tests/Streaming/FlyControllerTeleportTests.cs`（新規） | AC1 | 新規 | `[UnitySetUp]` で Play に入り、`[UnityTearDown]` で GameObject・Cursor・環境変数・Scene 構成を戻して EditMode へ出る（§5） | `SampleGame.InGame`（`SampleGame.Tests` の既存の参照） | なし | Play mode に入る EditMode テスト | `PlayerWorldReadySequenceTests` と同じ場所 | 0 → 約 110 |
| `SampleGame/Tests/Editor/Build/PublishedDirectoryReceiveTests.cs` | BS5b-2 の受け取りのテスト | `EmptyCacheRoot…` が実 cache root に書く | テストの一時 root | 既存 | なし | AC5 | 既存 | 230 → 約 220 |
| §18・program §7・§05 §5.13・§12 §3.2 | 文書 | AC6 | — | — | — | docs-audit | harvest 先 | — |

行数・責務の警報は発火しない。新しい公開 API・状態・依存 edge は無い。

## 4. 実装計画

- 変更対象: §3 の表のとおり。
- 修正の方針:
  - `Teleport`: `_body.position` と `transform.position` の両方に目標を書き、速度を 0 にする（Rigidbody への瞬間移動は `Rigidbody.position` が正しい API。`MovePosition` は補間つきの移動なので使わない）。`transform.position` も書くのは、同じフレームの `RegisterFlight` の直後に Focus（`transform.position`）が読まれるため。回転は既存の `ApplyLook` のまま。
  - `PlayerScene` のログ: 値を `Vector3` のまま渡さず、3 成分を `float` の値として渡す（例: `{spawn.x:F1}` 等）。行の文言 `Player ready at Cell stream spawn` は変えない（判定の照合が文言で探す）。
  - `EmptyCacheRoot_InstallsUnderTheDefaultRoot`: install をやめ、`cacheRoot = ""` の stages の `CacheRoot` が `ResolveCacheRoot("")` と一致することだけを確かめる（解決の規則は `PurposeLaunchSettingsTests` も覆う）。test 名が実態と合わなくなるなら名前を直す。teardown の既定 root の後始末（`_defaultCacheRevisions`）は不要になれば消す。
- 順序: (1) AC1 のテストを書き、Editor のコンパイル確認をして、テストだけの commit を切る（B はテストを走らせない。この commit は発見 C が修正前の失敗を確かめる対象で、SHA を §6 に書く）。(2) `Teleport` を直す。(3) ログの行を直す。(4) テストを直す。(5) 文書（AC6）。(6) `pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1`。
- Phase B から Phase A へ差し戻す条件: `Teleport` の修正で rig が spawn に留まらない（他に Rigidbody を書く経路がある）、Framework・asmdef・公開 API の変更が要る、`PlayerScene.unity` 等の Scene / Prefab の変更が要る。
- 対象外を維持する方法: Framework の `AppLoggerFactory` は触らない。DIST の製品コードは触らない。

## 5. テストとレビュー計画

- 単体テスト: AC1 の `FlyControllerTeleportTests`。Play に入ると `AppInitializer` が `RuntimeInitializeOnLoadMethod` で必ず起動するので、起動と後始末は `SampleGame/Tests/Rendering/Vfx/VfxPlayTestFixture.cs` と同じ原則に揃える（共通化はしない。必要な部分だけを同じ形で書く）:
  - `[UnitySetUp]`: `EditorSceneManager.GetSceneManagerSetup()` を保存 → 空の Scene を開く → `SAMPLEGAME_CONTENT__RUNTIMEMODE=addressables` と `SAMPLEGAME_DEBUGSOCKET__ENABLED=false` を、元の値を保存してから process の環境変数に設定（同じ全件回帰で `PublishedDirectoryReceiveTests` 等が `SAMPLEGAME_CONTENT__*` を書くので、directory mode の残りで起動させない）→ Cursor の状態を保存 → `yield return new EnterPlayMode()` → 起動の完了（`AbstractApplicationInitializer.ActiveVfxRuntimeHost.IsSceneLifecycleBound`）をフレーム数の上限つきで待ち、起動の失敗は Teleport の不具合と区別できる文言で失敗させる。
  - 本体: GameObject を作り `Rigidbody`・`FlyController` を付ける（`Awake` が `Interpolate` を設定する。Play に入る前に作った GameObject は Play 入りで破棄されるので、作るのは Play に入った後）→ `InputEnabled = false`（AC1 の詳細の「入力」）→ `yield return new WaitForFixedUpdate()` → `Teleport` → (i) を判定 → 窓の終わりまで `yield return null` ごとに (ii) を判定（外れたフレームの番号・`frameCount`・両方の位置を失敗の文言に出す）。
  - `[UnityTearDown]`: 状態で分岐する（`VfxPlayTestFixture` と同じ）。`try` の中で、作った GameObject を存在を確かめて破棄 → Cursor を保存済みなら復旧 → **`EditorApplication.isPlaying` のときだけ** `yield return null` と `yield return new ExitPlayMode()`（SetUp が Play に入る前に失敗したときに ExitPlayMode の例外で元の失敗を上書きしない）→ Scene 構成を保存済みなら復旧。`finally` で、変えた環境変数だけを元の値に戻す。終了時は EditMode に在る。Play から出られなかったときは、その事実を失敗の文言に残す。
  - 待機はフレームで、時間の待ちは使わない。`[UnityTest]` と `EnterPlayMode` は既存のテスト（`SampleGame.Tests` の `SessionSeasonControllerTests` は `[UnityTest]`、`VfxPlayTestFixture`（`SampleGame.Tests`）と `SampleGame.Tests.Editor` の `AttachedAssetDirectoryIntegrationTests` は `EnterPlayMode`）が既に使っており、asmdef の参照は足さない。
  - RED の実行（発見 C）も同じ fixture で走るので、失敗しても Editor は EditMode・元の Scene・元の環境変数に戻る。RED の判定は、結果 XML の当該 test-case が `result="Failed"` で、失敗の文言が (ii) の assertion であること（setup の失敗・Inconclusive は RED に数えない）。
- 差し戻し中の起点 `-Filter`: `SampleGame.Tests.Streaming.FlyControllerTeleportTests|OneStarMaker.Tests.Streaming.PlayerWorldReadySequenceTests|SampleGame.Tests.Editor.Build.PublishedDirectoryReceiveTests|SampleGame.Tests.Editor.Build.PurposeLaunchSettingsTests`（`FlyControllerTeleportTests` の namespace は B が決める。他の 3 件は実物の namespace）。
- B 限定の Unity テスト: 適用しない（H2 未適用）。AC1 の修正前の失敗は、発見 C が B のテストだけの commit を detached worktree で `pwsh tools/run-tests.ps1 -Filter <FlyControllerTeleportTests の完全名>` で 1 回走らせ、failed 1 を確かめる。
- 判定必須テスト（GO 候補 head）: EditMode 全件回帰（空 filter）。run 全体の `result="Passed"` は Inconclusive を含んでも立つ（S-4d5b の日の XML は `inconclusive=25` で `Passed`）ので、それだけで合格にしない。`FlyControllerTeleportTests` の全ケースと `PublishedDirectoryReceiveTests` の改名後のテストが、test-case ごとに `result="Passed"` で在ること（Inconclusive・Skipped・欠落は NO-GO）を XML から機械的に確かめ、件数と名前を記録する。
- 全 EditMode 回帰の適用除外: なし
- 操作・実行時・目視条件の検証経路:
  - **動作確認（B の後、Build なし）:** 実行担当は Phase B 以外の担当。ローカルの Unity Editor（`tools/unity-editor.cmd`）。directory mode で、空 Scene から、install 済みの `spring-planner-whitebox` の revision（S-4d5b の identity。`unity/` の製品コードは content に入らないので修正後の head でも使える）を `PurposeLaunchSettings.DirectoryPlay` → `ContentDeliveryPlayBridge.ApplyForOnePlay` → `EnterPlaymode` で起動し、Title が載ったら `start-button` へ `NavigationSubmitEvent` を送って Start する。`dirplay` の前の eval で `ac6reg.cs`（scene の load / unload の hook）を Play に入る直前に掛かるよう仕込む（`playModeStateChanged` の `ExitingEditMode` で登録する。決定記録 D1）。あわせて `EditorSettings.enterPlayModeOptions` の値を eval で読んで記録する。Play に入った後、Editor.log で `handler-registered` の後に `loaded Title` が在ることを確かめる。`Player ready` の行は App のログ（JSON の rolling file）に出るので、Editor.log ではなく App のログを poll して待ち、行が出たらすぐに位置（`obs`）を 1 点、その 0.5 s 以上後にもう 1 点と Scene の一覧を取る（Start の後 3 分出なければその時点で取り、期待との差として記録する）。Play 停止の eval（`stop.cs`）は、登録した `sceneLoaded`・`sceneUnloaded` の handler を外す（domain reload が無効なので、外さないと次の Play に残る。delegate は `AppDomain.CurrentDomain.SetData` 等で保持する）。この bridge と検証 Play は手で戻す ledger（Delivery 窓の Use For Next Play）が残っていると拒否するので、拒否の文字列が返ったら eval で `ContentDeliveryPlayBridge.Reset()` を呼んで 1 回だけやり直し、その事実を記録する。directory の revision は A1 と同じ実体（`dirplay.cs` が固定する Delivery 窓の cache root の `installed/spring-planner-whitebox/20261103T091410930Z-…`。別の PC では path を書き換える）を使う。観測: rig が spawn から 1 m 以内、`Spring_Cell_0_0` が載らない、App のログに `Player ready at Cell stream spawn` の行。eval の C# は A1 の `artifacts/bs-rig-spawn-a1/eval/`（`click.cs`・`obs.cs`・`dirplay.cs`・`newscene.cs`・`stop.cs`）と S-4d5b の `ac6reg.cs` を使う。`ac6reg.cs` は `handler-registered` の行を `[d5b-scene] handler-registered utc=<UTC ISO 8601> ft=<Time.frameCount>` に変える（`W_all` の App のログの開始時刻にする。F4-1）。ただし `click.cs`・移動と trigger の eval・Play 停止の eval は、送った時点の UTC 時刻と `Time.frameCount` を持つ marker 行（`[bs-rig-marker] <名前> utc=… ft=…`）を `Debug.Log` に出すように足す（判定 C の窓に使う）。この動作確認で疎通させるもの（届かなければ Phase A に戻す）: E1 の `IsSceneStable("InGameSession")` の eval、`[d5b-scene] handler-registered` が `[d5b-scene] loaded Title` より前に出ること。
  - **判定 C（GO 候補 head、Build あり）:** 実行担当は Phase C。Editor を起動する前に `git rev-parse HEAD` が候補 head で、`git status --porcelain` に `artifacts/` 以外の差分が無いことを記録する（Play の間に作業木を変えない）。Editor を閉じた状態から起動し、untitled の空 Scene から検証 Play `spring-planner-whitebox`（`VerifyPlayLauncher.VerifyAsync(…, interactive: false)`）を 1 回行う。Start は `start-button` への submit。`VerifyAsync` を呼ぶ前の eval で、`ac6reg.cs`（scene の load / unload の hook）を Play に入る直前（`ExitingEditMode`）に掛かるよう仕込み、`EditorSettings.enterPlayModeOptions` の値を記録する（決定記録 D1。手順は動作確認で疎通させた版。`handler-registered` の後に `loaded Title` が在ることを確かめ、`stop.cs` で handler を外す）。同じ Play で S-4d5b の判定 C と同じ手順（`artifacts/s-4d5b-phase-c-verdict/eval/` の `mv_above42.cs`・`mp_local.cs`・`mp_start.cs`・`mp_done.cs` 等をそのまま使う。長い距離の移動は `mv_*.cs` の Rigidbody と transform の二重書き、trigger への進入は `mp_*.cs` の `MovePosition`。trigger は Rigidbody の進入で数えるので、`mp_*` を二重書きに書き換えない）で E2 を確かめる（合否は §2 の「AC4 の詳細」）。検証 Play の前に、Delivery 窓の cache root（`ContentDeliveryWindow.LoadSettings().cacheRoot`）が空でないこと（空なら既定 root へ install して AC5 の一覧を乱す）を eval で読んで記録する。Play を止めた後に、Editor.log の `[VerifyPlay] installed … (cache root …)` の行の revision root の残存を pwsh の列挙で確かめる（E3 の (b)）。eval は動作確認で疎通させた版を使い、evidence bundle に入れる。保存する証拠: Editor.log の Play の区間、App のログ、`preflight.json`・`transport.json`・`outcome.json`、eval の出力、照合 script の出力、`SHA256SUMS.txt`。受け渡し: 判定 C の evidence bundle（U4 の裁定に従う。下の「evidence bundle」）。
  - **AC5 の前後の一覧:** 判定 C の全件回帰の直前と直後に、既定 cache root の root 自身を含む再帰の一覧（絶対 path・作成時刻・更新時刻）を同じ pwsh の列挙で取る（Claude の Bash は LocalAppData が仮想化されるので pwsh で取る）。
- **evidence bundle（C と C' の自己完結性）:** A1・S-4d5b の一次証拠は git 管理外で別の PC から参照できない（U6）。判定 C の bundle に、使った eval の C#、照合 script、原記録（Editor.log の区間・App のログ・json・一覧）、`SHA256SUMS.txt`、候補 head、content の revision、使った絶対 path、実行担当、未実行の項目を入れる。C' はこの bundle だけで照合を再実行できることを確かめ、bundle に無いものを「再現可能」と書かない。
- 未知の操作経路の疎通結果: A1 で、Play As の eval 起動、directory mode の bridge の eval 起動、`start-button` への submit、毎フレームの位置の hook が動くことを確かめた（`artifacts/bs-rig-spawn-a1/raw-observations.txt`）。Addressables mode は空 Scene から Title が載らない（Play-from-any-scene の仕様。Title を開いて Play する）。検証 Play の build は S-4d5b で疎通済み。
- 人間の判断が必要な条件: なし
- 高コスト証拠: なし（Player Build・実 R2 を使わない。content の build は判定 C の通常の手順）
- 判定 C の照合 script の方式: **判定 C の offline 実行。** 判定 C の担当が、保存した Editor.log の区間と App のログに対して数える script を書いて自分の環境で実行する（資格情報・通信に触れない log の解析だけ）。
  - **run の範囲:** 判定 C は Editor を閉じた状態から起動するので、その Editor.log は 1 回の run だけを含む。`[VerifyPlay] verify spring-planner-whitebox: Play requested.` と `[VerifyPlay] installed …` が各 1 件でなければ（複数の run の混在）計数不能。
  - **判定の窓:** Editor.log の行には時刻が無いので、境界は eval の marker 行（UTC 時刻と `frameCount` つき）と hook の行で定める。App のログは marker の UTC 時刻の間の timestamp で切る。
    - `W_all` = [`[d5b-scene] handler-registered`, Play 停止の marker]。App のログの開始はその行の `utc=` で、行に `utc=` が無ければ計数不能（F4-1）: `Spring_Cell_0_0` の load（診断の経路の spawn・`(1125,80,625)`・各 trigger（S-4d5b で x 1100〜1375、z 580〜660）はどれも `(0,0)` の Cell から load 距離の外なので、診断の後も 0 件が正しい。遅れて完了した load を落とさない。F4-3）、Cell 単位の Environment・Lighting・VFX の load、封鎖の拒否、`ContentDeclarationMismatch`、App の `Error` 以上（観測できた行）を**診断の移動の後も含めて全区間で** 0 件（V3-2）。診断の移動に伴う源流の Cell の unload や `4_2` の再ロードの揺れは数えない。
    - `W_spawn` = [submit の marker, 最初の診断の移動の marker): `Spring_Cell_0_4` の unload が 0 件（診断の移動の後の unload は正常なので `W_spawn` に限る）。`Spring_Cell_0_0` は上の `W_all` で、Editor.log の hook の行と App のログの `OnStabledImpl Spring_Cell_0_0` の両方を数える。
    - VFX の出現と回収は、各 trigger の eval の marker から 30 秒の上限まで（「AC4 の詳細」）。
  - **存在必須の正の証拠（無ければ NO-GO）:** `handler-registered` 1（`loaded Title` より前）、submit の marker、`IsSceneStable("InGameSession")` の eval の戻り `true`、App のログの `OnStabledImpl Spring_Cell_0_4`、`Flight registered`、`Player ready at Cell stream spawn`（文言と 3 成分の存在だけを数え、値を rig の位置として使わない）、`Addressables backend sealed` 1、`Editor content directory first scene: … stable=True` 1、AC3 の位置の eval の 2 点。封鎖の拒否の文言と Cell 単位の名前の正規表現は B が製品の文字列と `SceneResourceMap` の実名から取り、§6 に書く。
  - **計数不能は NO-GO:** 対象ファイルの欠落、解析できない行、run の混在、候補 head と記録した `HEAD` の不一致、作業木の差分。
  - **出力:** 対象ファイルの path と sha256、窓の境界の行番号と UTC 時刻、各計数の値と該当行の行番号。人が原記録で照合し直せる形にする。C' は script を再実行するだけでなく、原ログで `W_all`・`W_spawn` の境界と正の証拠を独立に照合し、Cell 単位の名前の正規表現と Error の判定式をレビューする。script の結果との不一致は NO-GO（F4-4。合成 fixture や別実装の script は作らない。S14）。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。
- A0/A1 主担当・モデル・ベンダー: Anthropic（Claude Code、本 session）。
- A2 独立レビュー: 下の §5.1。
- A3 統合担当・モデル: Anthropic（Claude Code、本 session）と発注者。
- C' 用に予約した担当: OpenAI（Codex）。初稿では xAI（Grok）を C' に予約していたが、A2 のアーキテクチャゲートで Codex が利用上限で応答せず Grok に替えたので、予約を Codex に移した。Phase C は Phase B と異なるモデル、C' は B・C と異なるモデルとする。
- 独立性の強化条件: risk が `normal` なので適用しない。

### 5.1 Phase A のレビューの記録

- **A2 入力版:** 未 commit の版。HANDOFF の内容 sha256 `31ffaeab2956efd75c5de22c53169722847de29ddb060dc3fd2355977fa8d00f`（2026-11-04、A1 を書き終えた時点）。アーキテクチャゲートの packet（HANDOFF の写し、`architecture-gates.md`、`AGENTS.md`、関係するソースの写し、A1 と S-4d5b の eval と観測の抜粋）は manifest の sha256 `a7fb39895fd6cb9c801eafe9ce97ad760ca3dd3f95abb725d25925f58f2b7211` で固定した。packet の要点と回答の写しは `artifacts/bs-rig-spawn-a1/a2/` に在る（Artifact Storage は使わない。U4）。
- **A2 (1) 目的レビュー:** Anthropic（Claude Code の新規 subagent。ファイル・ツールを使わない指示で、目的と利用者の 2 欄の原文だけを渡した）。入力の sha256 `42c409dbace24853bc6899191f4ed56ad60fa57dbcf8ee2fabd0444de0a98c16`。
- **A2 (2) アーキテクチャゲートと不変条件の破り方・検証経路の実行可能性:** xAI（Grok、cursor-agent）。OpenAI（Codex）で始めたが利用上限で応答が無く、Grok に替えた。1 回目は起動の誤り（Bash の PATH に cursor-agent が無く実行されていない）、2 回目は前景実行の 600 秒の上限で書き出しの前に打ち切られ、同じ session を `--resume` で続けて書かせた（調べ直しはさせていない）。計画の枠（§3・§4 は実装済みと仮定）を指示に固定した。blocker 0 件、非阻害 11 件、後続入力 1 件。

- **A2 (3) 外部監査（追加、発注者の指示）:** 入力は A3 統合中の版から §5.1 を省いた写し `artifacts/bs-rig-spawn-a1/a2b/HANDOFF-for-audit.md`（未 commit の版、内容 sha256 `029680c540237d724fe0a04aefd0be5b2bd824548b3e6f8aa97358951aef5004`。写し元の HANDOFF は `ae05640786ad09e52d1b46307a66d4f0dfed38b384780a96eaa0a5bfc4f2d2fd`）。他のレビューの所見と採否は渡さない。結果は発注者が受け取った文書 `BS_RIG_SPAWN_PLANNER_PROOF_REVIEW.md`（2026-11-04、内容 sha256 `6f0fff1caa08ac34c916a2e4ce935a6ba2f0b40a57b44a244d9f4e939e531797`。git 管理外）。担当・モデル: OpenAI（ChatGPT の Web 版。発注者が別 session で実施）。C' に予約した OpenAI（Codex）と同じベンダーだが、A2 (3) は計画のレビューで、C' は B・C と異なるモデルという §5 の条件に触れない。判定は「A3 凍結前に要修正（設計全面差し戻しではない）」、要修正 R1〜R4、要確認 R5〜R10。Unity の実行は伴わない静的レビュー。
- **A2 (3) 外部監査の第 2 回（2026-11-04、発注者の指示）:** OpenAI（ChatGPT の Web 版）。入力は第 1 回の反映後の版。結果は `BS_RIG_SPAWN_PLANNER_PROOF_REVIEW_V2.md`（内容 sha256 `9597cc870ed6499b1edbdd60c599886b33b1e1d38131340ace03fa4b6ff09c3f`。git 管理外）。判定は「基本方針を支持。A3 凍結前に要修正（全面差し戻し不要）」、R1〜R10 は概ね解消、新規 V2-1〜V2-7。静的レビュー。
- **A2 (3) 外部監査の第 3 回（2026-11-04、発注者の指示）:** OpenAI（ChatGPT の Web 版）。入力は第 2 回の反映後の版。結果は `BS_RIG_SPAWN_PLANNER_PROOF_REVIEW_V3.md`（内容 sha256 `02df735decdf10cd3c3c8a0e057d8d38b553ec7a6e3cbdee85fd106035b34837`。git 管理外）。判定は「基本設計は支持。V3-1・V3-2 の修正後なら A3 凍結に進んでよい」。全 Player Loop の観測基盤へ広げない（スライスが大きくなりすぎる）との指摘を含む。静的レビュー。
- **A2 (3) 外部監査の第 4 回（2026-11-04、発注者の指示）:** OpenAI（ChatGPT の Web 版）。入力は S14 の反映後の版。結果は `BS_RIG_SPAWN_PLANNER_PROOF_REVIEW_V4.md`（内容 sha256 `8a7736a45e2ae0496ad8d18e1d700d7ff639be6e0f38b11fb9593979d6cd1bef`。git 管理外）。判定は「S14 による簡素化を支持。F4-1・F4-2 を直してから凍結を推奨。全面差し戻し不要」。新しいテスト・観測の仕組み・内部 API の公開は求めない、と明記。静的レビュー。
- **A3 凍結（2026-11-04）:** 発注者の指示で、F4-1〜F4-5 の反映後の版を凍結した。
- **A3 の主担当の自己点検の第 2 回（同日、V2 の照合と同時）:** 成功条件の各項目を、何のログ・ファイル・eval で観測するかまで遡って確かめた。下の表の S10〜S13。
- **A3 の主担当の自己点検（2026-11-04、外部監査の照合と同時）:** 外部監査の各指摘をソース（`cea28ee4`）と A1 の一次証拠に照らし、あわせて Phase C / C' で崩されうる点を洗った。下の表の S2〜S9。

A3 の採否（2026-11-04 凍結）:

| ID | 出所 | 指摘 | 採否 | 理由と反映 |
| --- | --- | --- | --- | --- |
| P1 | 目的 | 「Planner の World が使える状態」を観測できる判定にする | 採用 | AC3 に、`Player ready` の後に入力 ON を eval で取ることと、`Player ready` は入力 ON の後に書かれることを足した。新しい判定項目は足していない（§18 の条件のまま） |
| P2 | 目的 | AssetDatabase ではなく Content Directory から読まれた結果である | 採用 | AC4 に封鎖の行・初回 Scene の行・封鎖の拒否 0 件を足した（S-4d5b の照合と同じ項目） |
| P3 | 目的 | 寿命の判定を、書かれた寿命の契約と照らす。正本が在るか不明 | 採用 | 正本は §13「DIST transport と disk cache の境界」に在った。§1 と E3 を、§13 を参照して新しい契約を書かない形に直した |
| P4 | 目的 | 寿命が W-6 と矛盾したら S-5 へ明示する | 採用（既存） | 矛盾しないと判定した（§1）。E3 で §18 に書く |
| P5 | 目的 | 専用の起動経路・Start を飛ばして rig を置く仕組み・新しい入力・W-6 の機構・installed の設定・汎用の監視を作らない | 採用（既存） | 計画はどれも作らない。動作確認の eval の bridge は検証の支援で、製品の経路ではない。判定 C は製品の検証 Play を使う |
| P6 | 目的 | 空になった時点・誤りなら直すか・2 つの実証の関係が曖昧 | 採用（既存） | A1 で既定 root にテストの跡が在ることを特定した（元の観測との因果は最有力の仮説。§1。2026-11-04 の第 2 回の外部監査 V2-7 で文言を直した）。直すのはテストだけ（U1）。2 つの実証は独立で、Planner Play は検証 Play、寿命はコードと cache root の観測で答える |
| G1 | ゲート | `SampleGame.Tests` の asmdef に `UnityEngine.TestRunner` が無く、`[UnityTest]` に asmdef の edge が要る | 不採用 | 同じ asmdef の `SessionSeasonControllerTests` が既に `[UnityTest]` を使っている（`UNITY_INCLUDE_TESTS` と `nunit.framework.dll` を持つテスト asmdef は Test Runner を参照できる）。`EnterPlayMode` も `SampleGame.Tests.Editor` が使っている。§5 に根拠を書いた |
| G2 | ゲート | AC1 の「修正前に失敗する」を実行できる担当が居ない | 採用 | B はテストだけの commit を切り、発見 C がその commit で filter を 1 回走らせる形にした（§4・§5・AC1） |
| G3 | ゲート | `[UnityTest]` の準備の順序（Play に入る前の GameObject は破棄される） | 採用 | §5 に順序を書いた |
| G4 | ゲート | ログの欠落のプローブの戻りと 22 file の列挙が packet に無い | 採用 | `eval-returns.txt` と `app-log-line-counts.txt` を一次証拠に足した。§18 では機構と事実を分けて書く（AC6） |
| G5 | ゲート | `autoSyncTransforms` の値の引用元が無い | 採用 | hook の戻りを `eval-returns.txt` に残し、§1 から引いた |
| G6 | ゲート | reflection と submit の同等は A1 で再実行していない | 採用 | §1 の文を、`TitleView.cs` 67 行の結び付けと症状の一致に限り、reflection は S-4d5b の 1 回だけと書いた |
| G7 | ゲート | テストの時刻の一致は XML が packet に無い | 採用（証拠の所在） | XML の写しは `artifacts/bs-rig-spawn-a1/` に在る（packet に入れていなかった）。§1 に所在を書いた |
| G8 | ゲート | 判定 C の移動の eval を二重書きに揃えると trigger の進入が変わりうる | 採用 | `mv_*` は二重書き、`mp_*` は `MovePosition` のまま使うと §5 に書いた |
| G9 | ゲート | ログの値は spawn の定数で、rig の位置ではない | 採用 | AC2 と照合 script を、文言と 3 成分の存在だけを数える形にし、1 m は eval の `Rigidbody.position` で数えると書いた |
| G10 | ゲート | 2 回目以降の回帰では path と作成時刻が変わらず AC5 の失敗経路が見えない。検証 Play が既定 root に install すると一覧が乱れる | 採用 | I2 と AC5 に更新時刻を足し、一覧の間に全件回帰だけを置き、検証 Play の前に窓の cache root を記録すると書いた |
| G11 | ゲート | 15 秒の一点、照合の窓の開始、ledger の拒否、directory の path | 採用 | `Player ready` の後に取り 3 分で失敗、窓の開始を submit の行と `Flight registered` に固定、ledger の拒否は Reset して 1 回やり直す、path は A1 と同じ実体と書いた |
| G12 | ゲート | I2 の探索境界の外（`tools/`・`OneStarMaker/Tests` の全体） | 採用（B の作業） | B が grep し、`AcquireTransaction` を含めて §6 に残す、と I2 に書いた |
| S1 | 主担当 | AC7（Addressables mode でも spawn に置かれる）は目的の外の mode の確認で、受け入れ条件に別の問いが混ざる契機に当たりうる | 採用（発注者の裁定 U5） | AC7 と動作確認の Addressables mode の手順を外した |
| R1 | 外部監査 | E3 は元の観測の因果を言い切れない（絶対 path が無い）。「DIST は revision を消さない」の一般化は不可 | 採用 | §1 を事実・最有力の仮説・不明に分けた。E3 を (a)〜(d) の新しい観測で閉じる形にし、寿命は「容量確保と明示の Evict でだけ消える」条件つきで書いた。照合で、`default-cache-root-listing.txt` に root 自身の行が無いのに §1 が root の作成時刻を書いていた誤りも見つけ、直した |
| R2 | 外部監査 | AC1 の Play のテストに隔離・後始末・失敗時の復旧が無い | 採用 | `AppInitializer` が Play 入りで必ず起動すること、`FlyController` が既定で入力 ON のため `OnEnable` で Cursor をロックすることをコードで確かめた。§5 を `VfxPlayTestFixture` と同じ原則の `[UnitySetUp]`/`[UnityTearDown]` にし、空 Scene・content mode・DebugSocket・`fixedDeltaTime`・Cursor・環境変数・Scene 構成の保存と復旧を書いた |
| R3 | 外部監査 | Editor.log と App のログの窓の開始の不一致、正の証拠の欠如、検査不能時の扱い | 採用 | 窓を UTC の marker で両ログ共通にし、正の証拠の列挙・計数不能の NO-GO を §5 に足した（S6 も参照。合成 fixture は S14 で外した） |
| R4 | 外部監査 | 物理テストは即時と補間後の時点を分ける | 採用（S2・S3 で強化） | AC1 を (i) 即時と (ii) 毎フレームに分けた |
| R5 | 外部監査 | `ApplyLook` の rotation も補間で戻りうる | 不採用（S14） | 製品の Teleport は yaw 0、マウス操作中は毎 Update に絶対値で書き直す。S-4d5b の未達の条件に無いので観測しない。症状が出たら §18 §4 へ |
| R6 | 外部監査 | 旧 AC20 の install の保証が消えることを明示 | 採用 | AC5・AC6 に保証の縮みと役割分担を書いた |
| R7 | 外部監査 | AC5 の観測者が正しい root を見たことを証拠化 | 採用（S14 で縮小） | 列挙した絶対 path の記録と、root が無い場合の区別だけにした。Unity の `ResolveCacheRoot` との照合は S14 で外した |
| R8 | 外部監査 | C / C' の evidence bundle の自己完結性 | 採用 | §5 に evidence bundle の中身と C' の再実行の条件を足した。U6 に、保留の間は一次証拠を別の PC から参照できない前提で扱うと書いた |
| R9 | 外部監査 | ログの欠落の下での `App Error=0` の意味を限定、後続の所有先を固定 | 採用（所有先は U7） | AC4 と §1 を「観測できた行の Error が 0」に限定し、Editor.log の補助の計数を足した。所有先は発注者の裁定で Foundation の Logging（§12 §3.2 のギャップの行、U7） |
| R10 | 外部監査 | 停止規則が NO-GO で終われない表現 | 採用 | 判定定義に不明・計数不能を NO-GO として足し、停止規則を「証拠を保存して NO-GO で終了し Phase A に戻す」に直した |
| S2 | 主担当 | A1 の `ft=4810`→`4811` は 67 ms で fixed step が走ったはずなのに戻っている。§1 の機構の説明（と `autoSyncTransforms=false` の引用）では説明できない | 採用 | §1 を観測と公式の説明だけに留め、未解明の点を「説明の及ばない点」として書いた |
| S3 | 主担当 | 最初の Tick は Teleport の次のフレームで Focus を読む。補間が古い姿勢を書けば終点の判定では見えない | 採用（S14 で縮小） | AC1 (ii) を毎フレームの判定にした。検証 Play の毎フレームの hook は S14 で外し、Cell の load / unload の 0 件で Tick の結果を見る |
| S4 | 主担当 | fixed step の位相次第で修正前のテストが偶然通り、RED が出ない | 採用（S14 で縮小） | `WaitForFixedUpdate` の明けに呼び、RED が出なければ Phase A に戻す。位相のケースは S14 で外した |
| S5 | 主担当 | 全件回帰の run は Inconclusive を含んでも `Passed` になる | 採用 | §5 の判定必須テストを test-case ごとの `result="Passed"` の機械検査にした。RED も test-case の `Failed` と assertion の文言で判定する |
| S6 | 主担当 | Editor.log の行に時刻が無く、App のログの窓の終わり（診断の eval の行）が定まらない | 採用 | eval に UTC 時刻と `frameCount` の marker を足し、両ログの窓をそれで定めた（§5） |
| S7 | 主担当 | 「既定 root に他の書き手が居ない」は探索が終わっていない否定の主張 | 採用 | I2 と §1 で、B の探索が終わるまで根拠に使わないと書いた |
| S8 | 主担当 | 「`ZLog*` に位置の値を渡すのは 1 箇所だけ」の grep のコマンドが残っておらず、`Vector3` 以外の値型も調べていない | 採用 | B が grep し直し、コマンドと一覧を §6 に残すと書いた |
| S9 | 主担当 | `hook.cs` は `EditorApplication.update` で採っており player loop の毎フレームではない | 解消（S14） | 判定に毎フレームの hook を使わなくなった。§1 の A1 の観測の記述にだけ残す |
| V2-1 | 外部監査 2 | `manifestPointer` と installed revision の取得元の取り違え | 採用（S14 で縮小） | install 先は Editor.log の `[VerifyPlay] installed` の行から取る。`manifestPointer` を revision の一致に使わない。bridge・transport・receipt の sha256 の突き合わせは S14 で外した |
| V2-2 | 外部監査 2 | `fixedDeltaTime` の指定だけでは位相を実証できない | 取り下げ（S14） | 位相を作り分けて数える仕組みをやめた（S4 の行） |
| V2-3 | 外部監査 2 | I1 の「全 Tick の入力」は今の記録では証明されない | 主張を取り下げ（V3-1・S14） | 全 Tick を証明する主張そのものを落とした |
| V2-4 | 外部監査 2 | SetUp の失敗時に ExitPlayMode を無条件に呼ぶ | 採用 | `EditorApplication.isPlaying` のときだけ出る、保存済みのものだけ戻す形にした（`VfxPlayTestFixture` と同じ） |
| V2-5 | 外部監査 2 | rotation の記録だけでは非ゼロの yaw を刺激しない | 不採用（S14） | 回転は観測しない（R5 の行） |
| V2-6 | 外部監査 2 | 入力 ON のテストは外部入力に依存する | 採用 | AC1 は入力 OFF で行う（`SampleGame.Tests` は Input System を参照しないので入力の無いことを検査できない）。入力 ON の後は AC3 で確かめる |
| V2-7 | 外部監査 2 | P6 に確定の表現が残る | 採用 | P6 を因果の限定と揃えた |
| S10 | 主担当 | E1 の「`InGameSession` が Stable」はどのログにも出ない（`OnStabledImpl` を出すのは Cell・Season・Lighting の Scene だけ）。正の証拠に挙げた項目が観測できない | 採用 | E1 に各項目の観測元を書き、`InGameSession` は eval で lifecycle の状態を読む形にした（動作確認で疎通、届かなければ Phase A）。`Spring_Cell_0_4` は `OnStabledImpl Spring_Cell_0_4` |
| S11 | 主担当 | AC3 の「Editor.log の scene の load / unload の行」は製品のログではなく S-4d5b の `ac6reg.cs` の hook が出していた。§5 の判定 C の手順にその hook が無い | 採用 | 判定 C で Play に入った直後に `ac6reg.cs` を掛け、`handler-registered` を正の証拠にした。`Spring_Cell_0_0` は App のログの `OnStabledImpl` でも独立に数える |
| S12 | 主担当 | 「S-4d5b の AC6・AC7 と同じ検査」「同じ手順」は、S-4d5b の照合 script が bundle に無く（出力だけ）再現できない。VFX の「出現と回収」の合否も定義が無い | 採用 | §2 に「AC4 の詳細」を足し、選択・ロードの名前の規則、VFX の出現と回収の観測・時間の上限、窓の分け方を書いた。名前の正規表現と封鎖の拒否の文言は B が製品の実名・文字列から取って §6 に書く |
| S13 | 主担当 | 判定 C の Play が候補 head の作業木で走ったことの証拠が無い | 採用 | Editor の起動前に `HEAD` と `git status --porcelain` を記録し、不一致・差分を計数不能にした |
| V3-1 | 外部監査 3 | `EditorApplication.update` での `_lastTickFocus` の採取は全 Tick の観測を証明しない | 採用 | I1 を観測できる主張に戻した。S14 で `_lastTickFocus` と毎フレームの hook も外した |
| V3-2 | 外部監査 3 | E2 の選択外のロードと App の Error の 0 件が、診断の移動の後の区間に適用されていない | 採用 | §5 の窓を `W_all`（全区間）と `W_spawn` に分け、選択外のロード・封鎖の拒否・Mismatch・App の Error は `W_all` で、診断の移動の後も含めて数えるようにした |
| V3-3 | 外部監査 3 | `sceneLoaded` の hook は登録の前の load を記録しない | 採用 | 主張を「登録の後の load が 0 件」に限り、登録が Title の load より前に入ることを動作確認の疎通の項目にした。判定 C で順序が崩れれば NO-GO |
| V3-4 | 外部監査 3 | `InGameSession` の Stable は public の `ISceneQuery.IsSceneStable` で読める | 採用 | コードで確かめ、E1 の観測元を `IsSceneStable("InGameSession")` に固定した（`SceneIds.InGameSession` の綴り）。`SceneDirector` への到達経路は動作確認で疎通 |
| V3-5 | 外部監査 3 | verify・install の行は Play の前に出るので run の境界が要る | 採用（軽い方） | レビューの代案どおり、Editor を閉じた状態から起動した Editor.log は 1 回の run と扱い、`Play requested`・`installed` が各 1 件でなければ計数不能とした。`run-begin` の marker は置かない |
| S14 | 主担当（発注者の問い「成功条件の厳密さを過剰に達成しようとしていないか」） | 第 2・3 回の監査への対応で、S-4d5b の未達の条件を満たすことより、観測の仕組みそのものの厳密さを示す方へ広がった | 採用 | 判定の主張を S-4d5b の未達の条件と E2 の原文に戻した。外したもの: AC1 の位相のケース A/B と回転のケース C、検証 Play の毎フレームの hook と `_lastTickFocus`、`W_run`・`run-begin`、run identity の sha256 の突き合わせ、合成 fixture の一式、E3 と AC5 の Unity との path の照合。残したもの: E1・E2 の原文の判定、AC1 の毎フレームの判定と RED、`W_all` の全区間の負の証拠（V3-2）、`IsSceneStable`（V3-4）、hook の登録順（V3-3）、HEAD と作業木の記録 |
| F4-1 | 外部監査 4 | `handler-registered` の行に UTC 時刻が無く、`W_all` の App のログの開始を決められない | 採用 | `ac6reg.cs` を確かめると時刻は eval の戻り値（ローカル時刻）にしか無かった。行を `handler-registered utc=… ft=…` に変え、App のログの `W_all` の開始をその時刻にし、無ければ計数不能とした |
| F4-2 | 外部監査 4 | I1 の冒頭と根拠文に全称の主張が残る | 採用 | I1 の見出しを「rig が維持され、原点の側の Cell への切り替えが観測されない」にし、根拠文を「持続的な原点への戻りなら通常は表れるので再発の検知の指標にする」に限った。試験の内容は増やしていない |
| F4-3 | 外部監査 4 | `0_0` の load の完了が診断の移動の後に遅れて出ると `W_spawn` で落ちる | 採用 | `IssueAdd` が完了を別に観測することをコードで確かめた。診断の経路（spawn・`(1125,80,625)`・S-4d5b の trigger の中心 x 1100〜1375）はどれも `(0,0)` の Cell から load 距離 375 m の外なので、`0_0` の load だけを `W_all` で数える。`0_4` の unload は `W_spawn` のまま |
| F4-4 | 外部監査 4 | C' の同じ script の再実行は再現性だけで、正しさを示さない | 採用 | C' が原ログで窓の境界と正の証拠を独立に照合し、正規表現と Error の判定式をレビューし、不一致は NO-GO と書いた |
| F4-5 | 外部監査 4 | 「0.5 s 後」は Tick の 2 回を実測した証拠ではない | 採用 | 「設定上の Tick の周期より十分長い時間の代わりの指標で、Tick の回数は数えない」と書いた。あわせて §7 の「認めてよい制限」に沿い、AC5 と E3 (b) の主張の範囲を限った |

## 6. Phase B 実装結果

- AC1 のテストだけの commit（修正前。発見 C が RED を確かめる対象）: `40fcdb34dd6840dfe0457d4a2930db63c6f4a897`。`unity/Assets/SampleGame/Tests/Streaming/FlyControllerTeleportTests.cs`（と `.meta`）だけを足した。Editor のコンパイル確認済み（`EditorUtility.scriptCompilationFailed=False`、型 `SampleGame.Tests.Streaming.FlyControllerTeleportTests` が `SampleGame.Tests` に在る）。
  - テストの完全名: `SampleGame.Tests.Streaming.FlyControllerTeleportTests.Teleport_InterpolatedBody_KeepsTargetPositionEveryFrame`。namespace は asmdef の `rootNamespace` と §5 の起点 filter に揃えて `SampleGame.Tests.Streaming` にした。
  - RED の判定に使う文言: (ii) の失敗は `Teleport の <n> フレーム後（frameCount=<f>）に位置が目標 (…) から外れた: transform=(…) rigidbody=(…)`。(i) の失敗は `Teleport の直後の Position … が目標 … から外れている`。setup の前提の失敗は `起動の失敗` または `前提:` で始まり、RED に数えない。
- 実装（implementation head `a79530b4302fdb3728220551cee5642de4662414`）:
  - `FlyController.Teleport`: `_body.position` を目標に書き、速度と角速度を 0 にしてから `transform.position` を書く（§4 の方針どおり。`MovePosition` は使わない）。signature は変えていない。回転は既存の `ApplyLook` のまま。
  - `PlayerScene`: `Player ready at Cell stream spawn ({spawn.x:F1}, {spawn.y:F1}, {spawn.z:F1})`。値は 3 つの `float`。文言は変えていない。
  - `PublishedDirectoryReceiveTests`: `EmptyCacheRoot_InstallsUnderTheDefaultRoot` を `EmptyCacheRoot_ResolvesToTheDefaultRoot` に改名し、install をやめて `stages.CacheRoot == ResolveCacheRoot("")` だけを確かめる（`ReceivedStages` の構築は `ResolveCacheRoot` で path を決めるだけで書き込まないことを `VerifyPlayLauncher.cs` 379〜387 行で確認）。teardown の既定 root の後始末（`_defaultCacheRevisions`）は不要になったので消した。判定必須テストの「`PublishedDirectoryReceiveTests` の改名後のテスト」はこの名前。
  - 文書（AC6）: Architecture §18 §4 の S-4d5b の行（原因、`Player ready` の欠落、再検証は未実施）・BS5b-2 の「テストの隙間」と「未調査の観測」（cache root の観測を E3 (a) の事実・最有力の仮説・不明の形で書き、寿命は §13 を参照、W-6 と条件つきで矛盾しない、AC5 の保証の縮み）・「検証の残り」（原点に留まる観測の文を消した）、program §7 の行（進行中）、§05 §5.13（再検証は未実施）、§12 §3.2 のギャップ G5（U7）。検証 Play の結果はまだ無いので、どの文書も再検証と E3 (b) を「未実施」と書いた。判定 C の後の Phase D で結果に合わせて書き直す。
  - 機械検査: `pwsh tools/contract-audit.ps1` は `AUDIT_RESULT kind=contract files=719 errors=0 warnings=0 checks=1,2,3,4,5,6,7,8,9 applicable8=False`。`pwsh tools/docs-audit.ps1` は `AUDIT_RESULT kind=docs files=110 errors=0 warnings=0 checks=1,2,3`。どちらも実装 commit の作業ツリーで実行した。Editor のコンパイル確認済み（`scriptCompilationFailed=False`、改名後のメソッドと `FlyController` の型が読み込まれている）。
- B に割り当てた調査:
  - **`ZLog` に Unity の値型を渡す箇所（§1・S8）:** 非テストの `.cs` の `.ZLog*(` の呼び出しを括弧の対応で切り出し、補間の穴を列挙した（行をまたぐ呼び出しも含む）。コマンド: `python zlog_holes.py unity/Assets/SampleGame unity/Assets/OneStarMaker`（script は `/Tests/` を含む path を除き、`\.ZLog\w*\(` から対応する `)` までを取り、`\{([^{}]+)\}` を穴とする。session の scratchpad に置いた使い捨てで、commit しない）。結果: 呼び出しは SampleGame の 57 件で、`OneStarMaker` の非テストには無い（`Foundation/Logging`・`Telemetry` の hit は設定と formatter で、呼び出しではない）。Unity の値型を渡すのは implementation base の `PlayerScene.cs` 172 行の `WorldCellCatalog.SpawnPosition()`（`Vector3`）の 1 箇所だけ（今回直した）。他の穴は `string`（Identity・`_label`・`eventIdentity`・`string.Join` 等）、`int`（`Candidates.Count`）、`float` の定数（`LoadRadius`・`UnloadRadius`）、enum（`MajorEventRejectReason`・`CellCompanionRole`）だけ。U7 へ渡す追加の箇所は無い。
  - **既定 cache root の書き手（I2・G12）:** コマンド: `grep -rnE "ResolveCacheRoot|LocalApplicationData|LoadSettings|AcquireTransaction|ContentCacheStore\(" --include=*.cs unity/Assets/SampleGame/Tests unity/Assets/OneStarMaker/Tests tools`（`bin/`・`obj/` を除く）。結果: `ResolveCacheRoot` は `PublishedDirectoryReceiveTests`（改名後は値の比較だけ）と `PurposeLaunchSettingsTests` 218・221 行（値の比較だけ）。`LoadSettings` はテストに無い。`AcquireTransaction` と `ContentCacheStore(` は `ContentDeliveryFailureMatrixTests`・`ContentHttpArtifactSourceTests`・`ContentInstallerTests`（Framework）と `BuildContentDirectoryIntegrationTests`・`ContentInstallTestOperationTests`（SampleGame）で、root はどれも `CreateTemporaryDirectory()`・`Temp()`・`Path.GetTempPath()` の下（各 file の root の代入を grep で確認）。`PurposeLaunchSettingsTests` 91 行の `new ContentDeliveryWindow.Settings()`（`cacheRoot` 既定）は `BridgeValues` の値を比べるだけで install しない。`LocalApplicationData` の hit は `tools/DebugStudio` の .NET の path policy と `ShellLayoutPersistenceTests` だけで、OSM の cache root（`OSM\ContentCache`）には触れない。既定 root に install するか transaction を取るテストは、改名前の 1 件の他に見つからなかった。
  - **Cell 単位の名前の正規表現（AC4 の詳細）:** `SceneResourceMap.asset`（`unity/Assets/OneStarMakerCommon/SceneMap/`）の `_sceneResources` の guid を `.asset.meta` で引き、各 SceneResource の identity を列挙した（`python map_identities.py <unity/Assets> <SceneResourceMap.asset>`、scratchpad の使い捨て）。457 件。Spring の Cell 単位の companion は `Spring_Environment_{x}_{y}`（54）、`Spring_Lighting_4_2`・`Spring_Lighting_5_2`、`Spring_VFX_4_2`、`Spring_Events_4_2`。Season 単位は `Spring_Lighting`・`Spring_AtmosphereVFX`。Scene の file 名は identity と同じ（例 `Spring_Cell_4_2/Spring_VFX_4_2/Spring_VFX_4_2.unity`）。Whitebox は別 identity ではなく `Spring_Cell_{x}_{y}` の representation。
    - 正規表現（Cell 単位の Environment・Lighting・VFX）: `^Spring_(Environment|Lighting|VFX)_[0-9]+_[0-9]+(:.+)?$`。`(:.+)?` は付随 asset の identity（S-4d5b の `preflight.json` に `Spring_Events_4_2:Burst` の形が在る）を同じ Scene の物として数えるため。`Spring_Lighting`・`Spring_AtmosphereVFX` と `Spring_Events_*` には当たらない。
    - 当てる値: `preflight.json` の `requirements[].logicalKey` と、`selected[]` の `|` 区切りの 3 番目の欄（例 `osm-build-candidate-v1|15:Spring_Cell_4_28:Whitebox32:…|Spring_Cell_4_2|…` の `Spring_Cell_4_2`）。Editor.log の `[d5b-scene] loaded <name>` の `<name>`。`excluded[]` は `…|<理由>|<種別>|<値>` の形（`Spring_Cell_4_2` の Full は `…|15:Spring_Cell_4_24:Full32:…|RepresentationNotPreferred|Representation|Full`）。
  - **封鎖の拒否の文言（AC4・§5）:** コマンド: `grep -rn --include=*.cs -i "sealed\b.*\"\|\"[^\"]*seal" unity/Assets/OneStarMaker/Scripts unity/Assets/SampleGame`（`/Tests/` と `sealed class` 等を除く）。拒否は 3 箇所で、どれも `Addressables backend is sealed:` で始まる（`AssetManagement.cs` 112 行の key のロード、186 行の description からの Scene のロード、`SealedAssetBackend.cs` 46 行の address のロード）。照合はこの部分文字列で数える。封鎖の正の行は `AbstractApplicationInitializer.cs` 247 行の `[AppInit] Content directory mode: Addressables backend sealed after reading the configuration.` で、`is sealed` を含まないので拒否の計数に当たらない。`AssetManagement.cs` 85 行の `Cannot seal the Addressables backend while …` は封鎖そのものの失敗（拒否ではない）で、出れば封鎖の正の行が出ない。
- HANDOFF との差:
  - `FlyControllerTeleportTests` は 182 行（§3 の予想は約 110）。§5 の SetUp・TearDown の各項目（環境変数 2 つ・Cursor・Scene 構成の保存と復旧、Play から出られなかった時の失敗）と、失敗の文言を区別するための前提の assertion（補間の設定、開始位置が目標から離れている）を書いた分。責務は増えていない。
  - §12 §3.2 のギャップの行は U7 では「Phase D で足す」だが、§0 の票と AC6 が B の文書の作業に含めていたので B で足した（G5）。
  - Editor の起動で、`unity/Assets/MobileDependencyResolver/Editor/` の tracked な `.pdb.meta` 5 件が消えた（Editor が自動で消した。本スライスの範囲外）。commit に入れず、Editor を閉じた後に `git checkout -- unity/Assets/MobileDependencyResolver` で戻した。
- 未実行: Unity テスト（AC1 の RED を含む）、Build、検証 Play、動作確認。どれも Phase B の担当外（§5）。
- implementation head commit: §0 の欄のとおり。
- Phase B 担当・ベンダー: Anthropic（Claude Code）。Phase C は Phase B と異なるモデルにする（§5）。

### 動作確認

- 対象 head: `a79530b4302fdb3728220551cee5642de4662414`（作業ツリーの HEAD は引き継ぐ commit `20753a501691eae13d1ae5764d9d8ce43f0bd1b3`。差分は HANDOFF だけ）。
- 条件ごとの実行担当・環境・実行したコマンド: 動作確認の実行担当（Phase B 以外の session）。ローカルの Unity Editor 6000.6.0f1（`unity open D:\repositories\unity\osm-s4d5\unity` で起動、PID 15392。`tools/unity-editor.cmd eval` で操作）。1 回だけ実行: `newscene` → `ledger` → `dirplay`（directory mode、revision `20261103T091410930Z-d377543307574cafa829b30d94255025`）→ Play に入ったのを poll で確認して `ac6reg`（hook）→ `obs` → `click`（`start-button` への submit）→ App のログで `Player ready` を確認 → `stable`（`IsSceneStable`）→ `obs` 2 回 → `stop`。ledger の拒否は出ず、`Reset()` のやり直しは 0 回。使った eval は `artifacts/bs-rig-spawn-a1/eval-b-check/`（git 管理外。`ac6reg`・`click`・`stop` は `utc=`・`ft=` の marker つきに足した版。`stable.cs` は `AbstractApplicationInitializer.ActiveVfxRuntimeHost` から reflection で `SceneDirector` へ辿り、`IsSceneStable` を呼ぶ）。Unity テスト・Build・資格情報・実 R2 は使っていない。
- 生の観測（合否は書かない）と保存先・状態: 保存先は `unity/Logs/Editor.log` と `unity/OneStarMakerLocal/Log/2026-11-04_001.log`（どちらも git 管理外。Editor 終了後に別の run で上書きされうる。実行時刻 UTC 2026-11-04 13:2x〜13:35）。
  - Editor.log: `Content directory mode: Addressables backend sealed` と `Editor content directory first scene: scene=Title … stable=True` が各 1 件（1234・1702 行）。
  - **hook の順序:** 1785〜1863 行に `[d5b-scene] loaded  (initial)`・`UIScene`・`OutGameScene`・`Title (initial)`、1889 行に `[d5b-scene] handler-registered utc=2026-11-04T13:28:02.0777363Z ft=24`。つまり `handler-registered` は Title の load より後で、Title は `(initial)` の一覧に在った。`dirplay` の返りから hook を送るまで約 14 秒（Play に入ったか 300 ms 間隔で poll した結果）。
  - submit の marker: `[bs-rig-marker] submit utc=2026-11-04T13:28:29.7128073Z ft=2143`。
  - hook 後の load / unload（Editor.log）: `unloaded Title`・`unloaded OutGameScene`、`loaded InGameUI`・`PlayerScene`・`Spring_Lighting`・`Spring_AtmosphereVFX`・`Spring_Cell_0_4`・`0_3`・`0_5`・`1_4`・`1_3`・`1_5`。`Spring_Cell_0_4` の unload と `Spring_Cell_0_0` の load は 0 件。Play 停止の marker: `[bs-rig-marker] play-stop utc=2026-11-04T13:35:54.1647111Z ft=30981`。
  - App のログ（001 の 312〜313 行）: `Flight registered`、`Player ready at Cell stream spawn (125.0, 28.0, 1125.0)`（Timestamp `2026-11-04T13:28:31.5658753+00:00`、`spawn.x/y/z` の 3 成分あり）。submit の約 1.9 秒後。
  - `stable.cs` の戻り: `IsSceneStable(InGameSession)=True Spring_Cell_0_4=True`（reflection 経由で `SceneDirector` に到達できた。eval の時刻は 22:35:42 JST ＝ UTC 13:35:42）。
  - `obs.cs` の 2 回（UTC 13:35:4x、`Player ready` の約 7 分後。1 s 間隔）: `transform=(125.00, 28.00, 1125.00) rigidbody=(125.00, 28.00, 1125.00) input=True`、scenes は上の load の一覧と同じ（`Spring_Cell_0_0` なし）。「`Player ready` の直後」の 1 点は取れていない（Start の後の待ちに 180 s の poll を使い、`Player ready` の行は Editor.log ではなく App のログにあるので poll の条件が当たらなかったため）。
  - 補助: 改名後の `PublishedDirectoryReceiveTests` や既定 cache root の一覧は取っていない（対象外）。
- 検証経路の期待と違う観測と、B への差し戻し:
  - **差 1（疎通の条件）:** 期待は「`[d5b-scene] handler-registered` が `[d5b-scene] loaded Title` より前に出ること、かつ `(initial)` の一覧に Title が無いこと」。観測は Title の `(initial)` の後に `handler-registered`（上記）。Play に入ってから eval で hook を掛ける今の手順では、Title の load より前に入らなかった（1 回。§0 の停止規則に従い、原因調査・再実行はしていない）。§5 の取り決めでは「届かなければ Phase A に戻す」ので、判定 C の `W_all` の起点の取り方（登録前の区間の扱い）は Phase A の判断を要する。製品コード（Phase B の実装）に起因する差ではない。
  - **差 2（観測の時点）:** 期待は位置の 2 点を「`Player ready` の直後」と「その 0.5 s 以上後」で取ること。観測は 2 点とも `Player ready` の約 7 分後（上記）。前の点は取れていない。
  - 上記以外（`IsSceneStable("InGameSession")` の eval の疎通、封鎖の行・初回 Scene の行、`Player ready` の文言と 3 成分、spawn から 1 m 以内の位置、`Spring_Cell_0_0` の load が 0 件）は期待と同じ向きの観測だが、合否は判定 C が決める。
- `未実施` の条件と理由・担当（未実施が無ければ `なし`、検証経路に該当条件が無いスライスだけ `対象なし`）: `Player ready` の直後の位置の 1 点（差 2。この回の実行担当は再実行しない。下の「差の扱い」で、差 1 の再実行の中で取ることにした）。他は `なし`。
- 後始末: Editor は `EditorApplication.Exit(0)` を依頼した（終了は未確認。既知の native 終了 stall の可能性があり、調査は反復しない）。Editor が書き換えた `unity/Assets/MobileDependencyResolver` の `.pdb.meta` 5 件の削除は `git checkout --` で戻した。
- 差の扱い（2026-11-04、決定記録 D1）:
  - 差 1: 検証経路の基盤不備（hook を掛ける時点の手順）。A 再開でも B 適応でもない。implementation head は変わらず、B へは戻さない。§5 の手順を「Play に入る直前に掛ける」に直し、動作確認を 1 回やり直す。直した手順でも登録が `loaded Title` より後なら Phase A に戻す。
  - 差 2: 観測の仕組み（`obs.cs`）は疎通しており、新しい経路の不確かさは無い。取りこぼしの原因（`Player ready` を Editor.log で待った）を §5 の手順で直し、差 1 の再実行の同じ Play で 2 点を取る。再実行までは `未実施` のまま。

#### 再実行

- 対象 head: `a79530b4302fdb3728220551cee5642de4662414`（作業ツリーの HEAD は `b04f6471836e7c5a6c359041c6981fc7c7b9b9c5`。implementation head はその祖先で、差分は HANDOFF だけ。作業ツリーに `artifacts/`・`docs/reviews/` 以外の差分なし）。
- 実行担当・環境・コマンド: 動作確認の実行担当（Phase B 以外の session）。Editor は閉じていたので `unity open D:\repositories\unity\osm-s4d5\unity` で起動（6000.6.0f1、PID 44860。起動後の import の間は eval が 503 / 5 s timeout で、数分待って応答した）。1 回だけ実行: `newscene` → `ledger` → `prehook`（D1。`playModeStateChanged` に掛け、`ExitingEditMode` で `sceneLoaded`・`sceneUnloaded` を登録して 1 回で外す）→ `dirplay`（directory mode、revision `20261103T091410930Z-d377543307574cafa829b30d94255025`）→ Editor.log を poll して `loaded Title` を待つ → `click` → App のログを poll して `Player ready` を待つ → `obs` 2 回 → `stable` → `stop`（handler を外す）。ledger の拒否は出ず、`Reset()` のやり直しは 0 回。`click` を誤って続けて 2 回送ったが、2 回目は Title が既に無く `no TitleView` を返しただけで submit は 1 件（marker は 1 件）。eval は `artifacts/bs-rig-spawn-a1/eval-b-recheck/`（`prehook.cs`・`stop.cs` が新規、他は 1 回目の版。git 管理外）。Unity テスト・Build・資格情報・実 R2 は使っていない。
- 生の観測（合否は書かない）と保存先・状態: Editor.log は `unity/Logs/Editor.log`、App のログは `unity/OneStarMakerLocal/Log/2026-11-04_003.log`（どちらも git 管理外。写しを `artifacts/bs-rig-spawn-a1/eval-b-recheck/Editor.log.copy`・`app-2026-11-04_003.log.copy` に置いた。`SHA256SUMS.txt` は未作成）。実行時刻 UTC 2026-11-04 14:09〜14:11。
  - `EditorSettings.enterPlayModeOptions` の eval の戻り: `DisableDomainReload`（`enabled=True`）。
  - hook の順序（Editor.log の行番号）: 934 `loaded  (initial)`（名前が空の untitled だけ。Title・UIScene・OutGameScene は `(initial)` に無い）、963 `handler-registered utc=2026-11-04T14:10:02.1069871Z ft=1`、994 `unloaded `、1031 `loaded `、1089 `loaded UIScene`、1444 `loaded OutGameScene`、**1481 `loaded Title`**。つまり `handler-registered` は `loaded Title` より前（1 回目は後だった）。
  - 封鎖の行 1004、初回 Scene の行 1491（`Editor content directory first scene: scene=Title … stable=True`）が各 1 件。
  - submit の marker（1576 行）: `submit utc=2026-11-04T14:10:41.2558396Z ft=57`。
  - submit の後の load / unload: `unloaded Title`・`unloaded OutGameScene`、`loaded InGameUI`・`PlayerScene`・`Spring_Lighting`・`Spring_AtmosphereVFX`・`Spring_Cell_0_4`・`0_3`・`0_5`・`1_4`・`1_3`・`1_5`。`Spring_Cell_0_4` の unload と `Spring_Cell_0_0` の load は 0 件。Play 停止の marker（1722 行）: `play-stop utc=2026-11-04T14:11:05.0620566Z ft=849`。
  - App のログ（`_003.log`）: 103 行 `Flight registered`、104 行 `Player ready at Cell stream spawn (125.0, 28.0, 1125.0)`（`spawn.x/y/z` の 3 成分あり。Timestamp `2026-11-04T14:10:49.6580692+00:00`。submit の約 8.4 秒後）。同ログに `Warning` 以上の行は 0 件、`OnStabledImpl Spring_Cell_0_0` は 0 件（grep）。
  - 位置の 2 点（`obs.cs`。eval の送信時刻は pwsh の UTC。App のログの Timestamp との差）: 1 点目 14:10:49.710（`Player ready` の +0.05 s、rt=47.5）、2 点目 14:10:50.681（+1.02 s、1 点目から +0.97 s、rt=48.8）。どちらも `transform=(125.00, 28.00, 1125.00) rigidbody=(125.00, 28.00, 1125.00) input=True`、scenes は上の load の一覧と同じ（untitled・`Spring_Cell_0_0` なし）。
  - `stable.cs`（14:10:52）の戻り: `IsSceneStable(InGameSession)=True Spring_Cell_0_4=True`。
- 期待との差: **なし**。1 回目の差 1（`handler-registered` が `loaded Title` より後）は、`Title` の load の前に登録が入り `(initial)` に Title が無い形で解消した。差 2（位置の 1 点目が `Player ready` の直後で取れていない）も、App のログの poll で取れた。観測は期待と同じ向きだが、合否は判定 C が決める。
- `未実施` の条件と理由・担当: `なし`。
- 後始末: Play は停止し（`st` で `play=False`）、handler は `stop.cs` で外した。Editor が消した `unity/Assets/MobileDependencyResolver` の `.pdb.meta` 5 件は `git checkout --` で戻した。Editor は `EditorApplication.Exit(0)` を依頼したが、依頼の約 2 分後も PID 44860 が残っていた（既知の native 終了 stall の可能性。調査は反復しない。AGENTS.md の規則）。終了の確認は取れていない。この Editor は本 session が起動したもの。

### 発見 C: AC1 の修正前の失敗（RED）

- 対象: テストだけの commit `40fcdb34dd6840dfe0457d4a2930db63c6f4a897`（修正前）。detached worktree（`D:\repositories\unity\osm-red`。`unity/Library` は作業 checkout から robocopy で写した。確認後に削除）で、Editor が閉じた状態から 1 回だけ実行した（sandbox 外の承認経路）。
- コマンド: `pwsh tools/run-tests.ps1 -Filter 'SampleGame.Tests.Streaming.FlyControllerTeleportTests.Teleport_InterpolatedBody_KeepsTargetPositionEveryFrame'`。
- 観測: 結果 XML の当該 test-case が `result="Failed"`（total 1・passed 0・failed 1・inconclusive 0）。失敗は (ii) の assertion で、`Teleport の 1 フレーム後（frameCount=5）に位置が目標 (125.000, 28.000, 1125.000) から外れた: transform=(0.000, 0.000, 0.000) rigidbody=(0.000, 0.000, 0.000)`（`FlyControllerTeleportTests.cs` 173 行）。setup の失敗・Inconclusive ではない。runner の exit は 1（Unity は終了コード 2）。
- 保存先: `artifacts/bs-rig-spawn-a1/red-check/`（`results.xml`・`step.json`・`unity.log`・`SHA256SUMS.txt`。git 管理外）。
- この確認の範囲: 修正前の AC1 が (ii) で落ちることだけ。構造レビューなど発見 C の残りは行っていない。GO / NO-GO は判定 C が決める。

## 7〜9

Phase C 以降で書く。
