# S-4b 四季 World 生成 — A0〜A3 と寿命契約の再開事項

> type: slice
> status: P1 の Phase B 指摘修正済み。旧 Phase C evidence は stale、再レビュー待ち。C' は人間。この PR は P1 生成器のみ。P2 / 起動配線 / P3 は worktree `codex/s-4b-a0` に残す。
> branch: `codex/s-4b-p1`
> implementation base commit: `17dc67b232433e8ff3f909d99e401cddb159de50`（この PR の develop merge-base。HANDOFF 旧記の `0792edc` は PR #45 時点）
> implementation head commit: `64d21c575d587004033351bb8ec77b3ec6df5b31`（指摘修正。旧 `f3597adc26556624dc5aa0b0acccc2e7dfdf6ee7` の Phase C evidence は stale）
> risk: high（明示ワイプ、652 Scene、Addressables、起動順序）
> owner: 発注者 / S-4b 担当
> A0 担当: Codex / GPT-6 / OpenAI。
> A1 担当: Cursor / Grok 4.6 / xAI。作業ツリー `codex/s-4b-a0`（worktree）。
> A2 観点1: GPT / OpenAI（architecture-gates）。入力は未コミット A1、HANDOFF SHA-256 `1D1DF89E7649FAF7CE4A726B93ABE7E731514669EA1FFA64A5F630B776C24FAC`。
> A2 観点2: GPT / OpenAI（破壊的生成・manifest・復旧・全件検査）。同じ A1 版。観点1の指摘は渡していない。
> A2 revision 2: Codex CLI / Astra / 軽い推論。観点を分けた限定レビュー。入力 HANDOFF SHA-256 `A3F9C0115121A2F338A9B7C8A8BBC78F3D5603F8D88DEA85B4A36F67E7D3F31D`、code HEAD `1cce49a`。互いの指摘は渡していない。
> A3 人間回答: 2026-09-10。F1 寸法と L1 空 Lighting は生成仕様。`Seasons/Materials` は共有 Material のみ（参照は共有 Texture / Shader に限る）。体積のメートル値は製品バーではなく検査の緩い閾値。季節の並行要求は拒否。
> A3 revision 2: Cursor App / Grok 4.6。Astra 指摘 8 件を採用（§7.21）。製品判断と旧 A2 採用は維持。
> 継続担当（今回の人間指定）: Cursor App の Grok が主担当。Codex CLI の Astra（軽い推論）へ限定した独立レビューを依頼する。C' と D は人間のまま。
> created: 2026-09-09
> expires: S-4b Phase D で harvest・削除。
> harvest to: Architecture §04 / §05 / §18 / §21 / §27、STREAMING_CURRENT_SPEC。後続 program へは必要な境界だけ反映。
> Phase A frozen snapshot path: `docs/handoff/S-4b_WORLD_GENERATION_A0.md`（本凍結 commit。設計本文の以後の変更は revision 3）
> Phase A snapshot generated at: 2026-09-10
> Phase A snapshot git / hash: `137d235e78416a3cda59aa2474b6147a29081595`。implementation base は PR #45 マージ後の `0792edc`。生成器・P2・起動配線は未着手。B result / C・C' evidence は未生成。
> evidence bundle path / id: `docs/handoff/evidence/s-4b-p1-c`（旧 implementation head 用。stale）
> evidence bundle generated at: 2026-09-11T21:18:39Z
> evidence bundle hash: `43a81d5d9c186ab4baddadbd80d7f7bf5cf28bb3ac44485092437c5b40c8a214`（diff/stat/commits）。機械検査込み `052843482ff76291a5d3285897fff32052cb0bfa154a603894380dff0c3ebf56`
> C' blind bundle path / id: `docs/handoff/evidence/s-4b-p1-cprime-blind`（旧 implementation head 用。使用禁止）
> C' blind bundle generated at: 2026-09-11T21:20:00Z
> C': 当面人間。Claude は再開しない。cursor-agent は Grok 系のみ。AI は証拠と手順を準備し、本人回答前に PASS / 完了を書かない。C' 判定と D のマージ判断は分けて記録する。

## 入力と優先関係

- 文書運用は [docs/README](../README.md)、常時契約は [AGENTS.md](../../AGENTS.md)。`osm-workflow` と参照先 phases-and-handoff / docs-policy / handoff-template / architecture-gates / review-evidence を全文確認した。Editor 制約も読んだが、今回は接続・操作していない。
- 要求は [S-4 program](S-4_FULL_SPEC_WORLD_AUTHORING.md) §1〜4 / §10〜12、世界構図は [SEASON_WORLD_DESIGN](SEASON_WORLD_DESIGN.md) §2〜4。program を直接 B へ渡さず、A1〜A3 で自己完結した HANDOFF にする。
- 現況は [Streaming 現状仕様](../streaming/STREAMING_CURRENT_SPEC.md)、Architecture [§04](../../unity/Assets/Docs/Architecture/04-app-startup.md) §4.8、[§05](../../unity/Assets/Docs/Architecture/05-scene.md) §5.13、[§18](../../unity/Assets/Docs/Architecture/18-asset-description.md)、[§20](../../unity/Assets/Docs/Architecture/20-variant-checkout-workflow.md)、[§21](../../unity/Assets/Docs/Architecture/21-scene-streaming.md)、[§27](../../unity/Assets/Docs/Architecture/27-folder-structure.md) §6 と現在コードを照合。空間の到着契約は [§34](../../unity/Assets/Docs/Architecture/34-ondemand-spatial-policy.md)。§34 冒頭に残る S-4a 前の名前依存の列挙は現況判定に使わない。
- 後発の承認済み program により、9×6 は確定、時間による縮小なし。Spring 自動ロード＋源流スポーンは確定。Lighting の global owner は S-4c。職種 Scene は optional、Environment は全件。大量生成器・旧 policy・専用 tests は生成後削除。世界稿の N-1 / N-2 / N-5 / N-8、縮小案、生成器保持・再生成実演の古い記述を再承認事項にしない。再生成実演に関わる公開文書の整合は削除時の harvest 対象。

## 1. 現況と変更する境界

開始時は `codex/human-cprime-workflow`。HEAD、local develop、origin/develop、`git ls-remote origin refs/heads/develop` が上記 SHA で一致。S-4a 実装は PR #43、harvest は PR #44 でマージ済み。旧 slice HANDOFF は削除済みで復活させない。公開記録は EditMode 681/681 passed・failed 0。今回の再実行結果ではない。

元 checkout には workflow Skill、phases-and-handoff、handoff-template の未コミット変更（13 行追加 / 5 行削除）と未追跡 `.codex/` の hook 2 ファイルがある。いずれも編集・stage・移動していない。観測中、外部操作で元 checkout が develop へ切り替わった。A0 は base SHA から別 worktree に分離し、workflow 差分を持ち込んでいない。人間 C' 方針は今回のユーザー指示として本 packet に明記する。

- **S-4a で利用可能:** 起動時固定 Variant（Editor profile 優先、既定 payload fallback）、4 companion set、親 Stable 後の companion Add と競合時回収、構造による Cell 分類、World Workspace の Open / optional 職種1件作成と recovery。全 Cell の Whitebox データは未生成。
- **旧資産の実在:** tracked `.unity` を数え、旧 World 1、Cell 16、Environment 4 を確認。World は NecessaryAlways。南辺4 identity は HandAuthored、他12は Generated。四季化はまだない。
- **起動:** `InGameSessionScene.cs` は OnLoaded で Driver を作り OnStabled で Start。`SessionWorldStreamingDriver.cs` は Catalog の4×4列を `CellIdentity.Format` して候補にし、体積を query する。`PlayerScene.cs` は IsStreamingActive 待ち→Teleport→入力 ON→Focus 登録で、Cell Stable を待たない。例外時にも入力 ON の経路がある。新しい順序ではこれらを一緒に見直す必要がある。
- **資源:** `WorldScene.cs` は共有 Lit を Scene scope で PreLoad し、`WorldMaterialBindings` は World/Materials/DemoCellLit.mat を指す。旧 World 廃止は、この所有者・参照の置換も必要。共有材そのものの削除根拠にはならない。
- **生成:** `WorldCellStreamingSliceCreator` は Map、Session、旧 World、SceneGraph Nodes/Total、Addressables を触る。EnvironmentSproutCells は Environment 作成と Ground 除外の二役。全件に拡張するだけでは床が消える。policy の実際の集合名は `HandAuthoredIdentities`。旧 probe と完了ログも旧南辺前提。
- **体積:** `SceneVolumeSceneReader` は既定 payload 優先、全 Renderer（inactive 含む）、Collider 単独は対象外。候補でない子の体積も親へ合併する。Full と Whitebox に独立した runtime Volume はない。

S-4a の残余を取り込まない。Workspace Open の途中の完全復元、既存 Variant build 中断耐性、checkout 初回 scene 読者、既存 streaming loop 全体の UpdateSystem 移行、T-07〜T-09 全体実証などは別件。S-4b に必要な起動順序の変更と区別する。新規 Tick の登録は AGENTS の UpdateSystem 契約に従い、既存ループ方式を新規実装の既定にしない。

## 2. 生成・削除・保持

**生成する確定範囲:** 同じ座標に四季、各54 Cell。論理 Season Resource 4（payload なし）、Season Lighting Scene/Resource 4、Cell Resource 216（各々 Full `""` と `Whitebox` の2 payload）、Environment Scene/Resource 216。したがって対象枝の論理 Resource は440、Scene 実体は652。652はプロジェクト全体件数ではない。Whitebox を別 identity / 別 Resource にしない。同じ Scene 名、Variants/Whitebox の別 path とする。

Season→Cell、Cell→Environment は OnDemand。Cell だけ StreamByDistance=true。Season Lighting は Season 直下 NecessaryAlways、距離候補外。初期216 Cell は全 Generated、S-8a 前に昇格しない。地形・衝突・移動面は Cell、Environment は全件 scaffold（空も許容）。線・見証・背の座標は世界稿で確定済み。SceneNodeData / SceneGraphEdges と、その投影である Resource / Map、payload、Addressables の整合も成果物に含める。

**明示削除範囲:** 旧 World Scene/Resource、旧16 Cell・4 Environment の Scene/Resource と対応 meta、旧 Graph node/edge、Map・親子・Addressables の旧参照。HandAuthored 4件も含む。移送・座標補正・stamp 生存判定・旧セル昇格はしない。`World/` ディレクトリ一括削除を許可する意味ではない。

**生成後の撤去範囲:** program §4.4 の一時生成器、旧 bulk generator、旧 policy / plan / reconciler / collector / probe、generator 専用 tests、WorldGridDefinition.asset と専用型・補助コードのうち置換済みのもの。A1 で正確な path と共有参照を列挙する。参照0だけで削除せず、shared test や runtime 契約テストまで巻き込まない。

**保持する範囲:** World Workspace、職種 Scene 1件作成・transaction/recovery、Variant / companion 実装と対応 tests、WSC / MultiFocus / lifecycle・空間契約の tests、共有 Lit / Primitive、Player・UI・Title・Session 等の無関係な Scene、共通 AssetManagement・SceneGraph 基盤。World 配下に残る Cell runtime 型を旧アセットと一緒に消さない。WorldScene / Bindings の退役と共有材の所有先は A1 の責務判断に残す。

**対象外:** S-4c の RenderEnvironment lease・URP adapter・global lighting ownership・ベイク、S-4d の VFX Graph・Events 実証（追加6 Scene）、S-5 Tunnel と遷移演出、S-6 group 再編、S-7 checkout、S-8 作り込み・昇格、S-9 性能予算。652件を658件へ先回りしない。一時生成器の汎用化はしない。

## 3. 受け入れ条件と確認分担（計画。実施結果ではない）

**全件機械検査:** 4×9×6 の期待 identity 集合と実体集合の一致、前節の Resource/Scene/payload 数、GUID と identity の衝突0、null/dangling reference 0、Map membership と hash、Graph 投影・双方向親子・単一親・循環なし、LoadType / 候補フラグ、全 payload の実在と Addressables 登録を検査する。GUID の共有参照自体は重複違反にしない。各 Full/Whitebox は異なる実体 GUID、同一論理 Scene 名。Whitebox 欠落を runtime fallback で合格にしない。

旧 World/Cell/Environment は実体だけでなく Graph / Map / Addressables / 起動参照からも消えていることを旧 GUID・identity の削除 manifest と照合する。単純 grep はコードの意味確認用 flag とし、無関係な `World` 文字列まで消さない。保持対象の GUID と参照が維持されていることも比較する。

全 Cell の体積が有限・非空で、同座標の四季で共通 AABB 契約を満たすこと、Full/Whitebox の proxy と companion 合併がそれを破らないことを計画する。Renderer の寄与範囲と許容差は A1 で根拠付きに固定し、格子 Bounds の直書きや新しい Volume API で埋めない。生成1回の実時間・対象件数・開始終了・失敗地点を記録し、時間による縮小はしない。

**起動・失敗経路の機械検証:** Spring 枝の直下 StreamByDistance children だけで候補54件を作る。Spring `(0,4)` の Stable より前に入力・通常距離 Tick が始まらず、Stable 後に開始する。Active Season 高々1、枝ごとの候補交換、identity 翻訳なし。失敗・キャンセル・Session 終了・Add 完了競合でも入力を先行解禁せず、残留 Scene / handle を回収する。初期 Ensure が親ロード待ちと循環しないことを検証する。具体的な責務配置・同期手段は A1。

**人間の代表操作案:** Spring `(0,4)` で通常起動と Whitebox+Planner 起動（再起動して切替）を確認し、Stable→入力→通常 Tick の生ログと照合。Planner で Environment を載せず床・衝突・移動面が使えることを見る。Workspace で `(4,2)` と隣接 `(5,2)` の Full/Whitebox、Cell+Environment、Season Lighting を開き、主要シルエット・通路・職種別ファイルを確認。他季節は同じ代表座標、背の代表 `(4,5)` も開く。これは標本確認であり、652件の全目視や S-8 品質バー完了ではない。optional 職種の恒久実証 Scene はこの確認のために生成しない。

AI は全件検査出力と代表確認手順を準備する。C は固定 implementation base/head に対して構造レビューと Unity tests / 必要な Addressables build を担当。B は Unity tests / build を実行せず contract-audit まで。A0 では文書検査だけを実施する。

**C' は当面人間。Claude は再開しない。** cursor-agent を使う場合は Grok 系を明示する。C より前に固定した Phase A/B snapshots、完全 diff、生テスト結果、機械検査出力から C' blind bundle を作り、C の結論・疑念候補を隔離する。人間が所見・確認/未確認範囲・残存リスク・判定を明示するまで PASS/完了にしない。C 結論を先に読んだ、設計・実装に関与した場合は独立性制約を記録し、盲検監査済みとはしない。C' の監査判定と D の突合・マージ判断は別々に記録する。

## 4. 破壊前提と戻せる地点

- **P0（ワイプ前）:** 対象 checkout / Editor project path と base を固定。既存の人間作業を退避済みと確認し、dirty / untitled Scene と pending Workspace journal を未解決のまま進めない。旧資産と保持資産の path/GUID、全変更予定の Graph/Map/Addressables を dry-run manifest に出し、削除 allowlist の外は無変更とする。共有材の所有先、生成仕様、復旧手順が A3 で凍結されるまで破壊を始めない。
- **P1（生成器＋dry-run commit）:** program §4.4 の最初の保存地点。未変更の旧 World 一式へ戻せる。大量ワイプを Workspace の単品作成 transaction と同じ原子性だと扱わない。
- **P2（生成物 commit）:** 一回生成・集約検査の生出力と実時間を保存。途中失敗なら自動再実行で上書きせず停止し、manifest 内の新規物と既存変更を識別して P1 の資産・meta・Graph・Map・Addressables を一体で復元する。残骸の無差別削除は禁止。Editor メモリ、未保存状態、Library は git revert だけでは戻らないため、再読込と整合再検査を復旧条件に含める。
- **P3（起動統合＋撤去後）:** 受け入れ対象は最終 implementation head。撤去前の generator/validator commit は履歴に保持し、最終 head に対する検査方法と生出力も再現可能にする。生成途中の結果を最終 head の PASS に流用しない。障害時は起動配線だけ・生成物だけを片戻しせず、S-4b 全体の整合した地点へ戻す。

以上は復旧計画であり、今回はワイプや復旧操作を実施していない。実作業は人間が開いた Editor への接続のみ、Scene/asset YAML 手編集なし。非squash merge は program で決定済み。

## 5. 分割判断

**A0 判断: S-4b 一つのスライス内の段階とする。** 生成器のみでは成果物の受け入れが成立しない。一回限りで撤去する装置を独立した恒久ツールとして出荷する必要もない。旧 World を消した生成物と現行起動は互換でなく、Spring 配線だけを先に入れても参照先がない。個別に受け入れ・取り消しするための互換モードは新たな複雑さになる。

program §4.4 の「生成器＋dry-run/validator」「生成物」「生成器等の撤去」の commit 分離を維持し、Spring 起動配線もレビュー可能な別 commit として同一枝内でまとめる。途中 commit を独立マージ可能とは宣言しない。最終的な受け入れ・取り消しは生成物＋起動＋撤去の一組。4スライス構成を変更する再承認は求めない。

A1 では生成規則と Unity I/O、起動 orchestration と純政策、共有資源所有を見積もり、500行・3責務・50%増の警報に理由を付ける。これらはファイル数によるスライス分割命令ではない。独立受け入れが可能な別要求が判明した場合だけ分割を再検討する。責務マップ・クラス/API・実装手順は §7。A3 凍結までは B に渡さない。

## 6. A0 論点への回答と A1 への引き渡し

1. **Full と Whitebox の形:** 床・通路・見証の形をどこまで共通にし、何を Whitebox で省くか、という設計課題。発注者へ抽象的な選択を求めず、A1 担当が具体案を先に出す。線・見証・背の座標は再承認不要。密度・寸法・seed は未確定のまま、完成美術や S-8 演奏レイヤを混ぜない。
2. **S-4c 前の Lighting:** 制作用 Scene の入れ物だけを作るか、何か light を置くか、という課題。こちらも A1 担当の案を先に示す。Season Lighting の存在・親子・LoadType は確定済み。global owner と季節 preset は S-4c。
3. **失敗時の方針（2026-09-09 発注者回答）:** 季節 WorldLevel / Season のロード失敗は当面エラーのまま。将来、前の Scene に戻るボタンだけを持つ `UnderconstructionScene` を検討するが、今回は作らない。Cell のロード失敗も通常の例外として扱い、専用画面・代替 Cell・ゲーム側の追加リトライや復旧フローを実装しない。初期 Cell Stable 前の入力解禁は禁止を維持し、失敗を成功に読み替える入力 ON は除く。現行15秒を新しい timeout 仕様として採用しない。

この3点は A0 の回答待ちゲートではない。発注者は計画の続行を了承した。1・2について案の承認が済んだとは記録しない。

人間への製品判断と区別して A1 が具体化すべきだった項目は §7。F1 / L1 / Materials / 並行拒否は人間回答で閉じた。N-10 を理由に FW の可変 Candidates API を先回り追加しない。

当初の停止点は A0。2026-09-10 に発注者から凍結・B 開始判断を委任された。コード照合で §7.17 の不足が判明したため Phase A を再開する。実装・生成・Unity 操作は未着手。追加の製品承認を待っている状態ではない。

## 7. A1 詳細設計と A3 反映


A0 §7 の方針は維持する。F1 / L1 / Materials 契約 / 並行拒否は 2026-09-10 の人間回答で生成仕様になった。program を B の実装指示にしない。S-4a 残余・S-4c/d・S-5 以降を混ぜず、一時生成器を汎用化しない。

### 7.1 コード照合で分かった現況差

照合対象は base `77ace60` の SampleGame / OSM Runtime・Editor。Unity Editor には接続していない。

| 箇所 | 現況 | S-4b で変える理由 |
|---|---|---|
| `InGameSession`（ファイル名 `InGameSessionScene.cs`、151行） | OnLoaded で Driver 生成、OnStabled で即 `Start()`。World は NecessaryAlways 前提 | Season は OnDemand。Tick 開始を Ensure 完了後へずらす |
| `PlayerScene`（238行） | `IsStreamingActive` を最大15秒待ち、`Cell_0_0` へ Teleport、成功も失敗も入力 ON | 15秒を新 timeout にしない。源流は `(0,4)`。失敗時の入力 ON を除く |
| `SessionWorldStreamingDriver`（248行） | 構築時に `WorldCellCatalog.EnumerateCells` + `CellIdentity.Format`（無修飾 `Cell_x_y`）で16候補 | 候補は active Season 直下の `StreamByDistance` 子だけ。HUD も格子 Format しない |
| `WorldCellCatalog`（216行） | 矩形 4×4、`SpawnPosition` は `(0,0)`、`WorldIdentity = "World"` | 矩形 9×6、スポーン `(0,4)`。identity 正本にはしない |
| `WorldScene` + `WorldMaterialBindings` | payload 付き World が Scene scope で Lit を PreLoad し、GO 参照を照合 | Season は payload なし。GO 検索はできない |
| `GameSceneFactory` | `"World"` 固定。Cell は `StreamByDistance`、職種は親が Cell | `Season_*` 4件と `*_Lighting`（座標なし）4件を結線 |
| `CellCompanionRoleClassifier` | 末尾から3トークン目で role。`Spring_Environment_4_2` は既に Environment | 変更しない。`Spring_Lighting`（2トークン）は職種 Cell に誤分類されない |
| `WorldCellStreamingSliceCreator`（約1213行） | Environment 萌芽4件だけに床を Environment 側へ移し、Cell 側は Marker only | 全 Cell に床を置く。萌芽条件を全件へ拡張すると Planner で床が消える |
| `SceneDirector.PerformUnitySceneLoad` | Payloads.Count==0 なら Unity ロードせず SceneBase 寿命だけ進める | 論理 Season の PreLoad はこの経路で届く |
| `SceneVolumeSceneReader` | 既定 payload 優先、全 Renderer（inactive 含む）。Collider 単独は非寄与 | N-9 は生成物の配置規約で抑える。Volume API は増やさない |
| `SceneVolumeMath.Merge` | 候補でない子（Environment）を親体積へ畳む | Whitebox 固有 Renderer が Full+Environment の焼付体積をはみ出さないこと |
| `WorldStreamingController.ObserveAddCompletionAsync` | 例外を観測して in-flight を外し、次 Tick の IsLoaded 再照合で再発行 | FW 契約は維持。ゲーム側の専用復旧画面・代替 Cell は足さない |
| World Workspace | 既に `{Season}_Cell_{x}_{y}` と 9×6 範囲、Whitebox 欠落を fallback しない | 生成物の消費者。Workspace 自体は S-4a 完了物として保持 |

`CellIdentity` は無修飾 `Cell_{x}_{y}` 専用のまま残し、修飾付き identity の組み立ては SampleGame 側の別ヘルパへ置く。FW に季節語を出さない。

### 7.2 Full / Whitebox（生成仕様）

共通前提（世界稿 §3。再承認不要）: 1セル 250m、谷 2250×1500、線9セル、見証 `(4,2)`、背は `y=5` の9セル。床・衝突・移動面は Cell。Environment は set dressing。Whitebox は別 identity にしない。

現行 4×4 は「南辺だけ床が Environment」なので、比較の基準には使わない。

**案 F1（生成仕様。2026-09-10 人間）— 大形共通、Whitebox は装飾省略**

同じ Cell の Full と Whitebox が共有する物（すべて Cell 側、Collider 付き床のみ歩行に使う）:

```
上から見た1セル（250m）。原点はセル南西。
+---------------------------+  y+1
|  背セルだけ: 北壁 245x24x20 |
|                           |
|     [見証セルだけ: 12x48x12] |
|           塔              |
|      ======== 線セルだけ   |  幅40m・高さ2m の通路帯
|                           |
| ########### 床 245x1x245  |
+---------------------------+  y
```

| 要素 | 寸法 | Full | Whitebox | Collider | 体積寄与 |
|---|---|---|---|---|---|
| Ground | 245×1×245、中心 y=0.5（下面 y=0、上面 y=1） | あり | あり | BoxCollider | する |
| 線の通路帯 | 幅40・高さ2。セル内の線分方向 | 線9セル | 同じ | なし（床で歩く） | する |
| 見証塔 | 12×48×12、セル中心 | `(4,2)` のみ | 同じ | なし | する |
| 背の北壁 | 245×24×20、セル北端 | `y=5` | 同じ | なし | する |
| Prop_* | 現行同様 2+motif、Collider 削除 | あり | **無し** | なし | Full のみ |
| Environment EnvProp | 1〜3、床なし | Full companion | 空ルート可 | なし | 親へ合併（候補ではないため） |

季節差はメッシュ位相と寸法を変えず、共有 Lit の MPB 色だけにする（既存 `GetCellTint` + 季節 Hue オフセット Spring 0 / Summer +0.08 / Autumn +0.16 / Winter +0.24）。見証・背の高さ係数は採らない。同じ (x,y) の四季が承認済みの共有 AABB を破るため。S-8 の演奏レイヤではない。

計算例: Ground は y=0..1。見証塔 48m は格子高さ 96m に収まる。四季でメッシュを変えないのでコーナー差の主因は浮動小数のみ。

F2 は推奨しない。F3 は採用しない。寸法は生成器へ渡してよい。

### 7.3 Season Lighting scaffold

存在・親子・LoadType は確定（Season 直下 NecessaryAlways、距離候補外）。global owner と季節 preset は S-4c。

**L1 を生成仕様とする（2026-09-10 人間）。** 空 Scene。Authored ルート GO のみ。Renderer / Light / Volume / LightingSettings なし。S-4b の受け入れを「四季の完成した光」にしない。`PlayerScene.ApplyDemoLook` の RenderSettings 直書きは S-4c まで残す。L2 / L3 は採らない。

### 7.4 初期 Ensure、入力解禁、失敗

所有者: Session が初期化の進行を所有する。Player は位置と入力だけを所有する。

循環を避けるため、**どの lifecycle フックでも `AddScene` を await しない。** 現行コメントどおり、OnLoaded 中の子 Add は親ロード待ちとデッドロックし得る。OnStabledImpl も即 return し、Ensure は Session CTS 付きの fire-and-forget にする。

```
Session.OnLoaded     : SeasonBranchController を生成。Tick は Start しない
Session.OnStabled    : EnsureWorldAsync().Forget() して return
Player.OnStabled     : Bootstrap が WaitUntilWorldReady を待つ（IsStreamingActive ではない。15秒上限なし）
EnsureWorldAsync     : 枝操作キューへ「Spring + 源流 Cell_0_4」を1件だけ投入
枝操作（直列・単一所有者）:
  操作中の別要求は拒否（待たせない）。Session 終了は操作を cancel し、投入中なら完了または失敗まで所有する
  1. AddScene("Season_Spring") を await。戻り値は UniTask のみ。直後に ISceneQuery.GetLoadedScene で SceneBase を捕捉
  2. IsSceneStable になるまで待つ。Add 完了 ≠ Stable。捕捉した個体と Query の個体が同一であることを確認
  3. 直下 StreamByDistance 子から候補54を SeasonCandidateSelection で作る
  4. 距離 driver をその候補で新規構築（まだ Start しない）。companion driver もこの枝で新規構築
  5. AddScene("Spring_Cell_0_4") → GetLoadedScene で捕捉 → Stable を再確認
  6. CompanionDriver.Start
  7. WorldReady を成功完了
  8. 距離 Driver.Start（既存どおり Focus 待ちループ。新規 UpdateSystem Tick は増やさない）
Player Bootstrap     :
  WaitUntilWorldReady 成功後、InputEnabled はまだ false
  PlayerWorldReadySequence が SpawnPosition=(0,4) 中心 + 28m へ Teleport → RegisterFlight → InputEnabled = true
失敗 / キャンセル / Session 終了:
  WorldReady を例外または cancel で完了。入力は OFF のまま
  例外 catch での入力 ON を削除
  捕捉した Season / Cell の寿命終端まで待ってから枝を手放す。成功扱いにしない
```

通知署名（SampleGame 内のみ）。`UniTask` プロパティは一度しか await できないため、公開はメソッドにする。

```
IInGameSessionServices
  UniTask WaitUntilWorldReady(CancellationToken ct)
      // 内部は UniTaskCompletionSource。成功・例外・teardown cancel。
      // 完了後の呼び出しは同じ結果を即返す。
  bool IsWorldReady { get; }
  bool IsStreamingActive { get; }   // 距離 Tick が Start 済み。初期化待ちには使わない
```

HUD の `isBusy` は `!IsWorldReady` を使う。`IsStreamingActive` を初期化待ちに再利用しない。Player Bootstrap は `WaitUntilWorldReady` を待つ。

Season / 源流のロード失敗は通常例外のまま。`UnderconstructionScene` と戻るボタンは作らない。Cell 失敗もゲーム側の専用画面・代替 Cell・追加リトライを足さない。通常距離 Tick 開始後の Cell 失敗は、現行 WSC の観測と次 Tick 再照合に任せる。

初期 Ensure が親待ちと循環しない検証: 枝操作の Scene/Player 接続は fake に差し替える。Ensure が OnLoaded/OnStabled の完了をブロックしないこと、Player が WaitUntilWorldReady だけを待つことをシグナルで確認する。`Task.Delay` / `Thread.Sleep` は使わない。PoNR 後は CTS だけでは Add を止められない前提で、失敗時も入力を上げない。

### 7.5 候補交換と HUD identity

`WorldStreamingController` に Candidates setter は無い（現状仕様）。差し替えは **旧枝の両 driver を破棄し、新 `StreamingCandidateSet` で作り直す**。

排他入口（S-4b。Tunnel UX は S-5）。`SessionSeasonController`（枝操作単位）が直列化・active 枝・両 driver・終了待ちを所有する。初回 Spring／源流指定と Player の入力解禁は外側（Session / PlayerWorldReadySequence）。

1. 操作中なら別要求は拒否（待たせない。2026-09-10 人間）。同一 Season への要求だけ no-op
2. 旧 Season の SceneBase を GetLoadedScene で捕捉できていなければ、進行中 Add の完了または cancel 処理の終わりを待ってから続ける
3. 両 driver を Dispose（Stop だけの companion 再 Start は不可。CTS が残る）
4. `UnloadScene(oldSeason)` を await する。この戻りを寿命終了とみなさない。ロード中は cancel / pending して即 return する経路がある。捕捉した SceneBase の AfterUnLoaded 相当まで待つ。IsSceneLoaded false（PreUnloading 以降）は証拠にしない
5. 新 Season を Add → GetLoadedScene で捕捉 → Stable。候補を作り、両 driver を新規構築
6. 同時に Stable な `Season_*` は高々1。破ったら失敗

FW に Candidates setter・新しい SceneState・専用復旧画面は足さない。Unload 完了を観測する公開口が既存 Query / 捕捉した SceneBase 寿命で足りない場合は B で決めず A に戻す。

identity を季節間で翻訳しない。同じ AABB に四季が重なるため、`WorldCellCatalog.TryGetCellIdentity`（無修飾 Format）は HUD / CurrentCell に使えない。

`CurrentCellIdentity` は **active 候補のうち、Focus の XZ が体積に入るもの**。複数なら identity の ordinal 最小。グリッド外・未登録は null。体積に入らなくても距離 Tick は中心距離で載せる（現行どおり）。

### 7.6 共有 Lit の所有者移行

| 項目 | 現況 | 移行案 |
|---|---|---|
| アセット | `.../World/Materials/DemoCellLit.mat` GUID `c03517b35199eb046852798ce73fa2d7` | GUID 維持。`git mv` で `.../InGameSession/Seasons/Materials/DemoCellLit.mat` |
| PreLoad | `WorldScene.OnPreLoaded` + Addressables | `SeasonScene.OnPreLoaded` が `LoadSceneScopedAssetAsync<Material>`。owner は `AssetOwner.Scene(Season identity)` |
| GO 配線 | `WorldMaterialBindings` を Find | **しない**。論理ノードは RootObjects 空 |
| 解放 | World Unload で Scene owner 解放 | Season Unload に委ねる。明示 Dispose しない（現行と同じ） |
| 退役 | World.unity / WorldScene / Bindings | 参照が新 path に置換されたことを確認してから削除 |

Cell / Environment の authored Renderer は同じ Material を `sharedMaterial` で指す。App 寿命へ上げない（Season 切替で再 PreLoad する）。S-6 の group 再編はしない。

`Seasons/Materials/` の契約（2026-09-10 人間）:

- 四季で共通に使う **Material だけ**を置く。Cell 固有・季節固有の Material / Texture / Shader は置かない
- そこに置く Material が参照してよいのは、**共通 Texture** と **共通 Shader** だけ。それ以外（季節専用テクスチャ、Mesh、Lighting アセット、Prefab など）を参照するならこのフォルダには置けない
- 現行 `DemoCellLit.mat` は URP Lit シェーダ（パッケージ）のみを参照し、Texture スロットはすべて空。この契約を満たすので移動してよい
- 生成器が新しいマップや固有テクスチャを足したくなったら、その Material は Cell / Environment 側へ置く。`Seasons/Materials` を増やして逃げない

### 7.7 責務マップ

行数は base の総行（空行込み）。増減は初稿見積もり。新規名は提案。asmdef 追加が必要なら A3 前に返す。

**ランタイム（SampleGame.InGame）**

| ファイル | 責務（一文） | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 配置理由 | 規模 |
|---|---|---|---|---|---|---|---|---|
| `InGameSessionScene.cs` / 型 `InGameSession` | Session 寿命で controller と driver を生成・破棄し、子へサービスを出す。OnLoaded だけが SceneDirector 具象を adapter / backend 工場へ渡す。OnPreUnload は Dispose のみで drain を await しない | Tick 即 Start をやめる。終了循環を避ける | Session Scene | 既存 SceneDirector（composition のみ） | `IInGameSessionServices` | 生成と破棄の順序は controller テストに委譲。PreUnload が発行済み操作を待たないこと | 既存ハブ | 151、+40〜70。Ensure 本体は持たない |
| `IInGameSessionServices.cs` | Player/HUD 向けの準備完了と、Session 終了前の受付停止口 | `WaitUntilWorldReady` / `IsWorldReady` / `StopWorldOperations` を足す | 契約のみ | なし（位置所有を渡さない） | SampleGame 内 | 署名のテストは Player bootstrap と Result 側 | 既存 | 61、+20〜35 |
| `Streaming/SessionSeasonController.cs` **新規** | 季節枝操作の直列化、active 枝、両 driver の生成破棄、発行済み操作の所有、失敗の伝播。Session 終了時は受付停止だけで drain しない | 独立して変わる起動政策 | Session CTS。枝操作中は1件 | `ISceneController` / `ISceneQuery` / `ISceneTerminalEvents`、driver 工場、immutable 候補、Issued 登録 | 内部 | 直列化・拒否・遅延 Unload・Add≠Stable・発行済み収束を fake 接続で。具象 SceneDirector は工場の外 | InGame Streaming | 280〜380 |
| `Streaming/ISceneTerminalEvents.cs` **新規** | Removed / CancelCleanedUp 相当の終端通知。identity のみ | Query 完了と混ぜない観測口 | 契約のみ | なし | 内部 | アダプタの透過 | Streaming | 20〜40 |
| `Streaming/SceneDirectorTerminalEvents.cs` **新規** | `SceneDirector.OnSceneEvent` を `ISceneTerminalEvents` へ適応 | 具象を composition に閉じる | Session 生成時 | SceneDirector | 内部 | イベント種別の透過 | Streaming。OnLoaded が生成 | 30〜50 |
| `Streaming/SceneTerminalTracker.cs` **新規** | 終端イベントと世代で個体の消滅を待つ。購読は Add/Unload より前。Query が null になったことだけでは完了にしない | 既存公開 API を増やさず寿命観測する | Session。購読は controller 生成時から破棄まで | `ISceneTerminalEvents` | 内部 | 同期完了、世代ずれ、購読前に出たイベントを後から完了扱いしないこと、同一 identity の後発個体を待たないこと | Streaming | 80〜140 |
| `Streaming/IssuedSceneOperationRegistry.cs` **新規** | この枝が発行した Add/Unload を登録し、終了まで所有する。Dispose / Stop / Clear で完了扱いしない | driver.Dispose と操作終了を混ぜない | 枝操作。Session 終了では新規登録だけ拒否 | Tracker | 内部 | Clear しないこと。未完了のまま次 Season を Add しないこと | Streaming | 80〜130 |
| `Streaming/SeasonCandidateSelection.cs` **新規** | Season 直下の StreamByDistance 子から候補集合を作る純関数 | 読出と選別を混ぜない | 値 | identity/flag/volume の列 | 内部 | 空・重複・欠落 volume を例外 | 同上 | 80〜140 |
| `Streaming/SeasonCellNames.cs` **新規** | `{Season}_Cell_{x}_{y}` 等の SampleGame 名称 | 無修飾 `CellIdentity` と混ぜない | 値 | なし | **SampleGame.InGame の public**（Editor はこれを使う。InternalsVisibleTo は足さない）。生成 Plan も同じ規則 | Format/parse の表 | Streaming | 40〜80 |
| `Streaming/PlayerWorldReadySequence.cs` **新規** | Teleport / RegisterFlight / 入力 ON の順だけ | PlayerScene の private bootstrap から切り離す | 呼び出し側（Player） | `IInGameSessionServices` と flyer 抽象 | 内部（Tests から見える範囲は既存 InternalsVisibleTo） | Unity GO なしで順序・失敗時入力 OFF | Player または Streaming | 40〜80 |
| `SessionWorldStreamingDriver.cs` | 渡された候補で WSC を Tick | 構築時 Catalog.Format をやめる。生成は `ISceneStreamingBackend` + `ISceneVolumeQuery` を受け、具象 SceneDirector を必須にしない | Session / 枝 | 既存 WSC | `CurrentCellIdentity` は候補体積 | 工場経由で fake backend | 既存 | 248、−20〜+40。距離政策は再実装しない |
| `WorldCellCatalog.cs` | 9×6 座標、スポーン、格子補助 | 4×4 と `(0,0)` スポーンをやめる | 静的 | なし | 制作座標。runtime identity 正本ではない | 既存 Catalog テストを 9×6 に更新 | 既存 | 216、−40〜+10。`CreateGridConfig` は参照0でも残す |
| `PlayerScene.cs` | 準備完了後に PlayerWorldReadySequence を呼ぶ | 15秒待ちと失敗時入力 ON を除く。順序の中核は Sequence 側 | Player Scene | 親サービス | なし | Scene 配線のみ。順序テストは Sequence | 既存。カメラ責務は増やさない | 238、−30〜+20 |
| `InGameUI.cs` | HUD の current/resident | Format 依存をやめ `IsWorldReady` に合わせる | UI Scene | 親サービス | なし | 表示文字列の単体は最小 | 既存 | 約151、+5〜15 |
| `World/SeasonScene.cs` **新規** | 共有 Lit の Scene scope PreLoad | WorldScene 置換 | `AssetOwner.Scene(Season_*)` | `IAssetManagement` | なし | PreLoad 呼び出しは fake assets | World フォルダ（runtime 型の置き場を維持） | 50〜90 |
| `World/SeasonLightingScene.cs` **新規** | 空 scaffold の SceneBase。S-4c の lease は書かない | Factory が null を返さないため | Season Lighting Scene | なし | なし | 生成と Stable ログ程度 | 同上。S-4c で責務が増えたらそのスライスで分割 | 30〜60 |
| `WorldScene.cs` / `WorldMaterialBindings.cs` | 退役 | 旧 World payload 廃止 | — | — | — | 参照置換後に削除 | — | 91 / 23 → 0 |
| `DemoCellScene.cs` | AuthoredRoot 検証 | ルート名契約は維持 | Cell Scene | なし | なし | 既存 | 既存。identity parse しない | 変更なし想定 |
| `GameSceneFactory.cs` | Season 4 + SeasonLighting 4 を結線。`"World"` 分岐削除 | 構造分類は既存の StreamByDistance を維持 | DependOnAll | InGame 型 | `ISceneFactory` | 結線の表テスト | 既存。asmdef 追加なし | 87、+20〜40 |
| `Result/ResultScene.cs` | SwitchScene の前に `StopWorldOperations` を呼ぶ。drain は await しない | 遅延 companion Add が Session 終了中に祖先を再ロードしないようにする | Result Scene | `IInGameSessionServices` | なし | Stop 後に新規発行 0。lifecycle 内で枝 drain を待たない | 既存 | 49、+10〜20 |

**FW（通常例外後回収。先行別 commit。公開 API / SceneState 値 / 季節語なし）**

| ファイル | 責務（一文） | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト境界 | 配置理由 | 規模 |
|---|---|---|---|---|---|---|---|---|
| `SceneDirector.FailedLoadCleanup.cs` **新規 partial** | この Add が作った newlyCreated 個体だけを、到達状態に応じて回収し、元の通常例外を保持したまま終端イベントを出す | 失敗回収は成功経路のオーケストレーションと独立して変わる | SceneDirector | Loading / Unloading の内部ヘルパ、Lifecycle | なし。新しい SceneEventType も作らない | 例外後の辞書除去、pending 0、再 Add、既存 Stable 親の保持、兄弟の片失敗 | Loading.cs が既に 500 超。変更理由が違うので分ける | 120〜200 |
| `SceneDirector.Loading.cs` | 非 OCE を catch して FailedLoadCleanup へ委譲する。NecessaryAlways 兄弟は一人の失敗で他を破棄せず完了または失敗まで待つ。Unity ロード中 identity を内部登録する。IncrementalAlways の非 OCE はその子だけ回収へ渡す | 呼び出し点と兄弟待機 | 同上 | FailedLoadCleanup、既存 `_inFlightSceneBaseLoads` | なし | 既存 Add 成功経路が壊れない。兄弟片失敗。Incremental 子失敗で親保持 | 既存。本体のオーケストレーションは残す | 約643、+20〜50。失敗本体は新規 partial |
| `SceneDirector.Unloading.cs` | 失敗回収の途中で UnloadScene が来たら pending に落とさず in-flight 回収へ合流する。Initializing からの 3-phase を許可する | 失敗中の Unload が永久 pending にならない | 同上 | Lifecycle | なし | pending 残留 0。Initializing 失敗後も辞書除去 | 既存の 3-phase を Stable 以外へ無理に流用しない（Initializing は辺追加で 3-phase） | 約362、+15〜40 |
| `SceneLifecycleManager.cs` | 既存14値のまま `Initializing → PreUnloading` を許可する | IsActive なのに 3-phase へ入れない穴を閉じる | 内部 | なし | なし。値・順序は変えない | 遷移表。Stable からの LoadCanceled は引き続き不可 | 既存。新しい SceneState は足さない | 約111、+3〜8 |
| `SceneDirectorCancellationTests.cs` | 非 OCE 後の pending 残留期待を「辞書除去・pending 0・Unload は no-op」へ更新 | 人間承認済みの期待変更 | tests | なし | なし | 旧期待を残さない | 既存 | 約250、±30 |
| `SceneDirectorFailedLoadCleanupTests.cs` **新規** | 失敗回収の回帰 | 新しい変更理由のテストを Cancellation に混ぜない | tests | 既存 SceneDirector テスト基盤 | なし | §7.20.2 の表 | Tests/Scene | 250〜400 |
| `SceneLifecycleManagerTests.cs` | Initializing→PreUnloading を合法とする | 辺追加の回帰 | tests | なし | なし | 既存の不正遷移は維持 | 既存 | +15〜25 |

**一時 Editor（SampleGame.DependOnAll.Editor、生成後削除）**

変更理由が違うので分ける。旧 `WorldCellStreamingSliceCreator` へ足さない。

| ファイル | 責務 | 寿命 | テスト | 規模と警報 |
|---|---|---|---|---|
| `WorldAuthoring/SeasonWorldGenerationPlan.cs` | 期待 identity / path / 形状パラメータ / dry-run manifest | 1 command | Unity なしの値テスト | 180〜280 |
| `WorldAuthoring/SeasonWorldWipe.cs` | allowlist 内の旧資産削除 I/O | 1 command、P0/P1 | dry-run 集合のテスト | 120〜200。Wipe と Generate は復旧地点が違うので分ける |
| `WorldAuthoring/SeasonWorldGenerationCommand.cs` | Scene/Graph/Map/Addressables の作成 I/O | 1 command、P2 | 実 Editor は validator に委譲 | 300〜500。500超なら「一回の作成順」として非分割を再評価。汎用 Tool にしない |
| `WorldAuthoring/SeasonWorldValidation.cs` | 生成契約の全件照合 | 1 command および P3 後の再生手順 | 純関数部分は Editor なし | 200〜350 |

旧 generator（`Editor/Streaming/Cells/**`）は置換後に削除する。既存 `WorldCellGenerator` を 9×6 四季へ汎用化しない。

### 7.8 生成・撤去・保持 manifest

件数は §2 のまま（論理 Resource 440、Scene 実体 652）。物理フォルダは program §2.5 と軸 B に合わせる。

**生成（path 規則）**

```
Assets/SampleGame/InGame/InGameSession/Seasons/
  Materials/DemoCellLit.mat          （GUID 維持の git mv。共有 Material のみ。参照は共有 Texture/Shader に限る）
  Spring/
    Spring_Lighting/Spring_Lighting.unity
    Cells/Spring_Cell_{x}_{y}/
      Spring_Cell_{x}_{y}.unity
      Variants/Whitebox/Spring_Cell_{x}_{y}.unity
      Spring_Environment_{x}_{y}/Spring_Environment_{x}_{y}.unity
  Summer/ Autumn/ Winter/ 同型
```

対応 SceneResource / SceneNodeData / Total.asset の edge、Addressables、InGameSession の子リンク。論理 `Season_*` は payload 空。Cell は `""` と `Whitebox` の2 payload。LoadType: Season OnDemand、Season Lighting NecessaryAlways、Cell OnDemand+StreamByDistance、Environment OnDemand+非候補。policy は全 Generated。

**明示削除（旧 World 一式。HandAuthored 4件を含む。移送しない）**

- `Assets/SampleGame/InGame/InGameSession/World/World.unity` と meta
- `World/Cells/Cell_{0-3}_{0-3}/` 配下の `.unity` / `.asset` / Environment / meta
- `World/WorldGridDefinition.asset`
- `Assets/OneStarMakerCommon/SceneMap/World.asset`
- `Assets/SceneGraphData/Nodes/World.asset` および `Nodes/Cells/` の旧 Cell_/Environment_ ノード
- Total.asset / InGameSession 子 / Map / Addressables の旧 GUID・identity
- `GameSceneFactory` の `"World"` 分岐、起動の World NecessaryAlways リンク

`World/` ディレクトリ一括削除はしない。残す runtime `.cs`（`CellScene`、`DemoCellScene`、`CellIdentity`、`CellGridConfig`、`CellCompanionScene`）と、移動後の Materials を巻き込む禁止。

**生成後に撤去するコード・専用 test（参照0だけを理由にしない。置換済みを確認）**

- `Editor/Streaming/Cells/WorldCellStreamingSliceCreator.cs`
- `Generation/WorldCellGenerator.cs` / `WorldCellGenerationTarget.cs` / `WorldGridDefinition.cs`
- `Planning/CellPopulationPlan.cs` / `CellAuthoringPolicy.cs`
- `State/WorldCellExistingStateCollector.cs` / `WorldCellFolderReconciler.cs`
- `HandEditProbe.cs`
- `LegacyWorldAuthoringNames.cs`（Workspace からの参照が 0 であることを確認してから）
- 一時 `SeasonWorld*` 生成器・validator
- Tests: `WorldCellGeneratorTests` / `WorldCellGenerationIdentityTests` / `WorldCellExistingStateCollectorTests` / `WorldCellFolderReconcilerTests` / `CellPopulationPlanTests` / `WorldGridDefinitionLoadTests`

**保持**

World Workspace 一式、職種1件作成・transaction/recovery、Variant/companion 実装と tests、WSC / MultiFocus / lifecycle / 空間契約 tests、共有 Lit GUID、Player/UI/Title/Session など無関係 Scene、AssetManagement / SceneGraph 基盤、`WorldCellCatalogTests` の格子契約（9×6 に更新）、`SessionCellCompanionLoadDriverTests`。

**対象外のまま**: S-4c lease/URP、S-4d 追加6 Scene、S-5 Tunnel、S-6 group、S-7 checkout、S-8 昇格、S-9 予算、652→658 の先回り、一時生成器の汎用化、S-4a の Workspace 途中復元・Variant 中断耐性・checkout 初回読者・既存 loop の UpdateSystem 移行。

### 7.9 復旧地点と手順（実施は B 以降。A1 は計画）

P0〜P3 は §4 を継承する。A1 で足す運用:

| 地点 | git | 戻し方 | git だけでは戻らないもの |
|---|---|---|---|
| P0 ワイプ前 | dirty / untitled / Workspace journal を解消。dry-run manifest | 作業しない | Editor メモリ |
| P1 生成器+dry-run | 最初の保存地点 | この commit へ revert | なし（資産未破壊） |
| P2 生成物 | 一回生成の生ログ・実時間・件数 | 下記「P2 失敗時の P1 復帰」。652 件の期待集合は使わない | Library、未保存 Scene |
| P3 起動+撤去後 | 最終 implementation head | 起動だけ・生成物だけを片戻ししない | 同上 |

大量ワイプを Workspace 単品 transaction と同じ原子性だと思わない。非 squash。途中 commit は独立マージ可能と宣言しない。

**P2 失敗時の P1 復帰（完了判定）**

652 件の期待集合は P1 には適用しない。照合先は P0/P1 で保存した path・GUID・参照集合。

1. 再実行で上書きしない。停止する
2. 失敗時に開いている `.unity` は保存せず閉じる。dirty / untitled は破棄。Workspace journal が残っていれば先に解消または中止
3. dry-run / 実行 manifest で識別した**新規物**を除去する。allowlist 外は触らない
4. P1 の資産・meta・Graph・Map・Addressables を一体で checkout / restore する
5. `AssetDatabase.Refresh`（または Editor 再読込）のあと、P0/P1 の GUID・identity・親子・Addressables 集合と対称差 0 を確認する
6. 旧 World 16 Cell + 4 Environment が戻り、Season_* が無いこと。Library の食い違いは git では戻らないので、再読込後の照合が失敗なら復旧未完了とする

### 7.10 全件機械検査と代表操作

**機械検査（全 652 Scene / 440 Resource。標本を全件目視にしない）**

1. 期待集合: 4×9×6 Cell identity、Whitebox path、Environment、Season 4、Season Lighting 4
2. 実体集合との対称差 0。GUID 衝突 0、identity 衝突 0
3. null / dangling 0。Map membership と hash
4. Graph 双方向親子、単一親、循環なし。Session→Season→Lighting/Cell→Environment
5. LoadType / StreamByDistance。Whitebox 欠落を runtime fallback で合格にしない
6. Addressables 登録。GUID 共有参照自体は重複違反にしない。Full と Whitebox は別実体 GUID、同一論理名
7. 旧 World/Cell/Environment の GUID・identity が Graph/Map/Addressables/起動参照から消えている
8. 保持 GUID が残っている
9. 体積（再計算メニューを先に回して保存値を上書きしない）:
   - 各 Cell の Full payload を GUID から開き、自己体積（Renderer 合併）を求める
   - 非候補 companion（Environment）の自己体積を同様に求め、既存 `SceneVolumeMath.Merge` で親へ畳んだ結果を「期待保存値」とする
   - 保存済み `SceneResource.Volume` と期待保存値を照合し、不一致は修復前に記録する
   - Whitebox 216 件は別 GUID の payload を開き、自己体積が Full 自己体積からはみ出していないこと。既定 payload 優先の Recalculate All だけでは Whitebox を見ない
   - 全 Cell が有限非空。寄与 Renderer は自分の格子セル（origin=`(x*250, 0, y*250)` size=`(250, 96, 250)`）の近傍に収まること。隣セルへ跳ねたら失敗
   - **四季の大きさ:** 同じ (x,y) なら形は同じ（高さ係数なし）なので体積もほぼ同じになる。差が出たら生成バグとして拾う。2m / 1m / 5m は製品の品質バーではない。焼き誤差で落ちないための検査用の緩い閾値で、厳密に製品判断しない
   - Collider のみは寄与しない
10. 生成1回の開始・終了・失敗地点・対象件数・実時間。時間で縮小しない

validator 撤去後の再現: P3 head に対し、純関数の期待集合（Plan 相当を harvest したテスト用表、または HANDOFF に転記した件数・命名規則）と、Editor メニューに残さない **Phase C 用の一回限り検査スクリプト出力** を evidence に保存する。最終 head に大量生成器を残さない。検査方法と生出力を bundle に置く。生成途中の PASS を流用しない。

**人間の代表操作（§3 継承。AI は手順だけ用意し PASS しない）**

1. Production Variant で通常起動。ログで `Season_Spring` Stable → `Spring_Cell_0_4` Stable → 入力 ON → 距離 Tick 開始の順を見る
2. 再起動して Whitebox + Planner。Environment なしで床・衝突・移動面
3. Workspace で `(4,2)` と `(5,2)` の Full/Whitebox、Cell+Environment、Season Lighting。主要シルエット・通路・職種ファイル
4. 他季節の同じ座標。背 `(4,5)`
5. 任意で排他入口の debug 呼び出し（Tunnel なし）が旧枝を残さないこと

### 7.11 実装順序と Phase B 停止条件

同一枝・非 squash。Play が途中 commit で緑であることは要求しない。途中 commit を独立マージ可能とは宣言しない。

1. **FW 先行** 通常例外後回収（FailedLoadCleanup + Initializing→PreUnloading + テスト期待更新）。生成器より前の別 commit
2. **P1** 一時 Plan / Wipe dry-run / Command（未実行）/ Validation の純部分とテスト
3. **P2** 人間が開いた Editor へ接続して一回生成。生ログを保存。対象 Editor が無ければ起動せず停止
4. **起動配線** SessionSeasonController、寿命 tracker / issued registry、Catalog 9×6、Factory、Lit 移行、PlayerWorldReadySequence、Result の StopWorldOperations
5. **P3** 一時生成器・旧 Cells 生成器・専用 tests の削除。最終検査を head に対して再実行

停止して A に戻す条件: 計画にない公開 API・asmdef・SceneState 値・FW Candidates setter・新しい Volume API・新しい SceneEventType、中核ロジックが単体テスト不能、World 一括削除が必要、一時生成器を日常ツールにしたくなる、S-4c/d/5 の実装が必要、`Seasons/Materials` の Material が共有 Texture/Shader 以外を参照すること、Unload 完了を既存イベントと捕捉個体では観測できず新しい FW 公開口が必要になったこと。

### 7.12 テストとレビュー計画

単体（Unity シーンなし、時間はシグナルまたは注入）: candidate 選別、空/重複 volume 拒否、枝操作の直列化と操作中拒否、Unload 早期 return を完了と誤認しないこと、Ensure が lifecycle をブロックしない、失敗/キャンセルで入力 OFF、排他後に旧枝 0、Add 完了≠Stable、遅延 Stable、WorldReady 例外。PlayerWorldReadySequence の入力順と失敗時 Focus 解除。終端 tracker の世代照合。issued registry が Dispose で Clear しないこと。Session 終了は受付停止だけで lifecycle 内 drain しないこと。StopWorldOperations 後に新規発行 0。

FW 回帰（既存 SceneDirector テスト基盤。B では実行せず C で回す）: 通常例外の PreLoad / payload / OnLoaded、Initializing 失敗、既存 Stable 親の保持、同じ identity の並行 Add、兄弟片方が失敗し他方が遅延、失敗後 Unload の収束と pending 0、再 Add 可能性、キャンセル窓前後、pending 解消と終端イベント1回。待機はシグナルまたは注入時間。

更新する既存: `WorldCellCatalogTests` の 4×4 前提。

実行しない（B）: `run-tests.ps1`、Addressables build、Unity.exe 起動。`contract-audit.ps1` まで。

C: 固定 base/head の構造レビュー、Unity tests、必要な Addressables build、全件機械検査の生出力。

A2（実施済み。同じ未コミット A1 版。互いの指摘は渡していない）:

- 観点1: GPT / OpenAI。architecture-gates。high 2 / medium 3
- 観点2: GPT / OpenAI。破壊的生成・manifest・復旧・全件検査。P1 2件 / P2 1件

同一ベンダーが A2 の両観点を担当した。C' は人間に残してある。本 A1 担当は設計に関与している。人間が本 HANDOFF や実装を読んだ場合は `独立性制約あり`。D は C/C' 突合とマージ判断として別記録。

### 7.13 残る判断事項と未確認

**製品判断は 2026-09-10 に閉じた。** 未確認は実装前の観測不足だけ。

- Addressables 現行エントリ数の再カウント、旧 GUID 全列（dry-run 待ち）
- `InGameSession.asset` の現行 Children 実体
- 論理 Season の OnPreLoaded 実機
- 線分帯をセル接続したときの Renderer AABB
- Unload 完了を既存 SceneBase 寿命だけで観測できるか。足りなければ A 再開
- HUD 複数体積ヒットは ordinal 最小の実装既定。製品判断しない

### 7.14 A2 独立レビュー結果（採否は §7.15）

観点1（architecture-gates）:

1. high: 季節交換の直列化と遅延完了が不足。並行 Add を止められない。PoNR 後は CTS だけでは止まない
2. high: `IsSceneLoaded` false と `UnloadScene` の即 return は寿命終了の保証にならない
3. medium: CompanionDriver は Stop 後に同じ個体を Start できない
4. medium: `SeasonCellNames` を internal のまま Editor から使う前提は誤り
5. medium: 距離 driver が具象 SceneDirector 依存、Player bootstrap が private、`AddScene` は SceneBase を返さない

観点2（破壊的生成）:

1. P1: F1 の Ground y=-0.5 と高さ係数が、格子箱 0..96 と四季コーナー差 2m と矛盾する
2. P1: 保存 `SceneResource.Volume` と Full+非候補合併の照合、Whitebox を GUID で読む手順、再計算の先実行禁止が不足
3. P2: P1 復帰の照合先が未定義。652 期待集合は P1 に使えない

問題ないとした契約（両観点）: Game→FW、Candidates setter なし、Lit の Scene owner、入力禁止、未承認案の明示、行数だけの分割強制なし、過削除の明確な指示なし、C' PASS の先書きなし。

### 7.15 A3 採否と人間回答

| 指摘 | 草案 | 反映 |
|---|---|---|
| 観点1-1 | 採用 | 枝操作を直列化。操作中は拒否。単一所有者が active・両 driver・終了待ち |
| 観点1-2 | 採用 | Unload の戻りと IsSceneLoaded を完了証拠にしない。捕捉した SceneBase の寿命終端まで待つ。足りなければ B で API を足さず A へ戻す |
| 観点1-3 | 採用 | companion も枝単位で破棄して作り直す |
| 観点1-4 | 採用 | `SeasonCellNames` は InGame の public。friend assembly も FW 移動もしない |
| 観点1-5 | 採用 | driver は backend + volume query。PlayerWorldReadySequence。Stable 確認は Query |
| 観点2-1 | 採用 | Ground 中心 y=0.5。季節の高さ係数を捨てる。Hue のみ |
| 観点2-2 | 採用 | §7.10 に保存値照合と Whitebox GUID 読取、再計算の後回し |
| 観点2-3 | 採用 | §7.9 に P1 復帰の完了判定 |
| 代替配置（枝操作単位） | 採用（1〜3,5 のまとめ） | `SessionSeasonController` がそれに相当。入力解禁は外側 |

不採用: なし。

人間回答（2026-09-10）:

- F1 寸法と L1 空 Lighting は生成仕様
- `Seasons/Materials` は共有 Material のみ。参照は共有 Texture / Shader 以外なら不可。現行 DemoCellLit は可
- 体積のメートル値は製品バーにしない。四季で大きさが違うのをこの緩い範囲で落とす検査、以上には決めない
- 季節の並行要求は拒否
- HUD ordinal は実装既定のまま製品判断しない

A3 の製品判断は閉じた。継続担当は §7.17 の理由で技術設計を凍結しないと判断した。Phase A snapshot は未生成。§7.16 は受領した引き渡し、最新の追加承認と再開点は §7.18。

### 7.16 受領した引き渡し（GPT。最新の引き渡しは §7.18）

発注者はこれ以降の判断を GPT に任せる。人間 C' と D のマージ判断は任さない。

**作業ツリー:** `C:\Users\void\.codex\visualizations\2026\09\09\01a08634-9d58-7d32-88f0-bfcf4f75d1ea\s-4b-a0`
**ブランチ:** `codex/s-4b-a0`
**受領時 HEAD:** `7e993db`（A0 + §7 方針初稿だけ）。受領時の A1〜A3 本文は **未コミット**だった。現在の版と判断は §7.18。引き続き staged / unstaged の双方を確認する。
**元リポジトリ** `D:\repositories\unity\SampleGameForOneStarMakerFramework` の workflow Skill / `.codex` hook は混ぜない。

**すでに閉じたこと（覆さない）**

- 一つの S-4b。生成器・生成物・起動配線は段階。非 squash。652 は全件機械検査、人間は代表操作
- 失敗は通常例外。UnderconstructionScene / 代替 Cell / 入力の先行解禁なし。FW の再試行契約は維持
- F1 寸法 + L1 空 Lighting は生成仕様
- `Seasons/Materials` は共有 Material のみ。参照は共有 Texture / Shader 以外なら不可。DemoCellLit は可
- 体積メートル値は製品バーにしない
- 季節の並行要求は拒否
- A2 指摘 8 件は採用済み（§7.15）

**次担当が判断してよいこと**

1. 本 HANDOFF を A3 凍結してよいか。よければ作業ツリー最新版の hash を Phase A snapshot として記録し、status を凍結に更新する。必要なら docs だけ先に commit する
2. 凍結後に Phase B を開始してよいか。開始するなら §7.11 の順（P1 → P2 → 起動配線 → P3）
3. Unload 完了を既存 SceneBase 寿命で観測できるか。足りなければ新しい SceneState / 公開 API を足さず A に戻す
4. Command が 500 行を超えたときの非分割再評価
5. 検査の緩い閾値の具体値（製品バーにはしない）

**やってはいけないこと**

- Unity.exe 起動、`unity test` / `unity run`、Phase B での `run-tests.ps1` と Addressables ビルド
- 人間が開いていない Editor への接続。P2 生成は開いている Editor の Pipeline / eval のみ（`osm-unity-editor`）
- FW に季節語、Candidates setter、新しい Volume API、SceneState 追加
- 一時生成器の汎用化、S-4c/d/5 の先回り、World/ 一括削除
- `Seasons/Materials` へ共有 Texture/Shader 以外を参照する Material を置く
- 人間 C' の PASS / 完了を本人回答前に書く。C 結論を C' に見せる bundle を作らない
- cursor-agent に Grok 以外を指定する。Claude を再開する

**B の完了条件（開始した場合）**

- HANDOFF 本文が正。衝突したら止まる
- 終了時 `pwsh tools/contract-audit.ps1`。Unity tests は未実行と明記
- 生成1回の生ログ・実時間を残す。時間で縮小しない
- 最終 head に大量生成器を残さない

### 7.17 継続判断 — Phase A revision 2 の再開根拠（2026-09-10。追加承認は §7.18）

担当: Codex / GPT-6 / OpenAI。Phase A の技術的な着手判定であり、Phase C / 人間 C' の結果ではない。

**判断:** 一つの S-4b、P1→P2→起動→P3 の非 squash 分離、生成仕様、652 全件検査と人間代表操作は妥当。§7.15 の採用8件と製品判断をすべて維持する。ただし現版は B の正本として未完で、Phase A snapshot は凍結しない。今回の成果は文書整理までとし、P1 のコード作成・P2 の資産変更には入らない。時間や Editor の可用性を停止理由にはしていない。

**受領した実体:** branch `codex/s-4b-a0`、HEAD `7e993db81822662bb0a79acce9d520972dc8ba73`。index は空、unstaged は docs/README.md と本 HANDOFF のみ。受領時 HANDOFF SHA-256 は `6B1E1FFC0110B410BE5FB6A126B778D68B80CBFC808CBC49A214B9CF5468D620`。これは入力照合用であり frozen snapshot hash ではない。元 checkout の workflow 3ファイルと `.codex/` は持ち込まない。

#### 7.17.1 寿命終端を既存 Query と捕捉した SceneBase だけでは観測できない

コード根拠（同 HEAD の実装を照合）:

- `SceneDirector.cs` の `GetLoadedScene` と `IsSceneLoaded` は `IsUnloadStarted` または `IsLoadCanceled` で対象を隠す。GetLoadedScene が null になったことも寿命終了の証拠にならない。
- `SceneBase.cs` の `Lifecycle` は internal、`_disposed` は private。SampleGame の controller が捕捉した通常の SceneBase から終端を待つ API は無い。`SceneLifecycleManager` 自体も internal。
- `SceneDirector.Unloading.cs` の `PhaseAfterUnloadAndDispose` は AfterUnload フック → Scene owner 資産解放 → Dispose → 辞書除去 → Removed イベントの順。`AfterUnloading` への遷移や `OnAfterUnLoadedImpl` の到達は、資産解放・辞書除去の完了より前。名前の近さで終端と同一視しない。
- `UnloadScene` には既に unload 中、pending、ロード中 cancel の即 return がある。A2 観点1-2の採用は正しく、具体的な観測口の設計がまだ必要。

既存の protected Dispose フックを利用して Game 側に通知を作ることや、既存 `SceneDirector.OnSceneEvent` の Removed / CancelCleanedUp を注入する方法は候補になる。ただし前者は辞書除去前の通知であり、後者は現在の責務マップの ISceneQuery / ISceneController 以外の観測依存になる。どちらも B の暗黙実装にせず、個体・操作との対応、購読開始時点、終了・例外・購読解放を Phase A に明記してから採用する。FW の公開 API / SceneState / friend assembly を増やさない。

#### 7.17.2 Season 一体の終了と枝全体の終了を区別する

- `SessionWorldStreamingDriver.Dispose` は Tick ループ停止だけ。`SceneDirectorStreamingBackend.RequestAdd` は CancellationToken.None で、既に投入した Add を controller が drain する口はない。
- `SessionCellCompanionLoadDriver.Dispose` は CTS cancel と in-flight 集合の Clear を行うが、Add と競合後の回収処理を await しない。Dispose 戻りを操作終了と扱えない。
- `CollectLoadedDescendants` は loading 中の子を cancel / pending に回す。その子を通常の3フェーズ待機集合には加えない。したがって Season 本体の終端だけを観測しても、すべての loading 子と遅延 Add の収束を証明できない。

補完する契約は「新規要求停止 → 旧枝が発行した全操作と捕捉個体の所有を維持 → 遅延 Add と回収を含めて収束 → 新 Season」の順。初期 Ensure だけでなく、通常距離 Tick と companion が発行する操作も対象にする。FW の再試行政策は変更しない。追跡を置く SampleGame のファイル、driver 工場との接続、Session 自身の unload との待機循環回避を責務マップに追記するまでは未解決。

#### 7.17.3 起動順序の確認対象を明確にする

§7.4 は WorldReady 成功直後に距離 Driver.Start、Player は別 continuation で Teleport → RegisterFlight → 入力 ON。§7.10 の代表確認は入力 ON → 距離 Tick。現行 RunLoopAsync には Focus 待ちがあるため、Driver.Start のログと実際の初回 Tick は別の事象である。

初期 Cell Stable 前の入力・Tick 禁止は維持する。代表操作の証拠は Start ログではなく初回 Tick を識別できるようにする。PlayerWorldReadySequence の同期区間と失敗時の Focus 登録解除を設計・テストで確認する。この確認事項だけを理由に新しい合図や公開サービスを増やさない。凍結停止の主因は §7.17.1〜2。

#### 7.17.4 次の作業と凍結条件

1. §7.17.1〜2 を SampleGame 内で満たす改訂案を作り、責務マップと単体テスト境界まで本文へ落とす。§7.17.3 の確認対象も明記する。既存 A2 採用を取り消す再レビューは行わず、不足を補う改訂部分をレビューする。
2. 同一 identity の個体差、購読前後の同期完了、Unload の即 return、キャンセル窓前後、通常 Tick / companion の遅延 Add、Session 終了競合、Player 失敗と初回 Tick を fake とシグナルで検証できる設計にする。親 lifecycle が子操作完了を待って循環しないことも含める。
3. 改訂の技術的な A2/A3 が閉じたら Phase A snapshot の path/id・生成時刻・hash を固定して B に渡す。製品判断の再承認は不要。B は従来どおり P1→P2→起動→P3。P2 で対象 Editor が開いていなければ起動せず報告する。

人間が今行う製品判断はない。次の工程は AI 側の Phase A 技術補完であり、人間 C' と D の権限は維持する。今回は Unity 接続・生成・テスト・Addressables build を実行していない。文書 commit は作業保存であって A3 凍結や B 完了を意味しない。

### 7.18 Cursor App / Grok への引き渡し（2026-09-10）

#### 7.18.1 現在位置と追加承認

作業ツリーは `C:\Users\void\.codex\visualizations\2026\09\09\01a08634-9d58-7d32-88f0-bfcf4f75d1ea\s-4b-a0`、branch は `codex/s-4b-a0`。この追記直前の HEAD は `76056ae9e3a3325e22608b6b8ea43e6c861fa8d0`。A1〜A3 と §7.17 はこの commit に保存済みであり、受領時の「A1〜A3 未コミット」は現在位置ではない。引き渡し commit の SHA は `git log -1` で確認し、staged / unstaged も必ず読む。

**まだ実装ファイルを変更していない。** FW 回収修正、生成器、652 Scene、起動配線のいずれも未実装。Phase A snapshot、B result、C/C' bundle は未生成。Unity 接続・起動・テスト・Addressables build は未実施。今回の後続作業は、発注者が「引継ぎ用のドキュメント作ってプロンプト教えて」と指示したため、引き渡し文書を整備して終了する。

発注者は通常例外後の回収不備を説明した質問に **「先行する FW 回収修正を認める」** と回答した。承認範囲は次のとおり:

- ロードが通常例外で失敗した後、途中状態の Scene が残り後続 Unload が永久保留になる経路を修正する。
- 同じ S-4b ブランチ内の先行する別 commit にする。生成器 / 生成物 / 起動 / 撤去との commit 分離を維持し、非 squash とする。
- FW の公開 API、SceneState の値、季節語は増やさない。SceneState の14値・順序は維持する。距離再試行の政策を変更しない。
- 既存テストの「非キャンセル例外の後は pending unload が残る」という期待を変えることを含めて承認された。通常例外を成功や入力 ON に読み替えず、呼び出し元へ伝える。
- この承認は具体的な cleanup 実装案を凍結したものではない。責務マップ・並行処理・回帰テストを A revision 2 に補完してから B へ進む。ほかの S-4a 残余や S-4c/d/5+ の追加許可ではない。

元 checkout `D:\repositories\unity\SampleGameForOneStarMakerFramework` は develop 上に workflow Skill 3ファイルの未コミットと `.codex/` を持つ。今回も編集・移動・stage していない。この worktree へコピーせず、一括 stage を避ける。

#### 7.18.2 確認済みの反例とコード根拠

主担当 Codex / GPT-6 と、別入力のコード調査担当が読み取りで同じ反例を確認した。これは改訂案の A2 PASS や Unity テスト結果ではない。

1. `SceneDirector.Loading.cs` の `LoadUnitySceneCore` は Loading に遷移してから `PerformUnitySceneLoad` を await する。
2. そこで通常例外が発生すると、`AddSceneCore` の cleanup は `catch (OperationCanceledException)` にしかなく、pair が残る。finally は `LoadCts` を null にするだけ。
3. `SceneDirector.Unloading.cs` の `UnloadScene` は loading 中で LoadCts が null なら `_pendingUnloads` に登録して即 return。失敗した Add は終了済みなので、以後 Stable への進行も pending の消費も起きない。
4. `SceneDirectorCancellationTests.cs` の `UnloadScene_AfterPreLoadNonCancellationException_DoesNotThrow` は PreLoad 例外後の pending 残留を実際に期待している。単なるテスト不足の推測ではない。
5. 再 Add は非 None の既存 SceneBase を再利用し、Loading 状態なら Loading→Loading の不正遷移に至り得る。ゲーム側追加リトライは解決策にしない。
6. `SceneDirector.Release` / Dispose は管理辞書と SceneBase を破棄するだけで、通常の Unity Scene unload / Scene owner 資産解放の代用にはならない。

さらに、公開 Add の unload 待ちはターゲット identity だけを待ち、待機後に祖先を集めてロードする。遅延 companion Add が旧 Cell の除去を待っている間に Session が消えると、祖先を再ロードし得る。CTS cancel のみで止まると仮定しない。

#### 7.18.3 次担当が固める技術設計

**FW 先行修正の対象候補（まだ配置・アルゴリズムは未凍結）:** `SceneDirector.Loading.cs`、`SceneDirector.Unloading.cs`、必要な既存14値内の遷移を所有する `SceneLifecycleManager.cs`、および SceneDirector / lifecycle の回帰テスト。新規 partial ファイルへ分けるなら「失敗したロードの回収」という変更理由・所有者・依存・単体テスト境界を説明する。既存 Loading ファイルは500行超なので行数警報を無視しない。

設計時に閉じること:

- cleanup は元の通常例外を保持し、未ロードを成功扱いにしない。cleanup 自体の例外が元の例外を隠さない扱いも決める。解放失敗まで無条件に回収成功とは宣言しない。
- その Add が作った資源と、既に Stable だった共有親・兄弟を区別する。無関係な既存枝を rollback しない。SceneBase の個体を照合し、同じ identity の後発個体を除去しない。
- 並列ロードの最初の失敗だけを見て、まだ動いている兄弟の payload を破棄しない。開始済み処理の観測・収束と cleanup の順序を決める。自分自身の Add 完了を待つ循環を作らない。
- PreLoad / Unity load / OnLoaded / UI Initializing / Stable hook では到達状態が違う。既存 cleanup を機械的に全例外へ流用しない。`IsActive` は Initializing を含むが、現行遷移表の Initializing→PreUnloading は許可されていない点も照合する。
- pending unload の消去、Scene owner の解放、Dispose、辞書除去、終端イベントの順序と1回性をテストで固定する。WSC の「失敗を観測し、次 Tick の再照合で必要なら再要求」はそのまま残す。

**Game 側の補完候補:** 既存 `SceneDirector.OnSceneEvent` の Removed / CancelCleanedUp は辞書除去後の通知で、捕捉個体の終端観測に使える。Game の composition 接続で注入し、pure な枝 controller から SceneDirector 具象を切り離す。R3 は現行 InGame asmdef の precompiledReferences にあるため、この観測だけを理由に asmdef を追加しない。

- 単一所有者が初期 Ensure・距離 driver・companion の全 Add / Unload を登録してから発行する。操作中拒否と同一 Season no-op の優先順位を明記し、操作中の別要求は待たせない。
- 枝の受付停止と発行済み処理の終了待ちを分ける。Dispose / Stop で記録を Clear して終了したことにしない。全旧個体と操作の収束を確認する前に次の Season を Add しない。
- 終端イベントの購読は Add / Unload より前。イベントは identity を持つが個体参照を持たないので、登録世代と個体照合、同期完了、購読解放まで設計する。Query が null になっただけでは完了としない。
- Session 自身の終了も入口から追う。現行は `ResultScene.OnStabledImpl` → `exitInGameScene` → `SceneFlow.EnterOutGame` → `SwitchScene(from=InGame)`。Session の OnPreUnload で全 drain を await するだけでは、子孫が先に unload される順序と循環し得る。Game 側の終了前処理をどこへ置くかを責務マップへ追記する。Common→InGame の逆依存を足さない。
- 既存 Result の lifecycle 内 await と新しい drain を組み合わせて自己待機を作らない。終了開始の合図・受付停止・外側の非同期完了を分ける。初期 Ensure / WorldReady の失敗は即観測可能にし、回収待ちのため入力 OFF や例外通知が永久に遅れないようにする。

**最低限の回帰テスト計画:** 通常例外の PreLoad / payload / OnLoaded、既存 Stable 親の保持、同じ identity の並行 Add、兄弟片方が失敗し他方が遅延、失敗後 Unload の収束、再 Add 可能性、キャンセル窓前後、pending 解消と終端1回、Game 枝終了中の遅延 companion、Session 終了との循環、Player 失敗時の入力 OFF・Focus 登録解除。待機はシグナルまたは注入時間とし、Task.Delay / Thread.Sleep は使わない。

#### 7.18.4 生成前の読み取り調査

この節は HEAD `76056ae` の tracked テキスト資産を読んだ結果。Editor の dirty / untitled Scene、Workspace journal、AssetDatabase での GUID 解決は未確認で、P0/P1 dry-run manifest を代替しない。

- Addressables の `AssetGroups/Default Local Group.asset` は32 entries。旧 World/Cell/Environment の Scene 21件と保持11件。group 自身の m_GUID を entry 件数へ混ぜない。
- `OneStarMakerCommon/SceneMap/InGameSession.asset` の Children は順に Result / InGameUI / PlayerScene / World。置換対象は World のリンクで、他3件を消さない。
- `SceneGraphData/Layouts/Total_Layout.asset` も旧 World / Cell / Environment の node GUID を保持する。Graph の node 削除に伴う残留参照除去として、§7.8 の編集 allowlist にこの Layout を含める。Layout 全体の無差別再生成・削除はしない。
- 承認済み `DemoCellLit.mat` の Material object は共有 URP Shader GUID `933532a4fcc9baf4fa0491de14d08ed7` を参照し、Texture と Parent は null。同じファイルの URP AssetVersion 用 Editor subasset に m_Script GUID `d0353a89b1f911e48b9e16bdc9f2e058` がある。Material 本体の参照制約と Editor のシリアライズメタデータを区別する。ファイル全体の GUID を一律「Texture / Shader 以外は禁止」と判定し、承認済み Material を誤拒否・改変しない。
- Material GUID `c03517b35199eb046852798ce73fa2d7` は維持して git mv。World 全体の削除は禁止のまま。

#### 7.18.5 担当・再開順・クレジット中断時

今回のユーザー指定は **Cursor App の Grok が主担当、Codex CLI の Astra が軽い推論で補助**。これは今回の担当指定であり、リポジトリ全体のモデル固定規則を変更しない。

1. Grok が本 HANDOFF 全文と workflow / architecture-gates を読み、§7.18.1 の追加承認を含めて A revision 2 の責務マップと実装計画を完成させる。
2. Codex CLI の Astra へ観点を分けて短い A2 依頼を出す。少なくとも責務・依存・寿命・テスト境界の構造観点を含める。同じ入力版を hash で固定し、別観点の指摘を先に渡さない。Grok が採否を統合し、確定済みの製品判断と旧 A2 の採用を覆さず、不足だけを補う。
3. 技術判断が閉じたら immutable な Phase A snapshot の path/id・生成時刻・hash を記録する。凍結したことと単に commit したことを区別する。B は別セッションへ snapshot で渡す。
4. **FW 回収修正の先行 commit → P1 一時生成器＋dry-run → P2 一回生成 → 起動配線 → P3 一時生成器撤去**。FW 先行修正と生成の前提を同じ S-4b 内で扱い、途中 commit を独立マージ可能とは宣言しない。
5. B 主担当は Grok。Astra は read-only 補助を基本とし、C を頼む場合は B と異なるモデル・新規セッション・固定 evidence を使う。A2 依頼を C の代わりにしない。C' は人間、C' PASS と D マージ判断はAIが書かない。
6. クレジットが厳しくなったら、意図を持つ小さい commit と本 HANDOFF の現況更新を先に残す。現在の Phase、実装 head、検査結果と未実行、次の1手、Editor と dirty Scene / journal の状態を記録する。生成の破壊的区間を中途半端な再実行で上書きしない。

Codex CLI の実行ファイル・指定モデルと軽い推論設定は現物で確認する。Astra の CLI 識別子やフラグは推測せず、未対応・クレジット不足なら依頼を未実施として残す。勝手に別モデルや重い推論へ切り替えない。Astra を cursor-agent 経由で実行しない。cursor-agent を使う場合は Grok 系を明示し、Claude は再開しない。

Phase B の Unity.exe 起動、run-tests.ps1、Addressables buildは禁止。人間が開いた対象 Editor のみ、osm-unity-editor に従って接続する。P2 で対象 Editor が無ければ起動せず止める。Scene / asset YAML は手編集しない。完了時 contract-audit、文書変更時 docs-audit。テストや生成の未実施を成功として埋めない。

### 7.19 貼り付け用プロンプト

**次セッション（P1 開始。Cursor App / Grok 主担当）**

```text
S-4b Phase B の P1 から再開してください。
作業ツリー: C:\Users\void\.codex\visualizations\2026\09\09\01a08634-9d58-7d32-88f0-bfcf4f75d1ea\s-4b-a0
branch: codex/s-4b-a0
HEAD: 次セッション開始時に git log -1 / status で確認（この引き渡しより後ならその SHA を正とする）
implementation base: develop 0792edc（PR #45 マージ済み）

最初に git status / HEAD を確認し、AGENTS.md、.agents/skills/osm-workflow/SKILL.md と参照、.agents/skills/osm-unity-editor/SKILL.md、docs/handoff/S-4b_WORLD_GENERATION_A0.md 全文を読んでください。正本は HANDOFF。再開点は §7.22 と §7.23。A は凍結済みなので再設計しないでください。

済んでいること: FW 通常例外後回収は PR #45 で develop に入った。この枝のコードは develop と揃っている。生成器・652 Scene・起動配線は未着手。

やる順（同一枝・非 squash。途中 commit を独立マージ可能とは宣言しない）:
1. P1: 一時 Plan / Wipe dry-run / Command（未実行）/ Validation の純部分とテスト。資産は壊さない。commit して戻せる地点にする。
2. P1 完了後、unity status が ready なら同じセッションで P2 へ進む（人間が開いた Editor への接続のみ。一回生成。生ログ保存）。
3. ready でなければ P2 に入らず止めて報告する。Unity.exe は起動しない。
4. その後が起動配線、最後が P3 撤去。P2 の途中で起動配線に食い込まない。

確定済みを維持: F1 寸法、L1 空 Lighting、Seasons/Materials は共有 Material のみ（参照は共有 Texture/Shader に限る）、体積メートル値は検査の緩い閾値、季節並行は拒否、旧 A2 採用 8 件、公開 API / SceneState 値 / SceneEventType を増やさない。

やってはいけない: 元リポジトリの未コミット workflow / .codex を混ぜる、asmdef 参照の独断追加、.unity/.prefab/.asset の YAML 手編集（Editor が届くとき）、World/ 一括削除、一時生成器の汎用化、B での run-tests.ps1 / Addressables build / unity test / unity run、C'/D の PASS 先書き。

完了時は contract-audit。文書を触ったら docs-audit。P1 と P2 は別 commit。実行済みと未実行を HANDOFF に残す。C' と D は人間。
```

**Grok が Codex CLI / Astra へ渡す補助レビュー雛形（任意。P1 構造確認）**

```text
これは Phase B 中の限定 read-only 確認です。コード編集・テスト・Unity 接続・生成・commit はしないでください。軽い推論。1観点だけ。

入力: HANDOFF path、code HEAD、観点をこの依頼に書くこと。未記載なら始めない。

AGENTS.md と architecture-gates、HANDOFF §7.7〜7.11 を読んで、P1 の配置・寿命・dry-run が計画とズレていないかだけ見てください。製品判断の再審査は不要です。

出力: 重要度順に最大5件。根拠 path/line。Phase C/C' の PASS は書かないでください。
```

### 7.20 A revision 2 技術設計（Grok / 2026-09-10。未凍結）

担当: Cursor App / Grok 4.6。code HEAD `15b9ba5` を照合。Unity 未接続。これは B の暗黙実装ではなく、§7.17〜18 の不足を閉じる設計である。製品判断と §7.15 の採用8件は維持する。Phase A snapshot はまだ作らない。

#### 7.20.1 FW 通常例外後回収

対象は `AddSceneCore` の try が OCE 以外で失敗したあと。キャンセル経路の `CleanupCanceledScene` を全例外へ機械的に流用しない。

**公開面:** 新しい FW 公開 API、SceneState 値、SceneEventType、friend assembly、季節語、Candidates setter、Volume API は足さない。終端通知は既存 `Removed`（辞書除去後の 3-phase）と `CancelCleanedUp`（ロード未完了の回収）を再利用する。

**内部追跡（公開しない）:**

- PreLoad 兄弟は既存 `_inFlightSceneBaseLoads` を使う。新しい公開表は作らない。
- Unity ロード中 identity も同様の private in-flight 表を `LoadUnityScene` の開始/finally で持つ。
- 3-phase / `RemoveScene` 実行中 identity も private in-flight を持つ。`IsUnloadStarted` は「誰かが最後まで実行中」の証拠にしない。

**開始済み処理の収束:** `newlyCreated` について PreLoad in-flight と Unity in-flight の両方を、失敗後もキャンセルせず完了または失敗まで観測してから回収を始める。`WhenAll` の一人失敗で他を破棄しない。まだ動いている兄弟の payload / SceneBase を先に破棄しない。

**回収対象の確定:** この Add の `newlyCreatedScenes` だけ。辞書の SceneBase がこの Add の個体であること。既存 Stable の共有親・無関係枝は触らない。

確定した集合を状態で分割する:

1. **IsActive（Initializing / Stable）の兄弟集合** — 逆順の個別 `RemoveScene` をしない。既存 `RunThreePhaseUnload` で集合全体を Phase1→2→3（sibling 参照保証）。Initializing は辺 `Initializing → PreUnloading` を追加してこの集合に入れる。
2. **IsInLoadingPhase** — 逆順に `CleanupCanceledScene` 相当。
3. **Unload in-flight がある** — その完了へ合流する。
4. **IsUnloadStarted だが in-flight が無い**（途中失敗で停止） — 現在状態から 3-phase の残りを再開する。新しい SceneState は足さない。

**IncrementalAlways:** 親 Add 成功後の Forget 失敗は親 catch に届かない。`IncrementalLoadAsync` の非 OCE catch が、その子 identity だけ FailedLoadCleanup する。親・兄弟の Stable 個体は保持する。ログだけで pair を残さない。

終端イベント: 集合 3-phase は `Removed`。Loading 回収は `CancelCleanedUp`。Initializing を LoadCanceled に倒さない。UI ViewIn が始まっているため ViewOut 付き 3-phase が必要。Stable からの LoadCanceled は現行どおり禁止。

**例外の扱い:** 元の通常例外を保持して呼び出し元へ再 throw する。未ロードを成功にしない。回収中の二次例外はログし、型を AggregateException に変えず元例外を隠さない。Unity unload が失敗しても辞書除去と終端イベントは出す（Game を永久待ちにしない）が、それを回収成功とは書かない。

**Unload 合流:** ロード中の UnloadScene は既存どおり pending 登録して即 return する（Add 完了を Unload が await すると、親 Unity ゲート待ちと循環する）。失敗回収は辞書除去と同時に pending を消す。回収後の UnloadScene は no-op。再 Add は新しい個体を作る。

**自分自身の Add 完了待ち循環を作らない。** 回収は `AddSceneCore` の catch 内、`_inFlightAddScenes` がまだ残っている間に実行する。回収から `AddScene` 公開入口を呼び直さない。

**テスト期待の変更（承認済み）:** `UnloadScene_AfterPreLoadNonCancellationException_DoesNotThrow` は pending 残留を期待しなくなる。代わりに辞書除去・pending 0・Unload no-op・同一 identity の再 Add 成功を固定する。

配置: 失敗回収本体は `SceneDirector.FailedLoadCleanup.cs`。Loading.cs は catch 委譲と兄弟待機と in-flight 登録だけ。行数警報は分割で応じる。

#### 7.20.2 Game 側の枝寿命と Session 終了

**観測:** 既存 `SceneDirector.OnSceneEvent` の Removed / CancelCleanedUp を使う。Game の `ISceneTerminalEvents` は identity と種別だけを渡す。Query が null / `IsSceneLoaded` false / FW の internal `ContainsScene` を完了証拠にしない。

**操作IDと個体世代を分ける:**

- `instanceGen`: その identity で観測した SceneBase 個体。終端イベント1回で、その個体に紐づく未完了 op をすべて完了する。
- `opId`: 発行した Add または Unload。同一個体への Unload は新しい個体を作らず、その個体の終端を待つ。
- 後発 Add が旧個体の Removed で完了しない。同一 identity の Add は FW `_inFlightAddScenes` で直列。Game は成功した Add の完了時に個体を捕捉して instanceGen を進める。
- 失敗した Add: FW は回収してから throw するので、await の例外時点でその Add が作った個体の終端は済んでいる。追加の Game drain はしない。
- UnloadScene の戻りは寿命終了ではない。捕捉済み個体がある Unload は終端イベントを待つ。
- 同期完了（no-op Unload）: **Game の登録履歴にその identity の未終端 instanceGen が無く、未完了 Add op も無い。** FW の辞書を覗かない。購読開始時点の既存 Session/Player/UI/Result は季節枝に入れない。Season Add が載せる NecessaryAlways 子（Lighting）は、親 Add 成功後に SceneResource.Children + Stable で観測個体として登録する。

購読は最初の Add/Unload より前（controller 生成時）。購読開始前のイベントは replay しない。

**発行済み操作:** `IssuedSceneOperationRegistry` が Ensure・距離 driver・companion の全 Add/Unload を、発行前に登録する。driver 工場は「登録してから backend を呼ぶ」decorator を SessionSeasonController が付ける。`Dispose` / `Stop` / `Clear` で記録を消して完了扱いしない。

**枝操作の優先順位:**

1. Session 終了開始後はすべての季節操作を拒否（待たせない）
2. 枝操作中の別 Season 要求は拒否（待たせない。2026-09-10 人間）
3. 同一 Season への要求は no-op
4. 受付停止と発行済み収束は分ける。次 Season の Add は、旧枝が発行した全操作と捕捉個体の終端が揃ってから

**WorldReady:** Ensure が await したその Add の FW 回収と例外再 throw は待つ（失敗した個体の終端と例外型が揃う）。その後に枝全体の issued drain や他 Cell の回収を待たない。入力は OFF のまま。これは「FW 回収より前に例外だけ先出しする」意味ではない。

**Session 終了（循環回避）:**

```
Result.OnStabled
  → IInGameSessionServices.StopWorldOperations()  // 同期。通常の新規 Add / 季節切替を拒否 + 発行停止 + WorldReady を cancel。drain しない
  → SceneFlow.EnterOutGame → SwitchScene(from=InGame)
Session.OnPreUnLoadedImpl
  → controller / driver の Dispose のみ。発行済みの await をしない。購読を外す
残留する loading 子と遅延 Add は FW のツリー Unload + 本スライスの失敗回収が所有する
```

通常の新規要求と、既に発行した Add に属する回収 Unload（companion の競合後 Unload）は区別する。後者は同一 op の続きとして Session PreUnload 前なら発行してよい。PreUnload 以降の未完了は FW へ移し、Game は待たない。

Result の lifecycle 内で枝 drain を await しない。Common→InGame の逆依存を足さない。`StopWorldOperations` は Session サービス面に置き、Result は親サービスだけを見る。

`InGameSession.OnLoaded` が composition: SceneDirector 具象から `ISceneStreamingBackend` / `ISceneVolumeQuery` / `ISceneTerminalEvents` を作り、純な `SessionSeasonController` へ渡す。controller 内で SceneDirector を見ない。

#### 7.20.3 起動順序の確認対象（§7.17.3）

初期 Cell Stable 前の入力・通常距離 Tick 禁止は維持する。Driver.Start のログは初回 Tick ではない。代表操作とテストの証拠は、Focus 登録後の最初の `TickOnce` / WSC.Tick を識別する。新しい合図や公開サービスは増やさない。

`PlayerWorldReadySequence` は WaitUntilWorldReady 成功後に Teleport → RegisterFlight → Input ON。失敗時は Input OFF、既に登録した Focus を Unregister。同期区間は Sequence の単体テストでシグナル確認する。

#### 7.20.4 検査の緩い閾値（製品バーではない）

同じ (x,y) の四季 AABB: 各軸のサイズ差が 2m を超えたら生成バグとして落とす。
格子箱 `origin=(x*250,0,y*250) size=(250,96,250)` から Renderer AABB が 5m を超えてはみ出したら失敗。
自己体積は有限かつ 1 m³ 超。Collider のみは寄与しない。これらは焼き誤差で落ちないための検査閾値であり、品質バーではない。

Command が 500 行を超えたときは「一回の作成順」として非分割を再評価する。P1 実装時に判断し、その場で汎用 Tool にしない。

#### 7.20.5 限定 A2 の対象

既存 A2 の再審査はしない。見るのは §7.20 と更新した責務マップだけ。観点は分け、互いの指摘を渡さない。

1. FW の通常例外後回収と並行ロード（architecture-gates: 状態・所有・兄弟待機・公開面）
2. Game 側の枝寿命と Session 終了（終端観測・発行済み所有・循環）

指摘の採否は Grok が統合する。C/C' PASS は書かない。

### 7.21 限定 A2（Astra）採否と凍結（2026-09-10）

入力: HANDOFF SHA-256 `A3F9C0115121A2F338A9B7C8A8BBC78F3D5603F8D88DEA85B4A36F67E7D3F31D`、code HEAD `1cce49a`。Codex CLI / `gpt-6-astra` / `model_reasoning_effort=low`。観点1と観点2は別プロセス、互いの指摘は渡していない。製品判断と §7.15 の採用8件は再審査していない。

| 指摘 | 採否 | 反映 |
|---|---|---|
| FW-1 PreLoad 兄弟が Unity in-flight だけでは待てない | 採用 | 既存 `_inFlightSceneBaseLoads` も失敗後に観測。§7.20.1 |
| FW-2 逆順個別回収が active 兄弟の 3-phase を壊す | 採用 | IsActive 集合は `RunThreePhaseUnload`。§7.20.1 |
| FW-3 `IsUnloadStarted` は実行中の証拠にならない | 採用 | unload in-flight へ合流。停止した unload は残りフェーズを再開。§7.20.1 |
| FW-4 IncrementalAlways 単独失敗の所有者が無い | 採用 | `IncrementalLoadAsync` の非 OCE がその子だけ回収。親は保持。§7.20.1 |
| Game-1 操作世代と個体寿命が未分離 | 採用 | `opId` と `instanceGen` を分離。同一個体の終端は1イベント。§7.20.2 |
| Game-2 同期完了が FW internal に依存する | 採用 | Game の登録履歴だけで no-op を証明。Lighting は親成功後に観測個体登録。§7.20.2 |
| Game-3 Session Dispose 後の回収 Unload 所有者 | 採用 | 新規要求と既存 Add に属する回収を区別。PreUnload 以降は FW 所有。§7.20.2 |
| Game-4 WorldReady 早期失敗と FW throw 順 | 採用（意味の明確化） | その Add の FW 回収＋throw は待つ。枝全体 drain は待たない。§7.20.2 |

不採用: なし。新しい公開 API 要求は出ていない。

**凍結判断:** revision 2 の技術設計は B の正本として足りる。Phase A を凍結する。実装は FW 先行 commit から。P2 で対象 Editor が無ければ起動せず止める。C' と D は人間のまま。

追加する回帰（B では書けるが実行しない）: PreLoad 片失敗の兄弟収束、active 兄弟の集合 3-phase、stuck Unloading の再開、Incremental 子回収で親保持、instanceGen と後発 Add、Game 履歴による no-op Unload、Stop 後の回収 Unload 許可、WorldReady が枝 drain を待たないこと。

### 7.22 PR #45 マージ後（2026-09-11）

FW 通常例外後回収は [PR #45](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/45) として `develop` `0792edc` へ入った。恒久契約は Architecture §05（Initializing → PreUnloading、FailedLoadCleanup partial、pending は finally で消す）。この HANDOFF は削除しない。残作業は P1 生成器+dry-run → P2 → 起動配線 → P3。

再開順の §7.18.5 項 4 は次で置き換える: **P1 から始める。** FW 先行 commit は済んでいる。C' と D は人間のまま。

### 7.23 次セッション（P1。Editor は P2 用）

HANDOFF 本文が正本。新規の引き渡しファイルは作らない。

- **P1:** 生成器コードと dry-run。Command は書いても実行しない。旧 World は壊さない。Editor なしでもコードは書ける。
- **P2:** 人間が開いた対象 Editor（この worktree の `unity/`）へ `unity status` が `ready` のときだけ一回生成。無ければ起動せず停止。
- **自動進行:** P1 を commit したあと Editor が ready なら、同じセッションで P2 に進んでよい。閉じていれば P1 で終わる。P2 の途中で起動配線へ進まない。

人間が次セッションで P2 までやらせたいなら、セッション開始前にこの worktree の Unity プロジェクトを開いておく。開く Editor は `D:\repositories\unity\SampleGameForOneStarMakerFramework` ではなく、この作業ツリー `...\s-4b-a0\unity` であること。

### 7.24 Phase B P1 実装記録（2026-09-11）

担当: Codex / GPT-6 / OpenAI。このセッションの人間指定により **P1 のみ**。§7.23 の P2 自動進行は今回は行わない。Phase A revision 2 本文・採否は変更していない。開始 HEAD は `293c14a`、branch は `codex/s-4b-a0`、開始時の index / worktree は clean。implementation base は local develop `0792edc0d05ad7eaecaf95ce06c48da7688d308f`。この節を含む `feat: add S-4b P1 season world plan, wipe dry-run, and validation` commit が P1 の保存地点であり、単独マージ可能な成果物とは扱わない。

**追加したコード**（既存 asmdef / friend assembly で収まり、参照追加なし）:

- `SampleGame.DependOnAll.Editor/WorldAuthoring/SeasonWorldGenerationPlan.cs`: 440 identity / Resource、652 payload Scene、出力 path、F1 の形状値、四季 Hue オフセット。線9座標は本文が指定する世界稿 §3 の既定座標を使用。AssetDatabase / Scene I/O なし。
- 同 `SeasonWorldWipe.cs`: 個別ファイルの allowlist、削除・更新・保持の path / GUID / identity / dependency、Map hash / membership・Graph edge・Addressables entry の読み取り snapshot。削除直前に全削除 GUID を照合。ディレクトリ削除はしない。Layout は §7.18.4 の Total_Layout の旧ノード位置だけを除く。
- 同 `SeasonWorldGenerationCommand.cs`: 一回の I/O 順序。対象 project path、dirty / untitled Scene、Workspace journal、生成先・identity 衝突、共有 Material 本体を事前検査。manifest と時刻・件数・失敗地点を保存し、再実行は既存ログで拒否。Wipe、GUID を保つ Material 移動、Scene / Graph / Resource / Map / Addressables、初回 volume bake、非修復の検査へ進む。P1 では実行していない。Command は約230行で500行警報未満、汎用 Tool は追加していない。
- 同 `SeasonWorldValidation.cs`: 期待集合・path・親・LoadType・候補・Generated・payload・GUID・Addressables の値照合と、有限かつ1m³超 / 格子外5m / 四季サイズ差2mの純検査。Editor 読者は Graph/Map、旧 GUID 残留、保持 GUID / 参照、Full / Whitebox / Environment の Renderer 体積と保存値を検査する。検査自身は再計算・修復しない。
- `SampleGame.InGame/InGameSession/Streaming/SeasonCellNames.cs`: Plan が必要とする public 名称規則だけ。Season controller / tracker / registry / Catalog / Factory / Player の配線は変更していない。
- `OneStarMaker.Tests.Editor/SeasonWorldGenerationTests.cs`: 件数・path・F1・canonical identity、allowlist の境界と dry-run 非変更性、Whitebox 欠落 / GUID 重複 / path・policy 不一致、体積異常の回帰。既存 WorldCellGeneratorTests と同じ asmdef。**テストは未実行**。

**P1 の読み取り証拠:** `TestResults/S-4b-P1/dry-run-filesystem.json`（gitignored、同ディレクトリに再採取用 `capture-p1.ps1`）。tracked 資産970件について path / GUID / identity / SHA-256 / meta / シリアライズ済み GUID 参照を採取。削除候補64資産＋対応meta、更新候補22、保持884。旧 Scene は21（World 1 + Cell 16 + Environment 4）、旧 identity は21。Default Local Group の Addressables entry は32。JSON SHA-256: `00053A8C1F9A33EAD1557381C74CF143A1E47B9DDC26AA15B1F1FFBF5649F259`。これはファイル読み取りの dry-run 証拠であり、AssetDatabase で `SeasonWorldWipe.Capture` を実行した証拠、Editor の未保存状態の確認、P0 完了を代替しない。Editor 側の dry-run は P2 着手前に別途保存する。

**実施 / 未実施:** contract-audit は違反なし。通常 sandbox では pwsh の解決に失敗したため、既存 PowerShell 7 へ昇格した実行で exit 0 を確認。docs-audit も違反なし。Unity コンパイル、Unity tests、Editor 接続、生成 Command / Wipe、Material 移動、Addressables build は未実施。旧 `.unity` / `.asset` / `.mat` / `.prefab` とその meta は変更していない。C / C' / D の完了・PASS 判定はしていない。

**残る確認:** MPB は Scene にシリアライズされず、既存コードにも再適用処理が無いため、P1 は Plan の Hue 値まで。永続的な色適用は後続 Lit 起動配線で扱う範囲解釈を質問したが、この記録時点では人間回答を得ていない。色が完成済みとは扱わない。P2 の実 Editor I/O、652 Scene の実在・GUID・体積の全件実測、Light/Volume/LightingSettings 不在、共有材の見た目、生成・復旧所要時間は未検証。

**P2 の入口:** この P1 commit と旧資産を保持したまま、対象 worktree の Editor が ready であること、clean な git / Scene / Workspace journal を確認する。`SeasonWorldGenerationCommand.DryRun()` で `artifacts/s-4b-p2/dry-run.txt` を保存し、削除・更新・保持・新規 path を確認する。その後にのみ `SeasonWorldGenerationCommand.Generate(expectedProjectPath)` へこの worktree の `unity` 絶対 path を渡す。P2 は別 commit。失敗時は自動再実行せず §7.9 の一体復旧を行う。起動配線と P3 はさらに後。P3 で一時コードを撤去しても、P1 履歴と最終 head 向け検査の生証拠を残す。

### 7.26 次セッション（2026-09-12。P1 を分けて PR、実装継続は a0）

HANDOFF 本文が正本。新規の引き渡しファイルは作らない。Unity Editor は **この PR のレビューと P1 分割には不要**。

**レビューセッション（この PR: `codex/s-4b-p1` → `develop`）**

- 見るのは P1 だけ。base は現在の `develop`。S-4b 作業枝に残っていた FW 回収の重複 SHA と、P2 の 652 Scene（約 4000 files）は載せない。
- 元の単一 commit `990fed2 feat: add S-4b P1 season world plan, wipe dry-run, and validation` を、変更理由ごとに分割した。順序は SeasonCellNames → Plan → Wipe → Validation → Command → Tests → 本 HANDOFF。
- Command は P1 では実行していない。テストも未実行。contract-audit は元 P1 時点で exit 0。
- 単独マージ可能な完成物とは扱わない。受け入れは生成物＋起動＋P3 撤去の一組。

**実装継続セッション（worktree `...\s-4b-a0` / 枝 `codex/s-4b-a0`）**

- HEAD: `0835d8a feat: generate S-4b season world scenes and resources`。Generate は一回済。`artifacts/s-4b-p2/generation.log` があるので再実行しない。
- 未コミット: 起動配線（SessionSeasonController、tracker / registry、Factory、PlayerWorldReadySequence、Result の StopWorldOperations、Catalog 9×6）と Streaming のフォルダ分け（`Interfaces/{IssuedOperations,PlayerReady}` と `Runtime/{Season,IssuedOperations,Distance,Companion,PlayerReady}`）。名前空間は `SampleGame.InGame.Streaming` のまま。
- 次の commit 順: 起動配線（P2 と混ぜない）→ P3（一時 `SeasonWorld*`、旧 `WorldCellStreamingSliceCreator` / `WorldCellGenerator` と専用 tests を置換確認のうえで削除）。
- `MobileDependencyResolver` の pdb.meta 削除は Editor ノイズ。コミットしない。
- Phase B では `Unity.exe` 起動、`run-tests.ps1`、Addressables ビルド、`unity test` / `unity run` は禁止。C' / D は人間。PASS 先書き禁止。
- 本リポジトリ `D:\repositories\unity\SampleGameForOneStarMakerFramework` の `develop` では生成しない。

### 7.27 Phase C（P1。2026-09-11）

担当: Cursor Cloud / Grok 4.6 / xAI。Phase B 実装は Codex / GPT-6。モデル系列は異なる。C' は人間のまま。この節は C' に渡さない。

**対象:** implementation base `17dc67b` / head `f3597ad`。evidence `docs/handoff/evidence/s-4b-p1-c`。blind 手順は `docs/handoff/evidence/s-4b-p1-cprime-blind/README.md`。

**機械検査:** `pwsh tools/contract-audit.ps1 -BaseRef develop` exit 0（検査1 差分 6 .cs）。`pwsh tools/docs-audit.ps1` exit 0。Unity Editor 無し。`run-tests.ps1` 未実行。Command / Wipe.Execute / Addressables build 未実行。

**構造適合（機能より先）:** §7.7 の一時 Editor 4 ファイルと public `SeasonCellNames`、Tests.Editor 配置は計画どおり。asmdef 参照追加なし。Game→FW、季節語の FW 露出なし。Command 229 行で 500 警報未満、一回の作成順として非分割妥当。Validation の純関数（Compare / Finite / CompareVolume）と Inspect の分離は同一ファイル内で計画どおり。Wipe と Generate は復旧地点が違う分割を維持。行数警報の新規ファイルはすべて 500 未満。Catalog 9×6 と起動配線は未着手（P1 の対象外維持）。Phase A 再開は不要。

**受け入れ（P1 範囲）:** Plan は論理 440 / Scene 652。identity は `SeasonCellNames` と一致。Wipe allowlist はディレクトリ・runtime `.cs`・共有 Material・範囲外を拒否。Inspect は再計算・修復しない。Command に Generate メニューは無い。

**findings ledger（採否は人間 / D）:**

| id | severity | category | 内容 | 根拠 | 状態 |
|---|---|---|---|---|---|
| C-1 | medium | semantic | 体積の公式読者は `OpenScene` Additive、Validation.ReadVolume は `OpenPreviewScene`。保存値照合の許容は 0.001m。単純 Cube なら一致し得るが、§7.10 の読者を共有していない | `SeasonWorldValidation.cs` 207–219 / `SceneVolumeSceneReader.cs` 88 | proposed。P2 前に公式読者へ寄せる |
| C-2 | medium | semantic | `RecalculateAll()` は非 batch で `SaveCurrentModifiedScenesIfUserWantsTo` を呼ぶ。P2 は開いた Editor が前提。dirty なら対話、Cancel なら bake 0 のまま Inspect 失敗し journal が残り再実行できない | `SeasonWorldGenerationCommand.cs` 115 / `SceneVolumeRecalculator.cs` 67–70 | proposed。対話を避ける入口が必要 |
| C-3 | medium | semantic | `Wipe.Capture` は全 SceneResource と `AddressableAssetsData/` を Update にする。§7.24 のファイル読み取り「更新 22」と定義が違う。Keep の参照不変は Update に適用されない。Generate が全 Resource を書き直すなら一部は正しいが、dry-run の人間レビューを濁す | `SeasonWorldWipe.cs` 86–105 | proposed |
| C-4 | low | obvious | L1 検査は Renderer / 余分な Component を見る。scene の LightingSettings 割り当ては見ない。Light / Volume は Component で落ちる | `SeasonWorldValidation.cs` 215–216 / §7.3 | proposed |
| C-5 | low | obvious | §7.8 の「policy は全 Generated」に対し、Compare は Cell だけを見る。Environment は旧 `CellAuthoringPolicy` の既定 Generated に依存。P3 で policy を消すとき検査が消える | `SeasonWorldValidation.cs` 49 | proposed |
| C-6 | low | obvious | `Format` は `y < 0` でも `nameof(x)` で投げる | `SeasonCellNames.cs` 32 | proposed |
| C-7 | low | machine | 旧 header の implementation base `0792edc` はこの PR の merge-base `17dc67b` とずれていた（PR #46）。本節で訂正 | HANDOFF header | 記録で訂正 |
| C-8 | low | obvious | 公開面 `docs/README.md` は handoff を「4 つだけ」と書く。作業台は 5 ファイル。docs-audit は数を見ない | `docs/README.md` 37–44 | proposed。P1 範囲外でも公開面の現況違反 |
| C-9 | low | machine | Wipe テストに現行 Environment 実パス `World/Cells/Cell_0_0/Environment_0_0.unity`（Cell 直下の兄弟）が無い。正規表現は通す | `SeasonWorldGenerationTests.cs` 80–92 | proposed。テスト穴 |

**非指摘:** 線 9 座標は世界稿 §3.1 と一致。オフラインで線帯 AABB を計算し、9 セルとも格子半幅 125m 内（最小余裕 11.4m）。Hue 0/0.08/0.16/0.24。論理 Season は payload 空。Lighting は空ルート。Material は MoveAsset。ディレクトリ削除なし。MPB 非永続は §7.24 のとおり色完成ではない。

**未確認:** Unity コンパイル、EditMode テスト、AssetDatabase の Capture、Command.Generate、652 実体、共有材の見た目、LightingSettings の実シーン、P2 所要時間。

### 7.28 Phase B 指摘修正（P1。2026-09-12）

担当: Cursor App / GPT-6（Grok 4.6 xAI の修正方針分析を入力に実装）。P2 の別 worktree `codex/s-4b-a0` と生成物・起動配線には触れていない。

- C-1: `SeasonWorldValidation` の体積読者を preview scene から公式 reader と同じ `OpenSceneMode.Additive` に変更し、空 Bounds を合併から除外した。既に開いている Scene は閉じない。
- C-2: `SaveHookSuspended` 中の一括生成では `RecalculateAll` の保存ダイアログを出さない。通常メニュー実行は従来どおり dirty Scene を確認する。
- C-3: dry-run の Update を全 SceneResource / Addressables 全域から、明示配線資産・全 graph・Addressables settings と実際に旧 entry を含む group / default group に限定した。Keep の依存不変検査を有効にする。
- C-4/C-5/C-6/C-8/C-9: LightingSettings の保存 YAML 検査、Environment の Generated 検査、座標別例外引数、公開 docs の handoff 件数、現行 Environment 実 path のテストを追加した。
- C-7: 既に §7.27 で修正済みのため追加変更なし。

`pwsh tools/contract-audit.ps1 -BaseRef develop`、`pwsh tools/docs-audit.ps1`、`git diff --check` は exit 0。Phase B のため Unity Editor、EditMode tests、Addressables build、Command は未実行。実装差分が変わったため §7.27 と `s-4b-p1-c` / `s-4b-p1-cprime-blind` は失効し、新しい implementation head を固定して Phase C と C' bundle を作り直す。C / C' PASS は記録しない。

**判定:** P1 の配置は計画に合う。C/C' PASS と D のマージ判断は書かない。C-1 / C-2 / C-3 は P2 の一回生成より前に人間が採否する。

