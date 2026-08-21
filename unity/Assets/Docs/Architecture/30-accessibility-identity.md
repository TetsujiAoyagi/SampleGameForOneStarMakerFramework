# 30. 意味アイデンティティ層（Accessibility Identity）

> ステータス: **構想・契約固定（実装前）**（2026-08-21）
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

**コードを今書かないこと** は [§12](#12-今やらない)。TTS / ガイド / AI / 片手プロファイルの実装もそこに含まれる。

---

## 3. 用語定義

| 用語 | 定義 |
|---|---|
| 記述子 (`AccessibilityDescriptor`) | 静的な意味の値型。Kind / Label / Hint / 既定 Flags。位置は持たない |
| エントリ | レジストリ内の実行時レコード。記述子 + 動的 Flags + 位置の供給元 |
| Kind | ガイドが最初に言う名詞。単一値。Threat は「脅威」であり、特定ジャンルの敵ではない |
| Flags | ホットパス用ビット。側面（Actionable 等）と動的状態を載せる |
| Label | 人向けの名前。読み上げとガイドの既定名詞。解決器を通す（既定は素通し） |
| Hint | 動作の短い説明（「調べる」「スタートする」）。プロンプトと発話の既定動詞句 |
| StableId | `{SceneIdentity}/{localId}`。Unity InstanceID / EntityId を外に出さない。`localId` は必須 |
| レジストリ | App 常駐の登録表。Scene スコープで出し入れする |
| WorldSnapshot | 視点から切った「今、把握すべき世界」。切る瞬間に位置供給元を読む |
| 読み上げ | Label / Hint を音声化するシンク。Urgent は割り込み |
| ガイド | Snapshot から機械的に状況を述べる層（近いもの、危険、行ける場所、方位） |
| AI アシスタント | Snapshot を見て要約・候補提示し、同意があれば Actionable を実行しうる消費者。特定 LLM に結合しない |
| 作者付け | 意味のあるオブジェクトへ記述子を載せる作業。未付けは「存在しない」 |

---

## 4. なぜクラス継承ではないか

「全オブジェクトが継承する」を `GameEntity : MonoBehaviour` で実装すると、今の OSM に届かない。

| 表現 | 実態 | 登録のタイミング | 解除の粒度 |
|---|---|---|---|
| ワールド作者物 | Unity `GameObject`（共通基底は無い） | Scene Stable の初期走査 + 以降は `OnEnable` / 明示 Register | Scene identity 一括。個体は Disable / Unregister |
| UI | `VisualElement`（`UIView` / `UIToolkitView`） | ViewIn（Initializing。Stable より前） | 同上。View 破棄時の個別 Unregister は冪等 |
| 将来の大量描画 | GameObject を持たないインスタンス（[24-rendering-system.md](24-rendering-system.md)） | 生成時。Archetype 既定値 + インスタンス Flags | 破棄時 / Scene 一括 |

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
| D-2 | **契約は FW。Kind はジャンル非依存。** Game 拡張は Flags の予約域（bit 16–31） | Foundation の enum に Game は名前付きメンバを足せない。生キャストを予約域に閉じる |
| D-3 | **解除の粒度は Scene identity 一括。** 登録タイミングは表現ごと（§4 の表） | 「登録寿命 = Stable〜Unload」だと UI の ViewIn（Initializing）が偽になる |
| D-4 | **既定は未登録。** `Kind.Decorative` は「在るが黙る」と明示したいときだけ | 2 択の基準が無いと監査が揺れる。全 GO 自動付与は Snapshot を壊す |
| D-5 | **読み上げ・字幕・ガイド・AI は同じ Snapshot を読む** | 系統ごとに名前フィールドを増やすと、HUD・音・デバッグがすぐバラける |
| D-6 | **片腕は同時入力の自由度を落とす。** 手足は増やせない | 「自動で上手くプレイさせる」はアクセシビリティではない |
| D-7 | **アシスタントは判断材料をレジストリから得る。特定 LLM に結合しない** | モデルは差し替え対象。世界モデルは FW の契約 |
| D-8 | **Label はプレーン文字列。** 消費者は `IAccessibilityText` 相当の解決器を通す（既定は素通し） | loc 基盤は今入れない。ただし Label を表示文字列として直接消費すると、key 化のとき全消費者を壊す |
| D-9 | **ホットパスは Kind + Flags。** 文字列はコールド（発話・字幕・デバッグ） | 毎フレームの `string` 比較でフォーカス巡回や Snapshot を回さない |
| D-10 | **配線は手動 DI。** レジストリは App 常駐。省略時のサービスロケータは禁止 | [03-di.md](03-di.md)。`BindAccessible` の registry は必須引数 |
| D-11 | **静的な意味と動的な状態を分ける。** 位置は供給元、Flags は `SetFlags` | 記述子に座標を焼くと Snapshot の距離が追従しない。二重 Register 禁止と両立しない |
| D-12 | **Kind はガイドが最初に言う名詞（単一）。** 側面は Flags | 「Threat かつ Collectible」を Kind 二つで書くと発話が決まらない。選ぶ規則を作者に任せる方が D-5 を壊す |
| D-13 | **Query は呼び出し側バッファを埋める。** 詳細はハンドル + `TryGet` | `IReadOnlyList<Descriptor>` を毎回 new すると、API 形状が後から変えられない |

### 5.2 却下案

| 却下 | 理由 |
|---|---|
| `GameEntity : MonoBehaviour` を全オブジェクトに強制継承 | §4。UI と将来インスタンスに届かない |
| Unity Accessibility API をワールドの正本にする | UI 専用ではないが、ヒエラルキは別シンク。Snapshot / AI の正本にしない |
| 敵 / 弾 / 武器などジャンル固有 enum を Runtime に置く | FW が Game を知ることになる。Threat / Hazard / Collectible で足りる |
| **各 Scene サブクラスが手で Register するのを唯一の入口にする** | 漏れの温床。初期集合は SceneBase 走査。動的な出入りは Authoring の Enable / 生成側の明示 Register |
| 全 GameObject に自動付与 | Ground / 無名メッシュまでレジストリに入り、Snapshot が読めなくなる |
| 系統ごとに DisplayName を持つ | D-5 の破壊 |
| 今 TTS / ガイド / チャットボットを実装する | Sound / Input は Phase 2 未着手。世界モデルが先 |
| 片腕を「全部自動化」で解く | プレイの主体を奪う。自由度を落とすのが上限 |
| 座標を Descriptor に焼いて Register し直す | D-11。動く対象と二重 Register 禁止が衝突する |
| `BindAccessible` の registry を省略可能にする | 省略時に既定インスタンスを引く実装が D-10 を破る |
| GameObject 名を `localId` の既定にする | 同名が並ぶと StableId が衝突し、二重 Register 失敗を踏む |

---

## 6. 寿命

Scene がオブジェクトの寿命の正本である（[05-scene.md](05-scene.md)）。**解除**はそれに従う。**登録**は表現ごとにずれる（D-3）。

```
Scene Loaded → Initialize（Root 取得）
  → ViewIn（Initializing） … UI はここで Register
  → Stable                 … ワールド Authoring の初期走査（非アクティブ含む）
  → （実行中）             … 生成物は OnEnable / 明示 Register
  → PreUnloading / LoadCanceled / Dispose … UnregisterScene(identity)（冪等）
  → Unload
```

実装上の罠: [`SceneBase.ExecutePreUnLoad`](../../OneStarMaker/Scripts/Runtime/SceneSystem/SceneBase.cs) は `OnPreUnLoadedImpl` の**前に** `_rootObjects` を Clear する。
解除を「もう一度 GO を走査する」で書くと、Unload 時に対象が既に無い。
**解除の第一級 API は `UnregisterScene(sceneIdentity)`** であり、GO 走査ではない。Clear との前後は関係ない。

レジストリは App 常駐なので、通常 Unload 以外でも残る。次も必ず `UnregisterScene` する（冪等）:

- `LoadCanceled`（Stable 前に落ちる経路。[05-scene.md](05-scene.md) の 14 状態）
- `SceneBase.Dispose`
- Editor 停止 / Domain Reload 無効時の Bootstrap クリーンアップ

UI は ViewIn 後に UICommon 配下へ移るため、ワールド走査では拾えない。UI の記述子は View が `BindAccessible` で出し、`OwningSceneIdentity` を載せる。Unload の一括解除が UI 分も消す。View 破棄時の個別 Unregister は冪等。

初期走査は `GetComponentsInChildren<T>(includeInactive: true)`。既定引数だと非アクティブ配下を落とす（`SearchUIView` と同じ罠）。
走査は**初期集合の供給**に限る。以降の出入りは `AccessibilityAuthoring` の `OnEnable` / `OnDisable`（または生成側の明示 Register / Unregister）が正。Scene 一括解除と個体 Disable が重なってもよい（冪等）。

セルストリーミング（[21-scene-streaming.md](21-scene-streaming.md)）では Cell の Stable / Unload がそのままレジストリの出入りになる。WSC の desired set にアクセシビリティ固有の判断を足さない。

---

## 7. データ契約

Foundation に置く純 C#。Unity UI に依存しない。

### 7.1 Kind（単一。ガイドの第一名詞）

```csharp
public enum AccessibilityKind
{
    Unspecified = 0,
    Ui,
    Character,
    Threat,
    Ally,
    Interactable,
    Collectible,
    Hazard,
    Objective,
    Region,
    System,
    Decorative,
}
```

複数に見えるものは **Kind を一つ選ぶ**（ガイドが最初に言う語）。残りは Flags。例: 拾える脅威は `Kind.Threat` + 側面としての Actionable。選ぶ基準は「全盲のガイド文で先に言いたいか」。

Game 層が「ボス」「弾」を足したくなったら Kind を増やさない（D-2, D-12）。

### 7.2 Flags

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
    Urgent      = 1 << 6,
    // bit 7–15: FW 予約（未使用）
    // bit 16–31: Game 専用。Foundation は解釈しない
}
```

`Hidden` は読み上げ・ガイド・フォーカス巡回から除外する。`Hidden | Focusable` は作者付け不備（監査対象）。Query 自体は Hidden を落とさない。Policy が落とす（O-4）。

動的に変わる Flags（施錠、撃破、達成）は記述子を焼き直さず `SetFlags` する（D-11）。

### 7.3 Descriptor（静的）

位置を持たない。Register 時にコピーされる意味のスナップショット。

```csharp
public readonly struct AccessibilityDescriptor
{
    public string StableId { get; }
    public string OwningSceneIdentity { get; }
    public AccessibilityKind Kind { get; }
    public AccessibilityFlags DefaultFlags { get; }
    public string Label { get; }
    public string? Hint { get; }
}
```

`StableId` は `{SceneIdentity}/{localId}`。`localId` は必須。GameObject 名フォールバックはしない。

Label が空で、かつ Focusable / Announcable / Actionable のいずれかが立っているものは作者付け不備。Decorative の空 Label は許容。Hidden の空 Label は Hidden が優先されるので監査対象外。

### 7.4 位置の供給元

```csharp
public interface IAccessibilityPositionSource
{
    bool TryGetWorldPosition(out float x, out float y, out float z);
}
```

ワールド Authoring は Transform を包む。UI と位置を持たない System は source 無し（`HasWorldPosition == false`）。Snapshot は切る瞬間に `TryGet` する。Register 時の座標は使わない。

GO を持たないインスタンスは、シミュレーション側の座標関数を source にする。今その実装はしない。

### 7.5 文言の解決器

```csharp
public interface IAccessibilityText
{
    string Resolve(string labelOrKey);
}
```

既定実装は素通し。loc 導入後に差し替える。読み上げ・字幕・ガイド・AI・UITK tooltip は Label / Hint を直接表示せず、これを通す（D-8）。解決器本体とテーブルは今作らない。

---

## 8. レジストリと WorldSnapshot

### 8.1 Registry

App 常駐。CameraSystem / AssetManagement と同格。SceneDirector が SceneBase / UIView へ注入する（D-10）。

```csharp
public interface IAccessibilityRegistry
{
    // 重複 StableId は InvalidOperationException。Null Object は無視して false。
    bool Register(
        in AccessibilityDescriptor descriptor,
        IAccessibilityPositionSource? positionSource = null);

    void SetFlags(string stableId, AccessibilityFlags flags);

    bool Unregister(string stableId);
    void UnregisterScene(string sceneIdentity);

    bool TryGet(string stableId, out AccessibilityHandle handle);
    int Query(
        List<AccessibilityHandle> results,
        AccessibilityKind? kind = null,
        AccessibilityFlags requiredFlags = AccessibilityFlags.None);
}

public readonly struct AccessibilityHandle
{
    public string StableId { get; }
    public AccessibilityKind Kind { get; }
    public AccessibilityFlags Flags { get; }
}
```

- `Unregister` / `UnregisterScene` / `SetFlags` 対象なし は冪等（SetFlags は no-op）
- Null Object: Register は `false`、Query は 0、例外を投げない
- 本番の重複は例外。作者付けバグを隠さない
- Handle は Kind + Flags + id。Label が要るときだけ `TryGet` から Descriptor を取る（D-9, D-13）

Descriptor の詳細取り出しは `TryGetDescriptor` を足してよい。Handle と Descriptor を混同して Query が文字列 4 本をコピーしない。

### 8.2 WorldSnapshot（将来。今は型の意味だけ固定する）

視点から、近いノードを方向・距離・Kind・Label・Hint・Actionable 付きで切った読み取り専用の切断面。**切る瞬間に位置供給元を読む。**

これが全盲向け世界モデルである。読み上げ・ガイド・AI は Snapshot を入力にし、各自が世界を再スキャンしない。

含めるもの:

- 視点位置
- ノード: StableId, Kind, その時点の Flags, 解決済み Label / Hint, 距離, 相対方位
- 今いる Region
- 冗長さ方針は Snapshot ではなく Policy が持つ

持たないもの:

- メッシュやテクスチャ
- 文面生成（読み上げ / ガイド / AI の責務）

空間インデックスは数が計測で問題になってから足す。初期は線形 Query で足りる想定。**アロケーション形状（呼び出し側バッファ）は今決める**（D-13）。インデックス自体は今作らない。

---

## 9. 消費者のフル像

すべて今は実装しない。読み方だけを固定する。各系統は記述子を読むだけで、独自の名前フィールドを増やさない（D-5）。

```
Descriptor ──► Registry ──► Snapshot ──► 読み上げ
                                   ├──► 字幕
                                   ├──► ガイド
                                   └──► AI アシスタント
Descriptor ──► UITK 投影（focusable / tooltip）
Descriptor ──► AssistiveSupport ヒエラルキ（別シンク）
Registry   ──► 片腕フォーカス巡回（Focusable + Actionable、Hidden 除外）
```

### 9.1 読み上げ（TTS）

UI と世界の Label / Hint を、解決器を通して音声化する。Urgent は割り込み、それ以外はキュー。

今やらない。TTS エンジン選定もしない。SoundService は Phase 2 未着手。

### 9.2 字幕

Announcable の別シンク。読み上げと同じ文面ソース。話者名の既定は Label。

今やらない。`TitleViewModel.SubtitleText` はタイトルの添え字であり、字幕パイプラインではない。

### 9.3 ガイド

Snapshot から機械的に述べる層。AI を必要としない。

今やらない。

### 9.4 AI アシスタント

Snapshot を見て状況を要約し、候補を提示する。同意があれば Actionable を実行しうる。世界の入力は Snapshot（とレジストリ）だけ。ラベルの無いオブジェクトは存在しない。

今やらない。結合点だけ残す。チャット UI も今作らない。

### 9.5 片腕

第二の手は作れない。できるのは同時入力の自由度を落とすこと（D-6）。Focusable + Actionable を巡回し、決定は 1 ボタン。Hidden は巡回から除外。

今やらない。入力プロファイル本体は [07-09-services.md](07-09-services.md) §8。

### 9.6 UI フォーカス / スクリーンリーダ

二つは別シンクである。まとめて「Unity Accessibility に投影」と書かない。

| シンク | 役割 | 対象 |
|---|---|---|
| UITK `focusable` / `tabIndex` / `tooltip` | キーボード / ゲームパッド操作とヒント | 全プラットフォーム。片腕の UI 経路 |
| `AssistiveSupport` + `AccessibilityHierarchy` | OS スクリーンリーダ（TalkBack / VoiceOver / Narrator） | Android / iOS / Windows / macOS。自動では UITK から生えない |

ワールドをスクリーンリーダへ出すなら AssistiveSupport のノードを自分で同期する。それは Snapshot / ガイドの代替ではない（D-1）。

今やらない。uGUI レガシー（DebugProfilerView）は対象外。

### 9.7 その他（同様に今やらない）

| 消費者 | 読み方 |
|---|---|
| 空間音 | 位置供給元からキューを置く |
| 色覚 / ハイコントラスト | 色は記述子に持たない。Kind → パレット |
| レーダー / マップ | Kind + Flags のブリップ |
| インタラクトプロンプト | Hint + 入力の表示名 |
| DebugSocket inspector | Kind / Label。実行時に未ラベルを警告 |

RenderingSystem の動的インスタンスは、Archetype 既定 Descriptor + インスタンス Flags + 位置供給元でレジストリへ載せる。GO が無いことは例外にしない。

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

`localId` 空は例外。シーン内で一意であることは作者の責務。衝突は Register が例外で顕在化する。

### 10.2 Authoring（ワールド）

```csharp
public sealed class AccessibilityAuthoring : MonoBehaviour, IAccessibilityPositionSource
{
    // localId（必須）, Kind, DefaultFlags, Label, Hint
    public AccessibilityDescriptor ToDescriptor(string sceneIdentity);
}
```

- Scene Stable: 初期走査（`includeInactive: true`）で Register
- 以降: `OnEnable` / `OnDisable` が個体の出入り
- Cell 自体は `Kind.Region` を identity から登録してよい

### 10.3 UI 投影

```csharp
public static IDisposable BindAccessible(
    this VisualElement element,
    in AccessibilityDescriptor descriptor,
    IAccessibilityRegistry registry);
```

`registry` は必須。View が Scene から受け取ったインスタンスを渡す。UITK へ投影し、Register、Dispose で Unregister（冪等）。

### 10.4 監査

```csharp
public static class AccessibilityAudit
{
    public static bool RequiresLabel(in AccessibilityDescriptor descriptor);
    public static bool HasMissingLabel(in AccessibilityDescriptor descriptor);
    public static bool HasInvalidHiddenFocus(AccessibilityFlags flags);
}
```

純関数。第一歩の検出場所は **実行時**（DebugSocket inspector）。EditMode のシーン走査バリデータは、セル YAML を触らない方針と衝突するので後続。実行時にしか分からない、と割り切る。

---

## 11. 配置

| 層 | 置くもの | 置かないもの |
|---|---|---|
| Foundation | Kind, Flags, Descriptor, Handle, StableId, Audit, `IAccessibilityText` | UITK、MonoBehaviour、TTS、位置の Unity 依存実装 |
| Runtime | Registry, Authoring, PositionSource（Transform）, SceneBase 初期走査, BindAccessible | ジャンル固有 Kind、LLM クライアント |
| Debug | inspector への Kind / Label / 未ラベル警告 | 世界モデルの正本 |
| Game.Common | Flags bit 16–31 の名前, タイトル固有の Label 文言 | FW 契約の再定義 |
| DependOnAll | レジストリ生成と SceneDirector への注入 | — |

---

## 12. 今やらない（実装）

文書（本章）は既にある。この表は **コードとして今書かないもの**。実装スライスが立っても、ここに残る行は「まだやらない」。

| 項目 | 理由 |
|---|---|
| C#（Descriptor / Registry / Authoring / BindAccessible） | 契約が読まれるまで型を先に増やさない。次は §13 |
| SceneBase / SceneDirector / AppInitializer の改修 | 寿命フックは文書化した |
| 既存 4 画面の UITK 配線 | 投影は §10.3 のスケッチに留める |
| セル YAML へのコンポーネント焼き込み | 生成パイプラインを今動かさない |
| TTS / SoundService | Phase 2 未着手 |
| ガイド / WorldSnapshot 実装 | レジストリが先。切る意味とバッファ形状だけ固定した |
| 空間インデックス | 計測してから |
| AI アシスタント / チャット UI / LLM 結合 | D-7。結合点だけ §9.4 |
| 片手入力プロファイル / リマップ UI | InputManager 未着手 |
| 字幕パイプライン | 読み上げと同じソース契約だけ固定 |
| ローカライズ本体 | D-8。解決器のシームだけ置いた |
| AssistiveSupport ヒエラルキ同期 | UITK 投影とは別。今はしない |
| 色覚パレット / レーダー / 空間音 | 消費者として読むだけと決めた |
| RenderingSystem インスタンス登録 | §24 側の実装に従う |
| EditMode のシーン監査バリデータ | 第一歩は実行時警告。YAML を今触らない |

---

## 13. 実装に進むときの最小スライス

設計が通った後の第一歩。TTS・ガイド・AI・片手プロファイル・Snapshot 生成・loc は第一歩にも入れない。

1. Foundation の Descriptor / Kind / Flags（予約域含む） / StableId / Audit / 素通し `IAccessibilityText` と EditMode テスト
2. Runtime の Registry: 重複は例外、Null Object は無視、`SetFlags`、呼び出し側バッファの Query、Scene 一括 Unregister
3. 位置供給元を Register に載せ、登録後に対象が動いても（テスト上の source 更新で）距離計算が追随すること。Snapshot 本体はまだ作らないが、source を読む関数はテストする
4. Authoring の `OnEnable` / `OnDisable` と Scene 一括解除の冪等
5. SceneBase: Stable で初期走査（inactive 含む）。解除は `UnregisterScene`。LoadCanceled / Dispose でも呼ぶ
6. `BindAccessible`（registry 必須）の UITK 投影。既存画面への適用は小さく
7. Cell を `Kind.Region` として登録
8. DebugSocket inspector に Kind / Label、実行時の未ラベル警告

受入の正は自動テスト。VoiceOver / Narrator の実機は Cloud Agent では検証できない。

---

## 14. 撤退ライン

1. **Unity Accessibility だけに寄せたくなった場合**  
   UITK 投影と AssistiveSupport はシンクとして残してよい。ワールド・Snapshot・AI の正本をそこに移さない（D-1）。

2. **GameEntity 継承を復活させたくなった場合**  
   アダプタが 3 表現をカバーしている限り、継承ツリーは増やさない。

3. **ジャンル固有 Kind を FW に足したくなった場合**  
   Flags 予約域か Game 側テーブルへ逃がす（D-2）。

4. **アシスタントを正の操作系にしたくなった場合**  
   既定は提案まで。実行は同意 + Actionable。片腕の代替にしない（§9.5）。

5. **座標を Descriptor に戻したくなった場合**  
   動く対象がある限り戻さない（D-11）。静的な看板だけなら source 無しで足りる。

契約（静的記述子 + 動的エントリ、Scene 一括解除、Snapshot が全盲向け入力、片腕は同時性を落とす）は撤退しても残す。

---

## 15. オープン論点

| # | 論点 | 現時点の見立て |
|---|---|---|
| O-1 | loc 導入時の key の置き場 | 解決器の後ろで Label を key として読む。フィールドは増やさない。導入時に確定 |
| O-2 | Snapshot の方位語彙 | 視覚に依存しないなら時計回り。ガイド実装時に 1 つに固定 |
| O-3 | 大量インスタンスをレジストリに載せる粒度 | Archetype 既定 + 近いものだけ実体化。計測してから |
| O-4 | Hidden を Query 既定で除外するか | しない。Policy が落とす。Hidden は読み上げと巡回から除外 |
| O-5 | UI の `localId` | UXML `name` をそのまま使うなら衝突しないよう作者が保証。フォールバック規則は置かない |
| O-6 | アシスタントの同意 UI | ConfirmDialog で足りるかは、実行を足すときに見る |
| O-7 | `TryGet` が返す詳細の型名 | Handle と Descriptor の二段。名前は実装時に固定 |
