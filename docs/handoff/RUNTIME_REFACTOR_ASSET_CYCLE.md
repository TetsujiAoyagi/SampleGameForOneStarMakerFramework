# AssetManagement と BuildSystem 本体の共通契約の切り出し

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。

- type: `slice`
- status: `C`（revision 0、A3 凍結済み（owner の指示、2026-11-07）。Phase B 完了（2026-11-07）。発見 C 完了（2026-11-07、阻害する findings なし）。**判定 C 完了**（2026-11-07）。次は C'）
- revision の経緯:
  - 2026-10-31 A1 初稿（旧名 `RUNTIME_REFACTOR_CYCLES.md`）は循環 2 件（AssetManagement ⇄ BuildContent、CameraSystem ⇄ Streaming）を扱い、A2 入力版を commit `93723a4c` で切った。
  - 2026-11-07 前半、A2 と外部レビューの後、Camera 側を別スライスへ切り出し、目的を「AssetManagement を BuildContent から独立した assembly にできる向き（例外型 2 つを AssetManagement へ移す）」に絞った書き直し版を作った（未 commit）。
  - 2026-11-07 後半、owner が上位目的を「AssetManagement 単独・BuildSystem 単独・OSM での併用」に改めた。program の A0 [ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)（以下「A0」）がこれを受けて構成案 3（共通契約の切り出し）を推奨し、owner が決定した（A0 §6 D1〜D5）。前半の書き直し版（案 1）は撤回し、本書は A0 に基づく A1 改訂版である。初回の凍結前に改めたので revision は 0 のままで、revision 0 として A3 凍結した（2026-11-07）。
- branch: 計画は `claude/runtime-refactor-stage0-plan`（マージ済み）。実装は `claude/runtime-refactor-asset-cycle`（Phase B 開始時に `develop` の head から切った）
- implementation base commit: `3e8bf06c9bdc866e99365e22a1465983664e0a4a`（B 開始時の `develop` の head。計画 PR #161 のマージ commit で、2026-11-07 に fetch して確認）。A1 の実測に使った `d4861882` からの差は `docs/` だけ（計画の文書）
- implementation head commit: `08177c39a3f17e56355e667420b1e10256b0b0bb`
- risk: `normal`（owner 裁定 2026-11-07 のまま。根拠は変更の性質だけで判定する: public 型 3 つのファイルの置き場所を変えるが、型の完全名・宣言・所属 assembly（`OneStarMaker.Runtime`）は変えない。実行時の分岐・serialized データ・寿命は変えない。旧版の根拠だった namespace 変更による互換の破壊は無くなったが、A3 で owner が `normal` のままと決めた（§5 A3 採否 Q1））
- owner: root（依頼者）。A2 / B / C / C' の担当は Phase ごとに選ぶ
- created: 2026-10-31（A1 初稿）、2026-11-07 A1 改訂
- expires: 2026-11-30 または置換 revision の成立時。期限時は owner が更新・廃止を判断する
- harvest to: `unity/Assets/Docs/Architecture/27-folder-structure.md` §2.2（AssetManagement と BuildSystem 本体は互いを参照せず、共通契約 `Runtime/AssetBuildContracts/` だけを共有する。両方を知るのは連携部分）と `unity/Assets/README.md` のフォルダ構成図（`Runtime/` 配下に `AssetBuildContracts/` の 1 行）。新しい Architecture 文書は作らない
- 関連: program の A0 [ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)（本書は A0 §6 D4 の順序の (1)）。後続は `BUILDSYSTEM_STANDALONE_ENTRY`（未作成）、[RUNTIME_REFACTOR_BOOTSTRAP.md](RUNTIME_REFACTOR_BOOTSTRAP.md) の S2、asmdef 分割スライス（未作成）。Camera ⇄ Streaming の循環は `RUNTIME_REFACTOR_CAMERA_FOCUS`（未作成）で、本書は扱わない
- Phase A snapshot の取得案内・状態: A3 凍結 snapshot（凍結版の本書と、A3 凍結 script `p-check.ps1`）を `evidence stage` で固定した。ローカル固定済み（未昇格）。所在 task は `runtime-refactor-asset-cycle`、snapshot の base は `d4861882`、head は計画 PR の最終 commit `4da676bc`（マージ commit `3e8bf06c` と tree が同一）。reference と期待 packageSha256 は本書に書かず、Phase B の起動指示で渡した（B が 2026-11-07 に fetch し、`p-check.ps1` の content sha256 が §5 の `94291d8f…` と、本書が `3e8bf06c` の本書と一致することを確認）。発見 C・判定 C・C' にも同じ reference と packageSha256 を起動指示で渡す。A2 は同一 PC 上の写しを content sha256 で固定して渡した（§5 A2 記録）。A2 入力版の commit は切っていない（理由は §5 A2 記録）
- Phase B result snapshot の取得案内・状態: 未生成（stage していない。B が実行した P 検査・ビルド・contract-audit の出力は判定の入力にしないので（§6）、発見 C・判定 C は head で自分で取り直す）
- evidence bundle の取得案内・状態: 判定 C の判定 evidence（base `3e8bf06c` / head `08177c39`）を `evidence stage` で固定した。ローカル固定済み（未昇格）。所在 task は `runtime-refactor-asset-cycle`。判定 evidence を取った head にはローカル tag `evidence/runtime-refactor-asset-cycle/1` を付けた（push しない）。reference と packageSha256 は本書に書かず、進行役の session に渡した。別 process の fetch で読み戻せることを確認済み
- C' blind bundle の取得案内・状態: 判定 evidence と同じ生証拠に、Phase B の実装結果（implementation head の版の §6）を加えたものを `evidence stage` で固定した。ローカル固定済み（未昇格）。発見 C・判定 C の所見・結論は含まない。取得案内は判定 evidence の取得案内とは分けて、進行役の session に渡した
- 引継ぎ票:
  - 次の Phase・役割: C'（1 巡目、独立監査）。判定 C は完了した（§7）。高コスト証拠は無いので、この head の C' が最終 C' になる。
  - 次の作業: C' blind bundle と Phase A snapshot だけを入力に、implementation head 欄の SHA を独立に監査し、§8 に記録する。§7 は読まない。
  - 引き継ぐ commit: この票を含む commit（判定 C の記録 commit）。SHA は起動指示で渡す。implementation head は欄の SHA で、これとは別の commit。
  - 取得案内の所在: C' blind bundle と Phase A snapshot の reference・packageSha256 は、進行役が起動指示で渡す（同一 PC。`pwsh tools/artifacts.ps1 fetch --profile osm --config <v2-config> --reference <reference> --sha256 <packageSha256>`。Release build 済みの worktree から、sandbox の外で実行する）。
  - 必要な権限: Artifact Storage の fetch（ローカル）。同じテストを再実行する義務は無い。Unity を起動する場合は sandbox の外で実行し、既知の native 終了 stall は `tools/Harness/README.md` の停止規則に従う。
  - 停止規則: 独立性は §5 で予約したとおり `独立性制約あり`（risk `normal` なので強化条件の対象外）。B・C と異なるモデルで、新しい session で開始する。満たせなければ C' を未実施として人間へ返す。PR はまだ無い。PR 作成・push は C' の完了後、owner の指示を待つ。
  - 引き渡さないもの: §7（発見 C・判定 C の所見と結論）、判定 evidence の取得案内、進行役が受け取った所見。

### PR 状態ブロック

```text
状態: 判定 C の 完了
Phase A: revision 0、凍結版は計画 PR #161（マージ commit 3e8bf06c）
implementation base / head: 3e8bf06c / 08177c39
引き継ぐ commit: <PR 作成時に記録>（票を含む commit）
次の担当: C'。次の作業: C' blind bundle と Phase A snapshot だけを入力に、implementation head を独立に監査する
HANDOFF: docs/handoff/RUNTIME_REFACTOR_ASSET_CYCLE.md
Evidence 取得案内: Phase A snapshot・判定 evidence・C' blind bundle はローカル固定済み（未昇格）（証拠の取得はローカル担当）。Phase B result snapshot は未生成
未決事項: なし（Q1〜Q3 回答済み）
```

## 1. 目的と対象外

- 目的: AssetManagement と BuildSystem をそれぞれ単独で、また OSMFramework の中で併用して使えるようにする取り組みの最初の段として、AssetManagement（AssetDescriptions を含む）と BuildSystem の Content Directory 中核が、互いの型を参照しない状態にする。両者が共有する型は、どちらの本体にも属さない小さな共通契約として置く。公開 API・実行時挙動・シリアライズされたデータは変えない。
- 利用者: OSMFramework の開発者（このリポジトリで AssetManagement・BuildSystem を変更する人）。このスライスに利用者の操作は無い。
- 目的・利用者の文言の改訂（A1）:
  - 元の文言: 目的「AssetManagement（AssetDescriptions を含む）を、BuildContent から独立した assembly に切り出せる依存の向きにする。具体的には、AssetManagement から BuildContent への唯一の逆向きの辺（例外型 2 つ）を、型の宣言内容・実行時挙動を変えずに取り除く。asmdef はこのスライスでは作らない。」利用者の欄は無かった（規則の導入前の A1）。
  - 変更理由: owner が上位目的を改めた（2026-11-07。A0 §1）。旧目的は AssetManagement の単独利用にしか近づかず、BuildSystem の単独利用を遠ざける（A0 §3.2 案 1）。
  - 人間の承認: owner（root）が 2026-11-07 に A0 §6 D1（案 3 を採る）を決め、本書の A1〜A3（凍結前まで）を指示した。上の目的の文言そのものは、A2 の目的レビューの後に A3 で owner が確定した（2026-11-07、§5 A3 採否 Q2）。
- 対象外（所有する後続）:
  - BuildSystem 単独の public 入口（Materialization snapshot を SceneResourceMap 無しで作る入口、成果物を読む・検証する入口）と出力先・target の固定の解消: `BUILDSYSTEM_STANDALONE_ENTRY`（未作成。A0 §4.3 X6・X7）
  - Bootstrap の Content Directory 起動ポリシーを facade に集めること: [RUNTIME_REFACTOR_BOOTSTRAP.md](RUNTIME_REFACTOR_BOOTSTRAP.md) S2
  - asmdef の追加・変更、`InternalsVisibleTo` の変更、連携部分（`ContentDirectorySession`、`UnityContentDirectoryBackend`）と OSM 固有部分（`PlayerContentConfiguration` 等）の folder 移動、連携部分が越える internal 13 件の扱い、`BuildContentRoot` を別 assembly へ移すときのシリアライズ互換: asmdef 分割スライス（未作成）
  - namespace の変更（folder と namespace の不一致の解消）: asmdef 分割スライス。owner 決定 A0 §6 D2 で本スライスは据え置く
  - 例外型・enum の分割（A0 案 2）、`ContentDirectoryFailureCode` の値の追加・削除
  - CameraSystem ⇄ Streaming の循環: `RUNTIME_REFACTOR_CAMERA_FOCUS`（未作成）
- 現況（base `d4861882` の実測。A0 §2 の要約と追加の確認）:
  - AssetManagement → BuildSystem 本体の型の辺は、`ContentDirectoryException` / `ContentDirectoryFailureCode`（宣言 `Runtime/BuildContent/ContentDirectoryException.cs`、32 行）だけ。使うのは `Runtime/AssetManagement/AssetManagement.ContentDirectory.cs`（6 箇所）。`Runtime/AssetManagement/IAssetManagement.cs` と `Runtime/AssetManagement/Internal/IContentDirectoryBackend.cs` の `using OneStarMaker.Runtime.BuildContent;` は型を使わない未使用の `using`。
  - BuildSystem 本体 → AssetManagement の型の辺は、`AssetType`（宣言 `Runtime/AssetManagement/Abstractions/AssetType.cs`、16 行）だけ。使うのは `Runtime/BuildContent/BuildContentRoot.cs`（4 箇所、`ScriptableObject` の serialized field `_category` を含む）と Editor の `Editor/Build/Content/UnityContentDirectoryAdapter.cs`。`Runtime/BuildContent/ContentDirectoryIndex.cs` の `using OneStarMaker.Runtime.AssetManagement;` は未使用の `using`。
  - 3 型はすべて public。`AssetType` は AssetManagement 自身も使う（`AssetKey.Type`、キャッシュの分類）。
  - P 検査（§5）で、base では U-A・U-B の集合がこの 3 型の欠落だけで失敗し、3 型を足すと成功することを確かめた。
  - 対象の folder（`Runtime/AssetDescriptions`、`Runtime/AssetManagement`、`Runtime/BuildContent`、`Editor/Build/Selection`、`Editor/Build/Materialization`、`Editor/Build/Content`）に `#if` は無い。`Runtime/AssetManagement`・`Runtime/BuildContent`・`Runtime/AssetDescriptions` に拡張メソッドは無い。
  - 3 型を文字列で扱う箇所（`"AssetType"`、`nameof`、`typeof` を使った型名の比較、`Type.GetType`）は `unity/Assets` に無い。`ContentDirectoryException.cs` と `AssetType.cs` は MonoBehaviour / ScriptableObject を含まないので、script GUID で参照されることは無い。
  - **Unity の外で移動対象の path を使う箇所が 1 つある:** `tools/ContentDeliveryProbe/ContentDeliveryProbe.csproj` が `Runtime/BuildContent/ContentDirectoryException.cs` を `<Compile Include>` で path 指定してコンパイルする（.NET 8 の配布検証用の probe）。この project は CI（`.github/workflows/offline-checks.yml` の `dotnet-offline-tests`）に入っていないので、path が切れても CI は気づかない。base ではビルドが通り、ファイルを移すだけだと `CS2001`（ソースが見つからない）で失敗し、`Include` の path を 1 行直すと通ることを、リポジトリ外の一時コピーで確かめた。他の tools・CI・テストに、移動する 2 ファイルの path を使う箇所は無い（`git grep`。`artifacts/` の過去の記録は対象外）。
  - **移動する 2 つの `.meta` は 2 行だけの最小形式**（`fileFormatVersion` と `guid` だけで、`MonoImporter:` 節が無い。`ContentDirectoryException.cs.meta` は末尾改行も無い）。Unity は移動後の再 import で `.meta` に importer 節の既定値を書き足すことがあり、どうなるかは Unity を動かすまで分からない（未確認。Phase B の Editor 操作で初めて分かる）。
- UC ごとの利用者の入力: なし（利用者の操作は無い）。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: AssetManagement と BuildSystem の Content Directory 中核の間の型の辺を、公開 API・実行時挙動・シリアライズを変えずに、両方向とも 0 にできるか（コンパイラで示せるか）。
- 判定定義: §2.1 の不変条件と成立条件を §2.2 の受け入れ条件で全て確認できれば GO。一つでも未達なら NO-GO。スパイクではないので CONDITIONAL ACCEPT は使わない。
- このスライスの成果の範囲: GO は「本体同士の型の辺が無くなった」ことだけを意味する。AssetManagement・BuildSystem を単独で使えることの達成は主張しない（A0 §5.2 段階 4 が program の完了判定）。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし（A3 後に作る場合は本書と別の記録にする）

### 2.1 進める最低条件

**不変条件（起きてはいけないこと）**

- **I1 公開契約の保持:** 3 型（`ContentDirectoryException`、`ContentDirectoryFailureCode`、`AssetType`）の完全名（namespace と型名）、所属 assembly（`OneStarMaker.Runtime`）、修飾子、基底型、コンストラクタ引数、プロパティ、enum メンバーの名前・順序・値、XML doc が変わらない。
  - 崩しうる経路: (a) 移動の途中で本文を編集する。(b) 移動先の folder に `.asmdef` / `.asmref` があるか、`OneStarMaker.Runtime.asmdef` の配下の外へ置くと、所属 assembly が変わり、`Type.AssemblyQualifiedName` とバイナリ互換が変わる。(c) `.meta` を伴わずに移し、Unity が別の asset として取り込み直す。
  - 探索した境界: `Runtime/` 配下の asmdef は `OneStarMaker.Runtime.asmdef` の 1 つだけで、`.asmref` は無い。未確認: なし。
- **I2 挙動の保持:** 実行される処理が変わらない。`.cs` の変更は、2 ファイルの移動と、未使用の `using` 3 行の削除だけ。
  - 崩しうる経路: (a) `using` の削除で、コンパイルエラーにならずに名前の結び付き先が変わる。(b) 移動の便乗で他の行を変える。(c) `.cs` 以外（asset、設定、テスト）を便乗で変える。
  - 探索した境界: 一般論ではなく、削除する 3 箇所について確かめた。3 ファイルは、削除する `using` の namespace（`OneStarMaker.Runtime.BuildContent` / `OneStarMaker.Runtime.AssetManagement`）の型・拡張メソッドを 1 つも使わない（A1 の simulated head で、3 行を消した後も全集合がコンパイルでき、消した namespace の型名は 3 ファイルに現れない）。両 namespace に拡張メソッドは無く、3 ファイルに `using` 別名（`using X = …`）も無い。経路 (b)(c) は AC2 の変更集合の許可リストで塞ぐ。未確認: なし。
- **I3 シリアライズの保持:** 既存の asset・Scene・生成済み Content Directory が、移動前と同じ型と値に読める。
  - 崩しうる経路: (a) `.meta` を作り直して GUID が変わる。(b) `BuildContentRoot._category` の型 `AssetType` の値が変わる。(c) `BuildContentRoot.cs` 自体を移動・改名して、script の assembly 名・型名・GUID が変わる（生成済み Content Directory が script を解決できなくなる）。
  - 探索した境界: `BuildContentRoot.cs` は移動しない。enum の値は I1 で保持。移動する 2 ファイルは script asset を含まない。未確認: なし。
- **I4 テストの意味の保持:** 既存テストの assertion・名前・本文を変えない。全 EditMode の実行テストの集合が base 以上。
- **I5 境界の保持:** `.asmdef` / `.asmref` の変更 0 件。`unity/Assets/SampleGame/` の変更 0 件（Tests を含む。namespace を変えないので `using` の変更も要らない）。新しい型・メンバーの追加 0 件。

**成立条件（起きなければならないこと）**

- **S1 U-A の閉包:** `Runtime/AssetDescriptions` + `Runtime/AssetManagement` + 共通契約 `Runtime/AssetBuildContracts` のソース集合が、OSM の他の部分も BuildSystem も参照せずにコンパイルできる（P 検査の集合 `UA-player` / `UA-editor`）。
- **S2 U-B の閉包:** `Runtime/BuildContent`（連携・OSM 固有の 4 ファイルを除く）+ 共通契約のソース集合と、Editor の BuildSystem 中核（`Editor/Build/Selection`、`Editor/Build/Materialization` から OSM 固有 2 ファイルを除く、`Editor/Build/Content`）が、AssetManagement も OSM の他の部分も参照せずにコンパイルできる（P 検査の `UB-runtime-player` / `UB-runtime-editor` / `UB-selection` / `UB-materialization` / `UB-content`）。
- **S3 回帰:** 全 EditMode 回帰と `pwsh tools/contract-audit.ps1` が合格し、移動対象を path で参照する `tools/ContentDeliveryProbe` がビルドできる。

S1・S2 の証明は P 検査（コンパイラ）が行う。`grep` は補助である。A0 §3.3 のとおり、namespace を据え置くので BuildSystem 本体のソースには `using OneStarMaker.Runtime.AssetManagement`（`AssetType` のため）が残り、「`using` の文字列が 0」は境界の主張に使わない。

### 2.2 受け入れ条件

合否は各 AC の本文で決める。「確認手段」は合否を見る手段で、同じ意味を別の手段で示してもよい。

- **AC1（I1, I3）** `Runtime/AssetBuildContracts/ContentDirectoryException.cs` と `Runtime/AssetBuildContracts/AssetType.cs` が存在し、`Runtime/BuildContent/ContentDirectoryException.cs` と `Runtime/AssetManagement/Abstractions/AssetType.cs` は無い。移動前後で 2 つの `.cs` の本文が同一（差 0 行）で、2 つの `.meta` の `guid:` の値が同一である。`.meta` の `guid:` 以外の差は、Unity が再 import で書き足した importer 節（`MonoImporter:` とその既定値）と末尾改行だけを許す。`Runtime/AssetBuildContracts/` に `.asmdef` / `.asmref` が無い。
  - 確認手段: `git diff -M base head -- <旧 path> <新 path>`（移動元と移動先の `.cs` と `.meta` を両方渡す）。`.cs` は rename だけで本文の差分行が無い。`.meta` は `guid:` 行に差が無い。
  - 合格の例: `.meta` に Unity が `MonoImporter:` 以下（`externalObjects: {}`、`serializedVersion: 2`、`defaultReferences: []`、`executionOrder: 0`、`icon: {instanceID: 0}`、`userData:`、`assetBundleName:`、`assetBundleVariant:` 等の既定値）を書き足した。B が Unity の書いたままの `.meta` を commit した（手で 2 行に戻さなくてよい。戻すと次の import でまた書き換わる）。
  - 不合格の例: 移動のついでに `.cs` の XML doc や空行を直す。`.meta` の `guid:` が変わる。`.meta` の importer 節に既定値でない値が入る。folder に asmdef を置く。
- **AC2（I2, I3, I4, I5）変更集合の許可リスト:** base から head までの変更（全 path）が、次の 5 種類だけである。これ以外の追加・変更・削除（他の `.cs`・`.meta`・asset・Scene・asmdef・`Packages/`・`ProjectSettings/`・テスト・`SampleGame/`・他の tools）が 1 件でもあれば不合格。
  1. AC1 の 2 組（`.cs` と `.meta`）の rename。差は AC1 が許すものだけ。
  2. 次の 3 ファイルの、各 1 行の削除だけ: `Runtime/AssetManagement/IAssetManagement.cs` と `Runtime/AssetManagement/Internal/IContentDirectoryBackend.cs` の `using OneStarMaker.Runtime.BuildContent;`、`Runtime/BuildContent/ContentDirectoryIndex.cs` の `using OneStarMaker.Runtime.AssetManagement;`。
  3. 新規の folder `.meta`（`unity/Assets/OneStarMaker/Scripts/Runtime/AssetBuildContracts.meta`）1 件（AC3）。
  4. `tools/ContentDeliveryProbe/ContentDeliveryProbe.csproj` の 1 行: `ContentDirectoryException.cs` の `<Compile Include>` の path を `Runtime/BuildContent/` から `Runtime/AssetBuildContracts/` へ変える（`Link` 属性は変えない）（AC6）。
  5. `docs/handoff/` 配下の HANDOFF の記録。
  - 確認手段: `git diff -M --name-status base head` の全行を上の 5 種類と照合し、2 の 3 ファイルと 4 の csproj は `git diff` の差分行が上のとおりであることを見る。
  - 不合格の例: `AssetManagement.ContentDirectory.cs` の `using OneStarMaker.Runtime.BuildContent;` を消す（namespace を据え置くので必要な `using`）。Unity が再 import で他の `.meta` や asset を書き換えたものを一緒に commit する。`BuildContentRoot.cs.meta` が変わる。
- **AC3（I1, I3）folder の `.meta`:** 新規の `Runtime/AssetBuildContracts.meta` が `folderAsset: yes` を持ち、その `guid` がリポジトリの他の `.meta` と重複しない。`Runtime/AssetBuildContracts/` に `.asmdef` / `.asmref` が無い（folder は `OneStarMaker.Runtime.asmdef` に属する）。
  - 確認手段: `.meta` の本文を読む。`git grep -l "guid: <その値>" -- "*.meta"` が 1 件だけ。
- **AC4（S1, S2）** head で P 検査（§5「P 検査」）が `P-CHECK pass=True`（exit 0）で終わる。すなわち陽性の 7 集合が全て exit 0、陰性対照 2 つ（`NEG-UA-without-contract`、`NEG-UB-runtime-without-contract`）がどちらも exit ≠ 0 で、その compiler の error が共通契約の型・namespace の欠落を示す（`reasonIsMissingContract=true`）。`-AllowMissingContract` は付けない。
  - 確認手段: A3 で凍結した `p-check.ps1` を head の worktree に対して実行し、`summary.json`（Unity の実版、参照 DLL の assembly identity と sha256 を含む）と各集合の `files.txt` / `args.rsp` / `csc.log` を evidence に残す。
  - 不合格の例: 陽性の集合のどれかが失敗する。陰性対照が成功する、または別の理由で失敗する（検査が空振りしている）。exit 2（前提の不備・条件付きコンパイルの出現）を合格と読む。集合の規則（§5 の表）を head に合わせて変えて通す。
- **AC5（S3, I4）** base と GO 候補 head のそれぞれで、同じ機械・同じ Unity 版・同じコマンド（`pwsh tools/run-tests.ps1`、空 filter、EditMode）で全 EditMode を実行する。base の結果は 1 件以上実行かつ failed 0 であること（そうでなければ比較の基準にしない）。head が合格し、head の結果 XML の実行テストのケース ID（parameterized テストの引数を含む full name）の集合が base の集合を含み、base で passed のケースが head で skipped / failed になっていない。`pwsh tools/contract-audit.ps1` 合格（path budget の検査 9 を含む）。
  - 確認手段: 2 つの XML から full name と結果を抜き出して比べる。総数・passed・skipped・failed を記録する。
  - base が failed 0 にならない場合は、本スライスの欠陥ではなく比較の基準が成立しないので、head を判定せずに人間へ返す（base の失敗を head の合否から差し引いて通さない）。
- **AC6（I2）** `dotnet build tools/ContentDeliveryProbe/ContentDeliveryProbe.csproj` が head で成功する（エラー 0）。
  - 確認手段: 上のコマンドの出力。base でも成功することを同じ機械で確かめておく（比較の基準）。

### 2.3 ここでは答えない問いと所有スライス

- 連携部分（`ContentDirectorySession`、`UnityContentDirectoryBackend`）が AssetManagement の internal 6 件と BuildSystem 本体の internal 7 件を使っている件（A0 §2.2 X4）: asmdef 分割スライス
- Bootstrap が両方の internal を直接配線している件: BOOTSTRAP S2
- BuildSystem 単独の入力・出力・検証の public 入口と、出力先・target の OSM 固定: `BUILDSYSTEM_STANDALONE_ENTRY`
- 共通契約を将来どの assembly が所有するか（候補は共通契約だけの小さな assembly）、namespace をそろえるか、そのときのバイナリ互換: asmdef 分割スライス
- `Rendering/Vfx` が AssetManagement の internal 2 型を使う件: Rendering を切る asmdef スライス

### 2.4 本文へ転記した実装制約（`AGENTS.md` の常時契約から）

- 依存は Game → Framework の一方向。asmdef 参照の追加は設計判断なので行わない（本スライスは asmdef に触れない）。
- 新規または編集する Unity 側の `.cs` は先頭に `#nullable enable`（対象ファイルは全て既に持つ。外さない）。
- `record` を使わない。
- 参照 0 を削除理由にしない（本スライスは型・メンバーを削除しない。削除する `using` 3 行は参照ではない）。
- path は `tools/path-budget.json` に従う（最長の新 path `unity/Assets/OneStarMaker/Scripts/Runtime/AssetBuildContracts/ContentDirectoryException.cs.meta` は 95 文字で予算 150 以内）。
- `.meta` は `.cs` と一緒に `git mv` する。Unity Editor でのコンパイル確認は Phase B が行ってよい。Unity テストの実行・判定は Phase C。

### 2.5 未決事項

なし（設計上の未決）。

### 2.6 スライスの分割の検討

分割しない。変更理由は「共通契約の置き場所」1 つで、2 ファイルの移動は同じ問い（両方向の型の辺を 0 にする）の両半分である。片方だけでは問いに答えられない（U-A だけ、U-B だけの閉包になる）。未知は P 検査の経路 1 つで、A1 で simulated head に対して成立を確かめた（§5）。

## 3. 責務マップ

新しい型・責務は増えない。変わるのは 2 ファイルの置き場所だけ。

| ファイル | 責務（一文） | 変更内容 | 現在行数 | 予想増分 |
|---|---|---|---:|---:|
| `Runtime/BuildContent/ContentDirectoryException.cs` → `Runtime/AssetBuildContracts/ContentDirectoryException.cs`（`.meta` も） | Content Directory の公開境界で返す失敗情報（例外と reason code）。AssetManagement の読み込み API と BuildSystem の形式・revision・配布の両方が投げる | `git mv` のみ | 32 | 0 |
| `Runtime/AssetManagement/Abstractions/AssetType.cs` → `Runtime/AssetBuildContracts/AssetType.cs`（`.meta` も） | Asset の分類。AssetManagement のキャッシュ分類と、BuildSystem の成果物（`BuildContentRoot`）が持つ分類の両方が使う | `git mv` のみ | 16 | 0 |
| `Runtime/AssetBuildContracts.meta`（新規、folder） | Unity の folder `.meta` | Unity Editor が生成 | — | — |
| `Runtime/AssetManagement/IAssetManagement.cs`、`Runtime/AssetManagement/Internal/IContentDirectoryBackend.cs` | （変更なし） | 未使用の `using OneStarMaker.Runtime.BuildContent;` を削除 | — | 各 −1 |
| `Runtime/BuildContent/ContentDirectoryIndex.cs` | （変更なし） | 未使用の `using OneStarMaker.Runtime.AssetManagement;` を削除 | — | −1 |
| `tools/ContentDeliveryProbe/ContentDeliveryProbe.csproj` | （変更なし。.NET 8 の配布検証 probe） | `ContentDirectoryException.cs` の `<Compile Include>` の path を新しい置き場所へ 1 行変える | 7 | 0 |
| `unity/Assets/Docs/Architecture/27-folder-structure.md`、`unity/Assets/README.md` | harvest 先（Phase D） | §0 harvest to のとおり | — | 各 +1〜2 |

配置を選んだ理由:

- **folder `Runtime/AssetBuildContracts/`:** フォルダ境界の基準（architecture-gates）の「依存方向」が違う。AssetManagement と BuildSystem 本体の両方から参照され、どちらも参照しない葉である。`Runtime/` 直下に置くのは、他の Runtime folder と同じ `OneStarMaker.Runtime.asmdef` の配下に留めて、所属 assembly を変えないため（I1）。`Runtime/Shared/` のような汎用の置き場にしないのは、名前が「AssetManagement と BuildSystem の共通契約」という変更主体を表すようにするため（汎用の置き場は未整理の型の受け皿になる）。
- **namespace を据え置く:** owner 決定（A0 §6 D2）。folder と namespace が一致しないのは意図した状態で、型の論理的な所有（両者の共通契約）と所属 assembly（現在は `OneStarMaker.Runtime`）を、folder 名ではなくこの表と A0 で示す。namespace をそろえるのは asmdef を分けるときに一度で行う。
- **未使用の `using` 3 行を消す:** 共通契約へ移した後は、コンパイルには影響しない（`using` の namespace は共通契約が提供し続ける）。残すと、読み手に「AssetManagement の interface が BuildContent に依存する」「BuildSystem の index が AssetManagement に依存する」という誤った辺を示すので消す。消さなくても S1・S2 は成立する。
- 新しいファイル・クラス・asmdef は作らない。行数警報に該当するファイルは無い。
- 将来の assembly の所有: 共通契約は asmdef 分割スライスで「共通契約だけの小さな assembly」にする候補とする。本スライスでは決めない（§2.3）。

## 4. 実装計画

- 変更対象: §3 の表のとおり。
- 開始の前提: 本書を含む計画ブランチの文書 PR が `develop` へマージされ、B の専用ブランチがその `develop` から切られている（本書と A0 が B のブランチにある）。
- 順序: (1) Unity Editor を閉じた状態で、`Runtime/AssetBuildContracts/` folder を作り、2 ファイルを `.cs` と `.meta` ごと `git mv` する → (2) 未使用の `using` 3 行を削除し、`ContentDeliveryProbe.csproj` の path を 1 行直す → (3) Unity Editor を開いて import させる（folder の `.meta` が生成される。移動した `.meta` に Unity が importer 節を書き足したら、そのまま残す。AC1） → (4) Unity Editor でコンパイル確認（エラー 0） → (5) `git status` で、Unity が他の file（`Packages/`、`ProjectSettings/`、他の `.meta`）を書き換えていないか確かめ、書き換えていたら commit に入れない（AC2） → (6) P 検査（`p-check.ps1`、§5）を head の worktree に対して実行し `P-CHECK pass=True` を確認 → (7) `dotnet build tools/ContentDeliveryProbe/ContentDeliveryProbe.csproj`（AC6） → (8) `pwsh tools/contract-audit.ps1`。起点 filter のテストは Phase C（発見 C）が実行する。
- P 検査は Unity のテストでも Build でもなく、Unity 同梱の compiler によるオフラインのコンパイルなので、Phase B が実行してよい（コンパイル確認の範囲）。判定に使う実行は判定 C が行う。
- commit は 1 つでよい（rename の検出を保つため、`git mv` と `using` の削除は同じ commit でよい）。
- Phase B から Phase A へ差し戻す条件: 3 型以外に AssetManagement と BuildSystem 本体の間の型の辺が見つかった（P 検査の陽性の集合が、3 型を移しても失敗する）。`.meta` の GUID を保てない。既存テストの assertion を変えないと通らない。Unity が folder を別 assembly に割り当てる。
- 対象外を維持する方法: `.asmdef` を開かない。`SampleGame/` に触れない。連携部分・OSM 固有部分のファイルを動かさない。AC2・AC3 で確認する。

## 5. テストとレビュー計画

- 単体テスト: 新しい中核ロジックは無い。3 型の契約は既存テスト（`ContentDirectoryFailureCode.<値>` を assert する 8 ファイル: `Tests/BuildContent/ContentRevisionGateTests.cs`、`ContentDirectoryIndexTests.cs`、`PlayerContentConfigurationTests.cs`、`Distribution/ContentDeliveryFailureMatrixTests.cs`、`Tests/Editor/Build/ContentDirectorySessionTests.cs`、`Tests/Editor/Build/AttachedAssetDirectoryTests.cs`、`SampleGame/Tests/Editor/Build/BuildContentDirectoryIntegrationTests.cs`、`SampleGame/Tests/Editor/Build/AttachedAssetDirectoryIntegrationTests.cs`）が固定する。
- 差し戻し中の起点 `-Filter`（C が根拠付きで変更可。受け入れ条件の追加ではない）: `OneStarMaker.Tests.BuildContent`、`OneStarMaker.Tests.Editor.Build`、`OneStarMaker.Tests.AssetManagement`。
- 判定必須テスト（GO 候補 head）: 全 EditMode 回帰（空 filter）を base と GO 候補 head の両方で同じ条件で実行する（AC5。base の実行は比較の基準を作るためで、1 プロセスずつ）。XML で上の 8 ファイルのテストが収録されていること。P 検査（AC4）。`ContentDeliveryProbe` のビルド（AC6）。順序は、全 EditMode 回帰（Unity が head の worktree に `Library/ScriptAssemblies` を作る）→ P 検査（その DLL を参照する）とする。
- 全 EditMode 回帰の適用除外: なし
- 統合・Unity テスト: 既存の EditMode だけ。PlayMode・Build は不要（実行時の分岐を変えない）。
- 操作・実行時・目視条件の検証経路: 対象なし（全条件が EditMode テスト、`git diff`、P 検査で閉じる）。
- 未知の操作経路の疎通結果: P 検査の経路を A1 で確認した（下記）。
- 人間の判断が必要な条件: なし
- 高コスト証拠: なし

### P 検査

- **方式:** A3 凍結 script。script `p-check.ps1` は A1 の主担当が書き、A2 のレビュー入力に含め、A3 で凍結する。本体は git に入れず、Phase A snapshot（凍結時に stage）に置く。判定 C は凍結済みの script を head の worktree に対して実行する。資格情報・通信・本番設定には触れない（ローカルのコンパイルだけ）。
- **何をするか:** Unity 同梱（`ProjectSettings/ProjectVersion.txt` の版、現在 6000.6.0f1）の Roslyn `csc` で、下の集合ごとにソースだけを 1 つの library にコンパイルする。参照は netstandard 2.1、Unity 同梱の `UnityEngine*.dll`（Editor の集合では加えて `UnityEditor*.dll` と define `UNITY_EDITOR`）、対象 worktree の `Library/ScriptAssemblies` の `UniTask.dll` / `Unity.Addressables.dll` / `Unity.ResourceManager.dll` だけ。`OneStarMaker.*` の DLL は渡さない（Editor の BuildSystem の集合は、同じ実行で作った U-B の library だけを参照する）。言語は C# 9、nullable 有効。
- **集合の規則（正本はこの表。script は冒頭の `$Sets` に同じ規則を 1 箇所だけ持ち、A3 凍結時に主担当が表と照合する）:** folder 単位で全 `.cs` を取り、除外は `Scripts/` からの相対 path が完全一致するものだけを外す（同名の別ファイルは外さない）。新しいファイルは自動で集合に入る。表の folder と除外対象の path が実物に無ければ、script は判定せず exit 2 で止まる（規則と実物のずれ）。

| 集合 | ソース（`unity/Assets/OneStarMaker/Scripts/` から） | 除外 | 追加の参照 | define |
|---|---|---|---|---|
| `UA-player` / `UA-editor` | `Runtime/AssetDescriptions/`、`Runtime/AssetManagement/`、`Runtime/AssetBuildContracts/` | なし | なし | なし / `UNITY_EDITOR` |
| `UB-runtime-player` / `UB-runtime-editor` | `Runtime/BuildContent/`、`Runtime/AssetBuildContracts/` | `ContentDirectorySession.cs`、`UnityContentDirectoryBackend.cs`（連携）、`PlayerContentConfiguration.cs`、`PlayerBakedContentConfiguration.cs`（OSM 固有） | なし | なし / `UNITY_EDITOR` |
| `UB-selection` | `Editor/Build/Selection/` | なし | なし | `UNITY_EDITOR` |
| `UB-materialization` | `Editor/Build/Materialization/` | `SceneResourceContentMaterializer.cs`、`ScenePayloadMappingPolicy.cs`（OSM 固有） | `UB-selection` | `UNITY_EDITOR` |
| `UB-content` | `Editor/Build/Content/` | なし | `UB-selection`、`UB-materialization`、`UB-runtime-editor` | `UNITY_EDITOR` |
| 陰性対照 `NEG-UA-without-contract` | `Runtime/AssetDescriptions/`、`Runtime/AssetManagement/`（`UA-player` と同じ条件で、共通契約だけを除く） | なし | なし | なし |
| 陰性対照 `NEG-UB-runtime-without-contract` | `Runtime/BuildContent/`（`UB-runtime-player` と同じ条件で、共通契約だけを除く） | `UB-runtime` と同じ 4 ファイル | なし | なし |

- **合格:** 陽性の 7 集合が全て exit 0、陰性対照 2 つがどちらも exit ≠ 0 かつ compiler の error に共通契約の型・namespace 名（`BuildContent`、`ContentDirectoryException`、`ContentDirectoryFailureCode`、`AssetType` のいずれか）が現れる。照合の範囲: `: error CS<番号>` を含む行だけを対象にし、名前が単一引用符で囲まれた形（`'BuildContent'` 等。compiler が型・namespace 名を示す書き方）だけを数える（script の `$NegativeReasonPattern`）。ファイル path（`…\Runtime\BuildContent\…`）は引用符で囲まれないので、path に `BuildContent` があっても理由の一致にはならない（計画 PR のレビューへの回答として記録した明確化で、凍結時の script の挙動そのもの）。script は合格なら exit 0、不合格なら 1、前提の不備・証明範囲外なら 2 で終わる。
- **exit 2（判定しない）になる場合:** Unity の実版（`Editor/Unity.exe` の ProductVersion）が `ProjectVersion.txt` と違う。compiler・参照 DLL・集合の folder・除外対象が無い。集合が空。集合のファイルに `#if` / `#elif` がある（P 検査は define を「無し」と `UNITY_EDITOR` の 2 通りしか試さないので、条件付きコンパイルが入った時点で証明範囲の外になる。Phase A へ戻す）。外部 DLL（`UniTask` / `Unity.Addressables` / `Unity.ResourceManager`）が `OneStarMaker*` namespace の型を定義している（偽陽性の経路）。予期しない例外。
- **出力（evidence）:** `summary.json`（Unity の期待版と実版、compiler の path、参照する外部 DLL ごとの path・assembly identity・byte 数・sha256、集合ごとのファイル数・exit code・エラー番号の集計、陰性対照の理由の判定）、各集合の `files.txt`（コンパイルしたファイルの相対 path）・`args.rsp`（参照と define）・`csc.log`（compiler の出力）。
- **外部 DLL の扱い:** `Library/ScriptAssemblies` の DLL は、対象 worktree の `Packages/packages-lock.json` から Unity が生成したもので、P 検査はその由来（lock との一致）までは検証しない。偽陽性の経路（DLL が OSM の型を持ち込む）は上の検査で塞ぎ、偽陰性（版違いで失敗）は失敗として表に出る。由来を問うときは `summary.json` の sha256 と assembly identity を、同じ lock から生成した別 worktree の値と比べる。
- **前提:** 対象 worktree を Unity Editor で一度開き、`Library/ScriptAssemblies` が生成されていること。
- **再実施の手順:** `pwsh -NoProfile -File p-check.ps1 -ProjectRoot <worktree>/unity -OutDir <出力先>`（Unity の場所が既定と違えば `-UnityEditorRoot`）。base を測るときだけ `-AllowMissingContract`（共通契約の folder が無いことを許す。判定には使わない）。
- **A1 / A3 で確かめた結果:** base `d4861882`（`-AllowMissingContract`）では、`UA-*` が `CS0234`×3（`using OneStarMaker.Runtime.BuildContent` の 3 箇所）、`UB-runtime-*` が `CS0246`×3・`CS0103`×1・`CS0234`×2（`AssetType` と未使用 `using`）で失敗、`UB-content` は依存先の欠落で失敗、`UB-selection` / `UB-materialization` は成功、`pass=False`（exit 1）。同じ base に `-AllowMissingContract` を付けないと exit 2。base の写しに §3 の移動と `using` 削除を当てた simulated head（リポジトリ外の一時コピー）では、陽性 7 集合が全て exit 0（`UA` 31 ファイル、`UB-runtime` 18 ファイル）、陰性対照 2 つが exit 1 で理由はどちらも共通契約の欠落、`pass=True`（exit 0）。失敗経路も simulated head の一時的な改変で確かめた: Unity 6000.5.0f1 を渡すと exit 2、除外対象を 1 つ消すと exit 2、集合のファイルに `#if` を足すと exit 2、BuildSystem 本体に `AssetKey`（AssetManagement の型）への参照を足すと `pass=False`（exit 1）。
- **P 検査が示さないもの:** Unity の asmdef ごとのコンパイル（その確認は Phase B の Editor コンパイルと AC5 の回帰）、Unity が実際に使う define の全組み合わせ（対象に `#if` が無いことを前提にし、出現したら exit 2）、実行時の挙動、シリアライズ互換。

### レビュー計画

- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`（harvest 時）、P 検査。
- A0/A1 主担当・モデル・ベンダー: Claude Code（Anthropic）。
- A2 独立レビュー（normal: 2 観点）: (a) アーキテクチャゲート（共通契約の置き場所・名前・所有、I1〜I5 の経路の網羅、AC と P 検査の集合が S1・S2 を本当に示すか、P 検査 script の正しさと再現性。「計画対象は実装済みと仮定し、現コードに無いことを欠陥にしない」で枠を固定する）、(b) 目的レビュー（§1 の目的・利用者の原文だけを渡す）。
- A3 統合担当・モデル・採否: owner + 主担当。
- C' 用に予約した担当・モデル・ベンダー: A2 で OpenAI と xAI を使うので、未関与の系列は残らない。C' は `独立性制約あり`（risk `normal` なので強化条件の対象外）。

### A2 独立レビュー

#### revision 0・A2 入力版（循環 2 件の版。履歴）

- 入力: 計画ブランチの commit `93723a4c`、旧名 `RUNTIME_REFACTOR_CYCLES.md` の content sha256 `ef83a07218319d1e76928b512764dbd0a2049014d86d55c810c1ddbcee1506f3`。base の Runtime・Tests・`SampleGame/Tests/Editor/Build` の写し、`AGENTS.md`、`27-folder-structure.md`、`unity/Assets/README.md`、`architecture-gates.md` を同梱。指示書は「§3・§4 は実装済みと仮定し、現コードに無いことを欠陥にしない」で枠を固定し、互いの指摘は見せていない。2026-11-07 実施。

| 観点 | 担当・モデル・ベンダー | 結果 |
|---|---|---|
| (a) アーキテクチャゲート | Codex CLI（OpenAI、read-only sandbox） | blocker-for-freeze 2（A-1 grep が型依存を証明しない、A-2 risk と互換性）、should 2（A-3 AC1 の pathspec・AC4 の複数行宣言、A-4 主張の範囲） |
| (b) 挙動同一性 | cursor-agent（xAI Grok 4.7 high） | blocker-for-freeze 3（B-1〜B-3）、should 3（B-4〜B-6）、note 2（B-7、B-8） |

外部レビュー（owner が依頼、2026-11-07）は F-01〜F-10。これらの採否は前半の書き直し版に反映した。A1 改訂版での扱い: A-1 / F-06（grep は型依存を証明しない）は P 検査（コンパイラ）を AC4 の証明手段にして解消。A-2 / F-04 / F-05（互換を壊す namespace 変更と risk）は namespace を据え置いて互換の破壊自体を無くした。F-01（目的が未検証）は A0 と owner の上位目的で置き換えた。B-1 / B-8 / F-02 / F-09 は Camera 側で、`RUNTIME_REFACTOR_CAMERA_FOCUS` の入力。その他（A-3、B-2〜B-7、F-03、F-07、F-08、F-10）は AC1〜AC5 と §0 に反映済み。

#### A1 改訂版の A2

- 入力版（未 commit の版）: 本書 A1 改訂版の content sha256 `21fef4f62deac69a06e768cf58b4b965faa405d296f5198f1d21a30b508483cb`、`p-check.ps1` の content sha256 `8b035af9648e5a699afc8e81669921e4304c5d8b6cb20bc79b465b6e100cc17c`。同一 PC 上の packet に写して渡した。A2 入力版の commit は切っていない。計画ブランチの作業ツリーに、本書の旧名からの rename と BOOTSTRAP・`docs/README.md` の別の未 commit の変更があり、HANDOFF だけの commit を切るにはそれらの扱いを owner と決める必要があるため（凍結時に A3 凍結版として commit する）。
- (a) アーキテクチャゲートの packet: 上の 2 file、指示書（英語。「§3・§4 は実装済みと仮定し、現コードに無いことを欠陥にしない」で枠を固定。owner 決定 A0 §6 D1〜D5 を問い直さない）、base の `Runtime/{AssetDescriptions,AssetManagement,BuildContent}`・`OneStarMaker.Runtime.asmdef`・`AssemblyInfo.cs`・`Editor/Build/**` の写し、P 検査の base と simulated head の `summary.json`、A0、`AGENTS.md`、`architecture-gates.md`、`27-folder-structure.md`、`path-budget.json`。
- (b) 目的レビューの入力: §1 の目的と利用者の原文だけ（規則どおりに連ねた 1 本の sha256 `e6cff5bc2405b320c76c59b2189fe968d6888454464a31d346ed3391a8893373`）。問いは規則の 2 つに固定し、他の file を読まないよう指示した。

| 観点 | 担当・モデル・ベンダー | 結果 |
|---|---|---|
| (a) アーキテクチャゲート | Codex CLI（OpenAI、read-only sandbox。静的レビューで、Unity と P 検査は実行していない） | 判定「修正後に凍結可」。blocker-for-freeze 3（R-01〜R-03）、should 3（R-04〜R-06）、note 1（R-07）。配置（`Runtime/AssetBuildContracts/`、namespace 据え置き、同じ assembly）、共通契約の過不足、スライスの切り方、I1・I3 の設計は問題なしとした |
| (b) 目的レビュー | cursor-agent（xAI Grok 4.7 high）。読んだ file は指示書と入力の 2 つだけ（実行ログで確認） | 保証すべきこと: 互いの型を参照しない、共有型はどちらの本体にも属さない小さな共通契約、公開 API・実行時挙動・シリアライズ不変、取り組みの最初の段であること。作らないもの: 利用者の入力・操作、公開 API 等を変える変更、共有型をどちらかの本体に置くこと、小さな共通契約を超える共通層や新しい機構、単独利用・併用の完成（配布・導入・設定）までをこの段で作ること |

実施 2026-11-07。2 件は互いの指摘を見ていない。

### A3 採否（主担当の整理。2026-11-07 に owner の指示で凍結）

| 指摘 | 採否 | 反映 |
|---|---|---|
| R-01 変更集合の制約が `.cs` と一部の path に限られ、他の `.meta`・asset・設定の便乗変更を通しうる | 採用 | AC2 を base→head の全 path の許可リスト（この時点で 4 種類。後の事前確認で `ContentDeliveryProbe.csproj` の 1 行を足して現行は 5 種類）に置き換えた。folder の `.meta` の妥当性と GUID の重複なしを AC3 にした |
| R-02 P 検査が folder の欠落を黙って飛ばし、除外を名前で全ファイルに当てる | 採用 | script の除外を相対 path の完全一致にし、folder・除外対象の欠落と空集合を exit 2 にした。共通契約の folder の欠落は `-AllowMissingContract`（base 測定専用）のときだけ許す。§5 の表の文言も合わせた |
| R-03 外部 DLL の実体・由来を確かめない（偽陽性・偽陰性） | 一部採用 | Unity の実版を `Unity.exe` の ProductVersion で照合し、外部 DLL の assembly identity・sha256 を `summary.json` に残し、DLL が `OneStarMaker*` の型を定義していれば exit 2 にした（偽陽性の経路を塞ぐ）。`packages-lock.json` との一致の検証は採らない: DLL は同じ worktree の lock から Unity が生成したもので、残る経路（版違い）は失敗として表に出る偽陰性だけのため。由来を問うときの比べ方を §5 に書いた |
| R-04 陰性対照が理由を問わず失敗を合格とする。前提の不備で exit 2 に届かない | 採用 | 陰性対照は error に共通契約の型・namespace 名が現れることを要求（`reasonIsMissingContract`）。`UA-player` と同じ条件で実行。前提の不備は stderr と exit 2 を明示し、全体を try/catch で exit 2 にした。版違い・除外欠落・`#if` 出現・隠れた依存の 4 経路を実際に通して確かめた（§5） |
| R-05 `using` 削除の説明が一般論として強すぎる。define の証明範囲 | 採用 | I2 の経路を一般論から 3 箇所の具体的な確認（型・拡張メソッド・別名の不使用）に改めた。集合に `#if` / `#elif` が現れたら exit 2（証明範囲外、Phase A へ戻す）にし、§5 の「示さないもの」に書いた |
| R-06 AC5 の base 集合の取り方が固定されていない | 採用 | AC5 に、同じ機械・Unity 版・コマンドで base と head を実行すること、base が 1 件以上かつ failed 0 であること、parameterized を含むケース ID で比べること、passed → skipped / failed を不合格とすることを書いた |
| R-07 集合の規則が表と script に重複 | 一部採用 | script の規則を冒頭の `$Sets` 1 箇所にまとめ、正本は §5 の表とした。凍結時に主担当が照合する（2026-11-07 の改訂版で照合済み: folder 6 組・除外 6 件が一致）。機械による照合は作らない（規則は 6 行で、変更は Phase A の revision でしか起きない） |
| 目的レビュー | 一致 | 回答の保証と「作らないもの」は、全て §1 の目的、§1 対象外、§2 の「成果の範囲」と I1〜I5 に既にある。初稿に無い保証も、初稿が作る要らないものも無い |

**owner の回答（2026-11-07）:**

- **Q1 risk:** `normal` のまま。
- **Q2 目的の文言:** §1 の目的・利用者の文言で確定。
- **Q3 凍結時の commit:** 主担当の案どおり。本書（旧名からの rename を含む）、A0、`docs/README.md` の一覧の更新を含め、BOOTSTRAP は A0 §4.2 の改訂を当てるまで含めない。

**A3 凍結（2026-11-07）:** owner（root）が凍結を指示した。凍結の前に主担当が §5 の集合の表と `p-check.ps1` の `$Sets` を照合し、folder 7 組（陽性 5 集合と陰性対照 2 つ）と除外 10 件（`UB-runtime` と `NEG-UB-runtime-without-contract` の各 4 件、`UB-materialization` の 2 件）が一致することを確かめた。凍結版は、本書・A0・`docs/README.md` を含む A3 凍結 commit `af97423f` と、Phase A snapshot（本書と `p-check.ps1`）。その後の記録訂正（下の段落）を含む commit が引き継ぐ commit で、凍結そのものとは区別する。BOOTSTRAP の未 commit の変更は含めていない（Q3）。

**凍結後の記録の訂正（計画 PR のレビュー、2026-11-07）:** 計画 PR の cursor[bot] レビューが引継ぎの状態表示の食い違いを指摘した。本書では、§0 の PR 状態ブロックの Evidence 欄（「未生成」→ Phase A snapshot はローカル固定済み）、§5 の A3 採否の見出し（「凍結は owner の指示待ち」→ 凍結済み）、§5 P 検査の陰性対照の照合範囲（引用符で囲まれた名前だけを数える。script の挙動そのものの明確化）を直した。凍結済みの条件（I1〜I5、S1〜S3、AC1〜AC6、P 検査の集合と script）の意味・合否は変えていないので、Phase A の revision は 0 のまま。Phase A snapshot は、訂正後の本書と同じ `p-check.ps1`（content sha256 `94291d8f…`、変更なし）で stage し直し、引き継ぐ commit はこの訂正を含む commit とする（渡された SHA と HEAD が一致するように）。続く cursor[bot] と owner のレビューで、凍結 commit と引き継ぐ commit の呼び分け（§0 の票と上の凍結の段落）、§0 の risk と revision の経緯に残っていた凍結前の言い回し、R-01 の採否欄の許可リストの件数（現行は 5 種類）を直した。これらも条件の意味・合否は変えない。Phase A snapshot は訂正のたびに、引き継ぐ commit を head にして stage し直す。

#### Phase C / C' で問われそうな点の事前確認（A3、2026-11-07）

owner の依頼で、凍結の前に、後の Phase で指摘されそうな点を実物で確かめた。次の 3 件は計画の欠落で、本書に反映した（A3 の整理版の改訂。目的・利用者の欄は変えていない）。

| 確認したこと | 結果 | 反映 |
|---|---|---|
| 移動する 2 ファイルを path で使う箇所が Unity の外にあるか | `tools/ContentDeliveryProbe/ContentDeliveryProbe.csproj` が `ContentDirectoryException.cs` を path 指定でコンパイルする。CI に入っていないので、壊れても気づかない。一時コピーで、移動だけだと `CS2001` で失敗し、path を 1 行直すと通ることを確かめた | §1 現況、AC2 の許可リスト 4、AC6（ビルド成功）、S3、§3 の表、§4 の順序 |
| 移動する `.meta` が Unity の再 import で書き換わるか | 2 つの `.meta` は 2 行だけの最小形式で、Unity が importer 節を書き足す可能性がある。旧 AC1 の「`.meta` の本文差 0」は、B が Unity の挙動しだいで守れない条件だった | AC1 を `guid:` の同一性と、許す差（importer 節の既定値と末尾改行）に改め、合格例・不合格例を付けた。§4 に「Unity が書いたまま残す」を書いた |
| P 検査の陰性対照が U-B 側の空振りも捕まえるか | 陰性対照は U-A 側だけだった | `NEG-UB-runtime-without-contract` を足し、2 つとも共通契約の欠落を理由に失敗することを AC4 の条件にした。base と simulated head で再実行し期待どおり（`p-check.ps1` の content sha256 `94291d8f45f9ee1a74ffa4848a0b38d350a49b5d681f13bb7b293f3dadf5e56a`） |

次は確認して問題が無かった点、または既に本書が扱っている点である。

- テスト・CI が移動する path を使っていないか: `.github/workflows` と `unity/Assets/**/Tests` に該当なし。`dotnet-offline-tests` の 4 project も該当ファイルを含まない。
- `contract-audit` の検査: 検査 1（`#nullable enable`）は移動する 2 ファイルとも先頭にある。namespace と folder の一致を見る検査は無い（folder と namespace の不一致は検査で落ちない）。検査 9（path 予算）は最長 95 文字。
- Unity が Editor を開いたときに `Packages/packages-lock.json`・`ProjectSettings/` 等を書き換えることがある: AC2 の許可リストで拒否し、§4 の手順 (5) で B が commit 前に外す。
- 判定 C の review worktree に `Library/ScriptAssemblies` が無いと P 検査は exit 2: §5 の判定必須の順序（全 EditMode 回帰で Unity に Library を作らせてから P 検査）で塞いだ。
- base の全 EditMode が既存の失敗で failed 0 にならない場合: AC5 で「比較の基準が成立しないので人間へ返す」とした（base の失敗を差し引いて GO にしない）。
- C' の blind bundle に `p-check.ps1` が入るか: 凍結時に Phase A snapshot に stage し、C' の bundle には凍結版の script と判定 C が出した `summary.json` 等を入れる（§0）。
- B の開始時点で本書が `develop` にあるか: §4 に開始の前提（計画の文書 PR のマージ後に `develop` から切る）を書いた。

#### 計画レビュー R3（A0 に対する、owner が依頼）

判定「A0 の方針・owner 決定を支持。A0 を閉じて次スライスへ進行可。ただし引継ぎ状態の整合を修正すること」。指摘 F-R3-01（A0 の §0・§4.1・§4.2 が、本書の進行状態と食い違う）は A0 側で直した（A0 §0 に、A0 は閉じたこと、本書は A3 整理済み・凍結前であること、folder が決まったこと、§4.2 の本書向けの表は適用済みであることを書いた）。本書への新しい指摘は無い。

## 6. Phase B 実装結果

- 実装: §4 の順序 (1)〜(8) のとおり。
  - (1) `Runtime/AssetBuildContracts/` を作り、`ContentDirectoryException.cs` と `AssetType.cs` を `.meta` ごと `git mv` した。`git diff -M --name-status` で 4 件とも `R100`（`.cs` と `.meta` の本文差 0。Unity は移動した `.meta` に importer 節を書き足さなかった）。
  - (2) 未使用の `using` 3 行（§2.2 AC2 の 2）を削除し、`ContentDeliveryProbe.csproj` の `<Compile Include>` の path を 1 行直した（`Link` は不変）。CRLF の作業ツリーの改行は保った。
  - (3) Unity Editor 6000.6.0f1 で head の worktree を開き import させた。folder の `.meta`（`folderAsset: yes`、guid `ec29c3f0df4ad8a40a7b7a716e82f97a`）が生成された。この guid を持つ `.meta` は `unity/Assets` で 1 件だけ（AC3）。
  - (4) コンパイル確認: Editor.log に `Tundra build success`、`: error CS` は 0 行。生成された `Library/ScriptAssemblies/OneStarMaker.Runtime.dll` を reflection で読み、3 型（`ContentDirectoryException`、`ContentDirectoryFailureCode`、`AssetType`）が完全名のまま `OneStarMaker.Runtime` に属することを確認した。
  - (5) Unity が作業ツリーに出した変更（`MobileDependencyResolver/**` の `.pdb.meta` 5 件の削除と `Settings/UniversalRenderPipelineGlobalSettings.asset`）は commit から外し、`git checkout` で戻した。`Packages/`・`ProjectSettings/`・他の `.meta` の変更は無かった。commit の変更集合は AC2 の 5 種類だけ（rename 4、`using` 削除 3 ファイル、folder `.meta` 1、csproj 1、本書）。
  - (6) P 検査: 凍結版 `p-check.ps1`（content sha256 `94291d8f…`）を head の worktree に `-AllowMissingContract` なしで実行し `P-CHECK pass=True`（exit 0）。陽性 7 集合が全て exit 0（`UA-*` 31 ファイル、`UB-runtime-*` 18、`UB-selection` 20、`UB-materialization` 6、`UB-content` 6）。陰性対照は `NEG-UA-without-contract` が exit 1（`CS0234`×1、`CS0246`×22）、`NEG-UB-runtime-without-contract` が exit 1（`CS0103`×1、`CS0234`×1、`CS0246`×7）で、どちらも `reasonIsMissingContract=True`。
  - (7) `dotnet build tools/ContentDeliveryProbe/ContentDeliveryProbe.csproj` が head で成功（警告 0、エラー 0）。出力先は worktree の外。
  - (8) `pwsh tools/contract-audit.ps1`: `errors=0 warnings=0 checks=1,2,3,4,5,6,7,8,9`、exit 0。
  - Editor は B が起動したものを `EditorApplication.Exit(0)` で正常終了させた（未保存の Scene 変更は無い）。
- HANDOFF との差: なし。base は §0 のとおり B 開始時の `develop` の head（`3e8bf06c`）で再固定した。
- 未実行: Unity テスト（`pwsh tools/run-tests.ps1`、起点 `-Filter` と全 EditMode の base / head、AC5）は Phase C。AC6 の base 側のビルド（比較の基準）も Phase C。上の (4)・(6)〜(8) は B の session の観測で、判定の入力にしない（判定 C が head で取り直す）。
- implementation head commit: `08177c39a3f17e56355e667420b1e10256b0b0bb`（§0 の欄と同じ）
- Phase B 担当・モデル・ベンダー: Claude Code（Anthropic）

### 動作確認

- 対象 head: 対象なし
- 条件ごとの実行担当・環境・実行したコマンド: 対象なし（§5 のとおり、全条件が EditMode テスト、`git diff`、P 検査で閉じる）
- 生の観測と保存先・状態: 対象なし
- 検証経路の期待と違う観測と、B への差し戻し: 対象なし
- `未実施` の条件と理由・担当: 対象なし（§5 のとおり検証経路に該当条件が無い）

## 7. Phase C

### 発見 C（1 回目、2026-11-07）

- 種別: 発見 C。対象は implementation base `3e8bf06c` から implementation head `08177c39` の差分。GO 判定はしていない。
- 使用した evidence bundle の取得案内・状態: evidence bundle は作っていない（発見 C は固定 commit の diff と機械検査で開始した）。Phase A snapshot を起動指示の reference と packageSha256 で fetch した。`p-check.ps1` の content sha256 は §5 の `94291d8f…` と一致し、snapshot 内の本書は `3e8bf06c`（＝`4da676bc`）の本書と一致した。base から head の本書の差は §0・§6 の B の記録だけで、凍結済みの条件（§1〜§5）は変わっていない。
- 開始時の照合: HEAD は渡された SHA と一致し、作業ツリーは clean だった。implementation head 欄は「この票を含む commit」で、`HEAD^..HEAD` に `docs/handoff/` 以外の file がある。よって implementation head は `08177c39` 自身。PR はまだ無い。
- 構造適合:
  - AC1: 2 組（`.cs` と `.meta`）とも `R100` の rename で、本文差 0 行、`guid:` も同一。Unity は importer 節を書き足していない。旧 path は無い。
  - AC2: `git diff -M --name-status` の 10 行は全て許可リストの 5 種類に入る（rename 4、`using` 1 行削除 3 ファイル、folder `.meta` 1、csproj の `Include` 1 行（`Link` は不変）、本書）。`AssetManagement.ContentDirectory.cs` の `using` は残っている。
  - AC3: `AssetBuildContracts.meta` は `folderAsset: yes`、guid `ec29c3f0…` を持つ `.meta` は 1 件だけ。`Runtime/` 配下の asmdef は `OneStarMaker.Runtime.asmdef` 1 つで、`.asmref` は無い。
  - §3 の責務マップとの差: なし（型・メンバーの追加 0、行数の増加 0）。削除した `using` 3 行のファイルに、消した namespace の型名・`cref` は無い。旧 folder `AssetManagement/Abstractions/` には他の 6 ファイルが残るので、空 folder にはならない。tools・CI・Architecture 文書に旧 path を使う箇所は無い（`docs/handoff` と `artifacts/` を除く `git grep`）。対象 folder に `#if` / `#elif` は無い。
- 現在の問いを阻害する findings: なし。
- 後続スライスへ移送する findings: なし。
- 実行した検査:
  - P 検査（凍結版 `p-check.ps1`、`-AllowMissingContract` なし、head の worktree。`Library/ScriptAssemblies` は B の Editor 起動で作られたもの）: `P-CHECK pass=True`、exit 0。陽性 7 集合が全て exit 0（`UA-*` 31、`UB-runtime-*` 18、`UB-selection` 20、`UB-materialization` 6、`UB-content` 6 ファイル）。陰性対照 2 つが exit 1 で、どちらも `reasonIsMissingContract=True`。
  - `pwsh tools/contract-audit.ps1`: exit 0。
  - AC6: `dotnet build tools/ContentDeliveryProbe/ContentDeliveryProbe.csproj` が head で成功した（エラー 0）。base `3e8bf06c` の一時 worktree でも成功した（エラー 0）。
- 実行したテストコマンドと `-Filter`、対象を選んだ理由: `pwsh tools/run-tests.ps1 -Filter 'OneStarMaker.Tests.BuildContent;OneStarMaker.Tests.Editor.Build;OneStarMaker.Tests.AssetManagement'`（§5 の起点 filter をそのまま使った。EditMode、sandbox 外）。
- テスト結果: 325 件中 passed 324、failed 1、skipped 0。
  - 失敗したのは `OneStarMaker.Tests.Editor.Build.BuildContentDirectoryIntegrationTests.ScenePrefabTextureAndTwoRepresentationsBuildAndMoveAsOneDirectory` だけで、`[Timeout(360000)]` の超過（実行 624 秒）。assertion の失敗ではない。log では Content Directory の build 2 回（Full と Incremental）とも呼び出しまで進んでおり、URP/Lit 等の shader compile が走っている。B は Unity テストを実行していないので、この worktree での Content build は初回だったとみられる（shader cache の状態は確かめていない）。同じテストは過去のスライスでも、shader compile の資源不足（`out of memory during compilation`）で 1 回落ちた記録がある。本スライスの変更は 2 ファイルの置き場所だけで、script の GUID・型・build の入力は変わらない。凍結済みの条件や常時契約に違反する根拠は無いので、blocker にしていない。環境の時間・資源によるものかは**未確定**で、判定は AC5（base と head の全 EditMode を同じ機械で比べる）に委ねる。base で passed なのに head で failed なら、AC5 で不合格になる。
  - Unity の終了: 結果 XML は 17:30 に完成した。その後、終了ログの末尾（`MemoryLeaks`）を出したまま、Unity の process は 1 時間以上終わらなかった。発見 C が起動した PID だけを止めようとしたが、`Stop-Process` と `taskkill /F` は「実行中のインスタンスが無い」と返し、process の一覧には残ったままである。`run-tests.ps1` の wrapper（発見 C が起動したもの）は強制終了した。この run は強制終了を含むので、成功の証拠にはしない（発見用の観測にだけ使う）。`tools/Harness/README.md` の既知の native 終了 stall と同じ症状とみて、原因は調べていない。判定 C は、残った process が再起動などで消えたことを確かめてから Unity を起動すること。
  - Unity が作業ツリーに出した `MobileDependencyResolver/**/*.pdb.meta` 5 件の削除は、`git checkout` で戻した（AC2）。
- 判定必須のうち未実行（判定 C で行う）: 全 EditMode 回帰の base と head（AC5）。判定用の P 検査（AC4）、AC6、contract-audit。evidence bundle の作成。
- 未確認事項: 上の timeout が環境によるものかどうか。B の担当モデルが §6 に書かれていないので、「Phase C は Phase B と異なるモデル」を満たすかは確認できていない（発見 C の担当は下記）。
- 担当・モデル: Claude Code（Anthropic、Claude Opus 5.5）

### 判定 C（2026-11-07）

- 種別: 判定 C。GO 候補 head は implementation head `08177c39`（発見 C の後に履歴の整理は無い）。base は `3e8bf06c`。
- 判定: **GO**。§2.1 の不変条件 I1〜I5 と成立条件 S1〜S3 を、AC1〜AC6 で全て確認した。理由は下の各 AC のとおりで、AC5 の読み方は「判断の注記」に書いた。
- 開始時の照合: HEAD は渡された SHA `6f045114` と一致し、作業ツリーは clean だった。implementation head 欄の `08177c39` は HEAD の祖先で、その間の差分は本書だけ。Phase A snapshot を起動指示の reference と packageSha256 で fetch した。`p-check.ps1` の content sha256 は §5 の `94291d8f…` と一致し、snapshot 内の本書は `3e8bf06c` の本書と同一だった。
- 使用した evidence bundle の取得案内・状態: §0 のとおり（判定 evidence、ローカル固定済み（未昇格）、tag `evidence/runtime-refactor-asset-cycle/1`）。収録物は diff、AC4・AC6・contract-audit の出力、AC5 の 3 回の run の `results.xml`・`unity.log`・`step.json`（run 2 の head は `step.json` なし。下記）、判定 C が書いた XML 比較 script とその出力。
- 各 AC:
  - AC1: 2 組（`.cs` と `.meta`）とも `R100` の rename で、本文差 0 行、`guid:` も同一。旧 path は無い。`Runtime/AssetBuildContracts/` に asmdef / asmref は無い。
  - AC2: `git diff -M --name-status base head` の 10 行は、全て許可リストの 5 種類に入る。`using` を消した 3 ファイルの差分は各 1 行の削除だけ。csproj の差分は `Include` の 1 行だけで、`Link` は変わっていない。
  - AC3: `AssetBuildContracts.meta` は `folderAsset: yes`、guid `ec29c3f0…` を持つ `.meta` は head に 1 件だけ。
  - AC4: 凍結版 `p-check.ps1` を head の worktree に `-AllowMissingContract` なしで実行し、`P-CHECK pass=True`、exit 0。陽性 7 集合が全て exit 0（`UA-*` 31、`UB-runtime-*` 18、`UB-selection` 20、`UB-materialization` 6、`UB-content` 6 ファイル）。陰性対照の `NEG-UA-without-contract` は exit 1（`CS0234`×1、`CS0246`×22）、`NEG-UB-runtime-without-contract` は exit 1（`CS0103`×1、`CS0234`×1、`CS0246`×7）で、どちらも `reasonIsMissingContract=true`。Unity の実版は `6000.6.0f1_f7f8ed4d1e24`。参照した外部 DLL は head の AC5 の run 1 で作られた `Library/ScriptAssemblies` のもの。
  - AC5: `pwsh tools/run-tests.ps1`（空 filter、EditMode、sandbox 外）を、同じ PC・Unity 6000.6.0f1・同じコマンドで実行した。base は専用の detached worktree（Library は新規作成）、head は本 worktree（unity/ の tree は `08177c39` と同一）。
    - base: 総数 1644、passed 1619、failed 0、skipped 0、inconclusive 25。XML の root result は `Passed`。
    - head の run 1: 総数 1644、passed 1618、failed 1。落ちたのは `BuildContentDirectoryIntegrationTests.ScenePrefabTextureAndTwoRepresentationsBuildAndMoveAsOneDirectory` で、`Timeout(360000)` の超過（実行 521 秒。base では 238 秒で passed）。実行中に、別 project（`SampleGameForOneStarMakerFramework`）の Unity Editor とその import worker（20:02 と 20:08 に起動）が動いていた。同じ Content build の段の所要が base の 2〜4 倍、run 全体も 922 → 1371 秒に延びた。発見 C の timeout も同じテストだった。
    - owner が他の Unity process を全て閉じ（2026-11-07、owner の指示「閉じた・再実行して」）、head を同じコマンドで 1 回だけ再実行した。
    - head の run 2: 総数 1644、passed 1619、failed 0、skipped 0、inconclusive 25。XML の root result は `Passed`。上のテストは 178 秒で passed、run 全体は 702 秒。
    - base と head の run 2 の比較（parameterized の引数を含む full name）: base のケースは全て head にあり（欠落 0、追加 0）、結果が変わったケースは 0 件。base で passed のケースが head で skipped / failed になったものは無い。inconclusive の 25 件は両方とも同じケース。
    - §5 の 8 ファイルのテストは head の XML に全て収録されている（`ContentRevisionGateTests` 4、`ContentDirectoryIndexTests` 5、`PlayerContentConfigurationTests` 13、`ContentDeliveryFailureMatrixTests` 16、`ContentDirectorySessionTests` 21、`AttachedAssetDirectoryTests` 2、`BuildContentDirectoryIntegrationTests` 1、`AttachedAssetDirectoryIntegrationTests` 1）。
    - contract-audit: exit 0（`errors=0 warnings=0`）。
  - AC6: `dotnet build tools/ContentDeliveryProbe/ContentDeliveryProbe.csproj` が head で成功した（警告 0、エラー 0）。比較の基準として base の worktree でも成功した（警告 0、エラー 0）。出力先は worktree の外。
- 判断の注記:
  - AC5 の「failed 0」と「head が合格」は、XML の failed 数と root result で読んだ。`run-tests.ps1` の `step.json` は、3 回とも Inconclusive があることを理由に status `failed` を記録している。Inconclusive の 25 件は `SampleGame.Tests.Rendering.Vfx.*PlayTests` で、`-nographics` で実行されたもの。base と head で同じケースが同じ結果なので、本スライスの変更によるものではない。凍結済みの AC5 は「同じコマンド（空 filter、EditMode）」だけを指定しているので、`-WithGraphics` は付けていない。runner の status で読めば base が比較の基準にならず、規則どおり人間へ返すことになる。この読み方の確認を owner に求めたが、回答の前に再実行の指示があったので、上の読み方で判定した。owner が runner の status で読むと裁定するなら、この判定は人間へ返す扱いに変わる。
  - head の run 1 は、他の Unity の負荷が重なったことによる不一致とみて、合否の入力にしていない（bundle には生のまま収録した）。run 2 は 1 回だけ実行し、同じ条件での反復はしていない。
  - Unity の終了: 3 回とも、結果 XML を書いた後に Unity が終了しなかった（既知の native 終了 stall と同じ症状。原因は調べていない）。base と head の run 1 の process は 20:30〜20:31 に exit 1 で終わり、`step.json` には「Unity process が正常終了しませんでした」がある。head の run 2 の process は XML から 10 分以上たっても終了せず、`step.json` の無い状態で bundle を作った。判定は XML で行った。
  - Unity が作業ツリーに出した変更（`MobileDependencyResolver/**/*.pdb.meta` 5 件の削除、`Settings/UniversalRenderPipelineGlobalSettings.asset`）は `git checkout` で戻し、commit していない（AC2）。
- 現在の問いを阻害する findings: なし。
- 後続スライスへ移送する findings:
  - `BuildContentDirectoryIntegrationTests.ScenePrefabTextureAndTwoRepresentationsBuildAndMoveAsOneDirectory` は、同じ PC で他の Unity と並行すると `Timeout(360000)` を超える（観測は 521 秒と 624 秒、静かなときは 178〜238 秒）。本スライスの変更とは関係が無い。判定用の全 EditMode は、他の Unity を閉じて実行する必要がある。
  - `run-tests.ps1` は Inconclusive を不合格とするので、`-nographics` の全 EditMode は VFX の PlayTests 25 件のために常に status `failed` になる。全 EditMode を判定に使う AC の読み方（XML の failed 数か、runner の status か、`-WithGraphics` で流すか）を、Harness かテンプレートで決めておく必要がある。
- 未確認事項: Phase C は Phase B と異なるモデルで行うという条件を満たすかは確認できていない（§6 に B のモデル名が無い。判定 C の担当は下記）。
- 担当・モデル: Claude Code（Anthropic、Claude Opus 5.5）。発見 C とは別の session。

## 8. Phase C'

- 担当方式:
- 種別と巡:
- 受け取った決定記録の版:
- 使用した blind audit bundle の取得案内・状態:
- 確認範囲・方法:
- 判定:
- 現在の問いを阻害する findings:
- 後続スライスへ移送する findings:
- 残存リスク:
- 監査できなかった範囲:
- 独立性:
- 発見 C / 判定 C 結論の事前閲覧・設計実装への関与:
- 担当・モデル:

## 9. Phase D

- C / C' の突合:
- マージ判断:
- 必要な Evidence の保存・取得・必須内容の閲覧確認:
- 取得案内の引渡し確認:
- harvest: §0 harvest to のとおり
- 削除確認:
- 通常の task 終了・後始末の状態と未完了事項:
