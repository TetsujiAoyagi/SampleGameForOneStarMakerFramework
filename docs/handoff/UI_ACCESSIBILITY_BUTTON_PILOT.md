# UI Accessibility: Button 最初の実証候補

## 0. 状態と権限
- type: slice
- status: A（A1 revised / A2・PR コメントを照合。A3 未承認、実装キューではない）
- branch: `codex/ui-accessibility-button-plan` / PR base: `develop`
- implementation base commit: `6d804ca637cf42fb876e602c8ccc0656cfbd255d`
- implementation head commit: 未実装
- risk: high（公開 API、表示寿命、OS との接続）
- owner: root（計画統合。実行・検証担当は A3 で別途合意）
- created: 2026-10-06
- expires: 2026-11-05 または置換 revision の成立時。期限時は owner が更新・廃止を判断する
- harvest to: `unity/Assets/Docs/Architecture/06-ui.md`。実装後に実証した契約だけを移す
- A2 固定入力: 公開 commit `e3e27c69c91890ee3c19f03c140942514c0d34ef`、tree `5fdd5f55b2722cc119a63a259785dc5b90bc0961`。同一 tree のローカル `547dd6b04912cddbaccae6abfa65dd791e67e350` を 2026-10-06 16:15:51 UTC に固定
- A2 入力本文 SHA-256: `f9c291c90d44bc56d2607690fe5c7c32d1470ef707b9f30c9d928131031ccefe`。本改稿はレビュー後の統合案であり、A3 凍結 snapshot は未作成
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
- Unity は `6000.6.0f1`。Runtime の Input / Sound にも System 型だけの policy と source-linked offline テストの先例がある。今回も共通モデルのファイル単位で Unity 型非依存を守り、assembly 全体の非依存は主張しない。
- [PR #95](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/95) は別の open Draft（確認 head `3d045236`）、名前・hint と初期 focus の lane。本候補は develop から独立し、同 PR の merge / cherry-pick / close / 修正を行わない。TitleView・UIToolkitView の重なりは将来着手前に再確認するが、#95 を前提にしない。

[Unity 6000.6 の公式導入例](https://docs.unity3d.com/6000.6/Documentation/Manual/accessibility/screen-readers-get-started.html) は UITK Button と AccessibilityHierarchy / Node、invoked、Windows Narrator による Player 検証を示す。
API が存在することは、本アプリの実機成功の証拠ではない。
[AccessibilityNode](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Accessibility.AccessibilityNode.html) の表示要素からの独立性と WindowsPlayer 対応、[AccessibilityHierarchy](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Accessibility.AccessibilityHierarchy.html) の変更通知・主ウィンドウ限定を前提にする。

対象外: World の §30 / §31、グローバル意味レジストリ、新描画木、汎用 Manager、uGUI backend、Input / Script / Sound の拡張、全画面対応、他 OS、配布・性能一般化。
Toggle / Slider は「UI control 拡張候補」、設定の200%表示は「UI 設定候補」、modal の focus 復帰・背面経路は「UI modal 候補」が所有する後続の問い。自動 panel reload 復元・Narrator on 復元・任意 opacity / zero-size の広い style 行列は「UI projection resilience 候補」へ送る。いずれも未承認で、新 A0〜A3 なしに着手しない。

## 2. A1: 最低条件と停止位置
文書納品と将来の機能受入を分ける。

- **文書納品:** 本候補を自己完結させ、固定版への独立レビューと採否を残す。docs / contract audit と diff 検査の実績・未実行を記録し Draft PR にする。実機未確認でも文書納品は可能。
- **Gate 1:** 共通モデルの純 C# テストと Unity adapter の関連テストが成立する。これは設計の足場であり、スクリーンリーダ機能完成ではない。
- **Gate 2:** 承認した Windows 検証経路で、既存アプリ内の対象 Button の実読み上げと invoke を初めて実証し、下記の表示・状態・寿命条件を満たす。ここで実装拡張を止めてレビューへ渡す。
- **機能 GO:** Gate 1 / 2、最終 head の判定必須テスト、C / C' が揃い、現在の問いへの blocker がないこと。最初の読み上げだけで後続 control へ進めない。
- **未達 / blocked:** 未完了として原因と未確認を返す。経路がないことを成功・対象外・強行継続へ読み替えない。CONDITIONAL ACCEPT は設けない。

受入詳細（最初の実証は Narrator を先に ON にし、既知の Title 親レイアウト・対象1個から始める）:

1. 対象の名前と Button role を Narrator が読み、Narrator activate と通常の click / submit が同じ Action を1操作につき1回だけ実行する。
2. 表示名・enabled の変更は共通モデルから通常 UI と node に反映される。再 focus 時に最新名を読み、無効状態で操作は発生しない。変更のたびに無条件 TTS を追加しない。
3. 制御された show / hide と親 enabled 切替では非適格 node を公開しない。対象・祖先の display / visibility / enabled、panel 接続、表示セッションを同期時と invoke 直前に検査する。任意の CSS 変更を瞬時に検知する一般保証はしない（§3）。
4. detach / reattach と明示した表示終了 / 再表示は新セッションにする。旧 callback は再表示後も無効。Narrator OFF、予期しない panel reload / modal / root disable では永久に現セッションを失効させ、ON / enable だけで自動復元しない。再試行は明示した close / reopen から行う。
5. 通常表示・入力と既存の6レイヤー、Track の解放順序を壊さない。画面ごとに activeHierarchy を上書きせず、終了時に他者の hierarchy を消さない。

## 3. 共通モデルと backend の契約案

### 共通 Button binding
Runtime の `UISystem/Accessibility/AccessibilityButtonBinding.cs` に、System 型だけの小さな sealed class を置く。UISystem の契約として配置し、このファイルを source-link する。
公開面は constructor `(string name, bool enabled, Action invoke)`、読み取り専用 Name / Enabled、`SetState(name, enabled)`、`Changed` 通知、`TryInvoke()`、`Dispose()` に限定する。
空・空白名は拒否。同値更新は通知しない。無効・Dispose 後の TryInvoke は false、action を呼ばない。Dispose は冪等で購読と action 参照を解放する。呼出しは main thread に限定する。
Name / Enabled / invoke の正本は TitleAccessibilityPilot が持つ唯一の binding。pilot は counter と binding を持つだけで UI 参照や状態の二重コピーを持たない。UITK / Unity Accessibility 型、汎用 role、focus tree、世界の ID は公開面へ出さない。
TryInvoke はモデルの enabled / disposal を守り、表示・祖先・世代の可否は adapter が直前にも確認する。

### UITK と Narrator の双方向接続
- `UIToolkitView` に protected `BindAccessibleButton(Button, AccessibilityButtonBinding)` を追加する案。返る IDisposable は Track に登録。OnRootCreated 時は View 内の pending binding 集合に保持し、node・OS 接続はまだ作らない。Button 型は UITK 専用層に閉じる。
- Runtime 内部の `UIToolkitButtonAccessibilityBinding` が、Name → Button.text / node.label、Enabled → Button.SetEnabled / node.state を同期する。通常 clicked と node.invoked は同じ guarded TryInvoke を同期呼出しする。invoked は実行結果の bool を返す。導入例の NavigationSubmitEvent 転送は併用しない。
- adapter は初期化時に main thread の identity を保持し、callback の最初に確認する。別 thread なら Unity / モデルへ触れず、invoked は false、clicked は無操作とし、action を queue しない。[invoked の成功値](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Accessibility.AccessibilityNode-invoked.html) を RequestElementApply の受付値で代用しない。API に thread 保証がないことは off-thread 障害の実測ではない。実 Windows callback がこの経路に適合しなければ Phase A へ戻し、blocking dispatch を足さない。
- 各表示・接続セッションで不変の token を capture した新 delegate を作る。suspend 時は旧 token を永久失効し購読解除する。再接続で同じ token や mutable な「現在世代参照」だけの delegate を再利用しない。旧 delegate は再表示後も操作・再登録できない。
- 同期時と invoke 直前に、現セッション、GameObject 有効、panel 接続、対象・祖先の resolved display / visibility、enabledInHierarchy を検査する。非適格 node は isActive=false、enabled=false は Disabled も反映。終了時は node 除去・イベント解除を行う。
- 既知の fixture の親 hide / 無効操作ではモデル Enabled=true を保ち、親の style / enabled だけを変える。次の main-thread apply で resolved 値を確認して投影を同期し、その同期完了後に node 非公開・操作拒否を検査する。show / enable 後も同じ同期地点で再公開する。モデル無効化で祖先判定の欠陥を隠さず、反映前の瞬時同期は保証しない。
- 外部から任意に変更された祖先 style は次の main-thread apply まで投影に遅延し得る。invoke は直前検査で即時拒否するが、それだけで一般的な不可視 node 非公開を保証しない。任意 style の即時同期・opacity 行列は本候補の実証範囲外。
- モデル Changed、GeometryChangedEvent、attach / detach と、backend 一つの UpdateSystem main-thread apply で状態・frame を差分同期する。毎フレーム Q() / tree 再生成はしない。layout 後の worldBound × panel.scaledPixelsPerPoint を frame へ直接代入し、frameGetter / RefreshNodeFrames は使わない。
- active hierarchy の差分反映後は `AssistiveSupport.notificationDispatcher.SendLayoutChanged(null)` にまとめ、名前 / enabled 変更で nodeToFocus を指定しない（[API](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Accessibility.IAccessibilityNotificationDispatcher.SendLayoutChanged.html)）。[activeHierarchy 代入](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Accessibility.AssistiveSupport-activeHierarchy.html) 自体の SendScreenChanged(null) に追加通知を重ねない。無変更 tick は通知0件。OS の最新読み上げは Gate 2 で観察する。

### hierarchy 所有者と登録 handshake
UICommon が内部 `UIToolkitAccessibilityBackend` を一つ App 寿命内で所有し、一つの AccessibilityHierarchy だけを接続する。各 View は backend を作らない。明示 binding がない間は dormant とし、node 作成、activeHierarchy 設定、global OS event 購読、更新登録を行わない。公開可能 node が0件なら activeHierarchy を代入せず、最後の公開 node が消えた時 / UICommon disable 時は自分の hierarchy の場合だけ null にする。hidden だが有効な session は再表示検出用の更新監視を保持してよい。

1. UICommon.Add が Root を初期化して pending binding を得た後、Runtime 内部メソッドで View と backend を結ぶ。ViewIn 成功後に、同じ Add token、現在の entries 所属、未取消を再確認して表示セッションを確定する。entries は候補の所在であり有効性の権威ではない。
2. BehaviorRunner が OperationCanceledException を吸収するため、await の正常 return だけで成功判定しない。`ct.IsCancellationRequested` と token 失効を確認し、遅れて完了した古い Add を再活性化しない。
3. 最初の適格 binding で既存 UpdateSystemRuntime.RegisterElement を行う。true は登録受付であり稼働証拠ではない。OnElementStart と最初の main-thread apply / layout 確認後に node を公開する。host 不在・登録拒否は接続失敗を記録し非公開のまま。代替 timer や bootstrap bypass は作らず、再試行は明示 reopen に限定する。 UICommon.Add は登録受付後に戻り、OnElementStart / 初回 apply を await しない。SceneDirector の Stable 到達と UpdateSystem の起動を循環待ちにせず、node 公開だけを後続の更新へ遅延する。
4. Remove は ViewOut 前、Add 失敗・取消は cleanup 時に token を失効し、node・session 購読・backend 関連付けを解除する。View 内 pending 定義は再表示まで非活性で保持、Track による destroy で最終解放する。モデル破棄より先に binding を止める。
5. 対象の detach は旧 token を失効し、同じ有効 View の明示 reattach で新 token と delegate を作る。Reader OFF / 予期しない panel reload / modal / root disable は全 pilot session を失効する。生の entries、Reader ON、enable から復元せず、close / reopen を必要とする。
6. Narrator は opening 前から ON を前提とし、OFF 通知で登録と投影を解放する。終了時は activeHierarchy が自分のものの場合だけ解除。他者の hierarchy があれば奪わず、競合を結果に記録して first proof を未達とする。global registry は作らない。

投影更新は RequestElementApply から main-thread 適用し、invoke の同期結果とは分ける。内部 layer ID / 順序は backend 内に閉じる。新 MonoBehaviour.Update は作らない。modal の正常運用・背面選択・focus 復帰を、この中止境界の検査から拡張しない。

## 4. 実証 fixture と責務配置

既存 Title に development build 専用・`--ui-accessibility-button-pilot` 指定時だけ出す fixture を置く案。
Title.uxml の fixture 親は default display:none。非 development または flag なしなら OnRootCreated で任意の fixture 親があれば挿入前に除去し、fixture 子要素の必須 query と binding / counter / log 作成を省く。fixture が欠落・非表示でも既存 title-panel / start-button / pulse-button 配線は通常通り。Editor は通常 OFF、テストだけ明示 gate 入力。対象と補助表示は UXML 所有で、一度だけ取得する。
`TitleViewModel` が opt-in 時だけ `TitleAccessibilityPilot` を所有し、pilot が唯一の binding と counter を所有する。pilot を SetViewModel しない。対象「カウントを増やす」で counter が1増え、TitleView の購読が label / Player log へ反映する。Track が adapter と UI 購読を解放した後、TitleViewModel.DisposeCore が pilot → binding を Dispose する。保存・通信・シーン遷移・音は行わない。
検証補助ボタンは対象の親コンテナの外に置き、名前変更、enabled、親 display / visibility / enabled、対象 detach / reattach を切り替える。対象以外は semantic node にしない。
Remove / Add、失敗・破棄・旧 delegate 注入は Unity テストで検証する。実 Player の明示 close / reopen 到達手段は §5 で選ぶ。支援が必要なら A3 前に配置・予算を確定する。

下表の行数は base 実測、増分は再見積り。小ささや実装成立の証明ではない。先頭 `R` = `unity/Assets/OneStarMaker/Scripts/Runtime/UISystem/`、`T` = `unity/Assets/OneStarMaker/Tests/`、`G` = `unity/Assets/SampleGame/OutGame/Title/`。

| 対象 | 現在 → 予想増分 | 責務、所有者・寿命、依存、テスト境界 |
|---|---:|---|
| R `Accessibility/AccessibilityButtonBinding.cs`（新規） | 0 → +80〜120 | 名前・enabled・invoke の唯一の正本。pilot 所有、ViewModel の Dispose まで。System のみ、公開面は §3。source-linked 純 C# テスト |
| R `Accessibility/UIToolkitButtonAccessibilityBinding.cs`（新規） | 0 → +130〜190 | 一つの Button の projection と guarded callback。View 所有、表示世代で suspend、Track で最終 dispose。Runtime 内共通モデル / UITK / Accessibility、内部型、Unity テスト |
| R `Accessibility/UIToolkitAccessibilityBackend.cs`（新規） | 0 → +130〜190 | hierarchy と OS 接続、通知、更新登録。UICommon 所有 / App。Runtime UpdateSystem / UITK / Accessibility、内部型、Unity テストと Player |
| R `UICommon.cs` | 571 → +35〜55 | 既存 entries と表示境界から backend を配線。App、既存 View 依存。OS node の詳細は置かない。UICommonUIToolkitTests |
| R `UIToolkitView.cs` | 194 → +35〜55 | binding 登録口、内部表示セッション、破棄順序。View 寿命、Runtime 内部。既存 public API の意味を変えない |
| G `TitleView.cs` | 124 → +20〜35 | opt-in 判定・fixture 配線と結果表示。Track 所有の adapter / UI 購読、既存 View 寿命。Game → Framework、Unity テスト |
| G `TitleViewModel.cs` | 29 → +15〜25 | optional pilot の唯一の所有者、DisposeCore で解放。UI 参照なし、既存 ViewModel 寿命、アプリテスト |
| G `Title.uxml` | 10 → +10〜20 | fixture の表示構造のみ。Title asset / Scene。既存 UXML、Editor / Player 表示確認 |
| G `TitleAccessibilityPilot.cs`（新規） | 0 → +60〜90 | counter と binding を所有し操作を提供。TitleViewModel 所有 / Dispose、System と Runtime 共通モデルのみ。UI 参照なし、Game 内部、アプリテスト |
| T `UISystem/UICommonUIToolkitTests.cs` | 135 → +40〜70 | 表示開始終了・取消・失敗の契約回帰。test ごとの View / UICommon、既存 Tests assembly |
| T `UISystem/UIToolkitViewLifecycleTests.cs` | 70 → +25〜40 | Track 解放順序・旧 callback 拒否。test 所有、既存 Tests assembly |
| T `UISystem/UIToolkitAccessibilityTests.cs`（新規） | 0 → +160〜220 | model / node 対応、制御下の祖先条件、detach、OFF失効、競合。test 所有、既存 Tests assembly |
| `unity/Assets/SampleGame/Tests/UIAccessibilityPilotTests.cs`（新規） | 0 → +80〜120 | アプリ fixture の同一 action・counter・opt-in。既存 SampleGame.Tests、Game → Framework |
| `tools/UIAccessibilityOfflineTests/UIAccessibilityOfflineTests.csproj` / `Program.cs`（新規） | 各0 → +20 / +120〜180 | 共通モデルだけ source-link。既存 offline runner 方式、.NET 8 / System のみ、プロセス寿命 |

namespace は OneStarMaker.Runtime.UISystem.Accessibility、SampleGame.OutGame.Title。既存 asmdef 内に置き、参照追加や新 asmdef は不要という提案。
UICommon は既に500行超だが、追加は既存表示 orchestration に限定し OS I/O は新 backend へ分離する。三つの別責務を一クラスへ集めない。
Title.uxml と小さな test は50%以上増加し得るが、同一 fixture / 同一寿命契約なので行数だけの分割をしない。新規ファイルは policy / OS I/O / 要素 projection / Game fixture の依存・所有者差で分ける。
検証を理由に Framework → Game、Runtime → Editor を作らない。新 .cs の `.meta` は将来実装で通常管理するが、今回の文書差分には含めない。

## 5. 検証経路と A3 blocker

**現状は Linux cloud の docs-only 作業。Windows 実行、Narrator 操作、音声取得、証拠の受け渡しは未確認。**
前回接続された利用者の Windows desktop は、今回の実行先として承認されていない。接続履歴を実行許可や疎通証拠にしない。利用者が手動で検証することも暗黙に割り当てない。

A3 前に次を同じ候補へ記録する必要がある。

1. 権限のある Windows 実行環境、実行・観察担当と操作方法。Unity 6000.6.0f1 / Windows Player の build・起動・Title 到達、Narrator を先に ON、focus / activate、fixture 切替・明示 reopen を誰が行うか。
2. 音声を誰が何で観察するか。録音を C / C' が再生して再評価する経路、または明示合意した観察者の一次記録を受理する方式を選ぶ。字幕・node 値・成功ログだけを実読み上げの代用にしない。
3. 固定 implementation head、Player / content の識別、OS・Narrator 設定、操作順、counter 前後、音声または観察原記録、スクリーンショット、Player log を取得・保存する経路。C / C' の受取側で閲覧・hash 確認できること。
4. 未知の既存操作経路だけを小さく疎通する。Unity test / build を伴うため凍結前に試せないなら「未確認。初回確認は Phase C」と明記し、担当、最初の確認地点、必要支援、不成立時の基盤整備または問いの分離を人間と A3 で合意する。

現在は全て未決。次の具体的 milestone は、権限のある Windows 検証経路と担当・証拠方式を選び、疎通または明示した Phase C 初回確認条件を人間と合意して A3 可否を決めること。利用者の手作業を既定にしない。接続だけをアプリ到達・音声取得成功にしない。

| 条件 | 合否に使う最小証拠（取得は全て将来、経路は A3 未決） |
|---|---|
| §2-1 読み上げ / invoke | Narrator ON → pilot opening → focus で名前と role → activate。通常 click / submit と各1回の counter 前後、音声または合意した一次観察記録、Player log |
| §2-2 最新名 / enabled | 名前変更 → 再 focus の実音声。モデル disable 後は探索対象から外れ counter 不変、enable 後は実 activate で1回。保持した旧 invoke の拒否と Disabled 状態は Unity テストで別に確認する。node 値だけでは読み上げの証拠にしない |
| §2-3 / 4 表示境界 | Player で制御下 hide / show、detach / reattach、明示 close / reopen の探索・操作を確認。古い delegate、取消済み ViewIn、destroy、OFF / reload / modal / disable の失効は UIToolkitAccessibilityTests と UICommonUIToolkitTests の合成入力で確認 |
| §2-5 所有者 / 非 opt-in | Unity テストで foreign hierarchy 非上書き、Track 解放順、非 opt-in の表示・node・OS購読0件、fixture 欠落 / 非表示でも通常 Title 配線成功。通常 Title の Unity 回帰。fake を OS 成功と呼ばない |

A3 で上記順序の実際の操作ボタン・到達方法と証拠の受取方法を埋める。未観察の条件は未確認のまま残す。

将来の検証順序:

- 純 C#: `dotnet run --project tools/UIAccessibilityOfflineTests`。同値更新、Changed、無効時拒否、dispose、action1回を検査。Unity 型なしでコンパイルできる範囲をこの一モデルに限定する。
- 発見 C の起点: `pwsh tools/run-tests.ps1 -Filter 'OneStarMaker.Tests.UISystem|SampleGame.Tests.UIAccessibilityPilotTests'`。XML で必要集合と1件以上の実行を確認。filter の調整は凍結条件への根拠を残す。
- Unity 統合: attach された実 panel で制御下の祖先条件、frame 直接代入・通知回数と引数、表示終了と破棄、旧 token、別 thread 拒否・同期 invoke 戻り値、0 node 時の OS 解除、登録 handshake、予期しない境界の失効、他 hierarchy 非干渉を検査。fake の node 呼出しを OS invoke の証拠にしない。
- 判定 C: GO 候補の最終 head で純 C#、関連 Unity テスト、`pwsh tools/run-tests.ps1 -Filter ''` による全 EditMode 回帰、および Windows Player / Narrator の Gate 2。全 EditMode の適用除外はなし。
- B は実装・限定 Editor 操作・compile 確認まで。Unity test / build と実機判定は C。H2 の他 task 用 B 限定許可は流用しない。
- Windows Unity test は最初から承認済み sandbox 外経路。既存 Editor と対象 project を確認し、標準 runner を使う。`unity test` / `unity run` は使わない。
- 既知の native 終了 stall の再調査予算は設けない。新規 task として回数をリセットせず、既存停止規則を継続し、結果 XML・終了状態・受容判断を分ける。
- C / C' は同一 base / head の証拠を使う。C' は C の所見を含まない blind bundle と未関与の別モデルまたは合意した人間を使い、未実施を PASS にしない。

## 6. 実装停止規則・計画レビュー

凍結後は §2 の条件または常時契約への違反だけを現スライスの blocker とし、他の問いは §1 の後続候補へ送る。
共通モデルのファイルへ Unity 型が必要、または計画外の公開 API、依存・asmdef、所有者、寿命、UI 更新経路、fixture 到達支援が必要なら B を止め、新 revision の Phase A へ返す。便宜的な Manager へ押し込まない。
Game → Framework、Runtime / Editor 分離、既存14値の SceneState、IAssetManagement / AssetOwner、公開ログ ILogger<T> を維持する。Unity C# は `#nullable enable`、record 禁止、偽 null を考慮。テストで Task.Delay / Thread.Sleep を使わない。

- A0/A1: 主担当 root、文書担当 OpenAI エージェント。A2 固定入力は §0。本改稿の採否は主担当による提案で、人間の A3 合意ではない
- A2: 2026-10-06、architecture は configured `gpt-6-astra`、validation は configured `gpt-6-sol`。両者 OpenAI、同系列・別モデルの独立セッション。同一入力を読み互いの所見を見ずにレビューした。cross-vendor ではない
- A3: TBD。§5 の実行・観察経路の合意が blocker。C' 用担当は未選定、A3 後の例外承認なし

| A2 指摘 | 主担当の採否と反映理由 |
|---|---|
| Validation 1: 最初の実証が広過ぎる | 一部採用。安全・名前更新・detach / 明示 reopen は維持。自動復元、広い style 行列、modal 運用は後続候補へ。OFF 等は失効で止める |
| Validation 2: View / backend 登録順 | 採用。pending 定義 → 内部関連付け → 適格 session → 更新稼働確認 → 公開、失敗時解除を §3 に明記 |
| Validation 3: 古い callback | 採用。不変 token を capture した session 別 delegate と永久失効を明記 |
| Validation 4 / Architecture 2: opt-in | 採用。default-hidden・非対象は挿入前除去、binding / global OS 接続なしを明記 |
| Validation 5: 条件と証拠の対応 | 採用。Player 一次観察と合成 Unity 検証を §5 に分け、担当・経路は A3 blocker として残す |
| Architecture 1: entries / 取消 | 採用。entries だけで活性化せず、ViewIn 後の ct と同一 token、遅延完了を確認 |
| r2 Architecture: Stable との循環待ち | 採用。Add は更新開始・node 公開を await せず、公開だけを遅延する |
| r2 Validation: 親判定の偽陽性 | 採用。モデル Enabled=true のまま親だけを変更し、投影同期後に検証。モデル無効と祖先非適格を別の試験にする |

r2 固定入力 `0e1ca2a1edb16554543527d67b1e59b6f8fd84ee`（tree `569c6e7a6c6d51893113a9e06f8aa308e1d1f6c7`）も両 reviewer が個別再確認し追加指摘を上表へ反映。直前公開版 `e49e2349` の root 検査は docs111文書 / contract608 C#、双方 error0・警告0、diff-check 成功。本改稿の検査結果は PR 本文に記録する。文書検査は機能 C / C' ではない。

PR #97 の外部指摘を e49e2349 と現行 API に照合した採否（実行時不具合の証明や GitHub thread resolve ではない）:

| 指摘 | 採否・理由 |
|---|---|
| [所有者](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/97#discussion_r4197842008) | 採用。Runtime 内 pure model、ViewModel → pilot → binding の一意所有と解放順に統一 |
| [invoke](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/97#discussion_r4197842020) | 一部採用。同期成功値と thread 検査を明記。RequestElementApply への action queue は成功結果を返せないため不採用、別 thread は拒否 |
| [通知 / frame](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/97#discussion_r4197842029) | 採用。frame 直接代入、null 指定の差分通知、代入時通知との重複排除。未採用 API の障害を実在扱いしない。layer 数値は A3 の実行順確認で確定し、opacity 行列は後続 |
| [OS 寿命](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/97#discussion_r4197842040) | 採用。公開 node0件で接続しない / 最後の node で所有時のみ解除。自動 ON 復元は既定の後続候補に維持 |
| [modal](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/97#discussion_r4197842048) | 解消済み。e49e2349 の §2-4 / §3 / §5 が失効境界を定義。正常 modal 運用への拡張は後続 |
| [fixture](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/97#discussion_r4197842058) | 基本境界は解消済み、取得失敗回避を採用。非 opt-in の子 query を省き、通常 Title 配線を保持 |

## 7. Phase C
未実施

## 8. Phase C'
未実施

## 9. 後続 Phase の記録
Phase B は未着手、Unity compile / test / build / Player 検証は未実行。Phase D の機能マージ判断も未実施。
実装開始時に承認済み A3 snapshot を固定し、B result と C / C' evidence の取得先・時刻・hash、担当モデル、未確認をその Phase で追記する。現在の文書レビューを機能の C / C' に数えない。
