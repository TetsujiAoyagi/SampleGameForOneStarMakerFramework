# S4-2（遊べる Player に上書きを重ねる）の計画への推奨

## 0. この文書の位置

- type: 外部計画レビューの入力（[`PLAYER_CONTENT_OVERLAY`](PLAYER_CONTENT_OVERLAY.md) の A3 が採否を決めるための材料）。実装指示ではない。HANDOFF と program は、この文書を書いた担当は編集していない。
- 書いた担当: PR #212 の計画レビューを 2 巡行った session（Claude Code）。同 PR のレビューと 2 つのコメント（型の照合をやめる案、S4-1a の入力）の続きである。
- 入力の版: HANDOFF は `claude/player-content-overlay` の `64bed39`、code は `develop` の `1fd200b`（S4-1a の merge の後）。この branch は その 2 つを merge した上にこの文書を足した物で、program の表の衝突 1 箇所（S4-1a の行は `develop`、S4-2 の行は HANDOFF の branch）だけを解いた。
- 確かめ方: code と文書を読んだ。Unity は動かしていない。`pwsh` が無い環境なので `docs-audit` と `contract-audit` は走らせていない。
- owner: root。expires: 2026-12-31、または S4-2 の A3 が採否を HANDOFF §5.1 に写した時点。harvest 先は無い（採否を写したらこの file を消す）。

## 1. 結論

**スライスの境界は正しい。分けない。S4-1a の後の `develop` に計画を載せ替え、下の §3 の 6 点を閉じれば凍結してよい。**

owner の決定（型と script を照合しない、map と UICommon は S4-1a で届くようにする）で、スライスは最初の案よりかなり軽くなった。残る中身は 4 つである。

1. Runtime: `Decide` を遊べる Player に開ける、上書きの digest の照合、Player の分岐で上書きを渡す。
2. Editor の入口: base と Player の決定、既存の上書きの計画と build の再利用、起動、結果の記録。
3. Player の入力の指紋（`player-compat.json`）で、`ProjectSettings` などの編集を止める。
4. 判定の test と driver。

この 4 つは program の S4-2 の行の問いに 1 つずつ要り、どれかを外すと問いに答えられない。

## 2. 決まっていること（もう一度検討しなくてよい）

owner（root）が 2026-11-24 に決めた。HANDOFF `64bed39` に反映済みである。

| 決定 | 中身 |
|---|---|
| 止める物と止めない物の線 | 「入れた物が信用できない」（digest、base の記録、key の欠け、Player の入力の違い）は止める。「入れた物の一部が Player に無い・古い」（script の差、型の欠け）は止めず、知らせもしない |
| 型の一覧 | 見たい時の手段は Editor の専用の debug menu。後続へ送る（仮の置き場は S5-1） |
| `ProjectSettings`・`Resources`・`StreamingAssets` の編集 | 止める（設定を変えて上書きを作り、古い設定の exe で遊ぶことになるので） |
| map と UICommon の編集 | 止めずに届ける。S4-1a が土台を作った（完了、PR #213） |
| `link.xml` | 足さない。Player の構成は S4-0・S4-1・G0 と同じ |
| 照合の場所 | Runtime を重くしない。型の照合は Runtime に置かない |

## 3. 凍結の前に閉じる 6 点

重い順に並べた。1 と 2 は設計に効き、3〜6 は確認と書き直しである。

### 3.1 base の決め方を「ledger」から「Player の identity」に変える（推奨。設計の変更）

今の D7 は、Delivery の Use For Next Play の ledger を base の正本にする。これには使い勝手の問題が 2 つある。

- **Unity を開くたびに選び直す。** ledger は `SessionState` で、Unity を閉じると消える（`ContentDeliveryPlayBridge.cs:147-206`）。利用者は毎回 Delivery の窓で選んでからでないと menu を使えない。UC2 の「menu 1 つ」に、書いていない前段が付く。
- **Editor の Play の入口と取り合う。** 手で戻す ledger があると、Editor の Play の入口はそれを「利用者の明示の設定」として扱う（`PlayEntryDecision.cs:349-350` の `HasExplicitSetting`）。Player で確かめるために選ぶと、Editor の自動の経路（Built + local edits）の挙動が変わる。Editor と Player を行き来する開発者が、その都度戻すことになる。

一方 D1 は「base は Player と同じ build identity の installed revision だけ」と決めている。**Player が決まれば base は 1 つに決まる。** 利用者が選ぶ必要があるのは base ではなく Player である。

推奨する形:

1. 入口は `artifacts/playable/players/` の Player を見る。1 つならそれを使い、複数なら build の新しい物を使う（Console に identity を出す）。
2. その Player の `build-receipt.json` の identity と `transportManifestSha256` に合う install を cache から探し、`ContentCacheStore.ValidateInstalled` で検証する。cache の列挙と検証は Editor の入口が既に使っている（`PlayEntryCandidates.cs:44-75`）。
3. install が無ければ「この Player の base を Delivery で install する」案内で止める。

これで D7 の読み口（`TryReadManualLedgerPair`）、`NoBaseSelected` と `BaseMismatch` の区別、`ContentDeliveryPlayBridge` の変更、判定の各 case が同じ process の中で `Apply` する手順、AC8 (d) が要らなくなる。外部 1 巡目の X1 が ledger を勧めた理由は「最新の install へ黙って縮退しない」ことで、Player の identity から決める形はそれを満たす（縮退ではなく、Player が指す 1 つだけを使う）。

採らない場合は、少なくとも「Unity を開くたびに選び直す」と「Editor の入口の挙動が変わる」を UC1 の利用者の入力として書き、owner の確認に足してほしい。

### 3.2 AC12 は「上書きに入った」ではなく「Player で効いた」を見る

S4-1a は S4-2 へ、次の未測定を送っている（program「S4-1a から移送した入力」の (1)）。

> 届くこと（UICommon と map の編集が上書きの root に入る、上書きの map が上書きの側の `SceneResource` を指す）は確かめていない。

今の AC12 が見るのは `outcome`・`startup`・「上書きの root に UICommon か map の root がある」だけで、**Player が読んだ map と UICommon が上書きの版だったか** を見ていない。base の版を読んでいても合格する。S4-1a を置いた理由（map の編集を届ける）の確認がここにしか無いので、効きを観測に入れてほしい。

- **UICommon:** AC1 と同じ形で、UICommon の上の見える物を token に変え、screenshot で読む。追加の手段は要らない。
- **map:** 起動の log は `Bootstrap SceneResourceMap: source=content` までで、どの層の map かは出ない。観測の手段を Phase A で決める必要がある。候補は 2 つ。
  - (a) その log の行に、読んだ map の `GenerateHash` を足す（Runtime の 1 行）。patch で hash を変えた上書きの map が読まれたことが、log だけで分かる。
  - (b) `SceneResource` の値のうち、遊びの log に出る物を変える（子の Scene の構成、付随 asset の名前）。code を足さずに済むが、patch が大きくなり、Title から届く範囲に限られる。
- **map から `SceneResource` への参照:** (a) だけでは「上書きの map が上書きの側の `SceneResource` を指す」は見えない。(b) を 1 つ足すか、見ない範囲として §2.3 に書くかを決める。

B に選ばせず、どれを見るかを AC12 に固定してほしい。map と UICommon は別の AC に分けた方が、片方だけ落ちた時に読みやすい。

### 3.3 計画を S4-1a の後の `develop` に載せ替える

HANDOFF の §1 の現況、§3 の行数、A2 の固定 base は `116b0091` のままである。S4-1a は S4-2 が触る同じ場所を変えた。

| 変わった所 | S4-2 への効き |
|---|---|
| 遊べる Player は `AfterSceneLoad` の最初に content を登録する（`BootstrapSourcePolicy.Plan`） | D3 の「登録の直前の digest の照合」と、上書きの登録の失敗は、UICommon より前に起きる。失敗の AC（AC3・AC4）の「Title の `Loaded.` が無い」は成り立つが、現況と I1 の経路の記述を今の順に合わせる |
| 起動の log が 3 行増えた（`Content directory registered: identity=… from=installed\|directory`、`Bootstrap UICommon: source=…`、`Bootstrap SceneResourceMap: source=…`） | AC1・AC8 (a) の観測に使える。上書きつきの起動では `from=installed` の行と上書きの登録の行の両方が出る。どちらを合否に使うかを書く |
| Player の構成の latch は開始 Scene の名前（`UICommon` か `PlayablePlayerStart`） | I2 の経路（`smoke` の判定）の記述を合わせる |
| 遊べる Player の build は bootstrap を写さない（`BuildPlayableFromStartScene`） | `player-compat.json` を書く場所は、この method の `Build` の後・`Publish` の前になる。§3 の coordinator の行数（229 → 今は 337）と、AC2 の「mutation の root が残らない」の読み（遊べる Player の root は所有の印だけ）を合わせる |
| S4-1 の `PlayerKindEndToEndTests` に case が増え、判定の run は `j1` | AC14・I7 の「S4-1 と同じ」を「S4-1a の後と同じ」に、全 EditMode の Inconclusive の基準の run を `jc5` から `j1` に揃える |

あわせて、**A2 のアーキテクチャの観点は Runtime の部分だけ取り直す** ことを勧める。A2 が読んだ `AbstractApplicationInitializer` の起動の順は今と違い、S4-2 が配線を足す位置（Player の分岐、登録の前）の前後が変わったからである。入口と compat の側は S4-1a の影響を受けないので、取り直しは要らない。

### 3.4 S4-1a から送られた入力を 1 つずつ置く

program「S4-1a から移送した入力」の S4-2 の項目のうち、HANDOFF に置き場の無い物がある。

| 項目 | 推奨 |
|---|---|
| (1) map と UICommon が届くこと | 上の 3.2 |
| (2) installed の pair からの成功の起動は誰も確かめていない | AC8 (a) が初めての確認になる。上書きつきの AC1 も同じ経路の上にある。未知（u9）として §5 に足し、driver の最初の case に置く（今の順で既に最初である）。落ちたら上書きの前の問題なので、切り分けの手順を書く |
| (3) 開始 Scene の asset は Player に焼き込む入力になる | D11 の指紋の集合に `PlayablePlayerStart.unity` を入れる。build は root が 0 個でないと落とすが、Scene の設定（lighting など）の編集は通る |
| (4) `PlayerContentStages` は `smoke` の receipt 専用の列 | D10 の `startup` の観測に使わない、と 1 行書く |
| (5) UICommon と map を読んだ後の失敗の後始末は既知の境界 | S4-2 の失敗の AC は全て登録より前に落ちるので当たらない。当たらないことを I1 の「探索した境界」に書く |
| (6)〜(8) 判定の test の使い回しの注記 | §5 の判定担当への注記に写す（`MapSceneResourceCount` の読み、`未登録のシーンをスキップ` の行は Error ではない、Variant profile の選択を空にする） |

### 3.5 type tree の前提を測る（u7、G0 (d)）

HANDOFF が既に凍結の前に測ると決めている。owner の確認 (C)（spike の branch の push と record の保存）の承認が前提である。測る物は、field を足す・消す・型を変える、の 3 通りで、Player の log の Error と exit code と probe の値を見る。落ちたら owner が D4 を見直す、も書いてある。追加の推奨は無い。

### 3.6 D10 の `startup` の観測は、batch の test から進められる形にする

D6 の (6) は、起動の後の観測を `EditorApplication.update` で続けると書く。判定の test は `run-tests.ps1` の batch の EditMode で走り、S4-1 の判定の test は同期の `[Test]` の中で log を待つ形である（`PlayerKindEndToEndTests` の `WaitForLine`）。同期の test が main thread を止めている間、`EditorApplication.update` は回らない。**そのまま作ると、AC が読む `startup` は `Pending` のまま変わらない。**

観測の 1 回分を、update からも test からも呼べる関数（port）にしておくか、判定の case を `[UnityTest]` にするかを §3 と §5 に書いてほしい。あわせて、観測の途中で script の再 compile（domain reload）が起きた時に `Pending` が残る扱い（読み直して続けるか、`TimedOut` にするか）も決める。

## 4. Phase C・C' で突かれそうな所

3 章の 6 点を閉じた後に残る物である。どれも凍結を止めるほどではないが、先に書いておくと差し戻しが減る。

- **AC5 の「Error 以上の行が 0」は u7 の結果に依る。** G0 (d) の結果を AC5 の期待値に写してから凍結する（field を足した時に warning が出るなら、その行を除く pattern を書く）。
- **AC13（Player を 2 つ同時に動かす）は u8 のまま。** 同じ folder の 2 つの process が game の log の file を取り合うかは未確認である。不成立の時は「`startup` が `Failed` で理由が出るので不変条件は破らない」と書いてあるが、AC13 自体は不合格になる。不成立の時に AC13 をどう読むか（合格にする条件を「A の directory が変わらない」だけに絞るか）を先に決める。
- **D11 の指紋と、新しい worktree の URP の書き換え。** 判定の driver は戻すと書いてあるが、利用者の新しい checkout でも同じことが起きる。最初の 1 回、触っていない file で `PlayerInputDiffers` になる。止まった時の案内に「Unity が書き換えた file なら戻す」を入れるか、既知の制約として README の「試す」に書く。
- **D11 の依存の閉包に package の file が入る。** `AssetDatabase.GetDependencies` は URP の package の shader などを返す。package の file は `Library/PackageCache` にあり、checkout ごとに path が違う。指紋に入れるのは `Assets/` の下だけにし、package の版は `Packages/packages-lock.json` を集合に足して見る、と決めると安定する。
- **AC1 の人間の段。** 押下だけを高コスト証拠にする形は妥当である。S4-1a は AC6 の click を AI が画面を取り込んで OS の入力で行っている（補助の script は repo に無い）。同じ手段を使うなら、owner の確認 (B) は要らなくなる。どちらにするかを書く。
- **古い上書きが溜まる。** 上書きは毎回新しい directory に出て、消す処理が無い。§2.3 に送ってあるが、判定の run が何回も build するので、driver の終わりに判定の上書きを消すかどうかを決める（消すなら I6 の観測の後）。

## 5. 実装の順への推奨（Phase B）

§4 の順は「digest → Decide → 配線 → compat → 入口 → 判定の test」である。未測定の経路が 2 つ（pair からの成功の起動、上書きつきの起動）とも Runtime の配線の先にあるので、次を勧める。

- Runtime の配線（1〜3）が済んだ時点で、環境変数で直に起動する 2 つの case（pair だけ、pair と上書きの 3 key）を先に通す。入口が無くても起動できる。ここで落ちるなら、入口と compat を作る前に分かる。
- HANDOFF は「動作確認（Phase B の後）: 対象なし」としている。Unity の test と build は Phase C の責任なので、この 2 つは発見 C の最初の限定実行として §5 に書くのが、今の規則に合う。

## 6. 後続へ送る物（S4-2 に入れない）

HANDOFF の §2.3 に既にある物と重なるが、この議論で増えた分をまとめる。

- 上書きが使う型のうち Player に無い物の一覧（Editor の debug menu。仮に S5-1）。
- 上書きで試せる型の範囲を広げること（`link.xml`、Player の `strippingInfo`）。
- 古い上書きの清掃。
- CI で build した Player との指紋の改行の差（S4-3）。
- Player の構成の違う commit の script を pull した時の案内（owner は「案内もしない」と決めた。要求が出たら置く）。

## 7. この推奨が依る事実と、確かめていない物

- 確かめた（code）: `ContentDeliveryPlayBridge` の ledger が `SessionState` であること、`HasExplicitSetting` が手で戻す ledger を含むこと、`PlayEntryCandidates.Select` が cache を列挙して検証すること、S4-1a の後の起動の順と 3 行の log、遊べる Player の build が bootstrap を写さないこと、上書きの build が毎回新しい directory に出ること。
- 確かめていない: Unity の実行に依る物は全て（type tree の挙動、2 つの process の log の取り合い、batch の test の間の `EditorApplication.update`、上書きの map の参照）。3.6 の `EditorApplication.update` は Unity の一般の挙動からの見込みで、この project で測ってはいない。
