# Cell 子シーン萌芽 — 次セッション HANDOFF

> 作成日: 2026-07-26  
> 対象: 次 Agent / 人間  
> 前提: T-07 Cell Streaming（World + Cell_{x}_{y} + WSC）は SampleGame に配線済み  
> 関連: [21-scene-streaming.md](../../unity/Assets/Docs/Architecture/21-scene-streaming.md)、会話合意（Full ティア = Unity シーン Load）

---

## 0. 今どこまで終わっているか（読み飛ばし用）

| 項目 | 状態 |
|---|---|
| 四季 Level / トンネル Coordinator | **破棄済み** |
| `InGameSession` → `WorldStreamingController` + Focus | **済** |
| `World` NecessaryAlways / `Cell_*` OnDemand × 10×10 | **済** |
| Cell 見た目はシーン事前配置 + 共有 Lit + MPB | **済**（メニュー再実行が未なら要実行） |
| Cell 配下の職種別子シーン | **未着手（本 HANDOFF の主題）** |

現行ツリー（実装済み）:

```
InGameSession
  ├── PlayerScene / InGameUI / World   (NecessaryAlways)
  │     └── Cell_{x}_{y}               (OnDemand・いまは葉)
  └── Result
```

---

## 1. ユーザー意図（ここが正）

Cell は「距離ストリーミングの境界」であると同時に、**人間がわちゃわちゃ作業する大きさの作業単位**である。

- Cell は十分大きく、中で複数職種が並走する
- **Cell の下に子 Unity シーンを付ける**（Environment / NPC / VFX / Gimmick 等）
- 粒度は職種によって違ってよい（同じ Cell でも Environment は粗い / VFX は細かい）
- 次セッションではフル実装ではなく、**その萌芽が感じられるスライス**まで

合意済みの制約（壊さない）:

1. Full ティア = **Unity シーンを SceneDirector で Load**（Prefab 直ストリーミングへ逃げない）
2. **距離判断の単位は Cell**（WSC は Cell identity だけ見る）
3. **子は Cell に引っ張られない** = Cell `AddScene` で Environment 等が自動ロードされない（既定 `OnDemand`）。職種・システムが別途 Add
4. Cell Unload 時、**既に載っている子は親寿命で再帰破棄**（ダングリング防止）。ロード時の引っ張りとは別
5. 共有ヘビーアセットは **一つ上（World / Cell）が参照して PreLoad** する方向（FW R-1）
6. **フォルダ = 実行環境境界**: Scene は実行環境の境界なので、その実行に必要なものは **World フォルダ配下を見れば分かる**ようにする（今のバラバラ配置は是正対象）

---

## 1.5 フォルダ配置方針（新規合意・次セッション必須）

### 原則

- **Scene identity の実行単位**と**ディスク上のフォルダ**を揃える
- 「この World / この Cell を動かすのに何が要るか」を、Explorer でそのフォルダを開けば把握できる
- SceneResource / Material / 白箱コンテンツ / 子シーンを、SceneMap や Common のあちこちに散らかさない

### 現状の散らばり（是正前）

| 物 | いまの場所 |
|---|---|
| `World.unity` + コード | `SampleGame/.../InGameSession/World/` |
| `World.asset` (SceneResource) | `OneStarMakerCommon/SceneMap/World.asset` |
| Cell `.unity` | `OneStarMakerCommon/World/Cells/`（フラット 100 枚） |
| Cell SceneResource | `OneStarMakerCommon/SceneMap/Cells/` |
| 共有 Lit | `OneStarMakerCommon/World/Materials/` |

### 目標レイアウト（既定）

ルートは SampleGame 側に寄せる（ゲーム実行コンテンツ = ゲーム asm 近く）:

```
SampleGame/InGame/InGameSession/World/
  World.unity
  WorldScene.cs
  WorldMaterialBindings.cs
  Materials/
    DemoCellLit.mat                 ← World 共有（PreLoad 対象）
  WorldGridDefinition.asset         ← 任意。生成入力もここ
  Cells/
    Cell_0_0/
      Cell_0_0.unity
      Cell_0_0.asset                ← SceneResource も同居（推奨）
      Environment_0_0.unity         ← 萌芽
      Environment_0_0.asset
    Cell_0_1/
      ...
```

補足:

- `SceneResourceMap` 本体は従来どおり `OneStarMakerCommon/SceneMap/` に残してよい（カタログ）。**中身の SceneResource アセットは World 配下を指す / そこに置く**
- Addressables address はパス追従で更新（生成メニューが責務）
- Generator / `WorldCellStreamingSliceCreator` の出力先を上記に変更する（CCS-00）
- 旧 `OneStarMakerCommon/World/Cells` と `SceneMap/Cells` は移行後に空にするか削除

「触ってよい」は実質 **この World フォルダ配下 + Session 配線（Streaming）+ Factory/Editor**。

---

## 2. 目標ツリー（萌芽スライス）

```
World
  └── Cell_{x}_{y}                    ← WSC が Add/Unload（ストリーミング境界）
        ├── Environment_{x}_{y}       ← OnDemand・白箱 or 床の一部を移設
        └── (任意で) Gimmick_{x}_{y}  ← 1 Cell だけでも「芽」が出ればよい
```

命名は仮。確定時の候補:

| 案 | 例 | メモ |
|---|---|---|
| A | `Env_3_2` / `Npc_3_2` | 短い。職種プレフィックス |
| B | `Cell_3_2_Environment` | Cell との対応が読みやすい |
| C | `Environment_3_2` | 人間向け。推奨寄り |

**萌芽の定義（受入）:**

- SceneResource 上で「Cell の子が 1 種類以上見える」
- Play で Cell が載ったあと、**明示 Add**（またはデモ用の薄いコーディネータ）で Environment が載る
- Cell だけ Add した瞬間に Environment は載っていない（引っ張られないことの実証）
- Cell Unload で載っていた Environment も消える
- HUD かログで「Cell / 子シーン」が区別できる

非スコープ（次々回以降）:

- 全 100 Cell × 全職種の量産
- 職種別ロード半径・別 WSC
- Variant / 部分 Checkout 運用の完成
- 本格コンテンツ制作パイプライン

---

## 3. 推奨アプローチ（次セッションの既定）

### 3.1 スライス範囲

- **1〜4 Cell だけ**に Environment 子を付ける（例: `Cell_0_0`〜`Cell_0_1`）
- 残り Cell は葉のまま（既存 DemoCellRoot でも可）
- 職種はまず **Environment 1 種**。NPC/VFX は identity / 空シーンのスタブでもよいが、必須ではない

### 3.2 ロード責務

| 誰 | 何をする |
|---|---|
| WSC | `Cell_*` の desired set のみ |
| SampleGame 薄い層（仮称 `CellChildLoadPolicy`） | Cell が Stable になったら、その Cell の Environment を `AddScene`（デモ用）。Cell が Unload されるときは親再帰に任せるか、先に子を Unload |
| CellScene / DemoCellScene | **判断しない**。子を NecessaryAlways にしない |

実装の置き場: `InGameSession/Streaming/`（ゲーム配線）。FW の WSC は触らない。

### 3.3 オーサリング

- **先に CCS-00（World フォルダ集約）**してから萌芽を足す。散らばったまま子を増やすと悪化する
- Editor メニュー拡張（既存 `WorldCellStreamingSliceCreator` の出力先を §1.5 に合わせ、「萌芽用子シーン」ステップを追加）
- 子 `.unity` は `Cells/Cell_x_y/` 配下に事前配置（ランタイム `CreatePrimitive` 禁止は維持）
- 見た目の一部を Cell 本体から Environment へ移すと「作業単位の分割」が体感しやすい  
  （例: Ground を Environment 側へ。Marker は Cell に残す＝ストリーミング境界の目印）

### 3.4 Factory

- `GameSceneFactory`: 子 identity はプレフィックス分岐（`Environment_` 等）
- 子の SceneBase は **`CellScene` 継承不要**（親が Cell であればよい）。薄い `SceneBase` 派生で十分

### 3.5 共有 Material

- 現状: World が `DemoCellLit` を PreLoad、Cell が MPB
- Environment も同じ共有 Lit を参照すればよい（World PreLoad のまま）

---

## 4. 実装チケット案（次セッション用）

| ID | 内容 | 受入 |
|---|---|---|
| **CCS-00** | **World フォルダ集約**: Cell/Material/SceneResource を §1.5 配下へ移動。Generator 出力先変更。旧パス掃除 | World フォルダを見れば実行物が揃う |
| CCS-01 | 命名規則と SceneResource 親子（Cell → Environment）を 1 Cell で手配線 or メニュー生成 | Map / 親子が正しい。ファイルは `Cells/Cell_x_y/` 配下 |
| CCS-02 | Environment 用空〜白箱シーン + Factory + Addressables | Play で明示 Add できる |
| CCS-03 | Cell Stable → Environment Add の薄いデモ配線（引っ張られないことのテスト付き） | Cell only / Cell+Env の差が観測できる |
| CCS-04 | HUD: CurrentCell + ResidentCells + （任意）LoadedChildren | 萌芽が画面で分かる |
| CCS-05 | 正典 21 or Sample 側メモに「Cell 作業単位 + 子シーン + フォルダ境界」を追記 | 次人が迷わない |

順序: **CCS-00 → CCS-01 → CCS-02 → CCS-03 → CCS-04**（CCS-05 は並行可）

---

## 5. 触ってよい場所 / 触らなくてよい場所

**触ってよい（実行コンテンツの本拠）**

- **`unity/Assets/SampleGame/InGame/InGameSession/World/**`** … World / Cell / 子シーン / Materials / 関連 SceneResource（§1.5）
- `unity/Assets/SampleGame/InGame/InGameSession/Streaming/**` … WSC 配線・Focus・薄い子ロードデモ
- Editor: `WorldCellStreamingSliceCreator`（出力先を World 配下に固定）
- `GameSceneFactory` の World/Cell/Environment 分岐
- `SceneResourceMap.asset` の参照更新のみ（カタログ。実体は World 配下）

**原則触らない**

- `WorldStreamingController` のポリシー本体（Cell 以外を距離判定に混ぜない）
- `CellScene` FW 基底にゲームロジックを足さない
- 新規アセットを `OneStarMakerCommon/SceneMap/Cells` やフラット `Common/World/Cells` へ増やさない（移行元の掃除のみ）

---

## 6. 次セッション開始プロンプト（コピー用）

```
docs/planning/CELL_CHILD_SCENES_SPROUT_HANDOFF_2026-07-26.md に従い実装して。
まず CCS-00: Cell/World 実行物を InGameSession/World/ 配下に集約（フォルダ=実行環境境界）。
その後 Environment 子シーン萌芽（CCS-01〜04）。
距離境界は Cell、子は OnDemand（引っ張られない）。ランタイム CreatePrimitive 禁止。
```

---

## 7. 更新履歴

| 日付 | 内容 |
|---|---|
| 2026-07-26 | 初版。T-07 完了後の「人間作業単位としての Cell + 子シーン萌芽」HANDOFF |
| 2026-07-26 | §1.5 追加: フォルダ=実行環境境界。CCS-00 を先頭チケットに。触ってよい場所を World 配下へ限定 |
| 2026-07-26 | 実装着手: CCS-00〜05（World 集約 / Environment 萌芽 / 明示 Add / HUD / 正典追記） |
