# Content Directory の増分 build の実測（スパイク）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（スパイク。[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S2 の前に置く。§1「方向」）
- status: `A`（revision 0 の A1 初稿。**A2 入力版、未凍結**。owner の指示で、A3 の凍結前に外部レビューへ出すためにこの版を commit・push する）
- branch: `claude/content-incremental-build-spike`（PR の base は `develop`）
- implementation base commit: `0b3e1cea`（`develop` の head、2026-11-12）
- implementation head commit: `未固定`
- risk: `normal`（製品 code を変えない。設計判断を含まない）
- owner: root（依頼者）
- created: 2026-11-12
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: [CONTENT_DIRECTORY_BUILD_FACTS.md](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)（「確かめていないこと」の増分 build cache の行を実測に置き換える）、program §5.3・§8（結論と S2 の行）
- Phase A snapshot の取得案内・状態: 未生成（機械向けの期待値・照合 script は無い）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割: Phase A の A2（owner 経由の外部レビュー）と A3（owner の裁定と凍結）。
  - 次の作業: 外部レビューの指摘を §5 の採否に足し、§2 の未決事項 U1〜U4 の裁定を反映して、A3 凍結版として commit する。
  - 必要な権限: なし（文書のみ）。
  - 停止規則: 凍結前に §4 の測定の build を実行しない。目的・利用者の 2 欄を変えたら目的レビューをやり直す。
  - 引き渡さないもの: なし。

## 1. 目的と対象外

- 目的: 同じ `Library` で Content Directory の build を繰り返した時、変わっていない content が作り直されないか、root の集合（宣言）を切り替えても作り直されないかを実測し、「CI が全体を 1 つの build として作り続ける」形が増分 build に頼れるかを、数値で判断できるようにする。
- 利用者: なし（実行時の操作も開発時の宣言も無い。結論を読むのは、Content の配布単位を設計する OSM の開発者）。
- 方向（このスライスが前提に置く仮の形。**決定ではない。** 2026-11-12 に owner が前提として確認した。外部レビューの対象に含める）:
  - 動機は「全部を build したくない」（[GOALS_AND_STRENGTHS.md](../GOALS_AND_STRENGTHS.md) §1 の 4 番「部分的な作業空間」）。想定は中規模チームの多人数開発で、SampleGame の規模や構成を設計の基準にしない。目標は、メモリの重複を避けることと、多人数の開発を効率化すること。
  - Unity 6.6 の公開文書（2026-11-12 に確認）: Content Directory の build は自己完結で、同じ asset を参照する 2 つの build はそれぞれ複製を持つ。別の build の出力を参照先として渡す入力は無い（`BuildContentDirectoryParameters` の 6 欄、`BuildContentOptions` の 7 値）。重複を避ける案内は「各 asset を 1 つの build からだけ参照し、他の build からは実行時に root 経由で引く」だけである。build は増分で、「前回の build から変わった content だけを作り直す」と書かれている（「前回」が何の単位かは書かれていない）。
  - 実測（[CONTENT_DIRECTORY_BUILD_FACTS.md](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)）: 別々の build の複製を両方登録すると、Scene の直接参照では実体が directory の数だけできる。
  - そこから置いた形: 中規模の project では共通の material・shader・texture を多数の Scene が直接参照するので、直接参照を保ったまま重複を無くせるのは同じ build に入れた時だけである。そこで **CI とリリースは全体を 1 つの build として作り（重複なし）、開発者は取得した build に、自分が触った Scene だけ手元で build して後から登録する**（重複は触った Scene が直接参照する共通 asset の分だけ、その人の PC でだけ生じる。owner はこれを許容と確認した）。
  - この形は、CI の全体 build が毎回まるごとの作り直しにならないことに依存する。本スライスはその 1 点だけを測る。
- 対象外（所有する後続）:
  - 取得した全体 build に手元で build した 1 Scene を混ぜて登録し Play できるか（正しい class が付くか、実体の重複が触った分に収まるか、解除の順で壊れないか）: 本スライスの結論の後に切る次のスライス。
  - 取得した build の source 一覧と手元の file を比べ、手元で build し直す対象を機械で決めること: 同上。
  - 別の PC・別の `Library` の間で cache や bytes が揃うか、CI の agent が複数ある時の cache の持ち方、archive、Player: 後続（program §8 の S2 / S4）。
  - 保留中の S2 の計画（draft の [PR #180](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/180)、未凍結）の扱いと、program §8 の S2 の行（「1 つの合成単位として作った 2 package」）の改訂: 本スライスの結論の後に owner が決める（U4）。
  - 製品 code の変更。
- 現況（base `0b3e1cea`）:
  - 事実の文書の「確かめていないこと」に「増分 build cache の関与（`CleanBuildCache` の build の log に、cache を使わなかったことを示す行が無い）」が残っている。build の所要時間を条件を揃えて比べた記録は無い。
  - SampleGame の Content Directory build は 225〜668 秒の報告がある（[RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md](RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md) §2）。増分が効いた回かどうかは区別されていない。
  - SampleGame の宣言（`spring-full`、`spring-whitebox` 等）は、同じ source から root と表現の選び方を変えた別々の build である（`SampleGameBuildPurposes`）。1 つの workspace で宣言を順に build した時に前の宣言の cache が残るかは分かっていない。残らなければ、CI は宣言ごとに workspace を分ける必要がある。
  - 過去のスパイク（PR #177）は 1 つの `Library` で root の違う 15 build を作り、共通 asset の file が同じ bytes になることを確かめた。時間は判定に使っていない。
- UC ごとの利用者の入力: なし。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 同じ `Library` で build を繰り返した時、変わっていない content の build は、初回に比べて十分に短いか。root の集合を切り替えた後でも短いか。
- 進める最低条件:
  - 不変条件（起きてはいけないこと）:
    - **I-1 測っていない物を「効く」と言わない。** 判定に使う時間は `BuildPipeline.BuildContentDirectory` の呼び出しの所要だけで、Unity の起動・import・fixture の書き換え・出力の hash 計算を含まない。基準の build（番 0）は cache を使っていない。
      - 崩しうる経路: (a) 番 0 の前に同じ fixture の build cache が `Library` に残っている（`CleanBuildCache` で塞ぐ。`CleanBuildCache` が何を消すかは文書に無いので、番 0 の時間が番 1 より十分長いことを AC1 で確かめる）。(b) 番 0 の時間に asset の import が混ざる（生成と import を別の process で先に済ませる）。(c) 番 2 の書き換えが build に届かず、番 1 と同じ build を測る（AC3）。(d) 同じ Unity process の中で続けて build し、process 内の memory の cache を測る（1 build = 1 process）。
      - 探索した境界: 過去のスパイクの harness（PR #177）、Unity の公開文書。未確認: `CleanBuildCache` の範囲。
    - **I-2 範囲の外へ結論を広げない。** 結論は、この PC・Unity 6000.6.0f1・StandaloneWindows64・loose file・合成 fixture・Editor の batch に限る。実 project の絶対時間、別の PC、CI の agent をまたぐ cache は言わない。
      - 崩しうる経路: harvest の文で「CI で効く」と書く（AC5）。
  - 成立条件（起きなければならないこと）:
    - **S-1** §4 の番 0〜5 の全ての build が成功し、番ごとの所要時間と出力の file 一覧（sha256 と bytes）が record に残る。
    - **S-2** 判定定義のどれに当たるかが、record の数値だけから決まる。
- 受け入れ条件:
  - **AC1（I-1）** 番 0 の所要が 60 秒以上である。未満なら無効とし、fixture を重くして番 0 からやり直す（差が雑音に埋もれるため）。
  - **AC2（S-1）** 番 0〜5 の各行が record にあり、build の結果が成功である。
  - **AC3（I-1）** 番 2 の出力に、番 1 の出力に無い sha256 の file が 1 つ以上ある。0 なら無効（書き換えが届いていない）。数と bytes を報告する。
  - **AC4（S-2）** 番 1・2・4・5 の所要を番 0 の所要で割った比を報告する。番 3 は参考値として報告する。
  - **AC5（I-2）** harvest の文は、範囲（I-2）と、言えない事を同じ段落に書く。
- 判定定義（実行の前に固定する。列挙に当たらない結果は未判定として owner に返す）:
  - **GO（増分が効き、宣言を切り替えても効く）:** AC1〜AC3 を満たし、番 1・番 4・番 5 の所要がどれも番 0 の 1 割以下。
  - **CONDITIONAL ACCEPT（増分は効くが、宣言の切り替えで失われる）:** AC1〜AC3 を満たし、番 1 は番 0 の 1 割以下だが、番 4 か番 5 が 1 割を超える。意味は「CI は宣言ごとに workspace（`Library`）を分ければ増分に頼れる」。
  - **NO-GO（増分に頼れない）:** AC1〜AC3 を満たし、番 1 が番 0 の 1 割を超える。§1 の方向を見直す材料として、番ごとの数値を owner に返す。
  - 番 2（共通の texture 1 枚の変更）は判定に使わず、所要と配り直す量を報告する。
- ここでは答えない問いと所有する後続スライス: §1 の対象外。
- スライス分割の判断: 契機なし。問いは 1 つで、独立して失敗しうる未知は増分 cache の挙動だけである。混ぜて Play の実証は別の問いなので、本スライスに入れない。
- 停止規則: 進める最低条件を満たし、判定定義のどれかに当たれば終了する。無効（AC1・AC3）は fixture を直してやり直し、やり直しは 2 回までとする。
- A3 後の例外承認: なし
- 決定記録: なし
- 本文へ転記した実装制約:
  - **harness・fixture・生の record・build の出力を commit しない（2026-11-12、owner）。** 未追跡の file として置き、終わったら消す。過去のスパイクは harness を commit して履歴に残したが、今回は残さない。
  - Unity の実行は `pwsh tools/run-tests.ps1`（EditMode の test を build の入口に使う）。`unity test` / `unity run` は使わない。
  - XML の完成後に Unity が終わらない既知の native 終了 stall は、`tools/Harness/README.md` の停止規則に従い、調査しない。process の自然終了を条件にしない。
- 未決事項（A3 で owner が裁定する）:
  - **U1 harness を commit しない時の Phase B・C・C' の形。** 通常の手順は、実装 commit を固定してレビューする。今回は実装 commit が無い。案: (a) Phase B の成果は「実行した script と record」とし、script と record を Artifact Storage に stage して C の入力にする。C は「測ったと言う物を測っているか」（I-1 の 4 経路）と、数値が判定定義に当たるかだけを見る。C' は置かない（製品 code も設計判断も無い）。(b) 保存もせず、担当の報告だけで閉じる。推奨は (a)。測り直しの手段が script の保存だけになるため。
  - **U2 合否の線の 1 割。** 根拠のある値ではなく、「CI の毎回の build が、全体の作り直しと比べて桁で軽い」を表す仮の線である。別の線が良ければ凍結前に変える。
  - **U3 fixture の重さ。** 初期値は texture 300 枚（1024×1024）、material 300、Scene 100。中規模の project の代表ではなく、時間差が見える最小として選んだ。規模による変化（線形か）を見るために 2 倍の fixture でもう 1 周するかは、結果を見て決める（本スライスの条件にしない）。
  - **U4 §1 の方向そのもの。** 外部レビューに、この方向の見落とし（開発者の手元の重複を許容した事、リリースの更新を「全体 build と file 単位の差分」で行う事、直接参照を root 経由に寄せる案を主にしない事）を問う。

## 3. 責務マップ

追跡する file の変更は、Phase D の harvest（事実の文書と program）だけである。製品 code、asmdef、公開 API の変更は無い。

| 物 | 置き場 | 寿命 |
|---|---|---|
| 測定の harness（EditMode の test 1 class、約 200 行。fixture の生成と番 0〜5 の build） | 測定用の worktree の `unity/Assets/OneStarMaker/Tests/Editor/` の下。未追跡 | 測定の終了まで。commit しない |
| 合成 fixture | 同 worktree の `unity/Assets/` の下の専用 folder。未追跡 | 同上 |
| record と build の出力 | 同 worktree の `TestResults/` の下（git の対象外） | 同上（U1 (a) なら record と script だけ保存） |

## 4. 実装計画

- fixture（合成。SampleGame を使わない）: 1024×1024 の texture 300 枚、それぞれを `_BaseMap` に持つ URP `Lit` の material 300、Scene 100（各 Scene は quad 20 個で、Scene `s` の `k` 番目は material `(3s + k) mod 300` を直接参照する。隣の Scene と material が重なる）。root は製品の `BuildContentRoot` を無改変で使い、X は Scene 0〜59、Y は Scene 40〜99 を `LoadableSceneId` で持つ。
- 順序（1 行 = 1 つの Unity process。同じ `Library`。出力先は毎回新しい directory。`compression` は `Uncompressed`）:

| 番 | 操作 | 記録 |
|---|---|---|
| 準備 | fixture を生成し import を済ませる | 時間は判定に使わない |
| 0 | X を `BuildContentOptions.CleanBuildCache` で build | T0（基準） |
| 1 | 何も変えず X を `None` で build | T1 |
| 2 | texture 1 枚（複数の Scene が参照する物）を書き換えて import し、X を build | T2、番 1 の出力に無い sha256 の file の数と bytes |
| 3 | Y を build（この `Library` で初めて） | T3（参考） |
| 4 | X を build | T4 |
| 5 | Y を build | T5 |

- 所要は `BuildContentDirectory` の呼び出しの前後の stopwatch で測り、`BuildReport.summary.totalTime` を併記する。
- Phase B から Phase A へ差し戻す条件: 番 0 が `CleanBuildCache` を付けても番 1 と同じ程度に短い（cache を消せていない疑い。基準の作り方を変える必要がある）。build が Unity の不具合で成功しない。
- 対象外を維持する方法: 登録・load・Play を harness に入れない。

## 5. テストとレビュー計画

- 単体テスト: なし（製品 code を変えない）。
- 差し戻し中の起点 `-Filter`: harness の class 名（未追跡。凍結時に本文へ書く）。
- 判定必須テスト: §4 の準備と番 0〜5 を、test 1 件ずつ別の `pwsh tools/run-tests.ps1 -Filter <test の完全名>` で順に実行する。
- 全 EditMode 回帰の適用除外: 適用しない。理由は、追跡する code の変更が無いこと。代替証拠は `git status` で追跡 file の変更が harvest の文書だけであること。
- 操作・実行時・目視条件の検証経路: 対象なし（Unity の batch test だけで閉じる）。
- 未知の操作経路の疎通結果: fixture の生成（準備）だけを凍結の前に 1 回実行し、harness が compile し build の入口まで届くことを確かめる（2026-11-12 に実行。結果は A3 で記録する）。測定の build（番 0〜5）は凍結の後。
- 人間の判断が必要な条件: なし。
- 高コスト証拠: なし。
- 判定 C の照合 script の方式: なし（record の数値を判定定義に当てるだけ）。
- 機械検査: `pwsh tools/docs-audit.ps1`（push の前）。
- A0/A1 主担当・モデル・ベンダー: Claude Fable 5.2（Anthropic）。
- A2 独立レビュー: owner 経由の外部レビュー（この commit を入力版とする）。目的レビューとアーキテクチャゲートの扱いは A3 で記録する（配置・依存・所有者・寿命を変える物が無いので、アーキテクチャゲートが見る対象は §3 の「追跡する変更が無い事」だけになる）。
- A3 統合担当・モデル・採否: 未実施。
- C' 用に予約した担当: U1 の裁定による。
- 独立性の強化条件を満たせない場合の理由: 未記入。

## 6. Phase B 実装結果

未実施。

## 7. Phase C

未実施。

## 8. Phase C'

未実施。

## 9. Phase D

未実施。
