# 測定用 backend の模型（S-9a-1、`STREAMING_BENCHMARK_BACKEND`）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `A`（**A3 凍結済み**。次は Phase B）
- 位置付け: 小計画 [STREAMING_POLICY_BENCHMARK.md](STREAMING_POLICY_BENCHMARK.md) §4 の S-9a-1。6 本のうちの最初の 1 本
- 版の経緯:
  - 2026-11-27 A1 初稿（S-9a を 1 スライスで行う初稿を owner が「大きい」と判断し、小スライスに分けた後の 1 本目）
  - 2026-11-27 **A3 凍結**（owner）。凍結前再レビュー（入力は head `b85d4872`）を反映した版を凍結した（§5.1）
  - 2026-11-27 A3 統合案の改訂 4。凍結前の実行検証レビュー（入力は head `60cc4b06`）を反映した（§5.1）。模型の動きは変えず、AC の漏れを埋めた
  - 2026-11-27 owner の裁定: 採否は外部計画レビューの推奨どおりとする（§5.1 の「owner の裁定」）。凍結の記録はまだ無い
  - 2026-11-27 A3 統合案の改訂 3。Draft PR #218 の外部計画再レビュー（入力は head `0c0adb8`。判定「ごく小さい AC の補正後に凍結推奨」）を反映した（§5.1）
  - 2026-11-27 A3 統合案の改訂 2。Phase C が判定できない所を主担当が見直した（§5.1 の「Phase C の観点の見直し」）
  - 2026-11-27 A3 統合案の改訂。Draft PR #218 の外部計画レビュー（入力は head `03eb1ced`。判定「小修正後に凍結推奨」）を反映した（§5.1）
  - 2026-11-27 A3 統合案。A2 の 2 件（アーキテクチャゲート、目的レビュー）の採否を §5.1 に記録し、模型の定義と受け入れ条件を改めた
- branch: `claude/s9a-policy-benchmark-handoff-qbq8fq`（`origin/develop` の `61b60e63` から切った計画ブランチ。PR の base は `develop`）
- implementation base commit: `61b60e635a59c35730f39ebac52a7482eb278f43`
- implementation head commit: 未固定
- A3 凍結記録: 承認者 owner（root）。日付 2026-11-27。対象 revision 1（この記録を含む commit が A3 凍結版）。根拠: Draft PR #218 の外部計画レビュー 3 巡（最後の判定は「新たな blocker なし。Python の実行検証の結論の扱いを直してから凍結を推奨」）を反映した後の、owner の凍結の指示
- risk: `low`（test の code だけ。Runtime、公開 API、asmdef を変えない）
- owner: root（依頼者）
- created: 2026-11-27
- expires: 2026-12-31、または置換 revision
- harvest to: 無し（test の道具で、恒久の設計判断を持たない）。模型の定義は小計画 §5.1 に残り、S-9a-3 の測定記録と一緒に `docs/streaming/STREAMING_CURRENT_SPEC.md` §6 へ移る
- Phase A snapshot の取得案内・状態: 未生成（Git 外の補助 file、機械向けの期待値、照合 script が無いので、凍結版は commit だけで固定する。C' 用の入力は Phase C が同じ commit から作る）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割と作業: Phase B（実装）。§3 の 2 file を作り、Editor で compile を確かめ、§6 と本票を書いて終える。Unity テストは実行しない。
  - 次の担当が読む範囲: 既定どおり §0〜§5（§5.1 の採否の記録は読まなくてよい。定義と AC は §2 が正本）。
  - 引き継ぐ commit: この票を含む commit（A3 凍結版）。SHA は tree の外（PR の状態ブロック）で渡す。
  - 必要な権限: Phase B は Unity Editor での compile 確認、Phase C は Unity EditMode テストの実行が要る。Cloud では Unity を動かさない。
  - 停止規則: §2 の定義どおりに作れない時、asmdef の参照か Runtime の変更が要る時は止めて Phase A へ返す。oracle や runner を作り始めたら止まる。
  - 引き渡さないもの: 発見 C・判定 C の所見を C' へ渡さない。

## 1. 目的と対象外

- 目的: Streaming の距離政策を件数と時間で測る後続の測定が土台にできるように、要求の完了とロード済みの状態を Tick の番号だけで決め、照会と要求を数える、test 専用の backend を用意する。
- 利用者: なし
- 対象外:
  - oracle、runner、workload、記録、統計（S-9a-2〜4）。`WorldStreamingController` を測る事自体。
  - `FakeStreamingBackend` の変更。`Runtime/Streaming` を含む Runtime の全て。
  - 確保しない作りの検証（確保量を測る口は S-9a-4 が作る。本スライスは作りだけ揃える）。
  - 小計画 §7 の、他の 2 ラインの領域。
- 現況: 小計画 §2 の表のとおり、`FakeStreamingBackend` は完了してもロード済みにせず、完了を test が 1 件ずつ呼び、`IsLoaded` を数えず、二重要求は保留中の Add への再 Add しか見ない。
- UC ごとの利用者の入力: なし

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 測定用 backend の模型は、定義（下）どおりに完了・ロード済み・計数を返すか。
- 模型の定義（**本節が正本**。A3 で凍結する。小計画 §5.1 は本節を指す）:
  - **構築。** 候補の identity の列（重複なし）、Add の遅延、Remove の遅延（どちらも Tick 数、0 以上）を受ける。id ごとの状態は構築時に確保する。列が null・空・重複あり、列の要素が null か空文字、または遅延が負なら、構築は `ArgumentException`（派生型を含む。以下同じ）にする。列に無い id（null と空文字を含む）を渡す呼び出し（`RequestAdd` / `RequestRemove` / `IsLoaded` / id ごとの読み口）も `ArgumentException` にする（test の誤用で、duplicate ではない）。
  - **時間。** 現在の Tick は 0 から始まり、`AdvanceTick()` で 1 だけ進む。任意の Tick へ飛ぶ口は持たない。
  - **Add。** `RequestAdd(id, priority)` は `現在の Tick + Add の遅延` に完了し、その時点で id はロード済みになる。`priority` は読まず、記録もしない。
  - **Remove。** `RequestRemove(id)` は `現在の Tick + Remove の遅延` に完了し、その時点で id は未ロードになる。ロード済みの id への Remove の保留中は、完了まで id はロード済みである。
  - **保留中の Add への Remove。** Add は元の時点で完了してロード済みになり、Remove は「Add の完了と `現在の Tick + Remove の遅延` の遅い方」に完了して未ロードになる。両方が同じ Tick なら Add、Remove の順に配送する。
  - **遅延 0。** 遅延 0 の Add と、保留中の Add が無い id への遅延 0 の Remove は、呼び出しの中で完了する。状態を反映してから戻り、返る task は完了済みである（継続は生じないので、下の「再入」の対象にならない）。遅延 0 で完了した要求は `AdvanceTick()` の戻り値に出ない。duplicate の判定は即時の完了より先に行う（ロード済みの id への遅延 0 の Add は duplicate である）。保留中の Add への Remove は、Remove の遅延が 0 でも上の行に従う。
  - **配送。** `AdvanceTick()` は、進めた後の Tick が期限の完了を、要求の順に 1 件ずつ配送する。1 件の配送は「状態の反映 → その task の完了」の順で、次の 1 件は前の task の継続が戻った後に反映する。戻り値はその 1 回の `AdvanceTick()` で配送した完了だけの列（種別と id。配送が無ければ空で、前の Tick の配送を含まない）で、backend が持つ buffer の読み取り専用の view である。次の `AdvanceTick()` までだけ有効とし、その後の内容は保証しない。
  - **再入。** 配送の最中（task の継続の中）から `RequestAdd` / `RequestRemove` / `AdvanceTick` が呼ばれたら `InvalidOperationException` にする（列に無い id での再入も、再入の検査を先に行い `InvalidOperationException` にする）。拒否した呼び出しは状態も計数も現在の Tick も変えず、拒否の後も backend は通常どおり要求と `AdvanceTick()` を受け、その `AdvanceTick()` の残りの配送は続く。`IsLoaded` と読み口は呼んでよい。継続が自分で捕まえなかった例外を backend は扱わない（UniTask の扱いに従う）。
  - **duplicate。** 次の 3 種類を別々に数え、例外は投げず、状態を変えず、完了済みの task を返す: (1) 保留中の Add がある id、またはロード済みの id への Add、(2) 保留中の Remove がある id への Remove、(3) 未ロードで保留中の Add も無い id への Remove。Remove は (2) を先に見る（保留中の Add と保留中の Remove の両方がある id への Remove は (2)）。
  - **計数。** `IsLoaded` の呼び出し、Add の要求、Remove の要求を累計で数える。要求の計数は duplicate を含む全ての呼び出しの数である。計数を 0 に戻す口は持たない（区間の値は読む側が差を取る）。
  - **読み口（どれも `IsLoaded` の計数に入らない）。** 現在の Tick、保留中の Add の数、保留中の Remove の数、ロード済みの数、id ごとの「ロード済み / 保留中の Add がある / 保留中の Remove がある」。
  - **持たない物。** 呼び出しの履歴、失敗・取消・再試行・優先度の模型、要求ごとに違う遅延、乱数。
- 進める最低条件:
  - 不変条件:
    - **I1 模型が時間を自分で進めない。** 状態が変わるのは要求と `AdvanceTick` の呼び出しの中だけで、実時間・frame・thread に依らない。
      - 崩しうる経路: 完了を `UniTask.Delay` や `Yield` で作る。`Task.Delay` / `Thread.Sleep` を使う。完了の継続が `AdvanceTick` から戻った後に走る。継続の中から要求が再入して、配送中の保留の列が変わる。状態の反映より先に task が完了して、継続が古い状態を読む。
      - 探索した境界: `ISceneStreamingBackend`、`FakeStreamingBackend`、`WorldStreamingController` の完了の観測（`ObserveAddCompletionAsync`）。確認済み（実行検証レビューが source で確かめた。UniTask 2.5.11、`packages-lock.json` の hash `e5acc106`）: `UniTaskCompletionSource.TrySetResult` は継続をその場で同期に呼ぶ。`async UniTaskVoid` が未完了の task を await した継続は PlayerLoop を通らない（PlayerLoop に触れるのは IL2CPP の枝だけ）。継続の例外は `TrySetResult` の呼び出し元へ伝わらず、未観測例外として `Debug.LogException` へ行く（repo は handler を登録していない）。完了済みの task の await は同期に続く。未確認: その log で Unity Test Framework の test が落ちる事（AC9 は例外を継続の中で捕まえるので、どちらでも成り立つ）。C# は動かしていないので、AC1 でも直接確かめる。
    - **I2 誤った要求を黙って受けない。** duplicate の 3 種類は必ず数えられ、状態を変えない。
      - 崩しうる経路: 二重の Add が保留を上書きして、先の task が完了しないまま残る。未ロードへの Remove がロード済みの数を負にする。Remove を受けた時点でロード済みを外す実装にすると、Remove の保留中の Add が受理される。候補に無い id を受理して状態を増やす。
    - **I3 既存の test と Runtime を変えない。** 変更は §3 の新規 file だけである。
  - 成立条件:
    - **S1** 定義の各行が、下の「定義と確認の対応」のとおりに test か構造レビューで確かめられる。
    - **S2** 同じ要求と `AdvanceTick` の列を 2 回与えると、配送の列と計数が一致する。
    - **S3** 10,000 件の id への Add を出して進めても、定義どおりに全件がロード済みになる。
- 受け入れ条件:
  - **AC1（S1・I1）** Tick 0 に遅延 3 の Add を出す。`AdvanceTick()` を 2 回呼んだ後（Tick 2）は未ロードで task は未完了、3 回目の後（Tick 3）はロード済みで task は完了している。task に付けた継続は 3 回目の `AdvanceTick()` から戻る前に走っており、継続の中で読んだ `IsLoaded` は true である。読み口の「現在の Tick」は `AdvanceTick()` のたびに 1 増える。配送の無い `AdvanceTick()`（1 回目と 2 回目）の戻り値は空で、配送のあった Tick の次の `AdvanceTick()` の戻り値も、前の配送を含まない。Remove も同じ形で確かめる: ロード済みの id に正の遅延の Remove を出すと、期限の前は保留中の Remove = true でロード済み = true、期限の `AdvanceTick()` の後は未ロードで、Remove の task の継続の中で読んだ `IsLoaded` は false である。不合格の例: 3 回目から戻った直後に継続がまだ走っていない。継続の中の `IsLoaded` が false である。
  - **AC2（S1）** 保留中の Add に Remove を出すと、Add の期限でロード済みになり、Remove の期限で未ロードになる。次の 3 つを別々に確かめる。(a) Remove の期限が Add の期限より後: Add の期限の Tick の後はロード済み、Remove の期限の 1 つ前の Tick の後はまだロード済みで保留中の Remove = true、Remove の期限の Tick の後は未ロード。(b) `現在の Tick + Remove の遅延` が Add の期限以下（等しい場合を含む）: Add の期限の 1 回の `AdvanceTick()` が Add、Remove の順に 2 件を返し、終わりは未ロード。(c) Remove の遅延が 0 で Add が保留中: Remove の task は呼び出しから戻った時点で未完了で、(b) と同じく Add の期限に配送される。(b) と (c) では Add の task と Remove の task に継続を 1 つずつ付け（同じ task を 2 回 await しない）、継続の中から読んだ状態を確かめる: Add の継続ではロード済み = true、保留中の Add = false、保留中の Remove = true。Remove の継続ではロード済み = false、保留は両方 false。Add の継続が Remove の継続より先に走る。不合格の例: Add と Remove の状態をまとめて反映してから task を順に完了する実装（Add の継続の中でロード済みが false に見える）。(d) 期限が要求の順に並ばない列（Add 3 / Remove 1）: Y をロード済みにしておき、Tick 3 に X へ Add（期限 6）、Tick 4 に X へ Remove（期限 6）、続けて Y へ Remove（期限 5）を出す。Tick 5 の戻り値は Y の Remove だけで Y は未ロード、Tick 6 の戻り値は X の Add、X の Remove の順である。不合格の例: Remove を要求の順の列で持ち、先頭の期限が来るまで後ろを出さない実装（Tick 5 の戻り値が空になる）。
  - **AC3（S1）** 遅延 0 の Add と、保留中の Add が無い id への遅延 0 の Remove は、呼び出しの中で完了し、返る task は完了済みである。遅延 0 で完了した要求は、次の `AdvanceTick()` の戻り値に出ない。呼び出しから戻った直後に、Add ではロード済み = true、Remove ではロード済み = false で、保留中の Add の数と保留中の Remove の数はどちらも 0 である。
  - **AC4（I2。失敗経路）** 次の 7 つを 1 件ずつ出す: 保留中の Add がある id への Add、保留中の Add と保留中の Remove の両方がある id への Add（種類は (1)）、ロード済みの id への Add、ロード済みで Remove の保留中の id への Add、保留中の Remove がある id への Remove、保留中の Add と保留中の Remove の両方がある id への Remove（種類は (2)）、未ロードで保留も無い id への Remove。それぞれ、対応する種類の duplicate の計数が 1 増え、返る task は完了済みで、呼び出しの直後の保留の数とロード済みの数は呼び出しの前と同じである。先に出した要求は元の期限で完了する。「ロード済みの id への Add」は、Add の遅延が 0 の instance でも 1 件確かめる（即時に完了せず duplicate になる）。
  - **AC5（S1）** `IsLoaded`、Add、Remove の計数が呼び出しの数と一致する。有効な Add 1 回と duplicate の Add 1 回の後、Add の計数は 2、duplicate は 1 である。duplicate の Remove も Remove の計数に入る。読み口を何回呼んでも `IsLoaded` の計数は増えない。
  - **AC6（S2）** 同じ列を 2 つの instance に与えると、`AdvanceTick()` が返す列と全ての計数が一致する。同じ期限の完了は要求の順に並ぶ。この順は、id の序数順とも構築時の列の順とも違う順で要求して確かめ、違う id の Add と Remove が同じ期限になる case を 1 つ含める（Add を全部出してから Remove、のような種別ごとの並びにならない事）。違う id の 2 つの Add が同じ期限の時、先の Add の継続の中から読むと、後の id はまだ未ロードで、保留中の Add が true である。
  - **AC7（S3）** 10,000 件の id に Add を出して期限まで進めると、ロード済みの数が 10,000、保留が 0 になる。別の新しい instance で、10,000 件の Add を全て保留にしたまま 10,000 件の Remove を出し、Add と Remove の期限を同じ Tick に揃えると、その 1 回の `AdvanceTick()` が 20,000 件を返し、duplicate は 0、終わりは全件が未ロードである。view の再利用と読み取り専用である事は構造レビューで見る（期限を過ぎた view の内容は契約にしない）。
  - **AC8（I1・I3）** `git diff --name-status <A3 凍結 commit> <implementation head>` が、§3 の 5 file の追加と、本書（`docs/handoff/STREAMING_BENCHMARK_BACKEND.md`）への実装・レビューの記録だけである。他の計画の文書の変更は含めない（implementation base からの差分には計画の文書 4 file が入るので、基準にしない。Phase B は `develop` を branch へ取り込まない）。`pwsh tools/contract-audit.ps1`（`Task.Delay` / `Thread.Sleep`、`#nullable enable`、`record` を含む）と `pwsh tools/docs-audit.ps1` が違反なし。
  - **AC11（I3）** GO 候補 head の全 EditMode 回帰（空 filter）が failed 0 で、XML に `StreamingBenchmarkBackendTests` の test が §5 の「XML で確かめる集合」のとおり入っている。本スライスと関係の無い test が落ちた時の扱いは §5 に従う。
  - **AC9（I1。失敗経路）** task の継続の中から `RequestAdd`、`RequestRemove`、`AdvanceTick` を呼ぶと、それぞれ `InvalidOperationException` になる。test の継続は例外を自分で捕まえて記録し、`AdvanceTick()` から戻った後に確かめる（継続から例外を漏らすと、UniTask の未観測例外として log に出て、別の理由で test が落ちる）。拒否した呼び出しの直前と直後に継続の中で読み口を読み、計数・保留の数・現在の Tick が変わっていない。同じ `AdvanceTick()` の後続の配送は行われている。戻った後、現在の Tick は 1 だけ進んでおり、次の `RequestAdd` と `AdvanceTick()` は通常どおり受理される。
  - **AC10（I2。失敗経路）** 構築時の列に無い id（null と空文字を含む）で `RequestAdd`、`RequestRemove`、`IsLoaded`、id ごとの読み口を呼ぶと `ArgumentException` になり、計数と状態は変わらない。構築は、列そのものが null、列が空、列に重複がある、列の要素が null、列の要素が空文字、遅延が負、のそれぞれで `ArgumentException` になる。
- 定義と確認の対応（S1。判定 C はこの表で漏れを見る）:

  | 定義の行 | 確かめる方法 |
  |---|---|
  | 構築 | AC10 |
  | 時間 | AC1（現在の Tick）。任意の Tick へ飛ぶ口が無い事は構造レビュー |
  | Add | AC1 |
  | Remove | AC2 (a)、AC4（Remove の保留中もロード済み） |
  | 保留中の Add への Remove | AC2 |
  | 遅延 0 | AC3（戻り値に出ない事を含む）、AC2 (c)。duplicate の判定が先である事は AC4 の「ロード済みの id への Add」を遅延 0 の instance でも 1 件確かめる |
  | 配送 | AC1（戻り値は当該 Tick の配送だけ）、AC2（中間の状態、(d) の期限が要求の順に並ばない列）、AC6（要求の順）、AC7（件数）。戻り値の view の再利用と読み取り専用は構造レビュー |
  | 再入 | AC9 |
  | duplicate | AC4 |
  | 計数 | AC5 |
  | 読み口 | AC1、AC2、AC5、AC6 |
  | `priority` を読まない、持たない物 | 構造レビュー（test では示せない） |
  | 実装制約の「全件を走査・整列しない」「buffer を伸ばさない」「履歴を溜めない」 | 構造レビュー（発見 C が code を読んで判定する。時間や確保の test は足さない） |
- ここでは答えない問いと所有する後続スライス:
  - oracle と runner が件数を正しく数えるか → S-9a-2
  - runner が、どの id に要求が出たかを知る方法 → S-9a-2。この backend は履歴を持たず、遅延 0 の完了は `AdvanceTick()` の戻り値に出ない。S-9a-2 は、正の遅延の workload で Tick の前後の id ごとの状態の差から復元できる条件を示すか、要る最小の観測の口を S-9a-2 で足す。本スライスでは履歴も通知も足さない
  - Controller が件数の予算に収まるか → S-9a-3
  - 時間と確保 → S-9a-4。backend 自身が測定中に確保しない事の確認も S-9a-4
  - 実物の SceneDirector との一致 → S-9b（`SEASON_WORLD_DESIGN.md` §6）
- スライス分割の判断: 契機なし。小計画 §4 で分けた後の 1 本で、問いは 1 つ、新規 code は約 530 行である（A2 と見直しと実行検証の反映で約 400 行から増えた。問いは 1 つのままで、増えたのは test である。backend は約 260 行のまま）。500 行の警報に当たるが、test の case を別のスライスに分けると測定器の正しさを 1 回で判定できなくなるので、分割しない。
- 判定定義: **GO** は I1〜I3 と S1〜S3 を満たす事（AC1〜AC11 が全て合格）。**NO-GO** はそのいずれかを満たさない事。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。oracle や runner を作り始めたら停止する。
- A3 後の例外承認: なし
- 決定記録: なし
- 本文へ転記した実装制約:
  - Unity 側の `.cs` は先頭に `#nullable enable`。`record` を使わない。
  - test で `Task.Delay` / `Thread.Sleep` を使わない。
  - asmdef の参照を足さない。既存の `OneStarMaker.Tests` に置く。
  - repo 相対 path は 150 文字以内（`tools/path-budget.json`）。
  - 長い実行で確保が伸びない作りにする: 呼び出しの履歴を溜めない。id ごとの状態は構築時に受けた identity の列で確保する。`AdvanceTick()` の戻り値は再利用する buffer にする。保留ごとの completion source の確保は本スライスでは許容する（要求を出さない Tick では走らない。確保量の確認は S-9a-4）。
  - backend が測定を支配しない作りにする（後続の S-9a-4 は `controller.Tick` の中の `IsLoaded` / `RequestAdd` / `RequestRemove` ごと測る）: identity から状態への索引を構築時に作る。`IsLoaded` と要求の受付で、候補の全件も保留の全件も走査・整列しない。配送の buffer は、同じ Tick に Add と Remove の両方が全候補分（候補数の 2 倍）来ても伸ばさずに済む大きさで確保する。`AdvanceTick()` の処理は Tick の時間の測定の外である。時間や確保を測る code と test は本スライスでは足さない。
  - Remove の期限は要求の順に単調ではない（保留中の Add への Remove は Add の期限まで延びる）。期限ごとに、要求の順で取り出せる持ち方にする。
  - 完了には pool しない `UniTaskCompletionSource` を使う（`AutoResetUniTaskCompletionSource` は結果の取得で返却され、その後に task の状態を読むと例外になるので、AC1・AC4 の「task は完了している」を継続の後に読めない）。
  - Unity テストは Phase C。Phase B は Editor での compile 確認まで。
- 未決事項:
  - A1 の U1（保留中の Add への Remove の模型は 1 通りで足りるか）と U2（duplicate を例外ではなく計数にする）は、A2 で「1 通りで足りる」「計数でよい」となり、その形で定義に入れた（§5.1）。
  - なし（目的の文言は owner が「変えない」と裁定した。§5.1）。

## 3. 責務マップ

新規 file は `unity/Assets/OneStarMaker/Tests/Streaming/Benchmark/` に置く（asmdef は既存の `OneStarMaker.Tests`、namespace は `OneStarMaker.Tests.Streaming.Benchmark`）。所有者は test で、寿命は test の 1 件の中だけである。`.meta` は Editor が作る。

| file | 責務 | 依存 | 公開面 | 行数（現在 → 見込み） |
|---|---|---|---|---|
| `StreamingBenchmarkBackend.cs` | §2 の模型。`ISceneStreamingBackend` の実装、Tick 番号での完了、計数、保留とロード済みの読み口 | `ISceneStreamingBackend`、UniTask | `internal` | 0 → 約 260 |
| `StreamingBenchmarkBackendTests.cs` | AC1〜AC7、AC9、AC10 | 上 | test | 0 → 約 270 |

変更 file は 5 つ: 上の 2 file、それぞれの `.meta`、新しい folder `Benchmark` の `.meta`（`Benchmark.meta`）。

配置の理由: `FakeStreamingBackend` に遅延と計数を足すと既存の test の前提（例外、手動の完了）が変わるので、別の型にする。後続の測定器と同じ folder に置き、Runtime には置かない。行数の警報は合計 500 行に当たる（約 530 行）。非分割の理由は §2「スライス分割の判断」。3 責務と 50% 増には当たらない。

## 4. 実装計画

- 変更対象: §3 の 2 file。
- 順序: backend を作る → test を書く → Editor で compile を確認する。1 commit。
- test の書き方（Phase C が XML と AC を突き合わせられるようにする）:
  - 全て同期の `[Test]` にする（`[UnityTest]`、PlayerLoop、frame の待ちを使わない）。
  - 継続は、Controller の `ObserveAddCompletionAsync` と同じ形（task を 1 回だけ await する `async UniTaskVoid` の観測 method）で付ける。Controller が実際に使う形で I1 を確かめるためである。
  - test method の名前を `AC<番号>_` で始める（例: `AC2_RemoveOnPendingAdd_...`）。1 つの AC に複数の test があってよい。
  - `.meta` は手で書かず、Editor が作った物を commit する。
- Phase B の出口の記録（§6）: Editor での compile 確認の方法と結果（Console の error 0）、確認できなかった場合はその旨、test 未実行である事。
- Phase B から Phase A へ差し戻す条件: 模型の定義どおりに作れない（`UniTaskCompletionSource` の継続が同期に走らない等）。asmdef の参照か Runtime の変更が要ると分かった。
- 行数: 1 file が 300 行を超えるか、合計が 500 行を超えた時は、止めずに理由を §6 に書く（構造レビューの警報で、分割の命令ではない）。
- 対象外を維持する方法: 変更 file を AC8 で照合する。

## 5. テストとレビュー計画

- 単体テスト: `StreamingBenchmarkBackendTests`（AC1〜AC7、AC9、AC10）。
- 差し戻し中の起点 `-Filter`: `OneStarMaker.Tests.Streaming`（既存の Streaming の test と新しい fixture。限定実行では XML で新しい fixture の test が入った事を確かめる）
- 判定必須テスト（GO 候補 head）: 全 EditMode 回帰 `pwsh tools/run-tests.ps1`（空 filter、1 process）
- XML で確かめる集合（判定 C）: fixture `OneStarMaker.Tests.Streaming.Benchmark.StreamingBenchmarkBackendTests` に、名前が `AC1_`、`AC2_`、`AC3_`、`AC4_`、`AC5_`、`AC6_`、`AC7_`、`AC9_`、`AC10_` で始まる test がそれぞれ 1 件以上あり、全て Passed である。AC2 は (a)(b)(c) を別の test か別の case として持つ。全体の件数は固定しない（他の fixture の件数は本スライスの条件ではない）。接頭辞だけでは AC の中の個々の case の漏れは分からないので、判定 C は §2 の AC の文と「定義と確認の対応」の表を test の code と突き合わせて、case ごとに確かめる。test の名前と件数をこれ以上は固定しない。
- 本スライスと関係の無い test が全 EditMode 回帰で落ちた時: GO にしない。同じ test が A3 凍結 commit でも落ちる事を同じ環境の実行で示し、その証拠を付けて owner に返す（本スライスの欠陥ではないので B へは戻さない。owner が GO の扱いを決める）。Unity の終了時の native stall と `0xC0000005` は [Harness README の停止規則](../../tools/Harness/README.md#unity終了時の既知native-stallと調査の停止規則) に従い、XML が完成していれば XML と log の末尾で判定する。
- 判定の evidence に入れる物: `run-tests.ps1` の出力一式（XML、Unity の log、step の記録）、`git diff --name-status <A3 凍結 commit> <implementation head>` の出力、2 つの audit の出力。
- 全 EditMode 回帰の適用除外: なし
- 統合・Unity テスト: 上だけ。
- 操作・実行時・目視条件の検証経路: なし。動作確認は `対象なし`。
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点: なし（既存の EditMode の経路だけを使う）
- 人間の判断が必要な条件: なし
- 高コスト証拠: なし
- 判定 C の照合 script の方式: なし（test の XML の合否で足りる）
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`
- A0/A1 主担当・モデル・ベンダー: Cloud の計画担当（owner の 2026-11-27 の指示により、文書にモデル・製品の割当を書かない）
- C' 用に予約した担当・モデル・ベンダー: owner が 2026-11-27 に指名した（A・B・C と外部計画レビューに関与していない系統。名前は owner の指示により文書に書かず、起動の時に owner が渡す）
- 独立性の強化条件を満たせない場合の理由: 該当なし（risk は `low`）

### 5.1 A2 の独立レビューと A3 の採否

- A2 独立レビュー（2026-11-27。互いの指摘を見せていない。モデル・製品の名前は owner の指示により書かない）:
  - **アーキテクチャゲート。** 担当: 主担当と別のモデルの独立した担当。入力版: 固定 base `c4bf4143186840b1d19010bec12c59e2cb43f3da`、本書の copy の sha256 `b11abe9114fd41862098a2bed8d1fc672355c3fad5e07c91998385e1571a4d5f`、小計画の copy の sha256 `ea2c7de7d271e6797ad73f9d48178773371a3dd403ac45aee96c2f5b352abc2a`。結果: blocker なし、major 6 件、minor 6 件、note 5 件。「A3 へ進めてよい。凍結の前に major を反映する」。
  - **目的レビュー。** 担当: 上の 2 者と別のモデルの独立した担当。入力: §1 の目的の原文だけ（利用者は `なし`）。原文の sha256 `93bdfc28be0a62196f0847446911a4a6f9fb5548da915f9eb03c91e75c83b286`。
- A3 統合担当・採否（統合は A1 の主担当。凍結の判断は owner）:

  | # | 出所 | 指摘 | 採否 | 反映 |
  |---|---|---|---|---|
  | G1 | ゲート major | 「遅延 0 は呼び出しの中で完了」と「保留中の Add への Remove は遅い方」が、Remove の遅延 0 で矛盾する | 採用 | 定義「遅延 0」、AC2 (c)、AC3 |
  | G2 | ゲート major | 「保留中の id への Add」が、Remove の保留を含むか読めない | 採用 | 定義「Remove」「duplicate」(1)、AC4 |
  | G3 | ゲート major | 構築引数（遅延だけ）と実装制約（候補数で確保）が食い違う。候補に無い id が未定義 | 採用 | 定義「構築」、AC10 |
  | G4 | ゲート major | 任意の Tick へ進める口の「現在の Tick」と、継続からの再入が未定義 | 採用 | 口を 1 Tick ずつ進める `AdvanceTick()` に絞り、配送中の再入を例外にした。定義「時間」「再入」、AC9 |
  | G5 | ゲート major | 状態の反映と task の完了の順序が未定義 | 採用 | 定義「配送」、AC1 |
  | G6 | ゲート major | 後続の oracle に、id ごとの保留の有無の読み口が足りない。配送の 1 件に完了 Tick が無い | 一部採用 | id ごとの保留の読み口を足した。完了 Tick は、1 Tick ずつ進める形にしたので現在の Tick と同じになり、足さない |
  | G7 | ゲート minor | 要求の計数が duplicate を含むか、読み口が照会数に入るかが読めない | 採用 | 定義「計数」「読み口」、AC5 |
  | G8 | ゲート minor | 過去の Tick を渡した時が未定義 | 採用（G4 で消えた） | 任意の Tick を渡す口が無い |
  | G9 | ゲート minor | 戻り値の列と保留ごとの completion source が「確保が伸びない作り」と食い違う | 採用 | 戻り値は再利用する buffer。completion source は許容と明記 |
  | G10 | ゲート minor | 「小計画 §5.1 と同じ」が事実と違う | 採用 | 本書 §2 を正本にし、小計画 §5.1 は本書を指す |
  | G11 | ゲート minor | 変更 file の照合に `.meta` が入っていない | 採用 | §3、AC8 |
  | G12 | ゲート minor | AC1・AC2・AC4 の時点の書き漏れ | 採用 | AC1、AC2 (b)、AC4 |
  | G13 | ゲート minor | `priority` の扱いが無い。duplicate の 3 種類別は合計で足りる | 一部採用 | `priority` は読まないと明記。3 種類別は診断に使えて費用が小さいので残す |
  | G14 | ゲート note | duplicate に完了済みの task を返すと Controller の in-flight の会計が実物とずれる。duplicate が出た後の件数の測定は無効として扱うべき | 後続へ移送 | S-9a-2（小計画 §4） |
  | G15 | ゲート note | U1: 模型は 1 通りで足りる。U2: 計数でよい。規模は適切 | 採用 | 未決事項から外した |
  | M1 | 目的 | 計数のリセット・履歴・通知は要らない（test ごとに作り直せば済む） | 採用 | 定義「計数」から 0 に戻す口を外した |
  | M2 | 目的 | 重複する要求も、呼ばれた数のまま数える | 採用 | G7 と同じ |
  | M3 | 目的 | 失敗・取消・再試行・優先度・乱数・実時間を作らない | 採用（初稿どおり） | 定義「持たない物」 |
  | M4 | 目的 | 完了までの Tick 数を要求ごとに変えられる | 不採用 | 小計画 §5.2 の 24 workload は一律の遅延で足りる。要る workload が出た時に足す |
  | M5 | 目的 | 既存の `IAssetManagement` 経由・寿命スコープに沿う | 不採用 | この backend は asset をロードしない。差し替えの境界は既存の `ISceneStreamingBackend` で、新設しない |
  | M6 | 目的 | Tick を進める独自の loop や UpdateSystem への登録を作らない | 採用（初稿どおり） | Tick は test が `AdvanceTick()` で進める |

- **目的の文言（owner が「変えない」と裁定済み。上の「owner の裁定」）:** 目的レビューは、目的の文だけからは次が読めないと指摘した: Tick の番号の出所、完了までの Tick 数の決め方、ロード済みの状態の単位と戻り方、「照会」の意味、数える対象の区別、test 専用の範囲、差し替える抽象が既存か新設か、後続の測定が要る値。目的・利用者の文言への指摘の採否は owner が裁定する。統合担当の案は「文言を変えない」である。理由: どれも §2 の定義で答えており、目的の文に入れると定義の写しになる。文言を変える裁定なら、変えた原文で目的レビューを 1 回やり直してから凍結する。
- A2 の入力の base について: A2 のゲートは `c4bf4143` を読んだ。implementation base は `61b60e63` である。2 つの差分は `docs/` の 4 file だけで、`unity/` 配下に差分は無い（`git diff --stat 61b60e6 c4bf414`）。ゲートが読んだ code は implementation base と同じである。
- Draft PR #218 の外部計画レビュー（2026-11-27、owner のアカウントからの投稿。入力は head `03eb1ced`。判定「S-9a-1 は小修正後に凍結推奨」）。下の採否は統合担当の案で、確定は owner の凍結判断による:

  | # | 指摘 | 採否（案） | 反映 |
  |---|---|---|---|
  | E1 | Major 1: AC2・AC6 は配送の順だけでなく、各完了の継続から見える中間の状態を固定する | 採用 | AC2、AC6 |
  | E2 | Major 2: backend の hot path の計算量、配送 buffer、`AdvanceTick` が測定の外である事を決める。件数の runner の観測の境界を残す | 採用 | 実装制約、「ここでは答えない問い」、小計画 §4 |
  | E3 | Minor: AC8 の差分の基準を A3 凍結 commit からにする。A2 の入力 base と implementation base の違いを確かめる | 採用 | AC8、上の行 |
  | E4 | 申し送り（S-9a-2/3）: 観測の時系列、B4 の導出、starvation の区別、duplicate は FAIL と記録 | 後続へ移送 | 小計画 §4・§5.3・§5.4・§6 |
  | E5 | 申し送り（S-9a-2/3）: 収束は `desired ⊆ resident ⊆ retain`。親計画の「`desired = resident`」を直す。B3 は模型の条件 | 採用 | 小計画 §5.4、`SEASON_WORLD_DESIGN.md` §6 |
  | E6 | 申し送り（S-9a-4）: 全 Tick・要求を出した Tick・静かな Tick の分布を分けて記録する。条件を A3 で固定する | 後続へ移送 | 小計画 §4・§5.3 |
  | E7 | 申し送り（S-9a-6）: 先行するのは純政策の値の emit と表示の基盤だけ。T-08 の完了は S-9b | 採用 | 小計画 §4、`SEASON_WORLD_DESIGN.md` §6 |
  | E8 | 目的の文言は変えなくてよい。M4・M5 の不採用、模型 1 通り、duplicate の計数に賛成 | —（owner の裁定の入力） | — |

- **owner の裁定（2026-11-27）:** 採否は外部計画レビューの推奨どおりとする。これにより次が確定した。
  - E1〜E8 と R1〜R7 の採否は、下の 2 つの表の「採否（案）」のとおり。
  - 目的の文言は変えない。目的レビューのやり直しは要らない。
  - A2 の M4（要求ごとの遅延）と M5（`IAssetManagement` 経由）は不採用。模型は 1 通り（保留 unload）、duplicate は計数。
  - C' は、A・B・C と外部計画レビューに関与していない担当を owner が指名する。
  - スライス分割と A2 の全面再実施はしない。
  - 凍結は、凍結前再レビューの反映の後に owner が指示した（§0 の A3 凍結記録）。
- 凍結前の実行検証レビュー（2026-11-27、owner の依頼。担当は主担当が起動した独立した担当。入力: head `60cc4b06307fd4dba8154cd280b36e781e0b4b8d`、本書の copy の sha256 `675ca8e62ffb0136e50bb4811bfcd02b276e21d6e67767b8c56a78f322f2df93`、小計画の copy の sha256 `f5d3b306502dc33ff0660286e4d2b4b9f34953242421d8e199681fb913199bdd`）:
  - **やった事。** §2 の定義だけから模型を python で実装し、AC を test にして通した。誤実装を 24 個作り、AC を全て通る物を探した。Controller の Tick を python に写して模型と組み、小計画の 24 workload（1,000 と 10,000 候補、縮小なし）と乱数の列 約 25 万を回した。UniTask の source（lock の hash の commit）を読んだ。
  - **限界。** C# は 1 行も動かしていない。全て python の写しでの結果である。script と結果は Git に入れておらず、Cloud の作業領域にあるだけで保存していない（Artifact Storage へは Cloud から届かない）。判定の証拠には使えない。
  - **結論。** blocker なし。定義だけで実装でき、状態（5 つ）× 操作（3 つ）の全ての組が定義の 1 行で決まる。UniTask の前提は source の読解で成り立つ。major は 1 群: AC を全て通る誤実装が 12 個あった。Controller の写しと組んだ試行では duplicate も配送中の再入も出なかったが、これは python の写しと有限の入力での探索で、C# で成り立つ事実ではない（S-9a-2 の最初の疎通と S-9a-3 の測定が確かめる）。
  - 採否（どれも既存の AC への 1〜2 文の追加で、模型の動きを変えない。凍結前再レビューが V1〜V7・V9 の採用、V8 の不採用、V10 の移送を推奨し、owner が凍結で確定した）:

  | # | 指摘 | 採否 | 反映 |
  |---|---|---|---|
  | V1 | major: 種別ごとの列で持ち、先頭の期限が来るまで後ろを出さない実装が AC を通る（実装制約から最も自然に出る形）。Remove の期限は要求の順に単調でない | 採用 | AC2 (d)、実装制約 |
  | V2 | major: 保留中の Add への Remove の期限が 1 Tick 早い実装が通る（測定値が変わる） | 採用 | AC2 (a) |
  | V3 | major: 戻り値を溜め続ける実装、遅延 0 の完了を次の戻り値に入れる実装が通る（後続の stale の計数が変わる） | 採用 | 定義「配送」「遅延 0」、AC1、AC3 |
  | V4 | minor: 同じ期限の順を id 順・列の順・種別ごとにした実装が通る | 採用 | AC6 |
  | V5 | minor: 拒否の後に guard を戻し忘れる実装、拒否した `AdvanceTick` が Tick だけ進める実装が通る。「拒否の後に変わらない」を戻った後に読むと文面どおりにならない | 採用 | 定義「再入」、AC9 |
  | V6 | minor: duplicate になる組は 7 つで、AC4 は 6 つ（両方の保留がある id への Add が無い） | 採用 | AC4 |
  | V7 | note: duplicate の Remove が計数に入る例が無い。列に無い id での再入の例外の型。Remove の保留中のロード済みの文。遅延 0 と duplicate の先後。AC10 の null と空文字 | 採用 | AC5、定義「再入」「Remove」「遅延 0」、AC10 |
  | V8 | note: 配送の中で読む現在の Tick を固定する | 不採用 | 測定値を変えず、要る利用者が無い |
  | V9 | note: UniTask の前提は確認済み。pool する completion source では AC1・AC4 の読み方ができない | 採用 | I1、実装制約 |
  | V10 | 小計画への入力（B4 の反例と式の候補、テレポートの読み、workload の値） | 後続へ移送 | 小計画 §4 |

- Draft PR #218 の凍結前再レビュー（2026-11-27、owner のアカウントからの投稿。入力は head `b85d4872`。判定「S-9a-1 の設計に新たな blocker なし。Python の実行検証の結論の扱いを直してから凍結を推奨。これ以上 mutation を潰すために A3 を膨らませない」）と owner の凍結:

  | # | 指摘 | 採否 | 反映 |
  |---|---|---|---|
  | F1 | 保存していない Python の写しの結論を、実物で成り立つ事実として書かない。stale の残留は Remove の遅延 1 でも 0 Tick に限らない。「テレポート中は何も resident にならない」は重なりの記述と合わない。B4 の式は候補として扱う | 採用 | 小計画 §4 の実行検証の節を「未検証の参考」に縮め、数値と 2 つの一般化を消した。上の「結論」の書き方 |
  | F2 | S-9a-2 の最初に、実物の Controller を新しい backend につなぐ小さい疎通を置く。本スライスの scope は増やさない | 後続へ移送 | 小計画 §4 |
  | F3 | S-9a-4 の測定値 A は同期の `controller.Tick` の区間で、政策の総 CPU 時間ではないと明記する | 後続へ移送 | 小計画 §4 |
  | F4 | 追加の Python 模型、mutation の基盤、独自の profiler、Runtime の計測 hook は要らない。C' を予約して凍結し、Phase B へ進む。C# の単体 test と疎通で反証が出た時だけ相応の Phase に戻る | 採用 | 凍結（§0） |

- Draft PR #218 の外部計画再レビュー（2026-11-27、owner のアカウントからの投稿。入力は head `0c0adb8`。判定「S-9a-1 はごく小さい AC の補正後に凍結推奨」。E1〜E8 の採否案には全て賛成、目的の文言は変更不要、スライス分割と A2 の全面再実施は不要、との意見）。下の採否は統合担当の案で、確定は owner の凍結判断による:

  | # | 指摘 | 採否（案） | 反映 |
  |---|---|---|---|
  | R1 | AC7 の「続けて」は全件ロード済みからの Add になり duplicate になる。新しい instance で、全 Add を保留中に全 Remove を出す。期限を過ぎた view の内容は契約にしない | 採用 | AC7、定義「配送」、対応表 |
  | R2 | AC3 に呼び出し直後の状態と保留数。ロード済みへの正の遅延の Remove の case。両方の保留がある id への Remove が種類 (2) になる case | 採用 | AC3、AC1、AC4 |
  | R3 | AC10 は列そのものの null・空と、要素の null・空を区別し、どちらも拒否する | 採用 | 定義「構築」、AC10 |
  | R4 | AC 接頭辞だけでは case の漏れを検出できない。判定 C が対応表で case を照合すれば足り、名前や件数をこれ以上固定しない | 採用 | §5「XML で確かめる集合」 |
  | R5 | AC8 の `docs/handoff` の許容を本書の記録に限る | 採用 | AC8 |
  | R6 | C' は A・B・C と外部計画レビューに関与していない担当を owner が予約する | 採用 | §5 |
  | R7 | 後続（S-9a-2〜4、P5）への提言 | 後続へ移送 | 小計画 §4・§5.2・§5.3・§5.4・§6 |

- Phase C の観点の見直し（2026-11-27、主担当。owner の依頼。レビューではなく、判定 C が合否を決められない所を埋めた）:

  | # | 判定できなかった所 | 直し方 |
  |---|---|---|
  | C1 | S1「定義の各行に対応する test」に対応表が無く、漏れを判定できない。test では示せない行（`priority`、持たない物、実装制約）の判定方法が無い | 「定義と確認の対応」の表。test で示せない行は構造レビューと明記 |
  | C2 | 判定 C は XML の test 名と件数で必要な集合を確かめるが、名前も件数も決まっていない | test 名を `AC<番号>_` で始める。§5「XML で確かめる集合」 |
  | C3 | 構築の引数が不正な時（null、空、重複、負の遅延）が未定義 | 定義「構築」、AC10 |
  | C4 | AC9 で継続から例外を漏らすと、未観測例外の log で test が別の理由で落ちる。拒否の後の状態と残りの配送が未定義 | 定義「再入」、AC9 |
  | C5 | 継続の付け方が決まっておらず、PlayerLoop に依る書き方をされると EditMode で不定になる | §4「test の書き方」（同期の `[Test]`、Controller と同じ観測の形） |
  | C6 | AC8 が file の差分・audit・全回帰を 1 つに束ね、無関係な test の失敗の扱いが無い | AC8 と AC11 に分けた。§5 に無関係な失敗の扱い |
  | C7 | 戻り値の view の「次の `AdvanceTick` まで有効」と buffer の大きさを確かめる条件が無い | AC7 |
  | C8 | duplicate の (2) と (3) の優先、遅延 0 と再入の関係が読めない | 定義「duplicate」「遅延 0」 |
  | C9 | Phase B の compile 確認の記録と、行数の警報に当たった時の扱いが無い | §4 |
  | C10 | C' の担当が「未定」のままで、凍結の時に誰が決めるかが無い | §5（owner が凍結の時に決める） |

  模型の動き（完了の時点、順序、duplicate の種類）は変えていない。変えたのは、未定義だった入力の扱い（C3、C4）と、確認の方法である。
- 設計の変更（G4 の口の絞り込み、G6 の読み口の追加）に対するアーキテクチャゲートの再レビュー: 不要と判断した。どちらもゲートの提案をそのまま採った物で、口を減らす向きの変更である。Draft PR の外部計画レビューはこの形を読んだ上で、凍結推奨とした。E1〜E3 は test と実装制約の追加で、模型の定義を変えないので、再レビューは不要と判断した。

## 6. Phase B 実装結果

未着手

### 動作確認

対象なし（§5）。

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手
