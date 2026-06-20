# 実装レビューと設計意図（2026-05-27）

このドキュメントは、直近で入れた以下の実装について、**なぜその設計にしたか**と**実装内容**を整理し、再レビュー時の修正点を記録したものです。

- DebugStudio の WebSocket Server Inversion
- CLI からの DebugCommand 制御プレーン
- Unity 側 Updater foundation（ゼロアロケーション志向の土台）

---

## 1. DebugStudio を Server 化した理由と実装内容

### なぜこの実装にしたか
- 運用要件として、Unity 側を outbound 接続（client）にしておくと、Editor/実機どちらでも接続経路を一本化しやすい。
- DebugStudio 側を受け口（server）に固定すると、ログ・テレメトリ・コマンド相関を 1 セッション管理に寄せられる。
- WPF UI を「接続先を打つ client UI」から「待受状態を可視化する server UI」へ転換でき、運用時の誤操作を減らせる。

### 実装内容（要点）
- `DebugStudio.App` 側で server transport を常時待受構成に変更。
- Unity 側 `DebugSocketService` に `Listen` / `Connect` モードを持たせ、`Connect` では再接続ループを実装。
- `app-config.json` を `debugSocket:mode = connect`（DebugStudio へ outbound）へ変更。
- 既存の `CommandResult` 相関ルールは維持し、transport の向きだけを反転。

---

## 2. CLI 制御プレーンの理由と実装内容

### なぜこの実装にしたか
- CLI を main endpoint へ直接つなぐ方式は、Unity セッションと競合するため不適切。
- そのため「CLI 専用ローカル endpoint」を追加し、**CLI -> App control plane -> 既存 SessionService/CommandService -> Unity** の経路にした。
- これで Unity mainline session を壊さずに CLI コマンドを中継できる。

### 実装内容（要点）
- `DebugStudioCliControlService` を追加し、`ControlCommandRequest/Response` プロトコルで受信・返信。
- `DebugCommandControlPlaneClient` を追加し、CLI は control plane URI（既定 `ws://127.0.0.1:5012/cli-control/`）へ接続。
- `AppCompositionRoot` で control service の起動/破棄をアプリ寿命に統合。
- `TransportCommandSender` と `ICommandSender` 抽象で、WPF/CLI で共通の requestId 相関ロジックを利用。

---

## 3. Unity Updater foundation の理由と実装内容

### なぜこの実装にしたか
- Scene 管理責務（resource/GameObject）と、更新制御責務（順序・pause・delta・将来並列化）を分離するため。
- `Awake` で register し、`Start` 相当を `Update` より前に一度だけ保証するため、pending -> active 昇格フェーズを明示した。
- 更新中の構造変更で破綻しないよう、`LateUpdate` 後 apply の deferred 方式を採用した。

### 実装内容（要点）
- Foundation:
  - `UpdaterFrameContext`, `IUpdater`, `UpdaterLayer`, `UpdaterWorld`
  - Layer ごとの `LayerOrder`, `ExecutionOrder`, `TimeScale`, `IsPaused`
  - `ActivatePendingRegistrations()`, `ApplyStructuralChanges()` を提供
- Runtime:
  - `UpdaterRuntimeHub`, `UpdaterDriver`, `UpdaterBehaviour`, `UpdaterRuntime`
  - `AbstractApplicationInitializer` の `BeforeSceneLoad` で hub を install
  - `SceneDirector` の `SceneEventType.Added` を scene 安定化トリガとして昇格実行
- テスト:
  - Start 相当先行、timeScale 反映、pause 停止、deferred register/remove の契約を `UpdaterWorldTests` で固定

---

## 4. 再レビューで見つかった問題と修正

### 修正した問題
- **問題:** 同一 `LayerId` に対して異なる `layerOrder` を後から渡しても静かに無視され、順序バグを潜在化させる。
- **修正:** `UpdaterWorld.GetOrCreateLayer` で既存 Layer の `LayerOrder` と不一致なら `InvalidOperationException` を投げるよう変更。
- **追加テスト:** `Register_同一LayerIdで異なるLayerOrderは例外になる`

### 修正ファイル
- `Assets\OneStarMaker\Scripts\Foundation\Core\TelementaryEnum.cs`
- `Assets\OneStarMaker\Tests\Scene\SceneLifecycleManagerTests.cs`

---

## 5. 現時点の判断（品質と今後）

- 直近実装の方向性は妥当。特に、CLI を main endpoint から分離した判断は運用上の整合性が高い。
- Updater は foundation としては成立しており、Scene 依存を減らした更新制御の基盤になっている。
- 未完了領域は、NativeArray/Job 前提の並列データパス、および FixedUpdate 系 Layer の設計。

