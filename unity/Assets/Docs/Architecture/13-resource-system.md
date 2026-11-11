# 13. リソースシステム + メモリバジェット設計

> ステータス: AssetResidentCache（常駐キャッシュ）実装済み。テレメトリ配線 / 品質降格 / Editor 概算ツールは次パス

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
| バジェット計上 | **キャッシュ内 (refcount 0) のみ**。使用中アセットは計上しない。**総メモリ上限は保証しない**（責務はスコープ設計側） |
| キャッシュ対象 | `LoadAssetAsync` / `LoadAppAssetSync` のアセットのみ。**シーンと `InstantiateAsync` のインスタンスは対象外** |
| バジェット未定義の AssetType | キャッシュせず即解放（明示オプトイン方式。バジェットを定義しない限り従来挙動） |
| 公開 API | `IAssetManagement` に Content Directory 用の型付き Object / Scene / Prefab 入口を追加。既存 Addressables API の署名と既定挙動は維持 |
| テレメトリ結合 | `AssetResidentCache.GetSnapshot()` をテレメトリ層がポーリング（配線は次パス）。AssetManagement にリアクティブ依存（R3）を持ち込まない判断 |
| 配信 cache | transport の取得・検証・disk budget・known-good pin・物理削除は DIST が所有。`AssetResidentCache` のメモリ budget と混同しない |
| 要求時解決 | `Runtime/ContentResolution/` の任意の層が、要求に対する候補の探索と由来の記録を持つ。`IAssetManagement` に member を足さず、所有と解放は既存の API のまま。この層を使わない AssetManagement と BuildSystem の単独利用は変わらない |

### Unity 6.6 Content Directories の実証済み境界

Unity `6000.6.0f1` の direct Content Directories API は、隔離した最小 fixture と
Standalone Windows Player（Mono、High stripping）で、root・asset・additive Scene の
build、登録、load、unload、release、unregister、欠損 directory からの再試行、別 path
への移設が成立した。Content build report の `ScriptsOnlyCache.yaml` は
`BuildPlayerOptions.previousBuildReportDirectories` の入力として受理された。

これは BuildSystem の backend 候補を認めた限定実証であり、現行
`IAssetManagement` / `AddressableBackend` を置き換えた事実ではない。後続の BS4 では、
本番 SceneResource graph から選択した content と Windows x64 IL2CPP / High stripping Player を
同じ build identity で対応付け、directory 登録、論理初回 Scene、代表 Prefab、明示 close までを
実 Player で確認した。HTTP 配信、処理中 native load の取消、全 incremental matrix は未実証である。
要求取消は native abort と同一視せず、発行済み処理の終端を単一 owner
が受け取ってから資源を解放する設計を後続 runtime backend の条件とする。

BS2b の Editor build 経路は、成功した選択 plan と materialization snapshot から
Scene・Prefab・Texture の Content Directory を生成する。成果物内の単一
`BuildContentRoot` が logical key・表現・Unity loadable ID の対応を保持する。
BS3 では、この root を Runtime で検証・索引化し、移設先 directory から型付きで
load・解放・unregister する経路を追加した。通常 Play は directory。Addressables backend は明示互換口として残る。

directory session が Unity 登録 handle、発行済み native 処理、root、unregister を所有する。
`AssetManagement` は `AssetOwner` 台帳と resident cache の唯一の owner であり、
directory 由来の token が session 利用権を保持する。caller の取消は native abort ではない。
発行済み処理が終端してから、不要になった成功結果と依存資源を回収する。明示 close は
新規受付を止めて処理を drain し、live owner/Prefab instance が残れば `ResourcesInUse`
として登録を保ち、解放後の再試行を許す。cache entry も解放前は revision の利用中とみなす。

`ContentRevisionGate` は identity+target と正規化 absolute path の双方について、
同一 process の token と同一 Windows user の process lease を束ねる。登録側は shared read lease を
session の reservation から drain / unregister 完了まで保持し、削除側は exclusive lease を
物理削除の成功または失敗が確定するまで保持する。待ち合わせはせず、競合は busy、
lock I/O failure は fail closed とする。別 Windows user、非 NTFS cache、外部 tool による変更は保証範囲外である。
検証済み snapshot は所有 token ではないため、session は reservation 取得後、native 登録前に
receipt、manifest、全 files を再検証する。`AssetManagement` は従来どおり owner 台帳と
resident cache を所有し、DIST の利用台帳を別に作らない。
通常の Play 停止は Runtime `ReleaseAll` のあと、同じスタックで同期の play-stop 完了が終わる。
directory Scene は `ReleaseSceneTokenAfterPlayStop` で token だけ返し、`ReleaseSceneAfterUnityShutdown`
は使わない。native `UnloadSceneAsync` も Addressables `UnloadSceneAsync` も呼ばない。
完了は受付停止、cache 退避、unregister、OS read lease 解放であり、`StopAndDrain` も pending native
も待たない。`UniTask.GetResult` で待たない。明示 close は新規受付を止めて `StopAndDrain` し、
残 Scene を unload する。live owner/Prefab instance が残れば `ResourcesInUse` として登録を保ち、
解放後の再試行を許す。cache entry も解放前は revision の利用中とみなす。Play 停止と明示 close を
同じ「完全 drain」として書かない。

### DIST transport と disk cache の境界

OSM transport manifest v1 は UTF-8 JSON で、product、contentSet、revision、
固定 target `StandaloneWindows64-Player`、Unity version、root schema、player config schema、
content files と案内用 sourceFiles を持つ。request は受信した manifest bytes の SHA-256 を pin し、
consumer は再 serialization した JSON を同一性の根拠にしない。Unity 内部 manifest と
Content BuildReport は opaque な build 成果物であり、外部 transport protocol ではない。

install は caller が明示した local NTFS cache の `staging` で全 file の size/hash と集合を検証し、
同一 volume の rename で immutable な `installed/<contentSet>/<revision>` を公開する。partial、取消、
timeout、404、切断、hash mismatch は active install にしない。同じ revision の異なる manifest は
上書きしない。HTTP と local directory は同じ validation/install 経路を使い、Runtime は network から起動しない。

disk cache の transaction は install、inspect、known-good pin、eviction、cleanup を同じ root 内で
直列化する。budget は installed、staging、tombstone の実 file bytes と新規 request の予約を数える。
known-good、今回 request、利用 lease を持つ revision を強制削除しない。known-good は register/load 成功後に
caller が明示的に昇格し、install 成功だけでは昇格しない。

物理削除は root 包含、receipt、marker を検証し、exclusive delete lease を取得してから tombstone へ
rename する。tombstone は再登録できず、物理削除失敗時は次回 cleanup の対象になる。失敗時も lease を
解放する。破損 receipt や pin は削除候補として黙って無視せず、削除を停止する構造化失敗として扱う。

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
    UniTask<ISceneHandle> LoadSceneAsync(
        string sceneIdentity,
        SceneAssetDescription desc,
        string variant = "",
        SceneLoadOptions options = default,
        CancellationToken ct = default);
    UniTask UnloadSceneAsync(string sceneIdentity, CancellationToken ct = default);
    UniTask<GameObject> InstantiateAsync(AssetKey key, Transform? parent = null, bool worldSpace = false, CancellationToken ct = default);
    UniTask<IAssetHandle<T>> LoadContentAssetAsync<T>(string logicalKey, string representation, AssetOwner owner, CancellationToken ct = default)
        where T : UnityEngine.Object;
    UniTask<ISceneHandle> LoadContentSceneAsync(string sceneIdentity, string representation, SceneLoadOptions options = default, CancellationToken ct = default);
    UniTask<GameObject> InstantiateContentAsync(string logicalKey, string representation, Transform? parent = null, bool worldSpace = false, CancellationToken ct = default);
    void Release(IAssetHandle handle);
    void ReleaseScene(string sceneIdentity);
    void ReleaseAll();
}
```

`AssetOwner.App` / `AssetOwner.Scene(sceneIdentity)` / `AssetOwner.Bind(go)` / `AssetOwner.Manual` で寿命を明示する。内部 backend は `IAssetBackend` で、`AsyncOperationHandle` / `SceneInstance` / `Addressables.` は公開 API に出さない。

### Scene 解放の役割分担

| API | 用途 | Scene backend Unload | 同期性 |
|---|---|---|---|
| `UnloadSceneAsync` | 通常 gameplay（SceneDirector Phase 2） | する | await 可能 |
| `ReleaseScene` | 所有アセット解放（Phase 3）。未 Unload Scene 本体が残っていると例外 | しない | 同期 |
| `ReleaseAll` | quitting / SubsystemRegistration の Shutdown。Unity 解体済み前提 | **しない**（directory は `ReleaseSceneTokenAfterPlayStop`。Addressables Unload は呼ばない） | **同期**。続けて `CompletePlayStop`（`StopAndDrain` は待たない） |

Play Mode 終了で `Addressables.UnloadSceneAsync` を呼ぶと `Cannot find handle for scene` になり得るため、
`ReleaseAll` は意図的に Addressables backend Scene Unload を行わない。directory の Play 停止完了は
続けて同期 `CompletePlayStop` が所有し、`StopAndDrain` は明示 close だけが使う。`.Forget()` による
非同期 Unload も持たない。

### 要求時の Content 解決（`Runtime/ContentResolution/`）

呼び出し側が供給元の種類や所在を知らないまま、要求に対して候補を得てロードし、何をどの供給元から得たかを説明するための任意の層である。

| 型 | 役割 |
|---|---|
| `ContentRequest` | logical key、kind（Scene / Object）、表現、expected identity。resolver も provider も書き換えない |
| `IContentProvider` | 供給元。候補の有無を `Available` / `NotFound` / `Unavailable` / `Rejected` で答えるだけで、bytes の取得や install はしない |
| `IContentCompatibilityRule` | 候補を採ってよいかの規則。製品の実装は `ContentCompatibilityRule`（下記）で、host が policy に渡す |
| `ResolutionPolicy` | provider の順序と規則 |
| `ContentResolver` | 上の 3 つを順に当て、probe ごとの結果を残す。Unity に依存しない（`Core/`） |
| `ContentRequestLoader` | 採用した候補を既存の `LoadContentSceneAsync` / `LoadContentAssetAsync<T>` に渡し、handle と `ContentLoadRecord` を返す |

- **順序・可用性・互換は別の判定である。** 供給元の順序、候補があること、候補を採ってよいことを混ぜない。採用した時点で以後の供給元を probe しない。不在・不達・規則の不採用は記録して次へ進む。
- **fail-closed:** 採用、identity の無い `Rejected`、pin の食い違い、`Fault`、取消の 5 つだけが探索を止める。候補は受け入れ根拠（`ContentAcceptanceBasis`）を申告し、供給元の種別ごとの固定表に合わなければ規則が採用しても採用しない。`Rejected` が 4 欄そろった identity を伴うときは、その identity と全欄一致する検証済みの候補だけで修復できる。この pin は 1 要求の中だけで持続する。
- **`IAssetManagement` に member を足さない理由:** 探索と由来は任意の機能なので、使わない利用者の必須依存にしない。loader は AssetManagement の public だけを使い、同じ assembly の internal を呼ばない。別 asmdef に分けていないので、コンパイルでは防げない。この点を確かめる検査は常設していない。
- **所有と解放は既存の契約のまま。** `Release`、`UnloadSceneAsync` → `ReleaseScene`、`ReleaseAll` を使い、解放 API を足さない。
- **まだ保証しないこと:** record は採用した候補の記録であり、実際にロードした directory との同一性は検証していない（`SourceBindingVerified` は常に false）。`IAssetManagement` が directory の identity を返さないためで、Runtime の供給元の接続で閉じる。record の表現は provider が申告した値で、`ContentDirectoryIndex` の `Full` fallback の実効値は写さない。Scene の provider adapter・SceneDirector 経由の経路・要求をまたぐ pin は未実装である。実在の provider はまだ無く、fake でだけ検証している。

#### 候補の互換判定（`ContentCompatibilityRule`）

`IContentCompatibilityRule` の製品実装である。host の宣言（宣言名・Scene 名等）を知らず、Play 前に固定した互換の基準 `ContentCompatibilityEnvelope` と、要求の expected identity だけで判定する。汎用の既定値は持たず、基準の値の出所は host か後続の adapter が決める。

| 基準の欄 | 扱い |
|---|---|
| content set、target、engine の version | 必須。候補と ordinal で完全一致させる。正規化も大文字小文字の同一視もしない |
| root schema の version | 必須（1〜999,999,999）。候補の事実は ASCII の `1`〜`9` で始まる 9 桁以内の 10 進表記だけを受け付ける |
| revision | 任意。null は「固定しない」で、互換な候補ならどの revision でも採る |

候補は engine と root schema の version を、`ContentCandidate.Facts` の予約 key `osm.compat.engineVersion` / `osm.compat.rootSchemaVersion`（`ContentCompatibilityFactKeys`）で申告する。`osm.compat.` の下の未知の key は判定に使わない。host 固有の事実は別の key に書く。

判定は次の順で、最初に当たった 1 つを `ContentCompatibilityMismatch` の値として返す（`Unset = 0` は「判定していない」で、`default` が互換と読めないようにしている）。

1. 由来と受け入れ根拠の組が `ContentDirectory` + `VerifiedDigest` か `EditorSnapshot` + `OpenSceneSnapshot` でなければ `UnsupportedSource`。この表は `ContentResolver` の private な固定表の複製なので、由来を足すときは両方を直す（offline テストが一致を確かめる）。
2. 要求に expected identity があれば、由来に関係なく 4 欄すべてを比べる（`ExpectedIdentityMismatch`）。
3. `EditorSnapshot` はここで採る。受け入れ理由が digest ではないので、identity・engine・schema を要求せず、利用者が開いている Scene を host の述語で崩さないよう述語も当てない。
4. `ContentDirectory` は identity の完全さ、content set、revision（固定した時だけ）、target、engine、schema を比べる。事実が欠けた・空白だけの候補は `…Missing`、schema の表記が壊れた候補は `SchemaVersionMalformed` で採らない。比べられない事実を一致として扱わない。
5. host が登録した述語（`IContentCompatibilityRule` の実装）を登録順に評価し、全てが採ると答えた時だけ採る（AND）。最初に false を返した述語で止め（`HostPredicateRejected`）、述語が true を返しても汎用の不採用は覆らない。述語の例外は捕まえず包み直さない。resolver が、取消済みの token の `OperationCanceledException` は取消として投げ直し、それ以外は `Fault` を記録して探索を止める。

- **不採用の残り方:** 規則の不採用は例外にせず、probe に `RuleRejected` と理由 `compat:<種別>:` を残して次の供給元へ進む。採用時の理由は `compat:Compatible:<由来>` の固定文字列で、述語の理由を連結しない。
- **要求の終わり方は既存の `DecideFailureCode` の優先順に従う。** 「規則で落ちたら必ず `NoCompatibleCandidate`」ではない。pin が未充足なら `IntegrityRejected`、`Fault` があれば `ProviderFault` になりうる。
- **`Evaluate` は互換性だけの部分判定である。** 要求の logical key・kind・表現と候補の一致、probe した供給元と候補の一致は resolver が規則の前に見る。resolver を通さずに結果だけで候補を確定する呼び手は、それらの一致を別に保証する。
- **基準を固定しない要求の読み方:** expected identity も基準の revision も無ければ、互換な候補はどの revision・digest でも採る。上位の供給元が不採用で、下位の別 revision が採られることは整合性の失敗ではない（別 revision を避けたい host は基準の revision か expected identity で固定する）。同名で digest が違う候補を見分けるのは expected identity がある時だけである。要求が表現を指定しない時は、別表現の候補も採りうる。
- **snapshot を優先する policy は、要求に directory の expected identity を載せない。** 載せると identity の空な snapshot は pin と一致せず、pin に合う directory が成功する。
- **まだ保証しないこと:** 規則は供給元の申告を照合するだけで、申告が bytes と一致することは証明しない。host の述語を登録しない policy は、宣言だけが違う候補を採る。実在の manifest から事実を作る adapter は無く、fake の供給元と事実でだけ検証している。複数 package の合成の可否、要求をまたぐ revision の一貫も判定しない。

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
IAssetPayloadProvider
└── AssetDescription (abstract, Serializable、埋め込み用)
    └── SceneAssetDescription
        ├── LoadType
        └── AssetPayload の一覧
```

現行の基底はScriptableObjectではない。SceneResourceの埋め込み構造を維持する。
AssetTypeはAssetKeyのカテゴリmetadataであり、上図の基底フィールドではない。
Prefab / Audio / Texture / Mesh等の個別Descriptionは未実装で、実需要時に追加する。
Payload列挙と拡張の現況は[18. AssetDescription](18-asset-description.md)を参照。

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

**注:** 当初案の `Observable<CacheEvent>` (R3) 前提は破棄。**AssetManagement 層にリアクティブ依存を持ち込まない**という判断であり、`GetSnapshot()` ポーリングで足りる。R3 自体はプロジェクトに導入済みで、UI 層（`HpGaugeViewModel` 等）では使っている。

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

## 13. 施行の記録 (T9-T15)

> **T12 / T13 は当初案であり、そのままの形では実装していない。** 独立 `IResourceCache` レイヤーは
> §5 のとおり不採用となり、`AssetManagement` 内の `AssetResidentCache` に統合された（実装は
> `Runtime/AssetManagement/Cache/`）。この表は当時の分割の記録であって、これから作るものの指示ではない。

| Phase | 内容 | 結果 |
|---|---|---|
| T9 | `AssetDescription` 基底、`AssetType`, `QualityLevel`, `ResourceState`, 全 interface | 施行済み |
| T10 | `MemoryBudgetConfig` (SO + IBudgetProvider) + AppConfig Override | 施行済み |
| T11 | `UnityLodGroupProvider`, `UnityTextureStreamingProvider` | 施行済み |
| T12 | `ResourceCache` (IResourceCache 実装) + `ResourceHandle` | **不採用**。`AssetResidentCache`（`AssetManagement` 内・LFU + 時間減衰）に置き換え |
| T13 | SceneDirector に IResourceCache 注入・統合 | **不採用**。T12 の変更に伴い不要 |
| T14 | CacheEvent → ITelemetrySink 接続 | 方式変更。`GetSnapshot()` ポーリング（R3 依存を持ち込まない判断）。配線は次パス |
| T15 | Editor: AssetMemoryEstimator バッチツール | 未着手 |

---

## 14. トレードオフ記録

| 決定 | 選択肢 | 採用 | 理由 |
|---|---|---|---|
| LOD 抽象化 | 直接呼び / ILodProvider / 統合 interface | ILodProvider (分離) | LOD と Mip は制御対象が異なる。MeshShader 差替え時に ILodProvider だけ交換可能 |
| QualityLevel 段階 | 2 / 4 / 連続値 | 4 段階 (STG は 2 のみ使用) | LODGroup の LOD0-2 + Unload に自然にマッピング |
| バジェット監視頻度 | ロード時のみ / 毎フレーム / 毎秒+ロード時 | 毎秒 + ロード時 | 毎フレームはコスト高、ロード時のみは遅い |
| Cache と SceneDirector | 内包 / 独立サービス / Cache が包含 | **AssetManagement 内の常駐キャッシュ** | 当初は「独立サービス」を採用したが、`AssetRegistry` と台帳が二重化するため撤回（§5 / 下段の「キャッシュ統合方式」が最終判断） |
| テレメトリ結合 | Observable(R3) / GetSnapshot ポーリング / delegate | GetSnapshot ポーリング | **AssetManagement にリアクティブ依存を持ち込まない**ため Observable 案は破棄（R3 自体は UI 層で使用中）。キャッシュはテレメトリに非依存のまま |
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
