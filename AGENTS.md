# AGENTS.md

Unity フレームワーク **OneStarMaker (OSM)** と、その検証用サンプルゲームのリポジトリ。

OSM は、複雑なゲームを載せても Scene / 寿命 / UI / Service / ロードまで一緒に複雑化しないための、境界・寿命・依存の契約を実装とドキュメントの両方で公開する。詳細は `docs/GOALS_AND_STRENGTHS.md`。

**このファイルは常時必要な地図と事故防止の契約だけを置く。** 作業手順は末尾の「作業別の追加指示」から共通 Skill を読むこと。解釈に迷った場合、この常時契約を `unity/Assets/Docs/Architecture/` の個別文書より優先する。

## 構成

- `unity/Assets/OneStarMaker/`: フレームワーク本体
- `unity/Assets/SampleGame/`: 検証用ゲーム
- `tools/DebugStudio/`: 外部デバッグツール（.NET 8）
- `unity/Assets/Docs/Architecture/`、`docs/`: 公開設計ドキュメント

Unity の正しいバージョンは `unity/ProjectSettings/ProjectVersion.txt` にある（現在は 6000.5.0f1）。

## 常時守る契約

- **依存は Game → Framework の一方向。** フレームワーク内と SampleGame 内に循環を作らず、全体配線は `DependOnAll` に集約する。正確な依存図は `unity/Assets/README.md`。asmdef 参照の追加は設計判断なので勝手に行わない。
- **アセットは `IAssetManagement` 経由。** `AssetOwner`（`App` / `Manual` / `Scene(id)` / `Bind(go)`）で寿命スコープを宣言する。
- **`SceneState` は既存の14値を減らさず、並べ替えない。** enum 順序は整数比較のガードに使われる。追加するなら、既存14値のどれが不足するかを先に HANDOFF に書く。状態変更は `SceneLifecycleManager` が所有する。
- **公開 API のログ抽象は `ILogger<T>`。** `ZLogger*` 型を公開面へ出さず、実装詳細に留める。
- **Update は `UpdateSystemRuntime` に登録する。** 1 フレームの順序は `ActivatePendingRegistrations` → `RunUpdate` → `RunLateUpdate` → `ApplyMainThreadChanges` → `ApplyStructuralChanges`。
- **1つの `UpdateSystem` の例外で、他のシステムの `Tick` を止めない。**
- **Editor コードを Runtime アセンブリに置かない。** `UnityEditor` 依存は Editor 用 asmdef に隔離する。
- **Unity側のC#で `record` を使わない。** `OneStarMaker.Runtime` にだけ `internal` な `IsExternalInit` polyfill があるが、他の asmdef からは利用できない。Unity側では一律に禁止し、`tools/DebugStudio` の .NET 8 コードには適用しない。
- **新規または編集する Unity側の `.cs` は先頭に `#nullable enable` を置く。** 既存ファイルからも外さない。未対応の既存ファイルは残っており、一括整備は別スライスとする。
- **Unity の偽 null を忘れない。** 破棄されうる `UnityEngine.Object` に `?.` / `??` / `is null` / `ReferenceEquals` を使わず、`== null` / `!= null` で判定する。
- **Phase B の実装担当は Unity.exe を起動しない。** `pwsh tools/run-tests.ps1` と Addressables ビルドも実行しない。`unity test` / `unity run` はどの Phase でも使わない。`unity/Library/` と `unity/Temp/UnityLockfile` はブランチを捨てても残る。例外は、人間が既に開いている Editor へ `unity status` / `unity command` / `unity eval` で接続する操作だけで、詳細は `osm-unity-editor` Skill に従う。
- **テストで `Task.Delay` / `Thread.Sleep` を使わない。** 待機はシグナル等へのリアクティブな待機にするか、時間を注入して進める。
- **参照 0 を削除理由にしない。** 未使用 API は意図的な先行宣言やフェーズ外の場合があり、置き換え残骸と確認できたものだけが削除候補になる。
- **PR の base は `develop`。** `main` は既定ブランチとして使わない。

## 作業別の追加指示

以下に該当する作業では、着手前に対応する `SKILL.md` を全文読む。

- 計画、HANDOFF、実装、レビュー、独立監査、テスト、ドキュメント、ブランチ、PR:
  `.agents/skills/osm-workflow/SKILL.md`
- Unity Scene、Prefab、SceneResource、Addressables、AuthoredRoot、WorldCell を Editor 経由で操作:
  `.agents/skills/osm-unity-editor/SKILL.md`
- Unity CLI 自体の一般操作:
  `.agents/skills/unity-cli/SKILL.md`
  - このリポジトリでは `osm-unity-editor` の制約を優先する。

Skill を自動検出しないエージェントも、上記パスを直接読んで同じ指示に従う。
