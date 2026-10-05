# Phase A snapshot — UI Toolkit の初期フォーカスと名前・ヒント

- snapshot id: `ui-toolkit-a11y-focus-phase-a`
- generated at: 2026-10-05T14:17:15Z
- implementation base: `f0b4b1d022a213603d8ce52d036f31795a9eb2cd`
- この文書は A1 初稿である。A2 の複数モデルレビューと A3 の人間による採否は、このセッションでは行っていない。凍結済み A3 ではない。
- 発見 C / 判定 C の結論、指摘、疑念は載せない。

## A0

- 現況: UISystem は `UIView` / `UIToolkitView` / `UICommon`、MVVM の `BindText` / `BindClick` / `BindVisible`、Behavior を持つ。フォーカスやアクセシビリティ文言の helper は無い。`ConfirmDialog` は Dialog レイヤーの実サンプルで、UXML に `tabindex` / `focusable` が無い。
- 要求: UIToolkit 画面のアクセシビリティを UISystem の責務で進める。同じスライスで、(1) VisualElement の名前とヒントを Track できる形で付ける、(2) Dialog / Modal の Toolkit ビューは ViewIn の後に最初の focusable または名前付き既定へフォーカスする、(3) ConfirmDialog はメッセージ / OK / Cancel に明示的なタブ順と名前を持ち、Blocker はフォーカス不可、(4) EditMode テストを置く。
- 制約: フォーカス・ヒント・ラベルは UISystem 単独で、UITK の `focusable` / `tabIndex` を使う。§30 の `BindAccessible` と世界の個体レジストリは復活させない。Input のリマップ UI、片手用 Input プロファイル、§31 の読み上げと出力予算、§29 のエフェクト合成は実装しない。HpGauge、ScriptSystem、InputSystem、SoundSystem は編集しない。開いている PR #94 の変更ファイルと重ねない。
- Unity 6000.6.0f1 の `VisualElement` に accessibility name / hint プロパティは無い。読み上げ用の公式 API は別階層の `AccessibilityNode` であり、`AssistiveSupport` へ接続すると出力側（§31）に入る。
- `VisualElementFocusRing` はレイアウト updater が `HierarchyDisplayed` を立てるまで候補を空にする。ViewIn 完了直後はそのフラグが無いことがある。
- 正の `tabIndex` は昇順で 0 より前に並ぶ。負の `tabIndex` はタブ順から外れる。0 同士は子の深さ優先。これはエンジンの `FocusRingSort` と同じ規則である。
- 対象外: ViewOut で開く前のフォーカスへ戻すこと。スクリーンリーダーへの投影。Input の再割当。世界の巡回。
- 未決だったこと: エンジンに名前プロパティが無いときの保持場所。この初稿では要素ローカルの側表に決める。A2/A3 は未実施なので、この決定は人間の凍結ではない。

## このスライスが答える問い

UI Toolkit の Dialog / Modal は、世界レジストリを使わず、初期フォーカスとタブ順、および要素に紐づく名前とヒントだけで、ConfirmDialog を次の操作対象まで用意できるか。

## 進める最低条件

1. `UIAccessibilityText` が VisualElement に名前と任意のヒントを付け、返した `IDisposable` を外すとその代入だけが消える。`UIToolkitView.Track` に渡せる。
2. Dialog と Modal の `UIToolkitView` は、`UICommon` が ViewIn に成功したあと、名前付き既定がタブ順に乗っていればそこへ、無ければタブ順の先頭へ `Focus` を試みる。それ以外のレイヤーは対象にしない。パネルが無ければ例外にしない。
3. ConfirmDialog のメッセージはタブ順の外、OK は `tabIndex` 1、Cancel は `tabIndex` 2。メッセージ名は表示文に追従し、OK / Cancel はそれぞれの名前とヒントを持つ。初期フォーカスの名前は `ok-button`。
4. Toolkit の Blocker は `focusable = false` かつ `tabIndex = -1` で、タブ順に入らない。
5. 上記を EditMode テストが固定する。判定時の全 EditMode 回帰は別条件（判定必須）であり、この最低条件のテスト集合はその中に含まれる。

## 受け入れ条件

- `Set` は名前とヒントを `TryGet` で読め、Dispose で消える。後から足した代入を残したまま先の代入を外しても、新しい方が残る。
- `Bind` は名前（と任意のヒント）の Observable に追従し、Dispose 後は更新しない。
- View の Track に載せた代入は、View の破棄で外れる。
- タブ順は正の `tabIndex` 昇順、その後 `tabIndex` 0 の深さ優先。負の `tabIndex`、非 focusable、`display: none`、無効な枝は入らない。
- 名前付き既定がタブ順に無ければ先頭へ戻る。
- `ShouldApply` は Modal と Dialog だけ true。Loading は false。
- ConfirmDialog の UXML と View 初期化後の文言が、上の名前・タブ順と一致する。破棄後は文言が残らない。
- Blocker 生成結果が focusable でなく `tabIndex` が -1 であり、隣のボタンだけのタブ順になる。

## ここでは答えない問い

- ViewOut で、開く前にフォーカスしていた要素へ戻すか。所有者を別に持つ必要があり、このスライスでは実装しない。後続スライスは未作成。
- 側表の名前とヒントを `AssistiveSupport` / `AccessibilityNode` へ投影し、スクリーンリーダーが読むか。§31 の出力予算とは混ぜない。後続スライスは未作成。
- Input のリマップ、片手プロファイル、世界の Focusable 巡回。§32 の InputManager 側。このリポジトリの Input 実装スライスには入れない。

## 判定定義

- GO: 上の進める最低条件を満たし、判定必須の全 EditMode（空 filter）が 1 件以上実行かつ失敗 0 で、常時契約とこの境界に反する欠陥が残っていない。
- NO-GO: 最低条件が未達、またはその条件か常時契約に反する欠陥が残っている。
- この初稿は A3 で凍結していない。A3 の人間採否が記録されるまで、実装差分だけでは GO にしない。
- 停止規則: 最低条件を満たし、現在の問いに致命的な反証が無ければ終了する。最低条件未達のまま GO にしない。

## A3 後の例外承認

なし。A3 自体が未実施。

## 本文へ転記した実装制約

- 依存は Game から Framework の一方向。asmdef 参照は足さない。
- 購読と要素に触れる `IDisposable` は `UIToolkitView.Track` へ集める。
- 新規・編集する Unity 側 `.cs` の先頭は `#nullable enable`。Unity 側で `record` を使わない。
- 破棄されうる `UnityEngine.Object` の null 判定に `?.` / `??` / `is null` を使わない。
- テストで `Task.Delay` / `Thread.Sleep` を使わない。
- 名前 helper は `BindAccessible` という名前にしない。世界の StableId、Flags、Registry へ登録しない。
- `AssistiveSupport.activeHierarchy` は触らない。
- 初期フォーカスは ViewIn の成功後。入場演出の途中では当てない。
- ViewOut のフォーカス復帰はしない。
- HpGauge、ScriptSystem、InputSystem、SoundSystem のファイルは変更しない。

## 責務マップ

| ファイル | 責務 | 変更理由 | 所有者・寿命 | 依存 | 公開面 | テスト | 行数 |
|---|---|---|---|---|---|---|---|
| `UIAccessibilityText.cs` | 要素ローカルの名前とヒントの着脱 | 新規。UITK にプロパティが無い | 代入の `IDisposable`。View なら Track の寿命 | UIElements、R3。レジストリ無し | `Set` / `Bind` / `TryGet` | EditMode。パネル不要 | 新規 約 145。単一責務 |
| `UIToolkitInitialFocus.cs` | タブ順の選択と `Focus` の試行 | 新規。選択規則を UICommon から離す | 状態を持たない。フォーカス実体はパネルの FocusController | UIElements の `focusable` / `tabIndex` / `Focus` | `ShouldApply` / `Select` / `TryApply`。列挙は internal | EditMode | 新規 約 202。単一責務 |
| `UIToolkitView.cs` | 派生が初期フォーカス名を宣言し、Modal/Dialog のときだけ適用する | 名前は画面の知識。適用タイミングの入口 | ビューインスタンス。名前は状態を保存しない | 同アセンブリの選択関数 | `protected` の名前、`internal` の適用 | TestToolkitView 経由 | 194 → 214（+20、50% 未満） |
| `UICommon.cs` | ViewIn 成功後に適用を呼ぶ。Blocker をタブ順から外す | 追加手順の唯一の場所が Add 経路だから | 既存の UI ルート。Blocker はエントリの寿命 | 既存 | 既存の Add。Blocker の 2 プロパティ | 既存 Blocker テストを拡張 | 571 → 577。元から 500 行警報の上。増分は呼び出しと Blocker の 2 代入。選択算法は移した。Add 手順を割るとオーケストレーションが裂けるので非分割 |
| `ConfirmDialog.uxml` | OK=1、Cancel=2、メッセージはタブ順外 | 静的なタブ順は UXML が権威 | アセット | 無し | UXML 属性 | CloneTree で読む | 数行 |
| `ConfirmDialogView.cs` | 文言の Track と初期フォーカス名 `ok-button` | サンプルが契約の利用者 | 既存ビュー寿命 | UISystem の helper。逆依存なし | 既存の `Decided`。フォーカス名は protected | Editor テスト | 105 → 116 |
| テスト 3 本と TestToolkitView、UICommon テスト | 上記の観測 | 新規と既存 1 本の拡張 | テスト | 既存テストアセンブリ。asmdef 参照は増やさない | 無し | それ自身 | 警報未満 |

分割警報: 500 行・3 責務・50% 増は、UICommon の既存超過だけが該当する。今回の増分は新しい責務の本体ではなく、既にある Add からの呼び出しである。新規 2 ファイルに算法と側表を分けた。

## 実装計画

- 順序: 側表 → タブ順選択 → UIToolkitView の名前と適用 → UICommon の呼び出しと Blocker → ConfirmDialog → EditMode テスト。
- Phase B から Phase A へ戻す条件: 新しい asmdef 参照、新しい所有者、AssistiveSupport、または単体テストできない中核が必要になったとき。
- 対象外を維持する方法: 変更ファイルを UISystem、ConfirmDialog、そのテストに限る。`BindAccessible`、Registry、Input、Sound、Script、HpGauge を編集しない。

## テスト計画

- 単体（EditMode）: `UIAccessibilityTextTests`、`UIToolkitInitialFocusTests`、`ConfirmDialogAccessibilityTests`、`CreateToolkitBlocker_SetsNamePickingModeAndFullscreenStyle`。
- 差し戻し中の起点 filter: `UIAccessibilityTextTests|UIToolkitInitialFocusTests|ConfirmDialogAccessibilityTests|CreateToolkitBlocker_SetsNamePickingModeAndFullscreenStyle`
- 判定必須: 空 filter の全 EditMode。適用除外は無し。
- 操作・目視: 合否は要素の `tabIndex`、`focusable`、側表の文字列、パネルがあるときの `focusedElement`。見た目の判定は不要。
- 未知の操作経路: このクラウド VM に Unity Editor 6000.6.0f1 は無い。`tools/run-tests.ps1` の成功は未確認。初回の成功確認は、その Editor がある環境の判定 C。不成立がテスト失敗なら実装を直す。Editor 不在は環境不足であり、実装の成功に読み替えない。
- 人間の目視: なし。
- 機械検査: `pwsh tools/contract-audit.ps1`。
- A0/A1: このクラウドセッション。モデルは Grok 4.7。ベンダーは Cursor。
- A2: 実施していない。アーキテクチャゲート担当も別モデルで見ていない。
- A3: 実施していない。採否記録は無い。
- C' の予約: 無し。このセッションが A1 と実装に関与している。AI で C' するなら、実装および Phase C と異なるモデルの新規セッションが要る。人間担当ならモデル差は不要。このセッションでは C' を実行しない。
- 独立性を満たせない理由: 同一セッションが計画と実装を続け、A2 を起動していない。
