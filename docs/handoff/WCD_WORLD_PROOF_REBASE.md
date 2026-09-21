# WCD — 世界証明の Content Directory 載せ替え

## 0. メタデータ

- type: slice
- status: B（A3 凍結。文書実装中）
- branch: `cursor/wcd-world-proof-a0-c6d7`
- implementation base commit: `23b1098e6b9c1d9b04543d2cdabfe3f0a82cffcb`
- implementation head commit: （B 完了時に記入）
- risk: high（世界の証明表、後続スライス所有、Addressables 残面、build identity に触れる）
- owner: WCD 主担当。A0 / A1 / A3 は本セッション。A2 は独立 subagent
- created: 2026-09-21
- expires: 本スライス Phase D。2026-10-21 に未マージなら再確認
- harvest to: `docs/handoff/SEASON_WORLD_DESIGN.md`、`docs/handoff/S-4_FULL_SPEC_WORLD_AUTHORING.md`、偽と確定した公開面だけ `docs/GOALS_AND_STRENGTHS.md` / Architecture §13 / §18 / §20 §9 / `docs/README.md`。未実装の新契約を公開面へ移さない
- Phase A snapshot: 本ファイルの A3 凍結節。生成は A3 commit
- Phase B result / evidence / C' blind bundle: 未作成

BuildSystem program と DIST / RET の完了 HANDOFF は復活させない。入力は現行公開 Architecture と残っている世界計画、RET 後続、本パケットだけとする。

## 1. 目的と対象外

### 目的

RET 後の通常入口（Content Directory build、DIST Delivery、directory Play、BS4 Player）の上で、SampleGame が昔から主張していた「部分的な作業空間」を、死んだ Addressables 語彙のまま残さない。

このスライスは計画スライスである。A0 は入力と意思決定境界を固定する。A1 以降で証明表の書き換え文面と、実装が要るかの判定を書く。A0 は最安の合法解を埋めない。

### このスライスが答える問い

RET 後も、Addressables グループとリモートカタログなしで、「1 季節だけリビルド」「手元に無い季節は取得済み content か明示失敗」を主張できるか。

答えは次のいずれかである。A0 は選ばない。

1. 現行の contentSet / revision / 単一 directory 契約で主張できる。世界計画の文面だけを直す。
2. 現行契約では主張できない。W-5 / W-6 を所有未定の後続へ降格し、S-4c からその前提を外す。
3. 現行契約では主張できない。新しい季節 partition の実装スライスを切る。そのスライスは本スライスの後続であり、本スライスの B に実装を入れない。

### 対象外

- Addressables package と serialized `AssetReference` の全廃
- WorldCompanion の Addressables 登録と flake の置換
- `AddressableAssetSettings` の Active Player DataBuilder を Unity 既定へ戻す mutation
- `TryLoadRemoteCatalogAsync` の削除、メニュー / CLI 試験の拡張
- 参照 0 の `CompleteContentDirectoryPlayStopAsync` の削除
- S-4c Lighting / RenderEnvironment の実装
- S-5 Tunnel、S-8 演奏レイヤ、S-9 計測
- 配信運用拡張（signing、latest channel、CDN、delta/resume）
- 非 Scene Description / Mesh
- InGame のゲームルール投入
- Analyzer 化
- 新しい Content Directory partition backend、公開 API、asmdef edge（A1 が「現行契約では問いに答えられない」と判定するまで）
- 完了済み HANDOFF の復元、`BUILD_SYSTEM_REBUILD_PROGRAM.md` / `DIST_CONTENT_DELIVERY.md` の復活

### 現況（2026-09-21、base `23b1098`）

BuildSystem 本線は閉じた。U66 → CD0 → BS1 → BS2a/b/c → BS3 → BS4 → DIST → RET は develop に入っている。作業台の program 正本は削除済み。通常入口は Content Directory と DIST である。

成立している契約（公開面）:

- 選択済み content は固定 target `StandaloneWindows64-Player` の単一 Content Directory。`BuildContentRoot` が logical key・表現・loadable ID を持つ。複数表現は同一 directory に入れられる。BuildTag ごとに directory を作る契約はない。
- SampleGame の Content build は季節・表現を選んだあと、UICommon と SceneResourceMap を同じ directory に合成する。Framework は季節語を知らない。
- Runtime は source を再走査しない。directory session が登録・native・unregister と OS read lease を所有する。通常 Play 停止は同期 `CompletePlayStop`（`StopAndDrain` は待たない）。明示 close だけが `StopAndDrain`。
- DIST は manifest digest を pin した revision 一式を install する。partial は active にしない。sourceFiles の Missing / Changed は編集案内であり、取得済み content からの起動を止めない。
- 未指定 `runtimeMode` の Editor Play は Delivery pair が無ければ失敗する。明示 `addressables` だけが互換口。未知 mode と片側 pair は Addressables へ戻らない。
- `com.unity.addressables` 2.11.2 は残す。残存 owner は `AddressableBackend`、serialized `AssetReference`、WorldCompanion、Player の `DoNotBuildWithPlayer`。package 削除は後続。

世界計画はまだ旧語彙のままである:

- `SEASON_WORLD_DESIGN.md` の W-5 は「1 季節リビルドで他 3 季節バンドルのハッシュ不変」。W-6 は「ローカル欠落季節がリモート解決 or 明示失敗」。スライス表の S-6 は「1 変奏 = 1 Addressables グループ」、S-7 は「1 変奏 = 1 Variant タグ + 未チェックアウト経路」。
- `S-4_FULL_SPEC_WORLD_AUTHORING.md` は S-6 / S-7 を対象外に残し、順序は `S-4d -> S-5` のあと旧 S-6 / S-7 を想定している。
- `§20` 後続は「部分 Checkout 開発の再燃は未所有」と書いて、世界計画の S-7 と所有が食い違う。

公開面にも RET 後に偽になった、または旧段落が残った箇所がある:

- `docs/GOALS_AND_STRENGTHS.md` は部分的作業空間の対応物を「Variant / Checkout、Hybrid Play」とし、アセットを「Addressables 経由を正」と書く。
- Architecture §13 に「既定の Addressables backend は残る」という旧段落が残る。通常 Play は directory。
- Architecture §18 はチェックアウト厳選をリモート Addressables カタログ付きの実装済み workflow として残している。

RET 発見レビューが後続へ送った残件（指摘 1 の lease 逆転は `98f376b` で修正済み）:

1. Active Player DataBuilder がまだ `VariantFilteringBuildScript`。通常 Play / BS4 は通らない。Unity Packed Mode を踏むと警告のあと本体が走り、pending JSON があれば groups を `SaveAssets` まで戻す。
2. `TryLoadRemoteCatalogAsync` は呼び出し切断済みの死コード。
3. `CompleteContentDirectoryPlayStopAsync` は参照 0。削除理由にしない。
4. `RetiredAddressablesMenuNoOpTests` はメニュー 4 つだけ。

### 要求

- 本スライスは A0 を今固定する。BuildSystem 関係の問いは、世界の証明表が新経路と食い違う限り続く。
- OpenAI ChatGPT クレジットは枯渇している。A2 は ChatGPT を前提にしない。モデル名を将来の固定割り当てとして書かない。
- 計画セッションに谷の構図や季節 layout を確定させない。世界の主題・座標・216 Cell は `SEASON_WORLD_DESIGN.md` のまま読む。
- Phase D のマージは人間の明示まで行わない。

### 制約（本文へ転記）

- 依存は Game → Framework の一方向。Framework に季節語を出さない。
- アセット寿命は `IAssetManagement` と `AssetOwner`。新公開 API は A1 が必要と書くまで足さない。
- `SceneState` 14 値は減らさず並べ替えない。
- 公開ログ抽象は `ILogger<T>`。
- Unity 側 C# は `record` 禁止、先頭 `#nullable enable`、破棄されうる `UnityEngine.Object` に `?.` / `??` / `is null` / `ReferenceEquals` を使わない。
- 参照 0 を削除理由にしない。
- 公開面は今この瞬間に真であること。未実装の季節 partition を実装済みとして harvest しない。
- 既存 Content Directory の既定は選択済み content の単一 directory である。BuildTag / 季節名ごとに directory を増やす契約は、この A0 が作った既定ではない。
- 旧 Addressables checkout / Hybrid / remote catalog は通常入口ではない。残ファイルを参照 0 だけで削除しない。
- Unity テストと Content Directory build は Phase C。A0 は実行しない。

### 未決事項（A0 は選ばない。A1 採否は §2）

| # | 論点 | 閉じる loc |
|---|---|---|
| U1 | W-5 を現行 contentSet / revision の隔離として読み替えるか、新 partition を要るか、所有未定へ降格するか | A1。最安解を先に埋めない |
| U2 | W-6 の「手元に無い季節」を DIST の別 revision install と明示失敗に読み替えるか、部分 Checkout 再燃として未所有のまま残すか | A1。`§20` 後続の「未所有」と世界計画 S-7 の食い違いをここで解消するか、所有だけ移す |
| U3 | 偽になった公開面（GOALS の Addressables 正、§13 旧段落、§18 のリモートカタログ実装済み）を本スライスで harvest するか、文面確定後の Phase D に限るか | A1。A0 は列挙だけする |
| U4 | 1 の答えが「実装が要る」になったとき、後続スライス名と開始条件 | A1。本スライス B に実装を入れない |
| U5 | DataBuilder 残置を ADDR-HYGIENE にまとめるか、package 全廃スライスに折り込むか | A1。本スライス B で mutation しない |

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: §1。一つの問いであり、Lighting や package 全廃と束ねない。
- A1 の答え: **現行の contentSet / revision / 単一 directory 契約で主張できる。世界計画と偽になった公開面の文面を直す。新しい partition backend は切らない。W-5 / W-6 は降格しない。**

### A1 採否（U1〜U5）

根拠は現行メニューと公開契約であり、旧 Addressables グループの形を残すための最安解ではない。

**U1 = 案 1（読み替え）。案 2（降格）と案 3（新 partition）は不採用。**

- 現行入口は `all-full` / `spring-full` / `spring-whitebox` / `spring-full-whitebox`。公開先は build identity ごとの directory。同じ target/contentSet の workspace は再利用するが、成功成果物は identity 別に残る。
- 「1 季節だけリビルド」は `Build Spring Full` で既にできる。その build は `all-full` の公開 directory を出力先にしない。
- 捨てる旧主張: 1 つの Addressables カタログ内で 4 季節グループのハッシュが独立する。共有 Lit / Primitive / Tunnel を Common グループへ逃がす。これは現行 CD に無い。同じ意味を backend で再現するのは新 partition であり、今の問いの成立に不要。
- 降格しない理由: 部分的作業空間は GOALS の 5 問の 1 つで、RET 後に対応物が空になると Framework の主張が死ぬ。現行契約で言い直せる。
- 送る問い（S-5）: session は process あたり directory 1 つ（`ContentRevisionGate`「already registered in this process」）。`spring-full` だけでは夏へ遷移できない。S-5 が 1 Player で四季を持つなら `all-full` 相当を起動条件にするか、複数登録 / partition をその A0 で切る。WCD は先回りしない。
- 他季節の単独 Content build メニュー（Summer Full 等）は無い。選択 policy は四季を扱える。メニュー追加は SampleGame 入口の後続であり、CD-SEASON-PARTITION ではない。本スライスの完了条件にしない。

**U2 = DIST 読み替え。部分 Checkout 再燃は未所有のまま。**

- 手元に無い季節の source は `sourceFiles` の Missing / Changed。Framework は VCS checkout しない。
- 実行は、その季節を含む検証済み revision があるときだけ。リモート Addressables カタログで欠損を埋めない。
- 含まれない季節への遷移は明示失敗。失敗の出し方は S-5 が所有する。
- S-7 実装スライスは廃止する。W-6 の文面と DIST が担う。`§20` の「部分 Checkout 開発の再燃は未所有」は維持する。sparse checkout + リモート補完は W-6 ではない。

**U3 = 本スライス B で、下表の偽主張だけ harvest する。**

今すでに偽で、この問いの「今真」に必要なものに限る。S-5 用の未決、§33 構図、未実装 partition は触らない。

**U4 = 要実装ではない。CD-SEASON-PARTITION は切らない。**

S-5 が「1 session のまま季節単位で独立 rebuild した成果物を差し込む」を要求したとき、その A0 の入力にする。WCD の後続としては予約しない。

**U5 = ADDR-HYGIENE に分離。ADDR-RETIRE と混ぜない。**

RET は「旧 BuildSystem 廃止」と「package 全廃」を分けた。DataBuilder 残置は Unity Packed Mode の警告経路で、`AssetReference` / WorldCompanion の残存 owner とは別の変更理由。ADDR-HYGIENE は DataBuilder index、`TryLoadRemoteCatalogAsync` 死コード、メニュー / CLI / 反射試験。`CompleteContentDirectoryPlayStopAsync` は削除しない。本スライス B で mutation しない。

### 進める最低条件

A0 の M1〜M5 を維持する。別の完了バーを足さない。

### 受け入れ条件（M1〜M5 の観測可能な詳細）

**M1** — `SEASON_WORLD_DESIGN.md` を次の文面へ置換する（意味を変えて言い換えない）。

- W-5 単独ビルド: 1 つの contentSet は 1 回の選択である。現行入口は All Seasons Full / Spring Full / Spring Whitebox / Spring Full And Whitebox。その contentSet の再 build は、別 contentSet または別 build identity の公開 directory を書き換えない。同一 contentSet の成功成果物は identity ごとの公開先に残る。DIST は同じ revision の内容差し替えを拒否する。共有 Lit / Primitive / Tunnel は選択に含まれればその directory に入り、Addressables の Common グループとしては分けない。同一 directory 内の季節グループ単位ハッシュ不変は現行契約にない。
- W-6 行の名前は「取得済み content からの実行」にする。DIST が検証した installed revision だけを登録する。sourceFiles の Missing / Changed は編集可否の案内であり、リモート Addressables カタログから欠損を埋めて Play しない。その revision に含まれない季節への遷移は明示失敗とする（所有は S-5）。Framework は VCS checkout を代行しない。部分 Checkout + リモート補完は未所有。
- スライス表の S-6 / S-7 は廃止し、「WCD。現行 Content build と DIST が担う」と書く。順序は `S-4c → S-4d → S-5 → S-8a → S-9 → S-8b〜d`。
- 検証マトリクスの実現手段列から Addressables グループ / リモートカタログを通常手段として残さない。
- 構図、座標、216 Cell、品質バー、S-5 トンネル契約の本文は変えない。

**M2** — `S-4_FULL_SPEC_WORLD_AUTHORING.md`:

- 対象外の「S-6 の 1 季節 1 Addressables group」「S-7 の季節別 checkout / remote catalog」を、「W-5 / W-6 は WCD が文面を所有。S-6 / S-7 実装スライスは廃止。季節 partition と部分 Checkout 再燃は対象外」へ置換する。
- 順序 `S-4a -> S-4b -> S-4c -> S-4d -> S-5` のあとに S-6 / S-7 を必須工程として残さない。
- S-4c の公開 API・検証方針に Addressables グループ再編を足さない。Lighting / VFX の本文は変えない。

**M3** — Architecture `20-variant-checkout-workflow.md` §9:

- ADDR-HYGIENE を名前付き後続にする。対象は Active Player DataBuilder の `VariantFilteringBuildScript` 残置、`TryLoadRemoteCatalogAsync` 死コード、retired メニュー / CLI / batch / DataBuilder / settings 反射試験。
- `CompleteContentDirectoryPlayStopAsync` は参照 0 でも削除しないと書く。
- ADDR-RETIRE は package / serialized `AssetReference` / WorldCompanion / `DoNotBuildWithPlayer` のまま。HYGIENE と混ぜないと書く。
- 「部分 Checkout 開発の再燃は未所有」を残す。世界計画 S-7 との食い違いは「S-7 廃止」で解消する。

**M4** — 本スライス B が直す公開面（これ以外の harvest を B で増やさない）:

| 箇所 | 今の偽 | 置換 |
|---|---|---|
| `docs/GOALS_AND_STRENGTHS.md` §1 行 4 | Variant / Checkout、Hybrid Play | Content Directory の選択 build、DIST の取得済み revision、どの Scene からでも Play。旧 Hybrid / リモートカタログは通常入口ではない |
| 同 2.1 箇条 | アセットは Addressables 経由を正 | 通常経路は Content Directory。Addressables は互換 backend |
| 同 2.4 | 「なぜ Addressables を正とするか」を現行の決定として並べる | 旧通常経路は Addressables。再評価して通常を Content Directory にした、と現況にする。手動 DI と WorldPartition の却下記録は残す |
| Architecture §13「既定の Addressables backend は残る」 | 通常 Play が directory なのに既定と読める | 通常 Play は directory。Addressables backend は明示互換口として残る |
| Architecture §18 第二用途段落 | リモートカタログ workflow が実装済みの通常開発手順 | 第二用途の意図（Variant を手元範囲のタグにする）は残す。実行の通常入口は DIST の installed revision。リモート Addressables ストリーミングは旧経路。詳細は §20 |

`unity/Assets/README.md` の AssetManagement / Content Directory 節は RET 後の現況と一致している。B で触らない。未実装の季節 partition をどの公開面にも実装済みと書かない。

**M5** — 本節の U1〜U5 採否が A3 で凍結される。A2 は同じこの版を見る。

### ここでは答えない問いと所有する後続

- ADDR-RETIRE: package 全廃。残存 owner は §20 冒頭。
- ADDR-HYGIENE: U5。本スライスのあと、S-4c と並行して切ってよい。S-4c の blocker ではない。
- 部分 Checkout 再燃: 未所有。
- 他季節の単独 Content build メニュー: SampleGame 入口の後続。Framework 契約ではない。
- 複数 directory 登録 / 季節 partition: S-5 の A0 が必要と書いたときだけ。WCD は切らない。
- S-4c Lighting / RenderEnvironment: 本スライス GO のあと着手してよい。
- S-5 / S-8 / S-9、配信運用拡張、非 Scene Description: 既存世界計画と §20 後続。

### 判定定義

A3 凍結後、M1〜M5 の文面が作業台と指定公開面に入ったら GO。Unity コード差分が無いことは NO-GO ではない。CONDITIONAL ACCEPT は使わない。

### 停止規則

進める最低条件を満たし、現在の問いに致命的な反証がなければ終了する。最低条件未達のまま終了しない。新しい不確実性は後続へ送る。

- A3 後の例外承認: なし
- 本文へ転記した実装制約: §1 制約。新公開 API / 所有者 / 寿命 / asmdef は不要。
- 未決事項: A3 で凍結。S-5 へ送った問いと未所有の再燃は後続。

### A2 結果と A3 採否

同じ A1 版 `f364764`。互いの指摘は渡していない。ChatGPT は使っていない。

| 担当 | モデル | 観点 | 判定 |
|---|---|---|---|
| A2-1 | Claude Sonnet 5 | architecture-gates | BLOCKER 0。A1 OK |
| A2-2 | Gemini 3.8 Flash | 世界計画 / S-5 漏れ | BLOCKER 0。漏れは封じ込め |
| A2-3 | Claude Opus | A0 だけの代替 | 1+2 ハイブリッドを提案。§2 を読み越したため **独立性制約あり**。設計解としては不採用、文言制約だけ採用 |

採否:

- U1〜U5 は A1 のまま凍結する。W-5 / W-6 全体の降格はしない。partition スライスは切らない。
- A2-1 LATER: W-6 の表から D-5（明示失敗と旧季節復帰）を無言で消さない。**採用。** M1 の W-6 に「出し方と旧季節復帰の所有は S-5（D-5）」を残す。
- A2-3 の「満たせない半分を所有未定へ降格」: 旧グループハッシュ不変と delta を W-5 の成立条件にしない、は A1 既定。所有未定の新項目は作らず、§20 の DIST 後続（delta/resume）と S-5 に既にある。**欠陥としては不採用。** B は「旧主張と等価ではない」を表に残す。
- A2-3 汚染: 代替レビューは A1 を見ていない保証ができない。A2-1 / A2-2 が独立に BLOCKER 0 なので凍結は進める。

A3 凍結担当: Cursor Grok 4.6 / 本セッション。人間が目的・範囲を変えていない。ユーザーは Phase C まで承認なし進行を明示した。

## 3. 責務マップ

すべて文書。新しい型、asmdef、Scene / Prefab は置かない。変更理由は一つ（世界証明と公開現況を RET 後の通常入口へ揃える）。ファイルを分ける理由は層（作業台 program / 公開目標 / 公開 Architecture）であり、独立した設計判断を増やさない。

| ファイル | 現在行 | 予想増分 | 責務 | 所有者・寿命 | 公開面 | テスト境界 | 分割 |
|---|---|---|---|---|---|---|---|
| `docs/handoff/WCD_WORLD_PROOF_REBASE.md` | 本パケット | A2/A3 記録 +40 以内 | スライス境界と採否 | 本スライス。Phase D で削除 | 作業台 | なし | 非分割。計画の正本 |
| `docs/handoff/SEASON_WORLD_DESIGN.md` | 342 | +40 / −30 目安。50% 未満 | 世界の証明表とスライス順。構図は触らない | 世界 program。S-4d 以降も残る | 作業台 | grep: W-5/W-6 節に「Addressables グループ」「リモートカタログ」を通常手段として残さない | 非分割。同じ証明表 |
| `docs/handoff/S-4_FULL_SPEC_WORLD_AUTHORING.md` | 386 | ±20 | S-4 対象外と順序だけ。Lighting/VFX は触らない | S-4 program | 作業台 | S-6/S-7 を必須工程として残していない | 非分割 |
| `docs/README.md` | 66 | ±5 | 作業台表の WCD / S-4c 行 | 公開方針 | 公開 | 正本 5 件 | 非分割 |
| `docs/GOALS_AND_STRENGTHS.md` | 126 | ±15 | 5 問の現況対応物 | 公開目標 | 公開 | 行 4 と 2.1 / 2.4 が directory 現況 | 非分割。目標宣言の 3 箇所だけ |
| `unity/Assets/Docs/Architecture/13-resource-system.md` | 532 | ±8 | CD 段落の「既定 backend」誤読 | 公開 Architecture | 公開 | 当該文が「通常 Play は directory」 | 非分割。1 文 |
| `unity/Assets/Docs/Architecture/18-asset-description.md` | 323 | ±20 | 第二用途を旧経路へ直す | 公開 Architecture | 公開 | 「実装済みの通常手順」としてリモートカタログを残さない | 非分割。1 段落 |
| `unity/Assets/Docs/Architecture/20-variant-checkout-workflow.md` | 259 | ±25 | §9 の後続所有 | 公開 Architecture | 公開 | ADDR-HYGIENE / ADDR-RETIRE / 未所有再燃が分かれている | 非分割 |

500 行を超える既存 §13 は、今回 +8 と 1 責務なので分割しない。3 責務混在は、層が違うファイルへ既に分かれている。

## 4. 実装計画

1. A2 がこの A1 版だけを見る。ChatGPT は使わない。
2. A3 で採否を凍結する。覆すなら Phase A revision。B に実装を入れない。
3. B は §3 のファイルだけを直す。禁止語の機械 grep と `docs-audit` まで。Unity Editor / テスト / Content build は実行しない。
4. 発見 C / 判定 C は文書差分。コードが無ければ全 EditMode 回帰は適用除外。

順序: 世界計画（M1）→ S-4 対象外（M2）→ §20 §9（M3）→ 公開 harvest（M4）→ README 表。

Phase B から Phase A へ差し戻す条件: 新しい公開 API、所有者、寿命、asmdef、季節 partition、Addressables 設定 mutation、S-5 トンネル仕様の確定、Summer Full メニュー追加が「問いに答えるため必要」になったとき。

対象外を維持する方法: B の diff に `.cs` / `.asset` / `.unity` を含めない。S-4c 本文と谷の構図を編集しない。

## 5. テストとレビュー計画

- 単体テスト: なし。中核は文面契約であり、新しい実行ロジックが無い。
- 差し戻し中の起点 filter: なし。文書 grep。
- 判定必須: `pwsh tools/docs-audit.ps1`。次の禁止を作業台の W-5/W-6 節と GOALS の当該箇所から落とす: 通常手段としての「Addressables グループ」「リモートカタログから Play」。
- 全 EditMode 回帰の適用除外: 本スライス B が Unity コードを変えない場合。理由は実行経路が変わらないこと。代替証拠は docs-audit と禁止語 grep、diff に `.cs` が無いこと。B がコードを触ったら除外を破棄し、空 filter の全 EditMode を判定必須に戻す。
- 統合・Unity テスト: なし。
- 機械検査: `docs-audit`。コードを触ったら `contract-audit` も必須。
- A0 / A1 主担当・モデル・ベンダー: Cursor Grok 4.6 / 本セッション。OpenAI 系列は使っていない。
- A2: 同じこの版。観点 1 は architecture-gates（新 API / partition / Game→FW / 層の越境）。観点 2 は世界計画と S-5 への漏れ（W-5 が 1 session 四季を黙って要求していないか）。ChatGPT は使わない。担当モデルは A2 開始時に選ぶ。
- A3: 主担当が採否。人間の目的変更が無ければ program 委任内で凍結してよい。
- C' 予約: 人間。AI にする場合は A2 / B / C 未使用の系列。A2 で全系統を使い切らない。
- 独立性の強化条件: C' を人間にしたのでモデル相違は適用しない。A2 は B より前なので、B/C のモデルは未定のまま残す。

## 6. Phase B 実装結果

- 実装: A3 凍結どおり、§3 の文書だけを置換した。`.cs` / `.asset` / `.unity` は無し。
- HANDOFF との差: §18 目的節の「Addressables カタログ構成で完結」は第二用途の直後にあり、置換後も偽になるため同じ M4 対象の矛盾として直した。B 適応。新 harvest 対象は増やしていない。
- 未実行: 公式 `pwsh tools/docs-audit.ps1`（この環境に pwsh が無い）。C で python stand-in と禁止語 grep を行う。
- implementation head commit: （この B commit）
- Phase B 担当・モデル・ベンダー: Cursor Grok 4.6 / 本セッション

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手
