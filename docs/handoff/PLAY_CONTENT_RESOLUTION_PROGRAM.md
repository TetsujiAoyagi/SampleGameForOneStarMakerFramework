# 要求時の Content 解決と Editor Play の入口（Phase A0 planning packet、revision 3）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。

- type: `program`（A0 の planning packet。実装スライスではない。§8 のスライス列の各 A0 への入力）
- status: `A`（A0 revision 3。revision 2 は外部の計画レビュー R1 で問いを上位へ戻した版。その後 owner 側で作られた提案 Rev3（要求時解決、Editor で開いている Scene の最優先）と Rev4（SampleGame への依存を API と合格条件から外す）を受け、owner の回答 A1〜A10（§11）で確定した方針を本 revision に反映した。A1 は未着手。**実装キューではない**）
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

## 3. 概念モデル（A1 で API の形を決める。型名は仮称）

Rev4 の最小の汎用モデルを採る。**既存の `IAssetManagement`、`IContentDirectoryBackend`、BuildSystem の入口で足りる所に重複する interface を増やさない。** これは S0-a が最初に判定する。

- **ContentRequest（要求）:** `LogicalKey`（Scene なら Scene identity）、`Kind`（最初は Scene。他種別へ拡張できる形）、`Representation`（要求元が与える data。resolver の enum にしない）、互換の制約、cancellation、診断の相関。SceneDirector の状態、親子、Season、SceneResourceMap、VCS の知識を入れない。
- **Provider / Availability（供給候補）:** 「この要求と policy に使用可能な候補があるか」を、検証される候補の identity / immutable digest / manifest reference / metadata として返す。任意の path をそのまま信用しない。探す段と Unity のロードを分ける。候補の取得・install は供給形態の adapter が担う。
- **ResolutionPolicy（規則。Play 前に固定）:** provider の順序、採用条件、互換基準、timeout / cancellation、明示 override、Editor snapshot の採用規則、bootstrap の基準。**将来どの Scene が要求されるかを列挙しない。** immutable。
- **ResolutionResult / LoadRecord（1 要求の決定と実績）:** 得た候補、実効表現、所有、理由。実際にどの version / digest / provider で成功し解放されたか。**予定（policy）と実績（record）を別の型にする。** 未保存 Editor 状態の完全再現は保証せず、record にそう残す。
- **Editor snapshot tier（owner 決定 A1）:** Editor で開いている Scene は、**Play 前に退避した複製**を Play 中の実体とし、開いている Scene 自体には影響を与えない。複製は要求時に「既にある Scene」の特殊な候補として最優先で採用する。`Assets/` に source file があるだけでは候補にしない。Dirty は採用し、未保存である事実を record に残す。複製の機構は S0-b で比較する（§5.2）。
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
10. ネットワーク失敗による次 tier への探索と、非互換 revision への暗黙 fallback。

## 4. 要求（A1 の入力）

### 4.1 要求時解決の契約

- 供給元の選択は**ロード要求時**に行う。Play 前は RunPurpose 相当の互換基準と policy と、bootstrap が必須とする graph / config / UICommon の取得可能性だけを確定する。
- `not found`・未接続・timeout は次 tier を試す。hash 不正・version 不整合・権限拒否は記録し、**同じ expected digest の正規コピーで修復できる時だけ**下位 tier へ進む。別 revision・別表現へ黙って切り替えない。
- 必須 Scene の失敗は fail-closed。任意 Scene（距離 streaming 等）の失敗の扱いは呼び出し側の policy が決め、resolver は欠損を成功にしない。
- 複数供給元の**同じ revision**から必要な物を取るのが標準。異なる revision の寄せ集めは、versioned composition の契約を別に実証するまで拒否する。
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

Rev2 の UX 要求は、core ではなく Editor adapter の**要求**として残す。受け入れ条件にするかは S0-c の A1 が決める。

- Play ボタンが入口。`ExitingEditMode` で `content:runtimeMode` がどの層にも無い時だけ介入し、どこかの層に値があれば意図した設定として通す。介入は取り消し → 準備 → 再 Play の transaction として設計し、二重 Play・再入・crash 後の ledger 残留を防ぐ。
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

- **(i) Scene の複製を一時 asset として置き、AssetDatabase / `EditorSceneManager` から読む。** 軽い。ただし directory 封じの下で Editor の load 経路を通すので、Rev2 の B-03 と同じ所有・封じ・台帳の問題が残る。
- **(ii) 複製から小さな一時 Content Directory を build し、通常の directory として登録して読む。** Runtime は directory しか見ないので hybrid の問題が消える。代償は Play 前の build 時間（Scene 数件）と、**複数 directory の登録**が必要になること（§2.4 の未実証）。publisher が `Full` 限定なのも触る。
- どちらでも、複製は Dirty の bytes を含み、その hash を record に残すので「未保存で再現不能」が「snapshot の hash で再現可能」に変わる。Unity の Play mode が終了時に開いていた Scene を復元する既存挙動との関係は S0-b で確認する。

### 5.3 配布粒度（Rev3 §7・Rev4 §6）

- **Stage I（暫定）:** 1 つの full revision を最初の要求で丸ごと install。順序・互換・所有・失敗の証明に使う。性能の合格には使わない。
- **Stage II（目標）:** 1 Scene の要求に対し、必要な Scene と依存を含む配布単位（Scene package / cell group / shared package）を索引で特定して取得する。単位は 1 Scene に固定しない。`Region` / `Season` / `Cell group` は host が自分の package 定義を作る場合の例示で、OSM の標準単位にしない。
- Unity の内部 content file を切り貼りする案は不可。BuildPipeline が生成する正規の directory の単位で検証する。Stage II が不成立なら owner に戻し、大きな chunk + 先読みか、全 revision 取得への縮退かを決める。

## 6. 不変条件（Rev4 §7。各 A1 が自分の範囲の物を不変条件として採る）

- I1: 呼び出し側の identity / kind / representation を provider が勝手に変えない。fallback は明示 policy の範囲だけ。
- I2: 受信 bytes は manifest / digest / 互換の検証を通るまで Unity の使用対象にしない。
- I3: 認証・ネットワーク・配信形式は lifetime / SceneSystem / BuildSelection に漏らさない。
- I4: 1 つの Scene / asset を誰が所有・解放するかが、snapshot / directory の違いで不明瞭にならない。
- I5: provider の優先度と、候補が利用可能・互換であることを区別する。
- I6: Play の準備段階で全論理 Scene を列挙・download することを必須にしない。
- I7: OSM の実行時契約に SampleGame の Scene 名・Cell 分割・職種・Season・VCS 実装を入れない。
- I8: AssetManagement 単独と BuildSystem 単独の最小機能は、resolver 無しでコンパイル・動作する。
- I9: Editor 開始時と実際のロード時は別の状態。snapshot の採用は現在の由来と identity を検証する。
- I10: Player の protected config、起動時の fail-closed、既存の Addressables 封鎖を、検証なしに弱めない。
- I11（A1）: Play は開いている Scene 自体に影響を与えない。Play 中の unload・変更は複製にだけ及ぶ。

## 7. owner 決定（revision 3）

| ID | 決定 | 経緯 |
|---|---|---|
| D1 | `app-config.json` に `addressables` を置いて Play 直押しを動かす案は採らない | revision 1、維持 |
| D2 | Framework 側の任意の連携層に置く。core は SampleGame を知らない | revision 1、Rev4 で汎用化を明示 |
| D3 | 互換な候補があれば Build を見せない。無い時だけ明示の回復操作として 1 回促す。再現可能性と選択結果の確認は隠さない | revision 2 で改訂、維持 |
| D4 | **置換:** 編集の反映は「Editor で開いている Scene の Play 前 snapshot」tier で行う。source file からの load は tier にしない。sha 差分で適用対象を決めない。snapshot の機構（§5.2 (i) / (ii)）は S0-b で比較 | revision 1 の「Runtime source overlay」を revision 2 で保留、Rev3 / Rev4 と A1 で置換 |
| D5 | Cloud は後続。本 program では adapter の差込口だけ確認し、tier として実装しない | revision 1、A5 で維持 |
| D6 | 通知は必要な警告だけ。Play のたびの popup は出さない。UX 要求は Editor adapter の要求として残し、受け入れ条件にするかは S0-c が決める | revision 1、A10 |
| D7 | 供給が無い現場では Build を促してよい。source-only Play は供給の無い現場の標準経路として残す。より安い解決（checkout hook の prefetch 等）は S4 の A1 で比較 | revision 1、A8 |
| D8 | VCS 連携は任意の adapter。SVN 実装は host 側か `tools/`。checkout hook から Content の prefetch を呼べる。手動 checkout でも同じ policy | revision 2 で改訂、A2 で追加 |
| D9 | 供給元の選択はロード要求時。Play 前に固定するのは規則と互換基準と bootstrap だけ。待ち時間は契約を変えずに use case ごとの安い予防で防ぐ | Rev3、A2 |
| D10 | 汎用 core を SampleGame を使わない fixture で検証し、SampleGame は最後の integration fixture とする | Rev4、A7（汎用化の是非は問わない。owner の意向） |
| D11 | S0-a（契約）、S0-b（技術実証）、S0-c（Play 入口）は独立で並行 | A3 |

## 8. スライス列

順序は「契約と実装の境界の検査 ‖ 混成の技術実証 ‖ Play 入口の最小実証 → 互換 → 粒度 → Runtime 接続 → 供給元 → Editor workflow と観測」。各スライスは `slice` の HANDOFF を切り、1 スライス 1 ブランチ。

| 順 | スライス（HANDOFF 名の候補） | 答える問い | 上位の問い | risk | gate（次へ進む最低条件） |
|---|---|---|---|---|---|
| S0-a | `GENERIC_CONTENT_REQUEST_CONTRACT` | SceneDirector や BuildPurpose が無くても、要求・候補・所有の契約を定義できるか。既存の `IAssetManagement` / `IContentDirectoryBackend` / DIST で何を再利用でき、新しい interface は本当に要るか | Q1、Q2、Q6 | normal | 純粋な契約 + fake provider の Unity 非依存テスト。SampleGame 固有型なし。単独利用の P 検査が閉じる |
| S0-b | `CONTENT_DEMAND_ROUTING_SPIKE` | Scene A（Editor で開いている Scene の snapshot）と Scene B（built directory）を同一 session で読み、所有・unload・cancel・再試行・Play 停止・domain reload 無効を保てるか。§5.2 の (i) / (ii) のどちらが成立するか。旧 Hybrid Play の廃止理由の回顧 | Q3、Q4、D4 | high | **SampleGame を使わない独立した 2 Scene fixture** での実機の証拠。LAN は模擬で可。Dirty、依存 prefab だけの変更、別表現、同名 entry の衝突を投入。実験 code は製品に残さない。不成立なら D4 の修正を owner に返す |
| S0-c | `EDITOR_PLAY_ENTRY_PROOF` | Runtime を変えずに、Play ボタンから RunPurpose の選択 → 候補の検査 → 明示の source-only / directory-only Play を迷わず起動できるか。明示設定を壊さず、初見が説明可能に始められるか。§4.2 の use case と予防の一覧 | Q5、§4.3 | normal | 編集者・検証者・初見の 3 利用者が迷わず起動でき、policy と結果が Console と観測 UI から追える。候補なし・別 revision・明示 override・手動 ledger の各場合を実機で確認。Rev2 の S0-a を流用 |
| S1 | `CONTENT_COMPATIBILITY_SELECTION` | 特定の host 宣言無しで expected identity / digest / target / schema / `declaration` の内容の汎用判定ができるか。host の policy を追加できるか | Q3 | normal | 同名異内容、壊れた manifest、異なる version、別表現、旧 manifest を誤採用しない |
| S2 | `CONTENT_PACKAGE_DISCOVERY` | 要求した logical key がどの正規 package に入るかを、tier ごとの索引と manifest で解決できるか。Stage I から Stage II への形 | Q2、§5.3 | high | 1 package / 複数 package / 欠損と共有依存の証拠。Unity の後勝ち探索・依存重複・native 登録を実測。不成立なら縮退判断を owner へ |
| S3 | `RUNTIME_PROVIDER_INTEGRATION` | provider route → install → Unity load → 所有 / unload の接続が、SceneDirector 経由と直接の `IAssetManagement` 経由の両方で成立するか | Q4、Q6 | high | 両経路を別々に確認。Player の非退行。`ContentDirectoryStartup` の Phase D 後はその API に合わせる |
| S4 | `LOCAL_LAN_PROVIDER` | local の installed cache / publish 済み / LAN の取得・検証・利用を段階化できるか。checkout hook と Play 入口の prefetch | Q2、§4.2 | normal | Local hit / miss、LAN timeout、tamper、cancel、immutable revision、cache lease。publish 成功だけで Play 可能と宣言しない |
| S5 | `EDITOR_WORKFLOW_AND_OBSERVABILITY` | 任意の Workset checkout、policy と record の観測 UI、Build の明示回復、menu の整理、README で、Workset の無い利用者も Play できるか | Q5、§4.3 | low | GameView からも観測可。手動 checkout と自動 checkout が同じ policy。SampleGame の streaming case を最後に接続し、共通 core を書き換えずに済む |

- **並行:** S0-a / S0-b / S0-c は独立。S1 は S0-b の結果を受ける。S2 の確定が S3 / S4 の全機能に先行し、S3 を Stage I だけで先行するなら Stage II の未達を gate と文書に明記する。
- **外部依存の gate:** 検証済み content の CI publish は本 program の外（[§18](../../unity/Assets/Docs/Architecture/18-asset-description.md)「CI 組み込み」）。供給がある現場の体験はこれ無しに成立せず、無い間は source-only Play と S5 の明示回復が経路（§10 Q9）。
- **誤った合格を除外する:** 「SampleGame の Spring を Play できた」だけで汎用 resolver の成立としない。「SceneDirector から呼べた」だけで `IAssetManagement` 単独利用の成立としない。「Editor に Scene A を開いていた」だけで handle の所有・release の成立としない。「複数 directory を登録できた」だけで異版の安全な合成としない。「最初の miss で full revision を取得した」だけで Stage II の成立としない。「record に revision がある」だけで未保存状態の厳密再現としない。

## 9. 検証計画（各 A1 の受け入れ条件の素材。Rev4 §9 と Rev3 §12 を統合）

| ID | プローブ | 合格条件 |
|---|---|---|
| G01 | 2 Scene fixture（`Entry` と `Next`）。親子・季節・距離なし | Entry は Editor の snapshot、Next は local の built directory から。ゲーム固有の policy 不要 |
| G02 | 多段 Scene fixture（`Menu` → `Level` → `Result`） | 要求順を resolver が決めず、要求時に解決。未要求 Scene を先にロード・download しない |
| G03 | AssetManagement 単独利用 | SceneDirector / BuildSystem / Workset 無しで Scene / Object API が動く。resolver は optional |
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
| G18 | 手動 Delivery、明示 `runtimeMode`、外部環境変数、Variant profile | 人間の明示選択を黙って壊さない |
| G19 | cache warm / local cold / LAN cold | 検出・転送・hash・install・register・native load を分けて計測 |
| G20 | SampleGame の streaming case（**最後**） | 既存の Cell 分割・world policy を adapter で接続でき、共通 core を書き換えずに済む |

G01 / G02 は SampleGame の Scene graph の名前を変えた物であってはならない。独立した小さな検証入力とする。G03 / G04 は型と動作の両方で検証する。

## 10. 未決事項（各スライスの A1 へ）

- **Q1（S0-a）:** 既存の `IAssetManagement` / `IContentDirectoryBackend` / DIST の入口で足りる範囲と、新しい interface が要る範囲の切り分け。
- **Q2（S0-b）:** snapshot の機構 (i) / (ii)。(ii) なら複数 directory の登録が前提になり、S2 の問いと結合する。
- **Q3（S0-b）:** snapshot を Runtime に渡す形。config の `content:` 予約 key か、Editor 専用 bridge か。Player の protected key に入れない。
- **Q4（S1）:** §4.7「known-good の自動適用はしない」と Editor adapter が policy で pair を書く行為の境界。案: Runtime の文言は変えず、policy と record を Console と観測 UI に出すので黙ってはいない、と §18 §4 に書く。
- **Q5（S1）:** 互換 fingerprint の最小集合。contentSet / target / revision / digest / `declaration` の内容 / Unity version / root schema を最小とし、graph version の追加は反例で判定。
- **Q6（S2）:** Stage II の配布単位と索引。build 時に package index を出すか、manifest を拡張するか、Editor 用 sidecar か。
- **Q7（S4）:** checkout hook から Content を落とす形。VCS adapter が resolver の prefetch API を呼ぶのか、tools の script か。
- **Q8（S5）:** `OneStarMaker/` と `Tools/OSM/` の二重 root を 1 つにするか。観測 UI の最小の置き場。
- **Q9（program）:** CI publish の供給の所有と順序。供給が無い間の経路（source-only Play と明示回復）を受け入れるか。
- **Q10（後続、対象外）:** Cloud tier の実装、Player での要求時解決、先読みの最適化。

## 11. 計画レビュー記録

- **R1（2026-11-08、外部、owner 経由）:** 対象 revision 1。Blocker 4 件（RunPurpose と Workset の分離、sha 差分は編集意図ではない、hybrid の成立性を先に実証、一致と互換の未定義）と Major 8 件。全て採用し revision 2 に反映。
- **Rev3（2026-11-08、owner 側の提案）:** revision 2 の「Play 前に全 Scene の供給元を確定」を撤回し、要求時解決、Editor で開いている Scene の最優先、Workset は checkout 支援のみ、Stage I / II の分離、「混ぜてはいけないもの」10 件、検証 T01〜T22 を提示。本 revision は §3.1・§4.1・§5.3・§9 に取り込んだ。Rev3 の「Cloud を第 4 tier として完成条件に含める」は A5 で採らない。
- **Rev4（2026-11-08、owner 側の提案）:** Rev3 の SampleGame 依存（Cell / Season / 距離 streaming / RunPurpose 必須 / SceneDirector 唯一の呼び手）を API と合格条件から外し、汎用の要求モデル、任意の resolver、不変条件 I1〜I10、SampleGame を使わない fixture、スライス列を提示。本 revision の §1・§3・§4.4・§5.1・§6・§8・§9 の骨格。
- **owner の回答（2026-11-08、本 revision の質問 Q1〜Q10 に対して）:** A1 開いている Scene は Play 前に退避し Play は複製を使う（D4、I11）。A2 待ち時間は契約を変えずに use case ごとの安い予防で防ぎ、checkout hook で Content も落とす（D8、D9、§4.2）。A3 S0-c は並行（D11）。A5 Cloud は後続、差込口だけ（D5）。A6 ASSET_BUILDSYSTEM_INDEPENDENCE との関係は境界の正本をあちらとする 1 行で足りる（§0 関連）。A7 汎用化の是非は問わない（D10）。A8 供給の無い現場は source-only Play と明示回復で、より安い解決は S4 で比較（D7）。A9 Rev3 を共有。A10 UX 要求は明記するが受け入れ条件にするかは任せる（D6、§4.3）。

## 12. この文書の終了

本書は program の地図で、各スライスの HANDOFF が切られるごとに §8 の行をその HANDOFF へのリンクに置き換える。S0-b の結果で採れない方式を絞り、全スライスの Phase D が終わるか owner が打ち切った時点で、§3 の概念、§4 の要求、§6 の不変条件、§7 の決定のうち恒久的な物を §0 の harvest 先へ移し、本書を削除する。Stage II が不成立の時は、owner が要求を改訂する明示の判断をした時だけ縮退を完成扱いにする。
