# 10. コーディング規約・共通ルール

> [ARCHITECTURE.md](../../ARCHITECTURE.md) に戻る

---

## 10.1 命名規則

| 種別 | 規則 | 例 |
|---|---|---|
| public メソッド | PascalCase | `AddScene`, `ViewIn` |
| private メソッド | **PascalCase** | `LoadUnityScene`, `RemoveScene` |
| フィールド | `_camelCase`（private）、プロパティ経由で公開 | `_sceneDirector` |
| const / static readonly | PascalCase | `SceneResourceMapAddress` |
| enum | PascalCase | `SceneState.PreLoading` |

**旧プロジェクトからの変更:** private メソッドを camelCase → **PascalCase に統一**。C# 標準に合わせ、旧プロジェクトでの camelCase / PascalCase 混在を解消する。

## 10.2 async/await

| 状況 | 方針 |
|---|---|
| 処理が全部同期 | **async にしない。** 同期メソッドとして定義する |
| Addressable の同期ロード | `WaitForCompletion()` を使う（公式 API） |
| UniTask を同期的に待つ必要がある | `.GetAwaiter().GetResult()` |
| 完了を待たない（fire-and-forget） | `.Forget()` を明示的に使う。エラーログを残す |
| クリーンアップ処理 | `CancellationToken.None` を渡す |

## 10.3 null 安全

- `#nullable enable` を全ファイルで有効にする。
- 初期化前アクセスには `NullReferenceException` ではなく `InvalidOperationException` を投げ、何が初期化されていないかメッセージに含める。

```csharp
// ✗
public SoundService Sound => _sound ?? throw new NullReferenceException();

// ✓
public SoundService Sound => _sound
    ?? throw new InvalidOperationException(
        "SoundService is not initialized. Ensure Initialize() is called first.");
```

## 10.4 Dispose パターン

- `IDisposable` を実装するクラスは、`Dispose()` が複数回呼ばれても安全であること。
- `CancellationTokenSource` は Dispose を忘れやすい。作成したスコープで確実に Dispose する。

## 10.5 Addressable パス管理

```csharp
// ✗ 文字列リテラルを直接使わない
Addressables.LoadAssetAsync<GameObject>("Assets/Common/Prefab/EventSystem.prefab");

// ✓ 定数クラスまたは virtual プロパティで定義する
public static class AddressableKeys
{
    public const string EventSystemPrefab = "Assets/Common/Prefab/EventSystem.prefab";
    public const string SceneResourceMap = "Assets/Common/SceneMap/SceneResourceMap.asset";
}
```

## 10.6 エラーハンドリング

- 起動パスには必ず try-catch を入れる。
- Parse 系メソッドは `TryParse` を使う。
- 配列アクセスは境界チェックを行う。

```csharp
// ✗
settings.Port = int.Parse(args[i + 1]);

// ✓
if (i + 1 < args.Length && int.TryParse(args[i + 1], out var port))
    settings.Port = port;
else
    Debug.LogWarning($"Invalid or missing port argument at index {i}");
```

## 10.7 ログ

- **ZLogger** を標準のロギング基盤とする。
- `Debug.Log` / `Debug.LogWarning` / `Debug.LogError` は直接使用しない。
- OneStarMaker.Foundation 層で **`IAppLogger<T>`** ベースのロギング基盤を提供する。
- Game 層は `IAppLogger<T>` のみを参照し、ZLogger / Microsoft.Extensions.Logging を直接参照しない。
- `AppLoggerFactory` で生成する。テスト時は `NullAppLogger<T>` を注入する。
- `Trace` / `Debug` レベルは `[Conditional]` 属性により Release ビルドでゼロコスト除去される。
