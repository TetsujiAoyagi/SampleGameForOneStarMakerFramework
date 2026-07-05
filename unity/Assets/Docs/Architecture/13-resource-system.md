# 13. リソースシステム + メモリバジェット設計

> ステータス: AssetResidentCache(常駐キャッシュ)実装済み。テレメトリ配線/品質降格は次パス (2026-07-05)
> 優先度: コア API 安定化後に Cache 実装へ進む

---

## 目次

1. [目的](#1-目的)
2. [決定事項サマリ](#2-決定事項サマリ)
3. [設計思想](#3-設計思想)
4. [レイヤー構成](#4-レイヤー構成)
5. [Interface 設計](#5-interface-設計)
6. [AssetType / QualityLevel](#6-assettype--qualitylevel)
7. [AssetDescription 汎用化](#7-assetdescription-汎用化)
8. [メモリバジェット](#8-メモリバジェット)
9. [キャッシュ戦略](#9-キャッシュ戦略)
10. [バジェット超過フロー](#10-バジェット超過フロー)
11. [メモリテレメトリ](#11-メモリテレメトリ)
12. [STG 向け簡易実装](#12-stg-向け簡易実装)
13. [施行 (T9-T15)](#13-施行-t9-t15)
14. [トレードオフ記録](#14-トレードオフ記録)
15. [将来拡張](#15-将来拡張)
16. [受け入れた前提と制約](#16-受け入れた前提と制約)

---

## 1. 目的

- プロジェクト毎に **メモリバジェット** を定義し、カテゴリ別に予算管理する
- **AssetKey.AssetType** にアセット種別メタを保持する
- 将来パスで **LFU + 時間減衰キャッシュ** を運用し、頻繁に使うものを保持・使わないものを解放する
- **Mesh LOD** と **Texture Mip** を独立に interface 化し、将来の高度なストリーミング（MeshShader 等）に対応する
- キャッシュの使用状況を **テレメトリ** に統合する

---

## 2. 決定事項サマリ

| 項目 | 決定 |
|---|---|
| LOD 制御 | Unity 標準 LODGroup を土台。`ILodProvider` interface で抽象化 |
| Mip 制御 | Unity Texture Streaming を土台。`IMipStreamingProvider` interface で抽象化 |
| LOD と Mip | 独立 interface として分離（制御対象が異なる） |
| キャッシュ戦略 | LFU + 時間減衰（halfLife = 300s）。カテゴリ別バジェットプール |
| バジェット定義 | ScriptableObject + AppConfig Override |
| 概算メモリ | 精度は追求しない。参考値として記録。Editor バッチツールで算出 |
| STG 向け実装 | Full / Unloaded の 2 段階。品質降格は将来有効化 |
| QualityLevel | 4 段階定義（Full / Reduced / Minimum / Unloaded）。STG では Full/Unloaded のみ使用 |
| 現行コア | `IAssetManagement` + `AssetRegistry` でスコープ付き寿命管理。Addressables 型は公開 API へ出さない |
| キャッシュ配置 | **常駐キャッシュ方式**。`AssetManagement` 内に統合し、refcount 0 のアセットを `AssetResidentCache` に退避。独立 `IResourceCache` レイヤーは不採用 |
| バジェット計上 | **キャッシュ内 (refcount 0) のみ**。使用中アセットは計上しない |
| テレメトリ結合 | `AssetResidentCache.GetSnapshot()` をテレメトリ層がポーリング（配線は次パス）。R3 はプロジェクトに存在しない |

---

## 3. 設計思想

### なぜ概算精度は重要でないか

- MeshShader / LOD によってメッシュ頂点数がフレーム毎に動的に変化する
- Texture Streaming によって Mip レベルが距離に応じて動的に変化する
- 実際の VRAM 使用量はフレーム毎に変わる → 静的な概算は参考値でしかない

### それでもバジェットが必要な理由

- 上限がないと際限なくロードする
- バジェット超過時に「品質を下げる / アセットを捨てる」の判断基準になる
- テレメトリで実使用量とバジェットの関係を追跡できる

---

## 4. レイヤー構成

```
┌──────────────────────────────────────────────────────────┐
│  Game 層 (SceneDirector から透過的に利用)                  │
├──────────────────────────────────────────────────────────┤
│  IAssetManagement ← スコープ付きロード/解放 + 常駐キャッシュ   │
│    ├── IAssetHandle / ISceneHandle                         │
│    ├── AssetKey / AssetOwner / SceneLoadOptions             │
│    ├── AssetRegistry ← refcount + owner tracking            │
│    └── AssetResidentCache ← refcount 0 退避 / LFU エビクション│
│          └── IBudgetProvider (MemoryBudgetConfig)            │
├──────────────────────────────────────────────────────────┤
│  IQualityPolicy (将来)      ← バジェット超過時の品質戦略     │
├──────────────────────────────────────────────────────────┤
│  IStreamingProvider (interface)                            │
│    ├── ILodProvider         ← Mesh LOD 制御              │
│    │     └── UnityLodGroupProvider (Unity LODGroup ラップ) │
│    └── IMipStreamingProvider ← Texture Mip 制御          │
│          └── UnityTextureStreamingProvider (Unity 標準)    │
├──────────────────────────────────────────────────────────┤
│  AddressableBackend         ← Addressables を呼ぶ唯一の実装 │
│  Addressables (Unity)       ← 実際のロード/アンロード      │
│  LODGroup (Unity)           ← Mesh LOD 切替              │
│  Texture Streaming (Unity)  ← Mip レベル制御             │
└──────────────────────────────────────────────────────────┘
```

---

## 5. Interface 設計

### 現行 AssetManagement API

```csharp
public interface IAssetManagement
{
    UniTask<IAssetHandle<T>> LoadAssetAsync<T>(AssetKey key, AssetOwner owner, CancellationToken ct = default)
        where T : UnityEngine.Object;
    IAssetHandle<T> LoadAppAssetSync<T>(AssetKey key) where T : UnityEngine.Object;
    UniTask<ISceneHandle> LoadSceneAsync(SceneAssetDescription desc, string variant, AssetOwner owner, SceneLoadOptions options = default, CancellationToken ct = default);
    UniTask UnloadSceneAsync(string sceneIdentity, CancellationToken ct = default);
    UniTask<GameObject> InstantiateAsync(AssetKey key, Transform? parent = null, bool worldSpace = false, CancellationToken ct = default);
    void Release(IAssetHandle handle);
    void ReleaseScene(string sceneIdentity);
    void ReleaseAll();
}
```

`AssetOwner.App` / `AssetOwner.Scene(sceneIdentity)` / `AssetOwner.Bind(go)` / `AssetOwner.Manual` で寿命を明示する。内部 backend は `IAssetBackend` で、`AsyncOperationHandle` / `SceneInstance` / `Addressables.` は公開 API に出さない。

### IResourceHandle / IResourceCache（不採用: 独立レイヤー案）

`LoadAsync` / `IResourceHandle` / `Observable<CacheEvent>` を持つ独立 `IResourceCache` レイヤー案は不採用。`AssetRegistry` と台帳が二重化するため、`AssetManagement` 内の `AssetResidentCache` に統合した。

### IAssetResidentCache（実装済み）

```csharp
/// <summary>refcount 0 のアセットを退避し、同一 key の再ロードで再利用する常駐キャッシュ。</summary>
internal interface IAssetResidentCache
{
    /// <summary>key がキャッシュにあれば取り出して返す（エントリはキャッシュから除去され、統計は復帰用に保持される）。</summary>
    bool TryTake(string key, out IBackendAsset asset);
    /// <summary>refcount 0 のアセットを退避する。バジェット超過分は effectiveFrequency 最小からエビクトされる。</summary>
    void Store(string key, AssetType type, IBackendAsset asset);
    /// <summary>全エントリをエビクトする（ReleaseAll 用）。</summary>
    void Clear();
    /// <summary>ヒット/ミス/エビクション数と type 別使用バイトのスナップショット。</summary>
    CacheStatsSnapshot GetSnapshot();
}
```

### CacheStatsSnapshot（実装済み）

```csharp
/// <summary>常駐キャッシュの統計スナップショット。</summary>
public readonly struct CacheStatsSnapshot
{
    public int HitCount { get; }
    public int MissCount { get; }
    public int EvictionCount { get; }
    public IReadOnlyDictionary<AssetType, long> ResidentBytes { get; }
}
```

### IQualityPolicy

```csharp
/// <summary>バジェット超過時の品質戦略を決定する。</summary>
public interface IQualityPolicy
{
    QualityLevel RecommendQuality(IResourceHandle handle, MemoryBudgetSnapshot budget);
}
```

### ILodProvider

```csharp
/// <summary>Mesh LOD 制御の抽象化。</summary>
public interface ILodProvider
{
    void SetLodBias(float bias);
    void ForceLodLevel(GameObject target, int level);
}
```

### IMipStreamingProvider

```csharp
/// <summary>Texture Mip ストリーミング制御の抽象化。</summary>
public interface IMipStreamingProvider
{
    void SetMipBias(float bias);
    void SetMemoryBudget(long bytes);
    long CurrentMipMemoryUsage { get; }
}
```

### IBudgetProvider（実装済み）

```csharp
/// <summary>AssetType 別のキャッシュバジェットを提供する。</summary>
public interface IBudgetProvider
{
    /// <summary>type のキャッシュバジェット（バイト）。未定義の type は 0 を返し、その type はキャッシュされない。</summary>
    long GetBudgetBytes(AssetType type);
}
```

---

## 6. AssetType / QualityLevel

```csharp
public enum AssetType
{
    Scene,
    Prefab,
    Texture,
    Audio,
    Other,
}

public enum QualityLevel
{
    /// <summary>最高品質（LOD0, Mip0）。</summary>
    Full = 0,
    /// <summary>品質低下（LOD1, Mip 制限）。</summary>
    Reduced = 1,
    /// <summary>最低品質（LOD2, 最低 Mip）。</summary>
    Minimum = 2,
    /// <summary>アンロード済み。</summary>
    Unloaded = 3,
}

public enum ResourceState
{
    Unloaded,
    Loading,
    Resident,
    Streaming,
}
```

---

## 7. AssetDescription 汎用化

```
AssetDescription (abstract, ScriptableObject)
├── AssetType           … enum
├── EstimatedMemoryBytes … long (概算、Editor バッチ算出)
│
├── SceneAssetDescription (既存を継承に変更)
│   ├── LoadType
│   └── ScenePayloads
│
├── PrefabAssetDescription (将来)
├── AudioAssetDescription (将来)
└── TextureAssetDescription (将来)
```

---

## 8. メモリバジェット

### MemoryBudgetConfig (ScriptableObject)

```csharp
[CreateAssetMenu(menuName = "OneStarMaker/MemoryBudgetConfig")]
public class MemoryBudgetConfig : ScriptableObject, IBudgetProvider
{
    [SerializeField] private long _totalBudgetMB = 256;
    [SerializeField] private long _sceneBudgetMB = 128;
    [SerializeField] private long _prefabBudgetMB = 64;
    [SerializeField] private long _textureBudgetMB = 48;
    [SerializeField] private long _audioBudgetMB = 16;
    ...
}
```

### AppConfig Override

```
Memory:Budget:Total = 512
Memory:Budget:Scene = 256
```

SO のデフォルトを AppConfig で上書き可能。QA テスト時にビルドなしで上限変更。

---

## 9. キャッシュ戦略

### LFU + 時間減衰

```
effectiveFrequency = accessCount × 0.5^(経過秒 / halfLifeSeconds)
```

- **halfLife** = 300s (5 分)。`MemoryBudgetConfig.HalfLifeSeconds` で設定可変。
- 新規ロード時 `accessCount = 1`
- `TryTake` でキャッシュヒットした際に `accessCount` を引き継ぎ、次の `Store` 時に累積される（使用中→解放のたびにリセットされない）
- エビクション判定時に `effectiveFrequency` が最小のエントリを解放
- **退避対象**は refcount 0 になったアセットのみ（使用中アセットはキャッシュに入らない）

### カテゴリ別プール

```
AssetCache
├── Pool: Scene  (budget: 128MB) ── LFU sorted entries
├── Pool: Prefab (budget: 64MB)
├── Pool: Texture (budget: 48MB)
└── Pool: Audio  (budget: 16MB)

Total budget: 256MB
```

各カテゴリが独立にバジェット管理。「Audio が Scene のバジェットを食い潰す」を防止。

---

## 10. バジェット超過フロー

```
バジェット監視 (毎秒 + ロード時)
    │
    ├─ 80% 超過 → CacheEvent.BudgetWarning テレメトリ発火
    │
    ├─ 90% 超過 → IQualityPolicy に問い合わせ
    │    └─ effectiveFrequency 最低のリソースを Reduced に降格
    │        └─ ILodProvider / IMipStreamingProvider で品質調整
    │
    └─ 100% 超過 → エビクション実行
         └─ effectiveFrequency 最低 + QualityLevel.Minimum のリソースをアンロード
             └─ CacheEvent.Evicted テレメトリ発火
```

※ STG 向け簡易実装では 90% の品質降格をスキップし、100% で即エビクション。

---

## 11. メモリテレメトリ

### ポーリング方式（実装済み / 配線は次パス）

常駐キャッシュは `GetSnapshot()` でカウンタと type 別常駐バイトを返す。テレメトリ層がこれを定期的にポーリングし、`ITelemetrySink` 等へ書き込む想定（配線は次パス）。

```csharp
// AssetResidentCache.GetSnapshot() が返す値
CacheStatsSnapshot {
    HitCount, MissCount, EvictionCount,
    ResidentBytes  // AssetType → 常駐バイト合計
}
```

**注:** 当初案の `Observable<CacheEvent>` (R3) 前提は破棄。本プロジェクトに R3 は存在しない。

### CacheEvent 種別（将来のテレメトリ配線用）

品質降格・バジェット警告等のイベント駆動テレメトリは将来パス。現行は上記スナップショットのポーリングのみ。

```csharp
public enum CacheEventType
{
    CacheHit,       // キャッシュから取得
    CacheMiss,      // 新規ロード
    Loaded,         // ロード完了
    Evicted,        // バジェット超過でアンロード
    BudgetWarning,  // 80% 閾値超過
    BudgetExceeded, // 100% 超過
}
```

---

## 12. STG 向け簡易実装

| 機能 | フル版 | STG 簡易版 |
|---|---|---|
| QualityLevel | Full / Reduced / Minimum / Unloaded | **Full / Unloaded のみ** |
| バジェット超過時の品質降格 | IQualityPolicy で段階的 | **スキップ（即エビクション）** |
| ILodProvider | 品質降格時に ForceLodLevel | **SetLodBias のみ（Unity LODGroup 任せ）** |
| IMipStreamingProvider | Mip バイアス動的制御 | **SetMemoryBudget のみ（Unity Texture Streaming 任せ）** |
| AssetDescription サブクラス | Scene / Prefab / Audio / Texture | **Scene のみ** |

---

## 13. 施行 (T9-T15)

### 依存グラフ

```
T9 (AssetDescription 汎用化 + Interface 定義)
├── T10 (IBudgetProvider + MemoryBudgetConfig)
├── T11 (IStreamingProvider + Unity 標準ラッパー)
├── T12 (IResourceCache + LFU 実装)
│    └── T13 (SceneDirector 統合)
├── T14 (メモリテレメトリ) ← T6 にも依存
└── T15 (Editor: 概算バッチツール)
```

### 実装順

| Phase | 内容 | Assembly | 新規/変更 |
|---|---|---|---|
| T9 | `AssetDescription` 基底、`AssetType`, `QualityLevel`, `ResourceState`, 全 interface | Runtime | 新規 8 + 変更 1 |
| T10 | `MemoryBudgetConfig` (SO + IBudgetProvider) + AppConfig Override | Runtime | 新規 1 |
| T11 | `UnityLodGroupProvider`, `UnityTextureStreamingProvider` | Runtime | 新規 2 |
| T12 | `ResourceCache` (IResourceCache 実装) + `ResourceHandle` | Runtime | 新規 2 |
| T13 | SceneDirector に IResourceCache 注入・統合 | Runtime | 変更 2 |
| T14 | CacheEvent → ITelemetrySink 接続 | Runtime | 新規 1 |
| T15 | Editor: AssetMemoryEstimator バッチツール | Editor | 新規 1 |

---

## 14. トレードオフ記録

| 決定 | 選択肢 | 採用 | 理由 |
|---|---|---|---|
| LOD 抽象化 | 直接呼び / ILodProvider / 統合 interface | ILodProvider (分離) | LOD と Mip は制御対象が異なる。MeshShader 差替え時に ILodProvider だけ交換可能 |
| QualityLevel 段階 | 2 / 4 / 連続値 | 4 段階 (STG は 2 のみ使用) | LODGroup の LOD0-2 + Unload に自然にマッピング |
| バジェット監視頻度 | ロード時のみ / 毎フレーム / 毎秒+ロード時 | 毎秒 + ロード時 | 毎フレームはコスト高、ロード時のみは遅い |
| Cache と SceneDirector | 内包 / 独立サービス / Cache が包含 | 独立サービス | Scene 以外のアセットもキャッシュ可能。DI で差替え容易 |
| テレメトリ結合 | Observable(R3) / GetSnapshot ポーリング / delegate | GetSnapshot ポーリング | 本プロジェクトに R3 が無いため Observable 案は破棄。キャッシュはテレメトリに非依存のまま |
| バジェット定義場所 | AppConfig のみ / SO のみ / 両方 | SO + AppConfig Override | SO はエディタ調整可能、AppConfig で QA 時にビルドなし変更 |
| 概算計算 | Import 時自動 / バッチ / ビルド前バリデーション | バッチ + ビルド前バリデーション | Import 頻度が高すぎる。バッチ + バリデーションで忘れ防止 |
| STG 実装範囲 | Interface のみ / on/off のみ / LFU (2 段階) | LFU (Full/Unloaded) | LFU エビクションは STG でも有用。品質降格は将来有効化 |
| キャッシュ統合方式 | 独立 IResourceCache レイヤー / AssetManagement 内常駐キャッシュ | 常駐キャッシュ方式 | 独立レイヤーは `AssetRegistry` と台帳二重化のため不採用 |
| バジェット計上範囲 | 使用中+キャッシュ / キャッシュ内のみ | キャッシュ内 (refcount 0) のみ | 使用中メモリの上限はスコープ設計の責務。バジェットは投機的保持分の上限 |
| LFU 時間減衰 | 純 LRU / LFU+減衰 / 固定 TTL | LFU + 時間減衰 | LRU に漸近するが、共通アセット保護のため accessCount 引き継ぎ付き LFU を維持 |

---

## 15. 将来拡張

| # | 内容 | トリガー |
|---|---|---|
| F1 | `MeshShaderLodProvider` — 独自 MeshShader LOD | オープンワールド着手時 |
| F2 | `VirtualTextureProvider` — Virtual Texture Mip 制御 | 大規模テレイン着手時 |
| F3 | QualityLevel 4 段階フル有効化 | F1/F2 完了後 |
| F4 | Adaptive Budget — デバイス RAM に応じた動的バジェット | 多機種対応開始時 |
| F5 | 実メモリ vs 概算の乖離テレメトリ | テレメトリ運用開始後 |
| F6 | PrefabAssetDescription / AudioAssetDescription | 各アセット種別のキャッシュ着手時 |
| F7 | フレーム分散エビクション — 大量エビクション時のスパイク抑制 | 大規模シーン遷移で GC スパイクが問題化した時 |
| F8 | CacheEvent テレメトリ配線 — `GetSnapshot()` ポーリングからイベント駆動へ | テレメトリ運用開始時 |
| F9 | AppConfig によるバジェット上書き | QA テストでビルドなし上限変更が必要になった時 |
| F10 | 品質降格 (IQualityPolicy, ILodProvider, IMipStreamingProvider) | F1/F2 完了後 |

---

## 16. 受け入れた前提と制約

1. **総メモリ上限は保証しない。** 使用中アセットの上限はスコープ設計の責務。バジェットは「投機的に持つ追加メモリ」の上限。
2. **エビクション = 即メモリ解放ではない。** Addressables はバンドル単位のため、実効性はバンドル分割粒度に依存する近似ノブ。
3. **概算の歪みは AssetType ごとに異なる。** `Profiler.GetRuntimeMemorySizeLong` は Prefab の依存 Texture/Mesh を含まない過小値。バジェット値は実測で調整する仮値。
4. **`AssetOwner.App` プリロードとの棲み分け:** 確実に必要なものは App スコープで明示固定（保証あり）、キャッシュは自動・保証なし。キャッシュはプリロードの代替ではない。
