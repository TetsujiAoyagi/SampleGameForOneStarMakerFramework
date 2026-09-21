# S-4c Phase B チェックリスト（人間用）

正本は `docs/handoff/S-4c_WORLD_LIGHTING.md`。食い違ったら HANDOFF。このファイルは順序と「開く場所」だけを増やす。

想定: ローカル Unity 6000.6.0f1、このリポジトリの `unity/`、ブランチ `cursor/s-4c-world-lighting-a3-4a38`。Cloud では bake できない。

テスト（`pwsh tools/run-tests.ps1`）と Addressables build は Phase C。B の終わりは `pwsh tools/contract-audit.ps1` と「テスト未実行」。

---

## 0. 開く前

- [ ] `git status` がこのブランチで、関係ない dirty が無い
- [ ] `tools/unity-editor.cmd status`。閉じていればこの project を開く。別 project の Editor には繋がない
- [ ] Lighting window の Auto Generate を切る（bake のとき忘れやすい）

HANDOFF を印刷するなら §2（境界）と §4（手順）と §2.4（API）だけでも足りる。preset 表は §2.3。

---

## 1. Framework（Rendering フォルダ）

作る場所: `unity/Assets/OneStarMaker/Scripts/Runtime/Rendering/`

先頭はすべて `#nullable enable`。`record` 禁止。`init` 禁止。

| 順 | ファイル | 要点 |
|---|---|---|
| 1 | `RenderEnvironmentState.cs` | readonly struct。コンストラクタ 8 引数。get-only。HANDOFF §2.2 の型どおり |
| 2 | `RenderEnvironmentValidator.cs` | **internal** 静的。負の intensity / density、weight が 0..1 外、色の負 → `ArgumentOutOfRangeException` |
| 3 | `IRenderEnvironmentSink.cs` | **public**。`Apply(in RenderEnvironmentState state, Light sun)` — `Light?` にしない |
| 4 | `IRenderEnvironment.cs` | **public**。`HasActiveOwner` と `Acquire(object ownerKey)` |
| 5 | `RenderEnvironmentLease.cs` | generation。`BindSun` 1 回。`Apply` は generation 一致かつ Bind 済。Dispose は一致なら Restore + Light を捨てる。不一致は no-op |
| 6 | `RenderEnvironment.cs` | **public sealed, IDisposable**。ctor は sink 必須。Acquire は占有中なら `InvalidOperationException`。成功で `CaptureBaseline`。`Dispose` は Restore 1 回 + owner クリア + **generation++** |
| 7 | `UnityRenderEnvironmentSink.cs` | **public sealed**（AppInitializer が new する）。RenderSettings だけ Capture/Restore。`sun == null` は Unity 偽 null。`sun?.` 禁止。FogMode は ExponentialSquared 固定。AmbientMode は Flat 固定。`GlobalVolumeWeight` は読まない |

asmdef は触らない。URP using を書かない。

Fake は Runtime に置かない。`unity/Assets/OneStarMaker/Tests/Rendering/FakeRenderEnvironmentSink.cs`。

---

## 2. テストを先に（B では実行しないがコンパイルはする）

`RenderEnvironmentLeaseTests` の最低ケース名は HANDOFF B1 に固定。stale は次の 4 手以外で書かない:

`Acquire(A) → Dispose(A) → Acquire(B) → Dispose(A)`

`BindSun` が Light を要るケースは `new GameObject` + `AddComponent<Light>()`、TearDown で `DestroyImmediate`。DontDestroyOnLoad 禁止。

`SeasonLightingPresetTableTests`: 4 identity が HANDOFF §2.3 の数値と一致。未知は false。

---

## 3. SampleGame

`SeasonLightingPresetTable`（internal static）:

- `TryGet(string identity, out RenderEnvironmentState state)`
- 表は HANDOFF §2.3 を写す。数値を「もっといい色」にしない

`SeasonLightingScene`:

- constructor に `IRenderEnvironment` 追加。null 拒否
- `OnLoadedImpl` の順序は HANDOFF B3 の疑似コードどおり（Acquire は太陽と preset の後）
- `SeasonSun` は name 完全一致、`LightType.Directional`
- 成功したら `_lease` を保持。失敗したら catch で Dispose して `_lease = null`
- `OnPreUnLoadedImpl` で Dispose。`OnAfterUnLoadedImpl` に足さない

`PlayerScene`: `ApplyDemoLook` とその呼び出しを削除。`RenderSettings` が残っていたら失敗

`GameSceneFactory`: 第 5 引数 `IRenderEnvironment`。4 つの `*_Lighting` に渡す。null 拒否

`AppInitializer`:

- `InitializeCameraSystem` の直後に `new RenderEnvironment(new UnityRenderEnvironmentSink())`
- `CreateSceneFactory` に渡す
- **`ReleaseRenderEnvironment` を独立メソッド**にする（Camera の Release に埋め込まない）
- `Sub` / quitting / `OnAfterSceneLoadInitializationFailed` から呼ぶ

Factory テストの全 `new GameSceneFactory` に Fake environment を足す。`Spring_Lighting_4_2` + StreamByDistance 親 → `CellCompanionScene` を 1 件。

---

## 4. Editor コンテンツ（ここが本体の半分）

YAML を編集しない。`tools/unity-editor.cmd command` の名前付き、足りなければ `eval`。

### 4.1 Cell Lighting 2 件

メニュー `OneStarMaker/World Workspace/Open Window`

1. Spring / x=4 / y=2 / Lighting / Full → optional が無いので Create → `Spring_Lighting_4_2`
2. 同様に `(5,2)`
3. VFX / Events は作らない
4. Empty に Directional が居ないことを Hierarchy で確認してから Point `LocalFill` を 1 つ

### 4.2 四季の太陽

`Spring_Lighting` `Summer_Lighting` `Autumn_Lighting` `Winter_Lighting` それぞれ:

- `SeasonLightingRoot` の子に `SeasonSun`（Directional）
- euler / color / intensity = preset 表
- Lightmap Static オン

### 4.3 bake（7 Scene だけ）

1. Auto Generate off
2. Full Cell を **Single** で開いて他を閉じる。Whitebox を開かない
3. World Workspace の Lighting Open（4 Scene）のまま Generate しない
4. Additive で 7 件:
   `Spring_Lighting`, `Spring_Cell_4_2`, `Spring_Environment_4_2`, `Spring_Lighting_4_2`,
   `Spring_Cell_5_2`, `Spring_Environment_5_2`, `Spring_Lighting_5_2`
5. Hierarchy が 7 件だけ
6. `Spring_Representative.lighting` を 7 件に割り当て（bakedGI on、realtime GI off）
7. Generate Lighting
8. 7 件だけ保存。Save All しない
9. `git status`: Whitebox や他 Cell が dirty なら戻す
10. `(4,2)`↔`(5,2)` の床に明白な段差が無いかを HANDOFF §6 に書く

commit してよいファイル種別は HANDOFF B5-10。

Directional 0 の EditMode テストをこのあと 1 件足す。

---

## 5. 終わる前

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

## 6. 止めて Phase A に返すとき

HANDOFF §4.3。典型:

- URP 参照が要る
- Host 太陽が要る
- World Workspace で 2 件作れない
- bake が 7 件以外の GUID を大量に変える
- lease を Unity なしで試験できない

「失敗した」「見た目が微妙」だけでは再開しない。preset 数値の変更は再開。

---

## 7. Phase C 担当へ渡すメモ（B ではやらない）

- 全 EditMode 回帰（空 filter）
- Play: companion Lighting または Full。Cell を落としても fog/sun が春のまま。戻して baked 床
- C' は Claude または GPT、新規セッション、blind bundle。Grok でやらない
