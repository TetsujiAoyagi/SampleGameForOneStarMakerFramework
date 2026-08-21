# 30. 意味アイデンティティ層（Accessibility Identity）

> ステータス: **構想・契約固定（実装前）**（2026-08-21）
> [ARCHITECTURE.md](../../ARCHITECTURE.md) に戻る
> 関連: [05-scene.md](05-scene.md)、[06-ui.md](06-ui.md)、[31-accessibility-output-budget.md](31-accessibility-output-budget.md)、[32-accessibility-input-dof.md](32-accessibility-input-dof.md)、[21-scene-streaming.md](21-scene-streaming.md)、[24-rendering-system.md](24-rendering-system.md)

本章は **番地付けと命名の層** である。アクセシビリティ機能そのものではない。
配送・注意の調停は [§31](31-accessibility-output-budget.md)、片腕の入力自由度は [§32](32-accessibility-input-dof.md)。

ジャンル（STG 含む）を前提にしない。**コードはまだ書かない。**

---

## 目次

1. [一文](#1-一文)
2. [目的・スコープ](#2-目的スコープ)
3. [Web 由来との差](#3-web-由来との差)
4. [用語定義](#4-用語定義)
5. [なぜクラス継承ではないか](#5-なぜクラス継承ではないか)
6. [設計判断](#6-設計判断)
7. [寿命](#7-寿命)
8. [データ契約](#8-データ契約)
9. [レジストリ](#9-レジストリ)
10. [消費者への供給](#10-消費者への供給)
11. [API スケッチ](#11-api-スケッチ)
12. [配置](#12-配置)
13. [今やらない](#13-今やらない)
14. [実装スライス](#14-実装スライス)
15. [撤退ライン](#15-撤退ライン)
16. [オープン論点](#16-オープン論点)

---

## 1. 一文

ゲーム内の「もの」は表現（`VisualElement` / `GameObject` / 将来の描画インスタンス）が違っても、同じ記述子を持つ。
読み上げ・字幕・ガイド・AI・片腕フォーカスは、いずれもこの記述子の**消費者**であり、系統ごとに名前を持たない。

本章が満たす答えは「それは何か、何と呼ぶか、今どの Flags か、どこにあるか（供給元経由）」までである。
「今、希少な出力に何を載せるか」は満たさない。それは §31。

---

## 2. 目的・スコープ

**本章が証明すること（将来）:**

- 意味のあるオブジェクトは、表現が違っても同一の Kind / Flags / Label / Hint で問える
- セルストリーミングで出入りしても、Scene 寿命と登録がずれない
- pull クエリ（今いる Region、Focusable な候補、距離付き一覧）の番地が欠けない

**本章が主張しないこと:**

- 視覚に頼れない人が、この層だけでプレイできる
- Announcable を全部読めば全盲向けになる
- 片腕プレイの完成（候補集合の供給だけ。[§32](32-accessibility-input-dof.md)）

**今やること:** 契約の文書化。コードは [§13](#13-今やらない)。

---

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

1. **維持** — 前回から何が変わったか（ポーリングの Snapshot では差分にならない）
2. **速度** — 希少な出力チャネルに、今載せる価値のある少数は何か

タグ / ラベル / ヒントは 1 と 2 を扱わない。扱わないと明言したうえで、答えは §31 に置く。連続量（接近、残量）も記述子に載せない。離散ビットと名前に閉じる。

---

## 4. 用語定義

| 用語 | 定義 |
|---|---|
| 記述子 (`AccessibilityDescriptor`) | 静的な意味の値型。Kind / Label / Hint / 既定 Flags。位置は持たない |
| エントリ | レジストリの**内部表現**。公開型ではない。記述子 + 動的 Flags + 位置供給元 |
| Kind | ガイドが最初に言う名詞。単一値 |
| Flags | ホットパス用ビット。側面と動的状態 |
| Label / Hint | 人向けの名前と動作句。解決器を通す（既定は素通し） |
| StableId | `{SceneIdentity}/{localId}`。`localId` は必須 |
| レジストリ | App 常駐の登録表。Scene スコープ |
| WorldSnapshot | pull 型の切断面。「今、把握すべき世界」。差分ではない |
| 作者付け | 記述子を載せる作業。未付けは「存在しない」 |

---

## 5. なぜクラス継承ではないか

「全オブジェクトが継承する」を `GameEntity : MonoBehaviour` で実装すると、今の OSM に届かない。

| 表現 | 実態 | 登録のタイミング | 解除の粒度 |
|---|---|---|---|
| ワールド作者物 | Unity `GameObject` | Scene Stable の初期走査（Attach + Register）。以降は Attach 済みの `OnEnable` / `OnDisable` | Scene identity 一括。個体は Disable / Unregister / OnDestroy |
| UI | `VisualElement` | ViewIn（Initializing。Stable より前） | 同上 |
| 将来の大量描画 | GameObject を持たないインスタンス | 生成時 | 破棄時 / Scene 一括 |

実装形は Transform のような必須サイドカー＋同一記述子契約。表現ごとのアダプタが記述子を出し、レジストリが寿命を持つ。

---

## 6. 設計判断

### 6.1 決定事項

| # | 決定 | 根拠 |
|---|---|---|
| D-1 | **記述子が識別の正本。** Unity Accessibility は別シンク | 2D の読み上げ順序モデルであり、距離・方位・Actionable を持たない。Snapshot の入力にならない |
| D-2 | **契約は FW。Kind はジャンル非依存。** Game 拡張は Flags bit 16–31 | Foundation の enum に Game は名前を足せない |
| D-3 | **解除の粒度は Scene identity 一括。** 登録タイミングは表現ごと（§5） | UI の ViewIn は Stable より前 |
| D-4 | **既定は未登録。** Decorative は「在るが黙る」と明示したいときだけ | 全 GO 自動付与は Snapshot を壊す |
| D-5 | **名前の正本は一つ。** 系統ごとに DisplayName を増やさない | 配送側（§31）が同じ Label を読む |
| D-8 | **Label はプレーン文字列。** 消費者は `IAccessibilityText` を通す | loc 本体は今入れない |
| D-9 | **ホットパスは Kind + Flags + Handle スロット。** 文字列はコールド | 毎フレームの string 比較をしない |
| D-10 | **配線は手動 DI。** Authoring は `Attach` されるまで自己登録しない | サービスロケータ禁止。[AssetReleaseOnDestroy.Initialize](../../OneStarMaker/Scripts/Runtime/AssetManagement/Components/AssetReleaseOnDestroy.cs) と同型 |
| D-11 | **静的な意味と動的な状態を分ける。** 位置は供給元、Flags は `SetFlags` | 記述子に座標を焼くと距離が追従しない |
| D-12 | **Kind は単一名詞。** 側面は Flags | 複数 Kind だと発話の第一語が決まらない |
| D-13 | **Query は呼び出し側バッファ。** 詳細は Handle + `TryGetDescriptor` | 戻り値形状は後から変えられない |
| D-14 | **初期走査が Attach と初回 Register を同時に行う。** Attach 前の OnEnable は登録しない | Stable 時点で OnEnable は既に終わっている。走査と OnEnable の二重 Register を防ぐ |
| D-15 | **Handle は Query / TryGet した瞬間のコピー。** 跨いで保持しない | `SetFlags` 直後に stale になる |
| D-16 | **連続量は記述子に載せない** | 接近・残量は §31 の非音声音、または Game の音響。Identity をテレメトリバスにしない |

片腕の「同時入力を落とす」は本章の決定ではない。[§32 D-1](32-accessibility-input-dof.md)。本章は Focusable + Actionable の候補集合だけを供給する。

### 6.2 却下案

| 却下 | 理由 |
|---|---|
| `GameEntity` 強制継承 | §5 |
| Unity Accessibility をワールド正本にする | D-1 |
| ジャンル固有 enum を Runtime に置く | D-2 |
| Scene サブクラスの手 Register を唯一の入口にする | 初期集合は走査。動的出入りは Attach 後の Enable |
| 全 GO 自動付与 | D-4 |
| 系統ごとに DisplayName | D-5 |
| 座標を Descriptor に焼いて Register し直す | D-11 |
| `BindAccessible` の registry 省略 | D-10 |
| GameObject 名を `localId` 既定 | 同名衝突 |
| Attach 前の OnEnable 自己登録 | registry を持たないのが正しい既定（D-14） |
| Announcable を全部読み上げる | 識別層の仕事ではない。§31 |
| Urgent 1 ビットで配送を済ませる | 識別上の「割り込み資格」にすぎない。調停は §31 |

---

## 7. 寿命

Scene が寿命の正本（[05-scene.md](05-scene.md)）。**解除**はそれに従う。**登録**は表現ごとにずれる（D-3, D-14）。

```
Scene Loaded → Initialize（Root 取得）
  → ViewIn（Initializing） … UI はここで Register
  → Stable                 … ワールド: 走査が Attach(registry, sceneIdentity) と初回 Register
  → （実行中）             … Attach 済みの OnEnable / OnDisable だけが個体の出入り
  → PreUnloading / LoadCanceled / Dispose … UnregisterScene(identity)（冪等）
  → Unload
```

[`SceneBase.ExecutePreUnLoad`](../../OneStarMaker/Scripts/Runtime/SceneSystem/SceneBase.cs) は `OnPreUnLoadedImpl` の前に `_rootObjects` を Clear する。
解除は GO 走査ではなく `UnregisterScene(sceneIdentity)`。Clear との前後は関係ない。

App 常駐レジストリなので、次でも `UnregisterScene` する（冪等）:

- `LoadCanceled`
- `SceneBase.Dispose`
- Editor 停止 / Domain Reload 無効時の Bootstrap クリーンアップ

初期走査は `GetComponentsInChildren<T>(includeInactive: true)`。
走査は初期集合に限る。生成物は、生成側が Attach してから Enable する。

OnDestroy でも Unregister する（個体）。Unregister されずに GO だけ壊れたエントリは、位置供給元が偽 null なら Snapshot / 距離 Query から落とす。`TryGet` には残ってよい。Scene 一括解除が最終掃除。

セルストリーミングでは Cell の Stable / Unload がレジストリの出入りになる。WSC の desired set にアクセシビリティ判断を足さない。

---

## 8. データ契約

Foundation の純 C#。Unity UI に依存しない。

### 8.1 Kind / Flags

Kind は §4 の単一名詞。複数に見えるものはガイドの第一語を Kind、残りは Flags。

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
    Urgent      = 1 << 6,  // 割り込み資格。配送の優先度クラスは §31
    // bit 7–15: FW 予約
    // bit 16–31: Game 専用
}
```

`Hidden` は読み上げ・ガイド・フォーカス巡回から除外。`Hidden | Focusable` と `Hidden | Announcable` は作者付け不備。Query は Hidden を落とさない。Policy が落とす。

`DefaultFlags` は Register 時の初期値にすぎない。下地として後から合成しない。以降は `SetFlags` が全置換。

`Urgent` は「割り込んでよい対象」の印。キューや同時鳴動は §31。

### 8.2 Descriptor

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

`localId` 必須。空 Label かつ Focusable / Announcable / Actionable は不備。Decorative の空 Label は許容。

### 8.3 位置の供給元

```csharp
public interface IAccessibilityPositionSource
{
    bool TryGetWorldPosition(out float x, out float y, out float z);
}
```

ワールド Authoring は Transform を包む。破棄済み `UnityEngine.Object` は `?.` / `??` では短絡しない。`if (_transform == null) return false;` が要る（Unity の `==` オーバーロード）。

UI と System は source 無し。Snapshot は切る瞬間に `TryGet` する。Register 時の座標は使わない。

### 8.4 文言の解決器

```csharp
public interface IAccessibilityText
{
    string Resolve(string labelOrKey);
}
```

既定は素通し。loc 導入後に差し替え。消費者は Label / Hint を直接表示しない。

---

## 9. レジストリ

App 常駐。SceneDirector が SceneBase / UIView へ注入する。Authoring へは SceneBase が `Attach` する（D-10, D-14）。

```csharp
public interface IAccessibilityRegistry
{
    bool Register(
        in AccessibilityDescriptor descriptor,
        IAccessibilityPositionSource? positionSource = null);

    void SetFlags(in AccessibilityHandle handle, AccessibilityFlags flags);
    void AddFlags(in AccessibilityHandle handle, AccessibilityFlags flags);
    void RemoveFlags(in AccessibilityHandle handle, AccessibilityFlags flags);
    void SetFlags(string stableId, AccessibilityFlags flags); // コールド

    bool Unregister(string stableId);
    void UnregisterScene(string sceneIdentity);

    bool TryGet(string stableId, out AccessibilityHandle handle);
    bool TryGetDescriptor(string stableId, out AccessibilityDescriptor descriptor);
    int Query(
        List<AccessibilityHandle> results,
        AccessibilityKind? kind = null,
        AccessibilityFlags requiredFlags = AccessibilityFlags.None);
}

public readonly struct AccessibilityHandle
{
    public int Slot { get; }           // レジストリ内部スロット。ホットパス用
    public string StableId { get; }    // コールド
    public AccessibilityKind Kind { get; }
    public AccessibilityFlags Flags { get; }
}
```

- 重複 StableId は `InvalidOperationException`。Null Object の Register は `false` で例外なし
- `Unregister` / `UnregisterScene` / 対象なしの Flags 変更は冪等
- `SetFlags` は全置換。`AddFlags` / `RemoveFlags` は部分更新
- Handle は取得瞬間のコピー（D-15）。Flags 変更の主経路は Handle。string キー版はコールド（D-9）
- Register / Unregister / SetFlags は §31 の変更フィードの材料になる。購読 API は本章に置かない

WorldSnapshot は pull の切断面として意味だけ残す。差分ではない。配送が読む「今の 3 つ」は §31 が Snapshot と変更フィードから選ぶ。

距離を読む前段（source をその場で `TryGet` し、疑似距離を返す純関数）の置き場は Runtime。Snapshot 本体より先にテストしてよい。

---

## 10. 消費者への供給

本章が供給するもの:

| 消費者 | 供給 |
|---|---|
| §31 出力 | 番地、名前、Flags、位置供給元、変更の材料 |
| §32 片腕 | Focusable + Actionable の候補（Hidden 除外） |
| UITK | `focusable` / `tabIndex` / `tooltip` |
| AssistiveSupport | 別シンク。Android / iOS / Windows / macOS（Unity 6.5 `AssistiveSupport` 公式）。自動では UITK から生えない |
| DebugSocket | Kind / Label。実行時の未ラベル警告。プロトコル変更は後続（§13） |

読み上げ・字幕・ガイド・AI の**文面生成とキュー**は §31。ここでは「読める名前がある」ことだけを保証する。

---

## 11. API スケッチ

今はコンパイルしない。

### 11.1 StableId

```csharp
public static class AccessibilityStableId
{
    public static string Combine(string sceneIdentity, string localId);
}
```

空 `localId` は例外。衝突は Register が例外で顕在化する。

### 11.2 Authoring

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
- Cell は `Kind.Region` を identity から登録してよい

### 11.3 UI

```csharp
public static IDisposable BindAccessible(
    this VisualElement element,
    in AccessibilityDescriptor descriptor,
    IAccessibilityRegistry registry);
```

`registry` 必須。

### 11.4 監査

```csharp
public static class AccessibilityAudit
{
    public static bool RequiresLabel(in AccessibilityDescriptor descriptor);
    public static bool HasMissingLabel(in AccessibilityDescriptor descriptor);
    public static bool HasInvalidHiddenCombo(AccessibilityFlags flags);
}
```

`HasInvalidHiddenCombo` は `Hidden | Focusable` と `Hidden | Announcable`。検出は実行時（inspector）。EditMode バリデータは後続。

---

## 12. 配置

| 層 | 置くもの | 置かないもの |
|---|---|---|
| Foundation | Kind, Flags, Descriptor, Handle, StableId, Audit, `IAccessibilityText` | UITK、MB、TTS、Unity Transform |
| Runtime | Registry, Authoring, Transform 位置供給元、source を読む距離の前段関数、SceneBase 初期走査、BindAccessible | 顕著性方針、LLM |
| Debug | inspector 表示（プロトコル非変更の範囲） | 世界モデルの正本 |
| Game.Common | Flags bit 16–31 の名前、Label 文言 | FW 契約の再定義 |
| DependOnAll | レジストリ生成と注入 | — |

---

## 13. 今やらない（実装）

| 項目 | 理由 |
|---|---|
| C# 一式 | 契約が読まれるまで型を増やさない。次は §14 |
| SceneBase / SceneDirector / UIView 改修 | S-2 |
| 既存画面の UITK 配線 | スケッチに留める |
| セル YAML 焼き込み | 生成パイプラインを今動かさない |
| DebugSocket プロトコル変更 | スキーマ版付き surface。DebugStudio が対向。S-2 でも後続へ回す |
| WorldSnapshot 実装 / 空間インデックス | レジストリと距離前段が先 |
| 変更フィード購読 API | §31 |
| TTS / ガイド / AI / 字幕 | §31。Phase 2 Sound 未着手 |
| 片手プロファイル | §32。InputManager 未着手 |
| ローカライズ本体 | D-8 |
| AssistiveSupport ヒエラルキ同期 | UITK 投影とは別 |
| 連続量を記述子に載せる | D-16 |
| EditMode シーン監査 | 第一歩は実行時警告 |

---

## 14. 実装スライス

TTS・ガイド・AI・片手・Snapshot 生成・loc・プロトコル変更は入れない。

**S-1（既存ファイルに触らない）**

1. Foundation の型 / Audit / 素通し `IAccessibilityText` と EditMode テスト
2. Runtime の Registry: 重複例外、Null Object、Set/Add/RemoveFlags、バッファ Query、`TryGetDescriptor`、Scene 一括 Unregister
3. 位置供給元を Register に載せ、source 更新で距離前段が追随すること（Snapshot 本体は作らない）

**S-2**

4. Authoring の Attach / OnEnable / OnDisable / OnDestroy と Scene 一括の冪等
5. SceneBase 初期走査。LoadCanceled / Dispose でも UnregisterScene
6. `BindAccessible`（registry 必須）。既存画面への適用は小さく
7. Cell を `Kind.Region` として登録
8. DebugSocket inspector に Kind / Label（**プロトコル変更を伴うならこの項は後続**）

受入の正は自動テスト。スクリーンリーダ実機は Cloud Agent では検証できない。

---

## 15. 撤退ライン

1. Unity Accessibility に識別の正本を移さない（D-1）
2. GameEntity 継承を復活させない
3. ジャンル固有 Kind を FW に足さない
4. 座標を Descriptor に戻さない（D-11）
5. 連続量を Identity に載せ始めてテレメトリバス化しない（D-16）

番地付け、Scene 一括解除、静的記述子と動的エントリの分離は撤退しても残す。

---

## 16. オープン論点

| # | 論点 | 現時点の見立て |
|---|---|---|
| O-1 | loc の key | 解決器の後ろで Label を key として読む |
| O-2 | Snapshot の方位語彙 | §31 のガイド実装時に時計回りで固定 |
| O-3 | 大量インスタンスの粒度 | Archetype 既定 + 近いものだけ。計測してから |
| O-4 | Hidden を Query 既定除外するか | しない。Policy が落とす |
| O-5 | UI の `localId` | UXML `name` を作者が衝突させない。フォールバック規則は置かない |
| O-6 | 破棄済み source を `TryGet` に残す上限 | Scene 一括解除まで。リーク疑いが出たら期限を足す |
