# S-4c World Lighting — HANDOFF

> Phase B の正本。ここに無い公開 API・依存・所有者・寿命・失敗契約は実装しない。衝突したら Phase A へ返す。

## 0. メタデータ

- type: `slice`
- status: `A`
- branch: `cursor/s-4c-world-lighting-a3-4a38`
- implementation base commit: `553b7b150e13245b369d75dc4baa12d86e9559aa`
- implementation head commit: （Phase B で追記）
- risk: `high`
- owner: Phase A 主担当 → Phase B は人間
- created: 2026-09-21
- expires: S-4d の Phase D 完了時
- harvest to: Architecture §24（REN-08 現況）、§05（Season Lighting lease）、§23 Volume 境界の確認、§27 フォルダ（Cell Lighting 2 件）、STREAMING には距離政策を足さない
- Phase A snapshot path: `artifacts/s-4c-phase-a/`
- Phase A snapshot generated at: （A3 凍結時に記入）
- Phase A snapshot hash: （A3 凍結時に記入）
- Phase B result snapshot path / id: （未到達）
- evidence bundle / C' blind bundle: （未到達）

## 1. 目的と対象外

- 目的: 季節グローバル照明の単一オーナー（lease）を Framework に置き、PlayerScene の `RenderSettings` 直書きを廃し、Spring 代表 2 Cell で multi-scene bake を成立させる。
- 対象外: `IRenderingSystem` / RenderWorld / BRG、24h TimeOfDay、Weather 合成、全 216 bake、Light Probe 再評価（S-9）、VFX / Events（S-4d）、Tunnel 遷移（S-5）、新しい LoadType / SceneState、Framework への季節語、一時生成器の復活、Whitebox bake、URP パッケージ追加。
- 現況: A0 packet `artifacts/s-4c-phase-a/A0-packet.md` に固定。Season Lighting 4 件は空 scaffold。Cell Lighting 0 件。`PlayerScene.ApplyDemoLook` が fog / ambient を直書き。Runtime は URP 非参照。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 季節グローバルな見た目状態を App lifetime の単一 RenderEnvironment が lease で所有でき、Spring `(4,2)` `(5,2)` の multi-scene bake が Scene 単位の着脱として成立するか。

- 進める最低条件:
  1. 純 C# テストが、単一 owner・二件目即失敗・stale Dispose / stale Apply が新 owner を消さないこと、解放で baseline に戻ることを示す。
  2. Game 層（少なくとも `PlayerScene`）から `RenderSettings` 代入が消えている。
  3. 四季の `SeasonLightingScene` が Load で lease を取り preset を Apply し、Unload で解放する。二件目の Season Lighting が Load したら失敗する。
  4. `Spring_Lighting_4_2` と `Spring_Lighting_5_2` が World Workspace で作られ、Directional Light を持たず、global sun / sky / fog / global Volume を書き換えない。
  5. 代表 7 Scene（Season Lighting + 両 Cell + 両 Environment + 両 Cell Lighting）を同時に開いた Progressive bake の lightmap が commit されている。
  6. 人間が `(4,2)` / `(5,2)` 境界で明白な lightmap seam が無いことを HANDOFF 実装結果へ記録する。
  7. Phase C が Cell unload / reload 後に fog/sun と baked data が復帰することを観測できる（判定必須の Play 手順が本文にある）。

- 受け入れ条件（上記の観測可能な詳細。別バーにしない）:
  - `IRenderEnvironment.Acquire(object ownerKey)` は空きなら lease を返す。既に owner がいるなら `InvalidOperationException`。暗黙の待ち・上書きはしない。
  - lease は整数 generation を token として持つ。Dispose は generation 一致のときだけ owner を空ける。不一致は no-op。
  - Dispose 済み lease の `Apply` は `InvalidOperationException`。新 owner の state は変わらない。
  - 同一 lease の二度目の Dispose は no-op。
  - `RenderEnvironmentState` は struct（`record` 禁止）。フィールドは本節の表に固定した値だけ。
  - SampleGame の preset 表が四季 identity → state を返す。Framework は Spring 等の語を持たない。
  - Unity sink は `Light`（bind された太陽）と `RenderSettings`（fog / ambient）へ書く。`Volume.weight` の URP 型操作は **しない**（後述裁定）。
  - `SeasonLightingScene` は `OnLoadedImpl` で `SeasonSun` を見つけ、無ければ失敗。lease.Acquire → BindSun → Apply。`OnPreUnLoadedImpl` で Dispose。
  - `PlayerScene.ApplyDemoLook` を削除する。Camera clear / background は CameraSystem 側の既存設定を残し、RenderEnvironment は触らない。
  - Cell Lighting 2 件の Scene 内に `LightType.Directional` が 0。local Point を各 1 以上置く。
  - bake 対象 Scene 以外の `.unity` を再シリアライズしない。Whitebox は焼かない。
  - Light Probe 共有は受け入れ事実として実装結果と S-9 送り状に書く。本スライスで Probe 方式を変えない。

- ここでは答えない問いと所有する後続スライス:
  - URP `Volume.weight` を Framework adapter が直接書くこと → 後続 `RenderEnvironment Volume 反映`（本スライスは authored Volume を Scene に置いてよいが、Runtime は触らない）
  - 季節遷移シーケンスでの lease 受け渡し → S-5
  - 全 Cell bake / Probe 再評価 / 共有 Probe のメモリ → S-9
  - VFX Graph、Atmosphere VFX、Events、major Event → S-4d
  - 品質バー（3 秒で主題と変奏が分かる）の完成目視 → S-8
  - TimeOfDay 時計 → REN-08 残り / P-R6
  - Camera skybox material の季節別差し替え → 後続。本スライスは fog / ambient / sun で変奏を載せる

- 判定定義:
  - GO: 進める最低条件 1〜7 を満たし、常時契約違反が無い。
  - NO-GO: 最低条件未達、または Game→Framework 逆転、SceneState 変更、URP asmdef 追加、一時生成器復活、216 bake。
  - CONDITIONAL ACCEPT: 使わない（スパイクではない）。

- 停止規則: 最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。

- A3 後の例外承認: なし（A3 で記入。凍結前は空）

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

- 未決事項: A3 凍結後は「なし」。凍結前の裁定候補は §2.1。

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

identity 完全一致。未知 identity は `SeasonLightingScene` が失敗（lease を取ったなら必ず Dispose する）。

| Identity | SunEuler | SunColor | Intensity | AmbientSky | FogEnabled | FogColor | FogDensity | VolumeWeight |
|---|---|---|---|---|---|---|---|---|
| `Spring_Lighting` | `(15, -30, 0)` | `(1.00, 0.85, 0.70)` | `0.80` | `(0.18, 0.22, 0.28)` | true | `(0.75, 0.82, 0.88)` | `0.0015` | `1` |
| `Summer_Lighting` | `(50, 40, 0)` | `(1.00, 0.98, 0.90)` | `1.30` | `(0.28, 0.30, 0.32)` | true | `(0.70, 0.78, 0.85)` | `0.0040` | `1` |
| `Autumn_Lighting` | `(8, 50, 0)` | `(1.00, 0.55, 0.25)` | `0.70` | `(0.18, 0.13, 0.08)` | true | `(0.55, 0.38, 0.22)` | `0.0025` | `1` |
| `Winter_Lighting` | `(70, 0, 0)` | `(0.95, 0.97, 1.00)` | `0.50` | `(0.68, 0.70, 0.74)` | true | `(0.90, 0.92, 0.95)` | `0.0020` | `1` |

数値の微調整は **B 適応にしない**。見た目が致命的に反証する場合だけ Phase A 再開。

### 2.4 凍結する公開 API（Framework）

配置 namespace: `OneStarMaker.Runtime.Rendering`

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
    // get-only プロパティ。Equals は書かなくてよい（テストはフィールド比較）。
}

public sealed class RenderEnvironmentLease : IDisposable
{
    public int Generation { get; }
    public void BindSun(Light sun);          // UnityEngine.Light。null は失敗
    public void Apply(in RenderEnvironmentState state);
    public void Dispose();                   // generation 不一致は no-op
}

public interface IRenderEnvironment
{
    bool HasActiveOwner { get; }
    RenderEnvironmentLease Acquire(object ownerKey);
}

public interface IRenderEnvironmentSink
{
    void CaptureBaseline();
    void Apply(in RenderEnvironmentState state, Light? sun);
    void RestoreBaseline();
}
```

- `IRenderEnvironment` の実装は `RenderEnvironment`（sealed）。constructor は `IRenderEnvironmentSink` 必須。
- `Acquire` 成功時に sink.`CaptureBaseline()` を一度呼ぶ（その owner 期間の復元点）。
- matching Dispose で sink.`RestoreBaseline()`。stale Dispose では呼ばない。
- `BindSun` 前の `Apply` は失敗（太陽が無いのに sun を書けない）。`BindSun` は lease 生存中に 1 回。二度目は失敗。
- `ownerKey` は照合 token に使わない。ログとデバッグ用。token は generation。`ownerKey == null` は `ArgumentNullException`。
- static / Service Locator は設けない。

`IRenderingSystem` は作らない。

## 3. 責務マップ

行数は 2026-09-21 の HEAD。予想は純増の目安。

### 3.1 新規

| ファイル | 責務（一文） | 変更理由 | 所有者・寿命 | 入力/出力依存 | 層 | 公開面 | テスト境界 | 配置理由 | 行数目安 |
|---|---|---|---|---|---|---|---|---|---|
| `unity/Assets/OneStarMaker/Scripts/Runtime/Rendering/RenderEnvironmentState.cs` | グローバル見た目の純データ | REN-08 前倒し | 値型。寿命なし | Unity `Vector3`/`Color` のみ | Policy | 公開 struct | 値の Validate を Environment 側で | CameraSystem の Geometry と同様、Runtime 配下に Rendering フォルダを新設。Framework/SampleGame・公開 API の差がある | ~50 |
| `unity/Assets/OneStarMaker/Scripts/Runtime/Rendering/IRenderEnvironment.cs` | 単一 owner の取得口 | 公開 API 限定追加 | App | なし | 公開 API | 公開 interface | Fake 実装で | 同上 | ~20 |
| `unity/Assets/OneStarMaker/Scripts/Runtime/Rendering/IRenderEnvironmentSink.cs` | state を装置へ翻訳する口 | policy と I/O を分ける | App（実装次第） | Light / RenderSettings | 公開面は interface。Unity I/O は実装 | 公開 interface（テスト差し替え） | Fake sink | CameraSystem の `ICameraBackend` と同型 | ~20 |
| `unity/Assets/OneStarMaker/Scripts/Runtime/Rendering/RenderEnvironmentLease.cs` | generation token と Apply/Dispose | stale 耐性 | lease オブジェクト。App 上の世代 | RenderEnvironment 内部 | Policy | 公開 sealed | 純 C# | Handle を独立型にしないと token 照合が埋もれる | ~80 |
| `unity/Assets/OneStarMaker/Scripts/Runtime/Rendering/RenderEnvironment.cs` | 単一 owner と generation の調停 | 同時 1 Season | App lifetime。`AppInitializer` が生成・破棄 | sink | Policy + orchestration | 公開 sealed | Fake sink で Unity なし | CameraSystem 本体に相当。Host GO は持たない | ~120 |
| `unity/Assets/OneStarMaker/Scripts/Runtime/Rendering/RenderEnvironmentValidator.cs` | state の fail-closed 検証 | 負の intensity 等を装置へ流さない | なし（純関数） | state のみ | Policy | internal | 純 C# | Environment に検証を混ぜるとテストと調停が混線する | ~40 |
| `unity/Assets/OneStarMaker/Scripts/Runtime/Rendering/UnityRenderEnvironmentSink.cs` | Light と RenderSettings への反映と baseline 復元 | 唯一の Unity I/O | App。RenderSettings はプロセスグローバル | `Light`, `RenderSettings` | Unity I/O | internal | Play/Edit は薄い。主検証は Fake | URP 型を使わない。Runtime 既存 UnityEngine 依存の範囲 | ~80 |
| `unity/Assets/SampleGame/InGame/InGameSession/World/SeasonLightingPresetTable.cs` | identity → state | 季節語を Game に閉じる | なし（静的表） | なし | Policy（Game） | internal | 純 C# | SeasonLightingScene に表を直書きすると Scene 寿命と数値が混ざる | ~80 |
| `unity/Assets/OneStarMaker/Tests/Rendering/RenderEnvironmentLeaseTests.cs` | 単一 owner / stale / restore | 最低条件 1 | テスト寿命 | Fake sink | テスト | なし | EditMode 純 C# | Camera テストと同配置 | ~250 |
| `unity/Assets/OneStarMaker/Tests/SampleGame/SeasonLightingPresetTableTests.cs` | 四季表の固定値 | 最低条件 3 の入力 | テスト寿命 | なし | テスト | なし | EditMode | SampleGame テスト | ~80 |

Rendering フォルダ新設の理由: Framework / SampleGame ではなく、CameraSystem と並ぶ Runtime サブシステム境界。Helpers/Managers ではない。

`RenderEnvironmentLease` と `RenderEnvironment` は変更理由が近いが、token 型を Environment の inner class にすると公開 API が読みにくい。行数警報（500 / 3 責務 / 50%）は単体では未達。非分割: `Lease` を独立ファイルにするのは公開 Handle のためであり、行数削減のための委譲ではない。

### 3.2 既存変更

| ファイル | 現在行 | 予想増減 | 責務 | 変更理由 | 分割判断 |
|---|---|---|---|---|---|
| `SeasonLightingScene.cs` | 42 | +70 | 季節 Lighting の寿命で lease を持つ | scaffold を本番にする | 非分割。SceneBase 1 ファイル 1 Scene 契約 |
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

Phase B 担当は **この節の順序** で進める。設計判断が必要になったら止まる。Unity テストは実行しない。

### 4.1 変更対象（コード）

1. Runtime Rendering 新規 7 ファイル（State, 2 interface, Lease, Environment, Validator, Unity sink）
2. SampleGame preset 表
3. `SeasonLightingScene` / `PlayerScene` / `GameSceneFactory` / `AppInitializer`
4. Factory テスト + lease テスト + preset テスト
5. Editor コンテンツ: 4 Season Lighting の `SeasonSun`、2 Cell Lighting、代表 bake

### 4.2 順序

**B0. 準備**

- ローカルで `tools/unity-editor.cmd status`。閉じていれば ProjectVersion 6000.6.0f1 の Editor をこの `unity/` で開く。Cloud なら B0 をスキップし、C# だけ書いてコンテンツ手順を実装結果に「未実行」と書く。人間 B はローカル必須（bake があるため）。
- 既存 Editor が別 project なら接続しない。
- `git status` がこのブランチで clean なことを確認してからコンテンツ以外の C# を書く。

**B1. Fake で lease を先に通す（Unity Scene を開かない）**

1. `RenderEnvironmentState` と `RenderEnvironmentValidator` を書く。Validate 失敗は `ArgumentOutOfRangeException`。
2. `IRenderEnvironmentSink` のテスト用 `FakeRenderEnvironmentSink` を Tests アセンブリへ置く（Runtime にテスト型を置かない）。記録: `CaptureCount`, `RestoreCount`, `Applies` リスト, 最後の `Light` 参照は持たず `SunBound` bool だけ。
3. `RenderEnvironment` + `RenderEnvironmentLease` を書く。
4. `RenderEnvironmentLeaseTests` を書く。最低ケース:
   - `Acquire_First_Succeeds_AndHasOwner`
   - `Acquire_Second_Throws_AndKeepsFirstOwner`
   - `Dispose_Matching_ClearsOwner_AndRestoresBaseline`
   - `Dispose_StaleAfterNewOwner_DoesNotClearNewOwner_AndDoesNotRestore`
   - `Apply_OnDisposedLease_Throws_AndDoesNotMutateSink`
   - `Dispose_Twice_IsIdempotent`
   - `Acquire_NullOwner_Throws`
   - `Apply_BeforeBindSun_Throws`
   - `BindSun_NullLight_Throws`（Fake では `new Light` は使わず、Tests では `BindSun` を Fake 経由で null チェックだけでも可。UnityEngine.Light を EditMode で new できないなら、null ケースと「非 null なら Apply が sink に届く」を Fake の `Apply` 呼び出し回数で見る。Light 実体が要るテストは Unity sink の薄い EditMode に限る）
   - `Validate_NegativeIntensity_Throws`
5. ここで `pwsh tools/run-tests.ps1` は **まだ走らせない**。コンパイルは Editor が開いていれば確認してよい。

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

`AppInitializer`:

- フィールド `_renderEnvironment`。
- `InitializeCameraSystem` の直後（Factory より前）に `new RenderEnvironment(new UnityRenderEnvironmentSink())`。
- `CreateSceneFactory` の引数に渡す。null なら Factory constructor が失敗するので、未初期化なら `InvalidOperationException`。
- `ReleaseCameraSystem` と同じタイミング（quitting / AfterSceneLoad 失敗 / SubsystemRegistration）で、残っている owner がいても Environment を破棄してよい。`IDisposable` を Environment に実装し、Dispose 中に active lease があれば matching 相当の強制 Restore + owner クリア。二重 Dispose 安全。
- CameraSystem より先に Environment を破棄しない。依存は無いが、解放順は「Factory が使わない状態にしてから」でよい。具体: `ReleaseCameraSystem` 内の末尾、または独立 `ReleaseRenderEnvironment` を `OnAfterSceneLoadInitializationFailed` と Sub から呼ぶ。

`GameSceneFactory`:

- 必須引数 `IRenderEnvironment renderEnvironment`。null は `ArgumentNullException`。
- `Spring_Lighting` 他 3 件の constructor に渡す。
- 他 Scene は受け取らない。

`SeasonLightingScene`:

```
OnLoadedImpl:
  sun = Find SeasonSun in RootObjects (name 完全一致)
  sun が Unity 偽 null または Light でない、または Directional でない → InvalidOperationException
  Try の外で lease を持たない
  _lease = _renderEnvironment.Acquire(this)
  以降の失敗は finally 相当で _lease.Dispose() してから throw
  _lease.BindSun(sun)
  if (!SeasonLightingPresetTable.TryGet(SceneResource.Identity, out var state)) 失敗
  _lease.Apply(state)

OnPreUnLoadedImpl:
  _lease?.Dispose(); _lease = null;
```

`Find` は `SceneBase` の RootObjects API を使う。無ければ既存 Scene の検索方法に合わせ、新しい反射を増やすな。

`PlayerScene`: `ApplyDemoLook` メソッドと呼び出しを削除。`RenderSettings` using が不要なら取り除く。Camera bind は残す。

**B4. テスト更新**

- `GameSceneFactory` の全 constructor 呼び出しに Fake `IRenderEnvironment` を足す。null 拒否テストを 1 件追加。
- Season Lighting 生成が environment を保持していることは、必須引数を渡したうえで型が `SeasonLightingScene` のままであることを既存テストで維持。

**B5. Editor コンテンツ（YAML 手編集禁止）**

対象を限定する。広範な再生成はしない。

1. World Workspace で Spring、座標 `(4,2)`、職種 Lighting、payload Full。optional が欠けるので Create。`Spring_Lighting_4_2` ができる。`(5,2)` も同様。
2. 各 Season Lighting Scene（4 件）を開き、空の `SeasonLightingRoot` 配下へ `SeasonSun` という Directional Light を作る。rotation/color/intensity は preset 表と同じ値にして bake と Play を一致させる。`Lightmap Static` はオン。
3. 任意: 同じ Scene に global Volume GO（isGlobal）を置いてよい。Runtime は触らない。無ければそれでも S-4c GO。
4. 各 Cell Lighting Scene に `LocalFill` という Point Light を 1 つ。`(4,2)` は見証付近（Cell 中心付近、地上 4m 目安）、`(5,2)` は隣接 Cell 中心。range は Cell を大きくはみ出さない（≤ 80）。shadows はオンでもオフでもよいが **Directional は置かない**。
5. Lighting Settings: 新規 asset を `Assets/SampleGame/InGame/InGameSession/Seasons/Spring/Spring_Representative.lighting` に作り、代表 7 Scene だけに割り当てる。bakedGI on、realtime GI off。既存他 Scene の LightingSettings は触らない。
6. 7 Scene を同時に開く:
   - `Spring_Lighting`
   - `Spring_Cell_4_2`（Full。Whitebox ではない）
   - `Spring_Environment_4_2`
   - `Spring_Lighting_4_2`
   - `Spring_Cell_5_2`
   - `Spring_Environment_5_2`
   - `Spring_Lighting_5_2`
7. Lighting window で Generate Lighting。完了まで待つ。失敗したら Phase A へ返さず、まず LightingSettings / 光源欠落を確認。計画外 API が必要なら停止。
8. 7 Scene を保存。**他の開いていない Scene を Save All で巻き込むな。** lightmap テクスチャと LightingDataAsset が 7 Scene 配下または `*/*.exr` 近傍に増える。それを commit 対象にする。
9. 境界の目視: 見証 `(4,2)` から東の `(5,2)` へ床が続く線。明白な明るさ段差が無いか。結果を HANDOFF §6 に文章で残す。スクリーンショットは任意（`artifacts/s-4c-phase-b/`）。
10. Cell Lighting を閉じて Cell だけ残したときの sun/fog が Season Lighting 由来のままであること（local を消しても空が春のまま）を Editor で一度見る。

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

- `IRenderEnvironment` 以外の公開 API が必要
- URP / 新規 asmdef が必要
- Host DontDestroyOnLoad の太陽が必要
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
  - `RenderEnvironmentLeaseTests`（必須。最低条件 1）
  - `SeasonLightingPresetTableTests`（表の 4 identity と未知 identity）
  - `GameSceneFactory` の引数契約
  - Validator の負値拒否（lease テストに含めてよい）

- 差し戻し中の起点 `-Filter`:
  ```
  RenderEnvironmentLeaseTests|SeasonLightingPresetTableTests|GameSceneFactoryTests|GameSceneLoggingTests
  ```
  C は凍結済み条件の確認に必要なら根拠を書いて変更してよい。受け入れ条件の追加ではない。

- 判定必須テスト:
  1. 最終の全 EditMode 回帰（空 filter）。`pwsh tools/run-tests.ps1`。Editor を閉じてから。Windows なら sandbox 外。
  2. Play Mode 手動（判定 C）: `world:cellCompanionSet=Lighting` または Full。Spring 起動。スポーンから `(4,2)` `(5,2)` へ移動。fog/sun が春 preset。Cell を UnloadRadius 外へ出て両 Cell が落ち、戻って baked 床が復帰。二重 Season は今のコントローラでは起きないので、lease 二件目は EditMode で足りる。
  3. 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、季節語 grep、SampleGame `RenderSettings` grep。

- 全 EditMode 回帰の適用除外: なし。

- 統合・Unity テスト: 新規 PlayMode 自動テストは **必須にしない**。unload/reload 復帰は判定 C の Play 目視 + ログ（SeasonLightingScene の既存 ZLog）で足りる。自動 PlayMode を足すなら既存 Streaming 統合の流儀に合わせ、`Task.Delay` を使わない。足さなくても GO 可。

- 機械検査: contract-audit、docs-audit、上記 grep。

- A0/A1 主担当・モデル・ベンダー: Cursor Grok 4.6 / xAI（このセッション）
- A2 独立レビュー: 後記（A3 で埋める）
- A3 統合担当: 後記
- C' 用に予約した担当: **Claude 系または GPT 系**（Phase A は Grok 主担当。強化条件のため A2 でも同系列を使い切らない）
- 独立性の強化条件を満たせない場合の理由: A2 を同一セッションの別エージェントで行う場合はモデル相違が弱い。そのときは C' を必ず別系列・新規セッションにする。HANDOFF に `独立性制約あり` と書く。

## 6. Phase B 実装結果

- 実装: 未着手
- HANDOFF との差: 未着手
- 未実行: 未着手
- implementation head commit:
- Phase B 担当・モデル・ベンダー:

## 7. Phase C

- 種別: 未実施
- evidence bundle id / hash:
- 構造適合:
- 現在の問いを阻害する findings:
- 後続スライスへ移送する findings:
- 実行したテストコマンド:
- テスト結果:
- 判定必須のうち未実行:
- 未確認事項:
- 担当・モデル:

## 8. Phase C'

- 担当方式: 未実施
- 判定: 未実施
- 担当・モデル:

## 9. Phase D

- 未実施

## 10. Light Probe 共有の送り状（S-9）

Unity の multi-scene bake では、同時に開いた Scene を跨ぐ shadow / GI bounce を計算し、lightmap は Scene 単位でロード・アンロードする。同時ベイクした Light Probe data は共有される。

S-4c はこの性質を受け入れ、Probe 方式を変えない。S-9 の実コンテンツ計測は、代表 2 Cell が resident のときと片側 Unload のときで Probe メモリがどう残るかを項目に含める。本スライスは数値予算を置かない。
