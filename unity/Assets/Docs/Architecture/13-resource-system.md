# 13. リソースシステム + メモリバジェット設計

> ステータス: 設計完了・実装待ち (2026-03-07)
> 優先度: テレメトリ (T1-T8) の後に着手

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

---

## 1. 目的

- プロジェクト毎に **メモリバジェット** を定義し、カテゴリ別に予算管理する
- **AssetDescription** にアセット種別と概算メモリを記録する
- バジェット内で **LFU + 時間減衰キャッシュ** を運用し、頻繁に使うものを保持・使わないものを解放する
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
| キャッシュ配置 | SceneDirector とは独立サービス。DI で注入 |
| テレメトリ結合 | `Observable<CacheEvent>` で疎結合。キャッシュ自体はテレメトリに非依存 |

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
│  IResourceCache ← バジェット制御・エビクション判断          │
│    ├── IResourceHandle      ← 1 リソースの抽象表現        │
│    ├── IQualityPolicy       ← バジェット超過時の品質戦略   │
│    └── IBudgetProvider      ← カテゴリ別バジェット定義     │
├──────────────────────────────────────────────────────────┤
│  IStreamingProvider (interface)                            │
│    ├── ILodProvider         ← Mesh LOD 制御              │
│    │     └── UnityLodGroupProvider (Unity LODGroup ラップ) │
│    └── IMipStreamingProvider ← Texture Mip 制御          │
│          └── UnityTextureStreamingProvider (Unity 標準)    │
├──────────────────────────────────────────────────────────┤
│  Addressables (Unity)       ← 実際のロード/アンロード      │
│  LODGroup (Unity)           ← Mesh LOD 切替              │
│  Texture Streaming (Unity)  ← Mip レベル制御             │
└──────────────────────────────────────────────────────────┘
```

---

## 5. Interface 設計

### IResourceHandle

```csharp
/// <summary>1 つのキャッシュエントリを表す。</summary>
public interface IResourceHandle : IDisposable
{
    string Key { get; }
    AssetType AssetType { get; }
    ResourceState State { get; }          // Unloaded / Loading / Resident / Streaming
    QualityLevel CurrentQuality { get; }  // Full / Reduced / Minimum / Unloaded
    long EstimatedMemoryBytes { get; }    // 概算（参考値）
    float EffectiveFrequency { get; }     // LFU + 時間減衰後の頻度
    void Touch();                         // アクセス記録
}
```

### IResourceCache

```csharp
/// <summary>バジェット制御付きリソースキャッシュ。</summary>
public interface IResourceCache
{
    UniTask<IResourceHandle> LoadAsync(AssetDescription desc, CancellationToken ct);
    void Release(string key);
    Observable<CacheEvent> OnCacheEvent { get; }
    MemoryBudgetSnapshot GetBudgetSnapshot();
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

### IBudgetProvider

```csharp
/// <summary>カテゴリ別メモリバジェットを提供する。</summary>
public interface IBudgetProvider
{
    long GetBudget(AssetType category);
    long TotalBudget { get; }
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
effectiveFrequency = accessCount × decay^(elapsedSeconds / halfLifeSeconds)
```

- **halfLife** = 300s (5 分)。設定可変。
- 新規ロード時 `accessCount = 1`
- アクセス毎に `accessCount++`
- エビクション判定時に `effectiveFrequency` が最小のエントリを解放

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

### CacheEvent 種別

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

`CacheEvent` は `Observable<CacheEvent>` で発火され、テレメトリ層が購読して `ITelemetrySink` に書き込む。

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
| テレメトリ結合 | 直接呼び / Observable / delegate | Observable | R3 Subject で疎結合。キャッシュはテレメトリに非依存 |
| バジェット定義場所 | AppConfig のみ / SO のみ / 両方 | SO + AppConfig Override | SO はエディタ調整可能、AppConfig で QA 時にビルドなし変更 |
| 概算計算 | Import 時自動 / バッチ / ビルド前バリデーション | バッチ + ビルド前バリデーション | Import 頻度が高すぎる。バッチ + バリデーションで忘れ防止 |
| STG 実装範囲 | Interface のみ / on/off のみ / LFU (2 段階) | LFU (Full/Unloaded) | LFU エビクションは STG でも有用。品質降格は将来有効化 |

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
