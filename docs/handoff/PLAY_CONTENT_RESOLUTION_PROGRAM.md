# 要求時の Content 解決と Editor Play の入口（Phase A0 planning packet、revision 3.2）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。

- type: `program`（A0 の planning packet。実装スライスではない。§8 のスライス列の各 A0 への入力）
- status: `A`（A0 revision 3.2。revision 3 への独立計画レビュー R2 の高優先と owner が任せた M-07 を 3.1 で、revision 3.1 への独立計画レビュー R3（§11）の F-01〜F-11 と U1〜U10 の推奨を 3.2 で反映した。R3 の U4 の後半だけ不採用（§11）。R2 は A0 の問題設定と構成を支持し、S0-a / S0-b / S0-c の A1 着手を支持している。revision 2 は外部の計画レビュー R1 で問いを上位へ戻した版。その後 owner 側で作られた提案 Rev3（要求時解決、Editor で開いている Scene の最優先）と Rev4（SampleGame への依存を API と合格条件から外す）を受け、owner の回答 A1〜A10（§11）で確定した方針を本 revision に反映した。A1 は未着手。**実装キューではない**）
- branch: `claude/osm-content-playas-issue-51y1cj`（計画ブランチ。PR の base は `develop`）
- implementation base commit: `e022934`（`develop` の head、2026-11-07。本書の実測は全てこの base。各スライスの着手時に head を取り直す）
- implementation head commit: 該当なし（program）
- risk: 各スライスで決める。S0-b（混成の技術実証）と S3（Runtime への接続）は `high`
- owner: root（依頼者）
- created: 2026-11-07（revision 1）、2026-11-08（revision 2・3）
- expires: 2026-12-31、または §8 の各スライスの HANDOFF へ取り込まれた時点。期限時は owner が更新・廃止を判断する
- harvest to: `unity/Assets/Docs/Architecture/18-asset-description.md` §4（Play の入口と Content の経路）、`04-app-startup.md` §4.6〜§4.8（Editor Play の起動条件）、`13-resource-system.md`（要求時解決と所有）、`20-variant-checkout-workflow.md` §1・§6（部分 checkout。「部分 Checkout 開発の再燃は未所有」の行を本 program が所有する）、`README.md`「試す」。program の完了時に反映する
- 関連: [ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)（**所有境界の正本。** 本 program の resolver はあちらの §2.1 の「連携部分」に置き、U-A / U-B の P 検査に参照を増やさない。境界を本書で定義し直さない）、[RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md](RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md)（同じ起動コードを触る。§8 の順序制約）、[SEASON_WORLD_DESIGN.md](SEASON_WORLD_DESIGN.md) W-5 / W-6、[BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md](BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md)（local object store の形式）
- Phase A snapshot の取得案内・状態: 該当なし（A0 は program の地図で、凍結する snapshot を持たない）
- Phase B result snapshot / evidence bundle / C' blind bundle: 該当なし
- 引継ぎ票:
  - 次の Phase・役割: owner が本 revision の §7 と §10 を確認する。確認後、S0-a・S0-b・S0-c の A1 を**別々に**始める（§8。3 つは独立で並行可）。
  - 次の作業: 各 S0 の HANDOFF を `slice` として切り、本書 §3〜§6・§9 を A0 の入力として A1 初稿を書く。新しく A1 を始めるので、[Phase と HANDOFF](../../.agents/skills/osm-workflow/references/phases-and-handoff.md#phase-a-の内部フロー) の利用者の入力の表、目的レビュー、分割の検討、不変条件と成立条件を適用する。各 A1 に「どうすればその問いに誤って YES を出せるか」の反例を書く。
  - 引き継ぐ commit: 本書を含む計画 PR の最終 commit（tree の外で渡す）。
  - 必要な権限: なし（文書のみ）。
  - 停止規則: S0-b の結論が出るまで、Runtime の新 API、配布粒度、複数 directory の合成を凍結しない。S1 以降は S0-b の Phase D より前に Phase B を始めない。Runtime を触るスライス（S0-b の観察 code、S3）の Phase B を `RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` の Phase B と同時に走らせない。
  - 引き渡さないもの: なし。

### 実測の方法

- 名前付き参照と行数: `grep` と `wc -l`（base `e022934`）。Rev3 / Rev4 が根拠に挙げた code の事実（§2.4）は読み直して確認した。
- 動作の確認は行っていない。Unity 上での再現は各スライスの A1 と S0 で行う。
- 本書の作成環境に `pwsh` が無く、`tools/docs-audit.ps1` は未実行。link 先の存在は `git ls-files` で手で照合した。計画 PR の作成前に owner 側で通す。

## 1. 目的と利用者

- **目的:** Scene や Asset のロード要求に対して、呼び出し側が供給元の種類・所在・取得手順を知らなくても、妥当な実体を要求時に解決してロードし、その所有・解放・失敗理由を既存の AssetManagement と SceneSystem と同じ契約で扱えるようにする。その上に、Unity の Play ボタンを押すだけで Editor Play に入れる入口を置く。特定のゲーム進行、世界の形、制作体制、レベル分割、ネットワーク運用の前提は core に持ち込まない。
- **利用者:** (a) 編集する開発者。担当する Scene を Editor で開いて編集し、Play で確認する。担当外は手元に無くてよい。(b) 遊ぶだけの人（テスター・企画）。source を持たず、検証済み content だけで Play する。(c) develop を clone した初見の人。README を読む前に Title を開いて Play を押す。(d) OSM の AssetManagement か BuildSystem を単独で使う利用者。resolver も SceneDirector も無い。
- **この program が答える上位の問い:** 「ロード要求に対して、供給元を知らない呼び出し側が、要求時に妥当な実体を得て、所有・解放・失敗を同一の契約で扱えるか。そしてその上で、Play ボタンだけで説明可能な Editor Play を始められるか。」
- **上位の問いの分解:**
  - **Q1 要求:** 呼び出し側は何を要求するか（logical identity、kind、表現、互換の制約）。Cell か UI 画面かを知る必要は無い。
  - **Q2 供給:** 要求を満たす候補を、優先順位を持つ provider から要求時に探せるか。
  - **Q3 信頼と互換:** どの候補が使用可能かを、検証済み identity・能力・互換条件で判断できるか。
  - **Q4 所有:** 得た Scene / Asset のロード、共有、cancel、unload、release、directory の unregister を一貫して扱えるか。
  - **Q5 観測:** 何をどの供給元から得て、何が失敗したかを利用者が説明できるか。
  - **Q6 統合の任意性:** SceneDirector、BuildSystem、Editor、VCS、LAN を使わない利用者にも、可能な範囲で成り立つか。
- **降格した問い:** revision 1 の「宣言 1 つで Play を押すだけで差分を source overlay で読む」、revision 2 の「Play 前に全 Scene の供給元を PlaySessionPlan で確定する」。どちらも手段を問いに含めていた。前者の sha 差分による適用判定は採らない。後者は「Play 前に確定するのは規則と互換基準だけ、Scene ごとの解決は要求時」に置き換える。

## 2. 現況（base `e022934`）

### 2.1 利用者から見た現況

- `develop` を clone し、`Title.unity` を開いて Play を押すと、BeforeSceneLoad で起動が失敗し、Console に英語の例外が 1 行出て止まる。`app-config.json` に `content:runtimeMode` が無く、`AbstractApplicationInitializer.ReadContentDirectoryConfiguration`（`unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs` 837〜866 行目）が「未指定は Delivery の verified pair が無ければ失敗」と判定するため。PR #115 以降の契約で、[§4.7](../../unity/Assets/Docs/Architecture/04-app-startup.md) に「known-good の自動適用はしない」と明記されている。壊れているのではなく、宣言を選ばせるための意図的な関門である。
- 動く入口は 3 つ、全て menu で、宣言 6 個 × 経路 3 本に展開されている。`Tools/OSM/Play As/<宣言>`（source のまま。`PlayAsLauncher`）、`Tools/OSM/Content/Verify Play/<宣言>`（build → publish → install → Play。`VerifyPlayLauncher`）、`Tools/OSM/Content/Delivery`（公開済み directory を受け取る窓）。`Tools/OSM/Content/Build <宣言>` が別に 6 個。root が `OneStarMaker/` と `Tools/OSM/` に割れている。
- 前提条件は 6 種類あり、`PlayAsLauncher.Check` が最初の 1 件だけを英語の文字列で返し、`DisplayDialog` の OK だけで止まる。
- Delivery 窓は manifest SHA-256、revision、disk budget 等の生の 10 欄。
- Play 中に「今どの宣言・どの content で動いているか」を見る場所が無い。
- 検証済み content の供給が無い。CI は develop の head ごとに publish していない（[§20](../../unity/Assets/Docs/Architecture/20-variant-checkout-workflow.md) 冒頭）。
- 所要時間の報告: Artifacts の fetch は Spring 221 object で 742 秒（R2。[tools/Artifacts/README.md](../../tools/Artifacts/README.md)）、Content Directory build は 225〜668 秒（[RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md](RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md) §2）。LAN からの install と cache hit は未計測。

### 2.2 Framework にある部品

| 部品 | 場所 | 行数 | 役割と、本 program に効く事実 |
|---|---|---|---|
| `IAssetManagement` / `AssetManagement` | `Runtime/AssetManagement/` | 494（AssetManagement） | `LoadContentAssetAsync` / `LoadContentSceneAsync` / `InstantiateContentAsync` が型付きの入口。owner 台帳と resident cache を所有。identity と表現を 1 つの台帳で管理 |
| `AssetManagement.ContentDirectory.cs` | 同 | — | `_contentDirectory` は単一。`InstallContentDirectory` は 2 回目を `InvalidOperationException` で拒否（20〜22 行目）。**複数 directory の合成は未実装** |
| `SealAddressables` / `SealedAssetBackend` | 同、`Internal/` | — | directory と決まった直後に Addressables の取得経路を 1 回だけ封じる。in-flight、resident cache、registry を固定 |
| `ContentDirectorySession` / `UnityContentDirectoryBackend` / `ContentDirectoryIndex` | `Runtime/BuildContent/` | 481 / 133 / 130 | 1 directory = 1 root の登録、native handle / token / close、`(logical key, representation, kind)` の索引は単一 root 内 |
| `ContentTransportManifest` / `ContentManifestValidation` | `Runtime/BuildContent/Distribution/` | — | `declaration`（名前・表現・runtime 既定値）は **optional で検証されない**。上限 4 MiB / 100,000 件 |
| `ContentSourceAdvisor` | 同 | 77 | `sourceFiles` と手元の file を比べ `Complete` / `Missing` / `Changed`。`Missing` が 1 件でもあれば `Changed` を返さない。checkout 案内用で、ロード索引ではない |
| `ContentCacheStore` / `ContentInstaller` / `InstalledRevisionVerifier` | 同 | 438 / 185 / 128 | cache root への install、known-good pin、disk budget、再検証、lease |
| `LocalContentArtifactSource` / `HttpContentArtifactSource` | 同 | — | local / LAN と HTTP の file stream 取得口。「どの Scene がどの package にあるか」の索引は持たない |
| `BuildPurposeDeclaration` / `BuildPurposeResolver` | `Editor/Build/Selection/`（asmdef `OneStarMaker.Build.Selection`、Engine 参照なし） | 278 | 宣言から実行に必要な到達集合を解く。build 時にだけ呼ばれる |
| `BuildContentCoordinator` / `ContentTransportPublisher` | `Editor/Build/Content/` | 188 / 143 | Content Directory build と publish。publish は `Full` build だけ受け付ける |
| `RegisterAlreadyLoadedScenes` | `AbstractApplicationInitializer.cs` 640 行目 | — | Editor で開いていた Scene を起動時に SceneDirector へ登録。`PerformUnitySceneLoad` は名前で再利用する。**由来（Editor か directory か）の区別は無い** |

### 2.3 SampleGame にある知識（host application 側に残す物）

| 知識 | 場所 | 行数 |
|---|---|---|
| 宣言の一覧と初回 Scene、companion set の既定 | `SampleGame/DependOnAll/Editor/Build/SampleGameBuildPurposes.cs` | 78 |
| 宣言 → config key=value と環境変数名 | `PurposeLaunchSettings.cs` | 101 |
| Play As の前提条件の検査と `EnterPlaymode` | `PlayAsLauncher.cs` | 176 |
| 検証 Play の段。**起動が再利用しうる Scene が開いていれば拒否する**（`CheckBeforeStart`、246〜278 行目） | `VerifyPlayLauncher.cs` | 499 |
| 1 回の Play で戻す / 手で戻す ledger | `ContentDeliveryPlayBridge.cs` | 234 |
| Delivery 窓、Content build の入口、BS4 Player | `ContentDeliveryWindow.cs`、`SampleGameContentBuild.cs`、`SampleGamePlayerBuildCoordinator.cs` | 212 / 148 / 152 |

### 2.4 成立していない前提

- **source と directory の同居。** 起動は source か directory かを BeforeSceneLoad で 1 つ選び、directory なら Addressables を封じる。表現は起動時に固定。`EditorSceneManager` で Scene を開けることと、OSM の所有・unload・依存・native handle を混ぜて正しく解放できることは別の問題。現行は保証していない。Verify Play はこの衝突を「開いている Scene を閉じる」で避けている。
- **旧 Hybrid Play の前例。** [§20](../../unity/Assets/Docs/Architecture/20-variant-checkout-workflow.md) §1「Checkout 済みかつ依存閉包が完結するアセットは AssetDatabase 直読み、未 Checkout は remote catalog」は 2026-09-20 に切断された。新しい混成は同じ技術ではないが、廃止理由の回顧を S0-b の A1 に含める。
- **Unity の複数 Content Directory。** `ContentLoadManager.RegisterContentDirectory` は複数登録でき、Editor では Play 中だけ登録できる（Unity 6.6 公開文書。OSM での適合は未実証）。横断探索は後から登録した directory が先になり、直接参照の依存は元の directory に閉じ、同じ asset を別 directory に入れると重複ロードされうる。**「複数登録できる」は「異なる build を安全に合成できる」を意味しない。**
- **宣言名の一致は互換を意味しない。** manifest の `declaration` は optional で検証されない。既存の消費者は contentSet / revision / target / Unity version / root schema / digest を検証するが、要求した実行内容と同じかまでは示さない。
- **local object store と installed cache は別の層。** Artifacts の store に置いただけでは Play に使えず、cache root への install と verify が要る。
- **`ExitingEditMode` は Play への移行途中。** 探索、install、hash を同期で置けるとは限らず、取り消し → 準備 → 再 Play、重複 event、compile / import 中、domain reload、crash を含む状態機械が要る。

### 2.5 類似事例（2026-11-08 調査。A1 の比較材料で、採用の決定ではない）

「Editor で開いている Dirty な Scene の複製」と「built content」を同じ Play で、所有の契約付きで扱う公開事例は見つかっていない。部分ごとに近い物を挙げる。「確認」は一次の公開文書を読んだ物、「要約」は検索結果の要約だけで、原典・source は読んでいない。

| 事例 | 近い部分 | 効くスライス | 確度 |
|---|---|---|---|
| Unity Entities の Content Management（[概要](https://docs.unity3d.com/Packages/com.unity.entities@1.4/manual/content-management-intro.html)、[archive の作成](https://docs.unity3d.com/Packages/com.unity.entities@1.4/manual/content-management-create-content-archives.html)、[配信](https://docs.unity3d.com/Packages/com.unity.entities@1.4/manual/content-management-delivery.html)） | subscene ごとに content archive を作り、複数の subscene が共有する object は共有 archive へ分ける。Player を build し直さずに `Assets > Publish > Content Update` か `RemoteContentCatalogBuildUtility.BuildContent` / `PublishContent` で archive と catalog を作れる。publish は file 名を内容の hash にし、出力は端末の local cache と同じ構造。weak reference は利用者が load / release し、参照数で unload。`ContentDeliveryGlobalState.Initialize` で remote から content set を取得・cache する | §5.2 (ii)（小さな directory を Play 前に作る）、S2 の共有依存の単位、S4 の cache | 確認（Entities 1.4）。Editor Play で archive を読むか AssetDatabase を読むか、Editor 由来との混在、版と hash の検証は文書に記述なし |
| Unity `Loadable<T>`（[Unity C# reference source](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/ContentLoad/Managed/Loadable.cs)） | Unity の実装コメントは、Editor Play 内で built content 由来の `Loadable<T>` は登録済み Content Directory 側を、project 由来の `Loadable<T>` は `AssetDatabase` 側を参照すると明記する。後者は Content Directory と異なり参照カウントで解放されない。source / built の**共存自体**に Unity 側の先例があるが、OSM の SceneBase lifecycle・owner・release の統一は未検証 | S0-b（混在の実証条件）、S3（所有・解放 adapter） | 確認（Unity C# reference source の API コメント。OSM の混成実装の成立証拠ではない） |
| Unity Addressables の Play Mode Script（[開発サイクル](https://docs.unity3d.com/Packages/com.unity.addressables@1.16/manual/AddressableAssetsDevelopmentCycle.html)） | Asset Database か既存 build かを 1 つ選ぶ。混成の公式手段は無い。現行 OSM の「起動時に 1 つ選ぶ」と同じ立場 | S0-b の比較の基準、S0-c | 要約 |
| Unity `ContentLoadManager.RegisterContentDirectory`（[6.6](https://docs.unity3d.com/ScriptReference/Unity.Loading.ContentLoadManager.RegisterContentDirectory.html)） | §2.4 に加え、Editor Play で built content を読むには type tree 付きで build する必要がある（`DisableWriteTypeTree` の content は Editor で読めない） | §5.2 (ii) の build 設定と所要時間 | 確認 |
| O2 Horizon（[README](https://github.com/OxygenButBeta/O2.Horizon)、[Virtual backend](https://github.com/OxygenButBeta/O2.Horizon/blob/main/Runtime/VirtualStreamBackend.cs)、[Baker](https://github.com/OxygenButBeta/O2.Horizon/blob/main/Editor/StreamWorldBaker.cs)） | 編集元を正本に保ちつつ、Bake 不要の Virtual（Play 中の GameObject を cell container に移し `SetActive`）と、別の Cell Scene を Bake して読む Baked を分ける。**Virtual は Play 前の Scene 複製ではなく**、OSM が目指す「同じ Play の中で Scene A は Editor、B は built」の解決でもない。編集・実行の表現分離と低コストな反復の比較対象 | S0-b（Unity 標準 Play / 複製の費用と非破壊性を比較）、S0-c（Play 体験） | 確認（公開実装。混成 Content Resolver の先例とはみなさない） |
| Unreal の Cooked Editor / `FHybridPackageStoreBackend`（[API](https://dev.epicgames.com/documentation/unreal-engine/API/Runtime/CoreUObject/FHybridPackageStoreBackend/ForceLoadPackageAsLoose)、[KB](https://forums.unrealengine.com/t/how-to-work-with-cooked-content-in-editor/265094)） | Editor が cook 済み package を読み、編集する物だけ cook 済み版をやめて source に戻す。Scene A が source、Scene B が built の形に最も近い。World Partition の map は Cooked Editor で未対応と Epic が述べている | S0-b（旧 Hybrid Play の回顧の比較対象。混在時に同じ asset が source と cook 済みの 2 実体になる問題の扱いを見る） | 要約 |
| Unreal の Cook on the fly / Zen storage server（[COTF](https://docs.unrealengine.com/4.27/API/Editor/UnrealEd/CookOnTheSide/ECookMode__Type/index.html)、[Zen](https://dev.epicgames.com/documentation/en-us/unreal-engine/using-zen-storage-server-as-cooked-output-store-for-unreal-engine)） | 要求された時に cook して配る server。要求時解決と供給元の分離 | S0-a、S4 | 要約 |
| Unreal の IoStore OnDemand（Experimental、[API](https://dev.epicgames.com/documentation/en-us/unreal-engine/API/Runtime/IoStoreOnDemandCore/IOnDemandIoStore)） | 実行中に必要な content を install / stream し、cache の verify・purge・必要容量の照会を持つ | S2（Stage II）、S4 | 要約 |
| GDC 2023 Guerrilla「Scaling Tools for Millions of Assets」（[Vault](https://gdcvault.com/play/1028848/Scaling-Tools-for-Millions-of)） | 手元の file を直接読まず server から content を配る Editor | S4（LAN tier） | 要約 |
| Insomniac「Loading Based on Imperfect Data」（[GDC 2013 Vault](https://www.gdcvault.com/play/1018201/Loading-Based-on-Imperfect)） | source と built の asset 管理を、tool の反復速度を落とさずに両立させる | S0-b、S2 | 要約 |

## 3. 概念モデル（A1 で API の形を決める。型名は仮称）

Rev4 の最小の汎用モデルを採る。**既存の `IAssetManagement`、`IContentDirectoryBackend`、BuildSystem の入口で足りる所に重複する interface を増やさない。** これは S0-a が最初に判定する。

- **ContentRequest（要求）:** `LogicalKey`（Scene なら Scene identity）、`Kind`（最初は Scene。他種別へ拡張できる形）、`Representation`（要求元が与える data。resolver の enum にしない）、互換の制約、cancellation、診断の相関。SceneDirector の状態、親子、Season、SceneResourceMap、VCS の知識を入れない。
- **Provider / Availability（供給候補）:** 「この要求と policy に使用可能な候補があるか」を、検証される候補の identity / immutable digest / manifest reference / metadata として返す。任意の path をそのまま信用しない。探す段と Unity のロードを分ける。候補の取得・install は供給形態の adapter が担う。
- **ResolutionPolicy（規則。Play 前に固定）:** provider の順序、採用条件、互換基準、timeout / cancellation、明示 override、Editor snapshot の採用規則、bootstrap の基準。**将来どの Scene が要求されるかを列挙しない。** immutable。
- **ResolutionResult / LoadRecord（1 要求の決定と実績）:** 得た候補、実効表現、所有、理由。実際にどの version / digest / provider で成功し解放されたか。**予定（policy）と実績（record）を別の型にする。** record の到達度は 2 段で区別する（R2 F-02）。**Traceable:** どの入力を選んだかを説明できる（identity、採用時点、dirty、hash、供給元）。**Reproducible:** 必要な入力 bytes と依存と toolchain を再取得でき、再構成が成立する。本 program の必須要求は Traceable までで、Reproducible は保証しない。
- **Editor snapshot tier（owner 決定 A1）:** Editor で開いている Scene は、**Play 前に退避した複製**を Play 中の実体とし、開いている Scene 自体には影響を与えない。複製は要求時に「既にある Scene」の特殊な候補として最優先で採用する。`Assets/` に source file があるだけでは候補にしない。Dirty は採用し、未保存である事実を record に残す。複製の機構は S0-b で比較する（§5.2）。**「複製を作る」と「元の Scene を壊さない」は別の契約**で（R2 F-01）、後者は I11 として S0-b が実機で証明する。
- **供給元の連鎖（Editor Play の標準 policy）:** Editor snapshot → Local（検証済み installed cache → local の publish 済み成果物を verify / install）→ LAN（verify / install）。Cloud は tier として実装せず、adapter の差込口だけ確認する（A5）。tier を設定していない利用者は、その tier を持たない policy で動く。

### 3.1 混ぜてはいけないもの（Rev3 §11 から。A1 の用語の検査に使う）

1. Editor で開いている Scene の snapshot と、disk 上に source が存在する Scene。
2. Scene がロード要求された時点と、Editor Play が始まる時点。
3. local の publish 済み directory が存在することと、DIST で検証・install されていること。
4. Content Directory が複数登録できることと、異なる build を安全に合成できること。
5. 同名の contentSet と、同一の実行内容。
6. 編集する範囲（任意の Workset）と、その Play で実際に snapshot を採用した集合。
7. ResolutionPolicy（規則）と LoadRecord（実行事実）。
8. 供給元の tier と、その Scene が今どこに cache されているか。
9. manifest の `sourceFiles` と、Runtime が持つ package / dependency の索引。
10. ネットワーク失敗による次 tier への探索と、非互換 revision への暗黙 fallback。後者は整合性の失敗（hash 不正等）の後に別 revision を採ることを指し、互換の規則が上位の候補を理由付きで採らず、下位の互換な候補へ進むことは含めない（S1 の U-S1-6、owner 裁定 2026-11-09）。

## 4. 要求（A1 の入力）

### 4.1 要求時解決の契約

- 供給元の選択は**ロード要求時**に行う。Play 前は RunPurpose 相当の互換基準と policy と、bootstrap が必須とする graph / config / UICommon の取得可能性だけを確定する。
- **物理的な Scene ロードと OSM lifecycle は別契約。** Editor で開いている Scene は resolver の供給候補であって、その事実だけで `SceneDirector.AddScene` を要求したことにはしない。`SceneBase` の初期化・進行は SceneDirector への明示的な要求（必要な bootstrap Scene も明示要求に含む）を起点とする。Unity の `Awake` / `Start` が実行されないことは保証せず、ゲーム進行・初期化をそれらに依存させない。現行 `RegisterAlreadyLoadedScenes()` は開いている map 登録済み Scene に `AddScene` を呼ぶため、この分離の成立と移行方法は S0-b で実証する。
- `not found`・未接続・timeout は次 tier を試す。hash 不正・version 不整合・権限拒否は記録し、**同じ expected digest の正規コピーで修復できる時だけ**下位 tier へ進む。別 revision・別表現へ黙って切り替えない。ここでの「hash 不正・version 不整合・権限拒否」は、供給元が自分の申告と bytes の食い違いとして検出し `Rejected` で返す整合性の失敗を指す。正しく作られた候補が互換の基準に合わないことは互換の不一致で、理由付きで記録して次の供給元へ進む。別 revision へ切り替えたくない host は基準の revision か要求の expected identity で固定する。「別表現」は要求が表現を指定した時だけ数える（S1 の U-S1-6、owner 裁定 2026-11-09）。
- 必須 Scene の失敗は fail-closed。任意 Scene（距離 streaming 等）の失敗の扱いは呼び出し側の policy が決め、resolver は欠損を成功にしない。
- この段落の revision 規則は、**検証済み package 同士の合成**に限る（R3 F-01）。異なる logical key について Editor snapshot と directory を併用することは revision の寄せ集めではなく、§3 の標準連鎖であり S0-b が実機で見る。同一 logical key は snapshot を 1 つだけ実行し、directory 側は実行しない（G21）。複数供給元の**同じ revision**から必要な物を取るのが標準。異なる revision の寄せ集めは、versioned composition の契約を別に実証するまで拒否する。**revision 名の一致は必要条件の一部に過ぎない**（R2 F-04）。独立に build した package は同じ名前でも依存 graph や shared asset の構成が違い得るので、package の合成可否は互換 envelope（1 候補の採否の基準（content set / target / engine / schema、任意の revision）は S1 が定め、合成の欄は S2 が定める。S1 の U-S1-1、owner 裁定 2026-11-09。共同の immutable build plan / dependency graph の出所、target / engine / schema、表現、shared asset の所有と重複の方針、同一 logical identity の衝突規則、package 索引の hash 関係）で判定し、名前と個々の digest だけで採用しない。
- resolver は暗黙に Build を始めない。Build は「互換な候補が全供給元に無い」時の**明示された回復操作**で、Scene ロードの裏で Unity の build を走らせない。
- Editor API を Runtime asmdef に持ち込まない。Editor snapshot tier は Editor 専用 adapter で、Player には無い。

### 4.2 待ち時間の扱い（owner 決定 A2）

要求時解決は契約で、待ち時間は**契約を変えずに安い軽減策で防ぐ**。S0-c と S4 の A1 で、実際の操作で問題になる use case を列挙し、各 case に最も安い予防を 1 つ対応づける。候補:

| use case | 問題 | 安い予防の候補 |
|---|---|---|
| Title で Start を押した瞬間に Spring の revision が LAN に無い | ゲーム中に数十秒〜数分止まる | Play 入口で RunPurpose 相当の集合を prefetch する（Play 前の確定ではなく先読み） |
| VCS から Scene を checkout / update した | その Scene の周辺 content が手元に無い | **checkout の hook で対応する Content Directory も一緒に落とす**（A2）。Editor 操作からも実行できる |
| fresh clone、供給が無い | 候補がどこにも無い | source-only Play（現行 Play As）を供給の無い現場の標準経路として残す（A8）。Build は明示の回復 |
| streaming で新しい領域に入った | cold load | 先読み・保温 cache・並列 fetch。Stage II の粒度（§5.3）と合わせて実測で決める |

Rev3 の「Stage I: 最初の miss で revision 全体を取得」は暫定として許すが、最終目標の達成とは呼ばない。

### 4.3 Editor Play の入口（UX。owner 決定 A6・A10）

Rev2 の UX 要求は、core ではなく Editor adapter の**要求**として残す。次の 3 つは program の必須条件とし（R2 M-07。owner は A10 で判断を任せた）、表示部品の配置と通知の細則だけを S0-c の裁量にする: **(1) Play ボタンから起動できる。(2) 利用者の明示設定を壊さない。(3) 失敗と由来を診断できる。**

- Play ボタンが入口。`ExitingEditMode` で `content:runtimeMode` がどの層にも無い時だけ**設定に**介入し、どこかの層に値があれば意図した設定として書き換えない。介入は取り消し → 準備 → 再 Play の transaction として設計し、二重 Play・再入・crash 後の ledger 残留を防ぐ。
- **設定を書き換えないことと、起動内容を検査しないことは別**（R2 F-03）。明示設定がある場合も、起動する Scene 群と Editor で開いている Scene の由来を検査し、明示の directory-only Play に Editor の既ロード Scene が混入するなら理由を知らせ、厳密モードなら停止する。「Editor snapshot を優先する policy」と「厳密な directory-only」は明示的に区別する。現行 `VerifyPlayLauncher.CheckBeforeStart` が持つこの保護は入口の統合で消さない（G18）。
- RunPurpose は永続の選択（週に数回）。初期値は前回の値、初回は host の既定。Play のたびには選ばない。
- 通知は 3 段。無言（計画が一意で Play に入れる。Console に要約 1 行と観測 UI）、非ブロック（複数候補から known-good で選んだ、供給元の縮退、snapshot の件数）、ブロック（互換な候補が無い、宣言 mismatch、graph の違い、必要な bootstrap の欠損、直せない前提条件、取得の失敗）。
- 黙って直してよい前提条件は Framework が所有する物（Addressables の Play Mode Script）に限る。Variant profile、手動 Delivery の ledger、明示 `content:runtimeMode`、環境変数は利用者の明示操作なので止める。
- 「今後聞かない」は非ブロックの通知だけに許し、互換性の失敗・未反映・検証の失敗は永続的に黙らせない。
- 観測は policy と record を分けて表示し、SceneView だけでなく GameView 中の検証者も見える場所を 1 つ含める。
- 既存入口の行き先: `Play As` と `Verify Play` の 12 menu は Play 割り込みに吸収。`Content/Build <宣言>` は残す。Delivery 窓の 10 欄は詳細へ格下げ。

### 4.4 責務（Rev4 §10。境界の正本は [ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)）

- **AssetManagement:** Scene / Asset の owner、同時ロードの調停、release、cache。provider 選択の知識を必須依存にしない。
- **BuildSystem:** content selection、materialization、正規 Content Directory と manifest の生成。SceneResourceMap を必須の入力にしない。
- **Content Distribution:** transport からの取得、整合性、install、disk budget、cache lease。Editor Scene を知らない。
- **任意の resolver（連携部分）:** 要求 → 使用可能な候補の探索、必要な時だけ install と backend の接続、record。SceneDirector / Editor / SampleGame に依存しない核。
- **SceneSystem adapter:** Scene lifecycle と既存の Scene registry に `ContentRequest` を接続する。SceneDirector はゲーム固有名を知らない。
- **Editor adapter:** 開いている Scene の snapshot と一致確認、Play 入口、Editor 専用設定と UI。Runtime / Player の共通依存にしない。
- **Host application（SampleGame）:** 何をいつ要求するか、表現の policy、bootstrap、RunPurpose の一覧、任意の VCS adapter。季節や Cell の policy はここに属する。
- **VCS adapter（任意）:** Workset からの checkout / status、SVN 固有の実装。checkout hook から Content の prefetch を呼べる（A2）。手動 checkout の人も同じ policy で動く。

依存は host → OSM の向き。OSM core が `SampleGame` を参照することは無い。SampleGame の無い素の Unity project で成立する最小の public 利用経路を各 A1 に書く。

## 5. 仮説と代替案（A1 と S0 で検証する。確定ではない）

### 5.1 構成の案（Rev4 §6）

| 案 | 構成 | 長所 | 課題 | A0 裁定 |
|---|---|---|---|---|
| A | 任意の resolver / provider 連携層を AssetManagement と DIST の上に置く。Editor adapter は別。SceneDirector も任意の呼び手 | 単独利用と用途非依存を守りやすい | 既存の session ownership との接続を実証する必要 | **第一候補** |
| B | AssetManagement の全 load API に provider chain を必ず組み込む | 利用側が単純 | 単独利用に LAN / Editor の機構が混入する | 不採用寄り |
| C | SceneDirector のロード処理に直接実装 | SceneSystem だけなら早い | SceneDirector が無い呼び手に使えない | 汎用実装としては不採用。adapter は可 |
| D | Game 側に専用 resolver | 特定タイトルでは最短 | 共通契約を再利用できない | 汎用の正本としては不採用 |

### 5.2 Editor snapshot の機構（S0-b で比較。owner 決定 A1 の実現）

- **(i) Scene の複製を一時 asset として置き、AssetDatabase / `EditorSceneManager` から読む。** 軽い。ただし directory 封じの下で Editor の load 経路を通すので、Rev2 の B-03 と同じ所有・封じ・台帳の問題が残る。directory mode では config の直後に Addressables を封じるので、**封じの後に AssetDatabase / Addressables で複製を読む案は比較に入れない**。封じの前に既ロード Scene として渡す案は、由来を持たない既存の既ロード登録に乗るので、実験 (1)(3)(4) で見る（R3 F-11、U2）。
- **(ii) 複製から小さな一時 Content Directory を build し、通常の directory として登録して読む。** Runtime は directory しか見ないので hybrid の問題が消える。代償は Play 前の build 時間（Scene 数件）と、**複数 directory の登録**が必要になること（§2.4 の未実証）。publisher が `Full` 限定なのも触る。
- どちらでも、複製は Dirty の bytes を含み、その hash を record に残す。**hash は出所追跡（Traceable）の手掛かりであり、依存を含む厳密な再現（Reproducible）を保証しない**（R2 F-02）。参照先 prefab / material / ScriptableObject、compile 済み C#、GUID / meta、serialized component の schema が違えば再現できず、temp asset を Play 後に消すなら bytes 自体が残らない。Play 後に snapshot の bytes を保存したかは record に分けて残す。Unity の Play mode が終了時に開いていた Scene を復元する既存挙動は S0-b の比較対象にする。
- **S0-b の結論（2026-11-08、PR #172、Unity 6000.6.0f1）:** スライスの判定は GO（判定 C と C' の 2 巡目が独立に同じ結論）。(i)・(ii) とも Enter Play Mode の 4 設定すべてで全 cell が PASS か定義済みの N/A で、**どちらも「成立」**。GO は技術的な採用候補として残ったという意味で、製品統合の証明ではない。**勝者は選んでいない。**
  - **(i):** 封じの前に `EditorSceneManager.LoadSceneAsyncInPlayMode` で既ロードとして読む形が成立した。封じの順序は harness 自身が `SealAddressables` を呼んで作った。封じの後に AssetDatabase / Addressables で読む案は引き続き採れない。fixture での snapshot 作成は 1 回の観測で約 0.3〜0.9 秒。
  - **(ii):** native 層で 2 directory を直接登録し、同じ identity は snapshot を優先し、同時 load、unload と再 load、取消の回収が成立した。既存の `ContentDirectorySession` を 2 つ持てることの証明ではない（`InstallContentDirectory` と `ContentRevisionGate` は 2 件目を拒否したまま）。fixture での一時 build は 1 回の観測で約 21〜25 秒。
  - **type tree の無い directory:** build には `DisableWriteTypeTree | SerializeUnityVersion` の両方が要る。Unity は Editor での登録を例外で拒否し、source へ戻らず失敗が記録された。
  - **実験で測っていないもの:** 実 Editor の異常終了の後の回復と実 process 2 つの衝突（AC9 (b)・AC10 は模擬）、実 initializer の protected config と bootstrap の順序、Play ボタンの経路（実験は batch の EditMode test）、参照先 asset の overlay（G22 のとおり Scene の hash は参照先の一致を保証しない）、異なる build の合成。
  - **旧 Hybrid Play の回顧（`20-variant-checkout-workflow.md` §1 へ program 経由で移す）:** 切断の直接の理由は RET スライスで経路が置換されたことで、不具合の記録は見つからなかった。code から読める失敗の型は、共有 Addressables 設定の一時改変、閉包判定に頼った asset の 2 実体、revision 不一致の警告止まり、2 catalog の探索順の 4 つである。新しい 2 機構の実験には共有設定の書き換えと警告止まりは現れなかった。

### 5.3 配布粒度（Rev3 §7・Rev4 §6）

- **Stage I（暫定）:** 1 つの full revision を最初の要求で丸ごと install。順序・互換・所有・失敗の証明に使う。性能の合格には使わない。
- **Stage II（目標）:** 1 Scene の要求に対し、必要な Scene と依存を含む配布単位（Scene package / cell group / shared package）を索引で特定して取得する。単位は 1 Scene に固定しない。`Region` / `Season` / `Cell group` は host が自分の package 定義を作る場合の例示で、OSM の標準単位にしない。
- Unity の内部 content file を切り貼りする案は不可。BuildPipeline が生成する正規の directory の単位で検証する。Stage II が不成立なら owner に戻し、大きな chunk + 先読みか、全 revision 取得への縮退かを決める。

## 6. 不変条件（Rev4 §7。各 A1 が自分の範囲の物を不変条件として採る）

- I1: 呼び出し側の identity / kind / representation を provider が勝手に変えない。fallback は明示 policy の範囲だけ。
- I2: 供給元から受け取った bytes は、manifest / digest / 互換の検証を通るまで Unity の使用対象にしない。Editor snapshot tier の受け入れ理由は digest ではなく、利用者が開いている Scene の複製であること。record に dirty と、digest 検証を受け入れ理由にしていないことを残す。これは別 revision への fallback ではない（R3 F-03）。
- I3: 認証・ネットワーク・配信形式は lifetime / SceneSystem / BuildSelection に漏らさない。
- I4: 1 つの Scene / asset を誰が所有・解放するかが、snapshot / directory の違いで不明瞭にならない。
- I5: provider の優先度と、候補が利用可能・互換であることを区別する。
- I6: Play の準備段階で全論理 Scene を列挙・download することを必須にしない。
- I7: OSM の実行時契約に SampleGame の Scene 名・Cell 分割・職種・Season・VCS 実装を入れない。
- I8: AssetManagement 単独と BuildSystem 単独の最小機能は、resolver 無しでコンパイル・動作する。
- I9: Editor 開始時と実際のロード時は別の状態。snapshot の採用は現在の由来と identity を検証する。
- I10: Player の protected config、起動時の fail-closed、既存の Addressables 封鎖を、検証なしに弱めない。
- I11（A1）: Play は開いている Scene 自体に影響を与えない。Play 中の unload・変更は複製にだけ及ぶ。対象は、開いている Scene 資産のシリアライズ内容と Dirty。参照先の prefab / material / ScriptableObject は複製せず、I11 の対象外（R3 F-07。Asset も overlay するかは S0-b の scope と非目標で決める）。

## 7. owner 決定（revision 3.2）

| ID | 決定 | 経緯 |
|---|---|---|
| D1 | `app-config.json` に `addressables` を置いて Play 直押しを動かす案は採らない | revision 1、維持 |
| D2 | Framework 側の任意の連携層に置く。core は SampleGame を知らない | revision 1、Rev4 で汎用化を明示 |
| D3 | 互換な候補があれば Build を見せない。無い時だけ明示の回復操作として 1 回促す。再現可能性と選択結果の確認は隠さない | revision 2 で改訂、維持 |
| D4 | **置換:** 編集の反映は「Editor で開いている Scene の Play 前 snapshot」tier で行う。source file からの load は tier にしない。sha 差分で適用対象を決めない。snapshot の機構（§5.2 (i) / (ii)）は S0-b で比較した（両方が技術的な候補として残り、どちらを製品にするかは未決定。§5.2） | revision 1 の「Runtime source overlay」を revision 2 で保留、Rev3 / Rev4 と A1 で置換 |
| D5 | Cloud は後続。本 program では adapter の差込口だけ確認し、tier として実装しない | revision 1、A5 で維持 |
| D6 | 通知は必要な警告だけ。Play のたびの popup は出さない。§4.3 の 3 条件（Play ボタンから起動できる、明示設定を壊さない、失敗と由来を診断できる）は program の必須で S0-c の gate。表示の配置と通知の細則は S0-c の裁量 | revision 1、A10。R3 F-04 で「受け入れ条件にするかを S0-c に任せる」の文を撤回し §4.3 に揃えた |
| D7 | 供給が無い現場では Build を促してよい。source-only Play は供給の無い現場の標準経路として残す。より安い解決（checkout hook の prefetch 等）は S4 の A1 で比較 | revision 1、A8 |
| D8 | VCS 連携は任意の adapter。SVN 実装は host 側か `tools/`。checkout hook から Content の prefetch を呼べる。手動 checkout でも同じ policy | revision 2 で改訂、A2 で追加 |
| D9 | 供給元の選択はロード要求時。Play 前に固定するのは規則と互換基準と bootstrap だけ。待ち時間は契約を変えずに use case ごとの安い予防で防ぐ | Rev3、A2 |
| D10 | 汎用 core を SampleGame を使わない fixture で検証し、SampleGame は最後の integration fixture とする | Rev4、A7（汎用化の是非は問わない。owner の意向） |
| D11 | S0-a（契約）、S0-b（技術実証）、S0-c（Play 入口）は独立で並行 | A3 |

## 8. スライス列

順序は「契約と実装の境界の検査 ‖ 混成の技術実証 ‖ Play 入口の最小実証 → 互換 → 粒度 → Runtime 接続 → 供給元 → Editor workflow と観測」。各スライスは `slice` の HANDOFF を切り、1 スライス 1 ブランチ。

| 順 | スライス（HANDOFF 名の候補） | 答える問い | 上位の問い | risk | gate（次へ進む最低条件） |
|---|---|---|---|---|---|
| S0-a | `GENERIC_CONTENT_REQUEST_CONTRACT`（完了、PR #171。契約は [§13](../../unity/Assets/Docs/Architecture/13-resource-system.md)） | SceneDirector や BuildPurpose が無くても、要求・候補・所有の契約を定義できるか。既存の `IAssetManagement` / `IContentDirectoryBackend` / DIST で何を再利用でき、新しい interface は本当に要るか | Q1、Q2、Q6 | normal | 純粋な契約 + fake provider の Unity 非依存テスト。SampleGame 固有型なし。単独利用の P 検査が閉じる。**resolver 無しの既存 AssetManagement の非退行**と、**SceneDirector 無しの resolver + AssetManagement の利用**を別々に示す（R2 §1 補正）。Object / Prefab を模した小さな例を 1 つ含め、Scene の都合で Asset 全体の API を先に固定しない（R2 M-03）。S0-a が凍結するのは、要求・候補・record・所有の契約と、resolver を AssetManagement の必須依存にしないことまで。`InstallContentDirectory` が 2 件目を拒否する規則は、S0-b が §5.2 の (i) / (ii) を選ぶまで凍結しない（R3 F-02）。LoadRecord は供給元の種別（snapshot / directory / addressables）を持ち、fake provider のテストでも由来の無い成功は合格にしない（R3 F-10） |
| S0-b | `CONTENT_DEMAND_ROUTING_SPIKE`（完了、PR #172。結論は §5.2。実験 code は製品に残さず git 履歴の commit `94b0c4a6` にある） | Scene A（Editor で開いている Scene の snapshot）と Scene B（built directory）を同一 session で読み、所有・unload・cancel・再試行・Play 停止・domain reload 無効を保てるか。§5.2 の (i) / (ii) のどちらが成立するか。旧 Hybrid Play の廃止理由の回顧 | Q4、I4、I11 | high | **SampleGame を使わない独立した 2 Scene fixture** での実機の証拠。LAN は模擬で可。実験項目（R2 F-01）: (1) Unity 標準 Play の Scene 隔離と `RegisterAlreadyLoadedScenes` の再利用を比較対象として実測（owner の複製要求は撤回しない）。(2) `EditorSceneManager.SaveScene(saveAsCopy)` 等の実複製で、Dirty、新規未保存、additive に複数、prefab override、参照先 asset の変更を再現。(3) 複製は別 path / GUID になり得るので、論理 Scene identity と物理 asset の写像を維持し、同名・同 GUID の衝突と二重ロードを避ける証明。(4) 複製の GameObject の寿命、SceneDirector の既登録、owner 台帳、Play 終了時の復元の整合。Play 停止後に元の Scene の Dirty と内容が無傷。(5) Reload Domain / Reload Scene の各設定で比較し、必要なら対応する設定を限定。(6) 失敗した複製と temp asset の cleanup、複数 Editor instance の衝突。(7) **物理的に開いているが未要求の Scene** は `SceneBase` lifecycle を進めないこと。現行 `RegisterAlreadyLoadedScenes()` の自動 `AddScene` と要求時登録を切り分け、bootstrap が必要とする Scene は明示要求として扱う。Unity の `Awake` / `Start` 抑止は合格条件にしない。**凍結条件:** これらが成功するまで (i) / (ii) と既ロード Scene の取り込み方を確定せず、製品 code への統合を始めない。実験 code は製品に残さない。不成立なら D4 の修正を owner に返す |
| S0-c | `EDITOR_PLAY_ENTRY_PROOF` | Runtime を変えずに、Play ボタンから RunPurpose の選択 → 候補の検査 → 明示の source-only / directory-only Play を迷わず起動できるか。明示設定を壊さず、初見が説明可能に始められるか。§4.2 の use case と予防の一覧 | Q5、§4.3 | normal | 編集者・検証者・初見の 3 利用者が迷わず起動でき、policy と結果が Console と観測 UI から追える。§4.3 の必須 3 条件（Play ボタン、明示設定の非破壊、由来の診断）を gate にし、UI の細部は裁量。候補なし（U4 の mode の書き込みか、ブロックか）・明示 override・手動 ledger・Editor に開いている Scene の混入（G18）の各場合を実機で確認。別 revision や互換でない digest / target / schema の判定は S1 の事例で、S0-c の gate にしない（R3 F-08）。Rev2 の S0-a を流用 |
| S1 | [`CONTENT_COMPATIBILITY_SELECTION`](CONTENT_COMPATIBILITY_SELECTION.md)（A3 凍結、2026-11-09。次は Phase B。owner が 2026-11-09 に同書 §2.5 の U-S1-1〜U-S1-7 を裁定。合成の欄は S2、§3.1 の 10・§4.1・U5 は裁定に合わせて直した） | 特定の host 宣言無しで expected identity / digest / target / schema の汎用判定ができるか。`declaration` の内容・表現・graph は host が登録した時だけ比べる述語として追加できるか（U5） | Q3 | normal | 同名異内容（要求が expected identity を持つ時）、壊れた manifest、異なる version、別表現（要求が表現を指定した時）、旧 manifest、別の content set を誤採用しない |
| S2 | `CONTENT_PACKAGE_DISCOVERY` | 要求した logical key がどの正規 package に入るかを、tier ごとの索引と manifest で解決できるか。Stage I から Stage II への形 | Q2、§5.3 | high | gate は「複数 root を Unity に登録できた」ではなく、**1 つの合成単位として作った 2 package** の交互ロード・共有依存・unload・失敗時の復元（R2 F-04）。欠損と共有依存の証拠。Unity の後勝ち探索・依存重複・native 登録を実測。異なる build 同士の合成は後続の別課題。不成立なら縮退判断を owner へ |
| S3 | `RUNTIME_PROVIDER_INTEGRATION` | provider route → install → Unity load → 所有 / unload の接続が、SceneDirector 経由と直接の `IAssetManagement` 経由の両方で成立するか | Q4、Q6 | high | 両経路を別々に確認。Player の非退行。`ContentDirectoryStartup` の Phase D 後はその API に合わせる |
| S4 | `LOCAL_LAN_PROVIDER` | local の installed cache / publish 済み / LAN の取得・検証・利用を段階化できるか。checkout hook と Play 入口の prefetch | Q2、§4.2 | normal | Local hit / miss、LAN timeout、tamper、cancel、immutable revision、cache lease。publish 成功だけで Play 可能と宣言しない |
| S5 | `EDITOR_WORKFLOW_AND_OBSERVABILITY` | 任意の Workset checkout、policy と record の観測 UI、Build の明示回復、menu の整理、README で、Workset の無い利用者も Play できるか | Q5、§4.3 | low | GameView からも観測可。手動 checkout と自動 checkout が同じ policy。SampleGame の streaming case を最後に接続し、共通 core を書き換えずに済む |

- **並行:** S0-a / S0-b / S0-c は独立。S1 は S0-b の結果を受ける。S2 の確定が S3 / S4 の全機能に先行し、S3 を Stage I だけで先行するなら Stage II の未達を gate と文書に明記する。
- **外部依存の gate:** 検証済み content の CI publish は本 program の外（[§18](../../unity/Assets/Docs/Architecture/18-asset-description.md)「CI 組み込み」）。供給がある現場の体験はこれ無しに成立せず、無い間は source-only Play と S5 の明示回復が経路（§10 U9。受け入れ済み）。
- **S0-a から移送した入力（2026-11-08）:** S3: 採用した候補と実際にロードした directory の同一性（record の `SourceBindingVerified` を true にできる条件）、resolver 無しで先にロード済みの同 identity の Scene を `LoadContentSceneAsync` が再利用するときの record、`Full` fallback の実効値を record に写すこと、採用後のロード失敗で probe と候補を呼び手へ残す方法。S3 / S5: `ProviderProbe` は `Rejected` の expected identity を持たないので、pin の食い違いの相手は `Reason` に書かれない限り record に残らない。S4: 要求をまたぐ pin。`IContentProvider.Id` / `SourceKind` の getter は `ContentResolver` が探索の `try` の外で読むので、例外を投げない・null を返さないことを adapter の契約にする（owner 裁定）。S1 以降: 「pin が未充足のまま `Fault`」→ `IntegrityRejected` の組み合わせを固定するテストが無い。asmdef 分割スライス（[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md) D4）: `Runtime/ContentResolution/` を別 asmdef にするか、P 検査の常設化。
- **S0-b から移送した入力（2026-11-08）:** S0-c / S3: 実際の Play ボタン経由で snapshot の load を封じの前に成立させられるかを、実 initializer と同じ流れで fail-closed・由来・所有まで確かめる gate を置く（実験は harness が封じを呼び、protected config と bootstrap の段の順序を通っていない）。S3: 物理的に loaded で未要求の Scene の非参加は未証明。実験の AC5 は隔離の下で `Other` が載らないことを見たもので、隔離しない基準の対照では map にある `Other` を既ロード登録が拾い、map に無ければ loaded のまま登録されなかった。要求時の `AddScene` と本番の `RegisterAlreadyLoadedScenes` の責務の分離を S3 の受け入れ条件から落とさない。S3: AC6 の取消は `IAssetManagement.LoadContentSceneAsync` に直接掛けた。`SceneDirector` の取消が Unity の Scene load に届かない差は未解消。S0-c / S3: 実 Editor の異常終了の残骸と実 process 2 つの衝突は未証明。S2: 参照先 asset の overlay と、異なる build・共有依存の重複。機構 (ii) を製品にする場合の別スライス: `InstallContentDirectory` の単一 install、`ContentRevisionGate` の単一登録、revision lease、close の順序、token の所有。製品の `CompleteContentDirectoryPlayStop` は drain を待たない。**外部レビューの提案（決定ではない）:** 計測の差と (ii) の製品変更の量から、まず (i) の実 bootstrap 接続を試し、成立しない制約が出たら (ii) を検討するのが低コスト。方式の選定は owner が S0-c / S3 の A1 で行う。
- **全 EditMode 回帰の受け入れ条件の書き方（S0-a の教訓）:** `pwsh tools/run-tests.ps1` は `-nographics` の VFX テスト 25 件が Inconclusive になり、`step.json` は常に `failed` になる。「合格」とだけ書かず、「XML の failed 0 と root result `Passed`」または `-WithGraphics` を A1 で書く。
- **BuildSystem 側の入力:** `BuildContentDirectoryIntegrationTests.ScenePrefabTextureAndTwoRepresentationsBuildAndMoveAsOneDirectory` は、他の Unity が同時に動く条件で失敗する。S0-a では `Directory.Move` の `Access denied` と 360 秒 Timeout が出て、他の Unity を止めた実行で通った。原因は未確定。
- **誤った合格を除外する:** 「SampleGame の Spring を Play できた」だけで汎用 resolver の成立としない。「SceneDirector から呼べた」だけで `IAssetManagement` 単独利用の成立としない。「Editor に Scene A を開いていた」だけで handle の所有・release の成立としない。「複数 directory を登録できた」だけで異版の安全な合成としない。「最初の miss で full revision を取得した」だけで Stage II の成立としない。「record に revision がある」だけで未保存状態の厳密再現としない。

## 9. 検証計画（各 A1 の受け入れ条件の素材。Rev4 §9 と Rev3 §12 を統合）

| ID | プローブ | 合格条件 |
|---|---|---|
| G01 | 2 Scene fixture（`Entry` と `Next`）。親子・季節・距離なし | Entry は Editor の snapshot、Next は local の built directory から。ゲーム固有の policy 不要 |
| G02 | 多段 Scene fixture（`Menu` → `Level` → `Result`） | 要求順を resolver が決めず、要求時に解決。未要求 Scene を先にロード・download しない |
| G03 | AssetManagement 単独利用 | 2 件に分ける。(a) resolver 無しの既存 AssetManagement が退行しない。(b) SceneDirector 無しの resolver + AssetManagement で Scene / Object を要求し owner を解放できる。(a) だけで resolver の汎用性を証明したとしない |
| G04 | BuildSystem 単独利用 | Bootstrap / SceneDirector / Editor Play UI 無しで publish 成果物を作れる |
| G05 | 開いていない `.unity` が local にある | 未 build の source を local tier として採用しない |
| G06 | 開いている Scene（Dirty・additive を含む）を要求 | snapshot を利用し、取得・ロードの二重計上なし。開いている Scene に影響が無い（I11） |
| G07 | SceneDirector 統合あり / なし | 同じ要求・候補・所有の契約で動く |
| G08 | contentSet 名が同じ別 digest / target / schema / `declaration` | 互換でない物を黙って採用しない |
| G09 | Local miss → LAN hit | LAN から verify / install 後に使用。Cloud 未設定でも成立 |
| G10 | 複数 Content Directory が同じ logical key を持つ | Unity の後勝ち探索による想定外の切替が無い |
| G11 | 異なる directory 間で共有依存 | 重複メモリ・native 登録・unload 順序を測定し、採否を確定 |
| G12 | Runtime でロード済みの Scene と Editor の snapshot | 由来を誤認せず、identity の重複を拒否 / 再利用できる |
| G13 | 不正 hash・途中切断・budget over | fail-closed、cache 汚染なし、lease / token 回収 |
| G14 | 後続をロードせずに Play 停止 | 未要求 resource を確保せず、既存 resource を正しく解放 |
| G15 | native load 中の取消と Scene unload | 遅れて完了した token を回収。unregister / delete を早めない |
| G16 | domain reload 無効 / Play 停止 / Play 再入 | 残留 provider / loaded scene / ledger が無く、同じ目的で再起動できる |
| G17 | SceneSystem を使うが streaming policy 無し | 必要 Scene と親子寿命だけで成立。Cell 概念ゼロ |
| G18 | 手動 Delivery、明示 `runtimeMode`、外部環境変数、Variant profile | 人間の明示選択を黙って壊さない。明示の directory-only Play で Editor に Scene が開いていても、設定を変えず、source の混入を無言で許さない（現行 Verify Play の保護を保つ） |
| G19 | cache warm / local cold / LAN cold | 検出・転送・hash・install・register・native load を分けて計測 |
| G20 | SampleGame の streaming case（**最後**） | 既存の Cell 分割・world policy を adapter で接続でき、共通 core を書き換えずに済む |
| G21 | Editor に Scene A が開いていて、同 identity の directory Scene A がある | snapshot 優先で **1 個だけ**実行される。Play 停止後に元 Scene が無傷 |
| G22 | Scene A は Dirty、参照 prefab は別途変更済み | record が、snapshot の hash は当該 Scene 資産の出所追跡であり、参照先 asset の一致を保証しない、と説明できる。参照先の漂移を検出する機能は合格条件にしない（R3 F-07） |
| G23 | Scene A / B を additive で開き、SceneDirector は A だけ要求 | B は物理的にロード済みでも、要求なしに `SceneBase` lifecycle へ進まず、OSM scene owner を取らない。Unity `Awake` / `Start` の実行抑止は求めない。bootstrap に必須の Scene があれば別の明示要求として記録する |
| G24 | 同 revision を名乗る 2 package が異なる shared prefab bytes を持つ | 個別 hash が通っても合成を拒否するか、検証済みの共有依存の戦略で解決する |
| G25 | 初回要求で full revision の download 中に別 Scene を要求 | 単一の install に合流し、取り消しても半端な install を公開しない |
| G26 | Enter Play Mode Settings の Scene Reload / Domain Reload の有無 | 同じ由来契約が維持されるか、対応外の設定を診断できる |

G01 / G02 は SampleGame の Scene graph の名前を変えた物であってはならない。独立した小さな検証入力とする。G03 / G04 は型と動作の両方で検証する。

## 10. 未決事項（各スライスの A1 へ。§1 の Q1〜Q6 と区別するため U 番号。R3 F-05）

R3 の推奨を各項に併記した。推奨は owner 決定の置き換えではなく、A1 の初稿が最初に検討する案である。

- **U1（S0-a）:** 新しい interface の範囲。二つに分ける。(1) resolver の表面: 小さな任意の表面が要る。`IAssetManagement` は logical key と表現で既に入っている 1 つの directory から読むだけで、provider の探索も record も返さない。全 load API に探索を入れる案 B は Q6 に反する。S0-a の合格を「新しい型が 0」に置かない。(2) directory の基数: `InstallContentDirectory` の 2 件目拒否は resolver の型とは別の既存契約。§5.2 (ii) が勝った時だけ製品コードで変える。その製品変更は S0-b の結論の直後に切るスライスとし、S3 の Phase B より前に置き場を決める。**S0-b の結果:** (ii) は native 層で成立したが勝者は選んでいないので、2 件目拒否の変更はまだ切らない（§5.2、§8 の移送）。
- **U2（S0-b）:** snapshot の機構 (i) / (ii)。勝者は選ばない。脱落条件: (i) が封じの後に AssetDatabase / Addressables で読むなら脱落（§5.2）。(ii) は複数 directory 登録が前提で U1 (2) と結び付き、publisher の `Full` 限定も比較のコストに入れる。**S0-b の結果:** どちらも脱落しなかった（§5.2）。選定は S0-c / S3 の A1 へ送る。
- **U3（S0-b）:** snapshot を Runtime に渡す形。推奨は Editor 専用 bridge。`content:` の予約 key には入れない。protected 照合が `content:runtimeMode` から `probeToken` までを Player の baked 値と突き合わせるので、snapshot の path をこの名前空間に置くと I10 の保護 key と同じ形になる。Player にこの bridge は無い。**S0-b の結果:** 実験は `content:` の key を使わず harness の中で渡した（AC12）。bridge の形は未決定で S0-c / S3 が決める。
- **U4（S0-c、S1）:** §4.7「known-good の自動適用はしない」と Editor adapter が config を書く行為の境界。二つの行為に分ける。(a) key がどの層にも無い時、Editor の transaction が `content:runtimeMode=addressables` をそのセッションに書くことは verified pair の自動適用ではない。`addressables` は既存の mode で directory を選ばない。Console に 1 行残す。D1 とは別で、コミットされた既定値にはしない（R3 採用）。(b) adapter が `content:installedRevisionPath` と `content:manifestSha256` の pair を書くこと。R3 は「§4.7 が拒んだ自動適用そのもの。書かない」と推奨したが、**本書は採らない**。現行の Verify Play の `Bridge` 段と Delivery の「Use For Next Play」は、install と verify を済ませた pair を 1 回の Play の ledger で書く既存の経路で、これを禁じると directory Play に入る手段が無くなる。§4.7 が拒むのは Runtime が未指定時に known-good を見ることで、Editor adapter が利用者の policy の下で候補を選び、その transaction で再検証し、1 回の Play の ledger で書き、record に残す行為は別。この境界の文言を S0-c の A1 が §18 §4 向けに書き、S1 が互換判定と接続する。
- **U5（S1）:** 互換 fingerprint。汎用の一致は contentSet、target、Unity version、root schema を常に比べ、revision は host が Play 前の基準で固定した時だけ、file digest は要求の expected identity がある時だけ全欄で比べる（S1 の U-S1-6、owner 裁定 2026-11-09。当初は revision と digest も常に比べる列だった）。`declaration` は optional で validation が見ず、空の name 同士が一致してしまうので汎用の最小集合に入れない。`declaration.name`、表現、graph は host が登録した時だけ比べる述語にする。表現は要求元の data なので汎用側は中身を解釈しない。graph version の追加は反例で判定。
- **U6（S2）:** Stage II の索引の置き場。答えるのは早い。制約: 索引は BuildSystem が正規 directory と一緒に出す bytes にする。Runtime が成果物を走査して作らない。Editor にしか無い sidecar にしない。Local と LAN が同じ bytes を見る。
- **U7（S4）:** checkout hook の形。VCS adapter は resolver の内部を参照しない。`tools/` の script か host が prefetch の公開入口を呼び、手動 checkout も同じ入口。SVN の型は OSM に入れない（D8）。
- **U8（S5）:** menu root と観測 UI。契約ではない。観測の置き場は Editor adapter。二重 root の統合を S0 の gate にしない。
- **U9（受け入れ済み、閉じる）:** CI publish の供給は program の外。供給が無い間の経路は source-only Play と S5 の明示回復（§8）。CI の所有者はこの program の未決に残さない（R3 F-09）。
- **U10（後続、対象外）:** Cloud tier の実装、Player での要求時解決、streaming の保温としての先読みの最適化。§4.2 の RunPurpose 相当の prefetch は program 内の待ち時間対策で、U10 の先読みの言い換えではない。
- **R2 の中優先 M-01〜M-08 の行き先:** M-01（並行は A1 まで。製品統合は S0-a の契約凍結後）→ 各 S0 の HANDOFF の停止規則。M-02（Stage I の in-flight 合流・cancel・進捗・timeout）→ S4。M-03 → S0-a（反映済み）。M-04（source-only Play は成立条件を満たす利用者への選択肢で、必ず救済できる手段ではない）→ S0-c と S5。M-05（snapshot tier は Scene 限定か、Asset も overlay するか）→ S0-b の scope と非目標（I11 の対象は Scene 資産に限定済み）。M-06（SceneResourceMap / UICommon の起動時特例は host の policy として登録し、generic の `ContentRequest` に入れない）→ S0-a。M-07 → §4.3 に反映。M-08（use case の例示を `Menu → Game` 等から始め、Spring / SVN は host の例とする）→ S0-c の A1 の文言。

## 11. 計画レビュー記録

- **R1（2026-11-08、外部、owner 経由）:** 対象 revision 1。Blocker 4 件（RunPurpose と Workset の分離、sha 差分は編集意図ではない、hybrid の成立性を先に実証、一致と互換の未定義）と Major 8 件。全て採用し revision 2 に反映。
- **Rev3（2026-11-08、owner 側の提案）:** revision 2 の「Play 前に全 Scene の供給元を確定」を撤回し、要求時解決、Editor で開いている Scene の最優先、Workset は checkout 支援のみ、Stage I / II の分離、「混ぜてはいけないもの」10 件、検証 T01〜T22 を提示。本 revision は §3.1・§4.1・§5.3・§9 に取り込んだ。Rev3 の「Cloud を第 4 tier として完成条件に含める」は A5 で採らない。
- **Rev4（2026-11-08、owner 側の提案）:** Rev3 の SampleGame 依存（Cell / Season / 距離 streaming / RunPurpose 必須 / SceneDirector 唯一の呼び手）を API と合格条件から外し、汎用の要求モデル、任意の resolver、不変条件 I1〜I10、SampleGame を使わない fixture、スライス列を提示。本 revision の §1・§3・§4.4・§5.1・§6・§8・§9 の骨格。
- **R2（2026-11-08、独立計画レビュー、owner 経由）:** 対象 revision 3（commit `a632dd8`）。判定は「A0 の問題設定と構成を支持。S0-a / S0-b / S0-c の A1 着手を支持。S0-b の方式選択、Editor 入口の安全契約、Stage II の合成互換性を実装既定に凍結するのは尚早」。高優先 F-01（複製と非破壊は別契約。S0-b の実験項目 6 件）、F-02（snapshot の hash と再現可能性の同一視）、F-03（明示 `runtimeMode` で設定を書き換えないことと由来を検査しないことは別）、F-04（同 revision は合成可能の証拠ではない）を全て採用し、§3・§4.1・§4.3・§5.2・§8・§9 に反映。中優先 M-01〜M-08 は §10 の行き先に従って各 A1 へ。全体の再計画は不要との判断に同意。
- **R3（2026-11-08、独立計画レビュー、Cursor 上の Grok 4.7、owner 経由。branch `cursor/play-content-plan-review-r3-5cc8` の commit `8b2dbbf5` にあった付属 file は要約を本項へ移して削除した）:** 対象 revision 3.1（commit `7385e7e`）。判定は「A0 の問題設定、要求時解決、案 A、Traceable までを必須にすること、S0 の分離を支持。全体の再計画は不要。R2 の F-01〜F-04 は再開しない。F-01〜F-04 を本文に入れた版から S0 の A1 を始める」。採否: F-01（§4.1 の revision 規則は package 合成に限る）採用。F-02（単一 directory の規則は S0-a が凍結しない）採用。F-03（I2 に snapshot の受け入れ理由）採用。F-04（D6 を §4.3 に揃える）採用。F-05（§10 を U 番号に）採用。F-06（S0-b の上位の問いを Q4・I4・I11 に）採用。F-07（I11 の対象を Scene 資産に限定、G22 を説明可能性に）採用。F-08（S0-c の gate から S1 の事例を外す）採用。F-09（U9 を閉じる）採用。F-10（LoadRecord は由来を持つ）採用。F-11（§5.2 (i) に封じの時点）採用。U1〜U3・U5〜U8・U10 の推奨を §10 に併記。**U4 の後半（adapter が pair を書くことを禁じる）は不採用**: 禁じると directory Play に入る手段が無くなり、現行 Verify Play の `Bridge` と Delivery の「Use For Next Play」が既にその経路である。§4.7 が拒むのは Runtime が未指定時に known-good を見ることで、policy の下で選び、transaction で再検証し、1 回の Play の ledger で書き、record に残す行為は別と整理した（§10 U4）。U4 の前半（`addressables` の書き込みは自動適用ではない）は採用。
- **owner の回答（2026-11-08、revision 3 の質問 Q1〜Q10 に対して）:** A1 開いている Scene は Play 前に退避し Play は複製を使う（D4、I11）。A2 待ち時間は契約を変えずに use case ごとの安い予防で防ぎ、checkout hook で Content も落とす（D8、D9、§4.2）。A3 S0-c は並行（D11）。A5 Cloud は後続、差込口だけ（D5）。A6 ASSET_BUILDSYSTEM_INDEPENDENCE との関係は境界の正本をあちらとする 1 行で足りる（§0 関連）。A7 汎用化の是非は問わない（D10）。A8 供給の無い現場は source-only Play と明示回復で、より安い解決は S4 で比較（D7）。A9 Rev3 を共有。A10 UX 要求は明記するが受け入れ条件にするかは任せる（D6、§4.3）。

## 12. この文書の終了

本書は program の地図で、各スライスの HANDOFF が切られるごとに §8 の行をその HANDOFF へのリンクに置き換える。S0-b の結果で採れない方式を絞り、全スライスの Phase D が終わるか owner が打ち切った時点で、§3 の概念、§4 の要求、§6 の不変条件、§7 の決定のうち恒久的な物を §0 の harvest 先へ移し、本書を削除する。Stage II が不成立の時は、owner が要求を改訂する明示の判断をした時だけ縮退を完成扱いにする。
