# 汎用の Content 要求・候補・記録の契約（S0-a）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `D 待ち`（**判定 C は GO、C' は 2 巡目で PASS（2026-11-08。§7・§8）。次は Phase D（owner のマージ判断）。** Phase A は A3 凍結版（revision 0、owner の指示で 2026-11-08 に凍結）のまま変えていない。A2 入力版は commit `d5f15d3`。本版は A2（§5）、外部の計画レビュー R1（対象 `d5f15d3`）・R2・R3（対象 `a2c6efe`）・R4（対象 `3734f91`）の採用分と、owner の Q1〜Q7 の裁定（R1 の推奨どおり）を反映した版。凍結後に設計判断を変える場合は Phase A を新しい revision として再開する。Phase A の commit は A2 入力版 `d5f15d3`、A3 整理版（`15c2469`〜`3734f91`。外部レビューの入力に使うための owner 指示による逸脱）、A3 凍結版（本版を含む commit）。program [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S0-a。A0 の入力は同書 §3〜§6・§9・§10 U1・M-03・M-06。S0-b（`CONTENT_DEMAND_ROUTING_SPIKE`、別ブランチで判定 C 中）と S0-c とは独立で並行（同書 D11））
- branch: `claude/adoring-galileo-4afnfa`（S0-a のスライスブランチ。PR の base は `develop`）
- implementation base commit: `5f971da`（`develop` の head、2026-11-08。本書の実測は全てこの base）
- implementation head commit: `7aa249d`
- risk: `normal`（program §8 のとおり。既存の公開 API・asmdef・所有者・寿命・`SceneState`・Addressables・起動順序を変えず、任意の層に新しい型を足すだけ。新しい公開面を足すことを「公開 API を変える」高リスクには数えない。owner 裁定 2026-11-08（§5 Q1、R1 の推奨どおり））
- owner: root（依頼者）。A2 / B / C / C' の担当は Phase ごとに選ぶ
- created: 2026-11-08
- expires: 2026-12-31、または Phase D で harvest・削除した時点。期限時は owner が更新・廃止を判断する
- harvest to: `unity/Assets/Docs/Architecture/13-resource-system.md`（要求時解決の任意の層と所有の契約。§2 決定事項サマリと §5 Interface 設計に節を足す。`IAssetManagement` に member を足さなかった理由、規則の製品実装が S1 にある理由、ロード元の同一性が S3 まで未検証であることを含める）、`27-folder-structure.md` §2.2（`Runtime/ContentResolution/` の依存位置: AssetManagement の公開面と共通契約だけに依存し、BuildSystem 本体・SceneSystem・Bootstrap を参照しない、AssetManagement の上の任意の層）、`unity/Assets/README.md`（プロジェクト構造の木と AssetManagement の節）。program の harvest（§18 §4 等）は program の完了時
- 関連: [ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)（**所有境界の正本。** resolver は AssetManagement の上の任意の層で、BuildSystem 本体を参照しない。同書 §2.1 の「連携部分（両方を知る）」とは別の分類として扱い、U-A / U-B の P 検査の集合に参照を増やさない。境界を本書で定義し直さない）、[RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md](RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md)（本スライスは起動コードに触れないので順序制約の対象外。§2.4）、S0-b `CONTENT_DEMAND_ROUTING_SPIKE`（ブランチ `claude/play-content-s0b-spike`、判定 C の再実行待ち。本書は S0-b の 2 機構に中立な形にしてある。§5「S0-b ブランチの確認」）
- Phase A snapshot の取得案内・状態: 確認済み（ローカル固定、未昇格）。発見 C の session が同じ PC から `evidence fetch` で取得し、収録された本書の内容 hash が A3 凍結 commit `30c98a7` の本書と一致することを確かめた。取得案内の reference と期待 packageSha256 は owner の手元と起動指示にあり、本書には転記しない。
- Phase B result snapshot / evidence bundle / C' blind bundle の取得案内・状態: 判定 evidence bundle、C' blind bundle、C' の所見の bundle の 3 つはローカル固定済み（未昇格）。Phase B result は単独の snapshot を作らず、blind bundle の `B-implementation-notes.md`（commit `7aa249d` の票の「実装の読み方」の写し）で渡した。取得案内は owner の手元（判定・C' の担当の報告）にあり、本書には転記しない。C' の担当の入力は、この session が `evidence fetch` で検証して取り出した写しで、別 session からの取得確認は未実施（C' の担当の実行環境では store に書けないため）。Phase D で別 session から取得と閲覧を確認する。
- 引継ぎ票:
  - 次の Phase・役割: **Phase D（owner のマージ判断と後始末）。** 判定 C は GO、C' は 2 巡目で PASS。implementation head は `7aa249d` のまま変わっていない。
  - 次の作業: 開始時に、渡された SHA と HEAD を照合する（implementation head 欄は SHA で、そこから HEAD までの差分は `docs/handoff/` の本書と決定記録だけ）。Phase D の手順: Evidence の取得・閲覧の確認（別 session から）、`evidence schedule status` が `passed` であること、CI の `dotnet-offline-tests` の緑（AC7）、push、harvest（§0 の harvest to）、本書と決定記録の削除、`workflow-task.ps1 end`。判定 C と C' の所見の突き合わせは下の §7・§8 で行う。
  - owner が Phase D で確認すること: DR-2（AC6 の「合格」を XML で読む）の結果として、`run-tests.ps1` の `step.json` は base と head で `failed`（Inconclusive 25 件）で、base と head 3 回目は XML 完成後に Unity を強制終了している。head の全 EditMode 回帰は 3 回実行し、1・2 回目は同じ 1 件が失敗している（§7）。
  - 実装の読み方（C が照合に使う。凍結条件は変えていない）: §7 より前の版の票と同じ。commit `7aa249d` の本書 §0 を正とする。
  - 引き継ぐ commit: この票を含む C' の記録 commit（SHA は tree の外、起動指示と PR #171 の状態ブロックで渡す）。
  - 必要な権限: ローカルの Artifact Storage の `fetch`、`evidence schedule status`、`git push`、PR の更新。
  - 停止規則: program §0 の停止規則を引き継ぐ。新しい worktree で Unity が import すると、`*.pdb` が git 管理外なので `unity/Assets/MobileDependencyResolver/Editor/` の `*.pdb.meta` 5 件を消す。base でも同じ。`git restore` で戻し、commit に入れない（AC2）。
  - 引き渡さないもの: なし（C' は完了した）。

### PR 状態ブロック

PR を作る前は本 §0 を唯一の正本とする。PR 作成時にテンプレートの形で本文先頭へ写す。

## 1. 目的と対象外

- 目的: Scene や Asset のロード要求に対して、供給元の種類・所在・取得手順を知らない呼び出し側が、要求時に妥当な候補を得て、既存の AssetManagement と同じ契約で所有・解放でき、何をどの供給元から得て何が失敗したかを説明できるようにするための、汎用の要求・候補・規則・記録の契約を定義する。SceneDirector、SampleGame、BuildPurpose を知らない fake の供給元だけでこの契約が成り立つことを示す。契約は AssetManagement の必須依存にせず、この契約を使わない既存の AssetManagement と BuildSystem の単独利用と併用を変えない。
- 利用者: OSMFramework の開発者（この契約の上に、供給元の adapter、SceneSystem adapter、Editor の Play 入口を後続スライスで実装する人）。このスライスに利用者の操作は無い。
- 目的・利用者の文言の改訂（A1）: A0（program §1・§8 S0-a 行）から A1 で書き起こした初出の文言。A2 の目的レビューはこの原文を見た（§5 A2 記録）。A2 の目的レビューは文言への指摘 5 点を出した。owner は 2026-11-08 に「文言は据え置き、意味は下の『読み方』で固定する」と裁定した（§5 Q6）。文言を変えていないので目的レビューのやり直しは要らない。
- 目的の文言の読み方（A3 整理。文言は変えず、曖昧と指摘された点の本書での意味を固定する。owner 裁定 Q6）:
  - 「同じ契約で所有・解放」= `AssetOwner` の 4 スコープ（App / Manual / Scene(id) / Bind(go)）と、`IAssetManagement` の既存の解放 API（`Release`、`UnloadSceneAsync` → `ReleaseScene`、`ReleaseAll`）とその既存の失敗契約。本スライスはこれらに新しい意味・API・例外を足さない。
  - 「妥当な候補」= 供給元が「ある」と答え（利用可能）、かつ規則（`IContentCompatibilityRule`。製品実装は S1）が「採ってよい」と答え、かつ要求の logical key・kind・表現・expected identity と矛盾しない候補。妥当さを決めるのは規則で、供給元は候補を申告するだけ、要求は制約を与えるだけ。
  - 「何が失敗したか」の粒度 = 供給元（provider）単位の probe の結果（不在・不達・整合性拒否・規則で不採用・不一致・fault）と、採用後のロード失敗（AssetManagement の既存の reason code）。取得手順の段階（download / verify / install）は S4 の adapter の内側で、本スライスの record は provider 単位。
  - 「Scene や Asset」= 契約は共通（`ContentKind` で区別する 1 つの要求型）。Scene の lifecycle（SceneSystem adapter）は後続（S3）で、本スライスは `IAssetManagement` の Scene / Asset の入口へ繋ぐまで。
  - 「併用を変えない」= AssetManagement と BuildSystem の二者の併用（現在の OSM、U-C）。本契約を加えた三者の併用は S3 以降。
- 対象外（所有する後続。§2.3 に一覧）:
  - Editor で開いている Scene の snapshot の機構、既ロード Scene の取り込み方、I11（開いている Scene を壊さない）: S0-b
  - `InstallContentDirectory` の 2 件目拒否の規則（directory の基数）: S0-b の結論の直後に切る製品スライス（program U1 (2)）
  - 互換 fingerprint の中身（identity / digest / target / schema の汎用判定、`declaration` の述語）: S1
  - logical key → package の索引、Stage II の粒度: S2
  - provider route → install → Unity load → 所有の接続、SceneDirector 経由の経路、`InstantiateContentAsync` 相当の経路、採用した候補と実際にロードした directory の同一性の検証、既ロード Scene の再利用と record の関係、既存 index の表現 fallback を record に写すこと: S3
  - Local / LAN の供給元 adapter、timeout、prefetch、checkout hook、要求をまたぐ pin（再試行で別 revision へ切り替わらないこと）: S4
  - Play ボタンの入口、通知、観測 UI、menu、record の保存・可視化・送信: S0-c / S5
  - 本スライスの folder を別 asmdef に分けること、P 検査の常設化: asmdef 分割スライス、[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md) D4
  - 実在の供給元 adapter（installed cache、LAN、Addressables、Editor snapshot）: S4 / S0-b。本スライスは fake だけ
- 現況（base `5f971da` の実測。path は `unity/Assets/OneStarMaker/Scripts/` からの相対）:
  - `Runtime/AssetManagement/IAssetManagement.cs`（75 行、public）: Content Directory の型付き入口は `LoadContentAssetAsync<T>(logicalKey, representation, owner)`、`LoadContentSceneAsync(sceneIdentity, representation, options)`、`InstantiateContentAsync(logicalKey, representation, parent)` の 3 つ。どれも「既に入っている 1 つの directory」から logical key と表現で読むだけで、供給元の探索も、どこから得たかの記録も返さない。provider・候補・policy に当たる型は無い。
  - `Runtime/AssetManagement/AssetManagement.ContentDirectory.cs`（164 行）: `_contentDirectory` は単一。`InstallContentDirectory` は internal で、2 件目を `InvalidOperationException` で拒否する（20〜22 行目）。2 件目の拒否はもう 1 か所、`Runtime/BuildContent/ContentRevisionGate.cs` の `Reserve`（process 単位の static な登録。path・revision が違っても 2 件目を拒否し、`ContentDirectorySession.RegisterCore` が必ず通る）にもある（S0-b の A3 と判定 C が確認。directory の基数は両方を扱う。program U1 (2)）。`RequireContentDirectory` は未登録なら `DirectoryNotRegistered` を投げる。`LoadContentSceneAsync` は同じ identity が台帳にあれば既存の handle を返し（116〜124 行目）、表現は `_contentSceneRepresentations` に要求のまま記録する（127 行目）。owner 台帳（`AssetRegistry`）と resident cache は AssetManagement が唯一の owner で、directory 由来の token は `IContentDirectoryToken` marker で見分ける。
  - `Runtime/AssetManagement/Internal/IContentDirectoryBackend.cs`（36 行、internal）: 1 directory 分の port。`BuildIdentity` / `Target` / `CachePrefix` と load / unload / release / close / Play 停止の操作を持つ。候補の探索は無い。`BuildIdentity` は internal で、`IAssetManagement` / `ISceneHandle` / `IAssetHandle` は directory の identity を返さない。
  - `Runtime/BuildContent/ContentDirectorySession.cs`（481 行、internal）: 1 directory = 1 root の登録、native handle、token、unregister を所有する連携部分。`Register` / `RegisterVerified` は internal。`Runtime/BuildContent/Distribution/InstalledRevisionVerifier.cs` の `VerifiedInstalledRevision`（internal）が「検証済み候補」の形（`Identity`、`ContentSet`、`Target`、`ManifestSha256`、`ContentPath`）を持つ。`Runtime/BuildContent/ContentDirectoryIndex.cs` は要求表現の Scene entry が無ければ `Full` へ fallback する（93〜96 行目）。
  - `Runtime/BuildContent/Distribution/ContentTransportManifest.cs`: `declaration`（名前・表現・runtime 既定値）は optional で検証されない（program §2.4）。
  - `Runtime/SceneSystem/SceneDirector.Loading.cs` 629〜656 行目 `PerformUnitySceneLoad`: Runtime で `LoadContentSceneAsync` を呼ぶ唯一の呼び手。表現は起動時に固定した `_sceneVariant`。名前で `SceneManager.GetSceneByName` に当たれば既にロード済みとして再利用し、由来は区別しない。
  - 共通契約 `Runtime/AssetBuildContracts/`（`AssetType`、`ContentDirectoryException` / `ContentDirectoryFailureCode`）は PR #165 で切り出し済み。AssetManagement と BuildSystem 本体は互いの型を参照せず、両方を知るのは連携部分（[27-folder-structure.md](../../unity/Assets/Docs/Architecture/27-folder-structure.md) §2.2）。`AssetType` の namespace は `OneStarMaker.Runtime.AssetManagement` のまま。
  - 同じ assembly の中では internal が見える: `Runtime/ContentResolution/` を `OneStarMaker.Runtime.asmdef` の配下に置くと、AssetManagement の internal（`InstallContentDirectory`、`AssetRegistry`、`IAssetBackend`、`SealedAssetBackend` 等）をコンパイルエラー無しで呼べる。本スライスはこれを AC3・AC4 で塞ぐ（A2 の指摘 A-05）。
  - テスト: `OneStarMaker.Tests`（EditMode、`Tests/AssetManagement/`）は `FakeAssetBackend`（internal `IAssetBackend` の fake）で Addressables に依存せずに AssetManagement を検証している。`IContentDirectoryBackend` の fake は `Tests/AssetManagement/SealedAssetBackendTests.cs`（`FakeDirectoryPort`）、`Tests/Scene/SceneVariantForwardingTests.cs`（`DirectoryScenePort`）、`Tests/Editor/Build/ContentDirectorySessionTests.cs` の 3 箇所に各 file 内の private 型としてあり、共有できない。既存テストは型名 `AssetManagement` と namespace の衝突を `Runtime.AssetManagement.AssetManagement` の完全修飾で避けている（`AssetManagementTests.cs` 38 行目）。Unity を起動しない .NET 8 の offline テストは `tools/*OfflineTests/`（4 project）にあり、Runtime の pure な `.cs` を `<Compile Include>` で link して `dotnet run` で走らせ、CI の `.github/workflows/offline-checks.yml` の `dotnet-offline-tests` job が project 名の matrix で回している。
  - P 検査（Unity 同梱 `csc` によるソース集合の閉包検査）の script `p-check.ps1` は ASSET_CYCLE スライス（PR #165）の Phase A snapshot に凍結されていて、git には無い。集合の正本は同スライスの HANDOFF の表（U-A: `Runtime/AssetDescriptions/` + `Runtime/AssetManagement/` + `Runtime/AssetBuildContracts/`。U-B runtime: `Runtime/BuildContent/` から連携 2 file と OSM 固有 2 file を除く + 共通契約）で、[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md) §5.1 に写しがある。script は集合の library を別の集合の参照に使える（`UB-content` が `UB-selection` 等を参照する）。
  - 本書の作成環境に Unity・pwsh・dotnet が無く、`tools/docs-audit.ps1`・`tools/contract-audit.ps1` は未実行。link 先の存在は `git ls-files` で手で照合した。PR 作成前に owner 側で通す。
- UC ごとの利用者の入力: なし（利用者の操作は無い）。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: SceneDirector や BuildPurpose が無くても、要求・候補・所有の契約を定義できるか。既存の `IAssetManagement` / `IContentDirectoryBackend` / DIST で何を再利用でき、新しい interface は本当に要るか。
- 問いへの A1 の答え（A2 で検証済みの仮説）: **要る。ただし小さな任意の層として。** `IAssetManagement` は「入っている 1 つの directory から logical key と表現で読む」契約で、供給元の探索・採用理由・由来の記録を返さない。これを `IAssetManagement` の全 load API に組み込む案（program §5.1 案 B）は I8（単独利用）に反する。再利用するのは、所有・解放・同時ロードの調停（`IAssetManagement` の公開 API と `AssetOwner` をそのまま使い、所有の契約を定義し直さない）、失敗の reason code（`ContentDirectoryException` を包まず、そのまま呼び出し側へ通す）、検証済み候補の「形」（`VerifiedInstalledRevision` の identity / contentSet / target / digest の欄を候補の `ContentIdentity` に写す。型そのものは internal で BuildSystem 本体に属するので参照しない）。新しく要るのは、要求（`ContentRequest`）、供給候補とその可否（`IContentProvider` / `ContentAvailability` / `ContentCandidate`）、規則（`ResolutionPolicy` と S1 が実装を入れる `IContentCompatibilityRule`）、探索の実績と由来（`ContentResolutionResult` / `ContentLoadRecord`）の型である。`IContentDirectoryBackend` には何も足さない（directory の port は 1 directory を読む口で、探索の口ではない）。resolver は時間・乱数・外部状態を持たず、同じ要求・同じ probe の回答・同じ規則なら同じ結果を返す（決定性）。
- どうすればこの問いに誤って YES を出せるか（反例。A2 と C はこれを探す）: (a) fake provider のテストが通るだけで「SceneDirector 無しで使える」と言う。実際に AssetManagement へ繋いで Scene / Object を得て解放する経路（G03 (b)）を別に示さなければ YES にしない（AC5）。(b) resolver 無しの既存テストが通るだけで汎用性を言う（G03 (a) と (b) は別の証拠。AC6）。(c) 候補に由来（供給元の種別・provider）が無くても成功にする（AC8 T7）。(d) 新しい型を 0 にして `IAssetManagement` の内側で探索を済ませる（I8 違反を「新しい interface は要らなかった」と読む。AC2・AC3）。(e) SampleGame の宣言名・Scene 名・Season・Cell・VCS の語彙を汎用型に入れて通す（AC9）。(f) 優先順位が高い供給元の候補をそのまま採用し、利用可能・互換の判定を飛ばす（I5 違反。AC8 T2）。(g) 失敗した供給元の後に別 revision の候補が見つかったら採用する（program §4.1 の fail-closed 違反。AC8 T3）。(h) loader が実装していないロード経路（既ロード Scene、Addressables）の候補を directory から読み、record に申告の由来を書く（由来の虚偽。AC5 E6）。(i) loader が同じ assembly の AssetManagement の internal を呼んで封鎖や台帳を迂回する（AC3・AC4）。
- 判定定義: §2.1 の不変条件と成立条件を §2.2 の受け入れ条件で全て確認できれば GO。一つでも未達なら NO-GO。スパイクではないので CONDITIONAL ACCEPT は使わない。
- このスライスの成果の範囲: GO は「要求・候補・規則・記録・所有の契約が、AssetManagement の必須依存にならずに、fake の供給元と既存の AssetManagement の上で成立した」ことだけを意味する。実際の供給元（installed cache、LAN、Editor snapshot）からの解決、SceneDirector との接続、採用した候補と実ロード元の同一性、Play 入口の成立は主張しない。program §8 の「誤った合格を除外する」の列をそのまま引き継ぐ。**record が保証するのは選択結果まで**（どの要求に、どの供給元に何と答えられ、どの候補をどの受け入れ根拠で採用したか）で、実ロード元を確認した証拠ではない（`SourceBindingVerified=false`）。要求・候補・採用結果・実ロード・ロード失敗は別の事実として扱い、S0-a が証明するのは前 3 つの対応だけ。採用後のロード失敗（`ContentDirectoryException` 等）で `Probes` と採用候補の対応を呼び手に残すことは S0-a の非保証で、S3 の入力（§2.3。R1 の 5）。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: [版 1](GENERIC_CONTENT_REQUEST_CONTRACT_DECISIONS.md)（2026-11-08、implementation head `7aa249d` に適用。発見 C の D-1 の移送と I5 の読み方の確定）

### 2.1 進める最低条件

**不変条件（起きてはいけないこと）**

- **I1 要求の不変:** 呼び出し側が与えた logical key・kind・表現・expected identity を、resolver も provider も変えない。候補の logical key または kind が要求と違えば採用しない。要求の表現が空でなければ候補の表現は完全一致でなければ採用せず、候補の表現が空なら採用しない（loader が空の表現で `LoadContent*Async` を呼ぶと、既存 index の `Full` fallback が実際の表現を変え、台帳には空が記録される）。要求に expected identity があれば、それを最初の pin とする（I2 の比較）。候補の `ProviderId` と `SourceKind` は、その候補を返した provider の `Id` と `SourceKind` に一致しなければ採用しない（record の由来が probe した供給元と食い違わないため。R1 の 2）。
  - 崩しうる経路: (a) provider が別の logical key・kind・表現の候補を「見つかった」と返す。(a') provider が別の provider 名や別の種別を名乗る候補を返す。(b) 既存 `ContentDirectoryIndex` の Scene の `Full` fallback（[18-asset-description.md](../../unity/Assets/Docs/Architecture/18-asset-description.md) §4）が、資源を得た後の実効表現を要求と変える。候補の表現を空にしないことで loader が空を渡す経路は塞ぐが、候補が申告した表現の entry が無いときの fallback は AssetManagement の内側にある既存の挙動で、本スライスは変えず、record の表現は provider が申告した表現とする。実効表現を record に写すことは S3 の入力（§2.3）。(c) SceneDirector が名前で既ロード Scene を再利用する経路（`PerformUnitySceneLoad`）。本スライスは SceneDirector に触れない。(d) 要求の expected identity と違う identity の候補を規則が採用する。resolver は規則の前に pin で弾く。
  - 探索した境界: `IAssetManagement` の 3 つの content 入口と `ContentDirectoryIndex.TryResolveScene` / `TryResolveObject`、`SceneDirector.Loading.cs` の呼び出し 1 箇所、`AssetManagement.ContentDirectory.cs` の表現の台帳。未確認: なし。
- **I2 未検証の候補を採用理由にしない:** resolver は候補を「採用する」だけで、bytes を取得・install・Build しない。候補は**受け入れ根拠**（`AcceptanceBasis`: `None` / `VerifiedDigest`（manifest と digest の検証を通した正規コピー）/ `OpenSceneSnapshot`（Editor で開いている Scene の複製。意味は S0-b が定める））を申告し、record に残す。根拠が `None` の候補は規則が採用しても採用しない。供給元の種別ごとに要る根拠は resolver の固定表で決め（`ContentDirectory` → `VerifiedDigest`、`EditorSnapshot` → `OpenSceneSnapshot`、`Addressables` → 本スライスでは定めず、採用しない（S3））、種別と根拠が合わない候補も採用しない。この表は「どの根拠なら使ってよいか」の境界で、S1 の互換判定（identity / digest / target / schema の一致）ではない（R1 の 3）。供給元が `Rejected`（hash 不正・version 不整合・権限拒否）を返したら、その回答が **4 欄（`ContentSet` / `Revision` / `Target` / `Digest`）のそろった** expected identity を伴う場合だけ探索を続け、同じ要求の中では、その identity と全欄が一致し、かつ根拠が `VerifiedDigest` の候補以外を採用しない（同じ expected digest の正規コピーでの修復。別 revision へ黙って切り替えない。program §4.1）。pin と identity が違う候補は `PinMismatch` として記録して**次の供給元へ進む**（修復が供給元の並び順に依存しないため。R3-1）。identity は pin と一致するが根拠が `VerifiedDigest` でない候補は `NoAcceptanceBasis` として記録して次へ進む。`Rejected` が expected identity を伴わない（権限拒否等で修復の基準が無い）場合は、その場で `IntegrityRejected` として探索を止める（R1 の 4）。pin にできる identity は 4 欄が全て非空の物だけで、一部の欄だけ入った identity は `ContentRequest.ExpectedIdentity` でも `Rejected` の `ExpectedIdentity` でも constructor が拒む（R2-2・R3-3）。pin の比較は全欄一致で、digest が同じでも target 等の他の欄が違えば不一致とする。pin が複数あって食い違えば、その場で `IntegrityRejected` として探索を止める。pin の供給源は要求の `ExpectedIdentity` と identity 付きの `Rejected` の 2 つで、どの組み合わせでも同じ: 要求の pin が X のとき `Rejected`（X'）はその場で止め（`PinnedIdentity` は X のまま、後続は probe しない）、`Rejected`（X）なら探索を続ける（R4-1）。pin の無しは `ContentIdentity?` の null で表し（`ContentRequest.ExpectedIdentity`、`Rejected` の `ExpectedIdentity`、`ContentResolutionResult.PinnedIdentity`）、`IsEmpty` の struct を pin として渡すのは `ArgumentException`。pin は 1 要求の中だけで持続し、`ContentResolutionResult.PinnedIdentity` として呼び手に渡す。要求をまたぐ pin（再試行で別 revision へ切り替わらないこと）は S4 の入力（§2.3）。
  - 崩しうる経路: (a) provider が `AcceptanceBasis.VerifiedDigest` を偽って申告する（契約では防げない。S4 の adapter の責任で、本スライスは record に申告のまま残す）。(b) 探索を打ち切らず、下位の供給元の別 identity を採る。(c) record を省略して成功を返す。(d) 本スライスの loader が、採用した候補と無関係に「入っている directory」から読む。`IAssetManagement` は directory の identity を公開しないので、本スライスはこの同一性を検証できない。record はこれを `SourceBindingVerified=false` として明示し、S3 がこの接続を作るときに閉じる（§2.3）。(e) 空の identity 同士、または digest の無い identity 同士が「同じ」と判定されて修復が成立する。(f) 根拠の無い候補、または `VerifiedDigest` でない候補で修復が成立する。(f') 全件採用する規則の下で、根拠 `None` の directory 候補がロード成功として扱われる。(g) 同じ要求の再試行で、先に `Rejected` だった供給元が `Unavailable` になり、下位の別 revision が採用される（1 要求の pin では防げない。S4）。
  - 探索した境界: `IContentDirectoryBackend.BuildIdentity` は internal。`IAssetManagement` と `ISceneHandle` / `IAssetHandle` は identity を返さない。本スライスの `ContentResolver` の 1 経路。未確認: なし。
- **I5 優先度・利用可能・互換の分離:** 供給元の順序（policy）、候補があること（provider の回答）、候補を採ってよいこと（規則）は別の 3 つの判定で、record は probe した供給元ごとに、何と答えられ、なぜ採らなかったか（不在・不達・整合性拒否・規則で不採用・pin 不一致・key 不一致・kind 不一致・表現不一致・provider 不一致・受け入れ根拠なし・fault）を機械で区別できる形で残す。採用した時点で探索を止め、以後の供給元は probe しない。provider または規則が `OperationCanceledException` 以外の例外を投げたら、その probe を fault として記録して探索を止め、`ProviderFault` で失敗する（fail-closed）。**探索を止めるのは**、採用、expected identity を伴わない `Rejected`、pin 同士の食い違い、`Fault`、取消の 5 つだけで、それ以外の不採用（`NotFound` / `Unavailable` / `RuleRejected` / `PinMismatch` / `KeyMismatch` / `KindMismatch` / `RepresentationMismatch` / `ProviderMismatch` / `NoAcceptanceBasis`）は記録して次の供給元へ進む。取消は各 probe の前に見て、前の probe が戻った時点で token が取消済みなら次の供給元を呼ばない。`OperationCanceledException` に `ContentResolutionResult` は付けない。取消と数えるのは token の `IsCancellationRequested` が真のときだけで、token が取消されていないのに provider が投げた `OperationCanceledException` は他の例外と同じ `Fault` として記録し、`ProviderFault` で止める（R4-6）。各 probe の `Outcome` は「その候補を採らなかった理由」、要求の `FailureCode` は「要求が終わった理由」で、誰も採用しなかったときの `FailureCode` は次の優先順位で 1 つに決める（R3-1）: (1) 未充足の pin がある（`PinMismatch` が 1 件でもある、または digest 付き `Rejected` の後に誰も採用していない）、または expected identity の無い `Rejected`、または pin の食い違い → `IntegrityRejected`。(2) それ以外で `KeyMismatch` / `KindMismatch` / `ProviderMismatch` / `Fault` が 1 件でもある → `ProviderFault`。(3) それ以外で `RuleRejected` / `NoAcceptanceBasis` / `RepresentationMismatch` が 1 件でもある → `NoCompatibleCandidate`。(4) 残り（`NotFound` と `Unavailable` だけ）→ `NoCandidate`。
  - 崩しうる経路: 最初に候補を返した供給元を規則を通さずに採る。規則で落ちた候補を次の供給元へ進まずに失敗にする。規則が例外を投げて `ContentResolutionResult` も記録も作られずに抜ける。
  - 探索した境界: 本スライスの `ContentResolver` の 1 経路。未確認: なし。
- **I7 汎用:** 本スライスの型・テスト（`Runtime/ContentResolution/**`、`Tests/ContentResolution/**`、`tools/ContentResolutionOfflineTests/**`）に SampleGame の Scene 名・Cell・Season・職種・RunPurpose・宣言名・VCS・SceneResourceMap・UICommon・Workset が現れない。SceneResourceMap / UICommon の起動時特例は host の policy として登録するもので、`ContentRequest` に入れない（program M-06）。
  - 崩しうる経路: テスト fixture に Spring 等の実名を使う。`ContentKind` や `ContentSourceKind` に host 固有の値を足す。
  - 探索した境界: 新規 file 全部（AC9 の grep）。未確認: なし。
- **I8 単独利用の非退行:** AssetManagement と BuildSystem 本体は本スライスの型を参照しない。`IAssetManagement`、`IContentDirectoryBackend`、`AssetOwner`、`IAssetHandle` / `ISceneHandle`、`ContentDirectoryException` / `ContentDirectoryFailureCode` の宣言を変えない。既存テストの assertion・名前・本文を変えない。本スライスの製品 code は AssetManagement の public だけを使い、同じ assembly にある internal（`InstallContentDirectory`、`AssetRegistry`、`IAssetBackend`、`SealedAssetBackend`、`IContentDirectoryBackend`、`AssetKey.FromContentDirectory` 等）を呼ばない。
  - 崩しうる経路: (a) 「由来を取るため」に `IAssetManagement` や `ISceneHandle` に member を足す。(b) `InternalsVisibleTo` や asmdef の参照を足す。(c) 既存の fake（`FakeAssetBackend`、各 `FakeDirectoryPort`）を本スライス向けに書き換える。(d) loader が `DirectoryNotRegistered` を受けて internal の Addressables backend を直接作り、directory mode の封鎖を迂回する（同じ assembly なのでコンパイルは通る。AC2 は新規 file の中身を見ない）。
  - 探索した境界: `Runtime/AssetManagement/**`、`Runtime/BuildContent/**`、`Runtime/OneStarMaker.Runtime.asmdef`、`Tests/**/*.asmdef`、本スライスの新規 file（AC3 の grep と AC4 の `RESOLVER-*` 集合: U-A を DLL にしてから resolver だけをその DLL 参照でコンパイルするので、internal の利用はコンパイルエラーになる）。未確認: なし。
- **I10 既存の fail-closed を弱めない:** `Runtime/Bootstrap/**`、`Runtime/AssetManagement/**`、`Runtime/BuildContent/**`、`Runtime/SceneSystem/**`、`SampleGame/**`、`.asmdef`、`ProjectSettings/`、`Packages/` を変えない。新規 file からも既存の封鎖・台帳を迂回しない（I8 (d)）。
  - 崩しうる経路: 便乗の変更。Unity Editor が import 時に書き換える file を一緒に commit する。I8 (d)。
  - 探索した境界: AC2 の変更集合の許可リストと AC3・AC4。未確認: なし。

**成立条件（起きなければならないこと）**

- **S1 契約の存在と Unity 非依存:** §3 の core の型が `Runtime/ContentResolution/Core/` に存在し、UnityEngine・UniTask・OSM の他の部分を参照せず、.NET 8 の offline テスト project（`tools/ContentResolutionOfflineTests/`）が core の `.cs` だけを link してビルド・実行できる（exit 0、1 件以上実行、failed 0）。
- **S2 fake provider での契約の成立:** offline テストが §5 の T1〜T26（T12b・T12c を含む 28 件）を全て通す（優先順位、利用可能・互換の分離、fail-closed、同 identity での修復、由来の無い成功の拒否、受け入れ根拠、要求の不変、provider の一致、Object kind、cancel、fault、汎用性の文字列検査）。
- **S3 SceneDirector 無しの AssetManagement との合成（G03 (b)）:** EditMode テストで、`AssetManagement`（`FakeAssetBackend`）に fake の `IContentDirectoryBackend` を install し、fake provider 1 つの policy で `ContentRequestLoader` が Scene と Object（`GameObject`）を要求し、handle と由来付きの record を得て、`IAssetManagement` の既存 API で解放できる。所有の観測は Scene と Object で分ける（R4-3）: Object は、fake port の `GetObjectKey` が返す canonical key で `IAssetDiagnostics.GetOwners` が `LoadAssetAsync` に渡した owner と一致し、`Release` の後に空。Scene は、既存の `LoadContentSceneAsync` が scene 台帳に載せるだけで asset 台帳に `AssetOwner` を取らないので `GetOwners` では観測せず、record の `OwnerLabel` と `UnloadSceneAsync` → `ReleaseScene` の経路（fake port の unload 回数）で見る。SceneDirector・SceneResourceMap・SampleGame の型は使わない。
- **S4 resolver 無しの非退行（G03 (a)）:** 既存の `Tests/AssetManagement/*` と `Tests/Editor/Build/*` と `Tests/Scene/*` を変えずに全て通す。U-A・U-B の P 検査の集合は変えずに成功し、`RESOLVER-player` / `RESOLVER-editor` 集合（`Runtime/ContentResolution/` だけを、U-A + 共通契約の library を参照してコンパイル）が成功する。
- **S5 回帰:** 全 EditMode 回帰（空 filter）と `pwsh tools/contract-audit.ps1` が合格し、CI の `dotnet-offline-tests` matrix に新 project が入っている。

### 2.2 受け入れ条件

合否は各 AC の本文で決める。「確認手段」は合否を見る手段で、同じ意味を別の手段で示してもよい。

- **AC1（S1）core の純粋さ:** `Runtime/ContentResolution/Core/*.cs` の各 file が `#nullable enable` で始まり、`using UnityEngine`、`using Cysharp`、`using OneStarMaker.Runtime.AssetManagement`、`using OneStarMaker.Runtime.BuildContent`、`using OneStarMaker.Runtime.SceneSystem` のどれも持たず、`record` 宣言と `init` accessor が無い。`tools/ContentResolutionOfflineTests/ContentResolutionOfflineTests.csproj` が core の file だけを `<Compile Include>` で link し、`PackageReference` を持たず、`<LangVersion>9.0</LangVersion>` を持ち、`dotnet run -c Release --project tools/ContentResolutionOfflineTests/ContentResolutionOfflineTests.csproj` が exit 0 で、出力の末尾の件数行が `failed 0` を示す（件数の下限は AC8 のテスト名の照合で見る）。
  - 確認手段: file の先頭と `using` を読む。`dotnet run` の出力。
  - 不合格の例: core が `UniTask` を使い、csproj に UniTask の `PackageReference` を足して通す（Q2 で `ValueTask<T>` に決めた）。
- **AC2（I8, I10）変更集合の許可リスト:** base から head までの変更（全 path）が次の 7 種類だけである。これ以外の追加・変更・削除が 1 件でもあれば不合格。
  1. 新規 `unity/Assets/OneStarMaker/Scripts/Runtime/ContentResolution/**`（`.cs` と `.meta`、`Core/` の folder `.meta`）と、その兄弟の folder `.meta` `unity/Assets/OneStarMaker/Scripts/Runtime/ContentResolution.meta`。
  2. 新規 `unity/Assets/OneStarMaker/Tests/ContentResolution/**`（`.cs` と `.meta`）と、兄弟の folder `.meta` `unity/Assets/OneStarMaker/Tests/ContentResolution.meta`。
  3. 新規 `tools/ContentResolutionOfflineTests/`（csproj と `Program.cs`。`bin/` `obj/` は `.gitignore` 済みで commit に入らない）。
  4. `.github/workflows/offline-checks.yml` の `dotnet-offline-tests` matrix への 1 行の追加。
  5. `docs/handoff/` 配下の HANDOFF の記録（本書と program §8 の S0-a 行のリンク）。
  6. harvest 先の公開文書（Phase D）。
  7. なし（予備。許可リストを増やすときは A3 の決定記録か Phase A の revision）。
  - 確認手段: `git diff -M --name-status <base> <head>` の全行を照合する。既存 `.cs`・`.asmdef`・`ProjectSettings/`・`Packages/`・`SampleGame/`・既存テストの変更が 0 件。
  - 合格の例: `A unity/Assets/OneStarMaker/Scripts/Runtime/ContentResolution.meta`（兄弟の folder `.meta`）。
  - 不合格の例: `M unity/Assets/OneStarMaker/Scripts/Runtime/AssetManagement.meta`。`IAssetManagement.cs` に 1 行でも変更がある。`FakeAssetBackend.cs` を共有のために書き換える。Unity が書き換えた他の `.meta` を一緒に commit する。
- **AC3（I8, I10）宣言の不変と internal の不使用:** `IAssetManagement`、`IContentDirectoryBackend`、`AssetOwner`、`IAssetHandle` / `IAssetHandle<T>` / `ISceneHandle`、`ContentDirectoryException` / `ContentDirectoryFailureCode` の宣言（member の名前・signature・enum 値）が base と同一。`Runtime/AssetManagement/**` と `Runtime/BuildContent/**` のどの file にも `ContentResolution` の文字列が無い。`Runtime/ContentResolution/**` のどの file にも `AssetManagement.Internal`、`InstallContentDirectory`、`AssetRegistry`、`IAssetBackend`、`SealedAssetBackend`、`AddressableBackend`、`IContentDirectoryBackend`、`FromContentDirectory`、`SealAddressables` の文字列が無い（テスト `Tests/ContentResolution/**` は fake port のために internal を使ってよい）。
  - 確認手段: AC2 で変更 0 件なら前半は成立。`git grep -n "ContentResolution" -- unity/Assets/OneStarMaker/Scripts/Runtime/AssetManagement unity/Assets/OneStarMaker/Scripts/Runtime/BuildContent` が 0 件。`git grep -n -E "AssetManagement\.Internal|InstallContentDirectory|AssetRegistry|IAssetBackend|SealedAssetBackend|AddressableBackend|IContentDirectoryBackend|FromContentDirectory|SealAddressables" -- unity/Assets/OneStarMaker/Scripts/Runtime/ContentResolution` が 0 件。
- **AC4（S4, I8）P 検査:** head で、ASSET_CYCLE で凍結した `p-check.ps1` と同じ条件で、U-A・U-B の既存 7 集合が exit 0、陰性対照 2 つが exit ≠ 0（共通契約の欠落が理由）、加えて本書 §5 の集合 `RESOLVER-player` / `RESOLVER-editor`（`Runtime/ContentResolution/` だけを、同じ実行で作った `UA-player` / `UA-editor` の library を参照してコンパイル）が exit 0、陰性対照 `NEG-RESOLVER-without-assetmanagement`（`Runtime/ContentResolution/` + 共通契約のソース。U-A の library を参照しない）が exit ≠ 0 でその error が `IAssetManagement` / `AssetOwner` / `ISceneHandle` / `IAssetHandle` のいずれかの欠落を示す。
  - 確認手段: §5「P 検査」の方式で判定 C が script を用意し、`summary.json` と各集合の `files.txt` / `args.rsp` / `csc.log` を evidence に残す。
  - 不合格の例: `RESOLVER-*` を通すために集合へ `Runtime/SceneSystem/` や `Runtime/BuildContent/` や U-A の**ソース**を足す（ソースを一緒にコンパイルすると internal が見えて I8 (d) を検出できない）。陰性対照が別の理由（参照 DLL の欠落等）で失敗している。
- **AC5（S3）EditMode の合成テスト:** `Tests/ContentResolution/ContentRequestLoaderTests.cs` の E1〜E6（§5）が存在し、全て Passed。E1 は Scene を要求して `ISceneHandle.IsLoaded` が true、record の `SourceKind` が `ContentDirectory`、`ProviderId` が fake の id、`OwnerLabel` が `Scene(<identity>)`、`SourceBindingVerified` が false、`UnloadSceneAsync` → `ReleaseScene` の後に fake port の unload 回数が 1。E2 は `GameObject` を `AssetOwner.Manual` で要求して `GetOwners` に Manual が居り、`Release` の後に空。E4 は provider が NotFound のとき `ContentResolutionException` が投げられ、`Result.IsResolved == false`、`Result.FailureCode == NoCandidate`、`Result.Probes` が 1 件（`NotFound`）、`Rejection == None`、fake port の load 回数が 0（探す段とロードを分ける）。E5 は provider が Rejected（identity 無し）のとき `IntegrityRejected`、`Probes` 1 件（`Rejected`）、load 回数 0。E6 は resolver が採用する由来・根拠の組（`EditorSnapshot` + `OpenSceneSnapshot`、および `ContentDirectory` + `VerifiedDigest`）で `LoadPath` だけ `AlreadyLoadedScene` / `Addressables` にした候補を loader が拒み、例外の `Result.IsResolved == true`、`Result.Candidate` がその候補、`Result.Probes` が 1 件（`Adopted`）、`Result.FailureCode == None`、`Rejection == LoadPathUnsupported`、load 回数 0（resolver の成功を「候補が無かった」に書き換えない。R2-1・R3-2）。対照: `EditorSnapshot` + `OpenSceneSnapshot` + `LoadPath.ContentDirectory`（S0-b の機構 (ii): snapshot から build した一時 directory）は拒まず、fake port の `LoadSceneAsync` が 1 回呼ばれる（installed directory は 1 件なので対照はここまで）。
  - 確認手段: 結果 XML の実行テスト名と結果。
- **AC6（S4, I8）既存テストの非退行:** base と GO 候補 head のそれぞれで、同じ機械・同じ Unity 版・同じコマンド（`pwsh tools/run-tests.ps1`、空 filter、EditMode）で全 EditMode を実行する。base の結果は 1 件以上実行かつ failed 0 であること（そうでなければ比較の基準にしない）。head が合格し、head の結果 XML の実行テストのケース ID（parameterized テストの引数を含む full name）の集合が base の集合を含み、base で passed のケースが head で skipped / failed になっていない。`pwsh tools/contract-audit.ps1` 合格（検査 9 の path 予算を含む。最長の新 path は §2.4）。
  - 確認手段: 2 つの XML から full name と結果を抜き出して比べる。総数・passed・skipped・failed を記録する。base が failed 0 にならない場合は比較の基準が成立しないので、head を判定せずに人間へ返す。
- **AC7（S5）CI の登録:** 判定 C の時点では、`.github/workflows/offline-checks.yml` の `dotnet-offline-tests` の `matrix.project` に `ContentResolutionOfflineTests` の 1 行があること、AC1 の `dotnet run` がローカルで exit 0 であることを見る。PR の CI でその job が緑であることは Phase D のマージ条件とし、判定 C の合否にしない（判定 C の時点で PR が無いため）。
  - 確認手段: yml の本文。Phase D で PR の check。
- **AC8（S2, I1, I2, I5）offline テストの名前の照合:** AC1 の `dotnet run` の出力に、§5 の T1〜T26（T12b・T12c を含む 28 件）の各テスト名について `PASS <テスト名>` の行があり、`FAIL` の行が無い。各 T が検証する条件は §5 の表で紐付ける。
  - 確認手段: 出力の `PASS` 行と §5 の一覧の照合。
  - 不合格の例: 無関係な 20 件が通っている。T を統合して名前が消えている（統合するなら §5 の一覧を A3 の決定記録で直す）。
- **AC9（I7）汎用性の文字列検査:** `git grep -n -i -E "SceneDirector|SampleGame|Spring|Summer|Autumn|Winter|Season|Cell|RunPurpose|SceneResourceMap|UICommon|Svn|Workset" -- unity/Assets/OneStarMaker/Scripts/Runtime/ContentResolution unity/Assets/OneStarMaker/Tests/ContentResolution tools/ContentResolutionOfflineTests` が、`Cancel`（`Cell` の誤検知を避けるため `CancellationToken` の行は除外して読む）以外に 0 件。
  - 確認手段: 上の grep の出力を読む。

### 2.3 ここでは答えない問いと所有スライス

| 問い | 所有 |
|---|---|
| Editor で開いている Scene の snapshot の機構 (i) / (ii)、既ロード Scene の取り込み方、I11、`Facts` に入れる `dirty` 等の key、`SourceKind.EditorSnapshot` の loader 経路 | S0-b `CONTENT_DEMAND_ROUTING_SPIKE` |
| `InstallContentDirectory` の 2 件目拒否（directory の基数）、複数 directory の合成 | S0-b の結論の直後に切る製品スライス（program U1 (2)）、S2 |
| `IContentCompatibilityRule` の製品実装（identity / digest / target / schema の汎用判定、host 登録の述語） | S1 `CONTENT_COMPATIBILITY_SELECTION` |
| logical key → package の索引、Stage I / II | S2 `CONTENT_PACKAGE_DISCOVERY` |
| provider route → install → Unity load → 所有の接続。採用した候補と実際にロードした directory の同一性の検証（record の `SourceBindingVerified` を true にできる条件）。resolver 無しで先にロード済みの同 identity の Scene を `LoadContentSceneAsync` が再利用する既存挙動と record の関係（record が「今ロードした」と書き、呼び手の `UnloadSceneAsync` が先の呼び手の Scene を落とす）。SceneDirector 経由の経路（SceneSystem adapter）。`InstantiateContentAsync` 相当の経路と `Bind(go)` 所有の record。既存 index の表現 fallback の実効表現を record に写すこと。`SourceKind.Addressables` の loader 経路と、その受け入れ根拠。採用後のロード失敗（`ContentDirectoryException` 等）で `Probes`・採用候補との対応を呼び手に残す方法（loader の resolve と load を 2 段の公開入口に分ける案を含む） | S3 `RUNTIME_PROVIDER_INTEGRATION` |
| installed cache / publish 済み / LAN の provider adapter、probe の timeout、取得・install の進捗、prefetch、checkout hook。要求をまたぐ pin（同じ要求の再試行で、先に `Rejected` だった供給元が `Unavailable` になり下位の別 revision が採用されること。1 要求の pin では防げない） | S4 `LOCAL_LAN_PROVIDER` |
| Play 入口、通知の 3 段、観測 UI、menu の整理、record の保存・可視化・送信 | S0-c `EDITOR_PLAY_ENTRY_PROOF`、S5 |
| `Runtime/ContentResolution/` を別 asmdef にするか、P 検査の常設化、`IsExternalInit` polyfill の所有 | asmdef 分割スライス、[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md) D4 |

### 2.4 本文へ転記した実装制約（`AGENTS.md` の常時契約から）

- 依存は Game → Framework の一方向。asmdef 参照の追加は設計判断なので行わない（本スライスは asmdef に触れない。新 folder は `OneStarMaker.Runtime.asmdef` の配下）。
- 新規の Unity 側 `.cs` は先頭に `#nullable enable`。`record` を使わない（core も Unity の asmdef でコンパイルされるので同じ）。core では `init` accessor も使わない（`OneStarMaker.Runtime` の internal な `IsExternalInit` polyfill に依存すると、将来 core を別 asmdef へ分けたときに壊れる）。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。offline テストの非同期は、完了済みの `ValueTask` と、`TaskCompletionSource` によるシグナル待ち（保留中の probe の経路）と `CancellationToken` だけで組む。
- core の非同期は呼び手の context で続行する（`ConfigureAwait(false)` を使わない。S4 の Unity adapter が main thread を前提にできるようにする）。
- 破棄されうる `UnityEngine.Object` は `== null` / `!= null` で判定する（EditMode テストの `GameObject`）。
- 参照 0 を削除理由にしない（`IContentCompatibilityRule` と `ContentTraceability.Reproducible` は意図的な先行宣言で、製品実装は S1 / 後続）。
- path は `tools/path-budget.json` の予算（150 文字）に従う。最長の新 path は `unity/Assets/OneStarMaker/Scripts/Runtime/ContentResolution/Core/ContentResolutionFailureCode.cs.meta`（101 文字）。
- Unity Editor でのコンパイル確認は Phase B が行ってよい。Unity テストの実行・判定は Phase C。offline テスト（`dotnet run`）は Unity でも Build でもないので Phase B が B exit で実行してよい。net8 の BCL にあって netstandard 2.1 に無い API（`ArgumentNullException.ThrowIfNull` 等）は offline では通るので、Phase B の Unity コンパイルで捕まえる。
- 本スライスは `AbstractApplicationInitializer` に触れないので、`RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` との Phase B の同時実行制約の対象外。

### 2.5 未決事項

なし（設計上の未決）。A2 入力版の U-a1（core の非同期型）と U-a2（P 検査の方式）は owner が 2026-11-08 に裁定した: U-a1 は `System.Threading.Tasks.ValueTask<T>`（UnityEngine・UniTask を参照しない core を .NET 8 で素のままビルドできる。A2 のアーキテクチャゲートが netstandard 2.1 と UniTask 側からの `await` の整合を問題なしとした）、U-a2 は「判定 C の offline 実行」（§5「P 検査」。A1 の主担当の環境に Unity・pwsh が無く「A3 凍結 script」の dry run ができないため）。

### 2.6 スライスの分割の検討

分割しない。変更理由は「要求時解決の契約を定義し、fake と既存 AssetManagement の上で成立を示す」1 つで、core（pure）と loader（Unity 側）は依存が違うが同じ問いの両半分である（core だけでは「既存の AssetManagement と同じ契約で所有・解放できる」を示せず、loader だけでは契約の汎用性を示せない）。未知は P 検査の集合追加と offline project の CI 追加の 2 つで、どちらも既存の経路（ASSET_CYCLE の P 検査、4 つの offline project）の繰り返しで独立に失敗しうる未知ではない。受け入れ条件に別の問いの判定は混じっていない（S0-b / S1 / S3 / S4 の問いは §2.3 で送った）。

## 3. 責務マップ

path は `unity/Assets/OneStarMaker/Scripts/` からの相対。型名は A3 で凍結する。行数は予想。全ての型は不変の値で、辞書・一覧は constructor で防御的に複製する。

| ファイル | 責務（一文） | 変更理由 | 所有者・寿命 | 許可する依存 | 公開面 | 単体テスト | 予想行数 |
|---|---|---|---|---|---|---|---:|
| `Runtime/ContentResolution/Core/ContentKind.cs`（新規） | 要求の種別 `None = 0`（無効）、`Scene`、`Object` | 種別を足すとき | 値型 | なし | public enum | offline | 15 |
| `Core/ContentSourceKind.cs`（新規） | 供給元の種別（由来。内容がどこから来たか）`None = 0`（記録に使えない）、`EditorSnapshot`（開いている Scene の複製。S0-b の機構 (i) でも (ii) でも由来は同じ）、`ContentDirectory`（検証済みの built directory）、`Addressables`。ロード経路（`ContentLoadPath`）とは別の欄 | 供給元の種別を足すとき（S0-b / S3） | 値型 | なし | public enum | offline | 20 |
| `Core/ContentLoadPath.cs`（新規） | 候補をどの経路でロードするか `None = 0`（無効）、`ContentDirectory`（登録済みの Content Directory から `LoadContent*Async`）、`AlreadyLoadedScene`（Play 前に既ロードの Scene。S0-b の機構 (i)）、`Addressables`。由来（`ContentSourceKind`）と独立。S0-a の loader が扱うのは `ContentDirectory` だけ | ロード経路を足すとき（S0-b / S3） | 値型 | なし | public enum | offline | 20 |
| `Core/ContentAcceptanceBasis.cs`（新規） | 候補の受け入れ根拠 `None = 0`（採用不可）、`VerifiedDigest`（manifest と digest の検証を通した正規コピー）、`OpenSceneSnapshot`（Editor で開いている Scene の複製。意味は S0-b）。種別ごとに要る根拠の固定表は `ContentResolver` が持つ | 根拠の種類を足すとき（S0-b / S3） | 値型 | なし | public enum | offline | 20 |
| `Core/ContentIdentity.cs`（新規） | 候補の identity（`ContentSet`、`Revision`、`Target`、`Digest`。候補の申告では空の欄を許す）。ordinal の等値と `IsEmpty` / `IsComplete`（4 欄が全て非空）だけを持ち、互換の判定は持たない。pin の比較（`MatchesPin`: pin は `IsComplete` が前提で、全欄一致）もここ | identity の欄が増えるとき | 値型（readonly struct） | なし | public | offline | 70 |
| `Core/ContentRequest.cs`（新規） | 呼び出し側の要求: `LogicalKey`（必須。`string.IsNullOrWhiteSpace` を拒む。`AssetOwner.Scene` が空白だけを拒むのと同じ基準で、成功経路の `OwnerLabel` 生成が失敗しないため）、`Kind`（`None` 不可）、`Representation`（要求元の data。空は「指定なし」）、`ExpectedIdentity`（任意。最初の pin。与えるなら 4 欄がそろった identity だけで、一部の欄だけの値は constructor が `ArgumentException` で拒む。部分制約は持たない。R2-2 (A)）、`CorrelationId`（任意）。SceneDirector の状態・親子・Season・SceneResourceMap・VCS を持たない | 要求の欄が増えるとき | 呼び出し側が作る不変の値 | なし | public sealed class | offline（T8） | 60 |
| `Core/ContentCandidate.cs`（新規） | provider が返す候補: `ProviderId`（空不可。ordinal で比較）、`SourceKind`（`None` 不可）、`LogicalKey`、`Kind`、`Representation`（空不可）、`Identity`、`ManifestReference`（opaque。path を信用しない）、`AcceptanceBasis`、`LoadPath`（`None` 不可）、`Facts`（provider 固有の事実の小さな辞書。複製して保持） | 候補の欄が増えるとき | provider が作る不変の値 | なし | public sealed class | offline | 80 |
| `Core/ContentAvailability.cs`（新規） | 1 回の probe の回答: `Status`（`None = 0` は無効。`Available` / `NotFound` / `Unavailable`（未接続・timeout。次へ進む）/ `Rejected`（hash 不正・version 不整合・権限拒否。修復の基準になる 4 欄のそろった `ExpectedIdentity` を伴えば探索を続け、伴わなければ resolver はその場で止める。一部の欄だけの identity は constructor が `ArgumentException` で拒む））、`Candidate`、`ExpectedIdentity`（`ContentIdentity?`）、`Reason`。形の壊れた組（`Available` で `Candidate` 無し、`Available` 以外で `Candidate` 有り、`Rejected` 以外で `ExpectedIdentity` 有り、`Rejected` で `Candidate` 有り）は constructor が `ArgumentException` で拒む（R4-4）。constructor を通らない `default` だけが resolver の `Fault` | 回答の種別が増えるとき | 値型 | なし | public readonly struct + enum | offline | 80 |
| `Core/IContentProvider.cs`（新規） | 供給元の差込口: `Id`、`SourceKind`、`FindAsync(ContentRequest, CancellationToken) → ValueTask<ContentAvailability>`。探すだけで、取得・install・Build はしない | 探索の契約が変わるとき | adapter（S4 / S0-b）が実装。寿命は policy の持ち主 | なし | public interface | offline（fake） | 25 |
| `Core/IContentCompatibilityRule.cs`（新規） | 候補を採ってよいかの述語: `Accepts(ContentRequest, ContentCandidate, out string reason)`。製品実装は S1。XML doc に「S0-a が既定実装を置かない理由（S1 の判定の既定を先に凍結しない）と、製品実装が無い間は policy を組めないこと」を書く | 互換判定の契約が変わるとき | policy の持ち主 | なし | public interface | offline（fake） | 25 |
| `Core/ResolutionPolicy.cs`（新規） | Play 前に固定する規則: 順序付きの `Providers`（1 件以上。`Id` は空不可、ordinal で重複なし。複製して保持）と `Rule`。immutable。将来の要求を列挙しない | 規則の欄が増えるとき | host が作る不変の値 | `IContentProvider`、`IContentCompatibilityRule` | public sealed class | offline（T9） | 50 |
| `Core/ProbeOutcome.cs`（新規） | probe 1 回の採否の理由: `None = 0`（無効）、`Adopted`、`NotFound`、`Unavailable`、`Rejected`、`RuleRejected`、`PinMismatch`、`KeyMismatch`、`KindMismatch`、`RepresentationMismatch`、`ProviderMismatch`（候補の `ProviderId` / `SourceKind` が probe した provider と違う）、`NoAcceptanceBasis`（根拠が `None`、または種別に要る根拠と合わない）、`Fault` | 理由が増えるとき | 値型 | なし | public enum | offline | 20 |
| `Core/ProviderProbe.cs`（新規） | 探索 1 回の実績: `ProviderId`、`SourceKind`、`Outcome`、`Candidate`（あれば）、`Reason` | 実績の欄が増えるとき | 値型 | 上の型 | public sealed class | offline | 40 |
| `Core/ContentResolutionFailureCode.cs`（新規） | resolver の要求の終わり方: `None = 0`（採用した。`IsResolved == true` のときの値）、`NoCandidate`、`NoCompatibleCandidate`、`IntegrityRejected`、`ProviderFault`。優先順位は I5 | 種別が増えるとき | 値型 | なし | public enum | offline | 15 |
| `Core/ContentLoadRejection.cs`（新規） | loader が採用済みの候補をロードの前に拒んだ理由: `None = 0`（拒んでいない）、`LoadPathUnsupported`（loader が実装していないロード経路）。resolver の結果とは別の欄で、resolver は使わない | 理由が増えるとき（S3） | 値型 | なし | public enum | EditMode（E6） | 15 |
| `Core/ContentResolutionResult.cs`（新規。A2 入力版の `ContentResolution` を改名） | 1 要求の決定: `IsResolved`、`Candidate`、`FailureCode`（`IsResolved` なら `None`）、`Probes`（probe した供給元の実績。採用で止まる）、`PinnedIdentity`（`ContentIdentity?`。pin が無ければ null）。policy（予定）と別の型。namespace と同名にしない。loader の拒否で書き換えない | 決定の欄が増えるとき | resolver が作る不変の値 | 上の型 | public sealed class | offline | 60 |
| `Core/ContentResolver.cs`（新規） | 純粋な orchestration。公開 method は `ValueTask<ContentResolutionResult> ResolveAsync(ContentRequest request, ResolutionPolicy policy, CancellationToken cancellationToken)` の 1 つ。policy の順に probe し、I1（key・kind・表現・expected identity・provider の一致）・I2（受け入れ根拠の有無と種別ごとの固定表。§4.1: `Rejected` 後は同 identity かつ `VerifiedDigest` だけ。基準の無い `Rejected` と pin の食い違いは即 `IntegrityRejected`）・I5（規則）をこの順に適用し、採用で止め、provider / 規則の例外は fault として fail-closed で止めて `ContentResolutionResult` を返す。I/O・時間・乱数・Unity を持たず決定的 | 探索の規則が変わるとき | 状態なし | 上の型 | public sealed class | offline（T1〜T7、T10〜T12、T14〜T23） | 170 |
| `Core/ContentResolutionException.cs`（新規） | 解決できなかった、または採用した候補を loader がロード前に拒んだことを呼び出し側へ返す例外。`Result`（resolver の結果そのまま。loader の拒否では `IsResolved == true` の成功結果を保つ）と `Rejection`（`ContentLoadRejection`。resolver の失敗では `None`）を伴う。E4 と E6 を呼び出し側が区別できる（R2-1・R3-2） | — | — | `ContentResolutionResult`、`ContentLoadRejection` | public sealed class : InvalidOperationException | offline | 35 |
| `Core/ContentTraceability.cs`（新規） | record の到達度 `Traceable` / `Reproducible`（本 program は `Traceable` までしか書かない） | — | 値型 | なし | public enum | offline | 15 |
| `Core/ContentLoadRecord.cs`（新規） | 1 要求の実績: 要求、採用候補（由来）、`Probes`、`OwnerLabel`（要求時に渡した owner の表記。Scene は `AssetOwner.Scene(request.LogicalKey).ToString()`、Object は渡した owner の `ToString()`。観測値ではない。S0-b の所有の 3 層 (a) SceneBase lifecycle / (b) 物理 Scene の unload 責任 / (c) AssetManagement の台帳のうち、本 record が書くのは (c) だけ。(a) は SceneSystem adapter（S3）の記録）、`Traceability`、`SourceBindingVerified`（採用候補と実ロード元の同一性を検証したか。S0-a の loader は常に false）。`SourceKind == None`、`ProviderId` が空、`AcceptanceBasis == None` のどれかなら作れない（由来の無い成功は無い）。XML doc に「record は選択結果の記録で実ロード元の証拠ではない」ことと S3 の所有を書く | 実績の欄が増えるとき | loader が作る不変の値 | 上の型 | public sealed class | offline（T7） | 70 |
| `Runtime/ContentResolution/ContentRequestLoader.cs`（新規） | 決定を既存の `IAssetManagement` に接続する: `UniTask<SceneLoadOutcome> LoadSceneAsync(request, options = default, ct)` → `LoadContentSceneAsync(key, candidate.Representation, options, ct)`、`UniTask<AssetLoadOutcome<T>> LoadAssetAsync<T>(request, owner, ct)` → `LoadContentAssetAsync<T>(…)`。`options` は既存の `SceneLoadOptions` をそのまま渡し（既定は `ActivateOnLoad == true`）、`ActivateOnLoad == false` の既存の `ContentDirectoryException` は包まない。`LoadSceneAsync` は `Kind != Scene`、`LoadAssetAsync<T>` は `Kind != Object` の要求を resolver を呼ぶ前に `ArgumentException` で拒む（入口の取り違えは解決の失敗ではないので `ContentLoadRejection` に足さない。R4-5）。resolver が失敗したら `ContentResolutionException(Result, Rejection = None)`。採用候補の `LoadPath` が `ContentDirectory` 以外なら、ロードの前に `ContentResolutionException(Result = resolver の成功結果, Rejection = LoadPathUnsupported)` を投げる（由来 `SourceKind` では弾かない。snapshot 由来でも一時 directory 経由なら読める）。handle と record を返す。解放の API は足さず、呼び出し側は `IAssetManagement` の既存 API で解放する。AssetManagement の public だけを使う。XML doc に「`IAssetManagement` に member を足さない理由（I8 と program の停止規則、案 B の不採用）」と「ロード元の同一性が未検証であること（S3）」を書く | AssetManagement への接続の仕方が変わるとき（S3 で置き換わりうる） | 呼び出し側が所有。`IAssetManagement` の寿命に従う | `IAssetManagement` と Abstractions（public のみ）、Core、UniTask | public sealed class | EditMode（E1〜E6） | 140 |
| `Runtime/ContentResolution/ContentLoadOutcomes.cs`（新規） | `SceneLoadOutcome`（`ISceneHandle` + record）、`AssetLoadOutcome<T>`（`IAssetHandle<T>` + record） | — | 値 | AssetManagement の Abstractions | public sealed class × 2 | EditMode | 40 |
| `unity/Assets/OneStarMaker/Tests/ContentResolution/ContentRequestLoaderTests.cs`（新規） | E1〜E6。fake `IContentDirectoryBackend` は既存 3 箇所の private fake と同じ形を file 内に持つ（共有のために既存 fake を動かさない） | — | — | `OneStarMaker.Tests` の既存参照 | — | — | 300 |
| `tools/ContentResolutionOfflineTests/ContentResolutionOfflineTests.csproj`、`Program.cs`（新規） | T1〜T26（28 件）。core の `.cs` を link。`LangVersion` 9（Unity のコンパイラに合わせ、新しい構文の混入を .NET 側で捕まえる） | — | — | .NET 8 SDK だけ | — | — | 25 + 480 |
| `.github/workflows/offline-checks.yml` | `dotnet-offline-tests` の matrix に 1 行 | — | — | — | — | — | +1 |

変更しない file（責務の参照だけ）: `Runtime/AssetManagement/IAssetManagement.cs`（75 行、増分 0）、`AssetManagement.ContentDirectory.cs`（164 行、増分 0）、`Internal/IContentDirectoryBackend.cs`（36 行、増分 0）、`Runtime/BuildContent/**`（増分 0）。行数警報（500 行・3 責務・50% 増）に該当する file は無い。

配置・依存・所有・寿命・テスト境界を選んだ理由:

- **folder `Runtime/ContentResolution/`（`OneStarMaker.Runtime.asmdef` の配下、namespace `OneStarMaker.Runtime.ContentResolution`）:** フォルダ境界の基準（architecture-gates）の「依存方向」と「所有者」が既存 folder と違う。AssetManagement の公開面と共通契約だけに依存し、AssetManagement・BuildSystem 本体・SceneSystem・Bootstrap のどれからも参照されない、AssetManagement の上の任意の層である。[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md) §2.1 の「連携部分（両方を知る）」とは別の分類で、BuildSystem を参照しない。asmdef 分割スライスへは「`ContentDirectorySession` と同じ assembly に寄せる理由は無い」と渡す。既存の連携部分（`Runtime/BuildContent/ContentDirectorySession.cs` 等）と同じ folder に置かないのは、(a) U-B の P 検査の集合から file 名で除外する一覧を増やさずに folder 単位で外せること、(b) 変更理由（要求・候補・規則の契約）が directory session（native handle と token の寿命）と独立なこと、の 2 つが違うため。asmdef は分けない（asmdef の追加は設計判断で、所有は asmdef 分割スライス）。同じ assembly に居ることで internal が見える問題は AC3・AC4 で機械的に塞ぐ。
- **`Core/` sub-folder:** UnityEngine・UniTask を参照しない file だけを置き、offline の .NET project がこの folder の file だけを link する。「Unity 非依存」を folder の境界で見えるようにし、Unity 側の接続（loader）を同じ folder の外の 1 file に限る。
- **core の非同期型に `ValueTask<T>`（U-a1）:** 探索は本質的に非同期（LAN の probe、S4）だが、core は Unity に依存しないので UniTask を使わない。netstandard 2.1 の `ValueTask` は Unity の Runtime asmdef でコンパイルでき、UniTask 側の loader は `await` できる。代替（UniTask + offline project への NuGet 参照）は owner が Q2 で採らなかった。
- **`IAssetManagement` に member を足さない:** 由来（どの directory か）を handle に持たせると `IAssetManagement` の変更になり I8 と program の停止規則（Runtime の新 API を S0-b 前に凍結しない）に当たる。record は loader が、採用した候補から作る。実ロード元との同一性の検証は S3（§2.3）で、record の `SourceBindingVerified=false` がその未検証を明示する。
- **`IContentCompatibilityRule` を差込口だけにする:** 互換の判定内容は S1 の問い。S0-a が「identity 等値」のような既定実装を置くと、S1 の判定の既定値を先に凍結してしまう。製品実装が無い間は policy を組めないが、S0-a は契約の成立だけを示す（§2 成果の範囲）。
- **失敗の表現:** 解決できないこと（候補なし、互換なし、整合性拒否、fault）は `ContentResolutionResult` の結果で返し、loader の公開入口だけが `ContentResolutionException` に変える（呼び出し側の `try` が 1 つで済む）。loader が採用済みの候補をロード経路の都合で拒むことは resolver の失敗ではないので、例外の `Rejection` 欄で別に伝え、`Result` は resolver の成功のまま保つ（候補選択と実ロードは別の事実。R2-1・R3-2）。AssetManagement のロード失敗（`ContentDirectoryException` 等）は包まずにそのまま通す（reason code の所有を変えない。[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md) G5）。
- **loader が `LoadPath.ContentDirectory` 以外の候補を拒み、由来（`SourceKind`）とロード経路（`LoadPath`）を別の欄にする:** loader が実装している経路は directory だけで、他の経路の候補を directory から読むと record の由来が虚偽になる。由来と経路を分けるのは S0-b の 2 機構に中立でいるため: 機構 (i) は由来 `EditorSnapshot` × 経路 `AlreadyLoadedScene`、機構 (ii) は由来 `EditorSnapshot` × 経路 `ContentDirectory`（snapshot から build した一時 directory）。受け入れ根拠の表は由来で引き（snapshot は `OpenSceneSnapshot`）、loader の制限は経路で引く。どちらが勝っても S0-a の型は変えずに済む。経路ごとの loader は S0-b の結論の後に S3 が足す。
- **解放の API を足さない:** 所有・解放は `AssetOwner` と `IAssetManagement` の既存契約（`Release` / `UnloadSceneAsync` → `ReleaseScene` / `ReleaseAll`）をそのまま使う。「同じ契約で扱える」は、新しい解放 API を作らないことで示す。
- **テスト境界:** core は .NET だけで決定的にテストする（offline）。loader は `AssetManagement` の実物 + fake の `IAssetBackend` / `IContentDirectoryBackend` で EditMode テストする（Unity の test runner は要るが、Editor Play・AssetDatabase・Addressables の実体には依存しない。既存の `AssetManagementTests` と同じ条件）。
- **既存 fake を動かさない:** 3 箇所の `FakeDirectoryPort` は private の入れ子型で共有できず、共有 fixture へ昇格させると既存テスト file の変更になり AC2 に当たる。本スライスは file 内の private fake を 1 つ足す。共有化は後続の整理。

## 4. 実装計画

- 変更対象: §3 の表のとおり。
- 開始の前提: 本書の A3 凍結版が本ブランチにあり、Phase B は凍結版の commit（tree の外で渡す SHA）から始める。S0-b の結果を待たない（独立）。
- 順序: (1) `Core/` の型を §3 の順（enum → 値型 → interface → policy → probe → resolver → record）で書く → (2) `tools/ContentResolutionOfflineTests/` を作り T1〜T26 を書いて `dotnet run` で通す（B exit の offline suite） → (3) `ContentRequestLoader` と outcomes を書く → (4) `Tests/ContentResolution/ContentRequestLoaderTests.cs` に E1〜E6 を書く → (5) Unity Editor を開いてコンパイル確認（エラー 0）。Unity が生成した folder の `.meta`（兄弟の `ContentResolution.meta` を含む）を commit に含め、他の file の書き換えは含めない（AC2） → (6) `offline-checks.yml` に 1 行足す → (7) AC3・AC9 の grep と `pwsh tools/contract-audit.ps1` → (8) B の出口: commit、§0 の票（implementation head 欄は (b) の「この票を含む commit」。`docs/handoff/` 以外を変える commit なので可）。起点 filter の EditMode テストと P 検査は Phase C が実行する。
- Phase B から Phase A へ差し戻す条件: §3 に無い public 型・member が要る（特に `IAssetManagement` / `ISceneHandle` / `IAssetHandle` への追加）。core に UnityEngine / UniTask / BuildContent / SceneSystem の参照が要る。`ValueTask` を UniTask の loader から `await` できない。loader が AssetManagement の internal を使わないと E1〜E6 が成立しない。既存テストの assertion を変えないと通らない。
- 対象外を維持する方法: `Runtime/AssetManagement/**`、`Runtime/BuildContent/**`、`Runtime/SceneSystem/**`、`Runtime/Bootstrap/**`、`SampleGame/**`、`.asmdef` を開かない。AC2・AC3 で確認する。

## 5. テストとレビュー計画

- 単体テスト（新しい中核ロジック = `ContentResolver` と record の検証）: offline の T1〜T26（T12b・T12c を含む 28 件）。fake provider は `Available` / `NotFound` / `Unavailable` / `Rejected` を script 化して返し、probe の回数と順序を数える。保留中の probe は `TaskCompletionSource` で表す。fake rule は候補ごとの採否を表で返すか、指定した例外を投げる。各 T の名前は `PASS <名前>` の行として出力し、AC8 で照合する。

| T | 名前 | 検証する条件 | 内容 |
|---|---|---|---|
| T1 | `Priority_FirstAvailableWins` | I5 | A が `NotFound`、B が `Available` → B を採用。`Probes` は A（`NotFound`）、B（`Adopted`）の順で 2 件 |
| T2 | `Priority_IsNotCompatibility` | I5、反例 (f) | A が `Available` だが rule が拒否、B が `Available` で rule が採用 → B を採用。A の probe は `RuleRejected` |
| T3 | `Rejected_ThenDifferentIdentity_FailsClosed` | I2、反例 (g) | A が `Rejected`（expected X）、B が `Available`（Y、`VerifiedDigest`）で供給元は B が最後 → 失敗 `IntegrityRejected`。B の probe は `PinMismatch`（記録して次へ進むが、次が無い）。採用候補は無い |
| T4 | `Rejected_ThenSameVerifiedIdentity_Repairs` | I2 | A が `Rejected`（X）、B が `Available`（X、`AcceptanceBasis.VerifiedDigest`）で rule が採用 → B を採用。`PinnedIdentity` が X。要求に `ExpectedIdentity=X` がある場合も同じ（`Rejected`（X）は食い違いではない） |
| T5 | `AllNotFound_NoCandidate` | I5 | 全て `NotFound` → `NoCandidate` |
| T6 | `AllUnavailable_NoCandidate_WithDistinctProbes` | I5 | 全て `Unavailable` → `NoCandidate`。`Probes` の `Outcome` で不在と不達を区別できる |
| T7 | `Record_WithoutProvenance_IsRejected` | 反例 (c) | `ContentLoadRecord` を `SourceKind.None` または空の `ProviderId` で作ると `ArgumentException` |
| T8 | `Request_Validation` | I1 | 空の `LogicalKey` と `Kind.None` は `ArgumentException`。`Representation` の null は `ArgumentNullException`、空は許す。`ExpectedIdentity` に一部の欄だけ入れた identity（例: `Digest` だけ、`ContentSet` と `Revision` だけ）は `ArgumentException`。4 欄そろった identity と「無し」は許す |
| T9 | `Policy_Validation_AndDefensiveCopies` | — | provider 0 件、`Id` の重複（ordinal）、空の `Id`、rule の null は `ArgumentException`。`ContentCandidate` の空の `ProviderId` も `ArgumentException`（R4-2）。渡した一覧・辞書を後から変えても policy / candidate の中身は変わらない |
| T10 | `Candidate_WithDifferentKey_IsProviderFault` | I1 | provider が要求と違う `LogicalKey` の候補を返す → その probe は `KeyMismatch`、次へ進む。全てそれなら失敗 `ProviderFault` |
| T11 | `ObjectKind_ResolvesAndKindMismatchIsRejected` | I1 | `Kind = Object` の要求が同じ規則で解決される。A の候補の `Kind` が `Scene` なら `KindMismatch` として記録して次へ進み、B の候補が `Object` なら B を採用。A だけなら失敗 `ProviderFault` |
| T12 | `Cancelled_BeforeFirstProbe` | — | 取消済みの token で `OperationCanceledException`（`ContentResolutionResult` を伴わない）、probe の回数 0 |
| T13 | `CoreSources_DoNotNameUnityOrHosts` | I7、AC1 | `Core/` の各 file の本文に `UnityEngine`、`Cysharp`、`SceneDirector`、`SampleGame`、`Spring`、`Season`、`Cell`、`RunPurpose`、`SceneResourceMap`、`UICommon`、`Svn`、`Workset` が現れない（`Cell` は `CancellationToken` を除く） |
| T14 | `Representation_MustMatchWhenRequested` | I1 | 要求 (Entry, Scene, "Low")、A が `Available`（"Full"）→ `RepresentationMismatch` として記録して次へ進み、B が `Available`（"Low"）なら B を採用。A だけなら失敗 `NoCompatibleCandidate`。要求の表現が空なら A の "Full" を採用し、record の表現は "Full" |
| T15 | `Candidate_EmptyRepresentation_IsNotAdopted` | I1 | 候補の表現が空 → `KeyMismatch` と同列の不採用（`RepresentationMismatch`）。`ContentCandidate` の constructor でも空を拒む |
| T16 | `ExpectedIdentity_IsInitialPin` | I1、I2 | 要求に `ExpectedIdentity=X`（4 欄そろい）、A が `Available`（Y、`VerifiedDigest`）で rule が採用 → `PinMismatch`、次へ進む。B が `Available`（X、`VerifiedDigest`）なら B を採用し `PinnedIdentity` が X。A だけなら失敗 `IntegrityRejected`（I5 の優先順位 (1)） |
| T17 | `Rejected_WithoutIdentity_StopsImmediately_AndPartialIdentityIsRejected` | I2 | A が `Rejected` を expected identity 無し（権限拒否）で返す → その場で `IntegrityRejected`、B は probe されない。`Rejected` に一部の欄だけの identity（`Digest` 無し等）を渡すと `ContentAvailability` の constructor が `ArgumentException`。pin X に対し、digest が同じで target が違う identity は一致しない。空の identity の候補はどの pin にも一致しない |
| T18 | `Repair_RequiresVerifiedCandidate` | I2 | A が `Rejected`（X）、B が `Available`（X、根拠 `None` または `OpenSceneSnapshot`）→ B の probe は `NoAcceptanceBasis`（identity は一致。不採用の理由は根拠側。R2-3・R3-1）、次へ進む。B が最後なら失敗 `IntegrityRejected`（pin が未充足のまま終わる。I5 の優先順位 (1)） |
| T19 | `ConflictingPins_FailClosed` | I2 | A が `Rejected`（X）、B が `Rejected`（X'）、C が `Available`（X'、検証済み）→ 失敗 `IntegrityRejected`。C は probe されない（pin の食い違いで止まる） |
| T20 | `RuleException_IsFaultAndStops` | I5 | A が `Available`、rule が `InvalidOperationException` を投げる → A の probe が `Fault`、B は probe されず、失敗 `ProviderFault`。`default(ContentAvailability)` を返す provider も `Fault`。形の壊れた組（`Available` で `Candidate` 無し等の 4 つ）は `ContentAvailability` の constructor が `ArgumentException`（R4-4） |
| T12b | `Cancelled_WhilePending_SkipsLaterProviders` | — | A が保留中（TCS）に cancel → `OperationCanceledException`、B は probe されない |
| T12c | `Provider_IgnoringToken_IsNotAdopted` | — | A が token を無視して cancel 後に `Available` を完了しても、採用せず `OperationCanceledException`。逆に token が取消されていないのに A が `OperationCanceledException` を投げたら `Fault`、失敗 `ProviderFault`（R4-6） |

| T21 | `Candidate_FromDifferentProvider_IsProviderMismatch` | I1 | provider A が `ProviderId="B"` または別の `SourceKind` を名乗る候補を返す → `ProviderMismatch`、次へ進む。全てそれなら失敗 `ProviderFault` |
| T22 | `Candidate_WithoutAcceptanceBasis_IsNeverAdopted` | I2、反例 (f') | 全件採用する rule の下で、`ContentDirectory` の候補の根拠が `None`、または `OpenSceneSnapshot`（種別と不一致）→ `NoAcceptanceBasis`、採用しない。`EditorSnapshot` の候補に `OpenSceneSnapshot` があれば採用する（resolver は種別を特別扱いせず表で判定する）。`Addressables` の候補はどの根拠でも採用しない |
| T23 | `Adoption_StopsProbing` | I5 | A が `Available` で採用 → B は probe されず、`Probes` は 1 件 |
| T24 | `Repair_DoesNotDependOnProviderOrder` | I2 | A が `Rejected`（X）、B が `Available`（Y、`VerifiedDigest`）、C が `Available`（X、`VerifiedDigest`）で rule が採用 → C を採用。B の probe は `PinMismatch`、`Probes` は 3 件、`PinnedIdentity` が X（R3-1） |
| T25 | `FailureCode_Precedence` | I5 | 誰も採用しない組み合わせで I5 の優先順位を 1 件ずつ: `PinMismatch` + `KeyMismatch` → `IntegrityRejected`。`KeyMismatch` + `RuleRejected` → `ProviderFault`。`RuleRejected` + `NotFound` → `NoCompatibleCandidate`。`NotFound` + `Unavailable` → `NoCandidate` |
| T26 | `RequestPin_ConflictingRejected_StopsImmediately` | I2 | 要求に `ExpectedIdentity=X`、A が `Rejected`（X'）、B が `Available`（X'、`VerifiedDigest`）で rule が採用 → その場で `IntegrityRejected`。B は probe されない。A の probe は `Rejected`。`PinnedIdentity` は X のまま（R4-1） |

（T12b・T12c は T12 の補助で、AC8 の照合対象に含める。合計 28 件。）

- EditMode（`OneStarMaker.Tests`、`Tests/ContentResolution/`。namespace は `OneStarMaker.Tests.ContentResolution` を明示する。起点 filter がこの名前）:
  - E1 Scene（G03 (b)）: `AssetManagement(FakeAssetBackend)` に fake `IContentDirectoryBackend` を `InstallContentDirectory`（internal。テストは既存と同じく internal を見られる）。policy = fake provider 1 件（`SourceKind.ContentDirectory`、`Available`、表現 "Full"）+ 採用する rule。`ContentRequestLoader.LoadSceneAsync(request("Entry", Scene, "Full"))` → `IsLoaded`、record の由来・`OwnerLabel == "Scene(Entry)"`・`SourceBindingVerified == false`。`UnloadSceneAsync("Entry")` → `ReleaseScene("Entry")` → fake port の unload 回数 1（fake port が返す scene は既存の private fake と同じく `IContentDirectoryToken` を実装し、`AssetManagement.UnloadSceneAsync` の directory 側の分岐で数える。`ReleaseScene` は Scene 本体を unload しない）。`FakeAssetBackend.LoadSceneCallCount == 0`（Addressables 側を通らない）。
  - E2 Object: `LoadAssetAsync<GameObject>(request("Widget", Object, "Full"), AssetOwner.Manual)` → handle の `Value` が非 null、fake port の `GetObjectKey` が返す key で `GetOwners(key)` に `Manual`。`Release(handle)` → `GetOwners` が空、fake port の release 回数 1（cache を渡さない `AssetManagement(IAssetBackend)` で組む）。`Kind = Scene` の要求を `LoadAssetAsync<T>` に渡すと resolver を呼ぶ前に `ArgumentException`（R4-5）。
  - E3 record の由来と要求の対応: E1 の record の `Request` が渡した要求と同値、`Candidate.Representation == "Full"`、`Traceability == Traceable`、`Probes` が 1 件で `Adopted`。
  - E4 探す段とロードの分離: provider が `NotFound` → `ContentResolutionException`。`Result.IsResolved == false`、`Result.FailureCode == NoCandidate`、`Result.Probes` 1 件（`NotFound`）、`Rejection == None`。fake port の load 回数 0。
  - E5 fail-closed: provider が `Rejected`（identity 無し）→ `Result.FailureCode == IntegrityRejected`、`Probes` 1 件（`Rejected`）、`Rejection == None`。load 回数 0。
  - E6 ロード経路の拒否: (1) 候補が由来 `EditorSnapshot`・根拠 `OpenSceneSnapshot`・経路 `AlreadyLoadedScene`、(2) 候補が由来 `ContentDirectory`・根拠 `VerifiedDigest`・経路 `Addressables`。どちらも resolver は採用し、loader が拒む: `Result.IsResolved == true`、`Result.Candidate` がその候補、`Result.Probes` 1 件（`Adopted`）、`Result.FailureCode == None`、`Rejection == LoadPathUnsupported`。load 回数 0。対照: 由来 `EditorSnapshot`・根拠 `OpenSceneSnapshot`・経路 `ContentDirectory` の候補は拒まず、fake port の `LoadSceneAsync` が 1 回呼ばれる。
- 差し戻し中の起点 `-Filter`（C が根拠付きで変更可。受け入れ条件の追加ではない）: `OneStarMaker.Tests.ContentResolution`、`OneStarMaker.Tests.AssetManagement`。offline suite は毎回 `dotnet run`。
- 判定必須テスト（GO 候補 head）: 全 EditMode 回帰（空 filter）を base と head で同じ条件で実行（AC6）。XML で `OneStarMaker.Tests.ContentResolution` の E1〜E6 と `Tests/AssetManagement/*` の既存テストが収録されていること。offline suite（AC1・AC8）。P 検査（AC4）。AC3・AC9 の grep。`pwsh tools/contract-audit.ps1`。順序は、全 EditMode 回帰（Unity が head の worktree に `Library/ScriptAssemblies` を作る）→ P 検査（その DLL を参照する）。
- 全 EditMode 回帰の適用除外: なし
- 統合・Unity テスト: EditMode だけ。PlayMode・Build・Player は不要（起動・Scene lifecycle・Player に触れない）。
- 操作・実行時・目視条件の検証経路: 対象なし（全条件が offline テスト、EditMode テスト、`git diff` / `git grep`、P 検査で閉じる）。
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点: 2 つ。(1) P 検査の `RESOLVER-*` 集合の追加: 凍結済み `p-check.ps1` の `$Sets` に、U-A の library を参照する集合を足す改変で、script が既に持つ「別集合の library を参照する」機能（`UB-content`）の繰り返し。主担当の環境では実行できないので **未確認。初回確認は Phase C**（判定 C の実行担当が script を用意して実行する。不成立なら、集合の定義を本書 §5 の表に照らして直し、script の改変だけで済まないなら Phase A の revision）。(2) offline project の CI 追加: 既存 4 project と同じ matrix 行で、経路は既存。主担当の環境に dotnet が無いので、B の出口で `dotnet run` を初めて実行する。
- 人間の判断が必要な条件: なし
- 高コスト証拠: なし

### P 検査

- **方式（U-a2）:** **判定 C の offline 実行。** 判定 C の担当が、ASSET_CYCLE の Phase A snapshot に凍結された `p-check.ps1`（ローカルの Artifact Storage から取得。content sha256 は同スライスの記録 `94291d8f…`）を元に、冒頭の `$Sets` へ下の表の 3 集合を足した script を書き、自分の環境で head の worktree に対して実行する。資格情報・本番設定・通信には触れない（ローカルのコンパイルだけ）。script と出力は判定 evidence bundle に置き、git に入れない。「A3 凍結 script」方式を採らない理由は §2.5 U-a2。
- **集合の規則（正本はこの表）:** 既存の 7 陽性集合と 2 陰性対照は ASSET_CYCLE の表のまま変えない。folder 単位で全 `.cs` を取り、除外は無い。参照は既存と同じ（netstandard 2.1、`UnityEngine*.dll`、Editor 集合では `UnityEditor*.dll` と `UNITY_EDITOR`、`Library/ScriptAssemblies` の `UniTask` / `Unity.Addressables` / `Unity.ResourceManager`）に、下の「追加の参照」を足す。

| 集合 | ソース（`unity/Assets/OneStarMaker/Scripts/` から） | 追加の参照 | 期待 |
|---|---|---|---|
| `RESOLVER-player` / `RESOLVER-editor` | `Runtime/ContentResolution/`（`Core/` を含む）だけ | 同じ実行で作った `UA-player` / `UA-editor` の library（U-A + 共通契約を 1 つの DLL にしたもの） | exit 0（resolver は U-A の **public** と共通契約だけで閉じる。internal を使えば CS0122 / CS0117 で失敗する。SceneSystem・BuildContent・Bootstrap を要しない） |
| 陰性対照 `NEG-RESOLVER-without-assetmanagement` | `Runtime/ContentResolution/`、`Runtime/AssetBuildContracts/` | なし | exit ≠ 0。error に `IAssetManagement`、`AssetOwner`、`ISceneHandle`、`IAssetHandle` のいずれかが引用符付きで現れる（loader が AssetManagement の公開面を本当に使っていることの対照。core だけなら閉じることは offline project が示す） |

- **合格:** 既存の 7 陽性集合と新 2 集合が exit 0、既存の陰性対照 2 つと新 1 つが exit ≠ 0 で理由が期待どおり。exit 2（前提の不備）を合格と読まない。
- **P 検査が示さないもの:** Unity の asmdef ごとのコンパイル（Phase B の Editor コンパイルと AC6 の回帰が担う）、実行時の挙動。

### 判定 C の照合 script の方式

- P 検査: 上のとおり「判定 C の offline 実行」。
- それ以外（EditMode の XML、offline suite の出力、`git diff` / `git grep`）: script は不要。XML の実行テスト名と結果を読み、offline suite は `PASS` 行と末尾の件数行と exit code を読む。機械向けの期待値 file は無い。

### レビュー計画

- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`（PR 作成前と harvest 時）、P 検査、offline suite、AC3・AC9 の grep。
- A0/A1 主担当・モデル・ベンダー: Claude Code（Anthropic、Claude Fable 5.1。Cloud の session）。
- A2 独立レビュー（normal: 2 観点）: (a) アーキテクチャゲート、(b) 目的レビュー。実績は下の A2 記録。
- A3 統合担当・モデル・採否: 主担当（同上）が整理、owner が裁定して凍結。
- C' 用に予約した担当・モデル・ベンダー: A1 / A2 / A3 整理は Anthropic 系列だけを使った。OpenAI・xAI・Google は未関与で C' に残る。B と C のモデルは Phase ごとに選ぶ（C は B と別モデル）。
- 独立性の強化条件を満たせない場合の理由: 該当なし（risk `normal`）。

### A2 独立レビュー（2026-11-08 実施）

- A2 入力版: commit `d5f15d3`。本書（A2 入力版）の内容 sha256 `1ddb5923d38f14d90c7d2ece74dfc44c2323002a8b6d8703132216deba5c5aa6`。Phase A snapshot は未生成（§0）。2 件は互いの指摘を見ていない。どちらも本書の主担当の session が新規 agent として起動し、担当は file を変更していない。
- (a) アーキテクチャゲートの入力: A2 入力版の本書、program A0、`architecture-gates.md`、`phases-and-handoff.md` の該当節、`AGENTS.md`、`ASSET_BUILDSYSTEM_INDEPENDENCE.md` §2.1・§3.3・§5.1、base `5f971da` の `Runtime/AssetManagement/**`・`Runtime/AssetBuildContracts/**`・`ContentDirectorySession.cs`・`ContentDirectoryIndex.cs`・`SceneDirector.Loading.cs`・asmdef・既存 fake・offline project・CI yml・`27-folder-structure.md`。枠は「§3・§4 は実装済みと仮定し、現コードに無いことを欠陥にしない」「owner 決定 D1〜D11 と問いの分解を問い直さない」で固定した。
- (b) 目的レビューの入力: §1 の目的と利用者の原文だけ（規則どおりに連ねた 1 本の sha256 `cf7e079fe87fecdebe8c5a4fa6d4e1db1c800c1a6d073f90c618b8e63b3d6db7`）。問いは規則の 2 つに固定し、ツールを使わず file を読まないよう指示した（担当は「file は 1 つも読んでいない」と自己申告。実行ログの確認はしていない）。

| 観点 | 担当・モデル・ベンダー | 結果 |
|---|---|---|
| (a) アーキテクチャゲート | Claude Code の新規 agent（Anthropic、Claude Opus。静的レビューで、build・テスト・P 検査は実行していない） | 判定「修正後に凍結可」。blocker-for-freeze 5（A-01〜A-05）、should 6（A-06〜A-11）、note 4（A-12〜A-15）。配置・依存の向き・`IAssetManagement` を変えない判断・`Core/` と loader の分離・`ValueTask` の選択・S3 への移送・§2.3 の移送先・path 予算・常時契約は問題なしとした |
| (b) 目的レビュー | Claude Code の新規 agent（Anthropic、Claude Sonnet） | 保証すべきこと 8 件（供給元の非露出、要求時の評価、同じ所有・解放の契約、規則の明示と決定性、成功・失敗・候補なしで残る記録、fake だけでの完走、必須依存にしない、後続実装者が始められる粒度）。作らないべきこと 8 件（実在の adapter、SceneSystem adapter と Play 入口、host 固有の参照、既存 API の変更、独自の所有の仕組み、UI、record の保存・可視化・送信、候補選択の高度化）。文言への指摘 5 件（「同じ契約」の範囲、「妥当な候補」の決め手、「何が失敗したか」の粒度、Scene と Asset で契約が共通か、「併用」の対象） |

### A3 採否（主担当の整理、2026-11-08。Q1〜Q7 は owner が裁定済み（下の節）。R4 の反映後に凍結）

| 指摘 | 採否 | 反映 |
|---|---|---|
| A-01 S2・I1・I2・I5 に紐付く AC が無く、AC1 は件数だけを見る | 採用 | AC8（T の名前と `PASS` 行の照合）を足し、§5 の T 表に各 T が検証する条件を併記した。AC1 の件数の下限を外し AC8 に委ねた |
| A-02 Rejected 後の pin が空洞（空 identity の等値、未検証の修復、要求をまたぐ pin、pin の食い違い） | 採用（(3) は S4 へ移送） | I2 を改め、修復は `Digest` 付きの expected identity を伴う `Rejected` の後だけ、pin の比較は全欄一致かつ空は不一致、修復の候補は根拠 `VerifiedDigest` 必須、pin の食い違いは `IntegrityRejected`、pin は 1 要求の中だけと定めた。T17・T18・T19 を足した。要求をまたぐ pin は §2.3 で S4 の入力にした（基準の無い `Rejected` の扱いは R1-4 で調整） |
| A-03 resolver が表現と expected identity を照合しない。空の表現が既存 index の fallback と台帳のずれを招く | 採用 | I1 を改め、要求の表現が空でなければ完全一致、候補の表現は空不可、`ExpectedIdentity` を最初の pin とした。`ProbeOutcome.RepresentationMismatch` を足し、T14・T15・T16 を足した |
| A-04 loader が `ContentDirectory` 以外の種別の候補を directory から読み、record の由来が虚偽になる | 採用 | loader はロードの前に未対応の候補を `LoadPathUnsupported` で失敗させる。`ContentResolutionFailureCode` に値を足し、E6 と反例 (h) を足した（S0-b ブランチの確認で、判定の鍵を由来 `SourceKind` からロード経路 `LoadPath` に改めた。下の「S0-b ブランチの確認」） |
| A-05 同じ assembly なので loader が AssetManagement の internal を使えて、AC2・AC4 で検出できない | 採用 | AC3 に internal 名の grep を足し、P 検査の `RESOLVER-*` を「U-A を library にしてから resolver だけをその参照でコンパイルする」形に変えた（A2 入力版の `RESOLVER-unity`（ソースを一緒にコンパイル）は置き換え）。I8 (d)・I10・反例 (i) に経路を書いた |
| A-06 probe の不採用理由が文字列に混ざる。provider / rule の例外の扱いと、採用後に probe を続けるかが未定 | 採用 | `ProbeOutcome` enum と `ProviderProbe.Candidate` を足した。OCE 以外の例外は `Fault` として記録し fail-closed で止める。採用で探索を止める。T20 と `default(ContentAvailability)` の扱い（A-11）を足した |
| A-07 未検証の結び付きのまま `Traceable` と書くと過大。既ロード Scene の再利用で record が実態とずれる。`OwnerLabel` は観測値ではない | 一部採用 | record に `SourceBindingVerified`（S0-a は常に false）を足し、XML doc に S3 の所有を書くことを §3 の要求にした。既ロード Scene の再利用は §2.3 で S3 の入力にした。`OwnerLabel` は「要求時に渡した owner の表記」と明記した。`Traceable` の値自体は program §3 の定義（どの入力を選んだかを説明できる）の範囲内なので変えない |
| A-08 完了済みの `ValueTask` だけでは保留中の probe・cancel・token 無視が未検証。`ConfigureAwait` が未定 | 採用 | §2.4 に TCS によるシグナル待ちの許可と「呼び手の context で続行（`ConfigureAwait(false)` を使わない）」を書き、T12b・T12c を足した |
| A-09 型 `ContentResolution` が namespace の末尾と同名で CS0118 を招く | 採用 | `ContentResolutionResult` に改名した |
| A-10 AC2 が兄弟の folder `.meta` を落とす。AC7 は判定 C の時点で判定できない。AC1 の件数 | 採用 | AC2 に兄弟の `.meta` 2 file を明記し合格例・不合格例を付けた。AC7 は判定 C で yml とローカル実行、CI の緑は Phase D のマージ条件とした。AC1 の件数は AC8 へ |
| A-11 enum の 0 番が有効値で `default` が「候補 null の Available」になる。防御的複製が未記載 | 採用 | `ContentKind` / `ContentAvailabilityStatus` / `ProbeOutcome` の 0 番を無効値にし、`default` は `Fault` と定めた。§3 の冒頭に防御的複製を書き、T9 に含めた |
| A-12 I7 の文字列検査が `Core/` だけで、検査語が不足 | 採用 | AC9 を足し、対象を `Runtime/ContentResolution/**`・`Tests/ContentResolution/**`・offline project に、検査語を I7 の列挙に揃えた |
| A-13 「連携部分」の分類が、asmdef 分割で BuildSystem 側へ寄せられる誤解を招く | 採用 | §0 の関連・§3 の理由・harvest to を「BuildSystem を参照しない、AssetManagement の上の任意の層」に改めた |
| A-14 半年後に「なぜ `IAssetManagement` を変えなかったか」「なぜ rule に製品実装が無いか」「ロード元の同一性が未検証」を復元できない | 採用 | `IContentCompatibilityRule`・`ContentRequestLoader`・`ContentLoadRecord` の XML doc にこれらを書くことを §3 の要求にし、harvest to の項目に加えた |
| A-15 net8 だけの API が offline で通る。core の `init` は polyfill に依存する | 採用 | §2.4 に core の `init` 禁止と、net8 だけの API は Phase B の Unity コンパイルで捕まえることを書いた。AC1 に `init` の不使用を足した |
| 目的レビュー・保証すべきこと（8 件） | 一致（1 件を明記） | 7 件は §1 対象外、§2、I1〜I10、S1〜S5 に既にある。「同じ要求・候補・規則なら同じ結果（決定性）」は §2 の答えと `ContentResolver` の責務に明記した |
| 目的レビュー・作らないべきこと（8 件） | 一致 | 8 件とも本書の対象外・§2.3・§3 の「足さない」判断に既にある。初稿が作る要らないものは無い。「record の保存・可視化・送信」を §1 対象外と §2.3 に明記した |
| 目的レビュー・文言への指摘（5 件: 「同じ契約」の範囲、「妥当な候補」の決め手、「何が失敗したか」の粒度、Scene と Asset の契約の共通性、「併用」の対象） | **保留（owner の裁定）** | 目的の文言そのものを問う指摘は人間が裁定する規則。主担当の案は「文言は変えず、§1 の『目的の文言の読み方』で意味を固定する」（文言を変えると目的レビューのやり直しが要る）。owner が文言の改訂を選ぶなら、改訂後の 2 欄の原文だけで目的レビューを 1 回やり直してから凍結する（Q6） |

### 計画レビュー R1（2026-11-08、外部、owner 経由。PR #171 のレビュー、対象 commit `d5f15d3` = A2 入力版）

判定「基本方針を支持。A2 入力として妥当。A3 凍結時に契約の保証範囲を確認したい」。任意の層・必須依存を増やさない・所有解放の再利用・G03 (a)/(b) の分離・core の offline 検査を支持。確認事項 5 件と Q1〜Q7 への提案。R1 は A2 入力版を見ており、A2 の採用分（本版）は見ていない。

| 指摘 | 採否 | 反映 |
|---|---|---|
| R1-1 [重要] 採用候補の由来と実ロード元の区別。loader を `ContentDirectory` 候補に限り、未対応種別を拒否し、record の保証は選択結果までと明記。directory の一致は S3 の gate に残す | 採用（A2 の A-04・A-07 と同じ方向。既に反映済みの部分を含む） | loader の種別の制限と E6 は A-04 で反映済み。§2「成果の範囲」に「record が保証するのは選択結果まで。実ロード元の証拠ではない。要求・候補・採用結果・実ロード・ロード失敗は別の事実」を明記。`ContentLoadRecord` の XML doc の要求に同じ文を加えた。directory の一致は §2.3 の S3 行のまま |
| R1-2 [重要] 表現の不一致の拒否を resolver の条件と反例テストにする。候補の `ProviderId` / `SourceKind` と probe した provider の一致 | 採用（表現は A-03 で反映済み。provider の一致は新規） | I1 に provider の一致を足し、`ProbeOutcome.ProviderMismatch` と T21 を足した。表現は T14・T15 |
| R1-3 [重要] `IsDigestVerified` の記録だけでは、全件採用の fake rule で未検証 directory が成功扱いになる。全候補への digest 必須化は snapshot に不適切。種別ごとの受け入れ根拠が無い候補をロード成功として扱わない境界を契約に | 採用 | `IsDigestVerified`（bool）を `AcceptanceBasis`（`None` / `VerifiedDigest` / `OpenSceneSnapshot`）に置き換え、resolver に「根拠 `None` は採用しない」「種別ごとに要る根拠の固定表（directory → `VerifiedDigest`、snapshot → `OpenSceneSnapshot`、Addressables → 本スライスでは採用しない）」を入れた。S1 の互換判定（一致の判定）とは別の境界であることを I2 に明記。`ProbeOutcome.NoAcceptanceBasis`、T22、反例 (f') を足した。record は `AcceptanceBasis == None` でも作れない |
| R1-4 [中] Rejected 後の pin と回復条件: expected digest が欠ける場合、digest が同じで target が違う場合、権限拒否の場合 | 採用（A-02 の反映を調整） | A-02 で「`Rejected` の expected identity に `Digest` 必須（constructor で拒否）」としていたが、権限拒否は修復の基準を持たないので表現できない。基準の無い `Rejected` は constructor で拒まず、resolver がその場で `IntegrityRejected` として止める（下位へ進まない）に改めた。digest 同じ・target 違いは全欄一致の規則で不一致（I2 に明記）。T17 を書き直した |
| R1-5 [中] resolve 成功後の load 失敗で `Probes`・採用候補との対応が呼び手から失われないか。失敗試行の記録を S3 に送るなら非保証として明記 | 採用（S3 へ移送、非保証を明記） | §2「成果の範囲」に S0-a の非保証として明記し、§2.3 の S3 行に「loader の resolve と load を 2 段の公開入口に分ける案を含む」と書いた。本スライスでは loader の公開面を増やさない |
| R1 の Q1〜Q7 への提案（Q1 normal 支持、Q2 `ValueTask` 支持、Q3 支持、Q4 許容、Q5 S1 へ、Q6 概ね支持（「何を得たか」の保証と候補選択記録の区別を反映）、Q7 S4 への委譲は可。ただし同一要求内の fail-closed と pin を保持・渡す主体の契約は S0-a に残す） | 記録（裁定は owner） | レビュー担当の提案として記録する。owner の回答ではない。Q7 の「pin を保持・渡す主体」は `ContentResolutionResult.PinnedIdentity` として呼び手に渡す形で I2 に明記した。Q6 の「何を得たかの保証と候補選択記録の区別」は上の R1-1 の反映で §2 に入った |

### S0-b ブランチの確認（2026-11-08、`claude/play-content-s0b-spike` の head `01ee19b`、判定 C の再実行待ち）

S0-b の HANDOFF（`CONTENT_DEMAND_ROUTING_SPIKE.md`）と実験 code を読み、S0-a の契約に影響する点を反映した。S0-b の結論（(i) / (ii) の判定）はまだ出ていないので、どちらにも中立な形にした。

| S0-b で分かったこと | S0-a への反映 |
|---|---|
| 機構 (ii) では snapshot が一時 Content Directory として登録され、directory の経路で読まれる。由来は snapshot のまま | 由来（`ContentSourceKind`）とロード経路（`ContentLoadPath`）を別の欄にした。受け入れ根拠の表は由来で引き、loader の制限は経路で引く。`SourceKindUnsupported` を `LoadPathUnsupported` に改名し、E6 に (ii) 型の対照を足した。A2 入力版・A3 整理版の「loader は `ContentDirectory` 以外の `SourceKind` を拒む」は (ii) を拒んでしまうので直した |
| 2 件目の directory の拒否は `InstallContentDirectory` と `ContentRevisionGate.Reserve` の 2 か所（S0-b の判定 C が `osm.secondRegistration` で実測） | §1 現況に追記。directory の基数（U1 (2)）の対象が 2 か所であることを後続へ渡す |
| 所有を (a) SceneBase lifecycle / (b) 物理 Scene の unload 責任 / (c) AssetManagement の台帳の 3 層で観測している | `ContentLoadRecord.OwnerLabel` が書くのは (c) だけと明記。(a) は S3 の SceneSystem adapter |
| SceneDirector 経由の取消は PreLoad の窓に限られ、Unity Scene の load は `CancellationToken.None` で走る（S0-b B 適応 9） | S0-a の loader は呼び手の token を `IAssetManagement` へそのまま渡す（既に §3）。SceneDirector 経由の取消の窓は S3 の入力で、§2.3 の S3 行に含まれる |
| S0-b は program §8 の S0-b 行を自分の HANDOFF へのリンクに置換した。S0-a も隣の行を置換している | 2 つの PR の後にマージする側で §8 の表の隣接行の衝突を解消する（内容の衝突は無い） |
| fixture の Scene 名は `Boot` / `Entry` / `Next` / `Other` / `Dup` | S0-a の E1 の `Entry` と同じ語彙（G01）。衝突なし |

### 計画レビュー R2・R3（2026-11-08、対象 commit `a2c6efe` = S0-b 整合後の A3 整理版）

- **R2（外部、owner 経由。PR #171 のレビュー）:** 判定「R1 の指摘は概ね解消。方向性を支持し、下記の契約上の曖昧さを整理すれば A3 凍結へ進んでよい」。R1-1〜R1-5 の反映と、由来・経路の分離、`UA-*.dll` 参照の P 検査を確認。確認事項 R2-1〜R2-3。
- **R3（外部、cursor[bot]。PR #171 の comment。base `5f971da` の code と突き合わせた静的レビュー）:** 判定「基本方針を支持。probe の結果と要求の失敗コードの対応、loader が経路を拒むときの例外の形、pin の入力を閉じれば進めてよい」。R1 の反映、由来・経路の分離、`ContentRevisionGate.Reserve` の現行 code との一致を確認。指摘 R3-1〜R3-3 と「凍結時に 1 行足すもの」3 件。
- 2 件は独立で、互いの指摘を見ていない。採否は主担当が整理した（owner の裁定は要らない範囲: 目的・利用者・受け入れ境界・risk・常時契約に触れない）。

| 指摘 | 採否 | 反映 |
|---|---|---|
| R2-1 / R3-2 [重要] `LoadPathUnsupported` が resolver の失敗コードと同じ enum にあり、loader の拒否が resolver の成功結果を「候補が無かった」に書き換えうる。E6 で `Result.IsResolved` 等を assert できる形に | 採用 | `ContentResolutionFailureCode` に `None = 0` を足し `LoadPathUnsupported` を外した。loader の拒否は新しい enum `ContentLoadRejection`（`None` / `LoadPathUnsupported`）で、`ContentResolutionException` に `Result`（resolver の結果そのまま）と `Rejection` の 2 欄を持たせた。E4・E5 に `Probes` と `Rejection == None`、E6 に `Result.IsResolved == true`・`Candidate`・`Probes`・`FailureCode == None`・`Rejection == LoadPathUnsupported` を足した。§3 の「失敗の表現」に候補選択と実ロードを別の事実として扱う理由を書いた |
| R2-3 後半 / R3-2 [中] E6 の `Addressables` は `SourceKind.Addressables` だと resolver が採用せず loader に届かない | 採用 | E6 を「resolver が採用する由来・根拠の組で `LoadPath` だけ未対応」の 2 件（`EditorSnapshot` + `OpenSceneSnapshot` + `AlreadyLoadedScene`、`ContentDirectory` + `VerifiedDigest` + `LoadPath.Addressables`）にし、loader の分岐を実際に通ったことを `Result` の値で assert する |
| R2-2 / R3-3 [中] 部分的な `ExpectedIdentity` はどの候補にも一致せず非自明。(A) 全欄必須を constructor で強制、(B) 部分制約と完全 pin を別概念に | 採用（(A)） | pin にする identity（`ContentRequest.ExpectedIdentity`、`Rejected` の `ExpectedIdentity`）は 4 欄が全て非空の物だけ。一部の欄だけの値は constructor が `ArgumentException`。`ContentIdentity` に `IsComplete` を足し、`MatchesPin` は完全な pin を前提に全欄一致。部分制約は S0-a では持たない。T8・T16・T17 に反例を足した |
| R2-3 前半 / R3-1 後半 [中] T18 の「identity は一致、根拠が足りない」を `PinMismatch` と呼ぶと T22 の `NoAcceptanceBasis` と重なる | 採用 | T18 を `NoAcceptanceBasis` に改めた。probe の `Outcome` は「その候補を採らなかった理由」、要求の `FailureCode` は「要求が終わった理由」と I5 に書き分けた |
| R3-1 [重要] (1) `PinMismatch` の後に後続の一致候補を見るかが開いている（並び順に依存しない契約にするなら次へ進む）。(2) 誰も採用しなかったときの `FailureCode` の優先順位の表が無い。(3) `KindMismatch` / `RepresentationMismatch` が次へ進むか未定 | 採用 | I5 に「探索を止めるのは採用・identity の無い `Rejected`・pin の食い違い・`Fault`・取消の 5 つだけ。他の不採用は記録して次へ進む」と、`FailureCode` の優先順位 (1)〜(4) を書いた。I2 の `PinMismatch` を「記録して次へ進む」に改めた。T24（A `Rejected`(X)、B Y、C X 検証済み → C 採用）と T25（優先順位の 4 組）を足し、T3・T11・T14・T16 に後続の採用例を足した |
| R3 凍結時に 1 行足すもの: loader の戻り型、`SceneLoadOptions` の既定と `ActivateOnLoad == false` の例外の扱い、取消は各 probe の前、`OperationCanceledException` に `Result` を付けない、E1 の unload 回数は fake の scene が `IContentDirectoryToken` を実装している前提 | 採用 | §3 loader 行（戻り型 `UniTask<SceneLoadOutcome>` / `UniTask<AssetLoadOutcome<T>>`、`options = default`、`ContentDirectoryException` は包まない）、I5（取消の時点、OCE に `Result` を付けない）、T12、E1 にそれぞれ書いた |

### 計画レビュー R4（2026-11-08、外部、cursor[bot]。PR #171 の comment。対象 commit `3734f91`）

判定「基本方針を支持。R2・R3 の反映で、失敗コードの優先順位、経路拒否の例外、完全な pin は一通りに読める。要求 pin と `Rejected` pin の食い違い、空の `ProviderId` の成功経路、Scene の `GetOwners` の文言を閉じれば進めてよい」。配置（`Core/` と loader の分離、asmdef・`IAssetManagement`・所有者・寿命を増やさない、AC3 と P 検査で internal を塞ぐ、受け入れ根拠の表を resolver に置く）はアーキテクチャゲートに合うとした。R3-1 の優先順位は再開しない。現行 code との照合で、`AssetOwner.Scene(id).ToString()` が `Scene(id)`、`LoadContentSceneAsync` の `ActivateOnLoad == false` の拒否、`UnloadSceneAsync` の token 分岐と `ReleaseScene` の挙動、cache 無しの `Release` 回数、2 か所の 2 件目拒否、行数の引用が本書と一致することを確認した。

| 指摘 | 採否 | 反映 |
|---|---|---|
| R4-1 [重要] 要求 pin X と `Rejected`（X'）の食い違いが T で固定されておらず、pin を置き換える実装が通る | 採用 | I2 に「pin の供給源は 2 つ、どの組み合わせでも食い違いは即停止、`Rejected`（X）は続行」を書き、T26 を足し、T4 に要求 pin 付きの対照を 1 句足した |
| R4-2 [重要] 空の `ProviderId` を policy も候補も拒まず、record の段階で初めて落ちてロード後に非 `ContentResolutionException` が出る | 採用 | `ResolutionPolicy` の `Id` と `ContentCandidate.ProviderId` を空不可・ordinal 比較にし、T9 に反例を足した。record 側の拒否は残す |
| R4-3 [重要] Scene 要求の所有は `GetOwners` では観測できない（scene 台帳に載るだけで asset 台帳に owner を取らない） | 採用 | S3 を Scene（`OwnerLabel` と unload → `ReleaseScene`）と Object（`GetObjectKey` の key で `GetOwners`）に分け、`OwnerLabel` の作り方（`AssetOwner.Scene(LogicalKey).ToString()` / owner の `ToString()`）を §3 に、`LogicalKey` の `IsNullOrWhiteSpace` 拒否を `ContentRequest` に書いた。E2 に `GetObjectKey` と cache 無しの前提を書いた |
| R4-4 [中] 形の壊れた `ContentAvailability`（`Available` で候補無し等の 4 組）の扱いが未定 | 採用 | constructor が `ArgumentException` で拒む。`default` だけが resolver の `Fault`。T20 に足した |
| R4-5 [中] loader の入口と `Kind` の不一致の扱いが未定 | 採用 | 入口が resolver を呼ぶ前に `ArgumentException`。`ContentLoadRejection` は増やさない。E2 に足した |
| R4-6 [中] token が取消されていない `OperationCanceledException` の扱いが未定 | 採用 | `IsCancellationRequested` が真のときだけ取消、それ以外の OCE は `Fault` → `ProviderFault`。I5 と T12c に書いた |
| 凍結時に 1 行足すもの: `ResolveAsync` の signature、pin の無しは `ContentIdentity?` の null、EditMode の namespace の明示 | 採用 | §3 `ContentResolver` 行、I2 と `ContentAvailability` / `ContentResolutionResult` 行、§5 EditMode の冒頭にそれぞれ書いた |

owner の判断（2026-11-08）: R4 の反映をもって凍結する。「厳密にやりすぎるとぼやける」ので、これ以上の外部レビューの巡は Phase A では行わず、残る不確実性は Phase B / C の発見に委ねる。

### A3 の確認事項と owner の裁定（2026-11-08。owner は「R1 の推奨どおり」と裁定した）

- **Q1 risk:** `normal` のまま。新しい公開面（任意の層の型）を足すが、既存の公開 API・asmdef・所有者・寿命は変えない（§0）。
- **Q2 core の非同期型:** `ValueTask<T>`。UniTask と offline project への NuGet 参照は採らない（§2.5、§3、AC1）。
- **Q3 offline project と CI:** `tools/ContentResolutionOfflineTests/` を判定必須（AC1・AC8）に含め、CI の matrix に足す（AC7）。
- **Q4 P 検査の方式:** 「判定 C の offline 実行」。script・入力・結果は判定 evidence bundle に固定する（§5「P 検査」）。
- **Q5 `IContentCompatibilityRule` の製品実装:** S0-a では作らず S1 に送る（§2.3）。
- **Q6 目的・利用者の文言:** §1 の原文のまま据え置き、目的レビューの 5 件は §1「目的の文言の読み方」で意味を固定する。「何を得たかの保証」と「候補選択の記録」の区別は §2「成果の範囲」に反映済み（R1-1）。目的レビューのやり直しは要らない。
- **Q7 要求をまたぐ pin:** S4 へ送る。同一要求内の fail-closed（基準の無い `Rejected` と pin の食い違いで即停止）と、pin を `ContentResolutionResult.PinnedIdentity` として呼び手へ渡す契約は S0-a に残す（I2）。

主担当への owner の指示（2026-11-08）: 推奨が決まっている問いは主担当が答えを出し、owner に渡すのは裁定が本当に要るもの（目的・利用者の文言、受け入れ境界の変更、risk、常時契約に関わるもの）に限る。

## 6. Phase B 実装結果

（未到達）

### 動作確認

（未到達。検証経路は `対象なし`）

## 7. Phase C

- 種別: 発見（implementation base `5f971da` / head `7aa249d`。GO 判定はしていない）
- 使用したevidence bundle の取得案内・状態: 未生成（§0）。入力は commit `7aa249d` と、確認済みの Phase A snapshot。
- 構造適合: 適合。§3 の責務マップと実物を照合した。`Core/` 20 file は表と 1 対 1 で、loader と outcomes は `Core/` の外の 2 file。asmdef の追加・`IAssetManagement` の member 追加・解放 API の追加は無い。AC2 は `git diff --name-status` の全 54 行が許可リストの種類に収まる。行数は `ContentResolver.cs` 318 行（予想 170）、offline の `Program.cs` 693 行（予想 480）で予想を超えるが、前者は探索の 1 責務で 500 行未満、後者は T ごとの method が並ぶテスト harness なので、分割は不要と判断した。XML doc の要求（`IContentCompatibilityRule`・`ContentRequestLoader`・`ContentLoadRecord`）は満たしている。§0 の「実装の読み方」の I5（途中で止めた場合も同じ優先順位）は、I5 の優先順位 (2) が `Fault` を含むことと整合する。
- 現在の問いを阻害する findings（違反する凍結済み条件 / 常時契約を併記）: なし。
- 後続スライスへ移送する findings:

| id | severity / category | 根拠 | 内容 | bucket・移送先 |
|---|---|---|---|---|
| D-1 | low / semantic | `Core/ContentResolver.cs` 50〜51 行目、`Core/ResolutionPolicy.cs` 33 行目のコメント | `provider.Id` / `SourceKind` は `try` の外で読む。getter が例外を投げる、または `Id` が null を返す adapter では、`ContentResolutionResult` も probe も作られずに例外が抜ける。policy のコメントは id を「1 回だけ読む」と書くが、resolver が読み直す。I5 と T20 の対象を `FindAsync` と `Accepts` と読み、宣言値の getter は adapter の契約とした（I5 の文言の隙間で、凍結条件の違反とはしない）。owner が S4 への移送を裁定した（決定記録 DR-1） | 後続の入力・S4（adapter の契約）。unique。未修正 |
| D-2 | low / semantic | `Core/ProviderProbe.cs`、T19・T26 | `ProviderProbe` は `Rejected` の `ExpectedIdentity` を持たないので、pin の食い違いの相手（X'）は `Reason` に書かれない限り record に残らない。§3 の凍結した欄のとおりで違反ではない | 後続の入力・S3 / S5（record の観測）。unique。未修正 |
| D-3 | info / semantic | T25、§0「実装の読み方」 | 「pin が未充足のまま `Fault`」→ `IntegrityRejected` の組み合わせを固定する T が無い。実装は I5 の優先順位どおり | 後続の入力・S1 以降でテストを足す場合。unique。未修正 |

- 実行したテストコマンドと `-Filter`、対象を選んだ理由: `pwsh tools/run-tests.ps1 -Filter "OneStarMaker.Tests.ContentResolution;OneStarMaker.Tests.AssetManagement"`（§5 の起点 filter。Unity コンパイルと `.meta` の import の確認を兼ねる。`Library` は同じ Unity 版の別 worktree から複製）。`dotnet run -c Release --project tools/ContentResolutionOfflineTests/ContentResolutionOfflineTests.csproj`。`pwsh tools/contract-audit.ps1`。AC3 の grep 2 本と AC9 の grep。
- テスト結果（XML 上の実行テスト名と件数）: EditMode total 57、passed 57、failed 0、skipped 0、compile error 0。内訳は `OneStarMaker.Tests.AssetManagement` 48 件、`OneStarMaker.Tests.ContentResolution` 9 件（E1〜E6 の `ContentRequestLoaderTests`。E6 は 2 case と対照 1 件、E2 は入口の取り違えの 1 件を含む）。Unity 6000.6.0f1。offline suite は 28 passed、0 failed、§5 の 28 件の名前が全て `PASS` 行にある（exit 0、.NET SDK 10.0.401 で net8.0 を実行）。contract-audit は errors=0 warnings=0。AC3 の 2 本は 0 件、AC9 は `Cancel` を含む行を除いて 0 件。新規 `.meta` は import で書き換わらなかった（差分 0。folder の `.meta` は既存の形と同じ）。
- 判定必須のうち未実行: 全 EditMode 回帰の base / head 比較（AC6）、P 検査（AC4）。判定 C が GO 候補 head で実行する。
- 重い検証を発見段階で限定実行した場合の理由と範囲: なし（起点 filter だけ）。
- 未確認事項: Unity の import が `MobileDependencyResolver/Editor` の `*.pdb.meta` 5 件を消した（`*.pdb` が git 管理外のため。本スライスの差分ではない）。`git restore` で戻した。base で同じ事象が起きるかは確かめていない。
- 担当・モデル: Claude Code（Anthropic、Claude Opus 5.5、ローカルの新規 session）。**`独立性制約あり`:** B と同じモデルで、「Phase C は Phase B と異なるモデル」を満たさない。owner が 2026-11-08 に、発見 C に限る例外として本 session での続行を裁定した。同じモデルの見落としの可能性が残るので、判定 C は B と別のモデルが担当する。

### 判定 C（2026-11-08、GO）

- 種別: 判定（implementation base `5f971da` / head `7aa249d`。local tag `evidence/generic-content-request-contract/1`）
- 使用した evidence bundle の取得案内・状態: 判定 evidence bundle はローカル固定済み（未昇格）。取得案内は owner の手元。
- 担当: 判断は Grok 4.7 High（cursor-agent、xAI、B と別ベンダー、新規 session 2 回）。実行は Claude Code（Opus 5.5、Anthropic、新規 session）で、凍結済みのコマンドと判断担当が書いた script だけを実行した。判断担当の環境で shell が起動しなかったので（理由の無い `Rejected:`）、P 検査の script（判定 C の offline 実行）と XML 比較 script は判断担当が書き、実行担当が無改変で実行して生の出力を判断担当へ返した。
- 構造適合: 適合。§3 の配置・依存と実物が一致する。asmdef・既存の `Runtime/**`・`SampleGame/**`・`ProjectSettings/`・`Packages/` は変更集合に無い。
- AC1〜AC9、I1・I2・I5・I7・I8・I10、S1〜S5: 全て PASS（判断担当の報告）。AC6 と S5 は DR-2 の読み方（下記）。
- 現在の問いを阻害する findings: なし。
- 後続へ移送する findings: DR-1（§7 上の発見 C の D-1）のまま。下の 1 件を追加する。

| id | severity / category | 根拠 | 内容 | bucket・移送先 |
|---|---|---|---|---|
| J-1 | low / machine | 全 EditMode の head run 1・2、`BuildContentDirectoryIntegrationTests.ScenePrefabTextureAndTwoRepresentationsBuildAndMoveAsOneDirectory` | 同じ 1 件が run 1 で `Directory.Move` の `IOException`（`BuildContentCoordinator.cs:97`）、run 2 で 360 秒の Timeout で失敗し、他の Unity が動いていない run 3 で Passed（112 秒）。S0-a の差分はこのテストにも Build 系にも触れていない。run 2 の実行中は別 session の Unity が同じ PC で動き、同じ worktree で `git restore` をした。run 1 が他の作業と重なったかは確認していない。原因は未確定。ASSET_CYCLE と BOOTSTRAP の Timeout と同じテストで、`Access denied` は今回初めて記録された | 後続の入力・BuildSystem 側（既存の Timeout の移送先と同じ）。未修正 |

- 実行したテストコマンドと結果: `pwsh tools/run-tests.ps1`（空 filter、EditMode、Unity 6000.6.0f1、sandbox 外）。
  - base `5f971da`: total 1655、passed 1630、failed 0、skipped 0、inconclusive 25、XML の root result `Passed`。
  - head `7aa249d` run 3: total 1664、passed 1639、failed 0、skipped 0、inconclusive 25、root result `Passed`。
  - 比較（引数を含む full name）: base のケースは全て head にあり（欠落 0）、base の Passed が head で Skipped / Failed になったものは 0、head の新規 9 件は全て `OneStarMaker.Tests.ContentResolution` で Passed。Inconclusive の 25 件は両方で同じケース（`SampleGame.Tests.Rendering.Vfx.*PlayTests`、`-nographics` による `Assume`）。`PublisherBackupPredicate_IsExactSuffix` の 1 件は XML に同じ full name が 2 件あり、base と head で同じ（Passed）。
  - head run 1・2 は失敗した（J-1）。生結果は判定 evidence bundle と blind bundle に収録した。head run 3 の前に、fixture の残骸を退避し、他の Unity が動いていないことを確かめた。実行回数と順序は owner の了承による。
  - offline suite: 28 passed、0 failed、exit 0。P 検査: `P-CHECK pass=True`、exit 0（既存 7 陽性集合と `RESOLVER-player` / `RESOLVER-editor` が exit 0、陰性対照 3 つが exit 1 で理由が期待どおり。script は判断担当が書いた派生で、凍結 script との差分は §5 の 3 集合と先頭の BOM だけ）。contract-audit: errors=0 warnings=0。AC3・AC9 の grep: 0 件（AC9 は `Cancel` の誤検知を除く）。
- プロセス終了の記録（XML の結果は変わらない）: base と head run 1・3 は、結果 XML の完成後に Unity が終了せず、実行担当がその Unity だけを終了させた（既知の native 終了 stall。原因調査はしていない）。head run 2 は自然終了した（Unity の exit 2）。**`run-tests.ps1` の `step.json` は、base と head run 3 で `status=failed`、`exitCode=1`、理由は `Inconclusive のテストがあります`。** 強制終了が理由ではない。判定 C の入力には「強制終了のため exit 1」と誤って書いて渡し、判定 C は `step.json` を読んでいない。この点は C' の F-01 で明らかになり、DR-2 で裁定した。
- 判定必須のうち未実行: なし。
- 重い検証を発見段階で限定実行した場合の理由と範囲: なし。
- 未確認事項: J-1 の原因。上の Unity の強制終了は規則上「成功証拠にしない」と書かれた事象で、受け入れは DR-2 による owner の裁定。
- 決定記録: DR-1、DR-2（`GENERIC_CONTENT_REQUEST_CONTRACT_DECISIONS.md`）。

## 8. Phase C'

- 種別: 最終（高コスト証拠なし）。1 巡目（全体）と 2 巡目（F-01 の裁定と AC6・S5）。implementation base `5f971da` / head `7aa249d`。
- 使用した blind audit bundle の取得案内・状態: C' blind bundle はローカル固定済み（未昇格）。担当の入力は、この bundle を `fetch` で検証して取り出した写しと、head の `git archive`。発見 C・判定 C の所見と裁定は入れていない。2 巡目に渡した裁定は DR-2 だけ。別 session からの取得確認は未実施（§0）。
- 担当・モデル: Codex CLI（OpenAI、`gpt-6-luna`、reasoning high、巡ごとに新規 session）。OpenAI は Phase A・B・C に未関与で、B（Anthropic）・C の判断（xAI）と別ベンダー。独立性の制約なし。
- 現在の問いを阻害する findings: 1 巡目に F-01 の 1 件（AC6・S5、`run-tests.ps1` の `step.json` が `failed`。上記）。owner の裁定 DR-2 により 2 巡目で解消（C' は「XML の読み方は凍結された確認手段に沿い、条件の意味・合否を変えない」と判断した）。2 巡目は `AUDIT: PASS`。
- 後続スライスへ移送する findings: 新規なし（§2.3 の S0-b・S1・S3 の所有事項の確認のみ）。
- 監査結果: 実装の契約違反・構造の劣化は指摘なし。
- 残存リスク: Inconclusive の 25 件は Passed として実行されていない（DR-2 の範囲）。base と head run 3 は自然終了していない。XML に同じ full name が各 1 件重複している（比較には影響しない）。
- 監査できなかった範囲: Unity の実行と再現（監査の規則どおり実施しない）。固定 bundle の外の実環境。
- 判定 C と C' の突き合わせ: Phase D で行う。F-01 は判定 C が見落とした点（`step.json`）を C' が見つけたもの（入力の誤記を含む。§7）。実装に関する所見で両者の矛盾はない。

## 9. Phase D

（未到達）
