# OneStarMaker (OSM)

**Unity で準オープンワールド規模のゲームを作るための土台**と、それを実際に載せて確かめるサンプルゲームのリポジトリです。

> OneStarMaker is a Unity 6 framework for semi-open-world games: scene lifetime as a tree, distance-based cell streaming, purpose-scoped content builds, and out-of-process observability. The sample game in this repo exercises all of it. Docs are in Japanese.

まだ試作段階で、コードには AI を活用しています。

## 目的

Unity で世界が広いゲームを作ると、ゲームそのものとは別の場所で同じ苦労を毎回やり直すことになります。

- どの Scene がいつ居て、いつ使える状態になるかを人が暗記している
- 1 箇所直すために全部を build して待つ
- 同じ場所を 2 人で触るとファイルが衝突する
- Player の中で何が起きているかが外から見えない

OSM は、この「ゲーム以外の部分」をフレームワーク側が引き受けることを目的にしています。Scene の寿命を木として宣言し、距離でセルを stream し、目的ごとに必要な分だけ build して Play し、ログと telemetry を外のツールで見る。ゲームが複雑になっても、寿命・UI・Service・ロードまで一緒に複雑化しないための境界と契約を、実装とドキュメントの両方で公開します。

ゲームルールを簡単にする道具ではありません。何を主張し、何をまだ主張しないかは [`docs/GOALS_AND_STRENGTHS.md`](docs/GOALS_AND_STRENGTHS.md) に線を引いてあります。

## できること

今日この repo を開いて動くものです。

- **Scene の寿命を木で持つ。** 親子と LoadType（NecessaryAlways / OnDemand / IncrementalAlways）で「いつ居るか」を宣言し、14 状態の lifecycle を単一の owner が進める。Scene Graph Editor が木を検証して `SceneResource` を生成する。
- **世界を距離で stream する。** サンプルは 4 季 × 9 × 6 セル（1 セル 250 m）で、床の Scene と置き物の Scene を職種ごとに分けて持つ。飛行 rig の位置から LoadRadius 375 m / UnloadRadius 550 m でセルを load / unload する。
- **目的ごとに必要な分だけ build する。** `spring-full`、`spring-whitebox`、`spring-planner-whitebox`、`all-full` などの宣言を選ぶと、その季節・職種・表現（Full / Whitebox）に必要な Scene だけを Content Directory として build する。同じ revision の上書きは拒否する。
- **build せずに source から Play する。** `Play As` で宣言を選ぶと、Asset Database のまま同じ表現・同じ companion 集合で Play に入る。
- **build から Play まで menu 一発で通す。** `Verify Play` は build → publish → install → Play を一続きで行い、途中で失敗したら環境を変えずに止まる。
- **published directory を受け取って Play する。** `Delivery` 窓でローカル / LAN の directory か HTTP から revision を取得し、SHA-256 で照合して install する。
- **MonoBehaviour に依らない Update。** `UpdateSystemRuntime` に登録した system を決まった順序で回し、1 つの例外で他を止めない。
- **ゲームの外から観測する。** ZLogger ベースの構造化ログと telemetry を DebugSocket で外に出し、.NET 8 の DebugStudio で読む。
- **依存の向きを機械で守る。** Game → Framework の一方向を asmdef で強制し、全体配線は `DependOnAll` の 1 箇所に集める。

## 試す

Unity **6000.6.0f1** で `unity/` を開きます（正確な版は `unity/ProjectSettings/ProjectVersion.txt`）。

1. **まず飛ぶ。** `Tools/OSM/Play As/spring-full` を選ぶと source から Play に入ります。Title で Start すると Spring の `(0,4)` セルに spawn し、飛行 rig で移動するとセルが距離で load / unload されます。
2. **必要な分だけ build する。** `Tools/OSM/Content/Build spring-full` で Spring の Full 表現だけを Content Directory として build します。`spring-whitebox` にすると同じ世界が Whitebox 表現で出ます。
3. **build した物で Play する。** `Tools/OSM/Content/Verify Play/spring-full` で build → publish → install → Play を一続きで行います。cache root などは `Tools/OSM/Content/Delivery` の窓で設定します。
4. **外から見る。** DebugStudio を起動して Play 中のログと telemetry を読みます（下の DebugStudio の節）。

テストは Unity を開かずに次で回ります。

```powershell
pwsh tools/run-tests.ps1
```

## まだできないこと

- **季節の遷移。** 4 季の Scene と `all-full` の build はあるが、Play 中に季節を跨ぐ遷移はまだ無い（Spring 固定で起動する）。
- **編集保護。** 手を入れたセルを生成物から守る判定は未着手。
- **性能の数字。** Streaming の計測は設計だけで、公開できる数値はまだ無い。
- **Windows 以外。** Content Directory の target は `StandaloneWindows64` だけ。
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
- Content Directory と Delivery の手順: [`20-variant-checkout-workflow.md`](unity/Assets/Docs/Architecture/20-variant-checkout-workflow.md)
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
