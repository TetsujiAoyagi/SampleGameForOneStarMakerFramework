# 目標と強み — 境界・寿命・依存の一般化問題として

> 作成日: 2026-07-25  
> 置く場所: `docs/` 直下（`docs/reference/` ではない）。本ドキュメントは OSM 自身の目標宣言であり、外部参照シリーズの深掘りキューではない。  
> 読むタイミング: [reference/00-motivation-overview.md](reference/00-motivation-overview.md) の次。「なぜこの repo があるか」の後に「何を目標とし、何が強みか」を読む。  
> 経緯: 外部レビュー（「境界と依存関係の再考」）とリポジトリ実装の突き合わせから抽出した。

---

## 一文で言うと

> **OSM は「ゲームを簡単にするフレームワーク」ではなく、「複雑なゲームを載せても、ゲーム以外の部分まで一緒に複雑化しない」ための境界・寿命・依存の契約を、実装とドキュメントの両方で公開する試みである。**

ゲームルールの地獄は消せないし、消そうともしない。  
守るのは、その地獄が UI の寿命・Service の管理・Scene ロード・観測へ漏れ出さないことである。

---

## 1. 解いている問題は一般化できる

「ゲームによって違うから」で片付けられがちだが、違うのは **スコープ木の形とゲームルールの中身** であり、問題そのものは業界共通に一般化できる。OSM が答えを試みている共通問題は次の 5 つ。

| # | 一般化問題 | 隣接業界での既解決語彙（比喩） | OSM の対応物 |
|:---:|---|---|---|
| 1 | **入れ子の寿命は必ずある**（プロセス > セッション > 画面 > 個体）。どの構造を寿命の正本にするか、人間がそれを読めるか | DI コンテナのスコープ階層（request / session / application） | SceneGraph = スコープ木、SceneDirector 親子 + LoadType |
| 2 | **Ready の契約**。「これが使える状態になった」を誰がいつ保証するか | readiness / liveness の考え方 | SceneState `Stable`、親が子の Ready 前提を保証（[SCENEGRAPH_AS_SCOPE_TREE](planning/SCENEGRAPH_AS_SCOPE_TREE_2026-07-19.md)） |
| 3 | **依存の方向**。ゲーム固有コードが基盤の内臓に触れない構造を、規約でなく機械で守れるか | モジュール / レイヤ境界 | asmdef 一方向依存 + DependOnAll 単一配線点 |
| 4 | **部分的な作業空間**。全体を実体化せずに一部だけ触って検証できるか | sparse checkout 的な発想 | Variant / Checkout、Hybrid Play、どの Scene からでも Play |
| 5 | **観測が本体を汚さない**こと | 非侵襲な観測側の分離 | Telemetry sink 非伝播、DebugStudio をゲーム外へ |

隣接業界で 1〜3 に共通語彙があるのは、HTTP のような **強制的に共通な実行形状** があったからである。ゲームにはその強制がなく、各タイトルが毎回再導出してタイトルと一緒に捨てる。**木の形が違うだけで、木があること自体は共通** — ここに気づいて共通部分の問題文を書くことが、この repo の目標である。

---

## 2. 強み

### 2.1 依存が「読める」

多くのエンジン／プロジェクトでは、実行時の依存取得がサービスロケータや散在した参照に寄り、**誰が何に依存しているかがシグネチャに現れない**。アセット側のハード参照連鎖も、規律（soft ref 等）に委ねられがちである。

OSM 側の寄せ方:

- Game 層はコンストラクタ注入のみ。依存は型シグネチャで自明（[03-di.md](../unity/Assets/Docs/Architecture/03-di.md)）
- 配線は `DependOnAll`（`AppInitializer` + `GameSceneFactory`）の単一出口
- コード境界は Foundation → Runtime → Debug、InGame ↔ OutGame 禁止を **asmdef で運用**
- アセットは Addressables 経由を正とする（規律の領域が残る点は §3）

「他エンジンの境界を移植した」のではなく、**依存を型と配線点に現す**ことを目標にしている。

### 2.2 寿命が「契約」— 暗記でもコメントでもなく状態機械

- 14 状態 SceneLifecycle、状態変更は `SceneLifecycleManager` の単一オーナー（[05-scene.md](../unity/Assets/Docs/Architecture/05-scene.md)）
- 親子ツリー + LoadType（NecessaryAlways / OnDemand / Incremental）で「いつ居るか」を構造で宣言
- Scene Graph Editor がサイクル禁止・単一親をバリデーションし、SceneResource を Generate — **人間が Editor 上で寿命木を読める**
- 「いつロードして、いつ解放するか」を個人の腕に残さず、構造側（SceneDirector + AssetResidentCache）へ寄せる

### 2.3 この軸を一枚で書くこと自体が希少（ただし深掘りキューではない）

境界・寿命・依存を **横断して** 問題文にする公開物は少ない。出るとしても DI ライブラリ、画面遷移 FW、配信パイプライン、ポストモーテム断片に分かれがちである。

理由は構造的で、解ける人は各スタジオに居るが公開インセンティブが薄く、一般化を書き残すのは再導出コストに耐えかねた長寿組織に寄りやすい。**OSM は「まとまっていない」のではなく「誰も書く動機を持たなかった」領域に、実装つきで問題文を書いている。**

> **注意:** この観察は「表に出ている事例を全部対照シリーズ化する」宣言ではない。  
> 外部深掘りは [reference/HANDOFF.md](reference/HANDOFF.md) の選定基準とキューに従う。本ドキュメントは候補リストを増やさない。

### 2.4 失敗と却下理由が読める

「なぜ今は手動 DI か」「なぜ WorldPartition 級は作らないか」「なぜ Addressables を正とするか」が、決定日・再評価条件つきで残っている（[03-di.md](../unity/Assets/Docs/Architecture/03-di.md)、[00-motivation-overview.md](reference/00-motivation-overview.md) の「これはやらない」、LT スライド）。これは motivation §7 の実践であり、FW 本体より長期の資産になりうる。

---

## 3. 強みの限界（誇張しないための正直な線引き）

強みを主張するとき、次の 4 点を超えて語らないこと。

| 主張してよい | まだ主張できない |
|---|---|
| asmdef と Scene Graph 生成による構造的強制 | 子→親のみ参照の機械的強制（`ISceneQuery.GetLoadedScene` は任意 Identity を返す。規約 + レビュー） |
| DependOnAll への配線集約 | 「置き場の強制装置」（スライド自ら「まだない。人間の善意」と明言） |
| OutGame 縦糸での寿命契約の実証 | 複雑なゲームロジックを載せた侵食防止の実証（InGame 階層は Factory 未配線のスケルトン） |
| 手動 DI で現状足りていること | 手動 DI の恒久性 — **コンストラクタ / Factory が肥大したら VContainer 導入を再評価する合意済み**（03-di.md の再評価条件） |

また、全てを Scene に寄せているわけではない。Camera / Logger / AssetManagement / UICommon は App / Bootstrap 常駐であり、「画面分類木 ≠ 寿命スコープ」を認めて App サービスへ出す判断基準を持つ（[SCENEGRAPH_AS_SCOPE_TREE](planning/SCENEGRAPH_AS_SCOPE_TREE_2026-07-19.md)）。これは弱点ではなく設計判断だが、「依存と寿命を全部 Scene 構造で表現する」と要約すると過大になる。

---

## 4. 目標（この repo が向かう先）

1. **§1 の 5 つの一般化問題に、Unity 上の実装つき回答を持つこと。** 完成したゲームではなく、問題文と回答のペアが成果物
2. **強制のグラデーションを上げていくこと。** 規約 → Editor バリデーション → コンパイル強制の順で、子→親参照規律・置き場のような「善意の領域」を減らす（Analyzer 化は architecture-review の既知課題）
3. **InGame 縦糸で侵食防止を実証すること。** ゲームルールが複雑になっても Scene / Lifetime / UI / Service / Loading が汚染されないことを、スケルトンでないゲームで示す
4. **判断理由の蓄積を続けること。** 借りない理由・撤退条件・再評価条件つきの決定を正典に残す

---

## 5. 位置づけの言い換え集（外向けに話すとき）

| 避ける言い方 | 使う言い方 |
|---|---|
| ゲーム開発を簡単にするフレームワーク | 複雑なゲームを載せても、ゲーム以外まで複雑化しないための契約 |
| 他エンジンの境界を移植した | 依存を型と配線点に現し、寿命をスコープ木の契約にする試作 |
| 依存関係を破れなくした | asmdef と Scene Graph は破れない。参照規律はまだ規約（強制装置は目標） |
| DI コンテナの代替 | Scene スコープ木 + DependOnAll 手動配線。肥大したらコンテナを再評価する |
| Scene をうまく扱う Unity Framework | 境界・寿命・依存の一般化問題への、実装つき問題文 |
| reference シリーズの結論がこれ | 本ドキュメントは目標宣言。外部深掘りは HANDOFF キューの話 |

---

## 次に読むもの

| 目的 | ドキュメント |
|---|---|
| なぜこの repo があるか（初見） | [reference/00-motivation-overview.md](reference/00-motivation-overview.md) |
| 未完了の問いを表で追う | [reference/00-questions-we-are-answering-2026-07-11.md](reference/00-questions-we-are-answering-2026-07-11.md) |
| 参照シリーズの横断評価（別物） | [reference/00-cross-cutting-assessment-2026-07-11.md](reference/00-cross-cutting-assessment-2026-07-11.md) |
| スコープ木の意味論 | [planning/SCENEGRAPH_AS_SCOPE_TREE_2026-07-19.md](planning/SCENEGRAPH_AS_SCOPE_TREE_2026-07-19.md) |
| 設計の入口 | [ARCHITECTURE.md](../unity/Assets/ARCHITECTURE.md) |

---

## 更新履歴

| 日付 | 内容 |
|---|---|
| 2026-07-25 | 初版（いったん `docs/reference/` に置いたが、外部シリーズと混ざるため同日 `docs/` 直下へ移動）。公開事例カタログを削り、深掘りキュー化しない旨を明記 |
