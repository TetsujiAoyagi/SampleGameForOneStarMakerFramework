# S-4a — Runtime Variant と職種 Workspace

## 0. メタデータ

- type: `slice`
- status: `A3 freeze candidate / purchaser decision pending`（architecture gate PASS。発注者が明示凍結するまで Phase B へ渡さない）
- branch: `codex/s-4a-runtime-variant-workspace-plan`
- implementation base commit: `be33e4716c70334100aada23c555ac75a0b0dbb7`
- implementation head commit: 未到達
- risk: `high`
- owner: 発注者（freeze 判断）/ 将来の S-4a Phase B 実装担当
- created: `2026-09-06`
- expires: S-4a Phase D で §04 / §05 / §18 / §27 / `docs/streaming/STREAMING_CURRENT_SPEC.md` へ harvest した時点
- harvest to: `unity/Assets/Docs/Architecture/04-app-startup.md`、`05-scene.md`、`18-asset-description.md`、`27-folder-structure.md`、`docs/streaming/STREAMING_CURRENT_SPEC.md`
- Phase A snapshot path / id: `docs/handoff/evidence/S-4A_PHASE_A_INPUT.txt` / `S-4a-A0-be33e47-20260906T103508+0900`
- Phase A snapshot generated at: `2026-09-06T10:35:08.3818239+09:00`
- Phase A snapshot hash: `sha256:15b2facfaabec89933ad73a5af92b48cfc78398f6e649e59bfad5af1339a40ce`
- Phase B result snapshot path / id: 未到達
- Phase B result snapshot generated at: 未到達
- Phase B result snapshot hash: 未到達
- evidence bundle path / id: 未到達
- evidence bundle generated at: 未到達
- evidence bundle hash: 未到達
- C' blind bundle path / id: 未到達
- C' blind bundle generated at: 未到達
- C' blind bundle hash: 未到達

## 1. 目的と対象外

### 1.1 目的

S-4 program から S-4a だけを切り出し、次の三つを一つの移行口として完成させる。

1. 起動時に一度だけ解決した Scene payload Variant を `SceneDirector` が全 Unity Scene load へ渡す。
2. Cell のロード単位と職種 child の選択を `SceneResource` 構造へ移し、修飾付き identity を座標 parse せず実行できるようにする。
3. season・座標・職種から既定の Scene set を安全に開き、任意の Cell Lighting / VFX / Events を一貫した Editor transaction で作る World Workspace を用意する。

この HANDOFF は Phase B が設計判断を追加せず実装できる粒度まで API、所有権、配置、テスト境界、失敗時挙動を固定する。

### 1.2 対象外

- S-4b の 4 season × 9 × 6 Cell / Environment、SceneResource、Addressables の大量生成・旧 4 × 4 world の wipe・spawn 移動・カメラ・代表 Cell の見た目。
- S-4c の `IRenderEnvironment`、Lighting、Volume、Post Process、品質 Tier。
- S-4d の VFX Graph package、PC/Mobile VFX 実装、Planner trigger、VFX の実コンテンツ。
- 実シーン内容、Lighting、VFX、Events の制作。Workspace が新規作成する任意 scene は空の編集単位までで、内容は後続 slice が所有する。
- 実行中の Variant / companion set 切替。変更の反映には Play Mode / Player の再起動を要求する。
- `SceneState` の追加・削除・並べ替え、新しい `LoadType`、第二の距離ストリーミング経路、asmdef 参照追加。
- OneStarMaker Runtime への season 名、座標文法、Environment / Lighting / VFX / Events の導入。
- Scene / asset YAML の直接編集、Unity CLI の再インストール、Unity Editor の新規起動。
- Phase B、C、C'、D の実施。本依頼の完了点は、レビュー統合済み freeze candidate を発注者へ返すところまで。

### 1.3 現況

- `SceneDirector.Loading.cs` は `IAssetManagement.LoadSceneAsync` の Variant に常に `string.Empty` を渡す。`IAssetManagement` と `SceneAssetDescription` はすでに Variant と「要求 Variant がなければ空 Variant」の fallback を実装済みである。
- `AbstractApplicationInitializer` の config provider 順は JSON → environment → command line で、後勝ちである。Editor runtime bridge の先例は remote catalog にある。
- `BuildVariantProfile` / `DeveloperVariantSettings` は whitelist と active profile を扱うが、Play Mode の Scene Variant 値を持たない。`WorldWhitebox` profile もない。
- `VariantPlayerBuild` は active profile の `FirstSceneIdentify` だけを build 中の `app-config.json` へ一時挿入し、Scene Variant は書かない。したがって profile に Whitebox を追加するだけでは Player の起動値は空のままである。
- `GameSceneFactory` は `CellIdentity` / `EnvironmentIdentity` で型を決める。`CellScene`、`DemoCellScene`、`EnvironmentScene` は runtime で座標を parse し、見た目を計算する。
- `SessionCellChildLoadDriver` は親 Cell identity から Environment identity を組み立てる。一方、stable gate、in-flight 重複防止、Add 中に親が消えた場合の明示 unload はすでに持つ。
- `SceneResource` は `Parent`、`Children`、`Volume`、`StreamByDistance`、payload 列挙をすでに公開している。この slice に新しい Framework 構造 API は不要である。
- 現行 bulk generator は 1212 行で、旧 4 × 4 専用である。日常 Workspace を同居させない。
- `SceneResourceMap` と `SceneResource` は生成物であり、正本は `Assets/SceneGraphData/Nodes` の `SceneNodeData` と `SceneGraphEdges` である。標準 `SceneResourceGenerator.Generate` は正本から parent/child、Map、`GenerateHash` を全再構築し、orphan resource を削除する。Workspace が生成物だけを直接 upsert してはならない。
- 現行 scene/resource の Cell payload は空 Variant だけである。全 Cell の Whitebox payload 作成は S-4b であり、S-4a の Workspace は不足を明示して勝手に fallback しない。

## 2. 受け入れ条件と制約

### 2.1 受け入れ条件

#### Runtime Variant

- RV-1: `SceneDirector` の public constructor は末尾に `string sceneVariant = ""` を受け、null は `ArgumentNullException`、空文字は正当な Production 値として受け入れる。値は private readonly field に保存し、公開 setter/getter を作らない。
- RV-2: Addressables を使うすべての Unity scene load は、その保存値を既存 `IAssetManagement.LoadSceneAsync` へ渡す。すでに同名 Scene が開いている場合と payload を持たない論理 node は従来どおり backend load を行わない。
- RV-3: `SceneAssetDescription.FindPayload` と `IAssetManagement` は変更せず、`Whitebox` payload がない child は空 Variant payload へ fallback する。
- RV-4: Player は `assets:sceneVariant` を既存 AppConfig から読む。provider 優先順位は JSON < environment < command line のまま。key 欠落時は空文字、値の trim・大小文字変換はしない。
- RV-5: Editor では active `BuildVariantProfile` の Scene Variant が config より優先する。bridge の戻り値 `null` は active profile なし、`""` は active Production profile の明示選択であり、この二つを区別する。active profile なしの場合だけ AppConfig へ戻る。Player build は editor resolver をコンパイル時に参照せず AppConfig だけを使う。
- RV-6: 解決は `SceneDirector` 構築直前に一回だけ行う。以後の UserSettings/config 変更は実行中の値を変えない。
- RV-7: `BuildVariantProfile.SceneVariant` を Editor 公開読取 API として追加する。`WorldWhitebox.asset` は Scene Variant `Whitebox`、whitelist は `""` と `"Whitebox"`。`Production.asset` は Scene Variant `""` で whitelist に Whitebox を含めない。
- RV-8: `app-config.json` に `assets:sceneVariant = ""` を追加する。環境変数は既存 prefix / `__` 変換、command line は既存 `--assets.sceneVariant=...` / `--assets:sceneVariant=...` 解釈をそのまま使い、新しい provider を作らない。
- RV-9: `VariantPlayerBuild` は active profile の `SceneVariant` を Player build 中だけ `app-config.json` の `assets:sceneVariant` へ upsert する。これは最低優先の JSON 値なので environment / command line の上書きを妨げない。元ファイルは `byte[]` で退避し、build 成否や例外にかかわらず `finally` で byte-for-byte 復元して再 import する。WorldWhitebox profile から build した Player が `Whitebox` を受け取ることを Editor test で固定する。
- RV-10: non-empty `BuildVariantProfile.SceneVariant` は同 profile の whitelist に ordinal 完全一致で含まれなければ settings validation、Editor startup resolver、Player build を失敗させる。empty は default payload として常に合法である。不整合を default fallback で隠さない。

#### Companion set と identity independence

- CP-1: `world:cellCompanionSet` は key 欠落時だけ `Full`。値は `Full`、`Planner`、`Lighting`、`VFX` の ordinal 完全一致だけを受理し、空、空白、大小文字違い、未知値は startup の `FormatException` にする。
- CP-2: role inclusion は `Full = Environment + Lighting + VFX + Events`、`Planner = Events`、`Lighting = Environment + Lighting`、`VFX = Environment + Lighting + VFX` で固定する。
- CP-3: `AppInitializer.CreateSceneFactory` で config を一度 parse し、immutable enum 値を `GameSceneFactory` → `InGameSessionScene` → `SessionCellCompanionLoadDriver` へ constructor injection する。static mutable state は使わない。invalid config は SceneDirector 構築前に startup を失敗させる。
- CP-4: Cell の分類は `SceneResource.StreamByDistance == true` だけを使う。Cell child の分類は `sceneResource.Parent != null && sceneResource.Parent.StreamByDistance` だけを使う。Unity object の null 判定は `== null` / `!= null` を使う。
- CP-5: `CellScene` は identity を parse せず、`SceneResource.StreamByDistance == false` を constructor で拒否する。公開する bounds が必要な箇所は `SceneResource.Volume` をそのまま返し、`CellGridConfig` から再計算しない。
- CP-6: `GameSceneFactory` は CP-4 の構造だけで `DemoCellScene` / `CellCompanionScene` を選ぶ。職種名は Scene class 選択に使わず、どの Cell child も no-UI の同じ lifecycle class になる。
- CP-7: role classifier は Cell child の identity を `_` で分割し、末尾二要素の直前一要素だけを `Environment` / `Lighting` / `VFX` / `Events` と完全一致比較する。末尾二要素を整数へ変換せず、parent identity、座標、bounds、dictionary key、新しい child identity を生成しない。structural Cell child でない resource には classifier を呼ばない。未知 role は選択対象外として warning、同じ Cell の同一 role が複数なら authoring error としてその role を一件も Add しない。欠落 role は optional として正常に扱う。
- CP-8: `SessionCellCompanionLoadDriver` は resident provider が返す各 identity から loaded `CellScene` を `ISceneQuery` で取得し、その `SceneResource.Children` だけを列挙する。Map 全走査、親名からの child 生成、距離判定はしない。
- CP-9: parent Cell が Stable になる前は child Add を一件も発行しない。role 非該当、set 非包含、ロード済み、in-flight の child は飛ばす。同じ child への同時 Add は最大一件である。
- CP-10: reconcile 時に parent `SceneBase` instance を residence token として捕捉し、Add 直前に parent Stable と session cancellation を再確認する。Add 完了後は fresh resident snapshot に parent identity が含まれること、parent が Stable であること、`ISceneQuery.GetLoadedScene(parent)` が捕捉 instance と `ReferenceEquals` で同一であること、session が未 cancellation であることをすべて再確認する。一つでも偽なら、その Add が載せた child を `LoadingDisplayType.None` で明示 unload する。通常の parent unload では既存の再帰 unload を正とし、driver は先回り remove を発行しない。`SceneBase` は `UnityEngine.Object` ではないため、この `ReferenceEquals` は fake-null 禁止に抵触しない。
- CP-11: `EnvironmentIdentity` と identity parse を行う runtime `EnvironmentScene` は置換残骸として削除する。旧 bulk generator にだけ必要な無修飾 Environment の format / 判定 / root 名は Editor-only `LegacyWorldAuthoringNames` へ移し、S-4b の generator 撤去と同時に削除する。
- CP-12: `DemoCellScene` は identity-derived tint を廃止し、authored root 検証だけを残す。S-4b が座標を入力として scene 内の見た目を焼く責務を持つ。S-4a は代替の runtime tint/binding や content を作らない。

#### World Workspace

- WW-1: menu から開く EditorWindow は season (`Spring` / `Summer` / `Autumn` / `Winter`)、x (`0..8`)、y (`0..5`)、role (`Level` / `Environment` / `Lighting` / `VFX` / `Planner`) と、Level のときだけ payload (`Full` / `Whitebox`) を選ぶ。選択状態は EditorWindow instance lifetime だけで、EditorPrefs/UserSettings へ保存しない。
- WW-2: open plan は次の順序に固定する。Level は Cell 一件、Environment は Cell → Environment、Lighting は Cell → Environment → `{Season}_Lighting` → optional Cell Lighting、VFX は Cell → Environment → `{Season}_Lighting` → optional Cell VFX、Planner は Whitebox Cell → optional Cell Events。Planner に Environment / season Lighting を混ぜない。
- WW-3: Full Cell と全 companion は空 Variant の payload を、Whitebox Cell は `Whitebox` payload を `SceneResource.GetPayloads()` から完全一致で解決する。Editor open では runtime fallback を使わない。必須 payload/resource がない場合は不足一覧を表示して一件も scene を閉じたり開いたりしない。optional child 不在は正常で、UI に `Create + Open` を出す。
- WW-4: 全対象を事前解決し、dirty scene の保存確認を一度だけ行う。cancel は無変更。先頭 Cell を `OpenSceneMode.Single`、残りを記載順に `Additive` で開く。途中 failure は error として報告し、それ以上開かない。開く前の完全な復元は Unity Editor API の保証外なので、「事前解決 + 保存確認」を open 操作の境界とし、作成 transaction と混同しない。
- WW-5: create 対象は選択 Cell の optional `Lighting` / `VFX` / `Events` だけ。Cell、Environment、season Lighting、AtmosphereVFX、大型 Event は作れない。親 Cell resource が存在し `StreamByDistance == true` でなければ事前検証で拒否する。
- WW-6: create plan は `{Season}_{Role}_{x}_{y}` identity、`Seasons/{Season}/Cells/{Season}_Cell_{x}_{y}/{identity}/{identity}.unity`、同 folder の SceneResource asset、`Assets/SceneGraphData/Nodes/Cells/{identity}.asset` の `SceneNodeData`、空 Variant payload、`LoadType.OnDemand`、`Volume = zero`、`StreamByDistance = false` を算出する。座標は Editor 生成入力にだけ使い、runtime へ保存した identity から復元させない。
- WW-7: create は書込み前に scene/resource/node path、全 `SceneNodeData` の identity、parent node と所属 `SceneGraphEdges`、parent-child edge、Map/hash、Addressables entry の衝突をすべて検証する。加えて `AssetDatabase.FindAssets("t:SceneResource")` で project 全体の SceneResource identity index を作り、target identity が stale/orphan を含め 0 件、planned co-located resource path が不存在であることを要求する。parent node はちょうど一つ、parent を含む graph もちょうど一つ、`SceneGraphValidator` error は 0、`SceneResourceMap.GenerateHash == SceneResourceGenerator.ComputeCurrentHash(allNodes, allGraphs)` を必須にする。既存の部分成果物を adopt/repair/overwrite せず、一つでも重複・不整合なら無変更で失敗する。
- WW-8: 一 transaction で空 `.unity`、default payload を持つ `SceneNodeData`、parent と同じ `SceneGraphEdges` への node/edge、co-located `SceneResource` の予約 asset、Addressables entry を作り、標準 `SceneResourceGenerator.Generate(allNodes, allGraphs)` を呼ぶ。予約 resource は作成直後に `SerializedObject` で `_identity` だけを target identity に設定し、標準 generator の global identity index が planned path を採用できるようにする。payload、parent/children、Map、GenerateHash は手書きせず Generate だけが投影する。Generate 後に `SceneResource` の `StreamByDistance == false` / `Volume == zero` を確認する。
- WW-9: transaction 開始前に、予定 path、開始時 source hash、parent/graph GUID、追加予定 identity、元の保存済み scene setup/active scene path を持つ pending journal を `Library/OneStarMaker/WorldWorkspace/pending-companion-create.json` へ原子的 replace で永続化する。予定 path は preflight 時点で不存在のため transaction 所有物と判定できる。scene/node/resource を各作成・import した直後は、その asset GUID を journal へ原子的 checkpoint してから dependent mutation へ進む。作成と checkpoint の間で中断した path は「開始時不存在 + pending予定path」を owner proof とし、recovery が現在 GUID を捕捉して journal へ checkpoint してから扱う。journal が存在する間は新しい Open/Create を fail-closed にする。
- WW-10: caught exception の rollback と domain reload / Editor crash 後の recovery は同じ idempotent reverse 操作を使う。各 step は dependency barrier であり、対象が既に desired rollback state か、操作後の再読取検証が成功するまで次へ進まない。順序は「一時 scene close と元 scene setup/active 復元 → Addressables entry 除去（scene/meta は GUID 解決のため残す）→ graph の exact child edge/node membership 除去（node asset は reference 解決のため残す）→ 新規 node/resource/scene の exact path+checkpoint GUID 一致を確認して削除 → 残った元 source から標準 Generate → map hash と開始時 source hash の一致を再検証 → save/refresh」である。ある barrier が失敗したら後続の破壊的 step へ進まず pending journal と識別元を残す。retry は完了済み step を no-op として同じ journal から再開できなければならない。原例外と recovery failure は両方報告する。`InitializeOnLoad` recovery service は自動削除せず、Workspace を block して exact 対象を表示し、発注者が `Rollback Pending Creation` を選んだ時だけ回復する。journal の破棄だけを行う command は作らない。
- WW-11: 一時 Editor `Scene` は transaction が所有する。現在の scene setup と active scene を mutation 前に snapshot し、空 scene を Additive で作成・保存した直後に閉じる。成功後の open は transaction ではなく creator → opener だけが行う。caught failure/recovery は一時 scene を先に閉じ、元 setup/active scene を復元する。close/restore failure も rollback failure として集約し、journal を残す。Editor process 強制終了そのものを瞬時に atomic にはしないが、次回 domain load で persistent journal により fail-closed かつ回復可能にする。
- WW-12: commit は標準 Generate 後の resource/link/map/hash、Addressables、scene/node/resource path を全再読取し、generated resource の path/GUID が予約 resource の checkpoint path/GUID と一致することを必須にする。同じ source でもう一度 Generate して作成 node/resource/link/map/entry が残る idempotence を Editor test で証明した後に journal を削除する。検証 failure は rollback 対象である。
- WW-13: Scene/asset の生成・変更は Editor API と `SerializedObject` / `SceneGraphEdges` 公開操作だけを使う。YAML text edit、GUID 手書き、既存 bulk generator への Workspace UI 追加は禁止する。

#### 検証と構造

- VT-1: Variant の空 / Whitebox 伝播、missing Whitebox fallback、Editor null/empty precedence を自動テストで証明する。
- VT-2: 四 companion set の role 行列、strict config、qualified/legacy role 認識、structural factory、qualified Cell の非 parse、stable gate、in-flight dedupe、parent-race cleanup を自動テストで証明する。
- VT-3: Workspace open plan の全組合せ、exact Whitebox 解決、範囲/role validation、create success invariant、各 mutation point の fault injection rollback、domain reload 相当の pending-journal recovery、標準 SceneGraph Generate 後の永続性を Editor test で証明する。`Task.Delay` / `Thread.Sleep` を使わず completion signal で同期する。
- VT-6: invalid companion config の startup integration test は、SceneDirector が未構築、派生 cleanup hook が呼ばれる、Framework がロード済み UI/map/App owner asset を release することを観測する。
- VT-4: 新規 asmdef 参照、SceneState/LoadType 値、Framework の season/role 語彙、Production Whitebox 混入がないことを mechanical check する。
- VT-5: 既存 500 行超ファイルへの変更は配線または参照置換に限定し、新しい policy / orchestration / Editor I/O を入れない。

### 2.2 本文へ転記した実装制約

- 依存方向は Game → Framework。全体配線は `SampleGame.DependOnAll` に置き、OneStarMaker Runtime は SampleGame を参照しない。asmdef 参照は追加しない。
- アセットロードは `IAssetManagement` 経由。Scene load の owner は従来どおり `SceneDirector` と `AssetOwner.Scene(identity)` であり、Workspace は runtime asset owner を持たない。
- `SceneState` 14 値を減らさず並べ替えず追加もしない。state transition owner は `SceneLifecycleManager` のまま。`LoadType` も追加しない。
- 公開 logging は `ILogger<T>` / `ILogger`。`ZLogger` 型を公開面へ出さない。
- Editor code を runtime assembly に置かず、`UnityEditor` / Addressables Editor 依存は `OneStarMaker.Editor` または `SampleGame.DependOnAll.Editor` に閉じる。
- 新規・編集する Unity C# の先頭は `#nullable enable`。Unity C# では `record` を使わない。
- 破棄され得る `UnityEngine.Object` は `== null` / `!= null` で判定し、`?.` / `??` / `is null` / `ReferenceEquals` を使わない。
- テストに `Task.Delay` / `Thread.Sleep` を使わない。
- 参照 0 だけを削除理由にしない。本 slice で削除する二つの runtime 型は、構造分類と Editor-only legacy helper に責務を置換したことを削除根拠にする。
- Phase B 実装担当は Unity.exe を起動せず、`pwsh tools/run-tests.ps1`、Addressables build、`unity test` / `unity run` を実行しない。人間が既に開いている Editor に限り `osm-unity-editor` の named pipeline / eval で接続する。
- SceneResource、BuildVariantProfile、scene、Addressables の変更を YAML で行わない。Editor が到達不能なら該当 asset 操作を止める。
- PR を後続 phase で作る場合の base は `develop`。

### 2.3 A0 未決事項の確定

- Editor null/empty と precedence: RV-5 に確定した。
- factory の構造分類: 既存 `SceneResource.Parent` と `StreamByDistance` だけを使う CP-4/CP-6 に確定し、Framework API を増やさない。
- role grammar: CP-7 の「末尾二 token の直前」を SampleGame 制作規約として固定した。座標の数値 parse はしない。
- runtime tint: CP-12 のとおり削除し、見た目を焼く責務は S-4b に置く。S-4a で bridge component は増やさない。
- create atomicity: WW-7〜WW-13 の source preflight + persistent journal + standard Generate + reverse recovery + postcondition 再検証に確定した。
- Whitebox editor fallback: runtime fallback と分離し、Workspace は exact payload を要求する WW-3 に確定した。
- 公開 API: §3.1 の 3 系統だけを Framework 追加面として確定した。

## 3. 責務マップ

### 3.1 公開 API の凍結面

Framework に追加・変更してよい公開面は次だけである。名称は Phase B で変更しない。

```csharp
public SceneDirector(
    ISceneFactory sceneFactory,
    UICommon uiCommon,
    SceneResourceMap sceneResourceMap,
    ILoadingDisplay loadingDisplay,
    IAssetManagement assetManagement,
    string sceneVariant = "")

protected virtual string ResolveSceneVariant()

public static class SceneVariantRuntimeBridge
{
    public static Func<string?>? EditorSceneVariantResolver { get; set; }
}

public string BuildVariantProfile.SceneVariant { get; }
```

`SceneVariantRuntimeBridge` の delegate は Editor injector が domain reload ごとに設定し、Player からは `ResolveSceneVariant` の `#if UNITY_EDITOR` 分岐により読まない。delegate は値の owner ではなく、UserSettings profile への同期 query 口である。

SampleGame の cross-assembly 公開面は次に限定する。

```csharp
public enum CellCompanionSet { Full, Planner, Lighting, Vfx }

public static class CellCompanionSetParser
{
    public static CellCompanionSet Parse(string? value, bool keyExists);
}
```

`CellCompanionRole`、set → role inclusion、classifier は `internal` とする。`VFX` config/identity と C# enum member `Vfx` の対応は classifier/parser 内だけに置く。driver の reconcile test seam は `internal` とし、`SampleGame.InGame` から test assembly への `InternalsVisibleTo` で検証する。新しい Framework interface、SceneResource field、LoadType、SceneState は作らない。

`ResolveSceneVariant()` は、S-4 program §10 が明示した Framework の app-specific 解決口なので `protected virtual` を維持する。既定実装は RV-4〜RV-6、戻り値 non-null、起動時一回という invariant を持ち、`AbstractApplicationInitializer` が null を拒否してから `SceneDirector` へ渡す。現 SampleGame は override せず既定実装を使う。将来の派生 application が固有の config source を合成することだけを consumer とし、実行中 mutation や Editor API 注入には使わせない。

### 3.2 Framework Runtime: Variant の解決と伝播

- `unity/Assets/OneStarMaker/Scripts/Runtime/AssetManagement/SceneVariantRuntimeBridge.cs`（新規、0 → 45 行目安）
  - 責務: Editor が active profile の nullable Variant を runtime startup へ注入する唯一の bridge。
  - 種別: 公開 API / query bridge。値や lifetime を所有しない。
  - 依存: `System.Func` のみ。UnityEditor、SampleGame 語彙を禁止。
  - テスト境界: delegate の null / empty / Whitebox を pure resolver 経由でテスト。
  - 配置理由: remote catalog bridge と同じ Runtime AssetManagement 境界だが、変更理由が別なので同じ class へ足さない。

- `unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap/SceneVariantResolver.cs`（新規、0 → 55 行目安、`internal static`）
  - 責務: configured value と nullable editor override から一つの immutable startup value を選ぶ policy。
  - 種別: policy。Unity/Editor I/O、状態、logging を持たない。
  - 依存: string と resolver delegate。出力は non-null string。
  - テスト境界: Unity/AssetDatabase なしで全 precedence を unit test。
  - 配置理由: 816 行の initializer に policy を埋めず、startup policy として閉じる。

- `unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs`（816 行、予想 +25）
  - 責務変更: `ResolveSceneVariant()` extension port を公開し、SceneDirector 構築直前に一回呼んで渡す配線だけ。
  - owner/lifetime: config、bridge query、解決結果の一時 local は App startup。永続 owner は構築後の SceneDirector。
  - 依存: 既存 AppConfig、`SceneVariantResolver`、bridge。SampleGame 語彙禁止。
  - テスト境界: resolver は別 unit、initializer は constructor forwarding test。
  - 警報判断: 500 行超だが、配線 25 行以内で新しい policy を置かないため非分割。+50% なし、責務追加なし。

- `unity/Assets/OneStarMaker/Scripts/Runtime/SceneSystem/SceneDirector.cs`（259 行、予想 +15）
  - 責務変更: 起動時 Variant を constructor validation して readonly 所有する。
  - owner/lifetime: `SceneDirector` / App lifetime。runtime mutation なし。
  - 依存: string のみ追加。出力は partial class 内の load callだけ。
  - テスト境界: null rejection と empty/Whitebox retention を実 load path から検証。

- `unity/Assets/OneStarMaker/Scripts/Runtime/SceneSystem/SceneDirector.Loading.cs`（643 行、予想 +1 / -1、net 0）
  - 責務変更: hard-coded `string.Empty` を readonly `_sceneVariant` に置換する一行だけ。
  - owner/lifetime: 新規 state なし。既存 Scene load orchestration / `AssetOwner.Scene(identity)` のまま。
  - テスト境界: fake backend が受けた address で variant exact と fallback を統合検証。
  - 警報判断: 500 行超だが責務・依存・規模を増やさない一行置換なので非分割。

- 変更しない境界: `IAssetManagement`、`AssetManagement`、`SceneAssetDescription`、`SceneResource`、`AssetKey`。既存 fallback を使うだけである。

### 3.3 Framework Editor: active profile の runtime 値

- `unity/Assets/OneStarMaker/Scripts/Editor/Build/Variants/BuildVariantProfile.cs`（88 行、予想 +12）
  - 責務変更: serialized `_sceneVariant` と read-only `SceneVariant` を whitelist profile の同じ authoring data として保持。
  - owner/lifetime: profile asset / project asset lifetime。
  - 依存: 既存 Editor profile だけ。runtime bridge を参照しない。
  - テスト境界: serialized field/property、whitelist との profile invariant。

- `unity/Assets/OneStarMaker/Scripts/Editor/Build/Variants/DeveloperVariantSettings.cs`（80 行、予想 +18）
  - 責務変更: active profile 未選択を null、選択済み profile の値を empty を含めそのまま返す query。
  - owner/lifetime: profile GUID は UserSettings asset / project-user lifetime。Variant のコピーを保持しない。
  - 依存: `AssetDatabase` と `BuildVariantProfile`。runtime 非依存。
  - テスト境界: missing GUID/profile と empty/Whitebox。

- `unity/Assets/OneStarMaker/Scripts/Editor/Build/Variants/SceneVariantEditorInjector.cs`（新規、0 → 40 行目安）
  - 責務: domain load 時に `SceneVariantRuntimeBridge.EditorSceneVariantResolver` へ query delegate を設定する。
  - owner/lifetime: static delegate / Editor domain lifetime。値 owner は profile asset。
  - 依存: Runtime bridge、DeveloperVariantSettings、`InitializeOnLoad`。
  - テスト境界: resolver delegate wiring は Editor test、selection policy は Runtime unit。

- `unity/Assets/OneStarMaker/Scripts/Editor/Build/Variants/DeveloperVariantSettingsProvider.cs`（146 行、予想 +22）
  - 責務変更: active profile の runtime Scene Variant を read-only 表示し、変更は profile inspector で行う導線を示す。
  - owner/lifetime: Settings UI / window draw lifetime。状態を複製しない。
  - 警報判断: +50% 未満。既存 active-profile UI の同じ変更理由なので非分割。

- `unity/Assets/OneStarMaker/Scripts/Editor/Build/Variants/VariantPlayerBuild.cs`（236 行、予想 +55）
  - 責務変更: active profile の first scene と Scene Variant を build 中の低優先 JSON overlay として同時 upsertし、raw original bytes を一つの `finally` で復元する。
  - owner/lifetime: original `byte[]` snapshot と overlay は一回の Player build operation lifetime。profile asset が値 owner、完成 Player の StreamingAssets JSON が runtime input owner。
  - 依存: BuildVariantProfile、`System.IO`、既存 BuildPipeline。environment / command-line provider は変更しない。
  - テスト境界: pure top-level string upsert、existing empty key の Whitebox 置換、JSON escaping、build success/failure/throw 時の exact restoration。BuildPipeline call は injectable internal seam に置く。
  - 警報判断: +50% 未満。既存の temporary app-config overlay と同じ変更理由・owner・rollback なので同 class が妥当。

- `unity/Assets/OneStarMaker/Editor/BuildProfiles/Production.asset`（既存 asset、serialized field +1）と `Default.asset`（既存 asset、+1）
  - 責務: Production/default の Scene Variant は空。Production whitelist に Whitebox がないことを固定。
  - owner/lifetime: project asset / build-developer selection lifetime。

- `unity/Assets/OneStarMaker/Editor/BuildProfiles/WorldWhitebox.asset` と `.meta`（新規、asset 約35行）
  - 責務: development build/Play profile。Scene Variant `Whitebox`、whitelist `""` + `"Whitebox"`、既存 map/always included/target group を明示する。
  - owner/lifetime: project asset。active selection は UserSettings が GUID を所有。
  - 作成境界: reachable な既存 Unity Editor の pipeline で `ScriptableObject` と GUID を生成する。YAML 手書き禁止。

### 3.4 SampleGame Runtime: config と構造的 companion loading

- `unity/Assets/SampleGame/Config/app-config.json`（既存、予想 +2 key）
  - 責務変更: Production default の `assets:sceneVariant: ""` と `world:cellCompanionSet: "Full"`。
  - owner/lifetime: StreamingAssets config / Player process startup。

- `unity/Assets/SampleGame/InGame/InGameSession/Streaming/CellCompanionPolicy.cs`（新規、0 → 105 行目安）
  - 責務: strict config parse と companion set → internal role inclusion の宣言的 policy。
  - owner/lifetime: immutable enum/value、process state なし。
  - 依存: `System` のみ。Framework/Unity/Editor I/O なし。
  - 公開面: §3.1 の `CellCompanionSet` と `CellCompanionSetParser.Parse` だけ。role enum と inclusion は internal。
  - テスト境界: 4 × 4 inclusion 行列、missing/empty/case/unknown。

- `unity/Assets/SampleGame/InGame/InGameSession/Streaming/CellCompanionRoleClassifier.cs`（新規、0 → 65 行目安、`internal static`）
  - 責務: structural child と確認済みの opaque identity から role token だけを分類。
  - owner/lifetime: state なし。
  - 依存: string と `CellCompanionRole`。CellIdentity/EnvironmentIdentity 禁止。
  - テスト境界: legacy/qualified/multi-token qualifier/invalid/season Lighting 非誤認識。

- `unity/Assets/SampleGame/InGame/InGameSession/Streaming/CellCompanionLoadRules.cs`（`CellChildLoadRules.cs` 34 行を rename、予想 65 行）
  - 責務: stable、role included、loaded/in-flight から Add 可否を返す pure rule。
  - owner/lifetime: state なし。
  - テスト境界: boolean matrix。driver orchestration test と分離。

- `unity/Assets/SampleGame/InGame/InGameSession/Streaming/SessionCellCompanionLoadDriver.cs`（`SessionCellChildLoadDriver.cs` 301 行を置換、予想 330 行）
  - 責務: resident Cell の structural Children を policy に従い明示 Add し、親競合時の cleanup を行う一つの orchestration loop。
  - owner/lifetime: `InGameSessionScene` が唯一 owner。`OnLoaded` で構築、`OnStabled` で Start、`OnPreUnLoaded` で Stop/Dispose。CTS と in-flight set は scene lifetime。
  - 依存入力: `ISceneController`、`ISceneQuery`、resident snapshot provider、immutable companion set、`ILogger`。出力: AddScene / race 時だけ UnloadScene / logs。
  - 禁止: Map 全走査、距離計算、identity 生成、static state。
  - テスト境界: `internal ReconcileOnceAsync` を completion-controlled fake controller/query で検証。loop timer 自体は smoke、policy/race は delay なし。
  - 警報判断: 500 行未満、既存比 +10% 程度。選択 policy と grammar を別ファイルへ出すため orchestration 一責務を維持。

- `unity/Assets/SampleGame/InGame/InGameSession/InGameSessionScene.cs`（147 行、予想 +18）
  - 責務変更: injected companion set を保持し、renamed driver を scene lifecycle に結線。
  - owner/lifetime: driver と set の利用 owner は `Scene(InGameSession)`。破棄順は companion driver → world streaming driver。
  - テスト境界: Start/Dispose lifecycle と constructor forwarding。

- `unity/Assets/SampleGame/DependOnAll/AppInitializer.cs`（297 行、予想 +18）
  - 責務変更: AppConfig key presence/value を読み、pure policy で一回 parse して factory へ渡す composition wiring。
  - owner/lifetime: config は App、enum は factory/session へ value copy。invalid は factory 作成 stage で throw。
  - 依存: 既存 Foundation config と SampleGame.InGame public policy。Editor 依存なし。

- `unity/Assets/SampleGame/DependOnAll/GameSceneFactory.cs`（77 行、予想 +25）
  - 責務変更: §CP-4 の SceneResource 構造で Cell / Cell child を分類し、companion set を InGameSession に注入。
  - owner/lifetime: factory / App lifetime。set は immutable value。Scene instance ownership は SceneDirector。
  - 依存: Framework SceneResource、SampleGame scene classes/policy。role classifier は使わない。
  - テスト境界: arbitrary qualified identity、StreamByDistance、Parent 構造、非 Cell resource switch。

- `unity/Assets/SampleGame/InGame/InGameSession/World/CellScenes/CellScene.cs`（51 行、予想 -10）
  - 責務変更: coordinate carrier をやめ、structural Cell invariant と no-UI だけを強制。bounds access は `SceneResource.Volume`。
  - owner/lifetime: SceneResource は project/App-loaded asset、CellScene は `Scene(cellIdentity)` lifetime。
  - 依存: CellIdentity/CellGridConfig を削除。Framework SceneResource だけ。
  - テスト境界: qualified opaque identity 成功、flag false 拒否、Volume exact。

- `unity/Assets/SampleGame/InGame/InGameSession/World/DemoCellScene.cs`（148 行、予想 -70）
  - 責務変更: authored root 存在検証と lifecycle logging だけ。identity-derived tint を削除。
  - owner/lifetime: `Scene(cellIdentity)`。scene content は Unity scene が所有。
  - テスト境界: root present/missing。見た目は S-4b。

- `unity/Assets/SampleGame/InGame/InGameSession/World/CellCompanionScene.cs`（新規、0 → 75 行目安）
  - 責務: structural Cell child 共通の no-UI lifecycle class。parent が streaming Cell でない場合を constructor で拒否。
  - owner/lifetime: `Scene(childIdentity)`。parent recursive unload に従う。
  - 依存: SceneResource.Parent/StreamByDistance、ILoggerFactory。role 語彙/parse なし。
  - テスト境界: qualified/unknown role の structural child 成功、orphan/non-cell parent 拒否。

- `unity/Assets/SampleGame/InGame/InGameSession/World/EnvironmentIdentity.cs`（110 行）と `EnvironmentScene.cs`（134 行）
  - 削除: runtime parent-name derivation / coordinate tint の置換残骸。`.meta` も Unity Editor で削除する。
  - 置換先: runtime loading は structural `CellCompanionScene`、旧 generator naming は Editor-only `LegacyWorldAuthoringNames`。

- `unity/Assets/SampleGame/InGame/Properties/AssemblyInfo.cs`（新規、0 → 5 行目安）
  - 責務: `OneStarMaker.Tests` に internal reconcile/classifier の test access を許可するだけ。
  - 公開 API を増やさず orchestration race を直接検証するための test boundary。production dependency edge は増やさない。

### 3.5 SampleGame Editor: legacy 隔離と World Workspace

すべて `unity/Assets/SampleGame/DependOnAll/Editor/WorldAuthoring/`、namespace `SampleGame.DependOnAll.Editor.WorldAuthoring`、既存 `SampleGame.DependOnAll.Editor.asmdef` に置く。Framework へ職種語彙を入れず、既存 asmdef 参照だけで Runtime model と Unity/Addressables Editor API に到達できるためである。

- `LegacyWorldAuthoringNames.cs`（新規、0 → 75 行目安、`internal static`）
  - 責務: 旧 4 × 4 bulk generator が一時的に必要とする無修飾 Environment format/判定と root 名。
  - owner/lifetime: state なし。S-4b の legacy generator 削除と同時に削除する期限付き compatibility code。
  - 依存: string/int だけ。runtime から参照不可。
  - テスト境界: existing EnvironmentIdentity tests を Editor test として移す。

- `WorldWorkspaceSelection.cs`（新規、0 → 170 行目安）
  - 責務: season/coordinate/role/payload value と identity/path の決定、および WW-2 の ordered open plan を返す pure policy。
  - owner/lifetime: immutable ordinary classes/enum。window に value copy、global state なし。
  - 依存: string/int/collections と SampleGame naming convention。UnityEditor/AssetDatabase なし。
  - 公開面: Editor assembly `internal`。
  - テスト境界: 4 seasons × boundary coordinates × 5 roles × payload、順序、required/optional/exact variant。

- `WorldWorkspaceSceneOpener.cs`（新規、0 → 180 行目安）
  - 責務: pure open plan を exact SceneResource payload path へ解決し、保存確認後に Single/Additive open する Editor I/O。
  - owner/lifetime: operation local。open scenes は Unity Editor が所有。
  - 依存: SceneResourceMap、AssetDatabase、EditorSceneManager。creation/mutation policy は持たない。
  - テスト境界: resolver を fakeable seam に分け、exact variant/no partial preflight を unit、actual open orderingを Editor integration。

- `WorldCompanionCreationPlan.cs`（新規、0 → 150 行目安）
  - 責務: Lighting/VFX/Events 一件の identity、scene/resource/node path、parent node/graph requirements、serialized values と precondition を算出する pure policy。
  - owner/lifetime: immutable operation value。
  - 依存: Workspace selection value と path strings。UnityEditor I/O なし。
  - テスト境界: role restriction、range、all calculated paths/values、collision input rejection。

- `WorldCompanionSceneCreator.cs`（新規、0 → 185 行目安）
  - 責務: preflight、scene-setup snapshot/保存確認、transaction 実行、postcondition、commit 後 open を順序付ける Editor orchestration。
  - owner/lifetime: one button invocation。transaction を `using` で所有し、成功時 commit、failure 時 rollback。
  - 依存: plan、transaction、SceneResourceMap、EditorSceneManager。serialized mutation の詳細は持たない。
  - テスト境界: fake transaction/backend で cancel/preflight/error/commit/rollback request。

- `WorldCompanionCreationTransaction.cs`（新規、0 → 360 行目安）
  - 責務: 一件の scene/node/graph/resource/addressable source mutation、標準 Generate 投影、persistent-journal commit/reverse recovery。
  - owner/lifetime: creator invocation。一時 Editor Scene と in-memory operation は invocation lifetime、pending journal は commit/recovery 完了まで Editor process を越えて存続する。
  - 依存: AssetDatabase、SerializedObject、EditorSceneManager、AddressableAssetSettings、`SceneResourceGenerator.Generate/ComputeCurrentHash`、SceneGraphValidator。
  - 公開面: Editor assembly `internal`。fault injection point と idempotent recovery entry だけ internal test seam。
  - テスト境界: temp Assets folder を使い各 mutation 後に例外/domain-reload相当を注入し、scene setup、node/edge、files、map/hash、addressable が開始状態へ戻ることを Editor test。
  - 分割理由: pure path/policy、UI、open I/O、mutation/rollback は変更理由・依存・test 方法が二つ以上異なるため別 class。transaction 内の forward/rollback は同じ invariant の表裏なので分けない。

- `WorldCompanionRecoveryJournal.cs`（新規、0 → 150 行目安）
  - 責務: `Library` の一件限定 pending record を durable write/read/checkpoint/delete し、transaction 所有 path/GUID、recovery barrier、開始 hash、元 scene setup を保持する。
  - owner/lifetime: pending operation。project asset ではなく ignored Library data、同時 pending は最大一件。
  - 依存: `System.IO` と JSON DTO。Unity asset mutation は持たない。
  - テスト境界: atomic replace、corrupt/missing journal、round-trip。corrupt は Workspace block を維持し自動削除しない。

- `WorldCompanionRecoveryService.cs`（新規、0 → 120 行目安）
  - 責務: domain load 時に pending journal を検出し、Workspace command を blockして exact 対象と明示 Rollback action を提示する。
  - owner/lifetime: Editor domain。state owner は journal、service は query/orchestration のみ。
  - 依存: `InitializeOnLoad`、journal、transaction recovery entry、Editor dialog/log。
  - テスト境界: no-journal、pending block、user rollback、rollback failure keeps journal。

- `WorldWorkspaceWindow.cs`（新規、0 → 230 行目安）
  - 責務: selection UI、plan preview、不足表示、Open/Create+Open command の提示。
  - owner/lifetime: EditorWindow instance。domain reload 時の永続保証なし。
  - 依存: pure selection、opener、creator。Asset serialization を直接行わない。
  - テスト境界: plan/command availability は presenter-level unit、GUI 描画は Editor smoke。

- 既存 legacy files の narrow edit:
  - `WorldCellStreamingSliceCreator.cs`（1212 行、予想 net 0）、`HandEditProbe.cs`（171 行、net 0）、`WorldCellExistingStateCollector.cs`（196 行、net 0）、対応 Editor tests は `EnvironmentIdentity` / `EnvironmentScene.AuthoredRootName` 参照を `LegacyWorldAuthoringNames` へ置換するだけ。
  - 500 行超 bulk generator に Workspace、transaction、qualified naming を追加しない。S-4b まで既存挙動を保つ機械置換なので非分割。

### 3.6 Tests と supporting files

- `unity/Assets/OneStarMaker/Tests/AssetManagement/FakeAssetBackend.cs`（181 行、予想 +25）
  - backend が受けた scene address/history を記録する test double。production API なし。

- `unity/Assets/OneStarMaker/Tests/Scene/SceneVariantForwardingTests.cs`（新規、0 → 180 行目安）
  - 実 `SceneDirector.PerformUnitySceneLoad` を通し、empty/Whitebox と fallback address を検証。既開き/logical node は load 0 も確認。

- `unity/Assets/OneStarMaker/Tests/Bootstrap/SceneVariantResolverTests.cs`（新規、0 → 110 行目安）
  - config/editor null/empty/Whitebox/player 分岐と one-shot constructor forwarding。

- `unity/Assets/OneStarMaker/Tests/Editor/Build/BuildVariantProfileSceneVariantTests.cs`（新規、0 → 130 行目安）
  - profile serialized 値、WorldWhitebox whitelist、Production exclusion、不整合 profile 拒否、injector query、static bridge の test teardown reset。

- `unity/Assets/OneStarMaker/Tests/Editor/Build/VariantPlayerBuildConfigOverlayTests.cs`（新規、0 → 190 行目安）
  - 既存 empty key の Whitebox upsert、first-scene との同時 overlay、build success/failure/exception 後の raw JSON exact restore、Player に埋め込む値を検証。

- `unity/Assets/OneStarMaker/Tests/Streaming/CellCompanionPolicyTests.cs`（既存 `CellChildLoadRulesTests.cs` 79 行を置換、予想 190 行）
  - strict parser、role matrix、classifier、pure add rule。増加 >50% だが同じ pure companion policy test fixture 群で、production 責務は混ぜない。fixture が 250 行を超える場合は parser / classifier を別 test file へ分ける。

- `unity/Assets/OneStarMaker/Tests/Streaming/SessionCellCompanionLoadDriverTests.cs`（新規、0 → 280 行目安）
  - stable 前 Add 0、各 profile、in-flight duplicate 0、Add 中 parent removal 後 unload、cancellation。`UniTaskCompletionSource` を用い timer sleep なし。

- `unity/Assets/OneStarMaker/Tests/Scene/CellSceneTests.cs`（272 行、予想 -30）
  - identity parse/coordinate tests を structural flag/Volume/qualified identity tests へ置換。

- `unity/Assets/OneStarMaker/Tests/SampleGame/GameSceneLoggingTests.cs`（237 行、予想 +55）
  - factory structural classification、companion-set forwarding、logger factory reuse。

- `unity/Assets/OneStarMaker/Tests/Bootstrap/InvalidCompanionConfigStartupTests.cs`（新規、0 → 180 行目安）
  - invalid key が CreateSceneFactory stage で失敗した時、SceneDirector 未構築、derived failure cleanup、UI/map/App owner asset release を observable backend/hook で検証。

- `unity/Assets/OneStarMaker/Tests/Editor/WorldAuthoring/WorldWorkspaceSelectionTests.cs`（新規、0 → 230 行目安）
  - pure open/create plans と exact variant requirements。

- `unity/Assets/OneStarMaker/Tests/Editor/WorldAuthoring/WorldCompanionCreationTransactionTests.cs`（新規、0 → 430 行目安）
  - temp asset graph で success、標準 Generate 再実行後の永続性、scene作成/保存/close、node/edge、resource、Generate、Addressables 各 fault point rollback と元 active scene setup 復元。500 行へ近づく場合は success invariant と rollback parameterized fixture に分ける。

- `unity/Assets/OneStarMaker/Tests/Editor/WorldAuthoring/WorldCompanionRecoveryTests.cs`（新規、0 → 260 行目安）
  - pending journal を各 phase の状態で残し、domain reload 相当の recovery が exact transaction-owned targets だけを除去し、元 source の GenerateHash と scene setup を復元することを検証。各 recovery barrier 自体にも一度 fault を注入し、識別元が残り、同じ journal の二回目 retry で完了することを検証する。

- `.meta` files（新規/rename/delete に追随）
  - owner は Unity AssetDatabase。手書きせず既存 Editor に生成させ、Phase C evidence に GUID/参照一意性を含める。

### 3.7 規模と警報の集計

- production C# は新規約 2,150 行、既存 net 約 -120〜+180 行、test は新規/増分約 1,650 行を見込む。Editor transaction/recovery fault tests の比率が高い。
- 500 行警報: 既存 `AbstractApplicationInitializer`、`SceneDirector.Loading`、`WorldCellStreamingSliceCreator` だけ。いずれも narrow wiring/reference edit で新責務を足さない。新規 file は 330 行以下を目標にする。
- 3責務警報: `WorldCompanionCreationTransaction` は scene/resource/link/map/addressable を別責務として扱うのではなく、「一つの graph invariant を atomically変更/復元する」一責務とする。policy、UI、open orchestration は別 file へ分離済み。
- 50%増加警報: pure companion policy test のみ想定。250 行超で test file を分ける。production の既存 file には発火させない。
- 実測が production 見積もりの +25% を超える、または新規 production file が 400 行を超える場合は Phase B を止め、単なる行分割ではなく責務マップを Phase A へ返す。

## 4. 実装計画

### 4.1 変更順序

1. Phase B 開始時に base commit、clean worktree、HANDOFF status `A frozen`、既存 Unity Editor 接続可否を確認する。freeze 前なら開始しない。
2. pure `SceneVariantResolver` と tests を作り、bridge/profile field/injector、initializer、SceneDirector の順で配線する。`IAssetManagement` / fallback を変更しない。
3. pure companion policy/classifier/rules と tests を作る。構造 factory と `CellScene`/`CellCompanionScene` を移行し、最後に session driver wiring と race tests を行う。
4. runtime identity/tint 参照が 0 で、Editor legacy 参照が移送済みであることを確認して `EnvironmentIdentity` / `EnvironmentScene` を削除する。
5. pure Workspace selection/create plans と tests を作る。次に opener、durable journal/recovery、source-backed transaction、creator、window を I/O 境界ごとに実装する。標準 SceneGraph Generate を投影器として使い、Map を独自正本にしない。
6. 人間が既に開いている Unity Editor へ `osm-unity-editor` の named pipeline で接続できる場合だけ、profiles、SceneGraph/Addressables integration、`.meta` を生成/更新する。接続不能なら YAML を編集せず停止する。CLI 再インストールや Unity.exe 起動はしない。
7. Phase B は static checks と差分/責務照合だけを行い、Unity tests / Addressables build は実行しない。implementation commit と Phase B result snapshot を固定して Phase C へ渡す。

### 4.2 Phase B の停止条件

次のどれか一つで直ちに実装を止め、変更を隠して継続せず Phase A revision を要求する。

- 本 HANDOFF にない public/protected API、SceneResource field、SceneState、LoadType、asmdef reference、package が必要。
- Cell / child を `Parent` と `StreamByDistance` だけで分類できず、identity parse または Framework への role API が必要。
- companion driver が `Children` 列挙だけでは成立せず、Map 全走査、親からの child id 生成、第二の距離 policy が必要。
- AppConfig の key presence と empty value を現行 API で区別できず、RV-5/CP-1 を守れない。
- active Editor profile の empty と未選択 null を bridge で区別できない。
- `IAssetManagement` / `SceneAssetDescription` の既存 fallback を変更しなければ Variant を通せない。
- workspace が exact Whitebox payload を runtime fallback なしで解決できない。
- atomic create の一 mutation を rollback journal で復元できない、または Addressables API が entry の ownership（既存か新規か）を判定できない。
- 既存部分成果物を repair/adopt/overwrite しなければ create できない。repair は別設計であり S-4a に足さない。
- Scene/asset/profile の変更に必要な人間起動済み Editor が到達不能。Unity.exe 起動、CLI reinstall、YAML edit へ迂回しない。
- 新規 production file >400 行、production 見積 +25% 超、既存 file +50% 超、または一 class に独立して変わる policy/I/O/orchestration が混在する。
- active profile の Scene Variant を Player build の低優先 JSON へ upsert し、元 JSON を build 成否にかかわらず exact restore できない。
- profile Scene Variant と whitelist の不整合を build/startup 前に fail-closed にできない。
- parent node/graph を一意に解決できない、開始時 GenerateHash が current source hash と一致しない、または標準 Generate 後に optional child が残らない。
- project 全体に target identity の stale/orphan `SceneResource` が一件でも存在する、または予約 resource の `_identity` を設定しても標準 Generate が planned co-located path を採用しない。
- transaction/recovery failure 後に一時 scene を閉じて元 scene setup/active scene を復元できない。
- persistent pending journal から transaction-owned path/GUID を一意に特定できず、既存 asset を削除する危険がある。
- recovery の先行 barrier が失敗した時に後続 asset を消さず停止できない、または同じ journal から二回目の idempotent retry を完了できない。
- current 4 × 4 asset を S-4a 内で書き換えないと code migration を成立させられない。content migration は S-4b 所有である。
- runtime tint 削除に代えて新しい material binding/content authoring が必要。S-4b へ返す。
- Unity fake null、nullable、public logging、test wait の常時契約に反する実装しか成立しない。

### 4.3 対象外を維持する方法

- S-4b の folder/tree/9 × 6 data は pure Workspace naming が参照する定数範囲以外は生成しない。Workspace は不足を表示するだけで、Cell/Environment を補完しない。
- S-4c の render API/Lighting content を作らない。`Lighting` は role token と空 editor scene の作業単位に限る。
- S-4d package/VFX type を参照しない。`VFX` は string role と空 editor scene に限り、`UnityEngine.VFX` / asmdef reference を追加しない。
- legacy bulk generator には compatibility name 参照置換以外を入れず、実行しない。
- Phase B 結果欄以降は本 Phase A task で埋めない。

## 5. テストとレビュー計画

### 5.1 単体テスト

- Variant policy: editor override `null` / `""` / `Whitebox`、player config、missing key、case/whitespace preservation。
- Variant forwarding: empty と Whitebox の backend address、Whitebox 欠落時 empty fallback、logical node/既開き scene load 0。
- Player build overlay: active profile Variant/first scene の JSON upsert、environment/CLI が後勝ちであること、全 build exit path の raw JSON exact restore。
- Companion policy: 4 set × 4 role の全行列、missing default、present empty/unknown/case mismatch failure。
- Structural scene factory: opaque qualified Cell、cell child、season Lighting（cell childでない）、orphan、legacy identity。
- Identity independence: `Spring_Cell_4_2` だけでなく `Qualifier_With_Underscore_Cell_not_an_int_east` も `StreamByDistance` が true なら Cell として構築でき、Volume をそのまま返す。
- Driver: stable gate、children-only、role filter、dedupe、parent が Add 中に resident を離れる順序、同 identity で別 SceneBase instance へ reload する ABA、session cancellation。時刻待ちは使わない。
- Workspace policy: 全 role の順序/required/optional/exact variant、coordinate boundary、invalid role/coordinate、create role 制限、planned path 外の stale/orphan resource identity collision。

### 5.2 統合・Unity テスト

- Editor profile assets: WorldWhitebox empty+Whitebox、Production no Whitebox、active profile resolver の null/empty 区別。
- Workspace exact payload: Whitebox 欠落は fallback open せず error、全 preflight 成功時の Single/Additive order。
- Atomic create/recovery: temp Assets root で success invariant、project-wide stale resource collision の無変更拒否、予約 `_identity` により標準 Generate が co-located path/GUID を採用すること、再 Generate 耐性、scene setup/node/edge/resource/map/hash/addressable 各 mutation 後 fault の完全 rollback、pending journal からの domain-reload recovery、各 recovery barrier 一回失敗後の同一 journal retry。テストが作った明示 temp path だけを teardown し、workspace root や glob を削除しない。
- Startup cleanup: invalid companion config 後に SceneDirector なし、failure hook、loaded UI/map/App asset release を統合確認。
- Phase C の test operator が `pwsh tools/run-tests.ps1` を使う。`unity test` / `unity run` は使わない。Phase B implementation agent は実行しない。
- Addressables full build と 658 scene aggregate validation は S-4b 以降。S-4a では profile/entry の構造 test まで。

### 5.3 機械検査

- `pwsh tools/docs-audit.ps1`
- `rg` で Runtime の `Spring|Summer|Autumn|Winter|Environment|Lighting|VFX|Events` 新規漏出を確認。ただし SampleGame Runtime の companion role 定義は許可し、OneStarMaker Runtime は 0 を要求する。
- `rg` で `EnvironmentIdentity.TryFromCellId`、runtime `CellIdentity.TryParse`、`ComputeBounds`、driver の child identity format が 0 であることを確認。
- SceneState enum の diff 0、LoadType enum の diff 0、`.asmdef` diff 0、packages diff 0。
- Production profile に `Whitebox` 0、WorldWhitebox profile に empty / Whitebox 各一件。
- active profile Player build overlay が `assets:sceneVariant` を更新し、全 exit path で `app-config.json` の原文 hash を復元することを確認。
- World Workspace 作成前後で `SceneResourceMap.GenerateHash == SceneResourceGenerator.ComputeCurrentHash(allNodes, allGraphs)`、標準 Generate 再実行後も optional node/resource/edge/map/addressable が一件ずつ存在することを確認。
- `git diff --stat` と実測 line count を §3.7 の警報へ照合。
- Unity meta/GUID、SceneResource identity、parent-child、Map、Addressables entry の一意性は Editor test evidence で確認し、YAML grep を真実源にしない。

### 5.4 Phase A レビュー実績

- A0/A1 主担当・モデル・ベンダー: Codex primary agent / GPT-5 系 / OpenAI。A0 snapshot を固定し、A1 を作成。
- A2 review input: `docs/handoff/S-4A_RUNTIME_VARIANT_WORKSPACE.md` 533 行、`sha256:d8fc1b74bc31ad27e5f957334f71ddd87514b19707e3ba77c11ab195fd4fc03e`。A0 は metadata 記載 hash。
- A2 architecture gate: 独立 session `s4a_arch_gate` / `gpt-5.6-sol` / OpenAI。初回 `FAIL`、source-backed revision `FAIL`、最終設計版 599 行 `sha256:f9bf476ab24cdd9071dc1f5aa1e440da08d3e6c5a2c05ad48a6e0fb383475843` で `PASS`。必須修正 0。各回とも current code と指定 hash を再照合した。
- A2 runtime/concurrency/test review: 独立 session `s4a_runtime_review` / `gpt-5.6-terra` / OpenAI。初回 `FAIL`、統合設計版 594 行 `sha256:e7d860a887bf65598415ca36f233951ac1112442fa016d55e6e94ae4cdc1ecfd` で `PASS`。必須修正 0。後続差分は architecture gate が要求した SceneResource collision/予約 identity/recovery barrier の強化だけで、runtime contract は変更していない。
- A2 A0-only alternative: 独立 session `s4a_a0_alternative` / `gpt-5.6-luna` / OpenAI。A1 と他 reviewer 所見を読まず、固定 A0 hash と current code だけから代替構成を作成したことを担当が明記。
- A3 統合担当・モデル・ベンダー: Codex primary agent / GPT-5 系 / OpenAI。以下の採否を統合し、修正版を同じ architecture/runtime 担当へ再 gate する。
- C' 用に予約した担当・モデル・ベンダー: GPT-5.5 系 / OpenAI を候補として予約し、Phase A/A2 では使わない。実際の担当は C' 開始時の利用可能性を見て選び、B/C と異なる新規 session/model を最低条件とする。
- 独立性: 3件は別 session/model。二つの A1 review は同一 hash、alternative は A0 のみ。初回 usage-limit failure は入力を読めず所見も生成しなかったためレビュー数に数えず、reset 後の完遂 turn だけを実績とする。

#### A3 findings の採否

- `採用 / High`: WorldWhitebox Player build への値経路不足。現行 `VariantPlayerBuild` が first scene しか overlay しない事実を確認し、RV-9、責務マップ、restore tests、stop 条件を追加した。
- `採用 / High`: Workspace が Map/resource だけを直接更新すると `SceneNodeData + SceneGraphEdges` 正本からの次回 Generate で消える。WW-6〜WW-12 を source-backed transaction と標準 Generate/idempotence gate へ変更した。
- `採用 / High`: 一時 Editor Scene / active scene setup の owner と rollback が未定義。transaction が Additive temp scene を所有し、保存後 close、creator だけが commit 後 open、failure 時 setup 復元とする WW-11 と fault tests を追加した。
- `採用 / High（再 gate）`: planned path 外の stale/orphan `SceneResource` が同 identity を持つと標準 Generate が誤採用する。WW-7 を project-wide identity 0 件 preflight と無変更拒否へ強化した。
- `採用 / High（再 gate）`: co-located 予約 resource は identity 未設定では generator index に載らない。WW-8 で予約直後に `_identity` だけを設定し、WW-12 で generated path/GUID の予約 asset 一致を必須にした。
- `採用 / Medium（再 gate）`: recovery 自身の途中失敗で識別元を先に消す危険。WW-9/10 を GUID checkpoint と dependency-barrier fail-stop にし、後続破壊を止め、同一 journal の二回目 retry test を追加した。
- `採用 / Medium`: parent-race の completion predicate 不足。fresh resident snapshot + Stable + captured `SceneBase` instance identity + session cancellation の全条件へ CP-10 を強化し、ABA test を追加した。
- `採用 / Medium`: in-memory rollback は crash-safe ではない。`Library` の persistent single-pending journal、fail-closed recovery service、明示 exact-target rollback、journal を捨てるだけの command 禁止へ強化した。
- `採用 / Medium`: invalid companion config が allocation 後に失敗する cleanup の観測不足。SceneDirector 未構築、derived failure hook、Framework asset release の startup integration test を追加した。
- `採用 / Medium`: static bridge の cross-test/domain state。injector は同期 queryだけを設定し、tests は previous delegate を `finally` で復元する契約を追加した。
- `採用 / API縮小`: `CellCompanionRole` と inclusion は cross-assembly consumer がないため internal に変更し、public は set と parser だけにした。
- `一部不採用 / API`: architecture reviewer の `ResolveSceneVariant` non-virtual 化。S-4 program §10 が `AbstractApplicationInitializer` の解決口を Framework 公開面として明示しているため `protected virtual` を維持する。ただし non-null/one-shot/default precedence を base validation で固定し、現 SampleGame は override しないこと、app-specific config source だけが将来 consumer であることを追記した。
- `不採用 / 代替案`: editor selection を `IsSelected + Variant` struct にする案。nullable string は `null = profileなし`、`"" = 明示 default` を情報損失なく表せ、追加公開型が不要なので現案を維持する。
- `採用 / 代替案`: residence ticket の必要性。新しい state/API は増やさず、捕捉した普通の C# `SceneBase` instance identity を token として使う。
- `採用 / 代替案`: role 重複/未知 token の扱いを凍結。unknown は warning + skip、duplicate はその role 全件を fail-closed、missing は optional とした。
- `不採用 / 代替案`: `CellCompanionSelector` を独立 class に増やす案。選択は classifier + pure inclusion + driver の Children loop で単一箇所に保て、別の owner/依存/test 方法を生まないため現時点では class を増やさない。driver が 400 行を超えた場合は Phase A 停止条件で再検討する。
- `保留 0`: A2 と再 gate の finding はすべて採用/一部採用/理由付き不採用へ分類済み。最終 architecture gate と runtime review は PASS、必須修正 0。

#### freeze 時点の残存リスク（blocker ではない）

- Phase A のため、標準 Generate の予約 path 採用、Editor scene setup 復元、Addressables rollback、journal retry は未実証である。Phase C の fault-injection evidence が最終証拠になる。
- checkpoint 前の作成直後 crash は「開始時不存在 + pending予定path」を ownership proof とする。pending 中に Workspace 外から同 path/entry を手作業変更した場合は recovery が削除せず fail-closed で止まり、journal を保持して人間へ返す。
- recovery 中に標準 Generate 自体が失敗する環境では自動完了しない。同一 journal を残し、原因解消後の明示 retry を owner とする。
- `SceneResourceGenerator.CleanupOrphanedResources` は default output folder 限定である。co-located resource の通常削除フローは S-4a の create-only + exact rollback 対象外で、必要になった時は別 Phase A で設計する。
- corrupt journal は意図的に fail-closed で、自動削除・自動復旧しない。harvest 時に手動調査手順を公開文書へ残す。
- Editor open の途中 Unity API failure は WW-4 のとおり完全復元対象外である。creation transaction の atomic/recovery contract と混同しない。

## 6. Phase B 実装結果

- 実装: 未到達
- HANDOFF との差: 未到達
- 未実行: 全項目
- implementation head commit: 未到達
- Phase B 担当・モデル・ベンダー: 未選定

## 7. Phase C

- evidence bundle id / hash: 未実施
- 構造適合: 未実施
- findings: 未実施
- テスト結果: 未実施
- 未確認事項: 未実施
- 担当・モデル: 未実施

## 8. Phase C'

- blind audit bundle id / hash: 未実施
- findings: 未実施
- 残存リスク: 未実施
- 監査できなかった範囲: 未実施
- 独立性: 未実施
- 担当・モデル: 未実施

## 9. Phase D

- C / C' の突合: 未到達
- マージ判断: 未到達
- harvest: 未到達
- 削除確認: 未到達
