# Unity ログ値型と出力失敗の検出 — A3 候補（未凍結）

## 0. 状態

- type: slice / status: Phase A、外部レビューを反映した再レビュー候補。凍結・Phase B 開始は未承認。
- branch: codex/logging-unity-value-serialization
- implementation base: f8ffd023 （開始時の origin/develop） / implementation head: 未固定
- risk: high（非同期ログ出力と失敗通知、寿命に影響）
- owner: 主担当 / created: 2026-11-12 / expires: 凍結または置換 revision
- harvest: `unity/Assets/Docs/Architecture/12-telemetry.md` の G5 とログ利用上の制約、`unity/Assets/Docs/Architecture/30-accessibility-identity.md` の Foundation における Vector3 の記述
- Phase A snapshot: 未生成。外部レビュー後、凍結前に固定・引渡し確認する。
- 次: 再レビューと人間の採否、下記の実現経路確認、正式 A2 の後に A3 を判断する。Phase B には進まない。

## 1. 目的と対象

- 目的: Unity の値型を含むログを欠落させず記録でき、記録できない場合も開発者が失敗を検出できるようにする。
- 利用者: Unity アプリのログを使って問題を調査する開発者。
- 現況: `AppLoggerFactory` の JSON formatter は `IncludeProperties.All`。文書 G5 に `Vector3` を structured property に含む行が循環参照で欠落した再現と実例がある。ZLogger 同梱 XML は `InternalErrorLogger` が書込み中の例外に呼ばれ既定 null は無視と説明する。callback の範囲・安全性は未実測。JSON 設定は rolling file と realtime JSON で共有、既定 realtime MessagePack は別 formatter。telemetry は JSON の structured fields を利用する。
- 対象: 再現済み `Vector3` の通常の有限成分と、provider の書込み処理で捕捉される変換失敗の可視化。一般の任意型や物理媒体の故障時の完全な復旧を保証しない。
- 対象外: DebugStudio protocol/画面変更、formatter 以外のログ基盤の作り直し、ログ呼出側の個別文字列化、全 Unity 型の網羅、ログの配信保証、既存ファイル修復、asmdef 参照追加。
- UC: 開発者は従来どおり `ILogger` にメッセージと値を渡す（開発時の呼出、調査したい値を指定するため必要）。アプリ起動・対象出力は既存配線と既定値による。調査時に既存 rolling log と独立した障害通知先を確認する（実行時の閲覧）。値型ごとの登録・新たな操作は求めない。

## 2. 境界と受け入れ候補

- 問い: 既存ログ配線で `Vector3` の行を正常に記録し、シリアライズできない別の値に起因する provider の失敗を、同じ失敗経路に依存せず検出できるか。
- 不変条件 I1: 通常の primitive と telemetry の JSON field の名前・値・種類、既定 MessagePack の wire と telemetry 抑止を変えない。境界は `AppLoggerFactory`、`JsonFileTelemetrySink`、`MessagePackZLoggerFormatter`。依存先の全外部消費者は未確認。
- I2: 失敗通知に同じ `ILogger` / 対象 provider を再利用せず、通知の失敗や多重通知で無限再帰・アプリ例外伝播を生じさせない。provider は背景処理であるため thread と終了境界を検証する。
- I3: **serialization 失敗**では、失敗 entry の bytes を出力先へ渡さず、前後の正常 entry をそれぞれ独立した JSON 行として保つ。64 KiB を超える失敗 entry も対象とする。stream 自体への書込み後の I/O 失敗は巻き戻せず、この条件と区別して通知対象・保証外とする。現行 ZLogger は変換途中に buffer を確定し得るため、converter と callback だけでは I3 を満たせない。実現方式は §3 の候補であり成立未確認。
- 成立条件 E1: 既存の structured logging 呼出で有限の `Vector3` を含む行が JSON として読み取れ、x/y/z を区別して数値として観測できる。message、level、category 等の既存情報も保持する。非有限数と他の Unity 型の保証は後続判断。
- E2: 対象 provider が捕捉したシリアライズ例外を provider ごとに識別できる通知として独立経路へ渡す。同じ entry が両 provider で失敗すれば各々通知され、繰返しの失敗は再通知され得る。通知に元データの復旧は要求せず、通知先自体が利用できない場合の到達は保証しない。開発者が通知を取得する具体的経路は未確定で、成立しないうちは凍結しない。
- E3: rolling と realtime JSON の双方で E1/E2 と I1〜I3 を検証する。既定 MessagePack は既存 wire/telemetry の回帰確認を行う。
- 合格例: `Vector3(1,2,3)` の x/y/z を数値で持つ正常行、正常 A → 64 KiB 超の変換失敗 B → 正常 C を実 provider に通し、出力全体を byte 単位で改行分割した各非空行が JSON 値ちょうど 1 個で A/C が各 1 行、B の断片がなく、対象 provider の通知を確認できる。 不合格例: 行だけが消える、通知が同一 logger に再投入される、B の断片が残る、途中断片と後続行が結合する。
- 分割判断: 型対応と失敗検出は独立に失敗するが、現在の問いは再現された欠落と検知不能の両方を閉じること。小さく収まる前提で一スライスとし、別通知基盤や多数の型へ広がる場合は A に戻して分割する。後続は残りの Unity 型・非有限数・媒体障害の検討（担当未定）。
- GO: 上記不変条件・成立条件と判定必須検証を満たす。NO-GO: いずれか未達。候補の実現性は未確認であり、成立しない場合は仕様を自動緩和せず A に戻す。
- 未決: entry 単位の一時 buffer 案の既存 provider への組込み、メモリ上限と超過時の通知、callback の実際の到達・provider 継続性、独立通知先の選択と可用性、目的文と対象型の整合。これらを A3 凍結前に確認・裁定する。Player の AOT / stripping は実機証拠がなく、Editor の結果から成功を推測しない。

## 3. 責務と実装案

- `Foundation/Logging/AppLoggerFactory.cs`（205行）: 既存 provider の配線、寿命は factory、公開面は `ILogger<T>`。JSON 設定と必要な通知 callback を接続。予定 +20〜40行。
- Foundation/Logging 内の小さな専用 converter（候補、新規）: 状態・I/O を持たず、有限 `Vector3` を x/y/z の数値 object にする。非有限値は変換失敗として通知する候補。formatter の既存 `JsonSerializerOptions` に converter を追加し、他の既定値を置き換えない。ZLogger 2.5.10 の source 上は設定点があるが実配線は未確認。登録と converter は factory と同寿命。公開 API・asmdef 追加なし。+50〜100行見込み。
- Foundation/Logging の entry 単位 buffer（新規候補）: JSON formatter を包み、一時 buffer 内で変換が成功した後だけ provider の writer へ渡す。失敗中に外側 writer へ途中 bytes を渡さない設計を検討する。各 provider の formatter と同寿命、Unity API 非依存。上限・上限超過時の通知と記録、buffer 再利用・例外後復帰、実 provider の改行処理との適合は未決。別 processor の置換も比較し、現行の公開 extension で実現不能なら A で再設計する。推定規模は未確定であり、計画をそのまま B に渡さない。
- Foundation/Logging 内の独立通知（候補）: factory が所有し provider 別の callback から同期的に通知する。複数 provider の並行アクセスを保護し、例外を内側で処理する。同じ `ILogger`、provider の dispose / flush 待ち、Unity main thread の同期待ちに戻らない。provider を `_inner.Dispose()` で drain した後に通知先を閉じる。通知先の具体的な I/O と開発者の閲覧経路、通知先停止時の扱いは未決。+30〜80行見込み。
- `MessagePackZLoggerFormatter` と `JsonFileTelemetrySink`: 変更を予定しない。既存テストを回帰境界として使う。
- Foundation tests: 既存の `InternalsVisibleTo` を利用し、factory 内部コンストラクタの一つの seam で一時 log directory と通知先を指定する案。公開 API は変えず、processor 自体は差し替えない。rolling と realtime JSON の実 factory 配線を通す。`Dispose` の完了後に全 bytes と通知を観測し、実時間 sleep を使わない。seam と stream の所有権は凍結前に確定する。
- 対応点や通知の所有者が上記で成立しない、公開面・asmdef・寿命が新たに必要、失敗後の JSON 整合を保証できない場合、Phase B で独自設計せず Phase A に戻す。

## 4. 実装順

凍結前に ZLogger 2.5.10 の実 provider に対し、entry buffer の組込み・64 KiB 超失敗時の出力・通知経路と終了の成立を小さく確認する。実行結果を得る前は候補であり成立済みとしない。Unity テストや Build が必要な確認は Phase C の責任に従い、A で実行しない。凍結後は converter・buffer・通知・配線、回帰テスト、文書更新の順。本番 Scene やアセットを変更しない。

## 5. 検証とレビュー候補

- 差戻し中の起点: 新設の logging fixture と `MessagePackZLoggerFormatterTests` / `TelemetryLogCorrelationTests` の該当テストを C が選択し、XML で実行名を確認する。具体 filter は fixture 名の凍結時に確定。
- 判定 C: `pwsh tools/run-tests.ps1` の空 filter 全 EditMode、同 XML の件数とテスト名確認、`pwsh tools/contract-audit.ps1`。Unity Editor と project を確認し、人間所有 Editor は止めない。Phase B では Unity test / Build を実行しない。
- 通常値、複数プロパティ、例外、繰り返し出力、強制 serialization failure と後続正常行、通知先 failure、JSON の行単位 parse、telemetry、MessagePack を検証。確定的な同期で flush/終了を待ち、通知とデータを書き終えた時点を判定する。
- 操作・目視: 通知先の実際の閲覧経路は未確定。担当・環境・観測・証拠と到達性の確認を凍結前に決める。自動テストで閉じる範囲は実ファイル全体で判定する。Player 固有の AOT/stripping は今回 GO の対象外とする案であり、人間の採否待ち。Editor の成功は Player 成功の証拠にしない。
- 高コスト証拠: なしを候補。判定 C の照合 script: なし、テスト自身の assert と XML を使用。
- A0/A1 と A3 統合: OpenAI GPT-6（主担当）。A2 の記録と採否はレビュー後追記。C′ の担当系列は凍結時に確定。

## 6. Phase B 以降

未着手。Phase A 凍結なしに実装しない。

## 7. 未凍結レビューと引継ぎ

- 目的レビュー（OpenAI GPT-6、目的と利用者のみを入力）: 手動型登録や独自 API を要求せず、対象の境界と通知不能時を明示する。候補に反映。目的文の Unity 値型一般という表現と今回の有限 Vector3 の境界の整合は人間の裁定待ち。
- 独立の代替検討（OpenAI GPT-6、A0 相当の依頼）: JSON 共有経路、MessagePack と telemetry の別境界、例外通知の再帰、失敗後の行整合性を指摘。候補に反映。API 成立は未実測。
- 構造の暫定 assist（OpenAI GPT-6-astra、A1 下書き。正式 A2 入力ではない）: シリアライズ途中の断片と媒体 write 失敗を区別し A→失敗 B→C の行整合性を検証すること、通知先の所有者・終了順・並行と再入・閲覧経路を固定すること、実 factory 配線と injection seam を定めること、目的と有限 Vector3 および Player の検証範囲の整合を人間が裁定すること。全件妥当と判断し、凍結前の未解決事項とする。解決のないまま B へ渡さない。
- 正式 A2（固定 snapshot を用いる構造レビュー、複数モデルの独立レビュー）は未実施。外部レビューはこれらの未決を含む候補の批評を依頼する。A3 の人間の採否と凍結も未実施。
- 外部レビュー（Grok 4.7、commit `c16009ba`、正式 A2 ではない）: 大きな entry の失敗で後続行が結合し得る点、通知の thread / 終了順 / provider 識別、実 factory を通した byte 単位の検証、目的・対象型と Player の範囲を採用し本文に反映。I3 を小さい失敗だけに狭める案は、元の行整合条件を失うため現時点では採らず、entry buffer と processor 方式の成立・コストを比較する。formatter の options を置換せず converter を追加する提案も採用。通知先と Player 除外は具体案を含め凍結前の未決。外部レビューは source 調査であり、実行再現ではない。
