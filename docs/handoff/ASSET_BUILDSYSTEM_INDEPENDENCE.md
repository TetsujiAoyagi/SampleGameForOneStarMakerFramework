# AssetManagement / BuildSystem の単独利用と併用（Phase A0 再計画）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。

- type: `program`（A0 の planning packet。実装スライスではない。既存 2 スライス [RUNTIME_REFACTOR_ASSET_CYCLE.md](RUNTIME_REFACTOR_ASSET_CYCLE.md) と [RUNTIME_REFACTOR_BOOTSTRAP.md](RUNTIME_REFACTOR_BOOTSTRAP.md) の改訂と、後続スライスの A0 への入力）
- status: `A`（A0 は閉じた。計画レビュー R1〜R3 反映済み、owner 判断済み（§6）。program 自体は進行中で、**実装キューではない**）
- branch: `claude/runtime-refactor-stage0-plan`（既存 2 文書と同じ計画ブランチ。ASSET_CYCLE の A3 凍結版と同じ commit で収録し、計画 PR で `develop` へ）
- implementation base commit: `d4861882`（`develop` の head。2026-11-07 に `origin/develop` を fetch して確認し、`d4861882` から進んでいないので差は無い）
- implementation head commit: 該当なし（program）
- risk: 各スライスで決める
- owner: root（依頼者）
- created: 2026-11-07
- expires: 2026-11-30、または §6 の owner 判断で各スライスへ取り込まれた時点
- harvest to: `unity/Assets/README.md` の依存図と `27-folder-structure.md`（案 3 の境界: AssetManagement と BuildSystem 本体は互いを参照せず共通契約だけを共有し、両方を知るのは連携部分）。program の完了時（§5.2 段階 4）に反映する
- 関連: ASSET_CYCLE（§4 で採否と改訂案）、BOOTSTRAP（§4 で改訂案）、CameraSystem ⇄ Streaming は別スライス `RUNTIME_REFACTOR_CAMERA_FOCUS`（未作成）のままで、本書は扱わない
- Phase A snapshot の取得案内・状態: 該当なし（A0 は program の地図で、凍結する snapshot を持たない。本書は計画ブランチの commit で固定され、各スライスの snapshot は各 HANDOFF §0 が持つ）
- Phase B result snapshot / evidence bundle / C' blind bundle: 該当なし
- 引継ぎ票:
  - 本書（A0）の状態: 閉じた。D1〜D5 は owner 決定済みで、計画レビュー R1〜R3 の指摘は反映済み。以後、本書は program の地図として後続スライスから参照され、各スライスの状態は各スライスの HANDOFF を正とする。
  - ASSET_CYCLE の状態（2026-11-07 時点）: §4.2 の改訂案を当てた A1 改訂版を作り、A2（アーキテクチャゲート、目的レビュー）を実施し、A3 の採否を整理した。owner は A3 の確認事項 Q1〜Q3 に答え、**A3 凍結を指示した**（2026-11-07。凍結版は本書を含む commit と Phase A snapshot）。次は、計画の文書 PR のマージ後の Phase B。共通契約の folder は `Runtime/AssetBuildContracts/` に決まった。詳しい状態と次の作業は [RUNTIME_REFACTOR_ASSET_CYCLE.md](RUNTIME_REFACTOR_ASSET_CYCLE.md) §0 を正とする。
  - BOOTSTRAP の状態: §4.2 の改訂をまだ当てていない（A1 初稿のまま）。次の作業は、§4.2 の BOOTSTRAP の表を当てる A1 改訂。
  - 必要な権限: なし（文書のみ）。
  - 停止規則: BOOTSTRAP は §4.2 の改訂を当てるまで A2・A3・Phase B を始めない。ASSET_CYCLE の停止規則は同書 §0 に従う。
  - 引き渡さないもの: なし。

### 実測の方法

- 名前付き参照: `grep`（base `d4861882`）。
- **コンパイラによる閉包検査**（以下「P 検査」）: Unity 6000.6.0f1 同梱の Roslyn `csc` で、指定したソース集合だけを 1 つの library にコンパイルした。参照は netstandard 2.1、`UnityEngine*.dll`（Editor 集合では `UnityEditor*.dll` と `UNITY_EDITOR`）、`Library/ScriptAssemblies` の `UniTask` / `Unity.Addressables` / `Unity.ResourceManager` だけ。`OneStarMaker.Runtime` / `OneStarMaker.Foundation` は渡さない。成功すれば、その集合が OSM の他の部分なしで型として閉じていることを示す。asmdef・リポジトリのファイルは変えていない。手順を書いた一時 script は git 管理外で、成果物は本書に転記した結果だけ。
- P 検査が示さないもの: Unity のコンパイル（asmdef ごとの define、Player 向けの define、analyzer）との完全な一致、実行時の挙動、シリアライズの互換性。

## 1. 要件の再記述

### 1.1 3 つの利用形態

| 形態 | 利用者が導入するもの | 導入しなくてよいもの |
|---|---|---|
| U-A AssetManagement 単独 | AssetManagement（AssetDescriptions を含む）と、Unity・UniTask・Addressables | OSMFramework の本体（Foundation、SceneSystem、Bootstrap 等）、BuildSystem |
| U-B BuildSystem 単独 | BuildSystem と、Unity・その外部パッケージ | OSMFramework の本体、AssetManagement |
| U-C OSM での併用 | 両方と、両者をつなぐ連携部分 | — |

「単独」は OSM 本体と相方の機能を必須にしないという意味で、Unity・外部パッケージ・小さな共通契約まで禁じるものではない（依頼書 §1）。

**U-B の今回の範囲:** Content Directory の生成と配布（Selection、Materialization の一般部分、`Editor/Build/Content`、`Runtime/BuildContent` の形式・revision・Distribution）に必要な BuildSystem の中核を、単独利用できる BuildSystem の第一弾とする。Addressables 経路の Variant Build（`Editor/Build/Variants` 等）と SampleGame の Player Build は含まない。これが成立しても「BuildSystem 全体を取り出せた」とは読まない。最初の対応 target は現行と同じ Windows Player（`StandaloneWindows64-Player`）に限る。

### 1.2 提供側が保証すべきこと

- **G1 型の閉包:** U-A・U-B のそれぞれで、導入する部分だけでコンパイルが通る。相方の機能や OSM 本体の型を参照しない。
- **G2 連携の任意性:** 両方を結ぶコード（BuildSystem の成果物を AssetManagement の読み込みに差し込むもの）は、U-A・U-B のどちらの必須要件にもならない。
- **G3 最小利用経路:** U-A・U-B のそれぞれに、OSM 固有の型を作らずに辿れる public な入口がある（§2.3 で具体化）。
- **G4 併用の非退行:** U-C（現在の OSM の動作）は変わらない。既存の起動順序・失敗経路・成果物形式を壊さない。
- **G5 失敗契約の所有が明確:** 各機能が投げる例外とその reason code の所有者が、その機能自身か、両者が共有すると明示した小さな契約のどちらかである。

### 1.3 保証しなくてよいこと（今回）

- 独立した Unity Package・UPM の配布形態、別リポジトリ、バージョン管理。実証の結果から後で決める（依頼書 §3）。
- 独立した asmdef をいま作ること。asmdef は単独利用の十分条件ではなく、作るかは後続（§4.3）。
- OSM 固有の便利機能（`AppConfig` からの起動構成、SceneResourceMap からの Build 入力、Addressables の Variant Build）を単独利用でも使えること。これらは OSM 側の部品として分類する（§2.1）。
- 利用者がどちらをなぜ使うか。問わない。

## 2. 現状の具体的な阻害要因

### 2.1 責務の分類（base `d4861882`）

`BuildSystem` と `Runtime/BuildContent` を名前で同一視せず、責務で分けた。パスは `unity/Assets/OneStarMaker/Scripts/` からの相対。

**AssetManagement（U-A の本体）**

| パス | 責務 |
|---|---|
| `Runtime/AssetDescriptions/`（7 ファイル） | Asset の記述（`AssetDescription`、`SceneAssetDescription`、`LoadType`、`AssetPayload`） |
| `Runtime/AssetManagement/`（23 ファイル） | `IAssetManagement` / `AssetManagement`、寿命スコープ（`AssetOwner`）、キャッシュ、Addressables backend、Content Directory 読み込みの port（`Internal/IContentDirectoryBackend.cs`）と public の読み込み API（`LoadContentAssetAsync` 等。directory が未登録なら `DirectoryNotRegistered` を投げる） |

**BuildSystem 本体（U-B の候補）**

| パス | 責務 |
|---|---|
| `Editor/Build/Selection/`（asmdef `OneStarMaker.Build.Selection`、参照 0、`noEngineReferences`） | Build の目的・tag・候補の選択 |
| `Editor/Build/Materialization/` のうち `AssetDatabaseGateway.cs`、`AssetDependencySnapshotBuilder.cs`、`BuildDependencySnapshot.cs`、`BuildMaterializationIssue.cs`、`BuildMaterializationSnapshot.cs`、`AssemblyInfo.cs` | 依存の snapshot と、Selection に渡す候補一式の型 |
| `Editor/Build/Content/`（asmdef `OneStarMaker.Build.Content`） | Unity Content Directory の生成（`BuildContentCoordinator`、`UnityContentDirectoryAdapter`）、成果物の投影・報告、配布 manifest の発行 |
| `Runtime/BuildContent/` のうち `BuildContentDeclaration.cs`、`BuildContentRoot.cs`、`ContentDeclarationMismatch.cs`、`ContentDirectoryException.cs`、`ContentDirectoryIndex.cs`、`ContentRevisionGate.cs`、`RevisionProcessLease.cs` | 成果物の形式（root・index・宣言）、revision の排他 |
| `Runtime/BuildContent/Distribution/`（10 ファイル） | 配布・cache・install・検証（`ContentInstaller`、`ContentCacheStore`、`InstalledRevisionVerifier` 等） |

**連携部分（U-C だけで要る）**

| パス | 責務 |
|---|---|
| `Runtime/BuildContent/ContentDirectorySession.cs` | 生成済み directory を登録し、AssetManagement の port `IContentDirectoryBackend` を実装する |
| `Runtime/BuildContent/UnityContentDirectoryBackend.cs` | Unity の Content Directory API で、AssetManagement の `IBackendAsset` / `IBackendScene` / `IBackendInstance` を実装する |
| （予定）BOOTSTRAP S2 の `ContentDirectoryStartup` | 起動時の登録・install・close・Play 停止。§4.2 |

**OSM 固有部分**

| パス | 理由 |
|---|---|
| `Runtime/BuildContent/PlayerContentConfiguration.cs`、`PlayerBakedContentConfiguration.cs` | `Foundation.Config.AppConfig` を読む起動構成 |
| `Runtime/Bootstrap/AbstractApplicationInitializer.cs` の Content Directory 起動ポリシー（約 75 行。BOOTSTRAP §1） | 起動順序の中で連携部分を配線する |
| `Editor/Build/Materialization/SceneResourceContentMaterializer.cs`、`ScenePayloadMappingPolicy.cs` | `SceneSystem.SceneResourceMap` を BuildSystem の入力へ写す |
| `Editor/Build/Variants/`、`Editor/Build/Addressables/`、`Editor/Build/AssetDescriptions/`（asmdef `OneStarMaker.Editor`） | Addressables 経路の Variant Build と SceneResourceMap の収集。`SceneSystem` と `RemoteCatalogRuntimeBridge` を使う。Content Directory 経路の BuildSystem ではない（AssetManagement の Addressables 側の Editor 道具に近いが、本書では深掘りしていない） |
| `SampleGame/DependOnAll/Editor/Build/`（17 ファイル） | SampleGame の Build 目的・Player Build・配布 UI |

### 2.2 阻害要因

P 検査の結果と、それが示す辺。

| # | 阻害要因 | 根拠 | 影響する形態 |
|---|---|---|---|
| X1 | **単一の Runtime asmdef。** AssetManagement も BuildContent も `OneStarMaker.Runtime`（参照: Foundation、UniTask、Addressables、ResourceManager、LitMotion、InputSystem、UniTask.Addressables、Cinemachine）の中にあり、`OneStarMaker.Build.Content` / `.Materialization` は `OneStarMaker.Runtime` 全体を参照する | asmdef の実物 | U-A・U-B とも、今は OSM の Runtime 全体を持ち込まないと使えない。ただしこれは配布単位の問題で、型の辺は X2〜X4 だけ |
| X2 | AssetManagement → BuildContent: `ContentDirectoryException` / `ContentDirectoryFailureCode` | P1: AssetDescriptions + AssetManagement だけでは `using OneStarMaker.Runtime.BuildContent` の 3 箇所（`AssetManagement.ContentDirectory.cs`、`IAssetManagement.cs`、`Internal/IContentDirectoryBackend.cs`。後 2 つは型を使わない未使用の `using`）で失敗。`ContentDirectoryException.cs` 1 ファイルを足すと **成功** | U-A |
| X3 | BuildSystem 本体 → AssetManagement: `AssetType` | P2: Runtime の BuildSystem 本体（16 ファイル + 例外ファイル）だけでは `BuildContentRoot.cs` の `AssetType`（field・property・引数、4 箇所）と `ContentDirectoryIndex.cs` の未使用の `using OneStarMaker.Runtime.AssetManagement` で失敗。`AssetType.cs` 1 ファイルを足すと **成功**。`AppConfig` も Addressables も要らない | U-B |
| X4 | 連携部分は両方の internal を使う | P3: 連携 2 ファイル + OSM 構成 2 ファイルを、AssetManagement と BuildSystem 本体の別 library に対してコンパイルすると、AssetManagement の internal 5 型（`IContentDirectoryBackend`、`IContentDirectoryToken`、`IBackendAsset`、`IBackendScene`、`IBackendInstance`）と static `AssetKey.FromContentDirectory`、**BuildSystem 本体の internal 7 件**（`ContentDirectoryIndex`、`ContentDirectoryIssue`、`VerifiedInstalledRevision`、`InstalledRevisionVerifier`、`ContentDeliveryFiles`、`ContentInstallReceipt`、`ContentRevisionGate.Reserve`）が見えずに失敗 | U-C（将来 assembly を分ける場合） |
| X5 | Bootstrap が両方の internal を直接配線 | BOOTSTRAP §1 の実測（AssetManagement の internal メソッド 4 つ、BuildContent の internal 8 型） | U-C。BOOTSTRAP S2 で facade に集める |
| X7 | **出力先と target が OSM の規約に固定。** `BuildContentCoordinator.Build` は出力 root が Unity project の親の `artifacts/bs2b` か `TestResults/bs3-fixture` の配下でなければ `ArgumentException` を投げ、`TargetName` は `StandaloneWindows64-Player` の固定値。「安全な出力先の検証」（path の境界・symlink の防御）と「OSM が許可する置き場所の規約」が同じ場所にある | `Editor/Build/Content/BuildContentCoordinator.cs` 33・45〜52 行（計画レビュー F-01、本書の担当が base で確認） | U-B（X6 を直しても、利用者が出力先を選べない） |
| X6 | **BuildSystem 単独の入力経路が無い。** `BuildContentCoordinator.Build(BuildContentRequest)` は `BuildMaterializationSnapshot` を要るが、そのコンストラクタは internal で、作れる public 経路は `SceneResourceContentMaterializer.Materialize(SceneResourceMap)` だけ。SampleGame は `InternalsVisibleTo("SampleGame.DependOnAll.Editor")` で直接作っている | `Materialization/BuildMaterializationSnapshot.cs` 20・27 行、`Materialization/AssemblyInfo.cs`。加えて同ファイル 59 行の provider identity が `osm.scene-resource-map.representation.v1` 固定 | U-B（コンパイルは通るが、OSM の SceneResourceMap 無しでは最小経路を辿れない） |

Editor 側の P 検査: `Selection` 単独 **成功**。`Materialization` は全体だと `SceneResourceMap` / `SceneResource` / `AssetPayload` / `LoadType` で失敗し、§2.1 の OSM 固有 2 ファイルを除けば **成功**。`Editor/Build/Content` は、Selection・Materialization の一般部分・P2 の library に対して **成功**（BuildContent の internal を使わない）。

### 2.3 最小利用経路

| 形態 | 経路 | 状態 |
|---|---|---|
| U-A | `new AssetManagement()` → `LoadAssetAsync<T>(AssetKey, AssetOwner.Manual)` / `LoadSceneAsync` → `Release` / `ReleaseScene` / `ReleaseAll`（全て public） | 型は X2 を除き閉じている。実行時の確認は未実施 |
| U-B | 候補一式を用意 → `BuildContentCoordinator.Build` → Content Directory と `BuildContentRoot` → 実行時に `ContentDirectoryIndex` で読む / Distribution で install・検証 | X6 で入口が閉じている。また `ContentDirectoryIndex` と `InstalledRevisionVerifier` が internal なので、U-B の利用者が実行時に成果物を検証する public 経路も無い（X4 の 7 件と同じ型） |
| U-C | 現在の OSM の経路（Bootstrap → session → `AssetManagement.InstallContentDirectory` → `LoadContent*Async`） | 動作中。SampleGame の `BuildContentDirectoryIntegrationTests` が生成から読み込みまでを通す |

### 2.4 未確認

- Unity のコンパイル（asmdef・define）での P 検査の再現。P 検査は型の閉包だけを示す。
- `Editor/Build/Materialization` の一般部分が `Unity.Addressables` を本当に要るか（P 検査では既定で参照に入れた）。
- `BuildContentRoot`（`ScriptableObject`）を将来別 assembly へ移したとき、生成済み Content Directory の script 参照が解決できるか。Unity は script を assembly 名と型名で解決するため、移すと既存の成果物の再生成が要る可能性がある。assembly を分けるスライスの A0 で確かめる。
- `Editor/Build/Variants` 等の Addressables 経路の分類の細部。

## 3. 構成案の比較と推奨

### 3.1 案

- **案 1（現行の一方向依存を整理して継続）:** ASSET_CYCLE のとおり例外型 2 つを AssetManagement へ移し、BuildContent → AssetManagement の一方向にする。
- **案 2（相互に必須依存しない + 任意の連携）:** AssetManagement と BuildSystem 本体の間の型の辺を両方向とも 0 にし、両方を知るのは連携部分だけにする。失敗契約は意味で分ける（AssetManagement は読み込みの失敗、BuildSystem は形式・revision・配布の失敗をそれぞれ自分の型で持ち、連携部分が橋渡しする）。`BuildContentRoot` の分類は BuildSystem 側の enum にし、連携部分が `AssetType` へ写す。
- **案 3（小さな共通契約を切り出す）:** 案 2 と同じく本体同士の辺を 0 にするが、型を分けずに、両方が使う型だけを「共通契約」として本体の外へ出す。対象は X2・X3 で P 検査が示した 3 型（`ContentDirectoryException`、`ContentDirectoryFailureCode`、`AssetType`、合計約 50 行）。public の型名・メンバー・enum 値は変えない。連携部分は今の 2 ファイルのまま。

### 3.2 比較

| 観点 | 案 1 | 案 2 | 案 3 |
|---|---|---|---|
| U-A の型の閉包（G1） | 成立（P1 + 例外ファイル） | 成立 | 成立（AssetManagement + 共通契約） |
| U-B の型の閉包（G1） | **不成立。** BuildSystem 本体が AssetManagement 全体を必須にする。しかも移設で `Distribution/` の 4 ファイル（`ContentCacheStore`、`ContentInstaller`、`ContentSourceAdvisor`、`InstalledRevisionVerifier`）と `ContentRevisionGate`・`RevisionProcessLease`・`PlayerContentConfiguration` に `using OneStarMaker.Runtime.AssetManagement` が新しく入り（ASSET_CYCLE §3 の表）、BuildSystem 本体 → AssetManagement の辺が `AssetType` 1 型から例外まで広がる | 成立 | 成立（BuildSystem 本体 + 共通契約。P2 + `AssetType.cs` が実質これ） |
| 失敗契約の所有（G5） | AssetManagement が `RevisionBusy`・`PathInUse`・`DeletionInProgress`・`RevisionLockUnavailable` 等、配布側の 16 値全部を公開契約として持つ（AssetManagement が投げるのは 5 値） | 明確（各機能が自分の失敗を持つ） | 共通契約として明示的に共有。どちらの本体にも属さない |
| 公開 API の変化 | public 2 型の namespace 変更（ソース・バイナリ互換を壊す。`Type.FullName` と `Exception.ToString()` も変わる） | public の例外型・enum の分割、既存の `catch` と code の assert（テスト 8 ファイル、`ContentRevisionGate`・Distribution の `catch` 5 箇所）の書き換え、BuildSystem 側 enum の追加 | **0**（folder の移動だけなら namespace も据え置ける。§6 D2） |
| 連携の任意性（G2） | 連携部分は BuildContent に残り、BuildSystem 本体と分けられない | 成立 | 成立 |
| コスト | 小（移動 1 + `using` 約 13） | 大（例外の分割と橋渡し、enum の写像、テスト書き換え） | 小（移動 2〜3 + `using` の調整。シリアライズ・実行時挙動は不変） |
| 将来の変更容易性 | BuildSystem を切り出すとき、例外を再び AssetManagement から出し直す二度手間 | 各機能が独立に失敗契約を進化できる | 共通契約を変えると両方に波及する。ただし対象は 3 型で、enum の値を足す以外の変更は稀。後で案 2 へ進む場合も、共通契約から分ける 1 段で済む |
| 層の先取り | なし | 橋渡しの型が増える（実装 1 つの段階で抽象を作る形に近い） | 新しい型・port・assembly を作らない |

### 3.3 推奨

**案 3。** P 検査で、本体同士の型の辺は 3 型（例外 2 + `AssetType`）だけと分かった。これを共通契約として本体の外へ出せば、公開 API と挙動を変えずに U-A・U-B の型の閉包が両方とも成立する。案 1 は U-A だけを満たし、U-B に対しては辺を増やす。案 2 は目的を満たすが、いま公開契約を分ける根拠（どちらかが独立に失敗契約を変えたい事情）が無い。

案 3 で残る阻害要因は X1（配布単位）、X4（連携部分が両方の internal を使う）、X6（BuildSystem 単独の入力経路）、X7（出力先と target の固定）で、どれも型の辺ではなく配布と公開面の問題なので、§4.3 の後続へ送る。

**境界の判定の仕方:** namespace を据え置く（§6 D2）と、BuildSystem 本体のソースには `using OneStarMaker.Runtime.AssetManagement`（`AssetType` のため）が残る。したがって「`using` の文字列が 0」は境界の主張に使わない。境界は、型がどのソース集合（将来は assembly）に属するかで判定し、その証明は P 検査（後に asmdef のコンパイル）が行う。また、将来共通契約を別 assembly へ移すと、namespace が同じでも CLR の型の同一性（assembly 名）が変わるので、そのときのバイナリ互換は改めて検討する。

**共通契約の規則:** 共通契約への依存は AssetManagement・BuildSystem の両方に許す。共通契約の変更（`ContentDirectoryFailureCode` の値の追加、`AssetType` の値の追加等）は両方への影響を評価してから行う。共通契約は両者の依存の導入を独立させるが、両者が契約の変更を独立に出せることまでは意味しない。必要になれば後で契約を分ける（案 2 へ進む）。

## 4. 現計画の採否と最小改訂案

### 4.1 ASSET_CYCLE の採否

**現行の書き直し版（例外型 2 つを `Runtime/AssetManagement/Abstractions/` へ移し namespace を変える）は採用しない。** 理由は §3.2 の案 1 の列。「AssetManagement からの逆参照を 1 本除く」という効果は案 3 でも得られ、そのうえ U-B を遠ざけない。

改訂して残す（スライスとしては存続させ、問いと AC を差し替える）。

- **今やること（ASSET_CYCLE の改訂版）:** 共通契約の切り出し。
  - 問い: AssetManagement（AssetDescriptions を含む）と BuildSystem 本体の間の型の辺を、公開 API と実行時挙動を変えずに両方向とも 0 にできるか。
  - 変更: `ContentDirectoryException.cs`（2 型）と `AssetType.cs` を、AssetManagement でも BuildSystem 本体でもない置き場所へ `.meta` ごと `git mv` する。namespace は据え置く（§6 D2）。移動先の folder は ASSET_CYCLE 改訂版の A1 が `Runtime/AssetBuildContracts/`（`OneStarMaker.Runtime.asmdef` の配下で、所属 assembly は変わらない）に決めた。Unity 上の配置、`.meta` の移動、P 検査の集合への含め方、将来の assembly の所有の扱いも同書が定義している。folder 名と namespace が一致しないことを、型の論理的な所有や assembly の所属を決める理由にしない。未使用の `using` 3 行（`IAssetManagement.cs`、`Internal/IContentDirectoryBackend.cs`、`ContentDirectoryIndex.cs`）を削除する。namespace を据え置くので、`AssetManagement.ContentDirectory.cs` と `BuildContentRoot.cs` の `using` は変えない。
  - 成立条件の証明を `grep`（現 AC2・AC6）から P 検査に置き換える: U-A 集合と U-B 集合（§5.1）がそれぞれコンパイルに成功する。`grep` は補助に下げる。このスライスの成果は「本体同士の型の辺が無くなった」までで、単独利用の達成とは書かない（§5.2）。
  - P 検査を AC に使う前提として、A1（遅くとも A2 入力版）までに、第三者が再実施できる形を固定する: コンパイル対象と除外のファイル一覧、参照 assembly、define、実行コマンド、exit code、出力ログ（計画レビュー F-04）。
  - 不変条件は現行の I1〜I4 を流用できる（宣言内容・挙動・`.asmdef` 不変・テストの意味）。D2 で namespace を据え置けば、I1 の「namespace を変えて互換を意図して壊す」は「namespace も変えない」に強まり、§0 の risk 根拠（互換の破壊）が消える。
- **後続でやること:** §4.3。
- **やらないこと:** 例外型・enum の分割（案 2）。`ContentDirectoryFailureCode` の値の追加・削除。連携部分の folder 移動（P 検査の集合定義で足りる。移動は asmdef を分けるときにまとめる）。asmdef の作成。

### 4.2 各文書の改訂案

各文書の A1 改訂で当てる差分。適用状況: **ASSET_CYCLE の表は適用済み**（2026-11-07 の A1 改訂版。表の「§2.2 AC1」の置き場所は `Runtime/AssetBuildContracts/` に、「§2.2 AC2・AC6」は P 検査の AC4 になり、A2 の指摘で AC2〜AC5 をさらに改めた。適用後の正本は同書）。**BOOTSTRAP の表は未適用**。

**ASSET_CYCLE**（適用済み。以下は適用時の指示の記録）

| 箇所 | 現行 | 改訂案 |
|---|---|---|
| 題 | AssetManagement → BuildContent の逆向き依存の解消 | AssetManagement と BuildSystem 本体の共通契約の切り出し |
| §0 risk の根拠 | public 2 型の namespace を変えて互換を意図して壊す | D2 で据え置くなら「public 型の宣言・namespace・挙動を変えない、ファイル移動だけ」。変えるなら現行の根拠を残す |
| §0 revision の経緯 | 2026-11-07 の決定で目的を「独立した assembly にできる向き」に絞った | 末尾に「2026-11-07 後半、owner が上位目的を『AssetManagement 単独・BuildSystem 単独・OSM での併用』に改めた。変更頻度を理由にした 2 assembly の決定はこれに置き換わった（[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)）」を追記 |
| §1 目的 | AssetManagement を BuildContent から独立した assembly に切り出せる向きにする | §4.1 の問い |
| §1 目的の理由（owner の決定、変更頻度、外部利用は結果） | 3 項目 | 全て差し替え: 「owner の意図（2026-11-07）: AssetManagement 単独、BuildSystem 単独、OSM での併用を可能にする。外部利用は目的そのもので、利用者がなぜ使うかは問わない」。変更頻度の実測は根拠から外す（BuildContent 誕生後の AssetManagement の変更 10 件中 7 件が BuildContent と同時で、独立の根拠にならない） |
| §1 分割調査の目標図の段落 | 2 つに改める決定に基づく | 「assembly の数と分け方は後続の asmdef スライスで、P 検査の集合を入力にして決める」 |
| §1 現況 | internal 5 型 | internal 5 型と `AssetKey.FromContentDirectory`（本文の他の箇所と揃える）。BuildSystem 本体 → AssetManagement は `AssetType` 1 型だけ、という P 検査の結果を追記 |
| §2.1 I1 | namespace を変える、shim を置かない | D2 に従う |
| §2.1 S1・S2 | `grep` で見る AssetManagement → BuildContent の辺 0、BuildContent の参照集合は base と同じ | S1: U-A 集合が P 検査で成功。S2: U-B 集合が P 検査で成功。S3（回帰）は現行のまま |
| §2.2 AC1 | 移動先 `Runtime/AssetManagement/Abstractions/` | D2・D3 の置き場所。`AssetType.cs` も対象に加える |
| §2.2 AC2・AC6 | `grep` | P 検査の再現手順（A1 で script の置き場所を決める。§5.3）。`grep` は補助 |
| §2.3 | 2 assembly に分けるときの internal 10 件 | 「assembly を分けるときに連携部分が越える internal は、AssetManagement 側 6 件、BuildSystem 本体側 7 件（本書 §2.2 X4）」。所有は asmdef スライス |
| §2.5 未決事項 | なし | 未決なし。§6 D2・D3 の決定（namespace 据え置き、`AssetType` も共通契約へ）を決定記録として転記 |
| §3 表 | 移動先・`using` の追加 7 + 3 | 案 3 の移動と `using` に差し替え。`Distribution/` への `using OneStarMaker.Runtime.AssetManagement` の追加は無くなる |
| §5 A2 再実施の観点 (a) | 例外型の置き場所・向き | 「本書の推奨（案 3）が U-A・U-B・U-C の 3 形態に対して妥当か」。入力は改訂版。旧書き直し版のレビューは行わない |
| harvest to | `27-folder-structure.md` §2.2 に BuildContent → AssetManagement の向き | 「AssetManagement と BuildSystem 本体は互いを参照せず、共通契約だけを共有する。両方を知るのは連携部分」 |

**BOOTSTRAP**

| 箇所 | 現行 | 改訂案 |
|---|---|---|
| §3.2 3 項目目 | 「ASSET_CYCLE §1 の owner 決定（2026-11-07）で、目標は `Runtime.AssetManagement` と `Runtime.BuildContent` の 2 assembly である」 | 「assembly の構成は未決。2026-11-07 の 2 assembly の決定は、owner の上位目的（AssetManagement 単独・BuildSystem 単独・併用。[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)）に置き換わった。`ContentDirectoryStartup` は BuildSystem 本体ではなく、連携部分と OSM 固有の起動構成（`AppConfig` を読む `Resolve`）にまたがる。本スライスの時点では全てが同じ `OneStarMaker.Runtime` assembly にあるので、facade は両方の internal を呼べ、I6 の『新しい public 型は 1 つ』は成り立つ。assembly を分けたときに facade が越える internal（AssetManagement 側の 4 メソッドと port、BuildSystem 本体側の 7 件）の扱いは asmdef スライスで決める」 |
| §3.2 1 項目目（facade を BuildContent に置く） | 使う型が全て BuildContent の internal なので BuildContent に置く | 「BuildSystem 本体の集合（U-B）に含めない場所に置く。§2.1 の連携部分と同じ folder（現状 `Runtime/BuildContent/`）でよいが、本書 §5.1 の U-B 集合から除く対象として AC に名前で挙げる」 |
| §2.1 S2 | `Runtime/BuildContent/ContentDirectoryStartup.cs` | 置き場所は上のとおり。S2 に「U-A・U-B の P 検査の集合に含まれない」を加える |
| §2.5 U3 | S2 だけを独立スライスにする案を選択肢として残す | 決定として明記: 「S2（+ AC9 / AC10）を独立スライス `CONTENT_DIRECTORY_STARTUP` にする。順序は ASSET_CYCLE 改訂版 → `BUILDSYSTEM_STANDALONE_ENTRY` → 本スライス → asmdef スライス。S3〜S5 は U-A・U-B・U-C の成立に関係しないので、この列の外で進めてよい」（owner 判断 D4） |
| §0 関連スライス | ASSET_CYCLE が先行（例外型の namespace 変更後の `using` を前提） | namespace は据え置き（D2）なので、その前提は消える。先行は ASSET_CYCLE 改訂版と `BUILDSYSTEM_STANDALONE_ENTRY`（D4） |
| §2.3 | asmdef Stage 2 等 | 「連携部分と OSM 固有部分を BuildSystem 本体から分ける asmdef の判断」を所有スライス（asmdef スライス）付きで追加 |

### 4.3 後続でやること（所有する後続）

| 項目 | 後続 | 入力 |
|---|---|---|
| X5: Bootstrap の Content Directory 起動ポリシーを facade に集める | BOOTSTRAP S2（独立スライス化は D4） | 本書 §4.2 |
| X6: BuildSystem 単独の入力経路（`BuildMaterializationSnapshot` を SceneResourceMap 無しで作る public 入口）と、実行時に成果物を読む・検証する public 入口（`ContentDirectoryIndex` 等） | 新規 `BUILDSYSTEM_STANDALONE_ENTRY`（未作成。D5） | §2.3 の U-B の行。public 面を増やすので、最小の入口 1 つから始める。コンストラクタをそのまま公開せず、候補・依存の閉包・tag・graph を利用者が組み立てられる factory にし、OSM 専用の provider identity を一般用途に漏らさない。AC に「SceneResourceMap も `InternalsVisibleTo` も使わない」を入れる |
| X7: 出力先と target の固定 | `BUILDSYSTEM_STANDALONE_ENTRY` か別の小スライス（D5） | 「安全な出力先の検証」と「OSM が許可する置き場所の規約」を別の責務に分ける。安全機構は外さない。検証: OSM 既定とは別の、利用者が選んだ安全な path で public 経路の Build が成立し、既存の path 境界・symlink の防御が退行しない。target は Windows Player のまま（§1.1） |
| X1・X4: asmdef の構成（AssetManagement、BuildSystem 本体、共通契約、連携、OSM 固有をどの assembly に入れるか）と、連携部分が越える internal の扱い | asmdef 分割スライス（未作成） | 本書 §2.2 X4 の 13 件、§2.4 の `BuildContentRoot` のシリアライズ互換、P 検査の集合 |
| 別の Unity プロジェクトでの実証（program の完了判定） | 上記の後 | §5.2 段階 4 |

## 5. 実証・受け入れ条件

### 5.1 3 ケースの集合（P 検査）

| ケース | ソース集合 | 2026-11-07 の結果（base `d4861882`） | 合格条件 |
|---|---|---|---|
| U-A | `Runtime/AssetDescriptions/`、`Runtime/AssetManagement/`、共通契約 | 共通契約の代わりに `ContentDirectoryException.cs` を足して成功。足さないと 3 件の `using` で失敗 | 共通契約を切り出した後、この集合だけで成功 |
| U-B（Runtime） | `Runtime/BuildContent/` から連携 2 ファイルと OSM 固有 2 ファイルを除いたもの、共通契約 | `AssetType.cs` を足して成功。足さないと `AssetType` 4 箇所と未使用 `using` 1 で失敗 | 同上 |
| U-B（Editor） | `Editor/Build/Selection/`、`Editor/Build/Materialization/` から OSM 固有 2 ファイルを除いたもの、`Editor/Build/Content/`、U-B（Runtime）の結果 | 成功 | 成功を維持 |
| U-C | 現在の `OneStarMaker.Runtime` と Editor の Build asmdef（Unity のコンパイル） | 動作中 | 全 EditMode 回帰と `contract-audit` が合格 |

### 5.2 段階

1. **段階 1（ASSET_CYCLE 改訂版の判定）:** U-A・U-B の P 検査がコンパイルに成功する（型の閉包、G1）。U-C は全 EditMode 回帰（`pwsh tools/run-tests.ps1`、空 filter）と `pwsh tools/contract-audit.ps1`（G4）。挙動の不変は現行 AC3〜AC5（assertion・挙動・`.meta` GUID）を流用する。
2. **段階 2（後続 X6 の判定）:** U-B の最小経路を EditMode テストで通す。テストは BuildSystem の public 型だけを使って、候補一式 → `BuildContentCoordinator.Build` → 生成物を BuildSystem の public 入口で読む、を行う。U-A は既存の `OneStarMaker.Tests.AssetManagement` のうち AssetManagement の public API だけを使うテストを U-A の最小経路の証拠として列挙する（新規テストが要るかは A1 で確かめる）。U-C は既存の `SampleGame/Tests/Editor/Build/BuildContentDirectoryIntegrationTests.cs`（生成 → 読み込み）。テスト asmdef は今は `OneStarMaker.Runtime` 全体を参照するので、テスト単独では閉包を示せない。閉包は段階 1 の P 検査、経路はテストで分担する。
3. **段階 3（asmdef スライスの判定）:** U-A・U-B のそれぞれだけを参照するテスト asmdef（例: AssetManagement の asmdef だけを参照する EditMode テスト）が Unity でコンパイルされ、段階 2 の経路テストが通る。ここで P 検査の役割はコンパイラ（asmdef）へ移る。
4. **段階 4（program の完了判定。「U-A / U-B を単独で使える」と宣言する唯一のゲート）:** U-A と U-B を**互いに隔離して**検証する。U-A 用のプロジェクトには AssetManagement、共通契約、許容する外部パッケージだけを入れ、BuildSystem と OSM 本体は入れない。U-B 用のプロジェクトには BuildSystem、共通契約、許容する外部パッケージだけを入れ、AssetManagement と OSM 本体は入れない。両方を同じプロジェクトに入れると相手への隠れた依存を見逃すので、別々のクリーンなプロジェクト（または導入内容を完全に作り直せる同等の隔離環境）を使う（コピーかローカル package でよい。UPM 化・別リポジトリ化・配布手段の確定は求めない）。それぞれで public API だけの最小ユースケースを通す。U-B は X7 を解消した後の、利用者が選んだ出力先で行う。あわせて U-C の非退行（全 EditMode 回帰）を示す。同じリポジトリの中の検査（段階 1〜3）では、Unity が暗黙に型を供給していないことまでは保証できないので、このゲートを段階 1〜3 で代用しない。

### 5.3 P 検査を何として持つか

段階 1 の判定にはスライスの AC として使えば足りる。常設の検査（`contract-audit` 等）にするか、スライスの照合手順だけに留めるかは §6 D4。どちらでも、集合の定義（§5.1 の表）はリポジトリの文書に 1 箇所だけ置き、script はそれを読む（同じ事実を 2 箇所に置かない）。

## 6. owner の判断（2026-11-07、計画レビューの提案どおりで決定）

計画レビュー（owner が依頼、2026-11-07。判定「案 3 を A0 の推奨として支持。X7 と最終ゲートを追記すること」。指摘 F-01〜F-07 は §1.1、§2.2 X7、§3.3、§4.1、§4.3、§5.2 段階 4 に反映済み）の提案を、owner が「推奨どおり」として採った。改訂版への計画レビュー R2（判定「基本方針を支持。ASSET_CYCLE の A1 改訂へ進んでよい」）の F-01〜F-04（共通契約の folder は A1 で決める、順序の統一、段階 4 の隔離、状態欄の更新）も反映した。利用者の採用動機や利用価値は問わない。

- **D1 構成:** 案 3（共通契約の切り出し）を採る。ASSET_CYCLE の現行の書き直し版（例外型を AssetManagement へ移す案 1）は撤回し、§4.1 のとおり改訂する。
- **D2 namespace:** 据え置く。共通契約へ移す 3 型は `OneStarMaker.Runtime.BuildContent` / `OneStarMaker.Runtime.AssetManagement` のままで、公開 API の互換を保つ。folder と namespace が一致しない状態は残す。別 assembly へ移すときの互換は、そのスライスで改めて検討する（§3.3 境界の判定の仕方）。
- **D3 `AssetType`:** そのまま共通契約へ移す。BuildSystem 側に同じ値の enum を作って写す案は、必要が出るまで採らない。
- **D4 順序:** (1) ASSET_CYCLE 改訂版（共通契約の切り出し。成果は型の閉包だけ）→ (2) `BUILDSYSTEM_STANDALONE_ENTRY`（X6・X7）→ (3) BOOTSTRAP S2（連携部分と OSM 固有の起動構成の分離）→ (4) asmdef スライス → (5) 段階 4 の別プロジェクト実証で program 完了。S2 を asmdef より先にするのは、全てが単一の `OneStarMaker.Runtime` にあり facade が両方の internal を呼べる段階で、越境を 1 箇所に集めておけば、asmdef スライスがその 1 箇所だけを裁定すればよいため（計画レビュー R2 F-02）。BOOTSTRAP の S3〜S5 は U-A・U-B・U-C に関係しないので、この列の外で進めてよい。P 検査を常設の検査にするかは、ASSET_CYCLE 改訂版の A1 で運用コストを見て決める。
- **D5 BuildSystem 単独の入口:** `BUILDSYSTEM_STANDALONE_ENTRY`（X6）と出力先の規約の分離（X7。同じスライスか別の小スライス）は、単独利用の完成条件とする。D4 の (2) として asmdef スライスより前に行い、program の完了を宣言する前に必ず段階 4 で検証する。それまで U-B は「型としては閉じているが、OSM の SceneResourceMap と既定の出力先無しでは最小経路を辿れない」状態として記録する。
