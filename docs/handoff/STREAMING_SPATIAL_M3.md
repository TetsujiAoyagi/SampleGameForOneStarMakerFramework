# Streaming 空間政策 M-3 HANDOFF — R-3 を候補フラグへ

> ステータス: **Phase A 完了・Phase B 未着手。M-2 の後に着手する。**
> 上位計画: [STREAMING_SPATIAL_MIGRATION.md](STREAMING_SPATIAL_MIGRATION.md)
> 到着契約: [§34 OnDemand の空間政策](../../unity/Assets/Docs/Architecture/34-ondemand-spatial-policy.md)
> 現状仕様: [STREAMING_CURRENT_SPEC.md](../streaming/STREAMING_CURRENT_SPEC.md)
> harvest 先: 実装値は `STREAMING_CURRENT_SPEC.md`、§21 の R-3 行を追随。マージ時に本書を削除する。

問 4 は移行 HANDOFFで閉じた。**一括。`IsCellId` 過渡は採らない。**

---

## 1. 目的

`SwitchScene` / `GoBack` / `TransitionPlan` の R-3 ガードを、名前文法
（`CellIdentity.IsCellId`）から `SceneResource.StreamByDistance` へ移す。

現行 `Cell_0_0` で失敗し続けること。修飾付き名での着地は S-4。
factory の `IsCellId` → `DemoCellScene` 結線は触らない。

---

## 2. 対象外

- factory / `CellScene` ctor / `TryFromCellId`（S-4）
- `ISceneVolumeQuery` の署名変更、失敗理由 enum（**却下して閉じる**）
- 公開 API / DI / asmdef の追加
- `GoBack` / `ExecuteTransitionPlan` への第二のガード
  （今どおり `SwitchSceneCore` 経由）
- 本番 Scene / asset のフラグ書き換え
- M-2 の生成器、M-4 の型移送

Phase B は C# と HANDOFF 実績欄だけ。Unity.exe / `run-tests.ps1` / Addressables ビルドを実行しない。

---

## 3. 決定

`SceneDirector.Transitions` の `ThrowIfCellIdentity` を **インスタンスメソッド** に変え、
フラグだけを見る。名前は仮に `ThrowIfStreamByDistanceCandidate`。

```csharp
var resource = _sceneResourceMap.GetSceneResource(sceneIdentify);
if (resource == null) return;          // 未登録・破棄済みは拒否しない
if (!resource.StreamByDistance) return; // フラグ off は拒否しない
throw new InvalidOperationException(...);
```

- from / to の双方を、span・LoadingDisplay・履歴・Unload / Add より前に見る。今と同じ位置。
- 体積が空でもフラグ true なら拒否する。
  `ISceneVolumeQuery.TryGetSceneVolume` は空体積を `false` に畳むので **使わない**。
- 例外メッセージから `Cell_{x}_{y}` / 「セル identity」を消す。
  距離政策の候補であることだけを書く。
- `SceneDirector.Transitions` から `CellIdentity` 参照 0。
- `StreamByDistance` の既定は `false`。テストはフラグを明示して立てる。

---

## 4. A-1〜A-4

| ファイル | 現在 | 予想上限 | 責務 / 判断 |
|---|---:|---:|---|
| `Scripts/Runtime/SceneSystem/SceneDirector.Transitions.cs` | 242 | 270 | ガードをフラグへ。新責務なし。第二ガードを足さない |
| `Scripts/Runtime/SceneSystem/SceneDirector.cs` | 259 | 259 | `ISceneVolumeQuery` 実装は触らない |
| `Tests/Scene/SceneDirectorTransitionTests.cs` | 166 | 360 | R-3 受入の正本。フラグ true のリソースを Map に載せる |
| `Tests/Scene/CellSceneTests.cs` | 323 | 250 | R-3 ブロック削除。CellScene / `IsCellId` 契約だけ残す |
| `Tests/Scene/SceneDirectorTestBase.cs` | 200 | 200 | 触らない（Format は M-4） |

A-2: 分割なし。Transitions は既に部分クラス。
A-3: 新責務なし。
A-4: 既存 R-3 2 本を移設し、フラグ off / 空体積 / 任意名 / 副作用なしを追加（§6）。

---

## 5. 受け入れ条件

1. `ThrowIfCellIdentity` 相当がインスタンスメソッドでフラグだけを見る。
2. `SceneDirector.Transitions` に `CellIdentity` が無い。
3. フラグ true なら任意名でも from / to とも拒否。空体積でも拒否。
4. フラグ off の `Cell_0_0` は名前だけでは拒否されない。
5. `Title` / `PlayerScene` 相当（フラグ off）は R-3 で拒否しない。
6. 失敗時に履歴・LoadingDisplay・既存シーン・to のロード状態が変わらない。
7. 例外メッセージに `Cell_{x}_{y}` / 「セル identity」が無い。
8. 本番 asset に差分がない。公開 API / DI / asmdef を増やさない。

---

## 6. テスト要求

`CellSceneTests` の `SwitchScene_WithCellIdentity_*` を削除または
「CellScene の契約だけ」に削る。残すなら R-3 を主張しない。

`SceneDirectorTransitionTests` へ移し、次を Map 上の `StreamByDistance` で証明する。

- フラグ true の任意名（例: `Valley`）への / からの `SwitchScene` が
  `InvalidOperationException`。シーンをロードしない。
- フラグ true かつ体積空でも拒否。
- フラグ off の `Cell_0_0` は R-3 例外を投げない。
- フラグ off の `Title` / `PlayerScene` 相当は R-3 例外を投げない。
- 失敗後、履歴件数、LoadingDisplay、既存シーン、to のロード状態が不変。
- `GoBack` / `ExecuteTransitionPlan` も同じ例外（第二ガードを足さずに通る）。

Phase C: 構造レビュー →
`pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Scene.SceneDirectorTransitionTests` →
`pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.Scene.CellSceneTests` →
`docs-audit.ps1` → 全 EditMode。

---

## 6.1 実装制約

M-2 HANDOFF §6.1 と同じ（`#nullable enable` / `record` 禁止 / 偽 null 禁止 /
asmdef 追加禁止 / `SceneState` 14 値 / `Task.Delay` 禁止 / YAML 手編集禁止）。
衝突したら Phase B を止める。

---

## 6.2 モデル運用

移行 HANDOFF に従う。Phase C は **Grok 4.6**。writer 1 名。無応答だけで再送しない。
C' に Grok 系列を使わない。満たせなければ独立監査済みと書かない。

---

## 6.3 Phase B 実績

- 担当 / モデル:
- 実装結果:
- 実行しなかった事項:
- HANDOFF との差異:

---

## 7. Phase C 実績

未着手。担当モデルは Grok 4.6。

---

## 8. Phase C' 実績

未着手
