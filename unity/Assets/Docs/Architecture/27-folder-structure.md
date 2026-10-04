# 27. フォルダ構成戦略（Assembly × Scene 同居）

> ステータス: 運用方針（2026-07-27 文書化）
> [ARCHITECTURE.md](../../ARCHITECTURE.md) に戻る
> 関連: [§2 Assembly 依存](../../ARCHITECTURE.md#2-レイヤー構造と-assembly-依存ルール), [05-scene.md](05-scene.md), [20-variant-checkout-workflow.md](20-variant-checkout-workflow.md), [README プロジェクト構造](../../README.md#プロジェクト構造)

---

## 1. 二軸で切る

フォルダは見た目の整理ではなく、次の二軸を同時に表す。

| 軸 | 問うこと | 単位 |
|---|---|---|
| **A. Assembly** | 誰が参照してよいか（コンパイル依存） | `Foundation` / `Runtime` / `Debug` / `SampleGame.*` |
| **B. Scene 同居** | 誰の寿命・所有か（アセットの置き場） | Scene ツリーに対応するフォルダ |

```text
  軸A  Assembly（誰が参照してよいか）
       Foundation ← Runtime ← Game(In/Out/Common) ← DependOnAll

  軸B  Scene 同居（寿命・所有の場所）
       「その Scene ツリーに必要なアセットは、そのフォルダ配下に閉じる」
       共有は親へ上げる（上がりすぎたら Common）
```

Scripts だけの話ではない。`.unity` / Texture / Mesh / Material / Prefab / UXML なども **軸 B** に従う。

---

## 2. 軸 A — Scripts / Assembly

### 2.1 全体

```text
Assets/
│
├── SampleGame/                    ← ゲーム固有（上の層）
│   ├── DependOnAll/               ← 配線だけ集約（Composition Root）
│   ├── Common/                    ← In/Out 共通
│   ├── InGame/                    ← ←→ OutGame は参照禁止
│   ├── OutGame/
│   └── Tests/                     ← SampleGame.Tests / Tests.Editor。FW テストからは参照しない
│
└── OneStarMaker/                  ← 汎用 FW（下の層・Game を知らない）
    ├── Scripts/
    │   ├── Foundation/            ← leaf（FW 内で誰にも依存しない）
    │   ├── Runtime/               ← → Foundation のみ
    │   ├── Debug/                 ← → Foundation + Runtime（重い依存隔離）
    │   └── Editor/                ← エディタ専用
    │       └── TestObservation/   ← OneStarMaker.Editor.TestObservation（TestRunner にだけ依存）
    └── Tests/                     ← OneStarMaker.Tests / Tests.Editor。Game を参照しない
        └── Editor/TestObservation/ ← OneStarMaker.Tests.TestObservation.Editor（観測器と TestRunner に依存）
```

### 2.2 依存の向き（参照してよい方向 = 下向き）

```text
        DependOnAll
       /   |    |   \
      v    v    v    v
  InGame OutGame Debug  …
      \    |    /
       v   v   v
        Common
          │
          v
       Runtime ──► Foundation (leaf)
          ▲
          │
        Debug
```

禁止:

- OneStarMaker → Game（`OneStarMaker.Tests` と `OneStarMaker.Tests.Editor` も含む）
- 同階層横断（例: `InGame` → `OutGame`）。共有は `Common` へ
- Assembly 循環依存

アプリの EditMode テストは `SampleGame.Tests` と `SampleGame.Tests.Editor` に置く。フレームワークのテストヘルパーが要るときは `SampleGame.Tests` → `OneStarMaker.Tests` の向きだけを使う。

詳細な asmdef ルールは [ARCHITECTURE.md §2](../../ARCHITECTURE.md#2-レイヤー構造と-assembly-依存ルール) を正とする。

Unity 内観測は専用の Editor assembly とテスト assembly に閉じる。両方とも `UnityEditor.TestRunner` / `UnityEngine.TestRunner` を参照し、Framework Runtime と Game を参照しない。Unity Test Framework 1.8.0 以上を有効化条件とし、テスト側には `UNITY_INCLUDE_TESTS` も必要である。詳細な実行・復旧の境界は [Harness README](../../../../tools/Harness/README.md#unity-内観測editmode) に置く。

### 2.3 ScriptSystem（任意の数値命令 executor）

`Runtime/ScriptSystem/`は、呼び出し側が C# で組み立てた数値命令を予算つきで進める任意の内側 executor である。通常のゲームイベントでは直接の型付き C# をまず比較対象にする。現在の9命令はメモリ上の意味だけを定め、言語・Event host・保存バイトコードの ABI は提供しない。

`ScriptProgram` は入力命令の不変コピー、`ScriptRegisters` はゼロ初期化された `long[]` を持ち、どちらも呼び出し側が所有する。純粋な `ScriptMachine` は両者を借り、PC・状態・終端ラッチを所有する。呼び出し側は Tick とレジスタ変更を逐次化し、変更を Tick の間に行う。共有レジスタを使う機械同士の順序も呼び出し側が決める。

`ScriptUpdateElement` は Update で固定予算の Tick を1回呼ぶ adapter で、Start / LateUpdate では命令を進めない。呼び出し側が Layer・寿命・Scene 所有を決め、`UpdateSystemRuntime` 経由で登録・解除し、解除後に借用した参照を手放す。`UpdateCoordinator` の直接利用は独立した決定的テストの seam である。

非正の予算はラッチ後も `RejectedBudget` を返し、保存状態を変更しない。予算消費による `Yielded` は再開可能で、予算と同時に末尾へ着いた場合は次の正の Tick で自然終端を検出する。不正な使用レジスタ・実際に取る跳躍先・opcode は型付き故障にラッチし、故障命令の PC と書き込みを変更しない。それ以前の完了命令は保持し、後続 Update 要素の実行を妨げない。詳細な命令意味はソースの XML コメントと ScriptSystem テストに置く。

---

## 3. 軸 B — Scene ごとのフォルダ同居

### 3.1 方針

- **InGame なら InGame に、InGame に必要なものは全部入れる**（Scene, Texture, Mesh, Material, Prefab, UI など）
- **子供にだけ必要なら子供のシーンフォルダへ**
- **複数の子供に必要なら親のシーンフォルダへ**（最寄りの共通祖先 = LCA）
- InGame と OutGame の両方なら `SampleGame/Common/`

SceneGraph の親子と、ディスク上のフォルダ親子を揃える。

### 3.2 配置ルール

```text
  使う範囲が …              置く場所
  ─────────────────        ────────────────
  子 Scene 1つだけ     →   その子のフォルダ
  兄弟の複数子         →   親 Scene のフォルダ
  InGame 全体          →   InGame/（またはその配下の共通親）
  InGame と OutGame    →   Common/
```

例:

| アセット | 置き場 |
|---|---|
| `Spring_Cell_0_4` だけが使う地面テクスチャ | `InGame/.../Seasons/Spring/Cells/Spring_Cell_0_4/` |
| 全季節の Cell が使う地面マテリアル | `InGame/.../Seasons/Materials/`（Season 群の共通親） |
| Session 中の HUD だけ | `InGame/.../InGameUI/` |
| Title 専用 UXML | `OutGame/Title/` |

### 3.3 フォルダ例（SampleGame）

```text
SampleGame/
├── Common/                         ← 複数トップ領域で共有するものだけ
│
├── OutGame/                        ← OutGame に必要なものは全部ここ配下
│   ├── OutGame.unity / Scripts …
│   ├── Title/                      ← Title だけが要る Scene/Tex/Mesh…
│   ├── HpGauge/
│   └── ConfirmDialog/
│
└── InGame/                         ← InGame に必要なものは全部ここ配下
    ├── InGame.unity / Scripts …
    └── InGameSession/
        ├── PlayerScene/            ← Player 専用アセット
        ├── InGameUI/               ← その UI 専用
        ├── Result/
        └── Seasons/
            ├── Materials/          ← 複数 Season / Cell が共有
            └── Spring/
                ├── Spring_Lighting/
                └── Cells/
                    └── Spring_Cell_0_4/  ← この子だけが要るもの
                          *.unity, Variants/, companion Scene…
```

### 3.4 SceneGraph との対応

```text
  Scene 親子（論理）              フォルダ（物理）
  ─────────────────              ────────────────
  InGame                         InGame/
    └─ InGameSession               └─ InGameSession/
         ├─ Player                      ├─ PlayerScene/
         ├─ Season_Spring               ├─ Seasons/Spring/
         │    ├─ Spring_Lighting        │    ├─ Spring_Lighting/
         │    └─ Spring_Cell_0_4        │    └─ Cells/Spring_Cell_0_4/
         └─ InGameUI                    └─ InGameUI/
```

論理ツリー（SceneResource / Graph Editor）と物理ツリー（フォルダ）がずれると、所有と寿命の見通しが悪くなる。新規 Scene を足すときは **フォルダも同じ親子で切る**。

---

## 4. なぜこの二軸か

| 狙い | 効く軸 |
|---|---|
| コンパイル時に逆依存を防ぐ | A |
| 「この画面を消す／Checkout する」とき消える範囲が読める | B |
| Variant / 部分 Checkout と相性が良い | B（領域がフォルダに閉じる） |
| InGame 作業中に OutGame 資産を漁らない | A + B |

軸 B は [20. Variant チェックアウト](20-variant-checkout-workflow.md) の「領域単位で触る」運用の土台にもなる。

---

## 5. やってはいけないこと

- 共有だからといっていきなり `Assets/Shared` やルート直下へ逃がす（まず親 Scene フォルダへ上げる）
- 子専用アセットを親や Common に置きっぱなしにする（所有が曖昧になる）
- Scripts だけ軸 A に従い、Texture/Mesh を別ツリーの雑多フォルダへ置く
- `InGame` のスクリプトから `OutGame` の型を参照する（軸 A 違反）。アセット参照も同様に境界を跨がない

---

## 6. World Workspace（職種 companion の Editor 作成）

任意の Cell Lighting / VFX / Events を作る Editor 口は `SampleGame/DependOnAll/Editor/WorldAuthoring/` に置く。Framework Runtime は職種名も Workspace も知らない。Workspace は runtime asset owner を持たない。

```text
SampleGame/DependOnAll/Editor/WorldAuthoring/
  WorldWorkspaceWindow.cs              ← 選択 UI（EditorWindow instance 寿命。Prefs に保存しない）
  WorldWorkspaceSelection.cs           ← season / x / y / role / payload の値
  WorldWorkspaceSceneOpener.cs         ← 事前解決 + 保存確認のあと Single → Additive
  WorldCompanionCreationPlan.cs        ← 予定 path / identity（Unity I/O なし）
  WorldCompanionCreationTransaction.cs ← 原子 create + rollback
  WorldCompanionOwnershipProof.cs      ← GUID / fingerprint / shape / graph membership
  WorldCompanionRecoveryJournal.cs     ← Library 配下の pending journal
  WorldCompanionRecoveryService.cs     ← domain load 時の block と明示 Rollback
  WorldCompanionSceneCreator.cs        ← 空 .unity の作成
```

旧 bulk generator と `LegacyWorldAuthoringNames.cs` は S-4b の生成後に撤去済みであり、World Workspace の構成要素ではない。

作成する companion の Scene / SceneResource は親 Cell フォルダへ同居させる（軸 B）。

```text
Seasons/{Season}/Cells/{Season}_Cell_{x}_{y}/{identity}/{identity}.unity
Assets/SceneGraphData/Nodes/Cells/{identity}.asset
```

正本は `SceneNodeData` と `SceneGraphEdges`。`SceneResource` / Map は標準 `SceneResourceGenerator.Generate` の投影物であり、Workspace が生成物だけを手で upsert してはならない。payload は空 Variant、`LoadType.OnDemand`、`Volume = zero`、`StreamByDistance = false`。座標は Editor 入力にだけ使い、runtime が identity から復元しない。

Workspace は SceneGraph Editor を通らない。`SceneGraphViewModel` / `SceneGraphEditorWindow` / `SceneGraphLayout` は更新せず、Undo も積まない。開いている GraphView は古いままになり、Layout に位置が無いノードは再読込すると `(0,0)` に出る。Play が読むのは Map 側なので、作成の成否は Editor ウィンドウでは判定しない。Editor から同じ identity を作り直さない。

Cell / Environment / season Lighting / 大型 Event はこの口では作れない。既存の部分成果物を adopt / repair / overwrite しない。衝突があれば無変更で失敗する。

### pending journal が残ったとき

journal は `Library/OneStarMaker/WorldWorkspace/pending-companion-create.json`（git 管理外）。存在中は新しい Open / Create を拒否する。

1. World Workspace の **Rollback Pending Creation** を実行する。完了済み step は no-op、失敗した barrier より先の破壊はしない。
2. 公式 Rollback が同じ barrier で止まる、またはファイルが壊れて読めないときは **自動削除しない**。journal と識別元（path / GUID / fingerprint）を残して調査する。
3. 調査後に不要と分かった残骸だけを、人間が Editor と Addressables を確認してから取り除く。journal 破棄だけの command は無い。

Editor open の途中で Unity API が失敗したときの完全復元は対象外である（事前解決 + 保存確認が境界）。creation transaction の atomic/recovery と混同しない。

---

## 7. 更新履歴

| 日付 | 内容 |
|---|---|
| 2026-09-23 | World Workspace は SceneGraph Editor / Layout を通らない現況を追加 |
| 2026-09-09 | World Workspace の配置、Generate 投影、pending journal の調査手順を追加 |
| 2026-07-27 | 初版。Assembly 軸と Scene 同居軸を文書化 |
