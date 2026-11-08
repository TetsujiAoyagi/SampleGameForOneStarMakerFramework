# 汎用の Content 要求・候補・記録の契約（S0-a）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `A`（**A2 と外部の計画レビュー R1 を反映した A3 の整理版。凍結していない。実装キューではない。** A2 入力版は commit `d5f15d3`。本版は A2（§5）と R1（PR #171 のレビュー、owner 経由、対象 `d5f15d3`）の採用分を反映した整理版で、owner の Q1〜Q7（§5）への回答の後に A3 凍結版として commit する。整理版を commit するのは owner の指示（2026-11-08。外部レビューの入力に A3 版を使うため）で、Phase A の commit を A2 入力版と A3 凍結版の 2 点に限る規則からの明示的な逸脱として記録する。program [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S0-a。A0 の入力は同書 §3〜§6・§9・§10 U1・M-03・M-06。S0-b（`CONTENT_DEMAND_ROUTING_SPIKE`、別ブランチで判定 C 中）と S0-c とは独立で並行（同書 D11））
- branch: `claude/adoring-galileo-4afnfa`（S0-a のスライスブランチ。PR の base は `develop`）
- implementation base commit: `5f971da`（`develop` の head、2026-11-08。本書の実測は全てこの base）
- implementation head commit: 未固定
- risk: `normal`（program §8 のとおり。既存の公開 API・asmdef・所有者・寿命・`SceneState`・Addressables・起動順序を変えず、任意の層に新しい型を足すだけ。新しい公開面を足すことを「公開 API を変える」高リスクに数えるかは A3 で owner が確認する（§5 Q1））
- owner: root（依頼者）。A2 / B / C / C' の担当は Phase ごとに選ぶ
- created: 2026-11-08
- expires: 2026-12-31、または Phase D で harvest・削除した時点。期限時は owner が更新・廃止を判断する
- harvest to: `unity/Assets/Docs/Architecture/13-resource-system.md`（要求時解決の任意の層と所有の契約。§2 決定事項サマリと §5 Interface 設計に節を足す。`IAssetManagement` に member を足さなかった理由、規則の製品実装が S1 にある理由、ロード元の同一性が S3 まで未検証であることを含める）、`27-folder-structure.md` §2.2（`Runtime/ContentResolution/` の依存位置: AssetManagement の公開面と共通契約だけに依存し、BuildSystem 本体・SceneSystem・Bootstrap を参照しない、AssetManagement の上の任意の層）、`unity/Assets/README.md`（プロジェクト構造の木と AssetManagement の節）。program の harvest（§18 §4 等）は program の完了時
- 関連: [ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)（**所有境界の正本。** resolver は AssetManagement の上の任意の層で、BuildSystem 本体を参照しない。同書 §2.1 の「連携部分（両方を知る）」とは別の分類として扱い、U-A / U-B の P 検査の集合に参照を増やさない。境界を本書で定義し直さない）、[RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md](RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md)（本スライスは起動コードに触れないので順序制約の対象外。§2.4）
- Phase A snapshot の取得案内・状態: 未生成。A1 / A2 / A3 整理の主担当の環境（Cloud の session。Unity・pwsh・dotnet・Artifact Storage なし）では stage できない。A2 入力版は commit `d5f15d3` と本書の内容 sha256（§5 A2 記録）で固定した。A3 凍結時に、凍結版の本書をローカルの担当が `evidence stage` で固定する
- Phase B result snapshot / evidence bundle / C' blind bundle の取得案内・状態: 未生成（未到達）
- 引継ぎ票:
  - 次の Phase・役割: A3 の凍結（owner + ローカルの担当）。owner が §5「A3 の確認事項」Q1〜Q7 に答え、必要なら文言を直して、A3 凍結版として commit する。目的・利用者の 2 欄（§1）を変えるなら、変えた 2 欄の原文だけで目的レビューを 1 回やり直してから凍結する。
  - 次の作業: 凍結版の本書を Phase A snapshot として `evidence stage` で固定し、Phase B の起動指示に凍結 commit の SHA と取得案内を渡す。
  - 引き継ぐ commit: 本版（A3 整理版）を含む commit は PR #171 の状態ブロックに書く。A2 入力版は `d5f15d3`。凍結版の commit は凍結時に tree の外で渡す。
  - 必要な権限: なし（文書のみ）。
  - 停止規則: program §0 の停止規則を引き継ぐ。S0-b の結論が出るまで、`IAssetManagement` / `IContentDirectoryBackend` / `InstallContentDirectory` の変更、配布粒度、複数 directory の合成を凍結しない（本書はこれらを変えない。§2.3）。製品 code への統合（S3）は本書の契約凍結後（program M-01）。**owner の明示指示（2026-11-08）: A3 まで進め、まだ凍結しない。**
  - 引き渡さないもの: なし。

### PR 状態ブロック

PR を作る前は本 §0 を唯一の正本とする。PR 作成時にテンプレートの形で本文先頭へ写す。

## 1. 目的と対象外

- 目的: Scene や Asset のロード要求に対して、供給元の種類・所在・取得手順を知らない呼び出し側が、要求時に妥当な候補を得て、既存の AssetManagement と同じ契約で所有・解放でき、何をどの供給元から得て何が失敗したかを説明できるようにするための、汎用の要求・候補・規則・記録の契約を定義する。SceneDirector、SampleGame、BuildPurpose を知らない fake の供給元だけでこの契約が成り立つことを示す。契約は AssetManagement の必須依存にせず、この契約を使わない既存の AssetManagement と BuildSystem の単独利用と併用を変えない。
- 利用者: OSMFramework の開発者（この契約の上に、供給元の adapter、SceneSystem adapter、Editor の Play 入口を後続スライスで実装する人）。このスライスに利用者の操作は無い。
- 目的・利用者の文言の改訂（A1）: A0（program §1・§8 S0-a 行）から A1 で書き起こした初出の文言。A2 の目的レビューはこの原文を見た（§5 A2 記録）。A2 の目的レビューは文言への指摘 5 点を出し、その採否は owner が裁定する（§5 Q6）。本版では文言を変えていない。
- 目的の文言の読み方（A3 整理。文言は変えず、曖昧と指摘された点の本書での意味を固定する。owner が Q6 で文言の改訂を選べばここを文言へ移す）:
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
  - `Runtime/AssetManagement/AssetManagement.ContentDirectory.cs`（164 行）: `_contentDirectory` は単一。`InstallContentDirectory` は internal で、2 件目を `InvalidOperationException` で拒否する（20〜22 行目）。`RequireContentDirectory` は未登録なら `DirectoryNotRegistered` を投げる。`LoadContentSceneAsync` は同じ identity が台帳にあれば既存の handle を返し（116〜124 行目）、表現は `_contentSceneRepresentations` に要求のまま記録する（127 行目）。owner 台帳（`AssetRegistry`）と resident cache は AssetManagement が唯一の owner で、directory 由来の token は `IContentDirectoryToken` marker で見分ける。
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
- どうすればこの問いに誤って YES を出せるか（反例。A2 と C はこれを探す）: (a) fake provider のテストが通るだけで「SceneDirector 無しで使える」と言う。実際に AssetManagement へ繋いで Scene / Object を得て解放する経路（G03 (b)）を別に示さなければ YES にしない（AC5）。(b) resolver 無しの既存テストが通るだけで汎用性を言う（G03 (a) と (b) は別の証拠。AC6）。(c) 候補に由来（供給元の種別・provider）が無くても成功にする（AC8 T7）。(d) 新しい型を 0 にして `IAssetManagement` の内側で探索を済ませる（I8 違反を「新しい interface は要らなかった」と読む。AC2・AC3）。(e) SampleGame の宣言名・Scene 名・Season・Cell・VCS の語彙を汎用型に入れて通す（AC9）。(f) 優先順位が高い供給元の候補をそのまま採用し、利用可能・互換の判定を飛ばす（I5 違反。AC8 T2）。(g) 失敗した供給元の後に別 revision の候補が見つかったら採用する（program §4.1 の fail-closed 違反。AC8 T3）。(h) loader が実装していない種別（`EditorSnapshot` / `Addressables`）の候補を directory から読み、record に申告の種別を書く（由来の虚偽。AC5 E6）。(i) loader が同じ assembly の AssetManagement の internal を呼んで封鎖や台帳を迂回する（AC3・AC4）。
- 判定定義: §2.1 の不変条件と成立条件を §2.2 の受け入れ条件で全て確認できれば GO。一つでも未達なら NO-GO。スパイクではないので CONDITIONAL ACCEPT は使わない。
- このスライスの成果の範囲: GO は「要求・候補・規則・記録・所有の契約が、AssetManagement の必須依存にならずに、fake の供給元と既存の AssetManagement の上で成立した」ことだけを意味する。実際の供給元（installed cache、LAN、Editor snapshot）からの解決、SceneDirector との接続、採用した候補と実ロード元の同一性、Play 入口の成立は主張しない。program §8 の「誤った合格を除外する」の列をそのまま引き継ぐ。**record が保証するのは選択結果まで**（どの要求に、どの供給元に何と答えられ、どの候補をどの受け入れ根拠で採用したか）で、実ロード元を確認した証拠ではない（`SourceBindingVerified=false`）。要求・候補・採用結果・実ロード・ロード失敗は別の事実として扱い、S0-a が証明するのは前 3 つの対応だけ。採用後のロード失敗（`ContentDirectoryException` 等）で `Probes` と採用候補の対応を呼び手に残すことは S0-a の非保証で、S3 の入力（§2.3。R1 の 5）。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし（A3 後に作る場合は本書と別の記録にする）

### 2.1 進める最低条件

**不変条件（起きてはいけないこと）**

- **I1 要求の不変:** 呼び出し側が与えた logical key・kind・表現・expected identity を、resolver も provider も変えない。候補の logical key または kind が要求と違えば採用しない。要求の表現が空でなければ候補の表現は完全一致でなければ採用せず、候補の表現が空なら採用しない（loader が空の表現で `LoadContent*Async` を呼ぶと、既存 index の `Full` fallback が実際の表現を変え、台帳には空が記録される）。要求に expected identity があれば、それを最初の pin とする（I2 の比較）。候補の `ProviderId` と `SourceKind` は、その候補を返した provider の `Id` と `SourceKind` に一致しなければ採用しない（record の由来が probe した供給元と食い違わないため。R1 の 2）。
  - 崩しうる経路: (a) provider が別の logical key・kind・表現の候補を「見つかった」と返す。(a') provider が別の provider 名や別の種別を名乗る候補を返す。(b) 既存 `ContentDirectoryIndex` の Scene の `Full` fallback（[18-asset-description.md](../../unity/Assets/Docs/Architecture/18-asset-description.md) §4）が、資源を得た後の実効表現を要求と変える。候補の表現を空にしないことで loader が空を渡す経路は塞ぐが、候補が申告した表現の entry が無いときの fallback は AssetManagement の内側にある既存の挙動で、本スライスは変えず、record の表現は provider が申告した表現とする。実効表現を record に写すことは S3 の入力（§2.3）。(c) SceneDirector が名前で既ロード Scene を再利用する経路（`PerformUnitySceneLoad`）。本スライスは SceneDirector に触れない。(d) 要求の expected identity と違う identity の候補を規則が採用する。resolver は規則の前に pin で弾く。
  - 探索した境界: `IAssetManagement` の 3 つの content 入口と `ContentDirectoryIndex.TryResolveScene` / `TryResolveObject`、`SceneDirector.Loading.cs` の呼び出し 1 箇所、`AssetManagement.ContentDirectory.cs` の表現の台帳。未確認: なし。
- **I2 未検証の候補を採用理由にしない:** resolver は候補を「採用する」だけで、bytes を取得・install・Build しない。候補は**受け入れ根拠**（`AcceptanceBasis`: `None` / `VerifiedDigest`（manifest と digest の検証を通した正規コピー）/ `OpenSceneSnapshot`（Editor で開いている Scene の複製。意味は S0-b が定める））を申告し、record に残す。根拠が `None` の候補は規則が採用しても採用しない。供給元の種別ごとに要る根拠は resolver の固定表で決め（`ContentDirectory` → `VerifiedDigest`、`EditorSnapshot` → `OpenSceneSnapshot`、`Addressables` → 本スライスでは定めず、採用しない（S3））、種別と根拠が合わない候補も採用しない。この表は「どの根拠なら使ってよいか」の境界で、S1 の互換判定（identity / digest / target / schema の一致）ではない（R1 の 3）。供給元が `Rejected`（hash 不正・version 不整合・権限拒否）を返したら、その回答が `Digest` の有る expected identity を伴う場合だけ探索を続け、同じ要求の中では、その identity と全欄が一致し、かつ根拠が `VerifiedDigest` の候補以外を採用しない（同じ expected digest の正規コピーでの修復。別 revision へ黙って切り替えない。program §4.1）。`Rejected` が expected identity を伴わない（権限拒否等で修復の基準が無い）場合は、その場で `IntegrityRejected` として探索を止める（R1 の 4）。pin の比較で空の欄は不一致とし、digest が同じでも target 等の他の欄が違えば不一致とする。pin が複数あって食い違えば、その要求は `IntegrityRejected` で失敗する。pin は 1 要求の中だけで持続し、`ContentResolutionResult.PinnedIdentity` として呼び手に渡す。要求をまたぐ pin（再試行で別 revision へ切り替わらないこと）は S4 の入力（§2.3）。
  - 崩しうる経路: (a) provider が `AcceptanceBasis.VerifiedDigest` を偽って申告する（契約では防げない。S4 の adapter の責任で、本スライスは record に申告のまま残す）。(b) 探索を打ち切らず、下位の供給元の別 identity を採る。(c) record を省略して成功を返す。(d) 本スライスの loader が、採用した候補と無関係に「入っている directory」から読む。`IAssetManagement` は directory の identity を公開しないので、本スライスはこの同一性を検証できない。record はこれを `SourceBindingVerified=false` として明示し、S3 がこの接続を作るときに閉じる（§2.3）。(e) 空の identity 同士、または digest の無い identity 同士が「同じ」と判定されて修復が成立する。(f) 根拠の無い候補、または `VerifiedDigest` でない候補で修復が成立する。(f') 全件採用する規則の下で、根拠 `None` の directory 候補がロード成功として扱われる。(g) 同じ要求の再試行で、先に `Rejected` だった供給元が `Unavailable` になり、下位の別 revision が採用される（1 要求の pin では防げない。S4）。
  - 探索した境界: `IContentDirectoryBackend.BuildIdentity` は internal。`IAssetManagement` と `ISceneHandle` / `IAssetHandle` は identity を返さない。本スライスの `ContentResolver` の 1 経路。未確認: なし。
- **I5 優先度・利用可能・互換の分離:** 供給元の順序（policy）、候補があること（provider の回答）、候補を採ってよいこと（規則）は別の 3 つの判定で、record は probe した供給元ごとに、何と答えられ、なぜ採らなかったか（不在・不達・整合性拒否・規則で不採用・pin 不一致・key 不一致・kind 不一致・表現不一致・provider 不一致・受け入れ根拠なし・fault）を機械で区別できる形で残す。採用した時点で探索を止め、以後の供給元は probe しない。provider または規則が `OperationCanceledException` 以外の例外を投げたら、その probe を fault として記録して探索を止め、`ProviderFault` で失敗する（fail-closed）。
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
- **S2 fake provider での契約の成立:** offline テストが §5 の T1〜T23（T12b・T12c を含む）を全て通す（優先順位、利用可能・互換の分離、fail-closed、同 identity での修復、由来の無い成功の拒否、受け入れ根拠、要求の不変、provider の一致、Object kind、cancel、fault、汎用性の文字列検査）。
- **S3 SceneDirector 無しの AssetManagement との合成（G03 (b)）:** EditMode テストで、`AssetManagement`（`FakeAssetBackend`）に fake の `IContentDirectoryBackend` を install し、fake provider 1 つの policy で `ContentRequestLoader` が Scene と Object（`GameObject`）を要求し、handle と由来付きの record を得て、`IAssetManagement` の既存 API（`UnloadSceneAsync` / `ReleaseScene` / `Release`）で解放でき、`IAssetDiagnostics.GetOwners` の所有者が要求時の owner と一致する。SceneDirector・SceneResourceMap・SampleGame の型は使わない。
- **S4 resolver 無しの非退行（G03 (a)）:** 既存の `Tests/AssetManagement/*` と `Tests/Editor/Build/*` と `Tests/Scene/*` を変えずに全て通す。U-A・U-B の P 検査の集合は変えずに成功し、`RESOLVER-player` / `RESOLVER-editor` 集合（`Runtime/ContentResolution/` だけを、U-A + 共通契約の library を参照してコンパイル）が成功する。
- **S5 回帰:** 全 EditMode 回帰（空 filter）と `pwsh tools/contract-audit.ps1` が合格し、CI の `dotnet-offline-tests` matrix に新 project が入っている。

### 2.2 受け入れ条件

合否は各 AC の本文で決める。「確認手段」は合否を見る手段で、同じ意味を別の手段で示してもよい。

- **AC1（S1）core の純粋さ:** `Runtime/ContentResolution/Core/*.cs` の各 file が `#nullable enable` で始まり、`using UnityEngine`、`using Cysharp`、`using OneStarMaker.Runtime.AssetManagement`、`using OneStarMaker.Runtime.BuildContent`、`using OneStarMaker.Runtime.SceneSystem` のどれも持たず、`record` 宣言と `init` accessor が無い。`tools/ContentResolutionOfflineTests/ContentResolutionOfflineTests.csproj` が core の file だけを `<Compile Include>` で link し、`PackageReference` を持たず、`<LangVersion>9.0</LangVersion>` を持ち、`dotnet run -c Release --project tools/ContentResolutionOfflineTests/ContentResolutionOfflineTests.csproj` が exit 0 で、出力の末尾の件数行が `failed 0` を示す（件数の下限は AC8 のテスト名の照合で見る）。
  - 確認手段: file の先頭と `using` を読む。`dotnet run` の出力。
  - 不合格の例: core が `UniTask` を使い、csproj に UniTask の `PackageReference` を足して通す（A3 の Q2 で UniTask を選んだ場合は、この例は不合格でなくなり、AC1 の「`PackageReference` を持たず」を UniTask 1 件の許可に改める）。
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
- **AC5（S3）EditMode の合成テスト:** `Tests/ContentResolution/ContentRequestLoaderTests.cs` の E1〜E6（§5）が存在し、全て Passed。E1 は Scene を要求して `ISceneHandle.IsLoaded` が true、record の `SourceKind` が `ContentDirectory`、`ProviderId` が fake の id、`OwnerLabel` が `Scene(<identity>)`、`SourceBindingVerified` が false、`UnloadSceneAsync` → `ReleaseScene` の後に fake port の unload 回数が 1。E2 は `GameObject` を `AssetOwner.Manual` で要求して `GetOwners` に Manual が居り、`Release` の後に空。E4 は provider が NotFound のとき `ContentResolutionException`（`NoCandidate`）が投げられ、fake port の load 回数が 0（探す段とロードを分ける）。E5 は provider が Rejected のとき `IntegrityRejected` で load 回数 0。E6 は `SourceKind.Addressables` / `EditorSnapshot` の候補を loader が `SourceKindUnsupported` で拒み、load 回数 0。
  - 確認手段: 結果 XML の実行テスト名と結果。
- **AC6（S4, I8）既存テストの非退行:** base と GO 候補 head のそれぞれで、同じ機械・同じ Unity 版・同じコマンド（`pwsh tools/run-tests.ps1`、空 filter、EditMode）で全 EditMode を実行する。base の結果は 1 件以上実行かつ failed 0 であること（そうでなければ比較の基準にしない）。head が合格し、head の結果 XML の実行テストのケース ID（parameterized テストの引数を含む full name）の集合が base の集合を含み、base で passed のケースが head で skipped / failed になっていない。`pwsh tools/contract-audit.ps1` 合格（検査 9 の path 予算を含む。最長の新 path は §2.4）。
  - 確認手段: 2 つの XML から full name と結果を抜き出して比べる。総数・passed・skipped・failed を記録する。base が failed 0 にならない場合は比較の基準が成立しないので、head を判定せずに人間へ返す。
- **AC7（S5）CI の登録:** 判定 C の時点では、`.github/workflows/offline-checks.yml` の `dotnet-offline-tests` の `matrix.project` に `ContentResolutionOfflineTests` の 1 行があること、AC1 の `dotnet run` がローカルで exit 0 であることを見る。PR の CI でその job が緑であることは Phase D のマージ条件とし、判定 C の合否にしない（判定 C の時点で PR が無いため）。
  - 確認手段: yml の本文。Phase D で PR の check。
- **AC8（S2, I1, I2, I5）offline テストの名前の照合:** AC1 の `dotnet run` の出力に、§5 の T1〜T23（T12b・T12c を含む 25 件）の各テスト名について `PASS <テスト名>` の行があり、`FAIL` の行が無い。各 T が検証する条件は §5 の表で紐付ける。
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

- U-a1 core の非同期型: `System.Threading.Tasks.ValueTask<T>`（初稿の案。UnityEngine・UniTask を参照しない core を .NET 8 で素のままビルドできる。A2 のアーキテクチャゲートは netstandard 2.1 と UniTask 側からの `await` の整合を問題なしとした）か、UniTask（repo の Runtime の慣習。offline project に UniTask の NuGet 参照が要り、UniTask の .NET 実行を前提にする）か。A3 で owner が決める（Q2）。
- U-a2 P 検査の方式: 初稿は「判定 C の offline 実行」（§5）。A1 の主担当が script を書いて A2 で読ませる「A3 凍結 script」方式は、主担当の環境に Unity・pwsh が無く dry run できないので採らなかった。owner がローカルで script の改変と dry run を A3 前に行えるなら「A3 凍結 script」へ変えてよい（Q4）。

### 2.6 スライスの分割の検討

分割しない。変更理由は「要求時解決の契約を定義し、fake と既存 AssetManagement の上で成立を示す」1 つで、core（pure）と loader（Unity 側）は依存が違うが同じ問いの両半分である（core だけでは「既存の AssetManagement と同じ契約で所有・解放できる」を示せず、loader だけでは契約の汎用性を示せない）。未知は P 検査の集合追加と offline project の CI 追加の 2 つで、どちらも既存の経路（ASSET_CYCLE の P 検査、4 つの offline project）の繰り返しで独立に失敗しうる未知ではない。受け入れ条件に別の問いの判定は混じっていない（S0-b / S1 / S3 / S4 の問いは §2.3 で送った）。

## 3. 責務マップ

path は `unity/Assets/OneStarMaker/Scripts/` からの相対。型名は A3 で凍結する。行数は予想。全ての型は不変の値で、辞書・一覧は constructor で防御的に複製する。

| ファイル | 責務（一文） | 変更理由 | 所有者・寿命 | 許可する依存 | 公開面 | 単体テスト | 予想行数 |
|---|---|---|---|---|---|---|---:|
| `Runtime/ContentResolution/Core/ContentKind.cs`（新規） | 要求の種別 `None = 0`（無効）、`Scene`、`Object` | 種別を足すとき | 値型 | なし | public enum | offline | 15 |
| `Core/ContentSourceKind.cs`（新規） | 供給元の種別 `None = 0`（記録に使えない）、`EditorSnapshot`、`ContentDirectory`、`Addressables` | 供給元の種別を足すとき（S0-b / S3） | 値型 | なし | public enum | offline | 20 |
| `Core/ContentAcceptanceBasis.cs`（新規） | 候補の受け入れ根拠 `None = 0`（採用不可）、`VerifiedDigest`（manifest と digest の検証を通した正規コピー）、`OpenSceneSnapshot`（Editor で開いている Scene の複製。意味は S0-b）。種別ごとに要る根拠の固定表は `ContentResolver` が持つ | 根拠の種類を足すとき（S0-b / S3） | 値型 | なし | public enum | offline | 20 |
| `Core/ContentIdentity.cs`（新規） | 候補の identity（`ContentSet`、`Revision`、`Target`、`Digest`。空の欄を許す）。ordinal の等値と `IsEmpty` / `HasDigest` だけを持ち、互換の判定は持たない。pin の比較（`MatchesPin`: 全欄一致かつ空の欄は不一致）もここ | identity の欄が増えるとき | 値型（readonly struct） | なし | public | offline | 70 |
| `Core/ContentRequest.cs`（新規） | 呼び出し側の要求: `LogicalKey`（必須）、`Kind`（`None` 不可）、`Representation`（要求元の data。空は「指定なし」）、`ExpectedIdentity`（任意。最初の pin）、`CorrelationId`（任意）。SceneDirector の状態・親子・Season・SceneResourceMap・VCS を持たない | 要求の欄が増えるとき | 呼び出し側が作る不変の値 | なし | public sealed class | offline（T8） | 60 |
| `Core/ContentCandidate.cs`（新規） | provider が返す候補: `ProviderId`、`SourceKind`（`None` 不可）、`LogicalKey`、`Kind`、`Representation`（空不可）、`Identity`、`ManifestReference`（opaque。path を信用しない）、`AcceptanceBasis`、`Facts`（provider 固有の事実の小さな辞書。複製して保持） | 候補の欄が増えるとき | provider が作る不変の値 | なし | public sealed class | offline | 80 |
| `Core/ContentAvailability.cs`（新規） | 1 回の probe の回答: `Status`（`None = 0` は無効。`Available` / `NotFound` / `Unavailable`（未接続・timeout。次へ進む）/ `Rejected`（hash 不正・version 不整合・権限拒否。修復の基準になる `Digest` 付きの `ExpectedIdentity` を伴えば探索を続け、伴わなければ resolver はその場で止める））、`Candidate`、`ExpectedIdentity`、`Reason`。`default` は resolver が fault として扱う | 回答の種別が増えるとき | 値型 | なし | public readonly struct + enum | offline | 80 |
| `Core/IContentProvider.cs`（新規） | 供給元の差込口: `Id`、`SourceKind`、`FindAsync(ContentRequest, CancellationToken) → ValueTask<ContentAvailability>`。探すだけで、取得・install・Build はしない | 探索の契約が変わるとき | adapter（S4 / S0-b）が実装。寿命は policy の持ち主 | なし | public interface | offline（fake） | 25 |
| `Core/IContentCompatibilityRule.cs`（新規） | 候補を採ってよいかの述語: `Accepts(ContentRequest, ContentCandidate, out string reason)`。製品実装は S1。XML doc に「S0-a が既定実装を置かない理由（S1 の判定の既定を先に凍結しない）と、製品実装が無い間は policy を組めないこと」を書く | 互換判定の契約が変わるとき | policy の持ち主 | なし | public interface | offline（fake） | 25 |
| `Core/ResolutionPolicy.cs`（新規） | Play 前に固定する規則: 順序付きの `Providers`（Id 重複なし、1 件以上。複製して保持）と `Rule`。immutable。将来の要求を列挙しない | 規則の欄が増えるとき | host が作る不変の値 | `IContentProvider`、`IContentCompatibilityRule` | public sealed class | offline（T9） | 50 |
| `Core/ProbeOutcome.cs`（新規） | probe 1 回の採否の理由: `None = 0`（無効）、`Adopted`、`NotFound`、`Unavailable`、`Rejected`、`RuleRejected`、`PinMismatch`、`KeyMismatch`、`KindMismatch`、`RepresentationMismatch`、`ProviderMismatch`（候補の `ProviderId` / `SourceKind` が probe した provider と違う）、`NoAcceptanceBasis`（根拠が `None`、または種別に要る根拠と合わない）、`Fault` | 理由が増えるとき | 値型 | なし | public enum | offline | 20 |
| `Core/ProviderProbe.cs`（新規） | 探索 1 回の実績: `ProviderId`、`SourceKind`、`Outcome`、`Candidate`（あれば）、`Reason` | 実績の欄が増えるとき | 値型 | 上の型 | public sealed class | offline | 40 |
| `Core/ContentResolutionFailureCode.cs`（新規） | 解決失敗の種別: `NoCandidate`、`NoCompatibleCandidate`、`IntegrityRejected`、`ProviderFault`、`SourceKindUnsupported`（loader が実装していない種別の候補を採用した） | 種別が増えるとき | 値型 | なし | public enum | offline | 15 |
| `Core/ContentResolutionResult.cs`（新規。A2 入力版の `ContentResolution` を改名） | 1 要求の決定: `IsResolved`、`Candidate`、`FailureCode`、`Probes`（probe した供給元の実績。採用で止まる）、`PinnedIdentity`。policy（予定）と別の型。namespace と同名にしない | 決定の欄が増えるとき | resolver が作る不変の値 | 上の型 | public sealed class | offline | 60 |
| `Core/ContentResolver.cs`（新規） | 純粋な orchestration: policy の順に probe し、I1（key・kind・表現・expected identity・provider の一致）・I2（受け入れ根拠の有無と種別ごとの固定表。§4.1: `Rejected` 後は同 identity かつ `VerifiedDigest` だけ。基準の無い `Rejected` と pin の食い違いは即 `IntegrityRejected`）・I5（規則）をこの順に適用し、採用で止め、provider / 規則の例外は fault として fail-closed で止めて `ContentResolutionResult` を返す。I/O・時間・乱数・Unity を持たず決定的 | 探索の規則が変わるとき | 状態なし | 上の型 | public sealed class | offline（T1〜T7、T10〜T12、T14〜T23） | 170 |
| `Core/ContentResolutionException.cs`（新規） | 解決できなかったことを呼び出し側へ返す例外。`Result` を伴う | — | — | `ContentResolutionResult` | public sealed class : InvalidOperationException | offline | 25 |
| `Core/ContentTraceability.cs`（新規） | record の到達度 `Traceable` / `Reproducible`（本 program は `Traceable` までしか書かない） | — | 値型 | なし | public enum | offline | 15 |
| `Core/ContentLoadRecord.cs`（新規） | 1 要求の実績: 要求、採用候補（由来）、`Probes`、`OwnerLabel`（要求時に渡した owner の表記。観測値ではない）、`Traceability`、`SourceBindingVerified`（採用候補と実ロード元の同一性を検証したか。S0-a の loader は常に false）。`SourceKind == None`、`ProviderId` が空、`AcceptanceBasis == None` のどれかなら作れない（由来の無い成功は無い）。XML doc に「record は選択結果の記録で実ロード元の証拠ではない」ことと S3 の所有を書く | 実績の欄が増えるとき | loader が作る不変の値 | 上の型 | public sealed class | offline（T7） | 70 |
| `Runtime/ContentResolution/ContentRequestLoader.cs`（新規） | 決定を既存の `IAssetManagement` に接続する: `LoadSceneAsync(request, options, ct)` → `LoadContentSceneAsync(key, candidate.Representation, …)`、`LoadAssetAsync<T>(request, owner, ct)` → `LoadContentAssetAsync<T>(…)`。採用候補の `SourceKind` が `ContentDirectory` 以外なら、ロードの前に `SourceKindUnsupported` で失敗する。handle と record を返す。解放の API は足さず、呼び出し側は `IAssetManagement` の既存 API で解放する。AssetManagement の public だけを使う。XML doc に「`IAssetManagement` に member を足さない理由（I8 と program の停止規則、案 B の不採用）」と「ロード元の同一性が未検証であること（S3）」を書く | AssetManagement への接続の仕方が変わるとき（S3 で置き換わりうる） | 呼び出し側が所有。`IAssetManagement` の寿命に従う | `IAssetManagement` と Abstractions（public のみ）、Core、UniTask | public sealed class | EditMode（E1〜E6） | 140 |
| `Runtime/ContentResolution/ContentLoadOutcomes.cs`（新規） | `SceneLoadOutcome`（`ISceneHandle` + record）、`AssetLoadOutcome<T>`（`IAssetHandle<T>` + record） | — | 値 | AssetManagement の Abstractions | public sealed class × 2 | EditMode | 40 |
| `unity/Assets/OneStarMaker/Tests/ContentResolution/ContentRequestLoaderTests.cs`（新規） | E1〜E6。fake `IContentDirectoryBackend` は既存 3 箇所の private fake と同じ形を file 内に持つ（共有のために既存 fake を動かさない） | — | — | `OneStarMaker.Tests` の既存参照 | — | — | 300 |
| `tools/ContentResolutionOfflineTests/ContentResolutionOfflineTests.csproj`、`Program.cs`（新規） | T1〜T23（25 件）。core の `.cs` を link。`LangVersion` 9（Unity のコンパイラに合わせ、新しい構文の混入を .NET 側で捕まえる） | — | — | .NET 8 SDK だけ | — | — | 25 + 480 |
| `.github/workflows/offline-checks.yml` | `dotnet-offline-tests` の matrix に 1 行 | — | — | — | — | — | +1 |

変更しない file（責務の参照だけ）: `Runtime/AssetManagement/IAssetManagement.cs`（75 行、増分 0）、`AssetManagement.ContentDirectory.cs`（164 行、増分 0）、`Internal/IContentDirectoryBackend.cs`（36 行、増分 0）、`Runtime/BuildContent/**`（増分 0）。行数警報（500 行・3 責務・50% 増）に該当する file は無い。

配置・依存・所有・寿命・テスト境界を選んだ理由:

- **folder `Runtime/ContentResolution/`（`OneStarMaker.Runtime.asmdef` の配下、namespace `OneStarMaker.Runtime.ContentResolution`）:** フォルダ境界の基準（architecture-gates）の「依存方向」と「所有者」が既存 folder と違う。AssetManagement の公開面と共通契約だけに依存し、AssetManagement・BuildSystem 本体・SceneSystem・Bootstrap のどれからも参照されない、AssetManagement の上の任意の層である。[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md) §2.1 の「連携部分（両方を知る）」とは別の分類で、BuildSystem を参照しない。asmdef 分割スライスへは「`ContentDirectorySession` と同じ assembly に寄せる理由は無い」と渡す。既存の連携部分（`Runtime/BuildContent/ContentDirectorySession.cs` 等）と同じ folder に置かないのは、(a) U-B の P 検査の集合から file 名で除外する一覧を増やさずに folder 単位で外せること、(b) 変更理由（要求・候補・規則の契約）が directory session（native handle と token の寿命）と独立なこと、の 2 つが違うため。asmdef は分けない（asmdef の追加は設計判断で、所有は asmdef 分割スライス）。同じ assembly に居ることで internal が見える問題は AC3・AC4 で機械的に塞ぐ。
- **`Core/` sub-folder:** UnityEngine・UniTask を参照しない file だけを置き、offline の .NET project がこの folder の file だけを link する。「Unity 非依存」を folder の境界で見えるようにし、Unity 側の接続（loader）を同じ folder の外の 1 file に限る。
- **core の非同期型に `ValueTask<T>`（U-a1）:** 探索は本質的に非同期（LAN の probe、S4）だが、core は Unity に依存しないので UniTask を使わない。netstandard 2.1 の `ValueTask` は Unity の Runtime asmdef でコンパイルでき、UniTask 側の loader は `await` できる。代替（UniTask + offline project への NuGet 参照）は A3 で owner が選ぶ。
- **`IAssetManagement` に member を足さない:** 由来（どの directory か）を handle に持たせると `IAssetManagement` の変更になり I8 と program の停止規則（Runtime の新 API を S0-b 前に凍結しない）に当たる。record は loader が、採用した候補から作る。実ロード元との同一性の検証は S3（§2.3）で、record の `SourceBindingVerified=false` がその未検証を明示する。
- **`IContentCompatibilityRule` を差込口だけにする:** 互換の判定内容は S1 の問い。S0-a が「identity 等値」のような既定実装を置くと、S1 の判定の既定値を先に凍結してしまう。製品実装が無い間は policy を組めないが、S0-a は契約の成立だけを示す（§2 成果の範囲）。
- **失敗の表現:** 解決できないこと（候補なし、互換なし、整合性拒否、fault、未対応の種別）は `ContentResolutionResult` の結果で返し、loader の公開入口だけが `ContentResolutionException` に変える（呼び出し側の `try` が 1 つで済む）。AssetManagement のロード失敗（`ContentDirectoryException` 等）は包まずにそのまま通す（reason code の所有を変えない。[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md) G5）。
- **loader が `ContentDirectory` 以外の候補を拒む:** loader が実装している経路は directory だけで、他の種別の候補を directory から読むと record の由来が虚偽になる。種別ごとの経路は S0-b（snapshot）と S3（Addressables）が足す。
- **解放の API を足さない:** 所有・解放は `AssetOwner` と `IAssetManagement` の既存契約（`Release` / `UnloadSceneAsync` → `ReleaseScene` / `ReleaseAll`）をそのまま使う。「同じ契約で扱える」は、新しい解放 API を作らないことで示す。
- **テスト境界:** core は .NET だけで決定的にテストする（offline）。loader は `AssetManagement` の実物 + fake の `IAssetBackend` / `IContentDirectoryBackend` で EditMode テストする（Unity の test runner は要るが、Editor Play・AssetDatabase・Addressables の実体には依存しない。既存の `AssetManagementTests` と同じ条件）。
- **既存 fake を動かさない:** 3 箇所の `FakeDirectoryPort` は private の入れ子型で共有できず、共有 fixture へ昇格させると既存テスト file の変更になり AC2 に当たる。本スライスは file 内の private fake を 1 つ足す。共有化は後続の整理。

## 4. 実装計画

- 変更対象: §3 の表のとおり。
- 開始の前提: 本書の A3 凍結版が本ブランチにあり、Phase B は凍結版の commit（tree の外で渡す SHA）から始める。S0-b の結果を待たない（独立）。
- 順序: (1) `Core/` の型を §3 の順（enum → 値型 → interface → policy → probe → resolver → record）で書く → (2) `tools/ContentResolutionOfflineTests/` を作り T1〜T23 を書いて `dotnet run` で通す（B exit の offline suite） → (3) `ContentRequestLoader` と outcomes を書く → (4) `Tests/ContentResolution/ContentRequestLoaderTests.cs` に E1〜E6 を書く → (5) Unity Editor を開いてコンパイル確認（エラー 0）。Unity が生成した folder の `.meta`（兄弟の `ContentResolution.meta` を含む）を commit に含め、他の file の書き換えは含めない（AC2） → (6) `offline-checks.yml` に 1 行足す → (7) AC3・AC9 の grep と `pwsh tools/contract-audit.ps1` → (8) B の出口: commit、§0 の票（implementation head 欄は (b) の「この票を含む commit」。`docs/handoff/` 以外を変える commit なので可）。起点 filter の EditMode テストと P 検査は Phase C が実行する。
- Phase B から Phase A へ差し戻す条件: §3 に無い public 型・member が要る（特に `IAssetManagement` / `ISceneHandle` / `IAssetHandle` への追加）。core に UnityEngine / UniTask / BuildContent / SceneSystem の参照が要る。`ValueTask` を UniTask の loader から `await` できない。loader が AssetManagement の internal を使わないと E1〜E6 が成立しない。既存テストの assertion を変えないと通らない。
- 対象外を維持する方法: `Runtime/AssetManagement/**`、`Runtime/BuildContent/**`、`Runtime/SceneSystem/**`、`Runtime/Bootstrap/**`、`SampleGame/**`、`.asmdef` を開かない。AC2・AC3 で確認する。

## 5. テストとレビュー計画

- 単体テスト（新しい中核ロジック = `ContentResolver` と record の検証）: offline の T1〜T20。fake provider は `Available` / `NotFound` / `Unavailable` / `Rejected` を script 化して返し、probe の回数と順序を数える。保留中の probe は `TaskCompletionSource` で表す。fake rule は候補ごとの採否を表で返すか、指定した例外を投げる。各 T の名前は `PASS <名前>` の行として出力し、AC8 で照合する。

| T | 名前 | 検証する条件 | 内容 |
|---|---|---|---|
| T1 | `Priority_FirstAvailableWins` | I5 | A が `NotFound`、B が `Available` → B を採用。`Probes` は A（`NotFound`）、B（`Adopted`）の順で 2 件 |
| T2 | `Priority_IsNotCompatibility` | I5、反例 (f) | A が `Available` だが rule が拒否、B が `Available` で rule が採用 → B を採用。A の probe は `RuleRejected` |
| T3 | `Rejected_ThenDifferentIdentity_FailsClosed` | I2、反例 (g) | A が `Rejected`（expected X）、B が `Available`（Y、検証済み）→ 失敗 `IntegrityRejected`。B の probe は `PinMismatch`。採用候補は無い |
| T4 | `Rejected_ThenSameVerifiedIdentity_Repairs` | I2 | A が `Rejected`（X）、B が `Available`（X、`AcceptanceBasis.VerifiedDigest`）で rule が採用 → B を採用。`PinnedIdentity` が X |
| T5 | `AllNotFound_NoCandidate` | I5 | 全て `NotFound` → `NoCandidate` |
| T6 | `AllUnavailable_NoCandidate_WithDistinctProbes` | I5 | 全て `Unavailable` → `NoCandidate`。`Probes` の `Outcome` で不在と不達を区別できる |
| T7 | `Record_WithoutProvenance_IsRejected` | 反例 (c) | `ContentLoadRecord` を `SourceKind.None` または空の `ProviderId` で作ると `ArgumentException` |
| T8 | `Request_Validation` | I1 | 空の `LogicalKey` と `Kind.None` は `ArgumentException`。`Representation` の null は `ArgumentNullException`、空は許す |
| T9 | `Policy_Validation_AndDefensiveCopies` | — | provider 0 件、Id の重複、rule の null は `ArgumentException`。渡した一覧・辞書を後から変えても policy / candidate の中身は変わらない |
| T10 | `Candidate_WithDifferentKey_IsProviderFault` | I1 | provider が要求と違う `LogicalKey` の候補を返す → その probe は `KeyMismatch`、次へ進む。全てそれなら失敗 `ProviderFault` |
| T11 | `ObjectKind_ResolvesAndKindMismatchIsRejected` | I1 | `Kind = Object` の要求が同じ規則で解決される。候補の `Kind` が違えば `KindMismatch` |
| T12 | `Cancelled_BeforeFirstProbe` | — | 取消済みの token で `OperationCanceledException`、probe の回数 0 |
| T13 | `CoreSources_DoNotNameUnityOrHosts` | I7、AC1 | `Core/` の各 file の本文に `UnityEngine`、`Cysharp`、`SceneDirector`、`SampleGame`、`Spring`、`Season`、`Cell`、`RunPurpose`、`SceneResourceMap`、`UICommon`、`Svn`、`Workset` が現れない（`Cell` は `CancellationToken` を除く） |
| T14 | `Representation_MustMatchWhenRequested` | I1 | 要求 (Entry, Scene, "Low")、A が `Available`（"Full"）→ `RepresentationMismatch`。要求の表現が空なら候補の "Full" を採用し、record の表現は "Full" |
| T15 | `Candidate_EmptyRepresentation_IsNotAdopted` | I1 | 候補の表現が空 → `KeyMismatch` と同列の不採用（`RepresentationMismatch`）。`ContentCandidate` の constructor でも空を拒む |
| T16 | `ExpectedIdentity_IsInitialPin` | I1、I2 | 要求に `ExpectedIdentity=X`、A が `Available`（Y）で rule が採用 → `PinMismatch`、失敗 `IntegrityRejected`（候補が無ければ `NoCompatibleCandidate` ではなく、pin 違反を優先） |
| T17 | `Rejected_WithoutDigest_StopsImmediately_AndEmptyNeverMatches` | I2 | A が `Rejected` を expected identity 無し（権限拒否）または `Digest` 無しで返す → その場で `IntegrityRejected`、B は probe されない。identity が空同士の候補は pin に一致しない。digest が同じで target が違う identity も一致しない |
| T18 | `Repair_RequiresVerifiedCandidate` | I2 | A が `Rejected`（X）、B が `Available`（X、根拠 `None` または `OpenSceneSnapshot`）→ `PinMismatch`、失敗 `IntegrityRejected` |
| T19 | `ConflictingPins_FailClosed` | I2 | A が `Rejected`（X）、B が `Rejected`（X'）、C が `Available`（X'、検証済み）→ 失敗 `IntegrityRejected`。C は probe されない（pin の食い違いで止まる） |
| T20 | `RuleException_IsFaultAndStops` | I5 | A が `Available`、rule が `InvalidOperationException` を投げる → A の probe が `Fault`、B は probe されず、失敗 `ProviderFault`。`default(ContentAvailability)` を返す provider も `Fault` |
| T12b | `Cancelled_WhilePending_SkipsLaterProviders` | — | A が保留中（TCS）に cancel → `OperationCanceledException`、B は probe されない |
| T12c | `Provider_IgnoringToken_IsNotAdopted` | — | A が token を無視して cancel 後に `Available` を完了しても、採用せず `OperationCanceledException` |

| T21 | `Candidate_FromDifferentProvider_IsProviderMismatch` | I1 | provider A が `ProviderId="B"` または別の `SourceKind` を名乗る候補を返す → `ProviderMismatch`、次へ進む。全てそれなら失敗 `ProviderFault` |
| T22 | `Candidate_WithoutAcceptanceBasis_IsNeverAdopted` | I2、反例 (f') | 全件採用する rule の下で、`ContentDirectory` の候補の根拠が `None`、または `OpenSceneSnapshot`（種別と不一致）→ `NoAcceptanceBasis`、採用しない。`EditorSnapshot` の候補に `OpenSceneSnapshot` があれば採用する（resolver は種別を特別扱いせず表で判定する）。`Addressables` の候補はどの根拠でも採用しない |
| T23 | `Adoption_StopsProbing` | I5 | A が `Available` で採用 → B は probe されず、`Probes` は 1 件 |

（T12b・T12c は T12 の補助で、AC8 の照合対象に含める。合計 25 件。）

- EditMode（`OneStarMaker.Tests`、`Tests/ContentResolution/`）:
  - E1 Scene（G03 (b)）: `AssetManagement(FakeAssetBackend)` に fake `IContentDirectoryBackend` を `InstallContentDirectory`（internal。テストは既存と同じく internal を見られる）。policy = fake provider 1 件（`SourceKind.ContentDirectory`、`Available`、表現 "Full"）+ 採用する rule。`ContentRequestLoader.LoadSceneAsync(request("Entry", Scene, "Full"))` → `IsLoaded`、record の由来・`OwnerLabel == "Scene(Entry)"`・`SourceBindingVerified == false`。`UnloadSceneAsync("Entry")` → `ReleaseScene("Entry")` → fake port の unload 回数 1。`FakeAssetBackend.LoadSceneCallCount == 0`（Addressables 側を通らない）。
  - E2 Object: `LoadAssetAsync<GameObject>(request("Widget", Object, "Full"), AssetOwner.Manual)` → handle の `Value` が非 null、`GetOwners(key)` に `Manual`。`Release(handle)` → `GetOwners` が空、fake port の release 回数 1。
  - E3 record の由来と要求の対応: E1 の record の `Request` が渡した要求と同値、`Candidate.Representation == "Full"`、`Traceability == Traceable`、`Probes` が 1 件で `Adopted`。
  - E4 探す段とロードの分離: provider が `NotFound` → `ContentResolutionException`（`NoCandidate`）。fake port の load 回数 0。
  - E5 fail-closed: provider が `Rejected` → `IntegrityRejected`。load 回数 0。
  - E6 由来の虚偽の拒否: provider の `SourceKind` が `Addressables`（もう 1 件 `EditorSnapshot`）→ `SourceKindUnsupported`。load 回数 0。
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

### A3 採否（主担当の整理、2026-11-08。owner の裁定待ち。凍結していない）

| 指摘 | 採否 | 反映 |
|---|---|---|
| A-01 S2・I1・I2・I5 に紐付く AC が無く、AC1 は件数だけを見る | 採用 | AC8（T の名前と `PASS` 行の照合）を足し、§5 の T 表に各 T が検証する条件を併記した。AC1 の件数の下限を外し AC8 に委ねた |
| A-02 Rejected 後の pin が空洞（空 identity の等値、未検証の修復、要求をまたぐ pin、pin の食い違い） | 採用（(3) は S4 へ移送） | I2 を改め、修復は `Digest` 付きの expected identity を伴う `Rejected` の後だけ、pin の比較は全欄一致かつ空は不一致、修復の候補は根拠 `VerifiedDigest` 必須、pin の食い違いは `IntegrityRejected`、pin は 1 要求の中だけと定めた。T17・T18・T19 を足した。要求をまたぐ pin は §2.3 で S4 の入力にした（基準の無い `Rejected` の扱いは R1-4 で調整） |
| A-03 resolver が表現と expected identity を照合しない。空の表現が既存 index の fallback と台帳のずれを招く | 採用 | I1 を改め、要求の表現が空でなければ完全一致、候補の表現は空不可、`ExpectedIdentity` を最初の pin とした。`ProbeOutcome.RepresentationMismatch` を足し、T14・T15・T16 を足した |
| A-04 loader が `ContentDirectory` 以外の種別の候補を directory から読み、record の由来が虚偽になる | 採用 | loader はロードの前に `SourceKindUnsupported` で失敗する。`ContentResolutionFailureCode` に値を足し、E6 と反例 (h) を足した |
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

### A3 の確認事項（owner へ。凍結前に回答を得る。R1 の提案は上の表に記録したが、裁定は owner が行う）

- **Q1 risk:** `normal` のままでよいか。新しい公開面（任意の層の型）を足すが、既存の公開 API・asmdef・所有者・寿命は変えない。A2 のアーキテクチャゲートは配置と依存を問題なしとした。
- **Q2 core の非同期型:** `ValueTask<T>`（初稿。A2 で整合を確認）か UniTask（offline project に NuGet 参照）か。主担当の推奨は `ValueTask<T>`。
- **Q3 offline project と CI:** `tools/ContentResolutionOfflineTests/` を判定必須（AC1・AC8）に含め、CI の matrix に足してよいか。
- **Q4 P 検査の方式:** 「判定 C の offline 実行」でよいか。owner がローカルで `p-check.ps1` の `$Sets` の改変（U-A の library を参照する `RESOLVER-*`）と dry run を A3 前に行えるなら「A3 凍結 script」に変える。
- **Q5 `IContentCompatibilityRule` の製品実装を S0-a で作らない**（S1 に送る）でよいか。製品実装が無い間は製品の policy を組めない。
- **Q6 目的・利用者の文言:** §1 の原文のまま確定し、目的レビューの 5 件の指摘は §1「目的の文言の読み方」で意味を固定する（主担当の案）か、文言を改めて目的レビューを 1 回やり直すか。
- **Q7 要求をまたぐ pin を S4 へ送ること**（A-02 (3)。同じ要求の再試行で別 revision へ切り替わりうる既知の制約を S0-a では受け入れる）でよいか。

## 6. Phase B 実装結果

（未到達）

### 動作確認

（未到達。検証経路は `対象なし`）

## 7. Phase C

（未到達）

## 8. Phase C'

（未到達）

## 9. Phase D

（未到達）
