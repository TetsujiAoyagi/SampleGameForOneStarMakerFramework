# 部分制作のための実行環境の構成と Play（Phase A0 planning packet、revision 2）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。

- type: `program`（A0 の planning packet。実装スライスではない。§8 のスライス列の各 A0 への入力）
- status: `A`（A0 revision 2。revision 1 は外部の計画レビュー R1（2026-11-08、§11）で「問いが実現方法に踏み込んでいる」と差し戻され、owner も同意した。本 revision で問いを上位へ戻し、実現方式は仮説（§5）へ下げた。A1 は未着手。**実装キューではない**）
- branch: `claude/osm-content-playas-issue-51y1cj`（計画ブランチ。PR の base は `develop`）
- implementation base commit: `e022934`（`develop` の head、2026-11-07。本書の実測は全てこの base）
- implementation head commit: 該当なし（program）
- risk: 各スライスで決める。S0-b（hybrid の技術実証）と S3 は `high`
- owner: root（依頼者）
- created: 2026-11-07（revision 1）、2026-11-08（revision 2）
- expires: 2026-12-31、または §8 の各スライスの HANDOFF へ取り込まれた時点。期限時は owner が更新・廃止を判断する
- harvest to: `unity/Assets/Docs/Architecture/18-asset-description.md` §4（Play の入口）、`04-app-startup.md` §4.6〜§4.8（Editor Play の起動条件）、`20-variant-checkout-workflow.md` §1・§6（部分 checkout と Delivery。「部分 Checkout 開発の再燃は未所有」の行を本 program が所有する）、`README.md`「試す」。program の完了時に反映する
- 関連: [ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)（AssetManagement / BuildSystem の境界。本 program の Editor 層は連携部分に置き、単独利用の P 検査を退行させない）、[RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md](RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md)（同じ起動コードを触る。§8 の順序制約）、[SEASON_WORLD_DESIGN.md](SEASON_WORLD_DESIGN.md) W-5 / W-6、[BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md](BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md)（local object store の形式。Cloud は対象外）
- Phase A snapshot の取得案内・状態: 該当なし（A0 は program の地図で、凍結する snapshot を持たない）
- Phase B result snapshot / evidence bundle / C' blind bundle: 該当なし
- 引継ぎ票:
  - 次の Phase・役割: owner が本 revision の §7（決定の改訂、特に D4 の保留）と §10 を確認する。確認後、S0-a から A1 を始める（§8。S0-a と S0-b は独立で並行可）。
  - 次の作業: S0-a の HANDOFF を `slice` として切り、本書 §3・§4・§5・§9 を A0 の入力として A1 初稿を書く。新しく A1 を始めるので、[Phase と HANDOFF](../../.agents/skills/osm-workflow/references/phases-and-handoff.md#phase-a-の内部フロー) の利用者の入力の表、目的レビュー、分割の検討、不変条件と成立条件を適用する。各 A1 には「どうすればその問いに誤って YES を出せるか」の反例を書く（R1 の 8-11）。
  - 引き継ぐ commit: 本書を含む計画 PR の最終 commit（tree の外で渡す）。
  - 必要な権限: なし（文書のみ）。
  - 停止規則: S0-b の結果が出るまで D4 を凍結しない。S3 以降は S1 の Phase D より前に Phase B を始めない。Runtime を触るスライス（S0-b、S3 の Runtime 側）の Phase B を `RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` の Phase B と同時に走らせない。
  - 引き渡さないもの: なし。

### 実測の方法

- 名前付き参照と行数: `grep` と `wc -l`（base `e022934`）。R1 が根拠に挙げた code の事実（§2.4）は本 revision で読み直して確認した。
- 動作の確認は行っていない。Unity 上での再現は各スライスの A1 と S0 で行う。
- 本書の作成環境に `pwsh` が無く、`tools/docs-audit.ps1` は未実行。link 先の存在は `git ls-files` で手で照合した。計画 PR の作成前に owner 側で通す。

## 1. 目的と利用者

- **目的:** 職種・担当範囲ごとに並行制作されるゲームで、開発者と検証者が、自分に必要な範囲だけを手元に用意し、手元の編集を失わず、意図どおりで再現できる実行環境を、無駄な作業なしに構成して Play できるようにする。Build、revision、install、publish、source か directory か、は利用者の判断から外す。ただし、何で動いているかはいつでも確かめられ、意図と違う素材を黙って混ぜない。
- **利用者:** (a) 編集する開発者。担当する Scene を手元に置いて編集し、Play で確認する。担当外は手元に無くてよい。(b) 遊ぶだけの人（テスター・企画）。source を持たず、検証済み content だけで Play する。(c) develop を clone した初見の人。README を読む前に Title を開いて Play を押す。
- **この program が答える上位の問い:** 「部分制作の現場で、利用者は、必要な範囲だけを用意し、手元の編集を反映しつつ、何で動いているかを説明・再現できる Editor Play を、迷わず起動できるか。」
- **上位の問いの分解（各スライスがどれに答えるかを §8 に書く）:**
  - **Q-A 利用者の意図:** 誰が、何を編集し、何を確認したいか。source を持たない検証者を含む。
  - **Q-B 作業集合:** 何を手元に置き、編集可能にする必要があるか。
  - **Q-C 実行集合:** Play の成立に何を読む必要があるか。作業集合と同じである必要は無い。
  - **Q-D 一貫性:** 手元の変更、検証済み content、Runtime code、scene graph が混ざっても、意図どおりだと説明・再現できるか。
  - **Q-E 操作と観測:** 利用者に何を選ばせ、何を隠し、異常時に何を知らせるか。
- **revision 1 の問い（降格）:** 「宣言を 1 回選んだ利用者が Play を押すだけで、手元の編集を反映しつつ残りを build 済み content から読む Editor Play に黙って入れるか」。宣言 1 個、差分の source overlay、既製 content 優先、という手段を問いに含めていた。これは §5 の仮説 H1〜H4 として残す。

## 2. 現況（base `e022934`）

### 2.1 利用者から見た現況

- `develop` を clone し、`Title.unity` を開いて Play を押すと、BeforeSceneLoad で起動が失敗し、Console に英語の例外が 1 行出て止まる。`app-config.json`（`unity/Assets/SampleGame/Config/app-config.json`）に `content:runtimeMode` が無く、`AbstractApplicationInitializer.ReadContentDirectoryConfiguration`（`unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs` 837〜866 行目）が「未指定は Delivery の verified pair が無ければ失敗」と判定するため。PR #115（2026-10-11）以降の契約で、[§4.7](../../unity/Assets/Docs/Architecture/04-app-startup.md) に「known-good の自動適用はしない。app-config の未指定を directory に書き換えない」と明記されている。**壊れているのではなく、宣言を選ばせるための意図的な関門**である。
- 動く入口は 3 つあり、全て menu で、宣言 6 個 × 経路 3 本に展開されている。`Tools/OSM/Play As/<宣言>`（source のまま。`PlayAsLauncher`）、`Tools/OSM/Content/Verify Play/<宣言>`（build → publish → install → Play。`VerifyPlayLauncher`）、`Tools/OSM/Content/Delivery`（公開済み directory を受け取る窓。`ContentDeliveryWindow`）。`Tools/OSM/Content/Build <宣言>` が別に 6 個。root が `OneStarMaker/` と `Tools/OSM/` に割れている。
- 前提条件は 6 種類あり（Title.unity を開いている、Variant profile が None、Addressables Play Mode Script が `Use Asset Database`、手で戻す ledger が残っていない、外の環境変数、コマンドライン引数）、`PlayAsLauncher.Check` が最初の 1 件だけを英語の文字列で返し、`DisplayDialog` の OK ボタンだけで止まる。
- Delivery 窓は manifest SHA-256、revision、disk budget 等の生の 10 欄で、日常の経路と同じ場所にある。
- Play 中に「今どの宣言・どの content で動いているか」を見る場所が無い。`ContentDeclarationMismatch` は Console の Warning 1 行。
- 検証済み content の供給が無い。[§20](../../unity/Assets/Docs/Architecture/20-variant-checkout-workflow.md) 冒頭「Editor Play 向け publish 入口は未整備」（2026-11-07 の追記で Verify Play が build → publish → install → Play を一続きで行うが、CI が develop の head ごとに publish してはいない）。
- 取得と build の所要時間の報告: Artifacts の fetch は Spring 221 object で 742 秒（R2。[tools/Artifacts/README.md](../../tools/Artifacts/README.md)）、Content Directory build は 225〜668 秒（[RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md](RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md) §2）。LAN からの install と cache hit の所要時間は未計測。

### 2.2 Framework にある部品

| 部品 | 場所 | 行数 | 役割 |
|---|---|---|---|
| `BuildPurposeDeclaration` / `BuildPurposeResolver` | `OneStarMaker/Scripts/Editor/Build/Selection/`（asmdef `OneStarMaker.Build.Selection`、Engine 参照なし） | 278（Resolver） | 宣言（roots + filter + 表現の優先順）から**実行に必要な**到達集合を解く。build 時にだけ呼ばれる。担当者の編集範囲を表す物ではない |
| `ContentTransportManifest` | `Runtime/BuildContent/Distribution/ContentTransportManifest.cs` | — | `contentSet`、`revision`、`target`、`unityVersion`、`rootSchemaVersion`、`declaration`（名前・表現・runtime 既定値。**optional**）、`files`、`sourceFiles`（path と sha256） |
| `ContentManifestValidation` | 同 folder | — | `files`・`contentSet`・`revision`・`target`・digest 等を検証。`declaration` は検証しない。上限 `MaxManifestBytes` 4 MiB、`MaxFiles` 100,000 |
| `ContentSourceAdvisor` | 同 folder | 77 | installed revision の `sourceFiles` と project root の file を比べ `Complete` / `Missing` / `Changed` を返す。**`Missing` が 1 件でもあれば `Changed` の一覧は返さない** |
| `ContentCacheStore` / `ContentInstaller` / `InstalledRevisionVerifier` | 同 folder | 438 / 185 / 128 | cache root への install、known-good pin（`MarkKnownGood`）、disk budget、再検証 |
| `LocalContentArtifactSource` / `HttpContentArtifactSource` | 同 folder `ContentArtifactSource.cs` | — | local / LAN directory と HTTP の取得口。同じ validation / install 経路 |
| `ContentDirectorySession` / `UnityContentDirectoryBackend` / `ContentDirectoryIndex` | `Runtime/BuildContent/` | 481 / 133 / 130 | directory の登録、native 操作、identity 解決 |
| `AssetManagement.SealAddressables` / `SealedAssetBackend` / `AssetManagement.ContentDirectory.cs` | `Runtime/AssetManagement/` | 494（AssetManagement） | directory と決まった直後に Addressables の取得経路を 1 回だけ封じる。in-flight、resident cache、registry を固定。identity と表現を 1 つの台帳で管理 |
| `BuildContentCoordinator` / `ContentTransportPublisher` | `Editor/Build/Content/`（asmdef `OneStarMaker.Build.Content`） | 188 / 143 | Content Directory build と transport publish。`sourceFiles` には preflight closure のうち **`File.Exists` の path だけ**を書く（72〜73 行目） |

### 2.3 SampleGame にある知識

| 知識 | 場所 | 行数 |
|---|---|---|
| 宣言の一覧と初回 Scene（全て `Title`）、companion set の既定 | `SampleGame/DependOnAll/Editor/Build/SampleGameBuildPurposes.cs` | 78 |
| 宣言 → config key=value と環境変数名（`SAMPLEGAME_` + key）、`Full` は `assets:sceneVariant` を置かない | `PurposeLaunchSettings.cs` | 101 |
| Play As の前提条件の検査と `EnterPlaymode` | `PlayAsLauncher.cs` | 176 |
| 検証 Play の段（`Start → Produce → Install → BeforePlay → Bridge → EnterPlaymode`） | `VerifyPlayLauncher.cs` | 499 |
| 1 回の Play で戻す / 手で戻す ledger（EditorPrefs `OSM.ContentDelivery.PlayBridge.v1`） | `ContentDeliveryPlayBridge.cs` | 234 |
| Delivery 窓 | `ContentDeliveryWindow.cs` | 212 |
| Content build の入口と BS4 Player | `SampleGameContentBuild.cs`、`SampleGamePlayerBuildCoordinator.cs` | 148 / 152 |

### 2.4 成立していない前提（R1 で確認したものを含む）

- **source と directory の同居。** 起動は source（Addressables 互換）か directory かを BeforeSceneLoad で 1 つ選び、directory なら Addressables を封じる。表現は起動時に固定し、同一 session の表現切替は提供しない（[§18](../../unity/Assets/Docs/Architecture/18-asset-description.md)「`EntryAmbiguous`」）。`EditorSceneManager` で Scene を開けることと、OSM の Scene 所有、unload、依存解決、directory の native handle を混ぜて正しく解放できることは別の問題。**現行は保証していない。**
- **旧 Hybrid Play の前例。** [§20](../../unity/Assets/Docs/Architecture/20-variant-checkout-workflow.md) §1 に「ローカルに Checkout 済みかつ依存閉包が完結するアセットは AssetDatabase 直読み、未 Checkout は remote Addressables catalog」という旧経路があり、2026-09-20 に通常経路から切断された。今回の source + Content Directory は同じ技術ではないが、「なぜ廃止したか」「新しい混成は旧経路のどの失敗を解消するか」の回顧を S0-b の A1 に含める。§20 §6 の「部分 Checkout 開発の再燃は未所有」を本 program が所有する。
- **file の sha の差は編集意図を意味しない。** 別 branch、別時点、VCS update で入った他人の変更でも差が出る。`.unity` の bytes が一致しても prefab / material / texture / SceneResourceMap / `.meta` の変更で見た目と参照は変わる。新規 Scene、削除・rename、GUID の欠落、未保存の Dirty Scene は manifest の sha 比較で扱えない。source を持たない検証者の `Missing` を source 側と誤判定してはならない。
- **「宣言名が一致」は content の互換を意味しない。** manifest の `declaration` は optional で検証されず、同じ名前でも graph・filter・runtime 既定値・code・build tool の出力が違い得る。既存の消費者は contentSet / revision / target / Unity version / root schema / digest を検証するが、要求した実行目的と同じ内容かまでは示さない。
- **local object store と installed cache は別の層。** Artifacts の store（`content/<contentSet>/<target>/<revision>/`）に置いただけでは Play に使えず、cache root への install と verify が要る。
- **到達集合だけを出す入口が無い。** `BuildPurposeResolver` は build の一部としてしか呼ばれない。
- **VCS の抽象が無い。** Framework は Git も SVN も知らない。`OneStarMaker > Variant > Checkout Report` は案内のみ。
- **`ExitingEditMode` は Play への移行途中。** 探索、install、hash、Build を同期で置けるとは限らず、取り消し → 準備 → 再 Play、重複 event、compile / import 中、domain reload、Editor の crash、複数 Editor を含む状態機械が要る。

## 3. 概念モデル（A1 で用語を固定する）

R1 の B-01 / B-02 / B-04 を受け、revision 1 が 1 つの宣言と sha 差分に押し込んでいた物を、次の 4 つに分ける。UI で「選ぶのは 1 つ」を保つ場合も、内部ではこの 4 つを同一視しない。

- **RunPurpose（実行目的）:** ゲーム全体として何を実行するか。現行の `BuildPurposeDeclaration`（roots + filter + 表現 + runtime 既定値）がこれ。実行の閉包は担当者の編集範囲より広い。Q-C を決める。
- **Workset（作業集合）:** 利用者が手元に所有し、編集し、Play に反映したい物。Scene identity か親子 scope の明示。省略は「read-only 実行」を意味し、検証者はこれで動く。Q-B を決める。初期実装で Workset を自動取得する必要は無い。
- **PlaySessionPlan（実行計画）:** 1 回の Play で何を、どこから、どの identity で読むかを確定した不変の計画。RunPurpose の内容 fingerprint、選んだ content の contentSet / target / revision / manifest digest、Unity version / root schema、Workset の各 Scene の供給元（source か directory か）と根拠、欠損・不一致の理由を持つ。Play はこの計画からだけ起こし、計画は Console と観測 UI から辿れる。Q-D と Q-E の根拠。
- **供給元の連鎖:** 検証済み content を探す順。手元の installed cache → local object store → LAN。Cloud は対象外（D5）。「候補がある」と「要求を満たす互換な候補がある」を区別する。

sha の差は、**計画の基準（content の `sourceFiles`）と手元の物理 file の一致確認**に使う補助証拠であり、「どの source を適用するか」の決定に使わない。適用は Workset で決める。

## 4. 要求（UX と成功条件。A1 の入力）

成功条件を 1 つに束ねず、次の 5 つに分けて各スライスの gate にする（R1 P0）。

- **R-1 Play の成立:** 3 種の利用者（編集者、検証者、初見）が、迷わず Play に入れる。初見は README を読む前に Play を押すので、Play ボタンが入口である。
- **R-2 編集の反映:** Workset の Scene の手元の変更が Play に反映される。Workset 外の手元の差は反映しないが、計画に記録して見える。
- **R-3 再現可能性:** 何で動いたかを PlaySessionPlan で説明でき、同じ計画で再現できる。意図と違う素材（別 branch の content、同名の別宣言、壊れた graph、別表現、旧 manifest）を黙って混ぜない。
- **R-4 取得コスト:** cold start（初回取得）、warm start（cache hit）、source-only を分けて合格時間を置く。「初見が 1 分以内に Play」は warm start の目標であり、供給が無い環境の受け入れ条件にしない（R1 M-08）。
- **R-5 観測:** Play 中の RunPurpose、content の出所、Workset の件数と対象、選択の理由、再現用の revision / digest に、SceneView と GameView の両方から到達できる（R1 M-07）。

### 4.1 利用者が選ぶもの

- RunPurpose は永続の選択（週に数回）。初期値は前回の値、初回は Game 側 provider の既定。Play のたびには選ばない。
- Workset は編集者だけが明示する。省略が既定で、検証者は何も選ばない。選ぶ単位は Scene identity か親子 scope で、既存の `World Workspace` 窓や Scene graph の node から選べる形を A1 で比較する。
- source の取得（VCS）は Workset を決めた時の動詞であり、Play の条件にしない。VCS 連携は任意の adapter で、手動で checkout した人も同じ PlaySessionPlan で動く（R1 M-05）。
- **「1 つだけ選ばせる」UI を保つなら**、宣言 1 つから RunPurpose と Workset の既定（空）を生成する規則を A1 で提案し、内部のモデルは分けたままにする（H4）。

### 4.2 Build の見せ方

- Build を促すのは、**利用可能で互換な content が 1 つも無い時、または再構成が必要な時**（R1 M-04）。候補が存在するだけでは促さず、要求を満たさない候補しか無ければ、再 Build、別 revision、source-only Play の選択肢を出す。
- 促す時は 1 画面で、探した場所、Build に source が要ること、所要時間の目安、`[Build して Play]` `[source のまま Play]` `[中止]`。source を取っていない人には `[取得して Build して Play]`。
- Build の後は publish → install → verify を経て Play する。publish だけで Play 可能と見なさない（R1 M-03）。次回は installed cache の検査で選ぶ。LAN の共有先へも置くかは設定の toggle 1 つ。
- sha 差分の件数を「Build すると速くなる」と結論しない。速度の bottleneck は別に計測する。

### 4.3 通知は 3 段、ただし結果は常に観測可能

| 段 | 出し方 | 条件 |
|---|---|---|
| 無言 | 何も出さない。Console に計画の要約 1 行と、観測 UI の表示 | 計画が一意に決まり、互換な content があり、Play に入れる。前回と同じ RunPurpose で LAN から取る（進捗は観測 UI） |
| 非ブロック | 観測 UI か Console の Warning 1 行。Play は止めない | 複数の互換候補から known-good で選んだ。供給元の縮退。Workset 外の手元の差の件数 |
| ブロック | dialog。Play を止める | Workset の Scene が手元に無い、または未保存（Dirty）。互換な content が無い。宣言 mismatch、graph の違い、必要な bootstrap の欠損。直せない前提条件（外の環境変数・コマンドライン・明示 `content:runtimeMode`・手動 Delivery の ledger）。取得の失敗（hash 不一致、budget 超過、全経路 404） |

- **黙って直してよい前提条件は限定する。** Addressables の Play Mode Script のように Framework が所有し、利用者の明示操作ではない物に限る。Variant profile の選択、手動 Delivery の「Use For Next Play」、明示 `content:runtimeMode`、環境変数は利用者の明示操作なので、黙って戻さず止める（R1 M-02、T13）。戻す場合は復元の記録を残す。
- **「今後聞かない」は、無視しても安全な条件に限る。** 許すのは供給元の縮退、Workset 外の差の件数のような非ブロックの通知だけ。互換性の失敗、未反映、検証の失敗は永続的に黙らせない（R1 M-07）。鍵（RunPurpose、Workset、失敗の種類）が変われば聞き直す。
- 観測 UI は SceneView の Overlay だけに置かず、GameView 中の検証者も見える場所（toolbar、Inspector、診断出力のどれか）を A1 で決める。

### 4.4 Framework / Game / VCS の責務

- **Framework Editor（新設、任意の連携層）:** PlaySessionPlan の生成（純粋な判定。side effect を持たない）、計画を実行する executor（探索、install、verify、config の適用、Play、復帰）、Play ボタンの割り込み、dialog と観測 UI、Content Sources の設定（cache root、disk budget、LAN root、store root。利用者ごとの値で git 管理外）。宣言と manifest の語彙だけで書き、SampleGame を知らない。置き場所は [ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md) §2.1 の連携部分。AssetManagement 単独・BuildSystem 単独の P 検査に余分な参照を増やさない（R1 M-06、T16）。
- **Game 側が実装する interface:** RunPurpose の一覧と既定、RunPurpose → 初回 Scene、RunPurpose → config key と環境変数名の写像。今 `PurposeLaunchSettings` と `SampleGameBuildPurposes` にある知識。Game の Editor asmdef が `InitializeOnLoad` で登録し、依存の向きは Game → Framework のまま。
- **VCS adapter（任意）:** 「この path 集合を手元に置け」「この path 集合の手元の状態（存在・sha・未保存）」の 2 つを interface で受ける。SVN 実装は Game 側か `tools/`。Framework core に VCS の語彙を入れない。
- **Runtime:** 既存の起動契約（未指定で黙って動かない、Addressables へ暗黙 fallback しない、protected key）は変えない。hybrid を採る場合も、Editor 型を Runtime asmdef に入れず、明示された identity だけを Editor 専用 loader に委譲する adapter を連携層に置く案を、`SealedAssetBackend` の一部解禁より先に比較する（R1 B-03）。
- **既存入口の行き先:** `Play As` と `Verify Play` の 12 menu は Play 割り込みに吸収。`Content/Build <宣言>` は CI と担当者用に残す。Delivery 窓の 10 欄は「特定 revision を指定して再現したい QA」向けの詳細へ格下げ。

## 5. 仮説と代替案（A1 と S0 で検証する。確定ではない）

### 5.1 仮説

- **H1 hybrid 読込:** directory 登録済みの Editor Play で、Workset の Scene だけを source から、残りを directory から読める（revision 1 の D4 (a)）。**S0-b で実証するまで本線にしない。**
- **H2 sha の補助利用:** `sourceFiles` の sha と手元の file の比較は、計画の基準と手元の一致確認として有効。差分の検出だけで適用対象を決めない。
- **H3 Play 割り込みの transaction:** `ExitingEditMode` で Play を取り消し、準備（探索・install・verify・計画の適用）を非同期で行い、完了後に再 Play する状態機械が、二重 Play・再入・crash 後の ledger 残留無しに成立する。
- **H4 1 つの選択:** 宣言 1 つから RunPurpose と空の Workset を生成し、編集者だけが Workset を足す UI で、R-1 と R-2 を両立できる。

### 5.2 代替案の比較（R1 §5 を採用。採否は S0 の結果で決める）

| 案 | 内容 | 利点 | 欠点 | 位置づけ |
|---|---|---|---|---|
| A | Play As / Verify Play / Delivery を統合し、RunPurpose の選択と自動 install・verify だけを改善。source-only と directory-only は現行 Runtime のまま | 既存の安全契約を維持。最短で R-1・R-3・R-5 の大半を改善 | 一部 source + 残り directory の hybrid は無い | **先行実施。** S0-a の本体 |
| D | 検証者は directory-only、編集者は source-only。1 つの UI で起動し、RunPurpose の選択だけ共通化 | 再現性が高く、今ある部品で動く | 編集者の手元に実行閉包の source が要り、部分 checkout の効果が限定される | A と組で最初の実用経路。H1 が成立するまでの基準 |
| B | Editor 専用の SourceSceneProvider と ContentDirectoryProvider を、Workset の明示 identity で束ねる route adapter（連携層） | sealed の契約と混成の方針を分けられ、どの Scene が source かが一意 | Scene 所有・graph・依存 asset の混成整合が難しい。技術実証が必須 | hybrid が要るなら第一候補。**S0-b が成立した時だけ採用** |
| C | Play 前に Workset を小さな Content Directory として build し、既製 content と構成して Play（revision 1 の (b)） | 読込形式を揃えられ、Runtime の経路差が減る | publisher は Full build 限定。差分 build・revision・root 合成が未整備。初回 latency が増える | B が不成立の時の退路。「簡単」と誤認しない |

## 6. 制約と常時契約

- `AGENTS.md` の常時契約を全て守る。特に: 依存は Game → Framework の一方向で asmdef 参照の追加は設計判断、アセットは `IAssetManagement` 経由、Editor コードを Runtime asmdef に置かない、Unity 側で `record` を使わない、`#nullable enable`、偽 null、テストで `Task.Delay` / `Thread.Sleep` を使わない。
- **Runtime の起動契約は変えない。** `content:runtimeMode` 未指定で黙って動かない、Addressables へ暗黙 fallback しない、未知 mode は失敗、protected key は Player で拒否、は維持する。Editor 層が選んだ結果は常に明示の config として Runtime に入り、PlaySessionPlan が理由を持つ。§4.7 の「known-good の自動適用はしない」と Editor 層の解決の境界は S1 で明文化する（§10 Q1）。
- **表現は起動時に固定**のまま。hybrid は表現の切替ではない。
- **Player は対象外。** BS4 Player の required JSON、protected key、smoke は触らない。
- **Cloud（R2）は対象外**（D5）。Artifacts README の「Unity を起動せず、Unity 側からも自動起動しません」は変えない。
- **単独利用の非退行。** AssetManagement 単独・BuildSystem 単独の P 検査（[ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)）に、本 program の Editor 連携が参照を増やさない。
- 既存の HANDOFF との順序: Runtime を触るスライスの Phase B を `RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` の Phase B と同時に走らせない。その Phase D の後は、facade `ContentDirectoryStartup` の API を使う設計へ更新する（R1 M-06）。
- `tools/path-budget.json` の予算に従う。

## 7. owner 決定の改訂（revision 2）

revision 1 の D1〜D8 を R1 に照らして分類した。「改訂」「保留」は owner が本 revision の確認で確定する。

| ID | revision 1 | revision 2 | 理由 |
|---|---|---|---|
| D1 | `app-config.json` に `addressables` を置く案は採らない | **維持** | 既定を Addressables にしない方針は変わらない |
| D2 | Framework 側に置く。SampleGame に寄せない | **維持**（連携層に置き、P 検査を退行させない、を追加） | R1 M-06 |
| D3 | 利用者に Build を意識させない | **改訂:** 「互換な content があれば Build を見せない。無い時と再構成が要る時だけ 1 回促す。再現可能性と選択結果の確認は隠さない」 | R1 留保・M-04。「意識させない」を「存在・出所・互換性を全く見せない」と同義にしない |
| D4 | Runtime の source overlay (a) を本線にする | **保留:** S0-b の実証まで凍結しない。A + D を先行し、B（連携層の adapter）を第一候補、C を退路として比較する | R1 B-03。現行の sealed 契約と Scene 所有の下で hybrid は未実証 |
| D5 | Cloud は後続 | **維持** | — |
| D6 | 通知は必要な警告だけ。Play のたびの popup は出さない | **維持**（「今後聞かない」と黙って直す対象の限定を追加） | R1 M-02・M-07 |
| D7 | content が無い現場では Build を促してよい | **維持**（条件を「互換な候補が無い時」に言い換え） | R1 M-04 |
| D8 | source code は Git、asset は SVN。宣言が SVN の取得範囲を決める | **改訂:** 「VCS 連携は任意の adapter。取得範囲は Workset から導き、RunPurpose（実行閉包）と同一視しない。手動 checkout でも同じ計画で動く」 | R1 B-01・M-05 |

## 8. スライス列

順序は「UX の最小実証 → hybrid の技術実証 → 契約の確定 → 実装」（R1 §6）。各スライスは `slice` の HANDOFF を切り、1 スライス 1 ブランチ。S0-a と S0-b は独立で並行できる。

| 順 | スライス（HANDOFF 名の候補） | 答える問い | 答える上位の問い | 主な変更対象 | risk | gate（次へ進む最低条件） |
|---|---|---|---|---|---|---|
| S0-a | `PLAY_ENTRY_MINIMAL` | Runtime を変えずに、RunPurpose の選択 → 手元 cache / local store / LAN の候補検査 → 明示の directory-only または source-only Play、を 1 つの入口から迷わず起動できるか。純粋な計画（PlaySessionPlan の原型）と side effect を持つ executor を分けられるか | Q-A、Q-E、R-1、R-3、R-5 | Framework Editor（新設）、`PlayAsLauncher` / `VerifyPlayLauncher` / `ContentDeliveryPlayBridge`（吸収）、Game provider interface | normal | 編集者・検証者・初見の 3 利用者が迷わず起動でき、選択の結果が Console と観測 UI から追える。候補なし・別 revision・明示 override・手動 ledger の各場合の扱いが実機で確認できる |
| S0-b | `HYBRID_LOAD_SPIKE` | Scene A（手元の source）と Scene B（published directory）を同一 session で読み、所有・unload・停止・再 Play・domain reload 無効・失敗と取消を保てるか。bootstrap（UICommon / SceneResourceMap）の供給元を明示できるか。旧 Hybrid Play の廃止理由を回顧し、新しい混成がどの失敗を解消するか | Q-C、Q-D、H1、案 B / C の採否 | 使い捨ての実証 code（製品 code に残さない）、`AssetManagement` の読み口の観察 | high | 不変条件に違反しない実機の証拠。source だけ変更、依存 prefab だけ変更、Missing、Dirty、別表現、混在 graph を投入して挙動を記録。不成立なら D4 の修正を owner に返し、C を評価する |
| S1 | `PLAY_SESSION_PLAN_CONTRACT` | (RunPurpose, Workset, content の identity, graph / source の provenance) から決定的な PlaySessionPlan を出し、意図しない混成を拒否できるか。旧 manifest と hybrid の許可条件を明記できるか | Q-D、R-3 | Framework Editor（計画の型と判定）、manifest の `declaration` の検証範囲、[§18](../../unity/Assets/Docs/Architecture/18-asset-description.md) の契約 | normal | 同名の別宣言・別 branch・壊れた graph・別表現・旧 manifest が無言で合成されない test matrix（§9 T06・T08・T09・T14）が通る |
| S2 | `SOURCE_METADATA_MINIMAL` | S1 の契約に必要な最小の source metadata は何か。`sourceFiles` の拡張が本当に要るか、Editor 用 sidecar（overlay index）で足りるか。Scene identity → file path / GUID、Scene → 依存 asset の責務をどこに置くか。`BuildPurposeResolver` の dry-run 入口が要るか | Q-B、Q-C、H2 | `Editor/Build/Selection/`、`ContentTransportPublisher`、`ContentSourceAdvisor`（`Missing` と `Changed` を同時に返す形を含む） | normal | Scene だけ / 依存 asset だけ / 新規 / 削除 / `.meta` の全 case で供給元の予測が正しい（T04〜T07） |
| S3 | `PLAY_RESOLVER_TRANSACTION` | 検証済み content の取得・install・verify、計画の適用、Play、1 回限りの設定の復帰を、`ExitingEditMode` からの transaction として fail-closed に実装できるか。S0-b が成立していれば Workset の hybrid 読込を含む | Q-E、R-1、R-2、H3 | Framework Editor（executor、状態機械）、Runtime 側は S0-b の結論に従う | high | 失敗・取消・hash 不一致で未検証 content では起動せず元の設定へ戻る（T11）。二重 Play・再入・crash 後の ledger 残留が無い（T12）。手動 Delivery・明示 `runtimeMode`・外部環境変数を黙って壊さない（T13） |
| S4 | `PLAY_BUILD_PROMPT` | 互換な候補が無い時だけ 1 画面で Build を促し、build → publish → install → verify を経て Play に入れるか | R-1（供給が無い現場）、R-4 | Framework Editor（dialog）、`BuildContentCoordinator` / `ContentTransportPublisher` の呼び出し、store への配置 | normal | publish 成功だけで Play 可能と宣言しない。install + verify 後に Play（T01） |
| S5 | `WORKSET_CHECKOUT_AND_OBSERVE` | Workset の選択と任意の VCS adapter（SVN を含む）で source を手元に置き、手動取得の人と同じ計画で動くか。GameView / SceneView の観測 UI、menu の整理、README で初見の体験が成立するか | Q-A、Q-B、R-5 | Framework Editor（Workset UI、VCS interface、観測 UI）、SVN 実装（Game 側か `tools/`）、`ContentDeliveryWindow`、menu、`README.md`、harvest | normal | 手動 checkout と自動 checkout が同じ PlaySessionPlan を得る。確認済みの provenance が表示される（T03、T15） |

- **外部依存の gate:** 検証済み content の CI publish（develop の head ごとの build → publish）は本 program の外（[§18](../../unity/Assets/Docs/Architecture/18-asset-description.md)「CI 組み込み（BS5b-1 から）」）だが、供給がある現場の R-1・R-4 はこれ無しに成立しない。owner が所有と順序を決め、無い間は S4 の Build 促しが唯一の供給である（§10 Q9）。
- **S0-a を最初にする理由:** Runtime を変えずに R-1・R-3・R-5 の大半を改善でき、採否を戻せる粒度で前進できる。S0-b の結果で S3 の形（hybrid を含むか）が決まる。
- **S2 は S1 の後。** 何の metadata が要るかは S1 の契約が決める。revision 1 の「`sourceFiles` を全 source file に格上げ」は、契約・version・上限（4 MiB / 100,000 件）・依存関係・未保存状態の解が出るまで延期（R1 M-01）。

## 9. 最低限の検証ケース（各 A1 の受け入れ条件の素材。R1 §7）

| ID | 場面 | 期待 |
|---|---|---|
| T01 | fresh clone、content 無し、source あり | 初回の選択ができ、候補の不存在と選択肢が説明される |
| T02 | cache hit、同じ RunPurpose と表現 | 無言で directory Play。計画を追跡できる |
| T03 | source 無しの検証者 + installed content | `Missing` を Workset と誤判定しない |
| T04 | Workset の Scene を編集して保存 | Workset の Scene だけ手元の変更が反映される |
| T05 | `.unity` 不変、共有 prefab / material だけ変更 | 変更を無視せず、適用するか互換性で拒否する |
| T06 | 手元の graph に Scene を追加 / 削除 | 不完全な directory のまま黙って Play しない |
| T07 | 未保存の Dirty Scene | 保存無しで旧 content を無言で読まない |
| T08 | 同名 RunPurpose、異なる filter / runtime 既定値 / branch | 誤選択を拒否する |
| T09 | source code の版と content の serialized component の不整合 | 明示の診断か拒否 |
| T10 | source Scene と directory Scene のそれぞれの unload / reload | 所有台帳が整合し、handle leak が無い |
| T11 | Play 途中で install が失敗 / 取消 / hash 不一致 | 未検証 content で起動せず、元の設定へ復旧 |
| T12 | `ExitingEditMode` で取得や Build が必要 | 二重 Play・無限再入・crash 後の ledger 残留が無い |
| T13 | 手動 Delivery、明示 `runtimeMode`、外部環境変数 | 人間の明示選択を黙って壊さない |
| T14 | 別 target / Unity version / 旧 manifest | 非互換候補を黙って使わない |
| T15 | cold と warm | 探索、hash、download、install、Play 入りまでを分けて実測 |
| T16 | AssetManagement 単独と BuildSystem 単独 | Editor 連携の追加で依存が退行しない |

## 10. 未決事項（各スライスの A1 へ）

- **Q1（S1）:** §4.7「known-good の自動適用はしない」と、Editor 層が PlaySessionPlan で pair を書く行為の境界。案: Runtime の文言は変えず、Editor 層は計画を Console と観測 UI に出すので黙ってはいない、と §18 §4 に書く。
- **Q2（S0-b / S3）:** Workset の供給元を Runtime に渡す形。config の `content:` 予約 key か、Editor 専用の bridge か。Player の protected key に入れない。
- **Q3（S0-b）:** Workset の Scene を source から読む経路。Addressables の `Use Asset Database` に依存するか、`EditorSceneManager` 直か。封じた backend を部分的に開けるのではなく、連携層の adapter に委譲できるか（案 B）。
- **Q4（S2）:** metadata の範囲。Scene file だけか、依存 asset まで含めるか。manifest に入れるか sidecar か。
- **Q5（S2 / S3）:** 手元の状態の計算コスト。Play のたびの全 file hash は重い可能性。mtime + size の短絡や VCS の status を使うか（S5 の adapter との境界）。
- **Q6（S4）:** Build 後に local object store へ置く形式。Artifacts の `content collect` と同じ layout を Unity から直接書くか、`tools/artifacts.ps1` を子 process で呼ぶか。後者は Artifacts README の契約に触れる。
- **Q7（S5）:** SVN sparse checkout の単位。Workset は Scene の集合だが、SVN は directory の depth。依存 asset の directory まで広げる規則と `.meta` / GUID の扱い。
- **Q8（S5）:** `OneStarMaker/` と `Tools/OSM/` の二重 root を 1 つにするか。
- **Q9（program）:** CI publish の供給の所有と順序。供給が無い間の体験（S4 の Build 促しだけ）を受け入れるか。
- **Q10（S0-a）:** 観測 UI の置き場。SceneView の Overlay、toolbar、Inspector、診断出力のどれを最小にするか。GameView 中の検証者が見える物を 1 つは含める。
- **Q11（後続、本 program の対象外）:** Cloud（R2）経路、取得の先読み、Scene 単位の増分 build（案 C の本格化）、Player での hybrid。

## 11. 計画レビュー記録

- **R1（2026-11-08、外部、owner 経由で受領）:** 対象は revision 1（commit `6e7466e`）。総合判断は「問う価値は高いが、D1〜D8 確定済みで S2 → S1 → S3 へ進める意味での承認は非推奨」。Blocker 4 件（B-01 RunPurpose と Workset の分離、B-02 sha 差分は編集意図ではない、B-03 hybrid の成立性を先に実証、B-04 一致と互換の未定義）、Major 8 件（M-01〜M-08）、代替案 A〜D、再計画 P0 / S0-a / S0-b / S1〜S5、検証 T01〜T16。
- **採否（owner 同意、2026-11-08）:** Blocker 4 件と Major 8 件を全て採用し、本 revision の §1・§3・§4・§5・§7・§8・§9 に反映した。採用の理由は、R1 が根拠に挙げた code の事実（§2.2・§2.4）を読み直して全て一致したこと、revision 1 の問いが owner の依頼の文言に引かれて手段を含んでいたこと。R1 が「不可能とは断定しない」とした hybrid は、本 revision でも仮説 H1 として S0-b で実証する。
- **R1 の指摘で本 revision が条件付きで扱った物:** B-04 の「graph version」の fingerprint 化は、最小は contentSet / target / revision / digest / `declaration` の内容 / Unity version / root schema とし、graph version の追加は S1 で必要性を判定する。M-08 の合格時間の数値は S0-a の実測（T15）の後に置く。

## 12. この文書の終了

本書は program の地図で、各スライスの HANDOFF が切られるごとに §8 の行をその HANDOFF へのリンクに置き換える。全スライスの Phase D が終わるか、owner が program を打ち切った時点で、§3 の概念、§4 の要求、§7 の決定のうち恒久的なものを §0 の harvest 先へ移し、本書を削除する。
