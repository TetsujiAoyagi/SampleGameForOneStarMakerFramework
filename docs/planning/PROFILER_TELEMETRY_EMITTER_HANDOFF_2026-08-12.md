# Profiler テレメトリを UIView から切り離して常駐させる ハンドオフ (2026-08-12)

- ブランチ: `feature/profiler-telemetry-emitter`（`develop` から切る）
- Phase A 作成: Claude Code (Opus 5)
- Phase B 実装: Sonnet 5（`claude-sonnet-5-thinking-high`）
- Phase C レビュー: Claude Code (Opus 5)
- Phase C' 監査: Opus 4.8（`claude-opus-4-8-thinking-high`）

---

## 0. 1分で把握

`ProfilerSummary` / `GcSpike` / `UiCost` の 3 種類のテレメトリが **Unity から一度も出ていない**。
このため Kibana ダッシュボードのパネル 8 枚（CPU 推移 / fps 推移 / メモリ推移 / CPU p95 / fps p05 等）が作れない。

原因は 1 つ:

> 送出コードが `DebugProfilerView.Update()`（UIView の MonoBehaviour メッセージ）の中にあり、
> **`DebugProfilerView` はプロジェクト全体で一度も生成されていない**。

```
$ grep -rn "DebugProfilerView" unity/Assets --include=*.cs
→ 定義（DebugProfilerView.cs）と AppTelemetry.cs のコメント以外に参照なし
$ grep -rln "DebugProfilerView" unity/Assets --include=*.prefab --include=*.unity --include=*.asset
→ 0 件（prefab / シーンにも置かれていない）
```

**このスライスは「常駐させて Update() を回す」ことではなく、「送出ロジックを UIView から出す」こと。**
理由は §1.1 に書く。完了すると `ProfilerSummary` が 1 秒に 1 件流れ、上記 8 枚が解禁される。

**やらないこと（範囲の上限）:**

- UIScene への Canvas 追加、`DebugProfilerView` の UI Toolkit 移植 → **別スライス**
- `UICommon` / `SceneDirector` / `AssetOwner` / `SceneState` への変更 → **一切触らない**
- Kibana のパネル追加・NDJSON 正本の変更 → **このスライスの範囲外**
- 新しい設計ドキュメントの作成 → **禁止**（`.cursor/rules/docs-policy.mdc` §2）

---

## 0.5 この HANDOFF の設計そのものを批判的にレビューすること（**必須**）

実装に入る前に §1 と §3 を読み、**設計判断そのものに反対意見があれば §6 に書いてから実装を始めること。**
「指示どおり作ったが筋が悪かった」を後で C / C' が見つけるのは高くつく。特に:

- §1.2 の「Policy / RecordFactory / Emitter の 3 分割」は過剰分割ではないか
- §1.3 の「Profiler は独自 Layer を Debug 側に持つ」は UpdateSystem の契約に反していないか
- §3 の行数見積もりが外れそうなら、**1 行も書く前に §6 に書く**

---

## 1. 確定方針（設計判断。実装側で勝手に変更しない。反対なら §6 へ）

### 1.1 「常駐 UIView にする」案は**採らない**。Canvas が無いため

前スライス（§7.7）は「シーンが所有しない UIView の寿命を誰が持つか」を次スライスの論点として申し送った。
Phase A で調べた結果、**その論点は今解いても無意味である**ことが分かった。

`DebugProfilerView` はレガシー uGUI の `UIView`（`Image` / `RawImage` / `TextMeshProUGUI`）である。
一方、`UICommon` が乗っているシーン `unity/Assets/OneStarMaker/Scenes/UISystem/UIScene.unity` には
**uGUI の Canvas が 1 つも無い**（実測）:

```
$ grep -c "^--- !u!223 " unity/Assets/OneStarMaker/Scenes/UISystem/UIScene.unity
0

UICommon        (RectTransform + UICommon.cs)
  ├ EventSystem
  ├ UIPanel     (PanelRenderer + PanelSettings)   ← UI Toolkit のみ
  └ 3DCanvasParent (RectTransform)
```

`UICommon.AddUIView` の uGUI 経路は `view.transform.SetParent(uiCommon.transform, false)` するだけなので、
**Canvas 祖先が存在せず、常駐させても 1 px も描画されない。**
ゲーム側の View は全て `UIToolkitView`（Title / HpGauge / ConfirmDialog / InGameHud / OutGameBackground）で、
uGUI 経路は実質テストハーネスからしか使われていない。

したがって:

| 問題 | このスライスで解く | 解き方 |
|---|---|---|
| `ProfilerSummary` が出ない | **解く** | 送出を UIView から切り離し、UpdateSystem の Element として常駐させる |
| 常駐 UIView の寿命を誰が持つか | **解かない** | 出す先の Canvas が無く、決める動機が満たせない。Canvas / UIToolkit 移植を伴う別スライスへ |

### 1.2 送出ロジックは 3 つに割る。**理由はテスト可能性**

現状 `DebugProfilerView.Update()` の中に、閾値判定・レコード組み立て・送出が全部入っている。
`Update()` は MonoBehaviour メッセージなので**単体テストが 1 本も書けない**。
これは CLAUDE.md が「`ApplyPaste`（約 120 行）がテスト 1 本も書けないまま残った」として記録した失敗と同型である。

分割は以下で確定する。**これは設計判断であり、実装側で 1 ファイルに戻さない。**

| 型 | 責務 | Unity 依存 | テスト |
|---|---|---|---|
| `ProfilerTelemetryPolicy` | 閾値と入力から**どのレコードを出すか決める**だけ | **なし**（純粋関数） | **必須。境界値を網羅** |
| `ProfilerTelemetryRecordFactory` | 決まった種別から `TelemetryRecord` を**組み立てる**だけ | **なし**（純粋関数） | **必須。形状を検証** |
| `ProfilerTelemetryEmitter` | `IUpdateElement` として毎フレーム駆動し、上 2 つを呼んで `AppTelemetry.WriteRecord` する | あり | 配線のみ。薄く保つ |

> **`Policy` と `RecordFactory` に `UnityEngine` の using を書いたら設計違反。**
> `Time.frameCount` / `GC.CollectionCount` / `ProfilerRecorder` は Emitter 側で読み、**値として** Policy に渡す。

### 1.3 常駐の形は `CameraSystem` と同型にする（**既存の前例をなぞる**）

`CameraSystem` は既に「MonoBehaviour を持たず、UpdateSystem の Element として `AppInitializer` が所有する」形になっている。
Profiler も同じ形にする。**新しい常駐パターンを発明しない。**

前例（`unity/Assets/SampleGame/DependOnAll/AppInitializer.cs`）の要点:

- `BeforeSceneLoad` の `UpdateSystemHost` 生成後に構築する
- `coordinator.RegisterElement(layerId, element, layerOrder: N)` で登録する
- **登録直後に `coordinator.ActivatePendingRegistrations()` を呼ぶ。**
  これを呼ばないと `UpdateSystemHost` の scene stability gate に引っかかり、
  `SceneDirector` が bind されるまで Element が動き出さない
- 解放は `Deactivate()`（no-op 化）→ `UnregisterElement()` の順。構造変更は遅延適用されるため順序が意味を持つ
- 解放は `Application.quitting` と `SubsystemRegistration` の**両方**から呼べるよう冪等にする
  （Domain Reload 無効時に前セッションの残骸を掃除するため）

### 1.4 Layer 定義は **Debug 側に置く**。`UpdateLayerIds` に足さない

`OneStarMaker.Runtime.UpdateSystem.Api.UpdateLayerIds` は doc コメントで
「Camera と Streaming の依存関係**だけ**をここで明示する」と自ら範囲を宣言している。
また Runtime は Debug を知らない（依存グラフ上、知ってはいけない）。

したがって `ProfilerTelemetryEmitter` に `public const string LayerId = "Profiler";` /
`public const int LayerOrder = 90;` を持たせる。
`UpdateCoordinator.RegisterElement` は `GetOrCreateLayer(layerId, layerOrder)` を内部で呼ぶため、
**Layer の事前宣言は不要**（実測: `UpdateCoordinator.cs:95`）。

順序 90 の根拠: Camera=50 / Streaming=60 より後。**フレームの計測は全部が終わってから取る。**
駆動は `OnElementLateUpdate` を使う（`OnElementUpdate` は空実装にする）。

### 1.5 `DebugProfilerView` からは送出を**消す**。表示だけ残す

二重送出を防ぐため、View 側の `WriteProfilerTelemetry` / `DetectGcSpike` / `DetectUiCost` は削除する。
View は「グラフ表示・数値表示・警告行表示」だけになる。

警告文言（`[⚠ GC] ...` / `[⚠ UI] ...`）は Emitter 側から
`AppTelemetry.NotifyBottleneck(message)` を呼ぶことで既存の `AlertStream.AlertRaised` に流す。
**View は既にこのイベントを購読しているので、View 側の購読コードは変えなくてよい**
（`DebugProfilerView.Awake` の `AppTelemetry.AlertStream.AlertRaised += OnBottleneckDetected`）。

> View は自前の `FrameTimeSampler` をグラフ表示用に持ち続ける。Emitter 側と二重にサンプリングすることになるが、
> **View は現時点で一度も生成されないため実害が無く**、Emitter → View の配線は Canvas スライスの仕事である。
> **これは意図して残す妥協であり、Phase C で「重複だから直せ」と指摘しない。**

### 1.6 config で切れるようにする

`telemetry:profiler:enabled`（bool、**デフォルト true**）。
`AppConfig.GetBool(key, defaultValue)` が既にある（`unity/Assets/OneStarMaker/Scripts/Foundation/Config/AppConfig.cs:62`）。

既存の閾値キーと命名を揃える（`telemetry:thresholds:gcPerFrame` 等が既にこの形）。

---

## 2. 出力仕様（**既存の出力を 1 バイトも変えない**）

送出される `TelemetryRecord` の形は**現行 `DebugProfilerView.WriteProfilerTelemetry` と完全に同一**にすること。
Kibana 側の検算ルール（V1〜V12）と `_export` 済みダッシュボードがこの形に依存している。

| 種別 | `name` | `kind` | payload | level | tags |
|---|---|---|---|---|---|
| 1 秒サマリ | `TelemetryStartType.ProfilerSummary` | `Sample` | `CreateFrameSampleTelemetry(fps, cpuTime, gpuTime, gpuAvailable)` | `Verbose` | `RuntimeTelemetryMetadataFactory.ClassifyFrameRate(avgFps)` |
| GC スパイク | `TelemetryStartType.GcSpike` | `Event` | `TelemetryPayload.ForEventDetail(gcGen0Delta, unityFrame)` | `Summary` | `AllocSpike \| Bottleneck` |
| UI コスト | `TelemetryStartType.UiCost` | `Event` | `TelemetryPayload.ForEventDetail(gcGen0Delta: 0, unityFrame)` | `Summary` | `Bottleneck` |

共通:

```csharp
var now = DateTime.UtcNow.Ticks;
var kind = TelemetryKindRules.InferKind(startType);
new TelemetryRecord(
    traceId: AppTelemetry.GenerateId(),
    spanId: AppTelemetry.GenerateId(),
    parentSpanId: -1,          // 親なしのセンチネルは -1 に統一（0 と混在させない）
    name: startType,
    startTimestampUtcTicks: now,
    endTimestampUtcTicks: now,
    elapsedMs: 0,              // sample では意味を持たないプレースホルダ
    isSuccess: true,
    tags: tags,
    level: level,
    metadata: metadata,        // Event 側は default
    kind: kind,
    payload: payload);
```

**`ProfilerSummary` は `kind=sample` であり、`elapsedMs` は意味を持たない**（Contract v3）。
export 側は sample の `elapsedMs` キーを省略する。ここを `span` にすると Kibana 側の検算が赤になる。

### 2.1 発火条件（現行と同一）

| 種別 | 条件 |
|---|---|
| `ProfilerSummary` | `sampler.SummaryUpdated == true`（1 秒ごと）。読み取り後 `false` に戻す |
| `GcSpike` | `gcDelta > 0` **かつ** `gcDelta > thresholds.GcPerFrame` |
| `UiCost` | UI コスト取得可能 **かつ**（`rebuilds > thresholds.CanvasRebuildPerFrame` **または** `batches > thresholds.BatchCount`） |
| 全種別共通の前提 | `AppTelemetry.IsEnabled == true` **かつ** `AppTelemetry.Thresholds != null` |

> **境界に注意: すべて `>` であって `>=` ではない。** 閾値ちょうどは発火しない。
> ここを `>=` に変えると `gcPerFrame: 1` のデフォルトで毎フレーム GcSpike が出る。

---

## 3. 変更対象ファイル一覧（A-1: 規模見積もり）

### 3.1 新規（すべて `unity/Assets/OneStarMaker/Scripts/Debug/Profiler/`）

| ファイル | 予想行数 | 責務数 |
|---|---|---|
| `ProfilerTelemetryPolicy.cs` | 約 120 | 1（閾値判定） |
| `ProfilerTelemetryRecordFactory.cs` | 約 120 | 1（レコード組み立て） |
| `ProfilerTelemetryEmitter.cs` | 約 140 | 1（`IUpdateElement` として駆動・送出） |
| `ProfilerUiCostCollector.cs` | 約 80 | 1（`ProfilerRecorder` の寿命管理） |

### 3.2 変更

| ファイル | 現在 | 予想 | 内容 |
|---|---|---|---|
| `Scripts/Debug/Profiler/DebugProfilerView.cs` | 513 | 約 300 | 送出・GC 検出・UI コスト検出を削除。末尾の `ProfilerUiCostCollector` / `ProfilerUiCostSnapshot` を 3.1 のファイルへ移設 |
| `SampleGame/DependOnAll/AppInitializer.cs` | 195 | 約 250 | Emitter の構築・登録・解放（`InitializeCameraSystem` / `ReleaseCameraSystem` と同型） |
| `OneStarMaker/Tests/OneStarMaker.Tests.asmdef` | — | +1 行 | `references` に `"OneStarMaker.Debug"` を追加（§3.4 参照） |
| `SampleGame/Config/app-config.json` | 5 | 6 | `"telemetry:profiler:enabled": true` |

### 3.3 新規テスト（`unity/Assets/OneStarMaker/Tests/Profiler/`）

| ファイル | 予想行数 |
|---|---|
| `ProfilerTelemetryPolicyTests.cs` | 約 180 |
| `ProfilerTelemetryRecordFactoryTests.cs` | 約 150 |

### 3.4 asmdef 参照追加は**設計判断**（A-3: 黙って足すな、と書いてあるので明示する）

`OneStarMaker.Tests` は現在 `OneStarMaker.Debug` を参照していない。
`SampleGame.DependOnAll` 経由で間接的に繋がっているが、**Unity の asmdef 参照は推移しない**ため、
このままでは `ProfilerTelemetryPolicy` 等をテストから見られない。

**`OneStarMaker.Tests` の `references` に `"OneStarMaker.Debug"` を足すことを、設計判断として許可する。**
根拠: Tests は依存グラフの葉であり、Debug は既に `DependOnAll` 経由でグラフに入っている。循環は生じない。

**これ以外の asmdef は 1 行も変更しないこと。** 特に `OneStarMaker.Runtime` に `OneStarMaker.Debug` を足すのは
依存グラフの逆流であり、絶対にやらない。

### 3.5 新責務の配置（**A-3: これは設計判断としてこう決めた**）

- `ProfilerUiCostCollector` / `ProfilerUiCostSnapshot` は現在 `DebugProfilerView.cs` の末尾に `internal` で同居している。
  **Emitter から使うため別ファイルへ移し、`public` にする。**
  併せて `IProfilerUiCostSource`（`ProfilerUiCostSnapshot Capture()` のみ）を同ファイルに定義し、
  Emitter はインタフェース越しに持つ（テストで差し替えられるようにするため）
- `LayerId` / `LayerOrder` の定数は `ProfilerTelemetryEmitter` の `public const` として持つ（§1.4）
- Emitter は `FrameTimeSampler` と `IProfilerUiCostSource` を**コンストラクタで受け取る**。`new` を内部で書かない

---

## 4. 施工チケット

### P-1 `ProfilerUiCostCollector` の切り出し

`DebugProfilerView.cs` 末尾の `ProfilerUiCostCollector` / `ProfilerUiCostSnapshot` を
`ProfilerUiCostCollector.cs` へ移す。**中身のロジックは 1 行も変えない。**

追加するのはインタフェースだけ:

```csharp
public interface IProfilerUiCostSource
{
    ProfilerUiCostSnapshot Capture();
}
```

`ProfilerUiCostCollector : IProfilerUiCostSource, IDisposable` にする。両型を `public` にする。

**完了条件:** コンパイルが通る。`DebugProfilerView` は移設後の型を参照して従来どおり動く。

---

### P-2 `ProfilerTelemetryPolicy`（**テスト必須**）

`UnityEngine` を using しない純粋型。

```csharp
[Flags]
public enum ProfilerTelemetryEmission
{
    None = 0,
    Summary = 1,
    GcSpike = 2,
    UiCost = 4,
}

public readonly struct ProfilerFrameInput
{
    public readonly bool SummaryUpdated;
    public readonly int GcGen0Delta;
    public readonly bool UiCostAvailable;
    public readonly long CanvasRebuildCount;
    public readonly long BatchCount;
    // コンストラクタで全部受ける
}

public static class ProfilerTelemetryPolicy
{
    public static ProfilerTelemetryEmission Decide(
        in ProfilerFrameInput input,
        TelemetryThresholds? thresholds,
        bool telemetryEnabled);
}
```

判定は §2.1 の表のとおり。

**要求するテスト（日本語名で書くこと。既存テストの命名に合わせる）:**

| # | 内容 |
|---|---|
| 1 | `telemetryEnabled=false` なら全入力で `None` |
| 2 | `thresholds=null` なら全入力で `None` |
| 3 | `SummaryUpdated=true` で `Summary` が立つ / `false` で立たない |
| 4 | **`gcDelta == GcPerFrame` では `GcSpike` が立たない**（境界。`>` であって `>=` ではない） |
| 5 | `gcDelta == GcPerFrame + 1` で `GcSpike` が立つ |
| 6 | `gcDelta <= 0` では立たない |
| 7 | `UiCostAvailable=false` なら rebuild/batch がいくら大きくても `UiCost` は立たない |
| 8 | rebuild だけ超過 / batch だけ超過のそれぞれで `UiCost` が立つ |
| 9 | 3 つ同時に条件を満たしたとき、3 つのフラグが全部立つ |

> **テスト 4 は「修正を外すと赤になる」ことを確認してから提出すること。**
> `>` を `>=` に書き換えてテストが赤になるのを一度見る。赤にならないならテストが効いていない。

**完了条件:** 上記 9 本が green。`ProfilerTelemetryPolicy.cs` に `using UnityEngine` が無い。

---

### P-3 `ProfilerTelemetryRecordFactory`（**テスト必須**）

`ProfilerTelemetryEmission` の 1 種別と必要な値から `TelemetryRecord` を組み立てて返す純粋型。
**`AppTelemetry.WriteRecord` は呼ばない**（呼ぶのは Emitter）。

```csharp
public static class ProfilerTelemetryRecordFactory
{
    public static TelemetryRecord CreateSummary(
        float fps, float cpuAvgMs, float gpuAvgMs, bool gpuAvailable, long utcTicks);

    public static TelemetryRecord CreateGcSpike(int gcGen0Delta, int unityFrame, long utcTicks);

    public static TelemetryRecord CreateUiCost(int unityFrame, long utcTicks);
}
```

- `utcTicks` と `Time.frameCount` は**引数で受ける**（`DateTime.UtcNow` を内部で読まない。テストで固定するため）
- 中身は §2 の表と現行 `WriteProfilerTelemetry` のコードをそのまま移す
- `AppTelemetry.GenerateId()` の呼び出しは内部で行ってよい（値の中身は検証しない）

**要求するテスト:**

| # | 内容 |
|---|---|
| 1 | `CreateSummary` の `kind` が `Sample` である |
| 2 | `CreateSummary` の `name` が `ProfilerSummary`、`level` が `Verbose` |
| 3 | `CreateGcSpike` / `CreateUiCost` の `kind` が `Event`、`level` が `Summary` |
| 4 | 3 種すべてで `parentSpanId == -1`（0 ではない） |
| 5 | 3 種すべてで `startTimestampUtcTicks == endTimestampUtcTicks == 引数の utcTicks`、`elapsedMs == 0` |
| 6 | `CreateGcSpike` の payload に渡した `gcGen0Delta` が載っている |
| 7 | `CreateSummary` の tags が `ClassifyFrameRate(fps)` と一致する（fps を 2 水準で確認） |

**完了条件:** 上記が green。

---

### P-4 `ProfilerTelemetryEmitter`

```csharp
public sealed class ProfilerTelemetryEmitter : IUpdateElement, IDisposable
{
    public const string LayerId = "Profiler";
    public const int LayerOrder = 90;

    public ProfilerTelemetryEmitter(FrameTimeSampler sampler, IProfilerUiCostSource uiCostSource);

    public void OnElementStart() { }
    public void OnElementUpdate(in UpdateFrameContext context) { }   // 空。計測は LateUpdate
    public void OnElementLateUpdate(in UpdateFrameContext context);
    public void Deactivate();
    public void Dispose();
}
```

`OnElementLateUpdate` の中身:

1. `_isActive` が false なら即 return
2. `_sampler.Sample()`
3. `GC.CollectionCount(0)` の差分を取り `_lastGcCount` を更新
4. `_uiCostSource.Capture()`
5. `ProfilerFrameInput` を組み立て、`ProfilerTelemetryPolicy.Decide(...)` を呼ぶ
6. 立っているフラグごとに `ProfilerTelemetryRecordFactory` でレコードを作り `AppTelemetry.WriteRecord(record)`
7. `Summary` を出したら `_sampler.SummaryUpdated = false` に戻す
8. `GcSpike` / `UiCost` を出したときは `AppTelemetry.NotifyBottleneck(msg)` も呼ぶ。
   文言は現行 `DebugProfilerView` と同一にする:
   - `[⚠ GC] {gcDelta} collections @ frame {frameCount} ({sceneName})`
   - `[⚠ UI] {rebuilds} rebuilds, {batches} batches`

`Deactivate()` は `_isActive = false`（`CameraSystemUpdateElement` と同じ理由: Unregister は構造変更フェーズまで遅延するため、先に no-op 化する）。
`Dispose()` は `_uiCostSource` が `IDisposable` なら Dispose する。

**完了条件:** コンパイルが通る。Emitter 自体のテストは要求しない（配線のみのため。ロジックは P-2 / P-3 で押さえてある）。

---

### P-5 `DebugProfilerView` から送出を削除

削除するもの:

- `WriteProfilerTelemetry` メソッド全体
- `DetectGcSpike` メソッド全体と `_lastGcCount` フィールド
- `DetectUiCost` メソッド全体と `_uiCostCollector` フィールド
- `Update()` 内の `DetectGcSpike()` / `DetectUiCost()` 呼び出し
- `LogSummary()` 内の `if (AppTelemetry.IsEnabled) { WriteProfilerTelemetry(...) }` ブロック
- `OnDestroy()` の `_uiCostCollector.Dispose()`
- 不要になった using（`Unity.Profiling` / `OneStarMaker.Foundation.Telemetry` 等。**未使用 using を残さない**）

残すもの:

- `_sampler` / `_graphRenderer` / 表示系すべて
- `LogSummary()` の `_logger.LogInformation(msg)`（ログ出力は残す）
- `AppTelemetry.AlertStream.AlertRaised` の購読と `OnBottleneckDetected` / `PushWarning` / `UpdateWarningDisplay`

**完了条件:** コンパイルが通る。`DebugProfilerView.cs` から `AppTelemetry.WriteRecord` の呼び出しが消えている。

---

### P-6 `AppInitializer` から常駐させる

`InitializeCameraSystem` / `ReleaseCameraSystem` と**同じ形**で `InitializeProfilerTelemetry` / `ReleaseProfilerTelemetry` を書く。

- 呼び出し位置: `Before()` の中、`InitializeCameraSystem()` の**直後**
- `Config?.GetBool("telemetry:profiler:enabled", true) != true` なら何もしない
- `UpdateCoordinator` が null なら何もしない
- `RegisterElement(ProfilerTelemetryEmitter.LayerId, emitter, layerOrder: ProfilerTelemetryEmitter.LayerOrder)`
  が false を返したら `InvalidOperationException`（Camera と同じ扱い）
- **登録後に `coordinator.ActivatePendingRegistrations()` を呼ぶ**（§1.3。忘れると動き出さない）
- `Sub()` の `s_instance.ReleaseCameraSystem()` の隣で `s_instance.ReleaseProfilerTelemetry()` も呼ぶ
- `Application.quitting` にも同様に登録する（`RegisterCameraSystemQuittingHandler` と同型）
- `OnAfterSceneLoadInitializationFailed` でも解放する
- 解放順: `Deactivate()` → `UnregisterElement()` → `Dispose()` → フィールドを null に戻す。**冪等にする**

**完了条件:** コンパイルが通る。`pwsh tools/run-tests.ps1` が green。

---

## 5. 完了条件と手順

### 5.1 Phase B（実装側）の完了条件

1. `pwsh tools/run-tests.ps1` が **exit 0（1 件以上実行 / failed 0）**
2. P-2 / P-3 のテストが新規に増えている（テスト総数が増えていること）
3. `git diff --stat` で 500 行を超えたファイル・50% 以上増えたファイルが無い
4. §3 の見積もりから ±30% を超えて外れたファイルがあれば **§6 に理由を書く**

> **Play Mode を回して `ProfilerSummary` が実際に出ることの確認は Phase B の完了条件に含めない。**
> Editor の Play Mode 実測は Phase C（Claude Code）が行う。§5.3 参照。

### 5.2 コマンド

```bash
pwsh tools/run-tests.ps1
```

- **Unity Editor を閉じた状態で実行すること**（プロジェクトロックで失敗する）
- 絞り込みは `pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Profiler`
- **テスト 0 件は失敗扱い**（コンパイルエラーが 0 件として現れるため）
- 結果は `TestResults/`（git 管理外）
- 終了コード `0xC0000005` でシャットダウンクラッシュしてもテスト結果自体は有効。ログ末尾の集計行を見ること

### 5.3 実地確認（Phase C が行う。**飛ばして完了としない**）

1. Unity Editor で Play Mode に入り、10 秒以上動かして止める
2. `%LOCALAPPDATA%\DebugStudio\telemetry\` の最新 NDJSON を開く
3. **`('sample','ProfilerSummary')` が 1 件以上あること** ← 本スライスの gate

> **gate 条件は「フィールドが `_field_caps` に現れること」ではなく
> 「そのパネルが参照する record が直近 run に 1 件以上あること」。**
> 前スライス（§7.7）が「K3-0 と同型の gate を踏んだ」として定めた条件である。

---

## 6. Phase C からの差し戻し / 実装側からの設計への異議

<!-- Phase B / C が追記する -->

### 6.1 Phase B（実装）から — §0.5 の設計レビュー結果

**§1 の設計判断に反対はない。実装を止める論点は無かった。** 根拠:

- **§1.2 の 3 分割は過剰分割ではない。** Policy は `TelemetryThresholds` だけに依存する純粋関数になり、
  境界値 9 本が Unity を起動せずに書けた。RecordFactory も `utcTicks` / `unityFrame` を引数で受けたことで
  `DateTime.UtcNow` を待たずに形状を検証できている。Emitter に残ったのは配線 60 行程度で、
  「テストが書けないロジック」は 1 つも残っていない。
- **§1.3 の「Debug 側に Layer を持つ」は UpdateSystem の契約に反していない。**
  `UpdateCoordinator.RegisterElement` が `GetOrCreateLayer(layerId, layerOrder)` を内部で呼ぶため事前宣言は不要で、
  既存の Layer 名（Camera / Streaming / Native）と `"Profiler"` は衝突しない（実測）。
  `UpdateLayerIds` に足さないことで Runtime → Debug の逆流も発生していない。

### 6.2 §5.1 条件 3 に抵触する箇所が 1 つある（**Phase C の判断を仰ぐ**）

`AppInitializer.cs` が **195 → 297 行（+52.3%）** となり、
§5.1 条件 3 の「50% 以上増えたファイルが無い」を満たしていない。
§3.2 の見積もり（250 行 = +28%）からの乖離は +18.8% で ±30% には収まっているため、
条件 4 には抵触しない。**見積もりの立て方ではなく、条件 3 の閾値そのものと衝突している。**

事実関係:

- 増分 102 行の内訳は `InitializeProfilerTelemetry`（52 行）/ `RegisterProfilerTelemetryQuittingHandler`（10 行）/
  `ReleaseProfilerTelemetry`（23 行）/ フィールド 3 本 + 呼び出し 4 箇所 + using 1 行。
  **これは §P-6 が「`InitializeCameraSystem` / `ReleaseCameraSystem` と同じ形で書く」と明示した指示どおりの複製である。**
- 責務は増えていない。増えたのは「常駐 Element を 1 つ所有する」という**既存責務の 2 件目**であり、
  §3.2 の見積もり 250 行は try/catch + quitting handler + 冪等 Release の 3 点セットを数え落としていた。
- ファイル自体は 297 行で、500 行のゲートからは十分遠い。

**実装側では分割していない。** §P-6 と A-3 が配置を設計判断として固定しているため、
実装側が独断で新しい型へ切り出すのは §0 の「範囲の上限」を越える。
常駐 Element の所有を `AppInitializer` から切り出すかどうかは Phase C / 次スライスの設計判断として残す。

### 6.3 移設に伴って落ちた挙動 2 件（いずれも意図的。C が見落としと誤認しないための記録）

1. **`DetectGcSpike` にあった `_logger.LogWarning("[Telemetry] GC spike: ...")` が消えた。**
   §P-4 の手順 8 が Emitter 側に要求しているのは `AppTelemetry.NotifyBottleneck` だけで、
   Emitter には logger を注入していないため。`DebugProfilerView` は一度も生成されていないので、
   実際に失われたログ出力は 0 件。ログも残すなら Emitter に `ILogger` を渡す配線が要る。
2. **`ProfilerSummary` の送出条件に `Thresholds != null` が加わった。**
   移設前は `AppTelemetry.IsEnabled` だけで出していたが、§2.1 の「全種別共通の前提」と
   §P-2 のテスト 2（`thresholds=null` なら全入力で `None`）に従った。
   `AbstractApplicationInitializer` が BeforeSceneLoad で `AppTelemetry.Thresholds` を設定してから
   Emitter が回り始めるため、実行時の挙動差は無い。

### 6.4 §P-2 のテスト 4 の実効性確認（要求されていた手順）

`ProfilerTelemetryPolicy.Decide` の `input.GcGen0Delta > thresholds.GcPerFrame` を
`>=` に書き換えて `pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Profiler.ProfilerTelemetryPolicyTests` を実行し、
**9 件中 1 件が赤くなることを確認した。**

```
GC差分が閾値ちょうどならGcSpikeは立たない
  Expected: None
  But was:  GcSpike
```

赤くなったのは当該 1 本だけで、他の 8 本は緑のまま。確認後に `>` へ戻してある。

### 6.5 PR #18 の cursor[bot] レビュー対応（唯一の非 Claude の目）

判定は **Approve（ブロッカーなし）**。指摘 4 件のうち 3 件を本ブランチで対応し、1 件は次スライスへ申し送る。

| ID | 指摘 | 対応 |
|---|---|---|
| **N2** | `Decide` が `None` のとき `SummaryUpdated` を落とさないため、テレメトリ無効から復帰した直後の 1 フレームで古いサマリが即出る | **直した。** `FrameTimeSampler.SummaryUpdated` は宣言自体が「読み取り後にリセットすること」と書いており、送出可否に関わらず読んだ時点で落とすのが元の契約に忠実。`OnElementLateUpdate` の先頭でローカルへ退避してから落とし、`EmitSummary` 側のリセットを外した |
| **N3** | Factory テストが GcSpike / UiCost の `tags` を固定していない（`AllocSpike\|Bottleneck` / `Bottleneck` が回帰しても赤にならない） | **直した。** `GcSpikeとUiCostのtagsが固定されている` を追加（Factory テスト 7 → 8 本） |
| **N4** | `DebugProfilerView.cs` に未使用 `using ZLogger;` が残っている | **直した。** ZLogger の拡張（`ZLogInformation` 等）は使っておらず、`LogInformation` は Microsoft.Extensions.Logging 側。移設前から未使用だったもので、§P-5 の「未使用 using を残さない」に合わせて削除 |
| **N1** | `AppTelemetry.IsEnabled == false` のとき、旧 View は GC/UI の `PushWarning` を出していたが、新 Policy は `None` を返すので `NotifyBottleneck` も止まる。旧は「警告表示」と「WriteRecord」が分離していたが、新は両方が `IsEnabled` に束ねられている | **直さない。申し送り。** 現状 View は生成されないため実害 0。「警告は `Level=Off` でも出すか」は表示側の要件であり、Canvas / UIToolkit 移植スライスで決める。§2.1 が全種別共通の前提として `IsEnabled` を要求している以上、実装側の独断で分離しない |

`AppInitializer` の +52.3%（§6.2）については **「本スライスでは許容。切り出さない判断に同意する。3 件目の常駐 Element が出た時点で Host/Owner へ切り出せばよい」** との回答があった。条件 3 を機械適用しての差し戻しは不要という判断。

**Play Mode 実測（§5.3）はレビューでも C1 として残件扱い。** 静的レビューのブロッカーではないが、製品ゲートとしては未達のまま。

---

## 7. Phase C レビュー

<!-- Phase C（Claude Code / Opus 5）が追記する -->

---

## 8. Phase C' 監査

<!-- Phase C'（Opus 4.8）が追記する -->

---

## 付録 A. 実装で踏みやすい罠（自己完結のため転記）

### A.1 Unity では `record` を使わない

このプロジェクトには `IsExternalInit` が無いため、**C# の `record` / `record struct` を 1 つでも書くと
プロジェクト全体がコンパイル不能になる。** 静的レビューでは絶対に出ない種類の事故である。
`ProfilerFrameInput` は `readonly struct` にすること。

### A.2 破棄済み `UnityEngine.Object` の偽 null

破棄済みの `UnityEngine.Object` は `== null` が true になるが、
**`?.` と `??` は Unity の `==` オーバーロードを迂回して短絡しない。**
`is null` / `ReferenceEquals` も同じく迂回する。

`UnityEngine.Object` 派生（`MonoBehaviour` / `GameObject` / `UIView` 等）に対しては
**`?.` `??` `??=` `is null` `ReferenceEquals` をすべて使わない。** `if (x == null)` と書く。

> `ProfilerTelemetryPolicy` / `RecordFactory` / `Emitter` は `UnityEngine.Object` を持たないので
> このルールの対象外だが、`DebugProfilerView` を編集するときは対象になる。

### A.3 触ってはいけない契約

- `SceneState` の enum 順序 — 整数比較でガードに使われている。**並べ替え禁止**
- `IAssetManagement` の `AssetOwner` 必須引数 — 変更しない
- asmdef の依存グラフ — §3.4 で許可した 1 行以外は変更しない
- `UpdateSystemRuntime` の 1 フレームの順序
  （`ActivatePendingRegistrations` → `RunUpdate` → `RunLateUpdate` → `ApplyMainThreadChanges` → `ApplyStructuralChanges`）

### A.4 テスト名とコメントは日本語で書く

既存テストに合わせる（例: `正本NDJSONはV1からV10で指摘0件である`）。
doc コメントには「何を守るためのテストか」「修正を外すとどう赤くなるか」を書く。

### A.5 `.meta` ファイル

新規 `.cs` を追加すると Unity が `.meta` を生成する。
**実装側で `.meta` を手書きしない。** `pwsh tools/run-tests.ps1`（バッチモード）が
アセットインポートを走らせるので、その過程で生成される。

### A.6 ドキュメントを新規作成しない

`unity/Assets/Docs/Architecture/` および `docs/` 配下に新しいドキュメントを作らない
（`.cursor/rules/docs-policy.mdc` §2）。この HANDOFF への追記（§6）は可。

---

## 付録 B. 参照する既存コードの所在（自己完結のため）

| 内容 | ファイル |
|---|---|
| 送出コードの現物（移設元） | `unity/Assets/OneStarMaker/Scripts/Debug/Profiler/DebugProfilerView.cs` L207〜L384 |
| `FrameTimeSampler` の API | `unity/Assets/OneStarMaker/Scripts/Debug/Profiler/FrameTimeSampler.cs` |
| 常駐 Element の前例 | `unity/Assets/SampleGame/DependOnAll/AppInitializer.cs` L91〜L193 |
| `IUpdateElement` 実装の前例 | `unity/Assets/OneStarMaker/Scripts/Runtime/CameraSystem/Hosting/CameraSystemUpdateElement.cs` |
| `RegisterElement` の挙動 | `unity/Assets/OneStarMaker/Scripts/Foundation/UpdateSystem/World/UpdateCoordinator.cs` L88〜L113 |
| 閾値の定義とデフォルト値 | `unity/Assets/OneStarMaker/Scripts/Foundation/Telemetry/TelemetryThresholds.cs` |
| `AppTelemetry` の公開 API | `unity/Assets/OneStarMaker/Scripts/Foundation/Telemetry/AppTelemetry.cs` L113〜L336 |
| テスト用 sink | `unity/Assets/OneStarMaker/Tests/Scene/TestDoubles/FakeTelemetrySink.cs` |
| config 読み出し | `unity/Assets/OneStarMaker/Scripts/Foundation/Config/AppConfig.cs` L42〜L62 |
