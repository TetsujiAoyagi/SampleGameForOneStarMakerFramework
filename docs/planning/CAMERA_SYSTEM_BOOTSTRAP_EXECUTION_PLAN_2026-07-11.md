# CameraSystem Bootstrap 有効化 施行表 — 2026-07-11

> 対象は既存 `CameraSystem` の起動時有効化だけとする。Player 追従、InGame、Gameplay カメラ作成、ストリーミング連携は含めない。
>
> 設計の正典は `unity/Assets/Docs/Architecture/23-camera-system.md`、既存実装の施行履歴は `docs/planning/CAMERA_SYSTEM_TDD_PLAN_2026-07-07.md` を参照する。

## 1. 目的と完了像

アプリ起動後、`DontDestroyOnLoad` 配下に `[CameraSystemHost]` が一つだけ存在し、`View_Main` 用の `Camera` と `CinemachineBrain` が稼働する状態にする。

既存の `SampleScene` と `Title` にある従来型 `Main Camera` は無効化し、CameraSystem と二重描画しないようにする。

## 2. 役割とレビュー運用

| 役割 | 担当 | 責務 |
|---|---|---|
| 実装 | 低コストモデル | チケット単位で実装・テスト・自己確認を行い、変更差分と検証結果を提出する |
| コードレビュー | 上位モデル | 下記のレビュー観点で差分を確認し、指摘をチケットへ返す |
| 修正 | 低コストモデル | 指摘を一件ずつ修正し、変更理由と再検証結果を提出する |
| 最終判定 | 上位モデル | 全チケットの受入条件、差分、テスト結果、Play Mode の証跡を確認して完了と判断する |

### 2.1 レビュー反復手順

1. 実装担当は、一度に一つのチケットだけを変更する。
2. レビュー担当は、コンパイル、ライフサイクル、設計境界、不要変更、テスト妥当性を確認する。
3. 指摘がある場合は「ファイル・該当箇所・問題・修正条件」の形式で返す。
4. 実装担当は指摘外のリファクタリングをせず、修正後に同じ検証を再実行する。
5. レビュー担当が指摘ゼロになるまで 2〜4 を反復する。
6. 全チケット完了後、最終判定担当が統合差分をあらためてレビューする。個別レビュー済みであっても省略しない。

### 2.2 差分提出テンプレート

各チケットで実装担当は次を必ず記載する。

```text
チケット:
変更ファイル:
変更内容:
実行した検証:
結果:
未検証事項・理由:
```

「未検証事項なし」と書く場合も、実行したコマンドまたは Unity 上の確認操作を明記する。

## 3. 実装ガードレール

### 3.1 設計・ライフサイクル

- コンポジションルートは `SampleGame/DependOnAll/AppInitializer.cs` とする。`AbstractApplicationInitializer` を今回のためだけに拡張しない。
- 初期化は `OnServicesInitializing` で一度だけ行う。`CameraSystemHost.Initialize()`、`CinemachineCameraBackend`、`CameraSystem`、`CameraSystemUpdateAdapter` の生成順を崩さない。
- `CameraSystemUpdateAdapter` は既存どおり `LateUpdate` で `CameraSystem.Tick` を呼ぶ。UpdateSystem への統合、独自の Update ループ追加、Tick の二重実行を行わない。
- Host、Backend、System、Adapter の所有者を明確にし、アプリ終了時に Host を安全に解放する。エディタの Play Mode 再突入を含め、Host の二重生成・破棄漏れを防ぐ。
- シーン側に CameraSystem 用のカメラを複製配置しない。CameraSystemHost が作成する `View_Main` だけを使用する。

### 3.2 変更範囲

- 変更候補は `AppInitializer.cs`、`Assets/Scenes/SampleScene.unity`、`SampleGame/OutGame/Title/Title.unity` と、必要最小限のテストに限定する。
- `ICameraSystem` のGame層注入、`GameSceneFactory` の変更、Player実装、Follow/LookAt設定、`CameraSystemSliceSetup`、Streaming配線は対象外。
- `Runtime/CameraSystem` のポリシー層へ `MonoBehaviour`、`UnityEngine.Camera`、Cinemachine 型を持ち込まない。
- 既存の CameraSystem API を都合で変更しない。必要に見えた場合は実装を停止し、レビュー担当へ設計判断を求める。

### 3.3 日本語コメント方針

コメントは処理の逐語説明ではなく、将来変更時に壊れやすい「理由・所有権・順序制約」を日本語で残す。

| コメントを追加する箇所 | 必須の説明 |
|---|---|
| `OnServicesInitializing` のカメラ生成 | なぜこのフックで生成するか、Host が常駐所有されること |
| `CameraSystemUpdateAdapter` の追加 | `LateUpdate` で Tick する理由と二重Tick禁止 |
| 終了時の解放処理 | Host 解放の所有者と、二重解放を安全に扱う意図 |
| シーン上の旧 Main Camera の無効化 | CameraSystemHost の `View_Main` と競合するためであること |

既存コードから自明な代入、型名の言い換え、Unity API の一般説明にはコメントを足さない。コメントと実装が矛盾しないことをレビュー項目に含める。

## 4. チケット施行表

### CAM-BS-01: Bootstrap で CameraSystem を初期化する

| 項目 | 内容 |
|---|---|
| 目的 | アプリ起動時に CameraSystem を一度だけ構築し、`View_Main` を利用可能にする |
| 主な変更先 | `unity/Assets/SampleGame/DependOnAll/AppInitializer.cs` |
| 実装 | `OnServicesInitializing` を override し、Host → Backend → System → Adapter の順で生成する。サービス参照と所有権を `AppInitializer` 側で保持する |
| 禁止 | `AbstractApplicationInitializer` の変更、シーンロードごとの再生成、`CameraSystemSliceSetup` の導入 |
| 日本語コメント | 初期化フックと常駐Hostの所有理由を記す |
| 受入条件 | 起動後に `[CameraSystemHost]` が一つだけ存在し、配下に `View_Main` の Camera と CinemachineBrain が存在する |

実装担当チェック:

- `OnServicesInitializing` が既存初期化順を壊していない。
- `CameraSystemUpdateAdapter.Initialize` に、構築した同一 `CameraSystem` を渡している。
- シーン遷移後も Host が増えない。

レビュー担当チェック:

- フィールド保持が単なる参照逃がしでなく、所有者を表している。
- `CameraSystemHost.Initialize()` の既存シングルトン規約と矛盾しない。
- 例外時に中途半端な再初期化状態を残さない。

### CAM-BS-02: 常駐カメラの解放をライフサイクルに接続する

| 項目 | 内容 |
|---|---|
| 目的 | アプリ終了・Play Mode 終了時にCameraSystemHostを解放し、次回起動へ状態を持ち越さない |
| 主な変更先 | `unity/Assets/SampleGame/DependOnAll/AppInitializer.cs` |
| 実装 | `AppInitializer` が所有するHostを終了通知で解放する。初期化前・解放済みの場合も安全に終了できるようにする |
| 禁止 | 既存の private な `ReleaseAll` への侵入、破棄のためだけの共通Bootstrap API追加 |
| 日本語コメント | 解放責務が `AppInitializer` にある理由と、二重解放を許容する意図を記す |
| 受入条件 | Play Mode を停止・再開しても二重Host、例外、残存Cameraがない |

実装担当チェック:

- `Application.quitting` 等の選択が Unity の実行順と合っている。
- 終了通知の重複登録をしない。
- Host が未生成でも例外にならない。

レビュー担当チェック:

- イベント購読解除または静的状態の残留対策が適切である。
- Editor の Domain Reload 無効設定でも問題を隠していない。

### CAM-BS-03: 既存 Main Camera との競合を解消する

| 項目 | 内容 |
|---|---|
| 目的 | 従来のシーンカメラと `View_Main` の二重描画・AudioListener競合を防ぐ |
| 主な変更先 | `unity/Assets/Scenes/SampleScene.unity`、`unity/Assets/SampleGame/OutGame/Title/Title.unity` |
| 実装 | 既存 `Main Camera` を削除ではなく無効化する。シーンの将来用途を残しつつ、実行中の有効CameraをHostに一本化する |
| 禁止 | CameraSystemHostをシーンに保存すること、UI用Cameraの変更 |
| 日本語コメント | Unityシーンアセットでコメント可能な場合のみ、無効化理由を残す。コメント不能なら本チケット記録を根拠とする |
| 受入条件 | Title表示時に有効なGame CameraがHostの `View_Main` だけであり、Consoleに複数AudioListener警告が出ない |

実装担当チェック:

- 両シーンで `Main Camera` の有効状態を確認する。
- タイトルUIの描画やイベント処理に影響がないことを確認する。

レビュー担当チェック:

- CameraSystemのMainViewが表示先を持たない状態になっていない。
- 変更がCamera以外のGameObjectやシーン設定へ波及していない。

### CAM-BS-04: テストとPlay Modeで有効化を検証する

| 項目 | 内容 |
|---|---|
| 目的 | 既存CameraSystemの回帰がないことと、実行時配線が成立することを確認する |
| 主な対象 | `unity/Assets/OneStarMaker/Tests/Camera/`、Unity Play Mode |
| 実装 | 必要な場合だけBootstrapの寿命管理を検証する小さなテストを追加する。既存テストを弱めたり、Ignore化したりしない |
| 禁止 | Play Modeでの不具合をテストなしで直接修正すること、0件実行のテスト成功扱い |
| 受入条件 | CameraSystem対象EditModeテストと全体EditMode回帰が成功し、Play Modeチェックリストを満たす |

実行コマンド:

```powershell
& "D:\UnityEditor\6000.5.0f1\Editor\Unity.exe" -batchmode -nographics `
  -projectPath "D:\repositories\unity\SampleGameForOneStarMakerFramework\unity" `
  -runTests -testPlatform EditMode `
  -testFilter "OneStarMaker.Tests.CameraSystem" `
  -testResults "D:\repositories\unity\SampleGameForOneStarMakerFramework\test-results-camerasystem-bootstrap.xml" `
  -logFile "D:\repositories\unity\SampleGameForOneStarMakerFramework\unity-test-run-camerasystem-bootstrap.log"
```

全体回帰では `-testFilter "OneStarMaker.Tests"` を使う。結果XMLのテスト件数とログのコンパイルエラーを確認し、0件実行を成功扱いしない。

Play Modeチェックリスト:

- [ ] 起動後に `[CameraSystemHost]` が一つだけある。
- [ ] Host配下に `View_Main`、Camera、CinemachineBrainが生成される。
- [ ] `SampleScene` と `Title` の旧Main Cameraは有効化されていない。
- [ ] Consoleに複数Camera、複数AudioListener、NullReference、二重初期化の警告・例外がない。
- [ ] TitleのUIが表示され、シーン遷移後もHost数が増えない。
- [ ] Play Mode停止後に、次回Playで前回のHostやCameraが残らない。

## 5. 最終レビュー基準

最終判定担当は、各チケットが完了していても次を統合差分で再確認する。

1. 変更が §3.2 の範囲に収まっており、Player追従など次段階の実装を混入させていない。
2. 初期化・Tick・解放の所有者が一貫して `AppInitializer` である。
3. CameraSystem と従来Main Cameraの有効Cameraが同時存在しない。
4. 日本語コメントが §3.3 の必要箇所にあり、実装と矛盾しない。
5. テストログがコンパイルエラーや0件実行を隠していない。
6. Play Modeチェックリストの結果と、残る制約・未検証事項が記録されている。

最終判定が「指摘なし」になるまで実装担当へ差し戻す。指摘ゼロであっても、Player追従が未実装であることを有効化失敗として扱わない。

## 6. 今回の完了後に扱う項目

- InGameシーンとPlayerの正式なライフサイクル設計
- Game層へCinemachine型を露出しない、追従カメラ作成ファサード
- `Follow` / `LookAt` / 三人称構図のCinemachine設定
- `ICameraSystem` の手動DIを通じたGameplayカメラのPush/Pop
- SceneStreamingへのカメラ注視点供給
