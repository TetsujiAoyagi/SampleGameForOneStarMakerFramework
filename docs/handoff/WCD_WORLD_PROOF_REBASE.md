# WCD — 世界証明の Content Directory 載せ替え

## 0. メタデータ

- type: slice
- status: A（A0 固定。A1 未着手）
- branch: `cursor/wcd-world-proof-a0-c6d7`
- implementation base commit: `23b1098e6b9c1d9b04543d2cdabfe3f0a82cffcb`
- implementation head commit: （未到達。A0 は計画パケットのみ）
- risk: high（世界の証明表、後続スライス所有、Addressables 残面、build identity に触れる）
- owner: WCD 主担当。A0 は本セッション
- created: 2026-09-21
- expires: 本スライス Phase D。2026-10-21 に未マージなら再確認
- harvest to: `docs/handoff/SEASON_WORLD_DESIGN.md`、`docs/handoff/S-4_FULL_SPEC_WORLD_AUTHORING.md`、偽と確定した公開面だけ `docs/GOALS_AND_STRENGTHS.md` / Architecture §13 / §18 / §20 §9 / `docs/README.md`。未実装の新契約を公開面へ移さない
- Phase A snapshot: 未作成（A3 凍結時）
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

### 未決事項（A0 は選ばない）

| # | 論点 | 閉じる loc |
|---|---|---|
| U1 | W-5 を現行 contentSet / revision の隔離として読み替えるか、新 partition を要るか、所有未定へ降格するか | A1。最安解を先に埋めない |
| U2 | W-6 の「手元に無い季節」を DIST の別 revision install と明示失敗に読み替えるか、部分 Checkout 再燃として未所有のまま残すか | A1。`§20` 後続の「未所有」と世界計画 S-7 の食い違いをここで解消するか、所有だけ移す |
| U3 | 偽になった公開面（GOALS の Addressables 正、§13 旧段落、§18 のリモートカタログ実装済み）を本スライスで harvest するか、文面確定後の Phase D に限るか | A1。A0 は列挙だけする |
| U4 | 1 の答えが「実装が要る」になったとき、後続スライス名と開始条件 | A1。本スライス B に実装を入れない |
| U5 | DataBuilder 残置を ADDR-HYGIENE にまとめるか、package 全廃スライスに折り込むか | A1。本スライス B で mutation しない |

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: 上記 §1。一つの問いであり、Lighting や package 全廃と束ねない。
- 進める最低条件:
  - M1: W-5 / W-6 / S-6 / S-7 が、現行 Content Directory + DIST の言葉で書き換えられるか、所有未定の後続へ明示降格される。Addressables グループ / リモートカタログを通常入口として残さない。
  - M2: S-4c の着手条件から Addressables グループ前提を外す。S-4c に本スライスの残件、package 全廃、季節 partition 実装を混ぜない。
  - M3: RET 後続 1（DataBuilder 残置）の owner を名前付き後続へ移す。2（死コード）と 4（試験範囲）も同じ後続か、別の名前付き後続へ移す。3 は削除しない。
  - M4: RET 後にすでに偽である公開面の主張を列挙し、本スライス harvest か後続かを分ける。偽のまま「今真」として残さない判断を A1 が書く。
  - M5: A1 が U1〜U5 を採否する。A0 の競合する読みを、根拠なしに 1 つへ畳まない。
- 受け入れ条件: A1 が M1〜M5 の観測可能な詳細を書く。A0 は別の完了バーを作らない。
- ここでは答えない問いと所有する後続:
  - Addressables package 全廃 → `§20` 後続（仮称 ADDR-RETIRE）。残存 owner は冒頭の表。
  - DataBuilder 残置、死コード、メニュー試験 → 仮称 ADDR-HYGIENE。A1 が ADDR-RETIRE へ折るか分ける。
  - 新しい季節 partition の実装 → A1 が U1=要実装と判定したときだけ切る。仮称 CD-SEASON-PARTITION。本スライスでは切らない。
  - 部分 Checkout 開発の再燃 → `§20` は未所有のまま。A1 が W-6 を DIST 読み替えにしたら、再燃は別問として未所有を維持する。
  - S-4c Lighting / RenderEnvironment → 既存 `S-4_FULL_SPEC_WORLD_AUTHORING.md`。本スライス GO のあと着手してよい。
  - S-5 / S-8 / S-9、配信運用拡張、非 Scene Description → 既存世界計画と `§20` 後続。
- 判定定義: A3 凍結後の本スライスは、M1〜M5 を世界計画と作業台表へ書いたら GO。実装未着手は NO-GO ではない。U1 が要実装でも、後続スライスを切って本スライスを終われる。CONDITIONAL ACCEPT は使わない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ終了する。最低条件未達のまま終了しない。新しい不確実性は後続へ送る。
- A3 後の例外承認: なし
- 本文へ転記した実装制約: §1 制約。
- 未決事項: §1 の U1〜U5。

## 3. 責務マップ

A1 で書く。A0 はファイル配置を決めない。

想定する変更対象は作業台と、A1 が harvest すると書いた公開面だけである。新しい Runtime / Editor 型、asmdef、Scene / Prefab は A0 の対象にしない。

## 4. 実装計画

A1 で書く。A0 の順序だけ固定する。

1. A1 が M1〜M5 の詳細、U1〜U5 の採否案、変更ファイル一覧、テスト方針を書く。
2. A2 は同じ A1 版を独立に見る。少なくとも 1 件は architecture-gates。ChatGPT は使わない。
3. A3 で採否を凍結する。U1=要実装なら後続スライス名だけを本文へ残し、B に実装を入れない。
4. B は凍結した世界計画の文面と、A3 が本スライス harvest とした公開面だけを直す。
5. 発見 C / 判定 C は文書差分と `docs-audit`。Unity コードが無いなら全 EditMode 回帰は適用除外し、理由と代替証拠を A1 が書く。

Phase B から Phase A へ差し戻す条件: 新しい公開 API、所有者、寿命、asmdef、季節 partition、Addressables 設定 mutation が必要になったとき。

## 5. テストとレビュー計画

- 単体テスト: A1。文書スライスならコードテストを必須にしない。
- 差し戻し中の起点 filter: A1。
- 判定必須テスト: A1。コード差分が無い場合の全 EditMode 適用除外は、理由と `docs-audit` を代替証拠として A1 が書く。
- 機械検査: `pwsh tools/docs-audit.ps1`。コードを触ったら `pwsh tools/contract-audit.ps1` も必須。
- A0 主担当・モデル・ベンダー: Cursor Grok 4.6 / 本セッション。OpenAI 系列は使っていない。
- A2 / A3 / C / C': 未割当。A2 は ChatGPT を前提にしない。C' 用の未関与担当は A1 で予約し、A2 で全系統を使い切らない。

## 6. Phase B 実装結果

未着手

## 7. Phase C

未着手

## 8. Phase C'

未着手

## 9. Phase D

未着手
