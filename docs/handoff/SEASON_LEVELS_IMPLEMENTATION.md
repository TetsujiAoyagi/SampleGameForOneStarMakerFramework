# 四季 Level と 4 動詞実証の実装計画 ハンドオフ (2026-08-26 / 2026-08-27 改訂)

> Phase A（計画）: Cursor Cloud Agent
> 対象スライス: **S-3「`WorldCellCatalog` を矩形集合へ一般化する」のみ**。世界構図と Level の中身は S-3C（Fable 依頼）。S-4〜S-9 は §1 に方針だけ確定させ、実装はしない
> 分解と順序の正本: [§33](../../unity/Assets/Docs/Architecture/33-sample-demonstration-boundaries.md)。前スライス: S-1「生成器の非破壊化」（harvest 済み → [§21 §6 / R-6](../../unity/Assets/Docs/Architecture/21-scene-streaming.md)）
> **実装・検証はローカルマシンの Cursor Editor セッションで行う**（発注者決定 2026-08-26）。本書を書いたクラウド VM に Unity は無く、Phase A は静的調査のみ。§0.3 の実測はコード読解と `git grep` によるもので、Play / テスト実行は一切していない
>
> **2026-08-27 改訂:** 初版 §1.3 の座標確定と §1.5 の季節テーマ表を撤回した。理由は §0.4。発注者の指摘は「大きさ・置き方が微妙」と「Fable の創造性は見たい。依頼の仕方ごと直せ」の 2 点。
>
> **2026-08-27 Cloud 改訂:** Unity シーン操作の手順、CLI / Skills、変更対象の欠落、A-1〜A-3、テスト主体を詰めた。この Cloud セッションでは CLI を動かさない。接続確認はローカル。

---

## 0. 1分で把握

### 0.1 何をするか

§33 が設計した Season / Tunnel / 4 動詞（Build / Commit / Checkout / Streaming）を実装へ落とす。本書はその作業指示である。

**初版との最大の差:** 世界の大きさ・置き方・中身は計画セッションが決めない。S-3 は機構だけを先に通し、構図と Level は制約カードを渡して Fable に設計させる（§1.3 / §1.5）。

### 0.2 発注者決定（§33 の記述より新しく、優先する）

| 日付 | 決定 |
|---|---|
| 2026-08-26 | **Level コンテンツは生成器で大まかに作ってよいが、人または AI の手による編集を正とする。** → §1.2 で D-6 を改訂する |
| 2026-08-26 | **実装・検証はローカルの Cursor Editor セッションで行う。** クラウドエージェントは計画とコード下書きまで（Unity が無いため生成器実行・テスト実行・Play 確認ができない） |
| 2026-08-27 | **大きさと置き方を計画側で確定しない。** §33 の「要る大きさ」は最小であり、構図ではない。世界構図と Level の中身は Fable への設計依頼にする（§1.5 のプロンプトを使う） |
| 2026-08-27 | **開いている Editor への Unity CLI（`unity command` / `unity eval`）は可。** Unity.exe 起動・`run-tests.ps1`・`unity test` / `unity run`・Addressables ビルドは禁止。接続できる Editor があるとき `.unity` / `.prefab` / `.asset` の YAML 手直しは禁止。`com.unity.pipeline` は manifest 宣言のみ（lock はローカル Editor が書く） |

### 0.3 現況の実測（2026-08-26 クラウド側で確認済み。やり直さないこと）

| 領域 | 実測 | 所在 |
|---|---|---|
| グリッド | dense 4×4 の const が正本。`WorldGridDefinition.asset` は写し（`EnsureGridDefinition` が毎回上書き） | `SampleGame/InGame/InGameSession/Streaming/WorldCellCatalog.cs` |
| Controller | `0..GridWidth × 0..GridHeight` の dense 全走査で desired / retain を計算。`StreamingConfig` が `GridWidth` / `GridHeight` を保持 | `OneStarMaker/Scripts/Runtime/Streaming/` |
| 飛行速度 | `FlyController._moveSpeed = 42` m/s（ブースト 2.4 倍で約 100 m/s） | `SampleGame/InGame/InGameSession/PlayerScene/FlyController.cs` |
| 正本 policy | 南辺 4 枚 `(0,0)(1,0)(2,0)(3,0)` = `HandAuthored`、他 12 枚 = `Generated` | `SampleGame/DependOnAll/Editor/Cells/CellAuthoringPolicy.cs` |
| 南辺ハードコード | `HandAuthoredCells` / `EnvironmentSproutCells` / `HandEditProbe` / 生成器完了ログの 4 箇所が南辺 4 枚を直書き | `CellAuthoringPolicy.cs` / `WorldCellStreamingSliceCreator.cs` / `HandEditProbe.cs` |
| セル実体 | 16 フォルダ。Environment `.unity` は南辺 4 枚のみ。中身は Cube 地面 + モチーフ 4 種の Primitive | `SampleGame/InGame/InGameSession/World/Cells/` |
| Variant | `.asset` の `Variant:` は **52 ファイル全て空文字**。非空値ゼロ | SceneMap / Cells / SceneGraphData |
| Addressables | グループは `Default Local Group` **1 個**（28 エントリ）。`Remote.LoadPath` 未定義。`RemoteFull.asset` / `VariantHybridPlayModeScript.asset` はメニュー実行待ちで未生成 | `AddressableAssetsData/` |
| §20 の機構 | `VariantFilteringBuildScript` / whitelist / Hybrid Play / `TryLoadRemoteCatalogAsync` / `RemoteCatalogRuntimeBridge` は**実装済み**。S-7 は新機構ではなくデータ（タグ・グループ・プロファイル）を流し込むスライス | `OneStarMaker/Scripts/Editor/Build/Variants/` ほか |
| テスト | WSC 10 + MultiFocus 3 + 統合 6 / 生成器 7 / `CellPopulationPlan` 13 ほか。全て EditMode。CI（GitHub Actions）は DebugStudio の `dotnet test` のみで、Unity テストはローカル `pwsh tools/run-tests.ps1` | `OneStarMaker/Tests/` |

### 0.4 初版 §1.3 / §1.5 を撤回する理由

初版は O-1（§33 が「S-3 で確定」と開けておいた構図）を、次のレイアウトで埋めた。

```
春 2×2 (x 0..1) — 空隙 3 列 — 夏 4×4 (x 5..8) — 空隙 3 列 — 秋 2×2 (x 12..13) — 空隙 3 列 — 冬 2×2 (x 17..18)
```

これは空隙条件を満たす**最安の合法解**であって、世界の構図ではない。問題は 3 段ある。

1. **§33 の「要る大きさ」を設計寸法にした。** 「2×2 で足りる」は Commit / Checkout / Build の証明に必要な最小で、500 m 四方（飛行 42 m/s で横断約 12 秒）は庭である。3 季節が同じアスペクトの切手、夏だけが「本物の場所」に見える
2. **算術的に置き、一列に並べた。** 座標 `0, 5, 12, 17` は計算しやすいだけで、地図として読む理由が無い。トンネルは隔離装置であり往来の街道ではないのに、街道のように見える
3. **創造の枠を計画が先に埋めた。** 桜 / 紅葉 / 雪だるまは四季ポスターの既定解である。確定方針にした瞬間、Fable が設計する余地が消える。計画セッションに「全部書いて完成に見せる」を求めると、開いた論点はこうして潰れる

**教訓（依頼の仕方）:** 計画セッションに構図を確定させるな。制約と品質バーと「答えてはいけない既定解」を渡し、設計セッションに構図を出させ、人が図を見てから座標をコードへ落とす。

---

## 1. 確定方針（Phase A の設計判断。実装で変えないこと）

### 1.1 スライス分解。1 スライス = 1 ブランチ = 1 HANDOFF

本書は **S-3 の HANDOFF を兼ね、S-3C の依頼文を同梱する**。S-4 以降は着手時に新しい HANDOFF を切り、本節の方針を引き継ぐ。

| # | 内容 | 前提 | 誰が |
|---|---|---|---|
| **S-3** | Catalog / Config / 生成器入力を「矩形の集合」へ一般化する。**本番レイアウトは現行 4×4 を矩形 1 個のまま残す。セルは動かさない** | §33 | ローカル（機構） |
| **S-3C** | 世界構図（大きさ・置き方）と Level のシルエット。人が図を承認してから移送・再生成 | S-3 | **Fable（設計）+ ローカル（実装）** |
| S-4 | Season Level 復活。生成器が `Season_*` を吐き `World` を置き換える。初回季節の Ensure 問題（§33 §8 注記）もここで裁定 | S-3C | ローカル |
| S-5 | Tunnel 常設 1 本。滞在中の明示 `AddScene` と D-5 の失敗経路 | S-4 | ローカル |
| S-6 | 季節ごとの Addressables グループ = **Build 実証（冬）** | S-4 | ローカル |
| S-7 | 季節 Variant タグ + 未 Checkout 経路 = **Checkout 実証（秋）** | S-5, S-6 | ローカル |
| S-8 | 春の職種別コンテンツの作り込み = **Commit 実証**。`HandEditProbe` と生成器のスキャフォールド宣言を退役 | S-3C | ローカル（S-3C で骨格、ここで密度） |
| S-9 | 夏で [§21](../../unity/Assets/Docs/Architecture/21-scene-streaming.md) の T-07〜T-09 = **Streaming 実証** | S-4 | ローカル（実測） |

**§21 の T-07〜T-09 を S-9 まで動かさないこと**（§33 §12。数値が季節化のあとで取り直しになる）。

S-3 と S-3C を混ぜないこと。混ぜると、機構を通すためにまた最安レイアウトが確定する（§0.4 の再発）。

### 1.2 正本 policy の改訂 — 「編集が正」を既定にし、夏だけ `Generated` に残す（D-6 改訂）

発注者決定を §21 §6 の既存機構に載せる。**新しい policy 種別は作らない。** `HandAuthored` の意味論（「`AuthoredRoot` があれば触らない。無ければ初回スキャフォールドとして生成する」）が「生成器で大まかに作り、以後の編集を正とする」そのものである。

| 季節 | 正本 policy（改訂後） | §33 D-6 からの変化 |
|---|---|---|
| 春 | `HandAuthored` | 変化なし |
| 夏 | **`Generated`** | 変化なし |
| 秋 | `HandAuthored` | `Generated` → 変更 |
| 冬 | `HandAuthored` | `Generated` → 変更 |

**夏を `Generated` に残す理由（全季節 `HandAuthored` 化は却下）:**

1. §21 §6 は「どちらか一方に決める必要はなく、**決めてもいけない** — 両方を同居させられることがサンプルの証明対象」と明記している。全季節を編集正本にすると、この証明の `Generated` 側が世界から消える
2. 夏は Streaming 計測（A-1 / A-2）の場である。手編集でセルごとの密度が揺れると、計測値が編集履歴に依存して再現しなくなる。均質な量産グリッドのまま保つ

**AI の編集も「手編集」である。** 生成器が守るのは `AuthoredRoot` 配下だけなので、AI セッションが `.unity` を編集するときも必ず `AuthoredRoot` 配下に置くこと。外に置いたものは次の生成で消える。

**§33 への反映:** S-3C の Phase D で §33 の D-6 行と §5 表の「正本 policy」列をこの割り当てへ改訂する。それまで §33 と本書が食い違う期間は本書 §1 が優先。レイアウト座標の harvest も S-3C 承認後であり、S-3 では書かない。

### 1.3 レイアウト — 固定するのは幾何条件だけ（O-1 は開けたまま）

§33 §7 の一文をそのまま守る。**並びと座標と「最小を超える寸法」は S-3C が答える。**

固定する条件:

| # | 条件 |
|---|---|
| L-1 | 単一座標空間に季節 **矩形 4 つ**（D-1）。L 字や穴開きは矩形集合の契約を破るので採らない |
| L-2 | 異なる矩形に属する最近セル中心間距離 > `UnloadRadius`（現行 550 m）。空隙 1 セル（中心間 500 m）は不可。2 セル（750 m）以上 |
| L-3 | `CellIdentity` は非負の `Cell_{x}_{y}`。FW に季節の語彙を入れない |
| L-4 | 夏は **少なくとも 4×4**（Streaming が半径を跨ぐため）。春 / 秋 / 冬は **少なくとも 2×2** |
| L-5 | トンネルは常設 1 本（D-4）。隙間を街道として飾らない。隙間は隔離帯 |
| L-6 | セル増やす提案は Streaming の証明としては却下（§33 §4）。**場所として必要な増分は別理由**で、§1.5 の予算内なら可 |

S-3 の本番データは現行 4×4 を矩形 1 個として残す。複数矩形の挙動は **テスト用フィクスチャ** で検証する（§3 T-A / T-B）。これで機構は通るが、世界はまだ動かない。

### 1.4 既存 16 セルの移送（O-2）— 手順は確定、行き先は S-3C 待ち

- **`Generated` 12 枚は捨てる。** 生成物なので再生成できる。破壊経路 3 に削除させる
- **`HandAuthored` 南辺 4 枚は春矩形へ移す。** 相対的な並び（横一列の 4 枚）を春のどの 4 マスへどう畳むかは、春矩形の寸法が決まってから決める。2×2 なら 2×2 に畳むし、3×2 なら余白が出る
- **順序は「構図の承認 → 移送 → 生成器」**。生成器を先に走らせると、南辺のうち春に入らないマスが「範囲外だが保持」の孤児になる（消えはしない。R-6）
- ワールド座標の補正は、移動したセルごとに「旧セル原点 → 新セル原点」の Δ を `AuthoredRoot` に足す（手段は §4.2。YAML の `m_LocalPosition` を手で足さない）
- 追随: フォルダ / `.unity` / `.asset` のファイル名、`.asset` 内 identity、SceneGraph ノード、Addressables address、§0.3 の南辺ハードコード 4 箇所

**S-3 では移送しない。** 手段（`move_asset` → identity → `set_transform`）は今確定し、実行は S-3C。S-3 が YAML 前提の API を作らないため。

### 1.5 S-3C — Fable への設計依頼

#### 1.5.1 依頼の切り方（これを破ると §0.4 が再発する）

| やってよい | やってはいけない |
|---|---|
| 制約カードと品質バーと既定解の禁止を渡す | 桜・紅葉・雪原など、中身の答えを先に書く |
| 「10 秒で何が見えるか」を季節ごとに 1 段落出させる | 計画セッションで座標表を埋めて確定にする |
| 人が overhead 図を見てから Catalog に落とす | 機構 PR のついでに最安レイアウトをマージする |
| Primitive の範囲でシルエットと職種分割を設計させる | 新しいアセットパイプラインや地形ツールを足させる |

創造性の置き場はカラーパレットではない。**地図の構図、矩形の縦横比、横断にかかる時間、職種で割ったオブジェクト、差し替え可能な 1 個の目印**である。グレースケールの overhead でも四季が区別できるなら、その構図は色に頼っていない。

#### 1.5.2 制約カード（破ったら差し戻し）

- §1.3 の L-1〜L-6
- 春の Cell には Environment 子を付ける（Commit）。同一 Cell フォルダ内で `Cell_*.unity` と `Environment_*.unity` が別職種の正本
- 夏は `Generated`。編集で密度を揺らさない
- 秋・冬は `HandAuthored`（§1.2）
- 使用してよいのは現行スタック（Cube / Cylinder / Sphere / 共有 Lit、セル 250 m）。メッシュ新規投入はしない
- FW（`unity/Assets/OneStarMaker/`）に Season / 季節 の語を出さない

#### 1.5.3 品質バー（これを満たさない構図は却下）

- overhead が「同じ切手 4 枚 + 隙間」に見えない。3 季節が同じ縦横比なら、ほぼこの失敗である
- 飛行 42 m/s で各季節を横断したとき、その動詞に合う滞在時間になっている（目安: 春は職種作業が目に入る距離、夏は Load/Unload 半径を実際に跨げる距離、秋は到着が 1 拍で分かる距離、冬は差し替え物に近づける距離）
- 春の 1 セルを開いたとき、地形（地面・高低・道）と置き物（木・建物相当）がファイルで分かれている
- 秋に「今来た」と分かる目印が 1 つある（ログを読まなくても Checkout 実演が指せる）
- 冬に「この季節だけ差し替えた」と指せる目印が 1 つある（雪だるまである必要は無い。差し替え可能な 1 個であればよい）
- 現行生成器のモチーフ 4 種（柱 / 筒 / 塔 / 球）を HSV で塗り分けただけ、になっていない

#### 1.5.4 予算

- 最小は §33 どおり 春 2×2 / 夏 4×4 / 秋 2×2 / 冬 2×2
- 最小を超えるセルは **1 セルにつき「場所として必要な理由」を 1 文**。Streaming の証明のための増設は却下
- 軟上限: 春+秋+冬の `HandAuthored` 合計 16 セル（現行南辺の 4 倍）。夏は 4×4 を基本とし、距離が足りないときだけ長辺を足す
- 硬上限: 64 セル構想には戻さない

#### 1.5.5 成果物（コードより先に出す。人が承認してから実装）

1. overhead の図（ASCII か手描き）。矩形 4 つの座標範囲と空隙距離を書き込む
2. 季節ごとに「10 秒で何が見えるか」1 段落。色名の列挙で終わらせない
3. 春の職種分割表: 何が `Cell_*.unity` で何が `Environment_*.unity` か
4. 冬の差し替え目印 1 個と、秋の到着目印 1 個
5. セル数の内訳と、最小を超えたセルの理由 1 文ずつ

#### 1.5.6 渡すプロンプト（ローカル Cursor で Fable を指定して貼る）

S-3 の機構 PR がマージされたあとの **別セッション** で使う。計画や S-3 のコード変更と同一スレッドに投げない。

```
モデルは Fable。実装計画ではなく、SampleGame の世界構図と Level のシルエットを設計してほしい。

読むもの:
- unity/Assets/Docs/Architecture/33-sample-demonstration-boundaries.md
- docs/handoff/SEASON_LEVELS_IMPLEMENTATION.md の §0.4 と §1.3 と §1.5

やってほしいこと:
- §1.5.5 の成果物 5 点をこのチャットに出す
- 座標を Catalog に書き込むのは、人間が図を承認したあと
- 生成器や FW のリファクタはしない

やってほしくないこと:
- 春=桜、秋=紅葉、冬=雪、という四季ポスターの既定解で埋めない
- 2×2 / 4×4 / 2×2 / 2×2 を横一列に空隙 3 セルで置く案は、前の計画が出した最安解なので再提出しない（同じ構図を別座標にずらすのも不可）
- 「最小で足りる」を理由に切手 3 枚へ戻さない。最小は下限であって目標ではない

品質バーは §1.5.3。制約は §1.5.2。予算は §1.5.4。
```

Fable が図を出したら、人が overhead を見て承認 or 差し戻しする。承認後の実装（Catalog 定数・移送・生成器）はローカルセッションでよく、Fable である必要は無い。

### 1.6 本スライス（S-3）でやらないこと

- 季節矩形の本番座標・寸法の確定（S-3C）
- 既存セルの移送（S-3C）
- `Season_*` / Tunnel ノード（S-4 / S-5）。**S-3 のシーン木は `InGameSession → World → Cell` のまま**
- Addressables グループ分割（S-6）、Variant タグ付与（S-7）
- 季節テーマの焼き込み・春の職種別コンテンツ（S-3C / S-8）
- テレメトリ計測・受入判定（S-9。T-07〜T-09 凍結）
- `unityyamlmerge` ドライバ設定（O-5。職種分割の前提条件ではない）
- 初回季節の Ensure 問題（§33 §8 注記。S-4 の論点）

### 1.7 Unity Editor 操作境界（Cloud / ローカル）

| 誰 | やってよい | やってはいけない |
|---|---|---|
| Cloud（本書を書いた環境） | C#・HANDOFF・manifest 宣言・Skills | `unity` バイナリのインストール、生成器実行、テスト実行、Play |
| ローカル実装 | 人間が開いた Editor への `unity status` / `unity command` / `unity eval`。生成器 1 回は既存メニュー | Unity.exe 起動、`pwsh tools/run-tests.ps1`、`unity test`、`unity run`、Addressables ビルド、接続中の YAML 手直し |
| Phase C | `pwsh tools/run-tests.ps1` 全緑。偽 null grep | — |

ローカルで CLI が未導入なら、人間が beta チャネルで入れる（Windows）:

```powershell
$env:UNITY_CLI_CHANNEL='beta'; irm https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.ps1 | iex
```

プロジェクトパスはリポジトリの `unity/`。`unity status` が `ready` になるまでシーンを触らない。繋がらなければ Safe Mode（コンパイルエラー）を先に疑う。YAML フォールバックは書かない。

名前付き Pipeline command（0.4 系の `move_asset` / `open_scene` / `save_scene` / `set_transform` / `menu`）を先に使い、足りないときだけ `eval`。コマンド名は `unity command` でその Editor の公開面を見てから叩く。

`com.unity.pipeline` は `unity/Packages/manifest.json` に `0.4.0-exp.1` を宣言してある。実験パッケージなので、ローカル Editor が解決したバージョンが違うなら `packages-lock.json` を正とする。lock は手で書かない。

スキル: `.cursor/skills/osm-unity-editor/SKILL.md` がこのリポジトリの上書き。公式 `.cursor/skills/unity-cli/SKILL.md` より優先する。

---

## 2. 変更対象ファイル一覧（S-3）

S-3 は「矩形 1 個 = 今の 4×4」でも「矩形 4 個」でも同じコード経路になるようにする。本番は前者。

規模は workflow A-1（現在行数 → 予想行数 / 責務）。予想は「足す量の上限」であり、超えたら §7 で構造レビューする。

| ファイル | 現在 | 予想 | 責務 | 変更 |
|---|---|---|---|---|
| `SampleGame/InGame/InGameSession/Streaming/WorldCellCatalog.cs` | 123 | ~165 | 1（格子定数 + 列挙） | dense 4×4 定数 → **矩形の集合**（`CellRect` リスト + `EnumerateCells`）。本番は `{ origin=(0,0), size=(4,4) }` の 1 要素。`TryGetCoordinate` は集合の membership。`SpawnPosition` / `CornerSpawn` は現行どおり 4×4 の隅 |
| `OneStarMaker/Scripts/Runtime/Streaming/StreamingConfig.cs` | 83 | ~110 | 1（ポリシーパラメータ） | `GridWidth` / `GridHeight` を削除し、展開済み **セル座標集合** を持つ。空集合は例外。**FW は矩形も季節も知らない**（D-1）。列挙は呼び出し側の責務 |
| `OneStarMaker/Scripts/Runtime/Streaming/WorldStreamingController.cs` | 302 | ~310 | 1（desired / retain ポリシー） | dense 二重ループ → `Config.Cells` 走査。**ポリシー（desired / retain / ヒステリシス / in-flight / priority）は不変**（§33 §10） |
| `SampleGame/InGame/InGameSession/Streaming/SessionWorldStreamingDriver.cs` | 209 | ~220 | 1（Catalog → Config 配線） | Catalog の全セル列挙を Config へ渡す。S-4 で「今いる季節の矩形」に絞る布石。S-3 は現行 16 セル全部 |
| `OneStarMaker/Scripts/Editor/Streaming/WorldGridDefinition.cs` + `.asset` | 61 | ~90 | 1（生成器入力 SO） | `_gridWidth` / `_gridHeight` → 矩形リスト。`EnsureGridDefinition` による「const 正本 → アセット写し」の関係は不変 |
| `OneStarMaker/Scripts/Editor/Streaming/WorldCellGenerator.cs` | 602 | ~620 | 1（計画 + 適用） | `ComputePlan` の走査を矩形集合に。分割しない |
| `SampleGame/DependOnAll/Editor/Cells/CellPopulationPlan.cs` | 270 | ~310 | 1（Populate / Skip / 削除の純関数） | `CellGridSpec` を矩形集合へ。範囲外判定・削除可否・keep 判定は同じ計画経由のまま |
| `SampleGame/DependOnAll/Editor/WorldCellStreamingSliceCreator.cs` | 1380 | ~1400 | 足場（削除候補） | **コンパイル追随のみ。** `EnsureGridDefinition` の `_gridWidth` / `_gridHeight` 書き込みと `CellGridSpec(definition.GridWidth, …)` を新 API に合わせる。クラス自身が「構造化しない」と書いてある。**A-2 例外（一度きりの生成スクリプト）。新責務を足さない（A-3）** |
| `OneStarMaker/Tests/Streaming/WorldStreamingControllerTests.cs` | 436 | 追随 + T-B | テスト | 単一矩形入力へ書き換え、既存 10 本相当を残す |
| `OneStarMaker/Tests/Streaming/WorldStreamingControllerMultiFocusTests.cs` | 184 | 追随 | テスト | `StreamingConfig` 生成を集合 API へ |
| `OneStarMaker/Tests/Streaming/StreamingIntegrationTests.cs` | 650 | 追随 | テスト | 同上 |
| `OneStarMaker/Tests/Streaming/CameraStreamingFocusAdapterTests.cs` | 156 | 追随 | テスト | 同上 |
| `OneStarMaker/Tests/Editor/WorldCellGeneratorTests.cs` | 291 | 追随 | テスト | 矩形入力へ |
| `OneStarMaker/Tests/Editor/CellPopulationPlanTests.cs` | 304 | 追随 + T-C | テスト | T-7 / T-11 を矩形集合の範囲外でも同じ保護に |
| 新規テスト（既存ファイルに足してよい） | — | T-A / T-D | テスト | §3 |

S-3 では触らない: `CellAuthoringPolicy`（南辺 4 枚のまま）、`EnvironmentSproutCells` の座標そのもの、`HandEditProbe`、セル `.unity` の中身。これらは S-3C の移送と同時に動かす。`WorldCellStreamingSliceCreator` の sprout 配列の**値**は動かさない。触るのは定義 API への追随だけ。

**FW（`OneStarMaker`）に「季節」の語彙を入れないこと**（型名・identity・コメントとも）。矩形は幾何としてのみ FW に渡る。`CellIdentity` 書式・`SceneState`・asmdef 依存方向は §33 §10 のとおり不変。

### 2.1 API 署名（実装で変えない）

`record` 禁止（`IsExternalInit` が無い）。矩形型は SampleGame Catalog と Editor の `WorldGridDefinition` に置く。Runtime の `StreamingConfig` は矩形を知らない。

```csharp
public readonly struct CellRect
{
    public CellRect(Vector2Int origin, Vector2Int size);
    public Vector2Int Origin { get; }
    public Vector2Int Size { get; } // x = 幅, y = 高さ。どちらも 1 以上
}

// WorldCellCatalog（SampleGame）
public static readonly CellRect[] Rectangles; // 本番: 1 要素 {(0,0),(4,4)}
public static IReadOnlyList<Vector2Int> EnumerateCells();
public static bool TryGetCoordinate(Vector3 worldPosition, out Vector2Int coordinate);
// TryGetCoordinate: Origin / CellSize で floor したあと集合 membership。
// AABB 内でも空隙なら false。

// StreamingConfig（FW）
public StreamingConfig(
    CellGridConfig grid,
    IReadOnlyList<Vector2Int> cells,
    float loadRadius,
    float unloadRadius,
    int maxInFlight);
public IReadOnlyList<Vector2Int> Cells { get; }
// cells が null / 空 → 例外。GridWidth / GridHeight は削除。
```

- 矩形の重なり・サイズ 0 以下は Catalog / `WorldGridDefinition` の検証で例外。重複座標は重なりを禁じた結果として出ない
- `CellGridSpec` は `GridWidth` / `GridHeight` をやめ、矩形集合（または展開済み座標）を取る
- `.asset` のシリアライズ形が変わる。`EnsureGridDefinition` が Catalog 正本から写し直す（手で YAML を書き換えない）。`packages-lock.json` と同様、生成物の差分はローカル Editor が書く

---

## 3. 単体テストの要求（必須）

前提: `OneStarMaker.Tests` は `SampleGame.InGame` を、`OneStarMaker.Tests.Editor` は `SampleGame.DependOnAll.Editor` を参照済み（S-1 で追加）。**着手時に asmdef を開いて再確認すること。**

| # | テスト | 検証内容 |
|---|---|---|
| T-A | **空隙ガード（フィクスチャ）**: 矩形 2 つ以上の合成レイアウトで「異なる矩形の最近セル中心間距離 > UnloadRadius」。違反レイアウトを与えるとテスト側で検出できること | 本番 4×4 単体ではペアが 0 なので、**テスト専用の矩形集合**で書く。S-3C 後に本番 Catalog へ同じ assert を足す |
| T-B | Controller 集合版: (1) 現行相当の 4×4 単一矩形で既存 10 本と同等の desired / retain / priority (2) 空隙を挟んだ 2 矩形の中間に focus を置くと desired が空 | 一般化の挙動保存。既存 10 本は「単一矩形を渡す」形へ書き換えて全部残す |
| T-C | `CellPopulationPlan` 矩形集合版: 空隙セルは Populate に現れない。範囲外 `HandAuthored` は削除不可・`Generated` は削除可（既存 T-6 / T-7 / T-11 の一般化） | 破壊経路 3 の保護が矩形集合でも同じに効く |
| T-D | `WorldCellCatalog.TryGetCoordinate`: 現行 4×4 の四隅で true、外側で false。フィクスチャで空隙座標が false | membership の境界 |

- **`record` を使わないこと**（`IsExternalInit` が無く、プロジェクト全体がコンパイル不能になる）。`readonly struct` か `sealed class`
- テストコードは実装より先に書く。EditMode の個別レッド確認は、ローカルで Editor が開いていれば `unity command eval` で呼んでよい
- **全件ランナー（`unity test` / `pwsh tools/run-tests.ps1`）は Phase C。** 実装者は走らせない。報告は「実装完了。テスト未実行」
- **テスト 0 件は失敗扱い**（コンパイルエラーが 0 件として現れる）

---

## 4. 実装順序

### 4.1 S-3（機構。ローカル。Cloud では走らせない）

1. `develop` からブランチを切る（§33 のブランチがマージ済みであることを先に確認）
2. **破壊経路 3 の再確認から始める**（§33 §12）: `CellPopulationPlanTests` の T-7 / T-11 を読み、矩形集合の範囲外でも同じ保護になることを §3 T-C のテストコードで先に固定する
3. `StreamingConfig` / `WorldStreamingController` を集合走査へ。既存 WSC テストを単一矩形入力に書き換え、T-B を足す
4. `WorldCellCatalog` / `WorldGridDefinition` / `WorldCellGenerator` / `CellPopulationPlan` を矩形集合へ。本番 Catalog は 4×4 の矩形 1 個。`WorldCellStreamingSliceCreator` は `EnsureGridDefinition` と `CellGridSpec` のコンパイル追随のみ
5. 人間がこのプロジェクトの Editor を開く。エージェントは `unity status` で `ready` を確認する
6. 生成器を **1 回**: 既存メニュー `OneStarMaker/Sample/Create World + Cell Streaming Slice`（`WorldCellStreamingSliceCreator.CreateFromMenu`）を `unity command menu` があればそれで、無ければ eval で叩く。現行 16 セルが削除も増設もされないこと（差分は Generated の焼き込み再出力が出ても、HandAuthored 南辺 4 + Environment 4 が `git status` に出なければよい）
7. PR（base: `develop`）→ cursor[bot] → 本書 §7 / §8 のうち S-3 分を埋める。**本書は S-3C が終わるまで `git rm` しない**
8. **`pwsh tools/run-tests.ps1` は実装者が走らせない。** Phase C が Unity を閉じてから全緑を取る

### 4.2 S-3C（構図。Fable 設計 → 人の承認 → ローカル実装）

1. §1.5.6 のプロンプトで Fable セッションを切る
2. 成果物 5 点を人が overhead で見る。切手一列なら差し戻し
3. 承認後、Catalog に矩形 4 つを書く
4. **移送 SOP（人間が Editor を開いた状態。YAML / 単独 `git mv` を正本にしない）:**
   1. `unity status` が `ready`
   2. フォルダ移動は `move_asset`（`.meta` GUID 同梱）。コマンド名は `unity command` で確認
   3. identity / 親子 / Addressables address は既存の `SerializedObject` 経路（`ConfigureSceneResource` と同型）。eval で足りる
   4. `AuthoredRoot` のワールド座標 Δ は `set_transform`（無ければ eval で `Transform`）。シーンは `save_scene`
   5. policy / sprout 配列を追随
5. `HandEditProbe.StampHandEdits`（移送前 or 移送直後の生存確認ができるタイミングで）
6. 生成器を §4.1 と同じメニュー経路で実行 → 旧 Generated 12 の削除と新セルの生成 → もう一度実行して HandAuthored 側差分 0
7. `VerifyHandEdits`、Phase C でテスト全緑、Editor Play で空隙に desired が空であること
8. §33 の D-6 / §5 表 / §7 座標 / O-1 / O-2 を harvest。`pwsh tools/docs-audit.ps1`。本書を `git rm`

---

## 5. 受入条件

### 5.1 S-3

| # | 条件 | 判定方法 |
|---|---|---|
| A-1 | 全テスト緑 | Phase C が `pwsh tools/run-tests.ps1`。failed 0。**テスト 0 件は失敗扱い。実装者は走らせない** |
| A-2 | 本番セルが動いていない | 南辺 4 Cell + Environment 4 の identity / フォルダが S-3 前後で同じ |
| A-3 | 複数矩形はテストでしか存在しない | 本番 Catalog の矩形数 = 1。T-A / T-B の 2 矩形はテストフィクスチャ |
| A-4 | FW に季節の語彙が漏れていない | `unity/Assets/OneStarMaker/` を Season / 季節で grep → 0 件（D-1） |
| A-5 | 既存 Streaming テストが残っている | WSC 10 本相当が単一矩形入力で緑 |

### 5.2 S-3C（Fable 成果物 + 実装）

| # | 条件 | 判定方法 |
|---|---|---|
| C-1 | §1.5.5 の 5 点がチャットにあり、人が overhead を承認している | 承認コメントが残っている。初版の横一列切手は不在 |
| C-2 | 空隙条件が本番 Catalog に対するテストで緑 | T-A を本番矩形に対しても走らせている |
| C-3 | 移送した手編集が消えない | stamp が生成器 2 回のあと 8 / 8 生存（Environment 本数が春の寸法で増えるなら、その分まで含めて生存） |
| C-4 | 旧 Generated 12 枚がフォルダ・SceneGraph とも消えている | Explorer / grep |
| C-5 | Editor Play で季節 A から空隙へ出ると desired が空 | 例外 0。隣の季節が load 半径に入らない |
| C-6 | 品質バー | §1.5.3 を人の目視。自動テスト化しない |

偽 null チェック（`?.` / `??` / `is null` / `ReferenceEquals`）の grep をレビュー時に行うこと（S-1 の教訓。破棄済み `UnityEngine.Object` は `?.` / `??` が短絡しない）。

---

## 6.0 Phase B からの設計指摘

（未記入）

---

## 6. Phase C からの差し戻し

（未記入）

---

## 7. Phase C レビュー

（未記入）

---

## 8. Phase C' 監査

（未記入）
