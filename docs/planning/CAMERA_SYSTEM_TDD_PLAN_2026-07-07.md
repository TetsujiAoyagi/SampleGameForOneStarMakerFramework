# CameraSystem TDD 施行計画 (CAM-01〜CAM-10) — 2026-07-07

> 設計の正典は `unity/Assets/Docs/Architecture/23-camera-system.md`（以下「正典」）。
> 本書はチケットを他の Agent へ委任するための **TDD 施行表 + 実装ガードレール**。
> 形式・運用は `SCENE_STREAMING_TDD_PLAN_2026-07-06.md` に倣う。

---

## 0. 運用ルール（人間 + 複数 Agent での回し方）

| 役割 | 担当 | 備考 |
|---|---|---|
| レッドテスト作成 | 安価モデル | 本書のチケット別テスト仕様に従う。**コンパイルは必ず通す**（§1.1） |
| テストレビュー | 上位モデル | テストが仕様を正しく表現しているか・弱いアサートがないかを検査 |
| 実装（グリーン化） | 実装担当 Agent | 本書 §2 のガードレール厳守 |
| 完了判定 | 上位モデル or 人間 | §1.3 の完了条件で判定し、正典 §11 へ完了記録を追記 |

- 1 チケット = 1 コミット推奨。CAM-01（パッケージ導入）と CAM-02（純 C# 数学）は他と独立に価値があるため特に分離すること
- チケット完了ごとに正典 §11 の該当行へ ✅ と完了記録を追記する（21-scene-streaming.md §10 の記録形式に倣う）
- セッションを跨ぐ場合は本書と正典の完了記録だけで文脈復元できる状態を保つ

---

## 1. 全チケット共通プロトコル

### 1.1 TDD サイクル（Unity 特有の制約込み）

SceneStreaming 施行表 §1.1 と同一。要点:

1. **新規クラスのチケット**（CAM-02〜CAM-05, CAM-07）: スケルトン（`NotImplementedException` 可）とテストを同時に作成 → 全テストがレッド → 実装 → グリーン
2. **既存 API 拡張を伴うチケット**（CAM-08）: 先にシグネチャだけ追加する（既定値で従来挙動）→ 挙動でレッド → 実装 → グリーン
3. レッド状態のテストは Ignore にせずレッドのままコミットしてよい。コミットメッセージと正典に「意図されたレッド」であることを明記する

### 1.2 テスト実行（実証済みコマンド）

Unity エディタを閉じた状態で PowerShell から実行する。`&&` は使えないので `;` で連結。`-nographics` 必須。

```powershell
& "D:\UnityEditor\6000.5.0f1\Editor\Unity.exe" -batchmode -nographics `
  -projectPath "D:\repositories\unity\SampleGameForOneStarMakerFramework\unity" `
  -runTests -testPlatform EditMode `
  -testFilter "OneStarMaker.Tests.CameraSystem" `
  -testResults "D:\repositories\unity\SampleGameForOneStarMakerFramework\test-results-camerasystem.xml" `
  -logFile "D:\repositories\unity\SampleGameForOneStarMakerFramework\unity-test-run-camerasystem.log" | Out-Null
```

- テスト namespace は `OneStarMaker.Tests.CameraSystem`、配置は `Tests/Camera/`
- 全体回帰は `-testFilter "OneStarMaker.Tests"`、結果は `test-results.xml` へ
- ベースライン: 217 本グリーン（2026-07-06 時点）。**着手時に全体を一度回して実数を再計測し、本書のこの行を更新すること**
- **テスト結果が「コンパイルエラーで 0 件実行」になっていないか必ず確認する**。ログ末尾に compile error があれば作業は未完了

### 1.3 完了条件（全チケット共通）

1. チケットのレッドテストが全てグリーン
2. `OneStarMaker.Tests` 全体で回帰ゼロ
3. 正典 §11 へ完了記録を追記（テスト本数・検証結果・割り切り事項）

---

## 2. 絶対制約（実装 Agent 向けガードレール）

**違反が必要に見えた時点で作業を止めて報告すること。**

### 2.1 禁止事項

| # | 規則 | 根拠 |
|---|---|---|
| G-1 | ポリシー層（`Runtime/CameraSystem/` のスタック・Modifier・Pose/Snapshot・Volume weight 計算）に `UnityEngine.Camera` / Cinemachine 型 / MonoBehaviour への参照を持ち込まない。許可されるのは数学型（`Vector3` / `Quaternion` / `Plane` 等）と `VolumeProfile` 等のアセット参照ハンドルのみ | 正典 D-6。純 C# テスト可能性と撤退ライン（正典 §12）の生命線 |
| G-2 | Cinemachine の Priority / Channel 値・`CinemachineCamera` 型を `ICameraSystem` / `ICameraView` / `LogicalCamera` の公開 API に露出しない（F-9 のラップ登録引数を唯一の例外とし、受け取った瞬間に内部表現へ変換する） | 正典 D-5。Priority 調整合戦の構造的排除 |
| G-3 | テストをグリーンにするためにテストを弱めない（アサート削除・Timeout 付与・Ignore 化）。テスト自体が誤っている場合は修正理由を添えて報告する | AI 作業で最も起きやすい不正解パターン |
| G-4 | `WorldStreamingController` の既存挙動・既存テストを変更しない。複数注視点対応（CAM-08）は加算的拡張（既定値で従来挙動）に限る | SceneStreaming は受け入れ判定済みの資産。ストリーミング施行表 G-5 と同趣旨 |
| G-5 | `docs/planning/` の計画書・本書を実装の都合で書き換えない | 判定基準の事後改変防止 |
| G-6 | 汎用の優先度スタックフレームワークを新設しない（UISystem / UpdateSystem との共通化をしない） | 正典 §3.2 却下案。時期尚早な抽象化 |

### 2.2 更新順序の不変条件（CAM-06 以降で最重要）

Cinemachine Brain は LateUpdate 駆動。以下の順序をフレーム内で保証すること（正典 §7 制約）。

| # | 不変条件 | 破った場合の症状 |
|---|---|---|
| I-1 | 1 フレームの処理順は **Brain 更新（ブレンド済み POV 確定）→ Modifier 適用 → Snapshot 確定** | シェイクが 1 フレーム遅れて二重像に見える / Snapshot がシェイク前の値を返しストリーミング注視点が揺れない |
| I-2 | Modifier は実 Camera の Transform を直接恒久変更しない。毎フレーム「Brain 出力 + Modifier 合成」を適用する（加算の蓄積禁止） | シェイクの原点ドリフト。UE CameraModifier と同じ規律 |
| I-3 | `Snapshot` / `IncomingSnapshot` は同一フレーム内で自己一貫（途中状態を観測させない） | ストリーミング先読みが不正な位置を読む |
| I-4 | ハンドル（`CameraStackHandle` / `CameraModifierHandle`）の Dispose は冪等。所有 View 破棄後の Dispose も安全（no-op） | カットシーン終了処理と View 破棄の順序でクラッシュ |

### 2.3 asmdef / パッケージの制約

- Cinemachine 参照は `OneStarMaker.Runtime.asmdef` へ追加する（Foundation には追加しない）
- ポリシー層の Cinemachine 非依存（G-1）はフォルダ規約 + レビューで守る（asmdef 分割はしない。分割が必要に見えたら報告）
- `OneStarMaker.Tests.asmdef` に Cinemachine 参照を追加してよいのは CAM-06 のテストのみが理由になる場合

---

## 3. チケット別施行表

### CAM-01: Cinemachine パッケージ導入 + asmdef 配線

| 項目 | 内容 |
|---|---|
| 目的 | Cinemachine（3 系以降、`OutputChannels` を持つ最新安定版）を導入し、コンパイルと既存テストの無傷を確認する |
| 変更対象 | `Packages/manifest.json`、`OneStarMaker.Runtime.asmdef` |
| 受入条件 | エディタ起動・バッチテストともにコンパイル成功。`OneStarMaker.Tests` 全体回帰ゼロ |

TDD 対象外（テストなし）。導入バージョンと選定理由（`OutputChannels` の有無を確認したこと）を正典 §11 の完了記録へ残す。

---

### CAM-02: `CameraPose` / フラスタム計算 / `CameraViewSnapshot`（純 C#）

| 項目 | 内容 |
|---|---|
| 目的 | POV とフラスタム 6 平面の計算を Unity Camera 非依存で実装する（F-7 の基盤） |
| 新規 | `Runtime/CameraSystem/CameraPose.cs`、`CameraFrustum.cs`、`CameraViewSnapshot.cs`、`CameraBlendSpec.cs` |
| 受入条件 | 下記テスト全グリーン。`GeometryUtility` 非使用 |

**レッドテスト（`Tests/Camera/CameraFrustumTests.cs`）:**

| テスト名 | 検証内容 |
|---|---|
| `Frustum_PointInFrontWithinFov_IsInside` | 正面・FOV 内・near/far 間の点が内側判定 |
| `Frustum_PointBehindCamera_IsOutside` | 背後の点が外側 |
| `Frustum_PointBeyondFarPlane_IsOutside` | far 超の点が外側 |
| `Frustum_PointNearerThanNearPlane_IsOutside` | near 未満の点が外側 |
| `Frustum_AspectAffectsHorizontalPlanes` | アスペクト比 2.0 で水平方向の視野が広がる（同一点の内外が変わる） |
| `Frustum_RotatedPose_PlanesFollow` | 回転した POV でフラスタムが追従する |
| `Frustum_MatchesUnityGeometryUtility` | 同一パラメータの実 Camera から `GeometryUtility.CalculateFrustumPlanes` で得た 6 平面と法線・距離が誤差内で一致（**テスト側のみ** Unity Camera を使うクロスチェック。EditMode） |
| `Snapshot_ContainsPoseAndVelocity` | 2 つの POV と dt から速度が計算される |

**実装ヒント:** 平面は `UnityEngine.Plane`（数学型なので G-1 適合）。クロスチェックテストの許容誤差は 1e-4 程度から始め、実測で調整理由を記録する。

---

### CAM-03: レイヤー×スタックポリシー + ハンドル

| 項目 | 内容 |
|---|---|
| 目的 | View 内のアクティブカメラ決定（F-1, F-10）を純 C# で実装する |
| 新規 | `Runtime/CameraSystem/CameraLayer.cs`、`LogicalCamera.cs`、`CameraStack.cs`（レイヤー×スタック + 勝者決定）、`CameraStackHandle.cs` |
| 受入条件 | 下記テスト全グリーン。MonoBehaviour / バックエンド非依存（勝者変更はイベントまたは戻り値で観測） |

**レッドテスト（`Tests/Camera/CameraStackTests.cs`）:**

| テスト名 | 検証内容 |
|---|---|
| `Push_EmptyStack_BecomesActive` | 最初の Push でアクティブになる |
| `Push_SameLayer_TopWins` | 同レイヤー 2 枚 → 後勝ち |
| `Push_HigherLayer_WinsOverLowerStackTop` | Gameplay 2 枚の上に Cutscene 1 枚 → Cutscene が勝つ |
| `Dispose_Top_RestoresPrevious` | トップの Dispose で直下（または下位レイヤーのトップ）へ復帰 |
| `Dispose_NonTop_RemovesWithoutActiveChange` | スタック中間のハンドル Dispose → アクティブ不変・勝者変更通知なし |
| `Dispose_Twice_IsIdempotent` | 二重 Dispose が安全（I-4） |
| `AllStacksEmpty_FallbackCameraActive` | 全レイヤー空 → フォールバックカメラがアクティブ（F-10） |
| `Push_ActiveChange_ReportsBlendSpec` | 勝者変更の通知に Push 時指定の `CameraBlendSpec` が乗る |
| `Dispose_ActiveChange_UsesDepartingCameraBlendSpec` | Pop による復帰時のブレンド仕様の出所が仕様どおり（退場カメラの Push 時指定を使う） |

---

### CAM-04: Modifier スタック

| 項目 | 内容 |
|---|---|
| 目的 | 最終 POV への加算修飾（F-6）を純 C# で実装する |
| 新規 | `Runtime/CameraSystem/ICameraPoseModifier.cs`、`CameraModifierStack.cs`、`CameraModifierHandle.cs`、参考実装 `ShakeModifier.cs`（減衰付き） |
| 受入条件 | 下記テスト全グリーン |

**レッドテスト（`Tests/Camera/CameraModifierTests.cs`）:**

| テスト名 | 検証内容 |
|---|---|
| `Apply_ModifiersRunInRegistrationOrder` | 登録順に適用される（オフセット 2 つの合成結果で検証） |
| `Apply_ReturnsFalse_ModifierAutoRemoved` | `Apply` が false を返した Modifier は次フレームから呼ばれない |
| `Handle_Dispose_RemovesModifier` | ハンドル Dispose で即時除去（二重 Dispose 安全） |
| `Apply_DoesNotAccumulate_BasePoseReappliedEachFrame` | 同一入力 POV に対し複数フレーム適用しても結果が発散しない（I-2） |
| `ShakeModifier_DecaysToZero_ThenSelfRemoves` | 参考実装が減衰完了で自己除去する |

---

### CAM-05: `ICameraBackend` + FakeBackend + `CameraView` / `CameraSystem` 結合

| 項目 | 内容 |
|---|---|
| 目的 | ポリシー一式（スタック + Modifier + Snapshot + Volume weight は CAM-07 で接続）をバックエンド抽象の上で結合する（F-2, F-4, F-7, F-8 のポリシー側） |
| 新規 | `Runtime/CameraSystem/ICameraBackend.cs`（正典 §6 の形状で確定）、`CameraView.cs`、`CameraSystem.cs`、`CameraViewConfig.cs`、`Tests/Camera/FakeCameraBackend.cs` |
| 受入条件 | 下記テスト全グリーン。`Tick(float deltaTime)` を外部から手動駆動（UpdateSystem 接続は CAM-06 のアダプタ） |

**API 確定事項（本書で固定。変更が必要なら報告してから）:** 正典 §6 のスケッチを正とする。`ViewId` は struct の不透明 ID。

**レッドテスト（`Tests/Camera/CameraViewTests.cs`、FakeBackend 使用・全て同期的に決定的）:**

| テスト名 | 検証内容 |
|---|---|
| `Push_WinnerChanged_BackendReceivesSetActiveWithBlend` | 勝者変更時のみ `SetActiveCamera` が呼ばれ、BlendSpec が伝わる |
| `Push_NonWinning_NoBackendCall` | 勝者が変わらない Push（下位レイヤーへの追加）でバックエンド呼び出しなし（差分発火） |
| `Tick_Snapshot_ReflectsBackendCurrentPose` | FakeBackend の `GetCurrentPose` 値が Snapshot に反映される |
| `Tick_Blending_IncomingSnapshotExposed` | `IsBlending=true` の間 `IncomingSnapshot` が遷移先 POV（`GetCameraPose`）を返す（F-8） |
| `Tick_NotBlending_IncomingSnapshotIsNull` | ブレンド終了後は null |
| `Tick_ModifierApplied_AfterPoseObservation` | Modifier 合成結果が `ApplyPostModifier` へ渡り、Snapshot は合成後の値（I-1, I-3） |
| `Tick_Velocity_ComputedAcrossTicks` | 連続 Tick で速度が (Δpos / dt) になる |
| `CreateView_RenderTextureConfig_HeldByView` | RT 出力設定が View に保持される（F-4。実描画は CAM-06） |
| `ReleaseView_HandleDisposeAfterRelease_IsSafe` | View 破棄後のハンドル Dispose が no-op（I-4） |

**FakeBackend の要件:** 呼び出し履歴（View・カメラ・BlendSpec）を記録し、`GetCurrentPose` / `GetCameraPose` / `IsBlending` を任意に設定可能とする。

---

### CAM-06: `CinemachineCameraBackend` + `CameraSystemHost` + UpdateSystem アダプタ

| 項目 | 内容 |
|---|---|
| 目的 | 本物メカニズムへの翻訳（正典 §7 の対応表）。独自ヒエラルキーの成立 |
| 新規 | `Runtime/CameraSystem/Cinemachine/CinemachineCameraBackend.cs`、`CameraSystemHost.cs`（DontDestroyOnLoad、Initialize パターン）、UpdateSystem 接続アダプタ |
| 受入条件 | 下記 EditMode テスト全グリーン + Play でのブレンド目視確認（記録を正典へ） |

**レッドテスト（`Tests/Camera/CinemachineBackendTests.cs`、EditMode。GameObject 生成可）:**

| テスト名 | 検証内容 |
|---|---|
| `CreateView_AssignsUniqueChannel_PerView` | View 毎に Brain の `ChannelMask` が一意に割当てられる |
| `CreateView_ExceedsChannelCapacity_Throws` | Channel 枯渇（15 + Default）で明示的例外 |
| `SetActiveCamera_OnlyWinnerEnabled_OnItsChannel` | 勝者の CinemachineCamera のみ当該 Channel で有効。同 View の他カメラは無効 |
| `SetActiveCamera_OtherViewCameras_Unaffected` | View A の切替が View B の有効状態を変えない（CA-2 の単体版） |
| `WrapSceneAuthoredCamera_RegistersAsLogicalCamera` | シーン配置済み CinemachineCamera のラップ登録（F-9）。Channel がラップ時に View のものへ書き換わる |
| `Host_CreatesHierarchy_UnderDontDestroyOnLoad` | Host 配下に View_Main の Camera + Brain が生成される |
| `RtView_UpdateFrequency_SkipsFrames` | N フレーム毎指定の RT View が指定間隔でのみレンダリング要求する（F-5。実描画でなく要求回数を検証） |

**実装ヒント:** Brain 更新後の Modifier 適用（I-1）は `CinemachineCore.CameraUpdatedEvent`（または同等のフック）を起点にする。フックの選定理由と順序保証の根拠を完了記録に書くこと。ブレンド補間値の妥当性は EditMode で時間駆動が困難なため Play 目視 + CAM-10 テレメトリに委ねる。

---

### CAM-07: Volume weight クロスフェード

| 項目 | 内容 |
|---|---|
| 目的 | 論理カメラ毎 VolumeProfile の weight 補間（F-3、正典 §8） |
| 新規 | `Runtime/CameraSystem/VolumeCrossfade.cs`（純 C#: 入退場カメラと経過時間 → weight ペア）、Host 側の Volume 反映 |
| 受入条件 | weight 計算の純 C# テスト全グリーン + Play 確認 |

**レッドテスト（`Tests/Camera/VolumeCrossfadeTests.cs`）:**

| テスト名 | 検証内容 |
|---|---|
| `Crossfade_MidBlend_WeightsAreComplementary` | t=0.5 で入場 0.5 / 退場 0.5（線形時）。合計 1 維持 |
| `Crossfade_Cut_ImmediatelyFullWeight` | duration 0 → 即座に 1 / 0 |
| `Crossfade_Complete_DepartingProfileReleased` | 完了後、退場側が weight 0 かつ解放対象としてマークされる |
| `Crossfade_InterruptedByNewBlend_StartsFromCurrentWeight` | ブレンド中の再切替が現在 weight から開始（F-2 と整合） |
| `Camera_WithoutProfile_NoCrossfadeEntry` | VolumeProfile を持たない論理カメラで weight 対象が生成されない |

---

### CAM-08: SceneStreaming 注視点供給アダプタ

| 項目 | 内容 |
|---|---|
| 目的 | 全 View + ブレンド先読みの注視点集合を `WorldStreamingController` へ供給する（F-8 消費側、正典 §9） |
| 新規 | `Runtime/Streaming/CameraFocusProvider.cs`（純 C#: `ICameraView` 群 → 注視点集合）。`WorldStreamingController` へ複数 focus 対応の加算的拡張 |
| 変更対象 | `WorldStreamingController.cs`（複数 focus のオーバーロード追加。**既存単一 focus API は挙動不変**） |
| 受入条件 | 下記テスト全グリーン + 既存 `Tests/Streaming/` 全テスト回帰ゼロ（G-4） |

**レッドテスト（`Tests/Streaming/CameraFocusProviderTests.cs` + `WorldStreamingControllerMultiFocusTests.cs`）:**

| テスト名 | 検証内容 |
|---|---|
| `Provider_SingleView_YieldsOnePosition` | View 1 つ → Snapshot 位置 1 点 |
| `Provider_TwoViews_YieldsBothPositions` | 分割画面想定で 2 点 |
| `Provider_Blending_IncludesIncomingPosition` | ブレンド中は `IncomingSnapshot` 位置が加わり 2 点（先読み） |
| `Provider_RtView_ExcludedByConfig` | ミニマップ等の RT View を注視点から除外する設定が効く |
| `Tick_MultiFocus_DesiredSetIsUnion` | 2 focus の desired set = 各 focus の和集合 |
| `Tick_MultiFocus_PriorityUsesNearestFocusDistance` | priority の距離順が「最寄り focus への距離」で計算される |
| `Tick_SingleFocusOverload_BehaviorUnchanged` | 既存 API 経由の結果が従来と完全一致（回帰検知用・スタブ段階でグリーン） |

---

### CAM-09: 実証スライス

Editor Play 主体で TDD 対象外。ただし以下を守る:

- 構成: 分割画面（View×2）+ ミニマップ RT View + カットシーン Push/Pop ブレンド + シェイク Modifier + ストリーミング注視点のカメラ供給切替
- ロジック不具合が出たら該当チケットへテストを追加してから直す（**Play で直接デバッグして直さない**。ストリーミング施行表 T-07 と同じ規律）
- 目視チェックリスト: CA-1〜CA-4（正典 §10）+ ブレンド先読みでロード開始がブレンド完了より早いこと（CA-5 の予備確認）

### CAM-10: テレメトリ + 受け入れ判定

- View 数 / スタック深度 / アクティブカメラ / ブレンド数のカウンタは**純 C# でテスト可能にする**（emit 先インターフェース化 or 公開プロパティ、ストリーミング施行表 T-08 と同方式）
- カメラ切替 span（Push → ブレンド完了、Verbose）を追加
- CA-1〜CA-5 を判定し、結果を正典 §10 の表へ実数とともに追記する

---

## 4. 依存関係と推奨順序

```
CAM-01 ──────────────┐
CAM-02 ──→ CAM-03 ──→ CAM-05 ──→ CAM-06 ──→ CAM-09 ──→ CAM-10
           CAM-04 ──────┘           CAM-07 ────┘
                        CAM-08 ────────────────┘
```

- CAM-02 / CAM-03 / CAM-04 は CAM-01 と独立・並行可（純 C# のため Cinemachine 不要）
- CAM-05 は CAM-03 / CAM-04 に依存。CAM-06 が CAM-01 + CAM-05 の合流点で最重要の品質ゲート
- CAM-08 は CAM-05（Snapshot / IncomingSnapshot）に依存し、CAM-06 とは独立に進められる

## 5. 既知の落とし穴

| 症状 | 原因と対処 |
|---|---|
| バッチテストが exit code -1073741819 でクラッシュ | `-nographics` を付ける |
| テスト 0 件実行で「成功」に見える | コンパイルエラー。ログ末尾の CS エラーを確認 |
| PowerShell で `&&` が構文エラー | `;` で連結する |
| EditMode で CinemachineBrain がブレンドしない | Brain の時間駆動は Play 前提。EditMode では有効化状態・Channel 割当のみ検証し、ブレンドは Play 確認に委ねる（CAM-06 の仕様） |
| シェイクの原点ドリフト | Modifier が Transform を直接蓄積変更している（I-2 違反）。毎フレーム「Brain 出力 + 合成」を適用し直す |
| Snapshot がシェイク前の値を返す | 更新順序違反（I-1）。UpdateSystem 上の駆動順とフック選定を確認 |
| DontDestroyOnLoad 絡みのテスト汚染 | EditMode テストで生成した GameObject は TearDown で必ず破棄。Host のシングルトン再入（二重 Initialize は例外の規約）に注意 |
