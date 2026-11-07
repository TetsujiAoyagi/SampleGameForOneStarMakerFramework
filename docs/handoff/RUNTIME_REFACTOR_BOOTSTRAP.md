# AbstractApplicationInitializer の責務分解（旧リモートカタログ・UICommon・常駐サービス）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。

- type: `slice`
- status: `C`（PR #166 のレビュー 1 巡目の阻害指摘（I4 / I4a、hook の中の `ReleaseAll` で戻り値が set から外れる）を B 適応で直し、implementation head が変わった（§6「PR レビュー 1 巡目の修正」）。旧 head `33e0f27f` の判定 C と C' 1 巡目は無効で、新 head で判定 C からやり直す。判定 C（2 回目、owner の例外で縮めた形）は完了、阻害する指摘なし（§7「判定 C（2 回目）」）。次は PR の再レビュー。以下は旧 head の経緯: `C' 待ち`（判定 C は完了、阻害する指摘なし。AC4 の `BuildContentDirectoryIntegrationTests`（360 秒の Timeout、head に起因せず `develop` でも再現）は owner の例外承認で本スライスの合否から外した（§7「判定 C の完了と owner の例外」）。C' 1 巡目の blind bundle はローカル固定済み。directory Play は GO 候補 head で取得済み。発見 C 1 回目は阻害する指摘なし。B と同じモデルで行ったので「C は B と異なるモデル」を満たさず、owner が `独立性制約あり` として受け入れた（§7「独立性」）。Phase B 完了、動作確認済み（directory Play は `未実施`。§6「動作確認」）。2026-11-07 に owner が凍結を指示し、同日に文書 PR のマージを待たずに Phase B を始めた（下の「Phase B 開始の記録」））
- **A1 改訂の記録（2026-11-07）:** program の A0 [ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md) §4.2 の BOOTSTRAP 向けの表を当てた。§6 D4（owner 判断）に従い、S2（Content Directory 起動の facade）と、それだけに属する AC9 / AC10・U1 / U2・責務マップの行・実装手順 1 を独立スライス `CONTENT_DIRECTORY_STARTUP` へ移し、素材は A0 §4.2 の §3.2 / §2.1 S2 / §2.3 の行を当てた形で [RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md](RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md) に置いた（正本は同書だけ）。本書は S3〜S5 と、それに伴う `InitializeAfterSceneLoad` の分割だけを扱う。番号は A0 や他文書からの参照を保つため振り直さず、移した S2・S6・AC9・AC10・U1・U2 は欠番にした。分割の判断は §2.6
- branch: A1 初稿は計画ブランチ `claude/runtime-refactor-stage0-plan`（PR #161 で `develop` へマージ済み）。A1 改訂は `claude/runtime-refactor-bootstrap-a1-d230c4`。実装は `claude/runtime-refactor-bootstrap`（A3 凍結版 `6844bf35` から切った。下の「Phase B 開始の記録」）
- implementation base commit: `9abd2ee6`
- **base の取り直し（2026-11-08）:** PR の作業中に `develop` の `9abd2ee6` を本ブランチへマージした（`feaf8343`）。マージは本スライスのコードを変えていない（`git diff 33e0f27f feaf8343 -- unity/` と `git diff 6844bf35 9abd2ee6 -- unity/` が同じ）。新 head の判定 C・C' の実装差分は base `9abd2ee6` で固定する。旧 head `33e0f27f` の記録（§6〜§9 の旧い節）は base `6844bf35` のまま
- **Phase B 開始の記録（owner の決定、2026-11-07）:** 文書 PR のマージを待たずに Phase B を始める。A3 凍結版 `6844bf35` から専用ブランチを切り、implementation base を `6844bf35` に固定する。`develop` の head `7c9b364d` とのコードの差は 0 で（`7c9b364d..6844bf35` は Phase A の文書 4 file だけ）、base を `develop` にすると実装差分に HANDOFF の変更まで入るため。`d4861882` とも `unity/Assets/OneStarMaker/` と `unity/Assets/SampleGame/` の差が 0 であることを B 開始時に確かめたので、§1 と §3.5 の行番号・公開面は取り直していない（`AbstractApplicationInitializer.cs` は 1,303 行で一致）。判定 C・C' の実装差分は base `6844bf35` で固定する
- implementation base の経緯: A1 時点の値は `d4861882`（A1 初稿は `840b6609`（PR #149 のマージ）で測り、2026-11-07 に再固定した。間に initializer を変えたのは BS5a（#150）と BS5b-2（#156）で、§1 と §3.5 は `d4861882` の実測に取り直してある）。A1 改訂の時点（2026-11-07）の `develop` の head `7c9b364d` は、`unity/Assets/OneStarMaker/` と `unity/Assets/SampleGame/` で `d4861882` との差が 0 なので、取り直していない。A3 は「B 開始時に `develop` の head で再固定する」としていたが、上の owner の決定で `6844bf35` に固定した。順序: 本スライスは A0 §6 D4 の列（ASSET_CYCLE → `BUILDSYSTEM_STANDALONE_ENTRY` → `CONTENT_DIRECTORY_STARTUP` → asmdef）の外にあり、先行条件を持たない（ASSET_CYCLE は Bootstrap folder を触らず namespace も変えない）。ただし `CONTENT_DIRECTORY_STARTUP` と同じ `AbstractApplicationInitializer.cs` を触るので、両者の Phase B を同時に走らせない。後に B へ入る側が base を取り直す
- implementation head commit: `e8eca726`
- risk: `high`（アプリ起動・終了の順序、失敗経路、Player の receipt に現れる stage 名、派生クラスの公開面に触れる。実行時挙動は変えないが、変えていないことの証明が主な作業になる）
- owner: root（依頼者）。A2 / B / C / C' の担当は Phase ごとに選ぶ
- created: 2026-10-31
- expires: 2026-11-30 または置換 revision の成立時。期限時は owner が更新・廃止を判断する
- harvest to: `unity/Assets/Docs/Architecture/04-app-startup.md` §4.2（クラス構造）と §4.3（解放の保証）。§4.7「Content Directory の起動時選択」の契約文は変えない（実装の置き場所だけが変わる）。新しい Architecture 文書は作らない
- 関連スライス: [RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md](RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md)（本書から切り出した S2。順序は上の implementation base の行）。`RUNTIME_REFACTOR_ASSET_CYCLE.md`（PR #165 で完了・削除） は先行条件ではない（namespace を据え置くので、本書が前提にする `using` の変化は無い）。S-4 の d5a は `CreateVfxBackend` hook と `VfxRuntimeHost` 配線を足した。残っていた d5b は PR #157 で完了し、initializer を触らなかった（§2.5 U4）
- Phase A snapshot の取得案内・状態: A2 入力版は commit `7b2cf205` の本書（§5「A2 独立レビュー」に content sha256）。A3 凍結版は commit `6844bf35` の本書。本スライスは文書だけで、凍結する script・機械向け期待値を持たないので、Artifact Storage の snapshot は作らない（凍結の正は commit）。A3 途中版を外部レビューへ渡した経路は owner の手元で、取得案内は無い
- Phase B result snapshot の取得案内・状態: 未生成（作らない。B の結果は implementation head と §6 で固定する。未 commit の変更は無い）
- evidence bundle の取得案内・状態: 新 head の判定 bundle は未生成。以下は旧 head `33e0f27f` のもの: 発見 C 1 回目の bundle はローカル固定済み（未昇格。判定 C が使い終えた。発見 C の所見を含むので C' には渡さない）。判定 C 用に別の bundle は作らず、判定 C の生証拠（全件回帰・単独 2 回の XML と log、directory Play の Console 記録、機械検査の生出力）は C' 用 blind bundle と同じ payload にある（判定 C の所見は含まれない）
- C' blind bundle の取得案内・状態: 新 head 用は未生成。旧 head `33e0f27f` 用は C' 1 巡目で使用済み: ローカル固定済み（未昇格、`evidence stage`、task `runtime-refactor-bootstrap`、base / head は上の欄、収録は 18 file）。取得案内は主担当の作業領域（git 管理外の evidence-guides）にあり、C' の起動指示で渡す。同じ PC で `fetch` の往復と README の閲覧を確認済み。所見・発見 C の bundle は含まない
- 引継ぎ票:
  - **判定 C（2 回目）完了（2026-11-08、新 head `e8eca726`）:** 阻害する指摘なし。全 EditMode 回帰は 1,655 件が全て Passed（`BuildContentDirectoryIntegrationTests` も Passed のため AC4 の例外は使わなかった）。結果と owner の AC4 例外の適用決定は §7「判定 C（2 回目）」。**次の Phase・役割: 次の作業は PR #166 の再レビュー（owner と cursor[bot]）。** 阻害指摘が無ければ Phase D。C' はやり直さない（下の owner の例外）。引き継ぐ commit: この判定 C 完了の記録 commit（SHA は起動指示で渡す）。implementation head は上の欄の `e8eca726` のまま。未解消の `未実施`: 新 head の directory Play は owner の例外で取り直さない（旧 head の観測を使う）。
  - **owner の例外（2026-11-08、この修正の後）:** skill の「判定後の head 変更」は新 head で判定 C と C' をやり直すとするが、owner は修正差分の小ささ（`unity/` の 2 file、コードは BeforeSceneLoad の代入 4 か所の後の呼び出しと private メソッド 1 つ）を理由に、次の形に縮めることを承認した。Phase A は再開しない。
    - **判定 C（2 回目）は縮めて行う:** (1) 全 EditMode 回帰（空 filter、`-WithGraphics`、StandaloneWindows64）を新 head で 1 回。XML で新規 11 件（`BeforeSceneLoadServiceStagingTests` 5、`FrameworkServiceSetTests` 2、`LegacyRemoteCatalogProbeTests` 4）が Passed、旧 head の Passed 1,652 件が新 head でも Passed であることを確かめる。(2) 修正差分（`git diff 33e0f27f <新 head> -- unity/`）を I4 / I4a と §6「PR レビュー 1 巡目の修正」の主張に照らして読む。AC1 などの機械検査の取り直し、AC1 の本文照合、directory Play、判定 bundle と C' 用 blind bundle の作成は行わない（公開面と通常の起動経路の値を変えない差分なので、旧 head の記録を使う）。
    - **C' はやり直さない。** 代わりに PR のレビュー（owner と cursor[bot]）が修正差分をもう一度見る。
    - **directory Play は取り直さない。** 修正は hook の中で `ReleaseAll` が走ったときだけ効き、通常の起動経路の値を変えないので、旧 head `33e0f27f` の観測（§7「判定 C（1 回目）」）を使う。
    - 下の新票の「次の作業」のうち、上と食い違う部分（機械検査・AC1・directory Play・bundle・C' 2 巡目）はこの例外で置き換える。判定 C の後は、PR の再レビューに阻害指摘が無ければ Phase D。
    - AC4 の例外を新 head に適用するか: 決定済み（2026-11-08、owner）。適用する。ただし判定 C（2 回目）で Passed になったので実際には使っていない（§7）。
  - **更新（PR レビュー 1 巡目の修正、2026-11-08）:** PR #166 のレビューの阻害指摘（I4 / I4a）を B 適応で直した（§6「PR レビュー 1 巡目の修正」）。implementation head が変わったので、旧 head `33e0f27f` の判定 C・C' 1 巡目・directory Play は新 head の合格証拠にしない（skill の「判定後の head 変更」）。**次の Phase・役割: 判定 C（2 回目）。** 新しい session で、Claude Opus 5.5（B と、この修正）以外のモデルで始める。
  - 次の作業: GO 候補 head を implementation head（上の欄）とし、ローカルの軽量 tag `evidence/runtime-refactor-bootstrap/2` で固定する（`/1` は旧 head のまま残す）。base は `9abd2ee6`。先に修正差分（`33e0f27f` → 新 head の `unity/`）の構造・I4 / I4a を見る。§5 の判定必須を新 head で実行する: 全 EditMode 回帰（空 filter、`-WithGraphics`、StandaloneWindows64 の Editor）。XML で新規 11 件（`BeforeSceneLoadServiceStagingTests` 5、`FrameworkServiceSetTests` 2、`LegacyRemoteCatalogProbeTests` 4）と §5 の既存テストの Passed を確かめる。AC1 の本文照合、機械検査（AC1 / 2 / 5 / 6 / 7 / 8 / 17、contract-audit）を base `9abd2ee6` と新 head で取り直す。directory Play を新 head で取得する（手順と観測項目は §7「判定 C（1 回目）」の動作確認と §5）。判定 bundle と C' 用 blind bundle を同じ head から作る。結果を §7 に書き、本票を次の担当（C' 2 巡目、差し戻しなら B）へ書き換えて commit する。
  - AC4 の例外: owner の例外承認（§7「判定 C の完了と owner の例外」）は元は head `33e0f27f` に限った。owner は 2026-11-08 に新 head `e8eca726` にも適用すると決めた（範囲・理由・移送先は同節と同じ。`BuildContentDirectoryIntegrationTests` の Timeout による Failed を AC4 の合否に使わない）。判定 C（2 回目）では同テストが Passed だったので、結果として未使用。
  - 次の C'（判定 C の後）: 2 巡目。前巡の C' の指摘（B-1、AC4）への owner の裁定（§9 の「例外承認を維持する」）を C' 用決定記録として渡し、前巡の head から新 head への修正差分とその影響経路を中心に監査させる。B・C と異なるモデルで、強化条件のため可能なら Anthropic 以外（Grok、cursor-agent）を使う。
  - 引き継ぐ commit（新票）: この票を含む commit（owner の例外の記録 commit。SHA は起動指示で渡す）。implementation head は上の欄の `e8eca726` で、この記録 commit とは別。
  - 未解消の `未実施`（新票）: 新 head の directory Play（判定 C が取得する）。
  - **旧票（判定 C 完了、2026-11-08。旧 head `33e0f27f`。上の新票で置き換えた）:** owner が AC4 の `BuildContentDirectoryIntegrationTests` の Failed を本スライスの合否に使わない例外を承認した（§7「判定 C の完了と owner の例外」）ので、判定 C は完了した。AC4 以外の判定必須は GO 候補 head `33e0f27f`（tag `evidence/runtime-refactor-bootstrap/1`）で揃っている。**次の Phase・役割: C'（1 巡目、blind audit）。** 新しい session で、Claude Opus 5.5（B・発見 C）と Claude Sonnet 5.5（判定 C）以外のモデルで始める（Google 系は無い。未関与が尽きれば Grok で `独立性制約あり`）。C' には blind bundle の取得案内だけを渡し、§7（発見 C・判定 C の所見）と発見 C の bundle は渡さない。C' の結果が揃ったら Phase D（人間または D 担当が判定 C と C' を突き合わせる）。差し戻しなら B へ。head は動かさない（O2 は現 head のまま）。
  - 次の Phase・役割（旧票。判定 C は完了）: 判定 C。新しい session で始める（Claude Opus 5.5 は B と発見 C で使ったので使わない。発見 C は owner の判断で `独立性制約あり` として受け入れ済み。§7「独立性」）。
  - 次の作業（旧票）: GO 候補 head を implementation head（上の欄）とし（B の 6 commit は §4 の手順ごとの単位なので履歴の整理は不要と判断した）、ローカルの軽量 tag `evidence/runtime-refactor-bootstrap/1` で固定する。§5 の判定必須を実行する: 全 EditMode 回帰（空 filter、**`-WithGraphics`**、StandaloneWindows64 の Editor。§7 O5）。XML をクラス名で確かめる（§7 O4: `BuildContentDirectoryIntegrationTests` は `OneStarMaker.Tests.Editor.Build`、`unity/Assets/SampleGame/Tests/Bootstrap/` のテストは `OneStarMaker.Tests.Bootstrap`）。AC1 の §3.5 の各メンバーの本文での見比べを §7 に記録する（必須）。§6 の `未実施`（directory Play）を取得する。手元の install は 2026-09 のもので現 head と対応しないので、先に GO 候補 head で `Tools/OSM/Content/Verify Play/<宣言名>` の Content Directory build → publish → install を行ってから Play する（観測項目は §5）。§7 O2 を base と一致させるか（1 行の B 適応）を判断する。直すなら head が変わるので B へ戻す。判定 bundle と C' 用 blind bundle を同じ head から作る。結果を §7 に書き、本票を次の担当（C'、差し戻しなら B）へ書き換えて commit する。
  - 引き継ぐ commit（旧票）: 判定 C 完了の記録 commit（SHA は起動指示で渡す）。implementation head は上の欄の値で、この記録 commit とは別。
  - 必要な権限（C' は読み取り中心で Unity の再実行は必須ではない。以下は判定 C の旧票）: ローカルの Unity Editor と `pwsh tools/run-tests.ps1`（Editor を閉じてから、sandbox 外の承認済み経路で起動）。directory Play には Content Directory の build → publish → install（`Tools/OSM/Content/Verify Play/<宣言名>`）。Evidence の stage は事前承認の範囲。資格情報は不要。
  - 未解消の `未実施`（旧票）: なし（§6「動作確認」の Editor directory Play は判定 C が GO 候補 head で取得した。§7「判定 C」）。AC4 の `BuildContentDirectoryIntegrationTests` のタイムアウトは `未実施` ではなく Failed で、owner の例外承認で合否から外した。
  - 停止規則: `CONTENT_DIRECTORY_STARTUP` の Phase B と同時に走らせない。判定必須または `未実施` の証拠が揃わないうちは判定 C を完了と記録せず、C' を起動しない（判定 C は完了済みで、C' を起動してよい）。差し戻しが決まったら最終判定用の検証一式を続けない。
  - 引き渡さないもの: A2 レビュー担当の生の回答（主担当の作業領域にだけあり、採否は §5 の表が正）。C' には §7（発見 C・判定 C の所見）と発見 C の bundle を渡さない。

### PR 状態ブロック

```text
状態: 判定 C（2 回目、縮めた形）完了、阻害する指摘なし。全 EditMode 1,655 件 Passed。PR の再レビュー待ち（C' はやり直さない）
Phase A: revision 0、A2 入力版 commit 7b2cf205、A3 凍結版 commit 6844bf35
implementation base / head: 9abd2ee6 / <修正 commit。PR 本文の状態ブロックに SHA を書く>
引き継ぐ commit: <修正 commit。implementation head と同じ>
次の担当: PR の再レビュー（owner、cursor[bot]）。阻害指摘が無ければ Phase D
HANDOFF: docs/handoff/RUNTIME_REFACTOR_BOOTSTRAP.md
Evidence 取得案内: 新 head の判定 bundle・C' 用 blind bundle は未生成（取得はローカル担当）
未決事項: なし（AC4 の例外は新 head にも適用と owner が決定。今回は Passed で未使用）
```

## 1. 目的と対象外

- 目的: `Runtime/Bootstrap/AbstractApplicationInitializer.cs`（1,303 行、1 クラス）から、起動契約（3 フェーズの順序、失敗境界、派生クラス向け hook）と変更理由の異なる 3 つの責務（旧リモートカタログの案内経路、UICommon の取得、Framework 常駐サービスの集合）をそれぞれのクラスへ移し、最長メソッド `InitializeAfterSceneLoad` を順序が読める長さに分ける。移した結果として、Bootstrap folder から DebugSocketServices の internal 型への参照を 0 にする。Content Directory の起動は initializer に残し、独立スライス `CONTENT_DIRECTORY_STARTUP` が facade へ移す。
- 目的の改訂（A1 改訂、2026-11-07）:
  - 元の文言: 「`Runtime/Bootstrap/AbstractApplicationInitializer.cs`（1,303 行、1 クラス）から、3 フェーズの起動順序と派生クラス向け hook の template だけを残し、変更理由の異なる 4 つの責務（Content Directory の起動、旧リモートカタログの案内経路、UICommon の取得、Framework 常駐サービスの集合）をそれぞれのクラスへ移す。移した結果として、Bootstrap folder から BuildContent / Config / DebugSocketServices の internal 型への参照を 0 にし、Content Directory の起動は BuildContent 側の public な 1 型だけを通る形にする。これは Bootstrap を独立 assembly にする後続スライスの前提である。」
  - 変更理由: program A0 §6 D4 で、Content Directory 起動（旧 S2）は AssetManagement / BuildSystem の単独利用 program の列（`BUILDSYSTEM_STANDALONE_ENTRY` の後）に入り、独立スライスになった。残る 3 責務は単独利用の成立に関係しないので、その列の外で進める。Config の internal（`RequiredJsonFileConfigProvider`）は `BuildConfig` に残り、facade と一緒に扱うかを `CONTENT_DIRECTORY_STARTUP` の U2 で決めるので、本書の目的から外した。「Bootstrap を独立 assembly にする前提」は、BuildContent の越境を `CONTENT_DIRECTORY_STARTUP` が片付けるまで成り立たないので、本書の目的には書かない。
  - 承認: owner の D4 判断（2026-11-07、program A0 §6）。利用者の入力は増やしていない（本スライスに利用者の操作は無い）。
- 利用者: なし（Framework 内部の構造変更。派生クラスの作者から見た公開面は変えない）。
- 対象外（所有する後続）:
  - Content Directory 起動（構成の解決、Addressables の封じ、session の登録・install・close、Play 停止時の完了、`BuildConfig` 内の `PlayerBakedContentConfiguration` 生成）と、BuildContent の internal 型・`AssetManagement` の directory 用 internal メソッド 4 つへの参照の解消: [RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md](RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md)。本スライスはこれらのコードの意味を変えず、initializer の外へ出さない（AC5）。`InitializeAfterSceneLoad` の分割で private メソッドへ移るものは、同じファイルの中に留まる
  - protected / internal の公開面（hook、accessor、nested 型、static 入口）の追加・削除・signature 変更: 本スライスでは一切行わない。必要なら Phase A を再開する
  - 起動順序・終了順序・失敗経路・stage 名の変更。性能改善。旧リモートカタログ経路の削除（通常起動から外れているが、`20-variant-checkout-workflow.md` が案内として残している）
  - namespace `OneStarMaker.Runtime` → `OneStarMaker.Runtime.Bootstrap` への移動（`AppInitializer.cs` の `using` が変わるので SampleGame の変更になる）: Bootstrap を独立 assembly にするスライス
  - `Runtime/Config/RequiredJsonFileConfigProvider`（internal）の public 化や移動、`BuildConfig` の変更: 触らない。facade へ移すかは `CONTENT_DIRECTORY_STARTUP` の U2、assembly の扱いは asmdef スライスで決める
  - `Runtime/Rendering/Vfx/Implements/VfxRuntimeHost`（internal）と `ActiveVfxRuntimeHost`: S-4d program の所有。本スライスは呼び出し位置を `FrameworkServiceSet` の中へ動かすだけで、型・可視性・生成と破棄の順序を変えない。Bootstrap → Rendering の internal 参照 1 件は残り、asmdef Stage 2 の入力として記録する
  - `UseRequiredPlayerFileConfiguration`（BS4 Player）の protected-key 契約、fail-closed の条件、receipt の schema: 変えない
  - SampleGame の製品コード（`AppInitializer.cs` を含む）: 変更 0
  - `AbstractApplicationInitializer` の XML doc 冒頭の使用例と `04-app-startup.md` §4.5 の実装ルール: 変えない
- 現況（base `d4861882` の実測）:
  - 1,303 行、19 フィールド、3 つの protected static 入口（`BootstrapSubsystemRegistration` / `BootstrapBeforeSceneLoad` / `BootstrapAfterSceneLoad`）、6 つの protected accessor、23 の protected virtual / abstract hook、protected nested 型 2 つ（`PlayerContentStage`、`PlayerContentFixtureResult`）、protected static 1 つ（`AppendFixtureStages`）、internal static 4 つ（`ShouldNotifyPlayerContentFailure`、`RunsPlayerContentSmoke`、`ReadEditorFirstScene`、`ActiveVfxRuntimeHost`。中 2 つは BS5b-2 で追加）。一覧は §3.5。
  - 最長メソッド `InitializeAfterSceneLoad` は 219 行（294〜512 行目）。Framework 全体で最長。
  - **他 folder の internal 型への参照（Runtime 内で Bootstrap だけが持つ）:** BuildContent 9 型（`ContentDirectorySession`、`UnityContentDirectoryBackend`、`PlayerContentConfiguration`、`PlayerBakedContentConfiguration`、`ContentDeliveryFiles`、`ContentInstallReceipt`、`InstalledRevisionVerifier`、`VerifiedInstalledRevision`、`ContentDeclarationMismatch`（630・635 行目。A1 初稿の 8 型から漏れていたのを A2 で訂正））、Config 1 型（`RequiredJsonFileConfigProvider`）、DebugSocketServices 1 型（`DebugSocketTelemetrySink`）、Rendering 1 型（`VfxRuntimeHost`）。加えて `AssetManagement` の internal メソッド 4 つ（`SealAddressables`（246 行目）、`InstallContentDirectory`（605）、`CloseContentDirectoryAsync`（421、463）、`CompleteContentDirectoryPlayStop`（981））を、`_assetManagement` を具象 `AssetManagement.AssetManagement` へ確かめる 5 箇所（240、419、456、603、977 行目）を経て呼ぶ。`AssetManagement` のコンストラクタは public（235 行目）。
  - **変更理由の混在。** 同じクラスに (a) 3 フェーズの順序と hook（起動契約）、(b) `AppConfig` から Content Directory 構成を解く判定（1079〜1151 行目、`ReadContentDirectoryConfiguration` + nested `ContentDirectoryConfiguration`）、directory と決まった直後の Addressables の封じ（240〜249 行目、BS5a）、session の登録・close と宣言の不一致警告（594〜636 行目、`InstallConfiguredContentDirectoryAsync` + `WarnOnDeclarationMismatch`、BS5b-2）、435〜480 行目付近の失敗時 cleanup、ReleaseAll の 2 分岐）、(c) 旧リモート Addressables catalog の追加ロードと revision 乖離警告（673〜822 行目、5 メソッド。`TryLoadRemoteCatalogAsync` に呼び出しは無く、`InitializeAfterSceneLoad` 内のコメント「旧リモート Addressables catalog 追加ロードは通常起動から外した」が残る）、(d) UICommon Scene / prefab の取得 3 経路と EventSystem の生成（514〜592 行目）、(e) logger / DebugSocket / UpdateSystemHost / VfxRuntimeHost の生成・開始・破棄順（886〜924 行目、941〜1009 行目）が入っている。
  - **隠れた結合。** `SampleGame/Tests/Bootstrap/InvalidCompanionConfigStartupTests.cs` は reflection で private フィールド `_assetManagement` を差し替える（149 行目）。`SampleGame/Tests/Editor/Build/BuildContentDirectoryIntegrationTests.cs` は reflection で `_contentDirectorySession` と `_sceneDirector` を読む（449・451 行目）。`_sceneDirector` は VFX の Play テスト群（`VfxScenePlayTests`、`LocalVfxEventPlayTests.Scenes`、`SeasonMajorEventPlayTests.Scenes`）も読む（A1 初稿は `_assetManagement` だけとしていたのを A2 で訂正）。`OneStarMaker/Tests/Editor/TestObservation/ObservationReloadTests.cs` は `GetMethod("GetEnvironmentVariablePrefix")` で protected hook を読む（I1 で守られる）。`OneStarMaker/Tests/BuildContent/PlayerContentConfigurationTests.cs` は `ShouldNotifyPlayerContentFailure` と nested `PlayerContentStage` / `AppendFixtureStages` を派生 harness から使う。S-4d5a が足した SampleGame の Play テスト群は `ActiveVfxRuntimeHost` を読む。
  - **観測可能な契約。** AfterSceneLoad の stage 名は 16 個で、失敗時に `OnAfterSceneLoadInitializationFailed(stage, ex)` と `OnPlayerContentStartupFailedAsync(stage, …)` に渡り、BS4 Player の receipt に残る: `before-scene-load`、`register-content-directory`、`load-ui-common`、`wait-ui-common-ready`、`load-scene-resource-map`、`start-framework-services`、`initialize-app-services`、`create-scene-factory`、`resolve-scene-variant`、`create-scene-director`、`register-loaded-scenes`、`load-player-first-scene`、`run-player-content-smoke`、`unload-player-first-scene`、`close-player-content-directory`、`complete-player-content-smoke`。このほかに BS5b-2 が足した動的な stage `"load-editor-first-scene:" + firstScene`（Editor directory Play の初回 Scene。Player の receipt には現れない）が 1 つある。
  - **BeforeSceneLoad の逐次代入。** 常駐サービスは 1 つ作るたびにフィールドへ代入される（253・261・263・267 行目）。そのため (1) 後ろの hook（`CreateDebugSocketService`、`CreateLoggerFactory`、`CreateVfxBackend`、`RegisterDefaultTelemetrySink`）は前で作ったものを protected accessor（`UpdateCoordinator`、`LoggerFactory`、`Vfx`）で読める。(2) base の `RegisterDefaultTelemetrySink`（1275〜1282 行目）は `_loggerFactory` が null なら Warning を出して JSON sink を登録しない。(3) 途中の hook が投げると、`BootstrapBeforeSceneLoad` の catch の `ReleaseAll` が作り済みの UpdateSystemHost（ctor で PlayerLoop に install 済み）と DebugSocket を破棄する。
  - **AfterSceneLoad の早期 return と冒頭の検査。** 337 行目（`_assetManagement` か `_cts` が null）と 384 行目（`_sceneDirector` が null）は catch を通らずに return し、finally が失敗の telemetry（その時点の stage）を出す。`ReleaseAll` は呼ばない。冒頭の `_cts == null` は例外を投げて catch に入る（306〜307 行目）。
  - **stage の代入点。** `InitializeAfterSceneLoad` の `startupStage` への代入は 18 箇所（初期値 296 行目を含む。313、317、321、326、343、347、350、353、360、368、390、400、408、411、415、418、424 行目）。`load-ui-common` は初期値と 317 行目の 2 箇所、`register-content-directory` は非 required の先行登録（313）と required ではない後段の登録（360）の 2 箇所。`before-scene-load` は AfterSceneLoad の代入ではなく、`BootstrapAfterSceneLoad` が BeforeSceneLoad の失敗を Player へ通知するときの stage（201 行目）。AC2 の literal 16 個は、この 1 個と AfterSceneLoad の 15 個（動的な接頭辞を除く）。
  - **UICommon の所有（526〜592 行目）。** 3 経路で所有が違う。(1) active Scene が `UICommon` で root に UICommon がある経路は **借用**: そのまま返し、`_uiCommonObject` にも `_uiSceneHandle` にも入れず、`EnsureEventSystem` も呼ばない。(2) directory、(3) Addressables の経路は、scene handle を `_uiSceneHandle` に、root を `_uiCommonObject` に入れ（component の検査より前）、最後に `EnsureEventSystem` を呼ぶ。EventSystem は `EventSystem.current` が無く Play 中のときだけ作り、そのときだけ `_eventSystemObject` に入れる。`ReleaseAll` は `AssetManagement.ReleaseAll` の後で `_uiCommonObject` を `Destroy` し、`_uiSceneHandle` は参照を null にするだけ（`ISceneHandle` は `IDisposable` ではなく、scene の unload は AM の ReleaseAll に任せる）。
  - **`UseRequiredPlayerFileConfiguration` の評価時点。** protected virtual の getter で、UICommon の取得では active Scene の検査の後に初めて読まれる（545 行目）。
  - **現在の順序（不変条件の基準）。** BeforeSceneLoad: CTS 生成 → `Application.quitting` 購読 → `AssetManagement` 生成 → `BuildConfig`（required Player なら `PlayerBakedContentConfiguration` もここで生成） → `ReadContentDirectoryConfiguration` → （directory なら）`SealAddressables` と封じたことの `Debug.Log` → `CreateUpdateSystemHost` → `AppTelemetry.Thresholds` → `CreateDebugSocketService` → `LogDebugSocketBootstrapStatus` → `CreateLoggerFactory` → `VfxRuntimeHost.Create` → `ActiveVfxRuntimeHost` の設定 → `RegisterDefaultTelemetrySink`。AfterSceneLoad: （directory かつ非 required なら）session 登録 → UICommon 取得 → `WaitForPanelReadyAsync` → SceneResourceMap（Player / directory / Addressables の 3 経路） → `StartFrameworkServicesAsync` → `OnServicesInitializing` → `CreateSceneFactory` → `ResolveSceneVariant` → （directory かつ未登録なら）session 登録 → representation の上書き → `SceneDirector` 生成 → `BindSceneDirector` → `BindSceneLifecycle`（VFX） → `RegisterAlreadyLoadedScenes` → （firstScene があれば）required Player でないなら Editor の初回 Scene を `AddScene` して観測ログ 1 行、required Player なら Player content smoke の 6 段。session 登録の直後に宣言の不一致警告（`WarnOnDeclarationMismatch`）。ReleaseAll: `_cts.Cancel` → `DisposeVfx` → DebugSocket → UpdateSystemHost → LoggerFactory → CTS → `_bakedPlayerContent` / `_config` / `_contentDirectoryConfiguration` → `SceneDirector.Dispose` → `AssetManagement.ReleaseAll`（+ `CompleteContentDirectoryPlayStop` または `BeginSynchronousShutdown`） → map → UICommon GO → `_uiSceneHandle` → EventSystem GO → `AppTelemetry.Shutdown`。
  - 失敗経路: AfterSceneLoad の catch は、required Player で first scene がロード済みなら unload（Editor は ReleaseAll と Play の終了に任せる） → directory が登録済みなら close → `OnAfterSceneLoadInitializationFailed` → （required Player なら）`OnPlayerContentStartupFailedAsync` → `ReleaseAll` の順で、各段の例外は記録して元の例外を隠さない。`OperationCanceledException` は非 required のときだけ成功扱い。
  - 本書の範囲との対応: 上の (b) と、BuildContent 9 型・Config 1 型・`AssetManagement` の internal メソッド 4 つへの参照は `CONTENT_DIRECTORY_STARTUP` の範囲で、本スライスでは initializer に残る。本スライスが移すのは (c)〜(e) と DebugSocketServices 1 型への参照。Rendering 1 型（`VfxRuntimeHost`）への参照は `FrameworkServiceSet` へ移るが、Bootstrap folder の中に残る。
  - 設計文書: `04-app-startup.md` §4.2 のクラス構造図は現在のメソッド構成を前提にしている。§4.7 には「Editor 設定読取の未決」（`BuildConfig` が directory 判定より前に `JsonFileConfigProvider` で Addressables を読む）が記録されており、本スライスはこの未決を解決も正当化もしない。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: `AbstractApplicationInitializer` の起動・終了順序と派生クラス向け公開面を変えずに、旧リモートカタログ・UICommon 取得・Framework 常駐サービスを変更理由ごとのクラスへ分け、`InitializeAfterSceneLoad` を分割し、Bootstrap から DebugSocketServices の internal 型への参照を 0 にできるか。
- 判定定義: §2.1 の不変条件と成立条件を §2.2 の受け入れ条件で全て確認できれば GO。一つでも未達なら NO-GO。スパイクではないので CONDITIONAL ACCEPT は使わない。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし（A3 後に作る場合は本書と別の記録にする）

### 2.1 進める最低条件

**不変条件（起きてはいけないこと）**

- **I1 公開面不変:** §3.5 の一覧にある protected / internal メンバーの名前、signature、アクセス修飾子、nested の位置、`static` の有無を変えない。追加もしない。private フィールド `_assetManagement`、`_contentDirectorySession`、`_sceneDirector` の名前と型を変えない（SampleGame.Tests の reflection。§1）。
- **I2 起動順序不変:** §1 の「現在の順序」にある各処理の相対順序を変えない。stage 名 16 個の文字列、動的 stage の接頭辞 `load-editor-first-scene:`、各処理に対応する stage の割当を変えない。
- **I3 終了順序不変:** `ReleaseAll` の解放順（§1）を変えない。複数回呼んでも安全であることを保つ。`ActiveVfxRuntimeHost` は、自分の host と同じときだけ（`ReferenceEquals`）、host の Dispose より前に解除する。破棄の途中で例外が出たときの扱い（現行は try/catch が無く、例外は `ReleaseAll` の外へ出る）も変えず、移動先で try/catch を足さない。UICommon の借用経路の GameObject・EventSystem は破棄せず、scene handle の unload も足さない（§1「UICommon の所有」）。
- **I4 失敗経路不変:** 各失敗点で呼ばれる hook と、その前に行う cleanup の順序、例外の握り方（元の例外を隠さない、cleanup の例外は記録だけ）を変えない。`ContentDirectoryException` の `Code` とメッセージを変えない。`OperationCanceledException` の扱いを変えない。BeforeSceneLoad の途中失敗では、それまでに作った常駐サービスが全て `ReleaseAll` で破棄される。AfterSceneLoad の失敗時に hook・receipt・telemetry へ渡す stage は、失敗した処理に割り当てられた stage のまま（§1 の 16 個の割当）。337・384 行目の早期 return（catch を通らず、失敗 telemetry だけを出す）を残す。
- **I4a hook から見える状態不変:** BeforeSceneLoad で各 `Create*` の戻り値は、次の hook を呼ぶより前に protected accessor から見える（§1「BeforeSceneLoad の逐次代入」）。UICommon の取得で読む virtual（`UseRequiredPlayerFileConfiguration`、`GetDirectoryBootstrapSceneLogicalKey`、`GetUICommonPrefabAddress`）は現行と同じ時点・同じ分岐でだけ評価される。`UseRequiredPlayerFileConfiguration` は active Scene の検査の後、2 つの hook はロード済み UICommon の経路と required Player の拒否の経路では呼ばれない。getter が純粋であることには頼らない（派生クラスの実装を本スライスは知らない）。
- **I5 越境を広げない:** 変更後の `Runtime/Bootstrap/**` に DebugSocketServices の internal 型の識別子が現れない。BuildContent・Config の internal 型と `AssetManagement` の internal メソッド 4 つ（`SealAddressables`、`InstallContentDirectory`、`CloseContentDirectoryAsync`、`CompleteContentDirectoryPlayStop`）への参照は、`AbstractApplicationInitializer.cs` の外（新しい 3 クラス）へ出さない。`CONTENT_DIRECTORY_STARTUP` が 1 ファイルだけを見れば済むようにするため。Rendering の `VfxRuntimeHost` は対象外（§1）。
- **I6 境界不変:** `.asmdef` の変更 0 件。`unity/Assets/SampleGame/` の変更 0 件（Tests を含む）。新しい public 型・public メンバーは 0（`DebugSocketService.CreateTelemetrySink` は internal。A2 入力版の public を A3 で改めた）。
- **I7 Telemetry 不変:** AppStartup span は BeforeSceneLoad と AfterSceneLoad の 2 つのまま。payload の `stage`（成功時 `BeforeSceneLoad` / `AfterSceneLoad`、失敗時は到達 stage）を変えない。
- **I8 新しい静的状態を作らない:** `ActiveVfxRuntimeHost` 以外の static 可変状態を増やさない。service locator を作らない。

**成立条件（起きなければならないこと）**

- **S1 サイズ:** `AbstractApplicationInitializer.cs` が 1,080 行以下。`InitializeAfterSceneLoad` が 130 行以下。（A1 初稿の 900 / 110 行は、S2 を含め、`InitializeAfterSceneLoad` の抽出を 2 つと見た値。S2 の分を戻し、A2 の再見積り（抽出 2 つでは約 149 行残る）に合わせて抽出を 4 つにし、stage の保持者の分を足した。900 行以下は `CONTENT_DIRECTORY_STARTUP` の目標として同書へ移した。見積りは §3.3）
- **S2:** 欠番（Content Directory の facade。`CONTENT_DIRECTORY_STARTUP` へ移した）。
- **S3 旧リモートカタログの隔離:** `Runtime/Bootstrap/LegacyRemoteCatalogProbe.cs`（internal）に旧 5 メソッドが移り、initializer から消える。呼び出し経路の有無は現状のまま（増やさない・減らさない）。
- **S4 UICommon 取得の隔離:** `Runtime/Bootstrap/BootstrapUiCommonLoader.cs`（internal）が UICommon の 3 経路と EventSystem 生成を持ち、所有する経路（directory / Addressables）の UICommon の GameObject と、自分が作った EventSystem の GameObject の破棄を所有する。scene handle は参照を持つだけで、`Dispose` で参照を null にする。scene の解放は AssetManagement の `ReleaseAll` が担い、loader は unload しない。借用経路では何も所有しない（§1「UICommon の所有」）。
- **S5 常駐サービス集合:** `Runtime/Bootstrap/FrameworkServiceSet.cs`（internal）が logger / DebugSocket / UpdateSystemHost / VfxRuntimeHost を保持し、`StartAsync` と `Dispose` の順序を所有する。BeforeSceneLoad の最初の `Create*` より前に空で作られ、各サービスを現行の代入位置で 1 つずつ受け取る（I4a）。DebugSocket の telemetry sink は `DebugSocketService.CreateTelemetrySink()`（新 internal）で得る。
- **S6:** 欠番（facade の判定表の単体テスト。`CONTENT_DIRECTORY_STARTUP` へ移した）。
- **S7 回帰:** 全 EditMode 回帰と `pwsh tools/contract-audit.ps1` が合格する。

### 2.2 受け入れ条件

- **AC1（I1）** 次の 3 つで判定する。
  1. 次のコマンドの出力が base と head で一致する。修飾子で始まる宣言を、`{`・`;`・`=>` が現れるまで次の行と連結してから比べるので、複数行にまたがる宣言（例: `OnPlayerContentStartupFailedAsync`、1229 行目〜）の引数まで含まれる。式形式メンバーの `=>` 以降と、行末の `{`、1 行で書いた本体は落とす（accessor の本体は S5 で `FrameworkServiceSet` 経由に変わるため）。`{ get; private set; }` のような accessor の可視性は残る。空白・括弧の内側・カンマの前後は正規化するので、宣言の改行位置を変えただけでは差にならない。base で 51 行。

     ```bash
     awk '/^[ \t]*(protected|internal|public)[ \t]/{d=$0; while (d !~ /[{;]|=>/ && (getline l)>0) d=d" "l; print d}' unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs | sed -E 's/[ \t]*=>.*$//; s/\)[ \t]*\{.*$/)/; s/[ \t]*\{[ \t]*$//; s/;[ \t]*$//; s/^[ \t]+//; s/[ \t]+/ /g; s/\( /(/g; s/ \)/)/g; s/ ?, ?/, /g' | sort
     ```

  2. `PlayerContentStage` の値の並びが base と head で一致する（修飾子を持たない enum の値は 1 のコマンドに出ないため）。base は `UiCommonReady ContentDirectoryRegistered FirstSceneStable FixtureLoaded FixtureInstantiated FixtureBehaviorVerified FixtureDestroyed FixtureHandleReleased FirstSceneUnloaded ContentDirectoryClosed` の 10 値。

     ```bash
     awk '/protected enum PlayerContentStage/{p=1;next} p&&/}/{exit} p' unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs | grep -oE '^[ \t]*[A-Z][A-Za-z]*' | tr -d ' \t' | tr '\n' ' '
     ```

  3. `grep -nE "private [^;]*\b(_assetManagement|_contentDirectorySession|_sceneDirector)\b" unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs` が base と同じ 3 つのフィールド宣言（型も同じ）を返す。

  1・2 は機械の補助で、テキストの照合なので取りこぼしがありうる（例: 属性の追加、`#if` の中の宣言）。判定 C は機械の結果に加えて、§3.5 の一覧の各メンバーについて base と head の宣言を本文で 1 つずつ見比べ、複数行の宣言の引数・既定値・属性・`static` の有無まで同じであることを §7 に記録する（必須）。合格例: 行の順序が変わる、accessor の本体が `_services?.LoggerFactory` になる。不合格例: `protected virtual` が `protected internal virtual` になる、hook が別クラスへ移って initializer から消える、複数行の宣言の引数の型が変わる、initializer と `FrameworkServiceSet` の両方に同じサービスのフィールドが残る（AC13 で確認）。
- **AC2（I2）** 次の出力が base と同じ 16 個である。

  ```bash
  grep -rhoE '"(before-scene-load|[a-z]+(-[a-z]+)+)"' unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap | sort -u
  ```

  加えて `grep -rn '"load-editor-first-scene:"' unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap` が 1 行を返す（動的 stage。上の grep は `:` で終わる文字列を拾わない）。

  かつ `InvalidCompanionConfigStartupTests`（4 件。A2 入力版の「5 件」は誤りで、`[Test]` は 4 つ。`create-scene-factory` で失敗する経路、`HasSceneDirector` / `HasAssetManagement` が false に戻ること）が変更なしで合格する。
- **AC3（I2, I3, I4, I4a）** B が head のコードを辿って次の 4 つの対応表を §6 に書く。C は表と実コードを照合する。対応が無い行、順序が入れ替わった行が 1 つでもあれば不合格。
  1. **順序:** §1 の「現在の順序」の各行 → head の呼び出し位置（`InitializeBeforeSceneLoad` / `InitializeAfterSceneLoad` / `ReleaseAll` と移動先のメソッド）。BeforeSceneLoad には、`FrameworkServiceSet` を空で作る位置（最初の `Create*` より前）と、各 `Create*` の戻り値を set へ渡す位置（次の hook 呼び出しより前）を行として含める。
  2. **stage:** §1「stage の代入点」の 18 箇所（初期値を含む）を分岐・代入箇所ごとに全数、直後に呼ぶ処理と、その代入を行うメソッドに対応付ける。同じ literal の 2 箇所（`load-ui-common`、`register-content-directory`）は別の行にする。`StartupProgress` の stage の初期値が `load-ui-common` であること。代入先は `StartupProgress`（§4 手順 4）で、catch・`CleanupAfterStartupFailureAsync`・finally の telemetry が同じインスタンスの現在値を読むこと。別の行として、`BootstrapAfterSceneLoad` の `before-scene-load` の通知が base と同じ位置・条件で残ること。
  3. **失敗経路:** AfterSceneLoad の catch の 6 行（LogError → Player 初回 Scene の unload → directory の close → `OnAfterSceneLoadInitializationFailed` → `OnPlayerContentStartupFailedAsync` → `ReleaseAll`）、OCE の filter、337・384 行目の早期 return 2 つ、BeforeSceneLoad の途中失敗で作り済みのサービスが破棄される経路、UICommon の取得途中の失敗で GameObject が破棄される経路（loader を await より前に field へ入れる）。
  4. **UICommon:** ロード済み Scene の検査（借用経路はここで return）→ `UseRequiredPlayerFileConfiguration` の評価と拒否 → directory か否かの分岐 → 該当する hook の評価とロード → handle と GameObject の記録 → component の検査 → `EnsureEventSystem` の行。加えて 3 経路ごとの所有の表（借用 / 所有）: GameObject を破棄するか、handle を持つか、EventSystem を作りうるか。`BootstrapUiCommonLoader.Dispose` の呼び出し位置が、base の `_uiCommonObject` の `Destroy` の位置（`AssetManagement.ReleaseAll` と map の解放の後）であること。

  合格例: `StartFrameworkServicesAsync` の本文が `FrameworkServiceSet.StartAsync` に移り、呼び出し位置が `OnServicesInitializing` の直前のまま。不合格例: VFX の `BindSceneLifecycle` が `RegisterAlreadyLoadedScenes` の後になる。`RunPlayerContentSmokeAsync` が stage を戻り値で返し、helper の中の失敗で catch が `load-player-first-scene` を見る。
- **AC4（I4）** 既存テスト `PlayerContentConfigurationTests`（XML の実行件数は base と同じ。`[TestCase]` を含め base で 13 件。A2 入力版の「9 件」は誤り。initializer の nested 型と static 判定を派生 harness から使う）と `SampleGame.Tests.Editor.Build.BuildContentDirectoryIntegrationTests`（1 件。Editor 統合で起動経路を通し、reflection で `_contentDirectorySession` と `_sceneDirector` を読む）が変更なしで、XML 上 **Passed** である（Skipped / Ignored は不合格。後者は StandaloneWindows64 以外で Ignore になるので、判定 C は StandaloneWindows64 の Editor で実行する）。
- **AC5（I5）** 次が 0 行を返す。

  ```bash
  grep -rn "DebugSocketTelemetrySink" unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap
  ```

  かつ、次が `AbstractApplicationInitializer.cs` の 1 ファイルだけを返す（件数は問わない。新しい 3 ファイルに 1 行でも出れば不合格。コメントに名前を書いただけでも不合格になるが、厳しい側なので許容する）。

  ```bash
  grep -rln -E "ContentDirectorySession|UnityContentDirectoryBackend|PlayerContentConfiguration|PlayerBakedContentConfiguration|ContentDeliveryFiles|ContentInstallReceipt|InstalledRevisionVerifier|VerifiedInstalledRevision|ContentDeclarationMismatch|ContentDirectoryConfiguration|RequiredJsonFileConfigProvider|SealAddressables|InstallContentDirectory|CloseContentDirectoryAsync|CompleteContentDirectoryPlayStop|AssetManagement\.AssetManagement\b|_contentDirectorySession|_bakedPlayerContent" unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap
  ```
- **AC6（I6）** 次の 3 つで判定する。
  1. `git diff --name-only base..head` に `\.asmdef$` と `^unity/Assets/SampleGame/` に当たる行が無い。
  2. 新しい public 型が 0: `git diff base..head -- 'unity/Assets/OneStarMaker/Scripts/*.cs' | grep -E '^\+[^+].*\bpublic\s+(sealed\s+|static\s+|abstract\s+|partial\s+|readonly\s+)*(class|struct|interface|enum|record)\b'` が 0 行。
  3. 新しい public メンバーが 0: `git diff base..head -- 'unity/Assets/OneStarMaker/Scripts/*.cs' | grep -E '^\+[^+].*\bpublic\b'` の各行を、B が §6 で「internal / private 型のメンバー」「コメント」「移動しただけの既存行」のどれかに分類する。C は各行がその分類に当たることを確かめ、どれにも当たらない行が 1 つでもあれば不合格。`CreateTelemetrySink` は `DebugSocketService.cs`（partial の本体）に `internal` で置く。対象を `Scripts/` に限るのは、NUnit のテスト fixture が public class であるため（テストは公開面ではない）。
- **AC7（I7）** `AppTelemetry.StartSpan(... AppStartup ...)` の呼び出しが Bootstrap folder に 2 箇所。span を閉じる処理を共通の private メソッドへ抽出した場合も、そのメソッドへ渡す stage の値が BeforeSceneLoad では `"BeforeSceneLoad"`、AfterSceneLoad では `success ? "AfterSceneLoad" : <StartupProgress の現在の stage>` で、`isSuccess`・`level`・`tags`・`payload` の渡し方が base と同じ。
- **AC8（S1）** `wc -l` で `AbstractApplicationInitializer.cs` ≤ 1,080、`InitializeAfterSceneLoad` の開始行から対応する閉じ括弧まで ≤ 130 行。
- **AC9:** 欠番（facade の判定表。`CONTENT_DIRECTORY_STARTUP` へ移した）。
- **AC10:** 欠番（facade の install 失敗経路。`CONTENT_DIRECTORY_STARTUP` へ移した）。
- **AC11（S3）** `LegacyRemoteCatalogProbe.cs`（internal static class。移動で変えてよいのは、`_config` の読み取りを引数 `AppConfig config` に置き換えることと `static` を付けることだけ）に `TryLoadAsync` / URL 解決 / revision 比較 / `build-info.json` URL 導出 / ローカル revision 解決が移り、`AbstractApplicationInitializer.cs` に `Addressables.LoadContentCatalogAsync`、`UnityWebRequest`、`RemoteCatalogRuntimeBridge`、`using UnityEngine.Networking` が無い。`DeriveBuildInfoUrl` 相当の純関数に 3 ケース（末尾 `/` あり、`/` なし、空）の単体テストを足す（`Tests/Bootstrap/`）。
- **AC12（S4, I4a）** `AbstractApplicationInitializer.cs` に `SceneManager.GetActiveScene`、`EventSystem`、`InputSystemUIInputModule`、`GetComponent<UICommon>` の参照が無い。`BootstrapUiCommonLoader` は、required Player のときに Addressables fallback を拒否する現行の例外と、directory の key が空のときの例外を、現行と同じメッセージ・同じ判定順で投げる。`UseRequiredPlayerFileConfiguration` は `Func<bool>`、hook（`GetDirectoryBootstrapSceneLogicalKey` / `GetUICommonPrefabAddress`）は `Func<string>` で loader に渡し、loader の中の現行と同じ時点・分岐でだけ評価する。directory か否か（`_contentDirectoryConfiguration != null`）は副作用の無い field の読み取りなので、呼び出し時に bool で渡してよい。借用経路では loader は何も所有しない（AC3 の表 4）。
- **AC13（S5, I3）** `AbstractApplicationInitializer.ReleaseAll` のサービス部が、`_cts.Cancel` → `ActiveVfxRuntimeHost` の条件付き解除（`ReferenceEquals` で自分の host のときだけ） → `FrameworkServiceSet.Dispose` → `_services = null` → `_cts.Dispose` の順である。`FrameworkServiceSet.Dispose` の内部順は VFX → DebugSocket → telemetry sink の登録 flag の初期化 → UpdateSystemHost → LoggerFactory で、未生成のものは飛ばし、2 回呼んでも安全。set は BeforeSceneLoad ごとに作り直し、使い回さない（sink の「一度だけ登録」は set のインスタンスが持つ）。`Dispose` の中に try/catch を足さない（I3）。initializer に logger / DebugSocket / UpdateSystemHost / VfxRuntimeHost のフィールドが残らない（AC3 の表 1 と `grep` で確認）。
- **AC14（S7, I6）** `pwsh tools/run-tests.ps1`（空 filter、EditMode）合格。テスト件数が base + 新規（AC11 の 3 件 + AC15 の 3 件 + AC16 の 2 件）以上。`pwsh tools/contract-audit.ps1` 合格。
- **AC15（I3, I4, I4a）** `Tests/Bootstrap/` に、`InvalidCompanionConfigStartupTests` の `TestInitializer` と同じ形の派生 harness（config file なし、環境変数で `content:runtimeMode=addressables`）を使う EditMode テストを 3 件足す。harness は `OneStarMaker.Tests` の中の型（`FakeAssetBackend` 等）だけで組み、SampleGame の型を使わない。組めない場合は Phase B を止めて Phase A へ戻す（テストの配置と観測点を決め直す。Phase C へ未確認のまま渡さない）。各テストは TearDown で `BootstrapSubsystemRegistration` を呼び、`ActiveVfxRuntimeHost`・`UpdateSystemRuntime`・`AppTelemetry` の static 状態を戻す。BeforeSceneLoad の失敗は `Debug.LogException` を出すので `LogAssert.Expect` で受ける。
  1. **サービスの途中まで:** `CreateVfxBackend` の override が、呼ばれた時点で `UpdateCoordinator` と `LoggerFactory` が非 null であることを記録してから例外を投げる。`BootstrapBeforeSceneLoad` の後に、記録が両方 true で、`UpdateCoordinator` と `LoggerFactory` が null、`UpdateSystemRuntime.Coordinator` が null（作り済みの host が破棄された）であることを確かめる。
  2. **サービスが揃った後:** `RegisterDefaultTelemetrySink` の override が、呼ばれた時点で `LoggerFactory` と `Vfx` が非 null であることを記録してから例外を投げる（base は呼ばない）。後に、記録が両方 true で、`Vfx`・`LoggerFactory`・`UpdateCoordinator`・`ActiveVfxRuntimeHost`・`UpdateSystemRuntime.Coordinator` が全て null であることを確かめる。
  3. **UICommon の component 欠落:** `FakeAssetBackend.SetSceneRoots` に UICommon component の無い root を置き（Addressables 経路）、`BootstrapAfterSceneLoad` が `load-ui-common` で失敗して `OnAfterSceneLoadInitializationFailed` に `load-ui-common` が渡り、例外メッセージが base と同じ（`UICommon component not found on prefab: <address>`）で、`ReleaseAll` が所有する root を破棄しようとする（EditMode の `Destroy may not be called from edit mode` を `LogAssert.Expect` で受ける。既存テストと同じ観測）ことを確かめる。借用経路（active Scene が `UICommon`）は EditMode で作れないので AC3 の表 4 で確認する。
- **AC16（I3）** `Tests/Bootstrap/FrameworkServiceSetTests.cs` に、Unity の transport・PlayerLoop を使わない 2 件を足す: 全て未設定のまま `Dispose` しても例外が出ない、`Dispose` を 2 回呼んでも例外が出ない。
- **AC17（§2.4 のログ文言）** 次の出力（`[AppInit]` を含む文字列リテラルの集合）が base と head で一致する。移動先のファイルを含めるため Bootstrap folder 全体を対象にする。

  ```bash
  grep -rhoE '"\[AppInit\][^"]*"|\$"\[AppInit\][^"]*"' unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap | sort
  ```

  複数行に分けた文字列は先頭の断片だけが比べられる。C は差が出た行について、文言が変わったのか断片の切れ目が変わっただけかを本文で確かめる。

### 2.3 ここでは答えない問いと所有スライス

- Content Directory 起動を facade（`ContentDirectoryStartup`）に集め、Bootstrap から BuildContent の internal 型と `AssetManagement` の directory 用 internal メソッドへの参照を 0 にする: `CONTENT_DIRECTORY_STARTUP`（[RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md](RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md)。program A0 §6 D4 の (3)）
- 連携部分と OSM 固有部分を BuildSystem 本体から分ける asmdef の判断: asmdef スライス（未作成。program A0 §4.3 の X1・X4）
- Bootstrap を `OneStarMaker.Runtime.Bootstrap` assembly に切り、namespace を揃える: asmdef スライス（未作成）
- `DebugSocketService.CreateTelemetrySink` を public にするか（Bootstrap と DebugSocketServices を別 assembly にしたときに要る）: asmdef スライス。本スライスは internal で置く
- `VfxRuntimeHost` を public にするか、Bootstrap と Rendering の境界をどう引くか: asmdef スライスの A0。S-4d program の所有物なので本スライスは触らない
- `04-app-startup.md` §4.7「Editor 設定読取の未決」（`JsonFileConfigProvider` が Addressables を読む）: 本スライスは `BuildConfig` の provider 列を変えない。未決は未決のまま残す
- `UpdateBehaviourAdapter` や `InputManager` のように製品配線の無い入口を initializer から配線するか: asmdef Stage 3 / 各機能の所有スライス
- `AbstractApplicationInitializer` を 500 行以下にするために hook を interface へ括り出す等の公開面の再設計: 本スライスでは行わない。必要なら新しい Phase A

### 2.4 本文へ転記した実装制約（`AGENTS.md` の常時契約から）

- 依存は Game → Framework の一方向。asmdef 参照の追加は行わない。
- アセットは `IAssetManagement` 経由。`BootstrapUiCommonLoader` と map の取得は現行と同じ `IAssetManagement` の公開メソッドを使う。
- `SceneState` の 14 値を変えない。状態変更は `SceneLifecycleManager` が所有する（本スライスは読まない）。
- 公開 API のログ抽象は `ILogger<T>`。新しい public 面は作らない（I6）。internal の `CreateTelemetrySink` も signature に `ZLogger*` 型を出さない。現行 initializer は `Debug.Log*` を使っており、移動先でも同じ呼び出しを保つ（ログ文言を変えない）。
- Update は `UpdateSystemRuntime` に登録する。`FrameworkServiceSet` は `UpdateSystemHost` の生成・破棄を移すだけで、登録の経路を変えない。
- Editor コードを Runtime アセンブリに置かない。現行の `#if UNITY_EDITOR` 3 箇所（`ResolveRemoteCatalogUrl`、`ResolveLocalRevision`、`ResolveSceneVariant`）はそのまま移す。
- `record` を使わない。新規 `.cs` の先頭に `#nullable enable`。
- 破棄されうる `UnityEngine.Object`（UICommon の GameObject、EventSystem の GameObject、`EventSystem.current`）は `== null` / `!= null` で判定する。現行コードもそうなっている。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。
- 参照 0 を削除理由にしない（旧リモートカタログ経路は隔離して残す）。
- 移したメソッド名を `<c>` で参照しているコメント（`Editor/Build/Variants/LocalRevisionEditorInjector.cs:19`、`Runtime/AssetManagement/RemoteCatalogRuntimeBridge.cs:18`、`Runtime/Rendering/Vfx/Implements/VfxRuntimeHost.cs:17`）は、コメントだけを追随させてよい。コードは変えない。
- Unity Editor でのコンパイル確認は Phase B が行ってよい。Unity テストの実行・判定は Phase C。`unity test` / `unity run` は使わず `pwsh tools/run-tests.ps1`。

### 2.5 未決事項（A2 で意見を求める）

- **U1・U2:** 欠番（facade の入力の渡し方と `RequiredJsonFileConfigProvider` の扱い。`CONTENT_DIRECTORY_STARTUP` へ移した）。
- **U3（決定済み）:** A1 初稿は「`FrameworkServiceSet` を本スライスに含めるか」と、S2 を独立スライスにする案を残していた。program A0 §6 D4（owner 判断、2026-11-07）で S2 を独立スライスにすることが決まり、本書は S3〜S5 を 1 スライスで扱う（§2.6）。`FrameworkServiceSet`（S5）は含める。含めないと initializer は S1 の 1,080 行を満たさない（§3.3）。
- **U4（事実で解消）:** S-4d5b は PR #157（2026-11-03）で `develop` に入った。変更は Architecture 文書への harvest と HANDOFF の削除だけで、`AbstractApplicationInitializer` を触っていない。S-4 program 自体も削除済みなので、本スライスと並行して initializer を触る S-4 のスライスは無い。
- **U5 private メンバーの名前（A3 で閉じた）。** A2 で `_contentDirectorySession` と `_sceneDirector` の reflection が見つかり、I1 と §3.5 に加えた。B は Phase B 開始時に `GetField(` / `GetMethod(` / `GetProperty(` / `nameof(` を `unity/Assets/SampleGame/Tests`（`Editor` を含む）と `unity/Assets/OneStarMaker/Tests` で grep し直す（`GetField(` の引数は次の行に折り返すことがあるので `-A2` で見る）。base より増えていれば名前を保ち、§6 に記録する。

### 2.6 スライス分割の判断

- **S2 を切り出した（D4 の適用）。** S2 は AssetManagement / BuildSystem の単独利用 program の問い（越境を 1 箇所に集め、asmdef スライスが裁定できるようにする）に答えるもので、`BUILDSYSTEM_STANDALONE_ENTRY` の結論（`ContentDirectoryIndex` 等に public 入口ができるか、target の固定を外すか）で facade の越える internal と入力が変わる。S3〜S5 はその program に関係しない。変更理由も判定に使う証拠（S2 は Content Directory の判定表と install 失敗、S3〜S5 は initializer の順序の保存）も違うので、問いを分けた。素材は [RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md](RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md) に移した。
- **S3〜S5 はこれ以上分けない。** 3 つとも「initializer から変更理由の違う責務を出す」同じ問いの部分で、判定は同じ不変条件（I1〜I4 の公開面・順序・失敗経路の保存）と同じ証拠（AC3 の順序対応表、directory Play 1 回、全 EditMode 回帰）で行う。分けると、同じ順序対応表と動作確認を 3 回作ることになる。独立して失敗しうる未知も無い（S3 は呼び出しの無い経路の移動、S4・S5 は既知の経路の移動で、新しい実行時の経路を作らない）。A1 初稿の「B1 = S2 + S3、B2 = S4 + S5」は、S2 が抜けたので根拠（asmdef の前提を先に）を失った。
- **`InitializeAfterSceneLoad` の分割は本書に残す。** Player content smoke の 6 段と失敗時 cleanup は Content Directory の close を含むが、private メソッドへ移すだけで意味も呼び出し先も変えず、同じファイルに留まる（I5）。`CONTENT_DIRECTORY_STARTUP` は、その private メソッドの中の呼び出しを facade に置き換える。
- **受け入れ条件に別の問いの判定は残っていない。** AC4 の `BuildContentDirectoryIntegrationTests` は Content Directory の機能ではなく、initializer の起動経路が変わっていないことの回帰として使う。

## 3. 責務マップ

配置は全て既存 folder。新しい asmdef・folder・namespace は作らない。

### 3.1 ファイルと責務

| ファイル | 責務（一文） | 変更理由 | 所有者・寿命 | 許可する依存 | 公開面 | 現在行 → 予想 |
|---|---|---|---|---|---|---|
| `Runtime/Bootstrap/AbstractApplicationInitializer.cs` | 3 フェーズの起動順序、失敗境界、派生クラス向け hook の template | 起動契約（順序・stage・hook）が変わるとき | App 寿命。`BootstrapUiCommonLoader` と `FrameworkServiceSet` のインスタンスを所有し `ReleaseAll` で破棄（`LegacyRemoteCatalogProbe` は static で所有しない） | 下の 3 クラス、`SceneDirector` / `UICommon` / `IAssetManagement` / `AppConfig` / `AppTelemetry` の public API。Content Directory 起動のための BuildContent・Config の internal 型と `AssetManagement` の internal メソッド 4 つは base のまま残る（`CONTENT_DIRECTORY_STARTUP` が外す。I5） | §3.5 のまま | 1,303 → 約 1,000（非分割判断は §3.3） |
| `Runtime/Bootstrap/LegacyRemoteCatalogProbe.cs`（新規、internal static class） | 旧リモート Addressables catalog の追加ロードと revision 乖離警告（案内のみの旧経路） | 旧経路の案内文・URL 規則が変わるとき、または退役するとき | なし（状態を持たず、呼び出し経路も無い。退役時に 1 ファイル削除） | Addressables、`UnityWebRequest`、`RemoteCatalogRuntimeBridge`、`RemoteBuildInfo` | internal static: `UniTask TryLoadAsync(AppConfig config)`、`string DeriveBuildInfoUrl(string)` | 0 → 約 160 |
| `Runtime/Bootstrap/BootstrapUiCommonLoader.cs`（新規、internal sealed） | UICommon の取得（3 経路: ロード済み Scene の借用 / directory logical key / Addressables address）と、所有する経路での GameObject・EventSystem の GameObject・scene handle の参照の寿命 | UICommon の取得経路・EventSystem の構成が変わるとき | initializer が所有し、**`LoadAsync` を await する前に** field へ入れる。loader は handle と GameObject を、現行と同じ位置（component の検査より前）で自分の field に入れる。GameObject は App 寿命で、`Dispose` で破棄（2 回呼んでも安全）。借用経路では何も所有せず、`Dispose` は何もしない。scene handle は参照を持つだけで、`Dispose` で null にする（unload しない。scene の解放は AM の `ReleaseAll`）。`Dispose` は initializer の `ReleaseAll` の中で、base の `_uiCommonObject` の `Destroy` の位置（AM の `ReleaseAll` と map の解放の後）で呼ぶ | `IAssetManagement`、`UICommon`、`SceneManager`、`EventSystem`、`InputSystemUIInputModule`、`SceneAssetDescription` | internal: `UniTask<UICommon> LoadAsync(IAssetManagement, BootstrapUiCommonSource source)`、`void Dispose()`。`BootstrapUiCommonSource` は internal readonly struct で、directory か否かの bool、`UseRequiredPlayerFileConfiguration` を遅延評価する `Func<bool>`、hook を遅延評価する `Func<string>` 2 つを持つ（mode を先に確定しない。I4a） | 0 → 約 115 |
| `Runtime/Bootstrap/FrameworkServiceSet.cs`（新規、internal sealed） | logger / DebugSocket / UpdateSystemHost / VfxRuntimeHost の段階的な保持、`StartAsync`、telemetry sink の一度だけの登録、`Dispose` の順序 | 常駐サービスの集合と開始・停止順が変わるとき | initializer が所有。BeforeSceneLoad の最初の `Create*` より前に空で作って field に入れ、生成は initializer の hook（`Create*`）が行い、戻り値を現行の代入位置で 1 つずつ set に渡す（I4a）。BeforeSceneLoad ごとに作り直し、`ReleaseAll` で `Dispose` して null に戻す | `DebugSocketService`（public）、`UpdateSystemHost`、`AppLoggerFactory`、`VfxRuntimeHost`（internal、S-4d 所有）、`AppTelemetry` | internal: 引数なし ctor、getter / setter `AppLoggerFactory? LoggerFactory`・`DebugSocketService? DebugSocket`・`UpdateSystemHost? UpdateSystemHost`・`VfxRuntimeHost? Vfx`、`UniTask StartAsync(CancellationToken)`、`void Dispose()`（未設定は飛ばし、2 回呼んでも安全）。`ActiveVfxRuntimeHost` の設定・解除は initializer に残す（setter が private のため） | 0 → 約 130 |
| `Runtime/DebugSocketServices/DebugSocketService.cs` | （既存。追加のみ） | sink の生成を service の責務にする | — | — | internal: `ITelemetrySink CreateTelemetrySink()`（partial の本体ファイルに置く。`DebugSocketTelemetrySink` は internal のまま） | 391 → 約 400 |
| `Tests/Bootstrap/LegacyRemoteCatalogProbeTests.cs`（新規） | AC11 の 3 ケース | — | — | — | — | 0 → 約 40 |
| `Tests/Bootstrap/FrameworkServiceSetTests.cs`（新規） | AC16 の 2 件 | — | — | — | — | 0 → 約 30 |
| `Tests/Bootstrap/BeforeSceneLoadServiceStagingTests.cs`（新規） | AC15 の 3 件（派生 harness。`OneStarMaker.Tests` の型だけ） | — | — | — | — | 0 → 約 170 |
| `unity/Assets/Docs/Architecture/04-app-startup.md` | harvest 先（Phase D）。§4.2 の図を新構成（initializer + 3 クラス）に、§4.3 の `ReleaseAll` 図に `FrameworkServiceSet.Dispose` / `BootstrapUiCommonLoader.Dispose` を反映 | — | — | — | — | +15 程度 |

### 3.2 配置と依存を選んだ理由

- **Content Directory 起動の置き場所と、assembly を分けたときの扱いは本書で決めない。** `CONTENT_DIRECTORY_STARTUP` の範囲（[同書](RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md) §3.2。A0 §4.2 の改訂を当てた理由はそこにある）。A1 初稿のここにあった「ASSET_CYCLE §1 の owner 決定で、目標は 2 assembly」は、owner の上位目的（AssetManagement 単独・BuildSystem 単独・併用。[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)）に置き換わって無効になった。本書が守るのは、その越境を新しい 3 クラスへ広げないこと（I5）だけ。
- **旧リモートカタログは Bootstrap folder に残す。** Addressables 互換経路の案内であり、BuildContent（Content Directory）の責務ではない。退役するときに 1 ファイル消せる形にしておく。
- **UICommon 取得と常駐サービスは internal。** 派生クラスは hook 経由で値を渡すだけで、これらの型を直接使わない。公開面を増やさないため internal にする。
- **hook は initializer に残す。** 公開面を変えない（I1）。`FrameworkServiceSet` は hook が作ったインスタンスを受け取る構成にして、派生クラスが override する場所を変えない。
- **新しい interface や抽象は作らない。** 3 クラスは具象で、initializer が直接生成する。差し替え点は既存の hook のままにし、テストは URL 導出の純関数と既存の harness で足りる。

### 3.3 行数警報

- **500 行:** `AbstractApplicationInitializer.cs` は約 1,040 行で残る。見積り（base `d4861882` の行範囲から）: S3 で 673〜826 行目の約 150 行、S4 で 514〜592 行目とフィールド・`ReleaseAll` の破棄で約 85 行、S5 で 886〜924 行目と `DisposeVfx`・フィールド・`ReleaseAll` の破棄で約 55 行が減る。`InitializeAfterSceneLoad` の 4 つの抽出（signature と波括弧で 1 つ約 5 行）と `StartupProgress`（約 10 行）で約 30 行増える。A2 の独立した再見積りは抽出込みで約 1,015〜1,035 行。S1 は 1,080 行以下とした。`InitializeAfterSceneLoad` は 219 行から、catch 本体（約 48 行減）、Player smoke（約 18 行減）、Editor 初回 Scene（約 10 行減）、finally の telemetry（約 16 行減）を抜いて約 125 行になる見込みで、S1 は 130 行以下とした。`CONTENT_DIRECTORY_STARTUP` が残りの Content Directory 分（約 130 行）を外して約 890 行になり、A1 初稿の見積り（約 860 行）とほぼ一致する。**非分割妥当**と判断する根拠: 本スライス後に残る内容は (a) XML doc と使用例 約 60 行、(b) protected 公開面の宣言と doc 約 250 行、(c) 3 フェーズの順序と失敗境界 約 350 行、(d) `BuildConfig` / `RegisterAlreadyLoadedScenes` / map 取得 約 100 行（map の取得は UICommon と同じく「directory なら logical key、それ以外は address」で呼び分けるが、所有物を持たず AM の App owner の handle だけなので、loader へ移さず順序の側に残す）、(e) Content Directory 起動 約 150 行（`CONTENT_DIRECTORY_STARTUP` の範囲）。(b) は公開面なので動かせない（I1）。(c) は「起動順序の契約」という 1 つの変更理由。(d) は順序と同じ理由で変わる。(e) は別スライスが外す。これ以上分けるには hook を interface へ括り出す公開面の再設計が要り、本スライスの対象外。A2 はこの判断を審査する。
- **3 責務:** 変更後の initializer は「順序 + hook + 失敗境界」と、`CONTENT_DIRECTORY_STARTUP` まで残る Content Directory 起動の 2 つ。後者は本スライスの対象外で、同書が外す。3 クラスは各 1 責務。
- **50% 増:** 既存ファイルで 50% 以上増えるものは無い。`DebugSocketService.cs` は +10 行。

### 3.4 harvest 先

`04-app-startup.md` §4.2 と §4.3（Phase D）。§4.7 の契約文は変えない。`20-variant-checkout-workflow.md` の旧リモート案内は、クラス名を書いていなければ変更不要（B が確認）。

### 3.5 公開面の一覧（I1 の基準。base `d4861882`）

変えない・増やさない・減らさないもの。

- protected static: `BootstrapSubsystemRegistration(AbstractApplicationInitializer)`、`BootstrapBeforeSceneLoad(AbstractApplicationInitializer)`、`BootstrapAfterSceneLoad(AbstractApplicationInitializer)`、`AppendFixtureStages(List<PlayerContentStage>, IReadOnlyList<PlayerContentStage>)`
- protected accessor: `Config`、`SceneDirector`、`AssetManagement`、`Vfx`、`LoggerFactory`、`UpdateCoordinator`
- protected abstract: `CreateSceneFactory()`、`GetUICommonPrefabAddress()`、`GetSceneResourceMapAddress()`、`CreateLoadingDisplay()`
- protected virtual: `CreateVfxBackend()`、`ResolveSceneVariant()`、`GetDirectoryBootstrapSceneLogicalKey()`、`GetDirectoryBootstrapMapLogicalKey()`、`GetConfigFilePath()`、`UseRequiredPlayerFileConfiguration`、`GetRequiredPlayerConfigurationPath()`、`LoadPlayerSceneResourceMap()`、`GetEnvironmentVariablePrefix()`、`OnServicesInitializing(CancellationToken)`、`OnAfterSceneLoadInitializationFailed(string, Exception)`、`OnPlayerContentReadyAsync(IAssetManagement, CancellationToken)`、`OnPlayerContentShutdownCompletedAsync(IReadOnlyList<PlayerContentStage>)`、`OnPlayerContentStartupFailedAsync(string, Exception, IReadOnlyList<PlayerContentStage>)`、`CreateLoggerFactory(Stream?)`、`CreateDebugSocketService(AppConfig)`、`CreateDebugCommandDispatcher()`、`CreateUpdateSystemHost()`、`RegisterDefaultTelemetrySink()`
- protected nested: `enum PlayerContentStage`（10 値、順序固定）、`sealed class PlayerContentFixtureResult`
- internal static: `ShouldNotifyPlayerContentFailure(bool)`、`RunsPlayerContentSmoke(bool)`、`ReadEditorFirstScene(AppConfig)`、`ActiveVfxRuntimeHost { get; private set; }`
- private だが名前を保つもの: フィールド `_assetManagement`（`InvalidCompanionConfigStartupTests` の reflection）、`_contentDirectorySession`（`BuildContentDirectoryIntegrationTests` の reflection）、`_sceneDirector`（同テストと VFX の Play テスト群の reflection）

## 4. 実装計画

- 変更対象: §3.1 の表。
- 順序（各手順を 1 commit にし、手順ごとに Unity Editor でコンパイルを確認する）:
  1. `LegacyRemoteCatalogProbe` へ 5 メソッドを移す（`_config` は引数にする。AC11）（本文は変えない）。AC11 のテスト。
  2. `BootstrapUiCommonLoader` へ `LoadUICommonAsync` / `EnsureEventSystem` と GameObject / handle の破棄を移す。
  3. `DebugSocketService.CreateTelemetrySink()`（internal）を足し、`FrameworkServiceSet` へ `StartFrameworkServicesAsync` / `DisposeVfx` の破棄部分 / service の破棄順を移す。set は BeforeSceneLoad の最初の `Create*` より前に空で作り、各 `Create*` の戻り値を現行の代入位置で set に渡す。protected accessor（`LoggerFactory`、`UpdateCoordinator`、`Vfx`）と base の `RegisterDefaultTelemetrySink` は set を読む。`ActiveVfxRuntimeHost` の設定・解除は initializer に残し、解除は `ReferenceEquals` の条件付きで set の `Dispose` より前に行う（AC13）。
  4. `InitializeAfterSceneLoad` を次の 4 つの private メソッドに分け、本体を 130 行以下にする。
     - `RunPlayerContentSmokeAsync`（Player content smoke の 6 段）と `LoadEditorFirstSceneAsync`（Editor directory Play の初回 Scene）。
     - `CleanupAfterStartupFailureAsync`（catch の中の Player 初回 Scene の unload、directory の close、2 つの hook の呼び出し。段ごとの try/catch は現行のまま持つ）。catch の `LogError` と `ReleaseAll` は本体の catch に残し、`ReleaseAll` は helper の後に呼ぶ。
     - `FinishStartupSpan`（span を閉じる処理。BeforeSceneLoad の finally と共通にする。AC7）。
     - stage と `playerContentStages` は private nested の `sealed class StartupProgress` に置き、`InitializeAfterSceneLoad` の冒頭で 1 回の起動ごとに作って各 helper に渡す（static にしない。I8）。stage の代入は helper の中でも `StartupProgress` に対して行い、戻り値で返さない（AC3 の表 2）。
     - `RunPlayerContentSmokeAsync` と `LoadEditorFirstSceneAsync` は try/catch を持たない。OCE の filter と `catch (Exception)` は本体に 1 つずつ残す。337・384 行目の早期 return は本体に残す。
     - helper はファイルの中で `InitializeAfterSceneLoad` の直後に、呼ばれる順に置く。
     - 両 helper の中の Content Directory の close（`CloseContentDirectoryAsync`）と具象 `AssetManagement` への確認は、本文を変えずにそのまま移す（`CONTENT_DIRECTORY_STARTUP` が facade に置き換える）。
  5. AC15 / AC16 のテストを書く（AC15 の harness は最初に組めるかを確かめる。組めなければ差し戻し）。
  6. AC1 / AC2 / AC5 / AC6 / AC7 / AC8 / AC17 のコマンドを実行し、AC3 の 4 つの表と AC6 の分類を §6 に書く。head で `grep -n "_contentDirectoryConfiguration\|_contentDirectorySession\|_bakedPlayerContent"` を実行し、Content Directory の状態に触れるメソッドの一覧を §6 に `CONTENT_DIRECTORY_STARTUP` への入力として記録する（受け入れ条件ではない）。`pwsh tools/contract-audit.ps1`。
- Phase B から Phase A へ差し戻す条件: §3.5 の公開面を変えないと分けられない。AC15 の harness が `OneStarMaker.Tests` の型だけで組めない。順序を変えないとコンパイルできない、または例外の握り方を変えないと移せない箇所がある。§3.1 にない public メンバーが要る。`VfxRuntimeHost` の型・可視性を変える必要がある。Content Directory 起動のコード（`CONTENT_DIRECTORY_STARTUP` の範囲）を initializer の外へ出さないと S1 か AC8 を満たせない。SampleGame のテストを変えないと通らない。
- 対象外を維持する方法: Content Directory 起動のコード（`ReadContentDirectoryConfiguration`、nested `ContentDirectoryConfiguration`、`InstallConfiguredContentDirectoryAsync`、`WarnOnDeclarationMismatch`、`BuildConfig`、Addressables の封じ、`ReleaseAll` の directory 分岐）の本文を変えない（AC5）。`.asmdef` を開かない。`unity/Assets/SampleGame/` に触れない（AC6 の `git diff --stat`）。hook の宣言行を編集しない（AC1）。`04-app-startup.md` の変更は Phase D まで行わない。

## 5. テストとレビュー計画

- 単体テスト: `LegacyRemoteCatalogProbeTests`（3 件）、`FrameworkServiceSetTests`（2 件）、`BeforeSceneLoadServiceStagingTests`（3 件、派生 harness）。Unity なしで検証できる範囲は URL 導出と set の空・二重の Dispose。成功経路の UICommon 取得と常駐サービスは既存の起動経路のテスト（AC2 / AC4、`GameSceneLoggingTests`、VFX の Play テスト群、`FlyControllerTeleportTests`）と AC3 の対応表で確認する。BeforeSceneLoad の途中失敗、サービスが揃った後の失敗、UICommon の component 欠落は既存テストが通らないので AC15 で足す（A2 と外部レビューの指摘）。UICommon の借用経路は EditMode で作れないので AC3 の表 4 で確認する。
- 差し戻し中の起点 `-Filter`（C が根拠付きで変更可。受け入れ条件の追加ではない）: `OneStarMaker.Tests.BuildContent`、`OneStarMaker.Tests.Bootstrap`、`SampleGame.Tests.Bootstrap`、`SampleGame.Tests.Editor.Build`、`OneStarMaker.Tests.SampleGame`（`GameSceneLoggingTests`。A2 入力版の `SampleGame.Tests.App` は存在しない namespace だった）、`SampleGame.Tests.Rendering`（VFX の Play テスト群が `ActiveVfxRuntimeHost` と `_sceneDirector` を読む）、`SampleGame.Tests.Streaming`（`FlyControllerTeleportTests` が実際の `AppInitializer` を起動する）。
- 判定必須テスト（GO 候補 head）: 全 EditMode 回帰（空 filter、StandaloneWindows64 の Editor）。XML で、新規 8 件、AC2 / AC4 の既存テスト、VFX の Play テスト群、`FlyControllerTeleportTests` が収録され、Passed であること（Skipped / Ignored は不合格）。
- 全 EditMode 回帰の適用除外: なし
- 統合・Unity テスト: 既存の `BuildContentDirectoryIntegrationTests`（Editor 統合、1 件、約 300 行）が Content Directory の build → install → 起動を通す。S-4d5a の Play テスト群（`SampleGame/Tests/Rendering/Vfx/*PlayTests`）が `ActiveVfxRuntimeHost` 経由で VFX 配線を確認する。どちらも全 EditMode 回帰に含まれる。Build は不要。
- 操作・実行時・目視条件の検証経路: **Editor の directory Play を 1 回**（AC3 の順序表の実機確認）。担当は動作確認の実行担当（AI）。環境: ローカル Unity Editor、Content Directory build 済みの install が Delivery に 1 つある状態（無ければ `20-variant-checkout-workflow.md` の手順で用意する。用意できなければ「未実施」と理由を書く）。操作: Title Scene から Play を開始し、Title が表示されたら停止する。観測: Console に `[AppInit] AfterSceneLoad: loading UICommon.` → `loading SceneResourceMap.` → `Starting DebugSocket transport` または `DebugSocket は無効です` の順で出ること、例外が無いこと、停止時に `Cannot find handle for scene` 等の例外が無いこと。保存する証拠: Console ログの text。合否は構造（ログ順序と例外 0）で判断し、画像は使わない。C / C' への受け渡しは evidence bundle。
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点: なし（directory Play は S-4 で反復済みの既知経路）。
- 人間の判断が必要な条件と合意した担当・証拠の受け入れ方: なし
- 高コスト証拠: なし
- 判定 C の照合 script の方式: なし（AC のコマンド、AC3 の対応表、XML のテスト名・件数で判定する）。
- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`、AC1 / AC2 / AC5 / AC6 / AC7 / AC8 / AC17 のコマンド。
- A0/A1 主担当・モデル・ベンダー: Claude Code（Anthropic）。調査は本書 §1 の現況に転記済み。
- A2 独立レビューごとの観点・担当・モデル・ベンダー、入力版: 下記。観点は (a) アーキテクチャゲート（3 クラスの責務と配置、`CreateTelemetrySink` 以外に公開面が要らないか、§3.3 の非分割判断と S1 の 1,050 行、§2.6 の分割の判断、I5 の「越境を新しいクラスへ広げない」が `CONTENT_DIRECTORY_STARTUP` の前提として足りるか）、(b) 不変条件の破り方（I2〜I4 を破る順序を 1 つ挙げられるか、AC3 の対応表方式で検出できるか、stage 名と receipt の互換）、(c) テスト境界（UICommon 取得と常駐サービスに新しい単体テストを作らない判断、SampleGame.Tests の reflection 以外の隠れた結合）。目的レビューは §1 の「目的」欄の原文だけを入力にする（利用者は `なし`）。高リスクなので、A0（program A0 と本書 §1 の現況）だけから代替構成を出すレビューを検討する。A2 は「計画対象は実装済みと仮定して」評価し、未実装を理由に差し戻さない。
- A3 統合担当・採否: owner + 主担当。採否は下の「A3 の採否」。owner が 2026-11-07 に凍結を指示した。
- C' 用に予約した担当・モデル・ベンダー: A2 で使わなかった系列。高リスクなので、Phase A / B / C に関与していないベンダーを優先する。
- 独立性の強化条件を満たせない場合の理由: 未定（A2 で記録する）。

### A2 独立レビュー

- 入力版: commit `7b2cf205` の本書。content sha256 `172391acd5d1c3581c3fdc6b713123cbeb52b154a73a00c0cbe4bb8008170c15`。
- 実施: 2026-11-07。4 件を互いの指摘を見せずに並行して実施した。いずれも主担当と同じベンダーの別 session（独立性制約あり。owner が別ベンダーの外部レビューを別途行う）。
  - **R1 アーキテクチャゲート**（観点 (a)）: 入力版、`architecture-gates.md`、program A0、`CONTENT_DIRECTORY_STARTUP`、現コード。「計画対象は実装済みと仮定」を指示。判定: 条件付き支持（blocker 1 / should 7 / nit 3）。
  - **R2 不変条件の破り方とテスト境界**（観点 (b)(c)）: 入力版と現コード・テスト。同じ枠の指示。判定: 条件付き支持（blocker 3 / should 6 / nit 2）。
  - **R3 目的レビュー**: §1「目的」欄の原文だけ（利用者 `なし`）。入力 sha256 `2aae67b5787e95c2d35857fbd65fb59218051a0bcdb03bf121c9404567a49775`。保証すべきこと 12 件、作らないもの 10 件。
  - **R4 A0 だけからの代替構成**: 本書 §1 と program A0、現コードだけ（初稿は読まない）。推奨案は本書と同じ 3 クラス（`FrameworkServiceSet`、UICommon の loader（hook は `Func<string>`）、旧カタログの static class）+ `InitializeAfterSceneLoad` の同ファイル内分割（stage は private nested の保持者）+ internal 側の `CreateTelemetrySink`。initializer 約 1,040 行。代替案（partial class のみ、stage の pipeline）は不採用を推奨。
- R1・R2 の生の回答は git に入れていない（主担当の作業領域にだけある）。採否は下の表が正。

### A3 の採否

| 指摘 | 内容 | 採否 | 反映先・理由 |
|---|---|---|---|
| R1 F-01 / R2 F-02（blocker） | `FrameworkServiceSet` を 4 インスタンスの ctor で作ると、BeforeSceneLoad 途中失敗でサービスが漏れ、hook から accessor が null に見え、base の telemetry sink が消える | 採用 | 段階的に満たす保持者に変更（§3.1、S5、§4 手順 3）。I4 / I4a、AC3 表 1・3、AC15 |
| R2 F-01（blocker） | AC1 が accessor の本体まで比べるので、正しい実装で必ず不一致 | 採用 | AC1 の sed で `=>` 以降を落とす |
| R2 F-03（blocker）/ R1 F-02 後半 | async helper へ stage を渡す方式が未定。戻り値で返すと receipt の stage が黙ってずれる | 採用 | `StartupProgress`（§4 手順 4）、AC3 表 2 |
| R1 F-02（should） | 抽出 2 つでは `InitializeAfterSceneLoad` が約 149 行残り 110 行に届かない | 採用（数値と手順の両方） | 抽出を 4 つ（Editor 初回 Scene、span の終了を追加）にし、上限を 130 行、ファイルを 1,080 行に（S1、AC8、§3.3）。R3 の「極小ラッパーを乱立させない」とは、追加の 2 つが「Player smoke と対になる分岐」と「2 フェーズ共通の処理」なので両立すると判断 |
| R1 F-03 | `CreateTelemetrySink` を public にする根拠が目的の改訂で消えた | 採用 | internal に（I6、§3.1、§2.3 に asmdef スライスへの問いとして残す）。R3 の保証 12 とも一致 |
| R1 F-04 / R2 F-04 | UICommon の source を先に組み立てると hook の呼び出し有無と検証順が変わる。loader を await 後に field へ入れると失敗時に GameObject が残る | 採用 | mode + `Func<string>`、await 前に field へ（§3.1、I4a、AC3 表 3・4、AC12） |
| R1 F-06 / R2 F-05 | `ActiveVfxRuntimeHost` の解除と set の Dispose の順、`_cts`・sink flag・`_services = null` の位置が未定 | 採用 | AC13 を書き直し、I3 に条件付き解除を明記 |
| R1 F-07 / R2 F-09 | `ContentDeclarationMismatch` が BuildContent の internal 型の一覧と AC5 から漏れている | 採用 | §1 を 9 型に、AC5 の正規表現を拡張（`ContentDirectoryConfiguration`、`AssetManagement.AssetManagement`、Content Directory 用 field も追加）。`CONTENT_DIRECTORY_STARTUP` の一覧も同じ訂正 |
| R1 F-08 / R2 F-06 | reflection の隠れた結合が `_assetManagement` 以外にもある（`_contentDirectorySession`、`_sceneDirector`）。`CONTENT_DIRECTORY_STARTUP` の field 統合が AC4 のテストを壊す | 採用 | §1、I1、§3.5、U5、AC1。`CONTENT_DIRECTORY_STARTUP` §2 に申し送り。触れる箇所の目録は §4 手順 6 で B の記録にする（受け入れ条件にはしない。結果に依存する文書を合否に使わないため） |
| R2 F-07 | AC6 が機械判定できない（HANDOFF 自身が diff に入る、`--stat` の省略、文言の矛盾、partial） | 採用 | AC6 を `--name-only`、`*.cs` に限った grep、public 型 0 の grep、行の分類の 3 段に |
| R2 F-08 / R1 F-11 | 新しい単体テストを作らない判断は失敗経路で空白。統合テストは StandaloneWindows64 以外で Ignore | 一部採用 | AC15（BeforeSceneLoad 途中失敗と telemetry sink の時点）、AC16（set の空・二重 Dispose）、AC4 に Passed を明記、判定必須に VFX の Play テスト群と `FlyControllerTeleportTests`、起点 filter に Rendering / Streaming。UICommon の component 欠落のテストは A2 では不採用にしたが、外部レビュー S2 で採用に改めた（AC15-3） |
| R2 F-10 | helper の try/catch と OCE の filter の位置 | 採用（修正して） | §4 手順 4。`CleanupAfterStartupFailureAsync` は現行の段ごとの try/catch を持つので「helper は try/catch を持たない」は smoke と Editor 初回 Scene の helper に限定 |
| R2 F-11 | 移したメソッド名を参照するコメントが Bootstrap の外に 3 箇所 | 採用 | §2.4 にコメントだけの追随を許可 |
| R1 F-09 | map の取得を initializer に残す理由が無い | 採用 | §3.3 (d)、loader の責務を寿命の側から書き直し |
| R1 F-10 / R4 | `LegacyRemoteCatalogProbe` は static class でよい | 採用 | §3.1、AC11 |
| R4 | 早期 return 2 つ（337・384 行目）が catch を通らずに失敗 telemetry を出す経路を残す | 採用 | §1、I4、AC3 表 3、§4 手順 4 |
| R4 | `[AppInit]` ログと stage literal の集合を前後で静的に照合する | 採用 | AC17（stage は AC2 が既に照合） |
| R4 | hook の呼び出し列を記録するテスト、hook ごとの fault injection で stage 名を確かめるテスト | 不採用 | AC3 の表 1・2 と AC15 で、壊れやすい箇所（逐次代入・stage の受け渡し・揃った後の失敗）は覆える。hook ごとの fault injection は hook 23 個分の harness になり、移動だけのスライスに対して重い。外部レビューも「全 hook への fault injection は不要」とした |
| R3（保証 4・5） | 起動契約が 1 か所で読める、分割後のメソッドが実行順に並ぶ | 採用 | §4 手順 4（helper を `InitializeAfterSceneLoad` の直後に呼ばれる順で置く） |
| R3（保証 8 / 作らないもの 5） | internal を public 化して参照 0 にしない | 採用 | R1 F-03 と同じ |
| R3（その他） | 振る舞い不変、新しい hook・interface・pipeline・facade を作らない、旧経路を消さない | 既に満たしている | I1〜I8、§2.3、§3.2 |

**外部レビュー（2026-11-07、owner が依頼。入力は A3 途中版、`develop` `7c9b364d` のコード。判定「方向性は支持。凍結前に Blocker 3 件と Should 3 件の処理を推奨」）**

| 指摘 | 内容 | 採否 | 反映先・理由 |
|---|---|---|---|
| B1 | UICommon の借用と所有を区別し、scene handle の解放権限を明記する | 採用 | §1「UICommon の所有」、I3、§3.1、AC3 表 4、AC12。コードで確認（530〜543 行目の借用経路は field に入れない） |
| B2 | stage の代入は 17 ではなく 18 箇所 | 採用 | §1「stage の代入点」、AC3 表 2。コードで確認 |
| B3 | AC15 の「未確認。初回確認は Phase C」が判定必須と矛盾 | 採用 | AC15 から免責を削り、組めなければ Phase A へ差し戻す（§4）。`FakeAssetBackend` が `OneStarMaker.Tests` にあることを確認した |
| S1 | `UseRequiredPlayerFileConfiguration` の評価時点も守る | 採用（遅延評価） | I4a、§3.1、AC12。getter の純粋さに頼る案は採らない（派生クラスの実装を知らないので owner の契約追加が要り、本スライスの範囲を超える） |
| S2 | UICommon の component 欠落は既存パターンで安くテストできる | 採用 | AC15-3。A2 で不採用にした判断（偽の Scene が要る）は誤りだった |
| S3 | サービスが揃った後の失敗を 1 件 fault injection する | 採用（AC15-2 に統合） | AC15-2 を「記録してから投げる」に変え、全 accessor・`ActiveVfxRuntimeHost`・`UpdateSystemRuntime.Coordinator` の null を確かめる。fake の VFX backend の Dispose 回数は数えない（`VfxRuntimeHost.Create` は null backend で未構成の backend を使うので、`ActiveVfxRuntimeHost` と accessor の null で破棄を観測できる） |

**外部レビュー 2 回目（2026-11-07、owner から受領）**

| 指摘 | 内容 | 採否 | 反映先・理由 |
|---|---|---|---|
| E2-1（重要） | AC1 の grep は複数行の宣言の引数を拾えない（`OnPlayerContentStartupFailedAsync`） | 採用 | AC1 を、宣言を連結してから比べるコマンドに変え（base で確認: 複数行の宣言が引数ごと 1 行になる）、判定 C に §3.5 の各メンバーの本文での見比べを必須にした。主担当の追加: 同じ理由で `PlayerContentStage` の値（修飾子を持たない）も AC1 に出ていなかったので、値の並びの比較を足した |
| E2-2 | S4 は scene handle の「破棄を所有」と書き、§3.1（参照を null にするだけ、解放は AM）と矛盾 | 採用 | S4 を §3.1 に合わせた |
| E2-3 | §1「本書の範囲との対応」が BuildContent 8 型のまま | 採用 | 9 型に訂正 |

**主担当の自己点検（Phase C / C' で突かれそうな点。2026-11-07）**

| 点 | 内容 | 反映先 |
|---|---|---|
| P1 | AC2 の「5 件」は実数 4 件、AC4 の「9 件」は `[TestCase]` を含め 13 件 | AC2、AC4（AC4 は「base と同じ件数」にした） |
| P2 | 起点 filter の `SampleGame.Tests.App` は存在しない namespace。`GameSceneLoggingTests` は `OneStarMaker.Tests.SampleGame` | §5 |
| P3 | AC6 の grep が `OneStarMaker/` 全体だと、新しいテスト fixture（public class）で必ず該当する | AC6 を `Scripts/` に限定 |
| P4 | `LegacyRemoteCatalogProbe` を static にすると `_config` を引数にする必要があり、「本文は変えない」と衝突する | AC11、§4 手順 1 に許す変更を明記 |
| P5 | 破棄の途中の例外の扱い（現行は try/catch 無し）を移動で「改善」すると挙動が変わる。逆に C' が「例外で後続が破棄されない」と指摘しうる | I3、AC13 に「try/catch を足さない」を明記（現行の挙動の保存が本スライスの問い） |
| P6 | AC15 のテストは PlayerLoop と `AppTelemetry` の static 状態を触るので、他のテストへの漏れを C' が突く | AC15 に TearDown での `BootstrapSubsystemRegistration` と `LogAssert` を明記 |
| P7 | §1 の順序に `ActiveVfxRuntimeHost` の設定と冒頭の `_cts` 検査が無く、AC3 の表 1 で照合されない | §1 |
| P8 | §3.1 の initializer の所有欄が static の probe を「所有」と書いていた | §3.1 |

初稿に無かった保証で R3 が挙げたもの（振る舞い・失敗境界・hook の不変、寿命の不変）は、R1・R2 の指摘を当てた I3 / I4 / I4a で本文に入った。初稿が作る要らないものは、R3 の観点では public の `CreateTelemetrySink` だけで、internal に改めた。目的欄の文言は変えていないので、目的レビューのやり直しは要らない。

## 6. Phase B 実装結果

- 実装（`6844bf35..33e0f27f`、§4 の手順ごとに 1 commit。各 commit で Unity Editor 6000.6.0f1 のコンパイル成功を確認）:
  - 手順 1 `bb046d29`: `LegacyRemoteCatalogProbe`（internal static）へ旧 5 メソッドを移した。変えたのは `_config` を引数 `AppConfig config` にしたこと、`static` を付けたこと、入口の名前を §3.1 の `TryLoadAsync` にしたこと、`DeriveBuildInfoUrl` を internal にしたこと（テストから呼ぶため）だけ。`LegacyRemoteCatalogProbeTests`（AC11）。
  - 手順 2 `690a82c9`: `BootstrapUiCommonLoader`（internal sealed）と `BootstrapUiCommonSource`（internal readonly struct）。
  - 手順 3 `9950bb30`: `DebugSocketService.CreateTelemetrySink()`（internal、`DebugSocketService.cs` の本体）と `FrameworkServiceSet`（internal sealed）。
  - 手順 4 `c37cadc0`: `InitializeAfterSceneLoad` の分割（`LoadEditorFirstSceneAsync`、`RunPlayerContentSmokeAsync`、`CleanupAfterStartupFailureAsync`、`FinishStartupSpan`、private nested `StartupProgress`）。
  - 手順 5 `936b10d1`: `BeforeSceneLoadServiceStagingTests`（AC15 の 3 件）、`FrameworkServiceSetTests`（AC16 の 2 件）。AC15 の harness は `OneStarMaker.Tests` の型（`FakeAssetBackend` と framework の型）だけで組めた（差し戻し条件に当たらない）。
  - `33e0f27f`: §2.4 のコメント追随。`Editor/Build/Variants/LocalRevisionEditorInjector.cs:19` の `AbstractApplicationInitializer.WarnOnRevisionMismatchAsync` を `LegacyRemoteCatalogProbe.WarnOnRevisionMismatchAsync` に。§2.4 の残り 2 箇所（`RemoteCatalogRuntimeBridge.cs:18`、`VfxRuntimeHost.cs:17`）は `AbstractApplicationInitializer` というクラス名と `ReleaseAll` を書いているだけで、移したメソッド名を含まず、内容も変更後に成り立つので変えていない。
- 機械検査の結果（head `33e0f27f`。base の値は `6844bf35` で同じコマンドを実行して取った）:
  - AC1-1: base 51 行、head 53 行。差は次の 2 行だけで、どちらも §4 手順 4 が求める private nested `StartupProgress` のメンバー（実効アクセスは private。§3.5 の公開面ではない）: `internal List<PlayerContentStage> PlayerContentStages { get; } = new()`、`internal string Stage { get; set; } = "load-ui-common"`。AC1 のコマンドは修飾子で始まる行を全て拾うので、private nested 型の internal メンバーも出る。§3.5 の 51 行は一致（判定 C の本文の見比べで確かめる）。
  - AC1-2: `PlayerContentStage` の 10 値の並びが一致。
  - AC1-3: `_sceneDirector`（`SceneDirector?`）、`_assetManagement`（`IAssetManagement?`）、`_contentDirectorySession`（`ContentDirectorySession?`）の 3 宣言。
  - AC2: 16 個が一致。`"load-editor-first-scene:"` は 1 行（`LoadEditorFirstSceneAsync`）。
  - AC5: `DebugSocketTelemetrySink` は Bootstrap folder で 0 行。Content Directory の正規表現は `AbstractApplicationInitializer.cs` の 1 ファイルだけ。
  - AC6: 1 は該当 0 行（`.asmdef` と `unity/Assets/SampleGame/` の変更なし）。2 は 0 行。3 も 0 行（`Scripts/` の追加行に `public` を含む行が無い）なので分類する行は無い。
  - AC7: `AppTelemetry.StartSpan(... AppStartup ...)` は 2 箇所（BeforeSceneLoad、AfterSceneLoad）。span を閉じる処理は `FinishStartupSpan(span, memBefore, success, stage)` に抽出し、BeforeSceneLoad は `"BeforeSceneLoad"`、AfterSceneLoad は `success ? "AfterSceneLoad" : progress.Stage` を渡す。`isSuccess: success`、`level: TelemetryLevel.Summary`、`tags: null`、`payload` は base と同じ。
  - AC8: `AbstractApplicationInitializer.cs` 1,046 行。`InitializeAfterSceneLoad` 130 行（286〜415 行目）。初版は 135 行で、`StartupProgress` を使う理由のコメントを同型の doc へ移し、2 つのコメント行をまとめ、空行を 1 つ除いて 130 行にした（コードは変えていない）。
  - AC11: `AbstractApplicationInitializer.cs` に `Addressables.LoadContentCatalogAsync`、`UnityWebRequest`、`RemoteCatalogRuntimeBridge`、`using UnityEngine.Networking` は無い。
  - AC12: 同ファイルに `SceneManager.GetActiveScene`、`EventSystem`、`InputSystemUIInputModule`、`GetComponent<UICommon>` は無い（コメントにも書いていない。`using UnityEngine.EventSystems` / `using UnityEngine.InputSystem.UI` も外した）。
  - AC13: 同ファイルに `_loggerFactory` / `_debugSocketService` / `_updateSystemHost` / `_vfxRuntimeHost` / `_debugSocketTelemetrySinkRegistered` は無い。サービスは `_services`（`FrameworkServiceSet?`）だけ。
  - AC17: base・head とも 28 個。差は 3 行で、いずれも補間の穴の式だけが変わり、文言は同じ: `AfterSceneLoad failed at stage '{startupStage}'` → `'{progress.Stage}'`、`Starting DebugSocket transport. mode={_debugSocketService.Options...}` → `{DebugSocket.Options...}`、`DebugSocket transport start sequence finished. mode={_debugSocketService.Options...}` → `{DebugSocket!.Options...}`。
  - `pwsh tools/contract-audit.ps1`: `errors=0 warnings=0`（checks 1〜9）。
- HANDOFF との差:
  - implementation base は `6844bf35`（§0「Phase B 開始の記録」の owner の決定）。§1・§3.5 の行番号は `d4861882` のまま有効（コードの差 0）。
  - AC11 の 3 ケースのうち「末尾 `/` あり」は、末尾が `/` の URL と、file 名で終わる URL の 2 つの `[TestCase]` にした（`/` を含む通常の catalog URL も固定するため）。新規テストの件数は AC11 が 4、AC15 が 3、AC16 が 2 の計 9 件で、AC14 の「base + 8 件以上」を満たす。
  - `FrameworkServiceSet.StartAsync` は await の後も `DebugSocket` プロパティを都度読む（base が field を都度読んでいたのと同じ。await 中に `ReleaseAll` が走ったときの挙動を変えないため）。
  - `InitializeAfterSceneLoad` の `await _services!.StartAsync(ct)` は、base の `_debugSocketService == null` の警告分岐を `_services` が null の場合には通らない。`_services` は BeforeSceneLoad の成功時に必ず作られ、`ReleaseAll` が `_cts` と同時に null にするので、直前の早期 return（`_assetManagement == null || _cts == null`）を通過した時点で null にならない（到達しない経路）。
  - UICommon の取得で、directory か否か（`_contentDirectoryConfiguration != null`）と `_assetManagement!` は `LoadAsync` の呼び出し時に読む（AC12 が許す field の読み取り）。virtual 3 つは関数で渡し、loader の中の base と同じ位置で評価する。
  - 新しい事実の分類: 上記はいずれも B 適応で、Phase A の差し戻し条件には当たらない。
  - 行数について owner の発言（2026-11-07、B の作業中）: 「行数は目安で絶対ではない。理由があれば超えてよく、コメントによる超過は人間判断で可」。現 head は AC8 を満たしているので凍結条件は変えていない。後の修正で超えた場合の扱いは、必要になった時点で決定記録にする。
- AC3 の対応表（head `33e0f27f`。行番号は `AbstractApplicationInitializer.cs`（AAI）、`FrameworkServiceSet.cs`（FSS）、`BootstrapUiCommonLoader.cs`（Loader））:

  **表 1 順序**

  | §1「現在の順序」の行 | head の位置 |
  |---|---|
  | BSL: CTS 生成 | AAI `InitializeBeforeSceneLoad` 228 |
  | BSL: `Application.quitting` 購読 | 231 |
  | BSL: `AssetManagement` 生成 | 234 |
  | BSL: `BuildConfig`（required Player なら baked もここ） | 237 |
  | BSL: `ReadContentDirectoryConfiguration` | 238 |
  | BSL: （directory なら）`SealAddressables` と `Debug.Log` | 239〜248 |
  | BSL: `FrameworkServiceSet` を空で作り `_services` へ入れる（追加の行。最初の `Create*` より前） | 252〜253 |
  | BSL: `CreateUpdateSystemHost` → set へ | 257 |
  | BSL: `AppTelemetry.Thresholds` | 260 |
  | BSL: `CreateDebugSocketService` → set へ | 265 |
  | BSL: `LogDebugSocketBootstrapStatus` | 266 |
  | BSL: `CreateLoggerFactory`（引数は set の DebugSocket の stream）→ set へ | 267 |
  | BSL: `VfxRuntimeHost.Create`（`CreateVfxBackend` をここで評価、logger と coordinator は set から）→ set へ | 271 |
  | BSL: `ActiveVfxRuntimeHost` の設定 | 272 |
  | BSL: `RegisterDefaultTelemetrySink`（base 実装は `_services?.LoggerFactory` を読む） | 274 |
  | BSL: span の終了 | finally 280 `FinishStartupSpan(..., "BeforeSceneLoad")` |
  | ASL: 冒頭の `_cts == null` で例外 | AAI `InitializeAfterSceneLoad` 296〜297 |
  | ASL: （directory かつ非 required）session 登録 → 直後に `WarnOnDeclarationMismatch` | 301〜305 → `InstallConfiguredContentDirectoryAsync` 573 |
  | ASL: UICommon 取得 | 307〜314（loader を 309 で field に入れてから 310 で await） |
  | ASL: `WaitForPanelReadyAsync` | 317 |
  | ASL: SceneResourceMap（Player / directory / Addressables） | 323〜327 |
  | ASL: 早期 return（`_assetManagement` か `_cts` が null） | 329〜333 |
  | ASL: `StartFrameworkServicesAsync` | 339 `_services!.StartAsync(ct)`（本文は FSS 46〜70） |
  | ASL: `OnServicesInitializing` | 343 |
  | ASL: `CreateSceneFactory` | 346 |
  | ASL: `ResolveSceneVariant` | 349 |
  | ASL: （directory かつ未登録）session 登録 | 352〜356 |
  | ASL: representation の上書き | 358〜359 |
  | ASL: `SceneDirector` 生成 | 364〜370 |
  | ASL: `BindSceneDirector` | 372 |
  | ASL: `BindSceneLifecycle`（VFX） | 374 |
  | ASL: 早期 return（`_sceneDirector` が null） | 376〜380 |
  | ASL: `RegisterAlreadyLoadedScenes` | 386 |
  | ASL: Editor の初回 Scene の `AddScene` と観測ログ 1 行 | 388〜390 → `LoadEditorFirstSceneAsync` 426〜430 |
  | ASL: Player content smoke の 6 段 | 391〜392 → `RunPlayerContentSmokeAsync` 439〜456 |
  | ASL: span の終了 | finally 413 `FinishStartupSpan(..., success ? "AfterSceneLoad" : progress.Stage)` |
  | RA: `_cts.Cancel` | AAI `ReleaseAll` 699 |
  | RA: `DisposeVfx` | 705〜709（`ActiveVfxRuntimeHost` が set の Vfx と `ReferenceEquals` のときだけ解除）→ 712 `services?.Dispose()` の先頭（FSS 83〜85。参照を外してから Dispose） |
  | RA: DebugSocket → sink の印の初期化 → UpdateSystemHost → LoggerFactory | FSS 87 → 89 → 91 → 94（`Dispose` の中。try/catch なし） |
  | RA: `_services = null` | 713 |
  | RA: CTS の Dispose | 715〜716 |
  | RA: `_bakedPlayerContent` / `_config` / `_contentDirectoryConfiguration` | 717、719、720 |
  | RA: `SceneDirector.Dispose` | 723〜724 |
  | RA: `AssetManagement.ReleaseAll`（+ `CompleteContentDirectoryPlayStop` / `BeginSynchronousShutdown`） | 728〜740 |
  | RA: map | 742 |
  | RA: UICommon GO → `_uiSceneHandle` → EventSystem GO | 746 `_uiCommonLoader?.Dispose()`（Loader 143〜157 の中でこの順）、747 で null |
  | RA: `AppTelemetry.Shutdown` | 750 |

  **表 2 stage**（代入先は全て 288 行目で起動ごとに作る `StartupProgress` の `Stage`。catch の `LogError`（403）、`CleanupAfterStartupFailureAsync` の 2 つの hook（505、507）、finally の telemetry（413）は同じインスタンスの現在値を読む）

  | base の代入点 | 値 | head のメソッドと行 | 直後の処理 |
  |---|---|---|---|
  | 296（初期値） | `load-ui-common` | `StartupProgress.Stage` の初期値 546 | 最初の代入より前の失敗（冒頭の `_cts == null` の例外など）で読まれる |
  | 313 | `register-content-directory`（非 required の先行登録） | `InitializeAfterSceneLoad` 303 | `InstallConfiguredContentDirectoryAsync` 304 |
  | 317 | `load-ui-common` | 307 | loader の生成 309、`LoadAsync` 310 |
  | 321 | `wait-ui-common-ready` | 316 | `WaitForPanelReadyAsync` 317 |
  | 326 | `load-scene-resource-map` | 321 | map の取得 323 |
  | 343 | `start-framework-services` | 338 | `_services!.StartAsync` 339 |
  | 347 | `initialize-app-services` | 342 | `OnServicesInitializing` 343 |
  | 350 | `create-scene-factory` | 345 | `CreateSceneFactory` 346 |
  | 353 | `resolve-scene-variant` | 348 | `ResolveSceneVariant` 349 |
  | 360 | `register-content-directory`（後段の登録） | 355 | `InstallConfiguredContentDirectoryAsync` 356 |
  | 368 | `create-scene-director` | 363 | `new SceneDirector` 364、Bind 372・374 |
  | 390 | `register-loaded-scenes` | 385 | `RegisterAlreadyLoadedScenes` 386 |
  | 400 | `"load-editor-first-scene:" + firstScene` | `LoadEditorFirstSceneAsync` 426 | `AddScene` 427 |
  | 408 | `load-player-first-scene` | `RunPlayerContentSmokeAsync` 439 | `AddScene` 440 |
  | 411 | `run-player-content-smoke` | 442 | `OnPlayerContentReadyAsync` 443、fixture の失敗の throw 445 |
  | 415 | `unload-player-first-scene` | 446 | `UnloadScene` 447 |
  | 418 | `close-player-content-directory` | 449 | 具象 AM の確認 450〜451、`CloseContentDirectoryAsync` 452 |
  | 424 | `complete-player-content-smoke` | 455 | `OnPlayerContentShutdownCompletedAsync` 456 |
  | 201（別の行） | `before-scene-load` | `BootstrapAfterSceneLoad` 198〜202 | base と同じ位置・条件（`_beforeSceneLoadSucceeded` が false で失敗を保持し、`ShouldNotifyPlayerContentFailure` が true のとき） |

  helper は stage を戻り値で返さない。helper の中で投げた例外は try/catch を持たない 2 つの helper（`LoadEditorFirstSceneAsync`、`RunPlayerContentSmokeAsync`）を素通りし、本体の catch がその helper が書いた stage を読む。

  **表 3 失敗経路**

  | 経路 | head |
  |---|---|
  | ASL catch 1: `LogError`（到達 stage） | AAI 403 |
  | catch 2: required Player で初回 Scene がロード済みなら unload（段ごとの try/catch） | `CleanupAfterStartupFailureAsync` 467〜482 |
  | catch 3: directory が登録済みなら close（段ごとの try/catch） | 483〜498 |
  | catch 4: `OnAfterSceneLoadInitializationFailed(stage, ex)` | 505（4・5 は 1 つの try/catch で、失敗は `LogError` だけ） |
  | catch 5: （required Player なら）`OnPlayerContentStartupFailedAsync(stage, ex, stages)` | 506〜507 |
  | catch 6: `ReleaseAll` | 本体の catch 408（helper の後） |
  | OCE の filter（非 required のときだけ成功扱い） | 396〜400（本体に 1 つ） |
  | 早期 return 1（`_assetManagement` か `_cts` が null。catch を通らず finally の telemetry だけ） | 329〜333 |
  | 早期 return 2（`_sceneDirector` が null） | 376〜380 |
  | BSL の途中失敗で作り済みのサービスを破棄 | `BootstrapBeforeSceneLoad` 172〜181 の catch の `ReleaseAll` → `_services` は最初の `Create*` より前に入っているので、作り済みのものだけが FSS `Dispose` で破棄される。AC15-1・2 が観測する |
  | UICommon の取得途中の失敗で GameObject を破棄 | loader を await より前に field へ入れる（309〜310）。Loader は root を component の検査より前に記録（125）→ 失敗で catch → `ReleaseAll` 746 の `_uiCommonLoader.Dispose` が root を `Destroy`。AC15-3 が観測する |

  **表 4 UICommon**（`BootstrapUiCommonLoader.LoadAsync`）

  | base の段 | head |
  |---|---|
  | ロード済み Scene の検査（借用経路はここで return） | Loader 75〜88 |
  | `UseRequiredPlayerFileConfiguration` の評価と拒否（同じメッセージ） | 90〜91（`source.UseRequiredPlayerFileConfiguration()`。ここで初めて評価） |
  | directory か否かの分岐 | 94（呼び出し時の bool） |
  | directory: `GetDirectoryBootstrapSceneLogicalKey` の評価、空なら例外（同じメッセージ）、`LoadContentSceneAsync` | 96〜103 |
  | Addressables: `GetUICommonPrefabAddress` の評価、`LoadSceneAsync` | 107〜116 |
  | handle と GameObject の記録 | handle 100 / 112、root なしの例外 120〜123、GameObject 125 |
  | component の検査（同じメッセージ） | 127〜132 |
  | `EnsureEventSystem` | 134（本体 160〜170。Play 中で `EventSystem.current` が無いときだけ作り、そのときだけ記録） |

  | 経路 | GameObject を破棄するか | handle を持つか | EventSystem を作りうるか |
  |---|---|---|---|
  | 借用（active Scene の UICommon） | しない（記録しない） | 持たない | 作らない（`EnsureEventSystem` を呼ばない） |
  | directory | する（`Dispose`） | 持つ（`Dispose` で null。unload しない） | 作りうる |
  | Addressables | する（`Dispose`） | 持つ（同上） | 作りうる |

  `_uiCommonLoader.Dispose` の呼び出しは AAI 746 で、AM の `ReleaseAll`（728〜740）と map の解放（742）の後。base の `_uiCommonObject` の `Destroy` と同じ位置。

- Content Directory の状態に触れるメソッドの一覧（`CONTENT_DIRECTORY_STARTUP` への入力。受け入れ条件ではない。head で `_contentDirectoryConfiguration` / `_contentDirectorySession` / `_bakedPlayerContent` を grep）: フィールド宣言 88〜90、`InitializeBeforeSceneLoad`（238、239）、`InitializeAfterSceneLoad`（301、311、325、352、353、358、359、360、388、391）、`LoadEditorFirstSceneAsync`（424、430）、`RunPlayerContentSmokeAsync`（440、447、453）、`CleanupAfterStartupFailureAsync`（468〜470、475、484、491）、`InstallConfiguredContentDirectoryAsync`（552、572）、`ReleaseAll`（717、720、731、737、740）、`BuildConfig`（653）、`ReadContentDirectoryConfiguration`（829、854）。新しい 3 クラスには無い（AC5）。
- U5 の再 grep（B 開始時）: `GetField(` / `GetMethod(` / `GetProperty(` / `nameof(` を `unity/Assets/SampleGame/Tests` と `unity/Assets/OneStarMaker/Tests` で `-A2` 付きで見た。`AbstractApplicationInitializer` を reflection で読むのは base と同じ `_assetManagement`（`InvalidCompanionConfigStartupTests`）、`_contentDirectorySession`・`_sceneDirector`（`BuildContentDirectoryIntegrationTests`）、`_sceneDirector`（`VfxScenePlayTests`、`LocalVfxEventPlayTests.Scenes`、`SeasonMajorEventPlayTests.Scenes`）だけで、増えていない。新規の `BeforeSceneLoadServiceStagingTests` も `_assetManagement` を同じ形で使う。
- §3.4 の harvest 先の確認: `20-variant-checkout-workflow.md` は旧経路をメソッド名 `TryLoadRemoteCatalogAsync` / `WarnOnRevisionMismatchAsync` で書いている（98、154、177、269 行目）。クラス名 `AbstractApplicationInitializer` は書いていないが、入口の名前は `TryLoadAsync` に変わったので、Phase D で 1 語の追随が要るかを harvest 担当が判断する（本スライスでは文書を変えない）。
- 未実行: Unity テスト（新規 9 件を含む）と全 EditMode 回帰は Phase C（本 B では実行していない。コンパイルだけ確認した）。動作確認（directory Play）も未実行で、次の担当が行う。`pwsh tools/docs-audit.ps1` は push 前の公開点で実行する。
- implementation head commit: §0 の欄を正とする。
- Phase B 担当・ベンダー: Claude Code（Anthropic）、Claude Opus 5.5（owner の申告。2026-11-07）。

### 動作確認

- 対象 head: implementation head `33e0f27f`（開始時の HEAD は B の出口の記録 commit `0366c6c6`。`33e0f27f..0366c6c6` の差分は本書だけであることを確かめた）。実施日 2026-11-07。
- 条件ごとの実行担当・環境・実行したコマンド: Editor の directory Play（§5 の 1 条件）。担当は動作確認の session（Claude Code）、ローカル Windows。実行したのは前提の確認だけ: `tools/unity-editor.cmd status`（起動中の Editor なし）、Delivery の設定と cache root の中身の確認。Editor は起動していない。
- 生の観測（Console ログの順序、例外の有無）と保存先・状態: なし（Play を行っていない）。
- 検証経路の期待と違う観測と、B への差し戻し: なし（観測が無いので差し戻さない）。
- `未実施` の条件と理由・担当: **Editor の directory Play が `未実施`。** 理由: 現 head の content と対応する install が手元に無い。既定の cache root の `installed/spring-full` は空で、Delivery の設定と `artifacts/` に残る publish 済み source・install はいずれも 2026-09 のもの（BS5a・BS5b-2・S-4 の変更より前）で、現 head の SceneResourceMap と対応する保証が無い。現 head で用意するには `Tools/OSM/Content/Verify Play/<宣言名>` の Content Directory build → publish → install が要り、Build は動作確認の対象外（Phase C の作業。§0 の旧票の指示どおり）。取得の担当: 判定 C の担当（判定 C を完了と記録する前に GO 候補 head で行う。§5 の観測項目をそのまま使う）。

### PR レビュー 1 巡目の修正（B 適応、2026-11-08）

- 入力: PR #166 のレビュー（owner の独立レビュー、cursor[bot] の 2 件。いずれも旧 head `33e0f27f` を対象にした静的な比較）。阻害 1 件で、3 件とも同じ指摘。
- 指摘（I4 / I4a）: `InitializeBeforeSceneLoad` は hook の戻り値を局所変数の `services` にだけ入れていた。hook の中で `ReleaseAll` が走ると（派生クラスが hook の中で `BootstrapSubsystemRegistration(this)` を呼ぶ等）、set は破棄されて `_services` から外れるが、hook はその後も戻り値を返し、BeforeSceneLoad は続く。戻り値は外れた set に残り、次の hook の accessor からも、後の `ReleaseAll` からも見えない。例: `CreateUpdateSystemHost` の中で `ReleaseAll` してから host を返すと、ReleaseAll が config を null に戻すので続く閾値の読み込みが投げ、catch の `ReleaseAll` が走っても host は破棄されず、PlayerLoop の登録が残る。base は戻り値を field へ直接代入していたので、次の hook から見え、後の `ReleaseAll` が破棄した。違反するのは I4（作り済みの常駐サービスを `ReleaseAll` が破棄する）と I4a（戻り値が次の hook より前に accessor から見える）。発見 C・判定 C・C' 1 巡目はこの経路を見ていない（AC15 のテストは hook が投げて戻らない場合だけを見ていた）。
- 分類: B 適応。責務の配置・公開面・AC13 の破棄順は変えない。
- 変更:
  - `AbstractApplicationInitializer.cs`: private の `KeepServicesAttached(FrameworkServiceSet)`（`_services ??= services`）を足し、4 つの代入（`UpdateSystemHost`、`DebugSocket`、`LoggerFactory`、`Vfx`）それぞれの直後で呼ぶ。外れていたら同じ set を field に戻す。破棄済みの set のメンバーは null なので、base で field が null に戻っていたのと同じ状態から戻り値だけが入る。`_services` を別の set に替えるのは `InitializeBeforeSceneLoad` の冒頭だけなので、ここで見る値は null か同じ set に限る。読み出し（`services.DebugSocket?.RealtimeStream`、`VfxRuntimeHost.Create` の引数）は同じ set を読むので、base の field の読み出しと同じ値になる。`CreateVfxBackend` の中で `ReleaseAll` が走った場合は、base と同じく `UpdateSystemHost!.Coordinator` で投げる。ファイルは 1,062 行（AC8 の上限 1,080 行以内）。`InitializeAfterSceneLoad` は変えていない。
  - `BeforeSceneLoadServiceStagingTests.cs` に 2 件を足した: `ReleaseAllInsideUpdateSystemHostHook_TheReturnedHostIsStillReleased`（hook の中で `ReleaseAll` してから host を返す。後の catch の `ReleaseAll` で `UpdateSystemRuntime.Coordinator` が null に戻る）と、`ReleaseAllInsideLoggerFactoryHook_TheNextHookSeesTheReturnedFactoryAndItIsReleased`（hook の中で `ReleaseAll` してから logger factory を返す。次の VFX の hook が accessor で読め、後の `ReleaseAll` で null に戻る）。修正前のコードでは、どちらも assertion で失敗するはず（実行はしていない）。
- 確認: 生成済みの `OneStarMaker.Tests.csproj` を `dotnet build` でビルドし、エラー 0（Editor でのコンパイルは確認していない）。`pwsh tools/contract-audit.ps1` は違反なし。Unity テストは実行していない（Phase C の担当）。
- base の取り直し: PR の作業中に `develop` の `9abd2ee6` が本ブランチへマージされた（`feaf8343`）。`git diff 33e0f27f feaf8343 -- unity/` と `git diff 6844bf35 9abd2ee6 -- unity/` は同じで、マージは本スライスのコードを変えていない。新しい head の実装差分は base `9abd2ee6` で固定する（`unity/` の差分は旧 head の 15 file に今回の修正を足したもの）。
- 担当・モデル: Claude Code（Anthropic）、Claude Opus 5.5（Phase B と同じモデル。進行役の session が owner の指示で修正した）。

## 7. Phase C

### 発見 C（1 回目、2026-11-07）

- 種別: 発見 C。GO 判定はしていない。C' は起動していない。
- 開始時の照合: 起動指示の SHA `048cd53e` と worktree の HEAD が一致。implementation head `33e0f27f` は HEAD の祖先で、`33e0f27f..048cd53e` の差分は本書だけ。base `6844bf35` は head の祖先。PR は無い。作業ツリーに本スライスの外の未追跡 file（`docs/handoff/` の他スライスの HANDOFF 4 つ）があり、触れていない。
- 使用した evidence bundle の取得案内・状態: base `6844bf35` / head `33e0f27f` の完全 diff・stat・name-status、AC1 / AC2 / AC5 / AC7 / AC8 / AC17 のコマンドの base と head の出力、AC6 と hard gate の出力、contract-audit の結果、下のテストの XML と log。`evidence stage` でローカル固定済み（未昇格）。取得案内は主担当の作業領域にあり、次の担当には起動指示で渡す。task は `runtime-refactor-bootstrap`。
- 構造適合（§3.1 との照合、§3.3 の行数警報の判定）:
  - 3 クラスのメンバー・可視性・所有は §3.1 と一致。`LegacyRemoteCatalogProbe`（internal static、`TryLoadAsync(AppConfig)` と internal `DeriveBuildInfoUrl`、他は private static）、`BootstrapUiCommonLoader`（internal sealed、`LoadAsync(IAssetManagement, BootstrapUiCommonSource)` と `Dispose`）と `BootstrapUiCommonSource`（internal readonly struct、bool と `Func<bool>` と `Func<string>` 2 つ）、`FrameworkServiceSet`（internal sealed、引数なし ctor、4 つの getter / setter、`StartAsync`、`Dispose`）。新しい interface・抽象・static 可変状態は無い（I8）。namespace は `OneStarMaker.Runtime` のまま、asmdef の変更は無い。
  - `DebugSocketService.CreateTelemetrySink()` は internal で本体 file にあり、signature に `ZLogger*` 型は出ていない。
  - 3 クラスは各 1 責務。initializer に残るのは「順序 + hook + 失敗境界」と、`CONTENT_DIRECTORY_STARTUP` が外す Content Directory 起動。行数警報は **非分割妥当**（§3.3 の判断どおり。1,046 行の残りは公開面の宣言・順序・Content Directory 起動で、これ以上分けるには hook の再設計が要る）。
  - base と head のコードを突き合わせ、移動で意味が変わった箇所が無いことを確かめた: BeforeSceneLoad の逐次代入（set を最初の `Create*` より前に入れる、`VfxRuntimeHost.Create` の引数の評価順）、`ReleaseAll` の VFX 解除と set の `Dispose` の順と例外時の扱い（`Dispose` の途中で投げると `_services = null` に届かず、次の `ReleaseAll` が残りを破棄する。base の field と同じ）、`StartAsync` の await 後の都度読み、UICommon の 3 経路と virtual の評価時点、`CleanupAfterStartupFailureAsync` の本文（base の catch と同一）、`FinishStartupSpan` の引数。
  - Unity の偽 null: 新しい file の `?.` / `??` は `FrameworkServiceSet` の 4 サービス（いずれも `UnityEngine.Object` ではない）と `BootstrapUiCommonSource` の `Func` だけ。GameObject と `EventSystem.current` は `== null` / `!= null`。
- 受け入れ条件の照合（機械の補助は base と head で同じコマンドを実行して比べた。判定 C の必須の本文照合（AC1）は判定 C が行う）:
  - AC1: 1 は base の 51 行が head に全て同じ字面で残り、増えたのは private nested `StartupProgress` の internal メンバー 2 行だけ（§4 手順 4 が求める型。§3.5 の公開面ではない）。2 は 10 値の並びが一致。3 は同じ 3 宣言。
  - AC2: 16 個が一致、動的 stage は 1 行。AC3: §6 の 4 つの表の全行を head の行番号と照合し、全て一致（対応の無い行・順序の入れ替わりは無い）。AC5: 0 行と 1 ファイル。AC6: 1・2・3 とも 0 行。AC7: 2 箇所、`FinishStartupSpan` に渡す値は base と同じ。AC8: 1,046 行、`InitializeAfterSceneLoad` は 286〜415 行目の 130 行。AC11: 禁止の識別子は無い。AC12・AC13: 禁止の識別子・field は無く、順序は表どおり。AC17: 28 個、差は補間の穴の式の 3 行だけで文言は同じ。
  - contract-audit: `errors=0 warnings=0`（checks 1〜9）。hard gate: 追加・変更した `.cs` は全て先頭が `#nullable enable`、`record` 型は無い（一致したのは引数名 `record` だけ）、テストに `Task.Delay` / `Thread.Sleep` は無い。
- 現在の問いを阻害する findings: なし（B への差し戻しは無い）
- 阻害しない観察（凍結済み条件・常時契約への違反根拠を持たない。判定 C と owner の参考）:
  - **O1 AC11 の「変えてよいもの」と §3.1 の名前。** AC11 は移動で変えてよいのを「`_config` を引数にすること」と「`static`」に限るが、B は入口の名前を `TryLoadRemoteCatalogAsync` から `TryLoadAsync` に、`DeriveBuildInfoUrl` を private から internal にした。どちらも凍結済みの §3.1 の公開面（`TryLoadAsync(AppConfig config)`、`string DeriveBuildInfoUrl(string)`）と AC11 自身のテスト要求に従ったもので、§6 に記録がある。凍結条件の間の読み方であり違反ではない。
  - **O2 `ReleaseAll` が `_uiCommonLoader` を null に戻す（確認待ち）。** base は UICommon の GameObject を initializer の field に持っていたので、UICommon のロードを await している間に `ReleaseAll` が走り（Editor の Play 停止で `Application.quitting`）、その後にロードが完了して root が記録された場合、後で走る `ReleaseAll` がその root を破棄しえた。head は 1 回目の `ReleaseAll` で loader を外すので、後から記録された root は外れた loader に残る。差が出るには、ロード中の `ReleaseAll` の後もロードが完了し、さらに同じ instance でもう一度 `ReleaseAll` が走る必要がある（例: component 欠落で catch に入る）。release 済みの AssetManagement でロードが完了するか、Play 停止で Unity が Scene の object を先に解体するかを確かめておらず、到達できるかは未確認。I3 が求める解放順と複数回呼び出しの安全性は保たれ、AC3 の表 3（取得途中の失敗で GameObject を破棄する経路）も満たすので、blocker にしない。判定 C か owner が base と完全に一致させたいなら、修正は `ReleaseAll` の `_uiCommonLoader = null;` の 1 行を消すことで済む（loader の `Dispose` は 2 回呼んでも安全）。
  - **O3 AC15-3 は同期完了に依存する。** `await _initializer.Failed.Task` は、`OnAfterSceneLoadInitializationFailed` の中の `TrySetResult` より前に `RunAfter` が全て同期で終わることを前提にする（`FakeAssetBackend` の Scene ロードは `UniTask.FromResult`）。非同期になると、assertion が `ReleaseAll` より前に走りうる。既存の `InvalidCompanionConfigStartupTests` と同じ形なので、本スライスの欠陥にはしない。下のテストでは Passed。
  - **O4 §5 の起点 filter と AC4 の名前に、存在しない namespace・テストがある。** `SampleGame.Tests.Bootstrap` と `SampleGame.Tests.Editor.Build` は存在しない（`unity/Assets/SampleGame/Tests/Bootstrap/` のテストは namespace `OneStarMaker.Tests.Bootstrap`、`BuildContentDirectoryIntegrationTests` は `OneStarMaker.Tests.Editor.Build`）。`GameSceneLoggingTests` はコードに無く、`05-scene.md` に名前があるだけ。AC4 の `SampleGame.Tests.Editor.Build.BuildContentDirectoryIntegrationTests` も fullname としては誤り。判定必須は空 filter の全件回帰なので収録は変わらないが、判定 C は XML をクラス名（`OneStarMaker.Tests.Editor.Build.BuildContentDirectoryIntegrationTests`）で確かめる。凍結条件の合否は変わらない（文言の誤記）。
  - **O5 判定 C は `-WithGraphics` で実行する。** 下の起点 filter の実行は `-nographics`（`run-tests.ps1` の既定）で、VFX の実 backend を使う 25 件が `No graphics device (GraphicsDeviceType.Null)` で Inconclusive になり、step は `failed`（Inconclusive あり、Unity の exit code 2）になった。§5 は VFX の Play テスト群が Passed であることを求め、Skipped / Ignored を不合格とするので、判定必須の全件回帰は `pwsh tools/run-tests.ps1 -WithGraphics`（空 filter、StandaloneWindows64 の Editor）で取る。
- 後続スライスへ移送する findings: なし（O2 を base と一致させる修正は B 適応で済み、後続の問いにはならない）
- 実行したテストコマンドと `-Filter`、対象を選んだ理由: `pwsh -NoProfile -File tools/run-tests.ps1 -Filter "OneStarMaker.Tests.BuildContent;OneStarMaker.Tests.Bootstrap;SampleGame.Tests.Bootstrap;SampleGame.Tests.Editor.Build;OneStarMaker.Tests.SampleGame;SampleGame.Tests.Rendering;SampleGame.Tests.Streaming"`（EditMode、`-nographics`、sandbox 外、Editor は閉じた状態）。§5 の起点 filter をそのまま使った。B は Unity テストを 1 件も実行していないので、新規 9 件と、AC2 / AC4 と起動経路の既存テストが head で通るかを確かめるため。Content Directory build を伴う `BuildContentDirectoryIntegrationTests` は O4 の namespace の誤りで filter に入らず、重い統合なので発見 C では追加実行していない（判定 C の全件回帰に含まれる）。
- テスト結果: 315 件。Passed 290、Failed 0、Inconclusive 25（全て O5 の graphics device 無し、VFX の実 backend）、Skipped 0。新規 `BeforeSceneLoadServiceStagingTests` 3、`FrameworkServiceSetTests` 2、`LegacyRemoteCatalogProbeTests` 4 は全て Passed。`InvalidCompanionConfigStartupTests` 4、`PlayerContentConfigurationTests` 13（base と同じ件数）、`FlyControllerTeleportTests` 1 は Passed。`VfxScenePlayTests` は 3 Passed / 2 Inconclusive。step の status は `failed`（理由は Inconclusive と exit code 2 だけ）。
- 判定必須のうち未実行: 全 EditMode 回帰（空 filter、`-WithGraphics`、StandaloneWindows64）と、その XML での AC4 の `BuildContentDirectoryIntegrationTests` の Passed、VFX の Play テスト群の Passed。§6「動作確認」の Editor の directory Play（`未実施` のまま。判定 C を完了と記録する前に GO 候補 head で取得する）。AC1 の §3.5 の各メンバーの本文での見比べ（判定 C の必須。機械の補助は上のとおり一致）。
- 未確認事項: O2 の到達可否。
- 担当・モデル: Claude Code（Anthropic）、Claude Opus 5.5。
- 独立性: **「Phase C は Phase B と異なるモデル」を満たしていない。** owner によれば Phase B も Claude Opus 5.5（2026-11-07、発見 C の記録の後に owner が伝えた）。**owner の判断（2026-11-07）: `独立性制約あり` として本記録を発見 C に受け入れ、やり直さない。** 判定 C と C' は Claude Opus 5.5 以外のモデルで行う。

### 判定 C（1 回目、2026-11-07）

- 種別: 判定 C。**状態: 判定 C 未完了。** AC4 の `BuildContentDirectoryIntegrationTests` が XML 上 Passed にならず、判定必須が揃わない。C' は起動せず、判定 bundle は作らない。判断は owner に返す（下の「owner への判断」）。
- 開始時の照合: 起動指示の SHA `307a8c2c` と HEAD が一致。implementation head `33e0f27f` は HEAD の祖先で、`33e0f27f..307a8c2c` の差分は本書だけ（`unity/` の差分 0）。作業ツリーに本スライスの外の未追跡 file（`docs/handoff/` の他スライス 4 つ）があり、触れていない。GO 候補 head を `33e0f27f` とし、ローカルの軽量 tag `evidence/runtime-refactor-bootstrap/1` で固定した（push しない）。
- 機械検査（base `6844bf35` と head `33e0f27f` で同じコマンドを自分で実行して比べた。発見 C の bundle は使っていない）:
  - AC1-1: base 51 行、head 53 行。差は `StartupProgress` の `PlayerContentStages` と `Stage` の 2 行だけ（private nested のメンバー）。AC1-2: 10 値の並びが一致。AC1-3: 3 宣言が同じ型。AC2: 16 個が一致、`"load-editor-first-scene:"` は 1 行。AC5: `DebugSocketTelemetrySink` 0 行、Content Directory の正規表現は `AbstractApplicationInitializer.cs` の 1 ファイルだけ。AC6: 1・2・3 とも 0 行。AC7: `StartSpan` 2 箇所、`FinishStartupSpan` の引数は `"BeforeSceneLoad"` と `success ? "AfterSceneLoad" : progress.Stage`。AC8: 1,046 行（`InitializeAfterSceneLoad` 130 行は B の記録）。AC17: 28 個、差の 3 行は補間の穴の式だけ。`#if` は base の 3 組のうち 2 組が `LegacyRemoteCatalogProbe` へ移り、1 組（`ResolveSceneVariant`）が残る。属性の差は無い。
- **AC1 の本文照合（必須）:** §3.5 の各メンバーを、head の `AbstractApplicationInitializer.cs` の宣言と base で 1 つずつ見比べた。protected static 4（`BootstrapSubsystemRegistration` / `BootstrapBeforeSceneLoad` / `BootstrapAfterSceneLoad` / `AppendFixtureStages(List<PlayerContentStage>, IReadOnlyList<PlayerContentStage>)`）、accessor 6、abstract 4、virtual 19（`OnPlayerContentStartupFailedAsync(string, Exception, IReadOnlyList<PlayerContentStage>)` の複数行の引数を含む）、nested 2（`enum PlayerContentStage` と `sealed class PlayerContentFixtureResult` はともにクラス直下で、`StartupProgress` の中ではない。公開メンバー 3 つ（`CompletedStages`、`Failure`、ctor）も同じ）、internal static 4（`ShouldNotifyPlayerContentFailure(bool)`、`RunsPlayerContentSmoke(bool)`、`ReadEditorFirstScene(AppConfig)`、`ActiveVfxRuntimeHost { get; private set; }`）は、修飾子・`static`・型・引数名・既定値が同じ。accessor の本文だけが変わったのは `Vfx`（`_vfxRuntimeHost?.System` → `_services?.Vfx?.System`）、`LoggerFactory`（`_loggerFactory` → `_services?.LoggerFactory`）、`UpdateCoordinator`（`_updateSystemHost?.Coordinator` → `_services?.UpdateSystemHost?.Coordinator`）で、合格例に当たる。`UseRequiredPlayerFileConfiguration` は getter のまま。private の `_assetManagement`（`IAssetManagement?`）、`_contentDirectorySession`（`ContentDirectorySession?`）、`_sceneDirector`（`SceneDirector?`）の名前と型も同じ。不一致なし。
- 構造・コードの照合: §6 の AC3 の 4 つの表のうち、`ReleaseAll`（`ActiveVfxRuntimeHost` の条件付き解除 → `FrameworkServiceSet.Dispose` の内部順 → `_services = null` → CTS → … → `_uiCommonLoader.Dispose` が AM の `ReleaseAll` と map の解放の後）、BeforeSceneLoad（空の set を最初の `Create*` より前に作り、各戻り値を次の hook の前に set へ渡す）、`FrameworkServiceSet.StartAsync`（await 後に `DebugSocket` を都度読む）を、base のコードと突き合わせて確かめた。意味の違いは見つからなかった。
- 判定必須テスト: `pwsh tools/run-tests.ps1 -WithGraphics`（空 filter、EditMode、sandbox 外、Editor は閉じた状態）。結果 XML（`TestResults/f7339a73-…/results.xml`、1,653 件、Passed 1,652、Failed 1、Inconclusive 0、Skipped 0、Ignored 0）。新規 `BeforeSceneLoadServiceStagingTests` 3、`FrameworkServiceSetTests` 2、`LegacyRemoteCatalogProbeTests` 4、`InvalidCompanionConfigStartupTests` 4、`PlayerContentConfigurationTests` 13（base と同じ件数）、`FlyControllerTeleportTests` 1、`VfxScenePlayTests` 5、`LocalVfxEventPlayTests` 5、`SeasonMajorEventPlayTests` 6 は全て Passed。
- **Failed の 1 件:** `OneStarMaker.Tests.Editor.Build.BuildContentDirectoryIntegrationTests.ScenePrefabTextureAndTwoRepresentationsBuildAndMoveAsOneDirectory`。失敗は `Timeout value of 360000 ms was exceeded`（スタックは `BuildPipeline.BuildContentDirectoryInternal`、つまり Unity 本体の Content Directory build の中。テストコードの 236〜239 行目）。切り分け:
  1. 全件回帰の中で 668 秒で失敗。
  2. 同じ head の単独実行（`-Filter OneStarMaker.Tests.Editor.Build.BuildContentDirectoryIntegrationTests`、`-WithGraphics`）でも 363 秒で同じ失敗。
  3. **base に相当するコード**（`develop` の `9abd2ee6`。`6844bf35` との差は `unity/` で 10 file・11 行で、ASSET_CYCLE の型の移動（`AssetBuildContracts` folder への `AssetType` と `ContentDirectoryException` の移動）と文書だけ。Bootstrap と BuildContent の build 処理は同じ）の worktree で同じ単独実行をしても 362 秒で同じ失敗。head の変更が原因ではない。
  4. 直近の合格記録は 2026-10-18 の 137 秒（この PC の `TestResults`）。以後 BS5a・BS5b-2 などで build が重くなったと見られるが、原因は調べていない。
  この Failed は head の変更に起因しないが、AC4 は XML 上の **Passed** を求める（Skipped / Ignored だけでなく Failed も不合格）。凍結条件の合否を変える裁定になるので、決定記録ではなく owner の判断が要る。
- Unity 終了時の stall: 全件回帰の Unity は結果 XML 完成後に終了せず（`step.json` が書かれない。保持 handle が未 signal のまま）、自分で起動した runner を終了させた。これは `tools/Harness/README.md` の既知の native stall と同じ症状で、規則どおり追加調査はしていない。この run は `step.json` が無く、XML と `unity.log` だけが原記録（強制終了した取得は成功証拠にしない）。単独 2 回の runner は終了し `step.json` がある（status `failed`、理由は Failed の 1 件と Unity の終了コード 2）。
- **動作確認（directory Play、GO 候補 head `33e0f27f`、取得担当は判定 C の session）:** 実施した。手順: Editor を起動 → `VerifyPlayLauncher.VerifyAsync(spring-full, interactive:false)` を reflection で呼ぶ（Content Directory の build → publish → install → bridge → Play）。1 回目は開発者ローカルの Variant profile 選択（`UserSettings`、git 管理外）が有効で `Rejected at Start` になったので、選択を一時的に空にして再実行し、終了後に元の GUID へ戻した。観測（Console、`directory-play-console.txt` として共有 notes folder に保存）: `[VerifyPlay] installed …`（install の revision は現 head の build）→ `Play requested` → `[AppInit] Content directory mode: Addressables backend sealed …` → `DebugSocket configured` → `AfterSceneLoad: loading UICommon.` → `loading SceneResourceMap.` → `Starting DebugSocket transport` → `DebugSocket transport start sequence finished` → `Editor content directory first scene: scene=Title … stable=True`。期待の順序（§5）どおり。Play 中の Exception はなし（DebugStudio サーバー未起動による `[DebugSocket] Failed to connect` の Warning のみ）。停止後（`isPlaying=false`）に Exception や `Cannot find handle for scene` は出なかった。`Verify Play` の 2 回目の content build は問題なく完走した（`BuildContentDirectoryIntegrationTests` のタイムアウトとは別に）。環境の副産物は `artifacts/bs4/players/installed/` の install 1 件。
- O2（`ReleaseAll` が `_uiCommonLoader` を null に戻す）: **現 head のままとする（B へ戻さない）。** 理由: 差が出るには、UICommon のロード中に `ReleaseAll` が走り、ロードが完了した後で同じ instance にもう一度 `ReleaseAll` が走る必要がある。ロード中の `ReleaseAll` は `_cts.Cancel` を伴うので、ロードは通常 `OperationCanceledException` で終わり、非 required では成功扱いで 2 回目の `ReleaseAll` は走らない（base も同じ）。I3 の解放順・複数回呼び出しの安全性・AC3 表 3 は満たす。head を動かすと全件回帰をやり直すことになり、凍結条件への違反根拠も無い。観察として残す。
- 阻害する findings: なし（凍結済み条件のうち、head の変更に起因する違反は見つけていない）。ただし AC4 の合格条件が未達（上の Failed の 1 件。head に起因しない）。
- 後続スライスへ移送する findings: `BuildContentDirectoryIntegrationTests` の 360 秒タイムアウト（content build の所要時間が `[Timeout(360000)]` を超える。`develop` でも再現）。所有: Content Directory の build とそのテストを持つスライス（`CONTENT_DIRECTORY_STARTUP` か BuildSystem 側）。本スライスでは直さない（timeout の延長は修復と扱わない）。
- 判定必須のうち未解消: AC4 の `BuildContentDirectoryIntegrationTests` が XML 上 Passed であること。
- **owner への判断:** (a) AC4 の読み方を決める。head に起因しない既存の環境依存の Failed を、(i) 決定記録で「base でも失敗する既存の欠陥で、本スライスの合否に使わない」と確定する（AC4 の合否の読みが変わるので、AC4 の凍結条件の意味を変える裁定にあたるかを owner が判断し、あたるなら Phase A の新しい revision として AC4 を置き換える）か、(ii) この PC の content build を速くするなどして、先に `develop` で Passed を取れる状態にし、head で再取得するか。(b) どちらにせよ head は動かさないので、(ii) で再取得する場合も GO 候補 head は `33e0f27f` のまま、判定 C を再開できる。(i) で進める場合、判定 C は残りが全て揃っているので、決定記録の後に判定 bundle を作って C' へ進める。
- 担当・モデル: Claude Code（Anthropic）、Claude Sonnet 5.5（発見 C・Phase B の Claude Opus 5.5 とは別モデル）。

#### 判定 C の完了と owner の例外（2026-11-08）

- **owner の裁定:** owner は上の「owner への判断」の (i) を選び、「決定記録で受け入れて」「C'' に進めて」と指示した（2026-11-08）。結論: `BuildContentDirectoryIntegrationTests` の Failed（360 秒の Timeout。`develop` の base 相当でも同じ）を、本スライスの AC4 の合否に使わない。理由: 失敗は Unity 本体の Content Directory build の所要時間が `[Timeout(360000)]` を超えることで、head の変更に起因せず（`develop` の `9abd2ee6` でも 362 秒で同じ失敗）、本スライスの問い（initializer の起動経路が変わっていないこと）は他の証拠（AC4 の `PlayerContentConfigurationTests` 13 件 Passed、directory Play、機械検査、AC1 の本文照合）で判定できる。timeout の延長は修復と扱わない。この Failed は後続スライスへ移送する（上の「後続スライスへ移送する findings」）。
- **扱いの明記:** この裁定は AC4 の合否の読みを変える（Failed を不合格にしない）。skill の決定記録の規則は、凍結条件の合否を変える読みを決定記録で行わず Phase A の新しい revision とするので、本件は決定記録ではなく **owner が明示した例外承認** として扱い、Phase A は再開しない。適用範囲はこの 1 テスト・この head `33e0f27f` に限る。AC4 のもう一方（`PlayerContentConfigurationTests`、XML 上 Passed、件数は base と同じ 13）と、判定必須の他の全て（全 EditMode の他 1,652 件の Passed）の要求は変えない。C'' は、この例外が不変条件を破ること、または凍結条件の意味を許容できない形で変えることを示せる場合に限り、異議を blocker にできる（blind bundle の README に同趣旨を載せた。裁定の理由・判定 C の所見は載せていない）。
- **判定 C の結論:** 完了。阻害する findings なし。判定必須は、AC4 の上の 1 件を例外承認で除き、GO 候補 head `33e0f27f` で全て揃った。GO 判定は C'' と突き合わせる Phase D まで行わない。
- 次: C'' 1 巡目（blind audit）。bundle は §0 の「C'' blind bundle」の欄。収録は、凍結した Phase A（base の本書）、Phase B の結果の記録時点の本書（`0366c6c6`、所見なし）、base..head の完全 diff、AC1 / 2 / 5 / 6 / 7 / 8 / 17 と contract-audit の生出力、全件回帰・head 単独・`develop` 単独の XML と log、directory Play の Console 記録、README（例外の 1 段落のみ）。
### 判定 C（2 回目、2026-11-08、新 head）

- 種別: 判定 C（§0 の owner の例外で縮めた形）。**状態: 完了。** 阻害する findings なし。GO 判定は Phase D まで行わない。
- 開始時の照合: 起動指示の SHA `45214bee` と HEAD が一致。implementation head `e8eca726` は HEAD の祖先で、`e8eca726..HEAD` の差分は本書だけ。作業ツリーに本スライスの外の未追跡 file（`docs/handoff/` の他スライス 4 つ）があり、触れていない。GO 候補 head を `e8eca726` とし、ローカルの軽量 tag `evidence/runtime-refactor-bootstrap/2` で固定した（push しない。`/1` は旧 head のまま）。base は `9abd2ee6`。
- 修正差分の照読（`git diff 33e0f27f e8eca726 -- unity/` のうち修正に当たる 2 file）: `AbstractApplicationInitializer.cs` は private `KeepServicesAttached(FrameworkServiceSet)`（`_services ??= services`）を足し、`UpdateSystemHost`・`DebugSocket`・`LoggerFactory`・`Vfx` の 4 代入の直後それぞれで呼ぶだけ。I4（hook の中で `ReleaseAll` が走っても戻り値が後の `ReleaseAll` で破棄される）と I4a（戻り値が次の hook の前に accessor から見える）の経路は、外れた set を同じ set で戻すので base の field 代入と同じ状態になる。`_services` を別の set に替えるのは冒頭だけ、という主張はコードで成り立つ。正常経路では `_services` が非 null なので `??=` は何もしない（通常の起動経路の値は変わらない）。公開面・AC13 の破棄順・`InitializeAfterSceneLoad` は不変。コメントは理由（hook 内 `ReleaseAll` と base との同一性）を書いていて、処理の言い換えではない。`BeforeSceneLoadServiceStagingTests` の新規 2 件は、それぞれ UpdateSystemHost と LoggerFactory の hook の中で `ReleaseAll` してから戻る経路を見て、後の解放と次の hook からの可視性を assertion している。`unity/` の他の差分は `git diff --stat` で ASSET_CYCLE の型移動・文書・README の範囲と確認した（本文は読んでいない。修正ではない）。
- 判定必須テスト: `pwsh tools/run-tests.ps1 -WithGraphics`（空 filter、EditMode、sandbox 外の承認済み経路、新 head、Editor は閉じた状態から。結果 XML は `TestResults/a319b7db-…/results.xml`）。**1,655 件、Passed 1,655、Failed 0、Inconclusive 0、Skipped 0**（旧 head の 1,653 件 + 新規 2 件）。新規の `BeforeSceneLoadServiceStagingTests` は 5 件（旧 3 + 今回の 2: `ReleaseAllInsideUpdateSystemHostHook_TheReturnedHostIsStillReleased`、`ReleaseAllInsideLoggerFactoryHook_TheNextHookSeesTheReturnedFactoryAndItIsReleased`）全て Passed、`FrameworkServiceSetTests` 2、`LegacyRemoteCatalogProbeTests` 4、`InvalidCompanionConfigStartupTests` 4、`PlayerContentConfigurationTests` 13（AC4）、`VfxScenePlayTests` 5 も Passed。**`BuildContentDirectoryIntegrationTests` は今回 Passed**（225 秒。旧 head では 360 秒の Timeout だった。原因は調べていない。環境の負荷差の可能性があるが未確認）。このため owner の AC4 の例外は使っていない。
- owner の決定（進行役 session 経由、2026-11-08）: AC4 の Timeout の例外承認を新 head `e8eca726` にも適用する。範囲は §7「判定 C の完了と owner の例外」と同じ（その 1 テストの Timeout による Failed を AC4 の合否に使わない）。今回は Passed なので結果に影響しない。
- Unity 終了時の stall: XML 完成（01:34）後に Unity が終了せず、`step.json` が書かれなかった。既知の native stall と同じ症状で、規則どおり追加調査はしていない。この run の原記録は XML と `unity.log` だけで、強制終了した取得は成功証拠にしない。XML は完成しており（`test-run` が `result="Passed"`、`end-time` あり）、判定は XML で行った。Unity プロセスは未終了のまま残っている（前回の同症状の PID と、別 worktree の `BuildContentDirectoryIntegrationTests` の runner も残っている。強制終了は自動モードの分類器に拒否されたので行っていない。owner の手元で終了してよい）。
- 取り直さなかったもの（owner の例外どおり）: AC1 の本文照合、機械検査（AC1 / 2 / 5 / 6 / 7 / 8 / 17）、contract-audit、directory Play、判定 bundle、C' 用 blind bundle。修正は公開面と通常の起動経路の値を変えないので旧 head `33e0f27f` の記録を使う。C' はやり直さない。
- 後続スライスへ移送する findings: なし（旧 head の Timeout の件は `CONTENT_DIRECTORY_STARTUP` か BuildSystem 側への移送のまま。今回の Passed を踏まえた再評価はそちらで）。
- 未確認事項: 新 head の directory Play（例外で取り直さない）。
- 担当・モデル: Claude Code（Anthropic）、Claude Sonnet 5.5（Phase B と修正の Claude Opus 5.5 とは別モデル）。

## 8. Phase C'

- 担当方式: AI（新規 session）。blind audit bundle だけを入力にした。
- 種別と巡: 1 巡目。高コスト証拠は「なし」（§5）なので、この 1 巡が C' の最終。
- 受け取った決定記録の版: なし（C' 用決定記録は 2 巡目以降に渡す）。
- 使用した blind audit bundle の取得案内・状態: C' blind bundle（§0 の欄）を取得案内どおりに `evidence fetch` で取得し、`status: passed`・`reasonCode: complete` で展開した。収録は bundle の README に従う。
- 確認範囲・方法:
  - 完全 diff（base `6844bf35` → head `33e0f27f`、15 file）を全行読んだ。
  - 凍結 Phase A の I1〜I8、AC1〜AC17、§2.4 の常時契約と照合した。
  - bundle の機械検査の生出力（AC1 / 2 / 5 / 6 / 7 / 8 / 17、contract-audit）を base と head で比べた。
  - XML（全件回帰、head 単独、develop 単独）を集計した。
  - head の `AbstractApplicationInitializer.cs` を読み、`ReleaseAll` の順序、`InitializeAfterSceneLoad` の長さ（286〜415 行の 130 行）、AfterSceneLoad の catch 連鎖、早期 return の guard、BeforeSceneLoad の失敗経路を確かめた。
  - 新規 3 クラスと `DebugSocketService.CreateTelemetrySink` の本体を読んだ。
  - Unity の再実行と Editor のコンパイルはしていない（必須ではない）。
- 判定: **AI 監査の結論は「未解決の阻害 blocker 1 件あり」。PASS とは記録しない。** B-1（AC4）の扱いは人間の裁定が要る。
- 現在の問いを阻害する findings（違反根拠つき）:
  - **B-1（AC4 の失敗）。** `BuildContentDirectoryIntegrationTests.ScenePrefabTextureAndTwoRepresentationsBuildAndMoveAsOneDirectory` が全件回帰 XML で `Failed`（Timeout 360000 ms）。全 1653 件のうち Failed はこの 1 件だけ。head 単独と develop 単独の XML でも同じ Timeout で Failed。
    - 違反根拠: AC4 は「XML 上 Passed」を要求し、判定定義は「一つでも未達なら NO-GO」（§2）。
    - 例外について: bundle の README にある owner の例外（2026-11-08、AC4 の Failed を合否に使わない）は、AC4 の合否を変える裁定である。§2 の「A3 後の例外承認: なし」と、phases-and-handoff の「凍結条件の意味・合否を変える裁定は決定記録で行わず、Phase A を新しい revision として再開する」に照らすと、この形では満たさない。置き換える条件・期限・検証予算も書かれていない。
    - 一次証拠: 全件回帰 `results.xml`（Failed 1 件）、head 単独の `step.json`（XML root が Passed でない）、develop 単独の `results.xml`。
    - 注記: B の記録では、この失敗は head に起因せず develop でも再現する。つまり凍結 AC4 は現在の develop 上で満たせない。これは head の不具合ではなく、Phase A を新しい revision にするか、AC4 の読み替えを人間が決めるべき事項である。
- 後続スライスへ移送する findings・所見（阻害しない）:
  - **N-1（AC1 の機械照合の差）。** head の AC1-1 は base の 51 行に対し 53 行。増えた 2 行は private nested の `StartupProgress` の `internal` メンバー（`PlayerContentStages`、`Stage`）である。§3.5 の公開面（protected / internal の一覧）の宣言は変わっていない。diff で変わった listed member の本体は `Vfx`、`LoggerFactory`、`UpdateCoordinator` の accessor 本体だけで、AC1 が許すものである。
    - 文言の読みの問題。I1 は違反していないので blocker にはしない。ただし、文字どおりの AC1-1 は不合格になるため、読み方は人間が決める（決定記録で確定するには合否を変える読みになる）。
  - **N-2（AC17）。** 差の 3 行は補間式だけが変わり、文言は同じ。
  - **N-3（nit）。** `LegacyRemoteCatalogProbe.cs` の `config?.GetString`（非 nullable の引数に対する `?.`）。契約違反ではない。
- 確認できた不変条件（違反なし）: I1（§3.5 の宣言は変わらず、private フィールド名と型は保持）、I2（stage 16 個と動的 stage、順序）、I3（`ReleaseAll` 順序。二重呼び出しで安全）、I4（catch 連鎖、OCE の filter、早期 return 2 つ、BeforeSceneLoad の失敗経路）、I4a（作り済みサービスを次の hook で読める。UICommon の virtual は現行の時点で評価）、I5（Bootstrap に DebugSocket internal 型の識別子なし）、I6（asmdef と SampleGame の変更なし、新しい public 面なし）、I7（span 2 つ、payload の stage）、I8（新しい static なし）、S1（1,046 行、`InitializeAfterSceneLoad` 130 行）、S3〜S5、S7（全件回帰は 1653 件中 1652 Passed。base 1644 → +9 で AC14 の +8 以上を満たす）。`#nullable enable` と `record` なし、`Task.Delay` なし、Unity の破棄判定は `== null` / `!= null`。
  - 補足: `_services!.StartAsync(ct)`（AAI 339 行付近）は、null になりえない。`ReleaseAll` が `_services` と `_cts` を同じ同期処理で null にし、その前の早期 return が `_cts == null` を見るため。
  - PlayerContentConfigurationTests は XML 上 13 件 Passed。
- 残存リスク:
  - AC4 の Timeout の原因は未調査（調査しない指示どおり）。develop 上の既存の失敗として扱うかは人間の判断。
  - 強化条件（異なるベンダー）は満たさない（後述）。
- 監査できなかった範囲:
  - §3.5 の全メンバーを本文で一つずつ見比べてはいない（diff の変更は上記 3 件の accessor だけと確認した）。
  - AC3 の §6 対応表は B の自己申告。C' は主要行（`ReleaseAll` 順序、早期 return、catch 連鎖、BeforeSceneLoad の失敗、UICommon の所有）だけを head の実コードと照合した。全行は未照合。
  - directory Play は Console 記録を読んだだけで、再実行していない。
- 独立性:
  - 最低条件（AI）: 満たす。新規 session、blind bundle のみ、モデルは B（Claude Opus 5.5、owner 申告）・発見 C（同）・判定 C（Claude Sonnet 5.5）のいずれとも異なる。
  - 強化条件（異なるモデル系列・ベンダー）: 満たさない。B・C と同じ Anthropic 系の製品を使った。`独立性制約あり`（強化条件）として記録し、採否は人間が決める。
  - 発見 C / 判定 C 結論の事前閲覧: 結論・指摘は読んでいない。ただし HANDOFF を検索した際に、§7 のモデル名を含む数行が表示された。監査判断には使っていない。モデル名は独立性の判定にだけ使った。
  - bundle の README には、判定 C の指摘に関する owner の例外（人間の裁定）が含まれていた。盲検 bundle の規則（裁定は含めない）から外れるので、Phase D で確認してほしい。C' は、AC4 の判定をこの例外に頼らず、一次証拠で行った。
  - 設計・実装への関与: なし。
- 発見 C / 判定 C 結論の事前閲覧・設計実装への関与: 上記「独立性」に記録。
- 担当・モデル: Claude Code（Anthropic）の新規 session。モデルは Claude Haiku 5.5（claude-haiku-5-5）。

## 9. Phase D

- C / C' の突合（旧 head `33e0f27f`）: 判定 C は阻害する指摘なし、C' 1 巡目の阻害指摘は AC4 の `BuildContentDirectoryIntegrationTests` の Failed 1 件（owner の例外承認が AC4 の合否を変えるので Phase A の新 revision が要る、という異議）。**owner の裁定（2026-11-08）: 例外承認を維持する。** Phase A は再開しない。理由は §7「判定 C の完了と owner の例外」のとおり（head に起因せず `develop` でも再現、本スライスの問いは他の証拠で判定できる、後続スライスへ移送）。C' の他の確認と AC1 の private nested 2 行（読み方の判断として移送）は §8 のとおり。C' は Haiku 5.5 で行った（高リスクの独立監査としては弱い点は owner が承知で選択）。
- C / C' の突合（新 head `e8eca726`、2026-11-08）: 旧 head の後に PR #166 のレビュー（owner と cursor[bot]）が I4 / I4a の阻害指摘を出し、B 適応で直した（§6「PR レビュー 1 巡目の修正」）。この経路は発見 C・判定 C・C' 1 巡目のどれも見ていなかった。owner の例外（§0）で、新 head の確認は縮めた判定 C（§7「判定 C（2 回目）」、全 EditMode 1,655 件 Passed、阻害なし）と PR の再レビュー（cursor[bot]: 阻害なし。owner: 指摘なし）で行い、C' はやり直していない。AC4 の例外は新 head にも適用すると owner が決めたが、判定 C（2 回目）で Passed だったので使っていない。
- マージ判断: owner が「指摘無し」「PhaseD凍結としてマージして後始末して」と指示した（2026-11-08）。PR #166 を `develop` へマージする。
- 必要な Evidence の保存・取得・必須内容の閲覧確認: 旧 head `33e0f27f` の発見 C bundle と C' 1 巡目の blind bundle はローカル固定済み（判定 C が `fetch` の往復を確認し、C' が `status: passed` で取得した）。新 head の判定 C（2 回目）の生証拠（`results.xml` と `unity.log`）を Phase D で `evidence stage` に固定し（task `runtime-refactor-bootstrap`、base `9abd2ee6` / head `e8eca726`、`locality: local`）、別 process の `fetch` で `results.xml`（1,655 件 Passed）を閲覧した。判定 bundle・新 head の C' 用 blind bundle は owner の例外で作っていない。directory Play の Console 記録は旧 head のもの（共有 notes folder）。`evidence schedule status` は `passed`。
- 取得案内の引渡し確認: 3 つの取得案内は主担当の作業領域（git 管理外の evidence-guides）にある。新しい案内は Phase D で追加した。昇格（R2）はしていない（Cloud・別 host の読み手はいない）。
- harvest: `04-app-startup.md` §4.2（initializer と 3 つの部品、hook の戻り値を set へ入れる契約）と §4.3（`ReleaseAll` の図の `FrameworkServiceSet.Dispose` と `BootstrapUiCommonLoader.Dispose`）。旧メソッド名の追随: `20-variant-checkout-workflow.md` の 3 行と `STREAMING_CURRENT_SPEC.md` の 1 行を `LegacyRemoteCatalogProbe.TryLoadAsync` にした。後続へ移送する findings（AC4 の Timeout と負荷の観察、initializer の行番号の取り直し）は `RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md` §2 へ移した。
- 削除確認: 本書は harvest と同じ commit で削除する。`docs/README.md`、`ASSET_BUILDSYSTEM_INDEPENDENCE.md`、`RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md` のリンクは文字列と「PR #166 で完了・削除」にした。
- 通常の task 終了・後始末の状態と未完了事項: マージ後に `workflow-task.ps1 end -Reason completed`（Decision は owner の指示から作る chat 形式）を記録し、`develop` へ切替、マージ済みブランチと evidence tag を整理する。結果は PR と進行役の報告に書く（本書は削除済みのため）。
