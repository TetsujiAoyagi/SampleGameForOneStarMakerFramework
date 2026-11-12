# Content Directory の増分 build の実測（調査記録）

## 0. メタデータ

- type: `research`（事実の測定。合否の判定を持たず、Phase B・C・C' の欄を持つスライスではない。2026-11-12 に owner が、A2 の外部レビューを受けて slice から改めた。§5）
- status: 測定済み（revision 1。結果は §6）。harvest は未実施
- branch: `claude/content-incremental-build-spike`（PR の base は `develop`）
- base commit: `0b3e1cea`（`develop` の head、2026-11-12）
- risk: `low`（追跡する code を変えない。設計判断を含まない）
- owner: root（依頼者）
- created: 2026-11-12
- expires: 2026-12-31、または結果を harvest して削除した時点
- harvest to: [CONTENT_DIRECTORY_BUILD_FACTS.md](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md)（「確かめていないこと」の増分 build cache の行を、測った範囲の数値に置き換える）、[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8（次のスライスの行）
- 次の作業: owner が §6 を読み、harvest する文を決める。

## 1. 目的と範囲

- 目的: 製品の publish と同じ条件で、同じ `Library` のまま Content Directory の build を繰り返した時の所要時間が、初回に対してどれだけになるかを、(1) 何も変えない時、(2) texture を 1 枚変えた時、(3) 別の root の集合（目的）の build を挟んだ時、について数値で残す。
- 使い道: CI の回し方（頻度、目的ごとに workspace を分けるか）を決める材料にする。**方向の承認にも否定にも使わない**（増分が効かなくても、CI の頻度と workspace の分け方が変わるだけである）。
- 合否の判定は置かない（2026-11-12、owner）。答えは §2 の表である。
- 方向（この測定の動機。決定ではない）:
  - 動機は「全部を build したくない」（[GOALS_AND_STRENGTHS.md](../GOALS_AND_STRENGTHS.md) §1 の 4 番）。想定は中規模チームの多人数開発で、SampleGame の規模や構成を設計の基準にしない。目標は、メモリの重複を避けることと、多人数の開発を効率化すること。
  - Unity 6.6 の公開文書（2026-11-12 に確認）: build は自己完結で、同じ asset を参照する 2 つの build はそれぞれ複製を持つ。別の build の出力を参照先として渡す入力は無い。重複を避ける案内は「各 asset を 1 つの build からだけ参照し、他の build からは実行時に root 経由で引く」だけである。build は増分で「前回の build から変わった content だけを作り直す」と書かれているが、「前回」の単位は書かれていない。
  - 置いた形: CI は目的（宣言）ごとに build する。リリースは 1 つの目的の build だけを載せる（重複なし）。開発者は **取得した 1 つの目的 build に、自分が触った物だけ手元で build して上書きとして後から登録する**（重複は上書きが直接参照する共通 asset の 1 組分で、その人の PC でだけ生じる。owner は許容と確認した）。
  - 目的別 build 同士を同時に登録する混ぜ方は主にしない。共通 asset の実体が目的の数だけ増え、索引で決められるのは同じ Scene のどちらを読むかだけで、重複と解除の寿命は決められないため（A2 の F7）。
- 範囲: この PC、Unity 6000.6.0f1、StandaloneWindows64 の Player subtarget、loose file、合成 fixture、Editor の batch。
- 言わない事: 実 project の絶対時間。別の PC・CI の agent をまたぐ cache。更新を file 単位の差分で小さく配れるか（番 2 の配り直し量は材料として残すだけ）。所要が短い事を「作り直していない」の証明にしない（時間から仕組みは読めない。出力の sha256 の一致も証明にならない。事実の文書は、`CleanBuildCache` の再 build でも共通の file の bytes が一致したと書いている）。
- 次のスライス（この記録の対象外。合否を付ける問い）: 取得した 1 つの目的 build に、手元で build した上書きを足して登録し、同じ Scene は上書き側が読まれ、正しい class が付き、実体の重複が上書きの分に収まり、Play の終わりまで壊れないか。fixture は root が一部重なる 2 つの build にする。保留中の S2 の計画（draft の [PR #180](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/180)）の扱いと program §8 の S2 の行の改訂は、そのスライスの計画で owner が決める。

## 2. 報告する物

番ごとに、`BuildPipeline.BuildContentDirectory` の呼び出しの所要（stopwatch と `BuildReport.summary.totalTime` の両方）、番 0 に対する比、出力の file の数と bytes、同じ root の集合の直前の出力に無い sha256 の file の数と bytes を表にする。stopwatch と `totalTime` が 1 割以上食い違う番は、両方を書いてその旨を記す。

測定が無効になる条件（やり直す。やり直しは 2 回まで）:

- 番 0 の所要が 60 秒未満（差が固定費に埋もれる）。fixture を 2 倍にして準備からやり直す。番 0 が 30 分を超えたら半分にしてやり直す。
- 番 2 の出力に、番 1 の出力に無い sha256 の file が無い（書き換えが build に届いていない）。
- 番 0 の所要が番 1 の所要の 1.5 倍未満（例: 番 0 が 80 秒で番 1 が 70 秒）。基準の build が cache を使った疑いがあり、比に意味が無い。この時はやり直さず、その旨を結果として報告する。番 0 が 80 秒で番 1 が 40 秒なら有効として報告する。

## 3. 手順

- fixture（合成。SampleGame を使わない。準備の process で生成と import を済ませ、その時間は使わない）: 1024×1024 の texture 300 枚（PNG。import 設定は `TextureImporter` の既定で、読めない・圧縮あり・mipmap あり。`isReadable` を record に書く）、それぞれを `_BaseMap` に持つ URP `Lit` の material 300、Scene 100（各 Scene は quad 20 個で、Scene `s` の `k` 番目は material `(3s + k) mod 300` を直接参照する）。root の集合は X が Scene 0〜59、Y が Scene 40〜99。
- build の条件（製品の publish に合わせる。A2 の F1）: 出力先は root の集合ごとに固定の workspace（X 用と Y 用）で、各番の前にその workspace を空にする（製品の `Full`）。`name` は毎回新しい identity、root asset（製品の `BuildContentRoot` を無改変で使い、Scene を `LoadableSceneId` で持つ）も毎回作って build の後に消す。`compression` は `Uncompressed`、`options` は番 0 と番 6 だけ `CleanBuildCache`、ほかは `None`。target が StandaloneWindows64 の Player subtarget である事を各番で確かめて record に書く（違えば build せず失敗にする。切り替えはしない）。製品の `BuildContentCoordinator`（選択と materialization）は通さず、Unity に渡す条件だけを合わせる。
- 順序（1 行 = 1 つの Unity process。同じ `Library`）:

| 番 | 操作 | 見る物 |
|---|---|---|
| 準備 | fixture を生成し import を済ませる | 使わない |
| 0 | X を `CleanBuildCache` で build | 基準 |
| 1 | 何も変えず X | 変更なしの再 build |
| 2 | texture 10（Scene 0〜3 の 4 つが参照し、全て X に入る）を書き換えて import し、X | 1 枚の変更。配り直す量 |
| 3 | Y（この `Library` で初めて。Scene 40〜59 は X と重なる） | 切り替えた先の初回 |
| 4 | X | 別の集合を挟んだ後の再 build |
| 5 | Y | 同上 |
| 6 | X を `CleanBuildCache` で build | 番 0 との対照（番 0 にだけ掛かる初回の費用が、build cache の外にあるか。例: shader の初回コンパイルが `Library` に残る場合、番 6 は番 0 より短くなる） |

- 実行: 準備と番 0〜6 を、test 1 件ずつ別の `pwsh tools/run-tests.ps1 -Filter <test の完全名>` で順に実行する。`unity test` / `unity run` は使わない。XML の完成後に Unity が終わらない既知の native 終了 stall は `tools/Harness/README.md` の停止規則に従い、調査しない。
- 登録・load・Play は行わない。

## 4. 置き場と git

- **harness・fixture・生の record・build の出力を commit しない（2026-11-12、owner）。** 測定用の worktree に未追跡の file として置き、終わったら消す。Artifact Storage にも保存しない。測り直しの手段は §3 の手順の文章である。
- harness は `unity/Assets/OneStarMaker/Tests/Editor/Spikes/` の下に置く EditMode の test 1 class で、全ての test に `[Explicit]` を付ける（file が残っている間に空 filter の全 EditMode 回帰が build を実行しないため。A2 の F8）。
- 追跡する変更は、この文書と、結果の harvest（事実の文書と program）だけである。実装 commit が無いので、判定 C・C' と implementation head の照合は行わない（A2 の F3 の 2 つ目の選択）。全 EditMode 回帰も行わない（追跡する code の変更が無い）。
- 測定の後に担当が record から確かめて §6 に書く事: 各番が別の process だった事、所要に import と hash の計算が入っていない事、番 2 の書き換えが届いた事、target と subtarget。

## 5. 計画レビュー記録

- **A2 外部レビュー（2026-11-12、Cursor の background agent、owner 経由。PR #181 のコメント）:** 入力は commit `e342a404` のこの文書（slice として書いた revision 0）。
  - F1（測る経路が CI の build と違う）: 採用。§3 の build の条件を製品の publish に合わせた。
  - F2（目的・方向・ゲートが別の番を見ている）: 採用。合否の判定を外し、番ごとの数値の報告にした。目的の文言は owner が「事実の測定」と裁定した（2026-11-12）。経路 (e)（番 0 の長さが content cache 以外の初回の費用である）は番 6 の対照で見る。
  - F3（harness を commit しないと判定 C を始められない）: 2 つ目の選択を採用。workflow の判定 C・C' を行わない調査記録にした。
  - F4（1 割の線と「同じ程度」に数値の例が無い）: 採用。1 割の線は廃止し、無効の条件に数値の例を書いた。
  - F5（番 2 の texture の index と import 設定）: 採用。texture 10 に固定し、import 設定を書いた。fixture の上限（30 分）を足した。
  - F8（未追跡の test に `[Explicit]`）: 採用。
  - F6・F7（方向と、目的別 build を混ぜる粒度）: 次のスライスの前提として採用。§1 の方向を「取得した 1 つの目的 build に手元の上書きを足す」に改め、目的別 build 同士の同時登録を主から外した。
- 目的レビューとアーキテクチャゲート: 行っていない（調査記録。配置・依存・所有者・寿命を変える物が無い）。
- 計画の主担当: Claude Fable 5.2（Anthropic）。
- 凍結の前に実行した物: fixture の生成（準備）だけを 1 回（2026-11-12。harness が compile し fixture が import される事の確認）。

## 6. 結果（2026-11-12〜13、各番 1 回ずつ）

出力は X・Y とも 660 file、約 142 MB。stopwatch と `totalTime` は全ての番で 1 秒以内に一致した。

| 番 | 操作 | 所要（秒） | 番 0 比 | 同じ集合の直前の出力に無い file |
|---|---|---:|---:|---|
| 0 | X、`CleanBuildCache` | 336 | 100% | — |
| 1 | X、変更なし | 158 | 47% | （一覧を取り損ねた。番 0 の一覧を消したため） |
| 2 | X、texture 1 枚変更 | 57 | 17% | 5 file、799,353 bytes |
| 3 | Y、初回 | 250 | 74% | —（番 2 の X と同じ sha256 の file は 308、68,062,048 bytes） |
| 4 | X、Y を挟んだ後 | 82 | 24% | 番 1 に対し 5 file、799,353 bytes（番 2 の変更の分） |
| 5 | Y、変更なし | 48 | 14% | 番 3 に対し 3 file、98,038 bytes |
| 6 | X、`CleanBuildCache` | 437 | 130% | 番 1 に対し 5 file、799,353 bytes |

読める事（この PC、合成 fixture、各 1 回の範囲）:

- 変更の無い再 build は cache なしの build より短いが、0 にはならない。48〜158 秒（番 0 の 14〜47%）で、ばらつきが大きい。
- 別の root の集合を挟んでも cache は失われない（番 4 が 24%、番 5 が 14%。切り替えた先の初回の番 3 は 74%）。
- 番 3 は、出力の約半分（308 file、bytes で 48%）が X と同じ sha256 なのに 74% かかった。集合をまたいだ共有による短縮は小さい。
- texture 1 枚の変更で変わった file は 5 つ、約 0.8 MB。変更の無い番 5 でも 3 file・約 98 KB が変わるので（毎回新しい identity の root と manifest の分と読める）、texture の分は 2 file・約 0.7 MB と読める。
- cache なしの build 自体が 336 秒と 437 秒で 3 割違う。比は目安で、1 割単位の差は読めない。番 6 が番 0 より短くならなかったので、「番 0 にだけ掛かる初回の費用」は見えなかった。

無効の条件（§2）: どれにも当たらない（番 0 は 60 秒以上、番 2 に新しい file あり、番 0 は番 1 の 2.1 倍）。

測定の条件で記録しておく事:

- 番 0 と番 1 の間が約 7 時間空いた（番 0 の後に Unity が終了処理で止まり、続きが流れなかった）。番 1 の 158 秒が他の変更なしの番（48〜82 秒）より長いのは、この間隔（OS の file cache が冷えた等）の影響かもしれず、切り分けていない。
- 番 0 の test は、build 中の Burst の Error log（pipe が見つからない）で失敗扱いになった。build は成功し record は書かれた。番 1 以降は harness がこの種の log で失敗しないようにした。
- 全ての番で、record が書かれた後に Unity を強制終了した（既知の native 終了 stall を待たないため）。自然終了は確認していない。
- 各番は別の Unity process、所要に import と hash の計算は入っていない、target は StandaloneWindows64 / Player、texture は読めない設定、を record と harness で確かめた。

## 7. この文書の終了

結果を事実の文書へ harvest し、program §8 に次のスライスの行を足した時点で、この文書を削除する。
