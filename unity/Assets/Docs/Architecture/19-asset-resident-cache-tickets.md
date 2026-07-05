# 19. AssetResidentCache 施行表

> ステータス: 施行待ち (2026-07-05)
> 前提資料: [13. リソースシステム](13-resource-system.md) / [18. AssetDescription](18-asset-description.md)
> 本書単体で施行できるよう、確定済みの設計判断と実装時の注意をすべて記載する。

---

## 0. 確定済みの設計判断（施行者は再検討しないこと）

| 判断 | 内容 |
|---|---|
| 統合方式 | **常駐キャッシュ方式**。refcount 0 になったアセットを即 `_backend.Release` せず `AssetResidentCache` に退避し、同一 canonical key の再ロードでヒットさせる。doc 13 の「独立 `IResourceCache` レイヤー」は不採用（`AssetRegistry` と台帳が二重化するため） |
| バジェット計上範囲 | **キャッシュ内 (refcount 0) のみ**。使用中アセットは計上しない。総メモリ上限の保証はしない（責務はスコープ設計側） |
| エビクションポリシー | LFU + 時間減衰。`effectiveFrequency = accessCount × 0.5^(経過秒 / halfLifeSeconds)`。halfLife 既定 300s |
| キャッシュ対象 | `LoadAssetAsync` / `LoadAppAssetSync` のアセットのみ。シーンと `InstantiateAsync` のインスタンスは対象外 |
| バジェット未定義の AssetType | キャッシュせず即解放（現行挙動と同じ。明示オプトイン） |
| 公開 API | `IAssetManagement` は変更しない。既存呼び出し元（`AbstractApplicationInitializer` 等）は無変更で従来挙動 |
| テレメトリ | 配線は次パス。ただし統計カウンタ（`GetSnapshot`）は本パスに含める。R3 はプロジェクトに存在しないため `Observable<CacheEvent>` 前提は破棄 |
| スコープ外 | AppConfig によるバジェット上書き / 品質降格 (IQualityPolicy, ILodProvider, IMipStreamingProvider) / フレーム分散エビクション |

## 0.1 共通ルール（全チケット適用）

- namespace は `OneStarMaker.Runtime.AssetManagement.Cache`（既存スタブの `OneStarMaker.Runtime` は誤り。修正すること）。
- 全ファイル `#nullable enable`。XML doc コメントは既存コード同様に日本語。ブロック namespace + 4 スペースインデント（既存スタイル踏襲）。
- `IBackendAsset` は internal のため、それを扱う型（`IAssetResidentCache`, `AssetResidentCache`）も internal にする。テストは `InternalsVisibleTo("OneStarMaker.Tests")`（`Runtime/AssemblyInfo.cs` 定義済み）でアクセスできる。
- `.cs` を削除する場合は対応する `.meta` も削除。新規ファイルは Unity Editor を一度開いて `.meta` を生成させる。
- 各チケットの完了条件: コンパイルエラーなし + 該当テストが Unity Test Runner (EditMode) でグリーン。

---

## 1. チケット一覧

| ID | 内容 | 依存 | 規模 |
|---|---|---|---|
| RC-1 | `IBudgetProvider` interface 化 + `MemoryBudgetConfig` 書き直し + スタブ削除 | なし | S |
| RC-2 | `AssetRegistry` 拡張（AssetType / IsInstance 保持、解放系の戻り値変更） | なし | S |
| RC-3 | `AssetResidentCache` 本体 + `CacheStatsSnapshot` + 単体テスト | RC-1 | M |
| RC-4 | `AssetManagement` への配線 + 統合テスト | RC-2, RC-3 | M |
| RC-5 | 全テスト実行 + ドキュメント更新 (doc 13 / 18) | RC-4 | S |

依存グラフ: RC-1 と RC-2 は並行可能。RC-3 → RC-4 → RC-5 は直列。

---

## 2. RC-1: IBudgetProvider + MemoryBudgetConfig

### 対象ファイル

- 書き直し: `Runtime/AssetManagement/Cache/IBudgetProvider.cs`
- 書き直し: `Runtime/AssetManagement/Cache/MemoryBudgetConfig.cs`
- 削除: `Runtime/AssetManagement/Cache/BudgetProvider.cs` (+ `.meta`) — 空スタブ。`MemoryBudgetConfig` 自身が `IBudgetProvider` を実装するため不要

### 実装内容

```csharp
public interface IBudgetProvider
{
    /// <summary>type のキャッシュバジェット（バイト）。未定義の type は 0 を返し、その type はキャッシュされない。</summary>
    long GetBudgetBytes(AssetType type);
}
```

`MemoryBudgetConfig`（ScriptableObject, `IBudgetProvider` 実装）:

- `[Serializable] private struct AssetTypeBudgetEntry { public AssetType Type; public int BudgetMB; }` のリスト `_budgets` に変更。
- `HalfLifeSeconds` プロパティは現行のまま維持（0 以下なら 300f にフォールバック）。
- `GetBudgetBytes`: エントリを線形検索し `(long)BudgetMB * 1024 * 1024` を返す。見つからなければ 0。

### 実装時の注意

- **現行コードの不具合を再現しないこと**: 現行 `_assetTypeBudges`（typo）は `int[]` で enum 並び順に暗黙依存、`Sum() * 1024 * 1024` は int 演算で 2GB 超過時オーバーフロー、null 配列で NRE。書き直しで全て解消する。
- long への昇格は乗算の**前**に行う（`(long)mb * 1024 * 1024`）。
- `_budgets` が null / 空でも例外を出さない（全 type バジェット 0 = キャッシュ無効として動く）。
- 同一 `AssetType` の重複エントリは**先勝ち**とし、挙動をテストで固定する。
- serialize 済みフィールドのリネームになるが、`MemoryBudgetConfig` はまだ未配線で `.asset` インスタンスが存在しないはずなので `FormerlySerializedAs` は不要。念のため `*.asset` 内の `MemoryBudgetConfig` 参照を検索し、存在した場合のみ移行を検討。
- `BudgetMB` の Tooltip に「概算は参考値（特に Prefab は依存 Texture/Mesh を含まない）。実測して調整する前提の仮値」と明記する。
- 現行の未使用 using（`System.ComponentModel` 等）を削除。

### 完了条件

- `IBudgetProvider` が public interface、`MemoryBudgetConfig : ScriptableObject, IBudgetProvider`。
- `BudgetProvider.cs` と `.meta` が削除されている。
- テスト（`OneStarMaker.Tests`）: 未定義 type → 0 / 定義済み type → MB×1024×1024 / 重複エントリ先勝ち / 2048MB 以上でオーバーフローしない。

---

## 3. RC-2: AssetRegistry 拡張

### 対象ファイル

- 変更: `Runtime/AssetManagement/Internal/AssetRegistry.cs`
- 追随: `Runtime/AssetManagement/AssetManagement.cs`（コンパイル通しの最小修正のみ。キャッシュ配線は RC-4）
- 追随: 既存テスト `Tests/AssetManagement/AssetManagementTests.cs` のコンパイル修正

### 実装内容

- `LoadedAsset` に `AssetType Type` と `bool IsInstance` を追加し、`AddAsset` / `Acquire` の引数で受け取る。
- 解放系メソッドの戻り値を `IBackendAsset` から `LoadedAsset` に変更する: `Release(string, out LoadedAsset?)` / `ReleaseSceneOwned` / `ReleaseGameObjectOwned` / `ReleaseAllAssets`。呼び出し側が「キャッシュ退避 or 即解放」を key/type/backend を見て判断できるようにするため。

### 実装時の注意

- **`:instance:` の判定を文字列パースにしないこと。** `InstantiateAsync` が registry へ Acquire する時点で `IsInstance = true` を明示的に渡す。key 文字列の規約に依存させない。
- `AssetType` は呼び出し側が `AssetKey.Type` から渡す。`LoadAppAssetSync` / `LoadAssetAsync` は `key.Type`、`InstantiateAsync` は `AssetType.Prefab` + `IsInstance = true`。
- 既存の挙動（refcount、owner 追跡、App スコープは追跡しない等）は一切変えない。純粋にメタ情報の追加と戻り値の型変更のみ。
- この時点の `AssetManagement` は戻り値型変更への追随として `loaded.Backend` を `_backend.Release` に渡すだけにする（挙動不変）。

### 完了条件

- 既存の `AssetManagementTests` が全て従来どおりグリーン（挙動不変の確認）。

---

## 4. RC-3: AssetResidentCache 本体

### 対象ファイル

- 書き直し: `Runtime/AssetManagement/Cache/IAssetResidentCache.cs`
- 書き直し: `Runtime/AssetManagement/Cache/AssetResidentCache.cs`
- 新規: `Runtime/AssetManagement/Cache/CacheStatsSnapshot.cs`
- 新規: `Tests/AssetManagement/AssetResidentCacheTests.cs`

### 実装内容

```csharp
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

`AssetResidentCache` コンストラクタ注入（すべてテスト差し替え用）:

- `IBudgetProvider budgetProvider`
- `float halfLifeSeconds`
- `Action<IBackendAsset> releaseAsset` — 本解放の実行者（本番は `_backend.Release`）
- `Func<UnityEngine.Object?, long> estimateBytes` — 既定は `Profiler.GetRuntimeMemorySizeLong`（null は 0）
- `Func<double> clock` — 既定は `() => Time.realtimeSinceStartupAsDouble`

エントリ: `key / type / backend / estimatedBytes / accessCount / lastAccessTime`。type 別使用バイト合計を別途集計して保持する（毎回全走査しない）。

`CacheStatsSnapshot`（readonly struct, public）: `HitCount / MissCount / EvictionCount / IReadOnlyDictionary<AssetType, long> ResidentBytes`。

### 実装時の注意

- **Store のフロー**: (1) `asset.IsValid == false` なら即 `releaseAsset` して終了（無効ハンドルをキャッシュしない）。(2) `GetBudgetBytes(type) <= 0` なら即 `releaseAsset`（この解放は EvictionCount に数えない）。(3) `estimateBytes` を **Store 時に 1 回だけ**計測して保持（エビクション判定のたびに再計測しない）。(4) 追加後、該当 type の合計がバジェットを超えている間、effectiveFrequency 最小のエントリを `releaseAsset` + EvictionCount++。**直前に Store したエントリ自身も候補に含める**（単体でバジェット超のアセットは即解放される。これは正しい挙動）。
- **accessCount の引き継ぎ**: `TryTake` でエントリを取り出す際、`(accessCount + 1, lastAccessTime = now)` を「復帰待ちテーブル」（`Dictionary<string, (int count, double last)>`）に移す。次に同じ key が `Store` された時に引き継ぐ。これが無いと LFU が意味を失う（使用中→解放のたびに count がリセットされ、実質 LRU に退化する）。テーブルは key 上書きなのでロード履歴の distinct key 数までしか増えない。`Clear` で一緒に消す。
- **時間減衰の計算**: `Math.Exp(-Ln2 * (now - lastAccess) / halfLife)` を **double** で計算。エビクション判定時のみ算出し、定期更新はしない。`accessCount` の上限ケア（int で十分だが飽和加算にしておく）。
- **タイブレーク**: effectiveFrequency が同値（全 count=1 直後など）の場合は lastAccessTime が古い方を落とす、と定義してテストで固定する。
- **同一 key の二重 Store**: registry が dedup するため通常発生しないが、防御として既存エントリがあれば古い方を `releaseAsset` してから上書きする。
- **エビクションの探索**: エントリ数は高々数十〜数百想定なので線形探索で良い。優先度キュー等の early optimization をしない（decay で順序が時間変化するため固定順ヒープは使えない）。
- `Time.realtimeSinceStartupAsDouble` はメインスレッド専用。既定 clock の評価はコンストラクタではなく呼び出し時に行う（ラムダで包む）。
- `GetSnapshot` の `ResidentBytes` は内部辞書のコピーを返す（内部状態を晒さない）。

### 単体テスト（fake clock / fake estimator / release 記録デリゲートで決定的に）

1. Store → TryTake でヒットし、同じ backend が返る。HitCount=1。
2. TryTake ミスで MissCount が増える。
3. バジェット超過時に effectiveFrequency 最小のエントリが release される。
4. 時間減衰: accessCount の多い古いエントリより、直近アクセスの新しいエントリが残る（clock を進めて検証）。
5. accessCount 引き継ぎ: Store → TryTake → Store 後も頻度が累積し、count=1 の新参より優先して残る。
6. AssetType 分離: Texture の超過で Prefab のエントリが release されない。
7. バジェット 0 の type は Store 即 release（EvictionCount は増えない）。
8. 単体でバジェット超のアセットは Store 直後に release される。
9. `IsValid == false` の Store は即 release されキャッシュされない。
10. Clear で全エントリ release + ResidentBytes が全て 0。

---

## 5. RC-4: AssetManagement 配線

### 対象ファイル

- 変更: `Runtime/AssetManagement/AssetManagement.cs`
- 新規: `Tests/AssetManagement/AssetManagementCacheTests.cs`

### 実装内容

- ctor 追加: `public AssetManagement(MemoryBudgetConfig? budgetConfig = null)`。null ならキャッシュ無効（従来挙動、既存呼び出し元は無変更）。internal ctor `(IAssetBackend backend, IAssetResidentCache? cache)` をテスト用に追加。
- ロード経路（`LoadAssetAsync` / `LoadAppAssetSync`）: registry ミス時、in-flight 確認の**前**に `_cache.TryTake(key.Canonical, out var asset)` を挟む。ヒットしたら backend ロードをスキップして registry へ Acquire。
- 解放経路（`ReleaseKey` / `ReleaseSceneOwned` ループ / `ReleaseGameObjectOwned` ループ）: 戻ってきた `LoadedAsset` が `IsInstance == false` なら `_cache.Store(key, type, backend)`、それ以外は従来どおり `_backend.Release`。
- `ReleaseAllAssetsNow`: registry の全アセットを従来どおり即 `_backend.Release` し（**Store しない**。全破棄の意図なので）、あわせて `_cache.Clear()` を呼ぶ。

### 実装時の注意

- キャッシュ無効時（null 設定）に**分岐が増えないこと**。null チェック分岐を散らすより、何もしない `NullResidentCache`（TryTake は常に false、Store は即 release 相当= backend.Release を呼ぶ実装 or AssetManagement 側で null 時は従来コードパス）のどちらかに統一する。推奨は「`_cache` が null なら従来コードパス」の単純分岐を各解放箇所に閉じ込めるヘルパー `ReleaseOrStore(LoadedAsset)` を 1 つ作ること。
- **本番の budget 配線は `AssetManagement(MemoryBudgetConfig)` ctor 内**で行う: `halfLifeSeconds = config.HalfLifeSeconds`、`releaseAsset = _backend.Release`。`AbstractApplicationInitializer` の変更はスコープ外（budgetConfig を渡す配線は次パス）。
- シーンは `_assets` 台帳に入らない（`_scenes` 別管理）ため、キャッシュ経路に乗らないことを前提にできる。ただし `ReleaseScene` の非同期経路（`UnloadThenReleaseSceneAsync`）でもシーン**所有アセット**は `ReleaseOrStore` を通すこと。
- in-flight dedup との順序: registry → cache → in-flight → backend。cache ヒット時に in-flight テーブルを触らない。
- `AttachDestroyReleaseIfNeeded` など既存の owner 処理はキャッシュヒット経路でも従来どおり呼ぶ。

### 統合テスト（`FakeAssetBackend` + fake cache または実 cache + fake estimator/clock）

1. load → release → load で `LoadAssetCallCount == 1` かつ `ReleaseCallCount == 0`（キャッシュヒットで backend 再ロードなし）。
2. budgetConfig なし（null）では従来どおり release 時に即 `ReleaseCallCount` が増える（既存テストの挙動維持）。
3. `InstantiateAsync` のインスタンスは GO 破棄時にキャッシュされず即 release。
4. シーン所有アセットがシーン解放時に Store され、次シーンの同一アセットロードでヒットする（シーン遷移の再利用シナリオ）。
5. `ReleaseAll` でキャッシュ内も含め全て backend.Release される。
6. バジェット超過エントリの evict が `FakeAssetBackend.ReleasedAssets` に記録される。

---

## 6. RC-5: 全テスト + ドキュメント更新

### 対象ファイル

- 変更: `unity/Assets/Docs/Architecture/13-resource-system.md`
- 変更: `unity/Assets/Docs/Architecture/18-asset-description.md`（末尾の関連ファイル節の 1 行のみ）

### 実装内容

1. Unity Test Runner (EditMode) で `OneStarMaker.Tests` 全件グリーンを確認。
2. doc 13 を更新:
   - ステータス行: 常駐キャッシュ実装済みに更新。
   - §4 レイヤー構成 / §5 Interface: `IResourceCache` レイヤー案を廃し、実装した `IAssetResidentCache` / `IBudgetProvider` / `CacheStatsSnapshot` の実シグネチャに差し替え。
   - §11 テレメトリ: `Observable<CacheEvent>`(R3) 前提を削除し、「`GetSnapshot()` をテレメトリ層がポーリングする」方式に書き換え（配線は次パス）。
   - §14 トレードオフ記録に追記: 常駐キャッシュ方式 vs 独立レイヤー / バジェット計上 = キャッシュ内のみ / LFU+減衰は LRU に漸近するが共通アセット保護のため維持。
   - 新節「受け入れた前提と制約」を追加（下記をそのまま記載）:
     - 総メモリ上限は保証しない。使用中アセットの上限はスコープ設計の責務。バジェットは「投機的に持つ追加メモリ」の上限。
     - エビクション = 即メモリ解放ではない。Addressables はバンドル単位のため、実効性はバンドル分割粒度に依存する近似ノブ。
     - 概算の歪みは AssetType ごとに異なる（Prefab は依存を含まない過小値）。バジェット値は実測で調整する仮値。
     - `AssetOwner.App` プリロードとの棲み分け: 確実に必要なものは App スコープで明示固定（保証あり）、キャッシュは自動・保証なし。キャッシュはプリロードの代替ではない。
   - §15 将来拡張に追記: フレーム分散エビクション / テレメトリ配線 / AppConfig 上書き。
3. doc 18 末尾の「既存資料: 13...」行を「常駐キャッシュ実装済み」に更新。

### 完了条件

- 全テストグリーンのエビデンス（Test Runner 結果）。
- doc 13 に上記 4 項目の「受け入れた前提と制約」が明記されている。

---

## 7. 施行後の確認チェックリスト

- [ ] `IAssetManagement` の公開シグネチャに変更がない（git diff で確認）
- [ ] budgetConfig を渡さない既存経路の挙動が完全に従来どおり（既存テスト無修正でグリーン、コンパイル追随除く）
- [ ] `Cache/` 配下の namespace が全て `OneStarMaker.Runtime.AssetManagement.Cache`
- [ ] `BudgetProvider.cs` / `.meta` が削除済み
- [ ] 新規ファイルの `.meta` が生成済み（Unity を一度開く）
- [ ] `:instance:` の文字列パースがコード中に存在しない
