# 末尾 JSON 変換失敗の検証

## 0. メタデータ

- type: slice（調査スパイク）; status: A3 統合中・未凍結
- branch: `codex/logging-tail-failure`
- implementation base: `054dc759e98d63e998665aa2f99298a63cc9f8e6`; implementation head: 未固定
- risk: high（非同期の同期と終了境界）; owner: root; created: 2026-11-14; expires: 結論の harvest または 2026-12-31
- harvest: Issue #183 と必要な公開ログ文書。修正が必要なら別スライスの Phase A
- Phase A snapshot: 未生成。B result / C evidence / C' blind bundle: 未生成。各取得案内は次担当への引渡し未完了。
- 引継ぎ: A3 で同期の成立とレビュー指摘を裁定する。凍結・Phase B 開始前に停止。実装 head と実行結果はまだない。

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
- 受け入れ観測: 各 provider の全 bytes が完全な A の JSON 1 行と改行のみなら保持、A 不在なら損失、余分な bytes は B 断片などとして記録する。JSON の識別子・値・完全な行終端を確認し、空行・未終端 bytes を捨てない。通知は Dispose 後に全 notice file を読み provider ごとの失敗通知の有無・件数・種別を確認する。A 不在や B 断片は製品期待の不成立であって検証基盤の不成立とは区別する。
- 条件成立例: 両 A 変換開始を確認し B 投入完了後に A を解放し、両 B 変換到達と失敗、Dispose を観測。A の出力サイズが writer の 65536 bytes より十分小さい。未成立例: 一方の gate に到達しない、途中 flush がありうる、B 投入前に A を解放、別 entry が混じる、Dispose 未完了。未成立では保持・損失を結論しない。
- 後続: 損失・断片・通知欠落が観測された場合、原因と対処責務を別 Phase A で設計する。条件成立で問題が観測されなくても本スライスの条件外へ一般化しない。
- 分割: 再現の成否と修正は独立した問い。後者を分離し、本スライスでは検証だけを扱う。
- 判定: GO は条件成立と両 provider の全 bytes / 通知を記録できた場合（製品期待が不成立でも観測結果を明記）。NO-GO は条件または観測が不成立・不明。修正の GO ではない。条件成立不能なら原因を記録して Phase A に戻り、成功扱いしない。
- 制約: 新規公開 API / asmdef 依存追加なし。同期は signal / barrier と失敗時の有限 watchdog を使い `Task.Delay` / `Thread.Sleep` は使わない。例外・timeout 時も gate を解放して worker を回収する。テスト以外の製品修正を行わない。
- 未決: gate が両実 provider で到達可能か、ログ値の getter が期待どおり非同期で呼ばれるか、A の未 flush を十分に証明できるか。Unity で未実行。

## 3. 責務マップ

- `unity/Assets/OneStarMaker/Tests/Foundation/AppLoggerFactoryJsonTests.cs`（現 248 行程度、増分概算 100〜180 行）: 実 factory を通す観測テスト。一時 directory と同期 fixture はテストが所有し、Dispose / teardown まで。既存 asmdef とログ依存のみ。規模・同期 fixture の独立性によって同 Foundation テスト内の専用 file への分離を検討し、公開面は増やさない。最終行数と配置は A3 で決める。
- `AppLoggerFactory.cs` / `BufferedJsonFormatter.cs` / ZLogger: 読取対象。製品コードの変更は含めない。試験用 seam が必要と判明したら Phase A に戻す。

## 4. 検証手順案

1. 実 factory を新規一時 directory と realtime JSON の観測 stream で構築。rolling と realtime はそれぞれ独立 worker。小さい A の構造化値の getter で各 worker の到達を signal し、解放まで待機する案。メッセージの `ToString` は gate しない。
2. 両 A の到達後に B を同じ logger へ投入し、投入が両 provider へ完了したことを確認して A を解放。B の getter は各 worker の到達を signal してから明示的に変換例外を起こす。B の後に entry を送らず Dispose。共有値が二つの worker で呼ばれることを前提にせず、到達数・識別・重複を検査する。
3. 根拠: ZLogger 2.5.10 `AsyncStreamLineMessageWriter.WriteLoop` は内側の `TryRead` を空になるまで処理し、その後 `writer.Flush()`、例外は外側 catch で通知する。`StreamBufferWriter` の既定容量は 65536 bytes、容量超過などで途中 flush する。A が十分小さく、両 A を保持中に B が enqueue 済みであれば各 worker の次の読取りは B になるというソース上の推論。A に先行 entry を置かず、A のサイズと行終端を上限内とする。必要なら realtime stream の write 回数・rolling file の bytes を B 到達時に補助観測するが、file buffering の見かけだけを内部状態の証明にしない。実 package と upstream 版の一致・実行時の getter と同期の成立は未確認。
4. A 単独の対照 run で完全な A の出力と fixture 自体の正常性を確認する。失敗 run の全 bytes は空も含め採取し、JSON の行数だけでなく残余 bytes を判定する。通知の source、例外種別、件数を記録し、個人情報や例外メッセージを要求しない。
5. B の gate が actual serializer の失敗にならない、同期点が働かない、writer の未 flush を証明できない場合は、未成立として停止。seam を勝手に追加したり、修正を実装しない。

## 5. テストとレビュー計画

- 差し戻し中の起点 filter: `OneStarMaker.Tests.Foundation.AppLoggerFactoryJsonTests` または分離先の明示テスト名。XML の実行名・件数を確認。
- 判定 C: 標準 `pwsh tools/run-tests.ps1` の EditMode 限定試験で観測を取得し、テストコードを追加するため最後に空 filter の全 EditMode 回帰。現行不具合を観測する試験は期待値と観測を分離し、仮説が当たったことを失敗隠しに使わない。生 bytes・通知・XML・実行条件を保存する。
- Unity 経路: 未確認。初回確認は Phase C。担当は C の実行者、Editor 不在を確認し、対象 checkout の `ProjectVersion.txt` の Editor と標準 runner を使う。条件成立不能は上記停止規則。実 fixture の getter 到達・二つの provider と package の一致は C で初確認。操作・目視の別条件はなし。
- 高コスト証拠: なし。判定 C の独立照合 script: なし（テストが全 bytes を検査し生データを残す）。機械検査: docs-audit、B/C は contract-audit。
- A0/A1: Codex / OpenAI。A2: 目的レビューは別 session、A1 原文のみ。アーキテクチャ・同期の独立レビューは手配中。C' は未関与のモデル系列を予約。
- A3: 統合中。採否はレビュー後に記録し、人間の裁定と凍結は未実施。

## 6. Phase B / 動作確認

未着手。implementation head 未固定。動作確認: 対象なし（Unity test は C）。

## 7. Phase C

未着手。evidence bundle 未生成。判定必須テストすべて未実行。

## 8. Phase C'

未着手。blind bundle 未生成。担当と独立性は凍結前に確定。

## 9. Phase D

未着手。保存・引渡し、harvest、task 終了と後始末は未実施。
