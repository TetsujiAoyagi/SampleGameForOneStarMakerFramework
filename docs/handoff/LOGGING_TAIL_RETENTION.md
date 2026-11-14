# JSON 末尾変換失敗時の正常行保持

## 0. メタデータ

- type: `slice`
- status: `B`（revision 1 の実装・テストコード整備完了、ローカル発見 C 待ち。コンパイル未確認、Unity テスト・Build 未実行）
- branch: `codex/logging-tail-retention-design`
- implementation base commit: `41099fb7a2c7e191d940614024031898214eed28`
- implementation head commit: `0f05175fd188c366f44322c8bd7c0d2f94a42be3`
- risk: `high`（非同期 drain の失敗経路・Dispose 境界を変える）
- owner: repository owner / Phase A 主担当
- created: 2026-11-14
- expires: 2026-11-30 または置換 revision
- harvest to: Architecture §12 G5、Issue #183
- task: `logging-tail-retention`。Windows の既存 Workflow で start / status 確認済み、active / version 1 / deliveryPending 0。Phase B の出口では task 全体を終了しない。
- Phase A snapshot の取得案内・状態: A3 凍結 commit の本文を既存 Storage にローカル固定済み（未昇格）。本 B 出口チャットの取得案内を C 担当へ渡す。送信側の fetch・hash 検証・本文閲覧済みだが、取得側の別 session の fetch・閲覧確認は未完了。Cloud でのレビューは assist のまま、正式 A2 完了へ遡及変更しない。
- Phase B result snapshot の取得案内・状態: 固定 implementation head の完全 diff、製品・恒久 fixture、静的 DLL 照合、機械検査、未確認事項を既存 Storage にローカル固定済み（未昇格）。本 B 出口チャットの同じ取得案内をローカル C 担当へ渡す。送信側 fetch・閲覧済み、取得側の別 session の確認は未完了。Unity 結果を含まず、GO / C′ 用の最終証拠ではない。
- evidence bundle の取得案内・状態: 未生成。C 担当から C′ / D 担当へ。
- C′ blind bundle の取得案内・状態: 未生成。所見と分離して C′ 担当へ。
- 引継ぎ票: 次はローカル Windows PC の発見 C。別途渡すこの票を含む引継ぎ commit SHA と clean な HEAD、上記 implementation head（HEAD の祖先、そこからの差分は HANDOFF のみ）、PR 状態ブロックを照合する。読む範囲は §0〜§8 と §5.1 の凍結採否・制約。取得案内は本 B 出口チャットのリンク先（worktree 外に保全）で、A / B snapshot を別 session で fetch・hash 検証・閲覧して引渡し確認を完了する。必要権限は通常 repo 読取・既存 Workflow / Storage・Unity Editor と標準 run-tests.ps1。起動前に対象 project・既存 Editor・残存 lock を確認し、初期パッケージ導入とコンパイルを完了して §5 の起点 filter から検証する。同期条件不成立・DLL 不一致・責務や API 等の設計変更が必要なら A へ戻す。B は C の実行・判定、C′ 起動を代行しない。次担当は同一 PC を想定し、実装・記録 commit は未 push。別 host を割り当てる場合は配布と到達性の成立を別途確認する。Cloud assist と正式 A2 未完了の記録を維持する。

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

1. AC1（I1/I2/S1）: A/B を異なる二 worker の getter でそれぞれ同時待機させ、Log(B) 正常復帰後に A を解放し、B 同時待機中の realtime の byte[] / Span 両 Write 回数が 0、両 provider の対照 run の A コピー長（JSON 部分、改行を除く）がそれぞれ `65533 - 1024` bytes 以下という既存条件を確保する。1024 bytes は timestamp の幅変動に備えた余裕として残し、rolling の未 flush を直接計測したとは扱わない。B の変換失敗後に Dispose を完了し、provider ごとの全出力が、fixture の Id=A / Probe.Value=17 の JSON object 1 件とその環境の改行だけであることを assert する。0 bytes、断片、追加空行は失敗。対照 run と失敗 run の timestamp の byte 一致は求めない。失敗 run の通知には下記 N1 を必須とし、通常出力の A が完全でも、通知欠落・重複・形式違反・Message / StackTrace 混入は不合格。成功 A だけの対照 run は通知全 bytes が0 bytes（通知行0行）であることを必須とし、N1 を適用しない。
2. AC2（I1/I2/S1）: A → 失敗 B → 正常 C、および失敗 B だけの終了を検証する。前者は provider ごとの生 bytes 全体が A の JSON object + Environment.NewLine + C の JSON object + Environment.NewLine だけであり、間・先頭・末尾の空行、B の断片、余剰 bytes を不合格とする。後者は通常出力 0 bytes。既存 ParseLines は空行を捨てるので、この検査には使わず、空行除去・Trim 等の正規化前の bytes で行数と行境界を検査する。各 JSON object の内容はパースして fixture 値を検査するが、対照 run や provider 間の timestamp の一致は求めない。通知の N1 は、A → 失敗 B → 正常 C の失敗 run と、失敗 B だけの失敗 run のそれぞれに適用し、各 run で provider ごとに正確に1行とする。成功 entry だけの対照 run には N1 を適用せず、通知全 bytes が0 bytes（通知行0行）であることを必須とする。失敗 run と対照 run は別 directory。通知 file を通常ログへ混ぜない。
3. AC3（I2/I3/S1）: formatter 単体で、70000 bytes を内部 staging 済みで例外を出す inner、1.2 MB の成功・失敗、連続失敗と次成功、通知 callback の例外を検証する。失敗時に外側 writer へ GetSpan/GetMemory/Advance を一切呼ばず、通知一度、次 entry へ断片を持ち越さず、保持容量は既存 1 MiB 目安に戻る。成功は内容と Environment.NewLine がちょうど一つ。
4. AC4（I3）: 外側 writer の GetSpan または Advance 失敗は再送出し、変換失敗 callback を呼ばない。既存 RealtimeIoFailure テストで provider-write-error / IOException の通知を維持。外側 I/O 故障時の A 保持は保証しない。
5. AC5（I1/I2/S1）: 既存の Vector3・JSON field・巨大正常行・通知 rotation・複数 producer・rolling rotation・default MessagePack/telemetry の回帰が通る。public API、asmdef、provider / stream 所有者・Dispose 順を増減しない。

N1（AC1/AC2 共通、I3/S1）: 各失敗 run の Dispose 後の通知全 bytes を通常ログと分けて検査する。rolling-json / realtime-json がそれぞれ既存形式の1行、合計2行だけで、各行は UTC timestamp（既存 O 形式）、固定 provider 名、既存 `provider-write-error`、既存安全化規則に従う例外型名の4フィールドと LF 終端だけであること。通知先が正常な fixture では0行・重複・余剰フィールド・余分な行・終端不備を不合格とする。AC1 と AC2 のいずれも失敗 fixture の例外 Message を `probe failure` に固定し、通知全 bytes にその Message 文字列および StackTrace を含まないことを assert する。別の Message の fixture を使って `probe failure` の不在だけを見る検査は受け入れない。例外 ToString() 全文・元ログ値を通知へ含める実装は件数が合っていても不合格。通知の新しい category は追加しない。

- 分割検討: 非同期失敗経路は高リスクだが、変換失敗の隔離と改行所有を分離すると B の空行が残るため同じスライス。Player/AOT と I/O 回復は独立した問いとして分ける。
- 後続の問いと所有者: Player/AOT の実証は Issue #183 の後続 `LOGGING_JSON_PLAYER_VALIDATION` 候補、I/O 障害下の保持・回復は同 Issue の `LOGGING_IO_RECOVERY` 候補。どちらも本スライスの実装キューにしない。
- GO: AC1〜5 と判定必須検証が揃うこと。観測のみで GO にした #185 と異なり、A 不在は NO-GO。CONDITIONAL ACCEPT は設けない。
- 停止規則: 最低条件達成で終了。必要な所有者・公開 API・asmdef・配置変更、DLL 経路の不一致、同期条件不成立は A へ戻す。未知を無断で除外しない。
- A3 後の例外承認・決定記録: なし。凍結時の owner 指示と保存・レビューの制約は §5.1 に記録。
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
2. formatter の WithLineBreak を常に false にし、対象 inner は factory の SystemTextJsonZLoggerFormatter に限定する。provider に改行を追加させない。
3. 処理順は「内側 FormatLogEntry の呼出しだけを catch(Exception) で囲む → 失敗時は既存通知 callback を一度呼び、callback の例外を隔離して return → 成功時だけ UTF-8 Environment.NewLine を内部 buffer に追加 → 内側の catch の外で外側 writer の GetSpan・全体コピー・Advance を各1回だけ行う → この全体を囲む finally で Reset」とする。失敗 return・成功・外側 writer 例外のいずれも最後に Reset を通る。コピー前に Reset せず、JSON と改行を別の外側 Advance に分けない。外側 writer の例外は吸収せず provider の既存 InternalErrorLogger へ届かせる。通知へ元ログや例外 Message / StackTrace を追加しない。
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

- 目的レビュー: 目的・利用者の原文だけを別 session へ渡した。正常 A 保持、B 断片の排除、通知が drain を阻害しないこと、利用者の追加 Flush / 待機 / 再送 / API を要求しないことを要求。再試行・永続キュー・I/O 回復は不要との所見。指摘はすべてを AC1〜5 と対象外に反映し、A3 で採用。入力 sha256: `b129637158ae23eb3dec9b119e5340cb83aa781b403cef7517a386b60998e770`（UTF-8/LF、目的本文 + 改行 + 利用者本文、末尾改行なし）。担当は新規 session、起動モデル指定 `gpt-6.1-sol` / OpenAI（担当の自己申告は GPT-6 系）。
- 構造レビュー（assist）: 新規 session、起動モデル指定 `gpt-6.1-terra` / OpenAI（自己申告 GPT-6 系）。入力は A2 commit `cf013d0bed2dfb42d11750b6332082f7d3e49517` の本文 sha256 `7a537ae7ef186684d7547bf6350dbc761f6ff57b05b3df3731fc91452641eaa1`。git show で全文と hash を確認し、implementation base の formatter / factory / 通知 / 関連テストを静的照合。I1 の drain 中断、I2 の余分な改行、I3 の通知例外・I/O 誤分類の反例順序を挙げ、計画の捕捉境界と検証が対応することを確認。配置・責務・依存・所有・寿命に修正必須の矛盾なしとの所見。Player/AOT・I/O 回復・producer/Dispose 競合は対象外のまま。DLL IL の独立再照合・Unity は未実施。snapshot 未保存のため正式 A2 に数えず、当時の owner の A3 採否は未実施で、最終の採否は本節末尾に記録。
- 高リスクの代替構成レビュー: 初稿を見ていない新規 session（起動指定 `gpt-6-sol` / OpenAI）へ、目的・利用者・現況・問い・制約の A0 原入力だけを渡した。entry staging 内の失敗隔離と drain 継続を第一候補、確定済み正常行を明示 flush する案を代替とした。通知を provider 外に保ち、公開 API・所有順変更が必要なら停止する所見。主担当は第一候補を採用し、代替は writer 抽象を越えるため不採用。A3 でその採否を確定した。
- 外部レビュー（assist）: PR #192 の [外部所見](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/192#issuecomment-6877207631) と [要約](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/192#issuecomment-6877098384) を確認。いずれも同じ A2 入力 commit / 本文内容 hash（上記）に対する所見で、内容が重複し、担当間の独立性は確認できないため1件に数える。cursor[bot]、初回はモデル系列未確認だったが、下記の再レビューで Grok 4.7 / xAI と自己申告された。DLL の制御フローを逆コンパイルで照合し方針妥当、IL offset 自体は未照合、正式 A2 / A3 ではないとの自己申告。
- 外部指摘の採用（owner が本節末尾で A3 凍結を指示）: R1 は §4.3 に内側 catch → 失敗通知 return / 成功改行追加 → 外側1回確定 → 全体 finally Reset の順を明記。誤った Reset 順と別 Advance を防ぐため採用。R2 は AC2 を正規化前の生 bytes による行境界検査とし、中間空行を隠す ParseLines を禁止。I2 の抜けを防ぐため採用。R3 は AC1/AC2 共通 N1 に provider 別の1行・形式・秘密非露出と不合格例を追加。I3/S1 の通知欠落と機微情報混入を検出するため採用。容量注記は AC1 に既存の `65533 - 1024` と両 Write 回数0を維持。現在の問い・責務配置・目的・利用者・対象外を変えず、不採用・後続への移送追加はなし。
- 外部再レビュー（assist）: [再レビュー所見](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/192#issuecomment-6877312042) とその要約を確認。入力 commit `4d2c51239f6fb7accd311b51df2221992616f715`、本文 sha256 `bede3b0bc248ef5ab78eb18039bdcf88ca83cf65f971e112ebfb5d5f127d5048`。担当は前回と同じ系列（自己申告 Grok 4.7 / xAI）。R1〜R3 は文面上解消し、追加指摘は AC2 の通知条件の対象の曖昧さと fixture Message の検査対象。主担当が修正案として AC1/AC2 の成功対照を通知0行、AC2 の二つの失敗 run だけを N1 と明記し、AC1/AC2 の失敗 Message を `probe failure` に固定した。新しい機構・責務・対象外の変更はなし。レビューそのものを正式 A2 / A3 と扱わず、owner の最終採否は本節末尾に記録。
- C′ 独立性の更新: この外部担当（自己申告 Grok 4.7 / xAI）は Phase A 関与済みのため C′ の予約から除外する。他の A 関与済み担当も踏まえ、未関与の系列・ベンダーを優先して選定する。未確認の系列を未関与と推測しない。
- A2 後の改訂と公開: owner が 2026-11-14 に「Pushして、またレビューしてもらうので」と明示したため、上記の修正案と補助レビュー記録を再レビュー用の追加 commit として公開する。通常の A2 途中版を commit しない規則に対する今回の公開指示を優先し、最初の A2 入力版は書き換えない。この票を含む commit が再レビュー入力であり、本文の内容 hash は取得時に照合する。正式な snapshot の保存・引渡しや A3 凍結を代替しない。
- 設計文書の機械検査: docs-audit は errors 0（既存 program の警告1）、contract-audit は errors 0 / warnings 0、git diff --check 成功。Unity テストは A の対象外で未実行。
- 最終の外部設計レビュー（assist）: [追加修正必須指摘なしの所見](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/192#issuecomment-6877502715)。対象 commit `406c912e0c7aebbf3991944849a68131d10cc9f6`、本文 sha256 `f036604dd43d2378faefb18d49b82457e4ab258c7f167c7cdea65659c291f09f`。HANDOFF 全節、implementation base の製品コード・既存 fixture、upstream writer の静的照合。実装や Unity 実測、正式 snapshot の受渡しを認定しないとの留保を維持。担当の正確なモデル系列は未確認で、Phase A 関与済みとして C′ 候補から除外。
- A3 統合・採否: 2026-11-14、owner が「新しいレビューで指摘無し、という事で凍結しましょう」「実装自体はローカルPCでやるべき」と明示。主担当 Codex / GPT-6 系 / OpenAI が本 HANDOFF の revision 1 を仕様凍結した。目的レビューの保証、entry 内隔離案、R1〜R3、容量余裕、対照/失敗 run の通知区別と Message 固定を採用。明示 flush・独自 processor・vendor 更新案は §4 の理由で不採用。スコープ追加・保留指摘はなし。公開レビューの assist と snapshot 保存未完了はそのまま記録し、正式 A2 完了と偽らず、owner の本指示でローカル Phase B へ渡す。
- 凍結の限界: 仕様の凍結であり製品修正・Unity 成功・Evidence 保存完了ではない。残る運用上の未完了は A snapshot の保存・取得・引渡しで、対応済みローカル環境の担当へ渡す。§2 の実装境界と §5 の C 必須テストは緩めない。

## 6. Phase B 実装結果

- 担当: Codex / GPT-6 系 / OpenAI、ローカル Windows PC（2026-11-15 JST）。入力 f9a78fb1e60f8cf43c5cff76371181381b66a220 と clean HEAD、§0、PR #192 を照合。別作業の未追跡 HANDOFF と既存 branch を保護し、clean な既存専用 checkout を指定 branch へ切替えた。
- 製品: BufferedJsonFormatter は inner の呼出しだけを catch、失敗は既存 callback 一度と return。callback 例外を隔離し、成功時だけ JSON と UTF-8 Environment.NewLine をまとめて外側へ GetSpan / Copy / Advance 各1回で確定。WithLineBreak は常に false、全経路 finally Reset。外側 writer 例外は再送出。factory は provider ごとに同じ独立通知 callback を wrapper と InternalErrorLogger に配線。公開 API・依存・asmdef・所有順・MessagePack・LoggerFailureFile は変更なし。
- AC1: 既存 gate・容量余裕・両 Write 回数0・producer / disposer / watchdog 完了と raw 保全を維持し、両 provider の正常 A 一件だけの完全出力を必須化。成功対照は通知0 bytes。
- AC2 / N1: 失敗のみ、A→失敗B→C と成功対照を別 directory の恒久 fixture に追加。正規化前の全 bytes を厳密 UTF-8 / 環境改行で区切り、余剰行・空行・断片・終端不備を拒否。通知は LF 終端・UTC O 形式・4 field・provider 各1件を検査し、固定 probe failure と stack 情報の不在を検査。Unity の反射 getter が通知する外側 TargetInvocationException 型名を期待し、Message / inner は出力しない。AC2 の raw も assertion 失敗時に保全。
- AC3 / AC4: 70000 bytes staging 後の連続失敗、1.2 MB 成功 / 失敗、通知 callback 例外、次 entry の断片不在と容量復帰、外側 GetSpan / Advance 失敗の再送出・通知0回を単体テスト化。失敗時の外側 GetSpan / GetMemory / Advance 呼出し0を数える。
- AC5: 既存 Vector3 / JSON field / 巨大正常行 / 並行 producer / 通知・rolling rotation / default MessagePack・telemetry / realtime I/O 分類 fixture を維持。実行結果は未取得。
- 配置と規模: 計画の製品2 file・テスト3 file に限定。tail fixture は400行で500行停止条件内。単体 fixture の増加は同じ entry 確定境界の失敗検証。厳密 bytes / 通知 assertion は既存 tail fixture 内の test 支援として共有し、新しい helper 型や依存を追加していない。設計の再開を要する変更なし。
- 原因照合: LFS 実体の同梱 ZLogger.dll の SHA-256 が §4.1 と一致。dnfile / dncil で writer の IL を読取り、WithLineBreak 一度読取、変換例外による drain Flush 迂回、DisposeAsync に writer 最終 Flush が無いことを確認。静的照合であり修正成功の実測ではない。
- 機械検査: contract-audit -BaseRef は §0 の implementation base を使用、errors 0 / warnings 0。docs-audit errors 0 / warnings 1（既存 program の harvest 警告）。git diff --check 成功。原出力は B result snapshot に収録。
- コンパイル確認: Unity 6000.6.0f1 / CLI 1.0.0-beta.8 で専用 project を開いたが、初期 Package Manager 導入画面（64/81）と AssetDatabase Initial Refresh Start 以後の compile 完了を確認できず、未確認。Pipeline 到達不可で正常 close も不可。Scene 等の編集前で未保存変更が無いことを確認し、今回起動した Editor だけを CLI --force で停止・終了確認。対象の lock file は残存するが非保持と確認。他作業の Editor は操作していない。既知 native 終了 stall の追加調査ではない。
- 未実行・未判定: Unity 全テスト、Build、AC1〜5 / N1 の実 Unity 成功、C / C′。C が初期 import / コンパイルを確認し、§5 の限定 filter・関連回帰・graphics 全 EditMode を実行する。製品受け入れ条件を免除しない。
- 保存・引渡し: §0 の A / B snapshot と B 出口の取得案内を参照。送信側検証と取得側の別 session 確認を区別し、後者は C 担当へ未完了として引き渡す。従前の assist を正式 A2 へ読み替えない。

### 動作確認

対象なし。凍結計画にテスト・Build 以外の実操作条件は無い。Unity のテスト検証を代替・省略しない。

## 7. Phase C

未着手

## 8. Phase C′

未着手

## 9. Phase D

未着手。保存・取得確認、C/C′ 突合、owner のマージ判断、G5 / Issue #183 への harvest、HANDOFF 削除・task 終了・後始末は行っていない。
