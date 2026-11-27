# Streaming の純政策ベンチマーク（S-9a）の小計画（`STREAMING_POLICY_BENCHMARK`）

> type: program
> ステータス: **A0 の計画。実装キューではない。** S-9a を 6 つの小スライスに分ける（測定 4 本と、Telemetry の修正 2 本）。各スライスは着手時に `slice` の HANDOFF を切る。最初の 1 本は [STREAMING_BENCHMARK_BACKEND.md](STREAMING_BENCHMARK_BACKEND.md)（A3 統合案、未凍結）。
> 位置付け: [SEASON_WORLD_DESIGN.md](SEASON_WORLD_DESIGN.md) §6 の S-9a。S-9b・S-9c は含まない。
> owner: root（依頼者）。created: 2026-11-27。期限: 2026-12-31、または各スライスへの取り込み。
> harvest 先: `docs/streaming/STREAMING_CURRENT_SPEC.md` §6（測定記録）、`SEASON_WORLD_DESIGN.md` §6 の S-9a の行と S-9c への入力、`unity/Assets/Docs/Architecture/21-scene-streaming.md` §9（計測の口の現況）。全スライスの harvest の後に本書を `git rm` する。
> Phase C / C' の欄は持たない（各スライスの HANDOFF が持つ）。`docs-audit.ps1` 検査3 の対象にしない。

## 1. owner の決定

| 日付 | 決定 | 効果 |
|---|---|---|
| 2026-11-27 | S-9a だけを S-5・S-8a より前に出す。S-9b・S-9c は入れない | `SEASON_WORLD_DESIGN.md` §1・§6 に反映済み |
| 2026-11-27 | 測定の中で Telemetry の不備を一覧にする。Streaming の中に閉じる計測の口は S-9a で直し、Telemetry の基盤側は後続へ送る | §3 |
| 2026-11-27 | A1 初稿（S-9a を 1 スライスで行う案。新規 code 約 1,400 行）は大きい。もっと細かく切る | §4 の測定 4 スライス。初稿の「分割しない」案は捨てた |
| 2026-11-27 | 実行時の Telemetry の修正も S-9a の列に入れる（Controller の読み口と、Streaming の Telemetry の 2 本） | §4 の S-9a-5・S-9a-6。「後続（S-9b の Phase A）へ送る」だけの扱いをやめた |

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
| D5 | in-flight 数 | Controller の集合は private | 取れない | S-9a-1（backend から見た保留数で測る）。Controller の読み口は S-9a-5 |
| D6 | backlog、最古要求待ち時間、starvation | 無い | 取れない | S-9a-2 |
| D7 | duplicate request、stale completion、cancel 後残留 | 保留中の Add への再 Add を例外にするだけ | 取れない | duplicate は S-9a-1、stale と残留は S-9a-2 |
| T1 | 実行時に上の値を Telemetry として出す | §21 §9 の「Controller が定期 emit」は未実装。Streaming 用の名前も payload 形も無い | 取れない | S-9a-5（読み口）と S-9a-6（Telemetry） |
| T2 | 実行時の Tick 時間の帰属 | `UpdateLayerIds.Streaming` / `StreamingLayerOrder` の参照は定義 file の外に 0 件。SampleGame の driver は自前の loop で Tick する | 取れない | S-9a の外（下の「S-9a に入れない物」） |
| T3 | 実行時の確保量 | frame ごとの確保 byte 数を出す record が無い | 取れない | S-9a の外（同上） |
| T4 | DebugStudio での Cell 状態の観測（§21 T-08） | resident の一覧は SampleGame の HUD 用の口だけ | 取れない | S-9a-6 |
| T5 | Cell のロード時間、cancel・保留 unload の数（S-9b の値） | Cell の `SceneLoad` / `SceneUnload` span は Verbose で出る。件数を出す口は見当たらない。`Runtime/SceneSystem` は PR #216 が変更中 | span は取れる | S-9a の外（S-9b の Phase A で測り直す） |

- **測定のスライスで直す物（D1〜D7。S-9a-1〜4）:** 値を test の中で読めれば足りる。変更は `unity/Assets/OneStarMaker/Tests/Streaming/Benchmark/` に閉じる。**S-9a-1〜4 は Runtime の code を変えない。** 測る Controller は `develop` の物そのままである。
- **Telemetry のスライスで直す物（T1・T4 と D5 の Controller の読み口。S-9a-5・S-9a-6）:** 値を実行時に外へ出す。S-9a-5 は `Runtime/Streaming` の中に閉じる。S-9a-6 は Telemetry の基盤（名前、payload 形、wire、DebugStudio の表示）を増やすので、同じ領域を変更中の PR #216 の merge の後に着手する。
- **S-9a に入れない物（T2・T3・T5）:** system ごとの Tick 時間と frame ごとの確保量は UpdateSystem と Profiler の側の機能で、Streaming に閉じない。SampleGame の driver が UpdateSystem に登録されていない事の扱いも含めて、別の計画が要る（所有は未定。§6 P5）。T5 は S-9b の値で、S-9b の Phase A が持つ。
- 測定の中で見つかった不備は、各スライスがこの表に足す。

## 4. スライス列

1 スライス = 1 ブランチ = 1 HANDOFF。S-9a-1〜4 は test の code だけで、asmdef は既存の `OneStarMaker.Tests` を使う。行数は test を含む見込みである。

| # | 答える問い | 作る物 | 見込み | owner の裁定 | 前提 |
|---|---|---|---|---|---|
| **S-9a-1** 測定用 backend | 測定用 backend の模型は、定義どおりに完了・ロード済み・計数を返すか | `StreamingBenchmarkBackend`（Tick 番号で完了する模型、`IsLoaded` と要求の計数、duplicate の検出、保留の一覧）と自己 test | 約 400 行 | 不要 | なし |
| **S-9a-2** oracle と件数の runner | Controller を呼ばない oracle と runner は、小さい格子で収束・backlog・待ち・starvation・stale・残留を正しく数えるか | `StreamingBenchmarkOracle`、`StreamingBenchmarkRunner`（件数だけ。時間と確保は測らない）、既知の列を与える自己 test（失敗経路を含む） | 約 500 行 | 不要 | S-9a-1 |
| **S-9a-3** 件数の測定 | 今の Controller は、24 workload で件数の予算（B1〜B4）に収まるか | workload の一覧、注視点の位置の純関数、記録（1 行の JSON）、`[Explicit]` の測定用 test。判定の表 | 約 500 行 | 不要（B1〜B4 は機械に依らない） | S-9a-2 |
| **S-9a-4** 時間と確保の測定 | 今の Controller は、対象ハードウェアで時間と確保の予算（B5・B6）に収まるか | 時刻と確保 byte 数の取得口（注入）、nearest-rank の統計、その自己 test。同じ 24 workload を測る。確保量は全 Tick・要求を出した Tick・静かな Tick に分けた分布を記録し、backend の費用（completion source 等）を含む値と Controller だけに帰属する値を分ける。暖機、反復の単位、測定器自身の overhead、Editor と Profiler の条件を A3 で固定する | 約 300 行 | **必要**（B5 の値と対象ハードウェア、B6 の値。着手時の A3 まで） | S-9a-3 |
| **S-9a-5** Controller の読み口 | Controller の in-flight・desired・backlog の件数を、Tick の計算を変えずに外から読めるか | `WorldStreamingController` の読み口（どの値を出すかは S-9a-3 の測定で要ると分かった物に限る）と test | 未見積もり（着手時の A1） | **必要**（公開 API の追加。risk は `high`） | S-9a-3 |
| **S-9a-6** Streaming の Telemetry | 純政策の件数（Controller の読み口の値）を実行時に Telemetry として出し、DebugStudio で見られるか。§21 の T-08 のうち、emit と表示の基盤だけを先行する。実コンテンツの値（cancel、保留 unload、実ロード時間）と T-08 の完了の宣言は S-9b に残す | Streaming 用の `TelemetryStartType` と payload 形、定期 sample の emit、wire と DebugStudio の表示、`12-telemetry.md` / §28 の更新 | 未見積もり（着手時の A1） | **必要**（Telemetry の契約の追加） | S-9a-5 と、PR #216 の merge |

- 測定の切り方の理由: 1〜2 は測定器で、測定器が正しいかという問いは Controller の測定結果と独立に判定できる。3 は結果が模擬時間だけで決まり、どの機械で測っても同じ値になる。4 だけが機械と owner の裁定に依る。裁定を待つ間も 1〜3 は進められる。
- S-9a-3 の結果だけで S-9c の入力になる項目（照会数、収束、starvation、duplicate、残留）がある。S-9a-4 を待たずに harvest する。
- Telemetry の 2 本を測定の後ろに置く理由: 読み口に何を出すかは、件数の測定（S-9a-3）で実際に使った値から決める。先に公開面を決めると、使わない値まで契約になる。S-9a-5 は他の 2 ラインと重ならないので S-9a-3 の後すぐ着手でき、S-9a-4 と順序の依存は無い。S-9a-6 は S-9a-5 の読み口を使い、PR #216 と同じ file を触るので merge を待つ。着手時に、最新の `develop` と責務の境界を照合し直す。
- S-9a-5 が Controller に読み口を足した後に時間と確保を測る（S-9a-4 が後になる）場合、S-9a-4 は読み口の追加が Tick の時間と確保を変えていない事を、追加の前後の commit で測って確かめる。
- S-9a-2・S-9a-3・S-9a-4 への提言（PR #218 の外部計画再レビュー。各スライスの A1 の入力で、まだ決定ではない）:
  - **1 Tick の順序の案（S-9a-2）:** Tick k の注視点を確定 → `AdvanceTick`（期限の配送。k = 0 は初期状態）→ snapshot（配送の後、要求の前）→ `controller.Tick(focus[k])` → snapshot（要求の後）。stale completion はその Tick の確定した注視点の retain で判定する。正の遅延なら snapshot の差から発行した id は復元できるが、発行の順と priority の値は復元できない。距離順の集合の違反だけを判定するなら口の追加は要らない。順序そのものを判定するなら、件数の測定の時だけ有効な最小の記録を S-9a-2 で検討する。
  - **B4 の導き方の案（S-9a-2・S-9a-3）:** 最小の上界を狙わない。止まった時点の既存の Add と Remove の完了を最悪の遅延で上から押さえ、その後に Add が要りうる desired（未ロード、または Remove の保留中）を数えて `maxInFlight` の波で運ぶ、保守的な上界にする。止まった直後の 1 件の再進入、両方の保留、desired 外の Add で枠が満杯、要求が 0 の安定状態を、手計算の自己 test として固定する。式は採用した snapshot の順序に合わせて確定する。
  - **規模の比較（S-9a-3）:** 1,000 候補の格子（幅 4,000）では、複数 Focus の最大の x は 1,500 で、テレポート先は x = 4,000 の境界の上になる。10,000 候補では内側である。密の workload は desired / retain の数と境界での欠け方が 2 つの規模で違うので、候補数だけの比較にならない。24 workload は捨てず、Tick ごとの desired / retain / resident の数と移動の区間を記録する。候補数だけを比べる問いを立てる時は、同じ局所の配置・経路・desired / retain を持つ組（遠くの候補だけを増やす等）を別に選ぶ。今は workload を増やさない。
  - **時間と確保の測り方（S-9a-4）:** 引き算で測定器の費用を消した値を正本にしない。(A) 同じ backend を使った `controller.Tick` 全体の実測を正本にする。(B) backend だけを同じ条件で再生した値を診断として併記する（同じ候補、ロード済みと保留の状態、`IsLoaded` の呼び出しの列、要求の種別と順序。trace は測定の外の別の反復で取り、測定中に呼び出しごとの timer や履歴を持たない。Controller の await と継続の登録を含まない等の差を明記する）。(C) 測定器の空の区間の overhead を健全性の確認にする。p99(A) − p99(B) は Controller の p99 ではない。B が大きければ帰属を「分離不能」とし、A を「Controller と模型 backend の呼び出し経路」の値として扱う。Runtime に計測の hook を足さない。
  - **集計の区間（S-9a-4）:** 静止の開始直後のロード、移動・テレポート中、止まった後の収束、収束後の 50 Tick を別に集計する（全 Tick の p99 だけだと、密の長い収束が移動中の spike を薄める）。区間ごとに標本数、反復数、中央値・p95・p99・最大、要求数を残す。標本が少なくて p99 が最大と同じになる区間はそう明記し、反復で補う。確保は 1 Tick あたりの byte と、5 Hz での 1 秒あたりの平均 byte を分ける。対象の機械が指定される前に、実機での合格を主張しない。予算外は測定スライスの失敗ではなく S-9c への結果である。
- S-9a-2 の A1 が決める事（PR #218 の外部計画レビューの申し送り）: (1) 1 Tick の中の順序（注視点の設定、`AdvanceTick`、`controller.Tick`、oracle と前後の snapshot）と、「止まった時点」「収束した時点」の定義。stale completion を前の Tick の注視点と新しい注視点のどちらで判定するかもここで決まる。(2) どの id に要求が出たかを runner が知る方法（S-9a-1 の backend は履歴を持たない。正の遅延で Tick の前後の状態の差から復元するか、最小の観測の口を足す）。(3) 行って戻る・入り直す小さい列を自己 test に入れ、正しい Controller を不合格にしない事。
- duplicate が出た事そのものを FAIL と記録する。無効な反復を除いて成功の扱いにしない。
- S-9a-2 の runner は、duplicate が 1 件でも出た反復を、それ以降の件数の測定が無効な反復として記録する（S-9a-1 の A2 の指摘。duplicate に完了済みの task を返すので、Controller の in-flight の会計が実物とずれる）。
- 予算外が出ても、どのスライスも Controller の Tick の計算を直さない（§6 の S-9a の行）。
- Phase B は Editor での compile 確認まで、Unity テストは Phase C。Cloud では Unity を動かさない。

## 5. スライスをまたいで固定する定義

各スライスが着手時の A3 で凍結する。凍結するスライスを見出しに書く。A3 の前の値は案である。

### 5.1 測定用 backend の模型（S-9a-1 で凍結）

定義の正本は [STREAMING_BENCHMARK_BACKEND.md](STREAMING_BENCHMARK_BACKEND.md) §2 である。S-9a-1 の harvest の時に、凍結した定義を本節へ移す。

- 要求は Tick 番号だけで完了し、Add の完了でロード済みになる。保留中の Add への Remove は、Add の完了の後に未ロードへ戻す（保留 unload の模型）。
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
| 複数 Focus（4 つ）の始点 | 幅の 1/8 と 3/8、奥行きの 1/4 と 3/4 の組み合わせ | 注視点の中心は格子の中に残る（1,000 候補のテレポート先は x = 4,000 の境界の上。半径は格子の外へ出る） |
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
| 収束 Tick 数 | 動きが止まった Tick から、次を全て満たす最初の Tick まで: oracle の desired が全てロード済み、ロード済みが全て retain の中（`desired ⊆ resident ⊆ retain`。ヒステリシスがあるので `desired = resident` ではない）、backend の保留が 0、その次の Tick が要求を出さない |
| in-flight の最大 | backend の保留中の Add の最大 |
| backlog の最大 | 各 Tick の後の「desired のうち、未ロードで Add の保留も無い」件数の最大 |
| 最古の待ち | identity が desired に入り続けた Tick 数のうち、Add が出るまでと、ロード済みになるまでの最大 |
| starvation | 次の 2 つを分けて数える（定義は S-9a-2 の A3 で固定する）。距離順の違反: ある Tick で Add が出た identity より近い、発行できる desired（未ロードで、Add も Remove も保留していない）が、同じ Tick で Add を出されなかった回数。長い未発行: 発行できる desired が Add を出されないまま続いた Tick 数の最大 |
| stale completion | Add の完了の時点で oracle の retain の外にある件数と、未ロードへ戻るまでの Tick 数の最大 |
| cancel 後残留 | 終わりの 50 Tick の間の、retain の外のロード済みの数、backend の保留の数、出た要求の数 |
| Tick 時間 | `controller.Tick` の呼び出しの前後の時刻の差。中央値・p95・p99・最大（nearest-rank） |
| Tick の確保量 | 呼び出しの前後の確保 byte 数の差。全 Tick、要求を出した Tick、静かな Tick（要求を 1 件も出さず直前の配送も 0 件）に分けた分布 |

### 5.4 予算（測定の前に凍結し、後から変えない）

予算は、実装を測った結果からではなく、仕様と構成から導く。

| # | 予算 | 導出 | 凍結 |
|---|---|---|---|
| B1 | `IsLoaded` の照会は 1 Tick に候補数以下 | 再照合（G-6）は候補 1 件につき 1 回で足りる | S-9a-3 |
| B2 | 保留中の Add は常に `maxInFlight` 以下 | `StreamingPolicySettings` の契約 | S-9a-3 |
| B3 | duplicate request = 0、starvation（距離順の違反）= 0、cancel 後残留の値が全て 0 | 政策の仕様（§21 §8、§34 §4）。duplicate = 0 はこの模型での条件で、実物の backend に一般化しない | S-9a-3 |
| B4 | 収束 Tick 数 ≤ `ceil((止まった時点で未ロードの desired の数 + 枠を占めている desired 外の Add の数) / maxInFlight) × (Add の遅延 + 1) + Remove の遅延 + 2` | 1 回の波が `maxInFlight` 件を「遅延 + 再発行の 1 Tick」で運ぶ。**この式は案で、足りない場合がある:** desired に戻った identity に Remove の保留が残っていると、Controller は Remove の完了まで Add を出さない。止まった時点の snapshot がロード済み・Remove 保留中の 1 件だけなら、式は 0 波と数えてしまう。S-9a-2 で観測の順序を決めた後、その状態が起きない理由を示すか、Remove の待ちと再 Add を含む上界に直す（§6 P6） | S-9a-3 |
| B5（案） | Tick 時間の p99: 1,000 候補で 0.5 ms 以下、10,000 候補で 2.0 ms 以下 | 60 fps の 1 frame の 3% と 12% を上限に置く案。割合と対象ハードウェアは owner が決める | S-9a-4 |
| B6（案） | 静かな Tick の確保量は 0 byte。5 Hz で走る処理なので、1 frame の瞬間の予算（B5）と平均の負荷は分けて扱う | 毎 frame 呼ばれる Tick は確保しないという repo 内の前例に揃える案。コードの読解では今の実装は満たさない見込み | S-9a-4 |

stale completion の件数には予算を置かない（テレポートでは起きるのが正しい）。

## 6. 未決事項

| # | 論点 | 決める時期 |
|---|---|---|
| P1 | B5 の値と対象ハードウェア（CPU、メモリ、OS、電源設定）。Cloud からは測定機の構成が分からない | owner。S-9a-4 の A3 まで |
| P2 | B6 の値（0 byte か、0 でない上限か） | owner。S-9a-4 の A3 まで |
| P3 | **解決済み:** 保留中の Add への Remove の模型は 1 通り（保留 unload）で足りる。取消の模型が要るなら S-9b の Phase A で足す | S-9a-1 の A2 |
| P6 | B4 の式の導出（Remove の保留が残る再進入を含むか）と、観測の時系列 | S-9a-2 の A1〜A3。B4 の凍結は S-9a-3 |
| P5 | system ごとの Tick 時間と frame ごとの確保量（T2・T3）、SampleGame の driver が UpdateSystem に登録されていない事を、どの計画が持つか。外部計画再レビューの推奨は「Streaming に固有でないので、UpdateSystem / Profiler の独立した計画で持つ。driver の接続の仕方もそこで判断する。S-9a-6 の完了条件に混ぜない」 | owner。S-9a-6 の着手まで |
| P4 | `[Explicit]` の test を `run-tests.ps1 -Filter` で選べるか、`TestContext.Progress` の行が保存される出力に残るか | S-9a-3 の Phase C で初回確認 |

## 7. 触らない場所

他の 2 ラインの領域: `unity/Assets/Docs/Architecture/12-telemetry.md` と `05-scene.md`、`Runtime/SceneSystem`、`Runtime/Bootstrap`、`DebugSocketServices`、`tools/DebugStudio`（PR #216）、`Runtime/BuildContent`、`Editor/Build`、SampleGame の `DependOnAll`、`tools/Harness`（PR #214）。S-9a-1〜5 はこれらに触れない。S-9a-6 だけが Telemetry の基盤と `12-telemetry.md` に触れるので、PR #216 の merge の後に着手する。
