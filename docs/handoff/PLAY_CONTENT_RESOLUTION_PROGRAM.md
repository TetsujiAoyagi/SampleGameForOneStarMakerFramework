# 宣言からの Play と content 解決の UX（Phase A0 planning packet）

## 0. メタデータ

このtracked文書の全Phase欄では、各成果物の取得案内を渡す既存の引渡し先・担当と状態（未生成／未保存／ローカル固定済み（未昇格）／引渡し未完了／確認済み）を、公開可能な範囲で記録する。取得に使うreference・期待packageSha256の実値、ローカルpath、内部catalog/receiptはこの文書へ転記しない。

- type: `program`（A0 の planning packet。実装スライスではない。§6 のスライス列の各 A0 への入力）
- status: `A`（A0。owner との対話で D1〜D8 を決めた（§5）。A1 は未着手。**実装キューではない**）
- branch: `claude/osm-content-playas-issue-51y1cj`（計画ブランチ。PR の base は `develop`）
- implementation base commit: `e022934`（`develop` の head、2026-11-07。本書の実測は全てこの base）
- implementation head commit: 該当なし（program）
- risk: 各スライスで決める。S1（Runtime の source overlay）は `high`
- owner: root（依頼者）
- created: 2026-11-07
- expires: 2026-12-31、または §6 の各スライスの HANDOFF へ取り込まれた時点。期限時は owner が更新・廃止を判断する
- harvest to: `unity/Assets/Docs/Architecture/18-asset-description.md` §4（Play の入口）、`04-app-startup.md` §4.6〜§4.8（Editor Play の起動条件）、`20-variant-checkout-workflow.md`（checkout と Delivery の手順）、`README.md`「試す」。program の完了時に反映する
- 関連: [ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md)（AssetManagement / BuildSystem の境界。本 program の Editor 層は連携部分に置く）、[RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md](RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP.md)（同じ起動コードを触る。§6 の順序制約）、[SEASON_WORLD_DESIGN.md](SEASON_WORLD_DESIGN.md) W-5 / W-6（取得済み content からの実行）、[BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md](BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md)（local object store の形式。Cloud は本 program の対象外）
- Phase A snapshot の取得案内・状態: 該当なし（A0 は program の地図で、凍結する snapshot を持たない。各スライスの snapshot は各 HANDOFF §0 が持つ）
- Phase B result snapshot / evidence bundle / C' blind bundle: 該当なし
- 引継ぎ票:
  - 次の Phase・役割: 本書の A0 を owner が確認し、§6 の S2 から A1 を始める（S2 → S1 の順。理由は §6）。
  - 次の作業: owner が §5 の決定と §7 の未決を確認する。確認後、S2 の HANDOFF を `docs/handoff/` に `slice` として切り、本書 §3・§4・§6 を A0 の入力として A1 初稿を書く。新しく A1 を始めるので、[Phase と HANDOFF](../../.agents/skills/osm-workflow/references/phases-and-handoff.md#phase-a-の内部フロー) の利用者の入力の表、目的レビュー、分割の検討、不変条件と成立条件を適用する。
  - 引き継ぐ commit: 本書を含む計画 PR の最終 commit（tree の外で渡す）。
  - 必要な権限: なし（文書のみ）。
  - 停止規則: S1 の Phase B を `RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` の Phase B と同時に走らせない（同じ `AbstractApplicationInitializer.cs` と `AssetManagement.cs` を触る）。S3 以降は S1 の Phase D より前に Phase B を始めない。
  - 引き渡さないもの: なし。

### 実測の方法

- 名前付き参照と行数: `grep` と `wc -l`（base `e022934`）。
- 動作の確認は行っていない。本書の「現況」は code と公開文書の読み取りで、Unity 上での再現は各スライスの A1 で行う。
- 本書の作成環境に `pwsh` が無く、`tools/docs-audit.ps1` は未実行。link 先の存在は `git ls-files` で手で照合した。計画 PR の作成前に owner 側で `pwsh tools/docs-audit.ps1` を通す。

## 1. 目的と利用者

- **目的:** OSM を使う開発者が、Unity の Play ボタンを押すだけで、自分の作業空間（宣言）に合った content で Play に入れるようにする。Build、revision、install、publish、source か directory か、は利用者に見せない内部事情にする。それでも利用者にしか決められない分岐と、Play しても無駄になる失敗だけを、利用者に伝える。
- **利用者:** (a) 編集する開発者。作業空間を選び、source を VCS から手元に置き、Scene を編集して Play で確認する。(b) 遊ぶだけの人（テスター・企画）。source を持たず、build 済み content だけで Play する。(c) develop を clone した初見の人。README を読む前に Title を開いて Play を押す。
- **この program が答える問い:** 「宣言を 1 回選んだ利用者が、Play ボタンを押すだけで、手元の編集を反映しつつ、残りを build 済み content から読む Editor Play に、黙って入れるか。content が無い現場では Build を 1 回促すだけで同じ状態に到達できるか。」

## 2. 現況（base `e022934`）

### 2.1 利用者から見た現況

- `develop` を clone し、`Title.unity` を開いて Play を押すと、BeforeSceneLoad で起動が失敗し、Console に英語の例外が 1 行出て止まる。`app-config.json`（`unity/Assets/SampleGame/Config/app-config.json`）に `content:runtimeMode` が無く、`AbstractApplicationInitializer.ReadContentDirectoryConfiguration`（`unity/Assets/OneStarMaker/Scripts/Runtime/Bootstrap/AbstractApplicationInitializer.cs` 837〜866 行目）が「未指定は Delivery の verified pair が無ければ失敗」と判定するため。これは PR #115（2026-10-11）以降の契約で、[§4.7](../../unity/Assets/Docs/Architecture/04-app-startup.md) に「known-good の自動適用はしない。app-config の未指定を directory に書き換えない」と明記されている。**壊れているのではなく、宣言を選ばせるための意図的な関門**である。
- 動く入口は 3 つあり、全て menu で、宣言 6 個 × 経路 3 本に展開されている。`Tools/OSM/Play As/<宣言>`（source のまま。`PlayAsLauncher`）、`Tools/OSM/Content/Verify Play/<宣言>`（build → publish → install → Play。`VerifyPlayLauncher`）、`Tools/OSM/Content/Delivery`（公開済み directory を受け取る窓。`ContentDeliveryWindow`）。`Tools/OSM/Content/Build <宣言>` が別に 6 個。root が `OneStarMaker/` と `Tools/OSM/` に割れている。
- 前提条件は 6 種類あり（Title.unity を開いている、Variant profile が None、Addressables Play Mode Script が `Use Asset Database`、手で戻す ledger が残っていない、外の環境変数、コマンドライン引数）、`PlayAsLauncher.Check` が最初の 1 件だけを英語の文字列で返し、`DisplayDialog` の OK ボタンだけで止まる。直す場所へは利用者が自分で辿る。
- Delivery 窓は manifest SHA-256、revision、disk budget 等の生の 10 欄で、日常の経路と同じ場所にある。
- Play 中に「今どの宣言・どの content で動いているか」を見る場所が無い。`ContentDeclarationMismatch` は Console の Warning 1 行。

### 2.2 Framework にある部品（本 program が束ねるもの）

| 部品 | 場所 | 行数 | 役割 |
|---|---|---|---|
| `BuildPurposeDeclaration` / `BuildPurposeResolver` | `OneStarMaker/Scripts/Editor/Build/Selection/`（asmdef `OneStarMaker.Build.Selection`、Engine 参照なし） | 278（Resolver） | 宣言（roots + filter + 表現の優先順）から到達集合を解く。build 時にだけ呼ばれる |
| `ContentTransportManifest` | `Runtime/BuildContent/Distribution/ContentTransportManifest.cs` | — | `contentSet`、`revision`、`declaration`（名前・表現・runtime 既定値）、`files`、`sourceFiles`（path と sha256） |
| `ContentSourceAdvisor` | `Runtime/BuildContent/Distribution/ContentSourceAdvisor.cs` | 77 | installed revision の `sourceFiles` と project root の file を比べ `Complete` / `Missing` / `Changed` を返す |
| `ContentCacheStore` / `ContentInstaller` / `InstalledRevisionVerifier` | `Runtime/BuildContent/Distribution/` | 438 / 185 / 128 | cache root の install、known-good pin（`MarkKnownGood`）、disk budget、再検証 |
| `LocalContentArtifactSource` / `HttpContentArtifactSource` | `Runtime/BuildContent/Distribution/ContentArtifactSource.cs` | — | local / LAN directory と HTTP の取得口。同じ validation / install 経路 |
| `ContentDirectorySession` / `UnityContentDirectoryBackend` / `ContentDirectoryIndex` | `Runtime/BuildContent/` | 481 / 133 / 130 | directory の登録、native 操作、identity 解決 |
| `AssetManagement.SealAddressables` / `SealedAssetBackend` | `Runtime/AssetManagement/AssetManagement.cs` 74 行目、`Internal/SealedAssetBackend.cs` | 494（AssetManagement） | directory と決まった直後に Addressables の取得経路を 1 回だけ封じる |
| `BuildContentCoordinator` / `ContentTransportPublisher` | `Editor/Build/Content/`（asmdef `OneStarMaker.Build.Content`） | 188 / 143 | Content Directory build と transport publish |

### 2.3 SampleGame にある知識（Framework へ interface で渡すもの）

| 知識 | 場所 | 行数 |
|---|---|---|
| 宣言の一覧と初回 Scene（全て `Title`）、companion set の既定 | `SampleGame/DependOnAll/Editor/Build/SampleGameBuildPurposes.cs` | 78 |
| 宣言 → config key=value と環境変数名（`SAMPLEGAME_` + key）、`Full` は `assets:sceneVariant` を置かない | `PurposeLaunchSettings.cs` | 101 |
| Play As の前提条件の検査と `EnterPlaymode` | `PlayAsLauncher.cs` | 176 |
| 検証 Play の段（`Start → Produce → Install → BeforePlay → Bridge → EnterPlaymode`） | `VerifyPlayLauncher.cs` | 499 |
| 1 回の Play で戻す / 手で戻す ledger（EditorPrefs `OSM.ContentDelivery.PlayBridge.v1`） | `ContentDeliveryPlayBridge.cs` | 234 |
| Delivery 窓 | `ContentDeliveryWindow.cs` | 212 |
| Content build の入口と BS4 Player | `SampleGameContentBuild.cs`、`SampleGamePlayerBuildCoordinator.cs` | 148 / 152 |

### 2.4 現況で成立していない前提

- **source と directory の同居。** 起動は source（Addressables 互換）か directory かを BeforeSceneLoad で 1 つ選び、directory なら Addressables を封じる（§4.7「directory mode の Addressables 封鎖」）。表現は起動時に固定し、同一 session の表現切替は提供しない（[§18](../../unity/Assets/Docs/Architecture/18-asset-description.md)「同一 session 内で Whitebox 要求を Full entry へ fallback した後に…`EntryAmbiguous`」）。「編集中の Scene だけ source、残りは directory」は今の Runtime に無い。
- **`sourceFiles` の範囲。** manifest の `sourceFiles` は「checkout 案内」用で（[§20](../../unity/Assets/Docs/Architecture/20-variant-checkout-workflow.md)「sourceFiles の Missing / Changed は checkout 案内として扱う」）、宣言の到達集合の全 source file を含む契約ではない。差分判定の根拠にするには契約の格上げが要る。
- **到達集合だけを出す入口。** `BuildPurposeResolver` は build の一部としてしか呼ばれない。「build せずに、この宣言に必要な source path の集合」を返す Editor 入口が無い。
- **publish の供給。** [§20](../../unity/Assets/Docs/Architecture/20-variant-checkout-workflow.md)「前段の通常 Editor Play 向け publish 入口は未整備」。CI が develop の head ごとに build → publish していないので、LAN の連鎖は現場が供給しない限り空である。
- **VCS の抽象。** Framework は Git も SVN も知らない。`OneStarMaker > Variant > Checkout Report` は案内のみで checkout を代行しない。

## 3. 要求（UX。A1 の入力）

owner との対話で確定した要求。設計の自由度は残すが、次の性質は各スライスの A1 で守る。

### 3.1 利用者が決めるのは「作業空間」1 つ

- 利用者が選ぶのは宣言（= 作業空間）だけ。選ぶ頻度は週に数回。Play のたびには選ばない。初期値は前回使った宣言、初回は Game 側 provider の既定。
- 宣言を選ぶと 2 つが同時に決まる。(1) VCS から手元に置く source の範囲 = 宣言の到達集合。(2) それ以外を埋める build 済み content の鍵 = 同じ宣言名。
- **source を取るかどうかは Play の条件にしない。** 遊ぶだけの人は source 無しで build 済み content だけで Play できる。source の取得は「編集する人」の動詞で、宣言を切り替えた時に 1 回だけ聞く（取得して切替 / 取得せず切替 / 中止）。
- **「どの Scene が自分の source か」は聞かない。** working copy の file の sha と、build 済み content の manifest の `sourceFiles` の sha を比べて、「手元の方が違う Scene」を機械的に出す。自分が編集した Scene も、VCS update で他人の変更が入った Scene も、同じ「content が古い Scene」として扱う。

### 3.2 Play 時の解決は「差分を埋める」

Play が要求された時、Editor 層が次の順で解決する。Runtime の契約（未指定で黙って動かない、Addressables へ暗黙 fallback しない）は変えず、Editor 層が解決結果を明示の verified pair と overlay 集合として Runtime に渡す。「勝手にやる」のは Editor 層で、Runtime は疑い深いまま。

1. 宣言に一致する build 済み content を、手元 cache → local object store（Artifacts の `content/<contentSet>/<target>/<revision>/`）→ LAN（設定した published directory の root）の順で探す。Cloud は対象外（D5）。
2. 見つかったら manifest の `sourceFiles` と working copy を比べ、差分 Scene の集合を出す。
3. 差分が空なら無言で directory Play。
4. 差分があれば、差分 Scene だけ source から、残りは directory から読む（**Runtime の source overlay**。D4）。Play は止めず、Overlay に件数を出すだけ。
5. content が 1 つも無い時だけ Build を促す（§3.3）。
6. 一致する content が複数あれば known-good pin を優先する。revision 名の新旧は使わず、「最新を取得した」とも表示しない（§20 の契約）。

### 3.3 Build は「無い時だけ、1 回だけ、Play に繋げて」

- Build を利用者に見せるのは、content がどこにも無い時に限る（D7。全員が CI / LAN の供給がある現場に居るわけではない）。1 画面で、探した場所、Build に source が要ること、所要時間の目安、`[Build して Play]` `[source のまま Play]` `[中止]` を出す。source を取っていない人には `[取得して Build して Play]` に変わる。
- Build が終わったら黙って local object store に publish する。次回から手元 cache に一致する content があるので無言で Play に入る。LAN の共有先へも置くかは Project Settings の toggle 1 つで、Build のたびには聞かない。
- **古い content しか無い時は促さない。** 差分が多いだけなら overlay で通し、Overlay に「content が古い Scene が N 件。Build すると速くなります」を非ブロックで出す。Build を促す dialog は「無い」時だけ。

### 3.4 通知は 3 段。既定は無言（D6）

| 段 | 出し方 | 条件 |
|---|---|---|
| 無言 | 何も出さない。Console に Info 1 行と Overlay の 1 行 | 解決が一意に決まり Play に入れる。前回と同じ宣言で LAN から取る（進捗は Overlay）。Framework が自分で直せる前提条件のずれ（Variant profile、ledger、Play Mode Script）を黙って直す |
| 非ブロック | Overlay か Console の Warning 1 行。Play は止めない | 複数候補から known-good で選んだ。`ContentDeclarationMismatch`。経路の縮退。差分 Scene の件数 |
| ブロック | dialog。Play を止める | 自分の編集が content に反映されない分岐（source で Play / cache で Play）。content が無く source Play へ落とす時（経路が変わるので 1 回聞く）。直せない前提条件（外の環境変数・コマンドライン）。取得の失敗（hash 不一致、budget 超過、全経路 404） |

- ブロックの dialog には「この条件では今後聞かない」を付け、条件の鍵（宣言名、差分 Scene の sha 集合、失敗の種類）ごとに覚える。鍵が変われば聞き直す。
- Scene view の Overlay 1 行を「今どの宣言で、どこから来た content で動いているか」の常設表示にする。通知を減らすほど、見たい時に見える場所が要る。宣言の変更もこの Overlay の Popup から。

### 3.5 Framework / Game の責務

- **Framework Editor（新設）:** content の解決（連鎖と差分判定）、Play ボタンの割り込み（`EditorApplication.playModeStateChanged` の `ExitingEditMode` で `content:runtimeMode` がどの層にも無い時だけ介入し、どこかの層に値があれば意図した設定として通す）、dialog と Overlay、Content Sources の設定（cache root、disk budget、LAN root、store root。利用者ごとの値で git 管理外）。宣言と manifest の語彙だけで書き、SampleGame を知らない。置き場所は [ASSET_BUILDSYSTEM_INDEPENDENCE.md](ASSET_BUILDSYSTEM_INDEPENDENCE.md) §2.1 の連携部分（AssetManagement と BuildSystem の両方を知る側）。
- **Game 側が実装する interface:** 宣言の一覧と既定、宣言 → 初回 Scene、宣言 → config key と環境変数名の写像。今 `PurposeLaunchSettings` と `SampleGameBuildPurposes` にある知識そのもの。Game の Editor asmdef が `InitializeOnLoad` で Framework に登録し、依存の向きは Game → Framework のまま。
- **VCS の抽象:** Framework は「この宣言に必要な path の集合を手元に置け」「この path 集合のうち手元の sha はこれ」の 2 つだけを interface で受ける。SVN 実装は Game 側か `tools/` に置く（D8。asset は SVN、source code は Git。宣言は Scene 単位なので asset 側だけで閉じる）。
- **既存入口の行き先:** `Play As` 6 menu と `Verify Play` 6 menu は Play 割り込みに吸収。`Content/Build <宣言>` は CI と担当者用に残す。Delivery 窓の 10 欄は「特定 revision を指定して再現したい QA」向けの詳細へ格下げし、日常の経路から消す。

## 4. 制約と常時契約

- `AGENTS.md` の常時契約を全て守る。特に: 依存は Game → Framework の一方向で asmdef 参照の追加は設計判断、アセットは `IAssetManagement` 経由、Editor コードを Runtime asmdef に置かない、Unity 側で `record` を使わない、`#nullable enable`、偽 null、テストで `Task.Delay` / `Thread.Sleep` を使わない。
- **Runtime の起動契約は変えない。** `content:runtimeMode` 未指定で黙って動かない、Addressables へ暗黙 fallback しない、未知 mode は失敗、protected key は Player で拒否、は維持する。Editor 層が選んだ結果は常に明示の config として Runtime に入り、Editor 層は選んだ理由を Console に 1 行で出す。§4.7 の「known-good の自動適用はしない」は Runtime の文言で、Editor 層の解決との境界は S3 の A1 で明文化する（§7 Q1）。
- **表現は起動時に固定**のまま。overlay は表現の切替ではなく、同じ表現の source entry を directory entry より優先する読み口である（S1 の A1 で `EntryAmbiguous` との整合を決める）。
- **Player は対象外。** overlay は Editor Play 限定。BS4 Player の required JSON、protected key、smoke は触らない。
- **Cloud（R2）は対象外**（D5）。連鎖は手元 cache → local object store → LAN で閉じる。Artifacts README の「Unity を起動せず、Unity 側からも自動起動しません」は変えない。
- 既存の HANDOFF との順序: S1 の Phase B は `RUNTIME_REFACTOR_CONTENT_DIRECTORY_STARTUP` の Phase B と同時に走らせない。
- `tools/path-budget.json` の予算に従う。新設 file の path 長を A1 で確認する。

## 5. owner 決定（2026-11-07）

| ID | 決定 | 却下した案 |
|---|---|---|
| D1 | `app-config.json` に `content:runtimeMode=addressables` を置いて Play 直押しを動かす案は採らない | Addressables を既定にする |
| D2 | Framework 側に置く。SampleGame に寄せない | SampleGame の Editor 拡張として Play As 窓を作る |
| D3 | 利用者に Build を意識させない。手元にあればそれを読み、無ければ local store → LAN から取る | 利用者が Build / Verify Play / Delivery を選ぶ |
| D4 | 差分 Scene は **Runtime の source overlay**（(a)）で読む。Play 前に差分だけ build して積む (b) は退路として記録に残す | (b) を本線にする |
| D5 | Cloud（R2）経路は後続で決める。本 program の対象外 | 連鎖に Cloud を含める |
| D6 | 通知は本当に必要な警告だけ。Play のたびの popup は出さない | Play のたびに確認 panel を出す |
| D7 | content が無い現場では Build を促してよい | Build を一切見せない |
| D8 | source code は Git、asset は SVN。宣言が SVN の取得範囲を決める | VCS を固定しない抽象だけ |

## 6. スライス列

各スライスは `slice` の HANDOFF を切り、1 スライス 1 ブランチ。問いは各 A1 で確定する。順序の理由は依存: S2 が無いと S3 の差分判定ができず、S1 が無いと S3 の差分 Scene を読めない。S2 は Editor のみで S1 と独立に進められるので先に置き、S1 の A1 と並行してよい。

| 順 | スライス（HANDOFF 名の候補） | 答える問い | 主な変更対象 | risk |
|---|---|---|---|---|
| S2 | `PLAY_RESOLUTION_SOURCE_SET` | 宣言から、build せずに到達集合の source path 一覧を Editor で出せるか。manifest の `sourceFiles` をその集合の全 file を含む契約に格上げして、`ContentSourceAdvisor` で「手元の方が違う Scene」を正しく出せるか | `Editor/Build/Selection/`（到達集合の dry-run 入口）、`ContentTransportPublisher`（`sourceFiles` の範囲）、`ContentSourceAdvisor`、[§18](../../unity/Assets/Docs/Architecture/18-asset-description.md) の transport 契約 | normal |
| S1 | `RUNTIME_SOURCE_OVERLAY` | directory 登録済みの Editor Play で、指定した Scene identity の集合だけを source（AssetDatabase）から読み、残りを directory から読めるか。表現の固定、Addressables の封じ、台帳（`EntryAmbiguous`）、終了順序を保ったまま | `AssetManagement.cs`（読み口の優先）、`SealedAssetBackend`（overlay 集合だけ通す）、`AbstractApplicationInitializer` または `ContentDirectoryStartup`（overlay 集合を config の `content:` 予約 key から受ける）、[§4.7](../../unity/Assets/Docs/Architecture/04-app-startup.md) | high |
| S3 | `PLAY_RESOLVER_AND_INTERCEPT` | Play ボタンを押すだけで、一致する content が手元 cache / local store / LAN にあれば無言で directory Play に入り、差分 Scene が overlay で読まれるか。Game 側は provider interface の実装だけで済むか | Framework Editor（新設: resolver、Play 割り込み、provider interface、Content Sources 設定）、`PurposeLaunchSettings` / `SampleGameBuildPurposes`（provider 実装へ）、`PlayAsLauncher` / `ContentDeliveryPlayBridge`（吸収） | high |
| S4 | `PLAY_BUILD_PROMPT` | content が無い時だけ 1 画面で Build を促し、Build 後に local store へ黙って publish して、次回から無言で Play に入れるか | Framework Editor（dialog、`BuildContentCoordinator` と `ContentTransportPublisher` の呼び出し、store への collect 相当）、`VerifyPlayLauncher`（吸収） | normal |
| S5 | `WORKSPACE_SOURCE_CHECKOUT` | 宣言の切替時に 1 回だけ source の取得を聞き、VCS 抽象の interface 経由で到達集合を手元に置けるか。SVN 実装を Framework の外に置けるか | Framework Editor（切替 dialog、VCS interface）、SVN 実装（Game 側か `tools/`） | normal |
| S6 | `PLAY_OVERLAY_AND_CLEANUP` | Overlay の常設表示、通知の 3 段と「今後聞かない」の記憶、Delivery 窓の格下げ、menu の整理、README の「試す」の書き換えで、初見の人が Play を押してから 1 分以内に正しい宣言で Play に入れるか | Framework Editor（Overlay）、`ContentDeliveryWindow`、menu、`README.md`、§18 / §20 / §04 の harvest | low |

- **S1 を最初にしない理由:** S1 は Runtime の高リスク変更で、A1 に時間がかかる。S2 は Editor だけで閉じ、S3 の差分判定の前提になる。S2 を先に出して S1 の A1 と並行する。
- **S3 の Phase B は S1 の Phase D の後。** overlay が無い状態で S3 を入れると、差分 Scene がある Play を「source のまま Play」へ落とすしか無く、D3 を満たさない。S3 の A1 は S1 の A3 の後に始めてよい。
- **S4 と S5 は S3 の後なら並行可。**
- **次スライスへ進む最低条件（program の gate）:** S2 の Phase D で「`sourceFiles` が到達集合の全 source を含み、1 Scene を編集した working copy で `Changed` がその Scene だけに出る」証拠。S1 の Phase D で「directory 登録済みの Play で、overlay 集合の Scene だけ AssetDatabase の変更が反映され、集合外は directory から読まれ、封じの契約と終了順序の既存テストが通る」証拠。S3 の Phase D で「`Title.unity` を開いて Play を押すだけで、LAN に content がある状態では dialog 無しで directory Play に入る」証拠。これらを満たさないまま次へ進まない。

## 7. 未決事項（各スライスの A1 へ）

- **Q1（S3）:** §4.7「known-good の自動適用はしない。app-config の未指定を directory に書き換えない」と、Editor 層の resolver が pair を書く行為の境界。案: Runtime の文言は変えず、Editor 層は「選んだ理由を Console と Overlay に 1 行出すので黙ってはいない」と §18 §4 に書く。
- **Q2（S1）:** overlay 集合を Runtime に渡す形。config の `content:` 予約 key（例: `content:sourceOverlay`、Scene identity の列）か、Editor 専用の bridge か。Player の protected key の集合に入れない。
- **Q3（S1）:** overlay の Scene を AssetDatabase から読む時、Addressables の `Use Asset Database` に依存するか、`EditorSceneManager` 直か。封じた backend を部分的に開けるのか、別の read-through backend を重ねるのか。
- **Q4（S2）:** `sourceFiles` に入れる範囲。Scene file だけか、Scene が参照する asset（prefab、material、texture）まで含めるか。後者は manifest が大きくなるが、差分判定の精度が上がる。到達集合の計算は `BuildPurposeResolver` の出力から source path へ写像できるか。
- **Q5（S3）:** working copy の sha 計算の時間。Spring の全 source file を Play のたびに hash するのは重い可能性がある。mtime + size の短絡や、VCS の status を使うか（S5 の VCS 抽象との境界）。
- **Q6（S4）:** Build 後に local object store へ置く形式。Artifacts の `content collect` と同じ layout（`content/<contentSet>/<target>/<revision>/`）を Unity から直接書くのか、`tools/artifacts.ps1` を子 process で呼ぶのか。後者は Artifacts README の「Unity 側から自動起動しない」契約に触れる。
- **Q7（S5）:** SVN sparse checkout の単位。宣言の到達集合は Scene file の集合だが、SVN は directory 単位の depth で取る。Scene が参照する asset の directory まで広げる規則。
- **Q8（S6）:** `OneStarMaker/` と `Tools/OSM/` の二重 root を 1 つにするか。asmdef の境界が理由なら、利用者向けの menu だけ Framework 側に寄せられるか。
- **Q9（program）:** CI publish の供給。[§18](../../unity/Assets/Docs/Architecture/18-asset-description.md)「CI 組み込み（BS5b-1 から）」が所有する。本 program は供給が無い現場を D7 で救うが、供給がある現場の体験は CI publish が無いと成立しない。順序と所有を owner が決める。
- **Q10（後続、本 program の対象外）:** Cloud（R2）経路、取得の先読み（pull 時のバックグラウンド取得。現状の fetch は Spring 221 object で 742 秒）、Scene 単位の増分 build（D4 の退路 (b) に相当）、Player での overlay。

## 8. この文書の終了

本書は program の地図で、各スライスの HANDOFF が切られるごとに §6 の行をその HANDOFF へのリンクに置き換える。全スライスの Phase D が終わるか、owner が program を打ち切った時点で、§3 の要求と §5 の決定のうち恒久的なものを §0 の harvest 先へ移し、本書を削除する。
