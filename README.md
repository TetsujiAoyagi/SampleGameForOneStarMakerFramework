# OneStarMaker (OSM)

## このリポジトリのモチベーションと概要

Unity のゲーム開発で問題になりそうなことを、事前に Policy を決めることで解決するためのフレームワークの試作リポジトリです。
多人数開発による Scene ファイルの競合、大規模な Scene ファイルが出来上がって分割できない、Scene の前提がコードの中にしか無く分かっている人がいないといじりづらい、膨大な数の Asset があり開くのが重い、など。
これを、USD の Variant に着想を得た Asset 表現（[`AssetDescription`](unity/Assets/Docs/Architecture/18-asset-description.md)）と、親子関係にある Scene で解決しようとしています。
その他、よく作りそうなものもセットにしています。性能計測のための Telemetry、デバッグのために別アプリを作って通信させる仕組み、レイヤー別の UpdateSystem、など。
いま実証に使っている適用先は**準オープンワールド**で、同じ repo の SampleGame が 4 季のセル世界を載せて確かめています。

> OneStarMaker is a Unity 6 framework prototype that settles, up front, the policies a multi-person Unity project otherwise rediscovers the hard way: scene lifetime, ownership and edit units as one scene tree, USD-Variant-inspired asset descriptions, purpose-scoped content builds, and out-of-process observability. The sample game exercises it on a semi-open-world. Docs are in Japanese.

大絶賛工事中です。試作段階で、コードには AI を活用しています。

## 目的

一番の目的は、**複数の職種が同じ世界を同時に作れる**ことです。そのために、Unity では別々の仕組みに散りがちな 3 つを 1 本の Scene の木に揃えます。

| 軸 | 木が決めること |
|---|---|
| 寿命 | どの Scene がいつ載り、いつ降りるか。親は枝に居る間ずっと居残り、子は親が Ready であることを前提にしてよい |
| 所有 | 誰がその node の中身に責任を持つか。親は、その枝の全ての子が必要とするものだけを初期化する |
| 編集単位 | 誰がどのファイルを触るか。フォルダ構成も木に揃え、職種ごとに Scene を分ける |

木は画面遷移の分類図ではなく、Ready と寿命を共有するスコープの木です。「親に置くと子が巻き込まれる」は機能の問題ではなく木の形が実態と合っていない印で、木を切り直すか、App サービスに出すか、子で吸収するかのどれかで直します。何を独立した node にするかにも規則があり、たとえば起点の Cell より長く生きるイベントは flag ではなく、Season の子という別の親を持ちます。詳細は [`05-scene.md` §5.6](unity/Assets/Docs/Architecture/05-scene.md) と §5.13 にあります。

部品そのものは目新しくありません。寿命管理、Update の順序、手動 DI の配線、構造化ログ、部分 build は、多くの現場が毎回作り直しているものです。この repo の成果物は部品ではなく、それらを同じ契約で繋ぐ決め方と、その理由と却下した案の記録です。性能についても「速い」とは言わず、外から計測でき、境界で原因を局所化できる状態を目指しています。

契約そのものは世界の広さに依りません。準オープンワールドは、その契約に一番負荷がかかる適用先として選んだもので、SampleGame が距離で stream するセル世界を載せて確かめています。

ゲームルールを簡単にする道具ではありません。何を主張し、何をまだ主張しないかは [`docs/GOALS_AND_STRENGTHS.md`](docs/GOALS_AND_STRENGTHS.md) に線を引いてあります。

## 見る箇所

今日この repo で動いているものと、それをどこで触り、どのコードを読み、どの文書が正本かの対応です。Editor で触る手順は次の「試す」にあります。文書の章番号は `unity/Assets/Docs/Architecture/` のものです。

| 何が見られるか | Editor で触る | コード | 文書 |
|---|---|---|---|
| **Scene の木。** 寿命・所有・編集単位を 1 本の木で持つ。親子と LoadType（NecessaryAlways / OnDemand / IncrementalAlways）で「いつ居るか」を宣言し、14 状態の lifecycle を単一の owner が進める。親が先に Stable になり、子は親のサービスを `ISceneQuery` で引く。兄弟は直接参照しない | `OneStarMaker/Scene Graph Editor` で木を検証して `SceneResource` を生成する。node は [`SceneGraphData/Nodes/`](unity/Assets/SceneGraphData/Nodes/) | [`SceneDirector.cs`](unity/Assets/OneStarMaker/Scripts/Runtime/SceneSystem/SceneDirector.cs)、[`SceneLifecycleManager.cs`](unity/Assets/OneStarMaker/Scripts/Runtime/SceneSystem/SceneLifecycleManager.cs)、[`ISceneQuery.cs`](unity/Assets/OneStarMaker/Scripts/Runtime/SceneSystem/ISceneQuery.cs) | [§05 Scene](unity/Assets/Docs/Architecture/05-scene.md)（§5.6「何を親に置くか」）、[§11 Scene Graph Editor](unity/Assets/Docs/Architecture/11-scene-graph-editor.md)、[§27 フォルダ構成](unity/Assets/Docs/Architecture/27-folder-structure.md) |
| **職種ごとの Scene。** 床（Cell）と置き物（Environment）を別 Scene に持ち、Lighting / VFX / Events は必要な Cell にだけ companion として足す。companion は Cell と同じ寿命で、Cell を跨ぐイベントだけが Season の子になる | `OneStarMaker/World Workspace/Open Window`（構造追加の transaction）。Scene 実体は [`Seasons/<Season>/Cells/`](unity/Assets/SampleGame/InGame/InGameSession/Seasons/) | [`CellCompanionScene.cs`](unity/Assets/SampleGame/InGame/InGameSession/World/CellCompanionScene.cs)、[`SessionSeasonController.cs`](unity/Assets/SampleGame/InGame/InGameSession/Streaming/Runtime/Season/SessionSeasonController.cs)、[`SeasonSceneTagPolicy.cs`](unity/Assets/SampleGame/DependOnAll/Editor/Build/SeasonSceneTagPolicy.cs) | [§05 §5.13](unity/Assets/Docs/Architecture/05-scene.md)、[§33 サンプルの実証境界](unity/Assets/Docs/Architecture/33-sample-demonstration-boundaries.md) |
| **同じ論理 Asset の Variant。** 1 つの論理 Scene に Full / Whitebox などの表現を持ち、Play と build が同じ宣言から選ぶ。Variant 名は自由ラベル | node asset の Payload 欄（例: `Spring_Cell_0_4.asset` は空の Variant と `Whitebox` を持つ） | [`AssetDescription.cs`](unity/Assets/OneStarMaker/Scripts/Runtime/AssetDescriptions/AssetDescription.cs) | [§18 AssetDescription](unity/Assets/Docs/Architecture/18-asset-description.md) |
| **距離 streaming。** 飛行 rig の位置から LoadRadius 375 m / UnloadRadius 550 m でセルを load / unload する。政策計算とロードの重さは分けて測れる設計（数字はまだ無い） | 「試す」1 | [`WorldStreamingController.cs`](unity/Assets/OneStarMaker/Scripts/Runtime/Streaming/WorldStreamingController.cs)（Framework の政策）、[`WorldCellCatalog.cs`](unity/Assets/SampleGame/InGame/InGameSession/Streaming/Runtime/Distance/WorldCellCatalog.cs)（SampleGame の格子と半径） | [§21 Scene Streaming](unity/Assets/Docs/Architecture/21-scene-streaming.md)、[§34 到着契約](unity/Assets/Docs/Architecture/34-ondemand-spatial-policy.md)、[`STREAMING_CURRENT_SPEC.md`](docs/streaming/STREAMING_CURRENT_SPEC.md) |
| **目的ごとの部分 build。** `spring-full`、`spring-whitebox`、`spring-planner-whitebox`、`all-full` などの宣言から、その季節・職種・表現に必要な Scene だけを Content Directory にする。成功した revision は上書きせず、再 build は新しい revision になる | `Tools/OSM/Content/Build <宣言>`（「試す」2） | [`SampleGameBuildPurposes.cs`](unity/Assets/SampleGame/DependOnAll/Editor/Build/SampleGameBuildPurposes.cs)（宣言の表）、[`OneStarMaker/Scripts/Editor/Build/`](unity/Assets/OneStarMaker/Scripts/Editor/Build/)（選択・materialization・Content Directory） | [§18 §4](unity/Assets/Docs/Architecture/18-asset-description.md) |
| **宣言から Play する 3 経路。** source のまま Play、build から publish・install・Play を一続き、published directory を受け取って Play | `Tools/OSM/Play As/<宣言>`、`Tools/OSM/Content/Verify Play/<宣言>`、`Tools/OSM/Content/Delivery`（「試す」1・3） | [`PlayAsLauncher.cs`](unity/Assets/SampleGame/DependOnAll/Editor/Build/PlayAsLauncher.cs)、[`VerifyPlayLauncher.cs`](unity/Assets/SampleGame/DependOnAll/Editor/Build/VerifyPlayLauncher.cs)、[`ContentDeliveryWindow.cs`](unity/Assets/SampleGame/DependOnAll/Editor/Build/ContentDeliveryWindow.cs) | [§18 §4](unity/Assets/Docs/Architecture/18-asset-description.md)、[§04 §4.6・§4.8](unity/Assets/Docs/Architecture/04-app-startup.md)、[§13 リソース](unity/Assets/Docs/Architecture/13-resource-system.md) |
| **レイヤー別 UpdateSystem。** MonoBehaviour に依らず、登録した system を決まった順序（Update → LateUpdate → main thread への反映 → 構造変更の適用）で回す。例外の捕捉は各 system が行う | （Editor の入口は無い） | [`UpdateSystemRuntime.cs`](unity/Assets/OneStarMaker/Scripts/Runtime/UpdateSystem/Api/UpdateSystemRuntime.cs)、[`UpdaterDriver.cs`](unity/Assets/OneStarMaker/Scripts/Runtime/UpdateSystem/Hosting/UpdaterDriver.cs)、[`Foundation/UpdateSystem/`](unity/Assets/OneStarMaker/Scripts/Foundation/UpdateSystem/)（layer と backend） | [`UPDATER_CURRENT_SPEC.md`](docs/updater/UPDATER_CURRENT_SPEC.md)、[§26 時間の権威](unity/Assets/Docs/Architecture/26-update-async-time-authority.md) |
| **外からの観測。** ZLogger の構造化ログと telemetry を DebugSocket で出し、.NET 8 の DebugStudio で読む | DebugStudio を起動する（下の節）。App のログ file は `unity/OneStarMakerLocal/Log/` | [`AppLoggerFactory.cs`](unity/Assets/OneStarMaker/Scripts/Foundation/Logging/AppLoggerFactory.cs)、[`Runtime/DebugSocketServices/`](unity/Assets/OneStarMaker/Scripts/Runtime/DebugSocketServices/)、[`Runtime/DebugCommands/`](unity/Assets/OneStarMaker/Scripts/Runtime/DebugCommands/)、[`tools/DebugStudio/src/`](tools/DebugStudio/src/) | [§12 Telemetry](unity/Assets/Docs/Architecture/12-telemetry.md)、[§15 Telemetry v2](unity/Assets/Docs/Architecture/15-telemetry-v2.md)、[§28 契約 v3](unity/Assets/Docs/Architecture/28-telemetry-contract-v3.md) |
| **依存の向きと配線点。** Game → Framework の一方向を asmdef で強制し、全体配線は `DependOnAll` の 1 箇所に集める | （asmdef を Inspector で見る） | [`AppInitializer.cs`](unity/Assets/SampleGame/DependOnAll/AppInitializer.cs)、[`GameSceneFactory.cs`](unity/Assets/SampleGame/DependOnAll/GameSceneFactory.cs)、各 `*.asmdef` | [`unity/Assets/README.md`](unity/Assets/README.md)「Assembly 依存ルール」、[§03 DI](unity/Assets/Docs/Architecture/03-di.md)、[§27](unity/Assets/Docs/Architecture/27-folder-structure.md) |

## 試す

Unity **6000.6.0f1** で `unity/` を開きます（正確な版は `unity/ProjectSettings/ProjectVersion.txt`）。

1. **まず飛ぶ。** `Assets/SampleGame/OutGame/Title/Title.unity` を開いておきます（Editor は開いている Scene を初期登録に使うので、空の Scene から始めると Title は載りません）。Addressables の Play Mode Script は `Use Asset Database` にします（`Window > Asset Management > Addressables > Groups` の `Play Mode Script`）。`Tools/OSM/Play As/spring-full` を選ぶと source から Play に入ります。Title の「スタート」で InGame に入り、Spring の `(0,4)` セルの `(125, 28, 1125)` に spawn して、周囲のセルが距離で load されます。飛行 rig で移動すると、離れたセルが unload され、近づいたセルが load されます。Play Mode Script や Variant profile が条件と違うときは、Play As が理由を出して入る前に止まります（`A Variant profile is active` なら `Project Settings > OneStarMaker > Variant`（表示名 `Variant Checkout`）で `Active Profile` を `(None)` にする）。
2. **必要な分だけ build する。** `Tools/OSM/Content/Build spring-full` で Spring の Full 表現だけを Content Directory として build します。`spring-whitebox` にすると、Whitebox 変種を持つ床の Scene だけが Whitebox になり、置き物の Scene は Full のまま入ります。
3. **build した物で Play する。** `Tools/OSM/Content/Verify Play/spring-full` で build → publish → install → Play を一続きで行います。途中で失敗したら後続の段へ進まず、Play 用に置いた設定を戻して止まります（build の出力と、開始前に閉じた Scene は戻しません）。cache root などは `Tools/OSM/Content/Delivery` の窓で設定します。
4. **外から見る。** DebugStudio を起動して Play 中のログと telemetry を読みます（下の DebugStudio の節）。

手順 1 は 2026-11-07 に Unity 6000.6.0f1 で 1 回確認しました。確認したのは起動・Start・spawn 位置・距離によるセルの load / unload で、起動は menu と同じ入口の直接呼び出し、移動は Teleport で行ったため、menu のクリックとキー入力での飛行は未確認です。手順 2 と 3 は menu と経路をコードで照合しただけで、実走はまだです。

テストは Unity を batchmode で起動して回します。対象 project を開いている Editor は先に閉じます。既定では `D:\UnityEditor\<版>\Editor\Unity.exe` を探すので、Unity Hub の標準の場所に入れている場合は `-UnityExe` で指定します。

```powershell
pwsh tools/run-tests.ps1 -UnityExe "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe"
```

## まだできないこと

- **季節の遷移。** 4 季の Scene と `all-full` の build はあるが、Play 中に季節を跨ぐ遷移はまだ無い（Spring 固定で起動する）。
- **構造編集の衝突。** 床と置き物の Scene を分けたので中身の編集は衝突しないが、`SceneResourceMap` と `Season_*` の子リストは構造変更のたびに全員が触る 1 file のままで、並行する構造編集の衝突は解けていない。
- **編集保護。** 手を入れたセルを生成物から守る判定は未着手。
- **性能の数字。** Streaming の計測は設計だけで、公開できる数値はまだ無い。
- **Windows 以外。** Content Directory の target は `StandaloneWindows64-Player` だけ。
- **配信の作り込み。** 署名・差分配信・CDN・latest channel は無い。
- **複雑なゲームルール。** InGame に載っているのは飛行とセル streaming で、ゲームルールの複雑さはまだ投入していない。

## 構成

```
.
├── unity/                       Unity プロジェクト（ここを Unity Hub で開く）
│   └── Assets/
│       ├── OneStarMaker/        フレームワーク本体
│       ├── SampleGame/          検証用ゲーム（Game → Framework の一方向依存）
│       └── Docs/Architecture/   設計ドキュメント
├── tools/
│   ├── DebugStudio/             .NET 8 デスクトップデバッグツール
│   └── Artifacts/, Harness/     開発用の成果物保存と作業の記録
└── docs/                        目標・現況仕様・進行中の作業台（handoff）
```

## ドキュメント

- 何を主張し、何を主張しないか: [`docs/GOALS_AND_STRENGTHS.md`](docs/GOALS_AND_STRENGTHS.md)
- Unity 内の設計の入口: [`unity/Assets/ARCHITECTURE.md`](unity/Assets/ARCHITECTURE.md)
- 設計の各章: [`unity/Assets/Docs/Architecture/`](unity/Assets/Docs/Architecture/)
- Play As / Verify Play / Delivery の現況: [`18-asset-description.md` §4](unity/Assets/Docs/Architecture/18-asset-description.md#4-buildsystem-との接続)
- Content Directory と DIST の背景: [`20-variant-checkout-workflow.md`](unity/Assets/Docs/Architecture/20-variant-checkout-workflow.md)（Editor Play の入口は §18 §4 の方が新しい）
- UpdateSystem の正本仕様: [`docs/updater/UPDATER_CURRENT_SPEC.md`](docs/updater/UPDATER_CURRENT_SPEC.md)
- Streaming の現状仕様: [`docs/streaming/STREAMING_CURRENT_SPEC.md`](docs/streaming/STREAMING_CURRENT_SPEC.md)（到着契約は [§34](unity/Assets/Docs/Architecture/34-ondemand-spatial-policy.md)）
- 文書の置き方の方針: [`docs/README.md`](docs/README.md)

コミットされているドキュメントは**現況を説明するものだけ**です。`docs/handoff/` だけは例外で、進行中のスライスの作業指示と複数スライスに跨る計画を置き、マージ時に恒久的な内容を `Docs/Architecture/` へ移してから削除します。

## DebugStudio

Unity の DebugSocket（connect モード）とペアで動作します。

```powershell
cd tools/DebugStudio
dotnet build DebugStudio.sln
dotnet test DebugStudio.sln
```

アプリ起動（WPF）:

```powershell
dotnet run --project src/DebugStudio.App/DebugStudio.App.csproj
```

既定の接続先は `ws://127.0.0.1:5011/debugsocket/`（`app-config.json` の `debugSocket:connectUri` と一致させる）。

## ブランチ

**統合先は `develop`。`main` は Initial commit しか持たない。**

| ブランチ | 役割 |
|---|---|
| `develop` | GitHub の既定ブランチ・統合先。PR の base は常にこれ |
| `main` | Initial commit のみ。リリースタグ用に空けてある |
| `feat/*` `fix/*` `chore/*` `docs/*` | 1 スライス = 1 ブランチ。`develop` から切る |

```bash
gh pr create --base develop
```

GitHub の既定も `develop` だが、作業環境の既定値に依存せず `--base develop` を明示する。`main` を base にした PR は統合後の全履歴を含み得るため作らない。

## 環境

| 項目 | バージョン |
|---|---|
| Unity | **6.6 (6000.6.0f1)** |
| .NET SDK | 8.0+（DebugStudio 用） |

## 使用ライブラリ

版はここに書きません。各ライブラリの版は次のファイルが正です。

- Unity（UPM）: `unity/Packages/manifest.json`（宣言）と `unity/Packages/packages-lock.json`（解決結果）
- NuGet（NuGetForUnity 経由）: `unity/Assets/packages.config`
- DebugStudio: `tools/DebugStudio` 配下の各 `.csproj` の `PackageReference`

### Unity（UPM）

| ライブラリ | 取得元 | 用途 |
|---|---|---|
| [URP](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest) | Unity Registry | レンダリング |
| [Addressables](https://docs.unity3d.com/Packages/com.unity.addressables@latest) | Unity Registry | 互換 backend（通常経路は Content Directory） |
| [Input System](https://docs.unity3d.com/Packages/com.unity.inputsystem@latest) | Unity Registry | 入力 |
| [uGUI](https://docs.unity3d.com/Packages/com.unity.ugui@latest) | Unity Registry | UI |
| [Test Framework](https://docs.unity3d.com/Packages/com.unity.test-framework@latest) | Unity Registry | Unity テスト |
| [UniTask](https://github.com/Cysharp/UniTask) | Git (UPM) | async/await |
| [LitMotion](https://github.com/annulusgames/LitMotion) | Git (UPM) | Tween |
| [CsprojModifier](https://github.com/Cysharp/CsprojModifier) | Git (UPM) | `.csproj` 生成調整 |
| [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity) | Git (UPM) | NuGet → Unity 取り込み |

### NuGet（NuGetForUnity 経由）

| ライブラリ | 用途 |
|---|---|
| [VContainer](https://github.com/hadashiA/VContainer) | **未使用**（手動 DI を採用。パッケージは将来削除予定） |
| [R3](https://github.com/Cysharp/R3) | Reactive Extensions |
| [ObservableCollections](https://github.com/Cysharp/ObservableCollections) | コレクション変更通知 |
| [ZLogger](https://github.com/Cysharp/ZLogger) | 構造化ログ（`ILogger<T>` + ZLogger 拡張） |
| [ZString](https://github.com/Cysharp/ZString) | ゼロアロケ文字列 |
| ZStringFormatExtension | ZString 拡張 |
| [MessagePack](https://github.com/MessagePack-CSharp/MessagePack-CSharp) | DebugSocket / テレメトリ直列化 |

推移的依存として [Microsoft.Extensions.*](https://www.nuget.org/packages/Microsoft.Extensions.Logging)、[System.Text.Json](https://www.nuget.org/packages/System.Text.Json) などを同梱。

### DebugStudio（`tools/DebugStudio`）

| ライブラリ | 用途 |
|---|---|
| MessagePack | DebugSocket プロトコル |
| [AvalonDock](https://github.com/Dirkster99/AvalonDock) | WPF ドッキング UI |
| xUnit | テスト（開発時のみ） |
