# 手元の編集の上書きを Play に載せる入口（S3-4b）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S3-4b の行）
- status: `A`（A3 の統合案。A2 の 4 件、外部計画レビューの 3 巡、owner の裁定、目的レビューのやり直しを反映した。未凍結）
- branch: `claude/overlay-play-entry`（PR の base は `develop`）
- implementation base commit: `d176033a`（`origin/develop` の head、2026-11-22。§1 の現況と §3 の行数はこの base）
- implementation head commit: 未固定
- A3 凍結記録（承認者・日付・対象 revision・承認の根拠。凍結前は `未凍結`）: 未凍結
- risk: `high`（起動の配線と fail-closed の契約、所有者と寿命（base と上書きの 2 層の登録を製品の起動から呼ぶ）、Editor の Play の入口の順序を変える）
- owner: root（依頼者）
- created: 2026-11-22
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: [18-asset-description.md](../../unity/Assets/Docs/Architecture/18-asset-description.md)「宣言からの Play 経路」（経路 Built + local edits、順序、作り直せない root の通知、record）と「上書きの build と逆引き」（再利用の記録の書き込みの契約。「既知の不具合」の項を消す）、[13-resource-system.md](../../unity/Assets/Docs/Architecture/13-resource-system.md)「base と上書きの登録契約」（入口は配線済み、Domain Reload 有効の Play の実測）、[04-app-startup.md](../../unity/Assets/Docs/Architecture/04-app-startup.md) §4.6・§4.7（上書きの起動の設定と失敗）、[CONTENT_DIRECTORY_BUILD_FACTS.md](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)（§4 の (1) の観測）、program §5.3（見直しの条件 (1)〜(3) の結論と、暫定の public の値の扱い）・§8（S5 への入力: 検査の費用の記録から分かった、省ける処理と今は省けない処理）
- 関連: [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md)（A0。§1、§4.3、§5.3「上書きの方式を選ぶ理由と見直しの条件」「編集の種類ごとの扱い」、§8 の S3-4b の行と S3-2・S3-3・S3-4a からの移送）、[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)（所有境界の正本）
- Phase A snapshot の取得案内・状態: 未生成（機械向けの期待値・照合 script は無い。§5）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: Phase A の A3（外部計画レビューと owner の凍結判断）。凍結していないので、Phase B の担当はまだ居ない。
  - 次の作業: owner が凍結を判断する。凍結の時に、A3 凍結記録・対象 revision・次担当 B をこの欄に書く。裁定（U1〜U6）、外部計画レビューの 2 巡、目的レビューのやり直しは済んでいる（§2.8、§5.1）。
  - 引き継ぐ commit: この票を含む commit（A3 の統合案）。SHA は PR 状態ブロックで渡す。
  - 必要な権限: なし（文書のみ）。
  - 停止規則: §2.6。凍結の前に Phase B を始めない。
  - 引き渡さないもの: なし。

## 1. 目的と対象外

- 目的: 取得した目的 build（base）で Play する開発者が、Play ボタンを押すだけで、自分の編集（開いている Scene の外にある asset の編集を含む）が反映された Play に入れるようにする。何を編集したか、上書きを作るか、前回の上書きを使い回すかを、開発者は入力しない。編集が無い時と、編集が前回と同じ時は、build をせずに Play に入る。編集の影響を受けるのに手元の source では作り直せない Scene がある時は、その Scene と理由を Play のたびに示した上で Play に入る。上書きを作れない時と base に重ねられない時は、base だけの Play に黙って入らず、理由を示して止める。保存していない asset の編集がある時は、全て保存して続けるかを確かめ、黙って保存しない。利用者が明示した設定と、今ある経路の動きは変えない。
- 利用者: (a) 編集する開発者。担当する Scene と asset を Editor で編集し、Play で確かめる。担当外の source は手元に無くてよい。上書きを使う経路を永続の選択として 1 回選ぶ。その他の新しい入力は、保存していない asset がある Play での保存の確認だけである。(b) 遊ぶだけの人。source を持たず、今の経路 Built のまま Play する。何も変わらない。(c) OSM を使う別の host の Editor の入口を書く者。自分で作った上書きの directory を、検証済みの base と一緒に起動の設定で渡す。
- 目的・利用者の文言の経緯: 上の 2 欄は program §8 の S3-4b の行の問いと、§4.3、§5.3、S3-2・S3-3・S3-4a から移送した入力から、本書で初めて書いた。A2 の目的レビューは A1 の原文で行い（§5.1）、owner は 2026-11-22 に U1 でその 2 欄を承認した。その後、owner の U4 の裁定（未保存の asset は確認の上でだけ保存する。§2.8）に合わせて 2 か所を改めた。目的は「保存していない asset の編集がある時は、全て保存して続けるかを確かめ、黙って保存しない。」の 1 文を足した。利用者 (a) は、元の「上書きを使う経路を永続の選択として 1 回選ぶ他に、新しい入力は無い。」を今の 2 文に改めた。理由は、確認が利用者の新しい入力だからである（設計の都合で入力を増やしたのではなく、入口が未保存の編集を黙って確定しないための owner の裁定による）。owner は 2026-11-22 に改めた文言を承認し、同日に目的レビューをやり直した（§5.1）。
- 対象外:
  - 開いている Scene を載せずに、上書きだけを重ねて宣言の初回 Scene から始める Play。新しい経路は、今の経路 Built + open scenes が Play に入れる状態（保存済みの Scene が開いている等）でだけ動く（§2.3）。
  - 入口の Framework への一般化、経路の統合、menu の整理。入口は今と同じく SampleGame の `DependOnAll/Editor/Build/` に置く（§2.3）。
  - `Runtime/ContentResolution/`、`ContentDirectorySession`、`ContentDirectoryLayers`、`ContentRevisionGate`、`AssetManagement`、`SceneSystem` の変更。登録・解決・解除は S3-2 のまま使う（AC6）。
  - 計画の中身（編集の検出、分類、鍵に入る入力）の変更。script・`Packages/`・import 時の依存・project の設定を検出と鍵に入れること（§2.3）。
  - 記録の無い base で、既にある asset だけを指す root の宣言を手元で足した場合と、記録に無い root の依存だけが手元に無い場合の検出（§2.3）。
  - base の選び方、base より古い checkout と偽の編集（改行、LFS の pointer）の見分け、要求の時点の install（S4）。
  - 検出の所要を減らす工夫、古い上書きの directory の掃除、再利用の記録を contentSet ごとに複数持つこと（S5）。
  - Player。上書きは Editor の Play だけで使い、Player の構成は上書きの設定を拒むだけである。Player の build は判定に含めない（§5）。
  - Play の間に script を compile し直した時（Domain Reload）の session の扱い。1 directory の Play と同じ性質で、上書きで新しく生じる問題ではない（§2.3）。
- 現況（base `d176033a`。code の読みと `grep` で確かめた。path は `unity/Assets/` からの相対）:
  - **上書きを Play に載せる製品の経路は無い。** `ContentOverlayPlanner.Plan` を呼ぶのは test だけで、`ContentDirectorySession.RegisterVerifiedWithOverlay`（`OneStarMaker/Scripts/Runtime/BuildContent/ContentDirectorySession.cs` 132 行目、internal）を呼ぶのも test だけである。`SampleGame/DependOnAll` に `overlay` の語は無い。
  - **起動は検証済みの directory を 1 つだけ登録する。** `AbstractApplicationInitializer.InstallConfiguredContentDirectoryAsync`（`OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs` 1305 行、740〜764 行目）は、installed の pair があれば `RegisterVerified`、無ければ `Register` を呼び、session を `AssetManagement` に install する。`ReadContentDirectoryConfiguration`（1080〜1141 行目）が読む key は `content:runtimeMode`・`installedRevisionPath`・`manifestSha256`・`directoryPath`・`buildIdentity`・`representation`・`firstScene` で、上書きの欄は無い。
  - **2 層の登録と解決は、登録の口に渡せば動く。** `RegisterVerifiedWithOverlay(VerifiedInstalledRevision, string overlayPath, string overlayIdentity)` は base の検証、base → 上書きの登録、上書きの root の記録と base の照合、失敗時の巻き戻しを持つ（13 章「base と上書きの登録契約」）。`RegisteredDirectoryRoute` は session に上書きの層があれば上書きの供給元を自分で作り、上書き → base の順に並べる。起動の配線に足すものは、登録の口の呼び分けと log だけである。
  - **Play ボタンの入口は 3 経路で、どれも上書きを知らない。** `PlayEntryInterceptor`・`PlayEntryDecision`（316 行）・`PlayEntryTransaction`（170 行）・`PlayEntryCandidates`（102 行）・`PlayEntryRecord`（307 行）は `SampleGame/DependOnAll/Editor/Build/` にある。経路は `Source` / `Built` / `BuiltWithOpenScenes`。tick の 1 回の同期呼び出しの中で、再判定 → 複製の準備 → 1 回の ledger → 計画の公開 → `EnterPlaymode` の順に進み、例外は複製を解除して `Blocked` にする（`PlayEntryTransaction.cs` 100〜145 行目）。値は process の環境変数（`SAMPLEGAME_` + key）で渡し、1 回の ledger が戻す。
  - **候補は install の値を持ち歩かない。** `PlayEntryCandidate` は revision と置く環境変数だけを持つ。計画が要る `ContentInstallResult` は `ContentCacheStore.ValidateInstalled`（public）が返すが、`PlayEntryCandidates` の中で捨てている。
  - **SampleGame の合成は欠けの記録を落とす。** `SampleGameBootstrapContentComposer.Compose`（104 行）は `BuildMaterializationSnapshot` を 5 引数で作り直し、`Absences` を引き継がない。計画は、欠けの記録を引き継がない合成を例外にする（18 章「呼び手の順と前提」）。`SampleGameContentBuild`（148 行）は欠けを許さない `Materialize` だけを呼ぶ。
  - **再利用の記録の書き込みが、成功した build を捨てる。** `BuildContentCoordinator.Build`（`OneStarMaker/Scripts/Editor/Build/Content/BuildContentCoordinator.cs` 258 行）は、final への移設（122 行目）と成功の outcome の書き込み（134 行目）の後、同じ `try` の中で `ContentOverlayReuse.Write` を呼ぶ（136 行目）。`Write` の例外は 148〜158 行目の `catch` に入り、final を消して outcome を失敗で上書きする。`Write`（`ContentOverlayReuse.cs` 148 行、72〜92 行目）は、別名に書いた後、既存の記録を消してから移す（90〜91 行目）ので、その間の失敗で古い記録も失われる。記録の書き込みを失敗させる test は無い。
  - **build は所要を記録しない。** `Editor/Build/` に所要の計測は無い。root の数は計画の一覧の件数から取れる。
  - **入口の tick は compile 中・import 中を見ない。** `PlayAsLauncher.CheckEditorState` を使うのは Play As・検証 Play と、詳細窓の回復のボタンの無効化だけである。
  - **複製の準備は Dirty の asset を file に書く。** `EditorSceneManager.SaveScene(…, saveAsCopy: true)` の副作用で、保存していない asset の編集が file に書かれ Dirty が倒れる（S3-3 の動作確認。18 章「宣言からの Play 経路」）。Scene の保存の hook（`SceneVolumeSaveHook`）は、保存した Scene の体積を計算し直し、値が変われば `SceneResource` を書いて `AssetDatabase.SaveAssets()` を呼ぶ。複製の保存でこの hook が走るかは未確認である（下の (v)）。
  - **Dirty の asset を書くのは Unity の `SaveScene` 自身で、Scene が参照しない asset も書く（2026-11-22 の実測、Unity 6000.6.9f1、この PC、各 1 回、`eval`）。** `Title` を開き、`Title` が参照する asset 1 つと参照しない material 1 つを Dirty にして、`SaveScene(scene, 複製の path, saveAsCopy: true)` を呼んだ。(a) そのまま: 2 つとも Dirty が倒れ、値を変えた material の file が書き換わった。(b) 保存の hook を止めて（`SceneVolumeRecalculator.SaveHookSuspended`）: (a) と同じ。hook ではなく `SaveScene` が書いている。(c) `Assets/` の下の Dirty の object を `Resources.FindObjectsOfTypeAll` と `EditorUtility.IsPersistent`・`IsDirty` で挙げ、`EditorUtility.ClearDirty` で印を外してから複製し、`SetDirty` で戻した: file は 2 つとも変わらず、Dirty は 2 つとも残り、memory の値も残った。同じ挙げ方は 2 つの asset を過不足なく返した（`Assets/` の外では、built-in と package の object が数百件 Dirty と出るので、path で絞る）。測っていないもの: Undo の履歴、sub-asset、Dirty の Scene、prefab の編集中、import の設定（`.meta`）の未適用の変更。
  - **入口が読む設定の定数と起動の log の marker は、Framework の Editor が公開し直している。** `editor:playSnapshotExpected` は Runtime の internal の定数（`EditorPlaySnapshotBridge.cs` 75 行目）を `OneStarMaker.Editor` の `PlaySnapshotSession` が public の定数で公開し直し、SampleGame はそれを参照する。record が読む起動の log の marker は、SampleGame 側に複写がある（`PlayEntryRecord.cs` 29 行目）。
  - **保存した経路の値は、知らない値を `Source` と読む。** `PlayEntrySettings` は `Enum.TryParse` の失敗を `Source` にする（32 行目）。
  - **Domain Reload の実測は一部だけある。** native の登録は Play を終えても Unity が自分では解除せず、script の reload の後は一覧が 0 件になった（13 章「測った設定」）。Play に入る時の reload が前の Play の残りを消すか、reload の後の OS の lease の file handle は測っていない（13 章が S3-4b へ送っている）。
  - **S3-3 の観測（入力）。** 複製から載った Scene が参照する asset は手元の Editor の版だった（O-1・O-2）。開いている Scene 自体と、その Scene から見える編集は、複製が届ける。
  - **未確認:** (i) 対話の Editor で、Dirty を含む Scene を開いたまま、入口の tick から Content Directory の build を起こした時、開いている Scene の内容・Dirty・path が変わらないか、Unity が保存を求めないか（18 章は「開いている未保存の Scene の build は batch で確かめ、対話の Editor では確かめていない」と書く）。(ii) tick の中で `AssetDatabase.Refresh()` を呼んだ後、同じ tick で計画・build・`EnterPlaymode` まで続けられるか。(iii) Domain Reload 有効の Play に入る時、前の Play の native の登録が 0 件で、lease を取り直せるか（上書きあり）。(iv) 複製を `Assets/OsmPlaySnapshot/` に置いた後の上書きの build が、複製を閉包や出力に含めず、複製の file を変えないか。(v) 複製の保存（`saveAsCopy`）が Scene の保存の hook を走らせ、`SceneResource` などの source を書くか。(vii) 未保存の asset の列挙（上の実測の (c) と同じ、`Assets/` の下の load 済みで Dirty の object）の所要が、Play のたびに使える小ささか。
- UC ごとの利用者の入力:

  **UC-1 編集した内容を Play で確かめる**（利用者 (a)）

  | 入力 | 誰がいつ決めるか | 目的からの正当化 |
  |---|---|---|
  | RunPurpose | 開発者が永続の選択として選ぶ（今と同じ） | base の候補は宣言で決まる |
  | 経路 `Built + local edits` | 開発者が永続の選択として 1 回選ぶ（今の経路の選択に値を 1 つ足す） | 目的の「今ある経路の動きは変えない」。上書きは宣言の source の materialize と Play のたびの計画を要し、source を持たない利用者 (b) と、開いている Scene だけを確かめる開発者には要らない（§2.4 P1） |
  | 何を編集したか | 入力にしない。計画が base の記録と手元の file から導く | 目的の「開発者は入力しない」 |
  | 上書きを作るか、使い回すか | 入力にしない。計画の状態と再利用の鍵が決める | 同上 |
  | 作り直せない Scene を認めるか | 入力にしない。入口が計画の集合をそのまま認め、Play のたびに示す（host の規則。§2.4 P3） | 目的の「示した上で Play に入る」 |
  | 保存していない asset を保存して続けるか | 検出できた未保存の asset が 1 件以上ある Play でだけ、開発者が確認に答える（実行時の操作。検出が 0 件の Play では出ない） | 目的の「全て保存して続けるかを確かめ、黙って保存しない」。上書きの build と Scene の複製は disk の保存済みの内容を使い、Unity の複製の保存は未保存の asset を全て書く（§1 の実測）。確かめずに進むと、入口が利用者の未保存の編集を黙って確定する |
  | Play ボタン | 実行時の操作 | 目的 |

  **UC-2 別の host の入口が上書きを起動に渡す**（利用者 (c)）: 検証済みの base の pair（今ある key）と一緒に、上書きの directory の path と build の identity の 2 つの key を、その Play の config に置く。key の名前は Framework の Editor の public の定数から取る。渡した directory は、計画や作り直しの対象にならず、base との照合だけを受けてそのまま登録される。上書きを作る手順は 18 章の計画の口のまま変わらない。

  利用者 (b) は操作を持たない。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: **編集する開発者が Play ボタンを押すと、取得した目的 build に自分の編集の上書きが重なった Play に入れ、明示設定と今ある経路を壊さず、上書きを作れない・重ねられない時に base だけの Play に縮退せずに理由を示せるか。部分 checkout でも、編集のたびに Play が止まらずに言えるか。**
- 誤って YES を出す道（受け入れ条件はこれを塞ぐ）:
  - 全ての source がある checkout だけで確かめる。作り直せない root が現れない。
  - 上書きの build の失敗を入口が握り、Built + open scenes と同じ値で Play に入る。Play は動き、base の版が出る。
  - 起動が上書きの key を読めない時に base だけを登録する。record の「上書きあり」が入口の計画だけから作られ、起動の観測と突き合わされない。
  - Reload Domain 無効（この project の既定）だけで確かめる。
  - 「再 Play は build 0 回」を、編集の無い Play だけで確かめる。再利用の経路が現れない。逆に、常に再利用して編集を足した後も古い上書きを出す。
  - 判定用の base を今の checkout から作らずに、古い base で確かめる。偽の編集で上書きが出て、「編集なし」の経路が現れない。
  - port を fake にした test で順序だけを確かめ、実 Editor で、保存していない asset の編集・開いている Dirty の Scene・build が同じ tick に並ぶ所を確かめない。
  - 開いている Scene を閉じて確かめる。複製と上書きの同居が現れない。
  - 作り直せない root の表示を、再利用した build の preflight（前回の計画）から取る。
  - 再利用の記録の修正を、書き込みが成功する経路だけで確かめる。
  - 保存した編集だけで確かめる。保存していない asset があるのに、確認を出さずに複製の保存がそれを file に書く。
  - 保存の確認を、fake の port の単体 test だけで確かめる。実際の列挙、複製の保存の副作用、「やめる」の後に何も書かれないことが、新しい経路で一度も通らない。
  - 保存していない asset が検出に 0 件だったことを、「保存していない asset は無い」と表示する。
  - 利用者の層に残った上書きの key が、今ある経路の Play に黙って重なる。
  - Player の構成での拒否を、Editor の分岐だけで確かめる。
  - Warning が出て Play に入れたことを、「編集を確かめられた」と数える。作り直せない root が base の版のままであることと、base にも無い root が要求の時に失敗することを見ない。
  - 「build 0 回」を、Play の前の検査の費用が無いことと読む。

### 2.1 進める最低条件

用語: 「上書きの Play」は、入口が上書きを登録すると決めて上書きの key を置いた Play、または起動の config に上書きの key がある Play を指す。「作り直せない root」は 18 章の計画の分類（`Unbuildable`）である。目的の「上書きを作れない時」は、計画の例外と build の失敗を指し、一部の root が作り直せないことは含まない（その時は示して Play に入る）。「編集」は、disk の保存済みの file と、base の記録の比較で決まる。開いている Scene の保存していない内容は複製が届ける。保存していない asset の編集は、保存されるまで計画と上書きの build の対象ではない。入口は、検出できた未保存の asset がある時に、全て保存して続けるかを確かめる（「保存して続ける」なら保存されて編集になり、「やめる」なら Play に入らない）。目的の「編集が前回と同じ時」の「前回」は、同じ base に対して最後に成功した上書きの build を指す（再利用の鍵が同じ時。前回の Play ではない。base を取り直した後は使い回さない。18 章「再利用」）。目的の「base に重ねられない時」は、起動の登録の失敗（上書きの directory が無い、上書きの記録と base の照合の不一致など。13 章「重ねてよい組み合わせ」）を指し、編集が無くて上書きを作らない Play は含まない。目的の「保存していない asset」は Scene 以外の asset を指し、開いている Scene の保存していない内容は確認の対象ではない。目的と利用者の「保存していない asset がある」は、入口が検出できた未保存の asset が 1 件以上あることを指す。検出が 0 件であることは、未保存の asset が無いことの証明ではなく、検出に掛からない Dirty は確認なしで複製の保存に書かれうる（§2.4 P5-c）。「示す」は、Console の Warning、record、詳細窓の 3 つである。「明示設定」は、JSON・環境変数・コマンドラインの層に利用者が置いた値で、経路の選択より優先する（入口は明示設定の行で素通りして値を書かない。その行より前にある安全のための拒否は、明示設定にも今と同じに掛かる）。

**不変条件（起きてはいけないこと）**

- **I-1 上書きの Play は base だけで始まらない。** 上書きの Play は、base → 上書きの 2 層が登録された Play か、理由を示した失敗（入口の `Blocked` か、起動の失敗）のどちらかになる。入口が「上書きが要る」と計画した後に、上書きの key を置かずに Play に入ることも無い。
  - 主張を崩しうる経路: (1) 入口の tick の `catch`（`PlayEntryTransaction.cs` 134〜139 行目）と、計画・build の結果を受ける新しい段。失敗を握って次へ進む実装。(2) build の結果の `IsSuccess` を見ずに `ContentPath` を使う。(3) 起動の `InstallConfiguredContentDirectoryAsync` の呼び分け。key を読む前に `RegisterVerified` で登録する、検証済みでない base（`directoryPath` の明示。`Register` は managed に見える path を検証し直して `RegisterVerified` へ回す）で key を無視する、登録の例外を握って base だけを登録し直す。(4) BeforeSceneLoad が addressables と決めて config を返さない時（1096 行目）に key が残る。(5) 1 回の ledger（`ContentDeliveryPlayBridge`）が、Play に入る前の idle で値を戻す、または key の一部だけを置く。(6) 再入の行（`SnapshotReentry`・`OnePlayLedger`）が、上書きの段を通らない 2 回目の `ExitingEditMode` を通す。(7) build の後、Play の前に上書きの directory が消える・別の物になる。(8) Play 停止で解除に失敗した層が、次の Play の登録の前の再試行で片付かない時（登録は拒まれ、base だけにはならない。S3-2 の `ContentOverlaySessionTests` が持つ）。(9) build の adapter が中で `AssetDatabase.Refresh()` を呼んだ後に compile が始まり、Play に入る前に domain reload が起きる（環境変数と ledger は reload を越えて残るので base だけにはならないが、Play に至るかは決まらない。入口は ledger の直前にも compile 中・import 中を見て止める）。(10) record が「上書きあり」を入口の計画だけから作り、起動の観測と突き合わせない。(11) 検査が重い・失敗した時に、入口が検査を省いて `NotRequired` や `Reused` と答える、または Built + open scenes と同じ値へ自動で切り替える。
  - 探索した境界: 入口の 5 file、`PurposeLaunchSettings`、`ContentDeliveryPlayBridge` の適用と復元、起動の BeforeSceneLoad の config の読みから AfterSceneLoad の `wire-content-resolution` まで、登録の口の失敗の code。未確認: `ContentDeliveryPlayBridge.HandleIdle` が Domain Reload の途中で走る順（Reload の有効な Play で、静的 constructor が値を戻す時点と起動の config の読みの前後）。(7) は登録が内容の hash を見ないので、消えた時（path が無い）だけが失敗になり、書き換えは見分けない（§2.3）。
- **I-2 今ある経路と明示設定は変わらない。** 上書きの key がどの層にも無い時、経路 Source・Built・Built + open scenes、明示設定の素通り、Play As、検証 Play、Delivery の Play は、上書きの計画も build も起こさず、置く key と判定の結果が今と同じである。上書きでない build の成果物と report は変わらない。Player の構成は上書きの key を起動の失敗にする。上書きの key が利用者の層にある時だけは動きが変わり、それは明示設定として扱う: 入口は、今の判定表の明示設定の行で素通りして値を書かず、計画も build もせず、起動が key を読む（照合つきで登録するか、起動の失敗にする。黙って無視しない）。判定表の行の順は変えない。明示設定の行より前の行（batch、Test Runner、再入、directory と再利用しうる Scene の拒否、1 回の ledger）は、上書きの key があっても今と同じに先に当たる。明示の directory に上書きの key を足しても、起動が再利用しうる Scene が開いていれば今と同じく拒否する（program の G18 の保護を保つ）。
  - 主張を崩しうる経路: (1) `PlayEntryDecision.Decide` の行の順（新しい行を足す位置）。(2) Editor の process の環境変数に上書きの key が既にある（別の host の入口が付けた、shell から引き継いだ）状態で、今ある経路や launcher を使う。判定の `HasExplicitSetting` は mode の 3 key だけを見る（`PlayEntryDecision.cs` 149 行目）ので、key を明示設定に含めなければ、入口は介入して自分の値を置き、起動が利用者の意図しない上書きを重ねる。launcher（Play As、検証 Play、Delivery）は上書きの key を見ない。この時は起動が読む（addressables なら起動の失敗、directory なら照合つきの登録）。(3) `SampleGameBootstrapContentComposer` の変更（欠けの記録の引き継ぎ）が、欠けを許さない materialize の build に効く。(4) coordinator の変更が上書きでない build の成功・失敗の扱いを変える。(5) 起動が、上書きの key が無い時の登録の呼び方を変える。(6) 新しい経路を選んだ後に古い code へ戻す。保存した経路の値は `Source` と読まれる（`PlayEntrySettings.cs` 32 行目。上書きは起きず、今ある経路の動きも変わらない）。
  - 探索した境界: 上の 6 つ。launcher の前提条件の検査に上書きの key は足さない（launcher の file を変えない。起動が fail-closed に読む）。
- **I-3 作り直せない root を黙って混ぜない。** 作り直せない root がある Play は、その root と理由を、Play のたびに Console と record に出す。出すのは今回の計画の集合である。黙らせる設定は無い。出す時に、Play で何が起きるかの違う 3 つの組を分けて示す: base の版で載る root（理由が `SourceAbsent`・`NotInLocalSelection`）、base にも上書きにも無く要求すると失敗する root（`NotInBase`）、base に閉包の記録が無いのでどちらか見分けられない root（`RecordMissing`・`RecordUnsupported`・`RecordInvalid`）。
  - 主張を崩しうる経路: (1) 再利用の経路。`GetReusableResult` が返す preflight は前回の build の記録で、今回の集合ではない。(2) 状態 `UnbuildableOnly`（build が無い）で、上書きの段が「何もしない」として表示を飛ばす。(3) Console の 1 行の長さの制限で一覧を切り、切ったことを示さない。(4) record を Play に入った後の観測で上書きして、計画の欄を消す。(5) 理由の違う root を 1 つの「作り直せない」の一覧にまとめ、「base の版で動く」とだけ書く（`NotInBase` には載る base の entry が無い）。
  - 探索した境界: `PlayEntryRecord` の policy と record の作り方、計画の public の面。
- **I-4 入口は利用者の物を壊さない。** 新しい経路の Play は、開いている Scene の file・Dirty・path を変えず、installed cache と base の directory を書かず、明示設定を書き換えない。検出できた未保存の asset を、確認を通さずに file に書かない。source（tracked の file）に書くのは、利用者が確認で「保存して続ける」と答えた時の保存と、今ある経路 Built + open scenes の複製の準備が今も起こしている書き込み（Scene の保存の hook が複製の保存で走るなら、その書き込み。§4 の (1) の (v) で特定する）だけである。他に書くのは、成果物の root の下、複製の置き場、1 回の ledger である。
  - 主張を崩しうる経路: (1) 対話の Editor での Content Directory の build が、開いている Scene を閉じる・保存を求める・Dirty を倒す（未確認 (i)）。(2) 複製の保存（`SaveScene(…, saveAsCopy: true)`）は Dirty の asset を全て file に書く（§1 の実測）。入口は複製の前に未保存の asset を列挙し、1 件以上あれば確認する。列挙に掛からない Dirty（§1 の「測っていないもの」）は、確認なしで複製の保存に書かれうる（既知の限界として 18 章に書く）。確認の後から複製までの間に、新しく Dirty になる asset がある。(3) build の adapter の `AssetDatabase.Refresh()` が複製の置き場に効く（未確認 (iv)）。(4) 失敗した後の後始末が複製の解除を飛ばす。(5) 複製の保存が Scene の保存の hook（`SceneVolumeSaveHook`）を走らせ、開いている Scene の `SceneResource` を書く（未確認 (v)）。走るなら、今ある経路でも起きている書き込みで、本書は増やしも減らしもしない。その `SceneResource` は SceneResourceMap の root の閉包に入るので、計画には編集として出る。(6) 判定が拒否した Play で、列挙・確認・保存をする。(7) 「やめる」の後に、複製や保存が走る。
  - 探索した境界: 入口の順序と adapter の契約（18 章「Scene に焼き込む prefab と model の編集」）、複製の保存の実測（§1）。未確認は上の (i)(ii)(iv)(v)(vii)。
- **I-5 再利用の記録の失敗は、成功した上書きも、既存の記録も壊さない。** 記録の書き込みが失敗しても、成功した上書きの成果物と成功の outcome は残り、build の結果は成功である。既存の記録は、置き換えが終わるまで読める。記録の失敗の後に、今の鍵と違う上書きが再利用されることは無い。
  - 主張を崩しうる経路: `BuildContentCoordinator.Build` の 134〜158 行目、`ContentOverlayReuse.Write` の 88〜91 行目、`TryGet` の全ての null の条件（記録が古いまま残った時に、鍵の違う計画へ古い結果を返さないこと）。
  - 探索した境界: `ContentOverlayReuse.cs` の全体と coordinator の `try` の中。成功の outcome の書き込み自体（134 行目）の失敗は、今までどおり build の失敗であり、変えない（outcome が無い成果物は再利用も診断もできない）。

**成立条件（起きなければならないこと）**

- **S-1** 経路 Built + local edits で、開いていない Scene から見える asset を編集して Play を押すと、開発者の入力なしに上書きが作られ、base → 上書きの順に登録された Play に入り、その Scene は手元の版で載る（record は上書きを指す）。
- **S-2** base と違う file が開いている Scene の file だけの時（保存した編集）と、開いている Scene の保存していない編集だけの時は、上書きを build せずに Play に入る（program §5.3 の見直しの条件 (1)）。Scene の保存が他の asset（`SceneResource` 等）を書き換えた時は、その asset の編集として計画に出るので、S-2 の対象ではない（§2.9）。
- **S-3** 編集の無い Play は build 0 回で、上書きを登録しない。同じ編集のままの再 Play は build 0 回で、前回の上書きを登録する（見直しの条件 (2)）。
- **S-4** Reload Domain の無効と有効の両方で、上書きの Play → 停止 → 上書きの Play が成り立つ。
- **S-5**（AC4 の (m)、AC5 の V5、AC8）作り直せない root がある部分 checkout で、Play に入る。base に entry がある root（理由が `SourceAbsent`・`NotInLocalSelection`）は、要求すると base から載り、その直接参照は base の build の時の版で出る。base にも上書きにも無い root（`NotInBase`）は、要求すると解決の失敗になり、古い版も別の物も出ない。Play に入れたことは、編集が全ての root に反映されたことを意味しない。
- **S-6** 入口は、上書きに入れた root の数、作り直せない root の数（組ごと）と、Play の前の検査の所要を 4 つに分けて Console と record に出す: 選択の準備（materialize・選択・合成）、計画（編集の検出・分類・鍵。計画の 1 回の呼び出しで、中は分けない）、再利用の確かめ、build。Framework も入口も閾値を持たない。
- **S-7** 再利用の記録の書き込みが失敗した build は成功を返し、失敗の理由を結果に持つ。入口はそれを示して Play に入る。

### 2.2 受け入れ条件

AC1〜AC4 と AC6 は EditMode の単体 test と機械検査、AC5 と AC7 の実 Editor の部分は §5 の検証経路、AC8 は合成 fixture の実機の test で確かめる。

- **AC1 再利用の記録（I-5、S-7）。** fake の adapter の単体 test（`ContentOverlayBuildAndReuseTests` に足す）。
  - (a) 既存の記録がある状態で、記録の置き場を書き込めなくして（既存の記録の file を他から開いたままにする等）、鍵の違う計画の上書きを build する。結果は成功（`IsSuccess`）、`content/<identity>/` が残り、outcome は成功のまま、既存の記録の bytes は build の前と同じ、結果が記録の失敗の理由を持つ。
  - (b) (a) の後、今回の計画の `GetReusableResult` は null を返す。既存の記録と同じ鍵の計画には、既存の結果を返す。
  - (c) 記録が無い状態で、置き場を作れなくして（`overlay` を file にする等）build する。結果は成功で、理由を持つ。
  - (d) 置き換えは、既存の記録を消す段を持たない（置き換えの 1 回の操作で差し替える。構造レビューで確かめる）。別名の file は、成功でも失敗でも残さない。
  - (e) 書き込みが成功する経路の既存の test が、変更なしで通る。
  - 合格例: 記録の失敗の後に同じ鍵でもう一度計画すると、build が 1 回走り、記録が書ける状態なら記録が今回の build に置き換わる。不合格例: 記録の失敗で結果が失敗になる。final が消える。outcome が失敗に書き換わる。既存の記録が消える。
- **AC2 起動の設定（I-1、I-2）。** config から上書きの登録を決める関数の単体 test（Unity を使わない）と、起動の呼び分けの code の読み。
  - (a) 上書きの key が 2 つとも無い: 上書きなし。登録の呼び方は今と同じ。
  - (b) 2 つともあり、Editor の Play、directory mode、検証済みの base（installed の pair）、Player の構成でない: 上書きつきの登録。
  - (c) 片方だけ、または空文字: 起動の失敗。
  - (d) 検証済みでない base（`directoryPath` の明示）: 起動の失敗。
  - (e) addressables mode: 起動の失敗。
  - (f) Player の構成、または Editor の Play でない: 起動の失敗。
  - (g) 起動は (b) の時だけ `RegisterVerifiedWithOverlay` を呼び、その例外を握らない。base だけの登録へやり直さない。登録に成功した時は、上書きの path・identity・base の revision の 1 行を log に出す。
  - (h) (e) の検査は、addressables の時に config を返さない分岐より前にある（code の読み）。
  - (i) key の名前と log の marker の正本は Runtime の internal の定数で、Framework の Editor の public の定数がそれを公開し直す。SampleGame と別の host は public の定数だけを使い、文字列を複写しない（構造レビュー）。
  - (j) mode も installed の pair も無く（今の起動が失敗にする設定）、上書きの key だけがある: 起動の失敗。key を無視して進まない。
  - 不合格例: (c)〜(f) と (j) で key を無視して base だけを登録する、または上書きなしで起動を続ける。
- **AC3 判定表と置く値（I-2）。** `PlayEntryDecision` と `PurposeLaunchSettings` の単体 test。
  - (a) 今ある 3 経路の既存の test が、期待値の変更なしで通る。
  - (b) 新しい経路は、Built + open scenes と同じ拒否（扱えない設定、候補なし、載せる Scene なし、扱えない Scene）を同じ順で返し、通った時だけ新しい rule になる。
  - (c) 再入の行は、新しい経路でも、入口が準備した session と 1 回の ledger が揃った時だけ通る。
  - (d) 上書きを登録する Play が置く値は、Built + open scenes の値に上書きの key 2 つを足したものである。上書きを登録しない Play（計画が `NotRequired` か `UnbuildableOnly`）が置く値は、Built + open scenes と同じで、上書きの key を含まない。
  - (e) 上書きの key は明示設定の判定に入る。判定表の行の順は変えない。次の 4 つを別々の test にする（(e-1)〜(e-3) は 4 つの経路のそれぞれで、(e-4) は新しい経路で）。
    - (e-1) 再利用しうる Scene が無く、環境変数に directory の mode・installed の pair・上書きの key がある: rule は `ExplicitSetting` の素通りで、置く値は空。
    - (e-2) `Title` が開いていて、同じ明示設定がある: rule は今と同じ `DirectoryWithReusableScenes` の拒否（上書きの key の有無で変わらない）。
    - (e-3) mode の key が無く、上書きの key だけがある: rule は `ExplicitSetting` の素通りで、置く値は空（入口は介入しない。起動が AC2 で失敗にする）。
    - (e-4) 入口自身が 1 回の ledger で上書きの key を置いた後の再入の 2 回目の `ExitingEditMode`: 今と同じ再入の行（`SnapshotReentry`）で通り、`ExplicitSetting` にならない。
    - 不合格例: (e-1) で `BuiltRoute` の介入になる。(e-2) で素通りになる。
- **AC4 入口の上書きの段（I-1、I-3、I-4、S-2、S-3、S-5、S-6、S-7）。** port を fake にした単体 test（`PlayEntryTransactionTests` と、新しい段の test）。
  - (a) 順序は、再判定（新しい経路の介入と決まる）→ 未保存の asset の列挙 →（1 件以上なら確認 →「保存して続ける」なら保存）→ 複製の準備 → import の反映 → 計画 → 認める → 再利用か build → 1 回の ledger → 計画の公開 → `EnterPlaymode`。今ある経路では、列挙・確認・保存・import の反映・計画のどの port も呼ばれない。新しい経路でも、判定が拒否した時と facts の採取が失敗した時は、どの port も呼ばれない。
  - (b) 計画が `NotRequired`: build 0 回、上書きの key なし、record の上書きの状態は `NotRequired`。
  - (c) 計画が `BuildRequired` で再利用できる: build 0 回、key は前回の成果物、状態は `Reused`。
  - (d) 計画が `BuildRequired` で再利用できない: build 1 回、key は今回の成果物、状態は `Built`、build の所要が record に入る。
  - (e) 計画が `UnbuildableOnly`: build 0 回、key なし、作り直せない root と理由が Warning と record に出る。
  - (f) `BuildRequired` で作り直せない root がある: 今回の計画の集合をそのまま認めて (c) か (d) に進み、今回の集合が Warning と record に出る（再利用の時も）。
  - (g) 複製の準備の例外、計画の例外、build の失敗（`IsSuccess` が偽）、compile 中・import 中（import の反映の直後と、1 回の ledger の直前の 2 か所で見る）: 1 回の ledger を置かず、`EnterPlaymode` を呼ばず、複製を解除し、`Blocked` に理由を出す。build の失敗の理由は issue の code と対象を含む。成功した上書きの成果物は消さない（次の Play の再利用の対象として残る）。
  - (h) 記録の失敗の理由を持つ成功（AC1）: Play に入り、理由が Warning と record に出る。
  - (i) 計画に渡す「他の供給元が届ける Scene」は、複製にして載せる Scene のうち SceneResourceMap にあるものの logical key だけである。
  - (j) Console の 1 行は root の一覧を上限で切る時、残りの件数を出す。詳細窓は全件を出す。
  - (k) 作り直せない root の表示を止める設定・保存された「今後表示しない」が code に無い（構造レビュー）。
  - (l) 入口が新しい経路で介入した Play では、record は、計画した上書きの identity と、起動の log の上書きつきの登録の行の identity を突き合わせる。見る区間は、今の record が起動の log（封じの行、取り込みの行）を観測する区間と同じである。計画は上書きありで、その区間に identity の一致する行が無い時と、計画は上書きなしで登録の行があった時は、record は不一致を最初の失敗として示し、状態を `StartupFailed` にする（`PlayEntryRecordTests`）。入口が素通りした Play（明示設定など）では突き合わせず、観測した登録の行だけを出す。
  - (m) 作り直せない root の 6 つの理由は、I-3 の 3 つの組に分かれて Warning と record に出る。`NotInBase` の root を含む計画でも Play に入り、その root は「要求すると失敗する」の組に出て、「base の版で載る」の組に出ない。
  - (n) 計画の例外、再利用の確かめの例外、所要の長さのどれでも、入口は検査を省いた結果（`NotRequired`・`Reused`）を作らず、Built + open scenes と同じ値で Play に入らない（(g) と同じく `Blocked`）。
  - (o) record は S-6 の 4 つの所要を別々の欄で持つ。行わなかった段（再利用できた時の build 等）は、0 ではなく「行っていない」と読める値にする。
  - (p) 未保存の asset の確認。列挙と確認と保存は port で受ける。列挙の対象は asset で、開いている Scene の未保存の内容は対象ではない（複製が届ける）。0 件: 確認も保存も呼ばれず、複製の準備へ進む。「保存していない asset は無い」とは表示しない（検出できた範囲だけを述べる）。1 件以上で「保存して続ける」: 保存の port が 1 回呼ばれ、その後に複製の準備へ進む。1 件以上で「やめる」: 保存・複製・計画・ledger・`EnterPlaymode` のどの port も呼ばれず、`Blocked` に理由が出る。確認に渡す文は、未保存の asset の一覧と、「続けると、project の保存していない asset を全て保存する」の文を含む（文の定数を test が照合する）。確認を省く設定・保存された「今後表示しない」が code に無い（構造レビュー）。判定用の応答の先置き（下の AC5）は、確認の段に着いた時に 1 回だけ読んで消す。検出が 0 件で確認を出さない時も消す（残った応答が、後の Play の確認に勝手に答えない）。先置きが無ければ、Editor の確認の dialog を出す。
  - 合格例: (f) で前回の build の時は作り直せない root が 1 つ、今回は 2 つなら、2 つが出る。不合格例: (g) の後に Built + open scenes の値で Play に入る。
- **AC5 実 Editor の縦断（S-1〜S-5、I-1、I-4）。** SampleGame、経路 Built + local edits、§5 の V1〜V7。判定用の base は、判定の担当が候補 head の checkout から検証 Play で作る。
  - V1c: 開いていない Scene から見える asset（開いている `Title` が参照しない material）を編集し、保存せずに Play して、確認に「やめる」と答える。Play に入らない（record は `Blocked` と理由）。material の file の bytes と Dirty、`Title` の path と Dirty は前と同じ。複製の置き場に今回の folder が無く、1 回の ledger が無く、`playModeStartScene` は元のまま、`reports/` に新しい directory が増えず、`git status --porcelain` は空のまま。
  - V1: V1c の続きで（material は Dirty のまま）、もう一度 Play して、確認に「保存して続ける」と答える。入口が保存し、build 1 回、起動の log に上書きつきの登録の 1 行、その Scene の record（`SceneDirector.GetContentLoadRecord`）の供給元が `ContentOverlay` で、編集した値が Play の中で手元の値で読める（HEAD の値と違う）。
  - V2: 停止 → そのまま再 Play。build 0 回、状態 `Reused`、V1 と同じ登録と値。
  - V3: 編集を元に戻す → Play。状態 `NotRequired`、上書きの key なし、上書きの登録の行なし。
  - V4a: 開いている Scene を編集し、保存せずに Play。build 0 回、状態 `NotRequired`、その Scene は複製から載り（record は `EditorSnapshot`）、編集が Play の中で読める。停止の後、Scene は Dirty のままである。
  - V4b: 続けて同じ編集を保存して Play。build 0 回、状態 `NotRequired`、計画の「他の供給元」にその Scene が 1 件あり、上書きの root は 0 件。編集は、Scene の保存の hook が他の asset を書き換えないもの（GameObject の名前）にする。保存で `SceneResource` などが書き換わった時は、V4b を不合格にせず、その事実を §7 に書いて owner に返す（S-2 の対象外の場合に当たる）。
  - V5: 開いていない Scene B を `.meta` と一緒に `Assets` の外へ退かし、B と、手元にある開いていない Scene C の両方の閉包に入る asset を編集 → Play。C は上書き、B は作り直せない root（base の版で載る組、理由 `SourceAbsent`）として Warning と record に名前つきで出て、Play に入る。B を要求すると base から載る（record は base を指す）。編集した asset の値を、C の参照から読むと手元の値、B の参照から読むと base の build の時の値（HEAD の値）である。つまり V5 は、Play に入れることに加えて、B が意図して古い版のままであることと、開発者がそれを Warning と record から B の名前で識別できることを観測する。後で B を戻す。
  - V6: 空の Scene だけを開いた明示設定の Play。経路 Built が置くのと同じ key（mode・installed の pair・build の identity・表現・初回 Scene）に上書きの key 2 つを足して環境変数に置き、上書きの path は V1 の成果物、identity は別の値にする。起動は登録の失敗で止まり（失敗の code は log のものを記録する）、要求時解決の配線の行（`Content resolution: wired=true`）が出ず、record は `StartupFailed`。base だけの Play として始まらない。登録の失敗の code ごとの照合（`OverlayBaseMissing`・`OverlayBaseMismatch`・`InvalidRoot`）は S3-2 の `ContentOverlaySessionTests` が持つ。
  - V7: Reload Domain を有効にして、V1 と同じ手順を V1 と違う値の編集で行い（鍵が変わるので build 1 回）、停止してそのまま再 Play する（build 0 回、`Reused`）。2 回とも上書きつきの登録が成功し、値が手元の値で読める。
  - working tree の読み方: base の準備（検証 Play）の後に 1 回、`git status --porcelain` が空で Dirty の asset が無いこと（基準）を記録する。V1c と V1 だけが、未保存の asset を残して Play する（確認が出る）。他の回は未保存の asset を残さずに Play する（確認は出ない）。確認を通らない Play は source に何も書かない（I-4）ので、あってよい差分は、判定の担当が準備で作って保存した差分と、V1 で確認に答えて入口が保存した差分だけである。回ごとに「開始の時にあってよい差分」と「終了の時にあってよい差分」を下の表で固定し、それ以外の差分が 1 つでもあれば不合格である。Dirty にする Scene（V4a）は、体積の変わらない編集にする（複製の保存で Scene の保存の hook が走ると分かった時、体積の変わる編集では `SceneResource` が書かれる。今ある経路と同じ動きで、本書の合否の対象ではない）。回は表の順に行う。

    | 回 | 準備 | 開始の時にあってよい差分 | 終了の時にあってよい差分 | 復元 |
    |---|---|---|---|---|
    | V0 | なし | なし | なし | なし |
    | V1c | material の色を変える（保存しない）。応答「やめる」を先に置く | なし（Dirty は material だけ） | なし（material は Dirty のまま。Play に入っていない） | しない |
    | V1 | 応答「保存して続ける」を先に置く | なし（Dirty は material だけ） | material の file（確認の答えで入口が保存した）。これを「V1 の差分」として記録する | しない |
    | V2 | なし | V1 の差分 | V1 の差分と同じ | しない |
    | V3 | material を戻す | なし | なし | なし |
    | V4a | `Title` を Dirty にする（保存しない） | なし（Dirty は `Title` だけ） | なし（`Title` は Dirty のまま） | しない |
    | V4b | `Title` を保存する | `Title` の file | `Title` の file | `Title` を戻し、開き直す |
    | V5 | B を退かす、material の色を変えて保存する（V1 と違う値） | B の `.unity` と `.meta` の削除、Addressables の設定（退かした時に書き換わった file。一覧を記録）、material の file | 開始の差分と同じ | B を戻し、material と Addressables の設定を戻す。空に戻ったことを確かめる |
    | V6 | 空の Scene を開く、環境変数を置く | なし | なし | 環境変数を消す。`Title` を開き直す |
    | V7 | Reload Domain を有効にする、material の色を変えて保存する（V1・V5 と違う値） | Enter Play Mode の設定の file（変わるなら）、material の file | 開始の差分と同じ（2 回の Play の後） | material と Enter Play Mode の設定を戻す。設定が元の値で、空に戻ったことを確かめる |

  - 確認への答え方: 実 Editor の `eval` は modal の dialog に答えられないので、確認の port は、判定用に先に置いた 1 回だけの応答（`SessionState`。AC4 の (p)）があればそれを使う。足すのはこの口だけで、判定用の基盤は作らない。V1c と V1 が通すのは、実際の列挙、入口の順序、`AssetDatabase.SaveAssets()`、複製の保存、「やめる」の後に何も書かれないことである。dialog の window 自体の表示は、判定に含めない。
  - 各回の前後で、開いている Scene の path と Dirty が同じことも確かめる（準備と復元の操作を除く）。V1 では installed の revision の file の一覧と大きさが前後で同じことも確かめる。
  - 検査の費用の記録（合否に使わない）: V1・V2・V3 で、record の 4 つの所要、上書きに入れた root の数、検査の対象の file の件数と容量（推定）、再利用の確かめの対象の成果物の file の件数と容量を記録する。対象の件数と容量は、判定の担当が計画の契約から数える（base の閉包の記録の file のうち手元にあるものと、手元の selected の root の閉包の和集合。`.meta` を含む。成果物は `content/<identity>/` の下）。これは検査の対象になりうる重複の無い file の量であり、実際に読んだ bytes ではない（計画の検出と、`BuildRequired` の時の hash の取り直しは、同じ file を複数回読みうる）。実際の読み取りの量は測らない（計画の code に計測を足さない）。絶対の秒数と閾値は判定に使わない。
  - 読み方: 「build 0 回」は、成果物の root の `reports/` に新しい directory が増えないことで読む（report の directory は preflight の段で必ず作られる）。V1〜V6 は Reload Domain 無効（project の既定）で行う。
- **AC6 変更の範囲（I-2、program §5.3 の見直しの条件 (3)）。** `git diff --stat <implementation base>..<head>` で次を確かめる。
  - `unity/Assets/OneStarMaker/Scripts/Runtime/` の差分は、`Bootstrap/AbstractApplicationInitializer.cs` と、起動の設定を読む新しい file 1 つ（と `.meta`）だけ。`ContentResolution/`・`BuildContent/`・`AssetManagement/`・`SceneSystem/` に差分が無い。
  - `Runtime/` に足す型は internal で、public の型・member・enum の値を足さない。`Runtime/` の外の Framework に足す public の面は、`BuildContentResult` の読み取り専用の欄 1 つと、Framework の Editor の定数の型 1 つだけである。Framework の複製の準備（`OneStarMaker/Scripts/Editor/PlaySnapshot/`）に差分が無い。
  - asmdef と `InternalsVisibleTo` に差分が無い。
  - `pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1` が通る。
- **AC7 今ある経路の非退行（I-2）。** 判定必須の既存の test（§5）と最終の全 EditMode 回帰が通る。実 Editor で経路 Built + open scenes の Play を 1 回行い（§5 の V0）、成果物の root に新しい report が増えず、置いた key に上書きの key が無く、Play に入ることを確かめる。

- **AC8 `NotInBase` の root の要求（S-5）。** 分類の test（計画が `NotInBase` と答える。`ContentOverlayPlannerTests` が既に持つ）と、要求の test は別物として扱う。合成 fixture の実機の test（`OverlayFromLocalEditsIntegrationTests` に 1 回足す）で、次を 1 つの流れで確かめる。(1) base の閉包の記録に無い root が、手元の選択にあり、source が手元に無い。(2) 計画はその root を `NotInBase` と答える。(3) 入口の規則と同じく、作り直せない集合をそのまま認めて上書きを作り、base → 上書きで登録して Play に入る。(4) その root を要求すると、見つからない種類の失敗になり、base と上書きの他の entry（別の表現、別の key）を代わりに採らない。同じ Play で、他の Scene の要求は今までどおり成功する。(4) を S3-2 の既存の実機の test が既に同じ条件で持つと B が確かめた時は、test を足さずに、その test の名前と assertion を §6 に書けばよい。

### 2.3 ここでは答えない問いと所有する後続

| 問い | 所有 |
|---|---|
| 開いている Scene を載せずに上書きだけを重ね、宣言の初回 Scene から始める Play。入口の Framework への一般化、経路の統合と menu の整理 | S5 |
| script・`Packages/`・import 時の依存・project の設定の変更を、検出と再利用の鍵に入れること（S3-4a が A1 に所有の決定を求めた） | BuildSystem の後続（18 章「BuildSystem の後続」）。入口は計画の結果をそのまま使い、計画の中身を変えない |
| 記録の無い base で、既にある asset だけを指す root の宣言を手元で足した場合と、記録に無い root の依存だけが手元に無い場合（同上） | S4（base の選び方。記録を持つ base を選ぶ規則と一緒に決める） |
| build の後に上書きの directory が書き換わった時の検出（登録は内容の hash を見ない。再利用の受け取りは hash を見る） | S4 |
| Play の間の script の compile（Domain Reload）で session が失われた後の扱い | S4（登録の寿命。1 directory の Play と同じ性質） |
| 上書きを実機（exe・Android・iOS の Player）に載せること。owner の見立て（2026-11-22）では、部分 checkout で content の build を使う主な用途は、編集を実機ですぐ試すことである。Editor では、編集してそのまま Editor で Play する使い方（今の Built + open scenes）が主になる。本書が Editor で上書きを重ねるのは、上書きの方式の縦断の実証として検証が安いからで、実機は後続で行う。Editor の Play の結論を Player で確かめ直すことも含む | S4（Player の実機）。上書きを Player に重ねることは、13 章の契約では未測定のため禁止のままである |
| base より古い checkout と偽の編集の見分け、検出の所要を減らす工夫、古い上書きの掃除、再利用の記録を複数持つこと | S4・S5（S3-4a が移送済み） |
| Play の前の検査を、正しさを失わずに軽くすること。第一の候補は「変わったものだけを確かめる」道（初回の全量の照合で基準を作り、Editor の import・asset の変更の通知から GUID を追い、確実に変わっていない file の hash を省く。再利用の成果物の確かめも、確かめ済みで変わらない成果物に限って省く）。通知だけを「変更が無い証拠」にせず、Editor を閉じている間の checkout・外部の編集・移動・削除・base や Unity や import の条件の変更・記録の破損で基準を捨てて全量へ戻す。正しさは、全量の判定と同じ計画・鍵・作り直せない集合になることの差分の test で持つ | S5（Phase D が、AC5 の検査の費用の記録から「省ける処理」と「今は省けない処理」を S5 の入力として program に書く。本書の受け入れ条件にしない） |
| 全量の検査をしない Play。今は、利用者が明示的に Source・Built・Built + open scenes を選ぶことがその道である。完全性を保証しない速い Play を足す時は、結果の保証の範囲（確かめた・一部・分からない）を区別して設計し、owner が裁定する | S5 |
| 複製の保存に未保存の asset を書かせないこと（Dirty の印を外して複製し、後で戻す。§1 の実測で成り立った）。入れれば、確認なしで、保存もせずに Play できる。今ある経路 Built + open scenes と新しい経路の両方に効く | owner の裁定を経て S5（Undo・sub-asset・Dirty の Scene の測定と一緒に。本書は確認だけにする） |
| 保存していない asset の検出の範囲を広げること（import の設定の未適用の変更、検出に掛からない Dirty） | S5 |
| `NotInBase` の root がある時に、Play の前に止めるか（U2 は止めない）。記録の無い base の欠けた root が base にあるかを manifest の `index` から見分けること | S4（base の選び方。base が checkout より古い時に生じる） |
| 上書きのために S3-2 が足した暫定の public の値を確定するか | Phase D が、見直しの条件 (1)〜(3) の結果を program §5.3 に書く。本書の受け入れ条件にしない |

入口は、明示 close が失敗した session を捨てる経路を作らない（S3-2 からの移送への答え。Play 停止と登録の巻き戻しの再試行は S3-2 のまま）。

### 2.4 代替案の比較

- **P1 上書きをどの経路に載せるか。**
  - P1-a（採る）: 経路を 1 つ足す（`Built + local edits`）。Built + open scenes の条件と複製をそのまま使い、上書きの段だけを足す。今ある 3 経路は変えない。
  - P1-b: Built + open scenes に上書きの段を足す。開発者の選択肢は増えないが、今その経路を使う全員が、Play のたびの計画（閉包の file の hash。速くする cache は無い）と、宣言の source の materialize の成否を負う。記録の無い base と部分 checkout の組では、編集が無くても毎回の通知が出る。確かめ済みの経路（S0-d・S3-3）の動きが変わる。
  - P1-c: 経路 Built にも載せる。source を持たない利用者 (b) は materialize が成り立たず、計画の失敗で Play できなくなるか、失敗を握って base だけに縮退するかになる。
  - P1-b の利点（開いていない Scene の編集が、通知なしに base の版で出る今の動きを無くせる）は、検出が安くなった後の経路の統合（S5）で取る。
- **P2 上書きを起動に渡す形。**
  - P2-a（採る）: `editor:` の名前空間の key 2 つ（path と identity）を、今の 1 回の ledger で process の環境変数に置く。渡すのは文字列だけなので、Domain Reload をまたぐ置き直しが要らない。`content:` の外なので Player の protected config の照合の対象にせず、Player の構成では key の存在を失敗にする。key の名前の正本は Runtime の internal の定数に置き、`editor:playSnapshotExpected` と同じく Framework の Editor が public の定数で公開し直す（別の host はこの定数を使う）。
  - P2-b: `EditorPlaySnapshotBridge` と同じ static の bridge。delegate を渡す snapshot には要るが、文字列 2 つには、置き直しの仕組みと「key はあるが bridge が無い」の失敗の分だけ余分である。
  - P2-c: `content:overlayPath` などを `content:` に足す。Player の protected の集合と baked の値に欄が増える。
- **P3 作り直せない root がある時（program が本書に割り当てた決定）。**
  - P3-a（採る）: 止めずに Play に入り、Play のたびに Warning と record に出す。黙らせる設定は作らない。計画の集合をそのまま認める。base にも上書きにも無い root（`NotInBase`）がある時も止めず、「要求すると失敗する」と分けて示す。この root は、map に root が足された後の checkout で、その source が手元に無く、base がそれより古い時に生じる。入口で止めると、新しい base が届くまで、その root に触れない Play も全て止まる。要求した時は解決の失敗になり（必須の Scene の失敗は fail-closed。program §4.1）、古い版や別の物は出ない。
  - P3-b: 止めて、確認を取ってから入る。共有 asset を編集するたびに確認が要り、program が見直しの引き金とした「日常的な共有 asset の編集のたびに Play を止める」に当たる。
  - P3-c: 止めるか続けるかを設定で選ばせる。利用者の入力が増え、止める側を選ぶ理由を目的から示せない（止めても手元では作り直せない）。
- **P4 順序。**
  - P4-a（採る）: 複製の準備 → import の反映 → 計画 → build。複製の準備は計画より前に終える。複製の保存が source を書く場合（列挙に掛からない Dirty、Scene の保存の hook）でも、計画より前なら計画の hash がそれを編集として拾い、計画より後の source の変更は coordinator の build の前後の hash が失敗にする。
  - P4-b: 計画 → build → 複製の準備（A2 の代替構成の案。複製の置き場が build に効くかという未確認 (iv) を構造で避けられる）。複製の準備が build の後に閉包の file を書くと（Dirty の asset、Scene の保存の hook）、上書きが手元より古いまま Play に入り、どの hash の検査にも掛からない。(iv) は §4 の (1) で先に測る。
- **P5 保存していない asset の編集。** Unity の複製の保存（`SaveScene(…, saveAsCopy: true)`）は、Scene が参照しない物も含めて Dirty の asset を全て file に書く（§1 の実測）。何もしなければ、新しい経路の Play は利用者の未保存の編集を黙って確定する。
  - P5-a（不採用。初稿の案）: 入口が Play の前に黙って `AssetDatabase.SaveAssets()` で保存する。owner が 2026-11-22 に不採用とした（利用者の未保存の編集を勝手に確定しない）。
  - P5-b（不採用）: 保存も確認もしない。Dirty の印を外して複製し、後で戻し、検出できた未保存の asset を知らせる。実測では成り立ったが、Framework の複製の準備の変更と、Undo・sub-asset・Dirty の Scene の追加の測定が要る。Editor の上書きの Play は、上書きの方式を安く実証する足場で、主な使い方ではない（§2.3）ので、ここまで作り込まない（owner、2026-11-22）。今ある経路と合わせて入れるかは S5（§2.3）。
  - P5-c（採る。owner の裁定 U4、2026-11-22）: 検出できた未保存の asset が 1 件以上ある Play でだけ、確認を出す。検出は `Assets/` の下の load 済みで Dirty の object の列挙（§1 の実測）で、未保存が無い Play（大半の再 Play）では確認を出さない。確認の文は、未保存の asset の一覧と、「続けると、project の保存していない asset を全て保存する」ことを明記する。「保存して続ける」なら入口が `AssetDatabase.SaveAssets()` を呼んでから複製と計画へ進み、保存した編集は反映される。「やめる」なら Play に入らず、何も書かない。「今後表示しない」は作らない。確認は build の時だけには絞れない: 保存を起こすのは build ではなく、Play のたびに行う複製の保存なので、build の時だけ確認すると、build の無い Play で確認なしの保存が起きる。列挙に掛からない Dirty（§1 の「測っていないもの」）は、確認なしで複製の保存に書かれうる（既知の限界）。列挙が重くて Play のたびに使えないと分かった時は、owner に返す（毎回確認するか、build の時だけにするかの裁定）。
- **P6 記録の失敗の伝え方。**
  - P6-a（採る）: build の結果は成功で、記録の失敗の理由を結果の欄に持つ。入口が Warning と record に出す。
  - P6-b: 例外にする。今の不具合と同じく、呼び手が成功した成果物を使えなくなる。
  - P6-c: 何も伝えない。次の Play が毎回作り直す理由が分からない。
- **P7 入口の置き場所。** 今の入口と同じ SampleGame の `DependOnAll/Editor/Build/`（採る）。Framework に入口の層を作る案は、host が 1 つの段階で port を増やすので採らない（S5 へ）。Framework に足すのは起動の設定だけである。

### 2.5 スライス分割の判断

**分けない。** 契機は「高リスクの初回実機スライスで独立して失敗しうる未知が複数ある」（§1 の未確認 (i)〜(v)）と、「独立した変更理由が混在する」（再利用の記録の修正は BuildSystem の Editor の code、入口は SampleGame、起動は Runtime の Bootstrap）である。

- 記録の修正を別のスライスにする案: 修正は約 40 行と test で、入口が再利用を使う前に要るという順序の制約だけがある。Phase A〜D をもう 1 回回す費用に見合わない。本書の実装の順の先頭に置き（§4 の (2)）、AC1 で独立に判定する。
- 起動の配線と入口を分ける案: 起動だけのスライスは、Play ボタンからの縦断を持てず、登録の口の test（S3-2・S3-4a で成立済み）と同じことを確かめ直すだけになる。問いは「Play ボタンから入れるか」の 1 つである。
- 記録の修正と未確認の観測だけを先のスライスにする案（A2 の代替構成の提案）: 上の 1 つ目と同じ理由で採らない。観測の結果で止める地点は、§4 の (1) と §2.6 の NO-GO が本書の中に持つ。
- 未知は Phase B の最初の作業（§4 の (1)）で先に観測し、不成立なら実装を積む前に §2.6 の NO-GO で止める。製品 code の増分が §3 の見込み（約 680 行）を大きく超えると B が判断した時は、起動の配線（Framework）と入口（SampleGame）の境界で Phase A へ戻す。

### 2.6 判定定義と停止規則

- GO: AC1〜AC8 が全て合格。
- NO-GO（owner に返す）: (i) 対話の Editor で、入口の tick からの上書きの build が、開いている Scene の内容・Dirty・path を変えるか保存を求め、順序を変えても I-4 を守れない（未確認 (i)）。(ii) Reload Domain 有効の Play で、前の Play の native の登録か lease が残って登録が失敗し、`ContentDirectorySession`・`ContentDirectoryLayers`・`ContentRevisionGate` の変更が要る（未確認 (iii)。見直しの条件 (3)）。(iii) 実装に AC6 の外の `Runtime/` の変更、core の public の値、asmdef の参照か `InternalsVisibleTo` の追加が要る（見直しの条件 (3)）。(iv) §2.9 の評価に反して、編集の種類 A〜F のどれかが、部分 checkout で Play を止める。
- CONDITIONAL ACCEPT は使わない（スパイクではない）。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし。
- 決定記録: なし。

### 2.7 本文へ転記した実装制約

- 依存は Game → Framework の一方向。asmdef の参照と `InternalsVisibleTo` を足さない。SampleGame の入口は、`OneStarMaker.Build.Content` と Runtime は public の口だけを呼び、Selection と Materialization は今ある `InternalsVisibleTo` の範囲（合成が今も使っている constructor）で呼ぶ。登録の口（internal）を呼ぶのは Runtime の起動だけである。
- 新規・編集する `.cs` は先頭に `#nullable enable`。Unity 側で `record` を使わない。
- `UnityEditor` に依存する code は Editor 用 asmdef に置く。`Runtime/` に足すのは、起動の設定を読む internal の型と、起動の呼び分けだけである。
- 公開 API に `ZLogger*` を出さない。
- test で `Task.Delay` / `Thread.Sleep` を使わない。
- 破棄されうる `UnityEngine.Object` は `== null` / `!= null` で判定する。
- tracked の file の path は `tools/path-budget.json` の予算に従う。
- 機械向けのデータ（期待値の一覧、baseline）を git に入れない。
- Unity の版は `unity/ProjectSettings/ProjectVersion.txt`（6000.6.9f1）。
- SampleGame の所要の秒数を、方式や閾値の判断に使わない。入口は所要を出すだけで、閾値を持たない。
- 入口は、Play の前の検査（materialize、編集の検出、鍵、再利用の成果物の確かめ）を省かない。検査が重い時・失敗した時に、確かめていない `NotRequired`・`Reused` を作らず、他の経路の値へ自動で切り替えない。検査を軽くする工夫は足さない（S5）。計測のために計画の code と core の public の面を変えない。
- 上書きは Editor の Play だけで使う。base 1 つと上書き 0 か 1 つ、登録は最初の load より前、解除は Play の終わり（13 章の契約を変えない）。
- 入口は、検出できた未保存の asset を、確認を通さずに file に保存しない。未保存の asset の検出のために Runtime と core の責務を増やさず、Framework の複製の準備（`PlaySnapshot`）も変えない。
- Phase B は Unity の test と batch の build を走らせない。§4 の (1) の観測は、開いている Editor の `tools/unity-editor.cmd eval` で行う。

### 2.8 owner の裁定が要る点と、担当が決めた点

owner の裁定（2026-11-22、PR #203 の owner のコメントと、その後の対話）。U1・U2・U3・U5・U6 は下の案のとおり承認された。U4 は owner が改めた（初稿の「入口が保存する」→「保存せず知らせる」→「未保存がある時だけ確認する」）。U4 に合わせて目的と利用者の文言を 1 文ずつ改め、owner が承認した（U1）:

- **U1 目的と利用者の文言**（§1）。A1 の文言と、U4 に合わせて改めた 2 か所（§1 の経緯）は、どちらも承認済みである。
- **U2 作り直せない root がある時は、止めずに Play に入り、毎回示す**（§2.4 P3-a、§2.9）。program が本書に割り当てた決定で、S3-4a の担当の見立てと同じである。base にも上書きにも無い root（`NotInBase`）がある時も止めず、「要求すると失敗する」と分けて示す（外部計画レビュー 1 巡目の Major 3。入口で拒否する案との裁定）。
- **U3 経路を 1 つ足す**（§2.4 P1-a）。今の Built + open scenes には上書きを足さない。
- **U4 保存していない asset は、確認の上でだけ保存する**（§2.4 P5-c。owner の裁定）。検出できた未保存の asset が 1 件以上ある Play でだけ、「project の保存していない asset を全て保存して続けるか」を確かめる。検出できた未保存の asset を黙って保存しない。Dirty の印を外して保存そのものを避ける作り込みはしない（Editor の上書きの Play は足場で、主な使い方ではない。§2.3）。
- **U5 見直しの条件 (3) の読み。** 「2 層のための Runtime の特例」は、解決・session・gate の中の分岐と public の値を指し、program の行が本書に割り当てた起動の配線（config の key 2 つの読みと、登録の口の呼び分け）は数えない。AC6 がこの読みで範囲を固定する。
- **U6 利用者の層にある上書きの key は明示設定として扱い、Scene の混在の安全のための拒否は明示設定より先に掛ける**（I-2、AC3 の (e)）。判定表の行の順は変えない。その key がある時、入口は明示設定の行で素通りし、起動が key を読む。明示の directory と上書きの key があっても、再利用しうる Scene が開いていれば今と同じく拒否する。別の host の明示の上書きに限って混在を許す案は採らない（外部計画レビュー 1 巡目の Major 1 の裁定）。

担当が決めた点:

- key の名前は `editor:contentOverlayPath` と `editor:contentOverlayIdentity`（§2.4 P2-a）。
- 上書きの contentSet は、host が宣言の名前から機械で作る（base と違う名前。coordinator は base と同じ contentSet を拒む）。成果物の root は今の `artifacts/bs2b`。
- Player の build を判定必須にしない（§5）。
- §2.3 の所有の割り当て。
- A2 は 4 件（アーキテクチャゲート、不変条件の反証と AC の読み違い、目的レビュー、A0 だけからの代替構成）。
- key の名前と log の marker の正本の置き場（Runtime の internal の定数と、Framework の Editor の public の定数）。
- 上書きの段の port の境界（§3）。

### 2.9 fail-closed せざるを得ない編集の頻度と体験の評価（program の行の「最初の作業」）

program は、入口の配線の前に、部分 checkout で Play を止めざるを得ない編集がどれくらい生じるかを評価し、日常的な共有 asset の編集のたびに Play を止めるなら方式を見直すことを求めている。評価は構造で行い、SampleGame の件数や秒数を使わない。

**新しい経路で Play が止まるのは次の時だけである。**

| 止まる時 | 編集の種類に依るか |
|---|---|
| base の manifest が install の記録と合わない（計画の例外） | 依らない。cache の破損 |
| 手元の構造の失敗（`SceneResourceMap`・`SceneResource` の asset が無い、GUID と path の不一致、衝突、graph の欠損）と、宣言の選択の失敗 | 依らない。map を持たない checkout は、この経路を使えない（前提） |
| build の失敗（計画の後に閉包の file が消えた・変わった、Unity の build の失敗） | 依らない。build の間に source を変えた時と、壊れた asset |
| 起動の登録の失敗（上書きの directory が無い、base との照合の不一致） | 依らない。再利用の鍵は base の識別を含むので、別の base の上書きは使い回されない |
| 今の Built + open scenes と同じ拒否（Scene reload が無効、等） | 依らない |

**program §5.3 の編集の種類ごとの結果（全 checkout と部分 checkout）。**

| 編集 | 結果 | Play |
|---|---|---|
| A 開いている Scene 自体 | 複製が届ける。build なし | 入る |
| B 局所的な asset。参照する root が全て手元にある | 上書きを build（参照する root の分） | 入る |
| C 広く共有された asset。参照する root が全て手元にある | 上書きを build（重い）。root の数と所要が出る | 入る |
| D 広く共有された asset。参照する root の一部が手元に無い | 手元にある root は上書き、無い root は作り直せない root として毎回出る。無い root は base の版のままで、編集は反映されない | 入る（U2） |
| E 新しい asset を参照に足す | 上書きを build | 入る |
| F 共有 asset の繰り返しの調整 | 開いている Scene から見える分は複製が手元の版で出す（S3-3 の O-1・O-2）。開いていない root は C か D | 入る |

**結論:** U2 の規則の下では、A〜F のどれも Play を止めない。これは「止めずに続ける」と選んだ結果であり、Play に入れることと、編集が期待した全ての root に反映された状態で確かめられることは別である。A・B・C・E は全ての root に反映される。D は手元にある root にだけ反映され、手元に無い root は base の版のままで、どの root がそうかが毎回示される。部分 checkout で日常的に起きるのは「止まる」ことではなく、D の通知が毎回出ること、D の root を手元では確かめられないこと、C・D の build の重さである。上書きの方式を続けるかの判断の材料として、AC5 の V5 が、base に残った Scene の依存が意図して古い版であることと、それを開発者が識別できることを観測する。この評価に反する観測が出たら §2.6 の NO-GO (iv) で止める。

**構造から分かる体験の弱点（止めはしないが、導入先が評価するもの）:**

- 記録の無い base（S3-4a より前に publish した build）と部分 checkout の組では、編集が無くても、欠けた root が全て作り直せない root として毎回出る（計画の契約）。記録を持つ base に替われば消える。
- base より古い checkout と偽の編集は、編集として計画に出て、要らない上書きが作られる。入口は root の数と所要を出すので気づける。見分けは S4。
- 計画は Play のたびに閉包の file を hash し、再利用の確かめは成果物の file を全て hash する。「build 0 回」の Play でも、検査の読み込みは 0 ではない。所要は project の規模で決まり、入口が 4 つに分けて出す（S-6）。大きな project では、build 0 回でも Play の体験が成り立たない可能性がある。本書は正しい全量の判定を基準として保ち、軽くする道は S5 が設計する（§2.3）。費用が許せない利用者は、今ある経路を明示的に選ぶ。
- base にも上書きにも無い root（`NotInBase`）は、base が checkout より古い時に生じ、要求すると失敗する。入口は分けて示す。base の選び方は S4。
- 検出できた未保存の asset が 1 件以上ある Play では確認が出る。「保存して続ける」で project の未保存の asset が全て保存されて上書きの対象になり、「やめる」で Play に入らない。検出が 0 件の Play は、Play ボタンを押すだけで入れる。0 件は、未保存の asset が無いことの証明ではない（検出に掛からない Dirty は、確認なしで複製の保存に書かれうる）。
- 開いている Scene を保存した時に、保存の hook が `SceneResource` を書き換えると、それは SceneResourceMap の root の編集として計画に出て、上書きが作られる（Scene だけの編集ではなくなる）。複製の保存でも hook が走るなら（未確認 (v)）、保存していない Scene の編集でも同じことが起きる。その時は §6 に書き、見直しの条件 (1) の読み方を owner に返す。

## 3. 責務マップ

path は `unity/Assets/` からの相対。行数は base `d176033a` の `wc -l`。

| file | 責務（変更後） | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 現在 → 予想 |
|---|---|---|---|---|---|---|---|
| `OneStarMaker/Scripts/Editor/Build/Content/BuildContentCoordinator.cs` | build の順序と成功の確定。成功を確定した後に、再利用の記録の更新を別に試み、失敗を結果の欄に写す | I-5 | build 1 回 | 今と同じ | `Build` の署名は変えない | fake の adapter の単体 test | 258 → 約 275 |
| `…/Content/ContentOverlayReuse.cs` | 再利用の記録の読み書き。置き換えを、既存の記録が読めなくなる時点を作らない形にし、別名の file を残さない | I-5 | 成果物の root の下の file | 今と同じ | internal のまま | 単体 test | 148 → 約 165 |
| `…/Content/BuildContentReport.cs` | `BuildContentResult` に、記録の失敗の理由の欄（null なら失敗なし）を足す | S-7 | — | — | public の読み取り専用の欄 1 つ | 単体 test | 402 → 約 410 |
| `OneStarMaker/Scripts/Runtime/Bootstrap/ContentOverlayStartup.cs`（新規、名前は B が決めてよい） | 起動の config と起動の状態（Editor の Play か、mode、検証済みの base か、Player の構成か）から、上書きなし・上書きつきの登録・失敗の理由のどれかを返す。Unity を呼ばない。key の名前と log の marker の定数の正本を持つ | AC2 | 起動 1 回。状態を持たない | `AppConfig` の読みだけ | internal | Unity を使わない単体 test | 0 → 約 75 |
| `OneStarMaker/Scripts/Editor/` の下の新規 file 1 つ（asmdef `OneStarMaker.Editor`。名前と folder は B が決めてよい） | 上の定数を public の定数で公開し直す。logic を持たない | AC2 (i)。SampleGame と別の host が文字列を複写しないため | — | Runtime（今ある `InternalsVisibleTo`） | public の定数 | 定数が Runtime の値と等しいことの単体 test | 0 → 約 20 |
| `OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs` | 上の結果で登録の口を呼び分け、1 行の log を出す。判断を持たない | AC2 | session は今と同じく initializer が持ち、`AssetManagement` に install する | `ContentDirectorySession`（今と同じ assembly） | 変えない | 実 Editor（AC5） | 1305 → 約 1335 |
| `SampleGame/DependOnAll/Editor/Build/PlayEntryOverlay.cs`（新規） | 入口の上書きの段の規則。port が返す計画の要約（状態、上書きの root、作り直せない root と理由、他の供給元、当たらなかった編集）から、再利用か build かを決めて port を呼び、結果の要約（`NotRequired` / `Reused` / `Built` / `UnbuildableOnly`、作り直せない root の 3 つの組、4 つの所要、記録の失敗、置く key）を返す。計画の object（`ContentOverlayPlan`）と build は port の向こうに置き、この file は SampleGame 側の小さな要約の型だけを扱う | S-1〜S-3、S-5〜S-7、I-1、I-3 | tick 1 回。状態を持たない | port（計画、認めて再利用、認めて build、時計） | internal | port を fake にした単体 test（Framework の計画の object を作らない） | 0 → 約 180 |
| `…/PlayEntryTransaction.cs` | 順序の状態機械。新しい経路の時だけ、未保存の asset の列挙と確認（と保存）→ 複製 → import の反映 → 上書きの段を、ledger の前に呼ぶ。判定の出力の値に上書きの段の key を足して `ApplyForOnePlay` に渡し、上書きの要約を `PublishPlanned` に渡す（合わせるのはここだけ） | AC4 (a)(g) | 今と同じ | port | internal | `PlayEntryTransactionTests` | 170 → 約 205 |
| `…/PlayEntryDecision.cs` | 判定表。経路の値と rule を 1 つずつ足し、明示設定の key に上書きの key 2 つを足す。上書きの中身は知らず、上書きのための一般の拡張点を持たない。候補の型 `PlayEntryCandidate`（この file にある）が、検証した install の結果（`ContentInstallResult`）を持つ | AC3 | 純粋 | 今と同じ | internal | `PlayEntryDecisionTests` | 316 → 約 355 |
| `…/PlayEntryInterceptor.cs` | Unity の callback と port の実物の配線。未保存の asset の列挙、確認（判定用の 1 回だけの応答の先置きを先に見る）、保存の port の実物を足す | 新しい port | 今と同じ | — | — | — | 230 → 約 265 |
| `…/PlayEntryCandidates.cs` | 検証した install の結果を捨てずに候補へ渡す | 計画の入力 | 今と同じ | 今と同じ | internal | `PlayEntryCandidatesTests`（test は `ContentInstallResult` を今ある `InternalsVisibleTo` で作る） | 102 → 約 108 |
| `…/PlayEntryRecord.cs` | policy に上書きの計画の要約、record に起動の上書きの登録の観測と、計画との突き合わせを足す。Console の 1 行に上書きの欄を足す。log の marker は Framework の Editor の public の定数から取る | S-6、I-1、I-3 | `SessionState` | — | internal | `PlayEntryRecordTests` | 307 → 約 380 |
| `…/PlayEntryWindow.cs` | 上書きの欄（全件）の表示 | S-6、I-3 | 表示 | record | — | なし（目視は判定に使わない） | 184 → 約 215 |
| `…/PurposeLaunchSettings.cs` | 新しい経路が置く値と、衝突の検査が見る key の一覧 | AC3 (d)(e) | 純粋 | — | internal | `PurposeLaunchSettingsTests` | 125 → 約 145 |
| `…/SampleGameContentBuild.cs` | 宣言と install した base から、欠けを許す materialize → 選択 → 合成 → 計画を呼ぶ host の関数を足す。計画の object を持ち、上書きの段の port の実物（計画の要約、認めて再利用、認めて build）を返す。今の build の口は変えない | 計画の呼び手 | tick 1 回 | 計画と build の public の口 | internal | 実 Editor（AC5） | 148 → 約 215 |
| `…/SampleGameBootstrapContentComposer.cs` | 合成が欠けの記録を引き継ぐ | 計画の前提 | 純粋 | — | — | 単体 test（欠けの記録が残る。欠けの無い入力では今と同じ結果） | 104 → 約 108 |

- 行数の警報: `AbstractApplicationInitializer.cs` は今も 500 行を超える。足すのは呼び分けと log の約 30 行で、判断は新しい file に置く。分割は [RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md](RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md) が所有し、本書では分けない。他の file は 500 行未満で、増加は 50% 未満である。新規の `PlayEntryOverlay.cs` は 1 つの変更理由（上書きの段の規則）を持つ。
- 責務の数の警報: `PlayEntryRecord.cs` は今も、`SessionState` への保存、起動の log の観測、Console の 1 行の書式の 3 つを持ち、本書は上書きの欄をそれぞれに足す。分けない。3 つは同じ record の DTO の欄が増えた時に一緒に変わり（今の snapshot の欄と同じ）、上書きの欄だけを別の file に出すと、1 つの record の読み書きが 2 か所に割れる。380 行を大きく超える時は、B が観測の解析を分ける案を Phase A へ返す。
- 新しい責務の配置の理由: 起動の設定の判断を initializer の外の純粋な型に置くのは、`EditorPlaySnapshotIntake.PlanStartup` と同じ形で、Unity なしで全ての分岐を test できるからである。上書きの段を `PlayEntryTransaction` と分けるのは、順序（状態機械）と、計画の状態から結果を決める規則が別々に変わるからである。上書きの段を Framework に置かないのは §2.4 P7。
- 製品 code の増分の見込み: 約 680 行（Framework 約 160、SampleGame 約 520）。test は別に約 780 行。

## 4. 実装計画

- 順序:
  1. **未確認 (i)〜(v)・(vii) と観測の経路 (vi) の確認**（開いている Editor の `eval`。Unity の test と batch の build は走らせない）。(i) Dirty を含む Scene を開いたまま、tick から Content Directory の build を 1 回起こし、前後で開いている Scene の path・Dirty・内容（root の数と、Dirty にした値）を比べる。(ii) tick の中で `Refresh()` を呼んだ直後の `isCompiling`・`isUpdating` と、同じ tick で build に進めるか。(iii) Reload Domain 有効で、directory の Play → 停止 → Play に入った直後の native の登録の一覧（13 章の実測と同じ読み方）。(iv) 複製の置き場に Scene がある状態の build の preflight の閉包と、複製の file の hash。(v) Dirty の Scene を開いたまま複製を準備し（今の経路 Built + open scenes の準備と同じ呼び出し）、前後の `git status --porcelain` で、複製の準備が書いた source を特定する（Scene の保存の hook を含む）。(vii) 未保存の asset の列挙の所要を測り、計画の所要と並べて §6 に書く。Play のたびに使えない重さなら、owner に返す（§2.4 P5-c）。(vi) 今ある経路の directory の Play の中で、`eval` から `SceneDirector` の instance に届き、`AddScene` と `GetContentLoadRecord` を呼べるか（AC5 の V1・V5 の観測の経路）。届かなければ、届く口を足さずに Phase A へ返す。結果を §6 に書き、§2.6 の NO-GO (i)(ii) に当たれば実装を積まずに owner に返す。順序の入れ替えで済むもの（例: 保存と複製の順）は B 適応として §6 に書く。
  2. 再利用の記録の修正と test（AC1）。
  3. 起動の設定の型と定数、Framework の Editor の public の定数、test、起動の呼び分け（AC2）。
  4. SampleGame の host の関数と合成の引き継ぎ。
  5. 判定表・置く値・候補（AC3）。
  6. 上書きの段・順序・record・詳細窓（AC4）。
  7. Editor での compile の確認。
- Phase B から Phase A へ差し戻す条件: §2.6 の NO-GO (i)〜(iii)。§3 に無い file の変更が要る。計画の public の口（18 章）だけでは入口が書けない。製品 code の増分が見込みを大きく超える（§2.5）。
- 対象外を維持する方法: AC6 の `git diff --stat`。計画の中身、`Runtime/` の解決・session・gate、今ある経路の判定の期待値に差分を作らない。

## 5. テストとレビュー計画

- 単体テスト: AC1〜AC4（Unity の build と Play を伴わない EditMode）。
- 差し戻し中の起点 `-Filter`（C が根拠付きで変更可。受け入れ条件の追加ではない）: `ContentOverlayBuildAndReuseTests`、起動の設定の新しい test class、`PlayEntryDecisionTests`、`PlayEntryTransactionTests`、上書きの段の新しい test class、`PlayEntryCandidatesTests`、`PlayEntryRecordTests`、`PurposeLaunchSettingsTests`、`ContentDeliveryPlayBridgeTests`、合成の test。
- 判定必須テスト（GO 候補 head）: 上の起点 filter、`ContentOverlayPlannerTests`、`ContentOverlaySessionTests`、`OverlayFromLocalEditsIntegrationTests`（AC8 の回を含む）、`OverlayRegistrationIntegrationTests`、`EditorSnapshotCandidateIntegrationTests`、`EditorPlaySnapshotStartupPlanTests`、最終の全 EditMode 回帰（空 filter。合否は「XML の failed 0 と root result `Passed`」で読む。`-nographics` の VFX の Inconclusive で `step.json` が `failed` になるため）、AC5 と AC7 の実 Editor（下の検証経路）。
- 全 EditMode 回帰の適用除外: なし。Player の build は判定必須にしない。Player に入る変更は、起動の設定を読む型（Player の構成では key があれば失敗、無ければ今と同じ）だけで、代替証拠は AC2 の (a)(f) と AC6 である。Player の実機の非退行は S4 が持つ。
- 統合・Unity テスト: build を伴う既存の実機の test は、他の Unity が同時に動くと失敗した既知の事例がある（program §8）ので、判定 C は他の Unity を止めて走らせる。batch の後の Unity の終了 stall は既知（`tools/Harness/README.md`）で、XML の完成で判定する。coordinator の staging の `Directory.Move` の access denied の既知の不安定に当たった時は、同じ head で走らせ直してよく、その事実を §7 に記録する。
- 操作・実行時・目視条件の検証経路（AC5・AC7 の実 Editor）:
  - 担当と環境: 判定 C の実行担当。候補 head の checkout の Editor（`tools/unity-editor.cmd status` が `ready`）。他の Unity を止める。操作は `tools/unity-editor.cmd eval <UTF-8 Base64>`。build 中と Play の移行中の `eval` は timeout して Console に Error を残すので、その行を App の Error と区別して読む（18 章「観測の注意」）。
  - 初期状態: `git status --porcelain` が空で、Dirty の asset が無い。RunPurpose は `spring-full`。検証 Play（`Tools/OSM/Content/Verify Play/spring-full` と同じ段）で、候補 head の checkout から base を build・publish・install する。開く Scene は、V0〜V5・V7 が `Title`（保存済み）、V6 が空の Scene。V4 は V4a と V4b の 2 回である。経路は V0 が Built + open scenes、V1c・V1〜V5・V7 が Built + local edits、V6 は経路に依らない（明示設定）。`EditorSettings.enterPlayModeOptions` は V7 だけ Reload Domain 有効。
  - Play の始め方と止め方: `EditorApplication.EnterPlaymode()` と `ExitPlaymode()`（入口の `ExitingEditMode` を通る。Toolbar の Play と同じ判定に入る）。
  - 対象の Scene の選び方と要求: V1 の対象は、宣言が選び、開いておらず、`Title` を開いた Play で `SceneDirector` が要求する Scene（開いている Scene の親、または起動の後の最初の遷移で載る Scene）から選ぶ。自動で要求されない Scene を使う時は、Play の中で `eval` から `SceneDirector.AddScene` を呼んで要求する。どちらで要求したかを記録する。この経路は初回 Scene を置かないので、開いていない Scene は要求しない限り載らない。
  - 編集の作り方: V1 は、対象の Scene の閉包に入り、`Title` の閉包に入らない `Assets/` の material を 1 つ選び（base の build の preflight の `rootClosures` から選ぶ）、色の property を `eval` で変える。V1c と V1 は保存せず、確認への応答を `eval` で先に置いてから Play する（V1c は「やめる」、V1 は「保存して続ける」）。material は、開いている `Title` が参照しないものを使う（複製の保存は Scene が参照しない Dirty の asset も書くので、その場合を通す）。V5 と V7 の編集は `AssetDatabase.SaveAssets()` で保存してから Play する（利用者の保存に当たる。確認は出ない）。V3 は Play を止めた後に `git checkout -- <path>` と `AssetDatabase.Refresh()` で戻す。V4a は `Title` の GameObject の名前を 1 つ変えて Scene を Dirty にし、保存しない。V4b はその Scene を保存する。V4b の後は `Title` の file を `git checkout` で戻し、Scene を開き直す。V5 と V7 の material の値は、V1 と違う値にして保存する（同じ値では V1 の上書きが再利用されうる）。V5 は、同じ material を直接参照する 2 つの Scene（B と C）を `rootClosures` から選び、Play の中で B と C を要求して、それぞれの参照から material の値を読む。準備では、B の `.unity` と `.meta` を project の外の一時 folder へ移して `Refresh()` する。SampleGame の Scene は Addressables の entry なので、退かすと group の asset が書き換わりうる。V5 の後に B を戻して `Refresh()` し、編集した material と、書き換わった Addressables の設定の file を `git checkout` で戻し、`git status --porcelain` が空に戻ることを確かめる。戻した file の一覧を記録する。該当する Scene の組が無い時と、空に戻せない時は、その事実を §7 に書いて owner に返す（fixture で代えるかは Phase A の裁定）。
  - V6 の値: mode は `directory`、installed の pair・build の identity・表現は installed の receipt と V1 の起動の log（`Content resolution: wired=true` の行）から取り、初回 Scene は `Title`、上書きの path は V1 の record の成果物の path、上書きの identity は V1 の identity の末尾を変えた値にする。環境変数は `SAMPLEGAME_` + key（`:` を `__`、大文字）で置き、V6 の後に消す。
  - 観測と合否: 各回で次を保存する。(1) `SessionState` の record の JSON（`OSM.PlayEntry.Record.v1`）。上書きの状態、root の数、作り直せない root（組ごと）、4 つの所要、置いた key、起動の観測との突き合わせ。(2) Editor の log のうち `[AppInit]` の行（上書きつきの登録の行、`Content resolution` の行）。(3) Play の中で `eval` で読んだ、編集した material の property の値（HEAD の値と並べる）と、対象の Scene の `SceneDirector.GetContentLoadRecord` の供給元（`ContentOverlay`・base の directory・`EditorSnapshot` のどれか）。(4) 成果物の root の `reports/` の directory の一覧（回の前後）。(5) 回の前後の開いている Scene の path と `isDirty`、`git status --porcelain`。合否は AC5・AC7 の各行の文で読む。値と構造の検査だけで判断し、画像は使わない。
  - 対象版と受け渡し: 候補 head の SHA を各回の記録に書く。保存した記録は判定の evidence bundle に入れ、C' には同じ生の記録を blind bundle で渡す。
  - 動作確認（Phase B の後）: V1c・V1・V2・V3 を各 1 回。入口が起こす上書きの build は操作そのものなので含める。base は installed cache に既にある検証済みの revision を使い、無ければ `未実施` として判定 C が用意する。
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点: **未確認。初回確認は Phase B の最初の作業（§4 の (1)）と Phase C。** 対象は §1 の未確認 (i)〜(v)・(vii)、V1 の「開いていない Scene を Play の中で要求する」操作と「Play の中で `eval` から `SceneDirector` に届いて record を読む」操作、V5 の「実 project の Scene を一時的に退かして戻す」操作（Addressables の設定が元に戻るかを含む）である。理由は、Unity の build を伴う試行を Phase A で行わないため。`eval` での Editor の操作、検証 Play の段、経路 Built + open scenes の実 Editor の Play と record の読みは、S0-d と S3-3 の判定 C で成立した経路である。不成立の時は §2.6 の NO-GO で owner に返す。V5 だけが作れない時は、合成 fixture の実機の test（`OverlayFromLocalEditsIntegrationTests` と同じ部分 checkout の fixture を、起動の設定から通す形）で代えるかを Phase A で決め直す。
- 人間の判断が必要な条件と合意した担当・証拠の受け入れ方: なし。
- 高コスト証拠: なし（Player の build も不可逆な操作も無い。検証 Play の base の build は判定の準備で、やり直せる）。
- 判定 C の照合 script の方式: なし（test の XML と、保存した記録を判定 C の担当が読む）。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC6 の `git diff --stat`。
- A0/A1 主担当・モデル・ベンダー: AI（Claude Code、Anthropic）。
- C' 用に予約した担当・モデル・ベンダー: A0〜A2 と統合は Anthropic 系だけを使う。外部計画レビューの系列は owner が選ぶ。C' は起動の時点で、Phase A〜C に関与していない系列から進行役が選び、残っていなければ `独立性制約あり` と記録する。
- 独立性の強化条件を満たせない場合の理由: A3 で記録する。

### 5.1 A2 の独立レビューと A3 の採否

- A2（2026-11-22、同一 PC）。入力版は固定 base `d176033a` の clean な detached checkout と、HANDOFF copy（sha256 `5a1e5a51e2408cfdd3a62ea3d457edefa4fc298e0e5c937d4ed368012d7a2eee`。A1 初稿）。4 件とも Claude Code の subagent（Anthropic 系。主担当と同じ系列で、主担当とは別のモデル）。互いの指摘は渡していない。
  - アーキテクチャゲート: HEAD・clean・hash の照合は一致。結論は blocker 0、major 1、minor 6。§1 と §3 の path・行数・行番号は、下の 3 件を除いて全て一致した。SampleGame の入口が public の口だけで書けること、AC6 が許す `Runtime/` の差分で足りること、状態の所有者と配置の一致を code で裏付けた。
  - 不変条件の反証と AC の読み違い: 照合は一致。I-1・I-3・I-5 は破れず、I-2 は破れ（利用者の層に上書きの key が残る順序）、I-4 は破れうる（Scene の保存の hook。要確認）。AC の読み違いは major 3、minor 4。検証経路は blocker 1（V6）、major 3（V1・V4・V5）、minor 1。
  - 目的レビュー: §1 の目的と利用者の本文だけを渡した別の担当（道具を使わない）。入力の sha256 `c915c5a5d69b2a7678e2a19e20192b67f6023ca4a672184eaba79dfbcb00b22a`。
  - A0 だけからの代替構成（risk `high` の追加分）: 目的レビューとは別の担当に、A0 の packet（sha256 `e4588879fc7bcea7735c507994778ed665cfb64462ed364cd21beb5dd8236d54`。目的、利用者、対象外、現況、問い、誤って YES を出す道、実装制約）と固定 base を渡した。初稿は読ませていない。目的レビューのやり直しに使える未関与の担当は残してある。
- A3 の分類（主担当の分類。owner は凍結の時に、覆す行だけを指す。目的・利用者の 2 欄は変えていないので、目的レビューのやり直しは今は無い）:

  | 出所 | 指摘 | 分類 | 反映 |
  |---|---|---|---|
  | 反証 I-2、反証 B2、代替構成 8-3 | Editor の環境変数に上書きの key が残っていると、今ある経路の入口が介入して自分の値を置き、起動が意図しない上書きを重ねる。確かめる AC も無い | 採用 | 上書きの key を明示設定に含め、どの経路でも素通りにする（I-2、AC3 の (e)(f)、§2.8 U6）。launcher は変えず、起動が fail-closed に読む |
  | 反証 I-4、反証 C3 | 複製の保存が Scene の保存の hook を走らせるなら、`SceneResource` が source に書かれ、I-4 の許す書き込みの一覧に無い。V4 の保存でも同じ書き換えが起き、期待（build 0 回）と食い違う | 採用 | I-4 の文と経路 (5)、未確認 (v)、§4 の (1)、§2.9 の弱点に書いた。V4 は保存しない編集に改めた |
  | 反証 B1 | AC1 の (d) は、消してから移す今の実装に別名の後始末を足しただけでも通る | 採用 | 「既存の記録を消す段を持たない」を構造レビューの条件にした（AC1 の (d)） |
  | 反証 B3 | 保存が判定の通過より前でも AC4 の (a) を通る | 採用 | AC4 の (a) と I-4 の経路 (6) |
  | 反証 B4、反証 I-1、代替構成 8-5 | build の adapter の中の `Refresh` の後にも compile が始まりうる | 採用 | I-1 の経路 (9)、AC4 の (g)（ledger の直前にも見る） |
  | 反証 B5、代替構成 3 | record が起動の観測と突き合わされないことを不合格にする AC が無い | 採用 | I-1 の経路 (10)、AC4 の (l) |
  | 反証 B6 | 解除の再試行の後に base だけにならないことの AC が無い | 採用（既存の test を判定必須にする） | `ContentOverlaySessionTests` を判定必須に足した（I-1 の経路 (8)、§5） |
  | 反証 B7、反証 C5 | 「Dirty の asset の保存の他の差分が無い」は反証できない | 採用 | 各回の初期条件と、許す差分の一覧を固定した（AC5、§5） |
  | 反証 C1 | V6 は、`Title` を開いていると判定が先に拒否し、base の build の成果物は identity の検査で先に止まる | 採用 | 空の Scene、V1 の成果物と別の identity に改めた。失敗の code ごとの照合は S3-2 の test が持つと書いた（AC5 の V6、§5） |
  | 反証 C2 | 実 project の Scene を退かすと Addressables の entry が消え、戻しても `git status` が空に戻らない見込み | 採用 | 戻す手順に Addressables の設定の `git checkout` と記録を足し、戻せない時は owner に返す（§5） |
  | 反証 C4 | 新しい経路は初回 Scene を置かないので、開いていない Scene は自動では載らない | 採用 | 対象の Scene の選び方と要求の手順を書いた（§5） |
  | ゲート 1 | config の key と起動の log の marker の正本の置き場が無く、Runtime と SampleGame で文字列が複写になる | 採用 | 正本は Runtime の internal の定数、Framework の Editor が public の定数で公開し直す（§3 に 1 行、AC2 の (i)、§2.4 P2-a） |
  | ゲート 2 | 判定の出力の値に上書きの key と要約を合わせる場所が無い | 採用 | `PlayEntryTransaction` が合わせる（§3） |
  | ゲート 3 | 上書きの段の単体 test は、Framework の計画の object を SampleGame の test から作れず重い | 採用 | 計画の object と build を port の向こうに置き、段は SampleGame 側の要約の型だけを扱う（§3） |
  | ゲート 4・5、現況の誤り | 「public の口だけ」は不正確（合成は今ある `InternalsVisibleTo` を使う）。候補の型は `PlayEntryDecision.cs` にある。`CheckEditorState` の利用箇所の記述が誤り | 採用 | §2.7、§3、§1 を直した |
  | ゲート 6 | `PlayEntryRecord` の 3 責務の警報を判断していない | 採用 | 非分割の理由を書いた（§3） |
  | ゲート 7 | 保存した経路の未知の値の読みは code で決まる（`Source`） | 採用 | §1 の現況と I-2 の経路 (6) |
  | 代替構成 2 | 複製の準備を build の後に置く（複製の置き場が build に効くかを構造で避ける） | 不採用 | 複製の準備は source を書きうる（Dirty の asset、保存の hook）ので、計画より前に終える。後に置くと、その書き込みがどの hash の検査にも掛からない。置き場の影響は §4 の (1) で測る（§2.4 P4） |
  | 代替構成 3 | key を `content:` の名前空間に置く | 不採用 | Editor の Play だけの設定で、`editor:playSnapshotExpected` の前例に揃える。Player の構成では key の存在を失敗にする（§2.4 P2） |
  | 代替構成 3 | 起動の検査を initializer の中に書く | 不採用 | 分岐を Unity なしで test するため、純粋な型に置く（ゲートも同じ形を支持した。§3） |
  | 代替構成 1 | 開いている保存済みの Scene が無い時は、初回 Scene の値で上書きだけを載せる | 保留（後続） | 起動の組み合わせが 1 つ増える。S5 へ送った（§1 の対象外、§2.3） |
  | 代替構成 7 | 記録の修正と未確認の観測を先のスライスに分ける | 不採用 | §2.5 |
  | 代替構成 6 (v) | 配線の前に、実 project の部分 checkout で計画だけを呼んで作り直せない root を数える | 不採用 | Phase B は base の build と install を用意しない。同じ計画は S3-4a の実機の test と AC5 の V5 が通す。件数は SampleGame の構成で決まり、判断に使わない（§2.9） |
  | 代替構成 8-1 | 保存していない asset の編集を計画が見ずに base の版が出る道 | 採用 | 誤って YES を出す道に足し、V1 の編集を保存しない形にした |
  | 代替構成 8-4 | Player の構成での拒否を Editor の分岐だけで確かめる道 | 採用 | 誤って YES を出す道に足した。AC2 の (f)(h) が純粋な型の test で持つ |
  | 目的レビュー | 「上書きを作れない」と一部の root が作り直せない時の境界、「編集」の比べる相手、「示す」の形、明示設定と経路の選択の優先が、原文だけでは 2 通りに読める | 採用（本文で読みを固定。2 欄の文言は変えない） | §2.1 の用語 |
  | 目的レビュー | 別の host が渡した directory は、自動の判定や作り直しの対象にしない | 採用 | UC-2 |
  | 目的レビュー | 作り直せない Scene の確認の dialog、強制 rebuild の切替、編集を選ぶ UI を作らない | 一致（初稿のまま） | §2.4 P3、UC-1 |

- 影響する A2 観点の再レビュー: 行わない。採った指摘は、A2 の担当が示した形（明示設定への組み入れ、定数の置き場、port の境界、検証経路の手順）をそのまま入れたもので、新しい責務・依存・公開面は Framework の Editor の定数の file 1 つだけである。外部計画レビューが統合案の全体を読む。
- A3 統合担当: AI（Claude Code、Anthropic）。
- 外部計画レビュー 1 巡目（2026-11-22、PR #203 の owner のアカウントのコメント 2 件。対象 head `bd64ccf6`。レビューの担当の系列は、本書の担当からは確かめていない）。下は主担当の採否の案で、owner は 2026-11-22 に、裁定を求められた 2 点（Major 1 と Major 3 の案。§2.8 の U6 と U2）を承認し、修正の push を指示した。指摘の根拠は 3 件とも base の code で確かめた（判定表の行の順は `PlayEntryDecision.cs` 163〜190 行目、`NotInBase` の定義は `ContentOverlayPlan.cs` 81〜82 行目）。

  | 指摘 | 案 | 反映 |
  |---|---|---|
  | Major 1: AC3 の (e) の「明示設定なら 4 経路とも素通り」は、今の判定の順（再入、directory と再利用しうる Scene の拒否、1 回の ledger が明示設定より前）と衝突する。行を繰り上げると安全の拒否を迂回し、今の順のままでは (e) に不合格 | 採用。裁定の案は「明示設定でも Scene の混在の安全の拒否を優先する」（行の順を変えない） | I-2、§2.1 の用語、AC3 の (e-1)〜(e-4)、§2.8 U6 |
  | Major 2: 「各回 `git status` が空」は V1 → V2 → V3 と両立しない（V1 の保存が V2 と V3 の開始に残る） | 採用 | AC5 の回ごとの表（基準、開始と終了の時にあってよい差分、復元。V7 の Enter Play Mode の設定を戻すことを含む） |
  | Major 3: 「作り直せない root は base から載る」は `NotInBase` には成り立たない。「警告して続ける」と「その root を要求しても成り立つ」は別の主張 | 採用。裁定の案は「止めずに続け、要求すると失敗する root を分けて示す」 | I-3（3 つの組）、S-5、AC4 の (m)、§2.4 P3-a、§2.8 U2、§2.9、§2.3（入口で止めるかと、記録の無い base の見分けは S4） |
  | 補足: Play に入れることと、編集が全ての root に反映されて確かめられることを分ける。V5 で、base に残った Scene の依存が意図して古い版であることと、開発者がそれを識別できることを観測する | 採用 | §2.9 の結論、S-5、AC5 の V5、誤って YES を出す道 |
  | 追加 1: 全量の判定を基準として保ち、確かめていない `NotRequired`・`Reused` や自動の縮退を足さない。軽くするためだけに本書の責務を増やさない | 採用（初稿と同じ向き。文にした） | I-1 の経路 (11)、AC4 の (n)、§2.7 |
  | 追加 2: AC5 の実 Editor の証拠に、検査の費用の内訳（所要、file の件数と総 bytes、root の数）を残す。合否にしない | 採用。編集の検出・分類・鍵は計画の 1 回の呼び出しなので、所要は 4 つ（選択の準備、計画、再利用の確かめ、build）に分け、計画の中は分けない。件数と bytes は判定の担当が計画の契約から数える（計画の code を変えないという指摘の条件のため） | S-6、AC4 の (o)、AC5 の「検査の費用の記録」、§3 |
  | 追加 3: S5 の第一の候補は「変わったものだけを確かめる」道。通知だけを証拠にしない、失効の条件、全量との差分の test | 採用（後続の入力） | §2.3 |
  | 追加 4: 全量をしない Play は、利用者が明示的に今ある経路を選ぶ道として残す。新しい経路の中で黙って省かない。完全性を保証しない速い Play は保証の範囲を区別して設計する | 採用（後続の入力） | §2.3、§2.9 |
  | Phase D で、省ける処理と今は省けない処理を S5 に渡す | 採用（harvest に置く。判定の結果に依る文書なので受け入れ条件にしない） | §0 の harvest to、§2.3 |

  push の前に、主担当が Phase C・C' の読み手の立場で受け入れ境界を読み直し、次を直した（指摘の採否ではなく、読み違いと実行できない手順の手当て）: S-2 の対象（保存が他の asset を書き換えた時を除く）、AC2 の (j)（mode の無い設定）、AC3 の (e-4) の適用範囲、AC4 の (l) の観測の区間と素通りの Play の扱い、AC5 の V4 を V4a・V4b に分けたこと（他の供給元の key の配線を実 Editor で 1 回通す）、V5・V7 の編集の値を V1 と変えること（同じ値では再利用になる）、record の読み方（`SceneDirector.GetContentLoadRecord`。log の行ではない）とその経路の確認 (vi)、「複製の書き込み」の許す範囲を B の列挙ではなく判定の担当の V0 の観測で決めること、AC6 の `Runtime/` の外の public の面の列挙。

  目的・利用者の 2 欄は変えていないので、目的レビューのやり直しは無い。影響する A2 観点の再レビュー（1 巡目）: 行わない。変えたのは、判定の順に合わせた AC の読み、表示の組分け、検証経路の手順、record の所要の欄で、責務・依存・公開面は増えていない（AC4 の (m)(o) は `PlayEntryOverlay` と `PlayEntryRecord` の中に収まる）。

- 外部計画レビュー 2 巡目と owner の裁定（2026-11-22、PR #203 の owner のアカウントのコメント 2 件。対象 head `e308e24c`）。

  | 指摘・裁定 | 扱い | 反映 |
  |---|---|---|
  | 再レビュー: 1 巡目の Major 3 件と検査の費用は、計画の記述と判定の条件としては解消した | 記録 | — |
  | 再レビュー: S-5 の `NotInBase` の「要求すると失敗し、別の物が出ない」は、今の必須の証拠では閉じていない。分類の test と要求の test を別物として扱う | 採用 | AC8（合成 fixture の実機の test に 1 回。既存の test が同じ条件を持つなら、名前と assertion を §6 に書く）、GO の条件、§5 の判定必須 |
  | 再レビュー Minor: 「計画が読む file の件数と総 bytes」は、重複の無い対象の量で、実際の読み取りの量ではない | 採用 | AC5 の「検査の費用の記録」の名前と注記 |
  | owner の裁定 U1・U3・U5: 現案を承認。U2・U6: 承認済みの案を維持 | 記録 | §2.8 |
  | owner の裁定 U4: 入口の自動の保存は採らない。保存するかは利用者に任せ、強制の確認も出さない。検出できた未保存の asset を知らせ、disk の内容で続ける。未保存の変更を反映したと記録・表示しない。検出できなかったことを保証にしない。V1 を「保存した編集の反映」と「未保存のまま Play（保存せず、通知し、source に書かない）」に分ける | 採用 | §1 の UC、§2.1 の用語と I-4、AC4 の (a)(p)(q)、AC5 の V1u・V1 と working tree の表、§2.4 P4・P5、§2.7、§2.8 U4、§2.9、§3、§4、§5 |
  | 主担当が U4 の反映で見つけた点: 入口が `SaveAssets` を呼ばなくても、複製の保存（`SaveScene(…, saveAsCopy: true)`）が Dirty の asset を全て保存するので、そのままでは U4 の「source に書き込みが起きない」が成り立たない。2026-11-22 に実 Editor で測り（§1）、印を外して複製し戻す方法で避けられることを確かめた | U7 として足した（owner の確認は未了）。新しい経路の複製の準備だけが保存しない。今ある経路は変えない | §1 の実測、I-2 の経路 (7)、I-4、§2.3、§2.4 P5、§2.6 の NO-GO (v)、§2.8 U7、§3 の `PlaySnapshot` の 2 行、§4 の (1) の (vii) |

  owner は 2026-11-22 に、上の U7 の案を見た上で、「実装上で困難があれば、全て保存するかの確認を出すだけでもよい。よくある仕組みである」と答えた。方式 B として §2.4 P5・§2.8 U4・AC4 の (r)・AC5 の V1u に入れ、(vii) が不成立の時に owner へ返す NO-GO は外した。続けて owner は、未保存の asset の検出が asset の全体の走査になるなら割り切ってよい、依存だけを見れば済むなら別である、と述べた。主担当は、通知と確認の判定は依存だけで足りるが、複製の保存に依存の外の Dirty の asset を書かせないには memory の全ての object の列挙（方式 A）が要ることを示した。owner は一度、方式 B では依存だけを調べ、依存の外の未保存の asset が確認なしで保存されることを、確認の文に「全て保存する」と明記する条件で受け入れた。その後、同日に改めた: 方式 B では常に確認を出す。依存だけでは分からないなら、出すべきである。主担当がそれを「新しい経路の Play のたびに確認する」と書いたところ、owner は、確認は build の時のもので、Play のたびに出す想定ではなかったと述べた。主担当は、保存を起こすのは build ではなく Play のたびの Scene の複製の保存であること、build の時だけ確認すると build の無い Play で確認なしの保存が残ることを示し、「未保存の asset が 1 件でもある Play でだけ確認する」を推した。owner はこれを受け入れた。方式 B はこの形で書いてある。owner はあわせて、Editor で built content を使う Play は今の主な使い方ではないので作り込みすぎなくてよいと述べた。主担当が「部分 checkout の開発者にはこの経路が日常になる」と補うと、owner は見立てを示した: 部分 checkout で content の build を試したいのは実機であり、Editor では編集してそのまま Editor で Play する。主担当は、その見立てなら実機に上書きを載せる測定を先にする案を示したが、owner は、Editor で重ねるのは検証が安いから先に行っており、実機は後続のつもりである、と答えた。本書の範囲と順序は変えない（§2.3 に位置づけを書いた）。Editor の体験の作り込みは、この位置づけで読む。

  主担当はこの位置づけを計画の重さに映していなかった（Dirty の印を外して保存を避ける方法を第一に置き、確認を代替に置いていた）ので、owner に、未保存の asset の扱いを確認だけにして、保存を避ける方法を落とすことを確かめた。owner は 2026-11-22 に承認し、計画の全体の見直しを指示した。見直しで変えたもの: 未保存の asset の扱いは確認だけ（§2.4 P5-c、§2.8 U4）。Framework の複製の準備（`PlaySnapshot`）の変更、U7、印を外す方法の追加の測定、それに掛かる受け入れ条件（AC4 の旧 (p)(q)、AC5 の V1u、方式 A・方式 B の呼び分け）を外した。複製の保存に未保存の asset を書かせない方法は、実測の結果と一緒に S5 へ送った（§2.3）。確認の実 Editor の確かめは行わず、単体 test で持つ（AC5）。目的と利用者の 2 欄は、確認が利用者の入力になるので 1 文ずつ改めた（§1。承認と目的レビューのやり直しは凍結の前）。見直して残したもの: 縦断の実証に要る実 Editor の回（V0〜V7）、作り直せない root の組分け、record と起動の観測の突き合わせ、検査の所要の記録、AC8。どれも、上書きの方式が成り立つかを言うのに要る証拠か、黙って誤った状態で進まないための物である。上の表と経緯にある「方式 A」「方式 B」「U7」は、この見直しより前の呼び方である。

  目的・利用者の 2 欄は変えていない（owner が U1 で維持と裁定した）。影響する A2 観点の再レビュー（2 巡目）: 行わない。増えた責務は Framework の Editor の複製の準備の選択 1 つで、依存は同じ asmdef の中（`SceneVolumeRecalculator`）に収まり、`Runtime/` と asmdef の参照は変わらない。次の外部計画レビューが統合案の全体を読む。

- 目的レビューのやり直し（2026-11-22）。目的と利用者の 2 欄を U4 に合わせて改め、owner が文言を承認した後に行った。担当は、目的・利用者以外の A0 の欄も初稿も読んでいない別の担当（Claude Code の subagent、Anthropic 系、道具を使わない）。渡したのは改めた 2 欄の原文だけで、入力の sha256 は `75536ca6f926ee32ae067bd5791b218e1ae2e6149b131f9b87d42c74dc34cb4a`。問いは 1 回目と同じ 2 つ。やり直しはこの 1 回で使い切った（この後に 2 欄を変える時は、owner が文言を裁定して凍結する）。

  | 回答 | 分類 | 反映 |
  |---|---|---|
  | 保証すべきこと 10 件（押すだけで入る、入力しない、build 0 回の 2 つの場合、作り直せない Scene を示して入る、base だけに黙って入らない、保存を確かめる、担当外の source が無くても成り立つ、経路の選択が永続する、Built が変わらない、別の host の明示の設定を尊重する） | 一致 | 不変条件・成立条件・AC が既に持つ |
  | 作らないべきもの（Play のたびの選択の UI、編集の一覧の入力、手動の build の手順、作り直せない Scene の確認、base だけへの自動の切り替え、確認なしの保存、保存する asset を選ぶ UI、「今後確認しない」、毎回の build、source を取りに行く機構、Built と起動の設定の変更、上書きの履歴や掃除の UI） | 一致 | 計画はどれも作らない（掃除は S5） |
  | 読みが割れる語: 「前回と同じ」の「前回」、「base に重ねられない」の条件、「保存していない asset」が開いている Scene を含むか | 採用（本文で読みを固定。2 欄の文言は変えない） | §2.1 の用語に 3 つ足した |
  | 読みが割れる語: 「編集が無い時」の比べる相手、作り直せない Scene の Play での中身、「示す」の手段、保存の確認を断った時、「明示した設定」の範囲、「上書きを作れない時」の範囲 | 一致（本文が既に読みを固定している） | §2.1 の用語、I-3、S-5、§2.4 P5-c |

- 外部計画レビュー 3 巡目（2026-11-22、PR #203 の owner のアカウントのコメント。対象 head `c94dedb5`）。結論は「前回の指摘は計画上は概ね解消。方針は妥当」。下は主担当の採否の案である。

  | 指摘 | 案 | 反映 |
  |---|---|---|
  | Major: U4 の保存の確認を fake の port の単体 test だけで確かめると、実際の列挙、複製の保存の副作用、「やめる」の後に何も書かれないことを、新しい経路で一度も通さない。開いている Scene と無関係な Dirty の asset で、「やめる」（できれば「保存して続ける」も）を実 Editor で 1 回通す | 採用。主担当は 1 つ前の見直しで、modal に `eval` が答えられないことを理由に実 Editor の確かめを外していた。外しすぎだった。V1 の前に V1c（「やめる」）を足し、V1 を「保存して続ける」で入る形にした（回は 1 つ増えるだけで、base の build は増えない）。確認への応答は、1 回だけの先置きの口で渡す | AC5 の V1c・V1 と working tree の表、AC4 の (p)、§3、§5、誤って YES を出す道 |
  | Minor: 「未保存の asset がある Play では確認が出る」は無条件に読める。「検出できた未保存の asset が 1 件以上」に揃え、0 件は無いことの証明ではないと書く | 採用。目的と利用者の 2 欄の文言は変えず（目的レビューのやり直しは使い切った）、用語で読みを固定した | §2.1 の用語、§1 の UC、§2.4 P5-c、§2.8 U4、§2.9 |

  影響する A2 観点の再レビュー: 行わない。足したのは実 Editor の回 1 つと、確認の port が応答の先置きを見る口で、責務・依存・公開面は増えていない。

## 6. Phase B 実装結果

未着手。

### 動作確認

未実施。

## 7. Phase C

未着手。

## 8. Phase C'

未着手。

## 9. Phase D

未着手。
