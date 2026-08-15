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
│   └── OutGame/
│
└── OneStarMaker/                  ← 汎用 FW（下の層・Game を知らない）
    ├── Scripts/
    │   ├── Foundation/            ← leaf（FW 内で誰にも依存しない）
    │   ├── Runtime/               ← → Foundation のみ
    │   ├── Debug/                 ← → Foundation + Runtime（重い依存隔離）
    │   └── Editor/                ← エディタ専用
    └── Tests/
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

- OneStarMaker → Game
- 同階層横断（例: `InGame` → `OutGame`）。共有は `Common` へ
- Assembly 循環依存

詳細な asmdef ルールは [ARCHITECTURE.md §2](../../ARCHITECTURE.md#2-レイヤー構造と-assembly-依存ルール) を正とする。

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
| `Cell_0_0` だけが使う地面テクスチャ | `InGame/.../Cells/Cell_0_0/` |
| 全 Cell が使う地面マテリアル | `InGame/.../World/Materials/`（親 World） |
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
        └── World/
            ├── Materials/          ← 複数 Cell が共有 → 親(World)に置く
            └── Cells/
                ├── Cell_0_0/       ← この子だけが要るもの
                │     *.unity, Texture, Mesh…
                └── Cell_1_0/
```

### 3.4 SceneGraph との対応

```text
  Scene 親子（論理）              フォルダ（物理）
  ─────────────────              ────────────────
  InGame                         InGame/
    └─ InGameSession               └─ InGameSession/
         ├─ Player                      ├─ PlayerScene/
         ├─ World                       ├─ World/
         │    ├─ Cell_0_0               │    └─ Cells/Cell_0_0/
         │    └─ Cell_1_0               │         …
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

## 6. 更新履歴

| 日付 | 内容 |
|---|---|
| 2026-07-27 | 初版。Assembly 軸と Scene 同居軸を文書化 |
