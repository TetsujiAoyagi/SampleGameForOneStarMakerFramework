# README 書き直しの brief（レビュー用）

- type: `brief`（単一スライス。実装は `README.md` の書き直しのみ）
- status: レビュー 2 巡目の修正済み（§7・§8）。「試す」の手順 1（Play As `spring-full`）は Editor で 1 回実走した（§8）。手順 2・3（Content build・Verify Play）は未実走
- owner: root
- created: 2026-11-07
- expires: README の PR マージ時にこの file を削除する（harvest 先は無い。残す内容は README 本文がすべて）

## 1. なぜ書き直すか

owner の目的は「**Unity の開発のつらみを減らしつつ、準オープンワールドのゲームを開発できる土台**」を作ることである。ところがこの一文は README にも `docs/GOALS_AND_STRENGTHS.md` にも書かれておらず、README は「工事中」の一行とライブラリ一覧から始まっていた。読んだ人が「何ができて、どう試すか」を掴めない。

外から見つけてもらう前提として、README を読んだ人が 1 分で次の 3 つを掴める状態にする。

1. 何のために作っているか
2. 今日この repo を開いて何が動くか
3. どの menu を押せば何が見えるか

## 2. 方針

- **目的を先頭に置く。** owner の一文を README の冒頭と「目的」節に書き、`GOALS_AND_STRENGTHS.md` の「侵食防止の契約」はその手段として後ろで参照する。
- **二層で書く（レビュー 1 巡目で owner が決定）。** 「準オープンワールド」は OSM 全体の対象制約ではなく、契約を実証する**適用先**である。冒頭は「契約を公開するフレームワーク」と「準オープンワールドの SampleGame で確かめる」を分けて書き、OSM 全体が準オープンワールド専用と読まれないようにする。
- **「大絶賛工事中」は残す（owner の指示）。**
- **「できること」は今日動くものだけ。** 設計上の意図や予定は書かない。各項目はコードの入口（menu 名・型名・定数）と突き合わせて書いた。
- **「まだできないこと」を同じ重さで書く。** 季節遷移、編集保護、性能の数字、Windows 以外、配信の作り込み、ゲームルール未投入。「工事中」の一言より、無いものを列挙した方が信用される。
- **数字は測った物だけ。** セル数・セル寸法・半径はコード定数から取った。build 時間や反復時間はまだ測っていないので書かない。
- **既存の節は消さずに下へ送る。** ブランチ・環境・DebugStudio・ライブラリ一覧は内容を変えず順序だけ変えた（1 巡目で落としていた `--base develop` の理由の段落は戻した）。
- **英語の要約を 2 行だけ置く。** 検索と海外の読者のため。本文の英訳はしない。
- **冒頭の段落は owner が自分の言葉で書いた（2026-11-07、3 巡目の後）。** 問題の列挙、USD の Variant に着想を得た Asset 表現、親子 Scene、同梱する部品、の順。Agent 側は H1 にプロジェクト名を戻す・表記の統一・`AssetDescription` へのリンク・英語要約を新しい本文に合わせて書き直す、だけを行った。
- **「目的」は多人数制作を先頭に置き、木が揃える 3 軸（寿命・所有・編集単位）を表にした。** 準オープンワールドの streaming は適用先であり、評価してほしい点は木の思想（`05-scene.md` §5.6・§5.13）である、という owner の指摘を反映した。「部品は目新しくない。繋ぎ方の契約が成果物」と「性能は速さでなく観測と局所化」も同じ議論から。
- **旧 README の誤りを 1 箇所直した。** 「Editor Play 向け publish 入口は未整備」は PR #156 の `Verify Play`（build → publish → install → Play）で解消済みなので、その記述に置き換えた。

## 3. 変更した file

- `README.md`: 全面的に並べ替え。新規の節は「目的」「できること」「試す」「まだできないこと」。
- `unity/Assets/Docs/Architecture/20-variant-checkout-workflow.md`: 冒頭の status に追記 1 段落。「Editor Play 向け publish 入口は未整備」が Verify Play 以前の記述であることと、現況は §18 §4 を正とすることを書いた。本文 3 箇所の「未整備」の書き換えは §6 の後続。
- この file。

## 4. レビューで見てほしい点

1. **目的の一文が owner の意図と合っているか。** 冒頭は owner 自身の文。「目的」節の 3 軸の表と、木の思想の要約（親が先に Ready、兄弟は直接参照しない、Cell より長く生きるイベントは Season の子）が `05-scene.md` §5.6・§5.13 と食い違っていないか。
2. **「できること」に過大な主張が無いか。** 特に次の 3 つは実装と照らしてほしい。
   - `Play As` が「同じ表現・同じ companion 集合で Play に入る」
   - `Verify Play` が「途中で失敗したら後続の段へ進まず、Play 用に置いた設定を戻して止まる（build の出力と、開始前に閉じた Scene は戻さない）」（1 巡目で限定した文。bridge より前の失敗は process 環境を触らず、bridge 以降は ledger を戻す）
   - Streaming の「4 季 × 9 × 6 セル、250 m、LoadRadius 375 m / UnloadRadius 550 m」（`WorldCellCatalog` の定数）
3. **「試す」の手順が実際の menu と一致するか。** menu 名は `PlayAsLauncher.MenuRoot`・`VerifyPlayLauncher.MenuRoot`・`SampleGameContentBuild` の `MenuItem` から写した。手順 1 は §8 のとおり Editor で 1 回通した（起動は menu と同じ入口の直接呼び出し、移動は Teleport。menu のクリックとキー入力は未確認）。手順 2・3 は未実走。
4. **「まだできないこと」に漏れが無いか。** 逆に、既にできているのに「できない」と書いた物が無いか。
5. **読者の視点。** Unity で広い世界を作っている人が README だけ読んで、自分のつらみのどれに効くか分かるか。

## 5. 書かなかったこと（意図的）

- build 時間・反復時間の数字（未計測）
- 工程（Phase A/B/C）や Artifact Storage、Harness の説明（開発者向けで、README の読者には不要。`docs/README.md` に残っている）
- 外部フレームワークとの比較（`docs/README.md` の方針どおり repo に置かない）

## 6. この brief の外にある関連作業

README とは別に、同じ目的で直す候補。この brief の範囲ではない。

- GitHub の repository description に全角の `ｗ`（`Frameｗork`）が入っていて検索に一致しない。半角の英語を含む一文に差し替える。
- `docs/GOALS_AND_STRENGTHS.md` の冒頭に owner の目的の一文を足す。
- `docs/handoff/SEASON_WORLD_DESIGN.md` §4 の「イテレーション」行が `Verify Play` 以前の記述のままになっている。
- `unity/Assets/Docs/Architecture/20-variant-checkout-workflow.md` 本文（§2 の図、§3 の手順）の「未整備」3 箇所を現況に書き換える。この PR では冒頭の追記だけで止めた。
- 手順 1 を menu のクリックとキー入力で通す（§8 は直接呼び出しと Teleport）。手順 2・3（Content build・Verify Play）を Editor で通し、所要時間を測って README の「試す」に足す。

## 7. レビュー 1 巡目の対応（2026-11-07）

owner のレビュー（インライン 4 件）と cursor[bot]（インライン 4 件）の指摘は、重なりを除いて 8 件。全部をコードで確かめ、全部 README 側の過大表現か欠落だったので修正 1 commit で反映した。

| 指摘 | 確かめた事実 | 対応 |
|---|---|---|
| `spring-whitebox` は世界全体が Whitebox ではない | `Spring_Cell_0_4.asset` に Whitebox 変種あり、`Spring_Environment_0_4.asset` は空の変種のみ | 「試す」2 を「Whitebox 変種を持つ床の Scene だけ」に修正 |
| UpdateSystem は例外を隔離しない | `SequentialUpdateExecutionBackend` に try/catch 無し。契約は各 system の捕捉で守られている | 「1 つの例外で他を止めない」を外し、順序と「捕捉は各 system」に修正 |
| Verify Play の「環境を変えずに止まる」は広い | 戻すのは Play 用 ledger のみ。build 出力と閉じた Scene は戻らない | 保証対象を限定した文に修正 |
| 二人編集の衝突が解決済みに読める | `SceneResourceMap` と `Season_*` の子リストは構造変更で全員が触る | 「まだできないこと」に「構造編集の衝突」を追加 |
| Play As の前提が無い | `PlayAsLauncher` は Play Mode Script が `BuildScriptFastMode` でないと拒否 | 「試す」1 に `Use Asset Database` の設定を追加 |
| §20 が「未整備」のまま | §20 の 4 箇所。現況は §18 §4 | README のリンクを §18 §4 へ、§20 冒頭に追記 1 段落 |
| `run-tests.ps1` の既定 path | 既定は `D:\UnityEditor`。Unity を batchmode で起動する | `-UnityExe` の例と Editor を閉じる注意を追加 |
| brief の「内容を変えず」が不正確 | `--base develop` の理由の段落を落としていた | 段落を戻した |

軽微: target の識別子を `StandaloneWindows64-Player` に、「同じ revision の上書き拒否」を「成功した revision の directory は上書きしない」に修正。

owner に委ねた「準オープンワールドは適用先か対象制約か」は**適用先**と決まり、冒頭を二層に書き分けた（§2）。

### 2 巡目（2026-11-07）

| 指摘 | 対応 |
|---|---|
| 英文の `proves it` が、複雑なゲームルール投入後の侵食防止まで実証したように読める | `aims to keep ...` / `exercises these contracts` に弱め、`Erosion resistance under complex game rules is a stated goal, not yet demonstrated.` を足した |
| §20 の §2 図と §3 手順が「未整備」を現況として説明したまま | owner の判断で後続に回す（§6）。この PR は冒頭の追記まで |
| brief §4 の Verify Play の引用が旧文 | 1 巡目で限定した文に更新 |
| マージ判断を「文書の整合」と「手順の実証」に分けて記録してほしい | 手順 1 を実走した（§8）。手順 2・3 は未実走のまま README に明記 |

## 8. 「試す」手順 1 の実走記録（2026-11-07）

環境: Unity 6000.6.0f1、Windows、この checkout（head は 2 巡目の修正 commit）。操作は `tools/unity-editor.cmd eval` で、menu が呼ぶのと同じ `PlayAsLauncher.Launch(declaration, interactive: false)` を呼んだ。人間が menu を押したのと違うのは、確認 dialog が出ない点だけ。

**観測（時刻は JST）:**

| 時刻 | 操作 / 観測 |
|---|---|
| 14:36 | `unity open` で Editor 起動。Asset Database refresh 549 秒、その後 Search の初回 index 作成で約 10 分間 eval が `Main thread operation timed out` になりやすかった（menu 操作には影響しない） |
| 14:50 | 環境確認: Play Mode Script = `BuildScriptFastMode`（Use Asset Database）、開いていた Scene は `Title.unity`、compile error 無し |
| 14:51 | 空の untitled Scene に切り替えて Play As `spring-full` → **拒否** `A Variant profile is active and overrides the representation.`（このマシンの `UserSettings/OneStarMaker/DeveloperVariantSettings.asset` に Production profile が選択されていた。VCS 外なので初見の clone では起きない）。README の「理由を出して入る前に止まる」はこの観測で確認 |
| 14:54 | profile 選択を外して再実行 → 受理。Play に入ったが、**空 Scene からは Title が載らず**、UIScene と SceneResourceMap を載せた後 200 秒待っても UI も rig も出なかった。§4.6 の「Editor は開いている Scene を初期登録の入力にする」どおり。README 手順 1 に「`Title.unity` を開いておく」を足した |
| 14:59:39 | `Title.unity` を開いて Play As `spring-full` → 受理（Editor log: `[PlayAs] spring-full content:runtimeMode=addressables world:cellCompanionSet=Full`） |
| 15:00:09 | Play 開始 26.8 秒: 載っている Scene は `Title`、`UIScene`、`OutGameScene` |
| 15:00:34 | `TitleView.Root` の `start-button`（表示「スタート」）に `NavigationSubmitEvent` を送信 |
| 15:00:35〜 | Editor log に `InGameUI`、`PlayerScene`、`Spring_Lighting`、`Spring_AtmosphereVFX`、`Spring_Cell_0_4`、`Spring_Environment_0_4`、`Spring_Cell_0_3`、`0_5`、`1_4`、`1_3`、`1_5` と各 Environment の load が順に出た |
| 15:01:38 | Play 開始 115.6 秒: rig の `transform.position` と `Rigidbody.position` がともに `(125.0, 28.0, 1125.0)`（`WorldCellCatalog.SpawnPosition()` と一致）。`Title` と `OutGameScene` は unload 済み。載っている Cell は (0..1, 3..5) の 6 セルと Environment |
| 15:02:04 | `FlyController.Teleport` で `(875, 28, 1125)`（セル (3,4) の中心、spawn から 750 m 東）へ移動 |
| 15:02:32 | 移動 28 秒後: `Spring_Cell_0_*` と `Spring_Environment_0_*` は全て unload、`Spring_Cell_1_4` と `Spring_Environment_1_4` は残存（500 m ≤ UnloadRadius 550 m と整合）、`Spring_Cell_2_3`〜`4_5` の 9 セルと Environment が load。rig は transform / Rigidbody とも `(875.0, 28.0, 1125.0)` |
| 15:03:08 | profile 選択を Production に戻し、Play を終了 |

**この Play で観測できなかった / しなかったこと:**

- `Player ready at Cell stream spawn` の行は Editor の Console には出ない（App の ZLogger は Unity Console へ流していない）。App のログ file `unity/OneStarMakerLocal/Log/2026-11-07_000.log` には `Telemetry` category の行しか無く、Scene 系の行は入っていなかった。原因は調べていない。spawn の確認は rig の位置で行った。
- 「飛行 rig で移動する」は入力操作ではなく `Teleport` で代替した。入力系（キー操作）は未確認。
- Verify Play と Content build（手順 2・3）は実走していない。
- Play 中の Error: 既知の `[DebugSocket] Failed to connect to DebugStudio server`（DebugStudio 未起動のため）以外に、filter 後の Editor log に Error / Exception 行は無かった。`ContentDeclarationMismatch` は 0 件。
- Variant profile の解除と復元は `DeveloperVariantSettings.SetActiveProfileGuid` で行い、VCS 管理外の file だけを触った。
