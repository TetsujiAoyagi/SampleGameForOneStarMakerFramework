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

`InputManager` は任意に導入する現在値の公開コンポーネントである。全 API は main thread で呼ぶ。SampleGame の既存コントローラは利用していない。`InputFrame` が純粋な managed 値、`InputActionAssetReader` が native read とマップ操作、manager が通知と登録 lease を所有する。Button / Vector2 を map + action 名で一度 index に解決し、以後 `TryRead` または `Published` span で読む。その他の型は公開せず `UnsupportedActionCount` に数える。

### 8.2 所有者と寿命

- 所有者は `IAssetManagement` と適切な `AssetOwner` で自分専用の `InputActionAsset` を取得してから manager を生成する。manager は asset を clone / load / Destroy しない。
- Player / UI マップ操作は排他的 lease である。同じマップを `PlayerInput`、`InputSystemUIInputModule`、他の controller と共有しない。全カタログ検証後に Player を有効化する。
- main thread で host 導入後に parameterless `TryRegister()` を呼ぶ。Input layer は order -100 / execution 0。未導入または失敗は false で再試行可能、成功後の重複も false。初回 activation は host の SceneDirector 接続と scene 安定 gate に従う。
- 所有者が対象 scene を選び `SetInteractionState` を渡す。初期 None、および Stable 以外への変更は即座に公開値を中立化する。Stable は WorldReady を意味しない。Stable に戻っただけでは中立を保ち、次の Sample で現在値を読む。
- 所有者は host 終了と asset 解放より前に manager を Dispose する。Dispose は即座に中立化し、実際に登録した coordinator から解除し、両マップを無効化して通知を終了する。asset はそれまで生存させる。

### 8.3 公開値と選択

`TrySetMap` は Player / UI だけを選ぶ。実変更は中立化後に native map を切り替え、成功後に ActiveMap と `MapChanged` を確定する。同じ map は再有効化や通知をしない。native 操作失敗は元の例外を伝え、中立を保って両マップの停止を試みる。以後 Sample は読まず、有効な選択の成功でだけ復旧する（以前の ActiveMap の再指定も再試行となる）。未知 map / profile は状態を変えない。profile は default ID だけで、リバインドや片腕プロファイルは未実装。

`Published` は manager 一つの共有 buffer であり、Sample、停止、map 変更、Dispose による上書きまでが値の寿命である。保持済み span も中立化を観測する。Dispose 後の読み取りは中立 slot を返し、明示的な変更 API は `ObjectDisposedException`。Update の level snapshot であり、押下 edge や FixedUpdate 向けの同期を保証しない。再開後の sample では保持中の control が保持値として現れうる。ゲーム固有 enum、scene readiness、UI module と cursor のモードは所有者側の責任である。

---

## 9. バックグラウンドサービス（HostedService）

### 9.1 設計

ASP.NET Core の `IHostedService` パターンの薄い移植（DI コンテナには依存しない。手動 DI 正式採用については [03-di.md](03-di.md) 参照）。

```
IHostBuilder
  └── Build() → IHostedServiceExecutor
                    ├── StartServicesAsync()  Starting → Start → Started
                    └── StopServicesAsync()   Stopping → Stop  → Stopped

IHostedService           … Start/Stop のみ
IHostedLifecycleService  … Starting/Started/Stopping/Stopped フック付き
BackgroundService        … 長時間実行タスクの基底クラス（UniTask ベース）
```

### 9.2 起動処理との統合

- `HostedServiceExecutor` は `AbstractApplicationInitializer` の起動フェーズ内で `StartServicesAsync` を呼ぶ。
- アプリ終了時（`Application.quitting`）に `StopServicesAsync` を呼ぶ。
- `IHostedService` / `IHostedLifecycleService` のインターフェースは特定の DI コンテナ・フレームワークに依存しない。

### 9.3 ルール

- サービスの登録は `IHostBuilder.Services.Add()` で起動前に行う。
- サービスの取得は `IHostedServiceExecutor.GetService<T>()` で行う。
- `BackgroundService` を継承して `ExecuteAsync` を実装すれば、バックグラウンドタスクを簡単に追加できる。
