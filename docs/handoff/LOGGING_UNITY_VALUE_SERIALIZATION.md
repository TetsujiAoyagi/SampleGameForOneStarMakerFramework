# Unity ログ値型と出力失敗の検出 — A3 候補（未凍結）

## 0. 状態

- type: slice / status: Phase A、修正候補の独立 A2 レビュー完了。A3 凍結・Phase B 開始は未承認。
- branch: codex/logging-unity-value-serialization
- implementation base: f8ffd023 （開始時の origin/develop） / implementation head: 未固定
- risk: high（非同期ログ出力と失敗通知、寿命に影響）
- owner: 主担当 / created: 2026-11-12 / expires: 凍結または置換 revision
- harvest: `unity/Assets/Docs/Architecture/12-telemetry.md` の G5 とログ利用上の制約（Editor の実 factory で確認した範囲と Player 未検証を区別）、`unity/Assets/Docs/Architecture/30-accessibility-identity.md` の Foundation における Vector3 の記述
- Phase A snapshot の取得案内・状態: 未 commit の改訂版を Artifact Storage にローカル固定済み（未昇格）。主担当管理の Git 外の Phase A 引渡し票に取得案内を保存。独立 A2 の2担当が同じ入力を fetch・閲覧し、本文内容 hash の一致を確認済み。manifest の自己参照を外した再梱包も構造担当が再取得し、全20 file の同一性と自己参照除去を確認済み。A3 凍結 snapshot は未生成。
- 引継ぎ票: 次は Phase A 主担当と人間による A3 採否・凍結判断。§3 の容量上限（完成長ではない 1 MiB）と通知ファイル（行512 bytes / file 1 MiB、利用不能・満杯時は通知を捨てる）を含む候補を判断する。対象型と Player 範囲の採否は取得済み。B / C / C′ の実装・検証は引き渡さない。A3 合意前に Phase B へ進まず、合意後に凍結 snapshot と凍結 commit を作る。ローカルの保存・取得に特別な権限追加は不要。

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
- 不変条件 I1: 出力に成功する primitive と telemetry の JSON field の名前・値・種類、既定 MessagePack の wire と telemetry 抑止を変えない。新しい JSON の容量拒否は型に関係なく適用されるので、従来出力できた大きな primitive / telemetry entry も落ちて E2 の通知対象となり得る。従来成功した全 entry の通過互換性は主張しない。この境界は §3 の容量上限と合わせて A3 で判断する。検証境界は `AppLoggerFactory`、`JsonFileTelemetrySink`、`MessagePackZLoggerFormatter`。全外部消費者の互換性は未確認であり、この判定の対象にはしない。
- I2: 失敗通知に同じ `ILogger` / 対象 provider を再利用せず、通知の失敗や多重通知で無限再帰・アプリ例外伝播を生じさせない。provider は背景処理であるため thread と終了境界を検証する。
- I3: **serialization 失敗**では、失敗 entry の bytes を出力先へ渡さず、前後の正常 entry をそれぞれ独立した JSON 行として保つ。64 KiB を超える失敗 entry も対象とする。stream 自体への書込み後の I/O 失敗は巻き戻せず、この条件と区別して通知対象・保証外とする。現行 ZLogger は変換途中に buffer を確定し得るため、converter と callback だけでは I3 を満たせない。実現方式は §3 の候補であり、非 Unity spike では成立を確認、実 factory / Unity では未確認。
- 成立条件 E1: 既存の structured logging 呼出で有限の `Vector3` を含む行が JSON として読み取れ、x/y/z を区別して数値として観測できる。message、level、category 等の既存情報も保持する。非有限値は変換失敗として通知する候補とし、値として記録するかは後続判断。他の Unity 型の保証は後続判断。
- E2: rolling JSON と realtime JSON の provider が捕捉した書込み処理の例外を provider ごとに識別できる通知として独立した `logging-errors.log` へ渡す。serialization 以外に stream の flush / I/O 例外も callback に届き得るため、全通知を serialization 失敗と分類しない。同じ entry が両 provider で失敗すれば各々通知され、繰返しの失敗は再通知され得る。通知に元データの復旧は要求せず、通知先自身の利用不能・容量到達時の到達は保証しない。閲覧は既存 log directory 内のテキストファイルで行う。通知の内容・容量・失敗処理は §3 に固定する候補。MessagePack provider の動作は変えない。
- E3: rolling と realtime JSON の双方で E1/E2 と I1〜I3 を検証する。既定 MessagePack は既存 wire/telemetry の回帰確認を行う。
- 合格例: `Vector3(1,2,3)` の x/y/z を数値で持つ正常行、正常 A → 64 KiB 超の変換失敗 B → 正常 C を実 provider に通し、出力全体を byte 単位で改行分割した各非空行が JSON 値ちょうど 1 個で A/C が各 1 行、B の断片がなく、対象 provider の通知を確認できる。別に上限以内の 64 KiB 超の正常 entry が一行で完全に出ること、上限超過 entry は変換結果が正常でも断片を出さず通知されることを確認する。 不合格例: 行だけが消える、通知が同一 logger に再投入される、B の断片が残る、途中断片と後続行が結合する。
- 分割判断: 型対応と失敗検出は独立に失敗するが、現在の問いは再現された欠落と検知不能の両方を閉じること。小さく収まる前提で一スライスとし、別通知基盤や多数の型へ広がる場合は A に戻して分割する。後続は残りの Unity 型・非有限数・媒体障害の検討（担当未定）。
- GO: Unity Editor の EditMode で実 factory を通し、上記不変条件・成立条件と判定必須検証を満たす。NO-GO: いずれか未達。Player / IL2CPP / AOT / stripping は今回 GO の対象外で未検証、Editor の結果を Player での修正の証拠にしない。候補の実現性は未確認であり、成立しない場合は仕様を自動緩和せず A に戻す。
- 未決: 下記の容量上限・通知ファイルの候補について人間の A3 裁定。独立 A2 の結果と採否は §7。非 Unity での wrapper・callback・provider 継続性は §7 の再実行で確認した。実 factory、converter、通知ファイル、Unity Editor 上の挙動は未実装・未確認で、凍結後の判定 C で確認する。Player の AOT / stripping は実機証拠がなく、Editor の結果から成功を推測しない。

## 3. 責務と実装案

- 新規 file は既存の `Scripts/Foundation/Logging/`、namespace `OneStarMaker.Foundation.Logging`、既存 Foundation asmdef の内部実装に置く。型は `Vector3JsonConverter`（値の変換 policy）、`BufferedJsonFormatter`（formatter と容量 writer を同じ file に置く、entry の確定規則）、`LoggerFailureFile`（障害通知の永続化）を候補名とする。値変換、entry の確定、通知 I/O は独立して変更するため分け、同じ容量アルゴリズム内の writer だけは分割しない。新フォルダ・公開型・asmdef edge は追加しない。converter は Unity の Vector3 と System.Text.Json、buffer は ZLogger / System.Buffers、通知は System の file / lock にだけ依存し、SampleGame / Editor へ依存しない。factory が全体の生成・配線・終了を所有する。buffer の上限・reset と通知の容量・失敗・並行性はそれぞれ直接単体テストでき、実 factory テストとは区別する。
- `Foundation/Logging/AppLoggerFactory.cs`（205行）: 既存 provider の配線、寿命は factory、公開面は `ILogger<T>`。JSON 設定と必要な通知 callback を接続。内部 constructor の共通化を含め予定 +40〜70行。
- Foundation/Logging 内の小さな専用 converter（候補、新規）: 状態・I/O を持たず、有限 `Vector3` を x/y/z の数値 object にする。非有限値は変換失敗として通知する候補。formatter の既存 `JsonSerializerOptions` に converter を追加し、他の既定値を置き換えない。既存の null 省略・escaping を保ち、非有限値への対応のために `NumberHandling` を全体で緩めない。ZLogger 2.5.10 の source 上は設定点があるが実配線は未確認。登録と converter は factory と同寿命。公開 API・asmdef 追加なし。+50〜100行見込み。
- Foundation/Logging の entry 単位 buffer（新規候補）: JSON formatter を包み、一時 buffer 内で変換が成功した後だけ完成 bytes を provider の writer へ渡す。変換失敗時は外側へ bytes を渡さず例外を再送出して provider の callback に届ける。成功コピー後と失敗再送出前の双方で一時 buffer を初期化し、次の entry に残さない。同じ配列を再利用してよく、内容の消去ではなく有効長のリセットを必須とする。`WithLineBreak` は内側の JSON formatter と同じ true とし、改行は provider のみが書く。各 provider の formatter と同寿命、Unity API 非依存。+100〜160行見込み。
- **容量上限の定義:** provider ごとの一時 byte 配列の長さを **1 MiB（1,048,576 bytes）以下**に保つ。`GetMemory` / `GetSpan` は `written + max(sizeHint, 1)` が上限を超えると確保前に例外にし、`Advance` も取得済み範囲外を拒否する。算術 overflow も確保前に拒否する。配列の成長先も上限以内にする。上限は完成 JSON bytes の保証ではない。完成形が 1 MiB 以下でも、writer の見積りによる容量要求が残量を超えれば entry 全体を落として通知する。以前の entry が残した配列容量によって writer の要求列が変わり得るため、同じ完成サイズの entry の通過を保証しない。上限内の容量要求で変換を完了できた entry だけを通常出力する。成功行も対象で、超過時は一時 bytes を外側へ渡さず破棄する。JSON writer 内部・ログキュー・元の値・旧新配列の同時生存を含む総メモリの 1 MiB 制限は主張しない。`ArrayPool` の実配列サイズを無条件に上限内と見なさず、通常配列を使う。2 provider なら各々この上限を持つ。非 Unity の成立確認は §7、Unity は判定 C。processor の置換は、この方法が成立しない場合の Phase A 再検討対象。
- Foundation/Logging 内の独立通知（候補）: factory が所有し、log directory から `logging-errors.log` を一意に導く。第二の本番設定や通知先 injection を足さない。`rolling-json` / `realtime-json` を各 callback に閉じ込め、同じ ZLogger を通さず同期追記する。1行は UTC ISO 8601 時刻、provider 名、固定の `provider-write-error`、例外の型名のみとする。例外の Message / StackTrace / Data / inner exception / 元ログ値は保存しない。型名は最大128文字で切り、制御文字を除去する。UTF-8 BOMなし、LFで1行最大512 bytes（LF含む）に制限し、型名を詰めても収まらなければ固定の `Exception` にする。原因の詳細や失敗ログの復旧はこの通知に求めない。
- 通知の容量・失敗・寿命: ファイル全体は **1 MiB** を上限とし、次の行が収まらなければその通知全体を捨てる。既存内容は切り詰めず、再起動で容量をリセットしない。既存ファイルが上限以上でも追記しない。ローテーションは入れず、容量到達後の新しい通知は保証外。通知ごとにファイルを開いて追記・閉じる（共有は読み取りのみ）、factory 内の一つの lock で両 provider の容量確認と書込みを直列化する。別 factory / process の競合や媒体への部分書込みは復旧保証外。構築時はファイルを開かず、callback 内の open / write / close 等の例外は全て内側で捕捉し、その回の通知を捨てる。通知先の失敗で factory 構築・通常ログを失敗させない。同じ `ILogger`、provider の dispose / flush 待ち、Unity main thread の同期待ちに戻らない。`_inner.Dispose()` の完了後、finally で通知オブジェクトを閉じる（恒常的な file handle は持たない）。drain 中は書込み可能とする。独立通知は +80〜130行見込み。開発者は通常ログと同じ directory のこの file を閲覧する。同じディスクの障害や通知先自身の利用不能時は到達を保証しない。
- `MessagePackZLoggerFormatter` と `JsonFileTelemetrySink`: 変更を予定しない。既存テストを回帰境界として使う。
- Foundation tests: 既存の `InternalsVisibleTo` を利用し、factory 内部コンストラクタの一つの seam で一時 log directory だけを指定する案。通知先は本番と同じ規則でそこから導き、実ファイルを読む。公開コンストラクタをその共通経路へ委譲し、formatter / callback 設定を複製しない。公開 API は変えず、processor 自体は差し替えない。rolling と realtime JSON の実 factory 配線を通す。`Dispose` の完了後に rolling file と realtime の全 bytes、通知を観測し、実時間 sleep を使わない。realtime は一時 file stream を渡し、provider による close 後に file を開き直して bytes を読む。ZLogger は渡された realtime stream を閉じる一方、現 factory コメントは呼び出し側所有と記述しており不一致がある。所有権の実装を変えず、コメントを実挙動に合わせて修正する案。非 Unity の結果を Unity の確認として使わない。factory は配線・寿命に限定し、converter、容量 buffer、通知 I/O をそれぞれ別 file にする。factory の増分は constructor 共通化を含め +40〜70行に改訂し、50%以上の増加が生じても新しい中核ロジックを factory へ混在させない。
- 対応点や通知の所有者が上記で成立しない、公開面・asmdef・寿命が新たに必要、失敗後の JSON 整合を保証できない場合、Phase B で独自設計せず Phase A に戻す。

## 4. 実装順

凍結前の小さい確認は非 Unity 実行で ZLogger 2.5.10 の実 provider と wrapper を用い、正常 A → 64 KiB 超の変換失敗 B → 正常 C で B の bytes が外側へ 0 byte、A/C は各 1 行、成功した 64 KiB 超の entry が 1 行、上限超過時は確保前に止めて通知、失敗後に同じ formatter を再利用できることを確認する。通知と終了の成立も確認する。これは Unity の実 factory 配線の証拠ではない。不成立なら Phase A に戻し、B で processor を場当たり的に改造しない。実行結果を得る前は候補であり成立済みとしない。Unity テストや Build が必要な確認は Phase C の責任に従い、A で実行しない。凍結後は converter・buffer・通知・配線、回帰テスト、文書更新の順。本番 Scene やアセットを変更しない。

## 5. 検証とレビュー候補

- 差戻し中の起点: `pwsh tools/run-tests.ps1 -Filter 'OneStarMaker.Tests.Foundation.AppLoggerFactoryJsonTests'`。加えて `MessagePackZLoggerFormatterTests` / `TelemetryLogCorrelationTests` や buffer / 通知の単体テストから修正に関係するものを C が選択し、XML で実行名を確認する。起点の新設 fixture 名はこの名前に固定する。
- 判定 C: `pwsh tools/run-tests.ps1` の空 filter 全 EditMode、同 XML の件数とテスト名確認、`pwsh tools/contract-audit.ps1`。Unity Editor と project を確認し、人間所有 Editor は止めない。Phase B では Unity test / Build を実行しない。
- 有限 Vector3 の正負・小数・ゼロ、非有限 Vector3 の拒否と通知、複数プロパティ、例外、繰り返し出力、強制 serialization failure と後続正常行、通知先 failure、JSON の行単位 parse、telemetry、MessagePack を検証。確定的な同期で flush/終了を待ち、通知とデータを書き終えた時点を判定する。
- 起点 fixture は新設 `AppLoggerFactoryJsonTests` とする。容量拒否では完成長と要求容量を区別し、70,000文字の成功、同量の先頭後の循環例外、完成形が収まる容量拒否、完成形も超える拒否、各拒否後の正常行を検証する。buffer 単体では要求と残量・確保前拒否・成功/失敗後の有効長リセットを確認する。通知は実ファイルで provider 識別、例外本文の非保存、行/ファイル容量、両 provider の並行通知、構築時と callback 時の利用不能、drain 中の通知を検証する。利用不能は通知 file と同名の directory 等で決定的に作り、通知失敗後も正常ログが出ることを見る。並行性は barrier / signal を使い時間待機しない。
- 操作・目視: 対象なし。開発者の閲覧先は同じ log directory の `logging-errors.log` に確定候補を置いた。E2 は判定 C の実 factory テストがこの本番通知 file を開き、全体の bytes を読むことで閉じる。実装前のファイル到達性を成立済みとはせず、初回確認は判定 C。不成立なら凍結した条件を緩めず B へ戻す。依頼者の裁定により Player 固有の AOT/stripping は今回 GO の対象外・未検証とする。Editor の成功は Player 成功の証拠にしない。
- 高コスト証拠: なしを候補。判定 C の照合 script: なし、テスト自身の assert と XML を使用。
- A0/A1 と A3 統合: OpenAI GPT-6（主担当）。A2 の記録と採否はレビュー後追記。C′ の担当系列は凍結時に確定。

## 6. Phase B 以降

未着手。Phase A 凍結なしに実装しない。

## 7. 未凍結レビューと引継ぎ

- 目的レビュー（OpenAI GPT-6、目的と利用者のみを入力）: 手動型登録や独自 API を要求せず、対象の境界と通知不能時を明示する。候補に反映。目的文の Unity 値型一般という表現と今回の有限 Vector3 の境界の整合は人間の裁定待ち。
- 独立の代替検討（OpenAI GPT-6、A0 相当の依頼）: JSON 共有経路、MessagePack と telemetry の別境界、例外通知の再帰、失敗後の行整合性を指摘。候補に反映。API 成立は未実測。
- 構造の暫定 assist（OpenAI GPT-6-astra、A1 下書き。正式 A2 入力ではない）: シリアライズ途中の断片と媒体 write 失敗を区別し A→失敗 B→C の行整合性を検証すること、通知先の所有者・終了順・並行と再入・閲覧経路を固定すること、実 factory 配線と injection seam を定めること、目的と有限 Vector3 および Player の検証範囲の整合を人間が裁定すること。全件妥当と判断し、凍結前の未解決事項とする。解決のないまま B へ渡さない。
- 正式 A2 の構造・契約レビューは下記の固定 snapshot で実施した。目的レビューは変更後の目的・利用者の原文を用いた既存結果を維持し、この改訂で2欄を変えていない。A3 の人間の採否と凍結は未実施。
- 外部レビュー（Grok 4.7、commit `c16009ba`、正式 A2 ではない）: 大きな entry の失敗で後続行が結合し得る点、通知の thread / 終了順 / provider 識別、実 factory を通した byte 単位の検証、目的・対象型と Player の範囲を採用し本文に反映。I3 を小さい失敗だけに狭める案は、元の行整合条件を失うため現時点では採らず、entry buffer と processor 方式の成立・コストを比較する。formatter の options を置換せず converter を追加する提案も採用。通知先と Player 除外は具体案を含め凍結前の未決。外部レビューは source 調査であり、実行再現ではない。
- 外部再レビュー（Grok 4.7、commit `ea33c3c1`、正式 A2 ではない）: wrapper の例外再送出・buffer 初期化・改行所有、成功した大きな行と上限超過、callback の I/O 例外、公開コンストラクタの共通配線、非 Unity での成立確認、I1 の判定範囲を採用。realtime stream が provider に閉じられる点は採用し、所有権を未決に記録。「閉じた MemoryStream から bytes を読めない」は seek/read には当てはまるが `ToArray()` は dispose 後も成功するため全面採用せず、.NET での限定確認と Unity 未確認を記録。目的文と Player の採否は人間の裁定待ち。
- 外部再レビュー（Grok 4.7、commit `f23abbbc`、正式 A2 ではない）: 上限は変換後ではなく一時 buffer の書込み・拡張中に適用し、超過 bytes を外へ渡さず通知する点を採用。realtime の `ToArray()` は .NET で確認済みだが Editor BCL では未確認とし、閉じた後に file を読む代案を記録。factory コメントは実挙動に合わせる案とし、stream の所有権の実装変更は行わない。目的と Player 範囲の人間の裁定、通知先・上限の値と経路の確認、正式 A2 は未了。
- 外部レビュー（Grok 4.7、commit `d9902b51`、正式 A2 ではない）と依頼者レビュー: 目的の範囲と Player の裁定を本文に反映し、目的レビューをやり直す。容量要求での確保前上限判定と非 Unity spike の観測項目を具体化。通知先の実体、上限値、realtime 観測方式、spike と正式 A2 は未了。2026-11-12 依頼者が推奨した有限 Vector3 と Editor 限定の判定を明示的に承認。凍結の承認ではない。
- 変更後の目的レビュー（OpenAI GPT-6、初稿を読まない別担当、目的・利用者の原文のみ）: 入力 SHA-256 `e18f0a25273b731d202e0e0de44921fe585e9310deae9a14017050cd93b0ab73`。通常の呼出で正負・小数・ゼロを含む有限成分が数値として残り、失敗を再帰しない独立経路で観測し、非同期出力の終了点と出力経路ごとの範囲を明示すべきとした。型登録や呼出側変換、汎用シリアライザ、配送保証基盤は不要とした。既存条件との照合: 呼出・経路・失敗・終了点は本文に記載し、正負・小数・ゼロをテスト対象へ追加。通知先は §3 の障害 file に具体化した候補で、Unity の初回確認は判定 C。
- 外部再レビュー（Grok 4.7、commit `f3fc2bcb`、正式 A2 ではない）: 裁定・目的レビューの入力 hash は整合。通知先、realtime 観測方式、非 Unity spike と正式 A2 が未完了との指摘を採用。通知先は別障害ファイル、realtime の観測は一時 file を候補として本文に具体化した。
- 以前の非 Unity 予備 spike の文章記録は、入力列と出力件数を対応付ける原記録が保存されていないため、成立確認の根拠に使わない。2 MiB は当時の試行値であり採用上限ではない。下記の再実行で置き換える。
- 外部再レビュー（Grok 4.7、commit `930d41a4`、正式 A2 ではない）: 完成長と容量要求の契約不整合、予備 spike の入力列・件数と原記録の不足、障害 file の名前・本文・容量・open 失敗、目的レビュー照合欄の古い未決を指摘。採用し §2〜§5 と本節を修正。容量要求を上限とする案を採り、完成長だけで通過を保証する案は採らない。通常成功の保証を過大にせず、確保前に拒否するという目的を保つため。採否の最終裁定は A3。
- 非 Unity の再実行可能 spike（2026-11-12、引継ぎ後の主担当）: source、project、入力定義、コマンド、console、生 JSONL と各 entry の計測 JSON を同一実行から保存する。ZLogger 2.5.10 の netstandard2.1 DLL は NuGet から直接参照し、SHA-256 が repo の LFS pointer と一致。実 stream provider と同じ JSON formatter instance、一つの再利用 buffer を用い、Dispose を完了点とした。Unity / 実 factory / Vector3 converter / 通知ファイルの証拠ではない。callback はこの spike では例外型をメモリへ記録する。
- 入力は順に A（短い正常）、B（70,000文字と循環オブジェクト）、C（短い正常）、S（70,000文字の正常）、R（300,000文字の正常）、O（3,000,000文字の正常）、D（短い正常）の **7 entry**。文字列は固定 seed の英数字で生成し、message と structured property の双方に入る。同じ入力を 32 MiB の対照と、256 KiB / 1 MiB の制限で実行した。変換例外の B には完成長が存在しないため数値を捏造せず null 相当（失敗・外側0 bytes）として扱う。各入力の対照の完成長、最大 sizeHint、written + sizeHint の最大値、確保済み容量、reset 前の有効長、外側に渡した長さ、通知件数を記録した。
- 観測: 256 KiB は A/C/D の **3行、通知4件**（B/S/R/O の容量拒否）。1 MiB は A/C/S/D の **4行、通知3件**（B の循環例外、R/O の容量拒否）。1 MiB の B は14万 bytes以上が一時 buffer に確定してから失敗し、外側は0 bytes。S は14万 bytes以上の完全な1行。R は対照で約60万 bytesに完成するが、要求する総容量が約120万 bytesで1 MiBを超え、確保前に拒否された。O は完成長自体も上限を超える。各失敗で外側0 bytes、同じ formatter / buffer で後続正常行を記録。各非空行は JSON 値ちょうど1個で、順序、Id、Large と Message の全内容を assert した。生数値の正本は snapshot 内の results.json。全 assert は PASS。これは方式の成立確認であり、スライス GO や Unity での合格ではない。
- 独立 A2（2026-11-12）: 未 commit の改訂版本文内容 SHA-256 `1e9953794aa9b13ee0747fbc676a13be747ab3a2e468c8c39778b9106dec9cb4` と非 Unity spike 一式を同じ Phase A snapshot から取得。構造担当 `gpt-6.1-sol` と契約・容量・証拠担当 `gpt-6-astra` は別の新規 session で互いの所見を見ずに確認した。両担当とも標準 fetch と必須内容の閲覧を確認。構造の blocker なし。契約担当の asmdef 参照不足という指摘は、実 Unity 6000.6.0f1 の既存 BCL 参照と型を追加確認した本人が誤検出として撤回し、最終 blocker はなし。新しい asmdef 参照は加えない。Unity での実装・コンパイル・テストの成功を主張したレビューではない。
- A2 採否: snapshot 内部 manifest の古い自己 hash は非 blocker の梱包欠陥として採用し、自己エントリを除去して再梱包した。HANDOFF / source / 生結果の内容は同一。非有限 Vector3 の拒否・通知は既存条件の検証として §5 に明記。原レビュー・訂正・取得確認は主担当管理の Git 外の引渡し票から渡す。試行錯誤の途中 commit を増やさず、既公開履歴も書き換えない。依頼者の明示的な push 指示により、今回の候補修正と記録を1 commitで公開する。これは A3 凍結の承認とは扱わず、凍結版は別途人間の合意後に作る。
- 主担当の機械検査: `pwsh tools/docs-audit.ps1` はエラー0、既存 program の harvest 警告1。`pwsh tools/contract-audit.ps1` はエラー0・警告0。`git diff --check` も通過。製品コード、Unity アセット、asmdef は変更していない。Unity のテスト・Build は未実行。
- 主担当の再点検: 非 Unity spike は、対照実行でも正常 entry が失敗していると容量拒否との比較が偽陽性になり得たため、対照の正常6行・循環例外1件・各完成長の存在、上限超過 fixture の完成長と拒否時の要求容量を明示 assert に追加。既存の生結果は期待どおりだったが、今後の再実行でも同じ根拠を要求する。I1 は field の互換性と容量による通過可否を区別し、primitive / telemetry も新しい上限で落ち得ることを A3 の判断対象として明記した。factory の増分見積りを一本化し、既存 JSON 設定を保つ制約と差戻し起点 filter も明記した。目的・利用者、型・Player の範囲は変更していない。
- 再点検後の再実行・A2: 補強した非 Unity spike は PASS。対照は両実行とも正常6行・循環例外1件、制限側は256 KiBで正常3行・通知4件、1 MiBで正常4行・通知3件のまま。未 commit の再点検版本文内容 SHA-256 `4a5c3a9a38bd334d80c25a4db188ded75871ac59c24ebc88a5999b3517483701` と source・新しい生結果を別 snapshot に固定し、同じ2担当が独立に差分を再確認した。両担当で標準 fetch・本文 hash・全20 file の hash / 長さを確認し、新しい blocker はなし。元の記録と訂正、再確認の記録、最終引渡し用 package の案内は Git 外の引渡し票にまとめる。容量拒否による既存 entry の欠落、障害通知 file が満杯なら再起動後も新規通知を捨てる境界は、人間の A3 採否を要する候補のまま。
