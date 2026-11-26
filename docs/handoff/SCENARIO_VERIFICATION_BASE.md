# 実機の検証の基盤 — S4-V

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S4-V。要求の正本は program §9.1）
- status: C' blind bundle の作り直し待ち（head `c38e0762` で判定 C（2 回目）は GO。C'（2 巡目）は GO だったが、その入力 bundle に固定 manifest が無く、前巡の C' の結果を収録していたので、作り直した bundle で C' をやり直す。§7・§8。凍結した Phase A は revision 0）
- branch: `claude/scenario-verification-base`（PR の base は `develop`）
- implementation base commit: `07cc2c975b63cf5b668130a1c45490dd3e77c5f0`
- implementation head commit: `c38e076263d138c7ad9445a161dc0ff2c617d1bf`
- A3 凍結記録: 承認者 owner（root）、2026-11-26、revision 0（改訂 4。この記録を含む commit）。次の担当は Phase B
- risk: `normal`（A1 で見直した。製品の code・公開 API・asmdef・Addressables を変えない。checkout を書き換え process を止める道具なので、A2 に不変条件を破る順序を探す観点を 1 件足した。§5.1。A2 の後も normal のままにした）
- owner: root（依頼者）
- created: 2026-11-25
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: [tools/Harness/README.md](../../tools/Harness/README.md)（基盤の契約と scenario の書き方。Phase B が書く）、program §9.1 の利用者表（各操作の状態）
- Phase A snapshot の取得案内・状態: ローカル固定済み（未昇格）。判定 C の照合の合成 fixture 59 組と期待の一覧（README、`expectations.json`、観測ごとの zip 3 つ）を task `s4-v-scenario-verification-base` に `evidence stage` で固定し、別の process の `fetch` で 5 file の一致を確かめた。取得案内は、判定 C の時点で手元の取得案内の置き場に無く、主担当の session の stage の出力（reference と packageSha256）から判定 C が取得し、同じ内容の案内を S4-V の取得案内の置き場（この PC の git 管理外の運用 directory の `s4-v-scenario-verification-base/handoff-guides/` の `phase-a.md`）に再掲した。別の process の取得で 5 file を確かめた（判定 C、2026-11-26）。Phase B には渡さない
- Phase B result snapshot の取得案内・状態: 該当なし（B は Unity の test を実行していない。結果は §6 と implementation head の差分。C' 用には Phase C の前の §6 を C' blind bundle に収録した）
- evidence bundle の取得案内・状態: **head `c38e0762` の物:** ローカル固定済み（未昇格）。判定 C（2 回目）の生証拠（V1・V2・V3 の原記録、offline と監査の log、V3 の期待集合、Phase A snapshot、完全 diff、判定 C の照合 script と fixture・原記録の結果。30 file）を implementation head で固定し、別の process の取得で 30 file が元の file と同じ SHA-256 であることを確かめた（2026-11-26）。引渡し先・担当は Phase D・owner（C' の後の突き合わせ）。取得案内は同じ置き場の `judgement-c38e0762.md`。**旧 head `c3f02632` の物（新しい head の合格証拠にしない）:** ローカル固定済み（未昇格）。判定 C の生証拠（V1・V2・V3 の原記録、offline と監査の log、V3 の期待集合、Phase A snapshot、完全 diff、判定 C の照合 script と結果。29 file と manifest）を implementation head で固定し、別の process の取得で manifest の全 file の SHA-256 が一致することを確かめた（2026-11-26）。引渡し先・担当は Phase D・owner（C' の後の突き合わせ）。取得案内は同じ置き場の `judgement.md`
- C' blind bundle の取得案内・状態: **head `c38e0762` の物（C' の 2 巡目の入力）:** ローカル固定済み（未昇格）。凍結した Phase A（A3 凍結版 `74e02dad` の本書と Phase A snapshot）、Phase C の前の Phase B 結果（`05963fa3` の §6）、本書を除いた base から head までの diff と、前巡の head `c3f02632` から `c38e0762` への修正差分（本書を除く）、C' 自身の 1 巡目の結果（§8 の転記）、判定 C と同じ原記録・期待集合・offline と監査の log（26 file。所見と判定 C の照合 script・結果は含まない。C' 用決定記録は無い）。別の process の取得で 26 file が元の file と同じ SHA-256 であることを確かめた（2026-11-26）。引渡し先・担当は C'（2 巡目）の担当。取得案内は同じ置き場の `cprime-c38e0762.md`。**旧 head `c3f02632` の物（使わない）:** ローカル固定済み（未昇格）。凍結した Phase A（A3 凍結版 `74e02dad` の本書と Phase A snapshot）、Phase C の前の Phase B 結果（`05963fa3` の §6）、本書を除いた diff、判定 C と同じ生証拠（23 file と manifest。所見と判定 C の照合 script・結果は含まない）。別の process の取得で全 file の SHA-256 の一致を確かめた。引渡し先・担当は C' の担当。取得案内は同じ置き場の `cprime.md`
- 引継ぎ票:
  - 次の Phase・役割: 判定 C の担当（C' blind bundle の作り直し。head `c38e0762`）。
  - 次の作業: 開始時に [引継ぎ票](../../.agents/skills/osm-workflow/references/phases-and-handoff.md#引継ぎ票) の照合を行う。判定 C（2 回目）が固定した C' blind bundle（取得案内 `cprime-c38e0762.md`）は [review-evidence](../../.agents/skills/osm-workflow/references/review-evidence.md) の 2 点に合わない。(1) 各 file の path・生成時刻・hash を記録する固定 manifest が無い（同文書の「manifest に記録する」「packageSha256 と混同しない」）。(2) 前巡の C' の結果（`cprime-prior/`）を収録している（同文書の「C' 用決定記録」は、2 巡目以降に渡せるのを C' 自身の前巡の指摘への人間の裁定だけに限る。今回は裁定が無いので渡す物は無い）。同じ head・同じ原記録（run `j2`）から、manifest を持ち `cprime-prior/` を除いた C' blind bundle を stage し直し、別の process の取得で manifest の全 file の SHA-256 の一致を確かめ、新しい取得案内を既存と同じ置き場に置き、§0 と §7 に記録する。判定用の evidence bundle（`judgement-c38e0762.md`）にも固定 manifest があるかを確かめ、無ければ同じく作り直す。Unity は起動しない（原記録は欠けていない）。判定 C の結論は変えない。次の担当は C'（2 巡目のやり直し）。
  - 読む範囲: §0、§7 の判定 C（2 回目）、review-evidence の C' blind bundle と C' 用決定記録の節。
  - 必要な権限: Artifact Storage の通常操作（stage と取得）。Unity の起動は要らない。
  - 停止規則: 実装を直さない。判定をやり直さない。C' blind bundle に発見 C・判定 C・C' の所見、照合 script と結果を入れない。
  - 引き渡すもの: 新しい C' blind bundle の取得案内（C' の担当へは進行役が渡す）。
  - 担当のモデルと独立性: 判定 C と同じく Phase B と同じ系列・モデルでよく、新しい session で行う（owner 受容）。C' は Phase B・C と違う系列で、新しい session で行い、§8 に使ったモデルと「独立性制約あり」を記録する。
  - C' の独立性の注記: Anthropic（主担当、A2、Phase B、発見 C、判定 C）、OpenAI（設計の材料、A2 の目的レビュー、外部計画レビュー）、xAI の Grok（外部計画レビュー、C'）が関わった。

### 実測の方法

- base `07cc2c97` の code と文書を読み、`grep` と `wc -l` で確かめた。driver は起動していない。Unity は、改訂 1 で窓のある Editor の起動の疎通だけを行った（§5 の「未知の操作経路」）。疎通の 1 回目は主担当の script の誤りで何も観測できず、import の途中の Unity を止めた（project lock の残骸はこの時の物）。2 回目が §5 の観測である。疎通の後、Editor が書き換えた URP の設定を `git checkout` で戻した。
- 入力として読んだ物: program §8 の S4-V の行と §9.1、[PR #214](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/214) のコメント 3 件（2026-11-25）、S4-3a の HANDOFF（同 PR の branch の `docs/handoff/LOCAL_CONTENT_ORIGIN.md`、head `fd1e15dc`）の §1 の現況と §5 の E 系列。

## 1. 目的と対象外

- 目的: Agent が新しい実機の検証（複数の checkout、Unity の batch の実行、窓のある Editor の操作、Player の process、固定 patch、run ごとの cache と証拠を使う検証）を作るとき、検証環境の構築方法を毎回考えなくて済むようにする。検証を書く担当は、自分の case・fixture・照合する値だけを書けば済む。そのための共通の基盤を `tools/` に置き、今ある利用者（S4-2 の driver と、窓のある Editor の疎通）で成り立つことを示す。
- 利用者: 実機の検証の driver を書く担当（program §9.1 の利用者表の各行の Phase A・B の担当。主に Agent）と、その driver を実行する担当（判定 C の実行担当）。
- 目的・利用者の文言の出どころ: A0 で書いた（骨組みの版には無かった）。1 文目は owner が 2026-11-25 の起動の指示で示した理想の文で、2 文目以降は program §8 の S4-V の行の問いである。A1 では改めていない。
- 対象外:
  - 製品の code と、製品の検証の合否。基盤は製品の型も入口も参照せず、受け入れ条件の合否を判定しない（program §9.1 の境界の規則）。
  - Player の build の取り直しと、S4-2 の判定の取り直し。S4-2 の driver の実の実行（Unity の起動を伴う case）は、S4-3d が次に走らせる時に初めて通る。
  - S4-3a 以降の case の列・fixture・照合する値。各行が持つ。
  - 端末（Android・iOS）の Player の起動（program §9.1 の S4-5 の行。分類 (c)）。
  - `tools/unity-editor.ps1` の変更（そのまま使う）、`run-tests.ps1` の公開の引数・marker・step の形の変更、Harness の H1 / H2c の CURRENT と gate の変更。
  - 判定 C の照合の規則の変更（[review-evidence の「判定 C の照合 script」](../../.agents/skills/osm-workflow/references/review-evidence.md#判定-c-の照合-script)）。
- 現況（base `07cc2c97`）:

| 事実 | 根拠 | この行への影響 |
|---|---|---|
| S4-2 の driver は、2 つの checkout の受け取り、case ごとの patch の適用と復元、`run-tests.ps1` の起動と件数の検査、run ごとの cache、記録を 1 つの file に持つ。case の列・test の class の名前・環境変数・`OSMContentDelivery.json`・`artifacts/playable` の複写は S4-2 に固有で、残りは固有でない | `tools/s42-player-overlay.ps1`（186 行）: S4-2 に固有なのは 41-43・47-74（test の class と case の列）、76-78（record の置き場 `TestResults/s42`）、104-132（cache の設定と複写）、145-147（環境変数と filter）。固有でないのは 80-101、134-144・148-165（patch・復元・marker・件数）、167-185（事前確認と step の選び方）。44-45 と 98-101・137・168 の URP の設定の戻しは、S4-2 ではなく repo の事情 | 固有でない部分を基盤へ移し、固有の部分だけを driver に残す。URP の設定を先に戻す処理は移さない（基盤は case の後に毎回 HEAD へ戻すので、次の case は clean から始まる。§2.9） |
| その driver の弱点: (1) RunId の検査 `^[A-Za-z0-9._-]+$` は `.` と `..` を通す。cache の root は `<drive>\s42c\<RunId>` で、最後の step が `Remove-Item -Recurse -Force` するので、RunId が `..` なら drive の root を消しに行く。(2) run の directory と cache を `New-Item -Force` で作り、既にある物をそのまま使う。(3) 最初に URP の設定を `git checkout` してから clean を確かめる（確かめる前に書く）。(4) patch の適用が `try` の外にあり、2 つ目の patch の失敗で 1 つ目が戻らない。(5) `finally` は `run-tests.ps1` の子の process がどう終わっても checkout を戻す（子が先に落ちて Unity が残っていても戻す）。(6) X と Y の違いは解決した path の文字列の比較（`-ieq`）だけで、junction を見ない。(7) `git status` の一致は、ignore される `UserSettings/`・`TestResults/`・`artifacts/` の状態を示さない | 同 file の 39・104・180、77・107、167-171、137-141、156-162、36-38。`.gitignore` の 30・33・68 | 基盤はこれらを写さない。§2 の不変条件 N1〜N4 と受け入れ条件 AC1〜AC5 |
| `run-tests.ps1` は Unity の process の起動を自分で持つ。`ProcessStartInfo.ArgumentList` で渡し、起動の直後に PID を固定し、開始した process の終了を期限なしで待つ。project lock は、稼働中の Editor が持つ物と残骸を分けて見る（残骸は消す）。起動の前に path 予算を検査する。どちらの関数も script の中の private で、dot-source は offline test の専用と書いてある | `tools/run-tests.ps1` の 42-72（PID は 59、待機は 63）、74-87、170-192、199、308 | 窓のある Editor と batch の `-executeMethod` を別に起動すると、この安全策の写しが要る。起動と lock を module に切り出し、runner と基盤が同じ物を使う（§3） |
| `run-tests.ps1` には、終了コードを得るまで asset を戻さない先例がある（`-ObserveUnity` の `EditorSettings` の復元。終了を確かめられなければ `termination-unconfirmed` で戻さない） | 同 125-152（133） | N2 の形の先例 |
| `tools/unity-editor.ps1` は、開いている Editor への `status`・`command`・`eval <UTF-8 Base64>`・`pipeline list` だけを通し、接続先を「その script のある checkout の `unity/`」に固定する。`--project-path` の上書きは拒む。Editor を開く・閉じる入口は持たない | `tools/unity-editor.ps1` の 39-46、90-93、100-116、118-124 | 検証用の checkout の Editor は、その checkout の `tools/unity-editor.cmd` で操作する。別の接続の実装を作らない |
| `tools/` に、窓のある Editor を起動して閉じる script は無い。Play ボタンの入口は batch と Test Runner の Scene を素通りするので、Play を見る検証は `run-tests.ps1` の test では組めず、窓のある Editor を `eval` で操作する | S4-3a の HANDOFF §1（`PlayEntryInterceptor.cs:184-196`、`PlayEntryDecision.cs:197-201`）。`tools/` と `docs/` に `unity open`・`EditorApplication.Exit` の記述が無い（grep） | 窓のある Editor の adapter（起動・ready・操作・終了）が要る |
| 過去の判定で観測した Editor の挙動（この PC、6000.6.x）: 新しい checkout の初回の起動は package の解決と import に約 11 分かかる。`status` が ready でも import 中の `eval` は 503 を返す。同期の長い処理は `Main thread operation timed out after 5000ms` の error 応答の後も最後まで走る。Play の開始・停止の移行中に送った `eval` は捨てられ、Console に Error を残す。`EditorApplication.Exit(0)` の応答は error になるが終了は正常。Editor を開くと `unity/Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` に serialize の差が付き、`unity/Assets/MobileDependencyResolver/` の下の `.meta` が working tree に出る | [18 章](../../unity/Assets/Docs/Architecture/18-asset-description.md)「宣言からの Play 経路」の観測の注意、S4-2 の driver の 44-45、S4-3a の HANDOFF §5 の RX-9。残りは担当の運用の記録で、Phase C の疎通（AC10）で取り直す | 送る `eval` は応答で成否を決められない（送り直さず、状態を読んで確かめる）。読む `eval` は読み直せる。ready は `status` だけで決めない。終了は応答ではなく process の handle で確かめる |
| Unity の終了の既知の native stall では、終了コード 0 でも process の handle が signal にならないことがある。正常な終了は保持した handle の signal と実の終了コードで確かめ、強制終了した取得を成功の証拠にしない。人の process を止めず、過去の PID 用の停止 script を使い回さない | [Harness README](../../tools/Harness/README.md#unity終了時の既知native-stallと調査の停止規則) | N2・N3 と AC3 |
| path 予算は repo 相対 150 文字で、checkout の root は末尾の区切りを含めて 109 文字まで。判定は `PathBudget.psm1` が持ち、`Test-PathBudgetRoot` を公開している | `tools/path-budget.json`、`tools/Harness/PathBudget.psm1` の 85・171 | checkout を用意する時にこの判定を使う |
| Harness の offline test は自前の `Run` / `Assert` で書かれ、一時の git repo を fixture に使い、runner の process は seam（`-ProcessInvoker`）で差し替える。CI の `harness-tests` job が 5 本を走らせる。`unity-editor.tests.ps1` は手元の Unity CLI が要るので CI に入っていない | `tools/Harness/tests/UnityTestRunner.Tests.ps1` の 1-34・150、`.github/workflows/offline-checks.yml` の 43-69 | 基盤の offline test も同じ形で書き、CI に足す |
| `Unity.exe -projectPath <path> -logFile <path>` で直接起動した窓のある Editor は、その checkout の `tools/unity-editor.cmd` の `status` と `eval` で操作できる。`status` の instance の `pid` と、`eval` が返す Editor 自身の PID は、起動した process の PID と同じである。`status` が ready でも、しばらくは `eval` が 503 を返す。終了の要求の応答は error になり、handle は数秒で signal になる。project lock の file は handle の signal より先に消える。持つ process のいない lock の残骸があっても起動する | Phase A の疎通（2026-11-25、6000.6.9f1、1 回。§5 の「未知の操作経路」） | 窓のある Editor の adapter は直接の起動で作れる。応答した Editor の照合に PID を使える。終了は handle で確かめ、lock の不在を終了の確認にしない |

- 今の driver の case の失敗の後の挙動（互換の対象。AC8）: test が `failed` でも実行件数が 1 なら次の case へ進み、最後まで走れば exit 0（driver は合否を判定しない。160・164）。件数が 1 でない時と marker が無い時は、record を書いてから止まる（164）。復元の後の状態が前と違う時も止まる（162）。`-Only` に列に無い名前を渡すと、何も走らずに exit 0 で終わる（174）。
- UC ごとの利用者の入力:

| UC | 利用者が選ぶ・入れるもの（誰がいつ） | 機械が決めるもの | 入力の正当化 |
|---|---|---|---|
| UC1 新しい検証の driver を書く | scenario の名前（開発時の宣言）。使う checkout の名前（`X`、`Y` など。開発時の宣言）。case の列（名前、どの checkout、当てる patch、body）。fixture。保存する証拠（開発時の宣言） | checkout の場所と作り方、事前確認、run の directory と記録の file、Unity の実行 file、lock の確認、checkout と run の占有、process の起動と終了の確認、patch の適用と復元の順、record の欄、`-Only`・`-DryRun` の意味 | 目的の「自分の case・fixture・照合する値だけ」に当たる物だけを入力に残した。checkout の名前は、case がどの checkout で走るかを指すために要る |
| UC2 driver を実行する | RunId（実行時の操作）。対象の commit（実行時の操作。省けば driver のある checkout の `HEAD`）。`-Only`、`-DryRun`（実行時の操作。省ける） | 上と同じ。checkout が無ければ作り、あれば対象の commit に合わせる | RunId は証拠を run ごとに分けるために要る。対象の commit は、判定の対象の head を実行の担当が指すために要る。S4-2 の driver だけは、互換のために `-XRoot`・`-YRoot` を受け続ける（program の行の「引数の互換」） |
| UC3 止まった run の続きを走らせる | 同じ RunId と `-Only`（実行時の操作）。終了を確かめられずに止まった時だけ、その前に checkout を手で戻して隔離を解除する（Harness README の手順。AC3） | run の directory の印（名前、RunId、対象の commit、checkout の場所、dry-run か本番か）の照合、checkout の事前確認のやり直し、試行の番号 | 止まった所から続けるには、どの run のどの step かを指す以外の入力は要らない。手で戻す操作は、基盤が終了を確かめられない process の後だけに残る（基盤は自分の始めていない確認で checkout を戻さない。N2） |
| UC4 検証用の checkout を登録する・片付ける | 対象の path と操作（実行時の操作。`scenario-checkout.ps1`）。登録は、手で作った worktree を渡す時だけ要る | 印のある checkout だけを選び、手が入っておらず Editor が開いていない物を `git worktree remove` で外す。登録は条件を満たす時だけ印を置く | `Library` を run の間で使い回すので、片付けは実行の担当が要る時に指す |

## 2. 意思決定と受け入れ境界

### 2.1 用語

- **scenario:** 1 つの検証の driver（例: `tools/s42-player-overlay.ps1`）。case の列を持つ。
- **case:** driver の 1 step。checkout を 1 つ使う case（patch を当て、Unity を動かし、戻す）と、checkout を使わない case（複写、片付け）がある。
- **run:** 1 つの RunId での scenario の実行。driver の呼び出しをまたいで続く（`-Only` の続きの実行）。**run の root** は記録と証拠を置く directory、**run の外部 root** は repo の外の短い path に置く directory で、cache や fixture をその子に作る。**run の印** は run の root に置く file で、scenario の名前、RunId、対象の commit、checkout の場所、dry-run か本番かを持つ。
- **検証用の checkout:** linked worktree で、HEAD が detached で、その worktree の専用の git dir に **基盤の印** がある物。印は、基盤が worktree を作る時か、実行の担当が登録の操作をした時にだけ置かれる。
- **lease:** driver の 1 回の呼び出しの間、基盤が検証用の checkout と run の root を占有する印（排他で開いて保持する file）。process が終われば OS が離す。
- **使用中の記録:** 基盤が、checkout を変える前（patch を当てる前、Unity を起動する前）に worktree の git dir に置き、復元を確かめた後に消す記録（run、case、試行、基盤が始めた process の PID と開始時刻）。driver が途中で落ちても残る。
- **隔離:** 使用中の記録が残っているのに、その checkout の lease を持つ呼び出しがいない状態。解除するまで、どの run もその checkout を使えない。
- **予約:** run が checkout の中に管理する file を残している間、その checkout をその run に結び付ける記録（worktree の git dir）。別の run、HEAD の切替、片付けを拒む。
- **送る `eval`・読む `eval`:** Editor の状態を変える `eval` と、状態を読むだけの `eval`。
- **試行:** 同じ run で同じ case を走らせた回。基盤が自分で保存する証拠は試行ごとに分ける。

### 2.2 このスライスが答える問い

program §9.1 の利用者表の全ての利用者が、検証環境の構築（checkout の用意と安全の確認、process の起動と終了の確認、Editor の操作、編集の適用と復元、証拠の保存）を自分で書かずに、case・fixture・照合する値だけを書けば済む基盤を、今ある利用者（S4-2 の driver と、窓のある Editor の疎通）で示せるか。

### 2.3 進める最低条件

**前提（N1〜N3 が立つ範囲）:** 検証用の checkout は検証の専用で、人が同時に編集しない。基盤が守るのは、検証用でない checkout、基盤を使う run 同士、基盤が確かめた時点の状態である。任意の git の process や人の同時の編集との排他は保証しない。

#### 不変条件（起きてはいけないこと）

| # | 守る主張 | 主張を崩しうる経路（探索した境界は `tools/s42-player-overlay.ps1`、`tools/run-tests.ps1`、`tools/unity-editor.ps1`、Harness README と、A2・外部計画レビューが挙げた順序） | 未確認 |
|---|---|---|---|
| N1 | 検証用でない checkout を変えない。検証用の checkout も、手が入っている時と、別の run が使っている時は変えない | 確かめる前の `git checkout`（S4-2 の 167-171）。clean で detached なだけの、人が過去の commit を調べている worktree。X と Y が同じ場所を指す（36-38 は文字列の比較だけ）。rebase・bisect・merge の途中の worktree。別の clone の worktree。渡された checkout の HEAD を対象の commit へ動かす。Unity の起動が書き換える file だからという理由で、人の編集を捨てる。2 つの run が同じ checkout の事前確認を同時に通り、片方が patch を当て、もう片方が HEAD を切り替える。run A が checkout に設定の file を残して終わり、run B が同じ checkout でそれを読むか上書きし、A が続きを走らせる。1 つ目の checkout に書いた後で 2 つ目の事前確認が落ちる | — |
| N2 | 終了を確かめていない process が使っている checkout を戻さない。その先の case へ進まない。次の run にも使わせない | S4-2 の `finally`（156-162）は、子の `pwsh` が落ちて Unity が残っていても戻す。基盤が持つ handle が子の `pwsh` の物である時、その終了は Unity の終了を示さない。project lock が無いことも、Unity がいないことを示さない（起動の直後でまだ取っていない、終了の途中で先に離した）。`EditorApplication.Exit(0)` の応答は error になる。既知の native stall では終了コード 0 でも handle が signal にならない。case の body が Editor を開いたまま例外を投げる。止まった後の次の呼び出しが、同じ checkout を使う。driver 自身が強制終了されるか PC が再起動して、lease が離され、終了の未確認を書く機会が無い | — |
| N3 | run が始めていない process を止めない・操作しない。run が作っていない path を消さない・上書きしない | RunId `..` と `Remove-Item -Recurse -Force`（39・104・180）。scenario の名前や checkout の名前に区切りや `..` を入れて、決めた親の外へ出る。`New-Item -Force` の再利用（77・107）。印のある directory を別の物に差し替える。親の directory を junction にする。管理する file の path が checkout の外を指す。作る途中で失敗した directory を消す。PID や名前で process を探して止める手順。事前確認の後に別の Editor が同じ project を開くと、基盤の `eval` と終了の要求がその Editor に届く。lock の残骸を消す関数（`run-tests.ps1` の 86）を基盤が使う | — |
| N4 | 起動の失敗、期限切れ、実行 0 件、古い実行の結果、証拠の保存の失敗、復元の失敗、対象でない commit での実行を、成功の記録にしない | marker が無い時の `status='no-marker'` と、その後の件数の検査（148-154・164）。marker が、前の起動の step を指す。`-Only` の打ち間違いで何も走らずに exit 0（174）。`-DryRun` の record が本番の record と同じ file に並ぶ。続きの実行の間に対象の commit が変わる | — |
| N5 | 基盤は製品を知らず、製品の合否を判定しない | S4-2 の driver が持つ製品の名前（41-43、104-131、145-146）が、移す時に基盤へ入る。製品の test の `failed` を基盤の失敗に数える | — |
| N6 | 今ある入口の挙動を変えない: `run-tests.ps1` の引数・marker・step の形・終了の待ち方、`tools/unity-editor.ps1`、S4-2 の driver の引数・case の順・`-Only`・`-DryRun`・`driver.jsonl` の今ある key・case の失敗の後の進み方 | `run-tests.ps1` の process の起動と実行 file の解決（42-87、228-240）を module へ切り出す変更。`Invoke-UnityTestProcess` は `CreateNoWindow = $true`（48）で、起動できなかった時は `ExitCode` に `$null` を返す（70）。`-ObserveUnity` は `ExitCode -is [int]` を終了の確認に使う（255）。既存の offline test は `-ProcessInvoker` を差し替えている。Harness の Unity adapter は `run-tests.ps1` を固定の引数で呼ぶ（`Adapters/UnityChecks.psm1` の 182・271） | — |

#### 成立条件（起きなければならないこと）

| # | 条件 |
|---|---|
| S1 | 新しい scenario が、case・fixture・保存する証拠の宣言だけで書ける。git・process・削除・Editor の接続の code を driver に書かない。S4-3a の E 系列の形（2 つの checkout、patch、fixture、設定の file、Editor の操作と観測、再起動、続きの実行）が、基盤の公開する関数だけで組める |
| S2 | S4-2 の driver が基盤の上に移り、引数・case の順・`-Only`・`-DryRun`・`driver.jsonl` の今ある key・case の失敗の後の進み方が変わらない |
| S3 | 窓のある Editor を、起動 → ready → 読む `eval` → 送る `eval` → 読んで確かめる → 終了、の順で実機で 1 回通し、応答したのが基盤の起動した Editor であることと、終了を process の handle で確かめる |
| S4 | program §9.1 の利用者表の「基盤に要る操作」の全てが (a)(b)(c) のどれかに分かれ、(a) は offline test か実機で、(b) は offline test で示される（§2.4） |
| S5 | 検証用の checkout の用意（無ければ作る、あれば対象の commit に合わせる）と片付けが基盤の入口で済み、実行の担当が worktree を手で作らない |
| S6 | 次に検証を書く担当が、基盤の入口と契約に辿り着ける（Harness README と、workflow の検証経路の節の 1 文） |

### 2.4 操作の分類（program §9.1 の「基盤の受け入れの決め方」）

(a) 実装し、今ある利用者で示す。(b) 拡張点だけを置き、後続を塞がないことを offline test で示す。(c) 対象外。

| # | 操作（利用者表の行） | 分類 | 示し方 |
|---|---|---|---|
| 1 | 検証用の checkout の用意と片付け（表に足した。§2.9 の (1)） | (a) | offline test（一時の git repo）。AC9。AC10 の疎通が用意した checkout で走る |
| 2 | worktree の事前確認（S4-2） | (a) | offline test。AC1。S4-2 の driver の `-DryRun`（V1） |
| 3 | 固定 patch の適用と復元（S4-2） | (a) | offline test。AC2。AC8。V1 |
| 4 | `run-tests.ps1` の起動と実行件数の検査（S4-2） | (a) | offline test（偽の runner）。AC5。実機は、疎通用の scenario の `tests` の case が基盤を通して全 EditMode を走らせる（V3） |
| 5 | run ごとの cache と fixture の root（S4-2、S4-3a） | (a) | offline test。AC4。AC8（S4-2 の cache） |
| 6 | 証拠の退避（S4-2） | (a) | offline test。AC5。AC10 の record |
| 7 | Harness の停止規則（S4-2） | (a) | offline test。AC3 |
| 8 | 窓のある Editor の adapter: 起動、ready、送る `eval` と読む `eval` の区別、終了（S4-3a、S5） | (a) | offline test（偽の CLI と偽の process）AC12、実機 AC10 |
| 9 | Editor を開く前の設定 file の書き込み（S4-3a。S4-2 の cache の設定も同じ操作） | (a) | offline test。AC4。AC8（S4-2 の driver が使う） |
| 10 | batch の Unity の起動（`-executeMethod`）（S4-3a） | (b) | offline test（偽の process で引数・終了コード・log の置き場を見る）。実の起動は S4-3a の Phase C |
| 11 | 補助の process を、driver の 1 回の呼び出しの間だけ起動して終える（S4-3b） | (b) | offline test（起動して保持し、呼び出しの終わりに、基盤が自分の始めた process の終了を確かめる。確かめられなければ戻さない）。`-Only` の続きの実行をまたいで生かす使い方は対象にしない。補助の process は、checkout に触れる子の process を残さない物に限る（基盤は、保持した handle の終了から、全ての子孫の終了を主張しない） |
| 12 | Editor の操作の途中で fault を入れる時点の制御（S4-3b） | (b) | offline test（送る `eval` は完了を待たずに戻り、case の body が次の操作を挟んでから、読む `eval` で状態を待てる） |
| 13 | Editor の再起動（S4-3c） | (b) | offline test（2 回目の起動は 1 回目の終了を確かめた後だけ通る）。AC15 |
| 14 | reload をまたいだ `eval` の再接続と状態の読み直し（S4-3c、S4-4 の「Play と reload をまたぐ観測」） | (b) | offline test（読む `eval` は接続の失敗の後に読み直し、期限で止まる）。実の reload は S4-3c |
| 15 | 外部 command（hook）の実行と記録（S4-3c） | (b) | offline test（引数・終了コード・出力・時刻を record に残す） |
| 16 | 1 つの run の中の複数の publish の名前付け（S4-3d） | (b) | offline test（scenario が保存する証拠は、同じ case の同じ試行の中で同じ名前を上書きしない。2 つ目は拒む） |
| 17 | Play 中の操作（S4-4） | (b) | 12・14 と同じ拡張点。Play の移行中の応答の実測は S4-4 |
| 18 | OS の handle の観測（S4-4） | (b) | 専用の関数は作らない。case の body が任意の観測（script block か 15 の外部 command）を走らせ、結果を証拠に保存できることを offline test で示す |
| 19 | Editor の状態（Dirty、Undo の履歴）を作る操作の記録（S5） | (a) | 8 の一部（送る `eval` は code・時刻・応答を record に残す）。AC12、AC10 |
| 20 | 端末の Player の起動（S4-5） | (c) | 利用者表が「未定。この表に行を足す時に決める」としている（着手の時期は owner が決める） |

S4-3d の「S4-2 の行の操作の全て」は 2〜7 と 9 に当たる。Player の process の起動と観測は、利用者表のとおり利用者（S4-2 の test）が持つ。

### 2.5 受け入れ条件（進める最低条件の詳細。括弧は検証する条件）

- **AC1（N1）事前確認と占有:** 基盤は、scenario が使う全ての checkout の事前確認と lease の取得を終えてから、最初の書き込み（run の root の作成と、checkout の新規の作成を含む）を行う。まだ無い checkout は、作る前に、置き場の path の検査（名前、path 予算、親が基盤の置き場であること、その path に何も無いこと）を済ませる。次のどれでも、どの checkout の file・index・HEAD も run の directory も変えずに、理由を出して止まる。
  - 基盤の印が無い（主の worktree、作業用の worktree、人が過去の commit を調べている detached の worktree を含む）。印はあるが `git rev-parse --git-common-dir` が自分の repo と違う。
  - branch を checkout している。進行中の git の操作がある（worktree の git dir に `rebase-merge`・`rebase-apply`・`BISECT_LOG`・`MERGE_HEAD`・`CHERRY_PICK_HEAD`・`sequencer` のどれかがある）。
  - tracked の変更（index に stage した物を含む）か、ignore されない未追跡の file がある。例外は無い（Unity の起動が書き換えた file だけが残っている時も、戻さずに止まる）。
  - 使用中の記録が残っている（隔離。AC3）。別の run の予約がある（AC4）。別の呼び出しがその checkout の lease を持っている。
  - 2 つの checkout が同じ物である（`git rev-parse --absolute-git-dir` が同じ）。
  - 稼働中の Editor が project lock を持つ（lock の file は消さずに調べる）。root が path 予算を超える。
  - 渡された checkout の HEAD が、指定した対象の commit と違う（渡された checkout の HEAD は基盤が動かさない）。
  - 合格例: 基盤が作った 2 つの checkout が clean で、lease が取れる。不合格例: X は通り Y に未追跡の file がある → X にも run の root にも何も書かずに止まる。同じ checkout を使う 2 つ目の呼び出し → 何も書かずに止まる。
- **AC2（N1、N2、N3）patch と復元:** case の patch は、当てる順に全てをまとめて `git apply --check --whitespace=nowarn` で確かめてから、同じ flag で当てる。patch の途中の失敗、body の例外、起動の失敗のどれでも、基盤は次の順で動く。
  1. その case で基盤が始めた process の終了を確かめる（AC3 の「確かめた」の定義）。Editor が開いたままなら、基盤が終了を頼み、既定の期限まで待つ。
  2. その checkout の project lock を、消さずに調べる。
  3. その時点の `git status` と差分を、run の root のその試行の置き場に保存する。
  4. checkout を case の前の状態へ戻す。tracked の file は HEAD の内容へ戻し、case の開始の時点で無かった ignore されない未追跡の file（事前確認で 0 件を確かめてある）を消す。ignore される file は消さない。
  5. 戻した後に、`git status --porcelain` が case の前と同じ、HEAD が case の前と同じ、index が HEAD と同じ、であることを確かめる。どれかが違えば、以後の case を走らせない。
  - 1 か 2 を確かめられない時は AC3 が優先し、4 を行わない。3 の保存に失敗した時は、1 と 2 が確かめられていれば 4 と 5 を行い、保存の失敗と復元の結果の両方を record に残して、case を失敗にする。
- **AC3（N2、N3）終了の未確認と隔離:**
  - 「終了を確かめた」は、起動の種類ごとに次とする。(i) 基盤が直接起動した process（窓のある Editor、batch の Unity、補助の process）: 保持した handle の signal と終了コードの取得。(ii) `run-tests.ps1` の起動: 子の `pwsh` の handle の signal に加えて、今回の起動の step（AC5 の「今回の物」）があり、その step の `process.exitCode` が整数であるか、Unity を起動しなかった step（`kind` が `preflight`）であること。marker が無い、step が今回の物でない、`process.exitCode` が `null`（起動できなかった時と待ちに失敗した時。key はあるが値が無い）の時は、子が終わっていても確かめたことにしない。
  - 基盤は、checkout を変える前に使用中の記録を置き、AC2 の 5 まで確かめた後にだけ消す。process を起動するたびに、その PID と開始時刻を記録に足す。
  - 終了を確かめられない時と、project lock を稼働中の process が持っている時、基盤はその checkout を戻さず、patch を当てたままにし、次の case を走らせず、process の PID・開始時刻と「戻していない」ことを record に書き、使用中の記録を残したまま非 0 で終わる。driver が強制終了された時、PC が再起動した時、record を書けなかった時も、使用中の記録は残る。
  - 使用中の記録が残っている checkout（隔離）は、lease が空いていても、どの run の事前確認も通らない。
  - 解除は実行の担当の操作で、手順は Harness README に書く: 記録にある process と、command line がその checkout の project を指す process が残っていないことを確かめ（残っていれば制約 12 の確かめ方で止める）、checkout を手で戻し、基盤の解除の入口を呼ぶ。解除の入口は、その checkout の lease を取った上で、lock が保持されておらず、AC1 の clean の条件を満たし、command line がその checkout の project を指す process が 1 つも無い時だけ記録を消す（CIM の `Win32_Process` で読む。読むだけで、基盤は止めない）。子の `pwsh` だけが終わって、その下の Unity が残っている時を、記録の process の不在だけで通さないためである。残っている process を run の物と確かめられない時は、解除せずに owner へ返す。基盤は、自分が終了を確かめていない process の後で checkout を戻さない。
  - 期限の後に基盤が process を止めるのは、scenario がその起動で明示した時だけで、相手は (i) の process とその子孫に限る。(ii) では、この指定を基盤が拒む。止めた終了は `forced` と記録し、自然な終了と分ける。
  - 解除の後の `-Only` の続きの実行は、新しい試行として通り、前の「戻していない」record を指す行を残す。
- **AC4（N3）所有:**
  - scenario の名前、checkout の名前、用途の directory の名前、RunId は、どれも `^[A-Za-z0-9]+([._-][A-Za-z0-9]+)*$` で 32 文字までとし、`.`・`..`・区切り・末尾の `.`・Windows の予約された device の名前（`NUL`、`CON`、`COM1` など。拡張子の付いた物を含み、大小文字を区別しない）を拒む。
  - run の root は、基盤がこの run で作って印を置いた物だけを、書き込みと削除の対象にする。印は「無ければ作る」の 1 回の操作で作り、既にあれば新しい run にしない。印の名前・RunId・対象の commit・checkout の場所・dry-run か本番かが全て同じ時だけ、`-Only` を付けた続きの実行を通し、record に続きであることを残す。続きの実行も run の lease を取る。合格例: 本番で止まった run を同じ RunId と `-Only` で続ける。不合格例: `-DryRun` で作った root を同じ RunId の本番が続ける。`-Only` を付けずに同じ RunId でもう一度走らせる。対象の commit が変わった後に続ける。同じ RunId の 2 つの呼び出しが同時に始まる（片方だけが通る）。
  - 外部 root は `<checkout の drive>\ovr\<scenario の名前>\<RunId>` で、最初に使う case が作り（`-DryRun` では作らない）、基盤の印をその直下に置く。cache や fixture は、その子の用途の directory に作る（製品の cache の root の中に基盤の印を置かない）。
  - 削除の直前に、基盤は次を確かめ直す: 解決した path が決めた親（run の root か外部 root）の下にある、その root の印がこの run の物である、親から対象までのどの成分も reparse point でない。削除は reparse point を辿らない。印の無い directory（作る途中で失敗した物を含む）は消さない。片付けた外部 root を、同じ run で作り直さない。
  - checkout の中の ignore される file への書き込みは、scenario が path を宣言した「管理する file」だけを通す。解決した path が checkout の中に留まり、途中に reparse point が無いことを確かめる。基盤は、書く前の bytes（有無と内容）を run の root に保存し、書いた後の SHA-256 を記録する。寿命は run で、driver の呼び出しをまたいで残り、戻すのは scenario が片付けの case で指した時だけである。続きの実行の時と戻す時に、今の bytes が基盤の書いた物と違えば、上書きせずに止まる。管理する file を書く時、基盤はその checkout に run の予約（scenario の名前、RunId、run の root の場所）を置き、全ての管理する file を戻した時に消す。予約のある checkout は、別の run の事前確認、HEAD の切替、片付けを通さない。同じ run の続きの実行は通す。放置された run の予約は、実行の担当が `scenario-checkout.ps1` の予約の解除で外す。予約の解除は、run の root にある書く前の記録（有無と内容）のとおりに全ての管理する file を戻せた時だけ予約を消す（元から無かった file は消し、あった file は保存した bytes に戻す）。今の bytes が基盤の書いた物と違う時と、書く前の記録そのものが run の root に無い時は、何も変えずに理由を出す。
  - 基盤は project lock の file を消さない。基盤が公開する関数は、PID や名前で process を止める入口を持たない。
- **AC5（N4）成功にしない:**
  - 起動の失敗、ready の期限切れ、`UNITY_TEST_RESULT` の marker の欠け、step が今回の物でない、実行件数が期待と違う（期待を指定しない時は 0 件）、証拠の保存の失敗、復元の後の状態の不一致は、どれも case の record に原因と一緒に残り、driver は非 0 で終わる。
  - 基盤は `run-tests.ps1` に、その試行のために作った出力の directory を `-OutputRoot` で渡す。marker の指す step は、その path がこの directory の下にあり、`projectPath` がその checkout の project、`filter` が今回渡した値である時だけ「今回の物」として受け入れる（Unity を起動しなかった step は process の時刻を持たないので、時刻では決めない）。
  - `-Only` に case の列に無い名前があれば、何も書かずに拒む。選ばれた case が 0 件なら非 0 で終わる。scenario が宣言していない checkout の名前を指す case は、登録の時に拒む。
  - checkout を使う case の record は、起動の直前と終了の後の HEAD の SHA を持つ。対象の commit を指定した run でそれが違えば、その case を失敗として非 0 で終わる。run の最初の record は、各 checkout の root・HEAD・`ProjectVersion.txt` の版を持つ。
  - record は、基盤の操作の成否（どの操作が失敗したか）と、case の body が返した値を別の欄に持つ。`-DryRun` の record は `status` が `dry-run` で、本番の record と同じ run に並ばない（AC4）。
- **AC6（N5）製品を知らない:** 基盤の module（§3 の `UnityProcess`・`ScenarioRun`・`ScenarioCheckout`・`ScenarioUnity`・`ScenarioEditor`・`Scenario` の 6 つ）と `scenario-checkout.ps1` の code に、製品の識別子（`SampleGame`、`OneStarMaker`、`OSMContent`、`artifacts/`、`unity/Assets/`、test の class の名前）が無い。offline test が、構文木の command の名前・型の名前・文字列の literal を走査する（comment と help は見ない）。基盤の record は、基盤の操作の成否と原記録の所在だけを持ち、受け入れ条件の合否の欄を持たない。S4-2 の `driver.jsonl` の `status` は、driver の body が runner の step から写した値で、基盤はそれで止まらず、合否と読まない。driver の exit 0 は製品の合格を意味しない（Harness README に書く）。
- **AC7（N6）runner と wrapper:**
  - `run-tests.ps1` の引数、marker、step の形、終了の待ち方（期限なし）、seam の形が変わらない。seam は、`-ProcessInvoker` の引数（実行 file、引数の列、作業 directory）と、返す object の `Id`・`StartedAt`・`EndedAt`・`DurationMs`・`ExitCode`・`Failure` で、`ExitCode` は終了を確かめた時だけ `[int]`、起動できなかった時と待ちに失敗した時は `$null` のままである。runner の起動は今と同じく窓を作らない。
  - 既存の offline suite（`UnityTestRunner`・`UnityObservation`・`UnityGate`・`Harness`）が、test の側を変えずに通る。期限なしの待ちと、起動できなかった時の `$null` は、新しい offline test（名前で辿れる）が見る。
  - GO 候補 head の全 EditMode 回帰（V3）が、新しい runner で起動から step の保存まで終わる。step が保存されなければ AC7 は満たさない。合否の読み方は V3 と §2.7。
  - `tools/unity-editor.ps1`・`.cmd` は差分に現れない。
- **AC8（S2、N6）S4-2 の driver の互換:**
  - 引数（`-XRoot`・`-YRoot`・`-RunId`・`-Only`・`-DryRun`）、step の名前と順（base `07cc2c97` の `$steps` のまま）、`driver.jsonl` の今ある key（`step`・`root`・`filter`・`patches`・`status`・`total`・`stepPath`・`gitStatusSame`・`gitStatusBefore`・`gitStatusAfter`・`utc`、`cache`・`copy`・`cleanup` の行の key）の名前・型・意味と、record の置き場（`<Y>/TestResults/s42/<RunId>/driver.jsonl`）が同じ。key と引数の追加は許す（`-XRoot`・`-YRoot` を省けば基盤が用意する）。ただし S4-2 の行に `event` の key を足さない（V1 と判定 C が、基盤の行と S4-2 の行を `event` の有無で分けるため）。S4-2 の scenario の名前は `s42` とする（外部 root が `<drive>\ovr\s42\<RunId>` になり、V1 の cache の root の規則と合う）。
  - case の失敗の後の進み方が同じ: (1) runner の step が `failed` でも件数が 1 なら、次の case へ進み、最後まで走れば exit 0。(2) 件数が 1 でない時と marker が無い時は、record を書いてその時点で止まる。(3) 復元の後の状態が前と違う時も止まる。
  - `driver.jsonl` の `filter` は、今と同じく末尾に `$` を付けない値である。`run-tests.ps1` へ渡す `-Filter` は、今と同じく末尾に `$` を付けた値である（付けないと部分一致になり、1 case に絞れない）。offline test は、record の値と、偽の runner が受けた引数の両方を見る。
  - 子の `pwsh` には、driver が渡した環境変数（`S42_RUN_ID`・`S41_RUN_ID`）が届く。基盤は、scenario が起動ごとに渡した名前と値を子の process にだけ設定する。
  - 示し方は 2 つ。(i) offline test: 一時の git repo の、中身の違う 2 つの checkout と偽の runner で、`-DryRun` でない実行を通す。test は、対象の file の bytes を、patch の前、body の中（runner が呼ばれた時点）、復元の後で、test の側が持つ期待の bytes と比べる。X の case の間に Y が、Y の case の間に X が変わらないこと、runner の呼び出しの回数・引数・filter・環境変数、複写と cache の設定と片付けの結果、`-Only` での続きの実行、(1)〜(3) の進み方も見る。(ii) 実の 2 つの検証用の checkout での `-DryRun` の新旧の比較（§5 の V1）。
  - 移した後の driver の code に、git・process の起動・削除の command と、基盤が公開しない関数の名前が無い（AC9 と同じ走査）。複写の対象を directory の名前の順で選ぶ所（今の 118）は S4-2 に固有の code として driver に残し、基盤へ入れない。
  - 前と変わる点を Harness README に列挙する: checkout に基盤の印が要る（渡す前に登録する）、Unity の起動が書き換えた file を driver が先に戻さなくなる（手が入っていれば止まる）、AC1 の他の拒否、RunId の規則と 32 文字の上限、列に無い `-Only` の名前の拒否、同じ RunId で `-DryRun` の後に本番を走らせる手順の拒否、`-Only` を付けない同じ RunId の再実行の拒否、cache の root の場所（run の外部 root の下に変わる）、runner の出力の場所（`stepPath` の指す先が run の root の下に変わる）、片付けの case が cache の設定の file を元へ戻すこと（今は書いたまま残す）、片付けの後の cache の作り直しの拒否。
- **AC9（S1、S5）新しい scenario と checkout の用意:**
  - 疎通用の scenario（§3 の `scenario-editor-smoke.ps1`）の code が、case と保存する証拠の宣言だけで書かれている。offline test が構文木を走査し、`git`・`Start-Process`・`Remove-Item`・`unity-editor`・`Unity.exe` の command、`System.Diagnostics.Process` の型、基盤が公開しない関数の名前が無いことを見る。走査は補助で、書けることの証明は AC15 が持つ。
  - checkout を渡さずに起動すると、基盤が検証用の checkout を対象の commit で用意する。無ければ linked worktree を detached で作り、その worktree の専用の git dir に基盤の印を置く。あれば、AC1 を通した上で HEAD を対象の commit に合わせる。印の無い物、別の repo の物は使わずに止まる。
  - `tools/Harness/scenario-checkout.ps1` が、実行の担当の 4 つの操作を持つ: 登録（手で作った linked worktree に印を置く。AC1 の印以外の条件を満たす時だけ）、片付け（印のある checkout を、AC1 を通した上で `git worktree remove` で外す）、隔離の解除（AC3）、予約の解除（AC4）。隔離の解除と予約の解除は成功の条件が違うので別の操作にし、両方が残る checkout では隔離の解除を先に通す。4 つとも、その checkout の lease を取ってから行う（走っている run の checkout には触れない）。`--force` と `git worktree prune` は使わない。
  - offline test は一時の git repo で、作成、再利用、commit の切替、登録、印の無い directory の拒否、別の repo の worktree の拒否、進行中の操作の拒否、path 予算の超過の拒否、片付け、隔離と解除、予約の解除（元から無かった管理する file を消す時と、書く前の記録が無い時の拒否を含む）を見る。
- **AC10（S3）窓のある Editor の疎通（実機）:** 疎通用の scenario の `editor` の case を、実行の担当が渡した token（`-Token`。実行のたびに新しく作る値）で 1 回走らせると、`driver.jsonl` に次の event が、この順で残る（欄は §3 の「record の契約」）。
  1. `editor-start`: 基盤が起動した process の PID と開始時刻。引数に `-batchmode` が無い。
  2. `editor-ready`: 読む `eval` が通った時刻と、応答の PID。PID が 1 と同じ。
  3. `eval`（読む）: 呼び出しごとの印と PID を含む原応答。Unity の版と project の path を返す。
  4. `eval`（送る）: `SessionState` に token を置く。この case の中で送る `eval` は 1 回。
  5. `eval`（読む）: `SessionState` から読んだ値。原応答の値が token と完全に一致する。
  6. `eval`（終了の要求）と `process-exit`: 1 の PID の handle の signal、終了コードが 0、`forced` が false。
  7. `restore`: 復元の後の確認（AC2 の 5）が全て真。
  - 判定の担当は、token、PID の一致、event の順、終了コードが 0 であること（`forced` が false でも 0 でなければ AC10 を満たさない）、Unity の版が `ProjectVersion.txt` と同じこと、project の path が用意した checkout の `unity/` と（区切りと大小文字を正規化した絶対 path で）同じことを、原記録で確かめる。
- **AC11（S4）分類:** §2.4 の全ての行について、(a) は示し方の欄の offline test（名前で辿れる）か実機の検証経路が、(b) は offline test があり、通る。
- **AC12（S4、N3）Editor の adapter（offline）:** 偽の CLI と偽の process で次を見る。test は、基盤が返した成否だけでなく、偽の CLI が受けた呼び出しの回数と中身を直接見る。
  - 基盤が送る全ての `eval`（送る、読む、終了の要求）の code は、先頭で Editor 自身の PID を基盤が保持した PID と比べ、違えば何もせずに「相手が違う」を返す。ready と「読めた」は、PID が合った応答だけで決める。別の PID が応答した時は起動の失敗にし、送る `eval` を送らない。
  - scenario の送る `eval` は、基盤が送り直さない。偽の CLI が「実行してから応答が timeout」の時も、「実行せずに error」の時も、呼び出しは 1 回である。終了の要求だけは、何度届いても結果が同じなので、handle が signal になるか期限が来るまで基盤が送り直してよい。
  - 読む `eval` は一時的な失敗の後に読み直し、印の合わない応答（前の呼び出しの応答を含む）を成功にしない。
  - ready は `status` の ready だけで決めず、読む `eval` が通るまで待つ。起動の後に process が先に終われば、ready を待たずに起動の失敗にする。期限で止まる。
  - lock を稼働中の Editor が持つ時は起動しない。持つ process のいない lock の残骸は、消さずにそのまま起動する。再起動は前の process の終了を確かめた後だけ通る。
  - 起動の引数は `-projectPath <checkout の project>` と `-logFile <その試行の置き場の editor.log>` で、`-batchmode` を付けず、窓を作る。
  - 送る `eval` の code・時刻・応答が record に残る。
- **AC13（S6）入口の文書:** [Harness README](../../tools/Harness/README.md) に、基盤の契約（前提と N1〜N5 の形）、scenario の書き方（最小の例として疎通用の scenario を指す）、record の契約と証拠の置き場、checkout の登録・片付け・隔離の解除・予約の解除の手順（AC3、AC4、AC9）、S4-2 の driver で前と変わる点（AC8）、driver の exit 0 の意味（AC6）がある。[検証経路の成立](../../.agents/skills/osm-workflow/references/phases-and-handoff.md#検証経路の成立) に、実機の検証は基盤の上に case を書くことを 1 文で足す。
- **AC14 機械検査:** `tools/contract-audit.ps1` と `tools/docs-audit.ps1` が通る。新しい offline suite が CI の `harness-tests` job に入っている。
- **AC15（S1）後続の形の複合 scenario（offline）:** S4-3a の E 系列を模した短い scenario を、offline test の fixture として書き、偽の process・偽の CLI・一時の git repo で最後まで通す。scenario は基盤の公開する関数だけを使い（AC9 の走査を通る）、次を 1 本の中で組み合わせる: 2 つの checkout（X と Y）の宣言、X での偽の batch の起動と結果の保存、X から run の fixture の directory への受け渡し、Y の管理する設定の file、Y への固定 patch、偽の Editor の起動・送る `eval`・状態の待ち・読む `eval`、Editor の再起動、証拠の退避、復元、同じ RunId の `-Only` での続きの実行。加えて、run の間の所有を 1 つの列で通す: run A が Y に管理する file を残して呼び出しを終える → 別の RunId の run B が同じ Y を使おうとして、何も変えずに拒まれる → A が `-Only` で続きを走らせる → A の片付けの case が管理する file を戻す → B が通る。使用中の記録も見る: 偽の process を残したまま driver の呼び出しを打ち切り（lease は離れる）、次の呼び出しが隔離で拒まれることと、解除の入口が条件を満たす時だけ通ることを確かめる。test は、各 case の後の X と Y の file の bytes、偽の process と偽の CLI の呼び出しの列、証拠の file、record を直接見る。製品の publish・Play・Player の build は行わない。新しい記法は作らない。

不変条件ごとの失敗経路を含む受け入れ条件: N1 は AC1・AC9、N2 は AC2・AC3、N3 は AC3・AC4・AC12、N4 は AC5、N5 は AC6、N6 は AC7・AC8。

### 2.6 ここでは答えない問いと所有する後続

| 問い | 所有 |
|---|---|
| S4-2 の driver の実の実行（Unity の起動を伴う case）が基盤の上で通るか | S4-3d（S4-2 の driver を走らせ直す行） |
| batch の `-executeMethod` の実の起動、Play の入口を通る Editor の操作、E 系列の case | S4-3a（`LOCAL_CONTENT_ORIGIN`） |
| 補助の process と fault の実の使い方。補助の process を `-Only` の続きの実行をまたいで生かすこと | S4-3b |
| reload をまたぐ `eval` の実測、hook | S4-3c |
| Play 中の操作と OS の handle の観測の実測 | S4-4 |
| `run-tests.ps1` の終了の待ちに期限を入れるか、子の Unity を基盤が止められるようにするか | 所有なし（Phase D で program §10 の U11 へ足す。要る時に owner が行を置く） |
| 人や任意の git の process との排他（基盤を使う run 同士の排他はこのスライスが持つ） | 所有なし（同上） |
| 隔離された checkout の回復を基盤が行うこと（今は実行の担当の手順と、基盤の解除の入口） | 所有なし（同上） |
| Player の process を基盤が直接起動・監視すること（今は利用者の test が持つ） | 所有なし（同上。使う行が §9.1 の表に足す） |
| 端末の Player の起動 | S4-5 |

### 2.7 スライス分割の判断、判定定義、停止規則

- **分割の判断:** 分割しない。検討した契機は「独立した変更理由の混在」で、(i) runner からの process の起動の切り出し、(ii) 基盤の中核と S4-2 の driver の移し替え、(iii) 窓のある Editor の adapter、の 3 つがある。分けない理由: (ii) と (iii) は (i) の module を使い、(i) だけを先に出しても使い手が runner しか無く、切り出した形が合っているかを示せない。実機でしか確かめられない経路は (iii) の 1 つで、Phase A で疎通を確かめた（§5）。S4-3a の凍結はこの行の A3、Phase B はこの行の merge を待つので、2 つの PR に分けると待ちが延びる。Phase B の commit は (i)(ii)(iii) の順に分け、(i) の commit だけで既存の offline suite が通る形にする。
- **判定定義:** GO は、AC1〜AC15 を全て満たした時。NO-GO は、どれかを満たさず、B 適応で直せない時。CONDITIONAL ACCEPT は使わない。
- **全 EditMode 回帰（V3）の読み方:** AC7 が求めるのは、(i) step が保存されること、(ii) step の起動の条件が全 EditMode の物であること、(iii) test の集合と結果が期待集合と合うこと、の 3 つである（細部は §5 の V3）。期待集合と違ってよいのは次の 1 種類だけ: 期待集合で `Passed` の test が `Failed` で、XML の失敗の message が `Access to the path`・`is denied`・`Directory.Move` の 3 つを含む物（content の build の staging の移動。owner が受容済みの、差分の外の事象。test が例外を受けて assert するので、`Directory.Move` は `stack-trace` ではなく message の中の例外の文に入る。この PC に残る XML の、この事象の `Failed` 14 件は全てこの形だった）。該当した test の名前と件数を判定の記録に残す。`-nographics` の VFX の `Inconclusive` は期待集合に入っているので、例外ではなく一致の対象である。それ以外の `Failed`、期待に無い `Inconclusive`、compile error は、AC7 の不成立である。合否の正は XML の集合の一致で、step の `status` ではない。runner は `Inconclusive` が 1 件でもあれば step を `failed` にし、`failure` に行を足す（`UnityTestResult.psm1` の 95）ので、期待集合と一致した実行でも step の `failure` は空にならない（`Inconclusive` だけなら XML の root の result は `Passed` で、root の result の行は出ない）。step の `failure` に許す行は次だけである: `Inconclusive のテストがあります`（`Inconclusive` の集合が期待集合と同じ時）、`失敗したテストがあります` と `XML root result が Passed ではありません`（`Failed` が上の 1 種類だけの時）、`Unity process が正常終了しませんでした: 2`（test の集合と結果が期待集合と合う時。Unity は同じ結果の集合でも終了コード 0 と 2 のどちらも返す。この PC に残る filter なしの全 EditMode の、`Failed` 0・`Inconclusive` 25 の別々の実行 5 回で、0 が 2 回、2 が 3 回だった。期待集合の step の終了コードとは比べない）。それ以外の行（XML を評価できない、件数の矛盾、成功した test が無い、終了コードを取得できない、2 以外の終了コード）と、`compileErrors` が空でないことは、AC7 の不成立である。2 以外の終了コードの行の例外は、下の stall の時だけである。
- **終了の native stall（V3）:** XML の完成の後に Unity が残る時は、Harness README の停止規則の既知の事象として、実行の担当が制約 12 の確かめ方で止め、note を添える。この時も `run-tests.ps1` が step を保存していて、XML が完成している（root の件数と test の数が合う）なら、AC7 の (i) は満たす。止めたことで step の `failure` に 2 以外の終了コードの行が加わった時は、note がある時に限り、その行を許す（test の失敗の行とは分けて読む。CIM の `Terminate` は終了コード 0 を作るので、行が加わらないこともある）。止めた終了は自然な終了の証拠ではなく、AC7 は自然な終了を主張しない。step が保存されなかった時は AC7 を満たさず、取り直しは 1 回までとする。2 回とも保存されなければ、判定を保留して owner へ返す（原因を調べない）。
- **停止規則:** 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- **A3 後の例外承認:** なし。
- **決定記録:** なし。

### 2.8 本文へ転記した実装制約

1. Unity 側の C# を変えない。変えるのは `tools/` の PowerShell、`.github/workflows/offline-checks.yml`、文書だけである。
2. 基盤の offline test は実の Unity も Unity CLI も起動しない。process と CLI は seam で差し替える。待ちは seam（偽の時計と偽の待機）で進め、test の中で実時間を待たない（`Start-Sleep` を test に書かない）。実の process を使う test は、標準入力を待つ子の `pwsh` のように、合図で終わる物に限る。
3. offline test は、基盤が返した成否の値だけを assert しない。実の file の bytes、git の状態、偽の process と偽の CLI が受けた呼び出しの回数と中身を直接見る。
4. .NET の file API（`[IO.File]` など）には絶対 path を渡す（PowerShell の `Set-Location` を .NET は見ない）。
5. PowerShell の関数は Verb-Noun の名前にする。組み込みの別名（`cli`、`rd`、`rm` など）と同じ名前の関数は、別名の方が走る。
6. Unity の process は `ProcessStartInfo.ArgumentList` で起動し、起動の直後に PID と開始時刻を固定する。`Start-Process` と文字列の連結の引数を使わない。窓の有無は起動の種類で決める: runner と batch は窓を作らず、窓のある Editor は `-batchmode` を付けずに窓を作る。
7. 基盤が process を止める時は、保持している `Process` の object に対して `Kill(entireProcessTree: true)` で行う。PID や名前で探さない。`Get-Process`・`Stop-Process` を使わない。
8. `run-tests.ps1` は今と同じく子の `pwsh` として起動し、その checkout の `tools/run-tests.ps1` を使う。dot-source しない。基盤が持つのは子の `pwsh` の handle で、その下の Unity の handle は持たない。この起動では基盤は process を止めない。子の終了は Unity の終了を示さないので、終了の確認は AC3 の (ii) による。
9. project lock を調べる関数は 2 つに分ける: 消さずに調べる物（基盤はこれだけを使う）と、残骸を片付ける物（`run-tests.ps1` だけが、今と同じ所で使う）。
10. 検証用の checkout の Editor は、その checkout の `tools/unity-editor.cmd` で操作する。`eval` の code は UTF-8 の Base64 の 1 引数で渡す。別の接続の実装を作らない。窓のある Editor の起動は `Unity.exe` の直接の起動だけとし、`unity open` を代わりに使わない（保持する handle が Editor の物でなくなる）。
11. 窓のある Editor の終了は、PID の照合を先頭に持つ `UnityEditor.EditorApplication.Exit(0)` を送る `eval` で頼み、保持した handle で確かめる。応答の error を失敗と読まない。
12. 実行の担当が Unity の process を止めてよいのは、次を CIM（`Win32_Process`）で確かめた時だけである: command line がその checkout の project を指す。作成の時刻が、record の開始時刻より後である。その上で、PID が record の Editor・batch の PID そのものであるか、親の PID が record の子の `pwsh`（`run-tests.ps1` の起動）であるか、record の子の `pwsh` が既に残っていない（その下の Unity だけが残った時）。前提どおり、人は検証用の checkout の project を開かないので、条件に合う process は run の Unity である。確かめられなければ止めない。止めた時は、時刻・PID・確かめた値を書いた note を証拠に添える。
13. `git clean` に `-x` を付けない（ignore される `Library/`・`artifacts/`・`TestResults/` を消さない）。`git worktree remove` に `--force` を付けない。`git worktree prune` を使わない。
14. `tools/Harness/README.md` の native stall の停止規則を変えない。基盤は原因の調査をしない。
15. 判定 C の期待値・合成 fixture・照合 script を git に入れない。基盤の offline test は実装から導ける fixture だけを使う。
16. `record` 型の禁止と `#nullable enable` は Unity 側の C# の規則で、このスライスは C# を足さない。

### 2.9 owner が確かめた項目と、担当が決めた項目

**owner が確かめた（2026-11-25。どちらも範囲を変える項目で、主担当の案のとおりにする、と owner が決めた。外部計画レビューの 2 件もどちらも支持した）:**

1. **program §9.1 の利用者表に「検証用の worktree の用意と片付け」を足す。** 表の S4-2 の行は「worktree の事前確認」だけで、作ることは入っていない。今は実行の担当が毎回 worktree を手で作り、場所・path 予算・対象の commit・別の `Library` を考えている。目的の「検証環境の構築方法を毎回考えなくて済む」は、これを基盤が持たないと成り立たないので、(a) として足した（§2.4 の 1、AC9）。この branch の program の表も直してある。足さない時は、§2.4 の 1 と AC9 の用意の部分、UC2 の対象の commit の入力と UC4 を外し、driver は登録した checkout の root を受け取るだけにする。
2. **program §8 の S4-V の行の「Unity を起動する確認は疎通 1 回」に、全 EditMode 回帰を足す。** `run-tests.ps1` の process の起動を module へ切り出す（§3）ので、実装変更のスライスの標準どおり、GO 候補 head の全 EditMode 回帰を判定必須にした（AC7）。この 1 回は基盤を通して走らせ、基盤からの `run-tests.ps1` の実の起動（§2.4 の 4）も同時に示す。切り出さない時は、runner と基盤に同じ起動の code が 2 つ残る。この branch の program の行も直してある。

**担当が決めた（覆す時だけ改める）:**

- 検証用の checkout は、基盤の印（worktree の専用の git dir）で見分ける。印は基盤が作る時か、実行の担当の登録の操作で置く。渡された checkout にも印を求める（初稿は印を置かない案、改訂前は基盤が作った物にだけ置く案だった。A2 の IB-4・AG-1 と外部計画レビューの X1 で改めた）。
- Unity の起動が書き換える file を事前確認で戻す例外は置かない（改訂前は一覧の file を持つ案だった。X1）。基盤の中では、case の後の復元が毎回 HEAD へ戻すので、次の case は clean から始まる。基盤の外で Unity を開いた checkout は、実行の担当が戻してから使う。
- 基盤を使う run 同士の排他は lease で持ち、終了の未確認の後は隔離する（X2）。人や任意の git の process との排他は前提に置き、保証しない。
- 検証用の checkout の既定の置き場は `<主の worktree の親>\osm-v\<scenario の名前>-<checkout の名前>` にした（短い path。`Library` を run の間で使い回す）。引数で変えられる。
- 期限の後に基盤が自分の始めた process を止める道は、scenario が起動ごとに明示した時だけ開く。既定は止めずに隔離する。
- 窓のある Editor の起動は `Unity.exe` の直接の起動だけにし、`unity open` への切り替えは置かない（X3）。直接の起動が成り立つことは Phase A で確かめた（§5）。
- risk は normal のままにした。製品の code・公開 API・asmdef・Addressables を変えない。破壊的な操作（checkout の復元、directory の削除、process の停止）は、どれも Unity なしの offline test で境界を見られる（AC1〜AC4）。

### 2.10 未決事項

なし。

## 3. 責務マップ

新規は ＋、変更は △。行数は予想。

| file | 責務（一文） | 種別 | 所有する状態・寿命 | 依存 | 公開面 | テスト境界 | 配置の理由 | 行数 |
|---|---|---|---|---|---|---|---|---|
| ＋ `tools/Harness/UnityProcess.psm1` | Unity の実行 file を `ProjectVersion.txt` から決め（要求の版と実の file の版を別に返す）、project lock を調べ、process を起動して identity（PID・開始時刻・handle）を固定し、終了を待つ | infrastructure（process の I/O） | なし。起動した process の object は呼び出し側が持つ | なし（.NET だけ） | 実行 file の解決、lock を消さずに調べる、lock の残骸を片付ける、起動（窓の有無を引数で受ける）、待機（期限なし・期限あり）、保持した object への停止 | 偽の starter で offline。実の子の `pwsh` で handle の挙動 | runner と基盤が同じ起動を使うため、どちらにも属さない Harness の module にする | 0 → 約 160 |
| △ `tools/run-tests.ps1` | Unity の test を実行し、XML・log・step を保存する（今と同じ） | orchestration | 変わらない | `UnityProcess.psm1` を足す | 変わらない（AC7 の seam） | 既存の offline suite を変えずに通す | `Invoke-UnityTestProcess` と `Test-UnityProjectLock` は名前を残し、中身を module の呼び出しにする。`Invoke-UnityTestRun` の中の実行 file の解決（228-240）も module の呼び出しにする。lock の残骸の片付けは今と同じ所で行う | 312 → 約 285 |
| ＋ `tools/Harness/ScenarioRun.psm1` | run の永続する状態を所有する: 名前と RunId の検査、root と外部 root の作成と印、run の lease、続きの実行の照合、試行の番号、record（JSONL）、証拠の保存、管理する file の保存と戻し、印のある範囲の削除 | 永続化と所有 | file だけ（run の root、外部 root、印）。driver の呼び出しをまたぐ。lease の handle だけは呼び出しの間 | なし | run の開始（新規・続き）、record の追記、証拠の保存、管理する file、外部の directory の作成と削除 | 一時の directory で offline | 「何を書いてよいか・消してよいか」の判断を 1 か所に集める | 0 → 約 300 |
| ＋ `tools/Harness/ScenarioCheckout.psm1` | 検証用の checkout を扱う: 用意（作成・再利用・commit の切替）、登録、片付け、事前確認、lease、使用中の記録と隔離の解除、run の予約、patch の確認と適用、復元、前後の状態の照合 | git の I/O | なし（状態は git と working tree。基盤の印・使用中の記録・予約・lease の file は worktree の git dir） | `PathBudget.psm1`、`UnityProcess.psm1`（lock を消さずに調べる関数だけ） | 用意、登録、片付け、事前確認、lease、隔離、解除、patch の適用、復元、状態の取得 | 一時の git repo と linked worktree で offline | git の扱いが変わる理由は run の所有とも Unity の操作とも別 | 0 → 約 330 |
| ＋ `tools/Harness/ScenarioUnity.psm1` | Unity と外部の process の起動: `run-tests.ps1` の子の起動（環境変数を子にだけ渡す）と marker・step・件数の検査、batch の `-executeMethod`、任意の外部 command（終わるまで待つ）、補助の process（起動して保持する） | process の I/O | なし。起動した process の object を返す | `UnityProcess.psm1`。record と証拠の書き手は引数で受け取る | test の起動、batch の起動、外部 command の実行、補助の process の起動 | 偽の runner と偽の process で offline | Editor の対話の session と変わる理由が違う | 0 → 約 190 |
| ＋ `tools/Harness/ScenarioEditor.psm1` | 窓のある Editor の session: 起動、ready、送る `eval`、読む `eval`、状態の待ち、終了の要求、PID の照合と印の埋め込み | Editor の I/O | session の object（起動から終了の確認まで）。process の object を持ち、呼び出し側へ返す | `UnityProcess.psm1`、その checkout の `tools/unity-editor.cmd`。record と証拠の書き手は引数で受け取る | session の開始・終了、送る、読む、待つ | 偽の CLI と偽の process、偽の時計で offline。実機は AC10 | S4-3c・S4-4・S5 で伸びるのはここだけなので、他の起動と分ける | 0 → 約 250 |
| ＋ `tools/Harness/Scenario.psm1` | driver が import する唯一の入口。case の列の登録と実行（`-Only`、`-DryRun`）、全ての checkout の事前確認と lease を先に済ませること、case ごとの順序（AC2）の保証、基盤が始めた process の台帳（handle）と、driver の呼び出しの終わりの終了の確認 | orchestration | run の object（1 回の driver の呼び出しの間）: case の列と、process の handle の台帳 | 上の 4 つ | run の開始、case の登録、実行と、case の body が使う関数だけ（証拠の保存、送る・読む・待つ、Editor の session の開始と終了、管理する file、外部の directory、test・batch・外部 command・補助の process の起動、record の追記）。checkout の復元、patch、process の停止、削除、隔離の解除の関数は公開しない | 偽の body と一時の git repo で offline | 順序の保証と handle の台帳は、どの I/O の module にも置けない。永続する状態（`ScenarioRun`）と寿命が違うので分ける | 0 → 約 240 |
| ＋ `tools/Harness/scenario-checkout.ps1` | 実行の担当の入口: 検証用の checkout の登録、片付け、隔離の解除、予約の解除 | CLI（薄い） | — | `ScenarioCheckout.psm1` | `register`・`remove`・`release`・`unreserve` と、対象の path | offline test は module の関数を直接見る。CLI は引数の受け渡しだけ | driver の引数に混ぜると、どの driver からも同じ操作を呼べる形にならない | 0 → 約 50 |
| ＋ `tools/Harness/scenario-editor-smoke.ps1` | 疎通用の最小の scenario。`editor` の case（AC10）と `tests` の case（基盤を通した `run-tests.ps1` の起動。filter を省けば全 EditMode。V3）。scenario の書き方の例を兼ねる | scenario | — | `Scenario.psm1` | `-RunId`、`-Commit`、`-Only`、`-DryRun`、`-Token`、`-TestFilter` | AC9 の走査。`-DryRun` | 基盤の例なので Harness に置く | 0 → 約 80 |
| △ `tools/s42-player-overlay.ps1` | S4-2 の case の列と、S4-2 に固有の step（複写、cache の設定、片付け） | scenario | — | `Scenario.psm1` | 引数の追加だけ（AC8） | AC8 | 場所を変えない（README と program が指している） | 186 → 約 130 |
| ＋ `tools/Harness/tests/Scenario.Tests.ps1` | run・checkout・case の順序・S4-2 の driver の互換・複合 scenario・走査（AC1〜AC6、AC8、AC9、AC15、AC11 の一部） | offline test | 一時の directory | 基盤 | `-ResultPath`、`-Filter`（既存の suite と同じ） | — | 既存の suite と同じ場所と形 | 0 → 約 1000 |
| ＋ `tools/Harness/tests/ScenarioUnity.Tests.ps1` | `UnityProcess`・`ScenarioUnity`・`ScenarioEditor`（AC3 の process の側、AC5 の起動の側、AC7 の seam、AC11 の一部、AC12） | offline test | 一時の directory | 基盤 | 同上 | — | 同上 | 0 → 約 550 |
| △ `.github/workflows/offline-checks.yml` | 上の 2 本を `harness-tests` job に足す | CI | — | — | — | — | — | +4 |
| △ `tools/Harness/README.md` | 「実機の検証の scenario」の節（AC13 の内容） | 公開文書 | — | — | — | docs-audit | Harness の入口の文書 | 148 → 約 220 |
| △ `.agents/skills/osm-workflow/references/phases-and-handoff.md` | 「検証経路の成立」に 1 文 | 規約 | — | — | — | docs-audit | 実機の検証を計画する担当が必ず読む節 | +1 |

- **行数の警報:** 新規の module はどれも 500 行未満。`Scenario.Tests.ps1` は 500 行を超える。既存の suite（`Harness.Tests.ps1` は約 700 行）と同じく case を並べた表だが、1000 行に近づくので、checkout の側（AC1・AC9 と AC3 の隔離）を `ScenarioCheckout.Tests.ps1` に分けてよい（分けたら CI にも足す。B 適応）。`ScenarioCheckout.psm1` は責務が多いが、どれも「検証用の checkout の git dir と working tree に触る」1 つの変わる理由で、分けると印・lease・使用中の記録・予約の file の場所を 2 つの module が知ることになるので分けない。
- **依存の向き:** `Scenario` → `ScenarioEditor`・`ScenarioUnity`・`ScenarioCheckout`・`ScenarioRun` → `UnityProcess`・`PathBudget`。`ScenarioEditor` と `ScenarioUnity` は `ScenarioRun` を import しない。逆向きは無い。基盤は Harness の H1 の store（`RecordStore.psm1`、`harness.ps1`）にも `tools/Artifacts` にも依存しない。
- **状態の所有:** 永続する物（root、印、record、証拠、管理する file の保存）は `ScenarioRun`。checkout に付く物（基盤の印、使用中の記録、予約、lease の file）は worktree の git dir にあり、`ScenarioCheckout` だけが読み書きする。driver の呼び出しの間だけの物（case の列、process の handle）は `Scenario` の run の object。Editor の session は `ScenarioEditor` の object で、その process は `Scenario` の台帳に載る。
- **基盤が決めて、driver に書かせない物:** run の root の既定（driver のある checkout の `TestResults/scenario/<名前>/<RunId>`。S4-2 は互換のため `<Y>/TestResults/s42/<RunId>` を渡す）、外部 root（`<checkout の drive>\ovr\<名前>\<RunId>`）、record の file 名（`driver.jsonl`）、基盤が自分で保存する証拠の置き場（run の root の `base/<case>/<試行の番号>/`: case の前・戻す前・戻した後の `git status --porcelain`・HEAD・index の差分の原出力、戻す前の差分、`eval` の code と原応答、Editor と batch の log、`run-tests.ps1` の出力の directory）、scenario が保存する証拠の置き場（`evidence/<case>/<試行の番号>/<名前>`）。
- **record の契約（判定の担当が読む欄。Phase B は欄を足してよいが、ここの名前と意味を変えない）:** 基盤が書く行は `event`・`case`・`attempt`・`utc` を持つ。`run-start`（`scenario`・`runId`・`mode`・`commit`・`continued`、checkout ごとの `root`・`head`・`unityVersion`）。`case-start`（`checkout`・`head`・`patches`）。`editor-start`（`pid`・`startedUtc`・`argv`）。`editor-ready`（`pid`・`evalPid`・`callId`）。`eval`（`kind` は `read`・`send`・`exit`、`callId`・`sentUtc`・`evalPid`・`answered`・`responsePath`）。`process-exit`（`pid`・`kind`・`signaled`・`exitCode`・`forced`）。`test-run`（`pid`・`startedUtc`・`filter`・`outputRoot`・`stepPath`・`stepIsCurrent`・`total`・`status`）。`restore`（`gitStatusSame`・`headSame`・`indexClean`）。`case-end`（`head`、`base` に失敗した操作の名前か `ok`、`body` に case の body が返した値）。`in-use`（`checkout`・`placed` か `cleared`）。`quarantine`（`checkout`・`pid`・`startedUtc`）。真偽の欄は基盤の申告で、判定の担当は `base/` の原出力で確かめ直す（§5）。S4-2 の `driver.jsonl` は、これらに加えて今ある形の行（AC8）を持つ。
- **窓のある Editor の `eval` の印と PID:** 基盤が送る全ての `eval` は、先頭で Editor 自身の PID を保持した PID と比べる。読む `eval` は、呼び出しごとの印（GUID）を code に埋め、応答にその印と合った PID がある時だけ読めたとする。CLI の終了コードと文言の分類に頼らない。

## 4. 実装計画

- 変更対象: §3 の表のとおり。
- 順序（commit を分ける）:
  1. `UnityProcess.psm1` を作り、`run-tests.ps1` の起動・lock・実行 file の解決を置き換える。既存の offline suite を変えずに通す。
  2. `ScenarioRun.psm1` → `ScenarioCheckout.psm1` と `scenario-checkout.ps1` → `Scenario.psm1` と `Scenario.Tests.ps1`。
  3. `ScenarioUnity.psm1` と、`tools/s42-player-overlay.ps1` の移し替え（互換の offline test を先に、今の driver に対して書いてから移す）。
  4. `ScenarioEditor.psm1`、`scenario-editor-smoke.ps1`、`ScenarioUnity.Tests.ps1`、複合 scenario（AC15）。
  5. CI、Harness README、workflow の 1 文。
- Phase B から Phase A へ差し戻す条件: N1〜N5 を守るのに §3 に無い状態・依存・公開の関数が要る。`run-tests.ps1` の引数・marker・step の形・seam の形を変えないと切り出せない。S4-2 の driver の今ある key、step の順、case の失敗の後の進み方を変えないと移せない。program §9.1 の表に無い操作が要る。窓のある Editor を `Unity.exe` の直接の起動で操作できない（`unity open` へ替えない）。§3 の record の契約の名前か意味を変える必要がある。
- B 適応の範囲: 関数の名前と引数の細部、record に足す欄、偽の process の形、test の file の分け方。どれも実装結果に記録する。
- 対象外を維持する方法: `unity/Assets/` と `tools/unity-editor.ps1`・`.cmd` が差分に現れないことを `git diff --stat` で確かめる。

## 5. テストとレビュー計画

- 単体テスト: `Scenario.Tests.ps1` と `ScenarioUnity.Tests.ps1`（§3）。各 AC と §2.4 の (a)(b) の行に、名前で辿れる case を 1 つ以上置く。制約 3 のとおり、実の file・git の状態・偽の呼び出しの列を直接見る。次の欠陥を入れた時に落ちる case を必ず持つ（括弧は見る物）: patch を当てない（body の中の bytes）、body を呼ばない（呼び出しの回数）、復元しない（復元の後の bytes と status）、古い step を受け入れる（偽の runner が前の step を指す）、別の Editor の応答を受け入れる（偽の CLI が別の PID を返す）、送る `eval` を timeout の後に送り直す（呼び出しの回数）、終了の未確認の後に戻す（偽の process が終わらない時の bytes と使用中の記録）、証拠の保存の失敗を成功にする（書けない置き場）。
- 差し戻し中の起点: 上の 2 本と、既存の `UnityTestRunner.Tests.ps1`・`UnityObservation.Tests.ps1`（どれも Unity を起動しない）。Unity の `-Filter` は使わない。
- 判定必須テスト（GO 候補 head）:
  1. offline: `Scenario.Tests.ps1`、`ScenarioUnity.Tests.ps1`、既存の `Harness.Tests.ps1`・`UnityGate.Tests.ps1`・`UnityObservation.Tests.ps1`・`UnityTestRunner.Tests.ps1`、`contract-audit.tests.ps1`。
  2. `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。
  3. S4-2 の driver の `-DryRun` の新旧の比較（V1）。
  4. 窓のある Editor の疎通 1 回（V2）。
  5. 全 EditMode 回帰 1 回（V3。基盤を通して走らせる）。
- 全 EditMode 回帰の適用除外: なし（runner を変えるので行う）。
- 統合・Unity テスト: 足さない（Unity 側の code を変えない）。
- 操作・実行時・目視条件の検証経路:
  - **V1（AC8 の公開の互換）S4-2 の driver の `-DryRun` の新旧の比較。** 互換の中身（patch・body・復元・runner・複写）は AC8 の offline test が見る。V1 が見るのは、実の checkout での公開の形だけである。担当: 動作確認は Phase B の session、判定は判定 C の実行担当。環境: GO 候補 head の 2 つの検証用の checkout（`git worktree add --detach` で作り、`scenario-checkout.ps1 register` で登録する。旧の driver は root を受け取るだけなので、同じ 2 つを渡す。Unity を起動しないので `Library` は要らない）。操作: (1) `git show 07cc2c97:tools/s42-player-overlay.ps1` を repo の外の作業用の directory に書き出し、`-XRoot <X> -YRoot <Y> -RunId <旧の id> -DryRun` で走らせる。(2) GO 候補 head の `tools/s42-player-overlay.ps1` を同じ `-XRoot`・`-YRoot` と別の RunId、`-DryRun` で走らせる。観測と合否: 2 つの `driver.jsonl` の、今ある key を持つ行（旧の driver が書く行。`step` の key を持ち `event` の key を持たない行）の数と順、各行の今ある key の名前・型・値が同じ。比べる前に外してよい値は 3 つだけで、それぞれ独立に確かめてから外す: `utc`（どちらも ISO 8601 の時刻）、RunId を含む値（それぞれの実行の RunId を含む）、cache の root の path（旧は `<drive>\s42c\<RunId>`、新は `<drive>\ovr\s42\<RunId>\` の下、の規則に合う）。新しい側の追加の key と、基盤が足す行は比べない。実行の前後で X と Y の `git status --porcelain` が空で、HEAD が同じ（実行の担当が driver の外で取った原出力で見る。基盤の `gitStatusSame` の値だけで決めない）。保存する証拠: 2 つの `driver.jsonl`、2 つの実行の出力、前後の `git status` と HEAD の原出力。
  - **V2（AC10）窓のある Editor の疎通。** 担当: 動作確認は Phase B の session、判定は判定 C の実行担当。環境: Windows の開発の PC、`ProjectVersion.txt` の版の Unity。操作: 実行の担当が新しい token を作り（GUID）、`pwsh tools/Harness/scenario-editor-smoke.ps1 -RunId <id> -Only editor -Token <token>` を 1 回走らせる（checkout は基盤が GO 候補 head で用意する。初回は import に時間がかかるので、ready の期限の既定は 30 分にする）。観測と合否: AC10 の event の列と、判定の担当の確かめ。判定の担当は、record の真偽の欄ではなく原記録で確かめる: `eval` の原応答の file にある印が record の `callId` と同じこと、原応答の PID と token、`base/` の case の前と戻した後の `git status`・HEAD・index の原出力が同じこと、`process-exit` の終了コード。保存する証拠: run の root の全体（`driver.jsonl`、`base/` の `eval` の原応答と `editor.log`、戻す前後の `git status`）と、実行の担当が渡した token を書いた note。終了が期限までに確かめられない時は、record の「終了の未確認」と隔離をそのまま証拠にし、Harness README の停止規則と制約 12 に従う（原因を調べない）。その時 AC10 は満たさない。
  - **V3（AC7、§2.4 の 4）全 EditMode 回帰。** 担当: 判定 C の実行担当（動作確認では行わない。Unity の test は Phase C）。操作: V2 と同じ RunId で `pwsh tools/Harness/scenario-editor-smoke.ps1 -RunId <id> -Only tests`（filter を渡さない。V2 と同じ checkout を使う）。観測と合否は次の 3 つで、§2.7 の読み方に従う。
    - step が保存されている。`driver.jsonl` の `test-run` の `stepPath` の step の原本があり、その path が `outputRoot` の下にある（`stepIsCurrent` の値だけで決めず、path を見る）。
    - step の起動の条件が全 EditMode の物で、期待集合と同じ条件である: `argv` に `-testFilter` が無く `-nographics` がある（`withGraphics` が偽）、`platform` が `EditMode`、`projectPath` が用意した checkout の project、`unity.requestedVersion` が `ProjectVersion.txt` の版、process の開始時刻が case の開始より後、`logs` の XML と log が同じ invocation の directory にある。
    - test の集合と結果が期待集合と合う: XML の全ての test の fullname の集合と件数（同じ fullname が複数ある時は、その数も含めて比べる）、test ごとの result（`Passed`・`Inconclusive`・`Skipped`）が期待集合と同じ。違ってよいのは §2.7 の 1 種類だけ。
    - **期待集合:** S4-2 の判定 C（run `j1`、head `df1d731c`）の全 EditMode 回帰の XML と、その step（`-nographics` の実行。total 2443、Passed 2383、Failed 0、Inconclusive 25 は `SampleGame.Tests.Rendering.Vfx`、Skipped 35 は `[Explicit]`）。`df1d731c..07cc2c97` の `unity/` の差分は Architecture の文書 3 つだけで、このスライスも `unity/` を変えないので、test の集合は同じである。判定の担当は、S4-2 の evidence bundle の取得案内（S4-2 の Phase D が owner へ渡した）で XML を取得する。取得できない時は、base `07cc2c97` の detached の checkout で、切り出す前の `tools/run-tests.ps1` を filter なしで 1 回走らせ、その XML を期待集合にする（その時だけ Unity の起動が 1 回増える）。今回の実行の結果から期待集合を作らない。
- 未知の操作経路の疎通結果と確認地点: 未知だったのは 1 つで、`Unity.exe` で直接起動した窓のある Editor を、その checkout の `tools/unity-editor.cmd` が見つけて操作できるかである。**Phase A で確かめた（2026-11-25、改訂 1。この PC、Unity 6000.6.9f1、この branch の worktree、`Library` が無い状態から 1 回）。** `Unity.exe -projectPath <path> -logFile <path>` を `-batchmode` なしで起動し、起動した process を保持したまま、`status` と `eval` を 20 秒おきに呼んだ。観測は次のとおり。(1) 起動の前に、持つ process のいない project lock の残骸があった（直前に止めた Unity が残した物）。Editor はそのまま起動した。(2) `status` は、最初の約 6 分は終了コード 6 で instance なしを返し、その後、その project の instance を `state` が `ready`、`pid` が起動した PID と同じ値で返した。(3) `status` が ready になってから約 1 分半は、`eval` が終了コード 6 と `503 Service Unavailable: Server Busy` を返した。(4) その後、Editor 自身の PID と呼び出しごとの印を返す `eval` が終了コード 0 で通り、PID は起動した PID と同じだった。(5) `SessionState` へ値を置く `eval` と読む `eval` が通り、読んだ値は置いた値と同じだった。(6) `EditorApplication.Exit(0)` の `eval` は終了コード 6 と `Invalid response format` を返し、約 4.3 秒後に保持した handle が signal になって終了コード 0 を得た。project lock の file は、handle の signal の約 0.3 秒前に消えた（lock が無いことは終了を示さない）。(7) 終了の後、tracked の変更は URP の設定の 1 つだけで、ignore されない未追跡の file は無かった。確かめていないこと: Play の移行中の応答、reload をまたぐ再接続、commit を切り替えた後の `Library` での起動、2 つの Editor が同じ project を争う時の挙動（AC12 は偽の CLI で見る）。不成立の時の扱い: 動作確認（V2）か判定で、起動・ready・`eval`・終了のどれかが通らない時は、基盤の不備として B へ戻す（人の操作で埋めず、`unity open` へ替えない）。commit を切り替えた後の起動だけが失敗する時は、実行の担当が checkout を片付けて作り直してから取り直し、それでも失敗する時に B へ戻す。
- 人間の判断が必要な条件: なし。
- 高コスト証拠: なし（Player の build、実の R2、人の観察は無い）。
- 判定 C の照合 script の方式: 判定 C の offline 実行。判定の担当が script を書き、V1・V2・V3 の原記録の複製に対して走らせる（資格情報にも通信にも触れない）。原記録へ当てる前に、A3 で凍結した合成 fixture を通す。`base/` の下の file 名と `eval` の原応答の形は Phase B が決めるので、fixture はそれを仮定で置いている（仮定は snapshot の README にある）。判定の担当は原記録の形に合わせて読み方を適応し、その対応を判定の記録に残してから、適応した読み方で fixture の全ての組を通す。
  - 期待値の導出の規則: V1 は、base の driver の `-DryRun` の出力が期待値で、V1 の比べ方で一致すること。V2 は、AC10 の event の列、PID の一致、token の完全な一致、原応答の印と `callId` の一致、版と path の一致。V3 は、上の 3 つの観測と §2.7 の読み方。どの観測も、基盤の record の真偽の欄だけで決めず、step・XML・`git` の原出力・`eval` の原応答・note で確かめ直す。
  - 合成 fixture（Phase A snapshot に収録し、A3 で凍結する。git に入れない）: 観測ごとに、受理すべき 1 組と、1 か所だけ壊した拒否すべき組を持つ。V1: 行の欠け、行の順の入れ替え、今ある key の値の違い、型の違い、cache の root が新旧どちらの規則にも合わない。受理の側は、追加の key と基盤が足す行、規則どおりに違う cache の root を含む。V2: token の違い、`editor-ready` の PID の違い、event の順の違い、`forced` が真、送る `eval` が 2 回、原応答の印が `callId` と違う、戻した後の `git status` の原出力が case の前と違うのに `restore` の欄が真。V3: 受理の側の step は、`status` が `failed` で、`failure` が `Inconclusive のテストがあります` の 1 行だけを持つ（`Inconclusive` だけなら XML の root の result は `Passed`）。終了コード 2 の行を持つ受理の組も置く（期待集合の step にその行が無い組を含む）。拒否の側は、test の部分集合、期待に無い `Failed`、期待に無い `Inconclusive`、`failure` に許されない行がある、`argv` に `-testFilter` がある、`argv` に `-nographics` が無い、`stepIsCurrent` が偽、step が無い。§2.7 の 1 種類に当たる `Failed` は受理の側にもう 1 組置く（失敗の message が 3 つの文字列を含み、step の `failure` に `失敗したテストがあります` と root の result の行が加わる）。message が `Access to the path` と `is denied` を含み `Directory.Move` を含まない `Failed` は拒否の組にする。stall で止めた時の組も置く: 2 以外の終了コードの行があり、note と完成した XML がある（受理）、note が無い（拒否）、XML が完成していない（拒否）。同じ fullname の数が期待と違う組（拒否）と、step の path が `outputRoot` の外にある組（拒否）も置く。ここに挙げた組は最低限で、snapshot の期待の一覧が全ての組を持つ。fixture の record の欄は §3 の record の契約に、step と XML は今の runner の形に従う（V3 の step と XML は base の `run-tests.ps1` を偽の process seam で動かして作る）。各 fixture の期待（受理・拒否と理由）を一覧にして同じ snapshot に置く。
  - 判定の担当が自分の環境で script を書いて走らせられない時は、実行担当と判断担当の規則（[実行担当と判断担当](../../.agents/skills/osm-workflow/references/phases-and-handoff.md#実行担当と判断担当)）に従い、Phase B の session に書かせない。
- 機械検査: 判定必須の 2。基盤と driver の走査（AC6、AC8、AC9）は offline test の中にある。
- A0/A1 主担当・モデル・ベンダー: Claude Fable 5.2（Anthropic）。
- C' 用に予約した担当・モデル・ベンダー: 予約なし。Anthropic（主担当と A2）、OpenAI（設計の材料、A2 の目的レビュー、外部計画レビュー）、xAI の Grok（外部計画レビュー）が Phase A に関わり、関わっていない系列は残っていない。C' は Phase B・C と違う model で行う。
- 独立性の強化条件を満たせない場合の理由: risk は normal で、強化条件は要らない。初稿は Grok を予約していたが、外部計画レビューを依頼した PR の bot が Grok だったので、予約を外した（主担当が依頼の前に bot の系列を確かめていなかった）。

### 5.1 A2 の独立レビューと A3 の採否

**A2 の入力版と担当（2026-11-25）:**

| 観点 | 担当・モデル・ベンダー | 入力版 |
|---|---|---|
| 目的レビュー | Codex CLI、`gpt-5.6-luna`（effort medium）、OpenAI。新しい session | 目的・利用者の原文だけ。sha256 `5e7c6c0927e85832a43fa1292347eb393caeab50f2beaf89aa97f6d9381dd747` |
| アーキテクチャゲート | Claude Opus 5.5、Anthropic。新しい session | 固定 base `d6e839405abb376ce2de99cfdaf3ac7bca7bff14`（code は implementation base と同じ。program §9.1 を含む）の clean な detached checkout と、HANDOFF の copy。sha256 `924290942b90b56a885264bdb4f9ae23bf4585cd69ee0222f47dca70ec556a25` |
| 不変条件を破る順序（計画の対象は実装済みと仮定） | Claude Opus 5.5、Anthropic。上とは別の新しい session | 同上 |

成立しなかった試行（採否に使っていない）: 目的レビューは Codex の sandbox が日本語の file を文字化けさせて読み、1 回目は問いを読み違え、2 回目は担当が自分で止めた。3 回目に本文を引数で渡して成立した（上の sha256 はその本文）。アーキテクチャゲートは、Codex が sandbox の git の所有者の検査と同じ文字化けで 2 回止まったので、担当を Claude Opus 5.5 に替えた。3 件とも互いの指摘を見ていない。

**A3 の統合担当:** Claude Fable 5.2（Anthropic）。採否は下の表。目的・利用者の文言は A2 の後も変えていない（目的レビューのやり直しは要らない）。

**目的レビューの回答と初稿の突き合わせ:**

| # | 回答の要点 | 採否 |
|---|---|---|
| P-1 | case の失敗と、検証環境の構築・実行の基盤の失敗を分けられること | **採用。** AC5 の record の 2 つの欄と、AC6・AC8 の `status` の読み方 |
| P-2 | どの run がどの patch と環境で走ったかを追える。別の checkout や別の実行の結果を取り違えない | **採用。** AC4 の印（対象の commit と checkout の場所）、AC5 の HEAD の記録 |
| P-3 | 入力の不足や環境の構築の失敗を、あいまいな成功にしない | **採用。** AC5（列に無い `-Only`、0 件、宣言の無い checkout） |
| P-4 | 複数の checkout の作成・切替・後始末を手作業にしない | **採用。** 用意に加えて片付けを足した（§2.4 の 1、AC9、UC4）。§2.9 の (1) の根拠にもした |
| P-5 | Player の process の起動・監視・終了と、残った process の回収を手作業にしない | **不採用（このスライスでは）。** program §9.1 の利用者表が、Player の起動と観測を利用者（S4-2 の test）の持ち物にしている。基盤が直接起動する使い手が今は無い。補助の process の起動（§2.4 の 11）がその時の拡張点になる。§2.6 に送った |
| P-6 | run の間で cache や証拠を共有しない | **初稿のとおり。** 検証用の checkout の `Library` は run の間で使い回すが、run の cache でも証拠でもない |
| P-7 | S4-2 や疎通だけに依存した専用の API、case・fixture・期待値の自動生成、合否の判断を基盤が肩代わりする機構を作らない | **初稿のとおり。** N5、AC6、§2.4 の (b) の offline test |
| P-8 | 実行 file の引数、PID、作業用の directory、cache の path、証拠の file 名を毎回入力させない | **初稿のとおり。** UC1・UC2 の「機械が決めるもの」、§3 の「基盤が決めて、driver に書かせない物」 |

**アーキテクチャゲート（AG）と不変条件の探索（IB）の指摘:**

| # | 重さ | 指摘の要点 | 採否 |
|---|---|---|---|
| IB-1 | blocker | 応答した Editor が run の起動した物とは限らない（事前確認の後に別の Editor が同じ project を開くと、`eval` と終了の要求がそちらへ届く） | **採用。** 全ての `eval` の先頭の PID の照合。AC10、AC12、§3、N3 の経路 |
| IB-2 | blocker | 保持した handle（子の `pwsh`、CLI）の終了を、checkout を使う Unity の終了と同じに扱っている | **採用。** 復元の前に project lock を消さずに調べる（AC2 の (2)、AC3、制約 6）。`unity open` の時の読み方を §5 に書いた。marker の欠けを別の条件にはしなかった（lock の確認で足りる。起動の直後の窓は N2 の未確認に書いた） **改訂 1 の X3 で置き換えた:** lock の確認だけでは足りない。終了の確認は AC3 の (ii) により、N2 の未確認の窓は無くなった。 |
| IB-3 / AG-7 | major / minor | lock の関数は残骸を消すので、基盤の事前確認が run の作っていない file を消す。実行 file の解決も 2 つの関数の外にある。`ExitCode` の型 | **採用。** lock の関数を 2 つに分ける（制約 7、AC4、§3）。実行 file の解決も module へ（§3、§4）。seam の形と `[int]` を AC7 に書いた |
| IB-4 / AG-1 | major | 「linked worktree で detached」だけでは、rebase・bisect の途中、別の clone の worktree、path の別名、stage した drift を分けられない。渡された checkout の HEAD を動かせてしまう | **採用。** 進行中の操作の拒否、`--absolute-git-dir` での同一の判定、基盤の印（git dir）と印のある物だけの commit の切替、渡された checkout の HEAD は動かさない、drift の例外は stage していない変更だけ（AC1、AC9、制約 10・11、§2.9） **改訂 1 の X1 で置き換えた部分:** 印は渡された checkout にも求める。Unity の起動が書き換える file を戻す例外はやめた。 |
| IB-5 | major | run の途中で対象の commit が変わっても、同じ run に混ざる | **採用。** 印に対象の commit と checkout の場所（AC4）、case ごとの HEAD の記録（AC5）。排他は §2.6 のまま **改訂 1 の X2 で置き換えた部分:** 基盤を使う run 同士の排他は lease で持つ。 |
| IB-6 | major | AC3 で止まった run を再開する道が無い（patch が残って事前確認が拒み、証拠の名前が衝突する） | **採用。** 試行の番号、回復の手順（Harness README）、続きの実行の record（AC3、§3、UC3）。基盤が回復まで行う案は採らず §2.6 に送った（基盤は自分が終了を確かめていない process の後で戻さない） **改訂 1 の X2 で足した:** 止まった checkout は隔離し、解除の入口を置いた。 |
| IB-7 / AG-12 | major / minor | 列に無い `-Only` で 0 件のまま exit 0。`-DryRun` の root を本番が続ける。RunId の予約名。同じ RunId の `-DryRun` の後の本番の拒否が互換の一覧に無い | **採用。** AC4、AC5、AC8 の「前より厳しくなる点」 |
| IB-8 / AG-10 | major / minor | step は 25 個で「26 個」は誤り。§1 の行の範囲が実際と合わない | **採用。** 数を書かない形にした（AC8）。§1 の 1 行目を直した |
| IB-9 | major | 「base と同じ桁」は比べる相手も意味も決まらない | **採用。** 数の比較を外し、total が 0 でないことと既知の事象だけの失敗にした（AC7、V3） **改訂 1 の X8 で置き換えた:** 期待集合との照合にした（V3）。 |
| IB-10 | major | AC2 と AC3 の優先、body の例外の後の Editor の後始末、`--check` の単位 | **採用（形を変えて）。** AC2 の順と AC3 の優先、`--check` はまとめて。送り直しは、応答の文言で「受理されなかった」を見分ける案を採らず、何度届いても同じ結果の終了の要求だけを基盤が送り直す形にした（AC12。文言の分類は実機で未確認のため） |
| IB-11 | minor | 文字列の走査は comment や help で当たる。`status` は製品の test の結果で、exit 0 が合格に読める | **採用。** 構文木の走査と対象の file の列挙（AC6、AC8、AC9）、`status` と exit 0 の読み方（AC6、AC13） |
| IB-12 | minor | 実行の担当が PID だけで止めると別の process を止めうる。止める範囲。AC10 の path の一致 | **採用。** 制約 12（command line・PID・開始時刻の照合）、制約 5 と AC3（子孫まで。`run-tests.ps1` の起動では拒む）、AC10 の正規化 |
| AG-2 | major | 管理する file の寿命が run の object（1 回の呼び出し）と合わない。今の driver は設定を戻さず、後の `-Only` がそれに依存する | **採用。** 寿命を run（呼び出しをまたぐ）にし、戻すのは片付けの指定の時だけ。書いた事実を run の root に残す（AC4、§3） |
| AG-3 | major | 外部 directory を作る時点と印の置き場が今の driver と違う（`-DryRun` で残る、印が製品の cache の root に入る、片付けの後の再開） | **採用。** 最初に使う case で作る、印は隣、片付けた物は作り直さない（AC4） **改訂 1 の X4 で置き換えた部分:** 印は外部 root の直下に置き、用途の directory をその子にする。 |
| AG-4 | major | case の失敗の後に止まるか続けるか、終了コードが決まっていない | **採用。** §1 に今の挙動、AC8 に互換の 3 点 |
| AG-5 | major | 関数の再公開で、driver が順序の保証を迂回できる | **採用。** 公開は case の body が使う物だけ。走査に公開しない関数の名前（§3、AC8、AC9） |
| AG-6 | major | 補助の process の置き場が §3 に無い | **採用。** `ScenarioUnity` に「起動して保持する」入口、呼び出しの終わりの確認は `Scenario`（§3、§2.4 の 11） |
| AG-8 | minor | 起動の前に戻す file の一覧は製品の project の path で、読む checkout も違う | **採用。** `tools/scenario-launch-drift.json`（repo の設定）にし、対象の checkout の root から読む（§3、AC1、AC6） **改訂 1 の X1 で置き換えた:** 一覧の file そのものをやめた。 |
| AG-9 | minor | 基盤を通した `run-tests.ps1` の実の起動を一度も通さない | **採用。** 全 EditMode 回帰を疎通用の scenario の `tests` の case で走らせる（V3、§2.4 の 4）。Unity の起動は増えない |
| AG-11 | minor | process の台帳（handle、呼び出しの間）と永続化（呼び出しをまたぐ）が同じ module にある | **採用。** 台帳は `Scenario` の run の object へ。`ScenarioEditor` と `ScenarioUnity` は `ScenarioRun` に依存しない（§3） |

A2 の後の設計の変更（印、PID の照合、lock の確認、試行、公開面の絞り込み）について、A2 の観点の再レビューは行わなかった。理由: どれも A2 の指摘が示した最小の修正の範囲で、責務の置き場と依存の向きを変えていない（`Scenario` に台帳を移した点は AG-11 の提案そのもの）。統合案は Draft PR の外部計画レビューが全体を見た（下）。

**外部計画レビュー 1 巡目（[PR #215](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/215)、2026-11-25、対象は統合案の commit `7772edac`）と改訂 1 の採否:**

1 巡目のレビューは 3 件。G は PR の bot（xAI の Grok）、O1 と O2 は owner の account から投稿された OpenAI の ChatGPT の 2 件（O2 は「Phase C の検証能力」）。下の採否は主担当の案で、凍結の時に owner が確かめる。上の A2 の表の採否のうち、下の行で改めた物は、A2 の表の各行の末尾に書き添えてある。

| # | 出どころ | 指摘の要点 | 採否 |
|---|---|---|---|
| X1 | O1 の P1-1 | clean で detached なだけの人の worktree も検証用に見える。Unity の起動が書き換える file だけの変更を、人の編集でも捨てる。全ての checkout の事前確認を終えてから書く | **採用。** 印を渡された checkout にも求め、登録の操作を置いた（AC1、AC9、`scenario-checkout.ps1`）。事前確認で戻す例外と `scenario-launch-drift.json` をやめた（基盤の中では、case の後の復元が毎回 HEAD へ戻す）。AC1 の最初の文 |
| X2 | O1 の P1-2 | 基盤を使う 2 つの run が同じ checkout の事前確認を通り、互いの patch や HEAD を壊す。run の印の作成も競合する | **採用。** checkout と run の lease、印の排他的な作成、終了の未確認の後の隔離（AC1、AC3、AC4）。人や任意の git の process との排他は前提に書き、保証しない（§2.3 の前提、§2.6） |
| X3 | O1 の P1-3、O2 の V2、G の 1 | project lock が無いことは Unity の終了を示さない。`unity open` への切り替えは、PID の照合も終了の確認も成り立たなくする | **採用。** `run-tests.ps1` の起動の「終了を確かめた」を、今回の step が Unity の終了コードを記録していることにした（AC3 の (ii)。marker の欠けや終了コードの無い step は、lock が空でも戻さない）。`unity open` への切り替えを置かず、直接の起動を Phase A で確かめた（§5、制約 10） |
| X4 | O1 の P1-4 | 隣の印だけでは directory の差し替えを見分けられない。祖先の reparse point、管理する file の path、作る途中の失敗、原 bytes の保存、復元の確認が status だけ、証拠の保存の失敗の時の復元 | **採用。** 印を外部 root の直下に置き、用途の directory を子にした。削除の直前の確かめ直し、管理する file の原 bytes の保存と外部の変更での停止、復元の後の HEAD と index の確認、保存の失敗の時も復元すること（AC2、AC4） |
| X5 | O1 の P2-1、O2 の P2 | 禁止語の走査は「case・fixture・照合値だけを書けば済む」の証明にならない。S4-3a の E 系列を模した複合の scenario を offline で通す | **採用。** AC15。走査は補助と書いた（AC9） |
| X6 | O1 の P2-2、O2 の V1 | `-DryRun` の record の一致は、patch・body・復元・runner・複写の実の経路を示さない。marker が古い step を指しうる | **採用。** AC8 の示し方 (i)（`-DryRun` でない offline の実行と、bytes・呼び出しの直接の確認）。V1 は公開の形の確認に絞った。step が今回の物であることの確認（AC5）。複写の対象の選び方は S4-2 に固有の code として driver に残す |
| X7 | O2 の V2 | 固定の値や自己申告では、今回の Editor の今回の操作と言えない。偽の CLI で「実行してから timeout」と「未実行」を分ける | **採用。** 実行の担当が渡す token と完全な一致、event の列（AC10、V2）。AC12 の 2 つ目 |
| X8 | O2 の V3、G の 4 | total が 0 でないだけでは部分の実行を拒めない。既知の事象の範囲が決まっていない。stall で止めてよい PID が record から決まらない。止めた取得を成功の証拠にしている | **採用。** 期待集合（S4-2 の判定 C の XML）と、test の集合・結果・起動の条件の照合（V3）。受容する失敗を 1 種類に固定し、stall と step の保存を分けた（§2.7）。止めてよい相手は、record の子の `pwsh` を親に持つ Unity だけ（制約 12） |
| X9 | O2 の「判定 C の照合自身の検出能力」 | 基盤の test が成功の flag だけを見る。判定の script を、正しい fixture と 1 か所ずつ壊した fixture で確かめる。「合成 fixture は要らない」は誤り | **採用。** 制約 3 と §5 の単体テストの欠陥の一覧。合成 fixture を Phase A snapshot に置いて凍結する（§5）。判定 C の offline 実行の方式は変えない |
| X10 | G の 2 | seam の `ExitCode` は、起動できなかった時は `$null`。`[int]` に揃えると、終了を確かめる前に `EditorSettings` を戻す | **採用。** AC7、N6 の経路 |
| X11 | G の 3 | 共有する起動の関数は窓を作らない設定のまま。窓のある Editor の引数と log の置き場が無い | **採用。** 制約 6、AC12、§3 |
| X12 | G の 5 | 復元で消してよい未追跡の file の範囲が無い | **採用。** AC2 の 4（case の開始の時点で無かった、ignore されない未追跡の file）。X2 の lease と前提で、N1 と両立する |
| X13 | G の 6 | 補助の process の寿命が、run と 1 回の呼び出しの両方になっている | **採用。** 1 回の呼び出しに揃えた（§2.4 の 11）。呼び出しをまたぐ使い方は S4-3b へ（§2.6） |
| X14 | G の 7 | 外部 command は、S4-2 の移し替えでも疎通でも示していないので (a) でない | **採用。** (b) にした（§2.4 の 15） |
| X15 | G の 8 | scenario の名前と checkout の名前が path に入るのに検査が無い | **採用。** AC4 の 1 つ目 |
| X16 | G の 9 | 子の process への環境変数の渡し方と、`git apply --whitespace=nowarn` が落ちている | **採用。** AC8、AC2 |
| X17 | G の 10 | 残骸の lock の file がある時に、窓のある Editor を起動するのかが無い | **採用。** 消さずに起動する（AC12）。Phase A の疎通で、残骸のある状態からの起動を確かめた（§5） |
| X18 | G の 11 | commit を切り替えた後の `Library` を Editor が開けない時の責任が無い | **採用。** §5 の「未知の操作経路」に、片付けて作り直すことと、それでも失敗する時の扱いを書いた |
| X19 | O1 の「owner 裁定」、G と O2 の注記 | risk の再評価。外部計画レビューに Grok と OpenAI が入ったので、C' の未関与の系列が無い | **risk は normal のまま**（§2.9 に理由）。C' の予約を外した（§5） |
| X20 | O1 と O2 の「owner 裁定」 | §2.9 の 2 件（worktree の用意と片付け、全 EditMode 回帰）を支持。Player の build の取り直しは不要 | 記録した（§2.9）。owner が 2026-11-25 に、2 件とも案のとおりにすると決めた |

**外部計画レビュー 2 巡目（同 PR、2026-11-25、対象は改訂 1 の commit `e36d4d11`）と改訂 2 の採否:**

| # | 出どころ | 指摘の要点 | 採否 |
|---|---|---|---|
| Y1 | G（2 巡目）の 1 | 期待集合は VFX の `Inconclusive` を含み、runner はそのたびに step を `failed` にして `failure` に行を足す。「`failure` が空でない」を不成立にすると、一致した実行でも AC7 を満たせない。`-nographics` の確認が無い | **採用。** XML の集合の一致を正にし、step の `failure` に許す行を決めた（§2.7）。`argv` の `-nographics`（V3）。合成 fixture の受理の側（§5） |
| Y2 | G の 2 | 起動に失敗した step も `process.exitCode` の key を持ち、値は `null`。key の有無で読むと、Unity が残っていても戻す | **採用。** 整数の時だけ確かめたとする（AC3 の (ii)） |
| Y3 | G の 3 | 隔離の解除が record の process（子の `pwsh`）の不在だけを見る。子が終わって Unity だけが残った時に、手で戻す所へ進める | **採用。** 解除の入口が、その project を指す process が無いことを CIM で読む（AC3）。実行の担当が止めてよい相手の決め方も、子が残っていない時を含めた（制約 12）。指摘は「Unity が親を付け替える」としていたが、Windows では親の PID の値は残る。どちらでも、project を指す process で確かめれば閉じる |
| Y4 | G の 4 | §5.1 の A2 の採否の欄が、改訂 1 でやめた設計を「採用」のまま残している | **採用。** A2 の表の 7 行の末尾に、置き換えた X の行を書き添えた |
| Y5 | G の 5 | S4-2 の record の `filter` は `$` なし、`run-tests.ps1` へ渡す引数は `$` 付き、の区別が AC8 に無い | **採用。** AC8 |
| Y6 | O（2 巡目）の P1 の 1 つ目 | 隔離を、終了の未確認を検知した後に書くのでは遅い（driver の強制終了、PC の再起動、record の書き込みの失敗では残らない）。登録・片付け・解除も同じ lease に従う。子の `pwsh` の不在だけで Unity の不在としない。補助の process の子孫の終了を、親の handle だけで主張しない | **採用。** checkout を変える前に使用中の記録を置き、復元を確かめた後にだけ消す（AC3、§2.1）。隔離の記録と兼ねるので、仕組みは増えない。`scenario-checkout.ps1` の 3 つの操作の lease（AC9）。解除の条件は Y3 と同じ。孤児を run の物と確かめられない時は owner へ返す。補助の process の契約（§2.4 の 11）。AC15 に、lease が離れて記録が残る列を足した |
| Y7 | O の P1 の 2 つ目 | 管理する file は呼び出しをまたいで残るのに、lease は呼び出しの単位で、checkout は別の RunId に使い回される。A の設定を B が読むか上書きし、A の続きが不整合になる。まだ無い checkout の事前確認の順 | **採用。** 管理する file が残る間の run の予約（AC4、AC1、§2.1）。放置された予約の解除。AC15 に A → B の拒否 → A の続き → 片付け → B の列を足した。まだ無い checkout は、作る前に置き場の path を検査する（AC1） |
| Y8 | O の P2 の 1 つ目 | V1 は cache の root の場所を変えたのに、値の一致を求めている。V3 は許す失敗があっても step の `failure` が空でないと不成立になる。stall の時の終了コードの行と test の失敗を分ける。Unity を起動しなかった step は process の時刻を持たない | **採用。** V1 で外してよい値を 3 つに決め、それぞれの規則を確かめてから外す（§5 の V1）。step の `failure` に許す行（Y1、§2.7）と、stall の時の終了コードの行は note と完成した XML がある時だけ許すこと（§2.7）。今回の step の証明を、基盤が渡す出力の directory にした（AC5）。合成 fixture に足した（§5） |
| Y9 | O の P2 の 2 つ目 | 判定は基盤の自己申告（`stepIsCurrent`、`restore` の真偽）だけでなく、step の原本、`git` の状態の原記録、fullname の重複を含む件数、終了コードと note、`eval` の原応答と `callId` を確かめ直す | **採用。** 基盤が `git` の原出力を証拠に保存する（§3）。V1・V2・V3 と照合の規則に、原記録で確かめ直すことを書いた（§5）。record の契約に、真偽の欄は申告であることを書いた（§3） |

改訂 1 と改訂 2 の設計の変更（印の必須化、lease・使用中の記録・予約、終了の確認の定義、record の契約、AC15、V3 の期待集合）について、A2 の観点の再レビューは行わない。理由: 変更は外部計画レビューの指摘への対応で、同じ担当が 2 巡目で改訂 1 を見て、1 巡目の指摘が受け入れ条件と制約に入ったことを確かめている。2 巡目の 2 件はどちらも、大きな設計の変更を求めていない。

**主担当の自己点検と改訂 3（2026-11-26、合成 fixture の作成時。担当は Anthropic の系列）:** V3 の fixture の step と XML を base の `run-tests.ps1` で作り、この PC に残る実の全 EditMode の step と XML（`TestResults` の下。写しの重複を除いた）と突き合わせて、次の誤りを見つけた。下の採否は主担当の案で、owner が 2026-11-26 に Z1〜Z5 の全てを承認した（承認の時点では未凍結）。

| # | 見つけた事実 | 採否 |
|---|---|---|
| Z1 | 受容済みの Directory.Move の `Failed` は、`Directory.Move` を `stack-trace` ではなく message に持つ（14 件の全て）。改訂 2 の §2.7 の読み方では、受容した事象が起きると AC7 が満たせない | **採用。** message が 3 つの文字列を含む物にした（§2.7）。fixture に、`Directory.Move` を含まない access denied の拒否の組を足した（§5） |
| Z2 | `Inconclusive` だけの実行では XML の root の result は `Passed` で、runner は root の result の行を足さない。改訂 2 の「受理の側の step は Inconclusive と root の result の 2 行」（§5）と、root の result の行を `Inconclusive` の条件で許す §2.7 の書き方は誤り | **採用。** §2.7 の許す行の条件と、§5 の受理の組の文を直した。許す行の集合の考え方は変えない |
| Z3 | Unity は同じ結果の集合でも終了コード 0 と 2 のどちらも返す（Failed 0・Inconclusive 25 の別々の実行 5 回で 0 が 2 回、2 が 3 回）。改訂 2 の「終了コードの行は期待集合の step に同じ行がある時だけ」では、期待集合の実行が 0 だった時に、同じ結果の GO 候補が偶然で AC7 を落とす | **採用。** 終了コード 2 の行は、test の集合と結果が期待集合と合う時に許す（§2.7）。2 以外は stall の note がある時だけ。fixture に、期待集合の step に行が無い受理の組を足した |
| Z4 | V1 は旧の driver の行を選んで比べるが、選び方が書かれていない。AC8 は key の追加を許すので、S4-2 の行に `event` を足すと基盤の行と分けられない | **採用。** 旧の driver の行は「`step` を持ち `event` を持たない行」と読み（§5 の V1）、S4-2 の行に `event` を足さない（AC8） |
| Z5 | `base/` の下の file 名と `eval` の原応答の形は Phase B が決めるので、凍結する fixture はそれを仮定するしかない。判定 C の offline 実行で、仮定の形の fixture と実の原記録の両方に同じ script を当てる手順が無い | **採用。** 判定の担当が読み方を適応し、対応を記録してから fixture を通す（§5 の「判定 C の照合 script の方式」）。仮定は snapshot の README に書いた |

改訂 3 について、A2 の観点の再レビューは行わない。理由: Z1〜Z3 は AC7 の読み方を実の runner と XML の形に合わせる訂正で、判定の主張（期待集合との一致と、受容した 1 種類だけの差）を変えない。Z4 と Z5 は判定の手順の欠けを埋める物で、責務の置き場・依存・状態を変えない。目的・利用者は変えていない。

**外部計画レビュー 3 巡目（同 PR、2026-11-26、対象は改訂 3 の commit `3a65d9bd`）と改訂 4 の採否:** G（PR の bot、xAI の Grok）が 4 件を挙げ、O（owner の account から投稿された OpenAI の ChatGPT）は追加の指摘なしで凍結を支持した（Z1〜Z5 も支持。Phase B へは、使用中の記録と予約を変更より先に確定する順序、process の起動の前の印、まだ無い checkout の path の排他を offline test で確かめるよう求めた。どれも既存の AC の実装の確認である）。下の採否は主担当の案で、owner は 3 巡目のコメントの確認の後に凍結を指示した。

| # | 出どころ | 指摘の要点 | 採否 |
|---|---|---|---|
| W1 | G の 1 | 予約の解除が「保存した bytes が無い」を拒否にしていて、元から無かった管理する file（S4-2 の `OSMContentDelivery.json`）を消して予約を外せない。隔離の解除と予約の解除は成功の条件が違う | **採用。** 書く前の記録のとおりに戻す（無かった file は消す）。拒否は書く前の記録そのものが無い時（AC4）。`scenario-checkout.ps1` に予約の解除を別の操作として足し、隔離の解除を先に通す（AC9、§3、AC13） |
| W2 | G の 2 | V1 の新しい cache の規則は `ovr\s42\` だが、AC8 は S4-2 の scenario の名前を決めていない | **採用。** AC8 に名前を `s42` と書いた |
| W3 | G の 3 | program §9.1 の S4-3b の行が、補助の process を「run の寿命」のままにしている | **採用。** 「driver の 1 回の呼び出しの間だけ。呼び出しをまたぐ分は S4-3b が足す」に直した（この branch の program） |
| W4 | G の 4 | AC10 の 6 は終了コードを記録するが、0 を合否にしていない | **採用。** 終了コードが 0 であることを AC10 の 6 と判定の文に足した。fixture に、`forced` が false で終了コードが 1 の拒否の組を足した |

改訂 4 について、A2 の観点の再レビューは行わない。理由: W1 は既存の予約の仕組みの解除の条件の訂正で、状態の置き場と所有を変えない（実行の担当の入口に操作が 1 つ増える）。W2〜W4 は名前・表・合否の値の確定で、責務と依存を変えない。目的・利用者は変えていない。

## 6. Phase B 実装結果

- 実装（§4 の順に 5 commit。base `07cc2c97` からの差分は `tools/`・`.github/workflows/offline-checks.yml`・文書だけで、`unity/` と `tools/unity-editor.ps1`・`.cmd` は差分に現れない）:
  1. `7ede3a83` `tools/Harness/UnityProcess.psm1`（151 行）を作り、`run-tests.ps1` の process の起動と待機・lock・実行 file の解決を置き換えた（312 → 289 行）。`Invoke-UnityTestProcess` と `Test-UnityProjectLock` は名前と返す形を残した。既存の 4 本の offline suite を test の側を変えずに通した。
  2. `be836e2f` `ScenarioRun.psm1`（381 行）、`ScenarioCheckout.psm1`（312 行）、`scenario-checkout.ps1`（95 行）、`Scenario.psm1` の中核、`tests/Scenario.Tests.ps1`。
  3. `d7d7d20b` `ScenarioUnity.psm1`（97 行）、`Scenario.psm1` の test・batch・外部 command・補助の process の関数、`tools/s42-player-overlay.ps1` の移し替え（186 → 154 行）、`tests/ScenarioDriver.Tests.ps1`（S4-2 の互換）、`tests/ScenarioTestSupport.ps1`（fixture の共有）。互換の test は先に書き、base の driver の写しに対して 7 件が通ることを確かめてから移した（`-S42Driver <写し>` で同じ test を旧の driver に向けられる。旧の driver に無い挙動の 1 件は飛ばす）。
  4. `7c8f2257` `ScenarioEditor.psm1`（168 行）、`Scenario.psm1` の Editor の関数（最終 763 行）、`scenario-editor-smoke.ps1`（61 行）、`tests/ScenarioUnity.Tests.ps1`（AC15 の複合 scenario を含む）、走査の test。
  5. `698340f7` CI に 3 本を足し、Harness README に「実機の検証の scenario」の節（+63 行）、`phases-and-handoff.md` の「検証経路の成立」に 1 文。`docs/unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md` の S4-2 の driver の説明（「URP の設定を driver が戻す」）が実装と食い違うので直した。
- 判定必須の offline と機械検査（implementation head `698340f7` で実行、全て exit 0）: `Scenario.Tests.ps1` 20 件、`ScenarioDriver.Tests.ps1` 12 件、`ScenarioUnity.Tests.ps1` 25 件、`Harness.Tests.ps1` 38 件、`UnityGate.Tests.ps1` 21 件、`UnityObservation.Tests.ps1` 16 件、`UnityTestRunner.Tests.ps1` 31 件、`contract-audit.tests.ps1` 29 件、`contract-audit.ps1`（違反なし）、`docs-audit.ps1`（error 0。警告 1 件は base からある `PLAY_CONTENT_RESOLUTION_PROGRAM.md` の harvest の頃合い）。
- 受け入れ条件と §2.4 の行から test への対応（AC11。test は名前の先頭に AC と行の番号 `Op<n>` を持つ。下に無い行は名前で辿れる）:
  - AC1: `AC1 preflight refuses unsafe checkouts without writing anything`（印なし・主の worktree・branch・MERGE_HEAD・tracked・staged・未追跡・同じ checkout・隔離・別の run の予約・lock・lease・別の repo・HEAD の不一致）、`AC1 pending creation path is exclusive`。Op1・Op2。
  - AC2: `AC2 patch is applied ...`、`AC2 a failing patch ...`、`AC2 a body exception ...`、`AC2 a restore that does not return ...`、`AC2 an evidence save failure ...`、`AC2 a body exception with the Editor open ...`。Op3。
  - AC3: `AC3 quarantine blocks every run ...`、`AC3 an unexited batch is quarantined ...`、`AC3 an exit not confirmed by the deadline ...`、`AC5 AC3 a stale, missing or unconfirmed step ...`。Op7。
  - AC4: `AC4 names reject ...`、`AC4 run root continuation ...`、`AC4 external root ...`（Op5）、`AC4 managed files ...`（Op9）、`AC4 managed file paths ...`、`AC4 unreserve ...`、`AC4 lock check reads without deleting ...`。
  - AC5: `AC5 -Only with an unknown name ...`、`AC5 After writes scenario lines ...`、`Op4 AC5 the current step is accepted ...`、`AC5 a preflight step ...`、`Op16 AC5 evidence names ...`。Op6 は AC2 の保存の失敗と AC15。
  - AC6: `AC6 foundation code names no product identifier`、`AC6 the scan sees literals and commands but not comments`。AC7: `AC7` の 4 件と既存の 4 本の suite。AC8: `AC8 S4-2 driver` の 8 件と `AC8 AC9 drivers hold only cases and declared evidence`。AC9: `AC9 prepared checkout ...`、`AC9 creation path checks ...`、`AC9 register and remove ...`、`AC9 the smoke scenario dry-run ...`。AC12: `AC12` と `Op8` の 7 件。AC15: `AC15 a composite scenario ...`。
  - (b) の行: `Op10`・`Op11`・`Op12`・`Op13`・`Op14`・`Op15`・`Op18`。Op17 は計画のとおり Op12・Op14 と同じ拡張点。Op19 は `Op8 Op19 AC12 start, ready by PID ...`（送る eval の code・時刻・原応答の記録）。
- §5 の「落ちる case を必ず持つ欠陥」: patch を当てない（`AC2 patch is applied` が body の中の bytes を見る）、body を呼ばない（呼び出しの回数）、復元しない（復元の後の bytes と status）、古い step を受け入れる（`stale` の変種）、別の Editor の応答を受け入れる（`another Editor answering`）、送る eval を送り直す（`a send is never resent`）、終了の未確認の後に戻す（`AC3` の 3 件が bytes と使用中の記録を見る）、証拠の保存の失敗を成功にする（`AC2 an evidence save failure`）。
- HANDOFF との差（どれも B 適応。責務の置き場・依存の向き・公開の関数の範囲・record の契約の名前と意味は変えていない）:
  - **test の file の分け方:** `Scenario.Tests.ps1`（run・checkout・case の順序・隔離と解除）、`ScenarioDriver.Tests.ps1`（S4-2 の互換・走査・疎通用の scenario の `-DryRun`）、`ScenarioUnity.Tests.ps1`（process の側・Editor の adapter・AC15。偽の process と偽の CLI がここにある）の 3 本に分け、fixture を `ScenarioTestSupport.ps1` で共有した。CI には 3 本を足した。
  - **`Scenario.psm1` は 763 行で、§3 の予想（約 240）と行数の警報（500 行）を超えた。分けない。** 増えたのは case の body が使う関数（test・batch・外部 command・補助の process・Editor の 5 関数）と、その record と台帳の扱いで、どれも §3 の公開面の行に書かれた物である。台帳・record・現在の case を private に持つので、別の module にすると private の関数を公開するか、台帳の置き場を 2 つにすることになる。§3 の責務（順序の保証と台帳と body の公開関数）は増やしていない。約 3 割が契約と判断理由の comment。
  - **lease の file の場所:** §3 は「worktree の git dir」と書くが、共通の git dir の `osm-scenario/leases/<path の SHA-256 の先頭 32 桁>.lease` に置いた。worktree の git dir に開いたまま置くと `git worktree remove` がその directory を消せず、まだ無い checkout の作成の排他（3 巡目の O の確認事項）にも同じ file を path から決めて使えるため。読み書きするのは `ScenarioCheckout` だけで、所有と寿命は変えていない。印・使用中の記録・予約は計画のとおり worktree の git dir（`osm-scenario/`）。
  - **`scenario-checkout.ps1` は `ScenarioRun.psm1` も import する。** 予約の解除が、run の root の書く前の記録のとおりに管理する file を戻すため（`Restore-ScenarioManagedFiles` は run の root の path だけで動く）。
  - **offline test の seam:** `Set-ScenarioSeam`（process の起動、Editor の CLI、時計、process の照会、repo の root、外部 root の基点）を置いた。test 専用で、driver の走査が禁止の名前に含める。外部 root の基点の seam は、offline test が実の drive の直下に書かないためにある（既定は run の root の drive）。
  - **`Add-ScenarioCase -After`:** 復元の後に呼ばれ、scenario が自分の形の行を書く所。S4-2 の今ある行は復元の後の `git status` を持つので、これが要った。隔離の時も呼び、戻していない今の状態（`Restored` 偽、`Quarantined` 真、`GitStatusAfter` は戻す前の status）を渡す。AC8 の (2)「marker が無い時は record を書いてから止まる」と AC3 の「戻さない」を両立させるため。
  - **record に足した欄と event:** `in-use` の状態は `state`（`placed`・`cleared`）。`quarantine` に `kind`・`restored`・`reason`。`test-run` に `childExitCode`・`unityExitCode`・`terminationConfirmed`。`eval` に `codePath`。`case-end` に `failure`。`run-start` に `only` と checkout ごとの `action`（`create`・`switch`・`use`）。event に `batch-start`・`command`・`helper-start`・`managed-file`・`managed-restore`。ready を待つ間の読む eval は `eval` の event にせず（原応答は `base/` に残り、通った 1 回は `editor-ready` の `callId` と `responsePath` が指す）、AC10 の event の列が ready の後の 5 回だけになるようにした。
  - **`base/` の file 名と eval の原応答の形（判定 C の合成 fixture の仮定と照合する所）:** case の前は `before.status.txt`・`before.head.txt`・`before.index.diff`・`before.work.diff`、戻す前は `restore-before.*`、戻した後は `after.*`、隔離の時は `quarantine.*`。eval は `editor-<n>-eval-<k>-<種類>.cs`（送った code）と `.response.json`（`callId`・`kind`・`sentUtc`・`exitCode`・`timedOut`・`output`）。`output` は CLI の原出力で、この呼び出しの応答は `OSMEVAL|<callId>|<Editor の PID>|ok|<値の UTF-8 の Base64>`（PID が違えば `wrong-target`）を含む。Editor の log は `editor-<n>.log`、batch は `batch-<n>.log`、runner は `runner-<n>/` と `runner-<n>.out.txt`、外部 command は `command-<n>.out.txt`。
  - **`-DryRun` は checkout を作らず、HEAD も切り替えない。** 用意されていない checkout では止まる（作ると dry-run が checkout を書くことになる）。
  - **渡された checkout で `-Commit` を省いた時:** 対象の commit を確かめず（S4-2 の今の使い方）、run の印に checkout ごとの HEAD を持たせて、続きの実行で HEAD が変わっていれば拒む。S4-2 の driver に省ける `-Commit` を足した。
  - **S4-2 の X の record の写し:** `x1-record.json` を run の root の直下ではなく、`evidence/CopyToY/<試行>/` に置く（run の root の直下は基盤の置き場の外なので）。Harness README の「前と変わる点」に足した。
- 未実行: V3（全 EditMode 回帰。判定 C の担当）。Editor のコンパイル確認は対象なし（C# を変えていない）。
- 開発中の副作用: 互換の test を base の driver に向けた時、base の driver が `C:\s42c\nomarker` と `C:\s42c\resume`（空の directory）を作った。消す操作は実行環境の保護で止められ、残っている。repo と検証用の checkout には影響しない。
- implementation head commit: §0 の欄を正とする。
- Phase B 担当・モデル・ベンダー: Claude Opus 5.5（Anthropic）。

### 差し戻しの修正（1 巡目、2026-11-26）

- 実装: `c3f02632`（1 commit。差分は `tools/Harness/` の `Scenario.psm1`（763 → 837 行）・`scenario-checkout.ps1`・`README.md`・`tests/Scenario.Tests.ps1`・`tests/ScenarioUnity.Tests.ps1` だけ）。分類はどれも B 適応（新しい状態・依存・公開の関数・record の契約の名前は要らなかった。§4 の A 再開の条件には当たらない）。
- 補助の process の終了を確かめる時（発見 C の F1）: checkout を使う case で起動した補助の process は、他の process と同じく、その case の終わりの復元の前に終了を確かめる（`Confirm-ScenarioCaseProcesses` が飛ばすのは checkout を使わない case の物だけ）。確かめられなければ今ある隔離の経路に入り、戻さず、使用中の記録（PID 入り）を残す。checkout を使わない case で起動した物は、今までどおり呼び出しの終わりに確かめ、確かめられない時は、その process がどの checkout に触れたかを基盤が知らないので、その呼び出しの全ての checkout に使用中の記録を置いて隔離する（判断。触れた checkout だけを選ぶ手掛かりが無く、選び損なうと N2 を破るため。既に使用中の記録がある checkout には PID を足すだけ）。この隔離の `quarantine` の行の `restored` は、case がその checkout を戻し終えていたか。`Invoke-Scenario` が例外で抜ける時も、lease を離す前に同じ確認と隔離を通す。隔離の記録は `driver.jsonl` より先に置く（記録が書けない時も隔離は残る）。補助の process の記録の重複を避けるため、呼び出しの終わりの確認は case の終わりに記録済みの物を飛ばす。checkout に触れる補助の process はその checkout を使う case で起動する、と `Start-ScenarioHelper` の comment と Harness README に書いた。
- 解除の後の続きの実行の record（F2）: 続きの実行の `run-start` に `priorQuarantines` を足した。同じ run の前の呼び出しが残した `quarantine` の行を、`driver.jsonl` の行の番号（1 から）・`case`・`attempt`・`checkout`・`kind`・`pid`・`reason`・`restored`・`baseDirectory`（`base/<case>/<試行>`）で指す。続きでない run では空の配列。置き場を `case-start` でなく `run-start` にしたのは、続きの実行が隔離された case と別の case だけを走らせる時も前の record を指せるため。
- 片付けの lease（F3）: `remove` は `git worktree remove` が終わるまで lease を持つ。lease の file は共通の git dir にあり、外す worktree の git dir の外なので、持ったまま外せる。
- 「返却時に B が併せて見てよい点」（同じ RunId を `-Only` なしで走らせ直すと、checkout を切り替えてから拒む）: 直した。事前確認で、`-Only` なしで run の root が既にあれば、`Open-ScenarioRun` と同じ文言で、checkout の作成・切替の前に止まる。拒否の条件は変えていない（AC4 のまま）。
- 足した offline test（どれも修正の前の code で落ち、修正の後に通ることを、前の code の一時の worktree に新しい test の file を写して確かめた。一時の worktree は消した）:
  - `Op11 AC3 a helper started in a checkout case is confirmed before the restore and an unexited one quarantines the checkout`（終わる補助の process は `process-exit` が `restore` より前、終わらない物は patch が残り、使用中の記録に PID、`restore` と `in-use` cleared の行が無く、次の run が拒まれる）。
  - `Op11 AC3 an unexited helper from a case without a checkout quarantines every checkout of the invocation`（呼び出しの終わりの隔離、解除の入口が通ること、例外で抜けた時の隔離）。
  - AC15 の test に、解除の後の続きの `run-start` の `priorQuarantines` の値・指す行・`base/edit/1/quarantine.status.txt` を見る assert を足した。
  - `AC9 remove holds the lease until the worktree is removed`（外す直前に別の呼び出しとして lease を取りに行き、取れないこと）。
  - `AC4 the same RunId without -Only is refused before the prepared checkout is switched`（拒まれた run が checkout の HEAD を変えず、record を足さない）。
- 判定必須の offline と機械検査（`c3f02632` と同じ tree で実行、全て exit 0、失敗 0）: `Scenario.Tests.ps1` 22 件、`ScenarioDriver.Tests.ps1` 12 件、`ScenarioUnity.Tests.ps1` 27 件、`Harness.Tests.ps1` 38 件、`UnityGate.Tests.ps1` 21 件、`UnityObservation.Tests.ps1` 16 件、`UnityTestRunner.Tests.ps1` 31 件、`contract-audit.tests.ps1` 29 件、`contract-audit.ps1`（違反なし）、`docs-audit.ps1`（違反なし。警告 1 件は base からある `PLAY_CONTENT_RESOLUTION_PROGRAM.md` の harvest の頃合い）。
- 動作確認を B の出口で取り直すかの判断: 取り直さない。V1（S4-2 の driver の `-DryRun`）と V2（疎通用の scenario の Editor）はどちらも補助の process を使わず、`remove` も呼ばない。修正が触れた共通の経路は、`run-start` の基盤の行に欄が 1 つ増えること（V1 が比べる S4-2 の行は `event` を持たない行で、変わらない）と、`-Only` なしの事前確認に run の root の有無が加わること（V1・V2 は新しい RunId で走らせる）だけである。V3 は判定 C の担当のまま。
- 未実行: V3（判定 C の担当）。Editor のコンパイル確認は対象なし（C# を変えていない）。
- Phase B 担当・モデル・ベンダー: Claude Opus 5.5（Anthropic）。新しい session。

### 差し戻しの修正（2 巡目、2026-11-26。PR #215 の外部実装レビュー）

- 入力: PR #215 の 2026-11-26 03:18〜03:22（UTC）の 3 件（owner が載せた外部実装レビューの P1・P2、cursor の code review の本文と `ScenarioEditor.psm1` 45 行のインラインコメント。cursor の 2 件は同じ指摘）。
- 実装: `c38e0762`（1 commit。差分は `tools/Harness/` の `scenario-checkout.ps1`（96 → 105 行）・`ScenarioRun.psm1`（381 → 391 行）・`README.md`・`tests/Scenario.Tests.ps1`・`tests/ScenarioUnity.Tests.ps1` だけ）。分類はどれも B 適応（新しい状態・依存・公開の関数・record の契約の名前は要らない。§4 の A 再開の条件には当たらない）。
- **P1（`unreserve` が事前確認を通さない）: 直した。** AC9 の「4 つとも、その checkout の lease を取ってから行う（走っている run の checkout には触れない）」の違反と読んだ。lease は呼び出し元の repo の共通の git dir にあるので、別の repo の checkout に対しては、その checkout の run の lease と重ならない。`unreserve` は印・使用中の記録・予約の有無だけを見ていたので、別の repo から呼ぶと、その repo の run の予約を外して管理する file を戻せた（修正の前の code で、新しい test が `Ok` 真で通ってしまうことを確かめた）。直し方: 他の 3 つの操作と同じ `Get-ScenarioCheckoutProblems`（worktree の最上位、linked、同じ共通の git dir、印の repo、detached、進行中の git の操作、clean、project lock、path 予算）を通し、解除する予約そのもの（`別の run の予約があります`）だけを拒否の理由から除く。隔離は今までどおり先に拒む。`release` が見る「project を指す process」の確認は足していない（指摘の範囲の外で、予約の解除は process を止めた後の操作ではない）。
- **P2（戻した管理する file を同じ run でもう一度書けない）: 直した。** AC4 の「寿命は run」と、`Restore-ScenarioManagedFiles` の comment（「同じ run がもう一度書けば、また書く前から始まる」）に反する。戻すと記録は空になるが書く前の bytes の file（`0.before`）は残り、次の書き込みが記録の件数から同じ名前を選んで `CreateNew` で落ちていた（予約は書き込みより先に置くので、予約も残る）。直し方: 書く前の bytes の file は消さず上書きもせず、まだ使っていない番号を選ぶ（記録の件数から始めて、file が無い番号まで進める。前の書き込みが記録の確定の前に落ちて file だけ残った時も同じ）。初めての run では今までと同じ名前になる。併せて、レビューが確認を求めた大小文字だけが違う path は、記録の照合を `OrdinalIgnoreCase` にして同じ管理する file として扱う（別の記録になると、2 つ目の書く前の bytes が 1 つ目の書いた物になり、戻した後が元と違う。修正の前の code で 2 つの記録になることを確かめた）。記録の `path` は最初に書いた時の綴りのまま。
- **cursor の指摘（値が空の `ok` 応答で `Convert.FromBase64String("")` が例外になる）: 直さない。違反でない。** pwsh 7.7.0 と Windows PowerShell 5.1 の両方で `[Convert]::FromBase64String("")` が長さ 0 の配列を返し、`ConvertFrom-ScenarioEvalResponse` は空の値を空文字として返すことを確かめた。指摘の前提（.NET が空文字を拒む）が成り立たない。回帰を防ぐため、AC12 の既存の test に、空の値の応答が `Matched` 真・`Status` `ok`・`Value` 空文字になる assert を足した（修正の前の code でも通る）。
- PR 本文の古い説明の指摘（cursor と外部レビューの補足）: 票のとおり Phase D で直す。ここでは触れていない。外部レビューの「C' の Passed 2383 件の fullname 全件照合は未実施で、判定 C の全件照合と区別する」も Phase D の要約の注意で、B の修正の対象ではない。
- 足した offline test（P1・P2 の 2 件は修正の前の code で落ち、修正の後に通ることを確かめた。P2 の番号の選び方は、修正の後の code からその部分だけを外して、2 回目の書き込みで落ちることも確かめた）:
  - `AC9 AC4 unreserve refuses a checkout of another repo and an unsafe checkout, and touches nothing`（独立した 2 つの repo。B の run が予約を持ち、B の lease を開いたまま A から `unreserve` → 非 0、`別の repo` の理由、管理する file の bytes・予約・run の root の記録が変わらない。lease を離した後も同じ。同じ repo でも tracked の変更があれば拒み何も変えない。clean に戻せば同じ repo の解除は通る）。
  - `AC4 a managed file can be written again after it was restored in the same run, without overwriting the before-bytes`（元からある file で、`-Only` の続きをまたいで書く → 大小文字だけが違う path で書き直す → 戻す → 外から値を変える → 書く → 戻す。各段の bytes と予約の有無、記録が 1 件であること、`0.before` が最初の bytes のまま残り `1.before` に 2 回目の書く前の bytes があること）。
  - `ScenarioUnity.Tests.ps1` の `AC12 eval code checks the PID first ...` に空の値の assert。
- 判定必須の offline と機械検査（`c38e0762` で実行、全て exit 0、失敗 0）: `Scenario.Tests.ps1` 24 件、`ScenarioDriver.Tests.ps1` 12 件、`ScenarioUnity.Tests.ps1` 27 件、`Harness.Tests.ps1` 38 件、`UnityGate.Tests.ps1` 21 件、`UnityObservation.Tests.ps1` 16 件、`UnityTestRunner.Tests.ps1` 31 件、`contract-audit.tests.ps1` 29 件、`contract-audit.ps1`（違反なし）、`docs-audit.ps1`（検査 1・2 に違反なし。警告 2 件は harvest の頃合いで、base からある `PLAY_CONTENT_RESOLUTION_PROGRAM.md` と、§7・§8 が埋まった本書）。
- 動作確認を B の出口で取り直すかの判断: 取り直さない。V1（S4-2 の driver の `-DryRun`）は管理する file を書かない（`-DryRun` の `Set-ScenarioFile` は書かずに返る）。V2 は管理する file も `scenario-checkout.ps1` も使わない。eval の応答の読み方は変えていない。V1〜V3 は判定 C が GO 候補 head で取り直す。
- 未実行: V1〜V3（判定 C の担当）。Editor のコンパイル確認は対象なし（C# を変えていない）。
- Phase B 担当・モデル・ベンダー: Claude Opus 5.5（Anthropic）。新しい session。

### 動作確認

- 対象 head: `698340f7e13346326bca4ec37b6f5f9baf5dcac5`（implementation head）。この PC、Windows、Unity 6000.6.9f1、2026-11-26。
- 条件ごとの実行担当・環境・実行したコマンド: V1 と V2 の実行担当は Phase B の session（§0 の引継ぎ票のとおり、判定の入力にはしない）。V3 は判定 C の担当なので行っていない。
  - V1: `git worktree add --detach` で `D:\repositories\unity\osm-v\v1-X` と `v1-Y` を対象 head に作り、`scenario-checkout.ps1 -Operation register` で登録した。`git show 07cc2c97:tools/s42-player-overlay.ps1` を repo の外に書き出して `-XRoot <X> -YRoot <Y> -RunId v1old -DryRun`、続けて対象 head の `tools/s42-player-overlay.ps1` を `-RunId v1new -DryRun` で走らせた。前後の `git status --porcelain` と HEAD は driver の外で取った。
  - V2: 新しい GUID の token で `pwsh tools/Harness/scenario-editor-smoke.ps1 -RunId v2 -Only editor -Token <token>` を 1 回（基盤が `D:\repositories\unity\osm-v\editor-smoke-E` を対象 head で作った。`Library` は無い状態から）。
- 生の観測（合否は書かない）と保存先・状態:
  - V1: どちらも exit 0。`event` を持たず `step` を持つ行は新旧とも 24 行で、順は `X1,X2,X-LaunchPlayable,X-BuildSmoke,X-LaunchSmoke,X3,Y01…Y17,cleanup`。旧の行の 257 個の key は全て新の行にあり、型と値は同じだった。外した値は `utc`（どちらも ISO 8601）と cleanup の行の `cacheRoot`（旧 `D:\s42c\v1old`、新 `D:\ovr\s42\v1new\cache`）だけ。新の行に足した key は無い。新の `driver.jsonl` には基盤の行が 120 行ある。X と Y は前後とも status が空、HEAD は対象 head のまま。
  - V2: exit 0（00:13:55 〜 00:21:16 UTC）。`editor-start`（PID 14168、argv は `-projectPath <E>\unity -logFile <run の root>\base\editor\1\editor-1.log`、`-batchmode` なし）→ `editor-ready`（00:20:53、`evalPid` 14168。ready を待つ間の eval は 38 回で、最初は CLI が「No Pipeline instance found」を返した）→ `eval` read（値 `6000.6.9f1|D:\repositories\unity\osm-v\editor-smoke-E\unity`）→ `eval` send（1 回）→ `eval` read（値が token と完全に一致）→ `eval` exit が 3 回（5 秒おき。どれも応答なし）→ `process-exit`（00:21:14、PID 14168、signaled、終了コード 0、`forced` false）→ `restore`（3 つとも真）→ `in-use` cleared → `case-end`（`base` は `ok`）。read と send の原応答の印は record の `callId` と同じで、PID は 14168。`base/editor/1/` の `before.*` と `after.*` は status と index の差分が空で HEAD が同じ、`restore-before.status.txt` は ` M unity/Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` の 1 行（Editor の起動が書き換えた物を基盤が戻した）。終了の後、CIM の `Win32_Process` に Unity.exe は無く、checkout は clean で使用中の記録も無い。終了の stall は起きなかった（Editor は最初の終了の要求の約 16 秒後に終わった。Phase A の疎通では約 4.3 秒）。
  - 保存先: V1 は `v1-Y` の `TestResults/s42/v1old`・`v1new`、V2 はこの worktree の `TestResults/scenario/editor-smoke/v2`（どちらも ignore される local の置き場）。未保存（Artifact Storage には入れていない。B の session の観測なので判定の入力にしない）。
- 検証経路の期待と違う観測と、B への差し戻し: なし。
- `未実施` の条件と理由・担当: V3（全 EditMode 回帰）は Unity の test なので動作確認の対象外で、判定 C の担当が行う。動作確認の条件の未実施は なし。
- 残した物: 検証用の checkout `D:\repositories\unity\osm-v\v1-X`・`v1-Y`（V1 用、`Library` なし）と `editor-smoke-E`（V2 で import 済みの `Library` あり。V3 は同じ checkout を使う）。どれも登録済みで clean。判定 C が同じ head で使える。要らなくなったら `scenario-checkout.ps1 -Operation remove` で外す。

## 7. Phase C

### 発見 C（1 回目、2026-11-26）

- 種別: 発見
- 対象: implementation base `07cc2c97` から implementation head `698340f7` までの差分（commit で固定。HEAD `05963fa3` との差は `docs/handoff/` の HANDOFF だけであることを `git merge-base --is-ancestor` と `git diff --stat` で確かめた）。
- 使用したevidence bundle の取得案内・状態: 未生成（発見 C は commit で固定した差分を読んだ。§0 のとおり）。
- 開始時の照合: 渡された SHA・HEAD・clean・票の存在・implementation head 欄は一致した。PR #215 の状態ブロックは Phase A（`3a65d9bd`、凍結前）のままで、`引き継ぐ commit` が渡された SHA と違う。原因は A3 凍結の commit `74e02dad` 以降が origin に push されていないこと（origin の branch は `3a65d9bd`）で、別の commit を指す引継ぎではない（`3a65d9bd` は HEAD の祖先）。食い違いの原因が未 push だけと確かめられたので発見 C を進め、push と状態ブロックの更新は公開点の規則に従って owner の判断に残す。PR timeline の新着は 3 巡目のレビュー（改訂 4 で反映済み）が最後で、現 head 宛ての指摘は無い。
- 構造適合: §3 の責務マップと合う。依存の向きは `Scenario` → `ScenarioEditor`・`ScenarioUnity`・`ScenarioCheckout`・`ScenarioRun` → `UnityProcess`・`PathBudget` で、`ScenarioEditor` と `ScenarioUnity` は `ScenarioRun` を import しない。逆向きと循環は無い。`unity/` と `tools/unity-editor.ps1`・`.cmd` は差分に無い。`Scenario.psm1` の 763 行（予想約 240、警報 500）は、増分が §3 の公開面の行にある body の関数と、その record と台帳の扱いで、台帳・record・現在の case を private に持つために 1 module にした、という §6 の説明を受け入れる（責務は増えていない）。lease の置き場（共通の git dir）、`scenario-checkout.ps1` の `ScenarioRun` の import、test の file の分け方も §6 の説明のとおり B 適応の範囲と読む。
- 現在の問いを阻害する findings（B へ返す。どれも B 適応で直せる。新しい状態・依存・公開の関数は要らないと見る。要ると分かったら A 再開の条件（§4）で止める）:
  - **F1 補助の process を checkout を使う case で起動すると、終了を確かめないまま checkout を戻し、使用中の記録を消し、次の run に使わせる。** 違反: §2.4 の 11（「呼び出しの終わりに、基盤が自分の始めた process の終了を確かめる。確かめられなければ戻さない」）、N2（「終了を確かめていない process が使っている checkout を戻さない。…次の run にも使わせない」）、AC3（使用中の記録は process の PID を持ち、復元を確かめた後にだけ消す）、AC11（11 の行の offline test `Op11 …` は checkout を使わない case だけを見ていて、「戻さない」を示していない）、Harness README の「基盤が守ること」の 2 つ目（補助の process を、終了を確かめるまで戻さない process に挙げている）。経路: `Confirm-ScenarioCaseProcesses` が `helper` を飛ばす（`Scenario.psm1` の 458 行）ので、case の終わりに復元と `Clear-ScenarioCheckoutInUse` が通る。呼び出しの終わりの `Complete-ScenarioInvocationProcesses` は未確認の補助の process で非 0 にするだけで、隔離を置かない。`Invoke-Scenario` が例外で抜けた時は、補助の process の確認も record も無い。再現（発見 C が一時の写しの suite で offline に実行し、写しは消していない scratch に置いた）: 登録した checkout `E` を使う case で、終わらない偽の補助の process を `Start-ScenarioHelper -WorkingDirectory <E>` で起動 → driver は exit 1、偽の process は生きたまま、`in-use.json` は無く、event は `in-use`（placed）→ `helper-start` → `restore` → `in-use`（cleared）→ `case-end` → `process-exit`（signaled 偽）。続けて別の RunId の run が同じ `E` で exit 0 で通った。直し方は B が決めて §6 に記録する（例: checkout を使う case で起動した補助の process は、その case の復元の前に終了を確かめ、確かめられなければ他の process と同じく隔離する。checkout を使わない case の補助の process で、呼び出しの終わりに終了を確かめられない時に、どの checkout を使わせないかも決める）。checkout を使う case の補助の process が終わらない時に checkout が隔離されることを見る offline test を足す。
  - **F2 隔離の解除の後の続きの実行が、前の「戻していない」record を指す行を残さない。** 違反: AC3 の最後の項（「解除の後の `-Only` の続きの実行は、新しい試行として通り、前の「戻していない」record を指す行を残す」）。新しい試行（attempt 2）にはなるが、`run-start`・`case-start` のどちらにも前の試行の `quarantine` を指す欄が無く（`Scenario.psm1` の 276・330 行）、AC15 の test も attempt が 2 であることしか見ていない。§6 の「HANDOFF との差」にも無い。前の試行の `quarantine` の record（case と試行の番号、または `base/<case>/<試行>/quarantine.*` の path）を指す欄か行を足し、AC15 の test でその値を見る。
  - **F3 `scenario-checkout.ps1 -Operation remove` が、`git worktree remove` の前に lease を離す。** 違反: AC9（「4 つとも、その checkout の lease を取ってから行う（走っている run の checkout には触れない）」）。`scenario-checkout.ps1` の 56 行が事前確認の後に `$lease.Dispose()` してから外すので、その間に別の呼び出しが lease を取って事前確認を通れる。comment の理由（lease の file は共通の git dir にあり worktree と一緒には消えない）は、lease を持ったまま外せることを示しており、先に離す理由にならない。外し終わるまで lease を持つ。
- 後続スライスへ移送する findings: なし。
- 返却時に B が併せて見てよい点（凍結した条件の違反とは読まない。直すかは B が決める）: 同じ RunId を `-Only` なしで走らせ直すと、基盤が用意した checkout の HEAD を対象の commit へ切り替え（`Open-ScenarioInvocation` の 262 行）、その後の `Open-ScenarioRun` で「run の root が既にあります」と拒む。拒否は AC4 のとおりで、切り替えるのは lease を持った clean な検証用の checkout なので N1 も破らないが、run の root の有無と印の照合を checkout の書き込みより前に済ませれば、拒む run が何も書かない形にできる。
- 実行したテストコマンドと `-Filter`、対象を選んだ理由: §5 の判定必須の 1・2 を、implementation head と tree が同じ HEAD `05963fa3` で、filter なしで実行した（差し戻しの前の最初の発見 C なので全件。どれも Unity を起動しない）。F1 の再現は、`ScenarioUnity.Tests.ps1` の写しに 1 件を足し、`-Filter 'ZZ probe'` で走らせた（写しは scratch。repo には足していない）。
- テスト結果（件数は各 suite の出力の行。全て exit 0）: `Scenario.Tests.ps1` 20 件、`ScenarioDriver.Tests.ps1` 12 件、`ScenarioUnity.Tests.ps1` 25 件、`Harness.Tests.ps1` 38 件、`UnityGate.Tests.ps1` 21 件、`UnityObservation.Tests.ps1` 16 件、`UnityTestRunner.Tests.ps1` 31 件、`contract-audit.tests.ps1` 29 件、いずれも失敗 0。`contract-audit.ps1` は error 0・warning 0。`docs-audit.ps1` は error 0、警告 1 件（base からある `PLAY_CONTENT_RESOLUTION_PROGRAM.md` の harvest の頃合い）。F1〜F3 はどれも既存の test が見ていない経路なので、test が通ることは指摘と矛盾しない。
- 判定必須のうち未実行: §5 の 3〜5（V1・V2・V3）。発見 C では行わない（判定 C の担当。差し戻しが決まったので、判定用の検証一式も行わない）。
- 重い検証を発見段階で限定実行した場合の理由と範囲: なし。
- 未確認事項: 実の検証用の checkout での `-DryRun` は行っていない（AC8 の互換は offline test と、判定 C の V1 が見る）。
- 巡: B と発見 C の差し戻しの 1 巡目（上限 4。最初の動作確認は数えていない）。
- 担当・モデル: Claude Opus 5.5（Anthropic）。新しい session。独立性制約あり（owner 受容。Phase B と同じ系列・モデル）。

### 発見 C（2 回目、2026-11-26）

- 種別: 発見（B と発見 C の差し戻しの 1 巡目の修正を読む）
- 対象: implementation base `07cc2c97` から implementation head `c3f02632` までの差分（commit で固定）。重点は 1 巡目の修正 `698340f7..c3f02632`（`Scenario.psm1`・`scenario-checkout.ps1`・Harness README・2 本の test。`unity/` と `tools/unity-editor.ps1`・`.cmd` は差分に無い）。
- 使用したevidence bundle の取得案内・状態: 未生成（commit で固定した差分を読んだ）。
- 開始時の照合: 渡された SHA `8f6b553f` と HEAD が一致し、作業ツリーは clean、HEAD の HANDOFF に票がある。implementation head 欄 `c3f02632` は HEAD の祖先で、HEAD との差分は `docs/handoff/SCENARIO_VERIFICATION_BASE.md` だけだった。PR #215 の状態ブロックの `引き継ぐ commit` と implementation base / head は、渡された SHA と §0 の欄に一致した（origin の branch は `3a65d9bd` のままで未 push。同じ PC なのでローカルの SHA で照合した）。PR timeline の新着は 3 巡目の外部計画レビュー（`3a65d9bd` 宛て、改訂 4 で反映済み）が最後で、現 head 宛ての指摘は無い。
- 1 回目の指摘の確認:
  - **F1 直った。** `Confirm-ScenarioCaseProcesses` が飛ばす補助の process は checkout を使わない case の物だけになり（`Scenario.psm1` の 490 行）、checkout を使う case の補助の process は復元の前に終了を確かめる。確かめられなければ今ある隔離の経路（戻さない、`quarantine` の行、使用中の記録に `Register-ScenarioProcess` が足した PID が残る）に入る。checkout を使わない case の物は呼び出しの終わり（`Invoke-Scenario` の例外の経路を含め、lease を離す `Close-ScenarioInvocation` より前）に確かめ、確かめられなければ呼び出しの全ての checkout に使用中の記録を置く（`Set-ScenarioHelperQuarantine`）。記録の書き込みの失敗でも隔離が残る順（使用中の記録が先、`driver.jsonl` が後）を確かめた。`Confirm-ScenarioCaseProcesses` の途中で記録の書き込みが例外になった時も、復元と `Clear-ScenarioCheckoutInUse` の前で抜けるので checkout は隔離のまま残り、まだ記録していない補助の process は呼び出しの終わりの確認に回る（`Recorded` で重複を避ける）。N2・AC3・§2.4 の 11 を満たす。§2.4 の 11 の「呼び出しの終わりに確かめる」に対し、checkout を使う case の物を case の終わりに確かめるのは確認を早めるだけで、Harness README と `Start-ScenarioHelper` の comment に書かれている（B 適応）。`-Only` をまたがない呼び出しの間の補助の process（S4-3b）は、checkout を使わない case で起動すれば今までどおり使えるので、拡張点を塞がない。test `Op11 AC3 a helper started in a checkout case …` と `Op11 AC3 an unexited helper from a case without a checkout …` は、bytes・使用中の記録の PID・event の有無と順・次の run の拒否・解除・例外の経路を直接見ている。
  - **F2 直った。** 続きの実行の `run-start` に `priorQuarantines`（行の番号・case・試行・checkout・kind・PID・reason・restored・`base/<case>/<試行>`）がある。§3 の record の契約の名前と意味は変えず、欄の追加（B 適応の範囲）。AC15 の test が、指す行が前の試行の `quarantine` であることと、指す置き場に `quarantine.status.txt` があることを見る。AC3 の最後の項を満たす。
  - **F3 直った。** `remove` は `git worktree remove` が終わるまで lease を持ち、lease は `finally` で離す。test `AC9 remove holds the lease until the worktree is removed` が、外す直前に別の呼び出しとして lease を取れないことを見る。AC9 を満たす。
  - 返却時に見てよい点（同じ RunId を `-Only` なしで走らせ直すと checkout を切り替えてから拒む）も直り、事前確認で、書き込みの前に止まる（AC1 の順のとおり）。拒否の条件は AC4 のまま。
- 構造適合: 1 回目の判断を変えない。修正で増えたのは `Scenario.psm1` の private の関数 2 つ（`Get-ScenarioPriorQuarantines`、`Set-ScenarioHelperQuarantine`）で、公開の関数・module・依存・状態の置き場は増えていない（837 行。§6 の説明の範囲）。
- 現在の問いを阻害する findings: なし。
- 後続スライスへ移送する findings（凍結した条件の違反とは読まない）:
  - **S4-3c へ: checkout を使わない case の外部 command の終了を確かめられない時、どの checkout も隔離しない。** `Invoke-ScenarioCommand` は checkout を使わない case でも使え、期限までに終わらず止めない時は、case の終わりに `quarantine`（`checkout` が null）の行を書いて呼び出しを止めるが、使用中の記録をどの checkout にも置かないので、次の run は checkout を使える。凍結した AC3 の「その checkout を戻さない」は case の checkout についての文で、checkout を使わない case には当たらず、§2.1 は checkout を使わない case を複写と片付けとしている。今ある利用者（S4-2、疎通用の scenario）は外部 command を使わない。ただし 1 巡目の F1 の修正の判断（どの checkout に触れたかを基盤が知らない時は全てを隔離する）と同じ形なので、hook の実の使い方を持つ S4-3c が、checkout を使わない case での外部 command の扱い（補助の process と揃えるか、checkout を使う case に限るか）を決める。
- 実行したテストコマンドと `-Filter`、対象を選んだ理由: §5 の判定必須の 1・2 を、implementation head と `tools/` の tree が同じ HEAD `8f6b553f` で、filter なしで実行した（修正が `Scenario.psm1` の共通の経路（case の終わりの確認、呼び出しの終わり、`run-start`、事前確認）に触れるので、起点の 2 本に限らず全件にした。どれも Unity を起動しない）。
- テスト結果（件数は各 suite の出力の行。全て exit 0、失敗 0）: `Scenario.Tests.ps1` 22 件、`ScenarioDriver.Tests.ps1` 12 件、`ScenarioUnity.Tests.ps1` 27 件、`Harness.Tests.ps1` 38 件、`UnityGate.Tests.ps1` 21 件、`UnityObservation.Tests.ps1` 16 件、`UnityTestRunner.Tests.ps1` 31 件、`contract-audit.tests.ps1` 29 件。`contract-audit.ps1` は違反なし。`docs-audit.ps1` は検査 1・2 に違反なし、警告 1 件（base からある `PLAY_CONTENT_RESOLUTION_PROGRAM.md` の harvest の頃合い）。
- 動作確認を取り直すかの判断: 取り直さない（§6 の B の判断を支持する）。修正が V1・V2 の経路に足したのは、`run-start` の基盤の行の欄 1 つ（V1 が比べるのは `event` を持たない S4-2 の行）と、`-Only` なしの事前確認の run の root の有無（V1・V2 は新しい RunId）だけで、補助の process と `remove` は V1・V2 が使わない。V1〜V3 は判定 C が GO 候補 head で行う。
- 判定必須のうち未実行: §5 の 3〜5（V1・V2・V3）。判定 C の担当。
- 重い検証を発見段階で限定実行した場合の理由と範囲: なし。
- 未確認事項: 修正の前の code で新しい test が落ちること（§6 の B の記録）は、発見 C では取り直していない。
- 巡: B と発見 C の差し戻しの 1 巡目の後のレビュー。阻害する指摘が無いので、差し戻しは 1 巡で終わる（上限 4）。
- 担当・モデル: Claude Opus 5.5（Anthropic）。新しい session。独立性制約あり（owner 受容。Phase B と同じ系列・モデル）。

### 判定 C（2026-11-26）

- 種別: 判定
- 対象: implementation base `07cc2c97`、GO 候補 head `c3f02632`（implementation head 欄）。
- 開始時の照合: 渡された SHA `8a30c0e2` と HEAD が一致し、作業ツリーは clean、HEAD の本書に票がある。implementation head `c3f02632` は HEAD の祖先で、HEAD との差分は本書だけだった。PR #215 の状態ブロックの `引き継ぐ commit` と implementation base / head は、渡された SHA と §0 の欄に一致した（origin の branch は `3a65d9bd` のままで未 push。同じ PC なのでローカルの SHA で照合した）。PR timeline の新着は 3 巡目の外部計画レビュー（`3a65d9bd` 宛て）が最後で、現 head 宛ての指摘は無い。
- 使用したevidence bundle の取得案内・状態: §0 の evidence bundle（ローカル固定済み、未昇格。取得の確認済み）。
- 構造適合: 発見 C の判断を変えない。依存の向き（`Scenario` → 4 module → `UnityProcess`・`PathBudget`、`ScenarioEditor`・`ScenarioUnity` は `ScenarioRun` を import しない）を import の行で、`unity/` と `tools/unity-editor.ps1`・`.cmd` が差分に無いことを `git diff --stat` で確かめた。
- 判定必須の実行と原記録の観測（実行担当は判定 C の session。この PC、Windows 11、Unity 6000.6.9f1）:
  1. offline（`c3f02632` と `tools/` の tree が同じ HEAD `8a30c0e2` で、filter なし。全て exit 0、失敗 0）: `Scenario.Tests.ps1` 22 件、`ScenarioDriver.Tests.ps1` 12 件、`ScenarioUnity.Tests.ps1` 27 件、`Harness.Tests.ps1` 38 件、`UnityGate.Tests.ps1` 21 件、`UnityObservation.Tests.ps1` 16 件、`UnityTestRunner.Tests.ps1` 31 件、`contract-audit.tests.ps1` 29 件。
  2. `contract-audit.ps1` は違反なし。`docs-audit.ps1` は検査 1・2 に違反なし、警告 1 件（base からある `PLAY_CONTENT_RESOLUTION_PROGRAM.md` の harvest の頃合い）。
  3. V1: `v1-X`・`v1-Y` を `git checkout --detach c3f02632` で GO 候補 head に合わせ、base の driver（`git show` で書き出した物）を `-RunId j1old -DryRun`、GO 候補 head の driver を `-RunId j1new -DryRun` で走らせた（どちらも exit 0）。`step` を持ち `event` を持たない行は新旧とも 24 行で、順は `X1,X2,X-LaunchPlayable,X-BuildSmoke,X-LaunchSmoke,X3,Y01…Y17,cleanup`。旧の 257 個の key は全て新の行にあり、型と値が同じ。外した値は `utc`（どちらも ISO 8601）と cleanup の行の `cacheRoot`（旧 `D:\s42c\j1old`、新 `D:\ovr\s42\j1new\cache`。それぞれの規則に合う）だけ。新の行に足した key は無く、基盤の行は 120 行。driver の外で取った前後の `git status --porcelain` は X・Y とも空で、HEAD は `c3f02632` のまま。
  4. V2: 新しい GUID の token で `scenario-editor-smoke.ps1 -RunId j1 -Commit c3f02632… -Only editor -Token <token>`（exit 0、01:21:35〜01:22:28 UTC）。`editor-start`（PID 43504、`-batchmode` なし、`-projectPath` は `editor-smoke-E\unity`）→ `editor-ready`（`evalPid` 43504。ready を待つ間の読む eval は 4 回）→ `eval` read → `eval` send（1 回）→ `eval` read → `eval` exit 2 回 → `process-exit`（PID 43504、signaled、終了コード 0、`forced` false）→ `restore`（3 つとも真）。原応答の `OSMEVAL` の印は record の `callId` と同じで PID は 43504、版と path は `6000.6.9f1|D:\repositories\unity\osm-v\editor-smoke-E\unity`、読み直した値は token と完全に一致した。`base/editor/1/` の `before.*` と `after.*` は status と index の差分が空で HEAD が同じ。`restore-before.status.txt` も空だった（B の動作確認の時と違い、import 済みの `Library` では URP の設定が書き換わらなかった）。
  5. V3: V2 と同じ RunId で `scenario-editor-smoke.ps1 -RunId j1 -Commit c3f02632… -Only tests`（filter なし。sandbox 外で起動。exit 0、01:22:48〜01:37:07 UTC）。step は `outputRoot`（`base/tests/1/runner-1`）の下に保存され、`stepIsCurrent` 真。`argv` は `-nographics` あり・`-testFilter` なし、`platform` EditMode、`projectPath` は `editor-smoke-E\unity`、`requestedVersion` 6000.6.9f1、process の開始は case の開始の後。XML は total 2443、Passed 2383、Failed 0、Inconclusive 25、Skipped 35 で、root の件数と test の数が合う。step の `failure` は `Inconclusive のテストがあります` と `Unity process が正常終了しませんでした: 2` の 2 行、`compileErrors` は空。終了の stall は起きず、終了の後に CIM の `Win32_Process` に Unity.exe は無く、`editor-smoke-E` は clean で使用中の記録も無い。
- 照合 script（§5 の「判定 C の offline 実行」。資格情報・通信に触れない）: 判定 C が書き、evidence bundle の `judge/` に置いた。原記録の形に合わせた読み方の対応は次のとおりで、fixture は機械的にこの形へ写してから同じ読み方で通した。V2: `case-before/`・`before-restore/`・`after-restore/` の `status.txt`・`head.txt`・`index-diff.txt`（・`worktree-diff.patch`）→ `before.*`・`restore-before.*`・`after.*`（`.status.txt`・`.head.txt`・`.index.diff`・`.work.diff`）。eval の原応答 `{exitCode, stdout, stderr}`（`stdout` は `{callId, pid, match, value}`）→ `{callId（送った code の印）, kind, sentUtc, exitCode, timedOut, output}`（`output` は `OSMEVAL|<callId>|<pid>|ok|<値の UTF-8 の Base64>`、`match` 偽は `wrong-target`。最初の読む eval の値は `<版>|<path>`）。eval の file は record の `responsePath` で読む。token は実行の担当の note から読む。V1・V3 は写しが要らなかった（V3 の runner の出力の置き場は record の `outputRoot`・`stepPath` で決まる）。Phase A snapshot の 59 組の全てが `expectations.json` の受理・拒否と一致し、拒否の組はどれも `reason` に当たる理由で拒否された（別の理由が併せて出た組は、行の欠け・足しに続く行のずれと、許さない `failure` の行が併せて出る物だけ）。その後に原記録の複製へ当て、V1・V2・V3 とも受理だった。
- V3 の期待集合: §5 のとおり、S4-2 の判定 C の evidence bundle（run `j1`、head `df1d731c`）の `full-editmode/` を S4-2 の取得案内で取得した（total 2443、Passed 2383、Failed 0、Inconclusive 25、Skipped 35）。今回の XML の fullname の集合と数、test ごとの result はこれと同じで、§2.7 の例外（`Directory.Move` の `Failed`）に当たる test は 0 件。
- 受け入れ条件の判定: AC1〜AC6・AC9・AC11・AC12・AC15 は 1 の offline test（名前と AC の対応は §6）、AC7 は 1 の既存 4 本の suite と V3、AC8 は 1 の互換の test と V1、AC10 は V2、AC13 は Harness README の節と workflow の 1 文、AC14 は 2 と CI の `harness-tests` job の 3 本で満たす。§2.4 の (a)(b) の全ての行に offline test か実機の経路がある（AC11）。動作確認の `未実施` は無い（V1〜V3 を GO 候補 head で判定 C の実行担当が取った）。
- 現在の問いを阻害する findings: なし。
- 後続スライスへ移送する findings: なし（発見 C の 2 回目の移送 1 件は §7 のとおり）。
- 判定: GO（AC1〜AC15 を満たす。§2.7 の判定定義）。高コスト証拠は無いので、C' の 1 巡目が最終 C'。
- 判定必須のうち未実行: なし。
- 未確認事項: Phase A snapshot の取得案内は、票が指す手元の置き場に無かった（主担当の session の stage の出力から取得し、§0 のとおり再掲した）。
- 担当・モデル: Claude Opus 5.5（Anthropic）。新しい session。判断と実行を同じ session が行った。独立性制約あり（owner 受容。Phase B・発見 C と同じ系列・モデル）。

### 発見 C（3 回目、2026-11-26）

- 種別: 発見（B と発見 C の差し戻しの 2 巡目の修正を読む）
- 対象: implementation base `07cc2c97` から implementation head `c38e0762` までの差分（commit で固定）。重点は 2 巡目の修正 `c3f02632..c38e0762`（`scenario-checkout.ps1`・`ScenarioRun.psm1`・Harness README・2 本の test。`unity/` と `tools/unity-editor.ps1`・`.cmd` は base からの差分に無い）。
- 使用したevidence bundle の取得案内・状態: 未生成（commit で固定した差分を読んだ）。判定 C の head `c3f02632` の evidence bundle と C' blind bundle は、[判定後の head 変更](../../.agents/skills/osm-workflow/references/review-evidence.md#判定後の-head-変更) のとおり新しい head の証拠にしない。
- 開始時の照合: 渡された SHA `5e920703` と HEAD が一致し、作業ツリーは clean、HEAD の本書に票がある。implementation head 欄 `c38e0762` は HEAD の祖先で、HEAD との差分は本書だけだった。PR #215 の状態ブロックの `引き継ぐ commit` と implementation base / head は、渡された SHA と §0 の欄に一致した（同じ PC なのでローカルの SHA で照合した）。PR timeline の新着は、票が指す 2026-11-26 03:18〜03:22（UTC）の 3 件が最後で、現 head 宛ての新しい指摘は無い。
- 2 巡目の修正の確認:
  - **P1 直った。** `unreserve` は、印・隔離・予約の有無を見た後、他の 3 つの操作と同じ `Get-ScenarioCheckoutProblems`（最上位、linked、同じ共通の git dir、印の repo、detached、進行中の git の操作、clean、project lock、path 予算）を通し、`別の run の予約があります` だけを拒否の理由から除く（`scenario-checkout.ps1` の 82〜86 行）。別の repo の checkout は共通の git dir の比較で拒まれるので、呼び出し元の repo の lease が対象の run の lease と重ならない経路は塞がれた。同じ repo では lease が同じ file なので、走っている run の checkout には触れない。AC9 の「4 つとも、その checkout の lease を取ってから行う（走っている run の checkout には触れない）」と N1 を満たす。管理する file は ignore される file に限られる（`AC4 ... tracked file accepted as managed`）ので、clean の条件は解除を妨げない。`release` が見る「project を指す process」の確認を足さない B の判断は、AC4 の予約の解除の条件に無いので受け入れる。
    - 発見 C が追加で確かめた経路: lease の key は path の文字列（小文字にした絶対 path の SHA-256）なので、同じ checkout を別の綴りで指すと lease が分かれないかを、一時の写しの probe で offline に見た。junction 経由の path は、git の `--show-toplevel` が実の path を返すので最上位でないとして拒まれた。8.3 の短い名前は最上位として通るが、`[IO.Path]::GetFullPath` が長い名前に直すので lease は同じ file になり、正規の path の lease を持ったまま短い名前で `unreserve` を呼ぶと `別の呼び出しがこの checkout の lease を持っています` で拒まれ、管理する file と予約は変わらなかった。指摘にしない。
  - **P2 直った。** `Write-ScenarioManagedFile` は書く前の bytes の file を消さず上書きせず、記録の件数から始めて file の無い番号を選ぶ。初めての run では今までと同じ名前になる。記録の照合を `OrdinalIgnoreCase` にしたので、大小文字だけが違う path は 1 つの管理する file になり、記録の `path` は最初の綴りのまま戻される。AC4 の「寿命は run」と `Restore-ScenarioManagedFiles` の comment に合う。CI の `harness-tests` は `windows-latest` で、大小文字を同一視する前提は実行環境と合う。
  - **cursor の指摘（空の Base64）を直さない判断を支持する。** pwsh 7.7.0 と Windows PowerShell 5.1 の両方で `[Convert]::FromBase64String("")` が長さ 0 の配列を返すことを発見 C も確かめた。正規表現の値の group は `*` なので空に一致し、`ConvertFrom-ScenarioEvalResponse` は空文字を返す。足した assert（`ScenarioUnity.Tests.ps1` の `AC12 eval code checks the PID first ...`）がこの経路を見る。
  - PR 本文の古い説明は票のとおり Phase D で直す。ここでは触れていない。
- 足した offline test が修正の経路を落とせるか: `c3f02632` の一時の detached worktree に `c38e0762` の `Scenario.Tests.ps1` を写して走らせた（一時の worktree は消した）。`AC9 AC4 unreserve refuses a checkout of another repo ...` は `unreserve from another repo` の assert で落ちた。`AC4 a managed file can be written again ...` は大小文字の assert（`a case-only variant of the path became a second managed file`）で先に落ちたので、写しの `rewrite` の path を同じ綴りに替えて走らせ直し、`second write` の assert（`CreateNew` が既にある `0.before` で落ちる）で落ちることを確かめた。どちらも修正の後の code では通る。§6 の 2 回目・1 回目の記録の未確認（修正の前の code で落ちること）は、この 2 件について解消した。
- 構造適合: 前回までの判断を変えない。修正で公開の関数・module・依存・状態の置き場は増えていない（`ScenarioRun.psm1` 391 行、`scenario-checkout.ps1` 105 行）。依存の向きは import の行で前回と同じことを確かめた。
- 現在の問いを阻害する findings: なし。
- 後続スライスへ移送する findings: なし。
- 実行したテストコマンドと `-Filter`、対象を選んだ理由: §5 の判定必須の 1・2 を、implementation head と `tools/` の tree が同じ HEAD `5e920703` で、filter なしで実行した（どれも Unity を起動しない）。修正前の code での落ち方の確認は、上の一時の worktree で `-Filter` に 2 件の test 名を渡した。lease の別名の probe は scratch の script（repo には足していない）。
- テスト結果（件数は各 suite の出力の行。全て exit 0、失敗 0）: `Scenario.Tests.ps1` 24 件、`ScenarioDriver.Tests.ps1` 12 件、`ScenarioUnity.Tests.ps1` 27 件、`Harness.Tests.ps1` 38 件、`UnityGate.Tests.ps1` 21 件、`UnityObservation.Tests.ps1` 16 件、`UnityTestRunner.Tests.ps1` 31 件、`contract-audit.tests.ps1` 29 件。`contract-audit.ps1` は違反なし。`docs-audit.ps1` は error 0、警告 2 件（harvest の頃合い。base からある `PLAY_CONTENT_RESOLUTION_PROGRAM.md` と、§7・§8 が埋まった本書）。
- 動作確認を取り直すかの判断: 発見 C では取り直さない（§6 の 2 巡目の B の判断のとおり、V1・V2 は管理する file も `scenario-checkout.ps1` も使わない）。implementation head が `c3f02632` から変わったので、判定 C は新しい head で判定必須の 1〜5（V1〜V3 を含む）を取り直し、evidence bundle と C' blind bundle を作り直す。
- 判定必須のうち未実行: §5 の 3〜5（V1・V2・V3）。判定 C の担当。
- 重い検証を発見段階で限定実行した場合の理由と範囲: なし。
- 未確認事項: なし。
- 巡: B と発見 C の差し戻しの 2 巡目の後のレビュー。阻害する指摘が無いので、差し戻しは 2 巡で終わる（上限 4）。
- 担当・モデル: Claude Opus 5.5（Anthropic）。新しい session。独立性制約あり（owner 受容。Phase B と同じ系列・モデル）。

### 判定 C（2 回目、2026-11-26）

- 種別: 判定
- 対象: implementation base `07cc2c97`、GO 候補 head `c38e0762`（implementation head 欄）。`c3f02632` の判定 C の結果と bundle は流用していない（[判定後の head 変更](../../.agents/skills/osm-workflow/references/review-evidence.md#判定後の-head-変更)）。
- 開始時の照合: 渡された SHA `fab27c76` と HEAD が一致し、作業ツリーは clean、HEAD の本書に票がある。implementation head `c38e0762` は HEAD の祖先で、HEAD との差分は本書だけだった。PR #215 の状態ブロックの `引き継ぐ commit` と implementation base / head は、渡された SHA と §0 の欄に一致した（同じ PC なのでローカルの SHA で照合した）。PR timeline の新着は 2026-11-26 03:18〜03:22（UTC）の 3 件（§6 の 2 巡目の入力）が最後で、現 head 宛ての新しい指摘は無い。
- 使用したevidence bundle の取得案内・状態: §0 の head `c38e0762` の evidence bundle（ローカル固定済み、未昇格。取得の確認済み）。
- 構造適合: 発見 C の判断を変えない。依存の向き（`Scenario` → 4 module → `UnityProcess`・`PathBudget`、`ScenarioEditor`・`ScenarioUnity` は `ScenarioRun` を import しない、`scenario-checkout.ps1` は `UnityProcess`・`ScenarioRun`・`ScenarioCheckout`）を head の import の行で、`unity/` と `tools/unity-editor.ps1`・`.cmd` が base からの差分に無いことを `git diff --stat` で確かめた。
- 判定必須の実行と原記録の観測（実行担当は判定 C の session。この PC、Windows 11、Unity 6000.6.9f1）:
  1. offline（`c38e0762` と `tools/` の tree が同じ HEAD `fab27c76` で、filter なし。全て exit 0、失敗 0）: `Scenario.Tests.ps1` 24 件、`ScenarioDriver.Tests.ps1` 12 件、`ScenarioUnity.Tests.ps1` 27 件、`Harness.Tests.ps1` 38 件、`UnityGate.Tests.ps1` 21 件、`UnityObservation.Tests.ps1` 16 件、`UnityTestRunner.Tests.ps1` 31 件、`contract-audit.tests.ps1` 29 件。
  2. `contract-audit.ps1` は error 0・warning 0。`docs-audit.ps1` は検査 1・2 に違反なし、警告 2 件（harvest の頃合い。base からある `PLAY_CONTENT_RESOLUTION_PROGRAM.md` と、§7・§8 が埋まった本書）。
  3. V1: `v1-X`・`v1-Y` を `git checkout --detach c38e0762` で GO 候補 head に合わせ、base の driver（`git show` で書き出した物）を `-RunId j2old -DryRun`、GO 候補 head の driver を `-RunId j2new -DryRun` で走らせた（どちらも exit 0）。`step` を持ち `event` を持たない行は新旧とも 24 行で、順は `X1,X2,X-LaunchPlayable,X-BuildSmoke,X-LaunchSmoke,X3,Y01…Y17,cleanup`。旧の 257 個の key は全て新の行にあり、型と値が同じ。外した値は `utc`（どちらも ISO 8601）と cleanup の行の `cacheRoot`（旧 `D:\s42c\j2old`、新 `D:\ovr\s42\j2new\cache`。それぞれの規則に合う）だけ。新の行に足した key は無く、基盤の行は 120 行。driver の外で取った前後の `git status --porcelain` は X・Y とも空で、HEAD は `c38e0762` のまま。
  4. V2: 新しい GUID の token で `scenario-editor-smoke.ps1 -RunId j2 -Commit c38e0762… -Only editor -Token <token>`（sandbox 外で起動。exit 0、03:53:18〜03:54:25 UTC）。基盤が `editor-smoke-E` を `c3f02632` から `c38e0762` へ切り替えた。`editor-start`（PID 9112、`-batchmode` なし、`-projectPath` は `editor-smoke-E\unity`）→ `editor-ready`（`evalPid` 9112。ready を待つ間の読む eval は 5 回）→ `eval` read → `eval` send（1 回）→ `eval` read → `eval` exit 2 回 → `process-exit`（PID 9112、signaled、終了コード 0、`forced` false）→ `restore`（3 つとも真）。原応答の `OSMEVAL` の印は record の `callId` と同じで PID は 9112、版と path は `6000.6.9f1|D:\repositories\unity\osm-v\editor-smoke-E\unity`（`ProjectVersion.txt` と一致）、読み直した値は token と完全に一致した。`base/editor/1/` の `before.*` と `after.*` は status と index の差分が空で HEAD が同じ。`restore-before.status.txt` も空だった。
  5. V3: V2 と同じ RunId で `scenario-editor-smoke.ps1 -RunId j2 -Commit c38e0762… -Only tests`（filter なし。sandbox 外で起動。exit 0、03:54:47〜04:08:58 UTC）。step は `outputRoot`（`base/tests/1/runner-1`）の下に保存され、`stepIsCurrent` 真。`argv` は `-nographics` あり・`-testFilter` なし、`platform` EditMode、`projectPath` は `editor-smoke-E\unity`、`requestedVersion` 6000.6.9f1、process の開始は case の開始の後。XML は total 2443、Passed 2383、Failed 0、Inconclusive 25、Skipped 35 で、root の件数と test の数が合う。step の `failure` は `Inconclusive のテストがあります` と `Unity process が正常終了しませんでした: 2` の 2 行、`compileErrors` は空。終了の stall は起きず（実行の担当は process を止めていない）、終了の後に CIM の `Win32_Process` に Unity.exe は無く、`editor-smoke-E` は clean で使用中の記録も無い（git dir の `osm-scenario/` は印の `checkout.json` だけ）。
- 照合 script（§5 の「判定 C の offline 実行」。資格情報・通信に触れない）: 1 回目の判定 C が書いた `S4vJudge.psm1`・`run-fixtures.ps1`・`run-raw.ps1` を、旧 head の evidence bundle から取得し、読んだ上で変えずにこの判定の script として使った（原記録の形は `c3f02632` から変わっていない。2 巡目の修正は `base/` の file 名と eval の原応答の形に触れていない）。原記録の形への読み方の対応は 1 回目の判定 C の記録のとおり。Phase A snapshot を取得案内（`phase-a.md`）で取得し直して 5 file が前の物と同じ bytes であることを確かめ、59 組を通した: 全てが `expectations.json` の受理・拒否と一致し、拒否の組はどれも `reason` に当たる理由で拒否された。その後に原記録の複製へ当て、V1・V2・V3 とも受理だった。script・fixture の結果・原記録の結果は evidence bundle の `judge/` にある。
- V3 の期待集合: §5 のとおり、S4-2 の判定 C の evidence bundle（run `j1`、head `df1d731c`）の `full-editmode/` を S4-2 の取得案内で取得し直した（1 回目の判定 C が使った物と同じ bytes。total 2443、Passed 2383、Failed 0、Inconclusive 25、Skipped 35）。今回の XML の fullname の集合と数、test ごとの result はこれと同じで、§2.7 の例外（`Directory.Move` の `Failed`）に当たる test は 0 件。
- 受け入れ条件の判定: AC1〜AC6・AC9・AC11・AC12・AC15 は 1 の offline test（名前と AC の対応は §6。2 巡目の修正の test `AC9 AC4 unreserve refuses …` と `AC4 a managed file can be written again …` を含む）、AC7 は 1 の既存 4 本の suite と V3、AC8 は 1 の互換の test と V1、AC10 は V2、AC13 は Harness README の節と workflow の 1 文、AC14 は 2 と CI の `harness-tests` job の 3 本で満たす。§2.4 の (a)(b) の全ての行に offline test か実機の経路がある（AC11）。高コスト証拠を除く操作・実行時の条件（V1〜V3）の証拠は GO 候補 head で揃い、判定 C の実行担当が取った。
- 現在の問いを阻害する findings: なし。
- 後続スライスへ移送する findings: なし（発見 C の 2 回目の移送 1 件は §7 のとおり）。
- 判定: GO（AC1〜AC15 を満たす。§2.7 の判定定義）。高コスト証拠は無いので、C' の 2 巡目が最終 C'。
- 判定必須のうち未実行: なし。
- 未確認事項: なし。
- 担当・モデル: Claude Opus 5.5（Anthropic）。新しい session。判断と実行を同じ session が行った。独立性制約あり（owner 受容。Phase B・発見 C と同じ系列・モデル）。

## 8. Phase C'

### C'（1 巡目、2026-11-26）

C' の担当は本書を読まないので、進行役が C' の最終回答（英語）を転記した。判断は C' の担当のもので、進行役は足していない。

- 判定: **GO**。head `c3f02632` の変更は、凍結した checkout・lease・使用中の記録・隔離・process の終了の確認・case の順序の規則を守り、V1・V2・V3 の原記録は §2.7 の読み方で §5 を満たす。
- 入力: C' blind bundle（取得案内 `cprime.md`）。進行役が `fetch` し、manifest の 23 file 全ての SHA-256 の一致を確かめ、発見 C・判定 C の所見と判定 C の照合 script が入っていないことを grep で確かめた。bundle の file は加工していない。workspace には bundle の写しと、zip を展開した写しと、head `c3f02632` の `tools/`・`.agents/skills/osm-workflow/`・`AGENTS.md`（本書を除く）を置いた。C' 用決定記録は無い。
- blocker: なし。
- 後続の入力:
  - C'F1: checkout の lease は §3 の言う worktree の git dir ではなく、共通の git dir の下の排他 file（`ScenarioCheckout.psm1` の `osm-scenario/leases/<path-sha>.lease`）にある。checkout と run の root に書く前に取っているので、N1 と AC1 は破らない。
- 確認範囲（C' の申告）: 凍結した §2（N1〜N6、AC1〜AC15、§2.8）と §5（V1〜V3）、`AGENTS.md` の常時契約。head の code のうち、復元・lease・使用中の記録・隔離・process の起動と終了・case の順序に関わる `Scenario.psm1`・`ScenarioCheckout.psm1`・`ScenarioRun.psm1`・`ScenarioUnity.psm1`・`ScenarioEditor.psm1`・`UnityProcess.psm1`・`run-tests.ps1`・`s42-player-overlay.ps1`。diff の範囲（`unity/` と `tools/unity-editor.ps1` は無い）。offline の log（各 suite の件数と失敗 0、contract-audit・docs-audit の error 0）。V1 の 24 行の順と、`utc`・`cacheRoot` を除く key の一致、X・Y の status と HEAD。V2 の event の順、PID、callId、eval の値（版と project の path、token）、終了コード 0 で `forced` が偽、前後の status と HEAD。V3 の step の引数・platform・project・版・時刻、XML と期待集合の件数（2443、Passed 2383、Failed 0、Inconclusive 25、Skipped 35）、Inconclusive 25 件の fullname と id、step の `failure` の 2 行、終了 0 で stall の note が無いこと、前後の status と HEAD。
- 未確認範囲と残存リスク（C' の申告）: §3 の配置と AC11〜AC15 は、上の経路と offline の log に触れる範囲だけを見て、test を走らせ直していない。Passed 2383 件の fullname は 1 件ずつの突き合わせをしていない（件数、失敗 0、標本の id、Inconclusive の全件で同一と読んだ）。`ProjectVersion.txt` は workspace に無く、`6000.6.9f1` は runner の欄と Editor の eval の値で見た。Phase B の動作確認の run は判定の bundle ではない（C' は run `j1` を見た）。
- 担当・モデル: xAI の Grok（cursor-agent、grok-4.7-high、`--mode=ask` の読み取り専用）。新しい session。独立性制約あり（外部計画レビューに関わった系列。未関与の系列は残っていない）。

### C'（2 巡目、2026-11-26）

C' の担当は本書を読まないので、進行役が C' の最終回答（英語）を転記した。判断は C' の担当のもので、進行役は足していない。

- 判定: **GO**。head `c38e0762` の 2 巡目の変更は、凍結した不変条件・受け入れ条件・実装制約と `AGENTS.md` の常時契約の内に収まり、run `j2` の原記録は §2.7 の読み方で V1・V2・V3 と AC7・AC8・AC10 を満たす。
- 入力: C' blind bundle（取得案内 `cprime-c38e0762.md`、26 file）。進行役が `fetch` し（fetch が package の SHA-256 を照合した。この bundle には manifest.json が無く、file ごとの照合は進行役では行っていない）、発見 C・判定 C の所見と照合 script が入っていないことを grep で確かめた。C' 自身の 1 巡目の結果（§8 の転記）は入っている。bundle の file は加工していない。workspace には bundle の写しと、zip を展開した写しと、head `c38e0762` の `tools/`・`.agents/skills/osm-workflow/`・`AGENTS.md` を置いた。C' 用決定記録は無い。
- blocker: なし。
- 後続の入力:
  - C'2-F1: 1 巡目の C'F1 はそのまま当たる。checkout の lease は共通の git dir の下の排他 file（`ScenarioCheckout.psm1` の `Get-ScenarioLeasePath`）にあり、2 巡目でも動いていない。新しい `unreserve` の事前確認で、この置き場が許していた別の repo からの書き込みは塞がった。同じ repo の呼び出しは checkout と run の root に書く前に lease を取るので、N1 と AC1 は保たれる。
- 確認範囲（C' の申告）: 2 巡目の差分（`README.md`・`ScenarioRun.psm1`・`scenario-checkout.ps1`・2 つの test。`unity/`・`tools/unity-editor.ps1`・`AGENTS.md` は無い）。大小文字だけが違う管理 file の path を 1 つの記録にすること、復元の後に新しい番号の `<n>.before` を使い古い物を上書きしないこと、`unreserve` が lease を先に取り `Get-ScenarioCheckoutProblems` を予約の行だけ除いて通すこと（汚れ・別の repo・project lock・進行中の git 操作で `Restore-ScenarioManagedFiles` の前に止まる）、空の `ok` の eval 値の assert が test だけであること。V1: 新旧 24 行の順・key の名前と型・値（`utc` と規則どおりの cache root を除く）、両 driver の終了 0、X・Y の前後の HEAD と status。V2: event の順、PID 9112、callId、token と版・project の path の decode、最初の exit の応答の CLI exit 6（制約 11 で失敗としない）、`process-exit` の終了コード 0 で `forced` が偽、前後の git の記録。V3: step の置き場・引数・platform・project・版・時刻、XML と期待集合の件数（2443、Passed 2383、Failed 0、Inconclusive 25、Skipped 35）、Inconclusive 25 件と Skipped 35 件の id と fullname の全件一致、step の `failure` の 2 行、終了 0 で stall の note が無いこと、前後の status と HEAD、`step.json` の cases の件数と標本の一致。offline の log の件数と失敗 0、contract-audit・docs-audit の error 0。
- 未確認範囲と残存リスク（C' の申告）: Passed 2383 件の fullname は 1 件ずつ比べていない（件数、Inconclusive と Skipped の全件、`step.json` の標本で一致と読んだ）。`ProjectVersion.txt` は workspace に無く、`6000.6.9f1` は run の欄と step と eval の値で見た。AC11〜AC15 は差分・README・offline の log の範囲だけを見た。suite は走らせ直していない。tools の tree が `fab27c76` と `c38e0762` で HANDOFF を除いて同じという commands の記述は確かめ直していない。
- 担当・モデル: xAI の Grok（cursor-agent、grok-4.7-high、`--mode=ask` の読み取り専用）。新しい session。独立性制約あり（外部計画レビューと C' の 1 巡目に関わった系列。未関与の系列は残っていない）。
- 入力の不備（2026-11-26 の PR #215 の外部再レビューの指摘を進行役が review-evidence と照らした）: この巡の bundle には固定 manifest が無く、前巡の C' の結果を収録していた（C' 用決定記録の規則に合わない。進行役は「C' 自身の結果は所見ではない」と読んで通した）。この巡の GO は、作り直した bundle での C' のやり直しの結果に置き換える。

## 9. Phase D

未着手
