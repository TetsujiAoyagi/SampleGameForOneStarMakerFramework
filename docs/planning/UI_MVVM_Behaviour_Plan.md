# Game UI Behavior Pipeline - Design Proposal v0.2

## 改訂履歴

| 版 | 日付 | 内容 |
|---|---|---|
| v0.1 | — | 初版（設計思想） |
| v0.2 | 2026-07-06 | 技術マッピング・割り込みポリシー確定・収束不変条件・副作用 Behavior・ユースケース骨組みを追加。コード API を正とする方針へ弱体化。UXML DSL を将来章へ移動 |

---

## 概要

本ドキュメントは、Unity（UI Toolkit）向けゲーム UI フレームワークの設計思想をまとめたものである。

目的は、

- MVVM の宣言性
- UI Toolkit（UXML）との親和性
- ゲーム特有の UI 演出
- 高い再利用性

を両立することである。

本設計では、**状態（State）** と **演出（Behavior）** を完全に分離することを目指す。

---

## 背景

ゲーム UI には一般的な業務 UI とは異なる特徴がある。

例えば HP のカウントダウン、ダメージ時の揺れ、ボタン演出、ポップアップ、エフェクト、サウンド、画面遷移など、「状態」だけでは表現できない時間を伴う演出が存在する。

一般的には Presenter へ命令を書く、Coroutine を書く、DOTween を書くという形になりやすい。しかしこれらは命令列になり、再利用しづらく、契約がコードへ散らばるという問題がある。

---

## 設計思想

### 状態と時間を分離する

この設計で最も重要な考え方は、

> **状態（State）と時間（Transition）は別物である**

ということである。

例えば `Closed` → `Opening` → `Opened` → `Closing` のような状態を持ちたくなる。しかし `Opening` / `Closing` は状態ではなく、**時間を伴う変化**である。つまり `Closed` → `Opened` という変化そのものを表現している。

本設計では `Closed` / `Opened` のみを **Stable State** として持つ。一方、`Closed` → `Opened` という変化は **Behavior** が担当する。

---

## Stable State

Stable State とは、ある瞬間に真である事実を表す。

例: `IsOpen`, `Selected`, `HP`, `Visible`, `Enabled` など。

Stable State には時間・アニメーション・演出は含まれない。ゲームロジックは Stable State だけを見て判断できる。

```csharp
if (Inventory.IsOpen)
{
    ...
}
```

だけで十分である。

---

## Transition

Transition とは、Stable State 同士の変化である。

例:

```
Closed → Opened
100 HP → 80 HP
```

Transition は時間を持つ。`100 → 99 → 98 → … → 80` という補間が存在するが、これは状態ではない。Behavior が担当する。

---

## Behavior

Behavior は Transition を表現する。

例: `TweenNumber`, `Shake`, `Flash`, `Popup`, `PlaySE`, `DisableButton`, `Particle` など。

Behavior は自由に合成できる。

```
HPChanged → TweenNumber → Flash → Shake
```

---

## Behavior Pipeline

Behavior は Pipeline として実行される。

```
UIContext → Behavior → UIContext → Behavior → UIContext
```

各 Behavior は `Context → 副作用 → Context` を行う。Pipeline Pattern や Middleware に近い設計である。

---

## Behavior Composition

Behavior は再利用可能な部品である。

```
HitEffect = Tween + Flash + Shake + PlaySE

CriticalEffect = HitEffect + CameraShake + SlowMotion
```

のように合成できる。

---

## Transition Runner

Behavior は Transition Runner が実行する。

```
Stable State
    ↓
Transition Resolver
    ↓
Behavior Runner
    ↓
Visual State
```

### Stable State

ViewModel が保持する。例: `IsOpen = true`

### Transition Resolver

状態変化を検知する。例: `false → true` を `OpenBehavior` へ変換する。

### Behavior Runner

Behavior を実行する。時間管理・割り込み・Visual State 保持を担当する。例: `Fade → Scale → PlaySE`

Runner は **`IsTransitioning`（読み取り専用 Observable）** を公開する。Stable State の権威は ViewModel、遷移中フラグの権威は Runner である。ViewModel は遷移中かどうかを知る必要がない。

### Visual State

現在画面へ表示する値。例: `Opacity`, `Scale`, `Rotation`, `Current HP Display`, `Animation Progress`

Visual State は Runner のみが保持する。**Model は一切知らない。**

---

## Visual State

重要なのは、Visual State は Model ではない。

例えば Current Display HP が `93` であっても、Model は `HP = 80` だけ知っていればよい。途中の `99, 98, 97, …` は Runner だけが持つ。

---

## 技術マッピング

本プロジェクトでの具体実装との対応関係を以下に示す。

| 概念 | 実装 |
|---|---|
| Stable State | R3 `ReactiveProperty`（ViewModel が保持） |
| Transition Resolver | R3 `Pairwise` ヘルパ（`TransitionBinder`） |
| Behavior Runner | 独自実装 + LitMotion（割り込みポリシー・`IsTransitioning`・Visual State 保持） |
| View | UI Toolkit（UXML / `VisualElement`） |
| Behavior 定義（直列化） | `BehaviorAsset`（ScriptableObject） |

Unity 6 組込 Data Binding は使わない。R3 ベースの独自バインディングを採用する。

---

## MVVM

本設計では MVVM を採用する。

- View が決まれば契約が決まる
- Binding が宣言的
- UXML との親和性

Presenter を書くことを目的としない。

---

## 契約

契約には三種類ある。

| 種類 | 内容 | 担当 |
|---|---|---|
| Layout Contract | 何を表示するか | UXML |
| Data Contract | 何を Binding するか | Binding（コード API） |
| Behavior Contract | どう動くか | Behavior（コード API。SO は直列化表現） |

---

## Behavior Asset

Behavior はデータとして管理する。

```
HitEffect.asset
  ・TweenNumber
  ・Flash
  ・Shake
  ・PlaySE
```

Behavior は再利用・差し替え・エディタ編集を可能にする。ScriptableObject はコード API で組んだ Pipeline の直列化表現である。

---

## Presenter との違い

一般的な Presenter:

```csharp
view.Shake();
await ...
view.SetText();
PlaySE();
```

Behavior Pipeline:

```
HPChanged → Tween → Flash → PlaySE
```

命令を書くのではなく、Behavior を組み合わせる。

---

## Model 更新について

原則として Behavior は Model を書き換えない。

- UI とゲームロジックの分離
- 再利用性
- テスト容易性

Behavior が必要なら `Completed`, `AnimationFinished`, `Command` などのイベントを通知する。ゲームロジックはそれを購読して Model を更新する。

---

## 割り込み

Transition は割り込みを考慮する。

```
Closed → Opening → (途中) → Closing
```

Runner は以下の **3 つの割り込みポリシー** のみを提供する。Model は関知しない。

| ポリシー | 動作 |
|---|---|
| **Restart** | 実行中をキャンセル → **最終値へスナップ** → 新規実行を最初から開始 |
| **FromCurrent** | 実行中をキャンセル（スナップしない）→ Visual State に残る現在値を OldValue として差し替え → 新規実行 |
| **Rewind** | 実行中をキャンセル → `IRewindableBehavior` を逆再生。非対応 Behavior は Restart にフォールバック |

ブレンド・即時終了などその他のポリシーは将来課題とする。

---

## 副作用系 Behavior（PlaySE / Particle 等）

UI Toolkit の `VisualElement` だけでは表現できない演出（パーティクル、グロー、カメラシェイク、サウンド等）は、**外部演出システム** を Behavior から叩く逃げ道とする。

- サービス参照は `UIBehaviorContext` へ **手動 DI** で注入する（`IServiceResolver` 等。DI コンテナは使わない）
- Behavior 実装は注入されたサービスを呼ぶだけとし、Model は変更しない
- 今回の Vertical Slice スコープでは未実装。契約のみ確定する

---

## 全体構成

```
                  Game Logic
                      │
                      ▼
                 Domain Model
                      │
                      ▼
                  ViewModel
                (Stable State)
                      │
                 Data Binding
                      │
                      ▼
               UXML / View Tree
                      │
     ┌────────────────┴──────────────┐
     │  State Change Detection        │
     │ (Transition Resolver)          │
     └────────────────┬──────────────┘
                      │
                      ▼
              Behavior Pipeline
                      │
      ┌───────────────┼────────────────┐
      │               │                │
      ▼               ▼                ▼
   Tween         PlaySE           Particle
      │               │                │
      └───────────────┼────────────────┘
                      ▼
               Behavior Runner
                      │
                      ▼
                 Visual State
                      │
                      ▼
               UI Toolkit Render
```

---

## ユースケース

本設計を実装に落とす際の代表例。Vertical Slice（T-13/T-14）の実コードを引用する。

### HP ゲージ更新

- **Stable State**: ViewModel の `ReactiveProperty<int> Hp`
- **Data Binding**: `BindText` で初期表示（即時反映）
- **Transition**: HP 変化時に `TransitionBinder` が `Pairwise` で `(old, new)` を検出
- **Behavior**: `Parallel(TweenNumber, Flash, Shake)`。割り込みポリシーは **FromCurrent**（連打時に表示 HP が滑らかに追従）
- **収束**: 静止後の表示値が ViewModel の `Hp` と一致すること（収束不変条件）

実コード（`HpGaugeView.OnRootCreated` より抜粋）:

```csharp
var runner = new BehaviorRunner(hpLabel, InterruptPolicy.FromCurrent);
_disposables.Add(runner);

var hpTransition = new ParallelBehavior(
    new TweenNumberBehavior(),
    new FlashBehavior(Color.red, 0.2f),
    new ShakeBehavior(6f, 0.3f, 10));

_disposables.Add(_viewModel.Hp.BindTransition(runner, hpTransition));
```

### 確認ダイアログの表示・非表示

- **Stable State**: シーンの有無（AddScene / RemoveScene）が真実。ViewModel はダイアログ内容を保持
- **ViewIn / ViewOut**: `Parallel(Fade, Scale)` を Behavior Runner 経由で実行
- **割り込み**: Opening 途中の Close 要求に **Rewind** が働き、画面固有の遷移中フラグなしで逆再生
- **入力**: Modal/Dialog レイヤーの Blocker で背面 UI を遮断

実コード（`ConfirmDialogView` より抜粋）:

```csharp
public override async UniTask ViewIn(CancellationToken ct)
{
    EnsureRootCreated();
    await _runner!.Run(
        new ParallelBehavior(
            new FadeBehavior(0f, 1f, 0.25f),
            new ScaleBehavior(0.8f, 1f, 0.25f)),
        new TransitionPayload(false, true),
        ct);
}

public override async UniTask ViewOut()
{
    // startFromCurrent: Opening 途中の Rewind 直後でも resolvedStyle の現在値から開始し、
    // opacity 0 → 1 等の視覚ジャンプを防ぐ。
    await _runner.Run(
        new ParallelBehavior(
            new FadeBehavior(1f, 0f, 0.25f, startFromCurrent: true),
            new ScaleBehavior(1f, 0.8f, 0.25f, startFromCurrent: true)),
        new TransitionPayload(true, false),
        CancellationToken.None);
}
```

### 撤退ライン判定（T-17、2026-07-06）

**結果: 合格（Behavior 層を維持する）**

| 判定項目 | 結果 | 根拠 |
|---|---|---|
| 1. 記述量テスト | 合格 | HP ゲージの「Tween+Flash+Shake、連打で FromCurrent 追従」は View 側 8 行（上記抜粋）で構成のみを宣言。LitMotion 直書きで同等機能（実行中モーションのキャンセル管理・表示中 HP の追跡・Flash 元色の復元・Shake の原点復帰・多重割り込みの収束保証）を実装すると、画面固有の状態管理コードが View に数十行漏れ出す。可読性・再利用性で明確に優位 |
| 2. 割り込みテスト | 合格 | ダイアログの「Opening 途中で Close」は Runner の `InterruptPolicy.Rewind` 指定のみで成立。`ConfirmDialogView` / `ConfirmDialogScene` に遷移中フラグの if 文は存在しない（コードレビューで確認）。視覚連続性は `Rewind` の逆再生 + `ViewOut` の `startFromCurrent` で担保 |

補足所見:

- 収束不変条件（割り込み連鎖・外部キャンセル・Dispose 後に表示値が最終 Stable State と一致）は EditMode テスト 154 件（うち Behavior パイプライン系 32 件）で自動検証済み。
- `Rewind` → `ViewOut` の視覚連続性は `startFromCurrent` フラグを Fade/Scale に追加して解決した。「逆再生の起点は現在の style 値を読む」という T-12 の注意点は、割り込み後の後続遷移にも適用すべき一般則であることが分かった。
- ジェネリック `TransitionPayload<T>` 化と GC 最適化（`style.translate` の boxing 等）は現時点で不要と判定。発火は状態変化時のみで hot path ではなく、スライスの計測でも問題が観測されていない。次スライスでリスト増減など高頻度発火のユースケースを扱う際に再評価する。

### 次のスライス候補

タブ切り替え、トースト通知、ローディング画面、リスト追加・削除、インベントリ更新など。ユースケースを通して DSL や Behavior の抽象化を見直し、「机上の理論」ではなく「実際にゲーム開発で使える設計」へ磨く。具体例は 10〜20 件へ拡充する。

---

## 設計原則（Design Principles）

1. **遷移が走っていないとき、Visual State は常に Stable State から一意に導出される値と一致する。**
2. **Stable State のみを状態として保持する。**
3. **時間を伴う変化は Behavior が担当する。**
4. **コード API を正とし、宣言（ScriptableObject / UXML）はその直列化表現とする。**
5. **Behavior は小さく、合成可能で再利用可能な部品とする。**
6. **Behavior は原則として Model を書き換えない。**
7. **ゲームロジックは Stable State だけを参照して成立することを目指す。**
8. **UI 仕様（レイアウト・Binding・Behavior）のデータ化は原則 4 に従属する。データ（ScriptableObject / UXML）はコード API の直列化表現として管理する。**

---

## 将来の拡張

- **UXML Behavior DSL**: `<On event="HPChanged">` 等の UXML 内 Behavior 記述（v0.1 で示した理想的な UXML は将来の拡張）
- **割り込みポリシーの追加**: ブレンド、即時終了など
- **Behavior DSL の設計**、UI Builder 連携、独自エディタ
- **並列・分岐・キャンセルの宣言的記述**
- **Transition Resolver** の責務拡張
- **Visual State** の保持方式の最適化
- **ジェネリック `TransitionPayload<T>`** 化、GC 最適化

### 理想的な UXML（将来）

```xml
<Label binding-path="HP">
    <On event="HPChanged">
        <TweenNumber duration="0.3"/>
        <Flash color="Red"/>
        <Shake amplitude="6"/>
    </On>
</Label>
```

あるいは `behavior="HitEffect"` のような参照形式。v0.2 ではコード API + BehaviorAsset を正とし、上記 DSL は将来章に留める。

---

## 関連ドキュメント

- 施行表: [`UI_BEHAVIOR_PIPELINE_WORKPLAN_2026-07-06.md`](UI_BEHAVIOR_PIPELINE_WORKPLAN_2026-07-06.md)
- UI アーキテクチャ正本: [`unity/Assets/Docs/Architecture/06-ui.md`](../../unity/Assets/Docs/Architecture/06-ui.md)
