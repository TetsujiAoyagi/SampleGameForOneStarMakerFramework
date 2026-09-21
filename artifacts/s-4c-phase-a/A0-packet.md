# S-4c World Lighting — A0 planning packet

- type: slice input
- slice: S-4c
- created: 2026-09-21
- implementation base (planned branch point): `553b7b150e13245b369d75dc4baa12d86e9559aa` (`origin/develop`)
- input sources: `docs/handoff/S-4_FULL_SPEC_WORLD_AUTHORING.md` §5 / §10 / §11, `docs/handoff/SEASON_WORLD_DESIGN.md` N-2, Architecture §24 REN-08 / D-1 / §9, Architecture §23 §8 Volume 境界, Architecture §5.13 companion 分類, 現行コード調査
- このファイルは A1/A2 の共通入力である。A2 の代替構成レビューは A1 を読まない。

## 1. 現況

S-4a / S-4b は develop へマージ済み。四季は同じ AABB の 9×6、Spring 起動、職種 companion、World Workspace が存在する。

Lighting 関連の現物:

- Season Lighting Scene は 4 件ある（`Spring_Lighting` 等）。`LoadType.NecessaryAlways`。payload は空の `SeasonLightingRoot` のみ。Directional Light / Volume / LightingSettings は無い。`RenderSettings` は Unity 既定値。
- `SeasonLightingScene` は Factory が未知 identity で null を返さないための空 scaffold。lease は未実装。コメントで S-4c に明示延期。
- Cell Lighting companion（`Spring_Lighting_{x}_{y}`）は **1 件も無い**。World Workspace の Lighting 職種は optional 作成できる。作成物は Empty Scene、`LoadType.OnDemand`、`StreamByDistance=false`。
- `PlayerScene.ApplyDemoLook()` が Play 準備完了後に `RenderSettings` の fog / ambient を直書きする。Camera clear color もここで上書きする。コメントは「環境オーナーは将来 Environment 子シーン側へ」であり、program 正本（Season Lighting + RenderEnvironment）と食い違う。
- `IRenderEnvironment` / `RenderEnvironmentState` / lease は未実装。`IRenderingSystem` / RenderWorld / BRG も未実装。Architecture §24 は構想のまま。
- `OneStarMaker.Runtime.asmdef` は URP / RenderPipeline Core を参照していない。CameraSystem は `VolumeProfile` を `UnityEngine.Object` として扱い、Host の URP Volume 反映は未完。
- Runtime 公開ログ抽象は `ILogger<T>`。`record` 禁止。`#nullable enable` 必須。Unity 偽 null は `== null`。
- `GameSceneFactory` は `ICameraSystem` と同様の constructor 注入で Scene へ依存を渡す。Composition Root は `AppInitializer`。
- 代表区画 Spring `(4,2)` `(5,2)` の Cell / Environment / Whitebox は存在する。baked GI データは無い（`m_LightingDataAsset` は空 GUID、`m_LightingSettings` は `{fileID: 0}`）。
- S-4b の一時生成器・旧 policy・`WorldGridDefinition` は撤去済み。復活させない。
- SEASON_WORLD_DESIGN N-2「RenderSettings の適用主体」は S-4 で決めるとある。S-4_FULL_SPEC は SeasonLightingScene + RenderEnvironment に決めている。

## 2. 要求（program 正本から転記）

S-4_FULL_SPEC §5:

1. Architecture §24 REN-08 を前倒しし、Framework へ `IRenderEnvironment`、`RenderEnvironmentState`、所有 lease を追加する。
2. App lifetime の RenderEnvironment は同時に 1 つの Season owner だけを受け付ける。
3. `SeasonLightingScene` が Load 時に lease を取得し、Unload 時に解放する。
4. 二つ目の owner を有効化しようとした場合は即失敗する。
5. 古い lease の遅延 Dispose が新 owner を消さない。owner token を照合する。
6. PlayerScene 等から `RenderSettings` 直接変更を除き、global state を RenderEnvironment へ集約する。
7. Camera 固有 Volume は CameraSystem、全 View 共通 Volume は RenderEnvironment。
8. 純 C# policy は preset から sun rotation / color / intensity、ambient、fog、global Volume weight を算出する。URP adapter だけが `Light` / `RenderSettings` / `Volume` へ反映する。
9. Cell Lighting は local light、Reflection Probe、local Volume を所有し、global sun / sky / fog / global Volume を書き換えない。必要な Cell だけに作る。
10. Spring `(4,2)` と `(5,2)` を代表区画として、Spring Lighting、両 Cell、Environment、Cell Lighting を同時に開き multi-scene bake する。
11. Unity の multi-scene bake は shadow / GI bounce を跨ぎ、lightmap / realtime GI は Scene 単位で着脱する。同時ベイクした Light Probe data は共有される。この性質を受け入れ条件と S-9 のメモリ計測項目に書く。
12. commit するのは代表 2 Cell のベイクデータだけ。全 216 Cell のベイクと Probe 方式の再評価は S-9。

S-4_FULL_SPEC §11 検証:

- RenderEnvironment の単一 owner と stale lease 耐性の純 C# テスト。
- Spring 代表 2 Cell で明白な lightmap seam がない。
- Cell unload / reload 後に Lighting state と baked data が復帰する。

公開 API 追加は Framework では `IRenderEnvironment`、`RenderEnvironmentState`、所有 lease に限定。Framework は season 名、Cell 座標文法、職種 role を知らない。`SceneState` 14 値は変えない。新しい LoadType は足さない。

SEASON_WORLD_DESIGN:

- 描画トーンは RenderSettings（霧・環境光・太陽角）+ 既存 tint。新メッシュ・新シェーダ・新パイプラインは投入しない。
- N-2 をこのスライスで閉じる。

## 3. 常時契約

- Game → Framework 一方向。asmdef 参照追加は設計判断。無断で足さない。
- アセットは `IAssetManagement` + `AssetOwner`。
- 公開ログは `ILogger<T>`。
- Unity 側 C# で `record` 禁止。新規/編集 `.cs` は `#nullable enable`。
- Unity 偽 null は `== null` / `!= null`。
- Scene / Prefab / Addressables は Editor 経由。接続できる Editor があるとき `.unity` YAML を手編集しない。
- Cloud に Editor が無い場合、Unity CLI を叩かない。C# と宣言だけ書く。
- テストで `Task.Delay` / `Thread.Sleep` 禁止。
- Phase B は `pwsh tools/contract-audit.ps1` まで。Unity バッチテストと Addressables build は Phase C。
- 一時生成器を復活させない。日常の World Workspace だけを使う。

## 4. 対象外

- `IRenderingSystem` / RenderWorld / BRG / 判定ゲート（REN-01〜07）。
- 24 時間 TimeOfDay 時計、Weather、GI 制御の合成フレームワーク（§24 P-R6）。
- 全 216 Cell のベイク、Light Probe 方式の再評価（S-9）。
- VFX Graph、Season Atmosphere VFX、Events、major Event（S-4d）。
- Tunnel / 季節遷移シーケンス（S-5）。S-4c は二重 Season を fail-closed にするだけで、遷移演出は書かない。
- 1 季節 1 Addressables group（S-6）、Variant checkout（S-7）、HandAuthored 昇格（S-8）。
- CameraSystem の View 固有 Volume 実装の完成（CAM-07 Play 目視は未了のまま触らない）。
- 新しい LoadType、`SceneState` 値の追加/並べ替え。
- Framework への季節語（Spring 等）の導入。
- ParticleSystem fallback、新シェーダ、新メッシュ。
- Whitebox Cell のベイク。
- S-4d が所有する VFX / Events Scene の作成。

## 5. このスライスが答える問い

季節グローバルな見た目状態（sun / ambient / fog / 共通 Volume）を、App lifetime の単一 RenderEnvironment が lease で所有でき、代表 2 Cell の multi-scene bake が Scene 単位の着脱として成立するか。

## 6. 次スライスへ進む最低条件（A0 時点の骨子。詳細は A1）

1. 純 C# で単一 owner・二件目即失敗・stale lease が新 owner を消さないことを示せる。
2. PlayerScene を含む Game 層から `RenderSettings` 直書きが消えている。
3. 四季の Season Lighting が lease 経由で preset を適用し、Unload で解放する。
4. Spring `(4,2)` `(5,2)` に Cell Lighting があり、global を書き換えない。
5. 代表区画の baked lightmap が commit され、明白な seam が無いことを人が記録する。
6. Cell unload / reload 後に lighting state と baked data が戻ることを Phase C が観測できる準備がある。

## 7. 後続スライスへ送る問い

| 問い | 所有 |
|---|---|
| Volume.weight を URP 型で Framework adapter が直接書くか。asmdef に Core RP を足すか | 本スライスで A1 が決める。Core RP を足さない場合は後続（RenderEnvironment Volume 反映） |
| Light Probe 共有のメモリ費用と方式再評価 | S-9 |
| 全 Cell ベイク、品質バー（季節が 3 秒で分かる）の目視完成 | S-8 / S-9 |
| 季節遷移中の lease 受け渡しをシーケンスに載せる | S-5（本スライスは二重 owner を失敗させるだけ） |
| Cell Lighting 4_2 / 5_2 の見た目作り込み、VFX、Events | S-4d が VFX/Events。Cell Lighting の作成そのものは bake 入力なので本スライス候補 |
| TimeOfDay 時計 | REN-08 残り / P-R6。本スライスは静的季節 preset |
| Camera clear color / skybox material の季節連動 | 本スライスで最小を決め、残りは後続 |

## 8. 既知の緊張（A1 が裁定する未決）

1. **Cell Lighting 作成の所有。** S-4d §6.2 は `Spring_Lighting_4_2` / `Spring_Lighting_5_2` を S-4 完了時 6 Scene に含めている。S-4c §5.3 は bake 時に Cell Lighting を同時に開くと書く。両方を満たすには S-4c が 2 Scene を World Workspace で作る必要がある。
2. **URP asmdef。** spec は URP adapter が Volume へ反映すると書く。Runtime は URP を参照していない。足すか、authored Volume を Scene ロードに乗せ policy の weight は記録だけにするか。
3. **Directional Light の所有者。** Host 常駐 vs Season Lighting Scene 内の authored Light。bake と Play の光源を一致させるなら Scene 内が自然。
4. **preset の置き場。** Framework が季節を知ってはならないので、数値表は SampleGame。Framework は state の検証と lease と sink 適用だけ。
5. **lease 取得タイミング。** Unity Light を bind するなら `OnLoadedImpl`（RootObjects あり）。`OnStabledImpl` では遅すぎて、NecessaryAlways の見た目が Player 入力解禁まで空白になり得る。解放は `OnPreUnLoadedImpl`（オブジェクト破棄前）。
6. **N-2 と PlayerScene。** RenderSettings 直書き削除は必須。Camera background は CameraSystem 所有のまま残すか、Season が触るか。
7. **Bake の実行主体。** 人間 Phase B がローカル Editor で行う。Cloud は Editor を叩かない。HANDOFF に手順を自己完結させる。

## 9. リスク

high。公開 API、所有者・寿命、Scene / Addressables、multi-scene bake、asmdef 判断を含む。C' 用に Phase A で全モデル系列を使い切らない。
