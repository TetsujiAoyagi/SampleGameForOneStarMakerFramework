# 世界設計「主題と変奏」— S-3C 成果物と実装プラン (2026-08-29)

> ステータス: **発注者承認済みの設計**（2026-08-29 チャットセッションにて Fable が設計、発注者が方向を承認）
> 位置づけ: [SEASON_LEVELS_IMPLEMENTATION.md](SEASON_LEVELS_IMPLEMENTATION.md) の S-3C 成果物。
> **同書 §1.2 / §1.3 / §1.4 / §1.5、および §33 の D-1 / D-6 / §4 / §5 / §7 と食い違う箇所は本書が優先する。**
> 発注者裁定（§1）に基づく改訂であり、食い違いを見つけても停止不要。本書の指示に従うこと。
> 実装エージェントへ: 本書は自己完結。ここに無い制約を他文書から再解釈しない。

---

## 0. 一文

**世界はひとつの谷（9×6 セル）であり、四季は同じ座標に載る四つの変奏である。**
区別は identity の接頭辞（`Spring_Cell_4_2` / `Summer_Cell_4_2` / …）で行い、
季節の入れ替えはトンネル滞在中の Unload → Add で隠す。ワールドは原点直上の
2.25km × 1.5km に収まり、座標オフセットもテレポート写像も存在しない。

---

## 1. 発注者裁定ログ（旧前提のどれが死んだか）

| 日付 | 裁定 | 効果 |
|---|---|---|
| 2026-08-26 | 生成器で大まかに作り、人/AI の編集を正とする | §21 §6 の既存機構（`HandAuthored`）で実現。新 policy 種別なし |
| 2026-08-29 | 各季節をもっと大きく。予算上限（軟 16 / 硬 64）撤廃 | 総セル 216（§6 スケール表参照） |
| 2026-08-29 | **季節ごとの動詞割当を廃止**。実証の目的は「多人数・職種別の同時編集、単独ビルド、単独チェックアウト、イテレーションが世界のどこでも簡単」であること | §33 §4 / §5 の季節↔動詞表は退役。§4 の検証マトリクスに置換 |
| 2026-08-29 | 座標帯オフセット（原点から 25km〜75km）は float 精度・物理の理由で却下 | 撤回済み。象限配置（〜5.5km）も**その後の裁定で不要になり撤回** |
| 2026-08-29 | **四季は同じ座標を共有し、identity 接頭辞で区別する**（`Spring_Cell_4_2` 方式） | §33 D-1 の「identity に季節名を入れない」を発注者が明示的に上書き。FW の変更は `CellIdentity` 1 ユーティリティに限定（§5.1） |
| 2026-08-29 | テーマ「主題と変奏」（時制 × 楽譜の混合）を承認 | §2 |

---

## 2. テーマ「主題と変奏」

谷はひとつの**主題**（楽譜 = 座標の純関数）。四季はそれぞれの**変奏**で、
テンポ（密度）・調（光と霧）・アーティキュレーション（形の崩し方）を自由に解釈してよい。
ただし主題（線・見証・背）は必ず透けて見えること。

| 変奏 | 季節 | 性格 | 10 秒で見えるもの |
|---|---|---|---|
| I 素描 | 春 | 夜明け、薄い霧 | 主題が最も裸に近い。線は破線の床、杭と張り糸と灯が続きを予告する。書きかけであることが美しさ |
| II 密 | 夏 | 真昼、強い距離霧 | 最大密度の機械的な総奏。霧の縁 = ロード半径が天候として見える。世界はあなたの周りでだけ組み上がる |
| III 残響 | 秋 | 夕、琥珀の霧と長い影 | 主題は断片で鳴る。線の近くだけ完全な形、離れると台座と輪郭だけが残る。減衰が景色になる |
| IV 静止 | 冬 | 白、影のない光 | 全部あるのに彩度とコントラストが抜かれ、谷は記憶のように平たい。見証の頂きだけが濃い |

- 桜・紅葉・雪などの四季ポスター既定解は使わない
- 描画トーンは各季節の RenderSettings（霧の色/濃度・環境光・太陽角）+ 既存 tint 機構で作る。
  **新メッシュ・新シェーダ・新アセットパイプラインは投入しない**（Cube / Cylinder / Sphere / 共有 Lit のみ）
- 見証の頂きは各変奏の「署名」。その変奏だけを単独リビルドすると頂きが差し替わる（Build 実演の指差し先）

**品質バー（人の目視。自動テスト化しない）:**

1. どの変奏でも、最初の 3 秒で「同じ場所（主題）だ」と分かり、続く 3 秒で「違う変奏だ」と分かる
2. 判別の根拠は完成度パターンと光であり、色名ではない（グレースケール overhead でも I〜IV を区別できる）
3. どの季節のどのセルを開いても、床（`*_Cell_*.unity`）と印（`*_Environment_*.unity`）の 2 ファイルがあり、どちらを開くべきか迷わない
4. 生成器を再実行しても、どの変奏の演奏レイヤ（`AuthoredRoot` 配下）も 1 個も消えない

---

## 3. 幾何と identity

### 3.1 楽譜マップ（全変奏共通、局所座標 9×6）

```
      x0 x1 x2 x3 x4 x5 x6 x7 x8      1 セル = 250m。谷全体 2250m × 1500m
y5  |  ^  ^  ^  ^  ^  ^  ^  ^  ^     ^ = 背（北の高まり。Generated の計測コリドー）
y4  |  ~  ~  .  .  .  .  .  .  .     ~ = 線（源流は北西）
y3  |  .  .  ~  ~  .  .  .  .  .
y2  |  .  .  .  .  ◇  ~  ~  .  .     ◇ = 見証（曲がり角、局所 (4,2)。頂きが変奏の署名）
y1  |  .  .  .  .  .  .  .  ~  ~     線は南東へ抜ける
y0  |  .  .  .  .  .  .  .  .  .
```

- 線セル: `(0,4)(1,4)(2,3)(3,3)(4,2)(5,2)(6,2)(7,1)(8,1)`
- 見証セル: `(4,2)`（線の曲がり角。tall vertical、遠くから同定できるシルエット）
- 背: `y=5` の行 9 セル
- グリッド定数は現行どおり: `Origin = (0,0,0)` / `CellSize 250` / `LoadRadius 375` / `UnloadRadius 550` / `MaxInFlight 2`
- スポーン: 春（変奏 I）の源流セル `(0,4)` 中心上空

### 3.2 identity 仕様

```
無修飾:  Cell_{x}_{y}                     （現行互換。テストフィクスチャ等で存続）
修飾付き: {Qualifier}_Cell_{x}_{y}         例: Spring_Cell_4_2
Qualifier 文法: [A-Za-z][A-Za-z0-9]*      （アンダースコアを含まない 1 語）
```

- パース規則: identity 内の**最後の** `Cell_` から後ろを `{x}_{y}`（非負・数字のみ）として読む。
  `Cell_` が先頭でない場合、直前の文字は `_` であること
- `Spring` / `Summer` / `Autumn` / `Winter` という**値**は SampleGame 側のデータ。
  FW のコード・型・コメントには季節の語彙を入れない（従来どおり）
- 季節シーン: `Season_Spring` 等（SampleGame 側の identity。FW は関知しない）
- Environment: `{Qualifier}_Environment_{x}_{y}` 例: `Spring_Environment_4_2`
- フォルダ名 = セル identity（現行規約を維持）

### 3.3 シーン木

```
Main
  └── InGameScene
        └── InGameSession
              ├── Tunnel (LoadType.NecessaryAlways, 常設 1 本)
              ├── Season_Spring (OnDemand)
              │     └── Spring_Cell_{x}_{y} (OnDemand)
              │           └── Spring_Environment_{x}_{y} (OnDemand)
              ├── Season_Summer (OnDemand)  … 以下同型
              ├── Season_Autumn (OnDemand)
              └── Season_Winter (OnDemand)
```

- 現行の `World` ノードは Season_* 4 つに置き換わる（§33 D-2 どおり）
- **常駐する季節はトンネル遷移中を除き常に 1 つ。** 全季節が同じ座標を占めるため、
  遷移は必ず「旧季節 Unload 完了 → 新季節 Add」の順（§5.4）。重畳を作らない
- トンネルの物理位置は**谷の AABB（XZ: 0..2250 × 0..1500）と重ならない場所**に置く（具体位置は S-5 で決定）

---

## 4. 検証マトリクス — このサンプルが証明する表

季節ごとに別の動詞を陳列するのではなく、**全変奏 × 全ワークフロー**を証明する。

| ワークフロー | 実現手段（全変奏共通） |
|---|---|
| 2 職種の同時編集 | 全セルに床 `*_Cell_*.unity`（地形職: 地面・高低・線の床・台座）+ 印 `*_Environment_*.unity`（置き物職: 杭・張り糸・灯・小屋・足場）。同じ地点を 2 人が同時に触ってもファイルが違うので衝突しない |
| 再生成しても編集が残る | 背 = `Generated`（機械の声部。自由に再生成）/ 谷 = `HandAuthored`（人の声部。`AuthoredRoot` を R-6 が保護） |
| 単独ビルド | 1 変奏 = 1 Addressables グループ。見証の頂きを差し替え → その変奏だけ再ビルド → 他 3 変奏のバンドルはハッシュ不変 |
| 単独チェックアウト | 1 変奏 = 1 Variant タグ。手元に無い変奏はリモートカタログから解決、解決不能ならトンネル出口で明示失敗し旧季節へ復帰（D-5 継承） |
| ストリーミング | 全域で動く。**計測（§21 A-1/A-2）だけは変奏 II（夏）の背コリドー**（最密・機械均一・直線 2250m）で取る |
| イテレーション | ループ実演: 印を 1 個編集 → 保存 → 生成器再実行（演奏レイヤは消えない）→ Play → 変奏単独の差分ビルド |

**正本 policy の配置（旧 §1.2 の季節別割当を置換）:**

| 領域 | policy | セル数 |
|---|---|---:|
| 背（各季節の局所 y=5 行） | `Generated` | 9 × 4 季節 = 36 |
| 谷（各季節の局所 y=0..4） | `HandAuthored` | 45 × 4 季節 = 180 |

`HandAuthored` は「全セルを手作業で作る」ではない。初回は生成器のスキャフォールド、
以後の編集が正。手を入れるのは各変奏で線沿い 8〜10 セル + 見証周辺が目安。

---

## 5. 実装スライス

順序: **S-3D → S-4 → S-5 → (S-6, S-7) → S-8 → S-9**。
1 スライス = 1 ブランチ = 1 HANDOFF（着手時に Phase A が本書から切り出す）。
旧 HANDOFF §1.7 の Editor 操作境界（人間が開いた Editor への CLI のみ可、Unity.exe 起動・
テスト実行・YAML 手編集は禁止、テストは Phase C）は全スライスに適用。`record` 禁止・
`#nullable enable`・破棄されうる `UnityEngine.Object` への `?.` / `??` 禁止も同様。

### 5.1 S-3D — `CellIdentity` の修飾対応（**FW 唯一の変更**）

対象: `OneStarMaker/Scripts/Runtime/SceneSystem/Cells/CellIdentity.cs` + テスト。

- `TryParse` / `IsCellId`: §3.2 の規則で無修飾・修飾付き両対応にする。
  `IsCellId` は R-3（セルを SwitchScene / GoBack / TransitionPlan に乗せない）のガードなので、
  修飾付きセルを**弾ける**ことがこのスライスの本題
- `Format(int x, int y)` は現行維持。`Format(string qualifier, int x, int y)` を追加
  （qualifier は §3.2 文法をバリデートし、違反は例外）
- `CellScene` は `TryParse` 経由なので自動追随（`Coordinate` は局所座標になる）。コード変更不要の見込み
- テスト: 無修飾の既存ケース全維持 / 修飾付きの parse・guard / `Cell_-1_0`・`X__Cell_1_2`・
  `Cell_1_2_3` などの拒否。**テストデータの qualifier に季節名を使わない**（`North` 等の中立語を使う。
  受入 grep を汚さないため）

受入: 既存テスト全緑 + 上記新規テスト緑。`unity/Assets/OneStarMaker/` を
`Season|Spring|Summer|Autumn|Winter|季節` で grep → 0 件。

### 5.2 S-4 — 谷の生成と季節スワップ（本体）

**Catalog（`SampleGame/InGame/InGameSession/Streaming/WorldCellCatalog.cs`）:**

```csharp
public enum SeasonId { Spring, Summer, Autumn, Winter }   // SampleGame 側のみ。FW に出さない

public static class WorldCellCatalog
{
    // 谷は 1 つ。矩形 { origin=(0,0), size=(9,6) }。全季節が共有する
    public static readonly CellRect[] Rectangles;          // 1 要素
    public static string Qualifier(SeasonId season);       // "Spring" 等
    public static IReadOnlyList<Vector2Int> EnumerateCells();          // 局所 54 セル
    public static bool TryGetCoordinate(Vector3 pos, out Vector2Int c); // 季節非依存（谷は 1 つ）
    public static string? TryGetCellIdentity(SeasonId active, Vector3 pos); // 修飾付き identity
}
```

**季節スコープの Backend デコレータ（SampleGame 側。FW の Controller / Config は無改変）:**

```csharp
// RequestAdd / RequestRemove / IsLoaded で cellId を $"{qualifier}_{cellId}" に変換して
// 内側の SceneDirectorStreamingBackend へ委譲するだけの ISceneStreamingBackend ラッパ
public sealed class SeasonScopedStreamingBackend : ISceneStreamingBackend { ... }
```

**Driver（`SessionWorldStreamingDriver`）:** `SwitchSeason(SeasonId)` を追加。
旧 Controller を Stop → 新季節の qualifier でデコレータを作り直し → Controller 再構築 → Start。
Controller 内部の in-flight はインスタンス内で閉じており、識別文字列は SceneDirector 層で
必ず修飾済みなので季節間で混流しない。`CurrentCellIdentity` / `GetResidentCellIdentities` も
アクティブ季節の qualifier を使う。

**生成器（`WorldCellGenerator` / `WorldCellStreamingSliceCreator` / `CellPopulationPlan`）:**

- Season_* 4 ノードを吐き、`World` を置き換える（§33 D-2）
- **楽譜関数**: 局所 `(x, y)` の純関数で 高さ（背 y=5 高 / 線セル低 / 他は傾斜）・線の床・
  見証（(4,2)）を配置。乱数を使うならシード固定
- **変奏パラメータ**: 季節ごとの 密度スカラー・tint パレット・崩し方。
  **セルあたりオブジェクト数は変奏内で一定**（計測の均質性は背コリドーの必須条件）
- 全セルに Environment シーンをスキャフォールド（空でよい）
- `CellAuthoringPolicy` を §4 の声部ベース（y=5 行 = Generated、他 = HandAuthored）へ。
  南辺ハードコード 4 箇所（`CellAuthoringPolicy` / `WorldCellStreamingSliceCreator` /
  `HandEditProbe` / 完了ログ）を追随
- 各 Season シーンに変奏の RenderSettings（霧・環境光・太陽角）を持たせる。
  適用主体は「初回季節を誰が Ensure するか」（§33 §8 注記）と同時にこのスライスで裁定する

**既存 16 セルの移送（旧 HANDOFF §1.4 / §4.2 の SOP を継承。行き先を確定）:**

| 旧 | 新 |
|---|---|
| `Cell_0_0` (+ `Environment_0_0`) | `Spring_Cell_0_4` (+ `Spring_Environment_0_4`) |
| `Cell_1_0` (+ `Environment_1_0`) | `Spring_Cell_1_4` (+ `Spring_Environment_1_4`) |
| `Cell_2_0` (+ `Environment_2_0`) | `Spring_Cell_2_3` (+ `Spring_Environment_2_3`) |
| `Cell_3_0` (+ `Environment_3_0`) | `Spring_Cell_3_3` (+ `Spring_Environment_3_3`) |

手順: `unity status` ready → `move_asset`（GUID 同梱）→ identity / 親 / Addressables address を
`SerializedObject` 経路で → `AuthoredRoot` のワールド Δ（旧セル原点 → 新セル原点）を
`set_transform` → policy / sprout 追随 → `StampHandEdits` → 生成器 → `VerifyHandEdits` →
生成器もう 1 回 → HandAuthored 側差分 0。旧 `Generated` 12 枚は破壊経路 3 で削除させる。
YAML 手編集・単独 `git mv` を正本にしない。

受入: 生成器 2 回実行後、セル 216 + Environment 216 + Season 4 が SceneResourceMap に重複警告 0 で
載る / stamp 全生存 / Editor Play で谷がロードされ例外 0。

### 5.3 S-5 — トンネルと季節遷移

- `InGameSession` 直下・`NecessaryAlways`・常設 1 本（§33 D-4）。物理位置は谷 AABB 外
- 遷移シーケンス: プレイヤーがトンネルに入る → `SwitchSeason` 停止（Tick 停止）→
  旧 Season を `UnloadScene`（配下セル再帰破棄）→ **Unload 完了後に** 新 Season を `AddScene` →
  Driver を新季節で再開 → 出口を開く。**同座標なので順序厳守**（重畳を作らない）
- 失敗経路（D-5 継承）: 新季節が解決不能ならトンネル出口で明示的に失敗し、旧季節を再 Add して戻す。
  暗黙フォールバック禁止
- テレポート写像は**存在しない**。入った場所と同じ座標に、別の変奏で出る

### 5.4 S-6 / S-7 / S-8 / S-9

| # | 内容 | 補足 |
|---|---|---|
| S-6 | 1 変奏 = 1 Addressables グループ | 受入: 1 季節リビルドで他 3 季節のバンドルがハッシュ不変。Tunnel は専用グループを持たない |
| S-7 | 1 変奏 = 1 Variant タグ + 未チェックアウト経路 | §20 の既存機構（whitelist / Hybrid Play / RemoteCatalog）にデータを流すだけ。新機構なし |
| S-8 | 各変奏の演奏レイヤ投入（旧「春の作り込み」を全変奏に一般化） | 線沿い + 見証の作り込み、`HandEditProbe` とスキャフォールド宣言の退役。品質バー §2 を人が目視 |
| S-9 | 変奏 II（夏）の背コリドーで §21 T-07〜T-09（A-1 / A-2 計測） | それまで T-07〜T-09 凍結（従来どおり） |

### 5.5 テストの扱い

- 既存 WSC 10 本 / MultiFocus / 統合 / 生成器 / `CellPopulationPlan` は S-3 で矩形集合対応済み。
  S-4 で「谷 9×6 単一矩形 + デコレータ」の入力に追随させ、全部残す
- 旧 T-A（矩形間の空隙ガード）の**本番 assert は不要になる**（本番は単一矩形 × 4 季節共有座標のため
  矩形ペアが存在しない）。フィクスチャテストとしては残す
- デコレータの単体テスト（qualifier 付与・`IsLoaded` の一貫性）を追加
- テストで `Task.Delay` / `Thread.Sleep` 禁止。全件実行（`pwsh tools/run-tests.ps1`）は Phase C の仕事。
  実装者は「実装完了。テスト未実行」と報告する

---

## 6. スケール（採用: M）

| 案 | 季節寸法 | 総セル | 長軸横断 (42 m/s) | 備考 |
|---|---|---:|---:|---|
| S | 6×4 | 96 | 36s | 縮小する場合は楽譜セル座標を再定義 |
| **M（採用）** | **9×6** | **216** | **54s** | 本書の全座標はこの前提 |
| L | 12×8 | 384 | 71s | bake / Play ロード / リポジトリが線形に伸びる |

---

## 7. 受入条件（プログラム全体）

| # | 条件 | 判定 |
|---|---|---|
| W-1 | FW に季節の語彙が無い | `unity/Assets/OneStarMaker/` を `Season\|Spring\|Summer\|Autumn\|Winter\|季節` で grep → 0 件 |
| W-2 | identity 重複 0 | SceneResourceMap 生成時の Duplicate 警告 0 |
| W-3 | 遷移の排他 | トンネル遷移ログで旧季節 Unload 完了 → 新季節 Add の順序。重畳 0。遷移後 desired set が完全に入れ替わる |
| W-4 | 編集が消えない | 生成器 2 回のあと stamp 全生存（Environment 増加分含む） |
| W-5 | 単独ビルド | 1 季節リビルドで他 3 季節バンドルのハッシュ不変。見証の頂きが変わる |
| W-6 | 単独チェックアウト | ローカル欠落季節がリモート解決 or 明示失敗 + 旧季節復帰 |
| W-7 | 品質バー | §2 の 4 項目を人が目視（自動化しない） |
| W-8 | 計測 | 変奏 II 背コリドーで A-1 / A-2 が §21 の受入値内 |

レビュー時の grep: `?.` / `??` / `is null` / `ReferenceEquals`（破棄されうる `UnityEngine.Object` 対象）。

---

## 8. §33 / 旧 HANDOFF への harvest チェックリスト（全スライス完了後）

- §33 D-1: 「identity に季節名を入れない」→ 発注者裁定で上書き。修飾 identity 仕様（§3.2）を反映
- §33 D-6 / §5 表: 季節↔動詞・季節別 policy → §4 の検証マトリクスと声部 policy へ書き換え
- §33 §7: 空隙の幾何 → 同座標 + 遷移排他（§3.3 / §5.3）へ書き換え
- §33 §8: シーン木の identity 例を修飾付きへ
- 旧 HANDOFF `SEASON_LEVELS_IMPLEMENTATION.md`: S-3C 完了時点で本書と重複する節を整理。
  両 HANDOFF とも、全スライス harvest 後に `git rm`
- `pwsh tools/docs-audit.ps1` を通す

---

## 9. 未決事項

| # | 論点 | 決定時期 |
|---|---|---|
| N-1 | 初回季節を誰が Ensure するか（トンネル始まりか、Session の初回 Ensure か） | S-4 |
| N-2 | RenderSettings の適用主体（Season シーンの Stable フックか、専用コンポーネントか） | S-4（N-1 と同時） |
| N-3 | トンネルの物理位置・内装・滞在時間（ロード隠蔽の実測待ち。§33 O-3） | S-5 |
| N-4 | Environment シーンのクラスが CellScene 型か否か（修飾 identity の影響確認） | S-3D 着手時に実地確認 |
| N-5 | 第三声部（照明職 `*_Lighting_*.unity`）を標準装備にするか | S-8 までに発注者判断。今回は 2 声部 |
| N-6 | `unityyamlmerge` ドライバ設定（§33 O-5。前提条件ではない） | 任意 |
