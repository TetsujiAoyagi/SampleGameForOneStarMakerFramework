# テストスイート棚卸し — カバレッジ穴の一覧

> ステータス: **判定待ち**（優先度は付けたが、着手順は人間が決める）
> 調査日: 2026-08-08
> 対象断面: `develop` @ 9c6463d + 未コミットの作業ツリー
> 正本か否か: **正本ではない**。各サブシステムの契約の正本は `unity/Assets/Docs/Architecture/*.md` と `docs/updater/UPDATER_CURRENT_SPEC.md`
> 関連: [UNUSED_API_INVENTORY_2026-08-03.md](UNUSED_API_INVENTORY_2026-08-03.md)（未使用 API の棚卸し。本書と対象が重なる）

---

## 0. この文書の位置づけ

「今のリポジトリのテストが妥当か」を調べた結果の**穴の一覧**である。同じ調査で見つかった以下 2 点は既に別途対応済みなので、本書には含まない:

| 対応済み | 内容 |
|---|---|
| 赤 3 件の修正 | `UpdateSystemHost.Dispose` の EditMode 対応、および Log producer 相関の採取タイミング（formatter → 呼び出しスレッド）。§4 に経緯 |
| テスト名・doc コメント | `TestN` 形式の 50 メソッドを日本語名にリネーム、テストクラス 21 本に日本語 `/// <summary>` を付与 |

**本書に書いたものは 1 件も実装していない。** 実装は別スライスに切ること。

### スイートの現況（数値）

| 対象 | ファイル | テスト数 |
|---|---:|---:|
| `OneStarMaker.Tests`（EditMode） | 74 | 398 |
| `OneStarMaker.Tests.Editor`（EditMode） | 8 | 39 |
| DebugStudio（xUnit / .NET 8） | 48 | 約 295 |

テスト設計の質そのものは総じて高い。`FakeAssetBackend` / `FakeCameraBackend` / `FakeStreamingBackend` / `SceneDirectorTestBase` + `TestableSceneDirector` という手書き Test Double が整備されており（モックライブラリは不使用）、AssetOwner の 4 スコープ・SceneState の 14 状態・Streaming の収束はいずれも正面から検証されている。**問題はカバレッジの偏りに集中している。**

---

## 1. 優先度 1 — CLAUDE.md が明示する契約なのにテストが無い

### T-1. UpdateSystem のフレーム順序契約

CLAUDE.md は 1 フレームの順序を契約として宣言している:

```
ActivatePendingRegistrations → RunUpdate → RunLateUpdate → ApplyMainThreadChanges → ApplyStructuralChanges
```

この順序を**実装しているのは唯一** `unity/Assets/OneStarMaker/Scripts/Runtime/UpdateSystem/Hosting/UpdaterDriver.cs` の `Update()` / `LateUpdate()` である。

`Tests` 配下を全文検索した結果、以下はいずれも**参照 0 件**:

| 型 | 場所 |
|---|---|
| `UpdaterDriver` | `Runtime/UpdateSystem/Hosting/UpdaterDriver.cs` |
| `UpdateSystemRuntime`（静的 façade） | `Runtime/UpdateSystem/Api/` |
| `UpdateBehaviourAdapter` | `Runtime/UpdateSystem/Adapters/` |

`UpdateCoordinatorTests`（45 テスト）が検証しているのは Coordinator の**個々の API** であり、テスト自身が手で正しい順に呼んでいるにすぎない。**`UpdaterDriver` 内で呼び順を入れ替えても全テストが green のまま通る。**

`UpdateSystemHostTests` は 1 テストのみで、`TryConsumeActivationRequest` の gate だけを見ている。

> 難所: `UpdaterDriver` は `MonoBehaviour` で、順序を決めているのが Unity の `Update` / `LateUpdate` コールバックそのものである。EditMode で PlayerLoop を回せないため、素直には書けない。順序決定部分を `MonoBehaviour` から切り出して純粋なメソッドにする設計変更とセットになる可能性が高い。

### T-2. 「SceneLifecycleManager が状態変更の単独オーナー」

CLAUDE.md は状態変更のオーナーが `SceneLifecycleManager` 単独であることを契約としている。

実際に `TransitionTo(` を呼んでいるのは `SceneBase.cs`（5 箇所）と `SceneDirector.cs:220` のみで、現状は守られている。しかし**第三者が呼んでも落ちるテストは存在しない**。`SceneLifecycleManagerTests`（27 テスト）が守っているのは遷移の妥当性であって、呼び出し元の制限ではない。

---

## 2. 優先度 2 — サブシステム丸ごとテスト 0 件

`Tests` 配下でクラス名が 1 度も出現しないもの。

| サブシステム | ソース数 | 未テストの主なクラス |
|---|---:|---|
| `Foundation/Config` | 5 | `AppConfig`, `CommandLineConfigProvider`, `EnvironmentVariableConfigProvider`, `JsonConfigFlattener`, `IConfigProvider` |
| `Runtime/Config` | 1 | `JsonFileConfigProvider` |
| `Runtime/Telemetry` | 3 | `RuntimeTelemetryMemorySnapshot`, `RuntimeTelemetryMetadataFactory`, `UnityEnginePlayerLoopFrameObservationBootstrap` |
| `Runtime/Bootstrap` | 2 | `AbstractApplicationInitializer`, `RemoteBuildInfo` |
| `Debug/Profiler` | 3 | `FrameTimeSampler`, `FrameTimeGraphRenderer`, `DebugProfilerView` |
| `Runtime/UISystem/Behaviors` | — | `FadeBehavior`, `FlashBehavior`, `ScaleBehavior`, `ShakeBehavior`, `TweenNumberBehavior`, `BehaviorAsset`（`BehaviorRunner` 自体は厚い） |
| `Editor/AssetManagement` | 1 | `AssetMemoryEstimator` |

部分的に薄いもの:

| サブシステム | 状況 |
|---|---|
| `Runtime/DebugSocketServices` | 21 ファイル中 5 クラスのみ言及。**トランスポート層・セッション層は完全に未テスト**（`DebugSocketClientSession`, `DebugSocketRealtimeStream`, `DebugSocketTransportHost`, `DebugSocketInboundMessageRouter`, `MainThreadDebugCommandDispatcher`, `DebugSocketService.*` の partial 5 ファイル全部）。テストがあるのは envelope/DTO のシリアライズと builder/registry の純粋部分のみ。DebugStudio 側の `CliControlPlaneRoundtripTests` / `DebugStudioServerSessionTransportTests` が .NET 側からこの穴を部分的に埋めている |
| `Editor/Build` | 19 ファイル中 3 クラス（`AssetDependencyClosure`, `AssetDescriptionCollector`, `VariantWhitelistBuilder`）のみ。`VariantFilteringBuildScript`, `VariantPlayerBuild`, `VariantRemoteBuildBatch/Setup`, `AddressablesGroupSyncFilter`, `RemoteCatalogEditorInjector` などは 0 |
| `Foundation/Logging` | `MessagePackZLoggerFormatter` と（本作業で追加した）`LogProducerCorrelation` のみ。`AppLoggerFactory`, `LogEnvelopeV1`, `RealtimeLogFormat` は 0 |
| `Editor/SceneGraph` | 12 ファイル中 4 クラス（`SceneGraphClipboard`, `SceneGraphEdges`, `SceneGraphPasteService`, `SceneGraphViewModel`）に 26 テストが集中。`SceneGraphValidator`, `SceneResourceGenerator`, `SceneGraphInspectorPanel`, `SceneGraphEditorWindow`, `SceneGraphNode` は 0 |
| `Runtime/AssetManagement` | 全体は厚いが `AddressableBackend`, `AssetRegistry`, `AssetReleaseOnDestroy`, `MemoryBudgetConfig`, `RemoteCatalogRuntimeBridge` は 0。`AssetOwner` 構造体自体の `Equals`/`GetHashCode`/`Scene(null)`/`Bind(null)` の直接単体テストも無い |

`SCENEGRAPH_EDITOR_MULTISELECT_HANDOFF_2026-08-05.md` に対応するテストファイルは存在しない（`SCENEGRAPH_EDITOR_PASTE_EXTRACTION` には `SceneGraphPasteServiceTests.cs` が対応している）。

---

## 3. 優先度 3 — 構造・その他

### S-1. PlayMode テストアセンブリが 1 つも存在しない

`OneStarMaker.Tests` / `OneStarMaker.Tests.Editor` はいずれも `"includePlatforms": ["Editor"]`。`[UnityTest]` は 103 件あるがすべて EditMode 上の `UniTask.ToCoroutine` であり、**実 PlayerLoop・実 Addressables・実 Unity SceneManager を通る経路は 1 つもテストされていない**。

`tools/run-tests.ps1` の記述（「EditMode で全件がカバーされる」）から、これは意図的なトレードオフと読める。**穴であることだけ記録する。** T-1 と S-1 は同根であり、T-1 に着手するならここも一緒に判断が要る。

### S-2. `UpdateCoordinatorTests.cs` の分割

1157 行 / 45 テスト。CLAUDE.md A-2 の 500 行閾値を大きく超えている。本作業ではリネームのみ行い、分割は見送った（リネームと分割を同一差分に混ぜるとレビュー不能になるため）。

責務は本作業で付けた doc コメントのとおり 4 つに分かれており、分割の切り口はそのまま使える:

1. 実行順序（layerOrder / executionOrder / 登録順 / native と managed の前後）
2. 遅延反映（Activate / ApplyStructuralChanges を跨ぐ登録・解除）
3. 例外伝播
4. mainThread apply

### S-3. 副作用を検証していないテスト

`unity/Assets/OneStarMaker/Tests/Scene/SceneDirectorGuardTests.cs`（52 行 / 4 テスト）は `Assert.Pass()` のみ。

```csharp
public IEnumerator UnloadScene_NonexistentScene_NoOp() => UniTask.ToCoroutine(async () => {
    SetupSingleScene();
    await Director.UnloadScene("NonExistent");
    Assert.Pass();
});
```

「例外が出ない」ことしか主張しておらず、**他シーンが影響を受けていないこと**を検証していない。ガードの本質は後者なので、現状は看板倒れになっている。

### S-4. リフレクション依存で静かに壊れるテスト

| ファイル | 状況 |
|---|---|
| `Tests/Editor/Build/AssetDescriptionCollectorTests.cs` | 54 行中 40 行が private field（`_payloads`, `_sceneAssetDescription`, `_sceneResources`, `_sceneResourceMap`）の `SetValue`。assert は 2 つのみ |
| `Tests/UpdateSystem/Registries/UpdateElementRegistryTests.cs` | `_entries` と nested type `ElementEntry` をリフレクションで差し込んで generation 上限を再現している |

いずれも**フィールド名を変えるとコンパイルは通るのに検証条件が黙って崩れる**。本作業で doc コメントにこの旨を明記したが、構造的な解決（テスト用の内部 API を切る等）は手付かず。

### S-5. DebugStudio 側

| 項目 | 内容 |
|---|---|
| テンプレート残骸 | `tests/DebugStudio.App.Tests/UnitTest1.cs` と `tests/DebugStudio.Contracts.Tests/UnitTest1.cs`。いずれも `Assert.True(true)` のみ |
| Test Double の重複定義 | `RecordingHttpMessageHandler` が 3 箇所（`Export.Tests/Elastic/ElasticTelemetryIngestClientTests.cs:255`, `App.Tests/Services/ElasticTelemetryPushServiceTests.cs:217`, `App.Tests/Features/Telemetry/TelemetryWindowViewModelTests.cs:319`）、`StubElasticEnvironmentReader` が 3 箇所に別々に定義されている |
| 巨大単一ファイル | `App.Tests/ShellCompositionTests.cs` が 43 テスト / 957 行 |

### S-6. 命名規約がリポジトリ内で不統一（残存）

本作業後の状況:

| 対象 | 日本語テスト名 |
|---|---|
| Unity 側 | 73 / 437 |
| DebugStudio 側 | 276 / 295 |

Unity 側の既存 364 メソッドは `Method_Condition_Expected` の英語名のまま残している。全面日本語化は git blame とテスト結果 XML の履歴が切れる割に得るものが少ないと判断した。**規約を統一するかどうかは未判断。**

---

## 4. 参考 — 本作業で直した赤 3 件の原因

再発時の参照用に残す。

| テスト | 原因 |
|---|---|
| `UpdateSystemHostTests.TryConsumeActivationRequest_BeforeSceneDirectorBinding_ReturnsFalse` | `UpdateSystemHost.Dispose()` が `UnityEngine.Object.Destroy` を呼んでいた。EditMode では Unity がエラーログを出し、NUnit が unhandled log message として失敗させる。コンストラクタ側には既に `Application.isPlaying` の分岐があり、`Dispose` だけが対になっていなかった |
| `TelemetryLogCorrelationTests.LogAndTelemetry_共有sequenceで1_2_3と採番される` | `MessagePackZLoggerFormatter` が `NextProducerSequence()` を **formatter 実行時**に呼んでいた。ZLogger は entry を背景スレッドで format するため、採番が「ログが起きた順」ではなく「flush された順」になっていた |
| `TelemetryLogCorrelationTests.LogInsideActiveSpan_TraceIdとSpanIdを持つ` | 同上。`AppTelemetry.CurrentTraceId` は `AsyncLocal` で流れており、ZLogger の背景スレッドには伝播しないため常に null になっていた |

どちらが正かは `Docs/Architecture/12-telemetry.md` が決着させている（`:384` producerSequence = 順序に意味がある、`:387` traceId/spanId = active span 内のみ、`:386` unityFrameAtEmit = format 時が正）。すなわち**テストが正しく、実装が契約を満たしていなかった**。

修正は相関値の採取を呼び出しスレッドへ移し、ZLogger の scope に載せて entry へ運ぶ方式（`Foundation/Logging/LogProducerCorrelation.cs`）。副次的に、同じログが rolling file と realtime stream の 2 provider を通っても採番が 1 回で済むようになった。

**記録に残す事実:** `TestResults/results-all-*.xml` に残っている**フル実行 9 回すべてが failed 3** だった（2026-08-05 01:01 〜 2026-08-06 06:15）。CLAUDE.md は「exit 0 = failed 0」を Phase C の回帰判定に使うと宣言しているが、2 日間その判定は機能していなかった。

修正後の実行結果（`results-all-20260808-093258.xml`）: **total 438 / passed 438 / failed 0、exit 0**。記録が残っている範囲では初めての green。DebugStudio 側も 320 件すべて成功（Contracts 27 / Export 60 / Server 10 / Cli 7 / App 216）。

---

## 5. やらないと決めたこと

- Unity 側テスト名の全面日本語化（§3 S-6）
- DebugStudio 側の命名変更（すでに日本語で統一されている）
- `// Arrange` / `// Act` / `// Assert` の追加（Unity 側に現状 0 件。無いままでよい）
- `UpdateCoordinatorTests.cs` の分割（§3 S-2 に切り口だけ残した）

---

## 6. 差し戻し

（未記入）

## 7. レビュー結果

（未記入）

## 8. C' 監査結果

（未記入）
