---
name: osm-unity-editor
description: >-
  Use when editing Unity scenes, SceneResource assets, WorldCell generator menus,
  Addressables, or AuthoredRoot transforms in this OneStarMaker / SampleGame
  repository. Overrides upstream unity-cli for this repo: drive an already-open
  Editor with named Pipeline commands or eval; never launch Unity.exe, never run
  tests from the implementation agent, never YAML-edit .unity when an Editor is
  reachable. Cloud agents have no Editor — do not invoke the Unity CLI there.
---

# OSM Unity Editor（このリポジトリの上書き）

公式 `unity-cli` skill より **このファイルを先に守る。** 公式が載せる `unity test` / `unity run` / Editor インストール / ヘッドレス起動は、このリポジトリでは実装エージェントの仕事ではない。

## 環境

- **ローカル（人間が Editor を開いている）:** `tools/unity-editor.cmd status` が `ready` なら、同ラッパーの `command` / `eval <UTF-8 Base64>` でシーンとアセットを触ってよい。
- **Cloud / Editor が無いマシン:** Unity CLI を叩かない。C# と git 上の宣言（manifest / HANDOFF）だけ書く。

`com.unity.pipeline` は `unity/Packages/manifest.json` に宣言済み。`packages-lock.json` はローカルで Editor を開いたときに UPM が書く。手で lock を捏造しない。バージョンが Editor 側でずれたら lock を正とする。

## 標準入口と権限

- ローカルのEditor操作はリポジトリルートから `tools/unity-editor.cmd status` / `tools/unity-editor.cmd command ...` を使う。ラッパーはPATH上のUnity CLIを優先し、無ければ `%LOCALAPPDATA%\Unity\bin\unity.exe` を解決し、通常はこのリポジトリの `unity/` を接続先に固定する。CD0 E7の使い捨てhostだけは `--cd0-player-host` を先頭へ付け、固定path `artifacts/cd0/player-host/unity/` へ接続してよい。任意project path指定には一般化しない。
- Cursor IDE / Cursor CLI / Claude Code / Codexでは、任意のshell全体ではなく `tools/unity-editor.cmd` のcommand prefixだけを永続許可する。`pwsh`、`unity.exe`、`Unity.exe` 全体を無確認にしない。
- 設定例はCursor IDEの `terminalAllowlist` に `tools\\unity-editor.cmd`、Cursor CLIのallowに `Shell(tools\\unity-editor.cmd)`、Claude Codeのallowに `Bash(tools/unity-editor.cmd:*)`。Codexは承認画面で同prefixをAlways allowにする。個人設定はgit管理しない。
- prefix allowlistがshell制御演算子の後続まで許す実装もあり得る。`tools/unity-editor.cmd ... && 別command` のように連結せず、1 tool callをラッパー1 invocationだけにする。allowlistは安全境界ではなく承認疲れを減らす補助と扱う。
- ラッパーは既に開いているEditor用の `status`、`command`、任意C#用の `eval <UTF-8 Base64>`、Safe Mode診断用の `pipeline list` だけを通す。`.cmd`のquote再解釈を避けるため、C#はUTF-8 bytesをBase64化して `eval` に1引数で渡す。CLIのinstall/update、Editorのopen、headless run/test/build、MCP設定は通常の個別承認でUnity CLIを直接使う。
- Unity CLIは更新してよい。更新後は `unity --version`、ラッパーの `status`、`command` discoveryを再確認し、公開command名を推測で固定しない。
- Unity MCPは任意の補助経路。人間が希望し、導入済みで到達可能なら使ってよいが、計画・実装・テストの前提条件にしない。MCPが無い/壊れている場合もPipeline CLIで同じ作業を継続できること。

## やってよい

1. `tools/unity-editor.cmd status` で接続を確認する（`ready`）。
2. `tools/unity-editor.cmd command` で **この Editor が公開している名前** を見る。推測でコマンド名を固定しない。
3. 名前付き command を先に使う。Pipeline 0.4 系なら `move_asset` / `open_scene` / `save_scene` / `set_transform` / `menu` が候補。
4. 名前付き command が足りないときだけ `tools/unity-editor.cmd eval <UTF-8 Base64>`。PowerShellなら `$encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($code))` で作る。
5. S-4b P3 以降、`WorldCellStreamingSliceCreator` / `SeasonWorldGenerationCommand` メニューは HEAD に無い。Generate を再実行しない。

## Phase B でやってはいけない

- Unity.exe の起動、`unity test`、`unity run`、`pwsh tools/run-tests.ps1`、Addressables ビルド

## Editor 操作でやってはいけない

- 接続できる Editor があるときの `.unity` / `.prefab` / `.asset` YAML 手直し
- 単独の `git mv` をシーン移送の正本にする（GUID は `move_asset` が運ぶ）
- Safe Mode（コンパイルエラーで Pipeline が載らない）を YAML 編集で迂回する。C# を直して Editor を立て直す
- Cloud セッションで `unity` バイナリを入れて接続確認したことにする

## テスト

Phase B の実装エージェントはテストを走らせず、「実装完了。テスト未実行」と報告する。Phase C は Editor を閉じてから `osm-workflow` に従って `run-tests.ps1` を実行してよい。`unity test` / `unity run` は Phase C でも使わない。
