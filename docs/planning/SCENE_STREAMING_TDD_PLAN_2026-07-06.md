# SceneStreaming TDD 施行計画 (T-03〜T-09) — 2026-07-06

> 設計の正典は `unity/Assets/Docs/Architecture/21-scene-streaming.md`（以下「正典」）。
> 本書はチケットを他の Agent へ委任するための **TDD 施行表 + 実装ガードレール**。
> T-01 / T-02 は完了済み（正典 §10 の完了記録参照）。本書は T-03 以降を対象とする。

---

## 0. 運用ルール（人間 + 複数 Agent での回し方）

| 役割 | 担当 | 備考 |
|---|---|---|
| レッドテスト作成 | 安価モデル | 本書のチケット別テスト仕様に従う。**コンパイルは必ず通す**（§1.1） |
| テストレビュー | 上位モデル | テストが仕様を正しく表現しているか・弱いアサートがないかを検査 |
| 実装（グリーン化） | 実装担当 Agent | 本書 §2 のガードレール厳守 |
| 完了判定 | 上位モデル or 人間 | §1.3 の完了条件で判定し、正典へ完了記録を追記 |

- 1 チケット = 1 コミット推奨。T-03 はストリーミングと独立に価値があるため特に分離すること
- チケット完了ごとに正典 §10 の該当行へ ✅ と完了記録を追記する（T-01/T-02 の記録形式に倣う）
- セッションを跨ぐ場合は本書と正典の完了記録だけで文脈復元できる状態を保つ

---

## 1. 全チケット共通プロトコル

### 1.1 TDD サイクル（Unity 特有の制約込み）

Unity ではテストコードのコンパイルエラーが **テストアセンブリ全体を落とし、既存テストごと実行不能になる**。
よって「まず失敗するテストを書く」を次のように運用する:

1. **API 形状の変更を伴うチケット**（T-03 等）: 先にシグネチャだけ追加する（既定値で従来挙動、実装は空）→ テストがコンパイルは通るが**挙動でレッド**になる状態を作る → 実装 → グリーン
2. **新規クラスのチケット**（T-04, T-06 等）: スケルトン（`NotImplementedException` 可）とテストを同時に作成 → 全テストがレッド → 実装 → グリーン
3. レッド状態のテストは Ignore にせずレッドのままコミットしてよい（T-01 の前例に従う）。ただしコミットメッセージと正典に「意図されたレッド」であることを明記する

### 1.2 テスト実行（実証済みコマンド）

Unity エディタを閉じた状態で PowerShell から実行する。`&&` は使えないので `;` で連結すること。
`-nographics` は必須（付けないとバッチモードがクラッシュした実績あり）。

```powershell
& "D:\UnityEditor\6000.5.0f1\Editor\Unity.exe" -batchmode -nographics `
  -projectPath "D:\repositories\unity\SampleGameForOneStarMakerFramework\unity" `
  -runTests -testPlatform EditMode `
  -testFilter "OneStarMaker.Tests.SceneSystem" `
  -testResults "D:\repositories\unity\SampleGameForOneStarMakerFramework\test-results-scenesystem.xml" `
  -logFile "D:\repositories\unity\SampleGameForOneStarMakerFramework\unity-test-run-scenesystem.log" | Out-Null
```

- 完了後、XML 冒頭の `<test-run ... result= total= passed= failed=` 行で判定する
- 全体回帰は `-testFilter "OneStarMaker.Tests"`、結果は `test-results.xml` へ
- 所要時間の目安: SceneSystem 単体 ≈ 1分、全体 ≈ 1分（2026-07-06 時点）
- **テスト結果が「コンパイルエラーで 0 件実行」になっていないか必ず確認する**。ログ末尾に compile error があれば作業は未完了

### 1.3 完了条件（全チケット共通）

1. チケットのレッドテストが全てグリーン
2. `OneStarMaker.Tests` 全体で回帰ゼロ（2026-07-06 時点のベースライン: 181 本グリーン + T-03 レッド分）
3. 正典 §10 へ完了記録を追記（テスト本数・検証結果・割り切り事項）

---

## 2. 絶対制約（実装 Agent 向けガードレール）

過去に実際に壊れた場所から導いた規則。**違反が必要に見えた時点で作業を止めて報告すること。**

### 2.1 禁止事項

| # | 規則 | 根拠 |
|---|---|---|
| G-1 | `SceneLifecycleManager` の遷移規則を変更・緩和しない | 遷移検証器は H-1 を検出した功労者。例外を消すために検証を緩めるのは症状隠しである |
| G-2 | テストをグリーンにするためにテストを弱めない（アサート削除・Timeout 付与・Ignore 化）。テスト自体が誤っている場合は修正理由を添えて報告する | AI 作業で最も起きやすい不正解パターン |
| G-3 | 画面遷移（`SwitchScene` / `GoBack` / `TransitionPlan`）の挙動を変えない | ストリーミングは `AddScene` / `UnloadScene` のみを使う（正典 D-5） |
| G-4 | セル identity（`Cell_` プレフィックス）を SwitchScene / 履歴 / TransitionPlan に乗せない | 履歴汚染防止（正典 R-3）。T-04 でバリデータを入れる |
| G-5 | `docs/planning/` の計画書・本書を実装の都合で書き換えない | 判定基準の事後改変防止 |

### 2.2 `SceneDirector.Loading.cs` の不変条件

このファイルは in-flight 辞書 3 層 + PoNR + 巻き戻しリストが絡む最も壊れやすい場所。
以下の不変条件はコードのどこにも一箇所でまとまって書かれていないため、ここに明文化する。

| # | 不変条件 | 破った場合の症状 |
|---|---|---|
| I-1 | `ScenePair.LoadCts` は「生きている CTS」か null のみ。**Dispose 済み CTS を指す状態を一瞬も作らない** | 後続 `UnloadScene` / `CollectLoadedDescendants` が `ObjectDisposedException`（2026-07-06 の事後修正はこれ。非 OCE 例外経路が漏れていた） |
| I-2 | in-flight 辞書（`_inFlightAddScenes` / `_inFlightSceneBaseLoads` / `_inFlightUnitySceneLoads`）のエントリは成功・OCE・非 OCE の**全経路で必ず除去**する | 該当 identity が永久にロード不能になる |
| I-3 | PoNR（Unity Scene ロード開始）通過後は外部キャンセル不可。`LoadCts` の null クリアが PoNR 通過の印 | キャンセル窓の意味論崩壊 |
| I-4 | `newlyCreatedScenes` の巻き戻し（キャンセル時クリーンアップ）は子→親の**逆順** | 親が先に消えて子のクリーンアップが浮く |
| I-5 | 同一 identity で in-flight に合流した後発 `AddScene` の `afterOnLoadedTask` / `context` / `progress` は**無視される**（仕様。正典 T-02 完了記録） | — |

### 2.3 Controller 設計の前提（T-06 以降で最重要）

> **G-6: `AddScene` の await 完了は「Stable 到達」を保証しない。**
> 先発がキャンセルされた場合、合流した後発は**シーン未ロードのまま正常終了**し得る（OCE の場合もある）。
> `WorldStreamingController` は AddScene / UnloadScene の完了を信頼して current 集合を確定させてはならず、
> **Tick ごとの再照合（実際のロード状態の観測 → desired との差分再発行）で収束させる**こと。
> この再照合こそが「あるべき集合への収束」の生命線であり、T-06 / T-06.5 のテスト対象である。

---

## 3. チケット別施行表

### T-03: `AddScene` への priority / テレメトリレベル公開（H-2, H-3）

| 項目 | 内容 |
|---|---|
| 目的 | Controller が距離順の優先度とセル用テレメトリレベル（Verbose）を渡せるようにする |
| 変更対象 | `SceneDirector.Loading.cs`（AddScene / PerformUnitySceneLoad）、`SceneDirector.Unloading.cs`（UnloadScene）、`TestableSceneDirector.cs` |
| 受入条件 | 既存呼び出しの挙動不変（既定値で従来と同一）。新引数が末端まで伝搬する |

**API 変更（シグネチャ確定）:**

- `AddScene(..., int priority = 100, TelemetryLevel telemetryLevel = TelemetryLevel.Summary)`
- `UnloadScene(..., TelemetryLevel telemetryLevel = TelemetryLevel.Summary)`
- `protected virtual PerformUnitySceneLoad(string, SceneResource, int priority)` — 現行の `priority: 100` ハードコード（`SceneLoadOptions`）を引数化

**レッドテスト（`Tests/Scene/SceneDirectorLoadOptionsTests.cs`）:**

| テスト名 | 検証内容 | スタブ段階での期待失敗 |
|---|---|---|
| `AddScene_PriorityArgument_ReachesUnitySceneLoad` | `priority: 10` → TestableSceneDirector が記録した priority == 10 | 100 が記録されて失敗 |
| `AddScene_DefaultPriority_Is100` | 引数省略 → 記録 priority == 100 | グリーン（回帰検知用） |
| `AddScene_Priority_AppliedToParentLoad` | 子を priority 指定でロード → 親のロードにも同じ priority | 100 が記録されて失敗 |
| `AddScene_TelemetryLevelVerbose_EmitsVerboseRecord` | FakeSink で SceneLoad レコードの Level == Verbose | Summary が記録されて失敗 |
| `AddScene_TelemetryLevelDefault_RemainsSummary` | 既定 → Summary | グリーン（回帰検知用） |
| `UnloadScene_TelemetryLevelVerbose_EmitsVerboseRecord` | UnloadScene 側も同様 | Summary が記録されて失敗 |

**テスト実装の注意:**

- `AppTelemetry` は static。テストでは `AppTelemetry.Level` を SetUp で退避し TearDown で必ず復元、FakeSink は `AddSink` / `RemoveSink` を対で呼ぶ。**テスト間汚染は SceneSystem 全体を不安定化させるため復元漏れ厳禁**
- FakeSink は `ITelemetrySink`（`Write` / `Flush` / `Dispose`）を実装し、受信した `TelemetryRecord` をリストに溜めるだけの実装とする。`TelemetryRecord` の Level / StartType プロパティ名は実物を確認すること
- priority の記録は `TestableSceneDirector` に `Dictionary<string, int> LastLoadPriorities` を追加して行う

**実装ヒント:** priority は `AddSceneCore` → `LoadUnityScene` → `PerformUnitySceneLoad` へ引数で伝搬（親ロードにも同値を適用）。telemetryLevel は `FinishSpan(..., level)` の引数に渡すだけ。in-flight 合流時は I-5 と同じ扱い（後発の priority / telemetryLevel は無視、仕様として正典に追記）。

---

### T-04: CellScene 基底 + セルテンプレート + セル identity バリデータ

| 項目 | 内容 |
|---|---|
| 目的 | セルの構造的制約（R-1/R-2）を基底クラスで強制し、R-3 の検証を「将来」から**本チケットへ繰り上げ**る |
| 新規 | `Runtime/SceneSystem/Cells/CellScene.cs`、`CellIdentity.cs`（純 C# ユーティリティ）、セルシーンテンプレート |
| 受入条件 | R-1/R-2 が構造的に守られる。`Cell_` identity の検証関数が存在しテスト済み |

**設計制約（正典 T-04 の再掲 + 強化）:**

- `CellScene` は **セル座標・バウンズのメタデータ運搬のみ**。距離判定・ロード判断のロジックを持たせたら即レビュー差し戻し（正典 D-3、却下案 1 の再侵入防止）
- UIView 検索を行わない（R-2）。`LoadingDisplayType` は常に None 前提（R-4）

**レッドテスト（`Tests/Scene/CellSceneTests.cs`）:**

| テスト名 | 検証内容 |
|---|---|
| `CellScene_ParsesCellCoordinate_FromIdentity` | `Cell_3_5` → 座標 (3, 5) が取得できる |
| `CellIdentity_IsCellId_Detection` | `Cell_0_0` は true、`Title` / `Cell_x` / `CellFoo` は false |
| `CellScene_Load_DoesNotRegisterUIView` | CellScene ロード後、UICommon に UIView が登録されない（R-2 構造的検証） |
| `CellScene_Bounds_ComputedFromGridConfig` | グリッド定義（原点・セルサイズ）から正しいワールドバウンズを返す |

**バリデータ（R-3 繰り上げ分）:** `CellIdentity.IsCellId(identity)` を公開し、SwitchScene / TransitionPlan 系のエントリポイントでセル identity を検出したら `InvalidOperationException` を投げるガードを追加する。ガード追加は画面遷移の**正常系挙動を変えない**こと（G-3。セル identity を渡すのは元々未定義動作であり、これを明示的な失敗に変えるのは許容）。ガードのテストも1本書く。

---

### T-05: World Cell Generator（エディタツール）

| 項目 | 内容 |
|---|---|
| 目的 | グリッド定義から N×N のセルシーン + SceneResource + Map 登録を量産する |
| 新規 | `Editor/Streaming/WorldCellGenerator.cs`、`WorldGridDefinition.cs`（ScriptableObject: 原点・セルサイズ・N×N・命名規則） |
| 流用 | `HpGaugeSliceSceneCreator`（シーン生成）、`SceneResourceGenerator` |
| 受入条件 | 生成物が下記テストを満たす。**再実行しても壊れない（冪等）** |

**TDD 方針:** エディタツールは UI 部分と生成ロジックを分離し、**生成ロジックを純関数（入力: グリッド定義、出力: 生成計画）にしてテストする**。シーンファイル I/O 自体はテスト対象外（手動確認 + T-07 で検証される）。

**受入テスト（`Tests/Editor/WorldCellGeneratorTests.cs`）:**

| テスト名 | 検証内容 |
|---|---|
| `Generate_GridDefinition_ProducesNxNResources` | 3×3 定義 → SceneResource 9 個 |
| `Generate_AllCells_AreOnDemandChildrenOfWorld` | 全セルの Parent == World、LoadType == OnDemand |
| `Generate_Naming_FollowsCellXYConvention` | 命名が `Cell_{x}_{y}` に一致 |
| `Generate_RunTwice_IsIdempotent` | 2 回実行 → 重複登録なし・差分なし |
| `Generate_RegistersAllCells_ToSceneResourceMap` | Map から全セルが引ける |

---

### T-06: `ISceneStreamingBackend` + `WorldStreamingController`

| 項目 | 内容 |
|---|---|
| 目的 | ポリシー層（desired set 計算・差分発火・ヒステリシス・in-flight 上限・priority）を純 C# で実装 |
| 新規 | `Runtime/Streaming/ISceneStreamingBackend.cs`、`WorldStreamingController.cs`、`StreamingConfig.cs`、`Tests/Streaming/FakeStreamingBackend.cs` |
| 受入条件 | FakeBackend による純 C# テストで下記全項目を検証。MonoBehaviour / Unity API 非依存 |

**API 確定事項（本書で固定。変更が必要なら報告してから）:**

```csharp
public interface ISceneStreamingBackend
{
    /// <summary>セルのロードを要求する。完了はセルの Stable 到達を保証しない（G-6）。</summary>
    UniTask RequestAdd(string cellId, int priority);
    /// <summary>セルのアンロードを要求する。窓内キャンセル/保留はバックエンド側で収束する。</summary>
    UniTask RequestRemove(string cellId);
    /// <summary>現在ロード済み（Stable）のセル集合を観測する。再照合（G-6）の入力。</summary>
    bool IsLoaded(string cellId);
}
```

- Controller は純 C#、`Tick(Vector3 focusPosition)` を**外部から手動駆動**する（UpdateSystem への接続は薄いアダプタとして分離し、テストでは Tick を直接呼ぶ）
- 時刻・乱数・フレームに依存しない。Tick 間引き（5Hz / 1/4 セル移動）はアダプタ側の責務とし、Controller 本体は「呼ばれたら差分計算」のみ
- current 集合は Controller 内部で追跡しつつ、**毎 Tick `IsLoaded` で実状態と突き合わせて自己修復する**（G-6）

**レッドテスト（`Tests/Streaming/WorldStreamingControllerTests.cs`、FakeBackend 使用・全て同期的に決定的）:**

| テスト名 | 検証内容 |
|---|---|
| `Tick_FocusInGrid_RequestsCellsWithinLoadRadius` | desired set = ロード半径内のセルが RequestAdd される |
| `Tick_CellBeyondUnloadRadius_IsRemoved` | アンロード半径外のロード済みセルが RequestRemove される |
| `Tick_CellBetweenRadii_IsRetained` | ロード半径外・アンロード半径内のセルは**何も発火しない**（ヒステリシス） |
| `Tick_SameFocusTwice_NoRedundantRequests` | 同一 focus で 2 回 Tick → バックエンド呼び出しが増えない（差分発火） |
| `Tick_LoadRequests_OrderedByDistanceToFocus` | RequestAdd の発行順（または priority 値）が focus に近い順 |
| `Tick_InFlightLimit_Respected` | maxInFlight=2 のとき未完了 RequestAdd が 2 件を超えない。1 件完了で次が発行される |
| `Tick_QueuedCellLeavesDesired_IsNotIssued` | キュー待ちセルが desired から外れたら発行されない（キュー取り消し） |
| `Tick_AddCompletedButNotLoaded_ReissuesNextTick` | RequestAdd 正常終了だが IsLoaded=false → 次 Tick で再発行（**G-6 の再照合**） |
| `Tick_FocusMovesDuringInFlight_ConvergesToDesired` | in-flight 中に focus 移動 → 最終的に desired と一致 |

**FakeBackend の要件:** RequestAdd/RequestRemove を即時完了 or 手動完了（`UniTaskCompletionSource` ゲート）に切り替え可能とし、呼び出し履歴（順序・priority 込み）を記録する。「完了したがロードされていない」状態（G-6）を作れること。

---

### T-06.5: Controller × 本物 SceneDirector 統合テスト（**新設チケット**）

| 項目 | 内容 |
|---|---|
| 目的 | H-1/H-4 が壊れていた「ポリシーとメカニズムの継ぎ目」を EditMode テストで押さえる。FakeBackend では再現できない SceneDirector の実挙動（キャンセル収束・保留アンロード・合流意味論）と Controller の相互作用を検証する |
| 新規 | `Runtime/Streaming/SceneDirectorStreamingBackend.cs`（ISceneStreamingBackend の本実装）、`Tests/Streaming/StreamingIntegrationTests.cs` |
| 構成 | `TestableSceneDirector` + World 親シーン + `Cell_x_y` の SceneResource 群（`SceneDirectorTestBase` のセットアップヘルパーを拡張） |
| 受入条件 | 下記テストが全グリーン。A-3 / A-5 の EditMode 版に相当 |

| テスト名 | 検証内容 |
|---|---|
| `Traversal_FocusSweepsGrid_ResidentSetMatchesDesired` | focus をグリッド横断させ Tick を繰り返す → 最終常駐セル集合 == desired（A-3 相当） |
| `FastTraversal_CancelDuringLoad_NoExceptionAndConverges` | SceneLoadGates でロードを保留中に focus 通過 → キャンセル/保留アンロードで収束、例外 0（A-5 相当） |
| `JoinedAdd_AfterLeaderCanceled_EventuallyLoads` | 先発キャンセルで合流側が未ロード終了 → 再照合により最終的にロードされる（G-6 の実機検証） |
| `WorldUnload_RemovesAllCells_AndControllerRestarts` | World 親の UnloadScene で全セル再帰破棄 → Controller 再開後に desired が復元される（InGame 退出/再入場） |
| `PendingUnload_CellStabilizes_ThenAutoUnloads` | PoNR 通過後に desired から外れたセルが Stable 到達後に自動アンロードされる |

**注意:** このチケットの失敗は Controller 側でなく SceneDirector 側の欠陥を示している可能性がある。その場合は G-1/G-5 に従い、修正方針を報告してから着手する。

---

### T-07: 実証スライス（10×10 グリッド + フライスルー）

Editor Play 主体で TDD 対象外。ただし以下を守る:

- T-06.5 のテストが通っている限り、ここで新たに発見される問題は「実 Addressables / 実シーン / フレームタイム」起因に限定されるはず。ロジック不具合が出たら T-06 / T-06.5 へテストを追加してから直す（**Play で直接デバッグして直さない**）
- 目視チェックリスト: セルの出入りが focus に追従 / 高速横断でエラーログ 0 / 横断往復後のセル集合一致（DebugStudio or ログで確認）
- 可能なら A-3 / A-5 検証を PlayMode テスト化する（判断は実装 Agent に委ねる。無理に自動化せず、チェックリスト実施記録を正典へ残すのでも可）

### T-08: テレメトリカウンタ

- Controller が emit するカウンタ（常駐セル数 / in-flight 数 / キャンセル発生数 / 保留アンロード発生数）は**純 C# でテスト可能にする**（emit 先をインターフェース化 or 値を公開プロパティ化して Tick 後にアサート）
- 受け入れ条件 A-1〜A-5 の実測基準値をこのチケットで確定させ、正典 §9 の「目安」を実数に置換する

### T-09: 受け入れ判定

- 記録のみ。A-1〜A-5 の判定結果と撤退判断（正典 §11）を正典へ追記する

---

## 4. 依存関係と推奨順序

```
T-03 ──→ T-06 ──→ T-06.5 ──→ T-07 ──→ T-08 ──→ T-09
T-04 ──→ T-05 ──────┘ (T-06.5 はセル SceneResource 構成を使う)
```

- T-03 と T-04 は独立・並行可
- T-06 は FakeBackend のみで進むため T-04/T-05 と独立に開始可能（priority を使うため T-03 には依存）
- T-06.5 が全ての合流点。ここが最重要の品質ゲート

## 5. 既知の落とし穴（過去の実績から）

| 症状 | 原因と対処 |
|---|---|
| バッチテストが exit code -1073741819 でクラッシュ | `-nographics` を付ける |
| `Already continuation registered` 例外 | `UniTask` は既定で二重 await 不可。複数 awaiter は `UniTaskCompletionSource` を使う（`.Preserve()` の Status 参照も罠） |
| テスト 0 件実行で「成功」に見える | コンパイルエラー。ログ末尾の CS エラーを確認 |
| テストがタイムアウトでハング | キャンセル待ちのデッドロック。`UniTask.WaitUntilCanceled` を使うテストはゲートの完了漏れを疑う |
| PowerShell で `&&` が構文エラー | `;` で連結する |
| **無関係なテストが「Unhandled log message: [Exception] ...」で散発的に落ちる** | 誰にも await されない `UniTaskCompletionSource` に `TrySetException` した非 OCE 例外が、GC 時に `ExceptionHolder` ファイナライザ経由で `Debug.LogException` を発行している（未観測例外）。発火タイミングが GC 依存のため**別のテストに濡れ衣がかかる**。in-flight 共有のように「複製した例外」を通知へ載せる場合は、通知側を即座に観測して破棄すること（`SceneDirector.Loading.cs` の `ObserveInFlightException` 参照。2026-07-06 に実際に発生） |
