# S-4c A2 — A0 のみの代替構成

- 入力: A0 packet + spec §5/10/11 + Architecture §24/§23。**A1 未読**
- 担当: 独立エージェント（同一セッション・Grok 系 inherit）

## Alternative A（推奨・レビュー側）

CameraSystem 同型。Play の Directional Light は `RenderEnvironmentHost`（DontDestroyOnLoad）。Season Lighting Scene の Light は bake sink。公開 API は `IRenderEnvironment` / `State` / `Lease` の 3 型。`BindSun(Light)` を公開面に出さない。URP は足さない。Cell Lighting 2 件は S-4c が作る。

## Alternative B（レビュー側が却下）

Play も Scene authored Light。Game が RenderSettings を書く、またはこのスライスで URP を足す。

## A1 との衝突（レビューは A1 を知らないが、統合側が照合する）

A1 は Scene authored `SeasonSun` + `BindSun` + Host 太陽なし。代替 A は Host 太陽。A3 で採否を決める。
