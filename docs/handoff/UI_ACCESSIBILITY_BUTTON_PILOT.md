# UI Accessibility: Button 最初の実証候補

## 0. 状態と権限

- type: slice
- status: A（A1 proposed。未承認候補、実装キューではない）
- branch: `codex/ui-accessibility-button-plan` / PR base: `develop`
- implementation base commit: `6d804ca637cf42fb876e602c8ccc0656cfbd255d`
- implementation head commit: 未実装
- risk: high（公開 API、表示寿命、OS との接続）
- owner: root（計画統合。実行・検証担当は A3 で別途合意）
- created: 2026-10-06
- expires: 2026-11-05 または置換 revision の成立時。期限時は owner が更新・廃止を判断する
- harvest to: `unity/Assets/Docs/Architecture/06-ui.md`。実装後に実証した契約だけを移す
- Phase A snapshot: A2 入力固定時に commit / 生成時刻 / hash を追記。A3 凍結 snapshot は未作成
- Phase B result / evidence / C' blind bundle: 未作成。各 Phase で取得先・時刻・hash を記録する

現在の承認は、この cloud 上の文書作成・独立計画レビュー・Draft PR まで。
この文書の承認・マージは、機能の A3 凍結、Phase B、Unity 実行、利用者の Windows 操作、機能 PR のマージを承認しない。
今の差分は本書と必要な文書索引だけ。Runtime、asset、`.meta`、asmdef は変更しない。
未承認候補なので文書 PR のマージ時には保持し、候補の解決・置換時に harvest / 削除する。

## 1. A0: 問い、現況、境界

**問い:** 既存 UISystem の表示寿命に沿った一つの無害な Button を、Unity 型なしの名前・有効状態・操作契約から、Windows Player の Narrator が読み、通常入力と同じ操作を実行できるか。

現況は上記 base のソースを確認したもの。

- `UICommon` は単一 PanelRenderer の6レイヤーへ `UIToolkitView.Root` を挿入する。`RemoveUIToolkitView` は ViewOut 後に Root を外すが View 自体を破棄しない。
- `UIToolkitView.Track` は OnDestroy で binding → ViewModel → Root の順に解放する。表示終了・detach の解除を Track だけで代用できない。
- `BindingExtensions.BindClick` は Button.clicked と Action を結び、BindVisible は display を変える。プラットフォームの semantic node との同期はない。
- TitleView / Title.uxml は既存 UISystem 経由で表示される。既存 start-button はシーン遷移、pulse-button は演出であり、どちらも最初の無害な実証用操作には転用しない。
- Unity は `6000.6.0f1`。Foundation の asmdef は `noEngineReferences: false`。今回の共通モデルだけを Unity 型非依存にし、Foundation 全体が Unity 非依存とは主張しない。
- [PR #95](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/95) は別の open Draft（確認 head `3d045236`）、名前・hint と初期 focus の lane。本候補は develop から独立し、同 PR の merge / cherry-pick / close / 修正を行わない。TitleView・UIToolkitView の重なりは将来着手前に再確認するが、#95 を前提にしない。

[Unity 6000.6 の公式導入例](https://docs.unity3d.com/6000.6/Documentation/Manual/accessibility/screen-readers-get-started.html) は UITK Button と AccessibilityHierarchy / Node、invoked、Windows Narrator による Player 検証を示す。
API が存在することは、本アプリの実機成功の証拠ではない。
[AccessibilityNode](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Accessibility.AccessibilityNode.html) の表示要素からの独立性と WindowsPlayer 対応、[AccessibilityHierarchy](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Accessibility.AccessibilityHierarchy.html) の変更通知・主ウィンドウ限定を前提にする。

対象外: World の §30 / §31、グローバル意味レジストリ、新描画木、汎用 Manager、uGUI backend、Input / Script / Sound の拡張、全画面対応、他 OS、配布・性能一般化。
Toggle / Slider は「UI control 拡張候補」、設定の200%表示は「UI 設定候補」、modal の focus 復帰は「UI modal 候補」が所有する後続の問い。いずれも未承認で、新 A0〜A3 なしに着手しない。

## 2. A1: 最低条件と停止位置

文書納品と将来の機能受入を分ける。

- **文書納品:** 本候補を自己完結させ、固定版への独立レビューと採否を残す。docs / contract audit と diff 検査の実績・未実行を記録し Draft PR にする。実機未確認でも文書納品は可能。
- **Gate 1:** 共通モデルの純 C# テストと Unity adapter の関連テストが成立する。これは設計の足場であり、スクリーンリーダ機能完成ではない。
- **Gate 2:** 承認した Windows 検証経路で、既存アプリ内の対象 Button の実読み上げと invoke を初めて実証し、下記の表示・状態・寿命条件を満たす。ここで実装拡張を止めてレビューへ渡す。
- **機能 GO:** Gate 1 / 2、最終 head の判定必須テスト、C / C' が揃い、現在の問いへの blocker がないこと。最初の読み上げだけで後続 control へ進めない。
- **未達 / blocked:** 未完了として原因と未確認を返す。経路がないことを成功・対象外・強行継続へ読み替えない。CONDITIONAL ACCEPT は設けない。

受入詳細（Gate 2 を構成する条件）:

1. 対象の名前と Button role を Narrator が読み、Narrator activate と通常の click / submit が同じ Action を1操作につき1回だけ実行する。
2. 表示名・enabled の変更は共通モデルから通常 UI と node に反映される。再 focus 時に最新名を読み、無効状態で操作は発生しない。変更のたびに無条件 TTS を追加しない。
3. 対象または祖先が非表示・無効、panel から detach、表示セッション終了、破棄後なら対象を探索・操作できない。enabledSelf だけで判定しない。
4. detach / reattach、表示終了 / 再表示、Narrator off / on で現在の対象だけが復元される。古い callback を保持して呼んでも操作・再登録が起きず、二重購読・二重実行がない。
5. 通常表示・入力と既存の6レイヤー、Track の解放順序を壊さない。画面ごとに activeHierarchy を上書きせず、終了時に他者の hierarchy を消さない。

## 3. 共通モデルと backend の契約案

### 共通 Button binding

Foundation の `UISystem/AccessibilityButtonBinding.cs` に、System 型だけの小さな sealed class を置く。
公開面は constructor `(string name, bool enabled, Action invoke)`、読み取り専用 Name / Enabled、`SetState(name, enabled)`、`Changed` 通知、`TryInvoke()`、`Dispose()` に限定する。
空・空白名は拒否。同値更新は通知しない。無効・Dispose 後の TryInvoke は false、action を呼ばない。Dispose は冪等で購読と action 参照を解放する。呼出しは main thread に限定する。
状態の権威はアプリ所有のこのモデル。UITK や Unity Accessibility 型、独自 role 一般化、focus tree、世界の ID は公開面へ出さない。
TryInvoke はモデルの enabled / disposal を守り、表示・祖先・世代の可否は adapter が直前にも確認する。

### UITK と Narrator の双方向接続

- `UIToolkitView` に protected `BindAccessibleButton(Button, AccessibilityButtonBinding)` を追加する案。返る IDisposable は Track へ登録する。Button 引数は既存 UITK 専用層内だけに閉じる。
- Runtime 内部の `UIToolkitButtonAccessibilityBinding` が、Name → Button.text / node.label、Enabled → Button.SetEnabled / node.state を同期し、通常 clicked と node.invoked を同じ guarded TryInvoke に結ぶ。別の NavigationSubmitEvent を重ねて送信しない。
- 適用可否は、表示セッションが有効、現在世代、GameObject 有効、panel 接続、対象と祖先の resolved display / visibility、enabledInHierarchy を合わせて決める。祖先 opacity 0 と面積0の frame も非表示として除外する。単なる別要素との視覚的重なりの一般判定は対象外。
- 不適格な node は isActive=false とし、enabled=false は Disabled 状態も同期する。無効化で通常入力も拒否する。解放時は node 除去・イベント解除・世代無効化を行う。
- `GeometryChangedEvent` と attach / detach、モデル Changed を契機に同期する。祖先 style 変更をイベントだけで取りこぼさないよう、backend の一つの UpdateSystem 登録から main-thread apply で有効性と frame を差分確認する案とする。毎フレーム Q() や tree 再生成はしない。
- frame は worldBound と panel の pixel scale から算出する。モデル値・可視性・frame の変更をまとめて通知し、変更がない tick は通知しない。active hierarchy の node 増減・frame 変更は SendLayoutChanged、表示開始は SendScreenChanged を使う。名前・enabled 更新時も同期後の layout 通知を行い、OS 上の結果は Gate 2 で確認する。

### hierarchy の所有者と表示寿命

`UICommon` が内部 `UIToolkitAccessibilityBackend` を一つ所有し、その一つの AccessibilityHierarchy だけを AssistiveSupport へ接続する。scope は既存共通 UI ルートの App 寿命内。各 View は自分の明示 binding のみを提供し、backend を生成しない。

- UICommon の Add は ViewIn 成功後に表示セッションを有効化する。Remove は ViewOut 開始前に無効化する。Add 失敗・キャンセルでも有効化しない。
- Panel reload / detach では即時に該当 binding を無効化し、再接続時には現セッションの新世代として再投影する。同じ View の再表示も新世代にする。
- UICommon disable では全表示投影と OS 購読を停止、enable では現在の entries から復元。View destroy は Track 経由で最終解放し、モデルを破棄する前に callback を止める。
- Narrator off で Unity が activeHierarchy を解除するため、on 時は現在有効な binding のみから再接続する。終了時は activeHierarchy が自分のものの場合だけ解除する。
- 他者の activeHierarchy が既にある場合は奪わず、競合を報告して本接続を停止する。複数 UICommon を調停する global registry は作らない。
- UICommon の既存 entries に Modal〜Loading の背面遮断がある間は、背面の pilot 投影を停止する最小判定だけを行う。modal の focus stack / 復帰先決定は追加しない。

更新登録は既存 `UpdateSystemRuntime.RegisterElement` / `RequestElementApply` を使い、backend 専用の内部 layer ID と順序を置く。新 MonoBehaviour.Update や独自 timer は作らない。登録不成立は接続失敗として返し、裏で別経路へ切り替えない。

## 4. 実証 fixture と責務配置

既存 Title に development build 専用・`--ui-accessibility-button-pilot` 指定時だけ出す fixture を置く案。
Title.uxml に対象 button、親コンテナ、結果 label、検証補助ボタンを宣言し、TitleView.OnRootCreated で一度だけ取得する。レイアウトは UXML が所有する。
`TitleAccessibilityPilot` が Name / Enabled とメモリ内 counter を所有し、対象「カウントを増やす」の1回の操作で counter が1増える。結果を label と Player log に残す。保存・通信・シーン遷移・音は行わない。
検証補助ボタンは対象の親コンテナの外に置き、名前変更、enabled、親 display / visibility / enabled、対象 detach / reattach を切り替える。対象以外は semantic node にしない。
表示セッションの Remove / Add、失敗・破棄・古い delegate 注入は Unity テストで検証する。実 Player の閉じて再表示する到達手段は検証経路確認時に選び、支援が追加で必要なら A3 前に配置と予算を確定する。

下表の行数は base 実測、増分は計画値。先頭 `F` = `unity/Assets/OneStarMaker/Scripts/Foundation/`、`R` = `unity/Assets/OneStarMaker/Scripts/Runtime/UISystem/`、`T` = `unity/Assets/OneStarMaker/Tests/`、`G` = `unity/Assets/SampleGame/OutGame/Title/`。

| 対象 | 現在 → 予想増分 | 責務、所有者・寿命、依存、テスト境界 |
|---|---:|---|
| F `UISystem/AccessibilityButtonBinding.cs`（新規） | 0 → +80〜120 | 名前・enabled・invoke policy。View 所有 / Bind(go) 相当。System のみ、公開面は §3。source-linked 純 C# テスト |
| R `Accessibility/UIToolkitButtonAccessibilityBinding.cs`（新規） | 0 → +160〜220 | 一つの Button の projection と guarded callback。View 所有、表示世代で suspend、Track で最終 dispose。Foundation / UITK / Accessibility、内部型、Unity テスト |
| R `Accessibility/UIToolkitAccessibilityBackend.cs`（新規） | 0 → +180〜240 | hierarchy と OS 接続、通知、更新登録。UICommon 所有 / App。Runtime UpdateSystem / UITK / Accessibility、内部型、Unity テストと Player |
| R `UICommon.cs` | 571 → +35〜55 | 既存 entries と表示境界から backend を配線。App、既存 View 依存。OS node の詳細は置かない。UICommonUIToolkitTests |
| R `UIToolkitView.cs` | 194 → +35〜55 | binding 登録口、内部表示セッション、破棄順序。Bind(go)、Runtime 内部と Foundation。既存 public API の意味を変えない |
| G `TitleView.cs` | 124 → +15〜25 | opt-in 判定・fixture 配線のみ。既存 View 寿命。Game → Framework、Unity テスト |
| G `Title.uxml` | 10 → +10〜20 | fixture の表示構造のみ。Title asset / Scene。既存 UXML、Editor / Player 表示確認 |
| G `TitleAccessibilityPilot.cs`（新規） | 0 → +100〜160 | counter・検証状態・画面結果。Title View 所有。Foundation / Runtime / UITK、Game 内部、アプリテスト |
| T `UISystem/UICommonUIToolkitTests.cs` | 135 → +50〜80 | 表示開始終了・失敗・reload の既存契約回帰。test ごとの View / UICommon、既存 Tests assembly |
| T `UISystem/UIToolkitViewLifecycleTests.cs` | 70 → +25〜40 | Track 解放順序・旧 callback 拒否。test 所有、既存 Tests assembly |
| T `UISystem/UIToolkitAccessibilityTests.cs`（新規） | 0 → +200〜280 | model / node 対応、祖先条件、detach、OS toggle、競合。test 所有、既存 Tests assembly |
| `unity/Assets/SampleGame/Tests/UIAccessibilityPilotTests.cs`（新規） | 0 → +80〜120 | アプリ fixture の同一 action・counter・opt-in。既存 SampleGame.Tests、Game → Framework |
| `tools/UIAccessibilityOfflineTests/UIAccessibilityOfflineTests.csproj` / `Program.cs`（新規） | 各0 → +20 / +120〜180 | 共通モデルだけ source-link。既存 offline runner 方式、.NET 8 / System のみ、プロセス寿命 |

namespace は配置に対応する Foundation.UISystem、Runtime.UISystem.Accessibility、SampleGame.OutGame.Title。既存 asmdef 内に置き、参照追加や新 asmdef は不要という提案。
UICommon は既に500行超だが、追加は既存表示 orchestration に限定し OS I/O は新 backend へ分離する。三つの別責務を一クラスへ集めない。
Title.uxml と小さな test は50%以上増加し得るが、同一 fixture / 同一寿命契約なので行数だけの分割をしない。新規ファイルは policy / OS I/O / 要素 projection / Game fixture の依存・所有者差で分ける。
検証を理由に Framework → Game、Runtime → Editor を作らない。新 .cs の `.meta` は将来実装で通常管理するが、今回の文書差分には含めない。

## 5. 検証経路と A3 blocker

**現状は Linux cloud の docs-only 作業。Windows 実行、Narrator 操作、音声取得、証拠の受け渡しは未確認。**
前回接続された利用者の Windows desktop は、今回の実行先として承認されていない。接続履歴を実行許可や疎通証拠にしない。利用者が手動で検証することも暗黙に割り当てない。

A3 前に次を同じ候補へ記録する必要がある。

1. 権限のある Windows 実行環境、実行・観察担当と操作方法。Unity 6000.6.0f1 / Windows Player の build・起動・Title 到達、Narrator off / on / focus / activate、fixture 状態切替を誰が行うか。
2. 音声を誰が何で観察するか。録音を C / C' が再生して再評価する経路、または明示合意した観察者の一次記録を受理する方式を選ぶ。字幕・node 値・成功ログだけを実読み上げの代用にしない。
3. 固定 implementation head、Player / content の識別、OS・Narrator 設定、操作順、counter 前後、音声または観察原記録、スクリーンショット、Player log を取得・保存する経路。C / C' の受取側で閲覧・hash 確認できること。
4. 未知の既存操作経路だけを小さく疎通する。Unity test / build を伴うため凍結前に試せないなら「未確認。初回確認は Phase C」と明記し、担当、最初の確認地点、必要支援、不成立時の基盤整備または問いの分離を人間と A3 で合意する。

現在は全て未決。将来の実行担当を名指しできるまで A3 を凍結しない。接続できただけで実アプリ到達や音声取得まで成功と扱わない。

将来の検証順序:

- 純 C#: `dotnet run --project tools/UIAccessibilityOfflineTests`。同値更新、Changed、無効時拒否、dispose、action1回を検査。Unity 型なしでコンパイルできる範囲をこの一モデルに限定する。
- 発見 C の起点: `pwsh tools/run-tests.ps1 -Filter 'OneStarMaker.Tests.UISystem|SampleGame.Tests.UIAccessibilityPilotTests'`。XML で必要集合と1件以上の実行を確認。filter の調整は凍結条件への根拠を残す。
- Unity 統合: attach された実 panel で ancestor style / enabled、frame、reload、表示終了と破棄の区別、Narrator status の再接続、他 hierarchy 非干渉を検査。fake の node 呼出しを OS invoke の証拠にしない。
- 判定 C: GO 候補の最終 head で純 C#、関連 Unity テスト、`pwsh tools/run-tests.ps1 -Filter ''` による全 EditMode 回帰、および Windows Player / Narrator の Gate 2。全 EditMode の適用除外はなし。
- B は実装・限定 Editor 操作・compile 確認まで。Unity test / build と実機判定は C。H2 の他 task 用 B 限定許可は流用しない。
- Windows Unity test は最初から承認済み sandbox 外経路。既存 Editor と対象 project を確認し、標準 runner を使う。`unity test` / `unity run` は使わない。
- 既知の native 終了 stall の再調査予算は設けない。新規 task として回数をリセットせず、既存停止規則を継続し、結果 XML・終了状態・受容判断を分ける。
- C / C' は同一 base / head の証拠を使う。C' は C の所見を含まない blind bundle と未関与の別モデルまたは合意した人間を使い、未実施を PASS にしない。

## 6. 実装停止規則・計画レビュー

凍結後は §2 の条件または常時契約への違反だけを現スライスの blocker とし、他の問いは §1 の後続候補へ送る。
計画外の公開 API、依存・asmdef、所有者、寿命、UI 更新経路、fixture 到達支援が必要なら B を止め、新 revision の Phase A へ返す。便宜的な Manager へ押し込まない。
Game → Framework、Runtime / Editor 分離、既存14値の SceneState、IAssetManagement / AssetOwner、公開ログ ILogger<T> を維持する。Unity C# は `#nullable enable`、record 禁止、偽 null を考慮。テストで Task.Delay / Thread.Sleep を使わない。

- A0/A1: 主担当 root、文書担当 OpenAI エージェント。モデル識別は固定入力作成時に実績を追記する
- A2: TBD。固定した同一 A0/A1 を独立セッションへ渡し、少なくとも責務・寿命・依存の architecture review と、実証経路・受入条件の review を分ける。互いの指摘は見せない
- A3: TBD。人間と主担当が全指摘を採用・不採用・保留に分類し理由を記録する。§5 の未決解消または明示した Phase C 初回確認条件の合意が必要
- C' 用担当: 未選定。A2 で利用可能な全モデル系列を使い切らず予約する
- A3 後の例外承認: なし

現在の文書検査: 作成時の `git diff --check` は成功。docs / contract audit は未実行で、root が既存の承認済み PowerShell 7.6.6 環境から文書固定後に `pwsh tools/docs-audit.ps1`、`pwsh tools/contract-audit.ps1` を実行し結果を記録する。未実行を合格へ読み替えない。

## 7. Phase C

未実施

## 8. Phase C'

未実施

## 9. 後続 Phase の記録

Phase B は未着手、Unity compile / test / build / Player 検証は未実行。Phase D の機能マージ判断も未実施。
実装開始時に承認済み A3 snapshot を固定し、B result と C / C' evidence の取得先・時刻・hash、担当モデル、未確認をその Phase で追記する。現在の文書レビューを機能の C / C' に数えない。
