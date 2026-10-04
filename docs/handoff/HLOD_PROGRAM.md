# HLOD / Proxy 導入 program

- type: `program`
- status: **Phase A1 r3。r2 A2確認と追加PRレビューの明確化を反映、A3未凍結・未実装。** 各段の着手には個別 HANDOFF と A2 / 人間 A3 が必要。
- owner: 主担当エージェント（計画・実装調整）、発注者（A3 / D 判断）
- created: 2026-10-04
- expires / harvest 期限: 2026-11-04 に継続要否を見直す。各段のマージ時に実証済み契約だけ harvest し、全段の完了・中止時に本書を削除する。
- harvest to: `unity/Assets/Docs/Architecture/24-rendering-system.md`、必要な実変更があれば `docs/streaming/STREAMING_CURRENT_SPEC.md`
- 調査 base: `2c29c99806788406551affba6cc795e67e614748`（`develop`）
- 最初のスライス: [resident pilot](HLOD_RESIDENT_PILOT.md)

## 1. 目的と出発点

近景の複数 detail 群を、遠景では同じ範囲を代表する一つの手製 proxy に置き換える。まず**何を描くか**を決め、資産のロード・解放と分けて確かめる。HLOD の親は表示の coverage を集約する親であり、OSM Scene の寿命依存の親ではない。

確認済みの現況:

- `SceneResource` / Scene graph の parent は寿命・依存の構造。表示距離の HLOD tree として流用しない。
- Content Directory の Scene 台帳は identity 単位。同一 Scene identity を別 representation で同時ロードする要求と deferred activation は拒否される。near / proxy を既存 Variant の単純切替えと見なさない。
- 現在の Cell には地面の Renderer と Collider が同居する。Scene / GameObject の非表示・解放を描画切替えに使うと物理や gameplay も巻き込む。
- Cell companion は構造 Cell のロードに追随し、設定に応じた役割を追加する。既存 companion 一括変換を初手にしない。
- Rendering は最小 `RenderEnvironment` lease / sink が実装済み。RenderWorld / BRG は構想段階で、HLOD pilot の前提にしない。

根拠: `SceneSystem/SceneResource.cs`、`AssetManagement/AssetManagement.ContentDirectory.cs`（いずれも `unity/Assets/OneStarMaker/Scripts/Runtime/`）、`unity/Assets/SampleGame/InGame/InGameSession/World/CellScenes/CellScene.cs`、`unity/Assets/Docs/Architecture/24-rendering-system.md`、`docs/streaming/STREAMING_CURRENT_SPEC.md`。

## 2. 段階と次へ進む証拠

| 段 | 答える問い / 最小成果 | 次段へ渡す証拠と境界 |
|---|---|---|
| **1: 全常駐 resident pilot** | 2 detail 群と1手製 proxy を明示 manifest で結び、通常の MeshRenderer だけで hard cut + hysteresis が成立するか | coverage / Renderer membership / bounds、登録・解放、main-thread pre-render swap、Collider / gameplay 不変を小さな fixture で確認。実画像と boolean 状態の証明を区別。ロード・解放なし。個別 HANDOFF が現在の作業単位 |
| **2: proxy の非同期ロード** | detail 常駐のまま proxy だけを `IAssetManagement` 経由で準備できるか | 完全に ready になるまで detail を保持。失敗・キャンセル・古い完了・owner 終了で表示を壊さない。proxy 資産 owner / 解放責任をここで決める。detail unload はまだ行わない |
| **3a: fixture の visual 所有分離** | detail visual と Collider / gameplay の寿命を、限定 fixture で分けられるか | 同一 GameObject / Scene を消すだけの分離を禁止。visual の登録・解放と常駐 gameplay の独立を確認。本番 companion 全体の移行は含めない |
| **3b: detail のロード・解放** | visual 所有分離後、detail を実際に退避・復帰できるか | near 要求時も全 coverage の detail が ready になるまで proxy を保持し、一括 swap 後に旧 visual を解放。far も ready な proxy への swap 後に detail を解放。失敗時の旧表示保持、再入場、反転、キャンセル、実測メモリを確認 |
| **4: 任意の階層化** | 複数親階層が実際の規模・分布で必要か | 2段の実測で必要性が出たときだけ追加。coverage の重複・欠落を防ぐ選択規則を別途設計。Scene parent、地理配置、描画 tree の同一視をしない |
| **5: 任意の baker** | 手製 manifest / proxy の制作コストが自動化を必要とするか | 手製データで成立した入出力契約を使う。生成対象・保護領域・再生成・Material / lighting・品質基準を別 HANDOFF で固定。Unity Mesh LOD / BRG / 自動簡略化を先取りしない |

段2以降の API・状態 schema・ファイル構成は、その段の Phase A で必要最小限に決める。段1で汎用 streaming framework、RenderWorld、巨大な readiness / request 状態機械を作らない。段4 / 5 は実測と制作上の必要性がなければ実施しない。

## 3. 表示と寿命の共通境界

- 手製 manifest は初手から必須。detail group ID、対象 Renderer の明示リスト、proxy が覆う group ID 集合、各 group / 集約 bounds、距離閾値を持つ。階層探索だけで membership を推定しない。
- 表示の切替え単位は coverage 全体。2群の片側だけと proxy の混在を通常状態にしない。後続段でも readiness は必要な coverage 全体で判定する。
- 表示 owner と asset owner を区別する。段1は fixture が resident object の表示だけを借り、アセットロード API を呼ばない。段2以降の資産取得は `IAssetManagement` と明示 `AssetOwner` に従う。
- 描画を止めること、資産が未ロードであること、Scene が非アクティブであることは別の事実。`SceneState` を描画 LOD の状態にしない。
- main camera / static opaque の限定を段1で明示する。複数 camera、transparent、動的物体、skinned mesh、影・反射・GI の切替品質は必要になった段で扱う。

## 4. 品質・性能の読み方

段1の小さな fixture は制御契約の反証を探す最小例であり、製品の視覚品質や性能改善の証明ではない。Renderer.enabled の排他性だけで穴・二重像が見えないとは言わず、代表の描画画像も残す。一方、数枚の画像だけで全フレームの排他性を証明したとも言わない。

- 段1: 距離往復・境界揺れ・切替フレームの状態記録と代表画像。draw call / triangles の変化は参考観測に留める。全常駐なのでメモリ削減を主張しない。
- 段2 / 3b: 同じ target、camera 経路、内容、条件でロード時間・frame time・resident memory を記録。平均だけでなく切替時のスパイクを調べる。
- 実コンテンツの採用判断前: 代表地域、許容 silhouette / pop / lighting 差、比較条件、CPU / GPU / memory の予算を別 Phase A で合意する。小 fixture の GO を製品採用 GO に読み替えない。

## 5. 現在の着手・未決事項

この branch は段1の A1 文書化まで。r1 の独立 A2 で見つかった試験host競合を、既存実アプリhost / main cameraを借用するr2案へ修正した。r2のarchitecture再確認と独立した受け入れ境界レビューを実施し、proxyをvisual-onlyに限定する条件も追記した。r3で追加PRレビューを照合し、表示ownershipの保持者・項目寿命とbounds入力述語を明確化した。人間 A3 の採否・検証経路合意、Phase B 実装、C / C' は未実施である。会話上の「最初の一歩を進める」は、新しい A3 条件の提示前の承認として扱わない。

現在の cloud 作業環境には Unity Editor がなく、既存 GitHub CI は DebugStudio の .NET 検証のみ。Unity 実行・画像取得の経路は未成立。候補は許可済みの Unity 搭載 executor または発注者が選ぶ既存 runner であり、人間に毎回手動テストしてもらう前提にはしない。段1 HANDOFF で担当・環境・最初の確認地点・不成立時の扱いを A3 前に確定する。
