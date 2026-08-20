# 30. 意味アイデンティティ層（Accessibility Identity）

> ステータス: **構想・契約固定（実装前）**（2026-08-20）
> [ARCHITECTURE.md](../../ARCHITECTURE.md) に戻る
> 関連: [05-scene.md](05-scene.md)、[06-ui.md](06-ui.md)、[07-09-services.md](07-09-services.md)、[21-scene-streaming.md](21-scene-streaming.md)、[24-rendering-system.md](24-rendering-system.md)

本章はジャンル（STG 含む）を前提にしない。画面読み上げ専用でもない。
**コードはまだ書かない。** 書くのは契約と、将来のフル実装を想定したうえで「今やらない」境界である。

---

## 目次

1. [一文](#1-一文)
2. [目的・スコープ](#2-目的スコープ)
3. [用語定義](#3-用語定義)
4. [なぜクラス継承ではないか](#4-なぜクラス継承ではないか)
5. [設計判断](#5-設計判断)
6. [寿命](#6-寿命)
7. [データ契約](#7-データ契約)
8. [レジストリと WorldSnapshot](#8-レジストリと-worldsnapshot)
9. [消費者のフル像](#9-消費者のフル像)
10. [API スケッチ](#10-api-スケッチ)
11. [配置](#11-配置)
12. [今やらない](#12-今やらない)
13. [実装に進むときの最小スライス](#13-実装に進むときの最小スライス)
14. [撤退ライン](#14-撤退ライン)
15. [オープン論点](#15-オープン論点)

---

## 1. 一文

ゲーム内の「もの」は表現（UI Toolkit の `VisualElement` / ワールドの `GameObject` / 将来の描画インスタンス）が違っても、**同じ記述子**を持つ。
読み上げ・字幕・ガイド・AI アシスタント・片腕用のフォーカス操作は、いずれもその記述子の**消費者**であり、系統ごとに名前や分類を持たない。

目的は、視覚に頼れない人でも世界を把握・判断でき、同時に両手を要求しない操作へ落とせるための、全オブジェクト共通の意味モデルを先に固定すること。

---

## 2. 目的・スコープ

**証明したいこと（将来）:**

- 意味のあるオブジェクトは、表現が違っても同一の Kind / Flags / Label / Hint で問える
- セルストリーミングで出入りしても、Scene 寿命と登録がずれない
- 読み上げ・ガイド・AI はピクセルではなくこのモデルを見る
- 片腕プレイは「同時入力の自由度を落とす」ことで成立し、レジストリの Focusable + Actionable がその候補集合になる

**本章が今やること:**

- 契約（記述子、寿命、消費者の読み方、却下理由）を文書化する

**本章が今やらないこと:**

- C# / UXML / シーン YAML / DebugSocket プロトコルの変更
- TTS、ガイド、AI、片手入力プロファイルの実装
- ローカライズ基盤の導入

詳細は [§12](#12-今やらない)。

---

## 3. 用語定義

| 用語 | 定義 |
|---|---|
| 記述子 (`AccessibilityDescriptor`) | 「これは何か / 何と呼ぶか / どの系統が扱ってよいか」の値型。正本 |
| Kind | ジャンル非依存の分類。Threat は「脅威」であり、特定ジャンルの敵ではない |
| Flags | ホットパス用ビット。Focusable / Announcable / Queryable / Hidden / Navigable / Actionable / Urgent |
| Label | 人向けの名前。読み上げとガイドの既定名詞。当面プレーン文字列。将来 loc key でも同じフィールド |
| Hint | 動作の短い説明（「調べる」「スタートする」）。プロンプトと発話の既定動詞句 |
| StableId | `{SceneIdentity}/{localId}`。Unity InstanceID / EntityId を外に出さない |
| レジストリ | App 常駐の登録表。Scene スコープで出し入れする |
| WorldSnapshot | 視点から切った「今、把握すべき世界」。全盲向け世界モデルの入力 |
| 読み上げ | Label / Hint を音声化するシンク。Urgent は割り込み |
| ガイド | Snapshot から機械的に状況を述べる層（近いもの、危険、行ける場所、方位） |
| AI アシスタント | Snapshot を見て要約・候補提示し、同意があれば Actionable を実行しうる消費者。特定 LLM に結合しない |
| 作者付け | 意味のあるオブジェクトへ記述子を載せる作業。未付けは「存在しない」 |

---

## 4. なぜクラス継承ではないか

「全オブジェクトが継承する」を `GameEntity : MonoBehaviour` で実装すると、今の OSM に届かない。

| 表現 | 実態 | 寿命の正本 |
|---|---|---|
| ワールド作者物 | Unity `GameObject`（共通基底は無い。`Prop_*` 等の名前規約） | 所属 Scene の `Stable`〜Unload |
| UI | `VisualElement`（`UIView` / `UIToolkitView`） | 所属 Scene の ViewIn〜ViewOut。GO は UICommon へ移る |
| 将来の大量描画 | GameObject を持たないインスタンス（[24-rendering-system.md](24-rendering-system.md)） | Archetype 既定値 + インスタンス Flags |

ここに継承ツリーを挿すと:

- 既存セル作者物を全部付け替える
- UI Toolkit と将来インスタンスに届かない
- OneStarMaker がゲーム固有型を知らない原則と衝突する

**実装形は Transform のような必須サイドカー＋同一記述子契約**である。表現ごとのアダプタが記述子を出し、レジストリが寿命を持つ。

---

## 5. 設計判断

### 5.1 決定事項

| # | 決定 | 根拠 |
|---|---|---|
| D-1 | **記述子が正本。** Unity `AccessibilityRole` / スクリーンリーダ API は UI シンクへの投影 | ワールド・インスタンス・ガイド・AI は UI Toolkit の a11y ツリーに載らない |
| D-2 | **契約は FW。Kind はジャンル非依存。** Game は Flags の上位ビットや解釈だけ足す | OneStarMaker はゲーム固有型を知らない。[ARCHITECTURE.md §2](../../ARCHITECTURE.md#2-レイヤー構造と-assembly-依存ルール) |
| D-3 | **登録寿命 = Scene `Stable`〜Unload。** 解除は Scene identity 単位の一括 | セルストリーミングで個体が大量に出入りする。[05-scene.md](05-scene.md) の寿命契約に乗せる |
| D-4 | **意味のある物は必須サイドカー。** 装飾は `Kind.Decorative` か未登録 | 全 GO 自動付与はノイズとストリーミングコスト。未ラベルは全盲にも AI にも存在しない |
| D-5 | **読み上げ・字幕・ガイド・AI は同じ Snapshot を読む** | 系統ごとに名前フィールドを増やすと、HUD・音・デバッグがすぐバラける |
| D-6 | **片腕は同時入力の自由度を落とす。** 手足は増やせない | 「自動で上手くプレイさせる」はアクセシビリティではない。できることは入力の同時性を減らすこと |
| D-7 | **アシスタントは判断材料をレジストリから得る。特定 LLM に結合しない** | モデルは差し替え対象。世界モデルは FW の契約 |
| D-8 | **Label はプレーン文字列。** 後で loc key に差し替え可能なフィールド名にする | 本 repo に localization は無い。今 loc 基盤を同時導入しない |
| D-9 | **ホットパスは Kind + Flags。** 文字列はコールド（発話・字幕・デバッグ） | 毎フレームの `string` 比較でフォーカス巡回や Snapshot を回さない |
| D-10 | **配線は手動 DI。** レジストリは App 常駐（Camera / AssetManagement と同格） | サービスロケータ禁止。[03-di.md](03-di.md) |

### 5.2 却下案

| 却下 | 理由 |
|---|---|
| `GameEntity : MonoBehaviour` を全オブジェクトに強制継承 | §4。UI と将来インスタンスに届かない |
| Unity Accessibility API をワールドの正本にする | UI 専用。3D・インスタンス・AI の入力にならない |
| 敵 / 弾 / 武器などジャンル固有 enum を Runtime に置く | FW が Game を知ることになる。Threat / Hazard / Collectible で足りる |
| 各 Scene サブクラスが手で `Register` する | 漏れの温床。SceneBase が走査し、Unload は identity 一括 |
| 全 GameObject に自動付与 | Ground / 無名メッシュまでレジストリに入り、Snapshot が読めなくなる |
| 系統ごとに DisplayName を持つ | D-5 の破壊。後から名前が揃わなくなる |
| 今 TTS / ガイド / チャットボットを実装する | Sound / Input は Phase 2 未着手。世界モデルが先 |
| 片腕を「全部自動化」で解く | プレイの主体を奪う。自由度を落とすのが上限 |

---

## 6. 寿命

Scene がオブジェクトの寿命の正本である（[05-scene.md](05-scene.md)）。記述子の登録もそれに従う。

```
Scene Loaded → Initialize（Root 取得）
  → ViewIn（UI がある場合）
  → Stable  … この時点で Authoring を走査して Register
  → PreUnloading … UnregisterScene(identity)
  → Unload
```

実装上の罠: [`SceneBase.ExecutePreUnLoad`](../../OneStarMaker/Scripts/Runtime/SceneSystem/SceneBase.cs) は `OnPreUnLoadedImpl` の**前に** `_rootObjects` を Clear する。
解除を「もう一度 GO を走査する」で書くと、Unload 時に対象が既に無い。
**解除の第一級 API は `UnregisterScene(sceneIdentity)`** であり、GO 走査ではない。

UI は ViewIn 後に UICommon 配下へ移るため、ワールド走査では拾えない。
UI の記述子は View が `BindAccessible` 相当で出し、`OwningSceneIdentity` を載せる。
Unload の一括解除が UI 分もまとめて消す。View 破棄時の個別 Unregister は冪等でよい。

セルストリーミング（[21-scene-streaming.md](21-scene-streaming.md)）では Cell の Stable / Unload がそのままレジストリの出入りになる。WSC の desired set にアクセシビリティ固有の判断を足さない。

---

## 7. データ契約

Foundation に置く純 C#。Unity UI に依存しない。位置は任意（UI は持たない）。

### 7.1 Kind

```csharp
public enum AccessibilityKind
{
    Unspecified = 0,
    Ui,            // 画面要素
    Character,     // プレイヤー / NPC 等、人格を持つもの
    Threat,        // 危害を加えうる対象（ジャンルの「敵」に限定しない）
    Ally,          // 味方・協力対象
    Interactable,  // 調べる / 開く / 話す
    Collectible,   // 拾う
    Hazard,        // 環境危険（穴、炎、毒域）
    Objective,     // 目的地・目標
    Region,        // セル / 部屋 / エリア
    System,        // HUD アナウンス、ローディング
    Decorative,    // 登録はするが通常クエリから除外してよい
}
```

Game 層が「ボス」「弾」を足したくなったら、Kind を増やさず Flags 上位や Game 側の解釈テーブルで足す（D-2）。

### 7.2 Flags

```csharp
[Flags]
public enum AccessibilityFlags : uint
{
    None        = 0,
    Focusable   = 1 << 0,  // UI フォーカス / ワールドの巡回候補
    Announcable = 1 << 1,  // 読み上げ・字幕の対象
    Queryable   = 1 << 2,  // Snapshot / 空間検索の対象
    Hidden      = 1 << 3,  // 存在するが通常は黙る
    Navigable   = 1 << 4,  // 行先になりうる
    Actionable  = 1 << 5,  // 決定 1 操作の対象
    Urgent      = 1 << 6,  // 読み上げを割り込んでよい
}
```

### 7.3 Descriptor

```csharp
public readonly struct AccessibilityDescriptor
{
    public string StableId { get; }
    public string OwningSceneIdentity { get; }
    public AccessibilityKind Kind { get; }
    public AccessibilityFlags Flags { get; }
    public string Label { get; }
    public string? Hint { get; }
    public bool HasWorldPosition { get; }
    public float WorldX { get; }
    public float WorldY { get; }
    public float WorldZ { get; }
}
```

`StableId` は `{SceneIdentity}/{localId}`。InstanceID 禁止（DebugSocket の NodeToken と同型の発想）。

Label が空で、かつ Focusable / Announcable / Actionable のいずれかが立っているものは**作者付け不備**。監査の対象。Decorative や Hidden の空 Label は許容してよい。

文字列比較はコールドに閉じる（D-9）。フォーカス巡回や Snapshot の一次フィルタは Kind + Flags。

---

## 8. レジストリと WorldSnapshot

### 8.1 Registry

App 常駐。CameraSystem / AssetManagement と同格。SceneDirector が SceneBase へ注入する（手動 DI、D-10）。

```csharp
public interface IAccessibilityRegistry
{
    void Register(in AccessibilityDescriptor descriptor);
    bool Unregister(string stableId);
    void UnregisterScene(string sceneIdentity);
    bool TryGet(string stableId, out AccessibilityDescriptor descriptor);
    IReadOnlyList<AccessibilityDescriptor> Query(
        AccessibilityKind? kind = null,
        AccessibilityFlags requiredFlags = AccessibilityFlags.None);
}
```

- 同一 `StableId` の二重 Register は失敗（作者付けバグを隠さない）
- `Unregister` / `UnregisterScene` は冪等
- 未注入時は Null Object（テストと未配線経路を壊さない）

### 8.2 WorldSnapshot（将来。今は型の意味だけ固定する）

視点（プレイヤーまたはカメラ位置）から、近いノードを方向・距離・Kind・Label・Hint・Actionable 付きで切った読み取り専用の切断面。

これが全盲向け世界モデルである。読み上げ・ガイド・AI は Snapshot を入力にし、各自が世界を再スキャンしない。

含めるもの:

- 視点位置
- ノード: StableId, Kind, Flags, Label, Hint, 距離, 相対方位（時計回り 12 分割など）
- 今いる Region（セル identity と、あれば Label）
- 冗長さ方針（短い / 普通 / 詳しい）は Snapshot 自体ではなく、それを読む Policy が持つ

持たないもの:

- メッシュやテクスチャ
- 「どう聞こえるか」「どう話すか」の文面生成（それは読み上げ / ガイド / AI の責務）

空間インデックス（距離クエリの高速化）は、数が計測で問題になってから足す。初期は Scene 単位の線形 Query で足りる想定。

---

## 9. 消費者のフル像

すべて今は実装しない。読み方だけを固定する。各系統は記述子を読むだけで、独自の名前フィールドを増やさない（D-5）。

```
Descriptor ──► Registry ──► Snapshot ──► 読み上げ
                                   ├──► 字幕
                                   ├──► ガイド
                                   └──► AI アシスタント
Descriptor ──► UITK / Unity Accessibility（UI シンク）
Registry   ──► 片腕フォーカス巡回（Focusable + Actionable）
```

### 9.1 読み上げ（TTS）

UI と世界の Label / Hint を音声化する。Urgent は割り込み、それ以外はキュー。

- 入力: Descriptor（UI 単体）および Snapshot（世界）
- 出力: SoundService 配下の音声シンク（[07-09-services.md](07-09-services.md) §7。Phase 2 未着手）
- Unity スクリーンリーダは UI の補助であり、ワールド読み上げの正本ではない（D-1）

今やらない。TTS エンジン選定も今しない。

### 9.2 字幕

Announcable の別シンク。読み上げと同じ文面ソース（Label / Hint / ガイド発話）を文字で出す。話者名の既定はオブジェクトの Label。

今やらない。`TitleViewModel.SubtitleText` はタイトルの添え字であり、字幕パイプラインではない。

### 9.3 ガイド

Snapshot から機械的に述べる層。AI を必要としない。

例: 「左 9 時、近い。扉。調べる」「前方に危険」「今は Cell_1_2」

- 入力: Snapshot + 冗長さ方針
- 方位は時計回りなど、視覚に依存しない語彙
- Region 入場はセル Stable に乗せて一度だけ言える

今やらない。

### 9.4 AI アシスタント

Snapshot を見て状況を要約し、候補を提示する。同意があれば Actionable を実行しうる。

| 規則 | 意味 |
|---|---|
| 世界の入力は Snapshot（とレジストリ）だけ | ピクセル OCR や画面スクショを正にしない |
| ラベルの無いオブジェクトは存在しない | 作者付けが必須になる理由そのもの |
| モデルは差し替え | FW はプロンプト契約と Snapshot の形だけ持つ（D-7） |
| 実行は同意のうえ Actionable に限る | ガイドが述べ、アシスタントが選ぶ。勝手に世界を改変しないのが既定 |

今やらない。結合点（Snapshot → 提案 → Actionable 実行）だけをここに残す。チャット UI も今作らない。

### 9.5 片腕

プログラムでできることは少ない。第二の手は作れない。できるのは **同時に要求する自由度を落とす** こと（D-6）。

| 手段 | 効果 | 置き場所 |
|---|---|---|
| コード入力（移動 + 視点 + アクション同時）を禁止するプロファイル | 片手で足りる同時性にする | InputManager（Phase 2） |
| ホールドをトグルに | 押しっぱなし用の第二指を要求しない | 同上 |
| フルリマップ | マウスのみ / スティック + 肩ボタン等 | 同上 |
| Focusable + Actionable を巡回し、決定は 1 ボタン | 狙いと動作を同時にやらない | レジストリが候補集合。入力は InputManager |
| UI は初期フォーカスと Tab / 決定 / キャンセル | コード操作必須にしない | UISystem + 記述子の Focusable |

「全部自動で上手くやる」は却下（§5.2）。アシスタントの代理実行は片腕の代替ではなく、全盲側の任意機能である。混ぜない。

今やらない。入力プロファイル本体は [07-09-services.md](07-09-services.md) §8 の InputManager 側。

### 9.6 UI フォーカス / スクリーンリーダ

記述子を UITK の `focusable` / `tabIndex` / `tooltip` と、モジュール既存の `UnityEngine.Accessibility` へ投影する。ダイアログは初期フォーカスとキャンセル Hint を持つ。

これも将来実装。本章では「UI も同じ記述子を持つ」ことだけを固定する。uGUI レガシー（DebugProfilerView）は対象外でよい。

### 9.7 その他（同様に今やらない）

| 消費者 | 読み方 |
|---|---|
| 空間音 | 位置付き Descriptor にキューを置く。読み上げの代替ではなく、方向の補助 |
| 色覚 / ハイコントラスト | 色は記述子に持たない。Kind → パレット |
| レーダー / マップ | Kind + Flags のブリップ。色はパレット側 |
| インタラクトプロンプト | Hint + 入力の表示名。「何に対して何をするか」 |
| DebugSocket inspector | Kind / Label を出し、Focusable な未ラベルを警告 |

RenderingSystem の動的インスタンスは、Archetype 既定 Descriptor + インスタンス Flags でレジストリへ載せる。GO が無いことは記述子契約の例外にしない。

---

## 10. API スケッチ

実装時の形。今はコンパイルしない。

### 10.1 StableId

```csharp
public static class AccessibilityStableId
{
    public static string Combine(string sceneIdentity, string localId);
}
```

`localId` はシーン内で一意。作者が空なら GameObject 名にフォールバックしてよいが、改名に弱いので Authoring に明示する方が正。

### 10.2 Authoring（ワールド）

```csharp
public sealed class AccessibilityAuthoring : MonoBehaviour
{
    // localId, Kind, Flags, Label, Hint
    public AccessibilityDescriptor ToDescriptor(string sceneIdentity);
}
```

SceneBase が Stable で Root 配下を走査して Register する。Cell 自体は `Kind.Region` を identity から登録してよい（セルはエリアである）。

### 10.3 UI 投影

```csharp
// BindingExtensions
public static IDisposable BindAccessible(
    this VisualElement element,
    in AccessibilityDescriptor descriptor,
    IAccessibilityRegistry? registry = null);
```

UITK へ投影し、registry があれば Register、Dispose で Unregister（冪等）。

### 10.4 監査

```csharp
public static class AccessibilityAudit
{
    public static bool RequiresLabel(in AccessibilityDescriptor descriptor);
    public static bool HasMissingLabel(in AccessibilityDescriptor descriptor);
}
```

Focusable / Announcable / Actionable の空 Label を不備とする。

---

## 11. 配置

| 層 | 置くもの | 置かないもの |
|---|---|---|
| Foundation | Kind, Flags, Descriptor, StableId, Audit | UITK、MonoBehaviour、TTS |
| Runtime | Registry, Authoring, SceneBase 走査, BindAccessible | ジャンル固有 Kind、LLM クライアント |
| Debug | inspector への Kind / Label / 未ラベル警告 | 世界モデルの正本 |
| Game.Common | Flags 上位の解釈、タイトル固有の Label 文言 | FW 契約の再定義 |
| DependOnAll | レジストリ生成と SceneDirector への注入 | — |

InGame と OutGame が横断して記述子型を定義しない。共有は Foundation / Runtime、文言は各 Scene 同居（[27-folder-structure.md](27-folder-structure.md)）。

---

## 12. 今やらない

本章の成果物は設計書と ARCHITECTURE.md の目次行だけである。

| 項目 | 理由 |
|---|---|
| C#（Descriptor / Registry / Authoring / BindAccessible） | 契約が読まれるまで型を先に増やさない |
| SceneBase / SceneDirector / AppInitializer の改修 | 寿命フックは文書化した。実装は次スライス |
| 既存 4 画面の UITK 配線 | 投影は §10.3 のスケッチに留める |
| セル YAML へのコンポーネント焼き込み | 生成パイプラインを今動かさない |
| TTS / SoundService | Phase 2 未着手 |
| ガイド / WorldSnapshot 実装 | レジストリが先 |
| AI アシスタント / チャット UI / LLM 結合 | D-7。結合点だけ §9.4 |
| 片手入力プロファイル / リマップ UI | InputManager 未着手 |
| 字幕パイプライン | 読み上げと同じソース契約だけ固定 |
| ローカライズ | D-8 |
| 色覚パレット / レーダー / 空間音 | 消費者として読むだけと決めた |
| Unity Accessibility 3D ヒエラルキー | D-1 |
| RenderingSystem インスタンス登録 | 描画構想（§24）側の実装に従う |

---

## 13. 実装に進むときの最小スライス

設計が通った後の第一歩。TTS・ガイド・AI・片手プロファイルは第一歩にも入れない。

1. Foundation の Descriptor / Kind / Flags / StableId / Audit と EditMode テスト
2. Runtime の Registry（Scene 一括 Unregister、二重 Register 失敗、Null Object）
3. SceneBase: Stable で Authoring 走査、PreUnload で `UnregisterScene`（Root Clear より前に identity 解除）
4. `BindAccessible` の UITK 投影（tooltip / focusable）。既存画面への適用は小さく
5. Cell を `Kind.Region` として登録
6. DebugSocket inspector に Kind / Label、Focusable な未ラベル警告

受入の正は自動テスト（値型、寿命、投影のプロパティ）とする。VoiceOver / Narrator の実機は Cloud Agent では検証できない。

---

## 14. 撤退ライン

1. **Unity Accessibility だけに寄せたくなった場合**  
   UI シンクとしては残してよい。ワールド・Snapshot・AI の正本をそこに移さない（D-1）。移した瞬間、全盲向け世界モデルが画面の外で死ぬ。

2. **GameEntity 継承を復活させたくなった場合**  
   アダプタ（Authoring / BindAccessible / Archetype 既定値）が 3 表現をカバーしている限り、継承ツリーは増やす必要が無い。1 表現にしか効かない基底クラスは却下のまま。

3. **ジャンル固有 Kind を FW に足したくなった場合**  
   Game 側の解釈テーブルへ逃がす（D-2）。FW の Kind が増えるのは、表現を問わずどのゲームでも使う分類（新しい「もの」の種類）に限る。

4. **アシスタントを正の操作系にしたくなった場合**  
   既定は提案まで。実行は同意 + Actionable。片腕の代替にしない（§9.5）。

契約（記述子が正本、Scene 寿命、Snapshot が全盲向け入力、片腕は同時性を落とす）は撤退しても残す。

---

## 15. オープン論点

| # | 論点 | 現時点の見立て |
|---|---|---|
| O-1 | loc 導入時に Label を key にするか、別フィールドにするか | 同じフィールドを key に差し替える方が D-5 を壊しにくい。導入時に決める |
| O-2 | Snapshot の方位語彙（時計回り / 左右前後 / コンパス） | 視覚に依存しないなら時計回りが無難。ガイド実装時に 1 つに固定する |
| O-3 | 大量インスタンスをレジストリに載せる粒度 | Archetype 既定 + 近いものだけ実体化、が有力。計測してから |
| O-4 | Hidden を Query 既定で除外するか | 呼び出し側が Flags で指定する方が隠さない。Policy が Hidden を落とす |
| O-5 | UI の StableId を UXML `name` にするか明示するのか | 初期は `name` フォールバック、衝突したら明示 |
| O-6 | アシスタントの同意 UI | ダイアログ既存パターン（ConfirmDialog）で足りるかは、実行を足すときに見る |
