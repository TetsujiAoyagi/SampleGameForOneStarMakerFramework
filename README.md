# SampleGameProject

OneStarMaker 汎用フレームワーク + SampleGame ゲーム層 + DebugStudio 開発ツールの試作リポジトリです。
古典的なSceneStream、MonobehaviourによらないUpdateSystem、テレメトリ、ロギング機構などを提供しつつ
外部ツールDebugStudioでLogViewerとテレメトリのElastic用加工を行い。かつDebug機能の拡充を視野に入れるプロジェクトです。
大絶賛工事中です、コードにはAIを活用しています。
また以下のライブラリを使用しています。

## 使用ライブラリ

バージョンは `unity/Packages/manifest.json` および `unity/Assets/packages.config`（2026-06 時点）に準拠。

### Unity（UPM）— フレームワークで主に利用

| ライブラリ | バージョン | 用途 |
|---|---|---|
| [URP](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest) | 17.5.0 | レンダリング |
| [Addressables](https://docs.unity3d.com/Packages/com.unity.addressables@latest) | 2.9.1 | アセット / シーンロード |
| [Input System](https://docs.unity3d.com/Packages/com.unity.inputsystem@latest) | 1.19.0 | 入力 |
| [uGUI](https://docs.unity3d.com/Packages/com.unity.ugui@latest) | 2.5.0 | UI |
| [LitMotion](https://github.com/annulusgames/LitMotion) | Git (UPM) | Tween |
| [CsprojModifier](https://github.com/Cysharp/CsprojModifier) | Git (UPM) | `.csproj` 生成調整 |
| [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity) | Git (UPM) | NuGet → Unity 取り込み |
| [Test Framework](https://docs.unity3d.com/Packages/com.unity.test-framework@latest) | 1.7.0 | Unity テスト |

### NuGet（NuGetForUnity 経由）— Cysharp 系・ロギング等

| ライブラリ | バージョン | 用途 |
|---|---|---|
| [VContainer](https://github.com/hadashiA/VContainer) | 1.0.2 | **未使用**（手動 DI を正式採用したため。パッケージは将来削除予定） |
| [UniTask](https://github.com/Cysharp/UniTask) | 2.5.10 | async/await |
| [R3](https://github.com/Cysharp/R3) | 1.3.0 | Reactive Extensions |
| [ObservableCollections](https://github.com/Cysharp/ObservableCollections) | 3.3.4 | コレクション変更通知 |
| [ZLogger](https://github.com/Cysharp/ZLogger) | 2.5.10 | 構造化ログ（`IAppLogger<T>` で隠蔽） |
| [ZString](https://github.com/Cysharp/ZString) | 2.6.0 | ゼロアロケ文字列 |
| ZStringFormatExtension | 0.0.6 | ZString 拡張 |
| [MessagePack](https://github.com/MessagePack-CSharp/MessagePack-CSharp) | 3.1.4 | DebugSocket / テレメトリ直列化 |

推移的依存として [Microsoft.Extensions.*](https://www.nuget.org/packages/Microsoft.Extensions.Logging) 8.0.0、[System.Text.Json](https://www.nuget.org/packages/System.Text.Json) 8.0.5 などを同梱。

### DebugStudio（`tools/DebugStudio`）

| ライブラリ | バージョン | 用途 |
|---|---|---|
| MessagePack | 3.1.4 | DebugSocket プロトコル |
| [AvalonDock](https://github.com/Dirkster99/AvalonDock) | 4.74.1 | WPF ドッキング UI |
| xUnit | 2.5〜2.9 | テスト（開発時のみ） |

## リポジトリ構成

```
SampleGameProject/
├── unity/                 Unity プロジェクト（ここを Unity Hub で開く）
│   ├── Assets/
│   │   ├── OneStarMaker/  汎用 FW
│   │   └── SampleGame/    ゲーム固有実装
│   ├── Packages/
│   └── ProjectSettings/
├── tools/
│   └── DebugStudio/       .NET 8 デスクトップデバッグスイート
└── docs/                  設計・計画ドキュメント(ルート由来の md)
```

## 環境


| 項目       | バージョン                |
| -------- | -------------------- |
| Unity    | **6.5 (6000.5.0f1)** |
| .NET SDK | 8.0+（DebugStudio 用）  |


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

## ドキュメント

- Unity 内メイン設計書: `unity/Assets/ARCHITECTURE.md`
- アーキテクチャ詳細: `unity/Assets/Docs/Architecture/`
- UpdateSystem 正本仕様: `docs/updater/UPDATER_CURRENT_SPEC.md`