# 測定用 backend の模型（S-9a-1、`STREAMING_BENCHMARK_BACKEND`）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `A`（A3 統合案。A2 実施済み。**未凍結**: owner の凍結判断の前。実装キューではない）
- 位置付け: 小計画 [STREAMING_POLICY_BENCHMARK.md](STREAMING_POLICY_BENCHMARK.md) §4 の S-9a-1。6 本のうちの最初の 1 本
- 版の経緯:
  - 2026-11-27 A1 初稿（S-9a を 1 スライスで行う初稿を owner が「大きい」と判断し、小スライスに分けた後の 1 本目）
  - 2026-11-27 A3 統合案。A2 の 2 件（アーキテクチャゲート、目的レビュー）の採否を §5.1 に記録し、模型の定義と受け入れ条件を改めた
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
  - 次の Phase・役割と作業: Phase A の A3 の残り。Draft PR で外部計画レビューを受け、owner が §5.1 の「owner の裁定待ち」を裁定して凍結を判断する。凍結の記録（承認者・日付・対象 revision）を本欄の上の「A3 凍結記録」に書いた commit が A3 凍結版で、その後に Phase B（実装）へ渡す。
  - 次の担当が読む範囲: 本書の全節。模型の定義の正本は本書 §2 である。
  - 引き継ぐ commit: この票を含む commit。SHA は tree の外（PR の状態ブロック）で渡す。
  - 必要な権限: Phase B は Unity Editor での compile 確認、Phase C は Unity EditMode テストの実行が要る。Cloud では Unity を動かさない。
  - 停止規則: owner の凍結の記録が無い間は Phase B へ入らない。
  - 引き渡さないもの: 目的・利用者の文言を変えた場合の目的レビューのやり直しには、変えた原文だけを渡す。

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
  - **構築。** 候補の identity の列（重複なし）、Add の遅延、Remove の遅延（どちらも Tick 数、0 以上）を受ける。id ごとの状態は構築時に確保する。列に無い id を渡す呼び出し（`RequestAdd` / `RequestRemove` / `IsLoaded` / 読み口）は `ArgumentException` にする（test の誤用で、duplicate ではない）。
  - **時間。** 現在の Tick は 0 から始まり、`AdvanceTick()` で 1 だけ進む。任意の Tick へ飛ぶ口は持たない。
  - **Add。** `RequestAdd(id, priority)` は `現在の Tick + Add の遅延` に完了し、その時点で id はロード済みになる。`priority` は読まず、記録もしない。
  - **Remove。** `RequestRemove(id)` は `現在の Tick + Remove の遅延` に完了し、その時点で id は未ロードになる。Remove の保留中も、完了までは id はロード済みである。
  - **保留中の Add への Remove。** Add は元の時点で完了してロード済みになり、Remove は「Add の完了と `現在の Tick + Remove の遅延` の遅い方」に完了して未ロードになる。両方が同じ Tick なら Add、Remove の順に配送する。
  - **遅延 0。** 遅延 0 の Add と、保留中の Add が無い id への遅延 0 の Remove は、呼び出しの中で完了する（返る task は完了済み）。保留中の Add への Remove は、Remove の遅延が 0 でも上の行に従う。
  - **配送。** `AdvanceTick()` は、進めた後の Tick が期限の完了を、要求の順に 1 件ずつ配送する。1 件の配送は「状態の反映 → その task の完了」の順で、次の 1 件は前の task の継続が戻った後に反映する。戻り値は配送した完了の列（種別と id）で、backend が持つ buffer の読み取り専用の view である。次の `AdvanceTick()` までだけ有効とする。
  - **再入。** 配送の最中（task の継続の中）から `RequestAdd` / `RequestRemove` / `AdvanceTick` が呼ばれたら `InvalidOperationException` にする。`IsLoaded` と読み口は呼んでよい。
  - **duplicate。** 次の 3 種類を別々に数え、例外は投げず、状態を変えず、完了済みの task を返す: (1) 保留中の Add がある id、またはロード済みの id への Add、(2) 保留中の Remove がある id への Remove、(3) 未ロードで保留中の Add も無い id への Remove。
  - **計数。** `IsLoaded` の呼び出し、Add の要求、Remove の要求を累計で数える。要求の計数は duplicate を含む全ての呼び出しの数である。計数を 0 に戻す口は持たない（区間の値は読む側が差を取る）。
  - **読み口（どれも `IsLoaded` の計数に入らない）。** 現在の Tick、保留中の Add の数、保留中の Remove の数、ロード済みの数、id ごとの「ロード済み / 保留中の Add がある / 保留中の Remove がある」。
  - **持たない物。** 呼び出しの履歴、失敗・取消・再試行・優先度の模型、要求ごとに違う遅延、乱数。
- 進める最低条件:
  - 不変条件:
    - **I1 模型が時間を自分で進めない。** 状態が変わるのは要求と `AdvanceTick` の呼び出しの中だけで、実時間・frame・thread に依らない。
      - 崩しうる経路: 完了を `UniTask.Delay` や `Yield` で作る。`Task.Delay` / `Thread.Sleep` を使う。完了の継続が `AdvanceTick` から戻った後に走る。継続の中から要求が再入して、配送中の保留の列が変わる。状態の反映より先に task が完了して、継続が古い状態を読む。
      - 探索した境界: `ISceneStreamingBackend`、`FakeStreamingBackend`、`WorldStreamingController` の完了の観測（`ObserveAddCompletionAsync`）。未確認: `UniTaskCompletionSource` の継続が `TrySetResult` の中で同期に走る事（UniTask の source は A2 でも読めていない。既存の test がこの前提で通っており、本スライスの AC1 で直接確かめる）。
    - **I2 誤った要求を黙って受けない。** duplicate の 3 種類は必ず数えられ、状態を変えない。
      - 崩しうる経路: 二重の Add が保留を上書きして、先の task が完了しないまま残る。未ロードへの Remove がロード済みの数を負にする。Remove を受けた時点でロード済みを外す実装にすると、Remove の保留中の Add が受理される。候補に無い id を受理して状態を増やす。
    - **I3 既存の test と Runtime を変えない。** 変更は §3 の新規 file だけである。
  - 成立条件:
    - **S1** 定義の各行に対応する test があり、通る。
    - **S2** 同じ要求と `AdvanceTick` の列を 2 回与えると、配送の列と計数が一致する。
    - **S3** 10,000 件の id への Add を出して進めても、定義どおりに全件がロード済みになる。
- 受け入れ条件:
  - **AC1（S1・I1）** Tick 0 に遅延 3 の Add を出す。`AdvanceTick()` を 2 回呼んだ後（Tick 2）は未ロードで task は未完了、3 回目の後（Tick 3）はロード済みで task は完了している。task に付けた継続は 3 回目の `AdvanceTick()` から戻る前に走っており、継続の中で読んだ `IsLoaded` は true である。不合格の例: 3 回目から戻った直後に継続がまだ走っていない。継続の中の `IsLoaded` が false である。
  - **AC2（S1）** 保留中の Add に Remove を出すと、Add の期限でロード済みになり、Remove の期限で未ロードになる。次の 3 つを別々に確かめる。(a) Remove の期限が Add の期限より後: Add の期限の Tick の後はロード済み、Remove の期限の Tick の後は未ロード。(b) `現在の Tick + Remove の遅延` が Add の期限以下（等しい場合を含む）: Add の期限の 1 回の `AdvanceTick()` が Add、Remove の順に 2 件を返し、終わりは未ロード。(c) Remove の遅延が 0 で Add が保留中: Remove の task は呼び出しから戻った時点で未完了で、(b) と同じく Add の期限に配送される。
  - **AC3（S1）** 遅延 0 の Add と、保留中の Add が無い id への遅延 0 の Remove は、呼び出しの中で完了し、返る task は完了済みである。
  - **AC4（I2。失敗経路）** 次の 5 つを 1 件ずつ出す: 保留中の Add がある id への Add、ロード済みの id への Add、ロード済みで Remove の保留中の id への Add、保留中の Remove がある id への Remove、未ロードで保留も無い id への Remove。それぞれ、対応する種類の duplicate の計数が 1 増え、返る task は完了済みで、呼び出しの直後の保留の数とロード済みの数は呼び出しの前と同じである。先に出した要求は元の期限で完了する。
  - **AC5（S1）** `IsLoaded`、Add、Remove の計数が呼び出しの数と一致する。有効な Add 1 回と duplicate の Add 1 回の後、Add の計数は 2、duplicate は 1 である。読み口を何回呼んでも `IsLoaded` の計数は増えない。
  - **AC6（S2）** 同じ列を 2 つの instance に与えると、`AdvanceTick()` が返す列と全ての計数が一致する。同じ期限の完了は要求の順に並ぶ。
  - **AC7（S3）** 10,000 件の Add を出して期限まで進めると、ロード済みの数が 10,000、保留が 0 になる。
  - **AC8（I1・I3）** 変更 file が §3 の 5 file と一致する。`pwsh tools/contract-audit.ps1`（`Task.Delay` / `Thread.Sleep`、`#nullable enable`、`record` を含む）と `pwsh tools/docs-audit.ps1` が違反なし。全 EditMode 回帰が通る。
  - **AC9（I1。失敗経路）** task の継続の中から `RequestAdd`、`RequestRemove`、`AdvanceTick` を呼ぶと、それぞれ `InvalidOperationException` になる。
  - **AC10（I2。失敗経路）** 構築時の列に無い id で `RequestAdd`、`RequestRemove`、`IsLoaded`、id ごとの読み口を呼ぶと `ArgumentException` になり、計数と状態は変わらない。
- ここでは答えない問いと所有する後続スライス:
  - oracle と runner が件数を正しく数えるか → S-9a-2
  - Controller が件数の予算に収まるか → S-9a-3
  - 時間と確保 → S-9a-4。backend 自身が測定中に確保しない事の確認も S-9a-4
  - 実物の SceneDirector との一致 → S-9b（`SEASON_WORLD_DESIGN.md` §6）
- スライス分割の判断: 契機なし。小計画 §4 で分けた後の 1 本で、問いは 1 つ、新規 code は約 480 行である（A2 の反映で約 400 行から増えた。500 行未満）。
- 判定定義: **GO** は I1〜I3 と S1〜S3 を満たす事。**NO-GO** はそのいずれかを満たさない事。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。oracle や runner を作り始めたら停止する。
- A3 後の例外承認: なし
- 決定記録: なし
- 本文へ転記した実装制約:
  - Unity 側の `.cs` は先頭に `#nullable enable`。`record` を使わない。
  - test で `Task.Delay` / `Thread.Sleep` を使わない。
  - asmdef の参照を足さない。既存の `OneStarMaker.Tests` に置く。
  - repo 相対 path は 150 文字以内（`tools/path-budget.json`）。
  - 長い実行で確保が伸びない作りにする: 呼び出しの履歴を溜めない。id ごとの状態は構築時に受けた identity の列で確保する。`AdvanceTick()` の戻り値は再利用する buffer にする。保留ごとの completion source の確保は本スライスでは許容する（要求を出さない Tick では走らない。確保量の確認は S-9a-4）。
  - Unity テストは Phase C。Phase B は Editor での compile 確認まで。
- 未決事項:
  - A1 の U1（保留中の Add への Remove の模型は 1 通りで足りるか）と U2（duplicate を例外ではなく計数にする）は、A2 で「1 通りで足りる」「計数でよい」となり、その形で定義に入れた（§5.1）。
  - **owner の裁定待ち:** 目的の文言（§5.1 の「owner の裁定待ち」）。

## 3. 責務マップ

新規 file は `unity/Assets/OneStarMaker/Tests/Streaming/Benchmark/` に置く（asmdef は既存の `OneStarMaker.Tests`、namespace は `OneStarMaker.Tests.Streaming.Benchmark`）。所有者は test で、寿命は test の 1 件の中だけである。`.meta` は Editor が作る。

| file | 責務 | 依存 | 公開面 | 行数（現在 → 見込み） |
|---|---|---|---|---|
| `StreamingBenchmarkBackend.cs` | §2 の模型。`ISceneStreamingBackend` の実装、Tick 番号での完了、計数、保留とロード済みの読み口 | `ISceneStreamingBackend`、UniTask | `internal` | 0 → 約 260 |
| `StreamingBenchmarkBackendTests.cs` | AC1〜AC7、AC9、AC10 | 上 | test | 0 → 約 220 |

変更 file は 5 つ: 上の 2 file、それぞれの `.meta`、新しい folder `Benchmark` の `.meta`（`Benchmark.meta`）。

配置の理由: `FakeStreamingBackend` に遅延と計数を足すと既存の test の前提（例外、手動の完了）が変わるので、別の型にする。後続の測定器と同じ folder に置き、Runtime には置かない。行数の警報（500 行、3 責務、50% 増）には当たらない。

## 4. 実装計画

- 変更対象: §3 の 2 file。
- 順序: backend を作る → test を書く → Editor で compile を確認する。1 commit。
- Phase B から Phase A へ差し戻す条件: 模型の定義どおりに作れない（`UniTaskCompletionSource` の継続が同期に走らない等）。asmdef の参照か Runtime の変更が要ると分かった。
- 対象外を維持する方法: 変更 file を AC8 で照合する。

## 5. テストとレビュー計画

- 単体テスト: `StreamingBenchmarkBackendTests`（AC1〜AC7、AC9、AC10）。
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

- **owner の裁定待ち（目的の文言）:** 目的レビューは、目的の文だけからは次が読めないと指摘した: Tick の番号の出所、完了までの Tick 数の決め方、ロード済みの状態の単位と戻り方、「照会」の意味、数える対象の区別、test 専用の範囲、差し替える抽象が既存か新設か、後続の測定が要る値。目的・利用者の文言への指摘の採否は owner が裁定する。統合担当の案は「文言を変えない」である。理由: どれも §2 の定義で答えており、目的の文に入れると定義の写しになる。文言を変える裁定なら、変えた原文で目的レビューを 1 回やり直してから凍結する。
- 設計の変更（G4 の口の絞り込み、G6 の読み口の追加）に対するアーキテクチャゲートの再レビュー: 不要と判断した。どちらもゲートの提案をそのまま採った物で、口を減らす向きの変更である。Draft PR の外部計画レビューで見る。

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
