# 7〜9. サウンド・入力・バックグラウンドサービス

> [ARCHITECTURE.md](../../ARCHITECTURE.md) に戻る

---

## 7. サウンド

### 7.1 構成

```
SoundService (MonoBehaviour, DontDestroyOnLoad)
  ├── VoiceGroup … 同時再生数制限の定義（ScriptableObject）
  └── SoundHolder … AudioClip のまとまり（ScriptableObject）
```

### 7.2 ルール

- VoiceGroup で同時再生数を制限し、上限超過時は優先度の低い音を停止する。
- フェードアウトは `CancellationTokenSource` で管理し、Dispose 時に確実にキャンセルする。
- SoundService は OneStarMaker.Runtime 層に置き、ゲーム固有の音定義は Game.Common 層で ScriptableObject として管理する。

---

## 8. 入力

### 8.1 構成

```
InputManager (OneStarMaker.Runtime)
  └── InputObserver … ActionMap の切り替え、イベント配信

NewStgCommonInput : InputManager (Game.Common)
  └── ゲーム固有の Action を enum で型安全に公開
      R3 の Observable でイベントを配信
```

### 8.2 ルール

- `InputActionAsset` は Unity の Input System で管理する。
- ActionMap の切り替え（Player ↔ UI）は `InputObserver.ChangeMode()` で行う。
- ゲーム固有の Action 定義は Game.Common 層の enum で管理し、OneStarMaker.Runtime は enum を知らない。
- イベント配信には R3 の `Observable` を使用する（旧プロジェクトの UniRx `IObservable` から移行）。

---

## 9. バックグラウンドサービス（HostedService）

### 9.1 設計

ASP.NET Core の `IHostedService` パターンを VContainer 上に薄くラップしたもの。

```
IHostBuilder
  └── Build() → IHostedServiceExecutor
                    ├── StartServicesAsync()  Starting → Start → Started
                    └── StopServicesAsync()   Stopping → Stop  → Stopped

IHostedService           … Start/Stop のみ
IHostedLifecycleService  … Starting/Started/Stopping/Stopped フック付き
BackgroundService        … 長時間実行タスクの基底クラス（UniTask ベース）
```

### 9.2 VContainer との統合

- `HostedServiceExecutor` を VContainer の `IAsyncStartable` として登録する。
- VContainer の Scope 破棄時に `StopServicesAsync` を呼ぶ。
- `IHostedService` / `IHostedLifecycleService` のインターフェースはそのまま定義（VContainer に依存しない）。

### 9.3 ルール

- サービスの登録は `IHostBuilder.Services.Add()` で起動前に行う。
- サービスの取得は `IHostedServiceExecutor.GetService<T>()` で行う。
- `BackgroundService` を継承して `ExecuteAsync` を実装すれば、バックグラウンドタスクを簡単に追加できる。
