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
| [ZLogger](https://github.com/Cysharp/ZLogger) | 2.5.10 | 構造化ログ（`ILogger<T>` + ZLogger 拡張） |
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
└── docs/                  現況を説明する設計ドキュメント
```

## ブランチ

**統合先は `develop`。`main` は Initial commit しか持たない。**

| ブランチ | 役割 |
|---|---|
| `develop` | 実質の既定ブランチ。PR の base は常にこれ |
| `main` | Initial commit のみ。リリースタグ用に空けてある |
| `feat/*` `fix/*` `chore/*` | 1スライス = 1ブランチ。`develop` から切る |

```bash
gh pr create --base develop
```

`--base` を省くとホスティング側の既定（`main`）に向いてしまい、`develop` 以降の全履歴が差分に乗ってレビュー不能になる。

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

- このリポジトリが何を主張しているか: [`docs/GOALS_AND_STRENGTHS.md`](docs/GOALS_AND_STRENGTHS.md)
- Unity 内メイン設計書: [`unity/Assets/ARCHITECTURE.md`](unity/Assets/ARCHITECTURE.md)
- アーキテクチャ詳細: [`unity/Assets/Docs/Architecture/`](unity/Assets/Docs/Architecture/)
- UpdateSystem 正本仕様: [`docs/updater/UPDATER_CURRENT_SPEC.md`](docs/updater/UPDATER_CURRENT_SPEC.md)

コミットされているドキュメントは**現況を説明するものだけ**である。計画書・外部フレームワーク比較・発表資料は作者の手元にのみ置き、リポジトリには含めない。

例外は `docs/handoff/` で、ここには**進行中のスライス 1 本分の作業指示だけ**が入る（実装を別セッション / 別ツールへ渡すため git worktree に持っていく必要がある）。マージ時に恒久的な内容を `Docs/Architecture/` へ移してから削除する。方針の正本は [`docs/README.md`](docs/README.md)。