# Streaming の純政策ベンチマーク（S-9a）の小計画（`STREAMING_POLICY_BENCHMARK`）

> type: program
> ステータス: **A0 の計画。実装キューではない。** S-9a を 4 つの小スライスに分ける。各スライスは着手時に `slice` の HANDOFF を切る。最初の 1 本は [STREAMING_BENCHMARK_BACKEND.md](STREAMING_BENCHMARK_BACKEND.md)（A1 初稿）。
> 位置付け: [SEASON_WORLD_DESIGN.md](SEASON_WORLD_DESIGN.md) §6 の S-9a。S-9b・S-9c は含まない。
> owner: root（依頼者）。created: 2026-11-27。期限: 2026-12-31、または各スライスへの取り込み。
> harvest 先: `docs/streaming/STREAMING_CURRENT_SPEC.md` §6（測定記録）、`SEASON_WORLD_DESIGN.md` §6 の S-9a の行と S-9c への入力、`unity/Assets/Docs/Architecture/21-scene-streaming.md` §9（計測の口の現況）。全スライスの harvest の後に本書を `git rm` する。
> Phase C / C' の欄は持たない（各スライスの HANDOFF が持つ）。`docs-audit.ps1` 検査3 の対象にしない。

## 1. owner の決定

| 日付 | 決定 | 効果 |
|---|---|---|
| 2026-11-27 | S-9a だけを S-5・S-8a より前に出す。S-9b・S-9c は入れない | `SEASON_WORLD_DESIGN.md` §1・§6 に反映済み |
| 2026-11-27 | 測定の中で Telemetry の不備を一覧にする。Streaming の中に閉じる計測の口は S-9a で直し、Telemetry の基盤側は後続へ送る | §3 |
| 2026-11-27 | A1 初稿（S-9a を 1 スライスで行う案。新規 code 約 1,400 行）は大きい。もっと細かく切る | §4 の 4 スライス。初稿の「分割しない」案は捨てた |

文書にモデル・製品の担当割当を書かない（owner 指示）。

## 2. 現況（base `61b60e63`、2026-11-27 の `origin/develop`）

- **政策の計算。** `WorldStreamingController.Tick`（`Runtime/Streaming/WorldStreamingController.cs`、301 行）は、Tick ごとに候補列を全件走査する。候補 1 件につき `Backend.IsLoaded` を 1 回呼び、注視点の数だけ XZ 距離を計算する。desired を距離の昇順に並べ、retain の外のロード済みと in-flight の Add に `RequestRemove` を出し、`MaxInFlight` の空きの分だけ `RequestAdd` を出す。
- **Tick ごとの確保（コードの読解。測定ではない）。** Tick のたびに `List` 1 つと `HashSet` 4 つを new している。定常でも 0 にならない見込みである。
- **時間の概念。** Controller は時刻を持たない。Tick の間隔は呼び出し側が決める。SampleGame では `SessionWorldStreamingDriver` が `UniTask.Delay` の loop で 0.2 秒ごとに呼ぶ。
- **in-flight。** `_inFlightAddCells` / `_inFlightRemoveCells` は private で、件数を読む口が無い。`MaxInFlight` が制限するのは Add だけである。retain の外へ出た in-flight の Add は、完了するまで枠を占める。
- **既存の test。** Controller の test は `WorldStreamingControllerTests`（11 件）と `WorldStreamingControllerMultiFocusTests`（3 件）で、数十候補の格子の正しさを見る。件数を変えて時間・確保を測る test は無い。
- **今の FakeBackend と fixture で組める物・組めない物。**

  | 軸 | 今の口 | 組めるか |
  |---|---|---|
  | 候補 1,000 / 10,000 | `StreamingCandidateFixtures.DenseGrid(width, height)`。identity は重複しない | 組める（40×25、100×100） |
  | 単一 / 複数 Focus | `Tick(Vector3)` と `Tick(IReadOnlyList<Vector3>)` | 組める |
  | 静止 / 等速 / テレポート | 注視点は test が Tick ごとに渡す値 | 組める（Tick の番号の純関数にする） |
  | desired 疎 / 密 | `StreamingCandidateFixtures.Settings(...)` の半径 | 組める |
  | Add の完了でロード済みになる | 完了してもロード済みにしない。test が `SetLoaded` を手で呼ぶ | **足りない** |
  | 完了の時点を時間で決める | `CompleteRequestAdd(id)` を 1 件ずつ呼ぶだけ | **足りない** |
  | `IsLoaded` の照会数 | 数えていない | **足りない** |
  | 長い実行での確保 | 履歴が無制限の `List`、保留ごとに `UniTaskCompletionSource` を new | **足りない** |
  | 二重要求の検出 | 保留中の Add への再 Add だけ例外 | **足りない** |
  | 保留中の Add への Remove | 関係の模型が無い | **足りない** |
  | 保留中の Remove の一覧 | 無い | **足りない** |

  `FakeStreamingBackend` は既存の test（FW の 3 file と SampleGame の 2 file）の前提なので変えない。足りない口は測定専用の backend が持つ（S-9a-1）。

## 3. Telemetry・計測の口の不備

§6 の S-9a の行が求める値ごとに、今の Telemetry（`AppTelemetry` へ record を書く経路）と計測の口（それ以外で値を読める API）で取れるかを調べた。

| # | 値 | 今の口 | 取れるか | 扱い |
|---|---|---|---|---|
| D1 | Tick 時間 | Streaming の Tick を測る span も sample も無い（`TelemetryStartType` は 9 個で Streaming の名前が無い）。`ProfilerSummary` の `cpuMs` は frame 全体。UpdateSystem に system ごとの時間は無い | 取れない | S-9a-4（test 側の測定器） |
| D2 | 1 Tick の GC allocation | Telemetry は `GcSpike`（Gen0 の回収回数の差分）と managed heap の大きさだけ。test では `GC.GetAllocatedBytesForCurrentThread` の前例がある（`VfxSystemTickAllocationTests`） | Telemetry では取れない | S-9a-4（test 側の測定器） |
| D3 | `IsLoaded` の照会数 | どこも数えていない | 取れない | S-9a-1（測定用 backend） |
| D4 | 収束までの時間 | Controller は desired を公開しない。SampleGame の `GetResidentCellIdentities` は resident だけで、呼ぶたびに全件を再照合して配列を確保する | 取れない | S-9a-2（test 側の oracle） |
| D5 | in-flight 数 | Controller の集合は private | 取れない | S-9a-1（backend から見た保留数で測る）。Controller の読み口は後続へ送る |
| D6 | backlog、最古要求待ち時間、starvation | 無い | 取れない | S-9a-2 |
| D7 | duplicate request、stale completion、cancel 後残留 | 保留中の Add への再 Add を例外にするだけ | 取れない | duplicate は S-9a-1、stale と残留は S-9a-2 |
| T1 | 実行時に上の値を Telemetry として出す | §21 §9 の「Controller が定期 emit」は未実装。Streaming 用の名前も payload 形も無い | 取れない | 後続（Telemetry の基盤側） |
| T2 | 実行時の Tick 時間の帰属 | `UpdateLayerIds.Streaming` / `StreamingLayerOrder` の参照は定義 file の外に 0 件。SampleGame の driver は自前の loop で Tick する | 取れない | 後続 |
| T3 | 実行時の確保量 | frame ごとの確保 byte 数を出す record が無い | 取れない | 後続 |
| T4 | DebugStudio での Cell 状態の観測（§21 T-08） | resident の一覧は SampleGame の HUD 用の口だけ | 取れない | 後続 |
| T5 | Cell のロード時間、cancel・保留 unload の数（S-9b の値） | Cell の `SceneLoad` / `SceneUnload` span は Verbose で出る。件数を出す口は見当たらない。`Runtime/SceneSystem` は PR #216 が変更中 | span は取れる | 後続（S-9b の Phase A で測り直す） |

- **S-9a で直す物（D1〜D7）:** 値を test の中で読めれば足りる。変更は `unity/Assets/OneStarMaker/Tests/Streaming/Benchmark/` に閉じる。**Runtime の code は S-9a のどのスライスでも変えない。** 測る Controller は `develop` の物そのままである。
- **後続へ送る物（T1〜T5 と、D5 の Controller の読み口）:** 値を実行時に外へ出すには Telemetry の契約（名前、payload 形、sink、wire、DebugStudio の表示）か Controller の公開面を増やす必要がある。所有は §21 の T-08 で、S-9b の Phase A が PR #216 の merge の後の Telemetry を基準に決める。
- 測定の中で見つかった不備は、各スライスがこの表に足す。

## 4. スライス列

1 スライス = 1 ブランチ = 1 HANDOFF。どれも test の code だけで、asmdef は既存の `OneStarMaker.Tests` を使う。行数は test を含む見込みである。

| # | 答える問い | 作る物 | 見込み | owner の裁定 | 前提 |
|---|---|---|---|---|---|
| **S-9a-1** 測定用 backend | 測定用 backend の模型は、定義どおりに完了・ロード済み・計数を返すか | `StreamingBenchmarkBackend`（Tick 番号で完了する模型、`IsLoaded` と要求の計数、duplicate の検出、保留の一覧）と自己 test | 約 400 行 | 不要 | なし |
| **S-9a-2** oracle と件数の runner | Controller を呼ばない oracle と runner は、小さい格子で収束・backlog・待ち・starvation・stale・残留を正しく数えるか | `StreamingBenchmarkOracle`、`StreamingBenchmarkRunner`（件数だけ。時間と確保は測らない）、既知の列を与える自己 test（失敗経路を含む） | 約 500 行 | 不要 | S-9a-1 |
| **S-9a-3** 件数の測定 | 今の Controller は、24 workload で件数の予算（B1〜B4）に収まるか | workload の一覧、注視点の位置の純関数、記録（1 行の JSON）、`[Explicit]` の測定用 test。判定の表 | 約 500 行 | 不要（B1〜B4 は機械に依らない） | S-9a-2 |
| **S-9a-4** 時間と確保の測定 | 今の Controller は、対象ハードウェアで時間と確保の予算（B5・B6）に収まるか | 時刻と確保 byte 数の取得口（注入）、nearest-rank の統計、その自己 test。同じ 24 workload を測る | 約 300 行 | **必要**（B5 の値と対象ハードウェア、B6 の値。着手時の A3 まで） | S-9a-3 |

- 切り方の理由: 1〜2 は測定器で、測定器が正しいかという問いは Controller の測定結果と独立に判定できる。3 は結果が模擬時間だけで決まり、どの機械で測っても同じ値になる。4 だけが機械と owner の裁定に依る。裁定を待つ間も 1〜3 は進められる。
- S-9a-3 の結果だけで S-9c の入力になる項目（照会数、収束、starvation、duplicate、残留）がある。S-9a-4 を待たずに harvest する。
- 予算外が出ても、どのスライスも Controller を直さない（§6 の S-9a の行）。
- Phase B は Editor での compile 確認まで、Unity テストは Phase C。Cloud では Unity を動かさない。

## 5. スライスをまたいで固定する定義

各スライスが着手時の A3 で凍結する。凍結するスライスを見出しに書く。A3 の前の値は案である。

### 5.1 測定用 backend の模型（S-9a-1 で凍結）

- `RequestAdd(id)` は `現在の Tick + Add の遅延` に完了する。完了の時点でその identity はロード済みになる。
- `RequestRemove(id)` は `現在の Tick + Remove の遅延` に完了し、その時点で未ロードになる。
- 保留中の Add に `RequestRemove` が来たら、Add は元の時点で完了してロード済みになり、Remove は「Add の完了と `現在の Tick + Remove の遅延` の遅い方」に完了して未ロードになる（保留 unload の模型）。
- 完了は呼び出し側が Tick と Tick の間に進める。配送は期限の順、同じ期限なら要求の順。
- duplicate: 保留中かロード済みの identity への Add、保留中の Remove がある identity への Remove、未ロードで Add の保留も無い identity への Remove。
- 実物の SceneDirector（キャンセル窓と保留 unload の両方がある）の再現は主張しない。実物は S-9b が測る。

### 5.2 workload manifest（S-9a-3 で凍結）

| 項目 | 値 | 根拠 |
|---|---|---|
| 候補の配置 | 均一格子。間隔 100、体積は `StreamingCandidateFixtures.CellVolume`。1,000 件 = 40×25、10,000 件 = 100×100 | 既存 fixture の既定値 |
| `maxInFlight` | 2 | 現状仕様の値 |
| 疎 | `LoadRadius` 150 / `UnloadRadius` 250（注視点 1 つの desired は約 9） | 既存 fixture の既定値 |
| 密 | `LoadRadius` 800 / `UnloadRadius` 1200（注視点 1 つの desired は約 200） | desired が `maxInFlight` の 100 倍になり backlog が出る |
| Tick の間隔（模擬時間） | 0.2 秒。Tick 数を秒へ換算する時だけ使う | 現状仕様の Tick |
| backend の遅延 | Add は 3 Tick、Remove は 1 Tick | 模型の値。実物の所要時間ではない |
| 単一 Focus の始点 | 格子の幅の 1/4、奥行きの 1/2 | — |
| 複数 Focus（4 つ）の始点 | 幅の 1/8 と 3/8、奥行きの 1/4 と 3/4 の組み合わせ | 動かしても格子の中に残る |
| 静止 | Tick 0 から始点に居続ける | — |
| 等速 | +X 方向へ 1 Tick あたり 50 を 40 Tick 進み、止まる | — |
| テレポート | 始点と、+X 方向へ 2,500 離れた点を 2 Tick ごとに往復し、20 回跳んだ後、跳んだ先で止まる | Add の遅延より短い間隔で離れる |
| 終わり方 | 止まった後、収束を確認してからさらに 50 Tick 回す。5,000 Tick で収束しなければ「未収束」と記録する | — |

workload は「候補数 2 × Focus 数 2 × 動き 3 × 密度 2」の 24 個。名前は `N{1k|10k}-F{1|4}-{still|linear|teleport}-{sparse|dense}`。SampleGame の所要時間（ロード時間、横断時間）を値の根拠にしない。

記録に添える manifest: 上の値、止まった時点の desired 数と resident 数、Unity と UniTask の版、Editor / EditMode である事、対象 commit、A3 の revision。S-9a-4 はこれに対象ハードウェア、反復数、測定の日時を足す。`SEASON_WORLD_DESIGN.md` §6 の manifest のうち contentSet・配信 bytes・quality tier・解像度・cache は S-9b の項目である。

### 5.3 測る値（件数は S-9a-2、時間と確保は S-9a-4 で凍結）

| 値 | 定義 |
|---|---|
| `IsLoaded` の照会数 | 1 Tick の呼び出し数の最小と最大 |
| 収束 Tick 数 | 動きが止まった Tick から、次を全て満たす最初の Tick まで: oracle の desired が全てロード済み、ロード済みが全て retain の中、backend の保留が 0、その次の Tick が要求を出さない |
| in-flight の最大 | backend の保留中の Add の最大 |
| backlog の最大 | 各 Tick の後の「desired のうち、未ロードで Add の保留も無い」件数の最大 |
| 最古の待ち | identity が desired に入り続けた Tick 数のうち、Add が出るまでと、ロード済みになるまでの最大 |
| starvation | ある Tick で Add が出た identity より近い desired が、未ロード・保留なしのまま同じ Tick で Add を出されなかった回数 |
| stale completion | Add の完了の時点で oracle の retain の外にある件数と、未ロードへ戻るまでの Tick 数の最大 |
| cancel 後残留 | 終わりの 50 Tick の間の、retain の外のロード済みの数、backend の保留の数、出た要求の数 |
| Tick 時間 | `controller.Tick` の呼び出しの前後の時刻の差。中央値・p95・p99・最大（nearest-rank） |
| 静かな Tick の確保量 | 要求を 1 件も出さず直前の配送も 0 件の Tick での、呼び出しの前後の確保 byte 数の差 |

### 5.4 予算（測定の前に凍結し、後から変えない）

予算は、実装を測った結果からではなく、仕様と構成から導く。

| # | 予算 | 導出 | 凍結 |
|---|---|---|---|
| B1 | `IsLoaded` の照会は 1 Tick に候補数以下 | 再照合（G-6）は候補 1 件につき 1 回で足りる | S-9a-3 |
| B2 | 保留中の Add は常に `maxInFlight` 以下 | `StreamingPolicySettings` の契約 | S-9a-3 |
| B3 | duplicate request = 0、starvation = 0、cancel 後残留の値が全て 0 | 政策の仕様（§21 §8、§34 §4） | S-9a-3 |
| B4 | 収束 Tick 数 ≤ `ceil((止まった時点で未ロードの desired の数 + 枠を占めている desired 外の Add の数) / maxInFlight) × (Add の遅延 + 1) + Remove の遅延 + 2` | 1 回の波が `maxInFlight` 件を「遅延 + 再発行の 1 Tick」で運ぶ | S-9a-3 |
| B5（案） | Tick 時間の p99: 1,000 候補で 0.5 ms 以下、10,000 候補で 2.0 ms 以下 | 60 fps の 1 frame の 3% と 12% を上限に置く案。割合と対象ハードウェアは owner が決める | S-9a-4 |
| B6（案） | 静かな Tick の確保量は 0 byte | 毎 frame 呼ばれる Tick は確保しないという repo 内の前例に揃える案。コードの読解では今の実装は満たさない見込み | S-9a-4 |

stale completion の件数には予算を置かない（テレポートでは起きるのが正しい）。

## 6. 未決事項

| # | 論点 | 決める時期 |
|---|---|---|
| P1 | B5 の値と対象ハードウェア（CPU、メモリ、OS、電源設定）。Cloud からは測定機の構成が分からない | owner。S-9a-4 の A3 まで |
| P2 | B6 の値（0 byte か、0 でない上限か） | owner。S-9a-4 の A3 まで |
| P3 | 保留中の Add への Remove の模型を 1 通りで足りるとするか | S-9a-1 の A2 |
| P4 | `[Explicit]` の test を `run-tests.ps1 -Filter` で選べるか、`TestContext.Progress` の行が保存される出力に残るか | S-9a-3 の Phase C で初回確認 |

## 7. 触らない場所

他の 2 ラインの領域: `unity/Assets/Docs/Architecture/12-telemetry.md` と `05-scene.md`、`Runtime/SceneSystem`、`Runtime/Bootstrap`、`DebugSocketServices`、`tools/DebugStudio`（PR #216）、`Runtime/BuildContent`、`Editor/Build`、SampleGame の `DependOnAll`、`tools/Harness`（PR #214）。S-9a は `Runtime/Streaming` も変えない。
