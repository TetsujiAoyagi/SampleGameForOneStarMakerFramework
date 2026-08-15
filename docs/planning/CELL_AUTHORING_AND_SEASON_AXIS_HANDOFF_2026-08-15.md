# Cell オーサリング正本の確立 と 季節軸の導入 ハンドオフ (2026-08-15)

> Phase A（計画）: Claude Code / Opus 5
> 対象スライス: **S-1「生成器の非破壊化と分割」のみ**。季節化（S-2 以降）は本書 §1 に方針だけ確定させ、実装はしない
> 前スライス: `chore/pending-changes-triage`（コミット 5 本。§0.3 参照）

---

## 0. 1分で把握

### 0.1 何が問題か

SampleGame の存在理由は **Build / Commit / Checkout / Streaming を一度に試せるサンプル**であること。しかし現状は **Streaming しか実証できていない**。

四つの動詞はそれぞれ別の境界を要求する。境界が 1 本（`Cell_x_y`）しかないので、残り 3 つは付く場所が無い:

| 動詞 | 必要な境界の性質 | 2026-08-15 時点の担い手 |
|---|---|---|
| **Streaming** | 距離で頻繁に跨ぐ / 小さい / 多数 / 均質 | ✅ `Cell_x_y`（250m, `LoadType.OnDemand`） |
| **Build** | バンドル / グループに一致 / 少数 / 独立に出荷できる | ❌ 無い |
| **Checkout** | 一部だけ sparse-checkout しても Play できる | ❌ 無い |
| **Commit** | 二人が同じファイルを触らない大きさ / 人が手で書く | ❌ 無い |

裏付け（すべて実測）:

- Addressables グループは **1 個（`Default Local Group`）に 32 エントリ全部**（Cell 16 + Environment 4 + World + UI + Title + config…）
- `Cell_0_0.asset` の `Variant:` は **空文字**。`WorldCellStreamingSliceCreator.EnsureWorldResource` が `Variant = string.Empty` を**明示的に書き込んでいる**
- `Docs/Architecture/21-scene-streaming.md` 「フォルダ境界 | Scene identity の**実行単位**とディスクフォルダを揃える」— フォルダ軸は実行単位側に固定済み
- `Docs/Architecture/27-folder-structure.md` の「二軸」は A=Assembly / B=Scene 同居 であって、**「編集 × 実行」ではない**

### 0.2 現行スライスは季節構想から後退している（重要）

`WorldCellStreamingSliceCreator.cs`（未追跡）に残骸がある:

```csharp
private static readonly string[] SeasonLevelIdentities = { "SpringLevel", "SummerLevel", "AutumnLevel", "WinterLevel" };
private static void DetachSeasonLevels(SceneResource session, SceneResourceMap map)  // InGameSession の子から外す
```

`Assets/OneStarMakerCommon/SceneMap/` に `SpringLevel.asset` 等は**もう存在しない**。つまり現在のフラットな `InGameSession → World → Cell_x_y` は季節構想の「前」ではなく「後」で、**Build / Checkout を担うはずだった中間層をこのスライスが明示的に取り外した**。

### 0.3 直前スライスで確定済み（やり直さないこと）

ブランチ `chore/pending-changes-triage` にコミット済み:

| コミット | 内容 |
|---|---|
| `cf325fb` | `docs(architecture)` §27〜29 新規 + 索引・相互リンク |
| `6d28d60` | `docs(planning)` HANDOFF 8 本 |
| `69811b1` | `fix(meta)` `TelemetryKind.cs.meta` の GUID 衝突解消（`a1b2c3d4…` は手打ちの偽値で `PlayerRigBindings.cs.meta` と重複していた） |
| `fe90b45` | `chore(build)` 起動シーン `SampleScene` → `Title` |
| `dc8977f` | `fix(streaming)` `WorldCellGenerator` の出力を `Cells/{id}/{id}.unity` へ。**develop のレッドテストを修復**（テストだけ先にコミットされていた） |

検証: `pwsh tools/run-tests.ps1` → **464 / 464 passed, failed 0**（exit 0）。

意図的に**コミットしなかった**もの:

- `WorldGridDefinition.cs` の既定値を SampleGame パスへ変える差分は **revert 済み**。OneStarMaker(FW) が SampleGame のパスを既定値に持つのは「FW → Game 参照禁止」に反する。実インスタンス `SampleGame/InGame/InGameSession/World/WorldGridDefinition.asset` が既に正しいパスを保持し、テストも `Assets/Test/...` を明示設定するため機能差はない
- 下記 10 本は**作業ツリーに保留**（破棄も `.gitignore` もしていない）。本スライスの入力:

```
 M unity/Assets/AddressableAssetsData/AssetGroups/Default Local Group.asset
 M unity/Assets/Docs/Architecture/21-scene-streaming.md
 M unity/Assets/SampleGame/DependOnAll/Editor/SampleGame.DependOnAll.Editor.asmdef
?? unity/Assets/AddressableAssetsData/ProfileDataSourceSettings.asset (+.meta)
?? unity/Assets/SampleGame/DependOnAll/Editor/PlayerInGameSliceSceneCreator.cs (+.meta)
?? unity/Assets/SampleGame/DependOnAll/Editor/WorldCellStreamingSliceCreator.cs (+.meta)
```

`AddressableAssetSettings.asset`（`m_currentHash` のゼロ化）はテスト実行時の Unity バッチ起動でハッシュが再計算され、差分が自然消滅した。

---

## 1. 確定方針（Phase A の設計判断。実装で変えないこと）

### 1.1 季節構想を採る

#### 用語（混ぜないこと）

| 語 | 意味 | 実体 |
|---|---|---|
| **境界** | 何が何と分かれるか | Season Level / Cell / Environment |
| **継ぎ目 (seam)** | 境界を跨ぐ「時間」を隠す場所。境界そのものではない | Tunnel |
| **実証担当** | その動詞を README で指させる季節 | 春=Commit / 夏=Streaming / 秋=Checkout / 冬=Build |

「Tunnel = Build 単位」とは書かない。Tunnel は**継ぎ目**であって境界ではない。

#### ツリー

四段の入れ子が、四つの動詞にちょうど対応する:

```
InGameSession
  ├── Tunnel                        ← 継ぎ目（常設 1 本）。SwitchScene 対象
  ├── SpringLevel                   ← Build 境界 = Addressables グループ / Checkout 境界 = Variant タグ
  │     └── Cell_0_0 (OnDemand)     ← Streaming 境界（距離で跨ぐ）
  │           └── Environment_0_0   ← Commit 境界（職種別。人が手で書く）
  ├── SummerLevel                   ← 以下同型
  ├── AutumnLevel
  └── WinterLevel
```

遷移: `SwitchScene(Tunnel)` → 滞在中に次季節をロード → `SwitchScene(NextSeason)`。

**Tunnel は季節ごとに入口/出口を持たせず、`InGameSession` 直下に常設 1 本とする。** 季節ごとに持つと 4×2 = 8 本になり、演出差が要らないうちは無駄。差別化が必要になったら Tunnel を Variant で分ける（Scene を増やさない）。**設計判断としてこう決めた。**

**継ぎ目が Build と Checkout を「実演可能」にする唯一の仕掛け。** 滞在中に次の季節のバンドルを取りに行くので:

- 秋を Checkout していない状態でもトンネルを抜けたら秋が始まる → `20-variant-checkout-workflow.md` のハイブリッド解決の実物デモ
- 冬だけ後から単独ビルドして差し替える → 差分ビルドの実物デモ

`21-scene-streaming.md` の D-5（セルを `SwitchScene` / `GoBack` / `TransitionPlan` に乗せるな）には**抵触しない**。禁止対象は **Cell** であって、Season Level と Tunnel は画面遷移の語彙に乗ってよい。既存の `LoadingDisplayType` / `TransitionPlan` に載る。

線形の進行（春→夏→秋→冬）は偶然ではなく利点。「次のものだけあれば進める」という性質が、部分 Checkout の証明をそのまま与える。

### 1.2 四季を同じものの 4 コピーにしない

4 季節 × 16 セル = 64 セルはコストが 4 倍になるだけで証明力は増えない。**各季節に別の実証責務を割り当てる:**

| 季節 | 実証する動詞 | 具体的な差 |
|---|---|---|
| **春** | **Commit** | セルの中身は手編集。生成器は骨格だけ作り中身に触らない。**同一 Cell の中を複数職種が同時に触っても衝突しない**ことを示す（下記） |
| **夏** | **Streaming** | 生成器で量産した均質グリッド。`21-scene-streaming.md` §9 の受入条件 A-1〜A-5 はここで実測する |
| **秋** | **Checkout** | ローカルに Checkout しない前提。リモートカタログから解決される |
| **冬** | **Build** | 別 Addressables グループ + 別 Variant。後から単独ビルドして差し替えられることを示す |

コンテンツ量は今の 4×4 グリッド 1 個分 + α で足りる（秋・冬は夏のグリッドを Variant 違いで使い回す）。`README` で「どの動詞がどこで実証されているか」を 1 行ずつ指させる。

**Commit 境界は Cell ではなく Environment（職種別子シーン）で確定する。** 「二人が別々の Cell を触る」は何も証明しない — Cell が別ファイルなのは Streaming 境界を切った結果であって、Commit 軸の成果ではない。Commit 軸が証明すべきは**同じ空間を複数職種が同時に触れること**なので、春の受入は「同一 Cell 内で、地形担当が `Cell_x_y.unity` を、背景担当が `Environment_x_y.unity` を同時に編集しても、マージ衝突なくコミットできる」になる。**設計判断としてこう決めた。**

これに伴い、春の Environment は「一部の Cell だけの萌芽」ではなく**全 Cell に置く**（現状は南辺 4 枚のみ）。夏以降は萌芽のままでよい。

### 1.3 正本は季節ごとに違ってよい（択一しない）

「Cell の `.unity` は生成物か手編集物か」を全体で択一する必要はない。1.2 の割り当てなら:

- **春** = 手編集が正本。生成器は初回スキャフォールドのみ。既存の `AuthoredRoot` があれば**触らない**
- **夏** = 生成物が正本。再生成で上書きしてよい

**サンプルが証明すべきは「両方を同居させられる」ことなので、片方に決める必要がない。** これが現行のフラット構成では表現できなかった。

### 1.4 順序の制約（守らないと破綻する）

**正本を決める前に季節化してはいけない。** 現行の `PopulateSingleCellScene` は毎回 `AuthoredRoot` を `DestroyImmediate` して作り直すため:

- 再生成すると Cell 16 + Environment 4 のシーンが**全文差分**になる（内容が同一でも fileID が総入れ替え）
- 人が Unity で手編集した中身は**次の生成で消える**

このまま 64 セルにすると生成のたび 64 シーン全文差分。よって **S-1（本スライス）= 正本の確立、S-2 = 季節化**。

### 1.5 本スライス（S-1）でやらないこと

- 季節 Level / Tunnel の追加（S-2）
- Addressables グループ分割・Variant タグ付与（S-3）
- グリッドサイズの変更（4×4 のまま。3×3 への縮小可否は S-2 で `loadRadius 150m / unloadRadius 250m / セル 250m` の横断が成立するか検証してから判断）
- `.gitattributes` の `merge=unityyamlmerge` ドライバ設定（local / global とも未設定で効いていないが、PC 依存の設定なので別途）

---

## 2. 変更対象ファイル一覧（A-1: 規模見積もり）

現状 `WorldCellStreamingSliceCreator.cs` は **1366 行・約 45 メソッド・責務 8 つ以上**（レガシー移行 / フォルダ削除 / マテリアル生成 / シーン内容生成 / SceneGraph ノード同期 / Addressables 登録 / Map 圧縮 / グリッド定義）。CLAUDE.md A-1 の「500 行 or 3 責務」を大幅超過し、`AssetDatabase` 密結合でテストが **0 本**。

| ファイル | 現在 → 予想 | 責務数 | 備考 |
|---|---|---|---|
| `SampleGame/DependOnAll/Editor/WorldCellStreamingSliceCreator.cs` | 1366 → **≤ 250** | 1（オーケストレーションのみ） | 未追跡。本スライスで初コミットする |
| `SampleGame/DependOnAll/Editor/Cells/CellAuthoringPolicy.cs` | 新規 → ~80 | 1 | **純 C#**。Generated / HandAuthored の宣言と、Cell → policy の解決 |
| `SampleGame/DependOnAll/Editor/Cells/CellPopulationPlan.cs` | 新規 → ~150 | 1 | **純関数**。定義 + 既存状態 + policy → Populate / Skip の計画 |
| `SampleGame/DependOnAll/Editor/Cells/CellSceneWriter.cs` | 新規 → ~250 | 1 | `AssetDatabase` / `EditorSceneManager` I/O のみ |
| `SampleGame/DependOnAll/Editor/Cells/WorldSceneGraphSync.cs` | 新規 → ~200 | 1 | SceneGraph ノード / エッジ同期 |
| `SampleGame/DependOnAll/Editor/Cells/WorldResourceLinker.cs` | 新規 → ~280 | 1 | SceneResource 親子 / Map 登録・圧縮 |
| `SampleGame/DependOnAll/Editor/Cells/WorldAddressablesRegistrar.cs` | 新規 → ~100 | 1 | Addressables エントリ登録 |
| `OneStarMaker/Tests/Editor/CellPopulationPlanTests.cs` | 新規 → ~200 | — | §3 参照 |
| `OneStarMaker/Tests/Editor/OneStarMaker.Tests.Editor.asmdef` | +1 行 | — | **`SampleGame.DependOnAll.Editor` 参照を追加。これが無いとテストがコンパイルしない**（§3 冒頭） |
| `SampleGame/DependOnAll/Editor/SampleGame.DependOnAll.Editor.asmdef` | +1 行 | — | `SampleGame.InGame` 参照を追加（保留中の差分をそのまま使う） |

### A-2: 500 行 / 3 責務を超える見込みへの対処

上表が分割先。**`WorldCellStreamingSliceCreator.cs` に新しいロジックを足さないこと。** 同ファイルに残してよいのは「どの順で何を呼ぶか」だけで、判断・生成・I/O はすべて上記の新ファイルへ置く。

### A-3: 既存ファイルへの新責務割り当て

- `CellAuthoringPolicy` を **`OneStarMaker`（FW）側に置かない**。正本の決め方は SampleGame の運用方針であって FW の契約ではない。**設計判断としてこう決めた**
- `WorldGridDefinition`（FW）に policy フィールドを足さない。同じ理由
- `OneStarMaker/Scripts/Editor/Streaming/WorldCellGenerator.cs` は**触らない**。`dc8977f` / `9a1c4e2`（S1 修正）で緑になったばかりで、既存テスト 6 本が乗っている
- **テストは `OneStarMaker/Tests/Editor/` に置く**（SampleGame 側に新規テストアセンブリを作らない）。SUT が SampleGame にあるのにテストが `OneStarMaker.Tests.*` にあるのは一見ねじれだが、`OneStarMaker.Tests` は既に `SampleGame.DependOnAll` / `SampleGame.InGame` / `SampleGame.OutGame` を参照しており、**テストアセンブリは依存グラフの頂点なので「FW → Game 禁止」に抵触しない**。確立済みのパターンに合わせる。**設計判断としてこう決めた**

### 削除するもの（C: 置き換え残骸）

| 対象 | 理由 |
|---|---|
| `MigrateLegacyLayout` / `MoveIfMissing` / `CleanupLegacyFolders` / `TryDeleteAssetFolderIfEmpty`（約 100 行） | `OneStarMakerCommon/World/` → `SampleGame/.../World/` の一度きりの移行。移行は完了済みで、レガシーフォルダは存在しない |
| `DetachSeasonLevels` / `IsSeasonLevel` / `SeasonLevelIdentities` | §1.1 で季節 Level を**復活させる**方針を確定したため、外す処理は有害。S-2 の妨げになる |

削除前に `git status` と Explorer で `Assets/OneStarMakerCommon/World/` が存在しないことを確認すること。

---

## 3. A-4: 単体テストの要求（必須）

> **テスト要求は構造の指示より強く効く。** 「どこに置け」は破られるが「テストを書け」はテスト可能な配置を強制する。2026-08-05 のスライスでは、テストを要求した箇所だけが新規ファイルとして切り出され、要求しなかった約 120 行は `GraphView` サブクラスに埋まってテストが 1 本も書けないまま残った。

**前提作業（先にやる）:** `OneStarMaker/Tests/Editor/OneStarMaker.Tests.Editor.asmdef` の `references` に `SampleGame.DependOnAll.Editor` を足す。現在の参照は `OneStarMaker.Editor` / `OneStarMaker.Runtime` / Addressables / UniTask だけで、**足さないとテストがコンパイルしない**。

`CellPopulationPlan` は **`AssetDatabase` / `EditorSceneManager` に一切依存しない純関数として書け。これができていないとテストが書けない。** 入力は「グリッド定義の値（原点・セルサイズ・N×N）」「既存セルの状態（identity と AuthoredRoot の有無を表す単純な構造体の集合）」「policy」で、出力は Populate / Skip の計画。

`OneStarMaker/Tests/Editor/CellPopulationPlanTests.cs` に最低限これらを要求する:

| # | テスト | 検証内容 |
|---|---|---|
| T-1 | `Generated` な Cell は AuthoredRoot の有無に関わらず Populate | 夏の挙動 |
| T-2 | `HandAuthored` かつ AuthoredRoot **あり** → **Skip** | 春の挙動。**これが本スライスの核心** |
| T-3 | `HandAuthored` かつ AuthoredRoot **なし** → Populate（初回スキャフォールド） | 春の初回 |
| T-4 | 同じ入力で 2 回計画しても結果が同一（冪等） | 再生成安全性 |
| T-5 | policy 未指定の Cell は既定 `Generated` に落ちる | 既定値の明示 |
| T-6 | グリッド範囲外の既存 Cell は計画に現れない | 縮小時の挙動 |

TDD で回すこと: スケルトン + レッドを Unity バッチで確認してから実装する。

---

## 4. 実装順序

1. ブランチを切る（`chore/pending-changes-triage` から、または develop へマージ後に develop から）
2. `CellAuthoringPolicy` / `CellPopulationPlan` のスケルトンと §3 のテスト 6 本を書き、**レッドを確認**
3. `CellPopulationPlan` を実装してグリーンにする
4. `CellSceneWriter` を切り出す。`PopulateSingleCellScene` の `DestroyImmediate` は **Populate 計画が出たセルに対してのみ**実行する
5. `WorldSceneGraphSync` / `WorldResourceLinker` / `WorldAddressablesRegistrar` を機械的に移す（挙動を変えない）
6. `WorldCellStreamingSliceCreator` をオーケストレーションだけに削る。§2 の削除対象を消す
7. 春に相当するセル（暫定: `Cell_0_0`〜`Cell_3_0` の南辺 4 枚。現在 Environment 萌芽がある 4 枚と揃える）を `HandAuthored` に指定する
8. 生成器を実行 → **もう一度実行** → §5 の冪等性を確認
9. 保留中の Addressables 差分（`Default Local Group` / `AddressableAssetSettings` / `ProfileDataSourceSettings`）を、生成器の出力と一致する状態で一緒にコミットする

---

## 5. 受入条件

| # | 条件 | 判定方法 |
|---|---|---|
| A-1 | 全テストが緑 | `pwsh tools/run-tests.ps1`（**Unity を閉じてから**）。464 + 新規 6 本以上、failed 0。**テスト 0 件は失敗扱い**（コンパイルエラーが 0 件として現れる） |
| A-2 | **生成器を 2 回連続実行して `.unity` の差分が 0** | 1 回目実行 → `git add -A` → 2 回目実行 → `git status --porcelain` に `.unity` が現れない。**これが本スライスの核心的受入条件** |
| A-3 | 手編集が消えない | `HandAuthored` 指定した Cell の `AuthoredRoot` 配下に GameObject を手で 1 つ足す → 生成器を実行 → その GameObject が残っている |
| A-4 | `WorldCellStreamingSliceCreator.cs` が 250 行以下・責務 1 | `git diff --stat` と目視 |
| A-5 | `DetachSeasonLevels` / レガシー移行コードが存在しない | grep |
| A-6 | 既存の Streaming 挙動が不変 | `OneStarMaker.Tests.Streaming` に回帰なし |

補足: 終了コード `0xC0000005` は Unity のシャットダウンクラッシュで、テスト結果自体は有効。コード変更を疑う前にログ末尾を見ること。

### 偽 null チェック（Unity 固有・レビュー時に必ず grep）

破棄済み `UnityEngine.Object` は `== null` が true になるが、**`?.` と `??` は Unity の `==` オーバーロードを迂回して短絡しない**。`is null` / `ReferenceEquals` だけでなく **`?.` / `??` も grep すること。** 2026-08-05 のスライスでは HANDOFF 本文とレビューの両方がこの 2 演算子を見落とし、C' 監査が検出した。

---

## 6. Phase C からの差し戻し

（未記入）

---

## 7. Phase C レビュー

（未記入）

---

## 8. Phase C' 監査

（未記入）
