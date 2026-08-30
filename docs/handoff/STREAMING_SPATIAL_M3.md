# Streaming 空間政策 M-3 HANDOFF — R-3 を候補フラグへ

> ステータス: **Phase C' 完了。ローカル commit 待ち。M-2 commit 後。**
> 上位計画: [STREAMING_SPATIAL_MIGRATION.md](STREAMING_SPATIAL_MIGRATION.md)
> 到着契約: [§34 OnDemand の空間政策](../../unity/Assets/Docs/Architecture/34-ondemand-spatial-policy.md)
> 現状仕様: [STREAMING_CURRENT_SPEC.md](../streaming/STREAMING_CURRENT_SPEC.md)
> harvest 先: `STREAMING_CURRENT_SPEC.md` の現況 / R-3 残件、§21 の R-3 行、§34 冒頭の「候補フラグ R-3 未実装」を追随。§21 の旧チケット履歴は履歴と明示する。マージ時に本書を削除する。

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
if (string.IsNullOrEmpty(sceneIdentify)) return;
var resource = _sceneResourceMap.GetSceneResource(sceneIdentify);
if (resource == null) return;          // 未登録・破棄済みは拒否しない
if (!resource.StreamByDistance) return; // フラグ off は拒否しない
throw new InvalidOperationException(...);
```

- from / to の双方を、span・LoadingDisplay・履歴・Unload / Add より前に見る。今と同じ位置。
- `fromSceneIdentify` は null 可なので、Map lookup より前に null / empty を return する。
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
| `Tests/Scene/SceneDirectorTransitionTests.cs` | 166 | 450 | R-3 受入の正本。フラグ true のリソースを Map に載せ、GoBack / plan / 空体積 / 破棄済みも受け入れる |
| `Tests/Scene/CellSceneTests.cs` | 323 | 280 | R-3 ブロックと専用 helper を削除。約 273 行見込み。CellScene / `IsCellId` 契約だけ残す |
| `Tests/Scene/SceneDirectorTestBase.cs` | 200 | 200 | 触らない（Format は M-4） |

A-2: 分割なし。Transitions は既に部分クラス。
A-3: 新責務なし。
A-4: 既存 R-3 2 本を移設し、フラグ off / 空体積 / 任意名 / 副作用なしを追加（§6）。

### Phase A 再検証記録

M-2 commit 後の現コードを Grok 4.6 の新規 CLI セッションで再検証した。フラグ判定、query 不使用、
第二ガードなし、公開 API / DI / asmdef 非追加の設計は維持する。事実補正として、null 可の from、
実 namespace、現実的なテスト行数、GoBack / TransitionPlan の fixture 手順、例外テンプレートを本文へ追加した。
M-2 C' のグリッド縮小時 HandAuthored 残件は生成器 / Addressables の論点であり、M-3 の範囲を広げない。
修正版は別の新規 Grok 4.6 CLI セッションが現コードと再照合し、Phase A PASS・残ブロッカー 0 と判定した。

---

## 5. 受け入れ条件

1. `ThrowIfCellIdentity` 相当がインスタンスメソッドでフラグだけを見る。
2. `SceneDirector.Transitions` に `CellIdentity` が無い。
3. フラグ true なら任意名でも from / to とも拒否。空体積でも拒否。
4. フラグ off の `Cell_0_0` は名前だけでは拒否されない。
5. `Title` / `PlayerScene` 相当（フラグ off）は R-3 で拒否しない。
6. 失敗時に履歴・LoadingDisplay・既存シーン・to のロード状態が変わらない。
7. 例外メッセージのテンプレートに `Cell_{x}_{y}` / 「セル identity」が無い。実 identity の引用は可。
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
- 未登録 identity と、Map 登録後に破棄された `SceneResource` は R-3 では拒否しない。

GoBack は公開履歴注入 APIを足さない。Title / Valley をフラグ off で通常遷移して履歴を作り、
その後 `Valley.StreamByDistance = true`（既存 internal setter、テスト asmdef から到達可）として
`GoBack` を呼ぶ。失敗後も履歴件数、Valley のロード、Title の未復帰、LoadingDisplay が不変であることを確認する。

`ExecuteTransitionPlan` はテスト内 `SceneBase` 派生型の `CreateTransitionPlan()` から
`NextSceneId = "Valley"` を返す。第二ガードを追加せず公開 `SwitchScene` → `SwitchSceneCore` を通ることを証明する。
fixture は `CreateInstance` と既存 helper だけで作り、本番 asset を変更しない。空体積は zero-size Bounds を使い、
`ISceneVolumeQuery` の結果に依存せず拒否されることを確認する。

Phase C: 構造レビュー →
`pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.SceneSystem.SceneDirectorTransitionTests` →
`pwsh tools/run-tests.ps1 -Filter OneStarMaker.Tests.SceneSystem.CellSceneTests` →
`docs-audit.ps1` → 全 EditMode。

---

## 6.1 実装制約

- 新規・編集する Unity C# は先頭に `#nullable enable`。
- Unity C# で `record` を使わない。
- 破棄されうる `UnityEngine.Object` は `== null` / `!= null`。`?.` / `??` / `is null` / `ReferenceEquals` を使わない。
- Editor コードを Runtime asmdef に置かない。asmdef 参照を追加しない。
- `SceneState` の 14 値を減らさず、並べ替えない。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。
- `.unity` / `.prefab` / `.asset` YAML を手編集しない。
- HANDOFF とコードが衝突する、新しい設計判断が必要なら Phase B を止める。

---

## 6.2 モデル運用

移行 HANDOFF に従う。Phase C は **Grok 4.6**。writer 1 名。無応答だけで再送しない。
C' に Grok 系列を使わない。満たせなければ独立監査済みと書かない。

---

## 6.3 Phase B 実績

- 担当 / モデル: Phase B 実装担当 1 名 / GPT-5.6 Luna。
- 実装結果: `SceneDirector.Transitions.cs` の既存 R-3 ガードをインスタンスメソッド
  `ThrowIfStreamByDistanceCandidate` へ置換した。null / empty は map lookup 前に return し、
  `SceneResourceMap.GetSceneResource` の結果を Unity の `== null` で判定し、
  `StreamByDistance` が true のときだけ、span・LoadingDisplay・履歴・Unload / Add より前に
  距離政策候補の例外を送出する。`CellIdentity`、`ISceneVolumeQuery`、第二ガード、公開 API /
  DI / asmdef は追加していない。`SceneDirectorTransitionTests` に任意名 Valley、空体積、
  flag off の Cell_0_0 / Title / PlayerScene、失敗時の副作用不変、GoBack、TransitionPlan、
  未登録 / 破棄済みリソースの受入を追加し、`CellSceneTests` から R-3 テストと専用 helper を削除した。
  実装後行数は Transitions 256、SceneDirectorTransitionTests 371、CellSceneTests 271、
  SceneDirectorTestBase は未変更 200 行（各上限 270 / 450 / 280 内）。
- 実行しなかった事項: Unity.exe の起動・接続、`pwsh tools/run-tests.ps1`（指定フィルタおよび全 EditMode）、
  `pwsh tools/docs-audit.ps1`、Addressables ビルド、`unity test` / `unity run`。静的に `git diff --check`、
  行数、対象ファイルの禁止語・`CellIdentity` 参照（CellScene の契約を除く）を確認した。
- HANDOFF との差異: なし。Phase C の構造レビューとテスト実行は未着手。

---

## 7. Phase C 実績

- 担当 / モデル: Cursor Agent 新規セッション / Grok 4.6。Phase B の GPT-5.6 Luna と異なるモデルで独立性を充足。
- 構造レビュー: 指摘 0。単一ガードの副作用前配置、null / empty と Unity 偽 null、`StreamByDistance` のみの判定、query / 第二ガード / 公開 API / DI / asmdef 非追加、要求ケース、行数 256 / 371 / 271 を確認。
- 対象テスト: `SceneDirectorTransitionTests` 14/14、`CellSceneTests` 7/7、failed 0。両 filter はテスト完了後の Unity 終了時クラッシュでスクリプト -1 だったが、完成 XML とログの `Test run completed. Exiting with code 0 (Ok).` により成功判定。
- 全 EditMode / docs audit: 523/523 failed 0、Unity exit 0。docs audit は 50 Markdown / 11081 行、検査1・2違反 0。M-2 HANDOFF の harvest 警告 1 件のみ。
- 未確認事項: Addressables build、本番 asset 変更、Play 経路は対象外のため未実行。
- 結論: **Phase C PASS**。ブロッキング指摘 0。Phase C' 待ち。

---

## 8. Phase C' 実績

- 担当 / モデル: Claude CLI 新規セッション / `claude-opus-5`（Claude Opus 5、Anthropic）。Phase B の GPT-5.6 Luna、Phase C の Grok 4.6 と異なるモデル・系列・ベンダーで、会話履歴を使わず独立性を充足。
- 監査結果: **PASS**。受け入れ条件 1–8、単一ガードの全入口、Unity 偽 null の実効的な受入、null / empty 順序、空体積、GoBack / TransitionPlan、例外テンプレート、本番 `SceneResource` のフラグ分布、行数・禁止変更を一次資料から再検証。ブロッキング指摘 0。
- テスト証拠: C' は Unity を起動せず、Phase C の XML / log を直接確認して 14/14、7/7、全 523/523 failed 0 と、ソース更新時刻が実行前であることを確認。filter の crash は `Test run completed` と完成 XML より後、全件は crash なし。docs audit は C' が読み取り専用で再実行し、50 Markdown、検査1・2違反 0、M-2 harvest 警告 1、exit 0 を再現。
- 非ブロッキング指摘: Phase D harvest 先に §34 冒頭と現状仕様の R-3 残件を明記すること、§21 の旧チケット履歴にある `CellSceneTests` 8 本 / R-3 1 本を現況と誤読させないこと。本書の harvest 行へ反映済み。
- 残存リスク: R-3 は登録済みで生存中かつ `StreamByDistance = true` のデータに依存する。未登録 `Cell_*` は R-3 を通過して後段で副作用後に失敗し得るが、HANDOFF の明示決定であり現本番16セルは全て登録・flag true。Addressables / Play / generator 再実行は対象外で未検証。
- 結論: **Phase C' PASS**。M-3 はローカル commit 可能。Claude 側のリポジトリ編集は無い。
