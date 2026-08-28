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

- **ローカル（人間が Editor を開いている）:** `unity status` が `ready` なら `unity command` / `unity eval` でシーンとアセットを触ってよい。
- **Cloud / Editor が無いマシン:** Unity CLI を叩かない。C# と git 上の宣言（manifest / HANDOFF）だけ書く。

`com.unity.pipeline` は `unity/Packages/manifest.json` に宣言済み。`packages-lock.json` はローカルで Editor を開いたときに UPM が書く。手で lock を捏造しない。バージョンが Editor 側でずれたら lock を正とする。

## やってよい

1. `unity status` で接続を確認する（`ready`）。
2. `unity command` で **この Editor が公開している名前** を見る。推測でコマンド名を固定しない。
3. 名前付き command を先に使う。Pipeline 0.4 系なら `move_asset` / `open_scene` / `save_scene` / `set_transform` / `menu` が候補。
4. 名前付き command が足りないときだけ `unity command eval`。
5. 生成器 1 回は既存メニュー `OneStarMaker/Sample/Create World + Cell Streaming Slice`（`WorldCellStreamingSliceCreator.CreateFromMenu`）を `menu` か eval で叩く。

## やってはいけない

- Unity.exe の起動、`unity test`、`unity run`、`pwsh tools/run-tests.ps1`、Addressables ビルド
- 接続できる Editor があるときの `.unity` / `.prefab` / `.asset` YAML 手直し
- 単独の `git mv` をシーン移送の正本にする（GUID は `move_asset` が運ぶ）
- Safe Mode（コンパイルエラーで Pipeline が載らない）を YAML 編集で迂回する。C# を直して Editor を立て直す
- Cloud セッションで `unity` バイナリを入れて接続確認したことにする

## テスト

実装エージェントはテストを走らせない。報告は「実装完了。テスト未実行」。`run-tests.ps1` は Phase C。
