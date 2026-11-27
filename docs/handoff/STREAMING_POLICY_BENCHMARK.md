# Streaming の純政策ベンチマーク（S-9a、`STREAMING_POLICY_BENCHMARK`）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `A`（A1 初稿。A2 未実施、A3 未凍結。実装キューではない）
- 位置付け: program [SEASON_WORLD_DESIGN.md](SEASON_WORLD_DESIGN.md) §6 の S-9a。owner が 2026-11-27 に、S-9a だけを S-5・S-8a より前に出すと決めた。S-9b・S-9c は本スライスに入れない
- 版の経緯:
  - 2026-11-27 A1 初稿
- branch: `claude/s9a-policy-benchmark-handoff-qbq8fq`（`origin/develop` の `61b60e63` から切った計画ブランチ。PR の base は `develop`）
- implementation base commit: `61b60e635a59c35730f39ebac52a7482eb278f43`（2026-11-27 の `origin/develop` の head。§1 の現況と行数はこの commit で測った）
- implementation head commit: 未固定
- A3 凍結記録: 未凍結
- risk: `normal`（公開 API・asmdef・所有者・寿命・`SceneState` を変えない。Runtime の変更は `internal` の読み口 2 つだけ。§2 U1 を public にする裁定が出たら `high` に上げて A2 を足す）
- owner: root（依頼者）
- created: 2026-11-27
- expires: 2026-12-31、または置換 revision
- harvest to: `docs/streaming/STREAMING_CURRENT_SPEC.md` §6（測定記録: 対象日、対象 commit、ハードウェア、workload ごとの値と予算内 / 予算外）、`SEASON_WORLD_DESIGN.md` §6 の S-9a の行の状態と S-9c への入力、`unity/Assets/Docs/Architecture/21-scene-streaming.md` §9（計測の口の現況。Telemetry の不備のうち本スライスで直した物）
- Phase A snapshot の取得案内・状態: 未生成（A3 で凍結する時に作る。A1 の時点で Git 外の補助 file は無い）
- Phase B result snapshot の取得案内・状態: 未生成
- evidence bundle の取得案内・状態: 未生成
- C' blind bundle の取得案内・状態: 未生成
- 引継ぎ票:
  - 次の Phase・役割と作業: Phase A の A2（独立レビュー）。アーキテクチャゲート 1 件と目的レビュー 1 件。その前に owner が §2 の未決事項 U1〜U5 を見る（U2・U3 は A3 までに裁定が要る）。
  - 次の担当が読む範囲: 既定どおり全節。目的レビューの担当は §1 の「目的」の原文だけを読む。
  - 引き継ぐ commit: この票を含む commit。SHA は tree の外（起動指示、PR があれば状態ブロック）で渡す。
  - 必要な権限: A2 は読むだけ。Phase B は Unity Editor での compile 確認、Phase C は Unity EditMode テストの実行と、§2 の予算で指定したハードウェアが要る。Cloud では Unity を動かさない。
  - 停止規則: A3 の凍結の前に Phase B へ入らない。予算と workload manifest（§2）を凍結する前に、測定用の実行を 1 回もしない。
  - 引き渡さないもの: 目的レビューには §1 の「目的」の原文だけを渡す。

## 1. 目的と対象外

- 目的: Streaming の距離政策（`WorldStreamingController`）が、候補が 1,000 件と 10,000 件の時に 1 回の Tick でどれだけの時間と確保を使い、注視点の動きに対して何 Tick で収束するかを、Scene のロードの重さと切り離して測り、測定の前に決めた予算と比べた結果を、後続の最適化・撤退の判断（S-9c）が使える形で残す。あわせて、この測定に要る値のうち今の Telemetry・計測の口で取れない物を一覧にする。
- 利用者: なし（利用者の操作は無い。結果を読むのは S-9c の判断担当と owner）
- 対象外:
  - S-9b（実コンテンツ横断）と S-9c（撤退・最適化判断）。SceneDirector、`IAssetManagement`、Content Directory、Scene のロード、SampleGame の Play。
  - `WorldStreamingController` の Tick の中身の変更。空間索引、ロード済み identity 列の取得口、Jobs / Burst、確保を減らす書き換えは、予算外が出ても本スライスで行わない（§6 の S-9a の行）。
  - Telemetry の基盤（`TelemetryStartType`、payload 形、sink、DebugSocket、DebugStudio）への追加。
  - Player（IL2CPP を含む）での測定。本スライスの測定環境は Editor の EditMode テストだけである。
  - 他の 2 ラインの領域: `unity/Assets/Docs/Architecture/12-telemetry.md` と `05-scene.md`、`Runtime/SceneSystem`、`Runtime/Bootstrap`、`DebugSocketServices`、`tools/DebugStudio`（PR #216）、`Runtime/BuildContent`、`Editor/Build`、SampleGame の `DependOnAll`、`tools/Harness`（PR #214）。
- 現況（base `61b60e63` で測った）:
  - **政策の計算。** `WorldStreamingController.Tick`（`Runtime/Streaming/WorldStreamingController.cs`、301 行）は、Tick ごとに候補列を全件走査する。候補 1 件につき `Backend.IsLoaded` を 1 回呼び、注視点の数だけ XZ 距離を計算する。desired を距離の昇順に並べ、retain の外のロード済みと in-flight の Add に `RequestRemove` を出し、`MaxInFlight` の空きの分だけ `RequestAdd` を出す。
  - **Tick ごとの確保（コードの読解。測定ではない）。** Tick のたびに `List` 1 つと `HashSet` 4 つ（desired の列、retain、loaded、unload 対象、この Tick で発行した集合）を new している。候補数と desired の数に比例して伸びる。定常でも 0 にならない見込みである。
  - **時間の概念。** Controller は時刻を持たない。Tick の間隔は呼び出し側が決める。SampleGame では `SessionWorldStreamingDriver` が `UniTask.Delay` の loop で 0.2 秒ごとに呼ぶ。
  - **in-flight。** `_inFlightAddCells` / `_inFlightRemoveCells` は private で、件数を読む口が無い。`MaxInFlight` が制限するのは Add だけで、Remove は制限しない。retain の外へ出た in-flight の Add は、完了するまで枠を占める。
  - **既存の test。** `Tests/Streaming/` の Controller の test は `WorldStreamingControllerTests`（11 件）と `WorldStreamingControllerMultiFocusTests`（3 件）で、どれも小さい格子（数十候補）の正しさを見る。件数を変えて時間・確保を測る test は無い。
  - **今の FakeBackend と fixture で組める物・組めない物。**

    | 軸 | 今の口 | 組めるか |
    |---|---|---|
    | 候補 1,000 / 10,000 | `StreamingCandidateFixtures.DenseGrid(width, height)`。identity は `Cell_{x}_{y}` で重複しない | 組める（40×25、100×100） |
    | 単一 / 複数 Focus | `Tick(Vector3)` と `Tick(IReadOnlyList<Vector3>)` | 組める |
    | 静止 / 等速 / テレポート | 注視点は test が Tick ごとに渡す値 | 組める（Tick の番号の純関数にする） |
    | desired 疎 / 密 | `StreamingCandidateFixtures.Settings(loadRadius, unloadRadius, maxInFlight)` | 組める（半径で決める） |
    | Add の完了でロード済みになる | `FakeStreamingBackend` は完了してもロード済みにしない。test が `SetLoaded` を手で呼ぶ | **足りない** |
    | 完了の時点を時間で決める | `CompleteRequestAdd(id)` を test が 1 件ずつ呼ぶだけ。遅延の模型が無い | **足りない** |
    | `IsLoaded` の照会数 | 数えていない | **足りない** |
    | 長い実行での確保 | 呼び出し履歴が無制限の `List`、保留ごとに `UniTaskCompletionSource` を new | **足りない**（確保の測定に混ざる） |
    | 二重要求の検出 | 保留中の Add への再 Add だけ例外。ロード済みへの Add、Remove の重複、未ロードへの Remove は見ない | **足りない** |
    | 保留中の Add への Remove | Add と Remove を独立に扱い、関係の模型が無い | **足りない** |
    | 保留中の Remove の一覧 | 無い（Add は `PendingAddCellIds` がある） | **足りない** |

    `FakeStreamingBackend` は既存の test（FW の 3 file と SampleGame の 2 file）の前提なので変えない。足りない口は、測定専用の backend を新しく作って持つ（§3）。
- UC ごとの利用者の入力: なし

### Telemetry・計測の口の現況と不備

§6 の S-9a の行が求める値ごとに、base `61b60e63` の Telemetry と計測の口で取れるかを調べた。「Telemetry」は `AppTelemetry` へ record を書く経路、「計測の口」はそれ以外で値を読める API である。

| # | 値 | 今の口 | 取れるか | 扱い |
|---|---|---|---|---|
| D1 | Tick 時間 | Streaming の Tick を測る span も sample も無い（`TelemetryStartType` は 9 個で Streaming の名前が無い）。`ProfilerSummary` の `cpuMs` は frame 全体の値で、Streaming に帰属できない。UpdateSystem に system ごとの時間の計測は無い | 取れない | **本スライスで直す**（test 側の測定器。§3 の runner） |
| D2 | 1 Tick の GC allocation | Telemetry は `GcSpike`（frame ごとの Gen0 の回収回数の差分）と `ProfilerSummary` の managed heap の大きさだけ。確保した byte 数は出ない。test では `GC.GetAllocatedBytesForCurrentThread` の前例がある（`VfxSystemTickAllocationTests`） | Telemetry では取れない。test では取れる | **本スライスで直す**（test 側の測定器） |
| D3 | `IsLoaded` の照会数 | どこも数えていない | 取れない | **本スライスで直す**（測定用 backend が数える） |
| D4 | 収束までの時間（最終 `desired = resident`） | Controller は desired を公開しない。SampleGame の `GetResidentCellIdentities` は resident だけを返し、呼ぶたびに候補を全件 `IsLoaded` で再照合して配列を確保する | 取れない | **本スライスで直す**（test 側の独立した oracle と収束の判定） |
| D5 | in-flight 数 | Controller の集合は private。`FakeStreamingBackend.PendingAddCellIds` は backend から見た保留だけ | 取れない | **本スライスで直す**（Controller に `internal` の読み口 2 つ。§2 U1） |
| D6 | backlog、最古要求待ち時間、starvation | 無い | 取れない | **本スライスで直す**（test 側の oracle） |
| D7 | duplicate request、stale completion、cancel 後残留 | `FakeStreamingBackend` が保留中の Add への再 Add を例外にするだけ | 取れない | **本スライスで直す**（測定用 backend の計数と D5 の読み口） |
| T1 | 実行時に上の値を Telemetry として出す | §21 §9 の「Controller が定期 emit」は未実装。Streaming 用の `TelemetryStartType` も payload 形も無い | 取れない | **後続へ送る**（Telemetry の基盤側。§2「ここでは答えない問い」） |
| T2 | 実行時の Tick 時間の帰属 | UpdateSystem に system ごとの時間が無い。`UpdateLayerIds.Streaming` / `StreamingLayerOrder` の参照は定義 file の外に 0 件で、SampleGame の driver は UpdateSystem ではなく自前の loop で Tick する | 取れない | **後続へ送る** |
| T3 | 実行時の確保量 | frame ごとの確保 byte 数を出す record が無い | 取れない | **後続へ送る** |
| T4 | DebugStudio での Cell 状態の観測（§21 T-08） | resident の一覧は SampleGame の HUD 用の口だけ。desired・in-flight・backlog は外から見えない | 取れない | **後続へ送る** |
| T5 | Cell のロード時間、cancel・保留 unload の数（S-9b の値） | Cell の `SceneLoad` / `SceneUnload` span は Verbose で出る（`SceneDirectorStreamingBackend`）。cancel・保留 unload の件数を出す口は見当たらない。`Runtime/SceneSystem` は PR #216 が変更中なので、merge の後に測り直す | span は取れる。件数は取れない | **後続へ送る**（S-9b の Phase A） |

分け方の基準: 値を **test の中で** 読めれば S-9a の問いに答えられる物は本スライスで直す（D1〜D7。変更は `Tests/Streaming/` と `Runtime/Streaming/` の中に閉じる）。値を **実行時に** 外へ出すために Telemetry の契約（名前、payload 形、sink、wire、DebugStudio の表示）を増やす物は後続へ送る（T1〜T5。`12-telemetry.md` と DebugSocket は PR #216 の領域でもある）。この一覧は A1 の時点の物で、測定の中で見つかった不備は Phase B・C が §6・§7 に足す。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 今の managed 実装の `WorldStreamingController` は、候補 1,000 / 10,000 件で、測定の前に固定した control-plane の予算の中に収まるか。収まらない項目はどれか。
- 進める最低条件:
  - 不変条件（起きてはいけないこと）:
    - **I1 測定が測定対象を変えない。** 測った Controller は base の Controller と同じ計算をする。
      - 崩しうる経路: 測定のために `Tick` の中へ計数や時刻の取得を足す。測定用 backend の確保が Tick の確保に混ざる。完了の継続（`ObserveAddCompletionAsync` の `finally`）が Tick の時間に入ったり入らなかったりする。
      - 探索した境界: `Runtime/Streaming/` の 8 file と `Tests/Streaming/` の全 file。未確認: UniTask の `UniTaskVoid` の runner が pool から出るか new されるか（確保の値に出るので、測定で分かる）。
    - **I2 予算と workload を測定の後に変えない。** 予算の表と workload manifest は A3 で凍結した値のまま GO 候補 head に残る。反復を選んで捨てない。
      - 崩しうる経路: Phase B・C が結果を見てから §2 の表や workload の定数を書き換える。外れた反復を「暖機」として後から除く。ハードウェアを結果の良い機械へ変える。
    - **I3 測定器の誤りを結果にしない。** oracle は Controller の code を呼ばずに desired / retain を計算する。数え方の誤りは測定器の自己 test で落ちる。
      - 崩しうる経路: oracle が Controller の private な距離計算や並べ替えを流用する。二重要求の検出が、検出対象を 1 件も含まない入力でしか試されていない。時間・確保の統計が実測でしか試されず、既知の入力で検算されていない。
    - **I4 他のラインの領域と公開面を変えない。** 変更は §3 の file に限る。public な型・member、asmdef、`SceneState` を増やさない。
    - **I5 全 EditMode 回帰を重くも不安定にもしない。** 測定用の実行は通常の回帰では走らない。回帰で走る test に、実時間の閾値の assert を入れない。test で `Task.Delay` / `Thread.Sleep` を使わない。
  - 成立条件（起きなければならないこと）:
    - **S1** 下の 24 workload の全てについて、凍結した反復数の記録と workload manifest が残る。
    - **S2** workload ごとに、Tick 時間の中央値・p95・p99・最大、静かな Tick の確保量、`IsLoaded` の照会数、収束 Tick 数、in-flight の最大、backlog の最大、最古の待ち、starvation、duplicate request、stale completion、cancel 後残留が記録される。
    - **S3** 予算の表の全項目について、workload ごとに「予算内 / 予算外」が判定される。予算外は S-9c の入力として残り、本スライスでは直さない。
    - **S4** 件数で数える値（S2 のうち時間と確保以外）は、同じ workload の全ての反復で一致する。一致しなければ測定器の欠陥として扱う。
    - **S5** Telemetry・計測の口の不備の一覧（§1）が、測定で分かった事を足した形で残り、後続へ送る物に所有先が付く。
- 受け入れ条件（かっこ内は検証する条件）:
  - **AC1（I1）** implementation base から GO 候補 head までの `WorldStreamingController.cs` の差分が、`internal` の property 2 つの追加だけである。`Tick` と private method の行に差分が無い。不合格の例: `Tick` の中に counter の加算が 1 行入っている。
  - **AC2（I1）** Tick 時間と確保量は `controller.Tick(...)` の呼び出しの前後だけで測る。backend の完了の配送は測定区間の外（Tick と Tick の間）で行う。確保量の予算は「静かな Tick」（その Tick で要求を 1 件も出さず、直前の配送も 0 件の Tick）だけに適用し、それ以外の Tick の確保量は別の列に記録する。
  - **AC3（I2）** A3 凍結 commit と GO 候補 head の間で、本書 §2 の予算の表と workload の表に差分が無い。記録には全ての反復が入り、除くのは凍結した暖機の反復（各 workload の最初の 1 回）だけである。失敗経路: 反復数が凍結値と違う記録、暖機以外の反復が欠けた記録は不合格とし、測り直す。
  - **AC4（I3）** 測定器の自己 test が次を含み、通常の回帰で通る。(a) 統計: 既知の数列に対する中央値・p95・p99（nearest-rank）の検算。(b) 時間と確保の取得口に偽の値を注入した時の記録の検算。(c) 測定用 backend の模型: 遅延、Add の完了でロード済みになること、保留中の Add への Remove。(d) **失敗経路:** 二重の Add、未ロードへの Remove、近い候補を飛ばした発行、収束しない列を runner に直接与えると、duplicate・starvation・未収束がそれぞれ 0 でない値として記録される。
  - **AC5（I3）** oracle の code は `WorldStreamingController` とその private な計算を呼ばない（`StreamingCandidateSet` と `StreamingPolicySettings` の値だけを読む）。
  - **AC6（I4）** 変更 file が §3 の一覧と一致する。`pwsh tools/contract-audit.ps1` と `pwsh tools/docs-audit.ps1` が違反なし。
  - **AC7（I5）** 測定用の test は `[Explicit]` と category で通常の回帰から外れる。全 EditMode 回帰が通り、その中で 10,000 候補の測定は走らない。新しい test に `Task.Delay` / `Thread.Sleep` が無い（contract-audit）。
  - **AC8（S1・S2・S4）** 24 workload の記録が、凍結した形式（§5「結果の残し方」）で evidence に残る。件数の値が反復の間で一致する。
  - **AC9（S3）** 判定 C が、記録と予算の表を突き合わせた「予算内 / 予算外」の表を §7 に書く。時間の予算は、manifest のハードウェアが凍結した対象と一致する記録だけで判定する。一致しない記録は「参考」とし、判定に使わない。
  - **AC10（S5）** §1 の不備の一覧が GO 候補 head で更新されている。
- ここでは答えない問いと所有する後続スライス:
  - 実コンテンツ（SceneDirector、asset payload、Content Directory）を通した時間・メモリ・解放 → S-9b（`SEASON_WORLD_DESIGN.md` §6。着手時に HANDOFF を切る。y=4 の未昇格の確認が前提のまま）
  - 予算外の項目に対して何を実装するか（現状維持、空間索引、ロード済み identity 列の取得口、backend の比較、SceneDirector の撤退ライン）→ S-9c
  - 実行時に Streaming の値を Telemetry へ出す契約（T1〜T4: 名前、payload 形、DebugStudio の表示、system ごとの Tick 時間、確保量）→ §21 の T-08。S-9b の Phase A で、PR #216 の merge の後の Telemetry を基準に決める
  - SampleGame の driver が UpdateSystem ではなく自前の loop で Tick している事の扱い（T2）→ S-9b の Phase A
  - Player での control-plane の値 → S-9b
- スライス分割の判断: 契機を 1 つ検討した。§3 の新規 code は合計で約 1,400 行の見込みで、`SEASON_WORLD_DESIGN.md` §6 の「500 行または 3 責務を超える見込みなら別スライス / 別ブランチへ切る」に行数で当たる。**分割しない**案を A1 の案とする。理由: その規則が分けるよう求める 3 つの責務（測定器と純政策ベンチマーク、実コンテンツ計測、判断記録）のうち、本スライスは 1 つ目だけを持つ。測定器と workload は同じ 1 つの問いのためだけに変わり、測定器だけを先に merge しても使う物が無い。file は責務ごとに分け、どれも 300 行以下にする。Phase B の commit は「測定器と自己 test」「workload と測定用 test」の 2 つに分ける。規則の文言を行数だけで読むなら分割になるので、owner の裁定を U4 に置く。
- 判定定義:
  - **GO:** I1〜I5 と S1〜S5 を満たす。予算外の項目があっても GO である（予算外は S-9c の入力で、本スライスの失敗ではない）。
  - **NO-GO:** 測定器の自己 test が落ちる、記録が欠ける、件数の値が反復で一致しない、予算か workload が凍結後に変わっている、変更が §3 の外に出ている、のいずれか。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。予算外を見て Controller を直し始めたら停止する。
- A3 後の例外承認: なし
- 決定記録: なし
- 本文へ転記した実装制約:
  - Unity 側の `.cs` は先頭に `#nullable enable`。`record` を使わない。
  - test で `Task.Delay` / `Thread.Sleep` を使わない。workload の時間は Tick の番号で進める模擬時間とし、実時間を読むのは Tick 時間の取得口（注入）だけにする。
  - asmdef の参照を足さない。新しい test は既存の `OneStarMaker.Tests` に置く。
  - 公開 API のログ抽象は `ILogger<T>`。本スライスは公開面を増やさない。
  - repo 相対 path は 150 文字以内（`tools/path-budget.json`）。§3 の最長は 95 文字以下である。
  - Unity テストと Build は Phase C。Phase B は Editor 操作と compile 確認まで。Cloud で Unity CLI を動かさない。
  - SampleGame の所要時間（ロード時間、横断時間）を予算の根拠にしない。
- 未決事項:
  - **U1（owner または A2）in-flight の読み口の可視性。** A1 の案は `internal`（`OneStarMaker.Tests` へは既存の `InternalsVisibleTo` で見える）。public にすると SampleGame と後続の Telemetry が使えるが、公開 API の追加になり risk が `high` に上がる。読み口を足さない案（backend の保留数だけで代用）もある。その場合、Controller の集合に残った項目を直接は見られない。
  - **U2（owner。A3 までに必須）時間の予算の値と対象ハードウェア。** 下の B5 は案である。対象の機械（CPU、メモリ、OS、電源設定）を owner が指定する。Cloud からは測定機の構成が分からない。
  - **U3（owner。A3 までに必須）確保量の予算。** 下の B6 の案は「静かな Tick で 0 byte」。コードの読解では今の実装はこれを満たさない見込みで、その場合は予算外として S-9c へ渡る。0 ではない上限を置くなら値と根拠を owner が決める。
  - **U4（owner）分割。** 上の「スライス分割の判断」。
  - **U5（A2）保留中の Add への Remove の模型。** 下の「測定用 backend の模型」は 1 通りに固定した。実物の SceneDirector（キャンセル窓と保留 unload の両方がある）の再現は主張しない。模型を 2 通り測るかは A2 の指摘を見て決める。

### workload manifest（A3 で凍結する）

共通の値:

| 項目 | 値 | 根拠 |
|---|---|---|
| 候補の配置 | 均一格子。間隔 100、体積は `StreamingCandidateFixtures.CellVolume`。1,000 件 = 40×25、10,000 件 = 100×100 | 既存 fixture の既定値。格子は fixture だけが知る |
| `maxInFlight` | 2 | 現状仕様の値 |
| 疎 | `LoadRadius` 150 / `UnloadRadius` 250（間隔の 1.5 倍 / 2.5 倍。注視点 1 つの desired は約 9） | 既存 fixture の既定値 |
| 密 | `LoadRadius` 800 / `UnloadRadius` 1200（間隔の 8 倍 / 12 倍。注視点 1 つの desired は約 200） | desired が `maxInFlight` の 100 倍になり backlog が出る大きさ |
| Tick の間隔（模擬時間） | 0.2 秒。Tick 数を秒へ換算する時だけ使う | 現状仕様の Tick。値は Tick 数を正とする |
| backend の遅延 | Add は 3 Tick 後、Remove は 1 Tick 後に完了 | 模型の値。in-flight が上限に張り付く最小の構成。実物の所要時間ではない |
| 単一 Focus の始点 | 格子の幅の 1/4、奥行きの 1/2 | — |
| 複数 Focus（4 つ）の始点 | 幅の 1/8 と 3/8、奥行きの 1/4 と 3/4 の組み合わせ | 等速で動かしても格子の中に残る |
| 反復 | 各 workload 11 回。最初の 1 回は暖機として記録に残し、統計から除く | JIT と pool の初期化を除く |
| 終わり方 | 動きが止まった後、収束を確認してからさらに 50 Tick 回す（静かな Tick の標本） | — |
| 打ち切り | 止まった後 5,000 Tick で収束しなければ「未収束」と記録して終える | 無限 loop を作らない |

動きの 3 種類（全ての注視点が同じ動きをする）:

| 動き | 定義 |
|---|---|
| 静止 | Tick 0 から始点に居続ける |
| 等速 | +X 方向へ 1 Tick あたり 50（間隔の半分）を 40 Tick 進み、止まる |
| テレポート | 始点と、+X 方向へ 2,500 離れた点（密の `UnloadRadius` の 2 倍より遠い）を 2 Tick ごとに往復し、20 回跳んだ後、跳んだ先で止まる。2 Tick は Add の遅延より短く、ロードが完了する前に離れる |

workload は「候補数 2 × Focus 数 2 × 動き 3 × 密度 2」の 24 個。名前は `N{1k|10k}-F{1|4}-{still|linear|teleport}-{sparse|dense}`。

記録に添える manifest: 上の値、候補数、Focus 数、止まった時点の desired 数と resident 数（oracle が数える）、Unity の版、UniTask の版、Editor / EditMode である事、script backend（Mono）、対象ハードウェア（CPU、メモリ、OS）、対象 commit、A3 の revision、反復数、測定の日時。`SEASON_WORLD_DESIGN.md` §6 の manifest のうち contentSet・配信 bytes・quality tier・解像度・cache は S-9b の項目で、本スライスは「対象外（Scene をロードしない）」と書く。

### 測定用 backend の模型

- `RequestAdd(id)` は `現在の Tick + 3` に完了する。完了の時点でその identity はロード済みになる。
- `RequestRemove(id)` は `現在の Tick + 1` に完了し、その時点で未ロードになる。
- 保留中の Add に `RequestRemove` が来たら、Add は元の時点で完了してロード済みになり、Remove は「Add の完了と `現在の Tick + 1` の遅い方」に完了して未ロードになる（保留 unload の模型）。
- 完了は runner が Tick と Tick の間に、期限の順、同じ期限なら要求の順で配送する。
- 数える物: `IsLoaded` の呼び出し、Add / Remove の要求、duplicate（保留中かロード済みの identity への Add、保留中の Remove がある identity への Remove、未ロードで Add の保留も無い identity への Remove）、stale completion（Add の完了の時点で oracle の retain の外にある）。

### 測る値の定義

| 値 | 定義 |
|---|---|
| Tick 時間 | `controller.Tick` の呼び出しの前後の時刻の差。止まる前と後の全ての Tick を標本にし、暖機以外の反復をまとめて中央値・p95・p99・最大を出す（nearest-rank） |
| 静かな Tick の確保量 | 静かな Tick（AC2）での、呼び出しの前後の確保 byte 数の差。中央値と最大 |
| `IsLoaded` の照会数 | 1 Tick の呼び出し数の最小と最大 |
| 収束 Tick 数 | 動きが止まった Tick から、次を全て満たす最初の Tick まで: oracle の desired が全てロード済み、ロード済みが全て retain の中、backend の保留が 0、その次の Tick が要求を出さない |
| in-flight の最大 | backend の保留中の Add の最大と、Controller の読み口の最大 |
| backlog の最大 | 各 Tick の後の「desired のうち、未ロードで Add の保留も無い」件数の最大 |
| 最古の待ち | identity が desired に入り続けた Tick 数のうち、Add が出るまでと、ロード済みになるまでの最大 |
| starvation | ある Tick で Add が出た identity より近い desired が、未ロード・保留なしのまま同じ Tick で Add を出されなかった回数 |
| stale completion | 上の模型の計数と、その identity が未ロードへ戻るまでの Tick 数の最大 |
| cancel 後残留 | 終わりの 50 Tick の間の、retain の外のロード済みの数、backend の保留の数、Controller の読み口の値、出た要求の数 |

### 予算（A3 で凍結する。測定の後に変えない）

予算は、実装を測った結果からではなく、仕様と構成から導く。B1〜B4 は実装と機械に依らない。B5 と B6 は owner の裁定が要る（U2・U3）。

| # | 予算 | 導出 |
|---|---|---|
| B1 | `IsLoaded` の照会は 1 Tick に候補数以下 | 再照合（G-6）は候補 1 件につき 1 回で足りる |
| B2 | 保留中の Add は常に `maxInFlight` 以下 | `StreamingPolicySettings` の契約 |
| B3 | duplicate request = 0、starvation = 0、cancel 後残留の 4 つの値が全て 0 | 政策の仕様（§21 §8、§34 §4） |
| B4 | 収束 Tick 数 ≤ `ceil((止まった時点で未ロードの desired の数 + 枠を占めている desired 外の Add の数) / maxInFlight) × (Add の遅延 + 1) + Remove の遅延 + 2` | 1 回の波が `maxInFlight` 件を「遅延 + 再発行の 1 Tick」で運ぶ。最後の Remove と確認の Tick を足す |
| B5（案） | Tick 時間の p99: 1,000 候補で 0.5 ms 以下、10,000 候補で 2.0 ms 以下（対象ハードウェアの Editor、EditMode） | Tick は 1 frame の中で走る。60 fps の 1 frame（16.67 ms）の 3% と 12% を control plane の上限に置く案。割合は owner が決める |
| B6（案） | 静かな Tick の確保量は 0 byte | 毎 frame 呼ばれる Tick は確保しないという repo 内の前例（`VfxSystemTickAllocationTests`）に揃える案 |

stale completion の件数には予算を置かない（テレポートでは起きるのが正しい）。記録だけする。

## 3. 責務マップ

新規 file は全て `unity/Assets/OneStarMaker/Tests/Streaming/Benchmark/`（asmdef は既存の `OneStarMaker.Tests`。namespace は `OneStarMaker.Tests.Streaming.Benchmark`）。寿命は test の 1 件の中だけで、所有者は test である。`.meta` は Editor が作る。

| file | 責務 | 依存 | 公開面 | test 境界 | 行数（現在 → 見込み） |
|---|---|---|---|---|---|
| `StreamingBenchmarkBackend.cs` | 測定用の `ISceneStreamingBackend`。Tick 番号で完了する模型、ロード済みの集合、計数 | `ISceneStreamingBackend`、UniTask | `internal` | 模型の自己 test | 0 → 約 240 |
| `StreamingBenchmarkWorkload.cs` | workload の値（上の表）と 24 個の一覧、注視点の位置を Tick 番号から返す純関数 | `StreamingCandidateFixtures`、`StreamingPolicySettings` | `internal` | 位置の純関数の test | 0 → 約 180 |
| `StreamingBenchmarkOracle.cs` | Controller を呼ばずに desired / retain を計算し、backlog・待ち・starvation・収束を判定する | `StreamingCandidateSet`、`StreamingPolicySettings` | `internal` | 小さい格子での検算 | 0 → 約 160 |
| `StreamingBenchmarkRunner.cs` | 1 回の反復を回す。時刻と確保 byte 数の取得口を注入で受け、Tick の前後だけを測る。完了を Tick の間に配送する | 上の 3 つ、`WorldStreamingController` | `internal` | 偽の取得口を注入した検算 | 0 → 約 240 |
| `StreamingBenchmarkStatistics.cs` | nearest-rank の中央値・p95・p99・最大 | なし | `internal` | 既知の数列の検算 | 0 → 約 80 |
| `StreamingBenchmarkRecord.cs` | 結果と manifest を 1 行の JSON にする（`JsonUtility`、`[Serializable]` の class） | UnityEngine | `internal` | 往復の検算 | 0 → 約 140 |
| `StreamingBenchmarkInstrumentTests.cs` | 測定器の自己 test（AC4）。通常の回帰で走る | 上の全て | test | — | 0 → 約 280 |
| `StreamingPolicyBenchmarkTests.cs` | 24 workload の測定（`[Explicit]`、category `StreamingBenchmark`）と、小さい格子での記録の形の煙 test（通常の回帰） | 上の全て | test | — | 0 → 約 120 |
| `Runtime/Streaming/WorldStreamingController.cs` | 既存。`internal int InFlightAddCount` / `InFlightRemoveCount` を足す（集合の `Count` を返すだけ） | 変えない | 公開面は変えない | 既存の test と自己 test | 301 → 約 310 |

配置の理由: 測定器は Controller の利用者ではなく test の道具なので Runtime に置かない。`FakeStreamingBackend` に計数と遅延を足すと既存の test の前提が変わるので、別の型にする。時刻と確保の取得口を runner が注入で受けるのは、測定器そのものを実時間なしで検算するためである（I3）。oracle を runner と分けるのは、政策の計算を 2 つ目の実装として独立に保つためである。

行数の警報: 新規の合計は約 1,440 行で 500 行を超える。分割しない理由は §2「スライス分割の判断」。1 file に 3 つ以上の責務を持つ物は無い。

文書の変更: 本書、`SEASON_WORLD_DESIGN.md` §1・§6（順序の決定。A1 で反映済み）、`docs/README.md`（作業台の行。A1 で反映済み）。Phase D で harvest 先（§0）を更新する。

## 4. 実装計画

- 変更対象: §3 の file。
- 順序:
  1. `WorldStreamingController` に読み口 2 つを足す（U1 の裁定に従う。足さない裁定ならこの手順を飛ばし、cancel 後残留は backend の保留数で見る）。
  2. 統計、backend の模型、oracle、workload、記録、runner の順に作り、それぞれの自己 test を足す。ここまでを 1 つ目の commit にする。
  3. 24 workload の測定用 test と煙 test を足す。2 つ目の commit にする。
  4. Editor で compile を確認する。Unity テストは実行しない（Phase C）。
- Phase B から Phase A へ差し戻す条件: 測定に `Tick` の中身の変更が要ると分かった。asmdef の参照か public な member が要ると分かった。workload か予算の値を変えないと測れないと分かった。`[Explicit]` の test を `-Filter` で選べず、代わりの方法（§5）も成り立たない。
- 対象外を維持する方法: 変更 file の一覧を AC6 で照合する。予算外の値を見ても Controller を直さない（停止規則）。Telemetry の基盤側の不備は §1 の表に足すだけにする。

## 5. テストとレビュー計画

- 単体テスト: `StreamingBenchmarkInstrumentTests`（AC4 の (a)〜(d)、AC5 の検算）。
- 差し戻し中の起点 `-Filter`: `OneStarMaker.Tests.Streaming`（既存の Streaming の test と測定器の自己 test）。
- 判定必須テスト（GO 候補 head）:
  1. 全 EditMode 回帰: `pwsh tools/run-tests.ps1`
  2. 測定: `pwsh tools/run-tests.ps1 -Filter 'OneStarMaker.Tests.Streaming.Benchmark.StreamingPolicyBenchmarkTests'`（対象ハードウェアで、他の重い process を止めて 1 回）
- 全 EditMode 回帰の適用除外: なし
- 統合・Unity テスト: 上の 2 つ。PlayMode と Player は使わない。
- 操作・実行時・目視条件の検証経路: なし（全ての条件が EditMode の test と記録の照合で閉じる）。動作確認は `対象なし`。
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点:
  - **`[Explicit]` の test を `run-tests.ps1 -Filter` で選べるか: 未確認。初回確認は Phase C。** 理由: repo に `[Explicit]` の前例が無く、Cloud では Unity を動かせない。担当は Phase C の実行担当、確認地点は判定必須テスト 2 の最初の実行（XML に 24 件の実行が出る事）。不成立の時: `[Explicit]` をやめ、環境変数 `OSM_STREAMING_BENCHMARK=1` が無ければ `Assert.Ignore` する形へ B が直す（B 適応。条件と責務配置は変わらない）。
  - **`TestContext.Progress.WriteLine` の行が保存される出力に残るか: 未確認。初回確認は Phase C。** 前例は `AppLoggerTailFailureTests` にある。担当と確認地点は上と同じ（XML か runner の標準出力に `OSM_S9A_RESULT` の行が workload の数だけある事）。不成立の時: 同じ行を `Debug.Log` で Unity の log へ出す（B 適応）。
- 人間の判断が必要な条件: なし（予算と対象ハードウェアの裁定は A3 で済ませる）
- 高コスト証拠: なし（測定は数分の EditMode の実行で、やり直せる）
- 判定 C の照合 script の方式: 判定 C の offline 実行。判定 C の担当が、evidence の中の `OSM_S9A_RESULT` の行を読んで §2 の予算の表と比べる。人間が読む導出の規則は §2 の「測る値の定義」と「予算」である。script を書く場合、本体と機械向けの期待値は git に入れず、置き場所は `review-evidence.md` の「判定 C の照合 script」に従う。
- 結果の残し方:
  - **生の記録:** 測定用 test が workload 1 つにつき 1 行、`OSM_S9A_RESULT ` に続く JSON（manifest、反復ごとの値、統計）を `TestContext.Progress` へ書く。`run-tests.ps1` の出力（XML、log、標準出力）を evidence bundle に入れる。git には入れない。
  - **判定の表:** 判定 C が §7 に、workload × 予算の「予算内 / 予算外 / 参考」と主な値を書く。
  - **恒久の記録:** Phase D で `STREAMING_CURRENT_SPEC.md` §6 に、対象日・対象 commit・ハードウェア・workload ごとの値と判定を移す。予算外の項目は `SEASON_WORLD_DESIGN.md` §6 の S-9c の入力として書く。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`。
- A0/A1 主担当・モデル・ベンダー: Cloud の計画担当（owner の 2026-11-27 の指示により、文書にモデル・製品の割当を書かない）
- C' 用に予約した担当・モデル・ベンダー: 未定（A3 で、A・B・C に関与しない担当を予約する）
- 独立性の強化条件を満たせない場合の理由: 該当なし（risk は `normal`）

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
