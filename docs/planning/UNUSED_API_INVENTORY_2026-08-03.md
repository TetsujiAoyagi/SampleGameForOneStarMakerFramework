# 未使用 API 棚卸し — 判定表

> ステータス: **判定待ち**（判定欄は空欄。削除 / 接続 / 保留の記入は作者が行う）
> 最終検証日: 2026-08-03
> 正本か否か: **正本ではない**。各 API の設計意図の正本は「由来 doc」列の文書
> 対象断面: `develop` @ 5f4552d + 未コミットの telemetry contract v3 作業
> 関連: [00-subsystem-design-audit-2026-08-03.md](../reference/00-subsystem-design-audit-2026-08-03.md)（監査本体）

---

## 0. この表の使い方 — 読む前に

**「参照 0 = 消してよい」ではない。** 棚卸しの過程で分かった最も重要なことは、未使用 API の多くが**事故ではなく明示的な設計判断の結果**だということだった。

由来 doc を逆引きすると、少なくとも 3 つの性格に分かれる:

| 性格 | 意味 | 例 |
|---|---|---|
| **A. 意図的な先行宣言** | doc が「契約のみ定義し実装しない」と明記している | `IServiceResolver`、`UpdateLayerIds.Streaming` |
| **B. フェーズ外に送られた実装** | doc が「本フェーズ外 / 禁止 / 対象外」と明記している | `VolumeCrossfade`、`CameraSystemSliceSetup` |
| **C. 置き換え済みの残骸** | 後継が入って本来消えるはずだったもの | snapshot 系 API、DebugStudio 旧クライアント経路 |

**C だけが単純な削除候補。** A は消すと契約が消え、B は消すと将来計画が消える。特に native/job 系は [Q5-1「Update 集約は Job/Burst の足がかりになるか」](../reference/00-questions-we-are-answering-2026-07-11.md)（ステータス 🔬 = 仮説）の実装なので、**削除は仮説の撤回を意味する**。単なるデッドコード削除とは重みが違う。

判定欄には `削除` / `接続` / `保留` のいずれかと、理由を 1 行。

---

## 1. Unity 側

### 1.1 UpdateSystem

| # | API | 場所 | 行数 | prod | test | 性格 | 由来 doc | 削除コスト | 接続コスト | 判定 |
|:---:|---|---|---:|:---:|:---:|:---:|---|---|---|---|
| U-1 | `UpdateBehaviourAdapter` | `Runtime/UpdateSystem/Adapters/UpdateBehaviourAdapter.cs` | 70 | **0** | **0** | A/C | `UPDATER_CURRENT_SPEC.md:50`（構成要素として記載）**⇔** `CAMERA_SYSTEM_UPDATESYSTEM_INTEGRATION_PLAN_2026-07-13.md:419`「本プロジェクト方針では**非推奨**。純 C# Register を維持」 | 低（誰も呼んでいない） | 中（シーン配置型登録の用途を作る必要） | |
| U-2 | `UpdateCoordinator.RegisterNative` ×2 / `RegisterNativePipeline` | `Foundation/UpdateSystem/World/UpdateCoordinator.cs:128,161,187` | — | **0** | 1 | B | `UPDATER_CURRENT_SPEC.md:140-141`（正本 API として記載）、[Q5-1 🔬](../reference/00-questions-we-are-answering-2026-07-11.md) | **高** — 下記 U-3 群の入口。消すと native 系全体が到達不能 | 高（Burst 化する gameplay がまだない） | |
| U-3 | native/job パイプライン一式 | `Foundation/UpdateSystem/Native/` 1,115 + `Batches/` 183 + `Backends/JobSystemUpdateProcessorBackend.cs` 92 | **1,390** | 内部のみ | 有 | B | 同上 | **高** — 仮説の撤回に相当 | 高 | |
| U-4 | `NativeStateRegistry` snapshot 系<br>`BuildExecutionBatch` / `ApplyExecutionResult` | `Native/Registries/NativeStateRegistry.cs:355,413` | — | 1<sup>†</sup> | 2 | **C** | コード自身が「**lease 導入前の互換 API**」と記載。後継は `BeginExecutionLease`(L284) で、そちらは `NativeExecutionRuntime.cs` が本番利用 | **低** — lease が本線と確定済み。dirty クリア責務が 3 箇所に分散している原因 | — | |
| U-5 | `GetOrCreateUpdateLayer` | `World/UpdateCoordinator.cs:83` | — | 1<sup>†</sup> | 1 | C | なし（`GetOrCreateLayer` への単なる転送） | 低 | — | |
| U-6 | `ApplyMainThreadCommands` | `World/UpdateCoordinator.cs:332` | — | 1<sup>†</sup> | **0** | C | なし（`ApplyMainThreadChanges` への単なる転送） | 低 | — | |
| U-7 | `UpdateLayerIds.Streaming` / `StreamingLayerOrder` | `Runtime/UpdateSystem/Layers/UpdateLayerIds.cs:17,24` | 24 | **0** | **0** | **A** | ファイル自身の doc コメント「Camera Snapshot を読む処理は必ず Camera Layer より後に置く」＝**順序契約の宣言**。[carbon-engine/02 §5.1](../reference/carbon-engine/02-scheduler-vs-update-system.md)「Streaming Tick の Layer 固定」提案 | 低（定数のみ） | 中（`SessionWorldStreamingDriver` を Layer 駆動に移す） | |

<sup>†</sup> prod=1 は定義ファイル自身のみ＝実質未使用。

**U-1 の注意:** 正本仕様と後続計画が矛盾している。`UPDATER_CURRENT_SPEC.md` は構成要素として列挙し、`CAMERA_SYSTEM_UPDATESYSTEM_INTEGRATION_PLAN_2026-07-13.md §11` は「非推奨」と書く。**判定の前にどちらを正とするか決める必要がある。**

### 1.2 CameraSystem / Streaming

| # | API | 場所 | 行数 | prod | test | 性格 | 由来 doc | 削除コスト | 接続コスト | 判定 |
|:---:|---|---|---:|:---:|:---:|:---:|---|---|---|---|
| U-8 | `VolumeCrossfade` | `Runtime/CameraSystem/Effects/VolumeCrossfade.cs` | 299 | 1<sup>†</sup> | 1 | **B** | `CAMERA_SYSTEM_TDD_PLAN_2026-07-07.md:234` で新規作成（レッドテスト付き）→ `CAMERA_SYSTEM_UPDATESYSTEM_INTEGRATION_PLAN_2026-07-13.md:418` **§11 将来拡張（本フェーズ外メモ）**「`VolumeCrossfade` 処理の追加（`CameraSystem.Tick` 内または別 Element）」 | 中（TDD で作られテストが 1 ファイルある） | 低〜中（Tick に足すか Element 化するかが既に選択肢として書かれている） | |
| U-9 | `CameraSystemSliceSetup` | `Runtime/CameraSystem/Hosting/CameraSystemSliceSetup.cs` | 127 | 1<sup>†</sup> | 1 | **B** | `CAMERA_SYSTEM_BOOTSTRAP_EXECUTION_PLAN_2026-07-11.md:85`「**禁止** \| … `CameraSystemSliceSetup` の導入」、同:59「対象外」。`..INTEGRATION_PLAN_2026-07-13.md:81` でも Game 層配線が対象外 | 中 | 中（禁止が解けるフェーズの特定が先） | |
| U-10 | `CameraStreamingFocusAdapter` | `Runtime/Streaming/CameraStreamingFocusAdapter.cs` | 73 | 1<sup>†</sup> | 1 | **B/C** | [carbon-engine/03 §5.2](../reference/carbon-engine/03-destiny-vs-scene-streaming.md)「**Focus プロバイダ抽象** — `CameraStreamingFocusAdapter` パターンを一般化。将来ミニマップ視点等を union に追加しやすく」 | 中 | 中（ゲームは `SessionWorldStreamingDriver` で独自駆動中。乗り換えが要る） | |
| U-11 | `CameraFocusProvider` | `Runtime/Streaming/CameraFocusProvider.cs` | 79 | 2<sup>‡</sup> | 1 | **B/C** | 同上 | 中 | 中 | |

<sup>‡</sup> prod=2 は自分自身 + `CameraStreamingFocusAdapter`（＝U-10 とセットでのみ使われる）。

**U-10 / U-11 の注意:** 参照シリーズの提案 → FW 実装 → ゲームが使わない、という 3 段の断絶が起きている唯一の箇所。「一般化する価値がある」という判断自体を再評価するか、`SessionWorldStreamingDriver` を乗り換えさせるかの二択。

### 1.3 UISystem

| # | API | 場所 | 行数 | prod | test | 性格 | 由来 doc | 削除コスト | 接続コスト | 判定 |
|:---:|---|---|---:|:---:|:---:|:---:|---|---|---|---|
| U-12 | `IServiceResolver` | `Runtime/UISystem/Behaviors/UIBehaviorContext.cs:10` | 74<sup>*</sup> | 2<sup>§</sup> | **0** | **A** | `UI_BEHAVIOR_PIPELINE_WORKPLAN_2026-07-06.md:199`「今回は null 許容の**プレースホルダ interface のみ定義し、実装しない**」／`UI_MVVM_Behaviour_Plan.md:289`「今回の Vertical Slice スコープでは未実装。**契約のみ確定する**」 | **低いが意味が重い** — doc が明示的に置いた契約 | 中（パーティクル / サウンド等の外部演出サービスを Behavior から叩く用途が発生したとき） | |

<sup>*</sup> ファイル全体の行数。interface 自体は 10 行程度。
<sup>§</sup> prod=2 は定義ファイル + `BehaviorRunner.cs:66`（`IServiceResolver? services = null` で受けるだけ、常に null）。

**U-12 は「消してよい未使用コード」の反例。** doc が 2 箇所で「実装しない」と明記している。削除する場合は doc 側の契約宣言も同時に取り下げる必要がある。

---

## 2. DebugStudio 側

| # | API | 場所 | 行数 | src 参照 | test 参照 | 性格 | 由来 doc | 削除コスト | 接続コスト | 判定 |
|:---:|---|---|---:|:---:|:---:|:---:|---|---|---|---|
| D-1 | `DebugStudioClientSessionTransport` | `App/Core/Services/DebugStudioClientSessionTransport.cs` | 85 | 自身のみ | `ShellCompositionTests.cs:752` | **C** | `DEBUGSTUDIO_SERVER_INVERSION_PLAN_2026-04-30.md`（**全編文字化けで読めない**） | 低（本番は `AppCompositionRoot.cs:137` の `DebugStudioServerSessionTransport`） | — | |
| D-2 | `DebugStudioSession` | `Client/DebugStudioSession.cs` | 254 | D-1 経由のみ | 同上 | **C** | 同上 | 低 | — | |
| D-3 | `DebugSocketConnectionLifecycle` | `Client/Internal/DebugSocketConnectionLifecycle.cs` | 233 | D-2 のみ | なし | **C** | 同上 | 低 | — | |
| D-4 | `DebugSocketSendGateway` | `Client/Internal/DebugSocketSendGateway.cs` | 61 | D-2 のみ | なし | **C** | 同上 | 低 | — | |
| D-5 | `DebugCommandRoundtripClient` | `Client/DebugCommandRoundtripClient.cs` | 120 | 自身のみ | `DebugCommandRoundtripClientTests.cs` | C? | 該当 doc 未特定 | 低 | — | |

**D-1〜D-4 合計 633 行**が、server 反転後に本番から外れたまま、テスト 1 箇所によって延命されている。**C（置き換え済みの残骸）の典型で、この表で最も素直な削除候補。**

### 2.1 消してはいけないもの（誤認防止のため明記）

Client プロジェクト 1,277 行が丸ごと死んでいるわけではない。以下は**本番の server transport / CLI が使っている**:

| 生きている | 使用元 |
|---|---|
| `DebugSocketReceiveLoop.cs`(102) | `DebugStudioServerSessionTransport.cs` |
| `DebugSocketInboundRouter.cs`(151) | 同上 |
| `DebugSocketSendOperations.cs`(40) | 同上 |
| `DebugSocketClientOptions.cs`(16) | `SessionService` / `SessionWindowViewModel` 他 |
| `DebugCommandControlPlaneClient.cs`(161) | `Cli/Program.cs` |

### 2.2 前提作業

**D-1〜D-4 の判定前に、`DEBUGSTUDIO_SERVER_INVERSION_PLAN_2026-04-30.md` の文字化けを復旧する必要がある。**（407 行、Shift-JIS → UTF-8 の誤変換、repo 内 md で唯一）

この文書は server 反転という最重要アーキテクチャ決定の根拠であり、**上記デッドコードを生んだ張本人**。「旧経路を残す理由が当時あったのか」が読めない状態で削除判定を下すのは危険。git 履歴からの再取得を先に試す。

関連して、`App/Core/Services/SessionMessageRouter.cs:11` のコメントが「`DebugStudioSession` から受信した protocol envelope を…」と書いているが、**実際の受信元は `DebugStudioServerSessionTransport`**。D-2 を削除する / しないに関わらず、この陳腐化コメントは直す価値がある。

---

## 3. 判定の順序（推奨）

依存関係があるので、この順で決めるのが安全。

1. **D-1〜D-4** — 前提として文字化け文書の復旧。性格 C で単純、かつ 633 行と量が最大。
2. **U-4 / U-5 / U-6** — 性格 C。lease が本線と確定済みなので判断材料が揃っている。U-4 は dirty クリア責務の 3 分散を解消できる副次効果あり。
3. **U-1** — 正本仕様と後続計画の矛盾を先に解消する（どちらを正とするか）。
4. **U-8 / U-9 / U-10 / U-11** — 性格 B。CameraSystem の「フェーズ外」がいつ開くかの見通し次第。U-10/U-11 は `SessionWorldStreamingDriver` の扱いとセット。
5. **U-2 / U-3** — 性格 B かつ最大の重み（1,390 行 + 入口）。[Q5-1](../reference/00-questions-we-are-answering-2026-07-11.md) の仮説を継続するか撤回するかの判断そのもの。**単独では決めない方がよい。**
6. **U-7 / U-12** — 性格 A。契約の宣言なので、消すなら doc 側も同時に。急ぐ理由は薄い。

---

## 4. 再現方法

```bash
# 各行の prod / test 参照数（定義ファイル自身を含むので prod=1 は実質未使用）
git grep -l '<symbol>' -- 'unity/Assets/OneStarMaker/Scripts/*' 'unity/Assets/SampleGame/*'
git grep -l '<symbol>' -- 'unity/Assets/OneStarMaker/Tests/*'

# DebugStudio 側
git grep -l '<symbol>' -- 'tools/DebugStudio/src/*'
git grep -l '<symbol>' -- 'tools/DebugStudio/tests/*'

# UpdateSystem 実消費者（唯一の RegisterElement 呼び出し）
git grep -n 'RegisterElement\|RegisterNative' -- 'unity/Assets/OneStarMaker/Scripts/Runtime/*' 'unity/Assets/SampleGame/*'
```

---

## 5. 更新履歴

| 日付 | 内容 |
|---|---|
| 2026-08-03 | 初版。Unity 12 件 + DebugStudio 5 件。判定欄は空欄で提出 |
