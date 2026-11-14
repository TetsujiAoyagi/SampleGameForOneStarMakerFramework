# JSON 末尾変換失敗時の正常行保持

## 0. メタデータ

- type: `slice`
- status: `A`（revision 1、A1・A2 入力版。未凍結）
- branch: `codex/logging-tail-retention-design`
- implementation base commit: `41099fb7a2c7e191d940614024031898214eed28`
- implementation head commit: 未固定
- risk: `high`（非同期 drain の失敗経路・Dispose 境界を変える）
- owner: repository owner / Phase A 主担当
- created: 2026-11-14
- expires: 2026-11-30 または置換 revision
- harvest to: Architecture §12 G5、Issue #183
- task: `logging-tail-retention`。Cloud Linux の `workflow-task.ps1 start` は `store-root-unavailable`、未登録。対応済み Windows 環境で開始する。store の代替実装・迂回はしない。
- Phase A snapshot の取得案内・状態: 未保存。Cloud では Windows 専用 Storage を利用できない。原入力を保全し、対応済み環境で A 担当が固定・取得確認する。現環境での snapshot を欠く構造レビューは assist とし、正式な A2 完了に数えない。
- Phase B result snapshot の取得案内・状態: 未生成。B 担当から動作確認・C 担当へ。
- evidence bundle の取得案内・状態: 未生成。C 担当から C′ / D 担当へ。
- C′ blind bundle の取得案内・状態: 未生成。所見と分離して C′ 担当へ。
- 引継ぎ票: 次は Phase A。A2 入力版の構造・代替構成レビューと snapshot の正式な受渡し、未決事項の解消、owner による A3 採否・凍結を行う。B へはまだ渡さない。読む範囲は全節。Unity テストを必要とする初回検証は C 担当へ。既存 PR #185 の完了済み作業台は復活させず、その製品修正をこの新スライスで扱う。

## 1. 目的と対象外

- 目的: AppLoggerFactory の rolling JSON と realtime JSON で、末尾 entry の JSON 変換失敗によって、それ以前に受理した正常 entry が Dispose 後の出力から失われることを防ぎ、失敗 entry の断片を出さずに障害通知を残す。
- 利用者: AppLoggerFactory が提供する ILogger<T> / ILogger からログを送る開発者。既存のログ呼出しと factory の Dispose を使う。
- 対象外: Player / AOT の実証、プロセス強制終了、通常出力または通知先の I/O 障害からの回復、MessagePack 方式変更、ZLogger 更新・fork、キュー再実装、再試行・永続キュー、PR #185 の一時物清掃。
- 現況: [PR #185](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/185) は観測スライスで製品修正ではない。[Issue #183](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/issues/183) は open。Editor 6000.6.0f1 の成立した同期・容量条件で、両 provider の出力は 0 bytes、正常 A 不在、通知行あり。rolling の未 flush は同梱 DLL 経路からの導出で、runtime の直接計測ではない。
- A0 の制約: 公開面は現況だけを説明する。未検証の対処を G5 に保証済みと書かない。PR #185 の VFX 除外の owner 裁定は今回へ自動適用しない。

### UC ごとの入力

| UC | 利用者が選ぶ・入れるもの（決定時点） | 機械が決めるもの | 目的からの正当化 |
|---|---|---|---|
| 通常のログ送信 | 既存 API の category、level、template、値（開発時宣言・実行時入力） | provider ごとの JSON 変換、成功時の行確定、失敗時の隔離と固定 provider 名の通知（自動導出） | 元の観測対象を送るため。新しい入力は要求しない |
| 終了 | 既存 factory の Dispose（実行時操作） | provider drain → 通知終了（既定の所有順） | 受理済みログの出力完了境界として必要。追加 Flush・待機・再送を要求しない |

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: JSON の変換失敗を entry 内で隔離し、既存 provider の drain を完遂させることで、末尾失敗時も正常行を保持できるか。
- 受理: minimum level 等を通り、既定 Grow channel への Log 呼出しが正常復帰した entry。Dispose と producer の競合は扱わず、全 producer の正常復帰後に Dispose する。出力先・通知先が正常である条件に限定する。

### 最低条件

- I1（不変条件）: 正常 entry を JSON 変換失敗が原因で失わない。既存の反例経路は BufferedJsonFormatter が内側の例外を再送出 → AsyncStreamLineMessageWriter の drain 外側 catch → writer.Flush を迂回 → channel 完了で loop 終了 → stream Dispose。調査境界は factory、formatter、通知、既存 tail fixture、ZLogger 2.5.10 の writer。別 provider の内部、Player / AOT は未確認。
- I2（不変条件）: 失敗 entry 由来の JSON 断片・改行を通常出力へ渡さない。既存の staging は断片を止めるが、例外を単に握り潰すと provider が改行を追加する。WithLineBreak は worker の開始時に一度読まれるため、entry ごとの true/false 切替では塞げない。
- I3（不変条件）: 失敗通知が正常行の drain を妨げず、既存の秘密非露出と I/O 分類を保つ。独立通知自身の I/O 失敗は既存通り到達保証外。外側 writer へのコピー例外を JSON 変換失敗として吸収してはならない。
- S1（成立条件）: 正常な通知先で、失敗した entry ごと・JSON provider ごとに既存形式の通知を一度残し、後続の正常 entry も既存 API だけで出力する。

### 受け入れ条件

1. AC1（I1/I2/S1）: A/B を異なる二 worker の getter でそれぞれ同時待機させ、Log(B) 正常復帰後に A を解放し、B 同時待機中の realtime write が 0、両 provider の A コピー長が 65533 bytes 以下という既存条件を確保する。B の変換失敗後に Dispose を完了し、provider ごとの全出力が、fixture の Id=A / Probe.Value=17 の JSON object 1 件とその環境の改行だけであることを assert する。0 bytes、断片、追加空行は失敗。対照 run と失敗 run の timestamp の byte 一致は求めない。
2. AC2（I1/I2/S1）: A → 失敗 B → 正常 C、および失敗 B だけの終了を検証する。前者は A/C のみ、後者は通常出力 0 bytes。両方の通知を provider ごとに正確に数える。失敗 run と対照 run は別 directory。通知 file を通常ログへ混ぜない。
3. AC3（I2/I3/S1）: formatter 単体で、70000 bytes を内部 staging 済みで例外を出す inner、1.2 MB の成功・失敗、連続失敗と次成功、通知 callback の例外を検証する。失敗時に外側 writer へ GetSpan/GetMemory/Advance を一切呼ばず、通知一度、次 entry へ断片を持ち越さず、保持容量は既存 1 MiB 目安に戻る。成功は内容と Environment.NewLine がちょうど一つ。
4. AC4（I3）: 外側 writer の GetSpan または Advance 失敗は再送出し、変換失敗 callback を呼ばない。既存 RealtimeIoFailure テストで provider-write-error / IOException の通知を維持。外側 I/O 故障時の A 保持は保証しない。
5. AC5（I1/I2/S1）: 既存の Vector3・JSON field・巨大正常行・通知 rotation・複数 producer・rolling rotation・default MessagePack/telemetry の回帰が通る。public API、asmdef、provider / stream 所有者・Dispose 順を増減しない。

- 分割検討: 非同期失敗経路は高リスクだが、変換失敗の隔離と改行所有を分離すると B の空行が残るため同じスライス。Player/AOT と I/O 回復は独立した問いとして分ける。
- 後続の問いと所有者: Player/AOT の実証は Issue #183 の後続 `LOGGING_JSON_PLAYER_VALIDATION` 候補、I/O 障害下の保持・回復は同 Issue の `LOGGING_IO_RECOVERY` 候補。どちらも本スライスの実装キューにしない。
- GO: AC1〜5 と判定必須検証が揃うこと。観測のみで GO にした #185 と異なり、A 不在は NO-GO。CONDITIONAL ACCEPT は設けない。
- 停止規則: 最低条件達成で終了。必要な所有者・公開 API・asmdef・配置変更、DLL 経路の不一致、同期条件不成立は A へ戻す。未知を無断で除外しない。
- A3 後の例外承認・決定記録: なし（未凍結）。
- 実装制約: Game → Framework、公開 ILogger<T>、Unity .cs の #nullable enable、record 禁止、テストの Task.Delay / Thread.Sleep 禁止を守る。Unity テストは C が標準 run-tests.ps1 から行い、B は実行しない。Unity 偽 null、path 予算を維持。ZLogger DLL と MessagePack formatter は変更しない。

### 今回の原因照合（静的）

LFS から取得した同梱 netstandard2.1 ZLogger.dll の SHA-256 は上記 #185 の値と一致した。dnfile / dncil で対象 DLL の IL を読み、AsyncStreamLineMessageWriter の `<WriteLoop>d__12.MoveNext` では WithLineBreak の読取りが初期化時の一度だけ（method body offset 0x49）、FormatUtf8 が 0x83、通常 drain 後の Flush が 0xb1、例外 handler は InternalErrorLogger.Invoke（0xcb）の後に WaitToReadAsync（0xe7）へ戻ることを確認した。`<DisposeAsync>d__13.MoveNext` は channel Complete、writeLoop 待機、stream Dispose であり、writer の最終 Flush 呼出しを持たない。StreamBufferWriter.Flush は written が非0の時だけ stream.Write/Flush する。したがって B の例外で当該 drain の Flush が飛び、channel 完了かつ後続なしで終了すると、未 flush の A は stream Dispose では救われない。これは静的な原因照合で、修正後の実 Unity 成功証拠ではない。上流 2.5.10 tag の同名ソースも照合した。

## 3. 責務マップ

| 対象 | 責務・変更理由 | 所有・寿命・依存・公開境界・テスト | 現在 → 予想 |
|---|---|---|---|
| Foundation/Logging/BufferedJsonFormatter.cs | entry の JSON と改行の確定、変換失敗の隔離 | provider ごとに生成、worker 専有。IBufferWriter / IZLoggerFormatter / 内部 Action<Exception> のみ。internal、Unity I/O なし。単体境界 | 88 → 約120行 |
| Foundation/Logging/AppLoggerFactory.cs | provider と既存通知 callback の配線 | factory が provider と LoggerFailureFile を所有、provider drain 後に通知を閉じる。公開 API 不変。統合テスト境界 | 236 → 約240行 |
| Tests/Foundation/BufferedJsonFormatterTests.cs | entry 確定境界の恒久回帰 | テスト所有の fake formatter / counting writer / callback。既存 Foundation test asmdef 内 | 78 → 約170行 |
| Tests/Foundation/AppLoggerTailFailureTests.cs | 実 factory の同一 drain 末尾失敗回帰 | test 所有の gate / stream / 一時 directory。既存の raw 保全と watchdog を維持 | 343 → 約420行 |
| Tests/Foundation/AppLoggerFactoryJsonTests.cs | 失敗のみ・後続成功と既存 wire 回帰 | 実 factory、外側 I/O を含む既存 fixture | 240 → 約270行 |

namespace、フォルダ、asmdef は既存のまま。新しい汎用 helper や provider を作らない。formatter の JSON/改行/失敗隔離は同じ entry 確定責務のため分割しない。単体テストの50%以上増加は独立した責務の混入ではなく同じ境界の失敗経路追加。tail fixture は500行を超える見積りなら A へ戻して test 支援の配置を再判断する。LoggerFailureFile（95行）と MessagePack formatter は変更対象外。§12 G5 の現況更新は検証後の D harvest とする。

## 4. 実装計画

1. 原因照合: 同梱 ZLogger DLL の SHA-256 が #185 の対象 `149f3ae932cbd19a24f0597ce9ff8688e0fbf8070aab2d892c241e92defda5af` と一致すること、writer の例外後に Flush がなく Dispose で最終 Flush しないことを確認する。upstream tag のみを実行 DLL と同一視しない。
2. formatter の WithLineBreak を常に false にする。成功 entry の JSON と UTF-8 Environment.NewLine を内部 buffer へ組み立て、まとめて外側へコピー・Advance する。provider の改行追加に頼らない。対象 inner は factory の SystemTextJsonZLoggerFormatter に限定する。
3. 内側 FormatLogEntry の呼出しだけを catch(Exception) の範囲とする。失敗は既存通知 callback を一度呼び、callback 例外は隔離して return。外側 writer への GetSpan/コピー/Advance は catch の外とし、失敗を provider の既存 InternalErrorLogger へ届かせる。buffer reset は全経路の finally。通知へ元ログや例外 Message / StackTrace を追加しない。
4. factory は provider ごとの同じ独立通知経路を formatter に Action<Exception> で渡す。InternalErrorLogger は外側 I/O 等のため維持する。JSON 変換失敗を worker へ再送出しないので二重通知しない。
5. AC の単体・統合テストを恒久 fixture として更新。旧観測テストを「条件が成立すれば結果は何でも成功」のままにせず、AC1 の正常 A 保持を assert する。raw 出力保全は失敗時も残す。

検討した代替: vendor 更新/fork は変更波及が大きい。独自 processor はキュー・寿命を複製する。entry ごと Flush は writer 抽象を越え、I/O 方針も変える。単なる catch-return は余分な改行を生む。今回は既存 formatter 内の失敗境界と成功行確定を選ぶ。

## 5. テストとレビュー計画

- 起点 filter: 下記3クラスを Unity の `;` 区切りで渡す。
- C の限定実行: `pwsh tools/run-tests.ps1 -Filter 'OneStarMaker.Tests.Foundation.BufferedJsonFormatterTests;OneStarMaker.Tests.Foundation.AppLoggerTailFailureTests;OneStarMaker.Tests.Foundation.AppLoggerFactoryJsonTests' -UnityExe <ProjectVersion.txtに一致するEditor実行ファイル> -OutputRoot <git外の実行別出力先>`。
- 判定必須: 上記、LoggerFailureFileTests、MessagePackZLoggerFormatterTests と最終全 EditMode。全回帰は `pwsh tools/run-tests.ps1 -WithGraphics -UnityExe <同じEditor> -OutputRoot <git外の出力先>`。起動前に対象 project・既存 Editor・lock を確認する。
- 全 EditMode 適用除外: なし。#185 の VFX Inconclusive は今回は免除にならない。graphics を用意できなければ未実行・未判定として担当へ返す。
- 検証経路: C が Unity 6000.6.0f1 で既存実 factory fixture を実行する。初期状態は専用の空 directory、正常 stream と通知先。両 worker の gate、各 A の容量、B 投入正常復帰、write 0、Dispose 完了を先に成立判定し、その後全 bytes と通知を照合。producer / disposer / watchdog の残存を成功にしない。テスト assembly と ZLogger/Microsoft.Logging DLL の実行 path / hash、checkout head、XML、runner log、raw bytes / notice を同じ固定 bundle に保全する。
- 未知の経路: 変更後の成功時改行と隔離後 drain の実 Unity 初回確認は未確認。初回確認は Phase C（Unity テストは A/B の責任外）。既存 #185 の同期成立実績を再実行成功と読み替えない。不成立なら原因を観測して A へ戻し、別 processor を即追加しない。
- 動作確認: テスト・Build 以外の実操作条件は対象なし。C のテストは省略しない。
- 人間の目視判断が必要な条件: なし。
- 高コスト証拠: なし。Player Build / 実 storage 操作を製品の受け入れ条件に含めない。
- 判定 C の照合 script: なし。NUnit の恒久 fixture が値・行数・通知数を assert し、C が raw bytes と XML / runner 結果をレビューする。別の baseline / key 一覧を Git に追加しない。
- 機械検査: `pwsh tools/docs-audit.ps1`、`pwsh tools/contract-audit.ps1 -BaseRef <implementation base>`、`git diff --check`。
- 保存・引渡し: 対応済み Windows 環境の A 担当が通常 task 開始後に A snapshot を stage / 別 session fetch・閲覧確認する。C の結果と所見を分け、C′ は同じ base/head と完全 diff、生証拠、凍結仕様だけを blind bundle で受け取る。Cloud 配送を確認済みにせず、原証拠を Git / CI artifact 等へ迂回して配布しない。
- A0/A1 主担当: Codex / GPT-6 系 / OpenAI。
- C′ 予約: 今回 A に関与しない系列・ベンダーを優先する。未割当。B/C と異なるモデル・新 session・blind bundle は必須。利用できなければ監査未実施として返す。

### 5.1 A2 の独立レビューと A3 の採否

- 目的レビュー: 目的・利用者の原文だけを別 session へ渡した。正常 A 保持、B 断片の排除、通知が drain を阻害しないこと、利用者の追加 Flush / 待機 / 再送 / API を要求しないことを要求。再試行・永続キュー・I/O 回復は不要との所見。主担当の提案はすべてを AC1〜5 と対象外に反映。入力 sha256: `b129637158ae23eb3dec9b119e5340cb83aa781b403cef7517a386b60998e770`（UTF-8/LF、目的本文 + 改行 + 利用者本文、末尾改行なし）。担当は新規 session、起動モデル指定 `gpt-6.1-sol` / OpenAI（担当の自己申告は GPT-6 系）。
- 構造レビュー: 未実施。固定 A2 入力版を使う。snapshot 取得なしのレビューは assist。
- 高リスクの代替構成レビュー: 初稿を見ていない新規 session（起動指定 `gpt-6-sol` / OpenAI）へ、目的・利用者・現況・問い・制約の A0 原入力だけを渡した。entry staging 内の失敗隔離と drain 継続を第一候補、確定済み正常行を明示 flush する案を代替とした。通知を provider 外に保ち、公開 API・所有順変更が必要なら停止する所見。主担当は第一候補を採用提案、代替は writer 抽象を越えるため不採用提案。正式採否は A3。
- A3: 未実施。未決事項は正式な snapshot 引渡し、A2 指摘の採否、owner による境界凍結。人間の採否を推測しない。

## 6. Phase B 実装結果

未着手。製品コード・テストコードの変更なし。implementation head は未固定。result snapshot 未生成。

### 動作確認

未実施。現計画ではテスト以外の実操作は対象なし。B 完了時に確定する。

## 7. Phase C

未着手

## 8. Phase C′

未着手

## 9. Phase D

未着手。保存・取得確認、C/C′ 突合、owner のマージ判断、G5 / Issue #183 への harvest、HANDOFF 削除・task 終了・後始末は行っていない。
