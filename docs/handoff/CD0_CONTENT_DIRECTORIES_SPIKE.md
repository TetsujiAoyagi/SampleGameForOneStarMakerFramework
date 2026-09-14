# CD0 — Content Directories technical spike

## 0. メタデータ

- type: `slice`（disposable technical spike。BuildSystem の実装スライスではない）
- status: `A3 revision 1 frozen / B implemented / C static review and EditMode tests complete / native experiments pending`。
- branch: `codex/cd0-phase-a`。PR base は `develop`。develop/main へ直接コミットしない。
- implementation base commit: `0a11a4be58c7b75356b076f356078d8d001c2e5b`（2026-09-13 fetch 後の origin/develop）
- implementation head commit: `bfc7c677e1c470d58797fb38a0128336f6490e79`。
- risk: `high`（未知の native loading / serialization / 非同期寿命 / stripping を実証する。ただし本番変更はしない）
- owner: Phase A 主担当 Codex / GPT-6 Astra（OpenAI）。A3 の採否・Phase D は人間。B/C/C' は開始時に担当を記録する。
- created: 2026-09-13 JST
- expires: 2026-09-27 JST、または exact Editor/package、実験配置、寿命の前提が変わった時点で A0 を再確認。CD0 Phase D で harvest 後削除。
- harvest to: `unity/Assets/Docs/Architecture/13-resource-system.md`（検証した loading 制約）、`unity/Assets/Docs/Architecture/20-variant-checkout-workflow.md`（現行と spike の区別・検証した build 制約）。次の BS1/BS2/BS3/BS4 Phase A に必要な制約は各開始時の A0 へ転記する。将来構成を現行実装として公開しない。
- Phase A snapshot path / id: A1は `artifacts/cd0-phase-a/A1-snapshot.md`、実装入力は `artifacts/cd0-phase-a/A3-frozen.md`。
- Phase A snapshot generated at / SHA-256: `artifacts/cd0-phase-a/manifest.json`。
- Phase B result snapshot: `artifacts/cd0-phase-a/B-result.md`、SHA-256 `DEA6E3A2E620F05058E11F2FFE3C7FFDCBE670D0A1EF4FF1CF81797AC9F1D236`。
- evidence bundle: `artifacts/cd0-phase-c/`（hashはmanifestに記録）。
- C' blind bundle: `artifacts/cd0-phase-c/blind-audit-bundle.md`（hashはmanifestに記録）。

## 1. 目的・対象外・A0 planning packet

### 目的

採用済み Unity `6000.6.0f1 (f7f8ed4d1e24)` の Content Directories direct API が、OSM の次の設計に使えるかを、最小の生成・build・load・失敗・解放で判断する。成功例の紹介ではなく、境界、所有者、寿命、依存、失敗時の復旧、制作と検証の実用性を証拠にする。実験で否定された仮説も有効な成果とする。

### 対象外

BuildTag、BuildRequest、BuildPlan、BuildSystem 本体、production backend、SceneResource payload、Player bootstrap、firstSceneIdentify 読者の設計・実装はしない。既存 Addressables、BuildVariantProfile、VariantWhitelistBuilder、VariantPlayerBuild、group、profile、app-config、package manifest/lock を変更しない。Spring や661 Sceneへの展開、世界生成、全asset再保存、既存buildの修理、production配信/cache、低レベル ContentLoadInterface の採用も対象外。

### A0 の入力と今回の調査範囲

- AGENTS.md、osm-workflow、phases-and-handoff、docs-policy、architecture-gates、handoff-template、review-evidence、docs/README.md、osm-unity-editor を全文確認。OSM の目標文書・依存図、現行 IAssetManagement と VariantPlayerBuild、Addressables package の Player callback を照合した。
- ユーザーが復元したローカル Pre-Phase A v3 を全文確認した。入力 SHA-256 は `AE1FD9A136A81001B6D5E01F4C6D224E3DDA71D18605B53CB93A8B36DD6277D2`。実装に必要な要件は本書へ転記済みで、別 checkout に原本は不要。docs/reference のコード・設計はコピーしていない。
- program の順序は U66 → CD0 → BS1 → BS2 → BS3/BS4 → DIST → RET。後続の型名・選択政策・partition は提案に留まり、CD0 では凍結しない。
- exact Editor は ProjectVersion とローカル `D:/UnityEditor/6000.6.0f1/Editor/Unity.exe` の ProductVersion で一致。Unity を起動・接続せず、同梱 XML と DLL metadata を読み取った。API の存在確認と実行成功は区別する。
- 現 manifest/lock の Addressables は registry `2.11.2`、SBP は registry `3.0.3`、Pipeline は `0.4.0-exp.1`。PackageCache `com.unity.addressables@42676f2a154e/package.json` も2.11.2。cache内の ContentDirectory / Content Directories / ContentDirectoryGroupSchema 検索は一致0件。検索だけであらゆる実装の不存在は証明しないが、2.11.2 の schema 採用根拠はない。
- Pre-Phase の「built-in Addressables 2.11.2」は採用しない。Editor core API と registry package は別。3.x/4.x の資料を2.11.2の機能へ合成しない。direct API の検証に package update を混ぜない。
- 現 manifest SHA-256: `EA86C6447579465367A586F45850E83AC52BBF530D7FAC2ECB1D3E7CC9783A21`、lock: `2F1049D369293985B18BF61AF87EE5845696896492845E1BBA457C04ADF6C9F2`。過去 evidence のファイル hash を現 checkout の raw bytes の hash と混同しない。

### 追加入力 — 重いsource assetのSVN管理予定

2026-09-13のユーザー方針: Texture、MeshなどGit管理の利点が小さい重いassetは、一部をSVN管理にする予定。これは採用予定の制約であり、現在すでにSVNへ移行済みとは扱わない。対象path、Git/SVNの境界、checkout配置、revisionの固定方法は未決。

CD0ではSVN導入・asset移行・checkout自動化を行わず、最小fixtureは従来どおり自己完結させる。SVN依存なしの場合は証拠に明記する。Content Directoryの出力をSVN管理するという指示でもなく、source assetの取得とbuilt contentの配布は別の責務として扱う。

後続BuildSystemはGit commitだけで入力を再現できる前提を置かない。複合入力としてGit commit、必要なSVN repository識別子・対象path・実際のworking-copy revision集合・local変更有無・asset/metaのhashを記録できる必要がある。mixed revisionやexternalsを使う場合、単一の最新revision番号で取得状態を代用しない。認証情報は証拠へ含めない。

assetと`.meta`の対応・GUIDを同じ入力snapshotで再現できることを必要条件とし、両者の管理先をどこに置くかは後続Phase Aで決める。GitとSVNで同じpathを二重所有せず、必要な依存assetが未取得の状態と、意図的にbuildから除外した状態を区別する。取得漏れをGUID再生成や無言の除外で補わない。Scene/Materialから重いassetへ伸びる参照も、この取得境界をまたぐ可能性がある。

### U66 から引き継ぐ現況（再受け入れ審査はしない）

[PR #51](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/51) は2026-09-13 06:10:16 UTCに develop へマージ済み（merge `d966e6bcb6660f76cee88a586e3969c4f1025fbb`）。Editor操作契約の [PR #52](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/52) もマージ済み。別案 [PR #50](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/50) はOPENで、CD0の実装入力にしない。

U66 の固定実装 `3244f3635c6e18fffc1bbc40d3126e6d1eee009a` → `4263a33ee0d5e75b67e82e6260ff556f12fd1dce` を確認。Editor/package、生成settings、TMP互換修理で、Scene/Prefab/asmdef変更なし。テストXMLとlogのhashは既存evidenceと一致した（679 passed / 0 failed / 0 skipped）。基本Play/Workspaceの証拠あり。既存whitelistに一致payloadがない8 IDのためpacked/Playerが停止し、Player/Season compatibility等は未検証のまま。C/C'の移行PASS不可という記録と、baseline採用のマージは別の事実として保持する。

根拠は `artifacts/u66-phase-c/evidence.md`、`live-editor-build-log.md`、`manifest.json`、`artifacts/u66-phase-c-prime/result.md` と PR #51。U66 HANDOFFのD pendingと旧版の公開記述が残っており、harvestは文書上未処理。**2026-09-13のユーザー指示に従い、U66受け入れの再審査・既存gate再実行・harvest完了をCD0開始条件に追加しない。** 既知事項を転記して引き継ぐだけとする。

CD0に直接影響するのは、通常の BuildPipeline.BuildPlayer でも AddressablesPlayerBuildProcessor.PrepareForBuild が走る点。独自Scene配列を指定しただけでは既存whitelist停止を隔離できない。§4の独立Player実験案で分離し、production設定を一時的にも無効化しない。

### 公式資料と exact 現物の照合

確認日2026-09-13。§10の版固定公式資料と、Editor同梱 `Data/Managed/UnityEditor.xml`、`UnityEngine.xml`、`Data/Managed/UnityEngine/UnityEngine.ContentLoadModule.dll` / `UnityEditor.CoreModule.dll` を照合。DLLはUnity同梱Cecilでmetadataのみ読んだ。

- `BuildPipeline.BuildContentDirectory(BuildContentDirectoryParameters)` → `BuildReport` が現物に存在。パラメータに outputPath / rootAssetPaths / options / compression / extraScriptingDefines / name。targetフィールドを捏造せず、active target/subtargetを前提として採取する。
- `LoadableSceneIdEditorUtility.CreateLoadableSceneId(string)` と GUID overload、`LoadableSceneIdToGuid` / `LoadableSceneIdToScene` が存在。文字列overloadを第一候補にし、private serialized fieldやYAMLを生成しない。
- `LoadableObjectIdEditorUtility.CreateLoadableObjectId(Object)` は永続assetを入力にする。未保存objectやScene内objectをasset ID生成の成功例にしない。
- `ContentLoadManager.RegisterContentDirectory(string)` → `ContentDirectoryHandle`、`GetRootAssets<T>(handle)` → `T[]`、`UnregisterContentDirectory(handle)` → void が存在。rootはLoadableラッパーではなくObject配列として返る。rootに独自Release/Destroyを要求する契約はまだ作らない。
- `Loadable<T>.LoadAsync()` の現物戻り値は `Awaitable<T>`、引数なし。`Load` / `Release` / `Status` / `Target` が存在。CancellationToken overloadはこの型の公開method一覧にない。取消はnative abortと要求の放棄を分けて実証する。
- `SceneManager.LoadSceneAsync(LoadableSceneId, LoadSceneParameters)` は同梱XMLに存在し、AsyncOperationを返す。unloadは実際に得たScene handleに対して行う計画。取消・activation制御・失敗の通知方法は実験で確定する。
- `BuildPlayerOptions.previousBuildReportDirectories` は string[] propertyとして現物に存在。公式はContent buildの出力またはreport directoryをUnityLinker入力へ渡す用途とする。reportを生成しただけ、またはEditor Play成功だけではPlayer strippingを証明しない。
- 公式はlocal-only。load前に登録、全asset解放・Scene unload後に登録解除する手順を示し、fileが開いたままの解除はerrorを記す。これを試験の期待値に使うが、失敗後のhandle有効性・回復・root寿命までは推測しない。
- runtime DLL SHA-256: `44A5824604ABC09D9D056FB105C84BABF066AD861E4E2B37B7911C62345C330B`。Editor Core DLL: `24DB9A2E2259D94964D54E33BE865B1F6DECF18685AC10B03D7FFA520B0758EE`。

## 2. 受け入れ条件と実装制約

### CD0 実行開始条件

U66 baselineの採用は済みとして扱う。必要なのはCD0 A2/A3で実験配置・限定例外・担当を決めて凍結すること、exact Editorと入力差分が合うこと、C担当とPlayer実験環境が確保できること。未凍結の本初稿でBを開始しない。U66の残件修理を前提にしない。

### 受け入れ条件

- AC1: E0〜E8の問いごとに実測結果と証拠があり、成功/制約付き/失敗/未実施を分ける。未実施を成功扱いしない。
- AC2: 保存・再import後のroot/Scene reference、生成物inventory、local登録、root取得、additive load/unloadを最小fixtureで再現できる。
- AC3: 失敗・取消・再試行の各終端で、取得した資源と残留資源を説明できる。native取消不能でも、安全な完了待ちと後始末を実証できれば制約付き候補とする。
- AC4: 別pathで元出力へアクセスせずloadできる成立条件と、再build差分/hashの意味が記録される。
- AC5: standalone PlayerでContent-only型とreportの関係を測る。Editor-only結果、Monoだけの結果、独立hostだけの結果をOSM Player/AOT互換へ拡大しない。
- AC6: 本番pathの差分0、一時資産・登録・build出力の所有者とcleanupが追跡可能。実験コードをFramework公開APIへ昇格させない。
- AC7: §9の判断と次のPhase Aへ渡す制約を記録。HTTPはlocal core成立後の別実験であり、localの判定を待たせない。

### 本文へ転記する常時契約

Game → Frameworkの一方向、全体配線DependOnAll、公開資産操作IAssetManagementとAssetOwner(App/Manual/Scene(id)/Bind(go))を維持。SceneState14値の順序を変えず状態変更はSceneLifecycleManagerだけが所有する。公開ログはILogger<T>。UpdateはUpdateSystemRuntimeへ登録し、ActivatePendingRegistrations → RunUpdate → RunLateUpdate → ApplyMainThreadChanges → ApplyStructuralChangesを維持、単一system例外で他Tickを止めない。

Unity C#の先頭は `#nullable enable`、record禁止。破棄されうるUnityEngine.Objectに `?.` / `??` / `is null` / ReferenceEqualsを使わず `== null` / `!= null`。テストのTask.Delay/Thread.Sleepは禁止。参照0だけを削除理由にしない。Editor依存はEditor専用asmdefへ置く。新しいasmdef edgeはA3で明示承認したものだけ。

**CD0限定の提案:** direct API実証はproduction IAssetManagementへ配線せず、独立fixture内だけで行う。この明示要求を本番の資産契約変更に一般化しない。実験runnerの資源所有者は1回のrunであり、本番App/Scene ownerを偽装しない。A3でこの隔離境界と追加assemblyを凍結する。

Phase BはUnity.exeを起動しない。run-tests.ps1、Addressables buildに加え、CD0のcontent/Player buildと実験実行もCへ渡す。人間が既に開いた本repo Editorに限り `tools/unity-editor.cmd status` → ready/project/version確認 → `command` discovery → 公開名のcommandを優先し、不足時だけUTF-8 Base64のevalを使う。ラッパー1 invocationを単独で実行する。MCPは前提にしない。接続不能時はpipeline list診断まで、起動やYAML編集で迂回しない。CloudはUnity CLIを呼ばない。接続可能なEditorのScene/Prefab/assetをYAML手編集しない。

## 3. 責務マップと変更候補

このターンの変更は本書、Phase A snapshot/manifest、docs/README.mdの一覧だけ。以下はA3で凍結するBの候補で、今は作成しない。

実験用配置は `unity/Assets/CD0Spike/`。FrameworkでもSampleGameのgameplayでもない、削除可能な検証fixtureという境界で分ける。namespaceは `CD0Spike`。既存assemblyから参照させない。Runtime assemblyはengine以外を参照せず、autoReferenced=false。Editor assemblyはEditor限定でfixture Runtimeのみ参照。test assemblyはEditor限定でfixture RuntimeとTest Frameworkのみ。既存asmdefには変更0。以下の予想行数は上限契約ではなく構造警報とする。

- `Runtime/Cd0Root.cs`: rootのserialized実験入力だけを定義。Scene ID1件と最小Loadable asset1件。資産の保存ownerはfixture generator、runtime参照はrun。UnityEngine/Unity.Loadingにのみ依存。SOのserialization上必要な型公開に留め、OSM APIにしない。現在0、増分30〜60行。保存再読込をE1で検証。
- `Runtime/Cd0ProbeAsset.cs` / `Cd0SceneMarker.cs`: 同じ小さなデータassetをrootのLoadableとpayload Sceneから参照し、依存追跡とcontent-only型の存在を観測する。表示やloading政策を持たない。各0→20〜40行。SceneMarkerはrunへの直接参照なし、完了を独立したlogで残す。E7のPlayer bootstrapから型を直接参照しない。
- `Runtime/Cd0RunLedger.cs`: request世代、取得済み資源、終端通知、cleanupを開始できる条件のpure policy。Unity Object/APIを保持しない。ownerはrun、寿命は開始〜cleanup完了。入力はrunnerの観測event、出力は次の許可操作。0→80〜140行。偽backend/event列でrace、重複cleanup、失敗を単体検証する。engine依存型が必要ならAへ返す。
- `Runtime/Cd0ProbeRunner.cs`: direct APIへの限定adapterと実験順序。directory/root/Loadable/Scene/進行中operationの参照を単一runが保持し、JSONLへ観測を出す。root発見はhandle指定で限定。SceneStateやOSM Serviceを作らず、Update pollingを追加しない。0→180〜260行。native挙動はC、policyはledgerで検証。表示UIやbuildを混ぜない。
- `Runtime/Cd0ContentSession.cs`: ContentDirectory handle、borrowed root、Loadable、実Scene、進行中native operationの唯一のowner。register/load/unload/release/unregister順序を実行し、証拠形式やscenario選択を知らない。run寿命。0→120〜200行。
- `Runtime/ICd0ProbeEventSink.cs` / `Editor/Cd0JsonEventSink.cs`: append-only event境界とJSONL実装。session/orchestratorのcleanupをfile formattingから分離する。0→各30〜100行。
- `Editor/Cd0FixtureAuthoring.cs`: AssetDatabase/Scene保存のI/O。固定2 Sceneとroot/dependencyを生成・検証・明示cleanupする。runtime asmdefにEditor APIを出さない。0→100〜160行。GUID/path inventoryと再生成時の既存asset再利用をE1で検証。
- `Editor/Cd0BuildExperiment.cs`: direct Content buildとreport保存だけ。root path/出力path/targetを検証して呼び出し、結果を返す。失敗時に成功stampを残さない。0→80〜140行。実行責任はC。Player build、HTTP、選択政策は持たない。
- `Editor/Cd0PlayerExperiment.cs`: 独立host内でbootstrapだけのPlayer buildとpreviousBuildReportDirectoriesの比較条件を固定。出力/run-idを所有。0→80〜140行。Cだけが実行、既存VariantPlayerBuildは呼ばない。
- `Editor/Cd0ArtifactInventory.cs`: CD0専用root配下だけのcanonical path検証、relative inventory、role分類、SHA-256、snapshot/diffを担当。削除は行わない。0→100〜180行。
- `Editor/Cd0FixtureCleanup.cs`: authoringが出力したexact inventoryを入力に、CD0専用root包含と既知GUIDを検証して削除する。生成・build・hashを担当しない。Phase Dの人間判断後にのみ実行。0→60〜100行。
- `Tests/Editor/Cd0RunLedgerTests.cs`: event順序に対する終端・後始末の回帰。0→100〜180行。実装をなぞるgetterテストではなくcancel/complete競合、失敗後再試行、二重解放の防止を確認。
- fixture Sceneは `Bootstrap.unity` と `Payload.unity` の**計2枚**、root1個、ProbeAsset1個。bootstrapはrootへのserialized直参照を持たない。画像・URP・音・実ゲームを持ち込まない。Scene数の追加が必要ならAへ返す。
- `artifacts/cd0/` はCのsource hash/manifest/log/report/inventoryの証拠保存先。本番runtimeには依存させない。raw出力の保管範囲とreview bundleを分け、Player binary/Library一式をgitへ入れない。

新規のため増加率50%以上の警報は全ファイルに該当する。上記はpolicy / native orchestration / Editor authoring / content build / Player buildで変更理由とテスト境界を分けた。500行または独立3責務を超える見込みなら、便宜的Helperに逃がさず責務マップをAへ返す。

## 4. 実験計画・所有権・復旧

### 実行環境と順序

E0 → E1 → E2 → E3 → E4 → E5 → E6/E8 → E7 → 判断、HTTPは別のE9とする。E6とE8の順序は独立だが同一runの出力を上書きしない。

本repoで生成・Content build・local経路を試す。Playerのstripping実験だけは **Cが用意する独立した一時Unity project** をA1第一候補とする。場所は `artifacts/cd0/player-host/`。同じexact Editorを使い、同じfixture source/assets/metaを限定コピーしてhashを照合する。Library/Temp、OSM、SampleGame、Addressables設定・callback・packageをコピーしない。engine-onlyのfixtureとしてcore APIを検証し、manifest/lockとeffective backend/stripping/targetを独立入力として保存する。これは本repoのpackage統合Playerを証明しない。その制約をBS4へ渡す。

本repoでは新規project作成・manifest変更を行わない。独立hostの生成・必要module・core-only構成の詳細はA3で固定する。Editorを開くのは人間。`tools/unity-editor.cmd` は本repoのunityへ固定されているため**別projectへ使わず、ラッパーを変更しない**。独立hostはC担当の人間が用意したメニューから実行しログ/reportを採取する。Bに別Editorの起動やbuildをさせない。この限定された手動実験が用意できない場合はE7をblockedとし、Editor成功だけでGOにしない。

### 共通の証拠

sourceがGit/SVN混在の場合は、上記の複合入力識別と未取得依存の検査結果を追加する。CD0の最小fixtureにSVNを使わない場合は `SVN inputs: none` と記録し、Git-only成功から将来のSVN取得経路まで検証済みとはしない。

case manifestはcase-id/run-id/generation/precondition/expected terminal/allowed diagnostics/required cleanup/resultを共通schemaで持つ。出力inventoryはruntime-required/report/log/unknownに分類し、unknownを成功判定前に解消する。

各実験に run-id、UTC時刻、source base/head、A3 hash、Editor revision、manifest/lock hash、host種別、target/subtarget、backend/stripping/Managed Code Variant、入力GUID/hash、実行操作、期待値、観測、判定、raw log/report/inventoryの相対pathとSHA-256を残す。存在しない指標を0にしない。exception型/message、Console error、native operation完了、登録一覧、loaded Scene一覧、ledger件数を成功時も失敗時も残す。メモリは補助観測であり、GCやプロセス終了だけで解放を証明しない。

### E0 — 環境と隔離のpreflight

- 問い: exact APIとfixtureだけで検証を開始でき、本番への混入を防げるか。
- 手順: Cは版・package・公開signature・targetを記録し、本番pathとGit外journalの開始時inventoryを保存。新規asmdefの依存方向、fixtureの参照先を確認。過去のCD0登録/出力がないことを確かめる。既存dirty Sceneがあれば保存を人間へ返し、それを自動saveしない。
- 期待結果: 対象は固定2 Sceneとroot/dependencyのみ。API欠落やunexpected dependencyなら停止。過去U66テストを再実行して開始判定しない。
- 証拠: environment.json、API metadata、保護path/GUID/hash、開始時scene setup/登録一覧。

### E1 — rootとScene referenceの生成・保存

- 問い: private field操作なしで生成し、保存/reimport後にも同じassetを参照できるか。
- 手順: Bがfixture authoring codeを用意。接続済みEditorの許可操作で2 SceneとProbeAssetを保存し、保存済みPayload pathからCreateLoadableSceneId(string)を生成。保存済みProbeAssetからObject IDを作りLoadableへ設定してrootをCreateAsset/SaveAssets。Cはreimport・再読込しutilityの逆変換でGUID/SceneAsset一致を確認。2回目生成は同じassetを更新し、GUIDを再生成しない。未保存asset/存在しないScene pathを別の負例にする。
- 期待結果: 保存済みrootがbuild入力として再読込可能。serialized形状は観測するがprivate layoutを将来schemaへ固定しない。再生成のbytesが違えば差分と意味を説明する。
- 証拠: authoring操作、生成前後meta/GUID/serialized diff、round-trip結果、負例の戻り値/例外。負例後の不要asset0。

### E2 — direct Content buildと生成物

- 問い: root1件からContent Directoryをbuildでき、何が配布物/診断物か区別できるか。
- 手順: CがBuildContentDirectoryParametersに固定rootAssetPaths/outputPath/nameを渡す。Windows x64 active target/subtargetを確認、初回はcompression/default optionsの実値を記録。出力はrun-id別。BuildReport summary.result、steps/messages、取得可能なfiles/dependencies/hashとbuild report保存先を採取する。
- 期待結果: 成功時だけ後段へ渡す。manifestとcontent fileの実名/構成をinventoryする。拡張子やreport directory名を事前に捏造しない。選外Bootstrapがcontentへ引かれていないことも確認する。
- 証拠: 全出力の相対path/size/SHA-256、report本体とexport、root到達依存、出力種別。失敗なら部分出力をquarantineし登録しない。

### E3 — local登録とroot取得

- 問い: Editor asset参照に頼らずbuilt rootを取得し、登録所有者を限定できるか。
- 手順: Cがbuilt directoryの絶対local pathで登録。GetRootAssets<Cd0Root>(handle)を使い型/件数/内容を観測。rootのLoadable assetをLoadAsyncし値を読む。AssetDatabaseからrootを取得するshortcutは使わない。登録前後と正常終了後の一覧を採取。
- 期待結果: root1件と期待payload ID/データが得られる。root参照の解放責任は実測して記録し、Destroy(root)やroot.Releaseを推測で入れない。Loadableは実際に所有した分だけReleaseする。通常runでは解除後のborrowed root/Targetをdereferenceしない。必要なら犠牲用negative caseへ隔離する。
- 証拠: run event順、root識別、Target/Status、登録差分、cleanup結果。同じ登録を繰り返して前runが残らないこと。

### E4 — additive Scene load/unload

- 問い: LoadableSceneIdからpayloadを追加し、bootstrapを保ったまま解放できるか。
- 手順: 登録rootのIDとAdditive LoadSceneParametersでLoadSceneAsync。完了signalを待ち、実際のScene handleとmarker/依存を確認。Scene handleでUnloadSceneAsyncし完了後のScene一覧を確認。正常順は「進行中処理が終端 → payload Scene unload完了 → runが取得したLoadableをRelease → directory unregister」を試験する。
- 期待結果: bootstrapのみへ戻る。rootのborrowed参照は解除前に利用終了し、解除後は再利用しない。Scene unloadとasset Releaseが同義とは扱わず、それぞれ終端を採取する。3周期でrun-owned資源が累積しない。
- 証拠: load/unloadの開始・終端、Scene handle/ID、依存の状態、登録一覧とledger、Console。UnloadUnusedAssetsを毎回挟んで残留を隠さない。

### E5 — 失敗・取消・再試行・解除

- 問い: native APIの制約内で、取り消された要求が後から資源を復活させず、安全に再試行できるか。
- 手順: ケースごとに独立runを作る。(a)存在しないdirectory、(b)manifest欠損/壊れたコピー、(c)content file欠損コピー、(d)不正Scene ID、(e)開始前取消、(f)asset/Scene load発行後かつ完了前の要求取消、(g)完了と取消の競合、(h)資源保持中unregister、(i)解放済みhandleでの重複操作を測る。破損注入は未登録のコピーだけ。
- 取消の期待値: native abortを仮定しない。asset/Sceneを別caseとし、ledger eventはoperationKind + generation + issued/completed/accepted/cleanupDoneを持つ。発行前は呼ばず、発行後は「要求を受理しない」状態を記録し、単一ownerがoperationの終端を受け取って後始末する。同じAwaitableを複数awaitしない。中途完了を再現できなければnative取消はinconclusiveでありGO条件にしない。時間sleepでraceを運任せにしない。
- 失敗の期待値: 例外・status・Consoleだけの通知を区別。資源保持中unregisterのerrorは期待されるが、その後の登録状態と再cleanup可能性は未確定。二重操作を実APIに強制する負例は独立runだけで行い、通常runnerは重複を防ぐ。
- 再試行: 失敗を記録し、未完了operationがなくcleanupが完了したことを確認してから、正常コピー＋新request世代で同じ論理入力を再試行。取消後に遅着するcallbackが新runを変更しないこと。残留資源が説明不能なら再登録を続けず停止する。
- 証拠: 各ケースの入力とevent trace、終端理由、exception/error、登録/Scene/assetの前後、復旧後成功。native crash/hangはlogを保全し、人間がEditorを復旧する。プロセス終了を正常cleanup成功と数えない。

### E6 — 別pathへの移設

- 問い: source output pathに依存せず、完成したdirectory一式を移して利用できるか。
- 手順: 全解除後、E2出力を別runの移設先へコピー。必要file候補を全て保持してbytesを照合。canonical pathが別でjunction/symlink/hardlinkではないこと、元出力が存在しないことを確認し、fresh Player processから移設先を登録してE3/E4を実行。空白・日本語pathは別case。Editor結果は補助で、Playerを合格証拠とする。
- 期待結果: 相対配置を保持した一式で成立、または絶対path依存などの制約が明らかになる。成功したOS/target/版だけを保証する。directory全体の移設と個別file renameは別で、後者を保証しない。
- 証拠: コピー元/先のfile manifest、hash一致、元path不在、初期登録0、Player/Editor log、cleanup。Editorのsource assetへのfallbackをPlayerで除外する。

### E7 — Player strippingとBuildReport

- 問い: Content-only型をPlayerが保持する条件は何か。reportのどのdirectoryを渡す必要があるか。
- 手順: Cの独立hostで同一fixtureを同exact EditorでContent buildし、そのreportを使う。bootstrapだけをPlayer scenesへ明示指定。bootstrapはSceneMarker/ProbeAsset型を直接参照せず、reflectionやPreserve/link.xmlでも保持しない。root契約型への参照は許す。Windows x64、Release相当、strippingの実効値を固定する。
- 比較: P0=previousBuildReportDirectoriesなし、P1=今回の**実測で確定したexact report directory**あり。content bytesは同一にし、登録/root取得までとcontent-only型の解決・marker実行を別段階で記録する。別出力で同条件build/runし、linker reportで対象型の保持/除去と最初の失敗段階を照合する。P0成功/失敗のどちらも単独では因果確定に使わない。output rootとreport directoryのどちらを渡すか曖昧ならP1a/P1bへ分ける。正負対照が成立しなければinconclusive。P2=存在しないreport directoryは別Player出力で診断を採取する。
- IL2CPPを主対象としてmoduleの有無をA3で確認する。利用不可ならMonoだけでAOT合格とせず、E7のAOT部分をblockedとしてCONDITIONAL/HOLDへ返す。module導入はこのターンもBも行わない。
- 期待結果: P1でContent-only型とSceneをload可能、使ったreportと型保持の因果が説明できる。生成物内のreportとruntime配布必須fileを区別する。古いreportで新規型が保持される保証をしない。
- 証拠: host入力/hash、Player BuildReport、content reportとpath、linker/stripping log、backend/options、P0/P1/P2結果、Player.log。OSM既存Player統合は未検証と明記してBS4へ渡す。

### E8 — 再build差分・依存・hash

- 問い: 何を変えると何が再生成されるか。内容同一とファイル同一を区別できるか。
- 手順: 同一outputでR0=clean、R1=無変更incremental、R2=Payload値変更、R3=ProbeAsset値変更、R4=root値変更、R5=入力bytes/GUIDをR0へ復元、R6=到達依存を外す、R7=同じ依存を戻す。各build前後にsourceと全生成物を別snapshotへ保存し、stale payload/tombstone/余剰fileとruntime loadを確認。最後に同一入力の別出力clean buildを比較。登録中のoutputを再buildしない。
- 期待結果: 変更入力と到達依存、reportの再処理範囲、manifest hash、file SHA-256/size、所要時間を関連付ける。「Scene周辺のfileだけ変わる」は仮説であり合格条件にしない。全fileが変われば事実と制約を残す。timestampsやreport logの差をcontent差と混同しない。
- 証拠: 各revisionの入力diff/GUID、report、相対path単位の追加/削除/変更/不変とhash、可能ならUnity側dependency識別。単一実行の時間差から性能保証しない。Unity hashをOSM配信プロトコルへそのまま採用しない。

### E9 — HTTP（別実験・今回のcore判定外）

E2〜E7のlocal成立後にのみAを追補する。完成した一式をHTTPで別local folderへ取得し、file hash照合後に登録する最小試験まで。UnityへURLを渡さない。中断・partial downloadを登録せず、失敗コピーを除去できるかを見る。本番cache/manifest protocol/CDN/atomic installの実装はDISTへ送る。HTTP未実施はlocal GOを妨げない。

### 一時資産のownerとcleanup

- source fixture / meta: Bが生成inventoryを作り、Cが内容を検証。Phase Dで人間の承認したinventoryに限りEditor経由で削除する。既存asset・ユーザーdirty Sceneを巻き込まない。
- native resource: run ownerだけが取得/後始末を行う。借用root参照、Loadable取得、Scene handle、進行中処理、directory登録を別々にledger化。未取得資源を解放しない。取消でもledgerを先に捨てない。
- content output / corrupt copies / relocation copies / Player host: C owner。run-idで隔離し、runtime登録中は書換え・移動・削除しない。raw evidenceを保存してからcleanupする。別pathへのmove/delete前はresolved絶対pathがCD0専用root内であることを確認する。
- evidence: Phase DまでCが保管。A/B snapshot、完全diff、raw log/XML/report、inventory/hashをbundleへ収録し、他checkoutでも取得可能にする。大容量binaryは保管先とhashをmanifestに残し、存在しない外部保存先を記録しない。
- Git外の既存Addressables snapshot、WorldWorkspace journal、user configは触らない。終了時に保護pathの開始時bytes/GUIDとの差が0であること、bootstrapのみ/登録baseline復帰を確認する。失敗時にgit reset/cleanやLibrary全削除で復旧したことにしない。

### 停止条件と復旧

版違い、API不一致、未承認asmdef edge/公開API/owner追加、本番assetへの参照拡大、既存Addressables設定を変える必要、third-party update、3枚目のScene、native残留の説明不能、Safe Mode、保護pathの意図しない差分で停止してAへ返す。

まず実行入力・log・diff・native終端/登録状態を保存し、判明している取得分だけ正常cleanupを試す。完了しなければquarantineして人間へ復旧を渡す。元資産を復元する際は開始時snapshotと現在差分を照合し、他の編集を上書きしない。新しい実装headが必要な修理はA追補→B→C/C'の新bundleにする。

## 5. テスト・レビュー計画とPhase A判断

- 今回はA0調査とA1初稿まで。Unity接続、資産生成、spike code、build、Unity testは未実行。
- B: 凍結範囲のcode/fixture生成のみ。`pwsh tools/contract-audit.ps1`、文書変更時の `pwsh tools/docs-audit.ps1`。Unity test/content build/Player build/実験は未実行とB resultへ明記。
- C: Bと異なるモデルの新規セッションで構造レビューを先に行う。ledgerのpure policyを偽eventで検証し、native統合はE0〜E8で測る。本repo Editorを閉じてから `pwsh tools/run-tests.ps1 -Filter CD0Spike`、回帰範囲は新assembly追加を踏まえて全EditModeを1回実行する。これはCD0差分の検証で、U66の再受け入れではない。0件は失敗。`unity test` / `unity run` はどのPhaseでも使わない。
- C実験: 本repoは人間が開いたEditor＋既存wrapper。独立Player hostは§4の人間操作。content/Player buildとその実行はCの責任。C担当がmodel agentの場合も手動操作の証拠を本人の操作記録と分ける。
- C/C'開始前にimplementation base/headを固定。A3 snapshot、B result、全diff/stat/name-status、Cより前の機械検査、raw XML/log/reportをhash付きでbundle化する。C'にはCの結論/findings/疑念候補と可変HANDOFFを渡さない。
- C': 新規セッション、AIならB/Cの双方と異なるモデル。可能ならAにも未関与の系列/ベンダーを予約。人間なら本人の確認範囲・所見・残存risk・判定を待ち、AIがPASSを代筆しない。対象headが変わればC/C'とも新bundleでやり直す。review記録だけのcommitはhead変更に数えない。
- A0/A1実績: Codex / GPT-6 Astra / OpenAI。
- A2実績: architecture担当 `/root/cd0_a2_architecture`、実験担当 `/root/cd0_a2_experiments`、A0のみの代替案担当 `/root/cd0_a0_alternative`（GPT-5 / OpenAI）に加え、別contextの `/root/cd0_a2_model_review`（GPT-6 Astra / OpenAI）が同じA1を独立レビュー。各担当は相互の指摘と現行実装を見ていない。ベンダー多様性はないためC'には未関与の別系列/ベンダーを優先する。
- A3採用: runnerからsession owner/event sink/filesystem inventory/cleanupを分離、Playerのfailure-stage対照、解除後borrowed参照禁止、asset/Scene取消分離、relocationのfresh Player条件、R5〜R7を採用。content-only stripping対照はrunner/rootが型参照しない`Cd0SceneMarker`とし、`Cd0ProbeAsset`はdependency load対象とする。部分取得後失敗をC必須caseにし、cleanup失敗中はunregister/retryを禁止する。native観測とledger自己申告を証拠上分ける。`GetContentDirectories()`はexact DLL/XMLに存在するため「一覧APIなし」指摘は不採用だが、run自身のhandle ledgerも併記する。全実験を別projectへ置く代替案は本repo direct API成立を測れないため不採用、Player比較だけ独立hostにする。pure ledger専用asmdefは今回は増やさず、Unity APIを使わない型として同Runtime asmdef内で検証する。既存asmdef変更0、fixture Runtime/Editor/Testsの3 asmdefを追加し、Player host側bootstrap asmdefだけfixture Runtimeへ明示参照する。
- A3 human integration: 2026-09-13、ユーザーの実装開始指示をCD0限定実装の承認として主担当が上記採否を統合しrevision 1を凍結。未知のnative挙動は設計判断で埋めず、Cの結果にする。
- C'予約: 未割当。A2で全系列を使い切らない。可用性不足なら独立性制約を記録し、人間監査へ渡す。

### A3までに決める事項

1. §4の独立Player hostでE7を行う案の採否。既存Addressablesを変更しない条件のための隔離であり、OSM Player統合はBS4に残る。この制約でGO/CONDITIONALをどう区別するか。
2. IL2CPP moduleとCの人間操作担当。未確保なら勝手なinstallやMono代替合格をしない。
3. 追加assemblyの名前/参照/autoReferencedと、fixture限定direct API境界。既存asmdef変更は0。
4. Native中途取消を再現する方法。発行と完了の間に観測点を作れない場合のinconclusiveを許容し、別検証へ渡す。

実験そのもので答える事項は、root借用寿命、invalid unregister後の状態、native失敗通知、実serialization、配布必須file、hash安定性、report/stripping条件。これらをA3前に推測で埋めない。

## 6. Phase B 実装結果

- 実装: `unity/Assets/CD0Spike/` にRuntime/Editor/Tests.Editorの3 assembly、18 C# / 803行、2 Scene、root/probe assetを追加。既存OSM/SampleGame/Addressables/package/profile/app-config/asmdefへの変更0。詳細は `artifacts/cd0-phase-a/B-result.md`。
- Editor: 人間が既に開いたexact `6000.6.0f1`へwrapperで接続。最終recompileはfailed=false/error 0。Editor APIでfixtureを生成し、元のclean SampleScene setupへ復元。生成後Console error 0。
- 検査: contract audit PASS。docs auditは検査1/2 PASS、既存U66 harvest警告のみ。
- HANDOFFとの差: 独立Player hostはCの実験環境なので未作成。JSONL sinkと全case matrix driverは、実際のnative結果を得る前に形式を固定しすぎないよう最小event sinkとcase ledgerまで実装。新しい設計判断が必要ならCで実装せずAへ戻す。
- 未実行: Unity tests、Content build、Player build/run、register/root/load/unload、failure/cancel/retry/unregister、relocation、incremental、HTTP。Phase B契約どおりCへ渡す。
- implementation head commit: `bfc7c677e1c470d58797fb38a0128336f6490e79`。ユーザーのuntracked Pre-Phase文書は未変更・未stage。
- Phase B担当・モデル: Codex / GPT-5 / OpenAI。

## 7. Phase C

- evidence bundle: `artifacts/cd0-phase-c/`。base `0a11a4be58c7b75356b076f356078d8d001c2e5b` / head `bfc7c677e1c470d58797fb38a0128336f6490e79` の完全diff、stat、name-status、生log/XML、機械検査を収録。
- 構造適合: 3 asmdef、既存asmdef変更0、Runtime/Editor/Test境界は適合。
- findings: 初回静的レビュー7件を採用して修正。再レビューで取消後のScene発行とincremental出力保護の2件を追加修正し、最終限定再レビューで確定的回帰なし。BuildContentDirectoryが例外を直接throwする経路とquarantine失敗は未実測。
- テスト: `CD0Spike.Tests` 10/10 PASS、全EditMode 688/688 PASS、failed/skipped 0。最初の試行は既存Editor lockで起動前停止し、その後の成功と分離して保存。
- 各AC/E結果: code/serialization/compile/EditModeのみ確認。Content build、登録/root取得、asset/Scene load/unload、failure/cancel/retry、relocation、Player/linker、R0〜R7は未実行のためHOLD/inconclusive。
- 担当・モデル: Codex主担当、静的レビュー `/root/cd0_phase_c_review` / GPT-6 Astra / OpenAI。

### Additional independent review (2026-09-14, Grok)

- 依頼: GitHub PR #53 `@cursoragent code_review`。implementation head は据え置き `189bc21086aaf96d77f64c017b4ebf7ad4301876`。
- 担当・モデル: Cursor Grok 4.6 / xAI。新規セッションだが C/C' 結論を読んでおり **独立性制約あり**。盲検 C' の代替ではない。
- 構造適合: 3 asmdef と責務分割は計画どおり。`分割済み`。既存 OSM/SampleGame/asmdef 参照 0。
- 追加 findings: G1 Player host の `Contains` が prefix 混同を許す（C' P3 の具体化）。G2 Player `outputPath` / report directory が CD0 root 外でも通る。G3 Content build/quarantine も contain 未検査。G4 Tests.Editor → Editor 参照が A3 本文から外れ B-result 未記録。G5 incremental ガードが BuildName のみ。G9 `async void` 中断時の native cleanup 漏れ。詳細は `artifacts/cd0-phase-c/cursor-grok-review.md`。
- 判定: 既存どおり **HOLD / inconclusive**。native 未実施を成功扱いにしない。G1–G3 は E7 前の修正候補だが、このレビュー commit では実装 head を更新しない。

## 8. Phase C'

- 担当方式: 新規agent/sessionによるblind audit。
- blind bundle: `artifacts/cd0-phase-c/blind-audit-bundle.md`。exact hashはmanifest。
- 確認範囲・方法: A3/B snapshot、固定diff/stat/name-status、機械検査、生test XML/logのみ。HANDOFFとC結論を入力から除外。
- 判定: **HOLD / inconclusive**。assembly隔離とEditMode回帰は成立したが、Content Directoriesのcore受け入れは未実証。
- findings: P1=E0〜E8のnative証拠なし、single happy-path runnerだけではE5 matrixを駆動できない。P2=directory/rootを含むrun-level ledgerとbuild evidence exportが不足。P3=Player host guardのcanonical containmentが未実装。
- 残存risk・監査不能範囲: native build/register/root/load/unload/retry/relocation/incremental/strippingの全項目。既存Player/季節Sceneは成功baselineがないためCD0判定へ流用しない。
- 独立性: Cの結論を事前閲覧せず、設計・実装にも未関与。利用可能モデルの都合でOpenAI/GPT系列内となり、モデルfamily多様性は未達。
- 担当・モデル: `/root/cd0_phase_c_prime_final`、Codex / GPT-6 / OpenAI。

## 9. go/no-go・Phase D・harvest

### 判断規則

- **GO（次のBuildSystem Phase Aへ）:** 保存/生成/Content build/local登録/root/Scene/正常cleanup/失敗後回復/移設/Player型保持が実証され、残る制約がOSM境界内で扱える。incremental粒度は実測値を採用。HTTP成功、661 Scene性能、本番互換は意味しない。実装採用やRET開始の許可でもない。
- **CONDITIONAL:** native取消不能だが要求取消後の安全なdrain/cleanupは実証済み、増分粒度が粗い、あるいはPlayerが独立hostでのみ確認できた等。制約ごとに必要な後続実証とownerを割り当て、BS1のpure selection設計へ渡してよい範囲とBS2/3/4で止める範囲を人間が判断する。
- **HOLD / inconclusive:** 環境不足、race未再現、stripping正負対照不足、Player未実行、証拠不足。backendが不適合だとは断定しない。未達実験だけを次へ残す。
- **NO-GO:** 支持されたAPIではScene寿命/解放/登録解除を安全に管理できない、復旧可能性が成立しない、必要なPlayer型保持が成立しないなど、本質的な不適合を再現した場合。代替backend比較の新Phase Aへ返す。CD0内でAddressables updateやContentLoadInterfaceへ置換しない。

### 次の設計へ渡すもの

Git/SVN混在方針はBS1/BS2のA0へ渡す。content選択をVCSの種類で決めず、必要なsourceがlocalに揃った入力とそのprovenanceをbuildへ渡す境界を検討する。取得・配置・更新の自動化はCD0で先行実装しない。SVNのpath/revision/.meta管理方針が未決でも、SVNを使わないCD0 fixtureの開始は妨げない。

BS1にはcontent選択とphysical buildの分離に必要な制約だけを渡し、BuildTagの最終型をCD0で決めない。BS2にはroot authoring、到達依存、build target、生成物、report、差分/hashの観測。BS3にはroot/asset/Scene/directoryの所有権、native取消の限界、後始末、retry条件。BS4にはreport入力とstripping/型保持、独立hostと本repoの差、既存callbackとの統合が未実証であること。DISTにはlocal移設の成立条件だけを渡す。

Phase DはC/C'を突合し、判定と範囲を人間が決める。検証した現況だけを§0へharvestし、一時code/assets/hostをinventoryに沿って撤去する。再現に必要な証拠と最小source snapshotはbundleに保管し、production sourceとして残さない。本書を削除してdocs/README.mdの一覧を更新する。U66のharvestをこのsliceの完了条件に混ぜない。

## 10. 一次資料

各URLは2026-09-13に取得・本文確認。WebのAPI存在とローカルexact DLL/XMLの一致までが今回の確認であり、実行結果ではない。

- [Content Directories introduction — Unity 6.6](https://docs.unity3d.com/6000.6/Documentation/Manual/content-directories-introduction.html): local-only、root、incremental、build間の依存重複。
- [Content references — Unity 6.6](https://docs.unity3d.com/6000.6/Documentation/Manual/content-directories-references.html): tracked referenceとrootからの到達性。
- [Load content directories — Unity 6.6](https://docs.unity3d.com/6000.6/Documentation/Manual/content-directories-load.html): root queryのscope、登録順、解除の前提。
- [BuildPipeline.BuildContentDirectory](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/BuildPipeline.BuildContentDirectory.html): signature、root入力、active target、report。
- [BuildPlayerOptions.previousBuildReportDirectories](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/BuildPlayerOptions-previousBuildReportDirectories.html): UnityLinkerへ追加Content buildの型情報を渡す入口。

create/scenes個別manualページは今回Web取得エラーがあったため、その本文を確認済みとはしない。Scene生成utility/Loadableの正確なsignatureは§1のexact Editor同梱資料とDLL metadataを根拠とする。
