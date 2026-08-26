# 30. 意味アイデンティティ層（Accessibility Identity）

> ステータス: **設計中**（2026-08-22）。§6 の理想は確定。§11 はホストの有無で消費者を分けた。**§9 以降の契約は S-1（番地）のみ暫定確定**
> [ARCHITECTURE.md](../../ARCHITECTURE.md) に戻る
> 関連: [05-scene.md](05-scene.md)、[06-ui.md](06-ui.md)、[31-accessibility-output-budget.md](31-accessibility-output-budget.md)、[32-accessibility-input-dof.md](32-accessibility-input-dof.md)、[21-scene-streaming.md](21-scene-streaming.md)、[23-camera-system.md](23-camera-system.md)、[24-rendering-system.md](24-rendering-system.md)、[26-update-async-time-authority.md](26-update-async-time-authority.md)

本章は **番地付けと分類の層** である。アクセシビリティ機能そのものではない。
配送・注意の調停は [§31](31-accessibility-output-budget.md)、片腕の入力自由度は [§32](32-accessibility-input-dof.md)。

ジャンル（STG 含む）を前提にしない。**コードはまだ書かない。**

**この文書の書き方の規律**（2026-08-22 に追加。過去にこれを守らず契約が空転した）:

- **消費者が存在しない要求で契約を凍結しない。** §11.1 が「今日成立する消費者」と「未着手サブシステムの後ろにある消費者」を分けている。後者の要求で型を増やさない
- **理想（§6）は契約（§9 以降）より先に来る。** どの情報が何を可能にするかを書いてから、その上位だけを実装する
- **「可能になること」の欄が埋まらない情報は載せない**

---

## 目次

1. [一文](#1-一文)
2. [目的・スコープ](#2-目的スコープ)
3. [Web 由来との差](#3-web-由来との差)
4. [用語定義](#4-用語定義)
5. [なぜクラス継承ではないか](#5-なぜクラス継承ではないか)
6. [World の理想](#6-world-の理想)
7. [設計判断](#7-設計判断)
8. [寿命](#8-寿命)
9. [データ契約](#9-データ契約)
10. [レジストリ](#10-レジストリ)
11. [消費者への供給](#11-消費者への供給)
12. [API スケッチ](#12-api-スケッチ)
13. [配置](#13-配置)
14. [今やらない](#14-今やらない)
15. [実装スライス](#15-実装スライス)
16. [撤退ライン](#16-撤退ライン)
17. [オープン論点](#17-オープン論点)

---

## 1. 一文

ゲーム内の「もの」は表現（`VisualElement` / `GameObject` / 将来の描画インスタンス）が違っても、同じ記述子を持つ。
強調描画・触覚・非音声音・読み上げ・方向走査・片腕フォーカスは、いずれもこの記述子の**消費者**であり、系統ごとに名前を持たない。

本章が満たす答えは「それは何か、どの分類か、今どの Flags か、どこにあるか（供給元経由）」までである。
「今、希少な出力に何を載せるか」は満たさない。それは §31。

---

## 2. 目的・スコープ

**本章が証明すること（将来）:**

- 意味のあるオブジェクトは、表現が違っても同一の Kind / Flags / 位置で問える
- セルストリーミングで出入りしても、Scene 寿命と登録がずれない
- 消費者（§11.1）が必要とする問い合わせの番地が欠けない

**本章が主張しないこと:**

- 視覚に頼れない人が、この層だけでプレイできる
- 登録物を全部読み上げれば全盲向けになる
- 片腕プレイの完成（候補集合の供給だけ。[§32](32-accessibility-input-dof.md)）
- **作者付けの漏れを検出できること。** 既定は未登録（D-4）なので、機構が見つけられるのは「登録済みで不備があるもの」だけである。**誰も登録しなかったオブジェクトは、原理的に検出できない。** これはオプトイン方式の唯一かつ最大の失敗モードであり、受け入れたうえで採用している

**今やること:** 契約の文書化と S-1 の範囲確定。コードは [§14](#14-今やらない)。

## 3. Web 由来との差

ARIA の role / name / state は、おおむね次が揃う文脈で成立している。

| Web 側の前提 | ゲーム世界 |
|---|---|
| 対象が離散で、文書に列挙できる | 動的生成・プール・ストリーミング。列挙は Scene 寿命に依存する |
| ユーザーが動かさない限り、対象は動かない | 視点も対象も動く |
| 文書順という 1 次元の読み上げ順序がある | 順序は距離・方位・緊急度。文書順は無い |
| レイテンシ予算が人間の読書スケール | プレイのテンポ。音声は毎秒数語しか運べない |

ラベル・ヒントは Web と同じく **呼ぶための番地** であり、有用である。UI のスクリーンリーダ、pull クエリ、ガイド文の名詞、AI が指差す対象、片腕の巡回候補は、名前が無いと成立しない。

足りないのは番地ではない。足りないのは:

1. **空間と連続量のチャネル** — 方位・距離・接近の度合いを運ぶ経路が無い。ARIA が持つのは文字列だけである
2. **速度** — 希少な出力チャネルに、今載せる価値のある少数は何か

なお **「何が変わったか」は Web も解いている。** `aria-live` のライブリージョンがそれで、`polite`（キューに積む）と `assertive`（現在の発話を中断し、キューを捨ててよい）の 2 段に振り分ける。[§31](31-accessibility-output-budget.md) の変更フィードはこれのゲーム世界版であり、新発明ではない。**振り分けの段数も、標準は 2 段 + off で足りている**（§31 は 4 クラスを立てている）。

タグ / ラベル / ヒントはこれらを扱わない。扱わないと明言したうえで、答えは §31 に置く。連続量（接近、残量）も記述子に載せない。離散ビットと名前に閉じる。

---

## 4. 用語定義

| 用語 | 定義 |
|---|---|
| 記述子 (`AccessibilityDescriptor`) | 静的な意味の値型。Kind / 既定 Flags / Label。位置は持たない（D-11） |
| エントリ | レジストリの**内部表現**。公開型ではない。記述子 + 動的 Flags + 位置供給元 + 世代 |
| Kind | **分類**。クエリの絞り込みと、消費者が語彙・表現を選ぶための鍵。単一値（D-12） |
| Flags | ホットパス用ビット。側面と動的状態 |
| Label | 人向けの名前。**説明的チャネルの積荷**であり、即時チャネルには載らない（§6.2 帰結 3） |
| Handle | `slot` + `generation`。レジストリ内の位置と世代。[`UpdateHandle`](../../OneStarMaker/Scripts/Foundation/UpdateSystem/Contracts/UpdateHandle.cs) と同型（D-15） |
| StableId | `{SceneIdentity}/{localId}`。`localId` は必須。**コールドパスの引き当てにのみ使う** |
| レジストリ | App 常駐の登録表。Scene スコープで一括解除できる |
| **即時チャネル** | 触覚・非音声音・視覚の即時強調。低レイテンシ。予算の単位は**区別できる信号の種類数**（§6.2） |
| **説明的チャネル** | 読み上げ・字幕・点検 UI・DebugStudio。学習不要。予算の単位は**毎秒の語数**（§6.2） |
| Backend | プラットフォームのアクセシビリティ木（`AssistiveSupport` / AccessKit）等への投影先。`IAccessibilityBackend`（§11.2） |
| 作者付け | 記述子を載せる作業。未付けは「存在しない」（§2 の限界） |
---
## 5. なぜクラス継承ではないか

「全オブジェクトが継承する」を `GameEntity : MonoBehaviour` で実装すると、今の OSM に届かない。

| 表現 | 実態 | 登録のタイミング | 解除の粒度 |
|---|---|---|---|
| ワールド作者物 | Unity `GameObject` | Scene Stable の初期走査（Attach + Register）。以降は Attach 済みの `OnEnable` / `OnDisable` | Scene identity 一括。個体は Disable / Unregister / OnDestroy |
| 将来の大量描画 | GameObject を持たないインスタンス | 生成時 | 破棄時 / Scene 一括 |

**UI はこの表に無い。** UI の意味情報はプラットフォームのアクセシビリティ木が既に持つ型（`AccessibilityNode` の role / state / value / label / hint、AccessKit も同型）を持っており、本章のレジストリに二重登録すると**同じ事実が 2 箇所に置かれる**。UI は Backend への投影として扱い、本章に UI 専用の登録経路を作らない（§11.2）。

実装形は Transform のような必須サイドカー＋同一記述子契約。表現ごとのアダプタが記述子を出し、レジストリが寿命を持つ。
---

## 6. World の理想

**どの情報があれば、何ができるようになるか。** この節は契約ではない。World 側の消費者から逆算するための表であり、計測と実装で改訂される。§9 以降の契約が実装するのは、この表の上位だけである。

規律を一つ課す: **「可能になること」の欄が埋まらない情報は、レジストリに載せない。** Label / 文言解決器 / loc が S-1 に入らないのは、この規律の結果であって好みではない。

前提として、World の支援は 3 段に分かれる — **知覚 → 判断 → 実行**。障害が奪うのは知覚か実行の帯域である。したがって:

> **判断は奪わない。知覚と実行だけ補う。**

自動プレイがこの線を越えているのは「判断を代行した」からであり、速度でも自動化率でもない。以下の表は、この線の内側で何ができるかの棚卸しである。

### 6.1 情報と、それが可能にすること

| # | 情報 | 可能になること | 無いと起きること | 供給コスト | OSM の現状 |
|---|---|---|---|---|---|
| W-1 | 存在と分類（Kind） | 種別フィルタ付きクエリ。強調描画の色分け。点検モードの一覧 | すべてが「もの」になり選別できない。帯域を使う手段が全部死ぬ | 低（作者付け） | 無い |
| W-2 | 位置 | 距離順・方位・定位。強調の優先順。接近／離反の導出 | 距離を持つ支援がすべて成立しない | 低（Transform） | 無い |
| W-3 | 跨ぎ同一性 | 「さっきの敵」「まだ居る」。再告知の抑制 | 毎フレーム新規扱いになり、うるさくなる | 低 | **解決済み。`UpdateHandle` の slot + generation を踏襲する** |
| W-4 | 大きさ・範囲 | 点か領域かの区別。Region の入退場。告知と強調の粒度 | 壁と鍵が同じ粒度になる | 低〜中 | 無い |
| W-5 | 可視性・遮蔽（視線が通るか） | 「見えているのに気づいていない」と「壁の向こう」の区別。強調の可否。**方向走査で「その方向に何があるか」を答える** | 壁越しの敵を光らせる（興ざめ）か、見えている物を落とす。**方向走査が成立しない** | **高**（視線判定が要る） | [§23](23-camera-system.md) F-7 のフラスタム平面が使える（[§24](24-rendering-system.md) D-7 により描画カリングには未使用）。視線判定そのものは無い |
| W-6 | 相互作用可能性 | 巡回候補の生成。決定 1 ボタンの対象集合 | 候補集合が作れない | 低（Flags） | 無い |
| W-7 | 到達可能性 | 「拾えるが行けない」を候補から外す | 到達不能な対象へ案内して詰ませる | **高**（ナビ情報） | 無い。供給元も無い |
| W-8 | 変化（差分） | 「前回から何が変わったか」。新規出現・消滅・状態遷移 | プレイヤーが毎回すべてを問い直す。点検コストが登録数に比例する | 中 | 無い |
| W-9 | 集約・階層 | 「敵が 3 体」「棚に 5 個」。**個別列挙をやめる** | 20 個の登録物が 20 回の告知になる | 中 | 無い |
| W-10 | Game 定義の順序値 | 同種の候補を畳むときの優先順 | 同率の候補を並べ替えられない | 低 | 無い。**D-16 と衝突する論点**（§6.2 帰結 4 で解決） |
| W-11 | 名前（Label） | UI の読み上げ。作者付け監査。DebugStudio 表示 | UI が読めない。デバッグで対象を指せない | 低 | 無い |

### 6.2 チャネルの二分 — 即時か、説明的か

チャネルは帯域だけでなく**レイテンシと学習コスト**が違う。おおむね逆相関する（速いものほど覚える必要がある）が、**厳密な二分ではない**。HCI の実測では習得に要する訓練回数が auditory icon（実世界の音）< spearcon（極端に時間圧縮した音声）< earcon（抽象的な音型）の順で、spearcon と speech は平均 1.14 回で正答率 100% に達する一方、earcon は最も多くを要する。つまり **「速い」と「学習不要」は両立しうる**（spearcon）。以下の二分は設計の出発点であり、実装時にはこの勾配で選ぶ。

| | **即時チャネル**（符号的） | **説明的チャネル**（学習不要） |
|---|---|---|
| 実体 | 触覚、非音声音、視覚の即時強調（点滅・輪郭） | 読み上げ、字幕、点検 UI の一覧、DebugStudio |
| レイテンシ | フレーム〜数百 ms | 秒 |
| 学習コスト | **要る**（音の意味を覚える）。ただし auditory icon / spearcon なら小さい | **要らない**。冗長であることが機能 |
| 予算の単位 | **区別できる信号の種類数**。上限値は実測で決める（[§31](31-accessibility-output-budget.md) D-7） | **毎秒の語数**（数語） |
| 運ぶもの | 「今」「どっち」「どれくらい」 | 「何が」「何と呼ぶか」「どうすれば」 |
| 連続量 | **ここに載せる**（ピッチ・強度・間隔） | 載せない |
| 駆動 | push（世界が起点） | **pull で足りる**（プレイヤーが起点） |
| 必要な行 | W-1（粗い分類）, W-2, W-5, W-8 | W-1, W-4, W-6, W-7, W-9, W-11 |

振り分けの規則は一行である:

> **即時性が要るものは即時チャネルへ。それ以外は説明的チャネルへ。同じキューは奪わない。**

帰結が 4 つある。

| # | 帰結 |
|---|---|
| 1 | **[§31](31-accessibility-output-budget.md) のキュー競合の大半は、1 本のチャネルに全部載せたことの副作用である。** 分ければ、緊急の告知と描写的な告知は**同じキューを奪わない**。注意そのものは共有されるので、同時提示が人間側で衝突しうる |
| 2 | `Urgent`（§9.1）は優先度ではなく**即時チャネルの候補資格**である。1 ビットで足りるのは、行き先そのものではなく「即時に出してよいか」を表すから。振り分けは §31 がイベントごとに行う（D-17） |
| 3 | **W-11（名前）は説明的チャネルの積荷であり、即時チャネルには載らない。** 「Label を落とす」のではなく「Label を pull 側に閉じる」が正しい |
| 4 | **W-O-2 が解ける。** 連続量は即時チャネルのパラメトリック符号化（D-16 の言う通り）、W-10（Game の順序値）は説明的チャネルの並べ替え。別チャネルの別機構なので衝突しない |

そして**予算の単位が違う**。§31 は「毎秒の語数」しかモデル化していないが、即時チャネルの上限は時間あたりではなく **プレイヤーが区別を学習できる信号の種類数**である。同じ予算式では設計できない。

### 6.3 手段ごとに必要な行

音は「知覚」を補う一手段にすぎない。手段を §6.2 のチャネルで分類すると、必要な情報が決まる。

| 手段 | チャネル | 律速を変える対象 | 必要 | 不要 |
|---|---|---|---|---|
| 視覚の作り替え（輪郭強調・コントラスト。ロービジョン） | 即時 | 知覚 | W-1, W-2, W-5 | W-9, W-10, W-11 |
| 触覚・非音声音（方向・近接・遮蔽） | 即時 | 知覚 | W-1, W-2, W-5, W-8 | W-4, W-7, W-9, W-11 |
| 点検（停止・低速中の pull クエリ） | 説明的 | 知覚 | W-1, W-2, W-4, W-6, **W-11** | W-8（pull なので差分が要らない） |
| 実行支援（巡回・スナップ・トグル化） | 説明的 | 実行 | W-1, W-2, W-6, W-7, W-11 | W-5, W-8, W-9 |
| レート（[§26](26-update-async-time-authority.md) の Layer timeScale を落とす） | — | 知覚と実行の**猶予** | W-8（未読量を時計の入力にする） | 他 |
| 音声配送（[§31](31-accessibility-output-budget.md)） | 説明的 | 知覚 | W-1, W-2, W-4, W-6, W-9, W-10, W-11 | — |

**W-1 / W-2 は全手段に現れる。W-11（名前）は説明的チャネルにのみ現れ、即時チャネルには一度も現れない。**

S-1 は消費者を置かず、全手段に共通する W-1 / W-2 / W-3 だけを証明する（§15）。チャネル選択はホストが実装されてから行う。Label をデバッグ表示用のプレーン文字列として持ってよい。この段階では `IAccessibilityText` と loc は要らない。

### 6.4 この表から出た未解決

| # | 論点 | 状態 |
|---|---|---|
| W-O-1 | **W-9（集約）が帯域に最も効く可能性がある。** 個別を減らすのが最大の節約であり、予算調停より前に来る。§6.2 により、これは**説明的チャネル側の問題**だと確定した。[§31 D-10 / §8](31-accessibility-output-budget.md#8-顕著性と集約) に機構として立てたが、**粒度（距離 / Kind / 階層）は未設計**。本章側は W-9 を供給していない | 未決 |
| W-O-2 | **W-10 と D-16 の衝突** | **解決（§6.2 帰結 4）。** 連続量は即時チャネルの符号化、W-10 は説明的チャネルの並べ替え。別機構なので衝突しない |
| W-O-3 | **W-5（視線）は説明的チャネルの必須要件だった。** NavStick 型の方向走査は line-of-sight で「その方向に何があるか」を答える。強調描画の本番品質にも要る。供給元が無いので S-1 では作らない | 未決。ホスト待ち |
| W-O-4 | **pull の interface は一覧ではなく方向走査であるべき。** 実測で、リストを辿る逐次 UI より好まれている。§10 の `Query`（`List` に詰める）はこの形を作れない。方向・扇形のクエリが要る | 未決 |
| W-O-5 | W-7（到達可能性）は供給元が存在しない | 保留 |
| W-O-6 | **Stable 時の初期走査は、対象 0 個のシーン／セルでも全額払う。** ストリーミングでは「セルが Stable になったフレーム」が [§21](21-scene-streaming.md) の守りたい予算そのもの。opt-in にすべきか | 未決。**計測してから決める** |

---

## 7. 設計判断

### 7.1 決定事項

| # | 決定 | 根拠 |
|---|---|---|
| D-1 | **記述子が識別の正本。** プラットフォームのアクセシビリティ木は Backend（投影先）であって正本ではない | 2D の読み上げ順序モデルであり、距離・方位・遮蔽を持たない。世界側の問い合わせの入力にならない |
| D-2 | **契約は FW。Kind はジャンル非依存。** Kind は数値域を分け、FW 予約域と Game 域を持つ。Flags も同じく bit 16–31 が Game | Foundation の `enum` に Game は名前を足せない。**旧版は「Game 拡張は Flags で」と書いていたが、Flags は形容詞であって名詞ではなく、Game が分類を足す手段が無かった**。数値域方式で解消する（§9.1） |
| D-3 | **解除の粒度は Scene identity 一括。** 登録タイミングは表現ごと（§5） | ストリーミングでセルが出入りしても、走査ではなく identity で落とせる |
| D-4 | **既定は未登録。** Decorative は「在るが黙る」と明示したいときだけ | 全 GO 自動付与は問い合わせ結果を無意味にする。代償は §2 の最後（漏れを検出できない） |
| D-5 | **名前の正本は一つ。** 系統ごとに DisplayName を増やさない | 説明的チャネルの消費者は全て同じ Label を読む |
| D-6 | **欠番。** 旧 D-6（片腕は同時入力の自由度を落とす）は [§32 D-1](32-accessibility-input-dof.md) へ移した | ID を安定させるため再利用しない |
| D-7 | **欠番。** 旧 D-7（アシスタントは特定 LLM に結合しない）は §31 へ移した | 同上 |
| D-8 | **Label はプレーン文字列。S-1 では解決器を入れない** | loc 本体は入れない。`IAccessibilityText` は消費者が現れてから（§14） |
| D-9 | **ホットパスは Kind + Flags + Handle スロット。** 文字列はコールド | 毎フレームの string 比較をしない |
| D-10 | **配線は手動 DI。** Authoring は `Attach` されるまで自己登録しない | サービスロケータ禁止。[AssetReleaseOnDestroy.Initialize](../../OneStarMaker/Scripts/Runtime/AssetManagement/Components/AssetReleaseOnDestroy.cs) と同型 |
| D-11 | **静的な意味と動的な状態を分ける。** 位置は供給元、Flags は `SetFlags` | 記述子に座標を焼くと距離が追従しない |
| D-12 | **Kind は単一値の分類。** 側面は Flags | 複数 Kind だと絞り込みの意味が決まらない。**「ガイドが最初に言う名詞」ではない** — 発話語は Label の仕事（§6.2 帰結 3） |
| D-13 | **Query は呼び出し側バッファ。** 詳細は Handle + `TryGetDescriptor` | 毎回の割り当てをしない。**ただし一覧を返す形が消費者の実装形と合っていない可能性がある**（W-O-4。方向走査） |
| D-14 | **初期走査が Attach と初回 Register を同時に行う。** Attach 前の OnEnable は登録しない | Stable 時点で OnEnable は既に終わっている。走査と OnEnable の二重 Register を防ぐ |
| D-15 | **Handle は `slot` + `generation`。** [`UpdateHandle`](../../OneStarMaker/Scripts/Foundation/UpdateSystem/Contracts/UpdateHandle.cs) を踏襲する | **スロット再利用の ABA 問題**。旧版は「跨いで保持しない」という規約だけで、A を Unregister した後の古い Handle が、同じスロットを取った B の Flags を黙って書き換えられた。FW 内に解決済みの前例があるのに別形を発明していた |
| D-16 | **連続量は記述子に載せない** | 接近・残量は**即時チャネルのパラメトリック符号化**（ピッチ・強度・間隔）で運ぶ。Identity をテレメトリバスにしない（§6.2） |
| D-17 | **チャネル振り分けが先、調停は後。** `Urgent` は**即時チャネルの候補資格**であり、通知の行き先そのものではない | 振り分けは [§31](31-accessibility-output-budget.md) が変更フィードの**イベントごと**に行う。オブジェクト Flags に行き先を焼くと、同じ Actor の接近（即時）と名前照会（説明的）を分けられない |
| D-18 | **`IAccessibilityBackend` を挟む。** 公開 API に Unity 型を漏らさない。**片方向の翻訳テストを課す** | [§24 D-3](24-rendering-system.md)「高価な実装の着手を計測まで遅延できる」。§24 D-5 と同じ型漏れ禁止。翻訳テストは §11.2。**AccessibilityRole への全単射は要求しない** |
| D-19 | **`Register` は Handle を返す（`out`）** | 返さないと登録側が毎回 string で引き直すことになり、D-9 を契約自身が破る。[`UpdateElementRegistry.Register`](../../OneStarMaker/Scripts/Foundation/UpdateSystem/Elements/UpdateElementRegistry.cs) と同形 |
| D-20 | **重複 StableId は Editor / 開発ビルドで例外、製品ビルドではログ + `false`** | `UnregisterScene` の呼び出し元は 4 経路（PreUnloading / LoadCanceled / Dispose / Bootstrap）あり、1 つ落とすとセル再入のたびに例外が飛ぶ。セル identity は `Cell_{x}_{y}` で固定なので確実に再現する |
| D-21 | **レジストリはメインスレッド限定** | `ApplyMainThreadChanges` を持つ UpdateSystem と同じ前提に揃える。並行登録を契約しない |

### 7.2 却下案

| 却下 | 理由 |
|---|---|
| `GameEntity` 強制継承 | §5 |
| プラットフォームのアクセシビリティ木を正本にする | D-1 |
| **プラットフォームのモデルをそのまま採用し、抽象を持たない** | D-18。Backend を持たないと、CI に載らない実装に全体が引きずられる |
| ジャンル固有 Kind を Foundation に置く | D-2。Game 域に置く |
| **Game 拡張を Flags だけで賄う** | D-2。Flags は形容詞であり、分類（名詞）を足せない |
| 全 GO 自動付与 | D-4 |
| 系統ごとに DisplayName | D-5 |
| 座標を Descriptor に焼いて Register し直す | D-11 |
| UI を本章のレジストリへ二重登録する | §5。Backend が既に同じ情報を持つ |
| GameObject 名を `localId` 既定 | 同名衝突 |
| Attach 前の OnEnable 自己登録 | registry を持たないのが正しい既定（D-14） |
| **Handle を slot だけで公開する** | D-15。ABA |
| Urgent を優先度クラスとして使う | D-17。優先度ではない |
| Urgent をオブジェクトの行き先そのものにする | D-17。行き先はイベントごと |
| 登録物を全部読み上げる | 識別層の仕事ではない。§31 |

## 8. 寿命

Scene が寿命の正本（[05-scene.md](05-scene.md)）。**解除**はそれに従う。

```
Scene Loaded → Initialize（Root 取得）
  → Stable     … 走査が Attach(registry, sceneIdentity) と初回 Register を同時に行う
  → （実行中） … Attach 済みの OnEnable / OnDisable だけが個体の出入り
  → PreUnloading / LoadCanceled / Dispose … UnregisterScene(identity)（冪等）
  → Unload
```

UI はここに現れない（§5。Backend への投影であり、本章に登録しない）。

[`SceneBase.ExecutePreUnLoad`](../../OneStarMaker/Scripts/Runtime/SceneSystem/SceneBase.cs) は `OnPreUnLoadedImpl` の前に `_rootObjects` を Clear する。
解除は GO 走査ではなく `UnregisterScene(sceneIdentity)`。Clear との前後は関係ない。

App 常駐レジストリなので、次でも `UnregisterScene` する（冪等）:

- `LoadCanceled`
- `SceneBase.Dispose`
- Editor 停止 / Domain Reload 無効時の Bootstrap クリーンアップ

**この 4 経路のうち 1 つでも落とすと、同じ identity のシーンに再入した時点で重複登録になる。** セル identity は `Cell_{x}_{y}` で固定（[CellScene](../../OneStarMaker/Scripts/Runtime/SceneSystem/Cells/CellScene.cs)）なので、ストリーミングで確実に再現する。したがって重複時の挙動を D-20 で分けている（Editor は例外、製品ビルドはログ + `false`）。

初期走査は `GetComponentsInChildren<T>(includeInactive: true)`。
走査は初期集合に限る。生成物は、生成側が Attach してから Enable する。

> **未計測のコスト:** この走査は Stable の瞬間にメインスレッドで走り、**対象が 0 個のシーン／セルでも全額払う**。ストリーミングでは「セルが Stable になったフレーム」が [§21](21-scene-streaming.md) の守りたい予算そのものである。opt-in（既定 false の virtual、あるいはルートのマーカー）にすべきかは W-O-6 として未決。

OnDestroy でも Unregister する（個体）。Unregister されずに GO だけ壊れたエントリは、位置供給元が偽 null なら位置を要する問い合わせから落とす。`TryGet` には残ってよい。Scene 一括解除が最終掃除。

セルストリーミングでは Cell の Stable / Unload がレジストリの出入りになる。WSC の desired set にアクセシビリティ判断を足さない。
---

## 9. データ契約

Foundation の純 C#。Unity UI にも Unity Accessibility にも依存しない（D-18）。

### 9.1 Kind

分類であって発話語ではない（D-12）。**`enum` ではなく数値域を持つ値型**にして、Game が分類を足せるようにする（D-2）。

```csharp
public readonly struct AccessibilityKind : IEquatable<AccessibilityKind>
{
    public int Value { get; }        // 0 = Unknown / 1–999 = FW 予約 / 1000– = Game
    public bool IsGameDefined => Value >= 1000;
}

public static class AccessibilityKinds   // FW 予約域のみ
{
    public static readonly AccessibilityKind Unknown  = new(0);
    public static readonly AccessibilityKind Actor    = new(1);   // 動くもの
    public static readonly AccessibilityKind Item     = new(2);   // 持てる・使える
    public static readonly AccessibilityKind Region   = new(3);   // 領域。セル・部屋
    public static readonly AccessibilityKind Passage  = new(4);   // 通行点。扉・階段・トンネル
    public static readonly AccessibilityKind Obstacle = new(5);   // 通行不能・遮蔽
    public static readonly AccessibilityKind Surface  = new(6);   // 地面・床材
    public static readonly AccessibilityKind Marker   = new(7);   // 作者が置いた目印
}
```

この 7 つはジャンルからではなく、盲目プレイヤーが頼ると報告されている手掛かり（不動のランドマーク、領域の切り替わり、通行点、足元の材質）から**推定した出発点**である。このゲームでの実測ではない。足りなければ足す（O-5）。ジャンル固有の名詞（敵機・宝箱）は Game 域に置く。

### 9.2 Flags

```csharp
[Flags]
public enum AccessibilityFlags : uint
{
    None        = 0,
    Focusable   = 1 << 0,
    Announcable = 1 << 1,
    Queryable   = 1 << 2,
    Hidden      = 1 << 3,
    Navigable   = 1 << 4,
    Actionable  = 1 << 5,
    Urgent      = 1 << 6,  // 即時チャネルの候補資格（D-17）。行き先そのものではない
    // bit 7–15: FW 予約
    // bit 16–31: Game 専用
}
```

`Hidden` は消費者から除外されるべき印。`Hidden | Focusable` と `Hidden | Announcable` は作者付け不備。
**Query は Hidden を自動では落とさない。** 落としたい消費者は `ExcludedFlags` に指定する（§10）。

`DefaultFlags` は Register 時の初期値にすぎない。以降は `SetFlags` が全置換。

### 9.3 Descriptor

```csharp
public readonly struct AccessibilityDescriptor
{
    public string StableId { get; }      // {OwningSceneIdentity}/{localId}
    public AccessibilityKind Kind { get; }
    public AccessibilityFlags DefaultFlags { get; }
    public string Label { get; }         // 説明的チャネル専用。即時チャネルは読まない
}
```

- `OwningSceneIdentity` は**フィールドとして持たない**。`StableId` から切り出す。旧版は両方を持っており、同じ事実が 2 箇所にあった
- `localId` 必須。空 `localId` は `Combine` が例外
- 空 Label かつ Focusable / Announcable / Actionable は不備（§12.3 が判定）
- **`Hint` は S-1 に入れない。** 説明的チャネルの消費者（点検・巡回）が現れてから足す（§14）

### 9.4 位置の供給元

```csharp
public interface IAccessibilityPositionSource
{
    bool TryGetWorldPosition(out float x, out float y, out float z);
}
```

ワールド Authoring は Transform を包む。**破棄済み `UnityEngine.Object` は `== null` が true になる一方、`?.` と `??` は Unity の `==` オーバーロードを迂回して短絡しない。** したがって `if (_transform == null) return false;` と書く。`_transform?.position` や `_transform ?? fallback` は誤り。

位置は Register 時に焼かない。読む側が**その瞬間に** `TryGetWorldPosition` する（D-11）。

`UnityEngine.Vector3` を使わないのは Foundation の既存方針に合わせるため（Foundation は現在 `Vector3` を一度も使っていない）。

---

## 10. レジストリ

App 常駐。SceneDirector が SceneBase へ注入し、SceneBase が Authoring へ `Attach` する（D-10, D-14）。
**メインスレッド限定**（D-21）。

```csharp
public interface IAccessibilityRegistry
{
    bool Register(
        in AccessibilityDescriptor descriptor,
        IAccessibilityPositionSource? positionSource,
        out AccessibilityHandle handle);          // D-19

    bool SetFlags(in AccessibilityHandle handle, AccessibilityFlags flags);
    bool AddFlags(in AccessibilityHandle handle, AccessibilityFlags flags);
    bool RemoveFlags(in AccessibilityHandle handle, AccessibilityFlags flags);

    bool Unregister(in AccessibilityHandle handle);
    void UnregisterScene(string sceneIdentity);

    bool TryGetHandle(string stableId, out AccessibilityHandle handle);   // コールド
    bool TryGetDescriptor(in AccessibilityHandle handle, out AccessibilityDescriptor descriptor);
    bool TryGetFlags(in AccessibilityHandle handle, out AccessibilityFlags flags);
    bool TryGetPosition(in AccessibilityHandle handle, out float x, out float y, out float z);

    int Query(List<AccessibilityQueryResult> results, in AccessibilityQueryFilter filter);
}

/// slot + generation のみ。ABA を避ける（D-15）
public readonly struct AccessibilityHandle : IEquatable<AccessibilityHandle>
{
    public static readonly AccessibilityHandle Invalid = default;
    public int Slot { get; }
    public uint Generation { get; }
    public bool IsValid => Generation != 0;
}

/// Query の戻り。ホットパス用に Kind / Flags のコピーを同梱する（D-9）
public readonly struct AccessibilityQueryResult
{
    public AccessibilityHandle Handle { get; }
    public AccessibilityKind Kind { get; }
    public AccessibilityFlags Flags { get; }
}

public readonly struct AccessibilityQueryFilter
{
    public AccessibilityKind? Kind { get; }
    public AccessibilityFlags RequiredFlags { get; }   // すべて含む
    public AccessibilityFlags ExcludedFlags { get; }   // 1 つでも含めば落とす
}
```

契約:

| 項目 | 挙動 |
|---|---|
| `Register` の重複 StableId | **Editor / 開発ビルドは例外、製品ビルドはログ + `false`**（D-20） |
| `Register` の null descriptor / 空 `localId` | 例外（プログラミングエラー） |
| `Register` の破棄済み供給元 | `false`。例外にしない |
| `Unregister` / `UnregisterScene` / 対象なしの Flags 変更 | 冪等。`false` を返すだけ |
| 失効した Handle での操作 | **generation 不一致で `false`。黙って別オブジェクトを書き換えない** |
| `Query` の `results` | **呼び出し側バッファ。実装が `Clear()` してから詰める。**戻り値は件数 |
| `Query` と `Hidden` | 自動では落とさない。`ExcludedFlags` に入れるのは消費者の責任 |
| `TryGetPosition` | 供給元が無い、または偽 null なら `false` |

`Handle` は `slot` + `generation` だけを持ち、`Kind` / `Flags` は持たない。持たせると `SetFlags` 直後に stale なコピーが出回る。ホットパスで両方が要る消費者のために、`Query` は `AccessibilityQueryResult`（その瞬間のコピー）を返す。

実装は [`UpdateElementRegistry`](../../OneStarMaker/Scripts/Foundation/UpdateSystem/Elements/UpdateElementRegistry.cs)（178 行）と同型 — 識別子 → slot の Dictionary、空きスロットの Stack、Remove で generation を進める、`TryGetEntry` が generation 不一致を弾く。**新しい設計ではなく既存パターンの踏襲である。**

### 10.1 未決 — 方向クエリ

一覧を返す `Query` は、**説明的チャネルの実装形と合っていない可能性がある**（W-O-4）。実測されている形は「方向をスクラブして、その方向に視線が通るものを答える」であり、リストの逐次走査は明示的に劣るとされている。

方向クエリは W-5（視線判定）を要求し、供給元が無い（W-O-3）。したがって **S-1 では作らない**。作るときは `Query` を置き換えるのではなく、別メソッドとして足す。

### 10.2 供給しないもの

Register / Unregister / SetFlags は §31 の変更フィードの材料になるが、**購読 API は本章に置かない**。§3 のとおり、変更フィードは `aria-live` のゲーム世界版であって本章の発明ではない。
---

## 11. 消費者への供給

本章が保証するのは **「何が・どこに・どの状態で在るか」** であって、「何と言うか」ではない。
名前（W-11）は**説明的チャネルの消費者にだけ**供給する（§6.2 帰結 3）。

### 11.1 消費者と、供給する行

| 消費者 | チャネル | 供給する行 | 前提 | 状態 |
|---|---|---|---|---|
| **DebugStudio inspector** | 説明的 | W-1, W-11 | ホストは存在する。アクセシビリティ用 surface は未着手。プロトコル非変更の範囲 | **今日ホストがある最初の候補** |
| **レート**（[§26](26-update-async-time-authority.md) の Layer timeScale） | — | W-8 | `UpdateLayer.SetTimeScale` は実装済み。未読量との接続は未着手 | Sample での実験 |
| **強調描画**（[§24](24-rendering-system.md) Policy 層。輪郭・コントラスト） | 即時 | W-1, W-2, W-5 | §24 は**構想段階**。`IRenderBackend` はコードゼロ | **S-1 の消費者にしない**。ホスト待ち |
| **点検・方向走査**（NavStick 型） | 説明的 | W-1, W-2, W-4, W-5, W-6, W-11 + **方向クエリ** | W-5 の視線判定が要る（W-O-3）。`Query` の形が合わない（W-O-4） | 後続。**S-1 の見積もりを超える** |
| **実行支援・巡回**（[§32](32-accessibility-input-dof.md)） | 説明的 | W-1, W-2, W-6, W-7, W-11 | InputManager 未着手 | 後続 |
| **触覚・非音声音** | 即時 | W-1, W-2, W-5, W-8 | Phase 2 Sound 未着手 | 後続 |
| **音声配送**（[§31](31-accessibility-output-budget.md)） | 説明的 | 全部 | Phase 2 Sound 未着手 | 後続 |

上 2 行はホストが今日ある。下 5 行は未着手のサブシステムの後ろにある。
**契約を下 5 行の要求で凍結しない。** 消費者が現れてから、消費者の形に合わせて足す。**強調描画も下側である。**

### 11.2 Backend — プラットフォームのアクセシビリティ木は別シンク

`IAccessibilityBackend` を挟む。[`ICameraBackend`](23-camera-system.md) / [`IRenderBackend`](24-rendering-system.md) / `ISceneStreamingBackend` と同型であり、根拠も同じ（§24 D-3）:

> Backend 抽象の最大の価値は差し替え可能性ではなく、**高価な実装の着手を計測結果まで遅延できること**

アクセシビリティでは「高価で CI に載らない実装」が実機スクリーンリーダ・TTS・プラットフォーム差である。全部この向こうへ置く。

| Backend | 対象 | 検証 |
|---|---|---|
| `FakeAccessibilityBackend` | テスト | EditMode。**ポリシー層の受入はここで全部取る** |
| `UnityAssistiveSupportBackend` | Android / iOS / Windows / macOS（デスクトップは [Unity 6.3 以降](https://discussions.unity.com/t/native-desktop-screen-reader-support-now-available-in-unity-6-3/1681788)） | 実機のみ。CI 不可 |
| `DebugSocketBackend` | DebugStudio | プロトコル非変更の範囲 |

規約は §24 D-5 と同じ: **`AccessibilityNode` / `AccessibilityRole` 等の Unity 型を公開 API に一切漏らさない。**

そして**翻訳テスト**を課す — 本章の Kind / Flags / Label / 位置が、Backend DTO へ**片方向で落ちずに載る**こと（EditMode。射影先は表データで表現し、Unity 型に依存しない）。

これは「独自発明ではなく抽象である」ことの機械的な下限である。**上限ではない。** Unity の `AccessibilityRole` は Button / Toggle / Header / Slider 等の **UI ウィジェット分類**であり、World Kind（Actor / Passage / Surface）とは空間が違う。大半は `None` + label に落ち、**全単射にも無損失の逆射影にもならない**。AccessKit の role も同様に UI / ARIA 由来である。UI を本章のレジストリから外した（§5）以上、抽象の証明を UI 木への全単射に置かない。

UITK は自動でノードを生やさない（`rootVisualElement` を走査して作る必要がある）。したがって UI も Backend への投影であり、本章に UI 専用の登録経路を作らない。

### 11.3 供給しないもの

| 供給しない | 置き場 |
|---|---|
| 文面の生成とキュー | §31 |
| 優先度の調停 | §31。ただし段数は **2 段 + off** から始める（§3 の `aria-live` 先例） |
| 音・触覚の語彙設計と、その予算値 | Game。予算は実測で決める（§6.2） |
| 顕著性（今の少数を選ぶ規則） | Game |
| 到達可能性（W-7） | 供給元が存在しない（W-O-5） |
---

## 12. API スケッチ

今はコンパイルしない。

### 12.1 StableId

```csharp
public static class AccessibilityStableId
{
    public static string Combine(string sceneIdentity, string localId);
    public static bool TryGetSceneIdentity(string stableId, out ReadOnlySpan<char> sceneIdentity);
}
```

空 `localId` は例外。`TryGetSceneIdentity` があるので Descriptor は `OwningSceneIdentity` を持たない（§9.3）。

### 12.2 Authoring

```csharp
public sealed class AccessibilityAuthoring : MonoBehaviour, IAccessibilityPositionSource
{
    internal void Attach(IAccessibilityRegistry registry, string sceneIdentity);
    public AccessibilityDescriptor ToDescriptor(string sceneIdentity);
}
```

- Attach 前の OnEnable は自己登録しない
- 初期走査が Attach と Register を同時に行う
- 以降の OnEnable / OnDisable だけが個体の出入り
- OnDestroy で Unregister
- Cell は `AccessibilityKinds.Region` を identity から登録してよい

### 12.3 監査

```csharp
public static class AccessibilityAudit
{
    public static bool RequiresLabel(in AccessibilityDescriptor descriptor);
    public static bool HasMissingLabel(in AccessibilityDescriptor descriptor);
    public static bool HasInvalidHiddenCombo(AccessibilityFlags flags);
}
```

`HasInvalidHiddenCombo` は `Hidden | Focusable` と `Hidden | Announcable`。検出は実行時（inspector）。EditMode バリデータは後続。

**この監査が見つけられるのは「登録済みで不備があるもの」だけである**（§2）。作者付けの漏れは検出できない。

### 12.4 UI — 置かない

旧版にあった `BindAccessible(this VisualElement, …)` は**削除した**。UI は Backend への投影であり、本章に登録経路を持たない（§5、§11.2）。

削除の副次効果として、旧版が抱えていた次の不具合も消えた: `IDisposable` を誰が破棄するか未定義で、実行中に開閉するポップアップが登録を漏らし、**同じ UI を 2 回開くと重複 StableId で例外が飛んでいた**（[`UICommon.AddUIView`](../../OneStarMaker/Scripts/Runtime/UISystem/UICommon.cs) は Stable 中にも呼ばれる）。

---

## 13. 配置

| 層 | 置くもの | 置かないもの |
|---|---|---|
| Foundation | Kind, Flags, Descriptor, Handle, QueryFilter / QueryResult, StableId, Audit, `IAccessibilityBackend` | UITK、MonoBehaviour、TTS、Unity Transform、Unity Accessibility 型 |
| Runtime | Registry, Authoring, Transform 位置供給元、距離の前段関数、SceneBase 初期走査 | 顕著性方針、LLM、文面生成 |
| Runtime（Backend 実装） | `UnityAssistiveSupportBackend` | ポリシー判断 |
| Debug | `DebugSocketBackend`、inspector 表示（プロトコル非変更の範囲） | 世界モデルの正本 |
| Tests | `FakeAccessibilityBackend` | — |
| Game.Common | Kind の Game 域（1000–）、Flags bit 16–31 の名前、Label 文言 | FW 契約の再定義 |
| DependOnAll | レジストリ生成と注入、Backend の選択 | — |

---

## 14. 今やらない（実装）

| 項目 | 理由 |
|---|---|
| C# 一式 | 契約が読まれるまで型を増やさない。次は §15 |
| SceneBase / SceneDirector 改修 | S-2 |
| `Hint` | 説明的チャネルの消費者が未着手（§9.3） |
| `IAccessibilityText` / ローカライズ | D-8。消費者が現れてから |
| 方向クエリ・視線判定（W-5） | 供給元が無い（W-O-3）。S-1 の見積もりを超える |
| 到達可能性（W-7） | 供給元が無い（W-O-5） |
| 集約・階層（W-9） | 未決（W-O-1）。**帯域には最も効くので、後続で最初に検討する** |
| 変化フィード（W-8）と購読 API | §31 |
| 空間インデックス（近傍検索の高速化） | 線形走査で足りるかを計測してから。レジストリと距離前段が先 |
| セル YAML 焼き込み | 生成パイプラインを今動かさない |
| DebugSocket プロトコル変更 | スキーマ版付き surface。DebugStudio が対向。非変更の範囲に留める |
| TTS / ガイド / AI / 字幕 | §31。Phase 2 Sound 未着手 |
| 片手プロファイル | §32。InputManager 未着手 |
| プラットフォーム木への同期（`AssistiveSupport`） | Backend 実装。`FakeAccessibilityBackend` が先 |
| 連続量を記述子に載せる | D-16 |
| EditMode シーン監査 | 第一歩は実行時警告 |

---

## 15. 実装スライス

**S-1 に製品消費者は置かない。** 強調描画を第一消費者とする旧稿は、新設した規律「消費者が存在しない要求で契約を凍結しない」に自ら反する。根拠に使った [§24](24-rendering-system.md) は構想段階で、`IRenderBackend` はリポジトリに存在しない。

今日ホストがあるのは DebugStudio（プロトコル既存）と [§26](26-update-async-time-authority.md) の `UpdateLayer.SetTimeScale` だけである。前者のアクセシビリティ surface も、後者の未読量接続も未着手なので、**ホストがある ≠ 消費者がいる**。

したがって S-1 は **W-1 / W-2 / W-3 の番地**（Kind / 位置 / Handle）を EditMode で証明する。Label はデバッグ表示用のプレーン文字列として持ってよいが、文言解決器・loc・Hint・方向クエリ・変更フィード・強調描画は含めない。

最初の製品消費者はホストが実装されたときに選ぶ。暫定の候補順は DebugStudio inspector（ホストあり）→ 強調描画（§24 待ち。本番品質には W-5 が要る）。

**S-1（既存ファイルに触らない。Foundation + Runtime + Tests のみ）**

| # | 内容 | 受入 |
|---|---|---|
| 1 | Foundation の型（Kind / Flags / Descriptor / Handle / QueryFilter / QueryResult / StableId / Audit） | EditMode |
| 2 | Registry: Register の `out handle`、重複時の D-20 挙動、Set/Add/RemoveFlags、`Clear` してから詰める Query、`ExcludedFlags`、Scene 一括 Unregister の冪等 | EditMode |
| 3 | **世代の検証**: A を登録 → handle 取得 → Unregister → B を登録（同じ slot を再利用）→ **handleA での `SetFlags` が `false` を返し、B が不変であること** | EditMode。**必須** |
| 4 | `TryGetPosition`: 供給元を Register に載せ、**供給元が動いたら次の `TryGetPosition` が追随すること**、破棄済みで `false` になること | EditMode |
| 5 | 翻訳テスト: Kind / Flags / Label / 位置が Backend DTO へ**片方向で載ること**（射影先は表データ。Unity 型に依存しない）。`AccessibilityRole` への全単射は要求しない | EditMode |

規模の見積もり: Foundation ≈ 260 行 / Runtime ≈ 200 行（[`UpdateElementRegistry`](../../OneStarMaker/Scripts/Foundation/UpdateSystem/Elements/UpdateElementRegistry.cs) が 178 行）/ Tests ≈ 300 行。**既存ファイルの変更ゼロ。**

**S-2**

| # | 内容 |
|---|---|
| 6 | Authoring の Attach / OnEnable / OnDisable / OnDestroy と Scene 一括の冪等 |
| 7 | SceneBase 初期走査。LoadCanceled / Dispose / Bootstrap でも `UnregisterScene`。**走査の opt-in 可否を計測してから決める**（W-O-6） |
| 8 | Cell を `AccessibilityKinds.Region` として登録 |
| 9 | `FakeAccessibilityBackend` と `DebugSocketBackend`（プロトコル非変更の範囲） |
| 10 | 最初の製品消費者。ホストが実装されてから選ぶ。暫定は DebugStudio inspector、次点は §24 Policy の強調描画 |

受入の正は自動テスト。スクリーンリーダ実機は Cloud Agent では検証できない。

---

## 16. 撤退ライン

1. プラットフォームのアクセシビリティ木に識別の正本を移さない（D-1）。**Backend として使うことは撤退ではない**
2. `IAccessibilityBackend` を外して Unity 型を公開 API へ露出させない（D-18）
3. GameEntity 継承を復活させない（§5）
4. ジャンル固有 Kind を FW 予約域に足さない（D-2。Game 域に置く）
5. 座標を Descriptor に戻さない（D-11）
6. Handle を slot だけにしない（D-15。ABA）
7. 連続量を Identity に載せ始めてテレメトリバス化しない（D-16）
8. 即時チャネルと説明的チャネルを 1 本に混ぜない（D-17）

番地付け、Scene 一括解除、静的記述子と動的エントリの分離、slot + generation は撤退しても残す。

---

## 17. オープン論点

§6.4 の W-O-* が World 側の未決。ここには本章の実装上の未決を置く。

| # | 論点 | 現時点の見立て |
|---|---|---|
| O-1 | loc の key | 解決器の後ろで Label を key として読む。S-1 では入れない（D-8） |
| O-2 | 方位の語彙 | §31 のガイド実装時に時計回りで固定 |
| O-3 | 大量インスタンスの粒度 | Archetype 既定 + 近いものだけ。計測してから |
| O-4 | Hidden を Query 既定除外するか | しない。`ExcludedFlags` に消費者が入れる（§10） |
| O-5 | Kind の FW 予約域が 7 つで足りるか | 足りなければ追加。**Game 域があるので不足は事故にならない**（D-2）。7 種は推定であり実測ではない |
| O-6 | 破棄済み source を `TryGetHandle` に残す上限 | Scene 一括解除まで。リーク疑いが出たら期限を足す |
| O-7 | `Query` の結果順序 | 未定義（登録順）とする。距離順は呼び出し側が `TryGetPosition` で並べ替える |

（World 側の未決は §6.4 の W-O-* にある）

