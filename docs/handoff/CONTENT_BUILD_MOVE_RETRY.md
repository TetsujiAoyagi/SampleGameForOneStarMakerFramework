# content の build の staging の移動を監視の open に耐えさせる

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`（[program](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S5-0。BuildSystem の小さいスライス）
- status: `C'`（判定 C が 2026-11-23 に GO とした。次は C'（最終 C'）。§7）
- branch: `claude/content-build-move-retry`（PR の base は `develop`）
- implementation base commit: `d926e3b6b40dafab086b293e6d60784e304ecf4c`（`develop` の head、2026-11-23。A2 の固定 base と同じ）
- implementation head commit: `d0785b2327f9795aa9c4dea78ebcbfcf51e2f31f`
- A3 凍結記録: 承認者は root（owner）、2026-11-23、対象は revision 0（この記録を含む commit。外部計画レビュー 2 巡目が見た commit `0ae53f8c` の本書に、2 巡目の採否・凍結前の自己点検・凍結の記録・Phase B 宛ての票を足した物）。承認の根拠: 外部計画レビュー 2 巡目が凍結を妨げる指摘なしとし、owner が凍結と Phase B への引き渡しを指示した。§5.1 の採否は、この指示で承認された。
- risk: `normal`（Editor の 2 つの公開点の移動と、再利用の記録の書き込みの失敗の扱いだけを変える。公開 API・asmdef・所有者・寿命・Runtime は変えない。§5）
- owner: root（依頼者）
- created: 2026-11-23
- expires: 2026-12-31、または Phase D で harvest・削除した時点
- harvest to: [18 章](../../unity/Assets/Docs/Architecture/18-asset-description.md) の coordinator の公開（「staging を経て公開する」の箇条）と `ContentTransportPublisher` の段落（同じ volume の move の再試行の条件と上限を 1 文ずつ）、[CONTENT_DIRECTORY_BUILD_FACTS.md](../unity-content/CONTENT_DIRECTORY_BUILD_FACTS.md) の「別に観察したこと」（「原因は切り分けていない」を、program の S5-0 の行の J1 の調査と本スライスの結果に合わせて直す）、program の S5-0 の行（完了と PR）
- 関連: [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md)（§8 の S5-0 の行。J1 の調査の記録と CP2 はそこにある）
- Phase A snapshot の取得案内・状態: 該当なし（機械向けの期待値・照合 script は無い。§5。レビューの入力は本書と固定 base だけ）
- Phase B result snapshot の取得案内・状態: 該当なし（レビューの入力は implementation head の commit。stage は使っていない）
- evidence bundle の取得案内・状態: ローカル固定済み（未昇格）。判定 C が implementation head で生成し、task `s5-0-content-build-move-retry` に `evidence stage` した（判定用、14 file、目次は `manifest.json`）。取得案内は git 管理外の既存の受け渡し場所（Evidence の取得案内の file 群）の判定用の file にある。別の pwsh process の fetch で `passed`、manifest の全 file の hash が一致し、XML を読めた。受け取り側の別 session の fetch・閲覧は未確認
- C' blind bundle の取得案内・状態: ローカル固定済み（未昇格）。同じ head・同じ生結果から作り、同じ task に `evidence stage` した（11 file、目次は `manifest.json`）。取得案内は同じ場所に、判定用とは別の file で置いた。別の pwsh process の fetch で `passed`、manifest の全 file の hash が一致した。受け取り側の fetch・閲覧の確認は C' の起動前に行う
- 引継ぎ票:
  - 次の Phase・役割: C'（独立監査、1 巡目。高コスト証拠は無いので最終 C'）。判定 C の判定証拠が揃った implementation head（§0 の欄）を、C' blind bundle だけから監査する。§5 が予約した、Phase A〜C に関与していない系列の担当で行い（選ぶのは進行役。`cursor-agent` を使うなら Grok 系だけ）、使った担当・モデルと独立性を §8 に書く。
  - 次の作業: C' blind bundle を fetch して閲覧を確かめてから監査し、結果を §8 に記録する（記録は進行役が C' の出力から書いてよい）。
  - 読む範囲: C' は blind bundle だけ（`manifest.json` が目次。凍結した条件は bundle の中の凍結版の本書の §0〜§5、Phase B の結果は B の記録の本書の §6）。可変な本書を読まない。
  - 引き継ぐ commit: この票を含む commit（判定 C の記録 commit。SHA は起動指示と PR #210 の状態ブロックで渡す）。implementation head は欄の SHA のまま動かない。
  - task: `s5-0-content-build-move-retry`（開始済み。新しく start しない）。
  - 必要な権限: C' blind bundle の fetch（非仮想化の pwsh、sandbox 外、Artifacts の 3 project の Release build 済みの checkout）。Unity の再実行は必須でない。push はしない（公開点は C' の後）。
  - 停止規則: C' の規則（osm-workflow の phase-c-prime.md）。C' の差し戻しは別に数えて上限 3 巡。
  - 引き渡さないもの: C' に、本書（特に §7 の発見 C・判定 C の所見と結論）、判定用の evidence bundle とその取得案内、PR の本文と会話を渡さない。

## 1. 目的と対象外

- 目的: content を build する test が増えた今の全 EditMode 回帰を、開発者の PC に普通にある監視（Unity Hub のように、変更の通知を受けて新しい file を開いて閉じる tool）が動いたままで、1 回の run で failed 0 にする。そのために、content の build の staging を公開先へ移す操作が、監視の一瞬の open で失敗しないようにする。あわせて、再利用の記録の書き込みで別名の file が残る時は、黙って残さない。
- 利用者: OSM の開発者（content の build と publish を Editor から呼ぶ人）と、判定 C で全 EditMode 回帰を走らせる担当。
- 対象外（所有する後続は §2 の「ここでは答えない問い」）:
  - 監視の tool を止める・設定を変える・test の出力先を監視の外へ出すこと。owner の入力（2026-11-23）: Unity Hub は消せないので、監視がある前提で回避する。
  - 公開の方式を「移動をやめて final へ直接書き、完了の印を最後に置く」へ変えること（§2 の分割の判断と、§5.1 の候補の比較）。
  - Runtime の `ContentInstaller` の staging → final の移動と `ContentCacheStore.DeleteRevision` の tombstone への移動、SampleGame の `PlayerBuildPublisher.MoveDirectory`。
  - 直前に閉じた後の失敗（J1 の調査の 43 回）で参照を持っていたものの特定。
  - 全 EditMode 回帰の他の不安定（Timeout、他の Unity の同時実行による失敗）。
- 現況（base `d926e3b6`）:
  - **J1（S3-4b の判定 C）と調査（program の S5-0 の行に全文がある。ここには判断に使う事実だけを写す）:** 全 EditMode 回帰の 3 回で毎回 2〜4 件、`BuildContentCoordinator.Build` の staging → final の `Directory.Move` が `IOException: Access to the path … is denied` になった。落ちる test は毎回違う。掴んでいたのは、変更の通知を受けて新しい file を開く user mode の監視（Unity Hub は `D:\repositories\unity` を再帰で監視する。ChatGPT の desktop app は repo の root を監視する）で、Process Monitor の記録では失敗 47 回の全てで移動の直前 0.2〜4 ms に両者の open があり、4 回は移動の時点で中の file を開いたままだった。Unity を起動しない pwsh の再現（小さい file 7 個の directory を複写し、すぐに `MoveFileEx` で同じ volume の別名へ移す）で、監視の下では 1,000 回あたり 35〜100 回が `ERROR_ACCESS_DENIED` になり、ほぼ全てが直後の移し直しで通る。監視の外では 0。中の file か subdirectory を開いた handle があると、`FILE_SHARE_DELETE` 付きでも directory の移動は `ERROR_ACCESS_DENIED` になる。移す directory そのものと親の handle は妨げない。
  - **owner の入力（2026-11-23）:** ChatGPT の監視はともかく、Unity Hub は消せないので回避するしかない。
  - **移動の箇所（Editor、`OneStarMaker.Build.Content`、`Scripts/Editor/Build/Content/`）:** `BuildContentCoordinator.Build`（file は 271 行、`Build` は 41〜172 行）の 123 行の `Directory.Move(staging, final)`。113〜114 行で `staging` と `final` が無いことを確かめ、117 行の `CopyDirectory` で workspace を `staging` へ複写し、120〜121 行で manifest と metadata の存在を確かめ、122 行で親を作ってから移す。移動の後に `published = true`。例外は 2 つの `catch` が受け、`staging` を消し、`published` なら `final` も消し、失敗の outcome を書く。`ContentTransportPublisher`（227 行）の 156 行の `Directory.Move(staging, final)`。`staging` は `<final の親>/.staging/<guid32>`、48〜49 行で `final` が既にあれば `IOException`、`finally` が残った `staging` を消す。coordinator の staging は test でも repo の下（`TestResults/bs3-fixture/…`）にあり、Unity Hub の監視の範囲に入る。publisher の test の `final` は `Path.GetTempPath()` の下で監視の範囲外だが、製品の公開先（`artifacts/bs4/transport` の形）は repo の下にあり、同じ失敗が起きうるので同じ扱いにする。
  - **再利用の記録（`ContentOverlayReuse.Write`、164 行）:** `<path>.tmp` に書いて `File.Replace`（無ければ `File.Move`）で置き換え、`finally` で別名を消しにいく。その削除が `IOException`・`UnauthorizedAccessException` を投げると 105〜106 行で握りつぶし、元の失敗だけを返すので、別名が残りうる（CP2。S3-4b の受け入れ条件「別名の file は成功でも失敗でも残さない」と食い違う）。呼び手の coordinator は `Write` の例外の `ex.Message` だけを `WithReuseRecordFailure` で結果に写し（`InnerException` の文言は写らない）、成功した build は失敗にしない（169〜171 行）。別名の名前は contentSet ごとに 1 つで固定なので、残っても増えず、次の `Write` が書き込みまで進めば上書きする。`TryGet` は別名を読まない。
  - **前例の再試行（`TestObservationWriter.AtomicWrite`、`Scripts/Editor/TestObservation/`）:** `File.Replace` を、Windows で `HResult` が共有違反（`0x80070020`）・lock 違反（`0x80070021`）・`0x80070497` の `IOException` に限り、20 ms 待って最大 5 回・100 ms まで再試行する。置き換え・待ち・時計・別名の削除を注入した internal の overload を持ち、test は時間を注入して進める。失敗した置き換えの後、待ちの前と後に両方の path が残ることを確かめ、残っていなければ再試行しない（待ちの後にも時計を見るので、待ちは上限を 1 回分またげる）。publication の失敗と後片付けの失敗の両方を `AggregateException` で返す。
  - **.NET の `Directory.Move` の例外:** Windows で移動が `ERROR_ACCESS_DENIED` になると、`UnauthorizedAccessException` ではなく `IOException`（`HResult` は `0x80070005`、文言は「Access to the path … is denied」）を投げる（.NET の `FileSystem.MoveDirectory` の実装。J1 の log の型と文言もこれと一致する）。Unity 6000.6.9f1 の Mono で `HResult` が同じかは未確認で、Phase B の test（AC2）が実物で確かめる。
  - Unity 上では何も実行していない。
- UC ごとの利用者の入力:

| UC | 利用者が選ぶ・入れるもの | 機械が決めるもの | 誰がいつ決めるか |
|---|---|---|---|
| content を build・publish する（Editor から coordinator・publisher を呼ぶ） | なし（今と同じ request だけ） | 移動を再試行する失敗の種類と、待ち・上限 | 開発時の宣言（code の定数） |
| 全 EditMode 回帰を走らせる | なし（今と同じ `run-tests.ps1` の引数だけ） | 同上 | 同上 |

  入力を増やさない。再試行の上限を利用者に選ばせないのは、目的が「監視が動いたままで失敗しない」で、監視の有無や頻度を利用者が知る必要を作らないためである。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: Editor の content の build と publish の staging → final の移動を、一時的な lock の失敗に限って短い上限で再試行すれば、Unity Hub の監視が動いたままの全 EditMode 回帰を 1 回の run で failed 0 にできるか。あわせて、再利用の記録の別名が削除の失敗で残る時に、それを結果に出せるか。
- 進める最低条件:
  - 不変条件:
    - **I1 公開は全部か無しか:** 再試行を経ても、`final` は「中身が揃った 1 回の移動の結果」か「無い」かのどちらかで、部分的な `final`・既にある `final` への上書きや混ぜ合わせを作らない。失敗した build・publish を成功として返さない。主張を崩しうる経路: (a) 移動の失敗の後に別の誰かが `final` を作った時に、再試行がそこへ入れる。(b) 移動が失敗を返した後に `staging` が無く `final` がある状態を「自分の移動が済んでいた」と読み、成功を返す。その `final` が今の build の物である根拠は存在だけからは得られない（別の処理が `staging` を取り除いて `final` を作った場合も同じ状態になる）ので、成功にしない（AC4）。前提: Windows の同期の rename（`MoveFileEx`）は失敗を返した時点で rename をしておらず、失敗を返した移動が後から済むことは無い（J1 の記録にもその例は無い）。この前提の下では、失敗した移動の後に揃った `final` があっても自分の物ではないので、coordinator が `published` を偽のまま `final` を消さずに失敗の outcome を書くのは正しい。(c) 再試行の後に投げる例外の型が変わり、coordinator の `BuildIssueException` の catch と一般の catch の振り分けや、publisher の `finally` が変わる。探索した境界: coordinator の 113〜171 行、publisher の 48〜49 行と 100〜165 行の公開の段と catch・finally。未確認: 同じ identity を 2 つの Editor が同時に build する場合（今も守っていない。範囲外）。上限まで handle が閉じない時に、呼び手の catch・finally の `Directory.Delete(staging, true)` も拒まれて、coordinator が outcome を書けない・publisher が元の失敗を後片付けの例外で覆うこと（今の code の振る舞い。AC6 と後続の項）。
    - **I2 恒久の失敗を遅らせて隠さない:** 一時的な lock 以外の失敗（`final` が既にある、`staging` が無い、path が無い、別の volume、権限の恒久の拒否のうち上限を超えて続くもの）は、今と同じ失敗として返り、成功にならない。上限を超えた再試行は最後の失敗を結果に残す。監視の無い環境で 1 回目の移動が成功する時の振る舞いは今と変えない（待たない）。主張を崩しうる経路: 失敗の種類の判定が `HResult` を広く取りすぎる（例: `0x80070005` の全てを一時的とみなす、または Unity の Mono が Win32 の値を載せない `IOException` の既定値を述語に足す）と、恒久の権限の拒否も上限まで待ってから失敗する（失敗にはなるので I1 は崩れないが、待ちの上限が守られなければ崩れる）。探索した境界: .NET の `Directory.Move` が投げる例外（現況）。未確認: Unity の Mono での `HResult`（AC2 が確かめる）。
    - **I3 別名の file を黙って残さない:** 再利用の記録の書き込みが失敗し、別名の削除も失敗した時、結果（`ReuseRecordFailure`）に元の失敗と、別名を消せなかったことと、その path が出る。成功した build は失敗にしない（今の契約のまま）。既存の記録の bytes は変わらない。主張を崩しうる経路: `finally` の削除の例外の握りつぶし（`ContentOverlayReuse.Write` の今の 105〜106 行）。情報を内側の例外にだけ置くと、coordinator が `ex.Message` だけを写すので結果から落ちる。置き換えが成功した後に別名が残る場合（`File.Replace` / `File.Move` が別名を消すので普通は起きないが、`finally` は成否によらず残った別名を消しにいき、その削除の失敗も同じく結果に出す）。探索した境界: `Write` と、その例外を受ける coordinator の 162〜171 行。
  - 成立条件:
    - **S1:** 移す directory の中の file を開いた handle があって移動が `ERROR_ACCESS_DENIED` になる時、その handle が上限の内に閉じれば移動は成功し、結果は今の成功と同じになる。
    - **S2:** Unity Hub が `D:\repositories\unity` を監視している PC で、GO 候補 head の全 EditMode 回帰を 1 回の run で failed 0（Inconclusive 0）にでき、どの run でも staging の移動が上限を超えて落ちない（AC8）。
- 受け入れ条件:
  - **AC1（S1、I1）:** 共有の移動の関数の単体 test。`staging` に file を置き、その file を `FileShare.ReadWrite | FileShare.Delete` で開いたまま移動を呼び、注入した待ちの 1 回目でその handle を閉じる。移動は成功し、`final` に元の file が全て同じ内容であり、`staging` は無く、試行は 2 回である。時間は注入で進め、`Task.Delay` / `Thread.Sleep` を使わない。開いた handle で directory の移動が失敗するのは Windows の振る舞いなので、この test は Windows だけで走らせ（`[Platform(Include = "Win")]` 等）、Windows 以外では注入した移動で同じ試行の回数と分岐を確かめる test が走る。
  - **AC2（S1 の前提）:** AC1 の 1 回目の失敗の例外の型と `HResult` を test の中で確かめ（観測した値は test の出力に書き、B が §6 に写す）、§3 の述語（Windows で、`IOException` の `HResult` が `0x80070005`・`0x80070020`・`0x80070021` のもの、または `UnauthorizedAccessException`）がその失敗を一時的と読むことを確かめる。観測した値が §3 の述語に入らない時は、述語に足さずに §4 の差し戻しにする（別の volume・path が無い時の `IOException` と同じ値かもしれず、足すと I2 を崩しうるため）。
  - **AC3（I1、失敗経路）:** 移動が一時的な失敗になった後の検査（待ちの前、または待ちの後で次の移動の前）の時点で `staging` と `final` の両方がある時（その間に誰かが `final` を作った）、移動を呼ばずに失敗を返し、`final` の bytes は変わらず、`staging` は残る（片付けは呼び手の catch・finally の仕事のまま）。
  - **AC4（I1、失敗経路）:** 移動が失敗した後の検査（待ちの前、または待ちの後で次の移動の前）で `staging` が無い時は、`final` の有無によらず、移動を呼ばずに最後の失敗を返す。(a) `staging` も `final` も無い時と、(b) `staging` が無く `final` がある時の両方を test し、どちらも成功とは報告せず、(b) の `final` の bytes は変わらない（その `final` の帰属は確かめられないので消さない。I1 の (b)）。
  - **AC5（I2、失敗経路）:** (a) 1 回目の移動が成功する時は待ちも時計も呼ばず、今と同じ 1 回の移動である。(b) 一時的でない失敗（別の volume など、§3 の述語に入らない `IOException`）は 1 回で失敗し、待ちを呼ばない。(c) 一時的な失敗が続く時は、試行の前と待ちの前に時計を見る: 試行を始める時点の経過が 2,000 ms 以上ならその試行を始めず、待ちを始める時点で「経過 + 50 ms」が 2,000 ms を超えるなら待たずに止める。止まった時は、最後の失敗を内側に持つ `IOException` を返し、その文言に試行の回数、`staging` と `final` の path、最後の失敗の文言を含める（型を `IOException` にするのは、呼び手の catch・finally と、publisher の例外を受ける呼び手の振り分けを今と変えないため。文言に path を含めるのは AC8 の (2) の数え方のため）。待ちの合計（要求した待ちの量。`Thread.Sleep` が要求より長く寝た分は数えない）は 2,000 ms を超えない。時計が進まない時も試行は 41 回を超えず、41 回目の失敗の後は待たずに止まる（待ちは最大 40 回）。合格例: 経過 1,960 ms で失敗したら待たずに止まる。時計が進まず 41 回目が失敗したら待たずに止まる。不合格例: 経過 1,960 ms で失敗して 50 ms 待ってから止まる。41 回目の失敗の後に 1 回待つ。
  - **AC6（I1 の (c)）:** coordinator と publisher の移動は共有の関数を通り、上限を超えた失敗は今と同じ catch・finally に入り、成功は返らず、`final` は作られない。後片付け（coordinator の catch と publisher の finally の `Directory.Delete(staging, true)`）が成功するとは保証しない: 上限まで handle が閉じなかった時はその削除も拒まれうり、coordinator は outcome を書かずに例外を外へ投げ、publisher は元の失敗を後片付けの例外で覆いうる（今の code の振る舞いで、本スライスは変えない。後続の項）。B は、Windows だけで走る test を 1 つ足し、`staging` の中の file を開いたまま（閉じない）注入した時計で上限に達させ、共有の関数が `IOException` を返すことだけを確かめた後、同じ handle を開いたまま `Directory.Delete(staging, true)` を呼んだ時の観測（例外の型・文言と、`staging` が残るか）を test の出力に書き、§6 に写す。この観測は合否に使わず、assert しない。既存の coordinator・publisher の test が変更なしで通ることで確かめ、移動の失敗を起こす新しい統合 test は足さない（AC1〜AC5 が関数で確かめ、呼び出しは 1 行の置き換えなので、構造レビューが call site を照合する）。
  - **AC7（I3、失敗経路）:** `ContentOverlayReuse.Write` の置き換えと別名の削除を注入で両方失敗させる単体 test。`Write` が投げる例外の `Message` と、coordinator 経由の `ReuseRecordFailure` の両方に、元の失敗の文言、別名を消せなかったこと、別名の絶対 path の 3 つが含まれる（`AggregateException`、または 3 つを本文に書いた 1 つの例外で返す）。3 つのどれかが内側の例外にしか無い結果は不合格。既存の記録の bytes は変わらない。置き換えは成功したが別名が残っていてその削除が失敗する時は（置き換えの失敗は無いので）、`Write` は「別名を消せなかったこと（削除の失敗の文言）」と「別名の絶対 path」の 2 つを本文に書いた例外を返し、coordinator は build を成功のまま `ReuseRecordFailure` に写す。置き換えた記録の bytes は今回の build の記録で、`TryGet` が今回の鍵で読み戻せる（注入で確かめる。架空の「元の失敗」の文言を作らない）。置き換えだけが失敗して削除は成功する時は、今と同じく元の失敗だけを返し、別名は残らない（既存の `ContentOverlayBuildAndReuseTests.RecordReplaceFails_BuildStaysSuccessful_AndTheExistingRecordIsKept` が変更なしで通る）。
  - **AC8（S2）:** GO 候補 head で、Unity Hub が動いている（`Get-CimInstance Win32_Process` に `Unity Hub.exe` があり、Hub の `%APPDATA%\UnityHub\projects-v1.json`（J1 の調査が読んだ file）の `containingFolderPath` に `D:\repositories\unity` がある）PC で、`pwsh tools/run-tests.ps1 -WithGraphics`（空 filter、EditMode）を続けて最大 3 run 走らせる。合格は次の 2 つが両方成り立つ時だけである。(1) XML の root result が `Passed`、failed 0、Inconclusive 0 の run が 1 つある。(2) 走らせた全 run で、staging（`…\staging\…`・`…\.staging\…`）の path を指す access denied・共有違反で落ちた test が 0 件である。共有の関数を通った上限超え（文言に試行の回数がある）だけでなく、試行の回数の無い生の失敗（共有の関数を通らない移動の置き忘れ）と、後片付けの削除の失敗も数える。(1) と (2) を満たした時点で残りの run は走らせない。Timeout など staging と無関係な失敗のある run は (1) に数えないが、それだけで不合格にはしない（その失敗は §7 に記録する）。(2) が 0 件のまま 3 run とも (1) に届かない時は、合格にも NO-GO にも Phase A の再開にもせず、判定 C を完了と記録しないで §7 に記録し、run を足すか打ち切るかを owner が決める。不合格例: ある run で staging の移動が上限を超えて落ちた（他の run が failed 0 でも不合格。上限が足りない証拠なので §4 に従って戻す）。failed 0 だが Inconclusive が 1 以上の run しかない。各 run の前に、Unity Hub と ChatGPT の process の有無と、同時に動く他の Unity の process を記録する。ChatGPT を止めることは求めない（止めたなら記録する）。
- 判定の補助の観測（合否に使わない）: 移動が再試行の後に成功した時、共有の関数は 1 行の情報 log（`[ContentStagingMove]`、試行の回数と `final`）を出す。判定 C は AC8 の各 run の Editor log（`run-tests.ps1` が XML・`step.json` と同じ出力先に `-logFile` で書く file）からその行の数と試行の回数を数え、「機構を通った観測」（この行）と「全回帰が通った観測」（XML）を分けて §7 に書く。0 でも AC8 の合否は変えない（監視の open と移動が重ならなかった run でも、J1 の原因に耐えることは AC1 が持つ。J1 の 47 回のうち移動の時点で開いていたと確かめたのは 4 回で、残り 43 回は直前に閉じた後の失敗なので、AC1 の開いた handle が全ての失敗の形を代表するとは言わない。この行の数がその差を埋める観測になる）。
- ここでは答えない問いと所有する後続スライス:
  - Runtime の `ContentInstaller` の staging → final の移動と、`ContentCacheStore.DeleteRevision` の tombstone への移動に同じ再試行を入れるか（Player の cache の置き場が監視される時）。test は temp の下で動き、全 EditMode 回帰の問いに関わらない。→ program §8 の S4（Player へ content を届ける経路と install を設計する行）の入力。
  - 移動の失敗の後に呼び手の後片付け（`Directory.Delete(staging, true)`）も失敗した時に、coordinator が outcome を書けない・publisher が元の失敗を覆うこと（今の code の振る舞い）。→ [18 章](../../unity/Assets/Docs/Architecture/18-asset-description.md)「BuildSystem の後続」の入力。AC8 の (2) が後片付けの失敗も数えるので、本スライスの判定には出る。
  - SampleGame の `PlayerBuildPublisher.MoveDirectory`（Player の build の公開）。→ [18 章](../../unity/Assets/Docs/Architecture/18-asset-description.md)「BuildSystem の後続」の入力。
  - 移動をやめ final へ直接書いて完了の印を最後に置く方式。読み手（`ContentOverlayReuse.TryGet`、publisher、選択）の「final があれば公開済み」の読みを全て変えるので、再試行で足りない証拠（AC8 の不合格）が出た時だけ検討する。→ 本スライスが B から A へ戻る時の入力（§4）。
  - 直前に閉じた後の失敗で参照を持っていたものと、監視を止めた時に失敗が消えるか。再試行がそれらにも効く限り答えない。→ 所有しない（AC8 が不合格の時に §4 の差し戻しで再び問う）。
  - 全 EditMode 回帰の他の不安定（Timeout、他の Unity の同時実行）。→ 本スライスの外。AC8 の run に出たら §7 に記録し、program の「BuildSystem 側の入力」に足す。
- スライス分割の判断: 契機は「独立した変更理由が混在する」（移動の再試行と、再利用の記録の別名の削除の失敗）。分割しない。どちらも program が S5-0 の 1 行に入れた BuildSystem の公開の失敗の扱いで、同じ 2 つの file 群（`Scripts/Editor/Build/Content/` と `Tests/Editor/Build/`）に閉じ、AC7 は AC8 の全 EditMode 回帰と同じ run で判定できる。分けると判定 C・C' を 2 回走らせる費用だけが増える。
- 判定定義: GO は AC1〜AC8 が全て満たされた時。NO-GO は AC のどれかが満たされない時（AC8 で上限を超えた移動の失敗が出たら、§4 の差し戻しに従う）。AC8 の (2) が 0 件のまま 3 run とも (1) に届かない時だけは、GO にも NO-GO にもせず、AC8 の定めどおり owner が run を足すか打ち切るかを決める。スパイクではないので CONDITIONAL ACCEPT は使わない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし
- 本文へ転記した実装制約:
  - 新規・編集する `.cs` の先頭に `#nullable enable` を置く。`record` を使わない。
  - test で `Task.Delay` / `Thread.Sleep` を使わない。待ちは注入して進める（本番の待ちだけが `Thread.Sleep` を使う。前例の `AtomicWrite` と同じ）。
  - asmdef の参照を足さない。`OneStarMaker.Build.Content` の中に閉じる。
  - `Scripts/Editor/TestObservation/` の `AtomicWrite` は変えない（別の asmdef・別の変更理由。判定の述語を共有しない）。
  - path は `tools/path-budget.json` の予算に従う。
  - 特定の監視の tool（Unity Hub など）を名指しする分岐・検出を作らない。判定は失敗の種類と `staging` / `final` の状態だけで行う。
- 未決事項: なし

未検証事項を網羅しない。A3 後の blocker は、凍結済みの進める最低条件、受け入れ条件、または常時契約への違反を示すものに限り、`phases-and-handoff.md` の「blocker の基準」に従って違反根拠を記録する。それ以外の新しい不確実性は重要度にかかわらず所有する後続スライスへ移送し、実装を追加しない。現スライスへ例外的に取り込む場合は人間の明示承認と Phase A revision を要する。

## 3. 責務マップ

| file | 役割 | 責務（一文） | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 行数（今 → 予想） |
|---|---|---|---|---|---|---|---|---|
| `Scripts/Editor/Build/Content/ContentStagingMove.cs`（新規） | Policy と Editor I/O（情報 log 1 行を含む）。orchestration ではない | 同じ volume の staging → final の移動を、一時的な lock の失敗に限って短い上限で再試行する | 再試行の条件・上限を変える時 | 状態を持たない static。呼び出しの間だけ | `System.IO`、`System.Diagnostics.Stopwatch`、`UnityEngine.Debug`（情報 log だけ） | internal。`Move(staging, final)` と、移動・待ち・時計・log を注入する internal の overload | 単体 test（AC1〜AC5）。Unity の API なしで、実 file system と注入した時間で確かめられる | 0 → 約 80 |
| `Scripts/Editor/Build/Content/BuildContentCoordinator.cs` | orchestration | （既存）content の build の orchestration | 123 行の移動を共有の関数へ置き換える | 変えない | `ContentStagingMove` を足す（同じ asmdef） | 変えない | 既存の test（AC6） | 271 → 271 |
| `Scripts/Editor/Build/Content/ContentTransportPublisher.cs` | orchestration | （既存）transport の publish | 156 行の移動を共有の関数へ置き換える | 変えない | 同上 | 変えない | 既存の test（AC6） | 227 → 227 |
| `Scripts/Editor/Build/Content/ContentOverlayReuse.cs` | 永続化 | （既存）再利用の記録の読み書き | 別名の削除の失敗を握りつぶさず、元の失敗と一緒に返す | 変えない | 変えない | `Write` に、置き換えと削除を注入する internal の overload を足す | 単体 test（AC7） | 164 → 約 185 |
| `Tests/Editor/Build/ContentStagingMoveTests.cs`（新規） | 検証 | AC1〜AC5 | — | — | `OneStarMaker.Build.Content` の internal（既存の `InternalsVisibleTo`） | — | 実 file は temp の下に作る（監視の有無に左右されない） | 0 → 約 170 |
| `Tests/Editor/Build/ContentOverlayBuildAndReuseTests.cs` または新規の単体 test の file | 検証 | AC7 | — | — | 同上 | — | 注入で失敗を起こす | 既存 + 約 50 |

- 配置の理由: 2 つの呼び手が同じ asmdef・同じ folder にあり、変更理由（Windows の監視の下での公開の移動）が同じなので、その folder に 1 つの小さい internal の class を置く。新しい folder・汎用の `Helpers` / `Utils` を作らない。`TestObservation` の `AtomicWrite` と述語を共有しないのは、別の asmdef（参照を足すことになる）で、対象の操作（file の置き換えと directory の移動）と失敗の種類（`0x80070005` を含むか）が違うためである。
- 行数警報: 発火しない（最大 271 行、増加は 50% 未満、責務は増えない）。
- 再試行の条件と上限（担当が決めた。覆す時だけ改める）: Windows の時だけ、`IOException` で `HResult` が `0x80070005`（access denied）・`0x80070020`（共有違反）・`0x80070021`（lock 違反）のもの、または `UnauthorizedAccessException` を一時的とみなす（AC2 で Unity の Mono が返す型を確かめ、判定はその両方を受ける）。失敗の後に再試行するのは、失敗が一時的で、かつ待ちの前と待ちの後（次の移動の前）の両方の検査で `staging` があり `final` が無い時だけである。それ以外（一時的でない、`staging` が無い、`final` がある）は移動を呼ばずに最後の失敗を返す（AC3・AC4・AC5 の (b)）。成功で戻るのは移動そのものが成功した時だけで、状態の検査から成功を推定しない（I1 の (b)）。待ちは 1 回 50 ms で、試行を始める時点の経過が 2,000 ms 以上ならその試行を始めず、待ちを始める時点で「経過 + 50 ms」が 2,000 ms を超えるなら待たずに止める（AC5）。時計が進まない時の上限として試行は最大 41 回とし、41 回目の失敗の後は待たない。前例の `AtomicWrite` は待ちの後に時計を見るので待ちが上限をまたげるが、ここではまたがない。上限の根拠は時間の大きさではなく構造である: 監視の open は file ごとの短い開閉で、移す directory の中の file の数だけ続きうるので、1 回の待ちで済む前提を置かず、一方で恒久の lock（I2）を長く待たないよう、1 回の build の所要に比べて小さい固定の上限にする。

## 4. 実装計画

- 変更対象: §3 の表。
- 順序: (1) `ContentStagingMove` と AC1〜AC5 の test（AC2 で Unity の Mono の例外の型を確かめる）。(2) coordinator と publisher の 2 行を置き換える。(3) `ContentOverlayReuse.Write` と AC7 の test。(4) 起点 filter で通し、B の出口へ。
- Phase B から Phase A へ差し戻す条件:
  - AC2 で、Unity の Mono が監視の open による移動の失敗を、`IOException`・`UnauthorizedAccessException` 以外の型で返す、または `HResult` で一時的な lock を他の失敗と区別できない（判定の範囲を決め直す必要がある）。
  - AC8 で、上限を超えた移動の失敗が出た（再試行で足りない。上限を延ばすか、§2 の「移動をやめる」方式を検討する。どちらも新しい revision で A2 / A3 を行う）。
  - 再試行のために、coordinator・publisher の catch・finally の振り分けや outcome の書き方を変える必要が出た。
- 対象外を維持する方法: Runtime の `ContentInstaller`・`ContentCacheStore`、SampleGame の `PlayerBuildPublisher`、`TestObservationWriter` を変更対象に入れない。構造レビュー（Phase C）は `git diff --stat` にこれらが出ないことを確かめる。

## 5. テストとレビュー計画

- 単体テスト: AC1〜AC5（`ContentStagingMoveTests`）、AC7。
- 差し戻し中の起点 `-Filter`（C が根拠付きで変更可。受け入れ条件の追加ではない）: `OneStarMaker.Tests.Editor.Build.ContentStagingMoveTests|OneStarMaker.Tests.Editor.Build.ContentOverlayBuildAndReuseTests|OneStarMaker.Tests.Editor.Build.BuildContentFullModeTests|OneStarMaker.Tests.Editor.Build.ContentPurposeIndexPublishTests|OneStarMaker.Tests.Editor.Build.SourceClosurePublishTests|OneStarMaker.Tests.Editor.Build.OverlayRegistrationIntegrationTests`（後ろの 2 つは J1 で落ちた呼び手。`OverlayRegistrationIntegrationTests` は `SampleGame/Tests/Editor/Build/` にあるが namespace は同じ）（AC7 を新規の file に置いたらその class も足す）。
- 判定必須テスト（GO 候補 head）: AC8 の全 EditMode 回帰（`pwsh tools/run-tests.ps1 -WithGraphics`、空 filter）。起点 filter の test はこの run に含まれる。
- 全 EditMode 回帰の適用除外: なし（AC8 そのものが全 EditMode 回帰である）。
- 統合・Unity テスト: 新しい統合 test は足さない（AC6）。
- 操作・実行時・目視条件の検証経路: 対象なし（全ての条件が Unity の batch test と XML で閉じる）。動作確認は `対象なし`。
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点: なし（`run-tests.ps1 -WithGraphics` は既知の経路。BOOTSTRAP の判定 C で Inconclusive 0 を取った）。
- 人間の判断が必要な条件: なし。
- 高コスト証拠: なし。
- 判定 C の照合 script の方式: なし（判定 C は XML の root result・failed・Inconclusive と、`run-tests.ps1` の `step.json`、Editor log の `[ContentStagingMove]` の行の数を読む。期待値 file は要らない）。
- 判定 C の実行の注意: Unity の終了 stall は [Harness README の停止規則](../../tools/Harness/README.md#unity終了時の既知native-stallと調査の停止規則) に従い、XML で判定する。`run-tests.ps1` は Inconclusive があると `step.json` が failed・exit 1 になるので、XML の root result と件数を読み、`step.json` の理由も読む。
- 機械検査: `pwsh tools/docs-audit.ps1`、contract-audit（`#nullable enable`、path の予算）。
- A0/A1 主担当・モデル・ベンダー: Claude Opus 5.5（Anthropic）。
- C' 用に予約した担当・モデル・ベンダー: Phase A で使っていない系列（OpenAI 系）。B・C の系列と重なれば、C' の開始時に未関与の系列を選び、§8 に記録する。
- 独立性の強化条件を満たせない場合の理由: risk は normal で強化条件は求めない。

### 5.1 A2 の独立レビューと A3 の採否

- A2 独立レビューごとの観点・担当・モデル・ベンダー、入力版:
  - **アーキテクチャゲート**（architecture-gates.md の Phase A の 1〜7、不変条件の破り方、曖昧な AC の合格例・不合格例、現況の事実の照合。計画の変更は実装済みと仮定する枠を指示書で固定した）: cursor-agent、Grok 4.7 high（xAI）、2026-11-23。入力版: 固定 base `d926e3b6b40dafab086b293e6d60784e304ecf4c` から関係 path を取り出した写しと、A1 の HANDOFF copy（sha256 `6f8ee0abe84a5b09c45178049fdf4fd9c6fd533c1f2d540056da982a6be32364`）。結論: ゲート 1〜7 は満たす（`ContentStagingMove` は分割しない、asmdef・公開面・Runtime は増えない）。指摘 6 件（blocker 1、major 2、minor 3）と事実の誤り 6 件。
  - **目的レビュー**（目的と利用者の 2 欄の原文だけを渡し、問いは「何を保証すべきか」「何を作らないべきか」の 2 つ。HANDOFF・code・他の欄は読ませていない）: Claude の subagent、Claude Opus 5.5（Anthropic）、2026-11-23。入力版: 原文の sha256 `bc042ba699d9c64f886bd3a92ebff8c0eafe19c3b4bc602bdca3a92cf2067095`。
- A3 統合担当・モデル・採否: 統合は A1 と同じ担当（Claude Opus 5.5）。owner は外部計画レビュー 2 巡目の後に凍結した（§0）。
  - アーキテクチャゲート 1（blocker、I1 の (b) を AC4 が合格にしていた）: A2 の時点では採用し、`staging` が無く `final` がある時は成功で戻す分岐を入れた。外部計画レビュー 1 巡目の 2 件目の R1 で**覆し、不採用にした**（下記）。I1 の (b) の順序は「失敗を返した移動が実は済んでいた」を前提にするが、Windows の同期の rename ではその前提が起きず、存在だけでは `final` の帰属を確かめられないので、成功で戻すと I1 の「失敗した build を成功にしない」をかえって崩しうる。
  - アーキテクチャゲート 2（major、AC2 の括弧書きが述語を広げる）: **採用。** 観測値が §3 の述語に入らなければ足さずに §4 の差し戻しにする（AC2）。
  - アーキテクチャゲート 3（major、AC8 の終わりと優先が割れる）: **採用。** 最大 3 run、合格は (1) failed 0・Inconclusive 0 の run が 1 つと (2) 全 run で移動の上限超えが 0 件の両方、満たした時点で止める（AC8、S2）。
  - アーキテクチャゲート 4（minor、待ちが上限をまたぐ）: **採用。** 試行の前と待ちの前に時計を見る（AC5 の (c)、§3）。
  - アーキテクチャゲート 5（minor、AC7 の文言が内側の例外だけに置ける）: **採用。** `Message` と `ReuseRecordFailure` の両方に 3 つを要求する（AC7、I3）。
  - アーキテクチャゲート 6（minor、§3 に役割の列が無い）: **採用。** 列を足した。
  - 事実の誤り（行番号、test 名、publisher の test の staging が temp の下にあること）: **採用。** §1 の現況、I1・I3 の探索した境界、AC7 の test 名を直し、publisher を範囲に残す理由を製品の公開先に書き換えた。
  - 目的レビュー「監視の無い環境の振る舞いを変えない」: **採用。** I2 と AC5 の (a) に入れた（1 回目が成功すれば待ちも時計も呼ばない）。
  - 目的レビュー「特定の tool を名指しする分岐を作らない」: **採用。** 本文へ転記した実装制約に足した。
  - 目的レビュー「再試行や再実行で failed 0 に持ち込んだ run は数えない」: **採用（既に満たす形に確定）。** AC8 の (1) は 1 つの run の XML が failed 0 であることで、run の中の test の再実行を数えない。3 run は「上限を超えた移動の失敗が無い」ことを複数 run で見るためで、(2) が全 run に効く。
  - 目的レビュー「再利用の記録の書き込みも、監視の open が重なっても壊さず読み戻せる状態で終える」: **不採用（現スライスでは）。** 記録の置き換えは今も 1 回の `File.Replace` で、失敗しても既存の記録は前の bytes のまま読める（壊れない）。監視による置き換えの失敗は J1 の 3 run・調査の記録に 1 件も無く、起きても build は成功のまま `ReuseRecordFailure` に出る。ただし既存の test の一部は `ReuseRecordFailure` が null であることを確かめるので、AC8 の run でこの失敗が出たら §7 に記録し、§4 の差し戻しの入力にする。
  - 目的レビュー「残った別名を自動で掃除する機構は作らない」: **採用（計画どおり）。** 次の `Write` が同じ名前を上書きする今の振る舞いのままで、掃除の機構は足さない。
  - 目的レビュー「判定 C に監視の有無を記録させる手順を足さない」: **不採用。** AC8 は「監視が動いたまま failed 0」を確かめる条件で、Unity Hub の process と登録の有無の記録が無いと、監視の無い run の failed 0 と区別できない。記録は `Get-CimInstance` と file の読み取りの機械の確認で、利用者の入力は増えない。
  - （A2 の採否の時点）目的・利用者の 2 欄は変えていないので、目的レビューのやり直しは要らない。
  - （A2 の採否の時点）A2 の再レビュー: 要らない。採用した変更はアーキテクチャゲートが自分で示した修正案（分岐・述語・AC8・時計の位置・AC7・役割の列）をそのまま入れたもので、責務マップの file・依存・公開面は変えていない。外部計画レビューが変更後の版を見る。
  - **外部計画レビュー 1 巡目**（Draft PR #210、対象 commit `2d325893`）。1 件目は cursor のレビュー（R1〜R8）、2 件目は PR に投稿された外部計画レビュー（R1〜R3）。採否は A3 の統合担当の案で、owner が凍結で承認した（§0 の A3 凍結記録）。
    - 1 件目 R1（AC8 で、上限超え 0 のまま 3 run とも failed 0 に届かない時の行き場が無い）: **採用。** 合格・NO-GO・A の再開のどれにもせず、判定 C を完了と記録しないで §7 に記録し、run を足すか打ち切るかを owner が決める（AC8）。最低条件未達で終了しない停止規則と両立する。
    - 1 件目 R2（4 分岐を待ちの前だけでなく、移動の例外の直後と待ちの後にも置く。`staging` が無く `final` があれば成功で戻す）: **一部採用。** 検査を待ちの前と待ちの後（次の移動の前）の両方に置き、AC3 の時期の括弧を直した。`staging` が無く `final` がある時に成功で戻す部分は、2 件目 R1 により不採用。種類の判定と状態の検査の順序は、再試行しない分岐が全て失敗を返すので結果が変わらず、§3 の書き方のままにする。
    - 1 件目 R3（時計が進まない時、41 回目の失敗の後は待たない。`Thread.Sleep` の寝過ごしは待ちの合計に数えない）: **採用。** AC5 の (c) と §3。
    - 1 件目 R4（AC8 の (2) に、試行の回数の無い生の access denied も数える）: **採用。** 後片付けの削除の失敗も数える（2 件目 R2 と合わせて）。
    - 1 件目 R5（起点 filter に J1 で落ちた 2 class を足す）: **採用。**
    - 1 件目 R6（AC1 は Windows の振る舞いに依存するので、Windows 以外は注入した移動で見る）: **採用。** AC1。
    - 1 件目 R7（置き換えの後に別名が残った時も `ReuseRecordFailure` に出す）: **採用。** 新しい機構は足さず、`finally` の残った別名の削除の失敗を成否によらず結果に出す（I3・AC7）。
    - 1 件目 R8（Hub の `projects-v1.json` の場所）: **採用。** `%APPDATA%\UnityHub\projects-v1.json` の `containingFolderPath`（この PC で file と欄があることを 2026-11-23 に確かめた）。
    - 2 件目 R1（AC4 の (b) は成功を確定できない。fail-closed にする）: **採用。** `staging` が無ければ `final` の有無によらず失敗を返し、`final` は消さない（I1 の (b)、AC4、§3）。A2 のアーキテクチャゲート 1 の採用を覆した理由は上の行。
    - 2 件目 R2（上限まで handle が閉じない時、呼び手の後片付けも拒まれ、AC6 の「`staging` を消して失敗の outcome を書く」は今の code と合わない）: **採用。** AC6 から後片付けの成功の保証を外し、B がその経路の観測を test で記録して §6 に書く。後片付けの失敗で outcome が書けない・元の失敗が覆われることは今の code の振る舞いで、本スライスでは直さず「BuildSystem の後続」へ送る。AC8 の (2) は後片付けの失敗も数えるので、上限が足りない時は判定に出る（上限の妥当性の判定は失われない）。
    - 2 件目 R3（「機構を通った」観測と「全回帰が通った」観測を分けて残す）: **採用。** 判定の補助の観測で、log の行の数と試行の回数を XML の結果と分けて §7 に書かせる。合否には使わない（結果に依存する条件を合否に入れると、監視の open と移動が重ならなかった run で原理的に未達になるため）。J1 の小さい再現を同じ環境で走らせ直すことは求めない（再現の script は repo に無く、AC1 と log の行で足りる）。
    - A2 の再レビュー: 要らない。変えたのは AC と §3 の分岐の書き方で、責務マップの file・依存・公開面・asmdef は変えていない。2 件目 R1 の採用で分岐は A1 の初稿に近い形（`staging` があり `final` が無い時だけ再試行する）に戻った。目的・利用者の 2 欄は変えていない。
  - **外部計画レビュー 2 巡目**（PR #210 に投稿された外部計画レビュー、対象 commit `0ae53f8c`）。結論は「1 巡目の R1・R2 は解決、R3 は対応済み、凍結を妨げる指摘は無い」。
    - 軽微な確認（AC7 の、置き換えは成功して別名の削除だけが失敗する場合に「元の失敗」が無い）: **採用。** その場合の期待値を「削除の失敗の文言」と「別名の絶対 path」の 2 つと、置き換えた記録が今回の鍵で読み戻せることに分けた（AC7）。
  - **A3 の統合担当の凍結前の自己点検**（Phase C で読み違いや抜けとして突かれそうな点。設計・責務マップは変えていない）:
    - 上限を超えた時に返す例外の型と文言を固定した（`IOException`、試行の回数・`staging` と `final` の path・最後の失敗の文言。AC5 の (c)）。型が決まっていないと AC6 の「今と同じ catch・finally」を確かめられず、path が文言に無いと AC8 の (2) で staging の失敗を数えられない。
    - AC2 の観測値と AC6 の後片付けの観測の書き先（test の出力と §6）と、AC6 の観測の test を assert しない観測として定義した（観測を合否に混ぜない）。
    - AC8 の判定保留（(2) が 0 件のまま 3 run とも (1) に届かない）を判定定義にも書いた（AC8 と判定定義の食い違いを防ぐ）。
    - 判定 C が読む Editor log の所在（`run-tests.ps1` の `-logFile`）を書いた。`Unity Hub.exe` の process 名をこの PC で確かめた（2026-11-23）。
    - 外部計画レビューの再レビュー: 要らない。2 巡目の指摘の文言の調整と、上の自己点検の精度の修正だけで、どの条件の意味も広げていない。
- 公開の方式の候補の比較（A1 の主担当）: (a) 一時的な lock の失敗に限って移動を短い上限で再試行する（採用）。J1 の調査で、失敗のほぼ全てが直後の移し直しで通ることを再現で確かめている。変更は 2 行の置き換えと小さい関数で、「final があれば公開済み」という読み手の契約を変えない。(b) 移動をやめ、final へ直接書いて完了の印を最後に置く。読み手の全て（再利用の受け取り、publisher、選択、Player の install）の「final がある」の読みを「印がある」へ変え、途中で止まった final の片付けを新しく要する。(a) で足りない証拠が無い今、費用に見合わない（§2 の後続）。(c) test の出力先を監視の外へ出す。owner の入力（Unity Hub は消せない）と、製品の経路（開発者の project の下への build）が直らない点で採らない。

## 6. Phase B 実装結果

- 実装:
  - `Scripts/Editor/Build/Content/ContentStagingMove.cs`（新規、93 行）: internal static の `Move(staging, final)` と、移動・待ち・時計・log・Windows の判定を注入する internal の overload、述語 `IsTransient`。§3 の条件と上限のとおり（Windows で、`IOException` の `HResult` が `0x80070005`・`0x80070020`・`0x80070021`、または `UnauthorizedAccessException`。待ちの前と待ちの後に `staging` があり `final` が無い時だけ移し直す。1 回 50 ms、試行の前と待ちの前に時計を見て 2,000 ms を超えない、試行は最大 41 回。上限超えは最後の失敗を内側に持つ `IOException` で、文言に試行の回数・`staging` と `final` の path・最後の失敗の文言。再試行の後の成功で `[ContentStagingMove] moved after N attempts: <final>` を 1 行）。
  - `BuildContentCoordinator.cs` の 123 行と `ContentTransportPublisher.cs` の 156 行の `Directory.Move(staging, final)` を `ContentStagingMove.Move(staging, final)` に置き換えた（各 1 行、行数は 271・227 のまま）。catch・finally・outcome の書き方は変えていない。
  - `ContentOverlayReuse.cs`（164 → 184 行）: `Write` に置き換えと別名の削除を注入する internal の overload を足した。別名の削除の失敗を握りつぶさず、元の失敗がある時は「元の失敗の文言 + Also, 別名を消せなかったこと・別名の絶対 path・削除の失敗の文言」を本文にし、内側に `AggregateException(元の失敗, 削除の失敗)` を持つ `IOException` を投げる。置き換えが成功して別名の削除だけが失敗した時は「The reuse record was replaced, but …」で始まる 2 つ（削除の失敗の文言と別名の絶対 path）の `IOException`。削除が成功する時は今と同じく元の失敗だけを返す。
  - test: `Tests/Editor/Build/ContentStagingMoveTests.cs`（新規、363 行、16 件。AC1〜AC5 と AC6 の観測）、`ContentOverlayBuildAndReuseTests.cs`（414 → 478 行、AC7 の 3 件）。
- AC ごとの対応する test（起点 filter の run で全て Passed）:
  - AC1: `OpenFileInStaging_ClosedDuringTheFirstWait_MovesOnTheSecondAttempt`（Windows だけ。実 file system の開いた handle）、`InjectedTransientFailure_ThenSuccess_MovesOnTheSecondAttempt`（全 OS。注入した移動）。
  - AC2: 上の Windows の test の中で観測し、述語が一時的と読むことを assert する。`TransientPredicate_TakesOnlyAccessSharingAndLockViolations`、`OutsideWindows_TheSameFailureIsNotRetried`。
  - AC3: `FinalAppearsWithTheFailure_FailsWithoutWaiting_AndKeepsBoth`、`FinalAppearsDuringTheWait_FailsBeforeTheNextMove_AndKeepsBoth`。
  - AC4: `StagingVanishesWithTheFailure_AndNoFinal_Fails`（(a)）、`StagingVanishesWithTheFailure_AndFinalExists_FailsAndLeavesFinalUntouched`（(b)）、`StagingVanishesDuringTheWait_FailsBeforeTheNextMove_WithOrWithoutFinal`（待ちの後の検査で (a)・(b)）。
  - AC5: `FirstMoveSucceeds_CallsNeitherWaitNorClock`（(a)）、`NonTransientFailure_FailsOnceWithoutWaiting`（(b)）、`TransientFailuresContinue_StopAtTheBudget_WithoutWaitingPastIt`・`FailureAt1960Ms_StopsWithoutWaiting`・`ClockDoesNotAdvance_StopsAfterTheFortyFirstFailure_WithoutAnotherWait`・`WaitOversleeps_TheNextAttemptIsNotStartedPastTheBudget`（(c)）。
  - AC6: 既存の coordinator・publisher の test（`BuildContentFullModeTests`、`ContentPurposeIndexPublishTests`、`SourceClosurePublishTests`、`OverlayRegistrationIntegrationTests`、`ContentOverlayBuildAndReuseTests`）が変更なしで通る。観測の test は `HandleNeverCloses_MoveFailsAtTheBudget_AndTheCleanupIsObserved`（Windows だけ、assert は共有の関数の `IOException` だけ）。
  - AC7: `ReplaceAndTemporaryDeleteBothFail_TheReasonCarriesBoth_AndThePath_AndTheExistingRecordIsKept`（coordinator 経由。別名を `FileShare.ReadWrite`（Delete なし）で先に開き、実 file system で置き換えと削除の両方を失敗させる）、`InjectedReplaceAndDeleteFailures_TheMessageCarriesBothTexts_AndThePath`（`Write` に注入、3 つの文言と内側の `AggregateException`）、`ReplaceSucceedsButTheTemporaryFileRemains_AndCannotBeDeleted_ReportsItWithoutAnInventedFailure`（`Write` に注入、2 つの文言と `TryGet` の読み戻し）。既存の `RecordReplaceFails_BuildStaysSuccessful_AndTheExistingRecordIsKept` は変更なしで通る。
- AC2 の観測値（Unity 6000.6.9f1 の Editor、Windows 11）: 1 回目の失敗は `System.IO.IOException`、`HResult=0x80070005`、文言は「Access to the path '<staging>' is denied.」。§3 の述語に入るので、述語は変えていない（§4 の差し戻しの条件に当たらない）。
- AC6 の観測（合否に使わない）: 共有の関数が上限で `IOException` を返した後、`staging` の中の file を `FileShare.ReadWrite | FileShare.Delete` で開いたまま `Directory.Delete(staging, true)` を呼ぶと、例外は無く、`staging` も開いていた file も直後に存在しなかった（この PC の Windows の削除の振る舞い）。開いた handle が `FileShare.Delete` を持たない監視では結果が違いうるので、上限を超えた時の後片付けの失敗の可能性は §2 の後続のまま残る。
- 起点 filter の run の `[ContentStagingMove]` の行: 0（この run の公開の移動は全て 1 回目で成功した）。
- HANDOFF との差（B 適応。契約・責務配置・公開面は変えていない）:
  - `ContentStagingMove` の注入の overload に、移動・待ち・時計・log に加えて Windows の判定（`bool windows`）を足した。本番の `Move(staging, final)` は実行中の OS を渡す。AC1 の「Windows 以外では注入した移動で同じ試行の回数と分岐を確かめる」を、述語の Windows の限定を保ったまま満たすため。
  - `ContentOverlayReuse.Write` の別名の削除の失敗は、`IOException`・`UnauthorizedAccessException` に限らず全ての例外を受けて結果に出す（今までは他の型の例外が `finally` から元の失敗を覆っていた。I3 の「黙って残さない」と「元の失敗を隠さない」の範囲）。
  - 行数の予想との差: `ContentStagingMoveTests` は約 170 → 363 行（AC4・AC5 を待ちの前と後、時計の進み方ごとに分けた）、`ContentOverlayBuildAndReuseTests` は +約 50 → +64 行。製品の file は予想どおり。
  - 置き換えは成功して別名の削除だけが失敗する場合に coordinator が build を成功のまま `ReuseRecordFailure` に写すことは、coordinator の今の一般の catch（変えていない）による。この場合を coordinator 経由で起こす注入の口は無く、足していない（`Write` への注入で例外の文言を確かめた）。
- 実行した検査:
  - `pwsh tools/run-tests.ps1 -Filter '<§5 の起点 filter の 6 class>'`（EditMode、`-nographics`）: XML の root `Passed`、total 81、passed 81、failed 0、inconclusive 0、skipped 0。`step.json` は `passed`・exit 0。Unity の process は自然終了した（終了 stall なし）。結果は worktree の `TestResults/` の下（git 管理外）にあり、Evidence として stage していない。
  - `pwsh tools/contract-audit.ps1`: errors 0、warnings 0（検査 1〜9）。
  - test の run で Unity が書き換えた `unity/Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` は戻した（実装の差分に含めない）。
- 未実行: AC8（判定必須の全 EditMode 回帰、`-WithGraphics`、Unity Hub の監視の下）。判定 C の仕事。`pwsh tools/docs-audit.ps1` は errors 0、warnings 1（既存の `PLAY_CONTENT_RESOLUTION_PROGRAM.md` の §7・§8 についての警告で、本スライスの変更と無関係）。
- implementation head commit: §0 の欄（`d0785b23`）。
- Phase B 担当・モデル・ベンダー: Claude Opus 5.5（Anthropic）。

### 動作確認

- 対象 head: 対象なし
- 条件ごとの実行担当・環境・実行したコマンド: 対象なし
- 生の観測（出力、事前・事後の観測。合否は書かない）と保存先・状態: 対象なし
- 検証経路の期待と違う観測と、B への差し戻し: 対象なし
- `未実施` の条件と理由・担当（未実施が無ければ `なし`、検証経路に該当条件が無いスライスだけ `対象なし`）: `対象なし`（§5。全ての条件が Unity の batch test と XML で閉じる）

## 7. Phase C

### 発見 C

- 種別: 発見（GO 判定はしない。C' は起動しない）。
- 対象: implementation base `d926e3b6` → head `d0785b23` の commit で固定した完全 diff（stat・name-status・diff を読んだ）。開始時の照合: 起動指示の SHA `b295763d` と HEAD が一致し、作業ツリーは clean。implementation head は HEAD の祖先で、head から HEAD までの差分は本書だけ。PR #210 の状態ブロックの引き継ぐ commit・implementation base / head も一致した。PR の timeline に implementation head 宛ての新着は無い（あるのは A3 前の外部計画レビューだけ）。
- 使用した evidence bundle の取得案内・状態: 未生成（発見 C は commit 固定の diff と機械検査だけで行った。review-evidence.md の「テスト未実行でも、固定した diff と機械検査だけで欠陥発見レビューを開始してよい」。判定 C の bundle は判定 C が作る）。
- 構造適合（architecture-gates.md の Phase C の 1〜6）:
  - 1. 責務マップとメンバー: 変更 file は §3 の表と一致する（新規の `ContentStagingMove.cs`・`ContentStagingMoveTests.cs`、coordinator・publisher の各 1 行、`ContentOverlayReuse.cs`、`ContentOverlayBuildAndReuseTests.cs`）。計画外の責務は無い。`ContentStagingMove` は状態を持たない internal static で、公開面は `Move(staging, final)`・注入の overload・述語 `IsTransient`・定数だけ。注入の overload の `bool windows` は §6 の B 適応で、述語の Windows の限定を保ったまま AC1 の「Windows 以外では注入した移動で分岐を確かめる」を満たすための引数であり、契約・責務配置を変えない。
  - 2. 依存・所有者・寿命・公開 API・folder・namespace・asmdef: 計画どおり。`ContentStagingMove` の依存は `System.IO`・`Stopwatch`・`Thread.Sleep`（本番の待ちだけ）・`UnityEngine.Debug.Log`（本番の log だけ）。namespace は `OneStarMaker.Editor.Build.Content`、folder は `Scripts/Editor/Build/Content/`。asmdef・`InternalsVisibleTo` の変更は無い。
  - 3. 単体 test の可能性: 中核の分岐は移動・待ち・時計・log・OS を注入でき、Unity の API なしで実 file system と注入した時間で確かめている（16 件）。
  - 4. 仕様変更の波及: 再試行の条件・上限を変える時の変更は `ContentStagingMove` の定数と述語に閉じ、2 つの呼び手は 1 行の呼び出しのまま変わらない。再利用の記録の別名の扱いを変える時は `ContentOverlayReuse.Write` に閉じる。
  - 5. 行数警報: 発火しない（製品の最大 271 行、`ContentOverlayReuse` は 164 → 184 で +12%）。test の file は 363 行で 500 行未満（§6 の予想 170 行との差は AC4・AC5 を待ちの前と後・時計の進み方ごとに分けたため、責務は AC1〜AC5 と AC6 の観測で増えていない）。
  - 6. 構造劣化: このスライスが作ったものは無い。
  - §4 の「対象外を維持する方法」: base → head の `git diff --stat` に Runtime の `ContentInstaller`・`ContentCacheStore`、SampleGame の `PlayerBuildPublisher`、`TestObservationWriter` は出ない。Editor の `Scripts/Editor/Build/` に残る `Directory.Move` は `ContentStagingMove` の本番の引数だけで、共有の関数を通らない移動の置き忘れは無い（`Directory.Move` の残りは Runtime の `ContentInstaller.cs:113` と `ContentCacheStore.cs:383` で、§2 の後続のまま）。
- 受け入れ条件の照合（code と test を読んで確かめた。test の実行結果は §6 の B の run による）:
  - AC1・AC2: `OpenFileInStaging_ClosedDuringTheFirstWait_MovesOnTheSecondAttempt` は Windows だけで、実 file の handle を `FileShare.ReadWrite | FileShare.Delete` で開き、注入した待ちの 1 回目で閉じる。移動 2 回・待ち 1 回・内容の一致・staging 無しを assert し、1 回目の失敗の型・`HResult` を `Debug.Log` に書いてから述語が一時的と読むことを assert する。時計は注入（`() => 0`）。Windows 以外は `InjectedTransientFailure_ThenSuccess_MovesOnTheSecondAttempt` が分岐を確かめる。述語（`IsTransient`）は §3 の 3 つの `HResult` と `UnauthorizedAccessException` だけで、既定の `HResult` の `IOException`・`0x80070011`・`DirectoryNotFoundException` は入らない。
  - AC3・AC4: 失敗の直後（待ちの前、62 行の `throw;`）と待ちの後（66 行）の両方で `ReadyToMove`（staging があり、final が directory としても file としても無い）を見て、満たさなければ移動を呼ばずに元の失敗をそのまま投げる。状態の検査から成功を返す経路は無い（成功の return は `move` が例外なく戻った後の 69〜70 行だけ）。test は final が現れる・staging が消える（final の有無の両方）を待ちの前と後で起こし、final の bytes が変わらないこと、staging が残ることを確かめる。
  - AC5: (a) 1 回目が成功すると注入した時計も待ちも呼ばない（`Stopwatch.StartNew()` は作るが読まない。注入した時計の読み出しは 0 回と test が確かめる。待たず、今と同じ 1 回の移動なので I2 の「監視の無い環境の振る舞いを変えない」に反しない）。(b) 述語に入らない失敗は `when` の filter を通らず 1 回で外へ出る。(c) 試行の前の検査は 2 回目以降だけ（52 行、`>= 2000` で始めない）、待ちの前の検査は `attempt >= 41` または `経過 + 50 > 2000`（63 行）。上限超えの例外は `IOException` で、内側に最後の失敗、文言に試行の回数・staging と final の path・最後の失敗の文言を持つ。test の 4 件（経過で 40 回目の後に止まる、1,960 ms で待たずに止まる、時計が進まず 41 回目の後に待たない、寝過ごしで 2,100 ms の試行を始めない）の期待値を code の分岐で追い、一致することを確かめた。
  - AC6: 2 つの呼び手は `Directory.Move(staging, final)` を `ContentStagingMove.Move(staging, final)` に置き換えた 1 行だけで、catch・finally・outcome の書き方は変わらない。上限超えは `IOException`（置き換え前の `Directory.Move` の access denied と同じ型）なので、coordinator では一般の `catch (Exception)` に、publisher では今と同じ `finally` に入る。`published = true` は移動の後なので、失敗の時に final を消す分岐には入らない。観測の test は assert を共有の関数の `IOException` と final 無しに限り、後片付けの結果は log に書くだけ（合否に使わない）。
  - AC7: `Write` は置き換えの失敗と別名の削除の失敗を別々に受け、削除が成功すれば元の失敗をそのまま投げ直す（`ExceptionDispatchInfo`。既存の `RecordReplaceFails_…` の振る舞いのまま）。削除が失敗すれば、元の失敗がある時は「元の文言 + Also, 別名を消せなかったこと・別名の絶対 path・削除の文言」を本文に、内側に `AggregateException(元, 削除)` を持つ `IOException`、元の失敗が無い時は「The reuse record was replaced, but …」の `IOException` を投げる。coordinator は `Write` の例外を `catch (Exception)` で受けて `ex.Message` を `WithReuseRecordFailure` に写す（165〜171 行、変更なし）ので、3 つの文言は `ReuseRecordFailure` に届く。test は coordinator 経由（実 file system の lock）と `Write` への注入の 2 経路、置き換えは成功して削除だけが失敗する場合（架空の元の失敗を作らない、`TryGet` で読み戻す）を確かめる。
- 常時契約: 編集・新規の `.cs` は全て先頭に `#nullable enable`。`record` は無い。test に `Task.Delay`・`Thread.Sleep` は無い（`Thread.Sleep` は本番の `Move(staging, final)` の待ちだけ）。`UnityEngine.Object` を扱わないので偽 null の論点は無い。Editor の asmdef の中だけで、Runtime に Editor の code は無い。特定の監視の tool を名指しする分岐・検出は無い（§2 の転記した実装制約）。
- 現在の問いを阻害する findings: なし。
- 後続スライスへ移送する findings: 新規なし（Runtime の 2 つの移動、後片付けの失敗、`PlayerBuildPublisher` は §2 の後続のまま）。
- 半年後の復元性: `ContentStagingMove` の remarks に、成功を状態から推定しない理由、移し直す条件、上限の見方と例外の型・文言の理由、監視の無い環境で待たないこと、tool を名指ししないことが書かれ、§2・§3 の判断を code だけから復元できる。`ContentOverlayReuse.Write` の注釈も、別名を消せなかった時に文言へ書く理由（呼び手が `Message` だけを写す）を持つ。
- 機械検査: `pwsh tools/contract-audit.ps1`: errors 0、warnings 0（検査 1〜9、検査 8 は not-applicable）。`pwsh tools/docs-audit.ps1`（本書の記録の後）: errors 0、warnings 1（§6 と同じ既存の `PLAY_CONTENT_RESOLUTION_PROGRAM.md` の §7・§8 の警告で、本スライスと無関係）。
- 実行したテストコマンドと `-Filter`、対象を選んだ理由: なし。阻害する指摘が無く修正が無いので、差し戻し中の確認のための run は要らない。起点 filter の結果は §6 の B の run（81 件 Passed）で、判定の入力にはしない。AC1〜AC7 の test は判定 C の全 EditMode 回帰に含まれる。
- テスト結果: 発見 C では実行していない。
- 判定必須のうち未実行: AC8（全 EditMode 回帰、`-WithGraphics`、Unity Hub の監視の下で最大 3 run）。判定 C の仕事。
- 重い検証を発見段階で限定実行した場合の理由と範囲: なし。
- 未確認事項: AC1・AC2・AC6 の Windows だけの test の実物の振る舞い（§6 の観測値）は B の run の記録で、発見 C は再実行していない。判定 C の全回帰の Editor log に同じ `[ContentStagingMoveTests]` の行が出る。
- 担当・モデル: Claude Opus 5.5（Anthropic）。Phase B と同じモデルで、`独立性制約あり`（owner 受容。進行役の指定）。

### 判定 C（2026-11-23）

- 種別: 判定 C。結論は **GO**（AC1〜AC8 を全て満たす。AC8 は 1 つ目の run で (1) と (2) が揃ったので、AC8 の定めどおり 2・3 つ目の run は走らせていない）。高コスト証拠は無いので、次の C' が最終 C' である。
- 対象: implementation base `d926e3b6b40dafab086b293e6d60784e304ecf4c`、implementation head `d0785b2327f9795aa9c4dea78ebcbfcf51e2f31f`。実行した checkout の HEAD は引き継いだ `5325b49ae0060cc42557d78a268c878bc75e003f` で、head との差は本書だけ。開始時の照合: 起動指示の SHA と HEAD が一致し、作業ツリーは clean、implementation head は HEAD の祖先。PR #210 の状態ブロックの引き継ぐ commit・implementation base / head も一致した。PR の timeline に implementation head 宛ての新着は無い（あるのは A3 前の外部計画レビューだけ）。
- evidence bundle: 判定用と C' blind bundle を同じ head・同じ生結果から作った（§0 の欄）。判定用は 14 file（目次の `manifest.json`、完全 diff・stat・name-status、C' 用と同じ本書を除いた diff・stat・name-status、凍結版 `f6a6e594` と B の記録 `b295763d` の本書、run1 の `results.xml`・`unity.log`・`step.json`、実行記録、contract-audit・docs-audit の出力）。C' 用は 11 file（完全 diff を除いた物。凍結版と B の記録の本書に発見 C の所見は無い。implementation head の本書は凍結版と同じ）。
- 構造適合: 発見 C と同じ diff（10 file。本書・program の 1 行・§3 の file と新規 file の `.meta`）を自分でも照合した。`ContentStagingMove.cs` を読み、成功の return が移動が例外なく戻った後だけであること、失敗の直後と待ちの後の両方で `staging` があり `final` が無い時だけ移し直すこと、試行の前（2 回目以降）の 2,000 ms と待ちの前の「41 回目」「経過 + 50 ms」の止め方、述語が §3 の 3 つの `HResult` と `UnauthorizedAccessException` だけであること、上限超えが最後の失敗を内側に持つ `IOException` で文言に試行の回数と 2 つの path を持つことを確かめた。§4 の対象外（Runtime の `ContentInstaller`・`ContentCacheStore`、SampleGame の `PlayerBuildPublisher`、`TestObservationWriter`）は diff に無い。
- 実行した判定必須テスト（§5。sandbox 外、repo root から `pwsh -NoProfile -File tools/run-tests.ps1 -WithGraphics`、空 filter、EditMode）:
  - run1 の前（2026-11-23 12:02:29）: `Get-CimInstance Win32_Process` に `Unity Hub.exe` 6 process（主 process は 2026-11-22 13:52 起動）と `ChatGPT.exe` 12 process（主 process は 2026-11-22 15:35 起動。止めていない）があり、`Unity.exe` は無かった（`Unity.Licensing.Client.exe` だけ）。Hub の `%APPDATA%\UnityHub\projects-v1.json` の `containingFolderPath` に `D:\repositories\unity` がある。
  - run1（12:02:48〜12:20:08）: XML の root `Passed`、total 2263、passed 2261、failed 0、Inconclusive 0、skipped 2（`OverlayFromLocalEditsR8Tests` の 2 件。本スライスの diff に無い既存の test で、XML の reason は「名前を選んで走らせる」）。`step.json` は `passed`・exit 0、理由は空。この run の Unity は XML の後に自然終了した（終了 stall なし、強制終了なし）。run の後の `git status --porcelain` は空。
  - 同時に動いた他の Unity: run1 の 12:18:17 に、Unity Hub から別の project（`osm-ds3`）の Editor（PID 38772）が起動し、run1 の最後の約 2 分と重なった。この run のものではないので止めていない。重なりによる失敗は無い（failed 0）。
  - run2・run3: 走らせていない（AC8 の「(1) と (2) を満たした時点で残りの run は走らせない」）。
- 判定の補助の観測（合否に使わない）: run1 の Editor log（`unity.log`）に `[ContentStagingMove]` の行が 3 本あり、試行の回数はどれも 2 回だった。`final` はどれも repo の下（`TestResults/bs3-fixture/` の `bs5b1-…`・直下・`s34a-…` の `content/<時刻>-<guid>`）で、Unity Hub の監視の範囲にある。つまり「機構を通った観測」は 3 回の移動で、1 回目の移動が一時的な失敗になり、待ちの後の 2 回目で通った。「全回帰が通った観測」は上の XML である。log の access denied・共有違反の行は、AC2 の観測の 1 行（`[ContentStagingMoveTests] AC2 observed first failure: type=System.IO.IOException HResult=0x80070005 …`、temp の下の test の staging）だけだった。AC6 の観測の行は §6 と同じ（例外なし、`staging` も開いていた file も無い）。

**受け入れ条件:**

| AC | 結果 | 根拠 |
|---|---|---|
| AC1 | 合格 | run1 の XML で `OpenFileInStaging_ClosedDuringTheFirstWait_MovesOnTheSecondAttempt`（Windows の実 handle）と `InjectedTransientFailure_ThenSuccess_MovesOnTheSecondAttempt` が Passed |
| AC2 | 合格 | 同じ test の中の観測が `System.IO.IOException`、`HResult=0x80070005`（run1 の log の行。§6 の B の観測と同じ）で、§3 の述語に入る。`TransientPredicate_TakesOnlyAccessSharingAndLockViolations`・`OutsideWindows_TheSameFailureIsNotRetried` が Passed |
| AC3 | 合格 | `FinalAppearsWithTheFailure_FailsWithoutWaiting_AndKeepsBoth`・`FinalAppearsDuringTheWait_FailsBeforeTheNextMove_AndKeepsBoth` が Passed。code の 62・66 行の分岐と一致 |
| AC4 | 合格 | `StagingVanishesWithTheFailure_AndNoFinal_Fails`・`StagingVanishesWithTheFailure_AndFinalExists_FailsAndLeavesFinalUntouched`・`StagingVanishesDuringTheWait_FailsBeforeTheNextMove_WithOrWithoutFinal` が Passed。状態の検査から成功を返す経路は無い |
| AC5 | 合格 | `FirstMoveSucceeds_CallsNeitherWaitNorClock`・`NonTransientFailure_FailsOnceWithoutWaiting`・`TransientFailuresContinue_StopAtTheBudget_WithoutWaitingPastIt`・`FailureAt1960Ms_StopsWithoutWaiting`・`ClockDoesNotAdvance_StopsAfterTheFortyFirstFailure_WithoutAnotherWait`・`WaitOversleeps_TheNextAttemptIsNotStartedPastTheBudget` が Passed（`ContentStagingMoveTests` の 16 件は全て Passed） |
| AC6 | 合格 | 2 つの呼び手は 1 行の置き換えだけ。既存の `BuildContentFullModeTests`（25）・`ContentPurposeIndexPublishTests`（6）・`SourceClosurePublishTests`（8）・`OverlayRegistrationIntegrationTests`（1）・`ContentOverlayBuildAndReuseTests`（25）が全て Passed。観測の test `HandleNeverCloses_MoveFailsAtTheBudget_AndTheCleanupIsObserved` も Passed |
| AC7 | 合格 | `ReplaceAndTemporaryDeleteBothFail_TheReasonCarriesBoth_AndThePath_AndTheExistingRecordIsKept`・`InjectedReplaceAndDeleteFailures_TheMessageCarriesBothTexts_AndThePath`・`ReplaceSucceedsButTheTemporaryFileRemains_AndCannotBeDeleted_ReportsItWithoutAnInventedFailure` と既存の `RecordReplaceFails_BuildStaysSuccessful_AndTheExistingRecordIsKept` が Passed |
| AC8 | 合格 | 前提: Unity Hub が動き、登録に `D:\repositories\unity` がある（run1 の前の記録）。(1) run1 の XML が root `Passed`・failed 0・Inconclusive 0。(2) 走らせた唯一の run（run1）で、staging の path を指す access denied・共有違反で落ちた test は 0 件（failed 0 で、failure のある test-case が無い。log にも staging の path の access denied は AC2 の観測の 1 行以外に無い） |

- A3 の目的レビューの不採用の項（AC8 の run で再利用の記録の置き換えが監視で失敗したら記録する）: 該当なし（failed 0 で、`ReuseRecordFailure` が null であることを確かめる既存の test も全て Passed）。
- 現在の問いを阻害する findings: なし。
- 後続スライスへ移送する findings: 新規なし。§2 の後続（Runtime の 2 つの移動、後片付けの失敗、`PlayerBuildPublisher`）はそのまま。§2 の「全 EditMode 回帰の他の不安定」は、run1 に失敗として出ていないので program に足さない（他の Unity の同時起動は上に記録しただけ）。
- 機械検査（HEAD `5325b49a`、run1 の後）: `pwsh tools/contract-audit.ps1`: errors 0、warnings 0（検査 1〜9、検査 8 は not-applicable）。`pwsh tools/docs-audit.ps1`: errors 0、warnings 1（§6 と同じ既存の `PLAY_CONTENT_RESOLUTION_PROGRAM.md` の §7・§8 の警告）。
- 判定必須のうち未実行: なし（AC8 は 1 run で合格の条件が揃った）。
- 未確認事項: bundle の受け取り側の別 session の fetch・閲覧（送り側の別 process の fetch では、どちらの bundle も `passed` で manifest の全 file の hash が一致した）。ChatGPT の監視は止めていないので、run1 の 3 回の再試行のどれが Unity Hub と ChatGPT のどちらの open によるかは分けていない（AC8 は分けることを求めない）。J1 の「直前に閉じた後の失敗」の形を AC1 が代表するかは §2 のとおり答えないが、run1 の 3 回はどれも 2 回目で通った。
- 担当・モデル: Claude Opus 5.5（Anthropic）。Phase B・発見 C と同じモデルで、`独立性制約あり`（owner 受容。進行役の指定）。
