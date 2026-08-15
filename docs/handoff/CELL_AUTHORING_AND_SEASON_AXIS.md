# Cell オーサリング正本の確立 と 季節軸の導入 ハンドオフ (2026-08-15 / 2026-08-16 改訂)

> Phase A（計画）: Claude Code / Opus 5
> 対象スライス: **S-1「生成器の非破壊化」のみ**。季節化（S-2 以降）は本書 §1 に方針だけ確定させ、実装はしない
> 前スライス: `chore/pending-changes-triage`（コミット 5 本。§0.3 参照）
>
> **2026-08-16 改訂:** 実コードとの突き合わせで 5 点を修正した。事実主張はすべて裏付けが取れている。
>
> 1. **生成器の 7 ファイル分割をやめた**（§2 / §2.1）。§1.2 の「生成 Script は捨てる前提」と、旧 §2 の分割計画・旧受入 A-4「250 行以下・責務 1」が正面衝突していた。抽出するのは純関数 2 ファイルだけ
> 2. **`DeleteOutOfGridCellFolders` という第二の破壊経路**を追記した（§2.2 / §3 T-7 / §5 A-7）。旧版に一切出てこなかった
> 3. **`CellPopulationPlan` の入力に Environment の状態と削除可否を含める**ことを明示した（§3）。旧版の入力定義では T-2b が Plan の外に落ちてテストが書けなくなる
> 4. **policy データの置き場**を「ハードコード静的配列」に確定した（§2.3）
> 5. **§4 にベースラインコミットを追加**した。生成器が untracked のままだとリファクタ差分が `git diff` に出ず、Phase C の構造レビューが成立しない

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
| **Commit** | 同じ空間を複数職種が同時に触れる（職種別ファイル分割） | ❌ 無い |

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
- 下記は**作業ツリーに保留**（破棄も `.gitignore` もしていない）。本スライスの入力。**2026-08-16 に `git status` で再確認した実態**:

```
 M unity/Assets/AddressableAssetsData/AddressableAssetSettings.asset
 M unity/Assets/AddressableAssetsData/AssetGroups/Default Local Group.asset
 M unity/Assets/SampleGame/DependOnAll/Editor/SampleGame.DependOnAll.Editor.asmdef
?? unity/Assets/AddressableAssetsData/ProfileDataSourceSettings.asset (+.meta)
?? unity/Assets/SampleGame/DependOnAll/Editor/PlayerInGameSliceSceneCreator.cs (+.meta)   ← 183 行
?? unity/Assets/SampleGame/DependOnAll/Editor/WorldCellStreamingSliceCreator.cs (+.meta)  ← 1366 行
```

以前ここに書いていた 2 点を訂正した:

- `unity/Assets/Docs/Architecture/21-scene-streaming.md` は**もう modified ではない**（リストから外した）
- `AddressableAssetSettings.asset` は「差分が自然消滅した」と書いていたが**現在 modified**。§4 の Addressables 差分コミットの対象に含めること

`PlayerInGameSliceSceneCreator.cs` は本スライスのロジック変更対象ではないが untracked のままなので、§4 のベースラインコミットに**無変更で一緒に入れる**。

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

遷移（S-2。本スライスでは実装しない）: 次季節は `SwitchScene` せず **`AddScene` で追加ロード**する。範囲外になったらその **Level を丸ごと Unload** する。

**Tunnel は季節ごとに入口/出口を持たせず、`InGameSession` 直下に常設 1 本とする。** 季節ごとに持つと 4×2 = 8 本になり、演出差が要らないうちは無駄。差別化が必要になったら Tunnel を Variant で分ける（Scene を増やさない）。**設計判断としてこう決めた。**

**継ぎ目が Build と Checkout を「実演可能」にする唯一の仕掛け。** 滞在中に次の季節のバンドルを取りに行くので:

- 秋を Checkout していない状態でもトンネルを抜けたら秋が始まる → `20-variant-checkout-workflow.md` のハイブリッド解決の実物デモ
- 冬だけ後から単独ビルドして差し替える → 差分ビルドの実物デモ
- **次 Level が無い（未ビルド / 未 Checkout）ときの Fallback は後で決める。初期はエラーでよい。**

`21-scene-streaming.md` の D-5（セルを `SwitchScene` / `GoBack` / `TransitionPlan` に乗せるな）は Cell に対してそのまま。Season Level の出し入れも画面遷移には乗せない（`AddScene` / 範囲外 Unload）。Tunnel は継ぎ目として残す。

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

**生成コンテンツは捨てる前提。** Script で作ってよいが、生成 Script 自体の Push や、生成物の中身に対する Test は不要。残すテストは手編集を消さないこと（`CellPopulationPlan` の Skip / Populate）だけ。

**Commit 境界は Cell 単体ではなく「同一 Cell フォルダ内の職種別 `.unity` 分割」で確定する。** 「二人が別々の Cell を触る」は何も証明しない — Cell が別ファイルなのは Streaming 境界を切った結果であって、Commit 軸の成果ではない。Commit 軸が証明すべきは**同じ空間を複数職種が同時に触れること**なので、春の受入は「同一 Cell 内で、地形担当が `Cell_x_y.unity` を、背景担当が `Environment_x_y.unity` を同時に編集しても、マージ衝突なくコミットできる」になる。**設計判断としてこう決めた。**

用語の精度: Commit 対象は Environment 単体ではない。**`Cell_x_y.unity` が地形担当の Commit 対象、`Environment_x_y.unity` が追加された職種ファイル**で、両方が Commit 境界の構成要素。したがって §1.3 の「触らない」対象も両方に及ぶ。

**S-1 / S-2 の切り分け（重要）:**

| | 範囲 |
|---|---|
| **S-1（本スライス）** | 現行フラットグリッドの**南辺 4 枚**（`Cell_0_0`〜`Cell_3_0`。既に Environment 萌芽がある 4 枚）で、Cell と Environment の**両方**の非破壊を証明する |
| **S-2** | 季節化。そのとき春の**全 Cell** に Environment を置く |

夏以降は萌芽のままでよい。

### 1.3 正本は季節ごとに違ってよい（択一しない）

「Cell の `.unity` は生成物か手編集物か」を全体で択一する必要はない。1.2 の割り当てなら:

- **春** = 手編集が正本。生成器は初回スキャフォールドのみ。**`Cell_x_y.unity` の `AuthoredRoot` と `Environment_x_y.unity` の中身の両方**について、既存があれば**触らない**
- **夏** = 生成物が正本。再生成で上書きしてよい

**Environment を Skip 対象から外さないこと。** §1.2 で Commit 境界を職種別ファイル分割にした以上、Environment 側の手編集を潰すと Commit 軸の証明が成立しない。加えて §5 の A-3(b)（`Environment_x_y.unity` に手で足した GameObject が残る）が直接落ちる。

**サンプルが証明すべきは「両方を同居させられる」ことなので、片方に決める必要がない。** これが現行のフラット構成では表現できなかった。

### 1.4 順序の制約

現行の `PopulateSingleCellScene` は毎回 `AuthoredRoot` を `DestroyImmediate` して作り直す。人が Unity で手編集した中身は**次の生成で消える**。これが S-1 で直す破綻点（実際には破壊経路は 3 つある。§2.2 の表を見ること）。

「このまま 64 セルにすると生成のたび 64 シーン全文差分」という見積りは **過剰**。各季節は人が作り込む想定なので、グリッド全体を正本として整合させる必要はない。

**S-1 の核は「生成器が手編集を消さない」ことだけ。** 南辺 4 枚（`Cell_0_0`〜`Cell_3_0`）と Environment 4 枚が残れば足りる。Generated セルの再生成差分を 0 にすること、季節化の前に全セルの正本を確定することはやらない。S-2（季節化）は手編集保護ができていれば進めてよい。

### 1.5 本スライス（S-1）でやらないこと

- 季節 Level / Tunnel の追加（S-2）
- Season Level の `AddScene` / 範囲外 Unload、および未ビルド Level の Fallback（S-2。初期はエラーでよい）
- Addressables グループ分割・Variant タグ付与（S-3）
- グリッドサイズの変更（4×4 のまま。3×3 への縮小可否は S-2 で `loadRadius 375m / unloadRadius 550m / セル 250m` の横断が成立するか検証してから判断。**実装値は `WorldCellCatalog.cs` が正**。以前ここに書いていた 150m / 250m は §21 の設計時初期値の写し間違いで、セル 250m では隣接セル中心が desired set に入らず成立しない）
  - ただし**縮小したときに `HandAuthored` な Cell が消えないガードは S-1 で入れる**（§2.2 / §3 T-7 / §5 A-7）。ガード無しで S-2 の縮小を試すと、南辺 4 枚の手編集がフォルダごと消える
- `.gitattributes` の `merge=unityyamlmerge` ドライバ設定（local / global とも未設定で効いていないが、PC 依存の設定なので別途）

---

## 2. 変更対象ファイル一覧（CLAUDE.md A-1: 規模見積もり）

> **ラベルの読み方:** §2〜§3 の `A-1`〜`A-4` は **CLAUDE.md の Phase A チェック項目**を指す。§5 の `A-1`〜`A-7` は**本スライスの受入条件**で、別物。混同しないこと。

現状 `WorldCellStreamingSliceCreator.cs` は **1366 行・39 メソッド・責務 8 つ以上**（レガシー移行 / フォルダ削除 / マテリアル生成 / シーン内容生成 / SceneGraph ノード同期 / Addressables 登録 / Map 圧縮 / グリッド定義）。`AssetDatabase` 密結合でテストが **0 本**。

| ファイル | 現在 → 予想 | 責務数 | 備考 |
|---|---|---|---|
| `SampleGame/DependOnAll/Editor/Cells/CellAuthoringPolicy.cs` | 新規 → ~60 | 1 | **純 C#**。Generated / HandAuthored の宣言と、Cell → policy の解決 |
| `SampleGame/DependOnAll/Editor/Cells/CellPopulationPlan.cs` | 新規 → ~150 | 1 | **純関数**。定義 + 既存状態 + policy → Populate / Skip / 削除可否の計画 |
| `SampleGame/DependOnAll/Editor/WorldCellStreamingSliceCreator.cs` | 1366 → **~1200** | 据え置き | 死コード ~130 行を削除し、破壊的処理を Plan 経由に差し替える。**分割しない**（下記） |
| `OneStarMaker/Tests/Editor/CellPopulationPlanTests.cs` | 新規 → ~220 | — | §3 参照 |
| `OneStarMaker/Tests/Editor/OneStarMaker.Tests.Editor.asmdef` | +1 行 | — | **`SampleGame.DependOnAll.Editor` 参照を追加。これが無いとテストがコンパイルしない**（§3 冒頭） |
| `SampleGame/DependOnAll/Editor/SampleGame.DependOnAll.Editor.asmdef` | +1 行 | — | `SampleGame.InGame` 参照を追加（保留中の差分をそのまま使う） |

### 2.1 (CLAUDE.md A-2) 500 行超をあえて分割しない — 設計判断

CLAUDE.md A-1 / A-2 の「500 行 or 3 責務を超えるなら分割先を明記」は**育つファイル**を想定した規律であり、**寿命が有限と分かっているスキャフォールドには適用しない。設計判断としてこう決めた。**

`WorldSceneGraphSync` / `WorldResourceLinker` / `WorldAddressablesRegistrar` / `CellSceneWriter` に相当する ~830 行は、責務を分けても**誰もテストせず、S-2 完了後に消える**。§1.2 の「生成コンテンツは捨てる前提。生成 Script 自体の Push や、生成物の中身に対する Test は不要」に従い、ここに構造化投資はしない。

**恒久的なのは「生成器が手編集を消さない」という判断だけで、それは純関数 2 ファイルに閉じる。** そこにテストを集中させる（§3）。CLAUDE.md の「テスト要求は構造の指示より強く効く」はこの形で満たす。

代わりに `WorldCellStreamingSliceCreator.cs` の先頭 XML doc に次の一文を足すこと（受入条件 A-4'）:

> このクラスは**スキャフォールド**であり、S-2（季節化）完了後の削除候補である。恒久的な判断は `Cells/CellPopulationPlan.cs` にのみ置き、ここには「どの順で何を呼ぶか」と使い捨ての生成手続きだけを置く。**構造化の投資をしないと決めた（HANDOFF §2.1）。**

これが無いと、次にこのファイルを見た人が「1200 行・責務 8 つ」を負債と読んで再び分割しにかかる。

### 2.2 破壊的処理は 3 箇所しかない — すべて Plan 経由にする

手編集を消しうる経路は次の 3 つで、**S-1 が塞ぐのはこれだけ**。ここ以外は挙動を変えないこと。

| 経路 | 現在の挙動 | S-1 後 |
|---|---|---|
| `PopulateSingleCellScene`（`:740`） | 毎回 `AuthoredRoot` を `DestroyImmediate` して作り直す | **Populate 計画が出た Cell にのみ**実行 |
| `PopulateEnvironmentScene`（`:923`） | 同上（Environment 側の `AuthoredRoot`） | **Populate 計画が出た Environment にのみ**実行 |
| `DeleteOutOfGridCellFolders`（`:387`） | 範囲外 Cell フォルダを `AssetDatabase.DeleteAsset` で `.unity` ごと削除 | **`HandAuthored` な Cell は範囲外でも削除しない**（下記） |

**`DeleteOutOfGridCellFolders` は当初の計画に無かった第二の破壊経路。** S-1 は 4×4 固定なので発火しないが、§1.5 が S-2 で検討するとしている **3×3 縮小がそのまま踏む**ため、今のうちに塞ぐ:

- 削除対象の判定も `CellPopulationPlan` を経由させる。`HandAuthored` な Cell は範囲外でも削除せず `Debug.LogWarning` に留める
- あわせて、判定に使っている `WorldCellCatalog.GridWidth` / `GridHeight`（SampleGame の const）を **`definition.GridWidth` / `definition.GridHeight` に統一する**。同ファイルの他メソッドは `definition` を使っており不整合になっている

`WorldCellGenerator`（FW 側）は**既存 `.unity` を上書きしない**ことを確認済み（`ApplySceneFiles` は `LoadAssetAtPath<SceneAsset>` が非 null なら skip する）。したがって §2.3 の「触らない」で問題ない。

### 2.3 (CLAUDE.md A-3) 既存ファイルへの新責務割り当て

- `CellAuthoringPolicy` を **`OneStarMaker`（FW）側に置かない**。正本の決め方は SampleGame の運用方針であって FW の契約ではない。**設計判断としてこう決めた**
- `WorldGridDefinition`（FW）に policy フィールドを足さない。同じ理由
- **policy データは `CellAuthoringPolicy.cs` 内のハードコード静的配列とする。** 既存の `EnvironmentSproutCells`（`WorldCellStreamingSliceCreator.cs:69` の `Vector2Int[]`）と同じ形。**設計判断としてこう決めた。** 却下した代案:
  - *ScriptableObject 資産* — 純関数性が崩れ、テストがアセット読み込みに依存する。§3 の「`AssetDatabase` に一切依存しない純関数として書け」と正面から衝突する
  - *`SceneResource` にフラグ* — `SceneResource` は FW 側の型なので、FW に SampleGame の運用概念が漏れる（上 2 項と同じ理由）

  S-1 の対象は南辺 4 枚固定なので配列で足りる。資産化は季節が入る S-2 で再検討する
- `OneStarMaker/Scripts/Editor/Streaming/WorldCellGenerator.cs` は**触らない**。`dc8977f`（サブフォルダ化）と `a9bdf99`（S1: `.asset` 側のフォルダ生成漏れ修正）で緑になったばかりで、既存テスト 6 本が乗っている
- **テストは `OneStarMaker/Tests/Editor/` に置く**（SampleGame 側に新規テストアセンブリを作らない）。SUT が SampleGame にあるのにテストが `OneStarMaker.Tests.*` にあるのは一見ねじれだが、**テストアセンブリは依存グラフの頂点なので「FW → Game 禁止」に抵触しない**。既に `OneStarMaker.Tests` が `SampleGame.DependOnAll` / `SampleGame.InGame` / `SampleGame.OutGame` を参照している確立済みのパターンに合わせる。**設計判断としてこう決めた**

  ⚠ **ただしそれは `OneStarMaker.Tests`（ランタイム側）の話で、新規テストを置く `OneStarMaker.Tests.Editor` は SampleGame を一切参照していない。** 現在の参照は `OneStarMaker.Editor` / `OneStarMaker.Runtime` / Addressables / ResourceManager / UniTask のみ。**「もう参照がある」と読んで §3 冒頭の前提作業を飛ばすと必ずコンパイルエラーになる。**

### 2.4 削除するもの（C: 置き換え残骸）

| 対象 | 理由 |
|---|---|
| `MigrateLegacyLayout` / `MoveIfMissing` / `CleanupLegacyFolders` / `TryDeleteAssetFolderIfEmpty`（約 100 行） | `OneStarMakerCommon/World/` → `SampleGame/.../World/` の一度きりの移行。移行は完了済みで、レガシーフォルダは存在しない |
| `DetachSeasonLevels` / `IsSeasonLevel` / `SeasonLevelIdentities` | §1.1 で季節 Level を**復活させる**方針を確定したため、外す処理は有害。S-2 の妨げになる |

削除前に `git status` と Explorer で `Assets/OneStarMakerCommon/World/` が存在しないことを確認すること。

`DeleteOutOfGridCellFolders` は**削除しない**（§2.2 のとおりガードを足して残す）。縮小時の掃除は S-2 で要る。

---

## 3. 単体テストの要求（CLAUDE.md A-4。必須）

> **テスト要求は構造の指示より強く効く。** 「どこに置け」は破られるが「テストを書け」はテスト可能な配置を強制する。2026-08-05 のスライスでは、テストを要求した箇所だけが新規ファイルとして切り出され、要求しなかった約 120 行は `GraphView` サブクラスに埋まってテストが 1 本も書けないまま残った。

**前提作業（先にやる）:** `OneStarMaker/Tests/Editor/OneStarMaker.Tests.Editor.asmdef` の `references` に `SampleGame.DependOnAll.Editor` を足す。現在の参照は `OneStarMaker.Editor` / `OneStarMaker.Runtime` / Addressables / UniTask だけで、**足さないとテストがコンパイルしない**。

`CellPopulationPlan` は **`AssetDatabase` / `EditorSceneManager` に一切依存しない純関数として書け。これができていないとテストが書けない。**

入力は 3 つ:

1. **グリッド定義の値** — 原点 / セルサイズ / `GridWidth` × `GridHeight`（`WorldGridDefinition` そのものではなく値を渡す）
2. **既存セルの状態** — `AssetDatabase` に触れない単純な構造体（`readonly struct`）の集合。1 件あたり次の 4 つを持つ:
   - Cell の identity（`Cell_x_y`）
   - Cell の `AuthoredRoot` の有無
   - **Environment `.unity` が存在するか**
   - **Environment の `AuthoredRoot` の有無**
3. **policy**（`Generated` / `HandAuthored`）

出力は **Cell の Populate / Skip、Environment の Populate / Skip、および削除可否**の計画。

⚠ **Environment の状態を入力に含めること。** ここを省くと T-2b の判定が `CellPopulationPlan` の外（呼び出し側の `if`）に落ち、**テストが 1 本も書けない配置**になる。これは CLAUDE.md 2026-08-05 の `ApplyPaste` と同じ失敗パターンなので、レビュー（Phase C）はここを最初に見ること。

同様に**削除可否も出力に含めること**（§2.2）。`DeleteOutOfGridCellFolders` 側で `if (policy == HandAuthored)` と書くと T-7 が書けなくなる。

`OneStarMaker/Tests/Editor/CellPopulationPlanTests.cs` に最低限これらを要求する:

| # | テスト | 検証内容 |
|---|---|---|
| T-1 | `Generated` な Cell は AuthoredRoot の有無に関わらず Populate | 夏の挙動 |
| T-2 | `HandAuthored` かつ Cell の AuthoredRoot **あり** → **Skip** | 春の挙動。**これが本スライスの核心** |
| T-2b | `HandAuthored` かつ **Environment シーンが既存** → その Environment も **Skip** | §1.3。これが無いと A-3(b) が落ちる |
| T-3 | `HandAuthored` かつ AuthoredRoot **なし** → Populate（初回スキャフォールド） | 春の初回 |
| T-4 | 同じ入力で 2 回計画しても結果が同一（冪等） | 再生成安全性 |
| T-5 | policy 未指定の Cell は既定 `Generated` に落ちる | 既定値の明示 |
| T-6 | グリッド範囲外の既存 Cell は **Populate 計画**に現れない | 縮小時の挙動 |
| T-7 | グリッド範囲外かつ `HandAuthored` な Cell は **削除計画に現れない** | §2.2 のガード。S-2 の縮小検討の前提。範囲外かつ `Generated` なら削除計画に現れること（対の確認）も同テストに含める |

TDD で回すこと: スケルトン + レッドを Unity バッチで確認してから実装する。

**`record` を使わないこと。** このプロジェクトには `IsExternalInit` が無く、`record` を書くとプロジェクト全体がコンパイル不能になる（静的レビューでは出ない）。入出力の構造体は `readonly struct` か通常の `sealed class` で書く。

---

## 4. 実装順序

1. ブランチを切る（develop から）
2. **ベースラインコミット** — `WorldCellStreamingSliceCreator.cs` と `PlayerInGameSliceSceneCreator.cs`（+ 各 `.meta`）を**無変更のまま** 1 コミットする。**以降の差分がガードの追加分だけになり、Phase C の構造レビューが `git diff` で読める。** これをやらないと初コミットが「新規 1200 行」になり、何を変えたのかレビュー側から判別できない
3. `OneStarMaker/Tests/Editor/OneStarMaker.Tests.Editor.asmdef` の `references` に `SampleGame.DependOnAll.Editor` を足す（§3 冒頭。無いとコンパイルしない）
4. `CellAuthoringPolicy` / `CellPopulationPlan` のスケルトンと §3 のテスト 7 本を書き、**レッドを確認**
5. `CellPopulationPlan` を実装してグリーンにする
6. `PopulateSingleCellScene` / `PopulateEnvironmentScene` の `DestroyImmediate` を、**Populate 計画が出た対象に対してのみ**実行するよう変える（§2.2）
7. `DeleteOutOfGridCellFolders` の削除判定を Plan 経由に変え、`WorldCellCatalog.GridWidth/Height` → `definition.GridWidth/Height` へ統一する（§2.2）
8. §2.4「削除するもの」を消す。**削除前に `Assets/OneStarMakerCommon/World/` が存在しないことを確認**
9. `WorldCellStreamingSliceCreator.cs` の先頭 XML doc にスキャフォールド宣言を足す（§2.1 / 受入 A-4'）
10. 春に相当するセル（`Cell_0_0`〜`Cell_3_0` の南辺 4 枚。既に Environment 萌芽がある 4 枚。§1.2 の S-1 / S-2 切り分け参照）を `HandAuthored` に指定する。**Cell 本体と Environment の両方が Skip 対象になることを確認する**
11. 生成器を実行 → **もう一度実行** → §5 の受入条件を確認
12. 保留中の Addressables 差分（`Default Local Group` / `AddressableAssetSettings` / `ProfileDataSourceSettings`）を、生成器の出力と一致する状態で一緒にコミットする

**分割リファクタは行わない**（§2.1）。6〜9 はすべて `WorldCellStreamingSliceCreator.cs` 内の局所変更で、新規ファイルは `Cells/` 配下の 2 本とテスト 1 本だけ。

---

## 5. 受入条件

| # | 条件 | 判定方法 |
|---|---|---|
| A-1 | 全テストが緑 | `pwsh tools/run-tests.ps1`（**Unity を閉じてから**）。464 + 新規 7 本以上、failed 0。**テスト 0 件は失敗扱い**（コンパイルエラーが 0 件として現れる） |
| **A-3** | **手編集が消えない（両方）。これが本スライスの核心的受入条件** | `HandAuthored` 指定した Cell について、(a) `Cell_x_y.unity` の `AuthoredRoot` 配下、(b) `Environment_x_y.unity` の中、それぞれに GameObject を手で 1 つ足す → 生成器を実行 → **両方**残っている |
| A-2 | HandAuthored の `.unity` が 2 回目実行で差分 0 | 1 回目実行 → `git add -A` → 2 回目実行 → `git status --porcelain` に **南辺 4 Cell（`Cell_0_0`〜`Cell_3_0`）と Environment 4 枚** が現れない。Generated 側に差分が出ても受入失敗にしない（生成物は捨てる前提）。**Skip 実装にした時点でほぼ自明に通るので、これ単独を核心と見なさないこと**（回帰検出用） |
| A-4' | スキャフォールド宣言がある | `WorldCellStreamingSliceCreator.cs` の先頭 XML doc に §2.1 の一文があること。目視。**旧 A-4（250 行以下・責務 1）は §2.1 の決定により削除した** |
| A-5 | `DetachSeasonLevels` / レガシー移行コードが存在しない | grep。`DeleteOutOfGridCellFolders` は**残る**（§2.2） |
| A-6 | 既存の Streaming 挙動が不変 | `OneStarMaker.Tests.Streaming` に回帰なし |
| A-7 | `HandAuthored` は範囲外でも消えない | **`WorldCellCatalog.GridWidth` の const を一時的に `3` にして**生成器を実行 → `Cells/Cell_3_0/` が**残っている**（`Cell_3_1`〜`Cell_3_3` は Generated なので消えてよい）→ const を `4` に戻して再実行。**`WorldGridDefinition.asset` を直接書き換える方法では検証できない**（§6.0 の裁定を見ること） |

補足: 終了コード `0xC0000005` は Unity のシャットダウンクラッシュで、テスト結果自体は有効。コード変更を疑う前にログ末尾を見ること。

### 偽 null チェック（Unity 固有・レビュー時に必ず grep）

破棄済み `UnityEngine.Object` は `== null` が true になるが、**`?.` と `??` は Unity の `==` オーバーロードを迂回して短絡しない**。`is null` / `ReferenceEquals` だけでなく **`?.` / `??` も grep すること。** 2026-08-05 のスライスでは HANDOFF 本文とレビューの両方がこの 2 演算子を見落とし、C' 監査が検出した。

---

## 6.0 Phase B からの設計指摘

結論: 破綻 1 件

1. **何が:** 受入 A-7（`WorldGridDefinition.asset` の `_gridWidth` を一時的に 3 にして生成器実行）と、`EnsureGridDefinition`（`:1014`）が毎回 `_gridWidth` / `_gridHeight` を `WorldCellCatalog` 定数で上書きする挙動が両立しない。**なぜ実装できない/壊れる:** `CreateCore` は `DeleteOutOfGridCellFolders` の前に必ず `EnsureGridDefinition` を呼ぶ（`:113`→`:119`）ため、アセットを 3 にしても実行直後に 4 へ戻る。Phase A 追加確定 #4 で削除判定を `definition` に寄せても、縮小が一度も起きず A-7（および T-7 の実機経路）が検証不能になる。変更対象一覧にも `EnsureGridDefinition` の緩和が無い。**代案:** 既存アセットでは `_gridWidth` / `_gridHeight` を上書きしない（新規作成時のみ Catalog 既定を書く）。A-7 は現行どおりアセット側の一時変更で検証する。

確認した論点（破綻なし）: asmdef 参照追加と Plan 公開 API の型制限 / Skip 時も配線継続（Populate のみ省略）と `:712` FileNotFound 維持 / `HandAuthoredCells` と `EnvironmentSproutCells` の非統合 / 生成器非分割 / CreateCore 完了 Log の Catalog 参照はスコープ外。

#### 裁定（Phase A / Claude Code, 2026-08-16）

**指摘 1 は事実として採用。代案は却下し、別解に差し替えた。**

- **事実の確認:** `EnsureGridDefinition`（`:1023`〜`:1027`）は毎回無条件に `_origin` / `_cellSize` / `_gridWidth` / `_gridHeight` / `_parentSceneIdentity` / 出力フォルダを `WorldCellCatalog` の値で上書きする。よって旧 A-7 の「`WorldGridDefinition.asset` の `_gridWidth` を 3 にする」手順は、`CreateCore` が `:113` で `EnsureGridDefinition` を呼んだ時点で 4 に戻り、`DeleteOutOfGridCellFolders`（`:119`）まで届かない。**旧 A-7 は実行不能だった。**
- **代案（既存アセットでは上書きしない）を却下する理由:** `WorldCellCatalog` の XML doc が「アセット側の `WorldGridDefinition` と数値を食い違わせないこと」と明示しており、`EnsureGridDefinition` はその一致を**強制する装置**である。ランタイムの `SessionWorldStreamingDriver`（`:49` / `:89`）はアセットではなく `WorldCellCatalog` の const を読んで desired set を組むため、アセットだけ 3 にすると**存在しない `Cell_3_*` を要求する**乖離が生まれる。S-1 のスコープ外の挙動変更でもある。
- **差し替え後の A-7:** `WorldCellCatalog.GridWidth` の **const を一時的に 3 にする**。`EnsureGridDefinition` がアセットへ伝播し、catalog とアセットが一致したまま縮小が起きるので、`DeleteOutOfGridCellFolders` が正しく発火する。検証後に const を 4 へ戻して再実行する。
- **Phase A 追加確定 #4（削除判定を `definition.GridWidth/Height` へ統一）は維持する。** 上記のとおり const → アセットへ伝播するので値は同じであり、「同一メソッド内で catalog 直参照とアセット参照が混在する」不整合の解消という当初の目的は変わらない。

---

## 6. Phase C からの差し戻し

（未記入）

---

## 7. Phase C レビュー

### 7.1 B1（スケルトン + テスト）の TDD レッド確認 — Claude Code 実行 / 2026-08-16

Grok は `unity/Library` の無い隔離 worktree で作業するため Unity バッチを回せない。HANDOFF §3 の「レッドを Unity バッチで確認してから実装する」はレビュー側が代行した。

`pwsh tools/run-tests.ps1`（コミット `5c3de68`）:

```
total : 472   passed : 464   failed : 8   skipped : 0
```

失敗 8 件はすべて `CellPopulationPlanTests` の `System.NotImplementedException`。**期待どおりのレッド。**

- **コンパイルは通っている** — 既存 464 本が実行され全て緑。asmdef への `SampleGame.DependOnAll.Editor` / `SampleGame.InGame` 追加は機能している
- テスト 0 件ではないので、コンパイルエラーが 0 件として現れる罠には該当しない
- `record` は使われていない（grep 済み）

**確認していないこと:** `CellPopulationPlan.Compute` の中身は未実装なので、判定ロジックの正しさは一切検証していない。`WorldCellStreamingSliceCreator` 側は未着手。

### 7.2 B1 への差し戻し（B2 に同梱）

| # | 指摘 |
|---|---|
| R-1 | **T-3 が Environment 側を assert していない。** `HandAuthored` かつ Environment `.unity` が**存在しない**とき Environment が `Populate` になることを確認するテストが 1 本も無い。現状のテスト 8 本は「HandAuthored なら Environment は常に Skip」という実装でも全部通ってしまい、その実装だと**初回スキャフォールドで Environment の中身が永久に生成されない**。T-3 に `EnvironmentAction == Populate` の assert を足すこと |

### 7.3 構造レビュー（機能レビューより先にやる）

`git diff --stat` から入った。

| 観点 | 結果 |
|---|---|
| `WorldCellStreamingSliceCreator.cs` の増減 | 1366 → **1307 行**（−59）。`CollectExistingStates` / `SceneHasAuthoredRoot` で +約 100、死コード削除で −172。**増えていない** |
| 新責務の所在 | `Cells/CellAuthoringPolicy.cs`（59 行）と `Cells/CellPopulationPlan.cs`（247 行）の 2 本に閉じている。スキャフォールド側に残ったのは「AssetDatabase から状態を読む」アダプタと「計画に従って呼ぶ / 呼ばない」の分岐だけ |
| `if (policy == HandAuthored)` の漏れ | **無し。** `WorldCellStreamingSliceCreator` に `CellAuthoringPolicy` の参照は 1 つも無く、policy 判断はすべて `CellPopulationPlan` の内側。R-2 で見つかった穴（範囲外の判定が呼び出し側のフォールバックに落ちていた）も `ShouldPopulateEnvironment` として計画側へ引き上げた |
| テストが書けないロジックの残存 | 無し。判定は 10 本のテストで覆われている |
| `HandAuthoredCells` と `EnvironmentSproutCells` の統合 | **されていない。** 別ファイルの別配列として残っている（§1.2 の設計判断どおり） |
| スキャフォールド宣言（A-4'） | あり。先頭 `<summary>` に `<para>` で 3 要素（削除候補である / 恒久的判断は `CellPopulationPlan.cs` にのみ置く / 構造化投資をしないと決めた）が入っている |

### 7.4 Phase C からの差し戻しと、その顛末

B↔C は **4 巡**した（上限内）。R-2 / R-3 は**実機を回すまで誰も気づかなかった**もので、静的レビューでは出ない。

| # | 指摘 | 原因 | 対応 |
|---|---|---|---|
| R-1 | T-3 が Environment 側を assert しておらず、「HandAuthored なら Environment は常に Skip」でもテストが全部通る。その実装だと初回スキャフォールドで Environment が永久に空になる | テスト設計の穴 | T-3 に assert 追加 + T-2c 新設（B2） |
| R-2 | **範囲外の `HandAuthored` は、フォルダは守られるのに `Environment_x_y.unity` だけ再生成されて手編集が消える** | **Phase A / C の指示ミス。** `Compute` は範囲内座標にしかエントリを出さないのに、`CreateEnvironmentSprouts` に「エントリが無ければ従来どおり Populate」というフォールバックを書かせた | `CellPopulationPlan.ShouldPopulateEnvironment` を新設して判定を計画側へ移動。計画に無い座標は false。T-8 が回帰テスト（B5） |
| R-3 | **生成器が `Cannot create a new scene additively with an untitled scene unsaved` で落ちる。`.unity` が 1 つも作られない** | `CollectExistingStates` が Additive で開閉を繰り返した結果、Unity が未保存 untitled シーンを残す。直後の `WorldCellGenerator.ApplySceneFiles` が `NewScene(Additive)` で必ず失敗する | 収集後に `OpenScene(WorldScenePath, Single)` で保存済みシーンへリセット（B6 / B7） |

**R-3 は clone 直後の初回実行が必ず落ちるバグだった。** 16 セルが揃っている間は `ApplySceneFiles` が既存 `.unity` を skip するため発火せず、**A-7 の縮小 → 復元でグリッドを作り直したときに初めて表に出た**。受入条件を実機で通していなければ、そのままマージされていた。

### 7.5 受入条件の判定

すべて Claude Code が実機で確認した。

| # | 結果 | 証拠 |
|---|---|---|
| A-1 | ✅ | `pwsh tools/run-tests.ps1` → **477 / 477 passed, failed 0**（exit 0）。内訳は既存 464 + 新規 13（T-1〜T-11 + T-2b + T-2c）。テスト 0 件ではない |
| **A-3** | ✅ | `HandEditProbe.StampHandEdits` で南辺 4 Cell の `AuthoredRoot` 配下と Environment 4 枚の `AuthoredRoot` 配下に計 8 個の GameObject を置き、生成器を実行 → `VerifyHandEdits` が **exit 0（8/8 生存）**。縮小（3 幅）を挟んでも 8/8 |
| A-2 | ✅ | クリーンな作業ツリーから生成器を 1 回実行した `git status --porcelain` に、**`Cell_0_0`〜`Cell_3_0` と `Environment_0_0`〜`Environment_3_0` が 1 つも現れない**。生成器のログも `Populated authored visuals in 12 cell scenes (skipped=4)` |
| A-4' | ✅ | 目視。§7.3 参照 |
| A-5 | ✅ | grep で `DetachSeasonLevels` / `IsSeasonLevel` / `SeasonLevelIdentities` / `MigrateLegacyLayout` / `CleanupLegacyFolders` / `MoveIfMissing` / `TryDeleteAssetFolderIfEmpty` がヒットしない。`DeleteOutOfGridCellFolders` は残っている |
| A-6 | ✅ | `OneStarMaker.Tests.Streaming` を含む既存 464 本に回帰なし |
| A-7 | ✅ | `WorldCellCatalog.GridWidth` を一時的に 3 にして生成器を実行 → `範囲外だが HandAuthored なので保持した: Cell_3_0` を出力し、`Cell_3_1`〜`Cell_3_3` は削除。`Cells/Cell_3_0/` は残り、**その中の手編集プローブも生存**（R-2 修正後）。**SceneGraph ノード `Cell_3_0.asset` / `Environment_3_0.asset` も生存**（§7.7 S2 修正後、`kept=17`）。const を 4 に戻して再実行し 16 セルに復元 |

**A-7 の判定方法は §6.0 の裁定どおり `WorldCellCatalog` の const 側を触った。** `WorldGridDefinition.asset` を直接書き換える旧手順では `EnsureGridDefinition` に上書きされて検証できない。

### 7.6 偽 null チェック（`?.` / `??` / `is null` / `ReferenceEquals`）

grep した結果、`WorldCellStreamingSliceCreator.cs` に `??` が 8 箇所ある。**すべてこのスライス以前からある既存コードで、新規に持ち込んだものは 0 件。** むしろ B3 は `CreateEnvironmentSprouts` の `LoadAssetAtPath<SceneResource>(...) ?? throw` を `== null` の明示チェックへ**直している**。

残る 8 箇所を評価した結果、**実害は無いと判断した**（対応しない）:

- `LoadAssetAtPath<T>(...) ?? throw`（5 箇所）— `LoadAssetAtPath` は「見つからない」ときに**本物の null** を返す。破棄済みオブジェクトを返す API ではないので `??` が短絡する
- `Shader.Find(...) ?? Shader.Find(...)`（2 箇所）— 同上
- `GetComponent<T>() ?? GetComponentInChildren<T>()`（1 箇所）— 同上

`Cells/` 配下の `??` は `string` と `IReadOnlyList<T>` に対するもので `UnityEngine.Object` ではない。

**これは既存の負債ですらなく、偽陽性である。** ただし「grep して評価した」ことは記録しておく（次のレビュアが同じ grep で同じ 8 件を踏むため）。

### 7.7 cursor[bot]（PR #21）の指摘と対応 — 唯一の非 Claude / 非 Grok の目

CLAUDE.md の「PR を立てて cursor[bot] のレビューを受けるまでを 1 スライスの完了条件に含める」に従って `@cursoragent code-review` を投げた。**Opus 5（C）と Codex 5.3 High（C'）が揃って見落としたものを 4 件拾った。** §8 の C' が「指摘なし」で終わっていることと合わせて読むこと。

| # | 重大度 | 指摘 | 対応 |
|---|---|---|---|
| B1 | **Blocker** | **PR の base が `main`。** `origin/main` は `5b84409 Initial commit` だけで、実質の統合先は `develop`（main から 143 コミット先）。このままマージすると develop 以降の全履歴が main に乗り、Files changed も 2690 files でレビュー不能だった | base を `develop` に付け替えた（51 files）。コード変更なし |
| S1 | Should-fix | `CellExistingState.HasEnvironmentScene` が死データ。`Compute` は `HasEnvironmentAuthoredRoot` しか読んでいないのに、T-2b の名前と assert 文言は「`.unity` が既存なら Skip」のままだった | XML doc に判定へ使わない理由を明記。T-2b をリネームし文言を修正。**Cell Populate × Environment Skip の独立行列を T-10 で固定** |
| S2 | Should-fix | **範囲外の `HandAuthored` の `SceneNodeData` はまだ刈られる。** `.unity` は守るのに `SyncSceneGraph` の `keepIdentities` が範囲内しか見ておらず、`PruneStaleCellSceneGraphNodes` が `Cell_3_0` / `Environment_3_0` のノードを消していた。**R-2 と完全に同じ形の穴** | `CellPopulationPlan.IsDeletable` を足して keep 判定を計画側へ寄せた（T-11）。実機で `kept=17`、`Cell_3_0.asset` / `Environment_3_0.asset` の生存を確認 |
| S3 | Should-fix | `CreateEnvironmentSprouts` が計画ではなく `EnvironmentSproutCells` を走査するため、親 Cell が削除済みだと `FileNotFoundException` で生成器が落ちる。現在は sprout == HandAuthored で一致しているため潜在 | 親 Cell の `.asset` が無い座標は `continue`（正常系）。`skipped` をログに追加 |

**B1 は Phase C（Claude Code）の手順ミス。** リポジトリの既定ブランチが `main` だという前提で PR を立てた。`docs/` にも CLAUDE.md にも「統合先は `develop`」と書かれておらず、C' も HANDOFF しか読まないので気づけなかった。**恒久対策は Phase D で `docs/` 側に統合先を明記すること。**

**S2 の教訓: R-2 を直したときに「同じ形の穴が他にないか」を横展開しなかった。** 「範囲外だが保持する `HandAuthored`」という新しい状態を作った以上、その状態を知らないコードは `SyncSceneGraph` 以外にもあり得た。Phase C は 1 件直したら同型を探すこと。

### 7.8 確認していないこと

- **Play モードでの実挙動を一度も見ていない。** Streaming が実際に動くか、`SessionWorldStreamingDriver` が 16 セルを正しく要求するかは EditMode テストと生成器の出力でしか担保していない
- **`git clone` 直後の完全な初回生成を通していない。** R-3 の修正は「グリッド縮小 → 拡大で 3 セルを新規作成する」経路で確認したもので、`Cells/` フォルダ自体が存在しない状態からの実行は試していない。同じ `ApplySceneFiles` の経路を通るので直っているはずだが、**未検証**
- **`CollectExistingStates` はグリッド 16 セル分の `.unity` を毎回開いて閉じる。** 実行時間への影響を測っていない（体感では生成器全体が 1〜4 分で、以前と大きく変わらない）
- **`DeleteOutOfGridCellFolders` の Map 掃除の網羅性が少し狭まった。** 以前は `Cells/` 配下の範囲外 `SceneResource` を無条件に Map から外していたが、現在は `Cell_*` という名前のフォルダに属するものだけが対象。`Cell_*` 以外の場所に stray な `SceneResource` があった場合は掃除されない。実害は確認していない
- **Environment 側の Skip 条件を「`.unity` の有無」から「`AuthoredRoot` の有無」へ精密化した**（§6.0 の裁定と B2 のコミットメッセージ参照）。これは HANDOFF §3 T-2b の文言からの逸脱なので、C' は妥当性を独立に判断すること

---

## 8. Phase C' 監査

結論: 指摘なし
論点 1: 確認したが問題なし。根拠は、恒久判断（HandAuthored/Generated の判定と Populate/Skip/削除可否）が `CellAuthoringPolicy` と `CellPopulationPlan` に閉じており、`WorldCellStreamingSliceCreator` は計画結果を消費するだけで policy 分岐を持たないこと、かつ Skip 時も `EnsureEnvironmentSceneFile` / `EnsureEnvironmentResource` / `EnsureChildLink` / `SetDirty` が維持されて配線が壊れないこと。
論点 1: `CellPopulationPlan` は `AssetDatabase` / `EditorSceneManager` 非依存で同一入力に対して決定的であり、Environment の Skip 条件を「`.unity` 有無」ではなく「`AuthoredRoot` 有無」にした裁定も、`.unity` だけ残る半端状態を自己回復できるため妥当。
論点 2: 確認したが問題なし。根拠は、`WorldCellStreamingSliceCreator` 内で Cell/Environment の手編集を消し得る `.unity` 破壊経路が `PopulateSingleCellScene` / `PopulateEnvironmentScene` / `DeleteOutOfGridCellFolders` の 3 経路に限定され、いずれも Plan 経由で Skip・削除抑止されること。
論点 2: `CollectExistingStates` と Populate/Skip 実行の間に `WorldCellGenerator.Generate` が入るが、同生成器は既存 `.unity` を上書きせず不足分の新規作成のみを行うため、状態ずれで手編集が消える経路は成立しない（範囲外 Environment の再生成漏れも `ShouldPopulateEnvironment` で閉じている）。
