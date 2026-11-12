# Unity ログ値型と出力失敗の検出 — A3 候補（未凍結）

## 0. 状態

- type: slice / status: Phase A。依頼者が前候補の容量拒否・通知停止を否認したため再設計。改訂版の独立 A2 完了。A3 凍結・Phase B 開始は未承認。
- branch: codex/logging-unity-value-serialization
- implementation base: f8ffd023 （開始時の origin/develop） / implementation head: 未固定
- risk: high（非同期ログ出力と失敗通知、寿命に影響）
- owner: 主担当 / created: 2026-11-12 / expires: 凍結または置換 revision
- harvest: `unity/Assets/Docs/Architecture/12-telemetry.md` の G5 とログ利用上の制約（Editor の実 factory で確認した範囲と Player 未検証を区別）、`unity/Assets/Docs/Architecture/30-accessibility-identity.md` の Foundation における Vector3 の記述
- Phase A snapshot の取得案内・状態: 前候補の原記録はローカル固定・取得確認済み。今回の改訂版もローカル固定し、独立2担当が同じ内容を取得・閲覧済み。通知 file の原出力を補完した最終 package の案内と取得確認票は、主担当管理の Git 外の引渡し票に置く。旧 A2 の合格を今回の設計に流用しない。A3 凍結 snapshot は未生成。
- 引継ぎ票: 次は主担当と人間による改訂版の A3 採否・凍結判断。通常ログへの新しい行サイズ制限と満杯後の通知停止は撤回した。通常ログの既存ローテーションを保ち、障害通知もローテーションして継続する候補を確認する。有限 Vector3 / Editor 限定は承認済み。A3 合意前に Phase B へ進まない。

## 1. 目的と対象

- 目的: 有限の Vector3 を含むログを x/y/z の数値として記録でき、記録できない場合も開発者が失敗を検出できるようにする。
- 利用者: Unity アプリのログを使って問題を調査する開発者。
- 目的文の変更: 元の文言は「Unity の値型を含むログを欠落させず記録でき、記録できない場合も開発者が失敗を検出できるようにする。」。再現された問題を最初のスライスで扱うため範囲を明確化。2026-11-12、依頼者は推奨した有限 Vector3 と Player 判定範囲の案を承認した。変更後の目的レビューは §7 に記録。
- 現況: `AppLoggerFactory` の JSON formatter は `IncludeProperties.All`。文書 G5 に `Vector3` を structured property に含む行が循環参照で欠落した再現と実例がある。ZLogger 同梱 XML は `InternalErrorLogger` が書込み中の例外に呼ばれ既定 null は無視と説明する。非 Unity spike では callback の到達と Dispose 後の観測を確認済みだが、実 factory / Unity の通知配線・thread・終了順は未確認。JSON 設定は rolling file と realtime JSON で共有、既定 realtime MessagePack は別 formatter。telemetry は JSON の structured fields を利用する。
- 対象: 再現済み `Vector3` の有限成分 x/y/z を JSON 数値として保存し、対象 provider の書込み処理で捕捉される変換失敗を可視化する。非有限成分は変換失敗として通知し、値としての保存は後続。`Quaternion` 等の他の Unity 値型の値の保存は保証しない。これらが変換例外として捕捉された場合は通知対象だが、すべての型が必ず失敗するとは仮定しない。一般の任意型や物理媒体の故障時の完全な復旧を保証しない。
- 対象外: DebugStudio protocol/画面変更、formatter 以外のログ基盤の作り直し、ログ呼出側の個別文字列化、全 Unity 型の網羅、ログの配信保証、既存ファイル修復、asmdef 参照追加。
- UC: 開発者は従来どおり `ILogger` にメッセージと値を渡す（開発時の呼出、調査したい値を指定するため必要）。アプリ起動・対象出力は既存配線と既定値による。調査時に既存 rolling log と独立した障害通知先を確認する（実行時の閲覧）。値型ごとの登録・新たな操作は求めない。

## 2. 境界と受け入れ候補

- 問い: 既存ログ配線で `Vector3` の行を正常に記録し、シリアライズできない別の値に起因する provider の失敗を、同じ失敗経路に依存せず検出できるか。
- 不変条件 I1: primitive / telemetry の JSON field の名前・値・種類、既定 MessagePack の wire と telemetry 抑止を変えない。JSON の完成長や writer の容量要求が1 MiBを超えたことを理由に正常 entry を落とす新しい制限は設けない。既存の通常ログの日付・rollingSizeKb によるローテーションと保持方針は維持する。検証境界は AppLoggerFactory、JsonFileTelemetrySink、MessagePackZLoggerFormatter。任意サイズのメモリ確保成功や全外部消費者の互換性まで保証するものではない。
- I2: 通知に同じ ILogger / 対象 provider を再利用せず、通知自身の失敗をアプリへ伝播・再帰させない。通知ファイルの満杯、過去の rotation I/O 失敗、再起動を理由に通知機能を恒久停止しない。利用不能な回の到達は保証できないが、次の callback では再び書込み・ローテーションを試みる。背景 thread と終了境界を検証する。
- I3: **serialization 失敗**では、失敗 entry の bytes を出力先へ渡さず、前後の正常 entry をそれぞれ独立した JSON 行として保つ。64 KiB を超える失敗 entry も対象とする。stream 自体への書込み後の I/O 失敗は巻き戻せず、この条件と区別して通知対象・保証外とする。現行 ZLogger は変換途中に buffer を確定し得るため、converter と callback だけでは I3 を満たせない。実現方式は §3 の候補であり、非 Unity spike では成立を確認、実 factory / Unity では未確認。
- 成立条件 E1: 既存の structured logging 呼出で有限の `Vector3` を含む行が JSON として読み取れ、x/y/z を区別して数値として観測できる。message、level、category 等の既存情報も保持する。非有限値は変換失敗として通知する候補とし、値として記録するかは後続判断。他の Unity 型の保証は後続判断。
- E2: rolling JSON と realtime JSON の provider callback の失敗を provider ごとに識別できる通知として、独立した logging-errors.txt へ渡す。満杯になる前に logging-errors.1.txt へ回し、今回の通知を新しい active に書く。全 callback を serialization 失敗と分類しない。両 provider の失敗は各々通知される。通知先自身の I/O 失敗時の到達は保証しない。MessagePack の配線は変えない。
- E3: rolling と realtime JSON の双方で E1/E2 と I1〜I3 を検証する。既定 MessagePack は既存 wire/telemetry の回帰確認を行う。
- 合格例: 正常 A → 64 KiB超の先頭後に変換失敗 B → 正常 C で、各非空行が JSON 値ちょうど1個、A/C は完全、B の断片は0 bytes。writer の要求が1 MiBを超える正常行と、完成形自体が1 MiBを超える正常行も完全に出る。cold / warm buffer の双方で同じ正常入力が出る。通知は複数回の回転後、満杯状態での再起動後、rotation I/O 障害解消後も新しい行が残る。不合格例: 人為的な1 MiB上限で正常 entry が落ちる、失敗断片と後続行が結合する、通知満杯を理由に以後の通知を捨て続ける。
- 分割判断: 型対応と失敗検出は独立に失敗するが、現在の問いは再現された欠落と検知不能の両方を閉じること。小さく収まる前提で一スライスとし、別通知基盤や多数の型へ広がる場合は A に戻して分割する。後続は残りの Unity 型・非有限数・媒体障害の検討（担当未定）。
- GO: Unity Editor の実 factory テストで不変条件・成立条件と判定必須検証を満たすこと。非 Unity の方式確認は §7 で実施済み。実 factory / converter / Unity の配線は未実装・未確認。Player / IL2CPP / AOT / stripping は今回の対象外で、Editor の結果を Player 成功の証拠にしない。
- 未決: 可変長 buffer と通知ローテーションの改訂案について人間の A3 採否・凍結判断。改訂版の独立 A2 は完了し、結果は §7 に記録済み。前候補の容量拒否・満杯後の停止は依頼者が否認済み。非 Unity の方式確認は §7、実 factory / Vector3 / Unity の確認は凍結後の判定 C とする。

## 3. 責務と実装案

- 新規 file は既存の `Scripts/Foundation/Logging/`、namespace `OneStarMaker.Foundation.Logging`、既存 Foundation asmdef の内部実装に置く。型は `Vector3JsonConverter`（値の変換 policy）、`BufferedJsonFormatter`（formatter と容量 writer を同じ file に置く、entry の確定規則）、`LoggerFailureFile`（障害通知の永続化）を候補名とする。値変換、entry の確定、通知 I/O は独立して変更するため分け、同じ容量アルゴリズム内の writer だけは分割しない。新フォルダ・公開型・asmdef edge は追加しない。converter は Unity の Vector3 と System.Text.Json、buffer は ZLogger / System.Buffers、通知は System の file / lock にだけ依存し、SampleGame / Editor へ依存しない。factory が全体の生成・配線・終了を所有する。buffer の拡張・保持量・reset と通知の容量・失敗・並行性はそれぞれ直接単体テストでき、実 factory テストとは区別する。
- `Foundation/Logging/AppLoggerFactory.cs`（205行）: 既存 provider の配線、寿命は factory、公開面は `ILogger<T>`。JSON 設定と必要な通知 callback を接続。内部 constructor の共通化を含め予定 +40〜70行。
- Foundation/Logging 内の小さな専用 converter（候補、新規）: 状態・I/O を持たず、有限 `Vector3` を x/y/z の数値 object にする。非有限値は変換失敗として通知する候補。formatter の既存 `JsonSerializerOptions` に converter を追加し、他の既定値を置き換えない。既存の null 省略・escaping を保ち、非有限値への対応のために `NumberHandling` を全体で緩めない。ZLogger 2.5.10 の source 上は設定点があるが実配線は未確認。登録と converter は factory と同寿命。公開 API・asmdef 追加なし。+50〜100行見込み。
- Foundation/Logging の entry 単位 buffer（新規候補）: JSON formatter を包み、一時 buffer 内で変換が成功した後だけ完成 bytes を provider の writer へ渡す。変換失敗時は外側へ bytes を渡さず例外を再送出して provider の callback に届ける。成功コピー後と失敗再送出前の双方で一時 buffer を初期化し、次の entry に残さない。同じ配列を再利用してよく、内容の消去ではなく有効長のリセットを必須とする。`WithLineBreak` は内側の JSON formatter と同じ true とし、改行は provider のみが書く。各 provider の formatter と同寿命、Unity API 非依存。+100〜160行見込み。
- **一時 buffer は行の制限ではない:** GetMemory / GetSpan の要求に応じて配列を拡張し、1 MiB超の要求も受ける。Advance の範囲外と算術 overflow は拒否するが、新しい製品上の entry サイズ上限を導入しない。成功・失敗の双方で finally から有効長をリセットする。再利用のため保持する配列は1 MiB以下を目安とし、これを超えた配列は entry 終了時に参照を外す。これはキャッシュ保持量の制御であり、変換中の確保上限でも完成 JSON 長の制限でもない。大きな entry では一時的なメモリ増加がある。旧新配列、serializer 内部、provider buffer、元データ、キューを含む総メモリ上限や、GC の即時回収は主張しない。確保失敗時の通知到達も保証しない。各 provider が formatter / buffer をその寿命中所有し、他 provider と共有しない。通常配列を使い、ArrayPool が実際にはより大きい配列を返す可能性に依存しない。
- Foundation/Logging 内の独立通知（候補）: factory が所有し、log directory から logging-errors.txt と logging-errors.1.txt を一意に導く。通常 JSON の *.log に非 JSON の障害通知を混ぜない。第二の本番設定・通知先 injection を足さない。rolling-json / realtime-json を各 callback に閉じ込め、ZLogger を通さず同期追記する。1行は UTC ISO 8601 時刻、provider 名、固定 provider-write-error、例外型名。Message / StackTrace / Data / inner exception / 元ログ値は保存しない。制御文字と区切り文字を除去し、型名は最大128文字、UTF-8 BOMなし・LF込みで1行最大512 bytes。収まらなければ型名を固定 Exception にする。原因の詳細や元ログの復旧は要求しない。
- 通知のローテーション・寿命: サイズ境界は既存 rollingSizeKb × 1024 bytes（既定1 MiB、long で計算）を使う。次の行を追記すると超える場合、書込み handle を閉じた状態で古い backup を削除、active を backup に移動、新しい active に今回の通知を書く。直近2ファイルを保持し、それより古い通知は世代交代で失われる。通常ログの回転・保持世代は変えない。再起動時も同じ2ファイルを使い、active が満杯なら回して今回の通知を書く。満杯だから今回の通知を捨てる設計にしない。factory 内の1つの lock で両 provider のサイズ確認・回転・追記を直列化する。通知ごとに file を開いて閉じ、共有は読み取りのみ。構築時は file を開かない。open / rotate / write / close 失敗は callback 内で捕捉するが、無効化フラグは立てず次回も再試行する。同じ ILogger、provider の Dispose / flush 待ち、Unity main thread の同期待ちに戻らない。_inner.Dispose() で provider を drain した後、finally で通知オブジェクトを閉じる。媒体故障・部分書込み・別 factory / process の同時操作・通知先の利用不能時の到達や、クラッシュ時の回転の原子性は保証しない。active 移動後に追記できなかった場合も次回は active 作成を試みる。独立通知は +100〜150行見込み。
- `MessagePackZLoggerFormatter` と `JsonFileTelemetrySink`: 変更を予定しない。既存テストを回帰境界として使う。
- Foundation tests: 既存の `InternalsVisibleTo` を利用し、factory 内部コンストラクタの一つの seam で一時 log directory だけを指定する案。通知先は本番と同じ規則でそこから導き、実ファイルを読む。公開コンストラクタをその共通経路へ委譲し、formatter / callback 設定を複製しない。公開 API は変えず、processor 自体は差し替えない。rolling と realtime JSON の実 factory 配線を通す。`Dispose` の完了後に rolling file と realtime の全 bytes、通知を観測し、実時間 sleep を使わない。realtime は一時 file stream を渡し、provider による close 後に file を開き直して bytes を読む。ZLogger は渡された realtime stream を閉じる一方、現 factory コメントは呼び出し側所有と記述しており不一致がある。所有権の実装を変えず、コメントを実挙動に合わせて修正する案。非 Unity の結果を Unity の確認として使わない。factory は配線・寿命に限定し、converter、容量 buffer、通知 I/O をそれぞれ別 file にする。factory の増分は constructor 共通化を含め +40〜70行に改訂し、50%以上の増加が生じても新しい中核ロジックを factory へ混在させない。
- 対応点や通知の所有者が上記で成立しない、公開面・asmdef・寿命が新たに必要、失敗後の JSON 整合を保証できない場合、Phase B で独自設計せず Phase A に戻す。

## 4. 実装順

非 Unity の実 ZLogger provider / wrapper と通知 file の小さな試作で方式を確認した（§7）。実 factory / Vector3 converter / Unity の配線は未確認で、凍結後に実装・コンパイル、判定 C の実 factory テストで確認する。試作の成功を製品実装の合格にしない。凍結後は converter → buffer → 通知 → 共通配線 → 回帰テストの順。本番 Scene / Prefab / Addressables は変更しない。

## 5. 検証とレビュー候補

- 差戻し中の起点: `pwsh tools/run-tests.ps1 -Filter 'OneStarMaker.Tests.Foundation.AppLoggerFactoryJsonTests'`。加えて `MessagePackZLoggerFormatterTests` / `TelemetryLogCorrelationTests` や buffer / 通知の単体テストから修正に関係するものを C が選択し、XML で実行名を確認する。起点の新設 fixture 名はこの名前に固定する。
- 判定 C: `pwsh tools/run-tests.ps1` の空 filter 全 EditMode、同 XML の件数とテスト名確認、`pwsh tools/contract-audit.ps1`。Unity Editor と project を確認し、人間所有 Editor は止めない。Phase B では Unity test / Build を実行しない。
- 有限 Vector3 の正負・小数・ゼロ、非有限 Vector3 の拒否と通知、複数プロパティ、例外、繰り返し出力、強制 serialization failure と後続正常行、通知先 failure、JSON の行単位 parse、telemetry、MessagePack を検証。確定的な同期で flush/終了を待ち、通知とデータを書き終えた時点を判定する。
- 起点 fixture は AppLoggerFactoryJsonTests。容量については約60万 bytesと1 MiB超の正常行、cold / warm の成功、大容量 entry 後の保持配列縮小、64 KiB超の先頭後の変換失敗と正常行への回復を検証する。通知は既存 rollingSizeKb を小さくして複数回回し、上限内の active / backup、最新通知、2 provider の識別と並行アクセス、満杯状態で再構築後の通知、回転失敗を解消した次の通知、drain 中の通知を確認する。backup と同名の directory 等で I/O 失敗を決定的に作り、解消後の再試行を見る。通常ログや factory 構築を通知失敗で止めない。並行性は barrier / signal を使い、時間待機しない。
- 操作・目視: 対象なし。開発者の閲覧先は同じ log directory の `logging-errors.txt` に確定候補を置いた。E2 は判定 C の実 factory テストがこの本番通知 file を開き、全体の bytes を読むことで閉じる。実装前のファイル到達性を成立済みとはせず、初回確認は判定 C。不成立なら凍結した条件を緩めず B へ戻す。依頼者の裁定により Player 固有の AOT/stripping は今回 GO の対象外・未検証とする。Editor の成功は Player 成功の証拠にしない。
- 高コスト証拠: なしを候補。判定 C の照合 script: なし、テスト自身の assert と XML を使用。
- A0/A1 と A3 統合: OpenAI GPT-6（主担当）。A2 の記録と採否はレビュー後追記。C′ の担当系列は凍結時に確定。

## 6. Phase B 以降

未着手。Phase A 凍結なしに実装しない。

## 7. 依頼者の差し戻しと改訂版の方式確認

- 2026-11-12、依頼者は前候補の人為的な1 MiB容量拒否と、障害通知が満杯後に再起動しても止まる動作を否認し、正常ログの成功を保ち、通知を回転して継続する設計を要求した。どちらも主担当が追加した未承認の制約であり、撤回する。撤回した候補の履歴は Git と前候補の Artifact Storage に保持し、現行の指示・合格根拠には使わない。前候補の source・生結果・レビューは保存したまま、改訂版を別 snapshot にする。
- 現行の通常ログは既に日付と rollingSizeKb によるサイズローテーションを行っている。この動作や通常ログの保持方針を変える依頼ではない。今回変更するのは新規の一時 buffer と障害通知の候補設計。
- 改訂版の非 Unity spike は .NET 8 と repo の LFS pointer に一致する ZLogger 2.5.10 netstandard2.1 DLL を使用。通常6 entry の元 provider 対照、同じ正常6 entryと大きな先頭後の循環失敗1 entryを持つ wrapper、約30万文字の cold entry を実行した。元 provider と wrapper はいずれも正常6行、wrapper の失敗通知は循環例外1件だけだった。
- 旧候補で落としていた約60万 bytesの R は、約120万 bytesの容量要求を受けても完全に出力された。完成形が約600万 bytesの O も出力された。大きな B の変換失敗は外側0 bytesで、同じ formatter / buffer から後続正常行が出た。各非空行の JSON parse、Id / message / property の全内容を検査した。entry 終了後の保持配列は全件1 MiB以下で、R/O の大きな配列は参照を外した。保有量の制御が出力の拒否条件になっていないことを確認した。
- 通知試作は1 KiBの回転境界で200件を投入し、複数回回転後の最新通知と2ファイルの上限を確認。満杯状態で再構築した後の新規通知、rotation I/O 失敗解消後の次の通知、2 provider 相当の100件の並行書込み後の継続も確認した。非 Unity の試作は PASS。source / project / 生 JSONL / results.json / console / 通知 file を同じ snapshot に保存する。実 factory / Vector3 / Unity での合格とは扱わない。
- 目的・利用者の原文と承認済みの有限 Vector3 / Editor 限定は変更していない。目的レビューの入力 SHA-256 は e18f0a25273b731d202e0e0de44921fe585e9310deae9a14017050cd93b0ab73。原文だけを見た既存の目的レビューは、型の手動登録不要、正負・小数・ゼロの保存、終了点と独立した失敗検出を要求した。今回もこの条件を維持する。今回の差し戻し指示は A3 凍結の承認ではない。

- 改訂版の独立 A2: 未 commit の本文内容 SHA-256 5e06ca6c7d15be3aea9f3d8a4006d1707159b697ee9ed505931158288afd8495 と source・生結果を同じ snapshot に固定。gpt-6.1-sol と gpt-6-astra が互いの所見を見ずに取得・閲覧し、構造と成功条件の保持・失敗経路を確認した。設計上の blocker はなし。両担当が指摘した通知 active / backup の梱包漏れは採用し、既存の原出力を追加して最終転送で確認する。方式の再実行や仕様の緩和で埋めない。本文から前候補の長い履歴を外し、現在の条件と否認の記録だけを残した。旧 source・生結果・レビューは保存済み。
- 検査: 改訂版の非 Unity spike は PASS、docs-audit はエラー0・既存 program の警告1。Unity の製品コード・アセット・asmdef は変更せず、Unity テスト・Build は未実行。凍結・製品実装の合格は主張しない。
