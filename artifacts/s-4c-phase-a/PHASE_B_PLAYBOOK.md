# S-4c Phase B 手順書（人間用・詳細）

正本は S-4c HANDOFF。食い違ったら HANDOFF。このファイルは「何を・どの順で・どの判定を書くか」だけを増やす。

想定: ローカル Unity 6000.6.0f1、このリポジトリの `unity/`、ブランチ `cursor/s-4c-world-lighting-a3-4a38`。Cloud では bake できない。

テスト（`pwsh tools/run-tests.ps1`）と Addressables build は Phase C。B の終わりは `pwsh tools/contract-audit.ps1` と「テスト未実行」。

---

## 0. 先にこれだけ頭に入れる

このスライスがやっていることは、ゲーム用語を外すと次の 1 文である。

> **世界の「空の色・霧・太陽」は同時に 1 人だけが書いてよい。その権利証が lease。権利証が古いと何も起きない。**

四季の Lighting Scene は、ロードされたときだけその権利証を持つ。Cell が落ちても権利証は季節 Lighting が持ったままなので、霧と太陽は春のまま残る。

CameraSystem との対応:

| CameraSystem | このスライス |
|---|---|
| `ICameraSystem` | `IRenderEnvironment` |
| `CameraSystem` | `RenderEnvironment` |
| `ICameraBackend` | `IRenderEnvironmentSink` |
| `CinemachineCameraBackend` | `UnityRenderEnvironmentSink` |
| View の Push Handle | `RenderEnvironmentLease` |

Backend / Sink は「装置への書き込み」。Environment は「誰が書いてよいか」。Lease は「今持っている権利証」。

やってはいけないこと（設計判断になるので、必要になったら Phase A に返す）:

- URP の `Volume` を C# から触る
- DontDestroyOnLoad に太陽を置く
- Framework に Spring / 季節 という語を書く
- `RenderSettings` を `PlayerScene` から書く
- asmdef を足す
- `record` / `init` を使う

---

## 1. いまの作業ツリー診断（2026-09-22）

骨格は置いてある。中身はまだ動かない。**下の完成形に置き換えてよい。** 部分的に直そうとすると、いまの誤解が残る。

| ファイル | 状態 | 直すポイント |
|---|---|---|
| `RenderEnvironmentState.cs` | ほぼ完成 | 未使用 `using JetBrains.Annotations` を消す。フィールドは get-only プロパティでも public readonly でも可。HANDOFF 文言は get-only |
| `IRenderEnvironment.cs` | 署名は正しい | 先頭に `#nullable enable`。同じ namespace の `using` は不要 |
| `IRenderEnvironmentSink.cs` | 完成に近い | そのままでよい |
| `RenderEnvironmentLease.cs` | 空 | `_currentState` を持たない。本体は Environment へ委譲するだけ |
| `RenderEnvironment.cs` | `NotImplemented` | owner / generation / bound sun を持つ。ここが本丸 |
| `RenderEnvironmentValidator.cs` | intensity だけ | `void Validate`。負の色・density・weight も投げる。bool を返さない |
| `UnityRenderEnvironmentSink.cs` | `NotImplemented` | B2 まで触らなくてよい |
| `FakeRenderEnvironmentSink.cs` | **作り直し** | Validator を呼ばない。sun から state を組み立てない。呼び出し回数を数えるだけ |
| `RenderEnvironmentLeaseTests.cs` | 空 | B1 のケース名を全部書く |
| SampleGame 配線 | 未着手 | B3 |
| Editor コンテンツ | 未着手 | B5 |

フォルダ名 `Implments` は typo。B0 で `Implements` にリネームする。

---

## 2. フォルダ（B 適応。公開 API は変えない）

namespace は全部 `OneStarMaker.Runtime.Rendering.Environments`。フォルダを分けても namespace を増やさない。

```text
unity/Assets/OneStarMaker/Scripts/Runtime/Rendering/Environments/
  IRenderEnvironment.cs
  IRenderEnvironmentSink.cs
  RenderEnvironmentLease.cs
  RenderEnvironmentState.cs
  Implements/                          ← いまの Implments をリネーム
    RenderEnvironment.cs
    UnityRenderEnvironmentSink.cs
  internal/
    RenderEnvironmentValidator.cs      ← class は internal。namespace は同じ

unity/Assets/OneStarMaker/Tests/Rendering/Environments/
  FakeRenderEnvironmentSink.cs
  RenderEnvironmentLeaseTests.cs
```

リネームは Unity の Project 窓で行う（`.meta` の GUID を保つ）。エクスプローラでフォルダだけ変えると meta が切れる。

Fake の namespace は `OneStarMaker.Tests.Rendering.Environments` でよい。

---

## 3. generation が分からないとき（ここが詰まる）

lease はオブジェクトの参照比較では古さを判定しない。**整数 `_generation` が権利証の番号**である。

初期値: `_generation = 0`、owner なし、太陽なし。

```text
Acquire("春")
  CaptureBaseline()
  owner = 春
  leaseA.Generation = 0 を渡す
  （この時点では _generation はまだ 0）

leaseA.Dispose()          ← 番号が一致するので「本物」
  RestoreBaseline()
  太陽参照を捨てる
  owner = なし
  _generation を 1 にする     ← これが要点。やらないと stale が壊れる

Acquire("夏")
  CaptureBaseline()
  owner = 夏
  leaseB.Generation = 1 を渡す

leaseA.Dispose()          ← 番号 0 ≠ 今の 1 なので「偽物」
  何もしない（夏を消さない、Restore しない）
```

`RenderEnvironment.Dispose()`（アプリ終了）も、owner がいれば Restore を **1 回** だけやり、owner を消し、`_generation++` する。そのあと古い lease の `Apply` は throw、`Dispose` は no-op。

**よくあるバグ:** matching Dispose で `_generation++` しない。すると春の古い lease が夏の番号と一致し、夏を消してしまう。

---

## 4. クラスが持つフィールド（これ以外を増やさない）

### 4.1 `RenderEnvironment`（Implements 配下）

```csharp
private readonly IRenderEnvironmentSink _sink;
private object? _ownerKey;
private int _generation;      // 初期 0
private Light? _boundSun;     // Unity 偽 null は == null
private bool _disposed;
```

公開:

- ctor(`IRenderEnvironmentSink sink`) … sink が null なら `ArgumentNullException`
- `HasActiveOwner` … `_ownerKey != null`
- `Acquire(object ownerKey)`
- `Dispose()`

internal（Lease からだけ呼ぶ。公開集合に足さない）:

- `BindSunFromLease(int generation, Light sun)`
- `ApplyFromLease(int generation, in RenderEnvironmentState state)`
- `ReleaseFromLease(int generation)`

### 4.2 `RenderEnvironmentLease`

```csharp
private readonly RenderEnvironment _environment;
public int Generation { get; }   // ctor でコピー。以後変えない
```

`_currentState` は持たない。Apply の正本は sink 側（Fake なら記録リスト）。

ctor は `internal`。`Acquire` だけが `new` する。

公開メソッドは委譲だけ:

- `BindSun` → `_environment.BindSunFromLease(Generation, sun)`
- `Apply` → `_environment.ApplyFromLease(Generation, state)`
- `Dispose` → `_environment.ReleaseFromLease(Generation)`

### 4.3 `FakeRenderEnvironmentSink`

記録専用。本物の見た目計算をしない。

```csharp
public int CaptureCount { get; private set; }
public int RestoreCount { get; private set; }
public bool SunBound { get; private set; }
public IReadOnlyList<RenderEnvironmentState> Applies { get; }
```

- `CaptureBaseline` … `CaptureCount++`
- `RestoreBaseline` … `RestoreCount++`
- `Apply` … `sun == null` なら `ArgumentNullException`。それ以外は `SunBound = true` して渡された **state をリストへ追加**。`sun.transform` から state を作らない。Validator を呼ばない（検証は Environment の仕事）

### 4.4 `RenderEnvironmentValidator`

`internal static`。`bool` を返さない。

```csharp
public static void Validate(in RenderEnvironmentState state)
```

失敗は全部 `ArgumentOutOfRangeException`:

- `SunIntensity < 0`
- `FogDensity < 0`
- `GlobalVolumeWeight` が 0..1 の外
- `SunColor` / `AmbientSkyColor` / `FogColor` の r/g/b/a のいずれかが負

### 4.5 `RenderEnvironmentState`

readonly struct。コンストラクタ 8 引数。HANDOFF §2.2 の順どおり。`init` 禁止。`record` 禁止。

---

## 5. メソッドの判定順（この順以外で書かない）

### `Acquire`

1. `ownerKey == null` → `ArgumentNullException`
2. `_disposed` → `ObjectDisposedException`
3. `_ownerKey != null` → `InvalidOperationException`（二件目即失敗。待ちも上書きもしない）
4. `_sink.CaptureBaseline()`
5. `_ownerKey = ownerKey`
6. `return new RenderEnvironmentLease(this, _generation)`

二件目で throw したとき、1 件目の owner も CaptureCount も変えない。

### `BindSunFromLease`

1. 権利証が無効（`_disposed` / 番号不一致 / owner なし）→ `InvalidOperationException`
2. すでに `_boundSun != null` → `InvalidOperationException`（1 lease につき 1 回）
3. `sun == null`（Unity 偽 null）→ `ArgumentNullException`。`sun?.` は使わない
4. `_boundSun = sun`

### `ApplyFromLease`

1. 権利証が無効 → `InvalidOperationException`（sink を触らない）
2. `_boundSun == null`（未 Bind、または破棄済み Light）→ `InvalidOperationException`
3. `RenderEnvironmentValidator.Validate(state)`
4. `_sink.Apply(state, _boundSun)`

### `ReleaseFromLease`（lease.Dispose）

1. `_disposed` → return（no-op）
2. `generation != _generation` → return（stale。Restore しない）
3. `_ownerKey == null` → return（二度目の Dispose。Restore しない）
4. `_sink.RestoreBaseline()`
5. `_boundSun = null`（Light を Destroy しない。参照を捨てるだけ）
6. `_ownerKey = null`
7. `_generation++`

### `RenderEnvironment.Dispose`（App 回収）

1. すでに `_disposed` → return
2. owner がいるときだけ `RestoreBaseline()` を 1 回
3. `_boundSun = null`、`_ownerKey = null`、`_generation++`、`_disposed = true`

---

## 6. B0. 準備

- [ ] `git status` がこのブランチ。関係ない dirty が無い（いまの Rendering 骨格は残してよい）
- [ ] `tools/unity-editor.cmd status`。閉じていればこの project を開く。別 project には繋がない
- [ ] Lighting window の Auto Generate を切る（忘れやすい。bake は B5）
- [ ] Project 窓で `Implments` → `Implements` にリネーム

---

## 7. B1. Fake で lease を先に通す（Scene を開かない）

Unity sink も SampleGame もまだ触らない。この塊が最低条件 1 である。

各ファイル先頭 `#nullable enable`。`record` 禁止。破棄されうる `Light` は `== null` / `!= null`。

### 7.1 完成形を置く順

1. `RenderEnvironmentState`（既存を掃除）
2. `RenderEnvironmentValidator`（作り直し）
3. `IRenderEnvironment` / `IRenderEnvironmentSink`（署名は現状でよい）
4. `FakeRenderEnvironmentSink`（作り直し）
5. `RenderEnvironment` + `RenderEnvironmentLease`（セットで書く。片方が空だとコンパイルできない）
6. `RenderEnvironmentLeaseTests`

### 7.2 Fake の完成形

```csharp
#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Runtime.Rendering.Environments;
using UnityEngine;

namespace OneStarMaker.Tests.Rendering.Environments
{
    public sealed class FakeRenderEnvironmentSink : IRenderEnvironmentSink
    {
        private readonly List<RenderEnvironmentState> _applies = new();

        public int CaptureCount { get; private set; }
        public int RestoreCount { get; private set; }
        public bool SunBound { get; private set; }
        public IReadOnlyList<RenderEnvironmentState> Applies => _applies;

        public void CaptureBaseline() => CaptureCount++;

        public void RestoreBaseline() => RestoreCount++;

        public void Apply(in RenderEnvironmentState state, Light sun)
        {
            if (sun == null)
            {
                throw new ArgumentNullException(nameof(sun));
            }

            SunBound = true;
            _applies.Add(state);
        }
    }
}
```

### 7.3 テスト共通部品

クラスに次を置く。`#if` で分けない。DontDestroyOnLoad は使わない。

```csharp
[TestFixture]
public sealed class RenderEnvironmentLeaseTests
{
    private FakeRenderEnvironmentSink _sink = null!;
    private RenderEnvironment _environment = null!;
    private GameObject? _sunGo;

    [SetUp]
    public void SetUp()
    {
        _sink = new FakeRenderEnvironmentSink();
        _environment = new RenderEnvironment(_sink);
    }

    [TearDown]
    public void TearDown()
    {
        _environment.Dispose();
        if (_sunGo != null)
        {
            UnityEngine.Object.DestroyImmediate(_sunGo);
            _sunGo = null;
        }
    }

    private Light CreateSun()
    {
        _sunGo = new GameObject("test-sun");
        var light = _sunGo.AddComponent<Light>();
        light.type = LightType.Directional;
        return light;
    }

    private static RenderEnvironmentState ValidState() => new(
        sunEulerDegrees: new Vector3(15f, -30f, 0f),
        sunColor: new Color(1f, 0.85f, 0.70f),
        sunIntensity: 0.80f,
        ambientSkyColor: new Color(0.18f, 0.22f, 0.28f),
        fogEnabled: true,
        fogColor: new Color(0.75f, 0.82f, 0.88f),
        fogDensity: 0.0015f,
        globalVolumeWeight: 1f);
}
```

Framework テストに `Spring` という語を書かない。数値は使ってよい。

### 7.4 最低ケース（名前は固定。中身の Arrange も固定）

`using NUnit.Framework;` と `#nullable enable` を付ける。

**`Acquire_First_Succeeds_AndHasOwner`**

- Act: `_environment.Acquire("a")`
- Assert: 例外なし、`HasActiveOwner == true`、`_sink.CaptureCount == 1`、`RestoreCount == 0`

**`Acquire_Second_Throws_AndKeepsFirstOwner`**

- Arrange: `var first = _environment.Acquire("a");`
- Act: `_environment.Acquire("b")` が `InvalidOperationException`
- Assert: `HasActiveOwner == true`、`CaptureCount == 1`（二件目は Capture しない）、`first` はまだ使える

**`Dispose_Matching_ClearsOwner_AndRestoresBaseline`**

- Arrange: `var lease = Acquire("a");`
- Act: `lease.Dispose();`
- Assert: `HasActiveOwner == false`、`RestoreCount == 1`

**`Dispose_StaleAfterNewOwner_DoesNotClearNewOwner_AndDoesNotRestore`**

Arrange は必ずこの 4 手。二件目 throw 中の Dispose を stale と書かない。

```csharp
var leaseA = _environment.Acquire("a");
leaseA.Dispose();
var leaseB = _environment.Acquire("b");
leaseA.Dispose();
```

Assert: `HasActiveOwner == true`、`RestoreCount == 1`（A の本物だけ。A の stale では増えない）

**`Apply_OnDisposedLease_Throws_AndDoesNotMutateSink`**

```csharp
var sun = CreateSun();
var lease = _environment.Acquire("a");
lease.BindSun(sun);
lease.Apply(ValidState());
lease.Dispose();
Assert.That(_sink.Applies.Count, Is.EqualTo(1));
Assert.Throws<InvalidOperationException>(() => lease.Apply(ValidState()));
Assert.That(_sink.Applies.Count, Is.EqualTo(1));
```

**`Apply_StaleLeaseAfterNewOwnerApply_Throws_AndDoesNotMutateNewOwner`**

```csharp
var sun = CreateSun();
var leaseA = _environment.Acquire("a");
leaseA.BindSun(sun);
leaseA.Apply(ValidState());
leaseA.Dispose();

var leaseB = _environment.Acquire("b");
leaseB.BindSun(sun);
var stateB = new RenderEnvironmentState(
    new Vector3(50f, 40f, 0f),
    new Color(1f, 0.98f, 0.90f),
    1.30f,
    new Color(0.28f, 0.30f, 0.32f),
    true,
    new Color(0.70f, 0.78f, 0.85f),
    0.0040f,
    1f);
leaseB.Apply(stateB);

Assert.Throws<InvalidOperationException>(() => leaseA.Apply(ValidState()));
Assert.That(_sink.Applies.Count, Is.EqualTo(2));
Assert.That(_sink.Applies[1].SunIntensity, Is.EqualTo(1.30f));
```

**`Dispose_Twice_IsIdempotent`**

- `Acquire` → `Dispose` → `Dispose`
- `RestoreCount == 1`、二度目で例外なし

**`Acquire_NullOwner_Throws`**

- `Acquire(null!)` が `ArgumentNullException`
- `HasActiveOwner == false`、`CaptureCount == 0`

**`Apply_BeforeBindSun_Throws`**

- `Acquire` のあとすぐ `Apply(ValidState())` が `InvalidOperationException`
- `Applies` は空

**`BindSun_Twice_Throws`**

- `BindSun(sun)` のあと、もう一度 `BindSun(sun)` が `InvalidOperationException`

**`BindSun_NullLight_Throws`**

- `BindSun(null!)` が `ArgumentNullException`

**`Validate_NegativeIntensity_Throws`**

Validator を直接呼んでよい（Runtime は Tests に `InternalsVisibleTo` 済み）。

```csharp
var bad = new RenderEnvironmentState(
    Vector3.zero, Color.white, -1f, Color.white, false, Color.white, 0f, 1f);
Assert.Throws<ArgumentOutOfRangeException>(() => RenderEnvironmentValidator.Validate(bad));
```

**`DisposeEnvironment_WithActiveLease_RestoresOnce_AndLeaseDisposeIsNoOp`**

```csharp
var sun = CreateSun();
var lease = _environment.Acquire("a");
lease.BindSun(sun);
lease.Apply(ValidState());
_environment.Dispose();
Assert.That(_sink.RestoreCount, Is.EqualTo(1));
Assert.Throws<InvalidOperationException>(() => lease.Apply(ValidState()));
lease.Dispose();
Assert.That(_sink.RestoreCount, Is.EqualTo(1));
```

B では `pwsh tools/run-tests.ps1` を走らせない。Editor が開いていればコンパイルだけ確認する。

---

## 8. B2. Unity sink

`Implements/UnityRenderEnvironmentSink.cs`。`public sealed class`。URP の using を書かない。

持つフィールド（RenderSettings のコピーだけ。Light の baseline は持たない）:

- fog / fogMode / fogColor / fogDensity
- ambientMode / ambientLight
- 取ったかどうかの bool

`CaptureBaseline` … 上記を `RenderSettings` から読む。

`Apply`:

1. `sun == null` → 何も書かず `ArgumentNullException`
2. `sun.type != LightType.Directional` → 何も書かず `InvalidOperationException`（type を書き換えない）
3. `sun.transform.rotation = Quaternion.Euler(state.SunEulerDegrees)`
4. `sun.color` / `sun.intensity` を state どおり
5. `RenderSettings.fog = state.FogEnabled`
6. `RenderSettings.fogMode = FogMode.ExponentialSquared`（state に FogMode は無い。四季共通）
7. fogColor / fogDensity
8. `RenderSettings.ambientMode = AmbientMode.Flat`
9. `RenderSettings.ambientLight = state.AmbientSkyColor`
10. `GlobalVolumeWeight` は読まない。コメントを残す: 「S-4c は URP 参照を足さない。weight は preset と Fake の観測用。」

`RestoreBaseline` … Capture した RenderSettings だけ戻す。Capture 前なら no-op でよい。

`AmbientMode` は `UnityEngine.Rendering.AmbientMode`。これは URP パッケージではない。asmdef 追加は不要。

---

## 9. B3. App 配線

ここから SampleGame。Framework に季節語を出さない。

### 9.1 `SeasonLightingPresetTable`

場所: `unity/Assets/SampleGame/InGame/InGameSession/World/SeasonLightingPresetTable.cs`

```csharp
internal static class SeasonLightingPresetTable
{
    public static bool TryGet(string identity, out RenderEnvironmentState state)
}
```

未知 identity は `false`、`state = default`。数値は HANDOFF §2.3 を写す。「もっといい色」にしない。

### 9.2 `SeasonLightingScene`

constructor に `IRenderEnvironment renderEnvironment` を追加。null なら `ArgumentNullException`。フィールドに保持。

`FindRootComponent<Light>()` は使わない。最初の Light が SeasonSun とは限らない。既存の `RootObjects` を回す。

```csharp
private Light FindSeasonSun()
{
    foreach (var root in RootObjects)
    {
        if (root == null)
        {
            continue;
        }

        var lights = root.GetComponentsInChildren<Light>(true);
        foreach (var light in lights)
        {
            if (light == null)
            {
                continue;
            }

            if (light.gameObject.name != "SeasonSun")
            {
                continue;
            }

            if (light.type != LightType.Directional)
            {
                continue;
            }

            return light;
        }
    }

    throw new InvalidOperationException(
        $"SeasonSun Directional Light was not found in {SceneResource.Identity}.");
}
```

`OnLoadedImpl` の順序（HANDOFF 固定。入れ替えない）:

```text
1. sun = FindSeasonSun()          ← 失敗しても Acquire しない
2. TryGet(identity, out state)    ← false なら throw。まだ Acquire しない
3. _lease = _renderEnvironment.Acquire(this)
4. try {
     _lease.BindSun(sun)
     _lease.Apply(state)
   } catch {
     _lease.Dispose()
     _lease = null
     throw
   }
5. 成功時は finally で Dispose しない。_lease をフィールドに残す
```

`OnPreUnLoadedImpl`:

```csharp
if (_lease != null)
{
    _lease.Dispose();
    _lease = null;
}
```

`_lease?.Dispose()` は C# オブジェクトなので使ってよい。`Light` に `?.` は使わない。

`OnAfterUnLoadedImpl` に Dispose を足さない。Load 失敗は PreUnLoad を通らないので、Acquire 後の失敗は catch で自分で返す。

### 9.3 `PlayerScene`

やること: `ApplyDemoLook();` の呼び出しを消す。メソッド本体も消す。

消してよいもの: メソッド内の `RenderSettings` 全部と、デモ用の Camera clear。Camera の既定 clear は `AppInitializer` / `InGameScene` が既に持っている。

残すもの: Camera bind、Flyer、WorldReady。

`using UnityEngine;` が RenderSettings 以外に必要なら残す。`RenderSettings` 参照が 0 件になること。

### 9.4 `GameSceneFactory`

必須引数を 1 つ足す。既存 4 引数の後ろ。

```csharp
IRenderEnvironment renderEnvironment
```

null は `ArgumentNullException`。フィールドに保持。

渡す相手は次の 4 identity だけ。他 Scene は受け取らない。

- `Spring_Lighting`
- `Summer_Lighting`
- `Autumn_Lighting`
- `Winter_Lighting`

`Spring_Lighting_4_2` は Factory の switch に出さない。親が `StreamByDistance` なら既存分岐で `CellCompanionScene` になる。lease を取らない。

### 9.5 `AppInitializer`

Camera の Release に埋め込まない。独立メソッドにする。

フィールド:

```csharp
private RenderEnvironment? _renderEnvironment;
private bool _renderEnvironmentQuittingHandlerRegistered;
```

using:

```csharp
using OneStarMaker.Runtime.Rendering.Environments;
```

`Before()` の `InitializeCameraSystem();` の直後（`InitializeProfilerTelemetry` より前でも後でもよいが、Factory より前）:

```csharp
s_instance.InitializeRenderEnvironment();
```

`InitializeRenderEnvironment`:

```csharp
private void InitializeRenderEnvironment()
{
    if (_renderEnvironment != null)
    {
        return;
    }

    _renderEnvironment = new RenderEnvironment(new UnityRenderEnvironmentSink());
    if (_renderEnvironmentQuittingHandlerRegistered)
    {
        return;
    }

    Application.quitting += ReleaseRenderEnvironment;
    _renderEnvironmentQuittingHandlerRegistered = true;
}
```

`CreateSceneFactory` の `new GameSceneFactory(...)` に `_renderEnvironment` を渡す。null なら `InvalidOperationException`（Camera と同じ文言パターン）。

`ReleaseRenderEnvironment`:

```csharp
private void ReleaseRenderEnvironment()
{
    Application.quitting -= ReleaseRenderEnvironment;
    _renderEnvironmentQuittingHandlerRegistered = false;
    _renderEnvironment?.Dispose();
    _renderEnvironment = null;
}
```

呼び先（Camera と同じタイミング、別メソッド）:

1. `Sub()` … `ReleaseCameraSystem();` の隣
2. `OnAfterSceneLoadInitializationFailed` … `ReleaseCameraSystem();` の隣
3. `Application.quitting` … 上の登録

Host GO は作らない。

---

## 10. B4. テスト更新

### 10.1 Factory

`GameSceneLoggingTests.cs` の `new GameSceneFactory(` は全部、第 5 引数に Fake を足す。null 拒否テストの `null!` 個数も 1 つ増える。

`CreateFactory` ヘルパーに `new FakeRenderEnvironmentForFactory()` を足す。Acquire を呼ばないので、こうで足りる:

```csharp
private sealed class FakeRenderEnvironmentForFactory : IRenderEnvironment
{
    public bool HasActiveOwner => false;
    public RenderEnvironmentLease Acquire(object ownerKey)
        => throw new System.NotImplementedException();
}
```

追加テスト:

1. `Constructor_NullRenderEnvironment_Throws`
2. 既存 `CreateSceneClass_SeasonLightingWithoutCellParent_IsNotCompanion` は維持
3. 親が `StreamByDistance`、子 identity が `Spring_Lighting_4_2` → `CellCompanionScene`

```csharp
var parent = SceneTestHelper.CreateSceneResource("opaque-parent", streamByDistance: true);
var child = SceneTestHelper.CreateSceneResource("Spring_Lighting_4_2", parent: parent);
```

### 10.2 preset 表

`unity/Assets/OneStarMaker/Tests/SampleGame/SeasonLightingPresetTableTests.cs`

4 identity が HANDOFF §2.3 の数値と一致。未知は `false`。

### 10.3 Cell Lighting Directional 0

B5 の直後に 1 件。B4 の今は書かない。

---

## 11. B5. Editor コンテンツ

YAML を手で編集しない。`tools/unity-editor.cmd` の名前付きコマンド、足りなければ `eval`。

### 11.1 Cell Lighting 2 件

メニュー `OneStarMaker/World Workspace/Open Window`

1. Spring / x=4 / y=2 / Lighting / Full → optional が無いので Create → `Spring_Lighting_4_2`
2. 同様に `(5,2)`
   Create は Node / Edges / Map / Addressables まで。SceneGraph Editor の Layout と開いている GraphView は更新されない。今はそれでよい。Editor から同じ identity を作り直さない。
3. VFX / Events は作らない
4. Empty に Directional が居ないことを Hierarchy で確認してから Point `LocalFill` を 1 つ
   - `(4,2)`: Cell 中心、地上 4m 目安
   - `(5,2)`: 隣接 Cell 中心
   - range ≤ 80
   - **Directional は 0**

### 11.2 四季の太陽

`Spring_Lighting` `Summer_Lighting` `Autumn_Lighting` `Winter_Lighting` それぞれ:

- `SeasonLightingRoot` の子に GameObject 名 **`SeasonSun`**（完全一致）
- Light Type = Directional
- euler / color / intensity = preset 表
- Lightmap Static オン

任意: 同じ Scene に global Volume（isGlobal）。Runtime は触らない。lease 所有の判定に Volume を使わない。

### 11.3 bake（7 Scene だけ）

1. Auto Generate **off**
2. Full Cell を **Single** で開いて他を閉じる。Whitebox を開かない
3. World Workspace の Lighting Open（4 Scene）のまま Generate しない
4. Additive でこの 7 件だけ:
   - `Spring_Lighting`
   - `Spring_Cell_4_2`（Full。Whitebox パスではない）
   - `Spring_Environment_4_2`
   - `Spring_Lighting_4_2`
   - `Spring_Cell_5_2`
   - `Spring_Environment_5_2`
   - `Spring_Lighting_5_2`
5. Hierarchy が 7 件だけ。8 件目があれば閉じる
6. Lighting Settings `Assets/SampleGame/InGame/InGameSession/Seasons/Spring/Spring_Representative.lighting` を 7 件に割り当て。bakedGI on、realtime GI off。他 Scene の LightingSettings は触らない
7. Generate Lighting
8. 開いている 7 件だけ保存。**Save All しない**
9. `git status`: Whitebox や他 Cell が dirty なら戻す
10. commit してよいもの:
    - `Spring_Representative.lighting` と `.meta`
    - 7× `.unity` の LightingSettings / LightingData 参照
    - 各 Scene 近傍の `LightingData.asset` / lightmap `.exr` と `.meta`
    - 新規 2 Cell Lighting の scene / resource / node / map / Addressables 差分
    - `Library/` は不可。7 件以外の `.unity` は不可
11. `(4,2)` から東の `(5,2)` を見て、明白な明るさ段差が無いかを HANDOFF §6 に書く
12. Editor で Cell Lighting を閉じても sun/fog が春のままであること
13. Directional 0 の EditMode テストを 1 件足す

---

## 12. B6 / B7. 終わる前

```text
rg "RenderSettings" unity/Assets/SampleGame --glob "*.cs"
rg "Season|Spring|Summer|Autumn|Winter|季節" unity/Assets/OneStarMaker/Scripts -g "*.cs"
pwsh tools/contract-audit.ps1
pwsh tools/docs-audit.ps1
```

SampleGame の `RenderSettings` は 0。Framework の季節語は 0。Unity sink の `RenderSettings` は Runtime Rendering にあってよい。

HANDOFF §6 を埋める:

- 実装したこと
- HANDOFF との差（B 適応だけ。契約を変えたら A 再開）
- 未実行: Unity テスト未実行、を必ず書く
- implementation head SHA

Phase C を走らせない。

---

## 13. 止めて Phase A に返すとき

HANDOFF §4.3。典型:

- URP 参照が要る
- Host 太陽が要る
- World Workspace で 2 件作れない
- bake が 7 件以外の GUID を大量に変える
- lease を Unity なしで試験できない

「失敗した」「見た目が微妙」だけでは再開しない。preset 数値の変更は再開。

B 適応でよい例: Find のループの書き方、テスト関数の分割、Light の range、LightingSettings の sample 数、`Implements` リネーム、コメント。

---

## 14. よくある詰まり

| 症状 | 原因 | 直し方 |
|---|---|---|
| 古い lease の Dispose で新しい季節が消える | matching Dispose で `_generation++` していない | §5 の ReleaseFromLease 7 番 |
| Fake の Apply が例外だらけ | Fake が Validator を呼んでいる / sun から state を作っている | §7.2 に置き換える |
| `BindSun` のテストが EditMode で落ちる | Light を作っていない、または TearDown していない | `CreateSun` + `DestroyImmediate` |
| 二件目 Acquire のあと 1 件目まで死ぬ | throw の前に owner を消している | Acquire は失敗したら何も変えない |
| Load 失敗なのに lease が残る | Acquire を太陽探索の前にやっている | Acquire は最後。失敗 catch で Dispose |
| Game に `RenderSettings` が残る | `ApplyDemoLook` の Camera 部分だけ消した | メソッドごと削除 |
| コンパイルで internal が見えない | Fake から Validator を呼んでいる | Fake は記録だけ。Validate は Tests から直接でよい |
| `sun?.intensity` | Unity 偽 null | `sun == null` のあと素のアクセス |

---

## 15. Phase C 担当へ渡すメモ（B ではやらない）

- 全 EditMode 回帰（空 filter）
- Play: companion Lighting または Full。Cell を落としても fog/sun が春のまま。戻して baked 床
- C' は Claude または GPT、新規セッション、blind bundle。Grok でやらない
