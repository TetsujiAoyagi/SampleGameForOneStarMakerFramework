# S-4c World Lighting — HANDOFF

> Phase B の正本。ここに無い公開 API・依存・所有者・寿命・失敗契約は実装しない。衝突したら Phase A へ返す。

## 0. メタデータ

- type: `slice`
- status: `C`（§7.4 判定保留：全 EditMode 909件成功。Play必須観測未完了。GO / NO-GO 未確定、C' 未実施）
- branch: `cursor/s-4c-world-lighting-a3-4a38`
- implementation base commit: `553b7b150e13245b369d75dc4baa12d86e9559aa`
- implementation head commit: `87a1cd297a0b96bb9743b3e47654a27130ebcffe`（今回の Phase C 判定対象。review record は §7.4）
- risk: `high`
- owner: Phase B は人間
- created: 2026-09-21
- A3 frozen: 2026-09-21
- expires: S-4d の Phase D 完了時
- harvest to: Architecture §24（REN-08 現況）、§05（Season Lighting lease）、§23 Volume 境界の確認、§27 フォルダ（Cell Lighting 2 件）、§11（Workspace は SceneGraph Editor を通さない現況）、STREAMING には距離政策を足さない
- Phase A snapshot path: `artifacts/s-4c-phase-a/`
- Phase A snapshot generated at: 2026-09-21
- Phase A snapshot hash: A1-snapshot `38be0c9de7a839e4ddb4d2615d1cd1bae39db0c5d961d2df6df10da1d750ca12`（A2 入力。A3 本文はこの HANDOFF）
- A2 入力: `artifacts/s-4c-phase-a/A0-packet.md` + `A1-snapshot.md`
- A2 結果: `A2-architecture.md` / `A2-lifecycle.md` / `A2-alternative.md`
- A3 決定: `artifacts/s-4c-phase-a/A3-frozen.md`
- Phase B 手順の読み順: 本 HANDOFF が正本。人間向け詳細手順は `artifacts/s-4c-phase-a/PHASE_B_PLAYBOOK.md`（矛盾したら HANDOFF）。2026-09-22 に Playbook をフィールド・判定順・現行骨格の直し方まで詳細化した
- Phase B result snapshot path / id: 本 HANDOFF §6。今回の固定版は `artifacts/s-4c-phase-c-rerun-87a1cd2/phase-b-result.txt`（source: implementation head 時点の §6）。
- evidence bundle: `artifacts/s-4c-phase-c-rerun-87a1cd2/manifest.json`。C' blind bundle: 未生成（Phase C 保留につき起動しない）。

## 1. 目的と対象外

- 目的: 季節グローバル照明の単一オーナー（lease）を Framework に置き、PlayerScene の `RenderSettings` 直書きを廃し、Spring 代表 2 Cell で multi-scene bake を成立させる。
- 対象外: `IRenderingSystem` / RenderWorld / BRG、24h TimeOfDay、Weather 合成、全 216 bake、Light Probe 再評価（S-9）、VFX / Events（S-4d）、Tunnel 遷移（S-5）、新しい LoadType / SceneState、Framework への季節語、一時生成器の復活、Whitebox bake、URP パッケージ追加。
- 現況: A0 packet `artifacts/s-4c-phase-a/A0-packet.md` に固定。Season Lighting 4 件は空 scaffold。Cell Lighting 0 件。`PlayerScene.ApplyDemoLook` が fog / ambient を直書き。Runtime は URP 非参照。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: App lifetime の単一 RenderEnvironment が lease で **sun / ambient / fog** を所有でき、Spring `(4,2)` `(5,2)` の multi-scene bake が Scene 単位の着脱として成立するか。共通 Volume の実行時所有（`Volume.weight`）はこの問いの判定に含めない。

- 進める最低条件:
  1. 純 C# テストが、単一 owner・二件目即失敗・stale Dispose / stale Apply が新 owner を消さないこと、解放で baseline に戻ることを示す。
  2. Game 層（少なくとも `PlayerScene`）から `RenderSettings` 代入が消えている。
  3. 四季の `SeasonLightingScene` が Load で lease を取り preset を Apply し、Unload で解放する。二件目の Season Lighting が Load したら失敗する。
  4. `Spring_Lighting_4_2` と `Spring_Lighting_5_2` が World Workspace で作られ、Directional Light を持たず、global sun / sky / fog / global Volume を書き換えない。
  5. 代表 7 Scene（Season Lighting + 両 Cell + 両 Environment + 両 Cell Lighting）を同時に開いた Progressive bake の lightmap が commit されている。
  6. 人間が `(4,2)` / `(5,2)` 境界で明白な lightmap seam が無いことを HANDOFF 実装結果へ記録する。
  7. Phase C が次を観測できる: Cell を Unload しても fog/sun は春 preset のまま（lease は Season Lighting が持つ）。同じ Cell を戻すと baked 床が再ロードされる。

- 受け入れ条件（上記の観測可能な詳細。別バーにしない）:
  - `IRenderEnvironment.Acquire(object ownerKey)` は空きなら lease を返す。既に owner がいるなら `InvalidOperationException`。暗黙の待ち・上書きはしない。
  - lease は整数 generation を token として持つ。Dispose は generation 一致のときだけ owner を空ける。不一致は no-op。
  - Dispose 済み lease の `Apply` は `InvalidOperationException`。新 owner の state は変わらない。
  - 同一 lease の二度目の Dispose は no-op。
  - `RenderEnvironmentState` は struct（`record` 禁止）。フィールドは本節の表に固定した値だけ。
  - SampleGame の preset 表が四季 identity → state を返す。Framework は Spring 等の語を持たない。
  - Unity sink は `Light`（bind された太陽）と `RenderSettings`（fog / ambient）へ書く。`Volume.weight` の URP 型操作は **しない**（後述裁定）。
  - `SeasonLightingScene` は `OnLoadedImpl` で **Acquire の前に** `SeasonSun` 探索と preset lookup を済ませる。欠ける太陽・未知 identity では lease を取らない。Acquire 後の失敗だけ `try/catch` で Dispose してから throw（`OnLoadedImpl` 例外は `OnPreUnLoadedImpl` を通らない）。成功時は `_lease` を保持し、`OnPreUnLoadedImpl` で Dispose。
  - `PlayerScene.ApplyDemoLook` を削除する。Camera clear / background は CameraSystem 側の既存設定を残し、RenderEnvironment は触らない。
  - Cell Lighting 2 件の Scene 内に `LightType.Directional` が 0。local Point を各 1 以上置く。
  - bake 対象 Scene 以外の `.unity` を再シリアライズしない。Whitebox は焼かない。
  - Light Probe 共有は受け入れ事実として実装結果と S-9 送り状に書く。本スライスで Probe 方式を変えない。

- ここでは答えない問いと所有する後続スライス:
  - URP `Volume.weight` を Framework adapter が直接書くこと、共通 Volume の実行時所有 → 後続 `RenderEnvironment Volume 反映`（本スライスは authored Volume を Scene に置いてよいが、Runtime は触らない。lease 所有の GO 判定に Volume を使わない）
  - S-4d は `Spring_Lighting_4_2` / `Spring_Lighting_5_2` を再作成しない（本スライスが作る）
  - 直列季節切替のあいだ RenderSettings が Capture 時点へ一瞬戻ること → S-5
  - 季節遷移シーケンスでの lease 受け渡し → S-5
  - 全 Cell bake / Probe 再評価 / 共有 Probe のメモリ → S-9
  - VFX Graph、Atmosphere VFX、Events、major Event → S-4d
  - 品質バー（3 秒で主題と変奏が分かる）の完成目視 → S-8
  - TimeOfDay 時計 → REN-08 残り / P-R6
  - Camera skybox material の季節別差し替え → 後続。本スライスは fog / ambient / sun で変奏を載せる
  - World Workspace 作成が SceneGraph Editor（ViewModel / Layout / 開いている GraphView）を更新しないこと → 後続。本スライスは Node + Edges + Generate まで。Editor 可視化の同期は問わない

- 判定定義:
  - GO: 進める最低条件 1〜7 を満たし、常時契約違反が無い。
  - NO-GO: 最低条件未達、または Game→Framework 逆転、SceneState 変更、URP asmdef 追加、一時生成器復活、216 bake。
  - CONDITIONAL ACCEPT: 使わない（スパイクではない）。

- 停止規則: 最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。

- A3 後の例外承認: なし

- 本文へ転記した実装制約:
  - Unity 側 C# で `record` 禁止。編集する `.cs` 先頭に `#nullable enable`。
  - 破棄されうる `UnityEngine.Object`（`Light` 含む）は `== null` / `!= null`。`?.` / `??` / `is null` / `ReferenceEquals` を使わない。
  - 公開ログは `ILogger<T>`。`ZLogger*` 型を公開面に出さない。既存 `SeasonLightingScene` の ZLog 呼び出しパターンは維持してよい。
  - asmdef 参照を追加しない。特に `Unity.RenderPipelines.*` を Runtime / InGame / DependOnAll に足さない。
  - `SceneState` 14 値を減らさない・並べ替えない。LoadType を増やさない。
  - Framework（`unity/Assets/OneStarMaker/`）に `Season|Spring|Summer|Autumn|Winter|季節` を出さない。
  - 依存配線は `AppInitializer` と `GameSceneFactory` のみ。Service Locator 禁止。
  - Scene / Prefab / SceneResource / Addressables はローカル Editor + `tools/unity-editor.cmd`。YAML 手編集禁止。Cloud では Unity CLI を叩かない。
  - Cell Lighting の作成は既存 World Workspace（`OneStarMaker/World Workspace`）だけ。S-4b 生成器を復活させない。
  - テストで `Task.Delay` / `Thread.Sleep` 禁止。
  - Phase B は `pwsh tools/contract-audit.ps1` まで。`pwsh tools/run-tests.ps1` と Addressables build は Phase C。

- 未決事項: なし（A3 で閉じた）

### 2.1 A1 裁定（A2 が見る版）

| 論点 | 裁定 | 理由 |
|---|---|---|
| Cell Lighting 2 件の作成 | **S-4c が World Workspace で作る** | bake 入力が S-4c の最低条件。S-4d §6.2 の 6 Scene は S-4 完了在庫であり、S-4d は VFX/Events 4 件を足す。2 Lighting を再作成しない |
| URP asmdef | **足さない** | Volume.weight 実行時操作は現在の問いの判定に不要。authored Volume を Scene に置いてロードで乗せる。policy の `GlobalVolumeWeight` は Fake sink が記録し、Unity sink は無視する（コメントで理由を残す） |
| 太陽 Light の所有者 | **各 Season Lighting Scene の authored `SeasonSun`** | bake 光源と Play 光源を一致させる。DontDestroyOnLoad Host に太陽を置くと bake と乖離する |
| preset 置き場 | **SampleGame 純 C# 表** | Framework が季節語を知ってはならない。Framework は Validate + lease + sink |
| 取得/解放フック | **OnLoadedImpl / OnPreUnLoadedImpl** | Light bind に RootObjects が要る。NecessaryAlways なので Season Add 中に見た目が載る |
| Camera background | **RenderEnvironment は触らない** | CameraSystem 所有。`ApplyDemoLook` ごと削除し、InGameScene / AppInitializer の既存 clear を残す |
| TimeOfDay 時計 | **やらない** | 静的季節 preset が REN-08 前倒しの最小。時計は P-R6 |
| 二件目 owner | **例外で即失敗** | spec「即失敗」。Try で false を返すと呼び出し側が無視し得る |
| Volume 所有境界 | **Camera 固有は CameraSystem のまま。全 View 共通は Season Lighting Scene の authored global Volume。Runtime は共通 Volume をコードから作らない** | 境界を文書と Scene 配置で守る。同一 Volume を CameraSystem と RenderEnvironment が触ることを禁止 |

### 2.2 凍結する `RenderEnvironmentState`

Unity 側は `record` 禁止のため、readonly struct + コンストラクタ。`init` は `IsExternalInit` が Runtime 内部にしか無いので **使わない**。プロパティは get-only。

| フィールド | 型 | 意味 | 制約 |
|---|---|---|---|
| `SunEulerDegrees` | `Vector3` | 太陽の euler（度）。X=pitch、Y=yaw、Z=0 | Z は 0 に正規化してよい |
| `SunColor` | `Color` | 太陽色 | RGB 0..1 を推奨。Validate は負を拒否 |
| `SunIntensity` | `float` | `Light.intensity` | `>= 0` |
| `AmbientSkyColor` | `Color` | `RenderSettings.ambientLight`（Flat） | 負を拒否 |
| `FogEnabled` | `bool` | fog on/off | |
| `FogColor` | `Color` | fog 色 | 負を拒否 |
| `FogDensity` | `float` | ExponentialSquared の density | `>= 0` |
| `GlobalVolumeWeight` | `float` | 将来の共通 Volume weight | `0..1`。Unity sink は S-4c で適用しない |

`FogMode` は state に持たない。Unity sink は常に `FogMode.ExponentialSquared` を書く。これは四季共通の製品判断であり、preset 差分にしない。

`RenderSettings.ambientMode` は sink が常に `AmbientMode.Flat` を書く。

### 2.3 凍結する四季 preset（SampleGame）

identity 完全一致。未知 identity は `SeasonLightingScene` が **Acquire 前に** 失敗する。

| Identity | SunEuler | SunColor | Intensity | AmbientSky | FogEnabled | FogColor | FogDensity | VolumeWeight |
|---|---|---|---|---|---|---|---|---|
| `Spring_Lighting` | `(15, -30, 0)` | `(1.00, 0.85, 0.70)` | `0.80` | `(0.18, 0.22, 0.28)` | true | `(0.75, 0.82, 0.88)` | `0.0015` | `1` |
| `Summer_Lighting` | `(50, 40, 0)` | `(1.00, 0.98, 0.90)` | `1.30` | `(0.28, 0.30, 0.32)` | true | `(0.70, 0.78, 0.85)` | `0.0040` | `1` |
| `Autumn_Lighting` | `(8, 50, 0)` | `(1.00, 0.55, 0.25)` | `0.70` | `(0.18, 0.13, 0.08)` | true | `(0.55, 0.38, 0.22)` | `0.0025` | `1` |
| `Winter_Lighting` | `(70, 0, 0)` | `(0.95, 0.97, 1.00)` | `0.50` | `(0.68, 0.70, 0.74)` | true | `(0.90, 0.92, 0.95)` | `0.0020` | `1` |

数値の微調整は **B 適応にしない**。見た目が致命的に反証する場合だけ Phase A 再開。

### 2.4 凍結する公開 API（Framework）

配置 namespace: `OneStarMaker.Runtime.Rendering.Environments`

A3 で公開集合を明示する（CameraSystem が `ICameraBackend` / `CinemachineCameraBackend` を公開するのと同型）。`IRenderingSystem` は作らない。

**公開:**

- `RenderEnvironmentState`（readonly struct）
- `IRenderEnvironment`
- `RenderEnvironmentLease`
- `IRenderEnvironmentSink`
- `RenderEnvironment`（sealed。`IDisposable`。constructor は `IRenderEnvironmentSink` 必須）
- `UnityRenderEnvironmentSink`（sealed。`AppInitializer` が `new` する。internal にしない）

**internal:**

- `RenderEnvironmentValidator`

```csharp
public readonly struct RenderEnvironmentState
{
    public RenderEnvironmentState(
        Vector3 sunEulerDegrees,
        Color sunColor,
        float sunIntensity,
        Color ambientSkyColor,
        bool fogEnabled,
        Color fogColor,
        float fogDensity,
        float globalVolumeWeight);
}

public sealed class RenderEnvironmentLease : IDisposable
{
    public int Generation { get; }
    public void BindSun(Light sun);
    public void Apply(in RenderEnvironmentState state);
    public void Dispose();
}

public interface IRenderEnvironment
{
    bool HasActiveOwner { get; }
    RenderEnvironmentLease Acquire(object ownerKey);
}

public interface IRenderEnvironmentSink
{
    void CaptureBaseline();
    void Apply(in RenderEnvironmentState state, Light sun);
    void RestoreBaseline();
}

public sealed class RenderEnvironment : IRenderEnvironment, IDisposable
{
    public RenderEnvironment(IRenderEnvironmentSink sink);
}
```

契約の追加（A3）:

- `IRenderEnvironmentSink.Apply` の `Light` は **nullable にしない**。lease が Bind 済みだけを渡す。実装は `sun == null`（Unity 偽 null）なら例外。`sun?.` は禁止。
- `Acquire` 成功時に sink.`CaptureBaseline()`。matching Dispose で `RestoreBaseline()` と bound Light 参照の破棄。stale Dispose では Restore も Light 破棄もしない。
- `RenderEnvironment.Dispose`（App 回収）: まだ owner がいれば Restore **1 回**、owner クリア、**generation を進める**。以降その lease の `Apply` / matching Dispose は stale（Apply は throw、Dispose は no-op）。二重 Dispose 安全。
- `BindSun` 前の `Apply` は失敗。`BindSun` は lease 生存中に 1 回。二度目は失敗。matching Dispose 後は Light を保持しない。
- `ownerKey == null` は `ArgumentNullException`。token は generation。
- static / Service Locator は設けない。
- `InternalsVisibleTo("SampleGame.DependOnAll")` は足さない。

## 3. 責務マップ

行数は 2026-09-21 の HEAD。予想は純増の目安。

### 3.1 新規

| ファイル | 責務（一文） | 変更理由 | 所有者・寿命 | 入力/出力依存 | 層 | 公開面 | テスト境界 | 配置理由 | 行数目安 |
|---|---|---|---|---|---|---|---|---|---|
| `unity/Assets/OneStarMaker/Scripts/Runtime/Rendering/Environments/RenderEnvironmentState.cs` | グローバル見た目の純データ | REN-08 前倒し | 値型。寿命なし | Unity `Vector3`/`Color` のみ | Policy | 公開 struct | 値の Validate を Environment 側で | CameraSystem の Geometry と同様、Runtime 配下に Rendering/Environments。Framework/SampleGame・公開 API の差がある | ~50 |
| `unity/Assets/OneStarMaker/Scripts/Runtime/Rendering/Environments/IRenderEnvironment.cs` | 単一 owner の取得口 | 公開 API 限定追加 | App | なし | 公開 API | 公開 interface | Fake 実装で | 同上 | ~20 |
| `unity/Assets/OneStarMaker/Scripts/Runtime/Rendering/Environments/IRenderEnvironmentSink.cs` | state を装置へ翻訳する口 | policy と I/O を分ける | App（実装次第） | Light / RenderSettings | 公開面は interface。Unity I/O は実装 | 公開 interface（テスト差し替え） | Fake sink | CameraSystem の `ICameraBackend` と同型 | ~20 |
| `unity/Assets/OneStarMaker/Scripts/Runtime/Rendering/Environments/RenderEnvironmentLease.cs` | generation token と Apply/Dispose | stale 耐性 | lease オブジェクト。App 上の世代 | RenderEnvironment 内部 | Policy | 公開 sealed | 純 C# | Handle を独立型にしないと token 照合が埋もれる | ~80 |
| `unity/Assets/OneStarMaker/Scripts/Runtime/Rendering/Environments/Implements/RenderEnvironment.cs` | 単一 owner と generation の調停 | 同時 1 Season | App lifetime。`AppInitializer` が生成・破棄 | sink | Policy + orchestration | 公開 sealed | Fake sink で Unity なし | CameraSystem 本体に相当。Host GO は持たない。namespace は親と同じ | ~120 |
| `unity/Assets/OneStarMaker/Scripts/Runtime/Rendering/Environments/internal/RenderEnvironmentValidator.cs` | state の fail-closed 検証 | 負の intensity 等を装置へ流さない | なし（純関数） | state のみ | Policy | internal | 純 C# | Environment に検証を混ぜるとテストと調停が混線する | ~40 |
| `unity/Assets/OneStarMaker/Scripts/Runtime/Rendering/Environments/Implements/UnityRenderEnvironmentSink.cs` | Light と RenderSettings への反映と baseline 復元 | 唯一の Unity I/O | App。RenderSettings はプロセスグローバル | `Light`, `RenderSettings` | Unity I/O | **公開** sealed（`AppInitializer` が `new`） | Play/Edit は薄い。主検証は Fake | URP 型を使わない。Runtime 既存 UnityEngine 依存の範囲。internal にしない（DependOnAll から new するため） | ~80 |
| `unity/Assets/SampleGame/InGame/InGameSession/World/SeasonLightingPresetTable.cs` | identity → state | 季節語を Game に閉じる | なし（静的表） | なし | Policy（Game） | internal | 純 C# | SeasonLightingScene に表を直書きすると Scene 寿命と数値が混ざる | ~80 |
| `unity/Assets/OneStarMaker/Tests/Rendering/Environments/RenderEnvironmentLeaseTests.cs` | 単一 owner / stale / restore | 最低条件 1 | テスト寿命 | Fake sink | テスト | なし | EditMode 純 C# | Camera テストと同配置 | ~250 |
| `unity/Assets/OneStarMaker/Tests/Rendering/Environments/FakeRenderEnvironmentSink.cs` | sink 呼び出しの記録 | 最低条件 1 の装置差し替え | テスト寿命 | なし | テスト | なし | EditMode | Runtime にテスト型を置かない | ~40 |
| `unity/Assets/OneStarMaker/Tests/SampleGame/SeasonLightingPresetTableTests.cs` | 四季表の固定値 | 最低条件 3 の入力 | テスト寿命 | なし | テスト | なし | EditMode | SampleGame テスト | ~80 |

Rendering/Environments フォルダ新設の理由: Framework / SampleGame ではなく、CameraSystem と並ぶ Runtime サブシステム境界。Helpers/Managers ではない。公開 namespace は `OneStarMaker.Runtime.Rendering.Environments` で凍結済み。ファイル配置だけ CameraSystem に合わせてサブフォルダへ分けた（B 適応。公開集合・asmdef・寿命は不変）。

作業ツリーの `Implments` は typo。Phase B で Unity Project 窓から `Implements` にリネームする（`.meta` GUID を保つ）。

`RenderEnvironmentLease` と `RenderEnvironment` は変更理由が近いが、token 型を Environment の inner class にすると公開 API が読みにくい。行数警報（500 / 3 責務 / 50%）は単体では未達。非分割: `Lease` を独立ファイルにするのは公開 Handle のためであり、行数削減のための委譲ではない。

### 3.2 既存変更

| ファイル | 現在行 | 予想増減 | 責務 | 変更理由 | 分割判断 |
|---|---|---|---|---|---|
| `SeasonLightingScene.cs` | 42 | +70 | 季節 Lighting の寿命で lease を持つ | scaffold を本番にする | 非分割。SceneBase 1 ファイル 1 Scene。追加分は Load/Unload orchestration のみ。preset 表と lease プロトコルは別型 |
| `PlayerScene.cs` | 229 | -25 | 飛行プレイヤー。描画オーナーではない | `ApplyDemoLook` 削除 | 非分割。削除のみ |
| `GameSceneFactory.cs` | 95 | +20 | 唯一の Scene 生成 | `IRenderEnvironment` を SeasonLighting へ渡す | 非分割。DependOnAll の配線 |
| `AppInitializer.cs` | 383 | +50 | Composition Root | RenderEnvironment の生成・破棄・Factory 注入 | 非分割。CameraSystem 初期化に隣接して追加。50% 未満 |
| `GameSceneLoggingTests.cs`（Factory テスト） | 287 | +40 | Factory 契約 | 新必須引数と null 拒否 | 非分割 |
| 四季 `*_Lighting.unity` | 空 root | Editor で `SeasonSun`（Directional）と任意の global Volume GO | authored 太陽 | YAML 手編集しない | コンテンツ |
| `Spring_Lighting_4_2` / `_5_2` | 不在 | World Workspace で作成 + local Point | bake 入力 | 既存 creator | コンテンツ |
| 代表 Cell / Environment `.unity` の lighting data | 未ベイク | bake 出力のみ | lightmap | 対象 7 Scene 以外を保存しない | コンテンツ |

`AppInitializer` は既に CameraSystem と Profiler の寿命を持つ。RenderEnvironment 追加は「App 常駐サービスの生成破棄」という同じ変更理由。新しい Host GO は作らない。3 責務警報は発火し得るが、Composition Root を分割すると配線が散るため **非分割**。理由: 依存の唯一の集約場所が DependOnAll である（Architecture §3）。

### 3.3 明示的に触らない

- `CameraSystemHost` / `VolumeCrossfade` / CAM-07
- `SessionSeasonController`（Lighting は既に NecessaryAlways）
- `CellCompanionScene`（Cell Lighting も構造的 child のまま）
- World Workspace の作成トランザクション実装（使うだけ）
- `SceneDirector` / `SceneState` / Streaming 政策

## 4. 実装計画（Phase B 手順書）

Phase B 担当は **この節の順序** で進める。設計判断が必要になったら止まる。Unity テストは実行しない。フィールド一覧・判定順・現行骨格の直し方は Playbook。契約が衝突したらこの HANDOFF。

### 4.0 1 枚で見る流れ

```text
SeasonLightingScene.OnLoadedImpl
  1. SeasonSun を RootObjects から name 完全一致で探す（まだ lease を取らない）
  2. SeasonLightingPresetTable.TryGet（未知なら throw。まだ lease を取らない）
  3. IRenderEnvironment.Acquire(this) → RenderEnvironmentLease
  4. lease.BindSun(sun) → lease.Apply(state)
       RenderEnvironment が generation を確認し、Validator のあと
       IRenderEnvironmentSink.Apply へ渡す
         Fake: 回数を記録するだけ（B1）
         Unity: Light + RenderSettings へ書く（B2）

SeasonLightingScene.OnPreUnLoadedImpl
  lease.Dispose → 番号が一致するときだけ RestoreBaseline、owner を空け、generation++
```

同時に権利証を持てるのは 1 件。二件目の `Acquire` は即 `InvalidOperationException`。古い lease の Dispose は新しい owner を消さない。

内部フィールド（公開 API には出さない。B 適応）:

| 型 | フィールド |
|---|---|
| `RenderEnvironment` | `_sink`, `_ownerKey`, `_generation`（初期 0）, `_boundSun`, `_disposed` |
| `RenderEnvironmentLease` | `_environment`, `Generation`（Acquire 時のコピー。以後変えない） |
| Lease → Environment の internal | `BindSunFromLease` / `ApplyFromLease` / `ReleaseFromLease` |

matching Dispose と `RenderEnvironment.Dispose` は、Restore のあと **必ず `_generation++`** する。これを忘れると stale 4 手が壊れる。

### 4.1 変更対象（コード）

1. Runtime `Rendering/Environments`（公開 4 + Implements 2 + internal Validator）
2. Tests `Rendering/Environments`（Fake sink + lease テスト）
3. SampleGame preset 表
4. `SeasonLightingScene` / `PlayerScene` / `GameSceneFactory` / `AppInitializer`
5. Factory テスト + preset テスト
6. Editor コンテンツ: 4 Season Lighting の `SeasonSun`、2 Cell Lighting、代表 bake

### 4.2 順序

**B0. 準備**

- ローカルで `tools/unity-editor.cmd status`。閉じていれば ProjectVersion 6000.6.0f1 の Editor をこの `unity/` で開く。Cloud なら B0 をスキップし、C# だけ書いてコンテンツ手順を実装結果に「未実行」と書く。人間 B はローカル必須（bake があるため）。
- 既存 Editor が別 project なら接続しない。
- `git status` がこのブランチであることを確認する。Rendering 骨格の dirty は残してよい。
- フォルダ `Implments` を Unity Project 窓で `Implements` にリネームする。

**B1. Fake で lease を先に通す（Unity Scene を開かない）**

現行骨格は空実装と誤った Fake がある。部分修正より Playbook §7 の完成形へ置き換える。

1. `RenderEnvironmentState` と `RenderEnvironmentValidator` を書く。Validator は `void Validate(in RenderEnvironmentState)`。失敗は `ArgumentOutOfRangeException`（負の intensity / density / 色、weight の 0..1 外）。bool を返さない。
2. `FakeRenderEnvironmentSink` を Tests アセンブリへ置く（Runtime にテスト型を置かない）。記録: `CaptureCount`, `RestoreCount`, `Applies` リスト, `SunBound` bool。**Validator を呼ばない。sun から state を組み立てない。渡された state をリストへ追加するだけ。**
3. `RenderEnvironment` + `RenderEnvironmentLease` を書く。Lease は Environment へ委譲するだけ。`_currentState` を Lease に持たない。
4. 判定順は Playbook §5。要約:
   - `Acquire`: null owner → 破棄済み → 既に owner → Capture → owner セット → `new Lease(this, _generation)`。失敗したら Capture も owner も変えない。
   - `ReleaseFromLease`（lease.Dispose）: 番号不一致 or owner なし → no-op。一致なら Restore、Light 参照破棄、owner クリア、`_generation++`。
   - `RenderEnvironment.Dispose`: owner がいれば Restore 1 回、owner クリア、`_generation++`、破棄フラグ。以降その lease の Apply は throw、Dispose は Restore を増やすな。
5. `RenderEnvironmentLeaseTests` を書く。最低ケース:
   - `Acquire_First_Succeeds_AndHasOwner`
   - `Acquire_Second_Throws_AndKeepsFirstOwner`
   - `Dispose_Matching_ClearsOwner_AndRestoresBaseline`
   - `Dispose_StaleAfterNewOwner_DoesNotClearNewOwner_AndDoesNotRestore`
     Arrange は必ず 4 手: `Acquire(A)` → `Dispose(A)` → `Acquire(B)` → `Dispose(A)`。二件目 throw 中の Dispose を stale と誤らない。
   - `Apply_OnDisposedLease_Throws_AndDoesNotMutateSink`
   - `Apply_StaleLeaseAfterNewOwnerApply_Throws_AndDoesNotMutateNewOwner`
   - `Dispose_Twice_IsIdempotent`
   - `Acquire_NullOwner_Throws`
   - `Apply_BeforeBindSun_Throws`
   - `BindSun_Twice_Throws`
   - `BindSun_NullLight_Throws`
   - `Validate_NegativeIntensity_Throws`
   - `DisposeEnvironment_WithActiveLease_RestoresOnce_AndLeaseDisposeIsNoOp`
     `RenderEnvironment.Dispose` のあと、同じ lease の `Apply` は throw、`Dispose` は Restore を増やさない。
6. ここで `pwsh tools/run-tests.ps1` は **まだ走らせない**。コンパイルは Editor が開いていれば確認してよい。

**Light を EditMode でどう扱うか（凍結）:** `BindSun` の引数型は `Light` のままにする。純 C# テストは `BindSun` を呼ばずに失敗経路を見るテストと、Tests 内で `GameObject` + `Light` を一時生成できる EditMode テストを分ける。`RenderEnvironmentLeaseTests` は Fake sink 中心のクラスに、`#if` で分けない。Light が必要なケースは同ファイルで `new GameObject` + `AddComponent<Light>()` し、TearDown で `DestroyImmediate`。これは Camera テストが stub `Object` を作るのと同型。DontDestroyOnLoad は使わない。

**B2. Unity sink**

- `UnityRenderEnvironmentSink`:
  - `CaptureBaseline` で fog / fogMode / fogColor / fogDensity / ambientMode / ambientLight をフィールドに保存。`Light` の baseline は持たない（Scene とともに消える）。
  - `Apply`: `sun == null` なら何も書かず例外（Environment 側で先に弾く）。`sun != null` なら `sun == null` の Unity 偽 null を再確認し、破棄済みなら例外。rotation は `Quaternion.Euler(state.SunEulerDegrees)`。`sun.color` / `intensity` / `type` は変更してよいが、`type` を Directional 以外へは変えない（authored が Directional であることだけ確認し、違えば例外）。
  - `RenderSettings.fog` 等を state どおり書く。`FogMode.ExponentialSquared`、`AmbientMode.Flat` 固定。
  - `GlobalVolumeWeight` は読まない。コメント: 「S-4c は URP 参照を足さない。weight は preset と Fake の観測用。」
  - `RestoreBaseline` は Capture した RenderSettings だけ戻す。
- Runtime に URP using を書かない。

**B3. App 配線**

`AppInitializer`（行の場所は現行ファイル基準）:

- フィールド `_renderEnvironment` と `_renderEnvironmentQuittingHandlerRegistered`。
- `Before()` の `InitializeCameraSystem();` の直後に独立メソッド `InitializeRenderEnvironment()`。中で `new RenderEnvironment(new UnityRenderEnvironmentSink())`。`ReleaseCameraSystem` に埋め込まない。
- `CreateSceneFactory` の `new GameSceneFactory(...)` に渡す。未初期化なら `InvalidOperationException`。
- **独立** メソッド `ReleaseRenderEnvironment`。中で `_renderEnvironment?.Dispose(); _renderEnvironment = null;`。quitting ハンドラもここで外す。
- 呼び先: `Sub()`（`ReleaseCameraSystem();` の隣）、`Application.quitting`、`OnAfterSceneLoadInitializationFailed`（同じく Camera の隣）。同じタイミング、別メソッド。
- 破棄順: Scene がもう Environment を使わない状態で呼ぶ。相互依存は無いので Camera の前後どちらでもよいが、両方を同じ失敗ハンドラから呼ぶ。Host GO は作らない。

`GameSceneFactory`:

- 必須引数 `IRenderEnvironment renderEnvironment` を既存 4 引数の後ろへ。null は `ArgumentNullException`。
- `Spring_Lighting` / `Summer_Lighting` / `Autumn_Lighting` / `Winter_Lighting` の constructor にだけ渡す。
- `Spring_Lighting_4_2` は switch に出さない。親が `StreamByDistance` なら既存分岐で `CellCompanionScene`。

`SeasonLightingScene`（`OnLoadedImpl` 例外は PreUnLoad を通らない）:

```
sun = RootObjects を回し GetComponentsInChildren<Light> から
      name 完全一致 "SeasonSun" かつ Directional かつ Unity 偽 null ではないもの
FindRootComponent<Light>() は使わない（最初の Light が SeasonSun とは限らない）
見つからなければ throw（まだ Acquire しない）
if (!SeasonLightingPresetTable.TryGet(identity, out state)) throw（まだ Acquire しない）
_lease = _renderEnvironment.Acquire(this)
try {
  _lease.BindSun(sun)
  _lease.Apply(state)
} catch {
  _lease.Dispose()
  _lease = null
  throw
}

OnPreUnLoadedImpl:
  _lease?.Dispose()  // C# の lease 参照。Light には ?. を使わない
  _lease = null
```

成功時は finally で Dispose しない。`OnAfterUnLoadedImpl` に Dispose を足して PreUnLoad 失敗を救う逃げはしない。

新しい反射を増やすな。`SceneBase` に Find ヘルパーを足さない。

`PlayerScene`: `ApplyDemoLook` メソッドと呼び出しを **まるごと** 削除する。中の Camera clear も消す（AppInitializer / InGameScene の既定が残る）。`RenderSettings` 参照が Game 層に残っていたら失敗。Camera bind は残す。

**B4. テスト更新**

- `GameSceneFactory` の全 constructor 呼び出しに Fake `IRenderEnvironment` を足す。null 拒否テストを 1 件追加。Factory 用 Fake は `Acquire` を実装しなくてよい（生成契約だけ見る）。lease テストの Fake sink と混ぜない。
- `CreateSceneClass_SeasonLightingWithoutCellParent_IsNotCompanion` を維持。
- 追加: 親が `StreamByDistance` の `Spring_Lighting_4_2` は `CellCompanionScene`（lease を取らない）。
- Cell Lighting 2 Scene 作成後、`OneStarMaker.Tests.Editor` に Directional 本数 0・Point 1 以上を数える EditMode を 1 件。Scene がまだ無い B4 時点では書かず、B5 の直後に足す。

**B5. Editor コンテンツ（YAML 手編集禁止）**

対象を限定する。広範な再生成はしない。

0. Lighting window の Auto Generate を **off**。Hierarchy に載っている Scene 全部が bake 対象になる。
1. World Workspace で Spring、座標 `(4,2)`、職種 Lighting、payload Full。optional が欠けるので Create。`Spring_Lighting_4_2`。`(5,2)` も同様。Create 直後の Empty Scene に Directional が混ざっていないか見てから `LocalFill` を足す。
2. 各 Season Lighting Scene（4 件）を開き、空の `SeasonLightingRoot` 配下へ `SeasonSun`（Directional）。rotation/color/intensity は preset 表と同じ。Lightmap Static オン。
3. 任意: 同じ Scene に global Volume GO（isGlobal）。Runtime は触らない。
4. 各 Cell Lighting に `LocalFill` Point を 1 つ。`(4,2)` は見証付近（Cell 中心、地上 4m 目安）、`(5,2)` は隣接 Cell 中心。range ≤ 80。**Directional は 0。** Hierarchy で確認。
5. Lighting Settings: `Assets/SampleGame/InGame/InGameSession/Seasons/Spring/Spring_Representative.lighting`。代表 7 Scene だけに割り当て。bakedGI on、realtime GI off。他 Scene の LightingSettings は触らない。
6. bake 用 Hierarchy の作り方（World Workspace の Lighting Open のまま焼かない）:
   - 先に **どれか 1 つの Full Cell を `OpenSceneMode.Single` で開いて** 他を閉じる。
   - Whitebox パス（`.../Variants/Whitebox/Spring_Cell_*`）を開かない。
   - 次の 7 件だけを Additive で開く:
     - `Spring_Lighting`
     - `Spring_Cell_4_2`（Full）
     - `Spring_Environment_4_2`
     - `Spring_Lighting_4_2`
     - `Spring_Cell_5_2`
     - `Spring_Environment_5_2`
     - `Spring_Lighting_5_2`
   - Hierarchy がこの 7 件だけであることを目視。8 件目があれば閉じる。
7. Generate Lighting。失敗したらまず LightingSettings / 光源欠落。計画外 API が必要なら停止。
8. 開いている 7 Scene だけ保存。**Save All しない。**
9. `git status` で Whitebox `.unity` や他 Cell が dirty なら破棄して戻す。
10. commit してよいもの:
    - `Spring_Representative.lighting` と `.meta`
    - 7× `.unity` の LightingSettings / LightingData 参照
    - 各 Scene 近傍の `LightingData.asset` / lightmap `.exr` と `.meta`
    - 新規 2 Cell Lighting の scene / resource / node / map / Addressables 差分
    - `Library/` は不可。7 件以外の `.unity` は不可
11. 境界の目視: `(4,2)` から東の `(5,2)`。明白な明るさ段差が無いかを HANDOFF §6 に書く。スクショは任意（`artifacts/s-4c-phase-b/`）。
12. Editor で Cell Lighting を閉じても sun/fog が春のままであること。
13. B4 で後回しにした Directional 0 の EditMode テストを足す。

**B6. 機械検査**

```text
pwsh tools/contract-audit.ps1
pwsh tools/docs-audit.ps1
```

grep 自己確認（契約）:

```text
rg "RenderSettings" unity/Assets/SampleGame --glob "*.cs"
rg "Season|Spring|Summer|Autumn|Winter|季節" unity/Assets/OneStarMaker/Scripts -g "*.cs"
```

Game 層の `RenderSettings` は 0 件。Framework の季節語は 0 件。Unity sink の `RenderSettings` は Runtime Rendering にだけあってよい。

**B7. 完了報告**

HANDOFF §6 に実装、差、未実行（テスト未実行を明記）、head SHA を書く。Phase C を自分で走らせない。

### 4.3 Phase B から Phase A へ差し戻す条件

- 凍結済み公開集合以外の型・asmdef・Host 太陽が必要
- URP / 新規 asmdef が必要
- DontDestroyOnLoad の Play 用太陽 Host が必要になった（A3 で不採用）
- `SceneState` / LoadType 変更が必要
- Cell Lighting を `CellCompanionScene` 以外の型にしないと local 非干渉を守れない
- World Workspace では 2 件を作れない
- bake が 7 Scene 以外の GUID を大量に変える
- preset 数値を変えないと「季節が見えない」が致命的（見た目の微調整は再開対象）
- 中核の lease を Unity なしでテストできない配置になった

B 適応でよい例: `Find` の具体メソッド名、コメント、テスト関数の分割、Light の range 微調整、LightingSettings の sample 数、生成された lightmap ファイル名。

### 4.4 対象外を維持する方法

- Rendering フォルダに RenderWorld / Archetype を置かない。
- `SeasonLightingPresetTable` 以外に季節語を Runtime へコピーしない。
- bake 前に Hierarchy で開いている Scene を確認する。Spring 以外を開かない。
- World Workspace の Create は Lighting optional 1 件ずつ。VFX/Events を作らない。
- `PlayerScene` から fog を「一時的に残す」逃げをしない。

## 5. テストとレビュー計画

- 単体テスト:
  - `RenderEnvironmentLeaseTests`（必須。最低条件 1。stale 4 手と Environment.Dispose を含む）
  - `SeasonLightingPresetTableTests`
  - `GameSceneFactory` の引数契約と `Spring_Lighting_4_2` companion 分類
  - Validator の負値拒否
  - B5 後: `CellLightingSceneTests.RepresentativeCellLightingScenes_HaveNoDirectional_AndAtLeastOnePoint`（`(4,2)` `(5,2)` の Directional 0・Point 1 以上）

- 差し戻し中の起点 `-Filter`:
  ```
  RenderEnvironmentLeaseTests|SeasonLightingPresetTableTests|GameSceneFactoryTests|GameSceneLoggingTests|CellLightingSceneTests|UnityRenderEnvironmentSinkTests
  ```
  Cell Lighting 検査は凍結済み受け入れ条件（Directional 0）の確認のため起点に含める。`UnityRenderEnvironmentSinkTests` は最低条件 1 の baseline 復帰（ambient 成分と sun）の確認に必要だったため、差し戻しの起点へ足した。受け入れ条件の追加ではない。

- 判定必須テスト:
  1. 最終の全 EditMode 回帰（空 filter）。`pwsh tools/run-tests.ps1`。Editor を閉じてから。Windows なら sandbox 外。
  2. Play Mode 手動（判定 C）: `world:cellCompanionSet=Lighting` または Full。Spring 起動。スポーンから `(4,2)` `(5,2)` へ。fog/sun が春 preset。UnloadRadius 外へ出て **両 Cell が落ちても fog/sun は春のまま**（消えたら lease を Cell 側で捨てている）。戻って baked 床が載る。二重 Season は EditMode の二件目失敗で足りる。
  3. 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、季節語 grep、SampleGame `RenderSettings` grep。

- 全 EditMode 回帰の適用除外: なし。

- 統合・Unity テスト: 新規 PlayMode 自動テストは必須にしない。

- 機械検査: contract-audit、docs-audit、上記 grep。

- A0/A1 主担当・モデル・ベンダー: Cursor Grok 4.6 / xAI
- A2 独立レビュー:
  - アーキテクチャゲート: 独立エージェント / Grok 系 inherit / `A2-architecture.md`
  - 寿命・bake: 独立エージェント / Grok 系 inherit / `A2-lifecycle.md`
  - A0 のみ代替構成: 独立エージェント / Grok 系 inherit / `A2-alternative.md`（A1 未読）
- A3 統合担当・モデル・採否: 本セッションの主担当（Grok 4.6）。採否は `artifacts/s-4c-phase-a/A3-frozen.md`
- C' 用に予約した担当・モデル・ベンダー: **Claude 系または GPT 系**、新規セッション、blind audit bundle。Phase A は Grok 系のみ。
- 独立性の強化条件を満たせない場合の理由: A2 は同一クラウドセッションの別エージェントで、モデル系列が主担当と同じ。`独立性制約あり`。C' で系列を分ける。A2 同士は互いの指摘を見ていない。

### 5.1 A3 採否

詳細と理由は `artifacts/s-4c-phase-a/A3-frozen.md`。要約:

| 指摘 | 判定 |
|---|---|
| F-1 Composition Root が internal sink を new できない | **採用**。`UnityRenderEnvironmentSink` と `IRenderEnvironmentSink` を公開 |
| F-2 公開集合の freeze | **採用**。§2.4 に列挙 |
| F-3 / S4C-A2-02 App Dispose が generation を進める | **採用** |
| F-4 matching Dispose で Light 参照を捨てる | **採用** |
| F-5 問いから Volume 実行時所有を外す | **採用** |
| F-6 / Acquire 前に preset lookup | **採用** |
| F-7 独立 `ReleaseRenderEnvironment` | **採用** |
| F-8 SeasonLightingScene 分割 | **保留（後続）**。今は非分割 |
| F-9 太陽を Restore しない | **保留（S-5）** |
| S4C-A2-01 OnLoadedImpl の try/catch | **採用** |
| S4C-A2-03 直列切替の空白 | **後続 S-5** |
| S4C-A2-04 Light `?.` | **採用**（コメント。API の Light は non-null） |
| S4C-A2-05 stale Arrange と追加テスト | **採用** |
| S4C-A2-06 Directional 0 の EditMode | **採用** |
| S4C-A2-07 bake Hierarchy / commit 列挙 | **採用** |
| S4C-A2-08 条件 7 の文言 | **採用** |
| S4C-A2-09 BindSun は lease | **確認。変更なし** |
| 代替 A（Host 太陽） | **不採用**。bake と Play を同じ authored `SeasonSun` にする。Host は公開面と寿命を増やす。lease の純 C# は Fake sink で足りる |

## 6. Phase B 実装結果

- 実装:
  - B1–B4: Framework `IRenderEnvironment` / `RenderEnvironmentLease` / `UnityRenderEnvironmentSink` / `RenderEnvironmentState`。公開 namespace は `OneStarMaker.Runtime.Rendering.Environments`。配置は `Rendering/Environments/{Abstractions,Implements,internal}`。
  - App 寿命: `AppInitializer.InitializeRenderEnvironment` が `new RenderEnvironment(new UnityRenderEnvironmentSink())`。`ReleaseRenderEnvironment` は Camera 解放と独立。`Application.quitting` でも呼ぶ。
  - `GameSceneFactory` が `IRenderEnvironment` を受け、四季 `*_Lighting` は `SeasonLightingScene`。`Spring_Lighting_4_2` は親が `StreamByDistance` なら `CellCompanionScene`（lease なし）。
  - `SeasonLightingScene.OnLoadedImpl` は FindSeasonSun → preset lookup → Acquire → BindSun / Apply。Acquire 後の失敗だけ Dispose して throw。`OnPreUnLoadedImpl` で Dispose。
  - `SeasonLightingPresetTable` は HANDOFF §2.3 の凍結値。`PlayerScene.ApplyDemoLook` は削除。SampleGame `.cs` の `RenderSettings` 代入は 0。
  - テストコード（未実行）: `RenderEnvironmentLeaseTests`（stale / Environment.Dispose / BindSun 前 Apply を含む）、`SeasonLightingPresetTableTests`、`GameSceneLoggingTests` の Factory 引数と companion 分類。
  - B5 コンテンツ: World Workspace で `Spring_Lighting_4_2` / `Spring_Lighting_5_2` を作成（Create 後の Map / parent / Addressables / Graph 辺は 4_2 で確認済み）。各 Scene に Point `LocalFill` 1 つ、Directional 0。`(4,2)` は `(1165, 4, 665)` range 80 intensity 200 青、`(5,2)` は `(1415, 4, 665)` range 60 intensity 55 赤。Mode は Mixed。
  - 四季 Season Lighting に Directional を配置。GameObject 名は `SeasonSun` 完全一致。euler / color / intensity は preset 表と一致。親は `SeasonLightingRoot`。
  - bake: `Spring_Representative.lighting`（guid `f770f1b668c369745a58960828ef9a23`）を代表 7 Scene だけに割当。baked GI on、realtime GI off、Mixed Bake Mode Subtractive、resolution 0.5、max 512、sample counts direct / indirect / environment = 32 / 64 / 64。7 Scene は同じ `LightingData.asset`（guid `0f7e7f4f5b3c44742a12ac17804d764e`）を参照し、`Lightmap-0..3_comp_light.exr` / `_comp_dir.png` を保存。
  - B6: `contract-audit` 違反なし。`docs-audit` は artifacts が個別 HANDOFF パスを指していた検査2を、パス表記を外して解消（exit 0）。Framework Scripts の季節語はコメントの否定文のみ。
- HANDOFF との差（B 適応。契約・公開集合・preset 数値は不変）:
  - ファイル配置を `Rendering/Environments/{Abstractions,Implements,internal}` へ分けた。
  - World Workspace の companion 親フォルダ定数を実 Cell のある `InGameSession/Seasons` へ直した。
  - World Workspace Create は SceneGraph Editor / Layout を通さない（現行の事実。S-4c では直さない。後続へ）。
  - LocalFill の range / intensity は見証用に B 適応で上げた（上限 80 内）。色は青/赤の識別用。
  - `Spring_Representative.lighting` は上記の限定設定で作成・割当済み。Lighting Settings を変更したのは代表 7 Scene のみ。
- 境界の目視（最低条件 6 / B5.11）: 人間が確認、境界に明白な明暗差なし。Play での確認は Content Directory 未指定で BeforeSceneLoad 失敗（`content:runtimeMode`）。これは S-4c では直さない。Editor の 7 Scene 目視が正。
- B5.12（Cell Lighting を閉じても sun/fog が春のまま）: **OK。** 人間確認（2026-09-23）。`Spring_Lighting` を残して `_4_2` / `_5_2` だけ閉じた。LocalFill（Point）だけ消え、Scene ビューの太陽の向きと全体の明るさは閉じる前と同じ。Lighting ウィンドウの Sun Source は空のまま（`RenderSettings.sun` 未割り当て。lease もここは書かない）。Editor の Fog / Ambient は Unity 既定のままで、閉じる前後で変わらない。春 preset の Fog / Ambient は Play の Apply 対象であり、この項目では見ない。
- 修正後のコンテンツ事実: 春 `SeasonSun` は Mixed のまま Contribute GI（`m_StaticEditorFlags: 1`）。2 Full Cell と 2 Environment の MeshRenderer 17 件が Contribute GI、`(4,2)` Ground も flags 1。Cell Lighting は各 Point 1、Directional 0。Season sun の Lightmap Static を混同せず、GI contribution flags を明示した。
- テスト: 差し戻し起点 filter は 34 件成功（2026-09-24、XML は §7.1 evidence bundle に記録）。全 EditMode 回帰および Play Mode 手動は未実行（判定 C 用）。
- implementation head commit: `1241c8e7b4e475f934fc8931ba783c755508ee2d`
- Phase B 担当・モデル・ベンダー: 人間。差し戻し修正、bake、限定 filter は Cursor Grok 4.7 / xAI（2026-09-24）

## 7. Phase C

- 種別: 発見 C（GO 判定・判定 C・C' は未実施）
- implementation base / head: `553b7b150e13245b369d75dc4baa12d86e9559aa` → `f8044a0177bb9ccf2eca560b57a69677110c4a67`。レビュー開始時 HEAD / 作業ツリー状態は bundle の `head.txt` / `status.txt`。
- evidence bundle id / path: `s4c-discovery-c-f8044a0` / `artifacts/s-4c-phase-c-discovery/`
- bundle generated at: `2026-09-23T23:49:51.7595533+09:00`（初回 manifest 時刻。機械検査結果を同 bundle に追加後の固定）
- implementation diff SHA-256: `FECE5C68CCC2B5EF213821D8092CDED166AB0339BA76AED7A5B378BBA69C9ED2`（`implementation.diff`）
- stat SHA-256: `41DED8A819375463AA81A8EF94AA90C3B676CAAEC2B9A1D81F6F7A87769D1D64`
- name-status SHA-256: `5296299DD3A8C17CCA7E640D426F5528044E01BB3DC73D2FCB7DFC0BE7CE4C3A`
- diff 規模: 81 files、+4,678 / −63 行。レビュー対象は base→implementation head の完全差分。現在の HEAD は `599026430c84615c43315c792e6cf145f9da98a9` で、HANDOFF のみを含む後続 review-record commit。
- 構造適合: lease / sink は Framework Runtime Rendering、season preset / scene adapter は SampleGame、全体配線は `DependOnAll` の `AppInitializer` / `GameSceneFactory` にあり、計画した Game → Framework 依存を維持。Unity Sink のみが `RenderSettings` を書き、SampleGame C# の `RenderSettings` は 0 件。Framework Scripts の季節語は否定的なコメント 2 件のみ。新しい中核ロジックには Fake sink 中心の lease テストがあり、実装配置から Unity 非依存テストが可能。行数と 81 ファイルの多くは Scene / 生成アセットと Phase A・HANDOFF artifact で、独立責務をまとめた新規 facade / helper は見つからない。
- 機械検査（2026-09-23、対象 commit 固定後）:
  - `pwsh tools/contract-audit.ps1`: exit 0。検査対象 19 C# 差分、違反なし。
  - `pwsh tools/docs-audit.ps1`: exit 0。検査 1・2 に違反なし。
  - `rg "RenderSettings" unity/Assets/SampleGame --glob "*.cs"`: 0 件。
  - `rg "Season|Spring|Summer|Autumn|Winter|季節" unity/Assets/OneStarMaker/Scripts -g "*.cs"`: コメントの否定文 2 件（`RenderEnvironmentState.cs`, `RenderEnvironment.cs`）。Framework に季節ロジックなし。
  - `git diff --check base..head`: Unity 生成 YAML / `.meta` の空値行に trailing whitespace を報告。意味のあるコード行ではなく、別の hard gate 違反とは判定しない。
- 現在の問いを阻害する findings:
  1. **F-C1 — 凍結条件 5 と受け入れ条件「代表 7 Scene の LightingData / lightmap が commit 済み」を満たす bake 証拠を確認できない。** 7 Scene の `m_LightingDataAsset` は同じ GUID を参照するが、`Spring_Representative.lighting` は存在せず、全 7 Scene の `m_LightingSettings` は `{fileID: 0}`。追跡可能な bake 出力は共通 `LightingData.asset` と `ReflectionProbe-0.exr` だけで、lightmap `.exr` は無い。加えて Full Cell の Renderer は `m_ReceiveGI: 1` だが、対象 GameObject の `m_StaticEditorFlags: 0`、Light は `m_Lightmapping: 1`（Realtime）であり、凍結手順の Lightmap Static が Scene 由来 Light に適用されていない。現証拠だけでは 2 Cell に bake 済み床が載ること・7 Scene が意図した設定で bake されたことを立証できず、最低条件 5、および最低条件 7 の reload 観測前提を阻害する。**修正先分類: Phase B 適応**（意図した Lighting Settings と static / bake 寄与設定を Editor で対象 7 Scene のみへ反映し、対象 bake 生成物を記録する。凍結した責務・公開面・寿命・問いは変えない）。
  2. **F-C2 — Ambient baseline が要求した値へ戻らない。** `UnityRenderEnvironmentSink.CaptureBaseline` は `RenderSettings.ambientLight` を保存する一方、`Apply` は `ambientMode = Flat` を設定する。`RestoreBaseline` は `ambientMode` を保存値へ戻すが `ambientLight` を書き戻すだけで、もとの `ambientMode` に対応する `ambientSkyColor` / equator / ground 等は capture・restore しない。たとえば baseline が `Skybox` で、Apply 後に元の `ambientLight` と `Skybox` を戻しても Skybox 評価が元の ambient 値を復元する保証はなく、baseline 復帰契約を満たせない。これは最低条件 1 の「解放で baseline に戻る」と受け入れ条件の baseline capture / restore に対するコード上の欠陥。**修正先分類: Phase B 適応**（実際の照明方式に応じた baseline 項目を capture / restore し、Fake / sink の確認を追加。契約範囲内）。
  3. **F-C3 — Sink.Apply は `RenderSettings.sun` を変更するが、baseline に保存・復元していない。** `RestoreBaseline` の対象は fog と ambient のみで、`RenderSettings.sun` は残る Light への参照を保持し得る。lease 解放後に Season Scene が unload されると Unity 偽 null 参照が残り、以前の sun が存在した場合の baseline も復元しない。凍結条件 1 の baseline 復帰と公開 sink の sole-I/O 契約に対する欠陥。**修正先分類: Phase B 適応**（sink が変更する global `RenderSettings.sun` を capture / restore。新しい所有者や寿命は導入しない）。
  4. **F-C4 — 春 `SeasonSun` の Editor light mode が Realtime。** `Spring_Lighting.unity` は `m_Lightmapping: 1`（Realtime）であるが、minimum 条件 5 の代表 multi-scene bake は同じ authored sun を使う前提。HANDOFF §6 は春 Mixed、他季節 Realtime と記録する一方、コミットされた YAML は春も Realtime。Lightmap Static 手順も未反映。条件 5 の焼き込み光源と Phase B 結果の不一致であり、F-C1 の設定欠落を個別に可視化する。**修正先分類: Phase B 適応**（Spring sun の既定モードを凍結された bake 意図へ合わせる）。
  5. **F-C5 — Live Cell Companion の SceneResource `.asset` が Addressables Local Group に登録されていない。** Group には companion Scene `.unity` の GUID `8382f8…`（および `_5_2` の Scene GUID）はあるが、resource asset GUID `22b75b99b54ad7a40864460bf17806e9` / `9019351cd64025141aa5e42f2f58e559` の entry はない。`SceneResourceMap` は Addressables 経由で SceneResource を引くため、Scene entry だけでは Player の live cell-companion load を構成できない。凍結条件 7 の unload → 戻る → baked 床再ロードを阻害する。`content:runtimeMode` 未指定で fail-closed した記録はこれと別件であり、起動契約を緩めずに解決できる。**修正先分類: Phase B 適応**（2 resource asset を既存 companion と同じ Local content 経路へ追加し、限定 load 検証を行う）。
- 後続スライスへ移送する findings: なし（今回は現在の問いを阻害するコード / content 欠陥を確認）。
- 撤回した指摘: 初稿で Resource GUID 未突合のまま F-C5 を撤回したが、その撤回は誤り。Scene GUID と SceneResource `.asset` GUID は別で、後者は Local Group に存在しないことを meta GUID で確認し、F-C5 として復帰。
- 既知残件の分類: `Spring_Representative.lighting` 未作成・7 Scene の Lighting Settings 未割当は F-C1 の blocker に含めた。Sun GameObject 名の B §6 記載は全四季が `SeasonSun`、FindSeasonSun は完全一致のため問題ではない。`(4,2)`→東`(5,2)` seam の目視記録は最低条件 6 未確認のまま。Cell Lighting を閉じた後の確認は §6 の記録上 sun/明るさ維持 OK（fog / ambient の春 preset 観測ではない）。Editor Play が `content:runtimeMode` 未指定で fail-closed した件は凍結契約を緩める理由にしない。
- 実行したテストコマンド: なし。ユーザー指示に従い全 EditMode 回帰および限定テストを実行せず。Unity Play Mode 手動も実行せず。
- テスト結果: 未実行。
- 判定必須のうち未実行: 全 EditMode 回帰、Play Mode 手動、限定起点 filter。
- 未確認事項: seam の人間目視（最低条件 6）、7 Scene の正しい設定による bake 完成と baked 床、復帰後の baked 床再ロード（F-C1 に伴い未立証）、Live Content Directory から両 companion resource / Scene を解決・load できること（F-C4）、Play 時の春 sun / fog preset。差分以外の Unity 実行時挙動。
- 担当・モデル: Phase C 主担当 Codex / GPT-6。Phase B の記載担当 Cursor Grok 4.6 / xAI とは異なるモデル。C' は未起動。
- 差し戻し修正（2026-09-24、Cursor Grok 4.7。発見のやり直しではない）:
  - `UnityRenderEnvironmentSink` の baseline に ambient sky / equator / ground / intensity と `RenderSettings.sun` を追加。確認は `UnityRenderEnvironmentSinkTests`。
  - 春 `SeasonSun` の `m_Lightmapping: 1` は `LightmapBakeType.Mixed`。夏/秋/冬の `4` は Realtime。モードは変えない。
  - SceneResource `.asset` の Addressables 個別登録はしていない。既存の `Spring_Cell_4_2` / `Spring_Environment_4_2` の `.asset` も Local Group に無く、Map の直接参照と `.unity` entry が現行である。
  - 差し戻し起点 filter は 34 件すべて成功（2026-09-24、`pwsh tools/run-tests.ps1 -Filter RenderEnvironmentLeaseTests|SeasonLightingPresetTableTests|GameSceneFactoryTests|GameSceneLoggingTests|CellLightingSceneTests|UnityRenderEnvironmentSinkTests`、XML `TestResults/results-RenderEnvironmentLeaseTests-SeasonLightingPresetTableTests-GameSceneFactoryTests-GameSceneLoggingTests-CellLightingSceneTests-UnityRenderEnvironmentSinkTests-20260924-042040.xml`）。全 EditMode 回帰と Play Mode 手動は未実行。発見 C のやり直しは別モデル。
- bake 実施（2026-09-24）。`Spring_Representative.lighting`（guid `f770f1b668c369745a58960828ef9a23`）を 7 Scene だけが共有。baked GI オン、realtime GI オフ、Mixed Bake Mode は Subtractive、解像度 0.5、最大 512、direct 32 / indirect 64 / environment 64。春 `SeasonSun` は Mixed のまま Contribute GI（`m_StaticEditorFlags: 1`）。両 Full Cell と両 Environment の MeshRenderer 17 件に Contribute GI。`(4,2)` の Ground は flags 1。生成物は `Spring_Lighting/Spring_Lighting/` の Lightmap-0..3 `_comp_light.exr` と `_comp_dir.png`、および既存 `LightingData.asset` の更新。7 Scene 以外の `m_LightingSettings` は `{fileID: 0}` のまま。 seam の人間目視（最低条件 6）は §6 に記録済み。

### 7.1 発見 C 再確認（修正後 head）

- 種別: 発見 C の再確認。GO 判定 / 判定 C / C' は未実施。
- implementation base / head: `553b7b150e13245b369d75dc4baa12d86e9559aa` → `1241c8e7b4e475f934fc8931ba783c755508ee2d`（branch HEAD と一致、作業ツリー clean）。
- evidence bundle id / path: `s4c-discovery-c-rediscovery-1241c8e` / `artifacts/s-4c-phase-c-rediscovery-1241c8e/`。完全 diff、stat、name-status、固定 SHA、A3 snapshot と Phase B result snapshot、機械検査ログ、限定 filter の生ログ/XML を収録。
- bundle generated at: `2026-09-24T05:38:10.1490850+09:00`。
- 完全 diff SHA-256: `D65129BB7E64D639C87EC13A2BF4169A244EF9FD93AA565421B61F3C7BD08FEA`。規模: 110 files、+11,971 / −85 行（履歴中の Phase A と前回 discovery evidence もこの base→head 完全差分に含む）。
- 構造適合: Framework / SampleGame / `DependOnAll` の配置と依存方向は維持。修正差分は sink baseline 復元、Sink EditMode テスト、7 Scene の LightingSettings / GI contribution / bake artifacts。新しい公開 API・所有者・寿命・asmdef は無い。
- 前回 finding の状態:
  - F-C1 解消: `Spring_Representative.lighting` を作成し、代表7 Sceneのみ同 GUID の LightingSettings / LightingData を参照。設定に baked GI on / realtime GI off。代表 bake 出力として Lightmap-0..3 の `_comp_light.exr` / `_comp_dir.png` がある。春 sun は Mixed + Contribute GI、Full Cell / Environment の 17 MeshRenderer に Contribute GI。7 Sceneの構成がファイル差分で整合する。
  - F-C2 / F-C3 解消: sink が ambient sky / equator / ground / intensity と `RenderSettings.sun` を capture / restore し、`UnityRenderEnvironmentSinkTests` が Trilight 復元および null sun 復元を確認。
  - F-C4 解消: Spring の `m_Lightmapping: 1` は Unity enum `LightmapBakeType.Mixed`。HANDOFF §6 が意図した Mixed と一致する。数字 `1` を Realtime と解釈した前回所見は誤り。
  - 旧 F-C5 撤回（false positive）: SceneResource `.asset` を Addressables Group に個別登録する契約はない。SceneResourceMap が `_4_2` / `_5_2` asset GUID を直接参照し、対応 Scene `.unity` GUID が Local Group にある。既存 Full Cell / Environment resource と同じ構成であり、Player 起動失敗の記録だけから登録欠落とはいえない。
- 現在の問いを阻害する未解決事項:
- 再確認後の状態（2026-09-24）:
  - R-C1 解消: 人間が `(4,2)` → 東 `(5,2)` の床境界を目視し、§6 に「境界に明白な明暗差なし」と記録した。スクリーンショットは作成していない。
  - R-C2 は本コミットに含むコメント修正で対応。Framework Runtime Rendering の3コメントから検索語を除き、作業ツリーで同じ `rg` は 0 件。今回の evidence bundle は旧固定 head `1241c8e` を対象とするため、このコメント修正を含む新 head の完全な再監査ではない。
- 現在の問いを阻害する未解決事項: なし。今回の bundle は旧固定 head `1241c8e` に対する再確認記録であり、本コミットを含む head の再確認は未実施。
- 後続スライスへ移送する findings: なし。
- 実行したテスト: Phase B 修正時の限定起点 filter。HANDOFF記載の XML を bundle に複製し hash 固定。`34 passed / 0 failed`。新しいテスト起動はしていない。全 EditMode 回帰は未実行。
- 機械検査（新 head の bundle に結果を保存）: `contract-audit` exit 0（20 C# 差分）、`docs-audit` exit 0、SampleGame `RenderSettings` grep 0件、Framework season-term grep 3件（すべてコメント）。`git diff --check` は Unity 生成 YAML / `.meta` の空値行 trailing whitespace のみ。
- 未確認事項: 春の runtime Play 表示と fog / sun、Cell companion の live unload / reload と baked floor 再ロード、全 EditMode 回帰。
- 担当・モデル: Codex / GPT-6。Phase B 修正は Cursor Grok 4.7 と記録。C' は未実施。

### 7.2 判定 C（2026-09-24、c93cab0）

- 担当・モデル: Codex / GPT-6（OpenAI）。新規セッション。Phase B の Cursor Grok 4.7 / xAI と異なる。C' は起動しない。
- implementation base / head: `553b7b150e13245b369d75dc4baa12d86e9559aa` → `c93cab004724fe6ed4f9b011edfbd852b5e879d9`。開始時の `git rev-parse HEAD` が一致、`git status --short --branch` は対象ブランチ・clean・origin に対し ahead 1。今回の追記は review record であり実装 head に含めない。
- 旧 evidence との違い: §7.1 は `1241c8e` 固定。そこから今回 head へは記録 bundle / HANDOFF と Framework コメント3か所だけが変更された。実行文は同一だが、旧34件の結果を今回の全回帰に読み替えない。旧完全 diff hash と A3 hash は manifest と一致。A1 の実測 hash は旧 manifest の `B7CE692F...` と一致し、§0 の `38be...` とは不一致。A1 は A2 入力であり、今回の条件は A3 決定と固定 head の本文 §1〜5で照合した。旧 B snapshot の「境界目視未記録」は §6 の人間確認により更新済み。
- evidence id / path: `s4c-decision-c-c93cab0` / `artifacts/s-4c-phase-c-decision-c93cab0/`。完全 binary diff、stat、name-status、前headとの差、条件・B結果のsnapshot、監査ログ、テストの生結果、生成時刻と SHA-256 manifest を保存済み。生成時刻 `2026-09-24T07:15:58.7249253+09:00`、完全diff SHA-256 `880A9D393D9D7E0734020FDA5DEE3201700951513D62263FCB8CF73FEF9FEB1A`。C' blind bundle は未生成（判定必須証拠が未完備）。
- 構造適合（機能レビュー前に実施）: 公開6型 / internal Validator、policy と Unity sink の分離、Game の preset / Scene adapter、DependOnAll の配線が責務マップと一致。asmdef / SceneState / LoadType 差分なし。AppInitializer 383→422行は App サービス配線のため非分割妥当。SeasonLightingScene 42→118行は50%警報対象だが、presetとlease policyを別型へ分離済みで、Scene寿命の orchestration に限定。WorldCompanionCreationPlan の実在親パスへの修正は B 適応であり、新しいトランザクション責務はない。
- 最低条件との照合:
  1. lease / stale / baseline の既存テストと sinkテストを確認。全回帰の結果は下記。二度目 Bind の失敗契約には C2-1 がある。
  2. SampleGame `RenderSettings` grep 0件。
  3. 四季の注入、sun探索とidentity判定がAcquireより前、Acquire後の失敗時だけDispose、PreUnload解放をコード確認。runtime Play の観測は未完了。
  4. Cell Lighting は companion 分岐。既存 Directional 0 / Point 1以上の EditMode を確認。global設定を書くGameコードなし。
  5. 代表7 Sceneの LightingSettings / LightingData GUID参照と、追跡済み4組の lightmap を確認。四季 sun authored の許可済み変更と7 Sceneのbakeを区別。他のCell / Whiteboxのbake差分はない。
  6. §6 の人間による `(4,2)`→東`(5,2)` の「明白な明暗差なし」を採用。再確認を要求しない。
  7. §6 の Cell Lighting 閉鎖後のsun/fog維持確認は確認済みとして扱う。別途 §5 の runtime両Cell unload→復帰後のbaked床は未確認。
- 現在の問いを阻害する欠陥:
  - **C2-1 / P2 / semantic / unique / accepted**: `RenderEnvironment.BindSunFromLease`（78行）の二度目判定が `_boundSun != null`。Bind(A)→Aを破棄→同一lease.BindSun(B)ではUnity偽nullにより判定を通過し、Bを受け入れる。凍結 §2.4 の「BindSun は lease 生存中に1回。二度目は失敗」に違反する静的所見。実行再現は未実施、既存 `BindSun_Twice_Throws` は生存Lightだけを検査。修正先はB適応（公開面・所有者・寿命を変えない）。自動修正なし。
  - **C2-2 / P2 / machine / unique / accepted**: 現headの `docs-audit` がexit 1。既存 `artifacts/s-4c-phase-c-rediscovery-1241c8e/phase-b-result.md:7` の個別HANDOFFパス参照を検査2が検出。`docs/README.md` の層の契約と §5 の必須機械検査に未適合。旧bundleのexit 0を新headへ流用しない。旧証拠を無断で書き換えず、記録整理として修正対象とhash更新を明示する必要がある。
- 後続スライスの入力: B記録の Workspace→SceneGraph Editor / Layout非同期は凍結済み後続項目。Volume実行時所有、S-5の受け渡し / baseline間隙、S-9のProbeメモリ、Camera skyboxは従来の移送先のまま。これらを差し戻し根拠にしない。
- 機械検査: `pwsh tools/contract-audit.ps1` exit 0（20 C#差分、550ファイル）。指定の SampleGame `RenderSettings` と Framework `Season|Spring|Summer|Autumn|Winter|季節` grep は双方0件。`pwsh tools/docs-audit.ps1` exit 1（上記1件）。diff-checkはUnity生成YAML / metaの空値行の末尾空白。新しいコードのhard gate違反にはしない。
- 全回帰: `pwsh tools/run-tests.ps1`、Filter空、EditMode、既定nographics、Unity 6000.6.0f1。Editor接続なしとランナーのロック検査後、最初からsandbox外で実行。ライセンス接続成功。**908 passed / 0 failed / 0 skipped、Unity / runner exit 0、所要13.1分**。XMLのtest-case全908件と4アセンブリを確認（OneStarMaker.Tests 644、OneStarMaker.Tests.Editor 249、SampleGame.Tests.Editor 14、Addressables DocExample 1）。S-4c関連34件も収録（Lease 13 / Sink 2 / preset 5 / Factory 13 / Cell Lighting 1）。生ログ `all-editmode-run.log`、XML `all-editmode-results.xml`、全test-case名 `test-cases.txt`、対象集合 `required-test-sets.txt` をbundleに保存。元結果は `TestResults/results-all-20260924-070058.xml`。テストによるPC_RPAsset再シリアライズは差分保存後にHEADへ戻した。開始時cleanだった実装には変更を残していない。
- Editor / Play: 既存Editorは正しいproject / 6000.6.0f1 / PID 31640でreadyだったが、`tools/unity-editor.cmd command list_open_scenes` がCLI / Pipeline 0.4.0-exp.1不整合でexit 1（0.6.0-exp.1以降を要求）。強制終了せず保存確認と終了を依頼し、その後statusで接続なしを確認。固定headのpackageは更新しない。Play操作・観測は未完了。`content:runtimeMode` 未指定のfail-closedを緩めず、configや起動コードを変更していない。
- 判定必須の未完了 / 未確認: §5 のPlay Mode手動、最低条件7のruntime unload中の春fog/sunとbaked床再ロード。§6 の目視済み事項は含めない。
- **Phase C 判定: 保留（検証証拠不足）。GO / NO-GO は確定しない。** 全回帰結果と未確認範囲を記録して停止し、上記欠陥をBへ返す。最終判定には欠陥の解消と最低条件7のruntime証拠が必要。C'は未実施。今回の作業差分はHANDOFFとevidenceのみ（未コミット）。

### 7.3 判定 C 差し戻し（2026-09-24、Cursor Grok 4.7。発見のやり直しではない）

- C2-1: `BindSun` の一度きりは `_sunBound` で持つ。破棄済み `Light` は `== null` になるため、参照の有無では二度目を拒否できない。`BindSun_AfterBoundLightDestroyed_Throws` を追加。Apply は生きた Light が要るので、そちらは参照の `== null` のまま。
- C2-2: `artifacts/s-4c-phase-c-rediscovery-1241c8e/phase-b-result.md` の個別 HANDOFF パスを外した。旧 SHA-256 `87429090DB5F56BEB1433AF855D6666D0980476F95F1671CF793A10877C82C93`。更新後 `C043A3B0482F61060B7D6A8BB5140165A12840BC5BB76D4A7D8184B96C95FC62`。同 bundle の `manifest.json` の `phaseBResultSnapshot.sha256` だけをこの値へ更新した。実装 diff の hash は変えていない。
- 差し戻しフィルタ 35/35、exit 0。XML は `TestResults/results-RenderEnvironmentLeaseTests-SeasonLightingPresetTableTests-GameSceneFactoryTests-GameSceneLoggingTests-CellLightingSceneTests-UnityRenderEnvironmentSinkTests-20260924-073451.xml`。`contract-audit` と `docs-audit` はどちらも exit 0。
- 最低条件 7 の runtime Play（Cell unload 中の春 fog/sun と、戻ったときの baked 床）は未実施。起動契約は緩めていない。判定は保留のまま。C' は未実施。

### 7.4 判定 C 再実施（2026-09-24、Codex / GPT-6）

- 担当・モデル: Codex / GPT-6（OpenAI）。Phase B の差戻し修正は Cursor Grok 4.7 / xAI。現在の問いを独立に再判定した。C' は起動しない。
- 対象固定: branch `cursor/s-4c-world-lighting-a3-4a38`。implementation base `553b7b150e13245b369d75dc4baa12d86e9559aa`、implementation head `87a1cd297a0b96bb9743b3e47654a27130ebcffe`。開始時 HEAD は一致し、tracked worktree は clean。旧判定記録 commit は `c93cab004724fe6ed4f9b011edfbd852b5e879d9`。完全な base..head diff / stat / name-status と SHA-256 は manifest に固定。
- 固定証拠: `artifacts/s-4c-phase-c-rerun-87a1cd2/manifest.json`。Phase A snapshot `artifacts/s-4c-phase-a/` と A3 凍結本文、§6 Phase B 結果を確認。旧 rediscovery bundle は `1241c8e`、前回 decision bundle は `c93cab0` 固定であり、古いテスト結果を今回の pass 証拠へ読み替えていない。
- 構造レビュー（機能レビューより先）: 公開集合6型と internal Validator、Runtime Rendering の policy / Unity sink、SampleGame の season table / Scene adapter、`AppInitializer` と `GameSceneFactory` の配線は凍結責務マップに沿う。`_sunBound` は BindSun の一度きり契約だけを記録し、dispose で reset。公開 API、owner、generation、Scene 寿命を変えない。asmdef、SceneState、LoadType、package、依存方向の差分なし。新しい facade / 責務は導入されていない。
- 凍結最低条件 / 受け入れ条件: 1 は全回帰で単一 owner・stale・baseline 等を含み、新規 `BindSun_AfterBoundLightDestroyed_Throws` も pass。§2.4「BindSun は lease 生存中に1回」の C2-1 を解消。2 は SampleGame `RenderSettings` grep 0件。3 は四季 Scene の lease 配線と失敗時解放をコード確認。4 は2 Cell Lighting の Directional 0 / Point 1以上を確認。5 は7 Sceneの LightingSettings / LightingData と lightmap commit を確認。6 は §6 の人間目視「明白な明暗差なし」を採用し再要求しない。7 の §6 close 後 sun/fog 維持も確認済み。Play 中の両 Cell unload 時の春 fog/sun と再訪時 baked 床は未確認。
- 残件分類: Workspace Create が SceneGraph Editor / Layout を更新しない現況は後続送りで最低条件ではない。Volume 実行時所有、S-5 lease handoff、S-9 Probe memory、Camera skybox も後続。C2-1 はテスト済み、C2-2（docs-audit snapshot path 検出）は今回 pass。現在の問いを阻害するコード / bake 欠陥は確認しなかった。
- 判定必須全 EditMode 回帰: Windows、Editor を閉じ、標準 `pwsh tools/run-tests.ps1` を Filter 空で実行（Unity 6000.6.0f1）。初回 XML `all-editmode-results-attempt1.xml`: 909 total、908 pass / 1 fail / 0 skip。唯一の失敗は `WorldCompanionRecoveryTests.ReservedResource_ExternallyGivenPayload_FailsClosed`。AssetDatabase の一時テストフォルダ `Seasons.meta` 書込み失敗ログだった。同一テスト filter は 1/1 pass。その後 Filter 空で全回帰を再実行し 909/909 pass、0 fail / 0 skip、runner exit 0。最終 XML の assembly は `OneStarMaker.Tests` 645、`OneStarMaker.Tests.Editor` 249、`SampleGame.Tests.Editor` 14、`Unity.Addressables.DocExampleCode.Editor.Tests` 1。初回失敗も保存し、一過性 filesystem / AssetDatabase 失敗として扱う。
- 機械検査（implementation head 固定）: `pwsh tools/contract-audit.ps1` exit 0（違反なし、base develop から20 C#差分）。`pwsh tools/docs-audit.ps1` exit 0。指定 SampleGame `RenderSettings` grep 0件、指定 Framework season-term grep 0件。ログを bundle に保存。
- Editor / Play 未確認: 最低条件7のruntime観測は未実施。正しい project / Unity 6000.6.0f1 を起動したが、Editor Pipeline `0.4.0-exp.1` が CLI `command list_open_scenes` に必要な `0.6.0-exp.1` 以降より古く、コマンドが拒否された。Play 中の cell unload/reload と fog/sun / baked floor は観測できず。package upgrade はせず、`content:runtimeMode` 契約も変更していない。試行ログは bundle。§6 の seam / close 後確認は未確認扱いにしない。
- 判定: **Phase C 保留（検証証拠不足）。GO / NO-GO は確定しない。** 全 EditMode と機械検査は成功したが、最低条件7の必須 Play runtime 観測が未完了。阻害欠陥は未検出。後続入力は上記分類。C' は起動しない。
- テストの `PC_RPAsset.asset` 再シリアライズ差分は bundle に保存後、implementation head の内容へ復元。今回の review record のみを別 commit にする。

### 7.5 Pipeline 更新後の Phase C 追補（2026-09-25、Codex / GPT-6）

- 対象実装は §7.4 と同じ `87a1cd297a0b96bb9743b3e47654a27130ebcffe`。現在の review-record HEAD は `d550832b934910b3f40b084e2dd09058298d3824`。Phase A / B snapshot、構造レビュー、差分、C' を起動しない規則は §7.4 の固定記録を引き継ぐ。
- 追補 evidence: `artifacts/s-4c-phase-c-pipeline-followup-20260925/`。manifest SHA-256 `2DB249373E6510A3EF27F34AE89C442330C22E138F4327D513B6F64BEFCD5D06`（`manifest.sha256`）。§7.4 の判定 bundle を書き換えず、今回の実行証拠を別束に固定した。
- Unity `6000.6.0f1`。ユーザーが `com.unity.pipeline` を `0.4.0-exp.1` から `0.7.0-exp.1` へ更新して Editor / Pipeline 操作を可能にした。manifest / lock の変更はユーザーの未コミット差分として保持し、この記録コミットへ含めない。実装コード、`content:runtimeMode`、fail-closed 起動契約は変更していない。Spring Full content revision `20260924T141708718Z-8e6155e6a2a449128189a1c1a1d28d7e` を既存の Content Delivery 検証経路で準備・適用し、Title からゲームへ遷移した。
- Play 観測: 14:53 UTC 頃、25 Scene が open。Player は `(1319.15, 4.48, 984.51)` で `(5,3)`、`Spring_Cell_5_2` / `Spring_Environment_5_2` / `Spring_Lighting_5_2` がロード済み。`Spring_Cell_5_2` の `Ground.lightmapIndex=0`、`LightmapSettings.lightmaps.Length=2`、scale/offset が非ゼロ。これは target Cell resident 中の baked lightmap 割当を示す。
- 同 snapshot および両 target Cell の unload 後 snapshot で `RenderSettings` は Spring preset と一致: fog enabled / `ExponentialSquared` / `(0.750, 0.820, 0.880, 1.000)` / density `0.0015`; ambient `Flat` / sky `(0.180, 0.220, 0.280, 1.000)`; sun `SeasonSun` / rotation `(15,330,0)` / color `(1.000,0.850,0.700,1.000)` / intensity `0.8`。
- 人間確認: ユーザーは `(4,2)` と `(5,2)` の両方を訪問し、その後両方を Unload 状態にしたと報告。後続の Pipeline Scene snapshot は `Spring_Lighting` を保持しつつ両 target Cell と companion を含まない状態だった。旧 §6 の seam と Cell Lighting close 観測は今回も確認済みとして採用し、再要求していない。
- 未確認: Unload 後に target Cell へ戻ったときの baked 床再表示。`Ground.lightmapIndex=0` の証拠は unload 前の resident snapshotであり、reload 後へ読み替えない。Pipeline の `FlyController.Teleport` probe は位置を維持できず、この条件の証拠から除外した。目視再確認が得られないため最低条件 7 の必須証拠不足として扱う。
- 判定必須全 EditMode 回帰: Windows Editor を閉じ、`pwsh tools/run-tests.ps1` を Filter 空で実行。Unity exit 0、909 passed / 0 failed / 0 skipped、XML duration 682.287 秒。assembly は `OneStarMaker.Tests` 645、`OneStarMaker.Tests.Editor` 249、`SampleGame.Tests.Editor` 14、`Unity.Addressables.DocExampleCode.Editor.Tests` 1。生ログ `all-editmode-run.log` と XML `all-editmode-results.xml` を保存。
- 機械検査: `pwsh tools/contract-audit.ps1` exit 0（違反なし）、`pwsh tools/docs-audit.ps1` exit 0（本追補後に再実行）、指定 SampleGame `RenderSettings` grep と Framework season-term grep はいずれも0件。
- `PC_RPAsset.asset` の Unity 再シリアライズ差分は bundle に退避して implementation head に復元済み。`unity/UserSettings/OSMContentDelivery.json` も開始時のバックアップへ復元。package manifest / lock 以外のユーザー変更は残していない。
- 現在の問いを阻害する実装欠陥: 今回の証拠から新たに確定したものはなし。後続スライス向け入力: §7.4 の分類を維持。Phase C は **保留（必須 runtime 証拠不足）**。GO / NO-GO を確定しない。保留理由は Unload 後の baked 床 reload 観測不足であり、実装修正の差戻しではない。C' は未起動。
- 担当・モデル: Codex / GPT-6。test environment の Pipeline package overlay とログ、XML、runtime probe は追補 evidence bundle に固定。

## 8. Phase C'

- 担当方式: 未実施
- 判定: 未実施
- 担当・モデル:

## 9. Phase D

- 未実施

## 10. Light Probe 共有の送り状（S-9）

Unity の multi-scene bake では、同時に開いた Scene を跨ぐ shadow / GI bounce を計算し、lightmap は Scene 単位でロード・アンロードする。同時ベイクした Light Probe data は共有される。

S-4c はこの性質を受け入れ、Probe 方式を変えない。S-9 の実コンテンツ計測は、代表 2 Cell が resident のときと片側 Unload のときで Probe メモリがどう残るかを項目に含める。本スライスは数値予算を置かない。
