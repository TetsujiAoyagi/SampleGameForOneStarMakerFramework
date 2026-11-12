# Unity ログ値型と出力失敗の検出 — A3 候補（未凍結）

## 0. 状態

- type: slice / status: Phase A。依頼者の4日保持・次回起動時清掃の指示を反映した改訂案。今回の保持変更も独立 A2 完了。A3 凍結・Phase B 開始は未承認。
- branch: codex/logging-unity-value-serialization
- implementation base: f8ffd023 （開始時の origin/develop） / implementation head: 未固定
- risk: high（非同期ログ出力と失敗通知、寿命に影響）
- owner: 主担当 / created: 2026-11-12 / expires: 凍結または置換 revision
- harvest: `unity/Assets/Docs/Architecture/12-telemetry.md` の G5 とログ利用上の制約（Editor の実 factory で確認した範囲と Player 未検証を区別）、`unity/Assets/Docs/Architecture/30-accessibility-identity.md` の Foundation における Vector3 の記述
- Phase A snapshot の取得案内・状態: 前候補の原記録はローカル固定・取得確認済み。今回の改訂版もローカル固定し、独立2担当が同じ内容を取得・閲覧済み。通知 file の原出力を補完した最終 package の案内と取得確認票は、主担当管理の Git 外の引渡し票に置く。旧 A2 の合格を今回の設計に流用しない。A3 凍結 snapshot は未生成。
- 引継ぎ票: 次は4日保持を含む改訂案の主担当・人間による A3 採否・凍結判断。通知の2ファイル限定保持を撤回し、通常ログと通知を起動時の期間清掃へ揃える。保持期間は内部 policy で後から変更できるようにし、設定項目を増やさない。有限 Vector3 / Editor 限定は承認済み。A3 合意前に Phase B へ進まない。

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
- 不変条件 I1: primitive / telemetry の JSON field の名前・値・種類、既定 MessagePack の wire と telemetry 抑止を変えない。1 MiBを超える容量要求や完成長を理由に正常 entry を落とす新しい制限は設けない。通常ログの既存の日付・サイズでの切替は維持し、保持だけを依頼者指示の4日・次回起動時清掃に揃える。実行中の世代数を理由に過去ログを削除しない。任意サイズのメモリ確保成功や全外部消費者の互換性まで保証するものではない。
- I2: 通知に同じ ILogger / 対象 provider を再利用せず、通知自身の失敗をアプリへ伝播・再帰させない。通知ファイルの満杯、過去の rotation I/O 失敗、再起動を理由に通知機能を恒久停止しない。利用不能な回の到達は保証できないが、次の callback では再び書込み・ローテーションを試みる。背景 thread と終了境界を検証する。
- I3: **serialization 失敗**では、失敗 entry の bytes を出力先へ渡さず、前後の正常 entry をそれぞれ独立した JSON 行として保つ。64 KiB を超える失敗 entry も対象とする。stream 自体への書込み後の I/O 失敗は巻き戻せず、この条件と区別して通知対象・保証外とする。現行 ZLogger は変換途中に buffer を確定し得るため、converter と callback だけでは I3 を満たせない。実現方式は §3 の候補であり、非 Unity spike では成立を確認、実 factory / Unity では未確認。
- 成立条件 E1: 既存の structured logging 呼出で有限の `Vector3` を含む行が JSON として読み取れ、x/y/z を区別して数値として観測できる。message、level、category 等の既存情報も保持する。非有限値は変換失敗として通知する候補とし、値として記録するかは後続判断。他の Unity 型の保証は後続判断。
- E2: rolling JSON / realtime JSON の callback の失敗を provider 名付きで独立した logging-errors_yyyy-MM-dd_nnn.txt に渡す。日付・サイズで新しい連番 file に切り替え、前 file を上書き・世代数で削除せず、今回の通知を書く。保存期間内は2ファイルを超えて残せる。失敗を恒久停止にせず、次回も再試行する。全 callback を serialization 失敗と分類しない。通知先自身の I/O 失敗時の到達は保証しない。MessagePack の配線は変えない。
- E3: rolling と realtime JSON の双方で E1/E2 と I1〜I3 を検証する。既定 MessagePack は既存 wire/telemetry の回帰確認を行う。
- 合格例: 正常 A → 64 KiB超の先頭後に変換失敗 B → 正常 C で、各非空行が JSON 値ちょうど1個、A/C は完全、B の断片は0 bytes。writer の要求が1 MiBを超える正常行と、完成形自体が1 MiBを超える正常行も完全に出る。cold / warm buffer の双方で同じ正常入力が出る。通知は複数回の回転後、満杯状態での再起動後、rotation I/O 障害解消後も新しい行が残る。不合格例: 人為的な1 MiB上限で正常 entry が落ちる、失敗断片と後続行が結合する、通知満杯を理由に以後の通知を捨て続ける。
- 分割判断: 型対応と失敗検出は独立に失敗するが、現在の問いは再現された欠落と検知不能の両方を閉じること。小さく収まる前提で一スライスとし、別通知基盤や多数の型へ広がる場合は A に戻して分割する。後続は残りの Unity 型・非有限数・媒体障害の検討（担当未定）。
- GO: Unity Editor の実 factory テストで不変条件・成立条件と判定必須検証を満たすこと。非 Unity の方式確認は §7 で実施済み。実 factory / converter / Unity の配線は未実装・未確認。Player / IL2CPP / AOT / stripping は今回の対象外で、Editor の結果を Player 成功の証拠にしない。
- 未決: 4日保持の policy と起動時清掃を含む改訂案全体の、人間の A3 採否・凍結判断。独立 A2 の結果は §7 に記録済み。期間と起動時に行う方針は今回の依頼者指示。既存の正常行・通知継続を保つ方式確認は §7、今回の保持変更は §7 に記録する。実 factory / Unity の検証は凍結後の判定 C。

## 3. 責務と実装案

- 新規 file は既存の `Scripts/Foundation/Logging/`、namespace `OneStarMaker.Foundation.Logging`、既存 Foundation asmdef の内部実装に置く。型は `Vector3JsonConverter`（値の変換 policy）、`BufferedJsonFormatter`（formatter と容量 writer を同じ file に置く、entry の確定規則）、`LoggerFailureFile`（障害通知の永続化）を候補名とする。値変換、entry の確定、通知 I/O は独立して変更するため分け、同じ容量アルゴリズム内の writer だけは分割しない。新フォルダ・公開型・asmdef edge は追加しない。converter は Unity の Vector3 と System.Text.Json、buffer は ZLogger / System.Buffers、通知は System の file / lock にだけ依存し、SampleGame / Editor へ依存しない。factory が全体の生成・配線・終了を所有する。buffer の拡張・保持量・reset と通知の容量・失敗・並行性はそれぞれ直接単体テストでき、実 factory テストとは区別する。
- `Foundation/Logging/AppLoggerFactory.cs`（205行）: 既存 provider の配線、寿命は factory、公開面は `ILogger<T>`。JSON 設定と必要な通知 callback を接続。内部 constructor の共通化を含め予定 +50〜80行。
- Foundation/Logging 内の小さな専用 converter（候補、新規）: 状態・I/O を持たず、有限 `Vector3` を x/y/z の数値 object にする。非有限値は変換失敗として通知する候補。formatter の既存 `JsonSerializerOptions` に converter を追加し、他の既定値を置き換えない。既存の null 省略・escaping を保ち、非有限値への対応のために `NumberHandling` を全体で緩めない。ZLogger 2.5.10 の source 上は設定点があるが実配線は未確認。登録と converter は factory と同寿命。公開 API・asmdef 追加なし。+50〜100行見込み。
- Foundation/Logging の entry 単位 buffer（新規候補）: JSON formatter を包み、一時 buffer 内で変換が成功した後だけ完成 bytes を provider の writer へ渡す。変換失敗時は外側へ bytes を渡さず例外を再送出して provider の callback に届ける。成功コピー後と失敗再送出前の双方で一時 buffer を初期化し、次の entry に残さない。同じ配列を再利用してよく、内容の消去ではなく有効長のリセットを必須とする。`WithLineBreak` は内側の JSON formatter と同じ true とし、改行は provider のみが書く。各 provider の formatter と同寿命、Unity API 非依存。+100〜160行見込み。
- **一時 buffer は行の制限ではない:** GetMemory / GetSpan の要求に応じて配列を拡張し、1 MiB超の要求も受ける。Advance の範囲外と算術 overflow は拒否するが、新しい製品上の entry サイズ上限を導入しない。成功・失敗の双方で finally から有効長をリセットする。再利用のため保持する配列は1 MiB以下を目安とし、これを超えた配列は entry 終了時に参照を外す。これはキャッシュ保持量の制御であり、変換中の確保上限でも完成 JSON 長の制限でもない。大きな entry では一時的なメモリ増加がある。旧新配列、serializer 内部、provider buffer、元データ、キューを含む総メモリ上限や、GC の即時回収は主張しない。確保失敗時の通知到達も保証しない。各 provider が formatter / buffer をその寿命中所有し、他 provider と共有しない。通常配列を使い、ArrayPool が実際にはより大きい配列を返す可能性に依存しない。
- Foundation/Logging 内の独立通知（候補）: factory が所有し、log directory 内に logging-errors_yyyy-MM-dd_nnn.txt を生成する。日付は通常ログと同じローカル日付、nnn は非負の連番（最低3桁）とし、通常 JSON の yyyy-MM-dd_nnn.log と区別する。同日の既存最大連番を認識し、再起動でも非満杯の最新 file への追記または次連番への切替で継続する。第二の本番設定・通知先 injection は増やさない。1行は UTC ISO 8601 時刻、rolling-json / realtime-json、固定 provider-write-error、例外型名。例外の Message / StackTrace / Data / inner exception / 元ログ値は保存しない。制御文字と区切りを除き、型名最大128文字、UTF-8 BOMなし・LF込みで1行最大512 bytes。収まらなければ型名を固定 Exception にする。
- 通知のローテーション・寿命: サイズ境界は既存 rollingSizeKb × 1024 bytes（longで計算）を使い、追記で超えるときや日付変更で新しい連番 file へ切り替える。回転時に過去 file を削除しない。factory 内の1つの lock で両 provider の file 選択・切替・追記を直列化し、各通知で file を開いて閉じ、共有は読み取りのみ。構築時は file を開かない。open / roll / write / close 失敗は callback 内で捕捉し、次回も再試行する。同じ ILogger、provider の Dispose / flush 待ち、Unity main thread の同期待ちに戻らない。_inner.Dispose() で drain した後に通知オブジェクトを閉じる。媒体故障・部分書込み・別 process の同時操作・通知先の利用不能時の到達は保証しない。予定 +100〜160行。
- 保持判断の配置: 内部 LogRetentionPolicy に現在の期間 TimeSpan.FromDays(4) と期限比較を置き、file I/O から分離する。別途内部 LogFileNames が生成名と管理対象名の照合を共有し、LogDirectoryMaintenance が対象列挙・削除・失敗処理を所有する。将来の期間変更は policy の一箇所で行え、writer や外部コンフィグへ4という値を散らさない。汎用清掃基盤や新しい公開 API / asmdef は導入しない。policy と名前規則は各 +30〜70行、maintenance は +50〜90行を見込む。
- 起動時の清掃: AppLoggerFactory の共通構築経路で directory 確保後、provider / 通知の file を開く前に1回行う。startupUtc を一度捕捉し、LastWriteTimeUtc < startupUtc - 4日（96時間）の管理対象 file だけを削除候補にする。ちょうど4日は保持し、未来時刻も消さない。起動中の定期清掃は行わず、長時間起動中は4日を超えて残ってよい。時刻は内部引数として渡してテストし、利用者に時刻を入力させない。
- 清掃対象: log directory 直下だけを非再帰で扱う。通常 yyyy-MM-dd_nnn.log と通知 logging-errors_yyyy-MM-dd_nnn.txt の厳密な日付・非負整数連番を検証し、名前だけを広い *.log / *.txt で削除しない。nnn は最低3桁であり、999の次の1000以降も生成・照合の対象とする。任意 file、未出荷の旧試作固定名、directory、reparse point は対象外。root が reparse point の場合も清掃を見送る。名前の日付ではなく最終更新 UTC を期間判断の正とし、古い日付名でも最近追記された file を消さない。
- 清掃の失敗・利用中保護: 全 provider を開く前に行い、OS が共有を拒否する利用中 file は削除失敗として残す。Windows の既存通常 writer と通知 writer は delete sharing を許可しない。この保護は OS の削除拒否に依存し、他 OS や delete sharing を許す別 process の利用中 file まで保護する主張にはしない。列挙・metadata・個別削除の失敗は通常ログの構築を失敗させず、個別失敗では他の候補を続け、次回起動時に再試行する。別 process による名前・時刻の同時変更や hostile な差替えとの原子的な清掃までは保証しない。実ログの削除は今行わず、実装後の最初の通常起動から既存の管理対象にも適用する。Player を判定対象外とすることは、共通 factory を使う Player で清掃を実行しない保証ではない。Player の実行・削除挙動は未検証。
- `MessagePackZLoggerFormatter` と `JsonFileTelemetrySink`: 変更を予定しない。既存テストを回帰境界として使う。
- Foundation tests: 既存の `InternalsVisibleTo` を利用し、factory 内部コンストラクタの一つの seam で一時 log directory だけを指定する案。通知先は本番と同じ規則でそこから導き、実ファイルを読む。公開コンストラクタをその共通経路へ委譲し、formatter / callback 設定を複製しない。公開 API は変えず、processor 自体は差し替えない。rolling と realtime JSON の実 factory 配線を通す。`Dispose` の完了後に rolling file と realtime の全 bytes、通知を観測し、実時間 sleep を使わない。realtime は一時 file stream を渡し、provider による close 後に file を開き直して bytes を読む。ZLogger は渡された realtime stream を閉じる一方、現 factory コメントは呼び出し側所有と記述しており不一致がある。所有権の実装を変えず、コメントを実挙動に合わせて修正する案。非 Unity の結果を Unity の確認として使わない。factory は配線・寿命に限定し、converter、容量 buffer、通知 I/O をそれぞれ別 file にする。factory の増分は constructor 共通化と清掃接続を含め +50〜80行に改訂し、50%以上の増加が生じても新しい中核ロジックを factory へ混在させない。
- 対応点や通知の所有者が上記で成立しない、公開面・asmdef・寿命が新たに必要、失敗後の JSON 整合を保証できない場合、Phase B で独自設計せず Phase A に戻す。

## 4. 実装順

非 Unity の実 ZLogger provider / wrapper と通知 file の小さな試作で方式を確認した（§7）。実 factory / Vector3 converter / Unity の配線は未確認で、凍結後に実装・コンパイル、判定 C の実 factory テストで確認する。試作の成功を製品実装の合格にしない。凍結後は converter → buffer → 通知 → 共通配線 → 回帰テストの順。本番 Scene / Prefab / Addressables は変更しない。

## 5. 検証とレビュー候補

- 差戻し中の起点: `pwsh tools/run-tests.ps1 -Filter 'OneStarMaker.Tests.Foundation.AppLoggerFactoryJsonTests'`。加えて `MessagePackZLoggerFormatterTests` / `TelemetryLogCorrelationTests` や buffer / 通知の単体テストから修正に関係するものを C が選択し、XML で実行名を確認する。起点の新設 fixture 名はこの名前に固定する。
- 判定 C: `pwsh tools/run-tests.ps1` の空 filter 全 EditMode、同 XML の件数とテスト名確認、`pwsh tools/contract-audit.ps1`。Unity Editor と project を確認し、人間所有 Editor は止めない。Phase B では Unity test / Build を実行しない。
- 有限 Vector3 の正負・小数・ゼロ、非有限 Vector3 の拒否と通知、複数プロパティ、例外、繰り返し出力、強制 serialization failure と後続正常行、通知先 failure、JSON の行単位 parse、telemetry、MessagePack を検証。確定的な同期で flush/終了を待ち、通知とデータを書き終えた時点を判定する。
- 起点 fixture は AppLoggerFactoryJsonTests。大きな正常行、cold / warm、大容量後の保持配列縮小、変換失敗後の行整合を確認する。通知は小さい rollingSizeKb で複数回切り替え、2個を超える既存 file の bytes 不変、日付変更、満杯での再構築、障害解消後の再試行、2 provider の並行通知・drain を確認する。連番999の満杯 file から1000への切替を確認し、通常ログ / 通知とも4桁連番を清掃対象として認識することを確認する。清掃は固定 UTC を注入し、通常ログ / 通知の4日境界の直前・一致・直後、古い日付名でも最近更新した file、未来の更新時刻、対象外名、子 directory、root / 個別 file の reparse point、利用中 file、削除失敗、再起動時の再試行を確認する。利用中保護の実 file 検証は Windows の delete sharing 拒否を確認し、その結果を他 OS の保証へ広げない。実時間待機は使わない。
- 操作・目視: 対象なし。開発者の閲覧先は同じ log directory の `logging-errors_yyyy-MM-dd_nnn.txt` に確定候補を置いた。E2 は判定 C の実 factory テストがこの本番通知 file を開き、全体の bytes を読むことで閉じる。実装前のファイル到達性を成立済みとはせず、初回確認は判定 C。不成立なら凍結した条件を緩めず B へ戻す。依頼者の裁定により Player 固有の AOT/stripping は今回 GO の対象外・未検証とする。Editor の成功は Player 成功の証拠にしない。
- 高コスト証拠: なしを候補。判定 C の照合 script: なし、テスト自身の assert と XML を使用。
- A0/A1 と A3 統合: OpenAI GPT-6（主担当）。A2 の記録と採否はレビュー後追記。C′ の担当系列は凍結時に確定。

## 6. Phase B 以降

未着手。Phase A 凍結なしに実装しない。

## 7. 4日保持への変更（依頼者指示と方式確認）

- 依頼者は「後から変えられる構造、コンフィグ追加は不要、期間は4日、次起動でそれ以前を削除」と指示した。2世代という主担当の固定保持案は撤回し、通常ログと障害通知の保持をこの指示に揃える。全体の A3 凍結・Unity 実装の承認とは区別する。
- 現況照合: Unity の AppLoggerFactory は日付・サイズのローテーションを設定するが、期間清掃はない。採用 ZLogger 2.5.10 の provider / RollingFileStream にも期限による削除はない。別製品の DebugStudio.Export の通常ログ writer は既定10世代の件数保持であり、4日期限ではない。DebugStudio 側の変更はこのスライスに含めない。
- 運用: 起動時点から96時間より古い最終更新の、OSM管理の通常ログ / 障害通知を起動前清掃で削除する。日付・サイズの切替は過去 file を残す。期間内の file 数上限を新たに置かない。保持判断は内部 policy の一箇所に置き、後から期間や判断を変更できるようにする。
- 非 Unity の改訂試作は PASS。既存の正常6行・大容量・失敗断片0 bytesを維持。通知200件の複数回切替で2世代を超えて残ること、再起動・日付変更・障害解消・並行100件後の継続、前世代の bytes 不変を確認した。保持では通常 / 通知の12ケース、期限直前・一致・直後、最近更新、未来時刻、対象外名、子 directory 不変、利用中 file の保持と解放後の次回清掃、再実行の冪等性を実 file と固定 UTC で確認した。実 Unity factory / Player の証拠ではない。
- 今回の独立 A2: 未 commit の本文内容 SHA-256 fffd946d472fbdff008ff6a845ac068c59797e73b508055223cbfccf0031df84 を同じ snapshot から gpt-6.1-sol / gpt-6-astra が独立に取得・閲覧。保持期間の内部 policy 化、起動時の4日清掃、通常ローテーション維持は依頼と整合し、方針変更を要する指摘はなし。両担当が見つけた試作の root reparse 除外漏れは採用し、guard を追加。隔離した実 junction の参照先に期限切れの管理対象 file を置き、削除されず bytes も変わらないことを追加確認した。新規の空出力 directory を試作の前提として固定し、全ケースを再実行して PASS。前改訂への合格は今回へ流用していない。

- 目的・利用者の原文は維持。変更後の目的レビュー入力 SHA-256 は e18f0a25273b731d202e0e0de44921fe585e9310deae9a14017050cd93b0ab73。依頼者承認済みの有限 Vector3 / Editor 限定と、独立した失敗検出・手動登録不要という条件を維持する。前候補の source・生結果・レビューは過去の Artifact Storage と Git に保存し、撤回済みの2世代保持や容量拒否を現行指示に混在させない。

- 原記録と最終引渡し: 今回の source・生結果・通知各世代・保持 fixture の残存・レビューを Artifact Storage の別 package に保存し、取得案内と受領確認を Git 外の引渡し票へ置く。実 Unity factory の保持処理は未実装で、実ログの削除は行っていない。docs-audit の結果も同梱する。

### 最新外部レビューの採否（A3 未凍結）

- 対象: 公開 commit `d9a502b5` への [外部レビュー](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/178#issuecomment-6826301465)。公開 HANDOFF の整合確認であり、snapshot の source・生結果は未閲覧、正式 A2 の再実施ではない。主担当の引継ぎでは、指定 commit / clean な作業ツリー / HANDOFF / draft PR の状態が一致した。
- 採用: 最低3桁の連番は1000以降も含む。非 Unity 試作の名前判定はすでに3桁以上を許しているが、4桁の実行結果は既存 PASS に含めない。§3 / §5 に生成・清掃の境界と検証例を補った。
- 採用: 最初の通常起動から既存管理対象へ適用すること、利用中保護が OS の削除拒否に依存すること、Player 対象外は Player で実行されない保証ではないことを §3 に明記した。4日保持・次起動清掃の方針と、Editor に限る判定範囲は維持する。
- 撤回した正常行の容量拒否・満杯後の通知停止・2ファイル限定保持は再採用しない。以前の外部レビューや A2 の整合確認を、これらに対する人間の承認として扱わない。
- 今回は文面と既存条件の検証例の補完。改訂後の版への独立 A2 再確認は未実施。主担当による試作の静的確認は新しい独立 A2 や実 Unity factory の成功判定に数えない。A3 全体の承認・凍結と Phase B 開始は未承認のままとする。
