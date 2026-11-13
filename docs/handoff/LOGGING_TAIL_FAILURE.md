# 末尾 JSON 変換失敗の検証

## 0. メタデータ

- type: slice（調査スパイク）; status: A3 凍結済み・Phase B 待ち
- branch: `codex/logging-tail-failure`
- implementation base: `054dc759e98d63e998665aa2f99298a63cc9f8e6`; implementation head: 未固定
- risk: high（非同期の同期と終了境界）; owner: root; created: 2026-11-14; expires: 結論の harvest または 2026-12-31
- harvest: Issue #183 と必要な公開ログ文書。修正が必要なら別スライスの Phase A
- Phase A snapshot: A3 凍結版をローカル固定済み（未昇格）。取得案内は既存の手元の運用メモに保管。送信側で取得・閲覧を確認、次担当による取得確認は引渡し時に行う。B result / C evidence / C' blind bundle: 未生成。
- 引継ぎ: 次担当: Phase B 実装。引き継ぐ commit を HEAD と照合し、凍結 snapshot の案内に従い取得・内容確認後、§3〜§5 のテスト支援だけを実装する。新規の公開 API / asmdef / 製品 seam が必要なら Phase A に戻す。Unity テスト・Build は実行しない。完了時に contract-audit、未実行を記録し commit する。implementation head は未固定。

## 1. 目的と対象外

- 目的: rolling JSON と realtime JSON の実 factory において、正常 entry A の直後の末尾 entry B がシリアライズに失敗し Dispose される条件で、A の保持、B 断片の不出力、障害通知を確定的に検証する。
- 利用者: なし
- 対象外: 修正方式・製品動作の変更、MessagePack、Player/AOT、性能や広範な障害回復、別のログを追加して暗黙 flush させること。
- 現況: `AppLoggerFactory` は rolling と realtime JSON に別 provider / formatter と共有通知先を構成し、Dispose は provider を先に閉じる。`BufferedJsonFormatter` は entry を局所 buffer に書いてから provider writer に移す。既存 `BothProviders_PreserveVectorPrimitiveExceptionAndFollowingLines` は B 後に正常 C を書くため末尾 B の証拠ではない。A の喪失は Issue #183 のソース上の可能性で、実 factory での再現・保持いずれも未確認。
- UC ごとの利用者入力: なし。試験担当が fixture と対象版を開発時に決める。

## 2. 意思決定と受け入れ境界

- 問い: 実 factory の両 JSON provider で A が未 flush のまま末尾 B の変換が失敗し Dispose したとき、全出力と通知はどうなるか。
- 不変条件: テストが実 factory/provider/formatter を置き換えたり、時間待ちや後続正常 entry で経路を変えたりせず、条件未成立を成立扱いしない。対象は factory からの全出力 bytes と通知。I/O 故障、他形式は未探索。
- 成立条件: 両 provider ごとに A → B が同一 drain 内である根拠と A が内部未 flush である根拠、B が末尾で失敗し Dispose が完了した事実を示し、出力を判別する。
- 受け入れ観測: 成立条件を確認した後、各 provider の全 bytes を (1) fixture の識別子と値を持つ完全な A の JSON オブジェクト 1 件と `Environment.NewLine` だけ、(2) 0 bytes、(3) それ以外の残余に分類する。(1) は保持、(2) は A 不在。(3) は空行・未終端も保持し、A の部分と B 由来を識別できた範囲だけ記録し、全体を B 断片と決めつけない。`ParseLines` は空行を捨てるので使わない。通知は Dispose 後に全 notice file を読み、provider ごとに行あり / 行なし / 読取失敗を記録する。行なしだけを通知欠落として後続の修正 Phase A を開かない。行なしも記録できた通知観測に含める。読取失敗は NO-GO。行なしは callback 不在を意味しない（通知先の I/O 失敗は握り潰される）。型は記録するが特定型を前提にしない。条件未成立の bytes は損失・保持の判定に使わない。
- 成立例: A の getter で異なる 2 worker が同時待機 → `Log(B)` 正常復帰 → A 解放 → B の getter で異なる 2 worker が同時待機 → realtime の `Write(byte[], int, int)` が 0 回、両 provider それぞれの A の改行を除くコピー長が 65533 bytes 以下を確認 → B を解放し変換失敗 → Dispose 完了。B は末尾。A/B の gate、呼び出し、write 回数と出力を記録する。
- 未成立例: 同時待機を確認できない、`Log(B)` の異常、write が 0 でない、サイズ超過、timeout、他 entry 混入、Dispose 未完了、実行 DLL と照合対象の不一致。いずれも生観測を保全するが製品の損失・保持を結論しない。
- 後続: A 不在、識別できた A の部分または B 由来の残余、通知行があり内容が揃わない場合、原因と対処責務を別 Phase A で設計する。条件成立で問題が観測されなくても本スライスの条件外へ一般化しない。
- 分割: 再現の成否と修正は独立した問い。後者を分離し、本スライスでは検証だけを扱う。
- 判定: GO は条件成立と両 provider の全 bytes / 通知を記録できた場合（製品期待が不成立でも観測結果を明記）。NO-GO は条件または観測が不成立・不明。修正の GO ではない。条件成立不能なら原因を記録して Phase A に戻り、成功扱いしない。
- 制約: 新規公開 API / asmdef 依存追加なし。同期は signal / barrier と失敗時の有限 watchdog を使い `Task.Delay` / `Thread.Sleep` は使わない。watchdog は producer / Dispose の待機とは別系統で動き、例外・timeout 時に A/B 両 gate を解放する。producer と Dispose の終了を有限に確認し、回収できなければ未成立として記録する。テスト以外の製品修正を行わない。
- 未決: Unity Editor で両 worker が A/B getter に同時到達するか、対象実行で各条件が成立するか。Unity で未実行。DLL 経路は外部レビューによる照合であり、実行観測ではない。

## 3. 責務マップ

- `unity/Assets/OneStarMaker/Tests/Foundation/AppLoggerFactoryJsonTests.cs`（現 240 行、増分概算 100〜180 行）: 実 factory を通す観測テスト。一時 directory と同期 fixture はテストが所有し、Dispose / teardown まで。既存 asmdef とログ依存のみ。同期と証拠保全は独立して変更されるため、同 Foundation の専用テスト file に配置する。fixture が gate、到達記録、worker の回収と一時資源を所有する。公開面は増やさない。
- `AppLoggerFactory.cs` / `BufferedJsonFormatter.cs` / ZLogger: 読取対象。製品コードの変更は含めない。試験用 seam が必要と判明したら Phase A に戻す。

## 4. 検証手順案

1. 失敗 run と対照 run はそれぞれ別の新規一時 directory とし、観測可能な realtime JSON stream で実 `AppLoggerFactory` を構築し、他 entry を送らない。A の小さい構造化 probe は通常の managed object とし、JSON だけが読む property getter でスレッド ID と同時待機をスレッドセーフに記録する。`ToString` は gate に触れない。A の getter に異なる 2 worker が同時に待つまで signal で待つ。累積 2 回だけでは不可。
2. A を保持したまま `Log(B)` を呼び、正常復帰を確認してから A を解放する。Dispose を開始しない間は channel を閉じない。B も managed probe の getter で異なる 2 worker が同時に待つ。例外、timeout、重複 worker だけの場合は未成立。
3. 両 B が同時待機中に realtime の `Write(byte[], int, int)` 回数が 0 と確認する。先行 entry のない対照 run の rolling と realtime 各々について、完全な A の bytes 長からその process の `Environment.NewLine` の byte 数を引き、改行を除く JSON コピー長が両方とも 65533 以下か確認する。実行間の可変部分により同じ上限を保証できない場合も未成立。rolling file の長さは内部未 flush の証拠に使わない。
4. B の両 gate を解放して serializer 内で変換例外を起こし、B の後に何も送らず Dispose する。Dispose が完了したら 失敗 run の rolling の全 log bytes を出力順に結合した 1 本、realtime の全 bytes、および notice 全 file を §2 の分類で観測する。`logging-errors_*.txt` を log bytes に混ぜない。複数 rolling file の出力順を確定できなければ未成立。A は fixture の識別子・値と JSON の構造で確認し、時刻が変わる対照 run との byte 一致は要求しない。対照 run の A も完全な 1 行であることを確認する。通知の例外型を決め打ちせず、provider の `|rolling-json|` / `|realtime-json|` を照合する。
5. 外部レビューは、同梱 ZLogger 2.5.10 / Microsoft.Extensions.Logging 8.0.0 の DLL を逆コンパイルし、両 provider の同じ writer、`Grow` の同期 `Post`、同一 drain と容量・改行の条件を照合したと報告している（PR #185 の外部レビュー）。`Log(B)` 復帰の根拠は Dispose 前・両 provider 有効・既定 `Grow`・channel 未完了であること。これらが崩れる、実行 DLL が照合対象と異なる、または前提を確認できないときは未成立。rolling file の見かけや最終 bytes を証明に代用しない。DLL 照合はレビュー報告であり Unity 実行結果ではない。
6. gate / producer / Dispose のどこで失敗・timeout しても、独立 watchdog が両 gate を解放する。producer と Dispose の終了を有限に確認し、終了できなければ処理を成功にせず証拠と未回収を残す。Unity test に必要な初回の到達確認は Phase C。実 factory を維持して条件を満たせなければ Phase A に戻し、製品 seam や修正を勝手に追加しない。

## 5. テストとレビュー計画

- 差し戻し中の起点 filter: `OneStarMaker.Tests.Foundation.AppLoggerFactoryJsonTests` または分離先の明示テスト名。XML の実行名・件数を確認。
- 判定 C: 標準 `pwsh tools/run-tests.ps1` の EditMode 限定試験で観測を取得し、テストコードを追加するため最後に空 filter の全 EditMode 回帰。現行不具合を観測する試験は期待値と観測を分離し、仮説が当たったことを失敗隠しに使わない。生 bytes・通知・XML・実行条件を保存する。成功・失敗・timeout のいずれも一時 directory の削除前に、両 provider の全 bytes（0 bytes の存在と長さも）、通知の読取成否・内容、A/B の同時待機とスレッド ID、Log(B) の結果、write 回数、コピー長、timeout、Dispose の終了状態、対象 SHA・実行 DLL の版と hash を、プロセス終了後も残る Git 管理外の専用結果 directory に保存する。Unity runner の XML / log と紐付け、収集できなければ未成立。0 bytes を空 file として stage できない場合は長さ 0 と内容 hash を manifest に記録し、他の非空証拠と一緒に収録する。C は採取物を Artifact Storage に stage し、C/C′ が取得・閲覧できる案内を渡す。
- Unity 経路: 未確認。初回確認は Phase C。担当は C の実行者、Editor 不在を確認し、対象 checkout の `ProjectVersion.txt` の Editor と標準 runner を使う。条件成立不能は上記停止規則。実 fixture の二つの worker の getter 到達と write 回数は C で初確認。実行 DLL の版・hash を照合済み対象に照らす。操作・目視の別条件はなし。
- 高コスト証拠: なし。判定 C の独立照合 script: なし（テストが全 bytes を検査し生データを残す）。機械検査: docs-audit、B/C は contract-audit。
- A0/A1: Codex / OpenAI。A2 入力 commit `6845a1a4795d89336db6038ed0be92596088c3e9`、HANDOFF の内容 sha256 `74a8b890e5896cb9d2c4490c141d753d4dee089380490784d3511f07a6093023`。同内容の Phase A snapshot をローカル固定、各レビュー担当が閲覧。上記 hash は Git blob と取得 snapshot の LF bytes。
- 目的レビュー: 別 session の Codex / OpenAI。目的原文のみ、sha256 `bbf316efe2270baee149c8ea1c406272012dffa736d3f0ec14ba42d8942facc0`。両実 provider、完全な A、B 断片なし、独立通知、同期と Dispose 完了を保証し、利用者の追加操作・公開 API を作らないことを提案。採用: 出力と通知を区別して観測する。完全な A と B 断片なしは製品期待であり、仮説をテストの成功条件にせず、成立条件と観測の記録をもって本スパイクを判定する。
- アーキテクチャ A2: 別 session の Codex / OpenAI、上記 commit と snapshot を取得・閲覧し hash 一致を確認。採用: teardown 前の空出力を含む証拠保全、fixture の所有・回収責務を具体化。製品コード・公開依存の追加なしの境界は維持。
- 同期 A2: Grok 4.5 Medium / xAI、上記 snapshot を閲覧（hash は取得側で照合、他 source は未閲覧）。指摘と暫定採否:
  1. 同一 drain / 未 flush の正の証拠が弱い: 採用。realtime の B 到達時 write 0 を観測し、rolling は DLL と対応する source の動作、両 gate と A の容量境界から導けるかを検討する。外部レビューによる DLL の経路照合と同時待機・容量条件を採用し、実行時に条件が満たせなければ未成立。最終出力から逆算しない。
  2. A 不在と条件不成立の区別: 採用。条件成立を独立に確認した後にのみ出力を分類する。
  3. B の両 enqueue を A 解放前に確認する方法が曖昧: 採用。外部レビューの DLL 照合に基づき、Dispose 前・両 provider 有効・既定 Grow の下で正常復帰を条件とする。B 到達だけでは投入時刻を証明しない。前提を確認できなければ未成立。
  4. Unity 試験未実行のため実行可能性未確認: 採用。Phase C 初回確認に委ねる箇所と停止規則を明記し、成功を先取りしない。凍結前にその委譲を人間が判断する。
  5. 共有通知 sink で provider 別帰属が可能か: 採用。`LoggerFailureFile` の provider フィールドと通知全行を照合し、通知取得不成立と通知欠落を区別する。
- PR #185 外部レビュー（Cursor、同梱 DLL 逆コンパイル）と独立レビュー（ソース確認）: 暫定採用。2 worker の同時待機、`Log(B)` 復帰順、B 同時待機中 write 0、65533 bytes、watchdog の独立性、終了前の証拠保全を §2 / §4 / §5 に統合。出力残余は B 断片と一括分類せず、通知行なしを callback 不在と断定しない。外部レビューの DLL 照合は報告として採用し、Unity 実行を済ませた主張にはしない。
- 外部再レビュー（Cursor、PR 本文の hash 一致、snapshot 未取得の assist）: 対照 run の分離、provider ごとの改行を除くサイズ、識別子・値での A 判定、通知行なしと読取失敗の区別を採用。独立レビューは追加 blocker なしと報告したが snapshot 未取得。owner がこれらを取り込む A3 凍結を指示。対象 package は ZLogger 2.5.10 / Microsoft.Extensions.Logging 8.0.0 の同梱 DLL で、レビュー担当の照合値は `149f3ae932cbd19a24f0597ce9ff8688e0fbf8070aab2d892c241e92defda5af` / `1a13da2bb23ebdd1af5776b5d49e587d78192b3aa1314e99589fe1cda1b4b16c`。A3 担当は LFS 実体を取得して同じ hash を確認した。Phase C は実際に実行される DLL の hash をこの値と照合する。
- C′: A/B/C 未関与の別モデル系列または人間を予約し、凍結前に担当を確定。
- A3: owner が 2026-11-14 に外部再レビューの指摘を統合する A3 凍結を指示し、主担当が統合・凍結した。A0 代替案レビュー（目的レビュー後の同担当、初稿を見ず A0 のみ）は実 factory を保った観測点と、未 flush / 同一 batch / B 失敗を証明できない場合の停止を提案。採用: 停止条件と一致。A 後の内部 buffer に直接 gate を置く具体的観測点は未確認のため保留し、新規製品 seam の追加は決めない。登録済み PowerShell 7.7.0 で scheduler status passed、snapshot stage/fetch passed。初回 stage は未生成 packaging DLL により unconfirmed、DLL の build 後に再実行して成功。scheduler job 自体の直近結果は partial（blocked 1）で、stage 成功とは別。

## 6. Phase B / 動作確認

未着手。implementation head 未固定。動作確認: 対象なし（Unity test は C）。

## 7. Phase C

未着手。evidence bundle 未生成。判定必須テストすべて未実行。

## 8. Phase C'

未着手。blind bundle 未生成。担当と独立性は凍結前に確定。

## 9. Phase D

未着手。保存・引渡し、harvest、task 終了と後始末は未実施。
