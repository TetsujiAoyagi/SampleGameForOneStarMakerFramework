# asset → owner 逆引き — 実装 HANDOFF

> 作成日: 2026-08-05
> 対象: 実装担当（次セッションの人間 / エージェント）
> ブランチ: `impl/asset-owner-reverse-lookup` を切ってから作業すること
> **この文書だけで実装できるように書いてある。他の docs を読みに行かないこと。**
> 設計判断が必要になったら、実装せず停止して報告すること。

---

## 0. 今どこまで終わっているか

| 項目 | 状態 |
|---|---|
| `AssetOwner`（4種の寿命スコープ） | **済** |
| `IAssetManagement.LoadAssetAsync` が owner 必須引数 | **済** |
| owner → assets の追跡（Scene / GameObject） | **済** |
| **asset → owners の逆引き** | **未着手（本 HANDOFF の主題）** |
| 逆引きの照会 API | 未着手 |
| DebugSocket からの照会 | **本スライスの範囲外**（§5 参照） |

---

## 1. ユーザー意図（ここが正）

アセットのリークを調査するとき、現状は **「参照カウントが 1 残っている」まで分かってそこで止まる**。誰が握っているかが分からない。

`AssetOwner` はロード時に必須なので情報自体は入口を通過している。それを捨てずに保持し、`このアセットは誰が要求しているか` を答えられるようにするのが本作業。

「Scene X は何を持っているか」（owner → assets）は既に答えられる。**足りないのは逆向きだけ。**

---

## 2. 壊さない制約

### 2.1 リポジトリ全体の不変条件（本作業に関係する分）

- **`.asmdef` の `references` を追加しない。** 現在の依存グラフは Foundation → Runtime → Debug、`SampleGame.Common` → {`InGame`, `OutGame`} → `DependOnAll` の一方向で循環が無い。参照を足したくなったら設計判断なので停止して報告する
- **配線は `unity/Assets/SampleGame/DependOnAll/` のみ。** 本作業では配線を触らない
- **アセットのロードは `IAssetManagement` 経由。** `Addressables.*` を直接呼ばない
- すべての `.cs` の先頭に `#nullable enable`
- **Unity を起動しない。`tools/run-tests.ps1` を実行しない。** Unity バッチ実行は `unity/Library/`（git 管理外・破損すると再インポートに長時間）や `unity/Temp/UnityLockfile`（残留すると以降の実行を塞ぐ）に触れ、ブランチを捨てても戻らない。**テスト実行はレビュー担当が行う。** 実装が終わったら「実装完了・テスト未実行」と報告すればよい

### 2.2 本作業に固有の制約

**(a) 多重 Acquire / 多重 Release の相殺契約を壊さないこと。**

同じアセットを同じ owner が 2 回ロードしたら、2 回 Release されるまで解放されない。既存コードは `_sceneOwned` / `_goOwned` を **重複を許容する `List<string>`** で持つことでこれを保証している。実際のコメント:

```csharp
// 所有回数ぶんの Release を保証するため、重複を許容する List で保持する（_goOwned と同構造）。
private readonly Dictionary<string, List<string>> _sceneOwned = new(StringComparer.Ordinal);
private readonly Dictionary<ulong, List<string>> _goOwned = new();
```

**新設する `Owners` も同じく重複を許容する `List` にすること。** `HashSet` にすると相殺契約が壊れる。

**(b) `TrackOwner` は App と Manual を追跡していない。**

ここが最大の罠。現状の `TrackOwner`（`AssetRegistry.cs:175`）は次のとおりで、**App と Manual は意図的に何もしない**:

```csharp
private void TrackOwner(AssetOwner owner, string key)
{
    switch (owner.Kind)
    {
        case AssetOwnerKind.App:
            // App スコープの解放は ReleaseAll（全 backend を 1 回ずつ Release）が担うため所有追跡は不要。
            break;
        case AssetOwnerKind.Scene:
            /* _sceneOwned に追加 */ break;
        case AssetOwnerKind.GameObject:
            /* _goOwned に追加 */ break;
        case AssetOwnerKind.Manual:
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(owner));
    }
}
```

**したがって `Owners` への追記を `TrackOwner` の中に書いてはならない。** そうすると App / Manual 所有のアセットが逆引きから静かに漏れる。**`Acquire` 側で、`TrackOwner` の呼び出しとは独立に追記すること。**

**(c) `RefCount` を撤去しない。**

`Owners.Count` から導出可能になるが、今回は**両方持つ**。`RefCount` の参照箇所の洗い出しは別スライスとする。

**(d) `AssetOwner` の等価性は `BoundObject` を見ない。**

```csharp
public bool Equals(AssetOwner other)
{
    return Kind == other.Kind
           && GameObjectId == other.GameObjectId
           && string.Equals(Id, other.Id, StringComparison.Ordinal);
}
```

`Kind` / `Id` / `GameObjectId` の3つだけ。GameObject が破棄済みでも等価比較は成立する。

---

## 3. 変更内容

### 3.1 `AssetRegistry.LoadedAsset` に `Owners` を追加

対象: `unity/Assets/OneStarMaker/Scripts/Runtime/AssetManagement/Internal/AssetRegistry.cs`

現状の `LoadedAsset`（26行目付近）:

```csharp
internal sealed class LoadedAsset
{
    public LoadedAsset(string key, IBackendAsset backend, AssetType type, bool isInstance) { /* ... */ }

    public string Key { get; }
    public IBackendAsset Backend { get; }
    public AssetType Type { get; }
    public bool IsInstance { get; }
    public int RefCount { get; set; }
}
```

`List<AssetOwner> Owners { get; }` を追加する（重複許容・制約(a)）。

### 3.2 `Acquire` で追記する

現状（`AssetRegistry.cs:62`）:

```csharp
public LoadedAsset Acquire(string key, IBackendAsset backend, AssetOwner owner, AssetType type, bool isInstance)
{
    if (_assets.TryGetValue(key, out var loaded))
    {
        loaded.RefCount++;
    }
    else
    {
        loaded = AddAsset(key, backend, type, isInstance);
    }

    TrackOwner(owner, key);
    return loaded;
}
```

**`TrackOwner(owner, key)` とは別に `loaded.Owners.Add(owner)` を行う**（制約(b)）。`RefCount` の増減と `Owners` の増減が常に 1:1 になるように置くこと。

> 注意: `AddAsset` は `RefCount` を明示的に 1 にしていない（`LoadedAsset` の初期値依存）。既存挙動を変えないこと。`Owners` も同様に、新規/既存のどちらの経路でもちょうど 1 件追加されるようにする。

### 3.3 `Release` に owner を渡す

現状の `Release`（`AssetRegistry.cs:82`）は key しか受け取らないため、**どの owner の分を取り消すのか分からない**。呼び出し側では常に分かっているので、渡すようにする。

```csharp
public bool Release(string key, AssetOwner owner, out LoadedAsset? loaded)
```

`Owners` から **`owner` と等価な要素を1件だけ**除去する（`List<T>.Remove` は最初の一致を1件だけ消すのでそのままでよい）。等価な要素が無い場合は、既存の `RefCount--` の挙動を変えずに続行する（防御的に握りつぶす。例外を投げない）。

呼び出し側で渡す owner:

| 呼び出し元 | 渡す owner |
|---|---|
| `ReleaseSceneOwned(sceneIdentity)`（`AssetRegistry.cs:101`） | `AssetOwner.Scene(sceneIdentity)` |
| `ReleaseGameObjectOwned(gameObjectId)`（`AssetRegistry.cs:120`） | GameObject id から作る（下記 3.4） |
| `AssetManagement.ReleaseKey(key)`（`AssetManagement.cs:224`。`Release(IAssetHandle)` からのみ呼ばれる） | `AssetOwner.Manual` |

`ReleaseAllAssets()`（`AssetRegistry.cs:140`）は全消去なので owner 単位の処理は不要。

### 3.4 `AssetOwner` に GameObject id からの生成と `ToString()` を足す

対象: `unity/Assets/OneStarMaker/Scripts/Runtime/AssetManagement/Abstractions/AssetOwner.cs`

- **`internal static AssetOwner FromGameObjectId(ulong id)`** を追加する。既存の `Bind(GameObject go)` は生きた `GameObject` を要求するが、`ReleaseGameObjectOwned` の時点では既に破棄されているため使えない。`Kind = GameObject`, `Id = id.ToString()`, `GameObjectId = id`, `BoundObject = null` で作る（制約(d)より等価比較は成立する）
- **`ToString()` オーバーライド**を追加する。診断表示用。`Kind` / `Id` / `GameObjectId` は `internal` のままにし、**public プロパティを新設しない**。例: `"Scene(Title)"`, `"GameObject(12345)"`, `"App"`, `"Manual"`

### 3.5 照会インターフェース `IAssetDiagnostics`

`IAssetManagement` にメソッドを足さない（§5）。新規ファイルで別インターフェースを切る。

配置: `unity/Assets/OneStarMaker/Scripts/Runtime/AssetManagement/Abstractions/IAssetDiagnostics.cs`

```csharp
#nullable enable

using System.Collections.Generic;

namespace OneStarMaker.Runtime.AssetManagement
{
    /// <summary>
    /// ロード済みアセットの所有関係を照会する診断用 API。
    /// 本番ロジックから使うことは想定していない（リーク調査・デバッグ用）。
    /// </summary>
    public interface IAssetDiagnostics
    {
        /// <summary>指定キーを現在所有している owner を列挙する。未ロードなら空。</summary>
        IReadOnlyList<AssetOwner> GetOwners(AssetKey key);

        /// <summary>指定 owner が現在所有しているアセットキーを列挙する。無ければ空。</summary>
        IReadOnlyList<AssetKey> GetOwnedAssets(AssetOwner owner);
    }
}
```

`AssetManagement`（`unity/Assets/OneStarMaker/Scripts/Runtime/AssetManagement/AssetManagement.cs`）に `IAssetDiagnostics` を実装させる（`class AssetManagement : IAssetManagement, IAssetDiagnostics`）。

実装方針:

- `GetOwners(key)`: `_registry.TryGetAsset(key.Canonical, out var loaded)` → `loaded.Owners` のコピーを返す。未ロードなら空リスト
- `GetOwnedAssets(owner)`: 全 `LoadedAsset` を走査し、`Owners` に等価な owner を含むものの `Key` を返す。**重複所有していても `AssetKey` は1件だけ返す**（「何を持っているか」の問いなので）。`AssetRegistry` 側に列挙用の internal メソッドを足してよい
- `AssetKey` の生成は既存の生成方法に合わせること（テストでは `AssetKey.FromAddress(...)` が使われている）。`LoadedAsset.Key` は `AssetKey.Canonical` に相当する文字列である

### 3.6 テスト

配置: `unity/Assets/OneStarMaker/Tests/AssetManagement/AssetOwnerLookupTests.cs`

既存の書式に合わせること（`unity/Assets/OneStarMaker/Tests/AssetManagement/AssetManagementCacheTests.cs` が参考になる）:

```csharp
#nullable enable

using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.AssetManagement
{
    [TestFixture]
    public class AssetOwnerLookupTests
    {
        // [SetUp] で FakeAssetBackend と AssetManagement を組む
        // [UnityTest] public IEnumerator Xxx() => UniTask.ToCoroutine(async () => { ... });
    }
}
```

`FakeAssetBackend` は `unity/Assets/OneStarMaker/Tests/AssetManagement/FakeAssetBackend.cs` に既にある。新規に作らないこと。

---

## 4. 受け入れ条件

### 4.1 新規テスト 4 本（`AssetOwnerLookupTests`）

| # | テスト内容 |
|:--:|---|
| 1 | `AssetOwner.Scene("Title")` でロード → `GetOwners` がその owner を1件返す。`ReleaseScene("Title")` 後は `GetOwners` が**空**になる |
| 2 | `AssetOwner.Bind(go)` でロード → `GetOwners` が GameObject owner を返す。GameObject 破棄経由の解放後は**空**になる |
| 3 | 同一キー・同一 owner で 2 回 Acquire → `GetOwners` が**2件**返す（重複記録される）。1回 Release すると**1件**残る |
| 4 | 未ロードのキーに対して `GetOwners` が**空リスト**を返す（null を返さない・例外を投げない） |

加えて `GetOwnedAssets` が owner → キーを返すことを、上記いずれかのテスト内で併せて確認すること。

### 4.2 回帰

**「全件緑」ではなく「既知の失敗以外に新規失敗が無いこと」で判定する。**

2026-08-05 時点の `develop` 作業ツリーで実測したベースライン:

```
total: 406 / passed: 403 / failed: 3 / skipped: 0
```

既に失敗している 3 件（**本作業とは無関係。直さなくてよい**）:

| テスト | メッセージ |
|---|---|
| `OneStarMaker.Tests.Foundation.TelemetryLogCorrelationTests.LogAndTelemetry_共有sequenceで1_2_3と採番される` | FinishSpan telemetry は span 内 log の後に wire 化される |
| `OneStarMaker.Tests.Foundation.TelemetryLogCorrelationTests.LogInsideActiveSpan_TraceIdとSpanIdを持つ` | Expected: 8972270 |
| `OneStarMaker.Tests.UpdateSystem.UpdateSystemHostTests.TryConsumeActivationRequest_BeforeSceneDirectorBinding_ReturnsFalse` | `Destroy may not be called from edit mode!`（`UpdateSystemHost.cs:92` に EditMode ガードが無い既存バグ） |

合格条件:

- 上記 3 件**以外**の失敗が 0 件
- 新規テスト 4 本が追加され、`total` が 410 になる
- 特に `OneStarMaker.Tests.AssetManagement` 配下（`AssetManagementTests` / `AssetManagementCacheTests` / `AssetResidentCacheTests`）が緑のままであること。`AssetRegistry.Release` のシグネチャ変更の影響を直接受けるため

> **注意（レビュー担当向け）:** Unity はバッチ実行の終了時にアクセス違反（終了コード `-1073741819`）でクラッシュする。2026-08-05 時点で実行のたびに再現している。
>
> - 結果 XML は正常に書かれるため、テスト結果自体は信頼してよい。`tools/run-tests.ps1` はプロセス終了コードではなく XML を正本として判定する
> - クラッシュにより `unity/Temp/UnityLockfile` が残骸として残る。ランナーは「ファイルの存在」ではなく「プロセスが排他保持しているか」で判定し、残骸なら自動削除して続行する
> - **このクラッシュ自体は未調査。** テスト結果には影響しないと判断しているが、根本原因は追えていない

### 4.3 実行コマンド（**実行するのはレビュー担当。実装側は実行しない**）

```powershell
pwsh tools/run-tests.ps1
```

絞り込み実行:

```powershell
pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.AssetManagement
```

exit 0 かつ failed 0 で合格。**テスト0件は失敗扱い**（コンパイルエラーは0件として現れるため）。

### 4.4 `.meta` について

新規 `.cs` を作ると Unity が `.meta` を生成するが、**Editor 不在では生成されない**。これは想定内。

**`.meta` を手書きしないこと**（GUID 衝突の原因になる）。レビュー担当が Unity 起動時に生成する。

---

## 5. やらないこと

- **DebugSocket コマンドの追加**。当初案に含まれていたが本スライスから外した。`DebugSocketBuiltInCommandHandler.TryHandle` は `RuntimeDiagnosticsSnapshot` を受け取る静的メソッドで、`IAssetDiagnostics` を届けるには `DebugSocketService` への依存注入と配線変更が要る。これは設計判断を伴うため別スライスとする
- `RefCount` の撤去（制約(c)）
- `IAssetManagement` へのメソッド追加
- `AssetResidentCache` への波及（キャッシュ滞留中のアセットの owner をどう扱うかは別問題）
- `AssetOwner` の `Kind` / `Id` / `GameObjectId` を `public` にすること
- `.asmdef` の変更
- 設計ドキュメントの新規作成

---

## 6. 差し戻し

（Phase C で逸脱が見つかった場合にレビュー担当が追記する。現時点では空。）

---

## 7. レビュー結果

> 実施日: 2026-08-05 / 実装: `composer-2.5`（Cursor CLI headless）/ レビュー: Claude
> ブランチ: `impl/asset-owner-reverse-lookup` / HEAD: `cf69bfa`（**コミットなし。全て未コミット**）
> 判定: **受け入れ可**（下記 7.5 の指摘は本スライスの差し戻し事由としない）

### 7.1 検査した不変条件

すべて `git diff` / `grep` による実測。主張ではない。

| # | 条件 | 結果 | 根拠 |
|:--:|---|:--:|---|
| 1 | `.asmdef` の `references` 追加なし | OK | `git status -- '*.asmdef'` に本作業起因の変更なし |
| 2 | 配線は `DependOnAll` のみ（今回は不変） | OK | **本作業の増分に** `SampleGame/DependOnAll/` の変更なし（#16 の実行前後差分による）。※作業ツリーには本作業と無関係な既存変更 `SampleGame.DependOnAll.Editor.asmdef` および未追跡 Editor スクリプトが存在する |
| 3 | `Addressables.*` 直呼びなし | OK | 変更5ファイルの `grep "Addressables\."` は `AssetManagement.cs:190` の**既存コメント**1件のみ |
| 4 | `MonoBehaviour.Update` 新規なし | OK | 差分に `+void Update(` / `+void LateUpdate(` なし |
| 5 | 全ファイル `#nullable enable` | OK | 5ファイルすべて1行目で確認 |
| 6 | 制約(a) `Owners` は重複許容 `List` | OK | `List<AssetOwner>`。`Remove` は最初の1件のみ除去 |
| 7 | **制約(b) `TrackOwner` の罠を踏んでいない** | OK | `loaded.Owners.Add(owner)` は `Acquire` 側にあり、`TrackOwner` の外。App/Manual も記録される |
| 8 | 制約(c) `RefCount` 未撤去 | OK | 併存。`Acquire` で `RefCount` と `Owners` が 1:1 で増える |
| 9 | 制約(d) 等価性が `BoundObject` 非依存 | OK | `FromGameObjectId` は `BoundObject=null` で生成、`Equals` は Kind/Id/GameObjectId のみ |
| 9b | §3.4 `ToString()` オーバーライドの追加 | OK | `AssetOwner.cs:84-99`。`App` / `Manual` / `Scene({Id})` / `GameObject({GameObjectId})` を返す。§3.4 の例示と一致 |
| 10 | §5 `IAssetManagement` へのメソッド追加なし | OK | 同ファイル未変更 |
| 11 | §5 `AssetResidentCache` 波及なし | OK | `Cache/` 配下に変更なし |
| 12 | §5 DebugSocket 変更なし | OK | `DebugSocketServices/` に変更なし |
| 13 | §5 `internal` の public 昇格なし | OK | `Kind` / `Id` / `GameObjectId` は internal のまま |
| 14 | §5 設計ドキュメント新規作成なし | OK | 実行前後の `git status` 差分に `.md` の増加なし |
| 15 | 指示どおりコミットしていない | OK | HEAD が `cf69bfa` のまま |
| 16 | 他人の未コミット作業を巻き込んでいない | OK | 実行前 34 件 → 実行後の増分は宣言された5ファイルのみ |

### 7.2 テスト実行証跡

```
コマンド : pwsh tools/run-tests.ps1        （フィルタなし = 全 EditMode）
結果     : total 410 / passed 407 / failed 3 / skipped 0
所要     : 3.1 分
exit code: 1
XML      : TestResults/results-all-20260805-014623.xml
ログ     : TestResults/unity-all-20260805-014623.log
```

- **`total` がベースライン 406 → 410。** 受け入れ条件の「新規4本」と一致
- **failed 3 件は §4.2 の既知失敗と<u>テスト名が</u>一致。新規失敗ゼロ。**
  ただし**失敗メッセージまでは一致しない** — 例: `LogInsideActiveSpan_TraceIdとSpanIdを持つ` は §4.2 記録時が `Expected: 8972270`、本実行が `Expected: 11370973`。メッセージ中の数値は実行ごとに変わるため、**照合はテスト名で行うのが正しく、メッセージ一致を判定基準にしてはならない**
- `OneStarMaker.Tests.AssetManagement` 配下 **33 件すべて Passed**（`Release` シグネチャ変更の回帰なし）
- 新規4本すべて Passed:
  `SceneOwner_GetOwnersAndReleaseScene` / `GameObjectOwner_GetOwnersAndDestroyRelease` /
  `SameOwner_DoubleAcquire_PartialRelease` / `UnloadedKey_GetOwners_ReturnsEmptyList`
- exit 1 は既知失敗3件によるもの
- Unity プロセスの終了コードは `-1073741819`（既知のクラッシュ）。**出典は結果 XML ではなくランナーの標準出力**（`tools/run-tests.ps1` が `Unity 終了コード: -1073741819 （所要 3.1 分）` と出力）。XML に終了コードは含まれない。ランナーは XML を正本に判定するため誤判定していない

### 7.3 `.meta` 整合

新規2ファイルとも、レビュー時のテスト実行（Unity 起動）で `.meta` が生成された。手書きではない。

| ファイル | GUID |
|---|---|
| `Abstractions/IAssetDiagnostics.cs.meta` | `4e3a5cd138b87b04099d1fb152074129` |
| `Tests/AssetManagement/AssetOwnerLookupTests.cs.meta` | `9ad712d8cedd7a045b22fe6e000e235c` |

**未コミットなので、コミット時に `.cs` と `.meta` を必ず同時に含めること。**

### 7.4 確認していないこと

**このスライスで検証していない事項。C' はここを重点的に見ること。**

1. **`GetOwnedAssets` の instance key 経路は一切テストしていない。** 新規4本はすべて `LoadAssetAsync` 経由で、`InstantiateAsync` が作る合成キー（`address:...:instance:<goId>`）を `GetOwnedAssets` に通す経路は未実行。7.5 の指摘はコード読解によるもので、**実行して再現させてはいない**
2. **`AssetOwner.App` でロードしたハンドルを `Release(IAssetHandle)` する経路**（実装者の自己申告2）は未テスト。`Owners` と `RefCount` が乖離しうるが、実測していない
3. **Play Mode / 実機 / IL2CPP ビルドは未確認。** EditMode のみ
4. **スレッド安全性は未確認。** `Owners` は素の `List` で、既存の `_sceneOwned` / `_goOwned` と同水準。並行アクセスの想定を確認していない
5. **メモリ影響を計測していない。** ロード済みアセット1件ごとに `List<AssetOwner>` が1つ増える。常駐アセットが多い状況での実測はしていない
6. **`GetAllLoadedAssets()` が毎回リストを新規確保する**点は診断用途として許容したが、呼び出し頻度の想定を確認していない
7. **実装エージェントが Unity を起動しなかったことの直接証拠はない。** ルールには明記されており、`Library/` に異常は見られず、テストも正常に完走したため問題は無いと判断したが、プロセス記録で確認したわけではない
8. **`description:` プレフィックスのキーが `Acquire` に到達しないという判断**は、`AssetKey.FromDescription` の呼び出し元を grep して確認した（`AssetManagement.cs:118` のシーンロードのみ、シーンは `AddScene` 経由）。ただし**将来の変更でこの前提が崩れる可能性は残る**

### 7.5 指摘（本スライスの差し戻し事由とはしない）

**`AssetManagement.ToAssetKey` は、そもそも `AssetKey` でなかった文字列から `AssetKey` を再構成している。**

実装者は「`description:` キーは復元できないので例外にした」と自己申告したが、**実際に問題になるのはそちらではない。**

- **例外パス（`description:`）は現状到達不能。** `FromDescription` の本番呼び出しは `AssetManagement.cs:118`（シーンロード）のみで、シーンは `AddScene` を通り `Acquire` に入らない。潜在的な地雷だが今は踏まない
- **実際に踏むのは instance key 経路。** `InstantiateAsync` は `instanceKey = $"{key.Canonical}:instance:{owner.GameObjectId}"`（`AssetManagement.cs:148`）を合成する。これは `address:` で始まるため `ToAssetKey` は**例外を投げずに通し**、`AssetKey.FromAddress("Assets/X.prefab:instance:12345")` を呼ぶ:

  | | 結果 |
  |---|---|
  | `Canonical` | 偶然そのまま復元される（等価比較は成立する） |
  | `Address` | `Assets/X.prefab:instance:12345` = **存在しない Addressables アドレス** |
  | `Type` | 末尾が `.prefab` でないため `InferType` が `Other` を返す（正しくは `Prefab`） |

  結果として、Prefab をインスタンス化した GameObject owner に `GetOwnedAssets` を呼ぶと**壊れた `AssetKey` が黙って返る**。

**深刻度: 低。** 診断専用 API であり、等価比較は `Canonical` のみを見るため動作はする。ただし「黙って壊れている」のは例外より悪い。

**これは設計判断を要するため Phase A に戻す。** 想定される選択肢:

1. `LoadedAsset` に元の `AssetKey` を保持させ、再構成そのものを無くす（instance エントリは合成文字列なので別途表現が要る）
2. `GetOwnedAssets` の戻り値を `AssetKey` ではなく正規化文字列にする
3. instance エントリを `GetOwnedAssets` の対象から除外する、または明示的にフラグを立てて返す

**実装者を責める指摘ではない。** HANDOFF §3.5 が「`AssetKey` の生成は既存の生成方法に合わせること」とだけ書き、instance key の存在を伝えていなかった。**HANDOFF 側の転記漏れが原因である。**

---

## 8. C' 監査結果

> 実施日: 2026-08-05 / 監査: `cursor-grok-4.5-high`（Cursor CLI headless）
> **実装は `composer-2.5`、レビューは Claude、監査は Grok。三者すべて別。**
> 監査結果は職掌3項目内に収まっており、設計の再議論・実装やり直し提案・HANDOFF 外要求の追加はなかった。

### 8.1 指摘と対応

**4件すべて妥当と判断し、反証せず §7 を訂正した。** 受け入れ判定「受け入れ可」を覆す §4 不充足は指摘されていない。

| # | 分類 | 指摘 | 対応 |
|:--:|---|---|---|
| 1 | 未検証の断定 | 7.2「既知失敗と**完全一致**」は過大。テスト名は一致するが失敗メッセージは相違（§4.2 記録時 `Expected: 8972270` / 本実行 `Expected: 11370973`、XML:1159） | **受諾。** 7.2 を「テスト名が一致」に訂正し、**メッセージ中の数値は実行ごとに変わるため照合基準にしてはならない**旨を明記した |
| 2 | 未検証の断定 | 7.2 の Unity 終了コード `-1073741819` は引用 XML に含まれず証跡不足 | **受諾。** 出典がランナー標準出力であることを明記し、ログのパスを 7.2 に追加した |
| 3 | 見落とし / 未検証の断定 | 7.1 #2「`DependOnAll/` に変更なし」は作業ツリー全体では偽（`SampleGame.DependOnAll.Editor.asmdef` に既存変更あり）。増分限定なら根拠を #16 に寄せるべき | **受諾。** #2 を「本作業の増分に変更なし（#16 の実行前後差分による）」へ書き換え、無関係な既存変更の存在を併記した |
| 4 | 見落とし | §3.4 が要求した `ToString()` の検証が 7.1 に無い（実装自体は `AssetOwner.cs:84-99` にあり） | **受諾。** 検査項目 #9b を追加した |

### 8.2 監査が確認した受け入れ条件

§4.1 の4テスト・付帯条件・§4.2 の total 410 / 新規失敗0 / AssetManagement 33件緑・§4.4 の `.meta` GUID 一致について、XML の行番号を根拠に**全項目「充足」**と判定された。

`.meta` が手書きでないことの直接証明はプロセス主張である旨も併記されており、7.4 の自己開示と整合する。

### 8.3 監査が「隠蔽された未検証断定ではない」と判定した項目

7.5 の instance key / `ToAssetKey` に関する断定は、7.4 #1 で「実行して再現させてはいない」と自己開示済みであり、コード上の根拠（`AssetManagement.cs:148`、`AssetKey.cs:102-104`）とも矛盾しないため問題なしと判定された。

### 8.4 この周回から得られた運用上の知見

- **C' は機能した。** 検出された4件はすべて「レビュー側の断定の精度」に関するもので、これは C' が担うべき領域そのものである。実装の欠陥は Phase C が、レビューの欠陥は C' が捕まえた
- **三者を別モデルにした効果が出ている。** 実装（composer）→ レビュー（Claude）→ 監査（Grok）で、それぞれ前段の見落としを検出した
- **本スライスの律速は「受け渡しの機械作業」ではなく「HANDOFF の質」だった**（7.5 の欠陥が HANDOFF の転記漏れ由来）。したがって Phase B' の自動化は着手条件を満たさない

---

## 正本ポインタ（人間用・実装時に読む必要はない）

- アセット寿命の設計: `unity/Assets/Docs/Architecture/13-resource-system.md`
- 常駐キャッシュ: `unity/Assets/Docs/Architecture/19-asset-resident-cache-tickets.md`
- 本作業の発端: `docs/planning/UNUSED_API_INVENTORY_2026-08-03.md` と、ChatGPT 提示の評価基準「このアセットは誰が要求しているかを追跡できるか」
