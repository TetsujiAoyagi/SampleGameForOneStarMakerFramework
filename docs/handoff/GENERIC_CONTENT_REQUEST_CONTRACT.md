# 汎用の Content 要求・候補・記録の契約（S0-a）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。空欄を保存済み・引渡し済みと扱わない。

- type: `slice`
- status: `A`（**A1 初稿、A2 入力版。凍結していない。実装キューではない。** program [PLAY_CONTENT_RESOLUTION_PROGRAM.md](PLAY_CONTENT_RESOLUTION_PROGRAM.md) §8 の S0-a。A0 の入力は同書 §3〜§6・§9・§10 U1・M-03・M-06。S0-b（`CONTENT_DEMAND_ROUTING_SPIKE`、別ブランチで判定 C 中）と S0-c とは独立で並行（同書 D11））
- branch: `claude/adoring-galileo-4afnfa`（S0-a のスライスブランチ。PR の base は `develop`）
- implementation base commit: `5f971da`（`develop` の head、2026-11-08。本書の実測は全てこの base）
- implementation head commit: 未固定
- risk: `normal`（program §8 のとおり。既存の公開 API・asmdef・所有者・寿命・`SceneState`・Addressables・起動順序を変えず、任意の層に新しい型を足すだけ。新しい公開面を足すことを「公開 API を変える」高リスクに数えるかは A3 で owner が確認する（§5 A3 の確認事項 Q1））
- owner: root（依頼者）。A2 / B / C / C' の担当は Phase ごとに選ぶ
- created: 2026-11-08
- expires: 2026-12-31、または Phase D で harvest・削除した時点。期限時は owner が更新・廃止を判断する
- harvest to: `unity/Assets/Docs/Architecture/13-resource-system.md`（要求時解決の任意の層と所有の契約。§2 決定事項サマリと §5 Interface 設計に節を足す）、`27-folder-structure.md` §2.2（`Runtime/ContentResolution/` の依存位置: AssetManagement の公開面と共通契約だけに依存し、AssetManagement・BuildSystem 本体・SceneSystem・Bootstrap のどれも参照しない任意の層）、`unity/Assets/README.md`（プロジェクト構造の木と AssetManagement の節）。program の harvest（§18 §4 等）は program の完了時
- 関連: [ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)（**所有境界の正本。** resolver は同書 §2.1 の「連携部分」の分類に入り、U-A / U-B の P 検査の集合に参照を増やさない。境界を本書で定義し直さない）、[RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md](RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md)（本スライスは起動コードに触れないので順序制約の対象外。§2.4）
- Phase A snapshot の取得案内・状態: 未生成。A1 の主担当の環境（Cloud の session。Unity・pwsh・dotnet・Artifact Storage なし）では stage できない。A2 入力版は本書を含む commit（tree の外で渡す SHA）と本書の内容 sha256 で固定する（§5 A2 記録）。A3 凍結時に、凍結版の本書をローカルの担当が `evidence stage` で固定する
- Phase B result snapshot / evidence bundle / C' blind bundle の取得案内・状態: 未生成（未到達）
- 引継ぎ票:
  - 次の Phase・役割: A2（独立レビュー 2 件: アーキテクチャゲート、目的レビュー。§5 レビュー計画）。A2 は本書の主担当の session が起動する。
  - 次の作業: A2 の回答を §5 の A2 記録に書き、採用・不採用・保留を A3 の整理として分類する。**凍結は owner の指示があるまで行わない**（owner の明示指示: 2026-11-08「A3 まで進める。まだ凍結しない」）。A2 中の改訂と A3 の整理版は commit しない（Phase A の commit は A2 入力版と A3 凍結版の 2 点だけ）。Cloud の session は容器が消えるので、整理版は内容 sha256 を添えた「未 commit の版」として owner へ file で渡す。
  - 引き継ぐ commit: 本書を含む A2 入力版の commit（tree の外で渡す）。
  - 必要な権限: なし（文書のみ）。
  - 停止規則: program §0 の停止規則を引き継ぐ。S0-b の結論が出るまで、`IAssetManagement` / `IContentDirectoryBackend` / `InstallContentDirectory` の変更、配布粒度、複数 directory の合成を凍結しない（本書はこれらを変えない。§2.3）。製品 code への統合（S3）は本書の契約凍結後（program M-01）。
  - 引き渡さないもの: なし。

### PR 状態ブロック

PR を作る前は本 §0 を唯一の正本とする。PR 作成時にテンプレートの形で本文先頭へ写す。

## 1. 目的と対象外

- 目的: Scene や Asset のロード要求に対して、供給元の種類・所在・取得手順を知らない呼び出し側が、要求時に妥当な候補を得て、既存の AssetManagement と同じ契約で所有・解放でき、何をどの供給元から得て何が失敗したかを説明できるようにするための、汎用の要求・候補・規則・記録の契約を定義する。SceneDirector、SampleGame、BuildPurpose を知らない fake の供給元だけでこの契約が成り立つことを示す。契約は AssetManagement の必須依存にせず、この契約を使わない既存の AssetManagement と BuildSystem の単独利用と併用を変えない。
- 利用者: OSMFramework の開発者（この契約の上に、供給元の adapter、SceneSystem adapter、Editor の Play 入口を後続スライスで実装する人）。このスライスに利用者の操作は無い。
- 目的・利用者の文言の改訂（A1）: A0（program §1・§8 S0-a 行）から A1 で書き起こした初出の文言。A0 の目的の欄を改めたのではなく、S0-a の範囲に限定して書いた。A2 の目的レビューはこの原文を見る。A2 または A3 で変えたら、変えた 2 欄の原文だけで目的レビューをやり直す。
- 対象外（所有する後続。§2.3 に一覧）:
  - Editor で開いている Scene の snapshot の機構、既ロード Scene の取り込み方、I11（開いている Scene を壊さない）: S0-b
  - `InstallContentDirectory` の 2 件目拒否の規則（directory の基数）: S0-b の結論の直後に切る製品スライス（program U1 (2)）
  - 互換 fingerprint の中身（identity / digest / target / schema の汎用判定、`declaration` の述語）: S1
  - logical key → package の索引、Stage II の粒度: S2
  - provider route → install → Unity load → 所有の接続、SceneDirector 経由の経路、`InstantiateContentAsync` 相当の経路、採用した候補と実際にロードした directory の同一性の検証、既存 index の表現 fallback を record に写すこと: S3
  - Local / LAN の供給元 adapter、timeout、prefetch、checkout hook: S4
  - Play ボタンの入口、通知、観測 UI、menu: S0-c / S5
  - 本スライスの folder を別 asmdef に分けること、P 検査の常設化: asmdef 分割スライス、[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md) D4
- 現況（base `5f971da` の実測。path は `unity/Assets/OneStarMaker/Scripts/` からの相対）:
  - `Runtime/AssetManagement/IAssetManagement.cs`（75 行、public）: Content Directory の型付き入口は `LoadContentAssetAsync<T>(logicalKey, representation, owner)`、`LoadContentSceneAsync(sceneIdentity, representation, options)`、`InstantiateContentAsync(logicalKey, representation, parent)` の 3 つ。どれも「既に入っている 1 つの directory」から logical key と表現で読むだけで、供給元の探索も、どこから得たかの記録も返さない。provider・候補・policy に当たる型は無い。
  - `Runtime/AssetManagement/AssetManagement.ContentDirectory.cs`（164 行）: `_contentDirectory` は単一。`InstallContentDirectory` は internal で、2 件目を `InvalidOperationException` で拒否する（20〜22 行目）。`RequireContentDirectory` は未登録なら `DirectoryNotRegistered` を投げる。owner 台帳（`AssetRegistry`）と resident cache は AssetManagement が唯一の owner で、directory 由来の token は `IContentDirectoryToken` marker で見分ける。
  - `Runtime/AssetManagement/Internal/IContentDirectoryBackend.cs`（36 行、internal）: 1 directory 分の port。`BuildIdentity` / `Target` / `CachePrefix` と load / unload / release / close / Play 停止の操作を持つ。候補の探索は無い。
  - `Runtime/BuildContent/ContentDirectorySession.cs`（481 行、internal）: 1 directory = 1 root の登録、native handle、token、unregister を所有する連携部分。`Register` / `RegisterVerified` は internal。`Runtime/BuildContent/Distribution/InstalledRevisionVerifier.cs` の `VerifiedInstalledRevision`（internal）が「検証済み候補」の形（`Identity`、`ContentSet`、`Target`、`ManifestSha256`、`ContentPath`）を持つ。
  - `Runtime/BuildContent/Distribution/ContentTransportManifest.cs`: `declaration`（名前・表現・runtime 既定値）は optional で検証されない（program §2.4）。
  - `Runtime/SceneSystem/SceneDirector.Loading.cs` 629〜656 行目 `PerformUnitySceneLoad`: Runtime で `LoadContentSceneAsync` を呼ぶ唯一の呼び手。表現は起動時に固定した `_sceneVariant`。名前で `SceneManager.GetSceneByName` に当たれば既にロード済みとして再利用し、由来は区別しない。
  - 共通契約 `Runtime/AssetBuildContracts/`（`AssetType`、`ContentDirectoryException` / `ContentDirectoryFailureCode`）は PR #165 で切り出し済み。AssetManagement と BuildSystem 本体は互いの型を参照せず、両方を知るのは連携部分（[27-folder-structure.md](../../unity/Assets/Docs/Architecture/27-folder-structure.md) §2.2）。
  - テスト: `OneStarMaker.Tests`（EditMode、`Tests/AssetManagement/`）は `FakeAssetBackend`（internal `IAssetBackend` の fake）で Addressables に依存せずに AssetManagement を検証している。`IContentDirectoryBackend` の fake は `Tests/AssetManagement/SealedAssetBackendTests.cs`（`FakeDirectoryPort`）、`Tests/Scene/SceneVariantForwardingTests.cs`（`DirectoryScenePort`）、`Tests/Editor/Build/ContentDirectorySessionTests.cs` の 3 箇所に各 file 内の private 型としてある。Unity を起動しない .NET 8 の offline テストは `tools/*OfflineTests/`（4 project）にあり、Runtime の pure な `.cs` を `<Compile Include>` で link して `dotnet run` で走らせ、CI の `.github/workflows/offline-checks.yml` の `dotnet-offline-tests` job が project 名の matrix で回している。
  - P 検査（Unity 同梱 `csc` によるソース集合の閉包検査）の script `p-check.ps1` は ASSET_CYCLE スライス（PR #165）の Phase A snapshot に凍結されていて、git には無い。集合の正本は同スライスの HANDOFF の表（U-A: `Runtime/AssetDescriptions/` + `Runtime/AssetManagement/` + `Runtime/AssetBuildContracts/`。U-B runtime: `Runtime/BuildContent/` から連携 2 file と OSM 固有 2 file を除く + 共通契約）で、[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md) §5.1 に写しがある。
  - 本書の作成環境に Unity・pwsh・dotnet が無く、`tools/docs-audit.ps1`・`tools/contract-audit.ps1` は未実行。link 先の存在は `git ls-files` で手で照合した。A2 入力版の commit の前に owner 側で通す必要は無い（Phase A は push を配布点にしない）が、PR 作成前に通す。
- UC ごとの利用者の入力: なし（利用者の操作は無い）。

## 2. 意思決定と受け入れ境界

- このスライスが答える問い: SceneDirector や BuildPurpose が無くても、要求・候補・所有の契約を定義できるか。既存の `IAssetManagement` / `IContentDirectoryBackend` / DIST で何を再利用でき、新しい interface は本当に要るか。
- 問いへの A1 の答え（A2 が検証する仮説）: **要る。ただし小さな任意の層として。** `IAssetManagement` は「入っている 1 つの directory から logical key と表現で読む」契約で、供給元の探索・採用理由・由来の記録を返さない。これを `IAssetManagement` の全 load API に組み込む案（program §5.1 案 B）は I8（単独利用）に反する。再利用するのは、所有・解放・同時ロードの調停（`IAssetManagement` の公開 API と `AssetOwner` をそのまま使い、所有の契約を定義し直さない）、失敗の reason code（`ContentDirectoryException` を包まず、そのまま呼び出し側へ通す）、検証済み候補の「形」（`VerifiedInstalledRevision` の identity / contentSet / target / digest の欄を候補の `ContentIdentity` に写す。型そのものは internal で BuildSystem 本体に属するので参照しない）。新しく要るのは、要求（`ContentRequest`）、供給候補とその可否（`IContentProvider` / `ContentAvailability` / `ContentCandidate`）、規則（`ResolutionPolicy` と S1 が実装を入れる `IContentCompatibilityRule`）、探索の実績と由来（`ContentResolution` / `ContentLoadRecord`）の型である。`IContentDirectoryBackend` には何も足さない（directory の port は 1 directory を読む口で、探索の口ではない）。
- どうすればこの問いに誤って YES を出せるか（反例。A2 と C はこれを探す）: (a) fake provider のテストが通るだけで「SceneDirector 無しで使える」と言う。実際に AssetManagement へ繋いで Scene / Object を得て解放する経路（G03 (b)）を別に示さなければ YES にしない。(b) resolver 無しの既存テストが通るだけで汎用性を言う（G03 (a) と (b) は別の証拠）。(c) 候補に由来（供給元の種別・provider）が無くても成功にする。(d) 新しい型を 0 にして `IAssetManagement` の内側で探索を済ませる（I8 違反を「新しい interface は要らなかった」と読む）。(e) SampleGame の宣言名・Scene 名・Season・Cell・VCS の語彙を汎用型に入れて通す。(f) 優先順位が高い供給元の候補をそのまま採用し、利用可能・互換の判定を飛ばす（I5 違反）。(g) 失敗した供給元の後に別 revision の候補が見つかったら採用する（program §4.1 の fail-closed 違反）。
- 判定定義: §2.1 の不変条件と成立条件を §2.2 の受け入れ条件で全て確認できれば GO。一つでも未達なら NO-GO。スパイクではないので CONDITIONAL ACCEPT は使わない。
- このスライスの成果の範囲: GO は「要求・候補・規則・記録・所有の契約が、AssetManagement の必須依存にならずに、fake の供給元と既存の AssetManagement の上で成立した」ことだけを意味する。実際の供給元（installed cache、LAN、Editor snapshot）からの解決、SceneDirector との接続、Play 入口の成立は主張しない。program §8 の「誤った合格を除外する」の列をそのまま引き継ぐ。
- 停止規則: 進める最低条件を満たし、現在の問いに致命的な反証がなければ GO で終了する。最低条件未達のまま終了しない。
- A3 後の例外承認: なし
- 決定記録: なし（A3 後に作る場合は本書と別の記録にする）

### 2.1 進める最低条件

**不変条件（起きてはいけないこと）**

- **I1 要求の不変:** 呼び出し側が与えた logical key・kind・表現を、resolver も provider も変えない。候補の logical key が要求と違えば採用しない。表現の変更は要求元の明示（要求に表現を与えない）に限る。
  - 崩しうる経路: (a) provider が別の logical key や表現の候補を「見つかった」と返す。(b) 既存 `ContentDirectoryIndex` の Scene の `Full` fallback（要求表現の entry が無ければ `Full` へ落とす。[18-asset-description.md](../../unity/Assets/Docs/Architecture/18-asset-description.md) §4）が、資源を得た後の実効表現を要求と変える。これは AssetManagement の内側にある既存の挙動で、本スライスは変えず、record の表現は provider が申告した表現とする。実効表現を record に写すことは S3 の入力（§2.3）。(c) SceneDirector が名前で既ロード Scene を再利用する経路（`PerformUnitySceneLoad`）。本スライスは SceneDirector に触れない。
  - 探索した境界: `IAssetManagement` の 3 つの content 入口と `ContentDirectoryIndex.TryResolveScene` / `TryResolveObject`、`SceneDirector.Loading.cs` の呼び出し 1 箇所。未確認: なし。
- **I2 未検証の候補を採用理由にしない:** resolver は候補を「採用する」だけで、bytes を取得・install・Build しない。候補が digest 検証済みか否かを候補と record に残す。供給元が「不正な hash・version 不整合・権限拒否」を返した後は、同じ expected identity の候補以外を採用しない（別 revision へ黙って切り替えない。program §4.1）。
  - 崩しうる経路: (a) provider が `IsDigestVerified=true` を偽って申告する（契約では防げない。S4 の adapter の責任で、本スライスは record に申告のまま残す）。(b) 探索を打ち切らず、下位の供給元の別 identity を採る。(c) record を省略して成功を返す。(d) 本スライスの loader が、採用した候補と無関係に「入っている directory」から読む。`IAssetManagement` は directory の identity を公開しないので、本スライスはこの同一性を検証できない。S3 がこの接続を作るときに閉じる（§2.3）。
  - 探索した境界: `IContentDirectoryBackend.BuildIdentity` は internal。`IAssetManagement` と `ISceneHandle` / `IAssetHandle` は identity を返さない。未確認: なし。
- **I5 優先度・利用可能・互換の分離:** 供給元の順序（policy）、候補があること（provider の回答）、候補を採ってよいこと（規則）は別の 3 つの判定で、record はどの供給元に何を聞いて何と答えられたかを全て残す。
  - 崩しうる経路: 最初に候補を返した供給元を規則を通さずに採る。規則で落ちた候補を次の供給元へ進まずに失敗にする。
  - 探索した境界: 本スライスの `ContentResolver` の 1 経路。未確認: なし。
- **I7 汎用:** 本スライスの型・テストに SampleGame の Scene 名・Cell・Season・職種・RunPurpose・宣言名・VCS・SceneResourceMap・UICommon が現れない。SceneResourceMap / UICommon の起動時特例は host の policy として登録するもので、`ContentRequest` に入れない（program M-06）。
  - 崩しうる経路: テスト fixture に Spring 等の実名を使う。`ContentKind` や `ContentSourceKind` に host 固有の値を足す。
  - 探索した境界: 新規 file 全部（grep）。未確認: なし。
- **I8 単独利用の非退行:** AssetManagement と BuildSystem 本体は本スライスの型を参照しない。`IAssetManagement`、`IContentDirectoryBackend`、`AssetOwner`、`IAssetHandle` / `ISceneHandle`、`ContentDirectoryException` / `ContentDirectoryFailureCode` の宣言を変えない。既存テストの assertion・名前・本文を変えない。
  - 崩しうる経路: (a) 「由来を取るため」に `IAssetManagement` や `ISceneHandle` に member を足す。(b) `InternalsVisibleTo` や asmdef の参照を足す。(c) 既存の fake（`FakeAssetBackend`、各 `FakeDirectoryPort`）を本スライス向けに書き換える。
  - 探索した境界: `Runtime/AssetManagement/**`、`Runtime/BuildContent/**`、`Runtime/OneStarMaker.Runtime.asmdef`、`Tests/**/*.asmdef`。未確認: なし。
- **I10 既存の fail-closed を弱めない:** `Runtime/Bootstrap/**`、`Runtime/AssetManagement/**`、`Runtime/BuildContent/**`、`Runtime/SceneSystem/**`、`SampleGame/**`、`.asmdef`、`ProjectSettings/`、`Packages/` を変えない。
  - 崩しうる経路: 便乗の変更。Unity Editor が import 時に書き換える file を一緒に commit する。
  - 探索した境界: AC2 の変更集合の許可リストで塞ぐ。未確認: なし。

**成立条件（起きなければならないこと）**

- **S1 契約の存在と Unity 非依存:** §3 の core の型が `Runtime/ContentResolution/Core/` に存在し、UnityEngine・UniTask・OSM の他の部分を参照せず、.NET 8 の offline テスト project（`tools/ContentResolutionOfflineTests/`）が core の `.cs` だけを link してビルド・実行できる（exit 0、1 件以上実行、failed 0）。
- **S2 fake provider での契約の成立:** offline テストが §5 の T1〜T13 を全て通す（優先順位、利用可能・互換の分離、fail-closed、同 identity での修復、由来の無い成功の拒否、要求の不変、Object kind、cancel、汎用性の文字列検査）。
- **S3 SceneDirector 無しの AssetManagement との合成（G03 (b)）:** EditMode テストで、`AssetManagement`（`FakeAssetBackend`）に fake の `IContentDirectoryBackend` を install し、fake provider 1 つの policy で `ContentRequestLoader` が Scene と Object（`GameObject`）を要求し、handle と由来付きの record を得て、`IAssetManagement` の既存 API（`UnloadSceneAsync` / `ReleaseScene` / `Release`）で解放でき、`IAssetDiagnostics.GetOwners` の所有者が要求時の owner と一致する。SceneDirector・SceneResourceMap・SampleGame の型は使わない。
- **S4 resolver 無しの非退行（G03 (a)）:** 既存の `Tests/AssetManagement/*` と `Tests/Editor/Build/*` と `Tests/Scene/*` を変えずに全て通す。U-A・U-B の P 検査の集合は変えずに成功し、`RESOLVER-unity` 集合（`Runtime/ContentResolution/` + U-A のソース + 共通契約）が、BuildSystem 本体・SceneSystem・Bootstrap のソース無しでコンパイルできる。
- **S5 回帰:** 全 EditMode 回帰（空 filter）と `pwsh tools/contract-audit.ps1` が合格し、CI の `dotnet-offline-tests` matrix に新 project が入っている。

### 2.2 受け入れ条件

合否は各 AC の本文で決める。「確認手段」は合否を見る手段で、同じ意味を別の手段で示してもよい。

- **AC1（S1, I7）core の純粋さ:** `Runtime/ContentResolution/Core/*.cs` の各 file が `#nullable enable` で始まり、`using UnityEngine`、`using Cysharp`、`using OneStarMaker.Runtime.AssetManagement`、`using OneStarMaker.Runtime.BuildContent`、`using OneStarMaker.Runtime.SceneSystem` のどれも持たず、`record` 宣言が無い。`tools/ContentResolutionOfflineTests/ContentResolutionOfflineTests.csproj` が core の file だけを `<Compile Include>` で link し、`PackageReference` を持たず、`dotnet run -c Release --project tools/ContentResolutionOfflineTests/ContentResolutionOfflineTests.csproj` が exit 0 で、出力の末尾の件数行が `passed >= 13`、`failed 0` を示す。
  - 確認手段: file の先頭と `using` を読む。`dotnet run` の出力。
  - 不合格の例: core が `ValueTask` の代わりに `UniTask` を使い、csproj に UniTask の `PackageReference` を足して通す（A3 の Q2 で UniTask を選んだ場合は、この例は不合格でなくなり、AC1 の「`PackageReference` を持たず」を UniTask 1 件の許可に改める）。
- **AC2（I8, I10）変更集合の許可リスト:** base から head までの変更（全 path）が次の 6 種類だけである。これ以外の追加・変更・削除が 1 件でもあれば不合格。
  1. 新規 `unity/Assets/OneStarMaker/Scripts/Runtime/ContentResolution/**`（`.cs` と `.meta`、folder の `.meta` を含む）。
  2. 新規 `unity/Assets/OneStarMaker/Tests/ContentResolution/**`（`.cs` と `.meta`、folder の `.meta`）。
  3. 新規 `tools/ContentResolutionOfflineTests/`（csproj と `Program.cs`）。
  4. `.github/workflows/offline-checks.yml` の `dotnet-offline-tests` matrix への 1 行の追加。
  5. `docs/handoff/` 配下の HANDOFF の記録（本書と program §8 の S0-a 行のリンク）。
  6. harvest 先の公開文書（Phase D）。
  - 確認手段: `git diff -M --name-status <base> <head>` の全行を照合する。既存 `.cs`・`.asmdef`・`ProjectSettings/`・`Packages/`・`SampleGame/`・既存テストの変更が 0 件。
  - 不合格の例: `IAssetManagement.cs` に 1 行でも変更がある。`FakeAssetBackend.cs` を共有のために書き換える。Unity が書き換えた他の `.meta` を一緒に commit する。
- **AC3（I8）宣言の不変:** `IAssetManagement`、`IContentDirectoryBackend`、`AssetOwner`、`IAssetHandle` / `IAssetHandle<T>` / `ISceneHandle`、`ContentDirectoryException` / `ContentDirectoryFailureCode` の宣言（member の名前・signature・enum 値）が base と同一。`Runtime/AssetManagement/**` と `Runtime/BuildContent/**` のどの file にも `ContentResolution` の文字列が無い。
  - 確認手段: AC2 で変更 0 件なら成立。加えて `git grep -n "ContentResolution" -- unity/Assets/OneStarMaker/Scripts/Runtime/AssetManagement unity/Assets/OneStarMaker/Scripts/Runtime/BuildContent` が 0 件。
- **AC4（S4）P 検査:** head で、ASSET_CYCLE で凍結した `p-check.ps1` と同じ条件で、U-A・U-B の既存 7 集合が exit 0、陰性対照 2 つが exit ≠ 0（共通契約の欠落が理由）、加えて本書 §5 の集合 `RESOLVER-unity-player` / `RESOLVER-unity-editor` が exit 0、陰性対照 `NEG-RESOLVER-without-assetmanagement`（`Runtime/ContentResolution/` + 共通契約だけ）が exit ≠ 0 でその error が `IAssetManagement` / `AssetOwner` / `AssetManagement` namespace の欠落を示す。
  - 確認手段: §5「P 検査」の方式で判定 C が script を用意し、`summary.json` と各集合の `files.txt` / `args.rsp` / `csc.log` を evidence に残す。
  - 不合格の例: `RESOLVER-unity` を通すために集合へ `Runtime/SceneSystem/` や `Runtime/BuildContent/` を足す。陰性対照が別の理由（参照 DLL の欠落等）で失敗している。
- **AC5（S3）EditMode の合成テスト:** `Tests/ContentResolution/ContentRequestLoaderTests.cs` の E1〜E5（§5）が存在し、全て Passed。E1 は Scene を要求して `ISceneHandle.IsLoaded` が true、record の `SourceKind` が `ContentDirectory`、`ProviderId` が fake の id、`OwnerLabel` が `Scene(<identity>)`、`UnloadSceneAsync` → `ReleaseScene` の後に fake port の unload 回数が 1。E2 は `GameObject` を `AssetOwner.Manual` で要求して `GetOwners` に Manual が居り、`Release` の後に空。E4 は provider が NotFound のとき `ContentResolutionException`（`NoCandidate`）が投げられ、fake port の load 回数が 0（探す段とロードを分ける）。E5 は provider が Rejected のとき `IntegrityRejected` で load 回数 0。
  - 確認手段: 結果 XML の実行テスト名と結果。
- **AC6（S4, I8）既存テストの非退行:** base と GO 候補 head のそれぞれで、同じ機械・同じ Unity 版・同じコマンド（`pwsh tools/run-tests.ps1`、空 filter、EditMode）で全 EditMode を実行する。base の結果は 1 件以上実行かつ failed 0 であること（そうでなければ比較の基準にしない）。head が合格し、head の結果 XML の実行テストのケース ID（parameterized テストの引数を含む full name）の集合が base の集合を含み、base で passed のケースが head で skipped / failed になっていない。`pwsh tools/contract-audit.ps1` 合格（検査 9 の path 予算を含む。最長の新 path は §2.4）。
  - 確認手段: 2 つの XML から full name と結果を抜き出して比べる。総数・passed・skipped・failed を記録する。base が failed 0 にならない場合は比較の基準が成立しないので、head を判定せずに人間へ返す。
- **AC7（S5）CI:** `.github/workflows/offline-checks.yml` の `dotnet-offline-tests` の `matrix.project` に `ContentResolutionOfflineTests` がある。PR の CI でその job が緑。
  - 確認手段: yml の本文と PR の check。

### 2.3 ここでは答えない問いと所有スライス

| 問い | 所有 |
|---|---|
| Editor で開いている Scene の snapshot の機構 (i) / (ii)、既ロード Scene の取り込み方、I11、`Facts` に入れる `dirty` 等の key | S0-b `CONTENT_DEMAND_ROUTING_SPIKE` |
| `InstallContentDirectory` の 2 件目拒否（directory の基数）、複数 directory の合成 | S0-b の結論の直後に切る製品スライス（program U1 (2)）、S2 |
| `IContentCompatibilityRule` の製品実装（identity / digest / target / schema の汎用判定、host 登録の述語） | S1 `CONTENT_COMPATIBILITY_SELECTION` |
| logical key → package の索引、Stage I / II | S2 `CONTENT_PACKAGE_DISCOVERY` |
| provider route → install → Unity load → 所有の接続。採用した候補と実際にロードした directory の同一性の検証。SceneDirector 経由の経路（SceneSystem adapter）。`InstantiateContentAsync` 相当の経路と `Bind(go)` 所有の record。既存 index の表現 fallback の実効表現を record に写すこと | S3 `RUNTIME_PROVIDER_INTEGRATION` |
| installed cache / publish 済み / LAN の provider adapter、probe の timeout、取得・install の進捗、prefetch、checkout hook | S4 `LOCAL_LAN_PROVIDER` |
| Play 入口、通知の 3 段、観測 UI、menu の整理 | S0-c `EDITOR_PLAY_ENTRY_PROOF`、S5 |
| `Runtime/ContentResolution/` を別 asmdef にするか、P 検査の常設化 | asmdef 分割スライス、[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md) D4 |

### 2.4 本文へ転記した実装制約（`AGENTS.md` の常時契約から）

- 依存は Game → Framework の一方向。asmdef 参照の追加は設計判断なので行わない（本スライスは asmdef に触れない。新 folder は `OneStarMaker.Runtime.asmdef` の配下）。
- 新規の Unity 側 `.cs` は先頭に `#nullable enable`。`record` を使わない（core も Unity の asmdef でコンパイルされるので同じ）。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。offline テストの非同期は完了済みの `ValueTask` と `CancellationToken` だけで組む。
- 破棄されうる `UnityEngine.Object` は `== null` / `!= null` で判定する（EditMode テストの `GameObject`）。
- 参照 0 を削除理由にしない（`IContentCompatibilityRule` と `ContentTraceability.Reproducible` は意図的な先行宣言で、製品実装は S1 / 後続）。
- path は `tools/path-budget.json` の予算（150 文字）に従う。最長の新 path は `unity/Assets/OneStarMaker/Scripts/Runtime/ContentResolution/Core/ContentResolutionFailureCode.cs.meta`（101 文字）。
- Unity Editor でのコンパイル確認は Phase B が行ってよい。Unity テストの実行・判定は Phase C。offline テスト（`dotnet run`）は Unity でも Build でもないので Phase B が B exit で実行してよい。
- 本スライスは `AbstractApplicationInitializer` に触れないので、`RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` との Phase B の同時実行制約の対象外。

### 2.5 未決事項

- U-a1 core の非同期型: `System.Threading.Tasks.ValueTask<T>`（初稿の案。UnityEngine・UniTask を参照しない core を .NET 8 で素のままビルドできる）か、UniTask（repo の Runtime の慣習。offline project に UniTask の NuGet 参照が要り、UniTask の .NET 実行を前提にする）か。A3 で owner が決める（Q2）。
- U-a2 P 検査の方式: 初稿は「判定 C の offline 実行」（§5）。A1 の主担当が script を書いて A2 で読ませる「A3 凍結 script」方式は、主担当の環境に Unity・pwsh が無く dry run できないので採らなかった。owner がローカルで script の改変と dry run を A3 前に行えるなら「A3 凍結 script」へ変えてよい（Q4）。

### 2.6 スライスの分割の検討

分割しない。変更理由は「要求時解決の契約を定義し、fake と既存 AssetManagement の上で成立を示す」1 つで、core（pure）と loader（Unity 側）は依存が違うが同じ問いの両半分である（core だけでは「既存の AssetManagement と同じ契約で所有・解放できる」を示せず、loader だけでは契約の汎用性を示せない）。未知は P 検査の集合追加と offline project の CI 追加の 2 つで、どちらも既存の経路（ASSET_CYCLE の P 検査、4 つの offline project）の繰り返しで独立に失敗しうる未知ではない。受け入れ条件に別の問いの判定は混じっていない（S0-b / S1 / S3 の問いは §2.3 で送った）。

## 3. 責務マップ

path は `unity/Assets/OneStarMaker/Scripts/` からの相対。型名は A1 の仮称で、A3 で凍結する。行数は予想。

| ファイル | 責務（一文） | 変更理由 | 所有者・寿命 | 許可する依存 | 公開面 | 単体テスト | 予想行数 |
|---|---|---|---|---|---|---|---:|
| `Runtime/ContentResolution/Core/ContentKind.cs`（新規） | 要求の種別 `Scene` / `Object` | 種別を足すとき | 値型 | なし | public enum | offline | 15 |
| `Core/ContentSourceKind.cs`（新規） | 供給元の種別 `None = 0`（記録に使えない）、`EditorSnapshot`、`ContentDirectory`、`Addressables` | 供給元の種別を足すとき（S0-b / S3） | 値型 | なし | public enum | offline | 20 |
| `Core/ContentIdentity.cs`（新規） | 候補の identity（`ContentSet`、`Revision`、`Target`、`Digest`。空を許す）。ordinal の等値だけを持ち、互換の判定は持たない | identity の欄が増えるとき | 値型（readonly struct） | なし | public | offline | 60 |
| `Core/ContentRequest.cs`（新規） | 呼び出し側の要求: `LogicalKey`（必須）、`Kind`、`Representation`（要求元の data。空は「指定なし」）、`ExpectedIdentity`（任意）、`CorrelationId`（任意）。SceneDirector の状態・親子・Season・SceneResourceMap・VCS を持たない | 要求の欄が増えるとき | 呼び出し側が作る不変の値 | なし | public sealed class | offline（T8） | 60 |
| `Core/ContentCandidate.cs`（新規） | provider が返す候補: `ProviderId`、`SourceKind`、`LogicalKey`、`Kind`、`Representation`、`Identity`、`ManifestReference`（opaque。path を信用しない）、`IsDigestVerified`、`Facts`（provider 固有の事実の小さな辞書） | 候補の欄が増えるとき | provider が作る不変の値 | なし | public sealed class | offline | 70 |
| `Core/ContentAvailability.cs`（新規） | 1 回の probe の回答: `Available`（候補あり）/ `NotFound` / `Unavailable`（未接続・timeout。次へ進む）/ `Rejected`（hash 不正・version 不整合・権限拒否。`ExpectedIdentity` を伴い、以後は同 identity 以外を採らない）と `Reason` | 回答の種別が増えるとき | 値型 | なし | public readonly struct + enum | offline | 70 |
| `Core/IContentProvider.cs`（新規） | 供給元の差込口: `Id`、`SourceKind`、`FindAsync(ContentRequest, CancellationToken) → ValueTask<ContentAvailability>`。探すだけで、取得・install・Build はしない | 探索の契約が変わるとき | adapter（S4 / S0-b）が実装。寿命は policy の持ち主 | なし | public interface | offline（fake） | 25 |
| `Core/IContentCompatibilityRule.cs`（新規） | 候補を採ってよいかの述語: `Accepts(ContentRequest, ContentCandidate, out string reason)`。製品実装は S1 | 互換判定の契約が変わるとき | policy の持ち主 | なし | public interface | offline（fake） | 20 |
| `Core/ResolutionPolicy.cs`（新規） | Play 前に固定する規則: 順序付きの `Providers`（Id 重複なし、1 件以上）と `Rule`。immutable。将来の要求を列挙しない | 規則の欄が増えるとき | host が作る不変の値 | `IContentProvider`、`IContentCompatibilityRule` | public sealed class | offline（T9） | 50 |
| `Core/ProviderProbe.cs`（新規） | 探索 1 回の実績: `ProviderId`、`Status`、`Reason`、`Accepted` | 実績の欄が増えるとき | 値型 | `ContentAvailability` | public readonly struct | offline | 30 |
| `Core/ContentResolutionFailureCode.cs`（新規） | 解決失敗の種別: `NoCandidate`、`NoCompatibleCandidate`、`IntegrityRejected`、`ProviderFault`（provider が要求と違う logical key を返した等） | 種別が増えるとき | 値型 | なし | public enum | offline | 15 |
| `Core/ContentResolution.cs`（新規） | 1 要求の決定: `IsResolved`、`Candidate`、`FailureCode`、`Probes`（全 provider の実績）、`PinnedIdentity`。policy（予定）と別の型 | 決定の欄が増えるとき | resolver が作る不変の値 | 上の型 | public sealed class | offline | 60 |
| `Core/ContentResolver.cs`（新規） | 純粋な orchestration: policy の順に probe し、I1（logical key 一致）・§4.1（Rejected 後は同 identity だけ）・I5（規則）を適用して `ContentResolution` を返す。I/O・時間・Unity を持たない | 探索の規則が変わるとき | 状態なし（static でもよい。初稿は instance） | 上の型 | public sealed class | offline（T1〜T7、T10〜T12） | 110 |
| `Core/ContentResolutionException.cs`（新規） | 解決できなかったことを呼び出し側へ返す例外。`Resolution` を伴う | — | — | `ContentResolution` | public sealed class : InvalidOperationException | offline | 25 |
| `Core/ContentTraceability.cs`（新規） | record の到達度 `Traceable` / `Reproducible`（本 program は `Traceable` までしか書かない） | — | 値型 | なし | public enum | offline | 15 |
| `Core/ContentLoadRecord.cs`（新規） | 1 要求の実績: 要求、採用候補（由来）、`Probes`、`OwnerLabel`、`Traceability`。`SourceKind == None` または `ProviderId` が空なら作れない（由来の無い成功は無い） | 実績の欄が増えるとき | loader が作る不変の値 | 上の型 | public sealed class | offline（T7） | 60 |
| `Runtime/ContentResolution/ContentRequestLoader.cs`（新規） | 決定を既存の `IAssetManagement` に接続する: `LoadSceneAsync(request, options, ct)` → `LoadContentSceneAsync(key, candidate.Representation, …)`、`LoadAssetAsync<T>(request, owner, ct)` → `LoadContentAssetAsync<T>(…)`。handle と record を返す。解放の API は足さず、呼び出し側は `IAssetManagement` の既存 API で解放する | AssetManagement への接続の仕方が変わるとき（S3 で置き換わりうる） | 呼び出し側が所有。`IAssetManagement` の寿命に従う | `IAssetManagement` と Abstractions（public のみ）、Core、UniTask | public sealed class | EditMode（E1〜E5） | 120 |
| `Runtime/ContentResolution/ContentLoadOutcomes.cs`（新規） | `SceneLoadOutcome`（`ISceneHandle` + record）、`AssetLoadOutcome<T>`（`IAssetHandle<T>` + record） | — | 値 | AssetManagement の Abstractions | public sealed class × 2 | EditMode | 40 |
| `unity/Assets/OneStarMaker/Tests/ContentResolution/ContentRequestLoaderTests.cs`（新規） | E1〜E5。fake `IContentDirectoryBackend` は既存 3 箇所の private fake と同じ形を file 内に持つ（共有のために既存 fake を動かさない） | — | — | `OneStarMaker.Tests` の既存参照 | — | — | 260 |
| `tools/ContentResolutionOfflineTests/ContentResolutionOfflineTests.csproj`、`Program.cs`（新規） | T1〜T13。core の `.cs` を link。`LangVersion` 9（Unity のコンパイラに合わせ、新しい構文の混入を .NET 側で捕まえる） | — | — | .NET 8 SDK だけ | — | — | 25 + 360 |
| `.github/workflows/offline-checks.yml` | `dotnet-offline-tests` の matrix に 1 行 | — | — | — | — | — | +1 |

変更しない file（責務の参照だけ）: `Runtime/AssetManagement/IAssetManagement.cs`（75 行、増分 0）、`AssetManagement.ContentDirectory.cs`（164 行、増分 0）、`Internal/IContentDirectoryBackend.cs`（36 行、増分 0）、`Runtime/BuildContent/**`（増分 0）。行数警報（500 行・3 責務・50% 増）に該当する file は無い。

配置・依存・所有・寿命・テスト境界を選んだ理由:

- **folder `Runtime/ContentResolution/`（`OneStarMaker.Runtime.asmdef` の配下、namespace `OneStarMaker.Runtime.ContentResolution`）:** フォルダ境界の基準（architecture-gates）の「依存方向」と「所有者」が既存 folder と違う。AssetManagement の公開面と共通契約だけに依存し、AssetManagement・BuildSystem 本体・SceneSystem・Bootstrap のどれからも参照されない任意の層で、[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md) §2.1 の「連携部分」に分類する。既存の連携部分（`Runtime/BuildContent/ContentDirectorySession.cs` 等）と同じ folder に置かないのは、(a) U-B の P 検査の集合から file 名で除外する一覧を増やさずに folder 単位で外せること、(b) 変更理由（要求・候補・規則の契約）が directory session（native handle と token の寿命）と独立なこと、の 2 つが違うため。asmdef は分けない（asmdef の追加は設計判断で、所有は asmdef 分割スライス）。
- **`Core/` sub-folder:** UnityEngine・UniTask を参照しない file だけを置き、offline の .NET project がこの folder の file だけを link する。「Unity 非依存」を folder の境界で見えるようにし、Unity 側の接続（loader）を同じ folder の外の 1 file に限る。
- **core の非同期型に `ValueTask<T>`（U-a1）:** 探索は本質的に非同期（LAN の probe、S4）だが、core は Unity に依存しないので UniTask を使わない。netstandard 2.1 の `ValueTask` は Unity の Runtime asmdef でコンパイルでき、UniTask 側の loader は `await` できる。代替（UniTask + offline project への NuGet 参照）は A3 で owner が選ぶ。
- **`IAssetManagement` に member を足さない:** 由来（どの directory か）を handle に持たせると `IAssetManagement` の変更になり I8 と program の停止規則（Runtime の新 API を S0-b 前に凍結しない）に当たる。record は loader が、採用した候補から作る。実ロード元との同一性の検証は S3（§2.3）。
- **`IContentCompatibilityRule` を差込口だけにする:** 互換の判定内容は S1 の問い。S0-a が「identity 等値」のような既定実装を置くと、S1 の判定の既定値を先に凍結してしまう。製品実装が無い間は policy を組めないが、S0-a は契約の成立だけを示す（§2 成果の範囲）。
- **失敗の表現:** 解決できないこと（候補なし、互換なし、整合性拒否）は `ContentResolution` の結果で返し、loader の公開入口だけが `ContentResolutionException` に変える（呼び出し側の `try` が 1 つで済む）。AssetManagement のロード失敗（`ContentDirectoryException` 等）は包まずにそのまま通す（reason code の所有を変えない。[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md) G5）。
- **解放の API を足さない:** 所有・解放は `AssetOwner` と `IAssetManagement` の既存契約（`Release` / `UnloadSceneAsync` → `ReleaseScene` / `ReleaseAll`）をそのまま使う。「同じ契約で扱える」は、新しい解放 API を作らないことで示す。
- **テスト境界:** core は .NET だけで決定的にテストする（offline）。loader は `AssetManagement` の実物 + fake の `IAssetBackend` / `IContentDirectoryBackend` で EditMode テストする（Unity の test runner は要るが、Editor Play・AssetDatabase・Addressables の実体には依存しない。既存の `AssetManagementTests` と同じ条件）。
- **既存 fake を動かさない:** 3 箇所の `FakeDirectoryPort` を共有 fixture へ昇格させると既存テスト file の変更になり AC2 に当たる。本スライスは file 内の private fake を 1 つ足す。共有化は後続の整理。

## 4. 実装計画

- 変更対象: §3 の表のとおり。
- 開始の前提: 本書の A3 凍結版が本ブランチにあり、Phase B は凍結版の commit（tree の外で渡す SHA）から始める。S0-b の結果を待たない（独立）。
- 順序: (1) `Core/` の型を §3 の順（enum → 値型 → interface → policy → resolver → record）で書く → (2) `tools/ContentResolutionOfflineTests/` を作り T1〜T13 を書いて `dotnet run` で通す（B exit の offline suite） → (3) `ContentRequestLoader` と outcomes を書く → (4) `Tests/ContentResolution/ContentRequestLoaderTests.cs` に E1〜E5 を書く → (5) Unity Editor を開いてコンパイル確認（エラー 0）。Unity が生成した folder の `.meta` を commit に含め、他の file の書き換えは含めない（AC2） → (6) `offline-checks.yml` に 1 行足す → (7) `pwsh tools/contract-audit.ps1` → (8) B の出口: commit、§0 の票（implementation head 欄は (b) の「この票を含む commit」。`docs/handoff/` 以外を変える commit なので可）。起点 filter の EditMode テストと P 検査は Phase C が実行する。
- Phase B から Phase A へ差し戻す条件: §3 に無い public 型・member が要る（特に `IAssetManagement` / `ISceneHandle` / `IAssetHandle` への追加）。core に UnityEngine / UniTask / BuildContent / SceneSystem の参照が要る。`ValueTask` を UniTask の loader から `await` できない。EditMode の E1〜E5 が `AssetManagement` の internal を本スライスの製品 code から呼ばないと成立しない。既存テストの assertion を変えないと通らない。
- 対象外を維持する方法: `Runtime/AssetManagement/**`、`Runtime/BuildContent/**`、`Runtime/SceneSystem/**`、`Runtime/Bootstrap/**`、`SampleGame/**`、`.asmdef` を開かない。AC2 で確認する。

## 5. テストとレビュー計画

- 単体テスト（新しい中核ロジック = `ContentResolver` と record の検証）: offline の T1〜T13。fake provider は `Available` / `NotFound` / `Unavailable` / `Rejected` を script 化して返し、probe の回数と順序を数える。fake rule は候補ごとの採否を表で返す。
  - T1 優先順位: A が `NotFound`、B が `Available` → B を採用。`Probes` は A、B の順で 2 件。
  - T2 I5（優先度 ≠ 互換）: A が `Available` だが rule が拒否、B が `Available` で rule が採用 → B を採用。`Probes` に A の「規則で不採用」が残る。
  - T3 fail-closed: A が `Rejected`（expected digest X）、B が `Available`（digest Y）→ 失敗 `IntegrityRejected`。B の候補は「identity 不一致で不採用」として `Probes` に残る。採用候補は無い。
  - T4 同 identity での修復: A が `Rejected`（expected X）、B が `Available`（digest X）で rule が採用 → B を採用。`PinnedIdentity` が X。
  - T5 全て `NotFound` → `NoCandidate`。T6 全て `Unavailable` → `NoCandidate`（`Probes` の Status で timeout と不在を区別できる）。
  - T7 由来の無い成功の拒否: `ContentLoadRecord` を `SourceKind.None` または空の `ProviderId` で作ると `ArgumentException`。
  - T8 要求の検証: 空の `LogicalKey` は `ArgumentException`。`Representation` の null は `ArgumentNullException`、空は許す。
  - T9 policy の検証: provider 0 件、Id の重複、rule の null は `ArgumentException`。
  - T10 I1: provider が要求と違う `LogicalKey` の候補を返す → その provider は `ProviderFault` として不採用、次へ進む。全てそれなら失敗 `ProviderFault`。
  - T11 Object kind: `Kind = Object` の要求が同じ規則で解決され、候補の `Kind` が要求と一致しなければ不採用。
  - T12 cancel: 取消済みの token で `OperationCanceledException`、probe の回数 0。
  - T13 汎用性: `Core/` の各 file の本文に `UnityEngine`、`Cysharp`、`SceneDirector`、`SampleGame`、`Spring`、`Season`、`Cell`、`RunPurpose`、`SceneResourceMap` が現れない（既存 offline テストの `Sources_DoNotName…` と同じ形）。
- EditMode（`OneStarMaker.Tests`、`Tests/ContentResolution/`）:
  - E1 Scene（G03 (b)）: `AssetManagement(FakeAssetBackend)` に fake `IContentDirectoryBackend` を `InstallContentDirectory`（internal。テストは既存と同じく internal を見られる）。policy = fake provider 1 件（`SourceKind.ContentDirectory`、`Available`）+ 採用する rule。`ContentRequestLoader.LoadSceneAsync(request("Entry", Scene, "Full"))` → `IsLoaded`、record の由来・`OwnerLabel == "Scene(Entry)"`。`UnloadSceneAsync("Entry")` → `ReleaseScene("Entry")` → fake port の unload 回数 1。`FakeAssetBackend.LoadSceneCallCount == 0`（Addressables 側を通らない）。
  - E2 Object: `LoadAssetAsync<GameObject>(request("Widget", Object, "Full"), AssetOwner.Manual)` → handle の `Value` が非 null、`GetOwners(key)` に `Manual`。`Release(handle)` → `GetOwners` が空、fake port の release 回数 1。
  - E3 record の由来と要求の対応: E1 の record の `Request` が渡した要求と同値、`Candidate.Representation == "Full"`、`Traceability == Traceable`、`Probes` が 1 件。
  - E4 探す段とロードの分離: provider が `NotFound` → `ContentResolutionException`（`NoCandidate`）。fake port の load 回数 0。
  - E5 fail-closed: provider が `Rejected` → `IntegrityRejected`。load 回数 0。
- 差し戻し中の起点 `-Filter`（C が根拠付きで変更可。受け入れ条件の追加ではない）: `OneStarMaker.Tests.ContentResolution`、`OneStarMaker.Tests.AssetManagement`。offline suite は毎回 `dotnet run`。
- 判定必須テスト（GO 候補 head）: 全 EditMode 回帰（空 filter）を base と head で同じ条件で実行（AC6）。XML で `OneStarMaker.Tests.ContentResolution` の E1〜E5 と `Tests/AssetManagement/*` の既存テストが収録されていること。offline suite（AC1）。P 検査（AC4）。`pwsh tools/contract-audit.ps1`。順序は、全 EditMode 回帰（Unity が head の worktree に `Library/ScriptAssemblies` を作る）→ P 検査（その DLL を参照する）。
- 全 EditMode 回帰の適用除外: なし
- 統合・Unity テスト: EditMode だけ。PlayMode・Build・Player は不要（起動・Scene lifecycle・Player に触れない）。
- 操作・実行時・目視条件の検証経路: 対象なし（全条件が offline テスト、EditMode テスト、`git diff`、P 検査で閉じる）。
- 未知の操作経路の疎通結果と、必要な検証支援・確認地点: 2 つ。(1) P 検査の `RESOLVER-unity` 集合の追加: 凍結済み `p-check.ps1` の `$Sets` に folder を足す改変で、ASSET_CYCLE で確かめた経路の繰り返し。主担当の環境では実行できないので **未確認。初回確認は Phase C**（判定 C の実行担当が script を用意して実行する。不成立なら、集合の定義を本書 §5 の表に照らして直し、script の改変だけで済まないなら Phase A の revision）。(2) offline project の CI 追加: 既存 4 project と同じ matrix 行で、経路は既存。主担当の環境に dotnet が無いので、B の出口で `dotnet run` を初めて実行する。
- 人間の判断が必要な条件: なし
- 高コスト証拠: なし

### P 検査

- **方式（U-a2）:** **判定 C の offline 実行。** 判定 C の担当が、ASSET_CYCLE の Phase A snapshot に凍結された `p-check.ps1`（ローカルの Artifact Storage から取得。content sha256 は同スライスの記録 `94291d8f…`）を元に、冒頭の `$Sets` へ下の表の 3 集合を足した script を書き、自分の環境で head の worktree に対して実行する。資格情報・本番設定・通信には触れない（ローカルのコンパイルだけ）。script と出力は判定 evidence bundle に置き、git に入れない。「A3 凍結 script」方式を採らない理由は §2.5 U-a2。
- **集合の規則（正本はこの表）:** 既存の 7 陽性集合と 2 陰性対照は ASSET_CYCLE の表のまま変えない。folder 単位で全 `.cs` を取り、除外は無い。参照は既存と同じ（netstandard 2.1、`UnityEngine*.dll`、Editor 集合では `UnityEditor*.dll` と `UNITY_EDITOR`、`Library/ScriptAssemblies` の `UniTask` / `Unity.Addressables` / `Unity.ResourceManager`）。

| 集合 | ソース（`unity/Assets/OneStarMaker/Scripts/` から） | 期待 |
|---|---|---|
| `RESOLVER-unity-player` / `RESOLVER-unity-editor` | `Runtime/ContentResolution/`（`Core/` を含む）、`Runtime/AssetDescriptions/`、`Runtime/AssetManagement/`、`Runtime/AssetBuildContracts/` | exit 0（resolver は U-A と共通契約だけで閉じる。SceneSystem・BuildContent・Bootstrap を要しない） |
| 陰性対照 `NEG-RESOLVER-without-assetmanagement` | `Runtime/ContentResolution/`、`Runtime/AssetBuildContracts/` | exit ≠ 0。error に `AssetManagement`（namespace）、`IAssetManagement`、`AssetOwner` のいずれかが引用符付きで現れる（loader が AssetManagement の公開面を本当に使っていることの対照。core だけなら閉じることは offline project が示す） |

- **合格:** 既存の 7 陽性集合と新 2 集合が exit 0、既存の陰性対照 2 つと新 1 つが exit ≠ 0 で理由が期待どおり。exit 2（前提の不備）を合格と読まない。
- **P 検査が示さないもの:** Unity の asmdef ごとのコンパイル（Phase B の Editor コンパイルと AC6 の回帰が担う）、実行時の挙動。

### 判定 C の照合 script の方式

- P 検査: 上のとおり「判定 C の offline 実行」。
- それ以外（EditMode の XML、offline suite の出力、`git diff`）: script は不要。XML の実行テスト名と結果を読み、offline suite は末尾の件数行と exit code を読む。機械向けの期待値 file は無い。

### レビュー計画

- 機械検査: `pwsh tools/contract-audit.ps1`、`pwsh tools/docs-audit.ps1`（PR 作成前と harvest 時）、P 検査、offline suite。
- A0/A1 主担当・モデル・ベンダー: Claude Code（Anthropic。Cloud の session）。
- A2 独立レビュー（normal: 2 観点）: (a) アーキテクチャゲート（folder・asmdef の位置、`Core/` と loader の境界、`IAssetManagement` に足さない判断、`IContentCompatibilityRule` の差込口、`ValueTask` の選択、I1〜I10 の経路の網羅、AC と P 検査の集合が S1〜S5 を本当に示すか、反例 (a)〜(g) の塞ぎ方。「計画対象は実装済みと仮定し、現コードに無いことを欠陥にしない」「owner 決定 D1〜D11 と program の問いの分解を問い直さない」で枠を固定する）、(b) 目的レビュー（§1 の目的・利用者の原文だけを渡す。問いは規則の 2 つ）。
- A3 統合担当・モデル・採否: owner + 主担当。凍結は owner の指示。
- C' 用に予約した担当・モデル・ベンダー: A1 / A2 は Anthropic 系列だけを使う予定なので、OpenAI・xAI・Google は未関与で C' に残る。B と C のモデルは Phase ごとに選ぶ。
- 独立性の強化条件を満たせない場合の理由: 該当なし（risk `normal`）。

### A2 独立レビュー

A2 入力版: 本書を含む commit（SHA は tree の外で渡す）と、本書の内容 sha256（commit 後に §5 のこの項へ追記する。A2 入力版の commit 自身には書けない）。Phase A snapshot は未生成（§0）。

（A2 の記録と A3 の採否は、A2 の実施後に「未 commit の版」として追記する。）

### A3 の確認事項（owner へ。凍結前に回答を得る）

- **Q1 risk:** `normal` のままでよいか。新しい公開面（任意の層の型）を足すが、既存の公開 API・asmdef・所有者・寿命は変えない。
- **Q2 core の非同期型:** `ValueTask<T>`（初稿）か UniTask（offline project に NuGet 参照）か。
- **Q3 offline project と CI:** `tools/ContentResolutionOfflineTests/` を判定必須に含め、CI の matrix に足してよいか。
- **Q4 P 検査の方式:** 「判定 C の offline 実行」でよいか。owner がローカルで script の改変と dry run を A3 前に行えるなら「A3 凍結 script」に変える。
- **Q5 `IContentCompatibilityRule` の製品実装を S0-a で作らない**（S1 に送る）でよいか。
- **Q6 目的・利用者の文言:** §1 の原文で確定するか。A2 の目的レビューの回答を見て判断する。

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
