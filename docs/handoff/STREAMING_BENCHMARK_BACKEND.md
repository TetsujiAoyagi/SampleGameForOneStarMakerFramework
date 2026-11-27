# 測定用 backend の模型（S-9a-1、`STREAMING_BENCHMARK_BACKEND`）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `A`（A1 初稿。A2 未実施、A3 未凍結。実装キューではない）
- 位置付け: 小計画 [STREAMING_POLICY_BENCHMARK.md](STREAMING_POLICY_BENCHMARK.md) §4 の S-9a-1。4 本のうちの最初の 1 本
- 版の経緯:
  - 2026-11-27 A1 初稿（S-9a を 1 スライスで行う初稿を owner が「大きい」と判断し、4 本に分けた後の 1 本目）
- branch: `claude/s9a-policy-benchmark-handoff-qbq8fq`（`origin/develop` の `61b60e63` から切った計画ブランチ。PR の base は `develop`）
- implementation base commit: `61b60e635a59c35730f39ebac52a7482eb278f43`
- implementation head commit: 未固定
- A3 凍結記録: 未凍結
- risk: `low`（test の code だけ。Runtime、公開 API、asmdef を変えない）
- owner: root（依頼者）
- created: 2026-11-27
- expires: 2026-12-31、または置換 revision
- harvest to: 無し（test の道具で、恒久の設計判断を持たない）。模型の定義は小計画 §5.1 に残り、S-9a-3 の測定記録と一緒に `docs/streaming/STREAMING_CURRENT_SPEC.md` §6 へ移る
- Phase A snapshot の取得案内・状態: 未生成（A3 で作る。Git 外の補助 file は無い）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割と作業: Phase A の A2（アーキテクチャゲート 1 件と目的レビュー 1 件）。
  - 次の担当が読む範囲: 本書の全節と、小計画の §2・§5.1。目的レビューの担当は §1 の「目的」の原文だけを読む。
  - 引き継ぐ commit: この票を含む commit。SHA は tree の外で渡す。
  - 必要な権限: A2 は読むだけ。Phase B は Unity Editor での compile 確認、Phase C は Unity EditMode テストの実行が要る。Cloud では Unity を動かさない。
  - 停止規則: A3 の凍結の前に Phase B へ入らない。
  - 引き渡さないもの: 目的レビューには §1 の「目的」の原文だけを渡す。

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
- 模型の定義（小計画 §5.1 と同じ。A3 で凍結する）:
  - 構築時に Add の遅延と Remove の遅延（Tick 数、0 以上）を受ける。現在の Tick は 0 から始まり、`AdvanceTo(tick)` でだけ進む。
  - `RequestAdd(id)` は `現在の Tick + Add の遅延` に完了し、その時点で id はロード済みになる。
  - `RequestRemove(id)` は `現在の Tick + Remove の遅延` に完了し、その時点で id は未ロードになる。
  - 保留中の Add に `RequestRemove` が来たら、Add は元の時点で完了してロード済みになり、Remove は「Add の完了と `現在の Tick + Remove の遅延` の遅い方」に完了して未ロードになる。
  - 遅延 0 の要求は、呼び出しの中で完了する（返る task は完了済み）。
  - `AdvanceTo` は期限が来た完了を、期限の順、同じ期限なら要求の順に配送し、配送した完了の列（種別と id）を返す。
  - duplicate として数えて、例外は投げない: 保留中かロード済みの id への Add、保留中の Remove がある id への Remove、未ロードで Add の保留も無い id への Remove。duplicate の要求は状態を変えず、完了済みの task を返す。
  - 数える物: `IsLoaded` の呼び出し、Add の要求、Remove の要求、duplicate（3 種類を別々に）。読む口: 保留中の Add の数、保留中の Remove の数、ロード済みの数、id ごとのロード済み。計数は区間ごとに読んで 0 に戻せる。
- 進める最低条件:
  - 不変条件:
    - **I1 模型が時間を自分で進めない。** 状態が変わるのは要求と `AdvanceTo` の呼び出しの中だけで、実時間・frame・thread に依らない。
      - 崩しうる経路: 完了を `UniTask.Delay` や `Yield` で作る。`Task.Delay` / `Thread.Sleep` を使う。完了の継続が `AdvanceTo` から戻った後に走る。
      - 探索した境界: `ISceneStreamingBackend`、`FakeStreamingBackend`、`WorldStreamingController` の完了の観測（`ObserveAddCompletionAsync`）。未確認: `UniTaskCompletionSource` の継続が `TrySetResult` の中で同期に走る事（既存の test がこの前提で通っているが、本スライスの test でも直接確かめる）。
    - **I2 誤った要求を黙って受けない。** duplicate の 3 種類は必ず数えられ、状態を変えない。
      - 崩しうる経路: 二重の Add が保留を上書きして、先の task が完了しないまま残る。未ロードへの Remove がロード済みの数を負にする。
    - **I3 既存の test と Runtime を変えない。** 変更は §3 の新規 file だけである。
  - 成立条件:
    - **S1** 定義の各行に対応する test があり、通る。
    - **S2** 同じ要求と `AdvanceTo` の列を 2 回与えると、配送の列と計数が一致する。
    - **S3** 10,000 件の id への Add を出して進めても、定義どおりに全件がロード済みになる。
- 受け入れ条件:
  - **AC1（S1・I1）** 遅延 3 の Add は、`AdvanceTo(2)` の後は未ロードで task は未完了、`AdvanceTo(3)` の後はロード済みで task は完了している。task に付けた継続は `AdvanceTo(3)` から戻る前に走っている。不合格の例: `AdvanceTo(3)` から戻った直後に継続がまだ走っていない。
  - **AC2（S1）** 保留中の Add に Remove を出すと、Add の期限でロード済みになり、Remove の期限で未ロードになる。Remove の期限が Add の期限より前に計算される場合は、Add の期限に両方が Add、Remove の順で配送され、終わりは未ロードである。
  - **AC3（S1）** 遅延 0 の Add と Remove は呼び出しの中で完了し、返る task は完了済みである。
  - **AC4（I2。失敗経路）** 保留中の id への Add、ロード済みの id への Add、保留中の Remove がある id への Remove、未ロードの id への Remove を出すと、対応する duplicate の計数が 1 ずつ増え、保留の数とロード済みの数は変わらず、先に出した要求は元の期限で完了する。
  - **AC5（S1）** `IsLoaded`、Add、Remove の計数が呼び出しの数と一致し、0 に戻した後は 0 から数える。
  - **AC6（S2）** 同じ列を 2 つの instance に与えると、`AdvanceTo` が返す列と全ての計数が一致する。同じ期限の完了は要求の順に並ぶ。
  - **AC7（S3）** 10,000 件の Add を出して期限まで進めると、ロード済みの数が 10,000、保留が 0 になる。
  - **AC8（I1・I3）** 変更 file が §3 と一致する。`pwsh tools/contract-audit.ps1`（`Task.Delay` / `Thread.Sleep`、`#nullable enable`、`record` を含む）と `pwsh tools/docs-audit.ps1` が違反なし。全 EditMode 回帰が通る。
- ここでは答えない問いと所有する後続スライス:
  - oracle と runner が件数を正しく数えるか → S-9a-2
  - Controller が件数の予算に収まるか → S-9a-3
  - 時間と確保 → S-9a-4。backend 自身が測定中に確保しない事の確認も S-9a-4
  - 実物の SceneDirector との一致 → S-9b（`SEASON_WORLD_DESIGN.md` §6）
- スライス分割の判断: 契機なし。小計画 §4 で分けた後の 1 本で、問いは 1 つ、新規 code は約 400 行である。
- 判定定義: **GO** は I1〜I3 と S1〜S3 を満たす事。**NO-GO** はそのいずれかを満たさない事。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。oracle や runner を作り始めたら停止する。
- A3 後の例外承認: なし
- 決定記録: なし
- 本文へ転記した実装制約:
  - Unity 側の `.cs` は先頭に `#nullable enable`。`record` を使わない。
  - test で `Task.Delay` / `Thread.Sleep` を使わない。
  - asmdef の参照を足さない。既存の `OneStarMaker.Tests` に置く。
  - repo 相対 path は 150 文字以内（`tools/path-budget.json`）。
  - 長い実行で確保が伸びない作りにする: 呼び出しの履歴を溜めない。id ごとの状態は構築時に受けた候補数で確保する。
  - Unity テストは Phase C。Phase B は Editor での compile 確認まで。
- 未決事項:
  - **U1（A2）** 保留中の Add への Remove の模型を、この 1 通り（保留 unload）で足りるとするか。Add を取り消してロード済みにしない模型を足すなら、構築時の選択にする（小計画 P3）。
  - **U2（A2）** duplicate を例外ではなく計数にする事。測定を最後まで回して数を記録するための選択で、`FakeStreamingBackend`（例外）とは違う。

## 3. 責務マップ

新規 file は `unity/Assets/OneStarMaker/Tests/Streaming/Benchmark/` に置く（asmdef は既存の `OneStarMaker.Tests`、namespace は `OneStarMaker.Tests.Streaming.Benchmark`）。所有者は test で、寿命は test の 1 件の中だけである。`.meta` は Editor が作る。

| file | 責務 | 依存 | 公開面 | 行数（現在 → 見込み） |
|---|---|---|---|---|
| `StreamingBenchmarkBackend.cs` | §2 の模型。`ISceneStreamingBackend` の実装、Tick 番号での完了、計数、保留とロード済みの読み口 | `ISceneStreamingBackend`、UniTask | `internal` | 0 → 約 240 |
| `StreamingBenchmarkBackendTests.cs` | AC1〜AC7 | 上 | test | 0 → 約 160 |

配置の理由: `FakeStreamingBackend` に遅延と計数を足すと既存の test の前提（例外、手動の完了）が変わるので、別の型にする。後続の測定器と同じ folder に置き、Runtime には置かない。行数の警報（500 行、3 責務、50% 増）には当たらない。

## 4. 実装計画

- 変更対象: §3 の 2 file。
- 順序: backend を作る → test を書く → Editor で compile を確認する。1 commit。
- Phase B から Phase A へ差し戻す条件: 模型の定義どおりに作れない（`UniTaskCompletionSource` の継続が同期に走らない等）。asmdef の参照か Runtime の変更が要ると分かった。
- 対象外を維持する方法: 変更 file を AC8 で照合する。

## 5. テストとレビュー計画

- 単体テスト: `StreamingBenchmarkBackendTests`（AC1〜AC7）。
- 差し戻し中の起点 `-Filter`: `OneStarMaker.Tests.Streaming`
- 判定必須テスト（GO 候補 head）: 全 EditMode 回帰 `pwsh tools/run-tests.ps1`
- 全 EditMode 回帰の適用除外: なし
- 統合・Unity テスト: 上だけ。
- 操作・実行時・目視条件の検証経路: なし。動作確認は `対象なし`。
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点: なし（既存の EditMode の経路だけを使う）
- 人間の判断が必要な条件: なし
- 高コスト証拠: なし
- 判定 C の照合 script の方式: なし（test の XML の合否で足りる）
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`
- A0/A1 主担当・モデル・ベンダー: Cloud の計画担当（owner の 2026-11-27 の指示により、文書にモデル・製品の割当を書かない）
- C' 用に予約した担当・モデル・ベンダー: 未定（A3 で、A・B・C に関与しない担当を予約する）
- 独立性の強化条件を満たせない場合の理由: 該当なし（risk は `low`）

### 5.1 A2 の独立レビューと A3 の採否

- A2 独立レビューごとの観点・担当・入力版: 未実施
- A3 統合担当・採否: 未実施

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
