# 26. UpdateSystem × Async/Await — 時間権威の設計方針

> ステータス: 設計方向メモ・実装前 (2026-07-19)
> 正本: 実装仕様は `docs/updater/UPDATER_CURRENT_SPEC.md` を参照。
> 関連: [16-update-architecture.md](16-update-architecture.md), [carbon-engine/02](../../../docs/reference/carbon-engine/02-scheduler-vs-update-system.md), [et-framework/01](../../../docs/reference/et-framework/01-distributed-lifecycle-vs-scene-update.md)

---

## 目次

1. [問い](#1-問い)
2. [背景 — 何が問題に見えていたか](#2-背景--何が問題に見えていたか)
3. [問題の言い換え — 二つのスケジューラではなく二つの時計](#3-問題の言い換え--二つのスケジューラではなく二つの時計)
4. [設計方針 — 時計を一本化する](#4-設計方針--時計を一本化する)
5. [API スケッチ](#5-api-スケッチ)
6. [帰結 — 溶ける論点](#6-帰結--溶ける論点)
7. [残る規約 — Analyzer で縛れる一行](#7-残る規約--analyzer-で縛れる一行)
8. [採用しないもの](#8-採用しないもの)
9. [他フレームワークとの対照](#9-他フレームワークとの対照)
10. [実装に向けた論点](#10-実装に向けた論点)

---

## 1. 問い

このドキュメントが答える問いは次の一つ。

> **`await` した継続は、誰の時計で再開されるのか。**

「UpdateSystem と UniTask をどう共存させるか」「async 派と ECS 派のどちらに寄せるか」という枠で議論すると、答えはプロジェクト依存の運用規約にしかならない。問いを時計に還元すると、大部分が機械的に決まる。

---

## 2. 背景 — 何が問題に見えていたか

UpdateSystem は ECS フレンドリーに設計されている（Layer 順序・5 フェーズ固定・構造変更のフレーム終端遅延・dense データ更新）。一方で「歩く → 話す → 待つ」のような **フレームをまたぐ時系列** を async/await で書きたい要望は、この箱の中では満たせない。

そこで UniTask を併用すると、次の不安が出る。

- 各所で `while (true) { Simulate(); await UniTask.Yield(); }` が書かれ、**分散した `Update()` が実質復活**する
- 実行順・pause・timeScale・寿命・観測が UpdateSystem の外へ逃げる
- 「UpdateSystem の Context を参照したい」「Layer の Pause に従いたい」という要望に、きれいな答えがない
- 更新の権威が二重化し、後から読む人が「UpdateSystem を見れば全体が分かる」と思えなくなる

これらを「実行モデルの衝突」と捉えると、運用規約とレビューで守るしかない、という結論になる。

---

## 3. 問題の言い換え — 二つのスケジューラではなく二つの時計

UniTask は実は **スケジューラを持っていない**。`UniTask.Yield()` は Unity PlayerLoop に、`UniTask.Delay()` は Unity の実時間に寄生して継続を再開しているだけである。

つまりカオスの正体は「二つの実行モデル」ではなく **「二つの時計」**。

| 時計 | 進み方 | pause / timeScale |
|---|---|---|
| UpdateSystem Layer 時間 | 5 フェーズ・Layer 順・scaled delta | Layer 単位で効く |
| Unity 素の時間（PlayerLoop / `Time.time`） | エンジン任せ | 効かない |

奔放な UniTask が UpdateSystem を無意味にするのは、async コードが **UpdateSystem の外の時計で勝手に進む** からであって、async 構文そのものが悪いのではない。

---

## 4. 設計方針 — 時計を一本化する

UniTask は再開ソースを差し替えられる設計なので、**UpdateSystem の tick を awaitable な唯一の時間源として公開する**。

```text
UpdateSystem = 時間と再開順の唯一権威（update の箱 → 時計へ昇格）
UniTask      = その時計を待つための構文（第二のスケジューラではなくなる）
```

- 継続の再開点は `RunUpdate` 内の固定位置（例: managed element 実行の前）に置く
- 再開キューは登録順 FIFO で消化し、5 フェーズの決定論の中に収める
- Layer が pause なら tick が発火しないので、継続は自然に再開されない
- `Delay` は Layer の scaled delta の累積で測る。timeScale も pause も自動で効く

C# コンパイラがステートマシンを生成し、UniTask が再開ソースの差し替えを担うため、Carbon tasklet の「単一権威が再開順を決める」性質を **協調ランタイムを自作せずに** 得られる。

---

## 5. API スケッチ

最終 API 名ではない。形の確認用。

```csharp
// Layer の時計への参照。Layer 単位で取得する
ILayerClock clock = updateSystem.GetClock("Simulation");

// 次の有効 tick まで待つ。pause 中は tick が来ないので止まる
await clock.NextTick(ct);

// scaled delta の累積で 2 秒。timeScale / pause が自動で効く
await clock.Delay(2.0f, ct);

// 時系列シーケンスの記述例
async UniTask PlayIntroAsync(CancellationToken ct)
{
    await character.WalkToAsync(stage.Center, clock, ct);
    await dialogue.ShowAsync("Hello", ct);
    await clock.Delay(0.5f, ct);
    await dialogue.ShowAsync("Welcome", ct);
}
```

原則:

- **async は必ず owner の `CancellationToken` を取る**（Scene / owner の寿命に紐付ける）
- Context（`UpdateFrameContext`）を async に持ち歩かない。必要な値（DeltaTime 等）は **tick 再開時に受け取る**

---

## 6. 帰結 — 溶ける論点

時計を一本化すると、これまでの論点がほぼ消える。

| 論点 | 帰結 |
|---|---|
| **Layer の Pause に従いたい** | Layer が止まれば tick が来ない。継続は再開されない。何も渡さなくてよい |
| **Context を参照したい** | 継続は tick の中で再開されるため、「今の DeltaTime」を再開時に受け取れる。持ち歩き不要 |
| **`while + Yield` の隠れ更新ループ** | `while + await clock.NextTick()` は **UpdateSystem に順序管理された正規の update element と等価** になる。禁止対象から「書き方の一つ」へ降格する |
| **順序の暗黙化** | 再開点が `RunUpdate` 内の固定位置・FIFO なので、決定論の内側に収まる |
| **時間系の混在**（`Time.deltaTime` vs `Delay`） | `clock.Delay` が scaled delta 基準なので混ざらない |

---

## 7. 残る規約 — Analyzer で縛れる一行

一本化しても、素の `UniTask.Delay()` / `UniTask.Yield()`（Unity 時計）は書けてしまう。残る規約は次の一行だけ。

> **gameplay の asmdef では素の `UniTask.Delay` / `UniTask.Yield` を使わず、layer clock を await する。**

「隠れ更新ループを書くな」のような曖昧な心得と違い、**API 名で機械的に判定できる** ため Roslyn Analyzer 一本で強制できる（UnityStarter が `async void` を Error にしているのと同じ手筋。[unity-starter/01](../../../docs/reference/unity-starter/01-foundation-vs-onestarmaker.md) 参照）。

適用範囲の目安:

- **対象**: gameplay / simulation 系 asmdef
- **対象外**: ロード・通信・エディタ・観測など、実時間で待つのが正しい層

---

## 8. 採用しないもの

| 案 | 理由 |
|---|---|
| **UpdateSystem 内部への async 直載せ**（`IUpdateElement` が UniTask を返す等） | 再開点がフレーム境界に散り、構造変更遅延・決定論・Job 化の利点が全部逆方向に働く |
| **第三のシーケンス FW の自作** | UniTask + layer clock で足りる。参照 FW（Carbon / ET）も「一本化」か「分離」の二択で、中途半端な第三スケジューラは避けている |
| **`UpdateFrameContext` を async に渡す API** | Context は 1 tick のスナップショットであり、await をまたぐと古くなる。要望の実体は「pause と scaled time の共有」なので clock で受ける |
| **UniTask の全面禁止** | ロード・演出待ち・UI 遷移・通信は async が自然。禁止ではなく時計の指定で縛る |

---

## 9. 他フレームワークとの対照

| FW | 更新の権威 | 待ち・時系列 | 本方針との関係 |
|---|---|---|---|
| **Carbon** | scheduler（tasklet 再開の唯一権威） | I/O も tasklet yield で同じ scheduler に載せる | 「単一権威」を C# 標準機能で再現する。Greenlet 写経はしない（[carbon 02](../../../docs/reference/carbon-engine/02-scheduler-vs-update-system.md) §5.3 と整合） |
| **ET** | Fiber / Actor | メッセージ駆動に寄せる | 別種の一本化。Fiber runtime は借りない判断済み |
| **UnityStarter** | Gameplay 側 Tick（Update 集約 FW は薄い） | UniTask + Analyzer（`async void` 禁止等） | Analyzer で機械的に縛る手筋を借りる |

Reference 勢の結論は「一本化する（重い）」か「分離して規約で守る（軽い）」の二択だった。本方針は **分離のまま、規約の大半を時計の設計に吸収させる** 第三の落とし所。

---

## 10. 実装に向けた論点

実装時に決めるべきこと。

1. **再開フェーズの位置** — `RunUpdate` 内の element 実行前か後か、専用フェーズか。5 フェーズ固定との整合を doc 化する
2. **clock の取得経路** — Layer ID 指定か、`UpdateSystemRuntime` 経由か。DI との関係
3. **`Delay` の精度契約** — tick 粒度で丸まる（次の tick で判定）ことを明記する
4. **キャンセルと構造変更の関係** — await 中に owner が unregister された場合、継続は CT でキャンセルされる。`ApplyStructuralChanges` との順序を固定する
5. **Analyzer ルール** — 対象 asmdef の指定方法、`UniTask.Delay/Yield` 検出の diagnostic ID とメッセージ
6. **観測** — 再開待ち継続数を Layer ごとに Telemetry へ出すか（隠れ負債の可視化）

---

## 更新履歴

| 日付 | 内容 |
|---|---|
| 2026-07-19 | 初版。「時間権威」への問いの還元と設計方針を記録 |
